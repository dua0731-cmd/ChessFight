using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // ChessFight > Pawn Rush > Build Course01 v4 (design doc v0.4 "Claude Code 제작 지시",
    // Course01v4Builder): one click deletes the course root in PawnRush_Course01.unity and builds it
    // again at the design doc's world coordinates, keeping the links to the imported obstacle
    // prefabs. Running it twice gives the same scene. Hand edits inside Course01v4_Root are lost on
    // the next build: change the builder, or keep edits outside the root.
    public static class Course01Assembler
    {
        public const string ScenePath = "Assets/Scenes/PawnRush_Course01.unity";

        [MenuItem("ChessFight/Pawn Rush/Open Course01", false, 0)]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EnsureScene()) return;
            var course = Object.FindFirstObjectByType<PawnRushCourse>();
            if (course != null)
            {
                Selection.activeGameObject = course.gameObject;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }

        [MenuItem("ChessFight/Pawn Rush/Build Course01 v4", false, 1)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EnsureScene()) return;
            var course = Object.FindFirstObjectByType<PawnRushCourse>();
            if (course == null || course.Kit == null)
            {
                EditorUtility.DisplayDialog("Build Course01 v4", "씬에 PawnRushCourse(키트 지정)가 없습니다.", "확인");
                return;
            }
            course.Clear();
            var previous = CourseBuilder.Spawn;
            CourseBuilder.Spawn = (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            try { Course01v4Builder.Build(course.transform, course.Kit); }
            finally { CourseBuilder.Spawn = previous; }
            var scene = course.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PawnRush] Build Course01 v4: 코스를 다시 만들고 씬을 저장했습니다.");
            Course01ValidatorMenu.Report(course, false);
            // The validator builds every game into every station and puts the preview back.
            EditorSceneManager.SaveScene(scene);
        }

        // The course scene open and active; asks to save the scene being left.
        static bool EnsureScene()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == ScenePath) return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(ScenePath);
            return true;
        }
    }

    // ChessFight > Pawn Rush > Validate Course01.
    public static class Course01ValidatorMenu
    {
        [MenuItem("ChessFight/Pawn Rush/Validate Course01", false, 2)]
        static void Validate()
        {
            var course = Object.FindFirstObjectByType<PawnRushCourse>();
            if (course == null)
            {
                EditorUtility.DisplayDialog("Validate Course01", "열린 씬에 PawnRushCourse가 없습니다. Open Course01 → Build Course01 v4 먼저.", "확인");
                return;
            }
            Report(course, true);
        }

        public static void Report(PawnRushCourse course, bool dialog)
        {
            var notes = new List<string>();
            var errors = Course01Validator.Run(course, notes);
            foreach (var n in notes) Debug.Log("[PawnRush] " + n);
            foreach (var e in errors) Debug.LogError("[PawnRush] 검증 실패: " + e);
            string summary = errors.Count == 0 ? "검증 통과 (오류 0)" : $"검증 오류 {errors.Count}개 — Console을 보세요";
            Debug.Log("[PawnRush] " + summary);
            if (dialog) EditorUtility.DisplayDialog("Validate Course01", summary + "\n\n" + string.Join("\n", notes), "확인");
        }
    }
}
