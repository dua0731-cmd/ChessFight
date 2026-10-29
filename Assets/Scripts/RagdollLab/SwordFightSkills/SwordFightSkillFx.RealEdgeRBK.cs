using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The rook's, the bishop's and the knight's edge skills for real (10-25, 승규 님: "룩 E 성벽 붕괴: 판 나무가 결 따라
    /// 쪼개지고 놋쇠 테가 휘며 떨어짐 / 비숍 E 구원의 손: 갈고리 지팡이로 걸어 당기기, 또는 던진 밧줄로 끌어올리기 / 나이트 E 벼랑
    /// 끝 역전: 가장자리 놋쇠에 편자가 긁히는 불꽃과 차는 순간의 충격"), as few lines drawn as can be:
    /// - the rook's chunk splits off along the wood's grain (parallel splits running in from the lip, the seams round it
    ///   opening), long splinters fly as it goes, and the brass along its edge buckles and falls away on its own;
    /// - the bishop throws a rope (a real three-strand hemp rope, lit as the board is) that loops round the falling ally's
    ///   waist, is pulled taut as he hauls them back, and goes slack when they are set down;
    /// - the knight's hooves scrape the brass lip as it springs off (sparks and their light), and its kick lands with a
    ///   flash at the feet, the air knocked on and a heavy hold.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        // ---------------------------------------------------------------- the rook's collapse

        /// <summary>The chunk he could break, while he can: a faint outline of light round it (it was an orange square).</summary>
        void RealChunkPreview(SwordFightSkills s)
        {
            var owner = s;
            kit.Keep(s, "rook real chunk aim", () =>
            {
                var fx = new Kit.Fx();
                fx.strips.Add(LineStrip());
                var pts = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    var floor = SwordFightEdgeFloor.Current;
                    if (owner == null || floor == null || !kit.Held(x)) return false;
                    var r = owner.EdgeChunk;
                    float y = floor.Top + 0.015f;
                    pts.Clear();
                    Vector3 a = new Vector3(r.xMin, y, r.yMin), b = new Vector3(r.xMax, y, r.yMin), c = new Vector3(r.xMax, y, r.yMax), dd = new Vector3(r.xMin, y, r.yMax);
                    foreach (var p in new[] { a, b, c, dd, a }) pts.Add(p);
                    // 10-27 (승규 님: "흰색이 좀 더 뚜껍게 잘 보였으면"): a thick, plain white line.
                    x.strips[0].Build(pts, j => 0.06f, kit.Eye, true);
                    x.strips[0].opacity = 0.8f + 0.15f * Mathf.Sin(kit.Clock * 5f);
                    x.strips[0].Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>
        /// The chunk cracking off: the wood splitting along its grain (straight splits running in from the lip across the
        /// chunk, a little apart, wandering only slightly) and its seams opening round its three inner sides, all dark, as
        /// the crack runs; a little grit shaken out of the seams. They lie open while it goes and fade when it is back.
        /// </summary>
        void RealCollapseMarks(SwordFightEdgeFloor.Collapse c)
        {
            var floor = SwordFightEdgeFloor.Current;
            if (floor == null || c.by == null || c.phase != SwordFightEdgeFloor.Phase.Crack) return;
            kit.Keep(c, "real grain", () =>
            {
                var holder = new Kit.Fx();
                var r = c.rect;
                Vector3 o = c.outward, t = c.along;
                bool alongX = Mathf.Abs(o.x) > 0.5f;
                float depth = alongX ? r.width : r.height, width = alongX ? r.height : r.width;
                Vector3 centre = new Vector3(r.center.x, floor.Top, r.center.y);
                Vector3 lipMid = centre + o * (depth * 0.5f), back = centre - o * (depth * 0.5f);
                var grain = new List<List<Vector3>>();
                // Splits along the grain: straight in from the lip (the grain runs in from the edge), a few across it.
                for (int i = 0; i < 6; i++)
                {
                    Vector3 from = lipMid + t * Mathf.Lerp(-0.45f, 0.45f, (i + Random.Range(0.2f, 0.8f)) / 6f) * width;
                    grain.Add(CrackPath(from, -o, depth * Random.Range(0.55f, 0.95f), 0.006f));
                }
                var seams = new List<List<Vector3>>();
                Vector3 a = lipMid - t * (width * 0.5f), b = back - t * (width * 0.5f), cc = back + t * (width * 0.5f), dd = lipMid + t * (width * 0.5f);
                foreach (var (p0, p1) in new[] { (a, b), (b, cc), (cc, dd) }) seams.Add(CrackPath(p0, p1 - p0, (p1 - p0).magnitude, 0.012f));
                float crack = Mathf.Max(0.05f, c.crack);
                // Only while it cracks: once it falls they went with it (10-27: they were left lying over the hole).
                bool Holding(float age) => c != null && c.phase == SwordFightEdgeFloor.Phase.Crack;
                // 10-27 (승규 님: "스크래치 지워주고"): no lines drawn on it any more - where the arena's squares break up their
                // own pieces part as it shakes (BreakUp); the drawn splits stay only for a floor with no squares.
                if (!shardSet.Contains(c))
                {
                    Cracks(grain, age => Mathf.InverseLerp(0f, 0.6f, age / crack), Holding, 0.6f, 0.016f);
                    Cracks(seams, age => Mathf.InverseLerp(0.25f, 0.9f, age / crack), Holding, 0.6f, 0.024f);
                }
                holder.step = (x, d) => kit.Held(x);
                return holder;
            });
            if (((int)(kit.Clock / F) % 7) == 0 && c.age / Mathf.Max(0.05f, c.crack) > 0.2f)
            {
                Vector3 seam = SeamPoint(c, floor.Top, Random.value);
                var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
                Color wood = FloorWood(seam);
                chipA = ChipsOf(wood); chipB = ChipsOf(wood, 0.7f); chipShare = 0.6f;
                WallChips(seam + Vector3.up * 0.02f, Vector3.up, 1, 0.8f, 0.6f, 0.006f, 0.014f);
                chipA = keepA; chipB = keepB; chipShare = keepShare;
            }
        }

        /// <summary>Long thin splinters of the board's wood torn off along a seam (lit as the board is, no ink), tumbling.</summary>
        void Splinters(Vector3 at, Vector3 dir, int n, Color wood, float speed)
        {
            var pal = ChipsOf(wood);
            for (int i = 0; i < n; i++)
            {
                var mesh = kit.RoundBox(new Vector3(1f, 0.12f, 0.16f), 0.03f);
                var sp = new Kit.Prop(kit, mesh, Random.value < 0.6f ? pal : ChipsOf(wood, 0.75f), "Splinter", null, ChipMaterial()) { inkWidth = 0f };
                float len = Random.Range(0.08f, 0.24f);
                Vector3 v = (dir + Random.insideUnitSphere * 0.7f).normalized * speed * Random.Range(0.5f, 1.1f);
                Vector3 p = at;
                Quaternion rot = Random.rotation;
                Vector3 spin = Random.onUnitSphere * Random.Range(300f, 800f);
                var f = kit.Run(Random.Range(1f, 1.5f), (fx, d) =>
                {
                    v += Physics.gravity * d;
                    p += v * d;
                    rot = Quaternion.AngleAxis(spin.magnitude * d, spin.normalized) * rot;
                    float fade = fx.age > fx.life - 0.25f ? (fx.age - (fx.life - 0.25f)) / 0.25f : 0f;
                    sp.t.SetPositionAndRotation(p, rot);
                    sp.t.localScale = Vector3.one * (len * (1f - fade));
                    sp.Apply();
                    return true;
                });
                f.props.Add(sp);
            }
        }

        /// <summary>A brass bar <paramref name="length"/> long along x, bowed <paramref name="bow"/> out (z) in its middle and
        /// twisted a little, its section the trim's (6 cm thick, 0.62 m tall, y down from its top).</summary>
        static void BentBar(Mesh mesh, float length, float bow, float lift, float twist)
        {
            const float Thick = 0.06f, Tall = 0.62f;
            const int N = 14;
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N, x = (u - 0.5f) * length;
                float b = Mathf.Sin(Mathf.PI * u);
                float outz = bow * b, up = lift * b * b, lean = Mathf.Sin(twist * b * Mathf.Deg2Rad) * Tall;
                v.Add(new Vector3(x, -Tall + up * 0.5f, -Thick * 0.5f + outz * 0.5f));
                v.Add(new Vector3(x, up, -Thick * 0.5f + outz + lean));
                v.Add(new Vector3(x, up, Thick * 0.5f + outz + lean));
                v.Add(new Vector3(x, -Tall + up * 0.5f, Thick * 0.5f + outz * 0.5f));
            }
            for (int i = 0; i < N; i++)
                for (int f = 0; f < 4; f++)
                {
                    int a0 = i * 4 + f, a1 = i * 4 + (f + 1) % 4, b0 = a0 + 4, b1 = a1 + 4;
                    t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                }
            t.AddRange(new[] { 0, 2, 1, 0, 3, 2 });
            int e = N * 4;
            t.AddRange(new[] { e, e + 1, e + 2, e, e + 2, e + 3 });
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// The chunk goes: long splinters torn off along its seams, the beat (a hold, the slow moment, a heavy shake); on the
        /// study board its brass is not carried down straight with it but buckles off the edge and falls on its own, bowing
        /// out and twisting as it goes (the squares' own brass is hidden while it is away and back when the chunk rises;
        /// R191: on the kit's board the walnut frame is thrown whole instead, FallingFrame).
        /// It comes back up: a little grit off the seams. It is whole again: nothing drawn.
        /// </summary>
        void RealOnFloor(SwordFightEdgeFloor.Collapse c)
        {
            var floor = SwordFightEdgeFloor.Current;
            float top = floor != null ? floor.Top : 0f;
            var rook = c.by != null ? c.by.Pawn : null;
            var arena = FindFirstObjectByType<SwordFightArena>();
            switch (c.phase)
            {
                case SwordFightEdgeFloor.Phase.Crack:
                    BreakUp(c, top);
                    break;
                case SwordFightEdgeFloor.Phase.Open:
                {
                    // The crack line the blow left lies on the chunk: it goes with it.
                    if (c.by != null && rookBreakFx.TryGetValue(c.by, out var crackFx)) { if (crackFx != null) Destroy(crackFx); rookBreakFx.Remove(c.by); }
                    if (shardSet.Contains(c)) ShowChunk(c, false);   // its pieces fall on their own
                    else ShatterChunk(c, top);
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 p = SeamPoint(c, top, (i + 0.5f) / 8f);
                        Splinters(p + Vector3.up * 0.03f, Vector3.up - c.outward * 0.2f, 2, FloorWood(p), 3f);
                    }
                    if (arena != null && arena.Brass != null)
                        foreach (var (at, along, length) in arena.BrassIn(c.rect, true)) FallingBrass(arena, at, along, c.outward, length);
                    HitStop(4);
                    Shake(rook, null, 0.26f, 16);
                    Beat(rook, null, 8f);
                    break;
                }
                case SwordFightEdgeFloor.Phase.Rise:
                    shardSet.Remove(c);
                    if (arena != null) { arena.BrassIn(c.rect, false); arena.SquaresIn(c.rect, false); }
                    ShowChunk(c, true);
                    break;
                case SwordFightEdgeFloor.Phase.Done:
                    shardSet.Remove(c);
                    if (arena != null) { arena.BrassIn(c.rect, false); arena.SquaresIn(c.rect, false); }
                    ShowChunk(c, true);
                    Shake(rook, null, 0.06f, 5);
                    break;
            }
        }

        /// <summary>The E_Break crack line each rook's blow left, to take away when its chunk falls.</summary>
        readonly Dictionary<SwordFightSkills, GameObject> rookBreakFx = new Dictionary<SwordFightSkills, GameObject>();

        /// <summary>The chunk's own picture on or off (off while its pieces fall; the floor moves it, this only hides it).</summary>
        static void ShowChunk(SwordFightEdgeFloor.Collapse c, bool on)
        {
            if (c == null || c.chunk == null) return;
            foreach (var r in c.chunk.GetComponentsInChildren<Renderer>(true)) r.enabled = on;
        }

        /// <summary>
        /// 10-27 (승규 님: "블록이 쪼개지면서 땅에 꺼지게", then "실제 체스판 블록이 쪼개지면서"): as it gives way the chunk itself
        /// breaks up - its picture goes and in its place the same board, cut into uneven blocks along splits that do not
        /// line up (each block drawn as the floor is where it was: its squares, its edges, its sides), which part and drop,
        /// the ones at the lip first, opening a little before they fall, turning and spreading as they go, gone below.
        /// </summary>
        /// <summary>The collapses whose squares are already broken into pieces (BreakUp).</summary>
        readonly HashSet<SwordFightEdgeFloor.Collapse> shardSet = new HashSet<SwordFightEdgeFloor.Collapse>();

        /// <summary>
        /// 10-27 (승규 님: "무작위로 해주고 땅이 흔들릴 때 갈라지는 조짐이 보여야 돼"): the moment the chunk starts to shake its
        /// squares are swapped for the same squares already broken - each cut at random into a handful of uneven shards
        /// (a Voronoi of scattered points, so no two breaks alike), lying tight together in its own wood. As it shakes the
        /// cracks between them open (they spread and settle a little, each tilting its own way, trembling more and more),
        /// so the break shows before it goes; when it gives way they drop, the lip's first, tipping and spreading, gone
        /// below. If it is called off they are taken away and the squares are whole again.
        /// </summary>
        void BreakUp(SwordFightEdgeFloor.Collapse c, float top)
        {
            if (shardSet.Contains(c)) return;
            var arena = FindFirstObjectByType<SwordFightArena>();
            var squares = arena != null ? arena.SquaresIn(c.rect, true) : null;
            if (squares == null || squares.Count == 0) return;
            shardSet.Add(c);
            Vector3 o = c.outward, t = c.along;
            bool alongX = Mathf.Abs(o.x) > 0.5f;
            float chunkDepth = alongX ? c.rect.width : c.rect.height;
            Vector3 chunkLip = new Vector3(c.rect.center.x, top, c.rect.center.y) + o * (chunkDepth * 0.5f);
            foreach (var (area, wood, thick) in squares)
            {
                Vector2 mid = area.center;
                foreach (var poly in VoronoiCells(area, UnityEngine.Random.Range(6, 11)))
                {
                    // A hair in from its neighbours so the seams are there (shut), then they open.
                    Vector2 cen = Vector2.zero;
                    foreach (var q in poly) cen += q;
                    cen /= poly.Count;
                    for (int k = 0; k < poly.Count; k++) poly[k] = cen + (poly[k] - cen) * 0.995f - (poly[k] - cen).normalized * 0.004f;
                    var mesh = Prism(poly, cen, thick, area);
                    var go = new GameObject("Board shard", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(kit.root, false);
                    Vector3 at = new Vector3(cen.x, top - thick * 0.5f, cen.y);
                    go.transform.position = at;
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = wood;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    float lateness = Mathf.Clamp01(Vector3.Dot(chunkLip - at, o) / Mathf.Max(0.01f, chunkDepth));
                    // Spreading out from its square's middle (the cracks open between them), a little toward the lip.
                    Vector2 away = cen - mid;
                    Vector3 spread = new Vector3(away.x, 0f, away.y).normalized * Mathf.Min(1f, away.magnitude) + o * (1f - lateness) * 0.4f;
                    Vector3 tiltAxis = Random.onUnitSphere; tiltAxis.y = 0f;
                    if (tiltAxis.sqrMagnitude < 1e-4f) tiltAxis = t;
                    tiltAxis.Normalize();
                    float tilt = Random.Range(1.5f, 4.5f), phase = Random.value * 10f;
                    float delay = lateness * 0.14f + Random.Range(0f, 0.06f);
                    Vector3 v = o * Random.Range(0.2f, 0.6f) * (1f - 0.6f * lateness) + t * Random.Range(-0.25f, 0.25f) + Vector3.down * Random.Range(0.05f, 0.35f);
                    Vector3 spin = (Vector3.Cross(Vector3.up, o) * Random.Range(0.6f, 1f) + t * Random.Range(-0.4f, 0.4f)).normalized * Random.Range(50f, 160f);
                    Vector3 p = at;
                    Quaternion rot = Quaternion.identity;
                    float openAt = -1f;
                    var f = kit.Run(c.crack + 3f, (fx, d) =>
                    {
                        if (go == null) return false;
                        if (c.phase == SwordFightEdgeFloor.Phase.Crack)
                        {
                            // The warning: the cracks between the shards open, the shards settle and tip, trembling more.
                            float k = Mathf.Clamp01(c.age / Mathf.Max(0.05f, c.crack)), kk = k * k;
                            float shake = (0.002f + 0.01f * k) * Mathf.Sin(kit.Clock * 70f + phase);
                            p = at + spread * (0.05f * kk) + Vector3.down * (0.02f * kk) + new Vector3(shake, 0f, -shake);
                            rot = Quaternion.AngleAxis(tilt * kk, tiltAxis);
                        }
                        else if (openAt < 0f && c.phase != SwordFightEdgeFloor.Phase.Open) return false;   // called off: whole again
                        else
                        {
                            if (openAt < 0f) openAt = fx.age;
                            if (fx.age - openAt > delay)
                            {
                                v += Physics.gravity * d;
                                p += v * d;
                                rot = Quaternion.AngleAxis(spin.magnitude * d, spin.normalized) * rot;
                            }
                        }
                        go.transform.SetPositionAndRotation(p, rot);
                        return p.y > SwordFightArena.DeskY + 0.3f;
                    });
                    f.end = () => { if (go != null) Destroy(go); Destroy(mesh); };
                }
            }
        }

        /// <summary>A rectangle cut at random into <paramref name="n"/> convex cells (a Voronoi of points scattered over a
        /// jittered grid, so the cells are uneven but none is a sliver), each a polygon counter-clockwise in x / z.</summary>
        static List<List<Vector2>> VoronoiCells(Rect area, int n)
        {
            int cols = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(n * area.width / Mathf.Max(0.01f, area.height))));
            int rows = Mathf.Max(2, Mathf.CeilToInt(n / (float)cols));
            var sites = new List<Vector2>();
            for (int i = 0; i < cols; i++)
                for (int j = 0; j < rows; j++)
                    sites.Add(new Vector2(area.xMin + area.width * (i + Random.Range(0.15f, 0.85f)) / cols,
                                          area.yMin + area.height * (j + Random.Range(0.15f, 0.85f)) / rows));
            while (sites.Count > n) sites.RemoveAt(Random.Range(0, sites.Count));
            var cells = new List<List<Vector2>>();
            foreach (var a in sites)
            {
                var poly = new List<Vector2> { new Vector2(area.xMin, area.yMin), new Vector2(area.xMax, area.yMin), new Vector2(area.xMax, area.yMax), new Vector2(area.xMin, area.yMax) };
                foreach (var b in sites)
                {
                    if (b == a) continue;
                    // Keep the side nearer a: (x - m) . (b - a) <= 0.
                    poly = ClipHalf(poly, (a + b) * 0.5f, b - a);
                    if (poly.Count < 3) break;
                }
                if (poly.Count >= 3) cells.Add(poly);
            }
            return cells;
        }

        static List<Vector2> ClipHalf(List<Vector2> poly, Vector2 m, Vector2 n)
        {
            var outp = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % poly.Count];
                float dp = Vector2.Dot(p - m, n), dq = Vector2.Dot(q - m, n);
                if (dp <= 0f) outp.Add(p);
                if ((dp < 0f) != (dq < 0f) && Mathf.Abs(dp - dq) > 1e-7f) outp.Add(p + (q - p) * (dp / (dp - dq)));
            }
            return outp;
        }

        /// <summary>A convex polygon (x / z, counter-clockwise) stood up as a block <paramref name="thick"/> deep, about its
        /// middle (<paramref name="cen"/> at its centre's height): flat-shaded top, bottom and sides, its texture laid on as
        /// the whole square's (<paramref name="square"/>) was, so a shard looks just like the part of the square it was
        /// (10-27, 승규 님: "부서질 때 재질이 좀 달라져" - with no texture coordinates its wood had gone flat).</summary>
        static Mesh Prism(List<Vector2> poly, Vector2 cen, float thick, Rect square)
        {
            var v = new List<Vector3>();
            var nrm = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            Vector2 Flat(Vector3 q) => new Vector2((q.x + cen.x - square.xMin) / square.width, (q.z + cen.y - square.yMin) / square.height);
            float h = thick * 0.5f;
            int n = poly.Count;
            Vector3 P(int i, float y) => new Vector3(poly[i].x - cen.x, y, poly[i].y - cen.y);
            // Winding: counter-clockwise seen from above in x / z is clockwise in Unity's left-handed view from +y.
            float area = 0f;
            for (int i = 0; i < n; i++) area += poly[i].x * poly[(i + 1) % n].y - poly[(i + 1) % n].x * poly[i].y;
            bool ccw = area > 0f;
            // Top.
            int b0 = v.Count;
            for (int i = 0; i < n; i++) { v.Add(P(i, h)); nrm.Add(Vector3.up); uv.Add(Flat(P(i, h))); }
            for (int i = 1; i < n - 1; i++) { if (ccw) { tri.Add(b0); tri.Add(b0 + i + 1); tri.Add(b0 + i); } else { tri.Add(b0); tri.Add(b0 + i); tri.Add(b0 + i + 1); } }
            // Bottom.
            b0 = v.Count;
            for (int i = 0; i < n; i++) { v.Add(P(i, -h)); nrm.Add(Vector3.down); uv.Add(Flat(P(i, -h))); }
            for (int i = 1; i < n - 1; i++) { if (ccw) { tri.Add(b0); tri.Add(b0 + i); tri.Add(b0 + i + 1); } else { tri.Add(b0); tri.Add(b0 + i + 1); tri.Add(b0 + i); } }
            // Sides.
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = P(i, h), b = P(j, h), cc = P(j, -h), dd = P(i, -h);
                Vector3 side = Vector3.Cross(b - a, dd - a).normalized;
                Vector3 outward = new Vector3((poly[i].x + poly[j].x) * 0.5f - cen.x, 0f, (poly[i].y + poly[j].y) * 0.5f - cen.y);
                bool flip = Vector3.Dot(side, outward) < 0f;
                int s0 = v.Count;
                v.Add(a); v.Add(b); v.Add(cc); v.Add(dd);
                // Across the side: where along the square's own side it lies (x or z, whichever the face runs along), and up it.
                bool runsX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
                float U(Vector3 q) => runsX ? (q.x + cen.x - square.xMin) / square.width : (q.z + cen.y - square.yMin) / square.height;
                float V(Vector3 q) => (q.y + h) / Mathf.Max(0.01f, thick);
                uv.Add(new Vector2(U(a), V(a))); uv.Add(new Vector2(U(b), V(b))); uv.Add(new Vector2(U(cc), V(cc))); uv.Add(new Vector2(U(dd), V(dd)));
                Vector3 nn = flip ? -side : side;
                for (int k = 0; k < 4; k++) nrm.Add(nn);
                if (!flip) { tri.Add(s0); tri.Add(s0 + 1); tri.Add(s0 + 2); tri.Add(s0); tri.Add(s0 + 2); tri.Add(s0 + 3); }
                else { tri.Add(s0); tri.Add(s0 + 2); tri.Add(s0 + 1); tri.Add(s0); tri.Add(s0 + 3); tri.Add(s0 + 2); }
            }
            var m = new Mesh { name = "Board shard", hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(v);
            m.SetNormals(nrm);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        void ShatterChunk(SwordFightEdgeFloor.Collapse c, float top)
        {
            var floor = SwordFightEdgeFloor.Current;
            if (floor == null || c.chunk == null) return;
            ShowChunk(c, false);
            Vector3 o = c.outward, t = c.along;
            bool alongX = Mathf.Abs(o.x) > 0.5f;
            float thick = Mathf.Max(0.05f, floor.Top - floor.Bottom);
            // On the arena what shows is its squares (maple and walnut blocks over the hidden floor): break those, each in
            // its own wood; elsewhere the floor's own box, drawn as the floor is.
            var arena = FindFirstObjectByType<SwordFightArena>();
            var squares = arena != null ? arena.SquaresIn(c.rect, true) : null;
            if (squares != null && squares.Count > 0)
                foreach (var (area, wood, depthOf) in squares) Shatter(area, depthOf, top, o, t, alongX, c.rect, (at, size) =>
                {
                    var go = new GameObject("Board piece", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(kit.root, false);
                    go.transform.position = at;
                    go.GetComponent<MeshFilter>().sharedMesh = kit.RoundBox(Quant(size), 0.015f);
                    var mr = go.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = wood;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    return (go, (Mesh)null);
                });
            else
                Shatter(c.rect, thick, top, o, t, alongX, c.rect, (at, size) =>
                {
                    var go = floor.FloorPiece(at, size, out var mesh);
                    go.transform.SetParent(kit.root, true);
                    return (go, mesh);
                });
        }

        /// <summary>A size rounded to 5 cm (the kit keeps one mesh per size).</summary>
        static Vector3 Quant(Vector3 v) => new Vector3(Mathf.Max(0.05f, Mathf.Round(v.x / 0.05f) * 0.05f), Mathf.Max(0.05f, Mathf.Round(v.y / 0.05f) * 0.05f), Mathf.Max(0.05f, Mathf.Round(v.z / 0.05f) * 0.05f));

        /// <summary>One block (<paramref name="area"/> its top, <paramref name="thick"/> deep, its top at
        /// <paramref name="top"/>) cut into uneven pieces along splits that do not line up, which part and drop: the ones
        /// nearer the lip of the whole <paramref name="chunk"/> first, opening a little before they fall, tipping over the
        /// way they fall and spreading, gone below. <paramref name="make"/> builds a piece (and the mesh it owns, or null).</summary>
        void Shatter(Rect area, float thick, float top, Vector3 o, Vector3 t, bool alongX, Rect chunk,
                     System.Func<Vector3, Vector3, (GameObject go, Mesh mesh)> make)
        {
            float depth = alongX ? area.width : area.height, width = alongX ? area.height : area.width;
            float chunkDepth = alongX ? chunk.width : chunk.height;
            Vector3 centre = new Vector3(area.center.x, top - thick * 0.5f, area.center.y);
            Vector3 chunkLip = new Vector3(chunk.center.x, top, chunk.center.y) + o * (chunkDepth * 0.5f);
            int nw = Mathf.Clamp(Mathf.RoundToInt(width / 0.7f), 2, 6), nd = Mathf.Clamp(Mathf.RoundToInt(depth / 0.7f), 2, 5);
            float[] Cuts(int n, float len)
            {
                var cuts = new float[n + 1];
                cuts[0] = 0f; cuts[n] = len;
                for (int k = 1; k < n; k++) cuts[k] = len * (k + Random.Range(-0.3f, 0.3f)) / n;
                return cuts;
            }
            var wc = Cuts(nw, width);
            const float Gap = 0.012f;
            for (int i = 0; i < nw; i++)
            {
                // Each strip its own splits across, so no seam runs straight through.
                var dc = Cuts(nd, depth);
                for (int j = 0; j < nd; j++)
                {
                    float w0 = wc[i], w1 = wc[i + 1], d0 = dc[j], d1 = dc[j + 1];
                    float sw = Mathf.Max(0.05f, w1 - w0 - Gap), sd = Mathf.Max(0.05f, d1 - d0 - Gap);
                    Vector3 at = centre + t * ((w0 + w1) * 0.5f - width * 0.5f) + o * (depth * 0.5f - (d0 + d1) * 0.5f);
                    Vector3 size = alongX ? new Vector3(sd, thick, sw) : new Vector3(sw, thick, sd);
                    var (go, mesh) = make(at, size);
                    if (go == null) continue;
                    // How far in from the lip of the whole chunk: the lip goes first, the further in the later and slower.
                    float lateness = Mathf.Clamp01(Vector3.Dot(chunkLip - at, o) / Mathf.Max(0.01f, chunkDepth));
                    float delay = 0.03f + lateness * 0.14f + Random.Range(0f, 0.05f);
                    Vector3 part = (t * ((w0 + w1) * 0.5f / width - 0.5f) + o * (0.5f - lateness) * 0.6f) * 0.25f;
                    Vector3 v = o * Random.Range(0.2f, 0.6f) * (1f - 0.6f * lateness) + t * Random.Range(-0.25f, 0.25f) + Vector3.down * Random.Range(0.05f, 0.35f);
                    // They tip over the way they fall, the lip side down first.
                    Vector3 spin = (Vector3.Cross(Vector3.up, o) * Random.Range(0.6f, 1f) + t * Random.Range(-0.4f, 0.4f)).normalized * Random.Range(50f, 150f);
                    Vector3 p = at;
                    Quaternion rot = Quaternion.identity;
                    var f = kit.Run(2.4f, (fx, d) =>
                    {
                        if (go == null) return false;
                        if (fx.age < delay)
                        {
                            // Parting: the cracks open a few centimetres, a slight sag.
                            float k = fx.age / delay;
                            p = at + part * k * 0.15f + Vector3.down * 0.02f * k;
                        }
                        else
                        {
                            v += Physics.gravity * d;
                            p += v * d;
                            rot = Quaternion.AngleAxis(spin.magnitude * d, spin.normalized) * rot;
                        }
                        go.transform.SetPositionAndRotation(p, rot);
                        return p.y > SwordFightArena.DeskY + 0.3f;
                    });
                    f.end = () => { if (go != null) Destroy(go); if (mesh != null) Destroy(mesh); };
                }
            }
        }

        /// <summary>One length of the edge's brass torn off by the collapse: bowing out and twisting in its first few frames
        /// (wrenched off), then falling and turning over, gone below the board.</summary>
        void FallingBrass(SwordFightArena arena, Vector3 at, Vector3 along, Vector3 outward, float length)
        {
            var loose = arena.LooseFrame(kit.root, length);
            if (loose != null)
            {
                FallingFrame(arena, loose, at, along, outward);
                return;
            }
            var mesh = new Mesh { name = "Falling brass", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            var go = new GameObject("Falling brass", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(kit.root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = arena.Brass;
            // The bar's own frame: x along the edge, z out, y up; its top at the board's top.
            Vector3 top = at + Vector3.up * 0.31f;
            Quaternion rot = Quaternion.LookRotation(outward, Vector3.up);
            if (Vector3.Dot(rot * Vector3.right, along) < 0f) along = -along;
            float bow = Random.Range(0.1f, 0.18f), lift = Random.Range(0.04f, 0.1f), twist = Random.Range(-14f, 14f);
            Vector3 v = outward * Random.Range(0.6f, 1.2f) + Vector3.up * Random.Range(0.4f, 1.2f);
            Vector3 spin = (along * Random.Range(-60f, 60f) + outward * Random.Range(40f, 120f));
            var f = kit.Run(1.8f, (fx, d) =>
            {
                float a = fx.age;
                float bend = Kit.EaseOut(Mathf.Clamp01(a / 0.12f));
                BentBar(mesh, length, bow * bend, lift * bend, twist * bend);
                if (a > 0.06f)
                {
                    v += Physics.gravity * d;
                    top += v * d;
                    rot = Quaternion.AngleAxis(spin.magnitude * d, spin.normalized) * rot;
                }
                go.transform.SetPositionAndRotation(top, rot);
                return top.y > SwordFightArena.DeskY + 0.5f;
            });
            f.end = () => { if (go != null) Destroy(go); Destroy(mesh); };
            HitStop(2);
        }

        /// <summary>R191: on the kit's board the edge is a walnut frame. Wood does not bow as the brass did: a length of it is
        /// wrenched off whole, leaning out in its first few frames with a few splinters where it tore, then falls turning
        /// over, gone below the board (the same throw as the brass).</summary>
        void FallingFrame(SwordFightArena arena, Transform piece, Vector3 at, Vector3 along, Vector3 outward)
        {
            // The piece's own frame: x out, y up, z along; at sits on the edge line at the square's middle height (-0.3),
            // the frame's bottom 0.32 below it.
            Quaternion rot = Quaternion.LookRotation(along, Vector3.up);
            if (Vector3.Dot(rot * Vector3.right, outward) < 0f)
            {
                along = -along;
                rot = Quaternion.LookRotation(along, Vector3.up);
            }
            Vector3 pos = at + outward * SwordFightArena.FrameMiddle.x + Vector3.up * (SwordFightArena.FrameMiddle.y - 0.32f);
            float lean = Random.Range(12f, 22f);
            Vector3 v = outward * Random.Range(0.6f, 1.2f) + Vector3.up * Random.Range(0.4f, 1.2f);
            Vector3 spin = along * Random.Range(-60f, 60f) + outward * Random.Range(40f, 120f);
            Splinters(pos + outward * 0.08f + Vector3.up * 0.3f, outward + Vector3.up, 3, arena.FrameWood, 3f);
            var f = kit.Run(1.8f, (fx, d) =>
            {
                float a = fx.age;
                // Its top tips out round its length (a negative turn round "along" moves +y toward +x).
                var tilt = Quaternion.AngleAxis(-lean * Kit.EaseOut(Mathf.Clamp01(a / 0.12f)), along);
                if (a > 0.06f)
                {
                    v += Physics.gravity * d;
                    pos += v * d;
                    rot = Quaternion.AngleAxis(spin.magnitude * d, spin.normalized) * rot;
                }
                piece.SetPositionAndRotation(pos, tilt * rot);
                return pos.y > SwordFightArena.DeskY + 0.5f;
            });
            f.end = () => { if (piece != null) Destroy(piece.gameObject); };
            HitStop(2);
        }

        /// <summary>The rook's blow on the lip: splinters out of the board where it struck and a short crack, a hold, a shake.</summary>
        void RealRookBreak(SfFxEvent e)
        {
            var s = e.by;
            Vector3 lip = Kit.FloorUnder(e.at - e.dir * 0.3f, 1f);
            var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
            Color wood = FloorWood(lip);
            chipA = ChipsOf(wood); chipB = ChipsOf(wood, 0.7f); chipShare = 0.6f;
            WallChips(lip + Vector3.up * 0.04f, Vector3.up + e.dir * 0.3f, 10, 3f, 0.7f, 0.01f, 0.035f);
            chipA = keepA; chipB = keepB; chipShare = keepShare;
            // The effect's crack line lies where the chunk breaks off, 2 m in from the lip (e.dir points out of the board).
            // 10-27: its drawn crack line is not played any more (승규 님: "스크래치 지워주고"); the chunk's own pieces show it.
            HitStop(4);
            Shake(s.Pawn, null, 0.15f, 9);
            Punch(s.Pawn, null, 6f, 16);
        }

        // ---------------------------------------------------------------- the bishop's rope

        Material hempMat;

        /// <summary>Natural hemp, matte, lit as the board is (the rope's three strands are in its shape).</summary>
        Material HempMaterial()
        {
            if (hempMat != null) return hempMat;
            // A dark, weathered hemp (10-25, 승규 님: "비숍 빛이 너무 강해서 밧줄이 안보여"): the pale straw tan was lost in the
            // lamp's glare on the light squares and the ivory pieces; this reads against both.
            hempMat = LitMaterials.NewLit(new Color(0.42f, 0.3f, 0.17f), 0.08f);
            hempMat.hideFlags = HideFlags.HideAndDontSave;
            hempMat.name = "Hemp rope";
            return hempMat;
        }

        GameObject RopeObject(string name, Mesh mesh)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(kit.root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = HempMaterial();
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = false;   // a thin tube in the match light speckles black with its own shadow
            return go;
        }

        /// <summary>The rope bites in the ally's hand (SwordFightSkillFx.RopeHand.cs): pulled taut with a jolt, a hold, the slow
        /// moment, a shake.</summary>
        void RealBishopCatch(SfFxEvent e)
        {
            var s = e.by;
            HitStop(3);
            Shake(s.Pawn, e.target, 0.09f, 7);
            Beat(s.Pawn, e.target, 8f);
        }

        // ---------------------------------------------------------------- the knight's reversal

        /// <summary>A faint circle of light lying on the board round <paramref name="centre"/>, kept while asked for (the one
        /// line an edge skill draws to say who or where: its target, its landing).</summary>
        void RealCircle(object owner, string key, System.Func<Vector3?> centre, float radius, float opacity = 0.4f)
        {
            kit.Keep(owner, key, () =>
            {
                var fx = new Kit.Fx();
                fx.strips.Add(LineStrip());
                var pts = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    var at = centre();
                    if (at == null || !kit.Held(x)) return false;
                    Vector3 c = Kit.FloorUnder(at.Value + Vector3.up * 0.5f, 1f);
                    pts.Clear();
                    for (int i = 0; i <= 32; i++)
                    {
                        float ang = i / 32f * Mathf.PI * 2f;
                        pts.Add(c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius + Vector3.up * 0.015f);
                    }
                    x.strips[0].Build(pts, j => 0.022f, kit.Eye, true);
                    x.strips[0].opacity = opacity;
                    x.strips[0].Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>Where it will come down, while it gathers itself: a faint small circle of light (it was a horseshoe).</summary>
        void RealKnightEdgeLand(SwordFightSkills s)
        {
            var owner = s;
            RealCircle(s, "knight real edge land", () => owner != null ? owner.EdgeLand : (Vector3?)null, 0.45f);
        }

        /// <summary>
        /// It springs off the edge (10-25: "가장자리 놋쇠에 편자가 긁히는 불꽃"): its shod hooves scrape along the brass lip as
        /// they push off - sparks thrown back and along the brass for a few frames, their warm light, a few flakes of brass -
        /// then see-through copies of it over the somersault, and at the top the beat with the view swinging round it.
        /// </summary>
        void RealKnightFlip(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            if (pawn == null) return;
            var X = EdgeS;
            Vector3 lip = e.at;
            var floor = SwordFightEdgeFloor.Current;
            if (floor != null && floor.Nearest(e.at, out var hit)) lip = hit.point + Vector3.up * (floor.Top - hit.point.y);
            Vector3 back = -Kit.FlatDir(e.dir, Vector3.forward), along = Vector3.Cross(Vector3.up, back);
            // 10-26 (승규 님: "나이트가 회전할 때 불꽃 파티클을 지워주고"): no sparks off the lip as it goes over any more - the
            // bent air round the turn carries it (the brass flakes stay).
            var arena = FindFirstObjectByType<SwordFightArena>();
            if (arena != null && arena.Brass != null)
            {
                var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
                chipA = chipB = ChipsOf(arena.Brass.color); chipShare = 1f;
                WallChips(lip + Vector3.up * 0.02f, back + Vector3.up * 0.6f, 4, 2.2f, 0.5f, 0.006f, 0.014f);
                chipA = keepA; chipB = keepB; chipShare = keepShare;
            }
            Punch(pawn, null, 5f, 12);
            // 10-26: the hind legs' sweep into the kick blurs (the turn over into the buck starts at 0.82 of the flight).
            HoofTrails(s, (X != null ? X.knightFlip : 0.8f) * 0.8f);
            // 10-26 (승규 님: "회전 할 때는 공기 굴절 바람 같은 효과"): as it goes over, the air it tears through bends round
            // it - bursts of bent air left behind its head and its hind legs, the fastest parts of the turn, each thrown
            // back the way that part came, so the wind swirls round the somersault. Only bending what is behind them (almost
            // no haze: on the dark board the white of it read as smoke).
            float flip = X != null ? X.knightFlip : 0.8f;
            Vector3 Feet() => Vector3.Lerp(pawn.bodies[(int)BodyId.FootL].position, pawn.bodies[(int)BodyId.FootR].position, 0.5f);
            Vector3 lastHead = pawn.bodies[(int)BodyId.Head].position, lastFeet = Feet(), lastHips = pawn.Hips.position;
            float nextAir = 0f;
            int airTurn = 0;
            kit.Run(2f, (fx, d) =>
            {
                if (s == null || pawn == null || s.EdgeStage != SfEdge.Active || s.Piece != PieceKind.Knight) return false;
                Vector3 head = pawn.bodies[(int)BodyId.Head].position, feet = Feet();
                float k = s.EdgeTime / Mathf.Max(0.1f, flip);
                // 10-26 (승규 님: "바람 조금 더 길게 … 시각적으로 잘 안보여"): from the throw back off the lip to the buck,
                // each burst lasting twice as long and carrying further, and a faint streaked haze in it so the wind shows.
                bool turning = k >= 0.12f && k <= 0.95f;
                if (turning && fx.age >= nextAir && d > 1e-4f)
                {
                    nextAir = fx.age + 0.022f;
                    Vector3 hips = pawn.Hips.position, vb = (hips - lastHips) / d;
                    // 10-26 (승규 님: "공기 굴절을 살짝 줄여주고 회전할 때 바람도 같이 휘쳐 나가는 걸 조화롭게"): a little
                    // less bend, and the air flung off the turn with it - each burst from the head or the hind legs goes
                    // out from the turn's middle and on round the way that part spins (a swirl whipped off it), not
                    // straight back; head and legs take turns, so the bursts follow one another round the somersault.
                    bool fromHead = (airTurn++ & 1) == 0;
                    Vector3 part = fromHead ? head : feet, v = fromHead ? (head - lastHead) / d : (feet - lastFeet) / d;
                    Vector3 spin = v - vb, outward = part - hips;
                    if (spin.sqrMagnitude > 1f && outward.sqrMagnitude > 1e-4f)
                    {
                        Vector3 way = outward.normalized * 0.65f + spin.normalized * 0.55f - vb.normalized * 0.25f;
                        Air.Kick(part + outward.normalized * 0.08f, way, Mathf.Clamp(spin.magnitude / 4f, 0.8f, 1.5f), null, 0.22f, 1.6f, 2.2f);
                    }
                    // Its middle drags a softer wake behind it.
                    if (vb.sqrMagnitude > 1f && (airTurn % 3) == 0) Air.Kick(hips, -vb, Mathf.Clamp(vb.magnitude / 5f, 0.6f, 1.1f), null, 0.16f, 1.2f, 2.2f);
                }
                lastHead = head;
                lastFeet = feet;
                lastHips = pawn.Hips.position;
                return true;
            });
            float over = (X != null ? X.knightFlip : 0.8f) * SwordFightSkills.FlipOut;
            bool beat = false;
            float next = 0f;
            kit.Run(2f, (fx, d) =>
            {
                if (s == null || s.EdgeStage != SfEdge.Active || s.Piece != PieceKind.Knight) return false;
                if (fx.age >= next)
                {
                    RealGhost(pawn, 0.28f, 0.18f, Vector3.zero);
                    next = fx.age + 2f * F;
                }
                if (!beat && s.EdgeTime >= over)
                {
                    beat = true;
                    Beat(pawn, s.EdgeTarget, 7f);
                    Orbit(pawn, 38f, 1.1f);
                }
                return true;
            });
        }

        /// <summary>The kick as it lands (10-25: "차는 순간의 충격"): a flash where its hooves strike,
        /// splinters off the board under it, a heavy hold, a shake, a kick of the view (no bent air: it read as smoke).</summary>
        void RealKnightKick(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            if (pawn == null) return;
            Vector3 c = Kit.FloorUnder(e.at + Vector3.up * 0.5f, 1f);
            Vector3 feet = Vector3.Lerp(pawn.bodies[(int)BodyId.FootL].position, pawn.bodies[(int)BodyId.FootR].position, 0.5f);
            Vector3 dir = Kit.FlatDir(e.dir, Vector3.forward);
            ClashFlash(feet + dir * 0.25f, 0.8f);
            var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
            Color wood = FloorWood(c);
            chipA = ChipsOf(wood); chipB = ChipsOf(wood, 0.7f); chipShare = 0.6f;
            WallChips(c + Vector3.up * 0.04f, Vector3.up - dir * 0.3f, 9, 3f, 0.7f, 0.01f, 0.035f);
            chipA = keepA; chipB = keepB; chipShare = keepShare;
            HitStop(5);
            Shake(pawn, e.target, 0.16f, 9);
            Punch(pawn, e.target, 7f, 14);
        }

        /// <summary>Whoever the kick takes (10-26, 승규 님: "상대 찰 때 뭔가 사실적인 이펙트", then "편자 자국 말고 VFX로" -
        /// the horseshoe spark burst and the screen's shock): where the hooves really meet it (from the knight's feet, not
        /// its middle), the steel shoes striking - a cone of hot sparks flung on the way it is kicked and a few thrown back
        /// off it, a hot light for a moment - the view kicked in hard with its edges splitting into colour (the lens shock,
        /// <see cref="LensShock"/>), splinters and chips of its wood, a small flash, a longer hold and a bigger shake.</summary>
        void RealKnightKickHit(SfFxEvent e)
        {
            if (e.target == null) return;
            var knight = e.by != null ? e.by.Pawn : null;
            Vector3 from = knight != null
                ? Vector3.Lerp(knight.bodies[(int)BodyId.FootL].position, knight.bodies[(int)BodyId.FootR].position, 0.5f)
                : e.at - e.dir;
            Vector3 at = OnBody(e.target, from, out Vector3 normal);
            Vector3 kick = Kit.FlatDir(e.dir, -normal);
            Vector3 shoe = at + normal * 0.05f;
            RealSparks(shoe, kick + Vector3.up * 0.25f, 30, 6.5f, 0.45f, 1.35f);
            RealSparks(shoe, normal + Vector3.up * 0.6f, 12, 3.2f, 0.9f, 1.2f);
            SparkLight(shoe, 3.2f, 0.14f, 2.6f);
            // 10-26 (승규 님: "색수차를 좀 더 세게"): full strength, held a moment before it dies away, and longer.
            LensShock(knight, e.target, 1f, 0.26f);
            Punch(knight, e.target, 11f, 16);
            Splinters(at + normal * 0.03f, kick + Vector3.up * 0.45f, 9, BodyColor(e.target), 3.4f);
            ClashFlash(at + normal * 0.03f, 0.45f);
            var real = e;
            real.at = at;
            RealHit(real, 6, 0.2f);
        }

#if CF_URP
        GameObject lensHolder;
        UnityEngine.Rendering.Volume lensVolume;
        UnityEngine.Rendering.VolumeProfile lensProfile;
        UnityEngine.Rendering.Universal.ChromaticAberration lensAberration;
#endif
        float lensAmp, lensLeft, lensTotal;

        /// <summary>
        /// A heavy blow's lens shock (10-26, the knight's kick): for <paramref name="seconds"/> the picture's edges split
        /// into colour as a camera's lens does under a blow (URP's chromatic aberration in a global volume of its own), at
        /// once and dying away - on the two pieces' own screens only, as the view's kick is.
        /// </summary>
        void LensShock(RagdollPawn a, RagdollPawn b, float strength, float seconds)
        {
#if CF_URP
            if (!shake || !Sees(a, b)) return;
            if (lensVolume == null)
            {
                lensProfile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
                lensProfile.hideFlags = HideFlags.HideAndDontSave;
                lensAberration = lensProfile.Add<UnityEngine.Rendering.Universal.ChromaticAberration>(true);
                lensHolder = new GameObject("Skill lens shock (volume)");
                lensHolder.transform.SetParent(kit.root, false);
                lensVolume = lensHolder.AddComponent<UnityEngine.Rendering.Volume>();
                lensVolume.isGlobal = true;
                lensVolume.priority = 120f;
                lensVolume.sharedProfile = lensProfile;
                lensVolume.weight = 0f;
            }
            float now = lensTotal > 0f ? lensAmp * Mathf.Clamp01(lensLeft / lensTotal) : 0f;
            lensAmp = Mathf.Max(now, Mathf.Clamp01(strength));
            lensLeft = lensTotal = Mathf.Max(0.02f, seconds);
#endif
        }

        /// <summary>One frame of the lens shock (effect time: it plays on through a hit's hold).</summary>
        void StepLensShock()
        {
#if CF_URP
            if (lensVolume == null) return;
            if (lensLeft > 0f) lensLeft -= dt;
            float k = lensLeft > 0f ? Mathf.Clamp01(lensLeft / Mathf.Max(0.01f, lensTotal)) : 0f;
            lensAberration.intensity.value = lensAmp;
            // Held at full for its first third, then dying away.
            float w = k > 0.66f ? 1f : k / 0.66f;
            lensVolume.weight = w * w;
            lensVolume.enabled = k > 0f;
#endif
        }

        /// <summary>
        /// The hind legs' sweep into the kick as a camera sees it (10-26): from the turn over into the buck until a few
        /// frames after the kick, a soft dark smear behind each hoof - the blur of shod hooves going fast, not a light.
        /// </summary>
        void HoofTrails(SwordFightSkills s, float from)
        {
            var pawn = s.Pawn;
            if (pawn == null) return;
            if (blurMat == null)
            {
                blurMat = new Material(Shader.Find(LitMaterials.ParticlesUnlitName)) { hideFlags = HideFlags.HideAndDontSave, name = "Blade blur" };
                LitMaterials.MakeTransparent(blurMat);
                blurMat.SetColor(LitMaterials.BaseColorId, Color.white);
            }
            var feet = new[] { BodyId.FootL, BodyId.FootR };
            var paths = new[] { new List<Vector3>(), new List<Vector3>() };
            var meshes = new Mesh[2];
            var gos = new GameObject[2];
            for (int i = 0; i < 2; i++)
            {
                gos[i] = new GameObject("Hoof blur", typeof(MeshFilter), typeof(MeshRenderer));
                gos[i].transform.SetParent(kit.root, false);
                meshes[i] = new Mesh { name = "Hoof blur", hideFlags = HideFlags.HideAndDontSave };
                meshes[i].MarkDynamic();
                gos[i].GetComponent<MeshFilter>().sharedMesh = meshes[i];
                var r = gos[i].GetComponent<MeshRenderer>();
                r.sharedMaterial = blurMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            const int Keep = 7;
            float endedAt = -1f;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var t = new List<int>();
            var run = kit.Run(3f, (fx, d) =>
            {
                if (s == null || pawn == null || s.Piece != PieceKind.Knight) return false;
                bool sweeping = s.EdgeStage == SfEdge.Active && s.EdgeTime >= from;
                bool kicked = s.EdgeStage == SfEdge.Recovery;
                if (s.EdgeStage == SfEdge.None) return false;
                if (kicked && endedAt < 0f) endedAt = fx.age;
                if (endedAt >= 0f && fx.age - endedAt > 8f * F) return false;
                for (int i = 0; i < 2; i++)
                {
                    var path = paths[i];
                    if (sweeping || (kicked && fx.age - endedAt < 2f * F)) path.Add(pawn.bodies[(int)feet[i]].position);
                    else if (path.Count > 0) path.RemoveAt(0);
                    while (path.Count > Keep) path.RemoveAt(0);
                    v.Clear(); c.Clear(); t.Clear();
                    int n = path.Count;
                    Vector3 eye = Eye;
                    for (int j = 0; j < n; j++)
                    {
                        float age = (n - 1 - j) / (float)(Keep - 1);
                        Vector3 along = path[Mathf.Min(n - 1, j + 1)] - path[Mathf.Max(0, j - 1)];
                        Vector3 acrossDir = Vector3.Cross(along.sqrMagnitude > 1e-8f ? along : Vector3.up, eye - path[j]);
                        acrossDir = acrossDir.sqrMagnitude > 1e-8f ? acrossDir.normalized : Vector3.up;
                        float w = 0.045f * (1f - 0.5f * age);
                        float a = 0.38f * (1f - age) * (1f - age);
                        v.Add(path[j] + acrossDir * w);
                        v.Add(path[j]);
                        v.Add(path[j] - acrossDir * w);
                        Color dark = new Color(0.2f, 0.19f, 0.18f, 0f);
                        c.Add(dark);
                        dark.a = a;
                        c.Add(dark);
                        dark.a = 0f;
                        c.Add(dark);
                    }
                    for (int j = 0; j < n - 1; j++)
                    {
                        int a0 = j * 3, b0 = a0 + 3;
                        t.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1, a0 + 1, b0 + 1, a0 + 2, a0 + 2, b0 + 1, b0 + 2 });
                    }
                    meshes[i].Clear();
                    meshes[i].SetVertices(v);
                    meshes[i].SetColors(c);
                    meshes[i].SetTriangles(t, 0);
                    meshes[i].RecalculateBounds();
                }
                return true;
            });
            run.end = () =>
            {
                for (int i = 0; i < 2; i++)
                {
                    if (gos[i] != null) Destroy(gos[i]);
                    if (meshes[i] != null) Destroy(meshes[i]);
                }
            };
        }

        void DestroyRealEdgeRBK()
        {
            if (hempMat != null) Destroy(hempMat);
#if CF_URP
            if (lensHolder != null) Destroy(lensHolder);
            if (lensProfile != null) Destroy(lensProfile);
#endif
        }
    }
}
