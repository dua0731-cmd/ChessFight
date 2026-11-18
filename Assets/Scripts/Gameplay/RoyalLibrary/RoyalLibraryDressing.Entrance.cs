namespace ChessFight.Gameplay
{
    // R1 entrance + stamp zone (PLAN.md R1, 11-18; ArtSource/PawnRush/RoyalLibraryModels/rl_r1_entrance_model.py):
    // the start and dispatch tables (E5 course table kit, took over R0's test table), the two stamp presses (fixed
    // column, arm and guide collar per side; the round head, ram and knob in "Stamp head"'s frame, one FBX on both
    // heads, so it moves with CommandStampPress), the stamp area in the seal's look (user 11-18: a red wax seal under
    // each stamp, the safe waiting lines as wax strips between brass, and the strike warning as a wax plate with a
    // round opening round the seal - hung on "Rectangular strike warning", so it shows and hides with the warning),
    // and the dimensional entry vestibule after GC_assets_04 (walnut box, marble arch with 24 rune stones following
    // the arch, marble floor, brass ball posts). Colliders, the stamp's travel (raisedHeight 3) and the walkable tops
    // (y 0) stay the prefab's. The veil keeps its object (RoyalLibraryPortal hides it): only its look changes.
    public sealed partial class RoyalLibraryDressing
    {
        static readonly Hang[] Entrance =
        {
            new Hang { zone = "Entrance", model = "RlStartTable", covers = new[] { "Approach floor -146*" } },
            new Hang { zone = "Entrance", model = "RlDispatchTable", covers = new[] { "Dispatch table after jump*" } },
            new Hang
            {
                zone = "Entrance", model = "RlStampHead", on = "Stamp head - edit timing and impulse",
                under = "Stamp head - edit timing and impulse", covers = new[] { "Press brass head", "Crown die" },
            },
            new Hang { zone = "Entrance", model = "RlStampFrames" },
            new Hang { zone = "Entrance", model = "RlWaxSeals", covers = new[] { "Safe waiting inlay" } },
            new Hang
            {
                zone = "Entrance", model = "RlStrikeWarning", on = "Rectangular strike warning",
                under = "Rectangular strike warning", covers = new[] { "Rectangular strike warning" },
            },
            new Hang
            {
                zone = "Entrance", model = "RlPortal", under = "Dimensional entry vestibule",
                covers = new[] { "Narrow portal passage*", "Entry shoulder*", "Portal rune*" },
            },
            // the veil: a translucent rippled membrane filling the arch, hung on the veil object so
            // RoyalLibraryPortal's SetActive still hides and shows it; only the teal box stops drawing
            new Hang
            {
                zone = "Entrance", model = "RlVeil", on = "Dimensional veil", under = "Dimensional veil",
                covers = new[] { "Dimensional veil" },
            },
        };
    }
}
