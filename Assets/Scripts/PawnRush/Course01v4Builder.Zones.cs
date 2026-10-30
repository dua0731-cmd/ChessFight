using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 승규 님's test scene (seoungtest, 10-30): course 01's sections and its mini-games built one at a time, each in a
    // group of its own, so the test scene can stand them side by side instead of chained (SeoungTestCourse).
    public static partial class Course01v4Builder
    {
        // The sections the test scene stands in a row, in running order: id, name, and where a pawn starts it (design
        // doc world coordinates, facing north; checked against the floor when the zone is built). Since 10-30 (R243)
        // 승규 님's endgame (E, F) in place of course 01's A, B, C and tower (T), but only on 승규 님's laptop (R257):
        // every other PC that opens the branch gets course 01's sections, so the endgame map is not shown there.
        public const string EndgameMachine = "KKND1236_LENOVO";
        public static readonly bool ShowsEndgame =
            string.Equals(System.Environment.MachineName, EndgameMachine, System.StringComparison.OrdinalIgnoreCase);

        public static readonly string[] ZoneSections = ShowsEndgame ? new[] { "E", "F" } : new[] { "A", "B", "C", "T" };
        public static readonly string[] ZoneSectionNames = ShowsEndgame
            ? new[] { "E 엔드게임 · 전략회의실 (S1–S4)", "F 최종 코스 · 알현실 · 왕좌 (원 8.5 m)" }
            : new[] { "A 구간 (출발 → 광장 1)", "B 구간 (광장 1 → 광장 2)", "C 구간 (광장 2 → 탑)", "T 탑" };
        static readonly Vector3[] ZoneStarts = ShowsEndgame
            ? new[] { V(0f, 0f, 3f), V(0f, 0f, 3f) }
            : new[] { V(0f, 0f, 2f), V(46f, 0f, 105f), V(-3f, 3f, 165f), V(-3f, 6f, 207f) };

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
                case "E": WarRoom(b); break;
                case "F": FinalApproach(b); break;
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
