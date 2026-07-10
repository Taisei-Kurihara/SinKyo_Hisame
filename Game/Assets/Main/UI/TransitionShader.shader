Shader "Custom/UI/TransitionShader"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Mask2D ("Mask Texture", 2D) = "white" {}
        [Header(Fade)]
        _FadeAlpha ("Fade Alpha (0 hide 1 show)", Range(0, 1)) = 1
        _AlphaFloor ("Alpha Floor (below to 0)", Range(0, 1)) = 0.01
        _AlphaCeil ("Alpha Ceil (above to 1)", Range(0, 1)) = 0.99
        [Header(Mask Remap)]
        _MaskRemapMin ("Remap Min (below to 0) (0-255)", Range(0, 255)) = 0
        _MaskRemapMax ("Remap Max (above to 1) (0-255)", Range(0, 255)) = 255
        [Header(Mask Channel Select)]
        [Toggle] _UseR ("R", Float) = 0
        [Toggle] _UseG ("G", Float) = 0
        [Toggle] _UseB ("B", Float) = 0
        [Toggle] _UseA ("A", Float) = 1
        [KeywordEnum(Default, Reverse)] _PATTERN ("Pattern", Float) = 0
        [Header(Shape Mask)]
        [Toggle] _UseAlphaShape ("Use Alpha as Shape Mask", Float) = 1

        // UI Stencil (RectMask2D等で使用)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _PATTERN_DEFAULT _PATTERN_REVERSE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // Vertex Color (UI Image.color)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Mask2D);
            SAMPLER(sampler_Mask2D);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _FadeAlpha;
                float _AlphaFloor;
                float _AlphaCeil;
                float _MaskRemapMin;
                float _MaskRemapMax;
                float _UseR;
                float _UseG;
                float _UseB;
                float _UseA;
                float _UseAlphaShape;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // MainTex sampling + Vertex Color (Image.color) tint
                float4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 color = mainTex.rgb * input.color.rgb;

                // Mask sampling (選択チャンネルの平均)
                float4 maskColor = SAMPLE_TEXTURE2D(_Mask2D, sampler_Mask2D, input.uv);
                float maskSum = 0;
                float maskCount = 0;
                if (_UseR > 0.5) { maskSum += maskColor.r; maskCount += 1; }
                if (_UseG > 0.5) { maskSum += maskColor.g; maskCount += 1; }
                if (_UseB > 0.5) { maskSum += maskColor.b; maskCount += 1; }
                if (_UseA > 0.5) { maskSum += maskColor.a; maskCount += 1; }
                float maskAlpha = maskCount > 0 ? maskSum / maskCount : 0;
                // Remap (0-255スケール → 0-1)
                float maskMin = _MaskRemapMin / 255.0;
                float maskMax = _MaskRemapMax / 255.0;
                maskAlpha = saturate((maskAlpha - maskMin) / max(maskMax - maskMin, 0.0001));

                // Transition alpha
                float transitionAlpha;
                #if defined(_PATTERN_REVERSE)
                    // saturate((FadeAlpha*2 - 1) + maskAlpha)
                    transitionAlpha = saturate(_FadeAlpha * 2.0 - 1.0 + maskAlpha);
                #else
                    // saturate((FadeAlpha*2 - 1) + (1 - maskAlpha)) = saturate(FadeAlpha*2 - maskAlpha)
                    transitionAlpha = saturate(_FadeAlpha * 2.0 - maskAlpha);
                #endif

                // Alpha floor/ceil
                transitionAlpha = (transitionAlpha < _AlphaFloor) ? 0.0 : transitionAlpha;
                transitionAlpha = (transitionAlpha > _AlphaCeil)  ? 1.0 : transitionAlpha;

                // Shape Mask: Alpha ch で線の形状をマスク (ON時: 背景alpha=0は常に透明)
                float shapeMask = (_UseAlphaShape > 0.5) ? maskColor.a : 1.0;

                // Vertex Color alpha (Image.color.a)
                float finalAlpha = transitionAlpha * shapeMask * input.color.a;

                return float4(color, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
