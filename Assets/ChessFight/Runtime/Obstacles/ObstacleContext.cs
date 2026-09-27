using UnityEngine;

namespace ChessFight.ProtectKing
{
    // Prefabs never serialize a reference to a scene object. Resolve once on instantiation.
    [DefaultExecutionOrder(-90)]
    public sealed class ObstacleContext : MonoBehaviour
    {
        public ProtectTheKingMatchController match;
        public bool Running => match == null || match.IsRunning;
        public bool Authority => match == null || match.HasAuthority;
        public float Elapsed => match != null ? match.Elapsed : Time.time - started;
        float started;
        void Awake()
        {
            started = Time.time;
            if (match != null) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                match = root.GetComponentInChildren<ProtectTheKingMatchController>();
                if (match != null) break;
            }
        }
    }
}
