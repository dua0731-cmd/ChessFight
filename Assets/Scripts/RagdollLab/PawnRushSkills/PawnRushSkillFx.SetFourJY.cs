using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The user's own skills on every screen (Docs/Skills/PAWN_RUSH_SET4_USER_SKILLS.md 5장, 11-26; the user had found
    /// the earlier effects "좀 별로", so the planning session wrote each skill's beats: ready - out - hit - after). Bundle 1:
    /// the pawn's steps and rope, the knight's jump and stomp, and the flattened piece (납작이). The shared rules of 5.2
    /// live here too: one sound word at a time, beside the hit and out of the way of a speech bubble (JyWord); a hit's
    /// picture stop, two white frames on the body, one big cloud and a shake only near it (JyHit). Not seen in Unity yet.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        // ---------------------------------------------------------------- 5.2: words and hits

        sealed class JyWordState { public bool done; }
        JyWordState jyWord;
        RagdollPawn jyWordWho;
        float jyWordAt = -9f;
        Material jyWhite;

        /// <summary>The knight's horseshoe (5.2): dark gold with a glossy black edge.</summary>
        static readonly Kit.Palette CmShoeGold = new Kit.Palette("#A8842E", "#E2C268", "#6A4E14", "#0B0906");

        static readonly Color JyWordGold = Kit.Hex("#FFD23F"), JyWordRed = Kit.Hex("#FF6A3D"), JyWordWood = Kit.Hex("#F2C27A"),
                              JyWordWhite = Kit.Hex("#FFF6E0");

        /// <summary>A sound word (5.2): only one at a time (a new one ends the last), beside the spot and a little up (the
        /// speech bubbles sit over heads), popping to 120% in 0.1 s and settling at 100%, staying 0.5 s, going in 0.2 s; about
        /// 7% of the picture's height whatever the distance. It rides with <paramref name="who"/> if given.</summary>
        void JyWord(string word, Vector3 at, Color color, RagdollPawn who = null)
        {
            if (font == null || kit == null) return;
            if (jyWord != null) jyWord.done = true;
            var st = jyWord = new JyWordState();
            jyWordWho = who;
            jyWordAt = Clock;
            var text = new Text3D(root, font, word, color, 1f);
            float roll = Random.Range(-8f, 8f);
            Vector3 off = who != null ? at - who.Hips.position : Vector3.zero;
            var f = kit.Run(0.82f, (fx, d) =>
            {
                if (st.done) return false;
                var cam = ViewCamera;
                if (cam == null) return true;
                float t = fx.age;
                float pop = t < 0.1f ? 1.2f * Kit.EaseOut(t / 0.1f) : t < 0.18f ? Mathf.Lerp(1.2f, 1f, (t - 0.1f) / 0.08f) : 1f;
                float outk = Mathf.Clamp01((t - 0.6f) / 0.2f);
                Vector3 spot = who != null ? who.Hips.position + off : at;
                float dist = Mathf.Max(1f, Vector3.Distance(cam.transform.position, spot));
                float h = 0.07f * 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                Vector3 p = spot + cam.transform.right * (h * 1.5f) + cam.transform.up * (h * 0.8f);
                text.Place(p, cam, Mathf.Max(0.01f, h * pop * (1f - 0.2f * outk)), 1f - outk);
                text.root.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, roll);
                return true;
            });
            f.end = () => text.Destroy();
        }

        /// <summary>A word of a skill was just put on <paramref name="who"/> (its hindrance's own word stays quiet).</summary>
        bool JyWordedRecently(RagdollPawn who) => who != null && who == jyWordWho && Clock - jyWordAt < 0.35f;

        /// <summary>A hit (5.2): the picture stops (light 0.04 s, normal 0.06 s, strong 0.09 s; the world does not), the body
        /// flashes white two frames, one big cloud, and the camera shakes only for those near it.</summary>
        void JyHit(RagdollPawn who, Vector3 at, int strength, Kit.Palette cloud)
        {
            HitStop(strength >= 3 ? 0.09f : strength == 2 ? 0.06f : 0.04f);
            JyShake(at, strength >= 3 ? 0.16f : strength == 2 ? 0.08f : 0.04f);
            if (who != null) JyFlashWhite(who);
            CmPoof(at, cloud, 2 + strength, 0.5f + 0.25f * strength, 0.42f + 0.1f * strength, 0.5f, 0.35f);
        }

        /// <summary>A shake for those near: the camera within 14 m of the spot (the hitter, the hit and those beside them).</summary>
        void JyShake(Vector3 at, float amount)
        {
            var cam = ViewCamera;
            if (cam == null) return;
            float d = Vector3.Distance(cam.transform.position, at);
            if (d > 14f) return;
            Shake(amount * Mathf.Clamp01(1.4f - d / 14f), 8f * Frame);
        }

        void JyFlashWhite(RagdollPawn who)
        {
            if (jyWhite == null && LitMaterials.Lit != null)
            {
                jyWhite = LitMaterials.NewLit(Color.white, 0.2f, 0f);
                jyWhite.hideFlags = HideFlags.HideAndDontSave;
                if (jyWhite.HasProperty("_EmissionColor"))
                {
                    jyWhite.EnableKeyword("_EMISSION");
                    jyWhite.SetColor("_EmissionColor", Color.white * 0.8f);
                }
            }
            var white = jyWhite;
            if (white == null) return;
            kit.Run(2.2f * Frame, (fx, d) =>
            {
                if (who == null) return false;
                WantGiant(who, 1f, 60f, white);
                return true;
            });
        }

        /// <summary>A little pink bump on a head for a while (the headbutt's after).</summary>
        void JyBump(RagdollPawn who, float seconds)
        {
            if (who == null) return;
            var bump = CmProp(kit.meshBall, CmBump, "Bump", 0.01f);
            var f = kit.Run(seconds, (fx, d) =>
            {
                if (who == null) return false;
                float k = GiantScaleOf(who);
                float grow = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.15f)) * (1f - Mathf.Clamp01((fx.age - seconds + 0.2f) / 0.2f));
                Vector3 head = DrawnHead(who), up = HeadUp(who), face = FlatDir(who.Facing);
                CmPlace(bump, head + up * (0.16f * k) + face * (0.1f * k), Quaternion.identity, Vector3.one * (0.13f * k * Mathf.Max(0.02f, grow)));
                return true;
            });
            f.props.Add(bump);
        }

        // ---------------------------------------------------------------- the bundle's moments

        void SetFourJYFx(SkillFxEvent e, float lvl)
        {
            switch (e.kind)
            {
                case SkillFxKind.S4Flat: JyFlatPressFx(e, lvl); break;
                case SkillFxKind.JyStep: JyStepFx(e, lvl); break;
                case SkillFxKind.JyRope: JyRopeFx(e, lvl); break;
                case SkillFxKind.JyStomp: JyStompFx(e, lvl); break;
                default: SetFourJYBFx(e, lvl); break;
            }
        }

        // ---------------------------------------------------------------- 폰: 첫 두걸음+대각선 공격

        readonly Dictionary<RagdollPawn, int> jyCells = new Dictionary<RagdollPawn, int>();
        readonly Dictionary<RagdollPawn, float> jyCellsUntil = new Dictionary<RagdollPawn, float>();

        void JyStepFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (by == null) return;
            switch (e.count)
            {
                case 0:
                {
                    // Ready: crouched 20%, a handful of dust off the back foot; then two steps, stretched forward, a square
                    // stamped where each lands, speed wedges behind.
                    float crouch = Params != null ? Params.jyStepCrouch : 0.1f, steps = Params != null ? Params.jyStepTime : 0.35f;
                    Vector3 dir = FlatDir(e.dir), feet = Ground(e.at);
                    kit.AddSquash(by, Kit.SquashKind.Down);
                    CmPoof(feet - dir * 0.3f + Vector3.up * 0.1f, CmDust, 3, 0.3f, 0.22f, 0.35f, 0.3f);
                    UltClip(UltClipId.Tock, 0.5f * lvl, 0.6f);   // ("끙": the nearest, a low knock)
                    bool one = false, two = false;
                    kit.Run(crouch + steps + 0.1f, (fx, d) =>
                    {
                        if (by == null) return false;
                        if (!one && fx.age >= crouch)
                        {
                            one = true;
                            kit.AddSquash(by, Kit.SquashKind.Spring);
                            PawnStepStamp(new SkillFxEvent { kind = SkillFxKind.PawnStep, by = by, at = by.FeetPoint, dir = dir, count = 1 });
                            CmStreak(by, S3Colors(by), steps, 0.28f, 0.22f);
                            UltClip(UltClipId.Tap, 0.75f * lvl, 1.05f);   // ("타닥": a wooden piece on the board, twice)
                        }
                        if (!two && fx.age >= crouch + steps * 0.55f)
                        {
                            two = true;
                            PawnStepStamp(new SkillFxEvent { kind = SkillFxKind.PawnStep, by = by, at = by.FeetPoint, dir = dir, count = 2 });
                            UltClip(UltClipId.Tap, 0.75f * lvl, 1.2f);
                        }
                        return true;
                    });
                    break;
                }
                case 1:
                    JyDiagCellsFx(by, Mathf.Max(0.2f, e.size), lvl);
                    break;
                case 2:
                    JyHeadbuttFx(e, lvl);
                    break;
                case 3:
                    jyCellsUntil[by] = Clock;
                    break;
                default:
                {
                    // The cells' enemies changed: a "ding" as one fills.
                    int was = jyCells.TryGetValue(by, out var w) ? w : 0, now = e.count - 10;
                    jyCells[by] = now;
                    if ((now & ~was) != 0) UltClip(UltClipId.Tick, 0.55f * lvl, 1.6f);
                    break;
                }
            }
        }

        /// <summary>The pawn's two capture squares (front left and front right) lit faint on the floor for the window; one with
        /// an enemy on it filled red, its edge beating.</summary>
        void JyDiagCellsFx(RagdollPawn by, float window, float lvl)
        {
            jyCells[by] = 0;
            jyCellsUntil[by] = Clock + window;
            var cellL = new Kit.Tile(kit, "Diagonal cell");
            var cellR = new Kit.Tile(kit, "Diagonal cell");
            var side = kit.SideColors(by);
            var f = kit.Run(window + 0.4f, (fx, d) =>
            {
                if (by == null) return false;
                float until = jyCellsUntil.TryGetValue(by, out var u) ? u : 0f;
                float outk = Mathf.Clamp01((Clock - until) / 0.25f);
                if (outk >= 1f) return false;
                int mask = jyCells.TryGetValue(by, out var m) ? m : 0;
                Vector3 feet = by.FeetPoint, face = FlatDir(by.Facing), right = Vector3.Cross(Vector3.up, face);
                float sq = 1.15f, pop = Kit.Pop(fx.age, 1.1f, 0.6f);
                for (int i = 0; i < 2; i++)
                {
                    var t = i == 0 ? cellL : cellR;
                    bool taken = (mask & (i == 0 ? 1 : 2)) != 0;
                    Vector3 at = Ground(feet + face * 1.2f + right * (i == 0 ? -1.2f : 1.2f) + Vector3.up * 0.6f) + Vector3.up * 0.02f;
                    t.Floor(at, face, Vector2.one * (sq * pop));
                    t.shape = 0f;
                    t.round = 0.08f;
                    t.fill = taken ? Kit.A(CmRed.main, 0.78f) : Kit.A(side.main, 0.18f);
                    t.ink = taken ? Kit.A(CmRed.ink, 1f) : Kit.A(side.ink, 0.7f);
                    t.inkWidth = taken ? 0.05f + 0.035f * (0.5f + 0.5f * Mathf.Sin(fx.age * 18f)) : 0.035f;
                    t.core = taken ? Kit.A(Color.white, 0.35f) : Color.clear;
                    t.coreWidth = 0.04f;
                    t.rim = Color.clear;
                    t.stripe = Color.clear;
                    t.fade = 1f - outk;
                    t.Apply();
                }
                return true;
            });
            f.tiles.Add(cellL);
            f.tiles.Add(cellR);
        }

        void JyHeadbuttFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            var who = e.target;
            Vector3 dir = FlatDir(e.dir), at = e.at;
            jyCellsUntil[by] = Clock;
            // The headbutt: stretched along the lunge, the forehead in front; the one hit bends and flies.
            kit.AddSquash(by, Kit.SquashKind.Bump);
            if (who != null) kit.AddSquash(who, Kit.SquashKind.Flatten, 0.12f);
            JyHit(who, at, 2, CmDust);
            JyWord("딱!", at, JyWordWood, who);
            UltClip(UltClipId.Tap, lvl, 0.75f);
            UltClip(UltClipId.Crack, 0.6f * lvl, 1.4f);
            // A big "x" (the capture's mark) on the square it was hit on, 0.6 s.
            Vector3 cell = Ground((who != null ? who.FeetPoint : at) + Vector3.up * 0.6f) + Vector3.up * 0.06f;
            var a = CmProp(kit.RoundBox(new Vector3(0.18f, 0.06f, 1.3f), 0.04f), CmRed, "Capture x", 0.016f);
            var b = CmProp(kit.RoundBox(new Vector3(0.18f, 0.06f, 1.3f), 0.04f), CmRed, "Capture x", 0.016f);
            var f = kit.Run(0.6f, (fx, d) =>
            {
                float s = Kit.Pop(fx.age, 1.15f, 0.2f) * (1f - Mathf.Clamp01((fx.age - 0.45f) / 0.15f));
                Quaternion yaw = Quaternion.LookRotation(dir, Vector3.up);
                CmPlace(a, cell, yaw * Quaternion.Euler(0f, 45f, 0f), Vector3.one * Mathf.Max(0.02f, s));
                CmPlace(b, cell, yaw * Quaternion.Euler(0f, -45f, 0f), Vector3.one * Mathf.Max(0.02f, s));
                return true;
            });
            f.props.Add(a);
            f.props.Add(b);
            // After: a bump on both heads; the pawn shakes its head.
            JyBump(by, 1.6f);
            if (who != null) JyBump(who, 2f);
            UltClip(UltClipId.Wah, 0.35f * lvl, 1.7f, 0.25f);   // ("아야")
            kit.Run(0.4f, (fx, d) =>
            {
                if (by == null) return false;
                if (fx.age > 0.1f) WantPose(by, Quaternion.AngleAxis(14f * Mathf.Sin((fx.age - 0.1f) * 40f) * (1f - fx.age / 0.4f), Vector3.up), false);
                return true;
            });
        }

        // ---------------------------------------------------------------- 폰: 앙파상-밧줄

        sealed class JyRopeLook
        {
            public int phase;            // 0 held, 1 swinging, 2 flying, 3 caught, 4 down (missed), 5 gone
            public float swingAt, flyAt, endAt;
            public Vector3 from, v0, knot;
            public RagdollPawn caught;
        }
        readonly Dictionary<RagdollPawn, JyRopeLook> jyRopes = new Dictionary<RagdollPawn, JyRopeLook>();

        void JyRopeFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (by == null) return;
            jyRopes.TryGetValue(by, out var look);
            switch (e.count)
            {
                case 0:
                    UltClip(UltClipId.Whoosh, 0.4f * lvl, 1.8f);   // ("촥")
                    JyRopeHeldFx(by);
                    break;
                case 1:
                    if (look != null) look.phase = 5;
                    CmPoof(by.bodies[(int)BodyId.HandR].position, CmDust, 2, 0.2f, 0.16f, 0.3f, 0.2f);
                    break;
                case 2:
                    if (look != null)
                    {
                        look.phase = 1;
                        look.swingAt = Clock;
                    }
                    break;
                case 3:
                    if (look == null) break;
                    look.phase = 2;
                    look.flyAt = Clock;
                    look.from = e.at;
                    look.v0 = FlatDirOr(e.dir) * Mathf.Max(1f, e.size);
                    kit.AddSquash(by, Kit.SquashKind.Spring);
                    UltClip(UltClipId.Whoosh, 0.8f * lvl, 0.9f);   // ("휘익—")
                    break;
                case 4:
                {
                    if (look == null) break;
                    look.phase = 3;
                    look.caught = e.target;
                    look.knot = e.at;
                    // Caught: the knot round the waist, a pinch, the rope taut with a shiver.
                    if (e.target != null) kit.AddSquash(e.target, Kit.SquashKind.Wide, 0.1f);
                    JyWord("착!", e.at, JyWordWood, e.target);
                    UltClip(UltClipId.Tap, 0.7f * lvl, 1.6f);
                    UltClip(UltClipId.Tick, 0.45f * lvl, 2f, 0.05f);   // ("팅")
                    UltClip(UltClipId.Hum, 0.5f * lvl, 0.6f, 0.1f);    // ("드드드득", dragged)
                    break;
                }
                case 5:
                {
                    // The chop: a big hand comes down on the neck; eyes go X, flat out; the pawn walks on dusting its hands.
                    if (look != null) look.phase = 5;
                    var who = e.target;
                    JyChopFx(by, who, e.at, FlatDir(e.dir), lvl);
                    break;
                }
                case 6:
                    if (look == null) break;
                    look.phase = 4;
                    look.knot = e.at;
                    look.endAt = Clock;
                    UltClip(UltClipId.Tap, 0.4f * lvl, 2.2f);
                    UltClip(UltClipId.Tap, 0.3f * lvl, 2.4f, 0.18f);   // ("톡, 토독")
                    break;
            }
        }

        static Vector3 FlatDirOr(Vector3 v) => v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;

        /// <summary>The rope from the pawn's right hand, as long as it is out: a coil and a knot held; swung round over the
        /// head, the circle growing 0.8 m to 1.4 m with a ribbon behind and one white flash when full; thrown, the knot ahead
        /// on its lob with the line waving out behind; caught, taut to the waist; missed, the knot bouncing twice and the line
        /// coiled back in.</summary>
        void JyRopeHeldFx(RagdollPawn by)
        {
            var look = new JyRopeLook { phase = 0 };
            jyRopes[by] = look;
            float spin = Params != null ? Mathf.Max(0.1f, Params.jyRopeSpin) : 1f;
            var coil = CmProp(kit.meshTorus, HempRope, "Rope coil", 0.012f);
            var knot = CmProp(kit.meshBall, HempRope, "Rope knot", 0.014f);
            var span = new RopeSpan(this, "Rope line");
            var ribbon = new Kit.Strip(kit, HempRope) { core = HempRope.light, coreShare = 0.4f, inkShare = 0.12f };
            var pts = new List<Vector3>(24);
            bool flashed = false;
            int bounces = 0;
            float lastWhoosh = -1f;
            var landing = new Kit.Tile(kit, "Rope landing");
            var f = kit.Run(30f, (fx, d) =>
            {
                if (by == null || look.phase == 5)
                {
                    span.Place(Vector3.zero, Vector3.zero, 0f, 0f);
                    return false;
                }
                Vector3 hand = by.bodies[(int)BodyId.HandR].position;
                Vector3 head = DrawnHead(by);
                // My own screen only: a cream ring where a throw now would come down.
                if (by == Viewer && look.phase <= 1 && ViewCamera != null && JyRopeLandingNow(by, look, spin, out var down))
                {
                    landing.Floor(down + Vector3.up * 0.03f, Vector3.forward, Vector2.one * 0.9f);
                    landing.shape = 1f;
                    landing.inner = 0.78f;
                    landing.fill = Kit.A(Kit.Hex("#FFF1C8"), 0.9f);
                    landing.ink = Kit.A(HempRope.ink, 0.8f);
                    landing.inkWidth = 0.03f;
                    landing.core = Color.clear;
                    landing.rim = Color.clear;
                    landing.stripe = Color.clear;
                    landing.fade = 1f;
                    landing.Apply();
                }
                else
                {
                    landing.fade = 0f;
                    landing.Apply();
                }
                switch (look.phase)
                {
                    case 0:
                    {
                        // Held: a coil at the hand, the knot hanging under it.
                        CmPlace(coil, hand + Vector3.down * 0.05f, Quaternion.LookRotation(FlatDir(by.Facing), Vector3.up), Vector3.one * 0.2f);
                        Vector3 k = hand + Vector3.down * (0.32f + 0.03f * Mathf.Sin(fx.age * 5f));
                        CmPlace(knot, k, Quaternion.identity, Vector3.one * 0.15f);
                        span.Place(hand, k, 0.02f, 1f);
                        ribbon.Hide();
                        break;
                    }
                    case 1:
                    {
                        // Swinging over the head: faster and wider as it charges; a ribbon trails the knot.
                        float c = Mathf.Clamp01((Clock - look.swingAt) / spin);
                        float r = Mathf.Lerp(0.4f, 0.7f, c), speed = Mathf.Lerp(7f, 16f, c);
                        float a = (Clock - look.swingAt) * speed;
                        Vector3 centre = head + Vector3.up * 0.45f;
                        Vector3 k = centre + new Vector3(Mathf.Cos(a), 0.05f * Mathf.Sin(a * 2f), Mathf.Sin(a)) * r;
                        CmPlace(knot, k, Quaternion.identity, Vector3.one * 0.15f);
                        coil.Show(false);
                        span.Place(hand, k, 0.03f, 1f);
                        pts.Clear();
                        for (int i = 0; i <= 10; i++)
                        {
                            float b = a - i * 0.18f;
                            pts.Add(centre + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * r);
                        }
                        ribbon.Build(pts, i => 0.12f * (1f - i / 10f), kit.Eye);
                        ribbon.fade = 0.55f;
                        ribbon.Apply();
                        if (Clock - lastWhoosh > Mathf.Lerp(0.32f, 0.12f, c))
                        {
                            lastWhoosh = Clock;
                            UltClip(UltClipId.Whoosh, 0.35f * S3Heard(centre), Mathf.Lerp(1.2f, 2f, c));   // ("휙, 휙, 휙", faster and higher)
                        }
                        if (!flashed && c >= 1f)
                        {
                            flashed = true;
                            kit.FlashRing(centre, r + 0.15f, CmWhite);
                            UltClip(UltClipId.Tick, 0.6f * S3Heard(centre), 1.8f);   // ("띵")
                        }
                        break;
                    }
                    case 2:
                    {
                        // Flying: the knot on its lob, the line waving out from the hand.
                        float t = Clock - look.flyAt;
                        Vector3 k = look.from + look.v0 * t + Vector3.down * (0.5f * Mathf.Max(0.01f, -Physics.gravity.y) * t * t);
                        look.knot = k;
                        CmPlace(knot, k, Quaternion.Euler(t * 700f, 0f, 0f), Vector3.one * 0.17f);
                        span.Place(hand, k, Mathf.Lerp(0.6f, 0.15f, Mathf.Clamp01(t / 0.4f)) * (1f + 0.3f * Mathf.Sin(t * 30f)), 1f);
                        ribbon.Hide();
                        if (t > 3.5f) look.phase = 5;
                        break;
                    }
                    case 3:
                    {
                        // Caught: taut to the waist, two turns round it, white shiver lines.
                        var who = look.caught;
                        if (who == null)
                        {
                            look.phase = 5;
                            break;
                        }
                        Vector3 waist = who.Hips.position + Vector3.up * 0.05f;
                        CmPlace(knot, waist + FlatDir(hand - waist) * 0.3f, Quaternion.identity, Vector3.one * 0.15f);
                        CmPlace(coil, waist, Quaternion.identity, new Vector3(0.36f, 0.18f, 0.36f) * GiantScaleOf(who));
                        span.Place(hand, waist + FlatDir(hand - waist) * 0.3f, 0.01f, 1f);
                        // Heels dug in: dust out of its feet as it is dragged.
                        if (Random.value < 0.45f) CmPoof(who.FeetPoint + Vector3.up * 0.05f, CmDust, 1, 0.12f, 0.16f, 0.4f, 0.1f);
                        pts.Clear();
                        Vector3 mid = Vector3.Lerp(hand, waist, 0.5f), across = Vector3.Cross(FlatDirOr(waist - hand), Vector3.up);
                        for (int i = 0; i < 4; i++) pts.Add(mid + across * ((i % 2 == 0 ? 1f : -1f) * 0.08f) + (waist - hand) * (i * 0.04f - 0.06f));
                        ribbon.Build(pts, i => 0.025f, kit.Eye);
                        ribbon.fade = 0.5f + 0.5f * Mathf.Sin(fx.age * 50f);
                        ribbon.Apply();
                        break;
                    }
                    case 4:
                    {
                        // Missed: the knot drops, bounces twice ("톡, 토독"), and is coiled back in over a second.
                        float t = Clock - look.endAt;
                        float hop = bounces < 2 ? Mathf.Abs(Mathf.Sin(t * 10f)) * 0.25f * (1f - t * 2f) : 0f;
                        if (t > 0.1f * (bounces + 1) && bounces < 2) bounces++;
                        float back = Mathf.Clamp01((t - 0.3f) / 0.8f);
                        Vector3 k = Vector3.Lerp(look.knot + Vector3.up * Mathf.Max(0f, hop), hand, back * back);
                        CmPlace(knot, k, Quaternion.identity, Vector3.one * 0.15f);
                        span.Place(hand, k, 0.15f * (1f - back), 1f);
                        ribbon.Hide();
                        if (back >= 1f) look.phase = 5;
                        break;
                    }
                }
                return true;
            });
            f.props.Add(coil);
            f.props.Add(knot);
            f.strips.Add(ribbon);
            f.tiles.Add(landing);
            f.end = () =>
            {
                span.Destroy();
                if (jyRopes.TryGetValue(by, out var l) && l == look) jyRopes.Remove(by);
            };
        }

        /// <summary>Where the rope would come down now (the thrower's own PC: its camera's way, the swing so far), as the
        /// host works it out (RagdollPawn.JyRopeLob: 35° up from the hand, 4 m to 10 m).</summary>
        bool JyRopeLandingNow(RagdollPawn by, JyRopeLook look, float spin, out Vector3 down)
        {
            down = default;
            var cam = ViewCamera;
            if (cam == null || Params == null) return false;
            float c = look.phase == 1 ? Mathf.Clamp01((Clock - look.swingAt) / spin) : 0f;
            float range = Mathf.Lerp(Params.jyRopeShort, Params.jyRopeLong, c);
            Vector3 hand = by.bodies[(int)BodyId.Chest].position + Vector3.up * 0.55f, aim = FlatDirOr(Flat(cam.transform.forward));
            float angle = 35f * Mathf.Deg2Rad, g = Mathf.Max(0.01f, -Physics.gravity.y), h = hand.y - by.FeetPoint.y;
            float cs = Mathf.Cos(angle), v = Mathf.Sqrt(g * range * range / Mathf.Max(0.1f, 2f * cs * cs * (range * Mathf.Tan(angle) + h)));
            Vector3 v0 = aim * (v * cs) + Vector3.up * (v * Mathf.Sin(angle)), p = hand;
            for (int i = 1; i <= 40; i++)
            {
                float t = i * 0.05f;
                Vector3 q = hand + v0 * t + Vector3.down * (0.5f * g * t * t);
                if (Physics.Linecast(p, q, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider.attachedRigidbody == null)
                {
                    down = hit.point;
                    return true;
                }
                p = q;
            }
            down = p;
            return true;
        }

        void JyChopFx(RagdollPawn by, RagdollPawn who, Vector3 neck, Vector3 dir, float lvl)
        {
            // The hand blade, big (1.5x), up a moment and down.
            var blade = CmProp(kit.RoundBox(new Vector3(0.1f, 0.42f, 0.26f), 0.05f), CmWhite, "Hand blade", 0.016f);
            var f = kit.Run(0.3f, (fx, d) =>
            {
                float t = fx.age / 0.3f;
                float lift = t < 0.35f ? Kit.EaseOut(t / 0.35f) : 1f - Kit.EaseIn((t - 0.35f) / 0.2f);
                Vector3 at = neck + Vector3.up * (0.25f + 0.45f * Mathf.Clamp01(lift)) - dir * 0.15f;
                CmPlace(blade, at, Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(-40f * Mathf.Clamp01(lift), 0f, 0f), Vector3.one * 1.5f * (1f - Mathf.Clamp01((t - 0.8f) / 0.2f)));
                return true;
            });
            f.props.Add(blade);
            JyHit(who, neck, 3, CmDust);
            JyWord("탁!", neck, JyWordGold, who);
            UltClip(UltClipId.Crack, lvl, 1.2f);
            UltClip(UltClipId.Tap, 0.8f * lvl, 0.7f);
            if (who != null) JyXEyes(who, Params != null ? Params.jyRopeFlop : 1.5f);
            // The pawn goes on dusting its hands (a little puff at each).
            if (by != null)
            {
                kit.Run(0.5f, (fx, d) =>
                {
                    if (by == null) return false;
                    if (Random.value < 0.25f)
                    {
                        CmPoof(by.bodies[(int)BodyId.HandL].position, CmDust, 1, 0.1f, 0.12f, 0.3f, 0.2f);
                        CmPoof(by.bodies[(int)BodyId.HandR].position, CmDust, 1, 0.1f, 0.12f, 0.3f, 0.2f);
                    }
                    return true;
                });
                UltClip(UltClipId.Tap, 0.35f * lvl, 1.9f, 0.3f);
                UltClip(UltClipId.Tap, 0.35f * lvl, 2f, 0.42f);
            }
        }

        /// <summary>Its eyes gone to X and the tongue out, for <paramref name="seconds"/> (out cold).</summary>
        void JyXEyes(RagdollPawn who, float seconds)
        {
            var parts = new Kit.Prop[5];
            for (int i = 0; i < 4; i++) parts[i] = CmProp(kit.RoundBox(new Vector3(0.025f, 0.025f, 0.12f), 0.01f), CmIron, "X eye", 0.006f);
            parts[4] = CmProp(kit.meshBall, CmBump, "Tongue", 0.008f);
            var f = kit.Run(seconds, (fx, d) =>
            {
                if (who == null) return false;
                float k = GiantScaleOf(who);
                var head = who.bodies[(int)BodyId.Head];
                Vector3 c = DrawnHead(who), fwd = head.rotation * Vector3.forward, up = head.rotation * Vector3.up, side = head.rotation * Vector3.right;
                for (int e = 0; e < 2; e++)
                {
                    Vector3 eye = c + fwd * (0.17f * k) + side * ((e == 0 ? -0.07f : 0.07f) * k) + up * (0.03f * k);
                    Quaternion q = Quaternion.LookRotation(side, fwd);
                    CmPlace(parts[e * 2], eye, q * Quaternion.Euler(0f, 0f, 0f) * Quaternion.AngleAxis(45f, fwd), Vector3.one * k);
                    CmPlace(parts[e * 2 + 1], eye, q * Quaternion.AngleAxis(-45f, fwd), Vector3.one * k);
                }
                CmPlace(parts[4], c + fwd * (0.16f * k) - up * (0.09f * k), Quaternion.LookRotation(fwd, up), new Vector3(0.06f, 0.025f, 0.09f) * k);
                return true;
            });
            foreach (var p in parts) f.props.Add(p);
        }

        // ---------------------------------------------------------------- 나이트: 점프&밟기

        sealed class JyMark { public RagdollPawn who; public bool lit; public float born; }
        readonly Dictionary<RagdollPawn, List<JyMark>> jyMarks = new Dictionary<RagdollPawn, List<JyMark>>();

        void JyStompFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            switch (e.count)
            {
                case 0:
                    // Ready: up on its hind legs a moment, then crouched 30%.
                    if (by == null) return;
                    UltClip(UltClipId.Wah, 0.55f * lvl, 1.9f);   // ("히힝!", the nearest)
                    kit.Run(Mathf.Max(0.1f, e.size), (fx, d) =>
                    {
                        if (by == null) return false;
                        float t = fx.age / Mathf.Max(0.1f, e.size);
                        Vector3 right = Vector3.Cross(Vector3.up, FlatDir(by.Facing));
                        WantPose(by, Quaternion.AngleAxis(-22f * Mathf.Sin(Mathf.Clamp01(t * 1.6f) * Mathf.PI), right), false);
                        return true;
                    });
                    kit.AddSquash(by, Kit.SquashKind.Down, 0.05f);
                    break;
                case 1:
                    // Up: stretched, the force lines and dust off the hooves (the knight's own take-off).
                    if (by == null) return;
                    kit.AddSquash(by, Kit.SquashKind.Spring);
                    KnightTakeOff(new SkillFxEvent { kind = SkillFxKind.KnightLeap, by = by, at = e.at, dir = e.dir, count = 0 });
                    UltClip(UltClipId.Whoosh, 0.7f * lvl, 0.8f);   // ("슈웅")
                    jyMarks[by] = new List<JyMark>();
                    break;
                case 2:
                {
                    if (by == null || e.target == null) return;
                    if (!jyMarks.TryGetValue(by, out var list)) jyMarks[by] = list = new List<JyMark>();
                    var mark = list.Find(m => m.who == e.target);
                    if (e.size <= 1.5f)
                    {
                        foreach (var m in list) m.lit = false;
                        if (mark != null) mark.lit = true;
                    }
                    if (mark == null)
                    {
                        mark = new JyMark { who = e.target, lit = e.size <= 1.5f, born = Clock };
                        list.Add(mark);
                        JyHorseshoeMarkFx(by, mark);
                        UltClip(UltClipId.Tick, 0.5f * lvl, 2.2f);   // ("딸깍")
                    }
                    break;
                }
                case 3:
                {
                    // The dive: curled, once round, hooves down, five force lines on the way.
                    if (by == null) return;
                    float dive = Mathf.Max(0.1f, e.size);
                    UltClip(UltClipId.Whoosh, 0.8f * lvl, 1.3f);   // ("휘이익")
                    kit.Run(dive, (fx, d) =>
                    {
                        if (by == null) return false;
                        Vector3 right = Vector3.Cross(Vector3.up, FlatDir(by.Facing));
                        WantPose(by, Quaternion.AngleAxis(360f * Mathf.SmoothStep(0f, 1f, fx.age / dive), right), true);
                        return true;
                    });
                    CmStreak(by, CmShoe, dive + 0.1f, 0.24f, 0.2f);
                    break;
                }
                case 4:
                {
                    // Stomped: pressed out into a pancake (the 납작이 look takes it on), a horseshoe stamped on its back.
                    var who = e.target;
                    JyHit(who, e.at, 3, CmDust);
                    JyWord("뿌직!", e.at, JyWordRed, who);
                    UltClip(UltClipId.Crack, lvl, 0.9f);
                    UltClip(UltClipId.SubDrop, 0.6f * lvl, 1.4f);
                    if (by != null && jyMarks.TryGetValue(by, out var list)) list.Clear();
                    break;
                }
                case 5:
                {
                    // The fork: a black and gold ring out along the floor to its radius.
                    float r = Mathf.Max(0.5f, e.size);
                    Vector3 c = Ground(e.at + Vector3.up * 0.3f) + Vector3.up * 0.04f;
                    var ring = new Kit.Tile(kit, "Fork ring");
                    var f = kit.Run(0.55f, (fx, d) =>
                    {
                        float t = fx.age / 0.55f, grow = Kit.EaseOut(Mathf.Clamp01(t / 0.6f));
                        ring.Floor(c, Vector3.forward, Vector2.one * (2f * r * Mathf.Max(0.05f, grow)));
                        ring.shape = 1f;
                        ring.inner = 0.86f;
                        ring.fill = Kit.A(CmShoe.main, 0.9f);
                        ring.core = Kit.A(CmGold.main, 0.95f);
                        ring.coreWidth = 0.05f;
                        ring.ink = Kit.A(CmShoe.ink, 1f);
                        ring.inkWidth = 0.04f;
                        ring.rim = Color.clear;
                        ring.stripe = Color.clear;
                        ring.fade = 1f - Mathf.Clamp01((t - 0.7f) / 0.3f);
                        ring.Apply();
                        return true;
                    });
                    f.tiles.Add(ring);
                    UltClip(UltClipId.Capture, 0.6f * lvl, 1.4f);   // ("철컥, 철컥")
                    break;
                }
                case 6:
                    JyShackleFx(e.target, Mathf.Max(0.3f, e.size));
                    UltClip(UltClipId.Capture, 0.45f * lvl, 1.6f, 0.06f);
                    break;
                case 7:
                {
                    // Down: a horseshoe stamped in the floor where it lands.
                    if (by != null && jyMarks.TryGetValue(by, out var list)) list.Clear();
                    Vector3 c = Ground(e.at + Vector3.up * 0.3f) + Vector3.up * 0.03f;
                    var shoe = CmProp(SkillMeshes.Horseshoe(), CmShoe, "Landing shoe", 0.012f);
                    var f = kit.Run(1.2f, (fx, d) =>
                    {
                        float s = Kit.Pop(fx.age, 1.15f, 0.2f) * (1f - Mathf.Clamp01((fx.age - 0.9f) / 0.3f));
                        CmPlace(shoe, c, Quaternion.LookRotation(FlatDirOr(e.dir), Vector3.up), new Vector3(0.6f, 0.15f, 0.6f) * Mathf.Max(0.02f, s));
                        return true;
                    });
                    f.props.Add(shoe);
                    CmPoof(c + Vector3.up * 0.1f, CmDust, 2, 0.4f, 0.24f, 0.4f, 0.2f);
                    UltClip(UltClipId.Tap, 0.6f * lvl, 1.3f);   // ("착")
                    break;
                }
            }
        }

        /// <summary>A horseshoe over a marked head (glossy black rim, dark gold), 0.7 m across, turning slowly 0.4 m over it;
        /// the lit one 1.3 times as big, a red ring beating round it and a red cell under its feet.</summary>
        void JyHorseshoeMarkFx(RagdollPawn by, JyMark mark)
        {
            var shoe = CmProp(SkillMeshes.Horseshoe(), CmShoeGold, "Stomp mark", 0.02f);
            var ring = new Kit.Strip(kit, CmRed) { core = CmRed.light, coreShare = 0.3f, inkShare = 0.3f };
            var cell = new Kit.Tile(kit, "Stomp cell");
            var pts = new List<Vector3>(25);
            var f = kit.Run(6f, (fx, d) =>
            {
                var who = mark.who;
                if (by == null || who == null || !jyMarks.TryGetValue(by, out var list) || !list.Contains(mark)) return false;
                float k = GiantScaleOf(who), pop = Kit.Pop(Clock - mark.born, 1.2f, 0.2f);
                float size = 0.7f * (mark.lit ? 1.3f : 1f) * pop;
                Vector3 c = DrawnHead(who) + Vector3.up * (0.4f + 0.2f * k);
                var cam = ViewCamera;
                Quaternion facing = cam != null ? Quaternion.FromToRotation(Vector3.up, -cam.transform.forward) : Quaternion.Euler(-60f, 0f, 0f);
                CmPlace(shoe, c, facing * Quaternion.Euler(0f, 180f + 20f * Mathf.Sin(fx.age * 2.2f), 0f), Vector3.one * (size * 0.85f));
                if (mark.lit)
                {
                    float beat = 1f + 0.12f * Mathf.Sin(fx.age * 14f);
                    pts.Clear();
                    for (int i = 0; i <= 24; i++)
                    {
                        float a = i * Mathf.PI * 2f / 24f;
                        pts.Add(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (size * 0.62f * beat));
                    }
                    ring.Build(pts, i => 0.05f, kit.Eye);
                    ring.Apply();
                    cell.Floor(Ground(who.FeetPoint + Vector3.up * 0.4f) + Vector3.up * 0.02f, FlatDir(by.Facing), Vector2.one * 1.1f);
                    cell.shape = 0f;
                    cell.fill = Kit.A(CmRed.main, 0.5f);
                    cell.ink = Kit.A(CmRed.ink, 0.9f);
                    cell.inkWidth = 0.04f;
                    cell.stripe = Kit.A(Color.white, 0.25f);
                    cell.stripePhase = fx.age * 1.5f;
                    cell.core = Color.clear;
                    cell.rim = Color.clear;
                    cell.fade = 1f;
                    cell.Apply();
                }
                else
                {
                    ring.Hide();
                    cell.fade = 0f;
                    cell.Apply();
                }
                return true;
            });
            f.props.Add(shoe);
            f.strips.Add(ring);
            f.tiles.Add(cell);
        }

        /// <summary>The fork's shackle: a horseshoe-shaped iron round its ankles, staked into the floor, as long as it is tied.</summary>
        void JyShackleFx(RagdollPawn who, float seconds)
        {
            if (who == null) return;
            var shoe = CmProp(SkillMeshes.Horseshoe(), CmShoe, "Fork shackle", 0.014f);
            var f = kit.Run(seconds, (fx, d) =>
            {
                if (who == null) return false;
                float s = Kit.Pop(fx.age, 1.2f, 0.2f) * (1f - Mathf.Clamp01((fx.age - seconds + 0.2f) / 0.2f));
                Vector3 c = Ground(who.FeetPoint + Vector3.up * 0.3f) + Vector3.up * 0.12f;
                CmPlace(shoe, c, Quaternion.LookRotation(FlatDir(who.Facing), Vector3.up) * Quaternion.Euler(-80f, 0f, 0f), Vector3.one * (0.5f * Mathf.Max(0.02f, s)));
                return true;
            });
            f.props.Add(shoe);
        }

        // ---------------------------------------------------------------- 납작이 (5.4)

        readonly Dictionary<RagdollPawn, int> jyFlatPresses = new Dictionary<RagdollPawn, int>();

        /// <summary>Pressed into a flat drum (25% high, 150% wide; it came out at 180% and wobbled in): its eyes up on the top,
        /// a horseshoe stamped on its back, waddling as it walks, dipping 5% more when someone stands on it; each jump press
        /// puffs it up 20%.</summary>
        void CcFlatLook(CcLook look, float lvl)
        {
            var who = look.who;
            jyFlatPresses[who] = 0;
            var eyes = new Kit.Prop[4];
            for (int i = 0; i < 2; i++)
            {
                eyes[i] = CmProp(kit.meshBall, CmWhite, "Flat eye", 0.008f);
                eyes[i + 2] = CmProp(kit.meshBall, CmIron, "Flat pupil", 0.004f);
            }
            var stamp = CmProp(SkillMeshes.Horseshoe(), CmShoe, "Flat stamp", 0.01f);
            float born = Clock, high = Params != null ? Params.flatHigh : 0.25f, wide = Params != null ? Params.flatWide : 1.5f;
            int need = Params != null ? Mathf.Max(1, Params.flatPresses) : 5;
            float dipUntil = -1f, baseH = -1f;
            var f = kit.Run(Mathf.Max(0.3f, look.until - Clock) + 0.2f, (fx, d) =>
            {
                if (who == null || Clock > look.until) return false;
                int presses = jyFlatPresses.TryGetValue(who, out var pr) ? pr : 0;
                float t = Clock - born;
                float entry = t < 0.25f ? Mathf.Lerp(1.8f, wide, Kit.EaseOutBack(t / 0.25f)) : wide;
                float share = Mathf.Clamp01((float)presses / need);
                float h = Mathf.Lerp(high, 0.9f, share * 0.8f), w = Mathf.Lerp(entry, 1.1f, share * 0.8f);
                // Someone on its top: a dip.
                foreach (var o in RagdollPawn.All)
                {
                    if (o == null || o == who) continue;
                    Vector3 off = o.FeetPoint - who.FeetPoint;
                    if (Flat(off).magnitude < 0.55f * w && off.y > 0.1f && off.y < 0.9f) dipUntil = Clock + 0.15f;
                }
                if (Clock < dipUntil) h *= 0.95f;
                WantStretch(who, new Vector3(w, h, w));
                if (who.HorizontalSpeed > 0.8f) WantPose(who, Quaternion.AngleAxis(7f * Mathf.Sin(t * 11f), FlatDir(who.Facing)), false);
                if (baseH < 0f) baseH = Mathf.Max(0.8f, GiantBaseHeight(who));
                Vector3 feet = who.FeetPoint, face = FlatDir(who.Facing), right = Vector3.Cross(Vector3.up, face);
                float top = baseH * h;
                bool blink = Mathf.Repeat(t, 2.6f) > 2.45f;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 eye = feet + Vector3.up * (top + 0.04f) + face * (0.16f * w) + right * ((i == 0 ? -0.11f : 0.11f) * w);
                    CmPlace(eyes[i], eye, Quaternion.identity, new Vector3(0.12f, blink ? 0.015f : 0.1f, 0.12f));
                    CmPlace(eyes[i + 2], eye + Vector3.up * 0.035f + face * 0.02f, Quaternion.identity, new Vector3(0.05f, blink ? 0.01f : 0.05f, 0.05f));
                }
                CmPlace(stamp, feet + Vector3.up * (top + 0.02f) - face * (0.1f * w), Quaternion.LookRotation(face, Vector3.up), new Vector3(0.45f, 0.12f, 0.45f) * w);
                return true;
            });
            foreach (var p in eyes) f.props.Add(p);
            f.props.Add(stamp);
            CmPoof(who.FeetPoint + Vector3.up * 0.1f, CmDust, 3, 0.6f, 0.3f, 0.4f, 0.15f);
        }

        /// <summary>A jump press on a flattened piece: "뿍", puffed up 20% more; the last pops it back with a turn ("뽕!").</summary>
        void JyFlatPressFx(SkillFxEvent e, float lvl)
        {
            var who = e.target != null ? e.target : e.by;
            if (who == null) return;
            int need = Mathf.Max(1, Mathf.RoundToInt(e.size));
            jyFlatPresses[who] = e.count;
            if (e.count < need)
            {
                kit.AddSquash(who, Kit.SquashKind.Bump);
                UltClip(UltClipId.Tap, 0.5f * lvl, 1.4f + 0.1f * e.count);   // ("뿍")
                return;
            }
            JyWord("뽕!", who.Hips.position, JyWordWhite, who);
            CmPoof(who.Hips.position, CmWhite, 4, 0.6f, 0.3f, 0.4f, 0.4f);
            UltClip(UltClipId.Swell, 0.6f * lvl, 1.6f);
            kit.Run(0.35f, (fx, d) =>
            {
                if (who == null) return false;
                WantPose(who, Quaternion.AngleAxis(360f * Kit.EaseOut(fx.age / 0.35f), Vector3.up), false);
                return true;
            });
        }

        void DestroySetFourJY()
        {
            jyRopes.Clear();
            jyMarks.Clear();
            jyCells.Clear();
            jyCellsUntil.Clear();
            jyFlatPresses.Clear();
            if (jyWhite != null) Destroy(jyWhite);
            jyWhite = null;
            DestroySetFourJYB();
        }
    }
}
