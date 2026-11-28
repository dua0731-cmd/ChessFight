using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // R904 (user 11-28: "대연회장 깜빡거리는 부분 외곽에서 보이고 내부에서도 몇개 있어 고쳐주고 왕의 서재도 깜빡거리는 부분
    // 지워주고"): faces of the hung models lying on other faces (the dais's marble on the hall's wall, a waltz stand on the
    // dance floor, the chess deck on the typewriter table at the library's doors, the ceiling's frieze on its coffers)
    // flicker. CoplanarScan (editor, Tools/Run-CoplanarScan.ps1 -Bake) finds them in Play and writes, for each face that
    // should come forward, its renderer's path under the dressing's object, its submesh, its place in that submesh's
    // indices and how far to move it (in the mesh's own frame) to a Resources text file. Once the models hang, each listed
    // renderer gets its own copy of its mesh with those faces lifted; a corner a lifted face shares with a face that stays
    // is copied, so only the lifted face moves (the gap it leaves shows the face it lay on). Colliders stay the builder's.
    //   line: <path>\t<submesh>\t<index of the face's first corner>\t<x> <y> <z>
    public static class CoplanarNudges
    {
        // Off: as the models were hung (the scan's bake reads them so).
        public static bool Enabled = true;

        public static int Apply(Transform root, string resource)
        {
            if (!Enabled || root == null) return 0;
            var text = Resources.Load<TextAsset>(resource);
            return text == null ? 0 : ApplyText(root, text.text);
        }

        // The lines themselves (the scan applies each round of its bake before the file exists). Returns the faces moved.
        public static int ApplyText(Transform root, string text)
        {
            var byPath = new Dictionary<string, List<(int sub, int k, Vector3 move)>>();
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim('\r', ' ');
                if (line.Length == 0 || line[0] == '#') continue;
                var cols = line.Split('\t');
                if (cols.Length != 4) continue;
                var v = cols[3].Split(' ');
                if (v.Length != 3 || !int.TryParse(cols[1], out int sub) || !int.TryParse(cols[2], out int k)) continue;
                if (!byPath.TryGetValue(cols[0], out var list)) byPath[cols[0]] = list = new List<(int, int, Vector3)>();
                list.Add((sub, k, new Vector3(Parse(v[0]), Parse(v[1]), Parse(v[2]))));
            }
            int moved = 0;
            foreach (var kv in byPath)
            {
                var found = new List<Transform>();
                Find(root, kv.Key.Split('/'), 0, found);
                foreach (var t in found)
                {
                    if (!t.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) continue;
                    var mesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
                    mesh.name = mf.sharedMesh.name;
                    moved += LiftFaces(mesh, kv.Value);
                    mf.sharedMesh = mesh;
                }
            }
            return moved;
        }

        static float Parse(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        // Every object along the path; "<name>#k" is only the k-th (from 1) of the siblings of that name (the chess
        // board's two decks, the waiters).
        static void Find(Transform at, string[] path, int depth, List<Transform> found)
        {
            bool last = depth == path.Length - 1;
            string want = path[depth];
            int twin = 0, hash = want.LastIndexOf('#');
            if (hash > 0 && int.TryParse(want.Substring(hash + 1), out int n)) { twin = n; want = want.Substring(0, hash); }
            int seen = 0;
            for (int i = 0; i < at.childCount; i++)
            {
                var c = at.GetChild(i);
                if (c.name != want) continue;
                if (twin > 0 && ++seen != twin) continue;
                if (last) found.Add(c);
                else Find(c, path, depth + 1, found);
            }
        }

        // The listed faces of `mesh` moved by their vectors (mesh frame). Shared corners are copied first.
        public static int LiftFaces(Mesh mesh, IList<(int sub, int k, Vector3 move)> faces)
        {
            int n = mesh.vertexCount;
            if (n == 0 || mesh.blendShapeCount > 0) return 0;
            var triangles = new int[mesh.subMeshCount][];
            for (int s = 0; s < mesh.subMeshCount; s++) triangles[s] = mesh.GetTopology(s) == MeshTopology.Triangles ? mesh.GetTriangles(s) : null;
            var lifted = new HashSet<(int, int)>();
            foreach (var f in faces)
                if (f.sub >= 0 && f.sub < triangles.Length && triangles[f.sub] != null && f.k >= 0 && f.k + 2 < triangles[f.sub].Length)
                    lifted.Add((f.sub, f.k));
            if (lifted.Count == 0) return 0;
            // corners a face that stays also uses
            var kept = new bool[n];
            for (int s = 0; s < triangles.Length; s++)
            {
                var idx = triangles[s];
                if (idx == null) continue;
                for (int k = 0; k + 2 < idx.Length; k += 3)
                    if (!lifted.Contains((s, k))) { kept[idx[k]] = true; kept[idx[k + 1]] = true; kept[idx[k + 2]] = true; }
            }
            var verts = new List<Vector3>(n);
            mesh.GetVertices(verts);
            var normals = new List<Vector3>(); mesh.GetNormals(normals);
            var tangents = new List<Vector4>(); mesh.GetTangents(tangents);
            var colors = new List<Color>(); mesh.GetColors(colors);
            var uvs = new List<Vector4>[8];
            for (int c = 0; c < 8; c++) { uvs[c] = new List<Vector4>(); mesh.GetUVs(c, uvs[c]); }
            var copy = new Dictionary<int, int>();
            var moves = new Dictionary<int, Vector3>();
            int count = 0;
            foreach (var f in faces)
            {
                if (!lifted.Contains((f.sub, f.k))) continue;
                var idx = triangles[f.sub];
                for (int j = f.k; j < f.k + 3; j++)
                {
                    int v = idx[j];
                    if (kept[v])
                    {
                        if (!copy.TryGetValue(v, out int c))
                        {
                            c = verts.Count;
                            copy[v] = c;
                            verts.Add(verts[v]);
                            if (normals.Count >= n) normals.Add(normals[v]);
                            if (tangents.Count >= n) tangents.Add(tangents[v]);
                            if (colors.Count >= n) colors.Add(colors[v]);
                            foreach (var uv in uvs) if (uv.Count >= n) uv.Add(uv[v]);
                        }
                        idx[j] = c;
                        v = c;
                    }
                    moves[v] = moves.TryGetValue(v, out var have) ? Combine(have, f.move) : f.move;
                }
                count++;
            }
            foreach (var kv in moves) verts[kv.Key] += kv.Value;
            if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            if (normals.Count == verts.Count) mesh.SetNormals(normals);
            if (tangents.Count == verts.Count) mesh.SetTangents(tangents);
            if (colors.Count == verts.Count) mesh.SetColors(colors);
            for (int c = 0; c < 8; c++) if (uvs[c].Count == verts.Count) mesh.SetUVs(c, uvs[c]);
            for (int s = 0; s < triangles.Length; s++) if (triangles[s] != null) mesh.SetTriangles(triangles[s], s, false);
            mesh.RecalculateBounds();
            return count;
        }

        // Two moves on one corner: the first kept, the second's shortfall along its own direction added.
        public static Vector3 Combine(Vector3 have, Vector3 move)
        {
            float len = move.magnitude;
            if (len < 1e-7f) return have;
            var dir = move / len;
            float along = Vector3.Dot(have, dir);
            return along < len ? have + dir * (len - along) : have;
        }
    }
}
