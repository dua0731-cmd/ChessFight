namespace ChessFight.PawnRush
{
    // R1 the entry and the Big Dipper's handle (ArtSource/PawnRush/RoyalObservatory/PLAN.md). R0's test piece first:
    // the first star platform (ro_r0_star_model.py -> Models/Dipper/RoDipperStar1.fbx + its lamps), in the section's
    // frame, over the graybox's deck, brass rim, five-ray mark and pillar (their colliders stay).
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly Hang[] Dipper =
        {
            new Hang { zone = "Dipper", model = "RoDipperStar1", under = "1 Big Dipper", covers = new[] { "Dipper star 1*" } },
            // R1: star 4, the handle's end (ro_stars_model.py, the R0 star platform with its own top and lamps).
            new Hang { zone = "Dipper", model = "RoDipperStar4", under = "1 Big Dipper", covers = new[] { "Dipper star 4*" } },
            // R1 part 1 (ro_r1_gate_model.py): the arched entry gate on the hall's south wall round the tunnel's
            // opening, its two leaves open against the tunnel's sides. Nothing of the graybox stands there to cover.
            new Hang { zone = "Dipper", model = "RoEntryGate" },
            // R1 part 5 (ro_r1_entry_model.py): the entry tunnel's walk - marble slabs and a runner of motif panels -,
            // the threshold under star 1 and the four balustrades with their pearl lamps.
            new Hang { zone = "Dipper", model = "RoEntryWalk", under = "0 Entry tunnel",
                       covers = new[] { "Entry floor", "Entry threshold", "Entry rail *" } },
            // R1 part 2 (ro_r1_lines_model.py): the four constellation lines' bridges (handle 1-2, 2-3, 3-4 and the
            // pointer from star 7 to the west gate), over the graybox's line boxes and their gold mark bars.
            new Hang { zone = "Dipper", model = "RoDipperLines", under = "1 Big Dipper", covers = new[] { "Dipper line *", "Pointer line *" } },
            // R1 part 3 (ro_r1_petals_model.py): the variable stars 2 and 3 - the hub, rod and pillar in the section's
            // frame; each petal in its StarPetal device's frame, so it folds with it ("Petal warning" is left drawing).
            new Hang { zone = "Dipper", model = "RoVariableHub2", under = "1 Big Dipper",
                       covers = new[] { "Dipper star 2 core", "Dipper star 2 mark", "Dipper star 2 pillar" } },
            new Hang { zone = "Dipper", model = "RoVariableHub3", under = "1 Big Dipper",
                       covers = new[] { "Dipper star 3 core", "Dipper star 3 mark", "Dipper star 3 pillar" } },
            Petal(2, 1), Petal(2, 2), Petal(2, 3), Petal(2, 4), Petal(2, 5),
            Petal(3, 1), Petal(3, 2), Petal(3, 3), Petal(3, 4), Petal(3, 5),
        };

        // A petal's model rides its device and covers its "Petal" and the flat blue "Petal warning" overlay (the user,
        // 11-19: "접히기 전 예고 같은 걸로 파란색 블록 아웃처럼 보여"). StarPetal still turns the warning object on and
        // off; PetalWarnings below makes the model's gold glow while it is on.
        static Hang Petal(int star, int petal)
        {
            string device = "Device_StarPetal Dipper star " + star + " " + petal;
            return new Hang { zone = "Dipper", model = "RoPetal" + star + "_" + petal, on = device, under = device,
                              covers = new[] { "Petal", "Petal warning" } };
        }

        // R1 part 4 (ro_r1_meteor_model.py): every meteor shower's views - the handle's and Cassiopeia's (PLAN R4 shares
        // R1's meteor) - drawn with the marble stone, its glowing tail and the engraved gold warning ring.
        static readonly System.Action<RoyalObservatoryDressing> Meteors = dressing =>
        {
            var stone = UnityEngine.Resources.Load<UnityEngine.GameObject>(ModelFolder + "Dipper/RoMeteor");
            var ring = UnityEngine.Resources.Load<UnityEngine.GameObject>(ModelFolder + "Dipper/RoMeteorRing");
            if (stone == null || ring == null) return;
            foreach (var shower in dressing.FindStarting("Device_MeteorShower"))
                shower.gameObject.AddComponent<MeteorShowerDress>().Configure(stone, ring, dressing.PaintModel);
        };

        static readonly System.Action<RoyalObservatoryDressing> PetalWarnings = dressing =>
        {
            for (int star = 2; star <= 3; star++)
                for (int petal = 1; petal <= 5; petal++)
                    foreach (var device in dressing.FindAll("Device_StarPetal Dipper star " + star + " " + petal))
                    {
                        var warning = device.Find("Petal warning");
                        var model = device.Find("RoPetal" + star + "_" + petal + " (model)");
                        if (warning == null || model == null) continue;
                        device.gameObject.AddComponent<DeviceWarningGlow>()
                              .Configure(warning.gameObject, model.GetComponentsInChildren<UnityEngine.Renderer>(true));
                    }
        };
    }
}
