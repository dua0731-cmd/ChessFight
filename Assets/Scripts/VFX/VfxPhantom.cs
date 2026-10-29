using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// Dark afterimages of a piece (R164, 10-27, 사용자: "잔상도 다시 디자인해줘"): its own shape (a skinned mesh baked in
    /// its pose of the moment, or a plain mesh) left where it was, near black and see-through with a dark red rim
    /// (VFX_PhantomPremul), drifting back a little along -Z and burning away from the noise's edges over
    /// <see cref="life"/>. Game code hands it the piece (<see cref="Take"/>, once per copy); left alone (the workbench) it
    /// copies the piece on the workbench stage <see cref="autoCopies"/> times along +Z, one after another, as if it ran.
    /// </summary>
    public sealed class VfxPhantom : MonoBehaviour
    {
        static readonly int FadeId = Shader.PropertyToID("_Fade"), DissolveId = Shader.PropertyToID("_Dissolve"),
            IntensityId = Shader.PropertyToID("_Intensity");

        public Material material;
        [Min(.05f)] public float life = .4f;
        [Range(0f, 1f), Tooltip("How solid a copy is when it is left (the material's body alpha times this).")]
        public float opacity = 1f;
        [Tooltip("HDR multiplier on the rim colour (see Docs/VFX §6).")]
        public float rimIntensity = 2.5f;
        [Tooltip("Metres a copy drifts back along -Z while it fades.")]
        public float drift = .12f;
        [Range(0f, 1f), Tooltip("How far it has burnt away by the end.")]
        public float dissolveEnd = 1f;

        [Header("By itself (the workbench)")]
        [Min(1)] public int autoCopies = 5;
        public float autoSpacing = 1.1f;
        [Min(0f)] public float autoGap = .06f;

        sealed class Copy
        {
            public GameObject go;
            public MeshRenderer view;
            public Mesh baked;
            public Vector3 at;
            public float born, life, opacity;
        }

        readonly List<Copy> copies = new List<Copy>();
        MaterialPropertyBlock block;
        bool taken;
        int made;
        float age;

        /// <summary>
        /// Leaves a copy of <paramref name="source"/> as it is now, moved by <paramref name="shift"/> (a copy of where it
        /// was a moment ago), living <paramref name="lifeScale"/> × <see cref="life"/> and <paramref name="opacityScale"/>
        /// × as solid.
        /// </summary>
        public void Take(Renderer source, Vector3 shift = default, float lifeScale = 1f, float opacityScale = 1f)
        {
            taken = true;
            Add(source, shift, lifeScale, opacityScale);
        }

        void OnEnable()
        {
            age = 0f;
            made = 0;
            taken = false;
        }

        void OnDestroy()
        {
            foreach (var c in copies)
                if (c.baked != null) Destroy(c.baked);
            copies.Clear();
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (!taken) Auto();
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = copies.Count - 1; i >= 0; i--)
            {
                var c = copies[i];
                float k = (age - c.born) / c.life;
                if (k >= 1f || c.go == null)
                {
                    if (c.go != null) Destroy(c.go);
                    if (c.baked != null) Destroy(c.baked);
                    copies.RemoveAt(i);
                    continue;
                }
                c.go.transform.position = c.at - transform.forward * (drift * k);
                c.view.GetPropertyBlock(block);
                block.SetFloat(FadeId, c.opacity * Mathf.Pow(1f - k, 1.3f));
                block.SetFloat(DissolveId, dissolveEnd * Mathf.Pow(k, 1.5f));
                block.SetFloat(IntensityId, rimIntensity);
                c.view.SetPropertyBlock(block);
            }
        }

        /// <summary>The workbench: the piece on its stage, copied along +Z one after another.</summary>
        void Auto()
        {
            while (made < autoCopies && age >= made * autoGap)
            {
                var source = StagePiece();
                if (source == null)
                {
                    made = autoCopies;
                    return;
                }
                Add(source, transform.forward * (autoSpacing * (made + 1)), 1f, 1f);
                made++;
            }
        }

        static Renderer StagePiece()
        {
            var stage = GameObject.Find("VFX Workbench Stage");
            if (stage == null) return null;
            foreach (Transform t in stage.transform)
            {
                if (t.name == "Floor" || t.name.StartsWith("Target")) continue;
                Renderer r = t.GetComponentInChildren<SkinnedMeshRenderer>();
                if (r == null) r = t.GetComponentInChildren<MeshRenderer>();
                if (r != null) return r;
            }
            return null;
        }

        void Add(Renderer source, Vector3 shift, float lifeScale, float opacityScale)
        {
            if (source == null || material == null) return;
            Mesh mesh, baked = null;
            Vector3 scale = Vector3.one;
            if (source is SkinnedMeshRenderer skin)
            {
                baked = new Mesh { name = "VFX phantom", hideFlags = HideFlags.DontSave };
                // Baked with the skin's own scale: the copy stands at scale 1.
                skin.BakeMesh(baked, true);
                mesh = baked;
            }
            else
            {
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) return;
                mesh = filter.sharedMesh;
                scale = source.transform.lossyScale;
            }
            var go = new GameObject("Phantom copy", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            var at = source.transform.position + shift;
            go.transform.SetPositionAndRotation(at, source.transform.rotation);
            go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var view = go.GetComponent<MeshRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            copies.Add(new Copy
            {
                go = go, view = view, baked = baked, at = at, born = age,
                life = Mathf.Max(.05f, life * lifeScale), opacity = opacity * opacityScale,
            });
        }
    }
}
