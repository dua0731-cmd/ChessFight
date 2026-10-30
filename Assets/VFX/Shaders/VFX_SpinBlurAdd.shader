// The blur of a sword spinning flat as it flies (R181, 10-28, F5 of the bishop's X cut; reference F_03_flight.png): a
// disc in the plane it spins in (the quad's own XY, as wide as the object's scale), turned _Face of the way toward the
// camera about its middle in the vertex shader so it is never seen quite edge-on (the player looks along it from behind
// the bishop), drawn as fine rings of silver streaks, each ring its own brightness (some dark) and its own length of
// smear, brightest right behind where the blade is now (_Turn degrees from x toward y; both halves of the sword, so the
// smear comes round every half turn) and dying away slowly round the turn behind it over a faint light all the way round,
// so it reads as one even whirl (_Dir +1 when the blade turns from x toward y, -1 the other way), a soft brighter band
// near the rim where the tip runs and a small soft core where the hilt turns, added onto what is behind it. Rings
// narrower than about two pixels melt into a smooth smear instead of shimmering.
// _Intensity, _Fade, _Turn and _Dir are set per renderer by VfxSwordFlight.
Shader "ChessFight/VFX/Spin Blur Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (0.76, 0.79, 0.83, 1)
        [HDR] _CoreColor ("Core color (HDR)", Color) = (1, 1, 1, 1)
        _CoreBoost ("Hot core brightness", Float) = 0.6
        _Core ("Hot core size (share of the radius)", Range(0, 0.5)) = 0.1
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Turn ("Blade angle (degrees, from x toward y)", Float) = 0
        _Dir ("Spin direction (+1 x toward y, -1 the other way)", Float) = 1
        _Tail ("Dying away behind the blade (per half turn)", Range(0.5, 12)) = 2.2
        _Lead ("Soft leading edge (share of a half turn)", Range(0.005, 0.3)) = 0.2
        _Base ("Light all the way round (share)", Range(0, 1)) = 0.12
        _Rings ("Streak rings across the radius", Range(2, 60)) = 11
        _RingSharp ("Streak thinness", Range(1, 16)) = 4
        _RingMix ("Streak contrast", Range(0, 1)) = 0.6
        _Rim ("Rim band (share of the radius, where the tip runs)", Range(0.3, 1)) = 0.8
        _RimGlow ("Rim band brightness", Range(0, 4)) = 0.6
        _Edge ("Soft outer edge (share of the radius)", Range(0.01, 0.5)) = 0.12
        _Face ("Turned toward the camera (share)", Range(0, 1)) = 0.35
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
                half _Core;
                half _Intensity;
                half _Fade;
                float _Turn;
                half _Dir;
                half _Tail;
                half _Lead;
                half _Base;
                half _Rings;
                half _RingSharp;
                half _RingMix;
                half _Rim;
                half _RimGlow;
                half _Edge;
                half _Face;
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
                // Turned _Face of the way from its own plane toward the camera, about its middle (a rotation, so it keeps
                // its size and its streaks keep their places round it).
                float3 centre = TransformObjectToWorld(float3(0, 0, 0));
                float3 offset = TransformObjectToWorld(input.positionOS.xyz) - centre;
                float3 normal = TransformObjectToWorldDir(float3(0, 0, 1));
                float3 toEye = normalize(GetCameraPositionWS() - centre);
                if (dot(normal, toEye) < 0.0) normal = -normal;
                float3 want = normalize(lerp(normal, toEye, _Face));
                float3 axis = cross(normal, want);
                float s = length(axis), c = dot(normal, want);
                if (s > 1e-4)
                {
                    axis /= s;
                    offset = offset * c + cross(axis, offset) * s + axis * dot(axis, offset) * (1.0 - c);
                }
                output.positionCS = TransformWorldToHClip(centre + offset);
                output.uv = input.uv;
                return output;
            }

            // A number in 0..1 for a ring.
            half Hash(float n)
            {
                return frac(sin(n * 12.9898) * 43758.5453);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 q = input.uv * 2.0 - 1.0;
                float r = length(q);
                float a = atan2(q.y, q.x);
                // How far behind the blade, round the way it turns, in half turns: 0 at the blade, 1 at its other half.
                float behind = frac((radians(_Turn) - a) * _Dir / PI);

                // Each ring of the radius its own brightness (about a third of them dark) and its own length of smear.
                float k = r * _Rings;
                float ring = floor(k);
                half bright = saturate(Hash(ring + 1.0) * 1.4 - 0.4), reach = Hash(ring + 17.0);
                half stripe = pow(sin(frac(k) * PI), _RingSharp);
                // Rings under about two pixels wide are let go (they would only shimmer).
                half contrast = _RingMix * saturate(1.5 - fwidth(k) * 2.0);
                half streak = lerp(1.0, stripe * (0.25 + 0.75 * bright) * 1.8, contrast);
                half smear = exp(-behind * _Tail * lerp(0.6, 1.6, reach)) * smoothstep(0.0, _Lead, behind) + _Base;

                half rim = 1.0 + _RimGlow * exp(-pow((r - _Rim) / 0.14, 2.0));
                half edge = 1.0 - smoothstep(1.0 - _Edge, 1.0, r);
                half inner = smoothstep(0.05, 0.3, r);
                half core = exp(-r * r / max(_Core * _Core, 1e-4));
                half3 col = _Color.rgb * (smear * streak * rim * edge * inner) + _CoreColor.rgb * core * _CoreBoost;
                return half4(col * _Intensity * _Fade, 1.0);
            }
            ENDHLSL
        }
    }
}
