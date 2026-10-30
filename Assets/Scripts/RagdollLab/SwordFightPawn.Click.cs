using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class SwordFightPawn
    {
        // R48 click-swing timings and arc, kept separately for live A/B testing. Since 10-25 every piece swings its own way
        // (SwingStyle below); these stay the queen's timings - the pace the skill test bed times its dummies by.
        public const float Windup = .16f, ActiveTime = .20f, Recovery = .34f;
        public const float SwingDuration = Windup + ActiveTime + Recovery;
        /// <summary>10-28 (승규 님, a "sword swipes" clip: "좌클릭을 누를 때마다 한 가지 동작으로 칼을 휘두르는게 아니라 여러
        /// 방면으로"): each piece's click is a combo of four cuts in different directions (Combos below; the fourth from 10-29). A click during a
        /// cut or within this long after it takes the next one; a longer pause starts over at the piece's own first cut.</summary>
        public const float ComboWindow = .6f;
        public float SwingAge { get; private set; } = 99f;
        /// <summary>Which cut of its piece's combo the click cut is (0 = the piece's own first cut).</summary>
        public int ComboStep { get; private set; }
        Vector3 swingDirection = Vector3.forward, bufferedAim, previousGrip, previousClickTip;
        readonly HashSet<SwordFightPawn> hitThisSwing = new HashSet<SwordFightPawn>();
        float inputBuffer, sinceSwing = 99f;
        int comboPiece = -1;
        bool wasActive;
        // 10-28 (승규 님: "관절이나 동작을 더 자연스럽게 이어가게"): this cut's windup - its style's, plus for a cut chained on
        // from the one before what was left of that one's recovery - and where the blade, arm, chest and body were when it
        // started: it winds up from there instead of from the guard.
        float windup = Windup, fromLean, fromTwist, fromRoll, fromBody;
        Vector3 fromLine, fromArm;
        // 10-28 (승규 님: "그에 따라서 왼팔 동작도 자연스럽게"): the free arm through the click cuts (OffArmLine, the chest's
        // frame) and how far it is posed over the walk's own (0..1); how fast the chest turns about up (deg/s, + toward the
        // sword hand); and, on a machine that only sees this fighter, when its blade last moved like a cut and how far the
        // free arm is drawn for it.
        Vector3 offArm = Vector3.left, offArmSpeed;
        float offArmOn, lastYaw, yawRate, remoteCut, remoteOffArm;
        /// <summary>10-29: how far the free hand is on the hilt for a two-handed cut (SwingStyle.TwoHands), 0..1.</summary>
        float twoHandsOn;
        Quaternion lastRemoteTurn = Quaternion.identity;
        float CutLength => windup + style.Active + style.Recovery;
        /// <summary>How far a chained cut's way round to its next windup bows back toward the guard (none from the guard;
        /// at 1 its curve is drawn by the guard, about halfway back - less and the blade hung near where the last cut ended).</summary>
        float Settle => Mathf.Clamp01((windup - style.Windup) / .3f);

        void CancelClickSwing()
        { inputBuffer = 0; SwingAge = 99f; sinceSwing = 99f; wasActive = false; hitThisSwing.Clear(); Pawn.BodyTurn = 0f; }

        /// <summary>A click while a cut runs waits for its blade to go through (the next cut of the combo) instead of being
        /// dropped.</summary>
        float ClickBuffer => .13f + (ClassicControls && Attacking ? Mathf.Max(0f, windup + style.Active - SwingAge) : 0f);

        /// <summary>A skill takes the sword: a click cut still running stops (without the step's input dropped, as StopCombat does).</summary>
        internal void CancelSwing() { if (ClassicControls) CancelClickSwing(); }

        void StepClickSwing(float dt)
        {
            if (Pawn.State != PawnState.Active) { CancelClickSwing(); StepOffArm(dt); return; }
            // A waiting click starts the next cut once this one's blade has gone through; its windup takes over the rest of
            // the recovery, so it lands when it did before, but the blade goes on round to it instead of back to the guard.
            bool through = Attacking && SwingAge >= windup + style.Active;
            if (inputBuffer > 0 && (!Attacking || through))
            {
                fromLine = Attacking ? SwingLine(SwingAge) : IdleLine();
                fromArm = ArmLine(ClickBladeDirection());
                ChestChannels(out fromLean, out fromTwist, out fromRoll);
                fromBody = Attacking ? Pawn.BodyTurn : 0f;
                float carried = through ? CutLength - SwingAge : 0f;
                bufferedAim.y = 0;
                swingDirection = bufferedAim.sqrMagnitude > .01f ? bufferedAim.normalized : Pawn.Facing;
                var combo = ComboOf(Pawn.Piece);
                ComboStep = (Attacking || sinceSwing <= ComboWindow) && comboPiece == (int)Pawn.Piece ? (ComboStep + 1) % combo.Length : 0;
                comboPiece = (int)Pawn.Piece;
                style = combo[ComboStep];
                windup = style.Windup + carried;
                SwingAge = 0; Swings++; inputBuffer = 0; Protection = 0;
                hitThisSwing.Clear(); wasActive = false;
            }
            inputBuffer = Mathf.Max(0, inputBuffer - dt);
            if (!Attacking) { Pawn.BodyTurn = 0f; sinceSwing += dt; StepOffArm(dt); return; }
            sinceSwing = 0f;
            SwingAge += dt;
            Pawn.BodyTurn = SwingPhase(fromBody, style.BodyUp, style.BodyThrough);
            bool active = SwingAge >= windup && SwingAge <= windup + style.Active;
            Vector3 grip = ClickGrip(), tip = grip + ClickBladeDirection() * .88f;
            if (active)
            {
                if (!wasActive) { previousGrip = grip; previousClickTip = tip; SwingSound(); }
                for (int s = 0; s <= 3; s++)
                    DetectClickHit(Vector3.Lerp(previousGrip, grip, s / 3f), Vector3.Lerp(previousClickTip, tip, s / 3f));
            }
            previousGrip = grip; previousClickTip = tip; wasActive = active;
            StepOffArm(dt);
        }

        /// <summary>The free arm through the cuts: taken over from the walk as a cut starts and handed back once the last
        /// one is done, and between them eased after where it should be (a moment behind, as a loose arm is).</summary>
        void StepOffArm(float dt)
        {
            offArmOn = Mathf.MoveTowards(offArmOn, Attacking ? 1f : 0f, dt / (Attacking ? .2f : .3f));
            // On the hilt from the windup's start through the cut, let go early in the recovery.
            bool both = Attacking && style.TwoHands && SwingAge < windup + style.Active + style.Recovery * .35f;
            twoHandsOn = Mathf.MoveTowards(twoHandsOn, both ? 1f : 0f, dt / .12f);
            ChestChannels(out _, out float twist, out _);
            float yaw = Pawn.BodyTurn + twist;
            yawRate = Mathf.Lerp(yawRate, (yaw - lastYaw) / Mathf.Max(dt, 1e-4f), 1f - Mathf.Exp(-dt / .05f));
            lastYaw = yaw;
            if (offArmOn <= 0f) { offArm = OffArmLine(); offArmSpeed = Vector3.zero; return; }
            // A hand on the hilt keeps up with it (the loose arm's lag would pull it off).
            offArm = Vector3.SmoothDamp(offArm, OffArmLine(), ref offArmSpeed, Mathf.Lerp(.07f, .02f, twoHandsOn), Mathf.Infinity, dt);
        }

        /// <summary>Where the free arm goes for the sword arm's line now, in the chest's frame (x to the sword hand, y up, z
        /// ahead): it swings against the sword arm - out ahead while the sword is drawn back or out on its own side, back
        /// and out of the way as the sword comes across, up with a sword raised high - and as the body spins (the rook's
        /// sweep) it is flung out and trails behind. Always out on its own side (the stub has no elbow).</summary>
        Vector3 OffArmLine()
        {
            Vector3 s = Pawn.bodies[(int)BodyId.Chest].transform.InverseTransformDirection(ArmLine(ClickBladeDirection())).normalized;
            float trail = Mathf.Clamp(-yawRate / 600f, -1f, 1f), fling = Mathf.Clamp01((Mathf.Abs(yawRate) - 150f) / 600f);
            var o = new Vector3(
                -.7f + .25f * s.x - .3f * fling,
                -.4f + .35f * (s.y + .57f) + .5f * fling,
                .2f + .6f * s.x - .55f * (s.z - .77f) + .45f * trail);
            o.x = Mathf.Min(o.x, -.2f);
            if (twoHandsOn <= 0f) return o.normalized;
            // A two-handed cut: the free arm reaches from its shoulder to the hilt just below the sword hand.
            Transform chest = Pawn.bodies[(int)BodyId.Chest].transform;
            Vector3 hilt = ClickGrip() - ClickBladeDirection() * .08f - Pawn.bodies[(int)BodyId.ArmL].position;
            if (hilt.sqrMagnitude < 1e-6f) return o.normalized;
            return Vector3.Slerp(o.normalized, chest.InverseTransformDirection(hilt.normalized), Smooth(twoHandsOn));
        }

        /// <summary>How much of the free arm the model draws where its body goes for the click cuts (RagdollPawn.LeftArmShare),
        /// every machine: here, as far as it is posed; where this fighter is only seen, eased in while its blade (sent
        /// relative to the hips) moves as fast as a cut's and out a moment after. None through a skill.</summary>
        float StepOffArmShare()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            if (!Authority)
            {
                bool cutting = Quaternion.Angle(lastRemoteTurn, remoteRotation) / dt > 200f;
                // The swing's whoosh where this fighter's cuts are only seen: as its blade starts moving like one.
                if (cutting && remoteCut <= 0f && ClassicControls && Alive && Pawn.State == PawnState.Active) SwingSound();
                remoteCut = cutting ? .2f : Mathf.Max(0f, remoteCut - dt);
                lastRemoteTurn = remoteRotation;
                remoteOffArm = Mathf.MoveTowards(remoteOffArm, remoteCut > 0f ? 1f : 0f, dt / (remoteCut > 0f ? .2f : .3f));
            }
            if (!ClassicControls || !Alive || Pawn.State != PawnState.Active) return 0f;
            if (Skills != null && (Skills.HostStage != SfStage.None || Skills.EdgeStage != SfEdge.None)) return 0f;
            return Smooth(Authority ? offArmOn : remoteOffArm);
        }

        /// <summary>10-29 (승규 님: "칼이 휘두를 때 sfx 넣어줘"): the piece's own sword whoosh as a click cut's blade starts
        /// going through (Resources/SwordFightSkillSfx/Swing + piece), a little higher or lower each time so a combo's
        /// four cuts are not one sound four times over.</summary>
        void SwingSound() =>
            SwordFightSkillSfx.Moment("Swing" + Pawn.Piece, Hand.position, Skills, Random.Range(.92f, 1.08f));

        void DetectClickHit(Vector3 start, Vector3 end)
        {
            if (!Attacking) return;   // stopped this step (a parried cut)
            int count = Physics.OverlapCapsuleNonAlloc(start, end, .15f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = overlaps[i];
                if (!RagdollPawn.ColliderOwner.TryGetValue(c, out var target) || target == Pawn ||
                    target.Team == Pawn.Team || c.GetComponent<SwordBladeContact>() != null) continue;
                var victim = target.GetComponent<SwordFightPawn>();
                if (victim == null || !victim.Alive || victim.Protection > 0 || !hitThisSwing.Add(victim)) continue;
                // A king in its guard (the skill test scene) takes the cut and answers it.
                if (victim.Skills != null && victim.Skills.Parry(this, c.ClosestPoint(Hand.position))) { HitFlash = .1f; CancelClickSwing(); return; }
                Vector3 away = target.Hips.position - Pawn.Hips.position; away.y = 0;
                CutPush(away.normalized, out Vector3 push, out Vector3 topple);
                target.TakeHit(push, .65f, 0, true);
                // The upper body goes on a little faster the way the blade went than the hips, so it falls that way.
                if (!target.NetworkPuppet)
                    for (int b = (int)BodyId.Chest; b <= (int)BodyId.HandR; b++) target.bodies[b].linearVelocity += topple * target.PushScale;
                victim.StopCombat(); victim.FlashHit(.14f); HitFlash = .1f; HitsLanded++;
                RaiseStruck(this, victim);
            }
        }

        /// <summary>10-29 (승규 님: "칼을 휘두르는 방향에 따라 상대가 그쪽 방향으로 넘어지거나"): a click cut's hit, the way its
        /// blade was going where it struck instead of always ahead. A sweep throws the victim sideways the way it went, a
        /// chop from above drives it down where it stands (less far, barely lifted), a rising cut lifts it up and back, a
        /// thrust shoves it straight back. Then (승규 님: "날아가는 범위를 줄여주고") less far than the old 3.4 m/s: a heavy
        /// 2.3 m/s along the ground; <paramref name="topple"/> is the extra the upper body gets that way, so it falls toward
        /// it.</summary>
        void CutPush(Vector3 away, out Vector3 push, out Vector3 topple)
        {
            // The tip's way round now; a thrust barely turns its blade, so then the way it points.
            Vector3 move = SwingLine(SwingAge + .02f) - SwingLine(SwingAge - .01f);
            Vector3 blade = SwingLine(SwingAge);
            bool thrust = style.ArmDrawn != Vector3.zero && move.magnitude < .12f;
            Vector3 way = thrust ? blade : move.normalized;
            Vector3 flat = way; flat.y = 0f;
            // Too steep to say which side (a chop straight down): the way the cut faces, then away from the cutter.
            if (flat.sqrMagnitude < .04f) flat = swingDirection * .7f + away * .3f;
            flat.Normalize();
            // Never back into the cutter: a cut's far end can curl toward it.
            if (Vector3.Dot(flat, away) < -.2f) flat = (flat + away * 1.2f).normalized;
            float down = Mathf.Clamp01(-way.y), up = Mathf.Clamp01(way.y);
            float along = 2.3f * (1f - .35f * down);
            push = flat * along + Vector3.up * (.7f - .5f * down + 1f * up);
            topple = flat * 1f + Vector3.down * (.8f * down);
        }

        Vector3 ClickBladeDirection()
        {
            if (!Alive || Pawn.State != PawnState.Active) return Hand.rotation * Vector3.right;
            Vector3 dir = Attacking ? SwingLine(SwingAge) : IdleLine();
            // The posed blade has no collider: it is kept over the floor, not swept through it - and out of its own body.
            Vector3 grip = ClickGrip();
            return AboveFloor(grip, ClearOfBody(grip, dir));
        }

        Vector3 IdleLine() => (Quaternion.AngleAxis(-25f, Vector3.up) * Pawn.Facing + Vector3.up * .35f).normalized;

        /// <summary>The piece's own cut (SwingStyle) at <paramref name="age"/>: the blade brought from where it was (the guard,
        /// or where the last cut ended) to the cut's first line through the windup, along its lines through the cut, then
        /// back to the guard. 10-28: every stage starts and ends at rest (no jump in speed at a stage's start for the
        /// drives, which are fed the pose's speed, to kick at) and the lines are one curve, without a corner at each.</summary>
        Vector3 SwingLine(float age)
        {
            var keys = style.Keys;
            if (age < windup)
                return Via(fromLine, IdleLine(), SwingWorld(keys[0]), Settle, Smooth(Mathf.Max(0f, age) / windup),
                    SwingWorld(keys[0] + (keys[0] - keys[1]).normalized * .5f));
            if (age < windup + style.Active)
            {
                float at = CutProgress((age - windup) / style.Active) * (keys.Length - 1);
                int i = Mathf.Min((int)at, keys.Length - 2);
                return SwingWorld(Through(keys, i, at - i));
            }
            return Vector3.Slerp(SwingWorld(keys[keys.Length - 1]), IdleLine(), Smooth((age - windup - style.Active) / style.Recovery));
        }

        /// <summary>How far through its lines the cut is, <paramref name="k"/> through its time: eased in and out, or a
        /// snapping chop's - speeding up to its hit 60% through, then the draw slowing out of it (same speed at the hit).</summary>
        float CutProgress(float k) => !style.Snap ? Smooth(k) : k < .6f ? .6f * (k / .6f) * (k / .6f) : .6f + .4f * Ease((k - .6f) / .4f);

        /// <summary>The cut's lines as one smooth curve through them (Catmull-Rom), <paramref name="t"/> of the way from line
        /// <paramref name="i"/> to the next.</summary>
        static Vector3 Through(Vector3[] keys, int i, float t)
        {
            Vector3 p1 = keys[i], p2 = keys[i + 1];
            Vector3 p0 = i > 0 ? keys[i - 1] : 2f * p1 - p2, p3 = i + 2 < keys.Length ? keys[i + 2] : 2f * p2 - p1;
            return .5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (3f * p1 - p0 - 3f * p2 + p3) * t * t * t);
        }

        /// <summary>From <paramref name="from"/> to <paramref name="to"/>, <paramref name="s"/> of the way, bowed back toward
        /// <paramref name="rest"/> by <paramref name="settle"/>: a cut chained on from the last settles part of the way back
        /// on its way round to the next instead of going all the way to the guard and stopping there.
        /// 10-28 (승규 님: "모션을 더 부드럽게 … 특히 룩"): most combos start a cut about where the last one ended (the rook's
        /// sweep and its sweep back), and bowed back toward the guard those swung halfway back and returned - the tower
        /// unwinding and winding up again. Ending near where it starts, the way bows instead to <paramref name="load"/>:
        /// on a little past where the next cut starts and back into it, as a heavy swing carries on and is gathered.</summary>
        static Vector3 Via(Vector3 from, Vector3 rest, Vector3 to, float settle, float s, Vector3 load)
        {
            float close = 1f - Mathf.Clamp01(Vector3.Angle(from, to) / 60f);
            Vector3 mid = Vector3.Slerp(Vector3.Slerp(Vector3.Slerp(from, to, .5f), rest, settle * (1f - close)), load, settle * close);
            return Vector3.Slerp(Vector3.Slerp(from, mid, s), Vector3.Slerp(mid, to, s), s);
        }

        static float Via(float from, float rest, float to, float settle, float s, float load)
        {
            float close = 1f - Mathf.Clamp01(Mathf.Abs(from - to) / 40f);
            float mid = Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(from, to, .5f), rest, settle * (1f - close)), load, settle * close);
            return Mathf.Lerp(Mathf.Lerp(from, mid, s), Mathf.Lerp(mid, to, s), s);
        }

        /// <summary>Which way the blade's flat faces (its "up"; the swords' edges are their X). 10-25 (승규 님: "킹도 비슷하게
        /// 칼날로 베기끔"): held flat-up, an upright chop came down on the flat; through the windup and the cut the blade
        /// turns so an edge leads the cut, and back flat-up through the recovery.</summary>
        Vector3 bladeUp = Vector3.up;

        Vector3 BladeUp(Vector3 dir)
        {
            Vector3 want = Vector3.up;
            if (Attacking && SwingAge < windup + style.Active)
            {
                // The way the cut moves (through the windup, the way it will start), across the blade.
                float at = Mathf.Max(SwingAge, windup + .01f);
                Vector3 ahead = Vector3.ProjectOnPlane(SwingLine(at + .02f) - SwingLine(at - .01f), dir);
                if (ahead.sqrMagnitude > 1e-6f)
                {
                    // The kukri's edge is its inner curve, on its -X side; the straight swords cut with either edge.
                    Vector3 up = Vector3.Cross(dir, Pawn.Piece == PieceKind.Knight ? -ahead.normalized : ahead.normalized);
                    want = Pawn.Piece != PieceKind.Knight && Vector3.Dot(up, bladeUp) < 0f ? -up : up;
                }
            }
            // 10-28: rolled over through the windup (the wrist turning as the blade comes round), quickening into the cut to
            // keep its edge leading, and back flat-up gently after it (at once, it snapped over as each cut ended).
            float rate = !Attacking || SwingAge >= windup + style.Active ? 10f
                : Mathf.Lerp(14f, 60f, Smooth((SwingAge - windup + .06f) / .08f));
            bladeUp = Vector3.Slerp(bladeUp, want, 1f - Mathf.Exp(-rate * Mathf.Max(Time.deltaTime, 1e-4f)));
            return bladeUp;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        // 10-29 (승규 님: "다 0.68초에 맞춰", then "폰만 기존 0.56초로"): every piece's cuts now take 0.68 s in all (windup +
        // cut + recovery), each piece keeping its own split of it (the knight's quick windup, the rook's long cut) - but
        // the pawn's, which stay its own quick 0.56 s.
        /// <summary>A piece's swing (10-25, 승규 님: "기물별로 휘두르는 모션을 따로" - the table: king's overhead chop, queen's
        /// diagonal, rook's waist sweep, bishop's rising cut, knight's chop-and-hook, pawn's thrust). Where it hits is
        /// just where its blade goes (the cut sweeps the blade's line), so a wide sweep reaches wide and a thrust far -
        /// the pace is what evens them out.</summary>
        sealed class SwingStyle
        {
            public float Windup, Active, Recovery;
            /// <summary>The blade's lines through the cut, in the swing's frame: x to the sword hand's side, y up, z ahead.</summary>
            public Vector3[] Keys;
            /// <summary>The chest's turn (+ toward the sword hand) at the top of the windup and at the end of the cut, and
            /// its lean forward (+) into the cut and back (-) in the windup.</summary>
            public float TwistUp, TwistThrough, LeanIn, LeanBack;
            /// <summary>A thrust's arm (the swing's frame): drawn back here through the windup while the blade already
            /// points ahead, then driven out along the blade through the first part of the cut. Zero: the arm keeps to
            /// the blade's line, as for a cut.</summary>
            public Vector3 ArmDrawn;
            /// <summary>A draw cut's arm (the swing's frame): pulled back here after the hit (the last 40% of the cut) while
            /// the blade drags along its edge, back to the blade's line through the recovery. Zero: none.</summary>
            public Vector3 ArmPull;
            /// <summary>The whole body's turn (RagdollPawn.BodyTurn, + toward the sword hand) at the top of the windup and
            /// at the end of the cut: the rook's sweep carries its hand round its wide body with it.</summary>
            public float BodyUp, BodyThrough;
            /// <summary>The cut speeds up into its hit (a snapping chop, 60% through its lines) instead of easing in and out.</summary>
            public bool Snap;
            /// <summary>10-29: both hands on the hilt (the free arm reaches to it, OffArmLine) from the windup through the cut.</summary>
            public bool TwoHands;
        }

        /// <summary>Lines round an arc in the plane of ahead and <paramref name="side"/>: 0 ahead, 90 along side.</summary>
        static Vector3[] Arc(Vector3 side, float from, float to, int steps)
        {
            var keys = new Vector3[steps + 1];
            side.Normalize();
            for (int i = 0; i <= steps; i++)
            {
                float r = Mathf.Lerp(from, to, i / (float)steps) * Mathf.Deg2Rad;
                keys[i] = (new Vector3(0f, 0f, Mathf.Cos(r)) + side * Mathf.Sin(r)).normalized;
            }
            return keys;
        }

        static readonly SwingStyle KingSwing = new SwingStyle
        {
            // Heavy and nearly upright: raised high over the head, brought straight down (15 degrees off upright).
            // 10-29 (승규 님: "킹 베는 동작이 좀 느려"): every king cut .26 / .18 / .40 -> .20 / .15 / .32 (0.84 s -> 0.67 s).
            Windup = .20f, Active = .15f, Recovery = .33f,
            Keys = Arc(new Vector3(.27f, 1f, 0f), 115f, -55f, 6), TwistUp = 6f, TwistThrough = -6f, LeanIn = 18f, LeanBack = -6f,
        };
        static readonly SwingStyle QueenSwing = new SwingStyle
        {
            // Quick and wide: raised high out over the sword shoulder, cut down across the body to the far hip (45 degrees)
            // with the body turning through it. 10-25 (승규 님: "퀸 너무 까딱까딱 거려 좀 멋있게"): it was drawn back behind
            // the shoulder, into the head, and the body check threw it from side to side; now it is raised high and upright
            // beside the head (its pommel hangs down the body's side, clear of it) and the body turns through the cut.
            Windup = .17f, Active = .18f, Recovery = .33f,
            Keys = Arc(new Vector3(.6f, 1f, 0f), 100f, -65f, 8), TwistUp = 15f, TwistThrough = -20f, LeanIn = 12f, LeanBack = -4f,
            BodyUp = 0f, BodyThrough = -45f,
        };
        static readonly SwingStyle RookSwing = new SwingStyle
        {
            // Waist high, a half circle from the sword side round the front to the far side: slow, but it sweeps wide. The
            // hand sits against the wide body, so the body turns with the blade and carries the hand round.
            // 10-29 (승규 님: "룩도 좀 느려"): every rook cut .22 / .26 / .40 -> .17 / .21 / .32 (0.88 s -> 0.70 s).
            Windup = .17f, Active = .20f, Recovery = .31f,
            Keys = Arc(new Vector3(1f, -.08f, 0f), 95f, -95f, 8), TwistUp = 15f, TwistThrough = -15f, LeanIn = 6f, LeanBack = 0f,
            BodyUp = 80f, BodyThrough = -85f,
        };
        static readonly SwingStyle BishopSwing = new SwingStyle
        {
            // The queen's line the other way: drawn down to the far hip with the body turned away, then cut up across the
            // body to high and upright beside the head as the body turns back through it (10-25, 승규 님: "비숍도
            // 마찬가지로" - the queen's sweep and body turn).
            Windup = .17f, Active = .18f, Recovery = .33f,
            Keys = Arc(new Vector3(.6f, 1f, 0f), -65f, 100f, 8), TwistUp = -18f, TwistThrough = 15f, LeanIn = 4f, LeanBack = 8f,
            BodyUp = -40f, BodyThrough = 30f,
        };
        static readonly SwingStyle KnightSwing = new SwingStyle
        {
            // A kukri's chop (10-25, 승규 님: "나이트는 휘두르는 게 쿠크리 처럼"): the blade cocked back over the sword
            // shoulder with the elbow drawn back, a short chop that speeds up into the hit (the wrist snapping the heavy belly
            // through) down across the front, then a draw - the blade pulled back toward the far hip along its edge.
            Windup = .13f, Active = .22f, Recovery = .33f,
            Keys = new[]
            {
                new Vector3(.35f, .55f, -.75f), new Vector3(.35f, .9f, .1f), new Vector3(.2f, .5f, .85f), new Vector3(0f, -.15f, 1f),
                new Vector3(-.25f, -.55f, .8f), new Vector3(-.4f, -.65f, .4f),
            },
            TwistUp = 15f, TwistThrough = -25f, LeanIn = 20f, LeanBack = -6f,
            BodyUp = 30f, BodyThrough = -30f,
            ArmDrawn = new Vector3(.55f, .35f, -.6f), ArmPull = new Vector3(.3f, -.75f, -.35f), Snap = true,
        };
        static readonly SwingStyle PawnSwing = new SwingStyle
        {
            // A straight thrust ahead (the pawn only goes forward), lifting a little at its end.
            Windup = .12f, Active = .14f, Recovery = .30f,
            Keys = new[] { new Vector3(.05f, .12f, 1f), new Vector3(0f, .04f, 1f), new Vector3(0f, .1f, 1f), new Vector3(0f, .42f, .9f) },
            TwistUp = 20f, TwistThrough = -12f, LeanIn = 22f, LeanBack = -10f,
            ArmDrawn = new Vector3(.35f, -.7f, -.45f),
        };

        // 10-28: each piece's second and third cuts (ComboWindow). Every cut of a combo keeps its piece's pace, so the
        // balance chosen on 10-25 (reach by where the blade goes, evened out by pace) is unchanged; only the direction
        // changes from click to click, as in the clip (a rising cut, one down from above, one across).
        static readonly SwingStyle KingRise = new SwingStyle
        {
            // Up from the low far side to high on the sword side, the heavy blade dragged up through the front.
            Windup = .20f, Active = .15f, Recovery = .33f,
            Keys = Arc(new Vector3(.45f, 1f, 0f), -60f, 105f, 8), TwistUp = -15f, TwistThrough = 12f, LeanIn = 6f, LeanBack = 6f,
            BodyUp = -35f, BodyThrough = 25f,
        };
        static readonly SwingStyle KingSweep = new SwingStyle
        {
            // The finisher: a waist-high sweep from the sword side round the front, the whole body turned through it.
            Windup = .20f, Active = .15f, Recovery = .33f,
            Keys = Arc(new Vector3(1f, -.12f, 0f), 95f, -95f, 8), TwistUp = 15f, TwistThrough = -15f, LeanIn = 8f, LeanBack = 0f,
            BodyUp = 75f, BodyThrough = -80f,
        };
        static readonly SwingStyle QueenRise = new SwingStyle
        {
            // Back up the way the first came down, flatter: from low on the far side to high out on the sword side.
            Windup = .17f, Active = .18f, Recovery = .33f,
            Keys = Arc(new Vector3(1f, .7f, 0f), -70f, 95f, 8), TwistUp = -18f, TwistThrough = 15f, LeanIn = 4f, LeanBack = 8f,
            BodyUp = -45f, BodyThrough = 35f,
        };
        static readonly SwingStyle QueenSweep = new SwingStyle
        {
            // Chest high and flat, from the sword side round to the far side.
            Windup = .17f, Active = .18f, Recovery = .33f,
            Keys = Arc(new Vector3(1f, .12f, 0f), 90f, -85f, 8), TwistUp = 18f, TwistThrough = -20f, LeanIn = 8f, LeanBack = -2f,
            BodyUp = 55f, BodyThrough = -65f,
        };
        static readonly SwingStyle RookBack = new SwingStyle
        {
            // The sweep back: from the far side round the front to the sword side, the body turned the other way.
            Windup = .17f, Active = .20f, Recovery = .31f,
            Keys = Arc(new Vector3(1f, -.08f, 0f), -95f, 95f, 8), TwistUp = -15f, TwistThrough = 15f, LeanIn = 6f, LeanBack = 0f,
            BodyUp = -85f, BodyThrough = 80f,
        };
        static readonly SwingStyle RookCleave = new SwingStyle
        {
            // 10-29 (승규 님: "지금 동작 3번 4번 아래 위로 휘두르는데 다르게 휘두르게 해"; it was a straight slam down): a heavy
            // slanting cleave, lifted to shoulder height on the sword side where the sweep back ended and driven down across
            // the front to low on the far side, flatter than the king's or queen's diagonals, the wide body turning through.
            Windup = .17f, Active = .20f, Recovery = .31f,
            Keys = Arc(new Vector3(1f, .55f, 0f), 105f, -80f, 8), TwistUp = 15f, TwistThrough = -18f, LeanIn = 14f, LeanBack = -4f,
            BodyUp = 70f, BodyThrough = -70f,
        };
        static readonly SwingStyle BishopFall = new SwingStyle
        {
            // The other half of its diagonal: from high on the sword side down across the body to the far hip.
            Windup = .17f, Active = .18f, Recovery = .33f,
            Keys = Arc(new Vector3(.6f, 1f, 0f), 100f, -65f, 8), TwistUp = 15f, TwistThrough = -20f, LeanIn = 12f, LeanBack = -4f,
            BodyUp = 0f, BodyThrough = -45f,
        };
        static readonly SwingStyle BishopCross = new SwingStyle
        {
            // The other diagonal, backhand: from high on the far side down across to low on the sword side (the two
            // diagonals make the bishop's X).
            Windup = .17f, Active = .18f, Recovery = .33f,
            Keys = Arc(new Vector3(-.6f, 1f, 0f), 95f, -65f, 8), TwistUp = -20f, TwistThrough = 15f, LeanIn = 10f, LeanBack = -4f,
            BodyUp = -55f, BodyThrough = 35f,
        };
        static readonly SwingStyle KnightRise = new SwingStyle
        {
            // Back up from the far hip where the draw ended, snapping up across the front to high on the sword side.
            Windup = .13f, Active = .22f, Recovery = .33f,
            Keys = new[]
            {
                new Vector3(-.45f, -.6f, .5f), new Vector3(-.35f, -.25f, .95f), new Vector3(0f, .15f, 1f),
                new Vector3(.35f, .5f, .8f), new Vector3(.55f, .8f, .2f),
            },
            TwistUp = -20f, TwistThrough = 20f, LeanIn = 6f, LeanBack = 4f,
            BodyUp = -35f, BodyThrough = 30f, Snap = true,
        };
        static readonly SwingStyle KnightHook = new SwingStyle
        {
            // The knight's L: a flat hook in from the sword side across the front, then dropped down and drawn back.
            Windup = .13f, Active = .22f, Recovery = .33f,
            Keys = new[]
            {
                new Vector3(.9f, .3f, .25f), new Vector3(.55f, .3f, .8f), new Vector3(0f, .25f, 1f),
                new Vector3(-.3f, .1f, .95f), new Vector3(-.4f, -.45f, .75f),
            },
            TwistUp = 20f, TwistThrough = -25f, LeanIn = 14f, LeanBack = -4f,
            BodyUp = 45f, BodyThrough = -40f,
            ArmPull = new Vector3(.3f, -.75f, -.35f), Snap = true,
        };
        static readonly SwingStyle PawnLeft = new SwingStyle
        {
            // A pawn takes on its forward diagonals: a short cut down across to the front on the far side…
            Windup = .12f, Active = .14f, Recovery = .30f,
            Keys = new[] { new Vector3(.4f, .55f, .75f), new Vector3(.15f, .25f, .97f), new Vector3(-.2f, -.05f, 1f), new Vector3(-.42f, -.3f, .85f) },
            TwistUp = 18f, TwistThrough = -18f, LeanIn = 14f, LeanBack = -6f,
            BodyUp = 0f, BodyThrough = -25f,
        };
        static readonly SwingStyle PawnRight = new SwingStyle
        {
            // …and one up across to the front on the sword side.
            Windup = .12f, Active = .14f, Recovery = .30f,
            Keys = new[] { new Vector3(-.4f, -.3f, .85f), new Vector3(-.1f, 0f, 1f), new Vector3(.25f, .3f, .95f), new Vector3(.48f, .55f, .7f) },
            TwistUp = -15f, TwistThrough = 15f, LeanIn = 10f, LeanBack = 4f,
            BodyUp = -25f, BodyThrough = 15f,
        };

        // 10-29 (승규 님: "각 기물별 1개 공격 방향을 더 해주고 자연스럽게 이어져야 돼"): a fourth cut per piece, in a
        // direction its combo did not have yet. Each starts near where the third ended and ends near where the first
        // starts (the combo wraps round), and its body turn runs from the third's end toward the first's windup, so the
        // chain keeps turning one way instead of unwinding between cuts.
        static readonly SwingStyle KingCleave = new SwingStyle
        {
            // Two-handed (10-29, 승규 님: "킹은 양손으로 대각선 내려치기로"): the free hand joins the hilt as the sword is
            // lifted high on the far side (from where the sweep ended), and both bring it down across to low on the sword
            // side, the body turning back and leaning into it; the overhead chop then loads up from there on the same side.
            Windup = .20f, Active = .15f, Recovery = .33f,
            Keys = Arc(new Vector3(-.45f, 1f, 0f), 100f, -60f, 8), TwistUp = -18f, TwistThrough = 12f, LeanIn = 20f, LeanBack = -6f,
            BodyUp = -60f, BodyThrough = 25f, TwoHands = true,
        };
        static readonly SwingStyle QueenReturn = new SwingStyle
        {
            // The sweep back: chest high from the far side where the sweep ended round to the sword side, rising a little,
            // which leaves the blade out on the sword side for the first cut's diagonal.
            Windup = .17f, Active = .18f, Recovery = .33f,
            Keys = Arc(new Vector3(1f, .3f, 0f), -85f, 95f, 8), TwistUp = -18f, TwistThrough = 18f, LeanIn = 6f, LeanBack = -2f,
            BodyUp = -60f, BodyThrough = 50f,
        };
        static readonly SwingStyle RookRam = new SwingStyle
        {
            // 10-29 (the same; it was a rising cut straight up): the battering ram. Both hands take the hilt low before the
            // belly with the point already ahead (from low on the far side where the cleave ended), and the whole body
            // lunges in behind it, driving it straight out waist high; the body turns back toward the sword side as it
            // goes, ready for the first sweep.
            Windup = .17f, Active = .20f, Recovery = .31f,
            Keys = new[] { new Vector3(-.08f, -.12f, 1f), new Vector3(-.03f, -.1f, 1f), new Vector3(0f, -.06f, 1f), new Vector3(.02f, 0f, 1f) },
            TwistUp = -12f, TwistThrough = 6f, LeanIn = 26f, LeanBack = -6f,
            BodyUp = -45f, BodyThrough = 20f,
            ArmDrawn = new Vector3(.05f, -.5f, .45f), TwoHands = true,
        };
        static readonly SwingStyle BishopThrust = new SwingStyle
        {
            // A two-handed thrust (10-29, 승규 님: "비숍 4번 째에서 양손으로 찌르기"): from low on the sword side where the X
            // ended, the hilt is drawn in low before the belly in both hands with the point already ahead, then both
            // drive it straight out chest high, the body squaring up behind it; the first cut then drops to the far hip.
            Windup = .17f, Active = .18f, Recovery = .33f,
            Keys = new[] { new Vector3(.05f, 0f, 1f), new Vector3(0f, -.02f, 1f), new Vector3(0f, .05f, 1f), new Vector3(0f, .15f, .99f) },
            TwistUp = 15f, TwistThrough = -8f, LeanIn = 22f, LeanBack = -8f,
            BodyUp = 20f, BodyThrough = -10f,
            ArmDrawn = new Vector3(0f, -.6f, .55f), TwoHands = true,
        };
        static readonly SwingStyle KnightReturn = new SwingStyle
        {
            // The L the other way round: up from the far hip where the hook's draw ended, then a flat backhand hook across
            // the front to the sword side, snapping up behind the sword shoulder where the chop is cocked.
            Windup = .13f, Active = .22f, Recovery = .33f,
            Keys = new[]
            {
                new Vector3(-.45f, -.4f, .75f), new Vector3(-.7f, .25f, .65f), new Vector3(-.2f, .35f, 1f),
                new Vector3(.4f, .4f, .85f), new Vector3(.75f, .5f, .3f),
            },
            TwistUp = -22f, TwistThrough = 20f, LeanIn = 8f, LeanBack = 2f,
            BodyUp = -40f, BodyThrough = 35f, Snap = true,
        };
        static readonly SwingStyle PawnChop = new SwingStyle
        {
            // Straight down ahead from high (where the rising cut ended), a short chop to low in front; the thrust then
            // draws back from there.
            Windup = .12f, Active = .14f, Recovery = .30f,
            Keys = new[] { new Vector3(.25f, .85f, .45f), new Vector3(.1f, .6f, .8f), new Vector3(0f, .15f, 1f), new Vector3(0f, -.25f, .95f) },
            TwistUp = 10f, TwistThrough = -8f, LeanIn = 20f, LeanBack = -8f,
            BodyUp = 15f, BodyThrough = 0f,
        };

        static readonly SwingStyle[] KingCombo = { KingSwing, KingRise, KingSweep, KingCleave };
        static readonly SwingStyle[] QueenCombo = { QueenSwing, QueenRise, QueenSweep, QueenReturn };
        static readonly SwingStyle[] RookCombo = { RookSwing, RookBack, RookCleave, RookRam };
        static readonly SwingStyle[] BishopCombo = { BishopSwing, BishopFall, BishopCross, BishopThrust };
        static readonly SwingStyle[] KnightCombo = { KnightSwing, KnightRise, KnightHook, KnightReturn };
        static readonly SwingStyle[] PawnCombo = { PawnSwing, PawnLeft, PawnRight, PawnChop };

        SwingStyle style = QueenSwing;

        static SwingStyle[] ComboOf(PieceKind piece) => piece switch
        {
            PieceKind.King => KingCombo,
            PieceKind.Rook => RookCombo,
            PieceKind.Bishop => BishopCombo,
            PieceKind.Knight => KnightCombo,
            PieceKind.Pawn => PawnCombo,
            _ => QueenCombo,
        };

        /// <summary>A value wound from <paramref name="from"/> (where it was when the cut started) to <paramref name="up"/>
        /// through the windup, carried to <paramref name="through"/> with the blade and eased back to zero through the
        /// recovery.</summary>
        float SwingPhase(float from, float up, float through)
        {
            // Gathered a little past its windup's, away from where the cut takes it (Via).
            if (SwingAge < windup) return Via(from, 0f, up, Settle, Smooth(SwingAge / windup), up + (up - through) * .15f);
            if (SwingAge < windup + style.Active) return Mathf.Lerp(up, through, CutProgress((SwingAge - windup) / style.Active));
            return through * (1f - Smooth((SwingAge - windup - style.Active) / style.Recovery));
        }

        /// <summary>The chest's lean forward (+) and back, its turn (+ toward the sword hand) and its roll through the cut,
        /// on top of its turn to the aim: it winds up its own way and turns through with the blade, leaning into it.</summary>
        void ChestChannels(out float lean, out float twist, out float roll)
        {
            lean = twist = roll = 0f;
            if (!Attacking) return;
            float sweep = SwingAge >= windup && SwingAge < windup + style.Active
                ? Mathf.Sin(CutProgress((SwingAge - windup) / style.Active) * Mathf.PI) : 0f;
            lean = SwingPhase(fromLean, style.LeanBack, 0f) + style.LeanIn * sweep;
            twist = SwingPhase(fromTwist, style.TwistUp, style.TwistThrough);
            roll = SwingPhase(fromRoll, 0f, 0f) - 6f * sweep;
        }

        Vector3 SwingWorld(Vector3 local)
        {
            Vector3 fwd = swingDirection, right = Vector3.Cross(Vector3.up, fwd);
            return (right * local.x + Vector3.up * local.y + fwd * local.z).normalized;
        }

        // The body's drawn shape (the piece's skinned body), for keeping the posed blade out of it; how far the hilt reaches
        // back from the grip (the boxes' 0.1 m, a piece model's longer: SwordFightPawn.UpdateSwordModel).
        readonly List<Renderer> bodyLook = new List<Renderer>();
        int bodyLookPiece = -1;
        float hiltBack = .1f;
        Transform visualHand;
        // The middle of the drawn mitten, in its bone's space. 10-25 (승규 님: "폰처럼 손잡이 부분에 겹치게 … 다른 기물들은
        // 붕 떠져 있어"): the bone starts at the wrist and the pieces' mittens sit out past it on the stub's end, so a grip
        // held at the bone passed beside the mitten; it is held through the mitten's middle, as the pawn's is.
        Vector3 handMiddle;

        /// <summary>The middle of what <paramref name="skin"/> draws on its bone <paramref name="bone"/> (the vertices
        /// mostly on it), in that bone's space.</summary>
        static Vector3 HandMiddle(SkinnedMeshRenderer skin, int bone) => MittenBounds(skin, bone).center;

        /// <summary>The mitten in its bone's space: the bounds of the vertices mostly on <paramref name="bone"/> (through its
        /// bindpose); empty at the bone's origin (the wrist) if there are none. 10-25: also where the bishop's rescue rope is
        /// held (SwordFightSkillFx.RopeHand.cs).</summary>
        internal static Bounds MittenBounds(SkinnedMeshRenderer skin, int bone)
        {
            var mesh = skin.sharedMesh;
            if (mesh == null || bone < 0 || bone >= mesh.bindposes.Length) return default;
            var weights = mesh.boneWeights;
            var vertices = mesh.vertices;
            Matrix4x4 toBone = mesh.bindposes[bone];
            Bounds b = default;
            bool any = false;
            for (int i = 0; i < vertices.Length && i < weights.Length; i++)
            {
                if (weights[i].boneIndex0 != bone || weights[i].weight0 < .5f) continue;
                Vector3 p = toBone.MultiplyPoint3x4(vertices[i]);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
            return any ? b : default;
        }

        /// <summary>The blade from <paramref name="grip"/> turned out round the body until it no longer passes through it
        /// (10-25, 승규 님: "칼을 들 때 몸에 관통이 되는 현상 수정해줘" — a wide piece such as the rook had the ready blade, turned
        /// 25° across the body, through its middle). The body is its drawn parts' upright box, as a round column.</summary>
        Vector3 ClearOfBody(Vector3 grip, Vector3 dir)
        {
            if (!BodyColumn(out Vector3 axis, out float radius, out float lo, out float hi)) return dir;
            radius += .03f;
            // The hand may itself be inside the drawn body: then only a blade (or a pommel) going in deeper than it counts.
            float gripOff = new Vector2(grip.x - axis.x, grip.z - axis.z).magnitude;
            // Held in the drawn hand: the body is no wider there than the hand is out from it (its box is the widest part).
            if (visualHand != null) radius = Mathf.Min(radius, gripOff - .01f);
            int Inside(Vector3 d)
            {
                if (bodyProfile != null) return InsideProfile(grip, d);
                int n = 0;
                // The blade out to its tip, and the hilt back to the pommel (the piece models' grips are long).
                for (int i = -6; i <= 8; i++)
                {
                    if (i == 0) continue;
                    Vector3 p = grip + d * (i > 0 ? .12f + .76f * i / 8f : hiltBack * i / 6f);
                    if (p.y < lo || p.y > hi) continue;
                    float off = new Vector2(p.x - axis.x, p.z - axis.z).magnitude;
                    // The pommel may just touch the body's side (a few cm, hidden by the hand): turning the whole blade
                    // away for it threw a raised blade about (10-25, the queen's "까딱까딱").
                    if (off < (i > 0 ? radius : radius - .05f) && off < gripOff + .02f) n++;
                }
                return n;
            }
            // 10-25 (승규 님: "칼을 휘두르면 이렇게 떨려 자연스럽게"): the turn that clears it was picked fresh each call in 15°
            // steps, so through a swing it jumped from side to side (and the arm, aimed along the blade, moved the drawn
            // hand and so the pick again). Now it is found once a frame to 2°, keeps to the side it is already turned to
            // unless the other is much nearer, and the blade eases to it over a few frames.
            if (clearFrame != Time.frameCount)
            {
                clearFrame = Time.frameCount;
                float target = 0f;
                if (Inside(dir) > 0)
                {
                    (float turn, bool clear) Need(float sign)
                    {
                        int best = int.MaxValue;
                        float at = 0f;
                        for (int a = 2; a <= 180; a += 2)
                        {
                            int n = Inside(Quaternion.AngleAxis(sign * a, Vector3.up) * dir);
                            if (n == 0) return (sign * a, true);
                            if (n < best) { best = n; at = sign * a; }
                        }
                        return (at, false);
                    }
                    var right = Need(1f);
                    var left = Need(-1f);
                    float rc = Mathf.Abs(right.turn) + (right.clear ? 0f : 360f) + (clearTurn < -1f ? 30f : 0f);
                    float lc = Mathf.Abs(left.turn) + (left.clear ? 0f : 360f) + (clearTurn > 1f ? 30f : 0f);
                    target = rc <= lc ? right.turn : left.turn;
                }
                clearTurn = Mathf.SmoothDampAngle(clearTurn, target, ref clearSpeed, .06f, Mathf.Infinity, Mathf.Max(Time.deltaTime, 1e-4f));
            }
            return Mathf.Abs(clearTurn) < .01f ? dir : Quaternion.AngleAxis(clearTurn, Vector3.up) * dir;
        }

        float clearTurn, clearSpeed, armOut, armOutGoal, armOutSpeed;
        int clearFrame = -1, armOutFrame = -1;

        // 10-25 (승규 님: "나이트가 몸에 관통 돼"): the body as one round column missed what a piece has out in front of it
        // (the knight's neck and muzzle) and the blade as a straight line missed the kukri's bend. A piece model's body is
        // now its own shape - how far out it reaches, per height and per direction round its hips bone, from its mesh -
        // and the kukri's blade is followed along its bend.
        float[,] bodyProfile;
        Transform profileBone;
        float profileLo;
        const float ProfileStep = .03f;
        const int ProfileSectors = 32;
        // The knight's curved blade's centre line off its straight line, toward its edge (model -X), every 5 cm up the model;
        // the other swords are straight to under 1 cm. 10-27: measured again for the new cavalry sabre (its tip 28 cm over).
        static readonly float[] KukriBend = { 0f, 0f, 0f, 0f, 0f, .001f, .001f, 0f, -.003f, -.008f,
            -.017f, -.029f, -.048f, -.074f, -.14f, -.17f, -.213f, -.278f };
        float modelLift, modelScale = 1f;

        int InsideProfile(Vector3 grip, Vector3 d)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(bladeUp.normalized, d)) > .98f ? (sword != null ? sword.up : Vector3.up) : bladeUp;
            Vector3 side = Quaternion.LookRotation(d, up) * Vector3.right;
            bool kukri = Pawn.Piece == PieceKind.Knight && swordModel != null;
            int n = 0;
            for (int i = -6; i <= 8; i++)
            {
                if (i == 0) continue;
                float along = i > 0 ? .12f + .76f * i / 8f : hiltBack * i / 6f;
                Vector3 p = grip + d * along;
                if (kukri && i > 0)
                {
                    float y = Mathf.Clamp((along - modelLift) / modelScale / .05f, 0f, KukriBend.Length - 1.001f);
                    int k = (int)y;
                    p += side * Mathf.Lerp(KukriBend[k], KukriBend[k + 1], y - k) * modelScale;
                }
                // The blade's own half width; the pommel may just touch the body's side (hidden by the hand).
                if (InBody(p, i > 0 ? .03f : -.03f)) n++;
            }
            return n;
        }

        bool InBody(Vector3 p, float margin)
        {
            Vector3 q = profileBone.InverseTransformPoint(p);
            int h = Mathf.FloorToInt((q.y - profileLo) / ProfileStep);
            if (h < 0 || h >= bodyProfile.GetLength(0)) return false;
            float r = new Vector2(q.x, q.z).magnitude;
            int a = (int)((Mathf.Atan2(q.z, q.x) + Mathf.PI) / (2f * Mathf.PI) * ProfileSectors) % ProfileSectors;
            float reach = 0f;
            for (int dh = -1; dh <= 1; dh++)
                for (int da = -1; da <= 1; da++)
                {
                    int hh = h + dh;
                    if (hh < 0 || hh >= bodyProfile.GetLength(0)) continue;
                    reach = Mathf.Max(reach, bodyProfile[hh, (a + da + ProfileSectors) % ProfileSectors]);
                }
            return reach > 0f && r < reach + margin;
        }

        /// <summary>How far the piece's body (the vertices mostly on its hips, spine and head bones) reaches out from its hips
        /// bone, per height step and direction sector, in that bone's space.</summary>
        static float[,] BodyProfile(SkinnedMeshRenderer skin, int hips, out float lo)
        {
            lo = 0f;
            var mesh = skin.sharedMesh;
            if (mesh == null || !mesh.isReadable || hips >= mesh.bindposes.Length) return null;
            var body = new bool[skin.bones.Length];
            for (int b = 0; b < skin.bones.Length; b++)
            {
                string name = skin.bones[b] != null ? skin.bones[b].name : "";
                body[b] = name.EndsWith("Hips") || name.EndsWith("Spine") || name.EndsWith("Spine1") || name.EndsWith("Spine2")
                    || name.EndsWith("Neck") || name.EndsWith("Head");
            }
            var weights = mesh.boneWeights;
            var vertices = mesh.vertices;
            Matrix4x4 toHips = mesh.bindposes[hips];
            var points = new List<Vector3>();
            float top = float.MinValue;
            lo = float.MaxValue;
            for (int i = 0; i < vertices.Length && i < weights.Length; i++)
            {
                int b = weights[i].boneIndex0;
                if (b < 0 || b >= body.Length || !body[b] || weights[i].weight0 < .5f) continue;
                Vector3 q = toHips.MultiplyPoint3x4(vertices[i]);
                points.Add(q);
                lo = Mathf.Min(lo, q.y); top = Mathf.Max(top, q.y);
            }
            if (points.Count == 0) return null;
            var profile = new float[Mathf.FloorToInt((top - lo) / ProfileStep) + 1, ProfileSectors];
            foreach (var q in points)
            {
                int h = Mathf.FloorToInt((q.y - lo) / ProfileStep);
                int a = (int)((Mathf.Atan2(q.z, q.x) + Mathf.PI) / (2f * Mathf.PI) * ProfileSectors) % ProfileSectors;
                profile[h, a] = Mathf.Max(profile[h, a], new Vector2(q.x, q.z).magnitude);
            }
            return profile;
        }

        /// <summary>Where the sword is held: the hand, or - when the hand is buried in a wide piece's drawn body (the rook's
        /// short arms end inside its tower) - the point of the body's surface beside it, so neither the blade nor the long
        /// hilt has to pass through the body.</summary>
        Vector3 ClickGrip()
        {
            // A piece model's own drawn hand (its rig's right hand bone, outside the body where the mitt is), else the
            // physical hand - pushed to the body's surface if the body is drawn round it.
            BodyColumn(out _, out _, out _, out _);
            if (visualHand != null) return visualHand.TransformPoint(handMiddle);
            Vector3 hand = Hand.position;
            if (!BodyColumn(out Vector3 axis, out float radius, out float lo, out float hi) || hand.y < lo || hand.y > hi) return hand;
            Vector3 off = hand - axis; off.y = 0;
            float d = off.magnitude;
            if (d >= radius) return hand;
            Vector3 outward = d > 1e-3f ? off / d : Vector3.Cross(Vector3.up, Pawn.Facing).normalized;
            return new Vector3(axis.x, hand.y, axis.z) + outward * (radius + .02f);
        }

        bool BodyColumn(out Vector3 axis, out float radius, out float lo, out float hi)
        {
            axis = Pawn.Hips.position; radius = 0; lo = hi = 0;
            if ((int)Pawn.Piece != bodyLookPiece || bodyLook.Exists(r => r == null))
            {
                bodyLookPiece = (int)Pawn.Piece;
                bodyLook.Clear();
                visualHand = null;
                bodyProfile = null; profileBone = null;
                // The piece's skinned body (Visual), else the parts drawn on the hips, chest and head.
                var visual = transform.Find("Visual");
                if (visual != null)
                    foreach (var r in visual.GetComponentsInChildren<Renderer>())
                    {
                        bodyLook.Add(r);
                        if (r is SkinnedMeshRenderer skin && visualHand == null)
                            for (int k = 0; k < skin.bones.Length; k++)
                                if (skin.bones[k] != null && skin.bones[k].name.EndsWith("RightHand"))
                                {
                                    visualHand = skin.bones[k];
                                    handMiddle = HandMiddle(skin, k);
                                    int hips = System.Array.FindIndex(skin.bones, x => x != null && x.name.EndsWith("Hips"));
                                    if (hips >= 0)
                                    {
                                        bodyProfile = BodyProfile(skin, hips, out profileLo);
                                        profileBone = bodyProfile != null ? skin.bones[hips] : null;
                                    }
                                    break;
                                }
                    }
                if (bodyLook.Count == 0)
                {
                    var parts = new[] { Pawn.bodies[(int)BodyId.Hips].transform, Pawn.bodies[(int)BodyId.Chest].transform, Pawn.bodies[(int)BodyId.Head].transform };
                    foreach (var r in Pawn.GetComponentsInChildren<Renderer>())
                    {
                        if (sword != null && r.transform.IsChildOf(sword)) continue;
                        foreach (var t in parts)
                            if (r.transform == t || r.transform.IsChildOf(t)) { bodyLook.Add(r); break; }
                    }
                }
            }
            bool any = false;
            Bounds b = default;
            foreach (var r in bodyLook)
            {
                if (!r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return false;
            axis = b.center;
            radius = .5f * Mathf.Min(b.size.x, b.size.z);
            lo = b.min.y; hi = b.max.y;
            return true;
        }
        Quaternion? ClickSwordPose(int part)
        {
            if (!Alive || Pawn.State != PawnState.Active) return null;
            if (part == (int)BodyId.Chest && Attacking)
            {
                float turn = Mathf.Clamp(Vector3.SignedAngle(Pawn.Facing, swingDirection, Vector3.up), -65, 65);
                ChestChannels(out float lean, out float twist, out float roll);
                return Quaternion.Euler(lean, turn * .65f + twist, roll);
            }
            // The free arm swings with the cuts (OffArmLine), from and back to the walk's own.
            if (part == (int)BodyId.ArmL)
                return offArmOn <= 0f ? (Quaternion?)null
                    : Quaternion.Slerp(Pawn.puppet[part].localRotation, Quaternion.FromToRotation(Vector3.left, offArm), Smooth(offArmOn));
            if (part != (int)BodyId.ArmR) return null;
            return Quaternion.FromToRotation(Vector3.right, Pawn.bodies[(int)BodyId.Chest].transform.InverseTransformDirection(ClickArmDirection()));
        }

        /// <summary>Where the sword arm points: along the blade, except through a thrust's windup and drive (ArmDrawn).</summary>
        Vector3 ClickArmDirection()
        {
            Vector3 blade = ClickBladeDirection();
            if (!Alive || Pawn.State != PawnState.Active) return blade;
            return KeepArmOut(ArmLine(blade));
        }

        /// <summary>The sword arm's line before it is kept out of the body (KeepArmOut).</summary>
        Vector3 ArmLine(Vector3 blade)
        {
            // 10-25 (승규 님: "이 모션이랑 팔 각도를 자연스럽게"): since a piece's sword arm is drawn all the way with its body
            // (RagdollPawn.RightArmFree), at rest it hangs forward and a little out, the sword held up from the wrist, not
            // the stub aimed along the blade; a cut takes it from there and gives it back.
            Vector3 guard = Pawn.Facing * .75f + Vector3.Cross(Vector3.up, Pawn.Facing) * .3f - Vector3.up * .55f;
            if (!Attacking) return guard;
            float end = windup + style.Active;
            if (SwingAge >= end)
            {
                float r = (SwingAge - end) / style.Recovery;
                // A draw cut's arm stays pulled into the first third of the recovery (the arm takes that long to get
                // there), then comes back.
                Vector3 after = style.ArmPull == Vector3.zero ? blade
                    : Vector3.Slerp(SwingWorld(style.ArmPull), blade, Smooth((r - .35f) / .65f));
                return Vector3.Slerp(after, guard, Smooth(r));
            }
            Vector3 drawn = style.ArmDrawn == Vector3.zero ? blade : SwingWorld(style.ArmDrawn);
            // From where it was (the guard, or where the last cut left it).
            if (SwingAge < windup) return Via(fromArm, guard, drawn, Settle, Smooth(SwingAge / windup), drawn);
            if (style.ArmDrawn == Vector3.zero) return blade;
            // Out to full reach in the first 60% of the cut, the blade's line from then on - or, for a draw cut, pulled
            // back toward the body after it.
            float k = (SwingAge - windup) / style.Active;
            Vector3 reach = Vector3.Slerp(drawn, blade, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / .6f)));
            if (style.ArmPull == Vector3.zero || k <= .6f) return reach;
            return Vector3.Slerp(reach, SwingWorld(style.ArmPull), Ease((k - .6f) / .4f));
        }

        /// <summary>The sword arm kept from pointing in across its own body (the stub would go into it): at most a little
        /// past straight ahead, measured on the body as it is turned (BodyTurn).</summary>
        Vector3 KeepArmOut(Vector3 dir)
        {
            Vector3 side = Quaternion.AngleAxis(Pawn.BodyTurn, Vector3.up) * Vector3.Cross(Vector3.up, Pawn.Facing);
            float across = Vector3.Dot(dir.normalized, side);
            // 10-28: eased onto that limit (a smooth max) instead of stopped at it, which put a corner in the arm's way.
            float kept = .5f * (across - .3f + Mathf.Sqrt((across + .3f) * (across + .3f) + .15f * .15f));
            dir = dir.normalized + side * (kept - across);
            // 10-25 (승규 님: "나이트가 몸에 관통 돼"): a cut pulled in across the front took the hand and the guard into a
            // piece that has more of itself out in front (the knight's chest); turning the blade cannot move them, so the
            // arm is turned out round the body until they are clear (eased, like the blade's own turn).
            if (armOutFrame != Time.frameCount)
            {
                armOutFrame = Time.frameCount;
                float rate = 0f, dt = Mathf.Max(Time.deltaTime, 1e-4f);
                if (bodyProfile != null && sword != null)
                {
                    Vector3 grip = ClickGrip(), guardAt = sword.position + sword.forward * (modelLift + .09f * modelScale);
                    Vector3 quillon = sword.right * .1f * modelScale;
                    bool Touches(float extra) => InBody(grip, .02f + extra) || InBody(guardAt, .01f + extra)
                        || InBody(guardAt + quillon, .01f + extra) || InBody(guardAt - quillon, .01f + extra);
                    // Out while they touch, back in only once well clear (so it does not rock at the edge).
                    if (Touches(0f)) rate = 10f;
                    else if (!Touches(.04f)) rate = -3f;
                }
                else rate = -3f;
                // 10-28: the arm eased after that (it started and stopped at full speed).
                armOutGoal = Mathf.Clamp(armOutGoal + rate * dt, 0f, 1.2f);
                armOut = Mathf.SmoothDamp(armOut, armOutGoal, ref armOutSpeed, .05f, Mathf.Infinity, dt);
            }
            // Out toward the sword side as far as needed (a push, not a turn about up: an arm pointing down turned about
            // up does not move its hand).
            return (dir.normalized + side * armOut).normalized;
        }
        void PoseClickSword()
        {
            Vector3 dir = ClickBladeDirection(), up = BladeUp(dir);
            // An upright blade has no "up" of its own to roll by: it keeps its last roll.
            if (Mathf.Abs(Vector3.Dot(up.normalized, dir)) > .98f) up = sword.up;
            sword.SetPositionAndRotation(ClickGrip(), Quaternion.LookRotation(dir, up));
        }
    }
}
