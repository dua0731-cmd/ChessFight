using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Added only by SwordFightGame. Shared pawn controls and tuning remain untouched.
    [DefaultExecutionOrder(-60)]
    public sealed class SwordFightPawn : MonoBehaviour
    {
        public const float Windup = .16f, ActiveTime = .20f, Recovery = .34f;
        public const float SwingDuration = Windup + ActiveTime + Recovery;
        public RagdollPawn Pawn { get; private set; }
        public ulong Id { get; private set; }
        public int Slot { get; private set; }
        public bool Bot { get; private set; }
        public bool Alive { get; private set; } = true;
        public bool Authority { get; set; } = true;
        public float Protection { get; private set; }
        public float SwingAge { get; private set; } = SwingDuration;
        public int Swings { get; private set; }
        public int HitsLanded { get; private set; }
        public uint SwingSerial { get; private set; }
        public Vector3 SwingDirection { get; private set; } = Vector3.forward;
        public bool Attacking => Alive && SwingAge < SwingDuration;
        public bool WeaponVisible => sword != null && sword.gameObject.activeInHierarchy && blade.enabled;
        public bool CanAttack => Alive && Pawn.State == PawnState.Active && !Attacking;
        public float RespawnSeconds { get; set; }
        public float HitFlash { get; private set; }
        public Vector3 BladeRoot => Grip();
        public Vector3 BladeTip => Grip() + BladeDirection() * .88f;
        readonly HashSet<SwordFightPawn> hitThisSwing = new HashSet<SwordFightPawn>();
        readonly Collider[] overlaps = new Collider[128];
        Transform sword;
        Renderer blade;
        Material bladeMaterial, hiltMaterial;
        Vector3 previousGrip, previousTip;
        bool wasActive;
        float inputBuffer;
        Vector3 bufferedAim;

        public void Initialize(ulong id, int slot, bool bot)
        {
            Pawn = GetComponent<RagdollPawn>(); Id = id; Slot = slot; Bot = bot;
            Pawn.PoseOverride = SwordPose;
            var root = new GameObject("Sword (mode visual)");
            root.transform.SetParent(transform); sword = root.transform;
            bladeMaterial = Material(new Color(.79f, .88f, .96f));
            hiltMaterial = Material(new Color(.85f, .60f, .21f));
            blade = Part("Rounded blade", new Vector3(0, 0, .49f), new Vector3(.105f, .045f, .76f), bladeMaterial);
            Part("Guard", new Vector3(0, 0, .09f), new Vector3(.24f, .07f, .07f), hiltMaterial);
            Part("Grip", new Vector3(0, 0, -.015f), new Vector3(.065f, .07f, .16f), hiltMaterial);
        }

        static Material Material(Color color) => new Material(Shader.Find("Standard")) { color = color };
        Renderer Part(string label, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label; part.transform.SetParent(sword, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            var c = part.GetComponent<Collider>(); c.enabled = false; Destroy(c);
            var r = part.GetComponent<Renderer>(); r.sharedMaterial = material; return r;
        }

        // LMB is intercepted before the shared pawn sees it. RMB/E/Q/F do nothing for a pawn.
        public void SetInput(PawnInput raw)
        {
            if (!Authority || !Alive) return;
            if (raw.shove) { inputBuffer = .13f; bufferedAim = raw.aim; }
            Pawn.SetInput(new PawnInput { move = raw.move, jump = raw.jump, sprint = raw.sprint, aim = raw.aim });
        }

        public void StopCombat()
        {
            inputBuffer = 0; SwingAge = SwingDuration; wasActive = false;
            Pawn.SetInput(default);
        }

        void FixedUpdate()
        {
            if (Pawn == null || !Authority || !Alive) return;
            float dt = Time.fixedDeltaTime;
            Protection = Mathf.Max(0, Protection - dt);
            HitFlash = Mathf.Max(0, HitFlash - dt);
            if (inputBuffer > 0 && CanAttack) BeginSwing(bufferedAim);
            inputBuffer = Mathf.Max(0, inputBuffer - dt);
            if (Pawn.State != PawnState.Active) { SwingAge = SwingDuration; wasActive = false; }
            if (!Attacking) return;
            SwingAge += dt;
            bool active = SwingAge >= Windup && SwingAge <= Windup + ActiveTime;
            Vector3 grip = Grip(), tip = BladeTip;
            if (active)
            {
                if (!wasActive) { previousGrip = grip; previousTip = tip; }
                // Sweep the blade between physics steps; one ragdoll can contribute many colliders.
                for (int s = 0; s <= 3; s++)
                    Detect(Vector3.Lerp(previousGrip, grip, s / 3f), Vector3.Lerp(previousTip, tip, s / 3f));
            }
            previousGrip = grip; previousTip = tip; wasActive = active;
        }

        void BeginSwing(Vector3 aim)
        {
            aim.y = 0; SwingDirection = aim.sqrMagnitude > .01f ? aim.normalized : Pawn.Facing;
            SwingAge = 0; SwingSerial++; Swings++; inputBuffer = 0;
            Protection = 0; hitThisSwing.Clear(); wasActive = false;
        }

        void Detect(Vector3 start, Vector3 end)
        {
            int count = Physics.OverlapCapsuleNonAlloc(start, end, .15f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!RagdollPawn.ColliderOwner.TryGetValue(overlaps[i], out var target) || target == Pawn || target.Team == Pawn.Team) continue;
                var fighter = target.GetComponent<SwordFightPawn>();
                if (fighter == null || !fighter.Alive || fighter.Protection > 0 || !hitThisSwing.Add(fighter)) continue;
                Vector3 away = target.Hips.position - Pawn.Hips.position; away.y = 0;
                Vector3 push = (SwingDirection * .6f + away.normalized * .4f).normalized;
                // Follow-up swings may reset the knockdown; no stand-up invulnerability is imposed.
                target.TakeHit(push * 3.4f + Vector3.up * 1.05f, .65f, 0, true);
                fighter.SwingAge = SwingDuration; fighter.inputBuffer = 0; fighter.HitFlash = .14f;
                HitsLanded++;
            }
        }

        Vector3 BladeDirection()
        {
            if (!Alive || Pawn.State != PawnState.Active)
                return Pawn.bodies[(int)BodyId.HandR].rotation * Vector3.right;
            float angle = -25f;
            if (Attacking)
            {
                if (SwingAge < Windup) angle = Mathf.Lerp(-25, -85, SwingAge / Windup);
                else if (SwingAge < Windup + ActiveTime) angle = Mathf.Lerp(-85, 85, (SwingAge - Windup) / ActiveTime);
                else angle = Mathf.Lerp(85, -25, (SwingAge - Windup - ActiveTime) / Recovery);
            }
            Vector3 idle = Pawn.Facing;
            // ApplyNetworkPose restores body rotations, not the controller's private facing.
            if (Pawn.NetworkPuppet)
            {
                idle = Pawn.Hips.transform.forward; idle.y = 0;
                idle = idle.sqrMagnitude > .001f ? idle.normalized : SwingDirection;
            }
            Vector3 forward = Attacking ? SwingDirection : idle;
            // The cutting arc passes near knee height, low enough for prone bodies without
            // burying the tip under the floor and shortening the usable forward reach.
            return (Quaternion.AngleAxis(angle, Vector3.up) * forward + Vector3.up * (Attacking ? -.32f : .35f)).normalized;
        }
        Vector3 Grip() => Pawn.bodies[(int)BodyId.HandR].position;
        Quaternion? SwordPose(int part)
        {
            if (!Alive || Pawn.State != PawnState.Active) return null;
            if (part == (int)BodyId.Chest && Attacking)
            {
                float turn = Mathf.Clamp(Vector3.SignedAngle(Pawn.Facing, SwingDirection, Vector3.up), -65, 65);
                float sweep = Mathf.Sin(Mathf.Clamp01((SwingAge - Windup) / ActiveTime) * Mathf.PI);
                return Quaternion.Euler(-8f * sweep, turn * .65f, -9f * sweep);
            }
            if (part != (int)BodyId.ArmR) return null;
            Vector3 local = Pawn.bodies[(int)BodyId.Chest].transform.InverseTransformDirection(BladeDirection());
            return Quaternion.FromToRotation(Vector3.right, local);
        }

        void LateUpdate()
        {
            if (sword == null || Pawn == null) return;
            sword.gameObject.SetActive(Alive);
            if (!Alive) return;
            sword.SetPositionAndRotation(Grip(), Quaternion.LookRotation(BladeDirection()));
            bladeMaterial.color = Attacking && SwingAge >= Windup && SwingAge <= Windup + ActiveTime
                ? new Color(1f, .89f, .42f) : new Color(.79f, .88f, .96f);
        }

        public void Eliminate()
        {
            Alive = false; inputBuffer = 0; SwingAge = SwingDuration; wasActive = false;
            Pawn.SetInput(default); Pawn.SetNetworkPuppet(true);
            foreach (var r in Pawn.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
        public void Respawn(Vector3 floor, Vector3 facing, bool authority)
        {
            Authority = authority;
            // The shared puppet exit clears velocity before switching off kinematic mode.
            // Prepare this mode's bodies first without changing the shared controller.
            if (Pawn.NetworkPuppet) foreach (var rb in Pawn.bodies) rb.isKinematic = false;
            Pawn.SetNetworkPuppet(false);
            Pawn.Teleport(floor + Vector3.up * (Pawn.standHeight + .02f), facing);
            Pawn.SetNetworkPuppet(!authority);
            Alive = true; Protection = 1.25f; SwingAge = SwingDuration; inputBuffer = 0; hitThisSwing.Clear();
            foreach (var r in Pawn.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
        }
        public void ApplyRemote(bool alive, float protection, float age, uint serial, Vector3 direction, float respawn)
        {
            Authority = false;
            if (Alive != alive)
            {
                Alive = alive;
                foreach (var r in Pawn.GetComponentsInChildren<Renderer>(true)) r.enabled = alive;
            }
            Protection = protection; SwingAge = age; SwingSerial = serial; SwingDirection = direction; RespawnSeconds = respawn;
        }
        void OnDestroy()
        {
            if (Pawn != null) Pawn.PoseOverride = null;
            if (bladeMaterial != null) Destroy(bladeMaterial);
            if (hiltMaterial != null) Destroy(hiltMaterial);
        }
    }
}
