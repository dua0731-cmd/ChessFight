using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace ChessFight.Network
{
    // Measures this PC once, off the main thread, so the host role can go to the
    // machine that simulates fastest (HostFitness, HostElection).
    //
    // The frame rate alone cannot tell machines apart: with v-sync on, a fast and
    // an average PC both show 60 fps in the lobby. So the probe times a fixed
    // physics-like workload (vector integration with square roots and branches)
    // and keeps the best of a few rounds, which ignores a busy moment.
    //
    // R294: one sitting is not enough. The first comes while the game is still
    // loading its first scenes (the logo film, shaders), and a PC busy then read
    // slower than a weaker one and kept losing the host role to it. So the probe
    // sits again a few times over the first two minutes, mostly in the lobby,
    // and the best time stands: a busy moment can only make a run slower.
    public static class HostFitnessProbe
    {
        static double benchMs = -1;
        static int started;
        // When each sitting starts, in milliseconds after Start().
        static readonly int[] SittingsAt = { 1500, 6000, 15000, 30000, 60000, 120000 };

        // Best workload time in milliseconds so far, -1 until the first sitting has finished.
        public static double BenchMs => Interlocked.CompareExchange(ref benchMs, 0, 0);

        public static void Start()
        {
            if (Interlocked.Exchange(ref started, 1) == 1) return;
            var thread = new Thread(Run) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "ChessFight host probe" };
            thread.Start();
        }

        // This machine's HostFitness.Base: the benchmark and the hardware, 0 until
        // the first sitting has finished; it only rises after that. SteamSession adds
        // the live frame rate itself. Main thread (SystemInfo).
        public static int BaseScore()
        {
            double ms = BenchMs;
            if (ms != scoredMs) { scoredMs = ms; score = HostFitness.Base(ms, SystemInfo.processorCount, SystemInfo.systemMemorySize, Application.isEditor); }
            return score;
        }
        static double scoredMs = double.NaN;
        static int score;

        static void Run()
        {
            int slept = 0;
            foreach (int at in SittingsAt)
            {
                Thread.Sleep(Math.Max(0, at - slept));
                slept = at;
                try
                {
                    double best = double.MaxValue;
                    for (int round = 0; round < 3; round++)
                    {
                        var watch = Stopwatch.StartNew();
                        Work();
                        watch.Stop();
                        best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
                        Thread.Sleep(50);
                    }
                    double current = BenchMs;
                    if (current < 0 || best < current) Interlocked.Exchange(ref benchMs, best);
                }
                // A sitting that fails leaves the best time so far.
                catch (Exception) { }
            }
        }

        // 256 bodies pulled towards targets under gravity, integrated 6000 steps.
        // Bounded (a unit pull and damping, so it never reaches NaN, whose slow
        // path would swamp the timing), deterministic, allocation-free after the
        // arrays. About 30 ms on a fast 16-thread desktop under Unity's Mono,
        // roughly HostFitness.ReferenceBenchMs on a mid-range one.
        static double sink;
        static void Work()
        {
            const int bodies = 256, steps = 6000;
            var px = new float[bodies]; var py = new float[bodies]; var pz = new float[bodies];
            var vx = new float[bodies]; var vy = new float[bodies]; var vz = new float[bodies];
            var tx = new float[bodies]; var ty = new float[bodies]; var tz = new float[bodies];
            for (int i = 0; i < bodies; i++)
            {
                px[i] = i * .37f % 5; py[i] = 1 + i * .11f % 3; pz[i] = i * .53f % 7;
                tx[i] = i * .71f % 6 - 3; ty[i] = 1 + i * .29f % 2; tz[i] = i * .43f % 6 - 3;
            }
            const float dt = 1f / 120f;
            for (int s = 0; s < steps; s++)
                for (int i = 0; i < bodies; i++)
                {
                    float dx = tx[i] - px[i], dy = ty[i] - py[i], dz = tz[i] - pz[i];
                    float inv = 1f / ((float)Math.Sqrt(dx * dx + dy * dy + dz * dz) + 1e-3f);
                    vx[i] = (vx[i] + dx * inv * 20f * dt) * .99f;
                    vy[i] = (vy[i] + (dy * inv * 20f - 9.81f) * dt) * .99f;
                    vz[i] = (vz[i] + dz * inv * 20f * dt) * .99f;
                    px[i] += vx[i] * dt; py[i] += vy[i] * dt; pz[i] += vz[i] * dt;
                    if (py[i] < 0) { py[i] = 0; vy[i] = -vy[i] * .3f; }
                    if (s % 97 == 0) tx[i] = -tx[i];
                }
            double sum = 0;
            for (int i = 0; i < bodies; i++) sum += px[i] + py[i] + pz[i];
            sink = sum;
        }
    }
}
