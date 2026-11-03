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
        // R263 (11-01): 승규 님's royal water garden (G) stands next to the library (E).
        // R297 (11-02): main's grand stair hall (H, Mid_2-1_GrandStairHall) stands next to the garden.
        // R323 (11-03): E's label is the war room (승규 님: the library conveyor course is now called 작전 회의실).
        public const string EndgameMachine = "KKND1236_LENOVO";
        public static readonly bool ShowsEndgame = true;

        public static readonly string[] ZoneSections = ShowsEndgame ? new[] { "E", "G", "H" } : new[] { "A", "B", "C", "T" };
        public static readonly string[] ZoneSectionNames = ShowsEndgame
            ? new[] { "E 작전 회의실", "G 왕실 수로 정원", "H 대계단 현관홀" }
            : new[] { "A 구간 (출발 → 광장 1)", "B 구간 (광장 1 → 광장 2)", "C 구간 (광장 2 → 탑)", "T 탑" };
        static readonly Vector3[] ZoneStarts = ShowsEndgame
            ? new[] { V(0f, 0f, -16.5f), V(0f, 0f, -57f), V(0f, 0f, -6f) }
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
                case "E": LibraryReplacement(b); break;
                case "F": FinalApproach(b); break;
                case "G": RoyalGarden(b); break;
                case "H": GrandStairHallZone(b); break;
            }
        }

        // The grand stair hall prefab (its own section frame: porch threshold at z 0, entered at z -12), built from
        // code into this scene's instance; the prefab asset is not baked.
        static void GrandStairHallZone(CourseBuilder b)
        {
            var prefab = (b.kit as Course01Kit)?.grandStairHall;
            if (prefab == null) throw new System.Exception("Course01Kit.grandStairHall이 비어 있습니다");
            var go = CourseBuilder.Spawn(prefab, b.parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var hall = go.GetComponent<GrandStairHall>();
            if (!hall.HasContent) GrandStairHallBuilder.Build(hall);
            GrandStairHallModel(b, go.transform);
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
