// A blinding star where two blades meet (R185, 10-28, F2 of the king's block; reference
// Assets/VFX/Reference/king/F_02_parry.png): a hot warm-white core in a soft gold glow, a burst of thin gold rays of
// uneven lengths all round it (_Rays of them, laid out by _Seed), four longer ones in a cross turned by _Turn, and one
// long thin streak level across the screen through it, added onto what is behind it. The quad is laid out square to
// the view here in the vertex shader, as high as the object's scale and _Aspect times as wide, so the streak can run on
// past the star, and _Toward metres nearer the eye than its place, so the blades in the clash do not cut it. Where
// something stands in front of it (his own body, seen from behind him), _Glare of it still shows (the second pass), as
// the glare of a blinding light does. Lines thinner than a pixel are widened to one and dimmed to match, so they do not
// shimmer. _Intensity, _Fade, _StreakFade, _Turn and _Seed are set per renderer by VfxStarFlash.
Shader "ChessFight/VFX/Star Flash Additive"
{
    Properties
    {
        [HDR] _Color ("Glow and ray color (HDR)", Color) = (0.8, 0.52, 0.12, 1)
        [HDR] _CoreColor ("Core color (HDR)", Color) = (1, 0.957, 0.839, 1)
        _Intensity ("Intensity", Float) = 1
        _Fade ("Star fade", Range(0, 1)) = 1
        _StreakFade ("Streak fade", Range(0, 1)) = 1
        _Aspect ("Width over height", Range(1, 8)) = 3.5
        _Core ("Core size (share of the radius)", Range(0.01, 0.5)) = 0.07
        _CoreBoost ("Core brightness over the rays", Float) = 4
        _Glow ("Glow strength", Float) = 0.8
        _Falloff ("Glow falloff", Range(0.5, 16)) = 3
        _Rays ("Ray count", Range(4, 48)) = 22
        _RayMin ("Shortest ray (share of the longest)", Range(0, 1)) = 0.3
        _RayReach ("Longest ray (share of the radius)", Range(0.2, 1)) = 1
        _RayThin ("Ray thinness", Range(4, 400)) = 30
        _RayStrength ("Ray strength", Float) = 1.8
        _Cross ("Cross rays' strength", Float) = 1.2
        _CrossThin ("Cross rays' thinness", Range(4, 400)) = 30
        _Streak ("Streak strength", Float) = 1
        _StreakThin ("Streak thinness", Range(10, 800)) = 45
        _Turn ("Turn (degrees)", Float) = 0
        _Seed ("Seed", Float) = 0
        _Toward ("Metres drawn toward the eye", Float) = 0.15
        _Glare ("Share that shows through what is in front", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _CoreColor;
            half _Intensity;
            half _Fade;
            half _StreakFade;
            float _Aspect;
            half _Core;
            half _CoreBoost;
            half _Glow;
            half _Falloff;
            float _Rays;
            half _RayMin;
            half _RayReach;
            half _RayThin;
            half _RayStrength;
            half _Cross;
            half _CrossThin;
            half _Streak;
            half _StreakThin;
            float _Turn;
            float _Seed;
            float _Toward;
            half _Glare;
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
            float size = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
            float3 centre = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
            // Moved toward the eye along the line to it (the view's origin), so it stays where it is on the screen.
            float away = length(centre);
            centre *= max(away - _Toward, 0.05) / max(away, 0.05);
            output.positionCS = TransformWViewToHClip(centre + float3(input.positionOS.xy * float2(_Aspect, 1.0) * size, 0));
            output.uv = input.uv;
            return output;
        }

        float Hash(float n)
        {
            return frac(sin(n * 12.9898 + _Seed * 78.233) * 43758.5453);
        }

        // A line along x through the middle, reach long, narrowing to a point; px is a pixel in these units. Narrower
        // than a pixel it is drawn a pixel wide and as much dimmer.
        half Line(float2 p, half reach, half thin, float px)
        {
            half along = saturate(1.0 - abs(p.x) / max(reach, 0.001));
            half width = along / thin;
            half wide = max(width, px);
            return exp(-abs(p.y) / max(wide, 1e-5)) * (width / max(wide, 1e-5)) * pow(along, 1.5);
        }

        // A ray from the middle out along +x only.
        half Ray(float2 p, half reach, half thin, float px)
        {
            return p.x > 0.0 ? Line(p, reach, thin, px) : 0.0;
        }

        // The ray of sector i of count (wrapped round), set somewhere inside it, its length between _RayMin and all of
        // _RayReach, its strength between a half and all.
        half SectorRay(float2 p, float i, float count, float px)
        {
            i -= count * floor(i / count);
            float s, c;
            sincos((i + 0.15 + 0.7 * Hash(i)) * (TWO_PI / count), s, c);
            float2 q = float2(c * p.x + s * p.y, c * p.y - s * p.x);
            half h = Hash(i + 31.7);
            return Ray(q, _RayReach * lerp(_RayMin, 1.0, h * h), _RayThin, px) * lerp(0.5, 1.0, Hash(i + 71.3));
        }

        half3 Shade(float2 uv)
        {
            // In units of the star's radius (half the quad's height); x runs _Aspect each way.
            float2 p = float2((uv.x * 2.0 - 1.0) * _Aspect, uv.y * 2.0 - 1.0);
            float px = max(fwidth(p.x), fwidth(p.y));
            half r = length(p);

            half glow = pow(saturate(1.0 - r), _Falloff) * _Glow;
            half core = saturate(1.0 - r / _Core);
            core = core * core * (3.0 - 2.0 * core);

            // The burst: each pixel looks at the ray of its own sector of the circle and at the two beside it.
            float count = max(floor(_Rays), 4.0);
            float angle = atan2(p.y, p.x);
            float i = floor((angle < 0.0 ? angle + TWO_PI : angle) / (TWO_PI / count));
            half burst = max(SectorRay(p, i, count, px), max(SectorRay(p, i - 1.0, count, px), SectorRay(p, i + 1.0, count, px)));

            // The cross: up and down (the level arm is the streak) and the two diagonals, shorter.
            float s, c;
            sincos(radians(_Turn), s, c);
            float2 t = float2(c * p.x + s * p.y, c * p.y - s * p.x);
            float2 d = float2(t.x + t.y, t.y - t.x) * 0.70710678;
            half cross = max(Line(t.yx, 1.0, _CrossThin, px), max(Line(d, 0.55, _CrossThin, px), Line(d.yx, 0.55, _CrossThin, px)));

            // The streak, level on the screen, with a hotter thread down its middle.
            half streak = Line(p, _Aspect, _StreakThin, px);
            half thread = Line(p, _Aspect * 0.7, _StreakThin * 3.0, px);

            // The rays are white-hot near the middle and gold further out.
            half hot = exp(-r * 5.0);
            half rays = burst * _RayStrength + cross * _Cross;
            half3 gold = _Color.rgb, white = _CoreColor.rgb;
            half3 star = gold * glow + lerp(gold, white, hot) * rays + white * core * _CoreBoost;
            half3 level = gold * streak + white * thread;
            return (star * _Fade + level * _Streak * _StreakFade) * _Intensity;
        }
        ENDHLSL

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

            half4 frag(Varyings input) : SV_Target
            {
                return half4(Shade(input.uv), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            // Only where something nearer stands in front of it: _Glare of it shows through.
            Name "Glare"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest Greater
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragGlare

            half4 fragGlare(Varyings input) : SV_Target
            {
                return half4(Shade(input.uv) * _Glare, 1.0);
            }
            ENDHLSL
        }
    }
}
