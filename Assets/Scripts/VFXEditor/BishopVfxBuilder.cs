using UnityEditor;
using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// The bishop's F and E effects, made the new way, layer by layer (R167, 10-27, 사용자: "비숍은 거의 vfx가 없으니까
    /// 만들어줘 리얼리스틱하게 안해줘도 돼"; DECISIONS FX7), in the bishop's own silver (#C2C9D3, BSilver in
    /// SwordFightSkillFx.Board). Menu ChessFight/VFX/Bishop/Build writes the generated textures and materials, the prefabs
    /// of Prefabs/Bishop with every layer built so far and their names in Resources/VfxSets/Bishop; play them in the VFX
    /// workbench (piece 4). Values tuned in a prefab's layers are kept when it is built again; a material is only made
    /// when missing, so its tuned colour stays too. Layers: 1 the X sigil under whoever the stakes pin (F_Pin), 2 silver
    /// motes rising round it while the pin holds (F_Pin).
    /// </summary>
    public static class BishopVfxBuilder
    {
        const string SetPath = "Assets/Resources/VfxSets/Bishop.asset";
        const string QuadMesh = VfxAssets.Root + "/Meshes/VFX_FloorQuad.asset";
        const string PinPrefab = VfxAssets.Root + "/Prefabs/Bishop/01_Bishop_F_Pin.prefab";
        const string PinTexture = VfxAssets.Root + "/Textures/T_Bishop_PinSigil.png";
        const string PinMaterial = VfxAssets.Root + "/Materials/M_Bishop_PinSigil.mat";
        const string MoteTexture = VfxAssets.Root + "/Textures/T_Bishop_Mote.png";
        const string MoteMaterial = VfxAssets.Root + "/Materials/M_Bishop_Mote.mat";
        const string Textured = "ChessFight/VFX/Textured Additive";
        const string SigilLayer = "L1 Sigil", MotesLayer = "L2 Motes";
        static readonly Color BishopSilver = new Color(.76f, .79f, .83f);

        [MenuItem("ChessFight/VFX/Bishop/Build")]
        public static void Build()
        {
            // In Play mode the layers' Awake runs as they are made and what it does is saved into the prefab (VFX1 found a
            // flash quad saved lit that way): build in edit mode only.
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the bishop effects are built in edit mode.");
                return;
            }
            var pin = BuildPin();
            AssetDatabase.SaveAssets();
            Selection.activeObject = pin;
            Debug.Log("[VFX] Bishop built: layers 1 pin sigil, 2 motes. Workbench: piece 4, " + pin.name);
        }

        /// <summary>The menu is greyed out in Play mode.</summary>
        [MenuItem("ChessFight/VFX/Bishop/Build", true)]
        static bool CanBuild() => !EditorApplication.isPlaying;

        // ---------------------------------------------------------------- F: the pin

        /// <summary>
        /// The pin at whoever's feet the stakes drive in (0.8 s): layer 1 an X sigil on the ground, its X along the bishop's
        /// diagonals (the prefab's +Z is his way), opening from the centre with a flash and dying away as the pin ends;
        /// layer 2 silver motes rising round it, a few at once as the stakes bite and a stream while the pin holds.
        /// </summary>
        static GameObject BuildPin()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(PinPrefab);
            var root = new GameObject("01_Bishop_F_Pin");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 1.1f;

            // Layer 1: the X sigil.
            var tex = VfxAssets.Mask(PinTexture, 512, PinSigil(512));
            var quad = VfxAssets.SaveMesh(QuadMesh, VfxAssets.FloorQuad());
            var mat = VfxAssets.Material(PinMaterial, "ChessFight/VFX/Sigil Additive", out bool made);
            mat.SetTexture("_MainTex", tex);
            if (made) mat.SetColor("_Color", BishopSilver);
            EditorUtility.SetDirty(mat);
            var sigil = new GameObject(SigilLayer);
            sigil.transform.SetParent(root.transform, false);
            sigil.transform.localPosition = new Vector3(0f, .02f, 0f);
            sigil.AddComponent<MeshFilter>().sharedMesh = quad;
            var view = sigil.AddComponent<MeshRenderer>();
            view.sharedMaterial = mat;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            var play = sigil.AddComponent<VfxSigil>();
            // As long as the pin (bishopPinTime 0.8 s) and a little after; no turn, so the X stays on the stakes' diagonals.
            play.duration = 1f;
            play.reveal = .1f;
            play.flashIntensity = 4.5f;
            play.intensity = 1.8f;
            play.flashTime = .2f;
            play.startScale = 1.4f;
            play.endScale = 1.6f;
            play.spin = 0f;
            play.fadeOut = .3f;
            Keep(old, SigilLayer, play);

            // Layer 2: the motes.
            var mote = Material(MoteMaterial, Textured, VfxAssets.Mask(MoteTexture, 64, Mote(64)), BishopSilver, 2.2f);
            var motes = new GameObject(MotesLayer);
            motes.transform.SetParent(root.transform, false);
            motes.transform.localPosition = new Vector3(0f, .03f, 0f);
            Motes(motes, mote);
            KeepParticles(old, MotesLayer, motes);

            var prefab = VfxAssets.Prefab(PinPrefab, root);
            Register("F_Pin", prefab);
            return prefab;
        }

        /// <summary>
        /// The bishop's pin seal, white on black, filling the square: a ring with a thin one inside it and four ticks between
        /// them on the axes, an X along the diagonals from a diamond in the centre out to the inner ring, tapering as it
        /// goes, a small diamond where each arm meets the ring, over a faint fill. Anti-aliased to a pixel.
        /// </summary>
        static float[] PinSigil(int n)
        {
            var v = new float[n * n];
            float px = 2f / n;
            const float Rim = .9f, Inner = .78f, Corner = .5515f;   // Corner: the inner ring on a diagonal (Inner / √2)
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + .5f) / n * 2f - 1f, (y + .5f) / n * 2f - 1f);
                    float r = p.magnitude;
                    float m = .06f * Mathf.Clamp01(1f - r);
                    m = Mathf.Max(m, Band(Mathf.Abs(r - Rim), .026f, px));
                    m = Mathf.Max(m, .75f * Band(Mathf.Abs(r - Inner), .01f, px));
                    if (r > Inner && r < Rim) m = Mathf.Max(m, .8f * Band(Mathf.Min(Mathf.Abs(p.x), Mathf.Abs(p.y)), .009f, px));
                    if (r < Inner)
                    {
                        float arm = Mathf.Min(Mathf.Abs(p.x - p.y), Mathf.Abs(p.x + p.y)) * .70711f;
                        m = Mathf.Max(m, Band(arm, Mathf.Lerp(.05f, .012f, r / Inner), px));
                    }
                    m = Mathf.Max(m, Band(Mathf.Abs(p.x) + Mathf.Abs(p.y), .1f, px));
                    var c = new Vector2(Mathf.Abs(p.x) - Corner, Mathf.Abs(p.y) - Corner);
                    m = Mathf.Max(m, Band(Mathf.Abs(c.x) + Mathf.Abs(c.y), .055f, px));
                    v[y * n + x] = m;
                }
            return v;
        }

        /// <summary>1 inside <paramref name="half"/> of a line (by its distance <paramref name="d"/>), 0 outside, a pixel's blend.</summary>
        static float Band(float d, float half, float px) => Mathf.Clamp01((half - d) / px + .5f);

        /// <summary>
        /// Silver motes round the sigil: eight as the stakes bite and a stream of about 26 over the pin's 0.8 s, from a band
        /// round its rim, rising 0.5 to 1 m a second, turning slowly round it and wandering a little, shrinking as they fade.
        /// </summary>
        static void Motes(GameObject go, Material mote)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .8f;
            main.loop = false;
            main.playOnAwake = true;
            main.startDelay = .03f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.02f, .12f);
            main.startSize = new ParticleSystem.MinMaxCurve(.03f, .07f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;
            var emission = ps.emission;
            emission.rateOverTime = 32f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .72f;
            shape.radiusThickness = .35f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // x, y and z have to be in the same mode (two constants here).
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(.5f, 1.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(.6f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .25f;
            noise.frequency = 1.4f;
            noise.scrollSpeed = .5f;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.damping = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, .3f));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(.15f, .5f);
            var view = go.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = mote;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// <summary>A mote, white on black: a small hot core in a soft round glow.</summary>
        static float[] Mote(int n)
        {
            var v = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = new Vector2((x + .5f) / n * 2f - 1f, (y + .5f) / n * 2f - 1f).magnitude;
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                    float core = Mathf.Clamp01(1f - r * 3.5f);
                    v[y * n + x] = Mathf.Clamp01(glow * .7f + core);
                }
            return v;
        }

        /// <summary>White, coming up over <paramref name="rise"/> of its life, held to <paramref name="holdTo"/>, gone by the end.</summary>
        static Gradient Fade(float rise, float holdTo)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, rise), new GradientAlphaKey(1f, holdTo), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // ---------------------------------------------------------------- shared

        /// <summary>The values tuned on the old prefab's layer, copied onto the rebuilt one.</summary>
        static void Keep<T>(GameObject old, string layer, T fresh) where T : Component
        {
            var part = old != null ? old.transform.Find(layer) : null;
            var tuned = part != null ? part.GetComponent<T>() : null;
            if (tuned != null) EditorUtility.CopySerialized(tuned, fresh);
        }

        static void KeepParticles(GameObject old, string layer, GameObject fresh)
        {
            Keep(old, layer, fresh.GetComponent<ParticleSystem>());
            Keep(old, layer, fresh.GetComponent<ParticleSystemRenderer>());
        }

        /// <summary>The material at the path with this texture; colour and intensity only when it is first made (then they
        /// are the user's).</summary>
        static Material Material(string path, string shader, Texture2D tex, Color colour, float intensity)
        {
            var mat = VfxAssets.Material(path, shader, out bool made);
            mat.SetTexture("_MainTex", tex);
            if (made)
            {
                mat.SetColor("_Color", colour);
                mat.SetFloat("_Intensity", intensity);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Lists <paramref name="prefab"/> under <paramref name="key"/> in the bishop's set (made if missing), the
        /// name the skill code plays it by.</summary>
        static void Register(string key, GameObject prefab)
        {
            var set = AssetDatabase.LoadAssetAtPath<VfxSet>(SetPath);
            if (set == null)
            {
                VfxAssets.Folder("Assets/Resources/VfxSets");
                set = ScriptableObject.CreateInstance<VfxSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            var entry = new VfxSet.Entry { key = key, prefab = prefab };
            int i = set.entries.FindIndex(e => e.key == key);
            if (i >= 0) set.entries[i] = entry;
            else set.entries.Add(entry);
            EditorUtility.SetDirty(set);
        }
    }
}
