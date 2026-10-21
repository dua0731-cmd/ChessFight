using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ChessFight.Game;
using ChessFight.Network;
using Steamworks;
using UnityEngine;

namespace ChessFight.RagdollLab.Net
{
    /// <summary>
    /// R149: the Sword Fight prototype match from the lobby. The carrier of <see cref="LabNetLink"/> (the host-authoritative
    /// ragdolls and the piece skills online, as the Sword Fight skill test runs them since R121) over the lobby's own
    /// session (NetworkRuntime's: the room the lobby matched, its seats, its host election and hand-over). Unlike
    /// SteamRagdollLink it makes no session of its own and draws no test panel: the sides are the room's seats. It never
    /// initializes or shuts down Steam (NetworkRuntime owns it, HANDOFF rule 5). SteamSwordFightLink attaches it in the
    /// match scene instead of itself when the scene plays the prototype match.
    /// </summary>
    public sealed class SteamMatchLabLink : LabNetLink
    {
        const int Channel = 35;   // 31 SteamMotion, 32 the lab test rooms, 33 the old Sword Fight link, 34 Pawn Rush

        SteamSession session;
        Callback<SteamNetworkingMessagesSessionRequest_t> sessionRequests;
        readonly IntPtr[] incoming = new IntPtr[64];
        readonly HashSet<ulong> connected = new HashSet<ulong>();
        readonly Dictionary<ulong, LabSeat> roster = new Dictionary<ulong, LabSeat>();

        protected override bool RealMatch => true;
        protected override bool NetOnline => session != null && session.Online;
        protected override bool NetActive => session != null && session.Started && session.Match != 0 && session.Roster.Count > 0;
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
            session = NetworkRuntime.Instance != null ? NetworkRuntime.Instance.Session : null;
            base.Awake();
            if (session == null || !session.Online) return;
            session.HostChanged += OnHostChanged;
            sessionRequests = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(c =>
            {
                ulong id = c.m_identityRemote.GetSteamID64();
                if (session.IsPeer(id)) SteamNetworkingMessages.AcceptSessionWithUser(ref c.m_identityRemote);
            });
        }

        protected override void OnDestroy()
        {
            if (session != null) session.HostChanged -= OnHostChanged;
            sessionRequests?.Dispose();
            // Do not close the peer sessions: the lobby's motion transport shares them.
            base.OnDestroy();
        }

        protected override void NetTick()
        {
            // NetworkRuntime ticks the session; this only reads the room's seats.
            roster.Clear();
            if (session == null) return;
            foreach (var entry in session.Roster) roster[entry.Key] = new LabSeat { Team = entry.Value.Team, Slot = entry.Value.Slot };
        }

        protected override void NetHostSilence(float seconds) => session.ReportHostSilence(seconds);
        protected override void NetLeave(string why) => session.Cancel();
        protected override void NetMatchOver() => session.EndMatch();
        protected override void DrawLobby() { }

        void OnHostChanged(ulong previous, ulong next) => HostChanged(previous, next);

        protected override void NetSend(ulong id, byte[] bytes)
        {
            if (session == null || id == 0 || id == session.Self || BotIdentity.IsBot(id)) return;
            var remote = new SteamNetworkingIdentity();
            remote.SetSteamID64(id);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                SteamNetworkingMessages.SendMessageToUser(ref remote, handle.AddrOfPinnedObject(), (uint)bytes.Length,
                    Constants.k_nSteamNetworkingSend_Unreliable | Constants.k_nSteamNetworkingSend_NoNagle, Channel);
                connected.Add(id);
            }
            finally
            {
                handle.Free();
            }
        }

        protected override void NetReceive(Action<ulong, byte[]> deliver)
        {
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
    }
}
