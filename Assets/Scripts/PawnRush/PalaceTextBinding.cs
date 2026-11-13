using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // OS fonts and their atlases cannot be stored in a prefab. Rebind them in the prefab stage and in Play.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PalaceTextBinding : MonoBehaviour
    {
        [SerializeField] TextMesh[] labels = new TextMesh[0];
        Font current;

        public void Configure(TextMesh[] value)
        {
            labels = value ?? new TextMesh[0];
            Bind();
        }

        void OnEnable()
        {
            if (!gameObject.scene.IsValid()) return;
            Font.textureRebuilt += AtlasChanged;
            Bind();
        }

        void OnDisable() => Font.textureRebuilt -= AtlasChanged;

        public void Bind()
        {
            if (!gameObject.scene.IsValid()) return;
            current = RuntimePanels.KoreanFont;
            if (current == null) return;
            foreach (var label in labels)
            {
                if (label == null) continue;
                label.font = current;
                var renderer = label.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = current.material;
            }
        }

        void AtlasChanged(Font changed)
        {
            if (changed != current) return;
            foreach (var label in labels)
                if (label != null && label.font == current && label.TryGetComponent<MeshRenderer>(out var renderer)) renderer.sharedMaterial = changed.material;
        }
    }
}
