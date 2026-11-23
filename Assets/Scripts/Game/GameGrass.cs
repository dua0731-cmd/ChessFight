using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24, the graphics upgrade, target Mario Kart World): lawns grow real grass in Play. Every renderer
    /// with a lawn material (its name says "lawn") gets blades scattered over the lawn's up-facing triangles (the mesh
    /// must be readable, as the garden's FBX are), built once into a few meshes by 12 m cells so the far ones are
    /// culled, drawn with Shaders/URP/GfxGrass.shader (Resources/Gfx/GfxGrass.mat) in the lawn's own colours. The
    /// blades follow their lawn: hidden while it is, gone when it goes. Nothing is saved.
    /// </summary>
    public static class GameGrass
    {
        /// <summary>Whether lawns grow grass in Play.</summary>
        public static bool On = true;
        /// <summary>Blades a square metre, a blade's height range (m), its width (m).</summary>
        public static float Density = 700f, MinHeight = .1f, MaxHeight = .2f, Width = .035f;
        /// <summary>At most this many blades in a scene; a lawn that would get fewer than MinDensity a square metre out
        /// of what is left (a backdrop lawn the size of a field, seen only from afar where blades sink anyway) gets none.</summary>
        public static int MaxBlades = 200000;
        public static float MinDensity = 250f;

        const float Cell = 12f;
        static readonly HashSet<int> seen = new HashSet<int>();
        static readonly List<(Renderer lawn, GameObject grass)> patches = new List<(Renderer, GameObject)>();
        static readonly Dictionary<Material, Material> looks = new Dictionary<Material, Material>();
        static Material template;
        static int blades;
        static bool shown = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            seen.Clear();
            patches.Clear();
            looks.Clear();
            blades = 0;
            shown = true;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
            var go = new GameObject("Gfx grass (JY-gfx)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watch>();
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            seen.Clear();
            patches.Clear();
            blades = 0;
        }

        /// <summary>Grows grass on every lawn not seen yet; returns how many lawns it grew on.</summary>
        public static int Grow()
        {
            if (!On) return 0;
            if (template == null) template = Resources.Load<Material>("Gfx/GfxGrass");
            if (template == null || template.shader == null || !template.shader.isSupported) return 0;
            int grown = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!seen.Add(r.GetInstanceID())) continue;
                var materials = r.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var m = materials[slot];
                    if (m == null || m.name.IndexOf("lawn", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (Plant(r, slot, m)) grown++;
                }
            }
            return grown;
        }

        /// <summary>Shows or hides the grass grown so far (to compare).</summary>
        public static void Show(bool show)
        {
            shown = show;
            Follow();
        }

        // Each patch as its lawn is: shown while the lawn is drawn, destroyed with it.
        static void Follow()
        {
            for (int i = patches.Count - 1; i >= 0; i--)
            {
                var (lawn, grass) = patches[i];
                if (grass == null)
                {
                    patches.RemoveAt(i);
                    continue;
                }
                if (lawn == null)
                {
                    Object.Destroy(grass);
                    patches.RemoveAt(i);
                    continue;
                }
                bool want = shown && lawn.enabled && lawn.gameObject.activeInHierarchy;
                if (grass.activeSelf != want) grass.SetActive(want);
            }
        }

        // Blades over one lawn: random points on its up-facing triangles (as many as their area wants), each blade a
        // three-step strip leaning a little, built in world space by cells.
        static bool Plant(MeshRenderer r, int slot, Material lawn)
        {
            var filter = r.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || !mesh.isReadable || slot >= mesh.subMeshCount) return false;
            var vertices = mesh.vertices;
            var triangles = mesh.GetTriangles(slot);
            var toWorld = r.transform.localToWorldMatrix;
            var b = r.bounds;
            var random = new System.Random(Mathf.RoundToInt(b.center.x * 13f + b.center.y * 7f + b.center.z * 3f) ^ slot);

            // The lawn's up-facing area first, so a lawn too big for what is left gets thinner.
            float area = 0f;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = toWorld.MultiplyPoint3x4(vertices[triangles[t]]), c1 = toWorld.MultiplyPoint3x4(vertices[triangles[t + 1]]), c2 = toWorld.MultiplyPoint3x4(vertices[triangles[t + 2]]);
                var n = Vector3.Cross(c1 - a, c2 - a);
                if (n.y > .6f * n.magnitude) area += n.magnitude * .5f;
            }
            int left = MaxBlades - blades;
            if (area <= 0f || left <= 0) return false;
            float density = Mathf.Min(Density, left / area);
            if (density < MinDensity)
            {
                Debug.Log($"[GameGrass] {r.name}: {area:0} m2, too big for grass");
                return false;
            }

            var cells = new Dictionary<Vector2Int, Builder>();
            int made = 0;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = toWorld.MultiplyPoint3x4(vertices[triangles[t]]), c1 = toWorld.MultiplyPoint3x4(vertices[triangles[t + 1]]), c2 = toWorld.MultiplyPoint3x4(vertices[triangles[t + 2]]);
                var n = Vector3.Cross(c1 - a, c2 - a);
                float twice = n.magnitude;
                if (twice <= 1e-6f || n.y <= .6f * twice) continue;
                n /= twice;
                float want = twice * .5f * density;
                int count = (int)want + (random.NextDouble() < want - (int)want ? 1 : 0);
                for (int k = 0; k < count; k++)
                {
                    float u = (float)random.NextDouble(), v = (float)random.NextDouble();
                    if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                    var p = a + (c1 - a) * u + (c2 - a) * v;
                    var key = new Vector2Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));
                    if (!cells.TryGetValue(key, out var builder)) cells[key] = builder = new Builder();
                    builder.Blade(p, n, random);
                    made++;
                }
            }
            if (made == 0) return false;
            blades += made;

            if (!looks.TryGetValue(lawn, out var look))
            {
                var colour = lawn.HasProperty("_BaseColor") ? lawn.GetColor("_BaseColor") : lawn.color;
                look = new Material(template) { name = "Grass on " + lawn.name, hideFlags = HideFlags.DontSave };
                look.SetColor("_Base", new Color(colour.r * .55f, colour.g * .55f, colour.b * .55f, 1f));
                look.SetColor("_Tip", new Color(Mathf.Min(1f, colour.r * 1.15f + .05f), Mathf.Min(1f, colour.g * 1.15f + .04f), Mathf.Max(0f, colour.b * 1.1f - .02f), 1f));
                looks[lawn] = look;
            }
            foreach (var cell in cells.Values)
            {
                // In world space (the blades were placed there), in the lawn's scene.
                var go = new GameObject("Grass (JY-gfx)");
                go.layer = r.gameObject.layer;
                SceneManager.MoveGameObjectToScene(go, r.gameObject.scene);
                go.AddComponent<MeshFilter>().sharedMesh = cell.Mesh();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = look;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = true;
                go.SetActive(shown);
                patches.Add((r, go));
            }
            Debug.Log($"[GameGrass] {r.name}: {area:0} m2, {made} blades in {cells.Count} cells ({blades} in the scene)");
            return true;
        }

        sealed class Builder
        {
            readonly List<Vector3> positions = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<Color32> colours = new List<Color32>();
            readonly List<int> indices = new List<int>();

            // A blade: root pair, middle pair, tip; it turns any way and leans a little.
            public void Blade(Vector3 root, Vector3 up, System.Random random)
            {
                float height = Mathf.Lerp(MinHeight, MaxHeight, (float)random.NextDouble());
                float turn = (float)(random.NextDouble() * Mathf.PI * 2.0);
                var side = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
                var lean = Vector3.Cross(side, up).normalized * (height * (.15f + .35f * (float)random.NextDouble()));
                float w = Width * (.7f + .6f * (float)random.NextDouble());
                byte shade = (byte)random.Next(256), tall = (byte)Mathf.Clamp(Mathf.RoundToInt(height / .5f * 255f), 0, 255);
                var colour = new Color32(shade, tall, 0, 255);
                int i = positions.Count;
                var mid = root + up * (height * .55f) + lean * .3f;
                var tip = root + up * height + lean;
                positions.Add(root - side * (w * .5f)); uvs.Add(new Vector2(0f, 0f));
                positions.Add(root + side * (w * .5f)); uvs.Add(new Vector2(1f, 0f));
                positions.Add(mid - side * (w * .35f)); uvs.Add(new Vector2(0f, .55f));
                positions.Add(mid + side * (w * .35f)); uvs.Add(new Vector2(1f, .55f));
                positions.Add(tip); uvs.Add(new Vector2(.5f, 1f));
                for (int k = 0; k < 5; k++)
                {
                    normals.Add(up);
                    colours.Add(colour);
                }
                indices.Add(i); indices.Add(i + 2); indices.Add(i + 1);
                indices.Add(i + 1); indices.Add(i + 2); indices.Add(i + 3);
                indices.Add(i + 2); indices.Add(i + 4); indices.Add(i + 3);
            }

            public Mesh Mesh()
            {
                var mesh = new Mesh { name = "Grass (JY-gfx)", hideFlags = HideFlags.DontSave };
                if (positions.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetColors(colours);
                mesh.SetTriangles(indices, 0, true);
                mesh.UploadMeshData(true);
                return mesh;
            }
        }

        // Lawns keep coming (sections are dressed as a round goes): look every two seconds; follow every frame.
        sealed class Watch : MonoBehaviour
        {
            float next;

            void LateUpdate()
            {
                Follow();
                if (Time.realtimeSinceStartup < next) return;
                next = Time.realtimeSinceStartup + 2f;
                Grow();
            }
        }
    }
}
