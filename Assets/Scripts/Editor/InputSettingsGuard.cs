using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace ChessFight.Editor
{
    // Keeps Active Input Handling on "Input Manager (Old)" while this project has
    // no uGUI.
    //
    // Without uGUI a runtime UI Toolkit panel cannot use the EventSystem path and
    // falls back to its own event system, which stops delivering pointer and
    // keyboard events once the Input System backend is enabled. The HUD then
    // renders perfectly and ignores every click.
    //
    // Editing ProjectSettings.asset in git is not enough: an Editor that is
    // already open holds the value in memory and writes its own copy back over
    // the pulled file. Going through SerializedObject writes through Unity
    // instead of behind its back.
    [InitializeOnLoad]
    public static class InputSettingsGuard
    {
        const int InputManagerOld = 0;

        static InputSettingsGuard() { EditorApplication.delayCall += Enforce; }

        static void Enforce()
        {
            // Once uGUI is present the constraint is gone, so leave the choice alone.
            if (UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p => p.name == "com.unity.ugui")) return;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var settings = new SerializedObject(assets[0]);
            var handler = settings.FindProperty("activeInputHandler");
            if (handler == null || handler.intValue == InputManagerOld) return;

            handler.intValue = InputManagerOld;
            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.LogWarning("[ChessFight] Active Input Handling을 'Input Manager (Old)'로 되돌렸습니다.\n" +
                             "이 프로젝트에는 uGUI가 없어 이 값이 Both/New이면 HUD 클릭과 번호 입력이 전부 죽습니다.\n" +
                             "★ 적용하려면 Unity를 완전히 종료했다가 다시 실행해야 합니다.");
        }
    }
}
