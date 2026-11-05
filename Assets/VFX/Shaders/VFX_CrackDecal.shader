// The cracks a landing piece leaves in the floor (R341, 11-04, I3 of the grand stair hall's falling knights): radial
// cracks out from where it struck, a few short branches off them and a broken ring round a dark dent under its base,
// glowing hot as they open, cooling through orange to dark scorched lines, then fading before the next warning. All of it
// is drawn from the shader's own noise (_Seed picks the pattern), so there is no texture and every PC draws the same
// cracks. A projected decal like VFX_GroundWarnDecal (VFX_DecalProjection.hlsl: only floor facing up inside the box),
// drawn premultiplied: the scorch darkens the stone, the glow adds light. _Reach (how far the cracks have run, m),
// _Heat (1 just struck .. 0 cold), _Fade, _Box (the box's side, m) and _Seed are set per renderer by VfxCrackDecal.
Shader "ChessFight/VFX/Crack Decal"
{
    Properties
    {
        [HDR] _HotColor ("Hot color (HDR)", Color) = (1, 0.72, 0.38, 1)
        [HDR] _GlowColor ("Cooling color (HDR)", Color) = (1, 0.3, 0.05, 1)
        _Light ("Glow light", Float) = 4
        _Scorch ("Scorch (a = how dark)", Color) = (0.07, 0.045, 0.03, 0.6)
        _MaxReach ("Longest crack (m)", Float) = 2.8
        _Cracks ("Main cracks", Float) = 9
        _Branches ("Branches", Float) = 14
        _Width ("Width at the middle (m)", Range(0.005, 0.3)) = 0.07
        _TipWidth ("Width at the tips (m)", Range(0.002, 0.1)) = 0.012
        _GlowWidth ("Glow over the crack's width", Range(1, 6)) = 2.2
        _Wiggle ("Wiggle (m)", Range(0, 0.6)) = 0.22
        _Dent ("Dent radius (m)", Range(0.1, 2)) = 1
        _DentDark ("Dent darkness", Range(0, 1)) = 0.45
        _Soft ("Line softness (m)", Range(0.002, 0.1)) = 0.012
        _Upward ("Floor facing up past (cos)", Range(0, 1)) = 0.6
        _EdgeFade ("Fade at the box's top and bottom (share)", Range(0.01, 0.5)) = 0.2
        _Reach ("Run (m)", Float) = 2.8
        _Heat ("Heat", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Box ("Box side (m)", Float) = 6.4
        _Seed ("Pattern", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-12" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Decal"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "VFX_DecalProjection.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HotColor;
                half4 _GlowColor;
                half _Light;
                half4 _Scorch;
                float _MaxReach;
                float _Cracks;
                float _Branches;
                float _Width;
                float _TipWidth;
                half _GlowWidth;
                float _Wiggle;
                float _Dent;
                half _DentDark;
                float _Soft;
                half _Upward;
                half _EdgeFade;
                float _Reach;
                half _Heat;
                half _Fade;
                float _Box;
                float _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float Hash(float n)
            {
                return frac(sin(n * 127.1 + _Seed * 311.7) * 43758.5453);
            }

            // Smooth 1D value noise, 0..1.
            float Noise(float x)
            {
                float i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash(i), Hash(i + 1.0), f);
            }

            // How much of a crack (0..1) and of its glow there is at polar (r, turn) for one family of `count` cracks, each
            // in its own slice of the turn, starting `from` and running `runs` metres (both 0..1 of _MaxReach, picked per
            // crack between the given ranges), slanting off the radial by up to `slant` metres a metre.
            void Family(float r, float turn, float count, float salt, float2 from, float2 runs, float slant,
                        inout half crack, inout half glow)
            {
                float slice = turn * count;
                float home = floor(slice);
                for (int k = -1; k <= 1; k++)
                {
                    float i = home + k;
                    float id = fmod(i + count, count) + salt;
                    float start = lerp(from.x, from.y, Hash(id + 0.17)) * _MaxReach;
                    float run = lerp(runs.x, runs.y, Hash(id + 0.53)) * _MaxReach;
                    float end = min(start + run, _Reach);
                    if (r < start || r > end) continue;
                    // Where in its slice it leaves from, then wandering and slanting as it runs.
                    float centre = i + 0.5 + (Hash(id + 0.91) - 0.5) * 0.6;
                    float across = (slice - centre) * (6.2831853 * max(r, 0.05) / count);
                    float wander = (Noise(r * 2.6 + id * 13.1) - 0.5) * 2.0 * _Wiggle * saturate(r / 0.6);
                    float lean = (Hash(id + 0.29) - 0.5) * 2.0 * slant * (r - start);
                    float d = abs(across - wander - lean);
                    float u = saturate((r - start) / max(run, 0.01));
                    float width = lerp(_Width, _TipWidth, u);
                    crack = max(crack, 1.0 - smoothstep(width * 0.5, width * 0.5 + _Soft, d));
                    glow = max(glow, 1.0 - smoothstep(0.0, width * _GlowWidth, d));
                }
            }

            half4 frag(Varyings input) : SV_Target
            {
                half surface;
                float3 local = DecalLocal(input.positionCS, _Upward, _EdgeFade, surface);
                float2 p = local.xz * _Box;
                float r = length(p);
                float turn = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);

                half crack = 0.0, glow = 0.0;
                Family(r, turn, _Cracks, 0.0, float2(0.15, 0.3), float2(0.45, 0.75), 0.06, crack, glow);
                Family(r, turn, _Branches, 50.0, float2(0.3, 0.6), float2(0.12, 0.3), 0.5, crack, glow);

                // The broken ring round the dent, gaps where its noise is low, once the cracks have run past it.
                half ring = (1.0 - smoothstep(0.02, 0.02 + _Soft, abs(r - _Dent))) * step(0.45, Noise(turn * 23.0 + 7.0))
                          * step(_Dent, _Reach);
                crack = max(crack, ring);
                glow = max(glow, ring * 0.8);
                half dent = (1.0 - smoothstep(_Dent * 0.6, _Dent, r)) * _DentDark;

                // Hot white-gold just struck, through orange, to nothing; brighter near the middle.
                half heat = _Heat * _Heat;
                half3 tint = lerp(_GlowColor.rgb, _HotColor.rgb, saturate(_Heat * 1.6 - 0.6));
                half3 light = tint * glow * heat * _Light * (1.0 - 0.5 * saturate(r / _MaxReach));
                half dark = _Scorch.a * max(crack, dent);

                half mask = surface * _Fade;
                return half4((light + _Scorch.rgb * dark) * mask, dark * mask);
            }
            ENDHLSL
        }
    }
}
