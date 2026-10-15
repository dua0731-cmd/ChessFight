using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// What the bishop's strike breaks (R134, 승규 님 10-14: "실제로 바닥이 파이는 연출이랑 x 자도 좀 더 사실 적으로"; R137, the same
    /// day, with two pictures: "벽이 있거나 언덕이 있으면 부자연 스럽게 구연이 돼 실제 인 게임 에서는 체스판 바닥이니까 모든 바닥을
    /// 체스판 바닥 같이 만들어주고 언덕도 자연스럽게 파괴가 되게 해줘 벽도 마찬가지로 벽도 임팩트가 있게 파괴 되게끔"). R134 drew it
    /// on one flat quad at the struck floor's height, so it stopped short at a step's edge, was cut off by a wall beside it
    /// and the wall stayed whole. Now the shapes it breaks out are taken away from the world itself: every block of the scene
    /// wears Skill Ground (QueenHillSkillGround.cs), which draws them as real holes in whatever they cut (a floor, a step's
    /// riser, a wall's face), from any angle.
    /// - The crater: a bowl under the struck face. By a step's edge it bites the edge off (pieces of it drop down the step);
    ///   by a face (a step up, a wall) the blast knocks a piece out of that face's foot — a big one out of a wall, cracks
    ///   running up it, chunks of it flying out, dust, a harder shake.
    /// - The four blades: each follows the floor out along its arm (up a slope, over an edge and down in a falling curve, on
    ///   along the lower floor), ploughing its trench behind it the whole way — over the edge and on below, the edge's corner
    ///   chipped where it went over, a dent where it lands. A face in its way stops it: it slams into it and gashes it (chunks,
    ///   sparks, cracks) and stays stuck in it.
    /// - Chips of whatever broke (a chessboard square's cream or walnut and the board's body under it, a wall's own colour),
    ///   broken plates of the face round the crater, sparks. It all stays a while, then heals.
    /// </summary>
    public partial class QueenHillSkillFx
    {
        /// <summary>The crater's radius and depth, the trenches' half width and depth (m); how long it all stays once the
        /// blades stop and how long it takes to heal (s).</summary>
        const float CraterRadius = 0.55f, CraterDepth = 0.24f, TrenchHalfWidth = 0.1f, TrenchDepth = 0.13f, DigHold = 2.2f, DigHeal = 0.6f;
        /// <summary>A blade's way is followed every PathStep m; a floor more than StepUp over it is a face that stops it; off an
        /// edge it falls on a curve this steep (m down per m² along); its front is BladeFront ahead of its middle.</summary>
        const float PathStep = 0.05f, StepUp = 0.3f, FallCurve = 7f, BladeFront = 0.26f;
        /// <summary>A face this high over its foot (m) is a wall (it breaks big); a lower one is a step.</summary>
        const float WallHeight = 1.2f;
        /// <summary>How many broken shapes, strikes and crack stars Skill Ground takes at once (its arrays' sizes).</summary>
        const int MaxBreaks = 96, MaxDigs = 4, MaxCracks = 8;

        static readonly Palette SparkColors = new Palette
        {
            main = new Color(3.2f, 2.3f, 1.1f), light = new Color(4f, 3.6f, 2.6f), deep = new Color(2.2f, 1.3f, 0.5f), ink = new Color(2.2f, 1.3f, 0.5f),
        };

        /// <summary>One blade's way out along its arm: where its foot is every PathStep m (on the floor, or in the air falling
        /// off an edge), how far it gets (a face in its way stops it) and what it meets.</summary>
        sealed class Arm
        {
            public Vector3 dir;
            public float stop;
            public readonly List<Vector3> foot = new List<Vector3>();
            public readonly List<bool> air = new List<bool>();
            public readonly List<Bump> bumps = new List<Bump>();
            public bool fell, struck;
        }

        /// <summary>What a blade meets on its way: 0 an edge it goes over (its corner breaks off), 1 a lower floor it lands on
        /// (a dent), 2 a face it runs into (a gash: a step's riser or a wall).</summary>
        sealed class Bump
        {
            public float s;
            public int kind;
            public Vector3 at, normal;
            public bool wall, done;
        }

        /// <summary>A shape broken out of the world, as Skill Ground reads it (_BreakA.._BreakD): kind 0 the crater's bowl, 1 a
        /// run of a trench, 2 a lump knocked out; a ball round it; a lump opens from its time <see cref="at"/> (the strike's
        /// age) over a few frames.</summary>
        sealed class Cut
        {
            public int kind;
            public Vector4 a, b, c, d;
            public Vector3 boundAt;
            public float bound, at;
        }

        sealed class CrackStar
        {
            public Vector3 at, normal;
            public float r0, len, count, seed, from;
        }

        sealed class Dig
        {
            public Vector3 center, fwd;
            public float seed, arm, rim, age, grown, glow, heal;
            public readonly Arm[] arms = new Arm[4];
            public readonly float[] reach = new float[4];
            public readonly Vector3[] blade = new Vector3[4];
            public readonly List<Cut> cuts = new List<Cut>();
            public readonly List<CrackStar> cracks = new List<CrackStar>();
        }

        /// <summary>What the last strike broke, in words (the probe reports it).</summary>
        public static string LastBreak { get; private set; } = "";

        readonly List<Dig> digs = new List<Dig>();
        readonly Vector4[] breakA = new Vector4[MaxBreaks], breakB = new Vector4[MaxBreaks], breakC = new Vector4[MaxBreaks],
            breakD = new Vector4[MaxBreaks], breakE = new Vector4[MaxBreaks];
        readonly Vector4[] digReach = new Vector4[MaxDigs], digState = new Vector4[MaxDigs], digBlade = new Vector4[MaxDigs * 4],
            digRange = new Vector4[MaxDigs], digBound = new Vector4[MaxDigs];
        readonly Vector4[] crackA = new Vector4[MaxCracks], crackB = new Vector4[MaxCracks], crackC = new Vector4[MaxCracks];
        bool breaksOn;

        // ---------------------------------------------------------------- the strike's plan

        /// <summary>A strike at <paramref name="at"/> (on the floor) thrown along <paramref name="yaw"/>, its blades going out
        /// along <paramref name="dirs"/> <paramref name="arm"/> far: their ways found now, the crater and the trenches laid
        /// (each trench cut only as far as its blade has got).</summary>
        Dig NewDig(Vector3 at, Vector3 yaw, Vector3[] dirs, float arm)
        {
            var g = new Dig { center = at, fwd = FlatDir(yaw, Vector3.forward), seed = Random.Range(0f, 40f), arm = arm, rim = CraterRadius };
            for (int k = 0; k < 4; k++)
            {
                g.arms[k] = PlanArm(at, dirs[k], arm);
                CutTrench(g, k);
            }
            // The crater: the cap of a ball under the face, rim radius and depth as set.
            float rho = (CraterRadius * CraterRadius + CraterDepth * CraterDepth) / (2f * CraterDepth);
            Vector3 ball = at + Vector3.up * (rho - CraterDepth);
            g.cuts.Insert(0, new Cut
            {
                kind = 0,
                a = new Vector4(ball.x, ball.y, ball.z, rho),
                b = new Vector4(at.y + 0.02f, g.seed, 0f, at.y),
                boundAt = ball,
                bound = rho * 1.17f + 0.03f,   // (its rim is rough by up to 15 %)
            });
            g.cracks.Add(new CrackStar { at = at, normal = Vector3.up, r0 = CraterRadius, len = 0.55f, count = 7, seed = g.seed, from = 0f });
            digs.Add(g);
            return g;
        }

        void EndDig(Dig g) => digs.Remove(g);

        /// <summary>Not part of the ground: the pieces, loose bodies, triggers.</summary>
        static bool Loose(Collider c)
        {
            if (c == null || c.isTrigger || RagdollPawn.ColliderOwner.ContainsKey(c)) return true;
            var rb = c.attachedRigidbody;
            return rb != null && !rb.isKinematic;
        }

        /// <summary>The highest floor under (x, z) of <paramref name="p"/> no higher than <paramref name="from"/> (world y), or
        /// float.MinValue.</summary>
        static float FloorBelow(Vector3 p, float from)
        {
            p.y = from;
            float best = float.MaxValue, y = float.MinValue;
            foreach (var h in Physics.RaycastAll(p, Vector3.down, 9f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Loose(h.collider) || h.normal.y < 0.5f || h.distance >= best) continue;
                best = h.distance;
                y = h.point.y;
            }
            return y;
        }

        /// <summary>The nearest steep face (a riser, a wall) from <paramref name="from"/> along <paramref name="dir"/> within
        /// <paramref name="dist"/>.</summary>
        static bool FaceAhead(Vector3 from, Vector3 dir, float dist, out RaycastHit face)
        {
            face = default;
            float best = float.MaxValue;
            bool found = false;
            foreach (var h in Physics.RaycastAll(from, dir, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                if (Loose(h.collider) || Mathf.Abs(h.normal.y) > 0.6f || h.distance >= best) continue;
                best = h.distance;
                face = h;
                found = true;
            }
            return found;
        }

        /// <summary>How high the face at <paramref name="hit"/> stands over <paramref name="footY"/> (m): its top, found from
        /// above just inside it.</summary>
        static float FaceHeight(RaycastHit hit, float footY)
        {
            Vector3 p = hit.point - hit.normal * 0.05f;
            float top = FloorBelow(p, footY + 6f);
            return top == float.MinValue ? 0f : top - footY;
        }

        /// <summary>One blade's way from <paramref name="c"/> along <paramref name="dir"/>: on the floor (up a slope), off an
        /// edge in a falling curve and on along the floor it lands on, until a face stops it or the arm ends. Off an edge near
        /// the arm's end it falls on past it until it lands (it stood in the air beside the step).</summary>
        static Arm PlanArm(Vector3 c, Vector3 dir, float arm)
        {
            var a = new Arm { dir = dir, stop = arm };
            float y = c.y, edgeY = c.y, edgeS = 0f;
            bool falling = false;
            a.foot.Add(c);
            a.air.Add(false);
            for (float s = PathStep; s <= arm + 1.2f; s += PathStep)
            {
                if (s > arm + 1e-4f && !falling) break;
                a.stop = Mathf.Max(arm, s);
                Vector3 prev = a.foot[a.foot.Count - 1];
                // A face in the way at the blade's height: it runs into it (its front a little into it).
                if (FaceAhead(prev + Vector3.up * 0.16f, dir, PathStep + BladeFront, out var face)
                    || FaceAhead(prev + Vector3.up * 0.42f, dir, PathStep + BladeFront, out face))
                {
                    a.stop = Mathf.Clamp(s - PathStep + face.distance - BladeFront + 0.07f, 0.1f, a.stop);
                    a.bumps.Add(new Bump { s = a.stop, kind = 2, at = face.point, normal = FlatDir(face.normal, -dir), wall = FaceHeight(face, y) > WallHeight });
                    return a;
                }
                Vector3 p = c + dir * s;
                float g = FloorBelow(p, y + StepUp);
                if (g == float.MinValue) g = y - 6f;
                if (!falling && g < y - 0.04f)
                {
                    // Over an edge: it goes on along its way, falling.
                    falling = true;
                    edgeY = y;
                    edgeS = s - PathStep;
                    a.bumps.Add(new Bump { s = edgeS, kind = 0, at = new Vector3(prev.x, y, prev.z), normal = dir });
                }
                if (falling)
                {
                    float x = s - edgeS, fy = edgeY - FallCurve * x * x;
                    if (fy <= g)
                    {
                        falling = false;
                        y = g;
                        a.bumps.Add(new Bump { s = s, kind = 1, at = new Vector3(p.x, g, p.z), normal = Vector3.up });
                    }
                    else y = fy;
                }
                else y = g;
                a.foot.Add(new Vector3(p.x, y, p.z));
                a.air.Add(falling);
                if (s > arm && !falling) break;   // landed past the arm's end: it stops there
            }
            a.stop = Mathf.Min(a.stop, (a.foot.Count - 1) * PathStep);
            return a;
        }

        /// <summary>Where blade <paramref name="a"/>'s foot is <paramref name="r"/> m out, and whether it is in the air there.</summary>
        static Vector3 FootAt(Arm a, float r, out bool air)
        {
            int n = a.foot.Count;
            float f = r / PathStep;
            if (n < 2 || f >= n - 1)
            {
                air = a.air[n - 1];
                return a.foot[n - 1] + a.dir * Mathf.Max(0f, r - (n - 1) * PathStep);
            }
            int i = Mathf.Max(0, (int)f);
            air = a.air[i + 1];
            return Vector3.Lerp(a.foot[i], a.foot[i + 1], f - i);
        }

        /// <summary>The trench blade <paramref name="k"/> ploughs: runs of its way (split where it bends: over an edge, down
        /// its fall, onto the floor below), its bottom the blade's foot less the trench's depth, narrower and shallower out
        /// toward the arm's end.</summary>
        void CutTrench(Dig g, int k)
        {
            var a = g.arms[k];
            var pts = a.foot;
            int n = pts.Count;
            if (n < 2) return;
            // The way to where it stops (into a face it ends a bit past the last point).
            var way = new List<Vector3>(pts);
            var at = new List<float>();
            for (int i = 0; i < n; i++) at.Add(i * PathStep);
            if (a.stop > at[n - 1] + 0.01f)
            {
                way.Add(pts[n - 1] + a.dir * (a.stop - at[n - 1]));
                at.Add(a.stop);
            }
            int m = way.Count;
            float Slope(int i) => (way[i].y - way[i - 1].y) / Mathf.Max(1e-4f, at[i] - at[i - 1]);
            bool Air(int i) => i < n && a.air[i];
            // Where a run ends: where it goes over an edge and where it lands, where the floor's slope changes; its fall is one
            // straight run (there is only air under it, but the edge's corner and where it lands).
            var ends = new List<int> { 0 };
            for (int i = 1; i < m - 1; i++)
            {
                bool here = Air(i), next = Air(i + 1);
                if (here && next) continue;
                if (here != next || Mathf.Abs(Slope(i + 1) - Slope(i)) > 0.12f) ends.Add(i);
            }
            ends.Add(m - 1);
            for (int e = 0; e + 1 < ends.Count; e++) TrenchRun(g, k, way[ends[e]], way[ends[e + 1]], at[ends[e]], at[ends[e + 1]]);
        }

        void TrenchRun(Dig g, int k, Vector3 p0, Vector3 p1, float s0, float s1)
        {
            if (s1 - s0 < 1e-3f) return;
            float t0 = s0 / g.arm, t1 = s1 / g.arm;
            float d0 = TrenchDepth * Mathf.Lerp(1f, 0.58f, t0), d1 = TrenchDepth * Mathf.Lerp(1f, 0.58f, t1);
            float w0 = TrenchHalfWidth * Mathf.Lerp(1f, 0.62f, t0), w1 = TrenchHalfWidth * Mathf.Lerp(1f, 0.62f, t1);
            Vector3 a = p0 - Vector3.up * d0, b = p1 - Vector3.up * d1;
            float high = Mathf.Abs(p0.y - p1.y) + Mathf.Max(d0, d1) + 0.12f;
            Vector3 mid = (a + b) * 0.5f + Vector3.up * (high * 0.5f);
            g.cuts.Add(new Cut
            {
                kind = 1,
                a = new Vector4(a.x, a.y, a.z, w0),
                b = new Vector4(b.x, b.y, b.z, w1),
                c = new Vector4(high, k, s0, s1),
                d = new Vector4(g.seed * 1.7f + 3f + k * 0.37f, 0f, d0, d1),
                boundAt = mid,
                bound = Mathf.Sqrt(Mathf.Pow(Vector3.Distance(a, b) * 0.5f, 2f) + Mathf.Pow(Mathf.Max(w0, w1) * 1.25f, 2f) + Mathf.Pow(high * 0.5f + Mathf.Abs(a.y - b.y) * 0.5f, 2f)) + 0.03f,
            });
        }

        /// <summary>A lump knocked out round <paramref name="mid"/>: its axes (across, up, in) and their radii; it opens at the
        /// strike's age <paramref name="opens"/>.</summary>
        static Cut Lump(Vector3 mid, Vector3 x, Vector3 y, Vector3 z, Vector3 radii, float opens)
        {
            float least = Mathf.Min(radii.x, Mathf.Min(radii.y, radii.z));
            Vector4 Axis(Vector3 v, float r) => new Vector4(v.x / r, v.y / r, v.z / r, 0f);
            var c = new Cut { kind = 2, at = opens };
            c.a = new Vector4(mid.x, mid.y, mid.z, Random.Range(0f, 40f));
            c.b = Axis(x.normalized, radii.x);
            c.c = Axis(y.normalized, radii.y);
            c.c.w = least;
            c.d = Axis(z.normalized, radii.z);
            c.boundAt = mid;
            c.bound = Mathf.Max(radii.x, Mathf.Max(radii.y, radii.z)) * 1.28f + 0.03f;
            return c;
        }

        // ---------------------------------------------------------------- what it breaks as it goes

        /// <summary>The faces round the crater, as it slams in: a step up or a wall right beside it gets a piece knocked out of
        /// its foot (a big one out of a wall: cracks run up it, chunks fly, dust, a harder shake); an edge beside it has its
        /// pieces drop down the step.</summary>
        void Breakaway(Dig g, RagdollPawn by, float stay)
        {
            Vector3 c = g.center;
            var nearest = new Dictionary<Collider, RaycastHit>();
            for (int k = 0; k < 16; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                foreach (float up in new[] { 0.12f, 0.4f })
                    if (FaceAhead(c + Vector3.up * up, dir, g.rim + 0.35f, out var hit)
                        && (!nearest.TryGetValue(hit.collider, out var was) || hit.distance < was.distance))
                        nearest[hit.collider] = hit;
            }
            int walls = 0, steps = 0;
            foreach (var h in nearest.Values)
            {
                float high = FaceHeight(h, c.y);
                if (high < 0.12f) continue;
                bool wall = high > WallHeight;
                if (wall) walls++;
                else steps++;
                Vector3 n = FlatDir(h.normal, (c - h.point).normalized);
                Vector3 across = Vector3.Cross(Vector3.up, n).normalized;
                Vector3 foot = new Vector3(h.point.x, c.y, h.point.z);
                Vector3 radii = wall ? new Vector3(0.78f, 0.64f, 0.4f) : new Vector3(0.44f, Mathf.Min(0.32f, high * 0.55f), 0.27f);
                Vector3 mid = foot - n * (wall ? 0.1f : 0.06f) + Vector3.up * (wall ? 0.3f : radii.y * 0.5f);
                g.cuts.Add(Lump(mid, across, Vector3.up, -n, radii, 0.02f));
                g.cracks.Add(new CrackStar
                {
                    at = foot + Vector3.up * (wall ? 0.32f : 0.14f), normal = n, r0 = wall ? 0.58f : 0.32f, len = wall ? 1.05f : 0.45f,
                    count = wall ? 9 : 6, seed = Random.Range(0f, 40f), from = 0.02f,
                });
                var pals = Debris(foot + Vector3.up * 0.25f, n);
                Chips(mid + n * (radii.z + 0.05f), n + Vector3.up * 0.5f, wall ? 10 : 5, wall ? 4.6f : 3.2f, 0.55f, pals, stay, wall ? 0.11f : 0.07f, wall ? 0.32f : 0.18f);
                PuffBurst(foot + n * 0.35f + Vector3.up * (wall ? 0.45f : 0.25f), DustOf(pals), wall ? 6 : 3, wall ? 0.6f : 0.35f, wall ? 0.4f : 0.28f, 0.7f, 0.35f);
            }
            if (walls > 0)
            {
                // A wall breaking: it is felt.
                Shake(by, null, 0.3f, 14);
            }
            // An edge round it: pieces of the edge drop down the step.
            int drops = 0;
            for (int k = 0; k < 12 && drops < 3; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, k * 30f + 15f, 0f) * Vector3.forward;
                Vector3 p = c + dir * (g.rim * 0.95f);
                float below = FloorBelow(p, c.y + 0.1f);
                if (below > c.y - 0.3f) continue;
                drops++;
                Vector3 edge = new Vector3(p.x, c.y - 0.04f, p.z);
                Chips(edge, dir * 0.8f + Vector3.up * 0.4f, 3, 2f, 0.4f, Debris(new Vector3(p.x, c.y, p.z) - dir * 0.2f, Vector3.up), stay, 0.08f, 0.2f);
                PuffBurst(edge + dir * 0.3f - Vector3.up * 0.35f, DustOf(Debris(edge - dir * 0.2f, Vector3.up)), 2, 0.3f, 0.26f, 0.6f, -0.1f);
            }
            int fell = 0, stuck = 0;
            foreach (var a in g.arms)
                foreach (var b in a.bumps)
                {
                    if (b.kind == 1) fell++;
                    if (b.kind == 2) stuck++;
                }
            LastBreak = $"구덩이 (높이 {c.y:0.0} m){(walls > 0 ? $" · 옆 벽 {walls}개 크게 깨짐" : "")}{(steps > 0 ? $" · 옆 턱 {steps}개 깨짐" : "")}"
                + $"{(drops > 0 ? $" · 가장자리 무너짐 {drops}곳" : "")} · 칼날 4개: 턱 아래로 떨어져 계속 {fell} · 벽·턱에 박힘 {stuck} · 홈 {g.cuts.FindAll(x => x.kind == 1).Count}토막";
        }

        /// <summary>Blade <paramref name="k"/> meets <paramref name="b"/> on its way: an edge (its corner breaks off and drops),
        /// a landing (a dent, dust), a face (a gash in it, chunks and sparks flying back, cracks; a wall is felt).</summary>
        void Bumped(Dig g, int k, Bump b, RagdollPawn by, float stay)
        {
            var arm = g.arms[k];
            Vector3 dir = arm.dir, across = Vector3.Cross(Vector3.up, dir).normalized;
            switch (b.kind)
            {
                case 0:
                {
                    // Over the edge: its corner goes with the blade.
                    Vector3 corner = b.at + dir * 0.06f;
                    g.cuts.Add(Lump(corner - Vector3.up * 0.05f, across, Vector3.up, dir, new Vector3(0.17f, 0.15f, 0.14f), g.age));
                    var pals = Debris(b.at - dir * 0.1f, Vector3.up);
                    Chips(corner, dir + Vector3.up * 0.3f, 3, 1.8f, 0.45f, pals, stay, 0.06f, 0.14f);
                    break;
                }
                case 1:
                {
                    // Lands on the floor below: a dent, dust.
                    g.cuts.Add(Lump(b.at - Vector3.up * 0.02f, across, Vector3.up, dir, new Vector3(0.17f, 0.09f, 0.2f), g.age));
                    var pals = Debris(b.at, Vector3.up);
                    Chips(b.at + Vector3.up * 0.05f, Vector3.up + dir * 0.4f, 3, 2.4f, 0.6f, pals, stay, 0.05f, 0.1f);
                    PuffBurst(b.at + Vector3.up * 0.12f, DustOf(pals), 2, 0.25f, 0.24f, 0.5f, 0.2f);
                    Sparks(b.at + Vector3.up * 0.05f, dir, 4);
                    arm.fell = true;
                    break;
                }
                default:
                {
                    // Into a face: it gashes it and sticks.
                    Vector3 n = b.normal, side = Vector3.Cross(Vector3.up, n).normalized;
                    Vector3 foot = FootAt(arm, b.s, out _);
                    Vector3 hit = new Vector3(b.at.x, foot.y + 0.22f, b.at.z);
                    Vector3 radii = b.wall ? new Vector3(0.27f, 0.34f, 0.23f) : new Vector3(0.21f, 0.24f, 0.18f);
                    g.cuts.Add(Lump(hit - n * 0.07f, side, Vector3.up, -n, radii, g.age));
                    g.cracks.Add(new CrackStar { at = hit, normal = n, r0 = 0.17f, len = b.wall ? 0.6f : 0.38f, count = 6, seed = Random.Range(0f, 40f), from = g.age });
                    var pals = Debris(hit, n);
                    Chips(hit + n * 0.12f, n + Vector3.up * 0.6f, b.wall ? 6 : 4, b.wall ? 3.6f : 2.8f, 0.5f, pals, stay, 0.06f, b.wall ? 0.2f : 0.14f);
                    Sparks(hit + n * 0.05f, n, 7, 3.4f);
                    PuffBurst(hit + n * 0.25f, DustOf(pals), b.wall ? 3 : 2, 0.3f, 0.26f, 0.55f, 0.25f);
                    if (b.wall) Shake(by, null, 0.1f, 6);
                    arm.struck = true;
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- debris

        /// <summary>What a block is made of at <paramref name="at"/> (its face facing <paramref name="normal"/>), as chips: the
        /// face's colour there (a square's on the chessboard, and its neighbour's), and its inside.</summary>
        static Palette[] Debris(Vector3 at, Vector3 normal)
        {
            QueenHillGround.ColorAt(at, normal, out var face, out var inside, out var other);
            return new[] { PaletteOf(face), PaletteOf(face), PaletteOf(other), PaletteOf(inside) };
        }

        static Palette PaletteOf(Color c)
        {
            c.a = 1f;
            Color deep = c * 0.6f, ink = Color.Lerp(c * 0.3f, new Color(0.13f, 0.1f, 0.08f), 0.5f);
            deep.a = ink.a = 1f;
            return new Palette { main = c, light = Color.Lerp(c, Color.white, 0.32f), deep = deep, ink = ink };
        }

        /// <summary>Dust the colour of what broke (paler).</summary>
        static Palette DustOf(Palette[] pals)
        {
            var c = Color.Lerp(pals[0].main, new Color(0.93f, 0.9f, 0.84f), 0.55f);
            return PaletteOf(c);
        }

        /// <summary>
        /// <paramref name="n"/> chips of what broke thrown from <paramref name="at"/> along <paramref name="dir"/> (spread by
        /// <paramref name="spread"/>) at about <paramref name="speed"/> m/s, sized <paramref name="small"/>..
        /// <paramref name="big"/> m, each from one of <paramref name="pals"/>: rough lumps tumbling, bouncing once, lying where
        /// they land until <paramref name="stay"/> s, then sinking away.
        /// </summary>
        void Chips(Vector3 at, Vector3 dir, int n, float speed, float spread, Palette[] pals, float stay, float small = 0.05f, float big = 0.12f)
        {
            var mesh = RoundBox(new Vector3(1f, 0.55f, 0.8f), 0.12f);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
            for (int i = 0; i < n; i++)
            {
                var chip = new Prop(this, mesh, pals[Random.Range(0, pals.Length)], "Broken chip") { inkWidth = 0.006f };
                Vector3 v = (dir + Random.insideUnitSphere * spread).normalized * speed * Random.Range(0.55f, 1.1f);
                if (v.y < 0.6f) v.y = 0.6f + Random.value * 1.2f;
                float size = Random.Range(small, big);
                Vector3 shape = new Vector3(Random.Range(0.75f, 1.25f), Random.Range(0.6f, 1.1f), Random.Range(0.75f, 1.25f));
                Vector3 p = at;
                Quaternion rot = Random.rotation;
                Vector3 spin = Random.onUnitSphere * Random.Range(400f, 1000f) * Mathf.Lerp(1f, 0.5f, (size - 0.05f) / 0.3f);
                bool landed = false, bounced = false;
                float life = Mathf.Max(0.5f, stay) + 0.35f;
                var f = Run(life, (fx, d) =>
                {
                    if (!landed)
                    {
                        v += Physics.gravity * d;
                        p += v * d;
                        rot = Quaternion.AngleAxis(spin.magnitude * d, spin.normalized) * rot;
                        float floor = FloorUnder(p + Vector3.up * 0.15f, 0.3f).y + size * 0.25f;
                        if (p.y <= floor && v.y < 0f)
                        {
                            p.y = floor;
                            if (!bounced && v.y < -2f)
                            {
                                bounced = true;
                                v = new Vector3(v.x * 0.45f, -v.y * 0.3f, v.z * 0.45f);
                                spin *= 0.4f;
                            }
                            else
                            {
                                landed = true;
                                rot = Quaternion.Euler(Random.Range(-12f, 12f), rot.eulerAngles.y, Random.Range(-12f, 12f));
                            }
                        }
                    }
                    float sink = fx.age > life - 0.35f ? (fx.age - (life - 0.35f)) / 0.35f : 0f;
                    chip.t.SetPositionAndRotation(p - Vector3.up * (size * 0.4f * sink), rot);
                    chip.t.localScale = shape * (size * (1f - 0.7f * sink) * Mathf.Min(1f, fx.age / (2f * F) + 0.3f));
                    chip.Apply();
                    return true;
                });
                f.props.Add(chip);
            }
        }

        /// <summary>Broken plates of the face round the crater: flat pieces pushed up and tilted out (their inner edges down in
        /// it), popping up as it breaks, lying until <paramref name="stay"/> s, then sinking back. Over an edge there is no
        /// face for them.</summary>
        void RimSlabs(Dig g, float stay)
        {
            var mesh = RoundBox(new Vector3(1f, 0.16f, 0.75f), 0.05f);
            const int n = 7;
            Vector3 c = g.center;
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float ang = turn + i * 360f / n + Random.Range(-14f, 14f);
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                Vector3 at = FloorUnder(c + dir * (g.rim * Random.Range(0.88f, 1.04f)) + Vector3.up * 0.3f, 0.6f);
                if (Mathf.Abs(at.y - c.y) > 0.04f) continue;
                var pal = PaletteOf(QueenHillGround.SquareAt(at));
                var slab = new Prop(this, mesh, pal, "Floor slab") { inkWidth = 0.008f };
                float size = Random.Range(0.2f, 0.3f), tilt = Random.Range(18f, 38f), roll = Random.Range(-12f, 12f), yaw = Random.Range(-20f, 20f);
                float life = stay + 0.35f;
                var f = Run(life, (fx, d) =>
                {
                    float a = fx.age / F;
                    // Up in four frames, past it and back (the floor breaking), then still.
                    float up = a < 4f ? EaseOutBack(a / 4f) : 1f;
                    float sink = fx.age > stay ? (fx.age - stay) / 0.35f : 0f;
                    Quaternion rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(-tilt * up, yaw, roll * up);
                    slab.t.SetPositionAndRotation(at + Vector3.up * (0.012f - 0.08f * sink) + dir * 0.03f, rot);
                    slab.t.localScale = Vector3.one * (size * (1f - 0.5f * sink));
                    slab.Apply();
                    return true;
                });
                f.props.Add(slab);
            }
        }

        /// <summary><paramref name="n"/> sparks flying from <paramref name="at"/> along <paramref name="dir"/> (and up): hot
        /// streaks stretched along their flight, gone in a few frames.</summary>
        void Sparks(Vector3 at, Vector3 dir, int n, float speed = 3.2f)
        {
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.up;
            for (int i = 0; i < n; i++)
            {
                var spark = new Prop(this, meshBall, SparkColors, "Spark") { inkWidth = 0f };
                Vector3 v = dir * Random.Range(0.6f, 1.2f) * speed + Vector3.up * Random.Range(0.8f, 2.6f) + Random.insideUnitSphere * 1.4f;
                Vector3 p = at;
                var f = Run(Random.Range(0.14f, 0.3f), (fx, d) =>
                {
                    v += Physics.gravity * 0.6f * d;
                    p += v * d;
                    float k = fx.age / fx.life, w = 0.022f * (1f - k);
                    spark.t.SetPositionAndRotation(p, Quaternion.LookRotation(v.sqrMagnitude > 1e-6f ? v : Vector3.up));
                    spark.t.localScale = new Vector3(w, w, w + 0.07f * Mathf.Min(1f, v.magnitude / 4f) * (1f - k));
                    spark.Apply();
                    return true;
                });
                f.props.Add(spark);
            }
        }

        // ---------------------------------------------------------------- to the ground's shader

        /// <summary>Every frame: what all the strikes have broken so far, to Skill Ground (its arrays and CF_BREAKS).</summary>
        void UploadBreaks()
        {
            int n = 0, nc = 0, slot = 0;
            for (int i = 0; i < MaxDigs; i++) digRange[i] = Vector4.zero;
            foreach (var g in digs)
            {
                if (slot >= MaxDigs) break;
                int first = n;
                // A ball round all its shapes and the grit round them (Skill Ground skips the strike outside it).
                Vector3 mid = g.center + Vector3.up * 0.3f;
                float reach = 0.5f;
                digReach[slot] = new Vector4(g.reach[0], g.reach[1], g.reach[2], g.reach[3]);
                digState[slot] = new Vector4(Mathf.Clamp01(g.heal), Mathf.Clamp01(g.glow), Mathf.Clamp01(g.grown), 0f);
                for (int i = 0; i < 4; i++) digBlade[slot * 4 + i] = g.blade[i];
                foreach (var c in g.cuts)
                {
                    if (n >= MaxBreaks) break;
                    Vector4 b = c.b, d = c.d;
                    if (c.kind == 2)
                    {
                        float o = (g.age - c.at) / 0.07f;
                        if (o <= 0f) continue;
                        b.w = slot;
                        d.w = EaseOut(o);
                    }
                    else if (c.kind == 1) d.y = slot;
                    else b.z = slot;
                    breakA[n] = c.a;
                    breakB[n] = b;
                    breakC[n] = c.c;
                    breakD[n] = d;
                    breakE[n] = new Vector4(c.boundAt.x, c.boundAt.y, c.boundAt.z, c.kind * 100f + Mathf.Min(c.bound, 99f));
                    reach = Mathf.Max(reach, Vector3.Distance(mid, c.boundAt) + c.bound);
                    n++;
                }
                digRange[slot] = new Vector4(first, n - first, 0f, 0f);
                digBound[slot] = new Vector4(mid.x, mid.y, mid.z, reach + 0.4f);
                foreach (var k in g.cracks)
                {
                    if (nc >= MaxCracks) break;
                    float grow = (g.age - k.from) / 0.12f;
                    if (grow <= 0f) continue;
                    crackA[nc] = new Vector4(k.at.x, k.at.y, k.at.z, k.r0);
                    crackB[nc] = new Vector4(k.normal.x, k.normal.y, k.normal.z, k.len);
                    crackC[nc] = new Vector4(k.count, k.seed, slot, EaseOut(grow) * g.grown);
                    nc++;
                }
                slot++;
            }
            bool on = n > 0;
            if (on || breaksOn)
            {
                Shader.SetGlobalFloat("_BreakCount", n);
                Shader.SetGlobalFloat("_CrackCount", nc);
                Shader.SetGlobalVectorArray("_BreakA", breakA);
                Shader.SetGlobalVectorArray("_BreakB", breakB);
                Shader.SetGlobalVectorArray("_BreakC", breakC);
                Shader.SetGlobalVectorArray("_BreakD", breakD);
                Shader.SetGlobalVectorArray("_BreakE", breakE);
                Shader.SetGlobalVectorArray("_DigReach", digReach);
                Shader.SetGlobalVectorArray("_DigState", digState);
                Shader.SetGlobalVectorArray("_DigBlade", digBlade);
                Shader.SetGlobalVectorArray("_DigRange", digRange);
                Shader.SetGlobalVectorArray("_DigBound", digBound);
                Shader.SetGlobalVectorArray("_CrackA", crackA);
                Shader.SetGlobalVectorArray("_CrackB", crackB);
                Shader.SetGlobalVectorArray("_CrackC", crackC);
            }
            if (on == breaksOn) return;
            breaksOn = on;
            if (on) Shader.EnableKeyword("CF_BREAKS");
            else Shader.DisableKeyword("CF_BREAKS");
        }

        /// <summary>Nothing broken any more (the effects go away).</summary>
        void EndBreaks()
        {
            digs.Clear();
            Shader.SetGlobalFloat("_BreakCount", 0f);
            Shader.SetGlobalFloat("_CrackCount", 0f);
            Shader.DisableKeyword("CF_BREAKS");
            breaksOn = false;
        }
    }
}
