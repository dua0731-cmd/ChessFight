using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>
    /// Bakes the artist's chess pieces onto the pawn's skeleton, for a pawn to wear (RagdollPawn.Piece).
    /// In Art/Pieces/&lt;Piece&gt;/: &lt;Piece&gt;_Rigged.fbx, the model weighted part by part onto the pawn's own Mixamo
    /// armature (Tools/PieceRig/rig_piece.py in Blender), and &lt;Piece&gt;.mat. Out: &lt;Piece&gt;_Skinned.asset, the
    /// mesh in the pawn skin's space with the pawn's bind poses, and Resources/PieceModels/&lt;Piece&gt;.prefab,
    /// which holds it with the material and six marks for RagdollVisualSync, in the pawn's rest space:
    /// where each arm stub leaves the body (LeftShoulder, RightShoulder), where its mitten begins
    /// (LeftWrist, RightWrist) and the middle of each foot (LeftFoot, RightFoot).
    /// Menu: ChessFight/Ragdoll Lab/Bake Piece Models.
    /// </summary>
    public static class PieceModelBaker
    {
        const string Root = "Assets";
        const string PawnPrefab = Root + "/Prefabs/RagdollPawn.prefab";
        const string PiecesDir = Root + "/Art/Pieces";
        const string ModelsDir = Root + "/Resources/PieceModels";
        static readonly string[] Pieces = { "King", "Queen", "Rook", "Bishop", "Knight" };

        [MenuItem("ChessFight/Ragdoll Lab/Bake Piece Models")]
        public static void BakeAll()
        {
            var log = new StringBuilder("[PieceModels]");
            var pawn = PrefabUtility.LoadPrefabContents(PawnPrefab);
            try
            {
                var skin = pawn.GetComponent<RagdollPawn>().skin;
                RagdollLabBuilder.ApplyBindPose(skin);
                var index = new Dictionary<string, int>();
                for (int i = 0; i < skin.bones.Length; i++)
                    if (skin.bones[i] != null) index[skin.bones[i].name] = i;
                foreach (string piece in Pieces) log.Append(' ').Append(Bake(piece, pawn.transform, skin, index)).Append(';');
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(pawn);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static string Bake(string piece, Transform pawn, SkinnedMeshRenderer pawnSkin, Dictionary<string, int> index)
        {
            string dir = PiecesDir + "/" + piece + "/";
            return Bake(piece, dir + piece + "_Rigged.fbx", AssetDatabase.LoadAssetAtPath<Material>(dir + piece + ".mat"),
                dir + piece + "_Skinned.asset", ModelsDir + "/" + piece + ".prefab", true, pawn, pawnSkin, index);
        }

        /// <summary>
        /// The Pawn Rush set (seoungtest R251): the same pieces and the pawn itself, weighted again by
        /// Tools/PieceRig/rig_pawnrush.py (Art/PawnRushRig/&lt;Name&gt;_Rigged_PawnRush.fbx), baked the same way into
        /// Art/PawnRushRig/&lt;Name&gt;_Skinned_PawnRush.asset and Resources/PieceModels/PawnRush/&lt;Name&gt;.prefab. Only a
        /// pawn with RagdollPawn.PawnRushPhysics wears them; the shared models above stay as they are.
        /// </summary>
        [MenuItem("ChessFight/Ragdoll Lab/Bake Pawn Rush Piece Models")]
        public static void BakePawnRush()
        {
            var log = new StringBuilder("[PieceModels PawnRush]");
            var pawn = PrefabUtility.LoadPrefabContents(PawnPrefab);
            try
            {
                var skin = pawn.GetComponent<RagdollPawn>().skin;
                RagdollLabBuilder.ApplyBindPose(skin);
                var index = new Dictionary<string, int>();
                for (int i = 0; i < skin.bones.Length; i++)
                    if (skin.bones[i] != null) index[skin.bones[i].name] = i;
                foreach (string piece in PawnRushNames)
                {
                    // The pawn keeps its own material (its team's colour is set on the skin); its prefab only carries the mesh.
                    var material = piece == "Pawn" ? skin.sharedMaterial
                        : AssetDatabase.LoadAssetAtPath<Material>(PiecesDir + "/" + piece + "/" + piece + ".mat");
                    log.Append(' ').Append(Bake(piece, PawnRushDir + "/" + piece + "_Rigged_PawnRush.fbx", material,
                        PawnRushDir + "/" + piece + "_Skinned_PawnRush.asset", PawnRushModelsDir + "/" + piece + ".prefab",
                        piece != "Pawn", pawn.transform, skin, index)).Append(';');
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(pawn);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        const string PawnRushDir = Root + "/Art/PawnRushRig";
        const string PawnRushModelsDir = ModelsDir + "/PawnRush";
        static readonly string[] PawnRushNames = { "Pawn", "King", "Queen", "Rook", "Bishop", "Knight" };

        static string Bake(string piece, string fbxPath, Material material, string meshPath, string prefabPath, bool marks,
            Transform pawn, SkinnedMeshRenderer pawnSkin, Dictionary<string, int> index)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null || material == null) return piece + " has no model";
            var model = (GameObject)Object.Instantiate(fbx);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model, pawn.gameObject.scene);
            try
            {
                var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                RagdollLabBuilder.ApplyBindPose(skin);
                // The same bones in the same rest pose, but the two FBX files' bone axes differ: line the
                // armatures up by the hips' position only.
                Transform hips = System.Array.Find(skin.bones, b => b != null && b.name == "mixamorig:Hips");
                model.transform.position += pawnSkin.bones[index["mixamorig:Hips"]].position - hips.position;

                Mesh source = skin.sharedMesh;
                Matrix4x4 toPawn = pawnSkin.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                Vector3[] vertices = source.vertices, normals = source.normals;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = toPawn.MultiplyPoint3x4(vertices[i]);
                    if (normals.Length > 0) normals[i] = toPawn.MultiplyVector(normals[i]).normalized;
                }
                int hipsIndex = index["mixamorig:Hips"];
                var remap = new int[skin.bones.Length];
                for (int i = 0; i < remap.Length; i++)
                    remap[i] = skin.bones[i] != null && index.TryGetValue(skin.bones[i].name, out int j) ? j : hipsIndex;
                BoneWeight[] weights = source.boneWeights;
                for (int i = 0; i < weights.Length; i++)
                {
                    BoneWeight w = weights[i];
                    w.boneIndex0 = remap[w.boneIndex0];
                    w.boneIndex1 = remap[w.boneIndex1];
                    w.boneIndex2 = remap[w.boneIndex2];
                    w.boneIndex3 = remap[w.boneIndex3];
                    weights[i] = w;
                }

                // Filled in place: a new mesh copied over the old asset once left the old vertices in it.
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                bool created = mesh == null;
                if (created) mesh = new Mesh { name = piece + "_Skinned" };
                else mesh.Clear();
                mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.vertices = vertices;
                if (normals.Length > 0) mesh.normals = normals;
                mesh.uv = source.uv;
                mesh.subMeshCount = 1;
                mesh.SetTriangles(source.triangles, 0);
                mesh.boneWeights = weights;
                mesh.bindposes = pawnSkin.sharedMesh.bindposes;
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                if (created) AssetDatabase.CreateAsset(mesh, meshPath);
                else EditorUtility.SetDirty(mesh);

                var holder = new GameObject(piece);
                try
                {
                    var look = holder.AddComponent<SkinnedMeshRenderer>();
                    look.sharedMesh = mesh;
                    look.sharedMaterial = material;
                    foreach (string side in marks ? new[] { "Left", "Right" } : new string[0])
                    {
                        if (!Mark(holder.transform, side, vertices, weights, index, pawnSkin.transform, pawn))
                            return piece + " has no " + side.ToLowerInvariant() + " arm and mitten";
                        if (!MarkFoot(holder.transform, side, vertices, weights, index, pawnSkin.transform, pawn))
                            return piece + " has no " + side.ToLowerInvariant() + " foot";
                    }
                    PrefabUtility.SaveAsPrefabAsset(holder, prefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(holder);
                }
                return $"{piece} {vertices.Length} vertices, lowest point {LowestPoint(pawnSkin, mesh, pawn):0.000} m";
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        // Where this side's stub leaves the body (a little in from its inner end, as the pawn's own arm pivot
        // is) and where its mitten begins, both on the stub's middle line.
        static bool Mark(Transform holder, string side, Vector3[] vertices, BoneWeight[] weights,
            Dictionary<string, int> index, Transform skin, Transform pawn)
        {
            int arm = index["mixamorig:" + side + "Arm"], hand = index["mixamorig:" + side + "Hand"];
            float armIn = float.MaxValue, handIn = float.MaxValue, sideX = 0f;
            var stub = new Bounds();
            bool any = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (weights[i].weight0 < 0.5f) continue;
                Vector3 p = pawn.InverseTransformPoint(skin.TransformPoint(vertices[i]));
                if (weights[i].boneIndex0 == arm)
                {
                    armIn = Mathf.Min(armIn, Mathf.Abs(p.x));
                    sideX += p.x;
                    if (any) stub.Encapsulate(p);
                    else stub = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else if (weights[i].boneIndex0 == hand) handIn = Mathf.Min(handIn, Mathf.Abs(p.x));
            }
            if (!any || handIn == float.MaxValue) return false;
            float sign = Mathf.Sign(sideX);
            var shoulder = new GameObject(side + "Shoulder").transform;
            shoulder.SetParent(holder, false);
            shoulder.localPosition = new Vector3(sign * (armIn + 0.012f), stub.center.y, stub.center.z);
            var wrist = new GameObject(side + "Wrist").transform;
            wrist.SetParent(holder, false);
            wrist.localPosition = new Vector3(sign * handIn, stub.center.y, stub.center.z);
            return true;
        }

        // The middle of this side's foot, which it turns about.
        static bool MarkFoot(Transform holder, string side, Vector3[] vertices, BoneWeight[] weights,
            Dictionary<string, int> index, Transform skin, Transform pawn)
        {
            int foot = index["mixamorig:" + side + "Foot"];
            var bounds = new Bounds();
            bool any = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (weights[i].weight0 < 0.5f || weights[i].boneIndex0 != foot) continue;
                Vector3 p = pawn.InverseTransformPoint(skin.TransformPoint(vertices[i]));
                if (any) bounds.Encapsulate(p);
                else bounds = new Bounds(p, Vector3.zero);
                any = true;
            }
            if (!any) return false;
            var mark = new GameObject(side + "Foot").transform;
            mark.SetParent(holder, false);
            mark.localPosition = bounds.center;
            return true;
        }

        // Worn by the pawn at rest, the piece's lowest point is on the pawn's ground: 0.
        static float LowestPoint(SkinnedMeshRenderer pawnSkin, Mesh mesh, Transform pawn)
        {
            Mesh own = pawnSkin.sharedMesh;
            pawnSkin.sharedMesh = mesh;
            var baked = new Mesh();
            pawnSkin.BakeMesh(baked, true);
            pawnSkin.sharedMesh = own;
            float lowest = float.MaxValue;
            foreach (Vector3 v in baked.vertices)
                lowest = Mathf.Min(lowest, pawn.InverseTransformPoint(pawnSkin.transform.TransformPoint(v)).y);
            Object.DestroyImmediate(baked);
            return lowest;
        }
    }
}
