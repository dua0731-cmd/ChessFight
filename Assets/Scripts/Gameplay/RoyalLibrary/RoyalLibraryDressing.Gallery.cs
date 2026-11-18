namespace ChessFight.Gameplay
{
    // R4 front-course gallery (ArtSource/PawnRush/RoyalLibraryModels/rl_r4_gallery_model.py, PLAN.md R4, 11-18): the
    // library hall around the front course (z -150 .. -10). Every collider stays the blockout's: walls, ceiling and
    // floor only stop drawing.
    //   RlGalleryWalls    both gallery walls (x +-38) and the entrance wall's inner face: walnut wainscot, pilasters,
    //                     cream stone with arched windows (shape only, no light: R7). The portal zone is R1's.
    //   RlGalleryCeiling  coffered ceiling under "Operations ceiling" (y 30)
    //   RlGalleryFloor    walnut boards on the far floor (y -23)
    //   RlGalleryBookcase one shape on all seven "Royal library bookcase" of the front course (the chess room's are R5's)
    public sealed partial class RoyalLibraryDressing
    {
        static readonly string[] GalleryBookcaseParts =
        {
            "Walnut bookcase back", "Carved upright", "Library shelf*", "Leather bound volume*", "Book spine gold title*",
        };

        static Hang GalleryBookcase(string name) => new Hang
        {
            zone = "Gallery", model = "RlGalleryBookcase", on = name, under = name, covers = GalleryBookcaseParts,
        };

        // Board frames: the wooden board only; its squares, WarRoomScoreLabel's markers and caption stay in front.
        static Hang GalleryFrame(string name) => new Hang
        {
            zone = "Gallery", model = "RlGalleryFrame", on = name, under = name, covers = new[] { "Framed score board" },
        };

        // Empty pots (body and rim only). Soil and plants sit inside them from RoyalLibraryDressing.Plants.cs (plants
        // session), which covers the blockout "Leaf" / "Plant trunk" / "Soil"; pot rim: inner radius 2.88 x s, soil
        // seat 4.15 x s (s = 1.5 / 1.8 / 1.2) - tell that session if these change.
        static Hang GalleryPlanter(string model, string name) => new Hang
        {
            zone = "Gallery", model = model, on = name, under = name, covers = new[] { "Ceramic planter", "Pot rim" },
        };

        static readonly Hang[] Gallery =
        {
            new Hang
            {
                zone = "Gallery", model = "RlGalleryWalls",
                covers = new[]
                {
                    "Royal library gallery wall -1", "Royal library gallery wall 1",
                    "Square perimeter entrance -83", "Square perimeter entrance 83", "Entrance header",
                },
            },
            new Hang { zone = "Gallery", model = "RlGalleryCeiling", covers = new[] { "Operations ceiling" } },
            new Hang { zone = "Gallery", model = "RlGalleryFloor", covers = new[] { "Distant operations room floor" } },
            GalleryBookcase("Royal library bookcase -35 -140"),
            GalleryBookcase("Royal library bookcase -35 -102"),
            GalleryBookcase("Royal library bookcase -35 -64"),
            GalleryBookcase("Royal library bookcase -35 -24"),
            GalleryBookcase("Royal library bookcase 35 -135"),
            GalleryBookcase("Royal library bookcase 35 -88"),
            GalleryBookcase("Royal library bookcase 35 -20"),
            GalleryFrame("Random GM replay preview 0"),
            GalleryFrame("Random GM replay preview 0.25"),
            GalleryFrame("Random GM replay preview 0.55"),
            GalleryFrame("Random GM replay preview 0.9"),
            GalleryPlanter("RlPlanterFluted112", "Library gallery houseplant (28, -22.5, -112)"),
            GalleryPlanter("RlBrassTub53", "Library gallery houseplant (29, -22.5, -53)"),
            GalleryPlanter("RlPlanterFluted35", "Library gallery houseplant (-28, -22.5, -35)"),
        };
    }
}
