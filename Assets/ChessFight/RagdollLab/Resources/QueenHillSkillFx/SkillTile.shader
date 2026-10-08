// Flat inked shapes for the Queen of the Hill skill effects (R89, design A "잉크 테두리 장난감 체스"): the 1.5 m board
// squares that warn (filled in the piece's colour or the viewer's side colour, a side-colour rim, an ink edge, sliding
// diagonal stripes on an enemy's), the captured square under a fallen piece, a trapdoor socket, rings (an impact ring,
// a guard ring, the king's wave, a clock that fills), a horseshoe (a ring with a gap) and long bars (the bishop's X).
// Drawn on a quad in its local XY plane, 1 x 1 in the mesh: the object's scale is its size, given in metres as _Size so
// the ink, the rim and the stripes keep their width at any size. Premultiplied: colours are paint, not light; only
// _Flash turns it white for a frame or two. Built-in render pipeline.
Shader "ChessFight/Skill Tile"
{
    Properties
    {
        _Fill ("Fill (a = cover)", Color) = (1, 1, 1, 0.35)
        _Core ("Core band (a = cover)", Color) = (1, 1, 1, 0)
        _CoreWidth ("Core width (m): rings, next to the inner edge; boxes, inside the rim", Float) = 0
        _InkColor ("Ink", Color) = (0.1, 0.12, 0.25, 1)
        _Ink ("Ink width (m)", Float) = 0.05
        _Rim ("Side rim (a = cover)", Color) = (0, 0, 0, 0)
        _RimWidth ("Side rim width (m)", Float) = 0.08
        _Stripe ("Stripes (a = cover)", Color) = (0, 0, 0, 0)
        _StripePhase ("Stripe phase (m)", Float) = 0
        _StripeWidth ("Stripe width (m)", Float) = 0.12
        _Size ("Size (m): x across, y along", Vector) = (1.5, 1.5, 0, 0)
        _Round ("Corner radius (m)", Float) = 0.06
        _Shape ("Shape: 0 rounded box, 1 ring, 2 disc", Float) = 0
        _Inner ("Ring inner radius (share of the outer)", Range(0, 1)) = 0.8
        _Gap ("Ring gap (degrees, centred on local -x)", Range(0, 360)) = 0
        _Arc ("Shown part, clockwise from local +y (rings: all of it; boxes: the rim)", Range(0, 1)) = 1
        _Flash ("White", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -4

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Fill, _Core, _InkColor, _Rim, _Stripe, _Size;
            float _CoreWidth, _Ink, _RimWidth, _StripePhase, _StripeWidth, _Round, _Shape, _Inner, _Gap, _Arc, _Flash, _Fade;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = (v.uv - 0.5) * _Size.xy;   // metres from the middle
                return o;
            }

            float RoundBox(float2 p, float2 h, float r)
            {
                float2 q = abs(p) - h + r;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            float Band(float e, float from, float width, float aa)
            {
                // 1 where from <= e < from + width (e = metres in from an edge), antialiased.
                return smoothstep(from - aa, from + aa, e) * (1 - smoothstep(from + width - aa, from + width + aa, e));
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.p;
                float2 h = _Size.xy * 0.5;
                bool ring = _Shape > 0.5 && _Shape < 1.5;
                float d, inner = 1e4;
                if (_Shape < 0.5) d = RoundBox(p, h, min(_Round, min(h.x, h.y)));
                else
                {
                    float R = min(h.x, h.y), l = length(p);
                    d = l - R;
                    if (ring)
                    {
                        inner = l - R * _Inner;   // metres out from the inner edge
                        d = max(d, -inner);
                    }
                }
                float aa = max(fwidth(d), 1e-4);
                float cover = 1 - smoothstep(-aa, aa, d);

                // Clockwise from +y: the gap of a horseshoe (round -x) and the part shown (a clock filling).
                float a01 = frac(atan2(p.x, p.y) / 6.2831853 + 1);
                if (ring)
                {
                    float gapHalf = _Gap / 720.0;
                    if (gapHalf > 0) cover *= smoothstep(gapHalf - 0.004, gapHalf + 0.004, abs(a01 - 0.75));
                    cover *= 1 - smoothstep(_Arc - 0.002, _Arc + 0.002, a01) * step(_Arc, 0.999);
                }
                if (cover <= 0.001) discard;

                float e = -d;   // metres in from the outer edge
                float3 col = _Fill.rgb;
                float a = _Fill.a;
                if (_Stripe.a > 0)
                {
                    float s = frac((p.x + p.y) * 0.70710678 / (2 * _StripeWidth) + _StripePhase / (2 * _StripeWidth));
                    float saa = max(fwidth(s), 1e-3);
                    float on = smoothstep(0.5 - saa, 0.5 + saa, s) * (1 - smoothstep(1 - saa, 1, s));
                    col = lerp(col, _Stripe.rgb, on * _Stripe.a);
                    a = max(a, on * _Stripe.a);
                }
                if (ring && _Core.a > 0)
                {
                    float c = Band(inner, _Ink, _CoreWidth, aa);
                    col = lerp(col, _Core.rgb, c * _Core.a);
                    a = lerp(a, 1, c * _Core.a);
                }
                if (!ring && _Core.a > 0)
                {
                    float c = Band(e, _Ink + _RimWidth * step(0.001, _Rim.a), _CoreWidth, aa);
                    col = lerp(col, _Core.rgb, c * _Core.a);
                    a = lerp(a, 1, c * _Core.a);
                }
                if (_Rim.a > 0)
                {
                    float r = Band(e, _Ink, _RimWidth, aa);
                    if (!ring) r *= 1 - smoothstep(_Arc - 0.002, _Arc + 0.002, a01) * step(_Arc, 0.999);
                    col = lerp(col, _Rim.rgb, r * _Rim.a);
                    a = lerp(a, 1, r * _Rim.a);
                }
                float ink = 1 - smoothstep(_Ink - aa, _Ink + aa, e);
                if (ring) ink = max(ink, 1 - smoothstep(_Ink - aa, _Ink + aa, inner));
                col = lerp(col, _InkColor.rgb, ink * _InkColor.a);
                a = lerp(a, 1, ink * _InkColor.a);

                col = lerp(col, float3(1, 1, 1), _Flash);
                a = lerp(a, 1, _Flash);
                a *= cover * _Fade;
                return float4(col * a, a);
            }
            ENDCG
        }
    }
}
