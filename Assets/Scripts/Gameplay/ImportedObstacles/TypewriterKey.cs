using ChessFight.Gameplay;
using UnityEngine;
namespace ChessFight.ProtectKing
{
    // StateDrivenMover: local prototype key follows foot pressure; no clock-driven animation.
    [DefaultExecutionOrder(-110), RequireComponent(typeof(Rigidbody))]
    public sealed class TypewriterKey : Obstacle, IMovingSurface, IJumpSpringSurface
    {
        [SerializeField, Min(0)] float pressDepth = .14f;
        [SerializeField, Min(.01f)] float pressSpeed = .8f;
        [SerializeField, Min(.01f)] float returnSpeed = 1.5f;
        [SerializeField, Min(0)] float jumpBonus = 1.2f;
        Rigidbody body; Vector3 rest; float depth, velocity, releaseUntil; bool pressed;
        public Quaternion DeltaRotation => Quaternion.identity;
        public Vector3 PointVelocity(Vector3 point) => transform.up * velocity;
        void Awake()
        {
            body = GetComponent<Rigidbody>(); rest = transform.localPosition;
            body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.Interpolate;
        }
        public void SetPressed(bool value) { pressed = value; }
        public float TakeJumpBonus(Vector3 point)
        {
            if (!isActiveAndEnabled || !pressed || depth < .015f || Time.time < releaseUntil) return 0;
            releaseUntil = Time.time + .15f; pressed = false;
            return Mathf.Clamp(jumpBonus, 0, 2);
        }
        void FixedUpdate()
        {
            float target = pressed && Time.time >= releaseUntil ? Mathf.Clamp(pressDepth, 0, .3f) : 0;
            float next = Mathf.MoveTowards(depth, target, (target > depth ? pressSpeed : returnSpeed) * Time.fixedDeltaTime);
            velocity = (depth - next) / Time.fixedDeltaTime; depth = next;
            Vector3 local = rest - Vector3.up * depth;
            body.MovePosition(transform.parent != null ? transform.parent.TransformPoint(local) : local);
        }
        public void ResetKey()
        {
            pressed = false; depth = velocity = releaseUntil = 0;
            if (body != null) body.position = transform.parent != null ? transform.parent.TransformPoint(rest) : rest;
        }
        void OnDisable() { if (body != null) ResetKey(); }
    }
}
