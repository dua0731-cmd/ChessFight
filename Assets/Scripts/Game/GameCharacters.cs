using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24): the pieces, smooth-shaded and in a look that sits in the upgraded world. JY: "캐릭터도 지금
    /// 일부러 로우폴리 느낌주려고 스무스 쉐이딩을 안했는데 하고 캐릭터 쉐이더도 더 잘 어울리게, 예쁘게 하는게 나은거같아".
    ///   smooth  the pieces' meshes are flat-shaded (every face its own normal). In Play every renderer that wears the
    ///           character shader gets a copy of its mesh whose normals are averaged over the faces meeting at a point,
    ///           as long as they meet at less than SmoothAngle (a rim, a crown's points stay crisp). The assets are not
    ///           touched; one copy per mesh lives in memory.
    ///   look    the character shader's JY-gfx light (Shaders/Characters/CFCharacter.shader, _GfxCharacter = 1): a soft
    ///           round shade lit by the world's own sky light, a lacquer highlight, the place's reflections at the
    ///           edges, the sun's rim when it is behind the piece, ambient occlusion. The numbers are globals
    ///           (_GfxCharacterLook) set from here.
    /// Everywhere the pieces are (the lobby too); GameGfx's switch puts the flat meshes and Look A back.
    /// </summary>
    public static class GameCharacters
    {
        public static bool On = true;
        /// <summary>Faces meeting at less than this many degrees share a normal: a cylinder's facets go smooth, while a
        /// collar, a crenel, a rim or a bevel stays a crisp edge (JY: "다 부드럽게 하는게 아니라 돌출부들은 확실히 보여야지").</summary>
        public static float SmoothAngle = 30f;
        /// <summary>The look: how much of the sky light fills the shade, the highlight, the sun's rim behind the piece,
        /// the reflections at the edges.</summary>
        public static float Ambient = .75f, Gloss = .3f, Rim = .9f, Reflection = .3f;
        /// <summary>The body's colour as the Tripo-textured pieces had it (JY 11-24: "색감은 ... 위쪽처럼"): the cream taken
        /// part of the way to grey (Desaturate) and toward a warm grey beige (Tint). Eyes, glints, blush, hands and feet
        /// keep their own.</summary>
        public static float Desaturate = .5f;
        public static Color Tint = new Color(.9f, .83f, .75f);

        const string ShaderName = "ChessFight/Character";
        static readonly int LookId = Shader.PropertyToID("_GfxCharacterLook"), OnId = Shader.PropertyToID("_GfxCharacter"),
            ToneId = Shader.PropertyToID("_GfxCharacterTone");
        static readonly Dictionary<Mesh, Mesh> smoothOf = new Dictionary<Mesh, Mesh>();
        static readonly Dictionary<Mesh, Mesh> flatOf = new Dictionary<Mesh, Mesh>();
        static readonly HashSet<int> seen = new HashSet<int>();
        static bool shown = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            smoothOf.Clear();
            flatOf.Clear();
            seen.Clear();
            shown = true;
            Apply();
            var go = new GameObject("Gfx characters (JY-gfx)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watch>();
            Application.quitting -= Quit;
            Application.quitting += Quit;
        }

        /// <summary>The look's numbers changed (or the switch): hand them on.</summary>
        public static void Apply()
        {
            Shader.SetGlobalFloat(OnId, On && shown ? 1f : 0f);
            Shader.SetGlobalVector(LookId, new Vector4(Ambient, Gloss, Rim, Reflection));
            Shader.SetGlobalVector(ToneId, new Vector4(Desaturate, Tint.r, Tint.g, Tint.b));
        }

        /// <summary>Smooth meshes and the new look on (true) or the flat meshes and Look A back (false).</summary>
        public static void Show(bool show)
        {
            shown = show;
            Apply();
            seen.Clear();
            Sweep();
        }

        static void Quit()
        {
            Shader.SetGlobalFloat(OnId, 0f);
            foreach (var mesh in smoothOf.Values) if (mesh != null) Object.Destroy(mesh);
            smoothOf.Clear();
            flatOf.Clear();
            seen.Clear();
        }

        static bool Wears(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && m.shader != null && m.shader.name == ShaderName) return true;
            return false;
        }

        /// <summary>Every piece not seen yet given its smooth mesh (or its flat one back when the look is off).</summary>
        public static void Sweep()
        {
            SweepSkinned();
            bool smooth = On && shown && GameGfx.Upgrade;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!seen.Add(r.GetInstanceID()) || !Wears(r)) continue;
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null) continue;
                var mesh = Swap(filter.sharedMesh, smooth);
                if (mesh != null && mesh != filter.sharedMesh) filter.sharedMesh = mesh;
            }
        }

        /// <summary>The skinned pieces only (the players, their ragdolls): few, and quick to find.</summary>
        public static void SweepSkinned()
        {
            bool smooth = On && shown && GameGfx.Upgrade;
            foreach (var r in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!seen.Add(r.GetInstanceID()) || !Wears(r)) continue;
                var mesh = Swap(r.sharedMesh, smooth);
                if (mesh != null && mesh != r.sharedMesh) r.sharedMesh = mesh;
            }
        }

        /// <summary>Something new came in (a round's sections, GfxSections' watch): every piece looked at on the next
        /// frame.</summary>
        public static void Wake() => woken = true;

        static bool woken;

        // The mesh to wear: the smooth copy (made once) or the flat original.
        static Mesh Swap(Mesh mesh, bool smooth)
        {
            if (mesh == null) return null;
            if (!smooth) return flatOf.TryGetValue(mesh, out var original) ? original : mesh;
            if (flatOf.ContainsKey(mesh)) return mesh;   // already smooth
            if (!smoothOf.TryGetValue(mesh, out var copy))
            {
                copy = Smooth(mesh);
                smoothOf[mesh] = copy;
                if (copy != null) flatOf[copy] = mesh;
            }
            return copy != null ? copy : mesh;
        }

        // Normals averaged over the faces that meet at each point (weighted by their area), across faces that meet at
        // less than SmoothAngle to this corner's own face.
        static Mesh Smooth(Mesh source)
        {
            if (!source.isReadable) return null;
            var vertices = source.vertices;
            var normals = source.normals;
            int n = vertices.Length;
            if (n == 0) return null;
            // Worked out at unit size: the pieces' meshes are tiny in their own units (centimetres), and a face's cross
            // product there is so small that Vector3.normalized gives zero.
            float unit = 1f / Mathf.Max(1e-6f, source.bounds.size.magnitude);
            for (int i = 0; i < n; i++) vertices[i] *= unit;
            var face = new Vector3[n];
            for (int s = 0; s < source.subMeshCount; s++)
            {
                if (source.GetTopology(s) != MeshTopology.Triangles) continue;
                var triangles = source.GetTriangles(s);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    face[a] += cross;
                    face[b] += cross;
                    face[c] += cross;
                }
            }
            var groups = new Dictionary<Vector3Int, List<int>>();
            const float Grid = 1e4f;
            for (int i = 0; i < n; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * Grid);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<int>(6);
                list.Add(i);
            }
            float limit = Mathf.Cos(SmoothAngle * Mathf.Deg2Rad);
            var result = new Vector3[n];
            foreach (var list in groups.Values)
                foreach (int i in list)
                {
                    var own = Unit(face[i]);
                    if (own == Vector3.zero) own = normals.Length == n ? normals[i] : Vector3.up;
                    var sum = Vector3.zero;
                    foreach (int j in list)
                    {
                        var other = Unit(face[j]);
                        if (other != Vector3.zero && Vector3.Dot(own, other) >= limit) sum += face[j];
                    }
                    var smooth = Unit(sum);
                    result[i] = smooth != Vector3.zero ? smooth : own;
                }
            var copy = Object.Instantiate(source);
            copy.name = source.name + " (smooth)";
            copy.hideFlags = HideFlags.DontSave;
            copy.normals = result;
            return copy;
        }

        static Vector3 Unit(Vector3 v)
        {
            float m = v.magnitude;
            return m > 1e-30f ? v / m : Vector3.zero;
        }

        // Pieces keep coming (players, ragdolls, the lobby's line-up). The skinned ones every second (finding them is
        // nearly free); the mesh ones (statues, line-ups) when a scene loads, when a round's sections come in (Wake) and
        // every ten seconds: finding every mesh renderer of a round is a few milliseconds.
        sealed class Watch : MonoBehaviour
        {
            float next, nextAll;

            void Start()
            {
                next = Time.realtimeSinceStartup + .5f;
                nextAll = next;
            }

            void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += Loaded;
            void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Loaded;
            void Loaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Wake();

            void LateUpdate()
            {
                float now = Time.realtimeSinceStartup;
                if (woken || now >= nextAll)
                {
                    woken = false;
                    nextAll = now + 10f;
                    next = now + 1f;
                    Sweep();
                    return;
                }
                if (now < next) return;
                next = now + 1f;
                SweepSkinned();
            }
        }
    }
}
