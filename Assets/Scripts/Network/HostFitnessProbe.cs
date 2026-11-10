using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

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
    //
    // R533 (user 11-10, rule 4: the best PC by "CPU, GPU 통합"): the graphics card is measured too. The host draws
    // the match like everyone else, and a PC that draws it slowly makes a poor host whatever its processor (it steps
    // the physics in lumps and sends its snapshots late). A fixed drawing workload - full-screen copies between two
    // 1080p textures, then a one-pixel read that makes the card finish - is timed on the main thread, a few times
    // over the first minutes, and the best time stands as well (HostFitness.GpuFactor). A weak card is told apart from
    // a short first slice, so it is not made to stall for the whole workload.
    public static class HostFitnessProbe
    {
        static double benchMs = -1;
        static int started;
        // When each sitting starts, in milliseconds after Start().
        static readonly int[] SittingsAt = { 1500, 6000, 15000, 30000, 60000, 120000 };

        // Best workload time in milliseconds so far, -1 until the first sitting has finished.
        public static double BenchMs => Interlocked.CompareExchange(ref benchMs, 0, 0);

        // Best time of the graphics workload in milliseconds so far: -1 until its first sitting, 0 when there is
        // nothing to measure (no graphics device) or it could not be measured - that costs the score nothing.
        public static double GpuMs => gpuMs;
        static double gpuMs = -1;
        // When each graphics sitting starts, in seconds after Start(). Main thread: each is a hitch of a few
        // milliseconds on a graphics card, some tens on an integrated chip.
        static readonly float[] GpuSittingsAt = { 2.5f, 8f, 20f, 45f, 90f };
        static int gpuSitting;
        static float startedAt;
        // Full-screen copies a sitting makes (pairs: there and back), the first slice of them, and how long that slice
        // may take before the rest is worked out from it instead of run.
        const int GpuPairs = 24, GpuFirstPairs = 4;
        const double GpuSlowSliceMs = 12;

        public static void Start()
        {
            if (Interlocked.Exchange(ref started, 1) == 1) return;
            startedAt = Time.realtimeSinceStartup;
            var thread = new Thread(Run) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "ChessFight host probe" };
            thread.Start();
        }

        // This machine's HostFitness.Base: the benchmarks and the hardware, 0 until the first sitting of each has
        // finished; it only rises after that (both keep their best time). SteamSession adds the live frame rate
        // itself. Main thread, every frame (SystemInfo, and the graphics sittings run from here).
        public static int BaseScore()
        {
            SitGpu();
            double ms = BenchMs, gpu = gpuMs;
            if (gpu < 0) return 0;
            if (ms != scoredMs || gpu != scoredGpu)
            {
                scoredMs = ms; scoredGpu = gpu;
                score = HostFitness.Base(ms, SystemInfo.processorCount, SystemInfo.systemMemorySize, Application.isEditor, gpu);
            }
            return score;
        }
        static double scoredMs = double.NaN, scoredGpu = double.NaN;
        static int score;

        // A graphics sitting when one is due.
        static void SitGpu()
        {
            if (started == 0 || gpuSitting >= GpuSittingsAt.Length || Time.realtimeSinceStartup - startedAt < GpuSittingsAt[gpuSitting]) return;
            gpuSitting++;
            double measured = MeasureGpu();
            if (measured <= 0) { if (gpuMs < 0) gpuMs = 0; }
            else if (gpuMs <= 0 || measured < gpuMs) gpuMs = measured;
        }

        // The graphics workload's time in milliseconds, 0 when it cannot be measured here.
        static double MeasureGpu()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return 0;
            RenderTexture a = null, b = null;
            Texture2D pixel = null;
            var active = RenderTexture.active;
            try
            {
                a = RenderTexture.GetTemporary(1920, 1080, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                b = RenderTexture.GetTemporary(1920, 1080, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                a.filterMode = b.filterMode = FilterMode.Bilinear;
                pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                // Making and filling the textures is not part of the time.
                Graphics.Blit(Texture2D.whiteTexture, a);
                Graphics.Blit(a, b);
                Finish(a, pixel);
                double best = double.MaxValue;
                for (int round = 0; round < 2; round++)
                {
                    var watch = Stopwatch.StartNew();
                    Copy(a, b, GpuFirstPairs);
                    Finish(a, pixel);
                    double total = watch.Elapsed.TotalMilliseconds;
                    if (total > GpuSlowSliceMs) total *= (double)GpuPairs / GpuFirstPairs;
                    else
                    {
                        Copy(a, b, GpuPairs - GpuFirstPairs);
                        Finish(a, pixel);
                        total = watch.Elapsed.TotalMilliseconds;
                    }
                    best = Math.Min(best, total);
                    // One round is hitch enough on a slow card.
                    if (total > 40) break;
                }
                return best < double.MaxValue ? best : 0;
            }
            catch (Exception) { return 0; }
            finally
            {
                RenderTexture.active = active;
                if (a != null) RenderTexture.ReleaseTemporary(a);
                if (b != null) RenderTexture.ReleaseTemporary(b);
                if (pixel != null) UnityEngine.Object.Destroy(pixel);
            }
        }

        static void Copy(RenderTexture a, RenderTexture b, int pairs)
        {
            for (int i = 0; i < pairs; i++) { Graphics.Blit(a, b); Graphics.Blit(b, a); }
        }

        // Reading a pixel back waits until the card has drawn everything queued before it.
        static void Finish(RenderTexture from, Texture2D pixel)
        {
            RenderTexture.active = from;
            pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
        }

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
