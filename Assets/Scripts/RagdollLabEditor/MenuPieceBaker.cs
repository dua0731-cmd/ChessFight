using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>
    /// The menu screens' chess pieces from the models the ragdoll wears (R148): the lobby, the entrance screen and the
    /// Pawn Rush result scenes show the same pawn and the same five pieces as a match, not code-built stand-ins.
    /// For each of Pawn, King, Queen, Rook, Bishop and Knight this copies the pawn prefab's Visual (its Mixamo armature
    /// and skin) in the bind pose, puts the piece's mesh and material on the skin (Resources/PieceModels, baked by
    /// PieceModelBaker; the pawn keeps its own) and saves Resources/MenuPieces/&lt;Piece&gt;.prefab with its feet on y = 0,
    /// centred over the origin and facing +Z, with the piece's LeftShoulder and RightShoulder marks. LastSceneFigure
    /// (ChessFight.Game) loads these and turns the arm and leg bones for its poses. Display only: no physics, no colliders.
    /// Menu: ChessFight/Ragdoll Lab/Bake Menu Pieces (run it again after Bake Piece Models).
    /// </summary>
    public static class MenuPieceBaker
    {
        const string PawnPrefab = "Assets/Prefabs/RagdollPawn.prefab";
        const string OutDir = "Assets/Resources/MenuPieces";
        static readonly string[] Pieces = { "Pawn", "King", "Queen", "Rook", "Bishop", "Knight" };

        [MenuItem("ChessFight/Ragdoll Lab/Bake Menu Pieces")]
        public static void BakeAll()
        {
            var log = new StringBuilder("[MenuPieces]");
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/Resources", "MenuPieces");
            var pawn = PrefabUtility.LoadPrefabContents(PawnPrefab);
            try
            {
                var skin = pawn.GetComponent<RagdollPawn>().skin;
                RagdollLabBuilder.ApplyBindPose(skin);
                foreach (string piece in Pieces) log.Append(' ').Append(Bake(piece, pawn, skin)).Append(';');
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(pawn);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static string Bake(string piece, GameObject pawn, SkinnedMeshRenderer pawnSkin)
        {
            Mesh mesh = pawnSkin.sharedMesh;
            Material material = pawnSkin.sharedMaterial;
            GameObject prefab = null;
            if (piece != "Pawn")
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PieceModels/" + piece + ".prefab");
                var look = prefab != null ? prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                if (look == null || look.sharedMesh == null) return piece + " has no piece model";
                if (look.sharedMesh.bindposes.Length != mesh.bindposes.Length) return piece + " does not fit the pawn's bones";
                mesh = look.sharedMesh;
                material = look.sharedMaterial;
            }

            var root = new GameObject(piece);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, pawn.scene);
            try
            {
                // The Visual subtree, copied whole so the skin keeps its bones (Instantiate remaps them to the copy).
                var visual = Object.Instantiate(pawnSkin.transform.parent.gameObject, root.transform, false);
                visual.name = "Visual";
                var pawnVisual = pawnSkin.transform.parent;
                visual.transform.localPosition = pawn.transform.InverseTransformPoint(pawnVisual.position);
                visual.transform.localRotation = Quaternion.Inverse(pawn.transform.rotation) * pawnVisual.rotation;
                var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
                skin.sharedMesh = mesh;
                skin.sharedMaterial = material;
                // The menus turn the arms and legs; keep the bounds with them.
                skin.updateWhenOffscreen = true;

                // Feet on the floor and the body over the origin, measured on the posed skin.
                var baked = new Mesh();
                skin.BakeMesh(baked, true);
                var box = new Bounds();
                bool first = true;
                foreach (var v in baked.vertices)
                {
                    var p = root.transform.InverseTransformPoint(skin.transform.TransformPoint(v));
                    if (first) { box = new Bounds(p, Vector3.zero); first = false; }
                    else box.Encapsulate(p);
                }
                Object.DestroyImmediate(baked);
                var shift = new Vector3(box.center.x, box.min.y, box.center.z);
                visual.transform.localPosition -= shift;
                // The piece's own shoulders (where each arm stub leaves the body, in the pawn's rest space): the menus
                // raise the arms about these, as the match does (RagdollVisualSync.PoseAsPiece). The pawn has none and
                // turns them about its arm bones.
                if (prefab != null)
                    foreach (string mark in new[] { "LeftShoulder", "RightShoulder" })
                    {
                        var source = prefab.transform.Find(mark);
                        if (source == null) continue;
                        var copy = new GameObject(mark).transform;
                        copy.SetParent(root.transform, false);
                        copy.localPosition = source.localPosition - shift;
                    }

                string path = OutDir + "/" + piece + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{piece} {box.size.y:0.00} m";
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
