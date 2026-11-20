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
    // R725 ro-vfx (user 11-20: the shadows drawn as the lamp's real shadows, "실제 빛 그림자", and "판정도 그림자에
    // 맞춤"): a point of the floor is sheltered when the line from it to the lamp's middle passes through a planet - each
    // planet's real shadow from a point light, an ellipse stretched away from under the lamp (ShadowEllipse), which is
    // what RoyalObservatoryVfxFlare draws. It used to be a circle round the planet's middle's projection, its radius the
    // planet's times how much farther the floor is (Shade, kept for the check); the outer orbit's shadow was 3.8 m
    // round and is now about 3.9 x 6.6 m. The timing, the push and the planets are as they were.
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
        // R725 ro-vfx: read only, for the effects drawn from the same clock (RoyalObservatoryVfxFlare).
        public float Calm => calm;
        public float Warn => warn;
        public float Burn => burn;
        public float Grace => grace;
        public Vector3 Sun => sun;          // the lamp's middle, in this object's frame
        public Transform Lamp => lamp;
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

        // Seconds into the flare's cycle at a time: calm from 0, the warning from Calm, the burn from Calm + Warn.
        public float CycleTime(double time)
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

        // How big the lamp is drawn at a time, times its own size: one smooth curve round the cycle (R725 ro-vfx, user
        // 11-20 "태양 커지는 움직임을 자연스럽게 해줘 지금 끊기면서 커져" - it used to shake through the warning, 6 Hz and
        // 5%, and jump twice, 1.35 to 1.55 as the burn began and 1.55 to 1 as it ended). It settles from the burn over the
        // calm's first 0.8 s, breathing 1% all the while (twice a cycle, so the same at its end as at its start); swells
        // through the warning to 1.32 and draws in a little over its last 15%; bursts out to 1.72 in the burn's first
        // 0.16 s and back to 1.55 by 0.4 s, fast but without a jump, and eases to 1.5 by its end, where the calm's
        // settling takes it up. Drawn only: the
        // lamp has no collider. RoyalObservatoryVfxFlare times the corona's drawing in to the same 15%.
        // R725 ro-vfx (user 11-20 "태양이 폭발 할 때 더 폭발 하는 것처럼 해줘"): the burn's first breath out overshoots -
        // 1.72 in 0.16 s, back to 1.55 by 0.4 s - still one smooth curve (no jump, no kink).
        public const float WarnPeak = 1.32f, Inhale = .04f, PopPeak = 1.72f, BurstPeak = 1.55f, BurnEnd = 1.5f, InhaleFrom = .85f;
        public const float PopRise = .16f, PopSettle = .4f;

        public float Swell(double time)
        {
            float t = CycleTime(time);
            float breathe = .01f * Mathf.Sin(t / Mathf.Max(Cycle, .01f) * 4f * Mathf.PI);
            if (t < calm)
            {
                float settle = Mathf.Max(Mathf.Min(.8f, calm), .01f);
                return 1f + (BurnEnd - 1f) * (1f - Mathf.SmoothStep(0f, 1f, t / settle)) + breathe;
            }
            if (t < calm + warn)
            {
                float w = (t - calm) / Mathf.Max(warn, .01f);
                return 1f + (WarnPeak - 1f) * Mathf.SmoothStep(0f, 1f, w / InhaleFrom)
                       - Inhale * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(InhaleFrom, 1f, w)) + breathe;
            }
            float into = t - calm - warn, b = into / Mathf.Max(burn, .01f);
            float drawnIn = WarnPeak - Inhale, size;
            if (into < PopRise) size = Mathf.Lerp(drawnIn, PopPeak, Mathf.SmoothStep(0f, 1f, into / PopRise));   // the pop
            else size = Mathf.Lerp(PopPeak, BurstPeak, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PopRise, PopSettle, into)));
            return size - (BurstPeak - BurnEnd) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, 1f, b)) + breathe;
        }

        // Where a planet's middle throws its shadow on the floor now (x, z in this object's frame) and the round size the
        // shelter had before R725 ro-vfx (the planet's radius times how much farther the floor is than the planet).
        // The shelter itself is ShadowEllipse / Sheltered. False when the planet is not between the lamp and the floor.
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

        // A planet's middle (this object's frame) and radius now. False as Shade.
        public bool Body(int planet, out Vector3 middle, out float size)
        {
            middle = Vector3.zero;
            size = 0f;
            if (planet < 0 || planet >= planets.Length || planets[planet] == null) return false;
            middle = transform.InverseTransformPoint(planets[planet].position);
            if (middle.y <= .05f || middle.y >= sun.y - .05f) return false;
            size = planet < sizes.Length ? sizes[planet] : 1f;
            return true;
        }

        // Whether a point of the floor (this object's frame; its height is not looked at) is in some planet's shadow.
        public bool Sheltered(Vector3 at)
        {
            for (int i = 0; i < planets.Length; i++)
                if (ShelteredBy(i, at)) return true;
            return false;
        }

        // Whether the floor under `at` is in this planet's real shadow: the line from it to the lamp's middle passes
        // through the planet. `grace` is kept as about that much more on the floor: the planet counts as that much
        // bigger over how much farther the floor is than the planet (0.15 m across a shadow and at its near end, up to
        // about 0.4 m at the outer orbit's far end, where the shadow stretches most).
        public bool ShelteredBy(int planet, Vector3 at)
        {
            if (!Body(planet, out var middle, out float size)) return false;
            var floor = new Vector3(at.x, 0f, at.z);
            float spread = sun.y / Mathf.Max(sun.y - middle.y, .05f);
            return SegmentDistance(floor, sun, middle) <= size + grace / spread;
        }

        static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector3.Distance(a + ab * t, p);
        }

        // A planet's real shadow on the floor (y 0) from the lamp's middle, without the grace: the section of the cone
        // from the lamp round the planet - an ellipse whose long axis points away from under the lamp (`along`, x z),
        // `centre` its middle, `semiMajor` along that and `semiMinor` across. False as Shade.
        public bool ShadowEllipse(int planet, out Vector2 centre, out Vector2 along, out float semiMajor, out float semiMinor)
        {
            centre = along = Vector2.zero;
            semiMajor = semiMinor = 0f;
            if (!Body(planet, out var middle, out float size)) return false;
            var foot = new Vector2(sun.x, sun.z);
            var off = new Vector2(middle.x, middle.z) - foot;
            float drop = sun.y - middle.y, reach = off.magnitude, distance = Mathf.Sqrt(reach * reach + drop * drop);
            float cone = Mathf.Asin(Mathf.Clamp(size / Mathf.Max(distance, .01f), 0f, .999f));   // half-angle
            float tilt = Mathf.Atan2(reach, drop);                                                // axis from straight down
            along = reach > 1e-4f ? off / reach : Vector2.up;
            float far = sun.y * Mathf.Tan(Mathf.Min(tilt + cone, 1.5f)), near = sun.y * Mathf.Tan(tilt - cone);
            float mid = (far + near) * .5f;
            semiMajor = (far - near) * .5f;
            // Across, at the middle: a floor point (mid, v) lies on the cone where its line to the lamp makes the
            // half-angle with the axis.
            float onAxis = mid * Mathf.Sin(tilt) + sun.y * Mathf.Cos(tilt), cos = Mathf.Cos(cone);
            semiMinor = Mathf.Sqrt(Mathf.Max(onAxis * onAxis / (cos * cos) - mid * mid - sun.y * sun.y, 0f));
            centre = foot + along * mid;
            return true;
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
            if (lamp != null) lamp.localScale = lampScale * Swell(now);
            for (int i = 0; i < shades.Length; i++)
            {
                if (shades[i] == null) continue;
                // The graybox's flat shadow: the real shadow's ellipse (R725 ro-vfx), its long side away from the lamp.
                bool on = ShadowEllipse(i, out var centre, out var along, out float major, out float minor);
                if (shades[i].gameObject.activeSelf != on) shades[i].gameObject.SetActive(on);
                if (!on) continue;
                shades[i].localPosition = new Vector3(centre.x, .05f, centre.y);
                shades[i].localRotation = Quaternion.LookRotation(new Vector3(along.x, 0f, along.y), Vector3.up);
                shades[i].localScale = new Vector3(minor * 2f, .004f, major * 2f);
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
