using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ChessFight.Game;
using ChessFight.Network;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab.Net
{
    // Reuses the lobby's sole Steam owner. This bridge never initializes or shuts down Steam.
    [DefaultExecutionOrder(-150)]
    public sealed class SteamSwordFightLink : MonoBehaviour
    {
        const int Channel = 33;
        SwordFightGame game;
        SteamSession session;
        readonly IntPtr[] incoming = new IntPtr[64];
        readonly Dictionary<ulong, Remote> inputs = new Dictionary<ulong, Remote>();
        readonly Dictionary<ulong, RagdollPose> poses = new Dictionary<ulong, RagdollPose>();
        readonly Dictionary<ulong, int> snaps = new Dictionary<ulong, int>();
        readonly List<RagdollPose> outgoing = new List<RagdollPose>();
        readonly List<RagdollSnapshot> buffer = new List<RagdollSnapshot>();
        readonly Stack<RagdollSnapshot> spare = new Stack<RagdollSnapshot>();
        readonly RagdollPose blend = new RagdollPose();
        readonly List<ulong> removed = new List<ulong>();
        Callback<SteamNetworkingMessagesSessionRequest_t> requests;
        RagdollNetInput pending;
        uint inputSequence, tick, lastPose, lastState;
        float sentAt, inputAt, receivedAt;
        double playback;
        struct Remote { public RagdollNetInput Input; public uint Sequence, Stamp; public float At; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            Attach();
        }
        static void OnScene(Scene scene, LoadSceneMode mode) => Attach();
        static void Attach()
        {
            var runtime = NetworkRuntime.Instance;
            var game = FindFirstObjectByType<SwordFightGame>();
            if (game == null || game.GetComponent<SteamSwordFightLink>() != null || runtime == null || !runtime.Session.Started) return;
            game.gameObject.AddComponent<SteamSwordFightLink>();
        }
        void Awake()
        {
            game = GetComponent<SwordFightGame>(); session = NetworkRuntime.Instance.Session;
            game.Networked = true; game.Authority = session.IsHost; game.LocalId = session.Self;
            game.LeaveMatch = () => session.Cancel();
            receivedAt = Time.realtimeSinceStartup;
            requests = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(c =>
            {
                if (session.IsPeer(c.m_identityRemote.GetSteamID64())) SteamNetworkingMessages.AcceptSessionWithUser(ref c.m_identityRemote);
            });
            SyncRoster();
        }
        void SyncRoster()
        {
            removed.Clear();
            foreach (var id in game.Fighters.Keys) if (!session.Roster.ContainsKey(id)) removed.Add(id);
            foreach (var id in removed) { game.Remove(id); inputs.Remove(id); poses.Remove(id); snaps.Remove(id); }
            foreach (var e in session.Roster)
                game.Add(e.Key, e.Value.Team, e.Value.Slot, BotIdentity.IsBot(e.Key), session.Name(e.Key));
        }
        void Update()
        {
            if (!session.Started || session.Match == 0) return;
            SyncRoster(); Receive();
            var local = game.ReadLocalInput();
            if (session.IsHost)
            {
                game.Local?.SetInput(local);
                if (Time.realtimeSinceStartup - sentAt >= 1f / 30f) SendSnapshot();
            }
            else
            {
                var fresh = RagdollNetInput.From(local);
                pending.move = fresh.move; pending.aim = fresh.aim; pending.sprint = fresh.sprint;
                pending.jump |= fresh.jump; pending.shove |= fresh.shove;
                // Edges use Steam's reliable ordered delivery on this dedicated channel; a lost
                // short click must not vanish. Stale held motion still times out on the host.
                if (Time.realtimeSinceStartup - inputAt >= 1f / 60f)
                {
                    inputAt = Time.realtimeSinceStartup;
                    Send(session.Host, RagdollNetProtocol.Input(session.Match, ++inputSequence, NowMs(), pending), true);
                    pending.jump = pending.shove = false;
                }
                Playback();
                if (Time.realtimeSinceStartup - receivedAt > 12) session.Abort("소드파이트 호스트 연결이 끊어졌습니다.");
            }
        }
        void FixedUpdate()
        {
            if (session == null || !session.IsHost) return;
            foreach (var f in game.Fighters.Values)
            {
                if (f.Id == session.Self || f.Bot) continue;
                if (!inputs.TryGetValue(f.Id, out var remote) || Time.realtimeSinceStartup - remote.At > .35f)
                { f.SetInput(default); continue; }
                f.SetInput(remote.Input.ToPawnInput());
                remote.Input.jump = remote.Input.shove = false; inputs[f.Id] = remote;
            }
        }
        static uint NowMs() => (uint)(Time.realtimeSinceStartupAsDouble * 1000);
        void SendSnapshot()
        {
            sentAt = Time.realtimeSinceStartup; ++tick;
            outgoing.Clear();
            var state = new SwordFightState { Tick = tick, White = game.White, Black = game.Black, Remaining = game.Remaining, Finished = game.Finished };
            foreach (var f in game.Fighters.Values)
            {
                if (!poses.TryGetValue(f.Id, out var pose)) poses[f.Id] = pose = new RagdollPose();
                f.Pawn.CaptureNetworkPose(pose); pose.id = f.Id;
                if (f.Pawn.NetworkSnap) { snaps[f.Id] = 4; f.Pawn.NetworkSnap = false; }
                snaps.TryGetValue(f.Id, out int n); pose.snap = n > 0; snaps[f.Id] = Mathf.Max(0, n - 1);
                outgoing.Add(pose);
                state.Fighters.Add(new SwordFighterState { Id = f.Id, Alive = f.Alive, Age = f.SwingAge,
                    Protection = f.Protection, Respawn = f.RespawnSeconds, Swing = f.SwingSerial,
                    Yaw = Mathf.Atan2(f.SwingDirection.x, f.SwingDirection.z) * Mathf.Rad2Deg });
            }
            byte[] poseBytes = RagdollNetProtocol.Snapshot(session.Match, tick, NowMs(), outgoing);
            byte[] stateBytes = SwordFightProtocol.Write(session.Match, state);
            foreach (var id in session.Roster.Keys)
            { Send(id, poseBytes, false); Send(id, stateBytes, false); }
        }
        void Send(ulong id, byte[] bytes, bool reliable)
        {
            if (id == session.Self || !session.IsPeer(id) || BotIdentity.IsBot(id)) return;
            var identity = new SteamNetworkingIdentity(); identity.SetSteamID64(id);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try { SteamNetworkingMessages.SendMessageToUser(ref identity, handle.AddrOfPinnedObject(), (uint)bytes.Length,
                (reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable) | Constants.k_nSteamNetworkingSend_NoNagle, Channel); }
            finally { handle.Free(); }
        }
        void Receive()
        {
            int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, incoming, incoming.Length);
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var msg = SteamNetworkingMessage_t.FromIntPtr(incoming[i]); ulong id = msg.m_identityPeer.GetSteamID64();
                    if (!session.IsPeer(id) || msg.m_cbSize <= 0 || msg.m_cbSize > RagdollNetProtocol.MaxBytes) continue;
                    byte[] bytes = new byte[msg.m_cbSize]; Marshal.Copy(msg.m_pData, bytes, 0, bytes.Length);
                    if (session.IsHost)
                    {
                        if (!RagdollNetProtocol.ReadInput(bytes, session.Match, out uint seq, out uint stamp, out var input)) continue;
                        inputs.TryGetValue(id, out var previous);
                        if (previous.Sequence != 0 && !RagdollNetProtocol.Newer(seq, previous.Sequence)) continue;
                        input.jump |= previous.Input.jump; input.shove |= previous.Input.shove;
                        inputs[id] = new Remote { Input = input, Sequence = seq, Stamp = stamp, At = Time.realtimeSinceStartup };
                    }
                    else if (id == session.Host)
                    {
                        if (SwordFightProtocol.Read(bytes, session.Match, out var state))
                        {
                            if (lastState != 0 && !RagdollNetProtocol.Newer(state.Tick, lastState)) continue;
                            lastState = state.Tick; game.ApplyScore(state.White, state.Black, state.Remaining, state.Finished);
                            foreach (var f in state.Fighters)
                                if (game.Fighters.TryGetValue(f.Id, out var pawn)) pawn.ApplyRemote(f.Alive, f.Protection, f.Age, f.Swing, Quaternion.Euler(0, f.Yaw, 0) * Vector3.forward, f.Respawn);
                        }
                        else
                        {
                            var snap = spare.Count > 0 ? spare.Pop() : new RagdollSnapshot();
                            if (!RagdollNetProtocol.ReadSnapshot(bytes, session.Match, snap) || (lastPose != 0 && !RagdollNetProtocol.Newer(snap.tick, lastPose))) { spare.Push(snap); continue; }
                            lastPose = snap.tick; receivedAt = Time.realtimeSinceStartup; buffer.Add(snap);
                        }
                    }
                }
                finally { SteamNetworkingMessage_t.Release(incoming[i]); incoming[i] = IntPtr.Zero; }
            }
        }
        void Playback()
        {
            if (buffer.Count == 0) return;
            var newest = buffer[buffer.Count - 1];
            double target = newest.hostTimeMs - 100d;
            if (playback <= 0 || Math.Abs(playback - target) > 500) playback = target;
            playback = Math.Min(newest.hostTimeMs, playback + Time.unscaledDeltaTime * 1000 * (playback < target - 50 ? 1.1 : playback > target + 50 ? .9 : 1));
            var from = buffer[0]; var to = from;
            foreach (var snapshot in buffer)
            { to = snapshot; if (snapshot.hostTimeMs >= playback) break; from = snapshot; }
            float t = Mathf.Clamp01((float)((playback - from.hostTimeMs) / Math.Max(1, (double)to.hostTimeMs - from.hostTimeMs)));
            for (int i = 0; i < to.count; i++)
            {
                var b = to.At(i);
                if (!game.Fighters.TryGetValue(b.id, out var f)) continue;
                RagdollPose a = b;
                for (int j = 0; j < from.count; j++) if (from.At(j).id == b.id) { a = from.At(j); break; }
                if (b.snap) f.Pawn.ApplyNetworkPose(b);
                else { RagdollPose.Blend(a, b, t, blend); f.Pawn.ApplyNetworkPose(blend); }
            }
            while (buffer.Count > 2 && (buffer[1].hostTimeMs < playback || buffer.Count > 16)) { spare.Push(buffer[0]); buffer.RemoveAt(0); }
        }
        void OnDestroy()
        {
            requests?.Dispose();
            // Do not close shared peer sessions: the lobby's motion transport owns them.
            if (game != null) game.LeaveMatch = null;
        }
    }
}
