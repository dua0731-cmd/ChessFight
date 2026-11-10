using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>
    /// Test copy only: the pawn wears the new model (Art/PawnNew/Pawn_Rigged.fbx, weighted part by part on the pawn's
    /// own Mixamo skeleton) instead of its old skin. The physics bodies, joints and colliders stay the old pawn's.
    /// The same steps as PieceModelBaker on main: bind poses, line the armatures up, the vertices into the pawn
    /// skin's space, bone indices by name, the pawn skin's bind poses.
    /// </summary>
    public static class PawnLookSwap
    {
        const string PawnPrefab = "Assets/ChessFight/RagdollLab/Prefabs/RagdollPawn.prefab";
        const string NewFbx = "Assets/ChessFight/RagdollLab/Art/PawnNew/Pawn_Rigged.fbx";
        const string Dir = "Assets/ChessFight/RagdollLab/Art/PawnNew";

        public static void SwapAndBuild()
        {
            try
            {
                Swap();
                RagdollLabBuilder.BuildPlayer();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Swap()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(NewFbx);
            if (mi == null) throw new Exception("Missing " + NewFbx);
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.isReadable = true;
            mi.optimizeGameObjects = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.SaveAndReimport();

            var skinMat = PieceSkin();

            var pawn = PrefabUtility.LoadPrefabContents(PawnPrefab);
            try
            {
                var pawnSkin = pawn.GetComponent<RagdollPawn>().skin;
                BindPose(pawnSkin);
                var index = new Dictionary<string, int>();
                for (int i = 0; i < pawnSkin.bones.Length; i++)
                    if (pawnSkin.bones[i] != null) index[pawnSkin.bones[i].name] = i;

                var model = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NewFbx));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model, pawn.scene);
                try
                {
                    // Turned as the old visual is (the builder turns it round when its toes point back).
                    Transform visual = pawn.transform.Find("Visual");
                    if (visual != null) model.transform.rotation = visual.rotation;
                    var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
                    BindPose(skin);
                    Transform hips = Array.Find(skin.bones, b => b != null && b.name == "mixamorig:Hips");
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
                    var mesh = new Mesh { name = "PawnNew_Skinned" };
                    mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                    mesh.vertices = vertices;
                    if (normals.Length > 0) mesh.normals = normals;
                    // One submesh, one material: the colours are the vertex colours the rig script painted.
                    var allTris = new List<int>();
                    for (int s = 0; s < source.subMeshCount; s++) allTris.AddRange(source.GetTriangles(s));
                    mesh.subMeshCount = 1;
                    mesh.SetTriangles(allTris, 0);
                    Color[] colors = source.colors;
                    if (colors.Length == vertices.Length) mesh.colors = colors;
                    else Debug.LogWarning("[PawnLookSwap] the model has no vertex colours");
                    // Rest positions in the pawn root's space (y up), for the shader's painted patches.
                    Matrix4x4 toRoot = pawn.transform.worldToLocalMatrix * pawnSkin.transform.localToWorldMatrix;
                    var rest = new List<Vector3>(vertices.Length);
                    foreach (Vector3 v in vertices) rest.Add(toRoot.MultiplyPoint3x4(v));
                    mesh.SetUVs(2, rest);
                    // Rest normals in the root's space (UV3), and as the mesh's tangents the rest frame's tangent
                    // (up x normal) in the skin's space: the facet shader snaps the rest normal and hands it to the
                    // lighting in that frame, so the facets ride along with every bone.
                    var restN = new List<Vector3>(vertices.Length);
                    var tangents = new Vector4[vertices.Length];
                    Matrix4x4 toSkin = toRoot.inverse;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 n0 = toRoot.MultiplyVector(normals[i]).normalized;
                        restN.Add(n0);
                        Vector3 t0 = Vector3.Cross(Vector3.up, n0);
                        t0 = t0.sqrMagnitude > 1e-6f ? t0.normalized : Vector3.right;
                        Vector3 t = toSkin.MultiplyVector(t0).normalized;
                        tangents[i] = new Vector4(t.x, t.y, t.z, 1f);
                    }
                    mesh.SetUVs(3, restN);
                    mesh.tangents = tangents;
                    var mats = new[] { skinMat };
                    mesh.boneWeights = weights;
                    mesh.bindposes = pawnSkin.sharedMesh.bindposes;
                    mesh.RecalculateBounds();
                    string meshPath = Dir + "/PawnNew_Skinned.asset";
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(mesh, meshPath);
                    pawnSkin.sharedMesh = mesh;
                    pawnSkin.sharedMaterials = mats;
                    Debug.Log($"[PawnLookSwap] {vertices.Length} vertices, colours {colors.Length}, {source.subMeshCount} submeshes merged, rest y {rest[0].y:F3}, visual turned {(visual != null ? visual.localEulerAngles.ToString() : "-")}");
                }
                finally
                {
                    Object.DestroyImmediate(model);
                }
                PrefabUtility.SaveAsPrefabAsset(pawn, PawnPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(pawn);
            }
            AssetDatabase.SaveAssets();
        }

        static Material PieceSkin()
        {
            string look = "facets";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-pieceLook") look = args[i + 1];
            string shaderName = look == "painted" ? "ChessFight/PiecePainted" : look == "skin" ? "ChessFight/PieceSkin" : "ChessFight/PieceFacets";
            string path = Dir + "/PawnNew_PieceSkin.mat";
            AssetDatabase.DeleteAsset(path);
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new Exception("Missing shader " + shaderName);
            Debug.Log("[PawnLookSwap] look " + look + " -> " + shaderName);
            var m = new Material(shader);
            m.SetColor("_TeamColor", new Color(0.314f, 0.376f, 0.58f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material Mat(string name, Color color)
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard")) { color = color };
                m.SetFloat("_Glossiness", 0.3f);
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        static void BindPose(SkinnedMeshRenderer skin)
        {
            var bones = skin.bones;
            var bind = skin.sharedMesh.bindposes;
            Matrix4x4 m = skin.transform.localToWorldMatrix;
            var order = new List<int>();
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) order.Add(i);
            order.Sort((a, b) => Depth(bones[a]).CompareTo(Depth(bones[b])));
            foreach (int i in order)
            {
                Matrix4x4 w = m * bind[i].inverse;
                bones[i].SetPositionAndRotation(w.GetColumn(3), w.rotation);
            }
        }

        static int Depth(Transform t)
        {
            int d = 0;
            for (; t.parent != null; t = t.parent) d++;
            return d;
        }
    }
}
