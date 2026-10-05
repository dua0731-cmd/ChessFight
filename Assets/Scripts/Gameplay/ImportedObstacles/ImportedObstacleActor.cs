using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
namespace ChessFight.ProtectKing
{
    // Per-character handle, not a controller. Existing character contracts own
    // knockback and launch; no player, input, AI or win system is imported.
    public sealed class ImportedObstacleActor
    {
        static readonly Dictionary<Component, ImportedObstacleActor> actors = new Dictionary<Component, ImportedObstacleActor>();
        readonly Component owner;
        readonly ICharacterDriver driver;
        readonly IHitReceiver hits;
        readonly ILaunchable launcher;
        readonly Rigidbody[] bodies;
        readonly RaycastHit[] groundHits = new RaycastHit[24];
        public GameObject gameObject => owner != null ? owner.gameObject : null;
        public bool isActiveAndEnabled => owner != null && owner.gameObject.activeInHierarchy && (!(owner is Behaviour b) || b.enabled);
        public Vector3 Position => launcher != null ? launcher.LaunchFrom : driver.FollowTarget.position;
        Rigidbody MainBody => driver.FollowTarget != null ? driver.FollowTarget.GetComponent<Rigidbody>() : null;
        public Vector3 Velocity => MainBody != null ? MainBody.linearVelocity : Vector3.zero;
        public bool CanReceiveObstacle => isActiveAndEnabled && hits != null && (MainBody == null || !MainBody.isKinematic);
        public bool Grounded
        {
            get
            {
                if (!isActiveAndEnabled || Velocity.y > 1.5f) return false;
                int n = Physics.SphereCastNonAlloc(Position + Vector3.up * .24f, .15f, Vector3.down,
                    groundHits, .4f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                    if (!groundHits[i].collider.transform.IsChildOf(owner.transform) && groundHits[i].normal.y > .55f) return true;
                return false;
            }
        }
        ImportedObstacleActor(Component owner, ICharacterDriver driver)
        {
            this.owner=owner; this.driver=driver;
            hits=owner.GetComponent<IHitReceiver>(); launcher=owner.GetComponent<ILaunchable>();
            bodies=owner.GetComponentsInChildren<Rigidbody>();
        }
        public static ImportedObstacleActor Find(Collider collider)
        {
            if (collider == null) return null;
            var driver=collider.GetComponentInParent<ICharacterDriver>();
            var owner=driver as Component;
            if (owner == null) return null;
            if (!actors.TryGetValue(owner,out var actor))
            {
                if (actors.Count > 128) actors.Clear();
                actor=new ImportedObstacleActor(owner,driver); actors[owner]=actor;
            }
            return actor;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => actors.Clear();
        public void AddObstacleImpulse(Vector3 velocity)
        { if (CanReceiveObstacle) hits.ApplyHit(velocity,0,0,true); }
        public void LaunchFromObstacle(Vector3 velocity)
        {
            if (!CanReceiveObstacle) return;
            if (launcher != null) launcher.Launch(velocity);
            else hits.ApplyHit(velocity - Velocity,0,0,true);
        }
        // Wind is continuous acceleration, not a repeated hit/stagger. Reject
        // puppets via their kinematic body, then push every body part equally.
        public Vector3 Accelerate(Vector3 acceleration, float limit, float dt)
        {
            if (!CanReceiveObstacle || limit <= 0 || acceleration.sqrMagnitude < .000001f) return Vector3.zero;
            Vector3 axis=acceleration.normalized;
            float amount=Mathf.Min(acceleration.magnitude * dt,Mathf.Max(0,limit-Vector3.Dot(Velocity,axis)));
            Vector3 change=axis*amount;
            foreach(var body in bodies) if(body != null && !body.isKinematic) body.AddForce(change,ForceMode.VelocityChange);
            return change;
        }
    }
}
