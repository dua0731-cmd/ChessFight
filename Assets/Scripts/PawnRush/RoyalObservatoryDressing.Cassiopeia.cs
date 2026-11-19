namespace ChessFight.PawnRush
{
    // R4 Cassiopeia (ArtSource/PawnRush/RoyalObservatory/PLAN.md; ro_r4_cassiopeia_model.py, the ro-hall session, 11-19):
    // the standing star dressed as a gear as RO_3 draws it ("톱니로 해"), its four lamps and the four axles, static in the
    // section's frame; each star gear's model rides its Device_StarGear and turns with it, its teeth standing out under
    // the rim where the builder's cogs mesh. Walking faces, the gears' holes, pivots and meshing stay the builder's.
    // The meteors are ro-path's (MeteorShowerDress dresses Cassiopeia's shower too).
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly Hang[] Cassiopeia =
        {
            new Hang
            {
                zone = "Cassiopeia", model = "RoCassiopeiaStatic", under = "3 Cassiopeia",
                covers = new[]
                {
                    "Cassiopeia star 1", "Cassiopeia star 1 brass rim", "Cassiopeia star 1 mark", "Cassiopeia star 1 pillar",
                    "Cassiopeia gear 2 axle", "Cassiopeia gear 3 axle", "Cassiopeia gear 4 axle", "Cassiopeia gear 5 axle",
                },
            },
            Gear(2, 16), Gear(3, 20), Gear(4, 16), Gear(5, 12),
        };

        static Hang Gear(int n, int teeth)
        {
            string device = "Device_StarGear Cassiopeia gear " + n;
            return new Hang { zone = "Cassiopeia", model = "RoGear" + teeth, on = device, under = device, covers = new[] { "Tooth *", "Hub", "Spoke *" } };
        }
    }
}
