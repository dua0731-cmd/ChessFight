using System.Diagnostics;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Runs the physics step by hand (after every FixedUpdate) so one step can be timed.
    /// Only used by the command-line load test.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class PhysicsStepProfiler : MonoBehaviour
    {
        public bool measuring;
        public int Steps { get; private set; }
        public double TotalMs { get; private set; }
        public double MaxMs { get; private set; }
        public double AverageMs => Steps > 0 ? TotalMs / Steps : 0d;

        readonly Stopwatch watch = new Stopwatch();

        void OnEnable() => Physics.simulationMode = SimulationMode.Script;

        void OnDisable() => Physics.simulationMode = SimulationMode.FixedUpdate;

        void FixedUpdate()
        {
            watch.Restart();
            Physics.Simulate(Time.fixedDeltaTime);
            watch.Stop();
            if (!measuring) return;
            double ms = watch.Elapsed.TotalMilliseconds;
            Steps++;
            TotalMs += ms;
            if (ms > MaxMs) MaxMs = ms;
        }

        public void ResetCounters()
        {
            Steps = 0;
            TotalMs = 0d;
            MaxMs = 0d;
        }
    }
}
