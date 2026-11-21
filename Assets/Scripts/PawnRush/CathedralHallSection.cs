using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The audience hall, variant B: the cathedral (JY 2026-11-21: "분위기로 압도하는 그런 느낌 … 맵이 더 길거나 큰 버전은
    // 느낌이 어떨지 궁금하고"; Docs/PawnRush/PAWN_RUSH_AUDIENCE_HALL_VARIANTS_v0.2.md §2 B). A graybox to play against
    // A, C and D: the rules are the throne contest's as they stand.
    //
    // Section frame: entry (0, 0, 0) in the middle of the south door, +z north.
    //   hall        30 x 64 m inside, walls and roof 24 m, a 12 m door 10 m high; the side walls solid to 12 m, a high
    //               window every 6 m between 12 and 20 m, solid above
    //   nave        a 4 m carpet 44 m long between two rows of columns (x +-9, every 6 m from z 6 to 42, 2 m thick, 20 m)
    //   altar       a grand stair of risers x rise (2.5 m) on the south, 12 m wide, to a top x -6..6 from z 50.75 to 60;
    //               a team's stair of the same steps on each side (z 52..58); its back a 2.5 m cliff nobody climbs (marble)
    //   platform    the gold circle on the top (7 m, centre z 55.5): the finish
    //   throne      one and a half times A's across the circle's north edge, a velvet drape and canopy behind it, the crown
    //   stands      white at x -12, black at x +12, rows about z 48.5 (IThroneStage)
    //
    // Built from code when the prefab wakes (the prefab holds the root only), like the other halls.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class CathedralHallSection : MonoBehaviour, IThroneStage
    {
        public const string Id = "Final_AudienceHall_B_Cathedral";
        public const string DisplayName = "국왕 알현실 B · 대성당";
        public const string ContentName = "Content";
        public const float HalfWidth = 15f, Length = 64f, Height = 24f, DoorHalf = 6f, DoorHeight = 10f;
        public const float ColumnX = 9f, AltarFoot = 44f, AltarHalf = 6f, AltarBack = 60f, CircleZ = 55.5f;
        public const float SideStairNear = 52f, SideStairFar = 58f, StandX = 12f, StandZ = 48.5f;
        const float WindowLow = 12f, WindowHigh = 20f, WindowHalf = 1.25f, Thick = .6f;

        [SerializeField] CourseKit kit;
        [Tooltip("왕좌 발판 지름 (m)")] [SerializeField, Range(4f, 10f)] float circleDiameter = 7f;
        [Tooltip("계단 단 수: 제단 높이 = 단 수 x 한 단 높이")] [SerializeField, Range(4, 14)] int risers = 10;
        [Tooltip("한 단 높이 (m), 0.25까지는 걸어서 오른다")] [SerializeField, Range(.1f, .25f)] float rise = .25f;
        [Tooltip("디딤판 깊이 (m): 걷는 경사가 19.5도를 넘지 않게")] [SerializeField, Range(.75f, 1.2f)] float tread = .75f;
        [Tooltip("순간이동 뒤 발판이 켜지기까지 (초, 0 = 바로)")] [SerializeField, Range(0f, 5f)] float standoffSeconds = 3f;

        public float AltarHeight => risers * rise;
        public float CircleRadius => circleDiameter * .5f;
        // Where the top begins: the grand stair's last step is the top's own floor.
        public float AltarNear => AltarFoot + (risers - 1) * tread;

        public Vector3 StandRow(int team) => new Vector3(team == Teams.White ? -StandX : StandX, 0f, StandZ);
        public float StandoffSeconds => standoffSeconds;
        // The kings' board (ThroneSummonsShow, the previz JY chose 2026-11-22): 2 m squares in the nave before the grand
        // stair, z 28..44; the nave's columns stop short of it.
        public ThroneBoard Board => new ThroneBoard(new Vector3(-7f, 0f, 29f), Vector3.forward, Vector3.right, 2f);

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
            float top = AltarHeight;

            b.In("Hall", () => Hall(b));
            b.In("Altar", () => Altar(b, top));
            b.In("Board", () => Board.Inlay(b));
            b.In("Throne", () => Throne(b, top));
            b.In("Lights", () => Lights(b, top));
            HallParts.Ends(b, root.transform, HalfWidth, Length, 10f,
                           new[] { Vector3.zero, new Vector3(0f, 0f, AltarFoot), new Vector3(0f, top, CircleZ), new Vector3(0f, top, AltarBack) });

            var section = GetComponent<PawnRushSection>();
            if (section != null)
            {
                float x = HalfWidth + 1f;
                section.Describe(Id, DisplayName, PawnRushSection.SlotKind.Final, Vector3.zero, new Vector3(0f, 0f, Length), 0f, new[]
                {
                    new SectionBeat("1 신랑(긴 길)", new Vector3(-x, -2f, -1f), new Vector3(x, Height, AltarFoot)),
                    new SectionBeat("2 큰 계단 · 제단", new Vector3(-x, -2f, AltarFoot), new Vector3(x, Height, AltarBack)),
                    new SectionBeat("3 제단 뒤", new Vector3(-x, -2f, AltarBack), new Vector3(x, Height, Length + 1f)),
                });
            }
            root.SetActive(true);
        }

        // The floor, the walls with their high windows and the roof, the columns, the carpet, the team banners.
        void Hall(CourseBuilder b)
        {
            b.Floor("Floor", -HalfWidth, HalfWidth, 0f, Length, 0f);
            float w = HalfWidth + Thick;
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0f ? "W" : "E";
                float x0 = s < 0f ? -w : HalfWidth, x1 = s < 0f ? -HalfWidth : w;
                b.Wall("Wall " + side + " low", x0, x1, -1f, WindowLow, 0f, Length + Thick, false);
                b.Wall("Wall " + side + " high", x0, x1, WindowHigh, Height, 0f, Length + Thick, false);
                // Piers between the windows (a window every 6 m, 2.5 m wide).
                float z = 0f;
                for (int k = 0; k <= 10; k++)
                {
                    float middle = 3f + 6f * k;
                    b.Wall("Pier " + side + " " + k, x0, x1, WindowLow, WindowHigh, z, middle - WindowHalf, false);
                    z = middle + WindowHalf;
                }
                b.Wall("Pier " + side + " 11", x0, x1, WindowLow, WindowHigh, z, Length + Thick, false);
            }
            b.Wall("Wall N", -w, w, -1f, Height, Length, Length + Thick, false);
            b.Wall("Wall S W", -w, -DoorHalf, -1f, Height, 0f, Thick, false);
            b.Wall("Wall S E", DoorHalf, w, -1f, Height, 0f, Thick, false);
            b.Wall("Door lintel", -DoorHalf, DoorHalf, DoorHeight, Height, 0f, Thick, false);
            b.Box("Roof", new Vector3(-w, Height, 0f), new Vector3(w, Height + .6f, Length + Thick), kit.wallNoClimb, false, false);

            for (int k = 0; k < 4; k++)   // z 6..24: the board lies from z 28 (JY 2026-11-22)
            {
                float z = 6f + 6f * k;
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Cylinder($"Column {(s < 0f ? "W" : "E")} {z:0}", new Vector3(s * ColumnX, 0f, z), 2f, 20f, kit.wallNoClimb, true, true);
                    b.Box($"Capital {(s < 0f ? "W" : "E")} {z:0}", new Vector3(s * ColumnX - 1.4f, 20f, z - 1.4f), new Vector3(s * ColumnX + 1.4f, 20.8f, z + 1.4f), kit.wallNoClimb, false, false);
                }
            }
            b.Box("Carpet", new Vector3(-2f, 0f, 0f), new Vector3(2f, .015f, AltarFoot), YardKit.CarriageRed, false, false);
            HallParts.Banner(b, Teams.White, HalfWidth, 45.5f, 51.5f, 3f, 11f);
            HallParts.Banner(b, Teams.Black, HalfWidth, 45.5f, 51.5f, 3f, 11f);
        }

        // The top (marble: its back is a cliff to go round, not to climb), the grand stair and the teams' stairs.
        void Altar(CourseBuilder b, float top)
        {
            float run = (risers - 1) * tread;
            b.Wall("Altar top", -AltarHalf, AltarHalf, HallParts.Deep, top, AltarNear, AltarBack, false, true);
            HallParts.FlightNorth(b, "Grand stair", -AltarHalf, AltarHalf, AltarFoot, risers, rise, tread);
            HallParts.FlightAcross(b, "West stair", SideStairNear, SideStairFar, -AltarHalf - run, 1f, risers, rise, tread);
            HallParts.FlightAcross(b, "East stair", SideStairNear, SideStairFar, AltarHalf + run, -1f, risers, rise, tread);
        }

        // The platform, the throne across its north edge, the drape and canopy behind it, the crown over the circle.
        void Throne(CourseBuilder b, float top)
        {
            float r = CircleRadius;
            HallParts.Platform(b, new Vector3(0f, top, CircleZ), r);
            HallParts.Throne(b, new Vector3(0f, top, 58f), 1.5f);
            var velvet = Arena.Lit("M_HallVelvet", Arena.Hex("#5B3C8C"), 0f, .85f);
            b.Box("Drape", new Vector3(-4.5f, top, AltarBack - .3f), new Vector3(4.5f, top + 15f, AltarBack), velvet, false, false);
            b.Box("Canopy", new Vector3(-5f, top + 15f, 56f), new Vector3(5f, top + 15.5f, AltarBack + .5f), velvet, false, false);
            b.Box("Carpet on the top", new Vector3(-2f, top, AltarNear), new Vector3(2f, top + .012f, 58f), YardKit.CarriageRed, false, false);
            HallParts.Crown(b, new Vector3(0f, top + 7f, CircleZ), 1.8f);
        }

        // Lamps down the nave and by the altar, and the high windows' light on the platform.
        void Lights(CourseBuilder b, float top)
        {
            for (int k = 0; k < 4; k++) HallParts.Lamp(b, "Nave lamp " + (k + 1), new Vector3(0f, 14f, 8f + 10f * k), 24f, 8f);
            HallParts.Lamp(b, "Altar lamp W", new Vector3(-9f, 9f, 54f), 18f, 6f);
            HallParts.Lamp(b, "Altar lamp E", new Vector3(9f, 9f, 54f), 18f, 6f);
            HallParts.Shaft(b, "Light on the throne", new Vector3(0f, top, CircleZ), 20f, 34f, 40f);
        }
    }
}
