using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R510 (H 국왕 체스 홀, "다음 작업" after the hall's art): H's own effects, soft particles in the hall's marble-and-
    // gold look instead of ArenaFx's primitives (the shared solid light column, cube bits and line rings, which the
    // user turned down in A: "이런 vfx 말고"). Same method as A's PaintBoardFx: code-drawn textures, URP particles.
    // Pictures only, played by every PC from its own view of the hunt (MgCheckmate.Present), nothing is sent.
    //   check     the king's square outlined in a red light frame that glides out, a few red sparks off the head
    //   freeze    a soft gold light shaft falls on the king from above (the hall's skylight shafts), a gold square frame
    //             and gold twinkles: "now it can be taken" (MINIGAME_ZONES §H)
    //   rescue    grey stone dust and chips burst off the king as it comes back to life
    //   capture   gold and team twinkles and cream flakes fly up from the square as the king tips over
    //   stalemate a soft blue-grey puff where the king vanishes and where it turns up
    //   hop       a little marble dust where a king lands
    public sealed class CheckmateFx : MonoBehaviour
    {
        static Texture2D dot, star, frameTex, shaftTex, flake, puff;
        ParticleSystem sparks, stars, frame, shaft, flakes, dust;
        ParticleSystem.EmitParams e;

        static readonly Color Gold = new Color(1f, .82f, .38f), Red = new Color(1f, .3f, .25f), Stone = new Color(.62f, .6f, .57f),
                              Marble = new Color(.9f, .86f, .78f), Smoke = new Color(.62f, .7f, .86f);

        void Awake()
        {
            Textures();
            sparks = Particles("H sparks", dot, true, ParticleSystemRenderMode.Stretch, 400, 2.4f);
            Fade(sparks, 1f, 0f);
            var s = sparks.main;
            s.gravityModifier = .6f;

            stars = Particles("H twinkles", star, true, ParticleSystemRenderMode.Billboard, 400, 3f);
            Fade(stars, 1f, 0f, .15f);
            var twinkle = stars.sizeOverLifetime;
            twinkle.enabled = true;
            twinkle.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(.2f, 1f), new Keyframe(.45f, .55f),
                new Keyframe(.7f, .9f), new Keyframe(1f, 0f)));
            var spin = stars.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

            frame = Particles("H square frame", frameTex, true, ParticleSystemRenderMode.HorizontalBillboard, 60, 1.8f);
            Fade(frame, 1f, 0f, .04f, .15f);
            Shrink(frame, FrameFrom, 1f, true);

            shaft = Particles("H light shaft", shaftTex, true, ParticleSystemRenderMode.VerticalBillboard, 20, 1.6f);
            Fade(shaft, 1f, 0f, .25f, .55f);

            flakes = Particles("H flakes", flake, false, ParticleSystemRenderMode.Billboard, 400);
            Fade(flakes, 1f, 0f, .02f, .7f);
            var f = flakes.main;
            f.gravityModifier = .5f;
            var tumble = flakes.rotationOverLifetime;
            tumble.enabled = true;
            tumble.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            dust = Particles("H dust", puff, false, ParticleSystemRenderMode.Billboard, 300);
            Fade(dust, .7f, 0f, .1f, .2f);
            Shrink(dust, .5f, 1.4f);
            var d = dust.main;
            d.gravityModifier = -.03f;
        }

        // ------------------------------------------------------------------ moments (at = the king's foot)

        public void Check(Vector3 at, float square, float yaw)
        {
            Frame(at + Vector3.up * .08f, square, yaw, .6f, Glow(Red, 2f));
            for (int i = 0; i < 8; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                Emit(sparks, at + Vector3.up * (MgCheckmate.KingHeight + .25f), new Vector3(dir.x, 0f, dir.y) * Random.Range(.8f, 1.6f) + Vector3.up * Random.Range(1f, 2f),
                     Random.Range(.35f, .55f), Random.Range(.05f, .08f), Glow(Red, 2.4f));
            }
        }

        public void Freeze(Vector3 at, float square, float yaw)
        {
            Emit(shaft, at + Vector3.up * 3.2f, Vector3.zero, 1.6f, 6.4f, Glow(Gold, 1.6f));
            Frame(at + Vector3.up * .08f, square, yaw, 1f, Glow(Gold, 2f));
            for (int i = 0; i < 12; i++)
            {
                var p = Random.insideUnitCircle * .5f;
                Emit(stars, at + new Vector3(p.x, Random.Range(.2f, MgCheckmate.KingHeight + .4f), p.y), Vector3.up * Random.Range(.1f, .5f),
                     Random.Range(.6f, 1f), Random.Range(.25f, .45f), Glow(Gold, 3f), Random.Range(0f, 360f));
            }
        }

        public void Rescue(Vector3 at, Color team)
        {
            for (int i = 0; i < 10; i++)
                Emit(dust, at + Vector3.up * Random.Range(.2f, .9f) + Random.insideUnitSphere * .25f, Random.insideUnitSphere * .6f + Vector3.up * .3f,
                     Random.Range(.7f, 1.1f), Random.Range(.4f, .7f), Stone);
            for (int i = 0; i < 12; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                Emit(flakes, at + Vector3.up * Random.Range(.3f, .9f), new Vector3(dir.x, 0f, dir.y) * Random.Range(1f, 2.4f) + Vector3.up * Random.Range(1.5f, 3f),
                     Random.Range(.6f, .9f), Random.Range(.08f, .14f), i % 4 == 0 ? team : Stone, Random.Range(0f, 360f));
            }
        }

        public void Capture(Vector3 at, float square, float yaw, Color team)
        {
            Frame(at + Vector3.up * .08f, square, yaw, .8f, Glow(Gold, 1.8f));
            for (int i = 0; i < 18; i++)
            {
                var dir = Random.insideUnitCircle * 1.2f;
                Emit(stars, at + Vector3.up * .4f, new Vector3(dir.x, Random.Range(2f, 4f), dir.y), Random.Range(.6f, 1f), Random.Range(.3f, .5f),
                     Glow(i % 3 == 0 ? team : Gold, 3f), Random.Range(0f, 360f));
            }
            for (int i = 0; i < 24; i++)
            {
                var dir = Random.insideUnitCircle;
                Emit(flakes, at + Vector3.up * .5f, new Vector3(dir.x, 0f, dir.y) * Random.Range(.8f, 2.2f) + Vector3.up * Random.Range(3f, 5f),
                     Random.Range(1.2f, 1.8f), Random.Range(.12f, .2f), i % 3 == 0 ? Gold : i % 3 == 1 ? team : Marble, Random.Range(0f, 360f));
            }
        }

        public void Vanish(Vector3 at)
        {
            for (int i = 0; i < 12; i++)
                Emit(dust, at + Vector3.up * Random.Range(.2f, 1f) + Random.insideUnitSphere * .3f, Random.insideUnitSphere * .5f + Vector3.up * .4f,
                     Random.Range(.8f, 1.2f), Random.Range(.5f, .8f), Smoke);
            for (int i = 0; i < 5; i++)
                Emit(stars, at + Vector3.up * Random.Range(.3f, 1.1f) + Random.insideUnitSphere * .3f, Vector3.up * .3f, .7f, Random.Range(.25f, .4f),
                     Glow(Smoke, 2.5f), Random.Range(0f, 360f));
        }

        public void Land(Vector3 at)
        {
            for (int i = 0; i < 4; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                Emit(dust, at + new Vector3(dir.x, .05f, dir.y) * .3f, new Vector3(dir.x, .15f, dir.y) * .5f, Random.Range(.4f, .6f), Random.Range(.25f, .4f), Marble);
            }
        }

        // ------------------------------------------------------------------ parts (as PaintBoardFx)

        const float FrameEdge = .82f, FrameFrom = 1f / 1.35f;

        void Frame(Vector3 at, float side, float yaw, float life, Color color) =>
            Emit(frame, at, Vector3.zero, life, side / FrameEdge / FrameFrom, color, yaw);

        void Emit(ParticleSystem ps, Vector3 at, Vector3 velocity, float life, float size, Color color, float rotation = 0f)
        {
            if (ps == null) return;
            e.position = at;
            e.velocity = velocity;
            e.startLifetime = life;
            e.startSize = size;
            e.startColor = color;
            e.rotation = rotation;
            ps.Emit(e, 1);
        }

        static Color Glow(Color c, float k) => new Color(Mathf.Clamp01(c.r * k * .4f), Mathf.Clamp01(c.g * k * .4f), Mathf.Clamp01(c.b * k * .4f), 1f);

        ParticleSystem Particles(string name, Texture2D texture, bool additive, ParticleSystemRenderMode mode, int max, float brightness = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader != null)
            {
                var m = new Material(shader) { mainTexture = texture, name = name };
                LitMaterials.MakeTransparent(m, additive);
                m.color = new Color(brightness, brightness, brightness, 1f);
                r.sharedMaterial = m;
            }
            else Debug.LogWarning("[미니게임 H] 파티클 셰이더가 없습니다: " + LitMaterials.ParticlesUnlitName);
            r.renderMode = mode;
            r.velocityScale = .08f;
            r.lengthScale = 1.2f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.View;
            ps.Play();
            return ps;
        }

        static void Fade(ParticleSystem ps, float peak, float end, float rise = .08f, float hold = .08f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, rise), new GradientAlphaKey(peak, Mathf.Max(rise, hold)),
                              new GradientAlphaKey(end, 1f) });
            col.color = g;
        }

        static void Shrink(ParticleSystem ps, float from, float to, bool quick = false)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = quick
                ? new AnimationCurve(new Keyframe(0f, from, 0f, 6f), new Keyframe(.35f, Mathf.Lerp(from, to, .85f)), new Keyframe(1f, to))
                : AnimationCurve.Linear(0f, from, 1f, to);
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        static void Textures()
        {
            if (dot != null) return;
            dot = Draw(64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 1.8f));
            star = Draw(96, (x, y) =>
            {
                float ax = Mathf.Abs(x), ay = Mathf.Abs(y), r = Mathf.Sqrt(x * x + y * y);
                float spikes = Mathf.Max(Mathf.Clamp01(1f - ay * 14f) * Mathf.Clamp01(1f - ax), Mathf.Clamp01(1f - ax * 14f) * Mathf.Clamp01(1f - ay));
                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 2.6f), 1.5f);
                return Mathf.Max(spikes * spikes, core);
            });
            frameTex = Draw(256, (x, y) =>
            {
                float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                return Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(d - FrameEdge) / .07f), 2f);
            });
            // a soft vertical shaft: a column a quarter of the quad wide, soft sides, bright at the top, gone at the foot
            shaftTex = Draw(128, (x, y) =>
            {
                float side = Mathf.Clamp01(1f - Mathf.Abs(x) / .26f);
                float v = (y + 1f) * .5f;
                return side * side * (3f - 2f * side) * Mathf.Pow(v, .8f) * Mathf.Clamp01(v * 8f);
            });
            flake = Draw(48, (x, y) =>
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x) - .3f), dy = Mathf.Max(0f, Mathf.Abs(y) - .2f);
                return Mathf.Clamp01((.3f - Mathf.Sqrt(dx * dx + dy * dy)) * 20f);
            });
            // a soft cloudy puff (dust, smoke)
            puff = Draw(96, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y), a = Mathf.Atan2(y, x);
                float edge = .78f + .08f * Mathf.Sin(a * 5f) + .05f * Mathf.Sin(a * 9f + 1f);
                return Mathf.Pow(Mathf.Clamp01(1f - r / edge), 1.4f);
            });
        }

        static Texture2D Draw(int n, System.Func<float, float, float> mask)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "H checkmate fx" };
            var pixels = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    byte b = (byte)(Mathf.Clamp01(mask((i + .5f) / n * 2f - 1f, (j + .5f) / n * 2f - 1f)) * 255f);
                    pixels[j * n + i] = new Color32(255, 255, 255, b);
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
