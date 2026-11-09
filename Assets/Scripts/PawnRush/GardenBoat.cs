using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A boat on the royal water garden's reservoir (v31 beat 6, the user: "팀 보트는 가로로 여러 개가 랜덤한 방향으로
    // 움직여서 플레이어가 다음 보트로 넘어가면서"): it runs along its lane at the lane's speed and way, goes out of
    // sight under the tunnel at the end, sinks there, comes round under the water and rises in the other tunnel
    // (RoyalWaterGardenRules.BoatX / BoatDrop). A pure function of the shared obstacle clock and its lane, which
    // BoatLanes gives it from the section's seed, so every PC has it in the same place. Whoever stands on its deck is
    // carried (an Obstacle is an IMovingSurface). Both teams ride the same boats.
    public sealed class GardenBoat : Obstacle
    {
        [Tooltip("Which of its lane's boats (0, 1): they run a spacing apart.")]
        [SerializeField] int boat;
        [Tooltip("Along the lane where the builder put it (so the prefab shows the boats spread out); 0 = the middle.")]
        [SerializeField] float authoredX;
        [Header("The lane (BoatLanes sets these from the section's seed)")]
        [SerializeField] int dir = 1;
        [SerializeField] double speed = 2.0;
        [SerializeField] double offset;

        // For the builder: on an inactive object, before Awake.
        public void Configure(int boat, float authoredX)
        {
            this.boat = boat;
            this.authoredX = authoredX;
            offset = authoredX + RoyalWaterGardenRules.Loop * .5 - boat * RoyalWaterGardenRules.Spacing;
        }

        public void SetLane(RoyalWaterGardenRules.BoatLane lane)
        {
            dir = lane.Dir;
            speed = lane.Speed;
            offset = lane.Offset;
        }

        public float X => (float)RoyalWaterGardenRules.BoatX(Lane, boat, ObstacleClock.Now + phase);

        RoyalWaterGardenRules.BoatLane Lane => new RoyalWaterGardenRules.BoatLane { Dir = dir, Speed = speed, Offset = offset };

        // R531: the wrap under the water is a cut (RoyalWaterGardenRules.BoatWraps): put there, not swept 48 m in one step.
        // The times are Evaluate's (the phase is in them).
        protected override bool JumpsBetween(double from, double to) => RoyalWaterGardenRules.BoatWraps(Lane, boat, from, to);

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            double x = RoyalWaterGardenRules.BoatX(Lane, boat, time);
            float drop = (float)RoyalWaterGardenRules.BoatDrop(x);
            rotation = StartRotation;
            position = StartPosition + StartRotation * new Vector3((float)x - authoredX, -drop, 0f);
        }
    }
}
