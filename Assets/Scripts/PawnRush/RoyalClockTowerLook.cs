using UnityEngine;

namespace ChessFight.PawnRush
{
    // The royal clock tower's grey-box look (R775). Here colour says what a face is for, because this is the section where
    // walls are the way: orange = a face to climb, slate = a face that cannot be climbed, violet = something that moves and
    // carries whoever hangs on it or stands on it, green = a place to stand and get the breath back, blue = a way down, red =
    // the drum's combs. Floors are plain grey. None of these names is in Arena's kit tables, so Arena.Lit gives the plain
    // colour it is asked for and caches it.
    public static class RoyalClockTowerLook
    {
        public static readonly Color InkColor = Arena.Hex("#292433");

        public static Material Floor => Arena.Lit("M_ClockFloor", Arena.Hex("#BDB9B1"), 0f, .9f);
        public static Material Climb => Arena.Lit("M_ClockClimb", Arena.Hex("#E0943A"), 0f, .85f);
        public static Material NoClimb => Arena.Lit("M_ClockNoClimb", Arena.Hex("#566074"), 0f, .45f);
        public static Material Mover => Arena.Lit("M_ClockMover", Arena.Hex("#A99CE8"), .15f, .5f);
        public static Material Rest => Arena.Lit("M_ClockRest", Arena.Hex("#5DCAA5"), 0f, .8f);
        public static Material Down => Arena.Lit("M_ClockDown", Arena.Hex("#85B7EB"), 0f, .6f);
        public static Material Comb => Arena.Lit("M_ClockComb", Arena.Hex("#FF4A3A"), 1.2f, .4f);
        public static Material Dial => Arena.Lit("M_ClockDial", Arena.Hex("#FFF3D6"), .5f, .6f);

        static SectionKit kit;

        // A section kit made at run time (the royal vault's pattern) for CourseBuilder: grey floors, orange for what it calls
        // climbable, slate for what it does not. Never saved.
        public static SectionKit Kit
        {
            get
            {
                if (kit == null)
                {
                    kit = ScriptableObject.CreateInstance<SectionKit>();
                    kit.name = "RoyalClockTowerKit";
                    kit.hideFlags = HideFlags.DontSave;
                }
                var floor = Floor;
                if (kit.floorChecker != floor)
                {
                    kit.floorChecker = floor;
                    kit.wallClimbable = Climb;
                    kit.wallNoClimb = NoClimb;
                    kit.glass = NoClimb;
                    kit.trimWhite = floor;
                    kit.trimBlack = floor;
                    kit.rankGold = floor;
                    kit.boardLight = floor;
                    kit.boardDark = floor;
                    kit.hazard = Comb;
                    kit.marker = Rest;
                    kit.respawnFloor = floor;
                }
                return kit;
            }
        }

        // A sign in ink, yaw 0 (read coming from -z), no collider.
        public static TextMesh Sign(Transform parent, string text, Vector3 at, float size, float yaw = 0f) =>
            YardKit.Label(parent, text, at, size, InkColor, yaw);
    }
}
