using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    public static class RoyalPalaceCourseTools
    {
        [MenuItem("ChessFight/Courses/Open Royal Palace Test")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Scenes/RoyalPalaceCourseTest.unity");
        }

        [MenuItem("ChessFight/Courses/Save Selected Course As Prefab")]
        public static void Save()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var course = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<CourseModule>() : null;
            if (course == null) { Debug.LogWarning("Select a CourseModule or one of its children."); return; }
            var path = EditorUtility.SaveFilePanelInProject("Save course prefab", course.name, "prefab",
                "Save this edited course as an independent prefab.", "Assets/Maps/RoyalPalace/Courses");
            if (string.IsNullOrEmpty(path)) return;
            // A temporary unpacked copy also supports saving a nested instance as a new course.
            var copy = Object.Instantiate(course.gameObject);
            copy.name = course.name;
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(copy))
                    PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                PrefabUtility.SaveAsPrefabAsset(copy, path);
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }

    [CustomEditor(typeof(CourseModule))]
    public sealed class CourseModuleEditor : UnityEditor.Editor
    {
        Transform previousExit;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var course = (CourseModule)target;
            EditorGUILayout.HelpBox($"Route: {course.RouteLength:F0} m. Target: {course.TargetSeconds:F0} s (requires playtesting).\nEdit obstacle parameters on the named objects under Beats. Connector alignment does not resolve overlapping course geometry; inspect the bounds before assembling a map.", MessageType.Info);
            previousExit = (Transform)EditorGUILayout.ObjectField("Previous exit", previousExit, typeof(Transform), true);
            using (new EditorGUI.DisabledScope(previousExit == null || Application.isPlaying || EditorUtility.IsPersistent(course)
                || (previousExit != null && previousExit.IsChildOf(course.transform))))
                if (GUILayout.Button("Align entry to previous exit"))
                {
                    Undo.RecordObject(course.transform, "Align course entry");
                    course.AlignEntryTo(previousExit);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(course.transform);
                    EditorSceneManager.MarkSceneDirty(course.gameObject.scene);
                }
        }
    }
}
