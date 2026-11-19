namespace ChessFight.Gameplay
{
    // R5 chess room (ArtSource/PawnRush/RoyalLibraryModels/rl_r5_chessroom_model.py, PLAN.md R5, 11-18): the
    // "Headquarters - enclosed command chamber" and the final exit walkway (z -10 .. 170), to concept D and the
    // furniture / door sheets (AssetRefs/GC_assets_03, GC_assets_04). Every collider, walking top and moving axis stays
    // the prefab's: a covered blockout piece only stops drawing. The play-time board, pieces, giants, entry / exit
    // platforms and exit arch are GiantChessCourse.cs's (not here); the four conservatory plants wait for Tripo.
    //   RlRoomWalls        the room's walls (gallery-style walnut wainscot, cream stone, pilasters, board frames on the
    //                      left / rear, arched windows on the right, the exit's stone arch). The front facade and the
    //                      doorway header stay drawn: their other face is the front gallery's far wall.
    //   RlRoomCeiling      coffered ceiling and beams      RlRoomFloor  walnut floor, stone border, the alcove rug
    //   RlRoomOakTable     the giant table under the live board (top y -0.8 kept)
    //   RlRoomCommanderChairs / RlRoomFootstools   sized to the seated giants (rl-giant, x +-64)
    //   RlRoomPieceTrays   props by the chairs (not in the prefab)
    //   RlRoomReadingChaise · RlRoomSideTable (with the four volumes) · RlRoomPortrait
    //   RlRoomDoorway      posts, pilasters, lintel and crest round the double doors (the banners give way)
    //   RlRoomDoorLeafL/R  each rides its own "Solid command door leaf" (index 0 = x -4, 1 = x 4), so it slides
    //   RlRoomExitWalkway  planks flush with its top (y 0), a 0.35 m curb, turned legs
    //   bookcases          the front gallery's RlGalleryBookcase (same prefab bookcase, same pose) on all six
    public sealed partial class RoyalLibraryDressing
    {
        const string ChessRoomRoot = "Headquarters - enclosed command chamber";

        static readonly string[] ChessRoomBookcaseParts =
        {
            "Walnut bookcase back", "Carved upright", "Library shelf*", "Leather bound volume*", "Book spine gold title*",
        };

        static Hang Room(string model, params string[] covers) => new Hang
        {
            zone = "ChessRoom", model = model, under = ChessRoomRoot, covers = covers,
        };

        static Hang RoomBookcase(string name) => new Hang
        {
            zone = "Gallery", model = "RlGalleryBookcase", on = name, under = name, covers = ChessRoomBookcaseParts,
        };

        static readonly Hang[] ChessRoom =
        {
            Room("RlRoomWalls", "HQ side wall", "HQ rear wall", "HQ rear exit right", "HQ rear exit header",
                 "Tall arched window frame", "Moonlit window pane"),
            Room("RlRoomCeiling", "HQ coffered ceiling", "Carved ceiling beam", "Ceiling cross beam"),
            Room("RlRoomFloor", "Room floor below fall reset", "Reading alcove rug"),
            Room("RlRoomOakTable", "Giant oak table", "Table leg", "Brass leg collar"),
            Room("RlRoomCommanderChairs", "Commander chair seat", "Commander chair back", "Chair crown", "Chair arm rest"),
            Room("RlRoomFootstools"),
            Room("RlRoomPieceTrays"),
            Room("RlRoomReadingChaise", "Reading chaise", "Chaise back"),
            Room("RlRoomSideTable", "Reading side table", "Oversized stacked volume*"),
            Room("RlRoomPortrait", "Royal library rear portrait", "Portrait velvet"),
            Room("RlRoomDoorway", "HQ door carved jamb", "HQ door lintel", "HQ royal crest", "Royal standard"),
            new Hang
            {
                zone = "ChessRoom", model = "RlRoomDoorLeafL", on = "Solid command door leaf", index = 0,
                under = "Headquarters privacy doors - proximity reveal",
                covers = new[] { "Oak command door", "Brass inset", "Purple panel" },
            },
            new Hang { zone = "ChessRoom", model = "RlRoomDoorLeafR", on = "Solid command door leaf", index = 1 },
            new Hang { zone = "ChessRoom", model = "RlRoomExitWalkway", covers = new[] { "Final exit walkway*" } },
            RoomBookcase("Royal library bookcase -119 12"),
            RoomBookcase("Royal library bookcase -119 43"),
            RoomBookcase("Royal library bookcase -119 105"),
            RoomBookcase("Royal library bookcase -119 145"),
            RoomBookcase("Royal library bookcase 119 28"),
            RoomBookcase("Royal library bookcase 119 123"),
        };
    }
}
