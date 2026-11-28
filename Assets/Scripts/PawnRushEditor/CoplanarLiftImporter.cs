using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // R904 (user 11-28: "대연회장 깜빡거리는 부분 ... 고쳐주고 왕의 서재도 깜빡거리는 부분 지워주고"): the banquet hall's and
    // the royal library's Blender models had faces lying on other faces of the same FBX (a brass inlay on its walnut, the
    // runner on the tablecloth, two pilasters on one spot), which flicker. On import, of every such pair the face of the
    // smaller part (mesh + material: the inlay, the trim, the runner) is lifted along its normal until it stands Lift in
    // front of the other; every vertex at the same place in that mesh goes with it, so no crack opens. Levels of detail
    // are compared only with their own level. Faces between two objects (a model on the hall's wall, a stand on the dance
    // floor) are CoplanarScan's bake: Gameplay CoplanarNudges.
    public sealed class CoplanarLiftImporter : AssetPostprocessor
    {
        const float Tolerance = .003f, Lift = .004f, MinOverlap = 1e-4f, MinArea = 2e-5f;

        static readonly string[] Folders = { "Assets/Resources/BanquetHall/Models/", "Assets/Resources/RoyalLibrary/Models/" };

        public override uint GetVersion() => 5;

        static bool Mine(string path)
        {
            foreach (var f in Folders) if (path.StartsWith(f, System.StringComparison.Ordinal)) return true;
            return false;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!Mine(assetPath)) return;
            // passes: a lifted face can meet another of the same file
            int lifted = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                int n = Separate(root);
                lifted += n;
                if (n == 0) break;
            }
            if (lifted > 0) Debug.Log("[겹친 면 띄우기] " + assetPath + ": 면 " + lifted + "개를 " + Lift * 1000 + " mm 띄움");
        }

        // One per mesh: Blender's linked copies (the ceiling's coffers) are one mesh under many objects, so a mesh is
        // changed once, its moves kept in its own frame (a move along a face's normal is the same for every copy).
        sealed class Part
        {
            public MeshFilter filter;
            public Vector3[] verts;
            public Dictionary<int, Vector3> moves = new Dictionary<int, Vector3>();   // in the mesh's frame
            public HashSet<int> held = new HashSet<int>();   // vertices of the faces that stay
            public HashSet<(int sub, int k)> lifted = new HashSet<(int sub, int k)>();   // the lifted faces: submesh, place in its indices
        }

        public static int Separate(GameObject root)
        {
            var parts = new List<Part>();
            var partOf = new Dictionary<Mesh, int>();
            var toLocal = new List<Matrix4x4>();   // per object: the root's frame into its mesh's
            var faces = new List<CoplanarFaces.Face>();
            var owner = new List<(int part, int obj, int sub, int k, int i0, int i1, int i2)>();
            var groupArea = new List<float>();
            var rootInv = root.transform.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                if (!partOf.TryGetValue(mesh, out int p))
                {
                    p = parts.Count;
                    partOf[mesh] = p;
                    parts.Add(new Part { filter = mf, verts = mesh.vertices });
                }
                var verts = parts[p].verts;
                var toRoot = rootInv * mf.transform.localToWorldMatrix;
                int obj = toLocal.Count;
                toLocal.Add(toRoot.inverse);
                int lod = CoplanarFaces.LodOf(mf.name);
                var world = new Vector3[verts.Length];
                for (int i = 0; i < world.Length; i++) world[i] = toRoot.MultiplyPoint3x4(verts[i]);
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                    var idx = mesh.GetTriangles(s);
                    int g = groupArea.Count;
                    groupArea.Add(0);
                    for (int k = 0; k + 2 < idx.Length; k += 3)
                    {
                        if (!CoplanarFaces.Make(world[idx[k]], world[idx[k + 1]], world[idx[k + 2]], g, lod, MinArea, out var f)) continue;
                        groupArea[g] += f.area;
                        faces.Add(f);
                        owner.Add((p, obj, s, k, idx[k], idx[k + 1], idx[k + 2]));
                    }
                }
            }
            if (faces.Count < 2) return 0;

            int lifted = 0;
            CoplanarFaces.Pairs(faces, Tolerance, MinOverlap, (i, j, area) =>
            {
                // two copies of one mesh on one spot: editing the mesh moves both alike (and they draw the same anyway)
                if (owner[i].part == owner[j].part && owner[i].obj != owner[j].obj) return;
                var A = faces[i];
                var B = faces[j];
                // the smaller part rises; one part over itself (a doubled face) or two parts as big: the later face
                bool liftA = Mathf.Approximately(groupArea[A.group], groupArea[B.group]) ? i > j : groupArea[A.group] < groupArea[B.group];
                int up = liftA ? i : j, other = liftA ? j : i;
                var U = faces[up];
                float ahead = U.d - faces[other].d;   // how far it already stands in front, along its own normal
                if (ahead >= Lift) return;
                var o = owner[up];
                var move = toLocal[o.obj].MultiplyVector(U.n * (Lift - ahead));
                var part = parts[o.part];
                if (part.lifted.Add((o.sub, o.k))) lifted++;
                Add(part.moves, o.i0, move);
                Add(part.moves, o.i1, move);
                Add(part.moves, o.i2, move);
                var s = owner[other];
                parts[s.part].held.UnionWith(new[] { s.i0, s.i1, s.i2 });
            });
            if (lifted == 0) return 0;

            foreach (var part in parts)
            {
                if (part.moves.Count == 0) continue;
                Unshare(part);
                // every vertex at a moved vertex's place goes with it (split at a seam or a hard edge), but not one of a face that stays
                var at = new Dictionary<Vector3Int, List<int>>();
                for (int i = 0; i < part.verts.Length; i++)
                {
                    var key = Key(part.verts[i]);
                    if (!at.TryGetValue(key, out var list)) at[key] = list = new List<int>();
                    list.Add(i);
                }
                var final = new Dictionary<int, Vector3>();
                foreach (var kv in part.moves)
                    foreach (var twin in at[Key(part.verts[kv.Key])])
                        if (twin == kv.Key || !part.held.Contains(twin)) Add(final, twin, kv.Value);
                var verts = part.verts;
                foreach (var kv in final) verts[kv.Key] += kv.Value;
                var mesh = part.filter.sharedMesh;
                mesh.vertices = verts;
                mesh.RecalculateBounds();
            }
            return lifted;
        }

        // A vertex both a lifted face and a face that stays use (Unity's submeshes share one vertex list: the stage
        // front's gold trim and its walnut) would lift both: the lifted faces get their own copy of it.
        static void Unshare(Part part)
        {
            var mesh = part.filter.sharedMesh;
            var shared = new List<int>();
            foreach (var v in part.moves.Keys) if (part.held.Contains(v)) shared.Add(v);
            if (shared.Count == 0 || mesh.blendShapeCount > 0) return;
            int n = part.verts.Length;
            var copy = new Dictionary<int, int>();
            foreach (var v in shared) copy[v] = n + copy.Count;

            var verts = new List<Vector3>(part.verts);
            foreach (var v in shared) verts.Add(part.verts[v]);
            var normals = new List<Vector3>();
            mesh.GetNormals(normals);
            var tangents = new List<Vector4>();
            mesh.GetTangents(tangents);
            var colors = new List<Color>();
            mesh.GetColors(colors);
            var uvs = new List<Vector4>[8];
            for (int c = 0; c < 8; c++)
            {
                uvs[c] = new List<Vector4>();
                mesh.GetUVs(c, uvs[c]);
            }
            var triangles = new int[mesh.subMeshCount][];
            for (int s = 0; s < mesh.subMeshCount; s++) triangles[s] = mesh.GetTopology(s) == MeshTopology.Triangles ? mesh.GetTriangles(s) : null;
            foreach (var v in shared)
            {
                if (normals.Count >= n) normals.Add(normals[v]);
                if (tangents.Count >= n) tangents.Add(tangents[v]);
                if (colors.Count >= n) colors.Add(colors[v]);
                foreach (var uv in uvs) if (uv.Count >= n) uv.Add(uv[v]);
            }
            foreach (var (sub, k) in part.lifted)
            {
                var idx = triangles[sub];
                for (int j = k; j < k + 3; j++)
                    if (copy.TryGetValue(idx[j], out var c)) idx[j] = c;
            }
            foreach (var v in shared)
            {
                part.moves[copy[v]] = part.moves[v];
                part.moves.Remove(v);
            }

            if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            if (normals.Count == verts.Count) mesh.SetNormals(normals);
            if (tangents.Count == verts.Count) mesh.SetTangents(tangents);
            if (colors.Count == verts.Count) mesh.SetColors(colors);
            for (int c = 0; c < 8; c++) if (uvs[c].Count == verts.Count) mesh.SetUVs(c, uvs[c]);
            for (int s = 0; s < triangles.Length; s++) if (triangles[s] != null) mesh.SetTriangles(triangles[s], s, false);
            part.verts = verts.ToArray();
        }

        static Vector3Int Key(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x * 1e5f), Mathf.RoundToInt(v.y * 1e5f), Mathf.RoundToInt(v.z * 1e5f));

        // Two moves on one vertex: the first kept, the second's shortfall along its own direction added.
        static void Add(Dictionary<int, Vector3> moves, int vertex, Vector3 move)
        {
            if (!moves.TryGetValue(vertex, out var have)) { moves[vertex] = move; return; }
            float len = move.magnitude;
            if (len < 1e-7f) return;
            var dir = move / len;
            float along = Vector3.Dot(have, dir);
            if (along < len) moves[vertex] = have + dir * (len - along);
        }
    }
}
