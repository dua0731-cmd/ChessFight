// A starburst added onto what is behind it (R162, 10-27, the Fire Beam's muzzle flare): a soft round glow with _Rays thin
// spikes out of it, every other one longer. The spikes are turned by where the quad stands, so two bursts in different
// places never point the same way. For a VfxFlash (a camera-facing quad), which sets _Fade and _Intensity per renderer.
Shader "ChessFight/VFX/Starburst Additive"
{
    Properties
    {
        [HDR] _Color ("Color (HDR)", Color) = (1, 0.72, 0.36, 1)
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Falloff ("Glow falloff", Range(0.5, 8)) = 3
        _Rays ("Rays", Float) = 8
        _RaySharpness ("Ray sharpness", Range(1, 64)) = 20
        _RayStrength ("Ray strength", Float) = 0.9
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
                half _Rays;
                half _RaySharpness;
                half _RayStrength;
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
                float turn : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                float3 at = TransformObjectToWorld(float3(0, 0, 0));
                output.turn = frac(sin(dot(at, float3(12.9898, 78.233, 37.719))) * 43758.5453) * 6.2831853;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 q = input.uv * 2.0 - 1.0;
                half d = length(q);
                half glow = pow(saturate(1.0 - d), _Falloff);
                float a = atan2(q.y, q.x) + input.turn;
                half spike = pow(abs(cos(a * _Rays * 0.5)), _RaySharpness);
                half reach = lerp(0.55, 1.0, abs(cos(a * _Rays * 0.25)));
                half along = saturate(1.0 - d / reach);
                glow += spike * along * along * _RayStrength;
                half3 col = _Color.rgb * _Intensity * input.color.rgb * input.color.a * glow * _Fade;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
