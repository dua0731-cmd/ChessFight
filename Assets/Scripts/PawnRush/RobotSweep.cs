using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A robot arm over the royal water garden's dyke (v31 beat 5): hung from a gantry on the dyke's north side, it
    // rests raised on its own side, winds up, sweeps south across the dyke just over the floor, holds out over the
    // water, swings back north across it and rests (RoyalWaterGardenRules.RobotAngle, 6 s). A pure function of the
    // shared obstacle clock. Its arm and hand push what they touch like any moving collider; on top of that, the
    // authority knocks a pawn the hand reaches while it crosses low: off the dyke into the water on the sweep, back
    // against the north wall on the return - once per pass.
    public sealed class RobotSweep : Obstacle
    {
        [Tooltip("Local axis it swings about; a positive angle lifts the hand towards its own (north) side.")]
        [SerializeField] Vector3 axis = Vector3.left;
        [SerializeField] Transform hand;
        [SerializeField] float handReach = 1.1f;
        [SerializeField] float sweepPush = 7f, sweepLift = 3f, sweepKnockdown = 1f;
        [SerializeField] float backPush = 4f, backLift = 2f, backKnockdown = .6f;
        [Tooltip("Degrees from hanging down within which the hand is low enough over the dyke to hit.")]
        [SerializeField] float lowAngle = 40f;

        readonly Collider[] overlaps = new Collider[32];
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
        long struckPass = long.MinValue;

        // For the builder: on an inactive object, before Awake.
        public void Configure(Vector3 axis, Transform hand)
        {
            this.axis = axis;
            this.hand = hand;
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            rotation = StartRotation * Quaternion.AngleAxis((float)RoyalWaterGardenRules.RobotAngle(time), axis);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!ObstacleClock.Authority || hand == null) return;
            double now = ObstacleClock.Now + phase, into;
            var swing = RoyalWaterGardenRules.RobotPhase(now, out into);
            bool sweep = swing == RoyalWaterGardenRules.Swing.Sweep;
            if (!sweep && swing != RoyalWaterGardenRules.Swing.Back) return;
            if (Mathf.Abs((float)RoyalWaterGardenRules.RobotAngle(now)) > lowAngle) return;
            long pass = RoyalWaterGardenRules.RobotCycleIndex(now) * 2 + (sweep ? 0 : 1);
            if (pass != struckPass) { struck.Clear(); struckPass = pass; }
            // South (off the dyke) is the frame's -z: the robot is placed unturned in the section.
            var away = StartRotation * (sweep ? Vector3.back : Vector3.forward);
            int n = Physics.OverlapSphereNonAlloc(hand.position, handReach, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || !struck.Add(actor)) continue;
                actor.AddObstacleHit(away * (sweep ? sweepPush : backPush) + Vector3.up * (sweep ? sweepLift : backLift),
                                     sweep ? sweepKnockdown : backKnockdown);
                GardenHits.Report(actor.gameObject, "로봇 팔");
            }
        }
    }
}
