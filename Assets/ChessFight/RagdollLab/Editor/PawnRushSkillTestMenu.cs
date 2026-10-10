using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>Opens the Pawn Rush skill test scene: a copy of RagdollTest with the skill test bed on the lab
    /// object (Docs/Skills/README.md). R112: the skill test scenes live on the branch skill-network-test now; on a branch
    /// without them this says where they went.</summary>
    public static class PawnRushSkillTestMenu
    {
        public const string ScenePath = "Assets/Scenes/SkillTest/PawnRush_SkillTest/PawnRush_SkillTest.unity";

        /// <summary>R112 (승규 님): the skill test scenes were taken off claude/bold-johnson-8ez95n.</summary>
        public const string Moved =
            "스킬 시험 씬은 이 브랜치에서 지웠어요 (R112).\n\n" +
            "스킬 시험(온라인 포함)은 skill-network-test 브랜치에서 해요.\n" +
            "승규 님 PC: Unity Hub에서 C:\\Work\\ChessFight-SkillNet 폴더를 열면 돼요.";

        [MenuItem("ChessFight/Pawn Rush/Open Skill Test", false, 40)]
        public static void Open()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("폰 러쉬 스킬 시험", Moved, "확인");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
