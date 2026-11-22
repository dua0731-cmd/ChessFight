using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
#if CF_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Time and the hard frames of the ultimates (JY-skill-lab, 2026-11-22; the user: "임팩트 프레임 + 시전자한텐 스킬 발동이
    /// 슬로우로 더 천천히 연출"; the design: the "!! Brilliant Move" spec, sections 2 and 9).
    ///
    ///   slow motion    the caster's own screen only. The REAL path (this PC is the whole game with one player at it: the
    ///                  skill test scene) slows the game's own time (Time.timeScale × the lab's own rate; the physics step
    ///                  is left as it is - the flights count physics steps - and the bodies are interpolated); the PICTURE
    ///                  path (online, or two players on one PC) slows only this screen's show clock and holds pictures for
    ///                  at most 0.12 s. Curves in real seconds: the key (a freeze, a float, a stamp, the snap back), at most
    ///                  one signature (an anticipation held until its impact, or an aftermath), the !! judgement. A low-pass
    ///                  on the listener and lower ultimate sounds while it is slow; the caster's camera dolly-zooms, rolls and
    ///                  kicks (never its direction).
    ///   impact frames  a few frames of a hard look over the whole picture, each held at least 1/60 s real and one rendered
    ///                  frame: INK (two-tone, only teal and red keep colour), INK-T (only teal), NEG (the negative), SIN
    ///                  (black-and-white, red kept), TEAL / RED (one colour), PUNCH (contrast and colour split only); over
    ///                  them radial or edge speed lines and the chess.com badge. Looks are URP colour curves (a colour
    ///                  filter would be greyed by the saturation in URP's LUT order). Caps keep it from strobing; a viewer
    ///                  standing in a live warning gets the overlays only, GameSettings.ReduceFlash one soft frame.
    ///   captures       the strikes of one frame are one burst; the ultimate's own code may call it a blow or its signature
    ///                  (UltBlow), otherwise it is a capture (the first, a king, two or more) or a punch.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        // ================================================================ slow motion

        public enum UltSlowKind { Key, SigAnticipate, SigAftermath, Judge, Great }

        struct SlowSeg
        {
            public float len, a, b;
            public byte ease;   // 0 linear, 1 smoothstep, 2 quad-in (a + (b - a) u²)
        }

        readonly List<SlowSeg> slowSegs = new List<SlowSeg>();
        int slowI;
        float slowSegT, slowNow = 1f, slowStartReal, slowResume = 1f;
        bool slowOn, slowReal, slowOpen;
        UltSlowKind slowKind;
        UltShow slowShow;
        // The online picture slow: this screen's show clock (decor only; every mark that times gameplay keeps Clock).
        float presentRate = 1f;
        // The caster's camera kicks (real time; read by UltSlowCamera).
        float camKickAt = -10f, camKickFov, camRollKick, camRollSign = 1f;
        AudioLowPassFilter lowPass;
        float lowCut = 22000f;

        /// <summary>Whether this PC's game is the whole game (no network): only then may the game's own time change.</summary>
        bool OfflineGame => !((game != null && game.NetworkControlled) || OnlineOverride);

        /// <summary>The REAL path: offline and one player at this PC.</summary>
        bool SlowRealPath => OfflineGame && !(game != null && game.SplitScreen);

        /// <summary>The slow motion's value now (1 normal, 0 frozen).</summary>
        float UltSlowNow => slowOn ? slowNow : 1f;

        /// <summary>The ultimates' sounds lower while it is slow.</summary>
        float UltSfxPitch => slowOn && slowNow < 0.5f ? 0.85f : 1f;

        float LabRate => game != null && game.SlowMotion ? game.slowMotionScale : 1f;

        void Seg(float len, float a, float b, byte ease = 0) => slowSegs.Add(new SlowSeg { len = len, a = a, b = b, ease = ease });

        /// <summary>A slow motion for the caster of <paramref name="u"/> (on its own screen only). Ignored for anyone else, for
        /// a second signature (or with the tunable ultSigSlow at 0), for a signature before the key's stamp, and past 3.3 s
        /// slowed in one ultimate. A request while one runs starts from the value it has now: slows never stack.</summary>
        void UltSlow(UltShow u, UltSlowKind kind)
        {
            if (u == null || u.by == null || Viewer != u.by) return;
            bool sig = kind == UltSlowKind.SigAnticipate || kind == UltSlowKind.SigAftermath;
            if (sig)
            {
                if (u.sigUsed || (Params != null && Params.ultSigSlow <= 0f)) return;
                if (RealClock - u.t0Real < 0.733f) return;
                u.sigUsed = true;
            }
            if (u.slowSpent >= 3.3f) return;
            if (!slowOn)
            {
                slowReal = SlowRealPath;
                slowResume = LabRate;
            }
            slowSegs.Clear();
            slowI = 0;
            slowSegT = 0f;
            slowOpen = false;
            slowKind = kind;
            slowShow = u;
            slowStartReal = RealClock;
            switch (kind)
            {
                case UltSlowKind.Key:
                    Seg(0.050f, 0f, 0f);          // 3 frozen frames (the key's impact frames)
                    Seg(0.070f, 0f, 0.15f);
                    Seg(0.580f, 0.15f, 0.22f, 1); // the float
                    Seg(0.033f, 0f, 0f);          // 2 frozen frames: the stamp
                    Seg(0.367f, 0.22f, 1f, 2);    // the snap back
                    break;
                case UltSlowKind.SigAnticipate:
                    Seg(0.08f, slowOn ? slowNow : 1f, 0.42f);
                    slowOpen = true;   // held at 0.42 until UltSlowImpact (or the cap)
                    break;
                case UltSlowKind.SigAftermath:
                    Seg(0.10f, 0f, 0f);
                    Seg(0.35f, 0.30f, 0.30f);
                    Seg(0.25f, 0.30f, 1f);
                    break;
                case UltSlowKind.Judge:
                    Seg(0.12f, 0f, 0f);
                    Seg(0.40f, 0.30f, 0.30f);
                    Seg(0.30f, 0.30f, 1f);
                    break;
                default:
                    Seg(0.08f, 0f, 0f);
                    break;
            }
            slowOn = true;
            if (!slowReal) SlowHoldFreeze(u, kind == UltSlowKind.Key ? 25f : 12f);
        }

        /// <summary>The anticipation's impact: frozen 0.10 s (its IF-BLOW plays inside), then back to normal over 0.25 s.</summary>
        void UltSlowImpact()
        {
            if (!slowOn || slowKind != UltSlowKind.SigAnticipate) return;
            slowSegs.Clear();
            slowI = 0;
            slowSegT = 0f;
            slowOpen = false;
            Seg(0.10f, 0f, 0f);
            Seg(0.25f, 0.42f, 1f, 2);
            if (!slowReal && slowShow != null) SlowHoldFreeze(slowShow, 12f);
        }

        /// <summary>The PICTURE path's freeze: the pictures of the pieces near the caster held (0.12 s at most, uncounted).</summary>
        void SlowHoldFreeze(UltShow u, float radius)
        {
            if (u.by == null) return;
            Vector3 c = u.by.Hips.position;
            foreach (var p in RagdollPawn.All)
                if (p != null && (p.Hips.position - c).sqrMagnitude <= radius * radius) p.HoldVisual(0.12f, false);
        }

        /// <summary>Every frame (Update, real time): the curve on the game's time (REAL) or this screen's show clock
        /// (PICTURE); the low-pass; the end.</summary>
        void UltSlowStep(float real)
        {
            if (!slowOn)
            {
                presentRate = 1f;
                slowNow = 1f;
                LowPassStep(real);
                return;
            }
            slowSegT += real;
            while (slowI < slowSegs.Count && slowSegT >= slowSegs[slowI].len)
            {
                slowSegT -= slowSegs[slowI].len;
                slowI++;
                // A freeze begun on the PICTURE path holds the pictures.
                if (!slowReal && slowI < slowSegs.Count && slowSegs[slowI].a <= 0f && slowSegs[slowI].b <= 0f && slowShow != null) SlowHoldFreeze(slowShow, 12f);
            }
            bool over = false;
            if (slowI < slowSegs.Count)
            {
                var s = slowSegs[slowI];
                float u = Mathf.Clamp01(slowSegT / Mathf.Max(1e-4f, s.len));
                slowNow = s.ease == 1 ? Mathf.Lerp(s.a, s.b, Smooth01(u)) : s.ease == 2 ? s.a + (s.b - s.a) * u * u : Mathf.Lerp(s.a, s.b, u);
            }
            else if (slowOpen)
            {
                slowNow = 0.42f;
                // The cap: an anticipation that has not reached its impact in 1.1 s eases out.
                if (RealClock - slowStartReal >= 1.1f)
                {
                    slowOpen = false;
                    slowSegs.Clear();
                    slowI = 0;
                    slowSegT = 0f;
                    Seg(0.25f, 0.42f, 1f, 2);
                }
            }
            else
            {
                over = true;
                slowNow = 1f;
            }
            if (slowShow != null && slowNow < 0.999f)
            {
                slowShow.slowSpent += real;
                if (slowShow.slowSpent >= 3.3f && slowI < slowSegs.Count - 1)
                {
                    // Past the budget: straight to a short ease out.
                    float from = slowNow;
                    slowSegs.Clear();
                    slowI = 0;
                    slowSegT = 0f;
                    slowOpen = false;
                    Seg(0.2f, from, 1f);
                }
            }
            if (over)
            {
                SlowEnd();
                LowPassStep(real);
                return;
            }
            if (slowReal)
            {
                presentRate = 1f;
                float scale = LabRate * slowNow;
                if (stopping) stopResume = Mathf.Max(0.0001f, scale);   // (what the hit stop gives back when it ends)
                else Time.timeScale = scale;
            }
            else presentRate = Mathf.Max(0.25f, slowNow);
            LowPassStep(real);
        }

        void SlowEnd()
        {
            slowOn = false;
            slowOpen = false;
            presentRate = 1f;
            slowNow = 1f;
            if (!slowReal) return;
            if (stopping) stopResume = LabRate;
            else if (game != null) game.ApplyTime();
            else Time.timeScale = slowResume;
        }

        /// <summary>The slow motion stopped at once (this object going): the game's time as it was.</summary>
        void UltSlowStop()
        {
            if (!slowOn) return;
            SlowEnd();
            if (lowPass != null) lowPass.enabled = false;
        }

        /// <summary>The listener's low-pass: 800 Hz while it is slow, opening as it ramps back.</summary>
        void LowPassStep(float real)
        {
            float target = !slowOn ? 22000f : slowNow < 0.6f ? 800f : Mathf.Lerp(800f, 22000f, (slowNow - 0.6f) / 0.4f);
            lowCut = Mathf.Lerp(lowCut, target, 1f - Mathf.Exp(-real / 0.03f));
            if (!slowOn && lowCut > 21000f)
            {
                if (lowPass != null && lowPass.enabled) lowPass.enabled = false;
                return;
            }
            if (lowPass == null)
            {
                var listener = FindFirstObjectByType<AudioListener>();
                if (listener == null) return;
                lowPass = listener.GetComponent<AudioLowPassFilter>();
                if (lowPass == null) lowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
            }
            lowPass.enabled = true;
            lowPass.cutoffFrequency = lowCut;
            lowPass.lowpassResonanceQ = 1f;
        }

        /// <summary>The caster's camera terms of the slow motion now (real time): field of view, distance back, height,
        /// roll. Its direction is never touched. Settings: 화면 흔들림 off (ReduceShake) leaves no roll and half the kicks;
        /// the shake slider scales the roll.</summary>
        void UltSlowCamera(UltShow u, ref float fov, ref float back, ref float up, ref float roll)
        {
            bool still = ChessFight.Game.GameSettings.ReduceShake;
            float motion = still ? 0.5f : 1f, rollScale = still ? 0f : ChessFight.Game.GameSettings.ShakeScale;
            float since = RealClock - u.t0Real;
            // The key: a dolly zoom in (FOV in, the camera back and up: the caster keeps its size while the board grows),
            // a roll; at the stamp the FOV springs open and everything goes back.
            if (since < 1.6f)
            {
                const float Stamp = 0.733f;
                if (since < Stamp)
                {
                    fov += -8f * Mathf.Clamp01(since / 0.033f);
                    float d = Smooth01(since / 0.25f);
                    back += 1.2f * d;
                    up += 0.4f * d;
                    roll += 3f * rollScale * Smooth01(since / 0.12f);
                }
                else
                {
                    float a = since - Stamp;
                    fov += 8f * motion * Mathf.Exp(-a / 0.30f);
                    float d = 1f - Smooth01(a / 0.5f);
                    back += 1.2f * d;
                    up += 0.4f * d;
                    if (a < 0.4f) roll += Mathf.Lerp(-1f, 0f, Smooth01(a / 0.4f)) * rollScale;
                }
            }
            if (slowOn && slowShow == u)
            {
                if (slowKind == UltSlowKind.SigAnticipate && slowOpen) fov -= 6f;
                if (slowKind == UltSlowKind.Judge && slowNow < 0.5f) fov -= 5f;
            }
            if (camKickAt > 0f)
            {
                float a = RealClock - camKickAt;
                fov += camKickFov * motion * Mathf.Exp(-a / 0.30f);
                roll += camRollKick * rollScale * Mathf.Exp(-a / 0.25f);
            }
        }

        // ================================================================ impact frames

        public enum ImpactLook { None, Ink, InkTeal, Neg, Sin, Teal, Red }

        public enum ImpactSeq { Key, Ally, Enemy, Stamp, Capture, Punch, Victim, Blow, Promotion, Sacrifice, Majority, Judge, JudgeEnemy, Great, Best, Blunder }

        struct ImpactFrame
        {
            public ImpactLook look;
            public float weight, contrast, exposure, ca, lens, vignette;
            public Color vignetteColor;
            public bool lines;            // radial speed lines on the anchor
            public Color linesColor;
            public float linesAlpha, linesClear;   // the clear middle's radius, × screen height
            public bool badge, giantInk, redFill, punch, kick;
            public Color badgeColor;
            public string badgeGlyph;
            public float badgeSize;       // × screen height
            public Vector3? anchor;
            public Vector2? anchorGui;    // on the 720-high screen
        }

        readonly Queue<ImpactFrame> impactQueue = new Queue<ImpactFrame>();
        readonly List<ImpactFrame> building = new List<ImpactFrame>();
        readonly List<float> impactStarts = new List<float>();
        readonly List<RagdollPawn> impactFill = new List<RagdollPawn>();
        ImpactFrame impactNow;
        bool impactShowing, impactWasOn;
        int impactFrameSeen = -1, impactFramesHeld;
        float impactHeldReal, lastSeqReal = -10f, lastInkReal = -10f, lastBlowReal = -10f, linesRot, linesTurned, deflateUntil = -10f;
        float impBaseContrast, impBaseExposure;
        // After a sequence: its speed lines fading, its colour split and contrast going back.
        float tailLines, tailLinesFade = 0.25f, tailCa, tailContrast, tailClear = 0.39f;
        Color tailLinesColor = Color.white;
        Vector3? tailAnchor;
        // Edge speed lines for runs, rolls and dashes (set each frame by the ultimate that wants them).
        float edgeLines, edgeLinesSet = -1f;
        Texture2D texSpeedLines, texDisc, texRingBadge;
        GUIStyle badgeStyle, badgeSymbolStyle;
        PawnRushSkillBloom viewBloom;
        /// <summary>The current frame wants the giant avatar drawn in ink (the giant's code reads it).</summary>
        public bool ImpactGiantInk => impactShowing && impactNow.giantInk;
#if CF_URP
        GameObject impactHolder;
        readonly Volume[] lookVolumes = new Volume[7];
        readonly VolumeProfile[] lookProfiles = new VolumeProfile[7];
        Volume impactVolume;
        VolumeProfile impactProfile;
        ColorAdjustments impactAdjust;
        ChromaticAberration impactAberration;
        LensDistortion impactLens;
        Vignette impactVignette;
#endif

        static bool BlowClass(ImpactSeq s) => s == ImpactSeq.Blow || s == ImpactSeq.Promotion || s == ImpactSeq.Sacrifice || s == ImpactSeq.Majority || s == ImpactSeq.Judge;

        static bool InkLook(ImpactLook l) => l == ImpactLook.Ink || l == ImpactLook.InkTeal || l == ImpactLook.Neg;

        /// <summary>Edge speed lines this frame (runs, rolls, dashes; 0.35 at most).</summary>
        void UltEdgeLines(float alpha)
        {
            edgeLines = Mathf.Clamp(alpha, 0f, 0.35f);
            edgeLinesSet = RealClock;
        }

        /// <summary>
        /// An impact frame sequence for this screen: who sees what of it (the caster all of it, others by the rules), the
        /// caps (starts 0.25 s apart, three a second, INK and NEG 0.4 s apart, blows 0.4 s apart), a viewer standing in a live
        /// warning (the overlays only, at half), ReduceFlash (one soft frame), and the hit stop that goes with it.
        /// </summary>
        void UltFrames(ImpactSeq seq, Vector3 anchor, UltShow u, IReadOnlyList<RagdollPawn> struck = null)
        {
            if (!effects || u == null || u.by == null) return;
            var me = Viewer;
            bool caster = me != null && me == u.by;
            bool ally = me != null && !caster && !Teams.AreEnemies(me.Team, u.by.Team);
            bool enemy = me != null && !caster && Teams.AreEnemies(me.Team, u.by.Team);
            var cam = ViewCamera;
            float far = cam != null ? Vector3.Distance(cam.transform.position, anchor) : 0f;
            bool onScreen = cam != null && OnScreen(cam, anchor);
            bool warned = ViewerUnderWarning();
            bool victim = struck != null && me != null && Contains(struck, me);
            // Who sees it.
            switch (seq)
            {
                case ImpactSeq.Key: case ImpactSeq.Stamp: case ImpactSeq.Capture: case ImpactSeq.Punch: case ImpactSeq.Best: case ImpactSeq.Blunder:
                    if (!caster) return;
                    break;
                case ImpactSeq.Ally:
                    if (!ally || far > 40f || !onScreen) return;
                    break;
                case ImpactSeq.Enemy:
                    if (!enemy || far > 40f || !onScreen || warned) return;
                    break;
                case ImpactSeq.Victim:
                    if (!victim) return;
                    break;
                case ImpactSeq.Great:
                    if (!caster && !ally) return;
                    break;
                case ImpactSeq.Judge:
                    if (enemy)
                    {
                        if (far > 60f) return;
                        seq = ImpactSeq.JudgeEnemy;
                    }
                    else if (!caster && !ally && far > 60f) return;
                    break;
                default:   // the blows: the caster all; others within 45 m (60 for the sacrifice) with it on screen one soft frame
                    if (!caster)
                    {
                        if (victim)
                        {
                            seq = ImpactSeq.Victim;
                            break;
                        }
                        float reach = seq == ImpactSeq.Sacrifice ? 60f : 45f;
                        if (far > reach || !onScreen || warned) return;
                        Push(SoftSin(anchor));
                        Shake(0.22f * Mathf.Clamp01(1f - far / reach), 0.45f);
                        return;
                    }
                    break;
            }
            // The caps.
            float now = RealClock;
            impactStarts.RemoveAll(t => now - t > 1f);
            bool tooSoon = now - lastSeqReal < 0.25f || impactStarts.Count >= 3 || (BlowClass(seq) && now - lastBlowReal < 0.4f);
            if (tooSoon && seq != ImpactSeq.Judge)
            {
                if (!warned) Push(PunchFrame(anchor));
                return;
            }
            lastSeqReal = now;
            impactStarts.Add(now);
            if (BlowClass(seq)) lastBlowReal = now;
            impactFill.Clear();
            if (struck != null) impactFill.AddRange(struck);
            building.Clear();
            BuildSequence(seq, anchor);
            if (building.Count > 0)
            {
                bool reduce = ChessFight.Game.GameSettings.ReduceFlash;
                if (reduce)
                {
                    // One soft SIN frame that keeps the first frame's overlays.
                    var first = building[0];
                    var soft = SoftSin(anchor);
                    soft.lines = first.lines;
                    soft.linesColor = first.linesColor;
                    soft.linesAlpha = first.linesAlpha;
                    soft.linesClear = first.linesClear;
                    soft.badge = first.badge;
                    soft.badgeColor = first.badgeColor;
                    soft.badgeGlyph = first.badgeGlyph;
                    soft.badgeSize = first.badgeSize;
                    soft.anchorGui = first.anchorGui;
                    building.Clear();
                    building.Add(soft);
                }
                bool inkOk = now - lastInkReal >= 0.4f, inked = false;
                for (int i = 0; i < building.Count; i++)
                {
                    var f = building[i];
                    if (InkLook(f.look))
                    {
                        if (inkOk) inked = true;
                        else f.look = ImpactLook.Sin;   // INK or NEG again so soon: a SIN frame
                    }
                    if (warned)
                    {
                        // Standing in a warning: no looks at all, only the overlays (drawn at half).
                        f.look = ImpactLook.None;
                        f.contrast = f.exposure = f.ca = f.lens = f.vignette = 0f;
                        f.redFill = f.giantInk = f.punch = f.kick = false;
                    }
                    if (impactQueue.Count < 16) impactQueue.Enqueue(f);
                }
                if (inked) lastInkReal = now;
                if (warned || reduce) tailCa = tailContrast = 0f;
            }
            // The hit stop that goes with it (the caster's screen; online it holds pictures only).
            float stop = seq switch
            {
                ImpactSeq.Capture => 0.067f,
                ImpactSeq.Blow => 0.10f,
                ImpactSeq.Promotion => 0.10f,
                ImpactSeq.Sacrifice => SlowRealPath ? 0.16f : 0.12f,
                ImpactSeq.Majority => 0.08f,
                ImpactSeq.Judge => 0.12f,
                ImpactSeq.Great => 0.08f,
                _ => 0f,
            };
            if (stop > 0f && caster)
            {
                stopBy = u.by;
                stopTarget = struck != null && struck.Count > 0 ? struck[0] : null;
                HitStop(stop);
                if (!OfflineGame && struck != null)
                    foreach (var p in struck) if (p != null) p.HoldVisual(Mathf.Min(stop, 0.12f), false);
            }
        }

        static bool Contains(IReadOnlyList<RagdollPawn> list, RagdollPawn p)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == p) return true;
            return false;
        }

        static bool OnScreen(Camera cam, Vector3 at)
        {
            Vector3 v = cam.WorldToViewportPoint(at);
            return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
        }

        /// <summary>Whether the piece played here stands in a live warning of an enemy ultimate now.</summary>
        bool ViewerUnderWarning()
        {
            var me = Viewer;
            if (me == null) return false;
            foreach (var u in ults)
                if (u.Driven && u.Playing && u.by != null && u.by != me && Teams.AreEnemies(me.Team, u.by.Team) && DriveWarning(u, me) != null) return true;
            return false;
        }

        ImpactFrame LookFrame(ImpactLook look, Vector3? anchor) => new ImpactFrame
        {
            look = look, weight = 1f, anchor = anchor, linesColor = Color.white, linesClear = 0.39f, badgeColor = Color.white, badgeGlyph = "!!",
            vignetteColor = Color.black,
        };

        ImpactFrame SoftSin(Vector3 anchor)
        {
            var f = LookFrame(ImpactLook.Sin, anchor);
            f.weight = 0.5f;
            f.contrast = 30f;
            f.ca = 0.25f;
            return f;
        }

        ImpactFrame PunchFrame(Vector3 anchor, float contrast = 40f)
        {
            var f = LookFrame(ImpactLook.None, anchor);
            f.contrast = contrast;
            f.ca = 0.6f;
            return f;
        }

        void Push(ImpactFrame f)
        {
            if (impactQueue.Count < 16) impactQueue.Enqueue(f);
        }

        static ImpactFrame WithLines(ImpactFrame f, Color c, float alpha, float clear = 0.39f)
        {
            f.lines = true;
            f.linesColor = c;
            f.linesAlpha = alpha;
            f.linesClear = clear;
            return f;
        }

        static ImpactFrame WithBadge(ImpactFrame f, Color c, float size, string glyph = "!!")
        {
            f.badge = true;
            f.badgeColor = c;
            f.badgeSize = size;
            f.badgeGlyph = glyph;
            return f;
        }

        /// <summary>The frames of a sequence (the spec's section 9; F = one 60 Hz frame), into <see cref="building"/>.</summary>
        void BuildSequence(ImpactSeq seq, Vector3 anchor)
        {
            Color white = Color.white, black = Color.black, brill = GradeColors[0];
            Vector3? at = anchor;
            ImpactFrame f;
            switch (seq)
            {
                case ImpactSeq.Key:
                    // F1 INK-T and lines on the caster, pinched toward it; F2 the negative with the giant in ink; F3 SIN with
                    // the enemies in view filled red. Then the lines fade over 0.25 s.
                    f = WithLines(LookFrame(ImpactLook.InkTeal, at), white, 0.85f);
                    f.lens = -0.15f;
                    f.ca = 0.7f;
                    f.contrast = 30f;
                    building.Add(f);
                    f = WithLines(LookFrame(ImpactLook.Neg, at), black, 0.85f);
                    f.giantInk = true;
                    f.ca = 1f;
                    f.contrast = 40f;
                    building.Add(f);
                    f = WithLines(LookFrame(ImpactLook.Sin, at), white, 0.6f);
                    f.redFill = true;
                    f.contrast = 60f;
                    f.exposure = -0.4f;
                    f.vignette = 0.45f;
                    f.ca = 0.5f;
                    building.Add(f);
                    Tail(0.5f, 0.25f, white, at, 0.39f);
                    break;
                case ImpactSeq.Ally:
                    building.Add(WithLines(SoftSin(anchor), white, 0.35f));
                    break;
                case ImpactSeq.Enemy:
                    building.Add(SoftSin(anchor));
                    break;
                case ImpactSeq.Stamp:
                    // At the end of the call: TEAL with the badge white and big and the lines on it; then the badge in Brill.
                    f = WithBadge(WithLines(LookFrame(ImpactLook.Teal, null), white, 0.7f), white, 0.27f);
                    f.anchorGui = StampAnchor;
                    f.contrast = 80f;
                    f.exposure = 0.2f;
                    f.ca = 0.6f;
                    building.Add(f);
                    f = WithBadge(LookFrame(ImpactLook.None, null), brill, 0.24f);
                    f.anchorGui = StampAnchor;
                    building.Add(f);
                    break;
                case ImpactSeq.Capture:
                    f = WithLines(LookFrame(ImpactLook.Sin, at), white, 0.6f, 0.095f);   // (lines 0.5·H wide on the victim)
                    f.redFill = true;
                    f.contrast = 60f;
                    f.exposure = -0.4f;
                    f.vignette = 0.45f;
                    f.ca = 0.35f;
                    building.Add(f);
                    f = LookFrame(ImpactLook.None, at);
                    f.ca = 0.2f;
                    building.Add(f);
                    break;
                case ImpactSeq.Punch:
                    for (int i = 0; i < 3; i++) building.Add(PunchFrame(anchor));
                    break;
                case ImpactSeq.Best:
                    building.Add(PunchFrame(anchor, 30f));
                    break;
                case ImpactSeq.Blunder:
                    deflateUntil = RealClock + 0.3f;   // a 0.3 s deflate: contrast -20, exposure -0.15
                    break;
                case ImpactSeq.Victim:
                    f = WithLines(LookFrame(ImpactLook.Red, at), Blood, 0.6f);
                    f.weight = 0.5f;
                    f.contrast = 50f;
                    f.vignette = 0.5f;
                    f.vignetteColor = new Color(0.7f, 0f, 0.03f);
                    f.ca = 0.4f;
                    building.Add(f);
                    break;
                case ImpactSeq.Promotion:
                    f = WithLines(LookFrame(ImpactLook.Teal, at), white, 0.7f);
                    f.contrast = 80f;
                    f.ca = 0.6f;
                    building.Add(f);
                    f = WithLines(LookFrame(ImpactLook.Neg, at), black, 0.7f);
                    f.giantInk = true;
                    f.ca = 1f;
                    building.Add(f);
                    f = LookFrame(ImpactLook.Sin, at);
                    f.contrast = 60f;
                    f.exposure = -0.4f;
                    f.kick = true;
                    building.Add(f);
                    Tail(0.35f, 0.15f, white, at, 0.3f);
                    break;
                case ImpactSeq.Sacrifice:
                    f = WithBadge(LookFrame(ImpactLook.Teal, at), white, 0.30f);
                    f.contrast = 80f;
                    f.ca = 0.6f;
                    building.Add(f);
                    f = WithLines(LookFrame(ImpactLook.Neg, at), black, 0.7f, 0.25f);
                    f.giantInk = true;
                    f.ca = 1f;
                    building.Add(f);
                    f = LookFrame(ImpactLook.Sin, at);
                    f.redFill = true;
                    f.contrast = 60f;
                    f.exposure = -0.4f;
                    building.Add(f);
                    f = PunchFrame(anchor);
                    f.kick = true;
                    building.Add(f);
                    Tail(0.35f, 0.15f, white, at, 0.25f);
                    break;
                case ImpactSeq.Majority:
                    f = LookFrame(ImpactLook.Teal, at);
                    f.contrast = 80f;
                    building.Add(f);
                    f = WithLines(LookFrame(ImpactLook.Neg, at), black, 0.6f);
                    f.giantInk = true;
                    building.Add(f);
                    f = PunchFrame(anchor);
                    f.kick = true;
                    building.Add(f);
                    break;
                case ImpactSeq.Judge:
                    f = WithBadge(WithLines(LookFrame(ImpactLook.Ink, at), white, 0.85f), white, 0.40f);
                    f.contrast = 30f;
                    f.ca = 0.7f;
                    building.Add(f);
                    f = WithBadge(WithLines(LookFrame(ImpactLook.Teal, at), white, 0.7f), white, 0.40f);
                    f.contrast = 80f;
                    building.Add(f);
                    f = WithBadge(LookFrame(ImpactLook.None, at), brill, 0.40f);
                    f.contrast = 60f;
                    f.exposure = 0.25f;
                    f.ca = 0.8f;
                    f.punch = true;
                    building.Add(f);
                    Tail(0.35f, 0.15f, white, at, 0.39f);
                    break;
                case ImpactSeq.JudgeEnemy:
                    f = WithBadge(WithLines(LookFrame(ImpactLook.Teal, at), white, 0.5f), white, 0.30f);
                    f.contrast = 50f;
                    building.Add(f);
                    break;
                case ImpactSeq.Great:
                    f = WithLines(LookFrame(ImpactLook.Sin, at), white, 0.6f);
                    f.contrast = 60f;
                    building.Add(f);
                    building.Add(WithBadge(LookFrame(ImpactLook.None, at), GradeColors[1], 0.30f, "!"));
                    break;
                default:   // a blow: INK and a bulge, the negative, then the punch (bloom up, the shake and the camera kick)
                    f = WithLines(LookFrame(ImpactLook.Ink, at), white, 0.55f, 0.21f);
                    f.lens = 0.30f;
                    f.ca = 1f;
                    f.contrast = 30f;
                    f.redFill = true;
                    building.Add(f);
                    f = WithLines(LookFrame(ImpactLook.Neg, at), black, 0.6f, 0.21f);
                    f.lens = 0.18f;
                    f.ca = 1f;
                    f.contrast = 40f;
                    building.Add(f);
                    f = WithLines(LookFrame(ImpactLook.None, at), white, 0.35f, 0.21f);
                    f.contrast = 60f;
                    f.exposure = 0.25f;
                    f.ca = 0.8f;
                    f.lens = 0.08f;
                    f.punch = true;
                    f.kick = true;
                    building.Add(f);
                    Tail(0.35f, 0.15f, white, at, 0.21f);
                    break;
            }
        }

        /// <summary>Where the stamp's badge sits (720-high screen): at the end of the call in the top band.</summary>
        Vector2 StampAnchor => new Vector2(Screen.width / Mathf.Max(1f, Screen.height / 720f) * 0.5f + 230f, 192f);

        void Tail(float lines, float fade, Color c, Vector3? anchor, float clear)
        {
            tailLines = lines;
            tailLinesFade = fade;
            tailLinesColor = c;
            tailAnchor = anchor;
            tailClear = clear;
            tailCa = 0.8f;
            tailContrast = 60f;
        }

        // ---------------------------------------------------------------- the moments that make them

        /// <summary>The key's stamp, 0.70 s real after the key (the slow motion's second freeze): on the caster's screen the
        /// !! stamped on the move, the brilliant chord and a boom; an ally hears the chord.</summary>
        void UltKeyStamp(UltShow u)
        {
            if (u.by == null) return;
            var me = Viewer;
            UltFrames(ImpactSeq.Stamp, ChestOf(u.by), u);
            if (me != null && me == u.by)
            {
                UltPlay(sfxGrade[0], 0.85f);
                UltPlay(sfxBoom, 0.7f);
                Shake(0.18f, 0.3f);
            }
            else if (me != null && !Teams.AreEnemies(me.Team, u.by.Team)) UltPlay(sfxGrade[0], 0.5f);
        }

        void UltBurstOpen(UltShow u)
        {
            u.burstFrame = Time.frameCount;
            u.burst.Clear();
            u.burstClaimed = u.burstDone = false;
        }

        /// <summary>The ultimate's own code naming this frame's moment (a blow, its signature: Blow, Promotion, Sacrifice,
        /// Majority, or a Punch for one that hit nobody); its frames play once the frame's strikes are all in.</summary>
        void UltBlow(UltShow u, ImpactSeq seq, Vector3 anchor)
        {
            if (u == null) return;
            if (u.burstFrame != Time.frameCount) UltBurstOpen(u);
            u.burstClaimed = true;
            u.claimSeq = seq;
            u.claimAt = anchor;
        }

        /// <summary>This frame's captures, once they are all in (UltStep): the blow the ultimate named, or a capture (its
        /// first, a king, two or more at once) or a punch; the struck pieces' own screens their red frame.</summary>
        void UltBurstResolve(UltShow u)
        {
            u.burstDone = true;
            Vector3 at = u.burst.Count > 0 && u.burst[0] != null ? ChestOf(u.burst[0]) : u.burstAt;
            ImpactSeq seq = ImpactSeq.Punch;
            if (u.burstClaimed)
            {
                seq = u.claimSeq;
                UltFrames(seq, u.claimAt, u, u.burst);
            }
            else if (u.burst.Count > 0)
            {
                bool king = false;
                foreach (var p in u.burst) if (p != null && p.Piece == PieceKind.King) king = true;
                seq = !u.captured || king || u.burst.Count >= 2 ? ImpactSeq.Capture : ImpactSeq.Punch;
                UltFrames(seq, at, u, u.burst);
            }
            if (u.burst.Count > 0)
            {
                if (!BlowClass(seq)) UltFrames(ImpactSeq.Victim, at, u, u.burst);   // (a blow's own call gives its victims theirs)
                u.captured = true;
            }
        }

        /// <summary>The judgement (UltHit, every screen): !! freezes the caster's time and stamps the badge on every screen
        /// near, then the stars come out; ! a short freeze and a blue badge for the caster's side; ★ a punch; ?? a deflate.</summary>
        void UltJudgeFrames(UltShow u, Vector3 at)
        {
            Vector3 anchor = Ground(at) + Vector3.up * 1.2f;
            switch (u.grade)
            {
                case RagdollPawn.GradeBrilliant:
                    UltSlow(u, UltSlowKind.Judge);
                    UltFrames(ImpactSeq.Judge, anchor, u);
                    u.starsAt = RealClock;
                    break;
                case RagdollPawn.GradeGreat:
                    UltSlow(u, UltSlowKind.Great);
                    UltFrames(ImpactSeq.Great, anchor, u);
                    break;
                case RagdollPawn.GradeBest:
                    UltFrames(ImpactSeq.Best, anchor, u);
                    break;
                case RagdollPawn.GradeBlunder:
                    UltFrames(ImpactSeq.Blunder, anchor, u);
                    break;
            }
        }

        /// <summary>The bloom told how much ultimate is on the picture now (tighter, clamped) and set at once, so a punch frame
        /// is bloomed in its own frame.</summary>
        void UltBloomRefresh(float k)
        {
            PawnRushSkillBloom.UltK = k;
            if (viewBloom != null) viewBloom.Refresh();
        }

        // ---------------------------------------------------------------- every frame

        /// <summary>Every frame (LateUpdate, before rendering): the next queued frame on once the last has been shown for 1/60 s
        /// real and at least one rendered frame; the tail fading; the volumes set.</summary>
        void UltImpactStep()
        {
            if (Time.frameCount == impactFrameSeen) return;
            impactFrameSeen = Time.frameCount;
            PawnRushSkillBloom.UltPunch = 0f;
            if (impactShowing)
            {
                impactHeldReal += realDt;
                impactFramesHeld++;
                if (impactHeldReal >= 1f / 60f - 0.0015f && impactFramesHeld >= 1) impactShowing = false;   // (a 60 Hz frame, give or take the timer)
            }
            if (!impactShowing && impactQueue.Count > 0 && effects)
            {
                impactNow = impactQueue.Dequeue();
                impactShowing = true;
                impactHeldReal = 0f;
                impactFramesHeld = 0;
                linesRot = Random.Range(0f, 360f);
                if (impactNow.redFill)
                {
                    if (impactFill.Count > 0) foreach (var p in impactFill) FlashFill(p);
                    else FlashFillEnemiesInView();
                }
                if (impactNow.punch) PawnRushSkillBloom.UltPunch = 1f;
                if (impactNow.kick)
                {
                    // The blow's third frame: the shake and the caster's camera kick (FOV +6°, a 2.5° roll, its side
                    // turning each blow).
                    Shake(0.30f, 0.45f);
                    camKickAt = RealClock;
                    camKickFov = 6f;
                    camRollKick = 2.5f * camRollSign;
                    camRollSign = -camRollSign;
                }
            }
            if (!impactShowing)
            {
                // The tail's lines are turned anew every 60 Hz frame (each impact frame got its own turn when it came).
                if (tailLines > 0.01f && RealClock - linesTurned >= 1f / 60f)
                {
                    linesTurned = RealClock;
                    linesRot = Random.Range(0f, 360f);
                }
                tailLines = Mathf.Max(0f, tailLines - realDt / Mathf.Max(0.05f, tailLinesFade) * 0.5f);
                tailCa *= Mathf.Exp(-realDt / 0.06f);
                tailContrast = Mathf.Max(0f, tailContrast - realDt / 0.1f * 60f);
            }
            if (RealClock - edgeLinesSet > 0.05f) edgeLines = Mathf.Max(0f, edgeLines - realDt * 3f);
#if CF_URP
            ImpactVolumesBuild();
            if (impactVolume == null) return;
            var look = impactShowing ? impactNow.look : ImpactLook.None;
            for (int i = 1; i < lookVolumes.Length; i++)
                if (lookVolumes[i] != null) lookVolumes[i].weight = i == (int)look ? Mathf.Clamp01(impactNow.weight) : 0f;
            float contrast = impactShowing ? impactNow.contrast : tailContrast, exposure = impactShowing ? impactNow.exposure : 0f;
            float ca = impactShowing ? impactNow.ca : tailCa, lens = impactShowing ? impactNow.lens : 0f;
            if (RealClock < deflateUntil)
            {
                contrast -= 20f;
                exposure -= 0.15f;
            }
            if (ChessFight.Game.GameSettings.ReduceFlash)
            {
                ca *= 0.5f;
                lens = 0f;
            }
            bool vig = impactShowing && impactNow.vignette > 0f;
            bool on = Mathf.Abs(contrast) > 0.5f || Mathf.Abs(exposure) > 0.01f || ca > 0.02f || Mathf.Abs(lens) > 0.005f || vig;
            if (on && !impactWasOn)
            {
                // The scene's own values, taken while this volume is still off (the grade's are read live below).
                var stack = VolumeManager.instance.stack?.GetComponent<ColorAdjustments>();
                impBaseContrast = stack != null ? stack.contrast.value : 0f;
                impBaseExposure = stack != null ? stack.postExposure.value : 0f;
            }
            impactWasOn = on;
            impactVolume.weight = on ? 1f : 0f;
            if (!on) return;
            bool graded = ultAdjust != null && gradeVolume != null && gradeVolume.weight > 0f;
            impactAdjust.contrast.value = Mathf.Clamp((graded ? ultAdjust.contrast.value : impBaseContrast) + contrast, -100f, 100f);
            impactAdjust.postExposure.value = (graded ? ultAdjust.postExposure.value : impBaseExposure) + exposure;
            impactAberration.intensity.value = Mathf.Clamp01(ca);
            impactLens.intensity.overrideState = Mathf.Abs(lens) > 0.005f;
            impactLens.intensity.value = Mathf.Clamp(lens, -1f, 1f);
            if (impactShowing && impactNow.anchor.HasValue && ViewCamera != null)
            {
                Vector3 v = ViewCamera.WorldToViewportPoint(impactNow.anchor.Value);
                impactLens.center.value = new Vector2(Mathf.Clamp(v.x, 0.2f, 0.8f), Mathf.Clamp(v.y, 0.2f, 0.8f));
            }
            impactVignette.intensity.overrideState = impactVignette.color.overrideState = vig;
            if (vig)
            {
                impactVignette.intensity.value = impactNow.vignette;
                impactVignette.color.value = impactNow.vignetteColor;
            }
#endif
        }

        /// <summary>The red fill for one frame on every enemy of the piece played here that is in view (the key's third frame).</summary>
        void FlashFillEnemiesInView()
        {
            var me = Viewer;
            var cam = ViewCamera;
            if (me == null || cam == null) return;
            foreach (var p in RagdollPawn.All)
                if (p != null && p != me && Teams.AreEnemies(p.Team, me.Team) && OnScreen(cam, p.Hips.position)) FlashFill(p);
        }

#if CF_URP
        /// <summary>The looks' volumes (one per look, priority 140, weight 0 or 1) and the adjust volume (priority 147: the
        /// grade's contrast and exposure plus the frame's, the colour split, the lens distortion, a vignette).</summary>
        void ImpactVolumesBuild()
        {
            if (impactHolder != null) return;
            impactHolder = new GameObject("Ult impact frames (volumes)");
            impactHolder.transform.SetParent(root, false);
            for (int i = 1; i < lookVolumes.Length; i++)
            {
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.hideFlags = HideFlags.HideAndDontSave;
                var curves = profile.Add<ColorCurves>(false);
                LookCurves((ImpactLook)i, curves);
                var holder = new GameObject("Ult look " + (ImpactLook)i);
                holder.transform.SetParent(impactHolder.transform, false);
                var v = holder.AddComponent<Volume>();
                v.isGlobal = true;
                v.priority = 140f;
                v.sharedProfile = profile;
                v.weight = 0f;
                lookVolumes[i] = v;
                lookProfiles[i] = profile;
            }
            impactProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            impactProfile.hideFlags = HideFlags.HideAndDontSave;
            impactAdjust = impactProfile.Add<ColorAdjustments>(false);
            impactAdjust.contrast.overrideState = impactAdjust.postExposure.overrideState = true;
            impactAberration = impactProfile.Add<ChromaticAberration>(false);
            impactAberration.intensity.overrideState = true;
            impactLens = impactProfile.Add<LensDistortion>(false);
            impactLens.center.overrideState = true;
            impactVignette = impactProfile.Add<Vignette>(false);
            var adjust = new GameObject("Ult impact adjust");
            adjust.transform.SetParent(impactHolder.transform, false);
            impactVolume = adjust.AddComponent<Volume>();
            impactVolume.isGlobal = true;
            impactVolume.priority = 147f;
            impactVolume.sharedProfile = impactProfile;
            impactVolume.weight = 0f;
        }

        static TextureCurve Curve(params Vector2[] points)
        {
            var keys = new Keyframe[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                float slopeIn = i > 0 ? (points[i].y - points[i - 1].y) / Mathf.Max(1e-4f, points[i].x - points[i - 1].x) : 0f;
                float slopeOut = i + 1 < points.Length ? (points[i + 1].y - points[i].y) / Mathf.Max(1e-4f, points[i + 1].x - points[i].x) : slopeIn;
                if (i == 0) slopeIn = slopeOut;
                keys[i] = new Keyframe(points[i].x, points[i].y, slopeIn, slopeOut);
            }
            return new TextureCurve(keys, 0f, false, new Vector2(0f, 1f));
        }

        /// <summary>The hue-vs-saturation curve: every hue grey but a teal band at <paramref name="teal"/> and a red band at
        /// <paramref name="red"/> (0.5 keeps a hue as it is, 1 doubles it, 0 greys it).</summary>
        static TextureCurve HueBands(float teal, float red)
        {
            var keys = new[]
            {
                new Keyframe(0f, red), new Keyframe(0.015f, red), new Keyframe(0.05f, 0f), new Keyframe(0.385f, 0f),
                new Keyframe(0.425f, teal), new Keyframe(0.525f, teal), new Keyframe(0.565f, 0f), new Keyframe(0.93f, 0f),
                new Keyframe(0.955f, red),
            };
            return new TextureCurve(keys, 0.5f, true, new Vector2(0f, 1f));
        }

        static void LookCurves(ImpactLook look, ColorCurves c)
        {
            var two = new[] { new Vector2(0f, 0f), new Vector2(1f, 1f) };
            switch (look)
            {
                case ImpactLook.Ink:
                    c.master.Override(Curve(new Vector2(0f, 0f), new Vector2(0.30f, 0f), new Vector2(0.42f, 1f), new Vector2(1f, 1f)));
                    c.hueVsSat.Override(HueBands(1f, 1f));
                    break;
                case ImpactLook.InkTeal:
                    c.master.Override(Curve(new Vector2(0f, 0f), new Vector2(0.30f, 0f), new Vector2(0.42f, 1f), new Vector2(1f, 1f)));
                    c.hueVsSat.Override(HueBands(1f, 0f));
                    break;
                case ImpactLook.Neg:
                    c.master.Override(Curve(new Vector2(0f, 1f), new Vector2(1f, 0f)));
                    c.hueVsSat.Override(HueBands(0f, 0f));
                    break;
                case ImpactLook.Sin:
                    c.hueVsSat.Override(HueBands(0f, 0.75f));
                    break;
                case ImpactLook.Teal:
                    c.hueVsSat.Override(HueBands(0f, 0f));
                    c.red.Override(Curve(new Vector2(0f, 0f), new Vector2(1f, 0.12f)));
                    c.green.Override(Curve(two));
                    c.blue.Override(Curve(new Vector2(0f, 0f), new Vector2(1f, 0.82f)));
                    break;
                case ImpactLook.Red:
                    c.hueVsSat.Override(HueBands(0f, 0f));
                    c.red.Override(Curve(two));
                    c.green.Override(Curve(new Vector2(0f, 0f), new Vector2(1f, 0.10f)));
                    c.blue.Override(Curve(new Vector2(0f, 0f), new Vector2(1f, 0.12f)));
                    break;
            }
        }
#endif

        // ---------------------------------------------------------------- the overlays (OnGUI, after post: never graded)

        /// <summary>OnGUI, last: the frame's radial speed lines and badge, the tail's fading lines, the edge lines; at half for
        /// a viewer standing in a warning or with ReduceFlash.</summary>
        void UltImpactGui()
        {
            if (Event.current.type != EventType.Repaint) return;
            bool any = impactShowing || tailLines > 0.01f || edgeLines > 0.01f;
            if (!any) return;
            ImpactTextures();
            var keep = GUI.color;
            var keepMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            float h = Screen.height, w = Screen.width;
            float soft = ViewerUnderWarning() || ChessFight.Game.GameSettings.ReduceFlash ? 0.5f : 1f;
            // Edge lines: the texture drawn 3.2·H wide on the middle of the screen, so only its outer lines show.
            if (edgeLines > 0.01f) DrawLines(new Vector2(w * 0.5f, h * 0.5f), 1.6f * 0.38f * h, Color.white, edgeLines * soft, 0f);
            if (impactShowing && impactNow.lines)
                DrawLines(GuiPoint(impactNow.anchor, impactNow.anchorGui, w, h), impactNow.linesClear * h, impactNow.linesColor, impactNow.linesAlpha * soft, linesRot);
            else if (!impactShowing && tailLines > 0.01f)
                DrawLines(GuiPoint(tailAnchor, null, w, h), tailClear * h, tailLinesColor, tailLines * soft, linesRot);
            if (impactShowing && impactNow.badge)
                DrawBadge(GuiPoint(impactNow.anchor, impactNow.anchorGui, w, h), impactNow.badgeSize * h, impactNow.badgeColor, impactNow.badgeGlyph, false, soft);
            GUI.color = keep;
            GUI.matrix = keepMatrix;
        }

        /// <summary>A frame's anchor on the screen (pixels): a 720-high screen point, a world point, or the middle.</summary>
        Vector2 GuiPoint(Vector3? world, Vector2? gui, float w, float h)
        {
            if (gui.HasValue) return gui.Value * (h / 720f);
            var cam = ViewCamera;
            if (world.HasValue && cam != null)
            {
                Vector3 v = cam.WorldToScreenPoint(world.Value);
                if (v.z > 0f) return new Vector2(Mathf.Clamp(v.x, 0f, w), Mathf.Clamp(h - v.y, 0f, h));
            }
            return new Vector2(w * 0.5f, h * 0.5f);
        }

        /// <summary>The speed lines round <paramref name="centre"/> (pixels) with a clear middle of <paramref name="clear"/>
        /// pixels' radius.</summary>
        void DrawLines(Vector2 centre, float clear, Color c, float alpha, float rotation)
        {
            if (alpha <= 0.01f) return;
            float size = clear / 0.38f * 2f;
            var keepMatrix = GUI.matrix;
            if (Mathf.Abs(rotation) > 0.01f) GUIUtility.RotateAroundPivot(rotation, centre);
            GUI.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
            GUI.DrawTexture(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), texSpeedLines, ScaleMode.StretchToFill, true);
            GUI.matrix = keepMatrix;
        }

        /// <summary>
        /// The chess.com badge (the spec's N16; OnGUI, pixels): a disc in <paramref name="disc"/>'s colour with a white rim and
        /// a soft shadow, the glyph (!!, !, ★, ?!, ??) in white - in ink on a white disc; the hollow one a teal ring and a teal
        /// glyph. Never graded, never bloomed (it is drawn after the picture).
        /// </summary>
        void DrawBadge(Vector2 c, float size, Color disc, string glyph, bool hollow, float alpha = 1f)
        {
            if (size < 4f || alpha <= 0.01f) return;
            ImpactTextures();
            BadgeStyles();
            var keep = GUI.color;
            float half = size * 0.5f;
            GUI.color = new Color(0f, 0f, 0f, 0.45f * alpha);
            if (texSoft != null) GUI.DrawTexture(new Rect(c.x - half * 1.3f, c.y - half * 1.3f + size * 0.05f, size * 1.3f, size * 1.3f), texSoft, ScaleMode.StretchToFill, true);
            Color glyphColor;
            if (hollow)
            {
                GUI.color = new Color(Teal.r, Teal.g, Teal.b, alpha);
                GUI.DrawTexture(new Rect(c.x - half, c.y - half, size, size), texRingBadge, ScaleMode.StretchToFill, true);
                glyphColor = Teal;
            }
            else
            {
                float rim = Mathf.Max(2f, size * 0.025f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(c.x - half - rim, c.y - half - rim, size + rim * 2f, size + rim * 2f), texDisc, ScaleMode.StretchToFill, true);
                GUI.color = new Color(disc.r, disc.g, disc.b, alpha);
                GUI.DrawTexture(new Rect(c.x - half, c.y - half, size, size), texDisc, ScaleMode.StretchToFill, true);
                bool light = disc.r * 0.3f + disc.g * 0.59f + disc.b * 0.11f > 0.8f;
                glyphColor = light ? new Color(0.03f, 0.05f, 0.07f) : Color.white;
            }
            var st = glyph == "★" ? badgeSymbolStyle : badgeStyle;
            st.fontSize = Mathf.Clamp(Mathf.RoundToInt(size * (glyph.Length > 1 ? 0.52f : 0.62f)), 8, 400);
            GUI.color = new Color(glyphColor.r, glyphColor.g, glyphColor.b, alpha);
            GUI.Label(new Rect(c.x - size, c.y - half - size * 0.02f, size * 2f, size), glyph, st);
            GUI.color = keep;
        }

        void BadgeStyles()
        {
            if (badgeStyle != null) return;
            var disp = ChessFight.Game.RuntimePanels.DisplayFont;
            if (disp == null) disp = ChessFight.Game.RuntimePanels.KoreanFont;
            if (disp == null) disp = font;
            badgeStyle = new GUIStyle { font = disp, fontSize = 40, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            badgeStyle.normal.textColor = Color.white;
            var symbols = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Arial Unicode MS", "DejaVu Sans" }, 64);
            badgeSymbolStyle = new GUIStyle { font = symbols, fontSize = 40, alignment = TextAnchor.MiddleCenter };
            badgeSymbolStyle.normal.textColor = Color.white;
        }

        /// <summary>The speed lines (about 90 thin wedges, stronger outward, a clear middle 0.38 of the half size) and the
        /// badge's disc and ring (its shadow is the effects' soft dot); made once.</summary>
        void ImpactTextures()
        {
            if (texSpeedLines == null)
            {
                const int N = 1024;
                texSpeedLines = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[N * N];
                var rnd = new System.Random(64);
                const int Rays = 180;
                var rays = new float[Rays];
                for (int i = 0; i < Rays; i++) rays[i] = (float)rnd.NextDouble();
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = (Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 0.5f) * Rays;
                        int ray = Mathf.FloorToInt(a) % Rays;
                        if (rays[(ray + 13) % Rays] < 0.5f) continue;   // about 90 of the 180 slots carry a line
                        float within = a - Mathf.Floor(a), width = 0.06f + 0.22f * rays[ray];
                        float line = Mathf.Clamp01(1f - Mathf.Abs(within - 0.5f) / (width * 0.5f));
                        float start = 0.38f + 0.25f * rays[(ray + 7) % Rays];
                        float k = line * Mathf.Clamp01((r - start) / 0.15f) * Mathf.Clamp01((1.02f - r) / 0.05f + 1f);
                        px[y * N + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(k)));
                    }
                texSpeedLines.SetPixels32(px);
                texSpeedLines.Apply(false, true);
            }
            if (texDisc == null)
            {
                texDisc = RoundTex(128, (r, px) => Mathf.Clamp01((0.5f - r) * px));
                texRingBadge = RoundTex(128, (r, px) => Mathf.Clamp01((0.5f - r) * px) * Mathf.Clamp01((r - 0.44f) * px));
            }
        }

        /// <summary>A round white mask, its alpha from the distance to the middle (0..0.71 of the side) and the side in pixels.</summary>
        static Texture2D RoundTex(int n, System.Func<float, float, float> alpha)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(alpha(Mathf.Sqrt(dx * dx + dy * dy), n))));
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        void DestroyImpact()
        {
            UltSlowStop();
            PawnRushSkillBloom.UltK = PawnRushSkillBloom.UltPunch = 0f;
#if CF_URP
            if (impactHolder != null) Destroy(impactHolder);
            foreach (var p in lookProfiles) if (p != null) Destroy(p);
            if (impactProfile != null) Destroy(impactProfile);
#endif
            foreach (var t in new[] { texSpeedLines, texDisc, texRingBadge }) if (t != null) Destroy(t);
        }
    }
}
