using UnityEditor;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // The mirror tools (design doc v0.2 §8), on top of the runtime TeamMirror. The mirror is the
    // world's x = 0 plane: the wings sit on both sides of the map.
    //   Mirror White → Black              rebuild a wing's black half (W1_Black, W2_Black) from the
    //                                     selected white one (W1_White, W2_White, or anything in it)
    //   Mirror Selected (Left-Right Pair) a mirrored copy of the selection about x = 0, back-and-forth
    //                                     obstacles half a cycle on
    // Build Course01 v2 rebuilds everything from code, mirrors included: these are for trying a
    // hand edit before it goes into the builder.
    public static class TeamMirrorMenu
    {
        [MenuItem("ChessFight/Pawn Rush/Mirror White → Black", false, 20)]
        static void MirrorTeam()
        {
            var white = FindWhite(Selection.activeGameObject);
            if (white == null) return;
            var black = TeamMirror.MirrorTeam(white);
            Undo.RegisterCreatedObjectUndo(black.gameObject, "Mirror White → Black");
            EditorUtility.SetDirty(black.gameObject);
            Selection.activeGameObject = black.gameObject;
        }

        [MenuItem("ChessFight/Pawn Rush/Mirror White → Black", true)]
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

        // The *_White group the selection is in (or is).
        static Transform FindWhite(GameObject selected)
        {
            for (var t = selected != null ? selected.transform : null; t != null; t = t.parent)
                if (t.name.EndsWith("_White")) return t;
            return null;
        }
    }
}
