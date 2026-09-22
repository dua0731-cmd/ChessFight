using System;
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
        // Only these scenes opt into the prototype. No gameplay scene is rewritten.
        static readonly string[] BootScenes = { "ChessFightLab", "NetworkSandbox", "SampleScene" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(BootScenes, SceneManager.GetActiveScene().name) < 0) return;
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
            else Debug.LogError("[ChessFight] Arena prefab missing. Assign it on GameSceneConfig.");

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
            hud.FindMatch += () => session.FindMatch();
            hud.CreateTest += () => session.FindMatch(true);
            hud.Invite += () => session.Invite();
            hud.StartGame += () => session.StartGame();
            hud.Cancel += () => session.Cancel();
            hud.LeaveParty += () => session.LeaveParty();
            hud.JoinParty += id => session.JoinParty(id);
            hud.JoinMatch += id => session.JoinPrivateMatch(id);
            hud.CopyParty += () => GUIUtility.systemCopyBuffer = session.Party.ToString();
            hud.CopyMatch += () => GUIUtility.systemCopyBuffer = session.Match.ToString();
            hud.AddBot += () => session.SetPartyBots(session.PartyBots + 1);
            hud.RemoveBot += () => session.SetPartyBots(session.PartyBots - 1);
            hud.FillRoom += () => session.FillRoomWithBots();
            hud.ClearRoomBots += () => session.ClearRoomBots();
        }

        void Update()
        {
            if (session == null) return;
            session.Tick();

            // Read every frame so an edge-triggered jump is never buffered across
            // the frames where movement is suppressed.
            var intent = input != null ? input.Read() : default;
            if (hud == null || !hud.MovementEnabled) intent = default;
            float x = Mathf.Clamp(intent.Move.x, -1f, 1f), z = Mathf.Clamp(intent.Move.y, -1f, 1f);
            motion?.Update(x, z, intent.Jump);

            float dt = Time.unscaledDeltaTime;
            if (motion != null) spawner.Sync(motion.States, dt);
            if (spawner.TryGet(session.Self, out var local)) cameraRig.Follow(local.transform, dt);

            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + .2f;
            var model = BuildModel();
            hud.Render(in model);
        }

        HudModel BuildModel()
        {
            bool idleLeader = session.Online && session.IsLeader && !session.Busy;
            bool hostWaiting = session.IsHost && !session.Started;
            int humans = session.PartyMembers.Length;
            return new HudModel
            {
                Status = session.Status,
                Error = session.Error,
                Details = $"Party: {session.Party}  ({humans}/6 players + {session.PartyBots} bots)\n" +
                          $"Match: {session.Match}\n{motion?.ConnectionStatus}\nInput: {input?.DisplayName}",
                Roster = BuildRoster(),
                Bots = session.Match == 0
                    ? $"Party bots  {session.PartyBots} / {session.MaxPartyBots}"
                    : $"Room  {session.Roster.Count} / 12   ({session.RoomBots} bots)",
                CanFindMatch = idleLeader,
                CanCreateTest = idleLeader,
                CanInvite = session.Online && !session.Busy && session.Party != 0,
                CanJoinParty = session.Online && !session.Busy,
                CanJoinMatch = idleLeader,
                // Mirrors StartGame's real rules, so the button is never a no-op.
                CanStart = hostWaiting && (session.PrivateRoom ? session.Roster.Count >= 2 : session.Roster.Count == 12),
                CanCancel = session.Busy,
                CanLeaveParty = session.Online,
                CanAddBot = idleLeader && session.PartyBots < session.MaxPartyBots,
                CanRemoveBot = idleLeader && session.PartyBots > 0,
                CanFillRoom = hostWaiting && session.Roster.Count < 12,
                CanClearRoomBots = hostWaiting && session.RoomBots > 0
            };
        }

        string BuildRoster()
        {
            if (session.Match == 0)
            {
                string players = string.Join("\n", session.PartyMembers.Select(id => (id == session.Self ? "> " : "  ") + session.Name(id)));
                if (session.PartyBots <= 0) return players;
                return players + "\n" + string.Join("\n", Enumerable.Range(1, session.PartyBots).Select(i => "  BOT " + i + " (queued)"));
            }
            return string.Join("\n", session.Roster.Values.OrderBy(p => p.Team).ThenBy(p => p.Slot)
                .Select(p => $"{(p.Team == 0 ? "BLUE" : "ORANGE")}  {session.Name(p.Id)}{(p.Id == session.Self ? " (you)" : "")}"));
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
