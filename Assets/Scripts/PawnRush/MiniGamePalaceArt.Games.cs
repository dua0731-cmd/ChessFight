using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    public static partial class MiniGamePalaceArt
    {
        public static void Dress(MissionStation station, int game)
        {
            if (station == null || station.Game == null) return;
            var root = station.Game.transform;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "Station floor" || renderer.name == "Gate strip") renderer.sharedMaterial = DaisMaterial(game);
            DressGate(station, game);
            // I replaces Position when a new seed arrives. Its marker belongs to that position, not to the game.
            var scope = root;
            if (game == PawnRushMissions.CheckBlock)
            {
                scope = null;
                foreach (Transform child in root) if (child.name.StartsWith("Position ")) { scope = child; break; }
                // A remote observer waits for the authoritative seed before it has a position to dress.
                if (scope == null) return;
            }
            if (scope.Find(DressName) != null) return;
            var decoration = Group(scope, DressName);
            GoalSign(decoration, station, game);
            switch (game)
            {
                case PawnRushMissions.Paint: PaintCourt(decoration, station); break;
                case PawnRushMissions.Planks: RepairWing(decoration, root); break;
                case PawnRushMissions.Drawbridge: Drawbridge(decoration, root); break;
                case PawnRushMissions.Capstan: Capstan(decoration, root); break;
                case PawnRushMissions.Bells: Library(decoration, root); break;
                case PawnRushMissions.EnPassant: Ballroom(decoration, root, station); break;
                case PawnRushMissions.KnightPads: Laundry(decoration, root, station); break;
                case PawnRushMissions.KingEscort: Procession(decoration, root); break;
                case PawnRushMissions.Checkmate: ChessHall(decoration, root); break;
                case PawnRushMissions.CheckBlock: Bedchamber(decoration, scope, station); break;
            }
        }

        static void GoalSign(Transform parent, MissionStation station, int game)
        {
            var sign = Group(parent, "DEC_GameSign", new Vector3(0f, 3.9f, .2f));
            Box(sign, "Game sign board", Vector3.zero, new Vector3(5.2f, .95f, .17f), Early(game) ? DaisMaterial(game) : Late(game) ? M("SignNavy", "#28344D") : Wood);
            Box(sign, "Sign top trim", Vector3.up * .44f, new Vector3(5.4f, .1f, .22f), Brass);
            var symbol = Arena.Text(sign, "Game symbol", new Vector3(-1.95f, 0f, -.13f), .58f, Arena.Gold);
            var title = Arena.Text(sign, "Game title", new Vector3(.35f, 0f, -.13f), .36f, Arena.Hex("#E5DECC"));
            var team = TeamColor(station.Team);
            var tag = Group(sign, "Team entrance plaque", new Vector3(0f, -.91f, 0f));
            Box(tag, "Backplate", Vector3.zero, new Vector3(3.8f, .63f, .12f), Iron);
            var lane = Arena.Text(tag, "Team entrance label", new Vector3(0f, 0f, -.09f), .36f, team);
            lane.text = Teams.Name(station.Team) + (game == PawnRushMissions.Paint ? "팀 출구 방향" : "팀 진영");
            var bar = Group(parent, "DEC_GateProgress", new Vector3(0f, 5.8f, 19.4f));
            Box(bar, "Progress groove", Vector3.zero, new Vector3(4.2f, .34f, .15f), Iron);
            var fill = Box(bar, "Progress fill", new Vector3(-1.99f, 0f, -.08f), new Vector3(.02f, .22f, .04f), M("Progress" + station.Team, station.Team == Teams.Black ? "#F08A2C" : "#3FC1D9", .5f));
            var count = Arena.Text(bar, "Progress count", new Vector3(0f, .52f, -.06f), .35f, TeamColor(station.Team));
            var exit = Group(bar, "Team exit plaque", new Vector3(0f, -.64f, 0f));
            Box(exit, "Backplate", Vector3.zero, new Vector3(3.4f, .66f, .12f), Iron);
            Arena.Text(exit, "Team exit label", new Vector3(0f, 0f, -.09f), .4f, team).text = Teams.Name(station.Team) + "팀 출구";
            var view = parent.gameObject.AddComponent<ArenaDressing>();
            view.Configure(station, symbol, title, fill.transform, count);
            view.Installed(game);
            title.text = MiniGameSectionBuilder.Names[game];
        }

        static void DressGate(MissionStation station, int game)
        {
            var gate = station.Gate;
            if (gate == null || gate.transform.Find("DEC_PalaceGate") != null) return;
            var original = gate.GetComponent<Renderer>();
            if (original != null) original.enabled = false;
            var art = Group(gate.transform, "DEC_PalaceGate", Vector3.zero, true);
            if (game == PawnRushMissions.Bells)
            {
                var pivot = Group(station.transform, "DEC_ShelfDoorPivot");
                pivot.position = gate.transform.TransformPoint(Vector3.zero) - station.transform.right * 1.9f;
                Shelf(pivot, new Vector3(1.9f, 0f, 0f), 3.8f, 4.6f, .45f, false);
                var binding = pivot.gameObject.AddComponent<PalaceGateLeaf>();
                binding.Configure(gate, pivot);
                return;
            }
            if (game == PawnRushMissions.Capstan)
            {
                foreach (float y in new[] { -1.8f, -.5f, .8f, 2f }) Box(art, "Portcullis crossbar", new Vector3(0f, y, -.18f), new Vector3(3.9f, .16f, .18f), Iron);
                for (int i = -4; i <= 4; i++)
                {
                    Box(art, "Portcullis vertical bar", new Vector3(i * .43f, 0f, 0f), new Vector3(.13f, 4.6f, .18f), Iron);
                    var point = Box(art, "Portcullis foot", new Vector3(i * .43f, -2.22f, 0f), new Vector3(.18f, .25f, .2f), Iron);
                    point.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
                Crown(art, new Vector3(0f, 2.1f, -.22f), .55f);
                return;
            }
            Box(art, "Door leaf", Vector3.zero, new Vector3(3.85f, 4.55f, .3f), game == PawnRushMissions.Planks ? Cloth : Wood);
            foreach (float x in new[] { -1.7f, 1.7f }) Box(art, "Door frame stile", new Vector3(x, 0f, -.19f), new Vector3(.2f, 4.4f, .13f), Brass);
            foreach (float y in new[] { -1.9f, 1.9f }) Box(art, "Door frame rail", new Vector3(0f, y, -.19f), new Vector3(3.6f, .16f, .13f), Brass);
            if (game == PawnRushMissions.EnPassant)
                Box(art, "High mirror insert", new Vector3(0f, 1.5f, -.24f), new Vector3(3f, 1.3f, .06f), Glass);
            else if (game == PawnRushMissions.Planks)
                for (int i = -3; i <= 3; i++) Box(art, "Dust curtain fold", new Vector3(i * .5f, 0f, -.18f), new Vector3(.16f, 4.3f, .16f), i % 2 == 0 ? Cloth : M("ClothShadow", "#C2B8A3"));
            else Crown(art, new Vector3(0f, .4f, -.25f), .7f);
        }

        static void PaintCourt(Transform art, MissionStation station)
        {
            // Only the outer edges are dressed: the shared 64-square board owns its visuals and state.
            float side = station.Team == Teams.White ? -1f : 1f;
            foreach (float u in new[] { 3.5f, 9f, 15f })
            {
                var flag = Group(art, "Parade flagpole", new Vector3(side * 7.7f, 0f, u));
                Cylinder(flag, "Flag stone base", Vector3.up * .12f, .36f, .24f, Cream);
                Cylinder(flag, "Flagpole", Vector3.up * 2.1f, .035f, 4.2f, Brass);
                Banner(flag, new Vector3(.62f, 3.8f, 0f), 1.05f, 1.7f, Cloth, true);
            }
        }

        static void RepairWing(Transform art, Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Plank (laid)" && t.name != "Pile plank") continue;
                MaterialOn(t, Wood);
                var detail = Group(t, "DEC_PlankJoinery", Vector3.zero, true);
                float w = t.localScale.x, h = t.localScale.y, d = t.localScale.z;
                foreach (float x in new[] { -w * .4f, w * .4f }) foreach (float z in new[] { -d * .35f, d * .35f })
                    Cylinder(detail, "Gold nail head", new Vector3(x, h * .5f + .012f, z), .045f, .028f, Gold);
            }
            for (int row = 0; row < 6; row++)
            {
                float z = 6f + row * 2f;
                Box(art, "Exposed floor joist", new Vector3(0f, -.9f, z), new Vector3(6f, .3f, .2f), Wood);
                foreach (float x in new[] { -7.5f, 7.5f })
                {
                    Box(art, "Lower-storey scaffold post", new Vector3(x, -2.2f, z), new Vector3(.15f, 3.7f, .15f), Maple);
                    if (row < 5) Beam(art, "Lower-storey diagonal brace", new Vector3(x, -3.8f, z), new Vector3(x, -.6f, z + 2f), .12f, Wood);
                }
            }
            foreach (float x in new[] { -8.5f, 8.5f })
            {
                Box(art, "Dust screen rail", new Vector3(x, 2.5f, 10.9f), new Vector3(.12f, .12f, 12f), Wood);
                Box(art, "Dust screen cloth", new Vector3(x, 1.5f, 11f), new Vector3(.05f, 1.7f, 11.6f), Cloth);
            }
        }

        static void Drawbridge(Transform art, Transform root)
        {
            var hinge = Find(root, "Deck look");
            if (hinge != null) Deck(Group(hinge, "DEC_DeckJoinery"), 3.95f, 4.1f);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Bridge tower")
                {
                    MaterialOn(t, Cream);
                    var top = Group(t, "DEC_RookCrown", Vector3.zero, true);
                    Box(top, "Pylon capital", new Vector3(0f, 3.15f, 0f), new Vector3(1.3f, .3f, 1.3f), Stone);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * .5f;
                        Box(top, "Rook merlon", new Vector3(Mathf.Cos(a) * .44f, 3.5f, Mathf.Sin(a) * .44f), new Vector3(.42f, .55f, .42f), Cream);
                    }
                }
            var drum = Find(root, "Drum");
            if (drum != null)
                foreach (float x in new[] { -.95f, .95f })
                {
                    var ring = Ring(drum, "Drum iron hoop", new Vector3(x, 0f, 0f), .63f, Iron);
                    ring.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            var frame = Find(root, "Winch frame");
            if (frame != null) MaterialOn(frame, Wood);
        }

        static void Capstan(Transform art, Transform root)
        {
            var drum = Find(root, "Capstan drum");
            MaterialOn(drum, Wood);
            if (drum != null)
            {
                var trim = Group(drum, "DEC_DrumHoops", Vector3.zero, true);
                foreach (float y in new[] { -.65f, .65f }) Ring(trim, "Capstan iron hoop", new Vector3(0f, y, 0f), .73f, Iron);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * .25f;
                    Box(trim, "Oak stave", new Vector3(Mathf.Cos(a) * .71f, 0f, Mathf.Sin(a) * .71f), new Vector3(.08f, 1.5f, .08f), Maple);
                }
            }
            var bars = Find(root, "Capstan bars");
            if (bars != null)
            {
                foreach (Transform bar in bars)
                {
                    if (bar.name != "Bar") continue;
                    MaterialOn(bar, Wood);
                    var detail = Group(bar, "DEC_BarGrip", Vector3.zero, true);
                    Box(detail, "Thirty centimetre gold grip", new Vector3(bar.localScale.x * .5f - .15f, 0f, 0f), new Vector3(.3f, .24f, .24f), Gold);
                }
                Crown(bars, new Vector3(0f, 2.1f, 0f), .8f);
            }
            Chain(art, new Vector3(0f, 2.5f, 10f), new Vector3(0f, 2.5f, 22.7f));
            Chain(art, new Vector3(0f, 2.5f, 22.7f), new Vector3(0f, 5.9f, 22.7f));
        }

        static void Library(Transform art, Transform root)
        {
            foreach (var tower in root.GetComponentsInChildren<Transform>(true))
            {
                if (!tower.name.StartsWith("Tower ") || !tower.name.Contains(" storey ")) continue;
                MaterialOn(tower, Wood);
                var s = tower.localScale;
                Shelf(Group(tower, "DEC_Bookshelf", Vector3.zero, true), Vector3.zero, s.x, s.y, s.z, true);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Bell post W" || t.name == "Bell post E" || t.name == "Bell beam") MaterialOn(t, Wood);
                if (t.name != "Bell") continue;
                var shade = Group(t, "DEC_ReadingLamp", Vector3.zero, true);
                Cylinder(shade, "Lamp shade cap", new Vector3(0f, .42f, 0f), .59f, .16f, Brass);
                Cylinder(shade, "Lamp base", new Vector3(0f, -.48f, 0f), .34f, .12f, Brass);
                Beam(shade, "Reading lamp neck", new Vector3(.44f, -.4f, 0f), new Vector3(.44f, .38f, 0f), .07f, Brass);
            }
        }

        static void Shelf(Transform parent, Vector3 at, float width, float height, float depth, bool onlyFace)
        {
            if (!onlyFace) Box(parent, "Shelf carcass", at, new Vector3(width, height, depth), Wood);
            foreach (float x in new[] { -width * .5f + .07f, width * .5f - .07f })
                Box(parent, "Shelf upright", at + new Vector3(x, 0f, -depth * .5f - .035f), new Vector3(.14f, height, .13f), Maple);
            int rows = Mathf.Max(2, Mathf.FloorToInt(height / .72f));
            for (int row = 0; row <= rows; row++)
            {
                float y = -height * .5f + row * height / rows;
                Box(parent, "Shelf ledge", at + new Vector3(0f, y, -depth * .5f - .08f), new Vector3(width + .1f, .1f, .26f), Maple);
                if (row == rows) continue;
                int books = Mathf.Max(3, Mathf.RoundToInt(width / .32f));
                for (int i = 0; i < books; i++)
                {
                    float w = (width - .35f) / books;
                    var book = Box(parent, "Book spine", at + new Vector3(-width * .5f + .18f + (i + .5f) * w, y + .32f, -depth * .5f - .05f), new Vector3(w * .8f, .5f + (i % 3) * .045f, .14f), M("Book" + i % 4, new[] { "#946445", "#A68156", "#6B6D59", "#704E4A" }[i % 4]));
                    if (i % 4 == 0) book.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
                }
            }
        }

        static void Ballroom(Transform art, Transform root, MissionStation station)
        {
            int them = station.Team == Teams.White ? Teams.Black : Teams.White;
            var costume = M("Costume" + them, them == Teams.Black ? "#B8774A" : "#5E8D9F");
            int number = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Statue ")) continue;
                var look = t.Find("Look");
                if (look == null) continue;
                var dressed = Group(look, "DEC_DancerCostume");
                bool gown = (number++ & 1) == 0;
                if (gown)
                {
                    Cylinder(dressed, "Pleated gown", new Vector3(0f, .48f, 0f), .38f, .52f, costume);
                    Ring(dressed, "Gown hem", new Vector3(0f, .22f, 0f), .38f, Brass);
                }
                else
                {
                    Box(dressed, "Tailcoat", new Vector3(0f, .58f, .12f), new Vector3(.45f, .7f, .25f), Iron);
                    Box(dressed, "Shirt front", new Vector3(0f, .65f, -.2f), new Vector3(.17f, .3f, .05f), Cloth);
                }
                Box(dressed, "Dance collar", new Vector3(0f, .68f, 0f), new Vector3(.31f, .1f, .28f), costume);
                var key = Group(dressed, "Clockwork winding key", new Vector3(0f, .68f, .4f));
                Cylinder(key, "Key spindle", Vector3.zero, .05f, .24f, Brass).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                for (int i = -1; i <= 1; i += 2)
                    Ring(key, "Winding key bow", new Vector3(i * .13f, 0f, .13f), .12f, Brass).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Bench(art, new Vector3(station.Mirrored ? -8.8f : 8.8f, 0f, 17.4f), 0f, true);
        }

        static void Laundry(Transform art, Transform root, MissionStation station)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Road ") && t.name.Length <= 7)
                {
                    var original = t.GetComponent<Renderer>();
                    if (original != null) original.enabled = false;
                    var detail = Group(t, "DEC_LaundryBarrel", Vector3.zero, true);
                    float h = t.localScale.y;
                    Cylinder(detail, "Barrel curved stave body", new Vector3(0f, .05f, 0f), 1f, Mathf.Min(h, 1.8f), Maple);
                    Cylinder(detail, "Barrel landing lid", new Vector3(0f, h * .5f + .03f, 0f), 1f, .08f, Maple);
                    foreach (float y in new[] { -.4f, .4f }) Ring(detail, "Barrel iron hoop", new Vector3(0f, y, 0f), 1.02f, Iron);
                }
                if (t.name.StartsWith("Jump pad "))
                {
                    var nozzle = Group(t, "DEC_BubbleNozzle");
                    Cylinder(nozzle, "Round perforated nozzle", new Vector3(0f, .13f, 0f), .37f, .08f, Gold);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        Cylinder(nozzle, "Nozzle hole", new Vector3(Mathf.Cos(a) * .2f, .175f, Mathf.Sin(a) * .2f), .035f, .006f, Iron);
                    }
                }
                if (!t.name.StartsWith("Piece ")) continue;
                var look = t.Find("Look");
                if (look == null) continue;
                foreach (var r in look.GetComponentsInChildren<Renderer>(true)) if (r.name != "Ring") r.sharedMaterial = Cloth;
                var seam = Group(look, "DEC_DollSeams");
                for (int i = 0; i < 7; i++) Box(seam, "Cloth stitch", new Vector3(0f, .18f + i * .11f, -.24f), new Vector3(.06f, .015f, .035f), Wood);
            }
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 3; i++) Box(art, "White washhouse wall tile", new Vector3(side * 9.85f, -.25f, 5f + i * 4f), new Vector3(.06f, .9f, 3.92f), Cloth);
        }

        static void Procession(Transform art, Transform root)
        {
            var sled = Find(root, "King sled");
            if (sled != null)
            {
                var look = sled.Find("Look");
                var body = Group(look != null ? look : sled, "DEC_RoyalPalanquin");
                foreach (float side in new[] { -1f, 1f })
                {
                    Box(body, "Palanquin side panel", new Vector3(side * .82f, .75f, 0f), new Vector3(.12f, .6f, 1.58f), Wood);
                    Box(body, "Carrier amber grip band", new Vector3(side * .89f, .4f, 0f), new Vector3(.07f, .13f, 1.68f), Gold);
                }
                foreach (float z in new[] { -.8f, .8f })
                {
                    Box(body, "Palanquin end panel", new Vector3(0f, .65f, z), new Vector3(1.7f, .4f, .12f), Wood);
                    Box(body, "Carrier amber end band", new Vector3(0f, .4f, z * 1.09f), new Vector3(1.8f, .13f, .07f), Gold);
                }
                Box(body, "Velvet seat cushion", new Vector3(0f, .6f, 0f), new Vector3(1.45f, .15f, 1.45f), Violet);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Sand ripple") MaterialOn(t, M("ErmineSpots", "#29282B"));
                else if (t.name.StartsWith("Road "))
                {
                    var r = t.GetComponent<Renderer>();
                    if (r != null && r.sharedMaterial != null && r.sharedMaterial.name == "M_Sand") r.sharedMaterial = M("Ermine", "#D8D1BE", 0f, .98f);
                }
                else if (t.name == "Curb") MaterialOn(t, Violet);
            var road = Find(root, "Road");
            if (road == null) return;
            // R543: the road is this game's own (KingRoad, drawn anew for every game); it says where its rooks stand.
            var escort = root.GetComponent<MgKingEscort>();
            var rookCells = escort != null ? escort.Road.RookCells : KingEscortPath.RookCells;
            int garageIndex = 0;
            foreach (Transform garage in road)
            {
                if (garage.name != "Garage" || garageIndex >= rookCells.Length) continue;
                var from = Find(road, "Road " + rookCells[garageIndex++]);
                if (from == null) continue;
                // Follow the built road and garage transforms, including the station's mirrored geometry.
                var at = art.InverseTransformPoint(garage.position);
                var direction = at - art.InverseTransformPoint(from.position);
                direction.y = 0f;
                var bay = Group(art, "DEC_RookParkingBay " + garageIndex, at);
                bay.localRotation = Quaternion.LookRotation(direction, Vector3.up);
                float back = MgKingEscort.PathCell * .5f;
                Box(bay, "Rook parking bay back", new Vector3(0f, 1.4f, back), new Vector3(3.2f, 2.8f, .12f), Wood);
                foreach (float x in new[] { -1.7f, 1.7f }) Box(bay, "Parking bay jamb", new Vector3(x, 1.4f, back), new Vector3(.16f, 2.8f, .6f), Marble);
            }
        }

        static void ChessHall(Transform art, Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Pillar")) continue;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                r.sharedMaterial = Marble;
                var trim = Group(t, "DEC_ChessPillar", Vector3.zero, true);
                Box(trim, "Chess pillar cornice", Vector3.up * (t.localScale.y * .5f), new Vector3(t.localScale.x + .2f, .16f, t.localScale.z + .2f), Stone);
            }
        }

        static void Bedchamber(Transform art, Transform position, MissionStation station)
        {
            var kingBase = Find(position, "King base");
            if (kingBase != null)
            {
                var oldKing = kingBase.Find("Our king");
                if (oldKing != null) foreach (var r in oldKing.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                var bed = Group(kingBase, "DEC_CanopyBed");
                Box(bed, "Bed walnut base", new Vector3(0f, .45f, 0f), new Vector3(1.9f, .48f, 2.3f), Wood);
                Box(bed, "Bed amber grip band", new Vector3(0f, .46f, 0f), new Vector3(1.98f, .12f, 2.38f), Gold);
                Box(bed, "Mattress", new Vector3(0f, .79f, 0f), new Vector3(1.72f, .24f, 2.15f), Cloth);
                Box(bed, "Purple blanket", new Vector3(0f, .94f, .4f), new Vector3(1.76f, .12f, 1.3f), Violet);
                Box(bed, "Pillow", new Vector3(0f, .99f, -.7f), new Vector3(1.1f, .18f, .5f), Cloth);
                foreach (float x in new[] { -.9f, .9f }) foreach (float z in new[] { -1.1f, 1.1f })
                {
                    Cylinder(bed, "Canopy post", new Vector3(x, 1.9f, z), .045f, 3.25f, Brass);
                    Ball(bed, "Canopy finial", new Vector3(x, 3.6f, z), .1f, Brass);
                }
                Box(bed, "Canopy cloth", new Vector3(0f, 3.5f, 0f), new Vector3(2.12f, .13f, 2.55f), Violet);
                foreach (float x in new[] { -.94f, .94f }) Box(bed, "Canopy high valance", new Vector3(x, 3.18f, 0f), new Vector3(.15f, .55f, 2.45f), Violet);
                var sleeper = MiniGameParts.Piece(bed, "Sleeping king", PieceKind.King, 1.1f, MiniGameParts.PieceMaterial(station.Team), TeamColor(station.Team), false).transform;
                sleeper.localPosition = new Vector3(0f, .98f, -.7f);
                sleeper.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Ball(bed, "Nightcap pompom", new Vector3(.2f, 1.15f, -.85f), .14f, Violet);
            }
            foreach (var cart in position.GetComponentsInChildren<Transform>(true))
            {
                if (!cart.name.StartsWith("Shield cart ")) continue;
                var look = cart.Find("Look");
                if (look == null) continue;
                var shield = Group(look, "DEC_ShieldScreen");
                Box(shield, "Walnut shield frame", new Vector3(0f, 1.23f, .04f), new Vector3(1.45f, 1.75f, .15f), Wood);
                Box(shield, "Shield velvet inset", new Vector3(0f, 1.23f, -.06f), new Vector3(1.16f, 1.43f, .08f), Violet);
                foreach (float x in new[] { -.65f, .65f }) Box(shield, "Shield brass stile", new Vector3(x, 1.23f, -.11f), new Vector3(.09f, 1.72f, .07f), Brass);
                Box(shield, "Shield amber grip stripe", new Vector3(0f, .6f, -.14f), new Vector3(1.32f, .12f, .08f), Gold);
                foreach (float x in new[] { -.62f, .62f }) foreach (float z in new[] { -.46f, .46f })
                    Cylinder(shield, "Cart wheel hub", new Vector3(x, .21f, z), .08f, .09f, Brass).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        static void Chain(Transform parent, Vector3 a, Vector3 b)
        {
            float length = Vector3.Distance(a, b);
            int n = Mathf.Max(2, Mathf.CeilToInt(length / .28f));
            for (int i = 0; i <= n; i++)
            {
                var ring = Ring(parent, "Chain link", Vector3.Lerp(a, b, i / (float)n), .12f, Iron);
                ring.transform.localRotation = Quaternion.LookRotation((b - a).normalized) * Quaternion.Euler(90f, 0f, i % 2 == 0 ? 0f : 90f);
            }
        }
    }
}
