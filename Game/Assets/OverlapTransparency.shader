// 2つの画像(image1, image2)の重なりで透過するシェーダー.
// 透過式: result_alpha = image1.a * (1 - image2.a)
// image2のalphaが高いほど、image1は透明になる.
//
// 使い方:
//   1. このシェーダーのMaterialを作成
//   2. image1のSpriteRenderer (またはUI Image) にMaterialを設定
//   3. MaterialのMaskTexにimage2のテクスチャを設定
//   4. MaskTexのTiling/Offsetでimage2の位置・スケールを調整
Shader "Custom/OverlapTransparency"
{
    Properties
    {
        [PerRendererData] _MainTex ("Image1 (メイン画像)", 2D) = "white" {}
        _MaskTex ("Image2 (マスク画像)", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "OverlapTransparency"

            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 maskUV : TEXCOORD1;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            float4 _MaskTex_ST;

            half4 _Color;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                // Tilingを中心(0.5,0.5)基準でスケーリング + Offset適用.
                float2 centered = input.uv - 0.5;
                centered *= _MaskTex_ST.xy;
                output.maskUV = centered + 0.5 + _MaskTex_ST.zw;
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 mainCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 maskCol = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.maskUV);

                // メインにTintを適用.
                mainCol *= input.color;

                // 透過式: result_alpha = image1.a * (1 - image2.a)
                half resultAlpha = mainCol.a * (1.0 - maskCol.a);

                return half4(mainCol.rgb, resultAlpha);
            }

            ENDHLSL
        }
    }
}
