using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    // StateDrivenMover: two-person approval shortcut for the offline war-room prototype.
    // A separate walkable route must remain available to a solo player.
    public sealed class DualApprovalGate : Obstacle
    {
        [SerializeField] ObstacleContext context;
        [SerializeField] BoxCollider firstPlate;
        [SerializeField] BoxCollider secondPlate;
        [SerializeField] GameObject firstIndicator;
        [SerializeField] GameObject secondIndicator;
        [SerializeField] BoxCollider doorwayVolume;
        [SerializeField] Rigidbody leftLeaf;
        [SerializeField] Rigidbody rightLeaf;
        [SerializeField, Min(.1f)] float approvalSeconds = 1.25f;
        [SerializeField, Min(1)] float openSeconds = 8;
        [SerializeField, Min(0)] float openDistance = 3.5f;
        [SerializeField, Min(.1f)] float moveSpeed = 3;

        readonly Collider[] overlaps = new Collider[512];
        readonly HashSet<ImportedObstacleActor> firstActors = new HashSet<ImportedObstacleActor>();
        readonly HashSet<ImportedObstacleActor> secondActors = new HashSet<ImportedObstacleActor>();
        Vector3 leftRest, rightRest, axis;
        double lastTime;
        float approval, remainingOpen, distance;

        public float ApprovalProgress => Mathf.Clamp01(approval / Mathf.Max(.1f, approvalSeconds));
        public float RemainingOpenSeconds => remainingOpen;

        void Awake()
        {
            if (firstPlate == null || secondPlate == null || firstPlate == secondPlate ||
                doorwayVolume == null || leftLeaf == null || rightLeaf == null || leftLeaf == rightLeaf)
            {
                Debug.LogError("Assign two separate approval plates, the doorway volume, and two door leaves.", this);
                enabled = false;
                return;
            }
            leftRest = leftLeaf.position;
            rightRest = rightLeaf.position;
            axis = transform.right;
            ConfigureLeaf(leftLeaf);
            ConfigureLeaf(rightLeaf);
        }

        static void ConfigureLeaf(Rigidbody body)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        void OnEnable() => lastTime = SharedTime;

        int Query(BoxCollider volume)
        {
            var scale = volume.transform.lossyScale;
            return Physics.OverlapBoxNonAlloc(volume.transform.TransformPoint(volume.center),
                Vector3.Scale(volume.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))),
                overlaps, volume.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
        }

        bool Collect(BoxCollider volume, HashSet<ImportedObstacleActor> actors)
        {
            actors.Clear();
            int count = Query(volume);
            // Truncated plate results must not authorize a door on incomplete evidence.
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor != null && actor.isActiveAndEnabled) actors.Add(actor);
            }
            return actors.Count > 0;
        }

        bool DoorwayOccupied()
        {
            int count = Query(doorwayVolume);
            if (count == overlaps.Length) return true; // Never close on an incomplete query.
            for (int i = 0; i < count; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor != null && actor.isActiveAndEnabled) return true;
            }
            return false;
        }

        bool DistinctOperators()
        {
            foreach (var first in firstActors)
                foreach (var second in secondActors)
                    if (!ReferenceEquals(first, second)) return true;
            return false;
        }

        void FixedUpdate()
        {
            double now = SharedTime;
            float dt = (float)System.Math.Min(.1, System.Math.Max(0, now - lastTime));
            if (now < lastTime) { approval = 0; remainingOpen = 0; }
            lastTime = now;
            // This stateful door has no replicated-state bridge. A shared-clock client must not simulate it.
            if (ObstacleClock.Shared || (context != null && (!context.Running || !context.Authority)))
            {
                SetIndicators(false, false);
                approval = 0;
                return;
            }

            bool firstOccupied = Collect(firstPlate, firstActors);
            bool secondOccupied = Collect(secondPlate, secondActors);
            SetIndicators(firstOccupied, secondOccupied);
            bool approved = firstOccupied && secondOccupied && DistinctOperators();
            approval = approved ? Mathf.Min(Mathf.Max(.1f, approvalSeconds), approval + dt) : 0;
            remainingOpen = Mathf.Max(0, remainingOpen - dt);
            if (approved && approval >= Mathf.Max(.1f, approvalSeconds))
                remainingOpen = Mathf.Max(1, openSeconds);

            // Occupancy protects a previously opened door; it must not unlock a closed shortcut.
            bool stayOpen = remainingOpen > 0 || (distance > .001f && DoorwayOccupied());
            distance = Mathf.MoveTowards(distance, stayOpen ? Mathf.Max(0, openDistance) : 0,
                Mathf.Max(.1f, moveSpeed) * dt);
            leftLeaf.MovePosition(leftRest - axis * distance);
            rightLeaf.MovePosition(rightRest + axis * distance);
        }

        void SetIndicators(bool first, bool second)
        {
            if (firstIndicator != null) firstIndicator.SetActive(first);
            if (secondIndicator != null) secondIndicator.SetActive(second);
        }

        void OnDisable()
        {
            approval = 0;
            firstActors.Clear();
            secondActors.Clear();
            SetIndicators(false, false);
        }
    }
}
