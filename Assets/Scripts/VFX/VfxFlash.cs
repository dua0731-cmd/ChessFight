using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// A short soft flash facing the camera (R159, 10-27): once each time it is turned on it grows from
    /// <see cref="startSize"/> to <see cref="endSize"/> and dies away over <see cref="duration"/>; <see cref="aspect"/> above
    /// 1 stretches it across the screen (an anamorphic streak). Drawn on a quad of its own with <see cref="material"/>
    /// (VFX_SoftGlowAdd), whose fade and intensity it sets on its renderer.
    /// </summary>
    public sealed class VfxFlash : MonoBehaviour
    {
        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity");
        static Mesh quad;

        public Material material;
        [Min(.02f)] public float duration = .1f;
        public float startSize = .2f, endSize = .5f;
        [Tooltip("Width over height: 1 round, above 1 a streak across the screen.")]
        public float aspect = 1f;
        [Tooltip("HDR multiplier on the material's colour (see Docs/VFX §6).")]
        public float intensity = 3f;

        Transform face;
        MeshRenderer view;
        MaterialPropertyBlock block;
        float age;

        void Awake()
        {
            // The quad turns to the camera; this object keeps the place and the prefab's own scale. A prefab built while the
            // editor was playing has one saved in it already (R171): that one is used, not a second.
            face = transform.Find("Flash quad");
            var go = face != null ? face.gameObject : new GameObject("Flash quad");
            go.transform.SetParent(transform, false);
            face = go.transform;
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null) filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = Quad();
            view = go.GetComponent<MeshRenderer>();
            if (view == null) view = go.AddComponent<MeshRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            age = 0f;
            if (view != null) view.enabled = true;
            Draw();
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= duration)
            {
                view.enabled = false;
                return;
            }
            Draw();
        }

        void Draw()
        {
            if (view == null) return;
            float t = Mathf.Clamp01(age / duration);
            var cam = Camera.main;
            if (cam != null) face.rotation = cam.transform.rotation;
            float size = Mathf.Lerp(startSize, endSize, 1f - (1f - t) * (1f - t));
            face.localScale = new Vector3(size * aspect, size, 1f);
            view.GetPropertyBlock(block);
            block.SetFloat(FadeId, (1f - t) * (1f - t));
            block.SetFloat(IntensityId, intensity);
            view.SetPropertyBlock(block);
        }

        static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh
            {
                name = "VFX flash quad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[] { new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f), new Vector3(-.5f, .5f, 0f), new Vector3(.5f, .5f, 0f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
            };
            quad.RecalculateBounds();
            return quad;
        }
    }
}
