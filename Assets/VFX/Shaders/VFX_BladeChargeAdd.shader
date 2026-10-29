// The light running up one of the bishop's blades as he draws it back (R188, 10-28, F3 of his X cut; reference
// F_02_throw.png): a ribbon drawn by a LineRenderer along the blade from just over its guard (u 0) to its tip (u 1), v
// across. Lit as far as the light has run (_Fill, with a soft edge _Soft long), dimmer at the guard and brighter toward
// the tip, with a brighter band at the light's running edge (_Front, gone as it gets to the tip); a glow a little wider
// than the blade with a thin white core and faint streaks along it, eased in at the guard and a little short of the tip
// so the tip's glint takes over there. Drawn _Lift metres toward the camera (along the line of sight, so it stays where
// it is on screen) so the blade it lies along does not hide it, even edge on (half the blade's 9 cm width); added onto
// what is behind it. Streaks under about two pixels wide melt into a smooth glow instead of shimmering.
// _Intensity, _Fade, _Fill and _Front are set per renderer by VfxBladeCharge.
Shader "ChessFight/VFX/Blade Charge Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (0.76, 0.79, 0.83, 1)
        [HDR] _CoreColor ("Core color (HDR)", Color) = (1, 1, 1, 1)
        _CoreBoost ("Core brightness over the glow", Float) = 1.8
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Fill ("How far up the blade the light has run", Range(0, 1)) = 1
        _Front ("Band at the running edge", Range(0, 1)) = 0
        _FrontWidth ("Band width (share of the length)", Range(0.02, 0.5)) = 0.14
        _FrontBoost ("Band brightness over the lit blade", Range(0, 4)) = 1.2
        _Soft ("Soft edge of the light (share of the length)", Range(0.005, 0.3)) = 0.04
        _Base ("Brightness at the guard (share of the tip's)", Range(0, 1)) = 0.35
        _TipRise ("Rise toward the tip", Range(0.3, 6)) = 1.6
        _CoreWidth ("Core width (share of the width)", Range(0.02, 1)) = 0.22
        _GlowFalloff ("Glow falloff across", Range(0.5, 8)) = 1.2
        _Streaks ("Streaks across", Range(1, 24)) = 14
        _StreakSharp ("Streak thinness", Range(1, 16)) = 9
        _StreakMix ("Streak contrast", Range(0, 1)) = 0.55
        _Lift ("Drawn this far toward the camera (m)", Range(0, 0.2)) = 0.08
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
                half _Fill;
                half _Front;
                half _FrontWidth;
                half _FrontBoost;
                half _Soft;
                half _Base;
                half _TipRise;
                half _CoreWidth;
                half _GlowFalloff;
                half _Streaks;
                half _StreakSharp;
                half _StreakMix;
                half _Lift;
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
                // A little toward the camera, so the blade it lies along does not hide it.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += normalize(_WorldSpaceCameraPos - positionWS) * _Lift;
                output.positionCS = TransformWorldToHClip(positionWS);
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

                // Lit as far as the light has run, dimmer at the guard and brighter toward the tip.
                half lit = saturate((_Fill - u) / _Soft);
                half rise = lerp(_Base, 1.0, pow(saturate(u), _TipRise));
                // The brighter band at the light's running edge.
                half d = (u - (_Fill - 0.5 * _FrontWidth)) / _FrontWidth;
                half band = _Front * exp(-d * d * 6.0) * saturate((_Fill + _Soft - u) / _Soft);
                half ends = smoothstep(0.0, 0.06, u) * (1.0 - 0.5 * smoothstep(0.94, 1.0, u));
                half lum = (lit * rise + band * _FrontBoost) * ends;

                // Faint streaks along the blade, each its own brightness.
                float k = input.uv.y * _Streaks;
                half bright = Hash(floor(k) + 3.0);
                half stripe = pow(sin(frac(k) * PI), _StreakSharp);
                half contrast = _StreakMix * saturate(1.5 - fwidth(k) * 2.0);
                half streak = lerp(1.0, stripe * (0.45 + 0.55 * bright) * 1.6, contrast);

                half3 col = _Color.rgb * glow * streak + _CoreColor.rgb * core * _CoreBoost;
                col *= lum * _Intensity * _Fade * input.color.rgb * input.color.a;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
