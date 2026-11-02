using UnityEngine;

namespace ChessFight.PawnRush
{
    // Falling water's picture: scrolls the shared waterfall material's streaks downwards. Only a picture (no collider,
    // nothing to do with the clock or the physics), so plain frame time is fine here.
    [RequireComponent(typeof(Renderer))]
    public sealed class WaterfallFlow : MonoBehaviour
    {
        [Tooltip("Texture lengths per second")]
        [SerializeField] float speed = .9f;

        Material material;

        void Awake() => material = GetComponent<Renderer>().sharedMaterial;

        void Update()
        {
            if (material == null) return;
            material.mainTextureOffset = new Vector2(0f, Time.time * speed % 1f);
        }
    }
}
