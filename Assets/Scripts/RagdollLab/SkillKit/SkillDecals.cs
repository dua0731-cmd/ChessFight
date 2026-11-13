using UnityEngine;
#if CF_URP
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
#endif

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Marks pressed into whatever is under them (R157, 승규 님 10-16 "다 적용 해줘" after the stage-2 mockups): a URP
    /// decal projector draws Resources/SkillDecals/&lt;name&gt;.png on the floor, a step or a slope as it is (a flat
    /// tile floated over it, as the hoof prints were, hangs in the air at an edge). It pops in and fades out on its own.
    /// The decal material (Resources/SkillDecals/Decal.mat, URP's decal shader) keeps the shader in a build; the PC
    /// renderer has the decal feature in screen-space mode, so it lands on our own shaders too. Without URP (the Linux
    /// compile check) <see cref="Stamp"/> returns false and the caller draws its old tile.
    /// </summary>
    public static class SkillDecals
    {
        /// <summary>
        /// Stamps decal <paramref name="name"/> at <paramref name="at"/> (a point on the floor), its top towards
        /// <paramref name="dir"/>, <paramref name="size"/> metres across, for <paramref name="life"/> seconds (full for
        /// 60%, then fading). False when there is no decal to stamp.
        /// </summary>
        public static bool Stamp(string name, Vector3 at, Vector3 dir, float size, float life)
        {
#if CF_URP
            var material = Material(name);
            if (material == null) return false;
            if (runner == null)
            {
                var host = new GameObject("Skill Decals") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(host);
                runner = host.AddComponent<Runner>();
            }
            Vector3 up = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.forward;
            var go = new GameObject("Decal " + name);
            // A shallow box round the floor point (10 cm under, 20 cm over), so feet standing on it stay clean.
            go.transform.SetPositionAndRotation(at + Vector3.up * .05f, Quaternion.LookRotation(Vector3.down, up));
            var projector = go.AddComponent<DecalProjector>();
            projector.material = material;
            projector.pivot = Vector3.zero;
            projector.size = new Vector3(.01f, .01f, .3f);
            projector.fadeFactor = 1f;
            runner.Add(projector, size, life);
            return true;
#else
            return false;
#endif
        }

#if CF_URP
        static Runner runner;
        static Material baseMaterial;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        static Material Material(string name)
        {
            if (materials.TryGetValue(name, out var m) && m != null) return m;
            if (baseMaterial == null) baseMaterial = Resources.Load<Material>("SkillDecals/Decal");
            var texture = Resources.Load<Texture2D>("SkillDecals/" + name);
            if (baseMaterial == null || texture == null) return null;
            m = new Material(baseMaterial) { name = "Skill decal " + name, hideFlags = HideFlags.HideAndDontSave };
            m.SetTexture("Base_Map", texture);
            return materials[name] = m;
        }

        sealed class Runner : MonoBehaviour
        {
            struct Mark
            {
                public DecalProjector projector;
                public float size, life, age;
            }

            readonly List<Mark> marks = new List<Mark>();

            public void Add(DecalProjector projector, float size, float life) =>
                marks.Add(new Mark { projector = projector, size = size, life = Mathf.Max(.05f, life) });

            void Update()
            {
                for (int i = marks.Count - 1; i >= 0; i--)
                {
                    var m = marks[i];
                    if (m.projector == null)
                    {
                        marks.RemoveAt(i);
                        continue;
                    }
                    m.age += Time.deltaTime;
                    float k = m.age / m.life;
                    if (k >= 1f)
                    {
                        Destroy(m.projector.gameObject);
                        marks.RemoveAt(i);
                        continue;
                    }
                    // Pops in a touch over size and settles (as the tiles' Pop), full for 60%, then fades.
                    float p = Mathf.Clamp01(m.age / .12f);
                    float pop = p < 1f ? 1f + .15f * Mathf.Sin(p * Mathf.PI) - (1f - p) * (1f - p) : 1f;
                    float s = m.size * Mathf.Max(.01f, pop);
                    m.projector.size = new Vector3(s, s, .3f);
                    m.projector.fadeFactor = k < .6f ? 1f : 1f - (k - .6f) / .4f;
                    marks[i] = m;
                }
            }
        }
#endif
    }
}
