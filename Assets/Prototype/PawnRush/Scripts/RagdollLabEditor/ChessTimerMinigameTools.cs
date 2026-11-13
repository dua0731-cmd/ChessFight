using UnityEditor;
using UnityEditor.SceneManagement;

namespace ChessFight.RagdollLab.Editor
{
    public static class ChessTimerMinigameTools
    {
        [MenuItem("ChessFight/Minigames/Open Chess Timer Prototype")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/ChessTimerMinigame.unity");
        }
    }
}
