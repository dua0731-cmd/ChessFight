using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.PawnRush.Editor
{
    // seoungtest (R680 QA): this branch is the carriage yard's modelling bench, so the editor shows the yard alone.
    //   - When the editor starts (once a session), the map test scene opens; its PawnRushMapTest plays only
    //     RoyalCarriageYard (onlyMap, no calibration yard).
    //   - Play from any scene plays the map test scene (EditorSceneManager.playModeStartScene).
    // Off (remembered on this PC): ChessFight > Pawn Rush > Carriage Yard Only (seoungtest). Batch runs are left alone.
    [InitializeOnLoad]
    static class SeoungTestYardOnly
    {
        const string MenuPath = "ChessFight/Pawn Rush/Carriage Yard Only (seoungtest)";
        const string PrefKey = "ChessFight.SeoungTest.YardOnly";
        const string SessionKey = "ChessFight.SeoungTest.YardOnly.Opened";

        static SeoungTestYardOnly()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Apply;
        }

        static bool On => EditorPrefs.GetBool(PrefKey, true);

        static void Apply()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PawnRushMapTestMenu.ScenePath);
            EditorSceneManager.playModeStartScene = On ? scene : null;
            if (!On || scene == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            if (SceneManager.GetActiveScene().path == PawnRushMapTestMenu.ScenePath) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (!SceneManager.GetSceneAt(i).isDirty) continue;
                Debug.Log("[seoungtest] 저장 안 한 장면이 있어 마차뜰 장면으로 바꾸지 않았습니다. "
                          + "ChessFight > Pawn Rush > Open Map Test로 여세요(Play는 어느 장면에서든 마차뜰).");
                return;
            }
            EditorSceneManager.OpenScene(PawnRushMapTestMenu.ScenePath, OpenSceneMode.Single);
            Debug.Log("[seoungtest] 마차뜰 장면(" + PawnRushMapTestMenu.ScenePath + ")을 열었습니다. Play를 누르면 마차뜰만 뜹니다. "
                      + "끄기: " + MenuPath);
        }

        [MenuItem(MenuPath, false, 4)]
        static void Toggle()
        {
            EditorPrefs.SetBool(PrefKey, !On);
            Apply();
            Debug.Log("[seoungtest] 마차뜰만 띄우기 " + (On ? "켬" : "끔"));
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleCheck()
        {
            Menu.SetChecked(MenuPath, On);
            return true;
        }
    }
}
