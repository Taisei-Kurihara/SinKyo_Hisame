Shader "Custom/FullscreenBlackEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float2 pos = float2(
                    (input.vertexID << 1) & 2,
                    input.vertexID & 2
                );

                output.positionHCS = float4(pos * 2.0 - 1.0, 0.0, 1.0);
                output.uv = pos;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv
                );

                // “§–¾•”•ª‚Í‚»‚Ì‚Ü‚Ü
                if (color.a <= 0.01)
                    return color;

                // •`‰æ‚³‚ê‚Ä‚¢‚é•”•ª‚ð•‰»
                return half4(0, 0, 0, color.a);
            }

            ENDHLSL
        }
    }
}