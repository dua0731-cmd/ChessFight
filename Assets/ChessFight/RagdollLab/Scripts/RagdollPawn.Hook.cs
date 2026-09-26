using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Where the pawn's grappling hook is (Queen of the Hill M5).</summary>
    public enum HookPhase : byte
    {
        None = 0,      // put away
        Held = 1,      // out, hanging from the right hand (E)
        Charging = 2,  // left button held: swung round overhead, the gauge filling
        Flying = 3,    // thrown
        Pulling = 4,   // stuck in something; the pawn is reeled up the rope
        Stuck = 5,     // arrived; the hook stays in for enPassantWindow, then comes back to the hand
    }

    /// <summary>
    /// The pawn's grappling hook (Queen of the Hill M5, DESIGN §4.1): the pawn's two-square first move.
    ///
    /// E takes it out and puts it away. Holding the left button swings it round overhead like a sling
    /// while HookCharge fills over hookChargeTime; letting go throws it along the aim, faster the fuller
    /// the gauge (hookSpeedMin..hookSpeedMax, hookLoft above the aim). It sticks in the first thing it
    /// hits - any face at any angle, only pawns are passed through - and the pawn is reeled up the rope
    /// at hookPullSpeed, placed bone by bone the way the climb places it (the rope is the script, not a
    /// joint). Where it ends depends on what the hook is in: on a top the pawn stands up on it; at a
    /// wall it hangs on the rope until the right button takes hold of the wall (and it is climbing);
    /// under a ceiling it hangs. On the way, a face in the way is slid along (up and over the lip
    /// below a top). A throw that hits nothing within hookMaxFlight is a
    /// miss, and the next one waits hookRetryDelay. Space or E lets go of the rope.
    ///
    /// En passant: an enemy (Teams.AreEnemies) holding interact within enPassantRadius of the hook for enPassantHold,
    /// while the pawn is still reeling in or up to enPassantWindow after it arrived, cuts the hook out
    /// and the pawn falls (or is thrown back off the top it just reached).
    ///
    /// All of it runs on the machine that simulates the pawn. A network puppet only shows what the
    /// snapshot says: HookPhase, HookCharge and HookPoint.
    /// </summary>
    public partial class RagdollPawn
    {
        const float HookRadius = 0.08f;
        /// <summary>The hips hang this far below the hands on the rope.</summary>
        const float RopeHang = 0.62f;
        const float RopeBodyRadius = 0.2f;

        HookPhase hookPhase;
        float hookCharge, hookSpin, hookRetry, hookFlight, hookPullSpeed, hookStuckTime, hookBlocked, ropeSettle, ropeTime;
        Vector3 hookPos, hookVel, hookNormal = Vector3.up, hookLocalPoint, hookLocalNormal, hookThrownFrom;
        Collider hookCollider;
        float enPassantTimer;
        RagdollPawn enPassantTarget;
        HookPhase netHookPhase;
        float netHookCharge;
        Vector3 netHookPoint;

        /// <summary>Where the hook is. Remote pawns read it off the wire.</summary>
        public HookPhase Hook => NetworkPuppet ? netHookPhase : hookPhase;
        public bool HookCharging => Hook == HookPhase.Charging;
        /// <summary>0..1, the throw gauge while swinging (and what the last throw used).</summary>
        public float HookCharge => NetworkPuppet ? netHookCharge : hookCharge;
        /// <summary>Flying: where the hook is now. Pulling / Stuck: where it is stuck. World space.</summary>
        public Vector3 HookPoint => NetworkPuppet ? netHookPoint : hookPos;
        /// <summary>The face the hook is stuck in (host only; up when not stuck).</summary>
        public Vector3 HookNormal => hookNormal;
        /// <summary>The swing's angle, radians, for drawing the hook going round.</summary>
        public float HookSpin => hookSpin;
        /// <summary>0..1 while this pawn holds interact over an enemy's hook.</summary>
        public float EnPassantProgress => enPassantTarget != null ? Mathf.Clamp01(enPassantTimer / Mathf.Max(0.01f, P.enPassantHold)) : 0f;
        public int HookThrows { get; private set; }
        public int HookHits { get; private set; }
        public int HookMisses { get; private set; }
        public int HookArrivals { get; private set; }
        /// <summary>Hooks this pawn has cut out of the wall (en passant), and times its own was cut.</summary>
        public int EnPassantCuts { get; private set; }
        public int HookCutOff { get; private set; }
        public string LastHookEvent { get; private set; } = "-";

        /// <summary>Swing rate, turns a second, for a gauge value: a lazy circle to a whirl.</summary>
        public static float HookSpinRate(float charge) => Mathf.Lerp(1.2f, 3.5f, Mathf.Clamp01(charge));

        /// <summary>
        /// Where the hook leaves from and how fast, for a gauge value. One function for the throw and the
        /// aim preview, so the dotted arc is the flight. Thrown from above the head (the swing's centre),
        /// hookLoft above the aim, never steeper than 85 degrees.
        /// </summary>
        public static void HookLaunch(RagdollParams p, Vector3 head, Vector3 aim, Vector3 facing, float charge,
                                      out Vector3 position, out Vector3 velocity)
        {
            Vector3 dir = aim.sqrMagnitude > 0.25f ? aim.normalized : (facing + Vector3.up * 0.3f).normalized;
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-4f) flat = new Vector3(facing.x, 0f, facing.z);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
            float rise = Mathf.Min(Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg + p.hookLoft, 85f) * Mathf.Deg2Rad;
            dir = flat * Mathf.Cos(rise) + Vector3.up * Mathf.Sin(rise);
            position = head + Vector3.up * 0.45f;
            velocity = dir * Mathf.Lerp(p.hookSpeedMin, p.hookSpeedMax, Mathf.Clamp01(charge));
        }

        /// <summary>
        /// One physics step of a flying hook: gravity, then a swept ball against everything but pawns.
        /// True when it hit something, with the hook left at the contact.
        /// </summary>
        public bool HookFlightStep(RagdollParams p, ref Vector3 position, ref Vector3 velocity, float dt, out RaycastHit hit)
        {
            velocity += Vector3.down * (p.hookGravity * dt);
            Vector3 move = velocity * dt;
            float length = move.magnitude;
            hit = default;
            if (length > 1e-5f && HookCast(position, move / length, length, out hit))
            {
                position = hit.point;
                return true;
            }
            position += move;
            return false;
        }

        bool HookCast(Vector3 from, Vector3 dir, float length, out RaycastHit hit)
        {
            hit = default;
            float best = float.MaxValue;
            bool found = false;
            int n = Physics.SphereCastNonAlloc(from, HookRadius, dir, hits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                // Starting inside something reports distance 0 and no point: not a hit to stick in.
                if (h.distance <= 0f || h.distance >= best) continue;
                if (ownSet.Contains(h.collider) || ColliderOwner.ContainsKey(h.collider) || PassesThrough(h.collider)) continue;
                best = h.distance;
                hit = h;
                found = true;
            }
            return found;
        }

        bool CanThrowHook => hookRetry <= 0f && CanSwingHook && HookStartZone.Allows(bodies[0].position);

        bool CanSwingHook => State == PawnState.Active && !Climbing && topOutTimer <= 0f && !Floating && !BeingHeld && !Grabbing;

        void UpdateHook(RagdollParams p, float dt)
        {
            hookRetry -= dt;
            if (input.ability)
            {
                if (hookPhase == HookPhase.None)
                {
                    hookPhase = HookPhase.Held;
                    LastHookEvent = "꺼냄";
                }
                else PutHookAway(p);
            }
            switch (hookPhase)
            {
                case HookPhase.Held:
                    if (input.shove && CanThrowHook)
                    {
                        hookPhase = HookPhase.Charging;
                        hookCharge = 0f;
                    }
                    break;
                case HookPhase.Charging:
                    SwingHook(p, dt);
                    break;
                case HookPhase.Flying:
                    FlyHook(p, dt);
                    break;
                case HookPhase.Pulling:
                    PullRope(p, dt);
                    break;
                case HookPhase.Stuck:
                    hookStuckTime += dt;
                    if (!RefreshHookPoint() || hookStuckTime >= p.enPassantWindow) hookPhase = HookPhase.Held;
                    break;
            }
            // With the hook out the left button is the hook's: no dive. Holding someone it still throws
            // them, and in the water it still thrashes.
            if (hookPhase != HookPhase.None && !Grabbing && !Floating) input.shove = false;
            EnPassant(p, dt);
        }

        void PutHookAway(RagdollParams p)
        {
            if (hookPhase == HookPhase.Pulling) ReleaseRope(p, false);
            hookPhase = HookPhase.None;
            hookCharge = 0f;
            LastHookEvent = "넣음";
        }

        /// <summary>Anything that knocks the pawn loose (a hit that drops it, the water, a knockdown) lets
        /// go of the swing, the throw and the rope; the hook stays in the hand.</summary>
        void DropHook()
        {
            if (hookPhase == HookPhase.None || hookPhase == HookPhase.Held) return;
            hookPhase = HookPhase.Held;
            hookRetry = Mathf.Max(hookRetry, 0.3f);
        }

        void SwingHook(RagdollParams p, float dt)
        {
            if (!CanSwingHook)
            {
                hookPhase = HookPhase.Held;
                return;
            }
            hookCharge = Mathf.Min(1f, hookCharge + dt / Mathf.Max(0.05f, p.hookChargeTime));
            hookSpin = Mathf.Repeat(hookSpin + HookSpinRate(hookCharge) * Mathf.PI * 2f * dt, Mathf.PI * 2f);
            // Square up to the aim, so the throw goes where the camera looks and the swing reads from behind.
            Vector3 aim = Flat(input.aim);
            if (aim.sqrMagnitude > 1e-4f) facing = Vector3.RotateTowards(facing, aim.normalized, 12f * dt, 0f);
            if (input.shoveHeld) return;
            HookLaunch(p, bodies[(int)BodyId.Head].position, input.aim, facing, hookCharge, out hookPos, out hookVel);
            hookThrownFrom = bodies[0].position;
            hookPhase = HookPhase.Flying;
            hookFlight = 0f;
            HookThrows++;
            LastHookEvent = $"던짐 (게이지 {hookCharge * 100f:0}%)";
        }

        void FlyHook(RagdollParams p, float dt)
        {
            hookFlight += dt;
            if (HookFlightStep(p, ref hookPos, ref hookVel, dt, out var hit))
            {
                // Knocked down, on a wall or in someone's hands by the time it lands: nothing to reel in.
                if (State != PawnState.Active || Climbing || Floating || BeingHeld) Miss(p, "박혔지만 끌려갈 수 없음");
                else Stick(hit);
                return;
            }
            if (hookFlight >= p.hookMaxFlight) Miss(p, "빗나감");
        }

        void Miss(RagdollParams p, string why)
        {
            hookPhase = HookPhase.Held;
            hookRetry = p.hookRetryDelay;
            HookMisses++;
            LastHookEvent = why;
        }

        void Stick(RaycastHit hit)
        {
            hookCollider = hit.collider;
            Transform t = hookCollider.transform;
            hookLocalPoint = t.InverseTransformPoint(hit.point);
            hookLocalNormal = t.InverseTransformDirection(hit.normal);
            hookPos = hit.point;
            hookNormal = hit.normal;
            hookPhase = HookPhase.Pulling;
            // A run-up counts: whatever speed the pawn already has along the rope, it keeps.
            Vector3 rope = hookPos - bodies[0].position;
            hookPullSpeed = Mathf.Max(0f, Vector3.Dot(ComVelocity(), rope.normalized));
            hookBlocked = 0f;
            ropeSettle = 0f;
            ropeTime = 0f;
            HookHits++;
            LastHookEvent = hit.normal.y > 0.6f ? "박힘: 윗면" : hit.normal.y < -0.6f ? "박힘: 천장" : "박힘: 벽";
            handL.Release();
            handR.Release();
            shoveTimer = 0f;
            throwOnShoveEnd = false;
            anchorPos = bodies[0].position;
            Vector3 flat = Flat(rope);
            if (flat.sqrMagnitude > 1e-4f) facing = flat.normalized;
        }

        /// <summary>The hook rides whatever it is stuck in (a moving wall carries it). False when that is gone.</summary>
        bool RefreshHookPoint()
        {
            if (hookCollider == null || !hookCollider.enabled || !hookCollider.gameObject.activeInHierarchy) return false;
            Transform t = hookCollider.transform;
            hookPos = t.TransformPoint(hookLocalPoint);
            hookNormal = t.TransformDirection(hookLocalNormal).normalized;
            return true;
        }

        void PullRope(RagdollParams p, float dt)
        {
            if (input.jump)
            {
                ReleaseRope(p, true);
                return;
            }
            if (State != PawnState.Active || Floating || Climbing || BeingHeld || !RefreshHookPoint())
            {
                ReleaseRope(p, false);
                return;
            }
            bool top = hookNormal.y > 0.6f, ceiling = hookNormal.y < -0.6f;
            ropeTime += dt;
            // Where the hips are going: standing on a top, hanging from the hook under a ceiling, a hug's
            // distance off a wall with the hands at the hook.
            Vector3 target = top ? hookPos + Vector3.up * (standHeight + 0.03f)
                : ceiling ? hookPos - Vector3.up * (RopeHang + 0.2f)
                : hookPos - Vector3.up * RopeHang + hookNormal * ClimbHug(p);
            hookPullSpeed = Mathf.MoveTowards(hookPullSpeed, p.hookPullSpeed, p.hookPullSpeed / Mathf.Max(0.01f, p.hookPullRamp) * dt);
            Vector3 to = target - anchorPos;
            float dist = to.magnitude;
            Vector3 next = anchorPos;
            if (dist > 1e-4f)
            {
                Vector3 step = to / dist * Mathf.Min(dist, hookPullSpeed * dt);
                next += RopeSlide(step, target, out bool jammed, out RaycastHit wall);
                if (jammed && !ClimbableHit(p, wall))
                {
                    // Squashed against a floor or a ceiling short of the hook: give up after a moment.
                    hookBlocked += dt;
                    if (hookBlocked > 0.5f)
                    {
                        ReleaseRope(p, false);
                        return;
                    }
                }
                else hookBlocked = 0f;
                // Pulled flat into a wall short of the hook, it hangs against the wall like at a hook in a
                // wall (below): the right button takes hold of it.
            }
            anchorVel = (next - anchorPos) / Mathf.Max(dt, 1e-4f);
            anchorPos = next;
            // Over the lip onto a top: the last half metre stands the body up.
            float left = Vector3.Distance(anchorPos, target);
            ropeSettle = top && anchorPos.y > hookPos.y ? Mathf.Clamp01(1f - left / 0.6f) : 0f;
            Vector3 toward = Flat(hookPos - anchorPos);
            if (toward.sqrMagnitude > 0.01f) facing = Vector3.RotateTowards(facing, toward.normalized, 10f * dt, 0f);
            anchor.MovePosition(anchorPos);
            anchor.MoveRotation(Quaternion.LookRotation(facing, Vector3.up));
            if (top && left <= 0.05f)
            {
                ArriveOnTop(p);
                return;
            }
            // At a wall - the one the hook is in, or one the rope pulled it flat against - it hangs on the
            // rope until the right button takes hold of the wall (the climb needs the button held anyway,
            // so taking hold for the player would only drop them). Under a ceiling it just hangs.
            if (input.grab && !ceiling)
            {
                Vector3 chest = bodies[(int)BodyId.Chest].position;
                Vector3 into = Flat(facing).sqrMagnitude > 1e-4f ? Flat(facing).normalized : -hookNormal;
                if (WallRay(p, chest, into, ClimbHug(p) + 0.6f, out var face) || WallRay(p, chest, -hookNormal, ClimbHug(p) + 0.6f, out face))
                    HoldWallFromRope(p, face);
            }
        }

        /// <summary>
        /// Move the body along the rope by <paramref name="step"/>, sliding along anything in the way: up a
        /// face and over its lip on the way to a top. Jammed means pulled square into something.
        /// </summary>
        Vector3 RopeSlide(Vector3 step, Vector3 target, out bool jammed, out RaycastHit wall)
        {
            jammed = false;
            wall = default;
            float length = step.magnitude;
            if (length < 1e-6f || !RopeBodyCast(anchorPos, step / length, length, out var hit)) return step;
            wall = hit;
            Vector3 before = step / length * Mathf.Max(0f, hit.distance - 0.01f);
            Vector3 rest = step - before;
            // A face in the way with the hook higher up: the rope runs over the lip, so it pulls straight up
            // the face, whatever the angle to the hook itself. Aimed at the line to the hook, a hook a few
            // metres back from the edge pulls almost level, and the pawn stalled halfway up the wall.
            Vector3 upFace = Vector3.ProjectOnPlane(Vector3.up, hit.normal);
            Vector3 along;
            if (target.y > anchorPos.y + 0.05f && upFace.sqrMagnitude > 0.1f) along = upFace.normalized;
            else
            {
                along = Vector3.ProjectOnPlane(rest, hit.normal);
                if (along.magnitude < 0.2f * rest.magnitude)
                {
                    jammed = true;
                    return before;
                }
                along.Normalize();
            }
            // Full speed along the face, not just the part of the pull that happens to point along it.
            Vector3 slide = along * rest.magnitude;
            if (RopeBodyCast(anchorPos + before, along, slide.magnitude, out var second))
                slide = along * Mathf.Max(0f, second.distance - 0.01f);
            return before + slide;
        }

        /// <summary>The body on the rope as two balls, the hips and the head, swept together.</summary>
        bool RopeBodyCast(Vector3 hips, Vector3 dir, float length, out RaycastHit hit)
        {
            bool found = BodyBall(hips, RopeBodyRadius, dir, length, out hit);
            if (BodyBall(hips + Vector3.up * headRise, headRadius, dir, length, out var head) && (!found || head.distance < hit.distance))
            {
                hit = head;
                found = true;
            }
            return found;
        }

        bool BodyBall(Vector3 center, float radius, Vector3 dir, float length, out RaycastHit hit)
        {
            hit = default;
            float best = float.MaxValue;
            bool found = false;
            int n = Physics.SphereCastNonAlloc(center, radius, dir, hits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance <= 0f || h.distance >= best) continue;
                if (ownSet.Contains(h.collider) || ColliderOwner.ContainsKey(h.collider) || PassesThrough(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                best = h.distance;
                hit = h;
                found = true;
            }
            return found;
        }

        /// <summary>Up on the top: physics back on, standing, with a small step on in the rope's direction.</summary>
        void ArriveOnTop(RagdollParams p)
        {
            Vector3 on = Flat(hookPos - hookThrownFrom);
            anchorVel = (on.sqrMagnitude > 1e-4f ? on.normalized : facing) * 1f;
            wallPoint = hookPos;
            wallNormal = Vector3.up;
            Arrived();
        }

        /// <summary>Hanging at a wall with the right button held: take hold of it and climb from here. The
        /// climb keeps the body placed, so the hands go from the rope to the wall without it dropping.</summary>
        void HoldWallFromRope(RagdollParams p, RaycastHit face)
        {
            StartClimb(p, face);
            Arrived();
        }

        void Arrived()
        {
            hookPhase = HookPhase.Stuck;
            hookStuckTime = 0f;
            ropeSettle = 0f;
            HookArrivals++;
            LastHookEvent = Climbing ? "도착: 벽에 매달림" : "도착: 올라섬";
        }

        /// <summary>Off the rope, carrying its speed (and a hop, if Space let go).</summary>
        void ReleaseRope(RagdollParams p, bool jumped)
        {
            hookPhase = HookPhase.Held;
            hookRetry = Mathf.Max(hookRetry, 0.3f);
            ropeSettle = 0f;
            if (jumped)
            {
                anchorVel += Vector3.up * (p.jumpImpulse * 0.6f);
                jumpTimer = 0.4f;
            }
            wallPoint = hookPos;
            wallNormal = hookNormal;
            // FixedUpdate turns the body back into a ragdoll after the pose step (EndClimbPose), with
            // anchorVel as its speed.
        }

        bool HookCuttable => hookPhase == HookPhase.Pulling
                             || (hookPhase == HookPhase.Stuck && hookStuckTime < P.enPassantWindow);

        /// <summary>Holding interact over someone else's stuck hook for enPassantHold cuts it out.</summary>
        void EnPassant(RagdollParams p, float dt)
        {
            RagdollPawn target = null;
            if (input.interact && State == PawnState.Active && !Climbing && hookPhase != HookPhase.Pulling && !Floating && !BeingHeld)
            {
                Vector3 me = bodies[(int)BodyId.Chest].position;
                float best = p.enPassantRadius;
                foreach (var other in All)
                {
                    // Only an enemy's hook (M10): a teammate's is left alone.
                    if (other == null || other == this || !other.HookCuttable || !Teams.AreEnemies(Team, other.Team)) continue;
                    float d = Vector3.Distance(me, other.hookPos);
                    if (d > best) continue;
                    best = d;
                    target = other;
                }
            }
            if (target != enPassantTarget)
            {
                enPassantTarget = target;
                enPassantTimer = 0f;
            }
            if (target == null) return;
            enPassantTimer += dt;
            if (enPassantTimer < p.enPassantHold) return;
            target.CutHook();
            EnPassantCuts++;
            LastHookEvent = "앙파상!";
            enPassantTarget = null;
            enPassantTimer = 0f;
        }

        /// <summary>En passant: the hook comes out of the wall. On the rope the pawn falls; just arrived, it
        /// is thrown back the way it came.</summary>
        void CutHook()
        {
            HookCutOff++;
            LastHookEvent = "앙파상 당함";
            var p = P;
            if (hookPhase == HookPhase.Pulling)
            {
                ReleaseRope(p, false);
                return;
            }
            hookPhase = HookPhase.Held;
            hookRetry = Mathf.Max(hookRetry, p.hookRetryDelay);
            Vector3 back = Flat(hookThrownFrom - hookPos);
            back = back.sqrMagnitude > 1e-4f ? back.normalized : -facing;
            TakeHit(back * 4f + Vector3.up * 2f, 1f, 0f, true);
        }

        /// <summary>
        /// On the rope the pawn is placed like the climb places it: hanging under both hands, lined up
        /// along the rope, both palms on it one above the other. The legs and head come from the puppet
        /// (HookPose); over the lip of a top the body straightens up to stand (ropeSettle).
        /// </summary>
        void ApplyRopePose(RagdollParams p)
        {
            if (!climbKinematic)
            {
                foreach (var rb in bodies) rb.isKinematic = true;
                climbKinematic = true;
            }
            Vector3 hands = anchorPos + Vector3.up * RopeHang;
            Vector3 rope = hookPos - hands;
            Vector3 along = rope.sqrMagnitude > 1e-4f ? rope.normalized : Vector3.up;
            Vector3 bodyUp = Vector3.Slerp(Vector3.up, along.y > 0f ? along : Vector3.up, 0.5f * (1f - ropeSettle)).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(facing, bodyUp);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(Vector3.forward, bodyUp);
            poseRot[0] = Quaternion.LookRotation(forward.normalized, bodyUp);
            // Swinging a little from side to side under the hands, strongest as the rope first snaps
            // taut, so it hangs rather than rides up like a lift.
            float sway = 11f * Mathf.Sin(ropeTime * Mathf.PI * 2f * 1.1f) * Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(ropeTime / 1.5f))
                         * (1f - ropeSettle);
            poseRot[0] = Quaternion.AngleAxis(sway, forward.normalized) * poseRot[0];
            poseScratch[0] = anchorPos;
            for (int i = 1; i < Count; i++)
            {
                int parent = ParentOf[i];
                poseRot[i] = poseRot[parent] * puppet[i].localRotation;
                poseScratch[i] = poseScratch[parent] + poseRot[parent] * jointOffset[i];
            }
            if (ropeSettle < 0.5f)
            {
                Vector3 across = Vector3.Cross(bodyUp, forward).normalized;
                for (int slot = 0; slot < 2; slot++)
                {
                    int arm = slot == 0 ? (int)BodyId.ArmL : (int)BodyId.ArmR;
                    int hand = arm + 1;
                    // Right hand low, left hand above it; both on the rope.
                    Vector3 palm = hands + along * (slot == 1 ? 0.02f : 0.14f) + across * (slot == 0 ? 0.03f : -0.03f);
                    palm = poseScratch[arm] + Vector3.ClampMagnitude(palm - poseScratch[arm], p.climbArmReach);
                    Vector3 reach = palm - poseScratch[arm];
                    if (reach.sqrMagnitude < 1e-6f) reach = bodyUp;
                    Quaternion aim = Quaternion.FromToRotation(slot == 0 ? Vector3.left : Vector3.right, reach.normalized);
                    poseRot[arm] = aim;
                    poseRot[hand] = aim;
                    poseScratch[hand] = palm - aim * palmLocal[slot];
                }
            }
            for (int i = 0; i < Count; i++)
            {
                bodies[i].MovePosition(poseScratch[i]);
                bodies[i].MoveRotation(poseRot[i]);
            }
        }

        /// <summary>Where the swung hook is on its circle over the head, for an angle. The drawing and the
        /// swinging arm both use it.</summary>
        public Vector3 HookSwingPoint(float spin, float charge)
        {
            Transform hips = bodies[0].transform;
            Vector3 forward = Flat(hips.forward);
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 center = bodies[(int)BodyId.Head].position + Vector3.up * 0.4f;
            return center + (forward * Mathf.Cos(spin) + right * Mathf.Sin(spin)) * Mathf.Lerp(0.45f, 0.8f, Mathf.Clamp01(charge));
        }

        /// <summary>The hook's part of the puppet pose: the swing, the follow-through, hanging on the rope.</summary>
        void HookPose(RagdollParams p, ref Quaternion armL, ref Quaternion armR, ref Quaternion chest, ref Quaternion head,
                      ref Quaternion thighL, ref Quaternion thighR, ref Quaternion footL, ref Quaternion footR)
        {
            Transform chestT = bodies[(int)BodyId.Chest].transform;
            Vector3 shoulder = bodies[(int)BodyId.ArmR].position;
            switch (hookPhase)
            {
                case HookPhase.Charging:
                {
                    // The right arm up and round with the hook, the body leaning into the swing.
                    Vector3 at = HookSwingPoint(hookSpin, hookCharge) - shoulder;
                    armR = Quaternion.FromToRotation(Vector3.right, chestT.InverseTransformDirection(at.normalized));
                    armL = Quaternion.Euler(0f, 20f, 25f);
                    chest = chest * Quaternion.Euler(-6f, 12f * Mathf.Sin(hookSpin), 0f);
                    head = Quaternion.Euler(-8f, 0f, 0f);
                    break;
                }
                case HookPhase.Flying:
                {
                    // Follow-through: the arm points down the rope after the hook.
                    Vector3 at = hookPos - shoulder;
                    if (at.sqrMagnitude > 1e-4f)
                        armR = Quaternion.FromToRotation(Vector3.right, chestT.InverseTransformDirection(at.normalized));
                    break;
                }
                case HookPhase.Pulling:
                {
                    // Hanging on: looking up the rope, legs dangling and kicking a little. Over the lip of a
                    // top they come under the body to stand.
                    // Running on air: the legs cycle against each other.
                    float kick = Mathf.Sin(ropeTime * Mathf.PI * 2f * 1.6f);
                    thighL = Quaternion.Euler(-25f + 22f * kick, 0f, 0f);
                    thighR = Quaternion.Euler(-25f - 22f * kick, 0f, 0f);
                    footL = footR = Quaternion.Euler(22f, 0f, 0f);
                    chest = Quaternion.Euler(-10f, 0f, 0f);
                    head = Quaternion.Euler(-24f, 0f, 0f);
                    if (ropeSettle > 0f)
                    {
                        thighL = Quaternion.Slerp(thighL, Quaternion.identity, ropeSettle);
                        thighR = Quaternion.Slerp(thighR, Quaternion.identity, ropeSettle);
                        footL = Quaternion.Slerp(footL, Quaternion.identity, ropeSettle);
                        footR = Quaternion.Slerp(footR, Quaternion.identity, ropeSettle);
                        chest = Quaternion.Slerp(chest, Quaternion.identity, ropeSettle);
                        head = Quaternion.Slerp(head, Quaternion.identity, ropeSettle);
                        armL = Quaternion.Euler(0f, 0f, p.armRestDown);
                        armR = Quaternion.Euler(0f, 0f, -p.armRestDown);
                    }
                    break;
                }
            }
        }

        void CaptureHook(RagdollPose pose)
        {
            pose.hook = (byte)hookPhase;
            pose.hookCharge = hookCharge;
            pose.hookPoint = hookPos;
        }

        void ApplyHook(RagdollPose pose)
        {
            netHookPhase = pose.hook <= (byte)HookPhase.Stuck ? (HookPhase)pose.hook : HookPhase.None;
            netHookCharge = pose.hookCharge;
            netHookPoint = pose.hookPoint;
        }

        void ClearHook()
        {
            hookPhase = HookPhase.None;
            hookCharge = hookRetry = hookFlight = hookPullSpeed = hookStuckTime = hookBlocked = ropeSettle = 0f;
            hookCollider = null;
            enPassantTarget = null;
            enPassantTimer = 0f;
        }
    }
}
