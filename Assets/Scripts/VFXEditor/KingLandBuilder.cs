using UnityEditor;
using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// The king's landing, made again the new way (R168, 10-27, 사용자: "킹 착지 vfx가 밋밋해 더 멋있게 해주고 리얼리스틱으로
    /// 안해줘도 돼", then "Ground Runes Decal랑 Golden Smoke Tendrils 랑 Anamorphic Lens Flare 합쳐줘" from the Vengeful
    /// Twilight breakdown; DECISIONS FX7). Menu ChessFight/VFX/King Land (new)/Build writes the generated textures, mesh and
    /// materials and the prefab Prefabs/King/10_King_E_Land_New; play it in the VFX workbench (piece 1). Values tuned in the
    /// prefab's layers (components, particle systems) and in its materials are kept when it is built again.
    /// Layers: 1 the king's seal on the ground (a crown and cross in a jewelled ring; it was a rune circle until the user
    /// found it "너무 마법"), 2 golden smoke tendrils curling up off it (Shuriken with trails, and soft puffs), 3 an
    /// anamorphic streak across the screen as he lands. Since R171 this is the game's landing: each build sets it as E_Land
    /// in Resources/VfxSets/King. The second menu rebuilds 06_King_E_Land (KingLandBuilder.Land06.cs), now workbench only.
    /// </summary>
    public static partial class KingLandBuilder
    {
        const string PrefabPath = VfxAssets.Root + "/Prefabs/King/10_King_E_Land_New.prefab";
        const string Tex = VfxAssets.Root + "/Textures/";
        const string Mat = VfxAssets.Root + "/Materials/";
        const string QuadMesh = VfxAssets.Root + "/Meshes/VFX_FloorQuad.asset";
        const string KingSet = "Assets/Resources/VfxSets/King.asset";
        const string Sigil = "ChessFight/VFX/Sigil Additive", Textured = "ChessFight/VFX/Textured Additive";
        const string SealLayer = "L1 King Seal", TendrilLayer = "L2 Smoke Tendrils", PuffLayer = "L2 Smoke Puffs", FlareLayer = "L3 Lens Flare";
        static readonly Color KingGold = new Color(1f, .76f, .38f);
        static readonly Color FlareWhite = new Color(1f, .93f, .78f);

        [MenuItem("ChessFight/VFX/King Land (new)/Build")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                // Built in Play mode, its parts' Awake runs and what they make at run time is saved into the prefab.
                EditorUtility.DisplayDialog("VFX", "Play 모드를 끄고 다시 눌러 주세요.", "OK");
                return;
            }
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var root = new GameObject("10_King_E_Land_New");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 2.2f;

            // Layer 1: the king's seal, opening from where he lands, upright to his facing (it does not turn).
            foreach (string gone in new[] { Tex + "T_King_LandSigil.png", Mat + "M_King_LandSigil.mat", Tex + "T_King_LandRunes.png", Mat + "M_King_LandRunes.mat" })
                AssetDatabase.DeleteAsset(gone);
            var quad = VfxAssets.SaveMesh(QuadMesh, VfxAssets.FloorQuad());
            var seal = Material(Mat + "M_King_LandSeal.mat", Sigil, VfxAssets.Mask(Tex + "T_King_LandSeal.png", 512, Seal(512)), KingGold, 2f);
            var l1 = Part(root, SealLayer, new Vector3(0f, .03f, 0f));
            l1.AddComponent<MeshFilter>().sharedMesh = quad;
            Renderer(l1.AddComponent<MeshRenderer>(), seal);
            var sigil = l1.AddComponent<VfxSigil>();
            sigil.spin = 0f;
            sigil.reveal = .16f;
            sigil.flashIntensity = 4f;
            sigil.intensity = 2f;
            sigil.startScale = 3f;
            sigil.endScale = 3.4f;
            Keep(old, SealLayer, sigil);

            // Layer 2: golden smoke curling up off the circle, as strands (trails) and soft puffs.
            var strand = Material(Mat + "M_King_SmokeStrand.mat", Textured, VfxAssets.Mask(Tex + "T_King_SmokeStrand.png", 256, Strand(256)), KingGold, 1.8f);
            var puff = Material(Mat + "M_King_SmokePuff.mat", Textured, VfxAssets.Mask(Tex + "T_King_SmokePuff.png", 256, Puff(256)), KingGold, 1.2f);
            var l2 = Part(root, TendrilLayer, new Vector3(0f, .05f, 0f));
            Tendrils(l2, puff, strand);
            KeepParticles(old, TendrilLayer, l2);
            var l2b = Part(root, PuffLayer, new Vector3(0f, .05f, 0f));
            Puffs(l2b, puff);
            KeepParticles(old, PuffLayer, l2b);

            // Layer 3: the anamorphic streak, at chest height as he lands.
            var flare = Material(Mat + "M_King_LandFlare.mat", Textured, VfxAssets.Mask(Tex + "T_King_FlareStreak.png", 256, Streak(256)), FlareWhite, 4f);
            var l3 = Part(root, FlareLayer, new Vector3(0f, .6f, 0f));
            var streak = l3.AddComponent<VfxFlash>();
            streak.material = flare;
            streak.duration = .4f;
            streak.startSize = .5f;
            streak.endSize = .7f;
            streak.aspect = 14f;
            streak.intensity = 4f;
            Keep(old, FlareLayer, streak);

            var prefab = VfxAssets.Prefab(PrefabPath, root);
            // R171 (사용자 "킹은 적용시켜줘" → 9번으로 교체): this is the game's landing now (VfxSets/King E_Land).
            Register(KingSet, "E_Land", prefab);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log("[VFX] King land (new) built, and set as the game's landing (E_Land): L1 king seal, L2 smoke tendrils + puffs, L3 lens flare. Workbench: piece 1, " + prefab.name);
        }

        static GameObject Part(GameObject root, string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = at;
            return go;
        }

        /// <summary>The material at the path with this texture; colour and intensity only when it is first made (then they are the user's).</summary>
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

        static void Renderer(Renderer view, Material mat)
        {
            view.sharedMaterial = mat;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
        }

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

        // ---------------------------------------------------------------- layer 2: the smoke

        /// <summary>
        /// Ten strands of smoke: they start round the circle's rim, swirl round it (orbital velocity) and rise 1.5 m or so,
        /// wandering (noise), each drawing a trail that thins to nothing behind it. The heads are faint puffs.
        /// </summary>
        static void Tendrils(GameObject go, Material head, Material trail)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .4f);
            main.startSize = new ParticleSystem.MinMaxCurve(.14f, .24f);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(.04f, 10) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.3f;
            shape.radiusThickness = .25f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // x, y and z have to be in the same mode (two constants here).
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(.8f, 1.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(1.6f);
            velocity.radial = new ParticleSystem.MinMaxCurve(-.35f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .7f;
            noise.frequency = .9f;
            noise.scrollSpeed = .6f;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.High;
            noise.damping = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .6f, 1f, 1.6f));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(.12f, .55f);
            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 1f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(.55f);
            trails.minVertexDistance = .04f;
            trails.worldSpace = true;
            trails.dieWithParticles = false;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.sizeAffectsWidth = true;
            trails.inheritParticleColor = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            trails.colorOverTrail = Fade(0f, 0f, true);
            var view = go.GetComponent<ParticleSystemRenderer>();
            Renderer(view, head);
            view.trailMaterial = trail;
            view.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// <summary>Soft golden puffs rising slowly round the circle, turning and spreading, never thicker than a haze.</summary>
        static void Puffs(GameObject go, Material puff)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .3f);
            main.startSize = new ParticleSystem.MinMaxCurve(.6f, 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, .45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.1f;
            shape.radiusThickness = .6f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // x, y and z have to be in the same mode (two constants here).
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(.35f, .7f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(.8f);
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-.8f, .8f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .7f, 1f, 2f));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(.2f, .4f);
            var view = go.GetComponent<ParticleSystemRenderer>();
            Renderer(view, puff);
            view.renderMode = ParticleSystemRenderMode.Billboard;
            view.sortMode = ParticleSystemSortMode.Distance;
        }

        /// <summary>White, coming up over <paramref name="rise"/> of its life, held, gone by the end (or fading all the way
        /// from the start for a trail).</summary>
        static Gradient Fade(float rise, float holdTo, bool trail = false)
        {
            var g = new Gradient();
            var white = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            g.SetKeys(white, trail
                ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, rise), new GradientAlphaKey(.8f, holdTo), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // ---------------------------------------------------------------- the generated textures

        /// <summary>
        /// The king's seal, white on black, filling the square: a heavy outer ring and a thin one, a ring of studs (large and
        /// small in turn) and an inner ring round a crown - a band on a base, five points with balls on the outer four and a
        /// cross on the middle one - drawn as a bright outline over a dim fill, over a faint glow. Upright: the cross toward
        /// +V, which the prefab lays toward his facing. Anti-aliased to a pixel.
        /// </summary>
        static float[] Seal(int n)
        {
            const int Studs = 24;
            var v = new float[n * n];
            float px = 2f / n;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + .5f) / n * 2f - 1f, (y + .5f) / n * 2f - 1f);
                    float r = p.magnitude, a = Mathf.Atan2(p.y, p.x);
                    float m = .06f * Mathf.Clamp01(1f - r);
                    m = Mathf.Max(m, Ring(r, .955f, .02f, px));
                    m = Mathf.Max(m, Ring(r, .9f, .006f, px) * .85f);
                    float seg = 2f * Mathf.PI / Studs;
                    int i = Mathf.FloorToInt(Mathf.Repeat(a, 2f * Mathf.PI) / seg) % Studs;
                    float along = (Mathf.Repeat(a, seg) - seg * .5f) * r;
                    float stud = Mathf.Sqrt(along * along + (r - .84f) * (r - .84f));
                    m = Mathf.Max(m, Line(stud, i % 2 == 0 ? .016f : .009f, px));
                    m = Mathf.Max(m, Ring(r, .775f, .009f, px));
                    float d = Crown(p);
                    // A dim fill inside, a bright outline along the edge.
                    m = Mathf.Max(m, (1f - Smooth(0f, px, d)) * .3f);
                    m = Mathf.Max(m, Line(Mathf.Abs(d), .009f, px));
                    m *= 1f - Smooth(.975f, 1f, r);
                    v[y * n + x] = m;
                }
            return v;
        }

        /// <summary>Signed distance to the crown and its cross (below 0 inside), in the seal's -1..1 square.</summary>
        static float Crown(Vector2 p)
        {
            float d = Box(p, new Vector2(0f, -.36f), new Vector2(.3f, .035f));
            d = Mathf.Min(d, Box(p, new Vector2(0f, -.25f), new Vector2(.36f, .06f)));
            float[] at = { -.3f, -.15f, 0f, .15f, .3f }, tip = { .02f, .12f, .14f, .12f, .02f };
            for (int k = 0; k < 5; k++)
            {
                d = Mathf.Min(d, Polygon(p, new Vector2(at[k] - .085f, -.2f), new Vector2(at[k] + .085f, -.2f), new Vector2(at[k], tip[k])));
                if (k != 2) d = Mathf.Min(d, (p - new Vector2(at[k], tip[k] + .03f)).magnitude - .035f);
            }
            d = Mathf.Min(d, Box(p, new Vector2(0f, .3f), new Vector2(.03f, .16f)));
            d = Mathf.Min(d, Box(p, new Vector2(0f, .34f), new Vector2(.1f, .03f)));
            return d;
        }

        static float Box(Vector2 p, Vector2 centre, Vector2 half)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - half;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        }

        /// <summary>Signed distance to a triangle (any winding).</summary>
        static float Polygon(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float edge = Mathf.Min(Segment(p, a, b), Mathf.Min(Segment(p, b, c), Segment(p, c, a)));
            float s1 = Cross(b - a, p - a), s2 = Cross(c - b, p - b), s3 = Cross(a - c, p - c);
            bool inside = (s1 >= 0f && s2 >= 0f && s3 >= 0f) || (s1 <= 0f && s2 <= 0f && s3 <= 0f);
            return inside ? -edge : edge;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>A wisp for a trail: u along the strand, v across it; a soft band whose width and brightness wander, torn
        /// into threads by noise.</summary>
        static float[] Strand(int n)
        {
            var v = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + .5f) / n, w = (y + .5f) / n;
                    float width = .2f + .12f * Fbm(u * 5f, 3.1f);
                    float band = Mathf.Exp(-Mathf.Pow((w - .5f - .08f * (Fbm(u * 3f, 7.7f) - .5f)) / width, 2f) * 2.2f);
                    float threads = Mathf.Lerp(.45f, 1f, Fbm(u * 10f, w * 6f));
                    v[y * n + x] = band * threads;
                }
            return v;
        }

        /// <summary>A soft puff of smoke: a round falloff broken up by noise, no hard edge anywhere.</summary>
        static float[] Puff(int n)
        {
            var v = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + .5f) / n * 2f - 1f, w = (y + .5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + w * w);
                    float body = Mathf.Clamp01(1f - r);
                    body *= body;
                    v[y * n + x] = body * Mathf.Lerp(.35f, 1.1f, Fbm(u * 3f + 4f, w * 3f + 9f));
                }
            return v;
        }

        /// <summary>An anamorphic streak: a thin hot line across the middle, a wider soft glow round it, both fading toward the
        /// ends; the quad is stretched across the screen (VfxFlash.aspect).</summary>
        static float[] Streak(int n)
        {
            var v = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs((x + .5f) / n * 2f - 1f), dy = (y + .5f) / n * 2f - 1f;
                    float along = 1f - Mathf.Pow(dx, 1.4f);
                    float core = Mathf.Exp(-Mathf.Pow(dy / .035f, 2f)) * along;
                    float glow = Mathf.Exp(-Mathf.Pow(dy / .22f, 2f)) * along * along * .35f;
                    float centre = Mathf.Exp(-(dx * dx * 40f + dy * dy * 40f)) * .6f;
                    v[y * n + x] = Mathf.Clamp01(core + glow + centre);
                }
            return v;
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
            }
        }

        static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx), b = Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        static float Fbm(float x, float y) => Noise(x, y) * .55f + Noise(x * 2.1f + 13f, y * 2.1f + 7f) * .3f + Noise(x * 4.3f + 3f, y * 4.3f + 19f) * .15f;

        static float Ring(float r, float centre, float half, float px) => 1f - Smooth(half, half + px, Mathf.Abs(r - centre));
        static float Line(float d, float half, float px) => 1f - Smooth(half, half + px, d);

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }
    }
}
