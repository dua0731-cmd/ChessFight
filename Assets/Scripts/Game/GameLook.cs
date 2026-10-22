#if CF_URP
using System.Collections;
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
