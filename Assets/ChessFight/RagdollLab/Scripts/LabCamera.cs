using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Shared camera that frames every player, plus a free-fly mode (F) for inspecting poses.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class LabCamera : MonoBehaviour
    {
        public LabGame game;
        public float yaw;
        public float pitch = 24f;
        public float minDistance = 2.2f;
        public float maxDistance = 28f;
        public float mouseSensitivity = 2.2f;
        public bool freeMode;

        /// <summary>Online play: follow only this pawn instead of framing every local player.</summary>
        public RagdollPawn soloTarget;

        Vector3 focus, focusVelocity;
        float distance = 6f;
        bool initialized;
        float freeYaw, freePitch;

        public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        public Vector3 FlatRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        public void AddYaw(float degrees) => yaw += degrees;

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

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            bool mouseLook = Cursor.lockState == CursorLockMode.Locked && (game == null || !game.PanelOpen);
            if (freeMode)
            {
                FreeFly(dt, mouseLook);
                return;
            }
            if (mouseLook) yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;

            bool any = false;
            Bounds bounds = default;
            if (soloTarget != null) Include(ref bounds, ref any, soloTarget.Hips.position);
            else if (game != null)
            {
                foreach (var slot in game.players)
                {
                    if (slot.pawn == null) continue;
                    Include(ref bounds, ref any, slot.pawn.Hips.position);
                }
            }
            if (!any)
                foreach (var pawn in RagdollPawn.All) Include(ref bounds, ref any, pawn.Hips.position);
            if (!any) return;

            Vector3 target = bounds.center + Vector3.up * 0.2f;
            float spread = Mathf.Max(bounds.size.x, bounds.size.z, bounds.size.y * 1.5f);
            float want = Mathf.Clamp(minDistance + spread * 1.15f, minDistance, maxDistance);
            if (!initialized)
            {
                focus = target;
                distance = want;
                initialized = true;
            }
            focus = Vector3.SmoothDamp(focus, target, ref focusVelocity, 0.12f, Mathf.Infinity, dt);
            distance = Mathf.Lerp(distance, want, 1f - Mathf.Exp(-3f * dt));
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
        }

        static void Include(ref Bounds bounds, ref bool any, Vector3 point)
        {
            if (!any)
            {
                bounds = new Bounds(point, Vector3.zero);
                any = true;
            }
            else bounds.Encapsulate(point);
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
