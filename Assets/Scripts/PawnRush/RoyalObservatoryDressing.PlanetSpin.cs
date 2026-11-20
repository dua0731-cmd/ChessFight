using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The planets hanging under the bowl's orbit platforms (R2, ro_r2_orbits_model.py: "Orbit ring <n> planet <name>",
    // each its own mesh in the ring's frame) turn on their own axes as they ride round (the user 11-19, a Unity Play
    // screenshot: "아래 행성들도 자연스럽게 돌아가게 해"). A picture only - the planets carry no collider - so it reads
    // Time.deltaTime. Each gets a pivot at its middle, leaning by its own axial tilt, turning at its own pace.
    public sealed partial class RoyalObservatoryDressing
    {
        // name -> (axial tilt in degrees, degrees a second; negative turns the other way, as Venus does)
        public static readonly Dictionary<string, (float tilt, float speed)> PlanetSpins = new Dictionary<string, (float, float)>
        {
            { "mercury", (2f, 14f) }, { "venus", (177f, 9f) }, { "earth", (23.4f, 30f) }, { "mars", (25.2f, 28f) },
            { "jupiter", (3.1f, 55f) }, { "saturn", (26.7f, 50f) }, { "uranus", (82f, -38f) }, { "neptune", (28.3f, 36f) },
        };
        public static float PlanetSpinDefaultSpeed = 30f;

        static readonly Action<RoyalObservatoryDressing> SpinPlanets = dressing =>
        {
            foreach (var t in dressing.FindStarting("Orbit ring "))
            {
                int at = t.name.IndexOf(" planet ", StringComparison.Ordinal);
                if (at < 0 || t.GetComponent<Renderer>() == null || t.parent == null) continue;
                string kind = t.name.Substring(at + " planet ".Length).Trim().ToLowerInvariant();
                var (tilt, speed) = PlanetSpins.TryGetValue(kind, out var s) ? s : (0f, PlanetSpinDefaultSpeed);
                var pivot = new GameObject(t.name + " (spin)").transform;
                pivot.SetParent(t.parent, false);
                pivot.position = t.GetComponent<Renderer>().bounds.center;
                pivot.localRotation = Quaternion.identity;
                t.SetParent(pivot, true);
                var spin = pivot.gameObject.AddComponent<RoyalObservatoryPlanetSpin>();
                // Lean about the platform's east-west line; a seeded start angle so the planets do not turn in step.
                spin.Configure(tilt, speed, (t.name.GetHashCode() & 0xffff) % 360);
            }
        };
    }
}
