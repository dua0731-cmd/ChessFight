namespace ChessFight.PawnRush
{
    // R2 the Big Dipper's bowl (ArtSource/PawnRush/RoyalObservatory/PLAN.md): star 7 (ro_stars_model.py, the R0 star
    // platform), the three crossing orbits (ro_r2_orbits_model.py), then the rolling planet.
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly Hang[] Bowl =
        {
            new Hang { zone = "Bowl", model = "RoDipperStar7", under = "1 Big Dipper", covers = new[] { "Dipper star 7*" } },
            Orbit(1), Orbit(2), Orbit(3),
            Track(1), Track(2), Track(3),
            new Hang { zone = "Bowl", model = "RoOrbitPillars", under = "1 Big Dipper", covers = new[] { "Orbit pillar *" } },
            // The short way (ro_r2_rolling_model.py): the ball rides RollingPlanet's device (its "Planet" collider stays);
            // the rails, ties, buffers and posts in the section's frame.
            new Hang { zone = "Bowl", model = "RoRollingPlanet", on = "Device_RollingPlanet", under = "Device_RollingPlanet",
                       covers = new[] { "Planet", "Band a", "Band b", "Spot" } },
            new Hang { zone = "Bowl", model = "RoPlanetRails", under = "1 Big Dipper",
                       covers = new[] { "Planet rail", "Planet rail tie *", "Planet rail post *" } },
        };

        // An orbit's turning part rides its device: three small star platforms with their planets, the arms and the sun.
        static Hang Orbit(int i)
        {
            string device = "Device_OrbitRing " + i;
            return new Hang { zone = "Bowl", model = "RoOrbitRing" + i, on = device, under = device,
                              covers = new[] { "Planet deck *", "Planet brass rim *", "Planet body *", "Planet mark *", "Arm *", "Orbit sun" } };
        }

        // The orbit's gold ring stays still, tilted with it.
        static Hang Track(int i)
        {
            string track = "Orbit track " + i;
            return new Hang { zone = "Bowl", model = "RoOrbitTrack" + i, on = track, under = "1 Big Dipper", covers = new[] { track } };
        }
    }
}
