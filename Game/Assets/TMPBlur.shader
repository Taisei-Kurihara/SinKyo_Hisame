// TextMeshPro テキストをぼかすシェーダー.
// SDF(符号付き距離場)を4重リング(33サンプル)でGaussianブラー.
// _BlurSize でぼかし強度を調整.
//
// 使い方:
//   1. TMPコンポーネントの既存Materialを選択 → 右クリック → Create → Material
//   2. 作成したMaterialのShaderを "TextMeshPro/Custom/Blur" に変更
//   3. TMPコンポーネントの Material Preset に設定
//   4. BlurSize でぼかし強度を調整
Shader "TextMeshPro/Custom/Blur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Font Atlas", 2D) = "white" {}
        _FaceColor ("Face Color", Color) = (1,1,1,1)
        _FaceDilate ("Face Dilate", Range(-1,1)) = 0

        _BlurSize ("ぼかし強度", Range(0, 30)) = 2.0
        _BlurAlpha ("ブラー透過", Range(0, 1)) = 1.0

        // TMP内部で使用されるプロパティ.
        _WeightNormal ("Weight Normal", float) = 0
        _WeightBold ("Weight Bold", float) = 0.5
        _ScaleRatioA ("Scale Ratio A", float) = 1
        _GradientScale ("Gradient Scale", float) = 10
        _TextureWidth ("Texture Width", float) = 512
        _TextureHeight ("Texture Height", float) = 512
        _ScaleX ("Scale X", float) = 1
        _ScaleY ("Scale Y", float) = 1

        // TMP内部が参照するプロパティ（未使用だが定義必須）.
        _OutlineWidth ("Outline Width", Range(0, 1)) = 0
        _OutlineSoftness ("Outline Softness", Range(0, 1)) = 0
        _CullMode ("Cull Mode", Float) = 0

        // UI Mask / Stencil.
        _Stencil ("Stencil ID", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            Name "TMPBlur"

            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask [_ColorMask]

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
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            half4 _FaceColor;
            float _FaceDilate;
            float _GradientScale;
            float _ScaleRatioA;
            float _WeightNormal;
            float _BlurSize;
            float _BlurAlpha;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 d1 = _BlurSize * _MainTex_TexelSize.xy;
                float2 d2 = d1 * 2.2;
                float2 d3 = d1 * 3.8;
                float2 d4 = d1 * 5.5;

                // SDF距離場を4重リング33サンプルGaussianブラー.
                half sd = 0;

                // 中心 (1, w=0.08).
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * 0.08;

                // Ring1 d*1 (8, w=0.36).
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d1.x,  0   )).a * 0.06;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d1.x,  0   )).a * 0.06;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,     d1.y)).a * 0.06;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,    -d1.y)).a * 0.06;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d1.x,  d1.y)).a * 0.03;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d1.x,  d1.y)).a * 0.03;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d1.x, -d1.y)).a * 0.03;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d1.x, -d1.y)).a * 0.03;

                // Ring2 d*2.2 (8, w=0.28).
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d2.x,  0   )).a * 0.045;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d2.x,  0   )).a * 0.045;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,     d2.y)).a * 0.045;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,    -d2.y)).a * 0.045;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d2.x,  d2.y)).a * 0.025;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d2.x,  d2.y)).a * 0.025;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d2.x, -d2.y)).a * 0.025;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d2.x, -d2.y)).a * 0.025;

                // Ring3 d*3.8 (8, w=0.18).
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d3.x,  0   )).a * 0.03;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d3.x,  0   )).a * 0.03;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,     d3.y)).a * 0.03;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,    -d3.y)).a * 0.03;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d3.x,  d3.y)).a * 0.015;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d3.x,  d3.y)).a * 0.015;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d3.x, -d3.y)).a * 0.015;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d3.x, -d3.y)).a * 0.015;

                // Ring4 d*5.5 (8, w=0.10).
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d4.x,  0   )).a * 0.018;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d4.x,  0   )).a * 0.018;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,     d4.y)).a * 0.018;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,    -d4.y)).a * 0.018;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d4.x,  d4.y)).a * 0.007;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d4.x,  d4.y)).a * 0.007;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d4.x, -d4.y)).a * 0.007;
                sd += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d4.x, -d4.y)).a * 0.007;

                // ぼかしたSDF → alpha変換.
                float weight = _WeightNormal * _ScaleRatioA;
                float dilate = _FaceDilate * _ScaleRatioA;
                float bias = 0.5 - weight - dilate;
                float softness = 0.05 + _BlurSize * 0.01;
                float alpha = smoothstep(bias - softness, bias + softness, sd);

                half4 col = _FaceColor * input.color;
                col.a *= alpha * _BlurAlpha;

                return col;
            }

            ENDHLSL
        }
    }
}
