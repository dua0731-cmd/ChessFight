using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;

namespace ChessFight.Network
{
    public sealed class SteamMotion : IDisposable
    {
        const int Channel = 31;
        readonly SteamSession session;
        readonly IntPtr[] incoming = new IntPtr[32];
        readonly Dictionary<ulong, MoveInput> inputs = new Dictionary<ulong, MoveInput>();
        readonly Dictionary<ulong, float> lastInput = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, byte> consumedJumps = new Dictionary<ulong, byte>();
        readonly List<MoveInput> unacknowledged = new List<MoveInput>();
        readonly HashSet<ulong> connected = new HashSet<ulong>();
        readonly BotDirector bots = new BotDirector();
        readonly Callback<SteamNetworkingMessagesSessionRequest_t> requests;
        readonly Callback<SteamNetworkingMessagesSessionFailed_t> failures;
        public readonly Dictionary<ulong, PawnState> States = new Dictionary<ulong, PawnState>();
        readonly ResponseTimer response = new ResponseTimer();
        struct Packet { public ulong Peer; public byte[] Bytes; }
        readonly LinkSimulator<Packet> delayedOut = new LinkSimulator<Packet>();
        readonly LinkSimulator<Packet> delayedIn = new LinkSimulator<Packet>();
        public string ConnectionStatus { get; private set; } = "";
        // Client only: how long the host has been silent, in player terms.
        public LinkHealth Health { get; private set; }
        public float Silence { get; private set; }
        // Steam's own transport ping: to the host on a client, the worst peer on the host.
        public int PingMs { get; private set; } = -1;
        public double ResponseMs => response.Milliseconds;
        uint sequence, tick, lastSnapshot;
        float accumulator, lastReceive, lastSnapshotSend, nextPingPoll;
        byte jumps;
        bool jumpQueued, listed;

        // Development only: extra delay and loss on this machine's packets.
        public LinkProfile Simulation
        {
            get => delayedOut.Profile;
            set { delayedOut.Profile = delayedIn.Profile = value; }
        }

        // One line for the HUDs.
        public string QualityLine
        {
            get
            {
                if (session.Match == 0) return Simulation.Active ? "지연 시뮬 " + Simulation : "";
                string ping = PingMs < 0 ? "—" : PingMs + "ms";
                string line = session.IsHost ? $"방장 · 최대 핑 {ping}"
                            : $"핑 {ping} · 응답 {(ResponseMs < 0 ? "—" : Math.Round(ResponseMs) + "ms")}";
                return Simulation.Active ? line + " · 지연 시뮬 " + Simulation : line;
            }
        }
        public SteamMotion(SteamSession session)
        {
            this.session = session;
            session.SessionChanged += Reset;
            requests = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(c =>
            {
                ulong id = c.m_identityRemote.GetSteamID64();
                if (session.IsPeer(id)) SteamNetworkingMessages.AcceptSessionWithUser(ref c.m_identityRemote);
            });
            failures = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(c =>
            { ConnectionStatus = "Steam P2P 연결 실패. 취소 후 다시 시도하세요."; });
        }
        public void Update(float x, float z, bool jump)
        {
            if (session.Match == 0) return;
            float now = Time.realtimeSinceStartup;
            Receive();
            delayedIn.Release(now, m => Handle(m.Peer, m.Bytes));
            delayedOut.Release(now, m => Transmit(m.Peer, m.Bytes));
            if (session.Match == 0) return;
            UpdateHealth(now);
            // While the host is silent nothing the player does can be confirmed,
            // so the pawn holds still instead of running on unseen.
            if (Health >= LinkHealth.Frozen) { x = z = 0; jump = false; }
            jumpQueued |= jump;
            foreach (ulong id in States.Keys.ToArray())
                if (!session.Roster.ContainsKey(id)) { States.Remove(id); inputs.Remove(id); lastInput.Remove(id); consumedJumps.Remove(id); bots.Forget(id); Close(id); }
            if (session.IsHost)
                foreach (var p in session.Roster) if (!States.ContainsKey(p.Key)) States[p.Key] = p.Value;
            accumulator = Math.Min(accumulator + Time.unscaledDeltaTime, PawnMotor.Step * 4);
            while (accumulator >= PawnMotor.Step)
            {
                accumulator -= PawnMotor.Step;
                if (jumpQueued) jumps++;
                var input = new MoveInput { Sequence = ++sequence, X = x, Z = z, Jump = jumpQueued, Jumps = jumps }; jumpQueued = false;
                if (session.Roster.ContainsKey(session.Self))
                {
                    if (session.IsHost) { inputs[session.Self] = input; lastInput[session.Self] = Time.realtimeSinceStartup; }
                    else
                    {
                        Send(session.Host, MotionProtocol.Input(session.Match, input));
                        response.Sent(input.Sequence, now);
                        if (States.TryGetValue(session.Self, out var me)) States[session.Self] = PawnMotor.Advance(me, input, PawnMotor.Step);
                        unacknowledged.Add(input);
                        if (unacknowledged.Count > 120) unacknowledged.RemoveAt(0);
                    }
                }
                if (session.IsHost)
                {
                    tick++;
                    foreach (ulong id in States.Keys.ToArray())
                    {
                        // A bot has no network input; the host is its only source.
                        if (BotIdentity.IsBot(id))
                        { States[id] = PawnMotor.Advance(States[id], bots.Think(id, States[id], now), PawnMotor.Step); continue; }
                        inputs.TryGetValue(id, out var current);
                        consumedJumps.TryGetValue(id, out byte consumed);
                        // The press count arrives in every packet, so a press survives
                        // the loss of the packet it was first sent in.
                        current.Jump = MotionProtocol.TakeJump(current.Jumps, ref consumed);
                        consumedJumps[id] = consumed;
                        if (!lastInput.TryGetValue(id, out float time) || now - time > .25f)
                        { current.X = current.Z = 0; current.Jump = false; }
                        States[id] = PawnMotor.Advance(States[id], current, PawnMotor.Step);
                    }
                }
            }
            if (session.IsHost && Time.realtimeSinceStartup - lastSnapshotSend >= .05f)
            {
                lastSnapshotSend = Time.realtimeSinceStartup;
                byte[] bytes = MotionProtocol.Snapshot(session.Match, tick, States.Values);
                foreach (ulong id in session.Roster.Keys) Send(id, bytes);
            }
            if (Health == LinkHealth.Lost)
            { session.Cancel(); ConnectionStatus = "방장의 응답이 끊겨 파티로 돌아왔습니다."; return; }
            if (now >= nextPingPoll) { nextPingPoll = now + 1; PingMs = PollPing(); }
        }

        void UpdateHealth(float now)
        {
            if (session.IsHost || !session.Roster.ContainsKey(session.Self)) { Health = LinkHealth.Ok; Silence = 0; listed = false; return; }
            // Admission can take seconds; the host only starts sending once we are
            // on its roster, so the silence clock starts there too.
            if (!listed) { listed = true; lastReceive = now; }
            Silence = now - lastReceive;
            var previous = Health;
            Health = LinkMonitor.Classify(Silence);
            if (Health == LinkHealth.Unstable) ConnectionStatus = "방장 연결이 불안정합니다...";
            else if (Health == LinkHealth.Frozen)
                ConnectionStatus = $"방장 응답 없음 - 멈춤 ({Math.Ceiling(LinkMonitor.LostAfter - Silence)}초 뒤 파티로 복귀)";
            else if (previous != LinkHealth.Ok) ConnectionStatus = "Steam으로 연결됨";
        }

        int PollPing()
        {
            int worst = -1;
            foreach (ulong id in connected)
            {
                if (!session.IsHost && id != session.Host) continue;
                var remote = new SteamNetworkingIdentity(); remote.SetSteamID64(id);
                var state = SteamNetworkingMessages.GetSessionConnectionInfo(ref remote, out _, out SteamNetConnectionRealTimeStatus_t status);
                if (state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected && status.m_nPing > worst) worst = status.m_nPing;
            }
            return worst;
        }

        void Send(ulong id, byte[] bytes)
        {
            // Bots have no Steam identity, so they are never a send target.
            if (id == 0 || id == session.Self || BotIdentity.IsBot(id)) return;
            if (Simulation.Active) delayedOut.Push(new Packet { Peer = id, Bytes = bytes }, Time.realtimeSinceStartup);
            else Transmit(id, bytes);
        }
        void Transmit(ulong id, byte[] bytes)
        {
            if (session.Match == 0) return;
            var remote = new SteamNetworkingIdentity(); remote.SetSteamID64(id);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var result = SteamNetworkingMessages.SendMessageToUser(ref remote, handle.AddrOfPinnedObject(), (uint)bytes.Length,
                    Constants.k_nSteamNetworkingSend_Unreliable | Constants.k_nSteamNetworkingSend_NoNagle, Channel);
                if (result != EResult.k_EResultOK) ConnectionStatus = "Steam 전송 오류: " + result;
                connected.Add(id);
            }
            finally { handle.Free(); }
        }
        void Receive()
        {
            int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, incoming, incoming.Length);
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var message = SteamNetworkingMessage_t.FromIntPtr(incoming[i]);
                    ulong sender = message.m_identityPeer.GetSteamID64();
                    if (BotIdentity.IsBot(sender) || !session.IsPeer(sender) ||
                        message.m_cbSize <= 0 || message.m_cbSize > MotionProtocol.MaxBytes) continue;
                    var bytes = new byte[message.m_cbSize]; Marshal.Copy(message.m_pData, bytes, 0, bytes.Length);
                    if (Simulation.Active) delayedIn.Push(new Packet { Peer = sender, Bytes = bytes }, Time.realtimeSinceStartup);
                    else Handle(sender, bytes);
                }
                finally { SteamNetworkingMessage_t.Release(incoming[i]); incoming[i] = IntPtr.Zero; }
            }
        }
        void Handle(ulong sender, byte[] bytes)
        {
            if (!session.IsPeer(sender)) return;
            float now = Time.realtimeSinceStartup;
            if (session.IsHost)
            {
                if (!session.Roster.ContainsKey(sender) || !MotionProtocol.ReadInput(bytes, session.Match, out var input)) return;
                if (inputs.TryGetValue(sender, out var previous) && !MotionProtocol.Newer(input.Sequence, previous.Sequence)) return;
                inputs[sender] = input; lastInput[sender] = now;
            }
            else if (sender == session.Host && MotionProtocol.ReadSnapshot(bytes, session.Match, out uint receivedTick, out var pawns) && MotionProtocol.Newer(receivedTick, lastSnapshot))
            {
                lastSnapshot = receivedTick; lastReceive = now;
                if (Health != LinkHealth.Ok || ConnectionStatus == "") ConnectionStatus = "Steam으로 연결됨";
                Health = LinkHealth.Ok;
                foreach (var pawn in pawns)
                {
                    if (!session.Roster.ContainsKey(pawn.Id)) continue;
                    var state = pawn;
                    if (pawn.Id == session.Self)
                    {
                        response.Acknowledged(pawn.Ack, now);
                        unacknowledged.RemoveAll(input => !MotionProtocol.Newer(input.Sequence, pawn.Ack));
                        foreach (var input in unacknowledged) state = PawnMotor.Advance(state, input, PawnMotor.Step);
                    }
                    States[pawn.Id] = state;
                }
            }
        }
        void Close(ulong id)
        {
            if (!connected.Remove(id)) return;
            var identity = new SteamNetworkingIdentity(); identity.SetSteamID64(id);
            SteamNetworkingMessages.CloseSessionWithUser(ref identity);
        }
        void Reset()
        {
            foreach (ulong id in connected.ToArray()) Close(id);
            States.Clear(); inputs.Clear(); lastInput.Clear(); consumedJumps.Clear(); unacknowledged.Clear(); bots.Clear();
            delayedOut.Clear(); delayedIn.Clear(); response.Clear();
            accumulator = 0; sequence = tick = lastSnapshot = 0; jumps = 0; jumpQueued = listed = false;
            lastReceive = Time.realtimeSinceStartup; ConnectionStatus = ""; Health = LinkHealth.Ok; Silence = 0; PingMs = -1;
        }
        public void Dispose()
        { session.SessionChanged -= Reset; Reset(); requests.Dispose(); failures.Dispose(); }
    }
}
