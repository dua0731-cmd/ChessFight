using System;
using UnityEngine;

namespace ChessFight.ChessBoxingPrototype
{
    // Four independent physical bodies, a new motor, and a new combat state machine.
    // The pawn's visual rig is an adapter, not the authority for damage or game rules.
    public sealed class CBFighter : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public Rigidbody Head { get; private set; }
        public Rigidbody LeftHand { get; private set; }
        public Rigidbody RightHand { get; private set; }
        public CBPawnVisual Visual { get; private set; }
        public CBFighter Opponent { get; set; }
        public CBBodyState State { get; private set; }
        public float Health { get; private set; }
        public float Stamina { get; private set; }
        public bool Guarding { get; private set; }
        public bool Attacking => attackTime >= 0f;
        public bool WindingUp => Attacking && attackTime < Windup;
        public bool HeavyAttack => heavy;
        public bool AttackLeft => useLeft;
        public bool AttackLow => attackLow;
        public bool AttackUppercut => uppercut;
        public bool SelectedLeft => selectedLeft;
        public CBDirection GuardDirection => guardDirection;
        public CBDirection EvadeDirection => evadeDirection;
        public bool Evading => dodgeTime > 0f;
        public float EvadeAmount
        {
            get
            {
                if (dodgeTime <= 0f) return 0f;
                float elapsed = .38f - dodgeTime;
                if (evadeDirection != CBDirection.Low)
                {
                    // Move off the punch line promptly and hold the slip before recovering.
                    return elapsed < .10f ? Mathf.SmoothStep(0f, 1f, elapsed / .10f)
                        : elapsed < .28f ? 1f : Mathf.SmoothStep(1f, 0f, (elapsed - .28f) / .10f);
                }
                return Mathf.Sin(Mathf.PI * elapsed / .38f);
            }
        }
        public Vector2 Intent { get; private set; } = new Vector2(-0.45f, 0.35f);
        public float RecoilAmount => Mathf.Clamp01(recoilTime / .50f);
        public float BalanceDamage => Mathf.Clamp01(stagger / 3.2f);
        public Vector3 LastContactPoint { get; private set; }
        public bool LastContactBlocked { get; private set; }
        public string LastContactPart { get; private set; } = "None";
        public bool Stunned => stun > 0f;
        public bool Paused { get; private set; }
        public bool Charging { get; set; }
        public bool InputEnabled { get; set; } = true;
        public int Landed { get; private set; }
        public int Blocks { get; private set; }
        public string LastEvent { get; private set; } = "READY";
        public float Health01 => Health / Mathf.Max(1f, settings.maxHealth);
        public float Stamina01 => Stamina / 100f;
        public Vector3 Position => Body.position;
        public Vector3 Facing => Flat(Body.rotation * Vector3.forward).normalized;
        public Vector3 CombatFacing
        {
            get
            {
                Vector3 toward = Opponent != null ? Flat(Opponent.Position - Position) : facing;
                return toward.sqrMagnitude > .0001f ? toward.normalized : facing;
            }
        }
        public event Action<CBFighter, bool, bool> Hit;
        public event Action<CBFighter> Defeated;

        CBSettings settings;
        Rigidbody[] bodies;
        PhysicsMaterial material;
        readonly Collider[] overlaps = new Collider[32];
        readonly RaycastHit[] sweepHits = new RaycastHit[32];
        readonly Vector3[] pausedVelocity = new Vector3[4];
        readonly Vector3[] pausedAngular = new Vector3[4];
        Vector3 move, facing = Vector3.forward, lastFist;
        Vector3 spawn;
        Quaternion spawnRotation;
        float attackTime = -1f, stateTime, stun, protection, tiredDelay, stagger;
        float tiltTime, dodgeTime, dodgeCooldown;
        float recoilTime, staggerRecoveryDelay;
        Vector3 impactDirection;
        Collider pendingContact;
        Vector3 pendingContactPoint;
        float pendingContactDistance;
        bool heavy, hitUsed, useLeft, wantGuard;
        bool selectedLeft = true, attackLow, uppercut;
        CBDirection guardDirection = CBDirection.High, evadeDirection = CBDirection.Low;
        float facingAngularSpeed;
        Vector3 guardBlend;
        ConfigurableJoint headJoint;
        CapsuleCollider bodyCollider;
        Vector3 standingCenter;
        float standingHeight;
        bool ready;

        float Windup => uppercut ? 0.30f : heavy ? 0.30f : useLeft ? 0.14f : 0.19f;
        float ActiveTime => uppercut ? 0.28f : heavy ? 0.24f : 0.20f;
        float Recovery => heavy ? 0.36f : useLeft ? 0.25f : 0.30f;
        Rigidbody Fist => useLeft ? LeftHand : RightHand;

        public void Initialize(GameObject model, CBSettings config, Vector3 ground, Vector3 forward, Color accent)
        {
            settings = config;
            spawn = ground;
            spawnRotation = Quaternion.LookRotation(forward);
            transform.SetPositionAndRotation(ground, spawnRotation);
            Visual = CBPawnVisual.Prepare(model, transform, accent);
            material = new PhysicsMaterial("CB own contact")
            {
                dynamicFriction = 0.12f, staticFriction = 0.18f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            Body = MakeBody("Body", new Vector3(0f, Visual.BodyHeight, 0f), settings.bodyMass);
            var capsule = Body.gameObject.AddComponent<CapsuleCollider>();
            float bottom = -Visual.BodyHeight + 0.015f;
            float top = Mathf.Max(0.25f, Visual.HeadOffset.y - Visual.HeadRadius * 0.55f);
            capsule.radius = Mathf.Clamp(Visual.BodyRadius * 0.85f, 0.13f, 0.26f);
            capsule.height = Mathf.Max(top - bottom, capsule.radius * 2f);
            capsule.center = new Vector3(0f, (top + bottom) * 0.5f, 0f);
            capsule.sharedMaterial = material;
            bodyCollider = capsule;
            standingCenter = capsule.center;
            standingHeight = capsule.height;
            Head = MakePart("Head", Visual.HeadOffset, Visual.HeadRadius, 2f, 0.38f);
            headJoint = Head.GetComponent<ConfigurableJoint>();
            LeftHand = MakePart("Left hand", Visual.LeftHandOffset, Visual.HandRadius, 0.7f, 0.85f);
            RightHand = MakePart("Right hand", Visual.RightHandOffset, Visual.HandRadius, 0.7f, 0.85f);
            LeftHand.gameObject.AddComponent<CBGloveContact>().Bind(this, LeftHand);
            RightHand.gameObject.AddComponent<CBGloveContact>().Bind(this, RightHand);
            bodies = new[] { Body, Head, LeftHand, RightHand };
            var colliders = GetComponentsInChildren<Collider>();
            for (int a = 0; a < colliders.Length; a++)
                for (int b = a + 1; b < colliders.Length; b++)
                    Physics.IgnoreCollision(colliders[a], colliders[b]);
            Visual.Bind(Body.transform, Head.transform, LeftHand.transform, RightHand.transform);
            ready = true;
            ResetFighter();
        }

        Rigidbody MakeBody(string label, Vector3 localPosition, float mass)
        {
            var part = new GameObject(label);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPosition;
            var rb = part.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.35f;
            rb.angularDamping = 1.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.solverIterations = 18;
            rb.solverVelocityIterations = 6;
            rb.maxAngularVelocity = 18f;
            return rb;
        }

        Rigidbody MakePart(string label, Vector3 offset, float radius, float mass, float travel)
        {
            var rb = MakeBody(label, new Vector3(0f, Visual.BodyHeight, 0f) + offset, mass);
            var sphere = rb.gameObject.AddComponent<SphereCollider>();
            sphere.radius = radius;
            sphere.sharedMaterial = material;
            var joint = rb.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = Body;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = offset;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Limited;
            joint.linearLimit = new SoftJointLimit { limit = travel };
            joint.linearLimitSpring = new SoftJointLimitSpring { spring = 1800f, damper = 65f };
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.enableCollision = false;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.08f;
            return rb;
        }

        public void Command(Vector3 movement, bool guard)
        {
            move = Vector3.ClampMagnitude(Flat(movement), 1f);
            wantGuard = guard;
        }

        public void SetIntent(Vector2 value)
        {
            Intent = Vector2.ClampMagnitude(value, 1f);
            if (Intent.x < -0.18f) selectedLeft = true;
            else if (Intent.x > 0.18f) selectedLeft = false;
            guardDirection = CBControl.Classify(Intent, guardDirection);
        }

        public bool Punch(bool strong) => BeginPunch(strong, selectedLeft, Intent.y < -0.30f, false);

        public bool PunchSelected(bool left, bool body, bool rising = false)
            => BeginPunch(false, left, body, rising);

        bool BeginPunch(bool strong, bool left, bool body, bool rising)
        {
            if (!ready || Paused || !InputEnabled || State != CBBodyState.Standing || Attacking || stun > 0f || dodgeTime > 0f)
                return false;
            float cost = strong ? settings.heavyCost : settings.jabCost;
            if (Stamina < cost) { LastEvent = "STAMINA LOW"; return false; }
            Spend(cost);
            heavy = strong;
            useLeft = left;
            selectedLeft = left;
            attackLow = body;
            uppercut = rising;
            hitUsed = false;
            pendingContact = null;
            attackTime = 0f;
            Guarding = false;
            Charging = false;
            lastFist = Fist.position;
            LastEvent = (left ? "LEFT " : "RIGHT ") + (rising ? "UPPERCUT" : body ? "BODY" : "HEAD");
            return true;
        }

        // Kept for existing callers; the new input chooses a body movement, not a world-space dash.
        public void Dodge(Vector3 direction)
        {
            Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(CombatFacing)) * direction;
            DodgeDirection(Mathf.Abs(local.x) > Mathf.Abs(local.z)
                ? (local.x < 0f ? CBDirection.Left : CBDirection.Right)
                : (local.z > 0f ? CBDirection.High : CBDirection.Low));
        }

        public bool DodgeDirection(CBDirection direction)
        {
            if (!ready || Paused || !InputEnabled || State != CBBodyState.Standing || Attacking || stun > 0f || dodgeCooldown > 0f || Stamina < 14f)
                return false;
            Spend(14f);
            evadeDirection = direction;
            dodgeTime = 0.38f;
            dodgeCooldown = 0.58f;
            Guarding = false;
            LastEvent = direction == CBDirection.High ? "DUCK" : direction == CBDirection.Low ? "PULLBACK" : "SLIP " + direction;
            // Translation is deliberately short. The head collider also follows the actual evasive pose.
            Vector3 directionForward = CombatFacing;
            Vector3 step = direction == CBDirection.Low ? -directionForward * 0.8f
                : direction == CBDirection.High ? directionForward * 0.45f
                : Vector3.Cross(Vector3.up, directionForward) * (direction == CBDirection.Left ? -0.70f : 0.70f);
            AddVelocity(step);
            return true;
        }

        void FixedUpdate()
        {
            if (!ready || Paused) return;
            // Resolve all contacts from the preceding physics step together, before advancing the punch.
            if (pendingContact != null)
            {
                if (Attacking && !hitUsed) ApplyContact(pendingContact, pendingContactPoint);
                pendingContact = null;
            }
            float dt = Time.fixedDeltaTime;
            stateTime += dt;
            stun = Mathf.Max(0f, stun - dt);
            protection = Mathf.Max(0f, protection - dt);
            tiredDelay -= dt;
            dodgeTime = Mathf.Max(0f, dodgeTime - dt);
            dodgeCooldown = Mathf.Max(0f, dodgeCooldown - dt);
            recoilTime = Mathf.Max(0f, recoilTime - dt);
            staggerRecoveryDelay = Mathf.Max(0f, staggerRecoveryDelay - dt);
            if (staggerRecoveryDelay <= 0f) stagger = Mathf.Max(0f, stagger - dt * 1.6f);
            Vector3 nextFacing = CombatFacing;
            facingAngularSpeed = Mathf.Clamp(Vector3.SignedAngle(facing, nextFacing, Vector3.up) * Mathf.Deg2Rad / dt, -12f, 12f);
            facing = nextFacing;
            if (State == CBBodyState.Down && stateTime >= settings.downSeconds)
            {
                State = CBBodyState.Rising;
                stateTime = 0f;
                LastEvent = "GETTING UP";
            }
            if (State == CBBodyState.Rising)
            {
                float tilt = Vector3.Angle(Body.transform.up, Vector3.up);
                if (stateTime > 0.6f && tilt < 25f)
                {
                    State = CBBodyState.Standing;
                    stateTime = 0f;
                    protection = settings.recoveryProtection;
                    stagger = 0f;
                }
                else if (stateTime > 2f)
                {
                    // Prototype safety net: a body pinned at the barrier gets a clean stand-up.
                    PlaceBodies(new Vector3(Mathf.Clamp(Position.x, -2.7f, 2.7f), 0f, Mathf.Clamp(Position.z, -2.7f, 2.7f)), Quaternion.LookRotation(facing));
                    State = CBBodyState.Standing;
                    stateTime = 0f;
                    protection = settings.recoveryProtection;
                    stagger = 0f;
                }
            }
            bool standing = State == CBBodyState.Standing;
            bool upright = standing || State == CBBodyState.Rising;
            Guarding = standing && InputEnabled && wantGuard && !Attacking && !Charging && stun <= 0f && dodgeTime <= 0f && Stamina > 0f;
            if (Guarding) Spend(3f * dt);
            else if (standing && !Attacking && !Charging && tiredDelay <= 0f)
                Stamina = Mathf.Min(100f, Stamina + settings.staminaRecovery * dt);
            if (upright) Motor(dt, standing);
            PoseHands();
            if (standing)
            {
                tiltTime = Vector3.Angle(Body.transform.up, Vector3.up) > 72f ? tiltTime + dt : 0f;
                if (tiltTime > 0.35f && protection <= 0f) Fall(false);
            }
            if (Attacking)
            {
                float before = attackTime;
                attackTime += dt;
                if (heavy && before < Windup && attackTime >= Windup) AddVelocity(facing * 0.65f);
                if (!hitUsed && attackTime >= Windup && attackTime <= Windup + ActiveTime) TryHit();
                lastFist = Fist.position;
                if (attackTime >= Windup + ActiveTime + Recovery) attackTime = -1f;
            }
            if (Position.y < -3f && State != CBBodyState.KnockedOut)
                PlaceBodies(spawn, spawnRotation);
        }

        void Motor(float dt, bool standing)
        {
            float recoil = standing ? RecoilAmount : 0f;
            Vector3 target = standing && InputEnabled && stun <= 0f ? move * settings.moveSpeed : Vector3.zero;
            target *= Mathf.Lerp(1f, .4f, recoil);
            if (Guarding) target *= 0.48f;
            if (Attacking || Charging) target *= 0.4f;
            if (dodgeTime <= 0f)
            {
                Vector3 velocityError = target - Flat(Body.linearVelocity);
                // Let the impact carry the body before the feet regain traction.
                float acceleration = settings.acceleration * Mathf.Lerp(1f, .18f, recoil);
                Body.AddForce(Vector3.ClampMagnitude(velocityError / Mathf.Max(dt, 0.001f), acceleration), ForceMode.Acceleration);
            }
            // Side slips also bend the knees so the shoulder clears the incoming glove.
            float crouch = evadeDirection == CBDirection.High ? EvadeAmount
                : evadeDirection == CBDirection.Left || evadeDirection == CBDirection.Right ? EvadeAmount * .45f : 0f;
            bodyCollider.height = standingHeight - crouch * 0.14f;
            bodyCollider.center = standingCenter + Vector3.up * (crouch * 0.07f);
            float goalHeight = Visual.BodyHeight + (State == CBBodyState.Rising ? 0.08f : 0f) - crouch * 0.14f;
            if (Body.position.y < goalHeight + 0.25f)
            {
                float support = (goalHeight - Body.position.y) * 100f - Body.linearVelocity.y * 15f - Physics.gravity.y;
                Body.AddForce(Vector3.up * Mathf.Clamp(support, -20f, 40f), ForceMode.Acceleration);
            }
            // Quaternion error also gets a completely upside-down body back up (cross(up, down) is zero).
            float evade = EvadeAmount;
            Vector3 lean = evadeDirection == CBDirection.High ? new Vector3(15f, 0f, 0f)
                : evadeDirection == CBDirection.Low ? new Vector3(-12f, 0f, 0f)
                : new Vector3(0f, 0f, evadeDirection == CBDirection.Left ? 28f : -28f);
            Quaternion facingRotation = Quaternion.LookRotation(facing, Vector3.up);
            Quaternion error = facingRotation * Quaternion.Euler(lean * evade) * Quaternion.Inverse(Body.rotation);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (!float.IsNaN(axis.x) && axis.sqrMagnitude > 0.01f)
            {
                Vector3 turn = axis * (angle * Mathf.Deg2Rad);
                float balance = settings.balanceStrength * Mathf.Lerp(1f, .15f, recoil);
                float swayDamping = Mathf.Lerp(14f, 3.8f, recoil);
                // Preserve lock-on yaw while releasing pitch/roll to the impact.
                Vector3 torque = new Vector3(turn.x * balance, turn.y * 100f, turn.z * balance);
                Vector3 damping = new Vector3(Body.angularVelocity.x * swayDamping,
                    (Body.angularVelocity.y - facingAngularSpeed) * 20f, Body.angularVelocity.z * swayDamping);
                Body.AddTorque(torque - damping, ForceMode.Acceleration);
            }
        }

        void PoseHands()
        {
            bool limp = State == CBBodyState.Down || State == CBBodyState.KnockedOut;
            // A/D changes the common opponent-relative frame. Selecting a hand never freezes yaw.
            Quaternion lockRotation = Quaternion.LookRotation(facing, Vector3.up);
            Quaternion bodyYaw = Facing.sqrMagnitude > .001f ? Quaternion.LookRotation(Facing, Vector3.up) : lockRotation;
            Quaternion bodyTilt = Quaternion.Inverse(bodyYaw) * Body.rotation;
            Quaternion poseRotation = limp ? Body.rotation : lockRotation * bodyTilt;
            Vector3 evadeOffset = evadeDirection == CBDirection.High ? new Vector3(0f, -0.38f, 0.04f)
                : evadeDirection == CBDirection.Low ? new Vector3(0f, -0.025f, -0.20f)
                : new Vector3(evadeDirection == CBDirection.Left ? -0.30f : 0.30f, -0.035f, 0f);
            Vector3 headTarget = Visual.HeadOffset + evadeOffset * EvadeAmount;
            if (headJoint != null) headJoint.connectedAnchor = Quaternion.Inverse(Body.rotation) * (poseRotation * headTarget);
            float recoil = RecoilAmount;
            Track(Head, headTarget, poseRotation, limp ? 12f : Mathf.Lerp(750f, 150f, recoil),
                limp ? 4f : Mathf.Lerp(48f, 17f, recoil), Quaternion.identity);
            Vector3 guardWanted = guardDirection == CBDirection.Low ? new Vector3(0f, 0.18f, 0.30f)
                : guardDirection == CBDirection.Left ? new Vector3(-0.18f, Visual.HeadOffset.y - 0.06f, 0.20f)
                : guardDirection == CBDirection.Right ? new Vector3(0.18f, Visual.HeadOffset.y - 0.06f, 0.20f)
                : new Vector3(0f, Visual.HeadOffset.y - 0.025f, 0.29f);
            guardBlend = Vector3.MoveTowards(guardBlend, guardWanted, Time.fixedDeltaTime * 2.7f);
            Vector3 left = HandTarget(true), right = HandTarget(false);
            void TrackGlove(Rigidbody hand, Vector3 target, bool isLeft)
            {
                // Brace a guard or a punch; a resting glove can be displaced by real contact.
                bool braced = CanGuardWith(hand) || (Attacking && hand == Fist);
                float spring = limp ? 3f : Mathf.Lerp(braced ? 950f : 400f, 250f, recoil);
                float damping = limp ? 1.5f : Mathf.Lerp(braced ? 42f : 27f, 20f, recoil);
                Track(hand, target, poseRotation, spring, damping,
                    limp ? Quaternion.identity : Visual.HandLocalRotation(isLeft, target));
            }
            TrackGlove(LeftHand, left, true);
            TrackGlove(RightHand, right, false);
        }

        Vector3 HandTarget(bool left)
        {
            Vector3 rest = left ? Visual.LeftHandOffset : Visual.RightHandOffset;
            if (State == CBBodyState.Down || State == CBBodyState.KnockedOut) return rest;
            float side = left ? -1f : 1f;
            Vector3 stance = new Vector3(side * 0.24f, Visual.HeadOffset.y - 0.15f, 0.22f);
            bool activeHand = Attacking && left == useLeft;
            if (!activeHand && (Guarding || (wantGuard && Attacking)))
            {
                bool lateral = guardDirection == CBDirection.Left || guardDirection == CBDirection.Right;
                Vector3 offset = lateral ? new Vector3(side * 0.035f, left ? 0.07f : -0.07f, left ? 0.03f : -0.01f)
                    : new Vector3(side * 0.075f, left ? 0.065f : -0.055f, 0f);
                return Visual.ConstrainHandTarget(left, guardBlend + offset);
            }
            if (activeHand)
            {
                float extend = attackTime < Windup ? 0f
                    : attackTime < Windup + ActiveTime ? Mathf.Clamp01((attackTime - Windup) / (ActiveTime * 0.72f))
                    : 1f - Mathf.Clamp01((attackTime - Windup - ActiveTime) / Recovery);
                Vector3 back = uppercut ? new Vector3(side * 0.16f, 0.02f, 0.22f) : stance + new Vector3(side * 0.035f, 0f, -0.05f);
                Vector3 front = new Vector3(side * 0.025f, attackLow ? 0.14f : Visual.HeadOffset.y - 0.02f, uppercut ? 0.70f : 0.82f);
                if (uppercut) front.y = Visual.HeadOffset.y - 0.04f;
                Vector3 target = Vector3.Lerp(back, front, Mathf.SmoothStep(0f, 1f, extend));
                // Commit the hand and height; the finite trajectory remains in the current lock-on frame.
                return Visual.ConstrainHandTarget(left, target);
            }
            if (selectedLeft == left) stance.z -= 0.035f;
            return Visual.ConstrainHandTarget(left, stance);
        }

        void Track(Rigidbody rb, Vector3 local, Quaternion poseRotation, float spring, float damping, Quaternion localRotation)
        {
            bool limp = State == CBBodyState.Down || State == CBBodyState.KnockedOut;
            Vector3 offset = poseRotation * local;
            Vector3 target = Body.position + offset;
            Vector3 frameAngular = limp ? Body.angularVelocity : Vector3.up * facingAngularSpeed;
            Vector3 targetVelocity = Body.linearVelocity + Vector3.Cross(frameAngular, offset);
            Vector3 relative = rb.linearVelocity - targetVelocity;
            Vector3 acceleration = (target - rb.position) * spring - relative * damping - (limp ? Vector3.zero : Physics.gravity);
            rb.AddForce(Vector3.ClampMagnitude(acceleration, 160f), ForceMode.Acceleration);
            Quaternion delta = poseRotation * localRotation * Quaternion.Inverse(rb.rotation);
            delta.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f) degrees -= 360f;
            if (axis.sqrMagnitude > 0.01f && !float.IsNaN(axis.x))
            {
                float springRotation = limp ? 2f : rb == Head ? Mathf.Lerp(60f, 12f, RecoilAmount) : 90f;
                float rotationDamping = limp ? .5f : rb == Head ? Mathf.Lerp(12f, 3f, RecoilAmount) : 14f;
                rb.AddTorque(axis * degrees * Mathf.Deg2Rad * springRotation
                    - (rb.angularVelocity - frameAngular) * rotationDamping, ForceMode.Acceleration);
            }
        }

        void TryHit()
        {
            // Process the first real contact along the swept glove, including an intercepting guard.
            Vector3 delta = Fist.position - lastFist;
            float length = delta.magnitude;
            float closest = float.MaxValue;
            Collider candidate = null;
            Vector3 point = Fist.position;
            void Consider(Collider collider, float distance, Vector3 at)
            {
                if (collider == null || collider.GetComponentInParent<CBFighter>() != Opponent) return;
                Rigidbody part = collider.attachedRigidbody;
                bool glove = part == Opponent.LeftHand || part == Opponent.RightHand;
                if (part != Opponent.Body && part != Opponent.Head && !(glove && Opponent.CanGuardWith(part))) return;
                if (distance > closest || (Mathf.Approximately(distance, closest) && !glove)) return;
                closest = distance; candidate = collider; point = at;
            }
            int count = Physics.OverlapSphereNonAlloc(lastFist, Visual.HandRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) Consider(overlaps[i], 0f, overlaps[i].ClosestPoint(lastFist));
            if (length > 0.00001f)
            {
                count = Physics.SphereCastNonAlloc(lastFist, Visual.HandRadius, delta / length, sweepHits, length, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++) Consider(sweepHits[i].collider, sweepHits[i].distance, sweepHits[i].point);
            }
            if (candidate == null) return;
            ApplyContact(candidate, point);
        }

        internal void RegisterGloveContact(Rigidbody hand, Collision collision)
        {
            // PhysX can separate touching bodies before the next pre-simulation sweep.
            // Preserve that real contact, using exactly the same active window and one-hit rule.
            if (!ready || Paused || !Attacking || hitUsed || hand != Fist
                || attackTime < Windup || attackTime > Windup + ActiveTime) return;
            Collider candidate = collision.collider;
            if (candidate == null || candidate.GetComponentInParent<CBFighter>() != Opponent) return;
            Rigidbody part = candidate.attachedRigidbody;
            bool glove = part == Opponent.LeftHand || part == Opponent.RightHand;
            if (part != Opponent.Body && part != Opponent.Head && !(glove && Opponent.CanGuardWith(part))) return;
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : hand.position;
            float distance = Vector3.Distance(lastFist, point);
            // A guard and head can touch in one solver step. Do not let callback order choose the hit.
            bool pendingGlove = pendingContact != null &&
                (pendingContact.attachedRigidbody == Opponent.LeftHand || pendingContact.attachedRigidbody == Opponent.RightHand);
            if (pendingContact == null || distance < pendingContactDistance - .001f
                || Mathf.Abs(distance - pendingContactDistance) <= .001f && glove && !pendingGlove)
            {
                pendingContact = candidate;
                pendingContactPoint = point;
                pendingContactDistance = distance;
            }
        }

        void ApplyContact(Collider candidate, Vector3 point)
        {
            if (hitUsed) return;
            bool blocked = candidate.attachedRigidbody == Opponent.LeftHand || candidate.attachedRigidbody == Opponent.RightHand;
            if (!Opponent.Receive(this, heavy, blocked)) return;
            hitUsed = true;
            LastContactPoint = Opponent.LastContactPoint = point;
            LastContactPart = Opponent.LastContactPart = blocked ? "Glove" : candidate.attachedRigidbody == Opponent.Head ? "Head" : "Body";
            LastContactBlocked = Opponent.LastContactBlocked;
            if (!LastContactBlocked) Landed++;
        }

        bool CanGuardWith(Rigidbody part)
        {
            return wantGuard && !Paused && InputEnabled && State == CBBodyState.Standing && stun <= 0f
                && dodgeTime <= 0f && Stamina > 0f && (!Attacking || part != Fist);
        }

        bool Receive(CBFighter attacker, bool strong, bool gloveContact = false)
        {
            if (Paused || !InputEnabled || State != CBBodyState.Standing || protection > 0f) return false;
            Vector3 away = Flat(Position - attacker.Position).normalized;
            impactDirection = away;
            bool front = Vector3.Dot(Facing, -away) > 0.35f;
            // A glove contact from behind cannot become a hit to an unrelated body collider.
            if (gloveContact && !front) return false;
            bool blocked = wantGuard && front && gloveContact;
            float cost = strong ? settings.guardHeavyCost : settings.guardJabCost;
            bool broken = blocked && Stamina < cost;
            LastContactBlocked = blocked && !broken;
            if (blocked) Spend(cost);
            if (blocked && !broken)
            {
                Health = Mathf.Max(0f, Health - 1f);
                Blocks++;
                LastEvent = "BLOCK";
                AddVelocity(away * (strong ? 0.8f : 0.4f));
                Body.AddTorque(Vector3.Cross(Vector3.up, away) * .55f, ForceMode.VelocityChange);
            }
            else
            {
                Health = Mathf.Max(0f, Health - (strong ? settings.heavyDamage : settings.jabDamage));
                stagger += strong ? 2.6f : 1f;
                staggerRecoveryDelay = 1.10f;
                recoilTime = strong ? .65f : .50f;
                stun = strong ? 0.30f : 0.16f;
                attackTime = -1f;
                Charging = Guarding = false;
                LastEvent = broken ? "GUARD BREAK" : strong ? "HEAVY HIT" : "HIT";
                // Unequal impulses and compliant tracking let head, torso and gloves lag one another.
                Vector3 lateral = Vector3.Cross(Vector3.up, away) * (attacker.AttackLeft ? .22f : -.22f);
                Vector3 impulseDirection = (away + lateral).normalized;
                Body.AddForce(impulseDirection * (strong ? 1.25f : .72f), ForceMode.VelocityChange);
                Body.AddTorque(Vector3.Cross(Vector3.up, impulseDirection) * (strong ? 5.1f : 3.6f), ForceMode.VelocityChange);
                Head.AddForce(impulseDirection * (strong ? 1.8f : 1.1f), ForceMode.VelocityChange);
                Head.AddTorque(Vector3.Cross(Vector3.up, impulseDirection) * (strong ? 5f : 3.3f), ForceMode.VelocityChange);
                if (Health > 0f && (broken || stagger >= 3.2f)) Fall(false);
            }
            Hit?.Invoke(this, blocked && !broken, strong);
            if (Health <= 0f)
            {
                Fall(true);
                Defeated?.Invoke(this);
            }
            return true;
        }

        void Fall(bool knockout)
        {
            State = knockout ? CBBodyState.KnockedOut : CBBodyState.Down;
            dodgeTime = 0f;
            if (bodyCollider != null) { bodyCollider.height = standingHeight; bodyCollider.center = standingCenter; }
            stateTime = tiltTime = 0f;
            attackTime = -1f;
            Guarding = Charging = wantGuard = false;
            move = Vector3.zero;
            // Continue the incoming fall instead of selecting the same pre-set direction every time.
            Vector3 fallAxis = impactDirection.sqrMagnitude > .01f ? Vector3.Cross(Vector3.up, impactDirection) : -Body.transform.right;
            Body.AddTorque(fallAxis * 1.5f, ForceMode.VelocityChange);
            LastEvent = knockout ? "KO" : "DOWN";
        }

        void Spend(float amount)
        {
            Stamina = Mathf.Max(0f, Stamina - amount);
            tiredDelay = 0.65f;
        }

        void AddVelocity(Vector3 velocity)
        {
            foreach (var rb in bodies) rb.AddForce(velocity, ForceMode.VelocityChange);
        }

        public void SetPaused(bool value)
        {
            if (!ready || Paused == value) return;
            Paused = value;
            for (int i = 0; i < bodies.Length; i++)
            {
                if (value)
                {
                    pausedVelocity[i] = bodies[i].linearVelocity;
                    pausedAngular[i] = bodies[i].angularVelocity;
                    bodies[i].collisionDetectionMode = CollisionDetectionMode.Discrete;
                    bodies[i].isKinematic = true;
                }
                else
                {
                    bodies[i].isKinematic = false;
                    bodies[i].collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    bodies[i].linearVelocity = pausedVelocity[i];
                    bodies[i].angularVelocity = pausedAngular[i];
                }
            }
            if (value) { Charging = false; Command(Vector3.zero, false); }
        }

        public void ResetFighter()
        {
            if (Paused) SetPaused(false);
            State = CBBodyState.Standing;
            Health = Mathf.Max(1f, settings.maxHealth);
            Stamina = 100f;
            InputEnabled = true;
            Guarding = Charging = wantGuard = false;
            attackTime = -1f;
            selectedLeft = true; attackLow = uppercut = false;
            Intent = new Vector2(-0.45f, 0.35f);
            guardDirection = CBDirection.High;
            guardBlend = new Vector3(0f, Visual.HeadOffset.y - 0.025f, 0.29f);
            if (headJoint != null) headJoint.connectedAnchor = Visual.HeadOffset;
            if (bodyCollider != null) { bodyCollider.height = standingHeight; bodyCollider.center = standingCenter; }
            stateTime = stun = tiredDelay = stagger = tiltTime = dodgeTime = dodgeCooldown = 0f;
            recoilTime = staggerRecoveryDelay = 0f;
            impactDirection = Vector3.zero;
            pendingContact = null;
            LastContactPart = "None";
            protection = 0.6f;
            move = Vector3.zero;
            facing = spawnRotation * Vector3.forward;
            facingAngularSpeed = 0f;
            Landed = Blocks = 0;
            LastEvent = "READY";
            PlaceBodies(spawn, spawnRotation);
        }

        void PlaceBodies(Vector3 floorPosition, Quaternion rotation)
        {
            Vector3 origin = floorPosition + Vector3.up * Visual.BodyHeight;
            Vector3[] offsets = { Vector3.zero, Visual.HeadOffset, Visual.LeftHandOffset, Visual.RightHandOffset };
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].position = origin + rotation * offsets[i];
                bodies[i].rotation = rotation;
                bodies[i].linearVelocity = Vector3.zero;
                bodies[i].angularVelocity = Vector3.zero;
                bodies[i].WakeUp();
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
