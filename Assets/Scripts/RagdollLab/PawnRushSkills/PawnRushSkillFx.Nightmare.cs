using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 나이트메어 on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.Nightmare.cs; the "!!" restyle after the
    /// spec's per-ultimate file): the knight's rampage of forks, drawn as chess - every leap a knight's move on the board.
    ///
    ///   every screen          lightning crawling over the knight (two arcs at most, quicker as its reach grows) and a red
    ///                         eye; each leap's knight's-L arrow from its square to the landing (teal for its side, red for
    ///                         the other); the landing's red circle closing, bright in its last 0.5 s, the squares under it
    ///                         red, a red warning pool and a beam standing on it from the lock; the landing's move on the
    ///                         card (N×f3 ... quiet ones grey); a fork: the eight knight squares popping, red rays from its
    ///                         head to every forked head, "포크!!" in the top band, the blow's frames and the check's ping
    ///   the knight's screen   its aim as chess.com's to-square on the board with the knight's-L arrow to it, the eight squares
    ///                         round it (the stomp's reach), a red fork preview when two or more stand in it, the aim spot;
    ///                         enemies in reach ringed on the head and on the floor, the fastened one under a thin red pillar
    ///   the signature         the first fork: slowed before it lands when it was foreseen at the lock (two or more within
    ///                         reach of the landing), or after it when nobody saw it coming
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>The knight's tour of the landings from g1: legal knight's moves (f3, e5, f7 ...), one a landing.</summary>
        static readonly string[] NmSquares = { "f3", "e5", "f7", "d8", "e6", "c7", "a8", "b6", "d5", "f6" };

        sealed class NmFx
        {
            public readonly Dictionary<RagdollPawn, Shape> marks = new Dictionary<RagdollPawn, Shape>();
            public readonly Shape[] dots = new Shape[6];
            public readonly List<RagdollPawn> inZone = new List<RagdollPawn>();
            public Vector3 aim;
            public RagdollPawn aimed;
            public bool blocked, haveAim, predicted;
            public float goAt = -10f, air = 1f;
            public int landings;
            public FloorArrow aimArrow;
            public bool aimArrowRed;
            public CellMesh zone, rays;
            public Text3D forkTag;
        }

        static bool NmFlyingNow(UltShow u) => u.by != null && (u.by.PlanNote & RagdollPawn.NmFlying) != 0;

        /// <summary>A point's board square, its middle at the point's own height.</summary>
        Vector3 NmCellAt(Vector3 p)
        {
            Vector3 c = CellFlat(Cell(p));
            c.y = p.y;
            return c;
        }

        /// <summary>The middle of the square the knight stands over, on the floor.</summary>
        Vector3 NmFromCell(UltShow u)
        {
            if (u.by == null) return u.castAt;
            Vector3 hips = u.by.Hips.position;
            float y = BrdGroundBelow(hips, 12f);
            return NmCellAt(new Vector3(hips.x, float.IsNaN(y) ? hips.y - u.by.standHeight : y, hips.z));
        }

        string NmNota(UltShow u, int landing, bool capture, bool fork)
        {
            string sq = NmSquares[(Mathf.Max(1, landing) - 1) % NmSquares.Length];
            string nota = capture ? "N×" + sq + (fork ? "+" : "") : "N" + sq;
            return u.black ? Mirror(nota) : nota;
        }

        void NmCast(UltShow u)
        {
            var f = new NmFx();
            u.state = f;
            NmAura(u);
            if (Viewer != u.by) return;
            BrdBuild();
            float sq = FamSquare;
            Color Tone() => f.blocked ? new Color(0.6f, 0.6f, 0.62f) : f.aimed != null ? Blood : Teal;
            bool AimShown() => u.Playing && f.haveAim && !NmFlyingNow(u);
            // The aim: chess.com's to-square on the board where the camera looks (red when fastened, grey under a roof).
            Add(new Shape(root, "Nm aim fill", meshQuad, matBrdFill)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(sq * 0.96f, sq * 0.96f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    s.bright = AimShown() ? 1f : 0f;
                    if (s.bright <= 0f) return;
                    s.at = NmCellAt(f.aim) + Vector3.up * 0.035f;
                    s.color = HdrA(Tone(), f.aimed != null ? 0.9f : 1.15f);
                    s.paint = f.aimed != null ? 0.3f : 0.4f;
                },
            });
            Add(new Shape(root, "Nm aim frame", meshQuad, matFrame)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(sq * 0.96f, sq * 0.96f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    s.bright = AimShown() ? 1f : 0f;
                    if (s.bright <= 0f) return;
                    s.at = NmCellAt(f.aim) + Vector3.up * 0.045f;
                    s.color = f.blocked ? new Color(0.6f, 0.6f, 0.62f) : f.aimed != null ? HdrA(Blood, 3f) : HdrA(TealPale, 3f);
                },
            });
            // The leap's arc, six dots.
            for (int i = 0; i < f.dots.Length; i++)
            {
                float k = (i + 1f) / (f.dots.Length + 1f);
                f.dots[i] = Add(new Shape(root, "Nm arc dot", meshQuad, matRing)
                {
                    life = 1e9f, billboard = true,
                    animate = (s, t) =>
                    {
                        if (!u.Playing) { s.life = 0f; return; }
                        bool show = AimShown() && !f.blocked && u.by != null;
                        s.bright = show ? 0.6f + 0.4f * Mathf.Clamp01(Mathf.Sin((k * 3f - Clock * 3f) * Mathf.PI)) : 0f;
                        if (!show) return;
                        Vector3 a = u.by.Hips.position, b = NmCellAt(f.aim) + Vector3.up * 0.9f;
                        float across = Flat(b - a).magnitude, high = Params != null ? Params.nmHigh + across * Params.nmHighPer : 9f;
                        s.at = Vector3.Lerp(a, b, k) + Vector3.up * (4f * high * k * (1f - k));
                        float size = (0.16f + 0.1f * Mathf.Clamp01(across / 60f)) * 1.4f;
                        s.scale = new Vector3(size, size, 1f);
                        s.color = f.aimed != null ? HdrA(Blood, 2.8f) : HdrA(TealPale, 2.8f);
                    },
                });
            }
            // The eight squares round the aim (the stomp's reach) and the fork preview's red rays.
            f.zone = BrdMesh("Nm stomp squares", matFrame);
            f.rays = BrdMesh("Nm fork rays", matBrdLineRed);
            UltAimSpot(u, () => AimShown() ? NmCellAt(f.aim) : (Vector3?)null, Tone);
        }

        /// <summary>On every screen while it plays: lightning crawling over the knight - at most two arcs, one every 0.12 s
        /// (0.09 s at 60 m reach, 0.07 s at 80 m), one to the floor every 0.5 s - and its red eye (a dot on its head, with a
        /// thin red streak; not on its own screen).</summary>
        void NmAura(UltShow u)
        {
            var by = u.by;
            if (by == null) return;
            BrdBuild();
            float next = 0f, floorNext = 0.5f;
            var alive = new float[2];
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!u.Playing || by == null) return false;
                if (dt <= 0f || arc == null) return true;
                int level = by.PlanNote & RagdollPawn.NmLevelMask;
                next -= dt;
                floorNext -= dt;
                if (next > 0f) return true;
                next = level >= 2 ? 0.07f : level == 1 ? 0.09f : 0.12f;
                int slot = alive[0] <= alive[1] ? 0 : 1;
                if (age - alive[slot] < 0.06f && alive[slot] > 0f) return true;   // (two alive already)
                alive[slot] = Mathf.Max(age, 1e-4f);
                Vector3 a = Part(by, AuraParts[Random.Range(0, AuraParts.Length)]) + Random.insideUnitSphere * 0.05f, b;
                if (floorNext <= 0f && by.OnTheFloor)
                {
                    floorNext = 0.5f;
                    b = FloorUnder(by) + Random.insideUnitSphere.normalized * 0.6f;
                }
                else b = Part(by, AuraParts[Random.Range(0, AuraParts.Length)]) + Random.insideUnitSphere * 0.25f;
                if ((b - a).sqrMagnitude < 0.02f) b = a + Random.onUnitSphere * 0.35f;
                arc.Strike(a, b, HdrA(Electric, 2.2f), Random.Range(0.05f, 0.08f), 0.06f, 1f, 0.3f, 1);
                return true;
            }) { tick = UltTick.Show });
            if (Viewer == by) return;
            Vector3 Eye()
            {
                Vector3 fwd = Flat(by.Facing).sqrMagnitude > 1e-4f ? Flat(by.Facing).normalized : Vector3.forward;
                return HeadOf(by) + fwd * 0.2f + Vector3.up * 0.04f;
            }
            Add(new Shape(root, "Nm eye", meshQuad, matBrdDotRed)
            {
                life = 1e9f, billboard = true, scale = new Vector3(0.14f, 0.14f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing || by == null) { s.life = 0f; return; }
                    s.at = Eye();
                    s.color = HdrA(Blood, 3f);
                    s.bright = 0.85f + 0.15f * Mathf.Sin(Clock * 9f);
                },
            });
            anims.Add(new Streak(root, matTrail, HdrA(Blood, 2f), 0.05f, 0.25f, () => by != null && u.Playing ? Eye() : (Vector3?)null));
        }

        void NmStep(UltShow u, float dt, Camera cam)
        {
            if (!(u.state is NmFx f) || u.by == null) return;
            var s = Params;
            if (s == null || Viewer != u.by || !u.Playing)
            {
                NmClear(f);
                return;
            }
            float reach = RagdollPawn.NmRange(s, u.by.PlanNote & RagdollPawn.NmLevelMask);
            f.haveAim = cam != null;
            if (cam != null && !NmFlyingNow(u))
                f.aim = RagdollPawn.NmAim(u.by, ViewAim(cam), reach, s, u.struckSet, out f.aimed, out f.blocked);
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == u.by || !Teams.AreEnemies(e.Team, u.by.Team) || f.marks.ContainsKey(e)) continue;
                f.marks[e] = NmMark(u, f, e);
            }
            bool shown = f.haveAim && !NmFlyingNow(u) && !f.blocked;
            // The knight's-L arrow from its square to the aim, teal or red when fastened (a new one when that changes).
            bool red = f.aimed != null;
            if (shown && (f.aimArrow == null || f.aimArrowRed != red))
            {
                bool want = red;
                f.aimArrowRed = red;
                f.aimArrow = UltArrow(() => NmFromCell(u), () => NmCellAt(f.aim), red ? Blood : Teal, 0.8f, true, 1e9f,
                                      () => u.Playing && f.haveAim && !NmFlyingNow(u) && !f.blocked && (f.aimed != null) == want);
            }
            if (!shown) f.aimArrow = null;
            // The eight squares round the aim, the enemies standing in its reach, the fork preview.
            f.inZone.Clear();
            Vector3 centre = NmCellAt(f.aim);
            float stomp = s.nmStomp;
            if (shown)
                foreach (var e in RagdollPawn.All)
                    if (e != null && e != u.by && Teams.AreEnemies(e.Team, u.by.Team) && e.State == PawnState.Active && !u.struckSet.Contains(e)
                        && Flat(e.FeetPoint - f.aim).magnitude <= stomp && Mathf.Abs(e.FeetPoint.y - f.aim.y) <= 3f)
                        f.inZone.Add(e);
            if (f.zone == null) f.zone = BrdMesh("Nm stomp squares", matFrame);
            if (f.rays == null) f.rays = BrdMesh("Nm fork rays", matBrdLineRed);
            f.zone.Begin(cam);
            if (shown)
            {
                Vector2Int c0 = Cell(f.aim);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        var c = c0 + new Vector2Int(dx, dz);
                        float fy = BoardFloor(u, c, f.aim.y);
                        if (float.IsNaN(fy)) continue;
                        Vector3 p = CellFlat(c);
                        p.y = fy + 0.05f;
                        f.zone.Quad(p, FamSquare * 0.96f, BrdTimes(Teal, 1f), 1f);
                    }
            }
            f.zone.End();
            bool fork = shown && f.inZone.Count >= 2;
            f.rays.Begin(cam);
            if (fork)
                foreach (var e in f.inZone)
                {
                    Vector3 a = centre + Vector3.up * 0.07f, b = new Vector3(e.FeetPoint.x, e.FeetPoint.y + 0.07f, e.FeetPoint.z);
                    Vector3 d = Flat(b - a);
                    if (d.sqrMagnitude < 0.01f) continue;
                    Vector3 m = new Vector3(d.z, 0f, -d.x).normalized * 0.04f;
                    f.rays.Corners(a - m, b - m, b + m, a + m, BrdTimes(Blood, 2.6f), 1f);
                }
            f.rays.End();
            if (fork && f.forkTag == null && font != null) f.forkTag = new Text3D(root, font, "포크!", HdrA(Teal, 1.3f), 0.4f);
            if (!fork && f.forkTag != null)
            {
                f.forkTag.Destroy();
                f.forkTag = null;
            }
            f.forkTag?.Place(centre + Vector3.up * 1.6f, cam, 1f, 0.9f + 0.1f * Mathf.Sin(Clock * 8f));
        }

        /// <summary>On the knight's screen: a red ring over an enemy's head and chess.com's capture ring at its feet while it is
        /// in reach (dim beyond it, grey under a roof); a thin red pillar of light only over the one fastened on.</summary>
        Shape NmMark(UltShow u, NmFx f, RagdollPawn e)
        {
            // 0 hidden, 1 in reach, 2 beyond it, 3 under a roof; checked a few times a second.
            int state = 0;
            float next = 0f;
            void Check()
            {
                if (Clock < next) return;
                next = Clock + 0.15f;
                var s = Params;
                if (s == null || u.by == null || e == null || e.State != PawnState.Active || u.struckSet.Contains(e)) { state = 0; return; }
                float reach = RagdollPawn.NmRange(s, u.by.PlanNote & RagdollPawn.NmLevelMask);
                float far = Flat(e.Hips.position - u.by.Hips.position).magnitude;
                state = !RagdollPawn.SkyOver(e.FeetPoint, s.nmSky) ? 3 : far <= reach + s.nmSnap ? 1 : 2;
            }
            Add(new Shape(root, "Nm mark pillar", meshCylinder, matPillar)
            {
                life = 1e9f,
                animate = (s, t) =>
                {
                    if (!u.Playing || e == null) { s.life = 0f; return; }
                    Check();
                    s.bright = state == 1 && f.aimed == e ? 1f : 0f;
                    if (s.bright <= 0f) return;
                    s.at = HeadOf(e) + Vector3.up * 0.8f;
                    s.scale = new Vector3(0.12f, 9f, 0.12f);
                    s.color = HdrA(Blood, 3f);
                },
            });
            Add(new Shape(root, "Nm mark capture ring", meshQuad, matBrdRingRed)
            {
                life = 1e9f, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    if (!u.Playing || e == null) { s.life = 0f; return; }
                    Check();
                    s.bright = state == 1 ? (f.aimed == e ? 1f : 0.7f) : 0f;
                    if (s.bright <= 0f) return;
                    Vector3 feet = e.FeetPoint;
                    s.at = new Vector3(feet.x, feet.y + 0.06f, feet.z);
                    float r = 1.9f / 0.86f * (1f + 0.15f * Mathf.Sin(Clock * Mathf.PI * 6f));
                    s.scale = new Vector3(r, r, 1f);
                    s.color = HdrA(Blood, 2.8f);
                },
            });
            return Add(new Shape(root, "Nm mark ring", meshQuad, matBrdRingRed)
            {
                life = 1e9f, billboard = true,
                animate = (s, t) =>
                {
                    if (!u.Playing || e == null) { s.life = 0f; return; }
                    Check();
                    bool aimed = f.aimed == e;
                    s.bright = state == 0 ? 0f : state == 1 ? (aimed ? 1.2f : 0.85f) + 0.2f * Mathf.Sin(Clock * 9f) : 0.4f;
                    if (state == 0) return;
                    float size = state == 1 ? (aimed ? 0.85f : 0.6f) : 0.4f;
                    s.at = HeadOf(e) + Vector3.up * 0.6f;
                    s.scale = new Vector3(size, size, 1f);
                    s.color = state == 3 ? new Color(0.55f, 0.55f, 0.6f) : HdrA(Blood, state == 1 ? 2.4f : 1.2f);
                },
            });
        }

        /// <summary>Before the key: the enemies the first leap would reach, on the knight's screen.</summary>
        void NmPreview(RagdollPawn me, PawnRushSkillParams s, HashSet<RagdollPawn> wanted)
        {
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == me || !Teams.AreEnemies(e.Team, me.Team) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                if (Flat(e.Hips.position - me.Hips.position).magnitude > s.nmRange0 + s.nmSnap) continue;
                if (!RagdollPawn.SkyOver(e.FeetPoint, s.nmSky)) continue;
                wanted.Add(e);
            }
        }

        /// <summary>A leap, on every screen: the take-off (a teal square wave, white and pale sparks, a tock), the knight's-L
        /// arrow to where it comes down, the landing's red circle closing on it and, from the lock (0.5 s before), the squares
        /// under it red, its coordinate, a red pool of light and a beam standing on it. A fork seen coming at the lock (two or
        /// more within reach of the landing) gets the knight's own slow motion.</summary>
        void NmGoFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is NmFx f)) return;
            BrdBuild();
            f.goAt = Clock;
            f.air = Mathf.Max(0.3f, e.size);
            Vector3 from = u.by != null ? FloorUnder(u.by) : e.at;
            UltSquareWave(from, FamSquare * 0.5f, 6f, 0.45f, Teal, 3f);
            SparkBurst(from + Vector3.up * 0.2f, 11, Color.white, Vector3.up, 45f, 4f, 9f, 0.5f, 0.04f);
            SparkBurst(from + Vector3.up * 0.2f, 11, TealPale, Vector3.up, 45f, 4f, 9f, 0.5f, 0.04f);
            UltSound(UltSfx.Leap, from, u.struck);
            UltClip(UltClipId.Tock, 0.6f * UltAudience(u, from));
            float stomp = Params != null ? Params.nmStomp : 3f, radius = stomp * 2f, lockAt = Params != null ? Params.nmLock : 0.5f;
            float goAt = f.goAt, air = f.air;
            bool Flying() => u.by != null && Clock < goAt + air + 0.12f;
            // Its knight's move on the board: teal for its side, red on the other side's screens.
            var me = Viewer;
            bool ours = me == null || u.by == null || !Teams.AreEnemies(me.Team, u.by.Team);
            Vector3 take = NmCellAt(from);
            UltArrow(() => take, () => u.by != null ? NmCellAt(u.by.PlanPoint) : take, ours ? Teal : Blood, 0.8f, true, air + 0.3f, Flying);
            Add(new Shape(root, "Nm landing circle", meshQuad, matBrdRingRed)
            {
                life = air + 0.25f, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    float left = goAt + air - Clock;
                    if (left < -0.05f || u.by == null) { s.bright = 0f; return; }
                    bool locked = left <= lockAt;
                    float grow = locked ? 1f : Mathf.Lerp(1.7f, 1.05f, Mathf.Clamp01(1f - (left - lockAt) / Mathf.Max(0.1f, air - lockAt)));
                    s.at = u.by.PlanPoint + Vector3.up * 0.07f;
                    s.scale = new Vector3(radius * grow, radius * grow, 1f);
                    s.color = HdrA(Blood, locked ? 2.8f : 1.5f);
                    s.bright = locked ? 0.75f + 0.45f * Mathf.Abs(Mathf.Sin(Clock * 18f)) : 0.6f;
                },
            });
            // A red pool of light on the landing, full at the lock.
            if (u.by != null) UltWarnPool(u, () => u.by != null ? u.by.PlanPoint : take, stomp, Mathf.Max(0.1f, air - lockAt), 70f, Flying);
            // From the lock: the squares under the circle red, the landing's coordinate, a red beam standing on it; and the
            // knight's foresight - two or more within reach of the landing - slows its own screen into the fork.
            var cells = BrdMesh("Nm landing squares", matBrdFrameRed);
            bool locked0 = false, slowed = false;
            anims.Add(new Ongoing((age, dt) =>
            {
                float left = goAt + air - Clock;
                if (left < -0.08f || u.by == null || !u.Playing && left < 0f) return false;
                if (left > lockAt) return true;
                Vector3 land = u.by.PlanPoint;
                if (!locked0)
                {
                    locked0 = true;
                    SkyBeam(land, Blood, lockAt + 0.35f, true, lockAt);
                    var c = Cell(land);
                    SquareMark(u, c, Blood, 0f, 0f, Blood, 2.4f, UltCoord(u, c), lockAt + 0.2f, null, land.y);
                    int near = 0;
                    float within = Mathf.Max(0.5f, stomp - 0.4f);
                    foreach (var p in RagdollPawn.All)
                        if (p != null && p != u.by && Teams.AreEnemies(p.Team, u.by.Team) && p.State == PawnState.Active && !u.struckSet.Contains(p)
                            && Flat(p.FeetPoint - land).magnitude <= within) near++;
                    f.predicted = near >= 2 && !u.sigUsed;
                }
                if (f.predicted && !slowed && left <= 0.42f)
                {
                    slowed = true;
                    UltSlow(u, UltSlowKind.SigAnticipate);
                }
                var cam = ViewCamera;
                cells.Begin(cam);
                Vector2Int c0 = Cell(land);
                for (int dx = -2; dx <= 2; dx++)
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        var c = c0 + new Vector2Int(dx, dz);
                        Vector3 p = CellFlat(c);
                        if (Flat(p - land).magnitude > stomp + FamSquare * 0.5f) continue;
                        float fy = BoardFloor(u, c, land.y);
                        if (float.IsNaN(fy)) continue;
                        p.y = fy + 0.05f;
                        cells.Quad(p, FamSquare * 0.96f, BrdTimes(Blood, 2.4f), 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(Clock * 18f)));
                    }
                cells.End();
                return true;
            }, () => BrdKill(cells)));
        }

        /// <summary>A head stomped, on every screen: lightning from it to the floor (three thin arcs) and the knight's stomp; on
        /// a fork, a red ray from the knight's head to the forked head for 0.8 s. The capture itself is the common one
        /// (UltBrilliantStrike); the frames are the burst's (a fork's blow is DriveLand's).</summary>
        void NmStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null) return;
            BrdBuild();
            var target = e.target;
            Vector3 head = HeadOf(target), floor = Ground(target.Hips.position);
            bool fork = e.size > 0.5f;
            if (arc != null)
                for (int i = 0; i < 3; i++)
                    arc.Strike(head, floor + Random.insideUnitSphere.normalized * 1.1f, HdrA(Electric, 2.4f), 0.07f, 0.08f, 1f, 0.3f, 1);
            if (fork && u.by != null)
            {
                var knight = u.by;
                Add(new Shape(root, "Nm fork ray", meshCylinder, matBrdLineRed)
                {
                    life = 0.8f,
                    animate = (s, t) =>
                    {
                        if (knight == null || target == null) { s.bright = 0f; return; }
                        Vector3 a = HeadOf(knight) + Vector3.up * 0.15f, b = HeadOf(target) + Vector3.up * 0.15f, d = b - a;
                        float len = d.magnitude;
                        if (len < 0.05f) { s.bright = 0f; return; }
                        s.at = a;
                        s.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
                        s.scale = new Vector3(0.05f, len, 0.05f);
                        s.color = HdrA(Blood, 3f);
                        s.bright = Mathf.Clamp01(s.Age / 0.04f) * (1f - Smooth01((t - 0.6f) / 0.4f));
                    },
                });
            }
            UltSound(UltSfx.Stomp, head, e.count);
            UltScreenStrike(u, e);
        }

        /// <summary>The landing, on every screen: its move written on the card (a quiet one grey, with the reach it is back to);
        /// a teal square wave and dust; on a fork the blow (its frames, the knight's own slow motion), its eight knight squares
        /// popping round it with the forked ones red, "포크!!" (three or more: "패밀리 포크!!") and the check's ping.</summary>
        void NmLandFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is NmFx f)) return;
            Vector3 floor = Ground(e.at);
            int hit = e.count;
            f.landings++;
            bool fork = hit >= 2;
            UltMove(u, NmNota(u, f.landings, hit > 0, fork), NmCellAt(floor), hit <= 0);
            if (hit <= 0)
            {
                DustRing(floor, 10, 0.6f, 4f, new Color(0.55f, 0.55f, 0.57f), 0.7f, 0.7f);
                var c = Cell(floor);
                SquareMark(u, c, MissGreyTone, 0f, 0f, MissGreyTone, 1.2f, null, 0.5f, null, floor.y);
                if (Viewer == u.by) UltPopWord(floor + Vector3.up * 1.4f, $"빈 칸 · {(Params != null ? Params.nmRange0 : 40f):0} m", new Color(0.72f, 0.72f, 0.76f), 0.45f, 0.9f);
                UltClip(UltClipId.Tock, 0.6f * UltAudience(u, floor), 0.7f);
                UltSound(UltSfx.Off, floor);
                if (f.predicted) UltSlowImpact();   // (a fork foreseen that the enemies ran from)
                f.predicted = false;
                return;
            }
            int level = Mathf.RoundToInt(e.size);
            UltSquareWave(floor, FamSquare * 0.5f, 12f, 0.6f, Teal, 3f);
            DustRing(floor, 12, 1f, 6f, new Color(0.5f, 0.5f, 0.52f), 0.9f, 0.9f);
            if (Viewer == u.by)
            {
                float reach = Params != null ? RagdollPawn.NmRange(Params, level) : 60f;
                UltPopWord(floor + Vector3.up * 1.6f, $"다음 {reach:0} m!", HdrA(Teal, 1.3f), 0.5f, 0.9f);
            }
            if (fork)
            {
                UltBlow(u, ImpactSeq.Blow, floor + Vector3.up * 1.2f);
                if (f.predicted) UltSlowImpact();
                else UltSlow(u, UltSlowKind.SigAftermath);   // (a fork nobody saw coming: slowed after it, once)
                NmForkSquares(u, floor);
                UltTopWord(hit >= 3 ? "패밀리 포크!!" : "포크!!", HdrA(Teal, 1.2f), 56);
                UltClip(UltClipId.Check, 0.8f * UltAudience(u, floor));
            }
            else if (f.predicted) UltSlowImpact();
            f.predicted = false;
            var cam = ViewCamera;
            float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, floor) / 30f) : 0.5f;
            Shake(Viewer == u.by ? 0.16f : 0.12f * near, 0.35f);
        }

        /// <summary>A fork's eight knight squares round the landing popping one after another (0.03 s apart, ×3.0), the ones an
        /// enemy stands on red.</summary>
        void NmForkSquares(UltShow u, Vector3 floor)
        {
            Vector2Int c0 = Cell(floor);
            var mesh = BrdMesh("Nm fork squares", matFrame);
            var red = BrdMesh("Nm fork squares red", matBrdFrameRed);
            var taken = new HashSet<Vector2Int>();
            foreach (var p in u.burst) if (p != null) taken.Add(Cell(p.Hips.position));
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age > 1.1f) return false;
                var cam = ViewCamera;
                mesh.Begin(cam);
                red.Begin(cam);
                for (int i = 0; i < KnightSquares.Length; i++)
                {
                    float a = age - 0.03f * i;
                    if (a < 0f) break;
                    var c = c0 + KnightSquares[i];
                    float fy = BoardFloor(u, c, floor.y);
                    if (float.IsNaN(fy)) continue;
                    Vector3 p = CellFlat(c);
                    p.y = fy + 0.05f;
                    float k = 1f - Smooth01((a - 0.5f) / 0.5f), size = FamSquare * 0.96f * BrdPop(a);
                    if (taken.Contains(c)) red.Quad(p, size, BrdTimes(Blood, 3f), k);
                    else mesh.Quad(p, size, BrdTimes(TealPale, 3f), k);
                }
                mesh.End();
                red.End();
                return true;
            }, () =>
            {
                BrdKill(mesh);
                BrdKill(red);
            }) { tick = UltTick.Show });
        }

        /// <summary>The knight's screen's aim patches gone (the show over, or this PC no longer the knight's).</summary>
        void NmClear(NmFx f)
        {
            BrdKill(f.zone);
            BrdKill(f.rays);
            f.zone = f.rays = null;
            f.forkTag?.Destroy();
            f.forkTag = null;
        }

        string NmHint(UltShow u)
        {
            if (u.by == null) return null;
            byte n = u.by.PlanNote;
            if ((n & RagdollPawn.NmFlying) != 0) return "이동 키 = 착지점 조금 고치기 (빨간 원에서 적이 비키면 놓쳐요)";
            if (u.state is NmFx f && f.blocked) return "하늘이 막힌 곳 — 다른 데를 봐요";
            if ((n & RagdollPawn.NmRiding) != 0) return "머리 위! 좌클릭 = 다음 도약";
            return "좌클릭 = 보는 곳으로 도약 (빨간 표식 가까이면 그 적에게 붙어요)";
        }

        string NmLine(UltShow u)
        {
            float reach = Params != null && u.by != null ? RagdollPawn.NmRange(Params, u.by.PlanNote & RagdollPawn.NmLevelMask) : 40f;
            return $"도약 {reach:0} m  ·  밟은 적 {u.struckSet.Count}";
        }

        string NmWarning(UltShow u, RagdollPawn me)
        {
            if (u.by == null || me == null || !NmFlyingNow(u)) return null;
            float radius = (Params != null ? Params.nmStomp : 3f) + 1f;
            return Flat(me.Hips.position - u.by.PlanPoint).magnitude <= radius ? "나이트가 내려와요 — 빨간 원에서 비켜요!" : null;
        }
    }
}
