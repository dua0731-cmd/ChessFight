using UnityEngine;
using UnityEngine.Rendering;
#if CF_URP
using UnityEngine.Rendering.Universal;
#endif

namespace ChessFight.Game
{
    // R610 (11-12, 승규 님: "여기서 체스판 전체적인 분위기만 그래픽 좋게 해줘"): the study's chessboard on the matching screen
    // (R609 brought the study back from the R608 LED stage), dressed up without moving anything that stands on it:
    //   - the board's lacquer mirrors the pawns and the lamp's pool (a mirror camera under the board, drawn with the R608
    //     stage's Stage Floor shader, see LoadingStudio.Stage.cs);
    //   - a dark lacquered plinth under the board's frame;
    //   - the room falls off into the dark round the lamp's pool, the table no longer shines with the scene's sky, and a
    //     cool rim from behind on each side lifts the pawns off the shelves;
    //   - two warm wall lamps between the shelves;
    //   - its own grading: ACES, a soft bloom, the shelves out of focus, a vignette.
    // The stage's fields (mirror, mirrorTexture, boardGloss, lookProfile) are reused: the stage and the study are never
    // both built, and DisposeStage frees them.
    public sealed partial class LoadingStudio
    {
        bool studyLook;

        void StudyLook(Transform t)
        {
            if (camera == null || Texture == null) return;
            studyLook = true;
            camera.allowHDR = true;
            camera.backgroundColor = MenuArt.Hex(0x060403);

            // the table, the board and its plinth lie on the floor layer: the mirror does not draw them
            var table = t.Find("Table");
            if (table != null) table.gameObject.layer = FloorLayer;
            var board = t.Find("Board");
            if (board != null)
            {
                foreach (Transform part in board) part.gameObject.layer = FloorLayer;
                float across = 8 * 1.15f + .7f + .34f;
                var plinth = LastSceneArt.Part(board, "Plinth", LastSceneArt.RoundedBox(new Vector3(across, .16f, across), .06f, 6),
                                               LastSceneArt.Lit(0x150D08, .82f), new Vector3(0, .08f, 0), null, null, false);
                plinth.layer = FloorLayer;
            }

            // the cold window between the shelves at a third of its glow: out of focus it read as hard blue blocks
            var window = t.Find("Window");
            var windowLook = window != null ? window.GetComponent<Renderer>() : null;
            if (windowLook != null)
            {
                var dim = new Material(windowLook.sharedMaterial) { name = "study window dim" };
                dim.color = MenuArt.Hex(0x3A4A6A) * .35f;
                stageMaterials.Add(dim);
                windowLook.sharedMaterial = dim;
            }

            // a cool rim from behind on each side, low enough to catch the pawns' heads and shoulders
            for (int s = -1; s <= 1; s += 2)
                MenuArt.Spot(t, new Vector3(s * 7.5f, 5.2f, 9f), new Vector3(s * 2.2f, 1.2f, 0f), 0xA9C2EC, 1.1f, .42f, .8f);

            // two warm wall lamps between the shelves, out of focus behind the board
            for (int s = -1; s <= 1; s += 2)
            {
                var at = new Vector3(s * 4.8f, 5.6f, 10.35f);
                LastSceneArt.Part(t, "Wall Lamp Arm", LastSceneArt.Cylinder(.035f, .035f, .5f, 10), MenuArt.BrassMetal(), at + new Vector3(0, -.35f, .25f), null, null, false);
                LastSceneArt.Part(t, "Wall Lamp Bulb", LastSceneArt.Sphere(.14f), MenuArt.Lamp(0xFFD9A0), at, null, null, false);
                MenuArt.Halo(t, at, 0xFFB86A, 1.6f, .55f, camera);
                var glow = new GameObject("Wall Lamp Light").AddComponent<Light>();
                glow.transform.SetParent(t, false);
                glow.transform.localPosition = at + new Vector3(0, 0, -.4f);
                glow.type = LightType.Point;
                glow.color = MenuArt.Hex(0xFFB06A);
                glow.range = 4.5f;
                glow.intensity = GraphicsSettings.currentRenderPipeline == null ? 1.2f : 3.5f;
                glow.shadows = LightShadows.None;
            }

            StudyGloss(Texture.width, Texture.height, t);
            StudyGrade();
        }

        // The mirror under the board and the sheet that lays its picture on the squares.
        void StudyGloss(int width, int height, Transform t)
        {
            if (Resources.Load<Shader>(StageShaders + "StageFloor") == null) return;
            mirrorTexture = new RenderTexture(Mathf.Max(64, width / 2), Mathf.Max(64, height / 2), 16, RenderTextureFormat.ARGBHalf)
                { name = "Loading Study mirror", wrapMode = TextureWrapMode.Clamp };
            mirrorTexture.Create();
            var go = new GameObject("Study Mirror");
            go.transform.SetParent(t, false);
            mirror = go.AddComponent<Camera>();
            mirror.enabled = false;
            mirror.clearFlags = CameraClearFlags.SolidColor;
            mirror.backgroundColor = Color.black;
            mirror.cullingMask = ~((1 << FloorLayer) | (1 << 5) | (1 << LookLayer));
            mirror.allowHDR = true;
            mirror.allowMSAA = false;
            mirror.targetTexture = mirrorTexture;
#if CF_URP
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
#endif
            boardGloss = StageMaterial("StageFloor", "study board gloss");
            boardGloss.SetTexture("_ReflTex", mirrorTexture);
            boardGloss.SetVector("_Gloss", new Vector4(.12f, .6f, .004f, .4f));
            boardGloss.SetVector("_Tint", new Vector4(1f, .9f, .78f, 1f));
            // w 0: here the mirror's picture comes out upright, so it is read flipped (checked on a still of the mirror's
            // texture: without the flip the pawns' reflections fell on the far half of the board instead of at their feet)
            boardGloss.SetVector("_Hole", new Vector4(0, 0, 0, 0));
            var sheet = LastSceneArt.Part(t, "Board Gloss", LastSceneArt.Panel(8 * 1.15f, 8 * 1.15f), boardGloss,
                                          new Vector3(0, StageHeight + .004f, 0), Quaternion.Euler(90f, 0, 0), null, false);
            sheet.layer = FloorLayer;
            var renderer = sheet.GetComponent<Renderer>();
            if (renderer != null) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // The study's own grading, on a layer of its own so no scene's look reaches it.
        void StudyGrade()
        {
#if CF_URP
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.requiresDepthOption = CameraOverrideOption.On;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.volumeLayerMask = 1 << LookLayer;
            data.dithering = true;
            var go = new GameObject("Study Look") { layer = LookLayer };
            go.transform.SetParent(root.transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Study Look";
            var tone = profile.Add<Tonemapping>(false);
            tone.mode.Override(TonemappingMode.ACES);
            var grade = profile.Add<ColorAdjustments>(false);
            grade.postExposure.Override(.7f);
            grade.contrast.Override(10f);
            grade.colorFilter.Override(Color.white);
            grade.hueShift.Override(0f);
            grade.saturation.Override(4f);
            var bloom = profile.Add<Bloom>(false);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(.55f);
            bloom.scatter.Override(.7f);
            bloom.tint.Override(Color.white);
            bloom.highQualityFiltering.Override(true);
            bloom.dirtIntensity.Override(0f);
            // the shelves soften behind the board (the board's far edge is about 23 m from the camera, the shelves 29)
            var focus = profile.Add<DepthOfField>(false);
            focus.mode.Override(DepthOfFieldMode.Gaussian);
            focus.gaussianStart.Override(24f);
            focus.gaussianEnd.Override(31f);
            focus.gaussianMaxRadius.Override(1.1f);
            focus.highQualitySampling.Override(true);
            var vignette = profile.Add<Vignette>(false);
            vignette.color.Override(Color.black);
            vignette.center.Override(new Vector2(.5f, .55f));
            vignette.intensity.Override(.3f);
            vignette.smoothness.Override(.5f);
            vignette.rounded.Override(false);
            volume.sharedProfile = profile;
            lookProfile = profile;
#endif
        }

        // Around the camera's draw (LoadingStudio.Render), after the hall's settings: a darker, warmer room, and glossy
        // things no longer pick up the active scene's sky (the table read grey-blue with it).
        void StudyLighting()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = MenuArt.Hex(0x0B0705);
            RenderSettings.fogStartDistance = 26f;
            RenderSettings.fogEndDistance = 75f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = MenuArt.Hex(0x5A3E28) * .75f;
            RenderSettings.ambientEquatorColor = MenuArt.Hex(0x3E2818) * .7f;
            RenderSettings.ambientGroundColor = MenuArt.Hex(0x0A0604);
            RenderSettings.reflectionIntensity = .12f;
        }
    }
}
