using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 승격 (e8=Q!!) on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.PromotionRun.cs; the "!!" restyle
    /// after the spec's per-ultimate file): a pawn race that ends in promotion. And the giant bodies the driven ultimates
    /// share (the pawn's 6 m queen, the king's walk).
    ///
    ///   the run      a FILE of the board ahead of the pawn: two edge lines 3.2 m apart, rank lines across it, the rank
    ///                numbers on its own side's screens, the 8th rank at its end - the promotion square named e8 with a
    ///                hollow ♛ over it and an arrow head; red on the other side's screens with a faint red floor between (their
    ///                lane to leave). Each rank it passes flashes with a tock, the card ticking e3 … e7; whoever it shoves is
    ///                written as a pawn's capture (e×d5, e×f5). On its own screen the edge speed lines and a riser
    ///   the leap     the promotion, its signature: chess.com's promotion picker over the landing (♛ ♞ ♜ ♝; the ♛ chosen and
    ///                swelling into the giant), a thin beam of light, a red pool on the landing, the card's "e8=Q!!"; on its
    ///                own screen the promotion's frames (the queen in ink on a negative world) and its slow motion into the
    ///                landing; the landing's blow, a checker stamp, two square waves, a blow light, "=Q"
    ///   the queen    a teal crown ring turning over her head; each dash's arrow on the floor first (red with its squares on
    ///                the other side's screens, a red pool at its middle), then a thin beam at hip height and the squares
    ///                along it flashing; one move a dash on the card (Q×d7, Q×c6 … a dash that hit nobody grey); a teal ring
    ///                sweeping up her body at each hit as she grows; at the end the crown breaks into motes
    ///   giants       a piece drawn bigger than its body: its skin baked each frame and drawn scaled about its feet (the
    ///                physics stays the piece's own size); the skin itself hidden meanwhile. On every PC, from the plan.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>The queen's dashes as a legal queen's path from e8, one a dash.</summary>
        static readonly string[] PrDashSquares = { "d7", "c6", "b5", "a4", "a1", "h8", "h1", "e4" };

        sealed class PrFx
        {
            public int dashHits, dashes, dashStruck, ranks;
            public float leapAt = -1f, warnAt = -10f, runAt = -1f, rankAt = -10f;
            public Vector3 warnFrom, warnTo;
            public float warnWidth;
            public bool queenShown, slowed;
            public CellMesh lane, laneFill;
            public readonly Text3D[] digits = new Text3D[5];
            public Text3D eight, crownGlyph;
            public Shape crown;
        }

        static int PrStageOf(RagdollPawn p) => p != null ? (p.PlanNote >> RagdollPawn.PrStageShift) & 3 : 0;

        string PrNota(UltShow u, string nota) => u.black ? Mirror(nota) : nota;

        void PrCast(UltShow u)
        {
            var f = new PrFx { runAt = Clock };
            u.state = f;
            BrdBuild();
            // Sparks and dust off its feet while it runs (30 a second at most).
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!u.Playing || u.by == null) return false;
                if (dt <= 0f || PrStageOf(u.by) != RagdollPawn.PrRun) return true;
                Vector3 feet = FloorUnder(u.by), back = -u.by.PlanDir;
                if (Random.value < 30f * dt)
                    sparks.Emit(feet + Random.insideUnitSphere * 0.3f, back * Random.Range(3f, 8f) + Vector3.up * Random.Range(1f, 3f), 0.04f, 0.35f, Tint(Random.value < 0.5f ? Color.white : TealPale, 0.2f));
                if (Random.value < 0.35f) smoke.Emit(feet, back * 2f + Vector3.up, Random.Range(0.4f, 0.7f), 0.5f, Tint(new Color(0.6f, 0.6f, 0.62f), 0.1f));
                return true;
            }));
        }

        void PrStep(UltShow u, float dt, Camera cam)
        {
            if (!(u.state is PrFx f) || u.by == null) return;
            var s = Params;
            int stage = PrStageOf(u.by);
            bool run = u.Playing && stage == RagdollPawn.PrRun && s != null;
            if (run) PrDrawLane(u, f, s, cam);
            else PrLaneOff(f);
            if (!u.Playing || stage < RagdollPawn.PrLeap || s == null)
            {
                PrCrownOff(f, u);
                return;
            }
            float giant = PrGiantScale(u.by, s) * Mathf.Min(s.prGrowMax, 1f + s.prGrow * f.dashHits);
            if (stage == RagdollPawn.PrLeap)
            {
                if (f.leapAt < 0f) f.leapAt = Clock;
                giant = Mathf.Lerp(1f, giant, Smooth01((Clock - f.leapAt) / Mathf.Max(0.1f, s.prLeap)));
                // The signature: slowed on its own screen from 0.13 s into the leap until it lands.
                if (!f.slowed && Clock - f.leapAt >= 0.13f)
                {
                    f.slowed = true;
                    UltSlow(u, UltSlowKind.SigAnticipate);
                }
            }
            WantGiant(u.by, giant, 1f / 0.25f);
            // The promotion's frames on its own screen, once it is a queen: the queen baked for the giant in ink.
            if (!f.queenShown && u.by.Piece == PieceKind.Queen && Viewer == u.by)
            {
                f.queenShown = true;
                UltGiantRebake(u);
                UltGiantAvatar(u, 12f);
                UltFrames(ImpactSeq.Promotion, ChestOf(u.by), u);
            }
            if (stage == RagdollPawn.PrQueen && f.crown == null) f.crown = PrCrown(u, f);
        }

        /// <summary>The run's file ahead of the pawn (every screen), rebuilt as it steers: two edge lines 3.2 m apart, a rank
        /// line across it every sixth of its length (7 m), the promotion square at its end with an arrow head - teal ×2.8 on
        /// its own side's screens with the rank numbers, red ×2.8 with a faint red floor between on the other side's. The
        /// ranks it has passed (by time) flash ×4 with a tock, and the card ticks e3 … e7.</summary>
        void PrDrawLane(UltShow u, PrFx f, PawnRushSkillParams s, Camera cam)
        {
            var me = Viewer;
            bool ours = me == null || !Teams.AreEnemies(me.Team, u.by.Team);
            if (f.lane == null) f.lane = BrdMesh("Pr file", ours ? matBrdLine : matBrdLineRed);
            if (f.laneFill == null && !ours) f.laneFill = BrdMesh("Pr file floor", matBrdFillRed);
            Color colour = ours ? Teal : Blood;
            Vector3 from = FloorUnder(u.by), to = u.by.PlanPoint, d = Flat(to - from);
            float len = d.magnitude;
            if (len < 2f)
            {
                PrLaneOff(f);
                return;
            }
            d /= len;
            Vector3 side = new Vector3(d.z, 0f, -d.x);
            // The ranks passed, by the run's time (it runs from rank 2 to the 8th in its prRun seconds).
            float elapsed = Clock - f.runAt, step = Mathf.Max(0.1f, s.prRun / 6f);
            while (f.ranks < 5 && elapsed >= step * (f.ranks + 1))
            {
                f.ranks++;
                f.rankAt = Clock;
                int rank = 2 + f.ranks;
                UltClip(UltClipId.Tock, 0.6f * UltAudience(u, from), 0.9f + 0.08f * rank);
                UltMove(u, PrNota(u, "e" + rank), from + d * 1.5f);
            }
            float flash = 1f + 3f * Mathf.Exp(-(Clock - f.rankAt) / 0.12f);
            float Y(float x) => Mathf.Lerp(from.y, to.y, Mathf.Clamp01(x / len)) + 0.06f;
            Vector3 P(float x, float across) => new Vector3(from.x + d.x * x + side.x * across, Y(x), from.z + d.z * x + side.z * across);
            f.lane.Begin(cam);
            f.laneFill?.Begin(cam);
            float squareAt = Mathf.Max(0f, len - FamSquare);
            // The two edges, in 4 m pieces.
            int n = Mathf.Clamp(Mathf.CeilToInt(squareAt / 4f), 1, 30);
            for (int i = 0; i < n; i++)
            {
                float x0 = squareAt * i / n, x1 = squareAt * (i + 1) / n;
                foreach (float e in new[] { -1.6f, 1.6f })
                    f.lane.Corners(P(x0, e - 0.03f), P(x1, e - 0.03f), P(x1, e + 0.03f), P(x0, e + 0.03f), BrdTimes(colour, 2.8f), 1f);
                if (f.laneFill != null)
                {
                    BrdFillColour(Blood, 0.9f, 0.15f, 1f, out var rgb, out var alpha);
                    f.laneFill.Corners(P(x0, -1.6f), P(x1, -1.6f), P(x1, 1.6f), P(x0, 1.6f), rgb, alpha);
                }
            }
            // The rank lines (the first, just ahead, flashes as a rank is passed).
            for (int r = 1; r <= 5; r++)
            {
                float x = squareAt * r / 6f, hdr = 2.2f * (r == 1 ? flash : 1f);
                f.lane.Corners(P(x - 0.035f, -3.5f), P(x + 0.035f, -3.5f), P(x + 0.035f, 3.5f), P(x - 0.035f, 3.5f), BrdTimes(colour, Mathf.Min(hdr, 5f)), 1f);
                // (the r-th line ahead is the rank after the ones passed; the 8th is the square at the end)
                if (ours) PrDigit(f, r - 1, (2 + f.ranks + r).ToString(), P(x, -3.9f) + Vector3.up * 0.4f, cam, 2 + f.ranks + r <= 7);
            }
            // The 8th rank: the promotion square, an arrow head over it, its name and a hollow queen.
            Vector3 sq = P(squareAt + FamSquare * 0.5f, 0f);
            f.lane.Quad(sq, FamSquare * 0.96f, BrdTimes(ours ? TealPale : Blood, 3.2f), 1f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            Vector3 hl = P(squareAt - 0.2f, -0.9f), hr = P(squareAt - 0.2f, 0.9f), tip = P(squareAt + 1.4f, 0f);
            f.lane.Corners(hl, tip, tip, hr, BrdTimes(colour, 2.8f), 0.9f);
            f.lane.End();
            f.laneFill?.End();
            if (ours && font != null)
            {
                if (f.eight == null) f.eight = new Text3D(root, font, PrNota(u, "e8"), new Color(1f, 1f, 1f, 0.85f), 0.32f);
                f.eight.Place(sq + Vector3.up * 0.35f + side * -0.7f, cam, 1f, 0.85f);
                if (brdSymbols == null) brdSymbols = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Arial Unicode MS", "DejaVu Sans" }, 64);
                if (f.crownGlyph == null) f.crownGlyph = new Text3D(root, brdSymbols, "♕", HdrA(TealPale, 1.6f), 1.2f);
                f.crownGlyph.Place(sq + Vector3.up * 2f, cam, 1f, 0.9f);
            }
            // Its own screen: the edge speed lines and the riser.
            if (Viewer == u.by)
            {
                UltEdgeLines(0.2f + 0.2f * Mathf.Clamp01(s.prSpeed / 25f));
                UltBed(UltClipId.Riser, 0.35f);
            }
        }

        void PrDigit(PrFx f, int i, string text, Vector3 at, Camera cam, bool show)
        {
            if (font == null) return;
            if (!show)
            {
                f.digits[i]?.Destroy();
                f.digits[i] = null;
                return;
            }
            if (f.digits[i] == null) f.digits[i] = new Text3D(root, font, text, new Color(1.1f, 1.1f, 1.1f, 1f), 0.7f);
            f.digits[i].Place(at, cam, 1f, 0.9f);
        }

        void PrLaneOff(PrFx f)
        {
            BrdKill(f.lane);
            BrdKill(f.laneFill);
            f.lane = f.laneFill = null;
            for (int i = 0; i < f.digits.Length; i++)
            {
                f.digits[i]?.Destroy();
                f.digits[i] = null;
            }
            f.eight?.Destroy();
            f.crownGlyph?.Destroy();
            f.eight = f.crownGlyph = null;
        }

        /// <summary>The giant queen's crown: a teal ring (1.4 m, ×3) turning 40°/s 0.3 m over her drawn head while she reigns.</summary>
        Shape PrCrown(UltShow u, PrFx f)
        {
            var by = u.by;
            return Add(new Shape(root, "Pr crown ring", meshQuad, matRing)
            {
                life = 1e9f,
                animate = (s, t) =>
                {
                    if (by == null || !u.Playing || PrStageOf(by) != RagdollPawn.PrQueen) { s.life = 0f; return; }
                    float scale = GiantScaleOf(by), h = Mathf.Max(1f, GiantBaseHeight(by)) * scale;
                    Vector3 feet = FloorUnder(by);
                    s.at = new Vector3(by.Hips.position.x, feet.y + h + 0.3f, by.Hips.position.z);
                    s.rotation = FlatOnFloor() * Quaternion.Euler(0f, 0f, Clock * 40f);
                    float r = 1.4f / 0.86f;
                    s.scale = new Vector3(r, r, 1f);
                    s.color = HdrA(Teal, 3f);
                    s.bright = Mathf.Clamp01(s.Age / 0.2f);
                },
            });
        }

        /// <summary>The queen's reign over: her crown breaks into teal motes with a soft pop.</summary>
        void PrCrownOff(PrFx f, UltShow u)
        {
            if (f.crown == null) return;
            Vector3 at = u.by != null ? new Vector3(u.by.Hips.position.x, FloorUnder(u.by).y + Mathf.Max(1f, GiantBaseHeight(u.by)) * GiantScaleOf(u.by) + 0.3f, u.by.Hips.position.z) : u.castAt;
            for (int i = 0; i < 40; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 9f, 0f) * Vector3.forward;
                motes.Emit(at + dir * 0.7f, dir * Random.Range(1.2f, 2.6f) + Vector3.up * Random.Range(-0.3f, 0.8f), 0.05f, Random.Range(0.6f, 1.1f), Tint(Teal, 0.2f));
            }
            UltClip(UltClipId.Tap, 0.4f * UltAudience(u, at), 0.8f);
            f.crown = null;
        }

        /// <summary>The queen's 6 m against this piece's drawn height.</summary>
        float PrGiantScale(RagdollPawn p, PawnRushSkillParams s)
        {
            float h = GiantBaseHeight(p);
            return h > 0.3f ? s.prTall / h : 4f;
        }

        /// <summary>The leap to the last rank (DriveState PrLeap), on every screen: chess.com's promotion picker over the
        /// landing, a thin beam of light 30 m up from it (0.9 s), a red pool of light on it (r 10 m, full in 0.55 s), the card's
        /// "e8=Q!!".</summary>
        void PrStateFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is PrFx f)) return;
            if (e.count != RagdollPawn.PrLeap) return;
            f.leapAt = Clock;
            Vector3 land = u.by != null && u.by.PlanPoint != Vector3.zero ? Ground(u.by.PlanPoint) : Ground(e.at);
            PromotionPicker(u, land);
            PrBeam(land, 30f, 0.12f, 0.6f, 0.9f);
            float leap = Params != null ? Params.prLeap : 0.55f, until = Clock + leap + 0.1f;
            UltWarnPool(u, () => land, 10f, leap, 70f, () => Clock < until);
            UltMove(u, PrNota(u, "e8=Q!!"), land);
            UltSquareWave(land, FamSquare * 0.5f, 12f, 0.7f, Teal, 3f);
            UltSound(UltSfx.Boom, land);
        }

        /// <summary>A thin vertical beam of light: a TealPale core ×3.0 in a teal sheath ×1.1, rising in 0.1 s, gone over the
        /// last 0.4 s of its life.</summary>
        void PrBeam(Vector3 foot, float height, float core, float sheath, float life)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                bool inner = layer == 0;
                Add(new Shape(root, inner ? "Pr beam core" : "Pr beam sheath", meshCylinder, matPillar)
                {
                    tick = UltTick.Show, life = life, at = foot,
                    animate = (s, t) =>
                    {
                        float r = inner ? core : sheath, k = Smooth01(s.Age / 0.1f);
                        s.scale = new Vector3(r, height * k, r);
                        s.color = inner ? HdrA(TealPale, 3f) : HdrA(Teal, 1.1f);
                        s.bright = 1f - Smooth01((s.Age - (life - 0.4f)) / 0.4f);
                    },
                });
            }
        }

        /// <summary>The queen lands (DriveLand, the signature's impact), on every screen: the blow (on its own screen the slow
        /// motion let go into it), a 5×5 checker stamp with the to-square teal, a teal square wave to 14 m and a white one to
        /// 20 m, a blow light, "=Q", grey dust and white and pale sparks; the predictable blow's sounds.</summary>
        void PrLandFx(UltShow u, SkillFxEvent e)
        {
            Vector3 floor = Ground(e.at);
            float radius = Mathf.Max(4f, e.size);
            UltBlow(u, ImpactSeq.Blow, floor + Vector3.up * 1.5f);
            UltSlowImpact();
            BoardStamp(floor, 2, 0.05f, 1.6f, Teal, 3f, 0f);
            SquareMark(u, Cell(floor), Teal, 1.15f, 0.42f, TealPale, 3.2f, PrNota(u, "e8"), 1.6f, null, floor.y);
            UltSquareWave(floor, FamSquare * 0.5f, radius * 1.4f, 0.9f, Teal, 3f);
            UltSquareWave(floor, FamSquare * 0.5f, radius * 2f, 1.1f, Color.white, 1.2f, 0.08f);
            UltBlowLight(floor + Vector3.up * 3f);
            UltPopWord(floor + Vector3.up * 3.2f, "=Q", HdrA(Teal, 1.3f), 1.3f, 0.9f);
            DustRing(floor, 22, 1.6f, radius * 0.8f, new Color(0.5f, 0.5f, 0.52f), 1.1f, 1.2f);
            SparkBurst(floor + Vector3.up * 0.5f, 20, Color.white, Vector3.up, 60f, 6f, 16f, 0.9f, 0.045f);
            SparkBurst(floor + Vector3.up * 0.5f, 20, TealPale, Vector3.up, 60f, 6f, 16f, 0.9f, 0.045f);
            float lvl = UltAudience(u, floor);
            UltClip(UltClipId.Crack, 0.9f * lvl);
            UltPlay(sfxImpactLow, 1f * lvl);
            UltPlay(sfxBoom, 0.7f * lvl);
            UltClip(UltClipId.SubDrop, 0.9f * lvl);
            var cam = ViewCamera;
            float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, floor) / 40f) : 0.5f;
            Shake(Viewer == u.by ? 0.3f : 0.25f * near, 0.7f);
        }

        /// <summary>A dash's warning (DriveWarn, 0.3 s), on every screen: chess.com's arrow on the floor from her feet to the
        /// dash's end as wide as it hits - teal for her side, red with the squares under it framed red on the other side's -
        /// a red pool of light at its middle, the old cue.</summary>
        void PrWarnFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is PrFx f)) return;
            f.warnAt = Clock;
            f.warnFrom = e.at;
            f.warnTo = e.normal;
            float warn = Params != null ? Params.prWarn : 0.3f, width = (Params != null ? Params.prRadius * Mathf.Min(Params.prGrowMax, 1f + Params.prGrow * f.dashHits) : 2.2f) * 2f;
            f.warnWidth = width;
            Vector3 a = Ground(e.at), b = Ground(e.normal), d = Flat(b - a);
            float len = d.magnitude;
            if (len < 0.5f) return;
            var me = Viewer;
            bool ours = me == null || u.by == null || !Teams.AreEnemies(me.Team, u.by.Team);
            float until = Clock + warn + 0.05f;
            UltArrow(() => a, () => b, ours ? Teal : Blood, width, false, warn + 0.15f, () => Clock < until);
            if (!ours) PrWarnSquares(u, a, b, warn + 0.1f);
            Vector3 mid = (a + b) * 0.5f;
            UltWarnPool(u, () => mid, width * 0.5f + 1f, warn, 70f, () => Clock < until);
            UltPlay(sfxOff, 0.5f, 0f, 1.6f);
        }

        /// <summary>The squares under a dash framed red (×1.6) on the screens it is coming at.</summary>
        void PrWarnSquares(UltShow u, Vector3 a, Vector3 b, float life)
        {
            var cells = new List<(Vector2Int c, float y)>();
            var seen = new HashSet<Vector2Int>();
            float len = Flat(b - a).magnitude;
            for (float x = 0f; x <= len; x += 1f)
            {
                Vector3 p = Vector3.Lerp(a, b, x / Mathf.Max(0.01f, len));
                var c = Cell(p);
                if (!seen.Add(c)) continue;
                float fy = BrdCastFloor(c, p.y);
                if (!float.IsNaN(fy)) cells.Add((c, fy));
            }
            var mesh = BrdMesh("Pr dash squares", matBrdFrameRed);
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age >= life) return false;
                mesh.Begin(ViewCamera);
                foreach (var (c, y) in cells)
                {
                    Vector3 p = CellFlat(c);
                    p.y = y + 0.05f;
                    mesh.Quad(p, FamSquare * 0.96f, BrdTimes(Blood, 1.6f), Mathf.Clamp01(age / 0.05f));
                }
                mesh.End();
                return true;
            }, () => BrdKill(mesh)));
        }

        /// <summary>The dash (DriveLine), on every screen: a thin beam at hip height (a TealPale core in a teal sheath, 0.45 s),
        /// the squares along it flashing in turn, white and pale sparks, a tock and a boom; on her own screen the edge lines,
        /// the camera kicked (FOV +7°, a 2° roll). Its move goes on the card 0.45 s later (grey when it hit nobody).</summary>
        void PrLineFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is PrFx f)) return;
            Vector3 a = e.at + Vector3.up * 1.5f, b = e.normal + Vector3.up * 1.5f, ab = b - a;
            float len = ab.magnitude;
            if (len < 0.5f) return;
            for (int layer = 0; layer < 2; layer++)
            {
                bool core = layer == 0;
                Add(new Shape(root, core ? "Pr dash core" : "Pr dash sheath", meshCylinder, matPillar)
                {
                    life = 0.45f, at = a, rotation = Quaternion.FromToRotation(Vector3.up, ab / len),
                    animate = (s, t) =>
                    {
                        float k = Smooth01(s.Age / 0.12f), fade = 1f - Smooth01((t - 0.5f) / 0.5f), r = core ? 0.12f : 0.6f;
                        s.scale = new Vector3(r, len * k, r);
                        s.color = core ? HdrA(TealPale, 3.2f) : HdrA(Teal, 1.1f);
                        s.bright = fade;
                    },
                });
            }
            PrLitSquares(Ground(e.at), Ground(e.normal));
            SparkBurst(a, 15, Color.white, ab / len, 30f, 6f, 14f, 0.5f, 0.04f);
            SparkBurst(a, 15, TealPale, ab / len, 30f, 6f, 14f, 0.5f, 0.04f);
            float lvl = UltAudience(u, a);
            UltClip(UltClipId.Tock, 0.6f * lvl);
            UltPlay(sfxBoom, 0.7f * lvl);
            if (Viewer == u.by)
            {
                Shake(0.14f, 0.3f);
                camKickAt = RealClock;
                camKickFov = 7f;
                camRollKick = 2f * camRollSign;
                camRollSign = -camRollSign;
                float until = RealClock + 0.2f;
                anims.Add(new Ongoing((age, dt) =>
                {
                    if (RealClock >= until) return false;
                    UltEdgeLines(0.35f);
                    return true;
                }) { tick = UltTick.Real });
            }
            // Its move on the card once the dash is over.
            f.dashes++;
            f.dashStruck = 0;
            int dash = f.dashes;
            Vector3 end = Ground(e.normal);
            float at = Clock + 0.45f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (Clock < at) return true;
                string sq = PrDashSquares[(dash - 1) % PrDashSquares.Length];
                bool hit = f.dashes != dash || f.dashStruck > 0;
                UltMove(u, PrNota(u, (hit ? "Q×" : "Q") + sq), end, !hit);
                return false;
            }));
        }

        /// <summary>The squares along a straight dash flashing in turn (TealPale ×2.8 → ×0.8), on the board.</summary>
        void PrLitSquares(Vector3 a, Vector3 b)
        {
            var cells = new List<(Vector2Int c, float y, float delay)>();
            var seen = new HashSet<Vector2Int>();
            float len = Flat(b - a).magnitude;
            for (float x = 0f; x <= len; x += 1f)
            {
                Vector3 p = Vector3.Lerp(a, b, x / Mathf.Max(0.01f, len));
                var c = Cell(p);
                if (!seen.Add(c)) continue;
                float fy = BrdCastFloor(c, p.y);
                if (!float.IsNaN(fy)) cells.Add((c, fy, x / 75f));
            }
            var mesh = BrdMesh("Pr dash lit squares", matFrame);
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age >= 1.1f) return false;
                mesh.Begin(ViewCamera);
                foreach (var (c, y, delay) in cells)
                {
                    float t = age - delay;
                    if (t < 0f) continue;
                    Vector3 p = CellFlat(c);
                    p.y = y + 0.05f;
                    mesh.Quad(p, FamSquare * 0.96f * BrdPop(t), BrdTimes(TealPale, Mathf.Lerp(2.8f, 0.8f, Smooth01(t / 0.5f))), 1f - Smooth01((t - 0.5f) / 0.4f));
                }
                mesh.End();
                return true;
            }, () => BrdKill(mesh)) { tick = UltTick.Show });
        }

        /// <summary>An enemy hit by the queen's dash or thrown by her landing, on every screen: on a dash, '+10 %' and a teal ring
        /// sweeping up her body from her feet to her crown in 0.25 s as she grows. The capture itself is the common one.</summary>
        void PrStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null || !(u.state is PrFx f)) return;
            Vector3 at = ChestOf(e.target);
            if (e.size > 0.5f)
            {
                f.dashHits++;
                f.dashStruck++;
                UltPopWord(HeadOf(e.target) + Vector3.up * 1.2f, "+10%", HdrA(Teal, 1.4f), 0.45f, 0.9f);
                var by = u.by;
                if (by != null)
                    Add(new Shape(root, "Pr growth ring", meshQuad, matRing)
                    {
                        tick = UltTick.Show, life = 0.3f,
                        animate = (s, t) =>
                        {
                            if (by == null) { s.bright = 0f; return; }
                            float h = Mathf.Max(1f, GiantBaseHeight(by)) * GiantScaleOf(by), r = 0.9f * GiantScaleOf(by) / 0.86f;
                            Vector3 feet = FloorUnder(by);
                            s.at = new Vector3(by.Hips.position.x, feet.y + h * Smooth01(s.Age / 0.25f), by.Hips.position.z);
                            s.rotation = FlatOnFloor();
                            s.scale = new Vector3(r, r, 1f);
                            s.color = HdrA(Teal, 2.4f);
                            s.bright = 1f - Smooth01((t - 0.7f) / 0.3f);
                        },
                    });
            }
            UltSound(UltSfx.Stomp, at, e.count);
            UltScreenStrike(u, e);
        }

        /// <summary>The run's shove (a pawn's own hit, not a capture the ultimate counts), written as the pawn's capture: e×d5
        /// when it goes to the pawn's left, e×f5 to its right, the rank it has reached; its square red and a capture ring for a
        /// moment, the capture's clack.</summary>
        void PrRunHit(SkillFxEvent e)
        {
            if (e.by == null || e.target == null) return;
            var u = ShowOf(e.by);
            if (u == null || u.skill != PawnRushSkillIds.PromotionRun || !(u.state is PrFx f) || !u.Playing || PrStageOf(u.by) != RagdollPawn.PrRun) return;
            Vector3 right = new Vector3(u.by.PlanDir.z, 0f, -u.by.PlanDir.x);
            bool toRight = Vector3.Dot(e.dir, right) >= 0f;
            int rank = Mathf.Clamp(3 + f.ranks, 3, 7);
            Vector3 at = e.target.Hips.position;
            UltMove(u, PrNota(u, $"e×{(toRight ? 'f' : 'd')}{rank}"), at);
            float floor = BrdGroundBelow(at, 6f);
            SquareMark(u, Cell(at), Blood, 0.9f, 0.30f, Blood, 2.4f, null, 0.6f, null, float.IsNaN(floor) ? at.y - e.target.standHeight : floor);
            CaptureRing(e.target, 0.6f);
            UltClip(UltClipId.Capture, 0.85f * UltAudience(u, at));
        }

        string PrHint(UltShow u)
        {
            int stage = PrStageOf(u.by);
            if (stage == RagdollPawn.PrRun) return "이동 키 = 질주 방향 (아무도 못 막아요)";
            if (stage == RagdollPawn.PrLeap) return "퀸으로!";
            byte n = u.by.PlanNote;
            if ((n & (RagdollPawn.PrWindup | RagdollPawn.PrDashing)) != 0) return "돌진!";
            return (n & RagdollPawn.PrDashMask) > 0 ? "좌클릭 = 카메라 쪽으로 30 m 돌진 (낮은 지붕 밑은 못 들어가요)" : "돌진을 다 썼어요";
        }

        string PrLine(UltShow u)
        {
            var f = u.state as PrFx;
            int stage = PrStageOf(u.by), hits = f != null ? f.dashHits : 0;
            if (stage == RagdollPawn.PrRun) return "질주!";
            float grow = Params != null ? Mathf.Min(Params.prGrowMax, 1f + Params.prGrow * hits) : 1f;
            return $"돌진 {u.by.PlanNote & RagdollPawn.PrDashMask}번  ·  크기 ×{grow:0.0}  ·  맞힌 적 {u.struckSet.Count}";
        }

        string PrWarning(UltShow u, RagdollPawn me)
        {
            if (u.by == null || me == null) return null;
            int stage = PrStageOf(u.by);
            Vector3 p = me.Hips.position;
            if (stage == RagdollPawn.PrRun)
                return DistanceToSegment(Flat(p), Flat(u.by.Hips.position), Flat(u.by.PlanPoint)) < 2.5f ? "폰이 질주해 와요 — 옆으로 비켜요!" : null;
            if (u.state is PrFx f && Clock - f.warnAt < 0.45f)
                return DistanceToSegment(Flat(p), Flat(f.warnFrom), Flat(f.warnTo)) < f.warnWidth * 0.5f + 0.5f ? "퀸의 돌진 줄 — 비켜요!" : null;
            return null;
        }

        // ---------------------------------------------------------------- giants

        sealed class Giant
        {
            public RagdollPawn p;
            public float scale = 1f, want = 1f, rate = 4f, height = -1f;
            public Mesh baked;
            public Material paint;   // drawn in this instead of its own materials (asked again every frame)
            // 11-26 (the 4판's bundle 다): turned this way round its feet, or round its hips (asked again every frame).
            public Quaternion pose = Quaternion.identity;
            public bool posed, poseAtHips;
            // 11-26 (the user's own skills): pressed or pulled out of shape round its feet, per axis (납작이's drum).
            public Vector3 stretch = Vector3.one;
            public bool stretched;
            // ...and drawn once more a little bigger behind itself in this (an outline: 멈춤's white edge).
            public Material outline;
        }

        /// <summary>This frame <paramref name="p"/> is drawn with an outline of <paramref name="outline"/> (an inverted hull: a
        /// front-face-culled copy 6% bigger round its hips).</summary>
        void WantOutline(RagdollPawn p, Material outline)
        {
            if (p == null || p.skin == null || outline == null) return;
            if (!giants.TryGetValue(p, out var g)) giants[p] = g = new Giant { p = p };
            g.outline = outline;
        }

        /// <summary>This frame <paramref name="p"/> is drawn <paramref name="stretch"/> times its size along each world axis
        /// round its feet (납작이: wide and low).</summary>
        void WantStretch(RagdollPawn p, Vector3 stretch)
        {
            if (p == null || p.skin == null) return;
            if (!giants.TryGetValue(p, out var g)) giants[p] = g = new Giant { p = p };
            g.stretch = stretch;
            g.stretched = true;
        }

        /// <summary>This frame <paramref name="p"/> is drawn turned by <paramref name="pose"/> round its feet (a tower falling
        /// over) or round its hips (a rook stuck head first in the floor), at the size WantGiant gives it.</summary>
        void WantPose(RagdollPawn p, Quaternion pose, bool aroundHips)
        {
            if (p == null || p.skin == null) return;
            if (!giants.TryGetValue(p, out var g)) giants[p] = g = new Giant { p = p };
            g.pose = pose;
            g.posed = true;
            g.poseAtHips = aroundHips;
        }

        readonly Dictionary<RagdollPawn, Giant> giants = new Dictionary<RagdollPawn, Giant>();
        readonly List<RagdollPawn> giantsGone = new List<RagdollPawn>();

        /// <summary>This frame <paramref name="p"/> is drawn <paramref name="scale"/> times its size (eased at
        /// <paramref name="rate"/> a second); not asked again, it goes back to its own size. 11-26: below 1 too (the 4판's
        /// 쪼꼬미, drawn half size from its feet).</summary>
        void WantGiant(RagdollPawn p, float scale, float rate = 4f, Material paint = null)
        {
            if (p == null || p.skin == null) return;
            if (!giants.TryGetValue(p, out var g)) giants[p] = g = new Giant { p = p };
            g.want = Mathf.Max(0.2f, scale);
            g.rate = rate;
            g.paint = paint;   // (11-26: the 4판's 무거움 draws it cast iron, at its own size)
        }

        /// <summary>A piece's drawn height (m) when it first grows (its skin standing).</summary>
        float GiantBaseHeight(RagdollPawn p)
        {
            if (p == null || p.skin == null) return -1f;
            if (giants.TryGetValue(p, out var g) && g.height > 0f) return g.height;
            float h = p.skin.bounds.size.y;
            if (g != null && h > 0.3f) g.height = h;
            return h;
        }

        /// <summary>Every frame (UltStep): the giants drawn, their scale eased; what was not asked for again shrinks back
        /// to its own size and its skin shows again. A giant carries its skin's rendering layers (the stage's rim light, L2,
        /// edges it too).</summary>
        void UltGiants(float dt)
        {
            giantsGone.Clear();
            foreach (var kv in giants)
            {
                var g = kv.Value;
                var skin = g.p != null ? g.p.skin : null;
                if (skin == null)
                {
                    if (g.baked != null) Destroy(g.baked);
                    giantsGone.Add(kv.Key);
                    continue;
                }
                float step = Mathf.Max(0.5f, g.want) * g.rate * dt;
                g.scale = Mathf.MoveTowards(g.scale, g.want, step);
                g.want = 1f;   // (asked again by the next frame's step, or it goes back)
                var paint = g.paint;
                g.paint = null;
                bool posed = g.posed;
                g.posed = false;
                bool stretched = g.stretched;
                g.stretched = false;
                var outline = g.outline;
                g.outline = null;
                if (Mathf.Abs(g.scale - 1f) <= 0.01f && paint == null && !posed && !stretched && outline == null)
                {
                    skin.forceRenderingOff = false;
                    if (g.baked != null) Destroy(g.baked);
                    giantsGone.Add(kv.Key);
                    continue;
                }
                if (g.baked == null) g.baked = new Mesh { name = "Giant look", hideFlags = HideFlags.HideAndDontSave };
                BakeSkin(skin, g.baked);
                skin.forceRenderingOff = true;
                Vector3 feet = FloorUnder(g.p);
                Matrix4x4 world = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                Matrix4x4 big = Matrix4x4.Translate(feet) * Matrix4x4.Scale(Vector3.one * g.scale) * Matrix4x4.Translate(-feet) * world;
                if (stretched) big = Matrix4x4.Translate(feet) * Matrix4x4.Scale(g.stretch) * Matrix4x4.Translate(-feet) * big;
                if (posed)
                {
                    Vector3 pivot = g.poseAtHips ? g.p.Hips.position : feet;
                    big = Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(g.pose) * Matrix4x4.Translate(-pivot) * big;
                }
                var mats = skin.sharedMaterials;
                var bounds = new Bounds(feet + Vector3.up * 3f * g.scale, Vector3.one * 8f * g.scale);
                for (int i = 0; i < g.baked.subMeshCount && mats.Length > 0; i++)
                {
                    var m = paint != null ? paint : mats[Mathf.Min(i, mats.Length - 1)];
                    if (m == null) continue;
                    var rp = new RenderParams(m)
                    {
                        shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                        receiveShadows = true,
                        layer = skin.gameObject.layer,
                        renderingLayerMask = skin.renderingLayerMask,
                        worldBounds = bounds,
                    };
                    Graphics.RenderMesh(rp, g.baked, i, big);
                    if (outline != null)
                    {
                        Vector3 hips = g.p.Hips.position;
                        var ro = new RenderParams(outline)
                        {
                            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                            receiveShadows = false,
                            layer = skin.gameObject.layer,
                            renderingLayerMask = skin.renderingLayerMask,
                            worldBounds = bounds,
                        };
                        Graphics.RenderMesh(ro, g.baked, i, Matrix4x4.Translate(hips) * Matrix4x4.Scale(Vector3.one * 1.06f) * Matrix4x4.Translate(-hips) * big);
                    }
                }
            }
            foreach (var p in giantsGone) giants.Remove(p);
        }

        void DestroyGiants()
        {
            foreach (var g in giants.Values)
            {
                if (g.p != null && g.p.skin != null) g.p.skin.forceRenderingOff = false;
                if (g.baked != null) Destroy(g.baked);
            }
            giants.Clear();
        }

        /// <summary>How big a piece is drawn now (1 its own size).</summary>
        float GiantScaleOf(RagdollPawn p) => p != null && giants.TryGetValue(p, out var g) ? g.scale : 1f;
    }
}
