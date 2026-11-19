using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's solar flare (R721; it takes the place of the planets that crossed the globe's climb,
    // which the user found neither hard nor new). The floor inside the great globe is the plane of its solar system;
    // the sun lamp hangs over its middle and planets turn round the lamp between it and the floor. Every few seconds
    // the lamp flares:
    //   calm -> warn (the lamp swells, light spreads over the floor from its middle) -> burn
    // and whoever stands on the floor in the burn, outside a planet's shadow, is thrown down the slope. The shadows
    // are the only shelter, and they move: a planet's shadow is where the line from the lamp through the planet
    // meets the floor, so it goes round the floor as the planet goes round the lamp.
    //
    // This object lies on the floor: its origin the floor's middle, +y off the floor. When it flares is a pure
    // function of the shared obstacle clock, and so is where each planet is (ObstacleMotion), so every PC sees the
    // same light and the same shadows and nothing is sent. Who is thrown is the authority's to say
    // (ObstacleClock.Authority), as with every hazard.
    public sealed class SolarFlare : MonoBehaviour
    {
        [Tooltip("How far from this object's middle the flare reaches over the floor.")]
        [SerializeField] float radius = 13.6f;
        [Tooltip("The lamp, in this object's frame.")]
        [SerializeField] Vector3 sun = new Vector3(0f, 8f, 0f);
        [SerializeField] float calm = 3.2f, warn = 1.3f, burn = .5f;
        [Tooltip("Seconds added to the shared clock.")]
        [SerializeField] float phase;
        [SerializeField] Transform[] planets = new Transform[0];
        [Tooltip("Each planet's radius.")]
        [SerializeField] float[] sizes = new float[0];
        [Tooltip("Counted as shadow this far past a shadow's edge.")]
        [SerializeField] float grace = .15f;
        [Tooltip("Swells with the warning.")]
        [SerializeField] Transform lamp;

        [Header("Hits")]
        [Tooltip("Thrown down the floor's slope at this speed, and up at `lift`.")]
        [SerializeField] float push = 9f;
        [SerializeField] float lift = 4f, knockdown = 1.1f;

        static readonly Color Light = new Color(1f, .8f, .3f), Dark = new Color(.04f, .05f, .14f, .62f);

        Transform glow, wave;
        Transform[] shades = new Transform[0];
        Material glowMaterial, waveMaterial;
        Vector3 lampScale = Vector3.one;
        long struckFlare = long.MinValue;
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
        readonly Collider[] overlaps = new Collider[512];

        public float Cycle => calm + warn + burn;
        public float Radius => radius;
        public int PlanetCount => planets.Length;
        // How many pawns it has thrown (the authority's count).
        public int Burned { get; private set; }

        // Call it on an inactive object (the builder's root is switched on at the end).
        public void Configure(float radius, Vector3 sun, float calm, float warn, float burn, float phaseSeconds,
                              Transform[] planets, float[] sizes, Transform lamp)
        {
            this.radius = radius;
            this.sun = sun;
            this.calm = Mathf.Max(0f, calm);
            this.warn = Mathf.Max(0f, warn);
            this.burn = Mathf.Max(.05f, burn);
            phase = phaseSeconds;
            this.planets = planets ?? new Transform[0];
            this.sizes = sizes ?? new float[0];
            this.lamp = lamp;
            if (lamp != null) lampScale = lamp.localScale;

            glowMaterial = new Material(Arena.Glow(new Color(Light.r, Light.g, Light.b, 0f)));
            glow = Disc("Flare light", glowMaterial, .02f);
            waveMaterial = new Material(Arena.Glow(new Color(Light.r, Light.g, Light.b, 0f)));
            wave = Disc("Flare front", waveMaterial, .03f);
            shades = new Transform[this.planets.Length];
            var dark = Arena.Glow(Dark);
            for (int i = 0; i < shades.Length; i++) shades[i] = Disc("Planet shadow " + (i + 1), dark, .05f);
            Draw(ObstacleClock.Now);
        }

        float CycleTime(double time)
        {
            double cycle = Cycle;
            double t = (time + phase) % cycle;
            return (float)(t < 0 ? t + cycle : t);
        }

        // Which flare a time belongs to (the cycle's number).
        public long Index(double time) => (long)Math.Floor((time + phase) / Cycle);
        public bool Warning(double time)
        {
            float t = CycleTime(time);
            return t >= calm && t < calm + warn;
        }
        public bool Burning(double time) => CycleTime(time) >= calm + warn;

        // The shadow a planet throws on the floor now: its middle (x, z in this object's frame) and its radius.
        // False when the planet is not between the lamp and the floor.
        public bool Shade(int planet, out Vector2 middle, out float size)
        {
            middle = Vector2.zero;
            size = 0f;
            if (planet < 0 || planet >= planets.Length || planets[planet] == null) return false;
            var p = transform.InverseTransformPoint(planets[planet].position);
            if (p.y <= .05f || p.y >= sun.y - .05f) return false;
            // From the lamp through the planet on to the floor (y 0): the shadow is as much bigger than the planet
            // as the floor is farther from the lamp.
            float t = sun.y / (sun.y - p.y);
            var on = sun + (p - sun) * t;
            middle = new Vector2(on.x, on.z);
            size = (planet < sizes.Length ? sizes[planet] : 1f) * t;
            return true;
        }

        // Whether a point of the floor (this object's frame) is in some planet's shadow.
        public bool Sheltered(Vector3 at)
        {
            var flat = new Vector2(at.x, at.z);
            for (int i = 0; i < planets.Length; i++)
                if (Shade(i, out var middle, out float size) && (flat - middle).magnitude <= size + grace) return true;
            return false;
        }

        void Update() => Draw(ObstacleClock.Now);

        void Draw(double now)
        {
            float t = CycleTime(now);
            bool warning = t >= calm && t < calm + warn, burning = t >= calm + warn;
            float k = warning ? (t - calm) / Mathf.Max(.01f, warn) : burning ? 1f : 0f;
            if (glow != null)
            {
                glow.localScale = new Vector3(radius * 2f, .004f, radius * 2f);
                glow.gameObject.SetActive(warning || burning);
                glowMaterial.color = burning ? new Color(1f, .95f, .75f, .85f) : new Color(Light.r, Light.g, Light.b, .08f + .3f * k);
            }
            if (wave != null)
            {
                // The light spreads from under the lamp and reaches the floor's edge as the burn begins.
                wave.gameObject.SetActive(warning);
                wave.localScale = new Vector3(radius * 2f * k, .004f, radius * 2f * k);
                waveMaterial.color = new Color(Light.r, Light.g, Light.b, .22f + .25f * k);
            }
            if (lamp != null)
            {
                float swell = burning ? 1.55f : warning ? 1f + .35f * k + .05f * Mathf.Sin(t * 40f) : 1f;
                lamp.localScale = lampScale * swell;
            }
            for (int i = 0; i < shades.Length; i++)
            {
                if (shades[i] == null) continue;
                bool on = Shade(i, out var middle, out float size);
                if (shades[i].gameObject.activeSelf != on) shades[i].gameObject.SetActive(on);
                if (!on) continue;
                shades[i].localPosition = new Vector3(middle.x, .05f, middle.y);
                shades[i].localScale = new Vector3(size * 2f, .004f, size * 2f);
            }
        }

        void FixedUpdate()
        {
            double now = ObstacleClock.Now;
            if (!ObstacleClock.Authority || !Burning(now)) return;
            // Once a flare for each pawn: whoever is out of the shadows at any moment of the burn.
            long flare = Index(now);
            if (flare != struckFlare)
            {
                struckFlare = flare;
                struck.Clear();
            }
            int n = Physics.OverlapBoxNonAlloc(transform.TransformPoint(Vector3.up * 1.5f), new Vector3(radius, 2.5f, radius), overlaps,
                                               transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            var down = Vector3.ProjectOnPlane(Vector3.down, transform.up);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || struck.Contains(actor)) continue;
                var at = transform.InverseTransformPoint(actor.Position);
                var flat = new Vector3(at.x, 0f, at.z);
                if (flat.magnitude > radius || at.y < -1f || at.y > 4f) continue;
                if (Sheltered(at)) continue;
                // Down the slope; on a level floor, away from the lamp.
                var away = down.sqrMagnitude > 1e-4f ? down.normalized
                         : flat.sqrMagnitude > 1e-4f ? transform.TransformDirection(flat.normalized) : transform.forward;
                actor.AddObstacleHit(away * push + Vector3.up * lift, knockdown);
                struck.Add(actor);
                Burned++;
            }
        }

        Transform Disc(string name, Material material, float height)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * height;
            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void OnDisable() => struck.Clear();
    }
}
