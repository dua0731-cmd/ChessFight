using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    // Collision and gameplay ownership for the ten palace plazas. The art builder adds pictures, not rules.
    public static class MiniGameSectionBuilder
    {
        public static readonly string[] Names =
        {
            "정문 의장 광장", "보수 중인 동관", "해자 도개교", "정문 쇠살문 누각", "왕궁 서고",
            "대무도회장", "왕의 행차 회랑", "국왕 체스 홀", "국왕 침실", "왕궁 세탁장"
        };

        public static string Id(int game, int zone) => (zone == 0 ? "Early_" : zone == 1 ? "Mid_" : "Late_") + PawnRushMissions.Ids[game];

        public static void Build(MiniGameSection owner)
        {
            if (owner == null || owner.Kit == null) return;
            var old = owner.transform.Find(MiniGameSection.ContentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var content = new GameObject(MiniGameSection.ContentName);
            content.SetActive(false);
            content.transform.SetParent(owner.transform, false);
            var b = new CourseBuilder(owner.Kit, content.transform);
            b.Group(MiniGameSection.LayoutMark);
            FloorsAndWalls(b, owner.GameIndex, owner.Zone);
            var section = owner.GetComponent<PawnRushSection>();
            section.Describe(Id(owner.GameIndex, owner.Zone), Names[owner.GameIndex], PawnRushSection.SlotKind.MiniGame,
                Vector3.zero, new Vector3(0f, 0f, 36f), 0f, new[]
                {
                    new SectionBeat(Names[owner.GameIndex], new Vector3(-22f, -3f, 0f), new Vector3(22f, 19f, 30f)),
                    new SectionBeat("문 뒤 회랑", new Vector3(-22f, -3f, 30f), new Vector3(22f, 19f, 36f))
                });
            var stations = new MissionStation[2];
            for (int team = 0; team < 2; team++) stations[team] = Station(b, owner.Kit, team, section.Slot / 2 + 1, owner.GameIndex);
            owner.SetStations(stations);
            b.Checkpoint(0, new Vector3(0f, 0f, 1.5f), 0f, 12f);
            b.Checkpoint(1, new Vector3(0f, 0f, 33f), 0f, 12f);
            b.Trigger<KillVolume>("Palace fall return", new Vector3(-24f, -10f, -1f), new Vector3(24f, -3f, 37f));
            Paths(b, owner.GameIndex);
            RebuildGames(owner);
            MiniGamePalaceArt.BuildShell(content.transform, owner.Kit, owner.GameIndex);
            MiniGameModels.Apply(owner);   // R501: after the shell, which the models replace
            RefreshText(owner);
            content.SetActive(true);
        }

        public static void RebuildGames(MiniGameSection owner)
        {
            var section = owner.GetComponent<PawnRushSection>();
            var random = Random.state;
            try
            {
                foreach (var station in owner.Stations)
                {
                    if (station == null) continue;
                    // Both teams receive the same seed without consuming the rest of the game's random stream.
                    Random.InitState(section.Seed);
                    station.Install(owner.GameIndex);
                    MiniGamePalaceArt.Dress(station, owner.GameIndex);
                }
            }
            finally { Random.state = random; }
            owner.Built(MiniGameScale.Clamp(section.TeamSize));
            MiniGameModels.Apply(owner);
            RefreshText(owner);
        }

        // Baked labels in game sockets are replaced with the games. Follow the new renderers, not the
        // serialized references to the destroyed editor-built copies, when the OS font atlas changes.
        public static void RefreshText(MiniGameSection owner)
        {
            if (owner == null) return;
            var binder = owner.GetComponent<PalaceTextBinding>();
            if (binder == null) binder = owner.gameObject.AddComponent<PalaceTextBinding>();
            binder.Configure(owner.GetComponentsInChildren<TextMesh>(true));
        }

        static MissionStation Station(CourseBuilder b, Course01Kit kit, int team, int plaza, int game)
        {
            var parent = b.parent;
            var root = b.Group("Station_" + Teams.Name(team));
            root.localPosition = new Vector3(team == Teams.White ? -12f : 12f, 0f, 4f);
            b.parent = root;
            var ramp = b.Ramp("Palace station ramp", -2.2f, 2.2f, -Arena.RampLength, 0f, 0f, Arena.DaisHeight, .5f);
            ramp.GetComponent<Renderer>().sharedMaterial = MiniGamePalaceArt.DaisMaterial(game);
            b.Floor("Gate passage", -2f, 2f, 20f, 26f);
            var door = b.Box("Team gate", new Vector3(-2f, 0f, 23f), new Vector3(2f, 4.6f, 23.4f), Arena.Lit("PR_MG_TeamDoor", Arena.TeamColor(team, true)), true);
            var gate = door.AddComponent<TeamGate>();
            var barrier = b.Box("Team barrier", new Vector3(-2f, 0f, 23.6f), new Vector3(2f, 13f, 23.8f), null, true);
            Object.DestroyImmediate(barrier.GetComponent<MeshRenderer>());
            barrier.AddComponent<TeamBarrier>().SetTeam(team);
            var socket = b.Group("Game socket");
            b.parent = parent;
            var station = root.gameObject.AddComponent<MissionStation>();
            station.Configure(team, plaza, team == Teams.Black, gate, socket, kit);
            station.SetPalaceStyle(true);
            return station;
        }

        static void FloorsAndWalls(CourseBuilder b, int game, int zone)
        {
            b.Floor("Entry band", -22f, 22f, 0f, 4f);
            b.Floor("Rear corridor", -22f, 22f, 30f, 36f);
            if (game == PawnRushMissions.Drawbridge)
            {
                // R502: the moat is 5 m (z 15.5..20.5), as the stations' (MgDrawbridge.PalaceMoat0/1, user 11-07)
                b.Floor("Central aisle south", -2f, 2f, 4f, 15.5f);
                b.Floor("Central aisle north", -2f, 2f, 20.5f, 24f);
                var water = b.Box("Central moat water", new Vector3(-2f, -.6f, 15.5f), new Vector3(2f, -.5f, 20.5f), b.kit.glass, false, false);
                b.Trigger<KillVolume>("Central moat return", new Vector3(-2f, -1.2f, 15.5f), new Vector3(2f, -.3f, 20.5f));
                Collision(b, "Central moat railing", new Vector3(-2f, 0f, 15.2f), new Vector3(2f, 1.1f, 15.5f));
            }
            else b.Floor("Central aisle", -2f, 2f, 4f, 24f);
            if (game == PawnRushMissions.Paint)
            {
                var ramp = b.Ramp("Shared board entry ramp", -2f, 2f, 4f - Arena.RampLength, 4f, 0f, Arena.DaisHeight, .5f);
                ramp.GetComponent<Renderer>().sharedMaterial = MiniGamePalaceArt.DaisMaterial(game);
            }
            float height = zone == 0 ? 11f : zone == 1 ? 13f : 17f;
            Collision(b, "Palace wall W", new Vector3(-23f, -1f, -1f), new Vector3(-22f, height, 36f));
            Collision(b, "Palace wall E", new Vector3(22f, -1f, -1f), new Vector3(23f, height, 36f));
            Collision(b, "Palace entry W", new Vector3(-22f, -1f, -1f), new Vector3(-6f, height, 0f));
            Collision(b, "Palace entry E", new Vector3(6f, -1f, -1f), new Vector3(22f, height, 0f));
            foreach (var span in new[] { new Vector2(-22f, -14f), new Vector2(-10f, 10f), new Vector2(14f, 22f) })
                Collision(b, "Palace north block", new Vector3(span.x, -1f, 24f), new Vector3(span.y, height, 30f));
            Collision(b, "Palace exit W", new Vector3(-22f, -1f, 36f), new Vector3(-6f, height, 37f));
            Collision(b, "Palace exit E", new Vector3(6f, -1f, 36f), new Vector3(22f, height, 37f));
        }

        static void Collision(CourseBuilder b, string name, Vector3 min, Vector3 max)
        {
            var go = new GameObject(name + " collision");
            go.transform.SetParent(b.parent, false);
            go.transform.localPosition = (min + max) * .5f;
            go.AddComponent<BoxCollider>().size = max - min;
            go.AddComponent<NoClimbSurface>();
        }

        static void Paths(CourseBuilder b, int game)
        {
            var routes = new Vector3[2][];
            for (int team = 0; team < 2; team++)
            {
                float x = team == Teams.White ? -12f : 12f;
                var points = new List<Vector3> { Vector3.zero, new Vector3(0f, 0f, 2f), new Vector3(x, 0f, 3f), new Vector3(x, .4f, 5f) };
                if (game == PawnRushMissions.KnightPads)
                    foreach (var square in KnightRoute.Squares)
                    {
                        var p = KnightRoute.Center(square);
                        points.Add(new Vector3(x + (team == Teams.White ? p.V : -p.V), .4f, p.U + 4f));
                    }
                else
                {
                    points.Add(new Vector3(x, .4f, 8f));
                    points.Add(new Vector3(x, .4f, 23f));
                }
                points.Add(new Vector3(x, 0f, 25f));
                points.Add(new Vector3(x, 0f, 33f));
                points.Add(new Vector3(0f, 0f, 33f));
                points.Add(new Vector3(0f, 0f, 36f));
                routes[team] = points.ToArray();
            }
            var labels = new string[routes[0].Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = Names[game];
            b.Group("ProgressPath").gameObject.AddComponent<ProgressPath>().Configure(routes[0], routes[1], labels);
            if (game == PawnRushMissions.Planks || game == PawnRushMissions.Drawbridge || game == PawnRushMissions.KnightPads)
            {
                float z0 = game == PawnRushMissions.Planks ? 10f : game == PawnRushMissions.Drawbridge ? 16f : 6.1f;
                float z1 = game == PawnRushMissions.Drawbridge ? 20f : game == PawnRushMissions.Planks ? 20f : 23.3f;
                foreach (float x in new[] { -12f, 12f })
                {
                    var gap = b.Group("Mini-game gap completed by play");
                    gap.localPosition = new Vector3(x, 0f, (z0 + z1) * .5f);
                    gap.gameObject.AddComponent<IntendedGap>().Configure(new Vector3(20f, 8f, z1 - z0 + .2f), "Game-built bridge or knight jump route");
                }
            }
        }
    }
}
