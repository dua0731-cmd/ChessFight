using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.ProtectKing
{
    // Shared surface contract only. Each concrete obstacle owns its own tuning.
    public abstract class ObstacleSurface : Obstacle, IMovingSurface
    {
        [SerializeField] protected ObstacleContext context;
        protected bool Running => isActiveAndEnabled && (context == null || context.Running);
        protected virtual void Awake()
        {
            if (context == null) context = GetComponentInParent<ObstacleContext>();
        }
        public virtual Vector3 PointVelocity(Vector3 point) => Vector3.zero;
        public virtual Quaternion DeltaRotation => Quaternion.identity;
    }
}
