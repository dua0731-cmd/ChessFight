using System;
using System.Collections;
using System.Diagnostics;
using ChessFight.Game;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ChessFight.RagdollLab
{
    /// <summary>R297: what a match link measures for the metrics box, over the last second.</summary>
    public struct NetMetrics
    {
        public bool Host;
        public int Players;
        public int SentBytes, ReceivedBytes;          // every packet of the link this PC sent and read in the last second
        public int SnapshotBytes;                     // the last snapshot this PC sent (host) or read (client)
        public int SnapshotsSent, SnapshotsReceived;  // in the last second: the host's snapshots, a client's snapshots read
        public float RoundTripMs;                     // a client: its input's time as the snapshots echo it
        public double PlaybackDelayMs;                // a client: how far behind the newest snapshot it draws
        public double LossShare;                      // a client: snapshots lost, 0..1
        public bool Piloted;                          // the autopilot plays this PC (-autopilot)
        public uint PilotSeed;
    }

    /// <summary>
    /// R297 (HOST_AUTHORITY P5): a development build's metrics box in a match, F3 to show it - this PC's frame and physics
    /// step time, its managed allocations and the link's numbers (NetMetrics), for the baselines in VALIDATION. A match
    /// link adds it (Attach); a release build never does. The physics step is timed from the last script's FixedUpdate
    /// (this one runs last) to the WaitForFixedUpdate after the step, so the physics loop runs as it always does
    /// (PhysicsStepProfiler runs it by hand instead, for the command-line load test only). The text is made once a second,
    /// so the box adds little garbage of its own while it is shown.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class NetMetricsOverlay : MonoBehaviour
    {
        // Free in both matches: the lab's own F3 panel is for its test rooms only (LabNetLink, not RealMatch).
        const KeyCode Toggle = KeyCode.F3;
        const float Width = 470f, LineHeight = 18f;

        Func<NetMetrics> source;
        bool shown;
        readonly string[] lines = new string[6];
        int lineCount;
        float frameMs = -1f, statsAt;
        readonly Stopwatch physics = new Stopwatch();
        WaitForFixedUpdate afterPhysics;
        double stepTotal, stepMax, stepAverage, stepLargest;
        int steps, stepRate;
        // The managed allocations of a frame (a development build's counter; none in a release build).
        ProfilerRecorder gcAllocated;
        long gcBytes, gcPerFrame = -1;
        int gcFrames, collectionsAtStart;
        GUIStyle style;
        Texture2D panel;

        /// <summary>Adds the box to a match link's object in a development build (nothing in a release build).</summary>
        public static NetMetricsOverlay Attach(GameObject on, Func<NetMetrics> source)
        {
            if (!Debug.isDebugBuild || on == null || source == null) return null;
            var overlay = on.GetComponent<NetMetricsOverlay>();
            if (overlay == null) overlay = on.AddComponent<NetMetricsOverlay>();
            overlay.source = source;
            return overlay;
        }

        void OnEnable()
        {
            gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            collectionsAtStart = GC.CollectionCount(0);
            if (afterPhysics == null) afterPhysics = new WaitForFixedUpdate();
            StartCoroutine(AfterPhysics());
        }

        void OnDisable()
        {
            gcAllocated.Dispose();
            physics.Reset();
        }

        void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }

        void FixedUpdate() => physics.Restart();

        IEnumerator AfterPhysics()
        {
            while (true)
            {
                yield return afterPhysics;
                if (!physics.IsRunning) continue;
                physics.Stop();
                double ms = physics.Elapsed.TotalMilliseconds;
                stepTotal += ms;
                steps++;
                if (ms > stepMax) stepMax = ms;
            }
        }

        void Update()
        {
            float dtMs = Mathf.Min(Time.unscaledDeltaTime * 1000f, 250f);
            frameMs = frameMs < 0f ? dtMs : frameMs + (dtMs - frameMs) * 0.05f;
            if (gcAllocated.Valid)
            {
                gcBytes += gcAllocated.LastValue;
                gcFrames++;
            }
            bool build = false;
            if (LegacyKeys.Down(Toggle) && !ChatBox.KeysHeld)
            {
                shown = !shown;
                build = shown;
            }
            if (Time.realtimeSinceStartup >= statsAt)
            {
                statsAt = Time.realtimeSinceStartup + 1f;
                stepRate = steps;
                stepAverage = steps > 0 ? stepTotal / steps : 0d;
                stepLargest = stepMax;
                gcPerFrame = gcFrames > 0 ? gcBytes / gcFrames : -1;
                steps = gcFrames = 0;
                stepTotal = stepMax = 0d;
                gcBytes = 0;
                build = shown;
            }
            if (build) Build();
        }

        void Build()
        {
            var m = source();
            lineCount = 0;
            Line($"측정 (F3) · {(m.Host ? "방장" : "참가자")} · {m.Players}명" + (m.Piloted ? $" · 자동 입력 (시드 {m.PilotSeed})" : ""));
            Line($"프레임 {frameMs:F1} ms · 물리 {stepAverage:F2} ms/스텝 (최대 {stepLargest:F2}) · {stepRate}스텝/초");
            if (m.Host)
            {
                Line($"스냅샷 {m.SnapshotBytes}바이트 · 초당 {m.SnapshotsSent}번 보냄");
                Line($"보내기 {m.SentBytes / 1024f:F1} KB/초 · 받기 {m.ReceivedBytes / 1024f:F1} KB/초");
            }
            else
            {
                Line($"왕복 지연 {m.RoundTripMs:F0} ms · 재생 지연 {m.PlaybackDelayMs:F0} ms · 손실 {m.LossShare * 100d:F1}%");
                Line($"받기 {m.ReceivedBytes / 1024f:F1} KB/초 (스냅샷 초당 {m.SnapshotsReceived}개 · {m.SnapshotBytes}바이트) · 보내기 {m.SentBytes / 1024f:F1} KB/초");
            }
            int collections = GC.CollectionCount(0) - collectionsAtStart;
            Line(gcPerFrame >= 0 ? $"GC 할당 {gcPerFrame / 1024f:F2} KB/프레임 · 경기 중 GC 수집 {collections}번"
                                 : $"GC 할당 - (개발 빌드에서만 측정) · 경기 중 GC 수집 {collections}번");
        }

        void Line(string line)
        {
            if (lineCount < lines.Length) lines[lineCount++] = line;
        }

        void OnGUI()
        {
            if (!shown || lineCount == 0) return;
            EnsureStyle();
            var box = new Rect(12f, 12f, Width, lineCount * LineHeight + 12f);
            GUI.DrawTexture(box, panel);
            for (int i = 0; i < lineCount; i++)
                GUI.Label(new Rect(box.x + 8f, box.y + 6f + i * LineHeight, Width - 16f, LineHeight), lines[i], style);
        }

        void EnsureStyle()
        {
            if (style != null) return;
            panel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            panel.SetPixel(0, 0, new Color(0.06f, 0.09f, 0.14f, 0.85f));
            panel.Apply();
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 13);
            style = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13, wordWrap = false };
            style.normal.textColor = new Color(0.85f, 0.92f, 1f);
        }
    }
}
