using UnityEngine;

namespace ChessFight.ProtectKing
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerIdentity))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public StageMap map;
        public float moveSpeed = 6f;
        public float jumpHeight = 1.6f;
        public float gravity = -24f;
        public PlayerIdentity Identity { get; private set; }
        public bool IsLocal { get; set; }
        public bool ExternalControl { get; set; }
        public bool Simulate { get; set; } = true;
        public void ClearInput() { moveInput = Vector2.zero; jumpRequested = false; }
        public bool Grounded
        {
            get
            {
                if (controller == null || verticalSpeed > 0) return false;
                if (controller.isGrounded) return true;
                float radius = controller.radius * .85f;
                var origin = transform.position + Vector3.up * (radius + .2f);
                return Physics.SphereCast(origin, radius, Vector3.down, out var hit, .32f,
                    1 << 0, QueryTriggerInteraction.Ignore) && hit.normal.y >= .65f;
            }
        }
        CharacterController controller;
        Vector2 moveInput;
        bool jumpRequested;
        float verticalSpeed;
        Vector3 pushVelocity;
        float pushDamping = 12;
        ObstacleSurface obstacleSurface;
        WeightedBridge bridgeSurface;
        Transform support;
        Vector3 supportPoint;
        Vector3 supportWorld;
        float lastGrounded = -10;
        float lastJump = -10;
        public float ViewYaw { get; set; }
        public bool CanReceiveObstacle => isActiveAndEnabled && Simulate && map != null &&
            map.match != null && map.match.HasAuthority && map.match.IsRunning;

        public void AddObstacleImpulse(Vector3 velocity)
        {
            if (!CanReceiveObstacle) return;
            pushVelocity = Vector3.ClampMagnitude(pushVelocity + Vector3.ProjectOnPlane(velocity, Vector3.up), 22);
            pushDamping = 8;
            if (velocity.y > 0)
            {
                verticalSpeed = Mathf.Max(verticalSpeed, velocity.y);
                support = null; lastGrounded = -10;
            }
        }

        public void LaunchFromObstacle(Vector3 velocity)
        {
            if (!CanReceiveObstacle) return;
            pushVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            pushDamping = 2;
            verticalSpeed = Mathf.Max(0, velocity.y);
            support = null; obstacleSurface = null; bridgeSurface = null;
            lastGrounded = -10; lastJump = Time.time; jumpRequested = false;
        }

        void Awake()
        {
            Identity = GetComponent<PlayerIdentity>();
            controller = GetComponent<CharacterController>();
        }

        public void SetInput(Vector2 movement, bool jump)
        {
            moveInput = Vector2.ClampMagnitude(movement, 1);
            jumpRequested |= jump;
        }

        void Update()
        {
            if (map == null || !Simulate) return;
            if (!map.match.IsRunning) { moveInput = Vector2.zero; jumpRequested = false; return; }
            var before = transform.position;
            Vector3 surfaceVelocity = Vector3.zero;
            if (Grounded)
            {
                if (obstacleSurface != null) surfaceVelocity += obstacleSurface.Apply(this);
                if (bridgeSurface != null) surfaceVelocity += bridgeSurface.SlideVelocity;
            }
            obstacleSurface = null;
            bridgeSurface = null;
            if (support != null && Grounded)
            {
                var nextPoint = support.TransformPoint(supportPoint);
                controller.Move(nextPoint - supportWorld);
            }
            support = null;
            if (Grounded)
            {
                lastGrounded = Time.time;
                if (verticalSpeed < 0) verticalSpeed = -2;
            }
            if ((IsLocal || ExternalControl) && jumpRequested && Time.time - lastGrounded < .12f &&
                Time.time - lastJump > .2f)
            {
                verticalSpeed = Mathf.Sqrt(-2 * gravity * jumpHeight);
                lastJump = Time.time;
                lastGrounded = -10;
            }
            jumpRequested = false;
            verticalSpeed = Mathf.Max(verticalSpeed + gravity * Time.deltaTime, -28);
            var local = (IsLocal || ExternalControl) ? new Vector3(moveInput.x, 0, moveInput.y) : Vector3.zero;
            var horizontal = Quaternion.Euler(0, ViewYaw, 0) * local * moveSpeed;
            controller.Move((horizontal + surfaceVelocity + Vector3.up * verticalSpeed + pushVelocity) * Time.deltaTime);
            pushVelocity = Vector3.MoveTowards(pushVelocity, Vector3.zero, pushDamping * Time.deltaTime);
            if (support != null)
            {
                supportPoint = support.InverseTransformPoint(transform.position);
                supportWorld = support.TransformPoint(supportPoint);
            }
            map.CheckTravel(this, before, transform.position);
        }

        public void Teleport(Vector3 position)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            verticalSpeed = 0;
            pushVelocity = Vector3.zero;
            pushDamping = 12;
            obstacleSurface = null;
            bridgeSurface = null;
            moveInput = Vector2.zero;
            jumpRequested = false;
            support = null;
            lastGrounded = -10;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > .5f)
            {
                obstacleSurface = hit.collider.GetComponentInParent<ObstacleSurface>();
                bridgeSurface = hit.collider.GetComponentInParent<WeightedBridge>();
                var rigidbody = hit.collider.attachedRigidbody;
                if (rigidbody != null && rigidbody.isKinematic) support = rigidbody.transform;
            }
            else
            {
                var impact = hit.collider.GetComponentInParent<ObstacleImpact>();
                if (impact != null) { impact.Hit(this, hit.normal); return; }
            }
            var motion = hit.collider.GetComponentInParent<ObstacleMotion>();
            if (motion == null) return;
            if (hit.normal.y > .5f) support = motion.transform;
            else if (motion.shove && motion.PointVelocity(hit.point).sqrMagnitude > .1f)
            {
                var outward = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
                pushVelocity = outward.normalized * 4f;
            }
        }
    }
}
