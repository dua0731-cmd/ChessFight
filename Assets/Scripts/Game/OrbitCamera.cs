using UnityEngine;

namespace ChessFight.Game
{
    // Third-person mouse camera for offline playtests of climbing scenes (Queen of
    // the Hill). The mouse swings it round the followed character, the wheel zooms,
    // and its view is also the aim (the grappling hook is thrown along AimForward).
    // Clicking the game view captures the mouse; Esc lets it go.
    //
    // V toggles a free-flying camera for looking round the map: WASD, Space / C up
    // and down, Shift fast. The character gets no input while it flies.
    //
    // It only knows a Transform to follow, never the character type, so it works
    // with the capsule stand-in and the ragdoll alike.
    [DefaultExecutionOrder(150)]
    public sealed class OrbitCamera : MonoBehaviour
    {
        [SerializeField] float distance = 5f;
        [SerializeField] float minDistance = 1.5f;
        [SerializeField] float maxDistance = 14f;
        [SerializeField] float minPitch = -40f;
        [SerializeField] float maxPitch = 75f;
        [SerializeField] float mouseSensitivity = 2.2f;
        [Tooltip("Aim point above the followed transform (the hips), about the top of the head.")]
        [SerializeField] float lookHeight = 0.6f;
        [Tooltip("Sideways offset over the right shoulder, so the view up a wall is not the back of the head.")]
        [SerializeField] float shoulder = 0.45f;
        [SerializeField] float followTime = 0.1f;
        [SerializeField] float heightTime = 0.25f;
        [SerializeField] float collisionRadius = 0.2f;
        [SerializeField] float flySpeed = 12f;
        [SerializeField] float flyFastSpeed = 45f;

        Transform target, targetRoot;
        float yaw, pitch = 15f, shown;
        Vector3 focus, focusVelocity;
        float heightVelocity;
        bool initialized;
        readonly RaycastHit[] hits = new RaycastHit[16];

        public bool FreeFly { get; private set; }
        public float Yaw => yaw;

        // Where the player aims: along the view, pitch included.
        public Vector3 AimForward => FreeFly ? transform.forward : Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
        public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        public Vector3 FlatRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        // What to follow, and the root whose colliders never block the view (the
        // character's own body parts). Facing sets the starting yaw.
        public void Follow(Transform what, Transform ownRoot, Vector3 facing)
        {
            target = what;
            targetRoot = ownRoot;
            facing.y = 0f;
            if (facing.sqrMagnitude > 1e-4f) yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            initialized = false;
        }

        // A teleport: cut to the new place instead of swooping there.
        public void Cut() => initialized = false;

        void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
                else if (Cursor.lockState != CursorLockMode.Locked && Application.isFocused
                         && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
                    Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
                if (Input.GetKeyDown(KeyCode.V))
                {
                    FreeFly = !FreeFly;
                    if (!FreeFly) initialized = false;
                }
            }
            catch (System.InvalidOperationException) { }
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            bool look = Cursor.lockState == CursorLockMode.Locked;
            if (look)
            {
                yaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * mouseSensitivity, FreeFly ? -85f : minPitch, FreeFly ? 85f : maxPitch);
                float wheel = Input.mouseScrollDelta.y;
                if (!FreeFly && Mathf.Abs(wheel) > 0.01f)
                    distance = Mathf.Clamp(distance * Mathf.Pow(0.88f, wheel), minDistance, maxDistance);
            }
            if (FreeFly)
            {
                Fly(dt);
                return;
            }
            if (target == null) return;

            Vector3 want = target.position + Vector3.up * lookHeight;
            if (!initialized || (want - focus).sqrMagnitude > 64f)
            {
                focus = want;
                focusVelocity = Vector3.zero;
                heightVelocity = 0f;
                shown = distance;
                initialized = true;
            }
            Vector3 flat = Vector3.SmoothDamp(new Vector3(focus.x, 0f, focus.z), new Vector3(want.x, 0f, want.z),
                ref focusVelocity, followTime, Mathf.Infinity, dt);
            float y = Mathf.SmoothDamp(focus.y, want.y, ref heightVelocity, heightTime, Mathf.Infinity, dt);
            focus = new Vector3(flat.x, y, flat.z);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = focus + rot * Vector3.right * shoulder;
            Vector3 back = rot * Vector3.back;
            // Pull in in front of walls (quickly), ease back out (slowly). The character's
            // own colliders never count.
            float allowed = distance;
            int n = Physics.SphereCastNonAlloc(pivot, collisionRadius, back, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance <= 0f || (targetRoot != null && h.collider.transform.IsChildOf(targetRoot))) continue;
                allowed = Mathf.Min(allowed, h.distance);
            }
            shown = Mathf.Lerp(shown, allowed, 1f - Mathf.Exp((allowed < shown ? -25f : -4f) * dt));
            transform.SetPositionAndRotation(pivot + back * Mathf.Max(0.3f, shown), rot);
        }

        void Fly(float dt)
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) move += Vector3.back;
            if (Input.GetKey(KeyCode.D)) move += Vector3.right;
            if (Input.GetKey(KeyCode.A)) move += Vector3.left;
            Vector3 world = rot * move;
            if (Input.GetKey(KeyCode.Space)) world += Vector3.up;
            if (Input.GetKey(KeyCode.C)) world += Vector3.down;
            float speed = Input.GetKey(KeyCode.LeftShift) ? flyFastSpeed : flySpeed;
            transform.SetPositionAndRotation(transform.position + world * (speed * dt), rot);
        }
    }
}
