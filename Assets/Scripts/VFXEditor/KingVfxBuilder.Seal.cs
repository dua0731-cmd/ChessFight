using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// E4's seal (10-28, 사용자 "킹 문양 모양을 사진이랑 같게 해주고", a picture of a gold seal under the king): white on
    /// black, filling the square. A heavy outer ring and a thin one with eight small arrowheads between them pointing out,
    /// two rings inside those, and a filled disc with the king's crown at its middle (<see cref="CrownPieces"/>) bright
    /// and parted by thin dark lines, leaning a little to the right as in the picture. The cross is toward +V before that lean,
    /// which the prefab lays toward his facing. Anti-aliased to a pixel.
    /// </summary>
    public static partial class KingVfxBuilder
    {
        const string CrownSealTexture = VfxAssets.Root + "/Textures/T_King_CrownSeal.png";
        const float SealLean = 12f;

        static float[] CrownSeal(int n)
        {
            var v = new float[n * n];
            float px = 2f / n;
            float lean = SealLean * Mathf.Deg2Rad, cos = Mathf.Cos(lean), sin = Mathf.Sin(lean);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + .5f) / n * 2f - 1f, (y + .5f) / n * 2f - 1f);
                    float r = p.magnitude, a = Mathf.Atan2(p.y, p.x);
                    float m = .05f * Mathf.Clamp01(1f - r);
                    m = Mathf.Max(m, SealRing(r, .95f, .028f, px));
                    m = Mathf.Max(m, SealRing(r, .87f, .01f, px) * .9f);
                    // Eight arrowheads between the outer rings and the next, pointing out, every other one smaller.
                    float seg = Mathf.PI / 4f;
                    int i = Mathf.FloorToInt(Mathf.Repeat(a + seg * .5f, 2f * Mathf.PI) / seg) % 8;
                    float side = (Mathf.Repeat(a + seg * .5f, seg) - seg * .5f) * r;
                    float big = i % 2 == 0 ? 1f : .7f;
                    float arrow = SealTriangle(new Vector2(side, r), new Vector2(-.05f * big, .79f), new Vector2(.05f * big, .79f), new Vector2(0f, .79f + .07f * big));
                    m = Mathf.Max(m, SealLine(Mathf.Abs(arrow), .007f, px));
                    m = Mathf.Max(m, (1f - SealSmooth(0f, px, arrow)) * .45f);
                    m = Mathf.Max(m, SealRing(r, .75f, .012f, px));
                    m = Mathf.Max(m, SealRing(r, .68f, .007f, px) * .85f);
                    // The disc: a dim gold fill with a bright rim.
                    m = Mathf.Max(m, (1f - SealSmooth(.62f, .62f + px, r)) * .32f);
                    m = Mathf.Max(m, SealRing(r, .62f, .012f, px));
                    // The crown, leaning: bright inside, a thin dark line along every piece's own edge and down between
                    // the lobes. The band lies over the body's foot: its edge is not drawn under it.
                    var q = new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
                    CrownPieces(q, out float band, out float body, out float orb, out float cross);
                    float inside = Mathf.Min(Mathf.Min(band, body), Mathf.Min(orb, cross));
                    float under = SealSmooth(0f, px, band);
                    float seam = Mathf.Max(Mathf.Max(SealLine(Mathf.Abs(band), .008f, px), SealLine(Mathf.Abs(body), .008f, px) * under),
                        Mathf.Max(SealLine(Mathf.Abs(orb), .008f, px), SealLine(Mathf.Abs(cross), .008f, px)));
                    if (q.y > -.2f && q.y < .12f)
                    {
                        float widen = 1f + .25f * Mathf.Clamp01((q.y + .2f) / .3f);
                        foreach (float at in Partings)
                            seam = Mathf.Max(seam, SealLine(Mathf.Abs(q.x - at * widen), .007f, px) * under * (1f - SealSmooth(0f, px, body)));
                    }
                    float fill = 1f - SealSmooth(0f, px, inside);
                    if (fill > 0f) m = Mathf.Lerp(m, .95f * (1f - .85f * seam), fill);
                    // A soft glow just round the crown.
                    m = Mathf.Max(m, .5f * Mathf.Clamp01(1f - inside / .05f) * (1f - fill));
                    m *= 1f - SealSmooth(.975f, 1f, r);
                    v[y * n + x] = m;
                }
            return v;
        }

        // Where the body parts between its lobes (at the band; they spread a little going up).
        static readonly float[] Partings = { -.225f, -.075f, .075f, .225f };

        /// <summary>Signed distances (below 0 inside) to the crown's pieces, upright, in the seal's -1..1 square (10-28,
        /// 사용자 "문양만 킹 왕관으로 바꿔주면 돼"): the band at its foot; the body widening up from it to five round
        /// lobes, the middle one highest; the orb on the middle lobe and the cross on the orb.</summary>
        static void CrownPieces(Vector2 p, out float band, out float body, out float orb, out float cross)
        {
            band = SealBox(p, new Vector2(0f, -.26f), new Vector2(.3f, .06f), .025f);
            const float Bottom = -.22f, Top = .07f;
            float t = Mathf.Clamp01((p.y - Bottom) / (Top - Bottom)), half = Mathf.Lerp(.28f, .36f, t);
            float dx = Mathf.Abs(p.x) - half, dy = Mathf.Max(Bottom - p.y, p.y - Top);
            body = dx < 0f && dy < 0f ? Mathf.Max(dx, dy) : new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
            float[] at = { -.3f, -.15f, 0f, .15f, .3f }, top = { .06f, .13f, .17f, .13f, .06f };
            for (int k = 0; k < 5; k++) body = Mathf.Min(body, (p - new Vector2(at[k], top[k])).magnitude - .082f);
            orb = (p - new Vector2(0f, .27f)).magnitude - .045f;
            cross = Mathf.Min(SealBox(p, new Vector2(0f, .39f), new Vector2(.03f, .09f), .01f),
                SealBox(p, new Vector2(0f, .41f), new Vector2(.08f, .028f), .01f));
        }

        static float SealBox(Vector2 p, Vector2 centre, Vector2 half, float round)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - half + Vector2.one * round;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - round;
        }

        static float SealTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float edge = Mathf.Min(SealSegment(p, a, b), Mathf.Min(SealSegment(p, b, c), SealSegment(p, c, a)));
            float s1 = SealCross(b - a, p - a), s2 = SealCross(c - b, p - b), s3 = SealCross(a - c, p - c);
            bool inside = (s1 >= 0f && s2 >= 0f && s3 >= 0f) || (s1 <= 0f && s2 <= 0f && s3 <= 0f);
            return inside ? -edge : edge;
        }

        static float SealSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        static float SealCross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static float SealRing(float r, float centre, float half, float px) => 1f - SealSmooth(half, half + px, Mathf.Abs(r - centre));

        static float SealLine(float d, float half, float px) => 1f - SealSmooth(half, half + px, d);

        static float SealSmooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }
    }
}
