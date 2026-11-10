# Test copy only: rest normals (UV3) and rest-frame tangents for the facet shader, and the look picked on the command
# line (-pieceLook facets|painted|skin).
import os, sys
root = sys.argv[1]
p = os.path.join(root, "Assets/ChessFight/RagdollLab/Editor/PawnLookSwap.cs")
s = open(p, encoding="utf-8").read()

def rep(old, new):
    global s
    if new in s:
        return
    assert old in s, old[:90]
    s = s.replace(old, new)

rep("""                    mesh.SetUVs(2, rest);
""", """                    mesh.SetUVs(2, rest);
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
""")
rep("""        static Material PieceSkin()
        {
            string path = Dir + "/PawnNew_PieceSkin.mat";
            AssetDatabase.DeleteAsset(path);
            var shader = Shader.Find("ChessFight/PieceSkin");
            if (shader == null) throw new Exception("Missing shader ChessFight/PieceSkin");""",
"""        static Material PieceSkin()
        {
            string look = "facets";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-pieceLook") look = args[i + 1];
            string shaderName = look == "painted" ? "ChessFight/PiecePainted" : look == "skin" ? "ChessFight/PieceSkin" : "ChessFight/PieceFacets";
            string path = Dir + "/PawnNew_PieceSkin.mat";
            AssetDatabase.DeleteAsset(path);
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new Exception("Missing shader " + shaderName);
            Debug.Log("[PawnLookSwap] look " + look + " -> " + shaderName);""")
open(p, "w", encoding="utf-8").write(s)
print("patched")
