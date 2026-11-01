// Royal garden water (R278, 승규 11-01: "the reference video's water, but clearer"; R279: bluer, and the boats and
// waterfalls act on it).
// See-through: the opaque scene behind the surface is refracted through the waves and tinted by how much water lies
// in front of it (scene depth minus surface depth), so shallow water stays clear and deep water turns blue.
// Three soft swells move the vertices, two scrolling normal maps add ripples, the sky is reflected at grazing
// angles, the sun leaves sharp glints and sparkles, and a thin foam line marks where the water meets stone.
// R279: GardenWaterInteraction sets global arrays: boats (hull foam, a V wake, bow ripples) and waterfall landing
// lines (churning foam, spreading rings). With no script (other scenes) the counts are 0 and nothing changes.
// Needs the URP depth and opaque textures (both on in PC_RPAsset / Mobile_RPAsset).
Shader "ChessFight/Pawn Rush Garden Clear Water"
{
    Properties
    {
        _ShallowColor ("Shallow tint", Color) = (0.36, 0.76, 0.98, 1)
        _DeepColor ("Deep colour", Color) = (0.01, 0.17, 0.50, 1)
        _Clarity ("Clear depth (m)", Float) = 1.2
        _SkyColor ("Sky tint", Color) = (0.52, 0.76, 1.0, 1)
        [Normal] _WaveMap ("Wave normals", 2D) = "bump" {}
        _WaveScale ("Wave size (per metre)", Float) = 0.15
        _WaveStrength ("Wave strength", Range(0, 2)) = 0.85
        _WaveSpeed ("Wave speed", Float) = 0.04
        _DetailScale ("Ripple size (per metre)", Float) = 0.85
        _DetailStrength ("Ripple strength", Range(0, 2)) = 0.45
        _SwellHeight ("Swell height (m)", Float) = 0.06
        _SwellLength ("Swell length (m)", Float) = 7
        _SwellSpeed ("Swell speed (m/s)", Float) = 1.2
        _Refraction ("Refraction", Range(0, 0.2)) = 0.04
        _Reflection ("Reflection", Range(0, 1)) = 0.8
        _FresnelPower ("Fresnel power", Range(1, 8)) = 5
        _Glint ("Sun glint", Range(0, 12)) = 7
        _GlintSharpness ("Glint sharpness", Range(16, 2048)) = 900
        _Sparkle ("Sparkle", Range(0, 4)) = 1.5
        _FoamColor ("Foam", Color) = (0.93, 0.98, 1, 1)
        _FoamDepth ("Edge foam depth (m)", Float) = 0.22
        _SplashFoam ("Waterfall foam", Range(0, 2)) = 1
        _WakeFoam ("Boat wake foam", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        // The scene behind is sampled and blended here, so the result is written as is; writing depth hides the
        // underwater part of anything transparent drawn later (the waterfall sheets' feet).
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
                half4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor;
                float4 _WaveMap_ST;
                float _Clarity, _WaveScale, _WaveStrength, _WaveSpeed, _DetailScale, _DetailStrength;
                float _SwellHeight, _SwellLength, _SwellSpeed, _Refraction, _Reflection, _FresnelPower;
                float _Glint, _GlintSharpness, _Sparkle, _FoamDepth, _SplashFoam, _WakeFoam;
            CBUFFER_END

            // Globals from GardenWaterInteraction (sizes match its MaxBoats / MaxSplashes).
            float4 _GardenBoat[2];        // world xyz, speed (m/s)
            float4 _GardenBoatAxis[2];    // heading xz, half length, half width
            float _GardenBoatCount;
            float4 _GardenSplash[12];     // landing line: start xz, end xz
            float4 _GardenSplashInfo[12]; // x: water height, y: strength
            float _GardenSplashCount;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            float GardenHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float GardenNoise(float2 p)
            {
                float2 a = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(GardenHash(a), GardenHash(a + float2(1, 0)), f.x),
                            lerp(GardenHash(a + float2(0, 1)), GardenHash(a + 1), f.x), f.y);
            }

            // Three crossing swells: height and its slope at p (world xz).
            float Swell(float2 p, out float2 slope)
            {
                static const float2 dirs[3] = { float2(0.83, 0.56), float2(-0.42, 0.91), float2(0.97, -0.24) };
                static const float lens[3] = { 1.0, 0.61, 0.37 };
                float h = 0; slope = 0;
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float len = _SwellLength * lens[k];
                    float w = 6.2831853 / len;
                    float a = _SwellHeight * lens[k];
                    float ph = w * (dot(dirs[k], p) - _SwellSpeed * sqrt(lens[k]) * _Time.y);
                    h += a * sin(ph);
                    slope += a * w * cos(ph) * dirs[k];
                }
                return h;
            }

            // Foam amount and extra slope from the waterfalls and the boats at world point (p.xz, height y).
            float Interaction(float2 p, float y, inout float2 slope)
            {
                float t = _Time.y;
                float foam = 0;
                [loop] for (int k = 0; k < 12; k++)
                {
                    if (k >= (int)_GardenSplashCount) break;
                    if (abs(y - _GardenSplashInfo[k].x) > 1.0) continue;
                    float2 a = _GardenSplash[k].xy, ab = _GardenSplash[k].zw - a;
                    float h = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-4));
                    float2 q = p - (a + ab * h);
                    float d = length(q);
                    float2 dir = q / max(d, 1e-3);
                    float ring = d * 5.5 - t * 4.5;
                    slope += dir * cos(ring) * 0.35 * exp(-d / 2.4) * _GardenSplashInfo[k].y;
                    float churn = GardenNoise(p * 2.2 + float2(t * 0.8, -t * 1.1));
                    foam += (exp(-d / 0.75) * (0.7 + 0.6 * churn) + saturate(sin(ring) * 0.5 + 0.5) * exp(-d / 2.0) * 0.4 * churn)
                            * _SplashFoam * _GardenSplashInfo[k].y;
                }
                [loop] for (int b = 0; b < 2; b++)
                {
                    if (b >= (int)_GardenBoatCount) break;
                    float4 boat = _GardenBoat[b], axis = _GardenBoatAxis[b];
                    if (axis.z <= 0 || abs(y - boat.y) > 1.5) continue;
                    float2 f = normalize(axis.xy), side = float2(-f.y, f.x);
                    float2 r = p - boat.xz;
                    float along = dot(r, f), across = dot(r, side);
                    float hl = axis.z, hw = axis.w;
                    float pace = saturate(boat.w / 1.8);
                    float e = length(float2(along / hl, across / hw));
                    float churn = GardenNoise(p * 3.0 + t * float2(0.6, -0.5));
                    // Foam hugging the hull, stronger at the bow when moving.
                    float bow = saturate(along / hl);
                    float hull = smoothstep(1.4, 1.0, e) * (0.25 + 0.75 * pace * (0.4 + 0.6 * bow));
                    // V wake behind the stern: two arms at about 19.5 degrees and churned water down the middle.
                    float back = -along - hl * 0.85;
                    float wake = 0;
                    if (back > 0)
                    {
                        float w = hw * 0.7 + back * 0.36;
                        float off = abs(across) - w;
                        float arms = exp(-off * off / (0.05 + 0.025 * back));
                        float centre = exp(-across * across / (hw * hw)) * exp(-back / 4.0);
                        wake = (arms * exp(-back / 9.0) + centre * 0.9) * pace;
                        slope += side * sign(across) * cos(off * 9.0 - t * 6.0) * 0.3 * arms * pace;
                    }
                    float rd = max(e - 1.0, 0) * hw;
                    slope += normalize(r + 1e-4) * cos(rd * 8.0 - t * 7.0) * 0.18 * exp(-rd / 1.5) * (0.3 + pace);
                    foam += (hull + wake) * (0.55 + 0.7 * churn) * _WakeFoam;
                }
                return foam;
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(v.normalOS);
                // Only the top face swells; the sides of water boxes keep still.
                float up = saturate(nws.y * 2 - 1);
                float2 slope;
                ws.y += Swell(ws.xz, slope) * up;
                nws = normalize(lerp(nws, float3(-slope.x, 1, -slope.y), up));
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
                float2 slope = a.xy + b.xy + c.xy;
                half up = saturate(i.normalWS.y);
                float stir = Interaction(p, i.positionWS.y, slope) * up;
                float3 base = normalize(i.normalWS);
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
                half shade = lerp(0.6, 1, sun.shadowAttenuation);
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

                // Foam: a thin line where the water meets stone, and the churn from falls and boats, broken into patches.
                half edge = 1 - saturate(rthick / max(_FoamDepth, 0.01));
                half patches = smoothstep(0.3, 0.75, GardenNoise(p * 4.0 + float2(_Time.y * 0.3, 0)) * 0.6 + GardenNoise(p * 9.0 - _Time.y * 0.5) * 0.4);
                half foam = saturate(edge * edge * saturate(0.5 + (a.x - b.y) * 1.5) + stir * (0.35 + 0.65 * patches)) * up;
                col = lerp(col, _FoamColor.rgb * (0.75 + 0.25 * lambert) * shade, foam);

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
