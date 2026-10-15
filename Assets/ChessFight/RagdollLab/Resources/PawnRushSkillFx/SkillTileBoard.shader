// The Pawn Rush skill tiles as board-game pieces (R125 trial, 승규 님 10-12: "스킬 그래픽이 체스 느낌이 났으면 … 사진 색과
// 같이 … 광택 있는 골드색 실버색도"): the same flat shapes as Skill Tile (rounded boxes, rings with a gap or only part of
// them shown, discs; fill, core, side rim, sliding stripes, ink), but standing up as a slab _Thick metres thick on a
// darker base, like the course's orange arrow pads on their navy blocks: the quad lies at the bottom of the slab and
// each pixel follows the eye's ray up through the slab (a prism of the shape), so the top shows where the ray meets
// it and the base's side wall where the ray enters through it, from any angle, inner walls of rings too. Adds a
// darker bevel band inside the edge, chunky chevron arrows on boxes (sliding with _ChevronPhase), polished metal for
// the gold and silver parts (a shaded face and a sheen sweeping over it) and a soft shadow round the foot on the
// floor. Premultiplied like Skill Tile. Built-in render pipeline.
// R130 (승규 님 10-13, a chess edit short as the reference for "발판 그래픽이나 미리보기 차징"): a see-through square
// charges with a grid of light dots (LED-like) swept from its near edge (local -y) to its far one as the windup fills
// (_Charge), a bright line riding the front, and its edge glows like a neon tube with a soft halo round it (_Glow).
// R139 (승규 님 10-14, of the knight's landing badge: "테두리는 광택나는 검은 색 안에는 광택나는 어두운 금색으로"): _Gloss lights
// the horseshoe badge as a cast piece: its outline a raised bead of black lacquer, the horseshoe inside it polished dark
// gold sloping down to the outline and into the keyhole, the keyhole and nail holes black, all mirroring a bright sky over
// a sharp horizon (a glint where it crosses) with the sun's crisp highlight (_FxSun), and the sheen (_Sheen) sweeping.
Shader "ChessFight/Skill Tile Board"
{
    Properties
    {
        _Fill ("Fill (a = cover)", Color) = (0.99, 0.64, 0.24, 0.9)
        _Core ("Core band (a = cover)", Color) = (1, 1, 1, 0)
        _CoreWidth ("Core width (m)", Float) = 0
        _InkColor ("Ink", Color) = (0.14, 0.14, 0.18, 1)
        _Ink ("Ink width (m)", Float) = 0.035
        _Rim ("Side rim (a = cover)", Color) = (0, 0, 0, 0)
        _RimWidth ("Side rim width (m)", Float) = 0.08
        _Stripe ("Stripes (a = cover)", Color) = (0, 0, 0, 0)
        _StripePhase ("Stripe phase (m)", Float) = 0
        _StripeWidth ("Stripe width (m)", Float) = 0.12
        _Size ("Shape size (m): x across, y along", Vector) = (1.5, 1.5, 0, 0)
        _Quad ("Quad size (m), the shape plus room for its walls and shadow", Vector) = (1.5, 1.5, 0, 0)
        _Round ("Corner radius (m)", Float) = 0.06
        _Shape ("Shape: 0 rounded box, 1 ring, 2 disc, 3 hoof print (R133), 4 horseshoe badge (R138)", Float) = 0
        _Inner ("Ring inner radius (share of the outer)", Range(0, 1)) = 0.8
        _Gap ("Ring gap (degrees, centred on local -x)", Range(0, 360)) = 0
        _Arc ("Shown part, clockwise from local +y", Range(0, 1)) = 1
        _Flash ("White", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        _Thick ("Slab thickness (m), 0 = flat", Float) = 0.045
        _Base ("Base (its side walls; a = cover)", Color) = (0.14, 0.14, 0.18, 1)
        _Bevel ("Bevel band inside the edge (a = cover)", Color) = (0, 0, 0, 0)
        _BevelWidth ("Bevel width (m)", Float) = 0.07
        _Metal ("Polished metal", Range(0, 1)) = 0
        _Sheen ("Sheen phase (sweeps once per unit)", Float) = 0
        _Chevron ("Chevron arrows (boxes; a = cover)", Color) = (0, 0, 0, 0)
        _ChevronPhase ("Chevron phase along +y (m)", Float) = 0
        _ChevronSize ("Chevron size (m)", Float) = 0.42
        _ChevronWidth ("Chevron bar width (m)", Float) = 0.11
        _ChevronStep ("Chevron spacing (m)", Float) = 0.75
        _Shadow ("Floor shadow round the foot (a)", Float) = 0
        _ShadowWidth ("Floor shadow width (m)", Float) = 0.12
        _Glass ("See-through top (the floor shows through its fill; edges stay solid)", Range(0, 1)) = 0
        _Charge ("Charging dots: how far they have swept, near edge to far (below 0: none)", Float) = -1
        _Dot ("Charging dots (a = cover)", Color) = (1, 1, 1, 0)
        _DotStep ("Dot spacing (m)", Float) = 0.1
        [HDR] _Glow ("Neon edge and its halo (a = strength)", Color) = (0, 0, 0, 0)
        _GlowWidth ("Halo width outside the edge (m)", Float) = 0.12
        _Gloss ("Badge in glossy black lacquer and polished gold (R139)", Range(0, 1)) = 0
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

            float4 _Fill, _Core, _InkColor, _Rim, _Stripe, _Size, _Quad, _Base, _Bevel, _Chevron;
            float _CoreWidth, _Ink, _RimWidth, _StripePhase, _StripeWidth, _Round, _Shape, _Inner, _Gap, _Arc, _Flash, _Fade;
            float _Thick, _BevelWidth, _Metal, _Sheen, _ChevronPhase, _ChevronSize, _ChevronWidth, _ChevronStep, _Shadow, _ShadowWidth, _Glass;
            float4 _Dot, _Glow;
            float _Charge, _DotStep, _GlowWidth;
            float _Gloss;
            float4 _FxSun;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0;     // metres from the middle, on the bottom of the slab
                float3 cam : TEXCOORD1;   // the eye in the same metres (z out of the tile)
                float3 sun : TEXCOORD2;   // toward the sun, in the same frame (R139)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = (v.uv - 0.5) * _Quad.xy;
                // The tile is scaled to its quad's size in x and y (1 in z): object space times that is metres.
                float3 oc = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                o.cam = float3(oc.x * _Quad.x, oc.y * _Quad.y, oc.z);
                float3 sunW = dot(_FxSun.xyz, _FxSun.xyz) > 1e-6 ? normalize(_FxSun.xyz) : normalize(float3(0.35, 0.79, -0.5));
                float3 so = mul((float3x3)unity_WorldToObject, sunW);
                o.sun = normalize(float3(so.x * _Quad.x, so.y * _Quad.y, so.z) + 1e-6);
                return o;
            }

            float RoundBox(float2 p, float2 h, float r)
            {
                float2 q = abs(p) - h + r;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            float Band(float e, float from, float width, float aa)
            {
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
            // (_Shape 4), a horseshoe standing on its heels, its arch toward +y: the outline (the arch, straight sides, a small
            // foot out at each heel), the keyhole in it (a round top, its slot down between the heels) and three nail holes a
            // side, both painted in the core colour. Returns the outline's distance; hole and nails their own (below 0 inside).
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

            bool IsRing() { return _Shape > 0.5 && _Shape < 1.5; }
            bool IsBadge() { return _Shape > 3.5; }

            // The shape at q: signed distance to its outline (below 0 inside), metres out from a ring's inner edge, and
            // the part of a ring that shows (a horseshoe's gap, a clock filling).
            void ShapeAt(float2 q, out float d, out float innerE, out float mask)
            {
                float2 h = _Size.xy * 0.5;
                innerE = 1e4;
                float badgeHole, badgeNails;
                if (_Shape < 0.5) d = RoundBox(q, h, min(_Round, min(h.x, h.y)));
                else if (_Shape > 3.5) d = ShoeBadge(q, h, badgeHole, badgeNails);
                else if (_Shape > 2.5) d = HoofPrint(q, h);
                else
                {
                    float R = min(h.x, h.y), l = length(q);
                    d = l - R;
                    if (IsRing())
                    {
                        innerE = l - R * _Inner;
                        d = max(d, -innerE);
                    }
                }
                mask = 1;
                if (IsRing())
                {
                    float a01 = frac(atan2(q.x, q.y) / 6.2831853 + 1);
                    float gapHalf = _Gap / 720.0;
                    if (gapHalf > 0) mask *= smoothstep(gapHalf - 0.004, gapHalf + 0.004, abs(a01 - 0.75));
                    mask *= 1 - smoothstep(_Arc - 0.002, _Arc + 0.002, a01) * step(_Arc, 0.999);
                }
            }

            float Cover(float2 q, float aa)
            {
                float d, ie, m;
                ShapeAt(q, d, ie, m);
                return (1 - smoothstep(-aa, aa, d)) * m;
            }

            // R139: the horseshoe badge (_Shape 4) lit as glossy black lacquer and polished dark gold at q on the top of the
            // slab, seen along V with the sun along L (both in the tile's metres, z out of it), premultiplied (no fade).
            // _InkColor is the lacquer, _Fill the gold, _Ink the outline's width, _BevelWidth how wide the gold slopes
            // down to it; the keyhole and the nail holes are lacquer too.
            float4 BadgeGloss(float2 q, float aa, float3 V, float3 L)
            {
                float2 h = _Size.xy * 0.5;
                float hole, nails;
                float d = ShoeBadge(q, h, hole, nails);
                float cover = 1 - smoothstep(-aa, aa, d);
                if (cover <= 0.001) return 0;
                float e = -d;
                // Which way is out of the outline, out of the keyhole and out of the nearest nail hole (for the slopes).
                float2 ex = float2(0.004, 0), ey = float2(0, 0.004);
                float hx0, hx1, hy0, hy1, nx0, nx1, ny0, ny1;
                float dx = ShoeBadge(q + ex, h, hx1, nx1) - ShoeBadge(q - ex, h, hx0, nx0);
                float dy = ShoeBadge(q + ey, h, hy1, ny1) - ShoeBadge(q - ey, h, hy0, ny0);
                float2 g = normalize(float2(dx, dy) + 1e-6);
                float2 gh = normalize(float2(hx1 - hx0, hy1 - hy0) + 1e-6);
                float2 gn = normalize(float2(nx1 - nx0, ny1 - ny0) + 1e-6);

                float rimW = max(_Ink, 0.01);
                float rim = 1 - smoothstep(rimW - aa, rimW + aa, e);
                float inHole = 1 - smoothstep(-aa, aa, hole);
                float inNail = 1 - smoothstep(-aa, aa, nails);
                float black = max(rim, max(inHole, inNail));

                // The slopes: a round bead along the outline; the gold falling away to it, and rounding down into the
                // keyhole and the nail holes.
                float s = clamp(1 - 2 * e / rimW, -1, 1);
                float3 nRim = normalize(float3(g * s * 0.9, sqrt(max(0.19, 1 - s * s * 0.81))));
                float2 tilt = 0;
                float bw = max(_BevelWidth, 0.01);
                tilt += -g * (1 - saturate((e - rimW) / bw)) * 0.55;
                tilt += -gh * (1 - saturate(hole / 0.035)) * 0.75;
                tilt += -gn * (1 - saturate(nails / 0.02)) * 0.6;
                float3 nGold = normalize(float3(tilt, 1));
                float3 n = normalize(lerp(nGold, rim > 0.5 ? nRim : float3(0, 0, 1), black));

                float3 R = reflect(-V, n);
                float raa = max(fwidth(R.z), 1e-3);
                float sky = smoothstep(-raa, raa, R.z);
                float up = saturate(R.z);
                float horizon = exp(-pow((R.z - 0.05) / 0.04, 2));
                float3 H = normalize(L + V);
                float nh = saturate(dot(n, H));
                float spot = smoothstep(0.45, 0.6, pow(nh, 160));
                float soft = pow(nh, 22) * 0.22;
                float ndv = saturate(dot(n, V));
                float fres = 0.06 + 0.94 * pow(1 - ndv, 4);
                float ndl = saturate(dot(n, L));

                // The sheen sweeping over it once per unit of _Sheen (a wide soft band and a thin bright one).
                float R0 = max(max(h.x, h.y), 0.2);
                float ss = (q.x + q.y) * 0.70710678 / R0;
                float pos = frac(_Sheen) * 3.6 - 1.8;
                float band = exp(-pow((ss - pos) / 0.16, 2)) * 0.55 + exp(-pow((ss - pos + 0.4) / 0.045, 2)) * 0.5;

                // Lacquer: a dark mirror, the sky faint in it and brighter at a slant (kept low: it stays black, the first
                // take went grey), a bright line on the horizon, the crisp highlight.
                float3 lacquer = _InkColor.rgb * (0.6 + 0.4 * ndl)
                    + float3(0.86, 0.88, 0.94) * (lerp(0.02, 0.32 + 0.2 * up, sky) + horizon * 0.7) * fres * 0.75
                    + float3(1, 0.98, 0.94) * (spot * 1.1 + soft * 0.5 + band * 0.22);
                // Holes are deeper: less of the sky reaches them.
                lacquer *= lerp(1, 0.45, max(inHole, inNail) * (1 - rim));
                // Dark gold, polished: its face lit from one side across it (a gentle sweep from light to shade), the sky
                // only near the top of its reflection (the first take mirrored a bright sky all over and read as caramel),
                // darker below the horizon, its glint and the highlight in its light colour.
                float sweepG = saturate(0.5 + 0.5 * (q.y - q.x) / R0);
                float3 goldLight = min(_Fill.rgb * 1.6 + 0.06, 1.2);
                float3 face = _Fill.rgb * lerp(0.72, 1.12, sweepG);
                float3 gold = lerp(_Fill.rgb * 0.28, lerp(face, goldLight, smoothstep(0.55, 1.0, up)), sky)
                    + goldLight * (horizon * 0.5 + spot + soft * 0.8 + band);

                float3 col = lerp(gold, lacquer, black);
                col = lerp(col, float3(1, 1, 1), _Flash);
                return float4(col * cover, cover);
            }

            // The top of the slab at q, premultiplied (no fade).
            float4 Top(float2 q, float aa)
            {
                float d, innerE, mask;
                ShapeAt(q, d, innerE, mask);
                float cover = (1 - smoothstep(-aa, aa, d)) * mask;
                if (cover <= 0.001) return 0;
                bool ring = IsRing();
                float e = -d;   // metres in from the outer edge
                float2 h = _Size.xy * 0.5;
                float R = max(max(h.x, h.y), 0.2);
                float3 col = _Fill.rgb;
                float a = _Fill.a;

                // Polished metal: lit from the top left, darker to the bottom right, and a sheen (a wide soft band with
                // a thin bright one beside it) sweeping across once per unit of _Sheen.
                if (_Metal > 0)
                {
                    float g = saturate(0.5 + 0.5 * (q.y - q.x) / R);
                    float3 m = lerp(_Fill.rgb * 0.7, min(_Fill.rgb * 1.22 + 0.07, 1.35), g);
                    float s = (q.x + q.y) * 0.70710678 / R;
                    float pos = frac(_Sheen) * 3.6 - 1.8;
                    float band = exp(-pow((s - pos) / 0.18, 2)) * 0.55 + exp(-pow((s - pos + 0.42) / 0.05, 2)) * 0.45;
                    m += float3(1, 0.98, 0.93) * band;
                    col = lerp(col, m, _Metal);
                    // Solid metal hides the floor; see-through (R129) it stays see-through, its sheen a brighter streak.
                    a = lerp(a, max(a, 0.95), _Metal * (1 - _Glass));
                    a = max(a, saturate(band) * 0.6 * _Metal * _Glass);
                }

                float rimW = _Rim.a > 0 ? _RimWidth : 0;

                // R130: the charging dots (boxes): a grid of round dots inside the edges, lit behind a front that sweeps
                // from the near edge to the far one as _Charge goes 0 -> 1 (faint ahead of it), a bright line on the front.
                if (_Charge > -0.5 && _Dot.a > 0 && !ring)
                {
                    float st = max(_DotStep, 0.04);
                    float2 g = (q + h) / st;
                    float2 cell = frac(g) - 0.5;
                    float dd = length(cell) * st;
                    float rad = st * 0.3;
                    float dotM = 1 - smoothstep(rad - aa, rad + aa, dd);
                    float keepIn = smoothstep(_Ink + rimW + 0.01, _Ink + rimW + 0.035, e);
                    float y01 = (q.y + h.y) / max(2 * h.y, 1e-3);
                    float front = saturate(_Charge) * 1.1 - 0.05;
                    float lit = 1 - smoothstep(front - 0.1, front + 0.02, y01);
                    float on = dotM * keepIn * lerp(0.2, 1, lit);
                    col = lerp(col, _Dot.rgb, on * _Dot.a);
                    a = max(a, on * _Dot.a);
                    float fl = exp(-pow((y01 - front) * 2 * h.y / 0.05, 2)) * keepIn * step(_Charge, 0.995);
                    col = lerp(col, min(_Dot.rgb * 1.35 + 0.25, 1.6), fl * 0.85);
                    a = max(a, fl * 0.9);
                }

                if (_Stripe.a > 0)
                {
                    float s = frac((q.x + q.y) * 0.70710678 / (2 * _StripeWidth) + _StripePhase / (2 * _StripeWidth));
                    float saa = max(fwidth(s), 1e-3);
                    float on = smoothstep(0.5 - saa, 0.5 + saa, s) * (1 - smoothstep(1 - saa, 1, s));
                    col = lerp(col, _Stripe.rgb, on * _Stripe.a);
                    a = max(a, on * _Stripe.a);
                }

                // Chunky chevrons pointing along +y (boxes): the course's arrow pads.
                if (_Chevron.a > 0 && !ring)
                {
                    float stepY = max(_ChevronStep, 0.2);
                    float yy = q.y - _ChevronPhase;
                    float cy = yy - stepY * floor(yy / stepY + 0.5);
                    float w = _ChevronSize * 0.5;
                    float2 c = float2(q.x, cy);
                    float dc = min(Seg(c, float2(-w, -w * 0.45), float2(0, w * 0.55)), Seg(c, float2(0, w * 0.55), float2(w, -w * 0.45))) - _ChevronWidth * 0.5;
                    float keep = smoothstep(_Ink + 0.1 - aa, _Ink + 0.1 + aa, e);
                    float ch = (1 - smoothstep(-aa, aa, dc)) * keep;
                    col = lerp(col, _Chevron.rgb, ch * _Chevron.a);
                    a = lerp(a, 1, ch * _Chevron.a);
                }

                if (ring && _Core.a > 0)
                {
                    float c = Band(innerE, _Ink, _CoreWidth, aa);
                    col = lerp(col, _Core.rgb, c * _Core.a);
                    a = lerp(a, 1, c * _Core.a);
                }
                if (!ring && !IsBadge() && _Core.a > 0)
                {
                    float c = Band(e, _Ink + rimW + (_Bevel.a > 0 ? _BevelWidth : 0), _CoreWidth, aa);
                    col = lerp(col, _Core.rgb, c * _Core.a);
                    a = lerp(a, 1, c * _Core.a);
                }
                // R138: the badge's keyhole and nail holes in the core colour, a thin light lip round the keyhole.
                if (IsBadge())
                {
                    float hole, nails;
                    ShoeBadge(q, h, hole, nails);
                    float inHole = 1 - smoothstep(-aa, aa, hole), nail = 1 - smoothstep(-aa, aa, nails);
                    float lip = Band(hole, 0, 0.022, aa);
                    col = lerp(col, min(_Fill.rgb * 1.25 + 0.08, 1.3), lip * 0.8);
                    float dark = max(inHole, nail) * _Core.a;
                    col = lerp(col, _Core.rgb, dark);
                    a = lerp(a, 1, dark);
                }
                // The bevel: a darker band just inside the side rim (the pads' darker edge), and round a ring's hole.
                if (_Bevel.a > 0)
                {
                    float b = Band(e, _Ink + rimW, _BevelWidth, aa);
                    if (ring) b = max(b, Band(innerE, _Ink, _BevelWidth * 0.6, aa));
                    col = lerp(col, _Bevel.rgb, b * _Bevel.a);
                    a = lerp(a, 1, b * _Bevel.a);
                }
                if (_Rim.a > 0)
                {
                    float r = Band(e, _Ink, _RimWidth, aa);
                    if (!ring)
                    {
                        float a01 = frac(atan2(q.x, q.y) / 6.2831853 + 1);
                        r *= 1 - smoothstep(_Arc - 0.002, _Arc + 0.002, a01) * step(_Arc, 0.999);
                    }
                    col = lerp(col, _Rim.rgb, r * _Rim.a);
                    a = lerp(a, 1, r * _Rim.a);
                }
                // A thin bright lip just inside the ink on metal: a polished edge.
                if (_Metal > 0)
                {
                    float lip = Band(e, _Ink, 0.018, aa);
                    if (ring) lip = max(lip, Band(innerE, _Ink, 0.014, aa));
                    col = lerp(col, min(_Fill.rgb * 1.5 + 0.25, 1.4), lip * _Metal * 0.8);
                }
                float ink = 1 - smoothstep(_Ink - aa, _Ink + aa, e);
                if (ring) ink = max(ink, 1 - smoothstep(_Ink - aa, _Ink + aa, innerE));
                col = lerp(col, _InkColor.rgb, ink * _InkColor.a);
                a = lerp(a, 1, ink * _InkColor.a);
                // R130: the neon tube, a bright line just inside the ink (round a ring's hole too).
                if (_Glow.a > 0)
                {
                    float tube = Band(e, _Ink, 0.028, aa);
                    if (ring) tube = max(tube, Band(innerE, _Ink, 0.022, aa));
                    col = lerp(col, _Glow.rgb, tube * saturate(_Glow.a));
                    a = lerp(a, 1, tube * saturate(_Glow.a));
                }

                col = lerp(col, float3(1, 1, 1), _Flash);
                a = lerp(a, 1, _Flash);
                a *= cover;
                return float4(col * a, a);
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.p;
                float3 c = i.cam;
                c.z = abs(c.z);   // seen from its back (a ring facing away): stand it toward the eye all the same
                float aa = max(length(fwidth(p)) * 0.75, 1e-4);
                float h = _Thick;

                float2 A = p;
                float side = 0, sideAt = 1;
                if (h > 1e-4 && c.z > h * 1.05)
                {
                    // Where the eye's ray through this pixel crosses the top of the slab, then down along it to the
                    // bottom (this pixel): the first point inside the shape is where it enters through a wall.
                    A = p + (c.xy - p) * (h / c.z);
                    [unroll] for (int k = 1; k <= 8; k++)
                    {
                        float t = k / 8.0;
                        float cv = Cover(lerp(A, p, t), aa);
                        if (cv > 0.5 && sideAt > 0.99) sideAt = t;
                        side = max(side, cv);
                    }
                }
                float4 top;
                if (_Gloss > 0.5 && IsBadge())
                {
                    float3 V = normalize(c - float3(A, h) + float3(0, 0, 1e-4));
                    top = BadgeGloss(A, aa, V, normalize(i.sun));
                }
                else top = Top(A, aa);
                float topCover = top.a > 0 ? Cover(A, aa) : 0;

                // The wall: the base colour, lighter at the top edge, darker toward the foot.
                float3 wall = _Base.rgb * lerp(1.25, 0.72, sideAt);
                wall = lerp(wall, _Base.rgb * 2.0 + 0.06, saturate(1 - sideAt * 5) * 0.4);
                wall = lerp(wall, float3(1, 1, 1), _Flash * 0.35);
                float wallA = side * (1 - topCover) * _Base.a;

                // A soft shadow on the floor round the foot (floor tiles only).
                float shadowA = 0;
                if (_Shadow > 0)
                {
                    float d, ie, m;
                    ShapeAt(p, d, ie, m);
                    shadowA = _Shadow * (1 - smoothstep(0, max(_ShadowWidth, 1e-3), max(d, 0))) * m;
                }

                float3 col = top.rgb;
                float a = top.a;
                // R130: the neon halo round the edge, outside the shape (and inside a ring's hole), under the top.
                if (_Glow.a > 0 && _GlowWidth > 1e-3)
                {
                    float d, ie, m;
                    ShapeAt(p, d, ie, m);
                    float outD = max(d, 0);   // outside, or in a ring's hole
                    float halo = 1 - smoothstep(0, _GlowWidth, outD);
                    halo *= halo * step(1e-4, outD) * m;
                    float ha = halo * saturate(_Glow.a) * 0.5;
                    col += (1 - a) * _Glow.rgb * ha;
                    a += (1 - a) * ha;
                }
                col += (1 - a) * wall * wallA;
                a += (1 - a) * wallA;
                col += (1 - a) * (_Base.rgb * 0.35) * shadowA;
                a += (1 - a) * shadowA;
                col *= _Fade;
                a *= _Fade;
                if (a <= 0.002) discard;
                return float4(col, a);
            }
            ENDCG
        }
    }
}
