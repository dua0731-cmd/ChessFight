// Royal garden water (R278 clear water; R279 boats/falls; R280 Gerstner swell; R281 승규 11-01: "no white where
// things meet the water, a livelier, calculated boat interaction, cleaner and fresher blue").
// See-through: the opaque scene behind the surface is refracted through the waves and tinted by how much water lies
// in front of it (scene depth minus surface depth), so shallow water stays clear and deeper water turns turquoise.
// The swell is four Gerstner waves from one heading whose speeds follow the finite-depth dispersion relation
// w^2 = g k tanh(k h); vertices move with them and every pixel takes their exact normal.
// Everything that meets the water only bends its surface (no white foam): GardenWaterInteraction sets global
// arrays for
//  - boats: a Kelvin wake (transverse and divergent crests inside the 19.47 degree wedge, wavelength 2 pi U^2 / g
//    for the boat's speed U), bow and stern waves round the hull, rings from its bobbing;
//  - waterfall landing lines and bobbing platforms: rings leaving them at the dispersion speed.
// With no script (other scenes) the counts are 0 and nothing changes. Needs the URP depth and opaque textures.
Shader "ChessFight/Pawn Rush Garden Clear Water"
{
    Properties
    {
        _ShallowColor ("Shallow tint", Color) = (0.62, 0.90, 0.96, 1)
        _DeepColor ("Deep colour", Color) = (0.06, 0.40, 0.56, 1)
        _Clarity ("Clear depth (m)", Float) = 2.2
        _SkyColor ("Sky tint", Color) = (0.68, 0.86, 1.0, 1)
        [Normal] _WaveMap ("Wave normals", 2D) = "bump" {}
        _WaveScale ("Wave size (per metre)", Float) = 0.15
        _WaveStrength ("Wave strength", Range(0, 2)) = 0.35
        _WaveSpeed ("Wave speed", Float) = 0.04
        _DetailScale ("Ripple size (per metre)", Float) = 0.85
        _DetailStrength ("Ripple strength", Range(0, 2)) = 0.18
        _WaveHeight ("Swell height (m)", Float) = 0.045
        _WaveLength ("Main wavelength (m)", Float) = 13
        _WaveHeading ("Swell heading (deg, from +x toward +z)", Float) = 75
        _WaterDepth ("Water depth for wave speed (m)", Float) = 0.6
        _Choppiness ("Gerstner steepness", Range(0, 1)) = 0.45
        _RippleHeight ("Ring ripple height (m)", Float) = 0.02
        _WakeHeight ("Boat wave height (m)", Float) = 0.045
        _Refraction ("Refraction", Range(0, 0.2)) = 0.04
        _Reflection ("Reflection", Range(0, 1)) = 0.7
        _FresnelPower ("Fresnel power", Range(1, 8)) = 5
        _Glint ("Sun glint", Range(0, 12)) = 7
        _GlintSharpness ("Glint sharpness", Range(16, 2048)) = 900
        _Sparkle ("Sparkle", Range(0, 4)) = 1.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        // The scene behind is sampled and blended here, so the result is written as is; writing depth hides the
        // underwater part of anything transparent drawn later (the waterfalls' feet).
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
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_WaveMap); SAMPLER(sampler_WaveMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor;
                float4 _WaveMap_ST;
                float _Clarity, _WaveScale, _WaveStrength, _WaveSpeed, _DetailScale, _DetailStrength;
                float _WaveHeight, _WaveLength, _WaveHeading, _WaterDepth, _Choppiness, _RippleHeight, _WakeHeight;
                float _Refraction, _Reflection, _FresnelPower;
                float _Glint, _GlintSharpness, _Sparkle;
            CBUFFER_END

            // Globals from GardenWaterInteraction (sizes match its MaxBoats / MaxSplashes / MaxBobbers).
            float4 _GardenBoat[2];        // world xyz, speed (m/s)
            float4 _GardenBoatAxis[2];    // heading xz, half length, half width
            float _GardenBoatCount;
            float4 _GardenSplash[12];     // landing line: start xz, end xz
            float4 _GardenSplashInfo[12]; // x: water height, y: strength
            float _GardenSplashCount;
            float4 _GardenBobber[8];      // world xyz of a bobbing platform, radius
            float4 _GardenBobberInfo[8];  // x: vertical speed (m/s), y: water height
            float _GardenBobberCount;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            // Angular frequency of a wave of wavenumber k on water of the given depth (finite-depth dispersion).
            float Omega(float k) { return sqrt(9.81 * k * tanh(k * _WaterDepth)); }

            // Four Gerstner waves from one heading: x heading offset (deg), y wavelength and z height (fractions of
            // the main wave), w phase. Returns the displacement at p (world xz); `normal` is the exact surface normal.
            float3 Gerstner(float2 p, out float3 normal)
            {
                static const float4 waves[4] = { float4(0, 1.0, 1.0, 0.0), float4(23, 0.62, 0.55, 1.7),
                                                 float4(-29, 0.41, 0.33, 4.1), float4(48, 0.26, 0.18, 2.6) };
                float3 offset = 0;
                float sx = 0, sz = 0, sy = 0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float a = radians(_WaveHeading + waves[k].x);
                    float2 d = float2(cos(a), sin(a));
                    float kk = 6.2831853 / max(_WaveLength * waves[k].y, 0.1);
                    float A = _WaveHeight * waves[k].z;
                    float Q = _Choppiness / max(kk * A * 4, 1e-4);
                    float f = kk * dot(d, p) - Omega(kk) * _Time.y + waves[k].w;
                    float c = cos(f), s = sin(f);
                    offset.xz += Q * A * d * c;
                    offset.y += A * s;
                    sx += d.x * kk * A * c;
                    sz += d.y * kk * A * c;
                    sy += Q * kk * A * s;
                }
                normal = normalize(float3(-sx, 1 - sy, -sz));
                return offset;
            }

            // Rings of wavelength `len` leaving a source at distance r (m) at the dispersion speed: d(height)/dr.
            float RingSlope(float r, float len, float height, float reach)
            {
                float k = 6.2831853 / len;
                float f = k * r - Omega(k) * _Time.y;
                return height * exp(-r / reach) * (k * cos(f) - sin(f) / reach);
            }

            // Height of one boat's waves at p.
            float BoatWaves(float2 p, float4 boat, float4 axis)
            {
                float2 f = normalize(axis.xy), side = float2(-f.y, f.x);
                float2 r = p - boat.xz;
                float along = dot(r, f), across = dot(r, side);
                float hl = axis.z, hw = axis.w;
                float U = max(boat.w, 0.0);
                float pace = saturate(U / 1.6);
                // The wave that keeps pace with the boat: lambda = 2 pi U^2 / g (no shorter than 0.5 m, so a slow boat
                // does not shimmer).
                float k0 = min(9.81 / max(U * U, 0.05), 12.0);
                float e = length(float2(along / hl, across / hw));
                float rim = max(e - 1.0, 0) * hw;          // metres out from the hull
                float h = 0;
                // Bow wave ahead, a trough along the sides, a stern wave behind.
                float shell = exp(-(rim * rim) / 0.36);
                float bow = saturate(along / hl), stern = saturate(-along / hl);
                h += _WakeHeight * pace * shell * (1.1 * bow * bow - 0.45 * (1 - bow - stern) + 0.6 * stern * stern);
                // Kelvin wake behind the stern: transverse crests across the track and divergent crests on the arms.
                float x = -along - hl * 0.7;
                if (x > 0)
                {
                    float y = abs(across);
                    float wedge = x * 0.3536 + hw * 0.6;   // tan 19.47 degrees
                    float inside = saturate(1 - y / wedge);
                    float fade = _WakeHeight * pace * pace / sqrt(1 + x / 2.5);   // grows with speed squared
                    h += fade * 0.8 * inside * cos(k0 * x);
                    float off = (y - wedge) / (0.3 + 0.06 * x);
                    float arm = exp(-off * off);
                    h += fade * arm * cos(k0 * 2.2 * (0.94 * y + 0.34 * x));
                }
                // While it rests or bobs, small rings leave the hull.
                float rk = 6.2831853 / 1.2;
                h += _RippleHeight * 0.4 * exp(-rim / 3.0) * sin(rk * rim - Omega(rk) * _Time.y);
                return h;
            }

            // Extra surface slope from the waterfalls, the platforms and the boats at world point (p.xz, height y).
            float2 Interaction(float2 p, float y)
            {
                float2 slope = 0;
                [loop] for (int k = 0; k < 12; k++)
                {
                    if (k >= (int)_GardenSplashCount) break;
                    if (abs(y - _GardenSplashInfo[k].x) > 1.0) continue;
                    float2 a = _GardenSplash[k].xy, ab = _GardenSplash[k].zw - a;
                    float h = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-4));
                    float2 q = p - (a + ab * h);
                    float d = length(q);
                    // Short rings from the impact over longer ones, both at the dispersion speed.
                    float s = RingSlope(d, 0.9, 0.012, 1.6) + RingSlope(d, 2.2, 0.02, 3.0);
                    slope -= q / max(d, 1e-3) * s * _GardenSplashInfo[k].y;
                }
                [loop] for (int m = 0; m < 8; m++)
                {
                    if (m >= (int)_GardenBobberCount) break;
                    if (abs(y - _GardenBobberInfo[m].y) > 1.0) continue;
                    float2 q = p - _GardenBobber[m].xz;
                    float dist = length(q);
                    float r = max(dist - _GardenBobber[m].w, 0);
                    float stir = 0.25 + saturate(abs(_GardenBobberInfo[m].x) / 0.35);
                    slope -= q / max(dist, 1e-3) * RingSlope(r, 1.6, _RippleHeight * stir, 4.0);
                }
                [loop] for (int b = 0; b < 2; b++)
                {
                    if (b >= (int)_GardenBoatCount) break;
                    float4 boat = _GardenBoat[b], axis = _GardenBoatAxis[b];
                    if (axis.z <= 0 || abs(y - boat.y) > 1.5) continue;
                    const float e = 0.04;
                    float h0 = BoatWaves(p, boat, axis);
                    float hx = BoatWaves(p + float2(e, 0), boat, axis);
                    float hz = BoatWaves(p + float2(0, e), boat, axis);
                    slope -= float2(hx - h0, hz - h0) / e;
                }
                return slope;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                // Only the top face swells; the sides of water boxes keep still.
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
                float t = _Time.y * _WaveSpeed;
                float2 p = i.positionWS.xz;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, p * _WaveScale + float2(t, t * 0.6)), _WaveStrength);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, p * _WaveScale * 1.9 + float2(-t * 0.7, t * 0.4)), _WaveStrength);
                half3 c = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, p * _DetailScale + float2(t * 2.1, -t * 1.6)), _DetailStrength);
                half up = saturate(i.normalWS.y);
                float2 slope = a.xy + b.xy + c.xy + Interaction(p, i.positionWS.y) * up;
                // The swell's exact normal at this pixel (smooth); the maps and the interactions bend it.
                float3 gn;
                Gerstner(p, gn);
                float3 base = normalize(lerp(normalize(i.normalWS), gn, up));
                float3 n = normalize(lerp(base, normalize(float3(base.x + slope.x, base.y, base.z + slope.y)), up));

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float surface = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float scene = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float thick = max(0, scene - surface);

                // Refract: bend the lookup by the waves, but never pick up something standing in front of the water.
                float2 ruv = uv + n.xz * _Refraction * saturate(thick);
                float rscene = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams);
                if (rscene < surface) { ruv = uv; rscene = scene; }
                float rthick = max(0, rscene - surface);
                // The view ray crosses more water than the vertical depth when looking down at a slant.
                float depth = rthick * saturate(abs(V.y) + 0.15);
                half3 behind = SampleSceneColor(ruv);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shade = lerp(0.72, 1, sun.shadowAttenuation);
                half lambert = saturate(dot(n, sun.direction)) * 0.3 + 0.7;
                half absorb = 1 - exp(-depth / max(_Clarity, 0.05));
                half3 deep = _DeepColor.rgb * lambert * shade * sun.color;
                half3 body = lerp(behind * _ShallowColor.rgb, deep, absorb);

                half ndv = saturate(dot(n, V));
                half fres = 0.02 + 0.98 * pow(1 - ndv, _FresnelPower);
                half3 env = GlossyEnvironmentReflection(reflect(-V, n), 0.06h, 1.0h);
                half3 sky = lerp(_SkyColor.rgb, env, 0.65);
                half3 col = lerp(body, sky, saturate(fres * _Reflection));

                float3 h = normalize(sun.direction + V);
                half ndh = saturate(dot(n, h));
                half glint = pow(ndh, _GlintSharpness) * _Glint;
                // Sparkle: a broader highlight cut to sharp flecks on the steepest ripples.
                half fleck = smoothstep(0.55, 0.9, length(c.xy) / max(_DetailStrength, 0.01)) * pow(ndh, 64) * _Sparkle;
                col += sun.color * (glint + fleck) * sun.shadowAttenuation;

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
