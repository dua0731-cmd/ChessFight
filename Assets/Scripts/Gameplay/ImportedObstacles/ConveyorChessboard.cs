using UnityEngine;

namespace ChessFight.ProtectKing
{
    public sealed class ConveyorChessboard : ObstacleSurface
    {
        [SerializeField] Vector3 direction = Vector3.forward;
        [SerializeField, Min(0)] float conveyorSpeed = 3f;
        [SerializeField] Transform[] beltMarkers;
        [SerializeField, Min(.1f)] float beltLength = 6f;
        Vector3[] markerOrigins;

        public override Vector3 PointVelocity(Vector3 point) => Running
            ? transform.TransformDirection(direction.normalized) * conveyorSpeed : Vector3.zero;
        protected override void Awake()
        {
            base.Awake();
            markerOrigins = new Vector3[beltMarkers == null ? 0 : beltMarkers.Length];
            for (int i = 0; i < markerOrigins.Length; i++) markerOrigins[i] = beltMarkers[i].localPosition;
        }
        void Update()
        {
            if (!Running) return;
            var axis = direction.normalized;
            for (int i = 0; i < markerOrigins.Length; i++)
            {
                float initial = Vector3.Dot(markerOrigins[i], axis);
                float travel = (float)(SharedTime * conveyorSpeed % Mathf.Max(.1f, beltLength));
                float offset = Mathf.Repeat(initial + travel + beltLength * .5f, beltLength) - beltLength * .5f;
                beltMarkers[i].localPosition = markerOrigins[i] + axis * (offset - initial);
            }
        }
    }
}
