Shader "Custom/UI/VideoLumaKey"
{
    Properties
    {
        _MainTex    ("Video Texture", 2D) = "white" {}
        _Threshold  ("White Threshold",  Range(0, 1)) = 0.9
        _Softness   ("Edge Softness",    Range(0, 1)) = 0.1

        // UI Stencil (RectMask2D 等で使用)
        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID",         Float) = 0
        _StencilOp        ("Stencil Operation",  Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask",  Float) = 255
        _ColorMask        ("Color Mask",         Float) = 15
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
            "RenderPipeline"  = "UniversalPipeline"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ColorMask [_ColorMask]

        Pass
        {
            Name "VideoLumaKey"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Threshold;
                float  _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;       // RawImage.color (alpha 含む)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.color      = input.color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // BT.601 輝度計算.
                float luma = dot(col.rgb, float3(0.299, 0.587, 0.114));

                // Threshold 以上の輝度を透明に。Softness でエッジをなだらかにする.
                float lumaAlpha = 1.0 - smoothstep(_Threshold - _Softness, _Threshold, luma);

                // RawImage.color の tint + alpha を乗算.
                col.rgb *= input.color.rgb;
                col.a    = lumaAlpha * input.color.a;

                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
