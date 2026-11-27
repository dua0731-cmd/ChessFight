using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The user's own skills on every screen (USER_SKILLS 5장), bundle 2: the bishop's three and 멈춤's look (5.4). Not
    /// seen in Unity yet.
    ///
    ///   성검 던지기  gold gathering in the hand into a big holy sword; thrown, a windmill on its arc with a gold disc of
    ///              its turning and a red cell where it will come down; hit: a beam from the sky, then the cross slams down,
    ///              squashes and wobbles like jelly, dust and gold cracks, two light chains to the ankles; teammates see it
    ///              half clear; blinking its last second and blowing away into gold dust from the top; a miss shivers in
    ///              the floor and is gone
    ///   케틀 궁     an orb growing on the staff, a red dotted line to the leader and a target turning over its head, beeps
    ///              quickening; the shot kicks the bishop back; a gold comet homing on, a gold ripple where it passes a
    ///              wall; the target jumping to whoever takes it ("퍽"), or bursting on the leader ("쾅!") who spins away on a
    ///              gold trail; a puff blown off the staff after
    ///   핀!         a white point on the staff; the beam out in 0.1 s (white core, gold glow) over a row of diamond cells;
    ///              "찰칵!" and the shutter; a light thread between two caught on one line
    ///   멈춤        grey with a thick white edge, a big ⏸ over the head with its time running round, turning ▶ and blinking
    ///              the last half second; dust and sweat hanging in the air with it; "째깍" each half second; colour and
    ///              all back with "뿅"
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly Kit.Palette CmHoly = new Kit.Palette("#FFF8E8", "#FFFFFF", "#D9D2C2", "#B8901C");

        Material jyGrey, jyOutline;

        Mesh jyPauseBar, jyPlay;

        void SetFourJYBFx(SkillFxEvent e, float lvl)
        {
            switch (e.kind)
            {
                case SkillFxKind.JySword: JySwordFx(e, lvl); break;
                case SkillFxKind.JyBow: JyBowFx(e, lvl); break;
                case SkillFxKind.JyBeam: JyBeamFx(e, lvl); break;
                default: SetFourJYCFx(e, lvl); break;   // the rook's (PawnRushSkillFx.SetFourJYC.cs)
            }
        }

        // ---------------------------------------------------------------- 성검 던지기

        sealed class JySwordLook { public int phase; public float at; public Vector3 from, v0; }
        readonly Dictionary<RagdollPawn, JySwordLook> jySwords = new Dictionary<RagdollPawn, JySwordLook>();

        void JySwordFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            switch (e.count)
            {
                case 0:
                {
                    if (by == null) return;
                    var look = new JySwordLook { phase = 0, at = Clock };
                    jySwords[by] = look;
                    float summon = Mathf.Max(0.1f, e.size);
                    UltClip(UltClipId.Shimmer, 0.7f * lvl, 1.3f);   // ("샤랑")
                    JySwordFlight(by, look, summon, lvl);
                    break;
                }
                case 1:
                    if (by == null || !jySwords.TryGetValue(by, out var l)) return;
                    l.phase = 1;
                    l.at = Clock;
                    l.from = e.at;
                    l.v0 = FlatDirOr(e.dir) * Mathf.Max(1f, e.size);
                    kit.AddSquash(by, Kit.SquashKind.Spring);
                    break;
                case 2:
                    if (by != null && jySwords.TryGetValue(by, out var l2)) l2.phase = 2;
                    JyCrossFx(e, lvl);
                    break;
                case 3:
                {
                    if (by != null && jySwords.TryGetValue(by, out var l3)) l3.phase = 2;
                    // A miss: stuck in the floor at a slant, shivering, then gold dust.
                    var blade = CmProp(CmBladeMesh(), CmHoly, "Missed sword", 0.016f);
                    var hilt = CmProp(CmHiltMesh(), CmGold, "Missed sword hilt", 0.014f);
                    Vector3 at = Ground(e.at + Vector3.up * 0.5f), way = FlatDirOr(e.dir);
                    UltClip(UltClipId.Tick, 0.6f * lvl, 0.9f);   // ("팅—")
                    var f = kit.Run(0.75f, (fx, d) =>
                    {
                        float shiver = fx.age < 0.4f ? 6f * Mathf.Sin(fx.age * 70f) * (1f - fx.age / 0.4f) : 0f;
                        float outk = Mathf.Clamp01((fx.age - 0.45f) / 0.3f);
                        Quaternion r = Quaternion.LookRotation(way, Vector3.up) * Quaternion.Euler(150f + shiver, 0f, 0f);
                        Vector3 grip = at + r * Vector3.up * -0.35f;
                        CmPlace(blade, grip, r, new Vector3(1.8f, 1.1f, 1.1f) * (1f - outk));
                        CmPlace(hilt, grip, r, Vector3.one * 1.1f * (1f - outk));
                        if (outk > 0f && Random.value < 0.4f) CmPoof(grip, CmGold, 1, 0.3f, 0.12f, 0.4f, 0.4f);
                        return true;
                    });
                    f.props.Add(blade);
                    f.props.Add(hilt);
                    break;
                }
            }
        }

        /// <summary>The sword from the hand: gathered in gold (the summon), carried, then thrown - a windmill on its arc with
        /// the gold disc of its turning, a red cell where it will come down - until it hits or misses.</summary>
        void JySwordFlight(RagdollPawn by, JySwordLook look, float summon, float lvl)
        {
            var blade = CmProp(CmBladeMesh(), CmHoly, "Holy sword", 0.018f);
            var hilt = CmProp(CmHiltMesh(), CmGold, "Holy sword hilt", 0.016f);
            var disc = new Kit.Strip(kit, CmGold) { core = CmGold.light, coreShare = 0.45f, inkShare = 0.15f };
            var discFill = new Kit.Tile(kit, "Sword disc");   // (STAGING_REVIEW 3.1: a solid gold disc, 1.4 m)
            var ring = new List<Vector3>(25);
            var cell = new Kit.Tile(kit, "Sword landing");
            float lastWhoosh = -1f;
            var f = kit.Run(4f, (fx, d) =>
            {
                if (by == null || look.phase >= 2) return false;
                if (look.phase == 0)
                {
                    // Gathered in gold at the hand, then held up.
                    float k = Mathf.Clamp01((Clock - look.at) / summon);
                    Vector3 hand = by.bodies[(int)BodyId.HandR].position;
                    Quaternion r = Quaternion.LookRotation(FlatDir(by.Facing), Vector3.up) * Quaternion.Euler(-20f, 0f, 0f);
                    CmPlace(blade, hand, r, new Vector3(1.8f, 1.1f, 1.1f) * Kit.EaseOutBack(k));
                    CmPlace(hilt, hand, r, Vector3.one * (1.1f * Kit.EaseOutBack(k)));
                    disc.Hide();
                    if (k < 1f && Random.value < 0.5f) CmPoof(hand + Random.onUnitSphere * 0.5f, CmGold, 1, 0.1f, 0.08f, 0.25f, -0.2f);
                    cell.fade = 0f;
                    cell.Apply();
                    return true;
                }
                // Thrown: on its arc, turning like a windmill about the way across it.
                float t = Clock - look.at, g = Mathf.Max(0.01f, -Physics.gravity.y);
                Vector3 pos = look.from + look.v0 * t + Vector3.down * (0.5f * g * t * t);
                Vector3 way = FlatDirOr(Flat(look.v0)), side = Vector3.Cross(Vector3.up, way);
                Quaternion spin = Quaternion.AngleAxis(t * 1100f, side) * Quaternion.LookRotation(way, Vector3.up);
                Vector3 mid = pos;
                CmPlace(blade, mid - spin * Vector3.up * 0.45f, spin, new Vector3(1.8f, 1.1f, 1.1f));
                CmPlace(hilt, mid - spin * Vector3.up * 0.45f, spin, Vector3.one * 1.1f);
                // The gold disc of its turning: a ring 1.4 m across in the plane it turns in.
                ring.Clear();
                for (int i = 0; i <= 24; i++)
                {
                    float a = i * Mathf.PI * 2f / 24f;
                    ring.Add(mid + (way * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * 0.7f);
                }
                disc.Build(ring, i => 0.08f, kit.Eye);
                disc.fade = 0.9f;
                disc.Apply();
                discFill.Facing(mid, side, Vector2.one * 1.4f, Vector3.up);
                discFill.shape = 1f;
                discFill.inner = 0f;
                discFill.fill = Kit.A(CmGold.light, 0.55f);
                discFill.ink = Kit.A(CmGold.ink, 0.8f);
                discFill.inkWidth = 0.04f;
                discFill.core = Color.clear;
                discFill.rim = Color.clear;
                discFill.stripe = Color.clear;
                discFill.fade = 1f;
                discFill.Apply();
                if (Clock - lastWhoosh > 0.16f)
                {
                    lastWhoosh = Clock;
                    UltClip(UltClipId.Whoosh, 0.35f * S3Heard(pos), 1.5f);   // ("휭, 휭, 휭")
                }
                // Where it will come down (flat ground at the thrower's feet): a red cell with flowing stripes.
                float h = look.from.y - by.FeetPoint.y, vy = look.v0.y;
                float tLand = (vy + Mathf.Sqrt(Mathf.Max(0f, vy * vy + 2f * g * h))) / g;
                Vector3 land = Ground(look.from + Flat(look.v0) * tLand + Vector3.up * 1.5f) + Vector3.up * 0.03f;
                cell.Floor(land, way, Vector2.one * 1.3f);
                cell.shape = 0f;
                cell.fill = Kit.A(CmRed.main, 0.5f);
                cell.ink = Kit.A(CmRed.ink, 0.9f);
                cell.inkWidth = 0.04f;
                cell.stripe = Kit.A(Color.white, 0.3f);
                cell.stripePhase = fx.age * 2f;
                cell.core = Color.clear;
                cell.rim = Color.clear;
                cell.fade = 1f;
                cell.Apply();
                return t < 3f;
            });
            f.props.Add(blade);
            f.props.Add(hilt);
            f.strips.Add(disc);
            f.tiles.Add(cell);
            f.tiles.Add(discFill);
        }

        /// <summary>The cross: a beam from the sky, then down it slams (white stone, gold trim, 3 m), squashing 30% and
        /// wobbling like jelly; dust and gold cracks; light chains to the ankles of the one bound; half clear to the thrower's
        /// teammates; its last second blinking, then blown away into gold dust from the top.</summary>
        void JyCrossFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            var who = e.target;
            float seconds = Mathf.Max(0.5f, e.size), high = Params != null ? Params.jyCrossHigh : 3f, wide = Params != null ? Params.jyCrossWide : 3f;
            float bind = Params != null ? Params.jyCrossBind : 2f;
            Vector3 foot = e.at, way = FlatDirOr(e.dir), side = Vector3.Cross(Vector3.up, way);
            Quaternion face = Quaternion.LookRotation(way, Vector3.up);
            // (STAGING_REVIEW 3.1: white stone edged in gold, deep enough to see it is a block)
            var pillar = CmProp(kit.RoundBox(new Vector3(0.55f, high, 0.62f), 0.07f), CmCrossStone, "Cross pillar", 0.045f);
            var bar = CmProp(kit.RoundBox(new Vector3(wide, 0.55f, 0.62f), 0.07f), CmCrossStone, "Cross bar", 0.045f);
            var trim = CmProp(kit.RoundBox(new Vector3(0.58f, 0.12f, 0.58f), 0.03f), CmGold, "Cross trim", 0.016f);
            var beam = new Kit.Strip(kit, CmGold) { core = Color.white, coreShare = 0.5f, inkShare = 0.1f };
            var chainL = new Kit.Strip(kit, CmGold) { core = CmGold.light, coreShare = 0.4f, inkShare = 0.25f };
            var chainR = new Kit.Strip(kit, CmGold) { core = CmGold.light, coreShare = 0.4f, inkShare = 0.25f };
            var pts = new List<Vector3>(8);
            bool landed = false;
            float landAt = 0f;
            bool ally = Viewer != null && by != null && Viewer.Team == by.Team && by.Team != Teams.None;
            UltClip(UltClipId.Riser, 0.5f * lvl, 1.6f);
            var f = kit.Run(seconds + 0.6f, (fx, d) =>
            {
                float t = fx.age;
                // The beam from the sky (0.2 s), then the cross slams down.
                if (t < 0.35f)
                {
                    pts.Clear();
                    pts.Add(foot + Vector3.up * 14f);
                    pts.Add(foot + Vector3.up * 0.1f);
                    beam.Build(pts, i => 0.35f * (1f - Mathf.Clamp01((t - 0.2f) / 0.15f)), kit.Eye);
                    beam.Apply();
                }
                else beam.Hide();
                float drop = Mathf.Clamp01((t - 0.2f) / 0.12f);
                if (drop < 1f && t >= 0.2f) { }
                if (!landed && drop >= 1f)
                {
                    landed = true;
                    landAt = Clock;
                    JyHit(null, foot + Vector3.up * 0.3f, 3, CmDust);
                    kit.DustRing(foot + Vector3.up * 0.05f, 14, 2.2f, CmDust, 0.4f, 0.55f);
                    JyWord("쿠웅!", foot + Vector3.up * 1.5f, JyWordGold);
                    UltClip(UltClipId.SubDrop, lvl, 0.7f);
                    UltClip(UltClipId.Castle, 0.7f * lvl, 0.55f);   // (a low bell, the nearest)
                    UltClip(UltClipId.Capture, 0.5f * lvl, 1.2f, 0.2f);   // ("철컥": the chains)
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f + Random.Range(-15f, 15f);
                        kit.FlashRing(foot + Quaternion.AngleAxis(a, Vector3.up) * way * 0.9f + Vector3.up * 0.03f, 0.35f, CmGold);
                    }
                }
                float since = landed ? Clock - landAt : 0f;
                float jelly = landed ? 1f - 0.3f * Mathf.Exp(-since * 6f) * Mathf.Cos(since * 22f) : 1f;
                float gone = Mathf.Clamp01((t - seconds) / 0.4f);
                bool blink = t > seconds - 1f && t < seconds && Mathf.Repeat((t - (seconds - 1f)) * 3f, 1f) > 0.7f;
                float fall = (1f - drop) * 1.5f;
                float sy = jelly * (1f - 0.85f * gone);
                float sx = landed ? 1f + (1f - jelly) * 0.5f : 1f;
                Vector3 baseAt = foot + Vector3.up * fall;
                CmPlace(pillar, baseAt + Vector3.up * (high * 0.5f * sy), face, new Vector3(sx, Mathf.Max(0.02f, sy), sx));
                CmPlace(bar, baseAt + Vector3.up * ((high - 0.8f) * sy), face, new Vector3(sx, Mathf.Max(0.02f, sy), 1f));
                CmPlace(trim, baseAt + Vector3.up * 0.06f, face, new Vector3(sx, 1f, sx) * (1f - gone));
                float fadeTo = ally ? 0.7f : 1f;
                pillar.fade = bar.fade = trim.fade = t < 0.2f ? 0f : fadeTo;
                if (blink)
                {
                    pillar.fade = bar.fade = 0.15f;
                }
                if (gone > 0f)
                {
                    // Blown away into gold dust from the top down (never falling over).
                    pillar.fade = bar.fade = trim.fade = (ally ? 0.7f : 1f) * (1f - gone);
                    for (int k = 0; k < 2; k++)
                        CmPoof(baseAt + Vector3.up * (high * (1f - gone) * Random.Range(0.6f, 1f)) + side * Random.Range(-wide * 0.45f, wide * 0.45f), CmGold, 1, 0.35f, 0.16f, 0.5f, 0.8f);
                }
                // The light chains to the ankles while it is bound.
                if (who != null && landed && since < bind)
                {
                    for (int c = 0; c < 2; c++)
                    {
                        var strip = c == 0 ? chainL : chainR;
                        Vector3 ankle = who.bodies[(int)(c == 0 ? BodyId.FootL : BodyId.FootR)].position;
                        Vector3 from = foot + side * (c == 0 ? -0.2f : 0.2f) + Vector3.up * 0.2f;
                        pts.Clear();
                        for (int i = 0; i <= 6; i++)
                        {
                            float u = i / 6f;
                            pts.Add(Vector3.Lerp(from, ankle, u) + Vector3.up * (0.12f * Mathf.Sin(u * Mathf.PI)));
                        }
                        strip.Build(pts, i => 0.07f, kit.Eye);
                        strip.Apply();
                    }
                }
                else
                {
                    chainL.Hide();
                    chainR.Hide();
                }
                return gone < 1f;
            });
            f.props.Add(pillar);
            f.props.Add(bar);
            f.props.Add(trim);
            f.strips.Add(beam);
            f.strips.Add(chainL);
            f.strips.Add(chainR);
            f.end = () => UltClip(UltClipId.Shimmer, 0.4f * S3Heard(foot), 1.6f);   // ("샤라락")
        }

        // ---------------------------------------------------------------- 케틀 궁

        /// <summary>The cross's stone: white, edged in gold (STAGING_REVIEW 3.1).</summary>
        static readonly Kit.Palette CmCrossStone = new Kit.Palette("#F6F1E6", "#FFFFFF", "#D8CDB6", "#B8862A");

        sealed class JyBowLook { public int phase; public float at; public Vector3 head; public RagdollPawn target, aimed; public float speed; }
        readonly Dictionary<RagdollPawn, JyBowLook> jyBows = new Dictionary<RagdollPawn, JyBowLook>();

        void JyBowFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            switch (e.count)
            {
                case 0:
                {
                    if (by == null) return;
                    var look = new JyBowLook { phase = 0, at = Clock, target = e.target, aimed = e.target };
                    jyBows[by] = look;
                    JyBowAimFx(by, look, Mathf.Max(0.2f, e.size), lvl);
                    break;
                }
                case 1:
                    if (by == null || !jyBows.TryGetValue(by, out var l)) return;
                    l.phase = 1;
                    l.at = Clock;
                    l.head = e.at;
                    l.speed = Mathf.Max(5f, e.size);
                    kit.AddSquash(by, Kit.SquashKind.Bump);
                    CmFlash(e.at, 0.6f, 6, CmGold);
                    UltClip(UltClipId.Whoosh, lvl, 2.1f);   // ("뿅—!")
                    break;
                case 2:
                {
                    if (by != null && jyBows.TryGetValue(by, out var l2))
                    {
                        l2.phase = 2;
                        l2.aimed = e.target;
                    }
                    if (by != null && jyBows.TryGetValue(by, out var taken)) taken.at = Clock;   // (the target jumps onto the taker)
                    JyHit(e.target, e.at, 2, CmGold);
                    JyWord("퍽!", e.at, JyWordWood, e.target);
                    UltClip(UltClipId.Tap, 0.8f * lvl, 0.8f);
                    break;
                }
                case 3:
                {
                    if (by != null && jyBows.TryGetValue(by, out var l3)) l3.phase = 2;
                    // The leader: the target bursts, it spins away on a gold trail.
                    CmPoof(e.at, CmGold, 6, 1f, 0.36f, 0.45f, 0.3f);
                    JyHit(e.target, e.at, 3, CmGold);
                    JyWord("쾅!", e.at, JyWordRed, e.target);
                    UltClip(UltClipId.Crack, lvl, 0.6f);
                    UltClip(UltClipId.SubDrop, 0.7f * lvl, 0.9f);
                    if (e.target != null)
                    {
                        CmStreak(e.target, CmGold, 1.1f, 0.3f, 0.25f);
                        var who = e.target;
                        kit.Run(0.9f, (fx, d) =>
                        {
                            if (who == null) return false;
                            WantPose(who, Quaternion.AngleAxis(fx.age * 900f, Vector3.up), true);
                            return true;
                        });
                    }
                    break;
                }
                case 4:
                {
                    // The smoke blown off the staff: "후".
                    if (by == null) return;
                    if (jyBows.TryGetValue(by, out var l4)) l4.phase = 3;
                    var who = by;
                    kit.Run(0.6f, (fx, d) =>
                    {
                        if (who == null) return false;
                        if (Random.value < 0.35f) CmPoof(who.bodies[(int)BodyId.HandR].position + FlatDir(who.Facing) * 0.35f + Vector3.up * 0.2f, CmSteam, 1, 0.12f, 0.12f, 0.5f, 0.5f);
                        return true;
                    });
                    UltClip(UltClipId.Wind, 0.4f * lvl, 1.4f, 0.15f);
                    break;
                }
            }
        }

        /// <summary>The aim and the shot: the orb growing on the staff, the red dotted line, the target turning over the
        /// leader's head, beeps quickening; then the gold comet homing on, rippling where it passes a wall.</summary>
        void JyBowAimFx(RagdollPawn by, JyBowLook look, float aim, float lvl)
        {
            var orb = CmProp(kit.meshBall, CmGold, "Bow orb", 0.014f);
            var comet = CmProp(kit.meshBall, CmGold, "Bow comet", 0.016f);
            var line = new Kit.Strip(kit, CmRed) { core = CmRed.light, coreShare = 0.4f, inkShare = 0.2f };
            var tail = new Kit.Strip(kit, CmGold) { core = Color.white, coreShare = 0.5f, inkShare = 0.15f };
            var ringA = new Kit.Strip(kit, CmRed) { core = Color.white, coreShare = 0.25f, inkShare = 0.3f };
            var ringB = new Kit.Strip(kit, CmRed) { core = Color.white, coreShare = 0.25f, inkShare = 0.3f };
            var board = new Kit.Tile[3];
            for (int i = 0; i < board.Length; i++) board[i] = new Kit.Tile(kit, "Bow target " + i);
            var pts = new List<Vector3>(32);
            var trail = new List<Vector3>(12);
            var dots = new Kit.Prop[8];
            for (int i = 0; i < dots.Length; i++) dots[i] = CmProp(kit.meshBall, CmRed, "Aim dot", 0.008f);
            float nextBeep = 0f;
            Vector3 lastHead = Vector3.zero;
            var f = kit.Run(8f, (fx, d) =>
            {
                if (by == null || look.phase >= 3) return false;
                var target = look.aimed;
                Vector3 staff = by.bodies[(int)BodyId.HandR].position + FlatDir(by.Facing) * 0.35f + Vector3.up * 0.25f;
                if (look.phase == 0)
                {
                    float k = Mathf.Clamp01((Clock - look.at) / aim);
                    CmPlace(orb, staff, Quaternion.identity, Vector3.one * Mathf.Lerp(0.2f, 0.6f, k));
                    if (target != null)
                    {
                        // The red line of light (5 cm), dots flowing along it toward the leader.
                        Vector3 to = target.bodies[(int)BodyId.Head].position + Vector3.up * 0.2f;
                        pts.Clear();
                        pts.Add(staff);
                        pts.Add(to);
                        line.Build(pts, i => 0.08f, kit.Eye);   // (STAGING_REVIEW 공1: everyone must see it)
                        line.fade = 0.9f;
                        line.Apply();
                        for (int i = 0; i < dots.Length; i++)
                        {
                            float u = Mathf.Repeat(fx.age * 0.8f + i / (float)dots.Length, 1f);
                            CmPlace(dots[i], Vector3.Lerp(staff, to, u), Quaternion.identity, Vector3.one * 0.12f);
                        }
                        JyTarget(board, target, fx.age, Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(fx.age)));
                        ringA.Hide();
                        ringB.Hide();
                    }
                    if (fx.age >= nextBeep)
                    {
                        nextBeep = fx.age + Mathf.Lerp(0.3f, 0.08f, k);
                        UltClip(UltClipId.Tick, 0.5f * lvl, 2.2f);   // ("삐, 삐, 삐", quickening)
                    }
                    comet.Show(false);
                    tail.Hide();
                    return true;
                }
                orb.Show(false);
                line.Hide();
                foreach (var dot in dots) dot.Show(false);
                if (look.phase == 1)
                {
                    // The comet, homing on the leader's chest.
                    var aimAt = look.target != null ? ChestOf(look.target) : look.head + Vector3.forward;
                    Vector3 to = aimAt - look.head;
                    float step = look.speed * d;
                    Vector3 next = look.head + (to.magnitude > step ? to.normalized * step : to);
                    if (Physics.Linecast(look.head, next, out var wall, ~0, QueryTriggerInteraction.Ignore) && wall.collider.attachedRigidbody == null)
                        kit.FlashRing(wall.point + wall.normal * 0.05f, 0.7f, CmGold);   // (through a wall: a gold ripple)
                    lastHead = look.head;
                    look.head = next;
                    CmPlace(comet, look.head, Quaternion.identity, Vector3.one * 0.5f);
                    trail.Insert(0, look.head);
                    if (trail.Count > 10) trail.RemoveAt(trail.Count - 1);
                    if (trail.Count >= 2)
                    {
                        tail.Build(trail, i => 0.32f * (1f - i / (float)trail.Count), kit.Eye);
                        tail.Apply();
                    }
                    if (target != null) JyTarget(board, target, fx.age, 1f);
                    return fx.age < 7f;
                }
                // Hit or taken: the target on the one it hit, popping big, then gone.
                comet.Show(false);
                tail.Hide();
                ringA.Hide();
                ringB.Hide();
                if (look.phase == 2 && look.aimed != null && Clock - look.at < 0.6f)
                {
                    float popK = 1f + 0.6f * Kit.EaseOut(Mathf.Clamp01((Clock - look.at) / 0.25f));
                    JyTarget(board, look.aimed, fx.age, popK);
                }
                else foreach (var t in board) { t.fade = 0f; t.Apply(); }
                return true;
            });
            f.props.Add(orb);
            f.props.Add(comet);
            foreach (var dot in dots) f.props.Add(dot);
            f.strips.Add(line);
            f.strips.Add(tail);
            f.strips.Add(ringA);
            f.strips.Add(ringB);
            foreach (var t in board) f.tiles.Add(t);
        }

        /// <summary>The target over a head: red, white and red rings 1 m across at <paramref name="size"/> 1, facing the
        /// camera (STAGING_REVIEW 3.1).</summary>
        void JyTarget(Kit.Tile[] board, RagdollPawn who, float age, float size)
        {
            var cam = ViewCamera;
            if (cam == null || who == null) return;
            Vector3 c = DrawnHead(who) + Vector3.up * (0.8f * GiantScaleOf(who));
            Vector3 n = -cam.transform.forward;
            for (int i = 0; i < board.Length; i++)
            {
                var t = board[i];
                float r = size * (i == 0 ? 1f : i == 1 ? 0.66f : 0.33f);
                t.Facing(c + n * (0.01f * i), n, Vector2.one * r, cam.transform.up);
                t.shape = 1f;
                t.inner = 0f;
                t.fill = i == 1 ? Kit.A(Color.white, 0.95f) : Kit.A(CmRed.main, 0.95f);
                t.ink = Kit.A(CmRed.ink, 1f);
                t.inkWidth = 0.03f;
                t.core = Color.clear;
                t.rim = Color.clear;
                t.stripe = Color.clear;
                t.fade = 1f;
                t.Apply();
            }
        }

        /// <summary>A dashed line as point pairs a strip draws as separate dashes (every other gap empty).</summary>
        static List<Vector3> DashPoints(Vector3 a, Vector3 b, int dashes, float phase, List<Vector3> into)
        {
            into.Clear();
            float off = Mathf.Repeat(phase, 1f);
            for (int i = 0; i < dashes; i++)
            {
                float u0 = (i + off) / dashes, u1 = (i + off + 0.5f) / dashes;
                if (u0 > 1f) break;
                into.Add(Vector3.Lerp(a, b, u0));
                into.Add(Vector3.Lerp(a, b, Mathf.Min(1f, u1)));
            }
            if (into.Count < 2)
            {
                into.Add(a);
                into.Add(b);
            }
            return into;
        }

        /// <summary>The big target over a head (1 m, tightening as <paramref name="size"/> falls), turning.</summary>
        void JyReticle(Kit.Strip outer, Kit.Strip inner, RagdollPawn who, float age, float size)
        {
            var cam = ViewCamera;
            if (cam == null || who == null) return;
            Vector3 c = DrawnHead(who) + Vector3.up * (0.75f * GiantScaleOf(who));
            Vector3 right = cam.transform.right, up = cam.transform.up;
            var pts = new List<Vector3>(26);
            for (int i = 0; i <= 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f + age * 2f;
                pts.Add(c + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * (0.5f * size));
            }
            outer.Build(pts, i => 0.06f, kit.Eye);
            outer.Apply();
            pts.Clear();
            float s = 0.32f * size;
            Vector3 r = Quaternion.AngleAxis(age * 90f, cam.transform.forward) * right, u = Quaternion.AngleAxis(age * 90f, cam.transform.forward) * up;
            pts.Add(c - r * s);
            pts.Add(c + r * s);
            pts.Add(c - u * s);
            pts.Add(c + u * s);
            inner.Build(pts, i => 0.04f, kit.Eye);
            inner.Apply();
        }

        // ---------------------------------------------------------------- 핀!

        readonly Dictionary<RagdollPawn, RagdollPawn> jyBeamFirst = new Dictionary<RagdollPawn, RagdollPawn>();

        void JyBeamFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            switch (e.count)
            {
                case 0:
                {
                    if (by == null) return;
                    // A white point on the staff.
                    var dot = CmProp(kit.meshBall, CmWhite, "Beam point", 0.006f);
                    float prep = Mathf.Max(0.05f, e.size);
                    var f = kit.Run(prep, (fx, d) =>
                    {
                        if (by == null) return false;
                        Vector3 tip = by.bodies[(int)BodyId.HandR].position + FlatDir(by.Facing) * 0.4f + Vector3.up * 0.2f;
                        CmPlace(dot, tip, Quaternion.identity, Vector3.one * (0.08f + 0.12f * fx.age / prep));
                        return true;
                    });
                    f.props.Add(dot);
                    UltClip(UltClipId.Tick, 0.5f * lvl, 2.6f);   // ("삐—")
                    if (by != null) jyBeamFirst.Remove(by);
                    break;
                }
                case 1:
                {
                    // The beam: out in 0.1 s, a white core in a gold glow, 0.25 s; diamond cells along the floor under it.
                    Vector3 from = e.at, dir = FlatDirOr(e.dir);
                    Vector3 full = e.dir.sqrMagnitude > 1e-6f ? e.dir.normalized : dir;
                    float len = Mathf.Max(0.5f, e.size);
                    var glow = new Kit.Strip(kit, CmGold) { core = Color.white, coreShare = 0.35f, inkShare = 0.05f };
                    var pts = new List<Vector3>(2);
                    var cells = new List<Kit.Tile>();
                    int n = Mathf.Clamp(Mathf.FloorToInt(len / 1.6f), 1, 20);
                    for (int i = 0; i < n; i++) cells.Add(new Kit.Tile(kit, "Beam cell"));
                    UltClip(UltClipId.Whoosh, 0.8f * lvl, 2.6f);   // ("피융!")
                    var f = kit.Run(0.5f, (fx, d) =>
                    {
                        float grow = Mathf.Clamp01(fx.age / 0.1f), outk = Mathf.Clamp01((fx.age - 0.35f) / 0.15f);
                        pts.Clear();
                        pts.Add(from);
                        pts.Add(from + full * (len * grow));
                        glow.Build(pts, i => 0.5f * (1f - outk), kit.Eye);
                        glow.Apply();
                        for (int i = 0; i < cells.Count; i++)
                        {
                            float along = (i + 0.5f) * len / cells.Count;
                            float lit = Mathf.Clamp01((len * grow - along) * 2f) * (1f - outk);
                            var c = cells[i];
                            c.Floor(Ground(from + full * along + Vector3.up * 1f) + Vector3.up * 0.02f, Quaternion.AngleAxis(45f, Vector3.up) * dir, Vector2.one * 0.75f);
                            c.shape = 0f;
                            c.fill = Kit.A(CmGold.light, 0.55f);
                            c.ink = Kit.A(CmGold.ink, 0.8f);
                            c.inkWidth = 0.03f;
                            c.core = Color.clear;
                            c.rim = Color.clear;
                            c.stripe = Color.clear;
                            c.fade = lit;
                            c.Apply();
                        }
                        return true;
                    });
                    f.strips.Add(glow);
                    foreach (var c in cells) f.tiles.Add(c);
                    break;
                }
                default:
                {
                    // Caught: "찰칵!" and the shutter; two on one line: a light thread between them while frozen.
                    var who = e.target;
                    if (who == null) return;
                    float seconds = Mathf.Max(0.3f, e.size);
                    if (e.count == 2)
                    {
                        JyWord("찰칵!", ChestOf(who), JyWordWhite, who);
                        UltClip(UltClipId.Tap, 0.9f * lvl, 2.2f);
                        UltClip(UltClipId.Tick, 0.7f * lvl, 1.4f, 0.04f);   // (the shutter's two clicks)
                        if (by != null) jyBeamFirst[by] = who;
                    }
                    else if (by != null && jyBeamFirst.TryGetValue(by, out var first) && first != null)
                    {
                        var thread = new Kit.Strip(kit, CmGold) { core = Color.white, coreShare = 0.6f, inkShare = 0.1f };
                        var pts = new List<Vector3>(2);
                        var f = kit.Run(seconds, (fx, d) =>
                        {
                            if (first == null || who == null) return false;
                            pts.Clear();
                            pts.Add(ChestOf(first));
                            pts.Add(ChestOf(who));
                            thread.Build(pts, i => 0.04f, kit.Eye);
                            thread.fade = 0.8f;
                            thread.Apply();
                            return true;
                        });
                        f.strips.Add(thread);
                    }
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- 멈춤 (5.4)

        /// <summary>Frozen: grey with a white edge, ⏸ over the head with its time running round (▶ blinking the last half
        /// second), dust and sweat hanging in the air; "째깍" each half second; everything back with "뿅".</summary>
        void CcFrozenLook(CcLook look, float lvl)
        {
            var who = look.who;
            if (who == null) return;
            var grey = JyGreyMaterial();
            var edge = JyOutlineMaterial();
            var barL = CmProp(JyPauseBar(), CmWhite, "Pause bar", 0.012f);
            var barR = CmProp(JyPauseBar(), CmWhite, "Pause bar", 0.012f);
            var play = CmProp(JyPlayMesh(), CmWhite, "Play", 0.012f);
            var ring = new Kit.Strip(kit, CmWhite) { core = Color.white, coreShare = 0.4f, inkShare = 0.35f };
            // Dust and sweat hung round it.
            var bits = new List<(Kit.Prop p, Vector3 off, bool drop)>();
            Vector3 hips = who.Hips.position;
            for (int i = 0; i < 6; i++)
            {
                bool drop = i >= 4;
                var p = CmProp(kit.meshBall, drop ? CmSweat : CmDust, drop ? "Frozen sweat" : "Frozen dust", 0.008f);
                bits.Add((p, Random.insideUnitSphere * 0.55f + Vector3.up * (drop ? 0.6f : -0.3f), drop));
            }
            float total = Mathf.Max(0.2f, look.until - Clock), born = Clock, nextTick = 0.5f;
            var pts = new List<Vector3>(26);
            var f = kit.Run(total + 0.6f, (fx, d) =>
            {
                if (who == null) return false;
                float left = look.until - Clock;
                var cam = ViewCamera;
                if (left > 0f)
                {
                    if (grey != null) WantGiant(who, 1f, 60f, grey);
                    if (edge != null) WantOutline(who, edge);
                    float k = GiantScaleOf(who);
                    Vector3 c = DrawnHead(who) + Vector3.up * (0.75f * k);
                    Quaternion q = cam != null ? Quaternion.LookRotation(cam.transform.forward, Vector3.up) : Quaternion.identity;
                    Vector3 right = cam != null ? cam.transform.right : Vector3.right;
                    bool last = left < 0.5f, on = !last || Mathf.Repeat(left * 6f, 1f) > 0.4f;
                    CmPlace(barL, c - right * 0.1f, q, Vector3.one * (on && !last ? 1f : 0.01f));
                    CmPlace(barR, c + right * 0.1f, q, Vector3.one * (on && !last ? 1f : 0.01f));
                    CmPlace(play, c, q, Vector3.one * (on && last ? 1f : 0.01f));
                    // Its time running round.
                    float share = Mathf.Clamp01(left / total);
                    pts.Clear();
                    Vector3 up = cam != null ? cam.transform.up : Vector3.up;
                    int n = Mathf.Max(2, Mathf.RoundToInt(24 * share));
                    for (int i = 0; i <= n; i++)
                    {
                        float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f * share / n;
                        pts.Add(c + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * 0.36f);
                    }
                    ring.Build(pts, i => 0.045f, kit.Eye);
                    ring.Apply();
                    foreach (var b in bits) CmPlace(b.p, hips + b.off, Quaternion.identity, Vector3.one * (b.drop ? 0.07f : 0.13f));
                    if (Clock - born >= nextTick)
                    {
                        nextTick += 0.5f;
                        UltClip(UltClipId.Tock, 0.4f * S3Heard(ChestOf(who)), 1.5f);   // ("째깍")
                    }
                    return true;
                }
                // Free: the bits move on again (dust drifting up, sweat falling), the marks gone.
                float t = -left;
                barL.Show(false);
                barR.Show(false);
                play.Show(false);
                ring.Hide();
                foreach (var b in bits)
                {
                    Vector3 at = hips + b.off + (b.drop ? Vector3.down * (4.9f * t * t) : Vector3.up * (0.6f * t));
                    CmPlace(b.p, at, Quaternion.identity, Vector3.one * (b.drop ? 0.07f : 0.13f) * (1f - Mathf.Clamp01(t / 0.6f)));
                }
                return true;
            });
            foreach (var b in bits) f.props.Add(b.p);
            f.props.Add(barL);
            f.props.Add(barR);
            f.props.Add(play);
            f.strips.Add(ring);
            f.end = () =>
            {
                if (who == null) return;
                CmPoof(ChestOf(who), CmWhite, 3, 0.5f, 0.22f, 0.35f, 0.3f);
                UltClip(UltClipId.Swell, 0.5f * S3Heard(ChestOf(who)), 1.8f);   // ("뿅")
            };
        }

        Material JyGreyMaterial()
        {
            if (jyGrey != null || LitMaterials.Lit == null) return jyGrey;
            jyGrey = LitMaterials.NewLit(new Color(0.66f, 0.66f, 0.68f), 0.15f, 0f);
            jyGrey.hideFlags = HideFlags.HideAndDontSave;
            return jyGrey;
        }

        Material JyOutlineMaterial()
        {
            if (jyOutline != null) return jyOutline;
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) return null;
            jyOutline = new Material(sh) { name = "Frozen outline", hideFlags = HideFlags.HideAndDontSave };
            if (jyOutline.HasProperty("_BaseColor")) jyOutline.SetColor("_BaseColor", Color.white);
            jyOutline.color = Color.white;
            if (jyOutline.HasProperty("_Cull")) jyOutline.SetFloat("_Cull", 1f);   // (front faces off: the inside of a bigger copy)
            return jyOutline;
        }

        Mesh JyPauseBar() => jyPauseBar != null ? jyPauseBar : jyPauseBar = kit.RoundBox(new Vector3(0.12f, 0.4f, 0.06f), 0.04f);

        /// <summary>▶: a flat rounded triangle pointing right (+X), facing -Z.</summary>
        Mesh JyPlayMesh()
        {
            if (jyPlay != null) return jyPlay;
            return jyPlay = CmExtrude(new[] { new Vector2(-0.15f, -0.2f), new Vector2(0.22f, 0f), new Vector2(-0.15f, 0.2f) }, 0.06f, "Play sign");
        }

        void DestroySetFourJYB()
        {
            jySwords.Clear();
            jyBows.Clear();
            jyBeamFirst.Clear();
            foreach (var m in new UnityEngine.Object[] { jyGrey, jyOutline, jyPauseBar, jyPlay })
                if (m != null) Destroy(m);
            jyGrey = jyOutline = null;
            jyPauseBar = jyPlay = null;
            DestroySetFourJYC();
        }
    }
}
