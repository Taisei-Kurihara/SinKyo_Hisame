Shader "Custom/FullscreenBlackEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Blend ("Blend", Range(0, 1)) = 1
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

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float2 pos = float2(
                    (input.vertexID << 1) & 2,
                    input.vertexID & 2
                );

                output.positionHCS = float4(pos * 2.0 - 1.0, 0.0, 1.0);
                output.uv = pos;

                // Direct3Dのレンダーテクスチャ描画時UV反転補正.
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

                // 背景判定: alpha≈0 は背景 → 白塗り.
                if (color.a <= 0.01)
                    return half4(1, 1, 1, _Blend);

                // オブジェクト部分を黒塗り（_Blendで強度制御）.
                return half4(0, 0, 0, color.a * _Blend);
            }

            ENDHLSL
        }
    }
}