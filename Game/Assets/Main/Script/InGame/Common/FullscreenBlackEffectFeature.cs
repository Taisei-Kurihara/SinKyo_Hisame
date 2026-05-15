using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

// FullscreenBlackEffectシェーダーをURP上で描画するRendererFeature.
// 静的プロパティで有効/無効・ブレンド値を制御する.
// カメラカラーのalpha値で背景(alpha≈0)とオブジェクト(alpha>0)を判定し、
// オブジェクト部分のみ黒塗りする.
public class FullscreenBlackEffectFeature : ScriptableRendererFeature
{
    [SerializeField] private Material material;

    // 静的制御プロパティ.
    public static bool IsEnabled { get; set; } = false;
    public static float Blend { get; set; } = 1f;

    // ドメインリロード無効時の静的フィールドリセット.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsEnabled = false;
        Blend = 1f;
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

        public FullscreenBlackEffectPass(Material material)
        {
            mat = material;
        }

#if UNITY_6000_0_OR_NEWER
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (mat == null) return;
            mat.SetFloat(BlendId, Blend);

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

            // Pass2: 一時テクスチャのalpha判定で黒塗りエフェクト描画.
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

            mat.SetFloat(BlendId, Blend);

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
