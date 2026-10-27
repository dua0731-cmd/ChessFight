// A texture lit in an HDR colour and added onto what is behind it (R168, 10-27): smoke puffs and tendril trails of
// Shuriken particles (their vertex colour carries each one's colour and fade) and camera-facing flares (VfxFlash sets
// _Fade and _Intensity on its renderer). The texture is a white-on-black mask; _Contrast sharpens or softens it.
Shader "ChessFight/VFX/Textured Additive"
{
    Properties
    {
        _MainTex ("Mask (white on black)", 2D) = "white" {}
        [HDR] _Color ("Color (HDR)", Color) = (1, 0.76, 0.38, 1)
        _Intensity ("Intensity", Float) = 1.5
        _Fade ("Fade", Range(0, 1)) = 1
        _Contrast ("Mask contrast", Range(0.25, 4)) = 1
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
                half _Contrast;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half mask = pow(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r, _Contrast);
                half3 col = _Color.rgb * _Intensity * input.color.rgb * input.color.a * mask * _Fade;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
