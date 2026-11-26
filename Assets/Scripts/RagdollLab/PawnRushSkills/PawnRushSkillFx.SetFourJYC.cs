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
    /// The user's own skills on every screen (USER_SKILLS 5장), bundle 3: the rook's four, and what every wall leaves when
    /// it tumbles (dust and bouncing bricks; the bricks themselves are PawnRushKitWall's). Not seen in Unity yet.
    ///
    ///   거대화      a deep breath, shaking, a ring at its feet as big as it will be; up like a balloon in three puffs
    ///              (1.5, 2.5, 4 times) wobbling each; a glowing footprint and its 8 s ring where it grew; dust and a thud at
    ///              every giant step; whoever it kicks curls up and flies ("뻥!"); its last second blinking and hissing;
    ///              then flapping like a balloon let go, shrinking back into the footprint ("푸슈우우욱… 뽁")
    ///   힘세지기    "으랏차!" and a flex, puffed up 1.5 times, steam from the nose, a strength ring over the head; heavy
    ///              steps; a red cell under the enemy it could lift; "덥석!" and the lifted one kicking on its back over the
    ///              head, sweat flying; "이얍!" and the throw spinning on a trail, "쿵!" where it lands; slipping out ("쏙")
    ///              leaves it puzzled; at the end it hisses down to size, shoulders dropping
    ///   벨코즈 q 성벽 a stamp and a crack running out along the aim; the wall rising piece by piece behind a glowing orange
    ///              head; "쩍!" where it splits both ways, a little flagged tower at the corner
    ///   성벽 짓기    the rook sat down grey with a hammer over it, its glowing clone out (teal to its side, red to the
    ///              other), dotted cells ahead of the clone; a squeal and a little tower at each turn; a red flash and "!"
    ///              over anyone shut in; a flag on the last piece and the clone gone in sparkles ("짠!"), the rook its colour
    ///              again; cut short, the clone pops
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly Kit.Palette CmOrange = new Kit.Palette("#FF9A3C", "#FFD08A", "#D2621A", "#3A1A06");
        static readonly Kit.Palette CmSand = new Kit.Palette("#DEBA80", "#F4DDB0", "#A88250", "#3C2A16");
        static readonly Kit.Palette CmTeal = new Kit.Palette("#3CC7B8", "#9FF0E6", "#1E8C80", "#08302C");

        void SetFourJYCFx(SkillFxEvent e, float lvl)
        {
            switch (e.kind)
            {
                case SkillFxKind.JyGiant: JyGiantFx(e, lvl); break;
                case SkillFxKind.JyStrong: JyStrongFx(e, lvl); break;
                case SkillFxKind.JyTWall: JyTWallFx(e, lvl); break;
                case SkillFxKind.JyBuild: JyBuildFx(e, lvl); break;
                case SkillFxKind.JyWallLeg: JyWallLegFx(e); break;
                case SkillFxKind.JyWallEnd: JyWallEndFx(e); break;
                default: SetFourFixFx(e, lvl); break;   // the round-1 fixes (PawnRushSkillFx.SetFourFix.cs)
            }
        }

        /// <summary>Every frame (UltGiants): the walls learn whose side this PC is on and where to send their tumbling.</summary>
        void JyWallsEachFrame()
        {
            var v = Viewer;
            PawnRushKitWall.ViewerTeam = v != null ? (int)v.Team : -1;
            PawnRushKitWall.OnCrumble = JyWallCrumbleFx;
        }

        /// <summary>A wall tumbling down: stone-coloured dust along it and three or four bricks bouncing off and popping.</summary>
        void JyWallCrumbleFx(PawnRushKitWall w, Vector3 a, Vector3 b)
        {
            if (w == null || kit == null) return;
            float len = Flat(b - a).magnitude;
            int n = Mathf.Clamp(Mathf.CeilToInt(len / 1.5f), 1, 10);
            for (int i = 0; i <= n; i++) CmPoof(Vector3.Lerp(b, a, i / (float)n) + Vector3.up * 0.6f, CmRock, 2, 0.7f, 0.42f, 0.55f, 0.35f);
            UltClip(UltClipId.Crack, 0.6f * S3Heard(a), 0.6f);
            UltClip(UltClipId.SubDrop, 0.4f * S3Heard(a), 1.2f);   // ("와르르")
            int bricks = Mathf.Clamp(Mathf.RoundToInt(len / 2f), 2, 4);
            for (int i = 0; i < bricks; i++)
            {
                var brick = CmProp(kit.RoundBox(new Vector3(0.36f, 0.18f, 0.18f), 0.03f), CmSand, "Wall brick", 0.012f);
                Vector3 from = Vector3.Lerp(a, b, Random.value) + Vector3.up * 1.2f;
                Vector3 v = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(2.5f, 4f), Random.Range(-2.5f, 2.5f));
                Vector3 spin = Random.onUnitSphere * 500f;
                float floorY = Ground(from).y;
                Vector3 pos = from;
                int bounces = 0;
                var f = kit.Run(1.1f, (fx, d) =>
                {
                    v += Vector3.down * (9.8f * d);
                    pos += v * d;
                    if (pos.y < floorY + 0.09f && v.y < 0f)
                    {
                        pos.y = floorY + 0.09f;
                        v = new Vector3(v.x * 0.6f, -v.y * 0.45f, v.z * 0.6f);
                        bounces++;
                    }
                    float outk = Mathf.Clamp01((fx.age - 0.85f) / 0.25f);
                    if (outk > 0f && fx.age - d <= 0.85f) CmPoof(pos, CmDust, 1, 0.2f, 0.16f, 0.3f, 0.2f);
                    CmPlace(brick, pos, Quaternion.Euler(spin * fx.age), Vector3.one * (1f - outk));
                    return true;
                });
                f.props.Add(brick);
            }
        }

        // ---------------------------------------------------------------- the walls on other PCs

        sealed class JyRemoteLeg { public PawnRushKitWall wall; public Vector3 a, dir; public float speed, max, born; public bool done; }
        readonly Dictionary<int, JyRemoteLeg> jyRemoteLegs = new Dictionary<int, JyRemoteLeg>();

        /// <summary>Another PC's: a picture of the host's growing wall, grown at its speed until its end is told.</summary>
        void JyWallLegFx(SkillFxEvent e)
        {
            var by = e.by;
            UltClip(UltClipId.Tap, 0.35f * S3Heard(e.at), 0.7f);   // ("두두두두")
            if (by == null || !by.NetworkPuppet) return;
            float high = e.normal.y > 0f ? e.normal.y : 2f, thick = e.normal.z > 0f ? e.normal.z : 0.6f;
            var w = PawnRushKitWall.Raise(e.count, by, e.at, e.at + FlatDirOr(e.dir) * 0.05f, high, thick, 60f, 0.15f, false);
            var leg = new JyRemoteLeg { wall = w, a = e.at, dir = FlatDirOr(e.dir), speed = Mathf.Max(1f, e.size), max = Mathf.Max(0.3f, e.normal.x), born = Clock };
            jyRemoteLegs[e.count] = leg;
            kit.Run(30f, (fx, d) =>
            {
                if (leg.wall == null || leg.done) return false;
                float len = Mathf.Min(leg.max, leg.speed * (Clock - leg.born));
                leg.wall.SetLine(leg.a, leg.a + leg.dir * Mathf.Max(0.05f, len));
                return true;
            });
        }

        void JyWallEndFx(SkillFxEvent e)
        {
            if (!jyRemoteLegs.TryGetValue(e.count, out var leg)) return;
            leg.done = true;
            if (leg.wall != null)
            {
                leg.wall.SetLine(leg.a, e.at);
                leg.wall.StandFor(Mathf.Max(0.1f, e.size));
            }
        }

        // ---------------------------------------------------------------- 거대화

        sealed class JyGiantLook { public int phase; public float at; }
        readonly Dictionary<RagdollPawn, JyGiantLook> jyGiants = new Dictionary<RagdollPawn, JyGiantLook>();

        void JyGiantFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            float scale = Params != null ? Params.jyGiantScale : 4f;
            switch (e.count)
            {
                case 0:
                {
                    if (by == null) return;
                    var look = new JyGiantLook { phase = 0, at = Clock };
                    jyGiants[by] = look;
                    float prep = Mathf.Max(0.1f, e.size);
                    Vector3 home = Ground(e.at + Vector3.up * 0.5f) + Vector3.up * 0.03f;
                    bool ours = Viewer != null && Viewer.Team == by.Team && by.Team != Teams.None;
                    var ring = new Kit.Tile(kit, "Giant size");
                    UltClip(UltClipId.Riser, 0.7f * lvl, 0.8f);   // ("흐읍—")
                    var f = kit.Run(prep, (fx, d) =>
                    {
                        if (by == null) return false;
                        // Trembling as it breathes in; the size it will be on the floor.
                        WantPose(by, Quaternion.Euler(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f)), false);
                        WantStretch(by, new Vector3(1f + 0.12f * fx.age / prep, 1f, 1f + 0.12f * fx.age / prep));
                        ring.Floor(home, Vector3.forward, Vector2.one * (2f * 0.55f * scale * Kit.EaseOut(fx.age / prep)));
                        ring.shape = 1f;
                        ring.inner = 0.9f;
                        ring.fill = ours ? Kit.A(CmTeal.main, 0.6f) : Kit.A(CmRed.main, 0.7f);
                        ring.ink = Kit.A(Color.black, 0.5f);
                        ring.inkWidth = 0.03f;
                        ring.core = Color.clear;
                        ring.rim = Color.clear;
                        ring.stripe = Color.clear;
                        ring.fade = 1f;
                        ring.Apply();
                        return true;
                    });
                    f.tiles.Add(ring);
                    break;
                }
                case 1:
                {
                    if (by == null || !jyGiants.TryGetValue(by, out var look)) return;
                    look.phase = 1;
                    look.at = Clock;
                    JyGiantRunFx(by, look, Mathf.Max(1f, e.size), scale, Ground(e.at + Vector3.up * 0.5f), lvl);
                    break;
                }
                case 2:
                {
                    var who = e.target;
                    JyHit(who, e.at, 2, CmDust);
                    JyWord("뻥!", e.at, JyWordGold, who);
                    UltClip(UltClipId.Crack, 0.8f * lvl, 0.8f);
                    if (who != null)
                    {
                        CmStreak(who, CmDust, 0.7f, 0.26f, 0.2f);
                        kit.Run(0.6f, (fx, d) =>
                        {
                            if (who == null) return false;
                            WantStretch(who, new Vector3(0.85f, 0.7f, 0.85f));   // (curled up like a ball)
                            return true;
                        });
                    }
                    break;
                }
                case 3:
                {
                    if (by == null) return;
                    if (jyGiants.TryGetValue(by, out var look)) look.phase = 2;
                    // Let go like a balloon: flapping and shrinking back into its footprint.
                    Vector3 was = e.normal, home = Ground(e.at + Vector3.up * 0.5f);
                    UltClip(UltClipId.Wind, 0.8f * lvl, 1.6f);
                    UltClip(UltClipId.Swell, 0.7f * lvl, 2.2f, 0.45f);   // ("푸슈우우욱… 뽁")
                    for (int i = 0; i <= 6; i++) CmPoof(Vector3.Lerp(was, home, i / 6f) + Vector3.up * (1.5f + Mathf.Sin(i / 6f * Mathf.PI) * 2f), CmWhite, 1, 0.3f, 0.3f, 0.5f, 0.2f);
                    kit.Run(0.5f, (fx, d) =>
                    {
                        if (by == null) return false;
                        float t = fx.age / 0.5f;
                        WantGiant(by, Mathf.Lerp(scale * 0.6f, 1f, Kit.EaseIn(t)), 60f);
                        WantPose(by, Quaternion.Euler(30f * Mathf.Sin(fx.age * 37f) * (1f - t), 0f, 30f * Mathf.Cos(fx.age * 29f) * (1f - t)), true);
                        return true;
                    }).end = () => { if (by != null) CmPoof(by.FeetPoint + Vector3.up * 0.2f, CmWhite, 3, 0.4f, 0.24f, 0.35f, 0.3f); };
                    break;
                }
            }
        }

        /// <summary>The giant's seconds: three puffs up, the steps thudding, the footprint and its ring where it grew, the last
        /// second blinking.</summary>
        void JyGiantRunFx(RagdollPawn by, JyGiantLook look, float seconds, float scale, Vector3 home, float lvl)
        {
            float grow = Params != null ? Mathf.Max(0.1f, Params.jyGiantGrow) : 0.5f;
            var foot = new Kit.Tile(kit, "Giant footprint");
            var timer = new Kit.Strip(kit, CmGold) { core = Color.white, coreShare = 0.3f, inkShare = 0.25f };
            var pts = new List<Vector3>(34);
            float nextStep = 0f;
            int puffed = 0;
            bool hissed = false;
            Material white = null;
            var f = kit.Run(seconds + 0.2f, (fx, d) =>
            {
                if (by == null || look.phase >= 2) return false;
                float t = fx.age;
                // Up in three puffs (1.5, 2.5, 4), each overshooting and wobbling.
                float[] steps = { 1.5f, 2.5f, scale };
                int step = Mathf.Min(2, Mathf.FloorToInt(t / (grow / 3f)));
                if (step + 1 > puffed && step < 3)
                {
                    puffed = step + 1;
                    UltClip(UltClipId.Swell, 0.6f * lvl, 0.9f + 0.25f * step);   // ("뿌우, 뿌우, 뿌웅!")
                }
                float into = (t - step * grow / 3f) / (grow / 3f);
                float s = t < grow ? Mathf.Lerp(step == 0 ? 1f : steps[step - 1], steps[step], Kit.EaseOutBack(Mathf.Clamp01(into))) : scale;
                float last = seconds - t;
                bool blink = last < 1f && Mathf.Repeat(last * 3f, 1f) > 0.6f;
                if (blink && white == null) white = jyWhite;
                WantGiant(by, s, 60f, blink ? jyWhite : null);
                if (last < 1f && !hissed)
                {
                    hissed = true;
                    UltClip(UltClipId.Wah, 0.6f * lvl, 2.1f);   // ("삐이익—")
                }
                // Each giant step: dust and a thud (a small shake for those near).
                if (by.HorizontalSpeed > 1f && t >= nextStep && t > grow)
                {
                    nextStep = t + 0.55f;
                    kit.DustRing(by.FeetPoint + Vector3.up * 0.05f, 10, 0.6f * s, CmDust, 0.35f, 0.5f);
                    UltClip(UltClipId.SubDrop, 0.4f * S3Heard(by.FeetPoint), 1.1f);   // ("쿵")
                    JyShake(by.FeetPoint, 0.04f);
                }
                // Where it grew: a glowing footprint and its ring running down.
                foot.Floor(home + Vector3.up * 0.03f, Vector3.forward, Vector2.one * 0.9f);
                foot.shape = 1f;
                foot.inner = 0f;
                foot.fill = Kit.A(CmGold.light, 0.55f + 0.2f * Mathf.Sin(t * 6f));
                foot.ink = Kit.A(CmGold.ink, 0.8f);
                foot.inkWidth = 0.03f;
                foot.core = Color.clear;
                foot.rim = Color.clear;
                foot.stripe = Color.clear;
                foot.fade = 1f;
                foot.Apply();
                float share = Mathf.Clamp01(last / seconds);
                pts.Clear();
                int n = Mathf.Max(2, Mathf.RoundToInt(32 * share));
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f * share / n;
                    pts.Add(home + new Vector3(Mathf.Cos(a), 0.05f, Mathf.Sin(a)) * 0.75f);
                }
                timer.Build(pts, i => 0.06f, kit.Eye, true);
                timer.Apply();
                return true;
            });
            f.tiles.Add(foot);
            f.strips.Add(timer);
            if (jyWhite == null && LitMaterials.Lit != null) JyFlashWhite(by);   // (makes the white paint once)
        }

        // ---------------------------------------------------------------- 힘세지기

        sealed class JyStrongLook { public int phase; public RagdollPawn held; public float heldAt; }
        readonly Dictionary<RagdollPawn, JyStrongLook> jyStrongs = new Dictionary<RagdollPawn, JyStrongLook>();

        void JyStrongFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            float big = Params != null ? Params.jyStrongScale : 1.5f;
            switch (e.count)
            {
                case 0:
                {
                    if (by == null) return;
                    var look = new JyStrongLook { phase = 0 };
                    jyStrongs[by] = look;
                    SayBubble(by, "으랏차!", 0.9f);
                    UltClip(UltClipId.Swell, 0.6f * lvl, 1.2f);
                    float prep = Mathf.Max(0.1f, e.size);
                    kit.Run(prep, (fx, d) =>
                    {
                        if (by == null) return false;
                        WantGiant(by, Mathf.Lerp(1f, big, Kit.EaseOutBack(fx.age / prep)), 60f);
                        WantStretch(by, new Vector3(1.15f, 1f, 1.15f));
                        if (Random.value < 0.3f) CmPoof(DrawnHead(by) + FlatDir(by.Facing) * 0.25f, CmSteam, 1, 0.1f, 0.12f, 0.35f, 0.1f);
                        return true;
                    });
                    break;
                }
                case 1:
                {
                    if (by == null || !jyStrongs.TryGetValue(by, out var look)) return;
                    look.phase = 1;
                    JyStrongRunFx(by, look, Mathf.Max(1f, e.size), big, lvl);
                    break;
                }
                case 2:
                {
                    if (by == null || !jyStrongs.TryGetValue(by, out var look) || e.target == null) return;
                    look.held = e.target;
                    look.heldAt = Clock;
                    JyWord("덥석!", e.at, JyWordWood, e.target);
                    UltClip(UltClipId.Tap, 0.8f * lvl, 0.9f);
                    var who = e.target;
                    kit.Run(Mathf.Max(0.5f, e.size) + 0.5f, (fx, d) =>
                    {
                        if (who == null || look.held != who) return false;
                        // On its back over the head, kicking, sweat flying.
                        Vector3 side = Vector3.Cross(Vector3.up, FlatDir(by.Facing));
                        WantPose(who, Quaternion.AngleAxis(90f, side) * Quaternion.Euler(0f, 0f, 18f * Mathf.Sin(fx.age * 22f)), true);
                        if (Random.value < 0.2f) CmPoof(DrawnHead(who), CmSweat, 1, 0.3f, 0.08f, 0.4f, 0.4f);
                        return true;
                    });
                    break;
                }
                case 3:
                {
                    if (by != null && jyStrongs.TryGetValue(by, out var look)) look.held = null;
                    var who = e.target;
                    if (by != null) SayBubble(by, "이얍!", 0.7f);
                    UltClip(UltClipId.Whoosh, 0.8f * lvl, 1f);
                    if (who == null) break;
                    CmStreak(who, CmOrange, 0.8f, 0.26f, 0.2f);
                    bool landed = false;
                    kit.Run(1.2f, (fx, d) =>
                    {
                        if (who == null) return false;
                        Vector3 side = Vector3.Cross(Vector3.up, FlatDirOr(e.dir));
                        if (!landed) WantPose(who, Quaternion.AngleAxis(fx.age * 720f, side), true);
                        if (!landed && fx.age > 0.35f && who.Hips.position.y - Ground(who.Hips.position + Vector3.up).y < 0.7f)
                        {
                            landed = true;
                            CmPoof(who.FeetPoint + Vector3.up * 0.2f, CmDust, 4, 1f, 0.42f, 0.5f, 0.3f);
                            JyWord("쿵!", who.Hips.position, JyWordRed, who);
                            UltClip(UltClipId.Crack, 0.7f * lvl, 1.1f);
                            UltClip(UltClipId.Tock, 0.6f * lvl, 1.4f, 0.05f);   // (a bowling pin, the nearest)
                            JyShake(who.FeetPoint, 0.08f);
                        }
                        return true;
                    });
                    break;
                }
                case 4:
                    if (by != null && jyStrongs.TryGetValue(by, out var l4)) l4.held = null;
                    if (e.target != null) CmPoof(e.target.Hips.position, CmWhite, 2, 0.3f, 0.18f, 0.3f, 0.2f);
                    UltClip(UltClipId.Tap, 0.5f * lvl, 2.3f);   // ("쏙")
                    if (by != null) SayBubble(by, "?", 0.5f);
                    break;
                case 5:
                {
                    if (by == null) return;
                    if (jyStrongs.TryGetValue(by, out var l5)) l5.phase = 2;
                    UltClip(UltClipId.Wind, 0.5f * lvl, 1.8f);   // ("푸쉬—")
                    CmPoof(DrawnHead(by), CmSteam, 3, 0.4f, 0.24f, 0.5f, 0.4f);
                    kit.Run(0.5f, (fx, d) =>
                    {
                        if (by == null) return false;
                        WantGiant(by, Mathf.Lerp(big, 1f, fx.age / 0.5f), 60f);
                        WantPose(by, Quaternion.AngleAxis(10f * Mathf.Sin(fx.age / 0.5f * Mathf.PI), Vector3.Cross(Vector3.up, FlatDir(by.Facing))), false);
                        return true;
                    });
                    break;
                }
            }
        }

        /// <summary>Strong for its seconds: drawn big, a ring over its head running down, thudding steps, a red cell under the
        /// enemy it could lift.</summary>
        void JyStrongRunFx(RagdollPawn by, JyStrongLook look, float seconds, float big, float lvl)
        {
            var timer = new Kit.Strip(kit, CmOrange) { core = Color.white, coreShare = 0.3f, inkShare = 0.25f };
            var cell = new Kit.Tile(kit, "Liftable");
            var pts = new List<Vector3>(34);
            float nextStep = 0f, reach = Params != null ? Params.jyStrongReach : 2f;
            var f = kit.Run(seconds + 0.2f, (fx, d) =>
            {
                if (by == null || look.phase >= 2) return false;
                WantGiant(by, big, 60f);
                float k = GiantScaleOf(by);
                Vector3 c = DrawnHead(by) + Vector3.up * (0.55f * k);
                var cam = ViewCamera;
                Vector3 right = cam != null ? cam.transform.right : Vector3.right, up = cam != null ? cam.transform.up : Vector3.up;
                float share = Mathf.Clamp01(1f - fx.age / seconds);
                pts.Clear();
                int n = Mathf.Max(2, Mathf.RoundToInt(32 * share));
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.PI * 0.5f - i * Mathf.PI * 2f * share / n;
                    pts.Add(c + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * 0.3f);
                }
                timer.Build(pts, i => 0.05f, kit.Eye);
                timer.Apply();
                if (by.HorizontalSpeed > 0.8f && fx.age >= nextStep)
                {
                    nextStep = fx.age + 0.5f;
                    UltClip(UltClipId.Tock, 0.35f * S3Heard(by.FeetPoint), 0.6f);   // (a heavy step)
                    CmPoof(by.FeetPoint + Vector3.up * 0.05f, CmDust, 1, 0.3f, 0.2f, 0.35f, 0.15f);
                }
                if (Random.value < 0.03f) CmStreak(by, CmOrange, 0.25f, 0.2f, 0.16f);
                // The enemy it could lift: a red cell under it.
                RagdollPawn best = null;
                float bestA = 60f;
                if (look.held == null)
                {
                    foreach (var o in RagdollPawn.All)
                    {
                        if (o == null || o == by || o.Team == by.Team) continue;
                        Vector3 dd = o.Hips.position - by.Hips.position;
                        if (Flat(dd).magnitude > reach + 0.3f || Mathf.Abs(dd.y) > 1.3f) continue;
                        float a = Vector3.Angle(FlatDir(by.Facing), Flat(dd));
                        if (a < bestA)
                        {
                            bestA = a;
                            best = o;
                        }
                    }
                }
                if (best != null)
                {
                    cell.Floor(Ground(best.FeetPoint + Vector3.up * 0.4f) + Vector3.up * 0.02f, FlatDir(by.Facing), Vector2.one * 1f);
                    cell.shape = 0f;
                    cell.fill = Kit.A(CmRed.main, 0.55f);
                    cell.ink = Kit.A(CmRed.ink, 0.9f);
                    cell.inkWidth = 0.035f;
                    cell.stripe = Kit.A(Color.white, 0.25f);
                    cell.stripePhase = fx.age * 1.5f;
                    cell.core = Color.clear;
                    cell.rim = Color.clear;
                    cell.fade = 1f;
                }
                else cell.fade = 0f;
                cell.Apply();
                return true;
            });
            f.strips.Add(timer);
            f.tiles.Add(cell);
        }

        // ---------------------------------------------------------------- 벨코즈 q 성벽

        readonly Dictionary<RagdollPawn, int> jyTWalls = new Dictionary<RagdollPawn, int>();

        void JyTWallFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            float speed = Params != null ? Params.jyTWallSpeed : 20f, length = Params != null ? Params.jyTWallLength : 10f;
            float wing = Params != null ? Params.jyTWallWing : 4f, stand = Params != null ? Params.jyTWallSeconds : 5f;
            Vector3 dir = FlatDirOr(e.dir), side = Vector3.Cross(Vector3.up, dir);
            if (e.count == 0)
            {
                if (by == null) return;
                jyTWalls[by] = 0;
                // The stamp: a crack running out along the aim, an orange glowing head leading the wall.
                float prep = Mathf.Max(0.05f, e.size);
                Vector3 feet = Ground(e.at + Vector3.up * 0.5f) + Vector3.up * 0.03f;
                kit.AddSquash(by, Kit.SquashKind.Down, 0.05f);
                CmPoof(feet + Vector3.up * 0.1f, CmDust, 3, 0.6f, 0.3f, 0.4f, 0.2f);
                UltClip(UltClipId.SubDrop, 0.8f * lvl, 0.9f);   // ("쾅")
                JyShake(feet, 0.08f);
                var crack = new Kit.Strip(kit, CmCrackInk) { core = CmCrackInk.main, coreShare = 0.6f, inkShare = 0.1f };
                var wedge = CmProp(kit.RoundBox(new Vector3(0.7f, 0.5f, 0.5f), 0.12f), CmOrange, "Wall head", 0.016f);
                var pts = new List<Vector3>(12);
                var f = kit.Run(prep + length / speed + 0.3f, (fx, d) =>
                {
                    if (by == null) return false;
                    float t = fx.age;
                    float crackLen = Mathf.Min(length, (t / Mathf.Max(0.05f, prep + 0.1f)) * length);
                    pts.Clear();
                    for (int i = 0; i <= 10; i++)
                    {
                        float u = i / 10f;
                        pts.Add(feet + dir * (0.6f + crackLen * u) + side * (0.08f * Mathf.Sin(u * 23f)));
                    }
                    crack.Build(pts, i => 0.07f, kit.Eye, true);
                    crack.fade = 1f - Mathf.Clamp01((t - prep - length / speed) / 0.3f);
                    crack.Apply();
                    int split = jyTWalls.TryGetValue(by, out var sp) ? sp : 1;
                    float head = Mathf.Min(length, speed * Mathf.Max(0f, t - prep));
                    if (split == 0 && t >= prep && head < length - 0.05f)
                        CmPlace(wedge, feet + dir * (0.8f + head) + Vector3.up * 0.45f, Quaternion.LookRotation(dir, Vector3.up), Vector3.one * (0.9f + 0.1f * Mathf.Sin(t * 30f)));
                    else wedge.Show(false);
                    return true;
                });
                f.strips.Add(crack);
                f.props.Add(wedge);
                return;
            }
            // The split: "쩍!", two heads running out both ways, a little flagged tower at the corner.
            if (by != null) jyTWalls[by] = 1;
            Vector3 at = Ground(e.at + Vector3.up * 0.5f);
            JyWord("쩍!", at + Vector3.up * 1.6f, JyWordRed);
            UltClip(UltClipId.Crack, 0.8f * lvl, 1.2f);
            var l = CmProp(kit.RoundBox(new Vector3(0.6f, 0.45f, 0.45f), 0.1f), CmOrange, "Wall head", 0.016f);
            var r = CmProp(kit.RoundBox(new Vector3(0.6f, 0.45f, 0.45f), 0.1f), CmOrange, "Wall head", 0.016f);
            var f2 = kit.Run(wing / speed + 0.05f, (fx, d) =>
            {
                float go = Mathf.Min(wing, speed * fx.age);
                CmPlace(l, at + side * go + Vector3.up * 0.45f, Quaternion.LookRotation(side, Vector3.up), Vector3.one);
                CmPlace(r, at - side * go + Vector3.up * 0.45f, Quaternion.LookRotation(-side, Vector3.up), Vector3.one);
                return true;
            });
            f2.props.Add(l);
            f2.props.Add(r);
            JyTowerFx(at, dir, stand + 0.4f, true, lvl);
        }

        /// <summary>A little round tower (a corner's), its battlements up, a red flag on top if <paramref name="flag"/>;
        /// standing <paramref name="seconds"/>, then tumbling with the wall.</summary>
        void JyTowerFx(Vector3 at, Vector3 dir, float seconds, bool flag, float lvl)
        {
            var drum = CmProp(S4cCylinderMesh(), CmSand, "Corner tower", 0.016f);
            var cap = CmProp(S4cCylinderMesh(), CmKeepDark, "Corner tower top", 0.014f);
            var pole = CmProp(kit.RoundBox(new Vector3(0.05f, 1f, 0.05f), 0.02f), CmWood, "Flag pole", 0.008f);
            var cloth = CmProp(CmExtrude(new[] { new Vector2(0f, 0f), new Vector2(0.55f, 0.18f), new Vector2(0f, 0.36f) }, 0.02f, "Flag"), CmRed, "Flag", 0.008f);
            float high = (Params != null ? Params.jyWallHigh : 2f) + 0.6f;
            var f = kit.Run(seconds + 0.4f, (fx, d) =>
            {
                float rise = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.25f)), fall = Mathf.Clamp01((fx.age - seconds) / 0.35f);
                float h = high * rise * (1f - fall * fall);
                CmPlace(drum, at, Quaternion.identity, new Vector3(0.6f, Mathf.Max(0.02f, h), 0.6f));
                CmPlace(cap, at + Vector3.up * h, Quaternion.identity, new Vector3(0.72f, 0.22f * rise, 0.72f));
                if (flag)
                {
                    CmPlace(pole, at + Vector3.up * (h + 0.6f), Quaternion.identity, Vector3.one * Mathf.Max(0.02f, rise * (1f - fall)));
                    CmPlace(cloth, at + Vector3.up * (h + 0.75f), Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir), Vector3.up) * Quaternion.Euler(0f, 8f * Mathf.Sin(fx.age * 9f), 0f), Vector3.one * Mathf.Max(0.02f, rise * (1f - fall)));
                }
                else
                {
                    pole.Show(false);
                    cloth.Show(false);
                }
                return true;
            });
            f.props.Add(drum);
            f.props.Add(cap);
            f.props.Add(pole);
            f.props.Add(cloth);
        }

        // ---------------------------------------------------------------- 성벽 짓기

        sealed class JyBuildLook { public int phase; }
        readonly Dictionary<RagdollPawn, JyBuildLook> jyBuilds = new Dictionary<RagdollPawn, JyBuildLook>();

        void JyBuildFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            switch (e.count)
            {
                case 0:
                {
                    if (by == null) return;
                    var look = new JyBuildLook { phase = 0 };
                    jyBuilds[by] = look;
                    UltClip(UltClipId.Whoosh, 0.6f * lvl, 1.5f);   // ("슉")
                    JyBuildRunFx(by, look, lvl);
                    break;
                }
                case 1:
                    // A turn: a squeal, a little tower at the corner.
                    UltClip(UltClipId.Wah, 0.4f * lvl, 2.4f);   // ("끽!")
                    JyTowerFx(Ground(e.at + Vector3.up * 0.5f), FlatDirOr(e.dir), (Params != null ? Params.jyBuildSeconds : 5f) + 6f, false, lvl);
                    break;
                case 2:
                {
                    // Shut in: the floor under it flashes red, "!" over its head.
                    var who = e.target;
                    if (who == null) return;
                    kit.FlashRing(Ground(who.FeetPoint + Vector3.up * 0.4f) + Vector3.up * 0.03f, 1.2f, CmRed);
                    PopWord("!", DrawnHead(who) + Vector3.up * 0.7f, JyWordRed, 0.55f, 1.2f, who);
                    UltClip(UltClipId.Capture, 0.7f * lvl, 0.7f);   // ("철컹")
                    break;
                }
                default:
                {
                    if (by == null) return;
                    bool done = e.count == 3;
                    if (jyBuilds.TryGetValue(by, out var look)) look.phase = done ? 2 : 3;
                    Vector3 at = Ground(e.at + Vector3.up * 0.5f);
                    if (done)
                    {
                        // A flag on the last piece; the clone gone in sparkles.
                        JyTowerFx(at, FlatDirOr(e.dir), Params != null ? Params.jyBuildSeconds : 5f, true, lvl);
                        CmPoof(at + Vector3.up * 1f, CmTeal, 5, 0.8f, 0.24f, 0.5f, 0.5f);
                        UltClip(UltClipId.Promote, 0.6f * lvl, 1.3f);   // ("짠!")
                    }
                    else
                    {
                        CmPoof(at + Vector3.up * 1f, CmWhite, 4, 0.6f, 0.3f, 0.35f, 0.3f);
                        UltClip(UltClipId.Crack, 0.5f * lvl, 1.6f);   // ("펑")
                    }
                    break;
                }
            }
        }

        /// <summary>While building: the rook a grey statue with a hammer over it, the clone (a see-through rook, teal to its
        /// side, red to the other) gliding where the host says, dotted cells ahead of it.</summary>
        void JyBuildRunFx(RagdollPawn by, JyBuildLook look, float lvl)
        {
            var grey = JyGreyMaterial();
            bool ours = Viewer != null && Viewer.Team == by.Team && by.Team != Teams.None;
            var clone = CmProp(SkillMeshes.Piece(PieceKind.Rook), ours ? CmTeal : CmRed, "Wall clone", 0.025f);
            var handle = CmProp(kit.RoundBox(new Vector3(0.06f, 0.4f, 0.06f), 0.02f), CmWood, "Hammer handle", 0.01f);
            var head = CmProp(kit.RoundBox(new Vector3(0.28f, 0.14f, 0.14f), 0.03f), CmIron, "Hammer head", 0.01f);
            var cells = new Kit.Tile[3];
            for (int i = 0; i < cells.Length; i++) cells[i] = new Kit.Tile(kit, "Build cell");
            float size = 1f;
            var mesh = SkillMeshes.Piece(PieceKind.Rook);
            if (mesh != null) size = 1.7f / Mathf.Max(0.2f, mesh.bounds.size.y);
            var f = kit.Run(40f, (fx, d) =>
            {
                if (by == null || look.phase >= 2 || !by.JyBuilding && fx.age > 0.5f) return false;
                if (grey != null) WantGiant(by, 1f, 60f, grey);
                WantStretch(by, new Vector3(1.08f, 0.86f, 1.08f));   // (sat down)
                float k = GiantScaleOf(by);
                Vector3 h = DrawnHead(by) + Vector3.up * (0.7f * k);
                Quaternion tap = Quaternion.Euler(0f, fx.age * 60f, 25f * Mathf.Sin(fx.age * 8f));
                CmPlace(handle, h, tap, Vector3.one);
                CmPlace(head, h + tap * Vector3.up * 0.22f, tap, Vector3.one);
                // The clone, where the host has it.
                Vector3 c = by.PlanPoint, dir = FlatDirOr(by.PlanDir);
                clone.fade = 0.55f;
                CmPlace(clone, c, Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, 6f * Mathf.Sin(fx.age * 5f)), Vector3.one * size);
                if (Random.value < 0.25f) CmPoof(c + Vector3.up * 0.8f, ours ? CmTeal : CmRed, 1, 0.3f, 0.12f, 0.4f, 0.3f);
                for (int i = 0; i < cells.Length; i++)
                {
                    var t = cells[i];
                    t.Floor(Ground(c + dir * (1f + i) + Vector3.up * 0.8f) + Vector3.up * 0.03f, dir, new Vector2(0.6f, 0.9f));
                    t.shape = 0f;
                    t.fill = Color.clear;
                    t.ink = Kit.A(ours ? CmTeal.main : CmRed.main, 0.8f - 0.2f * i);
                    t.inkWidth = 0.04f;
                    t.core = Color.clear;
                    t.rim = Color.clear;
                    t.stripe = Color.clear;
                    t.fade = 1f;
                    t.Apply();
                }
                return true;
            });
            f.props.Add(clone);
            f.props.Add(handle);
            f.props.Add(head);
            foreach (var t in cells) f.tiles.Add(t);
        }

        void DestroySetFourJYC()
        {
            DestroySetFourFix();
            jyGiants.Clear();
            jyStrongs.Clear();
            jyTWalls.Clear();
            jyBuilds.Clear();
            jyRemoteLegs.Clear();
            if (PawnRushKitWall.OnCrumble == (System.Action<PawnRushKitWall, Vector3, Vector3>)JyWallCrumbleFx) PawnRushKitWall.OnCrumble = null;
        }
    }
}
