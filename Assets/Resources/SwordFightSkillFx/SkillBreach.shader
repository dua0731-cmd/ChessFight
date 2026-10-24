// The king's sword breaks into the platform's edge (Sword Fight, the king's edge skill; 승규 님 10-14: "킹 왕의 귀환 때
// 실제로 칼이 박히는 연출이랑 그에 따라 벽도 실제로 부서지고 그 주변에 금이 가있게"). The way the Queen of the Hill bishop's
// strike digs the floor (Skill Dig, R134), turned onto the platform's side: a crater broken into the wall face round the
// sword, reaching up through the lip so the top is bitten out too, cracks running out over the face and on over the lip.
// - Two quads draw it, one on the face and one on the top by the lip (_Surface 0 / 1); both follow the eye's ray into the
//   same broken stone (the box minus a bowl dug in from the face), so the far walls show and the near ones hide from any
//   angle. A ray that comes out through the open side of the bitten lip shows what is beyond the edge.
// - The sword stuck in it is drawn where it is inside the crater (the real blade is hidden in the box there): its blade and
//   guard as boxes, in the blade's colour, darker deeper in.
// - What the wall is made of comes from the wall itself (the screen grabbed under the quad, softened): darker and rougher
//   inside, lit by how each broken face turns to the sun, shadowed where the stone is in the way, darker the deeper; a pale
//   chipped band at the break; a fresh break glows a moment (_Glow); it all heals (_Heal).
// Pixels of untouched stone with no crack are dropped. URP, right after the opaque world.
Shader "ChessFight/Skill Breach"
{
    Properties
    {
        _Origin ("Where the sword went in (world, on the face)", Vector) = (0, 0, 0, 0)
        _Normal ("Out of the face (world, flat)", Vector) = (1, 0, 0, 0)
        _Box ("The platform: top (world y), bottom (world y)", Vector) = (0, -0.6, 0, 0)
        _Crater ("Crater: radius (m), depth (m), grown (0..1), seed", Vector) = (0.3, 0.17, 1, 0)
        _Cracks ("Cracks: count, length (m), width (m), darkness", Vector) = (9, 0.55, 0.013, 0.7)
        _Surface ("Which quad: 0 the face, 1 the top", Float) = 0
        _Heal ("Healed (0 broken .. 1 gone)", Range(0, 1)) = 0
        _Tint ("Inside the stone (times the wall's colour)", Color) = (0.86, 0.85, 0.84, 1)
        [HDR] _Glow ("A fresh break's glow (alpha: how much)", Color) = (2.2, 1.5, 0.45, 0)
        _BladeOn ("The sword is in it (1) or out (0)", Float) = 0
        _BladePos ("The sword's grip (world)", Vector) = (0, 0, 0, 0)
        _BladeX ("The sword's x (world)", Vector) = (1, 0, 0, 0)
        _BladeY ("The sword's y (world)", Vector) = (0, 1, 0, 0)
        _BladeZ ("The sword's z, along the blade (world)", Vector) = (0, 0, 1, 0)
        _BladeColor ("The blade's colour", Color) = (0.7, 0.8, 0.9, 1)
        _GuardColor ("The guard's colour", Color) = (0.85, 0.6, 0.21, 1)
        _Debug ("Debug: 1 paints the broken part red, the rest of the quad blue", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-499" "RenderType" = "Opaque" "IgnoreProjector" = "True" "DisableBatching" = "True" }
        // URP: the wall under it comes from the camera's copy of the opaque picture (_CameraOpaqueTexture), which is only
        // ready after the opaque pass, so it is drawn first thing after it (Transparent-499) instead of at Geometry+100.
        Pass
        {
            // Its own lighting from the main light (URP's _MainLightPosition); an untagged pass draws in URP.
            ZWrite Off
            ZTest LEqual
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float _Surface, _Heal, _Debug, _BladeOn;
            float4 _Origin, _Normal, _Box, _Crater, _Cracks, _Tint, _Glow;
            float4 _BladePos, _BladeX, _BladeY, _BladeZ, _BladeColor, _GuardColor;
            sampler2D _CameraOpaqueTexture;
            float4 _MainLightPosition;
            #define _SkillBreachGrab_TexelSize float4(1.0 / _ScreenParams.xy, _ScreenParams.xy)

            // The frame at the sword: out of the face, along the edge, up.
            static float3 gN, gU;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float4 grab : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.grab = ComputeScreenPos(o.pos);
                return o;
            }

            float Hash1(float n) { return frac(sin(n) * 43758.5453); }
            float Noise1(float x)
            {
                float i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash1(i), Hash1(i + 1.0), f);
            }
            float Hash2(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float Noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash2(i), b = Hash2(i + float2(1, 0)), c = Hash2(i + float2(0, 1)), d = Hash2(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // The nearest of a jittered grid of points (cell size 1): its id (0..1) and how far the second nearest is past it
            // (0 on a border between two cells).
            float2 Cells(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float d1 = 8, d2 = 8, id = 0;
                [unroll] for (int y = -1; y <= 1; y++)
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 o = float2(Hash2(i + g), Hash2(i + g + 17.3));
                    float2 r = g + o - f;
                    float d = dot(r, r);
                    if (d < d1) { d2 = d1; d1 = d; id = Hash2(i + g + 3.7); }
                    else if (d < d2) d2 = d;
                }
                return float2(id, sqrt(d2) - sqrt(d1));
            }

            // A point in the frame: x along the edge, y up from the sword, z into the stone (metres).
            float3 Frame(float3 p)
            {
                float3 d = p - _Origin.xyz;
                return float3(dot(d, gU), d.y, -dot(d, gN));
            }

            // The crater's rim radius that way (broken: wider and narrower round it), grown.
            float RimRadius(float2 q)
            {
                float r = length(q);
                float2 dir = r > 1e-4 ? q / r : float2(1, 0);
                float seed = _Crater.w;
                return _Crater.x * _Crater.z * (1.0 + 0.18 * dir.y) * (1.0 + 0.26 * (Noise2(dir * 1.7 + seed) - 0.5) + 0.14 * (Noise2(dir * 5.0 + seed + 7.0) - 0.5));
            }

            // How deep the stone is broken in from the face at q (along the edge, up), 0 where it is whole: a bowl, deepest
            // round the blade, broken into flat chunks (each a step deeper or shallower than the next, so the break shows
            // planes and sharp edges like split stone, not a smooth scoop), and shallow flakes chipped off round its rim.
            float Depth(float2 q)
            {
                float keep = 1.0 - _Heal;
                if (keep <= 0) return 0;
                float R = RimRadius(q);
                float r = length(q);
                if (R < 0.01 || r >= R * 1.32) return 0;
                if (r >= R)
                {
                    // The rim's flakes: ragged patches just outside it chipped off, a few millimetres deep.
                    float n = Noise2(q * 13.0 + _Crater.w + 5.0) * 0.7 + Noise2(q * 31.0 + _Crater.w) * 0.3;
                    float out01 = (r - R) / (R * 0.32);
                    float cut = 0.5 + 0.35 * out01;
                    if (n < cut) return 0;
                    return (0.006 + 0.012 * saturate((n - cut) * 6.0)) * (1.0 - out01) * keep;
                }
                float2 cell = Cells(q * 11.0 + _Crater.w);
                float k = r / R;
                float d = _Crater.y * smoothstep(1.0, 0.5, k) * (0.6 + 0.4 * (1.0 - k * k));
                d += 0.014 * (cell.x - 0.5) * smoothstep(1.0, 0.7, k);
                d *= 1.0 - 0.08 * Noise2(q * 40.0 + _Crater.w * 2.0);
                return max(d, 0.004) * keep;
            }

            // Stone at p: inside the platform's box and not broken out of it.
            bool Solid(float3 p)
            {
                float3 f = Frame(p);
                if (f.z < 0 || p.y > _Box.x || p.y < _Box.y) return false;
                return f.z >= Depth(f.xy);
            }

            // Out in the air beyond the face or over the top (the ray has come out of the stone's broken side).
            bool Out(float3 p)
            {
                return Frame(p).z < -0.004 || p.y > _Box.x + 0.004;
            }

            // The face's cracks run on over the lip: a point on the top unfolds to (along, height of the lip + how far in).
            float2 Unfold(float3 p)
            {
                float3 f = Frame(p);
                return _Surface > 0.5 ? float2(f.x, (_Box.x - _Origin.y) + max(f.z, 0)) : f.xy;
            }

            // A crack's sideways offset (m) at o metres out along it: straight runs with sharp kinks every few centimetres.
            float Kinked(float o, float seed)
            {
                const float seg = 0.055;
                float j = floor(o / seg), f = frac(o / seg);
                float a = (Hash1(j * 3.7 + seed) - 0.5) * 0.045, b = (Hash1((j + 1.0) * 3.7 + seed) - 0.5) * 0.045;
                return lerp(a, b, f);
            }

            // Cracks running out from the crater's rim (0 .. 1): thin, straight between sharp kinks, tapering, some forked.
            float Cracks(float2 q, float R0)
            {
                float r = length(q), n = _Cracks.x, len = _Cracks.y;
                if (n < 1 || R0 < 0.02 || r > R0 + len || r < R0 * 0.9) return 0;
                float ang = atan2(q.y, q.x);
                float best = 0;
                [loop] for (int k = 0; k < 12; k++)
                {
                    if (k >= n) break;
                    float h1 = Hash1(k * 7.31 + _Crater.w), h2 = Hash1(k * 3.17 + _Crater.w + 1.7), h3 = Hash1(k * 5.77 + _Crater.w + 3.1);
                    float a0 = 6.2831853 * (k + 0.15 + 0.7 * h1) / n;
                    float l = len * (0.35 + 0.65 * h2);
                    float o = r - R0 * 0.9;
                    if (o > l) continue;
                    // Distance across it: the angle away from its line, times the radius, less its kinks.
                    float da = ang - a0;
                    da = da - 6.2831853 * floor(da / 6.2831853 + 0.5);
                    float side = abs(da * r - Kinked(o, k * 13.1 + _Crater.w));
                    float w = _Cracks.z * (1.0 - 0.8 * o / l) + 0.0008;
                    best = max(best, 1.0 - smoothstep(w * 0.35, w, side));
                    // A fork from a kink a third of the way out, splitting off at an angle.
                    float bo = o - l * (0.25 + 0.2 * h3);
                    if (bo > 0 && bo < l * 0.45)
                    {
                        float turn = (h3 > 0.5 ? 1.0 : -1.0) * (0.55 + 0.35 * h2);
                        float bside = abs(da * r - Kinked(o, k * 13.1 + _Crater.w) - turn * bo - Kinked(bo, k * 29.3 + 4.0) * 0.6);
                        float bw = _Cracks.z * 0.65 * (1.0 - bo / (l * 0.45)) + 0.0006;
                        best = max(best, 0.85 * (1.0 - smoothstep(bw * 0.35, bw, bside)));
                    }
                }
                return best;
            }

            // The wall's colour round here, softened (a few texels, so its grid lines do not show inside).
            float3 GrabSoft(float2 uv)
            {
                float2 px = _SkillBreachGrab_TexelSize.xy * 5.0;
                return (tex2D(_CameraOpaqueTexture, uv).rgb * 2.0
                    + tex2D(_CameraOpaqueTexture, uv + float2(px.x, px.y)).rgb + tex2D(_CameraOpaqueTexture, uv + float2(-px.x, px.y)).rgb
                    + tex2D(_CameraOpaqueTexture, uv + float2(px.x, -px.y)).rgb + tex2D(_CameraOpaqueTexture, uv + float2(-px.x, -px.y)).rgb) / 6.0;
            }

            // The ray against a box (centre c, axes, half sizes h): its entry, or -1.
            float BoxHit(float3 ro, float3 rd, float3 c, float3 h, out float3 nOut)
            {
                float3 ax = _BladeX.xyz, ay = _BladeY.xyz, az = _BladeZ.xyz;
                float3 d = ro - c;
                float3 o = float3(dot(d, ax), dot(d, ay), dot(d, az));
                float3 v = float3(dot(rd, ax), dot(rd, ay), dot(rd, az));
                float3 inv = 1.0 / (abs(v) > 1e-5 ? v : (v >= 0 ? 1e-5 : -1e-5));
                float3 t0 = (-h - o) * inv, t1 = (h - o) * inv;
                float3 tmin = min(t0, t1), tmax = max(t0, t1);
                float tn = max(max(tmin.x, tmin.y), tmin.z), tf = min(min(tmax.x, tmax.y), tmax.z);
                nOut = float3(0, 0, 0);
                if (tn > tf || tf < 0) return -1;
                float3 nl = tmin.x >= tmin.y && tmin.x >= tmin.z ? float3(-sign(v.x), 0, 0)
                    : tmin.y >= tmin.z ? float3(0, -sign(v.y), 0) : float3(0, 0, -sign(v.z));
                nOut = ax * nl.x + ay * nl.y + az * nl.z;
                return max(tn, 0);
            }

            float4 frag(v2f i) : SV_Target
            {
                gN = normalize(float3(_Normal.x, 0, _Normal.z));
                gU = normalize(cross(float3(0, 1, 0), gN));
                bool top = _Surface > 0.5;
                // The pixel on the stone's surface it lies on.
                float3 P0 = i.world;
                if (top) P0.y = _Box.x;
                else P0 -= gN * dot(P0 - _Origin.xyz, gN);
                float3 f0 = Frame(P0);
                if (top ? f0.z < 0 : (P0.y > _Box.x || P0.y < _Box.y)) discard;

                float2 uv = i.grab.xy / i.grab.w;
                float3 under = tex2D(_CameraOpaqueTexture, uv).rgb;
                float keep = 1.0 - _Heal;
                float d0 = Depth(f0.xy);
                bool broken = d0 > f0.z + 0.002;
                if (_Debug > 0.5) return broken ? float4(1, 0, 0, 1) : float4(0, 0, 1, 1);

                float2 c0 = Unfold(P0);
                float R0 = RimRadius(c0);
                if (!broken)
                {
                    // Unbroken stone: the cracks, the grit thrown out round the crater, a dark line where it breaks off.
                    float crack = Cracks(c0, R0) * keep;
                    float r = length(c0);
                    float grit = smoothstep(R0 * 1.8, R0 * 1.02, r) * (0.5 + 0.5 * Noise2(c0 * 7.0 + 3.0)) * keep;
                    const float e = 0.012;
                    float near = 0;
                    if (top)
                    {
                        near = max(max(Depth(f0.xy + float2(e, 0)) - f0.z, Depth(f0.xy - float2(e, 0)) - f0.z), Depth(f0.xy) - (f0.z - e));
                    }
                    else
                    {
                        near = max(max(Depth(f0.xy + float2(e, 0)), Depth(f0.xy - float2(e, 0))), max(Depth(f0.xy + float2(0, e)), Depth(f0.xy - float2(0, e))));
                    }
                    float edge = near > 0.002 ? lerp(0.35, 1.0, saturate(near / 0.03)) : 0.0;
                    if (crack <= 0.01 && grit <= 0.02 && edge <= 0.0) discard;
                    float3 col = under * (1.0 - 0.18 * grit);
                    col = lerp(col, under * 0.28, crack * _Cracks.w);
                    col = lerp(col, under * 0.3, edge * 0.8);
                    // A fresh crack glows a moment.
                    col += _Glow.rgb * _Glow.a * crack * 0.6;
                    return float4(col, 1);
                }

                // Into the break: where the eye's ray meets stone (steps, then halving), or comes out of the open side.
                float3 V = normalize(i.world - _WorldSpaceCameraPos);
                const int STEPS = 64;
                const float tMax = 0.9;
                float tA = 0, tB = tMax;
                bool hit = false, gone = false;
                [loop] for (int s = 1; s <= STEPS; s++)
                {
                    float t = tMax * s / STEPS;
                    float3 P = P0 + V * t;
                    if (Solid(P)) { tB = t; hit = true; break; }
                    if (Out(P)) { tB = t; gone = true; break; }
                    tA = t;
                }
                if (hit)
                {
                    [unroll] for (int h = 0; h < 6; h++)
                    {
                        float tm = 0.5 * (tA + tB);
                        if (Solid(P0 + V * tm)) tB = tm; else tA = tm;
                    }
                }

                // The sword stuck in it, in front of the stone it goes into.
                if (_BladeOn > 0.5)
                {
                    float3 nb, ng;
                    float3 bp = _BladePos.xyz;
                    float tb = BoxHit(P0, V, bp + _BladeZ.xyz * 0.5, float3(0.06, 0.0275, 0.38), nb);
                    float tg = BoxHit(P0, V, bp + _BladeZ.xyz * 0.09, float3(0.125, 0.0375, 0.035), ng);
                    float tS = tb;
                    float3 nS = nb;
                    float4 colS = _BladeColor;
                    if (tg >= 0 && (tS < 0 || tg < tS)) { tS = tg; nS = ng; colS = _GuardColor; }
                    if (tS >= 0 && (!hit || tS < tB) && (!gone || tS < tB))
                    {
                        float3 Ps = P0 + V * tS;
                        float3 L = normalize(_MainLightPosition.xyz + float3(0, 1e-4, 0));
                        float lit = saturate(dot(nS, L)) * 0.6 + 0.4;
                        float spec = pow(saturate(dot(reflect(V, nS), L)), 24.0) * 0.6;
                        float deep = saturate(Frame(Ps).z / max(_Crater.y, 0.05));
                        float3 c = colS.rgb * lit * lerp(1.0, 0.45, deep) + spec * (1.0 - deep);
                        return float4(c, 1);
                    }
                }

                if (!hit)
                {
                    // Out through the bitten lip's open side (seen from above): the shadowed broken stone deep in the bite (what is
                    // past the edge there is not on the screen; sampling the screen elsewhere showed sky through the floor).
                    return float4(GrabSoft(uv) * _Tint.rgb * 0.42, 1);
                }

                float3 Ph = P0 + V * tB;
                float3 fh = Frame(Ph);
                float depth = max(0, fh.z);
                // The broken face's slope there, turned to the world (out of the stone: the face's way plus the bowl's).
                const float ee = 0.008;
                float gx = (Depth(fh.xy + float2(ee, 0)) - Depth(fh.xy - float2(ee, 0))) / (2.0 * ee);
                float gy = (Depth(fh.xy + float2(0, ee)) - Depth(fh.xy - float2(0, ee))) / (2.0 * ee);
                float3 Nw = normalize(gN + gU * gx + float3(0, 1, 0) * gy);

                // Lit as each broken face turns to the sun, against the surface it broke out of (the grabbed colour is
                // already lit), never brighter than it; shadowed where the stone is in the way.
                float3 L = normalize(_MainLightPosition.xyz + float3(0, 1e-4, 0));
                float3 Nsurf = top ? float3(0, 1, 0) : gN;
                float faceLit = saturate(dot(Nsurf, L) * 0.75 + 0.25);
                float lit = clamp(saturate(dot(Nw, L) * 0.75 + 0.25) / max(faceLit, 0.05), 0.62, 1.15);
                float sunShade = 1.0;
                [loop] for (int k = 1; k <= 10; k++)
                {
                    float3 Sp = Ph + L * (k * 0.03) + Nw * 0.004;
                    if (Out(Sp)) break;
                    if (Solid(Sp)) { sunShade = 0.78; break; }
                }
                // How deep in it is: in from the face, and (seen from above) down from the top too.
                float deep = saturate(depth / max(_Crater.y, 0.05));
                if (top) deep = max(deep, saturate((_Box.x - Ph.y) / 0.2));
                float ao = lerp(1.0, 0.66, pow(deep, 0.8));
                float3 mat = GrabSoft(uv) * _Tint.rgb;
                float grain = 0.74 + 0.5 * Noise2(fh.xy * 26.0 + 5.1) * (0.55 + 0.45 * Noise2(fh.xy * 9.0 + 1.3));
                float3 col = mat * grain * lit * sunShade * ao;
                // The chipped edge of the break: a thin pale band right inside the surface.
                float lip = 1.0 - smoothstep(0.003, 0.018, top ? (_Box.x - Ph.y) : depth);
                col = lerp(col, under * 0.95, lip * 0.7);
                // A fresh break glows a moment, deep in round the blade.
                col += _Glow.rgb * _Glow.a * deep * deep;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
