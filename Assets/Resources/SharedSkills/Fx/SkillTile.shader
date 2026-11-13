// Flat inked shapes for the Queen of the Hill skill effects (R89, design A "잉크 테두리 장난감 체스"): the 1.5 m board
// squares that warn (filled in the piece's colour or the viewer's side colour, a side-colour rim, an ink edge, sliding
// diagonal stripes on an enemy's), the captured square under a fallen piece, a trapdoor socket, rings (an impact ring,
// a guard ring, the king's wave, a clock that fills), a horseshoe (a ring with a gap) and long bars (the bishop's X).
// Drawn on a quad in its local XY plane, 1 x 1 in the mesh: the object's scale is its size, given in metres as _Size so
// the ink, the rim and the stripes keep their width at any size. Premultiplied: colours are paint, not light; only
// _Flash turns it white for a frame or two. Built-in render pipeline.
// R130: the charging dots and the neon line inside the edge of Skill Tile Board (the Queen of the Hill queen's squares
// draped over the floor are drawn with this shader).
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
        _Shape ("Shape: 0 rounded box, 1 ring, 2 disc, 3 hoof print (R133), 4 horseshoe badge (R138)", Float) = 0
        _Inner ("Ring inner radius (share of the outer)", Range(0, 1)) = 0.8
        _Gap ("Ring gap (degrees, centred on local -x)", Range(0, 360)) = 0
        _Arc ("Shown part, clockwise from local +y (rings: all of it; boxes: the rim)", Range(0, 1)) = 1
        _Flash ("White", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        _Charge ("Charging dots: how far they have swept, near edge to far (below 0: none)", Float) = -1
        _Dot ("Charging dots (a = cover)", Color) = (1, 1, 1, 0)
        _DotStep ("Dot spacing (m)", Float) = 0.1
        [HDR] _Glow ("Neon line inside the edge (a = strength)", Color) = (0, 0, 0, 0)
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
            float4 _Dot, _Glow;
            float _Charge, _DotStep;

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

            float Seg(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
                return length(pa - ba * h);
            }

            // R133 (승규 님 10-14: "나이트 말 발굽을 조금 더 사실적으로"): a shod hoof's print (_Shape 3), its toe toward +y: the
            // shoe's U, wider at the toe, its branches closing in at the heels, four nail holes a side, and the frog's wedge
            // between the heels. Fitted to the tile's shorter half-size.
            float HoofPrint(float2 p, float2 h)
            {
                float s = min(h.x, h.y);
                float2 m = float2(abs(p.x), p.y) / s;   // one side: it is the same both ways
                const float2 C = float2(0, 0.05);
                const float R = 0.6;
                const float2 heel = float2(0.5, -0.86);
                float shoe = m.y > C.y ? abs(length(m - C) - R) : Seg(m, float2(R, C.y), heel);
                shoe -= lerp(0.1, 0.15, saturate((m.y + 0.86) / 1.5));
                float holes = length(m - lerp(float2(R, C.y), heel, 0.3));
                for (int k = 0; k < 3; k++)
                {
                    float ang = radians(35.0 + 27.5 * k);
                    holes = min(holes, length(m - (C + R * float2(sin(ang), cos(ang)))));
                }
                shoe = max(shoe, 0.04 - holes);
                float frog = max(dot(m - float2(0, 0.12), normalize(float2(0.86, 0.27))), -0.74 - m.y);
                return min(shoe, frog) * s;
            }

            // R138 (승규 님 10-14: "착지 할 때 지금 있는 원 모양 말고 마지막 사진 같은 로고로 해줘"): the knight's landing badge
            // (_Shape 4), the same as Skill Tile Board's: a horseshoe standing on its heels, its arch toward +y, the keyhole and
            // three nail holes a side in the core colour.
            float ShoeBadge(float2 p, float2 h, out float hole, out float nails)
            {
                float s = min(h.x, h.y);
                float2 q = float2(abs(p.x), p.y) / s;
                float outer = length(q - float2(0, 0.08)) - 0.84;
                outer = min(outer, RoundBox(q - float2(0, -0.36), float2(0.84, 0.44), 0.06));
                outer = min(outer, RoundBox(q - float2(0.86, -0.82), float2(0.13, 0.09), 0.03));
                hole = min(length(q - float2(0, 0.14)) - 0.34, RoundBox(q - float2(0, -0.68), float2(0.15, 0.82), 0.02));
                nails = min(min(length(q - float2(0.39, 0.54)), length(q - float2(0.6, 0.2))), length(q - float2(0.52, -0.42))) - 0.045;
                hole *= s;
                nails *= s;
                return outer * s;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.p;
                float2 h = _Size.xy * 0.5;
                bool ring = _Shape > 0.5 && _Shape < 1.5;
                bool badge = _Shape > 3.5;
                float d, inner = 1e4, badgeHole = 1e4, badgeNails = 1e4;
                if (_Shape < 0.5) d = RoundBox(p, h, min(_Round, min(h.x, h.y)));
                else if (badge) d = ShoeBadge(p, h, badgeHole, badgeNails);
                else if (_Shape > 2.5) d = HoofPrint(p, h);
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
                // R130: the charging dots (boxes), swept from the near edge (local -y) to the far one.
                if (_Charge > -0.5 && _Dot.a > 0 && !ring)
                {
                    float st = max(_DotStep, 0.04);
                    float2 cell = frac((p + h) / st) - 0.5;
                    float dd = length(cell) * st, rad = st * 0.3;
                    float dotM = 1 - smoothstep(rad - aa, rad + aa, dd);
                    float rimW = _RimWidth * step(0.001, _Rim.a);
                    float keepIn = smoothstep(_Ink + rimW + 0.01, _Ink + rimW + 0.035, e);
                    float y01 = (p.y + h.y) / max(2 * h.y, 1e-3);
                    float front = saturate(_Charge) * 1.1 - 0.05;
                    float lit = 1 - smoothstep(front - 0.1, front + 0.02, y01);
                    float on = dotM * keepIn * lerp(0.2, 1, lit);
                    col = lerp(col, _Dot.rgb, on * _Dot.a);
                    a = max(a, on * _Dot.a);
                    float fl = exp(-pow((y01 - front) * 2 * h.y / 0.05, 2)) * keepIn * step(_Charge, 0.995);
                    col = lerp(col, min(_Dot.rgb * 1.35 + 0.25, 1.6), fl * 0.85);
                    a = max(a, fl * 0.9);
                }
                if (ring && _Core.a > 0)
                {
                    float c = Band(inner, _Ink, _CoreWidth, aa);
                    col = lerp(col, _Core.rgb, c * _Core.a);
                    a = lerp(a, 1, c * _Core.a);
                }
                if (!ring && !badge && _Core.a > 0)
                {
                    float c = Band(e, _Ink + _RimWidth * step(0.001, _Rim.a), _CoreWidth, aa);
                    col = lerp(col, _Core.rgb, c * _Core.a);
                    a = lerp(a, 1, c * _Core.a);
                }
                // R138: the badge's keyhole and nail holes in the core colour, a thin light lip round the keyhole.
                if (badge)
                {
                    float inHole = 1 - smoothstep(-aa, aa, badgeHole), nail = 1 - smoothstep(-aa, aa, badgeNails);
                    float lip = Band(badgeHole, 0, 0.022, aa);
                    col = lerp(col, min(_Fill.rgb * 1.25 + 0.08, 1.3), lip * 0.8);
                    float dark = max(inHole, nail) * _Core.a;
                    col = lerp(col, _Core.rgb, dark);
                    a = lerp(a, 1, dark);
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
                // R130: the neon line just inside the ink.
                if (_Glow.a > 0)
                {
                    float tube = Band(e, _Ink, 0.028, aa);
                    if (ring) tube = max(tube, Band(inner, _Ink, 0.022, aa));
                    col = lerp(col, _Glow.rgb, tube * saturate(_Glow.a));
                    a = lerp(a, 1, tube * saturate(_Glow.a));
                }

                col = lerp(col, float3(1, 1, 1), _Flash);
                a = lerp(a, 1, _Flash);
                a *= cover * _Fade;
                return float4(col * a, a);
            }
            ENDCG
        }
    }
}
