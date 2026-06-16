// UI Image をぼかすシェーダー.
// 4重リング Gaussian カーネル (計33サンプル).
// _BlurSize でぼかし強度を調整.
//
// 使い方:
//   1. このシェーダーのMaterialを作成
//   2. UI Image の Material に設定
//   3. BlurSize でぼかし強度を調整
Shader "Custom/UIBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurSize ("ぼかし強度", Range(0, 40)) = 5.0
        _BlurAlpha ("ブラー透過", Range(0, 1)) = 1.0
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
            Name "UIBlur"

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
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            half4 _Color;
            float _BlurSize;
            float _BlurAlpha;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 d1 = _BlurSize * _MainTex_TexelSize.xy;
                float2 d2 = d1 * 2.2;
                float2 d3 = d1 * 3.8;
                float2 d4 = d1 * 5.5;

                // 4重リング 33サンプル Gaussian (weights sum = 1.00).
                half4 col = half4(0, 0, 0, 0);

                // 中心 (1, w=0.08).
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * 0.08;

                // Ring1 d*1 (8, w=0.36).
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d1.x,  0   )) * 0.06;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d1.x,  0   )) * 0.06;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,     d1.y)) * 0.06;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,    -d1.y)) * 0.06;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d1.x,  d1.y)) * 0.03;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d1.x,  d1.y)) * 0.03;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d1.x, -d1.y)) * 0.03;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d1.x, -d1.y)) * 0.03;

                // Ring2 d*2.2 (8, w=0.28).
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d2.x,  0   )) * 0.045;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d2.x,  0   )) * 0.045;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,     d2.y)) * 0.045;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,    -d2.y)) * 0.045;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d2.x,  d2.y)) * 0.025;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d2.x,  d2.y)) * 0.025;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d2.x, -d2.y)) * 0.025;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d2.x, -d2.y)) * 0.025;

                // Ring3 d*3.8 (8, w=0.18).
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d3.x,  0   )) * 0.03;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d3.x,  0   )) * 0.03;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,     d3.y)) * 0.03;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,    -d3.y)) * 0.03;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d3.x,  d3.y)) * 0.015;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d3.x,  d3.y)) * 0.015;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d3.x, -d3.y)) * 0.015;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d3.x, -d3.y)) * 0.015;

                // Ring4 d*5.5 (8, w=0.10).
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d4.x,  0   )) * 0.018;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d4.x,  0   )) * 0.018;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,     d4.y)) * 0.018;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,    -d4.y)) * 0.018;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d4.x,  d4.y)) * 0.007;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d4.x,  d4.y)) * 0.007;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( d4.x, -d4.y)) * 0.007;
                col += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-d4.x, -d4.y)) * 0.007;

                col *= input.color;
                col.a *= _BlurAlpha;
                return col;
            }

            ENDHLSL
        }
    }
}
