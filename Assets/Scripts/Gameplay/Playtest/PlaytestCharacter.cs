using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // STAND-IN. Lets map and obstacle work start before the ragdoll prefab lands
    // in this branch; replace it on PlaytestSpawner the moment the ragdoll
    // implements ICharacterDriver. It is a CharacterController, so it traverses
    // a course faithfully but only approximates hits, shoves and grabs.
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class PlaytestCharacter : MonoBehaviour, ICharacterDriver
    {
        [Header("Same numbers as the network PawnMotor, so both feel alike")]
        [SerializeField] float moveSpeed = 6f;
        [SerializeField] float jumpSpeed = 7f;
        [SerializeField] float gravity = 22f;
        [SerializeField] float turnSpeed = 15f;

        [Header("Stand-ins for the ragdoll's verbs")]
        [SerializeField] float shoveReach = 1.2f;
        [SerializeField] float shoveImpulse = 6f;
        [SerializeField] float grabReach = 1.6f;
        [SerializeField] float grabPull = 10f;
        [Tooltip("Share of an obstacle's surface speed turned into knockback.")]
        [SerializeField] float obstacleKnock = 1.2f;
        [SerializeField] float knockDecay = 12f;

        CharacterController body;
        CharacterCommand command;
        Vector3 velocity, knock;
        Rigidbody held;
        readonly Collider[] nearby = new Collider[24];
        readonly HashSet<Rigidbody> touched = new HashSet<Rigidbody>();

        public Transform FollowTarget => transform;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (body == null) body = gameObject.AddComponent<CharacterController>();
        }

        public void SetCommand(in CharacterCommand next)
        {
            command.Move = next.Move;
            command.Grab = next.Grab;
            // Edges survive until FixedUpdate consumes them, as in RagdollPawn.SetInput.
            command.Jump |= next.Jump;
            command.Shove |= next.Shove;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            body.enabled = false;
            // `position` is the ground; the controller's pivot sits at its middle.
            float lift = body.height * .5f - body.center.y + body.skinWidth;
            transform.SetPositionAndRotation(position + Vector3.up * lift, rotation);
            body.enabled = true;
            velocity = knock = Vector3.zero;
            held = null;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 move = Vector3.ClampMagnitude(new Vector3(command.Move.x, 0f, command.Move.z), 1f);

            if (body.isGrounded)
            {
                if (velocity.y < 0f) velocity.y = -2f;   // Stay glued to slopes and steps.
                if (command.Jump) velocity.y = jumpSpeed;
            }
            velocity.y -= gravity * dt;

            FeelObstacles();
            knock = Vector3.MoveTowards(knock, Vector3.zero, knockDecay * dt);

            Vector3 step = move * moveSpeed + knock;
            step.y = velocity.y;
            body.Move(step * dt);
            if (move.sqrMagnitude > 1e-3f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), turnSpeed * dt);

            if (command.Shove) Shove();
            UpdateGrab();
            command.Jump = command.Shove = false;
        }

        // A CharacterController is never pushed by other colliders, so without this
        // a spinner would sweep straight through it. The ragdoll does not need any
        // of this: it is built from rigidbodies and simply gets hit.
        void FeelObstacles()
        {
            Vector3 center = transform.TransformPoint(body.center);
            Vector3 half = transform.up * Mathf.Max(0f, body.height * .5f - body.radius);
            int count = Physics.OverlapCapsuleNonAlloc(center - half, center + half, body.radius + .05f,
                                                       nearby, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var rb = nearby[i].attachedRigidbody;
                if (rb == null || !rb.TryGetComponent(out Obstacle obstacle)) continue;
                Vector3 hit = obstacle.VelocityAt(center);
                hit.y = 0f;
                if (hit.sqrMagnitude < .25f) continue;
                hit *= obstacleKnock;
                if (hit.sqrMagnitude > knock.sqrMagnitude) knock = hit;
                velocity.y = Mathf.Max(velocity.y, 3f);
            }
        }

        void Shove()
        {
            touched.Clear();
            Vector3 at = transform.position + transform.forward * shoveReach;
            int count = Physics.OverlapSphereNonAlloc(at, shoveReach, nearby, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var rb = nearby[i].attachedRigidbody;
                if (rb == null || rb.isKinematic || !touched.Add(rb)) continue;
                rb.AddForce(transform.forward * shoveImpulse + Vector3.up * (shoveImpulse * .25f), ForceMode.VelocityChange);
            }
        }

        void UpdateGrab()
        {
            if (!command.Grab) { held = null; return; }
            if (held == null) held = Nearest();
            if (held == null) return;
            Vector3 hold = transform.position + transform.forward * 1.1f + Vector3.up * .3f;
            Vector3 toward = hold - held.worldCenterOfMass;
            if (toward.magnitude > grabReach * 2f) { held = null; return; }
            held.linearVelocity = toward * grabPull;
        }

        Rigidbody Nearest()
        {
            Vector3 at = transform.position + transform.forward * (grabReach * .5f);
            int count = Physics.OverlapSphereNonAlloc(at, grabReach, nearby, ~0, QueryTriggerInteraction.Ignore);
            Rigidbody best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var rb = nearby[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                float d = (rb.worldCenterOfMass - at).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = rb; }
            }
            return best;
        }

        // Walking into a loose box pushes it, as a body would.
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.rigidbody;
            if (rb == null || rb.isKinematic || hit.moveDirection.y < -.3f) return;
            var push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z) * 2f;
            rb.linearVelocity = new Vector3(push.x, rb.linearVelocity.y, push.z);
        }
    }
}
