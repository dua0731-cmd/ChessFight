using UnityEngine;
#if CF_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace ChessFight.RagdollLab
{
    // R834 (승규 11-25 "너무 뿌옇게 됐어 난 아주 맑은 걸 원해"; JY-gfx is not used in the tutorial, "패키지 파일 적용 하지
    // 말자"): the first-run tutorial's air made clear, in this scene only:
    //  - the fog the scene had from the map test (sky blue from 160 m, which veiled the hills) is pushed out to a hint at
    //    the horizon (from fogStart to fogEnd m), every frame in case anything sets it back;
    //  - a global volume over GameLook's soft grade (priority 10), under the settings' switches (1000+): bloom
    //    down to a glint on what is truly bright, no vignette, a little more contrast and colour, exposure a touch up.
    // Built at Start; gone with the scene.
    public sealed class TutorialClearAir : MonoBehaviour
    {
        [SerializeField] float fogStart = 900f, fogEnd = 6000f;
        [SerializeField] Color fogColor = new Color(.80f, .90f, 1f);
        [SerializeField] float exposure = .12f, contrast = 30f, saturation = 30f;
        [SerializeField] float bloomIntensity = .1f, bloomThreshold = 1.35f, bloomScatter = .45f;
        public const float Priority = 30f;

        // R855: the haze colour, set to the sunset's peach by TutorialSunset.
        public Color FogColor { get => fogColor; set => fogColor = value; }

        void Start()
        {
            Air();
#if CF_URP
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Tutorial clear air";
            var colour = profile.Add<ColorAdjustments>(false);
            colour.postExposure.Override(exposure);
            colour.contrast.Override(contrast);
            colour.saturation.Override(saturation);
            var bloom = profile.Add<Bloom>(false);
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.scatter.Override(bloomScatter);
            profile.Add<Vignette>(false).intensity.Override(0f);
            var dof = profile.Add<DepthOfField>(false);
            dof.mode.Override(DepthOfFieldMode.Off);
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = Priority;
            volume.sharedProfile = profile;
#endif
        }

        void LateUpdate() => Air();

        void Air()
        {
            if (RenderSettings.fogMode != FogMode.Linear) RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.fogColor = fogColor;
        }
    }
}
