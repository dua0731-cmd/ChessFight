using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The rook's pieces (R102, 승규 님 10-10 with the photo of the team's four piece characters: "벽돌 탑보다는 저 사진에서
    /// 발 다리만 뺀 체스탑이 올라 왔으면 좋겠어, 올라오는 순서는 스킬을 사용할 때마다 무작위"): as the wave reaches each square a
    /// pawn, a rook, a knight or a bishop of that photo bursts up out of the floor without its feet (a faceted wooden
    /// body, big black eyes with a shine, pink cheeks, the rook's orange collar, dark mittens thrown up as it pops), hops
    /// past its height, holds and sinks back. Which piece comes up where is shuffled anew for every slam. The shapes
    /// follow the menu's stand-ins for the same characters (<see cref="ChessFight.Game.PieceFigure"/>), painted the
    /// design A way: three hard bands from the sun and an ink rim (the rim drawn from a smooth copy of each part so it
    /// has no cracks where the facets meet).
    /// </summary>
    public partial class SwordFightSkillFx
    {
        static readonly PieceKind[] StatueKinds = { PieceKind.Pawn, PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop };
        /// <summary>The statues stand this big next to the menu figures (about 1.2 m tall: a head over a fighter).</summary>
        const float StatueScale = 0.62f;

        static readonly Kit.Palette Wood = new Kit.Palette("#F1E2CB", "#FFF8EE", "#D3B994", "#4A3220");
        static readonly Kit.Palette WoodDark = new Kit.Palette("#C9A27E", "#E3C6A6", "#A27C5A", "#4A3220");
        static readonly Kit.Palette Mitten = new Kit.Palette("#3A3035", "#5E5158", "#231C20", "#120C0F");
        static readonly Kit.Palette EyeBlack = new Kit.Palette("#16121A", "#4A4252", "#0B090D", "#0B090D");
        static readonly Kit.Palette Shine = new Kit.Palette("#FFFFFF", "#FFFFFF", "#E8E8F0", "#FFFFFF");
        static readonly Kit.Palette Blush = new Kit.Palette("#F4A0A3", "#FFC4C4", "#E0838A", "#E0838A");

        /// <summary>One part of a statue: its painted mesh, the smooth copy its ink rim is drawn from, where it sits (in
        /// the menu figure's metres, the foot of its base at 0) and which group it moves with (0 the body, −1 / +1 the
        /// left / right arm, turning at its shoulder).</summary>
        struct StatuePart
        {
            public Mesh fill, line;
            public Kit.Palette colors;
            public float ink;
            public Vector3 at, scale;
            public Quaternion rot;
            public int group;
        }

        readonly Dictionary<PieceKind, List<StatuePart>> statues = new Dictionary<PieceKind, List<StatuePart>>();
        readonly List<Mesh> statueMeshes = new List<Mesh>();
        static readonly Vector3 Shoulder = new Vector3(0.25f, 0.77f, 0f);

        /// <summary>The kinds for <paramref name="n"/> squares: the four in a new random order, again and again, never the same
        /// piece twice in a row.</summary>
        static List<PieceKind> StatueOrder(int n)
        {
            var list = new List<PieceKind>();
            while (list.Count < n)
            {
                var deck = new List<PieceKind>(StatueKinds);
                for (int i = deck.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (deck[i], deck[j]) = (deck[j], deck[i]);
                }
                if (list.Count > 0 && deck[0] == list[list.Count - 1]) (deck[0], deck[deck.Count - 1]) = (deck[deck.Count - 1], deck[0]);
                list.AddRange(deck);
            }
            list.RemoveRange(n, list.Count - n);
            return list;
        }

        /// <summary>A piece of the photo bursting up out of the floor at <paramref name="c"/>, facing back up the lane
        /// (give or take): up past its height in four frames with its mittens thrown up, a squash as it lands, held, and
        /// down into the floor again; the square under it flashes, dust and floor chips fly.</summary>
        void Statue(PieceKind kind, Vector3 c, Vector3 dir, RagdollPawn caster)
        {
            if (!FloorAt(c, out Vector3 floor)) return;   // no floor there: nothing comes up
            var parts = StatueParts(kind);
            var root = new GameObject($"Rook statue ({kind})").transform;
            root.SetParent(kit.root, false);
            var arms = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                arms[i] = new GameObject(i == 0 ? "Arm L" : "Arm R").transform;
                arms[i].SetParent(root, false);
                arms[i].localPosition = new Vector3(Shoulder.x * (i == 0 ? -1f : 1f), Shoulder.y, Shoulder.z);
            }
            var props = new List<Kit.Prop>();
            foreach (var part in parts)
            {
                var parent = part.group == 0 ? root : arms[part.group < 0 ? 0 : 1];
                var p = new Kit.Prop(kit, part.fill, part.colors, "Statue part", parent) { inkWidth = part.ink };
                p.t.GetChild(0).GetComponent<MeshFilter>().sharedMesh = part.line;
                Vector3 at = part.at;
                if (part.group != 0) at -= new Vector3(Shoulder.x * part.group, Shoulder.y, Shoulder.z);
                p.t.localPosition = at;
                p.t.localRotation = part.rot;
                p.t.localScale = part.scale;
                props.Add(p);
            }
            // How far down it starts: its whole height, stretched as it is in its first frame, under the floor.
            float height = 0f;
            foreach (var part in parts) height = Mathf.Max(height, part.at.y + part.fill.bounds.max.y * part.scale.y);
            float H = (height * 1.16f + 0.05f) * StatueScale;
            Vector3 back = -Kit.FlatDir(dir, Vector3.forward);
            Quaternion yaw = Quaternion.LookRotation(Quaternion.AngleAxis(Random.Range(-22f, 22f), Vector3.up) * back, Vector3.up);
            kit.Release(new List<Vector3> { floor + Vector3.up * 0.016f }, dir, caster, 1.45f);
            kit.DustRing(floor, 5, 0.85f, null, 0.28f, 0.5f);
            kit.Drops(floor + Vector3.up * 0.3f, Vector3.up, Kit.Stone, 3, 6f);
            float wobble = Random.Range(0f, 6.28f);
            void Pose(float a)
            {
                // Up: past its height by frame 4 (stretched tall), a squash as it lands (frames 4-7), held, down from
                // frame 20 to 36. The mittens go up as it pops and wave a little while it stands.
                float up = a < 4f ? Kit.EaseOut(a / 4f) * 1.12f : a < 7f ? Mathf.Lerp(1.12f, 1f, (a - 4f) / 3f) : a < 20f ? 1f : 1f - Kit.EaseIn((a - 20f) / 16f);
                float stretch = a < 4f ? 1f - a / 4f : 0f, squash = a >= 4f && a < 8f ? Mathf.Sin(Mathf.PI * (a - 4f) / 4f) : 0f;
                float sy = 1f + 0.16f * stretch - 0.12f * squash, sxz = 1f - 0.07f * stretch + 0.07f * squash;
                float lean = a >= 7f && a < 20f ? Mathf.Sin(a * 0.45f + wobble) * 3f * (1f - (a - 7f) / 13f) : 0f;
                root.SetPositionAndRotation(floor + Vector3.up * (H * (up - 1f) - 0.02f), yaw * Quaternion.Euler(0f, 0f, lean));
                root.localScale = new Vector3(sxz, sy, sxz) * StatueScale;
                float lift = a < 4f ? Mathf.Lerp(-15f, 75f, a / 4f) : a < 10f ? Mathf.Lerp(75f, 30f, (a - 4f) / 6f) : a < 20f ? 30f + 8f * Mathf.Sin((a - 10f) * 0.7f) : Mathf.Lerp(30f, 65f, (a - 20f) / 8f);
                arms[0].localRotation = Quaternion.Euler(0f, 0f, -lift);
                arms[1].localRotation = Quaternion.Euler(0f, 0f, lift);
                foreach (var p in props)
                {
                    p.flash = a < 2f ? 1f : 0f;
                    p.Apply();
                }
            }
            // Made inside another effect's step, it is first stepped next frame: until then it waits under the floor.
            Pose(0f);
            var f = kit.Run(36f * F, (fx, d) =>
            {
                Pose(fx.age / F);
                return true;
            });
            foreach (var p in props) f.props.Add(p);
            f.end = () => { if (root != null) Destroy(root.gameObject); };
        }

        List<StatuePart> StatueParts(PieceKind kind)
        {
            if (statues.TryGetValue(kind, out var cached)) return cached;
            var list = new List<StatuePart>();
            const float Base = 0.16f;   // the menu figure's skirt starts over its feet: the statues have none
            void Add(Mesh fill, Mesh line, Kit.Palette colors, float ink, Vector3 at, Quaternion rot, Vector3 scale, int group = 0)
            {
                list.Add(new StatuePart { fill = fill, line = line, colors = colors, ink = ink, at = at - Vector3.up * Base, rot = rot, scale = scale, group = group });
            }
            void Smooth(Mesh m, Kit.Palette colors, float ink, Vector3 at, Quaternion rot, Vector3 scale, int group = 0) => Add(m, m, colors, ink, at, rot, scale, group);

            // The skirt, the rook's orange collar and the disc on it (every piece has them).
            var (skirt, skirtLine) = LatheMesh(new[] { V(0f, .16f), V(.56f, .16f), V(.58f, .24f), V(.54f, .32f), V(.48f, .36f), V(.47f, .44f), V(.41f, .48f), V(.37f, .58f), V(.31f, .76f), V(.27f, .94f), V(.25f, 1.03f), V(0f, 1.03f) }, 14, true);
            Add(skirt, skirtLine, Wood, 0.018f, Vector3.zero, Quaternion.identity, Vector3.one);
            var (collar, collarLine) = LatheMesh(new[] { V(0f, -.035f), V(.3f, -.035f), V(.3f, .035f), V(0f, .035f) }, 14, true);
            Add(collar, collarLine, Kit.Rook, 0.016f, new Vector3(0f, 1.065f, 0f), Quaternion.identity, Vector3.one);
            var (disc, discLine) = LatheMesh(new[] { V(0f, -.04f), V(.35f, -.04f), V(.35f, .04f), V(0f, .04f) }, 14, true);
            Add(disc, discLine, Wood, 0.016f, new Vector3(0f, 1.135f, 0f), Quaternion.identity, Vector3.one);
            // The arms and their mittens (no feet, no legs).
            var (arm, armLine) = LatheMesh(new[] { V(0f, -.17f), V(.085f, -.17f), V(.075f, .17f), V(0f, .17f) }, 8, true);
            var ball = SmoothSphere();
            for (int s = -1; s <= 1; s += 2)
            {
                Add(arm, armLine, Wood, 0.014f, new Vector3(s * .4f, .93f, 0f), Quaternion.Euler(0f, 0f, s * 83f), Vector3.one, s);
                Smooth(ball, Mitten, 0.012f, new Vector3(s * .65f, .9f, .03f), Quaternion.identity, Vector3.one * .35f, s);
            }

            switch (kind)
            {
                case PieceKind.Rook:
                {
                    var (tower, towerLine) = LatheMesh(new[] { V(0f, -.33f), V(.42f, -.33f), V(.4f, .33f), V(0f, .33f) }, 14, true);
                    Add(tower, towerLine, Wood, 0.018f, new Vector3(0f, 1.48f, 0f), Quaternion.identity, Vector3.one);
                    var merlon = kit.RoundBox(new Vector3(.2f, .16f, .14f), 0.03f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f + Mathf.PI / 6f;
                        Smooth(merlon, Wood, 0.016f, new Vector3(Mathf.Sin(a) * .31f, 1.89f, Mathf.Cos(a) * .31f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), Vector3.one);
                    }
                    Face(list, new Vector3(0f, 1.46f, 0f), .405f, .14f, .04f, .24f, -.1f, 1f, true, Base);
                    break;
                }
                case PieceKind.Bishop:
                {
                    var (mitre, mitreLine) = LatheMesh(new[] { V(0f, 1.13f), V(.27f, 1.15f), V(.35f, 1.3f), V(.37f, 1.46f), V(.33f, 1.63f), V(.23f, 1.81f), V(.09f, 1.93f), V(0f, 1.96f) }, 14, true);
                    Add(mitre, mitreLine, Wood, 0.018f, Vector3.zero, Quaternion.identity, Vector3.one);
                    Smooth(ball, Wood, 0.014f, new Vector3(0f, 2.03f, 0f), Quaternion.identity, Vector3.one * .18f);
                    Smooth(kit.RoundBox(new Vector3(.055f, .32f, .16f), 0.02f), WoodDark, 0.008f, new Vector3(.13f, 1.72f, .18f), Quaternion.Euler(0f, 28f, -35f), Vector3.one);
                    Face(list, new Vector3(0f, 1.43f, 0f), .36f, .13f, .02f, .22f, -.1f, .9f, false, Base);
                    break;
                }
                case PieceKind.Knight:
                {
                    // The horse's head: the menu figure's side view, made thick, turned so the snout points ahead.
                    Quaternion turn = Quaternion.Euler(0f, -90f, 0f);
                    Vector3 H(float x, float y, float z) => turn * new Vector3(x, y, z);
                    var (horse, horseLine) = ExtrudeMesh(new[] { V(-.28f, 0f), V(-.31f, .34f), V(-.27f, .6f), V(-.17f, .8f), V(-.11f, .96f), V(-.05f, 1.08f), V(.02f, .92f), V(.14f, .86f), V(.4f, .6f), V(.5f, .47f), V(.47f, .34f), V(.34f, .31f), V(.21f, .35f), V(.12f, .22f), V(.2f, 0f) }, .42f);
                    Add(horse, horseLine, Wood, 0.018f, H(.04f, 1.14f, 0f), turn, Vector3.one);
                    Smooth(kit.RoundBox(new Vector3(.1f, .62f, .18f), 0.04f), WoodDark, 0.014f, H(-.28f, 1.66f, 0f), turn * Quaternion.Euler(0f, 0f, -16f), Vector3.one);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Smooth(ball, EyeBlack, 0.004f, H(.19f, 1.86f, s * .23f), turn * Quaternion.LookRotation(new Vector3(.35f, 0f, s)), new Vector3(.17f, .23f, .08f));
                        Smooth(ball, Shine, 0f, H(.22f, 1.91f, s * .265f), Quaternion.identity, Vector3.one * .052f);
                        Smooth(ball, Blush, 0.003f, H(.27f, 1.66f, s * .222f), turn * Quaternion.LookRotation(new Vector3(0f, 0f, s)), new Vector3(.18f, .12f, .02f));
                        Smooth(ball, EyeBlack, 0.003f, H(.55f, 1.59f, s * .09f), Quaternion.identity, Vector3.one * .06f);
                    }
                    break;
                }
                default:
                {
                    Smooth(ball, Wood, 0.018f, new Vector3(0f, 1.55f, 0f), Quaternion.identity, Vector3.one * .84f);
                    Face(list, new Vector3(0f, 1.55f, 0f), .42f, .15f, .04f, .25f, -.11f, 1f, false, Base);
                    break;
                }
            }
            statues[kind] = list;
            return list;
        }

        /// <summary>Two eyes with a shine and two pink cheeks on the front (+z) of a round or upright round head (the menu
        /// figure's face).</summary>
        void Face(List<StatuePart> list, Vector3 center, float r, float ex, float ey, float bx, float by, float size, bool cylinder, float lower)
        {
            var ball = SmoothSphere();
            void Smooth(Kit.Palette c, float ink, Vector3 at, Quaternion rot, Vector3 scale) =>
                list.Add(new StatuePart { fill = ball, line = ball, colors = c, ink = ink, at = at - Vector3.up * lower, rot = rot, scale = scale });
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 n = new Vector3(s * ex / r, ey / r, 1f).normalized;
                Vector3 p = center + n * r * .985f;
                if (cylinder)
                {
                    float z = Mathf.Sqrt(Mathf.Max(0f, r * r - ex * ex));
                    p = center + new Vector3(s * ex, ey, z);
                    n = new Vector3(s * ex, 0f, z).normalized;
                }
                Smooth(EyeBlack, 0.004f, p, Quaternion.LookRotation(n), new Vector3(.136f, .2f, .07f) * size);
                Smooth(Shine, 0f, p + new Vector3(s * .01f + .018f, .04f * size, .03f), Quaternion.identity, Vector3.one * .048f * size);
                Vector3 bn = new Vector3(s * bx / r, by / r, 1f).normalized;
                Vector3 bp = center + bn * r * 1.004f;
                if (cylinder)
                {
                    float z = Mathf.Sqrt(Mathf.Max(0f, r * r - bx * bx));
                    bp = center + new Vector3(s * bx, by, z + .004f);
                    bn = new Vector3(s * bx, 0f, z).normalized;
                }
                Smooth(Blush, 0.003f, bp, Quaternion.LookRotation(bn), new Vector3(.175f, .12f, .02f) * size);
            }
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        Mesh smoothSphere;

        /// <summary>A smooth ball 1 across (eyes, cheeks, mittens, the pawn's head).</summary>
        Mesh SmoothSphere()
        {
            if (smoothSphere != null) return smoothSphere;
            var profile = new Vector2[13];
            for (int i = 0; i <= 12; i++)
            {
                float t = Mathf.PI * i / 12f;
                profile[i] = new Vector2(Mathf.Sin(t) * 0.5f, -Mathf.Cos(t) * 0.5f);
            }
            smoothSphere = LatheMesh(profile, 20, false).fill;
            return smoothSphere;
        }

        /// <summary>A profile (radius, height; bottom to top, outside) turned round the vertical, welded so its normals are
        /// smooth (the ink rim's copy); and, if <paramref name="faceted"/>, a copy with every face flat (the painted one).</summary>
        (Mesh fill, Mesh line) LatheMesh(Vector2[] profile, int segments, bool faceted)
        {
            var v = new List<Vector3>();
            var rings = new List<int>();   // first vertex of each ring; a ring on the axis is one vertex
            foreach (var p in profile)
            {
                rings.Add(v.Count);
                if (p.x < 1e-5f) { v.Add(new Vector3(0f, p.y, 0f)); continue; }
                for (int k = 0; k < segments; k++)
                {
                    float a = Mathf.PI * 2f * k / segments;
                    v.Add(new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x));
                }
            }
            var t = new List<int>();
            for (int i = 0; i < profile.Length - 1; i++)
            {
                bool poleA = profile[i].x < 1e-5f, poleB = profile[i + 1].x < 1e-5f;
                if (poleA && poleB) continue;
                for (int k = 0; k < segments; k++)
                {
                    int k1 = (k + 1) % segments;
                    int a0 = poleA ? rings[i] : rings[i] + k, a1 = poleA ? rings[i] : rings[i] + k1;
                    int b0 = poleB ? rings[i + 1] : rings[i + 1] + k, b1 = poleB ? rings[i + 1] : rings[i + 1] + k1;
                    if (!poleA) { t.Add(a0); t.Add(b0); t.Add(a1); }
                    if (!poleB) { t.Add(a1); t.Add(b0); t.Add(b1); }
                }
            }
            return Meshes(v, t, faceted);
        }

        /// <summary>A side view (x ahead, y up) made <paramref name="depth"/> thick along z, both ways (see <see cref="LatheMesh"/>).</summary>
        (Mesh fill, Mesh line) ExtrudeMesh(Vector2[] outline, float depth)
        {
            var pts = new List<Vector2>(outline);
            float area = 0f;
            for (int i = 0; i < pts.Count; i++) area += pts[i].x * pts[(i + 1) % pts.Count].y - pts[(i + 1) % pts.Count].x * pts[i].y;
            if (area < 0f) pts.Reverse();   // counter-clockwise
            int n = pts.Count;
            var v = new List<Vector3>();
            foreach (var p in pts) v.Add(new Vector3(p.x, p.y, depth * 0.5f));
            foreach (var p in pts) v.Add(new Vector3(p.x, p.y, -depth * 0.5f));
            var t = new List<int>();
            var caps = EarClip(pts);
            for (int i = 0; i < caps.Count; i += 3)
            {
                t.Add(caps[i]); t.Add(caps[i + 1]); t.Add(caps[i + 2]);               // front (+z)
                t.Add(n + caps[i]); t.Add(n + caps[i + 2]); t.Add(n + caps[i + 1]);   // back (−z)
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                t.Add(i); t.Add(n + i); t.Add(j);
                t.Add(j); t.Add(n + i); t.Add(n + j);
            }
            return Meshes(v, t, true);
        }

        /// <summary>Ear clipping of a simple counter-clockwise polygon.</summary>
        static List<int> EarClip(List<Vector2> poly)
        {
            var idx = new List<int>();
            for (int i = 0; i < poly.Count; i++) idx.Add(i);
            var tris = new List<int>();
            for (int guard = 0; idx.Count > 3 && guard < 1000; guard++)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count && !clipped; i++)
                {
                    int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 0f) continue;   // reflex
                    bool blocked = false;
                    foreach (int j in idx)
                    {
                        if (j == ia || j == ib || j == ic) continue;
                        if (InTriangle(poly[j], a, b, c)) { blocked = true; break; }
                    }
                    if (blocked) continue;
                    tris.Add(ia); tris.Add(ib); tris.Add(ic);
                    idx.RemoveAt(i);
                    clipped = true;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]); }
            return tris;
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            float d2 = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
            float d3 = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        /// <summary>The welded mesh with smooth normals, and (if <paramref name="faceted"/>) a copy with each triangle on its
        /// own corners (flat faces); kept until the effects go.</summary>
        (Mesh fill, Mesh line) Meshes(List<Vector3> v, List<int> t, bool faceted)
        {
            var line = new Mesh { name = "Statue part (smooth)", hideFlags = HideFlags.HideAndDontSave };
            line.SetVertices(v);
            line.SetTriangles(t, 0);
            line.RecalculateNormals();
            line.RecalculateBounds();
            statueMeshes.Add(line);
            if (!faceted) return (line, line);
            var fv = new List<Vector3>(t.Count);
            var ft = new List<int>(t.Count);
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-12f) continue;
                ft.Add(fv.Count); fv.Add(a);
                ft.Add(fv.Count); fv.Add(b);
                ft.Add(fv.Count); fv.Add(c);
            }
            var fill = new Mesh { name = "Statue part (faceted)", hideFlags = HideFlags.HideAndDontSave };
            fill.SetVertices(fv);
            fill.SetTriangles(ft, 0);
            fill.RecalculateNormals();
            fill.RecalculateBounds();
            statueMeshes.Add(fill);
            return (fill, line);
        }

        void DestroyStatues()
        {
            foreach (var m in statueMeshes) if (m != null) Destroy(m);
            statueMeshes.Clear();
            statues.Clear();
            smoothSphere = null;
        }
    }
}
