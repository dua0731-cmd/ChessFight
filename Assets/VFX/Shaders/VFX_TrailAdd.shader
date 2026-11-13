// The trail a thrown sword leaves along its way (R181, 10-28, F5 of the bishop's X cut; reference F_03_flight.png): a
// ribbon drawn by a LineRenderer from its far end (u 0) to the sword (u 1), v across. Fine streaks of light run along
// it, each its own brightness and reaching its own way back from the sword, over a glow nearly as wide as the ribbon
// and a thin brighter core; brightest right behind the sword and dying away to nothing at the far end, soft at the sword
// so it melts into the rim of the spinning blur it leaves (VfxSwordFlight lays it there), added onto what is behind it.
// Streaks under about two pixels wide melt into a smooth glow instead of shimmering. _Intensity and _Fade are set per
// renderer by VfxSwordFlight.
Shader "ChessFight/VFX/Trail Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (0.76, 0.79, 0.83, 1)
        [HDR] _CoreColor ("Core color (HDR)", Color) = (1, 1, 1, 1)
        _CoreBoost ("Core brightness over the glow", Float) = 1
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Falloff ("Dying away toward the far end", Range(0.3, 8)) = 1.6
        _HeadSoft ("Soft end at the sword (share of the length)", Range(0.005, 0.3)) = 0.1
        _CoreWidth ("Core width (share of the width)", Range(0.02, 1)) = 0.18
        _GlowFalloff ("Glow falloff across", Range(0.5, 8)) = 0.8
        _Streaks ("Streaks across", Range(1, 24)) = 9
        _StreakSharp ("Streak thinness", Range(1, 16)) = 4
        _StreakMix ("Streak contrast", Range(0, 1)) = 0.75
        _StreakSpread ("How unevenly far back the streaks reach", Range(0, 1)) = 0.55
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
                half4 _CoreColor;
                half _CoreBoost;
                half _Intensity;
                half _Fade;
                half _Falloff;
                half _HeadSoft;
                half _CoreWidth;
                half _GlowFalloff;
                half _Streaks;
                half _StreakSharp;
                half _StreakMix;
                half _StreakSpread;
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

            // A number in 0..1 for a streak.
            half Hash(float n)
            {
                return frac(sin(n * 12.9898) * 43758.5453);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                half across = abs(input.uv.y * 2.0 - 1.0);
                half glow = pow(saturate(1.0 - across), _GlowFalloff);
                half core = 1.0 - smoothstep(_CoreWidth * 0.5, _CoreWidth, across);
                half body = pow(saturate(u), _Falloff);

                // The streaks: each its own brightness, each reaching its own share of the way back from the sword.
                float k = input.uv.y * _Streaks;
                float id = floor(k);
                half bright = Hash(id + 3.0), reach = 1.0 - _StreakSpread * Hash(id + 11.0);
                half stripe = pow(sin(frac(k) * PI), _StreakSharp);
                half along = pow(saturate((u - (1.0 - reach)) / max(reach, 0.001)), _Falloff);
                half contrast = _StreakMix * saturate(1.5 - fwidth(k) * 2.0);
                half streak = lerp(body, stripe * (0.45 + 0.55 * bright) * along * 1.6, contrast);

                half head = 1.0 - smoothstep(1.0 - _HeadSoft, 1.0, u);
                half3 col = _Color.rgb * glow * streak + _CoreColor.rgb * core * _CoreBoost * body;
                col *= _Intensity * _Fade * head * input.color.rgb * input.color.a;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
