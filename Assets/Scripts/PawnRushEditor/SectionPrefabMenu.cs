using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.PawnRush.Editor
{
    // The section prefabs' menus (ChessFight > Pawn Rush):
    //   Build Early 1-1 Royal Water Garden
    //                                    builds the garden's starting point (the v31 graybox with the garden's water,
    //                                    RoyalWaterGardenBuilder) into its prefab, then validates it (R400)
    //   Build Mid 2-1 Grand Stair Hall   rebuilds the hall from code (GrandStairHallBuilder) into its prefab,
    //                                    keeping the links to the imported obstacle prefabs; running it twice
    //                                    gives the same prefab. Then validates it.
    //   Open Section Test                the round test scene (PawnRush_Sections.unity); since R493 its last slot is the
    //                                    war room (Final_WarRoom, built in Play from zone E's code, never baked)
    //   Open Match Round                 the lobby's 폰 러쉬 match scene (PawnRush_Round.unity, R508): the same round with a
    //                                    crown finish on the war room's goal; played on its own it is offline
    //   Validate Sections                every prefab in the section set, built in a scratch scene and checked
    //                                    (SectionValidator), then the scratch scene is thrown away
    // Hand edits inside the prefab's Content are lost on the next build: change the builder instead.
    public static class SectionPrefabMenu
    {
        public const string HallPrefab = "Assets/Prefabs/PawnRushSections/Mid_2-1_GrandStairHall.prefab";
        public const string GardenPrefab = "Assets/Prefabs/PawnRushSections/Early_1-1_RoyalWaterGarden.prefab";
        public const string TestScene = "Assets/Scenes/PawnRush_Sections.unity";
        public const string MatchScene = "Assets/Scenes/PawnRush_Round.unity";
        public const string SectionSet = "Assets/Settings/PawnRushSections.asset";

        // R400 (승규 11-04, from main): the v31 graybox and the garden's water baked into the garden prefab, so it can be
        // seen and remodelled by hand in Prefab Mode. Rebuilding throws away whatever is under Content, so it asks first
        // when the prefab already holds a garden.
        [MenuItem("ChessFight/Pawn Rush/Build/Early 1-1 Royal Water Garden", false, 100)]
        public static void BuildRoyalWaterGarden()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var root = PrefabUtility.LoadPrefabContents(GardenPrefab);
            try
            {
                var garden = root.GetComponent<RoyalWaterGarden>();
                if (garden == null || garden.Kit == null)
                {
                    EditorUtility.DisplayDialog("Build Early 1-1 Royal Water Garden", GardenPrefab + "에 RoyalWaterGarden(키트 지정)이 없습니다.", "확인");
                    return;
                }
                if (garden.HasContent && !EditorUtility.DisplayDialog("Build Early 1-1 Royal Water Garden",
                        "프리팹에 이미 정원(Content)이 있습니다. 처음 상태로 다시 지으면 Content 밑에서 손으로 바꾼 것이 모두 사라집니다.", "다시 짓기", "취소"))
                    return;
                RoyalWaterGardenBuilder.Build(garden);
                PrefabUtility.SaveAsPrefabAsset(root, GardenPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log("[PawnRush] Build Early 1-1 Royal Water Garden: 프리팹을 다시 만들고 저장했습니다 (" + GardenPrefab + ").");
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GardenPrefab);
            if (prefabAsset != null) Report(new List<GameObject> { prefabAsset }, false);
        }

        [MenuItem("ChessFight/Pawn Rush/Build/Mid 2-1 Grand Stair Hall", false, 101)]
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

        [MenuItem("ChessFight/Pawn Rush/Open Section Test", false, 0)]
        public static void OpenTestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TestScene);
        }

        [MenuItem("ChessFight/Pawn Rush/Open Match Round", false, 1)]
        public static void OpenMatchScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MatchScene);
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Sections", false, 200)]
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
                    var garden = go.GetComponent<RoyalWaterGarden>();
                    if (garden != null && !garden.HasContent) RoyalWaterGardenBuilder.Build(garden);
                    // R493: the war room (slot 7) is never baked; it is built here as in Play.
                    var room = go.GetComponent<WarRoomSection>();
                    if (room != null && !room.HasContent) Course01v4Builder.BuildWarRoomSection(room);
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
