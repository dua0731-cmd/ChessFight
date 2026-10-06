using UnityEditor;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // The mirror tools (design doc §6 "거울 생성"), on top of the runtime TeamMirror:
    //   Mirror Team_White → Team_Black   rebuild a team module's black half from its white half
    //   Mirror Selected (Left/Right pair) a shared module's pair: a mirrored copy about the module's
    //                                    centre line, back-and-forth obstacles half a cycle on
    public static class TeamMirrorMenu
    {
        [MenuItem("ChessFight/Pawn Rush/Mirror Team_White → Team_Black", false, 20)]
        static void MirrorTeam()
        {
            var white = FindWhite(Selection.activeGameObject);
            if (white == null) return;
            // Inside a module placed in the scene the halves belong to the module prefab: edit the prefab.
            if (PrefabUtility.IsPartOfPrefabInstance(white))
            {
                EditorUtility.DisplayDialog("Mirror Team_White", "씬에 놓인 모듈 프리팹 안입니다. 모듈 프리팹을 열고(Project 창에서 더블클릭) 거기서 실행하세요.", "확인");
                return;
            }
            var black = TeamMirror.MirrorTeam(white);
            Undo.RegisterCreatedObjectUndo(black.gameObject, "Mirror Team_White");
            EditorUtility.SetDirty(black.gameObject);
            Selection.activeGameObject = black.gameObject;
        }

        [MenuItem("ChessFight/Pawn Rush/Mirror Team_White → Team_Black", true)]
        static bool CanMirrorTeam() => !EditorApplication.isPlaying && FindWhite(Selection.activeGameObject) != null;

        [MenuItem("ChessFight/Pawn Rush/Mirror Selected (Left-Right Pair)", false, 21)]
        static void MirrorSelected()
        {
            foreach (var go in Selection.gameObjects)
            {
                var copy = TeamMirror.MirrorCopy(go, true);
                Undo.RegisterCreatedObjectUndo(copy, "Mirror Selected");
            }
        }

        [MenuItem("ChessFight/Pawn Rush/Mirror Selected (Left-Right Pair)", true)]
        static bool CanMirrorSelected() => !EditorApplication.isPlaying && Selection.gameObjects.Length > 0;

        // The Team_White group of the selected module (or the selection itself).
        static Transform FindWhite(GameObject selected)
        {
            if (selected == null) return null;
            if (selected.name == TeamMirror.WhiteName) return selected.transform;
            var module = selected.GetComponentInParent<CourseModule>(true);
            return module != null ? module.transform.Find(TeamMirror.WhiteName) : null;
        }
    }
}
