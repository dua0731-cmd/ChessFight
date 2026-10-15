using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The air a piece shoves back as it springs off (R141, 승규 님 10-14, for Pawn Rush and Queen of the Hill both: "f 키를
    /// 누르면 뒤에(엉덩이 부분)에 임펙트가 나가는데 다른 임펙트로 해줘(공기 임팩트)"), in place of what was drawn out of its back
    /// (the pawn's speed wedges, the Pawn Rush knight's force lines, the Queen of the Hill knight's wind ribbon): a burst of
    /// bent air (<c>ChessFight/Skill Air</c>) thrown back from behind its hips, stretching and slowing as it goes and gone in
    /// about a third of a second, and a few wisps of pale vapour torn back out of it (the mode's own real dust, in white).
    /// Nothing drawn and nothing round (승규 님: no rings, discs or bubbles; natural air and dust over drawn pictures).
    /// Each mode owns one, steps it in its own effect time (so a held probe holds it too) and calls <see cref="Kick"/>.
    /// </summary>
    public class SkillAir
    {
        class Burst
        {
            public GameObject go;
            public MeshRenderer r;
            public readonly MaterialPropertyBlock b = new MaterialPropertyBlock();
            public Vector3 from, way;
            public float age, life, reach, size, strength, floorY;
        }

        /// <summary>The vapour's lit and shaded white (a cool grey in its shade, so it shows on the cream floor).</summary>
        static readonly Color VapourLit = new Color(0.97f, 0.98f, 0.99f), VapourShade = new Color(0.62f, 0.66f, 0.72f);

        readonly Transform root;
        readonly Material mat;
        readonly Mesh ball;
        readonly List<Burst> live = new List<Burst>();
        readonly List<Burst> spare = new List<Burst>();

        public SkillAir(Transform parent)
        {
            var shader = Shader.Find("ChessFight/Skill Air");
            if (shader == null) shader = Resources.Load<Shader>("PawnRushSkillFx/SkillAir");
            if (shader == null) return;
            mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            // Unity's own ball (radius 0.5), as the shader expects.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball = probe.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(probe);   // at once: its collider and its white ball must not stay for the frame
            root = new GameObject("Skill air").transform;
            if (parent != null) root.SetParent(parent, false);
        }

        /// <summary>The shader was found (otherwise nothing is drawn).</summary>
        public bool Ready => mat != null;

        public int Count => live.Count;

        /// <summary>
        /// A burst of air thrown from <paramref name="from"/> (just behind the hips) along <paramref name="way"/> (back, and
        /// down for a leap off the floor); <paramref name="strength"/> about 1 for a spring, less for a step. With
        /// <paramref name="vapour"/>, a few wisps of white vapour torn back out of it.
        /// </summary>
        public void Kick(Vector3 from, Vector3 way, float strength = 1f, SkillDust vapour = null)
        {
            if (!Ready) return;
            if (way.sqrMagnitude < 1e-6f) way = Vector3.back;
            way.Normalize();
            float floor = SkillDust.FloorY(from + Vector3.up * 0.2f, 1.6f);
            var burst = spare.Count > 0 ? spare[spare.Count - 1] : null;
            if (burst != null) spare.RemoveAt(spare.Count - 1);
            else
            {
                burst = new Burst { go = new GameObject("Air burst") };
                burst.go.transform.SetParent(root, false);
                burst.go.AddComponent<MeshFilter>().sharedMesh = ball;
                burst.r = burst.go.AddComponent<MeshRenderer>();
                burst.r.sharedMaterial = mat;
                burst.r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                burst.r.receiveShadows = false;
            }
            burst.from = from;
            burst.way = way;
            burst.age = 0f;
            burst.strength = Mathf.Clamp(strength, 0.3f, 1.6f);
            burst.life = Mathf.Lerp(0.3f, 0.44f, Mathf.InverseLerp(0.3f, 1.6f, burst.strength));
            burst.reach = 0.45f + 0.75f * burst.strength;
            burst.size = Mathf.Lerp(0.85f, 1.3f, Mathf.InverseLerp(0.3f, 1.6f, burst.strength));
            burst.floorY = float.IsNegativeInfinity(floor) ? -1e4f : floor;
            burst.go.SetActive(true);
            live.Add(burst);
            Place(burst);

            if (vapour == null || !vapour.Ready) return;
            Vector3 side = Vector3.Cross(Vector3.up, way);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            Vector3 up = Vector3.Cross(way, side).normalized;
            int n = Mathf.Max(3, Mathf.RoundToInt(7f * burst.strength));
            for (int i = 0; i < n; i++)
            {
                // Torn back out of the burst in a narrow cone, fast, stopping at once and hanging a moment.
                Vector3 dir = (way + side * Random.Range(-0.5f, 0.5f) + up * Random.Range(-0.3f, 0.35f)).normalized;
                Vector3 at = from + side * Random.Range(-0.08f, 0.08f) + up * Random.Range(-0.06f, 0.08f);
                vapour.Emit(at, dir * Random.Range(3f, 5.4f) * burst.strength, Random.Range(0.09f, 0.15f) * burst.size, Random.Range(0.34f, 0.5f),
                            VapourLit, VapourShade, 2.1f, 0.12f, Random.Range(0.3f, 0.45f), burst.floorY, 7f);
            }
        }

        /// <summary>One frame of every burst (<paramref name="dt"/> in the mode's effect time; 0 holds them).</summary>
        public void Step(float dt)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var burst = live[i];
                burst.age += dt;
                if (burst.age >= burst.life)
                {
                    burst.go.SetActive(false);
                    live.RemoveAt(i);
                    spare.Add(burst);
                    continue;
                }
                Place(burst);
            }
        }

        static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        /// <summary>Where a burst is and how strong at its age: thrown back fast and slowing, stretching along its way as it
        /// goes, bending hardest in its first frames and fading out; the white haze over its thick part gone sooner.</summary>
        void Place(Burst b)
        {
            float k = Mathf.Clamp01(b.age / b.life), go = EaseOut(k);
            Vector3 at = b.from + b.way * (0.1f + b.reach * go);
            float wide = Mathf.Lerp(0.36f, 0.95f, go) * b.size, length = Mathf.Lerp(0.5f, 1.5f, go) * b.size;
            var t = b.go.transform;
            t.SetPositionAndRotation(at, Quaternion.LookRotation(b.way, Mathf.Abs(b.way.y) > 0.95f ? Vector3.forward : Vector3.up));
            t.localScale = new Vector3(wide, wide * 0.85f, length);
            float rise = Mathf.Clamp01(b.age / 0.03f), left = 1f - k;
            b.b.SetFloat("_Bend", 0.045f * b.strength * rise * Mathf.Pow(left, 1.4f));
            b.b.SetFloat("_Haze", 0.42f * Mathf.Min(1f, b.strength) * rise * Mathf.Pow(left, 1.5f));
            b.b.SetFloat("_Shade", 0.13f * left);
            b.b.SetFloat("_Streak", 3f);
            b.b.SetFloat("_Phase", b.age * 7f);
            b.b.SetFloat("_FloorY", b.floorY);
            b.b.SetFloat("_Fade", 1f);
            b.r.SetPropertyBlock(b.b);
        }

        public void Destroy()
        {
            foreach (var b in live) if (b.go != null) Object.Destroy(b.go);
            foreach (var b in spare) if (b.go != null) Object.Destroy(b.go);
            live.Clear();
            spare.Clear();
            if (root != null) Object.Destroy(root.gameObject);
            if (mat != null) Object.Destroy(mat);
        }
    }
}
