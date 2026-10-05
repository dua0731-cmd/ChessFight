using System;
using ChessFight.Gameplay;
using UnityEngine;
namespace ChessFight.ProtectKing
{
    // Geometry reads the destination's shared clock. Local cooldowns keep a small
    // clock. Stateful hazards require an explicit host/state bridge in online use.
    public sealed class ObstacleContext : MonoBehaviour
    {
        public bool Running => isActiveAndEnabled;
        public bool Authority => !ObstacleClock.Shared;
        public float Elapsed => Time.time;
        public double Now => ObstacleClock.Now;
        public static float PeriodicTime(double time, float delay, float period, bool repeat = true)
        {
            double age = time - Math.Max(0, delay);
            if (age < 0 || !repeat) return (float)Math.Min(time, delay + period + 1);
            return Math.Max(0, delay) + (float)(age % Math.Max(.001, period));
        }
    }
}
