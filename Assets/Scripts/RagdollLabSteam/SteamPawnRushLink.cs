using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.PawnRush;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab.Net
{
    /// <summary>
    /// Pawn Rush course 01 online (R92): the lobby's 폰 러쉬 match. Like the sword fight's link, the host
    /// runs the only simulation - every roster pawn's ragdoll (players and bots), their checkpoints and
    /// falls, the mission stations' mini-games - and everyone else sends inputs and draws what the host
    /// sends: ragdoll snapshots (the lab's format) and the course state (PawnRushNetState: the round's two
    /// games and each station's progress and look). It boots in a match scene that has a PawnRushCourse
    /// and a PlaytestSpawner (whose character it spawns for everyone; the spawner itself stays off in a
    /// match). The scene's offline playtest HUD follows the local pawn (Course01Playtest.NetworkDriver).
    ///
    /// R125: the piece skills, the promotion pads and the crown finish, the same rules as the skill test's
    /// online (Docs/Network/SKILLS_ONLINE.md) on this link's own channel:
    /// <list type="bullet">
    /// <item>a client's input is the lab's (RagdollLabInput): the presses as counts, F as the skill press, and the
    /// skill moments it has had; the host plays the presses one per physics step;</item>
    /// <item>with every snapshot the host sends each client a skills packet: the busy pieces' states, the bishops'
    /// wires and the moments that client has not confirmed; a client writes the states into its puppets at once,
    /// plays the moments with the bodies (a measured playback delay), and draws its own press and aim at once;</item>
    /// <item>the host judges the pads (a promotion travels in the snapshot's piece) and the crown platform; the
    /// course state carries the pads, the race clock, the finishers and the winner (CFP2). When a team wins the
    /// host closes the room to new players (SteamSession.EndMatch), every PC shows the winner, loads its result
    /// scene, and from there leaves the match for the lobby (PawnRushResultFlow).</item>
    /// </list>
    ///
    /// R126 (mini-games v1.0, the 5-player test: "on every PC but the host, physics acted differently from what
    /// the screen showed"):
    /// <list type="bullet">
    /// <item>obstacles: the host runs them on the shared clock advanced with its physics steps and stamps that time
    /// into every snapshot; a client runs them at the stamp interpolated at the moment whose pawns it draws
    /// (HostObstacleClock, as the lab since R51). Before, a client drew the obstacles "now" and the pawns 100-200 ms
    /// ago, so a hammer was drawn hitting where the host's had long passed;</item>
    /// <item>only the host's obstacles push anyone (ObstacleClock.Authority; they pushed nobody anywhere before);</item>
    /// <item>the start bar sinks at the match's shared start on every PC (StartBar.ReleaseAtClock);</item>
    /// <item>the course state carries the host's time and each station's game block (CFP3; CFP4 adds the pads' choosers); a client applies it when
    /// its playback reaches that time, so gates, bridges, capstans and carried planks move with the bodies.</item>
    /// </list>
    ///
    /// R502 (host changes): every PC keeps each pawn's checkpoint from what its own physics sees (a client's puppets
    /// touch the same triggers), so a new host puts a fallen pawn back where the old host would have, not on the start
    /// square; a moment after taking over it also puts back whoever the old host had waiting in the water, a kill volume,
    /// an enemy team's zone or a long fall (Rearm).
    ///
    /// R508 (the match on sections): the lobby's 폰 러쉬 now loads PawnRush_Round, a PawnRushRound with a crown
    /// finish on the war room's goal. As the scene loads, before anything starts, this link builds the round from the
    /// match's number (PawnRushSections.MatchSeed: every PC draws the same sections and seeds, nothing is sent) and
    /// moves its start to the lobby's go once that is known (MatchCourse.StartAt). The course state names the round's
    /// stations (each mini-game section's white then black) and carries the sections' own state (ISectionNetPart:
    /// the guess bridge's squares, the tilting pads, the carriage yard's final gate). Course 01 still works the same
    /// way through MatchCourse (its draw and its pads).
    ///
    /// Not yet: bots only stand at the start.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class SteamPawnRushLink : MonoBehaviour
    {
        const int Channel = 34;                 // 31 capsule motion, 32 lab, 33 sword fight
        // R306: the snapshots go on a schedule at the host's rate (SnapshotRate: 60 a second for a few players, else 30), the
        // course state at 30 whatever it; a client's input 60 times a second and at once when it changes (InputSendGate).
        // InputInterval: the input's timing before R306 ("-latencyBefore=12").
        const float InputInterval = 1f / 60f;
        const double StateInterval = 1.0 / 30;
        const float InputTimeout = .35f, SilenceTimeout = 12f, FallLimit = -40f;
        /// <summary>R306 [6]: a development build's switch for drawing this PC's pawn sooner, and its command-line form.</summary>
        const KeyCode OwnLeadKey = KeyCode.F7;
        const string OwnLeadArgument = "-ownLead";
        /// <summary>A moment played this much later than the poses it belongs to is not drawn any more (ms).</summary>
        const double StaleEventMs = 1000;
        /// <summary>R508: a round's start until the lobby's go is known (s ahead on the obstacle clock). Under an hour:
        /// the yard's carriages take a start more than RoamingFleet.Leap away for a clock leap and set off at once.</summary>
        const double ProvisionalStart = 600;

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
            if (runtime == null || runtime.Session == null || !runtime.Session.Started || runtime.Session.Match == 0) return;
            var session = runtime.Session;
            var course = FindFirstObjectByType<PawnRushCourse>();
            var round = course == null ? FindFirstObjectByType<PawnRushRound>() : null;
            var spawner = FindFirstObjectByType<PlaytestSpawner>(FindObjectsInactive.Include);
            if ((course == null && round == null) || spawner == null || FindFirstObjectByType<SteamPawnRushLink>() != null) return;
            // R508: the match's round, built here (the scene has loaded, nothing has started) from the match's number,
            // the same sections and seeds on every PC; its start is the lobby's go (ArmMatchStart), which comes after
            // every PC has loaded, so until then a start ProvisionalStart ahead holds the gates shut and the carriages
            // at home.
            if (round != null)
                round.BuildForMatch(PawnRushSections.MatchSeed(session.Match),
                                    session.StartAt > 0 ? session.StartAt : ObstacleClock.Now + ProvisionalStart);
            var go = new GameObject("ChessFight Pawn Rush Net");
            SceneManager.MoveGameObjectToScene(go, (course != null ? course.gameObject : round.gameObject).scene);   // gone with the match scene
            var link = go.AddComponent<SteamPawnRushLink>();
            link.match = course != null ? MatchCourse.Of(course) : MatchCourse.Of(round);
            link.spawner = spawner;
        }

        sealed class Runner
        {
            public ulong Id;
            public int Team, Slot;
            public bool Bot;
            public RagdollPawn Pawn;
            public RagdollDriver Driver;
            // Where it comes back: on every PC (R502, OnCheckpoint), so a new host has it.
            public int Checkpoint = int.MinValue;
            public Vector3 RespawnAt;
            public Quaternion RespawnRotation;
            // Host only: when.
            public float RespawnTime = -1f;
            // Host only: the owner's latest input, the presses it has counted and those waiting for a physics step.
            public RagdollLabInput Input;
            public uint Sequence, ClientTimeMs;
            public float InputAt = -1f;
            public bool Synced, AckSet, InteractLatch;
            public byte Jumps, Shoves, Abilities, Abilities2, Skills;
            public int Jump, Shove, Ability, Ability2, Skill;
            // R306: the newest input a physics step has played (the snapshots echo its send time), and when the first
            // input since then arrived.
            public uint PlayedTimeMs;
            public float PlayedAt = -1f;
            public double ArrivedAt = -1d;
            // The last promotion choice heard (R130: the lab input's pieceSeq).
            public byte PieceSeq;
            public ushort EventAck;
        }

        // What the match runs on (R508): course 01 or a section round.
        MatchCourse match;
        bool matchStartSet;
        // R514: this PC's stamina bar (the offline playtest's, which a match switches off).
        MatchStaminaBar staminaBar;
        PlaytestSpawner spawner;
        NetworkRuntime runtime;
        SteamSession session;
        RagdollPawn prefab;
        OrbitCamera orbit;
        IMoveInputSource controls;
        Material white, black;
        bool authority;
        // The host this PC follows, to notice a change of it on every PC (R295, SetAuthority).
        readonly HostWatch hostWatch = new HostWatch();
        // R502: when a new host looks for the respawns the old one had waiting (Rearm), -1 when it does not.
        float rearmAt = -1f;
        const float RearmDelay = .3f;
        Callback<SteamNetworkingMessagesSessionRequest_t> requests;
        CrownFinish finish;

        readonly Dictionary<ulong, Runner> runners = new Dictionary<ulong, Runner>();
        readonly Dictionary<ICharacterDriver, Runner> byDriver = new Dictionary<ICharacterDriver, Runner>();
        readonly Dictionary<RagdollPawn, Runner> byPawn = new Dictionary<RagdollPawn, Runner>();
        readonly List<ulong> gone = new List<ulong>();
        readonly IntPtr[] incoming = new IntPtr[64];
        // Every message read into one reused array of its length (R297): nothing here keeps one past its turn.
        readonly PacketPool inbound = new PacketPool(RagdollNetProtocol.MaxBytes);
        readonly Dictionary<ulong, RagdollPose> poses = new Dictionary<ulong, RagdollPose>();
        readonly Dictionary<ulong, int> snaps = new Dictionary<ulong, int>();
        readonly List<RagdollPose> outgoing = new List<RagdollPose>();
        readonly List<RagdollSnapshot> buffer = new List<RagdollSnapshot>();
        readonly Stack<RagdollSnapshot> spare = new Stack<RagdollSnapshot>();
        readonly RagdollPose blend = new RagdollPose();
        readonly PawnRushNetState state = new PawnRushNetState(), received = new PawnRushNetState();
        uint tick, stateTick, inputSequence, lastPose, lastState;
        float sentAt, inputAt;
        // R306 [1] [2] [5]: the host's snapshot and course state schedules and its rate, a client's input gate and the
        // input it last sent.
        SendSchedule snapshotSchedule, stateSchedule;
        double snapshotInterval = SnapshotRate.Normal;
        InputSendGate inputGate;
        RagdollLabInput lastSent;
        // R306 [3]: the frame whose packets were read (the host reads in the frame's first physics step).
        int receivedFrame = -1;
        // R306 [0]: the host's inputs' wait from arriving to a physics step playing them, a client's presses timed, and
        // how the packets go (read once a second).
        readonly LatencyAverage applyWait = new LatencyAverage(64);
        readonly PressTimer presses = new PressTimer();
        string routeLine = "";
        // R521: the same routes as numbers, and the players the host's last snapshot went to (the metrics box's card).
        RouteSummary routes = RouteSummary.None;
        int snapshotReceivers;
        // R306 [6] (HOST_AUTHORITY P3): this PC's own pawn drawn the lost-snapshot margin sooner (PlaybackDelay.LeadMs),
        // and the obstacles with it - a development build's A/B switch, off unless F7 or -ownLead turns it on.
        bool ownLead;
        double ownLeadMs;
        static LatencyBefore Before => LatencyTest.Before;
        // A client: how long the host has been silent (R295: from its first packet in this scene or the shared start).
        readonly HostSilence hostSilence = new HostSilence();
        double playback;
        readonly PlaybackDelay delay = new PlaybackDelay();
        float roundTripMs;
        // R297: the match's metrics box (NetMetricsOverlay, a development build, F3): what went out and came in, a second.
        int sentBytes, receivedBytes, snapshotsOut, snapshotsIn, snapshotBytesLast;
        int sentRate, receivedRate, snapshotOutRate, snapshotInRate;
        float statsAt;
        // R297: a development build's autopilot (-autopilot, AutoPilot) plays this PC's pawn.
        AutoPilot pilot;

        // A client's own presses, counted (D-S7), and the rest of its input waiting for the next send.
        RagdollLabInput pending;
        byte myJumps, myShoves, myAbilities, myAbilities2, mySkills;
        bool grabWasDown, useWasDown;

        // The skills (R125, as LabNetLink R111).
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
        bool skillsApplied, replaying, ended;

        // The obstacle clock (R126): the shared clock the runtime set, advanced with the physics steps on the host,
        // the host's stamps on a client.
        Func<double> sharedClock;
        readonly HostObstacleClock hostObstacles = new HostObstacleClock();
        double obstacleOffset;
        bool obstacleSynced, startBarSet;

        // Course states waiting for the playback to reach their host time (a client), oldest first.
        readonly List<PawnRushNetState> stateQueue = new List<PawnRushNetState>();
        readonly Stack<PawnRushNetState> statePool = new Stack<PawnRushNetState>();

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            runtime = NetworkRuntime.Instance;
            session = runtime.Session;
            hostSilence.Enter(Time.realtimeSinceStartup);
            requests = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(c =>
            {
                if (session.IsPeer(c.m_identityRemote.GetSteamID64())) SteamNetworkingMessages.AcceptSessionWithUser(ref c.m_identityRemote);
            });
        }

        void Start()
        {
            prefab = spawner.CharacterPrefab != null ? spawner.CharacterPrefab.GetComponent<RagdollPawn>() : null;
            orbit = spawner.Orbit != null ? spawner.Orbit : FindFirstObjectByType<OrbitCamera>();
            if (prefab == null)
            {
                Debug.LogError("[PawnRush] 경기용 래그돌 프리팹이 없습니다 (PlaytestSpawner의 캐릭터 프리팹).");
                enabled = false;
                return;
            }
            white = new Material(prefab.skin.sharedMaterial) { color = new Color(.96f, .93f, .84f) };
            black = new Material(prefab.skin.sharedMaterial) { color = new Color(.15f, .18f, .24f) };
            controls = MoveInputSources.Create();
            controls.Enable();
            authority = session.IsHost;
            hostWatch.Changed(session.Host, authority);
            MiniGameBase.Remote = !authority;
            finish = match != null ? match.Finish : null;
            if (finish != null)
            {
                finish.Authority = authority;
                finish.Decided += OnDecided;
            }
            if (PawnRushCourseSkills.Current != null) PawnRushCourseSkills.Current.Online = true;
            // The obstacle clock: this link's from now on (MatchObstacleTime), the runtime's shared one underneath.
            sharedClock = ObstacleClock.Source ?? (() => Time.timeAsDouble);
            SyncObstacleClock();
            ObstacleClock.Use(MatchObstacleTime);
            ObstacleClock.Authority = authority;
            // The mini-games name pawns by their roster seat on the wire.
            MiniGameBase.SeatOf = d => d != null && byDriver.TryGetValue(d, out var r) ? RefOf(r) : MiniGameBase.NoSeat;
            MiniGameBase.DriverOf = seat =>
            {
                var pawn = PawnOf(seat);
                return pawn != null && byPawn.TryGetValue(pawn, out var r) ? r.Driver : null;
            };
            // R130: a client's promotion choice rides its input (pieceAsk / pieceSeq) to the host.
            PromotionChooser.Asked = authority ? null : (Action<PieceKind>)AskPiece;
            Checkpoint.Reached += OnCheckpoint;
            KillVolume.Entered += OnKill;
            FallDistanceRespawn.Fell += OnFell;
            WaterZone.Entered += OnWater;
            TeamZone.Intruded += OnIntruded;
            RagdollPawn.SkillFx += OnPawnRushFx;
            // R514: this PC's stamina, the bar the offline playtest shows (the playtest is off in a match); on once the race goes.
            staminaBar = gameObject.AddComponent<MatchStaminaBar>();
            staminaBar.enabled = false;
            SyncRoster();
            NetMetricsOverlay.Attach(gameObject, CurrentMetrics);
            if (Debug.isDebugBuild && AutoPilot.Requested(Environment.GetCommandLineArgs(), session.Self, out uint seed))
                pilot = new AutoPilot(seed);
            ownLead = Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), OwnLeadArgument) >= 0;
        }

        /// <summary>R297: the numbers of the match's metrics box (NetMetricsOverlay).</summary>
        NetMetrics CurrentMetrics() => new NetMetrics
        {
            Host = authority, Players = runners.Count, SentBytes = sentRate, ReceivedBytes = receivedRate,
            SnapshotBytes = snapshotBytesLast, SnapshotsSent = snapshotOutRate, SnapshotsReceived = snapshotInRate,
            RoundTripMs = roundTripMs, PlaybackDelayMs = delay.Milliseconds, LossShare = delay.LossShare,
            Piloted = pilot != null, PilotSeed = pilot != null ? pilot.Seed : 0u,
            SnapshotRateTarget = (int)Math.Round(1d / snapshotInterval), JitterMs = delay.JitterMs, IntervalMs = delay.IntervalMs,
            ApplyWaitMs = applyWait.Average, Answered = presses.Answered.Average, AnsweredLast = presses.Answered.Last,
            Seen = presses.Seen.Average, PressesTimed = presses.Seen.Count, PressesLost = presses.Lost, Route = routeLine,
            OwnLead = authority ? "" : ownLead ? $"내 폰 먼저 그리기 (P3, F7): 켜짐 · {ownLeadMs:F1} ms 먼저 (장애물도 내 폰 시각)"
                                                : "내 폰 먼저 그리기 (P3, F7): 꺼짐",
            Receivers = snapshotReceivers, Routes = routes,
        };

        void UpdateStats()
        {
            if (Time.realtimeSinceStartup < statsAt) return;
            statsAt = Time.realtimeSinceStartup + 1f;
            sentRate = sentBytes;
            receivedRate = receivedBytes;
            snapshotOutRate = snapshotsOut;
            snapshotInRate = snapshotsIn;
            sentBytes = receivedBytes = snapshotsOut = snapshotsIn = 0;
            // R306 [0]: straight or through Steam's relays (a development build's metrics box).
            routes = Debug.isDebugBuild ? SteamRoute.Summary(session) : RouteSummary.None;
            routeLine = Debug.isDebugBuild ? SteamRoute.Line(session, routes) : "";
        }

        void OnDestroy()
        {
            requests?.Dispose();
            controls?.Disable();
            Checkpoint.Reached -= OnCheckpoint;
            KillVolume.Entered -= OnKill;
            FallDistanceRespawn.Fell -= OnFell;
            WaterZone.Entered -= OnWater;
            TeamZone.Intruded -= OnIntruded;
            RagdollPawn.SkillFx -= OnPawnRushFx;
            if (finish != null) finish.Decided -= OnDecided;
            Course01Playtest.NetworkDriver = null;
            SectionPlaytest.NetworkDriver = null;
            MiniGameBase.LocalDriver = null;
            MissionStation.LocalTeam = Teams.White;
            MiniGameBase.Remote = false;
            PromotionChooser.Asked = null;
            MiniGameBase.SeatOf = null;
            MiniGameBase.DriverOf = null;
            if (sharedClock != null) ObstacleClock.Use(sharedClock);
            ObstacleClock.Authority = true;
            MatchPieces.Clear();
            // Do not close the peer sessions: the lobby's motion transport owns them.
        }

        // ------------------------------------------------------------------ roster

        void SyncRoster()
        {
            gone.Clear();
            foreach (var id in runners.Keys) if (!session.Roster.ContainsKey(id)) gone.Add(id);
            foreach (var id in gone) Remove(id);
            foreach (var e in session.Roster)
                if (!runners.ContainsKey(e.Key)) Spawn(e.Key, Mathf.Clamp(e.Value.Team, 0, 1), Mathf.Max(0, e.Value.Slot));
        }

        void Spawn(ulong id, int team, int slot)
        {
            SpawnSpot(team, slot, out var at, out var rotation);
            var pawn = Instantiate(prefab, at + Vector3.up * .02f, rotation);
            string name = session.Name(id);
            pawn.name = name;
            pawn.DisplayName = name;
            pawn.Team = team;
            pawn.skin.sharedMaterial = team == Teams.White ? white : black;
            var driver = pawn.GetComponent<RagdollDriver>();
            if (driver == null) driver = pawn.gameObject.AddComponent<RagdollDriver>();
            driver.Teleport(at, rotation);
            if (!authority) pawn.SetNetworkPuppet(true);
            var r = new Runner { Id = id, Team = team, Slot = slot, Bot = BotIdentity.IsBot(id), Pawn = pawn, Driver = driver,
                                 RespawnAt = at, RespawnRotation = rotation };
            runners[id] = r;
            byDriver[driver] = r;
            byPawn[pawn] = r;
            if (finish != null) finish.Register(driver, team, name, counts: !r.Bot, self: id == session.Self, seat: RefOf(r), host: id == session.Host);
            if (id == session.Self)
            {
                Course01Playtest.NetworkDriver = driver;
                Course01Playtest.NetworkTeam = team;
                SectionPlaytest.NetworkDriver = driver;
                SectionPlaytest.NetworkTeam = team;
                // Whose fanfare is loud, whose aiming arc shows (R508: the match's own pawn, as the playtests do offline).
                MissionStation.LocalTeam = team;
                MiniGameBase.LocalDriver = driver;
                if (staminaBar != null) staminaBar.Follow(driver);
                if (PawnRushCourseSkills.Current != null) PawnRushCourseSkills.Current.Local = pawn;
                if (orbit != null) orbit.Follow(driver.FollowTarget, pawn.transform, rotation * Vector3.forward);
            }
        }

        void Remove(ulong id)
        {
            if (!runners.TryGetValue(id, out var r)) return;
            runners.Remove(id);
            byDriver.Remove(r.Driver);
            if (r.Pawn != null) byPawn.Remove(r.Pawn);
            poses.Remove(id);
            snaps.Remove(id);
            if (finish != null) finish.Unregister(r.Driver);
            if (r.Pawn != null) Destroy(r.Pawn.gameObject);
        }

        // The start square's spot for this team and slot (six a team, built by the course), else the spawner.
        void SpawnSpot(int team, int slot, out Vector3 at, out Quaternion rotation)
        {
            foreach (var point in FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (point.Team != team || point.Index != slot % 6) continue;
                at = point.transform.position;
                rotation = point.transform.rotation;
                return;
            }
            at = spawner.transform.position + new Vector3((team == Teams.White ? -1f : 1f) * (2f + slot % 3 * 2f), 0f, -(slot / 3) * 4f);
            rotation = spawner.transform.rotation;
        }

        // A piece by its roster place (one byte on the wire: the roster is the same on every PC).
        static byte RefOf(Runner r) => r != null ? SkillWire.Ref(r.Team, r.Slot) : SkillWire.None;

        byte RefOf(RagdollPawn pawn) => pawn != null && byPawn.TryGetValue(pawn, out var r) ? RefOf(r) : SkillWire.None;

        RagdollPawn PawnOf(byte reference)
        {
            if (reference == SkillWire.None) return null;
            SkillWire.Place(reference, out int team, out int slot);
            foreach (var r in runners.Values)
                if (r.Team == team && r.Slot == slot) return r.Pawn;
            return null;
        }

        // ------------------------------------------------------------------ host change

        // The host changed, or this PC's role in it (R295: on every PC, hostWatch; it used to run only on the two PCs
        // whose roles swapped, and a third PC kept the old host's numbers and dropped the new host's course states and
        // skills as old ones, so its race clock, pads and skills stood still). Every PC starts the stream from the host
        // over; the new host carries on the old one's numbering.
        void SetAuthority(bool host)
        {
            // R504: the obstacle time this PC showed as the host, for HostObstacleClock.Hold below.
            bool wasHost = authority;
            double shownObstacles = HostObstacleTime();
            authority = host;
            MiniGameBase.Remote = !host;
            ObstacleClock.Authority = host;
            if (finish != null) finish.Authority = host;
            if (host)
            {
                // The newest pose, not the drawn one (the playback delay behind), and the skills from where they were.
                if (buffer.Count > 0)
                {
                    var newest = buffer[buffer.Count - 1];
                    for (int i = 0; i < newest.count; i++)
                    {
                        var pose = newest.At(i);
                        if (runners.TryGetValue(pose.id, out var r) && r.Pawn != null) r.Pawn.ApplyNetworkPose(pose);
                    }
                }
                foreach (var r in runners.Values)
                    if (r.Pawn != null)
                    {
                        r.Pawn.SetNetworkPuppet(false);
                        r.Pawn.AdoptSkillNet();
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
                tick = lastPose;
                stateTick = lastState;
            }
            else
            {
                foreach (var r in runners.Values) if (r.Pawn != null) r.Pawn.SetNetworkPuppet(true);
                foreach (var wire in SkillTripwire.LiveWires)
                {
                    if (wire == null || wire.Remote) continue;
                    wire.Yield();
                    remoteWires[wire.NetId] = wire;
                }
                lastPose = lastState = 0;
            }
            while (buffer.Count > 0) { spare.Push(buffer[0]); buffer.RemoveAt(0); }
            foreach (var r in runners.Values)
            {
                r.InputAt = r.PlayedAt = -1f;
                r.ArrivedAt = -1d;
                r.Sequence = 0;
                r.RespawnTime = -1f;
                r.Synced = r.AckSet = false;
                r.Jump = r.Shove = r.Ability = r.Ability2 = r.Skill = 0;
            }
            pending = default;
            snapshotSchedule.Reset();
            stateSchedule.Reset();
            snapshotInterval = SnapshotRate.Normal;
            inputGate.Reset();
            lastSent = default;
            presses.Reset();
            applyWait.Clear();
            // R504: the obstacles never step back at a host change (HostObstacleClock: that shakes anyone standing on a
            // platform). Clearing the clock here moved them 100-200 ms back on every client and on the old host: the new
            // host's first snapshot is drawn the playback delay behind. A client keeps its clock, which holds still until
            // the new stream passes it; a host that became a client holds from the time it showed.
            if (wasHost && !host) hostObstacles.Hold(shownObstacles, Time.timeAsDouble);
            obstacleSynced = false;
            while (stateQueue.Count > 0) { statePool.Push(stateQueue[0]); stateQueue.RemoveAt(0); }
            eventLog.Clear();
            inbox.Clear();
            eventQueue.Clear();
            skillsApplied = false;
            playback = 0d;
            roundTripMs = 0f;
            delay.Reset();
            hostSilence.Enter(Time.realtimeSinceStartup);
            // R502: the respawns waiting on the old host went with it (only a host schedules them); a moment from now,
            // once this PC's own physics has moved the bodies on from the newest pose, Rearm finds them again.
            rearmAt = host ? Time.time + RearmDelay : -1f;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (!session.Started || session.Match == 0) return;
            if (hostWatch.Changed(session.Host, session.IsHost) || session.IsHost != authority) SetAuthority(session.IsHost);
            SyncRoster();
            SyncObstacleClock();
            ArmStartBar();
            ArmMatchStart();
            // R514: shown from the shared start on (before it the loading screen is up), until the race is decided.
            if (staminaBar != null) staminaBar.enabled = Started() && !ended;
            // The match sheet's piece icons (VoiceHud, R131): who has been promoted, as this PC draws them.
            foreach (var r in runners.Values) MatchPieces.Set(r.Id, r.Pawn != null ? r.Pawn.Piece : PieceKind.Pawn);
            // A client's skill timers run before this frame's packets overwrite them.
            if (!authority)
                foreach (var r in runners.Values)
                    if (r.Pawn != null && r.Pawn.NetworkPuppet) r.Pawn.AdvanceSkillView(Time.unscaledDeltaTime);
            ReceiveOnce();
            var local = ReadLocalInput();
            float now = Time.realtimeSinceStartup;
            if (authority)
            {
                if (runners.TryGetValue(session.Self, out var self)) self.Pawn.SetInput(local);
                if (rearmAt >= 0f && Time.time >= rearmAt) { rearmAt = -1f; Rearm(); }
                foreach (var r in runners.Values) CheckRespawn(r);
                if (SnapshotDue(now)) SendSnapshot();   // the course state goes with it, 30 a second at most
                session.ReportHostSilence(0f);
            }
            else
            {
                if (Debug.isDebugBuild && LegacyKeys.Down(OwnLeadKey) && !ChatBox.KeysHeld) ownLead = !ownLead;
                ClientInput(local, now);
                Playback();
                ApplyDueStates();
                DispatchEvents();
                // R295: counted from the host's first packet in this scene or the shared start, whichever comes first.
                // The session hands a silent host's role on (HOST.md); the lobby's motion stream that used to tell it
                // stops in this scene, so this link tells it instead. Leaving is the last resort.
                float silence = hostSilence.Seconds(now, Started());
                session.ReportHostSilence(silence);
                if (silence > SilenceTimeout && !ended) session.Abort("폰 러쉬 방장 연결이 끊어졌습니다.");
            }
            UpdateStats();
        }

        // R306 [1]: the host's snapshot on its schedule ("-latencyBefore=1": an interval since the last one, as before).
        bool SnapshotDue(float now) => Before.Slipping ? now - sentAt >= snapshotInterval
            : snapshotSchedule.Due(Time.realtimeSinceStartupAsDouble, snapshotInterval);

        // R306 [3]: this frame's packets, read once - by the host in the frame's first physics step, so an input that
        // arrived since the last frame plays in this frame's physics instead of the next frame's; else in Update.
        void ReceiveOnce()
        {
            if (receivedFrame == Time.frameCount) return;
            receivedFrame = Time.frameCount;
            Receive();
        }

        // Host: every other player's latest input, the presses one per physics step, or standing still once it is
        // stale. Bots stand.
        void FixedUpdate()
        {
            if (!authority) return;
            // R306 [3]: not while a host change is still on its way to this PC's roles (Update's SetAuthority): the new
            // host's snapshots would be read as inputs and dropped.
            if (!Before.ReadInUpdate && session.IsHost && session.Started && session.Match != 0) ReceiveOnce();
            float now = Time.realtimeSinceStartup;
            foreach (var r in runners.Values)
            {
                if (r.Id == session.Self) continue;
                if (r.Bot || r.InputAt < 0f || now - r.InputAt > InputTimeout)
                {
                    r.Pawn.SetInput(default);
                    continue;
                }
                Played(r, now);
                var input = r.Input.Held();
                input.interact = false;   // F is the skill key on this course
                input.jump = Take(ref r.Jump);
                input.shove = Take(ref r.Shove);
                input.ability = Take(ref r.Ability);
                input.ability2 = Take(ref r.Ability2);
                input.skill = Take(ref r.Skill);
                r.Pawn.SetInput(input);
            }
        }

        static bool Take(ref int presses)
        {
            if (presses <= 0) return false;
            presses--;
            return true;
        }

        // R306 [0]: this player's newest input is in this physics step - the snapshots echo it from now on (the round trip
        // and the press timing include the wait for the step) - and how long it waited since it arrived.
        void Played(Runner r, float now)
        {
            r.PlayedTimeMs = r.ClientTimeMs;
            r.PlayedAt = now;
            if (r.ArrivedAt < 0d) return;
            applyWait.Add((Time.realtimeSinceStartupAsDouble - r.ArrivedAt) * 1000d);
            r.ArrivedAt = -1d;
        }

        // The local player's keys against the orbit camera, as the offline playtest reads them. Read every
        // frame (edges are not kept for later) and dropped while the loading screen or the chat holds input.
        // F (the use key) is the skill key on this course (R125).
        PawnInput ReadLocalInput()
        {
            var intent = controls.Read();
            if (pilot != null) return Piloted();
            bool blocked = runtime.InputBlocked || !Application.isFocused || (orbit != null && orbit.FreeFly);
            bool use = !blocked && intent.Interact;
            bool skill = use && !useWasDown;
            useWasDown = use;
            if (blocked) return default;
            Vector3 move = orbit != null ? orbit.FlatRight * intent.Move.x + orbit.FlatForward * intent.Move.y
                                         : new Vector3(intent.Move.x, 0f, intent.Move.y);
            return new PawnInput
            {
                move = Vector3.ClampMagnitude(move, 1f),
                aim = orbit != null ? orbit.AimForward : Vector3.forward,
                jump = intent.Jump, shove = intent.Shove, shoveHeld = intent.ShoveHeld, grab = intent.Grab,
                sprint = intent.Sprint, ability = intent.Ability, ability2 = intent.Ability2, skill = skill,
            };
        }

        // R297: the autopilot's keys in place of the player's - a walk against the orbit camera, a sprint, a jump, a click
        // and the skill key - whether the window is in front or not; nothing behind the loading screen or the chat.
        PawnInput Piloted()
        {
            var f = pilot.Step(Time.unscaledDeltaTime);
            if (runtime.InputBlocked) return default;
            Vector3 move = orbit != null ? orbit.FlatRight * f.MoveX + orbit.FlatForward * f.MoveY : new Vector3(f.MoveX, 0f, f.MoveY);
            return new PawnInput
            {
                move = Vector3.ClampMagnitude(move, 1f),
                aim = orbit != null ? orbit.AimForward : Vector3.forward,
                jump = f.Jump, shove = f.Click, sprint = f.Sprint, skill = f.Skill,
            };
        }

        // ------------------------------------------------------------------ host: checkpoints and falls

        bool Find(ICharacterDriver who, out Runner r) => Known(who, out r) && authority;

        bool Known(ICharacterDriver who, out Runner r)
        {
            r = null;
            return who != null && byDriver.TryGetValue(who, out r);
        }

        // R502: on every PC, not only the host's: a client's puppets pass through the same triggers, and a new host
        // used to start everyone's checkpoints over, so the next fall went back to the start square.
        void OnCheckpoint(ICharacterDriver who, Checkpoint checkpoint)
        {
            if (!Known(who, out var r) || checkpoint.Order <= r.Checkpoint) return;
            var spots = checkpoint.GetComponent<CourseCheckpoint>();
            if (spots != null && !spots.Shared && spots.Team != r.Team) return;
            r.Checkpoint = checkpoint.Order;
            r.RespawnAt = spots != null && spots.SpotCount > 0 ? spots.SpotWorld(r.Slot % spots.SpotCount) : checkpoint.RespawnPosition;
            r.RespawnRotation = checkpoint.transform.rotation;
        }

        void Schedule(ICharacterDriver who, float delay)
        {
            if (Find(who, out var r) && r.RespawnTime < 0f) r.RespawnTime = Time.time + Mathf.Max(0f, delay);
        }

        void OnKill(ICharacterDriver who, KillVolume volume) => Schedule(who, volume.RespawnDelay);
        void OnFell(ICharacterDriver who, FallDistanceRespawn rule) => Schedule(who, rule.RespawnDelay);
        void OnWater(ICharacterDriver who, WaterZone water) => Schedule(who, water.RespawnDelay);
        void OnIntruded(ICharacterDriver who, TeamZone zone) => Schedule(who, 0f);

        // R502: the respawns the old host had waiting. A zone tells only of its first touch, and this PC had that touch
        // while it was a client (the zones count the puppets' limbs in and out too), so it never told again: a pawn in
        // the water or a kill volume stayed there for good. A moment after the takeover (a pawn the old host had just put
        // back is out of the zone by then) whoever is still in one, in an enemy team's zone, or falling past the drop
        // rule is put back after that rule's delay, counted from now.
        void Rearm()
        {
            var kills = FindObjectsByType<KillVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var waters = FindObjectsByType<WaterZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var zones = FindObjectsByType<TeamZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var r in runners.Values)
            {
                if (r.Pawn == null || r.Driver == null || r.RespawnTime >= 0f) continue;
                foreach (var volume in kills) if (volume.Holds(r.Driver)) Schedule(r.Driver, volume.RespawnDelay);
                foreach (var water in waters) if (water.Report && water.Holds(r.Driver)) Schedule(r.Driver, water.RespawnDelay);
                Vector3 hips = r.Pawn.Hips.position;
                foreach (var zone in zones) if (zone.Team != r.Team && zone.Bounds.Contains(hips)) Schedule(r.Driver, 0f);
                var drop = r.Pawn.GetComponentInChildren<FallDistanceRespawn>();
                if (drop != null && drop.Falling) Schedule(r.Driver, drop.RespawnDelay);
            }
        }

        void CheckRespawn(Runner r)
        {
            if (r.Pawn == null) return;
            bool fell = r.Pawn.Hips.position.y < FallLimit || !r.Pawn.IsFinite();
            if (!fell && (r.RespawnTime < 0f || Time.time < r.RespawnTime)) return;
            r.RespawnTime = -1f;
            r.Driver.Teleport(r.RespawnAt, r.RespawnRotation);   // marks the next snapshot as a jump cut
            var drop = r.Pawn.GetComponentInChildren<FallDistanceRespawn>();
            if (drop != null) drop.Forget();
            if (r.Id == session.Self && orbit != null) orbit.Cut();
        }

        // ------------------------------------------------------------------ the race's end

        // A team has a majority on the crown platform (the host decided it, or a client heard it).
        void OnDecided(int team)
        {
            ended = true;
            // The result scene goes back to the lobby by leaving the match (the runtime loads the lobby then).
            var s = session;
            PawnRushResultFlow.Leave = () =>
            {
                if (s != null && s.Match != 0) s.Cancel();
                else SceneManager.LoadScene(SceneNames.Lobby);
            };
            if (!authority) return;
            // Nobody is let into the empty seats of a decided race; the winner goes out reliably as well.
            session.EndMatch();
            SendState(NowMs(), true);
        }

        // ------------------------------------------------------------------ host: sending

        static uint NowMs() => (uint)(Time.realtimeSinceStartupAsDouble * 1000);

        void SendSnapshot()
        {
            sentAt = Time.realtimeSinceStartup;
            uint stamp = NowMs();
            ++tick;
            outgoing.Clear();
            foreach (var r in runners.Values)
            {
                if (r.Pawn == null) continue;
                if (!poses.TryGetValue(r.Id, out var pose)) poses[r.Id] = pose = new RagdollPose();
                r.Pawn.CaptureNetworkPose(pose);
                pose.id = r.Id;
                // The newest input time a physics step has played from that player, for its round trip (0 = none yet, or
                // played too long ago for the 16-bit echo, R297; played rather than heard since R306).
                pose.ack = r.PlayedAt >= 0f && sentAt - r.PlayedAt < RagdollWire.AckFreshSeconds ? r.PlayedTimeMs : 0u;
                if (r.Pawn.NetworkSnap) { snaps[r.Id] = 4; r.Pawn.NetworkSnap = false; }
                snaps.TryGetValue(r.Id, out int n);
                pose.snap = n > 0;
                snaps[r.Id] = Mathf.Max(0, n - 1);
                outgoing.Add(pose);
            }
            // The obstacle time these poses were simulated at: clients draw the obstacles at it.
            byte[] bytes = RagdollNetProtocol.Snapshot(session.Match, tick, stamp, outgoing, ObstacleClock.Now);
            snapshotBytesLast = bytes.Length;
            snapshotsOut++;
            int receivers = 0;
            foreach (var id in session.Roster.Keys) if (Send(id, bytes, false)) receivers++;
            snapshotReceivers = receivers;
            SendSkills(stamp);
            // The course state 30 times a second whatever the snapshots' rate (R306 [5]): its stations and pads change
            // slowly, and the clients play it at its host time.
            if (snapshotInterval >= SnapshotRate.Normal || stateSchedule.Due(Time.realtimeSinceStartupAsDouble, StateInterval)) SendState(stamp);
            // R306 [5]: 60 snapshots a second while they cost little enough (SnapshotRate), else 30.
            snapshotInterval = Before.Always30 ? SnapshotRate.Normal : SnapshotRate.Interval(receivers, outgoing.Count);
        }

        /// <summary>The skills packet, to each client with every snapshot: the busy pieces, the bishops' wires, and the
        /// moments that client has not confirmed yet (as many as fit; the rest go in the next one).</summary>
        void SendSkills(uint stamp)
        {
            eventLog.Expire(stamp);
            skillOut.Clear();
            skillOut.HostTimeMs = stamp;
            skillOut.FirstSeq = eventLog.First;
            foreach (var r in runners.Values)
            {
                var pawn = r.Pawn;
                if (pawn == null || pawn.SkillNetIdle || skillOut.States.Count >= SkillWire.MaxStates) continue;
                var s = new SkillWireState { Pawn = RefOf(r) };
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
            int budget = RagdollNetProtocol.MaxBytes - RagdollNetProtocol.HeaderBytes;
            int fit = SkillWire.EventsThatFit(budget, skillOut.States.Count, skillOut.Wires.Count, 0, 255);
            foreach (var r in runners.Values)
            {
                if (r.Id == session.Self || r.Bot) continue;
                ushort acked = r.AckSet ? r.EventAck : unchecked((ushort)(eventLog.First - 1));
                eventLog.After(acked, fit, eventBatch);
                skillOut.Events.Clear();
                skillOut.Events.AddRange(eventBatch);
                Send(r.Id, RagdollNetProtocol.Skills(session.Match, skillOut), false);
            }
        }

        // The host's own skills, recorded as they happen (the effects on its own screen listen to the same events).
        void OnPawnRushFx(SkillFxEvent e)
        {
            if (!authority || replaying) return;
            eventLog.Add(new SkillWireEvent
            {
                Mode = SkillWire.ModePawnRush, Kind = (byte)e.kind, By = RefOf(e.by), Target = RefOf(e.target),
                X = e.at.x, Y = e.at.y, Z = e.at.z, Dx = e.dir.x, Dy = e.dir.y, Dz = e.dir.z,
                Ux = e.normal.x, Uy = e.normal.y, Uz = e.normal.z,
                Count = (byte)Mathf.Clamp(e.count, 0, 255), Size = e.size,
                Source = e.source is SkillTripwire wire && wire != null ? wire.NetId : (ushort)0,
            }, NowMs());
        }

        void SendState(uint stamp, bool reliable = false)
        {
            if (match == null) return;
            state.Tick = ++stateTick;
            state.HostTimeMs = stamp;
            // The course's own fields (course 01: its draw and pads; a round: its sections' bytes), from a clean slate.
            state.First = state.Second = -1;
            state.PadFirst = state.PadSecond = state.PadsUsed = 0;
            for (int i = 0; i < state.PadChooser.Length; i++) state.PadChooser[i] = PawnRushNetState.NoChooser;
            state.PartsLength = 0;
            match.Write(state);
            state.StationCount = Mathf.Min(match.StationCount, PawnRushNetState.MaxStations);
            for (int k = 0; k < state.StationCount; k++)
            {
                var station = match.Station(k);
                var game = station != null ? station.Game : null;
                state.Game[k] = (sbyte)(station != null ? station.GameIndex : -1);
                state.Progress[k] = game != null ? game.Progress01 : 0f;
                state.Completed[k] = game != null && game.Completed;
                var block = new NetBlock(state.Block[k], 0, PawnRushNetState.BlockBytes);
                if (game != null) game.WriteNet(ref block);
                state.BlockLength[k] = block.Written;
            }
            state.Winner = (sbyte)(finish != null ? finish.Winner : PawnRushFinish.None);
            state.Clock = finish != null ? (finish.Winner != PawnRushFinish.None ? finish.WinClock : finish.Clock) : 0f;
            state.FinisherCount = 0;
            if (finish != null)
                foreach (var r in finish.Runners)
                {
                    if (r.FirstOn < 0f || r.Seat == SkillWire.None || state.FinisherCount >= PawnRushNetState.MaxFinishers) continue;
                    state.FinisherSeat[state.FinisherCount] = r.Seat;
                    state.FinisherTime[state.FinisherCount] = r.FirstOn;
                    state.FinisherCount++;
                }
            // A decided race goes out reliably every time, so a client that missed some still hears who won.
            reliable |= finish != null && finish.Winner != PawnRushFinish.None;
            byte[] bytes = state.Write(session.Match);
            foreach (var id in session.Roster.Keys) Send(id, bytes, reliable);
        }

        /// <summary>False when <paramref name="id"/> is no one to send to (this PC, a bot, not in the room).</summary>
        bool Send(ulong id, byte[] bytes, bool reliable)
        {
            if (id == session.Self || !session.IsPeer(id) || BotIdentity.IsBot(id)) return false;
            sentBytes += bytes.Length;
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID64(id);
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                SteamNetworkingMessages.SendMessageToUser(ref identity, handle.AddrOfPinnedObject(), (uint)bytes.Length,
                    (reliable ? Constants.k_nSteamNetworkingSend_Reliable : Constants.k_nSteamNetworkingSend_Unreliable)
                    | Constants.k_nSteamNetworkingSend_NoNagle, Channel);
            }
            finally { handle.Free(); }
            return true;
        }

        // ------------------------------------------------------------------ receiving

        void Receive()
        {
            // Everything that arrived, not one buffer's worth (R297, as the other links): eleven players send 660 inputs a
            // second, so a host below about ten frames a second read them later and later.
            for (int round = 0; round < 8; round++)
            {
                int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, incoming, incoming.Length);
                // R306 [0]: the host times each input from Steam's receipt (its wait for the physics step), on this clock.
                double nowSeconds = Time.realtimeSinceStartupAsDouble;
                long nowUs = authority && count > 0 ? SteamNetworkingUtils.GetLocalTimestamp().m_SteamNetworkingMicroseconds : 0L;
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var msg = SteamNetworkingMessage_t.FromIntPtr(incoming[i]);
                        ulong id = msg.m_identityPeer.GetSteamID64();
                        if (!session.IsPeer(id) || msg.m_cbSize <= 0 || msg.m_cbSize > RagdollNetProtocol.MaxBytes) continue;
                        receivedBytes += msg.m_cbSize;
                        byte[] bytes = inbound.Get(msg.m_cbSize);
                        Marshal.Copy(msg.m_pData, bytes, 0, bytes.Length);
                        if (authority)
                            ReceiveInput(id, bytes, nowSeconds - Math.Max(0L, nowUs - msg.m_usecTimeReceived.m_SteamNetworkingMicroseconds) / 1e6);
                        else if (id == session.Host)
                        {
                            if (PawnRushNetState.Read(bytes, session.Match, received))
                            {
                                if (lastState != 0 && !RagdollNetProtocol.Newer(received.Tick, lastState)) continue;
                                lastState = received.Tick;
                                hostSilence.Heard(Time.realtimeSinceStartup);
                                QueueState(received);
                                continue;
                            }
                            byte type = RagdollNetProtocol.TypeOf(bytes);
                            if (type == RagdollNetProtocol.TypeSkills) ReceiveSkills(bytes);
                            else if (type == RagdollNetProtocol.TypeSnapshot) ReceiveSnapshot(bytes);
                        }
                    }
                    catch (Exception e)
                    {
                        // One message that throws must not cost the others, nor leave them unreleased (R297).
                        ReceiveErrors.Log(Channel, e);
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

        // Host: a player's input. The presses are counts: what is new since the last one heard waits for the physics.
        // arrivedAt: when Steam received it, on Time.realtimeSinceStartupAsDouble's clock (R306 [0]).
        void ReceiveInput(ulong id, byte[] bytes, double arrivedAt)
        {
            if (!runners.TryGetValue(id, out var r)) return;
            if (!RagdollNetProtocol.ReadLabInput(bytes, session.Match, out uint sequence, out uint clientTime, out var fresh)) return;
            if (r.Sequence != 0 && !RagdollNetProtocol.Newer(sequence, r.Sequence)) return;   // overtaken: its counts are stale
            if (!r.Synced)
            {
                // Counts start where they are when the host first hears the player (after a host change too).
                r.Synced = true;
                r.Jumps = fresh.jumps;
                r.Shoves = fresh.shoves;
                r.Abilities = fresh.abilities;
                r.Abilities2 = fresh.abilities2;
                r.Skills = fresh.skills;
                r.PieceSeq = fresh.pieceSeq;
            }
            // A new promotion choice: the pad it is for checks that this pawn is the one choosing.
            if (fresh.pieceSeq != 0 && fresh.pieceSeq != r.PieceSeq)
            {
                r.PieceSeq = fresh.pieceSeq;
                if (r.Driver != null) StationPromotion.Choose(r.Driver, (PieceKind)fresh.pieceAsk);
            }
            r.Jump = Mathf.Min(4, r.Jump + PressCount.Take(fresh.jumps, ref r.Jumps));
            r.Shove = Mathf.Min(4, r.Shove + PressCount.Take(fresh.shoves, ref r.Shoves));
            r.Ability = Mathf.Min(4, r.Ability + PressCount.Take(fresh.abilities, ref r.Abilities));
            r.Ability2 = Mathf.Min(4, r.Ability2 + PressCount.Take(fresh.abilities2, ref r.Abilities2));
            r.Skill = Mathf.Min(4, r.Skill + PressCount.Take(fresh.skills, ref r.Skills));
            r.Input = fresh;
            r.Sequence = sequence;
            r.ClientTimeMs = clientTime;
            r.InputAt = Time.realtimeSinceStartup;
            if (r.ArrivedAt < 0d) r.ArrivedAt = arrivedAt;
            SkillWire.TakeAck(fresh.eventAck, ref r.EventAck, ref r.AckSet);
        }

        // Not the host: my promotion choice, sent in every input from now on with a new number (never 0).
        void AskPiece(PieceKind piece)
        {
            pending.pieceAsk = (byte)piece;
            pending.pieceSeq = (byte)(pending.pieceSeq == 255 ? 1 : pending.pieceSeq + 1);
        }

        // Not the host: count my presses, draw my own press and aim at once (D-S3), send 60 times a second and at once
        // when the input changes (R306 [2]).
        void ClientInput(PawnInput local, float now)
        {
            if (local.jump) myJumps = PressCount.Add(myJumps);
            if (local.shove) myShoves = PressCount.Add(myShoves);
            if (local.ability) myAbilities = PressCount.Add(myAbilities);
            if (local.ability2) myAbilities2 = PressCount.Add(myAbilities2);
            if (local.skill) mySkills = PressCount.Add(mySkills);
            if (local.jump || local.shove || local.ability || local.ability2 || local.skill) presses.Pressed(NowMs());
            pending.Hold(local);
            if (runners.TryGetValue(session.Self, out var me) && me.Pawn != null)
            {
                float hold = Mathf.Clamp(roundTripMs / 1000f + 0.25f, 0.25f, 0.8f);
                if (local.skill) me.Pawn.PredictPress(0, hold);
                if (local.shove) me.Pawn.PredictPress(1, hold);
                if (local.grab && !grabWasDown) me.Pawn.PredictPress(2, hold);
                me.Pawn.ViewAim(local.aim);
            }
            grabWasDown = local.grab;
            // The counts every frame: the gate compares them with the ones last sent.
            pending.jumps = myJumps;
            pending.shoves = myShoves;
            pending.abilities = myAbilities;
            pending.abilities2 = myAbilities2;
            pending.skills = mySkills;
            if (!InputDue(now)) return;
            inputAt = now;
            pending.eventAck = inbox.Started ? inbox.Acked : (ushort)0;
            pending.roundTripMs = (ushort)Mathf.Clamp(Mathf.RoundToInt(roundTripMs), 0, ushort.MaxValue);
            pending.delayMs = (byte)Mathf.Clamp((int)Math.Round(delay.Milliseconds), 0, 255);
            pending.lossPercent = (byte)Mathf.Clamp((int)Math.Round(delay.LossShare * 100d), 0, 100);
            // Unreliable like the lab's: a lost packet only delays a press (the counts carry it), never drops it.
            Send(session.Host, RagdollNetProtocol.LabInput(session.Match, ++inputSequence, NowMs(), pending), false);
            lastSent = pending;
        }

        // R306 [2]: on the 60 Hz schedule, and at once when the input changed (InputSendGate). "-latencyBefore=2": the
        // slots only; with 1 as well, the old "1/60 s since the last send" (slipping a frame each time, as before).
        bool InputDue(float now)
        {
            if (Before.SlotsOnly && Before.Slipping) return now - inputAt >= InputInterval;
            bool changed = !Before.SlotsOnly && pending.DiffersFrom(lastSent);
            return inputGate.Due(Time.realtimeSinceStartupAsDouble, changed, Before.SlotsOnly ? 0d : SendSchedule.Early);
        }

        void ReceiveSnapshot(byte[] bytes)
        {
            var snap = spare.Count > 0 ? spare.Pop() : new RagdollSnapshot();
            if (!RagdollNetProtocol.ReadSnapshot(bytes, session.Match, snap) || (lastPose != 0 && !RagdollNetProtocol.Newer(snap.tick, lastPose)))
            {
                spare.Push(snap);
                return;
            }
            int lost = lastPose != 0 ? (int)Math.Min(30u, snap.tick - lastPose - 1) : 0;
            lastPose = snap.tick;
            snapshotsIn++;
            snapshotBytesLast = bytes.Length;
            hostSilence.Heard(Time.realtimeSinceStartup);
            delay.Arrived(Time.realtimeSinceStartupAsDouble * 1000d, snap.hostTimeMs, lost);
            // Round trip: my input's time as the host echoes it (the newest one its physics played, R306).
            uint nowMs = NowMs();
            double echoMs = -1d;
            for (int i = 0; i < snap.count; i++)
            {
                var pose = snap.At(i);
                if (pose.id != session.Self || pose.ack == 0) continue;
                int sample = RagdollWire.AckAge(nowMs, pose.ack);
                if (sample < 0 || sample >= 10000) continue;
                roundTripMs = roundTripMs <= 0f ? sample : Mathf.Lerp(roundTripMs, sample, 0.2f);
                echoMs = nowMs - (double)sample;
            }
            // R306 [0]: a press being timed is answered by the first snapshot echoing its input or a later one.
            presses.Arrived(nowMs, echoMs, snap.hostTimeMs);
            buffer.Add(snap);
        }

        // Not the host: the host's draw (course 01), each station's progress and look, the course's own state (course
        // 01's pads, a round's sections) and the race.
        void ApplyState(PawnRushNetState s)
        {
            if (match == null) return;
            match.ApplyBefore(s);
            int stations = Mathf.Min(s.StationCount, match.StationCount);
            for (int k = 0; k < stations; k++)
            {
                var station = match.Station(k);
                var game = station != null ? station.Game : null;
                if (game == null || station.GameIndex != s.Game[k]) continue;
                var block = new NetBlock(s.Block[k], 0, s.BlockLength[k]);
                game.ReadNet(ref block);
                game.ApplyRemote(s.Progress[k], s.Completed[k]);
            }
            match.ApplyAfter(s);
            if (finish != null) finish.ApplyRemote(s.Winner, s.Clock, s.FinisherSeat, s.FinisherTime, s.FinisherCount);
        }

        // A client keeps the course states until the pawns it draws reach their host time (R126).
        void QueueState(PawnRushNetState s)
        {
            var copy = statePool.Count > 0 ? statePool.Pop() : new PawnRushNetState();
            copy.CopyFrom(s);
            stateQueue.Add(copy);
            // Never more than a second's worth: the oldest are applied at once (a stalled playback).
            while (stateQueue.Count > 32) ApplyQueued(0);
        }

        void ApplyDueStates()
        {
            // Before the first snapshot there is no playback to wait for.
            while (stateQueue.Count > 0 && (playback <= 0d || stateQueue[0].HostTimeMs <= playback + 0.5d)) ApplyQueued(0);
        }

        void ApplyQueued(int index)
        {
            var s = stateQueue[index];
            stateQueue.RemoveAt(index);
            try { ApplyState(s); }
            catch (Exception e) { Debug.LogException(e); }
            statePool.Push(s);
        }

        // ------------------------------------------------------------------ the obstacle clock (R126)

        // The shared clock advanced with the physics steps: the runtime's shared clock reads real time, which
        // moves a whole frame at once, so several physics steps of a frame would see the same time and the
        // obstacles would move in jerks. Only the offset to the shared clock is measured, eased in a little each
        // frame (jumps on the first frame or a hitch).
        void SyncObstacleClock()
        {
            double target = sharedClock() - Time.fixedTimeAsDouble;
            double error = target - obstacleOffset;
            if (!obstacleSynced || Math.Abs(error) > 0.25)
            {
                obstacleOffset = target;
                obstacleSynced = true;
            }
            else obstacleOffset += Math.Max(-0.002, Math.Min(0.002, error));
        }

        double HostObstacleTime() => Time.fixedTimeAsDouble + obstacleOffset;

        // What the obstacles read: the host its own clock; a client the host's stamp at the moment it draws (until
        // the first snapshot, the shared clock).
        double MatchObstacleTime() => authority || !hostObstacles.HasSample ? HostObstacleTime() : hostObstacles.Now(Time.timeAsDouble);

        // The start bar sinks at the match's shared start plus its countdown, by the obstacle clock, on every PC.
        void ArmStartBar()
        {
            if (startBarSet || StartBar.Current == null || session.StartAt <= 0) return;
            startBarSet = true;
            StartBar.Current.ReleaseAtClock(session.StartAt + StartBar.Current.Countdown);
        }

        // R508: the round's sections count from the lobby's go once it is known (before the race: the mini-games are
        // built again from it), on every PC.
        void ArmMatchStart()
        {
            if (matchStartSet || match == null || session.StartAt <= 0) return;
            matchStartSet = true;
            match.StartAt(session.StartAt);
        }

        // R295: the match's shared start has passed, by the runtime's shared clock: from then on at the latest a client
        // counts its host's silence.
        bool Started() => session.StartAt > 0 && sharedClock != null && sharedClock() >= session.StartAt;

        /// <summary>The host's skills packet: the states at once (D-S4), the moments queued to play with the bodies.</summary>
        void ReceiveSkills(byte[] bytes)
        {
            if (!RagdollNetProtocol.ReadSkills(bytes, session.Match, skillIn)) return;
            bool newer = !skillsApplied || unchecked((int)(skillIn.HostTimeMs - lastSkillsTime)) > 0;
            if (newer)
            {
                skillsApplied = true;
                lastSkillsTime = skillIn.HostTimeMs;
                ApplyStates();
                ApplyWires();
            }
            inbox.Window(skillIn.FirstSeq);
            foreach (var e in skillIn.Events)
            {
                if (!inbox.Accept(e.Seq)) continue;
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
                pawn.ApplySkillNet(s, PawnOf);
                seenStates.Add(pawn);
            }
            foreach (var r in runners.Values)
                if (r.Pawn != null && r.Pawn.NetworkPuppet && !seenStates.Contains(r.Pawn)) r.Pawn.ClearSkillNet();
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
                    var skills = owner != null && owner.PawnRushSkills != null ? owner.PawnRushSkills
                               : PawnRushCourseSkills.Current != null ? PawnRushCourseSkills.Current.skills : null;
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

        /// <summary>The moments whose time the bodies drawn have reached (in step with the poses).</summary>
        void DispatchEvents()
        {
            if (eventQueue.Count == 0 || playback <= 0d) return;
            int n = 0;
            while (n < eventQueue.Count && eventQueue[n].TimeMs <= playback + 0.5d) n++;
            // Taken off the queue first: an effect that throws must not hold up the ones after it.
            eventBatch.Clear();
            for (int i = 0; i < n; i++) eventBatch.Add(eventQueue[i]);   // not GetRange: a new list every frame (R297)
            eventQueue.RemoveRange(0, n);
            foreach (var e in eventBatch)
            {
                if (playback - e.TimeMs > StaleEventMs || e.Mode != SkillWire.ModePawnRush) continue;
                try { Play(e); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        void Play(SkillWireEvent w)
        {
            var kind = (SkillFxKind)w.Kind;
            SkillTripwire wire = null;
            if (w.Source != 0) remoteWires.TryGetValue(w.Source, out wire);
            if (kind == SkillFxKind.BishopWire)
            {
                if (wire == null || wire.NetFxShown) return;
                wire.NetFxShown = true;
            }
            replaying = true;
            try
            {
                RagdollPawn.RaiseSkillFx(new SkillFxEvent
                {
                    kind = kind, by = PawnOf(w.By), target = PawnOf(w.Target), at = new Vector3(w.X, w.Y, w.Z),
                    dir = new Vector3(w.Dx, w.Dy, w.Dz), normal = new Vector3(w.Ux, w.Uy, w.Uz), count = w.Count, size = w.Size, source = wire,
                });
            }
            finally { replaying = false; }
        }

        // Not the host: the pawns drawn the measured playback delay behind the newest snapshot, blended between two;
        // far behind (the host stopped a moment), it jumps (R123).
        void Playback()
        {
            if (buffer.Count == 0) return;
            var newest = buffer[buffer.Count - 1];
            double target = newest.hostTimeMs - delay.Milliseconds;
            if (playback <= 0d) playback = target;
            playback = PlaybackDelay.Advance(playback, target, newest.hostTimeMs, Time.unscaledDeltaTime * 1000d, out double rate, out _);
            // R306 [6] (P3, a development build's F7): this PC's own pawn drawn up to the lost-snapshot margin sooner
            // (PlaybackDelay.LeadMs), never past the newest snapshot, and the obstacles at its time - the course it runs on
            // where the host had it for that pose; the others are drawn that much behind the obstacles. Off by default.
            ownLeadMs = ownLead ? delay.LeadMs : 0d;
            double own = Math.Min(playback + ownLeadMs, newest.hostTimeMs);
            Bracket(playback, out var from, out var to, out float t);
            Bracket(own, out var ownFrom, out var ownTo, out float ownT);
            // Obstacles on the same timeline as the pawns drawn this frame (with the lead, as this PC's own).
            hostObstacles.Sample(HostObstacleClock.Interpolate(ownFrom.obstacleTime, ownTo.obstacleTime, ownT), Time.timeAsDouble, rate);
            bool lead = ownLeadMs > 0d;
            for (int i = 0; i < to.count; i++)
                if (!lead || to.At(i).id != session.Self) Draw(to.At(i), from, t);
            if (lead)
                for (int i = 0; i < ownTo.count; i++)
                    if (ownTo.At(i).id == session.Self) { Draw(ownTo.At(i), ownFrom, ownT); break; }
            presses.Drawn(NowMs(), own);
            // 32: a second's 60 snapshots trimmed at the playback time anyway; the cap only bounds a stalled playback.
            while (buffer.Count > 2 && (buffer[1].hostTimeMs < playback || buffer.Count > 32))
            {
                spare.Push(buffer[0]);
                buffer.RemoveAt(0);
            }
        }

        // The two buffered snapshots around host time `time`, and how far between them it is.
        void Bracket(double time, out RagdollSnapshot from, out RagdollSnapshot to, out float t)
        {
            from = buffer[0];
            to = from;
            foreach (var snapshot in buffer)
            {
                to = snapshot;
                if (snapshot.hostTimeMs >= time) break;
                from = snapshot;
            }
            t = Mathf.Clamp01((float)((time - from.hostTimeMs) / Math.Max(1, (double)to.hostTimeMs - from.hostTimeMs)));
        }

        // One pawn drawn: its pose in the later snapshot, blended from the earlier one's (a snapped pose as it is).
        void Draw(RagdollPose b, RagdollSnapshot from, float t)
        {
            if (!runners.TryGetValue(b.id, out var r) || r.Pawn == null) return;
            RagdollPose a = b;
            for (int j = 0; j < from.count; j++) if (from.At(j).id == b.id) { a = from.At(j); break; }
            if (b.snap) r.Pawn.ApplyNetworkPose(b);
            else
            {
                RagdollPose.Blend(a, b, t, blend);
                r.Pawn.ApplyNetworkPose(blend);
            }
        }
    }
}
