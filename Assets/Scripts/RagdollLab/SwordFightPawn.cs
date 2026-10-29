using System.Collections.Generic;
using ChessFight.Network;
using ChessFight.Game;
using ChessFight.Network;
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
        public bool Attacking => Alive && (ClassicControls ? SwingAge < CutLength : Drawn && gesture > 0 && drawAge >= DrawTime);
        public bool WeaponVisible => sword != null && sword.gameObject.activeInHierarchy && blade.enabled;
        public Rigidbody SwordBody { get; private set; }
        /// <summary>10-25 (승규 님, the bishop's F: "그떄 비숍의 칼은 가지고 있지 않아"): the sword is out of the hand (thrown,
        /// in the board at someone's feet): its model is not drawn until it comes back. Drawing only.</summary>
        public bool SwordAway { get; set; }
        /// <summary>The sword model as drawn (its mesh and how big), for a copy of it; null with the boxes.</summary>
        public Transform SwordModel => swordModel;
        public float SwordModelScale => modelScale;
        public Vector3 BladeRoot => SwordBody.position + SwordBody.rotation * Vector3.forward * .13f;
        public Vector3 BladeTip => SwordBody.position + SwordBody.rotation * Vector3.forward * .88f;
        public Vector3 WeaponOffset => Alive ? Pawn.Hips.transform.InverseTransformPoint(sword.position) : Vector3.zero;
        public Quaternion WeaponRotation => Alive ? Quaternion.Inverse(Pawn.Hips.rotation) * sword.rotation : Quaternion.identity;
        public Vector3 Aim => aim;
        /// <summary>The piece skill (the Sword Fight skill test scene only, R91); null in the match.</summary>
        public SwordFightSkills Skills { get; set; }
        /// <summary>A test dummy the skill test drives itself for a moment (a scripted cut): the match leaves its input alone.</summary>
        public bool Scripted { get; set; }
        readonly Dictionary<SwordFightPawn, HitRecord> hits = new Dictionary<SwordFightPawn, HitRecord>();
        readonly Collider[] overlaps = new Collider[128];
        readonly Contact[] contacts = new Contact[24];
        struct HitRecord { public float Time, Travel; }
        struct Contact { public Collider Other; public Vector3 Point, Velocity, Angular; }
        Transform sword;
        Renderer blade, guard, grip;
        // Each piece's own sword (10-25, 승규 님: Tripo models refined in Blender, no colour yet), drawn in place of the three
        // boxes; the boxes stay (not drawn) for the tests and the effects that read them.
        Transform swordModel;
        Material modelMaterial;
        int modelPiece = -1, modelTeam = -2, modelBuild;   // modelBuild counts model rebuilds (piece or team), so held copies rebuild too
        Color modelBase = ModelPlain;
        /// <summary>Test key B (10-29, 승규 님): this pawn's sword wears the black team's look whatever its team.</summary>
        public bool BlackSwordTest;
        int SwordLookTeam => BlackSwordTest ? Gameplay.Teams.Black : Pawn.Team;
        static readonly Color ModelPlain = new Color(.8f, .8f, .8f);
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
        RagdollVisualSync visualSync;

        public void Initialize(ulong id, int slot, bool bot)
        {
            Pawn = GetComponent<RagdollPawn>(); Id = id; Slot = slot; Bot = bot;
            ownerColliders = Pawn.GetComponentsInChildren<Collider>();
            Pawn.PoseOverride = SwordPose;
            var root = new GameObject("Physical sword");
            root.transform.SetParent(transform); sword = root.transform;
            bladeMaterial = new Material(LitMaterials.Lit) { color = new Color(.69f, .82f, .94f) };
            hiltMaterial = new Material(LitMaterials.Lit) { color = new Color(.85f, .60f, .21f) };
            blade = Part("Blade", new Vector3(0, 0, .50f), new Vector3(.12f, .055f, .76f), bladeMaterial);
            guard = Part("Guard", new Vector3(0, 0, .09f), new Vector3(.25f, .075f, .07f), hiltMaterial);
            grip = Part("Grip", new Vector3(0, 0, -.015f), new Vector3(.07f, .075f, .16f), hiltMaterial);
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

        /// <summary>R149: someone's sword knocked someone down (the prototype match's credit for a fall).</summary>
        public static event System.Action<SwordFightPawn, SwordFightPawn> Struck;
        internal static void RaiseStruck(SwordFightPawn by, SwordFightPawn victim) => Struck?.Invoke(by, victim);

        /// <summary>10-29: a sword cut landed on this piece (every machine, with the hit sound): the hit effect.</summary>
        public static event System.Action<SwordFightPawn> Cut;

        /// <summary>R149: the respawn protection ends at once (the piece attacked: a skill, spec §8.4).</summary>
        public void EndProtection() => Protection = 0f;

        public void SetInput(PawnInput raw)
        {
            if (!Authority) return;
            // R149: the countdown and the result hold everyone still (the prototype match); the control style stays.
            if (SwordFightGame.InputLocked) raw = new PawnInput { ability2 = raw.ability2, aim = raw.aim };
            if (Skills != null) raw = Skills.Filter(raw);
            // This mode reserves ability2 as an absolute control-style bit. Never forward it
            // to the shared pawn, and never encode a lossy toggle edge over the network.
            SetControlStyle(raw.ability2);
            if (!Alive) return;
            if (!raw.shoveHeld && !raw.shove) waitForRelease = false;
            held = raw.shoveHeld && !waitForRelease;
            if (ClassicControls && raw.shove && !waitForRelease) { inputBuffer = ClickBuffer; bufferedAim = raw.aim; }
            // A click waiting for the cut before it to end takes the aim it has when it starts.
            else if (inputBuffer > 0 && Finite(raw.aim) && raw.aim.sqrMagnitude > .01f) bufferedAim = raw.aim;
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
            Vector3 direction = GuardDirection(aim);
            // Do not command an impossible wrist pose through the floor. The collider still
            // resolves contact with floors, bodies and other swords; no dynamic pose is teleported.
            return AboveFloor(Hand.position, direction, .09f);
        }

        /// <summary>The blade from <paramref name="grip"/> tipped up just enough that its tip stays above the floor under
        /// the hand (the posed swords: the click swing, the holster, the skills; they have no collider to stop them).</summary>
        Vector3 AboveFloor(Vector3 grip, Vector3 direction, float margin = .07f)
        {
            if (direction.sqrMagnitude < 1e-6f) return direction;
            direction.Normalize();
            int count = Physics.RaycastNonAlloc(grip + Vector3.up * .2f, Vector3.down, groundHits, 1.4f, ~0, QueryTriggerInteraction.Ignore);
            float floor = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
                // A piece's own body is not the floor (the piece-model bodies are not in ColliderOwner: the arm under the
                // hand held the blade level through the cut) - only static ground counts.
                if (!RagdollPawn.ColliderOwner.ContainsKey(groundHits[i].collider) && groundHits[i].normal.y > .6f
                    && (groundHits[i].rigidbody == null || groundHits[i].rigidbody.isKinematic))
                    floor = Mathf.Max(floor, groundHits[i].point.y);
            float minY = Mathf.Clamp((floor + margin - grip.y) / .88f, -1f, .95f);
            if (direction.y >= minY) return direction;
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Pawn.Facing;
            return flat * Mathf.Sqrt(1f - minY * minY) + Vector3.up * minY;
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
            // A king in its guard (the skill test scene) takes the cut and answers it.
            if (victim.Skills != null && victim.Skills.Parry(this, point)) { HitFlash = .1f; return; }
            Vector3 away = target.Hips.position - Pawn.Hips.position; away.y = 0;
            Vector3 direction = Vector3.ProjectOnPlane(cut, Vector3.up).normalized * .65f + away.normalized * .35f;
            if (direction.sqrMagnitude < .01f) direction = Pawn.Facing;
            float strength = Mathf.Lerp(2.8f, 4.5f, Mathf.InverseLerp(StrongCutEnergy, 2.5f, energy));
            // FixedUpdate, not a physics callback. Downed follow-ups remain legal.
            target.TakeHit(direction.normalized * strength + Vector3.up * .7f, .65f, 0, true);
            victim.StopCombat(); victim.FlashHit(.16f); HitFlash = .1f; HitsLanded++;
            RaiseStruck(this, victim);
        }

        Quaternion? SwordPose(int part)
        {
            if (Alive && Skills != null)
            {
                // 10-26 (승규 님: "기물이 스킬 쓸 때 연출과 모션이 필요하다고 하면 추가해도 돼"): the piece's own body through its
                // skill - chest, head, free arm, legs (SwordFightSkills.Motion) - and a piece the queen's edge cut went
                // through held as it stood. Null: the puppet's own, or the sword arm below.
                if (Skills.SkillBodyPose(part) is Quaternion own) return own;
                // The king's hand goes to his hilt in the stone even while he is still getting up from the push.
                if ((Pawn.State == PawnState.Active || Skills.KingHanging) && Skills.Blade is Vector3 skillBlade)
                {
                    if (part != (int)BodyId.ArmR) return null;
                    Vector3 along = Pawn.bodies[(int)BodyId.Chest].transform.InverseTransformDirection(Skills.Arm ?? AboveFloor(Hand.position, skillBlade));
                    // A skill's motion may take the arm off the blade's line (a fist drawn back, a hand before the chest).
                    if (Skills.SkillSwordArm(out Vector3 arm, out float w)) along = Vector3.Slerp(along.normalized, arm.normalized, w);
                    return Quaternion.FromToRotation(Vector3.right, along);
                }
            }
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
            if (!Alive) { SwordAway = false; return; }
            UpdateSwordModel();
            // 10-27: the bishop's sword is out of his hand while it is thrown (its copy flies, SwordFightPawn.OffHand.cs).
            if (Skills != null && Pawn.Piece == PieceKind.Bishop) SwordAway = Skills.BishopSwordsOut;
            if (swordModel != null && swordModel.gameObject.activeSelf == SwordAway) swordModel.gameObject.SetActive(!SwordAway);
            // The click sword is placed in the drawn hand once the skeleton is drawn this frame (10-25: a frame behind
            // it, the sword left the hand in a fast cut once the arm went all the way with it). Hooked afresh each
            // frame: a hook made when the pawn was set up did not last into play.
            if (visualSync == null) visualSync = Pawn.GetComponentInChildren<RagdollVisualSync>();
            if (visualSync != null)
            {
                visualSync.Posed -= PoseAfterSkeleton; visualSync.Posed += PoseAfterSkeleton;
                // 10-27: and a skill's second sword in the left hand (SwordFightPawn.OffHand.cs), on every machine.
                visualSync.Posed -= PoseOffHand; visualSync.Posed += PoseOffHand;
            }
            Pawn.RightArmFree = ClassicControls || Drawn || (Skills != null && Skills.Blade != null);
            // 10-26: a skill that poses the free arm has it drawn where it goes (every machine, from the skill's state);
            // 10-28: and so do the click cuts as it swings with them (StepOffArmShare).
            float offShare = StepOffArmShare();
            Pawn.LeftArmShare = Mathf.MoveTowards(Pawn.LeftArmShare, Skills != null && Skills.LeftArmPosed ? 1f : offShare, Time.deltaTime / 0.12f);
            if (!Authority)
                sword.SetPositionAndRotation(Pawn.Hips.transform.TransformPoint(remoteOffset), Pawn.Hips.rotation * remoteRotation);
            else if (Skills != null && Skills.Blade is Vector3 skillBlade)
            {
                // A sword stuck in the stone stays where it went in (the king's edge skill, R103).
                if (Skills.SwordAt is Vector3 stuck) sword.SetPositionAndRotation(stuck, Quaternion.LookRotation(skillBlade));
                else
                {
                    Vector3 grip = Hand.position + skillBlade * Skills.Reach01;
                    sword.SetPositionAndRotation(grip, Quaternion.LookRotation(AboveFloor(grip, skillBlade)));
                }
            }
            else if (ClassicControls)
            {
                if (visualSync == null || !visualSync.isActiveAndEnabled) PoseClickSword();
            }
            else if (!Drawn)
                PoseHolstered();
            bladeMaterial.color = HitFlash > 0 ? new Color(1f, .8f, .24f) : Skills != null && Skills.BladeTint is Color tint ? tint
                : Attacking ? new Color(.94f, .94f, .75f) : new Color(.69f, .82f, .94f);
            // The model is plain grey; it takes the blade's colour only for a skill's tint. 10-25 (승규 님: skills "마법 없이
            // 완전 실사"): no yellow flash on a hit or a parry any more - a steel blade stays steel (the sparks show the blow).
            if (modelMaterial != null)
                modelMaterial.color = Skills != null && Skills.BladeTint is Color mt ? Color.Lerp(modelBase, mt, .7f) : modelBase;
        }

        /// <summary>Swap in the sword of the piece this pawn is now (Resources/SwordFightSwords/&lt;piece&gt;), or the boxes if
        /// there is none. A model's grip is at the sword's origin and its blade along +Z, like the boxes.</summary>
        void UpdateSwordModel()
        {
            int piece = (int)Pawn.Piece;
            if (piece == modelPiece && SwordLookTeam == modelTeam) return;
            modelPiece = piece; modelTeam = SwordLookTeam; modelBuild++;
            if (swordModel != null) Destroy(swordModel.gameObject);
            swordModel = null;
            var prefab = Resources.Load<GameObject>("SwordFightSwords/" + Pawn.Piece);
            foreach (var r in new[] { blade, guard, grip }) if (r != null) r.forceRenderingOff = prefab != null;
            hiltBack = .1f;
            if (prefab == null) return;
            // 10-25 (승규 님: "퀸 나이트 폰 빼고 나머지 기물의 칼 크기가 커"): the king's, rook's and bishop's a size smaller.
            // Then ("왕이 몸에 비해 칼이 좀 커"): the king's smaller again, 0.8 → 0.65.
            // 10-27 ("킹이랑 룩 칼 좀 더 키워줘" → "룩이 아니라 비숍, 비숍 킹을 1.5배로"): king 0.65 → 1, bishop 0.8 → 1.2,
            // rook back to 0.8. Then ("퀸은 10퍼 작게"): queen 0.9.
            float size = Pawn.Piece == PieceKind.King ? 1f : Pawn.Piece == PieceKind.Bishop ? 1.2f : Pawn.Piece == PieceKind.Rook ? .8f
                : Pawn.Piece == PieceKind.Queen ? .9f : 1f;
            // The models' guards sit 0.09 m up from their origin and their pommels hang below it (the model stands along
            // +Y). 10-25 (승규 님: "손잡이 위쪽에 잡아져 있어" → "모든 기물이 손잡이 부분 잡게"): the hand held it just under
            // the guard; now the model goes up the blade so the hand is round the grip's lower end, just above the pommel's
            // knob (about 6 cm), with only the knob behind it - so the hilt no longer pokes into a wide body's side.
            var mf = prefab.GetComponentInChildren<MeshFilter>();
            float below = mf != null && mf.sharedMesh != null ? -mf.sharedMesh.bounds.min.y : .1f;
            // 10-27: the rook's new fortress sword ends in a tower base ~12 cm tall, so its hand sits just above that.
            float knob = Pawn.Piece == PieceKind.Rook ? .14f : .095f;
            float up = Mathf.Max(0f, below - knob) * size;
            hiltBack = Mathf.Max(.06f, below * size - up);
            modelLift = up; modelScale = size;
            var go = Instantiate(prefab, sword, false);
            go.name = "Sword model";
            // Exported standing up (+Y): turned so the blade runs along the sword's +Z.
            go.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, up), Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = Vector3.one * size;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            // 10-29 (승규 님: "백팀과 흑팀 칼을 다르게"): a piece with a painted look per team (Resources/SwordFightSwords/
            // Team/<piece>_<White|Black>, baked in Blender) wears it; the rest stay plain grey.
            var look = Resources.Load<Material>("SwordFightSwords/Team/" + Pawn.Piece + (modelTeam == Gameplay.Teams.Black ? "_Black" : "_White"));
            if (modelMaterial != null) Destroy(modelMaterial);
            if (look != null) modelMaterial = new Material(look);
            else
            {
                modelMaterial = new Material(LitMaterials.Lit) { color = ModelPlain };
                LitMaterials.SetSmoothness(modelMaterial, .45f);
            }
            modelBase = modelMaterial.color;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = sword.gameObject.layer;
            // No shadows received: the fine engraved mesh speckled with shadow acne under the match light.
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterial = modelMaterial; r.receiveShadows = false; }
            swordModel = go.transform;
        }
        void PoseHolstered()
        {
            // No collider or dynamic forces while holstered, including ragdoll/get-up.
            var hip = Pawn.Hips.transform;
            Vector3 point = hip.TransformPoint(new Vector3(.28f, -.06f, -.02f));
            Vector3 direction = AboveFloor(point, hip.TransformDirection(new Vector3(.18f, -.35f, -1f)), .05f);
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
        /// <summary>10-29 (승규 님: "상대맞출 때 sfx … 모든 기물 동일하게"): this piece's hit flash set, by the sword that cut it
        /// here or by the wire. A jump up to a hit's flash (a click cut's .14, a drag cut's .16; the board's ring reads it
        /// the same way) is a new hit, so every machine plays the one sword-hit sound for every piece at its chest.</summary>
        void FlashHit(float flash)
        {
            if (Alive && flash >= .13f && flash > HitFlash + .02f)
            {
                SwordFightSkillSfx.Moment("SwordHit", Pawn.bodies[(int)BodyId.Chest].position, Skills, Random.Range(.94f, 1.06f));
                Cut?.Invoke(this);
            }
            HitFlash = flash;
        }

        public void ApplyRemote(bool alive, bool drawn, float protection, Vector3 offset, Quaternion rotation, float respawn, float flash, bool classic = false)
        {
            Authority = false; bladeCollider.enabled = false; SwordBody.isKinematic = true;
            gripJoint.xMotion = gripJoint.yMotion = gripJoint.zMotion = ConfigurableJointMotion.Free;
            bool changed = Alive != alive;
            Alive = alive; Drawn = drawn && alive; ClassicControls = classic; Protection = protection; RespawnSeconds = respawn; FlashHit(flash);
            remoteOffset = offset; remoteRotation = rotation;
            if (changed) foreach (var r in Pawn.GetComponentsInChildren<Renderer>(true)) r.enabled = alive;
        }
        static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
        void PoseAfterSkeleton()
        {
            if (sword == null || Pawn == null || !Alive || !Authority) return;
            if (Skills != null && Skills.Blade is Vector3 skillBlade)
            {
                // 10-26: a skill's sword is held in the drawn mitten too. Its motion turns the chest, and the piece is drawn
                // with half the chest's turn, so the physical hand is off the mitten by then (the click sword does this).
                if (Skills.SwordAt == null)
                {
                    Vector3 grip = ClickGrip() + skillBlade * Skills.Reach01;
                    sword.SetPositionAndRotation(grip, Quaternion.LookRotation(AboveFloor(grip, skillBlade)));
                }
                return;
            }
            if (!ClassicControls) return;
            PoseClickSword();
        }

        /// <summary>10-26: the line a skill hands the sword back along as it ends - the click guard's (classic controls) or
        /// the holster's - so the sword does not jump when the match takes it back.</summary>
        public Vector3 HomeBlade => ClassicControls ? ClickBladeDirection()
            : Pawn.Hips.transform.TransformDirection(new Vector3(.18f, -.35f, -1f)).normalized;

        void OnDestroy()
        {
            if (visualSync != null) { visualSync.Posed -= PoseAfterSkeleton; visualSync.Posed -= PoseOffHand; }
            DestroyOffSword();
            if (bladeCollider != null) RagdollPawn.ColliderOwner.Remove(bladeCollider);
            if (Pawn != null) Pawn.PoseOverride = null;
            if (bladeMaterial != null) Destroy(bladeMaterial);
            if (hiltMaterial != null) Destroy(hiltMaterial);
            if (modelMaterial != null) Destroy(modelMaterial);
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
