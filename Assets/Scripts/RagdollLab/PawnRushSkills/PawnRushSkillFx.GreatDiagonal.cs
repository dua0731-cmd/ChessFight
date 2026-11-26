using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 롱 다이애거널 (a1–h8!!) on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.GreatDiagonal.cs; the "!!"
    /// restyle after the spec's per-ultimate file): the long diagonal and the skewer, drawn as a chessboard diagonal.
    ///
    ///   the key        the bishop's four diagonals light faintly out of its square for 1.5 s: its lines
    ///   gathering      a diamond (a square turned 45°) closing on its chest, white, then teal with a chime when ready; a
    ///                  hum rising; the line it would take drawn on the floor as a chessboard diagonal - a thin line and
    ///                  squares turned 45° touching corner to corner (3.11 m a square): on the bishop's screen teal with the
    ///                  first eight named a1 b2 … h8 and dimmer squares out to 60 m, a mark at each wall turn; faint teal on
    ///                  its allies'; red on its enemies' (who stand on it: a capture ring, the red mark on the chest, a warning)
    ///   the shot       a white core in a teal sheath along every stretch (the sheath wider at each turn), the diagonal's
    ///                  squares flashing white to teal as the light runs (420 m/s), a star and a square wave at each turn; on
    ///                  the bishop's screen a shake and the camera kicked toward the line's side
    ///   the hits       "B×" and the diagonal square each one stood on (B×c3, B×f6 …) on the card; pulled back along the light
    ///                  with a red streak; two or more on one shot is the skewer: the blow, the bishop's own slow motion
    ///                  (once), "스큐어!!" in the top band
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>The long diagonal's squares from a1 (one 3.11 m diamond each).</summary>
        static readonly string[] GdSquares = { "a1", "b2", "c3", "d4", "e5", "f6", "g7", "h8" };
        static readonly Vector2Int[] GdDiagonals = { new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1) };
        const float GdDiamond = 3.11f;   // a 2.2 m square's diagonal

        sealed class GdFx
        {
            public readonly Vector3[] path = new Vector3[RagdollPawn.GdBounces + 2];
            public int count;
            public readonly HashSet<RagdollPawn> onLine = new HashSet<RagdollPawn>();
            public readonly Dictionary<RagdollPawn, Shape> marks = new Dictionary<RagdollPawn, Shape>();
            public float gatherAt = -1f;
            public bool chimed;
            public CellMesh line, diamonds;
            public readonly Text3D[] labels = new Text3D[8];
            public readonly List<Vector3> floorPath = new List<Vector3>(96);
            public int skewerFrame = -1;
            // The floor path's last cast (its start and way, real time): cast again only when the aim moved or 0.08 s passed.
            public Vector3 castFrom, castWay;
            public float castAt = -10f;
        }

        static bool GdChargingNow(UltShow u) => u.by != null && u.Playing && (u.by.PlanNote & RagdollPawn.GdCharging) != 0;

        string GdSquareName(UltShow u, int i)
        {
            string sq = GdSquares[Mathf.Clamp(i, 0, GdSquares.Length - 1)];
            return u.black ? Mirror(sq) : sq;
        }

        void GdCast(UltShow u)
        {
            var f = new GdFx();
            u.state = f;
            BrdBuild();
            // The key: its four diagonals out of its square, faintly, for 1.5 s.
            BoardLines(u, u.fromCell, GdDiagonals, 8, Teal, 1.2f, 1.5f);
            // The gathering: a diamond closing on the chest.
            Add(new Shape(root, "Gd gather diamond", meshQuad, matFrame)
            {
                life = 1e9f, billboard = true, rotation = Quaternion.Euler(0f, 0f, 45f),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool on = GdChargingNow(u);
                    if (!on) { f.gatherAt = -1f; f.chimed = false; s.bright = 0f; return; }
                    if (f.gatherAt < 0f) f.gatherAt = Clock;
                    float need = Params != null ? Params.gdCharge : 0.5f, k = Mathf.Clamp01((Clock - f.gatherAt) / need);
                    bool ready = (u.by.PlanNote & RagdollPawn.GdCharged) != 0 || k >= 1f;
                    float flash = 1f;
                    if (ready && !f.chimed)
                    {
                        f.chimed = true;
                        UltSound(UltSfx.Perfect, u.by.GdMuzzle);
                    }
                    if (ready) flash = 1f + 3f * Mathf.Exp(-(Clock - f.gatherAt - need) / 0.03f);
                    float size = Mathf.Lerp(2.6f, 0.5f, Smooth01(k));
                    s.at = u.by.GdMuzzle;
                    s.scale = new Vector3(size, size, 1f);
                    s.color = ready ? HdrA(Teal, flash > 1.5f ? 4f : 3f) : HdrA(Color.white, 1.6f);   // (a one-frame flash ×4 when it is ready)
                    s.bright = ready ? 0.9f + 0.3f * Mathf.Sin(Clock * 20f) : 0.5f + 0.5f * k;
                    if (Random.value < 0.6f)
                    {
                        Vector3 from = s.at + Random.onUnitSphere * size * 0.6f;
                        motes.Emit(from, (s.at - from) * 3f, 0.05f, 0.3f, Tint(ready ? Teal : Color.white, 0.2f));
                    }
                },
            });
        }

        void GdStep(UltShow u, float dt, Camera cam)
        {
            if (!(u.state is GdFx f) || u.by == null) return;
            var s = Params;
            f.onLine.Clear();
            bool charging = s != null && GdChargingNow(u);
            if (!charging)
            {
                GdLineOff(f);
                return;
            }
            // The bishop's own screen from its own look (no lag); the others' from the way the host was told.
            Vector3 way = Viewer == u.by && cam != null ? ViewAim(cam) : u.by.PlanDir;
            f.count = RagdollPawn.GdPath(u.by.GdMuzzle, way, s.gdLength, f.path);
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == u.by || !Teams.AreEnemies(e.Team, u.by.Team) || e.State != PawnState.Active || u.struckSet.Contains(e)) continue;
                if (!RagdollPawn.GdOnLine(s, f.path, f.count, e.Hips.position, out _)) continue;
                f.onLine.Add(e);
                if (!f.marks.ContainsKey(e)) f.marks[e] = GdMark(u, f, e);
            }
            // The hum while it gathers, rising as it fills.
            float need = s.gdCharge, k = f.gatherAt >= 0f ? Mathf.Clamp01((Clock - f.gatherAt) / Mathf.Max(0.05f, need)) : 0f;
            UltBed(UltClipId.Hum, 0.35f * UltAudience(u, u.by.Hips.position), 1f + 0.5f * k);
            GdDrawLine(u, f, cam);
        }

        /// <summary>The gathering's line gone for good (the show over).</summary>
        void GdClear(GdFx f)
        {
            GdLineOff(f);
            BrdKill(f.line);
            BrdKill(f.diamonds);
            f.line = f.diamonds = null;
        }

        void GdLineOff(GdFx f)
        {
            f.line?.Hide();
            f.diamonds?.Hide();
            for (int i = 0; i < f.labels.Length; i++)
            {
                f.labels[i]?.Destroy();
                f.labels[i] = null;
            }
        }

        /// <summary>The line it would take as a chessboard diagonal on the floor (every screen): a thin line along the whole
        /// path, squares turned 45° touching corner to corner along it out to 60 m (the first eight named on the bishop's
        /// screen), a mark at each wall turn. Teal on the bishop's screen, faint teal on its allies', red on its enemies'.</summary>
        void GdDrawLine(UltShow u, GdFx f, Camera cam)
        {
            var me = Viewer;
            bool mine = me != null && me == u.by, enemy = me != null && !mine && Teams.AreEnemies(me.Team, u.by.Team);
            if (f.line == null) f.line = BrdMesh("Gd diagonal line", enemy ? matBrdLineRed : matBrdLine);
            if (f.diamonds == null) f.diamonds = BrdMesh("Gd diagonal squares", enemy ? matBrdFrameRed : matFrame);
            Color colour = enemy ? Blood : Teal;
            float lineHdr = enemy ? 2.4f : mine ? 2.2f : 0.6f * 2.2f, lineW = enemy ? 0.06f : 0.035f;
            float squareHdr = enemy ? 1.4f : mine ? 2.2f : 0.6f * 2.2f;
            // The path on the floor, a point every 4 m (and at each turn): cast again when the aim has moved, at most every
            // 0.08 s otherwise (up to ninety casts a time).
            Vector3 way = f.count >= 2 ? Flat(f.path[1] - f.path[0]).normalized : Vector3.forward;
            bool recast = RealClock - f.castAt > 0.08f || Vector3.Angle(way, f.castWay) > 0.5f || (f.path[0] - f.castFrom).sqrMagnitude > 0.04f;
            if (recast)
            {
                f.castAt = RealClock;
                f.castWay = way;
                f.castFrom = f.path[0];
                f.floorPath.Clear();
            }
            float y = Ground(u.by.Hips.position - Vector3.up * u.by.standHeight).y;
            for (int i = 0; recast && i + 1 < f.count; i++)
            {
                Vector3 a = f.path[i], b = f.path[i + 1];
                float len = Flat(b - a).magnitude;
                int n = Mathf.Clamp(Mathf.CeilToInt(len / 4f), 1, 80);
                for (int j = i == 0 ? 0 : 1; j <= n && f.floorPath.Count < 90; j++)
                {
                    Vector3 p = Vector3.Lerp(a, b, j / (float)n);
                    float g = BrdCastDown(new Vector3(p.x, Mathf.Max(p.y, y) + 1.5f, p.z), 8f);
                    if (!float.IsNaN(g)) y = g;
                    f.floorPath.Add(new Vector3(p.x, y + 0.06f, p.z));
                }
            }
            f.line.Begin(cam);
            f.diamonds.Begin(cam);
            float run = 0f, nextSquare = GdDiamond * 0.5f;
            int squares = 0;
            for (int i = 0; i + 1 < f.floorPath.Count; i++)
            {
                Vector3 a = f.floorPath[i], b = f.floorPath[i + 1], d = Flat(b - a);
                float len = d.magnitude;
                if (len < 1e-3f) continue;
                Vector3 dir = d / len, m = new Vector3(dir.z, 0f, -dir.x) * (lineW * 0.5f);
                f.line.Corners(a - m, b - m, b + m, a + m, BrdTimes(colour, lineHdr), 1f);
                // The diamonds: a 2.2 m square turned 45°, its diagonal along the line, one every 3.11 m out to 60 m.
                while (nextSquare <= run + len && nextSquare <= 60f)
                {
                    Vector3 c = Vector3.Lerp(a, b, (nextSquare - run) / len);
                    float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 45f, hdr = squares < 8 ? squareHdr : squareHdr * 0.5f;
                    f.diamonds.Quad(c, FamSquare * 0.96f, BrdTimes(colour, hdr), 1f, yaw);
                    if (mine && squares < 8) GdLabel(f, squares, c, GdSquareName(u, squares), cam);
                    squares++;
                    nextSquare += GdDiamond;
                }
                run += len;
            }
            for (int i = squares; i < f.labels.Length; i++)
            {
                f.labels[i]?.Destroy();
                f.labels[i] = null;
            }
            // A mark at each wall turn.
            for (int i = 1; i + 1 < f.count; i++)
            {
                Vector3 p = f.path[i];
                float g = BrdCastDown(p + Vector3.up * 0.5f, 8f);
                if (!float.IsNaN(g)) f.diamonds.Quad(new Vector3(p.x, g + 0.07f, p.z), 1.4f, BrdTimes(enemy ? Blood : TealPale, 3f), 1f, 45f);
            }
            f.line.End();
            f.diamonds.End();
        }

        void GdLabel(GdFx f, int i, Vector3 at, string name, Camera cam)
        {
            if (font == null) return;
            if (f.labels[i] == null) f.labels[i] = new Text3D(root, font, name, new Color(1f, 1f, 1f, 0.85f), 0.32f);
            f.labels[i].Place(at + Vector3.up * 0.35f, cam, 1f, 0.85f);
        }

        /// <summary>The red aim on the chest of an enemy standing on the line while it gathers, and chess.com's capture ring at
        /// its feet.</summary>
        Shape GdMark(UltShow u, GdFx f, RagdollPawn e)
        {
            Add(new Shape(root, "Gd capture ring", meshQuad, matBrdRingRed)
            {
                life = 1e9f, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    if (!u.Playing || e == null) { s.life = 0f; return; }
                    bool on = f.onLine.Contains(e);
                    s.bright = on ? 1f : 0f;
                    if (!on) return;
                    Vector3 feet = e.FeetPoint;
                    s.at = new Vector3(feet.x, feet.y + 0.06f, feet.z);
                    float r = 1.9f / 0.86f * (1f + 0.15f * Mathf.Sin(Clock * Mathf.PI * 6f));
                    s.scale = new Vector3(r, r, 1f);
                    s.color = HdrA(Blood, 2.8f);
                },
            });
            return Add(new Shape(root, "Gd aim mark", meshQuad, matBrdRingRed)
            {
                life = 1e9f, billboard = true,
                animate = (s, t) =>
                {
                    if (!u.Playing || e == null) { s.life = 0f; return; }
                    bool on = f.onLine.Contains(e);
                    s.bright = on ? 1f + 0.4f * Mathf.Sin(Clock * 24f) : 0f;
                    if (!on) return;
                    s.at = ChestOf(e);
                    s.scale = new Vector3(0.42f, 0.42f, 1f);
                    s.color = HdrA(Blood, 3f);
                },
            });
        }

        /// <summary>Before the key: whoever stands on the line the camera looks along now, on the bishop's screen.</summary>
        void GdPreview(RagdollPawn me, PawnRushSkillParams s, Camera cam, HashSet<RagdollPawn> wanted)
        {
            int count = RagdollPawn.GdPath(me.GdMuzzle, ViewAim(cam), s.gdLength, gdPreviewPath);
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == me || !Teams.AreEnemies(e.Team, me.Team) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                if (RagdollPawn.GdOnLine(s, gdPreviewPath, count, e.Hips.position, out _)) wanted.Add(e);
            }
        }

        readonly Vector3[] gdPreviewPath = new Vector3[RagdollPawn.GdBounces + 2];

        /// <summary>A stretch of the shot: a white core (0.05 m) in a teal sheath (0.3 m, ×1.3 wider at each turn) running
        /// out along it at 420 m/s, the diagonal's squares on the floor flashing white to teal as it passes; at the start the
        /// boom and the chime (and, on the bishop's screen, a shake and the camera kicked toward the line's side); at a turn a
        /// four-point star, a square wave and a higher chime.</summary>
        void GdLineFx(UltShow u, SkillFxEvent e)
        {
            int seg = e.count & 3;
            Vector3 a = e.at, b = e.normal, ab = b - a;
            float len = ab.magnitude;
            if (len < 0.1f) return;
            BrdBuild();
            Vector3 d = ab / len;
            const float Speed = 420f;   // m/s: how fast the light runs out
            float wait = seg * 0.04f, grow = len / Speed, sheath = 0.3f * Mathf.Pow(1.3f, seg);
            for (int layer = 0; layer < 2; layer++)
            {
                bool core = layer == 0;
                Add(new Shape(root, core ? "Gd beam core" : "Gd beam sheath", meshCylinder, matPillar)
                {
                    life = wait + grow + 1.1f, at = a, rotation = Quaternion.FromToRotation(Vector3.up, d),
                    animate = (s, t) =>
                    {
                        float age = s.Age - wait;
                        if (age < 0f) { s.bright = 0f; return; }
                        float k = Mathf.Clamp01(age / Mathf.Max(0.01f, grow)), fade = 1f - Mathf.Clamp01((age - grow - 0.15f) / 0.9f);
                        float r = (core ? 0.05f : sheath) * (1f + 0.25f * Mathf.Exp(-age / 0.1f));
                        s.scale = new Vector3(r * fade + 0.005f, len * k, r * fade + 0.005f);
                        s.color = core ? HdrA(new Color(0.9f, 1f, 0.97f), 3.5f) : HdrA(Teal, 1.1f);
                        s.bright = fade;
                    },
                });
            }
            GdLitDiamonds(u, a, b, wait, Speed, seg);
            var cam = ViewCamera;
            if (seg == 0)
            {
                Flare(a, Color.white, 2.5f, 8f, 0.2f);
                SparkBurst(a, 24, TealPale, d, 25f, 6f, 14f, 0.4f, 0.04f);
                UltSound(UltSfx.Boom, a);
                UltPlay(sfxPerfect, 0.6f, 0f, 1.5f);
                UltClip(UltClipId.Tock, 0.6f * UltAudience(u, a));
                if (Viewer == u.by)
                {
                    Shake(0.12f, 0.25f);
                    camKickAt = RealClock;
                    camKickFov = 6f;
                    camRollKick = cam != null && Vector3.Dot(cam.transform.right, d) > 0f ? -2f : 2f;
                }
            }
            else
            {
                // A turn off a wall: a four-point star, a square wave, a higher ping.
                Flare(a, TealPale, 2.5f, 8f, 0.3f);
                GdStar(a);
                SparkBurst(a, 24, Color.white, -d, 70f, 4f, 12f, 0.5f, 0.04f);
                UltSquareWave(RagdollPawn.DriveFloor(a, 2f, 6f) ?? a, FamSquare * 0.5f, 6f, 0.5f, Teal, 3f);
                UltPlay(sfxChime[Mathf.Clamp(seg * 2 + 3, 0, sfxChime.Length - 1)], 0.8f);
            }
            if (cam != null && Viewer != u.by)
            {
                // Whoever the light passes near feels it.
                float near = DistanceToSegment(cam.transform.position, a, b);
                if (near < 12f) Shake(0.08f * (1f - near / 12f), 0.25f);
            }
        }

        /// <summary>The diagonal's squares under a stretch flashing white to teal as the light passes, gone in 0.8 s.</summary>
        void GdLitDiamonds(UltShow u, Vector3 a, Vector3 b, float wait, float speed, int seg)
        {
            var mesh = BrdMesh("Gd lit squares", matFrame);
            var points = new List<(Vector3 at, float yaw, float delay)>();
            Vector3 d = Flat(b - a);
            float len = d.magnitude;
            if (len < 0.1f) return;
            d /= len;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 45f, y = Ground(a).y;
            for (float x = GdDiamond * 0.5f; x < len && points.Count < 96; x += GdDiamond)
            {
                Vector3 p = a + d * x;
                float g = BrdCastDown(new Vector3(p.x, Mathf.Max(p.y, y) + 1.5f, p.z), 8f);
                if (float.IsNaN(g)) continue;
                y = g;
                points.Add((new Vector3(p.x, g + 0.06f, p.z), yaw, wait + x / speed));
            }
            float life = wait + len / speed + 0.9f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age >= life) return false;
                mesh.Begin(ViewCamera);
                foreach (var (at, turn, delay) in points)
                {
                    float t = age - delay;
                    if (t < 0f) continue;
                    float k = 1f - Mathf.Clamp01(t / 0.8f);
                    mesh.Quad(at, FamSquare * 0.96f * BrdPop(t), BrdTimes(Color.Lerp(Color.white, Teal, Mathf.Clamp01(t / 0.25f)), 3f), k, turn);
                }
                mesh.End();
                return true;
            }, () => BrdKill(mesh)) { tick = UltTick.Show });
        }

        /// <summary>A four-point star at a turn: two crossed 2 m lines of light ×4 for 0.15 s.</summary>
        void GdStar(Vector3 at)
        {
            for (int i = 0; i < 2; i++)
            {
                bool across = i == 0;
                Add(new Shape(root, "Gd star", meshQuad, matBrdLine)
                {
                    life = 0.15f, at = at, billboard = true, scale = across ? new Vector3(2f, 0.06f, 1f) : new Vector3(0.06f, 2f, 1f),
                    animate = (s, t) =>
                    {
                        s.color = HdrA(TealPale, 4f);
                        s.bright = 1f - t;
                    },
                });
            }
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>An enemy struck by the light, on every screen: its move on the card ("B×" and the diagonal square it stood
        /// on, by how far along the line it was), a red streak as the light drags it back; the second on one shot makes it a
        /// skewer: the blow, the bishop's own slow motion (once), "스큐어!!". The capture itself is the common one.</summary>
        void GdStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null || !(u.state is GdFx f)) return;
            var target = e.target;
            Vector3 at = ChestOf(target);
            float until = Clock + 0.9f;
            anims.Add(new Streak(root, matTrail, HdrA(Blood, 2.6f), 0.35f, 0.35f, () => target != null && Clock < until ? ChestOf(target) : (Vector3?)null));
            int square = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, e.size) / GdDiamond), 1, GdSquares.Length - 1);
            UltMove(u, "B×" + GdSquareName(u, square), at);
            UltSound(UltSfx.Stomp, at, e.count);
            UltScreenStrike(u, e);
            // The skewer: a second struck on the same shot (the shot's strikes come in one frame).
            if (u.burstFrame == Time.frameCount && u.burst.Count >= 2 && f.skewerFrame != Time.frameCount)
            {
                f.skewerFrame = Time.frameCount;
                UltBlow(u, ImpactSeq.Blow, at);
                UltSlow(u, UltSlowKind.SigAftermath);
                UltTopWord("스큐어!!", HdrA(Teal, 1.2f), 56);
            }
        }

        string GdHint(UltShow u)
        {
            if (u.by == null) return null;
            byte n = u.by.PlanNote;
            if ((n & RagdollPawn.GdCharged) != 0) return "떼면 발사!  (벽에 닿으면 직각으로 꺾여요, 두 번까지)";
            if ((n & RagdollPawn.GdCharging) != 0) return "모으는 중…  (0.5초)";
            return "좌클릭 누르고 0.5초 모았다 떼기 = 300 m 빛의 대각선";
        }

        string GdLine(UltShow u)
        {
            int shots = u.by != null ? u.by.PlanNote & RagdollPawn.GdShotsMask : 0;
            return $"남은 빛 {shots}발  ·  꿰인 적 {u.struckSet.Count}";
        }

        string GdWarning(UltShow u, RagdollPawn me) =>
            u.state is GdFx f && f.onLine.Contains(me) ? "빛의 대각선이 겨눠요 — 줄에서 엇갈려 비켜요!" : null;
    }
}
