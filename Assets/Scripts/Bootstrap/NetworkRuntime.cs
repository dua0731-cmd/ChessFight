using System;
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
    //   Lobby -> KingRush         when the match starts (every client sees phase=playing)
    //   KingRush -> Lobby         when the match ends or the player leaves
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
        public IMoveInputSource Controls { get; private set; }

        // The active scene says whether local input may move the pawn: false while
        // typing a lobby number, or with the window in the background.
        public Func<bool> MovementGate { get; set; }

        bool inMatchScene, loading;
        readonly SharedClock clock = new SharedClock();

        // Only scenes that belong to the online flow start Steam. A course or test
        // scene opened on its own stays offline and runs its local playtest.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string first = SceneManager.GetActiveScene().name;
            if (first == SceneNames.Intro || first == SceneNames.Lobby) Ensure();
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
            Session.Initialize();
            if (Session.Online) Motion = new SteamMotion(Session);

            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach(SceneManager.GetActiveScene());
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            loading = false;
            Attach(scene);
        }

        void Attach(Scene scene)
        {
            switch (scene.name)
            {
                case SceneNames.Intro: Host(scene).AddComponent<IntroController>(); break;
                case SceneNames.Lobby: Host(scene).AddComponent<LobbyBootstrap>(); break;
                case SceneNames.KingRush:
                    // Opened as the match scene: show the networked pawns instead
                    // of the offline playtest.
                    if (inMatchScene) Host(scene).AddComponent<KingRushMatchView>();
                    break;
            }
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
            Session.Tick();
            // A successful Steam retry needs the movement layer built after the fact.
            if (Motion == null && Session.Online) Motion = new SteamMotion(Session);

            // Read every frame so an edge-triggered jump is never buffered across
            // the frames where movement is suppressed.
            var intent = Controls.Read();
            if (MovementGate != null && !MovementGate()) intent = default;
            Motion?.Update(Mathf.Clamp(intent.Move.x, -1f, 1f), Mathf.Clamp(intent.Move.y, -1f, 1f), intent.Jump);

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
        }

        void FollowMatch()
        {
            if (loading) return;
            string active = SceneManager.GetActiveScene().name;
            if (!inMatchScene && Session.Started && active == SceneNames.Lobby)
            {
                inMatchScene = true;
                PlaytestSpawner.NetworkDriven = true;
                // Every client reads the same Steam server clock, so obstacles line
                // up across screens without any obstacle messages.
                ObstacleClock.Use(clock.Now);
                Load(SceneNames.KingRush);
            }
            else if (inMatchScene && Session.Match == 0)
            {
                inMatchScene = false;
                PlaytestSpawner.NetworkDriven = false;
                ObstacleClock.Use(null);
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
                .Select(p => $"{(p.Team == 0 ? "청팀" : "주황팀")}  {session.Name(p.Id)}{(p.Id == session.Self ? " (나)" : "")}"));

        void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ObstacleClock.Use(null);
            PlaytestSpawner.NetworkDriven = false;
            Controls?.Disable();
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
