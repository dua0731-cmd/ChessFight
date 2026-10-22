// What holds the bishop's rope (R142, 승규 님 10-15: "비숍 교차 밧줄 할 때 밧줄 지탱하는 곳을 좀 더 사실 적으로 묘사해줘"): a
// squared wooden stake (PawnRushSkillFx.Stake.cs). The grain comes from where each point lies in the log it was split from:
// the log's heart off to one side (a little different for each stake, from where it stands), so its growth rings run as
// long wavy stripes down the faces and as arcs across the head (the end grain: where the mesh faces up), with fine fibres
// along it, pores and checks in the end grain, a split down one face from the head (the mallet), the head's crushed edge
// darker, the wood darker and duller where it goes into the floor. Matte with a faint sheen of handled wood, lit by the
// effects' sun (_FxSun) like Skill Rope. Each facet is shaded flat from its slope on screen (the mesh's own normals are
// smooth, for the kit's ink edge). The same property names as Skill Gloss and Skill Rope, so a SkillInkKit.Prop drives
// it (_Lit pale wood, _Mid wood, _Shade the grain, _Flash white, _Color.a fades it by a fine dither). Casts a shadow.
// Built-in render pipeline. Noise: SkillNoise.cginc.
Shader "ChessFight/Skill Wood"
{
    Properties
    {
        _Lit ("Pale wood", Color) = (0.71, 0.54, 0.37, 1)
        _Mid ("Wood", Color) = (0.54, 0.4, 0.27, 1)
        _Shade ("Grain", Color) = (0.31, 0.21, 0.13, 1)
        _Flash ("White", Range(0, 1)) = 0
        _Color ("Tint (a = fade)", Color) = (1, 1, 1, 1)
        _Top ("Head height (m, mesh space)", Float) = 0.32
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }

        Pass
        {
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SkillNoise.cginc"

            float4 _Lit, _Mid, _Shade, _Color, _FxSun;
            float _Flash, _Top;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 obj : TEXCOORD2;
                float3 objN : TEXCOORD3;
                float4 screen : TEXCOORD4;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.obj = v.vertex.xyz;
                o.objN = v.normal;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float Bayer4(float2 px)
            {
                int2 c = int2(fmod(px, 4));
                const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (m[c.y * 4 + c.x] + 0.5) / 16.0;
            }

            float4 frag (v2f i) : SV_Target
            {
                if (_Color.a < 0.999)
                {
                    float2 px = i.screen.xy / max(i.screen.w, 1e-5) * _ScreenParams.xy;
                    clip(_Color.a - Bayer4(px));
                }

                float3 p = i.obj;
                // The facet's own slope (flat, hewn), turned the way the smooth normal faces.
                float3 n = normalize(cross(ddy(i.obj), ddx(i.obj)));
                if (dot(n, i.objN) < 0) n = -n;
                // Each stake its own piece of the log (from where it stands).
                float seed = frac(dot(unity_ObjectToWorld._m03_m13_m23, float3(0.137, 0.311, 0.229))) * 40;
                // The log's heart off to one side; the rings round it, wavering.
                float2 heart = float2(0.13, -0.05) + float2(sin(seed), cos(seed * 1.3)) * 0.04;
                float2 q = p.xz - heart;
                float wave = SkillNoise(float3(p.xz * 9, p.y * 1.6) + seed) * 0.009 + SkillNoise(float3(p.xz * 30, p.y * 6) + seed * 2) * 0.002;
                float ring = frac((length(q) + wave) * 110);
                // Late wood: a darker band at the end of each year's ring, soft going in, sharp at its end.
                float late = smoothstep(0.62, 0.9, ring) * (1 - smoothstep(0.95, 1.0, ring));
                // Fibres along the stake on its faces; pores across the head (the end grain faces up).
                float endGrain = smoothstep(0.75, 0.9, n.y);
                float fibre = SkillNoise(float3(p.x * 260, p.y * 7, p.z * 260) + seed) * 0.5 + 0.5;
                float pores = SkillNoise(p * 420 + seed) * 0.5 + 0.5;
                float grain = lerp(fibre, pores, endGrain);
                float3 wood = lerp(_Mid.rgb, _Lit.rgb, saturate(grain * 0.55 + (1 - late) * 0.15));
                wood = lerp(wood, _Shade.rgb, late * lerp(0.5, 0.7, endGrain));
                // Patches of colour through the wood.
                wood *= 0.9 + 0.2 * (SkillNoise(float3(p.xz * 18, p.y * 3) + seed * 3) * 0.5 + 0.5);

                // The head: the cut end paler, its edge crushed darker by the mallet, checks running in from the edge.
                float edge = max(abs(p.x), abs(p.z)) / 0.0425;
                float ang = atan2(q.y, q.x);
                float check = smoothstep(0.94, 1.0, abs(sin(ang * 3 + seed + SkillNoise(p * 60) * 0.6))) * smoothstep(0.45, 1.0, edge);
                float3 head = wood * 1.12;
                head = lerp(head, _Shade.rgb * 0.8, smoothstep(0.8, 1.05, edge) * 0.6);
                head = lerp(head, _Shade.rgb * 0.45, check * 0.8);
                wood = lerp(wood, head, endGrain);

                // A split down one flat from the head.
                float z0 = 0.009 + SkillNoise(float3(seed, p.y * 40, 0)) * 0.003;
                float split = step(0.9, n.x) * (1 - smoothstep(0.0006, 0.0016, abs(p.z - z0))) * saturate((p.y - (_Top - 0.11)) / 0.11);
                wood = lerp(wood, _Shade.rgb * 0.35, split * 0.85);
                // Darker and duller where it goes into the floor.
                wood *= lerp(0.6, 1, smoothstep(-0.005, 0.05, p.y));

                float3 N = normalize(cross(ddy(i.world), ddx(i.world)));
                if (dot(N, i.normal) < 0) N = -N;
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                float3 L = dot(_FxSun.xyz, _FxSun.xyz) > 1e-6 ? normalize(_FxSun.xyz) : normalize(float3(0.35, 0.79, -0.5));
                // Matte, a soft wrap from the sun, a little sky from above, a faint sheen on the handled faces.
                float wrap = saturate((dot(N, L) + 0.25) / 1.25);
                float3 col = wood * (0.42 + 0.72 * wrap + 0.1 * saturate(N.y));
                col += pow(saturate(dot(N, normalize(L + V))), 18) * 0.05 * (1 - endGrain);
                col = lerp(col, float3(1, 1, 1), _Flash);
                col *= _Color.rgb;
                return float4(col, 1);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex CFShadowVertex
            #pragma fragment CFShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            float4 _Color;
            #define CF_SHADOW_CLIP clip(_Color.a - 0.5);
            #include "Assets/Shaders/URP/CFShadowCaster.hlsl"
            ENDHLSL
        }
    }
}
