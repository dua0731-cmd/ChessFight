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
        /// <summary>Left click. A dive / slide tackle; a throw while holding someone; a thrash while held.
        /// (The name is from when it was a shove, and the wire format still calls it that.)</summary>
        public bool shove;
        public bool grab;
        /// <summary>Held: sprint while stamina lasts.</summary>
        public bool sprint;
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

        /// <summary>In a slide tackle: limp like a knockdown, but not counted as one, and back up as
        /// soon as the slide runs out.</summary>
        public bool Diving { get; private set; }

        /// <summary>Holding sprint with stamina to spend.</summary>
        public bool Sprinting { get; private set; }

        /// <summary>Ran the stamina dry: no sprint and no new climb until it is back to sprintResume.</summary>
        public bool Exhausted { get; private set; }

        /// <summary>How far between the run (0) and the sprint (1) the gait currently is.</summary>
        public float SprintBlend => sprintBlend;

        /// <summary>The speed the pawn is running at right now, between moveSpeed and sprintSpeed.</summary>
        public float TopSpeed => Mathf.Lerp(P.moveSpeed, SprintTop(P), sprintBlend);

        /// <summary>Pawns this one has floored with a dive.</summary>
        public int Tackles { get; private set; }

        /// <summary>Times the launch guard stopped the body hopping off the ground on its own.</summary>
        public int HopsCaught { get; private set; }
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

        /// <summary>The climb is placing the hands on their holds, further out than the arms reach;
        /// RagdollVisualSync draws the arms out to meet them while this is set.</summary>
        public bool ArmsStretched => climbKinematic;

        /// <summary>0..1. Empty means the hands let go. Remote pawns read it off the wire.</summary>
        public float Stamina => NetworkPuppet ? netStamina
            : stamina < 0f ? 1f : P.climbStaminaMax <= 0f ? 0f : Mathf.Clamp01(stamina / P.climbStaminaMax);

        /// <summary>
        /// A steady point for a follow camera: the body's own centre of mass (limbs swinging against
        /// each other average out), and, while standing on something, standing height over the floor
        /// so the step bob never reaches the camera. Read from the interpolated transforms, so it
        /// moves smoothly every rendered frame.
        ///
        /// Not the locomotion anchor. That was tried: the anchor leads the body by up to 0.6 m while
        /// it changes direction, so a camera centred on it saw the pawn swing from side to side on
        /// every turn, as if its root were loose.
        /// </summary>
        public Vector3 CameraPoint
        {
            get
            {
                Vector3 sum = Vector3.zero;
                float mass = 0f;
                for (int i = 0; i < Count; i++)
                {
                    float m = Mathf.Max(0.01f, baseMass[i]);
                    sum += bodies[i].transform.position * m;
                    mass += m;
                }
                Vector3 c = sum / mass;
                if (State == PawnState.Ragdoll) return c;
                c.y = cameraFooting ? footingY + standHeight : bodies[0].transform.position.y;
                return c;
            }
        }

        /// <summary>Remote pawn: physics off, poses written from the network each frame.</summary>
        public bool NetworkPuppet { get; private set; }

        /// <summary>Set by Teleport so the next snapshot tells remotes to jump instead of interpolate.</summary>
        public bool NetworkSnap { get; set; }

        /// <summary>Test hook: return a pose for a body index to override the procedural puppet.</summary>
        public Func<int, Quaternion?> PoseOverride;

        public RagdollParams P => tuning != null ? tuning.values : fallback;
        readonly RagdollParams fallback = new RagdollParams();

        static PhysicsMaterial bodyMaterial, footMaterial, handMaterial, ragdollMaterial;

        PhysicsMaterial ownFootMaterial, ownDiveMaterial;
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
        float landDip, strideDrop, hopArc;
        float heldTimer, struggleTimer, escapeProgress, climbUp, climbSide, climbLost, climbCooldown, topOutTimer;
        float handStep = 1f;
        int movingHand;
        Vector3 wallPoint, wallNormal = Vector3.forward;
        Vector3 climbUpAxis = Vector3.up, climbAcross = Vector3.right;
        Vector3 topOutFrom, topOutMid, topOutTo;
        float topOutT, cornerTimer, cornerSide;
        Vector3 cornerFrom, cornerCtrl, cornerTo, cornerN0, cornerN1, cornerPoint0, cornerPoint1;
        const float CornerTime = 0.4f;
        readonly Vector3[] handHold = new Vector3[2];
        readonly Vector3[] palmLocal = new Vector3[2];
        readonly Quaternion[] poseRot = new Quaternion[Count];
        bool holdsPlaced, climbKinematic;
        // Set when a climb starts, cleared when grab is let go: while the same press lasts, the hands
        // must not grab the ledge the pawn has just climbed onto and haul it about.
        bool climbGrabLatch;
        Vector3 swingFrom;

        float struggleFlip = 1f, struggleRush, lastStrugglePunch;
        RagdollPawn holder;
        Collider heldCollider;
        float stamina = -1f;   // seconds; negative until the first tick reads the max
        float staminaIdle;     // seconds since stamina was last spent; recovery waits for it
        float sprintBlend, netStamina = 1f;
        float diveCooldown, jumpTimer, ledgePush;
        // While positive the launch guard stands down: set whenever the pawn is SUPPOSED to leave
        // the ground (a jump, a vault, the end of a climb, getting up).
        float freeFlight;
        Vector3 diveDirection = Vector3.forward;
        float slideSide = 1f;
        bool cameraFooting;
        float footingY, legReach;
        float headRise = 0.566f, headRadius = 0.195f;
        Vector3 leanAccel, leanLastVel;
        float leanAlign = 1f;
        bool launchCut;
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
            // Per-pawn too: a slide tackle changes its friction every step with the slope under it.
            ownDiveMaterial = new PhysicsMaterial("PawnSlide " + name)
            {
                dynamicFriction = 0.5f, staticFriction = 0.5f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
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
            // Where each palm ball sits on its hand body. The climb places the ball, not the body.
            palmLocal[0] = bodies[(int)BodyId.HandL].transform.InverseTransformPoint(handL.Center);
            palmLocal[1] = bodies[(int)BodyId.HandR].transform.InverseTransformPoint(handR.Center);
            facing = FlatDir(hips.forward, Vector3.forward);
            anchorPos = hips.position;
            // Hip joint to ankle, straight down: how much shorter a leg gets as it swings out.
            legReach = bindPos[(int)BodyId.ThighL].y - bindPos[(int)BodyId.FootL].y;
            // The head ball: the widest part of the pawn, and the part that meets a wall first.
            var headBall = bodies[(int)BodyId.Head].GetComponent<SphereCollider>();
            if (headBall != null)
            {
                Transform ht = bodies[(int)BodyId.Head].transform;
                headRise = ht.TransformPoint(headBall.center).y - hips.position.y;
                headRadius = headBall.radius * Mathf.Abs(ht.lossyScale.x);
            }
            anchor.transform.SetPositionAndRotation(anchorPos, Quaternion.LookRotation(facing));
        }

        void OnDestroy()
        {
            All.Remove(this);
            foreach (var c in own)
                if (c != null && ColliderOwner.TryGetValue(c, out var o) && o == this) ColliderOwner.Remove(c);
            if (ownFootMaterial != null) Destroy(ownFootMaterial);
            if (ownDiveMaterial != null) Destroy(ownDiveMaterial);
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
        void SetRagdollFriction(bool limp, bool dive = false)
        {
            ragdollMaterial.dynamicFriction = P.ragdollFriction;
            ragdollMaterial.staticFriction = P.ragdollFriction;
            if (dive) SetSlideFriction(P.diveFriction);
            PhysicsMaterial limpMaterial = dive ? ownDiveMaterial : ragdollMaterial;
            for (int i = 0; i < own.Count; i++)
                own[i].sharedMaterial = limp ? limpMaterial : ownMaterial[i];
        }

        void SetSlideFriction(float friction)
        {
            ownDiveMaterial.dynamicFriction = friction;
            ownDiveMaterial.staticFriction = friction;
        }

        public bool Owns(Collider c) => ownSet.Contains(c);

        public void SetInput(PawnInput next)
        {
            input.move = next.move;
            input.grab = next.grab;
            input.sprint = next.sprint;
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
            if (stamina < 0f) stamina = p.climbStaminaMax;
            contactTimer -= dt;
            hitTimer -= dt;
            shoveTimer -= dt;
            shoveCooldown -= dt;
            airTimer -= dt;
            diveCooldown -= dt;
            jumpTimer -= dt;
            freeFlight -= dt;

            SenseGround();
            coyote = Grounded ? 0.12f : coyote - dt;

            UpdateState(p, dt);
            UpdateStiffness(p, dt);
            float k = Stiffness * StateFactor;

            Jump(p, dt);
            Mantle(dt);
            UpdateClimb(p, dt);
            UpdateSprint(p, dt);
            Locomotion(p, dt);
            Struggle(p, dt);
            Shove(p);
            UpdateStamina(p, dt);
            if (!input.grab) climbGrabLatch = false;
            bool wantGrab = input.grab && State != PawnState.Ragdoll && !Climbing && !climbGrabLatch;
            handL.Tick(wantGrab, p, dt, Climbing);
            handR.Tick(wantGrab, p, dt, Climbing);
            if (Grounded) pullUpUsed = false; // one ledge vault per trip off the ground
            Pose(p, dt);
            if (Climbing) ApplyClimbPose(p);
            else EndClimbPose();
            Drives(p, k);
            GuardLaunch(p);
            // Standing on something, the camera takes its height from the floor, not the bobbing hips.
            cameraFooting = State != PawnState.Ragdoll && !Climbing && groundFound
                            && (Grounded || (coyote > 0f && jumpTimer <= 0f));
            if (cameraFooting) footingY = groundY;

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
            // The hips ride up to runLift + stepBob above standing height on purpose; counting that as
            // "off the ground" flickered Grounded on every stride, and each flicker let the body fly.
            float ride = standHeight + Mathf.Max(P.runLift + P.stepBob, P.sprintLift + P.sprintBob) + 0.1f;
            bool near = groundFound && bodies[0].position.y - groundY < ride;
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
                    if (Diving)
                    {
                        // On the flat the slide grips (short), going downhill it gets slippery and is
                        // allowed to keep going for as long as it stays fast - the whole point of
                        // throwing yourself down a hill. Getting up keeps the speed it earned.
                        Vector3 travel = Flat(bodies[0].linearVelocity);
                        bool downhill = groundFound && SlopeAngle >= 5f && Vector3.Dot(travel, Flat(groundNormal)) > 0f;
                        float steep = downhill ? Mathf.Clamp01((SlopeAngle - 5f) / 15f) : 0f;
                        SetSlideFriction(Mathf.Lerp(p.diveFriction, p.diveSlopeFriction, steep));
                        bool fast = travel.magnitude >= p.diveGetUpSpeed;
                        bool slowed = stateTimer >= p.diveMinTime && !fast;
                        bool timeUp = stateTimer >= p.diveMaxTime && !(steep > 0.5f && fast);
                        if (slowed || timeUp || stateTimer >= p.diveMaxTime * 4f) BeginGetUp(p);
                    }
                    else if (stateTimer >= p.getUpDelay) BeginGetUp(p);
                    break;
                case PawnState.GettingUp:
                    float t = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, p.getUpBlendTime));
                    StateFactor = t * t * (3f - 2f * t);
                    if (t >= 1f)
                    {
                        State = PawnState.Active;
                        stateTimer = 0f;
                        StateFactor = 1f;
                        // The anchor has just hauled the body up to standing; let it settle.
                        freeFlight = Mathf.Max(freeFlight, 0.3f);
                    }
                    break;
            }
        }

        public void Knockdown(string cause)
        {
            EndClimbPose();
            if (State == PawnState.Ragdoll && !Diving) return;
            Knockdowns++;
            LastKnockdownCause = cause;
            if (Diving)
            {
                // Already limp: the dive just turns into a real fall and waits out getUpDelay.
                Diving = false;
                stateTimer = 0f;
                SetRagdollFriction(true);
                return;
            }
            GoLimp(false);
        }

        void GoLimp(bool dive)
        {
            State = PawnState.Ragdoll;
            stateTimer = 0f;
            StateFactor = 0f;
            Diving = dive;
            handL.Release();
            handR.Release();
            shoveTimer = 0f;
            throwOnShoveEnd = false;
            SetRagdollFriction(true, dive);
        }

        /// <summary>
        /// Left click on the move: a slide tackle. The feet are kicked out ahead, the body tips over
        /// backwards and a little onto one hip, and from then on it is a completely limp ragdoll -
        /// the flop is the joke, so nothing holds a pose. It floors whoever it hits (OnPartCollision),
        /// grips on the flat so it stays short, and turns slippery downhill so it beats running.
        /// It does not count as a knockdown, and the pawn gets up by itself once the slide runs out.
        /// </summary>
        void Dive(RagdollParams p)
        {
            Vector3 want = Flat(input.move);
            Vector3 dir = want.sqrMagnitude > 0.04f ? want.normalized : facing;
            Vector3 travel = Flat(ComVelocity());
            float along = Mathf.Max(0f, Vector3.Dot(travel, dir));
            // A dive adds speed but never takes any away: from a downhill tumble at 14 m/s it keeps 14.
            float speed = Mathf.Max(along, Mathf.Min(along + p.diveBoost, p.diveMaxSpeed));
            Vector3 drift = (travel - dir * Vector3.Dot(travel, dir)) * 0.3f;
            bool onGround = Grounded || coyote > 0f;
            float hipsY = bodies[0].position.y;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            slideSide = -slideSide;   // alternate hips, so two tackles in a row do not look the same
            for (int i = 0; i < Count; i++)
            {
                Vector3 v = bodies[i].linearVelocity;
                Vector3 next = dir * speed + drift;
                // From the ground a small hop, just enough for the legs to swing out in front; in the
                // air it only softens the fall a little (no free double jump).
                next.y = onGround ? Mathf.Max(v.y, p.diveLift) : v.y + p.diveLift * 0.25f;
                // Feet first: everything below the hips shoots forward, everything above falls behind,
                // so the body goes over backwards like a slip on a banana skin. The side tip rolls it
                // onto one hip.
                float up = bodies[i].position.y - hipsY;
                next -= dir * (p.diveTip * up);
                next += side * (p.diveSideTip * slideSide * up);
                bodies[i].linearVelocity = next;
            }
            facing = dir;
            diveDirection = dir;
            UseStamina(p, p.diveStamina);
            GoLimp(true);
        }

        void BeginGetUp(RagdollParams p)
        {
            LastRagdollTime = stateTimer;
            if (Diving)
            {
                Diving = false;
                diveCooldown = p.diveCooldown;
            }
            State = PawnState.GettingUp;
            stateTimer = 0f;
            freeFlight = Mathf.Max(freeFlight, 0.3f);
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
            // Measured against the anchor as well as the run speed: letting go of sprint, or getting
            // up from a dive with speed to spare, sheds it at overspeedDecay through the anchor. The
            // anchor itself never goes past the run speed otherwise, so the launch this clamp exists
            // for is still caught.
            float limit = Mathf.Max(TopSpeed, anchorVel.magnitude) * p.overspeedClamp;
            Vector3 travel = Flat(ComVelocity());
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
                leanLastVel = anchorVel;
                leanAccel = Vector3.zero;
                return;
            }

            if (Climbing)
            {
                // UpdateClimb has already moved the anchor along the wall.
                leanLastVel = anchorVel;
                leanAccel = Vector3.zero;
                return;
            }

            Vector3 move = Flat(input.move);
            if (move.sqrMagnitude > 1f) move.Normalize();
            bool moving = move.sqrMagnitude > 0.0025f;
            wantsMove = moving;
            float top = TopSpeed;
            // The gait is scaled to whichever speed the pawn is going for, so a run is a whole run
            // and not a sprint cut short.
            float speedN = Mathf.Clamp01(HorizontalSpeed / Mathf.Max(0.1f, top));
            // A pawn off the ground for a moment without jumping - a bump, a hard stop, a sharp turn -
            // is still running. Keep the hips pulled down to standing height through the coyote
            // window instead of letting the anchor ride up with them: that is what turned every
            // small bump into a hop, and pressing jump on top of the hop into a huge one.
            bool planted = Grounded || (coyote > 0f && jumpTimer <= 0f && freeFlight <= 0f && groundFound
                                        && hp.y - groundY < standHeight + 0.35f);
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
                // A body at speed cannot pivot on the spot; cap how fast the facing may swing. Measured
                // against the sprint, so the slower run turns more sharply than the sprint does.
                float fastN = Mathf.Clamp01(HorizontalSpeed / Mathf.Max(0.1f, SprintTop(p)));
                float maxStep = Mathf.Lerp(1080f, p.turnRateTopSpeed, fastN) * dt;
                float delta = Mathf.Clamp(Mathf.DeltaAngle(current, wanted), -maxStep, maxStep);
                float yaw = (current + delta) * Mathf.Deg2Rad;
                facing = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            }

            Vector3 groundVel = Grounded && groundBody != null ? Flat(groundBody.GetPointVelocity(hp)) : Vector3.zero;
            Vector3 targetVel = move * top + groundVel;
            float accel = (moving ? p.acceleration : p.stopDeceleration) * (planted ? 1f : p.airControl);
            // Push off in steps instead of gliding: thrust peaks as each foot takes weight.
            if (p.stanceThrust > 0f && planted && moving)
            {
                // Walk: two pushes per cycle. Hop: one push while the feet are down, nothing at the
                // apex. Both waves average 1.0 so the top speed does not change either way.
                float walkPush = 1.5708f * Mathf.Abs(Mathf.Sin(gait));
                float hopPush = 3f * (1f - hopArc);
                float push = Mathf.Lerp(walkPush, hopPush, p.boundGait);
                accel *= Mathf.Lerp(1f, Mathf.Max(0.3f, push), p.stanceThrust);
            }
            float speedNow = anchorVel.magnitude;
            if (speedNow > top + 0.1f && Vector3.Dot(anchorVel, targetVel) > 0f)
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
            float leash = planted ? p.anchorLeash : p.anchorLeash * p.airControl * 0.5f;
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
            float lift = Gait(p.runLift, p.sprintLift) * Mathf.Clamp01(anchorVel.magnitude / Mathf.Max(0.1f, top));
            landDip = Mathf.MoveTowards(landDip, 0f, dt * 0.8f);
            if (planted && !wasGrounded && p.landingDip > 0f)
                landDip = Mathf.Max(landDip, p.landingDip * Mathf.Clamp01(-hips.linearVelocity.y / 6f));
            wasGrounded = planted;
            // The sprint bounds: the hips rise as the legs fling apart (twice a cycle, or once for a
            // hop). The run walks: the hips ride exactly on the legs. A straight leg swung out by some
            // angle is shorter by reach * (1 - cos angle), so the hips come down by that much as the
            // legs spread and are highest as they pass - the feet stay on the floor all the time
            // instead of the body riding up on them (which is what made the first run float).
            float spread = Mathf.Abs(Mathf.Sin(gait));
            float sprintWave = Mathf.Lerp(spread, hopArc, p.boundGait);
            float legAngle = Mathf.Abs(LegSwing(p) * Mathf.Clamp01(speedN * 1.5f) * Mathf.Sin(gait));
            float legDrop = legReach * (1f - Mathf.Cos(legAngle * Mathf.Deg2Rad));
            float runBob = p.stepBob * (1f - spread) - p.runLegDrop * legDrop;
            float bob = Gait(runBob, p.sprintBob * sprintWave) * speedN;
            next.y = planted && groundFound
                ? groundY + standHeight + lift + bob - landDip - strideDrop
                : hp.y + hips.linearVelocity.y * dt;
            anchorPos = next;
            anchor.MovePosition(anchorPos);

            // The anchor's rotation is the hips' balance target (upright + facing + lean); the anchor
            // joint's angular drives pull the hips toward it (implicit, so stiff values stay stable).
            // The running lean belongs to the direction of travel. Turning round (W then S, A then
            // D) the body spins 180 degrees while still moving the old way, and a forward lean
            // carried round that spin swept sideways across the camera - the side-to-side sway on
            // every reversal. It fades out while the pawn is not going where it faces.
            float align = anchorVel.sqrMagnitude > 0.25f ? Mathf.Clamp01(Vector3.Dot(anchorVel.normalized, facing)) : 1f;
            leanAlign = Mathf.MoveTowards(leanAlign, align * align, 6f * dt);
            // Each footfall drives the body forward a little more (the push-off), then it rises.
            float drive = Gait(p.runDrive, p.sprintDrive) * (1f - spread) * speedN;
            float lean = (Gait(p.runLean, p.sprintLean) * speedN + drive) * leanAlign;
            if (shoveTimer > 0f) lean += p.shoveLean;
            else if (input.grab && !Grabbing) lean += p.grabLean;
            // Rolling toward the stance leg reads as weight when the legs alternate. With both legs
            // together there is no stance side, so the same roll reads as a limp - fade it out.
            float roll = Gait(p.stepRoll, p.sprintRoll) * Mathf.Sin(gait) * speedN * (1f - p.boundGait) * leanAlign;
            anchor.MoveRotation(LeanIntoAcceleration(p, dt) * Quaternion.LookRotation(facing, Vector3.up)
                                * Quaternion.Euler(lean, 0f, roll));
            ClampOverspeed(p, dt);
        }

        void Jump(RagdollParams p, float dt)
        {
            if (Climbing) return;   // a jump on the wall is a kick-off (UpdateClimb)
            // Hanging off a ledge by the hands, holding forward is enough to climb onto it: after a
            // moment it does the same vault the jump button does. (Only the jump used to, so holding
            // W under a ledge just dangled there.)
            bool onLedge = handL.HoldingLedge || handR.HoldingLedge;
            ledgePush = onLedge && !Grounded && Vector3.Dot(Flat(input.move), facing) > 0.5f ? ledgePush + dt : 0f;
            bool autoVault = ledgePush >= 0.35f && !pullUpUsed;
            if ((!input.jump && !autoVault) || State == PawnState.Ragdoll) return;
            bool fromGround = coyote > 0f && airTimer <= 0f && !autoVault;
            bool pullUp = !fromGround && !pullUpUsed && onLedge;
            if (!fromGround && !pullUp) return;
            ledgePush = 0f;
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
                // SET the rise, never add to it. Adding stacked the jump on top of whatever the body
                // was already doing, so jumping out of a bump went twice as high as a clean jump.
                v.y = Mathf.Max(v.y, up);
                bodies[i].linearVelocity = v;
            }
            airTimer = 0.2f;
            jumpTimer = 0.5f;
            freeFlight = Mathf.Max(freeFlight, 0.5f);
            coyote = 0f;
            Grounded = false;
            if (pullUp) pullUpUsed = true;
        }

        /// <summary>
        /// The left click. Holding something: a lunge, then let go (a throw). Otherwise: a dive.
        /// A pawn being held never gets here - Struggle() has already taken the click as a thrash.
        /// </summary>
        void Shove(RagdollParams p)
        {
            if (input.shove && State == PawnState.Active && shoveCooldown <= 0f && !Climbing)
            {
                if (Grabbing)
                {
                    shoveTimer = p.shoveDuration;
                    shoveCooldown = p.shoveDuration + 0.25f;
                    throwOnShoveEnd = true;
                }
                else if (diveCooldown <= 0f) Dive(p);
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

        // ---------------------------------------------------------------- 전력질주 / 스테미나

        static float SprintTop(RagdollParams p) => Mathf.Max(p.moveSpeed, p.sprintSpeed);

        /// <summary>
        /// Leans the whole body into whatever the legs are doing to it: back while braking, forward
        /// while speeding up, inward round a curve - in the WORLD frame, so it does not care which
        /// way the body happens to face mid-spin. That is what stops a reversal rocking the pawn:
        /// the brake and the run-up afterwards push the same way, so the body leans once, steadily,
        /// and comes back upright. The old turn lean was worked out from how fast the FACING
        /// swung, which a 180 degree spin maxes out and a mouse turn (the direction jumps a little
        /// every frame) made flicker left and right. Smoothed over ~0.1 s; accelLean 1 is the lean
        /// that balances the push exactly, turnLean / sprintTurnLean cap it.
        /// </summary>
        Quaternion LeanIntoAcceleration(RagdollParams p, float dt)
        {
            Vector3 accel = (anchorVel - leanLastVel) / Mathf.Max(dt, 1e-4f);
            leanLastVel = anchorVel;
            leanAccel = Vector3.Lerp(leanAccel, Flat(accel), 1f - Mathf.Exp(-10f * dt));
            float cap = Mathf.Tan(Mathf.Clamp(Gait(p.turnLean, p.sprintTurnLean), 0f, 45f) * Mathf.Deg2Rad);
            Vector3 tilt = Vector3.ClampMagnitude(leanAccel * (p.accelLean / 9.81f), cap);
            return Quaternion.FromToRotation(Vector3.up, (Vector3.up + tilt).normalized);
        }

        /// <summary>A gait number between its run value and its sprint value, by how far into the sprint.</summary>
        float Gait(float run, float sprint) => Mathf.Lerp(run, sprint, sprintBlend);

        /// <summary>
        /// The hip joint stops a thigh at 60-75 degrees (RagdollLabBuilder). Commanding more does
        /// not swing the leg further: the drive shoves it into the stop, the excess spills into the
        /// sideways axis (up to 35 degrees) and the foot kicks out to the side, and past 90 degrees
        /// the target on the far side is more than 180 away so the drive pushes the WRONG way until
        /// the target comes back round. That is the sideways leg flicking seen from behind at
        /// the old 140 degree sprint. So the swing is capped at the joint's range.
        /// </summary>
        const float HipSwingLimit = 60f;

        float LegSwing(RagdollParams p) => Mathf.Min(Gait(p.legSwing, p.sprintLegSwing), HipSwingLimit);

        /// <summary>
        /// Holding sprint blends the run into the approved big run (sprintSpeed and the sprint gait)
        /// and spends stamina while the pawn is actually going somewhere. Running dry leaves it
        /// Exhausted until the pool is back to sprintResume, so the sprint cannot stutter at zero.
        /// </summary>
        void UpdateSprint(RagdollParams p, float dt)
        {
            if (Exhausted && stamina >= p.sprintResume * p.climbStaminaMax) Exhausted = false;
            bool moving = Flat(input.move).sqrMagnitude > 0.04f;
            Sprinting = input.sprint && moving && State == PawnState.Active && !Climbing && !Exhausted
                        && !BeingHeld && stamina > 0f && p.sprintSpeed > p.moveSpeed + 0.01f;
            if (Sprinting) UseStamina(p, p.sprintDrain * dt);
            sprintBlend = Mathf.MoveTowards(sprintBlend, Sprinting ? 1f : 0f, p.sprintBlendSpeed * dt);
        }

        /// <summary>Every stamina cost goes through here: climbing, sprinting, thrashing, diving.</summary>
        void UseStamina(RagdollParams p, float amount)
        {
            if (amount <= 0f) return;
            stamina = Mathf.Max(0f, stamina - amount);
            staminaIdle = 0f;
            if (stamina <= 0f) Exhausted = true;
        }

        /// <summary>One pool, one recovery: back on its feet, doing nothing tiring, after a breather.</summary>
        void UpdateStamina(RagdollParams p, float dt)
        {
            staminaIdle += dt;
            bool resting = Grounded && State == PawnState.Active && !Climbing && !Sprinting && !BeingHeld
                           && staminaIdle >= p.staminaRecoverDelay;
            if (resting) stamina += p.climbRecover * dt;
            stamina = Mathf.Min(stamina, p.climbStaminaMax);
        }

        /// <summary>
        /// The fix for pawns hopping on their own. Hard stops, sharp turns and big leg swings all end
        /// up pushing the body off the floor now and then (the one-piece legs pole-vault it), and a
        /// body already rising gets no help back down. While the pawn is standing on something and
        /// has no business leaving it - no jump, vault, climb, get-up, hit or grab - it may not come
        /// off the surface faster than launchClamp. Measured at the centre of mass and relative to
        /// whatever it stands on, so running up a slope or riding another pawn is untouched.
        /// </summary>
        void GuardLaunch(RagdollParams p)
        {
            bool cutting = false;
            if (p.launchClamp > 0.001f && State == PawnState.Active && !Climbing
                && freeFlight <= 0f && jumpTimer <= 0f && vaultTimer <= 0f && topOutTimer <= 0f
                && hitTimer <= 0f && !BeingHeld && !HoldingEnvironment()
                && groundFound && (Grounded || coyote > 0f))
            {
                Vector3 n = groundNormal.y > 0.5f ? groundNormal : Vector3.up;
                Vector3 v = ComVelocity();
                if (groundBody != null) v -= groundBody.GetPointVelocity(bodies[0].position);
                float away = Vector3.Dot(v, n);
                if (away > p.launchClamp)
                {
                    Vector3 cut = n * (away - p.launchClamp);
                    foreach (var rb in bodies) rb.linearVelocity -= cut;
                    cutting = true;
                    if (!launchCut && away > p.launchClamp + 0.5f) HopsCaught++;
                }
            }
            launchCut = cutting;
        }

        /// <summary>Velocity of the whole pawn (its centre of mass).</summary>
        Vector3 ComVelocity()
        {
            Vector3 momentum = Vector3.zero;
            float mass = 0f;
            foreach (var rb in bodies)
            {
                momentum += rb.linearVelocity * rb.mass;
                mass += rb.mass;
            }
            return momentum / Mathf.Max(0.001f, mass);
        }

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
            input.shove = false;            // this tap is a struggle, not a dive
            if (stamina <= 0f) return;      // too tired to fight
            UseStamina(p, p.struggleStamina);
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

        /// <summary>How far the body's centre is held off the face. The skirt is 0.28 m across: any
        /// closer and the body would be inside the wall.</summary>
        float ClimbHug(RagdollParams p) => p.climbHug;

        bool ClimbableHit(RagdollParams p, RaycastHit h)
        {
            if (ownSet.Contains(h.collider) || ColliderOwner.ContainsKey(h.collider)) return false;
            var rb = h.collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic) return false;
            return Mathf.Abs(h.normal.y) <= Mathf.Cos(p.climbGripAngle * Mathf.Deg2Rad);
        }

        /// <summary>Nearest climbable face along one ray.</summary>
        bool WallRay(RagdollParams p, Vector3 origin, Vector3 dir, float length, out RaycastHit hit)
        {
            hit = default;
            float best = float.MaxValue;
            bool found = false;
            int n = Physics.RaycastNonAlloc(new Ray(origin, dir), hits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance >= best || !ClimbableHit(p, h)) continue;
                best = h.distance;
                hit = h;
                found = true;
            }
            return found;
        }

        /// <summary>Anything solid that is not a pawn, at any angle, along one ray.</summary>
        bool SolidRay(Vector3 origin, Vector3 dir, float length, out RaycastHit hit)
        {
            hit = default;
            float best = float.MaxValue;
            bool found = false;
            int n = Physics.RaycastNonAlloc(new Ray(origin, dir), hits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (ownSet.Contains(h.collider) || ColliderOwner.ContainsKey(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                hit = h;
                found = true;
            }
            return found;
        }

        /// <summary>A climbable face right in front of the chest, to start a climb on.</summary>
        bool FindWallAhead(RagdollParams p, out RaycastHit hit)
        {
            Vector3 chest = bodies[(int)BodyId.Chest].position;
            float reach = p.grabRadius + standHeight * 1.1f;
            hit = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < 3; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i == 0 ? 0f : i == 1 ? 25f : -25f, Vector3.up) * facing;
                if (!WallRay(p, chest, dir, reach, out var h) || h.distance >= best) continue;
                best = h.distance;
                hit = h;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// Where the wall is under the body: three rays side by side at chest height, straight into
        /// the wall along its current normal, averaged. Always INTO the current face - the old probe
        /// took the nearest of a fan of rays, so at a corner it swapped faces from frame to frame
        /// and the pawn spun between them with its hands left on the other face.
        /// </summary>
        bool SenseWall(RagdollParams p, Vector3 at, out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            float length = ClimbHug(p) + 0.55f;
            int count = 0;
            for (int i = -1; i <= 1; i++)
            {
                // Start a little out from the body so a ray never begins inside the wall.
                Vector3 origin = at + climbAcross * (0.15f * i) + wallNormal * 0.1f;
                if (!WallRay(p, origin, -wallNormal, length, out var h)) continue;
                point += h.point;
                normal += h.normal;
                count++;
            }
            // One ray alone is the body hanging off the end of the wall, not a wall to hold.
            if (count < 2) return false;
            point /= count;
            normal.Normalize();
            return true;
        }

        void UpdateWallFrame()
        {
            Vector3 across = Vector3.Cross(wallNormal, Vector3.up);
            if (across.sqrMagnitude < 1e-4f) across = Vector3.Cross(wallNormal, facing);
            climbAcross = across.normalized;
            climbUpAxis = Vector3.Cross(climbAcross, wallNormal).normalized;
        }

        Vector3 Shoulder(int slot) => bodies[slot == 0 ? (int)BodyId.ArmL : (int)BodyId.ArmR].position;

        /// <summary>
        /// Stamina climbing in the style of PEAK, rebuilt to be solid rather than clever.
        /// While grab is held against a wall the whole pawn is placed kinematically (ApplyClimbPose),
        /// held exactly a hug's distance off the face at every step, and moved in the wall's own
        /// frame: W up, S down, A/D along. Edges stop the sideways move, the lip at the top becomes a
        /// scripted climb onto the top, the ground at the bottom ends it, jump kicks off, and letting
        /// go just drops the pawn. Stamina drains the whole time, faster while moving, and running
        /// dry opens the hands. The hands are pure presentation (UpdateClimbHands).
        /// </summary>
        void UpdateClimb(RagdollParams p, float dt)
        {
            Vector3 move = Flat(input.move);
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            climbUp = Vector3.Dot(move, facing);
            climbSide = Vector3.Dot(move, right);
            climbCooldown -= dt;

            if (topOutTimer > 0f)
            {
                TopOut(p, dt);
                return;
            }
            if (!Climbing)
            {
                holdsPlaced = false;
                bool wants = input.grab && State == PawnState.Active && stamina > 0f && climbCooldown <= 0f
                             && (!Grounded || climbUp > 0.1f);
                if (wants && FindWallAhead(p, out var ahead)) StartClimb(p, ahead);
                return;
            }
            if (!input.grab || State != PawnState.Active)
            {
                StopClimb(0.25f);
                return;
            }
            if (input.jump)
            {
                WallJump(p);
                return;
            }
            float up = Mathf.Clamp(climbUp, -1f, 1f), side = Mathf.Clamp(climbSide, -1f, 1f);
            UseStamina(p, (p.climbDrainHold + p.climbDrainMove * Mathf.Clamp01(Mathf.Abs(up) + Mathf.Abs(side))) * dt);
            if (stamina <= 0f)
            {
                stamina = 0f;
                StopClimb(1.2f);
                return;
            }

            if (cornerTimer > 0f)
            {
                TurnCorner(p, dt);
                return;
            }

            Vector3 chestAt = anchorPos + Vector3.up * 0.2f;
            if (SenseWall(p, chestAt, out var wp, out var wn))
            {
                climbLost = 0f;
                wallPoint = wp;
                // Eased, so a curved face turns the pawn smoothly instead of in steps.
                wallNormal = Vector3.Slerp(wallNormal, wn, 1f - Mathf.Exp(-12f * dt)).normalized;
                UpdateWallFrame();
            }
            else
            {
                // The face has ended at chest height, which only climbing takes the chest to: that is
                // the lip, so climb onto the top (even if W was let go on that very step).
                if (up >= 0f && TryStartTopOut(p)) return;
                // A seam or a gap for a moment is fine; the wall really gone is a fall.
                climbLost += dt;
                if (climbLost > 0.15f)
                {
                    StopClimb(0.4f);
                    return;
                }
            }

            // A reaching hand has nothing to pull with: the body is slow as a hand leaves its hold and
            // surges as it lands - that rhythm is what reads as climbing, not an elevator. The hands
            // swap back to back while climbing, so this averages exactly climbSpeed.
            float pull = handStep < 1f ? 0.35f + 1.3f * Smooth(handStep) : 1f;
            float vUp = up > 0f ? p.climbSpeed * up * pull : p.climbDownSpeed * up;
            float vSide = p.climbSideSpeed * side;
            if (Mathf.Abs(vSide) > 0.01f)
            {
                // Round a corner onto the next face, whichever way it turns; stop at anything else
                // (a wall that just ends, a face too flat to hold) instead of walking off the edge.
                float dir = Mathf.Sign(vSide);
                if (TryInsideCorner(p, chestAt, dir)) return;
                bool wallThere = WallRay(p, chestAt + climbAcross * (dir * 0.32f) + wallNormal * 0.1f,
                    -wallNormal, ClimbHug(p) + 0.55f, out _);
                bool blocked = SolidRay(chestAt, climbAcross * dir, 0.42f, out _)
                               || SolidRay(anchorPos, climbAcross * dir, 0.42f, out _);
                if (!wallThere && !blocked && TryOutsideCorner(p, chestAt, dir)) return;
                if (!wallThere || blocked) vSide = 0f;
            }
            Vector3 next = anchorPos + climbUpAxis * (vUp * dt) + climbAcross * (vSide * dt);
            // Climbing down onto the floor: stand on it.
            if (vUp < 0f && SolidRay(next + Vector3.up * 0.1f, Vector3.down, standHeight + 0.15f, out var floor)
                && floor.normal.y > 0.6f)
            {
                StopClimb(0.4f);
                return;
            }
            // A hug's distance off the face. Eased from wherever the body really is, never from the
            // running anchor - that could be 0.6 m ahead of the body, i.e. inside the wall, and the
            // climb used to start there with only the hands showing.
            float gap = Vector3.Dot(next - wallPoint, wallNormal);
            next += wallNormal * ((ClimbHug(p) - gap) * (1f - Mathf.Exp(-20f * dt)));
            // The hug is measured at the chest, but the head is wider than the arms are long and sits
            // half a metre higher: where the face leans out over the pawn it would go into the wall.
            // Keep the head a couple of centimetres off whatever is in front of it.
            // A sphere cast, not a ray: on the curved lane the bulge is often above or below the
            // middle of the head.
            float clear = HeadClearance(next);
            if (clear < 0.02f) next += wallNormal * ((0.02f - clear) * (1f - Mathf.Exp(-30f * dt)));
            anchorVel = (next - anchorPos) / Mathf.Max(dt, 1e-4f);
            anchorPos = next;
            Vector3 into = Flat(-wallNormal);
            if (into.sqrMagnitude > 1e-4f)
                facing = Vector3.RotateTowards(facing, into.normalized, 10f * dt, 0f);
            anchor.MovePosition(anchorPos);
            anchor.MoveRotation(Quaternion.LookRotation(facing, Vector3.up));
            UpdateClimbHands(p, dt, up, side);
        }

        /// <summary>
        /// A face across the way (the inside of a corner): turn onto it. The body steps into the
        /// corner, a hug's distance off both faces, and swings round to face the new one.
        /// </summary>
        bool TryInsideCorner(RagdollParams p, Vector3 chestAt, float dir)
        {
            Vector3 along = climbAcross * dir;
            float hug = ClimbHug(p);
            if (!WallRay(p, chestAt, along, hug + 0.25f, out var inner) || Vector3.Dot(inner.normal, -along) < 0.7f)
                return false;
            Vector3 to = anchorPos + along * Mathf.Max(0f, inner.distance - hug);
            StartCorner(to, Vector3.Lerp(anchorPos, to, 0.5f), inner.normal, inner.point, dir);
            return true;
        }

        /// <summary>
        /// The face ends to the side and the wall carries on round the edge (the outside of a corner):
        /// swing round the edge onto the side face. Found by looking back at the wall from just past
        /// its end and a little behind the face.
        /// </summary>
        bool TryOutsideCorner(RagdollParams p, Vector3 chestAt, float dir)
        {
            Vector3 along = climbAcross * dir;
            float hug = ClimbHug(p);
            Vector3 origin = chestAt + along * (0.42f + hug) - wallNormal * (hug + 0.2f);
            if (!WallRay(p, origin, -along, 0.8f, out var side) || Vector3.Dot(side.normal, along) < 0.7f)
                return false;
            Vector3 to = side.point + side.normal * hug - Vector3.up * 0.2f;
            if (InsideSolid(to, 0.12f)) return false;
            // The edge itself, at hip height: the side face's point brought back out to the old face.
            Vector3 edge = side.point + wallNormal * Vector3.Dot(wallPoint - side.point, wallNormal) - Vector3.up * 0.2f;
            // Out round the edge: the curve passes about half its control distance off the edge, so
            // this keeps the head (0.195 m) clear of the corner all the way round.
            StartCorner(to, edge + (wallNormal + side.normal).normalized * (hug * 2.6f), side.normal, side.point, dir);
            return true;
        }

        void StartCorner(Vector3 to, Vector3 ctrl, Vector3 normal, Vector3 point, float dir)
        {
            cornerFrom = anchorPos;
            cornerCtrl = ctrl;
            cornerTo = to;
            cornerN0 = wallNormal;
            cornerN1 = normal;
            cornerPoint0 = wallPoint;
            cornerPoint1 = point;
            cornerSide = dir;
            cornerTimer = CornerTime;
        }

        /// <summary>Round the corner on a curve, turning to face the new wall. The hands move to it
        /// once the pawn is more than half way round.</summary>
        void TurnCorner(RagdollParams p, float dt)
        {
            cornerTimer -= dt;
            float s = Smooth(1f - Mathf.Clamp01(cornerTimer / CornerTime));
            Vector3 pos = Vector3.Lerp(Vector3.Lerp(cornerFrom, cornerCtrl, s), Vector3.Lerp(cornerCtrl, cornerTo, s), s);
            anchorVel = (pos - anchorPos) / Mathf.Max(dt, 1e-4f);
            anchorPos = pos;
            Vector3 into = Flat(-Vector3.Slerp(cornerN0, cornerN1, s));
            if (into.sqrMagnitude > 1e-4f) facing = into.normalized;
            bool past = s >= 0.5f;
            wallNormal = past ? cornerN1 : cornerN0;
            wallPoint = past ? cornerPoint1 : cornerPoint0;
            UpdateWallFrame();
            climbLost = 0f;
            anchor.MovePosition(anchorPos);
            anchor.MoveRotation(Quaternion.LookRotation(facing, Vector3.up));
            // climbAcross keeps its handedness round a corner (UpdateWallFrame), so the way round is
            // the same side in the new face's frame as it was in the old one.
            if (past) UpdateClimbHands(p, dt, 0f, cornerSide);
        }

        /// <summary>How far the head ball would be from the face in front of it with the hips at
        /// <paramref name="hipsAt"/> (big if nothing is near).</summary>
        float HeadClearance(Vector3 hipsAt)
        {
            const float Back = 0.3f;
            Vector3 origin = hipsAt + Vector3.up * headRise + wallNormal * Back;
            int n = Physics.SphereCastNonAlloc(origin, headRadius, -wallNormal, hits, Back + 0.3f, ~0, QueryTriggerInteraction.Ignore);
            float best = 1f;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance <= 0f || ownSet.Contains(h.collider) || ColliderOwner.ContainsKey(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                best = Mathf.Min(best, h.distance - Back);
            }
            return best;
        }

        void StartClimb(RagdollParams p, RaycastHit hit)
        {
            Climbing = true;
            climbLost = 0f;
            wallPoint = hit.point;
            wallNormal = hit.normal;
            UpdateWallFrame();
            // From the body's real position, not the anchor's.
            anchorPos = bodies[0].position;
            anchorVel = Vector3.zero;
            Vector3 into = Flat(-wallNormal);
            if (into.sqrMagnitude > 1e-4f) facing = into.normalized;
            holdsPlaced = false;
            handStep = 1f;
            cornerTimer = 0f;
            climbGrabLatch = true;
            handL.Release();
            handR.Release();
            UpdateClimbHands(p, 0f, 0f, 0f);   // hands on the wall from the first frame
        }

        void StopClimb(float cooldown)
        {
            Climbing = false;
            topOutTimer = 0f;
            cornerTimer = 0f;
            climbCooldown = Mathf.Max(climbCooldown, cooldown);
            handL.Release(cooldown);
            handR.Release(cooldown);
        }

        /// <summary>Jump on the wall: let go and kick off it, up and backwards.</summary>
        void WallJump(RagdollParams p)
        {
            StopClimb(0.5f);
            anchorVel = wallNormal * 2.5f + Vector3.up * p.jumpImpulse;
            jumpTimer = 0.5f;
            freeFlight = Mathf.Max(freeFlight, 0.6f);
            airTimer = 0.2f;
        }

        /// <summary>
        /// The face ended below the chest while climbing up. If there is a floor on top of it, climb
        /// onto it along a fixed path - up past the lip, then in over it - rather than flinging the
        /// body and hoping (the old scramble could land short and fall back down the wall).
        /// </summary>
        bool TryStartTopOut(RagdollParams p)
        {
            // Land 0.4 m in from the edge: with less, the heels hung over it and the pawn tipped back off.
            Vector3 over = anchorPos + Vector3.up * 0.9f - wallNormal * (ClimbHug(p) + 0.4f);
            // Looking down from inside the wall means it goes on up past the chest, and the rays only
            // slipped through a seam (the curved lane is a stack of slabs); a ray started inside a
            // collider does not see it, so this would find the top of the slab BELOW and climb into
            // the one above.
            if (InsideSolid(over, 0.05f)) return false;
            if (!SolidRay(over, Vector3.down, 1.5f, out var top) || top.normal.y < 0.6f) return false;
            // The face ended at the chest, so its top is below the chest; anything higher is a seam.
            if (top.point.y > anchorPos.y + 0.25f) return false;
            float standY = top.point.y + standHeight + 0.03f;
            // Room to stand up there.
            if (SolidRay(top.point + Vector3.up * 0.05f, Vector3.up, 0.9f, out _)
                || InsideSolid(new Vector3(top.point.x, standY, top.point.z), 0.14f)) return false;
            topOutFrom = anchorPos;
            topOutMid = new Vector3(anchorPos.x, Mathf.Max(anchorPos.y, standY + 0.12f), anchorPos.z);
            topOutTo = new Vector3(top.point.x, standY, top.point.z);
            topOutTimer = Mathf.Max(0.1f, p.climbTopOut);
            topOutT = 0f;
            // Palms flat on the top, just in from the edge, pushing the body up past them.
            Vector3 edge = new Vector3(anchorPos.x, top.point.y, anchorPos.z) - wallNormal * (ClimbHug(p) + 0.08f);
            for (int slot = 0; slot < 2; slot++)
            {
                float width = Mathf.Abs(Vector3.Dot(Shoulder(slot) - anchorPos, climbAcross)) + p.climbHandSpread;
                handHold[slot] = edge + climbAcross * ((slot == 0 ? -1f : 1f) * width) + Vector3.up * handL.Radius;
            }
            handStep = 1f;
            return true;
        }

        /// <summary>0 while going up past the lip, rising to 1 as the pawn comes down onto the top:
        /// the pose straightens up, the legs come under it and the arms go back to rest, so physics
        /// takes over from a standing pawn.</summary>
        float TopOutSettle => topOutTimer > 0f ? Smooth(Mathf.Clamp01((topOutT - 0.55f) / 0.45f)) : 0f;

        void TopOut(RagdollParams p, float dt)
        {
            topOutTimer -= dt;
            float t = topOutT = 1f - Mathf.Clamp01(topOutTimer / Mathf.Max(0.1f, p.climbTopOut));
            Vector3 pos = t < 0.55f
                ? Vector3.Lerp(topOutFrom, topOutMid, Smooth(t / 0.55f))
                : Vector3.Lerp(topOutMid, topOutTo, Smooth((t - 0.55f) / 0.45f));
            anchorPos = pos;
            anchor.MovePosition(pos);
            if (topOutTimer > 0f) return;
            // Standing on top, upright and on both feet (TopOutSettle): physics back on with a small
            // step forward. No free flight - the pawn is on the ground and the anchor has to hold it
            // there; in free flight it rode along with the body instead, and a body still pitched
            // over with its legs tucked tipped back upright, over its heels and off the edge.
            Climbing = false;
            topOutTimer = 0f;
            climbCooldown = Mathf.Max(climbCooldown, 0.4f);
            anchorVel = Flat(-wallNormal) * 1f;
        }

        /// <summary>
        /// Hand over hand, as presentation. Each palm sits on a fixed point on the wall until it is
        /// too far from where it belongs for the way the pawn is moving - just above the shoulder going up,
        /// beside the shoulder going down, a step to the side going sideways - and then the hand that
        /// is furthest behind swings to a new hold (alternating when they are level). Every hold is
        /// on the face, and every palm is kept within climbArmReach of its shoulder at every step
        /// (PalmTarget), so an arm can never be drawn out across the wall.
        /// </summary>
        void UpdateClimbHands(RagdollParams p, float dt, float up, float side)
        {
            handStep = Mathf.Min(1f, handStep + p.climbCadence * dt);
            float lift = up < -0.05f ? -0.1f : 1f;
            if (!holdsPlaced)
            {
                handHold[0] = Grip(HandHome(0, p, lift, 0f), 0, p);
                handHold[1] = Grip(HandHome(1, p, lift * 0.5f, 0f), 1, p);
                holdsPlaced = true;
                movingHand = 0;
                handStep = 1f;
                return;
            }
            if (handStep < 1f) return;
            int pick = -1;
            float worst = 0f;
            for (int slot = 0; slot < 2; slot++)
            {
                // Measured against the hold the hand could really take, so at the top of a wall a
                // hand already on the lip stays there instead of slapping the same spot over and over.
                float error = (handHold[slot] - Grip(HandHome(slot, p, lift, side), slot, p)).magnitude;
                if (slot == movingHand) error *= 0.7f;   // prefer the other hand: hand over hand
                if (error <= worst) continue;
                worst = error;
                pick = slot;
            }
            bool moving = Mathf.Abs(up) > 0.05f || Mathf.Abs(side) > 0.05f;
            if (pick < 0 || worst < (moving ? 0.1f : 0.3f)) return;
            swingFrom = PalmTarget(pick, p);
            movingHand = pick;
            // Reach a little ahead along the way the pawn is going, so the new hold is not overtaken
            // before the hand has even landed.
            Vector3 lead = climbUpAxis * (0.06f * Mathf.Max(0f, up)) + climbAcross * (0.06f * side);
            handHold[pick] = Grip(HandHome(pick, p, lift, side) + lead, pick, p);
            handStep = 0f;
        }

        /// <summary>
        /// A hold near this point that is really there. Above the top of the wall the palm goes on
        /// the lip, curled over it; past the end of the wall it comes back in along the face. Holds
        /// used to go on the face's plane wherever that was, so near the top and at the ends of a
        /// wall the hands gripped thin air.
        /// </summary>
        Vector3 Grip(Vector3 at, int slot, RagdollParams p)
        {
            Vector3 shoulder = Shoulder(slot);
            at = OnFace(at, slot, p);
            float inward = Vector3.Dot(shoulder - at, climbAcross) > 0f ? 1f : -1f;
            for (int tries = 0; tries < 4; tries++)
            {
                if (WallRay(p, at + wallNormal * 0.2f, -wallNormal, 0.45f, out _)) return at;
                // Over the top: look down just inside the face for the lip, no lower than the shoulder
                // (a long ray would take the floor beside a wall's end for a lip).
                Vector3 over = at - wallNormal * (handL.Radius + 0.06f) + Vector3.up * 0.3f;
                if (SolidRay(over, Vector3.down, 0.3f + Mathf.Max(0f, at.y - shoulder.y), out var top)
                    && top.normal.y > 0.6f)
                {
                    Vector3 lip = top.point + Vector3.up * handL.Radius;
                    return shoulder + Vector3.ClampMagnitude(lip - shoulder, p.climbArmReach);
                }
                // Off the end of the wall: back toward the body.
                at = OnFace(at + climbAcross * (inward * 0.12f), slot, p);
            }
            return at;
        }

        /// <summary>Where a palm belongs right now: above the shoulder (or beside it going down), out to
        /// its own side, a step toward the way the pawn is moving.</summary>
        Vector3 HandHome(int slot, RagdollParams p, float lift, float side)
        {
            Vector3 at = Shoulder(slot) + climbUpAxis * (p.climbHandStep * lift)
                         + climbAcross * ((slot == 0 ? -1f : 1f) * p.climbHandSpread + side * 0.1f);
            return OnFace(at, slot, p);
        }

        /// <summary>Onto the face, one palm radius off it, within reach of the shoulder.</summary>
        Vector3 OnFace(Vector3 at, int slot, RagdollParams p)
        {
            at += wallNormal * (handL.Radius - Vector3.Dot(at - wallPoint, wallNormal));
            Vector3 shoulder = Shoulder(slot);
            return shoulder + Vector3.ClampMagnitude(at - shoulder, p.climbArmReach);
        }

        /// <summary>
        /// While on the wall the pawn stops being a ragdoll and is placed, bone by bone, from the
        /// puppet pose. A spring-driven arm cannot hold a palm on a fixed point, and pinning just the
        /// palms with a kinematic body turns the joint chain into a catapult. Taking the whole body
        /// out of physics for the duration is the version that both holds the hands still and stays
        /// put. Physics resumes the instant the climb ends, carrying the body's velocity.
        /// </summary>
        void ApplyClimbPose(RagdollParams p)
        {
            if (!climbKinematic)
            {
                foreach (var rb in bodies) rb.isKinematic = true;
                climbKinematic = true;
            }
            // Upright on the wall (the big head is what touches it first), leaning in over the top on
            // the way over the lip and straightening up again before standing on it.
            float pitch = topOutTimer > 0f ? 30f * Smooth(Mathf.Clamp01(topOutT / 0.3f)) * (1f - TopOutSettle) : 0f;
            Quaternion hipsRot = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);
            poseRot[0] = hipsRot;
            // One hand in the air means less to hang from: a small sag, then a snap back up.
            float sag = handStep < 1f && topOutTimer <= 0f ? p.climbSag * Mathf.Sin(handStep * Mathf.PI) : 0f;
            poseScratch[0] = anchorPos - Vector3.up * sag;
            for (int i = 1; i < Count; i++)
            {
                int parent = ParentOf[i];
                poseRot[i] = poseRot[parent] * puppet[i].localRotation;
                poseScratch[i] = poseScratch[parent] + poseRot[parent] * jointOffset[i];
            }
            // Point each arm from its shoulder straight at its palm and put the palm ball exactly
            // there. The arm is a 0.12 m stub and a hold is up to climbArmReach away, so the arm is
            // drawn out to meet the hand (RagdollVisualSync stretches it while ArmsStretched).
            for (int slot = 0; slot < 2; slot++)
            {
                int arm = slot == 0 ? (int)BodyId.ArmL : (int)BodyId.ArmR;
                int hand = arm + 1;
                Vector3 palm = PalmTarget(slot, p);
                Vector3 along = palm - poseScratch[arm];
                if (along.sqrMagnitude < 1e-6f) along = -wallNormal;
                Quaternion aim = Quaternion.FromToRotation(slot == 0 ? Vector3.left : Vector3.right, along.normalized);
                poseRot[arm] = aim;
                poseRot[hand] = aim;
                poseScratch[hand] = palm - aim * palmLocal[slot];
            }
            for (int i = 0; i < Count; i++)
            {
                bodies[i].MovePosition(poseScratch[i]);
                bodies[i].MoveRotation(poseRot[i]);
            }
        }

        /// <summary>Back to being a ragdoll, carrying the climb's own speed.</summary>
        public void EndClimbPose()
        {
            if (!climbKinematic) return;
            climbKinematic = false;
            freeFlight = Mathf.Max(freeFlight, 0.5f);
            // Only if the body really ended up inside something, step it out along the face. The old
            // version pushed it out whenever it was near the face plane, which on a ledge meant
            // shoving the pawn backwards off the edge it had just climbed onto.
            float clear = P.grabRadius + 0.2f;
            if (InsideSolid(bodies[0].position, 0.2f) || InsideSolid(bodies[(int)BodyId.Chest].position, 0.16f))
            {
                float gap = Vector3.Dot(bodies[0].position - wallPoint, wallNormal);
                Vector3 push = wallNormal * Mathf.Max(0.05f, clear - gap);
                foreach (var rb in bodies) rb.position += push;
                anchorPos += push;
                anchor.position = anchorPos;
            }
            // The hands were out on their holds, past the ends of the arms. Put them back where the
            // arm joints hold them before physics resumes, or the locked joints yank them in and the
            // recoil throws the pawn.
            for (int arm = (int)BodyId.ArmL; arm <= (int)BodyId.ArmR; arm += 2)
            {
                Rigidbody armBody = bodies[arm], handBody = bodies[arm + 1];
                handBody.position = armBody.position + armBody.rotation * jointOffset[arm + 1];
                handBody.rotation = armBody.rotation;
            }
            Vector3 carry = Vector3.ClampMagnitude(anchorVel, 7f);
            foreach (var rb in bodies)
            {
                rb.isKinematic = false;
                rb.linearVelocity = carry;
                rb.angularVelocity = Vector3.zero;
            }
        }

        bool InsideSolid(Vector3 at, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(at, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                if (ownSet.Contains(c) || ColliderOwner.ContainsKey(c)) continue;
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- puppet

        void Pose(RagdollParams p, float dt)
        {
            // Step at the speed the pawn is trying to go, so a blocked pawn runs in place instead of skidding.
            float speed = State == PawnState.Ragdoll ? 0f : Mathf.Max(HorizontalSpeed, anchorVel.magnitude);
            float speedN = Mathf.Clamp01(speed / Mathf.Max(0.1f, TopSpeed));
            // The tucked jump pose needs a reason: the jump button, or longer than coyote time off the
            // ground. Snapping the legs up on every one-frame loss of contact kicked the floor away,
            // and that kick was half of what made a pawn hop by itself.
            bool air = State == PawnState.Active && !Grounded && (coyote <= 0f || jumpTimer > 0f);
            // Keep a minimum cadence while the player is holding a direction, so the first push-off
            // of a standing start happens on a step instead of waiting for speed to build.
            float cadence = Mathf.Max(speed, wantsMove && State == PawnState.Active && Grounded ? 1.2f : 0f);
            // Distance-based tempo makes the legs spin faster and faster as top speed rises. Locking the
            // hop rate instead means a faster pawn covers more ground per hop at the SAME rhythm, which
            // is the only way the gait survives a big moveSpeed.
            float cycles = cadence / Mathf.Max(0.2f, p.strideLength);
            if (p.hopCadence > 0.01f)
            {
                float sprintHop = p.sprintCadence > 0.01f ? p.sprintCadence : p.hopCadence;
                cycles = Mathf.Lerp(p.hopCadence, sprintHop, sprintBlend) * Mathf.Clamp01(speedN * 2.5f);
            }
            gait += cycles * dt * Mathf.PI * 2f;
            if (gait > Mathf.PI * 2f) gait -= Mathf.PI * 2f;
            float s = Mathf.Sin(gait);
            // A real hop is a thrown body: height follows a parabola, flat at the ends, peak in the
            // middle. phase 0 = feet down, 0.5 = apex.
            float phase = gait / (Mathf.PI * 2f);
            hopArc = 4f * phase * (1f - phase);
            float legAmp = air ? 0f : LegSwing(p) * Mathf.Clamp01(speedN * 1.5f);

            // Running (not sprinting) the shoulders turn against the stepping legs and the head holds
            // still against them. The right shoulder comes forward with the left leg.
            float twist = p.runTwist * (1f - sprintBlend) * speedN * s;
            // Like the hips' lean, the chest only bends forward while going the way it faces.
            float chestLean = Gait(p.chestLean, p.sprintChestLean) * speedN * leanAlign;
            Quaternion chest = Quaternion.Euler(chestLean, -twist, 0f);
            Quaternion head = Quaternion.Euler(-0.5f * chestLean, 0.8f * twist, 0f);
            // boundGait 0 = legs alternate (a walk), 1 = legs move together (a hop). A hop has a
            // flight phase, so "the foot cannot keep up with the ground" simply stops applying - which
            // is the only way a body with 0.18 m legs can honestly move at several metres per second.
            float ampR = Mathf.Lerp(legAmp, -legAmp, p.boundGait);
            // The leg on its back swing kicks its foot out sideways (negative Z is outward on the
            // left, positive on the right), so the stepping shows beside the skirt from behind.
            float splay = Gait(p.runSplay, p.sprintSplay) * Mathf.Clamp01(speedN * 1.5f);
            float backL = Mathf.Clamp01(-s), backR = Mathf.Clamp01(ampR >= 0f ? s : -s);
            Quaternion thighL = Quaternion.Euler(-legAmp * s, 0f, -splay * backL);
            Quaternion thighR = Quaternion.Euler(ampR * s, 0f, splay * backR);
            Quaternion footL = Quaternion.Euler(0.8f * legAmp * s, 0f, 0f);
            Quaternion footR = Quaternion.Euler(-0.8f * ampR * s, 0f, 0f);
            // Two different arm actions. The run lets the arms hang beside the body and swings them
            // forward and back like pendulums (lower first, then turn about the body's side-to-side
            // axis), each against its leg. The sprint keeps the approved cartoon flail: arms out wide,
            // swept forward and back around the vertical. Turning hanging arms around the vertical,
            // as the first run did, only wiggles the hands in and out from behind.
            float armGo = air ? 0f : speedN;
            float runDown = Mathf.Lerp(p.armRestDown, p.runArmDown, Mathf.Clamp01(speedN * 2f));
            float runArm = p.armSwing * armGo * s, sprintArm = p.sprintArmSwing * armGo * s;
            Quaternion armL = Quaternion.Slerp(Quaternion.Euler(runArm, 0f, runDown),
                Quaternion.Euler(0f, -sprintArm, p.armRestDown), sprintBlend);
            Quaternion armR = Quaternion.Slerp(Quaternion.Euler(-runArm, 0f, -runDown),
                Quaternion.Euler(0f, -sprintArm, -p.armRestDown), sprintBlend);

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
                // Coming down onto the top: stand up straight on both legs, arms back at the sides.
                float settle = TopOutSettle;
                if (settle > 0f)
                {
                    thighL = Quaternion.Slerp(thighL, Quaternion.identity, settle);
                    thighR = Quaternion.Slerp(thighR, Quaternion.identity, settle);
                    footL = Quaternion.Slerp(footL, Quaternion.identity, settle);
                    footR = Quaternion.Slerp(footR, Quaternion.identity, settle);
                    chest = Quaternion.Slerp(chest, Quaternion.identity, settle);
                    head = Quaternion.Slerp(head, Quaternion.identity, settle);
                    armL = Quaternion.Slerp(armL, Quaternion.Euler(0f, 0f, p.armRestDown), settle);
                    armR = Quaternion.Slerp(armR, Quaternion.Euler(0f, 0f, -p.armRestDown), settle);
                }
            }
            else if (struggleTimer > 0f)
            {
                // Thrashing: arms out in a T and flapped up toward the head and down toward the feet,
                // fast and out of step with each other. Deliberately silly - a tantrum, not a swim.
                // Local Z swings an arm in the body's frontal plane: +Z lowers the left arm and raises
                // the right one (the rest pose uses the same signs). Swinging around Y instead, as
                // this used to, sweeps the arm between the front and the side, which read wrong.
                float fade = Mathf.Clamp01(struggleTimer / Mathf.Max(0.01f, p.struggleBurst));
                float a = Mathf.Sin(Time.time * 38f) * p.struggleSwing * fade;
                float b = Mathf.Sin(Time.time * 31f + 1.7f) * p.struggleSwing * fade;
                float rest = p.armRestDown * (1f - fade);
                armL = Quaternion.Euler(0f, 0f, rest + a);
                armR = Quaternion.Euler(0f, 0f, -rest + b);
                // The body rocks side to side with each tap and the head wobbles against it.
                chest = Quaternion.Euler(-6f * fade, 8f * fade * struggleFlip, 10f * fade * struggleFlip);
                head = Quaternion.Euler(0f, -8f * fade * struggleFlip, -14f * fade * struggleFlip);
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
        /// Where a palm ball is this step: on its hold, or for the hand that is changing holds, on an
        /// arc that peels off the wall, flings past the new hold and drops back onto it. Always within
        /// climbArmReach of its shoulder.
        /// </summary>
        Vector3 PalmTarget(int slot, RagdollParams p)
        {
            Vector3 target = handHold[slot];
            if (movingHand == slot && handStep < 1f)
            {
                float t = Smooth(handStep);
                float arc = Mathf.Sin(handStep * Mathf.PI);
                target = Vector3.Lerp(swingFrom, handHold[slot], t);
                target += wallNormal * (arc * 0.1f);
                // Fling it past the hold and let it drop back on - little arms flailing for the hold
                // is the joke. Only reaching up: a hand moving down has no reason to shoot upward.
                if (Vector3.Dot(handHold[slot] - swingFrom, climbUpAxis) > 0f)
                    target += climbUpAxis * (arc * p.climbOvershoot * 0.25f);
            }
            Vector3 shoulder = Shoulder(slot);
            float settle = TopOutSettle;
            if (settle > 0f)
            {
                // Standing up on the top: the hands let go of the lip and hang at the sides.
                int arm = slot == 0 ? (int)BodyId.ArmL : (int)BodyId.ArmR;
                Quaternion rest = Quaternion.Euler(0f, 0f, slot == 0 ? p.armRestDown : -p.armRestDown);
                Vector3 hang = bodies[(int)BodyId.Chest].rotation * (rest * (slot == 0 ? Vector3.left : Vector3.right));
                target = Vector3.Lerp(target, shoulder + hang * (jointOffset[arm + 1].magnitude + palmLocal[slot].magnitude), settle);
            }
            return shoulder + Vector3.ClampMagnitude(target - shoulder, p.climbArmReach);
        }

        /// <summary>
        /// Aims one arm at its palm. While climbing, ApplyClimbPose places the arms exactly; this is
        /// the puppet's version of the same aim, so the drives already agree when physics resumes.
        /// </summary>
        Quaternion ClimbReach(bool left, int slot, RagdollParams p, float panic)
        {
            Transform chestT = bodies[(int)BodyId.Chest].transform;
            Vector3 shoulder = bodies[left ? (int)BodyId.ArmL : (int)BodyId.ArmR].position;
            // Point the arm at the palm's hold and nothing else. A held hand is fixed in the world,
            // so as the body climbs past it the arm sweeps from overhead down past the shoulder on
            // its own - that sweep is the animation, and it cannot fall out of step with the climb
            // because the climb is what produces it.
            Vector3 aim = PalmTarget(slot, p) - shoulder;
            if (aim.sqrMagnitude < 1e-6f) aim = -wallNormal;
            Vector3 local = chestT.InverseTransformDirection(aim.normalized);
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
            float anchorSpring = p.hipAnchorStrength * kLower;
            // Near critical damping for the body on its anchor spring. At the shared 0.1 ratio it was
            // at 39% of critical, so after every change of direction the body swung past the path
            // and back around the anchor - the "root" wobbling from side to side.
            float anchorRatio = p.anchorDamperRatio > 0.0001f ? p.anchorDamperRatio : r;
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

            if (Diving && other != null)
            {
                // Slide tackle. A diver is already on the floor, so it is never knocked down by the
                // pawn it hits; the pawn it hits goes over from far less than an ordinary bump.
                if (impact >= p.diveTackleImpact && other.State != PawnState.Ragdoll && !other.NetworkPuppet)
                {
                    other.Knockdown("슬라이딩 태클");
                    other.AddVelocity(diveDirection * p.diveTacklePush + Vector3.up * (p.diveTacklePush * 0.4f));
                    Tackles++;
                }
                return;
            }
            // The belly flop at the end of a dive IS the dive; only something in the way (a wall, a
            // hazard) can turn it into a real knockdown.
            if (Diving && Mathf.Abs(normal.y) > 0.6f) return;

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
            leanAccel = leanLastVel = Vector3.zero;
            leanAlign = 1f;
            anchor.transform.SetPositionAndRotation(hipsPosition, rot);
            anchor.position = hipsPosition;
            anchor.rotation = rot;
            facing = face;
            State = PawnState.Active;
            stateTimer = 0f;
            StateFactor = 1f;
            Stiffness = 1f;
            contactTimer = hitTimer = shoveTimer = airTimer = 0f;
            Diving = false;
            diveCooldown = jumpTimer = 0f;
            freeFlight = 0.3f;
            sprintBlend = 0f;
            stamina = -1f;          // refilled on the next step
            Exhausted = false;
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
            cameraFooting = false;   // a remote pawn's floor is not sensed here; follow its hips
            if (!on)
            {
                anchorPos = bodies[0].position;
                anchorVel = Vector3.zero;
                Diving = false;
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
            pose.sprinting = Sprinting;
            pose.exhausted = Exhausted;
            pose.held = BeingHeld;
            pose.stamina = Stamina;
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
            // What the stamina gauge on a client needs to draw its own pawn.
            Sprinting = pose.sprinting;
            Exhausted = pose.exhausted;
            heldTimer = pose.held ? 0.2f : 0f;
            netStamina = pose.stamina;
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
