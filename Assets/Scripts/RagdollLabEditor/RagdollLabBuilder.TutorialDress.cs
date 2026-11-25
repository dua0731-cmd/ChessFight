using ChessFight.Game;
using UnityEditor;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    // The tutorial map's look (R834, palace drill yard, ArtSource/Tutorial): the Blender map over the grey box.
    // The grey boxes keep their colliders and only stop drawing; every number the game checks stays the builder's.
    //   Assets/Art/Tutorial/Models/TU_Map.fbx      TU_Course, TU_Castle, TU_Grounds (already at the game's coordinates)
    //   Assets/Art/Tutorial/Models/TU_Sweeper.fbx  the spinning bar's look, hung under the Spinning Bar
    //   Assets/Art/Tutorial/Materials/TU_*.mat     made here (URP Lit) from Assets/Art/Tutorial/Textures
    // Without the FBX the scene stays the grey box (a warning says so).
    public static partial class RagdollLabBuilder
    {
        const string TutorialArtDir = "Assets/Art/Tutorial";
        const string TutorialMapModel = TutorialArtDir + "/Models/TU_Map.fbx";
        const string TutorialSweeperModel = TutorialArtDir + "/Models/TU_Sweeper.fbx";
        const string TutorialEagleModel = TutorialArtDir + "/Models/TU_Eagle.fbx";
        const string TutorialMaterialDir = TutorialArtDir + "/Materials";
        const string TutorialTextureDir = TutorialArtDir + "/Textures";
        const string MiniGameMaterialDir = "Assets/Resources/PawnRushMiniGames/Materials";   // the reused flower pot and cypress

        // name, base map (null = plain colour), colour, smoothness, metallic, normal map
        static readonly (string name, string tex, Color color, float smooth, float metal, string normal)[] TutorialLooks =
        {
            ("TU_CreamBlock", "TU_CreamBlock_BaseColor", Color.white, .2f, 0f, null),
            ("TU_Paving", "TU_Paving_BaseColor", Color.white, .42f, 0f, null),
            ("TU_MarbleCream", "TU_MarbleCream_BaseColor", Color.white, .6f, 0f, null),
            ("TU_MarbleWalnut", "TU_MarbleWalnut_BaseColor", Color.white, .6f, 0f, null),
            ("TU_Walnut", "TU_Walnut_BaseColor", Color.white, .45f, 0f, null),
            ("TU_MoatFloor", "TU_MoatFloor_BaseColor", Color.white, .1f, 0f, null),
            ("TU_Gold", null, new Color(.93f, .66f, .12f), .65f, .6f, null),
            ("TU_Purple", null, new Color(.38f, .08f, .45f), .5f, 0f, null),
            ("TU_CreamPaint", null, new Color(.95f, .88f, .78f), .5f, 0f, null),
            ("TU_Grass", "TU_Grass_BaseColor", new Color(.92f, .95f, .9f), .1f, 0f, null),
            ("TU_CreamBlockShade", "TU_CreamBlockShade_BaseColor", Color.white, .15f, 0f, null),
            ("TU_WetStone", "TU_WetStone_BaseColor", Color.white, .6f, 0f, null),
            ("TU_Bark", "TU_Bark_BaseColor", Color.white, .1f, 0f, "TU_Bark_Normal"),
            ("TU_Leaves", "TU_Leaves_BaseColor", Color.white, .2f, 0f, null),
            ("TU_GrassBlades", "TU_GrassBlades_BaseColor", new Color(.95f, 1f, .92f), .1f, 0f, null),
            ("TU_Road", "TU_Road_BaseColor", Color.white, .15f, 0f, null),
            ("TU_Rock", "TU_Rock_BaseColor", Color.white, .1f, 0f, null),
            ("TU_Kit_CastleKit", "TU_Kit_CastleKit_BaseColor", Color.white, .3f, 0f, "TU_Kit_CastleKit_Normal"),
            ("TU_Kit_GateKit", "TU_Kit_GateKit_BaseColor", Color.white, .3f, 0f, "TU_Kit_GateKit_Normal"),
            // The opening's eagle (TU_Eagle.fbx, ArtSource/Tutorial/tu_eagle_v1.py): one feather texture under three colours.
            ("TU_EagleBrown", "TU_EagleFeather_BaseColor", new Color32(0x8A, 0x3A, 0x1C, 255), .5f, 0f, "TU_EagleFeather_Normal"),
            ("TU_EagleChest", "TU_EagleFeather_BaseColor", new Color32(0xB0, 0x5A, 0x2C, 255), .5f, 0f, "TU_EagleFeather_Normal"),
            ("TU_EagleCream", "TU_EagleFeather_BaseColor", new Color32(0xF4, 0xE2, 0xB8, 255), .45f, 0f, "TU_EagleFeather_Normal"),
            ("TU_EagleEye", null, new Color32(0xC7, 0x78, 0x0F, 255), .85f, 0f, null),
            ("TU_EagleDark", null, new Color32(0x24, 0x12, 0x0A, 255), .8f, 0f, null),
        };

        static readonly string[] ReusedLooks = { "PR_A_TripoProps", "PR_Early_TripoProps", "PR_Early_Soil", "PR_Early_Leaf", "PR_Early_Petal", "PR_Early_FlowerHeart" };

        /// <summary>Stops the lab map's boxes (all of <paramref name="env"/>) and the hurdles drawing, and puts the map's look in their place.</summary>
        static void DressTutorial(Transform env, Transform props)
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-tutorialNoDress") >= 0)
            {
                Debug.Log("[Tutorial] -tutorialNoDress: 회색 상자 그대로.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TutorialMapModel) == null)
            {
                Debug.LogWarning("[Tutorial] " + TutorialMapModel + "이 없어 회색 상자 그대로 둡니다 (ArtSource/Tutorial/tu_export_unity_v1.py).");
                return;
            }
            PrepareTutorialArt();
            var map = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialMapModel);
            var sweeper = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialSweeperModel);

            foreach (var r in env.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            foreach (var r in props.GetComponentsInChildren<MeshRenderer>(true))
                if (r.name.StartsWith("Hurdle")) r.enabled = false;
            var bar = env.GetComponentInChildren<SpinningBar>(true);

            var look = (GameObject)PrefabUtility.InstantiatePrefab(map);
            look.name = "Map look (ArtSource/Tutorial)";
            look.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var c in look.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            GameObjectUtility.SetStaticEditorFlags(look, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            // The moat's water and the falls out of its wall (ChessFight.PawnRush.TutorialMoatWater, built at Play from the
            // Royal Carriage Yard's water; this assembly does not reference Pawn Rush's, so it is added by name).
            var waterType = System.Type.GetType("ChessFight.PawnRush.TutorialMoatWater, ChessFight.PawnRush");
            if (waterType != null)
            {
                var water = new GameObject("Moat water (TutorialMoatWater)").AddComponent(waterType);
                var so = new SerializedObject(water);
                so.FindProperty("sill").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(TutorialMaterialDir + "/TU_Gold.mat");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("[Tutorial] TutorialMoatWater가 없어 해자를 비워 둡니다.");

            if (bar != null && sweeper != null)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(sweeper, bar.transform);
                s.name = "Sweeper look";
                s.transform.localPosition = Vector3.zero;
                s.transform.localRotation = Quaternion.identity;
                foreach (var c in s.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }

            PlantTutorialTrees();

            // The opening: the eagle that flies the pawn in and crash-lands (TutorialIntro moves it; no colliders).
            var eagleModel = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialEagleModel);
            if (eagleModel != null)
            {
                var eagle = (GameObject)PrefabUtility.InstantiatePrefab(eagleModel);
                eagle.name = "Eagle (TutorialIntro)";
                eagle.transform.SetPositionAndRotation(new Vector3(0f, 60f, -120f), Quaternion.identity);
                foreach (var c in eagle.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                eagle.AddComponent<TutorialIntro>();
                string parts = "";
                foreach (Transform t in eagle.GetComponentsInChildren<Transform>(true))
                    if (t != eagle.transform) parts += $" {t.name.Replace("TU_Eagle_", "")}{eagle.transform.InverseTransformPoint(t.position):F2}";
                Debug.Log($"[Tutorial] 독수리를 놓았습니다 (크기 {eagle.transform.localScale.x:F2}):{parts}");
            }
            else Debug.LogWarning("[Tutorial] " + TutorialEagleModel + "이 없어 시작 연출이 없습니다 (ArtSource/Tutorial/tu_eagle_v1.py -- --export).");

            // Clear air (11-25 "너무 뿌옇게 됐어 난 아주 맑은 걸 원해"): the map test's fog pushed to the horizon, a light bloom.
            // JY-gfx is not used here (11-25 "패키지 파일 적용 하지 말자").
            new GameObject("Clear air (TutorialClearAir)").AddComponent<TutorialClearAir>();
            // A clear deep-blue sky of its own (11-25 "하늘이나 전체적으로 더 맑아 보이게"): Unity's procedural sky with a
            // thin atmosphere (deeper blue overhead, a pale clean horizon), a crisp sun; the ambient light a little brighter
            // and bluer from above, so shade reads clean instead of grey.
            string skyPath = TutorialMaterialDir + "/TU_Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            // 11-25 "구름 그래픽 좋게 해줘": the panorama of path-traced cumulus (ArtSource/Tutorial/tu_sky_v1.py, its sun where
            // the scene's light is) on Skybox/Panoramic; without it, the clear procedural sky above.
            var panoramaPath = TutorialTextureDir + "/TU_SkyClouds.exr";
            if (AssetImporter.GetAtPath(panoramaPath) is TextureImporter pi && (pi.mipmapEnabled || pi.wrapModeV != TextureWrapMode.Clamp || pi.maxTextureSize < 4096))
            {
                pi.mipmapEnabled = false; pi.wrapModeU = TextureWrapMode.Repeat; pi.wrapModeV = TextureWrapMode.Clamp;
                pi.maxTextureSize = 4096; pi.textureCompression = TextureImporterCompression.CompressedHQ;
                pi.SaveAndReimport();
            }
            var panorama = AssetDatabase.LoadAssetAtPath<Texture2D>(panoramaPath);
            var skyShader = Shader.Find(panorama != null ? "Skybox/Panoramic" : "Skybox/Procedural");
            if (sky == null && skyShader != null) { sky = new Material(skyShader); AssetDatabase.CreateAsset(sky, skyPath); }
            if (sky != null)
            {
                sky.shader = skyShader;
                if (panorama != null)
                {
                    sky.SetTexture("_MainTex", panorama);
                    sky.SetFloat("_Mapping", 1f);           // latitude-longitude
                    sky.SetFloat("_ImageType", 0f);         // 360
                    sky.SetFloat("_Rotation", 0f);
                    sky.SetColor("_Tint", new Color(.5f, .5f, .5f));
                    sky.SetFloat("_Exposure", .72f);            // tu_sky_grade_v1: the horizon sits near 1.6 before this
                }
                else
                {
                    sky.SetFloat("_SunDisk", 2f);
                    sky.SetFloat("_SunSize", .035f);
                    sky.SetFloat("_SunSizeConvergence", 6f);
                    sky.SetFloat("_AtmosphereThickness", .62f);
                    sky.SetColor("_SkyTint", new Color(.30f, .52f, 1f));
                    sky.SetColor("_GroundColor", new Color(.36f, .46f, .30f));
                    sky.SetFloat("_Exposure", 1.2f);
                }
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.56f, .70f, .95f);
                RenderSettings.ambientEquatorColor = new Color(.56f, .62f, .66f);
                RenderSettings.ambientGroundColor = new Color(.34f, .36f, .30f);
            }
            var grassType = System.Type.GetType("ChessFight.PawnRush.TutorialMeadowGrass, ChessFight.PawnRush");
            if (grassType != null)
            {
                var grass = new GameObject("Meadow grass (TutorialMeadowGrass)").AddComponent(grassType);
                var gs = new SerializedObject(grass);
                gs.FindProperty("tufts").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(TutorialArtDir + "/Models/TU_GrassTufts.txt");
                gs.FindProperty("material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(TutorialMaterialDir + "/TU_GrassBlades.mat");
                gs.ApplyModifiedPropertiesWithoutUndo();
            }

            // The FBX is made at the game's coordinates: the dressed course spans the floors (x -40..35, z -37..32).
            var course = look.transform.Find("TU_Course");
            if (course != null && course.TryGetComponent<Renderer>(out var cr))
            {
                var b = cr.bounds;
                bool ok = Mathf.Abs(b.min.x + 40f) < 1.5f && Mathf.Abs(b.max.x - 35f) < 1.5f && Mathf.Abs(b.min.z + 37f) < 1.5f && Mathf.Abs(b.max.z - 32f) < 2f;
                if (ok) Debug.Log($"[Tutorial] 맵 겉모습 위치 맞음: {b.min} ~ {b.max}");
                else Debug.LogError($"[Tutorial] 맵 겉모습이 판정 상자와 어긋남(뒤집힘?): {b.min} ~ {b.max}");
            }
        }

        static void PrepareTutorialArt()
        {
            EnsureTutorialFolder(TutorialMaterialDir);
            foreach (var look in TutorialLooks)
            {
                if (look.normal != null) SetTextureKind(look.normal, true);
                if (look.tex != null) SetTextureKind(look.tex, false);
            }

            var remap = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var look in TutorialLooks)
            {
                string path = $"{TutorialMaterialDir}/{look.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(LitMaterials.Lit);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = LitMaterials.Lit;
                mat.color = look.color;
                mat.mainTexture = look.tex == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{TutorialTextureDir}/{look.tex}.png");
                if (look.tex != null && mat.mainTexture == null) Debug.LogWarning("[Tutorial] 텍스처 없음: " + look.tex);
                LitMaterials.SetSmoothness(mat, look.smooth);
                LitMaterials.SetMetallic(mat, look.metal);
                var normal = look.normal == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>($"{TutorialTextureDir}/{look.normal}.png");
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
                if (normal != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
                if (look.name == "TU_Leaves" || look.name == "TU_GrassBlades")
                {
                    // leaf cards: cut out by the texture's alpha, both faces drawn (their normals point out of the crown)
                    mat.SetFloat("_AlphaClip", 1f); mat.SetFloat("_Cutoff", .42f); mat.EnableKeyword("_ALPHATEST_ON");
                    mat.SetFloat("_Cull", 0f);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }
                if (look.name == "TU_Leaves" || look.name == "TU_Bark" || look.name == "TU_GrassBlades") mat.enableInstancing = true;
                if (look.name == "TU_Grass")
                {
                    // the meadow: its own shader (Assets/Art/Tutorial/Shaders/TU_Meadow.shader), no tile repeats, broad tint patches
                    var meadow = AssetDatabase.LoadAssetAtPath<Shader>(TutorialArtDir + "/Shaders/TU_Meadow.shader");
                    if (meadow != null)
                    {
                        mat.shader = meadow;
                        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TutorialTextureDir}/TU_Grass_BaseColor.png"));
                        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TutorialTextureDir}/TU_Grass_Normal.png"));
                        mat.SetTexture("_MacroMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TutorialTextureDir}/TU_MeadowMacro.png"));
                        mat.SetColor("_BaseColor", new Color(.9f, .93f, .86f));
                    }
                    else Debug.LogWarning("[Tutorial] TU_Meadow.shader가 없어 잔디는 Lit 그대로.");
                }
                EditorUtility.SetDirty(mat);
                remap[look.name] = mat;
            }
            foreach (var name in ReusedLooks)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MiniGameMaterialDir}/{name}.mat");
                if (mat != null) remap[name] = mat;
                else Debug.LogWarning("[Tutorial] 재사용 재질 없음: " + name);
            }
            AssetDatabase.SaveAssets();

            foreach (var cut in new[] { "TU_Leaves_BaseColor", "TU_GrassBlades_BaseColor" })
                if (AssetImporter.GetAtPath($"{TutorialTextureDir}/{cut}.png") is TextureImporter leafTex && !leafTex.alphaIsTransparency)
                {
                    leafTex.alphaIsTransparency = true; leafTex.mipMapsPreserveCoverage = true; leafTex.alphaTestReferenceValue = .42f;
                    leafTex.SaveAndReimport();
                }
            SetTextureKind("TU_Grass_Normal", true);
            SetTextureKind("TU_MeadowMacro", false);
            var models = new System.Collections.Generic.List<string> { TutorialMapModel, TutorialSweeperModel, TutorialEagleModel };
            foreach (var v in TutorialTreeKinds) models.Add($"{TutorialArtDir}/Models/TU_{v}.fbx");
            foreach (var model in models)
            {
                if (!(AssetImporter.GetAtPath(model) is ModelImporter mi)) continue;
                bool changed = false;
                if (mi.importCameras || mi.importLights || mi.addCollider || mi.importAnimation || mi.isReadable)
                {
                    mi.importCameras = false; mi.importLights = false; mi.addCollider = false; mi.importAnimation = false; mi.isReadable = false;
                    changed = true;
                }
                if (mi.indexFormat != ModelImporterIndexFormat.UInt32) { mi.indexFormat = ModelImporterIndexFormat.UInt32; changed = true; }
                var have = mi.GetExternalObjectMap();
                foreach (var kv in remap)
                {
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
                    if (have.TryGetValue(id, out var cur) && cur == kv.Value) continue;
                    mi.AddRemap(id, kv.Value); changed = true;
                }
                if (changed) mi.SaveAndReimport();
            }
        }

        static readonly string[] TutorialTreeKinds = { "TreeA", "TreeB", "TreeC", "Bush" };

        // The grown trees and bushes outside the walls (ArtSource/Tutorial/tu_trees_v1.py): one FBX per kind with _LOD0
        // and _LOD1 (Unity's importer makes the LOD group from the names), planted where TU_TreePlacements.txt says
        // (variant, x, y, z, Blender yaw, scale). The FBX keeps Blender's axes, so each tree turns 180 degrees more.
        static void PlantTutorialTrees()
        {
            var list = AssetDatabase.LoadAssetAtPath<TextAsset>(TutorialArtDir + "/Models/TU_TreePlacements.txt");
            if (list == null) { Debug.LogWarning("[Tutorial] TU_TreePlacements.txt가 없어 나무를 심지 않습니다."); return; }
            var kinds = new System.Collections.Generic.Dictionary<string, GameObject>();
            var root = new GameObject("Trees (ArtSource/Tutorial)").transform;
            int planted = 0;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var raw in list.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split(',');
                if (p.Length < 6) continue;
                if (!kinds.TryGetValue(p[0], out var model))
                    kinds[p[0]] = model = AssetDatabase.LoadAssetAtPath<GameObject>($"{TutorialArtDir}/Models/TU_{p[0]}.fbx");
                if (model == null) continue;
                float F(int i) => float.Parse(p[i], inv);
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
                tree.transform.SetPositionAndRotation(new Vector3(F(1), F(2), F(3)), Quaternion.Euler(0f, 180f - F(4), 0f));
                tree.transform.localScale = Vector3.one * F(5);
                if (tree.TryGetComponent<LODGroup>(out var lod))
                {
                    var lods = lod.GetLODs();
                    if (lods.Length >= 2)
                    {
                        lods[0].screenRelativeTransitionHeight = .07f;
                        lods[1].screenRelativeTransitionHeight = .008f;
                        lod.SetLODs(lods);
                    }
                }
                planted++;
            }
            Debug.Log($"[Tutorial] 나무 · 덤불 {planted}그루를 심었습니다.");
        }

        static void SetTextureKind(string name, bool normal)
        {
            string path = $"{TutorialTextureDir}/{name}.png";
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) { Debug.LogWarning("[Tutorial] 텍스처 없음: " + path); return; }
            var want = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            // crisp at a slant and far off (11-25 "바닥도 아주 맑아 보이게"): 16x anisotropic, Kaiser mips
            if (ti.textureType == want && ti.maxTextureSize == 2048 && ti.anisoLevel == 16 && ti.mipmapFilter == TextureImporterMipFilter.KaiserFilter) return;
            ti.textureType = want;
            ti.maxTextureSize = 2048;
            ti.anisoLevel = 16;
            ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            ti.SaveAndReimport();
        }

        static void EnsureTutorialFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureTutorialFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
