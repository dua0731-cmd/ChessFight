using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ChessFight.Game;
using ChessFight.Network;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab.Net
{
    // Standalone scene owner, like SteamRagdollLink. Never coexist with NetworkRuntime's Steam owner.
    [DefaultExecutionOrder(-150)]
    public sealed class SteamChessTimerLink : MonoBehaviour
    {
        const int Channel = 34;
        ChessTimerMinigame game;
        SteamSession session;
        Callback<SteamNetworkingMessagesSessionRequest_t> requests;
        readonly IntPtr[] incoming = new IntPtr[64];
        readonly Dictionary<ulong, Remote> inputs = new Dictionary<ulong, Remote>();
        readonly Dictionary<ulong, RagdollPose> poses = new Dictionary<ulong, RagdollPose>();
        readonly Dictionary<ulong, int> snaps = new Dictionary<ulong, int>();
        readonly List<RagdollPose> outgoing = new List<RagdollPose>();
        readonly List<RagdollSnapshot> buffer = new List<RagdollSnapshot>();
        readonly Stack<RagdollSnapshot> spare = new Stack<RagdollSnapshot>();
        readonly RagdollPose blend = new RagdollPose();
        RagdollNetInput pending;
        uint sequence, tick, lastPose, lastState;
        float sentAt, stateAt, inputAt, receivedAt, enteredAt, savedMaxStep = -1;
        double playback;
        bool matchActive, panelOpen = true, oldBackground;
        string roomCode = "", message = "";
        GUIStyle label, button;
        Vector2 scroll;
        struct Remote { public RagdollNetInput Input; public uint Sequence; public float At; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; Attach();
        }
        static void OnScene(Scene scene, LoadSceneMode mode) => Attach();
        static void Attach()
        {
            var game = FindFirstObjectByType<ChessTimerMinigame>();
            if (!game || game.GetComponent<SteamChessTimerLink>() || NetworkRuntime.Instance != null) return;
            game.gameObject.AddComponent<SteamChessTimerLink>();
        }
        void Awake()
        {
            game = GetComponent<ChessTimerMinigame>(); oldBackground = Application.runInBackground; Application.runInBackground = true;
            // A private build namespace prevents joining a lobby/lab with different scene semantics.
            session = new SteamSession("chess-timer-v1/" + NetworkRuntime.BuildTag, false);
            session.Initialize(); HostFitnessProbe.Start();
            session.HostChanged += HostChanged;
            game.LeaveNetwork = () => session.Cancel();
        }
        void Update()
        {
            if (!game.NetworkInitialized) return;
            if (Input.GetKeyDown(KeyCode.F2)) panelOpen = !panelOpen;
            game.NetworkPanelOpen = panelOpen;
            if (panelOpen) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (session.LocalFitness == 0) session.LocalFitness = HostFitnessProbe.BaseScore();
            session.ReportFrame(Time.unscaledDeltaTime * 1000);
            session.Tick();
            if (!session.Online) return;
            if (requests == null) requests = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(c =>
            { if (session.IsPeer(c.m_identityRemote.GetSteamID64())) SteamNetworkingMessages.AcceptSessionWithUser(ref c.m_identityRemote); });
            bool active = session.Started && session.Match != 0 && session.Roster.Count > 0;
            if (active && !matchActive)
            {
                ClearStream(); matchActive = true; enteredAt = receivedAt = Time.realtimeSinceStartup;
                game.BeginNetwork(session.IsHost); panelOpen = false; SyncRoster(); HostingGuard(session.IsHost);
            }
            else if (!active && matchActive)
            { matchActive = false; ClearStream(); HostingGuard(false); game.EndNetwork(); panelOpen = true; }
            if (!matchActive) return;
            SyncRoster(); Receive();
            session.ReportLoading(100);
            if (session.CanAnnounceStart && session.StartAt == 0 && MatchStart.ShouldStart(session.Roster.Keys.Select(session.LoadPercent), Time.realtimeSinceStartup - enteredAt))
                session.AnnounceStart(SteamUtils.GetServerRealTime() + 2);
            game.NetworkReady = session.StartAt > 0 && SteamUtils.GetServerRealTime() >= session.StartAt && (session.IsHost || game.NetworkHasState);
            var input = RagdollNetInput.From(game.ReadNetworkInput());
            if (session.IsHost)
            {
                inputs.TryGetValue(session.Self, out var old);
                input.jump |= old.Input.jump; input.shove |= old.Input.shove;
                inputs[session.Self] = new Remote { Input = input, At = Time.realtimeSinceStartup };
                if (Time.realtimeSinceStartup - sentAt >= 1f / 30) SendSnapshot();
            }
            else
            {
                pending.move = input.move; pending.aim = input.aim; pending.sprint = input.sprint; pending.grab = input.grab;
                pending.interact = input.interact; pending.shoveHeld = input.shoveHeld;
                pending.jump |= input.jump; pending.shove |= input.shove;
                if (Time.realtimeSinceStartup - inputAt >= 1f / 60)
                {
                    inputAt = Time.realtimeSinceStartup;
                    Send(session.Host, RagdollNetProtocol.Input(session.Match, ++sequence, NowMs(), pending), true);
                    pending.jump = pending.shove = false;
                }
                Playback();
                float silence = Time.realtimeSinceStartup - receivedAt; session.ReportHostSilence(silence);
                if (silence > 15) session.Abort("체스타이머 방장 연결이 끊어졌습니다. 방에 다시 참가해 주세요.");
            }
        }
        void SyncRoster()
        {
            var ids = new ulong[12];
            foreach (var entry in session.Roster.Values)
            {
                if (entry.Team < 0 || entry.Team > 1 || entry.Slot < 0 || entry.Slot >= 6) continue;
                int slot = entry.Team * 6 + entry.Slot; ids[slot] = entry.Id;
                game.SetNetworkSlot(slot, entry.Id, BotIdentity.IsBot(entry.Id), session.Name(entry.Id), entry.Id == session.Self);
            }
            for (int i = 0; i < 12; i++) if (ids[i] == 0) game.SetNetworkSlot(i, 0, false, "", false);
            foreach (var id in inputs.Keys.ToArray()) if (!session.Roster.ContainsKey(id)) inputs.Remove(id);
        }
        void FixedUpdate()
        {
            if (!matchActive || !session.IsHost) return;
            for (int i = 0; i < 12; i++)
            {
                ulong id = game.NetworkIds[i]; if (id == 0 || BotIdentity.IsBot(id)) continue;
                if (!inputs.TryGetValue(id, out var remote) || Time.realtimeSinceStartup - remote.At > .35f)
                { game.ApplyNetworkInput(id, default); continue; }
                game.ApplyNetworkInput(id, remote.Input.ToPawnInput());
                remote.Input.jump = remote.Input.shove = false; inputs[id] = remote;
            }
        }
        static uint NowMs() => (uint)(Time.realtimeSinceStartupAsDouble * 1000);
        void SendSnapshot()
        {
            sentAt = Time.realtimeSinceStartup; ++tick; outgoing.Clear();
            for (int i = 0; i < 12; i++)
            {
                ulong id = game.NetworkIds[i]; if (id == 0) continue;
                var pawn = game.NetworkPawn(i);
                if (!poses.TryGetValue(id, out var pose)) poses[id] = pose = new RagdollPose();
                pawn.CaptureNetworkPose(pose); pose.id = id;
                if (pawn.NetworkSnap) { snaps[id] = 4; pawn.NetworkSnap = false; }
                snaps.TryGetValue(id, out int n); pose.snap = n > 0; snaps[id] = Math.Max(0, n - 1); outgoing.Add(pose);
            }
            byte[] poseBytes = RagdollNetProtocol.Snapshot(session.Match, tick, NowMs(), outgoing);
            byte[] stateBytes = null;
            if (sentAt - stateAt >= .1f) { stateAt = sentAt; stateBytes = ChessTimerProtocol.Write(session.Match, game.CaptureNetworkState(tick)); }
            foreach (ulong id in session.Roster.Keys)
            { Send(id, poseBytes, false); if (stateBytes != null) Send(id, stateBytes, false); }
        }
        void Send(ulong id, byte[] bytes, bool reliable)
        {
            if (id == session.Self || !session.IsPeer(id) || BotIdentity.IsBot(id)) return;
            var identity = new SteamNetworkingIdentity(); identity.SetSteamID64(id);
            var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try { SteamNetworkingMessages.SendMessageToUser(ref identity, pinned.AddrOfPinnedObject(), (uint)bytes.Length,
                (reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable) | Constants.k_nSteamNetworkingSend_NoNagle, Channel); }
            finally { pinned.Free(); }
        }
        void Receive()
        {
            for (int drain = 0; drain < 8; drain++)
            {
                int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, incoming, incoming.Length);
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var msg = SteamNetworkingMessage_t.FromIntPtr(incoming[i]); ulong id = msg.m_identityPeer.GetSteamID64();
                        if (!session.IsPeer(id) || msg.m_cbSize <= 0 || msg.m_cbSize > ChessTimerProtocol.MaxBytes) continue;
                        var bytes = new byte[msg.m_cbSize]; Marshal.Copy(msg.m_pData, bytes, 0, bytes.Length);
                        if (session.IsHost)
                        {
                            if (game.NetworkSlot(id) < 0 || !RagdollNetProtocol.ReadInput(bytes, session.Match, out uint seq, out uint stamp, out var input)) continue;
                            inputs.TryGetValue(id, out var old);
                            if (old.Sequence != 0 && !RagdollNetProtocol.Newer(seq, old.Sequence)) continue;
                            input.jump |= old.Input.jump; input.shove |= old.Input.shove;
                            inputs[id] = new Remote { Input = input, Sequence = seq, At = Time.realtimeSinceStartup };
                        }
                        else if (id == session.Host)
                        {
                            if (ChessTimerProtocol.Read(bytes, session.Match, out var state))
                            {
                                if (lastState != 0 && !RagdollNetProtocol.Newer(state.Tick, lastState)) continue;
                                lastState = state.Tick; game.ApplyNetworkState(state); receivedAt = Time.realtimeSinceStartup;
                            }
                            else
                            {
                                var snap = spare.Count > 0 ? spare.Pop() : new RagdollSnapshot();
                                if (!RagdollNetProtocol.ReadSnapshot(bytes, session.Match, snap) || lastPose != 0 && !RagdollNetProtocol.Newer(snap.tick, lastPose)) { spare.Push(snap); continue; }
                                lastPose = snap.tick; receivedAt = Time.realtimeSinceStartup; buffer.Add(snap);
                            }
                        }
                    }
                    finally { SteamNetworkingMessage_t.Release(incoming[i]); incoming[i] = IntPtr.Zero; }
                }
                if (count < incoming.Length) break;
            }
        }
        void Playback()
        {
            if (buffer.Count == 0) return;
            var newest = buffer[buffer.Count - 1]; double target = newest.hostTimeMs - 100d;
            if (playback <= 0 || Math.Abs(playback - target) > 500) playback = target;
            playback = Math.Min(newest.hostTimeMs, playback + Time.unscaledDeltaTime * 1000 * (playback < target - 50 ? 1.1 : playback > target + 50 ? .9 : 1));
            var a = buffer[0]; var b = a;
            foreach (var frame in buffer) { b = frame; if (frame.hostTimeMs >= playback) break; a = frame; }
            float t = Mathf.Clamp01((float)((playback - a.hostTimeMs) / Math.Max(1, (double)b.hostTimeMs - a.hostTimeMs)));
            for (int i = 0; i < b.count; i++)
            {
                var to = b.At(i); int slot = game.NetworkSlot(to.id); if (slot < 0) continue;
                var from = to; for (int j = 0; j < a.count; j++) if (a.At(j).id == to.id) { from = a.At(j); break; }
                if (to.snap) game.NetworkPawn(slot).ApplyNetworkPose(to);
                else { RagdollPose.Blend(from, to, t, blend); game.NetworkPawn(slot).ApplyNetworkPose(blend); }
            }
            while (buffer.Count > 2 && (buffer[1].hostTimeMs < playback || buffer.Count > 16)) { spare.Push(buffer[0]); buffer.RemoveAt(0); }
        }
        void HostChanged(ulong previous, ulong next)
        {
            if (!matchActive) return;
            if (session.IsHost && !game.NetworkHasState)
            { session.Abort("복구할 체스타이머 상태를 받지 못했습니다. 새 방을 만들어 주세요."); return; }
            if (session.IsHost && buffer.Count > 0)
            {
                var newest = buffer[buffer.Count - 1];
                for (int i = 0; i < newest.count; i++) { int slot = game.NetworkSlot(newest.At(i).id); if (slot >= 0) game.NetworkPawn(slot).ApplyNetworkPose(newest.At(i)); }
            }
            tick = Math.Max(lastPose, lastState); lastPose = lastState = 0;
            game.ChangeNetworkAuthority(session.IsHost); HostingGuard(session.IsHost);
            while (buffer.Count > 0) { spare.Push(buffer[0]); buffer.RemoveAt(0); }
            inputs.Clear(); snaps.Clear(); pending = default; playback = 0; sentAt = stateAt = inputAt = 0;
            receivedAt = Time.realtimeSinceStartup; session.ReportHostSilence(0);
        }
        void ClearStream()
        {
            inputs.Clear(); poses.Clear(); snaps.Clear(); buffer.Clear(); spare.Clear();
            pending = default; tick = sequence = lastPose = lastState = 0; playback = 0; stateAt = sentAt = inputAt = 0;
        }
        void HostingGuard(bool on)
        {
            if (on && savedMaxStep < 0) { savedMaxStep = Time.maximumDeltaTime; Time.maximumDeltaTime = Mathf.Min(savedMaxStep, .1f); }
            if (!on && savedMaxStep >= 0) { Time.maximumDeltaTime = savedMaxStep; savedMaxStep = -1; }
        }
        void OnGUI()
        {
            if (label == null) { label = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 16, wordWrap = true }; button = new GUIStyle(GUI.skin.button) { font = RuntimePanels.KoreanFont, fontSize = 16 }; }
            if (!panelOpen) { GUI.Label(new Rect(12, Screen.height - 30, 700, 26), "F2: 체스타이머 온라인 · 방 만들기 / 참가 / 나가기", label); return; }
            GUILayout.BeginArea(new Rect(Mathf.Max(12, Screen.width - 480), 12, 465, 480), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("체스타이머 온라인 (F2 닫기)", label);
            GUILayout.Label(session.Online ? "Steam · " + session.Name(session.Self) : "Steam 로그인 후 다시 연결해 주세요. 오프라인 테스트도 가능합니다.", label);
            GUILayout.Label(session.Status + " " + session.Error + "\n" + message, label);
            if (!session.Online && GUILayout.Button("Steam 다시 연결", button)) session.Retry();
            bool canJoin = session.Online && session.IsLeader && !session.Busy;
            GUI.enabled = canJoin;
            if (GUILayout.Button("방 만들기", button)) session.FindMatch(true);
            GUILayout.BeginHorizontal(); roomCode = GUILayout.TextField(roomCode, 24);
            if (GUILayout.Button("방 코드 참가", button) && ulong.TryParse(roomCode, out ulong code)) session.JoinPrivateMatch(code);
            GUILayout.EndHorizontal(); GUI.enabled = session.Match != 0;
            if (GUILayout.Button("방 코드 복사: " + session.Match, button)) GUIUtility.systemCopyBuffer = session.Match.ToString();
            GUI.enabled = session.CanAddTestBot(0); if (GUILayout.Button("백팀 봇 추가", button)) session.AddTestBot(0);
            GUI.enabled = session.CanAddTestBot(1); if (GUILayout.Button("흑팀 봇 추가", button)) session.AddTestBot(1);
            GUI.enabled = session.CanStartGame; if (GUILayout.Button("함께 시작 (" + session.Roster.Count + "명)", button)) session.StartGame();
            GUI.enabled = session.Busy; if (GUILayout.Button("방 나가기", button)) session.Cancel();
            GUI.enabled = true;
            foreach (var p in session.Roster.Values) GUILayout.Label((p.Team == 0 ? "백 " : "흑 ") + session.Name(p.Id), label);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        void OnDestroy()
        {
            HostingGuard(false); requests?.Dispose();
            if (session != null) { session.HostChanged -= HostChanged; session.Dispose(); }
            if (game) { game.LeaveNetwork = null; game.NetworkPanelOpen = false; }
            Application.runInBackground = oldBackground;
        }
    }
}
