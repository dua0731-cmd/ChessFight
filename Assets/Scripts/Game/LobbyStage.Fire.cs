using UnityEngine;

namespace ChessFight.Game
{
    // The two braziers in the lobby castle burn (10-17, 승규 "화로 실제로 탁탁 거리면서 연기 나는 거"):
    // licking flames over the coals, sparks that jump out on every crackle, grey smoke drifting up,
    // the glow light flickering with it, and a looped crackle made in code (no audio files), played
    // from each bowl so the sparks and the pops land together.
    public sealed partial class LobbyStage
    {
        // Brazier bowls (Unity: x, coal height, depth), as placed in the Blender castle.
        static readonly Vector3[] BrazierCoals = { new Vector3(-7.6f, .66f, 7.6f), new Vector3(7.6f, .66f, 7.6f) };

        sealed class Fire
        {
            public ParticleSystem Flames, Sparks, Smoke;
            public Light Glow;
            public AudioSource Crackle;
            public float BaseIntensity, Seed;
            public int NextPop;
        }

        readonly System.Collections.Generic.List<Fire> fires = new System.Collections.Generic.List<Fire>();
        static Texture2D flameDot, smokePuff;
        static AudioClip crackleClip;
        static float[] crackleTimes;
        readonly System.Collections.Generic.List<Material> fireMaterials = new System.Collections.Generic.List<Material>();

        void Fires(Transform root)
        {
            var flameMaterial = LitMaterials.NewAdditiveParticles(FlameDot(), Color.white);
            var smokeMaterial = SmokeMaterial();
            if (flameMaterial == null || smokeMaterial == null) return;
            fireMaterials.Add(flameMaterial); fireMaterials.Add(smokeMaterial);
            var clip = CrackleClip();
            for (int i = 0; i < BrazierCoals.Length; i++)
            {
                var at = BrazierCoals[i];
                var holder = new GameObject("Brazier Fire");
                holder.transform.SetParent(root, false);
                holder.transform.localPosition = at;
                var fire = new Fire { Seed = i * 7.31f };
                fire.Flames = MakeFlames(holder.transform, flameMaterial);
                fire.Sparks = MakeSparks(holder.transform, flameMaterial);
                fire.Smoke = MakeSmoke(holder.transform, smokeMaterial);
                foreach (var light in root.GetComponentsInChildren<Light>())
                    if (light.name == "Brazier Glow" && (light.transform.localPosition - at).sqrMagnitude < 2f) { fire.Glow = light; fire.BaseIntensity = light.intensity; }
                fire.Crackle = holder.AddComponent<AudioSource>();
                fire.Crackle.clip = clip;
                fire.Crackle.loop = true;
                fire.Crackle.spatialBlend = 1f;
                fire.Crackle.rolloffMode = AudioRolloffMode.Linear;
                fire.Crackle.minDistance = 3f;
                fire.Crackle.maxDistance = 40f;
                fire.Crackle.volume = .2f;
                fire.Crackle.dopplerLevel = 0f;
                fire.Crackle.Play();
                // the two bowls start at different points of the loop so they don't crackle in step
                fire.Crackle.time = i * clip.length * .47f;
                fire.NextPop = NextPopIndex(fire.Crackle.time);
                fires.Add(fire);
            }
        }

        void TickFires(float t)
        {
            foreach (var fire in fires)
            {
                if (fire.Crackle != null && crackleTimes != null && crackleTimes.Length > 0)
                {
                    // a burst of sparks on every pop of the sound
                    float now = fire.Crackle.time;
                    int guard = 0;
                    while (guard++ < 8)
                    {
                        float pop = crackleTimes[fire.NextPop];
                        float last = crackleTimes[(fire.NextPop + crackleTimes.Length - 1) % crackleTimes.Length];
                        bool passed = pop <= now && (last < pop || now < last);
                        if (fire.NextPop == 0) passed = pop <= now && now < crackleTimes[crackleTimes.Length - 1];
                        if (!passed) break;
                        fire.Sparks.Emit(Random.Range(4, 11));
                        fire.NextPop = (fire.NextPop + 1) % crackleTimes.Length;
                    }
                }
                if (fire.Glow != null)
                {
                    float n = Mathf.PerlinNoise(t * 3.1f, fire.Seed) * .6f + Mathf.PerlinNoise(t * 9.7f, fire.Seed + 3f) * .4f;
                    fire.Glow.intensity = fire.BaseIntensity * (.85f + .25f * n);
                }
            }
        }

        static int NextPopIndex(float time)
        {
            if (crackleTimes == null || crackleTimes.Length == 0) return 0;
            for (int i = 0; i < crackleTimes.Length; i++) if (crackleTimes[i] > time) return i;
            return 0;
        }

        ParticleSystem MakeFlames(Transform parent, Material material)
        {
            var ps = NewSystem(parent, "Flames", material);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(.22f, .48f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.4f, .4f);
            main.maxParticles = 120;
            var emission = ps.emission; emission.rateOverTime = 42f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .26f; shape.rotation = new Vector3(-90f, 0, 0);
            var color = ps.colorOverLifetime; color.enabled = true;
            color.color = Gradient(
                new[] { (0f, new Color(1f, .86f, .5f)), (.25f, new Color(1f, .58f, .16f)), (.65f, new Color(.92f, .26f, .05f)), (1f, new Color(.35f, .06f, .02f)) },
                new[] { (0f, 0f), (.12f, .7f), (.6f, .4f), (1f, 0f) });
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .7f), new Keyframe(.25f, 1f), new Keyframe(1, .25f)));
            var noise = ps.noise; noise.enabled = true; noise.strength = .35f; noise.frequency = 1.6f; noise.scrollSpeed = 1.2f;
            return ps;
        }

        ParticleSystem MakeSparks(Transform parent, Material material)
        {
            var ps = NewSystem(parent, "Sparks", material);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.9f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .78f, .35f), new Color(1f, .5f, .12f));
            main.gravityModifier = .18f;
            main.maxParticles = 160;
            var emission = ps.emission; emission.rateOverTime = 3f;   // a few between the pops
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 28f; shape.radius = .2f; shape.rotation = new Vector3(-90f, 0, 0);
            var color = ps.colorOverLifetime; color.enabled = true;
            color.color = Gradient(new[] { (0f, new Color(1f, .9f, .6f)), (.5f, new Color(1f, .5f, .12f)), (1f, new Color(.6f, .12f, .03f)) },
                                   new[] { (0f, 1f), (.7f, .85f), (1f, 0f) });
            var noise = ps.noise; noise.enabled = true; noise.strength = .9f; noise.frequency = 2.2f; noise.scrollSpeed = .8f;
            var trails = ps.trails; trails.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = .05f; renderer.lengthScale = 1.2f;
            return ps;
        }

        ParticleSystem MakeSmoke(Transform parent, Material material)
        {
            var ps = NewSystem(parent, "Smoke", material);
            ps.transform.localPosition = new Vector3(0, .35f, 0);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.35f, .7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.45f, .8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 60;
            var emission = ps.emission; emission.rateOverTime = 7f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .22f; shape.rotation = new Vector3(-90f, 0, 0);
            var color = ps.colorOverLifetime; color.enabled = true;
            color.color = Gradient(new[] { (0f, new Color(.32f, .29f, .26f)), (1f, new Color(.62f, .6f, .58f)) },
                                   new[] { (0f, 0f), (.15f, .32f), (.6f, .18f), (1f, 0f) });
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .6f), new Keyframe(1, 3.2f)));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.4f, .4f);
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(.08f, .22f); velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f); velocity.z = new ParticleSystem.MinMaxCurve(0f, .05f);
            var noise = ps.noise; noise.enabled = true; noise.strength = .25f; noise.frequency = .5f; noise.scrollSpeed = .3f;
            return ps;
        }

        static ParticleSystem NewSystem(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.loop = true;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }

        static ParticleSystem.MinMaxGradient Gradient((float t, Color c)[] colors, (float t, float a)[] alphas)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i].c, colors[i].t);
            var ak = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].a, alphas[i].t);
            g.SetKeys(ck, ak);
            return new ParticleSystem.MinMaxGradient(g);
        }

        // A soft round dot, brighter in the middle (flames and sparks).
        static Texture2D FlameDot()
        {
            if (flameDot != null) return flameDot;
            const int n = 64;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            flameDot = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Brazier flame dot", wrapMode = TextureWrapMode.Clamp };
            flameDot.SetPixels(px); flameDot.Apply(true);
            return flameDot;
        }

        // A cloudy grey puff: a soft disc broken up by a little noise.
        static Material SmokeMaterial()
        {
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null) return null;
            if (smokePuff == null)
            {
                const int n = 96;
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float cloud = Mathf.PerlinNoise(x * .08f + 3.1f, y * .08f + 7.7f) * .65f + Mathf.PerlinNoise(x * .21f, y * .21f) * .35f;
                        float a = Mathf.Clamp01(1f - r) * Mathf.Lerp(.45f, 1f, cloud);
                        a = a * a;
                        px[y * n + x] = new Color(1f, 1f, 1f, a);
                    }
                smokePuff = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Brazier smoke", wrapMode = TextureWrapMode.Clamp };
                smokePuff.SetPixels(px); smokePuff.Apply(true);
            }
            var m = new Material(shader) { mainTexture = smokePuff, color = Color.white, name = "Brazier smoke" };
            LitMaterials.MakeTransparent(m, additive: false);
            return m;
        }

        // Eight seconds of a fire crackling: a low rushing bed, sharp ticks and now and then a louder pop
        // (a short click and its small echo). The pop times are kept so the sparks can jump with them.
        static AudioClip CrackleClip()
        {
            if (crackleClip != null) return crackleClip;
            const int rate = 44100; const float seconds = 8f;
            int count = (int)(rate * seconds);
            var data = new float[count];
            var random = new System.Random(1017);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < count; i++)
            {
                float white = (float)(random.NextDouble() * 2 - 1);
                lp += (white - lp) * .02f;       // low rush
                lp2 += (lp - lp2) * .05f;
                float wobble = .7f + .3f * Mathf.Sin(i / (float)rate * 1.3f) * Mathf.Sin(i / (float)rate * .37f);
                data[i] = lp2 * 1.6f * wobble;
            }
            var pops = new System.Collections.Generic.List<float>();
            float at = .05f;
            while (at < seconds - .1f)
            {
                bool big = random.NextDouble() < .28;
                float amp = big ? .75f + (float)random.NextDouble() * .25f : .18f + (float)random.NextDouble() * .3f;
                int start = (int)(at * rate);
                int clicks = big ? 1 + random.Next(3) : 1;
                for (int c = 0; c < clicks; c++)
                {
                    int s0 = start + c * random.Next(300, 1400);
                    float decay = big ? 0.0025f : 0.0012f;
                    int len = (int)(decay * rate * 6);
                    float tone = 1800f + (float)random.NextDouble() * 2600f;
                    for (int k = 0; k < len && s0 + k < count; k++)
                    {
                        float tt = k / (float)rate;
                        float env = Mathf.Exp(-tt / decay);
                        float noise = (float)(random.NextDouble() * 2 - 1);
                        data[s0 + k] += amp * env * (noise * .7f + Mathf.Sin(2f * Mathf.PI * tone * tt) * .3f) * (c == 0 ? 1f : .55f);
                    }
                }
                if (big) pops.Add(at);
                at += big ? .25f + (float)random.NextDouble() * .7f : .04f + (float)random.NextDouble() * .22f;
            }
            float peak = 0f;
            for (int i = 0; i < count; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = peak > 0f ? .9f / peak : 1f;
            // a short crossfade so the loop has no click at the seam
            int fade = rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                data[i] = data[i] * w + data[count - fade + i] * (1f - w);
            }
            for (int i = 0; i < count; i++) data[i] *= gain;
            crackleClip = AudioClip.Create("Brazier crackle", count - fade, 1, rate, false);
            var trimmed = new float[count - fade];
            System.Array.Copy(data, trimmed, count - fade);
            crackleClip.SetData(trimmed, 0);
            crackleTimes = pops.ToArray();
            return crackleClip;
        }
    }
}
