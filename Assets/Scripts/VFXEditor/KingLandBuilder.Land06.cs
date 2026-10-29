using UnityEditor;
using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// The king's return landing 06_King_E_Land (the game's own until R171, when the user chose 10_King_E_Land_New: now
    /// the workbench's only, and this build does not touch E_Land), made again the new way and more striking (R169, 10-27, 사용자 "6번을 더 멋있게 만들어줘 그래픽을"): the flash and the
    /// streak as before, gold cracks running out from where he lands, the disc of R163 breaking apart over them, a soft
    /// shock band, gold sparks bouncing off the ground, a ring of gold dust swept out along it, and embers drifting up.
    /// Saved over the old prefab at the same path; values tuned in its layers and materials are kept when it is built again. Menu ChessFight/VFX/King Land (06, game)/Build.
    /// </summary>
    public static partial class KingLandBuilder
    {
        const string Land06Path = VfxAssets.Root + "/Prefabs/King/06_King_E_Land.prefab";

        [MenuItem("ChessFight/VFX/King Land (06, game)/Build")]
        public static void BuildLand06()
        {
            if (EditorApplication.isPlaying)
            {
                // Built in Play mode, its parts' Awake runs and what they make at run time is saved into the prefab.
                EditorUtility.DisplayDialog("VFX", "Play 모드를 끄고 다시 눌러 주세요.", "OK");
                return;
            }
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(Land06Path);
            var root = new GameObject("06_King_E_Land");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 1.8f;

            var quad = VfxAssets.SaveMesh(QuadMesh, VfxAssets.FloorQuad());
            var glow = AssetDatabase.LoadAssetAtPath<Material>(Mat + "VFX_King_Glow.mat");
            var ring = AssetDatabase.LoadAssetAtPath<Material>(Mat + "VFX_King_Ring.mat");
            var spark = Material(Mat + "M_King_Spark.mat", Textured, VfxAssets.Mask(Tex + "T_King_Spark.png", 128, Spark(128)), KingGold, 3f);
            var puff = Material(Mat + "M_King_SmokePuff.mat", Textured, VfxAssets.Mask(Tex + "T_King_SmokePuff.png", 256, Puff(256)), KingGold, 1.2f);
            var flare = Material(Mat + "M_King_LandFlare.mat", Textured, VfxAssets.Mask(Tex + "T_King_FlareStreak.png", 256, Streak(256)), FlareWhite, 4f);
            var cracks = Material(Mat + "M_King_GroundCracks.mat", Sigil, VfxAssets.Mask(Tex + "T_King_GroundCracks.png", 512, Cracks(512)), KingGold, 2.4f);

            const string FlashLayer = "L1 Flash", StreakLayer = "L1 Streak", CrackLayer = "L2 Cracks", ShatterLayer = "L2 Shatter",
                ShockLayer = "L3 Shock", SparkLayer = "L4 Sparks", DustLayer = "L4 Dust Ring", EmberLayer = "L5 Embers";

            var flash = Part(root, FlashLayer, new Vector3(0f, .35f, 0f)).AddComponent<VfxFlash>();
            flash.material = glow;
            flash.duration = .12f;
            flash.startSize = .3f;
            flash.endSize = 1f;
            flash.intensity = 5f;
            Keep(old, FlashLayer, flash);

            var streak = Part(root, StreakLayer, new Vector3(0f, .45f, 0f)).AddComponent<VfxFlash>();
            streak.material = flare;
            streak.duration = .3f;
            streak.startSize = .4f;
            streak.endSize = .6f;
            streak.aspect = 10f;
            streak.intensity = 3.5f;
            Keep(old, StreakLayer, streak);

            var crack = Part(root, CrackLayer, new Vector3(0f, .028f, 0f));
            crack.AddComponent<MeshFilter>().sharedMesh = quad;
            Renderer(crack.AddComponent<MeshRenderer>(), cracks);
            var run = crack.AddComponent<VfxSigil>();
            run.duration = 1.8f;
            run.reveal = .1f;
            run.flashIntensity = 5f;
            run.intensity = 2f;
            run.flashTime = .3f;
            run.startScale = 4.4f;
            run.endScale = 4.6f;
            run.spin = 0f;
            run.fadeOut = .5f;
            Keep(old, CrackLayer, run);

            var shatter = Part(root, ShatterLayer, new Vector3(0f, .025f, 0f)).AddComponent<VfxShatter>();
            shatter.material = ring;
            shatter.radius = 2f;
            shatter.sectors = 10;
            shatter.rings = 3;
            shatter.whole = .16f;
            shatter.duration = 1.7f;
            shatter.intensity = 2.2f;
            shatter.pieceIntensity = 1.1f;
            Keep(old, ShatterLayer, shatter);

            var shock = Part(root, ShockLayer, new Vector3(0f, .03f, 0f)).AddComponent<VfxRing>();
            shock.material = ring;
            shock.shape = VfxRing.Shape.Floor;
            shock.startRadius = .3f;
            shock.endRadius = 2.8f;
            shock.width = .22f;
            shock.duration = .35f;
            shock.fadeIn = .01f;
            shock.intensity = 2f;
            shock.dissolveEnd = .4f;
            Keep(old, ShockLayer, shock);

            var sparks = Part(root, SparkLayer, new Vector3(0f, .1f, 0f));
            Sparks(sparks, spark);
            KeepParticles(old, SparkLayer, sparks);
            var dust = Part(root, DustLayer, new Vector3(0f, .12f, 0f));
            DustRing(dust, puff);
            KeepParticles(old, DustLayer, dust);
            var embers = Part(root, EmberLayer, new Vector3(0f, .1f, 0f));
            Embers(embers, spark);
            KeepParticles(old, EmberLayer, embers);

            var prefab = VfxAssets.Prefab(Land06Path, root);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log("[VFX] King land 06 (game) built: flash, streak, cracks, shatter, shock, sparks, dust ring, embers. Workbench: piece 1, " + prefab.name);
        }

        /// <summary>The effect list's entry <paramref name="key"/> set to <paramref name="prefab"/> (added if missing). A prefab
        /// saved over by the editor gets a new id for its root, so a list pointing at it has to be set again.</summary>
        static void Register(string setPath, string key, GameObject prefab)
        {
            var set = AssetDatabase.LoadAssetAtPath<VfxSet>(setPath);
            if (set == null)
            {
                Debug.LogWarning("[VFX] No effect list at " + setPath + "; " + key + " not registered.");
                return;
            }
            int at = set.entries.FindIndex(e => e.key == key);
            var entry = new VfxSet.Entry { key = key, prefab = prefab };
            if (at >= 0) set.entries[at] = entry;
            else set.entries.Add(entry);
            EditorUtility.SetDirty(set);
        }

        /// <summary>Forty gold sparks thrown up and out, stretched along their way, bouncing off the ground and fading.</summary>
        static void Sparks(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .2f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .055f);
            main.startColor = Color.white;
            main.gravityModifier = 2.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = .25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .3f)));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .6f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = .4f;
            collision.bounce = .35f;
            collision.lifetimeLoss = .15f;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.radiusScale = .5f;
            var view = go.GetComponent<ParticleSystemRenderer>();
            Renderer(view, mat);
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = .045f;
            view.lengthScale = 1.2f;
        }

        /// <summary>A ring of gold dust swept out along the ground from under him, slowing, spreading and thinning away.</summary>
        static void DustRing(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .2f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.6f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.5f, .9f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, .5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .35f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var slow = ps.limitVelocityOverLifetime;
            slow.enabled = true;
            slow.limit = new ParticleSystem.MinMaxCurve(.4f);
            slow.dampen = .12f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // x, y and z have to be in the same mode (one constant each here).
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(.3f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .8f, 1f, 2.2f));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(.08f, .5f);
            var view = go.GetComponent<ParticleSystemRenderer>();
            Renderer(view, mat);
            view.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// <summary>Twenty embers drifting up from round the landing, wandering, coming up and dying away.</summary>
        static void Embers(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .4f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.02f, .04f);
            main.startColor = Color.white;
            main.gravityModifier = -.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(.05f, 20) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.4f;
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // x, y and z have to be in the same mode (two constants here).
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(.6f, 1.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .5f;
            noise.frequency = 1.2f;
            noise.scrollSpeed = .4f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Fade(.1f, .6f);
            var view = go.GetComponent<ParticleSystemRenderer>();
            Renderer(view, mat);
            view.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// <summary>A spark: a hot point with a soft falloff.</summary>
        static float[] Spark(int n)
        {
            var v = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + .5f) / n * 2f - 1f, w = (y + .5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + w * w);
                    v[y * n + x] = Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - r), 3f) + Mathf.Exp(-r * r * 60f) * .5f);
                }
            return v;
        }

        /// <summary>
        /// Cracks in the ground, white on black, from a small crater at the centre: nine main cracks wandering out to near the
        /// rim, each thinning and dimming as it goes, with a branch or two off it. A fixed seed, so the same every build.
        /// </summary>
        static float[] Cracks(int n)
        {
            var v = new float[n * n];
            var random = new System.Random(4021);
            float Rand(float a, float b) => a + (float)random.NextDouble() * (b - a);
            void Crack(Vector2 from, float angle, float length, float width, float bright, int branches)
            {
                Vector2 at = from;
                float gone = 0f;
                while (gone < length)
                {
                    float step = Rand(.04f, .08f);
                    angle += Rand(-.45f, .45f);
                    var next = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * step;
                    float k = gone / length;
                    Stamp(v, n, at, next, width * (1f - .75f * k), bright * (1f - .55f * k));
                    if (branches > 0 && k > .2f && Rand(0f, 1f) < .18f)
                    {
                        branches--;
                        Crack(next, angle + Rand(.6f, 1f) * (Rand(0f, 1f) < .5f ? -1f : 1f), length * Rand(.25f, .4f), width * .55f, bright * .8f, 0);
                    }
                    at = next;
                    gone += step;
                }
            }
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2f / 9f + Rand(-.2f, .2f);
                Crack(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .08f, angle, Rand(.65f, .88f), .012f, 1f, 2);
            }
            float px = 2f / n;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2((x + .5f) / n * 2f - 1f, (y + .5f) / n * 2f - 1f);
                    float r = p.magnitude;
                    float crater = Mathf.Max(Ring(r, .1f, .014f, px), (1f - Smooth(.07f, .09f, r)) * .45f);
                    v[y * n + x] = Mathf.Max(v[y * n + x], crater) * (1f - Smooth(.95f, 1f, r));
                }
            return v;
        }

        /// <summary>A line from a to b (in the -1..1 square) into the mask, anti-aliased, the brighter value kept.</summary>
        static void Stamp(float[] v, int n, Vector2 a, Vector2 b, float half, float bright)
        {
            float px = 2f / n;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - half - px + 1f) / px));
            int x1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + half + px + 1f) / px));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - half - px + 1f) / px));
            int y1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + half + px + 1f) / px));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2((x + .5f) / n * 2f - 1f, (y + .5f) / n * 2f - 1f);
                    float m = Line(Segment(p, a, b), half, px) * bright;
                    if (m > v[y * n + x]) v[y * n + x] = m;
                }
        }
    }
}
