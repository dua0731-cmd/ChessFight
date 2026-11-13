using UnityEngine;

namespace ChessFight.Gameplay
{
    public abstract class KingRushCastleMotion : KinematicObstacle
    {
        [SerializeField, Min(.1f)] float period = 8;
        public void ConfigurePeriod(float seconds) => period = seconds;
        protected float CycleWave(double time) => Wave(time, period);
    }
}
