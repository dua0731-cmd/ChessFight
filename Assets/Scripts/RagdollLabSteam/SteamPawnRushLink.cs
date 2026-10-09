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
    /// Not in this first pass: bots only stand at the start, planks being carried show on the host only,
    /// a new host after a host change starts everyone's checkpoints over, and the finish has no result.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class SteamPawnRushLink : MonoBehaviour
    {
        const int Channel = 34;                 // 31 capsule motion, 32 lab, 33 sword fight
        const float SnapshotInterval = 1f / 30f, StateInterval = .1f, InputInterval = 1f / 60f;
        const float InputTimeout = .35f, SilenceTimeout = 12f, PlaybackDelayMs = 100f, FallLimit = -40f;

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
            // Host only: the owner's latest input.
            public RagdollNetInput Input;
            public uint Sequence;
            public float InputAt = -1f;
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

        readonly Dictionary<ulong, Runner> runners = new Dictionary<ulong, Runner>();
        readonly Dictionary<ICharacterDriver, Runner> byDriver = new Dictionary<ICharacterDriver, Runner>();
        readonly List<ulong> gone = new List<ulong>();
        readonly IntPtr[] incoming = new IntPtr[64];
        readonly Dictionary<ulong, RagdollPose> poses = new Dictionary<ulong, RagdollPose>();
        readonly Dictionary<ulong, int> snaps = new Dictionary<ulong, int>();
        readonly List<RagdollPose> outgoing = new List<RagdollPose>();
        readonly List<RagdollSnapshot> buffer = new List<RagdollSnapshot>();
        readonly Stack<RagdollSnapshot> spare = new Stack<RagdollSnapshot>();
        readonly RagdollPose blend = new RagdollPose();
        readonly PawnRushNetState state = new PawnRushNetState(), received = new PawnRushNetState();
        RagdollNetInput pending;
        uint tick, stateTick, inputSequence, lastPose, lastState;
        float sentAt, stateAt, inputAt, receivedAt;
        double playback;

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
            Checkpoint.Reached += OnCheckpoint;
            KillVolume.Entered += OnKill;
            FallDistanceRespawn.Fell += OnFell;
            WaterZone.Entered += OnWater;
            TeamZone.Intruded += OnIntruded;
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
            Course01Playtest.NetworkDriver = null;
            MiniGameBase.Remote = false;
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
            if (id == session.Self)
            {
                Course01Playtest.NetworkDriver = driver;
                Course01Playtest.NetworkTeam = team;
                if (orbit != null) orbit.Follow(driver.FollowTarget, pawn.transform, rotation * Vector3.forward);
            }
        }

        void Remove(ulong id)
        {
            if (!runners.TryGetValue(id, out var r)) return;
            runners.Remove(id);
            byDriver.Remove(r.Driver);
            poses.Remove(id);
            snaps.Remove(id);
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

        // ------------------------------------------------------------------ host change

        void SetAuthority(bool host)
        {
            authority = host;
            MiniGameBase.Remote = !host;
            buffer.Clear();
            foreach (var r in runners.Values)
            {
                r.InputAt = -1f;
                r.Sequence = 0;
                r.RespawnTime = -1f;
                if (r.Pawn != null) r.Pawn.SetNetworkPuppet(!host);
            }
            receivedAt = Time.realtimeSinceStartup;
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (!session.Started || session.Match == 0) return;
            if (session.IsHost != authority) SetAuthority(session.IsHost);
            SyncRoster();
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
                var fresh = RagdollNetInput.From(local);
                pending.move = fresh.move;
                pending.aim = fresh.aim;
                pending.sprint = fresh.sprint;
                pending.grab = fresh.grab;
                pending.shoveHeld = fresh.shoveHeld;
                pending.interact = fresh.interact;
                pending.jump |= fresh.jump;
                pending.shove |= fresh.shove;
                pending.ability |= fresh.ability;
                pending.ability2 |= fresh.ability2;
                // Reliable on this channel, so a short click is never lost; held motion times out on the host.
                if (now - inputAt >= InputInterval)
                {
                    inputAt = now;
                    Send(session.Host, RagdollNetProtocol.Input(session.Match, ++inputSequence, NowMs(), pending), true);
                    pending.jump = pending.shove = pending.ability = pending.ability2 = false;
                }
                Playback();
                if (now - receivedAt > SilenceTimeout) session.Abort("폰 러쉬 방장 연결이 끊어졌습니다.");
            }
        }

        // Host: every other player's latest input, or standing still once it is stale. Bots stand.
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
                r.Pawn.SetInput(r.Input.ToPawnInput());
                r.Input.jump = r.Input.shove = r.Input.ability = r.Input.ability2 = false;
            }
        }

        // The local player's keys against the orbit camera, as the offline playtest reads them. Read every
        // frame (edges are not kept for later) and dropped while the loading screen or the chat holds input.
        PawnInput ReadLocalInput()
        {
            var intent = controls.Read();
            if (runtime.InputBlocked || !Application.isFocused || (orbit != null && orbit.FreeFly)) return default;
            Vector3 move = orbit != null ? orbit.FlatRight * intent.Move.x + orbit.FlatForward * intent.Move.y
                                         : new Vector3(intent.Move.x, 0f, intent.Move.y);
            return new PawnInput
            {
                move = Vector3.ClampMagnitude(move, 1f),
                aim = orbit != null ? orbit.AimForward : Vector3.forward,
                jump = intent.Jump, shove = intent.Shove, shoveHeld = intent.ShoveHeld, grab = intent.Grab,
                sprint = intent.Sprint, ability = intent.Ability, ability2 = intent.Ability2, interact = intent.Interact,
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

        // ------------------------------------------------------------------ host: sending

        static uint NowMs() => (uint)(Time.realtimeSinceStartupAsDouble * 1000);

        void SendSnapshot()
        {
            sentAt = Time.realtimeSinceStartup;
            ++tick;
            outgoing.Clear();
            foreach (var r in runners.Values)
            {
                if (r.Pawn == null) continue;
                if (!poses.TryGetValue(r.Id, out var pose)) poses[r.Id] = pose = new RagdollPose();
                r.Pawn.CaptureNetworkPose(pose);
                pose.id = r.Id;
                if (r.Pawn.NetworkSnap) { snaps[r.Id] = 4; r.Pawn.NetworkSnap = false; }
                snaps.TryGetValue(r.Id, out int n);
                pose.snap = n > 0;
                snaps[r.Id] = Mathf.Max(0, n - 1);
                outgoing.Add(pose);
            }
            byte[] bytes = RagdollNetProtocol.Snapshot(session.Match, tick, NowMs(), outgoing);
            foreach (var id in session.Roster.Keys) Send(id, bytes, false);
        }

        void SendState()
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
            byte[] bytes = state.Write(session.Match);
            foreach (var id in session.Roster.Keys) Send(id, bytes, false);
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
                    if (authority)
                    {
                        if (!runners.TryGetValue(id, out var r)) continue;
                        if (!RagdollNetProtocol.ReadInput(bytes, session.Match, out uint sequence, out _, out var input)) continue;
                        if (r.Sequence != 0 && !RagdollNetProtocol.Newer(sequence, r.Sequence)) continue;
                        // Edges not yet used by a physics step stay set.
                        input.jump |= r.Input.jump;
                        input.shove |= r.Input.shove;
                        input.ability |= r.Input.ability;
                        input.ability2 |= r.Input.ability2;
                        r.Input = input;
                        r.Sequence = sequence;
                        r.InputAt = Time.realtimeSinceStartup;
                    }
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
                        var snap = spare.Count > 0 ? spare.Pop() : new RagdollSnapshot();
                        if (!RagdollNetProtocol.ReadSnapshot(bytes, session.Match, snap)
                            || (lastPose != 0 && !RagdollNetProtocol.Newer(snap.tick, lastPose)))
                        {
                            spare.Push(snap);
                            continue;
                        }
                        lastPose = snap.tick;
                        receivedAt = Time.realtimeSinceStartup;
                        buffer.Add(snap);
                    }
                }
                finally
                {
                    SteamNetworkingMessage_t.Release(incoming[i]);
                    incoming[i] = IntPtr.Zero;
                }
            }
        }

        // Not the host: the host's round (its two games) and each station's progress and look.
        void ApplyState(PawnRushNetState s)
        {
            var picker = course != null ? course.Missions : null;
            if (picker == null) return;
            if (s.First >= 0 && s.Second >= 0 && (picker.First != s.First || picker.Second != s.Second)) picker.Install(s.First, s.Second);
            for (int k = 0; k < PawnRushNetState.Stations; k++)
                picker.Station(k / 2 + 1, k % 2)?.Game?.ApplyRemote(s.Progress[k], s.Completed[k], s.Bits[k]);
        }

        // Not the host: the pawns drawn PlaybackDelayMs behind the newest snapshot, blended between two.
        void Playback()
        {
            if (buffer.Count == 0) return;
            var newest = buffer[buffer.Count - 1];
            double target = newest.hostTimeMs - PlaybackDelayMs;
            if (playback <= 0 || Math.Abs(playback - target) > 500) playback = target;
            playback = Math.Min(newest.hostTimeMs, playback + Time.unscaledDeltaTime * 1000 * (playback < target - 50 ? 1.1 : playback > target + 50 ? .9 : 1));
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
