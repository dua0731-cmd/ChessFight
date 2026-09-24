using System.Linq;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    // Composition root: it owns the Steam session and hands the pieces that do not
    // know about Steam (spawner, camera, HUD, input) the data they need.
    //
    // This lives in a Steam-gated assembly so the prefabs and the HUD keep
    // compiling before Steamworks.NET is installed.
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        // Only this scene opts into the prototype: it is the one carrying a wired
        // GameSceneConfig. The leftover template SampleScene stays inert.
        const string BootScene = "ChessFightLab";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name != BootScene) return;
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            var existing = FindFirstObjectByType<GameSceneConfig>();
            var host = existing != null ? existing.gameObject : new GameObject("ChessFight Game Root");
            host.AddComponent<GameBootstrap>();
        }

        GameSceneConfig config;
        SteamSession session;
        SteamMotion motion;
        PawnSpawner spawner;
        CameraRig cameraRig;
        NetworkHudView hud;
        IMoveInputSource input;
        GameObject arena;
        RenderPipelineAsset previousPipeline, previousQualityPipeline;
        bool pipelineOverridden;
        float refreshAt;
        // Pure navigation state. The session never reads it; it only decides which
        // group of buttons the action stack shows.
        HudStep step = HudStep.Home;
        bool friendsOpen;
        float refreshFriendsAt;

        void Awake()
        {
            Application.runInBackground = true;
            config = GetComponent<GameSceneConfig>();
            if (config == null) config = gameObject.AddComponent<GameSceneConfig>();

            if (config.ForceBuiltInPipeline)
            {
                // The Network branch still carries URP references without a URP
                // package. Borrow built-in rendering and hand it back on teardown.
                previousPipeline = GraphicsSettings.defaultRenderPipeline;
                previousQualityPipeline = QualitySettings.renderPipeline;
                GraphicsSettings.defaultRenderPipeline = null;
                QualitySettings.renderPipeline = null;
                pipelineOverridden = true;
            }
            RenderSettings.ambientLight = config.AmbientLight;

            var arenaPrefab = config.ArenaPrefab;
            if (arenaPrefab != null) { arena = Instantiate(arenaPrefab); arena.name = arenaPrefab.name; }
            else Debug.LogError("[ChessFight] Arena 프리팹이 없습니다. GameSceneConfig에 지정하세요.");

            cameraRig = gameObject.AddComponent<CameraRig>();
            cameraRig.Initialize();

            spawner = new GameObject("Pawns").AddComponent<PawnSpawner>();
            spawner.Configure(config.PawnPrefab, config.BlueTeamMaterial, config.OrangeTeamMaterial);

            input = MoveInputSources.Create();
            input.Enable();

            session = new SteamSession();
            session.Initialize();
            if (session.Online) motion = new SteamMotion(session);

            hud = gameObject.AddComponent<NetworkHudView>();
            hud.Build(config.HudLayout, config.HudTheme, config.HudPanelSettings, config.HudReferenceResolution);
            WireHud();
        }

        void WireHud()
        {
            // Navigation only: pressing play must not start anything by itself.
            hud.Play += () => step = HudStep.Mode;
            hud.MakeParty += () => step = HudStep.Party;
            hud.Back += () => step = step == HudStep.Party ? HudStep.Mode : HudStep.Home;

            // These do reach Steam, and each one makes the session busy, which
            // moves the stack to the busy group on the next frame.
            hud.QuickMatch += () => session.FindMatch();
            hud.PartyStart += () => session.FindMatch();
            hud.CreateTest += () => session.FindMatch(true);
            hud.JoinParty += id => session.JoinParty(id);
            hud.JoinMatch += id => session.JoinPrivateMatch(id);

            // The in-game picker replaces the Steam overlay, which renders at its
            // own resolution and hides presence behind a search box.
            hud.Invite += () => { friendsOpen = true; refreshFriendsAt = 0; };
            hud.CloseFriends += () => friendsOpen = false;
            hud.RefreshFriends += () => refreshFriendsAt = 0;
            hud.InviteFriend += id => session.InviteToParty(id);
            hud.SteamOverlayInvite += () => session.Invite();
            hud.StartGame += () => session.StartGame();
            hud.Cancel += () => { session.Cancel(); step = HudStep.Home; };
            hud.LeaveParty += () => { session.LeaveParty(); step = HudStep.Home; };
            hud.CopyParty += () => GUIUtility.systemCopyBuffer = session.Party.ToString();
            hud.CopyMatch += () => GUIUtility.systemCopyBuffer = session.Match.ToString();
            hud.AddBot += () => session.SetPartyBots(session.PartyBots + 1);
            hud.RemoveBot += () => session.SetPartyBots(session.PartyBots - 1);
            hud.FillRoom += () => session.FillRoomWithBots();
            hud.ClearRoomBots += () => session.ClearRoomBots();
            hud.RetrySteam += () => session.Retry();
        }

        void Update()
        {
            if (session == null) return;
            session.Tick();
            // A successful retry needs the movement layer built after the fact.
            if (motion == null && session.Online) motion = new SteamMotion(session);

            // Read every frame so an edge-triggered jump is never buffered across
            // the frames where movement is suppressed.
            var intent = input != null ? input.Read() : default;
            if (hud == null || !hud.MovementEnabled) intent = default;
            float x = Mathf.Clamp(intent.Move.x, -1f, 1f), z = Mathf.Clamp(intent.Move.y, -1f, 1f);
            motion?.Update(x, z, intent.Jump);

            float dt = Time.unscaledDeltaTime;
            if (motion != null) spawner.Sync(motion.States, dt);
            if (spawner.TryGet(session.Self, out var local)) cameraRig.Follow(local.transform, dt);

            // Presence changes while the panel sits open, so re-poll on a timer.
            if (friendsOpen && Time.unscaledTime >= refreshFriendsAt)
            {
                refreshFriendsAt = Time.unscaledTime + 3f;
                hud.SetFriends(session.Friends());
            }

            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + .2f;
            var model = BuildModel();
            hud.Render(in model);
        }

        HudModel BuildModel()
        {
            // Searching or sitting in a match room outranks navigation: the player
            // needs the cancel button wherever they had browsed to. Busy alone is
            // too broad - it is also true while the opening solo party is still
            // being created, which would greet the player with a cancel button.
            bool inFlight = session.Searching || session.Match != 0 ||
                            (session.Party != 0 && session.Busy && !session.IsLeader);
            if (inFlight) step = HudStep.Busy;
            else if (step == HudStep.Busy) step = HudStep.Home;

            // Inviting needs a joinable party, which searching turns off.
            if (session.Busy || !session.Online) friendsOpen = false;

            bool idleLeader = session.Online && session.IsLeader && !session.Busy;
            bool hostWaiting = session.IsHost && !session.Started;
            int humans = session.PartyMembers.Length;
            return new HudModel
            {
                Step = step,
                Status = session.Status,
                Error = session.Error,
                Hint = Hint(),
                Details = $"Steam: {(session.Online ? "연결됨" : "연결 안 됨 - Steam 실행 후 다시 연결")}\n" +
                          $"{motion?.ConnectionStatus}\n입력: {input?.DisplayName}",
                PartyId = "파티  " + (session.Party == 0 ? "—" : session.Party.ToString()),
                MatchId = "경기  " + (session.Match == 0 ? "—" : session.Match.ToString()),
                RosterTitle = session.Match == 0 ? $"내 파티  {humans}/6" : $"경기 명단  {session.Roster.Count}/12",
                Roster = BuildRoster(),
                Bots = session.Match == 0
                    ? $"파티 봇  {session.PartyBots} / {session.MaxPartyBots}"
                    : $"방 인원  {session.Roster.Count} / 12  (봇 {session.RoomBots})",
                CanQuickMatch = idleLeader,
                CanPartyStart = idleLeader,
                CanCreateTest = idleLeader,
                CanJoinMatch = idleLeader,
                CanInvite = session.Online && !session.Busy && session.Party != 0,
                CanJoinParty = session.Online && !session.Busy,
                // Mirrors StartGame's real rules, so the button is never a no-op.
                CanStart = hostWaiting && (session.PrivateRoom ? session.Roster.Count >= 2 : session.Roster.Count == 12),
                CanCancel = session.Busy,
                CanLeaveParty = session.Online && !session.Busy,
                CanAddBot = idleLeader && session.PartyBots < session.MaxPartyBots,
                CanRemoveBot = idleLeader && session.PartyBots > 0,
                CanFillRoom = hostWaiting && session.Roster.Count < 12,
                CanClearRoomBots = hostWaiting && session.RoomBots > 0,
                CanRetry = !session.Online,
                FriendsOpen = friendsOpen
            };
        }

        string Hint()
        {
            if (!session.Online) return "Steam에 연결되어야 시작할 수 있습니다.";
            switch (step)
            {
                case HudStep.Mode: return "혼자 바로 찾을까요, 친구와 함께 갈까요?";
                case HudStep.Party: return session.IsLeader
                    ? "친구를 초대한 뒤 게임 시작을 누르세요."
                    : "파티장이 시작하기를 기다리는 중입니다.";
                case HudStep.Busy: return session.Match == 0 ? "상대를 찾는 중입니다." : "";
                default: return "";
            }
        }

        string BuildRoster()
        {
            if (session.Match == 0)
            {
                string players = string.Join("\n", session.PartyMembers.Select(id => (id == session.Self ? "★ " : "   ") + session.Name(id)));
                if (session.PartyBots <= 0) return players;
                return players + "\n" + string.Join("\n", Enumerable.Range(1, session.PartyBots).Select(i => "   봇 " + i + " (대기)"));
            }
            return string.Join("\n", session.Roster.Values.OrderBy(p => p.Team).ThenBy(p => p.Slot)
                .Select(p => $"{(p.Team == 0 ? "청팀" : "주황팀")}  {session.Name(p.Id)}{(p.Id == session.Self ? " (나)" : "")}"));
        }

        void OnDestroy()
        {
            input?.Disable();
            // Motion must go before the session: it unregisters from SessionChanged.
            motion?.Dispose();
            session?.Dispose();
            if (spawner != null) { spawner.Clear(); Destroy(spawner.gameObject); }
            if (arena != null) Destroy(arena);
            if (pipelineOverridden)
            {
                GraphicsSettings.defaultRenderPipeline = previousPipeline;
                QualitySettings.renderPipeline = previousQualityPipeline;
            }
        }
    }
}
