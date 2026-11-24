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
            // Arrays from an earlier Play (HideAndDontSave outlives it in the editor) go first.
            foreach (var old in Resources.FindObjectsOfTypeAll<Texture2DArray>())
                if (old != null && old.name.StartsWith("Gfx surface sets (")) Object.DestroyImmediate(old);
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
        public static void Apply()
        {
            Shader.SetGlobalVector(OnId, Ready && enabled ? new Vector4(1f, Strength, Macro, 0f) : Vector4.zero);
            Shader.SetGlobalFloat(FoliageId, Ready && enabled ? Foliage : 0f);
            Shader.SetGlobalFloat(ReliefShadowId, ReliefShadow);
        }

        /// <summary>How much sun comes through leaves and grass blades (Gfx Lit's leaf set, Gfx Grass; 0 none).</summary>
        public static float Foliage = 1f;
        static readonly int FoliageId = Shader.PropertyToID("_GfxFoliage");

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
            Shader.SetGlobalFloat(FoliageId, 0f);
            if (masks != null) Object.Destroy(masks);
            if (normals != null) Object.Destroy(normals);
            masks = normals = null;
            foreach (var twin in originalOf.Keys) Object.Destroy(twin);
            twinOf.Clear();
            originalOf.Clear();
            looked.Clear();
        }

        // A new scene: what earlier scenes dressed is let go, or the twins would keep every material and texture they
        // used alive for the whole session. Anything still wearing a twin (kept across scenes) gets its own back first.
        static void Forget()
        {
            Dress(false);
            foreach (var twin in originalOf.Keys) if (twin != null) Object.Destroy(twin);
            twinOf.Clear();
            originalOf.Clear();
            reliefDepthOf.Clear();
        }

        // ---------------------------------------------------------------- the floors' relief

        /// <summary>The relief (parallax occlusion, joints darker in the shade) on the twins that have one.</summary>
        public static bool ReliefOn = true;
        /// <summary>Every twin's relief depth times this.</summary>
        public static float ReliefDepth = 1f;
        /// <summary>How much of the sun's shadow the relief casts into its own joints (0 none, 1 all).</summary>
        public static float ReliefShadow = 1f;
        static readonly int ReliefShadowId = Shader.PropertyToID("_GfxReliefShadow");

        static readonly Dictionary<string, Texture2D> reliefOf = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Material, float> reliefDepthOf = new Dictionary<Material, float>();
        static readonly int ParallaxMapId = Shader.PropertyToID("_ParallaxMap"), ParallaxId = Shader.PropertyToID("_Parallax"),
            OcclusionMapId = Shader.PropertyToID("_OcclusionMap"), OcclusionStrengthId = Shader.PropertyToID("_OcclusionStrength");

        /// <summary>The relief a material's swatch was baked with (JY-gfx 11-24: the swatch scripts r502-r509 baked height
        /// and occlusion from real geometry, but only colour and normals came into the game): Resources/Gfx/Height/
        /// &lt;swatch&gt;_HeightAO (R height, G occlusion), found by the base map's name (&lt;swatch&gt;_BaseColor); and its depth
        /// in UV units (a floor swatch's tile is 2 m).</summary>
        public static Texture2D Relief(Material m, out float depth)
        {
            depth = 0f;
            var baseMap = m.HasProperty(BaseMapId) ? m.GetTexture(BaseMapId) : null;
            if (baseMap == null) return null;
            string stem = baseMap.name;
            int at = stem.LastIndexOf("_BaseColor", System.StringComparison.Ordinal);
            if (at <= 0) return null;
            stem = stem.Substring(0, at);
            if (!reliefOf.TryGetValue(stem, out var map)) reliefOf[stem] = map = Resources.Load<Texture2D>("Gfx/Height/" + stem + "_HeightAO");
            if (map == null) return null;
            depth = Depth(stem);
            return map;
        }

        // Metres of relief over the swatch's 2 m tile, by what the swatch shows.
        static float Depth(string stem)
        {
            var s = stem.ToLowerInvariant();
            foreach (var w in new[] { "marble", "tile" }) if (s.Contains(w)) return .008f / 2f;
            foreach (var w in new[] { "wood", "walnut", "maple", "parquet", "plank", "oak", "joist", "staves", "wainscot", "door" }) if (s.Contains(w)) return .008f / 2f;
            foreach (var w in new[] { "canvas", "linen", "velvet", "ermine", "metal", "iron" }) if (s.Contains(w)) return .004f / 2f;
            return .03f / 2f;   // stone: flagstones, slate, paving, brick, basalt, limestone
        }

        /// <summary>ReliefOn and ReliefDepth onto every twin made so far.</summary>
        public static void ApplyRelief()
        {
            Shader.SetGlobalFloat(ReliefShadowId, ReliefShadow);
            foreach (var kv in reliefDepthOf)
            {
                var twin = kv.Key;
                if (twin == null) continue;
                if (ReliefOn) twin.EnableKeyword("_PARALLAXMAP");
                else twin.DisableKeyword("_PARALLAXMAP");
                twin.SetFloat(ParallaxId, kv.Value * ReliefDepth);
            }
        }

        // ---------------------------------------------------------------- URP Lit -> Gfx Lit

        /// <summary>Whether URP Lit materials wear Gfx Lit (their surface set) in Play.</summary>
        public static bool LitOn = true;

        static Shader urpLit, gfxLit;
        static bool warned;
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
            if (gfxLit == null && !Application.isEditor)
            {
                if (!warned) Debug.LogWarning("[GameSurfaces] ChessFight/Gfx Lit is not in this build: URP Lit materials keep their plain look (the menu ChessFight/Graphics/URP Lit materials -> Gfx Lit puts it in a build)");
                warned = true;
                return 0;
            }
            if (urpLit == null || gfxLit == null || !gfxLit.isSupported || (on && !Ready)) return 0;
            // Only the round's scenes (GameGfx.InScope); undressing works anywhere.
            if (on && !GameGfx.InScope()) return 0;
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

        /// <summary>A material as the upgrade dresses it in Play: its Gfx Lit twin (its surface set, its relief), or
        /// itself (not URP Lit, no set or relief, or Gfx Lit missing). For a material a script puts on later (GfxMapLights'
        /// floors).</summary>
        public static Material Dressed(Material m)
        {
            if (m == null) return null;
            if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (gfxLit == null) gfxLit = Shader.Find("ChessFight/Gfx Lit");
            if (gfxLit == null || !gfxLit.isSupported || !Ready || !LitOn || m.shader != urpLit) return m;
            return Twin(m);
        }

        // The material on Gfx Lit with everything else as it was, or the material itself when it wears no set. Only
        // material assets (positive instance ids): a material made at run time (renderer.material, new Material) is
        // one a script holds and changes, and must stay the one on the renderer.
        static Material Twin(Material m)
        {
            if (twinOf.TryGetValue(m, out var twin)) return twin;
            float scale = 0f, depth = 0f;
            bool asset = m.GetInstanceID() > 0;
            int set = asset ? Pick(m, out scale) : -1;
            // A floor whose swatch has a relief gets a twin for it even without a set (the pass skips sets below 0).
            var relief = asset && !(m.HasProperty(SurfaceId) && m.GetFloat(SurfaceId) > .5f) ? Relief(m, out depth) : null;
            if (set < 0 && relief == null)
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
            if (relief != null)
            {
                twin.SetTexture(ParallaxMapId, relief);
                twin.SetFloat(ParallaxId, depth * ReliefDepth);
                if (ReliefOn) twin.EnableKeyword("_PARALLAXMAP");
                // The baked occlusion (G) deepens the joints in the shade, unless the material brings its own.
                if (!m.IsKeywordEnabled("_OCCLUSIONMAP"))
                {
                    twin.SetTexture(OcclusionMapId, relief);
                    twin.SetFloat(OcclusionStrengthId, 1f);
                    twin.EnableKeyword("_OCCLUSIONMAP");
                }
                reliefDepthOf[twin] = depth;
            }
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

        /// <summary>Something new came in (a round's sections, GfxSections' watch): look on the next frame and again a
        /// second later.</summary>
        public static void Wake()
        {
            pending = 2;
            pendingAt = 0f;
        }

        static int pending;
        static float pendingAt;

        // Renderers come in when a scene loads and when a round lays out its sections (Wake): look then, and every ten
        // seconds for anything else (only renderers not seen before are dressed). A look over every renderer of a round
        // is a few milliseconds, too much for every two seconds.
        sealed class Watch : MonoBehaviour
        {
            const float Every = 10f;
            float next;
            void OnEnable() => SceneManager.sceneLoaded += Loaded;
            void OnDisable() => SceneManager.sceneLoaded -= Loaded;
            void Loaded(Scene scene, LoadSceneMode mode)
            {
                if (mode == LoadSceneMode.Single) Forget();
                StartCoroutine(Soon());
            }
            IEnumerator Soon()
            {
                yield return null;
                Dress(LitOn && enabled);
                yield return new WaitForSecondsRealtime(.5f);
                Dress(LitOn && enabled);
            }
            void Update()
            {
                float now = Time.realtimeSinceStartup;
                if (pending > 0 && now >= pendingAt)
                {
                    pending--;
                    pendingAt = now + 1f;
                    next = now + Every;
                    Dress(LitOn && enabled);
                    return;
                }
                if (now < next) return;
                next = now + Every;
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
            // No copy kept on the CPU: the slices are copied on the GPU below.
            try { array.Apply(false, true); } catch (System.Exception) { }
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
