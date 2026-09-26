using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Third-person orbit camera for one player. It sits behind that player's own pawn, and the mouse
    /// (or the right stick) swings it around; the wheel zooms. Local two-player play gives each player
    /// one of these on half the screen (LabGame), and online play follows the pawn this PC controls
    /// (soloTarget). F toggles a free-fly camera for inspecting poses.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class LabCamera : MonoBehaviour
    {
        public LabGame game;
        [Tooltip("The LabGame player slot this camera follows.")]
        public int playerIndex;
        public float yaw;
        public float pitch = 14f;
        public float distance = 3.6f;
        public float minDistance = 1.4f;
        public float maxDistance = 9f;
        public float minPitch = -35f;
        public float maxPitch = 70f;
        [Tooltip("Aim point above the hips, about the top of the pawn's head.")]
        public float lookHeight = 0.6f;
        public float mouseSensitivity = 2.2f;
        public float baseFov = 60f;
        [Tooltip("Extra field of view at full sprint, for a sense of speed.")]
        public float sprintFov = 8f;
        public float collisionRadius = 0.2f;
        [Tooltip("How softly the camera follows across the ground (s). Higher is steadier and floatier.")]
        public float followTime = 0.12f;
        [Tooltip("How softly it follows up and down (s): long, so steps and small hops do not bounce the view.")]
        public float heightTime = 0.3f;
        public bool freeMode;

        /// <summary>Online play: follow this pawn (the one this PC controls) instead of a local slot's.</summary>
        public RagdollPawn soloTarget;

        Camera cam;
        Vector3 focus, focusVelocity, lastWant, travel;
        float heightVelocity, shown, fovKick, lastFacingYaw, climbTurn;
        bool wasClimbing;
        bool initialized;
        RagdollPawn followed;
        float freeYaw, freePitch;
        readonly RaycastHit[] hits = new RaycastHit[16];

        public Camera Cam => cam != null ? cam : cam = GetComponent<Camera>();
        public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        public Vector3 FlatRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        /// <summary>Where the player aims: straight along the view, pitch included (the hook, the charges).</summary>
        public Vector3 AimForward => freeMode ? transform.forward : Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;

        /// <summary>The pawn this camera is following, or null.</summary>
        public RagdollPawn Target
        {
            get
            {
                if (soloTarget != null) return soloTarget;
                if (game != null && playerIndex >= 0 && playerIndex < game.players.Length) return game.players[playerIndex].pawn;
                return null;
            }
        }

        public void AddYaw(float degrees) => yaw += degrees;

        /// <summary>
        /// Recordings step game time a fixed 1/30 s per frame (Time.captureFramerate) while real time races
        /// ahead or crawls, so the camera must smooth on game time there or it trails the pawn by metres.
        /// In play it smooths on real time, so slow motion (T) does not slow the camera down.
        /// </summary>
        public static bool GameTimeClock { get; set; }

        public void AddPitch(float degrees) => pitch = Mathf.Clamp(pitch + degrees, minPitch, maxPitch);

        public void Zoom(float steps) => distance = Mathf.Clamp(distance * Mathf.Pow(0.88f, steps), minDistance, maxDistance);

        public void SetFreeMode(bool on)
        {
            if (on && !freeMode)
            {
                Vector3 e = transform.eulerAngles;
                freeYaw = e.y;
                freePitch = e.x > 180f ? e.x - 360f : e.x;
            }
            freeMode = on;
        }

        /// <summary>The mouse belongs to whichever slot plays on keyboard and mouse.</summary>
        bool MouseDriven =>
            game == null || playerIndex < 0 || playerIndex >= game.players.Length
            || game.players[playerIndex].device == LabDevice.KeyboardMouse;

        void LateUpdate()
        {
            float dt = GameTimeClock ? Time.deltaTime : Time.unscaledDeltaTime;
            bool mouseLook = MouseDriven && Cursor.lockState == CursorLockMode.Locked && (game == null || !game.PanelOpen);
            if (freeMode)
            {
                FreeFly(dt, mouseLook);
                return;
            }
            if (mouseLook)
            {
                yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
                AddPitch(-Input.GetAxisRaw("Mouse Y") * mouseSensitivity);
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) Zoom(wheel);
            }

            var pawn = Target;
            if (pawn == null && RagdollPawn.All.Count > 0) pawn = RagdollPawn.All[0];
            if (pawn == null) return;

            // Not the hips: they sway sideways on every stride, bob on every step and flail when the
            // pawn tumbles, and a camera glued to them shook with all of it. Not the locomotion
            // anchor either: on a turn or a reversal it runs up to 0.6 m ahead of the body, and a
            // camera on it slid the pawn off the middle of the screen and back - that was the
            // "swinging around its root". CameraPoint is the body's centre of mass, at standing
            // height over the floor.
            Vector3 want = pawn.CameraPoint + Vector3.up * lookHeight;
            if (!initialized || pawn != followed || (want - focus).sqrMagnitude > 36f)
            {
                // First frame, a new pawn, or a respawn: cut rather than swoop across the arena.
                if (!initialized || pawn != followed) yaw = Mathf.Atan2(pawn.Facing.x, pawn.Facing.z) * Mathf.Rad2Deg;
                focus = lastWant = want;
                focusVelocity = travel = Vector3.zero;
                heightVelocity = 0f;
                climbTurn = 0f;
                wasClimbing = false;
                shown = distance;
                initialized = true;
                followed = pawn;
            }
            // On a wall the camera turns with the pawn. Its keys are read against the camera, so when
            // the pawn went round a corner and the camera stayed put, "right" became "into the new
            // wall" and holding it climbed up instead of carrying on round.
            float facingYaw = Mathf.Atan2(pawn.Facing.x, pawn.Facing.z) * Mathf.Rad2Deg;
            if (pawn.Climbing && wasClimbing) climbTurn += Mathf.DeltaAngle(lastFacingYaw, facingYaw);
            wasClimbing = pawn.Climbing;
            lastFacingYaw = facingYaw;
            float turnNow = climbTurn * (1f - Mathf.Exp(-12f * dt));
            yaw += turnNow;
            climbTurn -= turnNow;

            // Aim a little ahead along the (smoothed) travel: that takes back half the lag a soft
            // follow builds up at speed without making it any stiffer. Measured on a model of this
            // filter: 0.3 m behind at a run, 0.5 m at a sprint, under 10 cm of overshoot on a stop.
            if (dt > 1e-4f)
            {
                Vector3 moved = (want - lastWant) / dt;
                moved.y = 0f;
                travel = Vector3.Lerp(travel, moved, 1f - Mathf.Exp(-8f * dt));
            }
            lastWant = want;
            // Softer on a tumble and on a remote pawn, whose point still carries some body sway.
            // A dive is on purpose and goes where it was aimed: follow it as tightly as a run.
            bool limp = pawn.State == PawnState.Ragdoll && !pawn.Diving;
            float across = followTime * (limp ? 1.5f : pawn.NetworkPuppet ? 1.25f : 1f);
            Vector3 lead = want + travel * (across * 0.5f);
            Vector3 flat = Vector3.SmoothDamp(new Vector3(focus.x, 0f, focus.z), new Vector3(lead.x, 0f, lead.z),
                ref focusVelocity, across, Mathf.Infinity, dt);
            float y = Mathf.SmoothDamp(focus.y, want.y, ref heightVelocity, limp ? heightTime * 1.3f : heightTime, Mathf.Infinity, dt);
            focus = new Vector3(flat.x, y, flat.z);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rot * Vector3.back;
            // Pull in in front of walls and ease back out. Pawns never block the view:
            // with a crowd around, the camera would otherwise dive into someone's head.
            float allowed = distance;
            int n = Physics.SphereCastNonAlloc(focus, collisionRadius, back, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance <= 0f || RagdollPawn.ColliderOwner.ContainsKey(h.collider)) continue;
                allowed = Mathf.Min(allowed, h.distance);
            }
            // In quickly (a wall must not end up between camera and pawn), out slowly. A hard snap in
            // made the view pump whenever the probe grazed something on and off.
            shown = Mathf.Lerp(shown, allowed, 1f - Mathf.Exp((allowed < shown ? -25f : -4f) * dt));
            transform.SetPositionAndRotation(focus + back * Mathf.Max(0.3f, shown), rot);

            if (Cam != null)
            {
                var p = pawn.P;
                float span = Mathf.Max(0.1f, p.sprintSpeed - p.moveSpeed);
                // From the smoothed travel, not the hips' own speed, which pulses with every stride.
                float kick = sprintFov * Mathf.Clamp01((travel.magnitude - p.moveSpeed) / span);
                fovKick = Mathf.Lerp(fovKick, kick, 1f - Mathf.Exp(-3f * dt));
                Cam.fieldOfView = baseFov + fovKick;
            }
        }

        void FreeFly(float dt, bool mouseLook)
        {
            if (mouseLook)
            {
                freeYaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
                freePitch = Mathf.Clamp(freePitch - Input.GetAxisRaw("Mouse Y") * mouseSensitivity, -85f, 85f);
            }
            Quaternion rot = Quaternion.Euler(freePitch, freeYaw, 0f);
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) move += Vector3.back;
            if (Input.GetKey(KeyCode.D)) move += Vector3.right;
            if (Input.GetKey(KeyCode.A)) move += Vector3.left;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move += Vector3.down;
            float speed = Input.GetKey(KeyCode.LeftShift) ? 18f : 6f;
            transform.SetPositionAndRotation(transform.position + rot * move * speed * dt, rot);
        }
    }
}
