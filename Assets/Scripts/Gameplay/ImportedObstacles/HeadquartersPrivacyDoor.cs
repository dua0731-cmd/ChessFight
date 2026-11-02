using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.ProtectKing
{
    // StateDrivenMover: proximity-operated reveal door for the offline war-room prototype.
    // Keep a broad occupancy volume around both leaves; never close on a body in the doorway.
    public sealed class HeadquartersPrivacyDoor : Obstacle
    {
        [SerializeField] Rigidbody leftLeaf;
        [SerializeField] Rigidbody rightLeaf;
        [SerializeField] BoxCollider presenceVolume;
        [SerializeField, Min(0)] float openDistance = 7;
        [SerializeField, Min(.1f)] float moveSpeed = 4;
        [SerializeField, Min(.2f)] float holdOpenSeconds = 1.5f;
        readonly Collider[] overlaps = new Collider[512];
        Vector3 leftRest, rightRest, axis;
        float distance;
        double openUntil = double.NegativeInfinity;
        void Awake()
        {
            if (leftLeaf == null || rightLeaf == null || presenceVolume == null)
            { Debug.LogError("Assign both headquarters door leaves and the occupancy volume.", this); enabled = false; return; }
            leftRest = leftLeaf.position; rightRest = rightLeaf.position; axis = transform.right;
            foreach (var body in new[] { leftLeaf, rightLeaf })
            {
                body.isKinematic = true; body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }
        void FixedUpdate()
        {
            var s = presenceVolume.transform.lossyScale;
            int count = Physics.OverlapBoxNonAlloc(presenceVolume.transform.TransformPoint(presenceVolume.center),
                Vector3.Scale(presenceVolume.size * .5f, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z))),
                overlaps, presenceVolume.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            bool occupied = count == overlaps.Length; // A saturated query must fail open.
            for (int i = 0; i < count && !occupied; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                occupied = actor != null && actor.isActiveAndEnabled;
            }
            if (occupied) openUntil = SharedTime + Mathf.Max(.2f, holdOpenSeconds);
            distance = Mathf.MoveTowards(distance, SharedTime < openUntil ? Mathf.Max(0, openDistance) : 0,
                Mathf.Max(.1f, moveSpeed) * Time.fixedDeltaTime);
            leftLeaf.MovePosition(leftRest - axis * distance);
            rightLeaf.MovePosition(rightRest + axis * distance);
        }
    }
}
