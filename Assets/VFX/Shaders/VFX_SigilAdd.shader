// A glowing pattern laid on the ground (R168, 10-27, the king's landing sigil): a white-on-black mask texture lit in an
// HDR colour and added onto what is behind it. _Reveal opens it from the centre out (0 nothing, 1 all of it), the
// pattern's edge being brightest as it opens (_EdgeGlow); _Fade and _Intensity are set per renderer by VfxSigil.
Shader "ChessFight/VFX/Sigil Additive"
{
    Properties
    {
        _MainTex ("Pattern (white on black)", 2D) = "black" {}
        [HDR] _Color ("Color (HDR)", Color) = (1, 0.76, 0.38, 1)
        _Intensity ("Intensity", Float) = 2
        _Fade ("Fade", Range(0, 1)) = 1
        _Reveal ("Reveal from the centre", Range(0, 1.2)) = 1
        _RevealSoft ("Reveal edge softness", Range(0.01, 0.5)) = 0.08
        _EdgeGlow ("Extra glow at the reveal edge", Range(0, 8)) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Intensity;
                half _Fade;
                half _Reveal;
                half _RevealSoft;
                half _EdgeGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                half r = length(input.uv * 2.0 - 1.0);
                half shown = 1.0 - smoothstep(_Reveal - _RevealSoft, _Reveal, r);
                half edge = 1.0 - saturate(abs(r - _Reveal) / _RevealSoft);
                half glow = mask * (shown + edge * _EdgeGlow * step(_Reveal, 1.0));
                return half4(_Color.rgb * _Intensity * glow * _Fade, 1.0);
            }
            ENDHLSL
        }
    }
}
