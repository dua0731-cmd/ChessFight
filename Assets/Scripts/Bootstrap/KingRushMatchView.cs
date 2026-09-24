using System;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // KingRush opened as the match scene. Draws the networked pawns on the course
    // and a read-only status panel; Esc leaves the match.
    //
    // PLACEHOLDER MOVEMENT: pawns still move with the lobby's flat PawnMotor,
    // which clamps them to a 38 x 38 area at ground height and ignores colliders.
    // They stand on the start platform and cannot run the course or be hit by
    // obstacles until the ragdoll is simulated by the host. Offline playtest is
    // where the course is exercised for now.
    [DisallowMultipleComponent]
    public sealed class KingRushMatchView : MonoBehaviour
    {
        NetworkRuntime runtime;
        PawnSpawner spawner;
        CameraRig cameraRig;
        PanelSettings ownedPanel;
        Label status, roster, link, warning;
        Func<bool> gate;
        float refreshAt;

        void Awake()
        {
            runtime = NetworkRuntime.Instance;
            if (runtime == null) { enabled = false; return; }

            var config = GetComponent<GameSceneConfig>();
            if (config == null) config = gameObject.AddComponent<GameSceneConfig>();

            cameraRig = FindFirstObjectByType<CameraRig>();
            if (cameraRig == null) cameraRig = gameObject.AddComponent<CameraRig>();
            cameraRig.Initialize();

            spawner = new GameObject("Pawns").AddComponent<PawnSpawner>();
            spawner.Configure(config.PawnPrefab, config.BlueTeamMaterial, config.OrangeTeamMaterial);

            var root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("MatchHud"),
                                            config.HudTheme, config.HudPanelSettings,
                                            config.HudReferenceResolution, out ownedPanel);
            status = root?.Q<Label>("match-status");
            roster = root?.Q<Label>("match-roster");
            link = root?.Q<Label>("match-link");
            warning = root?.Q<Label>("match-warning");

            gate = () => Application.isFocused;
            runtime.MovementGate = gate;
        }

        void Update()
        {
            var session = runtime.Session;
            if (LegacyKeys.Down(KeyCode.Escape)) session.Cancel();

            float dt = Time.unscaledDeltaTime;
            if (runtime.Motion != null) spawner.Sync(runtime.Motion.States, dt);
            if (spawner.TryGet(session.Self, out var local)) cameraRig.Follow(local.transform, dt);

            // Every frame, not on the HUD timer: the plan's first stage is a warning
            // within half a second of the host going quiet.
            ShowWarning(runtime.Motion);

            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + .2f;
            if (status != null) status.text = session.Status;
            if (roster != null) roster.text = NetworkRuntime.MatchRoster(session);
            if (link != null) link.text = runtime.Motion?.QualityLine ?? "";
        }

        void ShowWarning(SteamMotion motion)
        {
            if (warning == null) return;
            var health = motion?.Health ?? LinkHealth.Ok;
            bool show = health == LinkHealth.Unstable || health == LinkHealth.Frozen;
            warning.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) warning.text = motion.ConnectionStatus;
            warning.EnableInClassList("warning-frozen", health == LinkHealth.Frozen);
        }

        void OnDestroy()
        {
            if (runtime != null && runtime.MovementGate == gate) runtime.MovementGate = null;
            if (spawner != null) { spawner.Clear(); Destroy(spawner.gameObject); }
            if (ownedPanel != null) Destroy(ownedPanel);
        }
    }
}
