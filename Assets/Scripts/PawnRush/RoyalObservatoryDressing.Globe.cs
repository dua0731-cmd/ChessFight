using System;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R3 the great globe (ArtSource/PawnRush/RoyalObservatory/PLAN.md; ro_r3_globe_model.py, the ro-hall session, 11-19):
    // ten parts the user passed one by one and then put together. Each FBX was written in the frame of the object it
    // hangs on, so the floor, the sun, the orbits and the moons turn and swell with the builder's devices; their
    // colliders and walking faces stay the builder's.
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly Hang[] Globe =
        {
            // The glass and its engraving, the meridian and horizon rings, the stepped base, the two gate frames.
            new Hang
            {
                zone = "Globe", model = "RoGlobeShell", under = "2 Great globe",
                covers = new[] { "Great globe glass", "Meridian ring", "Horizon ring", "Globe base*", "West gate frame", "East gate frame" },
            },
            // The two gate thresholds: plain cream marble slabs (the user 11-19: "크림색 대리석만"), the builder's boxes
            // and walking faces unchanged.
            new Hang
            {
                zone = "Globe", model = "RoGlobeThresholds", under = "2 Great globe",
                covers = new[] { "West gate threshold", "East gate threshold" },
            },
            // The tilted floor (cream marble, the gold star chart), the sun's rod and foot, the orbits' wires, the
            // ecliptic track (the builder's ring is in the floor's frame too).
            new Hang
            {
                zone = "Globe", model = "RoGlobeFloor", on = "Device_SolarFlare", under = "2 Great globe",
                covers = new[] { "Ecliptic floor", "Planet orbit*", "Shadow track*", "Ecliptic ring" },
            },
            // The sun: rides the builder's "Sun lamp", which swells in the flare. Its surface (RO_PlanetSun) glows by
            // itself. (Its halo rings were taken out, the user 11-19: "일단 고리 빼주고".)
            new Hang { zone = "Globe", model = "RoGlobeSun", on = "Sun lamp", covers = new[] { "Sun lamp" }, under = "Device_SolarFlare" },
            new Hang { zone = "Globe", model = "RoGlobeOrbit1", on = "Device_PlanetOrbit 1", under = "Device_PlanetOrbit 1", covers = new[] { "Planet *" } },
            new Hang { zone = "Globe", model = "RoGlobeOrbit2", on = "Device_PlanetOrbit 2", under = "Device_PlanetOrbit 2", covers = new[] { "Planet *" } },
            new Hang { zone = "Globe", model = "RoGlobeOrbit3", on = "Device_PlanetOrbit 3", under = "Device_PlanetOrbit 3", covers = new[] { "Planet *" } },
            // The armillary's three gimbals (the lobby's look, the user 11-19: "사진에 보이는 고리를 로비처럼"): each FBX
            // was written in its pivot's frame (the globe's middle), so it swings with GlobeArmillary. Gold tubes with
            // RO_Gold's stars and scrolls wrapped round them (their own UVs), pins and balls where they hang.
            new Hang { zone = "Globe", model = "RoGlobeGimbalA", on = "Globe gimbal A", under = "Globe gimbal A", covers = new[] { "A ring *" } },
            new Hang { zone = "Globe", model = "RoGlobeGimbalB", on = "Globe gimbal B", under = "Globe gimbal B", covers = new[] { "B ecliptic band", "B colure" } },
            new Hang { zone = "Globe", model = "RoGlobeGimbalC", on = "Globe gimbal C", under = "Globe gimbal C", covers = new[] { "C ring *" } },
        };

        // The sun's light: brighter than a rail lamp and reaching the whole floor (13.6 m round), warm, no shadow.
        public static Color SunColor = new Color(1f, .8f, .5f);
        public static float SunIntensity = 9f, SunRange = 22f;

        static readonly Action<RoyalObservatoryDressing> GlobeSun = dressing =>
        {
            foreach (var lamp in dressing.FindAll("Sun lamp"))
            {
                var go = new GameObject("Sun light (model)");
                go.transform.SetParent(lamp, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = SunColor;
                light.intensity = SunIntensity;
                light.range = SunRange;
                light.shadows = LightShadows.None;
            }
        };
    }
}
