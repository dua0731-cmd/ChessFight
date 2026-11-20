namespace ChessFight.PawnRush
{
    // R725 ro-vfx (Royal Observatory R7 part 1, PLAN section 5 "장치 VFX 후보"): the devices' effects, added once the models
    // hang. Each is a component beside its device that only draws - when is the device's (the shared obstacle clock),
    // so nothing is sent - one device at a time, each seen by the user in Unity Play before the next:
    //   1 meteor shower    RoyalObservatoryVfxMeteor (warning glow, air streaks and shed sparks, shockwave, dust, sparks)
    //   2 solar flare      RoyalObservatoryVfxFlare (the planets' real shadows - SolarFlare judges by the same - heat,
    //                      blaze, ripple and shimmer on the floor, glow on exposed pawns, the sun's corona, glow and light)
    // Post-processing, the moonlight shaft and reflection probes are RoyalObservatoryLighting's, not these.
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly System.Action<RoyalObservatoryDressing> MeteorVfx = dressing =>
        {
            int showers = 0;
            foreach (var shower in dressing.FindStarting("Device_MeteorShower"))
                if (shower.GetComponent<MeteorShower>() != null && shower.GetComponent<RoyalObservatoryVfxMeteor>() == null)
                {
                    shower.gameObject.AddComponent<RoyalObservatoryVfxMeteor>();
                    showers++;
                }
            UnityEngine.Debug.Log("[천문대 VFX] 유성우 " + showers + "곳: 경고 고리 빛 번짐 · 공기 줄무늬 · 불티 · 금빛 충격파 · 별가루 먼지");
        };

        static readonly System.Action<RoyalObservatoryDressing> BeamVfx = dressing =>
        {
            int beams = 0;
            foreach (var device in dressing.FindStarting("Device_FocusBeam"))
                if (device.GetComponent<FocusBeam>() != null && device.GetComponent<RoyalObservatoryVfxBeam>() == null)
                {
                    device.gameObject.AddComponent<RoyalObservatoryVfxBeam>();
                    // R725 (user "온라인데서 경통에서 빛이 다으면 기물이에 타격감"): the strike on a thrown pawn, on every PC.
                    device.gameObject.AddComponent<RoyalObservatoryVfxBeamHit>();
                    beams++;
                }
            UnityEngine.Debug.Log("[천문대 VFX] 초점 광선 " + beams + "개: 렌즈에서 내려오는 빛기둥 · 먼지 반짝임 · 갑판 빛 원(판정 반지름 그대로)");
        };

        // R725 ro-vfx 4: each iris gate's sparks and clash, on an object beside its "Iris N warning" bar (the bar is
        // switched off between warnings; this must not be), timed by its west leaf (both leaves share the timing).
        static readonly System.Action<RoyalObservatoryDressing> IrisVfx = dressing =>
        {
            int gates = 0;
            for (int n = 1; n <= 4; n++)
            {
                var marks = dressing.FindAll("Iris " + n + " warning");
                var leaves = dressing.FindAll("Device_IrisLeaf " + n + " W");
                if (marks.Count == 0 || leaves.Count == 0 || !leaves[0].TryGetComponent<IrisLeaf>(out var leaf)) continue;
                var mark = marks[0];
                var go = new UnityEngine.GameObject("Iris " + n + " VFX");
                go.transform.SetParent(mark.parent, false);
                go.transform.localPosition = mark.localPosition;
                go.transform.localRotation = mark.localRotation;
                go.AddComponent<RoyalObservatoryVfxIris>().Configure(leaf, leaf.Travel + .02f);
                gates++;
            }
            UnityEngine.Debug.Log("[천문대 VFX] 조리개 " + gates + "곳: 닫힐 때 금속 불티 · 닫힌 순간 섬광과 불꽃");
        };

        static readonly System.Action<RoyalObservatoryDressing> FlareVfx = dressing =>
        {
            foreach (var device in dressing.FindStarting("Device_SolarFlare"))
                if (device.GetComponent<SolarFlare>() != null && device.GetComponent<RoyalObservatoryVfxFlare>() == null)
                    device.gameObject.AddComponent<RoyalObservatoryVfxFlare>();
        };
    }
}
