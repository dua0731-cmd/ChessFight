using System.Collections.Generic;
using ChessFight.VFX;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx2: what the observatory's moving-part effects share (RoyalObservatoryVfxGears, RoyalObservatoryVfxOrbits,
    // RoyalObservatoryVfxPlanet) - a spray of sparks drawn with VFX_BurstParticles from its age alone, and a seeded
    // "does this moment spark" so that every PC picks the same rare moments from the shared clock.
    public static class RoyalObservatoryVfxSparks
    {
        public const string BurstShader = "ChessFight/VFX/Burst Particles";
        public static readonly int AgeId = Shader.PropertyToID("_Age"), LightId = Shader.PropertyToID("_Light");

        // The one material every spark and dust layer shares (its look is set per renderer).
        static Material shared;
        public static Material Material
        {
            get
            {
                if (shared == null)
                {
                    var shader = VfxShaderKeep.Find(BurstShader);
                    if (shader != null) shared = new Material(shader) { name = "RO moving-part sparks" };
                }
                return shared;
            }
        }

        // True for about `chance` of the slots, the same slots on every PC (an integer hash, no floating point).
        public static bool Fires(long slot, int seed, float chance)
        {
            unchecked
            {
                ulong h = (ulong)slot * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)seed * 0xC2B2AE3D27D4EB4FUL;
                h ^= h >> 31;
                h *= 0xBF58476D1CE4E5B9UL;
                h ^= h >> 29;
                return (h & 0xFFFFFF) < (ulong)(chance * 0x1000000);
            }
        }

        public static MeshRenderer Part(string name, Transform parent, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }

        // A spray out along the object's +z, `spread` degrees either way and up, at speeds `speed`, rising `up`; the
        // first particle (when `flash`) a still hot glint at the contact, gone in a tenth of a second. Its span, the
        // seconds the last of it lives, comes back in `span`.
        public static Mesh Spray(string name, int count, int seed, float spread, Vector2 speed, Vector2 up, Vector2 life, bool flash,
                                 out float span)
        {
            var rng = new System.Random(seed);
            float Next() => (float)rng.NextDouble();
            var points = new List<Vector3>();
            var corners = new List<Vector2>();
            var particles = new List<Vector4>();
            var velocities = new List<Vector3>();
            var triangles = new List<int>();
            span = 0f;
            for (int i = 0; i < count; i++)
            {
                bool glint = flash && i == 0;
                float yaw = Mathf.Lerp(-spread, spread, Next()), pitch = Mathf.Lerp(-spread * .4f, spread * .6f, Next());
                var dir = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                var velocity = glint ? Vector3.zero : dir * Mathf.Lerp(speed.x, speed.y, Next()) + Vector3.up * Mathf.Lerp(up.x, up.y, Next());
                var start = new Vector3(Mathf.Lerp(-.05f, .05f, Next()), Mathf.Lerp(-.05f, .05f, Next()), Mathf.Lerp(-.05f, .05f, Next()));
                float l = glint ? .12f : Mathf.Lerp(life.x, life.y, Next());
                float delay = glint ? 0f : Mathf.Lerp(0f, .06f, Next());
                span = Mathf.Max(span, delay + l);
                var particle = new Vector4(l, glint ? 6f : Mathf.Lerp(.7f, 1.3f, Next()), delay, 0f);
                int at = points.Count;
                for (int c = 0; c < 4; c++)
                {
                    points.Add(start);
                    corners.Add(new Vector2(c & 1, c >> 1));
                    particles.Add(particle);
                    velocities.Add(velocity);
                }
                triangles.AddRange(new[] { at, at + 2, at + 1, at + 2, at + 3, at + 1 });
            }
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, particles);
            mesh.SetUVs(2, velocities);
            mesh.SetTriangles(triangles, 0);
            float reach = speed.y * life.y + 1f;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (2f * reach + 2f));
            return mesh;
        }

        // Small hot sparks, stretched along their flight, falling.
        public static void SparkLook(MeshRenderer r, MaterialPropertyBlock block, float light)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_ColorStart", new Color(1f, .9f, .62f));
            block.SetColor("_ColorEnd", new Color(1f, .45f, .12f));
            block.SetFloat(LightId, light);
            block.SetFloat("_AlphaStart", 1f);
            block.SetFloat("_AlphaEnd", 0f);
            block.SetFloat("_FadeIn", .02f);
            block.SetFloat("_SizeStart", .045f);
            block.SetFloat("_SizeEnd", .02f);
            block.SetFloat("_Stretch", .03f);
            block.SetFloat("_Soft", .8f);
            block.SetFloat("_Hard", 0f);
            block.SetFloat("_Additive", 1f);
            block.SetFloat("_DepthFade", 0f);
            block.SetFloat("_Gravity", 9f);
            block.SetFloat("_Drag", 1.5f);
            block.SetFloat("_FloorY", -1e5f);   // the abyss under them
            block.SetFloat(AgeId, -1f);
            r.SetPropertyBlock(block);
        }

        public static void Seek(MeshRenderer r, MaterialPropertyBlock block, float age, float span)
        {
            bool on = age >= 0f && age < span;
            r.enabled = on;
            if (!on) return;
            r.GetPropertyBlock(block);
            block.SetFloat(AgeId, age);
            r.SetPropertyBlock(block);
        }

        // A level frame looking along `dir` (its gravity the world's).
        public static Quaternion Level(Vector3 dir)
        {
            dir = Vector3.ProjectOnPlane(dir, Vector3.up);
            return dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;
        }

        public static Vector3 Eye(Transform fallback) => Camera.main != null ? Camera.main.transform.position : fallback.position;
    }
}
