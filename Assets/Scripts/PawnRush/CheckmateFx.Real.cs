using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R688 (seoungtest) (user 11-17: "8번 9번 0번 vfx 가 필요해 아직 작업 x"; the house rules: "실사로", "그냥 그려놓은 거 같은
    // 느낌이야 진짜 vfx로", "원은 사용 하지 않는 vfx", not ArenaFx's "이런 vfx 말고"): the real-light half of H's effects.
    // R510 left H with flat pictures: the attacked squares were a flat team-coloured band (Sprites/Default at 0.28),
    // the frozen king wore a LineRenderer ring, every moment came without light, and the capture's bits only floated.
    // Here, after the Sword Fight real queen (light that adds onto the floor) and YardFx (lit chips that bounce):
    //   floor light   the attacked squares and the check / freeze / capture squares are light added onto the board,
    //                 bright along the square's seams and softer inside, with a slight flicker (FloorLight)
    //   flashes       a short-lived point light for each moment (check red, freeze and capture gold, rescue the rescuer's
    //                 colour, stalemate cool), bright at once and out in a fraction of a second
    //   frozen glow   a frozen king is lit gold from below by its own light, slow gold motes drift up off it and a glint
    //                 catches on it now and then (in place of the line ring)
    //   chips         real marble chips (a lit, tumbling mesh) that fall and bounce on the board: capture, rescue, tray
    //   glints        four-point star pops on the check dot that just lit, the freeze, the capture
    //   sweep         the last king in the tray: gold glints rise all along the tray under a warm light
    // Pictures only, from the hunt every PC already draws; nothing is sent and no rule reads it.
    public sealed partial class CheckmateFx
    {
        static Texture2D floorTex;
        static Mesh chunk;
        static Material floorLight;
        ParticleSystem chips, motes, glints, haze;
        Material chipMaterial;

        struct Flash { public Light light; public float born, life, peak; }
        readonly List<Flash> flashes = new List<Flash>();
        readonly Light[] kingLights = new Light[ChessFight.Network.CheckmateHunt.MaxKings];
        readonly float[] nextMoteAt = new float[ChessFight.Network.CheckmateHunt.MaxKings], nextGlintAt = new float[ChessFight.Network.CheckmateHunt.MaxKings];

        static readonly Color Warm = new Color(1f, .78f, .42f), Cool = new Color(.66f, .76f, 1f), MarbleChip = new Color(.93f, .9f, .84f);

        void AwakeReal()
        {
            RealTextures();

            // Marble chips: a lit chunk mesh, tumbling in 3D, real gravity, bouncing on the board (static colliders only).
            chips = Particles("H marble chips", dot, false, ParticleSystemRenderMode.Mesh, 160);
            var r = chips.GetComponent<ParticleSystemRenderer>();
            var lit = LitMaterials.Lit;
            if (lit != null)
            {
                chipMaterial = new Material(lit) { name = "H marble chips", color = MarbleChip };
                LitMaterials.SetSmoothness(chipMaterial, .62f);
                r.sharedMaterial = chipMaterial;
            }
            r.mesh = chunk;
            r.alignment = ParticleSystemRenderSpace.World;
            var main = chips.main;
            main.gravityModifier = 1f;
            main.startRotation3D = true;
            main.startSize3D = true;
            var size = chips.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.82f, 1f), new Keyframe(1f, 0f)));
            var spin = chips.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-9f, 9f);
            spin.y = new ParticleSystem.MinMaxCurve(-9f, 9f);
            spin.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
            var collision = chips.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.enableDynamicColliders = false;
            collision.bounce = new ParticleSystem.MinMaxCurve(.2f, .38f);
            collision.dampen = new ParticleSystem.MinMaxCurve(.35f, .6f);
            collision.radiusScale = .5f;
            collision.quality = ParticleSystemCollisionQuality.Medium;

            // Gold motes: tiny HDR points drifting up on a lazy turbulence (a frozen king, the freeze's shaft, the sweep).
            motes = Particles("H gold motes", dot, true, ParticleSystemRenderMode.Billboard, 260, 2.6f);
            Fade(motes, 1f, 0f, .25f, .55f);
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = .18f;
            noise.frequency = .7f;
            noise.scrollSpeed = .35f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // Glints: a star that pops and is gone (light catching on gold or a lit dot).
            glints = Particles("H glints", star, true, ParticleSystemRenderMode.Billboard, 80, 4f);
            Fade(glints, 1f, 0f, .02f, .25f);
            var pop = glints.sizeOverLifetime;
            pop.enabled = true;
            pop.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .2f), new Keyframe(.18f, 1f), new Keyframe(1f, 0f)));
            var turn = glints.rotationOverLifetime;
            turn.enabled = true;
            turn.z = new ParticleSystem.MinMaxCurve(-.8f, .8f);

            // Low marble dust that rolls out over the board (alpha-blended, slows down, rises a little).
            haze = Particles("H floor dust", puff, false, ParticleSystemRenderMode.Billboard, 160);
            Fade(haze, .55f, 0f, .08f, .25f);
            Shrink(haze, .6f, 1.8f);
            var hm = haze.main;
            hm.gravityModifier = -.02f;
            var drag = haze.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 0f;
            drag.dampen = .08f;
        }

        // ------------------------------------------------------------------ moments (MgCheckmate calls these)

        // The check dot that just lit: a red glint on it.
        public void DotLit(Vector3 dotAt) => Emit(glints, dotAt, Vector3.zero, .28f, .32f, Glow(Red, 2.5f), Random.Range(0f, 90f));

        // A frozen king, every frame (on: frozen). Its own gold light from below, motes drifting up, a glint now and
        // then; off, the light goes out in a moment.
        public void KingGlow(int k, Vector3 foot, bool on)
        {
            if (k < 0 || k >= kingLights.Length) return;
            var l = kingLights[k];
            if (!on)
            {
                if (l != null && l.enabled)
                {
                    l.intensity = Mathf.MoveTowards(l.intensity, 0f, Time.deltaTime * 8f);
                    if (l.intensity <= 0f) l.enabled = false;
                }
                return;
            }
            if (l == null)
            {
                var go = new GameObject("H frozen king light " + (k + 1));
                go.transform.SetParent(transform, false);
                l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = Warm;
                l.range = 2.4f;
                l.shadows = LightShadows.None;
                l.intensity = 0f;
                kingLights[k] = l;
            }
            float t = Time.time;
            l.enabled = true;
            l.transform.position = foot + new Vector3(0f, .45f, 0f);
            // breathing with a little flame-like flicker
            float want = 1.25f + .35f * Mathf.Sin(t * 5f + k) + .12f * (Mathf.PerlinNoise(t * 7f, k * 3.1f) - .5f);
            l.intensity = Mathf.MoveTowards(l.intensity, want, Time.deltaTime * 10f);
            if (t >= nextMoteAt[k])
            {
                nextMoteAt[k] = t + Random.Range(.08f, .16f);
                var p = Random.insideUnitCircle * .42f;
                Emit(motes, foot + new Vector3(p.x, Random.Range(.05f, .5f), p.y), new Vector3(0f, Random.Range(.25f, .55f), 0f),
                     Random.Range(1f, 1.6f), Random.Range(.035f, .07f), Glow(Gold, 2.6f));
            }
            if (t >= nextGlintAt[k])
            {
                nextGlintAt[k] = t + Random.Range(.45f, .9f);
                var p = Random.insideUnitCircle * .16f;
                Emit(glints, foot + new Vector3(p.x, Random.Range(.35f, MgCheckmate.KingHeight + .15f), p.y), Vector3.zero,
                     Random.Range(.25f, .35f), Random.Range(.22f, .34f), Glow(Gold, 3f), Random.Range(0f, 90f));
            }
        }

        // The last king in the tray (the room's whole job done): gold glints and motes rise all along the tray under a
        // warm light that swells and fades.
        public void Sweep(Vector3 a, Vector3 b, Color team)
        {
            var mid = (a + b) * .5f;
            FlashLight(mid + Vector3.up * 1.2f, Warm, 4f, .9f, 6f);
            for (int i = 0; i < 26; i++)
            {
                var p = Vector3.Lerp(a, b, Random.value) + new Vector3(Random.Range(-.3f, .3f), Random.Range(.1f, .5f), 0f);
                Emit(motes, p, new Vector3(Random.Range(-.1f, .1f), Random.Range(.8f, 1.8f), Random.Range(-.1f, .1f)),
                     Random.Range(1.2f, 2f), Random.Range(.05f, .09f), Glow(i % 4 == 0 ? team : Gold, 2.8f));
            }
            for (int i = 0; i < 10; i++)
                Emit(glints, Vector3.Lerp(a, b, (i + Random.value) / 10f) + Vector3.up * Random.Range(.3f, 1.2f), Vector3.up * .3f,
                     Random.Range(.35f, .5f), Random.Range(.35f, .55f), Glow(Gold, 3f), Random.Range(0f, 90f));
        }

        // ------------------------------------------------------------------ parts

        // Marble chips burst from `at`, out and up, falling back to bounce on the board.
        void Chips(Vector3 at, int n, float speed, float up)
        {
            for (int i = 0; i < n; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                e.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
                e.startSize3D = new Vector3(Random.Range(.6f, 1.3f), Random.Range(.5f, 1f), Random.Range(.6f, 1.2f)) * Random.Range(.045f, .09f);
                e.position = at + new Vector3(dir.x, 0f, dir.y) * .15f;
                e.velocity = new Vector3(dir.x, 0f, dir.y) * Random.Range(.4f, 1f) * speed + Vector3.up * Random.Range(.6f, 1f) * up;
                e.startLifetime = Random.Range(1.1f, 1.7f);
                e.startColor = Color.white;
                chips.Emit(e, 1);
            }
            // back to the plain emit's defaults (Emit sets size and rotation itself)
            e.ResetStartSize();
            e.ResetRotation();
        }

        // Low dust rolling out over the board from `at`.
        void FloorDust(Vector3 at, int n, float speed, Color color)
        {
            for (int i = 0; i < n; i++)
            {
                var dir = Random.insideUnitCircle;
                Emit(haze, at + new Vector3(dir.x * .3f, .1f, dir.y * .3f), new Vector3(dir.x, .08f, dir.y) * speed * Random.Range(.6f, 1.2f),
                     Random.Range(.8f, 1.3f), Random.Range(.35f, .6f), color, Random.Range(0f, 360f));
            }
        }

        // A short-lived point light: bright at once, out in `seconds` (ease-out).
        void FlashLight(Vector3 at, Color color, float intensity, float seconds, float range)
        {
            var go = new GameObject("H flash light");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.shadows = LightShadows.None;
            l.intensity = intensity;
            flashes.Add(new Flash { light = l, born = Time.time, life = Mathf.Max(.02f, seconds), peak = intensity });
        }

        void Update()
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                float k = (Time.time - f.born) / f.life;
                if (f.light == null || k >= 1f)
                {
                    if (f.light != null) Destroy(f.light.gameObject);
                    flashes.RemoveAt(i);
                    continue;
                }
                f.light.intensity = f.peak * (1f - k) * (1f - k);
            }
        }

        void OnDestroy()
        {
            if (chipMaterial != null) Destroy(chipMaterial);
        }

        // The attacked-square plate's material (MgCheckmate): light added onto the board, bright along the square's seams,
        // softer inside; MgCheckmate tints and flickers it per square. Null without the particle shader.
        public static Material FloorLight()
        {
            if (floorLight != null) return floorLight;
            RealTextures();
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null) return null;
            floorLight = new Material(shader) { mainTexture = floorTex, name = "H attacked square light" };
            LitMaterials.MakeTransparent(floorLight, true);
            return floorLight;
        }

        // Seam light: d is the distance from the centre in the square's own measure (edge at FloorEdge).
        static float SeamLight(float x, float y)
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y), d = Mathf.Max(ax, ay);
            float seam = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(d - FrameEdge) / .07f), 2f);
            float corner = Mathf.Clamp01(Mathf.Min(ax, ay) / FrameEdge);
            seam *= .8f + .5f * corner * corner;
            float inside = Mathf.Clamp01((FrameEdge - d) / .05f);
            float fill = inside * (.16f + .22f * Mathf.Pow(d / FrameEdge, 3f));
            return Mathf.Clamp01(Mathf.Max(seam, fill) + .25f * seam * fill);
        }

        static void RealTextures()
        {
            if (floorTex != null) return;
            floorTex = Draw(128, SeamLight);
            chunk = Chunk();
        }

        // A small irregular stone chunk (an octahedron with its corners pulled about), flat-shaded.
        static Mesh Chunk()
        {
            var state = Random.state;
            Random.InitState(688);
            var corners = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int i = 0; i < corners.Length; i++) corners[i] = corners[i] * Random.Range(.35f, .6f) + Random.insideUnitSphere * .12f;
            Random.state = state;
            int[] faces = { 0, 2, 4, 4, 2, 1, 1, 2, 5, 5, 2, 0, 4, 3, 0, 1, 3, 4, 5, 3, 1, 0, 3, 5 };
            var v = new Vector3[faces.Length];
            var t = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { v[i] = corners[faces[i]]; t[i] = i; }
            var m = new Mesh { name = "H marble chip", vertices = v, triangles = t };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
