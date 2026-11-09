using System;
using System.Collections;
using System.Diagnostics;
using ChessFight.Game;
using ChessFight.Network;
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
        // R306 [0] (HOST_AUTHORITY §5): the latencies a press adds up on its way.
        public int SnapshotRateTarget;                // the host: the snapshots a second it sends at (30 or 60)
        public double JitterMs, IntervalMs;           // a client: the snapshots' arrival jitter, their interval as the host spaced them
        public double ApplyWaitMs;                    // the host: its players' inputs from arriving to a physics step (ms; -1 none yet)
        public double Answered, AnsweredLast, Seen;   // a client: a press until the host's answer is here, until it is drawn (ms; -1 none yet)
        public int PressesTimed, PressesLost;
        public string Route;                          // straight or through Steam's relays, and the ping
        public string OwnLead;                        // Pawn Rush: this PC's pawn drawn sooner (P3, F7); empty where not offered
        // R521: for the card.
        public int Receivers;                         // the host: the players its last snapshot went to (bots and itself not counted)
        public RouteSummary Routes;                   // Route as numbers (RouteSummary.None where it is not Steam)
    }

    /// <summary>R306: this run's "-latencyBefore" (LatencyBefore; a development build's, nothing in a release build), read on
    /// first use - on the main thread, not in a static initialiser.</summary>
    public static class LatencyTest
    {
        /// <summary>R306 [4]: "-queuedFrames=1" (1 to 3) - how many frames the graphics driver may queue ahead
        /// (QualitySettings.maxQueuedFrames: Direct3D 11 and 12 and Vulkan only, 2 on PC unless set), a development build's
        /// A/B switch for the input lag a queued frame adds against the frames it may cost. The default stays.</summary>
        public const string QueuedFramesArgument = "-queuedFrames=";

        static bool read;
        static LatencyBefore before;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyQueuedFrames()
        {
            if (!Debug.isDebugBuild) return;
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg != null && arg.StartsWith(QueuedFramesArgument, StringComparison.Ordinal)
                    && int.TryParse(arg.Substring(QueuedFramesArgument.Length), out int frames) && frames >= 1 && frames <= 3)
                    QualitySettings.maxQueuedFrames = frames;
        }

        public static LatencyBefore Before
        {
            get
            {
                if (!read)
                {
                    read = true;
                    before = Debug.isDebugBuild ? LatencyBefore.Parse(Environment.GetCommandLineArgs()) : default;
                }
                return before;
            }
        }
    }

    /// <summary>
    /// R297 (HOST_AUTHORITY P5): a development build's metrics box in a match - this PC's frame and physics step time, its
    /// managed allocations and the link's numbers (NetMetrics), for the baselines in VALIDATION. A match link adds it
    /// (Attach); a release build never does. R521: F3 first shows a card with the few numbers that say how the match goes
    /// online, each green, orange or red by NetHealth's lines, the worst of them at its top; F3 again shows the full lines
    /// the measurements read (as they were); F3 a third time hides it. Both are sized to the screen's height (1 at 1080p).
    /// The physics step is timed from the last script's FixedUpdate (this one runs last) to the WaitForFixedUpdate after
    /// the step, so the physics loop runs as it always does (PhysicsStepProfiler runs it by hand instead, for the
    /// command-line load test only). The text is made once a second and drawn in the repaint only, so the box adds little
    /// garbage of its own while it is shown.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class NetMetricsOverlay : MonoBehaviour
    {
        // Free in both matches: the lab's own F3 panel is for its test rooms only (LabNetLink, not RealMatch).
        const KeyCode Toggle = KeyCode.F3;
        // R521: the card's sizes at 1080p, px (the scale multiplies them).
        const float Margin = 16f, Pad = 16f, HeadHeight = 30f, RowHeight = 32f, LineHeight = 22f, FootHeight = 20f, Gap = 10f;
        const float DotSize = 10f, ChipHeight = 24f, MinWidth = 300f, Corner = 12f;
        const int NameSize = 15, NoteSize = 13, ValueSize = 19, HeadSize = 16, ChipSize = 13, LineSize = 14, FootSize = 13;

        enum View { Hidden, Card, Lines }

        /// <summary>A row of the card: its name and a grey note after it, its value and dot in its grade's colour.</summary>
        struct Row
        {
            public string Name, Note, Value;
            public NetGrade Grade;
        }

        // R521: a dark panel, light text, and the three grades' colours.
        static readonly Color Panel = new Color32(0x10, 0x15, 0x1D, 0xEB);
        static readonly Color Ink = new Color32(0xEE, 0xF2, 0xF7, 0xFF);
        static readonly Color Muted = new Color32(0x93, 0x9E, 0xAF, 0xFF);
        static readonly Color Divider = new Color(1f, 1f, 1f, 0.09f);
        static readonly Color GoodColour = new Color32(0x3D, 0xDC, 0x84, 0xFF);
        static readonly Color WarnColour = new Color32(0xFF, 0xA2, 0x3A, 0xFF);
        static readonly Color BadColour = new Color32(0xFF, 0x5A, 0x5F, 0xFF);
        static readonly Color NoneColour = new Color32(0x6B, 0x75, 0x86, 0xFF);
        static readonly Color HostChip = new Color32(0xF2, 0xC1, 0x4E, 0xFF);     // the game's gold
        static readonly Color ClientChip = new Color32(0x7C, 0xA6, 0xFF, 0xFF);
        static readonly Color ChipInk = new Color32(0x14, 0x17, 0x1F, 0xFF);

        // One OS font for every box (statics are kept by Resources.UnloadUnusedAssets; made again if it went anyway).
        static Font font;
        // R521: this project draws in linear colour space, where IMGUI's text comes out as if its colour were turned from
        // sRGB to linear once more (the orange #FFA23A drew as #FF5C0A, the grey notes almost black) while the dots and
        // panels beside it draw true. Text colours are gamma-encoded first so both match.
        static bool linearText;

        Func<NetMetrics> source;
        View view;
        readonly string[] lines = new string[10];
        int lineCount;
        readonly Row[] rows = new Row[6];
        int rowCount;
        bool host;
        string players = "", foot = "";
        NetGrade overall;
        float frameMs = -1f;
        // R521: the current second's start and end (0 until the box's first frame), the frames in it and its average, and
        // how many seconds have been measured (the first is not shown).
        double windowStart, statsAt, fps = -1d;
        int frames, windows;
        // R521: every character the box can draw has been measured once (Warm).
        bool warmed;
        readonly Stopwatch physics = new Stopwatch();
        WaitForFixedUpdate afterPhysics;
        double stepTotal, stepMax, stepAverage, stepLargest, stepsDue, pace = -1d;
        int steps, stepRate;
        // The managed allocations of a frame (a development build's counter; none in a release build).
        ProfilerRecorder gcAllocated;
        long gcBytes, gcPerFrame = -1;
        int gcFrames, collectionsAtStart;
        // R521: the styles and textures at this scale, and the widths of this second's text.
        float scale;
        GUIStyle panelStyle, nameStyle, noteStyle, valueStyle, headStyle, chipStyle, lineStyle, footStyle, keyStyle;
        Texture2D panelTexture, discTexture, pillTexture;
        readonly GUIContent measure = new GUIContent();
        bool measured;
        float cardWidth, linesWidth, valueWidth;

        /// <summary>Adds the box to a match link's object in a development build (nothing in a release build).</summary>
        public static NetMetricsOverlay Attach(GameObject on, Func<NetMetrics> source)
        {
            if (!Debug.isDebugBuild || on == null || source == null) return null;
            var overlay = on.GetComponent<NetMetricsOverlay>();
            if (overlay == null) overlay = on.AddComponent<NetMetricsOverlay>();
            overlay.source = source;
            return overlay;
        }

        // GUI only, no GUILayout: no layout pass every frame.
        void Awake() => useGUILayout = false;

        static Font MakeFont() => Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 16);

        void OnEnable()
        {
            gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            collectionsAtStart = GC.CollectionCount(0);
            statsAt = 0d;
            windows = 0;
            fps = pace = -1d;
            if (afterPhysics == null) afterPhysics = new WaitForFixedUpdate();
            StartCoroutine(AfterPhysics());
        }

        void OnDisable()
        {
            gcAllocated.Dispose();
            physics.Reset();
        }

        void OnDestroy() => DropTextures();

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
            double now = Time.realtimeSinceStartupAsDouble;
            // R521: the box's first frame is mostly the scene's load and the physics steps before it caught up on that load,
            // so measuring starts after it. The second after it is measured but not shown: Unity still runs its first frames
            // on a set 0.02 s while real time races on (1 fps, 1% physics in a test build), and the box warms its text then.
            bool first = statsAt <= 0d;
            if (!first)
            {
                float dtMs = Mathf.Min(Time.unscaledDeltaTime * 1000f, 250f);
                frameMs = frameMs < 0f ? dtMs : frameMs + (dtMs - frameMs) * 0.05f;
                frames++;
                // R521: the physics steps real time asked for this second; the pace is the steps taken over them.
                if (Time.fixedDeltaTime > 0f) stepsDue += Time.unscaledDeltaTime * Time.timeScale / Time.fixedDeltaTime;
                if (gcAllocated.Valid)
                {
                    gcBytes += gcAllocated.LastValue;
                    gcFrames++;
                }
            }
            bool build = false;
            if (LegacyKeys.Down(Toggle) && !ChatBox.KeysHeld)
            {
                // R521: the card, then the full lines, then nothing.
                view = view == View.Hidden ? View.Card : view == View.Card ? View.Lines : View.Hidden;
                build = view != View.Hidden;
            }
            if (first)
            {
                statsAt = now + 1d;
                windowStart = now;
                steps = 0;
                stepTotal = stepMax = 0d;
            }
            else if (now >= statsAt)
            {
                statsAt = now + 1d;
                bool shown = ++windows > 1;
                fps = shown ? frames / (now - windowStart) : -1d;
                windowStart = now;
                stepRate = steps;
                stepAverage = steps > 0 ? stepTotal / steps : 0d;
                stepLargest = stepMax;
                pace = shown && stepsDue >= 1d ? steps / stepsDue : -1d;
                gcPerFrame = gcFrames > 0 ? gcBytes / gcFrames : -1;
                frames = steps = gcFrames = 0;
                stepTotal = stepMax = stepsDue = 0d;
                gcBytes = 0;
                build = view != View.Hidden;
            }
            if (build) Build();
        }

        void Build()
        {
            var m = source();
            host = m.Host;
            BuildLines(m);
            BuildCard(m);
            measured = false;
        }

        /// <summary>The full lines (R297, R306) - the ones the measurements in VALIDATION read, unchanged.</summary>
        void BuildLines(NetMetrics m)
        {
            lineCount = 0;
            var before = LatencyTest.Before;
            Line($"측정 (F3) · {(m.Host ? "방장" : "참가자")} · {m.Players}명" + (m.Piloted ? $" · 자동 입력 (시드 {m.PilotSeed})" : "")
                 + (before.Any ? $" · R306 전 동작 {before.Items}" : ""));
            Line($"프레임 {frameMs:F1} ms · 물리 {stepAverage:F2} ms/스텝 (최대 {stepLargest:F2}) · {stepRate}스텝/초"
                 + $" · 그래픽 대기 {QualitySettings.maxQueuedFrames}프레임 ({SystemInfo.graphicsDeviceType})");
            if (m.Host)
            {
                Line($"스냅샷 {m.SnapshotBytes}바이트 · 초당 {m.SnapshotsSent}번 보냄 (목표 {m.SnapshotRateTarget}번)");
                Line($"보내기 {m.SentBytes / 1024f:F1} KB/초 · 받기 {m.ReceivedBytes / 1024f:F1} KB/초");
                Line($"입력 받음 → 물리 적용 {Ms(m.ApplyWaitMs)}");
            }
            else
            {
                Line($"왕복 지연 {m.RoundTripMs:F0} ms · 재생 지연 {m.PlaybackDelayMs:F0} ms · 흔들림 {m.JitterMs:F1} ms · 손실 {m.LossShare * 100d:F1}%");
                Line($"받기 {m.ReceivedBytes / 1024f:F1} KB/초 (스냅샷 초당 {m.SnapshotsReceived}개 · 간격 {m.IntervalMs:F1} ms · {m.SnapshotBytes}바이트) · 보내기 {m.SentBytes / 1024f:F1} KB/초");
                Line($"누름 → 방장 반응 {Ms(m.Answered)} (마지막 {Ms(m.AnsweredLast)}) · 내 몸이 움직여 보이기까지 {Ms(m.Seen)} · {m.PressesTimed}번 · 놓침 {m.PressesLost}");
                if (!string.IsNullOrEmpty(m.OwnLead)) Line(m.OwnLead);
            }
            Line("연결 " + (string.IsNullOrEmpty(m.Route) ? "-" : m.Route));
            int collections = GC.CollectionCount(0) - collectionsAtStart;
            Line(gcPerFrame >= 0 ? $"GC 할당 {gcPerFrame / 1024f:F2} KB/프레임 · 경기 중 GC 수집 {collections}번"
                                 : $"GC 할당 - (개발 빌드에서만 측정) · 경기 중 GC 수집 {collections}번");
        }

        void Line(string line)
        {
            if (lineCount < lines.Length) lines[lineCount++] = line;
        }

        /// <summary>A latency for the box ("-" before the first sample).</summary>
        static string Ms(double ms) => ms < 0d ? "-" : ms.ToString("0.0") + " ms";

        /// <summary>R521: the card - the few numbers that say how the match goes online, each graded (NetHealth). The host
        /// keeps the match going for everyone: its frames, its physics against real time, what it sends and its players'
        /// worst ping. A client sees what reaches it: its ping, how long a press takes to show, loss, jitter, the snapshots
        /// it gets and its own frames.</summary>
        void BuildCard(NetMetrics m)
        {
            rowCount = 0;
            players = m.Players + "명";
            if (m.Host)
            {
                AddRow("화면 프레임", "", Whole(fps, " fps"), NetHealth.Fps(fps));
                AddRow("물리 속도", "실제 시간 대비", Whole(Math.Min(pace, 1d) * 100d, "%"), NetHealth.Pace(pace));
                if (m.Receivers > 0)
                {
                    AddRow("경기 모습 보내기", "목표 초당 " + m.SnapshotRateTarget + "번", "초당 " + m.SnapshotsSent + "번",
                           NetHealth.Snapshots(m.SnapshotsSent, m.SnapshotRateTarget));
                    AddRow("업로드", "", (m.SentBytes / 1024f).ToString("0.0") + " KB/s", NetHealth.Upload(m.SentBytes / 1024d));
                    var route = m.Routes;
                    // Steam's ping, 0 on a LAN (a round trip of the game's own is never 0, so NetHealth.Ping reads 0 as none).
                    AddRow("참가자 최고 핑", route.Known ? "직접 " + route.Direct + " · 릴레이 " + route.Relayed : "",
                           route.Known ? Whole(route.PingMs, " ms") : "—",
                           route.Known ? NetHealth.AtMost(route.PingMs, NetHealth.PingGood, NetHealth.PingWarn) : NetGrade.None);
                }
                else AddRow("참가자", "아직 없음 (봇만)", "—", NetGrade.None);
            }
            else
            {
                var route = m.Routes;
                AddRow("핑", route.Known ? (route.Relayed > 0 ? "릴레이 연결" : "직접 연결") : "",
                       m.RoundTripMs > 0f ? Whole(m.RoundTripMs, " ms") : "—", NetHealth.Ping(m.RoundTripMs));
                AddRow("조작 반응", "누른 뒤 보이기까지", Whole(m.Seen, " ms"), NetHealth.Felt(m.Seen));
                AddRow("손실", "", (m.LossShare * 100d).ToString("0.0") + "%", NetHealth.Loss(m.LossShare));
                AddRow("흔들림", "", Whole(m.JitterMs, " ms"), NetHealth.Jitter(m.JitterMs));
                AddRow("경기 모습 받기", "", "초당 " + m.SnapshotsReceived + "번",
                       NetHealth.Snapshots(m.SnapshotsReceived, NetHealth.TargetFromInterval(m.IntervalMs)));
                AddRow("화면 프레임", "", Whole(fps, " fps"), NetHealth.Fps(fps));
            }
            overall = NetGrade.None;
            for (int i = 0; i < rowCount; i++) overall = NetHealth.Worse(overall, rows[i].Grade);
            var before = LatencyTest.Before;
            foot = m.Piloted ? "자동 입력 (시드 " + m.PilotSeed + ")" : "";
            if (before.Any) foot += (foot.Length > 0 ? " · " : "") + "R306 전 동작 " + before.Items;
        }

        void AddRow(string name, string note, string value, NetGrade grade)
        {
            if (rowCount < rows.Length) rows[rowCount++] = new Row { Name = name, Note = note, Value = value, Grade = grade };
        }

        /// <summary>A whole number and its unit ("—" while there is none).</summary>
        static string Whole(double value, string unit) => value >= 0d ? value.ToString("0") + unit : "—";

        static Color GradeColour(NetGrade grade) =>
            grade == NetGrade.Good ? GoodColour : grade == NetGrade.Warn ? WarnColour : grade == NetGrade.Bad ? BadColour : NoneColour;

        static string Verdict(NetGrade grade) =>
            grade == NetGrade.Good ? "좋음" : grade == NetGrade.Warn ? "주의" : grade == NetGrade.Bad ? "위험" : "재는 중";

        // ------------------------------------------------------------------ drawing (R521)

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!warmed) Warm();
            if (view == View.Hidden) return;
            if (view == View.Card ? rowCount == 0 : lineCount == 0) return;
            float want = Mathf.Clamp(Screen.height / 1080f, 0.8f, 2.5f);
            if (panelStyle == null || Mathf.Abs(want - scale) > 0.01f) MakeStyles(want);
            if (!measured) Measure();
            var colour = GUI.color;
            if (view == View.Card) DrawCard();
            else DrawLines();
            GUI.color = colour;
        }

        /// <summary>R521: measures, once, every character the box can draw, on its first frame (the match scene's start). Unity
        /// 6's IMGUI makes a character the first time text with it is measured or drawn - 1.5 to 3 ms apiece in a test build,
        /// then free at every size and weight - so the card's first F3 took 0.9 s: a stall the host's whole match would have
        /// felt, and that the card's next second would have shown in red. The texts come from the box's own builders with
        /// sample numbers; the sample rows are dropped after.</summary>
        void Warm()
        {
            warmed = true;
            if (panelStyle == null) MakeStyles(Mathf.Clamp(Screen.height / 1080f, 0.8f, 2.5f));
            var someone = new RouteSummary { Direct = 1, Relayed = 1, PingMs = 1 };
            var samples = new[]
            {
                new NetMetrics { Host = true, Players = 12, Receivers = 1, Piloted = true, Routes = someone,
                                 Route = "직접 9명 · 릴레이 2명 · Steam 핑 최대 183 ms · 참가자와 연결 정보 없음" },
                new NetMetrics { Host = true, Players = 8, Routes = RouteSummary.None },
                new NetMetrics { Host = false, Players = 6, Routes = someone, Route = "릴레이 · 방장과 연결 정보 없음",
                                 OwnLead = "내 폰 먼저 그리기 (P3, F7): 켜짐 · 12.3 ms 먼저 (장애물도 내 폰 시각) 꺼짐" },
            };
            foreach (var m in samples)
            {
                BuildLines(m);
                BuildCard(m);
                for (int i = 0; i < lineCount; i++) Width(lineStyle, lines[i]);
                for (int i = 0; i < rowCount; i++) Width(lineStyle, rows[i].Name + rows[i].Note + rows[i].Value);
                Width(lineStyle, players + foot);
            }
            Width(lineStyle, "좋음 주의 위험 재는 중 방장 참가자 F3 자세히 닫기 — 0123456789");
            rowCount = lineCount = 0;
            measured = false;
            if (view != View.Hidden) Build();
        }

        void DrawCard()
        {
            float s = scale, pad = Mathf.Round(Pad * s), rowH = Mathf.Round(RowHeight * s), headH = Mathf.Round(HeadHeight * s);
            float gap = Mathf.Round(Gap * s), footH = Mathf.Round(FootHeight * s), rule = Mathf.Max(1f, Mathf.Round(s));
            float chipH = Px(ChipHeight), dot = Px(DotSize);
            var card = new Rect(Mathf.Round(Margin * s), Mathf.Round(Margin * s), cardWidth,
                                pad + headH + gap + rule + gap * 0.5f + rowCount * rowH + gap * 0.5f + footH + Mathf.Round(pad * 0.6f));
            Fill(card, Panel);
            float x = card.x + pad, right = card.xMax - pad, y = card.y + pad;

            // The head: this PC's part and the players on the left, the worst grade on the right.
            float chipY = Mathf.Round(y + (headH - chipH) / 2f);
            string role = host ? "방장" : "참가자";
            var roleChip = new Rect(x, chipY, Mathf.Round(ChipWidth(role)), chipH);
            Pill(roleChip, host ? HostChip : ClientChip);
            Label(roleChip, role, chipStyle, ChipInk);
            Label(new Rect(roleChip.xMax + Mathf.Round(8f * s), y, right - roleChip.xMax, headH), players, headStyle, Ink);
            var tint = GradeColour(overall);
            string verdict = Verdict(overall);
            float statusW = Mathf.Round(StatusWidth());
            var status = new Rect(right - statusW, chipY, statusW, chipH);
            Pill(status, new Color(tint.r, tint.g, tint.b, 0.16f));
            Disc(status.x + chipH / 2f + dot / 2f, chipY + chipH / 2f, tint);
            Label(new Rect(status.x + chipH / 2f + dot + Mathf.Round(6f * s), chipY, Width(chipStyle, verdict), chipH), verdict, chipStyle, tint);
            y += headH + gap;

            GUI.color = Divider;
            GUI.DrawTexture(new Rect(x, y, right - x, rule), Texture2D.whiteTexture);
            y += rule + gap * 0.5f;

            for (int i = 0; i < rowCount; i++)
            {
                var row = rows[i];
                var colour = GradeColour(row.Grade);
                Disc(x + dot / 2f, y + rowH / 2f, colour);
                float nameX = x + dot + Mathf.Round(10f * s), nameW = Width(nameStyle, row.Name);
                Label(new Rect(nameX, y, nameW, rowH), row.Name, nameStyle, Ink);
                if (row.Note.Length > 0)
                    Label(new Rect(nameX + nameW + Mathf.Round(6f * s), y + Mathf.Round(s), right - nameX - nameW, rowH), row.Note, noteStyle, Muted);
                Label(new Rect(right - valueWidth * 2f, y, valueWidth * 2f, rowH), row.Value, valueStyle, row.Grade == NetGrade.None ? Muted : colour);
                y += rowH;
            }
            y += gap * 0.5f;
            var footRow = new Rect(x, y, right - x, footH);
            if (foot.Length > 0) Label(footRow, foot, footStyle, Muted);
            Label(footRow, "F3  자세히", keyStyle, Muted);
        }

        void DrawLines()
        {
            float s = scale, pad = Mathf.Round(Pad * s), lineH = Mathf.Round(LineHeight * s), footH = Mathf.Round(FootHeight * s);
            var card = new Rect(Mathf.Round(Margin * s), Mathf.Round(Margin * s), linesWidth,
                                pad + lineCount * lineH + Mathf.Round(Gap * s * 0.5f) + footH + Mathf.Round(pad * 0.6f));
            Fill(card, Panel);
            float x = card.x + pad, w = card.width - 2f * pad, y = card.y + pad;
            for (int i = 0; i < lineCount; i++)
                Label(new Rect(x, y + i * lineH, w, lineH), lines[i], lineStyle, i == 0 ? (host ? HostChip : ClientChip) : Ink);
            y += lineCount * lineH + Mathf.Round(Gap * s * 0.5f);
            Label(new Rect(x, y, w, footH), "F3  닫기", keyStyle, Muted);
        }

        /// <summary>The card's width for this second's rows (a value column wide enough for the widest value, rounded up
        /// to 20 px steps so it does not twitch as the numbers change) and the lines' width.</summary>
        void Measure()
        {
            measured = true;
            float s = scale, widest = 0f;
            for (int i = 0; i < rowCount; i++)
            {
                float w = Width(nameStyle, rows[i].Name) + valueWidth + 20f * s;
                if (rows[i].Note.Length > 0) w += 6f * s + Width(noteStyle, rows[i].Note);
                if (w > widest) widest = w;
            }
            float head = ChipWidth(host ? "방장" : "참가자") + 8f * s + Width(headStyle, players) + 20f * s + StatusWidth();
            float content = Mathf.Max(widest + Px(DotSize) + 10f * s, head) + 2f * Mathf.Round(Pad * s);
            float step = 20f * s;
            cardWidth = Mathf.Round(Mathf.Max(MinWidth * s, Mathf.Ceil(content / step) * step));
            float longest = 0f;
            for (int i = 0; i < lineCount; i++) longest = Mathf.Max(longest, Width(lineStyle, lines[i]));
            linesWidth = Mathf.Round(Mathf.Min(longest + 2f * Mathf.Round(Pad * s), Screen.width - 2f * Margin * s));
        }

        float ChipWidth(string word) => Width(chipStyle, word) + Px(ChipHeight);

        float StatusWidth() => Px(ChipHeight) + Px(DotSize) + Mathf.Round(6f * scale) + Width(chipStyle, Verdict(overall));

        float Width(GUIStyle style, string text)
        {
            measure.text = text;
            return Mathf.Ceil(style.CalcSize(measure).x);
        }

        /// <summary>A size at this scale in whole pixels, even (a disc's middle then falls between two pixels).</summary>
        float Px(float size) => Mathf.Max(2, Mathf.RoundToInt(size * scale / 2f) * 2);

        void Fill(Rect rect, Color colour)
        {
            GUI.color = colour;
            panelStyle.Draw(rect, false, false, false, false);
        }

        void Disc(float centreX, float centreY, Color colour)
        {
            float size = discTexture.width;
            GUI.color = colour;
            GUI.DrawTexture(new Rect(Mathf.Round(centreX - size / 2f), Mathf.Round(centreY - size / 2f), size, size), discTexture);
        }

        /// <summary>A rectangle with round ends in one colour: the halves of a disc as tall as it, a fill between them.</summary>
        void Pill(Rect rect, Color colour)
        {
            float h = rect.height, half = h / 2f;
            GUI.color = colour;
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y, half, h), pillTexture, new Rect(0f, 0f, 0.5f, 1f));
            GUI.DrawTexture(new Rect(rect.x + half, rect.y, rect.width - h, h), Texture2D.whiteTexture);
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - half, rect.y, half, h), pillTexture, new Rect(0.5f, 0f, 0.5f, 1f));
        }

        static void Label(Rect rect, string text, GUIStyle style, Color colour)
        {
            GUI.color = Color.white;
            style.normal.textColor = linearText ? colour.gamma : colour;
            GUI.Label(rect, text, style);
        }

        void MakeStyles(float s)
        {
            scale = s;
            measured = false;
            if (font == null) font = MakeFont();
            linearText = QualitySettings.activeColorSpace == ColorSpace.Linear;
            DropTextures();
            int corner = Mathf.Max(4, Mathf.RoundToInt(Corner * s));
            panelTexture = Rounded(corner);
            discTexture = DiscTexture((int)Px(DotSize));
            pillTexture = DiscTexture((int)Px(ChipHeight));
            panelStyle = new GUIStyle { border = new RectOffset(corner, corner, corner, corner) };
            panelStyle.normal.background = panelTexture;
            nameStyle = Style(NameSize, FontStyle.Normal, TextAnchor.MiddleLeft);
            noteStyle = Style(NoteSize, FontStyle.Normal, TextAnchor.MiddleLeft);
            valueStyle = Style(ValueSize, FontStyle.Bold, TextAnchor.MiddleRight);
            headStyle = Style(HeadSize, FontStyle.Bold, TextAnchor.MiddleLeft);
            chipStyle = Style(ChipSize, FontStyle.Bold, TextAnchor.MiddleCenter);
            lineStyle = Style(LineSize, FontStyle.Normal, TextAnchor.MiddleLeft);
            footStyle = Style(FootSize, FontStyle.Normal, TextAnchor.MiddleLeft);
            keyStyle = Style(FootSize, FontStyle.Normal, TextAnchor.MiddleRight);
            valueWidth = Mathf.Max(Width(valueStyle, "초당 60번"), Width(valueStyle, "999.9 KB/s"));
        }

        GUIStyle Style(int size, FontStyle weight, TextAnchor anchor)
        {
            var style = new GUIStyle
            {
                font = font, fontSize = Mathf.Max(8, Mathf.RoundToInt(size * scale)), fontStyle = weight, alignment = anchor,
                clipping = TextClipping.Overflow, wordWrap = false, richText = false,
            };
            style.normal.textColor = Ink;
            return style;
        }

        void DropTextures()
        {
            if (panelTexture != null) Destroy(panelTexture);
            if (discTexture != null) Destroy(discTexture);
            if (pillTexture != null) Destroy(pillTexture);
            panelTexture = discTexture = pillTexture = null;
        }

        /// <summary>A white square with corners of <paramref name="radius"/> px and two pixels to stretch between them (the
        /// panel's style 9-slices it), its edge anti-aliased.</summary>
        static Texture2D Rounded(int radius)
        {
            int size = radius * 2 + 2;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float dx = px - Mathf.Clamp(px, radius, size - radius), dy = py - Mathf.Clamp(py, radius, size - radius);
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(radius - Mathf.Sqrt(dx * dx + dy * dy)));
                }
            return MakeTexture(size, pixels);
        }

        /// <summary>A white disc <paramref name="size"/> px across, its edge anti-aliased.</summary>
        static Texture2D DiscTexture(int size)
        {
            var pixels = new Color32[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    pixels[y * size + x] = new Color32(255, 255, 255, Alpha(r - Mathf.Sqrt(dx * dx + dy * dy)));
                }
            return MakeTexture(size, pixels);
        }

        /// <summary>A pixel's cover from how far inside the shape's edge its centre is (px): a pixel's width of blend.</summary>
        static byte Alpha(float inside) => (byte)(Mathf.Clamp01(inside + 0.5f) * 255f);

        static Texture2D MakeTexture(int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
