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
    /// Not yet: bots only stand at the start, planks being carried show on the host only, a new host after a
    /// host change starts everyone's checkpoints over.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class SteamPawnRushLink : MonoBehaviour
    {
        const int Channel = 34;                 // 31 capsule motion, 32 lab, 33 sword fight
        const float SnapshotInterval = 1f / 30f, StateInterval = .1f, InputInterval = 1f / 60f;
        const float InputTimeout = .35f, SilenceTimeout = 12f, FallLimit = -40f;
        /// <summary>A moment played this much later than the poses it belongs to is not drawn any more (ms).</summary>
        const double StaleEventMs = 1000;

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
            var course = FindFirstObjectByType<PawnRushCourse>();
            var spawner = FindFirstObjectByType<PlaytestSpawner>(FindObjectsInactive.Include);
            if (course == null || spawner == null || FindFirstObjectByType<SteamPawnRushLink>() != null) return;
            var go = new GameObject("ChessFight Pawn Rush Net");
            SceneManager.MoveGameObjectToScene(go, course.gameObject.scene);   // gone with the match scene
            var link = go.AddComponent<SteamPawnRushLink>();
            link.course = course;
            link.spawner = spawner;
        }

        sealed class Runner
        {
            public ulong Id;
            public int Team, Slot;
            public bool Bot;
            public RagdollPawn Pawn;
            public RagdollDriver Driver;
            // Host only: where it comes back and when.
            public int Checkpoint = int.MinValue;
            public Vector3 RespawnAt;
            public Quaternion RespawnRotation;
            public float RespawnTime = -1f;
            // Host only: the owner's latest input, the presses it has counted and those waiting for a physics step.
            public RagdollLabInput Input;
            public uint Sequence, ClientTimeMs;
            public float InputAt = -1f;
            public bool Synced, AckSet, InteractLatch;
            public byte Jumps, Shoves, Abilities, Abilities2, Skills;
            public int Jump, Shove, Ability, Ability2, Skill;
            public ushort EventAck;
        }

        PawnRushCourse course;
        PlaytestSpawner spawner;
        NetworkRuntime runtime;
        SteamSession session;
        RagdollPawn prefab;
        OrbitCamera orbit;
        IMoveInputSource controls;
        Material white, black;
        bool authority;
        Callback<SteamNetworkingMessagesSessionRequest_t> requests;
        CrownFinish finish;

        readonly Dictionary<ulong, Runner> runners = new Dictionary<ulong, Runner>();
        readonly Dictionary<ICharacterDriver, Runner> byDriver = new Dictionary<ICharacterDriver, Runner>();
        readonly Dictionary<RagdollPawn, Runner> byPawn = new Dictionary<RagdollPawn, Runner>();
        readonly List<ulong> gone = new List<ulong>();
        readonly IntPtr[] incoming = new IntPtr[64];
        readonly Dictionary<ulong, RagdollPose> poses = new Dictionary<ulong, RagdollPose>();
        readonly Dictionary<ulong, int> snaps = new Dictionary<ulong, int>();
        readonly List<RagdollPose> outgoing = new List<RagdollPose>();
        readonly List<RagdollSnapshot> buffer = new List<RagdollSnapshot>();
        readonly Stack<RagdollSnapshot> spare = new Stack<RagdollSnapshot>();
        readonly RagdollPose blend = new RagdollPose();
        readonly PawnRushNetState state = new PawnRushNetState(), received = new PawnRushNetState();
        uint tick, stateTick, inputSequence, lastPose, lastState;
        float sentAt, stateAt, inputAt, receivedAt;
        double playback;
        readonly PlaybackDelay delay = new PlaybackDelay();
        float roundTripMs;

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

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            runtime = NetworkRuntime.Instance;
            session = runtime.Session;
            receivedAt = Time.realtimeSinceStartup;
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
            MiniGameBase.Remote = !authority;
            finish = course != null ? course.Finish : null;
            if (finish != null)
            {
                finish.Authority = authority;
                finish.Decided += OnDecided;
            }
            if (PawnRushCourseSkills.Current != null) PawnRushCourseSkills.Current.Online = true;
            Checkpoint.Reached += OnCheckpoint;
            KillVolume.Entered += OnKill;
            FallDistanceRespawn.Fell += OnFell;
            WaterZone.Entered += OnWater;
            TeamZone.Intruded += OnIntruded;
            RagdollPawn.SkillFx += OnPawnRushFx;
            SyncRoster();
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
            MiniGameBase.Remote = false;
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

        void SetAuthority(bool host)
        {
            authority = host;
            MiniGameBase.Remote = !host;
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
                r.InputAt = -1f;
                r.Sequence = 0;
                r.RespawnTime = -1f;
                r.Synced = r.AckSet = false;
                r.Jump = r.Shove = r.Ability = r.Ability2 = r.Skill = 0;
            }
            pending = default;
            eventLog.Clear();
            inbox.Clear();
            eventQueue.Clear();
            skillsApplied = false;
            playback = 0d;
            delay.Reset();
            receivedAt = Time.realtimeSinceStartup;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (!session.Started || session.Match == 0) return;
            if (session.IsHost != authority) SetAuthority(session.IsHost);
            SyncRoster();
            // The match sheet's piece icons (VoiceHud, R131): who has been promoted, as this PC draws them.
            foreach (var r in runners.Values) MatchPieces.Set(r.Id, r.Pawn != null ? r.Pawn.Piece : PieceKind.Pawn);
            // A client's skill timers run before this frame's packets overwrite them.
            if (!authority)
                foreach (var r in runners.Values)
                    if (r.Pawn != null && r.Pawn.NetworkPuppet) r.Pawn.AdvanceSkillView(Time.unscaledDeltaTime);
            Receive();
            var local = ReadLocalInput();
            float now = Time.realtimeSinceStartup;
            if (authority)
            {
                if (runners.TryGetValue(session.Self, out var self)) self.Pawn.SetInput(local);
                foreach (var r in runners.Values) CheckRespawn(r);
                if (now - sentAt >= SnapshotInterval) SendSnapshot();
                if (now - stateAt >= StateInterval) SendState();
            }
            else
            {
                ClientInput(local, now);
                Playback();
                DispatchEvents();
                if (now - receivedAt > SilenceTimeout && !ended) session.Abort("폰 러쉬 방장 연결이 끊어졌습니다.");
            }
        }

        // Host: every other player's latest input, the presses one per physics step, or standing still once it is
        // stale. Bots stand.
        void FixedUpdate()
        {
            if (!authority) return;
            float now = Time.realtimeSinceStartup;
            foreach (var r in runners.Values)
            {
                if (r.Id == session.Self) continue;
                if (r.Bot || r.InputAt < 0f || now - r.InputAt > InputTimeout)
                {
                    r.Pawn.SetInput(default);
                    continue;
                }
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

        // The local player's keys against the orbit camera, as the offline playtest reads them. Read every
        // frame (edges are not kept for later) and dropped while the loading screen or the chat holds input.
        // F (the use key) is the skill key on this course (R125).
        PawnInput ReadLocalInput()
        {
            var intent = controls.Read();
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

        // ------------------------------------------------------------------ host: checkpoints and falls

        bool Find(ICharacterDriver who, out Runner r)
        {
            r = null;
            return authority && who != null && byDriver.TryGetValue(who, out r);
        }

        void OnCheckpoint(ICharacterDriver who, Checkpoint checkpoint)
        {
            if (!Find(who, out var r) || checkpoint.Order <= r.Checkpoint) return;
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
            SendState(true);
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
                // The newest input time heard from that player, for its round trip (0 = none yet).
                pose.ack = r.InputAt >= 0f ? r.ClientTimeMs : 0u;
                if (r.Pawn.NetworkSnap) { snaps[r.Id] = 4; r.Pawn.NetworkSnap = false; }
                snaps.TryGetValue(r.Id, out int n);
                pose.snap = n > 0;
                snaps[r.Id] = Mathf.Max(0, n - 1);
                outgoing.Add(pose);
            }
            byte[] bytes = RagdollNetProtocol.Snapshot(session.Match, tick, stamp, outgoing);
            foreach (var id in session.Roster.Keys) Send(id, bytes, false);
            SendSkills(stamp);
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

        void SendState(bool reliable = false)
        {
            stateAt = Time.realtimeSinceStartup;
            var picker = course != null ? course.Missions : null;
            if (picker == null) return;
            state.Tick = ++stateTick;
            state.First = (sbyte)picker.First;
            state.Second = (sbyte)picker.Second;
            for (int k = 0; k < PawnRushNetState.Stations; k++)
            {
                var game = picker.Station(k / 2 + 1, k % 2)?.Game;
                state.Progress[k] = game != null ? game.Progress01 : 0f;
                state.Completed[k] = game != null && game.Completed;
                state.Bits[k] = game != null ? game.StateBits : 0UL;
            }
            state.PadFirst = (byte)picker.PadFirst;
            state.PadSecond = (byte)picker.PadSecond;
            state.PadsUsed = picker.PadsUsed;
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

        void Send(ulong id, byte[] bytes, bool reliable)
        {
            if (id == session.Self || !session.IsPeer(id) || BotIdentity.IsBot(id)) return;
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
        }

        // ------------------------------------------------------------------ receiving

        void Receive()
        {
            int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, incoming, incoming.Length);
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var msg = SteamNetworkingMessage_t.FromIntPtr(incoming[i]);
                    ulong id = msg.m_identityPeer.GetSteamID64();
                    if (!session.IsPeer(id) || msg.m_cbSize <= 0 || msg.m_cbSize > RagdollNetProtocol.MaxBytes) continue;
                    byte[] bytes = new byte[msg.m_cbSize];
                    Marshal.Copy(msg.m_pData, bytes, 0, bytes.Length);
                    if (authority) ReceiveInput(id, bytes);
                    else if (id == session.Host)
                    {
                        if (PawnRushNetState.Read(bytes, session.Match, received))
                        {
                            if (lastState != 0 && !RagdollNetProtocol.Newer(received.Tick, lastState)) continue;
                            lastState = received.Tick;
                            receivedAt = Time.realtimeSinceStartup;
                            ApplyState(received);
                            continue;
                        }
                        byte type = RagdollNetProtocol.TypeOf(bytes);
                        if (type == RagdollNetProtocol.TypeSkills) ReceiveSkills(bytes);
                        else if (type == RagdollNetProtocol.TypeSnapshot) ReceiveSnapshot(bytes);
                    }
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(incoming[i]);
                    incoming[i] = IntPtr.Zero;
                }
            }
        }

        // Host: a player's input. The presses are counts: what is new since the last one heard waits for the physics.
        void ReceiveInput(ulong id, byte[] bytes)
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
            if (!r.AckSet || SkillWire.Newer(fresh.eventAck, r.EventAck))
            {
                r.EventAck = fresh.eventAck;
                r.AckSet = true;
            }
        }

        // Not the host: count my presses, draw my own press and aim at once (D-S3), send 60 times a second.
        void ClientInput(PawnInput local, float now)
        {
            if (local.jump) myJumps = PressCount.Add(myJumps);
            if (local.shove) myShoves = PressCount.Add(myShoves);
            if (local.ability) myAbilities = PressCount.Add(myAbilities);
            if (local.ability2) myAbilities2 = PressCount.Add(myAbilities2);
            if (local.skill) mySkills = PressCount.Add(mySkills);
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
            if (now - inputAt < InputInterval) return;
            inputAt = now;
            pending.jumps = myJumps;
            pending.shoves = myShoves;
            pending.abilities = myAbilities;
            pending.abilities2 = myAbilities2;
            pending.skills = mySkills;
            pending.eventAck = inbox.Started ? inbox.Acked : (ushort)0;
            pending.roundTripMs = (ushort)Mathf.Clamp(Mathf.RoundToInt(roundTripMs), 0, ushort.MaxValue);
            pending.delayMs = (byte)Mathf.Clamp((int)Math.Round(delay.Milliseconds), 0, 255);
            pending.lossPercent = (byte)Mathf.Clamp((int)Math.Round(delay.LossShare * 100d), 0, 100);
            // Unreliable like the lab's: a lost packet only delays a press (the counts carry it), never drops it.
            Send(session.Host, RagdollNetProtocol.LabInput(session.Match, ++inputSequence, NowMs(), pending), false);
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
            receivedAt = Time.realtimeSinceStartup;
            delay.Arrived(Time.realtimeSinceStartupAsDouble * 1000d, snap.hostTimeMs, lost);
            // Round trip: my input's time as the host echoes it.
            for (int i = 0; i < snap.count; i++)
            {
                var pose = snap.At(i);
                if (pose.id != session.Self || pose.ack == 0) continue;
                int sample = unchecked((int)(NowMs() - pose.ack));
                if (sample >= 0 && sample < 10000) roundTripMs = roundTripMs <= 0f ? sample : Mathf.Lerp(roundTripMs, sample, 0.2f);
            }
            buffer.Add(snap);
        }

        // Not the host: the host's round (its two games), each station's progress and look, the pads and the race.
        void ApplyState(PawnRushNetState s)
        {
            var picker = course != null ? course.Missions : null;
            if (picker == null) return;
            if (s.First >= 0 && s.Second >= 0 && (picker.First != s.First || picker.Second != s.Second)) picker.Install(s.First, s.Second);
            for (int k = 0; k < PawnRushNetState.Stations; k++)
                picker.Station(k / 2 + 1, k % 2)?.Game?.ApplyRemote(s.Progress[k], s.Completed[k], s.Bits[k]);
            picker.ApplyPads((PieceKind)s.PadFirst, (PieceKind)s.PadSecond, s.PadsUsed);
            if (finish != null) finish.ApplyRemote(s.Winner, s.Clock, s.FinisherSeat, s.FinisherTime, s.FinisherCount);
        }

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
            eventBatch.AddRange(eventQueue.GetRange(0, n));
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
            playback = PlaybackDelay.Advance(playback, target, newest.hostTimeMs, Time.unscaledDeltaTime * 1000d, out _, out _);
            var from = buffer[0];
            var to = from;
            foreach (var snapshot in buffer)
            {
                to = snapshot;
                if (snapshot.hostTimeMs >= playback) break;
                from = snapshot;
            }
            float t = Mathf.Clamp01((float)((playback - from.hostTimeMs) / Math.Max(1, (double)to.hostTimeMs - from.hostTimeMs)));
            for (int i = 0; i < to.count; i++)
            {
                var b = to.At(i);
                if (!runners.TryGetValue(b.id, out var r) || r.Pawn == null) continue;
                RagdollPose a = b;
                for (int j = 0; j < from.count; j++) if (from.At(j).id == b.id) { a = from.At(j); break; }
                if (b.snap) r.Pawn.ApplyNetworkPose(b);
                else
                {
                    RagdollPose.Blend(a, b, t, blend);
                    r.Pawn.ApplyNetworkPose(blend);
                }
            }
            while (buffer.Count > 2 && (buffer[1].hostTimeMs < playback || buffer.Count > 16))
            {
                spare.Push(buffer[0]);
                buffer.RemoveAt(0);
            }
        }
    }
}
