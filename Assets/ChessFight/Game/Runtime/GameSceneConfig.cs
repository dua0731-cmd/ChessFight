using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // The single place a designer wires up prototype content. Drop this on a scene
    // object and assign the prefabs and UI assets in the Inspector.
    //
    // Every field falls back to a Resources copy so the prototype still boots from
    // a scene where nothing is wired, which keeps the old SampleScene path alive.
    // This component deliberately has no Steam or networking dependency, so the
    // prefabs referencing it stay valid before Steamworks is installed.
    public sealed class GameSceneConfig : MonoBehaviour
    {
        public const string ResourceRoot = "ChessFight/";

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
        [Tooltip("The Network branch still carries URP references without a URP package. " +
                 "Leave on until the render pipeline is properly migrated.")]
        [SerializeField] bool forceBuiltInPipeline = true;
        [SerializeField] Color ambientLight = new Color(.55f, .58f, .65f);

        public GameObject ArenaPrefab => arenaPrefab != null ? arenaPrefab : Resources.Load<GameObject>(ResourceRoot + "Arena");
        public PawnAvatar PawnPrefab => pawnPrefab != null ? pawnPrefab : Load<PawnAvatar>(ResourceRoot + "PawnAvatar");
        public Material BlueTeamMaterial => blueTeamMaterial != null ? blueTeamMaterial : Resources.Load<Material>(ResourceRoot + "TeamBlue");
        public Material OrangeTeamMaterial => orangeTeamMaterial != null ? orangeTeamMaterial : Resources.Load<Material>(ResourceRoot + "TeamOrange");
        public VisualTreeAsset HudLayout => hudLayout != null ? hudLayout : Resources.Load<VisualTreeAsset>(ResourceRoot + "NetworkHud");
        public ThemeStyleSheet HudTheme => hudTheme != null ? hudTheme : Resources.Load<ThemeStyleSheet>(ResourceRoot + "NetworkTheme");
        public PanelSettings HudPanelSettings => hudPanelSettings;
        public Vector2Int HudReferenceResolution => hudReferenceResolution;
        public bool ForceBuiltInPipeline => forceBuiltInPipeline;
        public Color AmbientLight => ambientLight;

        static T Load<T>(string path) where T : Component
        {
            var go = Resources.Load<GameObject>(path);
            return go != null ? go.GetComponent<T>() : null;
        }
    }
}
