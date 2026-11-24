using System.Collections;
using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    /// <summary>
    /// JY-gfx (2026-11-24): small 3D things on the outdoor paving, as Mario Kart World and Donkey Kong Bananza scatter
    /// them (JY: "floors first"): tufts of grass and weeds in the joints along wall feet, kerbs, planters and steps, a
    /// few rounded pebbles there, and the odd weed out on open paving. Rule-based from the section's own colliders,
    /// once per section when it is switched on: points on a 30 cm grid looked down on; a point on paving (a floor
    /// whose material says paving, flagstone, cobble...) with something standing within a step or two of it is an edge.
    /// No colliders of their own (pictures only); merged into a few meshes per 12 m cell; the tufts wear the grass's
    /// shader (wind, far fade), the pebbles Gfx Lit with the limestone set. Off with the upgrade (GameGfx).
    /// </summary>
    public static class GfxGround
    {
        public static bool On = true;
        /// <summary>Everything's density times this (1 as tuned).</summary>
        public static float Density = 1f;

        const float Step = .3f, Cell = 12f;
        static readonly string[] Paving = { "paving", "flag", "cobble", "courtyard", "gravel", "path" };
        static readonly HashSet<int> done = new HashSet<int>();
        static readonly List<GameObject> made = new List<GameObject>();
        static readonly Collider[] near = new Collider[32];
        static Material[] tuftLooks, pebbleLooks;
        static Vector3[] sphere;
        static int[] sphereTriangles;
        static bool shown = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            done.Clear();
            made.Clear();
            shown = true;
            var go = new GameObject("Gfx ground (JY-gfx)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        /// <summary>A section to dress (GfxSections' watch hands every new one in); spread over a few frames.</summary>
        public static void Request(PawnRushSection section)
        {
            if (section == null || done.Contains(section.GetInstanceID())) return;
            if (runner != null) runner.queue.Enqueue(section);
        }

        /// <summary>Dresses a section at once (the picture tool).</summary>
        public static void Now(PawnRushSection section)
        {
            if (section == null || !done.Add(section.GetInstanceID())) return;
            var e = Scatter(section, int.MaxValue);
            while (e.MoveNext()) { }
        }

        // ---------------------------------------------------------------- where

        static bool IsPaving(Material m)
        {
            if (m == null) return false;
            var name = m.name.ToLowerInvariant();
            foreach (var w in Paving) if (name.Contains(w)) return true;
            return false;
        }

        // Whether paving is drawn at this floor point: one of the section's paving renderers holds it in its box, its
        // top within 8 cm (the drawn floor is often a box or a mesh laid over a collider of its own).
        static bool OnPaving(Vector3 p, List<Bounds> paving)
        {
            foreach (var b in paving)
                if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z && Mathf.Abs(b.max.y - p.y) < .08f) return true;
            return false;
        }

        // How far (horizontally) the nearest thing standing beside this floor point is: a wall, a kerb or step at least
        // 10 cm up, a planter, a pot; infinity when nothing is within 35 cm of a point 45 cm up.
        static float Edge(Vector3 p, Collider floor)
        {
            var probe = p + Vector3.up * .45f;
            int n = Physics.OverlapSphereNonAlloc(probe, .35f, near, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var c = near[i];
                if (c == floor || c.attachedRigidbody != null) continue;
                var q = c is MeshCollider mc && !mc.convex ? c.bounds.ClosestPoint(probe) : c.ClosestPoint(probe);
                q.y = p.y;
                best = Mathf.Min(best, Vector3.Distance(q, p));
            }
            return best;
        }

        // ---------------------------------------------------------------- what

        struct Tuft { public Vector3 at; public float size; public int look; }
        struct Pebble { public Vector3 at; public float size; public int look; }

        static IEnumerator Scatter(PawnRushSection section, int perFrame)
        {
            // Course 1 only: the castle's outside (Early_ sections). Weeds have no place on the halls' floors.
            if (!On || !GameGfx.InScope() || !section.name.Contains("Early_")) yield break;
            var bounds = new Bounds();
            bool any = false;
            var paving = new List<Bounds>();
            foreach (var r in section.GetComponentsInChildren<MeshRenderer>(false))
            {
                var b = r.bounds;
                if (b.size.x > 300f || b.size.z > 300f) continue;
                if (!any) { bounds = b; any = true; }
                else bounds.Encapsulate(b);
                foreach (var m in r.sharedMaterials)
                    if (IsPaving(m)) { paving.Add(b); break; }
            }
            if (!any || paving.Count == 0) yield break;
            var random = new System.Random(section.name.GetHashCode() ^ Mathf.RoundToInt(bounds.center.x * 7f + bounds.center.z * 3f));
            var tufts = new List<Tuft>();
            var pebbles = new List<Pebble>();
            int counted = 0;
            float top = bounds.max.y + 1f, reach = bounds.size.y + 2f;
            for (float x = bounds.min.x; x < bounds.max.x; x += Step)
                for (float z = bounds.min.z; z < bounds.max.z; z += Step)
                {
                    if (++counted % perFrame == 0) yield return null;
                    var from = new Vector3(x + (float)(random.NextDouble() - .5) * Step, top, z + (float)(random.NextDouble() - .5) * Step);
                    if (!Physics.Raycast(from, Vector3.down, out var hit, reach, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < .9f || hit.collider.attachedRigidbody != null) continue;
                    if (!OnPaving(hit.point, paving)) continue;
                    float edge = Edge(hit.point, hit.collider);
                    float pTuft = edge < .35f ? .45f : edge < .8f ? .09f : .003f;
                    float pPebble = edge < .35f ? .1f : edge < .8f ? .025f : .001f;
                    if (random.NextDouble() < pTuft * Density)
                        tufts.Add(new Tuft { at = hit.point, size = Mathf.Lerp(1f, 1.7f, (float)random.NextDouble()) * (edge < .35f ? 1f : .75f), look = random.NextDouble() < .7 ? 0 : 1 });
                    if (random.NextDouble() < pPebble * Density)
                    {
                        // A little cluster: one to four, the first the biggest.
                        int count = 1 + random.Next(4);
                        for (int k = 0; k < count; k++)
                        {
                            var at = hit.point + new Vector3((float)(random.NextDouble() - .5) * .25f, 0f, (float)(random.NextDouble() - .5) * .25f);
                            pebbles.Add(new Pebble { at = at, size = Mathf.Lerp(.03f, .09f, (float)random.NextDouble()) * (k == 0 ? 1.3f : 1f), look = random.Next(3) });
                        }
                    }
                }
            Build(section, tufts, pebbles, random);
            Debug.Log($"[GfxGround] {section.name}: {tufts.Count} tufts, {pebbles.Count} pebbles");
        }

        // ---------------------------------------------------------------- how

        static void Looks()
        {
            if (tuftLooks != null && tuftLooks[0] != null) return;
            var grass = Resources.Load<Material>("Gfx/GfxGrass");
            var lit = Shader.Find("ChessFight/Gfx Lit");
            tuftLooks = new Material[2];
            // Fresh green and a drier olive (weeds in stone joints are not lawn).
            tuftLooks[0] = Tint(grass, "Tuft fresh", new Color(.16f, .26f, .08f), new Color(.46f, .62f, .2f));
            tuftLooks[1] = Tint(grass, "Tuft dry", new Color(.24f, .25f, .11f), new Color(.62f, .6f, .3f));
            pebbleLooks = new Material[3];
            var colours = new[] { new Color(.8f, .76f, .68f), new Color(.6f, .6f, .58f), new Color(.72f, .63f, .52f) };
            for (int i = 0; i < 3; i++)
            {
                if (lit == null) break;
                var m = new Material(lit) { name = "Pebble " + i + " (JY-gfx)", hideFlags = HideFlags.DontSave };
                m.SetColor("_BaseColor", colours[i]);
                m.SetFloat("_Smoothness", .32f);
                m.SetFloat("_GfxSet", 0f);
                m.SetFloat("_GfxSetScale", 1f);
                pebbleLooks[i] = m;
            }
        }

        static Material Tint(Material grass, string name, Color root, Color tip)
        {
            if (grass == null) return null;
            var m = new Material(grass) { name = name + " (JY-gfx)", hideFlags = HideFlags.DontSave };
            m.SetColor("_Base", root);
            m.SetColor("_Tip", tip);
            return m;
        }

        static void Build(PawnRushSection section, List<Tuft> tufts, List<Pebble> pebbles, System.Random random)
        {
            Looks();
            var cells = new Dictionary<(Vector2Int, int), Builder>();
            Builder CellOf(Vector3 p, int look)
            {
                var key = (new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell)), look);
                if (!cells.TryGetValue(key, out var b)) cells[key] = b = new Builder();
                return b;
            }
            foreach (var t in tufts) CellOf(t.at, t.look).Tuft(t.at, t.size, random);
            foreach (var p in pebbles) CellOf(p.at, 10 + p.look).Pebble(p.at, p.size, random);
            foreach (var kv in cells)
            {
                int look = kv.Key.Item2;
                var material = look >= 10 ? pebbleLooks[look - 10] : tuftLooks[look];
                if (material == null) continue;
                var go = new GameObject(look >= 10 ? "Pebbles (JY-gfx)" : "Tufts (JY-gfx)");
                go.layer = section.gameObject.layer;
                go.transform.SetParent(section.transform, true);
                go.AddComponent<MeshFilter>().sharedMesh = kv.Value.Mesh(look >= 10);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = look >= 10 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                go.SetActive(shown);
                made.Add(go);
            }
        }

        static void Sphere()
        {
            if (sphere != null) return;
            // An icosahedron split once: 42 points, 80 faces.
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            var f = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out var i)) return i;
                v.Add((v[a] + v[b]) * .5f);
                return mid[key] = v.Count - 1;
            }
            var g = new List<int>();
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                g.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            sphere = v.ConvertAll(p => p.normalized).ToArray();
            sphereTriangles = g.ToArray();
        }

        sealed class Builder
        {
            readonly List<Vector3> positions = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<Color32> colours = new List<Color32>();
            readonly List<int> indices = new List<int>();

            // A tuft: blades fanning out from one root, leaning away from its middle, the outer ones lower.
            public void Tuft(Vector3 root, float size, System.Random random)
            {
                int blades = 9 + random.Next(8);
                float turn0 = (float)(random.NextDouble() * Mathf.PI * 2.0);
                for (int b = 0; b < blades; b++)
                {
                    float turn = turn0 + b * (Mathf.PI * 2f / blades) + (float)(random.NextDouble() - .5) * .6f;
                    var outward = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
                    bool inner = random.NextDouble() < .45;
                    float height = size * (inner ? Mathf.Lerp(.12f, .2f, (float)random.NextDouble()) : Mathf.Lerp(.06f, .13f, (float)random.NextDouble()));
                    float lean = height * (inner ? Mathf.Lerp(.15f, .4f, (float)random.NextDouble()) : Mathf.Lerp(.5f, .9f, (float)random.NextDouble()));
                    var side = Vector3.Cross(Vector3.up, outward);
                    var start = root + outward * (.012f * size) + Vector3.down * .005f;
                    float w = .011f * (.8f + .4f * (float)random.NextDouble()) * Mathf.Sqrt(size);
                    var mid = start + Vector3.up * (height * .55f) + outward * (lean * .35f);
                    var tip = start + Vector3.up * height + outward * lean;
                    byte shade = (byte)random.Next(256), tall = (byte)Mathf.Clamp(Mathf.RoundToInt(height / .5f * 255f), 0, 255);
                    var colour = new Color32(shade, tall, 0, 255);
                    int i = positions.Count;
                    positions.Add(start - side * (w * .5f)); uvs.Add(new Vector2(0f, 0f));
                    positions.Add(start + side * (w * .5f)); uvs.Add(new Vector2(1f, 0f));
                    positions.Add(mid - side * (w * .35f)); uvs.Add(new Vector2(0f, .55f));
                    positions.Add(mid + side * (w * .35f)); uvs.Add(new Vector2(1f, .55f));
                    positions.Add(tip); uvs.Add(new Vector2(.5f, 1f));
                    // Lit as the ground under them is (as the lawn's blades are), leaning a little toward their side.
                    var n = (Vector3.up * 2f + outward).normalized;
                    for (int k = 0; k < 5; k++)
                    {
                        normals.Add(n);
                        colours.Add(colour);
                    }
                    indices.Add(i); indices.Add(i + 2); indices.Add(i + 1);
                    indices.Add(i + 1); indices.Add(i + 2); indices.Add(i + 3);
                    indices.Add(i + 2); indices.Add(i + 4); indices.Add(i + 3);
                }
            }

            // A pebble: a squashed, gently lumpy round stone, a third of it in the joint.
            public void Pebble(Vector3 at, float size, System.Random random)
            {
                Sphere();
                float yaw = (float)(random.NextDouble() * 360.0);
                var rot = Quaternion.Euler((float)(random.NextDouble() - .5) * 20f, yaw, (float)(random.NextDouble() - .5) * 20f);
                var scale = new Vector3(size * Mathf.Lerp(.85f, 1.25f, (float)random.NextDouble()), size * Mathf.Lerp(.45f, .65f, (float)random.NextDouble()), size * Mathf.Lerp(.8f, 1.1f, (float)random.NextDouble()));
                // Lumps: three slow waves over the sphere's directions.
                var waves = new Vector4[3];
                for (int k = 0; k < 3; k++)
                    waves[k] = new Vector4((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 6.28f);
                int i0 = positions.Count;
                foreach (var d in sphere)
                {
                    float lump = 0f;
                    foreach (var w in waves) lump += Mathf.Sin(Vector3.Dot(d, new Vector3(w.x, w.y, w.z)) * 2.2f + w.w);
                    var p = d * (1f + .07f * lump);
                    p = new Vector3(p.x * scale.x, p.y * scale.y, p.z * scale.z);
                    positions.Add(at + rot * p + Vector3.up * (scale.y * .35f));
                    normals.Add(Vector3.zero);
                    uvs.Add(Vector2.zero);
                    colours.Add(new Color32(255, 255, 255, 255));
                }
                foreach (var t in sphereTriangles) indices.Add(i0 + t);
            }

            public Mesh Mesh(bool smooth)
            {
                var mesh = new Mesh { name = "Ground detail (JY-gfx)", hideFlags = HideFlags.DontSave };
                if (positions.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(positions);
                mesh.SetUVs(0, uvs);
                mesh.SetColors(colours);
                mesh.SetTriangles(indices, 0, true);
                if (smooth) mesh.RecalculateNormals();
                else mesh.SetNormals(normals);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                return mesh;
            }
        }

        // ---------------------------------------------------------------- upkeep

        static Runner runner;

        sealed class Runner : MonoBehaviour
        {
            public readonly Queue<PawnRushSection> queue = new Queue<PawnRushSection>();
            bool busy;

            void Awake() => runner = this;

            void Update()
            {
                bool want = On && GameGfx.Upgrade;
                if (want != shown)
                {
                    shown = want;
                    made.RemoveAll(go => go == null);
                    foreach (var go in made) go.SetActive(shown);
                }
                if (!busy && queue.Count > 0) StartCoroutine(Next(queue.Dequeue()));
            }

            IEnumerator Next(PawnRushSection section)
            {
                busy = true;
                // Dressed already (Now) or gone: nothing to do.
                if (section != null && done.Add(section.GetInstanceID())) yield return Scatter(section, 1500);
                busy = false;
            }
        }
    }
}
