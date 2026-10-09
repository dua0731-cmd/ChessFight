using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The rook's pieces (R102, 승규 님 10-10 with the photo of the team's four piece characters: "벽돌 탑보다는 저 사진에서
    /// 발 다리만 뺀 체스탑이 올라 왔으면 좋겠어, 올라오는 순서는 스킬을 사용할 때마다 무작위"): as the wave reaches each square a
    /// stone tower in the shape of a pawn, a rook, a knight or a bishop bursts up out of the floor, hops past its height,
    /// holds and sinks back. Which shape comes up where is shuffled anew for every slam. Then (승규 님: "사진 참고만,
    /// 너무 캐릭터 느낌, 돌탑이나 성벽탑 같은 느낌"): the photo gives only the shapes; they are carved stone, a stepped
    /// plinth with mortar rings, the rook's orange band, no faces and no arms, a bit taller than a fighter and a half.
    /// Painted the design A way: three hard bands from the sun and an ink rim (the rim drawn from a smooth copy of each
    /// part so it has no cracks where the facets meet).
    /// </summary>
    public partial class SwordFightSkillFx
    {
        static readonly PieceKind[] StatueKinds = { PieceKind.Pawn, PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop };
        /// <summary>The statues stand this big next to the menu figures (about 1.4 m tall: a tower over the fighters).</summary>
        const float StatueScale = 0.75f;

        static readonly Kit.Palette StoneLight = new Kit.Palette("#CFC7B8", "#EEEAE0", "#8F877A", "#2B2620");
        static readonly Kit.Palette StoneDark = new Kit.Palette("#8F877A", "#B7AE9F", "#6F6658", "#2B2620");

        /// <summary>One part of a statue: its painted mesh, the smooth copy its ink rim is drawn from, where it sits (in
        /// the menu figure's metres, the foot of its base at 0).</summary>
        struct StatuePart
        {
            public Mesh fill, line;
            public Kit.Palette colors;
            public float ink;
            public Vector3 at, scale;
            public Quaternion rot;
        }

        readonly Dictionary<PieceKind, List<StatuePart>> statues = new Dictionary<PieceKind, List<StatuePart>>();
        readonly List<Mesh> statueMeshes = new List<Mesh>();

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

        /// <summary>A stone tower in a piece's shape bursting up out of the floor at <paramref name="c"/>, facing back up the lane
        /// (give or take): up past its height in four frames, a squash as it lands, held, and
        /// down into the floor again; the square under it flashes, dust and floor chips fly.</summary>
        void Statue(PieceKind kind, Vector3 c, Vector3 dir, RagdollPawn caster)
        {
            if (!FloorAt(c, out Vector3 floor)) return;   // no floor there: nothing comes up
            var parts = StatueParts(kind);
            var root = new GameObject($"Rook statue ({kind})").transform;
            root.SetParent(kit.root, false);
            var props = new List<Kit.Prop>();
            foreach (var part in parts)
            {
                var p = new Kit.Prop(kit, part.fill, part.colors, "Statue part", root) { inkWidth = part.ink };
                p.t.GetChild(0).GetComponent<MeshFilter>().sharedMesh = part.line;
                p.t.localPosition = part.at;
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
                // frame 20 to 36, rocking a little while it stands.
                float up = a < 4f ? Kit.EaseOut(a / 4f) * 1.12f : a < 7f ? Mathf.Lerp(1.12f, 1f, (a - 4f) / 3f) : a < 20f ? 1f : 1f - Kit.EaseIn((a - 20f) / 16f);
                float stretch = a < 4f ? 1f - a / 4f : 0f, squash = a >= 4f && a < 8f ? Mathf.Sin(Mathf.PI * (a - 4f) / 4f) : 0f;
                float sy = 1f + 0.16f * stretch - 0.12f * squash, sxz = 1f - 0.07f * stretch + 0.07f * squash;
                float lean = a >= 7f && a < 20f ? Mathf.Sin(a * 0.45f + wobble) * 3f * (1f - (a - 7f) / 13f) : 0f;
                root.SetPositionAndRotation(floor + Vector3.up * (H * (up - 1f) - 0.02f), yaw * Quaternion.Euler(0f, 0f, lean));
                root.localScale = new Vector3(sxz, sy, sxz) * StatueScale;
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
            void Add(Mesh fill, Mesh line, Kit.Palette colors, float ink, Vector3 at, Quaternion rot, Vector3 scale)
            {
                list.Add(new StatuePart { fill = fill, line = line, colors = colors, ink = ink, at = at - Vector3.up * Base, rot = rot, scale = scale });
            }
            void Smooth(Mesh m, Kit.Palette colors, float ink, Vector3 at, Quaternion rot, Vector3 scale) => Add(m, m, colors, ink, at, rot, scale);

            // Carved stone, not a character (승규 님: the photo is only for the shapes): a stepped plinth with mortar
            // rings, the rook's orange band, and the piece's head on it; no face, no arms.
            var (plinth, plinthLine) = LatheMesh(new[] { V(0f, .16f), V(.6f, .16f), V(.6f, .3f), V(.52f, .3f), V(.5f, .44f), V(.42f, .48f), V(.38f, .58f), V(.33f, .76f), V(.3f, .94f), V(.28f, 1.03f), V(0f, 1.03f) }, 12, true);
            Add(plinth, plinthLine, StoneLight, 0.02f, Vector3.zero, Quaternion.identity, Vector3.one);
            void Mortar(float y, float r) { var (m, ml) = LatheMesh(new[] { V(0f, -.018f), V(r, -.018f), V(r, .018f), V(0f, .018f) }, 12, true); Add(m, ml, Kit.Stone, 0.012f, new Vector3(0f, y, 0f), Quaternion.identity, Vector3.one); }
            Mortar(.3f, .615f);
            Mortar(.62f, .385f);
            Mortar(.86f, .325f);
            var (band, bandLine) = LatheMesh(new[] { V(0f, -.045f), V(.34f, -.045f), V(.34f, .045f), V(0f, .045f) }, 12, true);
            Add(band, bandLine, Kit.Rook, 0.016f, new Vector3(0f, 1.075f, 0f), Quaternion.identity, Vector3.one);
            var (disc, discLine) = LatheMesh(new[] { V(0f, -.04f), V(.38f, -.04f), V(.38f, .04f), V(0f, .04f) }, 12, true);
            Add(disc, discLine, StoneLight, 0.016f, new Vector3(0f, 1.155f, 0f), Quaternion.identity, Vector3.one);
            var ball = SmoothSphere();

            switch (kind)
            {
                case PieceKind.Rook:
                {
                    var (tower, towerLine) = LatheMesh(new[] { V(0f, -.36f), V(.44f, -.36f), V(.42f, .36f), V(0f, .36f) }, 12, true);
                    Add(tower, towerLine, StoneLight, 0.02f, new Vector3(0f, 1.55f, 0f), Quaternion.identity, Vector3.one);
                    Mortar(1.42f, .45f);
                    Mortar(1.68f, .44f);
                    var merlon = kit.RoundBox(new Vector3(.24f, .2f, .16f), 0.025f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f + Mathf.PI / 6f;
                        Smooth(merlon, StoneLight, 0.016f, new Vector3(Mathf.Sin(a) * .33f, 2.0f, Mathf.Cos(a) * .33f), Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), Vector3.one);
                    }
                    break;
                }
                case PieceKind.Bishop:
                {
                    var (mitre, mitreLine) = LatheMesh(new[] { V(0f, 1.15f), V(.3f, 1.17f), V(.38f, 1.32f), V(.4f, 1.48f), V(.36f, 1.66f), V(.25f, 1.85f), V(.1f, 1.98f), V(0f, 2.01f) }, 12, true);
                    Add(mitre, mitreLine, StoneLight, 0.02f, Vector3.zero, Quaternion.identity, Vector3.one);
                    Smooth(ball, StoneLight, 0.014f, new Vector3(0f, 2.08f, 0f), Quaternion.identity, Vector3.one * .2f);
                    Smooth(kit.RoundBox(new Vector3(.06f, .36f, .2f), 0.02f), StoneDark, 0.008f, new Vector3(.14f, 1.74f, .2f), Quaternion.Euler(0f, 28f, -35f), Vector3.one);
                    break;
                }
                case PieceKind.Knight:
                {
                    // The horse's head: a side view cut out of stone, turned so the snout points ahead.
                    Quaternion turn = Quaternion.Euler(0f, -90f, 0f);
                    Vector3 H(float x, float y, float z) => turn * new Vector3(x, y, z);
                    var (horse, horseLine) = ExtrudeMesh(new[] { V(-.3f, 0f), V(-.33f, .34f), V(-.29f, .6f), V(-.19f, .8f), V(-.12f, .96f), V(-.05f, 1.08f), V(.02f, .92f), V(.14f, .86f), V(.42f, .6f), V(.52f, .47f), V(.49f, .34f), V(.35f, .31f), V(.22f, .35f), V(.13f, .22f), V(.22f, 0f) }, .46f);
                    Add(horse, horseLine, StoneLight, 0.02f, H(.04f, 1.16f, 0f), turn, Vector3.one);
                    Smooth(kit.RoundBox(new Vector3(.12f, .62f, .2f), 0.04f), StoneDark, 0.014f, H(-.3f, 1.68f, 0f), turn * Quaternion.Euler(0f, 0f, -16f), Vector3.one);
                    break;
                }
                default:
                    Smooth(ball, StoneLight, 0.02f, new Vector3(0f, 1.58f, 0f), Quaternion.identity, Vector3.one * .86f);
                    break;
            }
            statues[kind] = list;
            return list;
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        Mesh smoothSphere;

        /// <summary>A smooth ball 1 across (the pawn's head, the bishop's knob).</summary>
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
