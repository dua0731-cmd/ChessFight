using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.PawnRush.Editor
{
    // seoungtest (R725): this branch is the Royal Observatory's modelling bench (ArtSource/PawnRush/RoyalObservatory/PLAN.md),
    // as it was the carriage yard's (R680 QA, SeoungTestYardOnly) and then the giant chess course's. The editor shows the
    // observatory alone:
    //   - When the editor starts (once a session), the course prefab test scene opens (CoursePrefabMenu.TestScene).
    //   - Play from any scene plays that scene (EditorSceneManager.playModeStartScene), and as Play begins the test
    //     switches to the observatory's course (Late_3-2_RoyalObservatory) - the number keys still pick the others.
    // Off (remembered on this PC): ChessFight > Pawn Rush > Royal Observatory Only (seoungtest). Batch runs are left alone.
    // Branch only: remove this file before work moves to main.
    [InitializeOnLoad]
    static class SeoungTestObservatoryOnly
    {
        const string MenuPath = "ChessFight/Pawn Rush/Royal Observatory Only (seoungtest)";
        const string PrefKey = "ChessFight.SeoungTest.ObservatoryOnly";
        const string SessionKey = "ChessFight.SeoungTest.ObservatoryOnly.Opened";
        const string Course = "Late_3-2_RoyalObservatory";

        static SeoungTestObservatoryOnly()
        {
            if (Application.isBatchMode) return;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.delayCall += Apply;
        }

        static bool On => EditorPrefs.GetBool(PrefKey, true);

        static void Apply()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(CoursePrefabMenu.TestScene);
            EditorSceneManager.playModeStartScene = On ? scene : null;
            if (!On || scene == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            if (SceneManager.GetActiveScene().path == CoursePrefabMenu.TestScene) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                Debug.Log("[seoungtest] 저장 안 한 장면이 있어 천문대 장면으로 바꾸지 않았습니다. "
                          + "ChessFight > Pawn Rush > Open Course Prefab Test로 여세요(Play는 어느 장면에서든 천문대).");
                return;
            }
            EditorSceneManager.OpenScene(CoursePrefabMenu.TestScene, OpenSceneMode.Single);
            Debug.Log("[seoungtest] 코스 프리팹 시험 장면(" + CoursePrefabMenu.TestScene + ")을 열었습니다. Play를 누르면 왕실 천문대가 뜹니다. "
                      + "끄기: " + MenuPath);
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !On) return;
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            if (test == null) return;
            int index = IndexOf(test);
            if (index < 0)
            {
                Debug.LogWarning("[seoungtest] 구간 목록에 " + Course + "가 없어 천문대를 고르지 못했습니다.");
                return;
            }
            test.Select(index);
        }

        // The entry's index in CoursePrefabTest's list, counted the same way (obstacle slots in order, each slot's prefabs,
        // then its drafts).
        static int IndexOf(CoursePrefabTest test)
        {
            var set = new SerializedObject(test).FindProperty("set").objectReferenceValue as PawnRushSectionSet;
            if (set == null) return -1;
            int index = 0;
            for (int s = 0; s < set.SlotCount; s++)
            {
                var slot = set[s];
                if (slot == null || slot.kind != PawnRushSection.SlotKind.Obstacle) continue;
                foreach (var list in new[] { slot.prefabs, slot.drafts })
                {
                    if (list == null) continue;
                    foreach (var prefab in list)
                    {
                        if (prefab == null) continue;
                        if (prefab.name == Course) return index;
                        index++;
                    }
                }
            }
            return -1;
        }

        [MenuItem(MenuPath, false, 4)]
        static void Toggle()
        {
            EditorPrefs.SetBool(PrefKey, !On);
            SessionState.SetBool(SessionKey, false);
            Apply();
            Debug.Log("[seoungtest] 천문대만 띄우기 " + (On ? "켬" : "끔"));
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleCheck()
        {
            Menu.SetChecked(MenuPath, On);
            return true;
        }
    }
}
