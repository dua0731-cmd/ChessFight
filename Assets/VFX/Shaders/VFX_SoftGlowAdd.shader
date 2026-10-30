// A soft round glow added onto what is behind it (R159, 10-27): flashes on a camera-facing quad (VfxFlash) and the dots
// and streaks of particles (VfxSparks, whose vertex colour carries each particle's fade). Brightest at the quad's centre,
// falling off by _Falloff; _Fade and _Intensity are set per renderer by the component that animates it.
Shader "ChessFight/VFX/Soft Glow Additive"
{
    Properties
    {
        [HDR] _Color ("Color (HDR)", Color) = (1, 0.76, 0.38, 1)
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Falloff ("Falloff", Range(0.5, 8)) = 2.5
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Fade;
                half _Falloff;
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
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half d = length(input.uv * 2.0 - 1.0);
                half glow = pow(saturate(1.0 - d), _Falloff);
                half3 col = _Color.rgb * _Intensity * input.color.rgb * input.color.a * glow * _Fade;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
