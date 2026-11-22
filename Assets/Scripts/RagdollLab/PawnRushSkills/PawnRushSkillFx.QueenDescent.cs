using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 여왕 강림 on the screens (JY-skill-lab, 2026-11-22; the rules in RagdollPawn.QueenDescent.cs; the "!!" restyle after the
    /// spec's per-ultimate file): the queen over the board, and the queen sacrifice.
    ///
    ///   the rise         four light posts shoot up from her square to 22 m (the queen leaves the board), a teal crown ring
    ///                    over her head while she flies
    ///   flying           every screen: a teal gaze of light under her and a soft dark disc; the wind. Her screen: chess.com's
    ///                    legal-move dots on her eight lines from the square under her (enemies on them ringed), the dive's
    ///                    aim as a to-square with its coordinate inside its 8 m ring (the squares in it framed, enemies' red), an
    ///                    arrow to it on the floor, a dashed drop line down to it, the aim spot; the sacrifice's faint 16 m ring
    ///   a dive           every screen: her shadow growing, the 8 m ring red with the squares in it rippling red from the
    ///                    middle, a red pool of light, capture rings on everyone in it, the falling whistle; the move typed on
    ///                    the card (Q×f7+, Q×g7+ … a dive on nobody grey). Its blow by the hits (two or more: the blow, three or
    ///                    more slowed after it once), a 7×7 checker stamp, two square waves, a blow light, a flash of her posts
    ///   the sacrifice    the signature, the archetypal brilliant move: a red neon ring round 16 m over a 9×9 red stamp, a red
    ///                    pool, "Q×h7+!!" in the top band, her own screen slowed into it; the blast's frames (the badge stamped,
    ///                    the queen in ink), the floor breaking into floating tiles, a teal light post where she stood and her
    ///                    body bursting into teal square shards; 0.5 s on, the reply "…K×h7" in grey: the sacrifice accepted.
    ///                    Its judgement comes 0.6 s after the blast (UltHit, PawnRushSkillFx.Ult.cs)
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>Her dives as checks round the black king (Q×f7+, Q×g7+ …), one a dive.</summary>
        static readonly string[] QdDives = { "f7+", "g7+", "e6+", "h6+", "g6+", "f8+" };
        static readonly Vector2Int[] QdLines =
        {
            new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1),
            new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
        };

        sealed class QdFx
        {
            public Vector3 aim;
            public bool haveAim, blocked, sacrificed, slowed, risen;
            public int dives;
            public float sacWarnAt = -1f, nextRing;
            public CellMesh zone, zoneRed;
        }

        Material matShadow;   // (made with the ultimates' parts: a soft disc that covers what is under it)

        static int QdStageOf(RagdollPawn p) => p != null ? (p.PlanNote >> RagdollPawn.QdStageShift) & 3 : 0;

        static bool QdFlying(UltShow u) => u.Playing && u.by != null && QdStageOf(u.by) != RagdollPawn.QdDown;

        string QdNota(UltShow u, int dive, bool capture)
        {
            string sq = QdDives[(Mathf.Max(1, dive) - 1) % QdDives.Length];
            string nota = capture ? "Q×" + sq : "Q" + sq.TrimEnd('+');
            return u.black ? Mirror(nota) : nota;
        }

        /// <summary>Four light posts (TealPale ×3.2, 22 m) from the corners of a square, shooting up in 0.25 s and gone in 1 s.</summary>
        void QdPosts(Vector3 floor, float height = 22f, float fade = 1f)
        {
            Vector3 c = CellFlat(Cell(floor));
            c.y = floor.y;
            float h = FamSquare * 0.48f;
            foreach (var d in new[] { new Vector3(h, 0f, h), new Vector3(h, 0f, -h), new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h) })
                LightPost(c + d, height, 0.25f, 0f, fade, true, 1f);
        }

        void QdCast(UltShow u)
        {
            var f = new QdFx();
            u.state = f;
            if (matShadow == null) matShadow = Glow(texSoft, opacity: 1f);
            BrdBuild();
            var by = u.by;
            // The rise: the queen leaves the board, her four posts shooting up from her square.
            QdPosts(Ground(u.castAt));
            // Every screen while she flies: her crown ring, a teal gaze of light under her, a soft dark disc under her, the wind.
            Add(new Shape(root, "Qd crown ring", meshQuad, matRing)
            {
                life = 1e9f, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    if (!u.Playing || by == null) { s.life = 0f; return; }
                    bool on = QdFlying(u) && s.Age > 0.3f;
                    s.bright = on ? 1f : 0f;
                    if (!on) return;
                    s.at = HeadOf(by) + Vector3.up * 0.35f;
                    s.rotation = FlatOnFloor() * Quaternion.Euler(0f, 0f, Clock * 40f);
                    float r = 0.9f / 0.86f;
                    s.scale = new Vector3(r, r, 1f);
                    s.color = HdrA(Teal, 3f);
                },
            });
            Add(new Shape(root, "Qd dark disc", meshQuad, matShadow)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(3f, 3f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing || by == null) { s.life = 0f; return; }
                    bool on = QdFlying(u) && QdStageOf(by) != RagdollPawn.QdDive;
                    float y = on ? BrdGroundBelow(by.Hips.position, 40f) : float.NaN;
                    s.bright = on && !float.IsNaN(y) ? 1f : 0f;
                    if (s.bright <= 0f) return;
                    s.at = new Vector3(by.Hips.position.x, y + 0.08f, by.Hips.position.z);
                    s.color = new Color(0f, 0f, 0f, 1f);
                    s.paint = 0.35f;
                },
            });
            UltGaze(u, () => QdFlying(u) && QdStageOf(u.by) != RagdollPawn.QdDive);
            if (Viewer != u.by) return;
            // Her screen: the dive's aim (its rings, its square with the coordinate, the arrow, the drop line, the aim spot).
            float r = Params != null ? Params.qdRadius : 8f, sac = Params != null ? Params.qdSacRadius : 16f;
            bool Show() => u.Playing && f.haveAim && QdStageOf(u.by) == RagdollPawn.QdFly && (u.by.PlanNote & RagdollPawn.QdDivesMask) > 0;
            Add(new Shape(root, "Qd aim ring", meshQuad, matRing)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(r * 2f / 0.86f, r * 2f / 0.86f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool show = Show();
                    s.bright = show ? 0.8f + 0.2f * Mathf.Sin(Clock * 8f) : 0f;
                    if (!show) return;
                    s.at = f.aim + Vector3.up * 0.08f;
                    s.color = f.blocked ? new Color(0.5f, 0.5f, 0.55f) : HdrA(Teal, 2.4f);
                },
            });
            Add(new Shape(root, "Qd sacrifice ring", meshQuad, matBrdRingRed)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(sac * 2f / 0.86f, sac * 2f / 0.86f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool show = Show() && !f.blocked;
                    s.bright = show ? 0.25f : 0f;
                    if (!show) return;
                    s.at = f.aim + Vector3.up * 0.07f;
                    s.color = HdrA(Blood, 1.6f);
                },
            });
            Add(new Shape(root, "Qd aim fill", meshQuad, matBrdFill)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare * 0.96f, FamSquare * 0.96f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool show = Show();
                    s.bright = show ? 1f : 0f;
                    if (!show) return;
                    s.at = NmCellAt(f.aim) + Vector3.up * 0.035f;
                    s.color = f.blocked ? new Color(0.6f, 0.6f, 0.62f) : HdrA(Teal, 1.15f);
                    s.paint = 0.42f;
                },
            });
            Add(new Shape(root, "Qd aim frame", meshQuad, matFrame)
            {
                life = 1e9f, rotation = FlatOnFloor(), scale = new Vector3(FamSquare * 0.96f, FamSquare * 0.96f, 1f),
                animate = (s, t) =>
                {
                    if (!u.Playing) { s.life = 0f; return; }
                    bool show = Show();
                    s.bright = show ? 1f : 0f;
                    if (!show) return;
                    s.at = NmCellAt(f.aim) + Vector3.up * 0.045f;
                    s.color = f.blocked ? new Color(0.6f, 0.6f, 0.62f) : HdrA(TealPale, 3.2f);
                },
            });
            // The drop line: twelve dashes from her down to the aim.
            for (int i = 0; i < 12; i++)
            {
                float k0 = i / 12f;
                Add(new Shape(root, "Qd drop dash", meshCylinder, matBrdLine)
                {
                    life = 1e9f,
                    animate = (s, t) =>
                    {
                        if (!u.Playing) { s.life = 0f; return; }
                        bool show = Show() && !f.blocked && by != null;
                        s.bright = show ? 1f : 0f;
                        if (!show) return;
                        Vector3 top = by.Hips.position - Vector3.up * 0.6f, bottom = f.aim + Vector3.up * 0.1f;
                        s.at = Vector3.Lerp(top, bottom, k0);
                        s.rotation = Quaternion.FromToRotation(Vector3.up, (bottom - top).normalized);
                        s.scale = new Vector3(0.04f, (bottom - top).magnitude / 12f * 0.55f, 0.04f);
                        s.color = HdrA(TealPale, 2f);
                    },
                });
            }
            UltArrow(() => by != null ? NmFromCell(u) : u.castAt, () => NmCellAt(f.aim), Teal, 0.8f, false, 1e9f, () => Show() && !f.blocked);
            UltAimSpot(u, () => Show() ? NmCellAt(f.aim) : (Vector3?)null, () => f.blocked ? new Color(0.5f, 0.5f, 0.5f) : Teal);
            // The queen's rays: chess.com's legal-move dots on her eight lines from the square under her.
            BoardDots(u, () => by != null ? Cell(by.Hips.position) : u.fromCell, QdLines, 7, Teal, 1.6f, () => Show());
        }

        void QdStep(UltShow u, float dt, Camera cam)
        {
            if (!(u.state is QdFx f) || u.by == null || Params == null) return;
            if (QdFlying(u)) UltBed(UltClipId.Wind, 0.25f * UltAudience(u, u.by.Hips.position));
            if (Viewer != u.by || cam == null) return;
            if (QdStageOf(u.by) != RagdollPawn.QdFly || !u.Playing)
            {
                f.zone?.Hide();
                f.zoneRed?.Hide();
                return;
            }
            f.aim = RagdollPawn.DriveAimPoint(u.by, ViewAim(cam), Params.qdReach, out _);
            f.blocked = !RagdollPawn.SkyOver(f.aim, Mathf.Max(4f, u.by.Hips.position.y - f.aim.y - 3f));
            f.haveAim = true;
            // The squares inside the dive's ring framed (×1.0), an enemy's red (×2.4).
            if (f.zone == null) f.zone = BrdMesh("Qd dive squares", matFrame);
            if (f.zoneRed == null) f.zoneRed = BrdMesh("Qd dive squares red", matBrdFrameRed);
            f.zone.Begin(cam);
            f.zoneRed.Begin(cam);
            float r = Params.qdRadius;
            qdTaken.Clear();
            foreach (var e in RagdollPawn.All)
                if (e != null && e != u.by && Teams.AreEnemies(e.Team, u.by.Team) && e.State == PawnState.Active) qdTaken.Add(Cell(e.Hips.position));
            if (!f.blocked && (u.by.PlanNote & RagdollPawn.QdDivesMask) > 0)
            {
                Vector2Int c0 = Cell(f.aim);
                int n = Mathf.CeilToInt(r / FamSquare);
                for (int dx = -n; dx <= n; dx++)
                    for (int dz = -n; dz <= n; dz++)
                    {
                        var c = c0 + new Vector2Int(dx, dz);
                        Vector3 p = CellFlat(c);
                        if (Flat(p - f.aim).magnitude > r) continue;
                        float fy = BoardFloor(u, c, f.aim.y);
                        if (float.IsNaN(fy)) continue;
                        p.y = fy + 0.05f;
                        if (qdTaken.Contains(c)) f.zoneRed.Quad(p, FamSquare * 0.96f, BrdTimes(Blood, 2.4f), 1f);
                        else f.zone.Quad(p, FamSquare * 0.96f, BrdTimes(Teal, 1f), 1f);
                    }
            }
            f.zone.End();
            f.zoneRed.End();
            // Enemies on her rays: a capture ring (refreshed four times a second).
            if (Clock >= f.nextRing)
            {
                f.nextRing = Clock + 0.25f;
                Vector2Int me = Cell(u.by.Hips.position);
                foreach (var e in RagdollPawn.All)
                {
                    if (e == null || e == u.by || !Teams.AreEnemies(e.Team, u.by.Team) || e.State != PawnState.Active) continue;
                    Vector2Int d = Cell(e.Hips.position) - me;
                    int ax = Mathf.Abs(d.x), az = Mathf.Abs(d.y);
                    if ((ax == 0 || az == 0 || ax == az) && Mathf.Max(ax, az) <= 7 && (ax | az) != 0) CaptureRing(e, 0.3f);
                }
            }
        }

        readonly HashSet<Vector2Int> qdTaken = new HashSet<Vector2Int>();

        void QdClear(QdFx f)
        {
            BrdKill(f.zone);
            BrdKill(f.zoneRed);
            f.zone = f.zoneRed = null;
        }

        /// <summary>A dive (count 1) or the sacrifice (count 2) committed, on every screen: her shadow growing for the second she
        /// comes down; its ring red with the squares inside rippling red from the middle, a red pool of light, a capture ring on
        /// everyone in it, the falling whistle; the dive's move on the card. The sacrifice: a red neon ring round 16 m over a
        /// 9×9 red checker stamp, "Q×h7+!!" in the top band, her own screen slowed into the blast from 0.58 s.</summary>
        void QdWarnFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is QdFx f)) return;
            bool sac = e.count == 2;
            float r = Mathf.Max(2f, e.size), time = Params != null ? Params.qdDiveTime : 1f;
            Vector3 floor = Ground(e.at), at = floor + Vector3.up * 0.09f;
            Add(new Shape(root, "Qd shadow", meshQuad, matShadow)
            {
                life = time + 0.05f, at = at, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    float k = Smooth01(s.Age / time);
                    float size = r * 2f * Mathf.Lerp(0.35f, 1.05f, k);
                    s.scale = new Vector3(size, size, 1f);
                    s.color = new Color(0.02f, 0.02f, 0.03f, 1f);
                    s.bright = 0.25f + 0.6f * k;
                },
            });
            Add(new Shape(root, "Qd ring", meshQuad, matBrdRingRed)
            {
                life = time + 0.05f, at = at + Vector3.up * 0.01f, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    float k = Smooth01(s.Age / time), size = r * 2f / 0.86f * Mathf.Lerp(1.2f, 1f, k);
                    s.scale = new Vector3(size, size, 1f);
                    s.color = HdrA(Blood, sac ? 3f : 2.6f);
                    s.bright = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Clock * (sac ? 16f : 10f)));
                },
            });
            QdRipple(u, floor, r, time, sac);
            float until = Clock + time + 0.1f;
            UltWarnPool(u, () => floor, r, time, sac ? 140f : 70f, () => Clock < until, sac ? 10f : 7f);
            foreach (var p in RagdollPawn.All)
                if (p != null && u.by != null && p != u.by && Teams.AreEnemies(p.Team, u.by.Team) && p.State == PawnState.Active && Flat(p.Hips.position - floor).magnitude <= r)
                    CaptureRing(p, time + 0.2f, true);
            var cell = Cell(floor);
            SquareMark(u, cell, Blood, 0f, 0f, Blood, 2.6f, UltCoord(u, cell), time + 0.15f, null, floor.y);
            UltAudioBuild();
            var me = Viewer;
            bool inside = me != null && Flat(me.Hips.position - floor).magnitude <= r + 1f;
            UltPlay(sfxWhistle, inside ? 0.9f : 0.8f * UltAudience(u, floor), 0f, sac ? 0.85f : 1f);
            if (sac)
            {
                f.sacrificed = true;
                f.sacWarnAt = Clock;
                BoardStamp(floor, 4, 0.04f, time + 0.4f, Blood, 1.4f, 1.1f);
                UltTopWord(QdSacNota(u) + "!!", HdrA(Teal, 1.2f), 56, 1.2f);
                float slowAt = Clock + 0.58f;
                anims.Add(new Ongoing((age, dt) =>
                {
                    if (Clock < slowAt) return u.Playing || u.hitAt >= 0f;
                    UltSlow(u, UltSlowKind.SigAnticipate);
                    return false;
                }));
            }
            else
            {
                f.dives++;
                int dive = f.dives;
                f.risen = false;
                // The move is typed now; a dive on nobody turns grey at its landing.
                qdPending[u] = dive;
            }
        }

        readonly Dictionary<UltShow, int> qdPending = new Dictionary<UltShow, int>();

        string QdSacNota(UltShow u) => u.black ? Mirror("Q×h7+") : "Q×h7+";

        /// <summary>The squares inside a dive's ring rippling red from the middle out (0.04 s a ring; fill ×1.1, frames ×2.6).</summary>
        void QdRipple(UltShow u, Vector3 floor, float r, float life, bool sac)
        {
            var frames = BrdMesh("Qd ripple", matBrdFrameRed);
            var fills = BrdMesh("Qd ripple fill", matBrdFillRed);
            Vector2Int c0 = Cell(floor);
            int n = Mathf.Min(7, Mathf.CeilToInt(r / FamSquare));
            var floors = new Dictionary<Vector2Int, float>();
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age >= life + 0.1f) return false;
                var cam = ViewCamera;
                frames.Begin(cam);
                fills.Begin(cam);
                for (int ring = 0; ring <= n; ring++)
                {
                    float a = age - ring * 0.04f;
                    if (a < 0f) break;
                    for (int dx = -ring; dx <= ring; dx++)
                        for (int dz = -ring; dz <= ring; dz++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                            var c = c0 + new Vector2Int(dx, dz);
                            Vector3 p = CellFlat(c);
                            if (Flat(p - floor).magnitude > r) continue;
                            if (!floors.TryGetValue(c, out float fy))
                            {
                                if (!BrdCastAllowed()) continue;
                                fy = BrdCastFloor(c, floor.y);
                                floors[c] = fy;
                            }
                            if (float.IsNaN(fy)) continue;
                            p.y = fy;
                            float k = Mathf.Clamp01(a / 0.06f);
                            frames.Quad(p + Vector3.up * 0.05f, FamSquare * 0.96f * BrdPop(a), BrdTimes(Blood, 2.6f), k);
                            BrdFillColour(Blood, 1.1f, sac ? 0.32f : 0.26f, k, out var rgb, out var alpha);
                            fills.Quad(p + Vector3.up * 0.035f, FamSquare * 0.96f, rgb, alpha);
                        }
                }
                frames.End();
                fills.End();
                return true;
            }, () =>
            {
                BrdKill(frames);
                BrdKill(fills);
            }));
        }

        /// <summary>The blow (DriveLand), on every screen: by the hits - two or more the blow (three or more slowed after it once),
        /// one a capture, none a punch; a 7×7 checker stamp, a teal square wave to 10 m and a white one to 15 m, dust, white
        /// sparks, a blow light, the dive's move on the card, a flash of her posts as she rises. The sacrifice: its blast - the
        /// frames (the badge stamped, the queen in ink), the floor in floating tiles, a teal light post where she stood, her
        /// body bursting into teal square shards, and 0.5 s on the reply "…K×h7".</summary>
        void QdLandFx(UltShow u, SkillFxEvent e)
        {
            if (!(u.state is QdFx f)) return;
            Vector3 floor = Ground(e.at);
            float r = Mathf.Max(3f, e.size);
            int hits = e.count;
            bool sac = f.sacrificed || (u.by != null && (u.by.PlanNote & RagdollPawn.QdSacrifice) != 0) || r > 12f;
            Vector3 anchor = floor + Vector3.up * 1.5f;
            float lvl = UltAudience(u, floor);
            if (sac)
            {
                UltBlow(u, ImpactSeq.Sacrifice, anchor);
                UltSlowImpact();
                UltShatter(floor, r * 0.8f, true);
                if (u.by != null)
                {
                    LightPost(Ground(u.by.Hips.position), 20f, 0.2f, 0.4f, 1f, true, 1.2f);
                    QdShards(u.by);
                }
                Vector3 replyAt = floor;
                float replyTime = Clock + 0.5f;
                anims.Add(new Ongoing((age, dt) =>
                {
                    if (Clock < replyTime) return true;
                    string reply = u.black ? Mirror("…K×h7") : "…K×h7";
                    UltMove(u, reply, replyAt, true);
                    UltTopWord(reply, MissGreyTone, 40, 1f);
                    UltClip(UltClipId.Capture, 0.7f * UltAudience(u, replyAt));
                    return false;
                }));
            }
            else
            {
                if (hits >= 2)
                {
                    UltBlow(u, ImpactSeq.Blow, anchor);
                    if (hits >= 3) UltSlow(u, UltSlowKind.SigAftermath);
                }
                else if (hits == 0) UltBlow(u, ImpactSeq.Punch, anchor);
                if (qdPending.TryGetValue(u, out int dive))
                {
                    UltMove(u, QdNota(u, dive, hits > 0), floor, hits == 0);
                    qdPending.Remove(u);
                }
                QdPosts(floor, 22f, 0.6f);   // (she rises again through a flash of her posts)
            }
            BoardStamp(floor, sac ? 4 : 3, 0.05f, 2f, sac ? Blood : Teal, 3f, 0f, sac);
            UltSquareWave(floor, FamSquare * 0.5f, 10f, 0.8f, sac ? Blood : Teal, 3f);
            UltSquareWave(floor, FamSquare * 0.5f, 15f, 1.1f, Color.white, 1.2f, 0.08f);
            DustRing(floor, 24, 1.6f, r * 0.75f, new Color(0.5f, 0.5f, 0.52f), 1.1f, 1.2f);
            SparkBurst(floor + Vector3.up * 0.5f, 40, Color.white, Vector3.up, 60f, 6f, 16f, 0.9f, 0.045f);
            UltBlowLight(floor + Vector3.up * 3f);
            UltClip(UltClipId.Crack, 0.9f * lvl);
            UltPlay(sfxImpactLow, 1f * lvl);
            UltPlay(sfxBoom, 0.7f * lvl);
            UltClip(UltClipId.SubDrop, 0.9f * lvl);
            var cam = ViewCamera;
            float near = cam != null ? Mathf.Clamp01(1f - Vector3.Distance(cam.transform.position, floor) / 45f) : 0.5f;
            Shake(Viewer == u.by ? 0.3f : (sac ? 0.32f : 0.22f) * near, sac ? 0.9f : 0.6f);
        }

        /// <summary>The sacrificed queen's body bursting into 24 teal-edged square shards (0.3 m) that rise and fade over 1.2 s.</summary>
        void QdShards(RagdollPawn queen)
        {
            Vector3 c = ChestOf(queen);
            for (int i = 0; i < 24; i++)
            {
                Vector3 start = c + Random.insideUnitSphere * 0.6f, vel = Random.onUnitSphere * Random.Range(1f, 3f) + Vector3.up * Random.Range(2f, 4f);
                var spin = Random.rotationUniform;
                float turn = Random.Range(-360f, 360f);
                Add(new Shape(root, "Qd shard", meshQuad, matFrame)
                {
                    tick = UltTick.Show, life = 1.2f, scale = new Vector3(0.3f, 0.3f, 1f),
                    animate = (s, t) =>
                    {
                        float a = s.Age;
                        s.at = start + vel * a + Vector3.up * (0.5f * a * a);
                        s.rotation = spin * Quaternion.Euler(0f, turn * a, turn * 0.5f * a);
                        s.color = HdrA(Teal, 2.6f);
                        s.bright = 1f - Smooth01((t - 0.4f) / 0.6f);
                    },
                });
            }
        }

        void QdStrikeFx(UltShow u, SkillFxEvent e)
        {
            if (e.target == null) return;
            UltSound(UltSfx.Stomp, ChestOf(e.target), e.count);
            UltScreenStrike(u, e);
        }

        /// <summary>She comes down at the end (QdDown): dust, a teal square wave; and a flash of her posts as she rises after
        /// each dive.</summary>
        void QdStateFx(UltShow u, SkillFxEvent e)
        {
            if (e.count != RagdollPawn.QdDown) return;
            Vector3 floor = Ground(e.at);
            DustRing(floor, 14, 0.8f, 4f, new Color(0.55f, 0.55f, 0.57f), 0.8f, 0.8f);
            UltSquareWave(floor, FamSquare * 0.5f, 6f, 0.5f, Teal, 3f);
            UltClip(UltClipId.Tock, 0.6f * UltAudience(u, floor));
        }

        string QdHint(UltShow u)
        {
            if (u.by == null) return null;
            int stage = QdStageOf(u.by), dives = u.by.PlanNote & RagdollPawn.QdDivesMask;
            if (stage == RagdollPawn.QdDive) return (u.by.PlanNote & RagdollPawn.QdSacrifice) != 0 ? "퀸 희생!!" : "내리꽂기!";
            if (stage == RagdollPawn.QdRise) return "솟는 중…";
            if (stage == RagdollPawn.QdDown) return "내려서는 중";
            if (u.state is QdFx f && f.blocked) return "지붕 밑에는 못 내리꽂아요 — 다른 데를 봐요";
            return dives > 0 ? "이동 키 = 날기  ·  좌클릭 = 보는 곳에 내리꽂기  ·  우클릭 = 퀸 희생 (16 m, 퀸은 잡혀요)" : "내리꽂기를 다 썼어요";
        }

        string QdLine(UltShow u) => $"내리꽂기 {(u.by != null ? u.by.PlanNote & RagdollPawn.QdDivesMask : 0)}번  ·  날린 적 {u.struckSet.Count}";

        string QdWarning(UltShow u, RagdollPawn me)
        {
            if (u.by == null || me == null || QdStageOf(u.by) != RagdollPawn.QdDive || Params == null) return null;
            float r = (u.by.PlanNote & RagdollPawn.QdSacrifice) != 0 ? Params.qdSacRadius : Params.qdRadius;
            return Flat(me.Hips.position - u.by.PlanPoint).magnitude <= r + 1f ? "퀸이 내리꽂아요 — 그림자에서 흩어져요!" : null;
        }

        /// <summary>Before the key: the enemies within the dive's reach under open sky, on the queen's screen.</summary>
        void QdPreview(RagdollPawn me, PawnRushSkillParams s, HashSet<RagdollPawn> wanted)
        {
            foreach (var e in RagdollPawn.All)
            {
                if (e == null || e == me || !Teams.AreEnemies(e.Team, me.Team) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                if (Flat(e.Hips.position - me.Hips.position).magnitude > s.qdReach) continue;
                if (!RagdollPawn.SkyOver(e.FeetPoint, 8f)) continue;
                wanted.Add(e);
            }
        }

        /// <summary>The sacrifice's judgement comes 0.6 s after the blast (the spec's §10: the host graded it in the blast's own
        /// frame; the reply is seen first).</summary>
        bool QdJudgeLater(UltShow u) => u.skill == PawnRushSkillIds.QueenDescent && u.state is QdFx f && f.sacrificed;
    }
}
