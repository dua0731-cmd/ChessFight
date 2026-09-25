using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>
    /// Builds the ragdoll lab from Pawn.fbx: re-weights the Mixamo skin per body region, creates the
    /// 11-body physics rig + puppet, and lays out the six test fixtures from the spec with primitives.
    /// Menu: ChessFight/Ragdoll Lab/Rebuild Pawn + Scene.
    /// </summary>
    public static class RagdollLabBuilder
    {
        const string Root = "Assets/ChessFight/RagdollLab";
        const string FbxPath = Root + "/Art/Pawn/Pawn.fbx";
        const string TexturePath = Root + "/Art/Pawn/PawnAlbedo.jpg";
        const string GeneratedDir = Root + "/Generated";
        const string BoxDir = GeneratedDir + "/Boxes";
        const string MaterialDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string PrefabPath = PrefabDir + "/RagdollPawn.prefab";
        const string SettingsDir = Root + "/Settings";
        const string TuningPath = SettingsDir + "/RagdollTuning.asset";
        const string SceneDir = Root + "/Scenes";
        public const string ScenePath = SceneDir + "/RagdollLab.unity";

        static readonly string[] BodyNames = { "Hips", "Chest", "Head", "Arm_L", "Hand_L", "Arm_R", "Hand_R", "Thigh_L", "Foot_L", "Thigh_R", "Foot_R" };
        static readonly float[] BodyMass = { 16f, 12f, 6f, 1.2f, 1.3f, 1.2f, 1.3f, 3f, 2.5f, 3f, 2.5f };

        class Materials
        {
            public Material pawnP1, pawnP2, pawnDummy, floor, wall, slope, beam, hazard, post, low;
        }

        [MenuItem("ChessFight/Ragdoll Lab/Rebuild Pawn + Scene")]
        public static void RebuildAll()
        {
            EnsureFolders();
            ConfigureImports();
            var materials = BuildMaterials();
            var tuning = BuildTuning();
            var prefab = BuildPawnPrefab(materials, tuning);
            BuildScene(prefab, tuning, materials);
            AssetDatabase.SaveAssets();
            Debug.Log("[RagdollLab] Rebuild complete: " + ScenePath);
        }

        [MenuItem("ChessFight/Ragdoll Lab/Build Windows Player")]
        public static void BuildPlayer()
        {
            string output = Arg("-labOut") ?? Path.GetFullPath("Builds/RagdollLab");
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(output, "RagdollLab.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Player build failed: " + report.summary.result);
            // Steam only initialises from a build if it is launched through Steam or finds this file.
            string appId = Path.GetFullPath("steam_appid.txt");
            if (File.Exists(appId)) File.Copy(appId, Path.Combine(output, "steam_appid.txt"), true);
            else Debug.LogWarning("[RagdollLab] steam_appid.txt not found at the project root; the build cannot start Steam.");
            Debug.Log("[RagdollLab] Player built: " + output);
        }

        public static void RebuildAllBatch() => RunBatch(RebuildAll);

        public static void BuildPlayerBatch() => RunBatch(BuildPlayer);

        public static void RebuildAndBuildBatch() => RunBatch(() =>
        {
            RebuildAll();
            BuildPlayer();
        });

        static void RunBatch(Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        static void EnsureFolders()
        {
            foreach (var dir in new[] { GeneratedDir, BoxDir, MaterialDir, PrefabDir, SettingsDir, SceneDir })
            {
                if (AssetDatabase.IsValidFolder(dir)) continue;
                string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
            }
        }

        static void ConfigureImports()
        {
            var model = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            if (model == null) throw new Exception("Missing " + FbxPath);
            model.animationType = ModelImporterAnimationType.Generic;
            model.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            model.importAnimation = false;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.isReadable = true;
            model.importBlendShapes = false;
            model.importCameras = false;
            model.importLights = false;
            model.importVisibility = false;
            model.optimizeGameObjects = false;
            model.meshCompression = ModelImporterMeshCompression.Off;
            model.SaveAndReimport();

            var texture = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            texture.sRGBTexture = true;
            texture.maxTextureSize = 2048;
            texture.textureCompression = TextureImporterCompression.CompressedHQ;
            texture.SaveAndReimport();
        }

        // ------------------------------------------------------------------ materials

        static Materials BuildMaterials()
        {
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var redCollar = RecolorCollar(0f, GeneratedDir + "/PawnAlbedo_RedCollar.png");
            var grid = GridTexture(GeneratedDir + "/Grid.png");
            return new Materials
            {
                pawnP1 = Mat("Pawn_P1", Color.white, albedo, 0.25f),
                pawnP2 = Mat("Pawn_P2", Color.white, redCollar, 0.25f),
                pawnDummy = Mat("Pawn_Dummy", new Color(0.6f, 0.6f, 0.62f), albedo, 0.15f),
                floor = Mat("Lab_Floor", new Color(0.88f, 0.88f, 0.86f), grid, 0.05f),
                wall = Mat("Lab_Wall", new Color(0.64f, 0.74f, 0.9f), grid, 0.05f),
                slope = Mat("Lab_Slope", new Color(0.66f, 0.84f, 0.62f), grid, 0.05f),
                beam = Mat("Lab_Beam", new Color(0.9f, 0.64f, 0.38f), grid, 0.05f),
                hazard = Mat("Lab_Hazard", new Color(0.93f, 0.3f, 0.24f), null, 0.35f),
                post = Mat("Lab_Post", new Color(0.34f, 0.35f, 0.38f), null, 0.2f),
                low = Mat("Lab_Low", new Color(0.97f, 0.8f, 0.28f), grid, 0.1f),
            };
        }

        static Material Mat(string name, Color color, Texture texture, float gloss)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var shader = Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.color = color;
            mat.mainTexture = texture;
            mat.SetFloat("_Glossiness", gloss);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D RecolorCollar(float hue, string path)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(TexturePath));
            var pixels = source.GetPixels32();
            int changed = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                Color.RGBToHSV(c, out float h, out float s, out float v);
                if (s < 0.3f || h < 0.52f || h > 0.75f) continue;
                Color n = Color.HSVToRGB(hue, Mathf.Min(1f, s * 1.1f), v);
                n.a = 1f;
                pixels[i] = n;
                changed++;
            }
            source.SetPixels32(pixels);
            source.Apply();
            File.WriteAllBytes(path, source.EncodeToPNG());
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(path);
            Debug.Log($"[RagdollLab] Recolored {changed} collar pixels -> {path}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D GridTexture(string path)
        {
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool light = (x / 64 + y / 64) % 2 == 0;
                float v = light ? 1f : 0.9f;
                if (x % 64 < 2 || y % 64 < 2) v = 0.74f;
                t.SetPixel(x, y, new Color(v, v, v, 1f));
            }
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static RagdollTuning BuildTuning()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<RagdollTuning>(TuningPath);
            if (tuning != null) return tuning; // keep values already found in play-testing
            tuning = ScriptableObject.CreateInstance<RagdollTuning>();
            AssetDatabase.CreateAsset(tuning, TuningPath);
            return tuning;
        }

        // ------------------------------------------------------------------ pawn

        static RagdollPawn BuildPawnPrefab(Materials materials, RagdollTuning tuning)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var root = new GameObject("RagdollPawn");
            try
            {
                var visual = (GameObject)Object.Instantiate(model, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                foreach (var animator in visual.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);

                var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var boneMap = new Dictionary<string, Transform>();
                foreach (var t in visual.GetComponentsInChildren<Transform>(true))
                    if (!boneMap.ContainsKey(t.name)) boneMap[t.name] = t;
                Transform Bone(string name)
                {
                    if (!boneMap.TryGetValue("mixamorig:" + name, out var t)) throw new Exception("Bone not found: mixamorig:" + name);
                    return t;
                }

                ApplyBindPose(skin);
                if (Bone("LeftToeBase").position.z < Bone("LeftFoot").position.z)
                    visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                Mesh source = skin.sharedMesh;
                Vector3[] local = source.vertices;
                int n = local.Length;
                var world = new Vector3[n];
                Matrix4x4 m = skin.transform.localToWorldMatrix;
                for (int i = 0; i < n; i++) world[i] = m.MultiplyPoint3x4(local[i]);
                float minY = world.Min(p => p.y), maxY = world.Max(p => p.y);
                float height = maxY - minY;
                if (height < 0.5f || height > 2f) throw new Exception($"Unexpected pawn height {height:F3} m; check FBX scale");
                visual.transform.position -= Vector3.up * minY;
                m = skin.transform.localToWorldMatrix;
                for (int i = 0; i < n; i++) world[i] = m.MultiplyPoint3x4(local[i]);
                Debug.Log($"[RagdollLab] Pawn height {height:F3} m, {n} vertices, {skin.bones.Length} bones");

                // ---- connected components (welded by position)
                var parent = new int[n];
                for (int i = 0; i < n; i++) parent[i] = i;
                int Find(int x)
                {
                    while (parent[x] != x)
                    {
                        parent[x] = parent[parent[x]];
                        x = parent[x];
                    }
                    return x;
                }
                void Union(int a, int b)
                {
                    a = Find(a);
                    b = Find(b);
                    if (a != b) parent[a] = b;
                }
                var weld = new Dictionary<Vector3Int, int>();
                for (int i = 0; i < n; i++)
                {
                    var key = Vector3Int.RoundToInt(local[i] * 20000f);
                    if (weld.TryGetValue(key, out int j)) Union(i, j);
                    else weld[key] = i;
                }
                int[] triangles = source.triangles;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Union(triangles[t], triangles[t + 1]);
                    Union(triangles[t + 1], triangles[t + 2]);
                }
                var groups = new Dictionary<int, List<int>>();
                for (int i = 0; i < n; i++)
                {
                    int r = Find(i);
                    if (!groups.TryGetValue(r, out var list)) groups[r] = list = new List<int>();
                    list.Add(i);
                }
                var comps = groups.Values.OrderByDescending(l => l.Count).ToList();
                var compOf = new int[n];
                for (int c = 0; c < comps.Count; c++)
                    foreach (int v in comps[c]) compOf[v] = c;
                var compBounds = comps.Select(l => BoundsOf(l.Select(i => world[i]))).ToList();

                float leftSign = Mathf.Sign(Bone("LeftHand").position.x);
                // 0 = main body, 1 = hand, 2 = arm stub, 3 = face detail
                var kind = new int[comps.Count];
                for (int c = 1; c < comps.Count; c++)
                {
                    Vector3 center = compBounds[c].center;
                    float ax = Mathf.Abs(center.x);
                    kind[c] = ax > 0.27f * height ? 1 : ax > 0.13f * height && center.y > 0.4f * height && center.y < 0.65f * height ? 2 : 3;
                }
                for (int c = 0; c < comps.Count; c++)
                    Debug.Log($"[RagdollLab] component {c}: {comps[c].Count} verts, kind {kind[c]}, center {compBounds[c].center:F3}, size {compBounds[c].size:F3}");
                int CompOfKind(int k, bool left)
                {
                    for (int c = 1; c < comps.Count; c++)
                        if (kind[c] == k && (Mathf.Sign(compBounds[c].center.x) == leftSign) == left) return c;
                    throw new Exception($"Pawn part kind {k} ({(left ? "left" : "right")}) not found");
                }
                int stubL = CompOfKind(2, true), stubR = CompOfKind(2, false);
                int handCompL = CompOfKind(1, true), handCompR = CompOfKind(1, false);

                // ---- skin weights by region
                int Index(string name)
                {
                    int idx = Array.IndexOf(skin.bones, Bone(name));
                    if (idx < 0) throw new Exception("Bone not skinned: " + name);
                    return idx;
                }
                int iHips = Index("Hips"), iSpine = Index("Spine"), iSpine1 = Index("Spine1"), iSpine2 = Index("Spine2"), iHead = Index("Head");
                int iArmL = Index("LeftArm"), iHandL = Index("LeftHand"), iArmR = Index("RightArm"), iHandR = Index("RightHand");
                int iFootL = Index("LeftFoot"), iFootR = Index("RightFoot");
                float yHips = Bone("Hips").position.y, ySpine = Bone("Spine").position.y;
                float ySpine1 = Bone("Spine1").position.y, ySpine2 = Bone("Spine2").position.y;
                float yNeck = Bone("Neck").position.y, yHead = Bone("Head").position.y;
                float footTop = 0.12f * height;
                float[] chainY = { yHips + 0.03f, ySpine + 0.03f, ySpine1 + 0.03f, ySpine2 + 0.03f };
                int[] chainBone = { iHips, iSpine, iSpine1, iSpine2 };

                var weights = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                {
                    Vector3 p = world[i];
                    int c = compOf[i];
                    bool left = Mathf.Sign(p.x) == leftSign;
                    if (c == 0)
                    {
                        float radius = new Vector2(p.x, p.z).magnitude;
                        if (p.y < footTop && Mathf.Abs(p.x) > 0.035f * height) weights[i] = One(left ? iFootL : iFootR);
                        else if (p.y >= yHead || (p.y > yNeck - 0.025f * height && radius < 0.13f * height)) weights[i] = One(iHead);
                        else weights[i] = Chain(p.y, chainY, chainBone);
                    }
                    else if (kind[c] == 1) weights[i] = One(c == handCompL ? iHandL : iHandR);
                    else if (kind[c] == 2) weights[i] = One(c == stubL ? iArmL : iArmR);
                    else weights[i] = One(iHead);
                }
                var skinned = Object.Instantiate(source);
                skinned.name = "PawnSkinned";
                skinned.boneWeights = weights;
                skinned = SaveAsset(skinned, GeneratedDir + "/PawnSkinned.asset");
                skin.sharedMesh = skinned;
                skin.sharedMaterial = materials.pawnP1;
                skin.updateWhenOffscreen = true;
                skin.rootBone = Bone("Hips");

                // ---- body pivots
                var pivots = new Vector3[RagdollPawn.Count];
                pivots[(int)BodyId.Hips] = Bone("Hips").position;
                pivots[(int)BodyId.Chest] = Bone("Spine").position;
                pivots[(int)BodyId.Head] = Bone("Neck").position;
                Bounds stubBoundsL = compBounds[stubL], stubBoundsR = compBounds[stubR];
                pivots[(int)BodyId.ArmL] = StubPivot(stubBoundsL, inner: true);
                pivots[(int)BodyId.ArmR] = StubPivot(stubBoundsR, inner: true);
                pivots[(int)BodyId.HandL] = StubPivot(stubBoundsL, inner: false);
                pivots[(int)BodyId.HandR] = StubPivot(stubBoundsR, inner: false);
                pivots[(int)BodyId.ThighL] = Bone("LeftUpLeg").position;
                pivots[(int)BodyId.ThighR] = Bone("RightUpLeg").position;
                pivots[(int)BodyId.FootL] = Bone("LeftFoot").position;
                pivots[(int)BodyId.FootR] = Bone("RightFoot").position;

                // ---- physics bodies
                var physics = new GameObject("Physics").transform;
                physics.SetParent(root.transform, false);
                var bodies = new Rigidbody[RagdollPawn.Count];
                var parts = new RagdollBodyPart[RagdollPawn.Count];
                for (int b = 0; b < RagdollPawn.Count; b++)
                {
                    var go = new GameObject(BodyNames[b]);
                    go.transform.SetParent(physics, false);
                    go.transform.SetPositionAndRotation(pivots[b], Quaternion.identity);
                    var rb = go.AddComponent<Rigidbody>();
                    rb.mass = BodyMass[b];
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.solverIterations = 16;
                    rb.solverVelocityIterations = 4;
                    rb.maxAngularVelocity = 40f;
                    rb.maxDepenetrationVelocity = 3f;
                    rb.linearDamping = 0.05f;
                    rb.angularDamping = 0.1f;
                    parts[b] = go.AddComponent<RagdollBodyPart>();
                    parts[b].id = (BodyId)b;
                    bodies[b] = rb;
                }

                // Hips: skirt hull. Chest: upper body + collar hull. Head: sphere.
                var main = comps[0];
                AddHull(bodies[(int)BodyId.Hips], main.Where(i => world[i].y >= footTop && world[i].y <= ySpine + 0.02f * height).Select(i => world[i]), "PawnHipsHull");
                AddHull(bodies[(int)BodyId.Chest], main.Where(i => world[i].y >= ySpine && world[i].y <= yHead).Select(i => world[i]), "PawnChestHull");
                var headBounds = BoundsOf(main.Where(i => world[i].y >= yHead).Select(i => world[i]));
                var headSphere = bodies[(int)BodyId.Head].gameObject.AddComponent<SphereCollider>();
                headSphere.center = headBounds.center - pivots[(int)BodyId.Head];
                headSphere.radius = 0.5f * (headBounds.size.x + headBounds.size.z) * 0.5f * 0.97f;

                AddArm(bodies[(int)BodyId.ArmL], stubBoundsL);
                AddArm(bodies[(int)BodyId.ArmR], stubBoundsR);
                var palmL = AddPalm(bodies[(int)BodyId.HandL], compBounds[handCompL]);
                var palmR = AddPalm(bodies[(int)BodyId.HandR], compBounds[handCompR]);
                AddThigh(bodies[(int)BodyId.ThighL], pivots[(int)BodyId.FootL]);
                AddThigh(bodies[(int)BodyId.ThighR], pivots[(int)BodyId.FootR]);
                var footBoxL = AddFoot(bodies[(int)BodyId.FootL], BoundsOf(main.Where(i => world[i].y < footTop && Mathf.Sign(world[i].x) == leftSign && Mathf.Abs(world[i].x) > 0.035f * height).Select(i => world[i])));
                var footBoxR = AddFoot(bodies[(int)BodyId.FootR], BoundsOf(main.Where(i => world[i].y < footTop && Mathf.Sign(world[i].x) != leftSign && Mathf.Abs(world[i].x) > 0.035f * height).Select(i => world[i])));

                // ---- joints (child -> parent)
                var joints = new ConfigurableJoint[RagdollPawn.Count];
                joints[(int)BodyId.Chest] = Joint(bodies, BodyId.Chest, -30f, 30f, 30f, 30f);
                joints[(int)BodyId.Head] = Joint(bodies, BodyId.Head, -40f, 40f, 45f, 35f);
                joints[(int)BodyId.ArmL] = Joint(bodies, BodyId.ArmL, 0f, 0f, 0f, 0f, free: true);
                joints[(int)BodyId.ArmR] = Joint(bodies, BodyId.ArmR, 0f, 0f, 0f, 0f, free: true);
                joints[(int)BodyId.HandL] = Joint(bodies, BodyId.HandL, -45f, 45f, 45f, 45f);
                joints[(int)BodyId.HandR] = Joint(bodies, BodyId.HandR, -45f, 45f, 45f, 45f);
                joints[(int)BodyId.ThighL] = Joint(bodies, BodyId.ThighL, -75f, 60f, 20f, 35f);
                joints[(int)BodyId.ThighR] = Joint(bodies, BodyId.ThighR, -75f, 60f, 20f, 35f);
                joints[(int)BodyId.FootL] = Joint(bodies, BodyId.FootL, -40f, 40f, 15f, 25f);
                joints[(int)BodyId.FootR] = Joint(bodies, BodyId.FootR, -40f, 40f, 15f, 25f);

                // ---- locomotion anchor
                var anchorGo = new GameObject("LocomotionAnchor");
                anchorGo.transform.SetParent(root.transform, false);
                anchorGo.transform.position = pivots[(int)BodyId.Hips];
                var anchor = anchorGo.AddComponent<Rigidbody>();
                anchor.isKinematic = true;
                anchor.useGravity = false;
                anchor.interpolation = RigidbodyInterpolation.None;
                var anchorJoint = bodies[(int)BodyId.Hips].gameObject.AddComponent<ConfigurableJoint>();
                anchorJoint.connectedBody = anchor;
                anchorJoint.autoConfigureConnectedAnchor = false;
                anchorJoint.anchor = Vector3.zero;
                anchorJoint.connectedAnchor = Vector3.zero;
                // Axis = world up: the angular X drive is yaw, the YZ drive keeps the hips upright.
                anchorJoint.axis = Vector3.up;
                anchorJoint.secondaryAxis = Vector3.forward;
                anchorJoint.xMotion = anchorJoint.yMotion = anchorJoint.zMotion = ConfigurableJointMotion.Free;
                anchorJoint.angularXMotion = anchorJoint.angularYMotion = anchorJoint.angularZMotion = ConfigurableJointMotion.Free;
                var zero = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = float.MaxValue };
                anchorJoint.xDrive = anchorJoint.yDrive = anchorJoint.zDrive = zero;
                anchorJoint.rotationDriveMode = RotationDriveMode.XYAndZ;
                anchorJoint.angularXDrive = anchorJoint.angularYZDrive = zero;
                anchorJoint.targetRotation = Quaternion.identity;
                anchorJoint.enablePreprocessing = false;
                anchorJoint.enableCollision = false;

                // ---- puppet (non-physical target pose hierarchy)
                var puppetRoot = new GameObject("Puppet").transform;
                puppetRoot.SetParent(root.transform, false);
                var puppet = new Transform[RagdollPawn.Count];
                for (int b = 0; b < RagdollPawn.Count; b++) puppet[b] = new GameObject("P_" + BodyNames[b]).transform;
                for (int b = 0; b < RagdollPawn.Count; b++)
                {
                    int p = RagdollPawn.ParentOf[b];
                    puppet[b].SetParent(p < 0 ? puppetRoot : puppet[p], false);
                }
                for (int b = 0; b < RagdollPawn.Count; b++) puppet[b].SetPositionAndRotation(pivots[b], Quaternion.identity);

                // ---- hands
                var handL = bodies[(int)BodyId.HandL].gameObject.AddComponent<PawnHand>();
                handL.body = bodies[(int)BodyId.HandL];
                handL.palm = palmL;
                var handR = bodies[(int)BodyId.HandR].gameObject.AddComponent<PawnHand>();
                handR.body = bodies[(int)BodyId.HandR];
                handR.palm = palmR;

                // ---- pawn
                var pawn = root.AddComponent<RagdollPawn>();
                pawn.tuning = tuning;
                pawn.bodies = bodies;
                pawn.joints = joints;
                pawn.puppet = puppet;
                pawn.anchor = anchor;
                pawn.anchorJoint = anchorJoint;
                pawn.handL = handL;
                pawn.handR = handR;
                pawn.footL = footBoxL;
                pawn.footR = footBoxR;
                pawn.standHeight = pivots[(int)BodyId.Hips].y;
                pawn.skin = skin;
                handL.owner = pawn;
                handR.owner = pawn;
                foreach (var part in parts) part.owner = pawn;

                // ---- visual sync (Mixamo bones follow physics bodies)
                var entries = new List<RagdollVisualSync.Entry>();
                void Absolute(string bone, BodyId driver)
                {
                    Transform t = Bone(bone);
                    Transform d = bodies[(int)driver].transform;
                    entries.Add(new RagdollVisualSync.Entry
                    {
                        bone = t, driverA = (int)driver, driverB = -1, setPosition = true,
                        positionOffset = d.InverseTransformPoint(t.position),
                        rotationOffset = Quaternion.Inverse(d.rotation) * t.rotation,
                    });
                }
                void Blend(string bone, BodyId a, BodyId b, float w)
                {
                    Transform t = Bone(bone);
                    Quaternion blended = Quaternion.Slerp(bodies[(int)a].transform.rotation, bodies[(int)b].transform.rotation, w);
                    entries.Add(new RagdollVisualSync.Entry
                    {
                        bone = t, driverA = (int)a, driverB = (int)b, blend = w, setPosition = false,
                        rotationOffset = Quaternion.Inverse(blended) * t.rotation,
                    });
                }
                Absolute("Hips", BodyId.Hips);
                Blend("Spine", BodyId.Hips, BodyId.Chest, 0.33f);
                Blend("Spine1", BodyId.Hips, BodyId.Chest, 0.66f);
                Absolute("Spine2", BodyId.Chest);
                Absolute("Head", BodyId.Head);
                Absolute("LeftArm", BodyId.ArmL);
                Absolute("LeftHand", BodyId.HandL);
                Absolute("RightArm", BodyId.ArmR);
                Absolute("RightHand", BodyId.HandR);
                Absolute("LeftFoot", BodyId.FootL);
                Absolute("RightFoot", BodyId.FootR);
                var sync = root.AddComponent<RagdollVisualSync>();
                sync.pawn = pawn;
                sync.entries = entries.ToArray();

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[RagdollLab] Prefab saved: {PrefabPath}, total mass {BodyMass.Sum():F1} kg, hips {pawn.standHeight:F3} m");
                return saved.GetComponent<RagdollPawn>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void ApplyBindPose(SkinnedMeshRenderer skin)
        {
            var bones = skin.bones;
            var bind = skin.sharedMesh.bindposes;
            Matrix4x4 m = skin.transform.localToWorldMatrix;
            foreach (int i in Enumerable.Range(0, bones.Length).Where(i => bones[i] != null).OrderBy(i => Depth(bones[i])))
            {
                Matrix4x4 w = m * bind[i].inverse;
                bones[i].SetPositionAndRotation(w.GetColumn(3), w.rotation);
            }
        }

        static int Depth(Transform t)
        {
            int d = 0;
            for (; t.parent != null; t = t.parent) d++;
            return d;
        }

        static BoneWeight One(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1f };

        static BoneWeight Chain(float y, float[] ys, int[] bones)
        {
            if (y <= ys[0]) return One(bones[0]);
            for (int k = 0; k < ys.Length - 1; k++)
            {
                if (y > ys[k + 1]) continue;
                float t = Mathf.SmoothStep(0f, 1f, (y - ys[k]) / (ys[k + 1] - ys[k]));
                return new BoneWeight { boneIndex0 = bones[k], weight0 = 1f - t, boneIndex1 = bones[k + 1], weight1 = t };
            }
            return One(bones[bones.Length - 1]);
        }

        static Bounds BoundsOf(IEnumerable<Vector3> points)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var p in points)
            {
                if (!any)
                {
                    b = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else b.Encapsulate(p);
            }
            if (!any) throw new Exception("Empty vertex region");
            return b;
        }

        static Vector3 StubPivot(Bounds stub, bool inner)
        {
            float sign = Mathf.Sign(stub.center.x);
            float x = inner ? Mathf.Min(Mathf.Abs(stub.min.x), Mathf.Abs(stub.max.x)) + 0.012f
                            : Mathf.Max(Mathf.Abs(stub.min.x), Mathf.Abs(stub.max.x)) - 0.02f;
            return new Vector3(sign * x, stub.center.y, stub.center.z);
        }

        static void AddHull(Rigidbody body, IEnumerable<Vector3> worldPoints, string name)
        {
            Vector3 origin = body.transform.position;
            // Snap to a 2 cm grid first: PhysX caps a convex hull at 256 polygons and silently
            // simplifies anything denser, which showed up as a warning on every import.
            var unique = new HashSet<Vector3Int>();
            var points = new List<Vector3>();
            foreach (var world in worldPoints)
            {
                Vector3 local = world - origin;
                if (!unique.Add(Vector3Int.RoundToInt(local * 50f))) continue;
                points.Add(local);
            }
            var hull = new Mesh { name = name };
            hull.SetVertices(points);
            // Convex cooking only needs the point cloud; a fan keeps the mesh valid.
            var tris = new List<int>();
            for (int i = 1; i + 1 < points.Count; i++)
            {
                tris.Add(0);
                tris.Add(i);
                tris.Add(i + 1);
            }
            hull.SetTriangles(tris, 0);
            hull.RecalculateBounds();
            hull = SaveAsset(hull, $"{GeneratedDir}/{name}.asset");
            var collider = body.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = hull;
            collider.convex = true;
        }

        static void AddArm(Rigidbody body, Bounds stub)
        {
            var capsule = body.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 0;
            capsule.radius = 0.5f * Mathf.Min(stub.size.y, stub.size.z);
            capsule.height = Mathf.Max(stub.size.x, capsule.radius * 2f);
            capsule.center = stub.center - body.transform.position;
        }

        static SphereCollider AddPalm(Rigidbody body, Bounds ball)
        {
            var sphere = body.gameObject.AddComponent<SphereCollider>();
            sphere.center = ball.center - body.transform.position;
            sphere.radius = 0.5f * (ball.size.x + ball.size.y + ball.size.z) / 3f;
            return sphere;
        }

        static void AddThigh(Rigidbody body, Vector3 anklePosition)
        {
            Vector3 hip = body.transform.position;
            Vector3 dir = anklePosition - hip;
            var go = new GameObject("ThighCollider");
            go.transform.SetParent(body.transform, false);
            go.transform.SetPositionAndRotation(hip + dir * 0.5f, Quaternion.FromToRotation(Vector3.up, dir.normalized));
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = 0.045f;
            capsule.height = dir.magnitude + 0.02f;
        }

        static BoxCollider AddFoot(Rigidbody body, Bounds foot)
        {
            var box = body.gameObject.AddComponent<BoxCollider>();
            box.center = foot.center - body.transform.position;
            box.size = Vector3.Max(foot.size, new Vector3(0.05f, 0.05f, 0.05f));
            return box;
        }

        static ConfigurableJoint Joint(Rigidbody[] bodies, BodyId child, float lowX, float highX, float y, float z, bool free = false)
        {
            var body = bodies[(int)child];
            var joint = body.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = bodies[RagdollPawn.ParentOf[(int)child]];
            joint.anchor = Vector3.zero;
            joint.autoConfigureConnectedAnchor = true;
            joint.axis = Vector3.right;
            joint.secondaryAxis = Vector3.up;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            var motion = free ? ConfigurableJointMotion.Free : ConfigurableJointMotion.Limited;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = motion;
            joint.lowAngularXLimit = new SoftJointLimit { limit = lowX };
            joint.highAngularXLimit = new SoftJointLimit { limit = highX };
            joint.angularYLimit = new SoftJointLimit { limit = y };
            joint.angularZLimit = new SoftJointLimit { limit = z };
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            joint.slerpDrive = new JointDrive { positionSpring = 0f, positionDamper = 0f, maximumForce = float.MaxValue };
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.02f;
            joint.projectionAngle = 5f;
            joint.enablePreprocessing = false;
            joint.enableCollision = false;
            return joint;
        }

        static T SaveAsset<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            EditorUtility.CopySerialized(asset, existing);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ------------------------------------------------------------------ scene

        static void BuildScene(RagdollPawn prefab, RagdollTuning tuning, Materials mats)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sky = new Color(0.63f, 0.78f, 0.93f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.8f, 0.86f, 0.96f);
            RenderSettings.ambientEquatorColor = new Color(0.66f, 0.68f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.4f, 0.38f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 170f;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = sky;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 300f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 4f, -12f), Quaternion.Euler(20f, 0f, 0f));
            var labCamera = cameraGo.AddComponent<LabCamera>();

            var env = new GameObject("Environment").transform;
            BuildGeometry(env, mats, out var bar);

            var gameGo = new GameObject("RagdollLab");
            var game = gameGo.AddComponent<LabGame>();
            game.pawnPrefab = prefab;
            game.tuning = tuning;
            game.labCamera = labCamera;
            game.bar = bar;
            game.dummyMaterial = mats.pawnDummy;
            game.players = new[]
            {
                new LabGame.Slot { name = "P1", device = LabDevice.KeyboardMouse, spawn = LabLayout.SpawnP1, material = mats.pawnP1 },
                new LabGame.Slot { name = "P2", device = LabDevice.Pad1, spawn = LabLayout.SpawnP2, material = mats.pawnP2 },
            };
            var slots = gameGo.AddComponent<LabParamSlots>();
            slots.tuning = tuning;
            slots.game = game;
            game.slots = slots;
            gameGo.AddComponent<LabPanel>().game = game;
            gameGo.AddComponent<LabAutoTest>().game = game;
            labCamera.game = game;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void BuildGeometry(Transform env, Materials mats, out SpinningBar bar)
        {
            float half = LabLayout.MainHalfSize;

            // [1] Flat floor 30 x 30
            Box(env, "Floor 30x30", new Vector3(0f, -0.5f, 0f), new Vector3(half * 2f, 1f, half * 2f), mats.floor);
            // Labels stay ASCII: the built-in TextMesh font has no Hangul glyphs.
            Label(env, "[1] 30 x 30", new Vector3(0f, 0.02f, -13.2f), Quaternion.Euler(90f, 0f, 0f), 0.6f);

            // [2] Rotating bar zone (north)
            Vector3 barCenter = LabLayout.BarCenter;
            Box(env, "Bar Floor", new Vector3(barCenter.x, -0.5f, barCenter.z), new Vector3(16f, 1f, 16f), mats.floor);
            var spinner = new GameObject("Spinning Bar");
            spinner.transform.SetParent(env, false);
            spinner.transform.position = barCenter;
            var spinBody = spinner.AddComponent<Rigidbody>();
            spinBody.isKinematic = true;
            bar = spinner.AddComponent<SpinningBar>();
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(spinner.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            post.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            post.GetComponent<MeshRenderer>().sharedMaterial = mats.post;
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = "Bar";
            rod.transform.SetParent(spinner.transform, false);
            rod.transform.localPosition = new Vector3(0f, LabLayout.BarHeight, 0f);
            rod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rod.transform.localScale = new Vector3(LabLayout.BarRadius * 2f, LabLayout.BarLength * 0.5f, LabLayout.BarRadius * 2f);
            rod.GetComponent<MeshRenderer>().sharedMaterial = mats.hazard;
            rod.AddComponent<RagdollHazard>();
            Label(env, "[2] BAR", barCenter + new Vector3(0f, 0.02f, -7f), Quaternion.Euler(90f, 0f, 0f), 0.6f);

            // [3] Walls 2 / 3 / 4 m (east)
            Box(env, "Wall Floor", new Vector3(half + 10f, -0.5f, 0f), new Vector3(20f, 1f, half * 2f), mats.floor);
            for (int i = 0; i < LabLayout.WallHeights.Length; i++)
            {
                float h = LabLayout.WallHeights[i];
                float z = LabLayout.WallZ[i];
                Box(env, $"Wall {h:F0}m", new Vector3(LabLayout.WallFrontX + LabLayout.WallDepth * 0.5f, h * 0.5f, z), new Vector3(LabLayout.WallDepth, h, LabLayout.WallWidth), mats.wall);
                Label(env, $"[3] {h:F0}m", new Vector3(LabLayout.WallFrontX - 0.02f, h * 0.5f, z), Quaternion.LookRotation(Vector3.right), 0.7f);
            }

            BuildClimbWalls(env, mats);
            {
            }

            // [4] Slopes 15 / 30 / 45 degrees (west), platform 5 m high
            float platformHeight = LabLayout.PlatformHeight;
            Box(env, "Slope Floor", new Vector3(-half - 12.5f, -0.5f, 0f), new Vector3(25f, 1f, half * 2f), mats.floor);
            float platformDepth = LabLayout.PlatformEdgeX - LabLayout.PlatformBackX;
            Box(env, "Platform 5m", new Vector3((LabLayout.PlatformEdgeX + LabLayout.PlatformBackX) * 0.5f, platformHeight * 0.5f, 0f),
                new Vector3(platformDepth, platformHeight, half * 2f), mats.wall);
            for (int i = 0; i < LabLayout.SlopeAngles.Length; i++)
            {
                float angle = LabLayout.SlopeAngles[i];
                float rad = angle * Mathf.Deg2Rad;
                float length = platformHeight / Mathf.Sin(rad);
                const float thickness = 0.6f;
                Vector3 down = new Vector3(Mathf.Cos(rad), -Mathf.Sin(rad), 0f);
                Vector3 normal = new Vector3(Mathf.Sin(rad), Mathf.Cos(rad), 0f);
                Vector3 top = new Vector3(LabLayout.PlatformEdgeX, platformHeight, LabLayout.SlopeZ[i]);
                Vector3 center = top + down * (length * 0.5f) - normal * (thickness * 0.5f);
                Box(env, $"Slope {angle:F0}deg", center, new Vector3(length, thickness, LabLayout.SlopeWidth), mats.slope, Quaternion.Euler(0f, 0f, -angle));
                Label(env, $"[4] {angle:F0}°", new Vector3(LabLayout.SlopeBottomX(angle) + 1.5f, 0.02f, LabLayout.SlopeZ[i]), Quaternion.Euler(90f, 90f, 0f), 0.7f);
            }

            // [5] Narrow beam 0.6 m over a drop (south)
            float beamLength = LabLayout.BeamStartZ - LabLayout.BeamEndZ;
            Box(env, "Beam 0.6m", new Vector3(0f, -0.25f, (LabLayout.BeamStartZ + LabLayout.BeamEndZ) * 0.5f), new Vector3(LabLayout.BeamWidth, 0.5f, beamLength), mats.beam);
            Box(env, "Beam Landing", new Vector3(0f, -0.5f, LabLayout.BeamEndZ - 3f), new Vector3(6f, 1f, 6f), mats.floor);
            Label(env, "[5] 0.6m", new Vector3(1.4f, 0.02f, LabLayout.BeamStartZ + 0.9f), Quaternion.Euler(90f, 0f, 0f), 0.6f);

            // [6] Low obstacles: limbo bar and low tunnel
            Vector3 limbo = LabLayout.LimboCenter;
            float slab = 0.25f;
            Box(env, "Limbo Bar", limbo + new Vector3(0f, LabLayout.LimboClearance + slab * 0.5f, 0f), new Vector3(4f, slab, 0.6f), mats.low);
            Box(env, "Limbo Post L", limbo + new Vector3(-2.15f, (LabLayout.LimboClearance + slab) * 0.5f, 0f), new Vector3(0.3f, LabLayout.LimboClearance + slab, 0.6f), mats.post);
            Box(env, "Limbo Post R", limbo + new Vector3(2.15f, (LabLayout.LimboClearance + slab) * 0.5f, 0f), new Vector3(0.3f, LabLayout.LimboClearance + slab, 0.6f), mats.post);
            Vector3 tunnel = LabLayout.TunnelCenter;
            Box(env, "Tunnel Roof", tunnel + new Vector3(0f, LabLayout.TunnelClearance + slab * 0.5f, 0f), new Vector3(4f, slab, LabLayout.TunnelLength), mats.low);
            Box(env, "Tunnel Wall L", tunnel + new Vector3(-2.15f, (LabLayout.TunnelClearance + slab) * 0.5f, 0f), new Vector3(0.3f, LabLayout.TunnelClearance + slab, LabLayout.TunnelLength), mats.post);
            Box(env, "Tunnel Wall R", tunnel + new Vector3(2.15f, (LabLayout.TunnelClearance + slab) * 0.5f, 0f), new Vector3(0.3f, LabLayout.TunnelClearance + slab, LabLayout.TunnelLength), mats.post);
            Label(env, "[6] 0.55m", limbo + new Vector3(0f, 0.02f, 1.3f), Quaternion.Euler(90f, 0f, 0f), 0.45f);
            Label(env, "[6] 0.65m", tunnel + new Vector3(0f, 0.02f, 2.3f), Quaternion.Euler(90f, 0f, 0f), 0.45f);
        }

        /// <summary>
        /// Three climbing test faces, all facing +Z so the pawn walks south into them. A plain flat
        /// wall is deliberately not among them: this pawn cannot touch one above its own skirt.
        /// </summary>
        static void BuildClimbWalls(Transform env, Materials mats)
        {
            float h = LabLayout.ClimbHeight, w = LabLayout.ClimbWidth, z = LabLayout.ClimbFaceZ;

            // --- [3b-1] the big wall: a staircase of set-back blocks, each about one stamina bar
            float x0 = LabLayout.ClimbX[0];
            for (int i = 0; i < LabLayout.LedgeSteps; i++)
            {
                float top = (i + 1) * LabLayout.LedgeStepHeight;
                float back = z + 0.5f + i * LabLayout.LedgeStepBack;
                Box(env, $"LedgeStep {i}", new Vector3(x0, top * 0.5f, back),
                    new Vector3(LabLayout.LedgeWallWidth, top, 1f + i * LabLayout.LedgeStepBack),
                    i == 0 ? mats.wall : mats.slope);
            }
            Label(env, "[3b] 큰 벽 + 바위", new Vector3(x0 + 3.4f, 2f, z - 0.02f), Quaternion.LookRotation(Vector3.forward), 0.9f);

            // --- [3b-2] overhang: the face leans out over the approach, so the body hangs clear
            float x1 = LabLayout.ClimbX[1];
            Box(env, "ClimbWall Overhang", new Vector3(x1, h * 0.5f, z + 0.7f), new Vector3(w, h, 1.2f),
                mats.wall, Quaternion.Euler(20f, 0f, 0f));
            Label(env, "[3b] 역경사 20도", new Vector3(x1 + 1.8f, h * 0.55f, z - 0.6f), Quaternion.LookRotation(Vector3.forward), 0.7f);

            // --- [3b-3] curved: stacked slabs, each tilted a little more. Every slab is its own convex
            // box, because PawnHand ignores non-convex meshes and could not grab a single curved mesh.
            float x2 = LabLayout.ClimbX[2];
            int slabs = Mathf.FloorToInt(h / 0.35f);
            for (int i = 0; i < slabs; i++)
            {
                float y = 0.175f + i * 0.35f;
                float t = i / (float)Mathf.Max(1, slabs - 1);
                float lean = Mathf.Lerp(-12f, 18f, t);          // leans in low, out high
                float bulge = Mathf.Sin(t * Mathf.PI) * 0.28f;
                Box(env, $"Curve {i}", new Vector3(x2, y, z + 0.5f - bulge), new Vector3(w, 0.34f, 1f),
                    mats.wall, Quaternion.Euler(lean, 0f, 0f));
            }
            Label(env, "[3b] 곡면", new Vector3(x2 + 1.8f, h * 0.5f, z - 0.4f), Quaternion.LookRotation(Vector3.forward), 0.7f);

            // --- floor behind the lanes. They stand at the north edge of the 30 x 30 floor, and the
            // tops of the shape-test lanes are only ~1 m deep, so a pawn that topped out and kept
            // going ran straight off the back into the void (curved lane: up 5.49 m, ended at
            // -6.25 m). The strip fills the 1 m gap to the bar floor; the pads cover the rest.
            float half = LabLayout.MainHalfSize;
            Box(env, "Climb Back Strip", new Vector3(0f, -0.5f, half + 0.5f), new Vector3(half * 2f, 1f, 1f), mats.floor);
            Box(env, "Climb Back East", new Vector3(11.5f, -0.5f, half + 5f), new Vector3(7f, 1f, 8f), mats.floor);
            Box(env, "Climb Back West", new Vector3(-11.5f, -0.5f, half + 5f), new Vector3(7f, 1f, 8f), mats.floor);
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rotation ?? Quaternion.identity);
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }

        /// <summary>Box mesh whose UVs are in world meters (the grid texture repeats every 2 m).</summary>
        static Mesh BoxMesh(Vector3 size)
        {
            string key = $"Box_{size.x:0.##}x{size.y:0.##}x{size.z:0.##}".Replace('.', 'p');
            string path = $"{BoxDir}/{key}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            Vector3 h = size * 0.5f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Face(Vector3 normal, Vector3 u, Vector3 w, float su, float sw)
            {
                int b = vertices.Count;
                Vector3 c = Vector3.Scale(normal, h);
                Vector3 du = u * (su * 0.5f), dw = w * (sw * 0.5f);
                vertices.Add(c - du - dw);
                vertices.Add(c + du - dw);
                vertices.Add(c + du + dw);
                vertices.Add(c - du + dw);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(su * 0.5f, 0f));
                uvs.Add(new Vector2(su * 0.5f, sw * 0.5f));
                uvs.Add(new Vector2(0f, sw * 0.5f));
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            }
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.left, Vector3.forward, size.x, size.z);
            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);

            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void Label(Transform parent, string text, Vector3 position, Quaternion rotation, float size)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 64;
            mesh.characterSize = size * 0.12f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(0.16f, 0.18f, 0.24f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }
    }
}
