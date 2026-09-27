using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Every pose, including off-course return travel, is a pure clock function.
    public sealed class KingRushToyMotion : Obstacle
    {
        public enum Motion { Orbit, RollingHead, ClockButton }
        public Motion motion;
        public float offset;
        public float period = 6;
        public float radius = 2;
        public Vector3 travel = new Vector3(0, -5.9f, -22);
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            double cycle = (time + offset) / Math.Max(.1, period);
            float f = (float)(cycle - Math.Floor(cycle));
            position = StartPosition; rotation = StartRotation;
            if (motion == Motion.Orbit)
            {
                float a = f * Mathf.PI * 2;
                position += new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius;
                rotation *= Quaternion.Euler(0, WrapDegrees(cycle * 360), 0);
            }
            else if (motion == Motion.ClockButton)
                position += Vector3.up * (.4f * Mathf.Cos(f * Mathf.PI * 2) - .25f);
            else
            {
                // Roll downhill for 65%, then return below the track. No wrap sweep through runners.
                if (f < .65f) position += travel * (f / .65f);
                else if (f < .72f) position += travel + Vector3.down * (8 * (f - .65f) / .07f);
                else if (f < .93f) position += travel * (1 - (f - .72f) / .21f) + Vector3.down * 8;
                else position += Vector3.down * (8 * (1 - f) / .07f);
                rotation *= Quaternion.Euler(WrapDegrees(-cycle * 1080), 0, 0);
            }
        }
    }
}
