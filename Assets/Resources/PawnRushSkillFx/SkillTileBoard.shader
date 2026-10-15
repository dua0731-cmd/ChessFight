// The Pawn Rush skill tiles as board-game pieces (R126, 승규 님 10-12: "스킬 그래픽이 체스 느낌이 났으면 … 사진 색과
// 같이 … 광택 있는 골드색 실버색도"): the same flat shapes as Skill Tile (rounded boxes, rings with a gap or only part of
// them shown, discs; fill, core, side rim, sliding stripes, ink), but standing up as a slab _Thick metres thick on a
// darker base, like the course's orange arrow pads on their navy blocks: the quad lies at the bottom of the slab and
// each pixel follows the eye's ray up through the slab (a prism of the shape), so the top shows where the ray meets
// it and the base's side wall where the ray enters through it, from any angle, inner walls of rings too. Adds a
// darker bevel band inside the edge, chunky chevron arrows on boxes (sliding with _ChevronPhase), polished metal for
// the gold and silver parts (a shaded face and a sheen sweeping over it) and a soft shadow round the foot on the
// floor. Premultiplied like Skill Tile. Built-in render pipeline.
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
        _Shape ("Shape: 0 rounded box, 1 ring, 2 disc", Float) = 0
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
            float _Thick, _BevelWidth, _Metal, _Sheen, _ChevronPhase, _ChevronSize, _ChevronWidth, _ChevronStep, _Shadow, _ShadowWidth;

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
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = (v.uv - 0.5) * _Quad.xy;
                // The tile is scaled to its quad's size in x and y (1 in z): object space times that is metres.
                float3 oc = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                o.cam = float3(oc.x * _Quad.x, oc.y * _Quad.y, oc.z);
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

            bool IsRing() { return _Shape > 0.5 && _Shape < 1.5; }

            // The shape at q: signed distance to its outline (below 0 inside), metres out from a ring's inner edge, and
            // the part of a ring that shows (a horseshoe's gap, a clock filling).
            void ShapeAt(float2 q, out float d, out float innerE, out float mask)
            {
                float2 h = _Size.xy * 0.5;
                innerE = 1e4;
                if (_Shape < 0.5) d = RoundBox(q, h, min(_Round, min(h.x, h.y)));
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
                    a = lerp(a, max(a, 0.95), _Metal);
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

                float rimW = _Rim.a > 0 ? _RimWidth : 0;
                if (ring && _Core.a > 0)
                {
                    float c = Band(innerE, _Ink, _CoreWidth, aa);
                    col = lerp(col, _Core.rgb, c * _Core.a);
                    a = lerp(a, 1, c * _Core.a);
                }
                if (!ring && _Core.a > 0)
                {
                    float c = Band(e, _Ink + rimW + (_Bevel.a > 0 ? _BevelWidth : 0), _CoreWidth, aa);
                    col = lerp(col, _Core.rgb, c * _Core.a);
                    a = lerp(a, 1, c * _Core.a);
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
                float4 top = Top(A, aa);
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
