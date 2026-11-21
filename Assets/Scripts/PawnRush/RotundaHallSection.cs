using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The audience hall, variant C: the rotunda (Docs/PawnRush/PAWN_RUSH_AUDIENCE_HALL_VARIANTS_v0.2.md §2 C). Under a
    // dome the floor is a chessboard of 4 m squares; its middle four squares are an island 0.6 m up - the throne
    // platform - in a moat one square wide that four bridges cross ("엔드게임의 킹은 중앙으로"). A pawn that falls in is a
    // captured piece: it comes back from its own team's arch (the contest tells each runner its team's checkpoint).
    //
    // Section frame: entry (0, 0, 0) in the middle of the south door, +z north; the rotunda's middle is (0, 0, 22).
    //   ring        radius 22 m, walls 12 m (marble panels), the 12 m door in the south, an arch 6.9 m wide and 7 m high
    //               on the west (white) and the east (black), each to a closed alcove with its team's checkpoint
    //   dome        18 m on the walls (30 m in all), panels round an oculus 3 m across; its light on the island
    //   board       8 x 8 squares from x -16 to 16, z 6 to 38 (pictures on the floor)
    //   moat        x -8..8, z 14..30 round the island x -4..4, z 18..26; floor 4.5 m down, climbable sides, a pawn
    //               below -2.5 m is captured; bridges 2.5 m wide in the middle of each side
    //   platform    the island's inscribed circle (8 m): the finish; the throne at its north edge, the crown over it
    //   stands      white at x -12, black at x +12, rows about z 22 (IThroneStage)
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class RotundaHallSection : MonoBehaviour, IThroneStage
    {
        public const string Id = "Final_AudienceHall_C_Rotunda";
        public const string DisplayName = "국왕 알현실 C · 원형 돔";
        public const string ContentName = "Content";
        public const float Radius = 22f, Middle = 22f, WallHeight = 12f, DomeRise = 18f, Oculus = 1.5f;
        public const float Square = 4f, IslandHalf = 4f, MoatHalf = 8f, IslandTop = .6f, PitFloor = -4.5f, BridgeHalf = 1.25f;
        public const float DoorHalf = 6f, DoorHeight = 10f, ArchHeight = 7f, AlcoveDepth = 6f, StandX = 12f;
        const int RingPanels = 40, DomeSegments = 16, DomeTiers = 4;
        const float Thick = .8f;

        [SerializeField] CourseKit kit;
        [Tooltip("순간이동 뒤 발판이 켜지기까지 (초, 0 = 바로)")] [SerializeField, Range(0f, 5f)] float standoffSeconds = 3f;

        public Vector3 StandRow(int team) => new Vector3(team == Teams.White ? -StandX : StandX, 0f, Middle);
        public float StandoffSeconds => standoffSeconds;
        // The kings' board: the floor's own, 4 m squares (the island its middle four); white's ranks on the west.
        public ThroneBoard Board => new ThroneBoard(new Vector3(-14f, 0f, 8f), Vector3.forward, Vector3.right, Square);

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

            b.In("Floor", () => Floor(b));
            b.In("Moat", () => Moat(b));
            b.In("Ring", () => Ring(b));
            b.In("Dome", () => Dome(b));
            b.In("Alcoves", () => Alcoves(b));
            b.In("Throne", () => Throne(b));
            b.In("Lights", () => Lights(b));
            HallParts.Ends(b, root.transform, Radius, 2f * Radius, Middle - MoatHalf,
                           new[] { Vector3.zero, new Vector3(0f, 0f, Middle - MoatHalf), new Vector3(0f, IslandTop, Middle), new Vector3(0f, 0f, Middle + MoatHalf) });

            var section = GetComponent<PawnRushSection>();
            if (section != null)
            {
                float x = Radius + AlcoveDepth + 1f;
                section.Describe(Id, DisplayName, PawnRushSection.SlotKind.Final, Vector3.zero, new Vector3(0f, 0f, 2f * Radius), 0f, new[]
                {
                    new SectionBeat("3 중앙 섬", new Vector3(-IslandHalf, IslandTop - .3f, Middle - IslandHalf), new Vector3(IslandHalf, 10f, Middle + IslandHalf)),
                    new SectionBeat("2 해자 · 다리", new Vector3(-MoatHalf, PitFloor - 1f, Middle - MoatHalf), new Vector3(MoatHalf, 10f, Middle + MoatHalf)),
                    new SectionBeat("1 원형 홀", new Vector3(-x, -2f, -1f), new Vector3(x, WallHeight + DomeRise, 2f * Radius + 1f)),
                });
            }
            root.SetActive(true);
        }

        // The floor round the moat, and the board's squares drawn on it (the island's four on the island).
        void Floor(CourseBuilder b)
        {
            float lo = Middle - MoatHalf, hi = Middle + MoatHalf;
            b.Floor("Floor S", -Radius, Radius, 0f, lo, 0f);
            b.Floor("Floor N", -Radius, Radius, hi, 2f * Radius, 0f);
            b.Floor("Floor W", -Radius, -MoatHalf, lo, hi, 0f);
            b.Floor("Floor E", MoatHalf, Radius, lo, hi, 0f);
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 8; j++)
                {
                    float x0 = -16f + Square * i, z0 = Middle - 16f + Square * j;
                    float cx = x0 + Square * .5f - 0f, cz = z0 + Square * .5f - Middle;
                    bool island = Mathf.Abs(cx) < IslandHalf && Mathf.Abs(cz) < IslandHalf;
                    bool moat = Mathf.Abs(cx) < MoatHalf && Mathf.Abs(cz) < MoatHalf && !island;
                    if (moat) continue;
                    float y = island ? IslandTop : 0f;
                    var colour = (i + j) % 2 == 0 ? kit.boardDark : kit.boardLight;
                    b.Box($"Square {(char)('a' + i)}{j + 1}", new Vector3(x0, y, z0), new Vector3(x0 + Square, y + .012f, z0 + Square), colour, false, false);
                }
        }

        // The pit with its climbable sides, the island standing in it, the four bridges, and where a fall is a capture.
        void Moat(CourseBuilder b)
        {
            float lo = Middle - MoatHalf, hi = Middle + MoatHalf;
            b.Floor("Pit floor", -MoatHalf, MoatHalf, lo, hi, PitFloor);
            b.Wall("Pit side S", -MoatHalf, MoatHalf, PitFloor, -1f, lo - .6f, lo, true);
            b.Wall("Pit side N", -MoatHalf, MoatHalf, PitFloor, -1f, hi, hi + .6f, true);
            b.Wall("Pit side W", -MoatHalf - .6f, -MoatHalf, PitFloor, -1f, lo, hi, true);
            b.Wall("Pit side E", MoatHalf, MoatHalf + .6f, PitFloor, -1f, lo, hi, true);
            b.Wall("Island", -IslandHalf, IslandHalf, PitFloor, IslandTop, Middle - IslandHalf, Middle + IslandHalf, true, true);
            b.Ramp("Bridge S", -BridgeHalf, BridgeHalf, lo, Middle - IslandHalf, 0f, IslandTop, .5f);
            b.Ramp("Bridge N", -BridgeHalf, BridgeHalf, hi, Middle + IslandHalf, 0f, IslandTop, .5f);
            b.RampX("Bridge W", Middle - BridgeHalf, Middle + BridgeHalf, -MoatHalf, -IslandHalf, 0f, IslandTop, .5f);
            b.RampX("Bridge E", Middle - BridgeHalf, Middle + BridgeHalf, MoatHalf, IslandHalf, 0f, IslandTop, .5f);
            b.Trigger<KillVolume>("Moat (captured)", new Vector3(-MoatHalf, PitFloor + .3f, lo), new Vector3(MoatHalf, -2.5f, hi));
        }

        // The round wall in panels, open for the door (south) and the two arches (west, east: only a lintel there),
        // and a straight wall along z 0 either side of the door, where the corridor's walls end.
        void Ring(CourseBuilder b)
        {
            float step = 360f / RingPanels, chord = 2f * (Radius + Thick * .5f) * Mathf.Sin(step * .5f * Mathf.Deg2Rad) + .15f;
            float door = Mathf.Asin((DoorHalf + .6f) / Radius) * Mathf.Rad2Deg;
            for (int k = 0; k < RingPanels; k++)
            {
                float a = (k + .5f) * step;   // degrees from +x toward +z
                float fromSouth = Mathf.Abs(Mathf.DeltaAngle(a, 270f)), fromWest = Mathf.Abs(Mathf.DeltaAngle(a, 180f)), fromEast = Mathf.Abs(Mathf.DeltaAngle(a, 0f));
                if (fromSouth < door) continue;
                float rad = a * Mathf.Deg2Rad, r = Radius + Thick * .5f;
                var at = new Vector3(Mathf.Cos(rad) * r, 0f, Middle + Mathf.Sin(rad) * r);
                var facing = Quaternion.LookRotation(new Vector3(-Mathf.Cos(rad), 0f, -Mathf.Sin(rad)));
                bool arch = fromWest < step || fromEast < step;
                float y0 = arch ? ArchHeight : -1f;
                HallParts.Panel(b, "Ring " + k, at + Vector3.up * ((y0 + WallHeight) * .5f), facing, new Vector3(chord, WallHeight - y0, Thick), kit.wallNoClimb, true);
            }
            float w = Radius;
            b.Wall("Front W", -w, -DoorHalf, -1f, WallHeight, 0f, .6f, false);
            b.Wall("Front E", DoorHalf, w, -1f, WallHeight, 0f, .6f, false);
            b.Wall("Door lintel", -DoorHalf, DoorHalf, DoorHeight, WallHeight, 0f, .6f, false);
            b.Wall("Door jamb W", -DoorHalf - .6f, -DoorHalf, -1f, WallHeight, .6f, 2.2f, false);
            b.Wall("Door jamb E", DoorHalf, DoorHalf + .6f, -1f, WallHeight, .6f, 2.2f, false);
        }

        // Panels on an ellipsoid from the ring's top to the oculus (looks only).
        void Dome(CourseBuilder b)
        {
            float a = Radius + Thick * .5f;
            float tMax = Mathf.Acos(Oculus / a);
            for (int tier = 0; tier < DomeTiers; tier++)
            {
                float t0 = tMax * tier / DomeTiers, t1 = tMax * (tier + 1) / DomeTiers, tm = (t0 + t1) * .5f;
                for (int s = 0; s < DomeSegments; s++)
                {
                    float p0 = Mathf.PI * 2f * s / DomeSegments, p1 = Mathf.PI * 2f * (s + 1) / DomeSegments, pm = (p0 + p1) * .5f;
                    Vector3 left = Dome(a, tm, p0), right = Dome(a, tm, p1), low = Dome(a, t0, pm), high = Dome(a, t1, pm);
                    var across = right - left;
                    var up = high - low;
                    var normal = Vector3.Cross(across, up).normalized;
                    var center = (left + right + low + high) * .25f;
                    // As wide as the panel's lower (wider) edge, and over its neighbours above and below: flat panels on a
                    // curved dome otherwise leave triangles of sky at their corners.
                    float wide = (Dome(a, t0, p1) - Dome(a, t0, p0)).magnitude;
                    HallParts.Panel(b, $"Dome {tier}.{s}", center, Quaternion.LookRotation(normal, up.normalized),
                                    new Vector3(wide * 1.06f, up.magnitude * 1.15f, .5f), kit.wallNoClimb, false);
                }
            }
        }

        static Vector3 Dome(float a, float t, float p) =>
            new Vector3(a * Mathf.Cos(t) * Mathf.Cos(p), WallHeight + DomeRise * Mathf.Sin(t), Middle + a * Mathf.Cos(t) * Mathf.Sin(p));

        // Behind each arch a closed alcove with its team's checkpoint (where a captured piece comes back) and banner.
        void Alcoves(CourseBuilder b)
        {
            float half = 3.6f;
            foreach (int team in new[] { Teams.White, Teams.Black })
            {
                float s = team == Teams.White ? -1f : 1f;
                string side = team == Teams.White ? "W" : "E";
                float inner = s * Radius, outer = s * (Radius + AlcoveDepth);
                float x0 = Mathf.Min(inner, outer), x1 = Mathf.Max(inner, outer);
                b.Floor("Alcove floor " + side, x0, x1, Middle - half, Middle + half, 0f);
                b.Wall("Alcove back " + side, s < 0f ? x0 - .6f : x1, s < 0f ? x0 : x1 + .6f, -1f, ArchHeight + .6f, Middle - half - .6f, Middle + half + .6f, false);
                b.Wall("Alcove side S " + side, x0, x1, -1f, ArchHeight + .6f, Middle - half - .6f, Middle - half, false);
                b.Wall("Alcove side N " + side, x0, x1, -1f, ArchHeight + .6f, Middle + half, Middle + half + .6f, false);
                b.Box("Alcove roof " + side, new Vector3(x0, ArchHeight, Middle - half - .6f), new Vector3(x1, ArchHeight + .6f, Middle + half + .6f), kit.wallNoClimb);
                b.Checkpoint(team == Teams.White ? 1 : 2, new Vector3(s * (Radius + 3f), 0f, Middle), s < 0f ? 90f : -90f, 6f, team);
                float bx = s * (Radius + AlcoveDepth) - (s < 0f ? 0f : .08f);
                b.Box("Banner " + side, new Vector3(bx, 1f, Middle - 2f), new Vector3(bx + .08f, ArchHeight - .5f, Middle + 2f), kit.Team(team), false, false);
            }
        }

        // The platform (the island's inscribed circle), the throne at the island's north edge, the crown over it.
        void Throne(CourseBuilder b)
        {
            HallParts.Platform(b, new Vector3(0f, IslandTop, Middle), IslandHalf);
            HallParts.Throne(b, new Vector3(0f, IslandTop, Middle + IslandHalf - 1.4f), 1f);
            HallParts.Crown(b, new Vector3(0f, IslandTop + 7f, Middle), 1.6f);
        }

        // The oculus' light on the island, and four lamps round the wall.
        void Lights(CourseBuilder b)
        {
            HallParts.Shaft(b, "Oculus light", new Vector3(0f, IslandTop, Middle), WallHeight + DomeRise - IslandTop, 24f, 45f);
            HallParts.Lamp(b, "Lamp W", new Vector3(-17f, 8f, Middle), 18f, 6f);
            HallParts.Lamp(b, "Lamp E", new Vector3(17f, 8f, Middle), 18f, 6f);
            HallParts.Lamp(b, "Lamp N", new Vector3(0f, 8f, Middle + 17f), 18f, 6f);
            HallParts.Lamp(b, "Lamp S", new Vector3(0f, 8f, Middle - 17f), 18f, 6f);
        }
    }
}
