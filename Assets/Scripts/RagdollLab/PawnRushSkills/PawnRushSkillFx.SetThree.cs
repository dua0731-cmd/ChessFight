using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 3판's things and moments on every screen (RagdollPawn.SetThree.cs; JY-skill-lab 11-23), drawn from the S3
    /// moments. 11-24, the comic remake (the user: "웃기고 코믹한 느낌이되 너무 허접하진 않고 퀄리티 좋게"; the parts in
    /// PawnRushSkillFx.Comic.cs): what was a glowing ball or bar is a chunky inked prop now, and every hit pops.
    ///
    ///   the bolas    two iron weights on a rope whirling flat with white whirl streaks; caught, the rope snaps twice round
    ///                the shins, the weights swinging, dust skidding off the feet while it is dragged; the chop: a big pop on
    ///                the head, a squash, dizzy pawns; the two bumping: a pop between them, both dizzy
    ///   the knight   an inked streak down its dive (the stomp itself is design A's); the back kick: a little puff and a
    ///                springy squash for the hip bob, then the hoof's smack behind (white pop, C ring flung back, dust kicked
    ///                back) and a streak along its hop forward
    ///   the rook     dust thrown up the whole roll and a streak behind it (the rolling column is its own piece baked); the
    ///                slam: a ring of dust; a wall: a pop and stars of little pawns; the split ground: a dark jagged crack
    ///                running along the floor with dust at its tip; the tower: its own piece in chunky stone rising with a
    ///                ring of dust round the one shut in, sinking into a poof of rubble-coloured cloud when it falls
    ///   the queen    the star: an eight-pointed gold outline on the floor with a see-through fill, popping in and turning
    ///                slowly under her; a lit blade: a gold beam along it; a check: a red square popping under the target and
    ///                her streak as she flies in
    ///   the bishop   the summons: a gold poof and ring at its feet; a sword (steel blade, gold hilt) popping in beside each
    ///                enemy, bobbing, with its diagonal laid in small gold squares that fill as the fuse burns, then flying with
    ///                a gold streak; a hit: a gold smack and gold rings binding the body; a miss: the sword stuck, wobbling,
    ///                gone in a poof; the stone: a cartoon boulder rolling with dust off its heels and a poof each time it lands,
    ///                breaking into a cloud; the holy water: a blue flask with a gold cork and cross tumbling along its arc, a
    ///                splash, a wobbly blue puddle with ripples and glints, splashes where someone slips
    ///   any hit      (S3Hit) the comic smack in the hitter's colour, an inked streak on the one flying; a third hit pops gold
    /// </summary>
    public partial class PawnRushSkillFx
    {
        sealed class CmBolas { public RagdollPawn by; public Vector3 pos, dir; public float speed, flown, range, spin; public bool done; }
        sealed class CmSword { public RagdollPawn by; public int id; public Vector3 at, dir, to, pos; public float born, fuse, flyAt = -1f, endAt = -1f, speed; public bool hit; }
        sealed class CmStone { public RagdollPawn by; public int id; public Vector3 pos, vel; public float flown; public bool done; }
        sealed class CmCrack { public RagdollPawn by; public Vector3 from, dir, tip; public float speed; public bool done; }
        sealed class CmTower { public RagdollPawn inside; public float born, until; public Vector3 size, feet; }
        sealed class CmStarFx { public RagdollPawn queen; public Vector3 center; public float radius, turn; }
        sealed class S3Look
        {
            public RagdollPawn by;
            public Mesh baked;
            public Matrix4x4 canon = Matrix4x4.identity;
            public Vector3 size = Vector3.one;
            public Material[] mats;
            public float angle, born;
        }

        readonly List<CmBolas> cmBolas = new List<CmBolas>();
        readonly List<CmSword> cmSwords = new List<CmSword>();
        readonly List<CmStone> cmStones = new List<CmStone>();
        readonly List<CmCrack> cmCracks = new List<CmCrack>();
        readonly List<CmTower> cmTowers = new List<CmTower>();
        readonly Dictionary<RagdollPawn, CmStarFx> cmStars = new Dictionary<RagdollPawn, CmStarFx>();
        Mesh cmTowerMesh;
        readonly Dictionary<RagdollPawn, S3Look> s3Rolls = new Dictionary<RagdollPawn, S3Look>();
        readonly List<RagdollPawn> s3RollsGone = new List<RagdollPawn>();
        MaterialPropertyBlock cmFillBlock;   // made on first use (none in a component's field initializers)
        bool s3Built;

        readonly HashSet<string> fxFaults = new HashSet<string>();

        /// <summary>An effect threw: written to the log once (by where and what), and the frame or the moment goes on
        /// without it.</summary>
        void FxFault(string where, System.Exception ex)
        {
            if (fxFaults.Add(where + ":" + ex.GetType().Name + ":" + ex.Message)) Debug.LogException(ex);
        }

        void S3Build()
        {
            if (s3Built) return;
            s3Built = true;
            BuildUltParts();
            BrdBuild();
            BuildPrevizParts();
            UltAudioBuild();
        }

        float S3Heard(Vector3 at)
        {
            var me = Viewer;
            return me != null && (me.Hips.position - at).sqrMagnitude < 9f ? 1f : UltHeard(at);
        }

        Kit.Palette S3Colors(RagdollPawn pawn) => kit != null && pawn != null ? kit.PieceColors(pawn) : CmWhite;

        static void CmPlace(Kit.Prop prop, Vector3 at, Quaternion rot, Vector3 scale)
        {
            prop.t.SetPositionAndRotation(at, rot);
            prop.t.localScale = scale;
            prop.Show(true);
            prop.Apply();
        }

        // ---------------------------------------------------------------- the moments

        void SetThreeFx(SkillFxEvent e)
        {
            S3Build();
            float lvl = S3Heard(e.at);
            switch (e.kind)
            {
                case SkillFxKind.S3Bolas: CmBolasThrow(e); UltClip(UltClipId.Whoosh, 0.5f * lvl, 1.4f); break;
                case SkillFxKind.S3BolasCatch: CmBolasCatch(e); UltClip(UltClipId.Tap, 0.7f * lvl, 0.8f); break;
                case SkillFxKind.S3BolasEnd: CmBolasEnd(e, lvl); break;
                case SkillFxKind.S3Stun: if (e.target != null) kit?.Daze(e.target, 0f, Mathf.Max(0.3f, e.size)); break;
                case SkillFxKind.S3Bound: CmBound(e); UltClip(UltClipId.Shimmer, 0.5f * lvl, 1.3f); break;
                case SkillFxKind.S3HopDive:
                    if (e.by != null) CmStreak(e.by, S3Colors(e.by), 0.45f, 0.24f, 0.14f);
                    UltClip(UltClipId.Whoosh, 0.7f * lvl, 0.8f);
                    break;
                case SkillFxKind.S3Roll:
                    if (e.by != null) S3BakeRoll(e.by);
                    CmRollStart(e);
                    UltClip(UltClipId.Castle, 0.6f * lvl, 0.8f);
                    break;
                case SkillFxKind.S3RollEnd: CmRollEnd(e, lvl); break;
                case SkillFxKind.S3Crack: CmCrackFx(e, lvl); break;
                case SkillFxKind.S3Tower: CmTowerRise(e); UltClip(UltClipId.Castle, 0.9f * lvl, 0.6f); break;
                case SkillFxKind.S3TowerFall: CmTowerFall(e); UltClip(UltClipId.Crack, 0.8f * lvl, 0.5f); break;
                case SkillFxKind.S3Star:
                    CmStar(e);
                    UltClip(e.count == 1 ? UltClipId.Shimmer : UltClipId.Riser, (e.count == 1 ? 0.7f : 0.5f) * lvl, e.count == 1 ? 1f : 1.6f);
                    break;
                case SkillFxKind.S3StarBlade: CmStarBlade(e); UltClip(e.count == 255 ? UltClipId.Tick : UltClipId.Capture, 0.6f * lvl, 1.1f); break;
                case SkillFxKind.S3Check: CmCheck(e); UltClip(UltClipId.Check, 0.8f * lvl, 1f + 0.08f * e.count); break;
                case SkillFxKind.S3SwordRise: CmSwordRise(e); break;
                case SkillFxKind.S3SwordFly: CmSwordFly(e); UltClip(UltClipId.Whoosh, 0.4f * lvl, 1.5f); break;
                case SkillFxKind.S3SwordEnd:
                    CmSwordEnd(e);
                    if (e.target != null) UltClip(UltClipId.Capture, 0.6f * lvl, 1.4f);
                    break;
                case SkillFxKind.S3StoneShot: CmStoneShot(e); UltClip(UltClipId.Whoosh, 0.6f * lvl, 0.6f); break;
                case SkillFxKind.S3StoneBreak: CmStoneBreak(e); UltClip(UltClipId.Crack, 0.8f * lvl, 0.8f); break;
                case SkillFxKind.S3WaterThrow: CmFlaskThrow(e); break;
                case SkillFxKind.S3Puddle: CmPuddle(e); UltClip(UltClipId.Crack, 0.5f * lvl, 1.8f); break;
                case SkillFxKind.S3Slip: CmSlip(e); UltClip(UltClipId.Wah, 0.6f * lvl, 1.1f); break;
                case SkillFxKind.S3Kick: CmKick(e, lvl); break;
                case SkillFxKind.S3Hit: CmHit(e); break;
            }
        }

        // ---------------------------------------------------------------- any hit

        void CmHit(SkillFxEvent e)
        {
            if (e.target == null) return;
            var p = S3Colors(e.by);
            float size = 1f + 0.15f * Mathf.Clamp(e.count - 1, 0, 3);
            CmSmack(e.target, e.at, FlatDir(e.dir) + Vector3.up * 0.35f, p, size);
            CmStreak(e.target, p, 0.45f, 0.16f, 0.15f);
            if (e.count == 3 && kit != null)
            {
                // A third in one go: a gold pop (the roll's "스트라이크", the stone's, the star's).
                kit.FlashRing(Ground(e.at) + Vector3.up * 0.05f, 1.6f, CmGold);
                CmPoof(e.at + Vector3.up * 0.3f, CmGold, 7, 1.1f, 0.38f, 0.5f, 0.6f);
            }
        }

        // ---------------------------------------------------------------- the pawn's bolas

        void CmBolasThrow(SkillFxEvent e)
        {
            if (kit == null || e.by == null) return;
            foreach (var old in cmBolas) if (old.by == e.by) old.done = true;
            var b = new CmBolas
            {
                by = e.by, pos = e.at, dir = e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : Vector3.forward,
                speed = Mathf.Max(1f, e.size), range = Params != null ? Params.bolasRange : 12f,
            };
            cmBolas.Add(b);
            var w1 = CmProp(kit.meshBall, CmIron, "Bolas weight");
            var w2 = CmProp(kit.meshBall, CmIron, "Bolas weight");
            var rope = new Kit.Strip(kit, CmRope) { core = CmRope.light, coreShare = 0.25f, inkShare = 0.3f };
            var whirl1 = new Kit.Strip(kit, CmWhite) { core = Color.white, coreShare = 0.5f, inkShare = 0.18f };
            var whirl2 = new Kit.Strip(kit, CmWhite) { core = Color.white, coreShare = 0.5f, inkShare = 0.18f };
            var cord = new List<Vector3>();
            var arc = new List<Vector3>();
            const float R = 0.32f;
            var f = kit.Run(3f, (fx, d) =>
            {
                if (b.done || b.by == null || b.flown >= b.range + 0.5f) return false;
                float step = b.speed * d;
                b.pos += b.dir * step;
                b.flown += step;
                b.spin += d * 1100f;
                Vector3 side = Quaternion.AngleAxis(b.spin, Vector3.up) * Vector3.right * R;
                Vector3 a1 = b.pos + side, a2 = b.pos - side;
                CmPlace(w1, a1, Quaternion.identity, Vector3.one * 0.2f);
                CmPlace(w2, a2, Quaternion.identity, Vector3.one * 0.2f);
                cord.Clear();
                for (int i = 0; i <= 8; i++)
                {
                    float t = i / 8f;
                    cord.Add(Vector3.Lerp(a1, a2, t) + Vector3.down * (0.07f * Mathf.Sin(t * Mathf.PI)));
                }
                rope.Build(cord, i => 0.05f, kit.Eye);
                rope.Apply();
                foreach (var (strip, from) in new[] { (whirl1, 0f), (whirl2, 180f) })
                {
                    arc.Clear();
                    for (int i = 0; i <= 10; i++) arc.Add(b.pos + Quaternion.AngleAxis(b.spin + from - 110f + i * 11f, Vector3.up) * Vector3.right * R);
                    strip.Build(arc, i => 0.08f * i / 10f, kit.Eye);
                    strip.Apply();
                }
                return true;
            });
            f.props.Add(w1);
            f.props.Add(w2);
            f.strips.Add(rope);
            f.strips.Add(whirl1);
            f.strips.Add(whirl2);
            f.end = () => cmBolas.Remove(b);
        }

        void CmBolasCatch(SkillFxEvent e)
        {
            foreach (var b in cmBolas) if (b.by == e.by) b.done = true;
            if (kit == null || e.target == null) return;
            var who = e.target;
            float pull = Params != null ? Params.bolasPull : 1f;
            CmFlash(e.at, 0.35f);
            CmPoof(who.FeetPoint + Vector3.up * 0.1f, CmDust, 4, 0.5f, 0.3f, 0.4f, 0.2f);
            var loop1 = CmProp(kit.meshTorus, CmRope, "Bolas wrap", 0.015f);
            var loop2 = CmProp(kit.meshTorus, CmRope, "Bolas wrap", 0.015f);
            var w1 = CmProp(kit.meshBall, CmIron, "Bolas weight");
            var w2 = CmProp(kit.meshBall, CmIron, "Bolas weight");
            var f = kit.Run(pull + 0.6f, (fx, d) =>
            {
                if (who == null) return false;
                float t = fx.age;
                Vector3 feet = who.FeetPoint, hips = who.Hips.position;
                Vector3 up = (hips - feet).sqrMagnitude > 0.01f ? (hips - feet).normalized : Vector3.up;
                Quaternion lay = Quaternion.FromToRotation(Vector3.up, up);
                // The loops snap in tight from wide, then hold, turning a little.
                float snap = 1.6f - 0.6f * Kit.EaseOutBack(Mathf.Clamp01(t / 0.14f));
                CmPlace(loop1, feet + up * 0.2f, lay * Quaternion.Euler(8f, t * 200f, 0f), Vector3.one * (0.2f * snap));
                CmPlace(loop2, feet + up * 0.33f, lay * Quaternion.Euler(-8f, -t * 200f, 0f), Vector3.one * (0.18f * snap));
                Vector3 across = Vector3.Cross(up, Vector3.forward);
                if (across.sqrMagnitude < 0.01f) across = Vector3.right;
                Vector3 swing = Quaternion.AngleAxis(t * 380f, up) * across.normalized;
                CmPlace(w1, feet + up * 0.12f + swing * 0.3f, Quaternion.identity, Vector3.one * 0.17f);
                CmPlace(w2, feet + up * 0.12f - swing * 0.3f, Quaternion.identity, Vector3.one * 0.17f);
                return true;
            });
            f.props.Add(loop1);
            f.props.Add(loop2);
            f.props.Add(w1);
            f.props.Add(w2);
            CmSkid(who, pull);
        }

        void CmBolasEnd(SkillFxEvent e, float lvl)
        {
            foreach (var b in cmBolas) if (b.by == e.by) b.done = true;
            if (e.count == 0)
                CmPoof(Ground(e.at) + Vector3.up * 0.1f, CmDust, 5, 0.5f, 0.3f);
            else if (e.count == 2 && e.target != null)
            {
                // The chop on the back of the neck: a big pop on the head, squashed, dizzy.
                Vector3 head = e.target.Hips.position + Vector3.up * 0.55f;
                CmSmack(e.target, head, Vector3.down + FlatDir(e.dir) * 0.3f, S3Colors(e.by), 1.3f);
                kit?.AddSquash(e.target, Kit.SquashKind.Flatten);
                UltClip(UltClipId.Capture, 0.8f * lvl, 1.2f);
            }
            else if (e.count == 3)
            {
                // Missed it: the two bump heads.
                CmFlash(e.at + Vector3.up * 0.6f, 0.45f);
                CmPoof(Ground(e.at) + Vector3.up * 0.2f, CmDust, 6, 0.7f, 0.35f);
                if (e.target != null) kit?.Daze(e.target, 0.05f, 0.8f);
                if (e.by != null) kit?.Daze(e.by, 0.05f, 0.8f);
                UltClip(UltClipId.Wah, 0.6f * lvl);
            }
        }

        // ---------------------------------------------------------------- the knight's back kick

        void CmKick(SkillFxEvent e, float lvl)
        {
            Vector3 fwd = FlatDir(e.dir), back = -fwd;
            var p = S3Colors(e.by);
            if (e.count == 0)
            {
                // The hip bob: a little puff under it and a springy squash.
                CmPoof(e.at + Vector3.up * 0.05f, CmDust, 3, 0.35f, 0.22f, 0.3f, 0.1f);
                if (e.by != null) kit?.AddSquash(e.by, Kit.SquashKind.Spring);
                UltClip(UltClipId.Tap, 0.5f * lvl, 1.4f);
                return;
            }
            bool hit = e.count > 1;
            Vector3 behind = e.at + back * 0.85f + Vector3.up * 0.45f;
            CmFlash(behind, hit ? 0.8f : 0.45f);
            kit?.HalfRing(behind, back, p, hit ? 0.65f : 0.42f);
            CmPoof(e.at + back * 0.6f + Vector3.up * 0.1f, CmDust, 5, 0.9f, 0.32f, 0.45f, 0.25f, back * 0.8f);
            if (e.by != null) CmStreak(e.by, p, 0.35f, 0.22f, 0.12f);
            if (hit)
            {
                HitStop(4f * Frame);
                Shake(0.07f, 10f * Frame);
            }
            UltClip(hit ? UltClipId.Capture : UltClipId.Whoosh, 0.7f * lvl, hit ? 0.9f : 1.2f);
        }

        // ---------------------------------------------------------------- the rook's roll and tower

        void CmRollStart(SkillFxEvent e)
        {
            CmPoof(e.at + Vector3.up * 0.1f, CmDust, 8, 1.2f, 0.4f, 0.5f, 0.2f);
            Shake(0.06f, 8f * Frame);
            var rook = e.by;
            if (rook == null || kit == null) return;
            var p = S3Colors(rook);
            InkTrail(() => rook != null && rook.RollingShort ? rook.Hips.position : (Vector3?)null, p, 0.22f, 0.4f, p.light);
            float next = 0f;
            kit.Run(5f, (fx, d) =>
            {
                if (rook == null || !rook.RollingShort) return fx.age < 0.2f;
                if (fx.age < next) return true;
                next = fx.age + 0.06f;
                Vector3 way = FlatDir(rook.PlanDir);
                CmPoof(Ground(rook.Hips.position) + Vector3.up * 0.1f - way * 0.6f, CmDust, 2, 0.55f, 0.36f, 0.42f, 0.3f, -way * 0.6f);
                return true;
            });
        }

        void CmRollEnd(SkillFxEvent e, float lvl)
        {
            if (e.count == 1)
            {
                // Up with a thump: a ring of dust as wide as the slam.
                float r = Params != null ? Params.rollSlamRadius : 4f;
                CmPoof(Ground(e.at) + Vector3.up * 0.15f, CmDust, 14, r * 0.85f, 0.55f, 0.6f, 0.15f);
                kit?.ImpactRing(Ground(e.at) + Vector3.up * 0.05f, Vector3.up, S3Colors(e.by), 0.5f, r, false);
                Shake(0.12f, 12f * Frame);
                UltClip(UltClipId.Crack, 0.7f * lvl, 0.7f);
            }
            else if (e.count == 2)
            {
                // Into a wall: a pop where it struck, and the rook sees little pawns.
                CmFlash(e.at, 0.7f);
                CmPoof(e.at, CmDust, 6, 0.8f, 0.4f);
                if (e.by != null) kit?.Daze(e.by, 0.05f, 0.6f);
                Shake(0.1f, 10f * Frame);
                UltClip(UltClipId.Capture, 0.8f * lvl, 0.6f);
            }
            else CmPoof(Ground(e.at) + Vector3.up * 0.1f, CmDust, 5, 0.7f, 0.35f);
        }

        void CmCrackFx(SkillFxEvent e, float lvl)
        {
            if (e.count != 0)
            {
                foreach (var c in cmCracks) if (!c.done && (c.by == e.by || e.by == null)) { c.done = true; c.tip = e.at; }
                CmPoof(Ground(e.at) + Vector3.up * 0.1f, CmDust, 6, 0.8f, 0.4f);
                return;
            }
            UltClip(UltClipId.Crack, 0.7f * lvl, 0.6f);
            if (kit == null) return;
            var crack = new CmCrack { by = e.by, from = Ground(e.at), dir = FlatDir(e.dir), speed = Mathf.Max(1f, e.size) };
            crack.tip = crack.from;
            cmCracks.Add(crack);
            var strip = new Kit.Strip(kit, CmCrackInk) { core = CmCrackInk.light, coreShare = 0.18f, inkShare = 0.35f };
            var floor = new List<Vector3>();
            var pts = new List<Vector3>();
            Vector3 side = Vector3.Cross(Vector3.up, crack.dir);
            const float Step = 0.45f;
            float nextDust = 0f;
            var f = kit.Run(3.5f, (fx, d) =>
            {
                float len = crack.done ? Vector3.Distance(crack.from, crack.tip) : fx.age * crack.speed;
                if (!crack.done) crack.tip = crack.from + crack.dir * len;
                int n = Mathf.Max(2, Mathf.CeilToInt(len / Step) + 1);
                // Each joint of the crack found on the floor once, a little to one side or the other.
                while (floor.Count < n)
                {
                    int i = floor.Count;
                    float jag = i == 0 ? 0f : Random.Range(-0.2f, 0.2f);
                    floor.Add(Ground(crack.from + crack.dir * (i * Step) + side * jag + Vector3.up * 0.5f) + Vector3.up * 0.035f);
                }
                pts.Clear();
                for (int i = 0; i < n - 1; i++) pts.Add(floor[i]);
                pts.Add(Vector3.Lerp(floor[n - 2], floor[n - 1], Mathf.Clamp01((len - (n - 2) * Step) / Step)));
                float fade = fx.age > 2.6f ? 1f - (fx.age - 2.6f) / 0.9f : 1f;
                int count = pts.Count;
                strip.Build(pts, i => Mathf.Lerp(0.34f, 0.08f, i / Mathf.Max(1f, count - 1f)), kit.Eye, true);
                strip.fade = fade;
                strip.Apply();
                if (!crack.done && fx.age >= nextDust)
                {
                    nextDust = fx.age + 0.05f;
                    CmPoof(crack.tip + Vector3.up * 0.1f, CmDust, 2, 0.4f, 0.3f, 0.35f, 0.4f);
                }
                return fade > 0f;
            });
            f.strips.Add(strip);
            f.end = () => cmCracks.Remove(crack);
        }

        void CmTowerRise(SkillFxEvent e)
        {
            if (kit == null || e.target == null) return;
            // A standing rook in chunky stone (the rook itself is lying on the floor by now, having thumped down).
            if (cmTowerMesh == null) cmTowerMesh = SkillMeshes.Piece(PieceKind.Rook);
            var mesh = cmTowerMesh;
            Vector3 size = mesh.bounds.size;
            var tower = new CmTower { inside = e.target, born = Clock, until = Clock + Mathf.Max(0.3f, e.size), size = size, feet = e.target.FeetPoint };
            cmTowers.Add(tower);
            var prop = CmProp(mesh, CmRock, "Tower", 0.012f);
            CmPoof(tower.feet + Vector3.up * 0.15f, CmDust, 10, 1.3f, 0.5f, 0.55f, 0.3f);
            Shake(0.08f, 10f * Frame);
            var f = kit.Run(Mathf.Max(0.3f, e.size) + 2f, (fx, d) =>
            {
                if (tower.inside == null) return false;
                float gone = Clock - tower.until;
                if (gone > 0.3f) return false;
                Vector3 feet = new Vector3(tower.inside.Hips.position.x, tower.feet.y, tower.inside.Hips.position.z);
                // Up to the chin, hugging the one inside (narrower than the tower that stops others, towerWide: a 1.6 m
                // wide stone up to a pawn's chin read as a cake), so only the head peeks out of the battlements.
                float tall = Mathf.Max(0.5f, tower.inside.Hips.position.y + 0.42f - feet.y);
                float wide = Mathf.Min(Params != null ? Params.towerWide : 1.6f, 1.1f);
                float rise = Kit.EaseOutBack(Mathf.Clamp01((Clock - tower.born) / 0.18f)), sink = Mathf.Clamp01(gone / 0.3f);
                float h = tall * Mathf.Max(0.02f, rise) * (1f - sink);
                // (Its foot at the mesh's y = 0, 1 tall.)
                CmPlace(prop, feet, Quaternion.identity,
                    new Vector3(wide / tower.size.x * (1f + 0.2f * sink), Mathf.Max(0.01f, h) / tower.size.y, wide / tower.size.z * (1f + 0.2f * sink)));
                return true;
            });
            f.props.Add(prop);
            f.end = () => cmTowers.Remove(tower);
        }

        void CmTowerFall(SkillFxEvent e)
        {
            foreach (var t in cmTowers) if (t.inside == e.target) t.until = Mathf.Min(t.until, Clock);
            CmPoof(Ground(e.at) + Vector3.up * 0.7f, CmRock, 9, 1.2f, 0.55f, 0.6f, 0.5f);
            CmPoof(Ground(e.at) + Vector3.up * 0.1f, CmDust, 8, 1.6f, 0.45f);
            Shake(0.06f, 8f * Frame);
        }

        // ---------------------------------------------------------------- the queen's star and checks

        void CmStar(SkillFxEvent e)
        {
            if (e.count == 1)
            {
                // The bishop's sign that the swords are coming: a gold poof and ring at its feet.
                CmPoof(e.at + Vector3.up * 0.2f, CmGold, 6, 0.7f, 0.26f, 0.45f, 0.6f);
                kit?.FlashRing(Ground(e.at) + Vector3.up * 0.05f, 1.2f, CmGold);
                return;
            }
            if (kit == null) return;
            var q = e.by;
            var star = new CmStarFx { queen = q, center = Ground(e.at), radius = Mathf.Max(1f, e.size), turn = Random.Range(0f, 45f) };
            if (q != null) cmStars[q] = star;
            var outline = new Kit.Strip(kit, CmGold) { core = Color.white, coreShare = 0.25f, inkShare = 0.25f };
            var pts = new List<Vector3>();
            var mesh = CmStarMesh();
            var mat = CmFillMaterial();
            const float Life = 1.3f;
            var f = kit.Run(Life, (fx, d) =>
            {
                if (q != null && fx.age < 0.6f) star.center = Ground(q.FeetPoint);
                float pop = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.22f)), fade = 1f - Mathf.Clamp01((fx.age - (Life - 0.35f)) / 0.35f);
                float r = star.radius * Mathf.Max(0.05f, pop);
                Quaternion rot = Quaternion.Euler(0f, star.turn + fx.age * 6f, 0f);
                Vector3 c = star.center + Vector3.up * 0.04f;
                pts.Clear();
                for (int i = 0; i <= 16; i++)
                {
                    float a = (90f + (i % 16) * 22.5f) * Mathf.Deg2Rad, k = i % 2 == 0 ? 1f : 0.55f;
                    pts.Add(c + rot * new Vector3(Mathf.Cos(a) * k * r, 0f, Mathf.Sin(a) * k * r));
                }
                outline.Build(pts, i => 0.22f, kit.Eye, true);
                outline.fade = fade;
                outline.Apply();
                if (cmFillBlock == null) cmFillBlock = new MaterialPropertyBlock();
                cmFillBlock.SetColor("_Color", new Color(1f, 0.82f, 0.25f, 0.22f * fade));
                var rp = new RenderParams(mat) { layer = 0, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, matProps = cmFillBlock, worldBounds = new Bounds(c, Vector3.one * (r * 2f + 1f)) };
                Graphics.RenderMesh(rp, mesh, 0, Matrix4x4.TRS(c + Vector3.up * 0.01f, rot * Quaternion.Euler(0f, 0f, 0f), new Vector3(r, 1f, r)));
                return true;
            });
            f.strips.Add(outline);
            f.end = () => { if (q != null && cmStars.TryGetValue(q, out var s) && s == star) cmStars.Remove(q); };
        }

        void CmStarBlade(SkillFxEvent e)
        {
            if (kit == null) return;
            if (e.count == 255)
            {
                kit.FlashRing(Ground(e.at) + Vector3.up * 0.05f, 1.4f, CmGold);
                return;
            }
            float radius = Params != null ? Params.starRadius : 8f;
            Vector3 c = Ground(e.at) + Vector3.up * 0.06f, dir = FlatDir(e.dir);
            var beam = new Kit.Strip(kit, CmGold) { core = Color.white, coreShare = 0.45f, inkShare = 0.2f };
            var pts = new List<Vector3>();
            var f = kit.Run(0.35f, (fx, d) =>
            {
                float t = fx.age / 0.35f;
                pts.Clear();
                for (int i = 0; i <= 6; i++) pts.Add(c + dir * (radius * i / 6f * Kit.EaseOut(Mathf.Clamp01(t * 4f))));
                beam.Build(pts, i => 0.42f * (1f - 0.5f * i / 6f), kit.Eye, true);
                beam.fade = 1f - Mathf.Clamp01((t - 0.5f) / 0.5f);
                beam.Apply();
                return true;
            });
            f.strips.Add(beam);
        }

        void CmCheck(SkillFxEvent e)
        {
            if (kit == null || e.target == null) return;
            var tile = new Kit.Tile(kit, "Check square");
            Vector3 at = Ground(e.target.FeetPoint) + Vector3.up * 0.03f, along = FlatDir(e.dir);
            var f = kit.Run(0.6f, (fx, d) =>
            {
                float k = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.12f));
                tile.Floor(at, along, Vector2.one * (1.5f * Mathf.Max(0.05f, k)));
                tile.shape = 0f;
                tile.fill = Kit.A(CmRed.main, 0.85f);
                tile.core = Kit.A(Color.white, 1f);
                tile.coreWidth = 0.03f;
                tile.ink = Kit.A(CmRed.ink, 1f);
                tile.inkWidth = 0.05f;
                tile.rim = Color.clear;
                tile.stripe = Color.clear;
                tile.arc = 1f;
                tile.fade = 1f - Mathf.Clamp01((fx.age - 0.4f) / 0.2f);
                tile.Apply();
                return true;
            });
            f.tiles.Add(tile);
            if (e.by != null) CmStreak(e.by, CmGold, 0.3f, 0.26f, 0.12f);
        }

        // ---------------------------------------------------------------- the bishop's swords

        void CmSwordRise(SkillFxEvent e)
        {
            if (kit == null) return;
            var sw = new CmSword
            {
                by = e.by, id = e.count, at = e.at, pos = e.at, dir = e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : Vector3.forward,
                born = Clock, fuse = Mathf.Max(0.2f, e.size),
            };
            cmSwords.Add(sw);
            var blade = CmProp(CmBladeMesh(), CmSteel, "Holy sword", 0.01f);
            var hilt = CmProp(CmHiltMesh(), CmGold, "Holy sword hilt", 0.01f);
            CmFlash(e.at, 0.45f, 8, CmGold);
            // Its diagonal on the floor in small gold squares, filling as the fuse burns.
            var squares = new List<Kit.Tile>();
            float reach = (Params != null ? Params.swordSide : 5f) * 2f;
            Vector3 floor0 = Ground(e.at), way = FlatDir(sw.dir);
            int count = Mathf.Clamp(Mathf.RoundToInt(reach / 0.75f), 2, 16);
            var spots = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                spots[i] = Ground(floor0 + way * (0.75f * (i + 0.5f)) + Vector3.up * 0.6f) + Vector3.up * 0.03f;
                squares.Add(new Kit.Tile(kit, "Sword square"));
            }
            var trailOn = false;
            var f = kit.Run(sw.fuse + 4f, (fx, d) =>
            {
                float age = Clock - sw.born;
                float scale = 1.3f * Kit.EaseOutBack(Mathf.Clamp01(age / 0.22f));
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, sw.dir);
                if (sw.endAt >= 0f)
                {
                    float since = Clock - sw.endAt;
                    if (sw.hit || since > 0.45f) return false;
                    // Stuck where it struck, wobbling.
                    rot = Quaternion.AngleAxis(Mathf.Sin(since * 40f) * 14f * (1f - since / 0.45f), Vector3.Cross(sw.dir, Vector3.up).normalized) * rot;
                }
                else if (sw.flyAt < 0f)
                {
                    // The skill gone before it flew (the bishop reset or knocked over): the sword goes in a poof.
                    if (age > sw.fuse + 1.2f)
                    {
                        CmPoof(sw.pos, CmGold, 4, 0.4f, 0.24f, 0.35f, 0.5f);
                        return false;
                    }
                    sw.pos = sw.at + Vector3.up * (0.08f * Mathf.Sin(age * 6f));
                }
                else
                {
                    if (Clock - sw.flyAt > 3f) return false;
                    if (!trailOn)
                    {
                        trailOn = true;
                        InkTrail(() => sw.endAt < 0f ? sw.pos + sw.dir * 0.6f : (Vector3?)null, CmGold, 0.14f, 0.28f, Color.white);
                    }
                    float total = (sw.to - sw.at).magnitude;
                    sw.pos = sw.at + sw.dir * Mathf.Min((Clock - sw.flyAt) * sw.speed, total);
                }
                CmPlace(blade, sw.pos, rot, Vector3.one * scale);
                CmPlace(hilt, sw.pos, rot, Vector3.one * scale);
                float warn = Mathf.Clamp01(age / sw.fuse), flash = sw.flyAt >= 0f ? Mathf.Clamp01(1f - (Clock - sw.flyAt) / 0.2f) : 0f;
                bool show = sw.flyAt < 0f || Clock - sw.flyAt < 0.2f;
                for (int i = 0; i < squares.Count; i++)
                {
                    var t = squares[i];
                    if (!show) { t.Hide(); continue; }
                    float lit = Mathf.Clamp01(warn * count - i * 0.6f);
                    t.Floor(spots[i], way, Vector2.one * 0.42f);
                    t.shape = 0f;
                    t.fill = Kit.A(flash > 0f ? Color.white : CmGold.main, 0.25f + 0.6f * lit);
                    t.core = Kit.A(Color.white, 1f);
                    t.coreWidth = 0.02f;
                    t.ink = Kit.A(CmGold.ink, 0.9f);
                    t.inkWidth = 0.04f;
                    t.rim = Color.clear;
                    t.stripe = Color.clear;
                    t.arc = 1f;
                    t.fade = 1f;
                    t.Apply();
                }
                return true;
            });
            f.props.Add(blade);
            f.props.Add(hilt);
            f.tiles.AddRange(squares);
            f.end = () => cmSwords.Remove(sw);
        }

        void CmSwordFly(SkillFxEvent e)
        {
            foreach (var sw in cmSwords)
                if (sw.by == e.by && sw.id == e.count && sw.flyAt < 0f)
                {
                    sw.flyAt = Clock;
                    sw.at = e.at;
                    sw.pos = e.at;
                    sw.dir = e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : sw.dir;
                    sw.to = e.normal;
                    sw.speed = Mathf.Max(1f, e.size);
                }
        }

        void CmSwordEnd(SkillFxEvent e)
        {
            foreach (var sw in cmSwords)
                if (sw.by == e.by && sw.id == e.count && sw.endAt < 0f)
                {
                    sw.endAt = Clock;
                    sw.hit = e.target != null;
                    if (!sw.hit)
                    {
                        // A miss: it sticks a little way into what stopped it, wobbles, and is gone in a poof.
                        Vector3 stuck = sw.pos;
                        kit?.Run(0.5f, (fx, d) =>
                        {
                            if (fx.age >= 0.45f)
                            {
                                CmPoof(stuck + sw.dir * 0.6f, CmGold, 5, 0.5f, 0.28f, 0.4f, 0.5f);
                                return false;
                            }
                            return true;
                        });
                    }
                }
            if (e.target != null)
            {
                CmSmack(e.target, e.at, e.dir, CmGold, 1f);
                CmPoof(e.at, CmGold, 5, 0.6f, 0.28f, 0.4f, 0.5f);
            }
            else CmFlash(e.at, 0.3f, 6, CmGold);
        }

        void CmBound(SkillFxEvent e)
        {
            if (kit == null || e.target == null) return;
            var who = e.target;
            float seconds = Mathf.Max(0.3f, e.size);
            var r1 = CmProp(kit.meshTorus, CmGold, "Bind ring", 0.012f);
            var r2 = CmProp(kit.meshTorus, CmGold, "Bind ring", 0.012f);
            var f = kit.Run(seconds, (fx, d) =>
            {
                if (who == null) return false;
                Vector3 hips = who.Hips.position;
                float snap = 1.5f - 0.5f * Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.15f));
                float end = Mathf.Clamp01((seconds - fx.age) / 0.12f);
                CmPlace(r1, hips + Vector3.up * 0.18f, Quaternion.Euler(6f, fx.age * 90f, 0f), Vector3.one * (0.3f * snap * end));
                CmPlace(r2, hips - Vector3.up * 0.08f, Quaternion.Euler(-6f, -fx.age * 90f, 0f), Vector3.one * (0.3f * snap * end));
                return true;
            });
            f.props.Add(r1);
            f.props.Add(r2);
            f.end = () =>
            {
                if (who != null) CmPoof(who.Hips.position, CmGold, 5, 0.6f, 0.25f, 0.35f, 0.4f);
            };
        }

        // ---------------------------------------------------------------- the bishop's stone and holy water

        void CmStoneShot(SkillFxEvent e)
        {
            if (kit == null) return;
            var st = new CmStone { by = e.by, id = e.count, pos = e.at, vel = (e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : Vector3.forward) * e.size };
            cmStones.Add(st);
            var prop = CmProp(CmRockMesh(), CmRock, "Stone", 0.02f);
            var s = Params;
            float size = s != null ? s.stoneSize : 1.2f, drop = s != null ? s.stoneDrop : 1.5f, range = s != null ? s.stoneRange : 25f;
            float nextDust = 0f;
            Quaternion rot = Random.rotation;
            bool grounded = false;
            var f = kit.Run(8f, (fx, d) =>
            {
                if (st.done || st.flown > range + 1f) return false;
                st.vel += Vector3.down * drop * d;
                Vector3 step = st.vel * d;
                st.pos += step;
                Vector3 probe = st.pos + Vector3.up * size, ground = Ground(probe);
                bool found = (ground - probe).sqrMagnitude > 1e-6f, was = grounded;
                grounded = false;
                if (found && st.pos.y - size * 0.5f < ground.y)
                {
                    st.pos.y = ground.y + size * 0.5f;
                    st.vel.y = 0f;
                    grounded = true;
                }
                if (grounded && !was)
                {
                    CmPoof(ground + Vector3.up * 0.1f, CmDust, 6, 0.9f, 0.38f, 0.45f, 0.25f);
                    Shake(0.03f, 6f * Frame);
                }
                st.flown += step.magnitude;
                Vector3 way = FlatDir(st.vel), axis = Vector3.Cross(Vector3.up, way);
                rot = Quaternion.AngleAxis(step.magnitude / Mathf.Max(0.2f, size * 0.5f) * Mathf.Rad2Deg, axis) * rot;
                CmPlace(prop, st.pos, rot, Vector3.one * size);
                if (grounded && fx.age >= nextDust)
                {
                    nextDust = fx.age + 0.07f;
                    CmPoof(ground + Vector3.up * 0.1f - way * (size * 0.45f), CmDust, 2, 0.5f, 0.32f, 0.4f, 0.3f, -way * 0.5f);
                }
                return true;
            });
            f.props.Add(prop);
            f.end = () => cmStones.Remove(st);
        }

        void CmStoneBreak(SkillFxEvent e)
        {
            foreach (var st in cmStones) if (st.by == e.by && st.id == e.count) st.done = true;
            CmPoof(e.at, CmRock, 8, 1.1f, 0.5f, 0.55f, 0.6f);
            CmPoof(Ground(e.at) + Vector3.up * 0.1f, CmDust, 8, 1.4f, 0.45f);
            Shake(0.06f, 8f * Frame);
        }

        void CmFlaskThrow(SkillFxEvent e)
        {
            if (kit == null) return;
            Vector3 from = e.at, to = e.normal;
            float time = Mathf.Max(0.1f, e.size);
            var body = CmProp(CmFlaskMesh(), CmWater, "Holy water", 0.012f);
            var cork = CmProp(CmCrossMesh(), CmGold, "Holy water cork", 0.01f);
            Vector3 axis = Vector3.Cross(Vector3.up, FlatDir(to - from));
            var f = kit.Run(time, (fx, d) =>
            {
                float t = Mathf.Clamp01(fx.age / time);
                Vector3 p = Vector3.Lerp(from, to, t) + Vector3.up * (4f * 1.4f * t * (1f - t));
                Quaternion rot = Quaternion.AngleAxis(fx.age * 760f, axis);
                const float S = 1.4f;
                Vector3 foot = p - rot * (Vector3.up * (0.24f * S));   // turning round its middle
                CmPlace(body, foot, rot, Vector3.one * S);
                CmPlace(cork, foot, rot, Vector3.one * S);
                return true;
            });
            f.props.Add(body);
            f.props.Add(cork);
        }

        void CmPuddle(SkillFxEvent e)
        {
            if (kit == null) return;
            Vector3 at = Ground(e.at) + Vector3.up * 0.012f;
            float radius = Mathf.Max(0.3f, e.size), seconds = Params != null ? Params.waterSeconds : 4f;
            // The bottle breaks: a splash of drops and a pop of water.
            kit.Drops(at + Vector3.up * 0.3f, Vector3.up, CmWater, 6, 6f);
            CmFlash(at + Vector3.up * 0.3f, 0.55f, 8, CmWater);
            CmPoof(at + Vector3.up * 0.1f, CmWater, 7, radius * 0.75f, 0.36f, 0.42f, 0.5f);
            var pool = CmProp(CmPuddleMesh(), CmWater, "Holy puddle", 0.006f);
            float nextRipple = 0.25f, nextGlint = 0.12f, spin = Random.Range(0f, 360f);
            var f = kit.Run(seconds + 0.4f, (fx, d) =>
            {
                float grow = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.25f)), shrink = Mathf.Clamp01((fx.age - seconds) / 0.4f);
                float s = radius * 2f * grow * (1f - shrink);
                if (s < 0.02f)
                {
                    pool.Show(false);
                    return true;
                }
                CmPlace(pool, at, Quaternion.Euler(0f, spin, 0f), new Vector3(s, 1f, s));
                if (shrink <= 0f && fx.age >= nextRipple)
                {
                    nextRipple = fx.age + 0.7f;
                    kit.FlashRing(at + Vector3.up * 0.03f, radius * 0.7f, CmWhite);
                }
                if (shrink <= 0f && fx.age >= nextGlint)
                {
                    nextGlint = fx.age + 0.22f;
                    Vector2 r = Random.insideUnitCircle * radius * 0.75f;
                    CmFlash(at + new Vector3(r.x, 0.06f, r.y), 0.14f, 10);
                }
                return true;
            });
            f.props.Add(pool);
        }

        void CmSlip(SkillFxEvent e)
        {
            kit?.Drops(e.at + Vector3.up * 0.2f, Vector3.up + FlatDir(e.dir) * 0.5f, CmWater, 5, 6f);
            CmPoof(e.at + Vector3.up * 0.1f, CmWater, 3, 0.5f, 0.26f, 0.35f, 0.4f);
            if (e.target != null)
            {
                CmStreak(e.target, CmWater, 0.5f, 0.14f, 0.15f);
                kit?.FlashBody(e.target, CmWater.main);
            }
        }

        // ---------------------------------------------------------------- every frame (UltStep)

        void SetThreeStepFx(Camera cam)
        {
            if (!s3Built) return;
            S3RollsStep(Time.deltaTime);
        }

        // ---------------------------------------------------------------- the rook's column and tower (its own piece baked)

        /// <summary>Write each bake's numbers to the log (to find why a baked rook did not show).</summary>
        public static bool LogBakes;

        /// <summary>The piece's skin baked as it stands, centred and turned back by its facing (as the driven roll's).</summary>
        static bool S3Bake(RagdollPawn p, out Mesh baked, out Matrix4x4 canon, out Vector3 size, out Material[] mats)
        {
            baked = null;
            canon = Matrix4x4.identity;
            size = Vector3.one;
            mats = null;
            var skin = p != null ? p.skin : null;
            if (skin == null) return false;
            baked = new Mesh { name = "S3 piece", hideFlags = HideFlags.HideAndDontSave };
            BakeSkin(skin, baked);
            baked.RecalculateBounds();
            Bounds b = baked.bounds;
            Quaternion skinRot = skin.transform.rotation;
            Vector3 skinPos = skin.transform.position, worldCenter = skinPos + skinRot * b.center;
            Vector3 face = new Vector3(p.Facing.x, 0f, p.Facing.z);
            Quaternion facing = Quaternion.LookRotation(face.sqrMagnitude > 1e-4f ? face.normalized : Vector3.forward, Vector3.up);
            canon = Matrix4x4.Rotate(Quaternion.Inverse(facing)) * Matrix4x4.Translate(skinPos - worldCenter) * Matrix4x4.Rotate(skinRot);
            Vector3 lo = Vector3.one * float.MaxValue, hi = Vector3.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i / 2 % 2 == 0 ? b.min.y : b.max.y, i / 4 == 0 ? b.min.z : b.max.z);
                Vector3 c = canon.MultiplyPoint3x4(corner);
                lo = Vector3.Min(lo, c);
                hi = Vector3.Max(hi, c);
            }
            size = Vector3.Max(hi - lo, Vector3.one * 0.05f);
            mats = skin.sharedMaterials;
            if (LogBakes)
                Debug.Log($"[S3 bake] {p.name}: {baked.vertexCount} verts, {baked.subMeshCount} parts, bounds {b.center}/{b.size}, size {size}, skin scale {skin.transform.lossyScale}, root bone {(skin.rootBone != null ? skin.rootBone.name : "-")}");
            return true;
        }

        /// <summary>The piece baked as <see cref="S3Bake"/>, with its turn put into the vertices and every part in one, for a
        /// kit prop (one material).</summary>
        static Mesh S3BakeMerged(RagdollPawn p, out Vector3 size)
        {
            if (!S3Bake(p, out var baked, out var canon, out size, out _)) return null;
            var v = baked.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = canon.MultiplyPoint3x4(v[i]);
            var all = new List<int>();
            for (int s = 0; s < baked.subMeshCount; s++) all.AddRange(baked.GetTriangles(s));
            baked.subMeshCount = 1;
            baked.vertices = v;
            baked.SetTriangles(all, 0);
            baked.RecalculateNormals();
            baked.RecalculateBounds();
            return baked;
        }

        void S3BakeRoll(RagdollPawn rook)
        {
            if (s3Rolls.TryGetValue(rook, out var old) && old.baked != null) Destroy(old.baked);
            if (!S3Bake(rook, out var baked, out var canon, out var size, out var mats)) return;
            s3Rolls[rook] = new S3Look { by = rook, baked = baked, canon = canon, size = size, mats = mats, born = Clock };
        }

        void S3RollsStep(float dt)
        {
            var s = Params;
            s3RollsGone.Clear();
            foreach (var kv in s3Rolls)
            {
                var rook = kv.Key;
                var look = kv.Value;
                if (rook == null || !rook.RollingShort || look.baked == null || s == null)
                {
                    s3RollsGone.Add(rook);
                    continue;
                }
                var skin = rook.skin;
                if (skin != null) skin.forceRenderingOff = true;
                Vector3 center = rook.Hips.position, dir = FlatDir(rook.PlanDir);
                float speed = Mathf.Lerp(s.rollSpeed0, s.rollSpeed1, Mathf.Clamp01(rook.PlanTime / Mathf.Max(0.1f, s.rollSeconds)));
                look.angle += speed / Mathf.Max(0.2f, s.rollRadius) * Mathf.Rad2Deg * dt;
                // Laid on its side across the way it rolls (its height along the column), turning about that line.
                Quaternion turn = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.AngleAxis(look.angle, Vector3.right) * Quaternion.Euler(0f, 0f, -90f);
                float width = 2f * s.rollRadius / Mathf.Max(0.1f, Mathf.Max(look.size.x, look.size.z)), length = s.rollLength / Mathf.Max(0.1f, look.size.y);
                Matrix4x4 m = Matrix4x4.TRS(center, turn, Vector3.one) * Matrix4x4.Scale(new Vector3(width, length, width)) * look.canon;
                S3Draw(look, m, new Bounds(center, Vector3.one * (s.rollLength + 2f)), skin);
            }
            foreach (var rook in s3RollsGone)
            {
                if (rook != null && rook.skin != null) rook.skin.forceRenderingOff = false;
                if (s3Rolls.TryGetValue(rook, out var look) && look.baked != null) Destroy(look.baked);
                s3Rolls.Remove(rook);
            }
        }

        void S3Draw(S3Look look, Matrix4x4 m, Bounds bounds, SkinnedMeshRenderer skin)
        {
            if (look.mats == null || look.mats.Length == 0) return;
            for (int i = 0; i < look.baked.subMeshCount; i++)
            {
                var mat = look.mats[Mathf.Min(i, look.mats.Length - 1)];
                if (mat == null) continue;
                var rp = new RenderParams(mat)
                {
                    shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    receiveShadows = true,
                    layer = skin != null ? skin.gameObject.layer : 0,
                    renderingLayerMask = skin != null ? skin.renderingLayerMask : 1u,
                    worldBounds = bounds,
                };
                Graphics.RenderMesh(rp, look.baked, i, m);
            }
        }

        void DestroySetThree()
        {
            foreach (var kv in s3Rolls)
            {
                if (kv.Key != null && kv.Key.skin != null) kv.Key.skin.forceRenderingOff = false;
                if (kv.Value.baked != null) Destroy(kv.Value.baked);
            }
            s3Rolls.Clear();
            cmBolas.Clear();
            cmSwords.Clear();
            cmStones.Clear();
            cmCracks.Clear();
            cmTowers.Clear();
            cmStars.Clear();
            DestroyComic();
        }
    }
}
