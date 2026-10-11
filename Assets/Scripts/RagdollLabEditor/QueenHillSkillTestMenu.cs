using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>Opens the Queen of the Hill skill test scene (R89): a copy of RagdollTest with the Queen of the Hill skill
    /// test bed on the lab object. If the scene is not there yet, it is made: RagdollTest copied, the bed added.</summary>
    public static class QueenHillSkillTestMenu
    {
        public const string ScenePath = "Assets/Scenes/SkillTest/QueenOfTheHill_SkillTest/QueenOfTheHill_SkillTest.unity";
        const string LabScene = "Assets/Scenes/RagdollTest.unity";

        [MenuItem("ChessFight/Queen of the Hill/Open Skill Test", false, 41)]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Make();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Copy RagdollTest into the Queen of the Hill skill test folder and put the bed on its lab object.
        /// Returns what it did (for the editor bridge).</summary>
        public static string Make()
        {
            if (File.Exists(ScenePath)) return "already there: " + ScenePath;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? ".");
            if (!AssetDatabase.CopyAsset(LabScene, ScenePath)) return "could not copy " + LabScene;
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var game = Object.FindFirstObjectByType<LabGame>();
            if (game == null) return "no LabGame in the copy";
            if (game.GetComponent<QueenHillSkillBed>() == null) game.gameObject.AddComponent<QueenHillSkillBed>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "made " + ScenePath + " (bed on " + game.name + ")";
        }
    }
}
