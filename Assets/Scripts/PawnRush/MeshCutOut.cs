using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R340 (승규 11-04 "계단 뒤에 올라간 후 그 공간에 난간 지워주고"): takes out of a model's mesh the separate pieces
    // (a baluster, a post, a hand rail - triangles joined through shared corners) whose middle lies in one of the boxes,
    // given in `frame`'s space. For a model that merges many things in one mesh (the grand stair hall's balustrades),
    // when there is no Blender to hand to re-export it. Cut in Play only, in a copy that is never saved: the scene keeps
    // the model's own mesh. The Editor reads any imported mesh; a player build needs the model's Read/Write on.
    [RequireComponent(typeof(MeshFilter))]
    public sealed class MeshCutOut : MonoBehaviour
    {
        [SerializeField] Transform frame;
        [SerializeField] Bounds[] boxes = new Bounds[0];

        public void Configure(Transform space, params Bounds[] cut)
        {
            frame = space;
            boxes = cut;
        }

        void Awake()
        {
            var filter = GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            if (source == null || frame == null || boxes.Length == 0) return;
            if (!source.isReadable && !Application.isEditor)
            {
                Debug.LogWarning("[MeshCutOut] " + source.name + "을(를) 읽을 수 없어(Read/Write 꺼짐) 자르지 않았습니다");
                return;
            }
            var vertices = source.vertices;
            int n = vertices.Length;

            // Pieces: corners at the same place (1 mm) are one corner, triangles join their corners.
            var parent = new int[n];
            var at = new Dictionary<Vector3Int, int>(n);
            for (int i = 0; i < n; i++)
            {
                var v = vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x * 1000f), Mathf.RoundToInt(v.y * 1000f), Mathf.RoundToInt(v.z * 1000f));
                if (at.TryGetValue(key, out int first)) parent[i] = first;
                else { at[key] = i; parent[i] = i; }
            }
            var triangles = new int[source.subMeshCount][];
            for (int s = 0; s < triangles.Length; s++)
            {
                var t = triangles[s] = source.GetTriangles(s);
                for (int k = 0; k < t.Length; k += 3)
                {
                    Join(parent, t[k], t[k + 1]);
                    Join(parent, t[k], t[k + 2]);
                }
            }

            // Each piece's middle in the frame, from its corners' bounds.
            var toFrame = frame.worldToLocalMatrix * transform.localToWorldMatrix;
            var inFrame = new Vector3[n];
            var lo = new Dictionary<int, Vector3>();
            var hi = new Dictionary<int, Vector3>();
            var all = new Bounds();
            for (int i = 0; i < n; i++)
            {
                int root = Find(parent, i);
                var p = inFrame[i] = toFrame.MultiplyPoint3x4(vertices[i]);
                if (i == 0) all = new Bounds(p, Vector3.zero);
                else all.Encapsulate(p);
                if (lo.TryGetValue(root, out var a)) { lo[root] = Vector3.Min(a, p); hi[root] = Vector3.Max(hi[root], p); }
                else { lo[root] = p; hi[root] = p; }
            }
            var gone = new HashSet<int>();
            foreach (var kv in lo)
                if (InBoxes((kv.Value + hi[kv.Key]) * .5f)) gone.Add(kv.Key);

            var cut = Instantiate(source);
            cut.name = source.name + " (cut)";
            cut.hideFlags = HideFlags.DontSave;
            // A piece goes whole when its middle is in a box; any other triangle whose own middle is in one goes too
            // (승규 11-04: the landing's hand and base rails stayed - a long rail the import joined to more than itself).
            int dropped = 0, alone = 0;
            var kept = new List<int>();
            for (int s = 0; s < triangles.Length; s++)
            {
                kept.Clear();
                var t = triangles[s];
                for (int k = 0; k < t.Length; k += 3)
                {
                    if (gone.Contains(Find(parent, t[k]))) { dropped++; continue; }
                    if (InBoxes((inFrame[t[k]] + inFrame[t[k + 1]] + inFrame[t[k + 2]]) / 3f)) { dropped++; alone++; continue; }
                    kept.Add(t[k]);
                    kept.Add(t[k + 1]);
                    kept.Add(t[k + 2]);
                }
                cut.SetTriangles(kept, s, false);
            }
            if (dropped == 0)
            {
                Destroy(cut);
                Debug.LogWarning("[MeshCutOut] " + source.name + ": 상자 안에 아무것도 없어 자르지 않았습니다(틀 안 메시 범위 " + all.min + " ~ " + all.max + ")");
                return;
            }
            cut.RecalculateBounds();
            filter.sharedMesh = cut;
            Debug.Log("[MeshCutOut] " + source.name + ": 조각 " + gone.Count + "개 + 낱 삼각형 " + alone + "개, 모두 삼각형 " + dropped +
                      "개를 뺐습니다(조각 " + lo.Count + "개 중, 틀 안 메시 범위 " + all.min + " ~ " + all.max + ")");
        }

        bool InBoxes(Vector3 p)
        {
            foreach (var box in boxes)
                if (box.Contains(p)) return true;
            return false;
        }

        void OnDestroy()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && (filter.sharedMesh.hideFlags & HideFlags.DontSave) != 0)
                Destroy(filter.sharedMesh);
        }

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        static void Join(int[] parent, int a, int b)
        {
            a = Find(parent, a);
            b = Find(parent, b);
            if (a != b) parent[b] = a;
        }

        void OnDrawGizmosSelected()
        {
            if (frame == null) return;
            Gizmos.matrix = frame.localToWorldMatrix;
            Gizmos.color = new Color(1f, .3f, .2f, .8f);
            foreach (var box in boxes) Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
