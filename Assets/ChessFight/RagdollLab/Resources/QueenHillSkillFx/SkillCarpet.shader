// The Queen of the Hill bishop's flying carpet (R130, 승규 님: "비숍 양탄자를 좀 더 멋있게 해줘"): a velvet carpet drawn in
// code on its own rippling mesh. A violet velvet field with a faint damask lattice, a chessboard medallion in ivory and
// ebony in its middle framed in gold, a gold border with a diamond chain running round it, gold fringe at its two short
// ends (cut out of the mesh's end strips). Lit like the other props (hard bands from _FxSun) with a velvet sheen toward its
// grazing edges and a glint on the gold. Seen from under it is darker. _Flash turns it white, _Color.a fades it by a
// fine dither (it stays opaque and depth-sorted). Built-in render pipeline.
Shader "ChessFight/Skill Carpet"
{
    Properties
    {
        _Field ("Velvet", Color) = (0.25, 0.14, 0.38, 1)
        _FieldLit ("Velvet sheen", Color) = (0.62, 0.45, 0.85, 1)
        _Gold ("Gold", Color) = (0.89, 0.69, 0.27, 1)
        _GoldDeep ("Gold shade", Color) = (0.55, 0.36, 0.08, 1)
        _Ivory ("Ivory", Color) = (0.95, 0.91, 0.83, 1)
        _Ebony ("Ebony", Color) = (0.12, 0.1, 0.15, 1)
        _InkColor ("Ink", Color) = (0.14, 0.14, 0.18, 1)
        _Size ("Carpet size (m): x along, y across", Vector) = (1.3, 0.95, 0, 0)
        _Fringe ("Fringe share of the length at each end", Float) = 0.06
        _Flash ("White", Range(0, 1)) = 0
        _Color ("Tint (a = fade)", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+12" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Field, _FieldLit, _Gold, _GoldDeep, _Ivory, _Ebony, _InkColor, _Size, _Color, _FxSun;
            float _Fringe, _Flash;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 world : TEXCOORD2;
                float4 screen : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float Bayer4(float2 px)
            {
                int2 c = int2(fmod(px, 4));
                const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (m[c.y * 4 + c.x] + 0.5) / 16.0;
            }

            float4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                if (_Color.a < 0.999)
                {
                    float2 px = i.screen.xy / max(i.screen.w, 1e-5) * _ScreenParams.xy;
                    clip(_Color.a - Bayer4(px));
                }
                float2 uv = i.uv;
                float f = _Fringe;
                float3 N = normalize(i.normal) * (facing > 0 ? 1 : -1);
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                float3 L = dot(_FxSun.xyz, _FxSun.xyz) > 1e-6 ? normalize(_FxSun.xyz) : normalize(float3(0.35, 0.79, -0.5));
                float ndl = dot(N, L) * 0.5 + 0.5;
                float light = lerp(0.62, 1.0, smoothstep(0.34, 0.4, ndl)) + 0.1 * smoothstep(0.78, 0.84, ndl);
                float under = facing > 0 ? 1 : 0.62;

                // The fringe: thin gold threads off both short ends.
                if (uv.x < f || uv.x > 1 - f)
                {
                    float out01 = uv.x < f ? (f - uv.x) / f : (uv.x - (1 - f)) / f;
                    float thread = frac(uv.y * _Size.y / 0.028);
                    clip(0.42 - abs(thread - 0.5) - out01 * 0.12);
                    float3 c = lerp(_Gold.rgb, _GoldDeep.rgb, out01 * 0.6) * light * under;
                    c = lerp(c, float3(1, 1, 1), _Flash);
                    return float4(c * _Color.rgb, 1);
                }

                // Metres in the carpet: x along (from its middle), y across.
                float2 m = float2((uv.x - 0.5) / (1 - 2 * f) * _Size.x, (uv.y - 0.5) * _Size.y);
                float2 h = float2(_Size.x, _Size.y) * 0.5;
                float e = min(h.x - abs(m.x), h.y - abs(m.y));   // metres in from the edge
                float aa = max(fwidth(e), 1e-4);

                // The field: velvet with a faint damask lattice (small diamonds), lighter at grazing angles.
                float2 lat = m / 0.11;
                float dia = abs(frac(lat.x + lat.y * 0.5) - 0.5) + abs(frac(lat.y - lat.x * 0.5) - 0.5);
                float damask = smoothstep(0.42, 0.38, dia) * 0.18;
                float sheen = pow(saturate(1 - abs(dot(N, V))), 2.2);
                float3 col = lerp(_Field.rgb, _FieldLit.rgb, damask + sheen * 0.75) * light;

                // The medallion: a diamond in the middle, a chessboard of ivory and ebony inside a gold line.
                float2 q = m / float2(h.x * 0.62, h.y * 0.66);
                float md = abs(q.x) + abs(q.y);
                if (md < 1.0)
                {
                    float2 board = (m + 1.0) / 0.12;
                    float check = fmod(floor(board.x) + floor(board.y), 2.0);
                    float3 sq = lerp(_Ivory.rgb, _Ebony.rgb, check);
                    col = sq * light;
                }
                float rimMd = smoothstep(0.035, 0.0, abs(md - 1.0) * h.y * 0.66);
                col = lerp(col, lerp(_GoldDeep.rgb, _Gold.rgb, light) * 1.05, rimMd);

                // The border: a gold band with an ink line on each side and a chain of diamonds along it.
                float bandW = 0.1;
                float band = smoothstep(bandW + aa, bandW - aa, e);
                float along = abs(m.x) > h.x - bandW ? m.y : m.x;
                float chain = abs(frac(along / 0.09) - 0.5) * 0.09 + abs(e - bandW * 0.5);
                float diamond = smoothstep(0.03, 0.022, chain);
                float3 gold = lerp(_GoldDeep.rgb, _Gold.rgb, light);
                // A glint on the gold where the sun bounces to the eye.
                float3 H = normalize(L + V);
                gold += pow(saturate(dot(N, H)), 40) * 0.6;
                float3 bandCol = lerp(gold, lerp(_Ebony.rgb, _Field.rgb, 0.4) * light, diamond * 0.85);
                col = lerp(col, bandCol, band);
                float lines = smoothstep(0.012, 0.0, abs(e - bandW)) + smoothstep(0.012, 0.0, e - 0.004);
                col = lerp(col, _InkColor.rgb, saturate(lines) * 0.9);

                col *= under;
                col = lerp(col, float3(1, 1, 1), _Flash);
                return float4(col * _Color.rgb, 1);
            }
            ENDCG
        }
    }
}
