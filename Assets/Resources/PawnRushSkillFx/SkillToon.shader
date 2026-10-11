// Cartoon puffs for the skill effects (R86, after 승규 님's reference shorts: a stylized explosion of dark smoke with
// glowing cracks and hot edges, and a hand-drawn burst of wind in hard colour bands with ink edges). A sphere is
// lumped by noise in the vertex shader, shaded from the sun in three hard bands, burns in the creases between its
// lumps up to _Heat (all of it at 1, only the deepest creases as it cools, like fire seen through cracks in the
// smoke), lights its edges (_Rim), inks the edges that turn away (_Ink) and is eaten away from its edges inward
// (_Dissolve) with an inked or glowing bitten edge. Opaque with alpha test: it goes by being eaten, as in the
// references. All motion comes from the effects' clock in C# (_Seed.w = age), so it runs on through a hit stop and
// one recorded frame at a time. Built-in render pipeline. Noise: SkillNoise.cginc.
Shader "ChessFight/Skill Toon"
{
    Properties
    {
        _Lit ("Lit", Color) = (1, 1, 1, 1)
        _Mid ("Middle", Color) = (0.6, 0.6, 0.6, 1)
        _Shade ("Shadow", Color) = (0.25, 0.25, 0.25, 1)
        _Bands ("Band edges: shadow|middle, middle|lit; wobble", Vector) = (0.42, 0.72, 0.12, 0)
        [HDR] _Fire ("Fire", Color) = (3, 1.5, 0.4, 1)
        [HDR] _FireCore ("Fire core", Color) = (5, 4, 2.5, 1)
        _Heat ("Heat (0 cold .. 1 all fire)", Range(0, 1.2)) = 0
        [HDR] _Rim ("Edge light", Color) = (0, 0, 0, 1)
        _RimPower ("Edge light power", Range(0.5, 8)) = 3
        _Ink ("Ink edge (0 = none)", Range(0, 1)) = 0
        _InkColor ("Ink", Color) = (0.03, 0.05, 0.15, 1)
        _Dissolve ("Eaten away", Range(0, 1)) = 0
        [HDR] _Bite ("Bitten edge", Color) = (0.03, 0.05, 0.15, 1)
        _Lump ("Lumps (share of the radius)", Range(0, 1)) = 0.3
        _Scale ("Lumps across", Float) = 2.2
        _Seed ("Seed (xyz), age (w)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Cull Back
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SkillNoise.cginc"

            float4 _Lit, _Mid, _Shade, _Bands, _Fire, _FireCore, _Rim, _InkColor, _Bite, _Seed;
            float _Heat, _RimPower, _Ink, _Dissolve, _Lump, _Scale;
            // Towards the sun, set by the effects (PawnRushSkillFx.Toon.cs).
            float4 _FxSun;

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
                float3 q : TEXCOORD2;
                float lump : TEXCOORD3;   // -1 deep in a crease .. 1 on top of a lump
            };

            // Two octaves: the big lumps and a few smaller ones on them.
            float Lumps(float3 q) { return SkillNoise(q) * 0.72 + SkillNoise(q * 2.13 + 17.1) * 0.28; }

            v2f vert (appdata v)
            {
                v2f o;
                // Noise space: the lumps stay on the puff as it grows, and boil slowly upward with age.
                float3 q = v.vertex.xyz * _Scale + _Seed.xyz + float3(0, -_Seed.w * 0.55, 0);
                float n = Lumps(q);
                float h = _Lump * 0.5;   // the sphere mesh is 1 across
                // The surface moves along its normal by n * h; its normal bends against the slope of n.
                const float e = 0.08;
                float3 slope = (float3(Lumps(q + float3(e, 0, 0)), Lumps(q + float3(0, e, 0)), Lumps(q + float3(0, 0, e))) - n) * (_Scale * h / e);
                float3 nrm = normalize(v.normal);
                float3 bent = normalize(nrm - (slope - dot(slope, nrm) * nrm));
                float3 p = v.vertex.xyz + nrm * n * h;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.normal = UnityObjectToWorldNormal(bent);
                o.world = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                o.q = q;
                o.lump = n;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                float3 L = dot(_FxSun.xyz, _FxSun.xyz) > 1e-6 ? normalize(_FxSun.xyz) : normalize(float3(0.35, 0.79, -0.5));

                float3 dq = i.q * 1.4 + 5.7;
                float detail = SkillNoise(dq) * 0.65 + SkillNoise(dq * 2.17 + 3.1) * 0.35;   // about -1..1

                // Three hard bands from the sun, their edges wobbling a little as if drawn by hand.
                float ndl = dot(N, L);
                float shade = ndl * 0.5 + 0.5 + detail * _Bands.z;
                float aa = max(fwidth(shade), 1e-4);
                float3 col = lerp(_Shade.rgb, _Mid.rgb, smoothstep(_Bands.x - aa, _Bands.x + aa, shade));
                col = lerp(col, _Lit.rgb, smoothstep(_Bands.y - aa, _Bands.y + aa, shade));

                // Fire: the creases between the lumps burn first and longest (a little broken up by the detail), so a
                // cooling ball turns to dark smoke with fire glowing through its cracks.
                float crease = saturate(i.lump * 0.6 + 0.5) + detail * 0.08;
                float caa = max(fwidth(crease), 1e-3);
                float fire = (1 - smoothstep(_Heat - caa - 0.03, _Heat + caa, crease)) * saturate(_Heat * 25);
                float core = saturate((_Heat - crease) * 3);
                float3 hot = lerp(_Fire.rgb, _FireCore.rgb, core);
                col = lerp(col, hot, fire);

                // Edges: light (hot rims) and ink (where it turns away).
                float facing = saturate(dot(N, V));
                float edge = 1 - facing;
                col += _Rim.rgb * pow(edge, _RimPower);
                float eaa = max(fwidth(edge), 1e-3);
                float ink = smoothstep(1 - _Ink - eaa, 1 - _Ink + eaa, edge) * step(1e-4, _Ink);
                col = lerp(col, _InkColor.rgb, ink);

                // Eaten away from its edges inward (it shrinks and frays rather than turning to crumbs), with a
                // bitten edge.
                float bite = SkillNoise(i.q * 0.9 + 41.0) * 0.5 + 0.5;
                bite = bite * 0.55 + facing * 0.45;
                float left = bite - _Dissolve * 1.02;
                clip(left);
                float bitten = (1 - smoothstep(0, 0.05, left)) * step(1e-4, _Dissolve);
                col = lerp(col, _Bite.rgb, bitten);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
