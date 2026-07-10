using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

// FullscreenBlackEffectシェーダーをURP上で描画するRendererFeature.
// 静的プロパティで有効/無効・ブレンド値を制御する.
//
// 2つのモード:
//   FillEnabled=true  : alpha閾値で背景/オブジェクトを白/黒で塗りつぶし (_Blend で強度制御).
//   FillEnabled=false : グレースケール彩度除去モード (_GrayscaleRatio で白黒率制御).
public class FullscreenBlackEffectFeature : ScriptableRendererFeature
{
    [SerializeField] private Material material;

    // 静的制御プロパティ.
    public static bool IsEnabled { get; set; } = false;
    public static float Blend { get; set; } = 1f;

    // 白黒率 (0=通常カラー, 0.5=中間, 1=完全白黒).
    public static float GrayscaleRatio { get; set; } = 0f;

    // 塗りつぶしモード有効 (true=Fill, false=Grayscale).
    public static bool FillEnabled { get; set; } = true;

    // 塗りつぶし閾値 (alpha > threshold → 黒, alpha <= threshold → 白).
    public static float FillThreshold { get; set; } = 0.01f;

    // ドメインリロード無効時の静的フィールドリセット.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsEnabled = false;
        Blend = 1f;
        GrayscaleRatio = 0f;
        FillEnabled = true;
        FillThreshold = 0.01f;
    }

    private FullscreenBlackEffectPass pass;

    public override void Create()
    {
        pass = new FullscreenBlackEffectPass(material);
        pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!IsEnabled || material == null) return;
        renderer.EnqueuePass(pass);
    }

    // 描画パス.
    class FullscreenBlackEffectPass : ScriptableRenderPass
    {
        private Material mat;
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int GrayscaleRatioId = Shader.PropertyToID("_GrayscaleRatio");
        private static readonly int FillEnabledId = Shader.PropertyToID("_FillEnabled");
        private static readonly int FillThresholdId = Shader.PropertyToID("_FillThreshold");

        public FullscreenBlackEffectPass(Material material)
        {
            mat = material;
        }

        // シェーダーパラメータを一括設定.
        private void SetShaderProperties()
        {
            mat.SetFloat(BlendId, Blend);
            mat.SetFloat(GrayscaleRatioId, GrayscaleRatio);
            mat.SetFloat(FillEnabledId, FillEnabled ? 1f : 0f);
            mat.SetFloat(FillThresholdId, FillThreshold);
        }

#if UNITY_6000_0_OR_NEWER
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (mat == null) return;
            SetShaderProperties();

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraColor = resourceData.activeColorTexture;

            // カメラカラーのコピー作成（同一テクスチャの読み書き競合回避）.
            var desc = renderGraph.GetTextureDesc(cameraColor);
            desc.name = "_FullscreenBlackEffect_Temp";
            var tempTexture = renderGraph.CreateTexture(desc);

            // Pass1: カメラカラー → 一時テクスチャにコピー.
            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("FullscreenBlackEffect_Copy", out var copyData))
            {
                copyData.source = cameraColor;
                builder.UseTexture(cameraColor, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (CopyPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }

            // Pass2: エフェクト描画.
            using (var builder = renderGraph.AddRasterRenderPass<EffectPassData>("FullscreenBlackEffect", out var effectData))
            {
                effectData.material = mat;
                effectData.source = tempTexture;
                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(cameraColor, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (EffectPassData data, RasterGraphContext context) =>
                {
                    data.material.SetTexture(MainTexId, data.source);
                    context.cmd.DrawProcedural(
                        Matrix4x4.identity, data.material, 0,
                        MeshTopology.Triangles, 3);
                });
            }
        }

        class CopyPassData
        {
            public TextureHandle source;
        }

        class EffectPassData
        {
            public Material material;
            public TextureHandle source;
        }
#else
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (mat == null) return;

            SetShaderProperties();

            var cmd = CommandBufferPool.Get("FullscreenBlackEffect");
            cmd.DrawProcedural(
                Matrix4x4.identity, mat, 0,
                MeshTopology.Triangles, 3);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#endif
    }
}
