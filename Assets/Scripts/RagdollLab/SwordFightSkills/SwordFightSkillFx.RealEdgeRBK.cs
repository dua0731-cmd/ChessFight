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
                    x.strips[0].Build(pts, j => 0.022f, kit.Eye, true);
                    x.strips[0].opacity = 0.32f + 0.08f * Mathf.Sin(kit.Clock * 5f);
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
                bool Holding(float age) => c != null && (c.phase == SwordFightEdgeFloor.Phase.Crack || c.phase == SwordFightEdgeFloor.Phase.Open);
                Cracks(grain, age => Mathf.InverseLerp(0f, 0.6f, age / crack), Holding, 0.6f, 0.016f);
                Cracks(seams, age => Mathf.InverseLerp(0.25f, 0.9f, age / crack), Holding, 0.6f, 0.024f);
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
        /// out and twisting as it goes (the squares' own brass is hidden while it is away and back when the chunk rises).
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
                case SwordFightEdgeFloor.Phase.Open:
                {
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
                    if (arena != null) arena.BrassIn(c.rect, false);
                    break;
                case SwordFightEdgeFloor.Phase.Done:
                    if (arena != null) arena.BrassIn(c.rect, false);
                    Shake(rook, null, 0.06f, 5);
                    break;
            }
        }

        /// <summary>One length of the edge's brass torn off by the collapse: bowing out and twisting in its first few frames
        /// (wrenched off), then falling and turning over, gone below the board.</summary>
        void FallingBrass(SwordFightArena arena, Vector3 at, Vector3 along, Vector3 outward, float length)
        {
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
