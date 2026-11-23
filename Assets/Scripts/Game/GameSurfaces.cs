using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24, the graphics upgrade): hands the surface sets to every shader. A set is the fine detail of a
    /// real material (limestone, sand, wood, iron, brass, paint, canvas, leaves, slate): a mask (R roughness, G cavity,
    /// B height, A colour variation) and a tangent-space normal, made by ArtSource/Gfx/textures.py and kept in
    /// Resources/Gfx/Sets. They go in two texture arrays (slice order = Sets) as the globals _GfxSetMask and
    /// _GfxSetNormal, and _GfxSurface = (1, Strength) turns them on in Shaders/URP/CFLit.hlsl (and in the shaders that
    /// read them the same way). Made once, before the first scene; nothing is synced or saved.
    /// The course's URP Lit materials (60-96% of a section's) wear the sets through ChessFight/Gfx Lit: in Play every
    /// renderer's URP Lit material is swapped for a twin on Gfx Lit, its set picked from the material's name (Pick).
    /// The material assets are never touched; the twins live only in memory.
    /// </summary>
    public static class GameSurfaces
    {
        /// <summary>The slices, in the order CFLit.hlsl's CF_SET_* name them.</summary>
        public static readonly string[] Sets = { "limestone", "sand", "wood", "iron", "brass", "paint", "canvas", "leaf", "slate" };

        /// <summary>How strongly the sets show, everywhere (1 as made).</summary>
        public static float Strength = 1f;

        /// <summary>How strongly broad patches (lighter, darker, glossier over ten-odd metres) show on big surfaces.</summary>
        public static float Macro = 1f;

        static readonly int MaskId = Shader.PropertyToID("_GfxSetMask"), NormalId = Shader.PropertyToID("_GfxSetNormal"), OnId = Shader.PropertyToID("_GfxSurface");
        static Texture2DArray masks, normals;

        public static bool Ready => masks != null && normals != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            masks = Build("mask");
            normals = Build("normal");
            if (!Ready)
            {
                Shader.SetGlobalVector(OnId, Vector4.zero);
                return;
            }
            Shader.SetGlobalTexture(MaskId, masks);
            Shader.SetGlobalTexture(NormalId, normals);
            Apply();
            Application.quitting -= Off;
            Application.quitting += Off;
            twinOf.Clear();
            originalOf.Clear();
            enabled = true;
            var watch = new GameObject("Gfx surfaces (JY-gfx)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            Object.DontDestroyOnLoad(watch);
            watch.AddComponent<Watch>();
        }

        /// <summary>Strength changed: hand it on.</summary>
        public static void Apply() => Shader.SetGlobalVector(OnId, Ready && enabled ? new Vector4(1f, Strength, Macro, 0f) : Vector4.zero);

        /// <summary>The sets on or off everywhere (GameGfx turns them off to compare): no surface, plain URP Lit.</summary>
        public static void Enable(bool on)
        {
            enabled = on;
            Apply();
            Dress(on && LitOn);
        }

        static bool enabled = true;

        // Shader globals and made materials outlive Play in the editor.
        static void Off()
        {
            Shader.SetGlobalVector(OnId, Vector4.zero);
            foreach (var twin in originalOf.Keys) Object.Destroy(twin);
            twinOf.Clear();
            originalOf.Clear();
            looked.Clear();
        }

        // ---------------------------------------------------------------- URP Lit -> Gfx Lit

        /// <summary>Whether URP Lit materials wear Gfx Lit (their surface set) in Play.</summary>
        public static bool LitOn = true;

        static Shader urpLit, gfxLit;
        static readonly Dictionary<Material, Material> twinOf = new Dictionary<Material, Material>();
        static readonly Dictionary<Material, Material> originalOf = new Dictionary<Material, Material>();
        // Renderers Dress has looked at. Each is dressed once, when first seen: a material a script puts on it later (a
        // lamp, a signal, a beam that shows its state by its material and checks it) stays as the script put it.
        static readonly HashSet<int> looked = new HashSet<int>();
        static readonly List<Material> slots = new List<Material>(8);
        static readonly int SetId = Shader.PropertyToID("_GfxSet"), SetScaleId = Shader.PropertyToID("_GfxSetScale"),
            BaseMapId = Shader.PropertyToID("_BaseMap"), MetallicId = Shader.PropertyToID("_Metallic"), SurfaceId = Shader.PropertyToID("_Surface");

        /// <summary>Every loaded renderer's URP Lit materials swapped for their Gfx Lit twins (on), or back (off).
        /// Returns how many material slots changed.</summary>
        public static int Dress(bool on)
        {
            if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (gfxLit == null) gfxLit = Shader.Find("ChessFight/Gfx Lit");
            if (urpLit == null || gfxLit == null || !gfxLit.isSupported || (on && !Ready)) return 0;
            int changed = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (on && !looked.Add(r.GetInstanceID())) continue;
                r.GetSharedMaterials(slots);
                bool dirty = false;
                for (int i = 0; i < slots.Count; i++)
                {
                    var m = slots[i];
                    if (m == null) continue;
                    var swap = on ? (m.shader == urpLit ? Twin(m) : m) : (originalOf.TryGetValue(m, out var original) ? original : m);
                    if (swap == m) continue;
                    slots[i] = swap;
                    dirty = true;
                    changed++;
                }
                if (dirty) r.sharedMaterials = slots.ToArray();
            }
            // Undressed: every renderer may be dressed again.
            if (!on) looked.Clear();
            return changed;
        }

        // The material on Gfx Lit with everything else as it was, or the material itself when it wears no set. Only
        // material assets (positive instance ids): a material made at run time (renderer.material, new Material) is
        // one a script holds and changes, and must stay the one on the renderer.
        static Material Twin(Material m)
        {
            if (twinOf.TryGetValue(m, out var twin)) return twin;
            float scale = 0f;
            int set = m.GetInstanceID() > 0 ? Pick(m, out scale) : -1;
            if (set < 0)
            {
                twinOf[m] = m;
                return m;
            }
            twin = new Material(m) { name = m.name + " (Gfx)", hideFlags = HideFlags.DontSave };
            twin.shader = gfxLit;
            foreach (var k in m.shaderKeywords) twin.EnableKeyword(k);
            twin.renderQueue = m.renderQueue;
            twin.SetFloat(SetId, set);
            twin.SetFloat(SetScaleId, scale);
            twinOf[m] = twin;
            originalOf[twin] = m;
            return twin;
        }

        /// <summary>The set a URP Lit material wears (Sets index, -1 none) and how strongly, from the words of its name
        /// (the first rule that matches); weaker over a picture or a normal map of its own; metals wear brass.</summary>
        public static int Pick(Material m, out float scale)
        {
            scale = 0f;
            if (m.HasProperty(SurfaceId) && m.GetFloat(SurfaceId) > .5f) return -1;  // see-through
            var words = Words(m.name);
            int set = -2;
            foreach (var (keys, ruleSet, ruleScale) in Rules)
            {
                bool hit = false;
                foreach (var key in keys)
                {
                    foreach (var w in words)
                        if (key.Length >= 5 ? w.Contains(key) : w == key)
                        {
                            hit = true;
                            break;
                        }
                    if (hit) break;
                }
                if (!hit) continue;
                set = ruleSet;
                scale = ruleScale;
                break;
            }
            if (set == -2)
            {
                set = 5;  // nothing known: a faint painted surface
                scale = .5f;
            }
            if (set < 0) return -1;
            if (m.HasProperty(MetallicId) && m.GetFloat(MetallicId) >= .5f && set != 3 && set != 4) set = 4;
            if (m.HasProperty(BaseMapId) && m.GetTexture(BaseMapId) != null) scale *= .6f;
            if (m.IsKeywordEnabled("_NORMALMAP")) scale *= .5f;
            return set;
        }

        // Words of a name: cut at anything not a letter and where a capital follows a small letter; lower case.
        static List<string> Words(string name)
        {
            var words = new List<string>();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool cut = !char.IsLetter(c) || (char.IsUpper(c) && i > 0 && char.IsLower(name[i - 1]));
                if (cut && sb.Length > 0)
                {
                    words.Add(sb.ToString());
                    sb.Clear();
                }
                if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
            }
            if (sb.Length > 0) words.Add(sb.ToString());
            if (name.ToLowerInvariant().Contains("gate light")) words.Add("glow");
            return words;
        }

        // Sets: 0 limestone, 1 sand, 2 wood, 3 iron, 4 brass, 5 paint, 6 canvas, 7 leaf, 8 slate; -1 none. A key of 5
        // letters or more matches inside a word (oldiron, ashlar...), a shorter one only the whole word. Nothing
        // matched: a faint paint.
        static readonly (string[] keys, int set, float scale)[] Rules =
        {
            // Nothing over: see-through, glowing, pictures and print, living things and the chess pieces.
            (new[] { "glass", "water", "foam", "jet", "splash", "fall", "flame", "fire", "glow", "emissi", "lamp", "lantern", "sky",
                     "skylight", "planet", "mirror", "eye", "beak", "veil", "wick", "beam", "vfx", "additive", "cutout", "transparent",
                     "portrait", "notation", "clock", "dial", "page", "parchment", "paper", "globe", "ink", "tea", "hair", "plume",
                     "dancer", "blush", "body", "bird", "petal", "flower", "lotus", "lily", "pollen", "stem", "stalk", "piece", "pawn",
                     "king", "queen", "bishop", "knight", "rook", "progress", "costume", "hoof", "gem", "crystal", "frost", "pearl",
                     "motif", "window" }, -1, 0f),
            (new[] { "gold", "brass", "bronze", "copper" }, 4, .8f),
            (new[] { "iron", "oldiron", "steel", "chain", "winch", "hinge", "rod", "nozzle", "metal", "lattice", "zinc", "anvil" }, 3, 1f),
            (new[] { "marble" }, 5, .35f),
            (new[] { "wood", "walnut", "oak", "maple", "ebony", "timber", "plank", "bark", "trunk", "staves", "parquet", "joist",
                     "carpentry", "door", "stage", "wicker", "frames", "capstan", "crate", "barrel" }, 2, .9f),
            (new[] { "cloth", "canvas", "linen", "velvet", "carpet", "rug", "banner", "pennant", "flag", "curtain", "upholstery",
                     "felt", "sack", "straw", "rope", "leather", "ermine", "fur", "coat", "seam", "bristle", "towel", "runner", "book" }, 6, 1f),
            (new[] { "leaf", "leaves", "bush", "cypress", "topiary", "hedge", "moss", "ivy", "lawn", "grass", "tree", "shrub" }, 7, 1f),
            (new[] { "slate", "roof", "basalt", "charcoal" }, 8, 1f),
            (new[] { "tile", "mosaic" }, 5, .6f),
            (new[] { "stone", "limestone", "brick", "ashlar", "wall", "castle", "tower", "pillar", "column", "arch", "paving", "plinth",
                     "step", "stair", "rock", "palace", "statue", "dais", "facade", "structure", "hall", "basement", "court",
                     "channel", "platform", "herringbone", "block", "floor", "reveal" }, 0, 1f),
            (new[] { "plaster" }, 0, .6f),
            (new[] { "sand", "soil", "dirt", "earth", "ground", "gravel", "mud", "grout", "joint", "groove" }, 1, 1f),
            (new[] { "paint", "enamel", "plastic", "ceramic", "ivory", "boat", "fender", "emblem", "shield",
                     "sign", "doll", "soap", "checker", "board" }, 5, .6f),
        };

        // Renderers keep coming (sections are dressed as a round goes): look just after a scene loads, then every two
        // seconds (only renderers not seen before are dressed).
        sealed class Watch : MonoBehaviour
        {
            float next;
            void OnEnable() => SceneManager.sceneLoaded += Loaded;
            void OnDisable() => SceneManager.sceneLoaded -= Loaded;
            void Loaded(Scene scene, LoadSceneMode mode) => StartCoroutine(Soon());
            IEnumerator Soon()
            {
                yield return null;
                Dress(LitOn && enabled);
                yield return new WaitForSecondsRealtime(.5f);
                Dress(LitOn && enabled);
            }
            void Update()
            {
                if (Time.realtimeSinceStartup < next) return;
                next = Time.realtimeSinceStartup + 2f;
                Dress(LitOn && enabled);
            }
        }

        // One array of the sets' images of one kind: the images copied on the GPU, every mip, so they need not be
        // readable (they are imported alike: GfxSetImport).
        static Texture2DArray Build(string kind)
        {
            var first = Resources.Load<Texture2D>("Gfx/Sets/" + Sets[0] + "_" + kind);
            if (first == null)
            {
                Debug.LogWarning("[GameSurfaces] no surface sets in Resources/Gfx/Sets");
                return null;
            }
            var array = new Texture2DArray(first.width, first.height, Sets.Length, first.format, first.mipmapCount > 1, true)
            {
                name = "Gfx surface sets (" + kind + ")",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
                hideFlags = HideFlags.HideAndDontSave,
            };
            for (int i = 0; i < Sets.Length; i++)
            {
                var image = i == 0 ? first : Resources.Load<Texture2D>("Gfx/Sets/" + Sets[i] + "_" + kind);
                if (image == null || image.width != first.width || image.height != first.height || image.format != first.format || image.mipmapCount != first.mipmapCount)
                {
                    Debug.LogWarning($"[GameSurfaces] set {Sets[i]}_{kind} is missing or not imported like the others");
                    Object.Destroy(array);
                    return null;
                }
                Graphics.CopyTexture(image, 0, array, i);
            }
            return array;
        }
    }
}
