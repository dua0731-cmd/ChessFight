using System.Collections.Generic;
using System.Globalization;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The HUD of the six driven ultimates' "!!" presentation (JY-skill-lab, 2026-11-22; the user: "궁극기 자체가 체스의
    /// 탁월한 수(!!)라는 컨셉"; the "!! Brilliant Move" spec's §6, §8, §10 and §11). It is drawn in OnGUI on the 720-px
    /// reference after the picture, so it is never graded and never bloomed, and every time in it is real time, so it keeps
    /// its pace through the slow motion and the hit stops.
    ///
    ///   the call       on the caster's screen only: the move typed in neon at the top, one tap a glyph, the skill's name
    ///                  under it, the !! stamped at its end; then both shrink into the move card
    ///   the top band   one big word at a time at y 192: the call, the grade word with its count, the words the ultimates
    ///                  queue (UltTopWord)
    ///   the eval bar   chess.com's bar at the left edge of every screen, the viewer's colour at the bottom: the caster's
    ///                  side behind at the key by its comeback, climbing with each capture, mate (M1) on a !!
    ///   the move card  the ultimate as notation over the bar ("3. e8=Q!!  e×d5  Q×c6"), its last move graded at the end
    ///   the clock      the caster's chess clock where the time bar was, red and ticking in the last 3 s
    ///   the badges     the hollow !! riding over the caster on the other screens (docked at the card's end on its own);
    ///                  at the judgement it flies to the to-square's corner and turns into the grade (!! ! ★ ?! ??)
    ///   pop words      each move's notation over its square, in the world, on every screen
    ///
    /// Presentation only: it reads the moments every PC is told and the caster's replicated plan time. Not seen in Unity yet.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        // ---------------------------------------------------------------- what it keeps

        /// <summary>One show's HUD (its UltShow.hud): the call, the eval, the move card, the clock and the badge.</summary>
        sealed class HudShow
        {
            public UltShow u;
            public bool black;
            // The call, the call with its stamped !!, the skill's name, the prefixes typed so far (made once), the seconds a
            // glyph, its width at 80 px and where it sits; the key's sounds played and the stamp seen.
            public string call = "", claim = "", name = "";
            public string[] typed;
            public float step = 0.055f, callW = -1f, callLeft, callRight;
            public int tapped;
            public bool swell, heart0, heart1, stamped, riser;
            // The eval in pawns from the caster's side, mate, the bar's sprung fill (white's share), its last tick, its way out.
            public float v, fill = 0.5f, fillVel, tickAt = -10f, outAt = -1f;
            public bool mate, judged, tickGood, sprung;
            public int labelKey = int.MinValue;
            public string label = "";
            // The move card: its number and prefix, the tokens (the call first), their widths, the first shown, the scroll.
            public int moveNo;
            public string prefix = "";
            public readonly List<string> tokens = new List<string>(8);
            public readonly List<float> widths = new List<float>(8);
            public bool dirty = true, cut;
            public int first;
            public float prefixW, ellipsisW, textW, scrollFrom, scrollAt = -10f, addedAt = -10f, cutAt = -1f;
            // The clock: the half seconds ticked, the time left shown, the DriveLine (read again ten times a second).
            public int tickHalf = int.MaxValue;
            public float shown = -1f, lineAt = -10f;
            public string line = "";
            // The badge: the riding one's world anchor, where it was drawn last (the flight's start), the card's dock and
            // the judgement (its real time and grade).
            public Vector3 ride;
            public bool rideOn, lastOn, fromOn, dockOn;
            public Vector2 last, from, dock;
            public float lastSize, fromSize, judgeAt = -1f;
            public int grade = -1;
        }

        /// <summary>A big word for the top band (the spec's §11): a show's grade word, or a word an ultimate queued.</summary>
        sealed class HudWord
        {
            public string text, sub;
            /// <summary>Its prefixes when it types glyph by glyph (the grade word), or null: all of it at once.</summary>
            public string[] typed;
            public Color colour;
            public int px;
            public float life, glyph, at = -1f, w = -1f;
            /// <summary>The show it belongs to (the grade word's), or null.</summary>
            public UltShow u;
        }

        /// <summary>A move's notation over its square in the world (the spec's §8).</summary>
        sealed class HudPop
        {
            public Text3D text;
            public UltShow u;
            public Vector3 at;
            public float age, fade = 1f, scale = 1f;
            public bool retiring;
        }

        // The places on the 720-px reference (the spec's §6 and §11; the card, its docked badge and its plate end by x 320,
        // the caps' item 12).
        const float HudBandY = 192f, HudUnderY = 238f;
        const float HudCardX = 14f, HudCardY = 172f, HudCardH = 24f, HudCardMax = 300f, HudGap = 9f, HudDock = 28f;
        const float HudBarX = 14f, HudBarTop = 200f, HudBarBottom = 500f, HudBarW = 14f;
        const float HudClockY = 528f, HudClockW = 150f, HudClockH = 34f, HudLineY = 515f;

        static readonly Color HudWhite = new Color(0.94f, 0.94f, 0.94f);
        static readonly Color HudBlack = new Color(0.25f, 0.24f, 0.22f);
        static readonly Color HudGrey = new Color(0.7f, 0.7f, 0.7f);
        static readonly Color HudMiss = new Color(0.6f, 0.6f, 0.62f);
        static readonly Color HudRed = new Color(0.75f, 0.1f, 0.12f);
        static readonly Color HudClockDark = new Color(0.08f, 0.09f, 0.1f);
        /// <summary>The move card's suffix by grade (★, the best move, has none in chess notation).</summary>
        static readonly string[] HudSuffix = { "!!", "!", "", "?!", "??" };
        /// <summary>The grade word's size by grade (px).</summary>
        static readonly int[] HudGradePx = { 88, 72, 64, 56, 56 };
        static readonly string[] HudChars = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", ":", "." };

        readonly int[] hudMoveNo = new int[2];
        readonly List<HudWord> hudQueue = new List<HudWord>(4);
        readonly List<HudPop> hudPops = new List<HudPop>(4);
        readonly GUIContent hudMeasure = new GUIContent();
        HudWord hudBand;
        GUIStyle hudCall, hudBig, hudClock, hudName, hudSub, hudCard, hudEval, hudLine;

        float HudUi => Mathf.Max(1e-3f, Screen.height / 720f);

        // ---------------------------------------------------------------- the moments (the lead calls these)

        /// <summary>The key, on every screen (the spec's §6): the show's HUD made - the call to type on the caster's screen,
        /// the eval bar's start, the move card's new line.</summary>
        void UltHudCast(UltShow u)
        {
            if (u == null || u.hud is HudShow) return;
            HudOf(u);
            UltEvalStart(u);
        }

        /// <summary>The notation typed so far, at the call's own pace (the allies' banner types it with it); from the stamp
        /// (0.70 s) the whole call with its !!.</summary>
        string UltBannerNota(UltShow u)
        {
            if (u == null) return "";
            if (!(u.hud is HudShow h) || h.typed == null) return u.nota ?? "";
            return h.stamped && !h.cut ? h.claim : h.typed[HudTypedCount(h)];
        }

        /// <summary>A big word for the top band at y 192 (the spec's §11: one at a time, queued after the call and the grade
        /// word; 1.6 s at most, the caps' item 5). At most three wait; the oldest gives way.</summary>
        void UltTopWord(string text, Color colour, int px = 56, float seconds = 1.4f)
        {
            if (string.IsNullOrEmpty(text)) return;
            int waiting = 0, oldest = -1;
            for (int i = 0; i < hudQueue.Count; i++)
                if (hudQueue[i].u == null)
                {
                    waiting++;
                    if (oldest < 0) oldest = i;
                }
            if (waiting >= 3 && oldest >= 0) hudQueue.RemoveAt(oldest);
            hudQueue.Add(new HudWord { text = text, colour = colour, px = Mathf.Clamp(px, 24, 88), life = Mathf.Clamp(seconds, 0.4f, 1.6f) });
        }

        /// <summary>The eval bar's start (the spec's §11): the caster's side behind by its comeback factor - the further
        /// behind its team was, the longer its ultimate and the worse the bar reads at the key.</summary>
        void UltEvalStart(UltShow u)
        {
            var h = HudOf(u);
            if (h == null) return;
            h.v = HudStartEval(u);
            h.mate = h.judged = h.sprung = false;
        }

        /// <summary>The eval moved by <paramref name="d"/> pawns for the caster's side (+1.3 for each enemy captured, the
        /// spec's §8), with a teal tick on the bar (grey for a loss).</summary>
        void UltEvalAdd(UltShow u, float d)
        {
            var h = HudOf(u);
            if (h == null || h.judged || Mathf.Abs(d) < 1e-4f) return;
            h.v += d;
            h.tickAt = RealClock;
            h.tickGood = d > 0f;
        }

        /// <summary>The eval at the judgement (the spec's §10): mate on !!, at least +3.5 on !, +1.5 on ★, 0.8 lost on ?!,
        /// 2.0 on ??; the bar leaves 3 s later.</summary>
        void UltEvalJudge(UltShow u)
        {
            var h = HudOf(u);
            if (h == null || h.judged || u.grade < 0) return;
            h.judged = true;
            switch (u.grade)
            {
                case RagdollPawn.GradeBrilliant: h.mate = true; break;
                case RagdollPawn.GradeGreat: h.v = Mathf.Max(h.v, 3.5f); break;
                case RagdollPawn.GradeBest: h.v = Mathf.Max(h.v, 1.5f); break;
                case RagdollPawn.GradeInaccuracy: h.v -= 0.8f; break;
                default: h.v -= 2f; break;
            }
            h.tickAt = RealClock;
            h.tickGood = u.grade <= RagdollPawn.GradeBest;
            h.outAt = RealClock + 3f;
        }

        /// <summary>A move of the ultimate, on every screen (the spec's §8): its notation pops up 2.5 m over the to-square
        /// and rises 0.5 m for 0.9 s (miss grey when <paramref name="quiet"/>), and it is written on the move card.</summary>
        void UltMove(UltShow u, string nota, Vector3 at, bool quiet = false)
        {
            if (u == null || string.IsNullOrEmpty(nota)) return;
            var h = HudOf(u);
            if (h.judgeAt < 0f && !h.cut)
            {
                h.tokens.Add(nota);
                h.dirty = true;
                h.addedAt = RealClock;
            }
            HudPopWord(u, nota, at, quiet);
        }

        /// <summary>The judgement's HUD, on every screen (the spec's §10): the grade word in the top band with the count
        /// under it, the move card's last move graded, the eval's verdict, and the riding badge flying to the to-square's
        /// corner (u.toCell, set before this) to turn into the grade.</summary>
        void UltHudJudge(UltShow u)
        {
            if (u == null || u.grade < 0) return;
            var h = HudOf(u);
            if (h.judgeAt >= 0f) return;
            int g = Mathf.Clamp(u.grade, 0, GradeBadges.Length - 1);
            h.grade = g;
            h.judgeAt = RealClock;
            h.from = h.last;
            h.fromSize = h.lastSize;
            h.fromOn = h.lastOn;
            // The last move's suffix; on a !! a check becomes mate when a king was struck.
            int last = h.tokens.Count - 1;
            if (last >= 0)
            {
                string t = h.tokens[last].TrimEnd('!', '?');
                if (g == RagdollPawn.GradeBrilliant && t.Length > 0 && t[t.Length - 1] == '+' && HudKingStruck(u)) t = t.Substring(0, t.Length - 1) + "#";
                h.tokens[last] = t + HudSuffix[g];
                h.dirty = true;
            }
            UltEvalJudge(u);
            string word = RagdollPawn.GradeName(g);
            var w = new HudWord { text = word, colour = GradeColors[g], px = HudGradePx[g], life = 1.6f, glyph = 0.03f, u = u, sub = HudScore(u) };
            w.typed = new string[word.Length + 1];
            for (int i = 0; i <= word.Length; i++) w.typed[i] = word.Substring(0, i);
            if (hudBand != null && hudBand.u == null) hudBand = null;   // (a queued word gives the band to the judgement)
            if (hudBand == null && !HudCallUp())
            {
                hudBand = w;
                w.at = RealClock;
            }
            else hudQueue.Insert(0, w);
        }

        /// <summary>A show cut short (the spec's §10): the move card's notation struck through as everything folds in 0.5 s,
        /// and no badge.</summary>
        void UltHudCut(UltShow u)
        {
            if (u == null || !(u.hud is HudShow h) || h.cut) return;
            h.cut = true;
            h.cutAt = RealClock;
            h.dirty = true;
        }

        // ---------------------------------------------------------------- every frame (LateUpdate)

        /// <summary>Every frame: the key's sounds on the caster's screen, the stamp's nudge, the bar's spring, the clock's
        /// ticks and the riser before the judgement, the riding badges' anchors, the pop words and the top band's queue.</summary>
        void UltHudStep()
        {
            if (ults.Count == 0)
            {
                if (hudPops.Count > 0 || hudBand != null || hudQueue.Count > 0) HudClear();
                return;
            }
            var me = Viewer;
            float now = RealClock;
            for (int i = 0; i < ults.Count; i++)
            {
                var u = ults[i];
                if (u.gone || !(u.hud is HudShow h)) continue;
                float t = now - u.t0Real;
                if (!h.cut && u.cut && u.endReal >= 0f)
                {
                    h.cut = true;
                    h.cutAt = u.endReal;
                    h.dirty = true;
                }
                bool caster = me != null && me == u.by;
                if (caster && !h.cut && effects) HudCallSounds(h, t);
                if (!h.stamped && t >= 0.70f)
                {
                    // The stamp (the spec's §6): the bar nudges +0.3 and the card's call carries its !!.
                    h.stamped = true;
                    if (!h.cut && !h.judged) h.v += 0.3f;
                    if (h.tokens.Count > 0 && h.tokens[0] == h.call)
                    {
                        h.tokens[0] = h.claim;
                        h.dirty = true;
                    }
                }
                HudSpring(h, t);
                HudClockStep(u, h, me, caster);
                h.rideOn = u.by != null && !caster && !h.cut && h.judgeAt < 0f && t >= 0.70f && u.Playing;
                if (h.rideOn) h.ride = HudHeadTop(u.by);
            }
            HudPopsStep();
            HudBandStep();
        }

        /// <summary>The key's sounds on the caster's screen (the spec's §6): SWELL from 0.10 s, cut dead at the stamp (played
        /// at its own pitch so the slow motion does not stretch it past 0.70 s), HEART at 0.12 and 0.45 s, a TAP for each
        /// glyph typed, a semitone higher each time.</summary>
        void HudCallSounds(HudShow h, float t)
        {
            float delay;
            if (HudDue(ref h.swell, t, 0.10f, out delay)) UltClip(UltClipId.Swell, 0.55f, 1f / Mathf.Max(0.1f, UltSfxPitch), delay);
            if (HudDue(ref h.heart0, t, 0.12f, out delay)) UltClip(UltClipId.Heart, 0.7f, 1f, delay);
            if (HudDue(ref h.heart1, t, 0.45f, out delay)) UltClip(UltClipId.Heart, 0.7f, 1f, delay);
            int typed = HudTypedCount(h);
            if (typed - h.tapped > 1) h.tapped = typed - 1;   // (a long frame: only the latest glyph taps)
            while (h.tapped < typed)
            {
                UltClip(UltClipId.Tap, 0.45f, Mathf.Pow(2f, h.tapped / 12f));
                h.tapped++;
            }
        }

        /// <summary>Whether a sound due <paramref name="due"/> s into the key starts this frame (a frame early, delayed to the
        /// moment, so it lands on time); one long past is skipped.</summary>
        bool HudDue(ref bool done, float t, float due, out float delay)
        {
            delay = 0f;
            if (done || t + realDt < due) return false;
            done = true;
            delay = Mathf.Max(0f, due - t);
            return t < due + 0.15f;
        }

        /// <summary>The bar's fill on a spring (the spec's §11: ω 18, ζ 0.55, about 12 % over and settled in 0.35 s), at its
        /// start value until it slides in.</summary>
        void HudSpring(HudShow h, float t)
        {
            float target = HudFillWhite(h);
            if (!h.sprung || t < 0.15f)
            {
                h.fill = target;
                h.fillVel = 0f;
                h.sprung = true;
                return;
            }
            const float W = 18f, Z = 0.55f, Sub = 1f / 120f;
            float dt = realDt;
            int n = Mathf.Clamp(Mathf.CeilToInt(dt / Sub), 1, 16);
            float sdt = dt / n;
            for (int k = 0; k < n; k++)
            {
                h.fillVel += (W * W * (target - h.fill) - 2f * Z * W * h.fillVel) * sdt;
                h.fill += h.fillVel * sdt;
            }
        }

        /// <summary>The clock (the spec's §7 and §11): the time left read from the caster's replicated plan time, a TICK every
        /// half second of the last 3 s on the caster's screen, and a RISER 0.4 s before the judgement on every screen (the
        /// caster at 0.5, an ally at 0.6 of it, an enemy at half, both falling with distance).</summary>
        void HudClockStep(UltShow u, HudShow h, RagdollPawn me, bool caster)
        {
            if (!u.Driven || u.by == null || h.cut || !u.Playing) return;
            float left = Mathf.Clamp(u.air - u.by.PlanTime, 0f, Mathf.Max(0.5f, u.air));
            // (A jump back up at the very end is the host's recovery resetting its plan time: the clock keeps its reading.)
            if (h.shown >= 0f && h.shown < 1.5f && left > h.shown + 1f) left = h.shown;
            h.shown = left;
            if (!effects) return;
            if (caster && left <= 3f && left > 0.02f)
            {
                int half = Mathf.CeilToInt(left / 0.5f) - 1;
                if (half < h.tickHalf)
                {
                    h.tickHalf = half;
                    UltClip(UltClipId.Tick, 0.35f);
                }
            }
            if (!h.riser && left <= 0.4f)
            {
                h.riser = true;
                float level = 0.5f;
                if (!caster) level *= (me != null && Teams.AreEnemies(me.Team, u.by.Team) ? 0.5f : 0.6f) * UltHeard(u.by.Hips.position);
                UltClip(UltClipId.Riser, level);
            }
            if (caster && RealClock - h.lineAt >= 0.1f)
            {
                h.lineAt = RealClock;
                h.line = DriveLine(u) ?? "";
            }
        }

        /// <summary>Where the riding badge hangs: over the caster's head (a grown piece's drawn head).</summary>
        Vector3 HudHeadTop(RagdollPawn p)
        {
            float s = GiantScaleOf(p);
            Vector3 head = HeadOf(p), feet = p.Hips.position - Vector3.up * p.standHeight;
            return feet + (head - feet) * s + Vector3.up * (0.3f * s);
        }

        /// <summary>The pop words: rising, fading, kept to the caps; gone with their show.</summary>
        void HudPopsStep()
        {
            if (hudPops.Count == 0) return;
            var cam = ViewCamera;
            for (int i = hudPops.Count - 1; i >= 0; i--)
            {
                var q = hudPops[i];
                q.age += realDt;
                if (q.retiring) q.fade -= realDt / 0.12f;
                bool gone = q.u == null || q.u.gone || !ults.Contains(q.u);
                if (gone || q.text == null || q.age >= 0.9f || q.fade <= 0f)
                {
                    if (q.text != null) q.text.Destroy();
                    hudPops.RemoveAt(i);
                    continue;
                }
                HudPlacePop(q, cam);
            }
        }

        /// <summary>The top band: the word up now ends (or goes with its show); the next waits while the caster's own call is
        /// up.</summary>
        void HudBandStep()
        {
            float now = RealClock;
            if (hudBand != null && (now - hudBand.at >= hudBand.life || (hudBand.u != null && (hudBand.u.gone || !ults.Contains(hudBand.u)))))
                hudBand = null;
            for (int i = hudQueue.Count - 1; i >= 0; i--)
            {
                var w = hudQueue[i];
                if (w.u != null && (w.u.gone || !ults.Contains(w.u))) hudQueue.RemoveAt(i);
            }
            if (hudBand == null && hudQueue.Count > 0 && !HudCallUp())
            {
                hudBand = hudQueue[0];
                hudQueue.RemoveAt(0);
                hudBand.at = now;
            }
        }

        /// <summary>Whether the call of the piece played here holds the top band now (the key to 1.8 s).</summary>
        bool HudCallUp()
        {
            var me = Viewer;
            if (me == null) return false;
            for (int i = 0; i < ults.Count; i++)
            {
                var u = ults[i];
                if (u.gone || u.by != me || !(u.hud is HudShow h) || h.cut) continue;
                if (RealClock - u.t0Real < 1.8f) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- OnGUI (720-px matrix set)

        /// <summary>Inside OnGUI (Repaint, the 720-px matrix set): one eval bar and its move card at the left edge, the
        /// caster's clock and call, the top band, then every show's badge in pixels.</summary>
        void UltHudGui(float vw)
        {
            if (!effects || ults.Count == 0 || Event.current == null || Event.current.type != EventType.Repaint) return;
            HudStyles();
            var keepColor = GUI.color;
            var keepMatrix = GUI.matrix;
            var me = Viewer;
            var cam = ViewCamera;
            float now = RealClock;
            // One bar on a screen: the viewer's own show's, else the latest show's.
            UltShow bar = null;
            bool barOwn = false;
            for (int i = 0; i < ults.Count; i++)
            {
                var u = ults[i];
                if (u.gone || !(u.hud is HudShow h)) continue;
                h.dockOn = false;
                if (!HudBarUp(u, h, now)) continue;
                bool own = me != null && u.by == me;
                if (bar == null || (own && !barOwn) || (own == barOwn && u.t0Real >= bar.t0Real))
                {
                    bar = u;
                    barOwn = own;
                }
            }
            if (bar != null) HudBarGui(bar, (HudShow)bar.hud, me, now);
            // The caster's own: its clock, and its call (no big words for a viewer standing in a warning, the caps' item 8).
            bool warnKnown = false, warned = false;
            if (me != null)
                for (int i = 0; i < ults.Count; i++)
                {
                    var u = ults[i];
                    if (u.gone || u.by != me || !(u.hud is HudShow h)) continue;
                    HudClockGui(u, h, vw, now);
                    float t = now - u.t0Real;
                    if (t < 0.12f || t >= 1.8f || (h.cut && now - h.cutAt >= 0.3f)) continue;
                    if (!warnKnown)
                    {
                        warned = ViewerUnderWarning();
                        warnKnown = true;
                    }
                    if (!warned) HudCallGui(u, h, vw, now);
                }
            if (hudBand != null && hudBand.at >= 0f)
            {
                if (!warnKnown) warned = ViewerUnderWarning();
                if (!warned) HudBandGui(hudBand, vw, now);
            }
            // The badges, in pixels (DrawBadge's).
            GUI.matrix = Matrix4x4.identity;
            for (int i = 0; i < ults.Count; i++)
            {
                var u = ults[i];
                if (u.gone || !(u.hud is HudShow h)) continue;
                HudBadgeGui(u, h, me, vw, now, cam);
            }
            GUI.matrix = keepMatrix;
            GUI.color = keepColor;
        }

        /// <summary>Whether a show's eval bar is on screen: in at +0.15 s, out 3 s after its judgement (0.25 s), or folded
        /// with a cut show (0.5 s).</summary>
        static bool HudBarUp(UltShow u, HudShow h, float now)
        {
            if (now - u.t0Real < 0.15f) return false;
            if (h.outAt >= 0f && now - h.outAt >= 0.25f) return false;
            return !(h.cut && now - h.cutAt >= 0.5f);
        }

        /// <summary>
        /// The eval bar (the spec's §11): chess.com's white and black, the viewer's colour at the bottom (white for a
        /// spectator), a 2 px teal divider on its sprung fill, the number at the leading side's end (M1 for mate), a tick
        /// at each change and a shimmer in the last 0.4 s; it slides in from the left edge and out again, the move card with
        /// it.
        /// </summary>
        void HudBarGui(UltShow u, HudShow h, RagdollPawn me, float now)
        {
            float t = now - u.t0Real, k = Smooth01((t - 0.15f) / 0.2f);
            if (h.outAt >= 0f) k *= 1f - Smooth01((now - h.outAt) / 0.25f);
            if (h.cut) k *= 1f - Smooth01((now - h.cutAt) / 0.5f);
            if (k <= 0.001f) return;
            float slide = -110f * (1f - k), alpha = Mathf.Clamp01(k * 1.5f);
            bool viewerWhite = me == null || me.Team != Teams.Black;
            float x = HudBarX + slide, top = HudBarTop, height = HudBarBottom - HudBarTop;
            float low = viewerWhite ? Mathf.Clamp01(h.fill) : 1f - Mathf.Clamp01(h.fill);
            HudRect(new Rect(x - 1.5f, top - 1.5f, HudBarW + 3f, height + 3f), UltInk, 0.75f * alpha);
            HudRect(new Rect(x, top, HudBarW, height), viewerWhite ? HudBlack : HudWhite, alpha);
            float div = top + height * (1f - low);
            HudRect(new Rect(x, div, HudBarW, top + height - div), viewerWhite ? HudWhite : HudBlack, alpha);
            HudRect(new Rect(x, div - 1f, HudBarW, 2f), Teal, alpha);
            float since = now - h.tickAt;
            if (since < 0.4f)
            {
                float aim = viewerWhite ? HudFillWhite(h) : 1f - HudFillWhite(h);
                HudRect(new Rect(x - 4f, top + height * (1f - aim) - 1.5f, HudBarW + 8f, 3f), h.tickGood ? Teal : HudMiss, alpha * (1f - since / 0.4f));
            }
            if (u.Driven && u.Playing && h.shown >= 0f && h.shown <= 0.4f)
            {
                // The shimmer before the judgement: a glint running up the bar.
                float sy = top + height * (1f - Mathf.Repeat(now * 5f, 1f));
                if (texSoft != null)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.55f * alpha);
                    GUI.DrawTexture(new Rect(x - 4f, sy - 22f, HudBarW + 8f, 44f), texSoft);
                }
                HudRect(new Rect(x, top, HudBarW, height), Color.white, 0.12f * alpha * (0.5f + 0.5f * Mathf.Sin(now * 40f)));
            }
            float white = h.black ? -h.v : h.v;
            bool whiteLeads = h.mate ? !h.black : white >= 0f, atBottom = whiteLeads == viewerWhite;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            Outlined(new Rect(x + HudBarW + 5f, atBottom ? HudBarBottom - 17f : top - 1f, 70f, 18f), HudEvalLabel(h, white), hudEval, HudWhite, 1f);
            HudCardGui(u, h, me, slide, alpha, now);
        }

        /// <summary>The bar's number (made again only when it changes): one decimal, or M1.</summary>
        static string HudEvalLabel(HudShow h, float white)
        {
            int key = h.mate ? int.MaxValue : Mathf.RoundToInt(Mathf.Abs(white) * 10f);
            if (key != h.labelKey)
            {
                h.labelKey = key;
                h.label = h.mate ? "M1" : (key / 10f).ToString("0.0", CultureInfo.InvariantCulture);
            }
            return h.label;
        }

        /// <summary>
        /// The move card over the bar (the spec's §11): 'N. ' for white, 'N... ' for black, then the latest five moves that
        /// fit (older ones scroll off to the left), the newest in white and the rest in grey, struck through for a cut show;
        /// on the caster's screen the call lands in it at 1.8 s and its riding badge docks at its end. Elsewhere the call
        /// types in with the banner.
        /// </summary>
        void HudCardGui(UltShow u, HudShow h, RagdollPawn me, float slide, float alpha, float now)
        {
            if (h.dirty) HudCardLayout(h, now);
            bool caster = me != null && me == u.by;
            float x = HudCardX + slide, y = HudCardY, t = now - u.t0Real;
            HudRect(new Rect(x - 5f, y - 1f, h.textW + 10f + (caster ? HudDock + 6f : 0f), HudCardH + 2f), Color.black, 0.38f * alpha);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            Outlined(new Rect(x, y, h.prefixW + 10f, HudCardH), h.prefix, hudCard, HudGrey, 1.2f);
            float cx = x + h.prefixW + 6f;
            if (h.first > 0)
            {
                GUI.color = new Color(1f, 1f, 1f, alpha);
                Outlined(new Rect(cx, y, h.ellipsisW + 10f, HudCardH), "…", hudCard, HudGrey, 1.2f);
                cx += h.ellipsisW + 4f;
            }
            float strike = cx;
            cx += h.scrollFrom * (1f - Smooth01((now - h.scrollAt) / 0.18f));
            int newest = h.tokens.Count - 1;
            for (int i = h.first; i <= newest; i++)
            {
                string s = h.tokens[i];
                float a = alpha;
                if (i == 0)
                {
                    if (caster) a *= Smooth01((t - 1.74f) / 0.06f);
                    else if (!h.stamped) s = h.typed[HudTypedCount(h)];
                }
                else if (i == newest) a *= Smooth01((now - h.addedAt) / 0.15f);
                if (a > 0.004f && s.Length > 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, a);
                    Outlined(new Rect(cx, y, h.widths[i] + 10f, HudCardH), s, hudCard, i == newest ? Color.white : HudGrey, 1.2f);
                }
                cx += h.widths[i] + HudGap;
            }
            float end = cx - HudGap;
            if (h.cut) HudRect(new Rect(strike - 2f, y + HudCardH * 0.5f - 1f, Mathf.Max(0f, end - strike + 4f), 2f), new Color(0.92f, 0.92f, 0.92f), alpha);
            h.dock = new Vector2(end + 6f + HudDock * 0.5f, y + HudCardH * 0.5f);
            h.dockOn = true;
        }

        /// <summary>The card's widths and the first move shown, measured again only when its moves change.</summary>
        void HudCardLayout(HudShow h, float now)
        {
            h.dirty = false;
            h.prefixW = HudWidth(hudCard, h.prefix);
            h.ellipsisW = HudWidth(hudCard, "…");
            h.widths.Clear();
            for (int i = 0; i < h.tokens.Count; i++) h.widths.Add(HudWidth(hudCard, h.tokens[i]));
            float room = HudCardMax - h.prefixW - 6f - HudDock - 6f;
            int first = Mathf.Max(0, h.tokens.Count - 5);
            float w = HudTokensWidth(h, first);
            while (first < h.tokens.Count - 1 && w + (first > 0 ? h.ellipsisW + 4f : 0f) > room)
            {
                first++;
                w = HudTokensWidth(h, first);
            }
            if (first > h.first)
            {
                float off = 0f;
                for (int i = h.first; i < first; i++) off += h.widths[i] + HudGap;
                h.scrollFrom = off;
                h.scrollAt = now;
            }
            h.first = first;
            h.textW = h.prefixW + 6f + (first > 0 ? h.ellipsisW + 4f : 0f) + w;
        }

        static float HudTokensWidth(HudShow h, int first)
        {
            float w = 0f;
            for (int i = first; i < h.widths.Count; i++) w += h.widths[i] + (i > first ? HudGap : 0f);
            return w;
        }

        /// <summary>
        /// The caster's chess clock where the time bar was (the spec's §11): a dark box 150 × 34 px at y 545 with the time
        /// left as m:ss.t in fixed cells (so the digits do not jitter), red in the last 3 s, showing 0:00.0 as it fades
        /// after the judgement; the DriveLine above it at y 515.
        /// </summary>
        void HudClockGui(UltShow u, HudShow h, float vw, float now)
        {
            if (!u.Driven || u.by == null || h.cut || h.shown < 0f) return;
            float alpha = Smooth01((now - u.t0Real - 0.15f) / 0.2f), left = h.shown;
            if (!u.Playing)
            {
                if (u.hitReal < 0f) return;
                alpha *= 1f - Smooth01((now - u.hitReal) / 0.3f);
                left = 0f;
            }
            if (alpha <= 0.004f) return;
            var box = new Rect(vw * 0.5f - HudClockW * 0.5f, HudClockY, HudClockW, HudClockH);
            HudRect(box, left <= 3f ? HudRed : HudClockDark, 0.9f * alpha);
            HudRect(new Rect(box.x, box.y, box.width, 1f), Color.white, 0.22f * alpha);
            int tenths = Mathf.Clamp(Mathf.FloorToInt(left * 10f), 0, 5999), s = tenths / 10 % 60;
            const float D = 16f, P = 8f;
            float x = vw * 0.5f - (4f * D + 2f * P) * 0.5f;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            x = HudDigit(x, D, HudChars[tenths / 600]);
            x = HudDigit(x, P, HudChars[10]);
            x = HudDigit(x, D, HudChars[s / 10]);
            x = HudDigit(x, D, HudChars[s % 10]);
            x = HudDigit(x, P, HudChars[11]);
            HudDigit(x, D, HudChars[tenths % 10]);
            if (u.Playing && h.line.Length > 0)
            {
                GUI.color = new Color(1f, 1f, 1f, alpha);
                Outlined(new Rect(vw * 0.5f - 330f, HudLineY - 13f, 660f, 26f), h.line, hudLine, new Color(0.9f, 1f, 0.97f), 1.5f);
            }
        }

        float HudDigit(float x, float w, string c)
        {
            GUI.Label(new Rect(x, HudClockY, w, HudClockH), c, hudClock);
            return x + w;
        }

        /// <summary>
        /// The call on the caster's screen (the spec's §6): typed from 0.12 s at y 192 in 80 px neon - a white core, a 3 px
        /// teal glow, a 1.5 px ink edge - centred, or ending short of the stamp's badge when it is long; the skill's name
        /// in white at y 238 from 0.30 s; at 1.6-1.8 s it shrinks into its place on the move card.
        /// </summary>
        void HudCallGui(UltShow u, HudShow h, float vw, float now)
        {
            float t = now - u.t0Real, alpha = 1f;
            if (h.cut) alpha = 1f - Smooth01((now - h.cutAt) / 0.3f);
            if (alpha <= 0.004f) return;
            HudCallMeasure(h, vw);
            float k = Smooth01((t - 1.6f) / 0.2f);
            Vector2 at = new Vector2(h.callLeft, HudBandY);
            if (k > 0f)
            {
                if (h.dirty) HudCardLayout(h, now);
                at = Vector2.Lerp(at, new Vector2(HudCardX + h.prefixW + 6f, HudCardY + HudCardH * 0.5f), k);
            }
            float a = alpha * (1f - Smooth01((t - 1.72f) / 0.08f));
            var keep = GUI.matrix;
            if (k > 0f)
            {
                float s = Mathf.Lerp(1f, 18f / 80f, k);
                GUIUtility.ScaleAroundPivot(new Vector2(s, s), at * HudUi);
            }
            HudNeon(new Rect(at.x, at.y - 60f, h.callW + 40f, 120f), h.typed[HudTypedCount(h)], hudCall, Color.white, Teal, a, 80f);
            GUI.matrix = keep;
            float na = alpha * Smooth01((t - 0.30f) / 0.15f) * (1f - Smooth01((t - 1.5f) / 0.15f));
            if (na > 0.004f && h.name.Length > 0)
            {
                GUI.color = new Color(1f, 1f, 1f, na);
                Outlined(new Rect(h.callLeft + h.callW * 0.5f - 300f, HudUnderY - 22f, 600f, 44f), h.name, hudName, Color.white, 1.5f);
            }
        }

        /// <summary>The call's width (measured once) and its place: centred, but ending at least 6 px short of the stamp's
        /// badge at the middle + 230 px (Impact.cs's StampAnchor, 0.27·H at its widest).</summary>
        void HudCallMeasure(HudShow h, float vw)
        {
            if (h.callW < 0f) h.callW = HudWidth(hudCall, h.call);
            h.callRight = Mathf.Min(vw * 0.5f + h.callW * 0.5f, vw * 0.5f + 230f - 0.135f * 720f - 6f);
            h.callLeft = h.callRight - h.callW;
        }

        /// <summary>A top-band word (the spec's §10 and §11): typed glyph by glyph or all at once, in neon in its colour, a
        /// small pop as it comes, fading over its last 0.25 s; a grade word's count under it at y 238.</summary>
        void HudBandGui(HudWord w, float vw, float now)
        {
            float a = now - w.at;
            if (a < 0f || a >= w.life) return;
            hudBig.fontSize = w.px;
            if (w.w < 0f) w.w = HudWidth(hudBig, w.text);
            string text = w.text;
            if (w.typed != null && w.glyph > 0f) text = w.typed[Mathf.Min(w.typed.Length - 1, 1 + Mathf.FloorToInt(a / w.glyph))];
            float fade = 1f - Smooth01((a - (w.life - 0.25f)) / 0.25f), alpha = Mathf.Clamp01(a / 0.05f) * fade;
            float pop = 1f + 0.15f * (1f - Smooth01(a / 0.12f));
            var keep = GUI.matrix;
            if (pop > 1.001f) GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), new Vector2(vw * 0.5f, HudBandY) * HudUi);
            HudNeon(new Rect(vw * 0.5f - w.w * 0.5f, HudBandY - w.px, w.w + 40f, w.px * 2f), text, hudBig, Color.Lerp(w.colour, Color.white, 0.55f), w.colour, alpha, w.px);
            GUI.matrix = keep;
            if (!string.IsNullOrEmpty(w.sub))
            {
                float sa = Smooth01((a - 0.25f) / 0.15f) * fade;
                if (sa <= 0.004f) return;
                GUI.color = new Color(1f, 1f, 1f, sa);
                Outlined(new Rect(vw * 0.5f - 300f, HudUnderY - 18f, 600f, 36f), w.sub, hudSub, Color.white, 1.5f);
            }
        }

        /// <summary>
        /// A show's badge (the spec's N16 and §10), in pixels. On the caster's screen: the stamp's !! in the brilliant colour
        /// settling to 0.13·H at the end of the call, then shrinking into the move card's end (hollow there). On the other
        /// screens: the hollow !! riding at the caster's top-right from the stamp, 22-64 px. After the judgement, the
        /// judgement's badge. World-anchored badges hide behind the camera and keep out of the middle (HudKeepOut).
        /// </summary>
        void HudBadgeGui(UltShow u, HudShow h, RagdollPawn me, float vw, float now, Camera cam)
        {
            bool caster = me != null && me == u.by;
            if (h.judgeAt >= 0f)
            {
                HudJudgeBadge(u, h, caster, vw, now, cam);
                return;
            }
            float t = now - u.t0Real;
            h.lastOn = false;
            if (h.cut || t < 0.70f) return;
            if (caster)
            {
                HudCallMeasure(h, vw);
                float settle = Smooth01((t - 0.733f) / 0.25f), k = Smooth01((t - 1.6f) / 0.2f);
                Vector2 c = Vector2.Lerp(new Vector2(vw * 0.5f + 230f, HudBandY), new Vector2(h.callRight + 6f + 0.065f * 720f, HudBandY), settle);
                float size = Mathf.Lerp(150f, 0.13f * 720f, settle);   // (150 px at most, the caps' item 12)
                if (k > 0f)
                {
                    Vector2 dock = h.dockOn ? h.dock : new Vector2(HudCardX + 60f, HudCardY + HudCardH * 0.5f);
                    c = Vector2.Lerp(c, dock, k);
                    size = Mathf.Lerp(size, HudDock, k);
                }
                HudBadgeAt(c, size, GradeColors[0], "!!", false, 1f - k);
                HudBadgeAt(c, size, Teal, "!!", true, k);
                h.last = c;
                h.lastSize = size;
                h.lastOn = true;
                return;
            }
            if (!h.rideOn || cam == null || !HudProject(cam, h.ride, out var p, out float ppm)) return;
            float sz = Mathf.Clamp(2.2f * ppm, 22f, 64f), pop = Pop(t - 0.70f, 0.12f);
            p += new Vector2(0.5f * sz + 0.15f * ppm, -(0.5f * sz + 0.1f * ppm));
            HudKeepOut(ref p, sz, false, vw);
            HudBadgeAt(p, Mathf.Max(22f, sz * (0.6f + 0.4f * pop)), Teal, "!!", true, pop * u.Life(Clock));
            h.last = p;
            h.lastSize = sz;
            h.lastOn = true;
        }

        /// <summary>
        /// The judgement's badge (the spec's §10): the riding !! flies for 0.25 s on a 1.5 m arc to the to-square's top-right
        /// corner as this camera sees it and turns into the grade at the 2.2 m equivalent (48-150 px): !! slams ×1 → ×2.2 →
        /// ×1 over 0.2 s (at most 150 px, the caps' item 12), the others flip over 0.18 s, ?? then drops 0.6 m with a
        /// bounce; held 3 s, fading over the last 0.6 s.
        /// </summary>
        void HudJudgeBadge(UltShow u, HudShow h, bool caster, float vw, float now, Camera cam)
        {
            float a = now - h.judgeAt;
            h.lastOn = false;
            if (a >= 3f || cam == null || h.grade < 0) return;
            if (!HudProject(cam, HudCorner(u, cam), out var dst, out float ppm)) return;
            float size = Mathf.Clamp(2.2f * ppm, 48f, 150f);
            HudKeepOut(ref dst, size, caster, vw);
            const float Fly = 0.25f;
            bool flies = h.fromOn;
            float k = flies ? Smooth01(a / Fly) : 1f;
            Vector2 c = flies ? Vector2.Lerp(h.from, dst, k) : dst;
            float sz = flies ? Mathf.Lerp(h.fromSize, size, k) : size;
            if (flies && k < 1f)
            {
                c.y -= 1.5f * ppm * Mathf.Sin(Mathf.PI * k);
                HudKeepOut(ref c, sz, caster, vw);
            }
            float alpha = 1f - Smooth01((a - 2.4f) / 0.6f), land = a - (flies ? Fly : 0f);
            int g = h.grade;
            if (land < 0f) HudBadgeAt(c, sz, Teal, "!!", true, alpha);
            else if (g == RagdollPawn.GradeBrilliant)
            {
                float slam = land < 0.2f ? 1f + 1.2f * Mathf.Sin(Mathf.PI * land / 0.2f) : 1f;
                HudBadgeAt(c, Mathf.Min(150f, sz * slam), GradeColors[g], GradeBadges[g], false, alpha);
            }
            else
            {
                float flip = Mathf.Clamp01(land / 0.18f), sx = Mathf.Abs(Mathf.Cos(Mathf.PI * flip));
                if (g == RagdollPawn.GradeBlunder && land > 0.18f)
                {
                    c.y += 0.6f * ppm * HudBounce(Mathf.Clamp01((land - 0.18f) / 0.5f));
                    HudKeepOut(ref c, sz, caster, vw);
                }
                if (sx > 0.02f)
                {
                    var keep = GUI.matrix;
                    if (sx < 0.999f) GUIUtility.ScaleAroundPivot(new Vector2(sx, 1f), c * HudUi);
                    if (flip >= 0.5f) HudBadgeAt(c, sz, GradeColors[g], GradeBadges[g], false, alpha);
                    else HudBadgeAt(c, sz, Teal, "!!", true, alpha);
                    GUI.matrix = keep;
                }
            }
            h.last = c;
            h.lastSize = sz;
            h.lastOn = true;
        }

        /// <summary>The to-square's corner nearest the top-right of this camera's view, at the judgement's floor height.</summary>
        Vector3 HudCorner(UltShow u, Camera cam)
        {
            Vector3 c = CellFlat(u.toCell);
            c.y = u.hitAt >= 0f ? u.hitPoint.y : u.castAt.y;
            float half = CellSize * 0.5f;
            Vector3 r = cam.transform.right, f = cam.transform.forward;
            c.x += r.x + f.x >= 0f ? half : -half;
            c.z += r.z + f.z >= 0f ? half : -half;
            return c;
        }

        // ---------------------------------------------------------------- pieces

        HudShow HudOf(UltShow u)
        {
            if (u == null) return null;
            if (u.hud is HudShow have) return have;
            var h = new HudShow { u = u, black = u.black };
            h.call = UltCallNota(u.skill, u.black) ?? "";
            h.claim = h.call + "!!";
            h.name = UltCallName(u.skill) ?? "";
            int n = h.call.Length;
            h.typed = new string[n + 1];
            for (int i = 0; i <= n; i++) h.typed[i] = h.call.Substring(0, i);
            h.step = n > 0 ? Mathf.Min(0.055f, 0.56f / n) : 0.055f;
            h.moveNo = ++hudMoveNo[u.black ? 1 : 0];
            h.prefix = u.black ? h.moveNo + "..." : h.moveNo + ".";
            h.tokens.Add(h.call);
            h.v = HudStartEval(u);
            u.hud = h;
            return h;
        }

        /// <summary>The glyphs of the call typed now: one every min(0.055, 0.56/length) s from 0.12 s, so it is done before
        /// the stamp.</summary>
        int HudTypedCount(HudShow h)
        {
            float t = RealClock - h.u.t0Real - 0.12f;
            if (t < 0f) return 0;
            return Mathf.Min(h.typed.Length - 1, 1 + Mathf.FloorToInt(t / Mathf.Max(0.001f, h.step)));
        }

        /// <summary>The start value for the caster's side (the spec's §11) from its comeback factor r = (time - the fixed
        /// part) / the base seconds RagdollPawn.(ultimate).cs gives BeginDrive: -5 from r 1.75, -3 from 1.25, -1.5 from
        /// 0.75, +0.5 below.</summary>
        float HudStartEval(UltShow u)
        {
            float r = HudComeback(u);
            return r >= 1.75f ? -5f : r >= 1.25f ? -3f : r >= 0.75f ? -1.5f : 0.5f;
        }

        float HudComeback(UltShow u)
        {
            var s = Params;
            if (s == null || !u.Driven) return 1f;
            float fixedPart = 0f, baseSeconds;
            switch (u.skill)
            {
                case PawnRushSkillIds.Nightmare: baseSeconds = s.nmSeconds; break;
                case PawnRushSkillIds.GreatDiagonal: baseSeconds = s.gdSeconds; break;
                case PawnRushSkillIds.PromotionRun:
                    baseSeconds = s.prQueen;
                    fixedPart = s.prRun + s.prLeap;   // (what RagdollPawn.PromotionRun.cs passes BeginDrive as its fixed part)
                    break;
                case PawnRushSkillIds.RookRoll: baseSeconds = s.rrSeconds; break;
                case PawnRushSkillIds.QueenDescent: baseSeconds = s.qdSeconds; break;
                default: return 1f;
            }
            return baseSeconds > 0.01f ? (u.air - fixedPart) / baseSeconds : 1f;
        }

        /// <summary>White's share of the bar: 0.5 + 0.5·tanh(v/5) within 0.03-0.97; mate fills it for the caster's side.</summary>
        static float HudFillWhite(HudShow h)
        {
            if (h.mate) return h.black ? 0.03f : 0.97f;
            float white = h.black ? -h.v : h.v;
            return Mathf.Clamp(0.5f + 0.5f * (float)System.Math.Tanh(white / 5f), 0.03f, 0.97f);
        }

        static bool HudKingStruck(UltShow u)
        {
            foreach (var p in u.struckSet) if (p != null && p.Piece == PieceKind.King) return true;
            return false;
        }

        /// <summary>The count under the grade word: the enemies hit and their material (P 1, N 3, B 3, R 5, Q 9, K 0).</summary>
        static string HudScore(UltShow u)
        {
            int material = 0;
            foreach (var p in u.struckSet)
                if (p != null)
                    material += p.Piece switch
                    {
                        PieceKind.Pawn => 1,
                        PieceKind.Knight => 3,
                        PieceKind.Bishop => 3,
                        PieceKind.Rook => 5,
                        PieceKind.Queen => 9,
                        _ => 0,
                    };
            return $"적 {u.hitCount}명 · +{material}";
        }

        /// <summary>A pop word (the spec's §8), within the caps' item 9: three on screen, two of them within 20 m (the oldest
        /// gives way, fading in 0.12 s).</summary>
        void HudPopWord(UltShow u, string nota, Vector3 at, bool quiet)
        {
            var cam = ViewCamera;
            Vector3 p = at + Vector3.up * 2.5f, eye = cam != null ? cam.transform.position : p + Vector3.one * 100f;
            bool near = (eye - p).sqrMagnitude < 400f;
            int live = 0, nearLive = 0;
            HudPop oldestNear = null;
            for (int i = 0; i < hudPops.Count; i++)
            {
                var q = hudPops[i];
                if (q.retiring) continue;
                live++;
                if ((eye - q.at).sqrMagnitude < 400f)
                {
                    nearLive++;
                    if (oldestNear == null) oldestNear = q;
                }
            }
            if (near && nearLive >= 2 && oldestNear != null)
            {
                oldestNear.retiring = true;
                live--;
            }
            if (live >= 3)
                for (int i = 0; i < hudPops.Count; i++)
                    if (!hudPops[i].retiring)
                    {
                        hudPops[i].retiring = true;
                        break;
                    }
            var pop = new HudPop { text = new Text3D(root, font, nota, quiet ? HudMiss : HdrA(TealPale, 1.3f), 0.6f), u = u, at = p };
            hudPops.Add(pop);
            HudPlacePop(pop, cam);
        }

        /// <summary>A pop word placed: rising 0.5 m over its 0.9 s and fading over the last 0.3 s; 0.6 m tall (growing in,
        /// never over) inside the centre clear zone, up to 2.2 times that far away outside it (the caps' item 5); faded
        /// close to the camera (the caps' item 7).</summary>
        void HudPlacePop(HudPop q, Camera cam)
        {
            Vector3 p = q.at + Vector3.up * (0.5f * Smooth01(q.age / 0.9f));
            float near = 1f, want = 1f;
            if (cam != null)
            {
                float d = Vector3.Distance(cam.transform.position, p);
                near = Mathf.Clamp01((d - 1.2f) / 2.5f);
                if (!HudInZone(cam.WorldToViewportPoint(p))) want = Mathf.Clamp(d / 15f, 1f, 2.2f);
            }
            q.scale = q.age <= 0f ? want : Mathf.MoveTowards(q.scale, want, realDt * 6f);
            float grow = 1f - 0.3f * Mathf.Exp(-q.age / 0.05f);
            float alpha = Mathf.Clamp01((0.9f - q.age) / 0.3f) * Mathf.Clamp01(q.fade) * near;
            q.text.Place(p, cam, q.scale * grow, alpha);
        }

        /// <summary>Whether a viewport point is in the caps' centre clear zone (an ellipse over x 30-70 %, y 36-80 % from the
        /// top).</summary>
        static bool HudInZone(Vector3 viewport)
        {
            if (viewport.z <= 0f) return false;
            float dx = (viewport.x - 0.5f) / 0.2f, dy = (1f - viewport.y - 0.58f) / 0.22f;
            return dx * dx + dy * dy < 1f;
        }

        /// <summary>A badge kept out of the middle (720-px reference): out of the caps' centre clear zone grown by its half
        /// size on every screen, and on the caster's own also out of a circle of 0.25·H round the middle (the spec's N16);
        /// then on the screen.</summary>
        static void HudKeepOut(ref Vector2 p, float size, bool caster, float vw)
        {
            float r = size * 0.5f, ax = vw * 0.2f + r, ay = 720f * 0.22f + r;
            Vector2 c = new Vector2(vw * 0.5f, 720f * 0.58f), d = p - c;
            float e = d.x / ax * (d.x / ax) + d.y / ay * (d.y / ay);
            if (e < 1f) p = c + (e > 1e-6f ? d / Mathf.Sqrt(e) : new Vector2(0f, -ay));
            if (caster)
            {
                Vector2 m = new Vector2(vw * 0.5f, 360f), q = p - m;
                float need = 0.25f * 720f + r, len = q.magnitude;
                if (len < need) p = m + (len > 1e-3f ? q / len : new Vector2(0f, -1f)) * need;
            }
            p.x = Mathf.Clamp(p.x, r, Mathf.Max(r, vw - r));
            p.y = Mathf.Clamp(p.y, r, 720f - r);
        }

        /// <summary>A world point on the 720-px reference and how many reference pixels a metre is there; false behind the
        /// camera.</summary>
        static bool HudProject(Camera cam, Vector3 world, out Vector2 p, out float ppm)
        {
            p = Vector2.zero;
            ppm = 0f;
            Vector3 v = cam.WorldToScreenPoint(world);
            if (v.z <= 0.1f) return false;
            float ui = Mathf.Max(1e-3f, Screen.height / 720f);
            p = new Vector2(v.x / ui, (Screen.height - v.y) / ui);
            float perMetre = cam.orthographic ? cam.pixelHeight / (2f * Mathf.Max(1e-3f, cam.orthographicSize))
                                              : cam.pixelHeight / (2f * v.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            ppm = perMetre / ui;
            return true;
        }

        /// <summary>DrawBadge (Impact.cs, pixels) from the 720-px reference.</summary>
        void HudBadgeAt(Vector2 c, float size, Color disc, string glyph, bool hollow, float alpha)
        {
            if (alpha <= 0.004f || size <= 1f) return;
            float ui = HudUi;
            DrawBadge(c * ui, size * ui, disc, glyph, hollow, Mathf.Clamp01(alpha));
        }

        /// <summary>A bounce settling at 1 (the ?? badge's drop).</summary>
        static float HudBounce(float x)
        {
            const float N = 7.5625f, D = 2.75f;
            if (x < 1f / D) return N * x * x;
            if (x < 2f / D)
            {
                x -= 1.5f / D;
                return N * x * x + 0.75f;
            }
            if (x < 2.5f / D)
            {
                x -= 2.25f / D;
                return N * x * x + 0.9375f;
            }
            x -= 2.625f / D;
            return N * x * x + 0.984375f;
        }

        /// <summary>Neon lettering (the spec's §6): an ink edge 1.5 px beyond a glow 3 px round a bright core, at 80 px and in
        /// step with the size.</summary>
        static void HudNeon(Rect r, string text, GUIStyle st, Color core, Color glow, float alpha, float px)
        {
            if (alpha <= 0.004f || string.IsNullOrEmpty(text)) return;
            float ink = px * (4.5f / 80f), halo = px * (3f / 80f);
            GUI.color = new Color(UltInk.r, UltInk.g, UltInk.b, UltInk.a * alpha);
            HudRing(r, text, st, ink);
            GUI.color = new Color(glow.r, glow.g, glow.b, 0.6f * alpha);
            HudRing(r, text, st, halo);
            HudRing(r, text, st, halo * 0.5f);
            GUI.color = new Color(core.r, core.g, core.b, alpha);
            GUI.Label(r, text, st);
        }

        static void HudRing(Rect r, string text, GUIStyle st, float radius)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * radius, r.y + Mathf.Sin(a) * radius, r.width, r.height), text, st);
            }
        }

        static void HudRect(Rect r, Color c, float alpha)
        {
            GUI.color = new Color(c.r, c.g, c.b, c.a * alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        float HudWidth(GUIStyle st, string text)
        {
            hudMeasure.text = text;
            return st.CalcSize(hudMeasure).x;
        }

        void HudStyles()
        {
            if (hudCall != null) return;
            var disp = ChessFight.Game.RuntimePanels.DisplayFont;
            var body = ChessFight.Game.RuntimePanels.KoreanFont;
            if (body == null) body = font;
            if (disp == null) disp = body;
            hudCall = HudStyle(disp, 80, TextAnchor.MiddleLeft, FontStyle.Normal);
            hudBig = HudStyle(disp, 56, TextAnchor.MiddleLeft, FontStyle.Normal);
            hudClock = HudStyle(disp, 26, TextAnchor.MiddleCenter, FontStyle.Normal);
            hudName = HudStyle(body, 30, TextAnchor.MiddleCenter, FontStyle.Bold);
            hudSub = HudStyle(body, 24, TextAnchor.MiddleCenter, FontStyle.Bold);
            hudCard = HudStyle(body, 18, TextAnchor.MiddleLeft, FontStyle.Bold);
            hudEval = HudStyle(body, 14, TextAnchor.MiddleLeft, FontStyle.Bold);
            hudLine = HudStyle(body, 20, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        static GUIStyle HudStyle(Font f, int size, TextAnchor anchor, FontStyle style)
        {
            var s = new GUIStyle { font = f, fontSize = size, alignment = anchor, fontStyle = style, clipping = TextClipping.Overflow, wordWrap = false };
            s.normal.textColor = Color.white;
            return s;
        }

        void HudClear()
        {
            for (int i = 0; i < hudPops.Count; i++)
                if (hudPops[i].text != null) hudPops[i].text.Destroy();
            hudPops.Clear();
            hudQueue.Clear();
            hudBand = null;
        }

        /// <summary>This object going: the pop words and the queued words gone (each show's own HUD goes with its show).</summary>
        void DestroyHud() => HudClear();
    }
}
