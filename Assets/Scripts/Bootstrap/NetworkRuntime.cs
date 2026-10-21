using System;
using System.Collections.Generic;
using System.Linq;
using ChessFight.Gameplay;
using ChessFight.Network;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    // The single owner of the Steam session, created once and kept across scene
    // loads. Before this, the lobby scene owned the session, so loading any other
    // scene would have shut Steam down and dropped the party and the match.
    //
    // It also runs the scene flow:
    //   Intro -> Lobby            the title screen, on any key
    //   Lobby -> mode scene       when the match starts (every client sees phase=playing);
    //                             the scene comes from the match room's game mode
    //   mode scene -> Lobby       when the match ends or the player leaves
    //
    // Scene objects never reference this Steam-gated assembly. Each scene is
    // given its controller here, when it loads.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class NetworkRuntime : MonoBehaviour
    {
        public static NetworkRuntime Instance { get; private set; }

        public SteamSession Session { get; private set; }
        public SteamMotion Motion { get; private set; }
        // Voice chat (Docs/Architecture/UI.md "음성 채팅", R131): Steam's microphone and the speakers.
        public SteamVoice Voice { get; private set; }
        public IMoveInputSource Controls { get; private set; }

        // The active scene says whether local input may move the pawn: false while
        // typing a lobby number, or with the window in the background.
        public Func<bool> MovementGate { get; set; }

        // True while nobody may move: the loading screen before the shared start, or the chat open.
        // A scene that reads its own input (a match it simulates itself) checks this.
        public bool InputBlocked => loading || loader == null || loader.Blocking || ChatBox.KeysHeld || GameMenu.IsOpen;

        bool inMatchScene, loading, customSimulation, escapeLeaves;
        // The scene the running match loaded, from its game mode.
        string matchScene = "";
        readonly SharedClock clock = new SharedClock();
        // Lobby -> match behind the loading screen, and the shared start.
        MatchLoader loader;
        // The chat, one panel for every scene (Docs/Architecture/UI.md "채팅").
        ChatBox chat;
        // The voice chat's sound and its match HUD (talkers top left, the P sheet).
        VoicePlayback speakers;
        VoiceHud voiceHud;
        readonly List<(ulong id, VoiceChannel channel)> talkers = new List<(ulong id, VoiceChannel channel)>();
        readonly SheetData sheet = new SheetData();

        // Only scenes that belong to the online flow start Steam. A course or test
        // scene opened on its own stays offline and runs its local playtest.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string first = SceneManager.GetActiveScene().name;
            if (first == SceneNames.Intro || first == SceneNames.Lobby) Ensure();
            // A course played offline goes on to its result scene and then the lobby (R125): the lobby
            // loaded later starts the session then, as if the game had started there.
            SceneManager.sceneLoaded -= LateStart;
            SceneManager.sceneLoaded += LateStart;
        }

        static void LateStart(Scene scene, LoadSceneMode mode)
        {
            if (Instance == null && (scene.name == SceneNames.Intro || scene.name == SceneNames.Lobby)) Ensure();
        }

        // Player Settings > Version, plus "-dev" for the Editor and development builds.
        // Raise the version for every build handed to testers, so stale copies
        // are told to update instead of desyncing.
        public static string BuildTag => Application.version + (Debug.isDebugBuild ? "-dev" : "");

        public static NetworkRuntime Ensure()
        {
            if (Instance != null) return Instance;
            var host = new GameObject("ChessFight Network Runtime");
            DontDestroyOnLoad(host);
            return host.AddComponent<NetworkRuntime>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.runInBackground = true;

            Controls = MoveInputSources.Create();
            Controls.Enable();
            // Development and release builds never meet: the build tag differs,
            // and only development builds may take bots into public matches.
            Session = new SteamSession(BuildTag, Debug.isDebugBuild);
            // The name chosen on the name screen (R84), shown and published by the session.
            Session.LocalName = PlayerProfile.Name;
            PlayerProfile.Changed += OnNameChanged;
            Session.Initialize();
            if (Session.Online) Motion = new SteamMotion(Session);
            // Measures this PC in the background so the host role can go to the
            // machine that simulates fastest (Docs/Network/HOST.md).
            HostFitnessProbe.Start();
            // The settings window's top bar shows who is playing (R77).
            SettingsWindow.Identity = () => Session.Online ? Session.Name(Session.Self) : null;
            loader = new MatchLoader(this, clock.Now);
            // A chat that fails to build must not take the session down with it.
            // On an object of its own: a UI document on this object would make the
            // loading screen's document (a child of this object) its child, and
            // Unity then refuses the loading screen's own panel settings.
            try
            {
                var chatHost = new GameObject("Chat");
                chatHost.transform.SetParent(transform, false);
                chat = chatHost.AddComponent<ChatBox>();
                chat.Build();
                chat.Connect(Session.Chat, Session.CanChat, Session.Say);
            }
            catch (Exception e) { Debug.LogException(e); }
            // The voice chat (R131), built the same way: speakers and its HUD on objects of their own,
            // and a failure here leaves the session and the chat running.
            try
            {
                var voiceHost = new GameObject("Voice");
                voiceHost.transform.SetParent(transform, false);
                speakers = voiceHost.AddComponent<VoicePlayback>();
                var hudHost = new GameObject("Voice HUD");
                hudHost.transform.SetParent(transform, false);
                voiceHud = hudHost.AddComponent<VoiceHud>();
                voiceHud.Build();
                voiceHud.NameOf = id => Session.Name(id);
                voiceHud.Sheet = FillSheet;
                StartVoice();
            }
            catch (Exception e) { Debug.LogException(e); }

            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach(SceneManager.GetActiveScene());
        }

        void OnNameChanged()
        {
            if (Session != null) Session.LocalName = PlayerProfile.Name;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            loading = false;
            Attach(scene);
            loader?.SceneLoaded(scene.name);
        }

        void Attach(Scene scene)
        {
            customSimulation = escapeLeaves = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                var config = root.GetComponentInChildren<GameSceneConfig>(true);
                if (config == null || !config.customMatchSimulation) continue;
                customSimulation = true;
                escapeLeaves |= config.escapeLeavesMatch;
            }
            switch (scene.name)
            {
                case SceneNames.Intro: Host(scene).AddComponent<IntroController>(); break;
                case SceneNames.Lobby: Host(scene).AddComponent<LobbyBootstrap>(); break;
            }
            // A mode scene opened as the match scene shows the networked pawns
            // instead of its offline playtest.
            if (inMatchScene && scene.name == matchScene && !customSimulation) Host(scene).AddComponent<MatchSceneView>();
            // A match scene without a menu of its own: Esc is the in-game menu, and leaving it leaves the match.
            if (inMatchScene && scene.name == matchScene && escapeLeaves) GameMenu.Use(Host(scene), () => Session.Cancel(), true);
        }

        // The scene's GameSceneConfig object when it has one, so the controller
        // sits next to the Inspector wiring; otherwise a fresh object in that scene.
        static GameObject Host(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var config = root.GetComponentInChildren<GameSceneConfig>(true);
                if (config != null) return config.gameObject;
            }
            var host = new GameObject("ChessFight Scene Controller");
            SceneManager.MoveGameObjectToScene(host, scene);
            return host;
        }

        void Update()
        {
            // What the host election reads: this machine's score once the probe has
            // finished, and every frame's time (a slow host hands the match on).
            if (Session.LocalFitness == 0) Session.LocalFitness = HostFitnessProbe.BaseScore();
            Session.ReportFrame(Time.unscaledDeltaTime * 1000f);
            Session.Tick();
            // A successful Steam retry needs the movement layer built after the fact.
            if (Motion == null && Session.Online) Motion = new SteamMotion(Session);
            if (Voice == null && Session.Online) StartVoice();
            try { UpdateVoice(); }
            catch (Exception e) { Debug.LogException(e); }

            // Read every frame so an edge-triggered jump is never buffered across
            // the frames where movement is suppressed.
            var intent = Controls.Read();
            if (MovementGate != null && !MovementGate()) intent = default;
            // Nobody moves before the shared start behind the loading screen, nor
            // while typing a chat line.
            if (loader.Blocking || ChatBox.KeysHeld || GameMenu.IsOpen) intent = default;
            if (!customSimulation)
                Motion?.Update(Mathf.Clamp(intent.Move.x, -1f, 1f), Mathf.Clamp(intent.Move.y, -1f, 1f), intent.Jump);
            // R148: a scene that runs its own match without a menu (Pawn Rush course 01) no longer leaves on Esc:
            // Esc opens the in-game menu (GameMenu, installed in Attach), whose "게임 나가기" asks first.

            // Development aid for the network review: F8 cycles extra delay and
            // loss on this machine, so one tester can play on a bad connection.
            if (Debug.isDebugBuild && Motion != null && LegacyKeys.Down(KeyCode.F8))
            {
                var presets = LinkProfile.Presets;
                int next = (Array.FindIndex(presets, p => p.RoundTripMs == Motion.Simulation.RoundTripMs && p.LossPercent == Motion.Simulation.LossPercent) + 1) % presets.Length;
                Motion.Simulation = presets[next];
                Debug.Log("[ChessFight] 지연 시뮬레이터: " + Motion.Simulation);
            }

            FollowMatch();
            // A broken loading screen falls back to loading without one, never to
            // a player stuck behind it.
            try { loader.Update(); }
            catch (Exception e) { Debug.LogException(e); loader.Cancel(); }

            // The chat: bottom left in the lobby, bottom right in any match scene
            // (its own simulation or not), hidden on the title and under the
            // entrance screen (matchmaking and loading).
            if (chat != null)
                chat.Where = loading || loader.Covering ? ChatBox.Layout.Hidden
                    : inMatchScene ? ChatBox.Layout.Match
                    : SceneManager.GetActiveScene().name == SceneNames.Lobby ? ChatBox.Layout.Lobby
                    : ChatBox.Layout.Hidden;
            // The voice HUD the same way: talkers and the sheet in a match; the lobby shows voice on
            // its nameplates. Sword Fight keeps its team badge in the corner, so the list starts below it.
            if (voiceHud != null)
            {
                string active = SceneManager.GetActiveScene().name;
                voiceHud.Where = loading || loader.Covering ? VoiceHud.Layout.Hidden
                    : inMatchScene ? VoiceHud.Layout.Match
                    : active == SceneNames.Lobby ? VoiceHud.Layout.Lobby
                    : VoiceHud.Layout.Hidden;
                voiceHud.TalkersTop = active == SceneNames.SwordFight ? 78f : 18f;
            }
        }

        // ---------- voice chat (R131) ----------

        ulong voiceMatch;

        void StartVoice()
        {
            if (Voice != null || !Session.Online) return;
            Voice = new SteamVoice(Session) { Muted = VoiceBoard.IsMuted };
            Voice.Sink = (id, pcm, bytes, gain) => { if (speakers != null) speakers.Push(id, pcm, bytes, gain); };
            if (speakers != null) Voice.SampleRate = speakers.DecodeRate;
            SettingsWindow.VoiceLevel = () => Voice != null ? Voice.MicLevel : 0f;
            SettingsWindow.VoiceProblem = () => Voice != null ? Voice.Problem : "";
        }

        // Settings and keys into the voice, the voice into the board the HUDs read.
        void UpdateVoice()
        {
            if (Voice != null)
            {
                Voice.Enabled = GameSettings.VoiceOn;
                Voice.ListenParty = GameSettings.VoicePartyAuto;
                Voice.ListenTeam = GameSettings.VoiceTeamAuto;
                Voice.PushToTalk = GameSettings.PushToTalk;
                // Not while typing a chat line or a name, nor behind the settings window.
                Voice.TalkHeld = Application.isFocused && !ChatBox.KeysHeld && !NameScreen.Showing && !SettingsWindow.IsOpen &&
                                 GameSettings.Held(GameKey.Talk);
                Voice.Threshold = GameSettings.VoiceThreshold;
                Voice.GainPercent = Mathf.RoundToInt(GameSettings.MicGain * 100f);
                // The sheet's pick lasts for one match; the next one starts from the rule again.
                if (Session.Match != voiceMatch) { voiceMatch = Session.Match; VoiceBoard.Picked = null; }
                Voice.Picked = VoiceBoard.Picked;
                Voice.Testing = SettingsWindow.VoicePageOpen;
                Voice.Echo = SettingsWindow.VoiceEcho;
                Voice.Tick();
                Voice.Talkers(talkers);
            }
            else talkers.Clear();
            VoiceBoard.Running = Voice != null && GameSettings.VoiceOn;
            VoiceBoard.Self = Session.Online ? Session.Self : 0;
            VoiceBoard.Target = Voice?.Target;
            VoiceBoard.PartyUsable = Voice != null && Voice.PartyUsable;
            VoiceBoard.TeamUsable = Voice != null && Voice.TeamUsable;
            VoiceBoard.PartyOthers = Voice != null ? Voice.PartyOthers : 0;
            VoiceBoard.TeamOthers = Voice != null ? Voice.TeamOthers : 0;
            VoiceBoard.Problem = Voice != null ? Voice.Problem : "";
            VoiceBoard.SetTalking(talkers);
            if (speakers != null) speakers.Volume = GameSettings.Voice;
            if (Session.Match == 0) MatchPieces.Clear();
        }

        // The match sheet's two columns: my team (me, then my party, then the rest) and the other team.
        SheetData FillSheet()
        {
            sheet.Mine.Clear();
            sheet.Theirs.Clear();
            int mine = Session.Roster.TryGetValue(Session.Self, out var me) ? me.Team : 0;
            sheet.MyTeam = mine;
            var party = Session.PartyMembers;
            bool partyHasOthers = party.Length > 1;
            foreach (var p in Session.Roster.Values.OrderBy(p => p.Team != mine ? 3 : p.Id == Session.Self ? 0 : Array.IndexOf(party, p.Id) >= 0 ? 1 : 2)
                                                  .ThenBy(p => p.Slot))
            {
                var player = new SheetPlayer
                {
                    Id = p.Id, Name = Session.Name(p.Id), Me = p.Id == Session.Self, Bot = BotIdentity.IsBot(p.Id),
                    Party = p.Team == mine && partyHasOthers && Array.IndexOf(party, p.Id) >= 0, VoiceOff = Session.VoiceOff(p.Id),
                };
                (p.Team == mine ? sheet.Mine : sheet.Theirs).Add(player);
            }
            return sheet;
        }

        void FollowMatch()
        {
            if (loading) return;
            string active = SceneManager.GetActiveScene().name;
            if (!inMatchScene && Session.Started && active == SceneNames.Lobby)
            {
                var mode = Session.MatchMode ?? GameModes.Default;
                // The lobby never offers a mode without a scene, but a room joined
                // by its number is the host's choice, so check before loading.
                if (!mode.Playable) { Session.Abort(mode.Name + " 모드는 아직 준비 중입니다."); return; }
                inMatchScene = true;
                matchScene = mode.Scene;
                PlaytestSpawner.NetworkDriven = true;
                // Every client reads the same Steam server clock, so obstacles line
                // up across screens without any obstacle messages.
                ObstacleClock.Use(clock.Now);
                // In the background behind the loading screen, not a frozen LoadScene.
                loading = true;
                MovementGate = null;
                if (!loader.Begin(matchScene, mode))
                {
                    loading = inMatchScene = false;
                    matchScene = "";
                    PlaytestSpawner.NetworkDriven = false;
                    ObstacleClock.Use(null); ObstacleClock.Authority = true;
                    Session.Abort(mode.Name + " 경기 씬을 불러오지 못했습니다.");
                }
            }
            else if (inMatchScene && Session.Match == 0)
            {
                inMatchScene = false;
                matchScene = "";
                PlaytestSpawner.NetworkDriven = false;
                ObstacleClock.Use(null); ObstacleClock.Authority = true;
                loader.Cancel();
                Load(SceneNames.Lobby);
            }
        }

        void Load(string scene)
        {
            loading = true;
            MovementGate = null;
            SceneManager.LoadScene(scene);
        }

        // Shared by the lobby and the match HUD.
        public static string MatchRoster(SteamSession session) =>
            string.Join("\n", session.Roster.Values.OrderBy(p => p.Team).ThenBy(p => p.Slot)
                .Select(p => $"{(p.Team == 0 ? "청팀" : "주황팀")}  {session.Name(p.Id)}{(p.Id == session.Self ? " (나)" : "")}{(p.Id == session.Host ? " · 방장" : "")}"));

        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            PlayerProfile.Changed -= OnNameChanged;
            loader?.Cancel();
            ObstacleClock.Use(null); ObstacleClock.Authority = true;
            PlaytestSpawner.NetworkDriven = false;
            Controls?.Disable();
            // The voice stops recording while Steam is still up.
            Voice?.Dispose();
            Voice = null;
            VoiceBoard.Running = false;
            SettingsWindow.VoiceLevel = null;
            SettingsWindow.VoiceProblem = null;
            // Motion must go before the session: it unregisters from SessionChanged.
            Motion?.Dispose();
            Session?.Dispose();
            Instance = null;
        }

        // Steam's server clock is identical on every client but ticks in whole
        // seconds. The fraction comes from the local clock, re-anchored at each
        // tick, which keeps clients within about a frame of each other.
        // A first cut: its real skew between machines is for the network review.
        sealed class SharedClock
        {
            uint lastSecond;
            double anchoredAt;

            public double Now()
            {
                uint second = SteamUtils.GetServerRealTime();
                double local = Time.realtimeSinceStartupAsDouble;
                if (second != lastSecond) { lastSecond = second; anchoredAt = local; }
                return second + Math.Min(.999, local - anchoredAt);
            }
        }
    }
}
