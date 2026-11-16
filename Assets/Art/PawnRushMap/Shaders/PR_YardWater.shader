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
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor;
                float4 _Absorb, _Flow, _WaveMap_ST;
                float _DeepAt, _EdgeFoam, _FoamScale;
                float _WaveScale, _WaveStrength, _WaveSpeed, _DetailScale, _DetailStrength;
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
                    float s1 = RingSlope(q, 0.7 * info.w, 0.014, 2.2, d1);
                    float s2 = RingSlope(q, 1.9 * info.w, 0.022, 5.5, d2);
                    slope -= (d1 * s1 + d2 * s2) * min(info.y, 1.5);
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

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                // Only the top swells (YardWater draws a grid of up faces; a box's sides keep still).
                float up = saturate(nws.y * 2 - 1);
                float3 gn;
                ws += Gerstner(ws.xz, gn) * up;
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
                float2 slope = a.xy + b.xy + c.xy + boil.xy * saturate(churn) + touched;
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
                float cover = saturate(edge * edge * 1.2 + churn);
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
                half rough = length(c.xy) / max(_DetailStrength, 0.01) + saturate(churn) * length(boil.xy);
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
