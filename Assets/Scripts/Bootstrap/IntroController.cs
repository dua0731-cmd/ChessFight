using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // Title screen. NetworkRuntime starts Steam while this is on screen, and any
    // key moves on to the lobby. Deliberately needs no clickable UI, only a key.
    [DisallowMultipleComponent]
    public sealed class IntroController : MonoBehaviour
    {
        const float MinimumShow = 1f;

        PanelSettings ownedPanel;
        Label status, hint;
        float shownAt;

        void Awake()
        {
            var root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("IntroHud"),
                                            Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                            new Vector2Int(1280, 720), out ownedPanel);
            status = root?.Q<Label>("intro-status");
            hint = root?.Q<Label>("intro-hint");
            shownAt = Time.unscaledTime;
        }

        void Update()
        {
            var session = NetworkRuntime.Instance != null ? NetworkRuntime.Instance.Session : null;
            bool ready = Time.unscaledTime - shownAt >= MinimumShow;
            if (status != null) status.text = Describe(session);
            if (hint != null) hint.text = ready ? "아무 키나 눌러 시작" : "";
            // A failed Steam start still continues: the lobby has the retry button.
            if (ready && LegacyKeys.AnyDown()) SceneManager.LoadScene(SceneNames.Lobby);
        }

        static string Describe(SteamSession session)
        {
            if (session == null) return "";
            if (session.Online) return "Steam 연결됨";
            return string.IsNullOrEmpty(session.Error) ? "Steam 연결 중..." : session.Error;
        }

        void OnDestroy() { if (ownedPanel != null) Destroy(ownedPanel); }
    }
}
