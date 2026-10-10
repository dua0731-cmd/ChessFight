using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ChessFight.Network;
using Steamworks;
using UnityEngine;

namespace ChessFight.RagdollLab.Net
{
    /// <summary>
    /// Host-authoritative ragdoll over Steam for the lab: the Steam carrier for <see cref="LabNetLink"/>, which holds the
    /// match itself (R111: moved there so the same match also runs over a local socket, LoopbackLabLink). This class owns
    /// the Steam session (rooms, the roster, the host election and hand-over, HOST.md) and SteamNetworkingMessages.
    /// It boots itself in whichever scene runs the lab (a LabGame is present) and stays out of the way until a match
    /// actually starts, so local two-player testing is unchanged. It lives outside RagdollLab/ on purpose: like
    /// Bootstrap/, it is the bridge between a gameplay assembly and Steam, and the lab itself must not know about Steam.
    /// </summary>
    public sealed class SteamRagdollLink : LabNetLink
    {
        const int Channel = 32;                 // SteamMotion uses 31; the ragdoll stream is separate.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<LabGame>() == null) return;
            if (FindFirstObjectByType<LabNetLink>() != null) return;
            // A loopback test (two copies of the game, no Steam) asked for on the command line runs instead.
            if (LoopbackLabLink.Requested) return;
            new GameObject("ChessFight Ragdoll Net").AddComponent<SteamRagdollLink>();
        }

        SteamSession session;
        Callback<SteamNetworkingMessagesSessionRequest_t> sessionRequests;
        Callback<SteamNetworkingMessagesSessionFailed_t> sessionFailures;
        readonly IntPtr[] incoming = new IntPtr[64];
        readonly HashSet<ulong> connected = new HashSet<ulong>();
        readonly Dictionary<ulong, LabSeat> roster = new Dictionary<ulong, LabSeat>();
        string roomCode = "";

        protected override bool NetOnline => session != null && session.Online;
        protected override bool NetActive => session.Started && session.Match != 0 && session.Roster.Count > 0;
        protected override bool NetIsHost => session.IsHost;
        protected override ulong NetSelf => session.Self;
        protected override ulong NetHost => session.Host;
        protected override ulong NetMatch => session.Match;
        protected override IReadOnlyDictionary<ulong, LabSeat> NetRoster => roster;
        protected override string NetName(ulong id) => session.Name(id);
        protected override string NetKind => "스팀";
        protected override double NetServerSeconds() => SteamUtils.GetServerRealTime();

        protected override void Awake()
        {
            base.Awake();
            session = new SteamSession();
            session.Initialize();
            // Measures this PC so the host role can go to the fastest machine (Docs/Network/HOST.md).
            HostFitnessProbe.Start();
            if (!session.Online) return;
            session.HostChanged += OnHostChanged;
            sessionRequests = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(c =>
            {
                ulong id = c.m_identityRemote.GetSteamID64();
                if (session.IsPeer(id)) SteamNetworkingMessages.AcceptSessionWithUser(ref c.m_identityRemote);
            });
            sessionFailures = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(c =>
                message = "스팀 연결 실패. 방을 나갔다가 다시 시도해 주세요.");
            session.SessionChanged += ResetStream;
        }

        protected override void OnDestroy()
        {
            if (session != null)
            {
                session.SessionChanged -= ResetStream;
                session.HostChanged -= OnHostChanged;
                ResetStream();
                session.Dispose();
            }
            sessionRequests?.Dispose();
            sessionFailures?.Dispose();
            base.OnDestroy();
        }

        protected override void NetTick()
        {
            if (session == null) return;
            // What the host election reads (HOST.md): this machine's score once measured, and every frame's time, so a
            // host that stays slow hands the match on.
            if (session.LocalFitness == 0) session.LocalFitness = HostFitnessProbe.BaseScore();
            session.ReportFrame(Time.unscaledDeltaTime * 1000f);
            session.Tick();
            roster.Clear();
            foreach (var entry in session.Roster) roster[entry.Key] = new LabSeat { Team = entry.Value.Team, Slot = entry.Value.Slot };
        }

        protected override void NetHostSilence(float seconds) => session.ReportHostSilence(seconds);

        protected override void NetLeave(string why) => session.Cancel();

        void OnHostChanged(ulong previous, ulong next)
        {
            CloseSession(previous);
            HostChanged(previous, next);
        }

        protected override void OnStreamReset()
        {
            foreach (ulong id in connected)
            {
                var identity = new SteamNetworkingIdentity();
                identity.SetSteamID64(id);
                SteamNetworkingMessages.CloseSessionWithUser(ref identity);
            }
            connected.Clear();
        }

        void CloseSession(ulong id)
        {
            if (!connected.Remove(id)) return;
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(id);
            SteamNetworkingMessages.CloseSessionWithUser(ref identity);
        }

        // ---------------------------------------------------------------- transport

        protected override void NetSend(ulong id, byte[] bytes)
        {
            if (session == null || id == 0 || id == session.Self) return;
            var remote = new SteamNetworkingIdentity();
            remote.SetSteamID64(id);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var result = SteamNetworkingMessages.SendMessageToUser(ref remote, handle.AddrOfPinnedObject(), (uint)bytes.Length,
                    Constants.k_nSteamNetworkingSend_Unreliable | Constants.k_nSteamNetworkingSend_NoNagle, Channel);
                if (result != EResult.k_EResultOK) message = "스팀 전송 오류: " + result;
                connected.Add(id);
            }
            finally
            {
                handle.Free();
            }
        }

        protected override void NetReceive(Action<ulong, byte[]> deliver)
        {
            // Everything that arrived, not one buffer's worth: a host whose frame rate drops would otherwise read its
            // clients' 60 Hz inputs later and later.
            for (int round = 0; round < 8; round++)
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
                        deliver(sender, bytes);
                    }
                    finally
                    {
                        SteamNetworkingMessage_t.Release(incoming[i]);
                        incoming[i] = IntPtr.Zero;
                    }
                }
                if (count < incoming.Length) break;
            }
        }

        // ---------------------------------------------------------------- HUD

        protected override void DrawLobby()
        {
            GUILayout.Label(session.Online ? $"스팀: {session.Name(session.Self)}" : "스팀에 연결되지 않았어요 (Steam 실행 후 다시 시작)", Small);
            GUILayout.Label(session.Status + (string.IsNullOrEmpty(session.Error) ? "" : "  " + session.Error), Small);
            bool canQueue = session.Online && session.IsLeader && !session.Busy;
            GUILayout.BeginHorizontal();
            GUI.enabled = canQueue;
            if (GUILayout.Button("테스트 방 만들기", Button)) session.FindMatch(true);
            GUI.enabled = session.Match != 0;
            if (GUILayout.Button("방 번호 복사", Button))
            {
                GUIUtility.systemCopyBuffer = session.Match.ToString();
                message = "방 번호를 복사했어요. 상대에게 보내세요.";
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("방 번호", Small, GUILayout.Width(52f));
            GUI.SetNextControlName("netRoomCode");
            roomCode = GUILayout.TextField(roomCode, 24, Button);
            GUI.enabled = canQueue && ulong.TryParse(roomCode, out _);
            if (GUILayout.Button("참가", Button, GUILayout.Width(60f)) && ulong.TryParse(roomCode, out ulong id)) session.JoinPrivateMatch(id);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = session.IsHost && !session.Started && session.Roster.Count >= 2;
            if (GUILayout.Button($"시작 ({session.Roster.Count}명)", Button)) session.StartGame();
            GUI.enabled = session.Busy;
            if (GUILayout.Button("나가기", Button)) session.Cancel();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label($"내 방 번호: {session.Match}   {Hint()}", Small);
            // R111: two copies of the game on one PC (or two PCs on one LAN) without Steam.
            GUILayout.Label("Steam 없이 시험 (같은 PC의 두 창, 또는 같은 공유기의 두 PC):", Small);
            GUILayout.BeginHorizontal();
            GUI.enabled = !session.Busy;
            if (GUILayout.Button("로컬 시험: 방장", Button)) LoopbackLabLink.StartHost();
            if (GUILayout.Button("로컬 시험: 참가 (이 PC)", Button)) LoopbackLabLink.StartClient();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            game.SuppressInput = GUI.GetNameOfFocusedControl() == "netRoomCode";
        }

        protected override void DrawMatchExtras()
        {
            if (GUILayout.Button("경기 나가기", Button)) session.Cancel();
            game.SuppressInput = false;
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
    }
}
