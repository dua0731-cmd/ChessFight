using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Shared timing only. Each toy owns its motion and physical parameters.
    public abstract class KingRushToyMotion : KinematicObstacle
    {
        [SerializeField] protected float offset;
        [SerializeField, Min(.1f)] protected float period = 6;
        public void ConfigureTiming(float seconds, float delay) { period = seconds; offset = delay; }
        protected double Cycle(double time) => (time + offset) / Math.Max(.1, period);
        protected static float Fraction(double cycle) => (float)(cycle - Math.Floor(cycle));
    }
}
