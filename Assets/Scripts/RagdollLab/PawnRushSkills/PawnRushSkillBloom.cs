using UnityEngine;
#if CF_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Bloom on a camera of the skill test scenes (R79): the skill effects are drawn brighter than white (HDR) and
    /// this makes them glow, spilling their colour around them. Since the URP move it is URP's own Bloom: a global
    /// volume with a Bloom override, and post-processing turned on for this camera while it is enabled (it was a
    /// hand-made bloom through OnRenderImage, which URP does not call). Light above <see cref="threshold"/> (linear,
    /// as before; URP takes it in gamma) blooms. The lit test floor measures up to 1.7 (mean 0.9), so the threshold sits
    /// above it and only the effects' hot cores bloom (a first try at 1.15 washed the whole picture white). The test bed
    /// adds it; nothing else does. Without the URP package (the Linux compile check) it does nothing.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PawnRushSkillBloom : MonoBehaviour
    {
        public float threshold = 2.2f;
        public float intensity = 0.55f;
        [Range(0f, 1f)] public float scatter = 0.7f;

#if CF_URP
        GameObject holder;
        Volume volume;
        VolumeProfile profile;
        Bloom bloom;
        UniversalAdditionalCameraData cameraData;
        bool hadPostProcessing;

        void OnEnable()
        {
            cameraData = GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();
            hadPostProcessing = cameraData.renderPostProcessing;
            cameraData.renderPostProcessing = true;
            if (volume == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.hideFlags = HideFlags.HideAndDontSave;
                bloom = profile.Add<Bloom>(true);
                holder = new GameObject("Skill Bloom (volume)");
                holder.transform.SetParent(transform, false);
                holder.layer = 0;
                volume = holder.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100f;
                volume.sharedProfile = profile;
            }
            volume.enabled = true;
            Apply();
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (bloom == null) return;
            bloom.active = intensity > 0f;
            bloom.threshold.value = Mathf.LinearToGammaSpace(threshold);
            bloom.intensity.value = intensity;
            bloom.scatter.value = scatter;
        }

        void OnDisable()
        {
            if (volume != null) volume.enabled = false;
            if (cameraData != null) cameraData.renderPostProcessing = hadPostProcessing;
        }

        void OnDestroy()
        {
            if (holder != null) Destroy(holder);
            if (profile != null) Destroy(profile);
        }
#endif
    }
}
