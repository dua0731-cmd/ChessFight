namespace ChessFight.PawnRush
{
    // R725 ro-vfx2 (Royal Observatory R7, the second device-VFX session beside ro-vfx's .Vfx.cs): more devices' effects,
    // added once the models hang. Each is a component beside its device that only draws - when is the device's (the
    // shared obstacle clock), so nothing is sent - one at a time, each seen by the user in Unity Play before the next:
    //   1 variable-star petals   RoyalObservatoryVfxPetals (star dust and glints shed as a petal folds, gold bleeding
    //                            out along its seams as the petals come up; the red warning pulse stays DeviceWarningGlow's)
    //   2 gears, orbits, planet  RoyalObservatoryVfxGears / VfxOrbits (rare sparks where cogs mesh and where a platform's
    //                            rim runs over its track), RoyalObservatoryVfxPlanet (star dust off the rolling planet's
    //                            rails); shared bits in RoyalObservatoryVfxSparks
    //   3 the hall's air         RoyalObservatoryVfxHall (dust and star motes, lit where they cross ro-post's moon shaft -
    //                            no second beam - and faint nebula clouds welling up out of the abyss)
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly System.Action<RoyalObservatoryDressing> PetalVfx = dressing =>
        {
            int petals = 0;
            foreach (var device in dressing.FindStarting("Device_StarPetal"))
                if (device.GetComponent<StarPetal>() != null && device.GetComponent<RoyalObservatoryVfxPetals>() == null)
                {
                    device.gameObject.AddComponent<RoyalObservatoryVfxPetals>();
                    petals++;
                }
            UnityEngine.Debug.Log("[천문대 VFX2] 변광성 꽃잎 " + petals + "장: 접힐 때 별가루 · 금빛 반짝이, 펴질 때 금빛 번짐");
        };

        // 2: Cassiopeia's gears spark now and then where they mesh (each with the one before it, the first with the
        // standing star), the orbits' platforms where their rims run over the track, and the rolling planet kicks
        // star dust off its rails.
        static readonly System.Action<RoyalObservatoryDressing> MovingPartVfx = dressing =>
        {
            int meshes = 0, orbits = 0, planets = 0;
            UnityEngine.Transform before = null;
            foreach (var star in dressing.FindAll("Cassiopeia star 1")) { before = star; break; }
            for (int n = 2; n <= 5; n++)
            {
                UnityEngine.Transform gear = null;
                foreach (var g in dressing.FindAll("Device_StarGear Cassiopeia gear " + n)) { gear = g; break; }
                if (gear == null) continue;
                if (before != null && gear.GetComponent<RoyalObservatoryVfxGears>() == null)
                {
                    gear.gameObject.AddComponent<RoyalObservatoryVfxGears>().Configure(before, n);
                    meshes++;
                }
                before = gear;
            }
            foreach (var orbit in dressing.FindStarting("Device_OrbitRing"))
                if (orbit.GetComponent<ChessFight.ProtectKing.ObstacleMotion>() != null && orbit.GetComponent<RoyalObservatoryVfxOrbits>() == null)
                    orbit.gameObject.AddComponent<RoyalObservatoryVfxOrbits>().Configure(++orbits);
            foreach (var planet in dressing.FindStarting("Device_RollingPlanet"))
                if (planet.GetComponent<RollingPlanet>() != null && planet.GetComponent<RoyalObservatoryVfxPlanet>() == null)
                {
                    planet.gameObject.AddComponent<RoyalObservatoryVfxPlanet>();
                    planets++;
                }
            UnityEngine.Debug.Log("[천문대 VFX2] 톱니 맞물림 " + meshes + "곳 불티 · 궤도 " + orbits + "개 발판 불티 · 구르는 행성 " + planets
                                  + "개 레일 별가루");
        };

        // 3: the hall's air - dust and star motes (lit in ro-post's moon shaft) and nebula glow welling up from the abyss.
        static readonly System.Action<RoyalObservatoryDressing> HallAirVfx = dressing =>
        {
            if (dressing.GetComponent<RoyalObservatoryVfxHall>() != null) return;
            dressing.gameObject.AddComponent<RoyalObservatoryVfxHall>();
            UnityEngine.Debug.Log("[천문대 VFX2] 홀 공중 먼지 · 별가루(달빛 줄기 속에서 밝아짐) · 심연에서 올라오는 성운 빛");
        };
    }
}
