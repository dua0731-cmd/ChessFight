using System;
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
    /// The rook's and the bishop's F skills for real (10-25, 승규 님: "룩 f 미리보기 네모칸도 실사로 바꿔주고 판에 금 가는
    /// 자국과 놋쇠 테 휘어짐만 더함 적용시켜줘, 비숍f 를 사용하면 칼 양쪽에 2개가 소환 되면서 x 자로 상대 발 쪽에 꽃아주는
    /// 연출을 해줘 미리보기도 실사로 바꿔주고"):
    /// - the previews are faint lines of light lying on the board (no squares, no rings drawn in a colour);
    /// - the rook's line cracks open in the wood as he raises his sword, cracks run out from where he slams it, and where
    ///   his wave reaches the board's edge the brass trim there is buckled up out of true for a few seconds;
    /// - the bishop calls up two forged iron stakes beside his blade, and on F they fly out on the two diagonals and in on
    ///   the other two, crossing, and drive into the board at whoever's feet in an X, holding them there for the pin.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        // ---------------------------------------------------------------- lines of light on the board (previews)

        static readonly Color LightLine = new Color(1f, 0.96f, 0.88f);

        Kit.Strip LineStrip()
        {
            var pal = new Kit.Palette { main = LightLine, light = LightLine, deep = LightLine, ink = LightLine };
            return new Kit.Strip(kit, pal) { core = LightLine, coreShare = 1f, inkShare = 0f };
        }

        /// <summary>
        /// A way along the board while it is aimed and wound up: two faint lines of light at the edges of what it takes and
        /// a fainter one down its middle, from <c>start</c> to <c>reach</c> along <c>dir</c> from <c>from</c>, brighter as
        /// <c>k</c> (0..1) comes up. Kept while asked for each frame.
        /// </summary>
        void LightLane(SwordFightSkills s, string key, float alpha, Func<(Vector3 from, Vector3 dir, float start, float reach, float half, float k)> shape)
        {
            var owner = s;
            kit.Keep(s, key, () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < 3; i++) fx.strips.Add(LineStrip());
                var line = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x)) return false;
                    var (from, dir, start, reach, half, k) = shape();
                    dir = Kit.FlatDir(dir, Vector3.forward);
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    for (int i = 0; i < 3; i++)
                    {
                        float off = i == 0 ? -half : i == 1 ? half : 0f;
                        line.Clear();
                        if (reach > start + 0.1f)
                            for (int j = 0; j <= 16; j++)
                            {
                                float a = start + (reach - start) * j / 16f;
                                line.Add(Kit.FloorUnder(from + dir * a + side * off, 1.5f) + Vector3.up * 0.014f);
                            }
                        var st = x.strips[i];
                        if (line.Count < 2) { st.Hide(); continue; }
                        st.Build(line, j => (i == 2 ? 0.02f : 0.03f) * (1f - 0.5f * j / 16f), kit.Eye, true);
                        st.opacity = alpha * (i == 2 ? 0.18f : 0.38f) * (0.55f + 0.45f * k);
                        st.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        // ---------------------------------------------------------------- cracks in the wood

        static readonly Color CrackDark = new Color(0.07f, 0.05f, 0.035f);

        /// <summary>A crack's way over the board: from <paramref name="from"/> along <paramref name="dir"/> for
        /// <paramref name="length"/>, wandering a little to either side as wood splits along and across its grain.</summary>
        static List<Vector3> CrackPath(Vector3 from, Vector3 dir, float length, float wander)
        {
            dir = Kit.FlatDir(dir, Vector3.forward);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            var pts = new List<Vector3>();
            int n = Mathf.Max(3, Mathf.CeilToInt(length / 0.12f));
            float off = 0f;
            for (int i = 0; i <= n; i++)
            {
                float a = length * i / n;
                if (i > 0) off = Mathf.Clamp(off + Random.Range(-1f, 1f) * wander, -wander * 2.5f, wander * 2.5f);
                pts.Add(Kit.FloorUnder(from + dir * a + side * off, 1.5f) + Vector3.up * 0.013f);
            }
            return pts;
        }

        /// <summary>
        /// Cracks opening in the board: each path drawn dark and thin, running out from its start as <paramref name="grow"/>
        /// (0..1) says, its width tapering to nothing at its end; lying there while <paramref name="hold"/> says so, then
        /// fading over <paramref name="fade"/> seconds.
        /// </summary>
        Kit.Fx Cracks(List<List<Vector3>> paths, Func<float, float> grow, Func<float, bool> hold, float fade, float width)
        {
            var pal = new Kit.Palette { main = CrackDark, light = CrackDark, deep = CrackDark, ink = CrackDark };
            var strips = new List<Kit.Strip>();
            foreach (var p in paths) strips.Add(new Kit.Strip(kit, pal) { core = CrackDark, coreShare = 1f, inkShare = 0f });
            float released = -1f;
            var fx = kit.Run(30f, (x, d) =>
            {
                if (released < 0f && !hold(x.age)) released = x.age;
                float gone = released < 0f ? 0f : Mathf.Clamp01((x.age - released) / fade);
                if (gone >= 1f) return false;
                float g = Mathf.Clamp01(grow(x.age));
                for (int i = 0; i < paths.Count; i++)
                {
                    var pts = paths[i];
                    int n = pts.Count;
                    float w = width * (i == 0 ? 1f : 0.6f);
                    strips[i].Build(pts, j => w * (1f - 0.85f * j / Mathf.Max(1f, n - 1f)) * (0.7f + 0.3f * Mathf.PerlinNoise(j * 0.9f, i * 3.1f)), kit.Eye, true);
                    strips[i].head = g;
                    strips[i].opacity = 0.85f * (1f - gone);
                    strips[i].Apply();
                }
                return true;
            });
            fx.strips.AddRange(strips);
            return fx;
        }

        /// <summary>A crack with a branch or two off it (the branches start along it and run off at an angle).</summary>
        static List<List<Vector3>> CrackWithBranches(Vector3 from, Vector3 dir, float length, float wander, int branches)
        {
            var main = CrackPath(from, dir, length, wander);
            var list = new List<List<Vector3>> { main };
            for (int b = 0; b < branches; b++)
            {
                int at = Random.Range(main.Count / 4, Mathf.Max(main.Count / 4 + 1, main.Count * 3 / 4));
                float turn = Random.Range(25f, 55f) * (Random.value < 0.5f ? -1f : 1f);
                list.Add(CrackPath(main[at], Quaternion.AngleAxis(turn, Vector3.up) * dir, length * Random.Range(0.2f, 0.4f), wander * 0.7f));
            }
            return list;
        }

        // ---------------------------------------------------------------- the rook, for real

        /// <summary>His way while he aims and raises his sword: lines of light along it (as far as the floor goes), and in the
        /// windup the board splitting down its middle, the crack running out as the slam comes and lying open after.</summary>
        void RealRookMarks(SwordFightSkills s, SwordFightSkillParams S, bool aim, float alpha)
        {
            var owner = s;
            LightLane(s, aim ? "rook real aim" : "rook real lane", alpha, () =>
            {
                bool aiming = owner.Stage == SfStage.Aim;
                Vector3 dir = aiming ? owner.AimDir : owner.Dir;
                float reach = aiming ? owner.AimReach : owner.Reach;
                reach = Mathf.Min(reach, SwordFightSkills.FloorDistance(owner.Origin, dir, reach, 0.6f) - 0.1f);
                return (owner.Origin, dir, 0.6f, reach, S.rookWidth * 0.5f, aiming ? 0.3f : Kit.Charge(owner.StageTime, S.rookWindup));
            });
            if (aim) return;
            kit.Keep(s, "rook real crack", () =>
            {
                var holder = new Kit.Fx();
                float windup = Mathf.Max(0.05f, S.rookWindup);
                var paths = CrackWithBranches(owner.Origin + owner.Dir * 0.45f, owner.Dir, Mathf.Max(0.8f, owner.Reach - 0.45f), 0.03f, 3);
                float startAge = -1f;
                Cracks(paths,
                       age => { if (startAge < 0f) startAge = age; return Mathf.Lerp(0.05f, 0.7f, (age - startAge) / windup) + (owner.Stage != SfStage.Windup ? 1f : 0f); },
                       age => age < windup + 3.2f, 0.9f, 0.03f);
                holder.step = (x, d) => owner != null && kit.Held(x);
                return holder;
            });
        }

        /// <summary>
        /// The slam: the board takes it - cracks running out from under his sword, splinters of the square, a hold, a shake,
        /// a kick of the view - then a tower comes up out of each square as the wave reaches it (<see cref="Statue"/>), and
        /// if the wave runs on to the board's edge the brass there is buckled up (<see cref="BrassBuckle"/>).
        /// </summary>
        void RealRookSlam(SfFxEvent e)
        {
            var s = e.by;
            Vector3 at = Kit.FloorUnder(e.at, 1.5f);
            var radial = new List<List<Vector3>>();
            int n = 6;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * 360f / n + Random.Range(-20f, 20f), Vector3.up) * Kit.FlatDir(e.dir, Vector3.forward);
                radial.AddRange(CrackWithBranches(at, dir, Random.Range(0.45f, 0.95f), 0.025f, 1));
            }
            Cracks(radial, age => age / 0.08f, age => age < 3f, 0.9f, 0.026f);
            var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
            Color wood = FloorWood(at);
            chipA = ChipsOf(wood); chipB = ChipsOf(wood, 0.7f); chipShare = 0.6f;
            WallChips(at + Vector3.up * 0.04f, Vector3.up, RookChips(12), 3.4f, 0.8f, 0.012f, 0.04f);
            chipA = keepA; chipB = keepB; chipShare = keepShare;
            RookVfx("F_Slam", at, Kit.FlatDir(e.dir, Vector3.forward));
            HitStop(5);
            Shake(s.Pawn, null, 0.17f, 9);
            Punch(s.Pawn, null, 6f, 16);
            float stamped = 0.6f, reach = s.Reach;
            Vector3 origin = s.Origin, way = s.Dir;
            var caster = s.Pawn;
            // R165 (사용자: "룩은 f 스킬을 쓸 때 퀸 처럼 vfx", in the rook's own beige): the wave burns a line down the middle
            // of its lane as it runs, stopping at the board's edge (SwordFightSkillFx.RookFVfx.cs).
            Vector3 lineFrom = Kit.FloorUnder(origin + way * 0.6f + Vector3.up * 0.5f, 1.5f);
            var line = BeamOf(RookFVfx("F_Line", lineFrom, Kit.FlatDir(way, Vector3.forward)));
            if (line != null) line.Reach(lineFrom);
            var order = StatueOrder(Mathf.Max(1, Mathf.CeilToInt((reach - 0.6f) / 1.5f)));
            int next = 0;
            // Where the wave meets the board's edge, if it gets that far.
            var arena = FindFirstObjectByType<SwordFightArena>();
            bool edge = false;
            Vector3 edgeAt = Vector3.zero, edgeOut = Vector3.zero;
            float edgeDist = float.MaxValue;
            if (arena != null && BoardEdge(arena, origin, way, out edgeAt, out edgeOut, out edgeDist)) edge = edgeDist <= reach + 0.9f;
            bool buckled = false;
            var wave = kit.Run(3f, (fx, d) =>
            {
                bool running = s != null && s.Stage == SfStage.Active && s.Piece == PieceKind.Rook;
                float front = running ? s.WaveFront : reach;
                if (line != null)
                {
                    if (!running) line.Release();
                    else if (Kit.FloorUnder(origin + way * front + Vector3.up * 0.5f, 1.5f, out Vector3 under) && Mathf.Abs(under.y - lineFrom.y) < 0.35f)
                        line.Reach(under);
                }
                while (stamped < reach - 0.3f && stamped + 0.35f <= front + 0.01f)
                {
                    float len = Mathf.Min(1.5f, reach - stamped);
                    Statue(order[Mathf.Min(next++, order.Count - 1)], origin + way * (stamped + len * 0.5f), way, caster);
                    RookVfx("F_Stamp", Kit.FloorUnder(origin + way * (stamped + len * 0.5f) + Vector3.up * 0.5f, 1.5f), way);
                    stamped += 1.5f;
                }
                if (edge && !buckled && (front >= edgeDist - 0.5f || !running))
                {
                    buckled = true;
                    BrassBuckle(arena, edgeAt, edgeOut);
                }
                return running || stamped < reach - 0.3f || (edge && !buckled);
            });
            wave.end = () => { if (line != null) line.Release(); };
        }

        /// <summary>Where a line from <paramref name="from"/> along <paramref name="dir"/> crosses the board's edge: the point on
        /// the brass, its outward side, and how far along.</summary>
        static bool BoardEdge(SwordFightArena arena, Vector3 from, Vector3 dir, out Vector3 at, out Vector3 outward, out float dist)
        {
            float h = arena.HalfSize;
            dir = Kit.FlatDir(dir, Vector3.forward);
            at = outward = Vector3.zero;
            dist = float.MaxValue;
            if (h <= 0f) return false;
            float tx = Mathf.Abs(dir.x) > 1e-4f ? ((dir.x > 0f ? h : -h) - from.x) / dir.x : float.MaxValue;
            float tz = Mathf.Abs(dir.z) > 1e-4f ? ((dir.z > 0f ? h : -h) - from.z) / dir.z : float.MaxValue;
            dist = Mathf.Min(tx, tz);
            if (dist <= 0f || dist == float.MaxValue) return false;
            at = new Vector3(from.x + dir.x * dist, 0f, from.z + dir.z * dist);
            outward = tx < tz ? new Vector3(Mathf.Sign(dir.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(dir.z));
            return true;
        }

        /// <summary>
        /// The brass at the board's edge buckled by the wave: a length of it (its own brass) bowed up and out of true over
        /// the straight trim, snapping up in a few frames with a shudder, a few splinters of the square behind it and a
        /// hold; it lies bent while the board recovers, then eases flat again.
        /// </summary>
        void BrassBuckle(SwordFightArena arena, Vector3 at, Vector3 outward)
        {
            if (arena == null || arena.Brass == null) return;
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            const float Length = 1.5f, Thick = 0.06f, Tall = 0.62f;
            var mesh = new Mesh { name = "Buckled brass", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            var go = new GameObject("Buckled brass", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(kit.root, false);
            go.transform.SetPositionAndRotation(at + outward * (Thick * 0.5f), Quaternion.LookRotation(outward, Vector3.up));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = arena.Brass;
            float lift = Random.Range(0.1f, 0.16f), bulge = Random.Range(0.07f, 0.12f), twist = Random.Range(-8f, 8f);
            var v = new List<Vector3>();
            var t = new List<int>();
            const int N = 16;
            void Build(float b)
            {
                // The bar in its own frame: x along the edge, z out of the board, y up; its top bowed up and out.
                v.Clear(); t.Clear();
                for (int i = 0; i <= N; i++)
                {
                    float u = i / (float)N, x = (u - 0.5f) * Length;
                    float bow = Mathf.Sin(Mathf.PI * u);
                    bow *= bow;
                    float up = lift * bow * b, outz = bulge * bow * b;
                    float tilt = (twist * bow * b) * Mathf.Deg2Rad;
                    // Four corners of the bar's cross-section at this point (back/front, bottom/top), the top leaning out.
                    float top = 0.01f + up, bottom = -Tall + 0.01f + up * 0.5f;
                    float lean = Mathf.Sin(tilt) * Tall;
                    v.Add(new Vector3(x, bottom, -Thick * 0.5f + outz * 0.4f));
                    v.Add(new Vector3(x, top, -Thick * 0.5f + outz + lean));
                    v.Add(new Vector3(x, top, Thick * 0.5f + outz + lean));
                    v.Add(new Vector3(x, bottom, Thick * 0.5f + outz * 0.4f));
                }
                for (int i = 0; i < N; i++)
                    for (int f = 0; f < 4; f++)
                    {
                        int a0 = i * 4 + f, a1 = i * 4 + (f + 1) % 4, b0 = a0 + 4, b1 = a1 + 4;
                        t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                    }
                // End caps.
                t.AddRange(new[] { 0, 2, 1, 0, 3, 2 });
                int e = N * 4;
                t.AddRange(new[] { e, e + 1, e + 2, e, e + 2, e + 3 });
                mesh.Clear();
                mesh.SetVertices(v);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
            Build(0f);
            var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
            Color wood = FloorWood(at - outward * 0.3f);
            chipA = ChipsOf(arena.Brass.color); chipB = ChipsOf(wood); chipShare = 0.35f;
            WallChips(at + Vector3.up * 0.05f, outward + Vector3.up * 0.8f, RookChips(9), 3f, 0.6f, 0.01f, 0.035f);
            chipA = keepA; chipB = keepB; chipShare = keepShare;
            HitStop(3);
            var f2 = kit.Run(4.2f, (fx, d) =>
            {
                float a = fx.age;
                // Up in 0.08 s past its bend, a shudder back to it, held, flat again over the last 0.6 s.
                float b = a < 0.08f ? Kit.EaseOut(a / 0.08f) * 1.15f
                    : a < 0.35f ? 1f + 0.15f * Mathf.Cos((a - 0.08f) * 40f) * (1f - (a - 0.08f) / 0.27f)
                    : a < 3.6f ? 1f : 1f - Kit.EaseIn((a - 3.6f) / 0.6f);
                Build(b);
                return true;
            });
            f2.end = () => { if (go != null) Destroy(go); Destroy(mesh); };
        }

        /// <summary>Whoever the wave throws: chips of their wood, a hold, a shake (no colour, no ring).</summary>
        void RealRookHit(SfFxEvent e)
        {
            if (e.target != null)
            {
                ShieldShatter(e.target, e.dir);
                RookVfx("F_Hit", e.target.Hips.position, e.dir);
            }
            RealHit(e, 4, 0.14f, RookChips(10));
        }

        /// <summary>The wave stopped (by a king's guard or a wall): splinters thrown back off where it broke.</summary>
        void RealRookBlocked(SfFxEvent e)
        {
            Vector3 at = Kit.FloorUnder(e.at, 1.5f);
            var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
            Color wood = FloorWood(at);
            chipA = ChipsOf(wood); chipB = ChipsOf(wood, 0.7f); chipShare = 0.6f;
            WallChips(at + Vector3.up * 0.1f, -e.dir + Vector3.up * 0.6f, RookChips(9), 2.8f, 0.6f, 0.012f, 0.035f);
            chipA = keepA; chipB = keepB; chipShare = keepShare;
            HitStop(4);
        }

        // ---------------------------------------------------------------- the bishop's stakes

        /// <summary>The stake drawn half again as big as its profile (0.93 m, a 4 cm shank): it has to read at a glance.</summary>
        const float StakeSize = 1.5f, StakeHalf = 0.31f * StakeSize;

        Mesh stakeMesh;
        Material stakeMat;

        /// <summary>A forged iron stake 0.62 m long along +z, its point at +0.31: a square shank (turned 45°) tapering to the
        /// point, a collar under a broad flat head.</summary>
        Mesh StakeMesh()
        {
            if (stakeMesh != null) return stakeMesh;
            // (z, half-width) from the head to the point.
            var prof = new[]
            {
                new Vector2(-0.31f, 0f), new Vector2(-0.31f, 0.034f), new Vector2(-0.292f, 0.034f), new Vector2(-0.286f, 0.019f),
                new Vector2(-0.262f, 0.019f), new Vector2(-0.256f, 0.025f), new Vector2(-0.236f, 0.025f), new Vector2(-0.23f, 0.016f),
                new Vector2(0.1f, 0.013f), new Vector2(0.24f, 0.006f), new Vector2(0.31f, 0f),
            };
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < prof.Length - 1; i++)
                for (int k = 0; k < 4; k++)
                {
                    float a0 = (k * 90f + 45f) * Mathf.Deg2Rad, a1 = ((k + 1) * 90f + 45f) * Mathf.Deg2Rad;
                    Vector3 p00 = new Vector3(Mathf.Cos(a0) * prof[i].y, Mathf.Sin(a0) * prof[i].y, prof[i].x);
                    Vector3 p01 = new Vector3(Mathf.Cos(a1) * prof[i].y, Mathf.Sin(a1) * prof[i].y, prof[i].x);
                    Vector3 p10 = new Vector3(Mathf.Cos(a0) * prof[i + 1].y, Mathf.Sin(a0) * prof[i + 1].y, prof[i + 1].x);
                    Vector3 p11 = new Vector3(Mathf.Cos(a1) * prof[i + 1].y, Mathf.Sin(a1) * prof[i + 1].y, prof[i + 1].x);
                    int b = v.Count;
                    v.Add(p00); v.Add(p01); v.Add(p11); v.Add(p10);
                    t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
                }
            for (int i = 0; i < v.Count; i++) v[i] *= StakeSize;
            stakeMesh = new Mesh { name = "Iron stake", hideFlags = HideFlags.HideAndDontSave };
            stakeMesh.SetVertices(v);
            stakeMesh.SetTriangles(t, 0);
            stakeMesh.RecalculateNormals();
            stakeMesh.RecalculateBounds();
            return stakeMesh;
        }

        Material StakeMaterial()
        {
            if (stakeMat != null) return stakeMat;
            // Forged steel, hammered: grey, half polished where it was struck (black iron read as a hole on the board).
            stakeMat = LitMaterials.NewLit(new Color(0.46f, 0.45f, 0.44f), 0.5f, 0.9f);
            stakeMat.hideFlags = HideFlags.HideAndDontSave;
            stakeMat.name = "Iron stake";
            return stakeMat;
        }

        /// <summary>One thrown blade: its transform, half its length (its middle to its point) and its own way.</summary>
        sealed class Stake
        {
            public Transform t;
            public Vector3 from, c1, c2, to, sink;
            public float side, half = StakeHalf;
        }

        /// <summary>10-25 (승규 님: "말뚝 말고 실제로 비숍이 가지고 있는 칼이 x 자로 날아가면서 뽑히는 걸 … 날아갈 때 칼을 조금
        /// 작게"): the thrown blades are the bishop's own sword, a little smaller than in his hand (0.6 to its 0.8).</summary>
        const float ThrownSwordSize = 0.6f;

        Material thrownMat;

        /// <summary>A copy of the bishop's sword to throw (point along +z, its middle at the origin); the forged stake if
        /// there is no sword model.</summary>
        Stake NewStake(float side)
        {
            var go = new GameObject("Thrown sword");
            go.transform.SetParent(kit.root, false);
            var prefab = Resources.Load<GameObject>("SwordFightSwords/" + PieceKind.Bishop);
            var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = StakeMesh();
                go.AddComponent<MeshRenderer>().sharedMaterial = StakeMaterial();
                return new Stake { t = go.transform, side = side };
            }
            if (thrownMat == null)
            {
                // As the sword in his hand is drawn (SwordFightPawn's model material).
                thrownMat = LitMaterials.NewLit(new Color(.8f, .8f, .8f), .45f);
                thrownMat.hideFlags = HideFlags.HideAndDontSave;
                thrownMat.name = "Thrown sword";
            }
            var b = mf.sharedMesh.bounds;
            var model = new GameObject("Sword model", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            model.SetParent(go.transform, false);
            model.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = model.GetComponent<MeshRenderer>();
            r.sharedMaterial = thrownMat;
            r.receiveShadows = false;
            // Exported standing up (+Y, pommel down): laid along +z, point first, its middle at the origin.
            model.SetLocalPositionAndRotation(new Vector3(0f, 0f, -b.center.y * ThrownSwordSize), Quaternion.Euler(90f, 0f, 0f));
            model.localScale = Vector3.one * ThrownSwordSize;
            return new Stake { t = go.transform, side = side, half = b.extents.y * ThrownSwordSize };
        }

        /// <summary>The bishop's sword back in his hand: drawn again (it was out of his hand while thrown).</summary>
        static void SwordBack(SwordFightSkills by)
        {
            if (by != null && by.Fighter != null) by.Fighter.SwordAway = false;
        }

        /// <summary>
        /// Drawn out of the board (or caught off a king's blade) and back: each thrown blade comes up out of the wood along
        /// its own way, then flies back to the bishop's hand turning to lie as his sword lies, and the sword is in his hand
        /// again as they get there (both go into the one sword). Nobody to go back to: they are only gone.
        /// </summary>
        void ReturnBlades(SwordFightSkills by, Stake[] blades, Vector3[] ways)
        {
            var from = new Vector3[blades.Length];
            var rot0 = new Quaternion[blades.Length];
            for (int i = 0; i < blades.Length; i++)
                if (blades[i].t != null) { from[i] = blades[i].t.position; rot0[i] = blades[i].t.rotation; }
            const float Out = 0.12f, Back = 0.24f;
            var f = kit.Run(Out + Back + 0.02f, (fx, d) =>
            {
                float a = fx.age;
                bool home = by != null && by.Fighter != null && by.Fighter.SwordBody != null && by.Fighter.Alive;
                for (int i = 0; i < blades.Length; i++)
                {
                    var k = blades[i];
                    if (k.t == null) continue;
                    Vector3 pulled = from[i] - ways[i] * 0.32f;
                    if (a < Out)
                    {
                        // Pulled straight back up out of the wood along the way it went in.
                        k.t.position = Vector3.Lerp(from[i], pulled, Kit.EaseOut(a / Out));
                        continue;
                    }
                    float e = Mathf.Clamp01((a - Out) / Back);
                    if (!home)
                    {
                        k.t.localScale = Vector3.one * Mathf.Max(0.01f, 1f - e);
                        continue;
                    }
                    var sw = by.Fighter.SwordBody;
                    Vector3 grip = sw.position + sw.rotation * Vector3.forward * 0.45f;
                    Vector3 lift = Vector3.Lerp(pulled, grip, 0.5f) + Vector3.up * 0.6f;
                    float s = e * e * (3f - 2f * e);
                    Vector3 p = Vector3.Lerp(Vector3.Lerp(pulled, lift, s), Vector3.Lerp(lift, grip, s), s);
                    k.t.SetPositionAndRotation(p, Quaternion.Slerp(rot0[i], sw.rotation, s));
                }
                return true;
            });
            f.end = () =>
            {
                foreach (var k in blades) if (k.t != null) Destroy(k.t.gameObject);
                SwordBack(by);
            };
        }

        /// <summary>Where a stake waits beside the bishop's blade (its side: -1 left, 1 right), and which way it points.</summary>
        static void StakeReady(SwordFightSkills s, float side, out Vector3 at, out Quaternion rot)
        {
            Vector3 dir = Kit.FlatDir(s.Dir, Vector3.forward);
            Vector3 perp = Vector3.Cross(Vector3.up, dir);
            Vector3 mid = s.Fighter != null && s.Fighter.SwordBody != null ? Vector3.Lerp(s.Fighter.BladeRoot, s.Fighter.BladeTip, 0.55f)
                : s.Pawn.bodies[(int)BodyId.Chest].position + dir * 0.5f;
            at = mid + perp * (side * 0.07f);   // the two leave his own sword, one to each side
            rot = Quaternion.LookRotation((dir + perp * side * 0.25f + Vector3.up * 0.1f).normalized, Vector3.up);
        }

        /// <summary>Where a stake goes into the board at someone's feet: on its diagonal (in from its side), its point just
        /// past the middle of the feet, so the two cross over the ankles in an X.</summary>
        static void StakeTarget(Vector3 feet, Vector3 dir, float side, out Vector3 sink, out Vector3 inDir)
        {
            dir = Kit.FlatDir(dir, Vector3.forward);
            Vector3 perp = Vector3.Cross(Vector3.up, dir);
            inDir = (dir - perp * side).normalized;
            // In at a steep slant: its point 0.2 m into the wood, its way down and on along its diagonal.
            sink = feet + perp * (side * 0.06f) + inDir * 0.08f + Vector3.down * 0.16f;
        }

        /// <summary>A stake's flight from where it waits, out on its own diagonal and in on the other, arching, to drive in at
        /// <paramref name="sink"/> on <paramref name="inDir"/>.</summary>
        static void StakeWay(Stake k, Vector3 from, Vector3 sink, Vector3 dir, Vector3 inDir)
        {
            Vector3 perp = Vector3.Cross(Vector3.up, Kit.FlatDir(dir, Vector3.forward));
            Vector3 outDir = (Kit.FlatDir(dir, Vector3.forward) + perp * k.side).normalized, flat = sink - from;
            flat.y = 0f;
            float len = Mathf.Clamp(flat.magnitude * 0.38f, 0.5f, 2.6f);
            k.from = from;
            k.sink = sink;
            Vector3 down = (inDir * 0.62f + Vector3.down * 0.78f).normalized;
            k.to = sink - down * 0.2f;   // where its point is when it first touches the board
            k.c1 = from + outDir * len + Vector3.up * 0.4f;
            k.c2 = k.to - down * len * 0.9f;
        }

        /// <summary>The bishop's stakes called up beside his blade while he winds up (they come with F):
        /// each grows out of nothing along its length in a tenth of a second, light runs down it, and it hangs there turning
        /// a little toward where it will go.</summary>
        void ReadyStakes(SwordFightSkills s, SwordFightSkillParams S)
        {
            var owner = s;
            kit.Keep(s, "bishop real stakes", () =>
            {
                var fx = new Kit.Fx();
                var stakes = new[] { NewStake(-1f), NewStake(1f) };
                bool glinted = false;
                fx.end = () => { foreach (var k in stakes) if (k.t != null) Destroy(k.t.gameObject); };
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x)) return false;
                    float grow = Kit.EaseOut(Mathf.Clamp01(x.age / 0.1f));
                    foreach (var k in stakes)
                    {
                        StakeReady(owner, k.side, out Vector3 at, out Quaternion rot);
                        float bob = Mathf.Sin(kit.Clock * 9f + k.side) * 0.01f;
                        k.t.SetPositionAndRotation(at + Vector3.up * bob, rot);
                        k.t.localScale = new Vector3(1f, 1f, Mathf.Max(0.02f, grow));
                    }
                    if (!glinted && x.age > 0.1f)
                    {
                        glinted = true;
                        SparkLight(owner.Pawn.bodies[(int)BodyId.Chest].position + owner.Dir * 0.4f, 0.8f, 0.12f, 1.4f, new Color(0.9f, 0.93f, 1f));
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>
        /// The stakes fly and drive in: out on their diagonals, in on the others, crossing, point first; as each bites into
        /// the board, splinters of the square and a short crack, the stake shuddering in the wood. They stay in while
        /// <paramref name="held"/> says so (the pin), then are drawn up out of it and gone. <paramref name="target"/>, if
        /// any, is shaken as they bite.
        /// </summary>
        void DriveStakes(SwordFightSkills by, Vector3 feet, Vector3 dir, RagdollPawn target, Func<bool> held, float fly)
        {
            var stakes = new[] { NewStake(-1f), NewStake(1f) };
            foreach (var k in stakes)
            {
                Vector3 from;
                Quaternion r0;
                if (by != null && by.Pawn != null) StakeReady(by, k.side, out from, out r0);
                else from = feet + Vector3.up * 1.6f - Kit.FlatDir(dir, Vector3.forward) * 1.2f;
                StakeTarget(feet, dir, k.side, out Vector3 sink, out Vector3 inDir);
                StakeWay(k, from, sink, dir, inDir);
            }
            bool bit = false;
            float released = -1f;
            var f = kit.Run(20f, (fx, d) =>
            {
                float a = fx.age;
                float t = Mathf.Clamp01(a / fly);
                float e = 1f - (1f - t) * (1f - t) * (1f - t) * 0.6f - 0.4f * (1f - t);   // quick off, still fast when it bites
                foreach (var k in stakes)
                {
                    if (k.t == null) continue;
                    if (t < 1f)
                    {
                        Vector3 p = Bezier(k.from, k.c1, k.c2, k.to, e);
                        Vector3 tan = BezierTangent(k.from, k.c1, k.c2, k.to, Mathf.Clamp(e, 0.02f, 0.98f));
                        // The point leads; the blade's own middle is half its length behind it.
                        Quaternion rot = Quaternion.LookRotation(tan.sqrMagnitude > 1e-6f ? tan.normalized : Vector3.down, Vector3.up);
                        k.t.SetPositionAndRotation(p - rot * Vector3.forward * k.half, rot);
                    }
                    else
                    {
                        // In: its point at the sink, its way as it came; a shudder in the wood for a third of a second.
                        Vector3 way = (k.sink - k.to).normalized;
                        float since = a - fly;
                        float shake = since < 0.35f ? Mathf.Sin(since * 70f) * 4f * (1f - since / 0.35f) : 0f;
                        Vector3 side = Vector3.Cross(Vector3.up, way).normalized;
                        Quaternion rot = Quaternion.AngleAxis(shake, side) * Quaternion.LookRotation(way, Vector3.up);
                        k.t.SetPositionAndRotation(k.sink - rot * Vector3.forward * k.half, rot);
                    }
                }
                if (!bit && t >= 1f)
                {
                    bit = true;
                    var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
                    Color wood = FloorWood(feet);
                    chipA = ChipsOf(wood); chipB = ChipsOf(wood, 0.7f); chipShare = 0.6f;
                    var cracks = new List<List<Vector3>>();
                    foreach (var k in stakes)
                    {
                        Vector3 into = Kit.FlatDir(k.sink - k.to, dir);
                        WallChips(k.to + Vector3.up * 0.03f, -into + Vector3.up * 0.9f, 6, 2.4f, 0.6f, 0.008f, 0.025f);
                        cracks.AddRange(CrackWithBranches(Kit.FloorUnder(k.to, 0.5f), into, Random.Range(0.25f, 0.4f), 0.02f, 1));
                    }
                    chipA = keepA; chipB = keepB; chipShare = keepShare;
                    var hold = held;
                    Cracks(cracks, age => age / 0.05f, age => hold(), 0.6f, 0.016f);
                    HitStop(target != null ? 4 : 2, by != null ? by.Pawn : null, target);
                    Shake(by != null ? by.Pawn : null, target, target != null ? 0.08f : 0.05f, 5);
                    Punch(by != null ? by.Pawn : null, target, 3f, 10);
                }
                if (t >= 1f && released < 0f && !held())
                {
                    // Let go: drawn out of the board and back to his hand (ReturnBlades owns them from here).
                    released = a;
                    var ways = new Vector3[stakes.Length];
                    for (int i = 0; i < stakes.Length; i++) ways[i] = (stakes[i].sink - stakes[i].to).normalized;
                    ReturnBlades(by, stakes, ways);
                    return false;
                }
                return true;
            });
            f.end = () =>
            {
                if (released >= 0f) return;
                foreach (var k in stakes) if (k.t != null) Destroy(k.t.gameObject);
                SwordBack(by);
            };
        }

        /// <summary>The bishop's F, for real: its preview (a ring of light the size of its reach on the point and the X of
        /// its diagonals through it, brighter with someone there to take) and the stakes it calls up as it winds up.</summary>
        void RealBishopMarks(SwordFightSkills s, SwordFightSkillParams S, bool aim, float alpha)
        {
            var owner = s;
            kit.Keep(s, aim ? "bishop real aim" : "bishop real mark", () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < 3; i++) fx.strips.Add(LineStrip());
                var pts = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x)) return false;
                    bool aiming = owner.Stage == SfStage.Aim;
                    Vector3 c = Kit.FloorUnder(aiming ? owner.AimPoint : owner.Point, 1.5f);
                    Vector3 dir = Kit.FlatDir(aiming ? owner.AimDir : owner.Dir, Vector3.forward);
                    float k = aiming ? 0.3f : Kit.Charge(owner.StageTime, S.bishopWindup);
                    float got = owner.PinCandidate != null ? 1.6f : 1f;
                    float r = S.bishopRadius;
                    // The ring.
                    pts.Clear();
                    for (int j = 0; j <= 40; j++)
                    {
                        float ang = j / 40f * Mathf.PI * 2f;
                        pts.Add(Kit.FloorUnder(c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r, 1f) + Vector3.up * 0.014f);
                    }
                    x.strips[0].Build(pts, j => 0.028f, kit.Eye, true);
                    x.strips[0].opacity = alpha * 0.36f * got * (0.55f + 0.45f * k);
                    x.strips[0].Apply();
                    // The X of its diagonals.
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 diag = Quaternion.AngleAxis(i == 0 ? 45f : -45f, Vector3.up) * dir;
                        pts.Clear();
                        for (int j = 0; j <= 10; j++) pts.Add(Kit.FloorUnder(c + diag * Mathf.Lerp(-r * 0.8f, r * 0.8f, j / 10f), 1f) + Vector3.up * 0.015f);
                        x.strips[1 + i].Build(pts, j => 0.022f * (1f - 0.6f * Mathf.Abs(j / 10f - 0.5f) * 2f), kit.Eye, true);
                        x.strips[1 + i].opacity = alpha * 0.3f * got * (0.55f + 0.45f * k);
                        x.strips[1 + i].Apply();
                    }
                    return true;
                };
                return fx;
            });
            // 10-25: nothing called up beside the blade any more - his own sword is what flies (RealBishopFire).
        }

        /// <summary>F goes: no one on the point - the stakes drive into the empty board there and are drawn out a moment
        /// later (someone there: the pin flies them, <see cref="RealPin"/>).</summary>
        void RealBishopFire(SfFxEvent e)
        {
            var s = e.by;
            bool got = e.order > 0;
            Punch(s.Pawn, null, 3f, 10);
            // His sword is what he throws: out of his hand until the blades come back (and for certain a little after).
            if (s.Fighter != null)
            {
                s.Fighter.SwordAway = true;
                var S = this.S;
                float latest = (S != null ? S.bishopPinTime : 0.8f) + 1.6f;
                kit.Run(latest, (fx, d) => true).end = () => SwordBack(s);
            }
            if (got || (handsCut.TryGetValue(s, out float cutAt) && kit.Clock - cutAt < 0.1f)) return;
            Vector3 c = Kit.FloorUnder(e.at, 1.5f);
            float until = kit.Clock + HandFly * F + 0.6f;
            DriveStakes(s, c, s.Dir, null, () => kit.Clock < until, HandFly * F);
        }

        /// <summary>Pinned, for real: the bishop's two stakes fly to the feet and drive in crossed over the ankles, and stay in
        /// the board while the pin holds.</summary>
        void RealPin(SwordFightSkills s)
        {
            var pawn = s.Pawn;
            var owner = s;
            kit.Keep(s, "pinned real", () =>
            {
                var fx = new Kit.Fx();
                SwordFightSkills by = null;
                Vector3 dir = Kit.FlatDir(pawn.Facing, Vector3.forward);
                if (pinnedBy.TryGetValue(pawn, out var src))
                {
                    pinnedBy.Remove(pawn);
                    if (src.by != null && src.by.Pawn != null && kit.Clock - src.at < 0.3f)
                    {
                        by = src.by;
                        dir = Kit.FlatDir(src.dir, dir);
                    }
                }
                Vector3 feet = Kit.FloorUnder(pawn.Hips.position, 0.6f);
                bool on = true;
                // Held by something else than a bishop (10-25: a queen's cut standing a beat before it lands): no blades.
                if (by != null) DriveStakes(by, feet, dir, pawn, () => on, HandFly * F);
                fx.step = (x, d) =>
                {
                    on = pawn != null && kit.Held(x) && owner.PinLeft > 0f && pawn.State == PawnState.Active;
                    return on;
                };
                fx.end = () => on = false;
                return fx;
            });
        }

        /// <summary>A king's guard takes the stakes: they fly at his feet, his blade meets them (a white flash and a lens streak
        /// at each), and they are knocked away tumbling and gone.</summary>
        void DeflectStakes(SwordFightSkills bishop, RagdollPawn king)
        {
            if (king == null) return;
            Vector3 feet = Kit.FloorUnder(king.Hips.position, 0.6f);
            Vector3 dir = bishop != null ? bishop.Dir : Kit.FlatDir(king.Facing, Vector3.forward);
            var stakes = new[] { NewStake(-1f), NewStake(1f) };
            var spin = new Vector3[2];
            var vel = new Vector3[2];
            foreach (var k in stakes)
            {
                Vector3 from;
                Quaternion r0;
                if (bishop != null && bishop.Pawn != null) StakeReady(bishop, k.side, out from, out r0);
                else from = feet + Vector3.up * 1.6f;
                StakeTarget(feet + Vector3.up * 0.35f, dir, k.side, out Vector3 sink, out Vector3 inDir);
                StakeWay(k, from, sink, dir, inDir);
            }
            float fly = HandFly * F;
            bool met = false, returned = false;
            var f = kit.Run(fly + 0.7f, (fx, d) =>
            {
                float a = fx.age;
                for (int i = 0; i < 2; i++)
                {
                    var k = stakes[i];
                    if (k.t == null) continue;
                    if (a < fly)
                    {
                        float e = a / fly;
                        Vector3 p = Bezier(k.from, k.c1, k.c2, k.to, e);
                        Vector3 tan = BezierTangent(k.from, k.c1, k.c2, k.to, Mathf.Clamp(e, 0.02f, 0.98f));
                        Quaternion rot = Quaternion.LookRotation(tan.sqrMagnitude > 1e-6f ? tan.normalized : Vector3.down, Vector3.up);
                        k.t.SetPositionAndRotation(p - rot * Vector3.forward * k.half, rot);
                        vel[i] = tan.normalized * 6f;
                    }
                    else
                    {
                        vel[i] += Physics.gravity * d;
                        k.t.position += vel[i] * d;
                        k.t.rotation = Quaternion.AngleAxis(spin[i].magnitude * d, spin[i].normalized) * k.t.rotation;
                    }
                }
                if (met && a >= fly + 0.35f)
                {
                    // Knocked away, they turn in the air and fly back to his hand (ReturnBlades owns them from here).
                    returned = true;
                    ReturnBlades(bishop, stakes, new[] { Vector3.zero, Vector3.zero });
                    return false;
                }
                if (!met && a >= fly)
                {
                    met = true;
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 at = stakes[i].t.position + stakes[i].t.forward * stakes[i].half;
                        ClashFlash(at, 0.7f);
                        Vector3 back = -Kit.FlatDir(vel[i], dir);
                        vel[i] = (back * 2.4f + Vector3.up * 2.6f + Vector3.Cross(Vector3.up, back) * (stakes[i].side * 1.2f));
                        spin[i] = Random.onUnitSphere * 900f;
                    }
                    SparkLight(feet + Vector3.up * 0.3f, 1.2f, 0.08f, 1.6f, new Color(0.92f, 0.95f, 1f));
                    RealBlockBeat(king, bishop != null ? bishop.Pawn : null);
                }
                return true;
            });
            f.end = () =>
            {
                if (returned) return;
                foreach (var k in stakes) if (k.t != null) Destroy(k.t.gameObject);
                SwordBack(bishop);
            };
        }

        void DestroyRealRookBishop()
        {
            if (stakeMesh != null) Destroy(stakeMesh);
            if (stakeMat != null) Destroy(stakeMat);
            if (thrownMat != null) Destroy(thrownMat);
            if (cutMat != null) Destroy(cutMat);
            if (chipMat != null) Destroy(chipMat);
        }
    }
}
