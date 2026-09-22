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
        readonly List<MoveInput> unacknowledged = new List<MoveInput>();
        readonly HashSet<ulong> connected = new HashSet<ulong>();
        readonly BotDirector bots = new BotDirector();
        readonly Callback<SteamNetworkingMessagesSessionRequest_t> requests;
        readonly Callback<SteamNetworkingMessagesSessionFailed_t> failures;
        public readonly Dictionary<ulong, PawnState> States = new Dictionary<ulong, PawnState>();
        public string ConnectionStatus { get; private set; } = "";
        uint sequence, tick, lastSnapshot;
        float accumulator, lastReceive, lastSnapshotSend;
        bool jumpQueued;
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
            { ConnectionStatus = "Steam peer connection failed. Cancel and try again."; });
        }
        public void Update(float x, float z, bool jump)
        {
            if (session.Match == 0) return;
            jumpQueued |= jump;
            Receive();
            foreach (ulong id in States.Keys.ToArray())
                if (!session.Roster.ContainsKey(id)) { States.Remove(id); inputs.Remove(id); lastInput.Remove(id); bots.Forget(id); Close(id); }
            if (session.IsHost)
                foreach (var p in session.Roster) if (!States.ContainsKey(p.Key)) States[p.Key] = p.Value;
            accumulator = Math.Min(accumulator + Time.unscaledDeltaTime, PawnMotor.Step * 4);
            while (accumulator >= PawnMotor.Step)
            {
                accumulator -= PawnMotor.Step;
                var input = new MoveInput { Sequence = ++sequence, X = x, Z = z, Jump = jumpQueued }; jumpQueued = false;
                if (session.Roster.ContainsKey(session.Self))
                {
                    if (session.IsHost) { inputs[session.Self] = input; lastInput[session.Self] = Time.realtimeSinceStartup; }
                    else
                    {
                        Send(session.Host, MotionProtocol.Input(session.Match, input));
                        if (States.TryGetValue(session.Self, out var me)) States[session.Self] = PawnMotor.Advance(me, input, PawnMotor.Step);
                        unacknowledged.Add(input);
                        if (unacknowledged.Count > 120) unacknowledged.RemoveAt(0);
                    }
                }
                if (session.IsHost)
                {
                    tick++;
                    float now = Time.realtimeSinceStartup;
                    foreach (ulong id in States.Keys.ToArray())
                    {
                        // A bot has no network input; the host is its only source.
                        if (BotIdentity.IsBot(id))
                        { States[id] = PawnMotor.Advance(States[id], bots.Think(id, States[id], now), PawnMotor.Step); continue; }
                        inputs.TryGetValue(id, out var current);
                        if (!lastInput.TryGetValue(id, out float time) || now - time > .25f)
                        { current.X = current.Z = 0; current.Jump = false; }
                        States[id] = PawnMotor.Advance(States[id], current, PawnMotor.Step);
                        current.Jump = false; inputs[id] = current;
                    }
                }
            }
            if (session.IsHost && Time.realtimeSinceStartup - lastSnapshotSend >= .05f)
            {
                lastSnapshotSend = Time.realtimeSinceStartup;
                byte[] bytes = MotionProtocol.Snapshot(session.Match, tick, States.Values);
                foreach (ulong id in session.Roster.Keys) Send(id, bytes);
            }
            if (!session.IsHost && session.Roster.ContainsKey(session.Self) && Time.realtimeSinceStartup - lastReceive > 12)
            { session.Cancel(); ConnectionStatus = "Host stopped sending movement updates. Returned to party."; }
        }
        void Send(ulong id, byte[] bytes)
        {
            // Bots have no Steam identity, so they are never a send target.
            if (id == 0 || id == session.Self || BotIdentity.IsBot(id)) return;
            var remote = new SteamNetworkingIdentity(); remote.SetSteamID64(id);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                var result = SteamNetworkingMessages.SendMessageToUser(ref remote, handle.AddrOfPinnedObject(), (uint)bytes.Length,
                    Constants.k_nSteamNetworkingSend_Unreliable | Constants.k_nSteamNetworkingSend_NoNagle, Channel);
                if (result != EResult.k_EResultOK) ConnectionStatus = "Steam send: " + result;
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
                    if (session.IsHost)
                    {
                        if (!session.Roster.ContainsKey(sender) || !MotionProtocol.ReadInput(bytes, session.Match, out var input)) continue;
                        if (inputs.TryGetValue(sender, out var previous) && !MotionProtocol.Newer(input.Sequence, previous.Sequence)) continue;
                        inputs[sender] = input; lastInput[sender] = Time.realtimeSinceStartup;
                    }
                    else if (sender == session.Host && MotionProtocol.ReadSnapshot(bytes, session.Match, out uint receivedTick, out var pawns) && MotionProtocol.Newer(receivedTick, lastSnapshot))
                    {
                        lastSnapshot = receivedTick; lastReceive = Time.realtimeSinceStartup; ConnectionStatus = "Connected through Steam";
                        foreach (var pawn in pawns)
                        {
                            if (!session.Roster.ContainsKey(pawn.Id)) continue;
                            var state = pawn;
                            if (pawn.Id == session.Self)
                            {
                                unacknowledged.RemoveAll(input => !MotionProtocol.Newer(input.Sequence, pawn.Ack));
                                foreach (var input in unacknowledged) state = PawnMotor.Advance(state, input, PawnMotor.Step);
                            }
                            States[pawn.Id] = state;
                        }
                    }
                }
                finally { SteamNetworkingMessage_t.Release(incoming[i]); incoming[i] = IntPtr.Zero; }
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
            States.Clear(); inputs.Clear(); lastInput.Clear(); unacknowledged.Clear(); bots.Clear();
            accumulator = 0; sequence = tick = lastSnapshot = 0; jumpQueued = false;
            lastReceive = Time.realtimeSinceStartup; ConnectionStatus = "";
        }
        public void Dispose()
        { session.SessionChanged -= Reset; Reset(); requests.Dispose(); failures.Dispose(); }
    }
}
