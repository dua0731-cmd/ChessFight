using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ChessFight.Network;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab.Net
{
    /// <summary>
    /// Host-authoritative ragdoll over Steam for the lab. The host runs the only physics simulation;
    /// clients send 24-byte inputs and draw interpolated poses (63 bytes per pawn) with no local physics.
    /// It boots itself in the RagdollLab scene and stays out of the way until a match actually starts,
    /// so local two-player testing is unchanged.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class SteamRagdollLink : MonoBehaviour
    {
        const int Channel = 32;                 // SteamMotion uses 31; the ragdoll stream is separate.
        const float SnapshotInterval = 1f / 30f;
        const float InputInterval = 1f / 60f;
        const float PlaybackDelayMs = 110f;     // jitter buffer: about three snapshots
        const float InputTimeout = 0.35f;
        const float SilenceTimeout = 12f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name != "RagdollLab") return;
            if (FindFirstObjectByType<SteamRagdollLink>() != null) return;
            new GameObject("ChessFight Ragdoll Net").AddComponent<SteamRagdollLink>();
        }

        struct RemoteInput
        {
            public Vector2 move;
            public bool jump, shove, grab;
            public uint sequence, clientTimeMs;
            public float received;
        }

        SteamSession session;
        LabGame game;
        LabCamera cam;
        Callback<SteamNetworkingMessagesSessionRequest_t> sessionRequests;
        Callback<SteamNetworkingMessagesSessionFailed_t> sessionFailures;

        readonly IntPtr[] incoming = new IntPtr[64];
        readonly Dictionary<ulong, RagdollPawn> pawns = new Dictionary<ulong, RagdollPawn>();
        readonly Dictionary<ulong, RemoteInput> inputs = new Dictionary<ulong, RemoteInput>();
        readonly Dictionary<ulong, RagdollPose> posePool = new Dictionary<ulong, RagdollPose>();
        readonly Dictionary<ulong, int> snapCountdown = new Dictionary<ulong, int>();
        readonly List<RagdollPose> outgoing = new List<RagdollPose>();
        readonly List<RagdollSnapshot> buffer = new List<RagdollSnapshot>();
        readonly Stack<RagdollSnapshot> spare = new Stack<RagdollSnapshot>();
        readonly RagdollPose blended = new RagdollPose();
        readonly HashSet<ulong> connected = new HashSet<ulong>();

        bool matchActive;
        uint tick, sequence, lastTick;
        float lastSnapshotSend, lastInputSend, lastReceive, statsAt;
        double playbackMs;
        Vector2 pendingMove;
        bool pendingJump, pendingShove, pendingGrab;
        int sentBytes, receivedBytes, snapshotsIn;
        int sentRate, receivedRate, snapshotRate;
        float roundTripMs;
        string roomCode = "";
        string message = "";
        bool hudOpen = true;
        GUIStyle label, small, button;
        Texture2D panel;

        static uint NowMs() => (uint)(Time.realtimeSinceStartupAsDouble * 1000d);

        void Awake()
        {
            game = FindFirstObjectByType<LabGame>();
            cam = FindFirstObjectByType<LabCamera>();
            session = new SteamSession();
            session.Initialize();
            if (!session.Online) return;
            sessionRequests = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(c =>
            {
                ulong id = c.m_identityRemote.GetSteamID64();
                if (session.IsPeer(id)) SteamNetworkingMessages.AcceptSessionWithUser(ref c.m_identityRemote);
            });
            sessionFailures = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(c =>
                message = "스팀 연결 실패. 방을 나갔다가 다시 시도해 주세요.");
            session.SessionChanged += ResetStream;
        }

        void OnDestroy()
        {
            if (session != null)
            {
                session.SessionChanged -= ResetStream;
                ResetStream();
                session.Dispose();
            }
            sessionRequests?.Dispose();
            sessionFailures?.Dispose();
            if (panel != null) Destroy(panel);
        }

        // ---------------------------------------------------------------- frame loop

        void Update()
        {
            if (session == null || game == null) return;
            if (Input.GetKeyDown(KeyCode.F3)) hudOpen = !hudOpen;
            // The lab locks the cursor for mouse-look; this panel needs it back or its buttons never get clicked.
            game.UiWantsCursor = hudOpen && !game.AutoTest;
            session.Tick();
            if (!session.Online) return;
            Receive();

            bool active = session.Started && session.Match != 0 && session.Roster.Count > 0;
            if (active && !matchActive) EnterMatch();
            else if (!active && matchActive) ExitMatch();
            if (!matchActive) return;

            SyncRoster();
            if (session.IsHost) HostFrame();
            else ClientFrame();
            UpdateStats();
        }

        void FixedUpdate()
        {
            if (!matchActive || session == null || !session.IsHost) return;
            float now = Time.realtimeSinceStartup;
            foreach (var pair in pawns)
            {
                if (pair.Key == session.Self || pair.Value == null) continue;
                if (!inputs.TryGetValue(pair.Key, out var remote) || now - remote.received > InputTimeout)
                {
                    pair.Value.SetInput(default);
                    continue;
                }
                pair.Value.SetInput(new PawnInput
                {
                    move = new Vector3(remote.move.x, 0f, remote.move.y),
                    jump = remote.jump,
                    shove = remote.shove,
                    grab = remote.grab,
                });
                remote.jump = false;
                remote.shove = false;
                inputs[pair.Key] = remote;
            }
            foreach (var pair in pawns)
            {
                if (pair.Value == null || pair.Value.Hips.position.y >= LabLayout.KillHeight) continue;
                var spot = session.Roster.TryGetValue(pair.Key, out var entry) ? entry : default;
                Respawn(pair.Value, spot.Team, spot.Slot);
            }
        }

        void HostFrame()
        {
            if (pawns.TryGetValue(session.Self, out var mine) && mine != null)
                mine.SetInput(game.ReadPlayerInput(0));
            if (Input.GetKeyDown(KeyCode.R))
                foreach (var pair in pawns)
                {
                    var spot = session.Roster.TryGetValue(pair.Key, out var entry) ? entry : default;
                    Respawn(pair.Value, spot.Team, spot.Slot);
                }

            if (Time.realtimeSinceStartup - lastSnapshotSend < SnapshotInterval) return;
            lastSnapshotSend = Time.realtimeSinceStartup;
            uint stamp = NowMs();
            outgoing.Clear();
            foreach (var pair in pawns)
            {
                if (pair.Value == null) continue;
                var pose = Pose(pair.Key);
                pair.Value.CaptureNetworkPose(pose);
                pose.id = pair.Key;
                pose.ack = inputs.TryGetValue(pair.Key, out var remote) ? remote.clientTimeMs : stamp;
                if (pair.Value.NetworkSnap)
                {
                    snapCountdown[pair.Key] = 3;   // unreliable packets: repeat the teleport flag
                    pair.Value.NetworkSnap = false;
                }
                int left = snapCountdown.TryGetValue(pair.Key, out int n) ? n : 0;
                pose.snap = left > 0;
                if (left > 0) snapCountdown[pair.Key] = left - 1;
                outgoing.Add(pose);
            }
            if (outgoing.Count == 0) return;
            byte[] bytes = RagdollNetProtocol.Snapshot(session.Match, ++tick, stamp, outgoing);
            foreach (ulong id in session.Roster.Keys)
                if (id != session.Self) Send(id, bytes);
        }

        void ClientFrame()
        {
            var local = game.ReadPlayerInput(0);
            pendingMove = new Vector2(local.move.x, local.move.z);
            pendingJump |= local.jump;
            pendingShove |= local.shove;
            pendingGrab = local.grab;

            if (Time.realtimeSinceStartup - lastInputSend >= InputInterval)
            {
                lastInputSend = Time.realtimeSinceStartup;
                byte[] bytes = RagdollNetProtocol.Input(session.Match, ++sequence, NowMs(), pendingMove, pendingJump, pendingShove, pendingGrab);
                Send(session.Host, bytes);
                pendingJump = false;
                pendingShove = false;
            }

            Playback();

            if (Time.realtimeSinceStartup - lastReceive > SilenceTimeout)
            {
                message = "호스트가 자세 전송을 멈췄어요. 파티로 돌아갑니다.";
                session.Cancel();
            }
        }

        /// <summary>Render the buffer a fixed delay behind the newest snapshot, speeding up or slowing
        /// down the playback clock instead of syncing clocks with the host.</summary>
        void Playback()
        {
            if (buffer.Count == 0) return;
            var newest = buffer[buffer.Count - 1];
            if (playbackMs <= 0d) playbackMs = newest.hostTimeMs - PlaybackDelayMs;
            double target = newest.hostTimeMs - PlaybackDelayMs;
            double rate = playbackMs < target - 60d ? 1.15d : playbackMs > target + 60d ? 0.9d : 1d;
            playbackMs += Time.unscaledDeltaTime * 1000d * rate;
            if (playbackMs > newest.hostTimeMs) playbackMs = newest.hostTimeMs;

            RagdollSnapshot from = buffer[0], to = buffer[0];
            for (int i = 0; i < buffer.Count - 1; i++)
            {
                if (playbackMs < buffer[i].hostTimeMs || playbackMs > buffer[i + 1].hostTimeMs) continue;
                from = buffer[i];
                to = buffer[i + 1];
                break;
            }
            if (buffer.Count == 1) from = to = buffer[0];
            else if (playbackMs >= buffer[buffer.Count - 1].hostTimeMs) from = to = buffer[buffer.Count - 1];

            float span = Mathf.Max(1f, to.hostTimeMs - from.hostTimeMs);
            float t = Mathf.Clamp01((float)(playbackMs - from.hostTimeMs) / span);
            for (int i = 0; i < to.count; i++)
            {
                var latest = to.At(i);
                if (!pawns.TryGetValue(latest.id, out var pawn) || pawn == null) continue;
                var source = Find(from, latest.id);
                if (latest.snap || source == null)
                {
                    pawn.ApplyNetworkPose(latest);
                    playbackMs = newest.hostTimeMs;
                }
                else
                {
                    RagdollPose.Blend(source, latest, t, blended);
                    pawn.ApplyNetworkPose(blended);
                }
                if (latest.id == session.Self && latest.ack != 0)
                    roundTripMs = Mathf.Lerp(roundTripMs, Mathf.Max(0f, NowMs() - latest.ack), 0.2f);
            }

            while (buffer.Count > 2 && buffer[0].hostTimeMs < playbackMs - 400d) Recycle(0);
            while (buffer.Count > 16) Recycle(0);
        }

        static RagdollPose Find(RagdollSnapshot snapshot, ulong id)
        {
            for (int i = 0; i < snapshot.count; i++)
                if (snapshot.At(i).id == id) return snapshot.At(i);
            return null;
        }

        // ---------------------------------------------------------------- match lifecycle

        void EnterMatch()
        {
            matchActive = true;
            pawns.Clear();
            game.DespawnAll();
            game.NetworkControlled = true;
            ResetStream();
            message = session.IsHost ? "호스트로 경기를 시작했어요." : "호스트에 접속했어요.";
        }

        void ExitMatch()
        {
            matchActive = false;
            foreach (var pawn in pawns.Values) if (pawn != null) Destroy(pawn.gameObject);
            pawns.Clear();
            if (cam != null) cam.soloTarget = null;
            game.NetworkControlled = false;
            game.DespawnAll();
            game.SpawnLocalPlayers();
            ResetStream();
        }

        void SyncRoster()
        {
            foreach (ulong id in new List<ulong>(pawns.Keys))
            {
                if (session.Roster.ContainsKey(id) && pawns[id] != null) continue;
                if (pawns[id] != null) Destroy(pawns[id].gameObject);
                pawns.Remove(id);
                inputs.Remove(id);
            }
            foreach (var entry in session.Roster)
            {
                if (pawns.ContainsKey(entry.Key)) continue;
                var pawn = game.Spawn(LabLayout.NetSpawn(entry.Value.Team, entry.Value.Slot),
                    LabLayout.NetFacing(entry.Value.Team), game.TeamMaterial(entry.Value.Team), session.Name(entry.Key));
                if (!session.IsHost) pawn.SetNetworkPuppet(true);
                pawns[entry.Key] = pawn;
                if (entry.Key == session.Self && cam != null) cam.soloTarget = pawn;
            }
        }

        void Respawn(RagdollPawn pawn, int team, int slot)
        {
            if (pawn == null) return;
            pawn.Teleport(LabLayout.NetSpawn(team, slot) + Vector3.up * (pawn.standHeight + 0.02f), LabLayout.NetFacing(team));
        }

        RagdollPose Pose(ulong id)
        {
            if (!posePool.TryGetValue(id, out var pose)) posePool[id] = pose = new RagdollPose();
            return pose;
        }

        // ---------------------------------------------------------------- transport

        void Send(ulong id, byte[] bytes)
        {
            if (id == 0 || id == session.Self) return;
            var remote = new SteamNetworkingIdentity();
            remote.SetSteamID64(id);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var result = SteamNetworkingMessages.SendMessageToUser(ref remote, handle.AddrOfPinnedObject(), (uint)bytes.Length,
                    Constants.k_nSteamNetworkingSend_Unreliable | Constants.k_nSteamNetworkingSend_NoNagle, Channel);
                if (result != EResult.k_EResultOK) message = "스팀 전송 오류: " + result;
                else sentBytes += bytes.Length;
                connected.Add(id);
            }
            finally
            {
                handle.Free();
            }
        }

        void Receive()
        {
            int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, incoming, incoming.Length);
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var packet = SteamNetworkingMessage_t.FromIntPtr(incoming[i]);
                    ulong sender = packet.m_identityPeer.GetSteamID64();
                    if (!session.IsPeer(sender) || packet.m_cbSize <= 0 || packet.m_cbSize > RagdollNetProtocol.MaxBytes) continue;
                    var bytes = new byte[packet.m_cbSize];
                    Marshal.Copy(packet.m_pData, bytes, 0, bytes.Length);
                    receivedBytes += bytes.Length;
                    if (session.IsHost) ReceiveInput(sender, bytes);
                    else if (sender == session.Host) ReceiveSnapshot(bytes);
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(incoming[i]);
                    incoming[i] = IntPtr.Zero;
                }
            }
        }

        void ReceiveInput(ulong sender, byte[] bytes)
        {
            if (!session.Roster.ContainsKey(sender)) return;
            if (!RagdollNetProtocol.ReadInput(bytes, session.Match, out uint seq, out uint clientTime, out var move, out bool jump, out bool shove, out bool grab)) return;
            inputs.TryGetValue(sender, out var previous);
            if (previous.sequence != 0 && !RagdollNetProtocol.Newer(seq, previous.sequence)) return;
            inputs[sender] = new RemoteInput
            {
                move = move,
                jump = previous.jump | jump,
                shove = previous.shove | shove,
                grab = grab,
                sequence = seq,
                clientTimeMs = clientTime,
                received = Time.realtimeSinceStartup,
            };
        }

        void ReceiveSnapshot(byte[] bytes)
        {
            var snapshot = spare.Count > 0 ? spare.Pop() : new RagdollSnapshot();
            if (!RagdollNetProtocol.ReadSnapshot(bytes, session.Match, snapshot))
            {
                spare.Push(snapshot);
                return;
            }
            if (lastTick != 0 && !RagdollNetProtocol.Newer(snapshot.tick, lastTick))
            {
                spare.Push(snapshot);
                return;
            }
            lastTick = snapshot.tick;
            lastReceive = Time.realtimeSinceStartup;
            snapshotsIn++;
            buffer.Add(snapshot);
        }

        void Recycle(int index)
        {
            spare.Push(buffer[index]);
            buffer.RemoveAt(index);
        }

        void ResetStream()
        {
            foreach (ulong id in connected)
            {
                var identity = new SteamNetworkingIdentity();
                identity.SetSteamID64(id);
                SteamNetworkingMessages.CloseSessionWithUser(ref identity);
            }
            connected.Clear();
            while (buffer.Count > 0) Recycle(0);
            inputs.Clear();
            snapCountdown.Clear();
            tick = sequence = lastTick = 0;
            playbackMs = 0d;
            roundTripMs = 0f;
            lastReceive = Time.realtimeSinceStartup;
            pendingJump = pendingShove = pendingGrab = false;
        }

        void UpdateStats()
        {
            if (Time.realtimeSinceStartup < statsAt) return;
            statsAt = Time.realtimeSinceStartup + 1f;
            sentRate = sentBytes;
            receivedRate = receivedBytes;
            snapshotRate = snapshotsIn;
            sentBytes = receivedBytes = snapshotsIn = 0;
        }

        // ---------------------------------------------------------------- HUD

        void OnGUI()
        {
            if (session == null || game == null || game.AutoTest) return;
            EnsureStyles();
            // Keep clear of the tuning panel, which owns the left edge while it is open.
            float x = game.PanelOpen ? 500f : 12f;
            if (!hudOpen)
            {
                GUI.Label(new Rect(x, Screen.height - 26f, 600f, 22f), "F3: 온라인 패널", small);
                return;
            }

            float width = 430f, height = matchActive ? 210f : 300f;
            GUILayout.BeginArea(new Rect(x, Screen.height - height - 12f, width, height), GUIContent.none, GUI.skin.box);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), panel);
            GUILayout.Label("온라인 (F3으로 닫기)", label);
            GUILayout.Label(session.Online ? $"스팀: {session.Name(session.Self)}" : "스팀에 연결되지 않았어요 (Steam 실행 후 다시 시작)", small);
            GUILayout.Label(session.Status + (string.IsNullOrEmpty(session.Error) ? "" : "  " + session.Error), small);
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, small);

            if (!matchActive)
            {
                bool canQueue = session.Online && session.IsLeader && !session.Busy;
                GUILayout.BeginHorizontal();
                GUI.enabled = canQueue;
                if (GUILayout.Button("테스트 방 만들기", button)) session.FindMatch(true);
                GUI.enabled = session.Match != 0;
                if (GUILayout.Button("방 번호 복사", button))
                {
                    GUIUtility.systemCopyBuffer = session.Match.ToString();
                    message = "방 번호를 복사했어요. 상대에게 보내세요.";
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("방 번호", small, GUILayout.Width(52f));
                GUI.SetNextControlName("netRoomCode");
                roomCode = GUILayout.TextField(roomCode, 24, button);
                GUI.enabled = canQueue && ulong.TryParse(roomCode, out _);
                if (GUILayout.Button("참가", button, GUILayout.Width(60f)) && ulong.TryParse(roomCode, out ulong id)) session.JoinPrivateMatch(id);
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUI.enabled = session.IsHost && !session.Started && session.Roster.Count >= 2;
                if (GUILayout.Button($"시작 ({session.Roster.Count}명)", button)) session.StartGame();
                GUI.enabled = session.Busy;
                if (GUILayout.Button("나가기", button)) session.Cancel();
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.Label($"내 방 번호: {session.Match}   {Hint()}", small);
                game.SuppressInput = GUI.GetNameOfFocusedControl() == "netRoomCode";
            }
            else
            {
                GUILayout.Label(session.IsHost ? "역할: 호스트 (이 PC가 물리를 계산해요)" : "역할: 참가자 (호스트 화면을 받아 그려요)", small);
                GUILayout.Label($"인원 {pawns.Count}명 · 보내기 {sentRate / 1024f:F1} KB/s · 받기 {receivedRate / 1024f:F1} KB/s", small);
                GUILayout.Label(session.IsHost
                    ? $"스냅샷 {1f / SnapshotInterval:F0}Hz 송신 · 폰당 {RagdollNetProtocol.PoseBytes}바이트"
                    : $"스냅샷 수신 {snapshotRate}Hz · 왕복 지연 {roundTripMs:F0} ms · 버퍼 {buffer.Count}개 (지연 {PlaybackDelayMs:F0} ms)", small);
                if (GUILayout.Button("경기 나가기", button)) session.Cancel();
                game.SuppressInput = false;
            }
            GUILayout.EndArea();
        }

        /// <summary>Why a button is greyed out, in one short line.</summary>
        string Hint()
        {
            if (!session.Online) return "스팀 연결이 필요해요";
            if (session.IsHost && !session.Started) return session.Roster.Count >= 2 ? "시작할 수 있어요" : "상대가 들어오길 기다리는 중";
            if (session.Match != 0) return "방에 들어가 있어요 (호스트가 시작을 눌러야 해요)";
            if (session.Searching) return "방을 찾는 중";
            if (!session.IsLeader) return "파티장이 아니에요 (혼자면 자동으로 파티장이에요)";
            if (session.Busy) return "이전 작업 정리 중";
            return "방을 만들거나 번호로 참가하세요";
        }

        void EnsureStyles()
        {
            if (label != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 14);
            panel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            panel.SetPixel(0, 0, new Color(0.06f, 0.09f, 0.14f, 0.92f));
            panel.Apply();
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, fontStyle = FontStyle.Bold };
            label.normal.textColor = new Color(0.9f, 0.94f, 1f);
            small = new GUIStyle(GUI.skin.label) { font = font, fontSize = 12, wordWrap = true };
            small.normal.textColor = new Color(0.76f, 0.82f, 0.9f);
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 12 };
        }
    }
}
