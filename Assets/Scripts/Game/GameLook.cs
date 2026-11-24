#if CF_URP
using System.Collections;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    /// <summary>
    /// The game's URP look (R152, 승규 님 10-16 "선명한 대비로 해줘", mockup B): the grading itself is the volume
    /// profile on the URP assets (Settings/GameLook.asset: ACES tonemapping, exposure, contrast, saturation, bloom,
    /// vignette), SSAO and 4096 shadows are on the PC renderer. This turns post-processing and SMAA on for every camera
    /// that draws to the screen (code makes most cameras, and a new camera has post-processing off) and, in a scene
    /// that reflects only the sky, renders one reflection probe of the scene once it is built. Cameras that draw into
    /// a texture (portraits, the loading studio) are left as they are, to keep their alpha.
    /// </summary>
    public static class GameLook
    {
        static Runner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            RenderPipelineManager.beginCameraRendering -= Prepare;
            RenderPipelineManager.beginCameraRendering += Prepare;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
            GameSettings.Changed -= BloomSetting;
            GameSettings.Changed += BloomSetting;
            GameSettings.Changed -= ColourAidSetting;
            GameSettings.Changed += ColourAidSetting;
        }

        // R674, settings → 화면 → 그래픽 품질 → 빛 번짐: off puts one global volume over every other one (the profile's,
        // the skills' flash at 100, the loading studio's at 50) that takes the bloom down to nothing. Made the first time
        // it is turned off, kept across scenes.
        static Volume noBloom;

        static void BloomSetting()
        {
            bool off = !GameSettings.Bloom;
            if (noBloom == null)
            {
                if (!off) return;
                var go = new GameObject("Game Look (no bloom)") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                noBloom = go.AddComponent<Volume>();
                noBloom.isGlobal = true;
                noBloom.priority = 1000f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Game Look (no bloom)";
                profile.Add<Bloom>(false).intensity.Override(0f);
                noBloom.sharedProfile = profile;
            }
            noBloom.enabled = off;
        }

        // R792, settings → 접근성 → 색약 모드: one global volume over every other one (priority 1001, above the no-bloom
        // one) whose channel mixer is Core ColourAid's correction for the kind and strength picked. Made the first time
        // it is turned on, kept across scenes; off, disabled. Cameras that draw to the screen have post-processing on
        // (Prepare); the menus and HUD are drawn after the cameras and keep their own colours.
        static Volume colourAid;
        static ChannelMixer colourMixer;
        static int aidKind = -1;
        static float aidStrength = -1f;

        /// <summary>JY-gfx: the colour aid worked out again (the grade's tonemapper changed: GameGrade).</summary>
        public static void RefreshColourAid()
        {
            aidKind = -1;
            ColourAidSetting();
        }

        static void ColourAidSetting()
        {
            int kind = GameSettings.ColourAidKind;
            float strength = GameSettings.ColourAidStrength;
            bool on = kind != ColourAid.Off && strength > 0f;
            if (colourAid == null)
            {
                if (!on) return;
                var go = new GameObject("Game Look (colour aid)") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                colourAid = go.AddComponent<Volume>();
                colourAid.isGlobal = true;
                colourAid.priority = 1001f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Game Look (colour aid)";
                colourMixer = profile.Add<ChannelMixer>(false);
                colourAid.sharedProfile = profile;
            }
            colourAid.enabled = on;
            if (!on || (kind == aidKind && Mathf.Approximately(strength, aidStrength))) return;
            aidKind = kind;
            aidStrength = strength;
            // URP's mixer takes percentages, -200..200, in the grading's own space: ACEScg under GameLook.asset's ACES
            // tonemapper (ColourAid.InAces), linear sRGB under JY-gfx's Neutral one (GameGrade.ReplacesAces).
            var m = GameGrade.ReplacesAces ? ColourAid.Matrix(kind, strength) : ColourAid.InAces(ColourAid.Matrix(kind, strength));
            float P(int i) => Mathf.Clamp(m[i] * 100f, -200f, 200f);
            colourMixer.redOutRedIn.Override(P(0));
            colourMixer.redOutGreenIn.Override(P(1));
            colourMixer.redOutBlueIn.Override(P(2));
            colourMixer.greenOutRedIn.Override(P(3));
            colourMixer.greenOutGreenIn.Override(P(4));
            colourMixer.greenOutBlueIn.Override(P(5));
            colourMixer.blueOutRedIn.Override(P(6));
            colourMixer.blueOutGreenIn.Override(P(7));
            colourMixer.blueOutBlueIn.Override(P(8));
        }

        static void Prepare(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game || camera.targetTexture != null) return;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            if (data.renderType != CameraRenderType.Base) return;
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        // 승규 10-17: past the title ("클릭하여 시작하기") the vivid contrast is too strong. Every other scene gets
        // a scene-wide volume over the profile that brings contrast, saturation and the vignette down.
        const float SoftContrast = 4f, SoftSaturation = 5f, SoftVignette = .08f;

        static void Soften(Scene scene)
        {
            if (scene.name == SceneNames.Intro) return;
            var go = new GameObject("Game Look (soft)");
            SceneManager.MoveGameObjectToScene(go, scene);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Game Look (soft)";
            var grade = profile.Add<ColorAdjustments>(false);
            grade.contrast.Override(SoftContrast);
            grade.saturation.Override(SoftSaturation);
            var vignette = profile.Add<Vignette>(false);
            vignette.intensity.Override(SoftVignette);
            volume.sharedProfile = profile;
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            Soften(scene);
            BloomSetting();
            ColourAidSetting();
            if (runner == null)
            {
                var go = new GameObject("Game Look") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
            }
            runner.StopAllCoroutines();
            runner.StartCoroutine(runner.Probe());
        }

        sealed class Runner : MonoBehaviour
        {
            ReflectionProbe probe;

            public IEnumerator Probe()
            {
                if (probe != null) Destroy(probe.gameObject);
                probe = null;
                // The stages and courses are built by code in their first frames.
                yield return new WaitForSecondsRealtime(1.5f);
                var view = Camera.main;
                if (RenderSettings.defaultReflectionMode != DefaultReflectionMode.Skybox || view == null) yield break;
                var go = new GameObject("Game Look Probe");
                go.transform.position = view.transform.position + view.transform.forward * 8f;
                probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                probe.size = new Vector3(400f, 200f, 400f);
                probe.resolution = 256;
                probe.hdr = true;
                probe.RenderProbe();
            }
        }
    }
}
#endif
