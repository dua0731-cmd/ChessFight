// A cartoon swoosh of wind (R86, the knight, after the reference's hand-drawn wind burst): a ribbon that
// PawnRushSkillFx.Toon.cs bends into arcs and swirls, u along it (tail 0 .. head 1), v across it. Only the part
// between _Tail and _Head shows, as a crescent pointed at both ends, in three hard bands across (lit on its outer
// side) with an ink edge, eaten away by noise as it goes. Premultiplied: _Opacity 1 covers like paint.
// _Symmetric 1 lays the bands out from the middle instead (hot middle, ink on both edges): the rook's cartoon
// lightning and speed lines on line renderers, which show on a white floor where light alone did not.
Shader "ChessFight/Skill Swoosh"
{
    Properties
    {
        _Lit ("Lit", Color) = (1, 1, 1, 1)
        _Mid ("Middle", Color) = (0.5, 0.75, 1, 1)
        _Shade ("Shadow", Color) = (0.15, 0.35, 0.9, 1)
        _InkColor ("Ink", Color) = (0.03, 0.06, 0.22, 1)
        _Ink ("Ink width (share of the half width)", Range(0, 0.6)) = 0.18
        _Head ("Head along it", Range(0, 1)) = 1
        _Tail ("Tail along it", Range(0, 1)) = 0
        _Dissolve ("Eaten away", Range(0, 1)) = 0
        _Opacity ("Covers (0 = light only)", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Seed ("Seed", Float) = 0
        _Symmetric ("Bands from the middle (lines, bolts)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SkillNoise.cginc"

            float4 _Lit, _Mid, _Shade, _InkColor;
            float _Ink, _Head, _Tail, _Dissolve, _Opacity, _Fade, _Seed, _Symmetric;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float span = max(_Head - _Tail, 1e-3);
                float s = (i.uv.x - _Tail) / span;                     // 0..1 over the part that shows
                // A crescent: pointed at both ends, fullest a little towards the head.
                float thick = pow(saturate(sin(saturate(pow(saturate(s), 0.8)) * 3.14159)), 0.6) * step(0, s) * step(s, 1);
                float across = (i.uv.y - 0.5) * 2;                    // -1 inner edge .. 1 outer edge
                float inside = thick - abs(across);
                float aa = max(fwidth(inside), 1e-4);
                float cover = smoothstep(-aa, aa, inside);

                float wobble = SkillNoise(float3(i.uv.x * 7, i.uv.y * 2, _Seed)) * 0.15;
                float side = across / max(thick, 1e-3) + wobble;       // -1..1 across the crescent's own width
                // Symmetric: 1 in the middle .. -1 at both edges.
                side = lerp(side, 1 - 2 * abs(across) / max(thick, 1e-3), _Symmetric);
                float baa = max(fwidth(side), 1e-3);
                float3 col = lerp(_Shade.rgb, _Mid.rgb, smoothstep(-0.4 - baa, -0.4 + baa, side));
                float litEdge = lerp(0.35, 0.5, _Symmetric);   // a narrower white-hot middle on lines
                col = lerp(col, _Lit.rgb, smoothstep(litEdge - baa, litEdge + baa, side));
                float ink = 1 - smoothstep(_Ink * thick - aa, _Ink * thick + aa, inside);
                col = lerp(col, _InkColor.rgb, ink * step(1e-4, _Ink));

                float bite = SkillNoise(float3(i.uv.x * 9, i.uv.y * 3, _Seed + 13.7)) * 0.5 + 0.5;
                cover *= smoothstep(0, 0.04, bite - _Dissolve * 1.02);
                cover *= _Fade;
                return float4(col * cover, cover * _Opacity);
            }
            ENDCG
        }
    }
}
