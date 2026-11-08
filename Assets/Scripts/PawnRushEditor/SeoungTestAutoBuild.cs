using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.PawnRush.Editor
{
    // 승규 님's test scene kept up with the code (R242, R243): when the open SeoungTest_PawnRush scene's zones are not the
    // code's (a pull that added the endgame's E and F, or took course 01's sections out), it offers once per Editor session
    // to build the scene's zones again,
    // the same as the menu ChessFight > SeoungTest > Build Pawn Rush Test Scene (hand edits inside SeoungTest_Zones go).
    [InitializeOnLoad]
    static class SeoungTestAutoBuild
    {
        static string Asked => "SeoungTestAutoBuild.Asked.v" + SeoungTestCourse.Version;

        static SeoungTestAutoBuild()
        {
            EditorApplication.delayCall += Check;
            EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != SeoungTestMenu.PawnRushScene || SessionState.GetBool(Asked, false)) return;
            var test = Object.FindFirstObjectByType<SeoungTestCourse>();
            if (test == null) return;
            var root = test.transform.Find(SeoungTestCourse.RootName);
            string mismatch = root == null ? "구역 없음" : SeoungTestCourse.Mismatch(root);
            if (mismatch == null) return;
            SessionState.SetBool(Asked, true);
            if (EditorUtility.DisplayDialog("SeoungTest",
                    "이 시험 씬의 구역이 코드와 다릅니다(" + mismatch + "). 구역을 지금 다시 지어 씬에 저장할까요?\n\n" +
                    "SeoungTest_Zones 안에서 손으로 고친 것은 사라집니다. 나중에 메뉴 ChessFight > SeoungTest > " +
                    "Build Pawn Rush Test Scene으로 해도 됩니다. 다시 짓지 않아도 Play하면 그 Play에서는 코드대로 지어집니다.",
                    "다시 짓기", "나중에"))
                SeoungTestMenu.BuildPawnRush();
        }
    }
}
