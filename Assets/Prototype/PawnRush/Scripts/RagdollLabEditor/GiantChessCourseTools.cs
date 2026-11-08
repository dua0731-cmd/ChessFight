using UnityEditor;
using UnityEditor.SceneManagement;

namespace ChessFight.RagdollLab.Editor
{
    public static class GiantChessCourseTools
    {
        [MenuItem("ChessFight/Minigames/Open Giant GM Chess Course")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/GiantChessCourse.unity");
        }
    }
}
