using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// One independent grabbing hand. While grab is held it looks for the nearest collider within
    /// grabRadius, the puppet reaches toward it, and a FixedJoint attaches on contact.
    /// </summary>
    public class PawnHand : MonoBehaviour
    {
        public RagdollPawn owner;
        public Rigidbody body;
        public SphereCollider palm;

        FixedJoint joint;
        float regrabCooldown;
        readonly Collider[] buffer = new Collider[24];

        /// <summary>A grip within this distance below an obstacle's top counts as a ledge (generous: short arms).</summary>
        public const float LedgeDepth = 0.4f;

        public bool IsHolding => joint != null;
        public bool HoldingLedge { get; private set; }
        public Collider HeldCollider { get; private set; }
        public Rigidbody HeldBody { get; private set; }
        public bool HasReach { get; private set; }

        /// <summary>Surface direction at the grip, pointing from the surface toward the palm.
        /// Near-vertical y means a floor or a ceiling; near-zero y means a wall worth climbing.</summary>
        public Vector3 GripNormal { get; private set; } = Vector3.up;
        public Vector3 ReachPoint { get; private set; }
        public Vector3 Center => palm.transform.TransformPoint(palm.center);
        public float Radius => palm.radius * Mathf.Abs(palm.transform.lossyScale.x);

        public void Tick(bool want, RagdollParams p, float dt, bool wallOk = false)
        {
            regrabCooldown -= dt;
            HasReach = false;
            if (!want)
            {
                Release();
                return;
            }
            if (IsHolding)
            {
                if (HeldCollider == null || !HeldCollider.enabled)
                {
                    Release();
                    return;
                }
                bool onPawn = RagdollPawn.ColliderOwner.TryGetValue(HeldCollider, out var victim) && victim != owner;
                float limit = onPawn ? p.pawnGrabBreakForce : p.grabBreakForce;
                if (!Mathf.Approximately(joint.breakForce, limit))
                {
                    joint.breakForce = limit;
                    joint.breakTorque = limit;
                }
                // Tell the victim who has them and by which body, so a thrash can be aimed at the grip.
                if (onPawn) victim.NotifyHeld(owner, HeldCollider);
                return;
            }
            if (regrabCooldown > 0f) return;

            Vector3 center = Center;
            float radius = Radius;
            // While rising, walls only catch at the ledge, so a jump can reach the top edge.
            // Climbing is the one case where a hand SHOULD catch a flat wall while moving up.
            bool rising = !wallOk && owner.Hips.linearVelocity.y > 0.5f;
            int n = Physics.OverlapSphereNonAlloc(center, p.grabRadius, buffer, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Collider bestCollider = null;
            Vector3 bestPoint = center;
            for (int i = 0; i < n; i++)
            {
                var col = buffer[i];
                if (owner.Owns(col)) continue;
                if (col is MeshCollider mesh && !mesh.convex) continue;
                Vector3 point = col.ClosestPoint(center);
                if (rising && IsEnvironment(col) && !IsLedge(col, point)) continue;
                float d = Vector3.Distance(point, center) - radius;
                if (d >= best) continue;
                best = d;
                bestCollider = col;
                bestPoint = point;
            }
            if (bestCollider == null) return;
            HasReach = true;
            ReachPoint = bestPoint;
            if (best <= p.grabContactDistance) Attach(bestCollider, bestPoint, p);
        }

        static bool IsEnvironment(Collider c) =>
            !RagdollPawn.ColliderOwner.ContainsKey(c) && (c.attachedRigidbody == null || c.attachedRigidbody.isKinematic);

        static bool IsLedge(Collider c, Vector3 point) => c.bounds.max.y - point.y < LedgeDepth;

        void Attach(Collider target, Vector3 point, RagdollParams p)
        {
            joint = gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = target.attachedRigidbody;
            bool grabbedPawn = RagdollPawn.ColliderOwner.ContainsKey(target);
            joint.breakForce = grabbedPawn ? p.pawnGrabBreakForce : p.grabBreakForce;
            joint.breakTorque = joint.breakForce;
            joint.enablePreprocessing = false;
            joint.enableCollision = false;
            HeldCollider = target;
            HeldBody = target.attachedRigidbody;
            HoldingLedge = IsEnvironment(target) && IsLedge(target, point);
            Vector3 away = Center - point;
            GripNormal = away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.up;
        }

        public void Release(float cooldown = 0f)
        {
            // DestroyImmediate so the grip is gone before the next physics step of this frame.
            if (joint != null) DestroyImmediate(joint);
            joint = null;
            HeldCollider = null;
            HeldBody = null;
            HoldingLedge = false;
            if (cooldown > 0f) regrabCooldown = cooldown;
        }

        void OnJointBreak(float breakForce)
        {
            joint = null;
            HeldCollider = null;
            HeldBody = null;
            HoldingLedge = false;
            regrabCooldown = 0.6f;
        }
    }
}
