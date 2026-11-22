// Shallow running water a few centimetres deep over a stone bed: the east lock of the royal water garden (2026-11-22,
// ArtSource/PawnRush/GardenConcept/island_lock_sample_v2.py "Water lock", GardenFinish.fbx).
// The mesh carries the flow: UV0 = metres (u across, v downstream), vertex colour r = how much foam stands there
// (under each gate's roller, along the walls, at the inflow and the drain), g = across 0..1, b = a seed. Its surface is
// already shaped (rollers, standing waves, shock lines); this draws the water on it:
//   the bed seen through, refracted by ripples that run downstream at _Speed
//   sunlight caustics drifting over the bed (the edges of a moving cell pattern)
//   foam where the mesh says so, broken into patches that travel with the water, and thin streaks along the flow
//   a sky-tinted sheen where it is seen flat, the sun's glint
Shader "ChessFight/Garden Flow Water"
{
    Properties
    {
        _Tint ("Water tint over the bed", Color) = (0.55, 0.93, 0.95, 1)
        _FoamColor ("Foam", Color) = (0.95, 0.98, 1.0, 1)
        _SkyColor ("Sheen when seen flat", Color) = (0.75, 0.92, 1.0, 1)
        _Speed ("Flow (m/s)", Float) = 1.5
        _Refraction ("Refraction", Range(0, 0.1)) = 0.02
        _Ripple ("Ripple strength", Range(0, 1)) = 0.35
        _CausticScale ("Caustic cells per metre", Float) = 2.6
        _CausticWidth ("Caustic line width", Range(0.01, 0.3)) = 0.06
        _Caustic ("Caustic brightness", Range(0, 1)) = 0.35
        _Specular ("Sun glint", Range(0, 2)) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _FoamColor, _SkyColor;
                float _Speed, _Refraction, _Ripple, _CausticScale, _CausticWidth, _Caustic, _Specular;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv0:TEXCOORD0; float4 color:COLOR; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                float2 uv:TEXCOORD2;
                float4 data:TEXCOORD3;
                float fog:TEXCOORD4;
            };
            float FlowHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float2 FlowHash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }
            float FlowNoise(float2 p)
            {
                float2 a = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(FlowHash(a), FlowHash(a + float2(1, 0)), f.x), lerp(FlowHash(a + float2(0, 1)), FlowHash(a + 1), f.x), f.y);
            }
            // The ripples' height field (metres across, metres downstream moving with the water).
            float Ripple(float u, float flow)
            {
                return FlowNoise(float2(u * 1.7, flow * 0.9)) * 0.6 + FlowNoise(float2(u * 4.3 + 3.1, flow * 2.2 + 1.7)) * 0.4;
            }
            // Distance to the nearest edge of a moving cell pattern: about 0 on the caustics' bright lines.
            float CausticEdge(float2 p, float t)
            {
                float2 g = floor(p), f = frac(p);
                float d1 = 8, d2 = 8;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 o = float2(x, y);
                    float2 h = FlowHash2(g + o);
                    float2 r = o + 0.5 + 0.4 * sin(t + 6.2831853 * h) - f;
                    float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) { d2 = d; }
                }
                return sqrt(d2) - sqrt(d1);
            }
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionWS = ws;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv0; o.data = v.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float foamHere = i.data.r, seed = i.data.b;
                float t = _Time.y;
                float flow = i.uv.y - t * _Speed;                       // features travel downstream
                const float e = 0.04;
                float h0 = Ripple(i.uv.x, flow);
                float2 grad = float2(Ripple(i.uv.x + e, flow) - h0, Ripple(i.uv.x, flow + e) - h0) / e;
                half3 n = normalize(normalize(i.normalWS) + half3(grad.x, 0, grad.y) * _Ripple);
                // The bed through the water, bent by the ripples.
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                half3 behind = SampleSceneColor(suv + grad * _Refraction * 0.1);
                // Caustics drifting over the bed, slower than the water.
                float ce = CausticEdge(float2(i.uv.x, i.uv.y - t * _Speed * 0.35) * _CausticScale + grad * 0.15, t * 1.3);
                half caustic = 1 - smoothstep(0.0, _CausticWidth, ce);
                // Foam: where the mesh puts it, in patches carried by the water; thin streaks along the flow.
                float fp = FlowNoise(float2(i.uv.x * 3.2 + seed * 13.0, flow * 1.1)) * 0.7 + FlowNoise(float2(i.uv.x * 9.0, flow * 3.0)) * 0.3;
                half patches = smoothstep(1.0, 1.12, fp * 0.9 + foamHere * 0.75);
                // Streaks (2026-11-22, 승규 on Unity: the white band "좀 더 얇게 해서 물 그래픽 처럼"): thin wavy lines along
                // one contour of a stretched noise - a few centimetres wide, not a filled band - and see-through.
                float sn = FlowNoise(float2(i.uv.x * 3.0 + seed * 7.0, flow * 0.14)) * 0.75 + FlowNoise(float2(i.uv.x * 7.0, flow * 0.5)) * 0.25;
                half streak = (1 - smoothstep(0.0, 0.012, abs(sn - 0.5))) * (foamHere * 0.6 + 0.25);
                half white = saturate(max(patches, streak * 0.55));
                Light sun = GetMainLight();
                half3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                half fres = pow(1 - saturate(dot(n, V)), 4);
                half spec = pow(saturate(dot(n, normalize(sun.direction + V))), 120);
                half3 water = behind * _Tint.rgb + _Tint.rgb * caustic * _Caustic * sun.color + _SkyColor.rgb * fres * 0.6;
                half3 foam = _FoamColor.rgb * (0.8 + 0.2 * saturate(dot(n, sun.direction)));
                half3 c = lerp(water, foam, white) + sun.color * spec * _Specular * (1 - white);
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
