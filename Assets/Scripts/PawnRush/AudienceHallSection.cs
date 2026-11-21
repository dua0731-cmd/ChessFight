using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The round's ending section, the king's audience hall, as a graybox (JY, 2026-11-21: "내가 만들려는 엔딩 구간은
    // 엔드게임 구간 바로 다음에 나오는거야. 알현실을 모티브로 한 공간에, 일정 시간동안 서있는 팀이 이기는 왕좌 발판을 놓고
    // 양 팀 기물들이 서로 밀치고 치고받는."). It takes the temporary floor's place in the last slot; the rules are the throne
    // contest's (R707) as they stand. What the space is for: a pawn shoved off the throne platform has to climb back, so a
    // shove costs the team time (Docs/PawnRush/PAWN_RUSH_AUDIENCE_HALL_DESIGN_v0.1.md, Mockups/AudienceHall_v0.1.svg).
    //
    // Section frame: entry (0, 0, 0) in the middle of the south wall's door, +z north toward the throne.
    //   hall      24 x 22 m inside (x -12..12, z 0..22), walls 8 m, a 12 m door in the south wall (the corridor's width)
    //   dais      top 8 x 8 m (x -4..4, z 11..19) at risers x rise (1.25 m): flights on the south, west and east, a sheer
    //             climbable back over a 3 m way behind it, a plinth with a statue in each front corner. The treads are
    //             pictures walked on a slope under their noses, GrandStairHallBuilder's way (real risers catch a
    //             ragdoll's feet): 0.25 m on 0.75 m is 18.4 degrees, under the course's 19.5.
    //   platform  the gold circle on the top (6 m, centre z 15): the finish, a FinishZone box (CrownFinish takes its
    //             narrower half as the radius and its floor as the middle)
    //   throne    across the circle's north edge: something to hold on to and to climb; a velvet canopy and the crown
    //   columns   four a side at x +-10.5 (z 3, 8, 13, 18)
    //   stands    the throne contest stands white at x -9 and black at x +9 (IThroneStage), the platform off for a while
    //
    //   Final_AudienceHall (AudienceHallSection, PawnRushSection)
    //     Content   Hall, Dais, Throne, CP0, the finish, the kill volume, ProgressPath
    //
    // Built from code when the prefab wakes, like the temporary ending (the prefab holds the root only). The serialized
    // numbers are the draft's starting values: change them on the prefab to try others (every PC builds that prefab).
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class AudienceHallSection : MonoBehaviour, IThroneStage
    {
        public const string Id = "Final_AudienceHall";
        public const string DisplayName = "국왕 알현실";
        public const string ContentName = "Content";
        public const float HalfWidth = 12f, Length = 22f, WallHeight = 8f, WallThick = .5f, DoorHalf = 6f, DoorHeight = 6f;
        public const float TopHalf = 4f, TopNear = 11f, TopFar = 19f, CircleZ = 15f;
        // Where the contest stands each team's row, across the hall from the circle's middle.
        public const float StandX = 9f;
        public const float ColumnX = 10.5f, ColumnDiameter = 1.2f;
        // Step pictures reach this far under the floor's top.
        const float Deep = -.5f;

        [SerializeField] CourseKit kit;
        [Tooltip("왕좌 발판 지름 (m), 단상 윗면 8 m 안")] [SerializeField, Range(4f, 8f)] float circleDiameter = 6f;
        [Tooltip("계단 단 수: 단상 높이 = 단 수 x 한 단 높이")] [SerializeField, Range(2, 8)] int risers = 5;
        [Tooltip("한 단 높이 (m), 0.25까지는 걸어서 오른다")] [SerializeField, Range(.1f, .25f)] float rise = .25f;
        [Tooltip("디딤판 깊이 (m): 걷는 경사가 19.5도를 넘지 않게")] [SerializeField, Range(.5f, 1.2f)] float tread = .75f;
        [Tooltip("남쪽 큰 계단 (끄면 남쪽도 절벽)")] [SerializeField] bool southStairs = true;
        [Tooltip("순간이동 뒤 발판이 켜지기까지 (초, 0 = 바로)")] [SerializeField, Range(0f, 5f)] float standoffSeconds = 3f;

        public float DaisHeight => risers * rise;
        public float CircleRadius => circleDiameter * .5f;
        // How far the flights reach out past the top's edges.
        public float Run => (risers - 1) * tread;

        // IThroneStage: each team's row StandX from the circle's middle, so this far from its edge.
        public float StandGap => StandX - CircleRadius;
        public float StandoffSeconds => standoffSeconds;

        void Awake()
        {
            if (transform.Find(ContentName) == null) Build();
        }

        void Build()
        {
            var root = new GameObject(ContentName);
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            var b = new CourseBuilder(kit, root.transform);
            float top = DaisHeight, run = Run;
            float footZ = TopNear - run, footX = TopHalf + run;

            b.In("Hall", () => Hall(b, footZ));
            b.In("Dais", () => Dais(b, top, footZ, footX));
            b.In("Throne", () => Throne(b, top));

            var cp = b.Checkpoint(0, new Vector3(0f, 0f, 2f), 0f, 2f * DoorHalf);
            CourseBuilder.CoverFloor(cp, -HalfWidth, HalfWidth, 0f, footZ);
            b.Trigger<KillVolume>("Kill volume (far below)", new Vector3(-30f, -42f, -10f), new Vector3(30f, -40f, Length + 10f));

            var route = new[] { Vector3.zero, new Vector3(0f, 0f, footZ), new Vector3(0f, top, CircleZ), new Vector3(0f, top, TopFar) };
            var path = new GameObject("ProgressPath");
            path.transform.SetParent(root.transform, false);
            path.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), new[] { "1", "1", "1", "1" });

            var section = GetComponent<PawnRushSection>();
            if (section != null)
            {
                float x = HalfWidth + 1f;
                section.Describe(Id, DisplayName, PawnRushSection.SlotKind.Final, Vector3.zero, new Vector3(0f, 0f, Length), 0f, new[]
                {
                    new SectionBeat("1 알현실 앞", new Vector3(-x, -2f, -1f), new Vector3(x, 9f, footZ)),
                    new SectionBeat("2 단상 · 왕좌 발판", new Vector3(-x, -2f, footZ), new Vector3(x, 9f, TopFar)),
                    new SectionBeat("3 왕좌 뒤", new Vector3(-x, -2f, TopFar), new Vector3(x, 9f, Length + 1f)),
                });
            }
            root.SetActive(true);
        }

        // The floor, the walls round it with the door, the columns, and what is only seen: the team banners, the carpet.
        void Hall(CourseBuilder b, float footZ)
        {
            b.Floor("Floor", -HalfWidth, HalfWidth, 0f, Length, 0f);
            float w = HalfWidth + WallThick;
            b.Wall("Wall W", -w, -HalfWidth, -1f, WallHeight, 0f, Length + WallThick, false);
            b.Wall("Wall E", HalfWidth, w, -1f, WallHeight, 0f, Length + WallThick, false);
            b.Wall("Wall N", -w, w, -1f, WallHeight, Length, Length + WallThick, false);
            // The south wall starts at the corridor's end (z 0) and leaves its 12 m open.
            b.Wall("Wall S W", -w, -DoorHalf, -1f, WallHeight, 0f, WallThick, false);
            b.Wall("Wall S E", DoorHalf, w, -1f, WallHeight, 0f, WallThick, false);
            b.Wall("Door lintel", -DoorHalf, DoorHalf, DoorHeight, WallHeight, 0f, WallThick, false);
            foreach (float s in new[] { -1f, 1f })
                foreach (float z in new[] { 3f, 8f, 13f, 18f })
                    b.Cylinder($"Column {(s < 0f ? "W" : "E")} {z:0}", new Vector3(s * ColumnX, 0f, z), ColumnDiameter, WallHeight, kit.wallNoClimb, true, true);
            // Each team's banner on its own side wall (looks only; to fill with the team's time later).
            b.Box("Banner W (white)", new Vector3(-HalfWidth, 2f, 13.5f), new Vector3(-HalfWidth + .08f, 7.5f, 16.5f), kit.trimWhite, false, false);
            b.Box("Banner E (black)", new Vector3(HalfWidth - .08f, 2f, 13.5f), new Vector3(HalfWidth, 7.5f, 16.5f), kit.trimBlack, false, false);
            b.Box("Carpet", new Vector3(-1.5f, 0f, 0f), new Vector3(1.5f, .015f, footZ), YardKit.CarriageRed, false, false);
        }

        // The top (climbable: its sheer back is climbed by the lip), the flights, the front corners' plinths and statues.
        void Dais(CourseBuilder b, float top, float footZ, float footX)
        {
            b.Wall("Top", -TopHalf, TopHalf, Deep, top, TopNear, TopFar, true, true);
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "SW" : "SE";
                float x0 = s < 0f ? -footX : TopHalf, x1 = s < 0f ? -TopHalf : footX;
                b.Wall("Plinth " + side, x0, x1, Deep, top, footZ, TopNear, false, true);
                var foot = new Vector3((x0 + x1) * .5f, top, (footZ + TopNear) * .5f);
                float d = Mathf.Min(1.2f, (TopNear - footZ) * .5f);
                var statue = s < 0f ? kit.trimWhite : kit.trimBlack;
                b.Cylinder("Statue " + side, foot, d, 2.4f, statue, true, true);
                b.Cylinder("Statue " + side + " top", foot + Vector3.up * 2.4f, d * 1.3f, .45f, statue, true, true);
            }
            if (southStairs)
            {
                for (int k = 1; k < risers; k++)
                    Picture(b, "South step " + k, new Vector3(-TopHalf, Deep, footZ + (k - 1) * tread), new Vector3(TopHalf, k * rise, footZ + k * tread));
                Walk(b.Slope("South stairs (walking slope)", -TopHalf, TopHalf, footZ - tread * .5f, footZ + (risers - .5f) * tread,
                             0f, top, .6f, 0f, kit.floorChecker, true));
            }
            else b.Wall("Front", -TopHalf, TopHalf, Deep, top, footZ, TopNear, true, true);
            for (int k = 1; k < risers; k++)
            {
                Picture(b, "West step " + k, new Vector3(-footX + (k - 1) * tread, Deep, TopNear), new Vector3(-footX + k * tread, k * rise, TopFar));
                Picture(b, "East step " + k, new Vector3(footX - k * tread, Deep, TopNear), new Vector3(footX - (k - 1) * tread, k * rise, TopFar));
            }
            Walk(b.SlopeX("West stairs (walking slope)", TopNear, TopFar, -footX - tread * .5f, -footX + (risers - .5f) * tread,
                          0f, top, .6f, 0f, kit.floorChecker, true));
            Walk(b.SlopeX("East stairs (walking slope)", TopNear, TopFar, footX + tread * .5f, footX - (risers - .5f) * tread,
                          0f, top, .6f, 0f, kit.floorChecker, true));
        }

        // The platform (the finish and its gold circle), the carpet on to it, the throne, its canopy and the crown.
        void Throne(CourseBuilder b, float top)
        {
            float r = CircleRadius;
            b.Box("Carpet on the top", new Vector3(-1.5f, top, TopNear), new Vector3(1.5f, top + .012f, 17.6f), YardKit.CarriageRed, false, false);
            YardKit.Cylinder(b.parent, "Finish circle", new Vector3(0f, top + .02f, CircleZ), Vector3.up, r * 2f, .03f, YardKit.Gold, YardKit.Solid.None);
            b.Trigger<FinishZone>("Finish (goal)", new Vector3(-r, top, CircleZ - r), new Vector3(r, top + 3.4f, CircleZ + r)).Configure(true);
            // The seat across the circle's north edge: hands hold it, feet climb it.
            b.Box("Seat", new Vector3(-1f, top, 17.6f), new Vector3(1f, top + .5f, 18.8f), kit.rankGold);
            b.Box("Back", new Vector3(-1.25f, top, 18.5f), new Vector3(1.25f, top + 2.6f, 18.8f), kit.rankGold);
            b.Box("Arm W", new Vector3(-1.25f, top, 17.6f), new Vector3(-1f, top + .9f, 18.5f), kit.rankGold);
            b.Box("Arm E", new Vector3(1f, top, 17.6f), new Vector3(1.25f, top + .9f, 18.5f), kit.rankGold);
            var velvet = Arena.Lit("M_HallVelvet", Arena.Hex("#5B3C8C"), 0f, .85f);
            b.Box("Canopy", new Vector3(-1.8f, top + 3.6f, 17.2f), new Vector3(1.8f, top + 3.8f, Length), velvet, false, false);
            // The crown over the circle, seen from anywhere; no collisions.
            var crown = new Vector3(0f, top + 4.2f, CircleZ);
            b.Cylinder("Crown band", crown, 1.6f, .5f, kit.rankGold, false);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var at = crown + new Vector3(Mathf.Sin(a) * .7f, .5f, Mathf.Cos(a) * .7f);
                b.Box("Crown point " + (i + 1), at - new Vector3(.12f, 0f, .12f), at + new Vector3(.12f, .45f, .12f), kit.rankGold, false, false);
            }
        }

        // A step's picture: seen, never touched.
        static GameObject Picture(CourseBuilder b, string name, Vector3 a, Vector3 c) =>
            b.Box(name, a, c, b.kit.wallNoClimb, false, false);

        // A flight's walking slope: walked on, not seen (GrandStairHallBuilder.Walk).
        static GameObject Walk(GameObject slope)
        {
            var renderer = slope.GetComponent<MeshRenderer>();
            if (renderer != null) DestroyImmediate(renderer);
            var filter = slope.GetComponent<MeshFilter>();
            if (filter != null) DestroyImmediate(filter);
            slope.AddComponent<CourseFloor>().sloped = true;
            return slope;
        }
    }
}
