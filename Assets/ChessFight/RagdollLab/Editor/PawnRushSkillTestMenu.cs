using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>Opens the Pawn Rush skill test scene: a copy of RagdollTest with the skill test bed on the lab
    /// object (Docs/Skills/README.md).</summary>
    public static class PawnRushSkillTestMenu
    {
        public const string ScenePath = "Assets/Scenes/SkillTest/PawnRush_SkillTest/PawnRush_SkillTest.unity";

        [MenuItem("ChessFight/Pawn Rush/Open Skill Test", false, 40)]
        public static void Open()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[Pawn Rush] {ScenePath} 이 없어요.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
