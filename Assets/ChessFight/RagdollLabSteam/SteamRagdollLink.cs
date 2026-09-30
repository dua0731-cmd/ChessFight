using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ChessFight.Gameplay;
using ChessFight.Network;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab.Net
{
    /// <summary>
    /// Host-authoritative ragdoll over Steam for the lab. The host runs the only physics simulation;
    /// clients send 24-byte inputs and draw interpolated poses (64 bytes per pawn) with no local physics.
    /// It boots itself in whichever scene runs the lab (a LabGame is present; on main that is
    /// RagdollTest) and stays out of the way until a match actually starts, so local two-player
    /// testing is unchanged. It lives outside RagdollLab/ on purpose: like Bootstrap/, it is the
    /// bridge between a gameplay assembly and Steam, and the lab itself must not know about Steam.
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
        // Overload guard while this PC hosts: at most this much game time per frame, so
        // at 120 Hz no frame runs more than 12 physics steps. Unity's default (1/3 s,
        // 40 steps) lets an overloaded host freeze for a third of a second and then send
        // everything at once, which clients see as stutter and jumps; capped, the host
        // runs a little slow instead and its snapshots keep coming evenly.
        const float HostMaxFrameStep = 0.1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<LabGame>() == null) return;
            if (FindFirstObjectByType<SteamRagdollLink>() != null) return;
            new GameObject("ChessFight Ragdoll Net").AddComponent<SteamRagdollLink>();
        }

        struct RemoteInput
        {
            // Edges (jump, shove, abilities) and interact stay latched until a physics step has
            // used them; interactNow is the latest packet's own interact, which takes over after.
            public RagdollNetInput input;
            public bool interactNow;
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
        float nextSnapshot, nextInput, lastReceive, statsAt, lastMatchSend;
        // Time.maximumDeltaTime before this PC started hosting, -1 while it is not hosting.
        float savedMaxFrameStep = -1f;
        const float MatchInterval = 0.5f;
        double playbackMs;
        RagdollNetInput pending;
        int sentBytes, receivedBytes, snapshotsIn;
        int sentRate, receivedRate, snapshotRate;
        float roundTripMs;
        // Obstacle clock for the match (SyncObstacleClock). A client replaces it with the host's
        // obstacle time from the snapshots as soon as one arrives (hostObstacles).
        readonly HostObstacleClock hostObstacles = new HostObstacleClock();
        // Client diagnostics: how far the Steam-clock estimate is from the host's obstacle time.
        double steamClockErrorMs;
        double obstacleOffset, serverAnchor;
        uint serverSecond;
        bool obstacleSynced;
        string roomCode = "";
        string message = "";
        // Closed by default: while it is open the cursor stays free for its buttons, and a free cursor
        // means no mouse look and no left/right click actions - which read as "the clicks are broken".
        bool hudOpen;
        GUIStyle label, small, button;
        Texture2D panel;

        static uint NowMs() => (uint)(Time.realtimeSinceStartupAsDouble * 1000d);

        void Awake()
        {
            game = FindFirstObjectByType<LabGame>();
            // P1's camera specifically: local split screen adds a second LabCamera for P2.
            cam = game != null && game.labCamera != null ? game.labCamera : FindFirstObjectByType<LabCamera>();
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

        void OnDestroy()
        {
            if (session != null)
            {
                session.SessionChanged -= ResetStream;
                session.HostChanged -= OnHostChanged;
                ResetStream();
                session.Dispose();
            }
            StopHostingGuard();
            sessionRequests?.Dispose();
            sessionFailures?.Dispose();
            if (panel != null) Destroy(panel);
            if (matchActive) ObstacleClock.Use(null);
        }

        // ---------------------------------------------------------------- frame loop

        void Update()
        {
            if (session == null || game == null) return;
            if (Input.GetKeyDown(KeyCode.F3)) hudOpen = !hudOpen;
            // The lab locks the cursor for mouse-look; this panel needs it back or its buttons never get clicked.
            // Only while it is open, though: F3 closes it and the clicks go back to the pawn.
            game.UiWantsCursor = hudOpen && !game.AutoTest;
            // What the host election reads (HOST.md): this machine's score once measured,
            // and every frame's time, so a host that stays slow hands the match on.
            if (session.LocalFitness == 0) session.LocalFitness = HostFitnessProbe.BaseScore();
            session.ReportFrame(Time.unscaledDeltaTime * 1000f);
            session.Tick();
            if (!session.Online) return;
            Receive();

            bool active = session.Started && session.Match != 0 && session.Roster.Count > 0;
            if (active && !matchActive) EnterMatch();
            else if (!active && matchActive) ExitMatch();
            if (!matchActive) return;
            SyncObstacleClock();

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
                pair.Value.SetInput(remote.input.ToPawnInput());
                remote.input.jump = false;
                remote.input.shove = false;
                remote.input.ability = false;
                remote.input.ability2 = false;
                remote.input.interact = remote.interactNow;
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

            // On a schedule rather than "an interval since the last send", which slipped to
            // the next frame every time and made the rate uneven on a slow host. After a
            // hitch the schedule restarts instead of bursting to catch up.
            float now = Time.realtimeSinceStartup;
            if (now < nextSnapshot) return;
            nextSnapshot += SnapshotInterval;
            if (nextSnapshot <= now) nextSnapshot = now + SnapshotInterval;
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
            // The obstacle time these poses were simulated at: clients draw the obstacles at it.
            byte[] bytes = RagdollNetProtocol.Snapshot(session.Match, ++tick, stamp, outgoing, ObstacleClock.Now);
            foreach (ulong id in session.Roster.Keys)
                if (id != session.Self) Send(id, bytes);
            SendMatchState();
        }

        /// <summary>The Queen of the Hill round's opened sections (M8), twice a second: the host judges the
        /// bells, the clients show the same paths opening at the same shared-clock moment.</summary>
        void SendMatchState()
        {
            var match = QueenHillMatch.Current;
            if (match == null || Time.realtimeSinceStartup - lastMatchSend < MatchInterval) return;
            lastMatchSend = Time.realtimeSinceStartup;
            string text = match.Encode();
            if (text.Length == 0) return;
            byte[] bytes = RagdollNetProtocol.Match(session.Match, text);
            if (bytes == null) return;
            foreach (ulong id in session.Roster.Keys)
                if (id != session.Self) Send(id, bytes);
        }

        void ClientFrame()
        {
            var local = game.ReadPlayerInput(0);
            var now = RagdollNetInput.From(local);
            pending.move = now.move;
            pending.grab = now.grab;
            pending.sprint = now.sprint;
            pending.aim = now.aim;
            pending.shoveHeld = now.shoveHeld;
            pending.jump |= now.jump;
            pending.shove |= now.shove;
            pending.ability |= now.ability;
            pending.ability2 |= now.ability2;
            // Held, but latched until sent: a tap shorter than one send interval still reaches the host.
            pending.interact |= now.interact;

            float clock = Time.realtimeSinceStartup;
            if (clock >= nextInput)
            {
                nextInput += InputInterval;
                if (nextInput <= clock) nextInput = clock + InputInterval;
                byte[] bytes = RagdollNetProtocol.Input(session.Match, ++sequence, NowMs(), pending);
                Send(session.Host, bytes);
                pending.jump = pending.shove = pending.ability = pending.ability2 = pending.interact = false;
            }

            Playback();

            // A host silent for a few seconds is replaced by its successor (HOST.md); the
            // return to the party below is only the last resort.
            session.ReportHostSilence(clock - lastReceive);
            if (clock - lastReceive > SilenceTimeout)
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
            // Obstacles on the same timeline as the pawns drawn this frame (HostObstacleClock).
            double hostObstacleTime = HostObstacleClock.Interpolate(from.obstacleTime, to.obstacleTime, t);
            hostObstacles.Sample(hostObstacleTime, Time.timeAsDouble, rate);
            double error = (ObstacleTime() - hostObstacleTime) * 1000d;
            steamClockErrorMs = steamClockErrorMs == 0d ? error : steamClockErrorMs + (error - steamClockErrorMs) * 0.05d;
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

        // ---------------------------------------------------------------- obstacle clock

        /// <summary>
        /// Moving platforms (the Queen of the Hill test bed) must be in the same place on both PCs without
        /// a packet about them (DECISIONS G2), so during a match obstacles run on Steam's server clock. Not
        /// on the raw clock, though: it reads real time, which jumps a whole frame at a time, and a lift
        /// driven by it would move unevenly from one physics step to the next and shake its rider. The
        /// obstacles advance with the physics steps instead, and only their offset to the server clock is
        /// measured every frame and eased in. A client also runs them PlaybackDelayMs behind, because it
        /// draws the pawns that far behind the host: a pawn riding a lift is then drawn on the lift.
        /// </summary>
        void SyncObstacleClock()
        {
            double target = ServerSeconds() - Time.fixedTimeAsDouble - (session.IsHost ? 0d : PlaybackDelayMs / 1000d);
            double error = target - obstacleOffset;
            if (!obstacleSynced || Math.Abs(error) > 0.25)
            {
                obstacleOffset = target;   // first frame, or a hitch: jump (Obstacle does not sweep a jump)
                obstacleSynced = true;
            }
            else obstacleOffset += Math.Max(-0.002, Math.Min(0.002, error));
        }

        double ObstacleTime() => Time.fixedTimeAsDouble + obstacleOffset;

        /// <summary>What obstacles read during a match. The host runs them on the shared clock above and
        /// stamps it into every snapshot. A client runs them on that stamp, interpolated at the moment
        /// whose pawns it is drawing: then an obstacle is exactly where the host had it when it judged
        /// those pawns, whatever the network delay, the playback rate or the Steam clock's accuracy.
        /// Until the first snapshot arrives a client falls back on the shared clock.</summary>
        double MatchObstacleTime()
        {
            if (session == null || session.IsHost || !hostObstacles.HasSample) return ObstacleTime();
            return hostObstacles.Now(Time.timeAsDouble);
        }

        /// <summary>Steam's server time: the same on every PC, whole seconds, the fraction from the local
        /// clock since the second last changed. Same as NetworkRuntime's match clock.</summary>
        double ServerSeconds()
        {
            uint second = SteamUtils.GetServerRealTime();
            double local = Time.realtimeSinceStartupAsDouble;
            if (second != serverSecond)
            {
                serverSecond = second;
                serverAnchor = local;
            }
            return second + Math.Min(0.999, local - serverAnchor);
        }

        void EnterMatch()
        {
            matchActive = true;
            obstacleSynced = false;
            // A new round on the shared clock: bells closed; only the host rings them.
            var queen = QueenHillMatch.Current;
            if (queen != null)
            {
                queen.ResetRound();
                queen.Authority = session.IsHost;
            }
            SyncObstacleClock();
            hostObstacles.Clear();
            steamClockErrorMs = 0d;
            ObstacleClock.Use(MatchObstacleTime);
            pawns.Clear();
            game.DespawnAll();
            game.NetworkControlled = true;
            if (session.IsHost) StartHostingGuard();
            ResetStream();
            // Hand the mouse to the pawn: the panel would otherwise keep the cursor free all match.
            hudOpen = false;
            message = session.IsHost ? "호스트로 경기를 시작했어요." : "호스트에 접속했어요.";
        }

        void ExitMatch()
        {
            matchActive = false;
            StopHostingGuard();
            ObstacleClock.Use(null);
            var queen = QueenHillMatch.Current;
            if (queen != null)
            {
                queen.Authority = true;
                queen.ResetRound();
            }
            foreach (var pawn in pawns.Values) if (pawn != null) Destroy(pawn.gameObject);
            pawns.Clear();
            if (cam != null) cam.soloTarget = null;
            game.NetworkControlled = false;
            game.DespawnAll();
            game.SpawnLocalPlayers();
            ResetStream();
        }

        /// <summary>
        /// The host role moved mid-match (Docs/Network/HOST.md). The new host takes every pawn from
        /// the newest pose it received and hands it to its own physics; a host that stepped down turns
        /// its pawns into puppets of the new one. Nobody is respawned: the round goes on from where it
        /// was, with a short stop while the pawns change hands.
        /// </summary>
        void OnHostChanged(ulong previous, ulong next)
        {
            // Before the match is on (the handover at the start), EnterMatch sets the roles itself.
            if (!matchActive || session == null) return;
            CloseSession(previous);
            bool nowHost = session.IsHost;
            if (nowHost)
            {
                // The newest pose, not the drawn one, which is PlaybackDelayMs behind.
                if (buffer.Count > 0)
                {
                    var newest = buffer[buffer.Count - 1];
                    for (int i = 0; i < newest.count; i++)
                    {
                        var pose = newest.At(i);
                        if (pawns.TryGetValue(pose.id, out var pawn) && pawn != null) pawn.ApplyNetworkPose(pose);
                    }
                }
                foreach (var pawn in pawns.Values) if (pawn != null) pawn.SetNetworkPuppet(false);
                tick = lastTick;   // clients keep accepting "newer" snapshots
                StartHostingGuard();
                message = "이 PC가 새 호스트가 되었어요. 물리를 이어서 계산해요.";
            }
            else
            {
                foreach (var pawn in pawns.Values) if (pawn != null) pawn.SetNetworkPuppet(true);
                StopHostingGuard();
                lastTick = 0;
                message = $"호스트가 {session.Name(next)} 님으로 바뀌었어요.";
            }
            var queen = QueenHillMatch.Current;
            if (queen != null) queen.Authority = nowHost;
            while (buffer.Count > 0) Recycle(0);
            // The next host stamps obstacle times from its own clock: start over from its
            // first snapshot instead of easing from the previous host's timeline.
            hostObstacles.Clear();
            inputs.Clear();
            snapCountdown.Clear();
            pending = default;
            playbackMs = 0d;
            roundTripMs = 0f;
            nextSnapshot = nextInput = 0f;
            lastReceive = Time.realtimeSinceStartup;
        }

        void StartHostingGuard()
        {
            if (savedMaxFrameStep >= 0f) return;
            savedMaxFrameStep = Time.maximumDeltaTime;
            Time.maximumDeltaTime = Mathf.Min(savedMaxFrameStep, HostMaxFrameStep);
        }

        void StopHostingGuard()
        {
            if (savedMaxFrameStep < 0f) return;
            Time.maximumDeltaTime = savedMaxFrameStep;
            savedMaxFrameStep = -1f;
        }

        void CloseSession(ulong id)
        {
            if (!connected.Remove(id)) return;
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(id);
            SteamNetworkingMessages.CloseSessionWithUser(ref identity);
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
                pawn.Team = entry.Value.Team;   // the roster's side (M10)
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
            // Everything that arrived, not one buffer's worth: a host whose frame rate drops
            // would otherwise read its clients' 60 Hz inputs later and later.
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
                        receivedBytes += bytes.Length;
                        if (session.IsHost) ReceiveInput(sender, bytes);
                        else if (sender == session.Host)
                        {
                            if (RagdollNetProtocol.ReadMatch(bytes, session.Match, out string text)) QueenHillMatch.Current?.ApplyRemote(text);
                            else ReceiveSnapshot(bytes);
                        }
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

        void ReceiveInput(ulong sender, byte[] bytes)
        {
            if (!session.Roster.ContainsKey(sender)) return;
            if (!RagdollNetProtocol.ReadInput(bytes, session.Match, out uint seq, out uint clientTime, out var fresh)) return;
            inputs.TryGetValue(sender, out var previous);
            if (previous.sequence != 0 && !RagdollNetProtocol.Newer(seq, previous.sequence)) return;
            bool interactNow = fresh.interact;
            fresh.jump |= previous.input.jump;
            fresh.shove |= previous.input.shove;
            fresh.ability |= previous.input.ability;
            fresh.ability2 |= previous.input.ability2;
            fresh.interact |= previous.input.interact;
            inputs[sender] = new RemoteInput
            {
                input = fresh,
                interactNow = interactNow,
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
            hostObstacles.Clear();
            steamClockErrorMs = 0d;
            lastReceive = Time.realtimeSinceStartup;
            pending = default;
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
                string line = matchActive
                    ? $"F3: 온라인 패널 · {(session.IsHost ? "호스트" : "참가자")} · {pawns.Count}명"
                    : "F3: 온라인 패널 (방 만들기·참가)";
                GUI.Label(new Rect(x, Screen.height - 26f, 600f, 22f), line, small);
                return;
            }

            float width = 430f, height = matchActive ? 210f : 300f;
            GUILayout.BeginArea(new Rect(x, Screen.height - height - 12f, width, height), GUIContent.none, GUI.skin.box);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), panel);
            GUILayout.Label("온라인 (F3으로 닫기 - 열려 있는 동안은 마우스 조작이 멈춰요)", label);
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
                if (!session.IsHost)
                    GUILayout.Label(hostObstacles.HasSample
                        ? $"장애물: 방장 시각에 맞춤 (Steam 시계였다면 {steamClockErrorMs:+0;-0;0} ms 어긋남)"
                        : "장애물: 방장 스냅샷 기다리는 중 (Steam 시계 사용)", small);
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
