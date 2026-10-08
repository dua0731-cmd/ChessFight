using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The parts the skill effects are built from (R79): everything is made in code at start, no art files. One
    /// material (<c>ChessFight/Skill Glow</c>, HDR light that the camera's bloom spreads) on a few meshes (an open
    /// cylinder for walls and beams, a sphere for shells of light, a square, a box of four walls) and on three
    /// particle systems (sparks that bounce off the floor and walls, motes of light, smoke), plus point lights that
    /// light the floor and the pieces, trails and lightning. Textures are drawn by code too: a soft dot, smoke,
    /// noise, a beam that fades upward, a ring, a lit square. The cartoon parts of R86 (puffs, cracks, swooshes,
    /// speed lines) are in PawnRushSkillFx.Toon.cs.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        // ---------------------------------------------------------------- textures and meshes

        Texture2D texDot, texSoft, texSmoke, texNoise, texBeam, texRing, texTile, texBand;
        Mesh meshQuad, meshCylinder, meshSphere, meshBox;
        Shader glowShader;

        void BuildParts()
        {
            glowShader = Shader.Find("ChessFight/Skill Glow");
            if (glowShader == null) glowShader = Resources.Load<Shader>("PawnRushSkillFx/SkillGlow");
            texNoise = Tex(128, 128, TextureWrapMode.Repeat, (u, v) => Grey(Fbm(u, v, 4, 4)));
            texDot = Tex(64, 64, TextureWrapMode.Clamp, (u, v) =>
            {
                float r = Radius(u, v);
                return White(Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(1f - r), 2.4f) * 0.75f + Mathf.Exp(-r * r * 60f) * 0.6f));
            });
            texSoft = Tex(64, 64, TextureWrapMode.Clamp, (u, v) => White(Mathf.Pow(Mathf.Clamp01(1f - Radius(u, v)), 2f)));
            texSmoke = Tex(64, 64, TextureWrapMode.Clamp, (u, v) =>
            {
                float r = Radius(u, v);
                float blob = Mathf.Clamp01(1f - r);
                return White(Mathf.Clamp01(blob * blob * (0.45f + 0.9f * Fbm(u * 2f, v * 2f, 3, 3))));
            });
            texBeam = Tex(16, 128, TextureWrapMode.Clamp, (u, v) =>
                White(Mathf.Clamp01(Mathf.Pow(1f - v, 1.7f) * Mathf.SmoothStep(0f, 1f, v * 14f) + Mathf.Exp(-v * 22f) * 0.5f)));
            texRing = Tex(128, 128, TextureWrapMode.Clamp, (u, v) =>
            {
                float r = Radius(u, v);
                return White(Mathf.Clamp01(Mathf.Exp(-Sq((r - 0.86f) / 0.045f)) + 0.35f * Mathf.Exp(-Sq((r - 0.8f) / 0.16f))));
            });
            texBand = Tex(64, 32, TextureWrapMode.Clamp, (u, v) =>
                White(Mathf.Exp(-Sq((v - 0.5f) / 0.2f)) * Mathf.Pow(Mathf.Clamp01(1f - u), 0.6f)));
            texTile = Tex(128, 128, TextureWrapMode.Clamp, (u, v) =>
            {
                float d = Mathf.Min(Mathf.Min(u, v), Mathf.Min(1f - u, 1f - v));
                float corner = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) > 0.38f && Mathf.Min(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) > 0.3f ? 0.5f : 0f;
                return White(Mathf.Clamp01(Mathf.Exp(-d / 0.03f) * 1.1f + corner * Mathf.Exp(-d / 0.08f) + 0.2f));
            });

            meshQuad = Quad();
            meshCylinder = Cylinder(48);
            meshBox = OpenBox();
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meshSphere = Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            Destroy(tmp);

            // Sparks: white-hot (above the bloom threshold) with a coloured glow. Motes: coloured, covering the floor
            // behind them so the colour shows on a light floor. Smoke: paint.
            sparks = new Emitter(root, "Sparks", Glow(texDot, opacity: 0.5f, color: 2.4f), stretch: true, gravity: 1.4f, drag: 0.6f, collide: true, noise: 0f,
                fade: new[] { 0f, 1f, 0.7f, 1f, 1f, 0f }, size: new[] { 0f, 1f, 1f, 0.3f });
            motes = new Emitter(root, "Motes", Glow(texDot, opacity: 0.9f, color: 1.15f), stretch: false, gravity: -0.05f, drag: 1.2f, collide: false, noise: 0.6f,
                fade: new[] { 0f, 0f, 0.15f, 1f, 1f, 0f }, size: new[] { 0f, 0.6f, 0.2f, 1f, 1f, 0.2f });
            smoke = new Emitter(root, "Smoke", Glow(texSmoke, opacity: 0.55f, soft: 0.4f), stretch: false, gravity: -0.03f, drag: 2.5f, collide: false, noise: 0.3f,
                fade: new[] { 0f, 0f, 0.12f, 1f, 1f, 0f }, size: new[] { 0f, 0.5f, 1f, 1.6f });
            smoke.ps.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.Distance;
        }

        void DestroyParts()
        {
            foreach (var m in materials) if (m != null) Destroy(m);
            foreach (var t in new Object[] { texDot, texSoft, texSmoke, texNoise, texBeam, texRing, texTile, texBand, meshQuad, meshCylinder, meshSphere, meshBox })
                if (t != null) Destroy(t);
            DestroyToon();
        }

        static float Sq(float x) => x * x;
        static float Radius(float u, float v) => Mathf.Sqrt(Sq(u - 0.5f) + Sq(v - 0.5f)) * 2f;
        static Color32 White(float a) => new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
        static Color32 Grey(float g) { byte b = (byte)(Mathf.Clamp01(g) * 255f); return new Color32(b, b, b, 255); }

        static Texture2D Tex(int w, int h, TextureWrapMode wrap, Func<float, float, Color32> f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = wrap, filterMode = FilterMode.Bilinear, anisoLevel = 4, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        /// <summary>Tileable value noise, octaves of <paramref name="cells"/> × 2ⁿ cells over the square.</summary>
        static float Fbm(float u, float v, int cells, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++, cells *= 2, amp *= 0.5f)
            {
                float x = u * cells, y = v * cells;
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);
                float a = Hash(x0, y0, cells), b = Hash(x0 + 1, y0, cells), c = Hash(x0, y0 + 1, cells), d = Hash(x0 + 1, y0 + 1, cells);
                sum += amp * Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
                norm += amp;
            }
            return sum / norm;
        }

        static float Hash(int x, int y, int period)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            uint h = (uint)(x * 374761393 + y * 668265263 + period * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }

        static Vector2 V(float radius, float height) => new Vector2(radius, height);

        static Mesh Quad()
        {
            var m = new Mesh
            {
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) },
                normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                hideFlags = HideFlags.HideAndDontSave,
            };
            return m;
        }

        /// <summary>An open cylinder of radius 1 from y 0 to 1: u around, v up, normals outward.</summary>
        static Mesh Cylinder(int segments)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(dir); v.Add(dir + Vector3.up);
                n.Add(dir); n.Add(dir);
                uv.Add(new Vector2((float)i / segments, 0f)); uv.Add(new Vector2((float)i / segments, 1f));
                if (i < segments)
                {
                    int b = i * 2;
                    tri.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
                }
            }
            var m = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            var c = new Color[v.Count];
            for (int i = 0; i < c.Length; i++) c[i] = Color.white;
            m.colors = c;
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The four walls of a 1 × 1 square, from y 0 to 1 (a box of light without lid or floor).</summary>
        static Mesh OpenBox()
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            var corners = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], b = corners[(i + 1) % 4];
                Vector3 normal = Vector3.Cross(b - a, Vector3.up).normalized * -1f;
                int s = v.Count;
                v.Add(a); v.Add(b); v.Add(b + Vector3.up); v.Add(a + Vector3.up);
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(0f, 1f));
                tri.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
            var m = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            var c = new Color[v.Count];
            for (int i = 0; i < c.Length; i++) c[i] = Color.white;
            m.colors = c;
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        // ---------------------------------------------------------------- materials

        readonly List<Material> materials = new List<Material>();

        /// <summary>The glow material on <paramref name="shape"/>. Opacity 0 = light added on top.</summary>
        Material Glow(Texture shape, float opacity = 0f, float noise = 0f, Vector4 noiseST = default, float rim = 0f, float rimPower = 2f, float soft = 0f, float color = 1f)
        {
            var m = new Material(glowShader != null ? glowShader : Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            m.mainTexture = shape;
            m.SetColor("_Color", new Color(color, color, color, 1f));
            m.SetFloat("_Opacity", opacity);
            m.SetTexture("_NoiseTex", texNoise);
            m.SetVector("_NoiseST", noiseST == default ? new Vector4(1f, 1f, 0f, 0f) : noiseST);
            m.SetFloat("_NoiseAmount", noise);
            m.SetFloat("_Rim", rim);
            m.SetFloat("_RimPower", rimPower);
            m.SetFloat("_Soft", soft);
            materials.Add(m);
            return m;
        }

        /// <summary>Lets a camera show the effects at their best: depth for soft edges, HDR and bloom.</summary>
        public static void PrepareCamera(Camera cam)
        {
            if (cam == null) return;
            cam.depthTextureMode |= DepthTextureMode.Depth;
            cam.allowHDR = true;
            if (cam.GetComponent<PawnRushSkillBloom>() == null) cam.gameObject.AddComponent<PawnRushSkillBloom>();
        }

        // ---------------------------------------------------------------- a mesh of light

        /// <summary>A mesh with the glow material, placed and coloured each frame by <see cref="animate"/> (given its
        /// age as a share of <see cref="life"/>).</summary>
        class Shape : Anim
        {
            readonly Transform tf;
            readonly MeshRenderer mr;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            public float life = 1f;
            public Vector3 at, scale = Vector3.one;
            public Quaternion rotation = Quaternion.identity;
            public Color color = Color.white;
            public float bright = 1f, dissolve;
            /// <summary>How much it hides what is behind (the material's own when below 0). Bright floors need
            /// it: light added to white is just white, colour shows only where the floor is covered.</summary>
            public float paint = -1f;
            public bool billboard;
            public Action<Shape, float> animate;
            public float Age => age;

            public Shape(Transform parent, string name, Mesh mesh, Material material)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                tf = go.transform;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (age >= life) return false;
                animate?.Invoke(this, age / life);
                bool show = bright > 0.001f && scale.sqrMagnitude > 1e-8f;
                mr.enabled = show;
                if (!show) return true;
                tf.SetPositionAndRotation(at, billboard && cam != null ? cam.transform.rotation * rotation : rotation);
                tf.localScale = scale;
                block.SetColor("_Color", color * bright);
                block.SetFloat("_Dissolve", dissolve);
                if (paint >= 0f) block.SetFloat("_Opacity", paint);
                mr.SetPropertyBlock(block);
                return true;
            }

            public override void Destroy() { if (tf != null) Object.Destroy(tf.gameObject); }
        }

        /// <summary>Something that runs while <see cref="step"/> says so (given its age and the frame's time).</summary>
        class Ongoing : Anim
        {
            readonly Func<float, float, bool> step;
            readonly Action end;

            public Ongoing(Func<float, float, bool> step, Action end = null)
            {
                this.step = step;
                this.end = end;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                return step(age, dt);
            }

            public override void Destroy() => end?.Invoke();
        }

        Shape Add(Shape s)
        {
            anims.Add(s);
            return s;
        }

        // ---------------------------------------------------------------- particles

        Emitter sparks, motes, smoke;

        /// <summary>
        /// A particle system the effects emit into by hand (position, speed, size, life and tint each), stepped by
        /// the effects' own clock: real time, so it moves through a hit stop, and one recorded frame at a time while
        /// filming. The tint is 8-bit; the brightness above white comes from the material colour.
        /// </summary>
        class Emitter
        {
            public readonly ParticleSystem ps;
            readonly ParticleSystemRenderer renderer;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();

            public Emitter(Transform parent, string name, Material material, bool stretch, float gravity, float drag, bool collide, float noise, float[] fade, float[] size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 1f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 3000;
                main.gravityModifier = gravity;
                main.startSpeed = 0f;
                var emission = ps.emission;
                emission.enabled = false;
                var shape = ps.shape;
                shape.enabled = false;
                if (drag > 0f)
                {
                    var limit = ps.limitVelocityOverLifetime;
                    limit.enabled = true;
                    limit.limit = 1000f;
                    limit.drag = drag;
                    limit.multiplyDragByParticleSize = false;
                    limit.multiplyDragByParticleVelocity = false;
                }
                var colour = ps.colorOverLifetime;
                colour.enabled = true;
                var g = new Gradient();
                var keys = new GradientAlphaKey[fade.Length / 2];
                for (int i = 0; i < keys.Length; i++) keys[i] = new GradientAlphaKey(fade[i * 2 + 1], fade[i * 2]);
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, keys);
                colour.color = g;
                var grow = ps.sizeOverLifetime;
                grow.enabled = true;
                var curve = new AnimationCurve();
                for (int i = 0; i < size.Length / 2; i++) curve.AddKey(size[i * 2], size[i * 2 + 1]);
                grow.size = new ParticleSystem.MinMaxCurve(1f, curve);
                if (noise > 0f)
                {
                    var n = ps.noise;
                    n.enabled = true;
                    n.strength = noise;
                    n.frequency = 0.8f;
                    n.scrollSpeed = 0.5f;
                    n.quality = ParticleSystemNoiseQuality.Medium;
                }
                if (collide)
                {
                    var c = ps.collision;
                    c.enabled = true;
                    c.type = ParticleSystemCollisionType.World;
                    c.mode = ParticleSystemCollisionMode.Collision3D;
                    c.bounce = 0.35f;
                    c.dampen = 0.35f;
                    c.lifetimeLoss = 0.2f;
                    c.radiusScale = 0.4f;
                    c.quality = ParticleSystemCollisionQuality.Medium;
                    c.enableDynamicColliders = false;
                }
                renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
                renderer.velocityScale = stretch ? 0.035f : 0f;
                renderer.lengthScale = stretch ? 1.6f : 1f;
                renderer.maxParticleSize = 3f;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            public void Emit(Vector3 position, Vector3 velocity, float size, float life, Color32 tint, float spin = 0f)
            {
                emit.position = position;
                emit.velocity = velocity;
                emit.startSize = size;
                emit.startLifetime = life;
                emit.startColor = tint;
                emit.rotation = Random.Range(0f, 360f);
                emit.angularVelocity = spin;
                ps.Emit(emit, 1);
            }

            public void Step(float dt) { if (dt > 0f) ps.Simulate(dt, true, false, false); }
        }

        static Vector3 OnSphere(float upBias = 0f)
        {
            var d = Random.onUnitSphere;
            d.y = Mathf.Abs(d.y) * (1f - upBias) + upBias;
            return d.normalized;
        }

        /// <summary>A direction within <paramref name="degrees"/> of <paramref name="axis"/>.</summary>
        static Vector3 InCone(Vector3 axis, float degrees)
        {
            var r = Quaternion.LookRotation(axis.sqrMagnitude > 1e-6f ? axis : Vector3.up);
            float a = Random.Range(0f, Mathf.PI * 2f), t = Mathf.Tan(Random.Range(0f, degrees) * Mathf.Deg2Rad);
            return (r * new Vector3(Mathf.Cos(a) * t, Mathf.Sin(a) * t, 1f)).normalized;
        }

        static Color32 Tint(Color c, float whiten = 0f)
        {
            c = Color.Lerp(c, Color.white, whiten);
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b, 1e-4f));
            return new Color32((byte)(c.r / m * 255f), (byte)(c.g / m * 255f), (byte)(c.b / m * 255f), 255);
        }

        /// <summary>Sparks out of <paramref name="at"/>: streaks that fall and bounce off the floor and walls.</summary>
        void SparkBurst(Vector3 at, int count, Color color, Vector3 axis, float cone, float speedMin, float speedMax, float life = 0.55f, float size = 0.06f)
        {
            for (int i = 0; i < count; i++)
            {
                var dir = cone >= 180f ? OnSphere(0.15f) : InCone(axis, cone);
                sparks.Emit(at, dir * Random.Range(speedMin, speedMax), size * Random.Range(0.7f, 1.3f), life * Random.Range(0.6f, 1.2f), Tint(color, Random.value * 0.5f));
            }
        }

        /// <summary>Motes of light drifting from <paramref name="at"/>.</summary>
        void MoteBurst(Vector3 at, int count, Color color, float spread, float up, float speed, float life = 1.1f, float size = 0.09f)
        {
            for (int i = 0; i < count; i++)
            {
                var off = Random.insideUnitSphere * spread;
                off.y = Mathf.Abs(off.y) * 0.4f;
                var vel = (off.normalized * speed + Vector3.up * up) * Random.Range(0.5f, 1.2f);
                motes.Emit(at + off, vel, size * Random.Range(0.6f, 1.4f), life * Random.Range(0.6f, 1.2f), Tint(color, Random.value * 0.4f));
            }
        }

        /// <summary>A ring of smoke rolling out along the floor from <paramref name="at"/>.</summary>
        void DustRing(Vector3 at, int count, float radius, float speed, Color color, float size = 0.6f, float life = 0.9f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.6f) * Mathf.PI * 2f / count;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var c = Color.Lerp(color, Color.white, Random.value * 0.2f);
                smoke.Emit(at + dir * radius + Vector3.up * 0.15f, dir * speed * Random.Range(0.7f, 1.2f) + Vector3.up * Random.Range(0.2f, 0.6f),
                    size * Random.Range(0.7f, 1.3f), life * Random.Range(0.7f, 1.2f), (Color32)c, Random.Range(-60f, 60f));
            }
        }

        // ---------------------------------------------------------------- light, trails, lightning

        /// <summary>A point light that flares up and dies away: it really lights the floor and the pieces.</summary>
        class LightFlash : Anim
        {
            readonly Light light;
            readonly float peak, rise, life;
            readonly Func<Vector3> follow;

            public LightFlash(Transform parent, Vector3 at, Color color, float peak, float range, float life, float rise = 0.03f, Func<Vector3> follow = null)
            {
                var go = new GameObject("Effect light");
                go.transform.SetParent(parent, false);
                go.transform.position = at;
                light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.range = range;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
                this.peak = peak;
                this.rise = rise;
                this.life = life;
                this.follow = follow;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (age >= life) return false;
                float k = age < rise ? age / rise : 1f - (age - rise) / Mathf.Max(0.01f, life - rise);
                light.intensity = peak * k * k;
                if (follow != null) light.transform.position = follow();
                return true;
            }

            public override void Destroy() { if (light != null) Object.Destroy(light.gameObject); }
        }

        void Flare(Vector3 at, Color color, float peak, float range, float life, Func<Vector3> follow = null)
        {
            anims.Add(new LightFlash(root, at, color, peak, range, life, 0.03f, follow));
        }

        /// <summary>A ribbon of light behind a body part while <see cref="Active"/> holds; it fades after.</summary>
        class Streak : Anim
        {
            readonly TrailRenderer trail;
            readonly Func<Vector3?> where;
            readonly Color color;
            float endedAt = -1f;
            bool started;
            /// <summary>Seconds before it starts following (the daze orbs come in late).</summary>
            public float delay;
            /// <summary>How much it covers what is behind (see Shape.paint).</summary>
            public float paint = -1f;

            public Streak(Transform parent, Material material, Color color, float width, float time, Func<Vector3?> where)
            {
                var go = new GameObject("Effect trail");
                go.transform.SetParent(parent, false);
                trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = material;
                trail.time = time;
                trail.minVertexDistance = 0.04f;
                trail.widthMultiplier = width;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                trail.numCapVertices = 4;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.textureMode = LineTextureMode.Stretch;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = g;
                this.color = color;
                this.where = where;
                trail.emitting = false;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (age < delay) return true;
                var p = endedAt < 0f ? where() : null;
                if (p.HasValue)
                {
                    trail.transform.position = p.Value;
                    if (!started)
                    {
                        // The first point where it is, not where the object was made.
                        trail.Clear();
                        started = true;
                        var block = new MaterialPropertyBlock();
                        block.SetColor("_Color", color);
                        if (paint >= 0f) block.SetFloat("_Opacity", paint);
                        trail.SetPropertyBlock(block);
                    }
                    trail.emitting = true;
                    return true;
                }
                if (endedAt < 0f) endedAt = age;
                trail.emitting = false;
                return age - endedAt < trail.time + 0.05f;
            }

            public override void Destroy() { if (trail != null) Object.Destroy(trail.gameObject); }
        }

        /// <summary>Crackling bolts from a point outwards, redrawn every few hundredths of a second. Given a
        /// <see cref="plane"/> (its normal), they run along it only: over the floor or across a wall (R86, the rook).
        /// <see cref="toon"/>: drawn with the rook's cartoon lightning material, wider.</summary>
        class Bolts : Anim
        {
            readonly LineRenderer[] lines;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            readonly Func<Vector3> from;
            readonly Color color;
            readonly float reach, life;
            float redraw;
            public Vector3 plane;
            public bool toon;

            public Bolts(Transform parent, Material material, int count, Func<Vector3> from, Color color, float reach, float life)
            {
                this.from = from;
                this.color = color;
                this.reach = reach;
                this.life = life;
                lines = new LineRenderer[count];
                for (int i = 0; i < count; i++)
                {
                    var go = new GameObject("Effect bolt");
                    go.transform.SetParent(parent, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.sharedMaterial = material;
                    lr.positionCount = 7;
                    lr.useWorldSpace = true;
                    lr.numCapVertices = 2;
                    lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lr.receiveShadows = false;
                    lr.textureMode = LineTextureMode.Stretch;
                    lines[i] = lr;
                }
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (age >= life) return false;
                redraw -= dt;
                float fade = 1f - age / life;
                if (redraw <= 0f)
                {
                    redraw = 0.035f;
                    Vector3 o = from();
                    bool flat = plane.sqrMagnitude > 1e-6f;
                    Vector3 n = flat ? plane.normalized : Vector3.up;
                    foreach (var lr in lines)
                    {
                        Vector3 dir = flat ? Vector3.ProjectOnPlane(Random.onUnitSphere, n).normalized : OnSphere(-0.3f);
                        float len = reach * Random.Range(0.5f, 1f);
                        Vector3 side = flat ? Vector3.Cross(dir, n).normalized : Vector3.Cross(dir, Random.onUnitSphere).normalized;
                        for (int k = 0; k < lr.positionCount; k++)
                        {
                            float t = k / (float)(lr.positionCount - 1);
                            Vector3 jitter = Random.insideUnitSphere * 0.03f;
                            if (flat) jitter = Vector3.ProjectOnPlane(jitter, n);
                            Vector3 p = o + dir * len * t + side * Random.Range(-0.12f, 0.12f) * len * Mathf.Sin(t * Mathf.PI) + jitter;
                            lr.SetPosition(k, p);
                        }
                        lr.widthMultiplier = Random.Range(0.04f, 0.075f) * (0.4f + 0.6f * fade) * (toon ? 1.8f : 1f);
                    }
                }
                if (toon) block.SetFloat("_Fade", Mathf.Clamp01(fade * 2f));
                else
                {
                    block.SetColor("_Color", color * fade);
                    block.SetFloat("_Opacity", 0.3f);
                }
                foreach (var lr in lines) lr.SetPropertyBlock(block);
                return true;
            }

            public override void Destroy() { foreach (var lr in lines) if (lr != null) Object.Destroy(lr.gameObject); }
        }
    }
}
