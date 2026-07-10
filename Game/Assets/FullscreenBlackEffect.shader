Shader "Custom/FullscreenBlackEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Blend ("Blend", Range(0, 1)) = 1
        _GrayscaleRatio ("GrayscaleRatio", Range(0, 1)) = 0
        _FillEnabled ("FillEnabled", Float) = 1
        _FillThreshold ("FillThreshold", Range(0, 1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "FullscreenBlack"

            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _Blend;
            float _GrayscaleRatio;
            float _FillEnabled;
            float _FillThreshold;

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float2 pos = float2(
                    (input.vertexID << 1) & 2,
                    input.vertexID & 2
                );

                output.positionHCS = float4(pos * 2.0 - 1.0, 0.0, 1.0);
                output.uv = pos;

                // Direct3D UV Y-flip.
                #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1.0 - output.uv.y;
                #endif

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv
                );

                // Mode 1: Fill mode (alpha threshold).
                // alpha > threshold -> black, alpha <= threshold -> white.
                if (_FillEnabled > 0.5)
                {
                    if (color.a <= _FillThreshold)
                        return half4(1, 1, 1, _Blend);

                    return half4(0, 0, 0, color.a * _Blend);
                }

                // Mode 2: Grayscale desaturation.
                // _GrayscaleRatio: 0=normal, 0.5=halfway, 1=fully B&W (二値化: 000 or 111).
                // シェーダー内でlerp合成（アルファブレンド依存を排除）.
                float lum = dot(color.rgb, float3(0.299, 0.587, 0.114));
                half3 gray = half3(lum, lum, lum);
                // max時は二値化（完全白黒: rgb 000 or 111）.
                half bwVal = step(0.5, lum);
                half3 bw = half3(bwVal, bwVal, bwVal);
                // ratio < 1: 元色→グレースケール. ratio ≈ 1: グレースケール→二値化.
                half3 desaturated = lerp(gray, bw, saturate((_GrayscaleRatio - 0.8) * 5.0));
                half3 result = lerp(color.rgb, desaturated, _GrayscaleRatio);
                return half4(result, _GrayscaleRatio > 0.001 ? 1.0 : 0.0);
            }

            ENDHLSL
        }
    }
}