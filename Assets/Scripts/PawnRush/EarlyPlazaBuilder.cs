using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The early mini-game section's plaza (R153, Docs/KingRush/PAWN_RUSH_EARLY_PLAZA.md §2), built from code into
    // EarlyPlaza's Content. Section frame: origin on the entry threshold's middle (where the obstacle section before
    // ends its 12 m band), +z north, +x east, y = the plaza floor. Entered at z 0 and left at (0, 0, 36), both 12 m wide.
    //
    //   Early_C_MoatDrawbridge (EarlyPlaza, PawnRushSection)
    //     Content
    //       Forecourt      the wall either side of the arch (|x| 7..23, z -6.4..-5.6, 7 m), the lawns and their hedge, the
    //                      game's props on the lawns, the stand-in approach (on only when no section comes before)
    //       Plaza          the front walk (z 0..4, the whole 44 m), the aisle (x ±2), the side walls, the split marks
    //       Station_White, Station_Black
    //                      MissionStation at (∓12, 0, 4): team gate, team barrier, gate passage, game socket (the game is
    //                      installed when Play starts)
    //       Facade         the north block z 24..30 (12 m, the gatehouse 16 m), the closed main gate, the two door passages
    //       Vestibule      z 30..36, where the passages meet; the exit x ±6
    //       Moat           (C) the moat across the aisle, its rail, the raised show bridge before the main gate
    //       Checkpoints    CP0 on the front walk, CP1 in the vestibule, the kill volume far below
    //       ProgressPath   white by the west station, black by the east, the same length
    //
    // The design sheet put the stations straight on the threshold, but a station's ramp, flags and sign stand 1.6 m in
    // front of it (Arena.Dress), so the plaza keeps course 01's 4 m front walk (the spec's 44 x 25 m). Everything solid
    // is unclimbable (NoClimbSurface). Built inactive and switched on at the end, so nothing wakes up half placed.
    public static class EarlyPlazaBuilder
    {
        public const float TeamX = 12f, Side = 22f, GateWidth = 4f, Front = 4f;
        public const float North0 = Front + MissionStation.Size, North1 = North0 + 6f, VestibuleEnd = North1 + 6f;
        public const float WallSouth = -6.4f, WallNorth = -5.6f, Deep = -3f;
        public static readonly Vector3 Entry = Vector3.zero, Exit = new Vector3(0f, 0f, VestibuleEnd);

        public static string Id(int game) =>
            game == PawnRushMissions.Drawbridge ? "Early_C_MoatDrawbridge"
            : "Early_" + (game >= 0 && game < PawnRushMissions.Count ? PawnRushMissions.Ids[game] : "None");

        public static string DisplayName(int game) => "정문 앞 광장 · " + PlaceName(game);

        // The zone plan's places for the early games (mini-game zones v0.1 §3).
        static string PlaceName(int game)
        {
            switch (game)
            {
                case PawnRushMissions.Drawbridge: return "C 해자 도개교";
                case PawnRushMissions.Capstan: return "D 쇠살문 누각";
                case PawnRushMissions.Paint: return "A 의장 광장";
                default: return MissionPicker.Name(game);
            }
        }

        public static GameObject Build(EarlyPlaza plaza)
        {
            var old = plaza.transform.Find(EarlyPlaza.ContentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(EarlyPlaza.ContentName);
            root.SetActive(false);
            root.transform.SetParent(plaza.transform, false);
            var b = new CourseBuilder(plaza.Kit, root.transform);
            int game = plaza.Game;
            bool moat = game == PawnRushMissions.Drawbridge;

            GameObject standIn = null;
            b.In("Forecourt", () => standIn = Forecourt(b, game));
            b.In("Plaza", () => Plaza(b, moat));
            var white = Station(b, Teams.White, moat);
            var black = Station(b, Teams.Black, moat);
            b.In("Facade", () => Facade(b));
            b.In("Vestibule", () => Vestibule(b));
            if (moat) b.In("Moat", () => MoatAcross(b));
            b.In("Checkpoints", () => Checkpoints(b));
            Path(b);

            plaza.Configure(white, black, standIn);
            var section = plaza.GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(Id(game), DisplayName(game), PawnRushSection.SlotKind.MiniGame, Entry, Exit, 0f, Beats(game), Front - 1f);
            root.SetActive(true);
            return root;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // ------------------------------------------------------------------ the vocabulary

        // A wall or a block: grey grid, unclimbable.
        static GameObject Solid(CourseBuilder b, string name, Vector3 a, Vector3 c) => b.Box(name, a, c, b.kit.wallNoClimb, true);

        // A picture: no collider.
        static GameObject Picture(CourseBuilder b, string name, Vector3 a, Vector3 c, Material material) =>
            b.Box(name, a, c, material, false, false);

        // A floor slab on the plaza level (top y 0).
        static GameObject Ground(CourseBuilder b, string name, float x0, float x1, float z0, float z1) => b.Floor(name, x0, x1, z0, z1, 0f);

        // A sky-blue patch on the floor: a checkpoint's.
        static void Marker(CourseBuilder b, string name, float x0, float x1, float z0, float z1) =>
            Picture(b, name, V(x0, .005f, z0), V(x1, .03f, z1), CourseKit.Or(b.kit.marker, b.kit.rankGold));

        // ------------------------------------------------------------------ the forecourt (z -6.4 .. 0)

        // Ours either side of the band: the obstacle section before builds the terrace, the arch, the four steps and the
        // 12 m band up to our entry (2-1's exit does). Our wall in line with the arch's pillars makes the arch the gate in
        // the plaza's wall, whatever section comes before; the lawns run from it to the front walk.
        static GameObject Forecourt(CourseBuilder b, int game)
        {
            var k = b.kit;
            foreach (float s in new[] { -1f, 1f })
            {
                Solid(b, "Forecourt wall", V(Mathf.Min(s * 7f, s * (Side + 1f)), -1f, WallSouth), V(Mathf.Max(s * 7f, s * (Side + 1f)), 7f, WallNorth));
                float l0 = Mathf.Min(s * 6.3f, s * Side), l1 = Mathf.Max(s * 6.3f, s * Side);
                Ground(b, "Lawn", l0, l1, WallNorth, 0f);
                Solid(b, "Lawn hedge", V(l0, 0f, -.6f), V(l1, .5f, 0f));
                foreach (float x in new[] { 17.5f, 21f })
                    b.Cylinder("Cypress", V(s * x, 0f, -4.4f), 1.4f, 5f, k.boardDark, false);
            }
            Props(b, game);

            // Where no section comes before (a round that starts here; the slot 1 list is still empty): a plain 12 m
            // approach in the band's place. EarlyPlaza switches it on only then.
            var standIn = b.Group("Stand-in approach (no section before)");
            var before = b.parent;
            b.parent = standIn;
            Ground(b, "Stand-in band", -6f, 6f, -12f, 0f);
            b.Rail("Stand-in rail W", -6.3f, -6f, -12f, 0f, 0f, 1.1f);
            b.Rail("Stand-in rail E", 6f, 6.3f, -12f, 0f, 0f, 1.1f);
            b.parent = before;
            standIn.gameObject.SetActive(false);
            return standIn.gameObject;
        }

        // The game's props on the lawns (mini-game zones v0.1 §3: 문턱 안쪽 세트), pictures only. C: a coiled rope, a
        // spare cog, a life buoy (cream, not red: red is danger and check), a stone pot. Every early set has the pot.
        static void Props(CourseBuilder b, int game)
        {
            var k = b.kit;
            if (game == PawnRushMissions.Drawbridge)
            {
                b.Cylinder("Prop rope coil", V(-9.6f, 0f, -2.3f), 1.6f, .5f, k.boardLight, false);
                b.Cylinder("Prop spare cog", V(-13.2f, 0f, -2.3f), 1.4f, .3f, k.boardDark, false);
                b.Cylinder("Prop life buoy", V(9.6f, 0f, -2.3f), 1.3f, .2f, k.trimWhite, false);
            }
            b.Cylinder("Prop stone pot", V(13.2f, 0f, -2.3f), 1.2f, .9f, k.wallNoClimb, false);
        }

        // ------------------------------------------------------------------ the plaza (z 0 .. 24)

        static float MoatSouth => Front + MgDrawbridge.Moat0 - .2f;
        static float MoatNorth => Front + MgDrawbridge.Moat1 + .2f;

        static void Plaza(CourseBuilder b, bool moat)
        {
            var k = b.kit;
            Ground(b, "Front walk", -Side, Side, 0f, Front);
            if (moat)
            {
                Ground(b, "Aisle", -2f, 2f, Front, MoatSouth);
                Ground(b, "Aisle (north bank)", -2f, 2f, MoatNorth, North0);
            }
            else Ground(b, "Aisle", -2f, 2f, Front, North0);
            Solid(b, "Plaza side wall W", V(-Side - 1f, Deep, WallNorth), V(-Side, 6f, North0));
            Solid(b, "Plaza side wall E", V(Side, Deep, WallNorth), V(Side + 1f, 6f, North0));
            // The split marks where the band ends: white's arrow to the west, black's to the east.
            Arrow(b, -1f, k.Team(Teams.White));
            Arrow(b, 1f, k.Team(Teams.Black));
        }

        static void Arrow(CourseBuilder b, float s, Material material)
        {
            Picture(b, "Split mark", V(Mathf.Min(s * 2.6f, s * 6f), .01f, .92f), V(Mathf.Max(s * 2.6f, s * 6f), .03f, 1.08f), material);
            foreach (float turn in new[] { -1f, 1f })
            {
                var head = Picture(b, "Split mark head", V(-.4f, .01f, -.08f), V(.4f, .03f, .08f), material);
                head.transform.localPosition = V(s * 5.7f, .02f, 1f + turn * .25f);
                head.transform.localRotation = Quaternion.Euler(0f, s * turn * 40f, 0f);
            }
        }

        // A team's station (Course01v4Builder.Station's parts): the gate passage through the north block, the door at
        // the station's north edge, the team barrier just behind it, the team's colour over the door, the game socket.
        static MissionStation Station(CourseBuilder b, int team, bool moat)
        {
            var g = b.Group("Station_" + Teams.Name(team));
            g.localPosition = V(team == Teams.White ? -TeamX : TeamX, 0f, Front);
            var before = b.parent;
            b.parent = g;
            float half = GateWidth * .5f, u = MissionStation.Size, depth = North1 - North0;
            b.Floor("Gate passage", -half, half, u, u + depth, 0f);
            var door = b.Box("Team gate", V(-half, 0f, u), V(half, 4.6f, u + .4f), b.kit.Team(team), true);
            var gate = door.AddComponent<TeamGate>();
            var barrier = b.Box("Team barrier", V(-half, 0f, u + .6f), V(half, 4.6f, u + .8f), b.kit.glass, true);
            barrier.AddComponent<TeamBarrier>().SetTeam(team);
            Picture(b, "Gate colour", V(-half - .3f, 4.6f, u - .05f), V(half + .3f, 5.2f, u), b.kit.Team(team));
            var socket = b.Group("Game socket");
            b.parent = before;
            var station = g.gameObject.AddComponent<MissionStation>();
            station.Configure(team, 1, team == Teams.Black, gate, socket, b.kit);
            station.MoatRunsOn = moat;
            return station;
        }

        // ------------------------------------------------------------------ the facade and the vestibule (z 24 .. 36)

        static void Facade(CourseBuilder b)
        {
            float d0 = TeamX - GateWidth * .5f, d1 = TeamX + GateWidth * .5f;
            Solid(b, "Facade W", V(-Side - 1f, Deep, North0), V(-d1, 12f, North1));
            Solid(b, "Facade E", V(d1, Deep, North0), V(Side + 1f, 12f, North1));
            Solid(b, "Facade middle W", V(-d0, Deep, North0), V(-5f, 12f, North1));
            Solid(b, "Facade middle E", V(5f, Deep, North0), V(d0, 12f, North1));
            Solid(b, "Gatehouse", V(-5f, Deep, North0), V(5f, 16f, North1));
            // Over the door passages: doors in the facade as high as the team gate (4.6 m; it rises into the wall when it
            // opens), so the gate's colour and the progress bar (Arena.Dress, 5.6 m) sit on the wall above it.
            Solid(b, "Facade over the door W", V(-d1, 4.6f, North0), V(-d0, 12f, North1));
            Solid(b, "Facade over the door E", V(d0, 4.6f, North0), V(d1, 12f, North1));
            // The closed main gate on the gatehouse (a picture: the gatehouse behind it is solid), 6 x 7 m.
            Picture(b, "Main gate (closed)", V(-3f, 0f, North0 - .12f), V(3f, 7f, North0), b.kit.boardDark);
        }

        static void Vestibule(CourseBuilder b)
        {
            float w = TeamX + GateWidth * .5f;
            Ground(b, "Vestibule floor", -w, w, North1, VestibuleEnd);
            Solid(b, "Vestibule wall W", V(-w - 1f, Deep, North1), V(-w, 7f, VestibuleEnd + 1f));
            Solid(b, "Vestibule wall E", V(w, Deep, North1), V(w + 1f, 7f, VestibuleEnd + 1f));
            Solid(b, "Vestibule wall N (west)", V(-w, Deep, VestibuleEnd), V(-6f, 7f, VestibuleEnd + 1f));
            Solid(b, "Vestibule wall N (east)", V(6f, Deep, VestibuleEnd), V(w, 7f, VestibuleEnd + 1f));
        }

        // ------------------------------------------------------------------ C: the moat across the aisle

        // The stations' moats (MgDrawbridge, ends left open: MissionStation.MoatRunsOn) and this one make one moat across
        // the plaza. In the station's frame the water is at -0.9 and the bottom at -1.6 over a floor 0.4 up (the dais),
        // so on the plaza: water -0.5, bottom -1.2. The aisle ends at a low rail; beyond the water the show bridge stands
        // raised before the main gate (fixed, a picture of the gate's own drawbridge).
        static void MoatAcross(CourseBuilder b)
        {
            var k = b.kit;
            float z0 = Front + MgDrawbridge.Moat0, z1 = Front + MgDrawbridge.Moat1;
            Solid(b, "Moat wall (aisle) S", V(-2f, -1.2f, z0 - .2f), V(2f, 0f, z0));
            Solid(b, "Moat wall (aisle) N", V(-2f, -1.2f, z1), V(2f, 0f, z1 + .2f));
            Solid(b, "Moat bottom (aisle)", V(-2f, -2.2f, z0), V(2f, -1.2f, z1));
            Picture(b, "Water (aisle)", V(-2f, -.55f, z0), V(2f, -.5f, z1), k.glass);
            b.Trigger<KillVolume>("Moat (aisle, fall)", V(-2f, -1.2f, z0), V(2f, -.3f, z1));
            b.Rail("Aisle rail", -2f, 2f, z0 - .5f, z0 - .2f, 0f, 1f);
            b.Box("Show drawbridge (raised)", V(-2f, 0f, z1 + .2f), V(2f, 5f, z1 + .5f), k.boardDark, true);
        }

        // ------------------------------------------------------------------ checkpoints, path, beats

        static void Checkpoints(CourseBuilder b)
        {
            b.Checkpoint(0, V(0f, 0f, 2f), 0f, 10f);
            Marker(b, "CP0 marker", -5f, 5f, 1.4f, 2.6f);
            b.Checkpoint(1, V(0f, 0f, North1 + 3f), 0f, 10f);
            Marker(b, "CP1 marker", -5f, 5f, North1 + 2.4f, North1 + 3.6f);
            b.Trigger<KillVolume>("Kill volume (far below)", V(-60f, -32f, -40f), V(60f, -30f, 80f));
        }

        // White's route by the west station, black's by the east (the mirror, the same length): the band's end, the
        // front walk, the station's ramp, up the station (over C's lowered bridge), the gate, the passage, the vestibule.
        static void Path(CourseBuilder b)
        {
            Vector3[] Route(float s)
            {
                float x = s * TeamX, dais = Arena.DaisHeight;
                return new[]
                {
                    V(0f, 0f, 0f), V(0f, 0f, 1.5f), V(x, 0f, 1.5f), V(x, 0f, Front - Arena.RampLength), V(x, dais, Front),
                    V(x, dais, North0 - .1f), V(x, 0f, North0 + .5f), V(x, 0f, North1 + 3f), V(0f, 0f, North1 + 3f), V(0f, 0f, VestibuleEnd),
                };
            }
            string[] beats = { "1", "1", "1", "1", "2", "2", "3", "3", "3", "3" };
            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            go.AddComponent<ProgressPath>().Configure(Route(-1f), Route(1f), beats);
        }

        // Areas for the HUD and the CSV (the first that holds a pawn wins).
        public static SectionBeat[] Beats(int game) => new[]
        {
            new SectionBeat("1 앞뜰·입구 띠", V(-Side - 1f, -3f, -12f), V(Side + 1f, 8f, Front)),
            new SectionBeat("2 " + PlaceName(game), V(-Side, -3f, Front), V(Side, 12f, North0)),
            new SectionBeat("3 문 통로·현관 회랑", V(-15f, -3f, North0), V(15f, 12f, VestibuleEnd + 1f)),
        };
    }
}
