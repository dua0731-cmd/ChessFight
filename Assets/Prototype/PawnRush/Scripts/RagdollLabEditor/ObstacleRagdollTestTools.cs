using UnityEditor;
using UnityEditor.SceneManagement;

namespace ChessFight.RagdollLab.Editor
{
    public static class ObstacleRagdollTestTools
    {
        public const string ScenePath = "Assets/Scenes/ObstacleRagdollTest.unity";

        [MenuItem("ChessFight/Obstacles/Open Ragdoll Test (15 Obstacles)")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
