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

        /// <summary>The ultimates' share of the picture now (0..1, PawnRushSkillFx.UltScreen.cs) and a one-frame punch (the
        /// third frame of a blow or a judgement): while an ultimate shows, the bloom is tighter (less scatter), a little
        /// stronger and clamped, so thin neon lines get halos and no pixel spreads a blob over the picture.</summary>
        public static float UltK, UltPunch;

        /// <summary>The volume set at once from <see cref="UltK"/> and <see cref="UltPunch"/> (the effects call it after
        /// setting them, so a punch frame is bloomed in its own frame whatever order the LateUpdates run in).</summary>
        public void Refresh()
        {
#if CF_URP
            Apply();
#endif
        }

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
            float k = Mathf.Clamp01(UltK);
            bloom.intensity.value = Mathf.Lerp(intensity, Mathf.Min(0.70f, Mathf.Max(intensity, 0.70f)), k) * (1f + 0.6f * Mathf.Clamp01(UltPunch));
            bloom.scatter.value = Mathf.Lerp(scatter, Mathf.Min(scatter, 0.5f), k);
            bloom.clamp.overrideState = true;
            bloom.clamp.value = k > 0.01f ? 6f : 65472f;
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
