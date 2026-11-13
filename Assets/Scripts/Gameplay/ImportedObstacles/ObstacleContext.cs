using System;
using ChessFight.Gameplay;
using UnityEngine;
namespace ChessFight.ProtectKing
{
    // Geometry reads the destination's shared clock. Local cooldowns keep a small
    // clock. Stateful hazards require an explicit host/state bridge in online use.
    // Authority (R126): offline, or this PC hosts the match. It was "the clock is not
    // shared", which is false on the host too, so online no hammer, piece or vent ever
    // pushed anyone while every screen showed them hitting.
    public sealed class ObstacleContext : MonoBehaviour
    {
        public bool Running => isActiveAndEnabled;
        public bool Authority => ObstacleClock.Authority;
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
