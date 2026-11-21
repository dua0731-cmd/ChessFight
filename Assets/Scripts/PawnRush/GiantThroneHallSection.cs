using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The audience hall, variant D: the giant throne (Docs/PawnRush/PAWN_RUSH_AUDIENCE_HALL_VARIANTS_v0.2.md §2 D). One
    // throne 14 m high fills the hall and its cushion is the throne platform; next to it the pieces look like toys.
    // Up to the cushion: book steps to a footstool, then climb the cushion's front by its lip, or walk a velvet sash up
    // to an armrest and drop in; two tassels hang from the cushion's front as ropes (RopeLine).
    //
    // Section frame: entry (0, 0, 0) in the middle of the south door, +z north.
    //   hall        34 x 44 m inside, walls 32 m, a 12 m door 12 m high, a roof open over the throne (x -7..7, z 22..40)
    //   throne      seat (the cushion) x -5..5, z 29..36, 3 m up; armrests x 5..6.5 either side, 4.5 m; back z 36..37.5,
    //               14 m; all gold, all climbable
    //   way up      books 0.5 / 1.0 / 1.5 m (z 21.5..24.5), footstool 1.5 m (z 24.5..29, against the cushion), a sash
    //               1.5 m wide from the floor at z 15 to each armrest's south end (17.8 degrees)
    //   platform    the gold circle on the cushion (6 m, centre z 32.5): the finish
    //   stands      white at x -12, black at x +12, rows about z 29 (IThroneStage)
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class GiantThroneHallSection : MonoBehaviour, IThroneStage
    {
        public const string Id = "Final_AudienceHall_D_GiantThrone";
        public const string DisplayName = "국왕 알현실 D · 거인의 왕좌";
        public const string ContentName = "Content";
        public const float HalfWidth = 17f, Length = 44f, Height = 32f, DoorHalf = 6f, DoorHeight = 12f;
        public const float SeatHalf = 5f, SeatNear = 29f, SeatFar = 36f, SeatTop = 3f, ArmOuter = 6.5f, ArmTop = 4.5f, BackFar = 37.5f, BackTop = 14f;
        public const float StoolHalf = 4f, StoolNear = 24.5f, StoolTop = 1.5f, SashFoot = 15f, CircleZ = 32.5f;
        public const float StandX = 12f, StandZ = 29f;

        [SerializeField] CourseKit kit;
        [Tooltip("왕좌 발판 지름 (m), 방석 10 x 7 m 안")] [SerializeField, Range(4f, 7f)] float circleDiameter = 6f;
        [Tooltip("순간이동 뒤 발판이 켜지기까지 (초, 0 = 바로)")] [SerializeField, Range(0f, 5f)] float standoffSeconds = 3f;

        public float CircleRadius => circleDiameter * .5f;
        public Vector3 StandRow(int team) => new Vector3(team == Teams.White ? -StandX : StandX, 0f, StandZ);
        public float StandoffSeconds => standoffSeconds;
        // The kings' board (ThroneSummonsShow): 1.75 m squares inside the door, z 0.5..14.5, before the way up.
        public ThroneBoard Board => new ThroneBoard(new Vector3(-6.125f, 0f, 1.375f), Vector3.forward, Vector3.right, 1.75f);

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

            b.In("Hall", () => Hall(b));
            b.In("Throne", () => Throne(b));
            b.In("Board", () => Board.Inlay(b));
            b.In("Way up", () => WayUp(b));
            b.In("Lights", () => Lights(b));
            HallParts.Ends(b, root.transform, HalfWidth, Length, 12f,
                           new[] { Vector3.zero, new Vector3(0f, 0f, 21f), new Vector3(0f, StoolTop, 26.5f), new Vector3(0f, SeatTop, CircleZ) });

            var section = GetComponent<PawnRushSection>();
            if (section != null)
            {
                float x = HalfWidth + 1f;
                section.Describe(Id, DisplayName, PawnRushSection.SlotKind.Final, Vector3.zero, new Vector3(0f, 0f, Length), 0f, new[]
                {
                    new SectionBeat("3 왕좌 위", new Vector3(-ArmOuter, SeatTop - .5f, SeatNear), new Vector3(ArmOuter, BackTop + 2f, BackFar)),
                    new SectionBeat("2 왕좌 오르는 길", new Vector3(-9f, -2f, SashFoot), new Vector3(9f, 8f, SeatNear)),
                    new SectionBeat("1 거인의 홀", new Vector3(-x, -2f, -1f), new Vector3(x, Height, Length + 1f)),
                });
            }
            root.SetActive(true);
        }

        // The floor, the walls, the roof open over the throne, pilasters, giant candlesticks, the carpet, the banners.
        void Hall(CourseBuilder b)
        {
            b.Floor("Floor", -HalfWidth, HalfWidth, 0f, Length, 0f);
            HallParts.Walls(b, HalfWidth, Length, Height, DoorHalf, DoorHeight);
            float w = HalfWidth + .6f;
            b.Box("Roof S", new Vector3(-w, Height, 0f), new Vector3(w, Height + .6f, 22f), kit.wallNoClimb, false, false);
            b.Box("Roof N", new Vector3(-w, Height, 40f), new Vector3(w, Height + .6f, Length + .6f), kit.wallNoClimb, false, false);
            b.Box("Roof W", new Vector3(-w, Height, 22f), new Vector3(-7f, Height + .6f, 40f), kit.wallNoClimb, false, false);
            b.Box("Roof E", new Vector3(7f, Height, 22f), new Vector3(w, Height + .6f, 40f), kit.wallNoClimb, false, false);
            for (int k = 0; k < 5; k++)
                foreach (float s in new[] { -1f, 1f })
                    b.Cylinder($"Pilaster {(s < 0f ? "W" : "E")} {k + 1}", new Vector3(s * 15.6f, 0f, 8f + 8f * k), 2f, 26f, kit.wallNoClimb, true, true);
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "W" : "E";
                b.Cylinder("Candlestick foot " + side, new Vector3(s * 10f, 0f, 36f), 2.4f, .6f, kit.rankGold, true, true);
                b.Cylinder("Candlestick " + side, new Vector3(s * 10f, .6f, 36f), .9f, 8f, kit.rankGold, true, true);
                b.Cylinder("Candle " + side, new Vector3(s * 10f, 8.6f, 36f), .7f, 1.6f, kit.trimWhite, true, true);
            }
            b.Box("Carpet", new Vector3(-2f, 0f, 0f), new Vector3(2f, .015f, 21.5f), YardKit.CarriageRed, false, false);
            HallParts.Banner(b, Teams.White, HalfWidth, 25f, 33f, 3f, 20f);
            HallParts.Banner(b, Teams.Black, HalfWidth, 25f, 33f, 3f, 20f);
        }

        // The throne: the cushion (the platform), the armrests, the back, the front legs, the crown on the back's top.
        void Throne(CourseBuilder b)
        {
            var gold = kit.rankGold;
            b.Box("Seat (cushion)", new Vector3(-SeatHalf, 0f, SeatNear), new Vector3(SeatHalf, SeatTop, SeatFar), gold).AddComponent<CourseFloor>();
            b.Box("Arm W", new Vector3(-ArmOuter, 0f, SeatNear), new Vector3(-SeatHalf, ArmTop, SeatFar), gold).AddComponent<CourseFloor>();
            b.Box("Arm E", new Vector3(SeatHalf, 0f, SeatNear), new Vector3(ArmOuter, ArmTop, SeatFar), gold).AddComponent<CourseFloor>();
            b.Box("Back", new Vector3(-ArmOuter, 0f, SeatFar), new Vector3(ArmOuter, BackTop, BackFar), gold);
            b.Box("Leg W", new Vector3(-ArmOuter, 0f, SeatNear - .8f), new Vector3(-ArmOuter + 1f, SeatTop, SeatNear), gold);
            b.Box("Leg E", new Vector3(ArmOuter - 1f, 0f, SeatNear - .8f), new Vector3(ArmOuter, SeatTop, SeatNear), gold);
            HallParts.Platform(b, new Vector3(0f, SeatTop, CircleZ), CircleRadius);
            HallParts.Crown(b, new Vector3(0f, BackTop, (SeatFar + BackFar) * .5f), 3f);
        }

        // Books to the footstool, the footstool against the cushion, a sash up to each armrest, two tassel ropes.
        void WayUp(CourseBuilder b)
        {
            var covers = new[] { YardKit.CarriageBlue, YardKit.CarriageGreen, YardKit.CarriageRed };
            for (int k = 0; k < 3; k++)
            {
                float z0 = StoolNear - 3f + k, h = .5f * (k + 1);
                b.Box("Book " + (k + 1), new Vector3(-3f, 0f, z0), new Vector3(3f, h, z0 + 1f), covers[k]).AddComponent<CourseFloor>();
            }
            b.Box("Footstool", new Vector3(-StoolHalf, 0f, StoolNear), new Vector3(StoolHalf, StoolTop, SeatNear), YardKit.WoodDark).AddComponent<CourseFloor>();
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "W" : "E";
                float x0 = s < 0f ? -ArmOuter : SeatHalf, x1 = s < 0f ? -SeatHalf : ArmOuter;
                var sash = b.Ramp("Sash " + side, x0, x1, SashFoot, SeatNear, 0f, ArmTop, .6f);
                sash.GetComponent<MeshRenderer>().sharedMaterial = YardKit.CarriageRed;
                var tassel = new GameObject("Tassel " + side);
                tassel.transform.SetParent(b.parent, false);
                tassel.transform.localPosition = new Vector3(s * 2.5f, SeatTop - .05f, SeatNear - .15f);
                tassel.transform.localRotation = Quaternion.LookRotation(Vector3.back);
                tassel.AddComponent<RopeLine>().Configure(SeatTop - .4f, 4f, 6f, s < 0f ? 0f : 3f, .12f, kit.rankGold);
            }
        }

        // The light through the roof's opening on the cushion, the candles' flames, lamps down the hall.
        void Lights(CourseBuilder b)
        {
            HallParts.Shaft(b, "Light on the cushion", new Vector3(0f, SeatTop, CircleZ), Height - SeatTop, 26f, 45f);
            HallParts.Lamp(b, "Candle flame W", new Vector3(-10f, 10.6f, 36f), 16f, 6f);
            HallParts.Lamp(b, "Candle flame E", new Vector3(10f, 10.6f, 36f), 16f, 6f);
            HallParts.Lamp(b, "Hall lamp S", new Vector3(0f, 16f, 8f), 24f, 8f);
            HallParts.Lamp(b, "Hall lamp M", new Vector3(0f, 16f, 20f), 24f, 8f);
        }
    }
}
