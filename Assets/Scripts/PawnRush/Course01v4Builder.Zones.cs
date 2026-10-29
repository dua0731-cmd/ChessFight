using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 승규 님's test scene (seoungtest, 10-30): course 01's sections and its mini-games built one at a time, each in a
    // group of its own, so the test scene can stand them side by side instead of chained (SeoungTestCourse).
    public static partial class Course01v4Builder
    {
        // The road sections between the plazas, in running order: id, name, and where a pawn starts it (design doc
        // world coordinates, facing north; checked against the floor when the zone is built).
        public static readonly string[] ZoneSections = { "A", "B", "C", "T" };
        public static readonly string[] ZoneSectionNames = { "A 구간 (출발 → 광장 1)", "B 구간 (광장 1 → 광장 2)", "C 구간 (광장 2 → 탑)", "T 탑" };
        static readonly Vector3[] ZoneStarts = { V(0f, 0f, 2f), V(46f, 0f, 105f), V(-3f, 3f, 165f), V(-3f, 6f, 207f) };

        public static Vector3 ZoneStart(string id) => ZoneStarts[System.Array.IndexOf(ZoneSections, id)];

        // One road section into b.parent at the design doc's coordinates.
        public static void Section(CourseBuilder b, string id)
        {
            switch (id)
            {
                case "A": SharedA(b); break;
                case "B": SharedB(b); break;
                case "C": SharedC(b); break;
                case "T":
                    // The tower stands on C's last floor (y 6): alone it needs a floor of its own in front.
                    b.Floor("Approach (test only)", -13f, 7f, 200f, 210f, 6f);
                    Tower(b);
                    break;
            }
        }

        // One mini-game alone: white's station (plaza 1 sizes) with the plaza's entry band in front of it, the station's
        // south edge at the group's origin. The pawn starts on the entry band, facing the game.
        public static MissionStation SingleStation(CourseBuilder b, int game)
        {
            float half = MissionStation.Half + 2f;
            b.Floor("Entry band", -half, half, -6f, 0f, 0f);
            var station = Station(b, 1, Teams.White, 0f, 0f, 0f, 4f, 5f);
            station.Install(game);
            return station;
        }
    }
}
