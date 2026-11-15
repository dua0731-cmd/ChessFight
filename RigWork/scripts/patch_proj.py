# Test copy only: the new pawn with the PieceSkin shader, the team colour for it, and arms that never stretch.
import os, sys
root = sys.argv[1]

def patch(rel, pairs):
    p = os.path.join(root, rel)
    s = open(p, encoding="utf-8").read()
    for old, new in pairs:
        if new in s:
            continue
        assert old in s, (rel, old[:80])
        s = s.replace(old, new)
    open(p, "w", encoding="utf-8").write(s)
    print("patched", rel)

patch("Assets/ChessFight/RagdollLab/Editor/PawnLookSwap.cs", [
("""            var wood = Mat("PawnNew_Wood", new Color(0.93f, 0.84f, 0.70f));
            var dark = Mat("PawnNew_Dark", new Color(0.10f, 0.085f, 0.075f));
""", """            var skinMat = PieceSkin();
"""),
("""                    mesh.subMeshCount = source.subMeshCount;
                    var mats = new Material[source.subMeshCount];
                    var handsAndFeet = new HashSet<int>();
                    foreach (string b in new[] { "LeftHand", "RightHand", "LeftFoot", "RightFoot" }) handsAndFeet.Add(index["mixamorig:" + b]);
                    for (int s = 0; s < source.subMeshCount; s++)
                    {
                        int[] tris = source.GetTriangles(s);
                        mesh.SetTriangles(tris, s);
                        // A submesh is the dark one if its vertices are mostly the hands' and the feet's.
                        int dk = 0;
                        foreach (int t in tris) if (handsAndFeet.Contains(weights[t].boneIndex0)) dk++;
                        mats[s] = dk > tris.Length / 2 ? dark : wood;
                    }
""", """                    // One submesh, one material: the colours are the vertex colours the rig script painted.
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
                    var mats = new[] { skinMat };
"""),
("""                    Debug.Log($"[PawnLookSwap] {vertices.Length} vertices, {source.subMeshCount} submeshes, visual turned {(visual != null ? visual.localEulerAngles.ToString() : "-")}");""",
 """                    Debug.Log($"[PawnLookSwap] {vertices.Length} vertices, colours {colors.Length}, {source.subMeshCount} submeshes merged, rest y {rest[0].y:F3}, visual turned {(visual != null ? visual.localEulerAngles.ToString() : "-")}");"""),
("""        static Material Mat(string name, Color color)""", """        static Material PieceSkin()
        {
            string path = Dir + "/PawnNew_PieceSkin.mat";
            AssetDatabase.DeleteAsset(path);
            var shader = Shader.Find("ChessFight/PieceSkin");
            if (shader == null) throw new Exception("Missing shader ChessFight/PieceSkin");
            var m = new Material(shader);
            m.SetColor("_TeamColor", new Color(0.314f, 0.376f, 0.58f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material Mat(string name, Color color)"""),
])

patch("Assets/ChessFight/RagdollLab/Scripts/LabGame.cs", [
("""            if (material != null && pawn.skin != null && pawn.skin.sharedMaterials.Length == 1) pawn.skin.sharedMaterial = material;""",
"""            if (material != null && pawn.skin != null)
            {
                Material own = pawn.skin.sharedMaterial;
                if (own != null && own.HasProperty("_TeamColor"))
                {
                    // Test copy: the piece shader paints the collar itself; the red side only changes its colour.
                    bool red = material.name.Contains("P2") || (material.mainTexture != null && material.mainTexture.name.Contains("Red"));
                    if (red)
                    {
                        var m = new Material(own);
                        m.SetColor("_TeamColor", new Color(0.58f, 0.314f, 0.314f));
                        pawn.skin.sharedMaterial = m;
                    }
                }
                else if (pawn.skin.sharedMaterials.Length == 1) pawn.skin.sharedMaterial = material;
            }"""),
])

patch("Assets/ChessFight/RagdollLab/Scripts/RagdollVisualSync.cs", [
("""            var bodies = pawn.bodies;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                Transform a = bodies[e.driverA].transform;
                if (stretch != null && stretch[i] != null)
                {
                    float k = 1f;
                    if (pawn.ArmsStretched)
                        k = Mathf.Max(1f, Vector3.Distance(a.position, bodies[e.driverA + 1].transform.position) / restLength[i]);
                    stretch[i].SetPositionAndRotation(a.position, a.rotation);
                    stretch[i].localScale = new Vector3(k, 1f, 1f);
                    continue;
                }
                Quaternion rotation = e.driverB >= 0
                    ? Quaternion.Slerp(a.rotation, bodies[e.driverB].transform.rotation, e.blend)
                    : a.rotation;
                if (e.setPosition) e.bone.SetPositionAndRotation(a.TransformPoint(e.positionOffset), rotation * e.rotationOffset);""",
"""            var bodies = pawn.bodies;
            Vector3 drawL = Vector3.zero, drawR = Vector3.zero;
            for (int i = 0; i < entries.Length; i++)
            {
                Entry e = entries[i];
                Transform a = bodies[e.driverA].transform;
                if (stretch != null && stretch[i] != null)
                {
                    // Test copy: the arm never stretches. A hand further from its shoulder than at rest (on a wall
                    // the climb puts each palm on a hold up to climbArmReach away) is drawn back along the arm to the
                    // rest length, so the stub keeps the length it has at rest and its root, hidden in the body,
                    // never slides out of it.
                    Vector3 toHand = bodies[e.driverA + 1].transform.position - a.position;
                    float over = toHand.magnitude - restLength[i];
                    Vector3 draw = over > 0f ? -toHand.normalized * over : Vector3.zero;
                    if (e.driverA == (int)BodyId.ArmL) drawL = draw; else drawR = draw;
                    stretch[i].SetPositionAndRotation(a.position, a.rotation);
                    stretch[i].localScale = Vector3.one;
                    continue;
                }
                Quaternion rotation = e.driverB >= 0
                    ? Quaternion.Slerp(a.rotation, bodies[e.driverB].transform.rotation, e.blend)
                    : a.rotation;
                Vector3 shift = e.driverA == (int)BodyId.HandL ? drawL : e.driverA == (int)BodyId.HandR ? drawR : Vector3.zero;
                if (e.setPosition) e.bone.SetPositionAndRotation(a.TransformPoint(e.positionOffset) + shift, rotation * e.rotationOffset);"""),
])
