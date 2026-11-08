#if CF_URP
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChessFight.PawnRush.Editor
{
    /// <summary>
    /// R502 step 2 (the drawbridge remake, one user check per step): the phase-1 look on its own, before any
    /// mini-game uses it. Builds Assets/Scenes/SeoungTest/R502_EarlyLookTest.unity: a floor and walls of the stone
    /// block module (the Tripo block shape dressed in the Tripo tower's stone, ArtSource/PawnRush/MiniGames/Early/
    /// Materials), a 4 m moat of the new moat water, a 1 m pawn for scale, a warm sun with soft shadows and the
    /// phase-1 volume (warm white balance, a little exposure and saturation, soft bloom; SSAO is the PC renderer's).
    /// The materials and the volume profile are made once under Resources/PawnRushMiniGames/Early/R502 and then kept,
    /// so values tuned in Unity survive a rebuild; the scene itself is regenerated every time.
    /// </summary>
    public static class EarlyLookTestMenu
    {
        const string Root = "Assets/Resources/PawnRushMiniGames/Early/R502/";
        const string ScenePath = "Assets/Scenes/SeoungTest/R502_EarlyLookTest.unity";
        const string StoneMat = Root + "PR_Early_StoneBlock.mat";
        const string WaterMat = Root + "PR_Early_MoatWater.mat";
        const string LookPath = Root + "PR_Early_Look.asset";
        const string WaterShader = "ChessFight/Pawn Rush Moat Water";

        // the module (PR_Early_StoneBlock.fbx): 1 m wide, 0.74 m deep, 1.093 m tall; front face at z = 0 facing -z
        const float W = 1f, D = .74f, H = 1.093f;
        const float MoatWidth = 4f, WaterLevel = -.35f, MoatBottom = -1.2f;

        [MenuItem("ChessFight/Pawn Rush/Development/R502 Early Look Test", false, 300)]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var module = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "PR_Early_StoneBlock.fbx");
            if (module == null)
            {
                EditorUtility.DisplayDialog("R502 Early Look Test", "모듈 FBX가 없습니다: " + Root + "PR_Early_StoneBlock.fbx\nArtSource/PawnRush/MiniGames/Early/Materials/r502_export_unity.py 를 먼저 실행하세요.", "확인");
                return;
            }
            var stone = StoneMaterial();
            var water = WaterMaterial();
            var look = LookProfile();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Lighting(look);
            var root = new GameObject("R502 Early Look Test").transform;

            // south ground (where the pawn stands), the moat, north ground, a back wall
            var floor = new GameObject("Floor").transform;
            floor.SetParent(root, false);
            for (int row = 0; row < 6; row++)
                for (int i = 0; i < 8; i++)
                    FloorModule(module, stone, floor, new Vector3(-3.5f + i, 0f, -(row + 1) * H));
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 8; i++)
                    FloorModule(module, stone, floor, new Vector3(-3.5f + i, 0f, MoatWidth + row * H));
            var moat = new GameObject("Moat").transform;
            moat.SetParent(root, false);
            for (int row = 0; row < 4; row++)
                for (int i = 0; i < 8; i++)
                    FloorModule(module, stone, moat, new Vector3(-3.5f + i, MoatBottom, row * H));
            for (int i = 0; i < 8; i++)
            {
                // the moat's two stone banks face the water: south bank looks north, north bank looks south
                Module(module, stone, moat, new Vector3(-3.5f + i, MoatBottom, 0f), 180f, (i & 1) == 0);
                Module(module, stone, moat, new Vector3(-3.5f + i, MoatBottom, MoatWidth), 0f, (i & 1) == 1);
            }
            // one block standing in the water, for the foam ring
            Module(module, stone, moat, new Vector3(1.5f, MoatBottom, 1.6f), 90f, false);
            var surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = "Moat water";
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetParent(moat, false);
            surface.transform.localPosition = new Vector3(0f, WaterLevel, MoatWidth / 2f);
            surface.transform.localScale = new Vector3(.8f, 1f, MoatWidth / 10f);
            var mr = surface.GetComponent<MeshRenderer>();
            mr.sharedMaterial = water;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var wall = new GameObject("Back wall").transform;
            wall.SetParent(root, false);
            float back = MoatWidth + 3 * H;
            for (int course = 0; course < 2; course++)
                for (int i = 0; i < 8; i++)
                    Module(module, stone, wall, new Vector3(-3.5f + i, course * H, back), 0f, ((i + course) % 3) == 0);

            Pawn(root, new Vector3(.6f, 0f, -1.6f));
            Camera(new Vector3(5.2f, 3.4f, -7.4f), new Vector3(0f, -.2f, 1.8f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[R502] 1단계 룩 시험 장면을 만들었습니다: " + ScenePath + " (재질·프로필: " + Root + ")");
        }

        // A module standing as a wall: front face toward -z turned by `yaw`. `flip` turns it round (its back is
        // bricked too), so the joints of neighbouring modules do not line up into a grid.
        static void Module(GameObject module, Material mat, Transform parent, Vector3 at, float yaw, bool flip)
        {
            var place = new GameObject("Module").transform;
            place.SetParent(parent, false);
            place.localPosition = at;
            place.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Hang(module, mat, place, flip ? new Vector3(0f, 0f, D) : Vector3.zero, flip ? 180f : 0f);
        }

        // A module laid face up: its front face is the floor at `at`.y, its body below, its 1.093 m length along +z.
        static void FloorModule(GameObject module, Material mat, Transform parent, Vector3 at)
        {
            var place = new GameObject("Floor module").transform;
            place.SetParent(parent, false);
            place.localPosition = at;
            place.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Hang(module, mat, place, Vector3.zero, 0f);
        }

        // As MiniGameModels: the FBX under a pivot turned 180 degrees about y (Blender x, y, z = Unity x, z, y).
        static void Hang(GameObject module, Material mat, Transform place, Vector3 offset, float turn)
        {
            var turnPivot = new GameObject("turn").transform;
            turnPivot.SetParent(place, false);
            turnPivot.localPosition = offset;
            turnPivot.localRotation = Quaternion.Euler(0f, turn, 0f);
            var pivot = new GameObject("pivot").transform;
            pivot.SetParent(turnPivot, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(module, pivot);
            foreach (var r in copy.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                r.sharedMaterials = slots;
            }
        }

        static Material StoneMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(StoneMat);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PR_Early_StoneBlock" };
            mat.SetTexture("_BaseMap", Tex("PR_Early_StoneBlock_BaseColor"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", Tex("PR_Early_StoneBlock_Normal"));
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetTexture("_OcclusionMap", Tex("PR_Early_StoneBlock_AO"));
            mat.SetFloat("_OcclusionStrength", 1f);
            mat.EnableKeyword("_OCCLUSIONMAP");
            mat.SetTexture("_MetallicGlossMap", Tex("PR_Early_StoneBlock_MetallicSmoothness"));
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            AssetDatabase.CreateAsset(mat, StoneMat);
            return mat;
        }

        static Material WaterMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMat);
            if (mat != null) return mat;
            var shader = Shader.Find(WaterShader);
            if (shader == null) throw new System.InvalidOperationException("Missing shader: " + WaterShader);
            mat = new Material(shader) { name = "PR_Early_MoatWater" };
            mat.SetTexture("_WaveMap", Tex("PR_Early_Water_Normal"));
            mat.SetTexture("_CausticMap", Tex("PR_Early_Water_Caustic"));
            AssetDatabase.CreateAsset(mat, WaterMat);
            return mat;
        }

        static Texture2D Tex(string name)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/" + name + ".png");
            if (t == null) Debug.LogWarning("[R502] 텍스처가 없습니다: " + Root + "Textures/" + name + ".png");
            return t;
        }

        // Phase 1 = clear midday outside the palace (R499): warm, bright, gently saturated, soft bloom on the water.
        static VolumeProfile LookProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookPath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, LookPath);
            var grade = profile.Add<ColorAdjustments>(false);
            grade.postExposure.Override(.15f);
            grade.contrast.Override(8f);
            grade.saturation.Override(12f);
            grade.colorFilter.Override(new Color(1f, .975f, .94f));
            var white = profile.Add<WhiteBalance>(false);
            white.temperature.Override(10f);
            var bloom = profile.Add<Bloom>(false);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(.25f);
            bloom.scatter.Override(.6f);
            var vignette = profile.Add<Vignette>(false);
            vignette.intensity.Override(.06f);
            foreach (var component in profile.components)
            {
                component.name = component.GetType().Name;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void Lighting(VolumeProfile look)
        {
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, .93f, .81f);
            sun.intensity = 1.9f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .82f;
            sunGo.transform.rotation = Quaternion.Euler(46f, 28f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.66f, .76f, .90f);
            RenderSettings.ambientEquatorColor = new Color(.74f, .69f, .62f);
            RenderSettings.ambientGroundColor = new Color(.42f, .35f, .28f);
            RenderSettings.fog = false;
            var volumeGo = new GameObject("Phase 1 look (volume)");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;   // over GameLook's scene-wide soft volume (priority 10)
            volume.sharedProfile = look;
        }

        static void Pawn(Transform root, Vector3 at)
        {
            var prefab = Resources.Load<GameObject>("PieceModels/PawnRush/Pawn");
            if (prefab == null) return;
            var pawn = Object.Instantiate(prefab, root);
            pawn.name = "Pawn (1 m, scale only)";
            // a picture for scale only: no scripts, physics or colliders (the test floor has none)
            foreach (var b in pawn.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(b);
            foreach (var j in pawn.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j);
            foreach (var rb in pawn.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var c in pawn.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var bounds = new Bounds();
            bool any = false;
            foreach (var r in pawn.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (any && bounds.size.y > .01f) pawn.transform.localScale *= 1f / bounds.size.y;
            pawn.transform.position = at;
            if (any)
            {
                float bottom = float.MaxValue;
                foreach (var r in pawn.GetComponentsInChildren<Renderer>(true)) bottom = Mathf.Min(bottom, r.bounds.min.y);
                pawn.transform.position += Vector3.up * (at.y - bottom);
            }
        }

        static void Camera(Vector3 at, Vector3 target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.63f, .79f, .95f);
            cam.fieldOfView = 45f;
            go.transform.position = at;
            go.transform.LookAt(target);
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;
        }
    }
}
#endif
