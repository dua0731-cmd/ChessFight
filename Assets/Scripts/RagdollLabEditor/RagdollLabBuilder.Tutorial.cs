using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    // The first-run tutorial scene (R829 grey box, Docs/Player/TUTORIAL_DESIGN_v0.1.md): the ragdoll lab's map
    // (BuildGeometry, the same LabLayout numbers as RagdollTest) under a Pawn Rush offline playtest - the map test
    // scene's camera, light, physics profile and playtest pawn (PawnRush_MapTest) - plus the tutorial's own props and
    // TutorialCourse. RagdollTest itself is left as it is (the lab and its automatic checks).
    //   Development > Tutorial > Open Tutorial Scene      opens Assets/Scenes/Tutorial.unity (makes it the first time)
    //   Development > Tutorial > Rebuild Tutorial Scene   makes it again (hand edits in the scene are lost)
    public static partial class RagdollLabBuilder
    {
        public const string TutorialScenePath = SceneDir + "/Tutorial.unity";
        const string TutorialSource = SceneDir + "/PawnRush_MapTest.unity";

        [MenuItem("ChessFight/Development/Tutorial/Open Tutorial Scene", false, 1)]
        public static void OpenTutorial()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialScenePath) == null) BuildTutorial();
            else EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);
        }

        [MenuItem("ChessFight/Development/Tutorial/Rebuild Tutorial Scene", false, 2)]
        public static void RebuildTutorial()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialScenePath) != null &&
                !EditorUtility.DisplayDialog("튜토리얼 씬", TutorialScenePath + "을(를) 다시 만듭니다. 씬에서 손으로 바꾼 것은 사라집니다.", "다시 만들기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildTutorial();
        }

        // Batch: Unity -batchmode -executeMethod ChessFight.RagdollLab.Editor.RagdollLabBuilder.BuildTutorialBatch
        public static void BuildTutorialBatch() => RunBatch(BuildTutorial);

        // The automatic check's way in (TutorialValidation): no dialogs.
        internal static void BuildTutorialForCheck() => BuildTutorial();

        static void BuildTutorial()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialSource) == null)
                throw new System.Exception(TutorialSource + "이 없습니다 (ChessFight > Development > Map Authoring > Rebuild Pawn Rush Map Test Scene).");
            EnsureFolders();
            // R841: the scene is in the build list now (the intro loads it), by its GUID: a rebuild writes over the file
            // and keeps its .meta, so the list's entry stays good (DeleteAsset + CopyAsset gave it a new GUID each time).
            if (System.IO.File.Exists(TutorialScenePath))
            {
                System.IO.File.Copy(TutorialSource, TutorialScenePath, true);
                AssetDatabase.ImportAsset(TutorialScenePath, ImportAssetOptions.ForceUpdate);
            }
            else AssetDatabase.CopyAsset(TutorialSource, TutorialScenePath);
            var scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);

            // The map test's own parts go (its maps are built at Play; nothing of them is in the scene).
            foreach (var name in new[] { "Pawn Rush Map Test", "Map Test Start" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }

            var mats = LabMaterials();
            var env = new GameObject("Environment (ragdoll lab map)").transform;
            BuildGeometry(env, mats, out _);
            // The lab's floor labels ("[2] BAR", and Korean ones the built-in font cannot draw) are a test bench's.
            foreach (var label in env.GetComponentsInChildren<TextMesh>(true)) Object.DestroyImmediate(label.gameObject);

            // Lesson 2's hurdles.
            var props = new GameObject("Tutorial props").transform;
            for (int i = 0; i < TutorialLayout.HurdleX.Length; i++)
                Box(props, $"Hurdle {i + 1}", new Vector3(TutorialLayout.HurdleX[i], TutorialLayout.HurdleHeight * 0.5f, TutorialLayout.HurdleZ),
                    new Vector3(TutorialLayout.HurdleThickness, TutorialLayout.HurdleHeight, TutorialLayout.HurdleLength), mats.low);

            // The palace drill yard look (ArtSource/Tutorial): the boxes above keep their colliders and stop drawing.
            DressTutorial(env, props);

            var gold = Mat("Tutorial_Gold", new Color(1f, 0.78f, 0.22f), null, 0.5f);
            LitMaterials.SetEmission(gold, new Color(0.55f, 0.38f, 0.06f));
            EditorUtility.SetDirty(gold);

            var start = new GameObject("Tutorial Start").transform;
            start.SetPositionAndRotation(TutorialLayout.Start, Quaternion.Euler(0f, TutorialLayout.StartYaw, 0f));
            var spawner = Object.FindFirstObjectByType<PlaytestSpawner>();
            if (spawner == null) throw new System.Exception(TutorialSource + "에 PlaytestSpawner가 없습니다.");
            var sp = new SerializedObject(spawner);
            sp.FindProperty("spawnPoint").objectReferenceValue = start;
            sp.FindProperty("showHelp").boolValue = false;   // the tutorial draws its own lines
            sp.FindProperty("team").intValue = Teams.White;
            sp.FindProperty("fallLimit").floatValue = -20f;
            sp.ApplyModifiedPropertiesWithoutUndo();

            var course = new GameObject("Tutorial").AddComponent<TutorialCourse>();
            var so = new SerializedObject(course);
            so.FindProperty("spawner").objectReferenceValue = spawner;
            so.FindProperty("gold").objectReferenceValue = gold;
            so.FindProperty("botSkin").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/Pawn_P2.mat");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Tutorial] 튜토리얼 씬을 만들었습니다: " + TutorialScenePath);
        }

        // The lab's map materials as they are (BuildMaterials would also rewrite the pawn's).
        static Materials LabMaterials()
        {
            Material Load(string name)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");
                if (mat == null) throw new System.Exception($"{MaterialDir}/{name}.mat이 없습니다 (Ragdoll Lab > Rebuild Pawn + Scene).");
                return mat;
            }
            return new Materials
            {
                floor = Load("Lab_Floor"), wall = Load("Lab_Wall"), slope = Load("Lab_Slope"), beam = Load("Lab_Beam"),
                hazard = Load("Lab_Hazard"), post = Load("Lab_Post"), low = Load("Lab_Low"),
            };
        }
    }
}
