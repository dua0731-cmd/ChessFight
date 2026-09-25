using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public enum BodyId { Hips, Chest, Head, ArmL, HandL, ArmR, HandR, ThighL, FootL, ThighR, FootR }

    public enum PawnState { Active, Ragdoll, GettingUp }

    public struct PawnInput
    {
        /// <summary>World-space horizontal move direction, length 0..1.</summary>
        public Vector3 move;
        public bool jump;
        public bool shove;
        public bool grab;
    }

    /// <summary>
    /// Active ragdoll pawn: a non-physical puppet produces target poses, ConfigurableJoint slerp
    /// drives make the physical body follow them, and a kinematic LocomotionAnchor pulls the hips.
    /// One dynamic multiplier scales every spring, so "stiff" and "floppy" blend continuously.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class RagdollPawn : MonoBehaviour
    {
        public const int Count = 11;
        public static readonly int[] ParentOf = { -1, 0, 1, 1, 3, 1, 5, 0, 7, 0, 9 };

        /// <summary>
        /// Measured in this project (Unity 6000.3, PhysX): a Slerp drive only delivers ~1/4 of its
        /// positionSpring/positionDamper as N·m/rad, while linear and XY&amp;Z drives are 1:1. Scaling by 4
        /// makes every spring slider read as real N·m/rad.
        /// </summary>
        public const float SlerpDriveScale = 4f;
        public static readonly Dictionary<Collider, RagdollPawn> ColliderOwner = new Dictionary<Collider, RagdollPawn>();
        public static readonly List<RagdollPawn> All = new List<RagdollPawn>();

        [Header("Built by RagdollLabBuilder")]
        public RagdollTuning tuning;
        public Rigidbody[] bodies = new Rigidbody[Count];
        public ConfigurableJoint[] joints = new ConfigurableJoint[Count];
        public Transform[] puppet = new Transform[Count];
        public Rigidbody anchor;
        public ConfigurableJoint anchorJoint;
        public PawnHand handL;
        public PawnHand handR;
        public BoxCollider footL;
        public BoxCollider footR;
        public float standHeight = 0.256f;
        public SkinnedMeshRenderer skin;

        public string DisplayName { get; set; } = "Pawn";
        public PawnState State { get; private set; } = PawnState.Active;
        public float Stiffness { get; private set; } = 1f;
        public float TargetStiffness { get; private set; } = 1f;
        public float StateFactor { get; private set; } = 1f;
        public bool Grounded { get; private set; }
        public float SlopeAngle { get; private set; }
        public bool Touching => contactTimer > 0f;
        public bool Stunned => hitTimer > 0f;
        public bool OnSlope => Grounded && SlopeAngle >= P.slopeAngleThreshold;
        public bool Grabbing => handL.IsHolding || handR.IsHolding;
        public bool Shoving => shoveTimer > 0f;
        public int Knockdowns { get; private set; }
        public string LastKnockdownCause { get; private set; } = "-";
        public float LastRagdollTime { get; private set; }
        public Rigidbody Hips => bodies[0];
        public Vector3 Facing => facing;
        public float HipsTilt => Vector3.Angle(bodies[0].transform.up, Vector3.up);
        public float HorizontalSpeed => Flat(bodies[0].linearVelocity).magnitude;
        public float EffectiveStiffness => Stiffness * StateFactor;
        public Vector3 AnchorPosition => anchorPos;

        /// <summary>Someone else's hand is holding this pawn right now.</summary>
        public bool BeingHeld => heldTimer > 0f;

        /// <summary>How hard the last thrash hit the grip, 0..1 against the break force. Display only.</summary>
        public float EscapeProgress => lastStrugglePunch;

        /// <summary>Hanging on a wall by the hands, spending stamina.</summary>
        public bool Climbing { get; private set; }

        /// <summary>0..1. Empty means the hands let go.</summary>
        public float Stamina => P.climbStaminaMax <= 0f ? 0f : Mathf.Clamp01(stamina / P.climbStaminaMax);

        /// <summary>Remote pawn: physics off, poses written from the network each frame.</summary>
        public bool NetworkPuppet { get; private set; }

        /// <summary>Set by Teleport so the next snapshot tells remotes to jump instead of interpolate.</summary>
        public bool NetworkSnap { get; set; }

        /// <summary>Test hook: return a pose for a body index to override the procedural puppet.</summary>
        public Func<int, Quaternion?> PoseOverride;

        public RagdollParams P => tuning != null ? tuning.values : fallback;
        readonly RagdollParams fallback = new RagdollParams();

        static PhysicsMaterial bodyMaterial, footMaterial, handMaterial, ragdollMaterial;

        PhysicsMaterial ownFootMaterial;
        PawnInput input;
        readonly Vector3[] jointOffset = new Vector3[Count];
        readonly Quaternion[] prevTarget = new Quaternion[Count];
        bool targetsPrimed;
        readonly Vector3[] poseScratch = new Vector3[Count];
        readonly Quaternion[] startRel = new Quaternion[Count];
        readonly Vector3[] bindPos = new Vector3[Count];
        readonly float[] baseMass = new float[Count];
        readonly Quaternion[] bindRot = new Quaternion[Count];
        readonly List<Collider> own = new List<Collider>();
        readonly List<PhysicsMaterial> ownMaterial = new List<PhysicsMaterial>();
        readonly HashSet<Collider> ownSet = new HashSet<Collider>();
        readonly Collider[] overlap = new Collider[16];
        readonly RaycastHit[] hits = new RaycastHit[16];
        Vector3 facing = Vector3.forward;
        Vector3 anchorPos, anchorVel;
        sealed class Leg
        {
            public Vector3 plant;
            public Vector3 swingFrom;
            public Vector3 target;
            public float swingT;
            public bool swinging;
            public bool ready;
        }

        readonly Leg[] legs = { new Leg(), new Leg() };
        int swingingLeg = -1;
        float landDip, turnRate, strideDrop, hopArc;
        float heldTimer, struggleTimer, escapeProgress, climbTimer, climbUp, climbSide, climbGrace, climbCooldown, topOutTimer;
        float handStep = 1f;
        int movingHand;
        Vector3 wallPoint, wallNormal = Vector3.forward;
        Vector3 climbUpAxis = Vector3.up, climbAcross = Vector3.right, climbFace;
        readonly Vector3[] handHold = new Vector3[2];
        readonly Quaternion[] poseRot = new Quaternion[Count];
        bool holdsPlaced, climbKinematic;
        Vector3 swingFrom;
        float climbCeiling;
        float struggleFlip = 1f, struggleRush, lastStrugglePunch;
        RagdollPawn holder;
        Collider heldCollider;
        float stamina = -1f;   // seconds; negative until the first tick reads the max
        bool wantsMove, wasGrounded;

        float contactTimer, hitTimer, shoveTimer, shoveCooldown, airTimer, coyote, stateTimer, gait;
        bool throwOnShoveEnd, pullUpUsed;
        float vaultTimer, vaultTop;
        Vector3 vaultDirection;
        bool groundFound;
        float groundY;
        Vector3 groundNormal = Vector3.up;
        Rigidbody groundBody;

        void Awake()
        {
            All.Add(this);
            EnsureMaterials();
            // Per-pawn foot material: grip while standing, slide while running (friction set each step).
            // Minimum combine so the pawn's value wins over the floor's default 0.6.
            ownFootMaterial = new PhysicsMaterial("PawnFoot " + name)
            {
                dynamicFriction = footMaterial.dynamicFriction, staticFriction = footMaterial.staticFriction,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = footMaterial.bounceCombine,
            };
            for (int i = 0; i < Count; i++)
            {
                foreach (var c in bodies[i].GetComponentsInChildren<Collider>(true))
                {
                    if (!ownSet.Add(c)) continue;
                    own.Add(c);
                    ColliderOwner[c] = this;
                    c.sharedMaterial = c == footL || c == footR ? ownFootMaterial
                        : (i == (int)BodyId.HandL || i == (int)BodyId.HandR) ? handMaterial
                        : bodyMaterial;
                    ownMaterial.Add(c.sharedMaterial);
                }
            }
            for (int a = 0; a < own.Count; a++)
                for (int b = a + 1; b < own.Count; b++)
                    Physics.IgnoreCollision(own[a], own[b], true);

            for (int i = 0; i < Count; i++) baseMass[i] = bodies[i].mass;

            Transform hips = bodies[0].transform;
            Quaternion hipsInv = Quaternion.Inverse(hips.rotation);
            for (int i = 0; i < Count; i++)
            {
                Transform t = bodies[i].transform;
                bindPos[i] = hipsInv * (t.position - hips.position);
                bindRot[i] = hipsInv * t.rotation;
                if (i > 0) startRel[i] = Quaternion.Inverse(bodies[ParentOf[i]].transform.rotation) * t.rotation;
            }
            // Every joint locks linear motion at the child's own origin, so a child's world position is
            // its parent's position plus this fixed offset rotated by the parent. That is what lets a
            // remote pawn be rebuilt from rotations alone.
            for (int i = 1; i < Count; i++) jointOffset[i] = bindPos[i] - bindPos[ParentOf[i]];
            facing = FlatDir(hips.forward, Vector3.forward);
            anchorPos = hips.position;
            anchor.transform.SetPositionAndRotation(anchorPos, Quaternion.LookRotation(facing));
        }

        void OnDestroy()
        {
            All.Remove(this);
            foreach (var c in own)
                if (c != null && ColliderOwner.TryGetValue(c, out var o) && o == this) ColliderOwner.Remove(c);
            if (ownFootMaterial != null) Destroy(ownFootMaterial);
        }

        static void EnsureMaterials()
        {
            if (bodyMaterial != null) return;
            // Nearly frictionless body so a jump that bumps a wall slides up it instead of stalling.
            bodyMaterial = new PhysicsMaterial("PawnBody")
            {
                dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            footMaterial = new PhysicsMaterial("PawnFoot")
            {
                dynamicFriction = 0.6f, staticFriction = 0.7f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            handMaterial = new PhysicsMaterial("PawnHand")
            {
                dynamicFriction = 0.9f, staticFriction = 1f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum, bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            ragdollMaterial = new PhysicsMaterial("PawnRagdoll")
            {
                dynamicFriction = 0.1f, staticFriction = 0.1f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
            };
        }

        /// <summary>Low friction while limp so a tumble keeps its momentum (spec: rolling downhill is faster).</summary>
        void SetRagdollFriction(bool limp)
        {
            ragdollMaterial.dynamicFriction = P.ragdollFriction;
            ragdollMaterial.staticFriction = P.ragdollFriction;
            for (int i = 0; i < own.Count; i++)
                own[i].sharedMaterial = limp ? ragdollMaterial : ownMaterial[i];
        }

        public bool Owns(Collider c) => ownSet.Contains(c);

        public void SetInput(PawnInput next)
        {
            input.move = next.move;
            input.grab = next.grab;
            input.jump |= next.jump;
            input.shove |= next.shove;
        }

        public void NotifyHeld(RagdollPawn captor, Collider heldPart)
        {
            contactTimer = Mathf.Max(contactTimer, P.contactLinger);
            heldTimer = 0.2f;
            holder = captor;
            heldCollider = heldPart;
        }

        public void AddVelocity(Vector3 dv)
        {
            foreach (var rb in bodies) rb.linearVelocity += dv;
        }

        void FixedUpdate()
        {
            if (NetworkPuppet) return;
            var p = P;
            float dt = Time.fixedDeltaTime;
            contactTimer -= dt;
            hitTimer -= dt;
            shoveTimer -= dt;
            shoveCooldown -= dt;
            airTimer -= dt;

            SenseGround();
            coyote = Grounded ? 0.12f : coyote - dt;

            UpdateState(p, dt);
            UpdateStiffness(p, dt);
            float k = Stiffness * StateFactor;

            Jump(p);
            Mantle(dt);
            UpdateClimb(p, dt);
            Locomotion(p, dt);
            Struggle(p, dt);
            Shove(p);
            bool wantGrab = input.grab && State != PawnState.Ragdoll && !Climbing;
            handL.Tick(wantGrab, p, dt, Climbing);
            handR.Tick(wantGrab, p, dt, Climbing);
            if (Grounded) pullUpUsed = false; // one ledge vault per trip off the ground
            Pose(p, dt);
            if (Climbing) ApplyClimbPose(p);
            else EndClimbPose();
            Drives(p, k);

            input.jump = false;
            input.shove = false;
        }

        // ---------------------------------------------------------------- sensing

        void SenseGround()
        {
            Vector3 origin = bodies[0].position + Vector3.up * 0.05f;
            int n = Physics.SphereCastNonAlloc(origin, 0.09f, Vector3.down, hits, standHeight + 0.6f, ~0, QueryTriggerInteraction.Ignore);
            groundFound = false;
            groundBody = null;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (ownSet.Contains(h.collider) || h.distance <= 0f) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                groundY = h.point.y;
                groundNormal = h.normal;
                groundBody = h.rigidbody;
                groundFound = true;
            }
            bool feet = FootTouches(footL) || FootTouches(footR);
            bool near = groundFound && bodies[0].position.y - groundY < standHeight + 0.1f;
            Grounded = State != PawnState.Ragdoll && airTimer <= 0f && (feet || near);
            SlopeAngle = groundFound ? Vector3.Angle(groundNormal, Vector3.up) : 0f;
        }

        bool FootTouches(BoxCollider box)
        {
            Transform t = box.transform;
            Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.45f;
            Vector3 footCenter = t.TransformPoint(box.center);
            int n = Physics.OverlapBoxNonAlloc(footCenter + Vector3.down * 0.04f, half, overlap, t.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                if (ownSet.Contains(c)) continue;
                if (c is MeshCollider mesh && !mesh.convex) return true;
                // Only support under the foot counts; a wall beside the foot is not ground.
                if (c.ClosestPoint(footCenter).y < footCenter.y - half.y * 0.5f) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- state

        void UpdateState(RagdollParams p, float dt)
        {
            stateTimer += dt;
            switch (State)
            {
                case PawnState.Active:
                    StateFactor = 1f;
                    break;
                case PawnState.Ragdoll:
                    StateFactor = 0f;
                    if (stateTimer >= p.getUpDelay) BeginGetUp(p);
                    break;
                case PawnState.GettingUp:
                    float t = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, p.getUpBlendTime));
                    StateFactor = t * t * (3f - 2f * t);
                    if (t >= 1f)
                    {
                        State = PawnState.Active;
                        stateTimer = 0f;
                        StateFactor = 1f;
                    }
                    break;
            }
        }

        public void Knockdown(string cause)
        {
            EndClimbPose();
            if (State == PawnState.Ragdoll) return;
            State = PawnState.Ragdoll;
            stateTimer = 0f;
            StateFactor = 0f;
            Knockdowns++;
            LastKnockdownCause = cause;
            handL.Release();
            handR.Release();
            shoveTimer = 0f;
            throwOnShoveEnd = false;
            SetRagdollFriction(true);
        }

        void BeginGetUp(RagdollParams p)
        {
            LastRagdollTime = stateTimer;
            State = PawnState.GettingUp;
            stateTimer = 0f;
            SetRagdollFriction(false);
            Vector3 v = Flat(bodies[0].linearVelocity);
            anchorVel = v * p.momentumRetention;
            if (v.sqrMagnitude > 1f) facing = v.normalized;
            anchorPos = bodies[0].position;
        }

        void UpdateStiffness(RagdollParams p, float dt)
        {
            // Active conditions cap the stiffness; the lowest cap wins (a product would collapse to ~0.1).
            float m = 1f;
            if (contactTimer > 0f && shoveTimer <= 0f) m = Mathf.Min(m, p.contactStiffnessMultiplier);
            if (Grabbing) m = Mathf.Min(m, p.grabStiffnessMultiplier);
            if (OnSlope) m = Mathf.Min(m, p.slopeStiffnessMultiplier);
            if (hitTimer > 0f) m = Mathf.Min(m, p.hitStiffnessMultiplier);
            TargetStiffness = m;
            Stiffness = Mathf.Lerp(Stiffness, m, 1f - Mathf.Exp(-p.stiffnessLerpSpeed * dt));
        }

        /// <summary>Hit by another pawn's shove: counts as "피격" for the dynamic stiffness.</summary>
        public void NotifyShoved() => hitTimer = Mathf.Max(hitTimer, P.hitRecoveryTime);

        // ---------------------------------------------------------------- locomotion

        /// <summary>
        /// The anchor is a position spring, so a hard direction change lets it stretch to the leash and
        /// pull with hipAnchorStrength * leash newtons - 1800 N, or 36 m/s^2 on this pawn, more than the
        /// acceleration setting allows. The body then overshoots and ends up travelling faster than it
        /// can run. Trim the excess off the whole body (measured at the centre of mass, so a whipping
        /// limb does not trigger it), but leave slopes, jumps and knockdowns alone: momentum earned by
        /// throwing yourself down a hill is the point of the game.
        /// </summary>
        void ClampOverspeed(RagdollParams p, float dt)
        {
            if (p.overspeedClamp <= 0.001f || State != PawnState.Active || !Grounded || OnSlope) return;
            float limit = p.moveSpeed * p.overspeedClamp;
            Vector3 momentum = Vector3.zero;
            float mass = 0f;
            foreach (var rb in bodies)
            {
                momentum += rb.linearVelocity * rb.mass;
                mass += rb.mass;
            }
            Vector3 travel = Flat(momentum / Mathf.Max(0.001f, mass));
            float speed = travel.magnitude;
            if (speed <= limit) return;
            Vector3 excess = travel * ((speed - limit) / speed) * Mathf.Clamp01(dt * 25f);
            foreach (var rb in bodies) rb.linearVelocity -= excess;
        }

        void Locomotion(RagdollParams p, float dt)
        {
            Rigidbody hips = bodies[0];
            Vector3 hp = hips.position;
            if (State == PawnState.Ragdoll)
            {
                anchorPos = hp;
                anchorVel = Flat(hips.linearVelocity);
                anchor.MovePosition(anchorPos);
                return;
            }

            if (Climbing)
            {
                ClimbMove(p, dt);
                return;
            }

            Vector3 move = Flat(input.move);
            if (move.sqrMagnitude > 1f) move.Normalize();
            bool moving = move.sqrMagnitude > 0.0025f;
            wantsMove = moving;
            float speedN = Mathf.Clamp01(HorizontalSpeed / Mathf.Max(0.1f, p.moveSpeed));
            // Feet slide while travelling, lunging into a shove, or reeling from a hit; they grip when idle.
            bool sliding = moving || anchorVel.sqrMagnitude > 0.25f || shoveTimer > 0f || hitTimer > 0f
                           || HorizontalSpeed > 0.6f;
            float footFriction = sliding ? p.footFrictionMoving : p.footFrictionIdle;
            ownFootMaterial.dynamicFriction = footFriction;
            ownFootMaterial.staticFriction = footFriction * 1.15f;
            if (moving && !Grabbing)
            {
                float current = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                float target = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg;
                float wanted = Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-p.turnResponsiveness * dt));
                // A body at speed cannot pivot on the spot; cap how fast the facing may swing.
                float maxStep = Mathf.Lerp(1080f, p.turnRateTopSpeed, speedN) * dt;
                float delta = Mathf.Clamp(Mathf.DeltaAngle(current, wanted), -maxStep, maxStep);
                turnRate = Mathf.Lerp(turnRate, delta / Mathf.Max(dt, 1e-4f), 0.3f);
                float yaw = (current + delta) * Mathf.Deg2Rad;
                facing = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            }
            else turnRate = Mathf.Lerp(turnRate, 0f, 0.3f);

            Vector3 groundVel = Grounded && groundBody != null ? Flat(groundBody.GetPointVelocity(hp)) : Vector3.zero;
            Vector3 targetVel = move * p.moveSpeed + groundVel;
            float accel = (moving ? p.acceleration : p.stopDeceleration) * (Grounded ? 1f : p.airControl);
            // Push off in steps instead of gliding: thrust peaks as each foot takes weight.
            if (p.stanceThrust > 0f && Grounded && moving)
            {
                // Walk: two pushes per cycle. Hop: one push while the feet are down, nothing at the
                // apex. Both waves average 1.0 so the top speed does not change either way.
                float walkPush = 1.5708f * Mathf.Abs(Mathf.Sin(gait));
                float hopPush = 3f * (1f - hopArc);
                float push = Mathf.Lerp(walkPush, hopPush, p.boundGait);
                accel *= Mathf.Lerp(1f, Mathf.Max(0.3f, push), p.stanceThrust);
            }
            float speedNow = anchorVel.magnitude;
            if (speedNow > p.moveSpeed + 0.1f && Vector3.Dot(anchorVel, targetVel) > 0f)
            {
                // Momentum from a tumble or a slope: steer toward the input, shed the extra speed slowly.
                Vector3 dir = Vector3.RotateTowards(anchorVel / speedNow, targetVel.normalized, p.turnResponsiveness * 0.5f * dt, 0f);
                anchorVel = dir * Mathf.Max(targetVel.magnitude, speedNow - p.overspeedDecay * dt);
            }
            else anchorVel = Vector3.MoveTowards(anchorVel, targetVel, accel * dt);

            Vector3 next = anchorPos + anchorVel * dt;
            if (!moving && shoveTimer <= 0f)
            {
                // Idle pawns settle where they are; when shoved or tangled they stay where physics put
                // them instead of springing back to the old spot.
                float follow = hitTimer > 0f || contactTimer > 0f ? Mathf.Max(p.anchorIdleFollow, 15f) : p.anchorIdleFollow;
                next += Flat(hp - next) * (1f - Mathf.Exp(-follow * dt));
            }
            // In the air the anchor may only lead a little: the bell-shaped skirt meets walls at a slant,
            // so pressing into a wall would push the pawn down and kill the jump.
            float leash = Grounded ? p.anchorLeash : p.anchorLeash * p.airControl * 0.5f;
            if (shoveTimer > 0f)
            {
                // The shove is a short step into the target: the anchor leads the hips forward.
                next += facing * (p.shoveLunge / Mathf.Max(0.05f, p.shoveDuration) * dt);
                leash += p.shoveLunge;
            }

            Vector3 offset = Flat(next - hp);
            // Reversing direction leaves the anchor behind the still-moving body, and the anchor
            // spring then yanks the hips backwards at up to hipAnchorStrength * leash newtons - which
            // is how pressing the opposite key launched the pawn faster than it can run. Limit how far
            // the anchor may TRAIL, and the yank is limited with it. Leading is untouched.
            if (p.anchorBrakeLeash > 0.001f)
            {
                Vector3 travel = Flat(hips.linearVelocity);
                if (travel.sqrMagnitude > 0.04f && Vector3.Dot(offset, travel) < 0f)
                {
                    Vector3 back = travel.normalized;
                    float trailing = -Vector3.Dot(offset, back);
                    if (trailing > p.anchorBrakeLeash)
                    {
                        Vector3 fix = back * (trailing - p.anchorBrakeLeash);
                        next += fix;
                        offset += fix;
                    }
                }
            }
            float distance = offset.magnitude;
            if (distance > leash)
            {
                Vector3 dir = offset / distance;
                next.x = hp.x + dir.x * leash;
                next.z = hp.z + dir.z * leash;
                float bodyAlong = Vector3.Dot(Flat(hips.linearVelocity), dir);
                float anchorAlong = Vector3.Dot(anchorVel, dir);
                if (anchorAlong > bodyAlong) anchorVel -= dir * (anchorAlong - Mathf.Max(0f, bodyAlong));
            }
            // Running lifts the hips a little so the one-piece legs swing clear instead of scraping.
            // Airborne: ride along with the hips (one step ahead) so the vertical drive never brakes a jump.
            float lift = p.runLift * Mathf.Clamp01(anchorVel.magnitude / Mathf.Max(0.1f, p.moveSpeed));
            landDip = Mathf.MoveTowards(landDip, 0f, dt * 0.8f);
            if (Grounded && !wasGrounded && p.landingDip > 0f)
                landDip = Mathf.Max(landDip, p.landingDip * Mathf.Clamp01(-hips.linearVelocity.y / 6f));
            wasGrounded = Grounded;
            // The hips rise on every push-off and sink for a moment after a landing.
            // Two rises per cycle for a walk, one big rise per cycle for a hop.
            float bobWave = Mathf.Lerp(Mathf.Abs(Mathf.Sin(gait)), hopArc, p.boundGait);
            float bob = p.stepBob * bobWave * speedN;
            next.y = Grounded && groundFound
                ? groundY + standHeight + lift + bob - landDip - strideDrop
                : hp.y + hips.linearVelocity.y * dt;
            anchorPos = next;
            anchor.MovePosition(anchorPos);

            // The anchor's rotation is the hips' balance target (upright + facing + lean); the anchor
            // joint's angular drives pull the hips toward it (implicit, so stiff values stay stable).
            float lean = p.runLean * speedN;
            if (shoveTimer > 0f) lean += p.shoveLean;
            else if (input.grab && !Grabbing) lean += p.grabLean;
            // Sway with each step, and lean into a turn the way a runner has to.
            // Rolling toward the stance leg reads as weight when the legs alternate. With both legs
            // together there is no stance side, so the same roll reads as a limp - fade it out.
            float roll = p.stepRoll * Mathf.Sin(gait) * speedN * (1f - p.boundGait)
                       - Mathf.Clamp(turnRate / 180f, -1f, 1f) * p.turnLean * speedN;
            anchor.MoveRotation(Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(lean, 0f, roll));
            ClampOverspeed(p, dt);
        }

        void Jump(RagdollParams p)
        {
            if (!input.jump || State == PawnState.Ragdoll) return;
            bool fromGround = coyote > 0f && airTimer <= 0f;
            bool pullUp = !fromGround && !pullUpUsed && (handL.HoldingLedge || handR.HoldingLedge);
            if (!fromGround && !pullUp) return;
            float up = p.jumpImpulse * (pullUp ? p.pullUpFactor : 1f);
            Vector3 toward = Vector3.zero;
            if (pullUp)
            {
                // Vault: let go of the ledge and spring up; Mantle() carries the pawn over once its feet clear the top.
                PawnHand ledgeHand = handL.HoldingLedge ? handL : handR;
                vaultTop = ledgeHand.HeldCollider.bounds.max.y;
                toward = Flat(ledgeHand.Center - bodies[0].position);
                vaultDirection = toward.sqrMagnitude > 1e-4f ? toward.normalized : facing;
                toward = vaultDirection * 1.5f;
                vaultTimer = 0.8f;
                handL.Release(0.35f);
                handR.Release(0.35f);
            }
            for (int i = 0; i < Count; i++)
            {
                Vector3 v = bodies[i].linearVelocity + toward;
                v.y = Mathf.Max(v.y, 0f) + up;
                bodies[i].linearVelocity = v;
            }
            airTimer = 0.2f;
            coyote = 0f;
            Grounded = false;
            if (pullUp) pullUpUsed = true;
        }

        void Shove(RagdollParams p)
        {
            if (input.shove && State == PawnState.Active && shoveCooldown <= 0f)
            {
                shoveTimer = p.shoveDuration;
                shoveCooldown = p.shoveDuration + 0.25f;
                throwOnShoveEnd = Grabbing;
            }
            if (throwOnShoveEnd && shoveTimer <= 0f)
            {
                handL.Release(0.4f);
                handR.Release(0.4f);
                throwOnShoveEnd = false;
            }
        }

        /// <summary>After a ledge vault, once the feet clear the top, nudge the pawn over the edge.</summary>
        void Mantle(float dt)
        {
            if (vaultTimer <= 0f) return;
            vaultTimer -= dt;
            if (State == PawnState.Ragdoll)
            {
                vaultTimer = 0f;
                return;
            }
            float feet = Mathf.Min(footL.bounds.min.y, footR.bounds.min.y);
            if (feet < vaultTop + 0.02f) return;
            float along = Vector3.Dot(Flat(bodies[0].linearVelocity), vaultDirection);
            if (along < 2.5f) AddVelocity(vaultDirection * (2.5f - along));
            anchorVel = vaultDirection * Mathf.Max(2.5f, anchorVel.magnitude);
            vaultTimer = 0f;
        }

        public bool HoldingEnvironment() => IsEnvironmentGrip(handL) || IsEnvironmentGrip(handR);

        static bool IsEnvironmentGrip(PawnHand h) =>
            h.IsHolding && (h.HeldBody == null || h.HeldBody.isKinematic);

        // ---------------------------------------------------------------- 버둥대기

        /// <summary>
        /// Thrashing out of a grab. There is no escape meter and nothing releases the captor's hand:
        /// each tap throws a real impulse through the body the captor is actually holding, and the
        /// grip breaks when PhysX decides it has had enough (FixedJoint.breakForce, which for a hold
        /// on another pawn is pawnGrabBreakForce). So escape is the physics giving way.
        ///
        /// Three things make a tap hit harder, all multiplying: tapping fast, steering AGAINST the
        /// direction the captor is hauling you, and jumping at the same moment. Each tap costs
        /// stamina, so mashing forever is not free.
        /// </summary>
        void Struggle(RagdollParams p, float dt)
        {
            heldTimer -= dt;
            struggleTimer -= dt;
            struggleRush = Mathf.Max(0f, struggleRush - dt);
            lastStrugglePunch = Mathf.Max(0f, lastStrugglePunch - dt * 0.9f);
            if (!BeingHeld)
            {
                holder = null;
                heldCollider = null;
                return;
            }
            if (State == PawnState.Ragdoll || !input.shove) return;
            input.shove = false;            // this tap is a struggle, not a shove
            if (stamina < 0f) stamina = p.climbStaminaMax;
            if (stamina <= 0f) return;      // too tired to fight
            stamina = Mathf.Max(0f, stamina - p.struggleStamina);
            struggleTimer = p.struggleBurst;
            struggleFlip = -struggleFlip;

            // Mash bonus: taps landing inside the burst window stack up to struggleRushBonus.
            struggleRush = Mathf.Min(1f, struggleRush + 0.42f);
            float rush = Mathf.Lerp(1f, p.struggleRushBonus, struggleRush);

            // Pulling against the haul. The captor's own travel is the direction to fight.
            Vector3 haul = holder != null ? Flat(holder.Hips.linearVelocity) : Vector3.zero;
            Vector3 want = Flat(input.move);
            float away = 1f;
            if (haul.sqrMagnitude > 0.25f && want.sqrMagnitude > 0.01f)
                away = Mathf.Lerp(1f, p.struggleAwayBonus,
                    Mathf.Clamp01(-Vector3.Dot(want.normalized, haul.normalized)));
            float leap = input.jump ? p.struggleJumpBonus : 1f;

            float punch = p.struggleShake * rush * away * leap;
            Vector3 side = Vector3.Cross(Vector3.up, facing) * (punch * struggleFlip);
            Vector3 lift = Vector3.up * (punch * (input.jump ? 0.7f : 0.25f));

            // Drive it through the part being held, so the grip takes the load rather than the skirt.
            Rigidbody held = heldCollider != null && ColliderOwner.ContainsKey(heldCollider)
                ? BodyOf(heldCollider) : bodies[(int)BodyId.Chest];
            held.AddForce(side + lift, ForceMode.Impulse);
            bodies[(int)BodyId.ArmL].AddForce(-side * 0.5f, ForceMode.Impulse);
            bodies[(int)BodyId.ArmR].AddForce(side * 0.5f, ForceMode.Impulse);
            if (holder != null) holder.bodies[(int)BodyId.Chest].AddForce(-side * 0.35f, ForceMode.Impulse);

            lastStrugglePunch = Mathf.Clamp01(punch * 60f / Mathf.Max(1f, p.pawnGrabBreakForce));
        }

        Rigidbody BodyOf(Collider c)
        {
            for (int i = 0; i < Count; i++)
                foreach (var own in bodies[i].GetComponentsInChildren<Collider>(true))
                    if (own == c) return bodies[i];
            return bodies[(int)BodyId.Chest];
        }

        // ---------------------------------------------------------------- 등반

        /// <summary>
        /// Looks for a climbable face right in front of the chest. Hand FixedJoints are deliberately
        /// NOT used to hold the body up: a 0.25 m arm fixed to the wall at shoulder height cannot
        /// lift the pawn past its own joint limits, so the body just hangs there. The controller
        /// lifts the body and the hands are posed to match, which is what the wall probe is for.
        /// </summary>
        bool ProbeWall(RagdollParams p)
        {
            Vector3 chest = bodies[(int)BodyId.Chest].position;
            float reach = p.grabRadius + standHeight * 1.1f;
            Vector3 side = Vector3.Cross(Vector3.up, facing);
            float best = float.MaxValue;
            bool found = false;
            // Straight ahead finds a plain wall; angled up finds an overhang leaning over the pawn or
            // the underside of a rock; angled down keeps contact as the body tops out over a lip.
            // The side rays keep a bulge from slipping out from between the hands.
            for (int i = 0; i < 5; i++)
            {
                Vector3 dir = i == 0 ? facing
                    : i == 1 ? (facing + Vector3.up * 0.7f).normalized
                    : i == 2 ? (facing - Vector3.up * 0.4f).normalized
                    : (facing + side * (i == 3 ? 0.45f : -0.45f)).normalized;
                Vector3 origin = chest + Vector3.up * (i == 1 ? 0.12f : 0f);
                int n = Physics.RaycastNonAlloc(new Ray(origin, dir), hits, reach, ~0, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < n; j++)
                {
                    var h = hits[j];
                    if (ownSet.Contains(h.collider)) continue;
                    if (ColliderOwner.ContainsKey(h.collider)) continue;
                    var rb = h.collider.attachedRigidbody;
                    if (rb != null && !rb.isKinematic) continue;
                    if (Mathf.Abs(h.normal.y) > Mathf.Cos(p.climbGripAngle * Mathf.Deg2Rad)) continue;
                    if (h.distance >= best) continue;
                    best = h.distance;
                    wallPoint = h.point;
                    wallNormal = h.normal;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Stamina climbing in the style of PEAK: the hands hold the wall, the body is pulled up
        /// between them, and stamina drains the whole time - faster while actually moving. When it
        /// runs out the hands open on their own and the pawn falls, so a wall is a route with a
        /// length, not a yes/no check.
        /// </summary>
        void UpdateClimb(RagdollParams p, float dt)
        {
            Vector3 move = Flat(input.move);
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            climbUp = Vector3.Dot(move, facing);
            climbSide = Vector3.Dot(move, right);

            if (stamina < 0f) stamina = p.climbStaminaMax;
            climbCooldown -= dt;
            topOutTimer -= dt;
            bool gripping = ProbeWall(p);
            // The wall running out IS the ledge: the big wall is built as set-back blocks, so the
            // top of each block is somewhere to stand. Throw the body forward so it flops onto it.
            // Only once the probe has been empty for the WHOLE grace window: a single frame with no
            // hit happens constantly at a seam or when an arm swings past, and treating that as "the
            // wall ended" cancelled the climb every second.
            if (Climbing && climbGrace <= 0f && climbUp > 0.1f && topOutTimer <= -0.2f)
                topOutTimer = p.climbTopOut;
            // A step over a seam between two blocks must not drop the pawn off the wall.
            climbGrace = gripping ? 0.55f : climbGrace - dt;
            bool wants = input.grab && State == PawnState.Active && climbGrace > 0f
                         && stamina > 0f && climbCooldown <= 0f;
            Climbing = wants && (!Grounded || climbUp > 0.1f);

            if (topOutTimer > 0f && State == PawnState.Active)
            {
                // A short scramble over the lip. Deliberately clumsy - it should look like a flop.
                anchorPos += (facing * 1.6f + Vector3.up * 2.2f) * dt;
                anchorVel = Flat(facing) * 1.4f;
                anchor.MovePosition(anchorPos);
                climbCooldown = Mathf.Max(climbCooldown, 0.35f);   // let it land before grabbing again
            }
            if (!Climbing)
            {
                holdsPlaced = false;
                if (Grounded && State == PawnState.Active)
                    stamina = Mathf.Min(p.climbStaminaMax, stamina + p.climbRecover * dt);
                return;
            }

            stamina -= (p.climbDrainHold + p.climbDrainMove * Mathf.Abs(climbUp)) * dt;

            // The wall frame the hands work in. Both palm targets are measured from the CHEST, so
            // they cannot trail below the body as it rises - an earlier version pinned them to world
            // points and within a second the arms were hanging at the pawn's sides.
            Vector3 across = Vector3.Cross(wallNormal, Vector3.up);
            if (across.sqrMagnitude < 1e-4f) across = Vector3.Cross(wallNormal, facing);
            across.Normalize();
            climbUpAxis = Vector3.Cross(across, wallNormal).normalized;
            climbAcross = across;
            climbFace = wallPoint + wallNormal * 0.05f;
            // Hand over hand, the same way the feet do it on the ground: a palm holds a real point
            // on the wall, the body climbs toward it, and only once the body has caught up does that
            // hand let go and reach higher. The body may never rise above what the top hand allows,
            // which is the whole trick - drive the body on its own clock instead and the hands trail
            // below it within a second, which is how the arms ended up hanging at the pawn's sides.
            float shoulderY = bodies[(int)BodyId.ArmL].position.y;
            float shoulderRise = shoulderY - bodies[0].position.y;
            if (!holdsPlaced)
            {
                handHold[0] = Plant(0, p, shoulderY + p.climbHandStep);
                handHold[1] = Plant(1, p, shoulderY + p.climbHandStep * 0.25f);
                holdsPlaced = true;
                movingHand = 0;
                handStep = 1f;
            }
            handStep = Mathf.Min(1f, handStep + p.climbCadence * dt);

            // Whichever palm is higher is the one carrying the pawn, and it does not move at all:
            // the body climbs past it until it is climbPullDepth below the shoulder, and only then
            // does that hand let go and reach again. Everything the arm does follows from that, so
            // there is no animation loop to fall out of step with the climb.
            // The HIGHER palm carries the pawn; the LOWER one is the one that gets to reach. Picking
            // the higher one to swing meant it replanted even higher every time and the same arm moved
            // forever, which is exactly what it looked like.
            int hold = handHold[0].y >= handHold[1].y ? 0 : 1;
            int swing = 1 - hold;
            climbCeiling = handHold[hold].y + p.climbPullDepth - shoulderRise;
            // One hand in the air means less to hang from: drop a little, then snap back up.
            if (handStep < 1f) climbCeiling -= p.climbSag * Mathf.Sin(handStep * Mathf.PI);

            // Test the COMMANDED height, not the measured one. The body hangs ~5 cm under the
            // anchor because the spring balances its weight there, and comparing the real
            // shoulder against the hold left the swap permanently 5 cm out of reach.
            bool reachedTop = anchorPos.y >= climbCeiling - 0.02f;
            if (handStep >= 1f && climbUp > 0.05f && reachedTop)
            {
                swingFrom = handHold[swing];
                movingHand = swing;
                handHold[swing] = Plant(swing, p, handHold[hold].y + p.climbHandStep);
                handStep = 0f;
            }
            else if (handStep >= 1f && climbUp < -0.05f)
            {
                swingFrom = handHold[hold];
                movingHand = hold;
                handHold[hold] = Plant(hold, p, handHold[swing].y - p.climbHandStep);
                handStep = 0f;
            }
            if (stamina > 0f) return;
            stamina = 0f;
            Climbing = false;
            climbCooldown = 1.2f;
            handL.Release(1f);
            handR.Release(1f);
        }

        /// <summary>
        /// While on the wall the pawn stops being a ragdoll and is placed, bone by bone, from the
        /// puppet pose. A spring-driven arm cannot hold a palm on a fixed point - measured, the hand
        /// rides at a constant offset above the hips the whole way up - and pinning just the palms
        /// with a kinematic body turns the joint chain into a catapult. Taking the whole body out of
        /// physics for the duration is the only version that both holds the hands still and stays put.
        /// Physics resumes the instant the climb ends, carrying the body's velocity so nothing pops.
        /// </summary>
        void ApplyClimbPose(RagdollParams p)
        {
            if (!climbKinematic)
            {
                foreach (var rb in bodies) rb.isKinematic = true;
                climbKinematic = true;
            }
            Quaternion hipsRot = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(9f, 0f, 0f);
            poseRot[0] = hipsRot;
            poseScratch[0] = anchorPos;
            for (int i = 1; i < Count; i++)
            {
                int parent = ParentOf[i];
                poseRot[i] = poseRot[parent] * puppet[i].localRotation;
                poseScratch[i] = poseScratch[parent] + poseRot[parent] * jointOffset[i];
            }
            // Put the palms exactly on their holds. Everything else hangs off the chain.
            for (int slot = 0; slot < 2; slot++)
            {
                int id = slot == 0 ? (int)BodyId.HandL : (int)BodyId.HandR;
                Vector3 at = handHold[slot];
                if (movingHand == slot && handStep < 1f)
                {
                    float t = Smooth(handStep);
                    at = Vector3.Lerp(swingFrom, handHold[slot], t)
                         + wallNormal * (Mathf.Sin(handStep * Mathf.PI) * 0.1f)
                         + climbUpAxis * (Mathf.Sin(handStep * Mathf.PI) * p.climbOvershoot * p.climbHandStep);
                }
                poseScratch[id] = at;
            }
            for (int i = 0; i < Count; i++)
            {
                bodies[i].MovePosition(poseScratch[i]);
                bodies[i].MoveRotation(poseRot[i]);
            }
        }

        /// <summary>Back to being a ragdoll, carrying whatever speed the climb had.</summary>
        public void EndClimbPose()
        {
            if (!climbKinematic) return;
            climbKinematic = false;
            // Kinematic bodies are pushed through geometry rather than stopped by it, so the pawn can
            // be inside the wall when physics resumes - and then it falls straight through the level.
            // Step it back out along the face first.
            float clear = P.grabRadius + 0.2f;
            float gap = Vector3.Dot(bodies[0].position - wallPoint, wallNormal);
            if (gap < clear)
            {
                Vector3 push = wallNormal * (clear - gap);
                foreach (var rb in bodies) rb.position += push;
                anchorPos += push;
                anchor.position = anchorPos;
            }
            Vector3 carry = Vector3.ClampMagnitude(anchorVel + Vector3.up * 0.5f, 4f);
            foreach (var rb in bodies)
            {
                rb.isKinematic = false;
                rb.linearVelocity = carry;
                rb.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>Hangs the body below the hands and walks it up the wall.</summary>
        void ClimbMove(RagdollParams p, float dt)
        {
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            Vector3 next = anchorPos;
            next.y += p.climbSpeed * Mathf.Clamp(climbUp, -1f, 1f) * dt;
            // Never higher than the hand that is holding on, so the climb rate is set by how fast the
            // hands can be placed rather than by a number. That is what makes it read as climbing.
            if (holdsPlaced && climbUp > 0f) next.y = Mathf.Min(next.y, climbCeiling);
            next += right * (p.climbSpeed * 0.55f * Mathf.Clamp(climbSide, -1f, 1f) * dt);
            // Hold the body a body-depth off the face, at the height the probe says the wall is.
            // The skirt is 0.273 m deep: hug any closer than this and the body is jammed into the
            // wall, pushed back out by the contact, and the climb stalls.
            Vector3 hug = wallPoint + wallNormal * (p.grabRadius + 0.1f);
            next.x = Mathf.Lerp(next.x, hug.x, 1f - Mathf.Exp(-8f * dt));
            next.z = Mathf.Lerp(next.z, hug.z, 1f - Mathf.Exp(-8f * dt));
            // Face the wall while climbing, so "forward" always means up the wall.
            Vector3 into = Flat(-wallNormal);
            if (into.sqrMagnitude > 1e-4f)
                facing = Vector3.RotateTowards(facing, into.normalized, 6f * dt, 0f);
            anchorVel = Vector3.zero;
            anchorPos = next;
            anchor.MovePosition(anchorPos);
            // Belly to the wall. A negative pitch leans the pawn AWAY from it, which is what made
            // it look like a back-slash propped against the face.
            anchor.MoveRotation(Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(9f, 0f, 0f));
        }

        // ---------------------------------------------------------------- puppet

        void Pose(RagdollParams p, float dt)
        {
            // Step at the speed the pawn is trying to go, so a blocked pawn runs in place instead of skidding.
            float speed = State == PawnState.Ragdoll ? 0f : Mathf.Max(HorizontalSpeed, anchorVel.magnitude);
            float speedN = Mathf.Clamp01(speed / Mathf.Max(0.1f, p.moveSpeed));
            bool air = State == PawnState.Active && !Grounded;
            // Keep a minimum cadence while the player is holding a direction, so the first push-off
            // of a standing start happens on a step instead of waiting for speed to build.
            float cadence = Mathf.Max(speed, wantsMove && State == PawnState.Active && Grounded ? 1.2f : 0f);
            // Distance-based tempo makes the legs spin faster and faster as top speed rises. Locking the
            // hop rate instead means a faster pawn covers more ground per hop at the SAME rhythm, which
            // is the only way the gait survives a big moveSpeed.
            float cycles = cadence / Mathf.Max(0.2f, p.strideLength);
            if (p.hopCadence > 0.01f)
                cycles = p.hopCadence * Mathf.Clamp01(speedN * 2.5f);
            gait += cycles * dt * Mathf.PI * 2f;
            if (gait > Mathf.PI * 2f) gait -= Mathf.PI * 2f;
            float s = Mathf.Sin(gait);
            // A real hop is a thrown body: height follows a parabola, flat at the ends, peak in the
            // middle. phase 0 = feet down, 0.5 = apex.
            float phase = gait / (Mathf.PI * 2f);
            hopArc = 4f * phase * (1f - phase);
            float legAmp = air ? 0f : p.legSwing * Mathf.Clamp01(speedN * 1.5f);
            float armAmp = air ? 0f : p.armSwing * speedN;

            Quaternion chest = Quaternion.Euler(p.chestLean * speedN, 0f, 0f);
            Quaternion head = Quaternion.Euler(-0.5f * p.chestLean * speedN, 0f, 0f);
            // boundGait 0 = legs alternate (a walk), 1 = legs move together (a hop). A hop has a
            // flight phase, so "the foot cannot keep up with the ground" simply stops applying - which
            // is the only way a body with 0.18 m legs can honestly move at several metres per second.
            float ampR = Mathf.Lerp(legAmp, -legAmp, p.boundGait);
            Quaternion thighL = Quaternion.Euler(-legAmp * s, 0f, 0f);
            Quaternion thighR = Quaternion.Euler(ampR * s, 0f, 0f);
            Quaternion footL = Quaternion.Euler(0.8f * legAmp * s, 0f, 0f);
            Quaternion footR = Quaternion.Euler(-0.8f * ampR * s, 0f, 0f);
            Quaternion armL = Quaternion.Euler(0f, -armAmp * s, p.armRestDown);
            Quaternion armR = Quaternion.Euler(0f, -armAmp * s, -p.armRestDown);

            if (air)
            {
                thighL = Quaternion.Euler(-25f, 0f, 0f);
                thighR = Quaternion.Euler(-15f, 0f, 0f);
                footL = footR = Quaternion.Euler(20f, 0f, 0f);
                armL = Quaternion.Euler(0f, 0f, -35f);
                armR = Quaternion.Euler(0f, 0f, 35f);
            }

            // Measured: with legs this short a foot can only stay planted below about 1 m/s
            // (41% of the time at 0.65 m/s, 1% at 1.35 m/s). Past that it only fights the leg
            // swing, so the lock fades out and the procedural stride takes over.
            float lockAmount = p.stepLock * (1f - Mathf.Clamp01(speed / 1.2f));
            if (lockAmount > 0.001f && !air && State == PawnState.Active && Grounded)
            {
                Vector3 footTargetL = StepTarget(p, dt, 0, speed, speedN);
                Vector3 footTargetR = StepTarget(p, dt, 1, speed, speedN);
                LegIk(footTargetL, 0, out Quaternion ikThighL, out Quaternion ikFootL);
                LegIk(footTargetR, 1, out Quaternion ikThighR, out Quaternion ikFootR);
                thighL = Quaternion.Slerp(thighL, ikThighL, lockAmount);
                footL = Quaternion.Slerp(footL, ikFootL, lockAmount);
                thighR = Quaternion.Slerp(thighR, ikThighR, lockAmount);
                footR = Quaternion.Slerp(footR, ikFootR, lockAmount);
                strideDrop = Mathf.Lerp(strideDrop, PelvisDrop(footTargetL, footTargetR) * lockAmount, 0.25f);
            }
            else
            {
                legs[0].ready = legs[1].ready = false;
                swingingLeg = -1;
                strideDrop = Mathf.Lerp(strideDrop, 0f, 0.25f);
            }

            if (Climbing)
            {
                // Both arms overhead on the wall, legs tucked and dangling.
                // Each arm points at its own palm spot on the wall, so a planted hand stays planted
                // while the body creeps past it. Only the hand that is moving swings, and it arcs out
                // and slaps down rather than sliding - that reads as "stuck on" instead of "waving".
                float panic = 1f + 1.6f * (1f - Mathf.Clamp01(Stamina / 0.35f));
                armL = ClimbReach(true, 0, p, panic);
                armR = ClimbReach(false, 1, p, panic);
                // The legs push off the wall in time with the hands instead of paddling at nothing:
                // the leg under the reaching arm straightens, the other tucks.
                // Reaching lifts that shoulder and tips the head the other way, eased across the swap
                // rather than snapped - this is what makes it read as a person climbing.
                float toRight = movingHand == 1 ? 1f : -1f;
                float ease = Mathf.Lerp(-toRight, toRight, Smooth(handStep));
                // The leg opposite the reaching arm pushes, the way a person does it.
                float step = 0.5f + 0.5f * ease;
                thighL = Quaternion.Euler(-10f - 20f * step, 0f, 0f);
                thighR = Quaternion.Euler(-10f - 20f * (1f - step), 0f, 0f);
                footL = footR = Quaternion.Euler(20f, 0f, 0f);
                chest = Quaternion.Euler(-6f, 4f * ease, p.climbShoulderLift * ease);
                head = Quaternion.Euler(-12f, -6f * ease, -p.climbHeadTilt * ease);
            }
            else if (struggleTimer > 0f)
            {
                // Thrashing: both arms flap fast and out of phase. Deliberately silly.
                float fade = Mathf.Clamp01(struggleTimer / Mathf.Max(0.01f, p.struggleBurst));
                float a = Mathf.Sin(Time.time * 38f) * p.struggleSwing * fade;
                float b = Mathf.Cos(Time.time * 31f) * p.struggleSwing * fade;
                armL = Quaternion.Euler(0f, -a, p.armRestDown - 60f * fade);
                armR = Quaternion.Euler(0f, b, -p.armRestDown + 60f * fade);
                chest = Quaternion.Euler(-6f * fade, 16f * fade * struggleFlip, 0f);
                head = Quaternion.Euler(0f, -10f * fade * struggleFlip, 0f);
                legAmp = Mathf.Max(legAmp, 30f * fade);
                thighL = Quaternion.Euler(-legAmp * Mathf.Sin(Time.time * 26f), 0f, 0f);
                thighR = Quaternion.Euler(legAmp * Mathf.Sin(Time.time * 26f), 0f, 0f);
            }

            if (State != PawnState.Ragdoll && !Climbing && struggleTimer <= 0f)
            {
                if (input.grab)
                {
                    armL = ReachPose(handL, true, air);
                    armR = ReachPose(handR, false, air);
                    chest *= Quaternion.Euler(p.grabLean * 0.6f, 0f, 0f);
                }
                if (shoveTimer > 0f)
                {
                    armL = Quaternion.Euler(0f, 90f, 0f);
                    armR = Quaternion.Euler(0f, -90f, 0f);
                    chest = Quaternion.Euler(p.chestLean + p.shoveLean * 0.6f, 0f, 0f);
                }
            }

            SetPuppet(BodyId.Chest, chest);
            SetPuppet(BodyId.Head, head);
            SetPuppet(BodyId.ThighL, thighL);
            SetPuppet(BodyId.ThighR, thighR);
            SetPuppet(BodyId.FootL, footL);
            SetPuppet(BodyId.FootR, footR);
            SetPuppet(BodyId.ArmL, armL);
            SetPuppet(BodyId.ArmR, armR);
            SetPuppet(BodyId.HandL, Quaternion.identity);
            SetPuppet(BodyId.HandR, Quaternion.identity);
        }

        /// <summary>
        /// Where one foot should be this step. A planted foot keeps its world spot while the body
        /// passes over it; once it has fallen too far behind, that foot swings to a new spot in front.
        /// Only one foot swings at a time, so the other one is always carrying the pawn.
        /// </summary>
        Vector3 StepTarget(RagdollParams p, float dt, int side, float speed, float speedN)
        {
            Leg leg = legs[side];
            Vector3 hp = bodies[0].position;
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            int footId = side == 0 ? (int)BodyId.FootL : (int)BodyId.FootR;
            int thighId = side == 0 ? (int)BodyId.ThighL : (int)BodyId.ThighR;
            float lateral = bindPos[footId].x;
            float ankle = standHeight + bindPos[(int)BodyId.FootL].y;
            float ground = groundFound ? groundY : hp.y - standHeight;
            Vector3 under = new Vector3(hp.x, ground + ankle, hp.z) + right * lateral;
            // These legs are one stiff piece, so a step can never be longer than the foot can reach
            // (leg length x sin 55 degrees, front and back). Asking for more just drags the foot.
            float reach = (bindPos[footId] - bindPos[thighId]).magnitude * 1.6f;
            float stride = Mathf.Min(p.stepLength, reach);
            float swingTime = Mathf.Clamp(stride / Mathf.Max(0.6f, speed) * 0.5f, 0.04f, 0.3f);

            if (!leg.ready)
            {
                leg.plant = under;
                leg.ready = true;
                leg.swinging = false;
            }

            if (leg.swinging)
            {
                leg.swingT += dt / swingTime;
                if (leg.swingT >= 1f)
                {
                    leg.swingT = 1f;
                    leg.swinging = false;
                    leg.plant = leg.target;
                    if (swingingLeg == side) swingingLeg = -1;
                    return leg.plant;
                }
                float t = leg.swingT * leg.swingT * (3f - 2f * leg.swingT);
                Vector3 swing = Vector3.Lerp(leg.swingFrom, leg.target, t);
                swing.y += Mathf.Sin(leg.swingT * Mathf.PI) * stride * (0.25f + 0.25f * speedN);
                return swing;
            }

            leg.plant.y = ground + ankle;
            if (Flat(leg.plant - under).magnitude > stride * 0.5f && swingingLeg < 0)
            {
                swingingLeg = side;
                leg.swinging = true;
                leg.swingT = 0f;
                leg.swingFrom = leg.plant;
                Vector3 ahead = Flat(anchorVel).sqrMagnitude > 0.04f ? Flat(anchorVel).normalized : facing;
                // Aim at where the hips will be when the foot lands, half a stride in front of them.
                leg.target = under + Flat(anchorVel) * swingTime + ahead * (stride * 0.5f);
                leg.target.y = ground + ankle;
            }
            return leg.plant;
        }

        /// <summary>
        /// A stiff leg is a fixed length: the further a planted foot is from straight below the hip,
        /// the lower the hips must sit for that leg to still reach it. Without this the pelvis is
        /// pinned at ride height, the legs are wedged between it and the floor, and they cannot
        /// swing at all - which is exactly what makes a pawn look like it is gliding.
        /// </summary>
        float PelvisDrop(Vector3 footL, Vector3 footR)
        {
            float drop = 0f;
            for (int side = 0; side < 2; side++)
            {
                if (legs[side].swinging) continue;
                int thighId = side == 0 ? (int)BodyId.ThighL : (int)BodyId.ThighR;
                int footId = side == 0 ? (int)BodyId.FootL : (int)BodyId.FootR;
                float legLength = (bindPos[footId] - bindPos[thighId]).magnitude;
                Vector3 hip = bodies[0].position + bodies[0].rotation * bindPos[thighId];
                Vector3 foot = side == 0 ? footL : footR;
                float reach = Flat(foot - hip).magnitude;
                float vertical = Mathf.Sqrt(Mathf.Max(0.0001f, legLength * legLength - reach * reach));
                drop = Mathf.Max(drop, legLength - vertical);
            }
            return Mathf.Min(drop, 0.08f);
        }

        /// <summary>
        /// Aims a one-piece leg at a world point. There is no knee, so only the direction can be
        /// matched; the ankle counter-rotates to keep the sole flat.
        /// </summary>
        void LegIk(Vector3 foot, int side, out Quaternion thigh, out Quaternion ankle)
        {
            int thighId = side == 0 ? (int)BodyId.ThighL : (int)BodyId.ThighR;
            int footId = side == 0 ? (int)BodyId.FootL : (int)BodyId.FootR;
            Quaternion hipsRot = bodies[0].rotation;
            Vector3 bindDir = bindPos[footId] - bindPos[thighId];
            Vector3 hip = bodies[0].position + hipsRot * bindPos[thighId];
            Vector3 want = Quaternion.Inverse(hipsRot) * (foot - hip);
            if (want.sqrMagnitude < 1e-6f || bindDir.sqrMagnitude < 1e-6f)
            {
                thigh = ankle = Quaternion.identity;
                return;
            }
            thigh = Quaternion.FromToRotation(bindDir.normalized, want.normalized);
            float angle = Quaternion.Angle(Quaternion.identity, thigh);
            if (angle > 55f) thigh = Quaternion.Slerp(Quaternion.identity, thigh, 55f / angle);
            ankle = Quaternion.Inverse(thigh);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>
        /// Where one palm plants: out to its own side, up at <paramref name="height"/>, and as close
        /// to the face as the arm can actually get. Pinning it exactly ON the face does not work - the
        /// arm reaches 0.20 m and the skirt holds the body 0.27 m off, so only a band at shoulder
        /// height would ever be touchable and the arm could never swing.
        /// </summary>
        Vector3 Plant(int slot, RagdollParams p, float height)
        {
            Vector3 shoulder = bodies[slot == 0 ? (int)BodyId.ArmL : (int)BodyId.ArmR].position;
            Vector3 at = climbFace + climbAcross * (slot == 0 ? -p.climbHandSpread : p.climbHandSpread);
            at += climbUpAxis * (height - at.y);
            // Pull it back toward the shoulder if the arm cannot span the gap to the face.
            Vector3 off = at - shoulder;
            float len = off.magnitude;
            return len > 0.30f ? shoulder + off * (0.30f / len) : at;
        }

        /// <summary>
        /// Aims one arm at the palm spot it is supposed to be holding. The hand that is mid-swap
        /// travels along a small arc so it lifts off, reaches, and plants, instead of gliding.
        /// </summary>
        Quaternion ClimbReach(bool left, int slot, RagdollParams p, float panic)
        {
            Transform chestT = bodies[(int)BodyId.Chest].transform;
            Vector3 shoulder = bodies[left ? (int)BodyId.ArmL : (int)BodyId.ArmR].position;
            // Point the arm at the palm's hold and nothing else. A held hand is fixed in the world,
            // so as the body climbs past it the arm sweeps from overhead down past the shoulder on
            // its own - that sweep is the animation, and it cannot fall out of step with the climb
            // because the climb is what produces it.
            Vector3 target = handHold[slot];
            if (movingHand == slot && handStep < 1f)
            {
                // The swinging hand: peel off, arc up, plant on the new hold.
                float t = Smooth(handStep);
                target = Vector3.Lerp(swingFrom, handHold[slot], t);
                target += wallNormal * (Mathf.Sin(handStep * Mathf.PI) * 0.09f);
                // Fling it past the hold and let it drop back on - little arms flailing for the hold
                // is the joke, and it also sells the effort.
                target += climbUpAxis * (Mathf.Sin(handStep * Mathf.PI) * p.climbOvershoot * p.climbHandStep);
                target += climbAcross * (Mathf.Sin(handStep * Mathf.PI * 2f) * 0.05f * (slot == 0 ? -1f : 1f));
            }
            Vector3 aim = target - shoulder;
            if (aim.sqrMagnitude < 1e-6f) aim = -wallNormal;
            Vector3 local = chestT.InverseTransformDirection(aim.normalized);
            return Quaternion.FromToRotation(left ? Vector3.left : Vector3.right, local);
        }

        /// <summary>Points one arm up the wall: <paramref name="raise"/> 0 = straight out, 90 = overhead.</summary>
        Quaternion ClimbArm(bool left, float raise)
        {
            Transform chest = bodies[(int)BodyId.Chest].transform;
            float t = Mathf.Clamp01(raise / 90f);
            Vector3 aim = Vector3.Slerp((left ? -chest.right : chest.right), Vector3.up, t) + chest.forward * 0.55f;
            Vector3 local = chest.InverseTransformDirection(aim.normalized);
            return Quaternion.FromToRotation(left ? Vector3.left : Vector3.right, local);
        }

        Quaternion ReachPose(PawnHand hand, bool left, bool air)
        {
            Transform chest = bodies[(int)BodyId.Chest].transform;
            Vector3 shoulder = bodies[left ? (int)BodyId.ArmL : (int)BodyId.ArmR].position;
            Vector3 aim = hand.HasReach && !hand.IsHolding
                ? hand.ReachPoint - shoulder
                : Quaternion.AngleAxis(air ? -45f : -15f, chest.right) * chest.forward;
            Vector3 local = chest.InverseTransformDirection(aim.normalized);
            return Quaternion.FromToRotation(left ? Vector3.left : Vector3.right, local);
        }

        void SetPuppet(BodyId id, Quaternion localRotation) => puppet[(int)id].localRotation = localRotation;

        // ---------------------------------------------------------------- drives

        /// <summary>
        /// Swinging a leg costs inertia, and the foot dominates it: 2.5 kg at 0.19 m from the hip is
        /// three times the thigh's 3 kg at 0.10 m. Lighter feet therefore buy cadence far more cheaply
        /// than any drive setting. Whatever the legs give up goes into the hips, so the pawn still
        /// weighs the same and every impulse-based threshold keeps its meaning.
        /// </summary>
        void ApplyMasses(RagdollParams p)
        {
            float thigh = p.thighMass > 0.01f ? p.thighMass : baseMass[(int)BodyId.ThighL];
            float foot = p.footMass > 0.01f ? p.footMass : baseMass[(int)BodyId.FootL];
            if (Mathf.Approximately(bodies[(int)BodyId.ThighL].mass, thigh)
                && Mathf.Approximately(bodies[(int)BodyId.FootL].mass, foot)) return;
            bodies[(int)BodyId.ThighL].mass = bodies[(int)BodyId.ThighR].mass = thigh;
            bodies[(int)BodyId.FootL].mass = bodies[(int)BodyId.FootR].mass = foot;
            float moved = 2f * (baseMass[(int)BodyId.ThighL] - thigh) + 2f * (baseMass[(int)BodyId.FootL] - foot);
            bodies[0].mass = Mathf.Max(1f, baseMass[0] + moved);
        }

        void Drives(RagdollParams p, float k)
        {
            ApplyMasses(p);
            float r = p.damperRatio;
            // Dynamic softening hits the upper body and arms fully, legs/anchor only by lowerBodyDynamicShare.
            float kLower = StateFactor * Mathf.Lerp(1f, Stiffness, p.lowerBodyDynamicShare);
            float anchorSpring = p.hipAnchorStrength * kLower * (Climbing ? p.climbPull : 1f);
            float anchorRatio = Climbing ? p.climbDamperRatio : r;
            var linear = new JointDrive { positionSpring = anchorSpring, positionDamper = anchorSpring * anchorRatio, maximumForce = float.MaxValue };
            anchorJoint.xDrive = linear;
            anchorJoint.yDrive = linear;
            anchorJoint.zDrive = linear;
            // Anchor joint axis is world-up: X drive = yaw toward facing, YZ drive = keep the hips upright.
            anchorJoint.angularXDrive = new JointDrive { positionSpring = p.yawStrength * kLower, positionDamper = p.balanceDamper * 0.5f * kLower, maximumForce = float.MaxValue };
            anchorJoint.angularYZDrive = new JointDrive { positionSpring = p.balanceStrength * kLower, positionDamper = p.balanceDamper * kLower, maximumForce = float.MaxValue };

            float lower = p.lowerBodySpring * kLower;
            float upper = p.upperBodySpring * k;
            float arm = p.armSpring * k * (shoveTimer > 0f ? p.shoveArmMultiplier : 1f)
                        * (struggleTimer > 0f ? p.struggleArmMultiplier : 1f);
            float armL = arm * ArmBoost(handL, p);
            float armR = arm * ArmBoost(handR, p);
            Slerp(BodyId.Chest, upper, r);
            Slerp(BodyId.Head, upper, r);
            // A drive tracks up to 1/damperRatio rad/s and no further, because Slerp() ties the damper
            // to the spring. At damperRatio 0.1 that is 1.59 Hz, so a fast stepping gait is filtered
            // away. The legs get their own, lower ratio; the anchor and torso keep the stable one.
            //
            // But one drive is doing two jobs: swinging the leg AND holding the body up on it. Loose
            // enough to swing fast is too loose to stand on, which is why a low ratio alone just makes
            // the pawn collapse. So the ratio follows the gait: loose on the leg that is in the air,
            // firm on the leg that is carrying weight. The lower foot is the one taking the load.
            float legR = p.legDamperRatio > 0.0001f ? p.legDamperRatio : r;
            float rL = legR, rR = legR;
            if (legR < r)
            {
                float lift = (bodies[(int)BodyId.FootR].position.y - bodies[(int)BodyId.FootL].position.y) / 0.04f;
                float stanceL = Mathf.Clamp01(0.5f + 0.5f * Mathf.Clamp(lift, -1f, 1f));
                rL = Mathf.Lerp(legR, r, stanceL);
                rR = Mathf.Lerp(legR, r, 1f - stanceL);
            }
            Slerp(BodyId.ThighL, lower, rL);
            Slerp(BodyId.FootL, lower, rL);
            Slerp(BodyId.ThighR, lower, rR);
            Slerp(BodyId.FootR, lower, rR);
            Slerp(BodyId.ArmL, armL, r);
            Slerp(BodyId.HandL, armL, r);
            Slerp(BodyId.ArmR, armR, r);
            Slerp(BodyId.HandR, armR, r);

            // A drive's damper pulls the joint toward targetAngularVelocity, which is zero everywhere
            // in Unity unless it is set. So while the pose is swinging, the damper is braking the very
            // motion the spring is asking for. Handing it the pose's own angular velocity removes that
            // brake without touching the spring, the damping ratio or the stability of anything else.
            float step = Time.fixedDeltaTime;
            bool feed = p.driveFeedForward > 0.001f && step > 0f && targetsPrimed;
            for (int i = 1; i < Count; i++)
            {
                Quaternion local = puppet[i].localRotation;
                if (PoseOverride != null)
                {
                    var o = PoseOverride(i);
                    if (o.HasValue) local = o.Value;
                }
                // ConfigurableJoint.targetRotation is relative to the joint's starting orientation.
                Quaternion target = Quaternion.Inverse(startRel[i] * local) * startRel[i];
                joints[i].targetRotation = target;
                joints[i].targetAngularVelocity = feed
                    ? AngularVelocity(prevTarget[i], target, step) * p.driveFeedForward
                    : Vector3.zero;
                prevTarget[i] = target;
            }
            targetsPrimed = true;
        }

        float ArmBoost(PawnHand hand, RagdollParams p)
        {
            if (hand.IsHolding) return p.grabArmMultiplier;
            return input.grab && State != PawnState.Ragdoll ? p.reachArmMultiplier : 1f;
        }

        /// <summary>Angular velocity that carries <paramref name="from"/> to <paramref name="to"/> in dt.</summary>
        static Vector3 AngularVelocity(Quaternion from, Quaternion to, float dt)
        {
            Quaternion delta = to * Quaternion.Inverse(from);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-8f) return Vector3.zero;
            if (angle > 180f) angle -= 360f;
            Vector3 w = axis.normalized * (angle * Mathf.Deg2Rad / dt);
            // A teleporting pose (respawn, get-up blend) must not become a huge velocity command.
            return w.sqrMagnitude > 900f ? Vector3.zero : w;
        }

        void Slerp(BodyId id, float spring, float ratio)
        {
            joints[(int)id].slerpDrive = new JointDrive
            {
                positionSpring = spring * SlerpDriveScale,
                positionDamper = spring * ratio * SlerpDriveScale,
                maximumForce = float.MaxValue,
            };
        }

        // ---------------------------------------------------------------- collisions

        public void OnPartCollision(RagdollBodyPart part, Collision c, bool enter)
        {
            if (NetworkPuppet) return;
            ColliderOwner.TryGetValue(c.collider, out RagdollPawn other);
            if (other == this) return;
            var p = P;
            if (other != null)
            {
                contactTimer = p.contactLinger;
                // Being touched by a pawn mid-shove counts as a hit (stiffness only, no damage).
                if (shoveTimer > 0f) other.NotifyShoved();
            }
            if (!enter) return;

            float impact = 0f;
            Vector3 normal = Vector3.up;
            int n = c.contactCount;
            for (int i = 0; i < n; i++)
            {
                var contact = c.GetContact(i);
                float s = Mathf.Abs(Vector3.Dot(c.relativeVelocity, contact.normal));
                if (s <= impact) continue;
                impact = s;
                normal = contact.normal;
            }

            var hazard = c.collider.GetComponentInParent<RagdollHazard>();
            if (hazard != null && hazard.alwaysKnockdown && impact >= hazard.minImpact)
            {
                Knockdown("회전 봉");
                return;
            }
            if (impact >= p.knockdownImpulseThreshold)
            {
                Knockdown(other != null ? "캐릭터 충돌" : "충격·낙하");
                return;
            }
            bool support = Mathf.Abs(normal.y) > 0.6f &&
                           (part.id == BodyId.FootL || part.id == BodyId.FootR || part.id == BodyId.Hips);
            if (!support && impact >= p.hitImpactThreshold && State == PawnState.Active)
                hitTimer = p.hitRecoveryTime;
        }

        // ---------------------------------------------------------------- utilities

        public void Teleport(Vector3 hipsPosition, Vector3 faceDirection)
        {
            handL.Release();
            handR.Release();
            Vector3 face = FlatDir(faceDirection, Vector3.forward);
            Quaternion rot = Quaternion.LookRotation(face, Vector3.up);
            for (int i = 0; i < Count; i++)
            {
                var rb = bodies[i];
                Vector3 pos = hipsPosition + rot * bindPos[i];
                Quaternion r = rot * bindRot[i];
                rb.transform.SetPositionAndRotation(pos, r);
                rb.position = pos;
                rb.rotation = r;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            anchorPos = hipsPosition;
            anchorVel = Vector3.zero;
            anchor.transform.SetPositionAndRotation(hipsPosition, rot);
            anchor.position = hipsPosition;
            anchor.rotation = rot;
            facing = face;
            State = PawnState.Active;
            stateTimer = 0f;
            StateFactor = 1f;
            Stiffness = 1f;
            contactTimer = hitTimer = shoveTimer = airTimer = 0f;
            SetRagdollFriction(false);
            NetworkSnap = true;
        }

        // ---------------------------------------------------------------- networking

        /// <summary>Puppet mode: all bodies go kinematic and the controller stops; poses come from the wire.</summary>
        public void SetNetworkPuppet(bool on)
        {
            if (NetworkPuppet == on) return;
            NetworkPuppet = on;
            handL.Release();
            handR.Release();
            for (int i = 0; i < Count; i++)
            {
                var rb = bodies[i];
                if (!on)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = on;
                rb.interpolation = on ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            }
            if (!on)
            {
                anchorPos = bodies[0].position;
                anchorVel = Vector3.zero;
                State = PawnState.Active;
                StateFactor = 1f;
                Stiffness = 1f;
                stateTimer = 0f;
            }
        }

        public void CaptureNetworkPose(RagdollPose pose)
        {
            pose.hips = bodies[0].transform.position;
            for (int i = 0; i < Count; i++) pose.rotations[i] = bodies[i].transform.rotation;
            pose.state = State == PawnState.Ragdoll ? (byte)1 : State == PawnState.GettingUp ? (byte)2 : (byte)0;
            pose.grounded = Grounded;
            pose.grabbing = Grabbing;
            pose.snap = NetworkSnap;
        }

        /// <summary>Rebuild the whole pawn from hips + rotations (see jointOffset in Awake).</summary>
        public void ApplyNetworkPose(RagdollPose pose)
        {
            poseScratch[0] = pose.hips;
            for (int i = 1; i < Count; i++)
                poseScratch[i] = poseScratch[ParentOf[i]] + pose.rotations[ParentOf[i]] * jointOffset[i];
            for (int i = 0; i < Count; i++)
            {
                var rb = bodies[i];
                rb.transform.SetPositionAndRotation(poseScratch[i], pose.rotations[i]);
                rb.position = poseScratch[i];
                rb.rotation = pose.rotations[i];
            }
            State = pose.state == 1 ? PawnState.Ragdoll : pose.state == 2 ? PawnState.GettingUp : PawnState.Active;
            StateFactor = pose.state == 1 ? 0f : 1f;
            Grounded = pose.grounded;
            anchorPos = pose.hips;
        }

        public bool IsFinite()
        {
            foreach (var rb in bodies)
            {
                Vector3 v = rb.position;
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                    float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z)) return false;
            }
            return true;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        static Vector3 FlatDir(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
        }
    }
}
