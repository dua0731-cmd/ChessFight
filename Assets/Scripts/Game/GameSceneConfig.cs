using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The single place a designer wires up prototype content. It sits on the
    // scene's ChessFight Game Root and the fields are assigned in the Inspector.
    //
    // Prefabs and materials must be assigned here. Only the UI assets, which live
    // in Assets/Resources because their importer-generated IDs cannot be wired by
    // hand, fall back to a Resources lookup.
    // This component deliberately has no Steam or networking dependency, so the
    // prefabs referencing it stay valid before Steamworks is installed.
    public sealed class GameSceneConfig : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] GameObject arenaPrefab;
        [SerializeField] PawnAvatar pawnPrefab;
        [SerializeField] Material blueTeamMaterial;
        [SerializeField] Material orangeTeamMaterial;

        [Header("HUD")]
        [SerializeField] VisualTreeAsset hudLayout;
        [SerializeField] ThemeStyleSheet hudTheme;
        [Tooltip("Optional. One is created at runtime when empty.")]
        [SerializeField] PanelSettings hudPanelSettings;
        [SerializeField] Vector2Int hudReferenceResolution = new Vector2Int(1280, 720);

        [Header("Rendering")]
        [SerializeField] Color ambientLight = new Color(.55f, .58f, .65f);

        public GameObject ArenaPrefab => arenaPrefab;
        public PawnAvatar PawnPrefab => pawnPrefab;
        public Material BlueTeamMaterial => blueTeamMaterial;
        public Material OrangeTeamMaterial => orangeTeamMaterial;
        public VisualTreeAsset HudLayout => hudLayout != null ? hudLayout : Resources.Load<VisualTreeAsset>("NetworkHud");
        public ThemeStyleSheet HudTheme => hudTheme != null ? hudTheme : Resources.Load<ThemeStyleSheet>("NetworkTheme");
        public PanelSettings HudPanelSettings => hudPanelSettings;
        public Vector2Int HudReferenceResolution => hudReferenceResolution;
        public Color AmbientLight => ambientLight;
    }
}
