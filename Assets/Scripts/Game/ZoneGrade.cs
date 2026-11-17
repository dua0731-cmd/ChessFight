using UnityEngine;
#if CF_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace ChessFight.Game
{
    /// <summary>
    /// One place's own colour grade (R683, the carriage yard's light and air): a global volume made in code over
    /// GameLook's soft grade (priority 10) and under its no-bloom switch (1000) and the skills' flash (100), whose weight
    /// the place sets every frame from where the camera is. So the grade fades in and out at the place's edges with no
    /// trigger collider (a pawn would touch a trigger) and changes nothing anywhere else. Without URP it does nothing.
    /// </summary>
    public sealed class ZoneGrade : MonoBehaviour
    {
        [System.Serializable]
        public struct Look
        {
            public float exposure, contrast, saturation, temperature, tint;
            public Color filter;
            // Shadows / highlights tints (r, g, b, offset), as URP's Shadows Midtones Highlights takes them.
            public Vector4 shadows, highlights;
            public float bloomThreshold, bloomIntensity, bloomScatter;
            public Color bloomTint;
            public float vignette, vignetteSmoothness;
        }

        public const float Priority = 20f;

#if CF_URP
        Volume volume;
        VolumeProfile profile;
#endif
        float weight;

        public static ZoneGrade Add(GameObject host, string name, Look look)
        {
            var grade = host.AddComponent<ZoneGrade>();
#if CF_URP
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = name;
            var colour = profile.Add<ColorAdjustments>(false);
            colour.postExposure.Override(look.exposure);
            colour.contrast.Override(look.contrast);
            colour.saturation.Override(look.saturation);
            colour.colorFilter.Override(look.filter);
            var balance = profile.Add<WhiteBalance>(false);
            balance.temperature.Override(look.temperature);
            balance.tint.Override(look.tint);
            var tones = profile.Add<ShadowsMidtonesHighlights>(false);
            tones.shadows.Override(look.shadows);
            tones.highlights.Override(look.highlights);
            var bloom = profile.Add<Bloom>(false);
            bloom.threshold.Override(look.bloomThreshold);
            bloom.intensity.Override(look.bloomIntensity);
            bloom.scatter.Override(look.bloomScatter);
            bloom.tint.Override(look.bloomTint);
            var vignette = profile.Add<Vignette>(false);
            vignette.intensity.Override(look.vignette);
            vignette.smoothness.Override(look.vignetteSmoothness);
            var volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = Priority;
            volume.weight = 0f;
            volume.enabled = false;
            volume.sharedProfile = profile;
            grade.volume = volume;
            grade.profile = profile;
#endif
            return grade;
        }

        /// <summary>0 = the place's grade is not there at all (the volume is off), 1 = all of it.</summary>
        public float Weight
        {
            get => weight;
            set
            {
                weight = Mathf.Clamp01(value);
#if CF_URP
                if (volume == null) return;
                volume.weight = weight;
                volume.enabled = weight > .001f;
#endif
            }
        }

        void OnDestroy()
        {
#if CF_URP
            if (profile != null) Destroy(profile);
#endif
        }
    }
}
