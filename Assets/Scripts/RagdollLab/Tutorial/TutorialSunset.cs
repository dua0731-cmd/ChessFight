using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R855 (user 11-26 "튜토리얼 씬에 인트로같은 노을을 추가해줘", then "노을이랑 빛 효과만 … 구름이나 이런 부분은 전으로"):
    /// the first-run tutorial lit like the intro's sunset. Then (11-26 "하늘도 노을로 만들어줘"): the sky too, but with the
    /// scene's own clouds - the same cumulus re-rendered at sunset (ArtSource/Tutorial/tu_sky_v1.py --sunset: the intro's
    /// gold-pink-violet by height, the sun low at the same azimuth, the clouds lit pink by it) in
    /// Resources/TutorialSky/TU_SkyCloudsSunset.exr, put in a copy of the scene's panoramic sky material (TU_Sky).
    ///  - the sun: the scene's own directional light turned low and warm (the intro's "Sunset Light" orange, a little
    ///    lighter for an open yard), from the same side of the sky as before so the lessons' shadows fall the same way;
    ///  - the ambient light: dusk violet from above, warm round the horizon (Trilight, as in the intro);
    ///  - the far haze (TutorialClearAir, only a hint at the horizon) in the sunset's peach.
    /// Added by TutorialCourse.Start; everything goes with the scene.
    /// </summary>
    public sealed class TutorialSunset : MonoBehaviour
    {
        // Where the sun stands: degrees from +Z towards +X (the scene light came from about 125 degrees), and above the
        // horizon, low but high enough that the walls' shadows stay short enough to read the lessons by.
        const float SunAzimuth = 125f, SunElevation = 26f;
        const int SunColor = 0xFFB070;          // the intro's 0xFF9C58, lightened for daylight-lit grass and stone
        const float SunIntensity = 2.4f;        // the scene's daylight was 1.9 white
        const int AmbientSky = 0x8C84B4, AmbientEquator = 0xD49C78, AmbientGround = 0x4E3E32;
        const int HazeColor = 0xF0B48C;         // the intro sunset picture's horizon

        void Start()
        {
            Sky();
            Sun();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(AmbientSky);
            RenderSettings.ambientEquatorColor = Hex(AmbientEquator);
            RenderSettings.ambientGroundColor = Hex(AmbientGround);
            var air = FindAnyObjectByType<TutorialClearAir>();
            if (air != null) air.FogColor = Hex(HazeColor);
            else RenderSettings.fogColor = Hex(HazeColor);
        }

        void Sun()
        {
            float a = SunAzimuth * Mathf.Deg2Rad, e = SunElevation * Mathf.Deg2Rad;
            var travel = -new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
            Light sun = RenderSettings.sun;
            if (sun == null || sun.type != LightType.Directional)
                foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional) { sun = light; break; }
            if (sun == null)
            {
                sun = new GameObject("Tutorial Sunset Light").AddComponent<Light>();
                sun.transform.SetParent(transform, false);
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
            }
            sun.transform.rotation = Quaternion.LookRotation(travel);
            sun.color = Hex(SunColor);
            sun.intensity = SunIntensity;
            RenderSettings.sun = sun;
        }

        Material sky;

        void Sky()
        {
            var day = RenderSettings.skybox;
            var image = Resources.Load<Texture>("TutorialSky/TU_SkyCloudsSunset");
            if (day == null || image == null || !day.HasProperty("_MainTex")) return;
            sky = new Material(day) { name = "Tutorial sunset sky" };
            sky.SetTexture("_MainTex", image);
            RenderSettings.skybox = sky;
        }

        void OnDestroy()
        {
            if (sky != null) Destroy(sky);
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }
}
