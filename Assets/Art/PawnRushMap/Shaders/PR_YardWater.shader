// Royal Carriage Yard water (R684, 승규 11-16 "물 그래픽도 수학적으로 계산해서 물래방아에서 떨어지는 물 등등 해서 그래픽 좋게
// 넣어"): the canal, the mill pond and the flume's trough. Built on the garden's clear water (PR_GardenClearWater:
// Gerstner swell on the finite-depth dispersion relation, refraction through the opaque texture, Fresnel sky) with
// what an open-air, running yard canal adds. Every formula is written where it is used:
//  - a current U (world xz, m/s, _Flow, from YardWater / a WaterCurrent in the yard): the ripple maps and the foam
//    are carried along it (sampled at p - U t), the swell is Doppler-shifted (omega' = omega + k d.U) and rings
//    drift downstream;
//  - Beer-Lambert colour: light from the bed loses each channel at its own rate along its path through the water,
//    T = exp(-sigma d) (red first, so it turns turquoise), and the water's own in-scattered light, from the concept's
//    shallow #3D9EBD to deep #0A3861, fills in what is lost: C = C_bed T + C_water (1 - T);
//  - Schlick's Fresnel with F0 = ((n - 1) / (n + 1))^2 = 0.02 for water (n = 1.33), reflecting the yard's box
//    reflection probes (RoyalCarriageYard.Lighting.cs) through the cluster loop on PC (Forward+) and the per-object
//    probe on the mobile renderer;
//  - white foam where the concept puts it ("white foam only at edges and under falls"): along whatever stands in the
//    water (the depth texture says the water there is shallower than _EdgeFoam: walls, posts, piers, the giant wheel's
//    rim where it cuts the surface) and in the churn under every fall (_RcyFallLines), broken into drifting patches by
//    two octaves of value noise carried with the current;
//  - rings: from each fall's landing line all the time, and from a pawn's splash as a wave packet (_RcySplashes).
// The globals are the yard's own (YardWater fills them; with no yard the counts are 0). The garden's _Garden* arrays
// are never touched. Needs the URP depth and opaque textures (both on in PC_RPAsset and Mobile_RPAsset).
Shader "ChessFight/Pawn Rush Yard Water"
{
    Properties
    {
        _ShallowColor ("Water colour, shallow (concept #3D9EBD)", Color) = (0.239, 0.620, 0.741, 1)
        _DeepColor ("Water colour, deep (concept #0A3861)", Color) = (0.039, 0.220, 0.380, 1)
        _DeepAt ("Path to the deep colour (m)", Float) = 4
        _Absorb ("Absorption per metre, r g b (Beer-Lambert sigma)", Vector) = (0.55, 0.14, 0.10, 0)
        _SkyColor ("Sky tint where no probe reaches", Color) = (0.70, 0.84, 0.96, 1)
        _FoamColor ("Foam (a: how white)", Color) = (0.96, 0.98, 1.0, 0.92)
        _EdgeFoam ("Edge foam depth (m)", Float) = 0.32
        _FoamScale ("Foam patches per metre", Float) = 1.7
        _Flow ("Current (world x, z, m/s)", Vector) = (0, 0, 0, 0)
        [Normal] _WaveMap ("Ripple normals", 2D) = "bump" {}
        _WaveScale ("Ripple size (per metre)", Float) = 0.21
        _WaveStrength ("Ripple strength", Range(0, 2)) = 0.42
        _WaveSpeed ("Wind drift of the ripples (m/s)", Float) = 0.05
        _DetailScale ("Small ripple size (per metre)", Float) = 0.9
        _DetailStrength ("Small ripple strength", Range(0, 2)) = 0.22
        _RingStrength ("Rings off the falls: strength", Range(0, 6)) = 1
        _RingReach ("Rings off the falls: how far they carry (x)", Range(0.5, 8)) = 1
        _RingRadius ("Rings off the falls: gone past this distance (m, 0: no limit)", Float) = 0
        _LapOn ("Laps against walls (1: on, needs _LapMap)", Float) = 0
        _LapMap ("Distance to the wall (r, m) and the way away from it (g, b)", 2D) = "black" {}
        _LapRect ("Lap map: world x min, z min, 1 / width, 1 / depth", Vector) = (0, 0, 1, 1)
        _LapHeight ("Lap height at the wall (m)", Float) = 0.05
        _LapLength ("Lap wavelength (m)", Float) = 2.4
        _LapReach ("Lap fades over (m)", Float) = 2.5
        _LapFoam ("Lap foam at the wall", Range(0, 1.5)) = 0.8
        _LapSurge ("Lap: how high the water rises up the wall as a crest arrives (m)", Float) = 0
        _LapSurgeReach ("Lap: how far out the rise reaches (m)", Float) = 0.8
        _LapFallReach ("Lap: how far the falls' waves carry to a wall (m, e-folding)", Float) = 25
        _PawStrength ("Breeze patches: ripple strength (0: off)", Range(0, 2)) = 0
        _PawCover ("Breeze patches: share of the water they cover", Range(0, 1)) = 0.45
        _PawSize ("Breeze patches: size (m)", Float) = 14
        _PawWind ("Breeze: wind (world x, z, m/s)", Vector) = (0.5, 0.2, 0, 0)
        _PawRippleScale ("Breeze ripples per metre", Float) = 1.3
        _WaveHeight ("Swell height (m)", Float) = 0.028
        _WaveLength ("Main wavelength (m)", Float) = 3.4
        _WaveHeading ("Swell heading (deg, from +x toward +z)", Float) = 180
        _WaterDepth ("Water depth for the wave speed (m)", Float) = 3.3
        _Choppiness ("Gerstner steepness", Range(0, 1)) = 0.5
        _Refraction ("Refraction", Range(0, 0.2)) = 0.035
        _Reflection ("Reflection", Range(0, 1)) = 0.85
        _Glint ("Sun glint", Range(0, 12)) = 6
        _GlintSharpness ("Glint sharpness", Range(16, 2048)) = 700
        _Sparkle ("Sparkle", Range(0, 4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        // As the garden's: the scene behind is sampled and blended here, so the result is written as is, and writing
        // depth hides the underwater part of the falls drawn after it (Transparent+5).
        Blend One Zero
        ZWrite On
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            // YardWater's low quality tier: two swell waves instead of four, no small ripples, no rings (the falls'
            // churn and foam stay), no splash packets.
            #pragma multi_compile_local _ _RCY_WATER_LOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_WaveMap); SAMPLER(sampler_WaveMap);
            TEXTURE2D(_LapMap); SAMPLER(sampler_LapMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor;
                float4 _Absorb, _Flow, _WaveMap_ST, _LapMap_ST, _LapRect;
                float _LapOn, _LapHeight, _LapLength, _LapReach, _LapFoam, _LapSurge, _LapSurgeReach, _LapFallReach;
                float4 _PawWind;
                float _PawStrength, _PawCover, _PawSize, _PawRippleScale;
                float _DeepAt, _EdgeFoam, _FoamScale;
                float _WaveScale, _WaveStrength, _WaveSpeed, _DetailScale, _DetailStrength, _RingStrength, _RingReach, _RingRadius;
                float _WaveHeight, _WaveLength, _WaveHeading, _WaterDepth, _Choppiness;
                float _Refraction, _Reflection, _Glint, _GlintSharpness, _Sparkle;
            CBUFFER_END

            // YardWater (16 and 8 long: Unity keeps a global array's first size for the editor session, so these names
            // must never be reused at another size).
            float4 _RcyFallLines[16];      // a fall's landing line: start xz, end xz
            float4 _RcyFallLinesInfo[16];  // x: its water's height, y: strength, z: churn reach (m), w: ring scale
            float _RcyFallLinesCount;
            float4 _RcySplashes[8];        // a splash: xz, start (_Time.y), strength
            float4 _RcySplashesInfo[8];    // x: its water's height
            float _RcySplashesCount;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            static const float RCY_G = 9.81;
            static const float RCY_TAU = 6.2831853;

            // Angular frequency of a wave of wavenumber k on water of depth h (finite-depth dispersion):
            // omega^2 = g k tanh(k h).
            float Omega(float k) { return sqrt(RCY_G * k * tanh(k * _WaterDepth)); }

            // The swell: a sum of Gerstner waves round one heading. Each: x heading offset (deg), y wavelength and z
            // height (fractions of the main wave's), w phase. A surface point at rest p moves to
            //   p + sum Q_i A_i d_i cos f_i,  y = sum A_i sin f_i,  f_i = k_i d_i.p - (omega_i + k_i d_i.U) t + phase_i
            // (omega_i on still water, Doppler-shifted by the current U), with steepness Q_i = c / (k_i A_i N) so the
            // crests sharpen but never loop. Returns the displacement; `normal` is the exact normal
            //   n = (-sum d_x k A cos f, 1 - sum Q k A sin f, -sum d_z k A cos f).
            float3 Gerstner(float2 p, out float3 normal)
            {
                static const float4 waves[4] = { float4(0, 1.0, 1.0, 0.0), float4(31, 0.57, 0.5, 1.9),
                                                 float4(-24, 0.36, 0.32, 4.3), float4(63, 0.23, 0.2, 2.7) };
                #if defined(_RCY_WATER_LOW)
                    const int count = 2;
                #else
                    const int count = 4;
                #endif
                float3 offset = 0;
                float sx = 0, sz = 0, sy = 0;
                [unroll] for (int i = 0; i < count; i++)
                {
                    float a = radians(_WaveHeading + waves[i].x);
                    float2 d = float2(cos(a), sin(a));
                    float k = RCY_TAU / max(_WaveLength * waves[i].y, 0.1);
                    float A = _WaveHeight * waves[i].z;
                    float Q = _Choppiness / max(k * A * count, 1e-4);
                    float f = k * dot(d, p) - (Omega(k) + k * dot(d, _Flow.xy)) * _Time.y + waves[i].w;
                    float c = cos(f), s = sin(f);
                    offset.xz += Q * A * d * c;
                    offset.y += A * s;
                    sx += d.x * k * A * c;
                    sz += d.y * k * A * c;
                    sy += Q * k * A * s;
                }
                normal = normalize(float3(-sx, 1 - sy, -sz));
                return offset;
            }

            // Rings off a steady source (a fall's landing) seen at q from it: waves of length `len` leave at the phase
            // speed c = omega / k, so the crest now r out left r / c ago, and the current has carried it U r / c
            // downstream since - the rings are measured from the source moved that far (one step of the fixed point,
            // enough while U << c). Height h(r) = H e^(-r/R) sin(k r - omega t); returns dh/dr, `dir` the way out.
            float RingSlope(float2 q, float len, float height, float reach, out float2 dir)
            {
                float k = RCY_TAU / len;
                float w = Omega(k);
                float2 drifted = q - _Flow.xy * (length(q) * k / w);
                float r = length(drifted);
                dir = drifted / max(r, 1e-3);
                float f = k * r - w * _Time.y;
                return height * exp(-r / reach) * (k * cos(f) - sin(f) / reach);
            }

            // A splash's rings `age` s old, r out from where it went in (the garden's packet, R442): waves of length
            // `len` leave as a group at the group speed c_g = omega / (2 k) (deep water), as wide as they have spread,
            // weaker as they spread (1 / sqrt r) and dying away (e-folding 1.6 s). Returns dh/dr.
            float SplashRing(float r, float age, float len, float height)
            {
                float k = RCY_TAU / len;
                float w = Omega(k);
                float front = 0.5 * w / k * age;
                float width = 0.35 + 0.5 * age;
                float x = (r - front) / width;
                return height * exp(-x * x) * exp(-age / 1.6) / sqrt(1 + r) * k * cos(k * r - w * age);
            }

            // Extra slope from the falls and the splashes at world point (p.xz, height y), and how churned the water is
            // there (0..1+): foam under a fall reaches `reach` across and is carried further downstream, the distance
            // along the current shrunk by 1 / (1 + 2 |U|) past the line.
            float2 Interaction(float2 p, float y, out float churn)
            {
                float2 slope = 0;
                churn = 0;
                float speed = length(_Flow.xy);
                float2 down = speed > 1e-3 ? _Flow.xy / speed : float2(0, 0);
                [loop] for (int i = 0; i < 16; i++)
                {
                    if (i >= (int)_RcyFallLinesCount) break;
                    float4 info = _RcyFallLinesInfo[i];
                    if (abs(y - info.x) > 0.4) continue;                 // its own pool only
                    float2 a = _RcyFallLines[i].xy, ab = _RcyFallLines[i].zw - a;
                    float h = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-4));
                    float2 q = p - (a + ab * h);
                    float along = dot(q, down);
                    float2 stretched = q - down * along * (along > 0 ? 1 - 1 / (1 + 2 * speed) : 0);
                    churn = max(churn, info.y * exp(-length(stretched) / max(info.z, 0.05)));
                    #if !defined(_RCY_WATER_LOW)
                    // Short rings from the impact over longer ones that carry far, both at the dispersion speed.
                    float2 d1, d2;
                    // 11-26 (the tutorial's moat, "웨이브 넣지 말고 폭포수 구간만 상호작용 식으로 … 멀리 갈수록 약하게"):
                    // _RingStrength and _RingReach scale the rings and their e-folding distance (both 1 in the yard);
                    // then "폭포수 근처에서만": past _RingRadius (m from the landing line) they are gone, faded out from
                    // half of it (0 in the yard: no limit).
                    float s1 = RingSlope(q, 0.7 * info.w, 0.014, 2.2 * _RingReach, d1);
                    float s2 = RingSlope(q, 1.9 * info.w, 0.022, 5.5 * _RingReach, d2);
                    float ringFade = _RingRadius > 0 ? 1 - smoothstep(0.5 * _RingRadius, _RingRadius, length(q)) : 1;
                    slope -= (d1 * s1 + d2 * s2) * min(info.y, 1.5) * _RingStrength * ringFade;
                    #endif
                }
                #if !defined(_RCY_WATER_LOW)
                [loop] for (int s = 0; s < 8; s++)
                {
                    if (s >= (int)_RcySplashesCount) break;
                    if (abs(y - _RcySplashesInfo[s].x) > 0.4) continue;
                    float4 sp = _RcySplashes[s];
                    float age = _Time.y - sp.z;
                    if (age < 0 || age > 5) continue;
                    float2 q = p - (sp.xy + _Flow.xy * age);             // the rings drift with the water
                    float d = length(q);
                    float rings = SplashRing(d, age, 0.45, 0.03) + SplashRing(d, age, 1.2, 0.045);
                    slope -= q / max(d, 1e-3) * rings * sp.w;
                    // The white water the cavity leaves, for about a second.
                    churn = max(churn, sp.w * saturate(1.3 - age) * exp(-max(0, d - 0.3 - 0.9 * sqrt(age)) / 0.4));
                }
                #endif
                return slope;
            }

            // Smooth value noise for the foam's patches.
            float RcyHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float RcyNoise(float2 p)
            {
                float2 a = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(RcyHash(a), RcyHash(a + float2(1, 0)), f.x), lerp(RcyHash(a + float2(0, 1)), RcyHash(a + 1), f.x), f.y);
            }

            // 11-26 (the tutorial's moat, "물이 살짝살짝 씩 안쪽 벽을 치게", "물이 실제로 살짝 올라가는 연출", then "수학적으로
            // 계산해보면 변 4개에 전부 동일하게 올라가는 게 말이 돼?"): the waves at the island's walls are the falls' waves,
            // the only waves on this still water, arriving and reflecting. _LapMap holds, over _LapRect, the signed
            // distance d to the nearest wall (m, negative under the island) and n, the way away from the wall (its gradient;
            // TutorialMoatWater works it out at Start). For each fall landing on this water, from the nearest point of its
            // landing line, r away along D (unit):
            //  - only a face turned to the fall is reached: the energy flux into the wall goes as cos(theta) = max(0, -D.n),
            //    so the far side of a pillar or a wall facing away stays still, its sides get a little;
            //  - the wave has spread and damped on the way: a = s e^(-r/Lf) sqrt(8 / (8 + r)) (s the fall's strength, a
            //    slow wander in time as a fall's pour is unsteady, each fall its own phase - they are not in step);
            //  - a vertical wall reflects it whole and without inversion; incident plus reflected at d from the wall, with
            //    the wall's foot r + d cos(theta) from the fall:
            //      eta = 2 a cos(k d cos(theta)) sin(f),  f = k (r + d cos(theta)) - omega t + phase,
            //    standing across the wall (twice as high at it) and running along it at c / sin(theta), so a crest sweeps
            //    along a wall instead of every point rising at once;
            //  - run-up at the wall: the crest piles up, S a e^(-|d|/Rs) q^3 with q = (1 + sin f) / 2 (never below the still
            //    water);
            //  - foam where a crest meets the wall (within 0.8 m).
            // eta is shown within R of the walls (e^(-|d|/R)), the falls' own rings carrying the rest. Under the island the
            // sheet, out of sight, keeps the wall's own height so it stays up against the wall's face. Returns the height.
            float LapHeight(float2 p, float y, out float foam)
            {
                foam = 0;
                if (_LapOn < 0.5) return 0;
                float4 m = SAMPLE_TEXTURE2D_LOD(_LapMap, sampler_LapMap, (p - _LapRect.xy) * _LapRect.zw, 0);
                float d = max(m.r, 0);
                float len = length(m.gb);
                if (len < 1e-4) return 0;
                float2 n = m.gb / len;
                float k = RCY_TAU / max(_LapLength, 0.3);
                float w = Omega(k);
                float t = _Time.y;
                float Lf = max(_LapFallReach, 1);
                float eta = 0, rise = 0, crest = 0;
                [loop] for (int i = 0; i < 16; i++)
                {
                    if (i >= (int)_RcyFallLinesCount) break;
                    float4 info = _RcyFallLinesInfo[i];
                    if (abs(y - info.x) > 0.4) continue;                 // falls into this water only
                    float2 a0 = _RcyFallLines[i].xy, ab = _RcyFallLines[i].zw - a0;
                    float2 q = p - (a0 + ab * saturate(dot(p - a0, ab) / max(dot(ab, ab), 1e-4)));
                    float r = length(q);
                    float cosT = saturate(-dot(q / max(r, 1e-3), n));
                    if (cosT <= 0) continue;
                    float a = min(info.y, 1.5) * exp(-r / Lf) * sqrt(8 / (8 + r)) * cosT
                            * (0.55 + 0.45 * RcyNoise(float2(t * 0.25, i * 7.13)));
                    float f = k * (r + d * cosT) - w * t + RcyHash(float2(i, 3.7)) * RCY_TAU;
                    float s = sin(f);
                    eta += 2 * a * cos(k * d * cosT) * s;
                    float qq = 0.5 + 0.5 * s;
                    rise += a * qq * qq * qq;
                    crest += a * smoothstep(0.55, 1.0, qq);
                }
                foam = _LapFoam * saturate(1 - d / 0.8) * saturate(crest * 2);
                return _LapHeight * exp(-d / max(_LapReach, 0.1)) * eta + _LapSurge * exp(-d / max(_LapSurgeReach, 0.1)) * rise;
            }

            // The slope of that (-grad h) by differences over a short step, and its foam.
            float2 LapSlope(float2 p, float y, out float foam)
            {
                const float e = 0.12;
                float f1, f2;
                float h0 = LapHeight(p, y, foam);
                float hx = LapHeight(p + float2(e, 0), y, f1);
                float hz = LapHeight(p + float2(0, e), y, f2);
                return -float2(hx - h0, hz - h0) / e;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                // Only the top swells (YardWater draws a grid of up faces; a box's sides keep still).
                float up = saturate(nws.y * 2 - 1);
                float3 gn;
                ws += Gerstner(ws.xz, gn) * up;
                float lapFoam;
                ws.y += LapHeight(ws.xz, ws.y, lapFoam) * up;
                nws = normalize(lerp(nws, gn, up));
                o.positionWS = ws;
                o.normalWS = nws;
                o.positionCS = TransformWorldToHClip(ws);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 p = i.positionWS.xz;
                float2 U = _Flow.xy;
                half up = saturate(i.normalWS.y);
                float churn;
                float2 touched = Interaction(p, i.positionWS.y, churn) * up;
                churn *= up;

                // Ripples carried by the current: sampled where the water now here was t seconds ago (p - U t), with a
                // little wind drift of their own on top.
                float t = _Time.y;
                float2 carried = p - U * t;
                float wind = t * _WaveSpeed;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, carried * _WaveScale + float2(wind, wind * 0.6)), _WaveStrength);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, carried * _WaveScale * 1.9 + float2(-wind * 0.7, wind * 0.4)), _WaveStrength);
                #if defined(_RCY_WATER_LOW)
                    half3 c = half3(0, 0, 1);
                #else
                    half3 c = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, carried * _DetailScale + float2(wind * 2.1, -wind * 1.6)), _DetailStrength);
                #endif
                // Churned water is rough: a fast boil on top where a fall lands.
                half3 boil = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, carried * 2.6 + float2(t * 0.35, -t * 0.28)), 0.9);
                float lapFoam;
                float2 lapSlope = LapSlope(p, i.positionWS.y, lapFoam);
                // 11-26 (the tutorial's moat, "여기 밋밋한 부분만 좀 살려줘"): still water away from the falls and the walls
                // is not glass - gusts roughen it in patches ("cat's paws") that ride the wind and come and go, glassy
                // between them. The patches: two octaves of value noise of size _PawSize carried at the wind W (sampled
                // at p - W t) and changing slowly as they go, cut at the _PawCover quantile with a soft edge. Inside, one
                // ripple layer (never two crossing: no double pattern), carried a little faster than the patch as the
                // ripples are pushed along by the gust.
                float paw = 0;
                half3 pawN = half3(0, 0, 1);
                if (_PawStrength > 0)
                {
                    float2 W = _PawWind.xy;
                    float2 g = (p - W * t) / max(_PawSize, 0.5);
                    float m = RcyNoise(g + float2(0, t * 0.02)) * 0.65 + RcyNoise(g * 2.3 + 11.1 - float2(t * 0.04, 0)) * 0.35;
                    paw = smoothstep(1 - _PawCover - 0.1, 1 - _PawCover + 0.15, m) * up;
                    pawN = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, (p - W * t * 1.3) * _PawRippleScale), _PawStrength);
                }
                float2 slope = a.xy + b.xy + c.xy + boil.xy * saturate(churn) + touched + lapSlope * up + pawN.xy * paw;
                float3 gn;
                float swell = Gerstner(p, gn).y;
                float crest = saturate(swell / max(_WaveHeight * 1.4, 1e-3) * 0.5 + 0.5);
                float3 base = normalize(lerp(normalize(i.normalWS), gn, up));
                float3 n = normalize(lerp(base, normalize(float3(base.x + slope.x, base.y, base.z + slope.y)), up));

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float surface = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float scene = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float thick = max(0, scene - surface);

                // Refraction: the lookup bent by the waves (more in the churn), but never onto something standing in
                // front of the water.
                float2 ruv = uv + n.xz * _Refraction * (1 + 2 * saturate(churn)) * saturate(thick);
                float rscene = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams);
                if (rscene < surface) { ruv = uv; rscene = scene; }
                float rthick = max(0, rscene - surface);
                // Water between the surface and what is seen under it: rthick along the view (eye depth, near enough
                // the ray's length for a view not too far off the screen's middle); the vertical depth there is that
                // times |V.y|, and the light from the bed came down that far first.
                float vertical = rthick * abs(V.y);
                float path = rthick + vertical;
                half3 behind = SampleSceneColor(ruv);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shade = lerp(0.65, 1, sun.shadowAttenuation);
                half3 sky = SampleSH(half3(0, 1, 0));
                // Light the water's body scatters: the sun through the surface plus the sky.
                half3 lit = sun.color * shade * (0.55 + 0.25 * saturate(dot(n, sun.direction))) + sky * 0.6;
                // Beer-Lambert, per channel: T = exp(-sigma d); the in-scattered colour fills in the rest.
                half3 T = exp(-_Absorb.rgb * path);
                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(path / max(_DeepAt, 0.1))) * lit;
                half3 body = behind * T + water * (1 - T);
                // Sunlight through the thin top of each crest.
                half through = crest * crest * (0.5 + 0.5 * saturate(dot(-V, sun.direction) * 0.5 + 0.5));
                body = lerp(body, _ShallowColor.rgb * 1.25 * lit, through * 0.3 * up);

                // Foam: along what stands in the water (the depth under the surface below _EdgeFoam) and in the churn,
                // cut into patches that drift with the current; less coverage, smaller and fewer bubbles.
                float edge = saturate(1 - vertical / max(_EdgeFoam, 0.01)) * saturate(thick * 8);
                float cover = saturate(edge * edge * 1.2 + churn + lapFoam * up);
                float2 fp = (p - U * t) * _FoamScale;
                // 11-17: the only white water where the falls land (the falls draw none of their own), so it must boil
                // and fade: the churned foam's pattern changes faster the harder it churns (its own clock runs at
                // 0.6 + 1.4 churn), it fades over a soft edge instead of a line, and fine bubbles open holes in it.
                float foamClock = t * (0.6 + 1.4 * saturate(churn));
                float pattern = RcyNoise(fp) * 0.5 + RcyNoise(fp * 2.7 + 17.3 + float2(foamClock * 0.45, -foamClock * 0.3)) * 0.3
                              + RcyNoise(fp * 6.1 + 5.1 + float2(-foamClock * 0.9, foamClock * 0.7)) * 0.2;
                half foam = smoothstep(pattern - 0.14, pattern + 0.14, cover * 1.05);
                half bubbles = RcyNoise(fp * 13.0 + float2(foamClock * 1.6, -foamClock * 1.1));
                foam *= lerp(0.62, 1.0, smoothstep(0.25, 0.6, bubbles)) * up;
                // Under the foam the churned water is full of bubbles: lighter and paler.
                body = lerp(body, _ShallowColor.rgb * 1.6 * lit, saturate(churn) * 0.35 * up);

                // Fresnel (Schlick, F0 = 0.02 for n = 1.33) between the body and the reflected sky and walls.
                half ndv = saturate(dot(n, V));
                half fres = 0.02 + 0.98 * pow(1 - ndv, 5);
                half3 env = GlossyEnvironmentReflection(reflect(-V, n), i.positionWS, 0.05h, 1.0h, uv);
                half3 reflected = lerp(_SkyColor.rgb, env, 0.75);
                half3 col = lerp(body, reflected, saturate(fres * _Reflection));

                // Foam sits on top: it scatters every colour (white), lit by the sun and the sky.
                half3 foamLit = _FoamColor.rgb * (sun.color * shade * (0.65 + 0.35 * saturate(dot(n, sun.direction))) + sky * 0.55);
                col = lerp(col, foamLit, foam * _FoamColor.a);

                // The sun on the water: a sharp glint, and sparkle cut to flecks on the steepest ripples.
                float3 h = normalize(sun.direction + V);
                half ndh = saturate(dot(n, h));
                half glint = pow(ndh, _GlintSharpness) * _Glint;
                half rough = length(c.xy) / max(_DetailStrength, 0.01) + saturate(churn) * length(boil.xy)
                           + paw * length(pawN.xy) / max(_PawStrength, 0.01);   // the breeze patches sparkle too
                half fleck = smoothstep(0.55, 0.9, rough) * pow(ndh, 64) * _Sparkle;
                col += sun.color * (glint + fleck) * sun.shadowAttenuation * (1 - foam);

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
