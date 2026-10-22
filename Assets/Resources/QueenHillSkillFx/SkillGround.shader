// The Queen of the Hill skill test bed's ground (R137, 승규 님 10-14: "실제 인 게임 에서는 체스판 바닥이니까 모든 바닥을 체스판
// 바닥 같이 만들어주고 언덕도 자연스럽게 파괴가 되게 해줘 벽도 마찬가지로 벽도 임팩트가 있게 파괴 되게끔"): every floor, step and
// wall of the scene wears it.
// - Its upward faces are a chessboard: 1.5 m cream and walnut squares (the hill's and the course's colours) laid out in the
//   world, so they run on from one block to the next, a thin seam between them, each square a hair its own shade. Its other
//   faces keep their own colour and the lab's grid. Lit like the Standard shader it stands in for (the sun, its shadows,
//   the sky's light).
// - Broken (CF_BREAKS, on while the bishop's strike has broken something): the shapes the strike cut out of the world
//   (_Break*: a crater's bowl, the trenches its blades plough, pieces knocked off a step's edge or out of a wall's face)
//   are taken away from every block that wears this. A pixel of a face inside one follows the eye's ray on into the block
//   until it meets what is left and draws that, at that depth (so a piece standing in it hides and shows as it should):
//   the block's inside (the board's thickness, then its body), rough, lit as it faces the sun, shadowed by the rest of the
//   block, darker the deeper, its broken edge pale. A ray that leaves the block first sees through it. Faces round a cut
//   get its dark broken edge, the grit thrown out of it and cracks.
// URP (lit like it: the sun and its shadows, the other lights, the sky). The blocks are boxes: per block its mesh's bounds (_BoxMin, _BoxMax), the world into its own
// space (_WorldToBox) and its scale, set from its transform — not unity_WorldToObject, which batching changes (the hill's
// tiers were drawn as one and a hole in one saw straight through it).
Shader "ChessFight/Skill Ground"
{
    Properties
    {
        _Color ("Colour (its sides)", Color) = (0.88, 0.88, 0.86, 1)
        _MainTex ("The lab's grid (its sides)", 2D) = "white" {}
        _Board ("Chessboard on its upward faces (0/1)", Float) = 0
        _BoardA ("Light squares", Color) = (0.929, 0.878, 0.761, 1)
        _BoardB ("Dark squares", Color) = (0.549, 0.38, 0.239, 1)
        _Square ("A square (m)", Float) = 1.5
        _Origin ("A corner of a square (world x, z)", Vector) = (0, 0, 0, 0)
        _Seam ("The line between squares", Color) = (0.22, 0.15, 0.1, 1)
        _Inlay ("The board's thickness (m)", Float) = 0.07
        _Body ("Inside it (under the board)", Color) = (0.36, 0.25, 0.16, 1)
        _Gloss ("Shine", Range(0, 1)) = 0.12
        _BoxMin ("Its box in its own space (set per block)", Vector) = (-0.5, -0.5, -0.5, 0)
        _BoxMax ("", Vector) = (0.5, 0.5, 0.5, 0)
        _BoxScale ("Its scale (set per block)", Vector) = (1, 1, 1, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "DisableBatching" = "True" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        #define BREAKS 96
        #define DIGS 4
        #define CRACKS 8

        float4 _Color, _BoardA, _BoardB, _Seam, _Body, _Origin, _BoxMin, _BoxMax, _BoxScale;
        float4x4 _WorldToBox;
        float _Board, _Square, _Inlay, _Gloss;
        sampler2D _MainTex;
        float4 _MainTex_ST;

        // What the strike broke (QueenHillSkillFx.Dig.cs, every frame). _BreakE: a ball round each shape (xyz, radius) with
        // its kind in the hundreds (0 bowl, 1 trench run, 2 lump).
        float _BreakCount, _CrackCount;
        float4 _BreakA[BREAKS];
        float4 _BreakB[BREAKS];
        float4 _BreakC[BREAKS];
        float4 _BreakD[BREAKS];
        float4 _BreakE[BREAKS];
        // Per strike: how far each of its four blades has cut along its arm (m); healed (0..1), its fresh cuts' glow (0..1),
        // its crater grown (0..1); where its blades are.
        float4 _DigReach[DIGS];
        float4 _DigState[DIGS];
        float4 _DigBlade[DIGS * 4];
        // Per strike: its shapes (the first, how many) and a ball round all of them (and the grit round them): a pixel
        // outside it looks at none of them (every ground pixel asks).
        float4 _DigRange[DIGS];
        float4 _DigBound[DIGS];
        // Cracks: the middle and the radius they start at; the face they run on (its normal) and how long they get; how
        // many, seed, strike, grown (0..1).
        float4 _CrackA[CRACKS];
        float4 _CrackB[CRACKS];
        float4 _CrackC[CRACKS];

        float GHash1(float n) { return frac(sin(n) * 43758.5453); }
        float GNoise1(float x)
        {
            float i = floor(x), f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(GHash1(i), GHash1(i + 1.0), f);
        }
        float GHash2(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }
        float GNoise2(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = GHash2(i), b = GHash2(i + float2(1, 0)), c = GHash2(i + float2(0, 1)), d = GHash2(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }
        float GHash3(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }
        float GNoise3(float3 x)
        {
            float3 i = floor(x), f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(GHash3(i), GHash3(i + float3(1, 0, 0)), f.x), lerp(GHash3(i + float3(0, 1, 0)), GHash3(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(GHash3(i + float3(0, 0, 1)), GHash3(i + float3(1, 0, 1)), f.x), lerp(GHash3(i + float3(0, 1, 1)), GHash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

        // The chessboard's colour at a point of an upward face, filtered by w (m per pixel) so it does not shimmer far off.
        float3 BoardAt(float2 xz, float2 w)
        {
            float2 g = (xz - _Origin.xy) / _Square;
            float2 fw = max(w / _Square, 1e-4) + 0.002;
            float2 tri = 2.0 * (abs(frac((g - 0.5 * fw) * 0.5) - 0.5) - abs(frac((g + 0.5 * fw) * 0.5) - 0.5)) / fw;
            float dark = 0.5 - 0.5 * tri.x * tri.y;
            float2 cell = floor(g);
            float3 c = lerp(_BoardA.rgb, _BoardB.rgb, dark) * (0.955 + 0.09 * GHash2(cell + 17.0));
            // A soft grain along each square.
            c *= 0.975 + 0.05 * GNoise2(float2(g.x * 2.0 + cell.y * 3.1, g.y * 26.0 + cell.x * 1.7));
            // The seam between squares, gone where a pixel is wider than it.
            float2 e = (0.5 - abs(frac(g) - 0.5)) * _Square;
            float sw = max(max(w.x, w.y), 1e-4);
            float seam = (1.0 - smoothstep(0.006, 0.006 + sw * 1.5, min(e.x, e.y))) * saturate(1.0 - sw * 25.0);
            return lerp(c, _Seam.rgb, seam * 0.55);
        }

        // ---------------------------------------------------------------- what the strike broke

        // A crater: the cap of a ball under the face it broke, rough round its rim, rubble on its bottom; it grows out of the
        // point it was struck. A: the ball's middle and radius at full size; B: the face's height (nothing above it is cut),
        // seed, strike, the struck point's height.
        float BowlCut(int k, float3 p)
        {
            float4 A = _BreakA[k], B = _BreakB[k];
            float4 st = _DigState[(int)B.z];
            float grow = max(st.z, 0.02);
            float3 F = float3(A.x, B.w, A.z);
            float3 C = F + (A.xyz - F) * grow;
            float3 q = p - C;
            // Rough round its rim and lumpy on its bottom: a few waves (cheap; this is worked out many times a pixel).
            float2 h = q.xz / max(length(q.xz), 1e-4);
            float rough = 0.075 * sin(dot(h, float2(2.9, 1.3)) * 1.7 + B.y) + 0.045 * sin(dot(h, float2(-4.1, 5.3)) + B.y * 1.7)
                + 0.03 * sin(dot(h, float2(8.3, -6.7)) + B.y * 2.3);
            float rho = A.w * grow * (1.0 + rough);
            float d = length(q) - rho;
            d = max(d, p.y - B.x);
            float bottom = C.y - rho + grow * (0.038 + 0.03 * sin(p.x * 23.0 + B.y) * sin(p.z * 19.0 + B.y * 1.3));
            d = max(d, bottom - p.y);
            return d + st.x * 0.5;
        }

        // A run of a trench: a to b (its bottom line, world) with their half widths; C: how high it is cut over its bottom (m),
        // which blade (0..3), where a and b are along that blade's arm (m); D: seed, strike, its depth at a and at b (m). Only
        // as far as the blade has got; just behind it the trench comes up to the face.
        float SlotCut(int k, float3 p)
        {
            float4 A = _BreakA[k], B = _BreakB[k], C = _BreakC[k], D = _BreakD[k];
            int dig = (int)D.y;
            float2 ab = B.xz - A.xz;
            float len = max(length(ab), 1e-4);
            float2 dir = ab / len;
            float2 rel = p.xz - A.xz;
            float along = dot(rel, dir);
            float u = saturate(along / len);
            float across = abs(rel.x * dir.y - rel.y * dir.x);
            float s = lerp(C.z, C.w, u);
            float reach = _DigReach[dig][(int)C.y];
            float hw = lerp(A.w, B.w, u) * (1.0 + 0.13 * sin(s * 9.0 + D.x) + 0.07 * sin(s * 27.0 + D.x * 2.0));
            float depth = lerp(D.z, D.w, u);
            float bottom = lerp(A.y, B.y, u) + depth * smoothstep(reach - 0.16, reach + 0.004, s) + 0.008 * (sin(p.x * 31.0 + D.x) + sin(p.z * 29.0 + D.x * 1.7));
            // A U across it: its sides round into its bottom.
            float x = saturate(across / max(hw, 1e-4));
            float floorAt = bottom + depth * 0.55 * (1.0 - sqrt(saturate(1.0 - x * x)));
            float d = max(across - hw, floorAt - p.y);
            d = max(d, p.y - (bottom + C.x));
            d = max(d, max(-along, along - len));
            d = max(d, s - reach);
            return d + _DigState[dig].x * 0.5;
        }

        // A piece knocked out: a lump (a ball stretched along three axes and broken up). A: its middle, seed; B, C, D: its axes
        // over their radii; B.w its strike, C.w its smallest radius (m), D.w how far it has opened (0..1).
        float BlobCut(int k, float3 p)
        {
            float4 A = _BreakA[k], B = _BreakB[k], C = _BreakC[k], D = _BreakD[k];
            float3 q = p - A.xyz;
            float3 l = float3(dot(q, B.xyz), dot(q, C.xyz), dot(q, D.xyz));
            float r = length(l);
            float3 n = l / max(r, 1e-4);
            float jag = 0.12 * sin(dot(n, float3(4.1, 2.3, 3.7)) + A.w) + 0.08 * sin(dot(n, float3(-5.3, 6.1, 1.9)) + A.w * 1.3)
                + 0.05 * sin(dot(n, float3(7.7, -5.1, 9.3)) + A.w * 2.1);
            float d = (r - max(D.w, 0.02) * (1.0 + jag)) * C.w;
            return d + _DigState[(int)B.w].x * 0.5;
        }

        // Below 0: inside what was broken out (about how far in, m); above: about how far from it.
        float Carve(float3 p)
        {
            float d = 1e3;
            [loop] for (int g = 0; g < DIGS; g++)
            {
                float4 R = _DigRange[g];
                if (R.y < 0.5) continue;
                float4 Bd = _DigBound[g];
                float bd = length(p - Bd.xyz) - Bd.w;
                if (bd > 0.0) { d = min(d, bd + 0.4); continue; }   // (its ball has 0.4 m to spare round its shapes)
                int k1 = (int)(R.x + R.y);
            [loop] for (int k = (int)R.x; k < k1; k++)
            {
                float4 E = _BreakE[k];
                float kind = floor(E.w * 0.01);
                float b = length(p - E.xyz) - (E.w - kind * 100.0);
                if (b > 0.03) { d = min(d, b); continue; }
                // (if, not ?: — that would work out all three)
                float c;
                if (kind < 0.5) c = BowlCut(k, p);
                else if (kind < 1.5) c = SlotCut(k, p);
                else c = BlobCut(k, p);
                d = min(d, c);
            }
            }
            return d;
        }

        // The shapes the eye's ray from P passes near (as far as L): a mask of up to 96.
        uint3 RayMask(float3 P, float3 V, float L)
        {
            uint3 m = 0;
            int n = (int)_BreakCount;
            [loop] for (int k = 0; k < BREAKS; k++)
            {
                if (k >= n) break;
                float4 E = _BreakE[k];
                float kind = floor(E.w * 0.01);
                float rad = E.w - kind * 100.0;
                float3 w = E.xyz - P;
                float3 off = w - V * clamp(dot(w, V), 0.0, L);
                if (dot(off, off) > rad * rad) continue;
                uint bit = 1u << (uint(k) & 31u), word = uint(k) >> 5;
                if (word == 0u) m.x |= bit;
                else if (word == 1u) m.y |= bit;
                else m.z |= bit;
            }
            return m;
        }

        // Carve, over only the shapes in the mask (no balls to test: the walk into a hole asks this ~25 times).
        float CarveIn(float3 p, uint3 m)
        {
            float d = 1e3;
            [loop] for (uint w = 0; w < 3; w++)
            {
                uint bits = w == 0u ? m.x : w == 1u ? m.y : m.z;
                [loop] while (bits != 0u)
                {
                    uint b = firstbitlow(bits);
                    bits &= bits - 1u;
                    int k = (int)(w * 32u + b);
                    float kind = floor(_BreakE[k].w * 0.01);
                    float c;
                    if (kind < 0.5) c = BowlCut(k, p);
                    else if (kind < 1.5) c = SlotCut(k, p);
                    else c = BlobCut(k, p);
                    d = min(d, c);
                }
            }
            return d;
        }

        // Cracks running out from where it broke, on faces along the plane they run in: 0 .. 1. Round a crater (a star
        // on an upward face) also the grit thrown out of it (grit: 0 .. 1).
        float Cracks(float3 P, float3 N0, out float grit)
        {
            float best = 0.0;
            grit = 0.0;
            int n = (int)_CrackCount;
            [loop] for (int k = 0; k < CRACKS; k++)
            {
                if (k >= n) break;
                float4 A = _CrackA[k], B = _CrackB[k], C = _CrackC[k];
                float3 d = P - A.xyz;
                float r = length(d);
                float R0 = A.w, len = B.w * C.w;
                if (r > max(R0 + len, R0 * 1.9) || r < R0 * 0.8 || abs(dot(N0, B.xyz)) < 0.6 || abs(dot(d, B.xyz)) > 0.15) continue;
                float3 u = normalize(cross(B.xyz, abs(B.y) < 0.9 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 v = cross(B.xyz, u);
                float ang = atan2(dot(d, v), dot(d, u)) / 6.2831853;
                float o = r - R0 * 0.8;
                float heal = _DigState[(int)C.z].x;
                if (B.y > 0.9) grit = max(grit, smoothstep(R0 * 1.9, R0 * 1.02, r) * C.w * (1.0 - heal));
                [loop] for (int j = 0; j < 12; j++)
                {
                    if (j >= C.x) break;
                    float h1 = GHash1(j * 7.31 + C.y), h2 = GHash1(j * 3.17 + C.y + 1.7);
                    float a0 = (j + 0.2 + 0.6 * h1) / C.x;
                    float l = len * (0.4 + 0.6 * h2);
                    if (o > l) continue;
                    // (nowhere near this line, wobble and all: on to the next)
                    if (abs(frac(ang - a0 + 0.5) - 0.5) * 6.2831853 * max(r, 0.05) > 0.08 + 0.4 * r) continue;
                    float wob = 0.045 * sin(r * 8.0 + j * 5.3) + 0.0175 * sin(r * 26.0 + j * 11.1);
                    float side = abs(frac(ang - a0 - wob + 0.5) - 0.5) * 6.2831853 * max(r, 0.05);
                    float wid = 0.014 * (1.0 - o / l) + 0.002;
                    best = max(best, (1.0 - smoothstep(wid * 0.4, wid, side)) * (1.0 - heal));
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- the block itself

        // How far inside its block a point is (m, below 0 outside), and how far under its top face.
        float2 InBlock(float3 w)
        {
            float3 o = mul(_WorldToBox, float4(w, 1.0)).xyz;
            float3 sc = abs(_BoxScale.xyz);
            float3 lo = (o - _BoxMin.xyz) * sc, hi = (_BoxMax.xyz - o) * sc;
            return float2(min(min(min(lo.x, lo.y), lo.z), min(min(hi.x, hi.y), hi.z)), hi.y);
        }

        // How far the eye's ray from a point on its face runs inside its block (in the ray's own steps).
        float BlockExit(float3 w, float3 v)
        {
            float3 o = mul(_WorldToBox, float4(w, 1.0)).xyz;
            float3 d = mul((float3x3)_WorldToBox, v);
            float3 inv = 1.0 / (d + (d >= 0.0 ? 1e-6 : -1e-6));
            float3 t0 = (_BoxMin.xyz - o) * inv, t1 = (_BoxMax.xyz - o) * inv;
            float3 tm = max(t0, t1);
            return min(min(tm.x, tm.y), tm.z);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile _ CF_BREAKS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
#if defined(CF_BREAKS) && defined(SHADER_API_D3D11)
                // (a depth that is only ever farther needs the pixel's position read this way on D3D11)
                noperspective centroid float4 pos : SV_POSITION;
#else
                float4 pos : SV_POSITION;
#endif
                float3 wpos : TEXCOORD0;
                float3 wnrm : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogCoord : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.wpos = TransformObjectToWorld(v.vertex.xyz);
                o.pos = TransformWorldToHClip(o.wpos);
                o.wnrm = TransformObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.fogCoord = ComputeFogFactor(o.pos.z);
                return o;
            }

            // URP's screen-space ambient occlusion where it is on (1 where it is off), set per pixel in frag.
            static float gAoDirect = 1.0, gAoIndirect = 1.0;

            // The sun's shadow on a point (URP's main light, faded out with its shadow distance).
            float SunShadow(float3 P)
            {
                return GetMainLight(TransformWorldToShadowCoord(P), P, half4(1, 1, 1, 1)).shadowAttenuation;
            }

            float3 Lit(float3 albedo, float3 N, float3 V, float shadow, float gloss)
            {
                Light sun = GetMainLight();
                float3 L = sun.direction;
                float ndl = saturate(dot(N, L));
                float3 col = albedo * (sun.color * ndl * shadow * gAoDirect + SampleSH(N) * gAoIndirect);
                float3 H = normalize(L - V);
                return col + sun.color * gloss * pow(saturate(dot(N, H)), 40.0) * shadow * ndl;
            }

            // The other lights on a face, plain diffuse (what the Built-in ForwardAdd pass added).
            float3 OtherLights(float3 albedo, float3 N, float3 P, float4 pos)
            {
                float3 c = 0.0;
#if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = P;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(pos);
#if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light l = GetAdditionalLight(lightIndex, P, half4(1, 1, 1, 1));
                    c += albedo * l.color * saturate(dot(N, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                }
#endif
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l = GetAdditionalLight(lightIndex, P, half4(1, 1, 1, 1));
                    c += albedo * l.color * saturate(dot(N, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                LIGHT_LOOP_END
#endif
                return c;
            }

#ifdef CF_BREAKS
            // The depth it writes is only ever farther (into the block), so the GPU may still drop pixels hidden behind
            // others before shading them (with plain SV_Depth every ground pixel was shaded).
#if defined(SHADER_API_D3D11) && defined(UNITY_REVERSED_Z)
            float4 frag(v2f i, out float outDepth : SV_DepthLessEqual) : SV_Target
#elif defined(SHADER_API_D3D11)
            float4 frag(v2f i, out float outDepth : SV_DepthGreaterEqual) : SV_Target
#else
            float4 frag(v2f i, out float outDepth : SV_Depth) : SV_Target
#endif
#else
            float4 frag(v2f i) : SV_Target
#endif
            {
                float3 P = i.wpos;
                float3 N0 = normalize(i.wnrm);
                float3 V = normalize(P - _WorldSpaceCameraPos);
                float2 w = fwidth(P.xz);
                bool top = _Board > 0.5 && N0.y > 0.7;
                float3 grid = _Color.rgb * tex2D(_MainTex, i.uv).rgb;
                float3 albedo = top ? BoardAt(P.xz, w) : grid;
                float gloss = top ? _Gloss : _Gloss * 0.3;
                float atten = SunShadow(P);
#if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor aoAt = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.pos));
                gAoDirect = aoAt.directAmbientOcclusion;
                gAoIndirect = aoAt.indirectAmbientOcclusion;
#endif
                float3 col;
#ifdef CF_BREAKS
                outDepth = i.pos.z;
                float c0 = Carve(P);
                if (c0 < 0.0)
                {
                    // Broken out here: on into the block until what is left of it. (Steps of at least 1.2 cm: inside a cut the
                    // distance to its nearest wall is small along a wall, and tiny steps ran out before the far side — those
                    // pixels went missing and showed what was behind.)
                    float tOut = BlockExit(P, V);
                    uint3 mask = RayMask(P, V, min(tOut, 2.5));
                    float t = 0.0, tIn = 0.0;
                    bool found = false;
                    [loop] for (int s = 0; s < 48; s++)
                    {
                        float d = CarveIn(P + V * t, mask);
                        if (d >= 0.0) { found = true; break; }
                        tIn = t;
                        t += clamp(-d * 0.9, 0.015, 0.25);
                        if (t > tOut) break;
                    }
                    if (found)
                    {
                        [loop] for (int h = 0; h < 6; h++)
                        {
                            float tm = 0.5 * (tIn + t);
                            if (CarveIn(P + V * tm, mask) < 0.0) tIn = tm; else t = tm;
                        }
                    }
                    else
                    {
                        // Out of the block still inside what broke: it is broken through, see what is behind. Otherwise it
                        // ran out of steps: draw where it got to (better than a hole).
                        if (t > tOut && CarveIn(P + V * tOut, mask) < 0.0) discard;
                        t = min(t, tOut);
                    }
                    float3 E = P + V * t;
                    // Its slope there: four samples round it (in a loop, so Carve is not written out four more times).
                    const float e = 0.005;
                    float3 g = 0.0;
                    [loop] for (int q = 0; q < 4; q++)
                    {
                        float3 k = q == 0 ? float3(1, -1, -1) : q == 1 ? float3(-1, -1, 1) : q == 2 ? float3(-1, 1, -1) : float3(1, 1, 1);
                        g += k * CarveIn(E + k * e, mask);
                    }
                    float3 N = -normalize(g + float3(0, 1e-5, 0));
                    N = faceforward(N, V, N);
                    float2 inb = InBlock(E);
                    float deep = max(inb.x, 0.0);
                    float3 inside = (_Board > 0.5 && inb.y < _Inlay) ? BoardAt(E.xz, w) * 0.8 : _Body.rgb;
                    inside *= 0.78 + 0.42 * GNoise3(E * 24.0) * (0.55 + 0.45 * GNoise3(E * 7.0));
                    // Shadowed by the rest of the block.
                    float3 Ls = normalize(_MainLightPosition.xyz);
                    float self = 1.0;
                    [loop] for (int k = 1; k <= 6; k++)
                    {
                        float3 Q = E + N * 0.004 + Ls * (0.045 * k);
                        if (InBlock(Q).x <= 0.0) break;
                        if (CarveIn(Q, mask) > 0.004) { self = 0.5; break; }
                    }
                    float ao = lerp(0.95, 0.5, saturate(deep / 0.36));
                    col = Lit(inside, N, V, atten * self, 0.02) * ao;
                    // The broken edge of the face: a pale chipped band just under it.
                    float lip = 1.0 - smoothstep(0.004, 0.02, deep);
                    col = lerp(col, Lit(albedo * 1.04, N0, V, atten, gloss), lip * 0.55);
                    // A fresh cut glows a moment behind its blade.
                    float glow = 0.0;
                    [loop] for (uint gb = 0; gb < DIGS * 4; gb++)
                        glow = max(glow, _DigState[gb >> 2].y * exp(-length(E - _DigBlade[gb].xyz) / 0.32));
                    col += float3(1.8, 1.1, 0.45) * glow * (0.35 + 0.65 * saturate(deep / 0.12));
                    float4 cp = mul(UNITY_MATRIX_VP, float4(E, 1.0));
#if defined(UNITY_REVERSED_Z)
                    outDepth = cp.z / cp.w;
#else
                    outDepth = cp.z / cp.w * 0.5 + 0.5;
#endif
                    col = MixFog(col, i.fogCoord);
                    return float4(col, 1.0);
                }
                // Round a cut: its broken edge, the grit thrown out round a crater, cracks.
                float edge = 1.0 - smoothstep(0.004, 0.018, c0);
                float grit;
                float crack = Cracks(P, N0, grit);
                grit *= 0.45 + 0.55 * GNoise2(P.xz * 7.0 + 3.0);
                albedo *= 1.0 - 0.2 * grit;
                albedo = lerp(albedo, albedo * 0.28, crack * 0.75);
                col = Lit(albedo, N0, V, atten, gloss) * (1.0 - 0.72 * edge) + OtherLights(albedo, N0, P, i.pos);
#else
                col = Lit(albedo, N0, V, atten, gloss) + OtherLights(albedo, N0, P, i.pos);
#endif
                col = MixFog(col, i.fogCoord);
                return float4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex CFShadowVertex
            #pragma fragment CFShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Assets/Shaders/URP/CFShadowCaster.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex CFDepthVertex
            #pragma fragment CFShadowFragment
            #include "Assets/Shaders/URP/CFShadowCaster.hlsl"
            ENDHLSL
        }
    }
    Fallback Off
}
