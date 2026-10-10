using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class SpringLaunchPad : LaunchPad
    {
        [SerializeField, Min(.1f)] float period = 5f;
        [SerializeField] float phase;
        [SerializeField] Transform windKey;
        [SerializeField] float windTurns = 2f;
        Quaternion keyRest = Quaternion.identity;
        long lastCycle = long.MinValue;
        public override float Period => period;
        public override float SecondsToFire
        {
            get
            {
                if (period <= 0f) return 0f;
                double time = SharedTime + phase;
                return (float)(period - (time - Math.Floor(time / period) * period));
            }
        }
        protected override void ConfigureSchedule(float seconds, float offset, Transform key)
        {
            period = seconds; phase = offset; windKey = key;
            if (windKey != null) keyRest = windKey.localRotation;
            lastCycle = long.MinValue;
        }
        void Awake() { if (windKey != null) keyRest = windKey.localRotation; }
        void FixedUpdate()
        {
            if (period <= 0f) { ThrowAllOn(); return; }
            long cycle = (long)Math.Floor((SharedTime + phase) / period);
            // First observe the clock; do not fire immediately when activated.
            if (lastCycle != long.MinValue && cycle != lastCycle) ThrowAllOn();
            lastCycle = cycle;
        }
        void Update()
        {
            if (windKey == null || period <= 0f) return;
            float wound = 1f - SecondsToFire / period;
            windKey.localRotation = keyRest * Quaternion.Euler(0f, 360f * windTurns * wound, 0f);
        }
    }
}
