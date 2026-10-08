using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Mode-local physical weapon. The shared pawn never receives sword buttons.
    [DefaultExecutionOrder(-60)]
    public sealed partial class SwordFightPawn : MonoBehaviour
    {
        public const float DrawTime = .05f, RepeatDelay = .5f;
        public RagdollPawn Pawn { get; private set; }
        public ulong Id { get; private set; }
        public int Slot { get; private set; }
        public bool Bot { get; private set; }
        public bool Alive { get; private set; } = true;
        public bool Authority { get; set; } = true;
        public bool Drawn { get; private set; }
        public bool ClassicControls { get; private set; }
        public float Protection { get; private set; }
        public float RespawnSeconds { get; set; }
        public float HitFlash { get; private set; }
        public int Swings { get; private set; }
        public int HitsLanded { get; private set; }
        public int SoftContacts { get; private set; }
        public const float StrongCutEnergy = .65f, MinimumStroke = 18f;
        public bool Attacking => Alive && (ClassicControls ? SwingAge < SwingDuration : Drawn && gesture > 0 && drawAge >= DrawTime);
        public bool WeaponVisible => sword != null && sword.gameObject.activeInHierarchy && blade.enabled;
        public Rigidbody SwordBody { get; private set; }
        public Vector3 BladeRoot => SwordBody.position + SwordBody.rotation * Vector3.forward * .13f;
        public Vector3 BladeTip => SwordBody.position + SwordBody.rotation * Vector3.forward * .88f;
        public Vector3 WeaponOffset => Alive ? Pawn.Hips.transform.InverseTransformPoint(sword.position) : Vector3.zero;
        public Quaternion WeaponRotation => Alive ? Quaternion.Inverse(Pawn.Hips.rotation) * sword.rotation : Quaternion.identity;
        public Vector3 Aim => aim;
        readonly Dictionary<SwordFightPawn, HitRecord> hits = new Dictionary<SwordFightPawn, HitRecord>();
        readonly Collider[] overlaps = new Collider[128];
        readonly Contact[] contacts = new Contact[24];
        struct HitRecord { public float Time, Travel; }
        struct Contact { public Collider Other; public Vector3 Point, Velocity, Angular; }
        Transform sword;
        Renderer blade;
        Collider bladeCollider;
        Collider[] ownerColliders;
        ConfigurableJoint gripJoint;
        Material bladeMaterial, hiltMaterial;
        PhysicsMaterial contactMaterial;
        readonly RaycastHit[] groundHits = new RaycastHit[16];
        Vector3 aim = Vector3.forward, requestedAim = Vector3.forward;
        Vector3 previousRoot, previousTip, stepVelocity, stepAngular, remoteOffset;
        Quaternion remoteRotation = Quaternion.identity, driveGoal = Quaternion.identity;
        bool held, sweepPrimed, waitForRelease;
        int contactCount;
        float drawAge, gesture, travel, strokeTravel, intentSpeed;
        Rigidbody Hand => Pawn.bodies[(int)BodyId.HandR];

        public void Initialize(ulong id, int slot, bool bot)
        {
            Pawn = GetComponent<RagdollPawn>(); Id = id; Slot = slot; Bot = bot;
            ownerColliders = Pawn.GetComponentsInChildren<Collider>();
            Pawn.PoseOverride = SwordPose;
            var root = new GameObject("Physical sword");
            root.transform.SetParent(transform); sword = root.transform;
            bladeMaterial = new Material(Shader.Find("Standard")) { color = new Color(.69f, .82f, .94f) };
            hiltMaterial = new Material(Shader.Find("Standard")) { color = new Color(.85f, .60f, .21f) };
            blade = Part("Blade", new Vector3(0, 0, .50f), new Vector3(.12f, .055f, .76f), bladeMaterial);
            Part("Guard", new Vector3(0, 0, .09f), new Vector3(.25f, .075f, .07f), hiltMaterial);
            Part("Grip", new Vector3(0, 0, -.015f), new Vector3(.07f, .075f, .16f), hiltMaterial);
            var shape = root.AddComponent<CapsuleCollider>();
            shape.direction = 2; shape.center = new Vector3(0, 0, .5f); shape.height = .8f; shape.radius = .065f;
            bladeCollider = shape;
            contactMaterial = new PhysicsMaterial("Sword smooth contact") { dynamicFriction = 0, staticFriction = 0, bounciness = 0,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
            shape.sharedMaterial = contactMaterial;
            // Register the owned weapon so the shared camera ignores it like a pawn body.
            root.layer = 2;
            RagdollPawn.ColliderOwner[bladeCollider] = Pawn;
            SwordBody = root.AddComponent<Rigidbody>();
            SwordBody.mass = .18f; SwordBody.isKinematic = true;
            // The active wrist supports the blade at the grip. Keeping the supported COM
            // on that pin avoids a translational hand impulse flipping the drawn blade.
            // Rotational inertia and collision response remain simulated.
            ConfigureSupportedMass();
            // The active wrist supports the weapon's weight; contact and rotational inertia remain physical.
            SwordBody.useGravity = false;
            SwordBody.interpolation = RigidbodyInterpolation.Interpolate;
            SwordBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // Solver safety, not a camera/input turn-rate limit.
            SwordBody.maxAngularVelocity = 30f;
            SwordBody.solverIterations = 24; SwordBody.solverVelocityIterations = 8;
            SwordBody.angularDamping = .4f;
            var contact = root.AddComponent<SwordBladeContact>(); contact.Owner = this;
            bladeCollider.enabled = false;
            // A physical pin at the hand allows rotation/contact/inertia, not a detached sword.
            gripJoint = root.AddComponent<ConfigurableJoint>();
            gripJoint.connectedBody = Hand; gripJoint.autoConfigureConnectedAnchor = false;
            gripJoint.anchor = Vector3.zero; gripJoint.connectedAnchor = Vector3.zero;
            gripJoint.xMotion = gripJoint.yMotion = gripJoint.zMotion = ConfigurableJointMotion.Free;
            gripJoint.angularXMotion = gripJoint.angularYMotion = gripJoint.angularZMotion = ConfigurableJointMotion.Free;
            gripJoint.enableCollision = false;
        }

        Renderer Part(string label, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label; part.transform.SetParent(sword, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            var c = part.GetComponent<Collider>(); c.enabled = false; Destroy(c);
            var r = part.GetComponent<Renderer>(); r.sharedMaterial = material; return r;
        }

        public void SetInput(PawnInput raw)
        {
            if (!Authority) return;
            // This mode reserves ability2 as an absolute control-style bit. Never forward it
            // to the shared pawn, and never encode a lossy toggle edge over the network.
            SetControlStyle(raw.ability2);
            if (!Alive) return;
            if (!raw.shoveHeld && !raw.shove) waitForRelease = false;
            held = raw.shoveHeld && !waitForRelease;
            if (ClassicControls && raw.shove && !waitForRelease) { inputBuffer = .13f; bufferedAim = raw.aim; }
            if (Finite(raw.aim) && raw.aim.sqrMagnitude > .01f) requestedAim = raw.aim.normalized;
            Pawn.SetInput(new PawnInput { move = raw.move, jump = raw.jump, sprint = raw.sprint, aim = raw.aim });
        }

        public void SetControlStyle(bool classic)
        {
            if (ClassicControls == classic) return;
            StopCombat(); ClassicControls = classic; waitForRelease = true;
            if (classic) PoseClickSword(); else PoseHolstered();
        }

        public void StopCombat()
        {
            held = false; SetDrawn(false); CancelClickSwing(); Pawn.SetInput(default);
        }

        void FixedUpdate()
        {
            if (Pawn == null || !Authority || !Alive) return;
            float dt = Time.fixedDeltaTime;
            Protection = Mathf.Max(0, Protection - dt); HitFlash = Mathf.Max(0, HitFlash - dt);
            if (ClassicControls) { aim = requestedAim; StepClickSwing(dt); return; }
            SetDrawn(held && Pawn.State == PawnState.Active);
            if (!Drawn) { aim = requestedAim; contactCount = 0; return; }
            // Aim follows the camera immediately; only the physical wrist has finite response.
            float delta = Vector3.Angle(aim, requestedAim); aim = requestedAim;
            // Filter across render/network packet gaps. A slow cut can ricochet quickly off a
            // body, but that solver velocity alone must never promote it to an intended strike.
            intentSpeed = Mathf.Lerp(intentSpeed, delta / dt, 1f - Mathf.Exp(-dt / .07f));
            travel += delta; drawAge += dt; gesture = Mathf.Max(0, gesture - dt);
            if (delta > 30f * dt && drawAge >= DrawTime)
            {
                if (gesture <= 0) { Swings++; strokeTravel = 0; }
                strokeTravel += delta;
                gesture = .12f;
            }
            for (int i = 0; i < contactCount; i++) TryHit(contacts[i].Other, contacts[i].Point, contacts[i].Velocity, contacts[i].Angular);
            contactCount = 0;
            if (sweepPrimed && Attacking)
            {
                // Sweep simulated poses, never an animation target. Contacts cover a blade
                // bouncing between samples; this covers angular tunnelling.
                for (int s = 0; s <= 3; s++)
                {
                    Vector3 start = Vector3.Lerp(previousRoot, BladeRoot, s / 3f);
                    Vector3 end = Vector3.Lerp(previousTip, BladeTip, s / 3f);
                    int n = Physics.OverlapCapsuleNonAlloc(start, end, .075f, overlaps, ~0, QueryTriggerInteraction.Ignore);
                    for (int j = 0; j < n; j++)
                    {
                        Vector3 point = overlaps[j].ClosestPoint((start + end) * .5f);
                        TryHit(overlaps[j], point, SwordBody.GetPointVelocity(point), SwordBody.angularVelocity);
                    }
                }
            }
            previousRoot = BladeRoot; previousTip = BladeTip; sweepPrimed = true;
            Quaternion goal = Quaternion.LookRotation(BladeDirection(), Vector3.up);
            Vector3 targetVelocity = Vector3.ClampMagnitude(RotationVector(goal * Quaternion.Inverse(driveGoal)) / dt, 30f);
            driveGoal = goal;
            Vector3 error = RotationVector(goal * Quaternion.Inverse(SwordBody.rotation));
            // Some physical follow-through remains after the softened camera changes course.
            SwordBody.AddTorque(Vector3.ClampMagnitude(error * 500f + (targetVelocity * .25f - SwordBody.angularVelocity) * 42f, 650f), ForceMode.Acceleration);
            stepVelocity = SwordBody.linearVelocity; stepAngular = SwordBody.angularVelocity;
        }

        static Vector3 RotationVector(Quaternion rotation)
        {
            rotation.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            return Finite(axis) ? axis * (angle * Mathf.Deg2Rad) : Vector3.zero;
        }

        void ConfigureSupportedMass()
        {
            SwordBody.automaticCenterOfMass = false; SwordBody.automaticInertiaTensor = false;
            SwordBody.centerOfMass = Vector3.zero;
            SwordBody.inertiaTensorRotation = Quaternion.identity;
            SwordBody.inertiaTensor = new Vector3(.025f, .025f, .003f);
        }

        void SetDrawn(bool value)
        {
            if (Drawn == value) return;
            Drawn = value; contactCount = 0; sweepPrimed = false; gesture = 0; drawAge = 0; strokeTravel = 0; intentSpeed = 0;
            if (value)
            {
                aim = requestedAim; Protection = 0;
                // Enable first: rebuilding a collider can resync the old holstered transform
                // and mass properties. Seed both visible and physics poses only afterwards.
                bladeCollider.enabled = true;
                Quaternion rotation = Quaternion.LookRotation(BladeDirection());
                sword.SetPositionAndRotation(Hand.position, rotation);
                SwordBody.isKinematic = false;
                SwordBody.position = Hand.position; SwordBody.rotation = rotation;
                ConfigureSupportedMass();
                driveGoal = SwordBody.rotation;
                SwordBody.linearVelocity = Hand.linearVelocity; SwordBody.angularVelocity = Vector3.zero;
                gripJoint.xMotion = gripJoint.yMotion = gripJoint.zMotion = ConfigurableJointMotion.Locked;
                foreach (var c in ownerColliders) if (c != null) Physics.IgnoreCollision(bladeCollider, c);
            }
            else
            {
                bladeCollider.enabled = false;
                gripJoint.xMotion = gripJoint.yMotion = gripJoint.zMotion = ConfigurableJointMotion.Free;
                SwordBody.isKinematic = true;
            }
        }

        public static Vector3 GuardDirection(Vector3 view)
        {
            Vector3 flat = Vector3.ProjectOnPlane(view, Vector3.up).normalized;
            if (flat.sqrMagnitude < .01f) flat = Vector3.forward;
            // Present the side of the blade outside the silhouette, not its end behind the head.
            Vector3 raised = Quaternion.AngleAxis(-12f, Vector3.Cross(Vector3.up, flat)) * view;
            return Quaternion.AngleAxis(24f, Vector3.up) * raised;
        }

        Vector3 BladeDirection()
        {
            Vector3 flat = new Vector3(aim.x, 0, aim.z).normalized;
            if (flat.sqrMagnitude < .01f) flat = Pawn.Facing;
            Vector3 direction = GuardDirection(aim);
            flat = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            // Do not command an impossible wrist pose through the floor. The collider still
            // resolves contact with floors, bodies and other swords; no dynamic pose is teleported.
            int count = Physics.RaycastNonAlloc(Hand.position + Vector3.up * .2f, Vector3.down, groundHits, 1.4f, ~0, QueryTriggerInteraction.Ignore);
            float floor = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
                if (!RagdollPawn.ColliderOwner.ContainsKey(groundHits[i].collider) && groundHits[i].normal.y > .6f)
                    floor = Mathf.Max(floor, groundHits[i].point.y);
            float minY = Mathf.Clamp((floor + .09f - Hand.position.y) / .88f, -1f, .95f);
            if (direction.y < minY)
                direction = flat * Mathf.Sqrt(1f - minY * minY) + Vector3.up * minY;
            return direction;
        }

        internal void RecordContact(Collision c)
        {
            if (!Authority || !Alive || !Attacking || contactCount >= contacts.Length || c.contactCount == 0) return;
            Vector3 point = c.GetContact(0).point;
            contacts[contactCount++] = new Contact { Other = c.collider, Point = point,
                Velocity = stepVelocity + Vector3.Cross(stepAngular, point - SwordBody.worldCenterOfMass), Angular = stepAngular };
        }

        void TryHit(Collider other, Vector3 point, Vector3 velocity, Vector3 angular)
        {
            if (!Attacking || other == null || !RagdollPawn.ColliderOwner.TryGetValue(other, out var target) || target == null || target == Pawn || target.Team == Pawn.Team || other.GetComponent<SwordBladeContact>() != null) return;
            var victim = target.GetComponent<SwordFightPawn>();
            if (victim == null || !victim.Alive || victim.Protection > 0) return;
            // Walking into someone with a stationary guard is not a damaging swing.
            Vector3 cut = velocity - Pawn.Hips.linearVelocity;
            float speed = cut.magnitude;
            // Require an intentional stroke and rotational cutting energy at the actual contact.
            // Walking, raising the hand, and tiny fast twitches cannot buy a full knockdown.
            Vector3 rotational = Vector3.Cross(angular, point - SwordBody.worldCenterOfMass);
            float energy = .5f * SwordBody.mass * rotational.sqrMagnitude;
            if (!Finite(cut) || !Finite(rotational) || speed < 2.7f || energy < StrongCutEnergy || strokeTravel < MinimumStroke || intentSpeed < 150f)
            { SoftContacts++; return; }
            if (hits.TryGetValue(victim, out var last) && (Time.time - last.Time < RepeatDelay || travel - last.Travel < 35f)) return;
            hits[victim] = new HitRecord { Time = Time.time, Travel = travel };
            Vector3 away = target.Hips.position - Pawn.Hips.position; away.y = 0;
            Vector3 direction = Vector3.ProjectOnPlane(cut, Vector3.up).normalized * .65f + away.normalized * .35f;
            if (direction.sqrMagnitude < .01f) direction = Pawn.Facing;
            float strength = Mathf.Lerp(2.8f, 4.5f, Mathf.InverseLerp(StrongCutEnergy, 2.5f, energy));
            // FixedUpdate, not a physics callback. Downed follow-ups remain legal.
            target.TakeHit(direction.normalized * strength + Vector3.up * .7f, .65f, 0, true);
            victim.StopCombat(); victim.HitFlash = .16f; HitFlash = .1f; HitsLanded++;
        }

        Quaternion? SwordPose(int part)
        {
            if (ClassicControls) return ClickSwordPose(part);
            if (!Alive || !Drawn || Pawn.State != PawnState.Active) return null;
            if (part == (int)BodyId.Chest)
                return Quaternion.Euler(0, Mathf.Clamp(Vector3.SignedAngle(Pawn.Facing, Vector3.ProjectOnPlane(aim, Vector3.up), Vector3.up), -40, 40) * .65f, -5);
            if (part == (int)BodyId.ArmR)
            {
                Vector3 reach = GuardDirection(aim) + Vector3.up * .10f;
                return Quaternion.FromToRotation(Vector3.right, Pawn.bodies[(int)BodyId.Chest].transform.InverseTransformDirection(reach));
            }
            return null;
        }

        void LateUpdate()
        {
            if (sword == null || Pawn == null) return;
            sword.gameObject.SetActive(Alive);
            if (!Alive) return;
            if (!Authority)
                sword.SetPositionAndRotation(Pawn.Hips.transform.TransformPoint(remoteOffset), Pawn.Hips.rotation * remoteRotation);
            else if (ClassicControls)
                PoseClickSword();
            else if (!Drawn)
                PoseHolstered();
            bladeMaterial.color = HitFlash > 0 ? new Color(1f, .8f, .24f) : Attacking ? new Color(.94f, .94f, .75f) : new Color(.69f, .82f, .94f);
        }
        void PoseHolstered()
        {
            // No collider or dynamic forces while holstered, including ragdoll/get-up.
            var hip = Pawn.Hips.transform;
            Vector3 point = hip.TransformPoint(new Vector3(.28f, -.06f, -.02f));
            Vector3 direction = hip.TransformDirection(new Vector3(.18f, -.35f, -1f));
            sword.SetPositionAndRotation(point, Quaternion.LookRotation(direction, hip.forward));
        }

        public void Eliminate()
        {
            StopCombat(); Alive = false; Pawn.SetNetworkPuppet(true);
            foreach (var r in Pawn.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
        public void Respawn(Vector3 floor, Vector3 facing, bool authority)
        {
            StopCombat(); Authority = authority;
            if (Pawn.NetworkPuppet) foreach (var rb in Pawn.bodies) rb.isKinematic = false;
            Pawn.SetNetworkPuppet(false);
            Pawn.Teleport(floor + Vector3.up * (Pawn.standHeight + .02f), facing);
            Pawn.SetNetworkPuppet(!authority);
            Alive = true; Protection = 1.25f; aim = requestedAim = facing;
            hits.Clear(); travel = 0; HitFlash = 0; waitForRelease = false;
            if (ClassicControls) PoseClickSword(); else PoseHolstered();
            foreach (var r in Pawn.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }
        public void ApplyRemote(bool alive, bool drawn, float protection, Vector3 offset, Quaternion rotation, float respawn, float flash, bool classic = false)
        {
            Authority = false; bladeCollider.enabled = false; SwordBody.isKinematic = true;
            gripJoint.xMotion = gripJoint.yMotion = gripJoint.zMotion = ConfigurableJointMotion.Free;
            bool changed = Alive != alive;
            Alive = alive; Drawn = drawn && alive; ClassicControls = classic; Protection = protection; RespawnSeconds = respawn; HitFlash = flash;
            remoteOffset = offset; remoteRotation = rotation;
            if (changed) foreach (var r in Pawn.GetComponentsInChildren<Renderer>(true)) r.enabled = alive;
        }
        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        void OnDestroy()
        {
            if (bladeCollider != null) RagdollPawn.ColliderOwner.Remove(bladeCollider);
            if (Pawn != null) Pawn.PoseOverride = null;
            if (bladeMaterial != null) Destroy(bladeMaterial);
            if (hiltMaterial != null) Destroy(hiltMaterial);
            if (contactMaterial != null) Destroy(contactMaterial);
        }
    }

    // Contacts only enqueue data; mutations are deferred to the owner's next step.
    public sealed class SwordBladeContact : MonoBehaviour
    {
        public SwordFightPawn Owner;
        void OnCollisionEnter(Collision c) => Owner?.RecordContact(c);
        void OnCollisionStay(Collision c) => Owner?.RecordContact(c);
    }
}
