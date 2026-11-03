using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.PawnRush.Editor
{
    // The section prefabs' menus (ChessFight > Pawn Rush):
    //   Build Early Plaza C Moat Drawbridge
    //                                    rebuilds the early mini-game plaza with C (EarlyPlazaBuilder) into its prefab
    //                                    (R153); the game itself is installed at Play. Then validates it.
    //   Build Mid 2-1 Grand Stair Hall   rebuilds the hall from code (GrandStairHallBuilder) into its prefab,
    //                                    keeping the links to the imported obstacle prefabs; running it twice
    //                                    gives the same prefab. Then validates it.
    //   Open Section Test                the round test scene (PawnRush_Sections.unity)
    //   Validate Sections                every prefab in the section set, built in a scratch scene and checked
    //                                    (SectionValidator), then the scratch scene is thrown away
    // Hand edits inside the prefab's Content are lost on the next build: change the builder instead.
    public static class SectionPrefabMenu
    {
        public const string PlazaCPrefab = "Assets/Prefabs/PawnRushSections/Early_C_MoatDrawbridge.prefab";
        public const string HallPrefab = "Assets/Prefabs/PawnRushSections/Mid_2-1_GrandStairHall.prefab";
        public const string TestScene = "Assets/Scenes/PawnRush_Sections.unity";
        public const string SectionSet = "Assets/Settings/PawnRushSections.asset";

        [MenuItem("ChessFight/Pawn Rush/Build Early Plaza C Moat Drawbridge", false, 19)]
        public static void BuildEarlyPlazaC() => BuildPlaza(PlazaCPrefab, "Build Early Plaza C Moat Drawbridge");

        static void BuildPlaza(string path, string title)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var plaza = root.GetComponent<EarlyPlaza>();
                if (plaza == null || plaza.Kit == null)
                {
                    EditorUtility.DisplayDialog(title, path + "에 EarlyPlaza(키트 지정)가 없습니다.", "확인");
                    return;
                }
                EarlyPlazaBuilder.Build(plaza);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[PawnRush] " + title + ": 프리팹을 다시 만들고 저장했습니다 (" + path + "). 미니게임은 Play 때 스테이션에 들어갑니다.");
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefabAsset != null) Report(new List<GameObject> { prefabAsset }, false);
        }

        [MenuItem("ChessFight/Pawn Rush/Build Mid 2-1 Grand Stair Hall", false, 20)]
        public static void BuildGrandStairHall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var root = PrefabUtility.LoadPrefabContents(HallPrefab);
            try
            {
                var hall = root.GetComponent<GrandStairHall>();
                if (hall == null || hall.Kit == null)
                {
                    EditorUtility.DisplayDialog("Build Mid 2-1 Grand Stair Hall", HallPrefab + "에 GrandStairHall(키트 지정)이 없습니다.", "확인");
                    return;
                }
                var previous = CourseBuilder.Spawn;
                CourseBuilder.Spawn = (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                try { GrandStairHallBuilder.Build(hall); }
                finally { CourseBuilder.Spawn = previous; }
                PrefabUtility.SaveAsPrefabAsset(root, HallPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[PawnRush] Build Mid 2-1 Grand Stair Hall: 프리팹을 다시 만들고 저장했습니다 (" + HallPrefab + ").");
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HallPrefab);
            if (prefabAsset != null) Report(new List<GameObject> { prefabAsset }, false);
        }

        [MenuItem("ChessFight/Pawn Rush/Open Section Test", false, 21)]
        public static void OpenTestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TestScene);
        }

        [MenuItem("ChessFight/Pawn Rush/Validate Sections", false, 22)]
        public static void ValidateSections()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var set = AssetDatabase.LoadAssetAtPath<PawnRushSectionSet>(SectionSet);
            var prefabs = new List<GameObject>();
            if (set != null)
                for (int i = 0; i < set.SlotCount; i++)
                    if (set[i] != null && set[i].prefabs != null)
                        foreach (var p in set[i].prefabs)
                            if (p != null && !prefabs.Contains(p)) prefabs.Add(p);
            if (prefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("Validate Sections", SectionSet + "에 구간 프리팹이 없습니다.", "확인");
                return;
            }
            Report(prefabs, true);
        }

        // Each prefab in a scratch scene far from anything else, built if it is not baked yet, checked, gone.
        static void Report(List<GameObject> prefabs, bool dialog)
        {
            var notes = new List<string>();
            var errors = new List<string>();
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                foreach (var prefab in prefabs)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scratch);
                    go.transform.position = new Vector3(0f, 0f, 20000f);
                    var hall = go.GetComponent<GrandStairHall>();
                    if (hall != null && !hall.HasContent) GrandStairHallBuilder.Build(hall);
                    var plaza = go.GetComponent<EarlyPlaza>();
                    if (plaza != null)
                    {
                        if (!plaza.HasContent) EarlyPlazaBuilder.Build(plaza);
                        // The game is a Play-time part; built here only to be checked (the scratch scene goes).
                        plaza.InstallGames();
                    }
                    var section = go.GetComponent<PawnRushSection>();
                    if (section == null)
                    {
                        errors.Add(prefab.name + ": PawnRushSection이 없습니다");
                        continue;
                    }
                    errors.AddRange(SectionValidator.Run(section, notes));
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scratch, true);
            }
            foreach (var n in notes) Debug.Log("[PawnRush] " + n);
            foreach (var e in errors) Debug.LogError("[PawnRush] 구간 검증 실패: " + e);
            string summary = errors.Count == 0 ? "구간 검증 통과 (오류 0)" : $"구간 검증 오류 {errors.Count}개 — Console을 보세요";
            Debug.Log("[PawnRush] " + summary);
            if (dialog) EditorUtility.DisplayDialog("Validate Sections", summary + "\n\n" + string.Join("\n", notes), "확인");
        }
    }
}
