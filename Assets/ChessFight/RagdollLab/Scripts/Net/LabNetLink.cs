using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>A seat in a network match: the roster's team and slot (the same on every PC).</summary>
    public struct LabSeat
    {
        public int Team, Slot;
    }

    /// <summary>
    /// The lab's network match (host-authoritative ragdolls), whatever carries the packets. The host runs the only
    /// physics; clients send inputs and draw interpolated poses with no physics of their own. Moved out of
    /// SteamRagdollLink in R111 so the same match runs over Steam (SteamRagdollLink, two PCs and two accounts) and over a
    /// plain UDP socket (LoopbackLabLink: two copies of the game on one PC, or two PCs on a LAN, no Steam), which is what
    /// lets the skills online be tried, filmed and checked frame by frame on one PC.
    ///
    /// R111 also adds the piece skills online (Docs/Network/SKILLS_ONLINE.md):
    /// <list type="bullet">
    /// <item>presses travel as counts (D-S7, RagdollLabInput): a lost packet delays a press by 1/60 s, never drops it;</item>
    /// <item>with every snapshot the host sends each client a skills packet (D-S5): every busy piece's state, the bishops'
    /// wires, the barricades, and the skill moments that client has not confirmed (D-S1, numbered, drawn once);</item>
    /// <item>a client writes the states into its puppets as they arrive (D-S4: a warning shows as early as possible), plays
    /// the moments in step with the bodies, draws its own aim from its own camera and its own press at once (D-S3);</item>
    /// <item>a hit stop holds only the two pieces' picture on each screen (D-S2, the effects' side);</item>
    /// <item>a new host takes over cooldowns, guards and wires (D-S6);</item>
    /// <item>the jitter buffer is measured (70..160 ms) instead of a fixed 110 ms, and F12 adds delay and loss (the capsule
    /// motor's F8 presets) to this PC's packets both ways.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public abstract class LabNetLink : MonoBehaviour
    {
        /// <summary>The link running in this scene (Steam or loopback), for tools and the editor.</summary>
        public static LabNetLink Current { get; private set; }

        /// <summary>Development: log what a client is sent and plays (the loopback test's checks read it).</summary>
        public static bool Trace;

        /// <summary>A trace line with the wall clock (the same on two copies on one PC, so their logs line up to the ms).</summary>
        public static void Log(string text) => Debug.Log($"[LabNet {DateTime.Now:HH:mm:ss.fff}] {text}");

        protected const float SnapshotInterval = 1f / 30f;
        const float InputInterval = 1f / 60f;
        const float InputTimeout = 0.35f;
        protected const float SilenceTimeout = 12f;
        // Overload guard while this PC hosts: at most this much game time per frame, so at 120 Hz no frame runs more than
        // 12 physics steps. Unity's default (1/3 s, 40 steps) lets an overloaded host freeze for a third of a second and
        // then send everything at once, which clients see as stutter and jumps; capped, the host runs a little slow
        // instead and its snapshots keep coming evenly.
        const float HostMaxFrameStep = 0.1f;
        const float MatchInterval = 0.5f;
        /// <summary>A moment played this much later than the poses it belongs to is not drawn any more (ms).</summary>
        const double StaleEventMs = 1000;

        // ---------------------------------------------------------------- what a carrier provides

        /// <summary>Can talk at all (Steam is up / the socket is open).</summary>
        protected abstract bool NetOnline { get; }
        /// <summary>A match is on: started, with a roster.</summary>
        protected abstract bool NetActive { get; }
        protected abstract bool NetIsHost { get; }
        protected abstract ulong NetSelf { get; }
        protected abstract ulong NetHost { get; }
        protected abstract ulong NetMatch { get; }
        protected abstract IReadOnlyDictionary<ulong, LabSeat> NetRoster { get; }
        protected abstract string NetName(ulong id);
        /// <summary>Send raw bytes now (after the lag simulator).</summary>
        protected abstract void NetSend(ulong id, byte[] bytes);
        /// <summary>Hand every packet that arrived to <paramref name="deliver"/> (sender, bytes).</summary>
        protected abstract void NetReceive(Action<ulong, byte[]> deliver);
        /// <summary>The same seconds on every PC (Steam's server clock; the PC clock for a loopback test).</summary>
        protected abstract double NetServerSeconds();
        /// <summary>Once a frame, before anything else (the session's own work).</summary>
        protected virtual void NetTick() { }
        /// <summary>How long the host has been silent (a Steam session hands the host role on after a while).</summary>
        protected virtual void NetHostSilence(float seconds) { }
        /// <summary>Leave the match (the host stopped sending).</summary>
        protected abstract void NetLeave(string why);
        /// <summary>The panel's part for when no match is on (making and joining a room).</summary>
        protected abstract void DrawLobby();
        /// <summary>What carries the packets, for the panel.</summary>
        protected abstract string NetKind { get; }
        /// <summary>Development commands from the host are taken (loopback tests only).</summary>
        protected virtual bool AcceptsDev => false;

        // ---------------------------------------------------------------- state

        class Remote
        {
            public RagdollLabInput input;
            public uint sequence, clientTimeMs;
            public float received;
            public bool synced;
            public byte jumps, shoves, abilities, abilities2, skills, pieceSeq;   // counts already taken
            public int jump, shove, ability, ability2, skill;                    // presses waiting for a physics step
            public bool interactLatch, interactNow;
            public ushort eventAck;
            public bool ackSet;
        }

        struct Packet
        {
            public ulong peer;
            public byte[] bytes;
        }

        protected LabGame game;
        protected LabCamera cam;
        protected bool hudOpen;
        protected string message = "";

        readonly Dictionary<ulong, RagdollPawn> pawns = new Dictionary<ulong, RagdollPawn>();
        readonly Dictionary<RagdollPawn, ulong> idOf = new Dictionary<RagdollPawn, ulong>();
        readonly Dictionary<ulong, Remote> inputs = new Dictionary<ulong, Remote>();
        readonly Dictionary<ulong, RagdollPose> posePool = new Dictionary<ulong, RagdollPose>();
        readonly Dictionary<ulong, int> snapCountdown = new Dictionary<ulong, int>();
        readonly List<RagdollPose> outgoing = new List<RagdollPose>();
        readonly List<RagdollSnapshot> buffer = new List<RagdollSnapshot>();
        readonly Stack<RagdollSnapshot> spare = new Stack<RagdollSnapshot>();
        readonly RagdollPose blended = new RagdollPose();
        readonly List<ulong> scratchIds = new List<ulong>();

        protected bool matchActive;
        uint tick, sequence, lastTick;
        float nextSnapshot, nextInput, lastReceive, statsAt, lastMatchSend;
        float savedMaxFrameStep = -1f;
        double playbackMs;
        readonly PlaybackDelay delay = new PlaybackDelay();

        // A client's own presses, counted (D-S7), and the rest of its input waiting for the next send.
        RagdollLabInput pending;
        byte myJumps, myShoves, myAbilities, myAbilities2, mySkills, ownPieceSeq;
        bool grabWasDown;

        int sentBytes, receivedBytes, snapshotsIn, skillBytes;
        int sentRate, receivedRate, snapshotRate, skillRate;
        float roundTripMs;

        // Obstacle clock for the match (SyncObstacleClock). A client replaces it with the host's obstacle time from the
        // snapshots as soon as one arrives (hostObstacles).
        readonly HostObstacleClock hostObstacles = new HostObstacleClock();
        double steamClockErrorMs, obstacleOffset, serverAnchor;
        uint serverSecond;
        bool obstacleSynced;

        // The skills (R111).
        readonly SkillEventLog eventLog = new SkillEventLog();
        readonly SkillEventInbox inbox = new SkillEventInbox();
        readonly SkillWirePacket skillOut = new SkillWirePacket(), skillIn = new SkillWirePacket();
        readonly List<SkillWireEvent> eventBatch = new List<SkillWireEvent>();
        readonly List<SkillWireEvent> eventQueue = new List<SkillWireEvent>();
        readonly Dictionary<ushort, SkillTripwire> remoteWires = new Dictionary<ushort, SkillTripwire>();
        readonly HashSet<ushort> seenWires = new HashSet<ushort>();
        readonly HashSet<RagdollPawn> seenStates = new HashSet<RagdollPawn>();
        readonly List<ushort> goneWires = new List<ushort>();
        uint lastSkillsTime;
        bool skillsApplied, replaying;
        int eventsPlayed;
        readonly Dictionary<RagdollPawn, SkillStage> hostSeen = new Dictionary<RagdollPawn, SkillStage>();

        // F12: extra delay and loss on this PC's packets both ways (the capsule motor's F8 presets).
        readonly LinkSimulator<Packet> lagOut = new LinkSimulator<Packet>(), lagIn = new LinkSimulator<Packet>();
        int lagPreset;

        GUIStyle label, small, button;
        Texture2D panel;

        protected static uint NowMs() => (uint)(Time.realtimeSinceStartupAsDouble * 1000d);

        /// <summary>This PC's lag simulation (F12, the panel's buttons).</summary>
        public LinkProfile Lag => lagOut.Profile;
        public bool MatchActive => matchActive;
        public bool IsHost => matchActive && NetIsHost;
        public float RoundTripMs => roundTripMs;
        public double PlaybackDelayMs => delay.Milliseconds;
        public int EventsPlayed => eventsPlayed;

        // ---------------------------------------------------------------- lifecycle

        protected virtual void Awake()
        {
            Current = this;
            game = FindFirstObjectByType<LabGame>();
            // P1's camera specifically: local split screen adds a second LabCamera for P2.
            cam = game != null && game.labCamera != null ? game.labCamera : FindFirstObjectByType<LabCamera>();
        }

        protected virtual void OnEnable()
        {
            RagdollPawn.SkillFx += OnPawnRushFx;
            RagdollPawn.QueenHillFx += OnQueenHillFx;
        }

        protected virtual void OnDisable()
        {
            RagdollPawn.SkillFx -= OnPawnRushFx;
            RagdollPawn.QueenHillFx -= OnQueenHillFx;
        }

        /// <summary>End a match this link is in, as if it had ended normally (the local players come back), and stop. For a
        /// link being swapped for another one mid-scene (LoopbackLabLink.Start/Stop).</summary>
        public void Shutdown()
        {
            if (matchActive) ExitMatch();
            enabled = false;
            OnShutdown();
        }

        /// <summary>The carrier lets go of what it holds at once (a socket's port), not at the end of the frame.</summary>
        protected virtual void OnShutdown() { }

        protected virtual void OnDestroy()
        {
            if (Current == this) Current = null;
            StopHostingGuard();
            if (matchActive)
            {
                ObstacleClock.Use(null);
                DropRemoteWires();
                if (game != null)
                {
                    game.NetworkControlled = game.NetworkHost = false;
                    game.NetworkLocal = null;
                }
            }
            if (panel != null) Destroy(panel);
        }

        protected virtual void Update()
        {
            if (game == null) return;
            if (Input.GetKeyDown(KeyCode.F3)) hudOpen = !hudOpen;
            if (Input.GetKeyDown(KeyCode.F12)) SetLag((lagPreset + 1) % LinkProfile.Presets.Length);
            // The lab locks the cursor for mouse-look; this panel needs it back or its buttons never get clicked. Only
            // while it is open, though: F3 closes it and the clicks go back to the pawn.
            game.UiWantsCursor = hudOpen && !game.AutoTest;
            NetTick();
            if (!NetOnline) return;
            // A client's skill timers run before this frame's packets overwrite them.
            if (matchActive && !NetIsHost) AdvanceViews(Time.unscaledDeltaTime);
            PumpReceive();

            bool active = NetActive;
            if (active && !matchActive) EnterMatch();
            else if (!active && matchActive) ExitMatch();
            if (matchActive)
            {
                SyncObstacleClock();
                SyncRoster();
                if (NetIsHost) HostFrame();
                else ClientFrame();
            }
            PumpSend();
            UpdateStats();
        }

        protected virtual void FixedUpdate()
        {
            if (!matchActive || !NetIsHost) return;
            float now = Time.realtimeSinceStartup;
            foreach (var pair in pawns)
            {
                if (pair.Key == NetSelf || pair.Value == null) continue;
                if (!inputs.TryGetValue(pair.Key, out var r) || now - r.received > InputTimeout)
                {
                    pair.Value.SetInput(default);
                    continue;
                }
                // The held buttons as they are; the presses one per physics step, so two quick ones stay two.
                var input = r.input.Held();
                input.interact = r.interactLatch || r.interactNow;
                r.interactLatch = false;
                input.jump = Take(ref r.jump);
                input.shove = Take(ref r.shove);
                input.ability = Take(ref r.ability);
                input.ability2 = Take(ref r.ability2);
                input.skill = Take(ref r.skill);
                pair.Value.SetInput(input);
            }
            foreach (var pair in pawns)
            {
                if (pair.Value == null || pair.Value.Hips.position.y >= LabLayout.KillHeight) continue;
                var seat = NetRoster.TryGetValue(pair.Key, out var s) ? s : default;
                Respawn(pair.Value, seat.Team, seat.Slot);
            }
        }

        static bool Take(ref int presses)
        {
            if (presses <= 0) return false;
            presses--;
            return true;
        }

        // ---------------------------------------------------------------- host

        void HostFrame()
        {
            if (pawns.TryGetValue(NetSelf, out var mine) && mine != null)
            {
                mine.SetInput(game.ReadPlayerInput(0));
                if (game.NetPieceSeq != ownPieceSeq)
                {
                    ownPieceSeq = game.NetPieceSeq;
                    ApplyPieceAsk(mine, game.NetPieceAsk);
                }
            }
            if (Input.GetKeyDown(KeyCode.R) && !game.SuppressInput)
                foreach (var pair in pawns)
                {
                    var seat = NetRoster.TryGetValue(pair.Key, out var s) ? s : default;
                    Respawn(pair.Value, seat.Team, seat.Slot);
                }
            if (Trace)
                foreach (var pawn in pawns.Values)
                {
                    if (pawn == null) continue;
                    hostSeen.TryGetValue(pawn, out var seen);
                    if (seen == pawn.SkillStage) continue;
                    Log($"host: {pawn.DisplayName} {pawn.Piece} {seen} → {pawn.SkillStage}");
                    hostSeen[pawn] = pawn.SkillStage;
                }

            // On a schedule rather than "an interval since the last send", which slipped to the next frame every time and
            // made the rate uneven on a slow host. After a hitch the schedule restarts instead of bursting to catch up.
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
            byte[] bytes = RagdollNetProtocol.Snapshot(NetMatch, ++tick, stamp, outgoing, ObstacleClock.Now);
            foreach (ulong id in NetRoster.Keys)
                if (id != NetSelf) Send(id, bytes);
            SendSkills(stamp);
            SendMatchState();
        }

        /// <summary>A player asked for a piece (the panel, Z/X): the host switches the pawn (and starts its skill over).</summary>
        void ApplyPieceAsk(RagdollPawn pawn, PieceKind kind)
        {
            if (pawn == null || !ChessPieces.IsValid((byte)kind) || pawn.Piece == kind) return;
            pawn.SetPiece(kind);
            pawn.ResetSkill();
            if (Trace) Log($"{pawn.DisplayName} → {kind}");
        }

        /// <summary>
        /// The skills packet, to each client with every snapshot: the busy pieces, the wires, the barricades, and the moments
        /// that client has not confirmed yet (as many as fit in 1024 bytes; the rest go in the next one).
        /// </summary>
        void SendSkills(uint stamp)
        {
            eventLog.Expire(stamp);
            skillOut.Clear();
            skillOut.HostTimeMs = stamp;
            skillOut.FirstSeq = eventLog.First;
            foreach (var pair in pawns)
            {
                var pawn = pair.Value;
                if (pawn == null || pawn.SkillNetIdle || skillOut.States.Count >= SkillWire.MaxStates) continue;
                var s = new SkillWireState { Pawn = RefOf(pawn) };
                if (s.Pawn == SkillWire.None) continue;
                pawn.CaptureSkillNet(ref s, RefOf);
                skillOut.States.Add(s);
            }
            foreach (var wire in SkillTripwire.LiveWires)
            {
                if (wire == null || wire.Remote || skillOut.Wires.Count >= SkillWire.MaxWires) continue;
                var o = new SkillWireObject();
                wire.CaptureNet(ref o, RefOf);
                skillOut.Wires.Add(o);
            }
            var barricades = SkillBarricade.All;
            for (int i = 0; i < barricades.Count && i < SkillWire.MaxBarricades; i++)
                if (barricades[i] != null)
                    skillOut.Barricades.Add(new SkillWireBarricade { Index = (byte)i, Standing = barricades[i].Standing, RegrowLeft = barricades[i].RegrowLeft });
            int budget = RagdollNetProtocol.MaxBytes - RagdollNetProtocol.HeaderBytes;
            int fit = SkillWire.EventsThatFit(budget, skillOut.States.Count, skillOut.Wires.Count, skillOut.Barricades.Count, 255);
            foreach (ulong id in NetRoster.Keys)
            {
                if (id == NetSelf) continue;
                inputs.TryGetValue(id, out var r);
                ushort acked = r != null && r.ackSet ? r.eventAck : unchecked((ushort)(eventLog.First - 1));
                eventLog.After(acked, fit, eventBatch);
                skillOut.Events.Clear();
                skillOut.Events.AddRange(eventBatch);
                byte[] bytes = RagdollNetProtocol.Skills(NetMatch, skillOut);
                skillBytes += bytes.Length;
                Send(id, bytes);
            }
        }

        /// <summary>The Queen of the Hill round's opened sections (M8), twice a second: the host judges the bells, the
        /// clients show the same paths opening at the same shared-clock moment.</summary>
        void SendMatchState()
        {
            var match = QueenHillMatch.Current;
            if (match == null || Time.realtimeSinceStartup - lastMatchSend < MatchInterval) return;
            lastMatchSend = Time.realtimeSinceStartup;
            string text = match.Encode();
            if (text.Length == 0) return;
            byte[] bytes = RagdollNetProtocol.Match(NetMatch, text);
            if (bytes == null) return;
            foreach (ulong id in NetRoster.Keys)
                if (id != NetSelf) Send(id, bytes);
        }

        // The host's own skills, recorded as they happen (the effects on its own screen listen to the same events).
        void OnPawnRushFx(SkillFxEvent e)
        {
            if (!matchActive || !NetIsHost || replaying) return;
            eventLog.Add(new SkillWireEvent
            {
                Mode = SkillWire.ModePawnRush, Kind = (byte)e.kind, By = RefOf(e.by), Target = RefOf(e.target),
                X = e.at.x, Y = e.at.y, Z = e.at.z, Dx = e.dir.x, Dy = e.dir.y, Dz = e.dir.z,
                Ux = e.normal.x, Uy = e.normal.y, Uz = e.normal.z,
                Count = (byte)Mathf.Clamp(e.count, 0, 255), Size = e.size,
                Source = e.source is SkillTripwire wire && wire != null ? wire.NetId : (ushort)0,
            }, NowMs());
        }

        void OnQueenHillFx(QueenHillFxEvent e)
        {
            if (!matchActive || !NetIsHost || replaying) return;
            eventLog.Add(new SkillWireEvent
            {
                Mode = SkillWire.ModeQueenHill, Kind = (byte)e.kind, By = RefOf(e.by), Target = RefOf(e.target),
                X = e.at.x, Y = e.at.y, Z = e.at.z, Dx = e.dir.x, Dy = e.dir.y, Dz = e.dir.z,
                Ux = e.to.x, Uy = e.to.y, Uz = e.to.z,
                Count = (byte)Mathf.Clamp(e.count, 0, 255), Size = e.size,
            }, NowMs());
        }

        void ReceiveInput(ulong sender, byte[] bytes)
        {
            if (!NetRoster.ContainsKey(sender)) return;
            if (!RagdollNetProtocol.ReadLabInput(bytes, NetMatch, out uint seq, out uint clientTime, out var fresh)) return;
            if (!inputs.TryGetValue(sender, out var r)) inputs[sender] = r = new Remote();
            if (r.sequence != 0 && !RagdollNetProtocol.Newer(seq, r.sequence)) return;   // older, overtaken: its counts are stale
            if (!r.synced)
            {
                // A client's counts start where they are when the host first hears it (after a host change too): presses
                // from before are not played again. The piece it asked for is.
                r.synced = true;
                r.jumps = fresh.jumps;
                r.shoves = fresh.shoves;
                r.abilities = fresh.abilities;
                r.abilities2 = fresh.abilities2;
                r.skills = fresh.skills;
                r.pieceSeq = 0;
            }
            r.jump = Mathf.Min(4, r.jump + PressCount.Take(fresh.jumps, ref r.jumps));
            r.shove = Mathf.Min(4, r.shove + PressCount.Take(fresh.shoves, ref r.shoves));
            r.ability = Mathf.Min(4, r.ability + PressCount.Take(fresh.abilities, ref r.abilities));
            r.ability2 = Mathf.Min(4, r.ability2 + PressCount.Take(fresh.abilities2, ref r.abilities2));
            r.skill = Mathf.Min(4, r.skill + PressCount.Take(fresh.skills, ref r.skills));
            r.interactLatch |= fresh.interact;
            r.interactNow = fresh.interact;
            r.input = fresh;
            r.sequence = seq;
            r.clientTimeMs = clientTime;
            r.received = Time.realtimeSinceStartup;
            if (fresh.pieceSeq != 0 && fresh.pieceSeq != r.pieceSeq)
            {
                r.pieceSeq = fresh.pieceSeq;
                if (pawns.TryGetValue(sender, out var pawn)) ApplyPieceAsk(pawn, (PieceKind)fresh.pieceAsk);
            }
            if (!r.ackSet || SkillWire.Newer(fresh.eventAck, r.eventAck))
            {
                r.eventAck = fresh.eventAck;
                r.ackSet = true;
            }
        }

        // ---------------------------------------------------------------- client

        void ClientFrame()
        {
            var local = game.ReadPlayerInput(0);
            if (local.jump) myJumps = PressCount.Add(myJumps);
            if (local.shove) myShoves = PressCount.Add(myShoves);
            if (local.ability) myAbilities = PressCount.Add(myAbilities);
            if (local.ability2) myAbilities2 = PressCount.Add(myAbilities2);
            if (local.skill) mySkills = PressCount.Add(mySkills);
            pending.Hold(local);
            // Held, but latched until sent: a tap shorter than one send interval still reaches the host.
            pending.interact |= local.interact;

            if (pawns.TryGetValue(NetSelf, out var me) && me != null)
            {
                // D-S3: my own press and aim drawn at once, for about a round trip, until the host's state says the same.
                float hold = Mathf.Clamp(roundTripMs / 1000f + 0.25f, 0.25f, 0.8f);
                if (local.skill) me.PredictPress(0, hold);
                if (local.shove) me.PredictPress(1, hold);
                if (local.grab && !grabWasDown) me.PredictPress(2, hold);
                me.ViewAim(local.aim);
            }
            grabWasDown = local.grab;

            float clock = Time.realtimeSinceStartup;
            if (clock >= nextInput)
            {
                nextInput += InputInterval;
                if (nextInput <= clock) nextInput = clock + InputInterval;
                pending.jumps = myJumps;
                pending.shoves = myShoves;
                pending.abilities = myAbilities;
                pending.abilities2 = myAbilities2;
                pending.skills = mySkills;
                pending.pieceAsk = (byte)game.NetPieceAsk;
                pending.pieceSeq = game.NetPieceSeq;
                pending.eventAck = inbox.Started ? inbox.Acked : (ushort)0;
                Send(NetHost, RagdollNetProtocol.LabInput(NetMatch, ++sequence, NowMs(), pending));
                pending.interact = false;
            }

            Playback();
            DispatchEvents();

            // A host silent for a few seconds is replaced by its successor (Steam, HOST.md); leaving is the last resort.
            NetHostSilence(clock - lastReceive);
            if (clock - lastReceive > SilenceTimeout)
            {
                message = "호스트가 자세 전송을 멈췄어요. 경기를 나갑니다.";
                NetLeave(message);
            }
        }

        /// <summary>Render the buffer a little behind the newest snapshot (the measured jitter buffer), speeding up or
        /// slowing down the playback clock instead of syncing clocks with the host.</summary>
        void Playback()
        {
            if (buffer.Count == 0) return;
            var newest = buffer[buffer.Count - 1];
            double target = newest.hostTimeMs - delay.Milliseconds;
            if (playbackMs <= 0d) playbackMs = target;
            double behind = target - playbackMs;
            double rate = behind > 60d ? 1.15d : behind > 12d ? 1.04d : behind < -60d ? 0.9d : behind < -12d ? 0.96d : 1d;
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
                if (latest.id == NetSelf && latest.ack != 0)
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

        void AdvanceViews(float dt)
        {
            foreach (var pawn in pawns.Values)
                if (pawn != null && pawn.NetworkPuppet) pawn.AdvanceSkillView(dt);
        }

        void ReceiveSnapshot(byte[] bytes)
        {
            var snapshot = spare.Count > 0 ? spare.Pop() : new RagdollSnapshot();
            if (!RagdollNetProtocol.ReadSnapshot(bytes, NetMatch, snapshot))
            {
                spare.Push(snapshot);
                return;
            }
            if (lastTick != 0 && !RagdollNetProtocol.Newer(snapshot.tick, lastTick))
            {
                spare.Push(snapshot);
                return;
            }
            int lost = lastTick != 0 ? (int)Math.Min(30u, snapshot.tick - lastTick - 1) : 0;
            lastTick = snapshot.tick;
            lastReceive = Time.realtimeSinceStartup;
            snapshotsIn++;
            delay.Arrived(Time.realtimeSinceStartupAsDouble * 1000d, snapshot.hostTimeMs, lost);
            buffer.Add(snapshot);
        }

        /// <summary>The host's skills packet: the states at once (D-S4), the moments queued to play with the bodies.</summary>
        void ReceiveSkills(byte[] bytes)
        {
            if (!RagdollNetProtocol.ReadSkills(bytes, NetMatch, skillIn)) return;
            skillBytes += bytes.Length;
            // States and wires are whole in every packet: one overtaken by a newer is old news (its moments still count).
            bool newer = !skillsApplied || unchecked((int)(skillIn.HostTimeMs - lastSkillsTime)) > 0;
            if (newer)
            {
                skillsApplied = true;
                lastSkillsTime = skillIn.HostTimeMs;
                ApplyStates();
                ApplyWires();
                var barricades = SkillBarricade.All;
                foreach (var b in skillIn.Barricades)
                {
                    if (b.Index >= barricades.Count || barricades[b.Index] == null) continue;
                    barricades[b.Index].Remote = true;
                    barricades[b.Index].ApplyNet(b.Standing, b.RegrowLeft);
                }
            }
            inbox.Window(skillIn.FirstSeq);
            foreach (var e in skillIn.Events)
            {
                if (!inbox.Accept(e.Seq)) continue;
                // Kept in time order: each plays when the bodies drawn reach its moment.
                int at = eventQueue.Count;
                while (at > 0 && unchecked((int)(eventQueue[at - 1].TimeMs - e.TimeMs)) > 0) at--;
                eventQueue.Insert(at, e);
            }
        }

        void ApplyStates()
        {
            seenStates.Clear();
            foreach (var s in skillIn.States)
            {
                var pawn = PawnOf(s.Pawn);
                if (pawn == null || !pawn.NetworkPuppet) continue;
                var was = pawn.SkillStage;
                pawn.ApplySkillNet(s, PawnOf);
                seenStates.Add(pawn);
                if (Trace && was != pawn.SkillStage) Log($"{pawn.DisplayName} {pawn.Piece} {was} → {pawn.SkillStage} (stage {s.StageTime:0.00}s, cd {s.Cooldown:0.0}s)");
            }
            foreach (var pawn in pawns.Values)
                if (pawn != null && pawn.NetworkPuppet && !seenStates.Contains(pawn))
                {
                    var was = pawn.SkillStage;
                    pawn.ClearSkillNet();
                    if (Trace && was != pawn.SkillStage) Log($"{pawn.DisplayName} {pawn.Piece} {was} → {pawn.SkillStage} (idle)");
                }
        }

        void ApplyWires()
        {
            seenWires.Clear();
            foreach (var o in skillIn.Wires)
            {
                seenWires.Add(o.Id);
                if (!remoteWires.TryGetValue(o.Id, out var wire) || wire == null)
                {
                    var owner = PawnOf(o.Owner);
                    var skills = owner != null && owner.PawnRushSkills != null ? owner.PawnRushSkills : AnySkillParams();
                    if (skills == null) continue;
                    float yaw = o.Yaw * Mathf.Deg2Rad;
                    wire = SkillTripwire.SpawnRemote(owner, o.Id, new Vector3(o.X, o.Y, o.Z), new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)), skills);
                    remoteWires[o.Id] = wire;
                }
                wire.ApplyNet(o);
                if (!wire.NetFxShown && o.Age > 0.6f)
                {
                    // Joined after it was laid (its moment is gone): its squares are drawn now instead.
                    wire.NetFxShown = true;
                    replaying = true;
                    try
                    {
                        RagdollPawn.RaiseSkillFx(new SkillFxEvent
                        {
                            kind = SkillFxKind.BishopWire, by = wire.Owner, at = new Vector3(o.X, o.Y, o.Z), dir = wire.Forward, size = wire.LineLength, source = wire,
                        });
                    }
                    finally { replaying = false; }
                }
            }
            goneWires.Clear();
            foreach (var pair in remoteWires)
                if (!seenWires.Contains(pair.Key)) goneWires.Add(pair.Key);
            foreach (ushort id in goneWires)
            {
                if (remoteWires.TryGetValue(id, out var wire) && wire != null) wire.RemoveRemote();
                remoteWires.Remove(id);
            }
        }

        static PawnRushSkillParams AnySkillParams()
        {
            foreach (var pawn in RagdollPawn.All)
                if (pawn != null && pawn.PawnRushSkills != null) return pawn.PawnRushSkills;
            var bed = FindFirstObjectByType<PawnRushSkillBed>();
            return bed != null ? bed.skills : null;
        }

        /// <summary>The moments whose time the bodies drawn have reached (C6: in step with the poses).</summary>
        void DispatchEvents()
        {
            if (eventQueue.Count == 0 || playbackMs <= 0d) return;
            int n = 0;
            while (n < eventQueue.Count && eventQueue[n].TimeMs <= playbackMs + 0.5d) n++;
            // Taken off the queue first: an effect that throws must not hold up the ones after it.
            eventBatch.Clear();
            eventBatch.AddRange(eventQueue.GetRange(0, n));
            eventQueue.RemoveRange(0, n);
            foreach (var e in eventBatch)
            {
                if (playbackMs - e.TimeMs > StaleEventMs) continue;
                try
                {
                    Play(e);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        void Play(SkillWireEvent w)
        {
            var by = PawnOf(w.By);
            var target = PawnOf(w.Target);
            var at = new Vector3(w.X, w.Y, w.Z);
            var dir = new Vector3(w.Dx, w.Dy, w.Dz);
            var u = new Vector3(w.Ux, w.Uy, w.Uz);
            replaying = true;
            try
            {
                if (w.Mode == SkillWire.ModePawnRush)
                {
                    var kind = (SkillFxKind)w.Kind;
                    SkillTripwire wire = null;
                    if (w.Source != 0) remoteWires.TryGetValue(w.Source, out wire);
                    if (kind == SkillFxKind.BishopWire)
                    {
                        if (wire == null || wire.NetFxShown) return;
                        wire.NetFxShown = true;
                    }
                    RagdollPawn.RaiseSkillFx(new SkillFxEvent { kind = kind, by = by, target = target, at = at, dir = dir, normal = u, count = w.Count, size = w.Size, source = wire });
                    if (Trace) Log($"moment {w.Seq} {kind} by {(by != null ? by.DisplayName : "-")} on {(target != null ? target.DisplayName : "-")}, {playbackMs - w.TimeMs:0} ms after its pose");
                }
                else
                {
                    var kind = (QueenHillFxKind)w.Kind;
                    RagdollPawn.RaiseQueenHillFx(new QueenHillFxEvent { kind = kind, by = by, target = target, at = at, dir = dir, to = u, size = w.Size, count = w.Count });
                    if (Trace) Log($"moment {w.Seq} {kind} by {(by != null ? by.DisplayName : "-")} on {(target != null ? target.DisplayName : "-")}, {playbackMs - w.TimeMs:0} ms after its pose");
                }
                eventsPlayed++;
            }
            finally { replaying = false; }
        }

        // ---------------------------------------------------------------- the pieces by their seats

        /// <summary>The pawn of a player in the match (null if none).</summary>
        protected RagdollPawn PawnFor(ulong id) => pawns.TryGetValue(id, out var pawn) ? pawn : null;

        byte RefOf(RagdollPawn pawn)
        {
            if (pawn == null || !idOf.TryGetValue(pawn, out ulong id) || !NetRoster.TryGetValue(id, out var seat)) return SkillWire.None;
            return SkillWire.Ref(seat.Team, seat.Slot);
        }

        RagdollPawn PawnOf(byte reference)
        {
            if (reference == SkillWire.None) return null;
            SkillWire.Place(reference, out int team, out int slot);
            foreach (var pair in NetRoster)
                if (pair.Value.Team == team && pair.Value.Slot == slot)
                    return pawns.TryGetValue(pair.Key, out var pawn) ? pawn : null;
            return null;
        }

        // ---------------------------------------------------------------- obstacle clock

        /// <summary>
        /// Moving platforms (the Queen of the Hill test bed) must be in the same place on both PCs without a packet about
        /// them (DECISIONS G2), so during a match obstacles run on the shared clock. Not on the raw clock, though: it reads
        /// real time, which jumps a whole frame at a time, and a lift driven by it would move unevenly from one physics
        /// step to the next and shake its rider. The obstacles advance with the physics steps instead, and only their
        /// offset to the shared clock is measured every frame and eased in. A client also runs them its playback delay
        /// behind, because it draws the pawns that far behind the host: a pawn riding a lift is then drawn on the lift.
        /// </summary>
        void SyncObstacleClock()
        {
            double target = ServerSeconds() - Time.fixedTimeAsDouble - (NetIsHost ? 0d : delay.Milliseconds / 1000d);
            double error = target - obstacleOffset;
            if (!obstacleSynced || Math.Abs(error) > 0.25)
            {
                obstacleOffset = target;   // first frame, or a hitch: jump (Obstacle does not sweep a jump)
                obstacleSynced = true;
            }
            else obstacleOffset += Math.Max(-0.002, Math.Min(0.002, error));
        }

        double ObstacleTime() => Time.fixedTimeAsDouble + obstacleOffset;

        /// <summary>What obstacles read during a match. The host runs them on the shared clock above and stamps it into
        /// every snapshot. A client runs them on that stamp, interpolated at the moment whose pawns it is drawing: then an
        /// obstacle is exactly where the host had it when it judged those pawns, whatever the network delay, the playback
        /// rate or the shared clock's accuracy. Until the first snapshot arrives a client falls back on the shared clock.</summary>
        double MatchObstacleTime()
        {
            if (NetIsHost || !hostObstacles.HasSample) return ObstacleTime();
            return hostObstacles.Now(Time.timeAsDouble);
        }

        /// <summary>The shared clock: whole seconds from the carrier, the fraction from the local clock since the second
        /// last changed.</summary>
        double ServerSeconds()
        {
            double shared = NetServerSeconds();
            uint second = (uint)Math.Floor(shared);
            double local = Time.realtimeSinceStartupAsDouble;
            if (second != serverSecond)
            {
                serverSecond = second;
                serverAnchor = local - (shared - second);
            }
            return second + Math.Min(0.999, local - serverAnchor);
        }

        // ---------------------------------------------------------------- match lifecycle

        void EnterMatch()
        {
            matchActive = true;
            obstacleSynced = false;
            // A new round on the shared clock: bells closed; only the host rings them.
            var queen = QueenHillMatch.Current;
            if (queen != null)
            {
                queen.ResetRound();
                queen.Authority = NetIsHost;
            }
            SyncObstacleClock();
            hostObstacles.Clear();
            steamClockErrorMs = 0d;
            ObstacleClock.Use(MatchObstacleTime);
            pawns.Clear();
            idOf.Clear();
            game.DespawnAll();
            ClearWiresAndBarricades();
            game.NetworkControlled = true;
            game.NetworkHost = NetIsHost;
            ownPieceSeq = 0;   // the piece this player last asked for is asked again (a client's is, by its first packet)
            foreach (var b in SkillBarricade.All) if (b != null) b.Remote = !NetIsHost;
            if (NetIsHost) StartHostingGuard();
            ResetStream();
            // Hand the mouse to the pawn: the panel would otherwise keep the cursor free all match.
            hudOpen = false;
            message = NetIsHost ? "호스트로 경기를 시작했어요." : "호스트에 접속했어요.";
            if (Trace) Log($"match on ({NetKind}), {(NetIsHost ? "host" : "client")}, self {NetSelf}, host {NetHost}");
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
            idOf.Clear();
            if (cam != null) cam.soloTarget = null;
            ClearWiresAndBarricades();
            foreach (var b in SkillBarricade.All) if (b != null) b.Remote = false;
            game.NetworkControlled = game.NetworkHost = false;
            game.NetworkLocal = null;
            game.DespawnAll();
            game.SpawnLocalPlayers();
            ResetStream();
            if (Trace) Log("match off");
        }

        /// <summary>
        /// The host role moved mid-match (Docs/Network/HOST.md). The new host takes every pawn from the newest pose it
        /// received and hands it to its own physics, and what it was drawing of the skills becomes what it runs (D-S6):
        /// cooldowns, guards, the wires. A host that stepped down turns its pawns into puppets of the new one and draws its
        /// wires from the new host's packets. Nobody is respawned: the round goes on from where it was, with a short stop
        /// while the pawns change hands.
        /// </summary>
        protected void HostChanged(ulong previous, ulong next)
        {
            // Before the match is on (the handover at the start), EnterMatch sets the roles itself.
            if (!matchActive) return;
            bool nowHost = NetIsHost;
            if (nowHost)
            {
                // The newest pose, not the drawn one, which is the playback delay behind.
                if (buffer.Count > 0)
                {
                    var newest = buffer[buffer.Count - 1];
                    for (int i = 0; i < newest.count; i++)
                    {
                        var pose = newest.At(i);
                        if (pawns.TryGetValue(pose.id, out var pawn) && pawn != null) pawn.ApplyNetworkPose(pose);
                    }
                }
                foreach (var pawn in pawns.Values)
                    if (pawn != null)
                    {
                        pawn.SetNetworkPuppet(false);
                        pawn.AdoptSkillNet();
                    }
                ushort highest = 0;
                foreach (var pair in remoteWires)
                {
                    if (pair.Value == null) continue;
                    pair.Value.Adopt();
                    if (pair.Key > highest) highest = pair.Key;
                }
                SkillTripwire.ReserveIds(highest);
                remoteWires.Clear();
                foreach (var b in SkillBarricade.All) if (b != null) b.Remote = false;
                tick = lastTick;   // clients keep accepting "newer" snapshots
                StartHostingGuard();
                message = "이 PC가 새 호스트가 되었어요. 물리와 스킬을 이어서 계산해요.";
            }
            else
            {
                foreach (var pawn in pawns.Values) if (pawn != null) pawn.SetNetworkPuppet(true);
                foreach (var wire in SkillTripwire.LiveWires)
                {
                    if (wire == null || wire.Remote) continue;
                    wire.Yield();
                    remoteWires[wire.NetId] = wire;
                }
                foreach (var b in SkillBarricade.All) if (b != null) b.Remote = true;
                StopHostingGuard();
                lastTick = 0;
                message = $"호스트가 {NetName(next)} 님으로 바뀌었어요.";
            }
            game.NetworkHost = nowHost;
            var queen = QueenHillMatch.Current;
            if (queen != null) queen.Authority = nowHost;
            while (buffer.Count > 0) Recycle(0);
            inputs.Clear();
            snapCountdown.Clear();
            pending = default;
            eventLog.Clear();
            inbox.Clear();
            eventQueue.Clear();
            skillsApplied = false;
            playbackMs = 0d;
            roundTripMs = 0f;
            delay.Reset();
            nextSnapshot = nextInput = 0f;
            lastReceive = Time.realtimeSinceStartup;
            if (Trace) Log($"host {previous} → {next}, this PC {(nowHost ? "hosts" : "follows")}");
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

        void SyncRoster()
        {
            scratchIds.Clear();
            scratchIds.AddRange(pawns.Keys);
            foreach (ulong id in scratchIds)
            {
                if (NetRoster.ContainsKey(id) && pawns[id] != null) continue;
                if (pawns[id] != null)
                {
                    idOf.Remove(pawns[id]);
                    Destroy(pawns[id].gameObject);
                }
                pawns.Remove(id);
                inputs.Remove(id);
            }
            foreach (var entry in NetRoster)
            {
                if (pawns.ContainsKey(entry.Key)) continue;
                var pawn = game.Spawn(LabLayout.NetSpawn(entry.Value.Team, entry.Value.Slot),
                    LabLayout.NetFacing(entry.Value.Team), game.TeamMaterial(entry.Value.Team), NetName(entry.Key));
                pawn.Team = entry.Value.Team;   // the roster's side (M10)
                if (!NetIsHost) pawn.SetNetworkPuppet(true);
                pawns[entry.Key] = pawn;
                idOf[pawn] = entry.Key;
                if (entry.Key == NetSelf)
                {
                    if (cam != null) cam.soloTarget = pawn;
                    game.NetworkLocal = pawn;
                }
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

        /// <summary>No wires of a match left lying about, every barricade standing.</summary>
        void ClearWiresAndBarricades()
        {
            DropRemoteWires();
            var wires = new List<SkillTripwire>(SkillTripwire.LiveWires);
            foreach (var wire in wires) if (wire != null) Destroy(wire.gameObject);
            foreach (var b in SkillBarricade.All) if (b != null) b.ApplyNet(true, 0f);
        }

        void DropRemoteWires()
        {
            foreach (var wire in remoteWires.Values) if (wire != null) wire.RemoveRemote();
            remoteWires.Clear();
        }

        // ---------------------------------------------------------------- transport

        protected void Send(ulong id, byte[] bytes)
        {
            if (id == 0 || id == NetSelf || bytes == null) return;
            sentBytes += bytes.Length;
            if (lagOut.Profile.Active)
            {
                lagOut.Push(new Packet { peer = id, bytes = bytes }, Time.realtimeSinceStartupAsDouble);
                return;
            }
            NetSend(id, bytes);
        }

        void PumpSend() => lagOut.Release(Time.realtimeSinceStartupAsDouble, p => NetSend(p.peer, p.bytes));

        void PumpReceive()
        {
            NetReceive(OnRaw);
            lagIn.Release(Time.realtimeSinceStartupAsDouble, p => Deliver(p.peer, p.bytes));
        }

        void OnRaw(ulong sender, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > RagdollNetProtocol.MaxBytes) return;
            receivedBytes += bytes.Length;
            // A test's own commands are not part of the link under test: never delayed or lost by F12.
            if (lagIn.Profile.Active && RagdollNetProtocol.TypeOf(bytes) != RagdollNetProtocol.TypeDev)
            {
                lagIn.Push(new Packet { peer = sender, bytes = bytes }, Time.realtimeSinceStartupAsDouble);
                return;
            }
            Deliver(sender, bytes);
        }

        void Deliver(ulong sender, byte[] bytes)
        {
            byte type = RagdollNetProtocol.TypeOf(bytes);
            if (NetIsHost)
            {
                if (type == RagdollNetProtocol.TypeLabInput) ReceiveInput(sender, bytes);
                return;
            }
            if (sender != NetHost) return;
            switch (type)
            {
                case RagdollNetProtocol.TypeSnapshot:
                    ReceiveSnapshot(bytes);
                    break;
                case RagdollNetProtocol.TypeSkills:
                    ReceiveSkills(bytes);
                    break;
                case RagdollNetProtocol.TypeMatch:
                    if (RagdollNetProtocol.ReadMatch(bytes, NetMatch, out string text)) QueenHillMatch.Current?.ApplyRemote(text);
                    break;
                case RagdollNetProtocol.TypeDev:
                    if (AcceptsDev && RagdollNetProtocol.ReadDev(bytes, NetMatch, out string command)) RunDev(command);
                    break;
            }
        }

        void Recycle(int index)
        {
            spare.Push(buffer[index]);
            buffer.RemoveAt(index);
        }

        /// <summary>A fresh stream: nothing buffered, every count and number started over.</summary>
        protected void ResetStream()
        {
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
            eventLog.Clear();
            inbox.Clear();
            eventQueue.Clear();
            skillsApplied = false;
            delay.Reset();
            lagOut.Clear();
            lagIn.Clear();
            OnStreamReset();
        }

        protected virtual void OnStreamReset() { }

        void UpdateStats()
        {
            if (Time.realtimeSinceStartup < statsAt) return;
            statsAt = Time.realtimeSinceStartup + 1f;
            sentRate = sentBytes;
            receivedRate = receivedBytes;
            snapshotRate = snapshotsIn;
            skillRate = skillBytes;
            sentBytes = receivedBytes = snapshotsIn = skillBytes = 0;
        }

        /// <summary>Extra delay and loss on this PC's packets both ways (F12 cycles; 0 = off).</summary>
        public void SetLag(int preset)
        {
            lagPreset = Mathf.Clamp(preset, 0, LinkProfile.Presets.Length - 1);
            lagOut.Profile = lagIn.Profile = LinkProfile.Presets[lagPreset];
            message = lagPreset == 0 ? "지연 시뮬레이터 꺼짐" : $"지연 시뮬레이터: {lagOut.Profile} (이 PC의 보내기·받기 모두, F12로 바꿈)";
        }

        // ---------------------------------------------------------------- development commands (loopback tests)

        /// <summary>Send a development command to every client (loopback tests: the editor drives the other copy).</summary>
        public void SendDev(string command)
        {
            if (!matchActive || !NetIsHost) return;
            byte[] bytes = RagdollNetProtocol.Dev(NetMatch, command);
            foreach (ulong id in NetRoster.Keys)
                if (id != NetSelf && id != 0) NetSend(id, bytes);   // straight out: a test command is not part of the link under test
        }

        /// <summary>
        /// A command from the host, on a loopback test client: <c>piece Queen</c>, <c>skill</c>, <c>click</c>, <c>jump</c>,
        /// <c>rclick 0.3</c>, <c>move x z seconds</c> (world direction), <c>look yaw pitch</c>, <c>lag 2</c>,
        /// <c>shot path.png</c>, <c>trace on</c>.
        /// </summary>
        protected void RunDev(string command)
        {
            if (string.IsNullOrEmpty(command)) return;
            string[] p = command.Trim().Split(' ');
            float F(int i, float fallback) => i < p.Length && float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;
            switch (p[0])
            {
                case "piece":
                    if (p.Length > 1 && Enum.TryParse(p[1], true, out PieceKind kind)) game.AskPiece(kind);
                    break;
                case "skill":
                    game.DevPress.skill = true;
                    break;
                case "click":
                    game.DevPress.shove = true;
                    break;
                case "jump":
                    game.DevPress.jump = true;
                    break;
                case "rclick":
                    game.DevGrabUntil = Time.unscaledTime + F(1, 0.2f);
                    break;
                case "move":
                    game.DevMove = Vector3.ClampMagnitude(new Vector3(F(1, 0f), 0f, F(2, 0f)), 1f);
                    game.DevMoveUntil = Time.unscaledTime + F(3, 0.5f);
                    break;
                case "look":
                    if (cam != null)
                    {
                        cam.yaw = F(1, cam.yaw);
                        cam.pitch = F(2, cam.pitch);
                    }
                    break;
                case "lag":
                    SetLag((int)F(1, 0f));
                    break;
                case "shot":
                    if (p.Length > 1) ScreenCapture.CaptureScreenshot(command.Substring(5).Trim());
                    break;
                case "burst":
                    // burst <path prefix> <count> <seconds apart>: a row of frames round a moment, for a contact sheet.
                    if (p.Length > 1) StartCoroutine(Burst(p[1], (int)F(2, 10f), F(3, 0.05f)));
                    break;
                case "trace":
                    Trace = p.Length < 2 || p[1] != "off";
                    break;
                case "hud":
                    hudOpen = p.Length < 2 || p[1] != "off";
                    break;
                case "report":
                    Log(Report());
                    break;
            }
            if (Trace) Log("dev: " + command);
        }

        /// <summary>One line of what this PC has of the match now (the loopback checks read it from the log).</summary>
        public string Report()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"report: {(NetIsHost ? "host" : "client")}, playback delay {delay.Milliseconds:0} ms (jitter {delay.JitterMs:0}, snapshots lost {delay.LossShare * 100:0.0}%{(delay.Lossy ? ", one more for the losses" : "")}), round trip {roundTripMs:0} ms, lag {lagOut.Profile}");
            sb.Append($", moments played {eventsPlayed}, confirmed up to {inbox.Acked}, queued {eventQueue.Count}, wires {SkillTripwire.LiveWires.Count} (copies {remoteWires.Count})");
            var barricades = SkillBarricade.All;
            for (int i = 0; i < barricades.Count; i++)
                if (barricades[i] != null) sb.Append($", barricade {i} {(barricades[i].Standing ? "standing" : $"down {barricades[i].RegrowLeft:0.0}s")}{(barricades[i].Remote ? " (host's)" : "")}");
            foreach (var pawn in pawns.Values)
                if (pawn != null)
                    sb.Append($", {pawn.DisplayName}: {pawn.Piece} {pawn.State} {pawn.SkillStage} {pawn.SkillStageTime:0.00}s cd {pawn.SkillCooldown:0.0}s{(pawn.NetworkPuppet ? " (puppet)" : "")}");
            return sb.ToString();
        }

        System.Collections.IEnumerator Burst(string prefix, int count, float apart)
        {
            for (int i = 0; i < Mathf.Clamp(count, 1, 60); i++)
            {
                ScreenCapture.CaptureScreenshot($"{prefix}_{i:00}.png");
                if (Trace) Log($"burst {System.IO.Path.GetFileName(prefix)}_{i:00}");
                float until = Time.realtimeSinceStartup + Mathf.Max(0.016f, apart);
                while (Time.realtimeSinceStartup < until) yield return null;
            }
        }

        // ---------------------------------------------------------------- HUD

        protected virtual void OnGUI()
        {
            if (game == null || game.AutoTest) return;
            EnsureStyles();
            // Keep clear of the tuning panel, which owns the left edge while it is open.
            float x = game.PanelOpen ? 500f : 12f;
            if (!hudOpen)
            {
                string lag = lagOut.Profile.Active ? $" · 지연 시뮬 {lagOut.Profile}" : "";
                string line = matchActive
                    ? $"F3: 온라인 패널 · {NetKind} · {(NetIsHost ? "호스트" : "참가자")} · {pawns.Count}명{lag}"
                    : $"F3: 온라인 패널 ({NetKind}: 방 만들기·참가){lag}";
                GUI.Label(new Rect(x, Screen.height - 26f, 700f, 22f), line, small);
                return;
            }

            float width = 470f, height = matchActive ? 300f : 330f;
            GUILayout.BeginArea(new Rect(x, Screen.height - height - 12f, width, height), GUIContent.none, GUI.skin.box);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), panel);
            GUILayout.Label($"온라인 · {NetKind} (F3으로 닫기 - 열려 있는 동안은 마우스 조작이 멈춰요)", label);
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message, small);
            if (!matchActive)
            {
                DrawLobby();
            }
            else
            {
                GUILayout.Label(NetIsHost ? "역할: 호스트 (이 PC가 물리와 스킬을 계산해요)" : "역할: 참가자 (호스트 화면을 받아 그려요)", small);
                GUILayout.Label($"인원 {pawns.Count}명 · 보내기 {sentRate / 1024f:F1} KB/s · 받기 {receivedRate / 1024f:F1} KB/s · 스킬 {skillRate / 1024f:F1} KB/s", small);
                GUILayout.Label(NetIsHost
                    ? $"스냅샷 {1f / SnapshotInterval:F0}Hz 송신 · 폰당 {RagdollNetProtocol.PoseBytes}바이트 · 스킬 신호 대기 {eventLog.Count}개"
                    : $"스냅샷 수신 {snapshotRate}Hz · 왕복 지연 {roundTripMs:F0} ms · 버퍼 {buffer.Count}개 · 재생 지연 {delay.Milliseconds:F0} ms (흔들림 {delay.JitterMs:F0} ms · 손실 {delay.LossShare * 100:F0}%{(delay.Lossy ? " → 한 장 더 기다림" : "")})", small);
                if (!NetIsHost)
                {
                    GUILayout.Label($"스킬 신호 받음 {eventsPlayed}개 · 확인 번호 {inbox.Acked} · 재생 대기 {eventQueue.Count}개", small);
                    GUILayout.Label(hostObstacles.HasSample
                        ? $"장애물: 방장 시각에 맞춤 (공유 시계였다면 {steamClockErrorMs:+0;-0;0} ms 어긋남)"
                        : "장애물: 방장 스냅샷 기다리는 중 (공유 시계 사용)", small);
                }
                DrawLagButtons();
                DrawMatchExtras();
            }
            GUILayout.EndArea();
        }

        void DrawLagButtons()
        {
            GUILayout.Label($"지연 시뮬레이터 (이 PC, 보내기·받기 모두, F12): {lagOut.Profile}", small);
            GUILayout.BeginHorizontal();
            string[] names = { "끔", "+100", "+200·5%", "+300·10%" };
            for (int i = 0; i < LinkProfile.Presets.Length && i < names.Length; i++)
            {
                GUI.enabled = lagPreset != i;
                if (GUILayout.Button(names[i], button)) SetLag(i);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        /// <summary>The carrier's own buttons while a match is on (leaving).</summary>
        protected virtual void DrawMatchExtras() { }

        protected void EnsureStyles()
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

        protected GUIStyle Small => small;
        protected GUIStyle Button => button;
    }
}
