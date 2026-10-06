using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // ChessFight > Pawn Rush > Build Course01 (design doc §6): one click rebuilds the course in
    // PawnRush_Course01.unity from Course01Layout - baked module prefabs, laid end to end. Running
    // it twice gives the same scene; turning an extension on or off in the layout and building
    // again moves the modules behind it. Nothing is placed by hand.
    public static class Course01Assembler
    {
        public const string ScenePath = "Assets/Scenes/PawnRush/PawnRush_Course01.unity";

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

        [MenuItem("ChessFight/Pawn Rush/Build Course01", false, 1)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EnsureScene()) return;
            var course = Object.FindFirstObjectByType<PawnRushCourse>();
            if (course == null || course.Layout == null || course.Kit == null)
            {
                EditorUtility.DisplayDialog("Build Course01", "씬에 PawnRushCourse(레이아웃·키트 지정)가 없습니다.", "확인");
                return;
            }
            Course01ModuleBuilder.BakeMissing(course.Layout, course.Kit);
            course.Clear();
            course.Assemble((entry, origin) =>
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab, course.transform);
                go.transform.position = origin;
                return go;
            });
            var scene = course.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PawnRush] Build Course01: 모듈 {course.Modules.Count}개 배치 (" +
                      string.Join(", ", course.Modules.Select(m => m.Code)) + ")");
            Course01ValidatorMenu.Report(course, false);
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
                EditorUtility.DisplayDialog("Validate Course01", "열린 씬에 PawnRushCourse가 없습니다. Open Course01 → Build Course01 먼저.", "확인");
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
