using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    // R501: Blender models over a mini-game section (ArtSource/PawnRush/MiniGames/<phase>/, the R499 art standard).
    // Like the garden's concept dressing, a model is only a picture: every collider, door, rope, knot and lamp stays the
    // game's own, and the code-built pictures it replaces stop drawing (their colliders stay). Moving parts ride the
    // game's own transforms (the drawbridge's hinge, the winch's drum), so the game moves them as before.
    //
    // Resources/PawnRushMiniGames/<phase>/<name>.fbx; materials are the phase kit's (phase 1: the garden's GC_ set,
    // Resources/PawnRushGardenConcept/Materials, by name). A missing model leaves the R312 code-built art as it was.
    // Partial: each mini-game's own part may live in MiniGameModels.<Game>.cs (E: MiniGameModels.Library.cs).
    public static partial class MiniGameModels
    {
        const string Folder = "PawnRushMiniGames/";
        const string Materials = "PawnRushGardenConcept/Materials/";
        const string EnvName = "MODEL_Env";
        const string RideName = "MODEL_Ride";
        const string GateName = "MODEL_CenterGate";
        const string DrumName = "MODEL_CapstanDrum";
        const string ArchName = "MODEL_TeamArches";
        const string PortcullisName = "MODEL_Portcullis";
        const string OwnMaterials = "PawnRushMiniGames/Materials/";   // textured materials of the user-model sets (R505~)

        // Called after a section's games are (re)installed and dressed, and after a build's shell.
        public static void Apply(MiniGameSection owner)
        {
            if (owner == null) return;
            if (owner.GameIndex == PawnRushMissions.Drawbridge) Drawbridge(owner);
            else if (owner.GameIndex == PawnRushMissions.Capstan) Capstan(owner);
            else if (owner.GameIndex == PawnRushMissions.Planks) RepairWing(owner);
            else if (owner.GameIndex == PawnRushMissions.KingEscort) RoyalProcession(owner);
            else if (owner.GameIndex == PawnRushMissions.Bells) RoyalLibrary(owner);
            else if (owner.GameIndex == PawnRushMissions.Paint) ParadeCourt(owner);   // MiniGameModels.ParadeCourt.cs (R506)
        }

        // G · 왕의 행차 회랑 (R504, ArtSource/PawnRush/MiniGames/Late/g_center_gate_v1.py).
        // The user's "Royal Stone Gateway" (x16: 16 m wide, 10.4 m high) dresses the closed central door; it never opens,
        // the teams leave through their own gates. The phase-3 wall is 17 m, higher than the gate, so unlike B and D the
        // centre wall and its ashlar stay; only the shell's door pictures in front of it below the gate top stop drawing
        // (closed door, crown arch, crown, the two inner pilasters). The royal emblem high above the gate stays.
        static void RoyalProcession(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            if (content == null) return;
            var cache = new Dictionary<string, Material>();
            var gate = Load("Late/G/G_CenterGate");
            if (gate != null)
            {
                if (content.Find(GateName) == null)
                {
                    var at = new GameObject(GateName).transform;
                    at.SetParent(content, false);
                    at.localPosition = new Vector3(0f, 0f, 23.69f);   // the closed central door's face (MiniGamePalaceArt.Shell)
                    Hang(at, RideName, gate, cache);
                }
                var shell = content.Find(MiniGamePalaceArt.ShellName);
                var facade = shell != null ? Find(shell, "ENV_NorthFacade") : null;
                if (facade != null)
                    foreach (var renderer in facade.GetComponentsInChildren<Renderer>(true))
                    {
                        string n = renderer.name;
                        if (n == "North wall mass" || n == "Ashlar face") continue;
                        var b = renderer.bounds;
                        var lo = content.InverseTransformPoint(b.min);
                        var hi = content.InverseTransformPoint(b.max);
                        float x0 = Mathf.Min(lo.x, hi.x), x1 = Mathf.Max(lo.x, hi.x);
                        float y0 = Mathf.Min(lo.y, hi.y), z = (lo.z + hi.z) * .5f;
                        if (x0 >= -9.7f && x1 <= 9.7f && z > 22.5f && z < 27f && y0 < 10.6f) renderer.enabled = false;
                    }
            }
            RoyalProcessionStations(owner, cache);
            RoyalProcessionHall(content, cache);
            RoyalProcessionFloor(owner, content);
        }

        // G pieces (R504, Late/g_props_v1.py: the user's models cut from the asset photos). Each model rides the game's own
        // moving body, so the game moves it as before; only the code-built pictures stop drawing (colliders stay):
        //   king sled "Look"   -> palanquin (base 1.6 m = SledSize). White: the palanquin with its ivory king; black: the
        //                         empty palanquin with the game's own black king kept. The push ring stays.
        //   "Rook in the way k" -> guard rook 1.6 m in the opponent's colour (ebony for black, ivory for white); ring stays.
        //   "Rook tower"       -> castling tower 2.2 m (white), the ebony rook grown to 2.2 m (black).
        //   garages c7 / g7    -> the arched alcove (2.6 m wide), mouth to the road, where DEC_RookParkingBay stood. The
        //                         photo's gold bar across its mouth was cut away; the game's gold stop at the back stays.
        const string AlcoveName = "MODEL_RookAlcove";
        const float AlcoveScale = 2.6f / 3.6f;

        static void RoyalProcessionStations(MiniGameSection owner, Dictionary<string, Material> cache)
        {
            var palanquinKing = Load("Late/G/G_PalanquinKing");
            var palanquin = Load("Late/G/G_Palanquin");
            var ebony = Load("Late/G/G_RookEbony");
            var ivory = Load("Late/G/G_RookIvory");
            var castling = Load("Late/G/G_CastlingRook");
            var alcove = Load("Late/G/G_GarageAlcove");
            foreach (var station in owner.Stations)
            {
                if (station == null || station.Game == null) continue;
                var game = station.Game.transform;
                bool white = station.Team == Teams.White;

                var sled = Find(game, "King sled");
                var look = sled != null ? sled.Find("Look") : null;
                var carrier = white ? palanquinKing : palanquin;
                if (look != null && carrier != null)
                {
                    if (white) HideExcept(look, "Push ring");
                    else HideExcept(look, "Push ring", "Our king");
                    if (look.Find(RideName) == null) Hang(look, RideName, carrier, cache);
                }

                var theirs = white ? ebony : ivory;
                for (int k = 1; k <= 2; k++)
                {
                    var rook = Find(game, "Rook in the way " + k);
                    if (rook == null || theirs == null) continue;
                    HideExcept(rook, "Ring");
                    if (rook.Find(RideName) == null) Hang(rook, RideName, theirs, cache);
                }

                var tower = Find(game, "Rook tower");
                var ours = white ? castling : ebony;
                if (tower != null && ours != null)
                {
                    HideExcept(tower, "Ring");
                    if (tower.Find(RideName) == null)
                    {
                        var at = new GameObject(RideName).transform;
                        at.SetParent(tower, false);
                        if (!white) at.localScale = Vector3.one * (2.2f / 1.6f);
                        Hang(at, "MODEL_Pivot", ours, cache);
                    }
                }

                TeamGateDoor(station, cache);

                // the team ramp (station frame x -2.2..2.2, z -1.6..0, up 0 -> 0.4) dressed as stairs; its slope collider
                // stays the walking surface, only its flat picture stops drawing
                var ramp = Find(station.transform, "Palace station ramp");
                var rampStairs = Load("Late/G/G_StairsRamp");
                if (ramp != null && rampStairs != null && ramp.parent.Find("MODEL_RampStairs") == null)
                {
                    if (ramp.TryGetComponent<Renderer>(out var flatRamp)) flatRamp.enabled = false;
                    var at = new GameObject("MODEL_RampStairs").transform;
                    at.SetParent(ramp.parent, false);
                    at.localPosition = new Vector3(0f, 0f, -Arena.RampLength);
                    at.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);   // low side to the entry
                    Hang(at, RideName, rampStairs, cache);
                }

                var road = Find(game, "Road");
                if (road == null || alcove == null) continue;
                int index = 0;
                foreach (Transform garage in road)
                {
                    if (garage.name != "Garage" || index >= KingEscortPath.RookCells.Length) continue;
                    var from = Find(road, "Road " + KingEscortPath.RookCells[index]);
                    index++;
                    Hide(Find(station.transform, "DEC_RookParkingBay " + index));
                    if (from == null || game.Find(AlcoveName + " " + index) != null) continue;
                    var dir = garage.position - from.position;
                    var local = game.InverseTransformPoint(garage.position);
                    var at = new GameObject(AlcoveName + " " + index).transform;
                    at.SetParent(game, false);
                    at.localPosition = new Vector3(local.x, 0f, local.z);
                    at.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(dir, game.up).normalized, game.up);
                    at.localScale = Vector3.one * AlcoveScale;
                    Hang(at, RideName, alcove, cache);
                }
            }
        }

        // A real dais (user 11-07, "진짜 단으로"): the team floors already stand Arena.DaisHeight (0.4 m) up with a ramp
        // each; now the central aisle between them (x -2..2, z 4..24) rises to the same height as a walkable floor with
        // the purple runner on it, reached from the entry by the user's marble stairs (g_stairs_v1.py G_StairsCenter,
        // 4 m wide, z 2.4..4) over an invisible slope collider, as the team ramps (so no pawn trips on a step edge).
        // These are the only colliders MiniGameModels adds: a walking surface, CourseFloor-marked like the builder's.
        static void RoyalProcessionDais(Transform content, Transform env, Dictionary<string, Material> cache)
        {
            float h = Arena.DaisHeight, run = Arena.RampLength;
            var cream = Resources.Load<Material>(OwnMaterials + "PR_Late_MarbleCream");
            var velvet = Resources.Load<Material>(OwnMaterials + "PR_Late_Velvet");
            // the raised aisle: a solid box (walked on) and its tiled marble picture, the runner on top
            var aisle = new GameObject("Raised aisle (walk)", typeof(BoxCollider), typeof(CourseFloor)).transform;
            aisle.SetParent(env, false);
            aisle.localPosition = new Vector3(0f, h * .5f, 14f);
            aisle.GetComponent<BoxCollider>().size = new Vector3(4f, h, 20f);
            if (cream != null) MarbleBlock(env, "Raised aisle", new Vector3(0f, h * .5f, 14f), new Vector3(4f, h, 20f), cream);
            if (velvet != null) MarbleBlock(env, "Aisle runner", new Vector3(0f, h + .01f, 14f), new Vector3(2.4f, .02f, 20f), velvet);
            // the central stairs and their slope (0.5 m thick under the surface, as CourseBuilder.Ramp's)
            var stairs = Load("Late/G/G_StairsCenter");
            if (stairs != null)
            {
                var at = new GameObject("Entry stairs").transform;
                at.SetParent(env, false);
                at.localPosition = new Vector3(0f, 0f, 4f - run);
                at.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                Hang(at, RideName, stairs, cache);
            }
            var from = new Vector3(0f, 0f, 4f - run); var to = new Vector3(0f, h, 4f);
            var d = to - from;
            var rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            var slope = new GameObject("Entry stairs slope (walk)", typeof(BoxCollider));
            slope.AddComponent<CourseFloor>().sloped = true;
            slope.transform.SetParent(env, false);
            slope.transform.localRotation = rotation;
            slope.transform.localPosition = (from + to) * .5f - rotation * Vector3.up * .25f;
            slope.GetComponent<BoxCollider>().size = new Vector3(4f, .5f, d.magnitude);
        }

        // The team gate door (g_team_gate_v1.py, 4.5 x 6 m, 0.35 m thick) rides the game's team gate box (4 x 4.6 m, it
        // rises 4.6 m), bottom to the box's bottom, as the D portcullis: it fills the arch's door and slides up into the
        // wall above. Its painted wood (slot PR_Late_G_GatePaint) takes the station's team colour (blue white team,
        // orange black team); its iron and brass stay. The box's flat picture and the R312 palace gate dressing stop
        // drawing; the box, its collider and its lift are the game's.
        static readonly Dictionary<int, Material> teamPaint = new Dictionary<int, Material>();

        static void TeamGateDoor(MissionStation station, Dictionary<string, Material> cache)
        {
            var box = station.Gate != null ? station.Gate.transform : null;
            var door = box != null ? Load("Late/G/G_TeamGateDoor") : null;
            if (door == null || box.Find(RideName) != null) return;
            Hide(box.Find("DEC_PalaceGate"));
            var flat = box.GetComponent<Renderer>();
            if (flat != null) flat.enabled = false;
            var at = new GameObject(RideName).transform;
            at.SetParent(box, false);
            var s = box.localScale;
            at.localScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(s.x)), 1f / Mathf.Max(.0001f, Mathf.Abs(s.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(s.z)));
            at.localPosition = new Vector3(0f, -.5f, 0f);    // the box's bottom face, in its own (scaled) units
            Hang(at, "MODEL_Pivot", door, cache);
            if (!teamPaint.TryGetValue(station.Team, out var paint) || paint == null)
            {
                var source = Resources.Load<Material>(OwnMaterials + "PR_Late_G_GatePaint");
                if (source == null) return;
                paint = new Material(source) { name = "PR_Late_G_GatePaint " + Teams.Name(station.Team) };
                var team = MiniGamePalaceArt.TeamColor(station.Team);
                paint.SetColor("_BaseColor", Color.Lerp(team, Color.white, .15f));
                teamPaint[station.Team] = paint;
            }
            foreach (var r in at.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null && slots[i].name.StartsWith("PR_Late_G_GatePaint")) slots[i] = paint;
                r.sharedMaterials = slots;
            }
        }

        // G hall dressing in section coordinates (R504), outside every game square (team boards x +-3..21, z 6..24 on
        // the 0.4 m dais; ramps x +-9.8..14.2, z 2.4..4; central aisle x -3..3):
        //   velvet rope stanchions along both aisle edges (x +-2.55, rope along the aisle), crown pedestals either side
        //   of the runner at the door, trumpet stands in the entry corners, balustrades either side of each team ramp.
        //   Pictures only, no colliders.
        // Walls (user 11-07: "기존에 있던 구조물 벽이나 이런거 치우고 맞게 다시 세팅해", "벽 구조물 다시 만들자 … 기존에
        // 있던 벽은 치우라니까"): every wall of the R312 shell stops drawing - side walls, the north facade round the
        // centre gate (balcony, team portal arches, banners), the entry wall and arch, the rear passage - so the new wall
        // models (asset photo Docs/PawnRush/ConceptArt/R504_RoyalProcession/Assets/G_W1_walls.png -> Tripo) can stand in
        // their place. The floor paving, the roof beams and the threshold props stay. Pictures only: every collider, the
        // team gates and the section's entry/exit openings are untouched.
        // Kept from the shell: the floor, the threshold props, the rear passage the teams walk out through (everything
        // north of z 25.5) and, in the north facade, the team portals' tunnel (linings, rear arch, pocket roof).
        static readonly string[] ShellKeep = { "ENV_EntryAndRearPaving", "DEC_ThresholdProps" };
        static readonly string[] PortalTunnel = { "Portal tunnel lining", "Team portal rear", "Gate pocket cover" };

        // The new walls (user's Tripo "Classical Marble Elements" from G_W1_walls.png, g_props_v1.py "Walls"): all 12 m
        // high, the ceiling comes down to 12 m with them (structure may change, user 11-07). Window/plain bays stand
        // edge to edge between fluted columns, every bay stretched along the wall to the exact column spacing, so the
        // wall runs on without a gap; the columns stand in front of the bay joints. Openings the game needs keep their
        // size: the team portals (x +-9.75..14.25, 5.8 m) and the entry (x +-6.25, 7.5 m) get marble lintels above.
        const float Hall = 12f, BackX = 22.6f, BackN = 24.6f, BackS = -.75f;
        const float ColumnHalf = 1.215f, ColumnDepth = 2.4f;
        static readonly float[] BayDepth = { 1.75f, 1.46f };            // window, plain (model depth at 12 m)
        static readonly float[] BayWidth = { 5.48f, 5.36f };

        static void RoyalProcessionHall(Transform content, Dictionary<string, Material> cache)
        {
            if (content.Find(EnvName) != null) return;
            var env = new GameObject(EnvName).transform;
            env.SetParent(content, false);
            void Put(GameObject model, string name, float x, float z, float yaw, float y = 0f)
            {
                if (model == null) return;
                var at = new GameObject(name).transform;
                at.SetParent(env, false);
                at.localPosition = new Vector3(x, y, z);
                at.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Hang(at, RideName, model, cache);
            }
            var stanchion = Load("Late/G/G_StanchionPair");
            var pedestal = Load("Late/G/G_CrownPedestal");
            var trumpets = Load("Late/G/G_TrumpetStand");
            foreach (float side in new[] { -1f, 1f })
            {
                // the model's face is the holder's -z; turn it to the aisle / room (+x on the west, -x on the east)
                float inward = side < 0f ? -90f : 90f;
                // on the dais edge (the team floors start at |x| 2, 0.4 m up)
                foreach (float z in new[] { 7f, 11f, 15f, 19f }) Put(stanchion, "Aisle stanchions", side * 2.55f, z, inward, Arena.DaisHeight);
                Put(pedestal, "Crown pedestal", side * 2.4f, 1.2f, 0f);
                Put(trumpets, "Fanfare trumpets", side * 18f, 1.4f, 0f);
                // (the ramp balustrades gave way to the stairs' own balustrades, RoyalProcessionDais)
            }
            RoyalProcessionDais(content, env, cache);
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell != null)
                foreach (Transform t in shell)
                {
                    if (t.name == "ENV_NorthFacade") HideExcept(t, PortalTunnel);
                    else if (System.Array.IndexOf(ShellKeep, t.name) < 0 && t.localPosition.z < 25.5f) Hide(t);
                }
            var marble = Resources.Load<Material>(OwnMaterials + "PR_Late_MarbleCream");
            ProcessionWalls(env, cache, marble);

            // Skylight vaults (g_skylight_v1.py): the user's rib frame is half a barrel (low rail at Blender -x, high bar
            // at +x, 5.9 m across, 3.4 m rise). Two halves, the east one mirrored, meet in a barrel over each team half
            // (centre x +-8, 11.8 m wide) on the new 12 m ceiling, each half twice along z 0..23.6 (stretched 0.8 along its
            // rails). Open to the sky, no glass (user 11-07). The flat marble ceiling closes everything else.
            var vault = Load("Late/G/G_SkylightVault");
            if (vault != null)
                foreach (float centre in new[] { -8f, 8f })
                    foreach (float half in new[] { -1f, 1f })
                        foreach (float z in new[] { 5.9f, 17.7f })
                        {
                            var at = new GameObject("Skylight vault").transform;
                            at.SetParent(env, false);
                            at.localPosition = new Vector3(centre + half * 2.95f, Hall + .5f, z);   // on top of the 0.5 m ceiling slab, so no face shares its plane
                            at.localScale = new Vector3(half < 0f ? 1f : -1f, 1f, .8f);
                            Hang(at, RideName, vault, cache);
                        }
            RoyalProcessionLight(env);
            RoyalProcessionCloth(env);
            if (marble == null) return;
            const float slab = .5f;
            void Ceiling(float x0, float x1, float z0, float z1) =>
                MarbleBlock(env, "Ceiling", new Vector3((x0 + x1) * .5f, Hall + slab * .5f, (z0 + z1) * .5f), new Vector3(x1 - x0, slab, z1 - z0), marble);
            Ceiling(-BackX, -13.6f, BackS, BackN); Ceiling(-2.4f, 2.4f, BackS, BackN); Ceiling(13.6f, BackX, BackS, BackN);
            foreach (float s in new[] { -1f, 1f })
            {
                float a = s * 2.4f, b = s * 13.6f;   // slab edges 0.3 m under the vault rails, not in the rails' outer face (flicker)
                Ceiling(Mathf.Min(a, b), Mathf.Max(a, b), BackS, .3f);
                Ceiling(Mathf.Min(a, b), Mathf.Max(a, b), 23.3f, BackN);
            }
        }

        static void ProcessionWalls(Transform env, Dictionary<string, Material> cache, Material marble)
        {
            var bays = new[] { Load("Late/G/G_WallBayWindow"), Load("Late/G/G_WallBayPlain") };
            var column = Load("Late/G/G_WallColumn");
            if (bays[0] == null || bays[1] == null || column == null) return;

            // One piece on a wall: `along` runs along the wall, `back` is the wall's back line, `inward` the unit
            // normal into the room (in x/z), yaw turns the model's face (holder -z) to it.
            void Piece(GameObject model, string name, Vector3 along, float alongAt, Vector3 inward, float back, float depth, float stretch)
            {
                var at = new GameObject(name).transform;
                at.SetParent(env, false);
                var pos = along * alongAt + inward * 0f;
                if (Mathf.Abs(inward.x) > .5f) pos.x = -inward.x * (back - depth * .5f);
                else pos.z = inward.z > 0f ? back + depth * .5f : back - depth * .5f;
                at.localPosition = pos;
                at.localRotation = Quaternion.LookRotation(-inward, Vector3.up);
                at.localScale = new Vector3(stretch, 1f, 1f);
                Hang(at, RideName, model, cache);
            }
            // columns at `cols` along the wall, bays between neighbours (pattern window, plain, ...), skipping a gap
            void Run(Vector3 along, Vector3 inward, float back, float[] cols, bool[] bayOn, bool[] colOn, string wall)
            {
                for (int i = 0; i < cols.Length; i++)
                    if (colOn[i]) Piece(column, wall + " column", along, cols[i], inward, back, ColumnDepth, 1f);
                for (int i = 0, k = 0; i + 1 < cols.Length; i++)
                {
                    if (!bayOn[i]) continue;
                    int kind = k++ % 2;
                    float width = cols[i + 1] - cols[i];
                    Piece(bays[kind], wall + (kind == 0 ? " window bay" : " plain bay"), along, (cols[i] + cols[i + 1]) * .5f, inward, back, BayDepth[kind], width / BayWidth[kind]);
                }
            }
            float colIn = BackX - ColumnHalf;    // 21.385: corner columns, shared by the side and end walls
            // side walls: 5 columns from the entry corner to the north corner, 4 bays
            var side = new[] { .45f, 6.3f, 12.15f, 18f, 23.4f };
            foreach (float s in new[] { -1f, 1f })
                Run(Vector3.forward, new Vector3(-s, 0f, 0f), BackX, side, new[] { true, true, true, true }, new[] { true, true, true, true, true }, s < 0f ? "West wall" : "East wall");
            // north wall, per half: column beside the centre gate, the team portal (no bay, a lintel), column, one bay to
            // the corner column (that one belongs to the side wall)
            foreach (float s in new[] { -1f, 1f })
            {
                var cols = new[] { 8.535f, 15.465f, colIn };
                if (s < 0f) cols = new[] { -colIn, -15.465f, -8.535f };
                var bayOn = s < 0f ? new[] { true, false } : new[] { false, true };
                var colOn = s < 0f ? new[] { false, true, true } : new[] { true, true, false };
                Run(Vector3.right, Vector3.back, BackN, cols, bayOn, colOn, "North wall");
                // the team portal (g_team_gate_v1.py): the arch frame fills the bay between the columns from the floor
                // to the ceiling (origin = opening bottom centre on the wall's back line, 1.6 m deep), the rook crest on
                // its panel above the arch; without the model, a plain marble lintel over the 5.8 m opening
                var frame = Load("Late/G/G_TeamGateFrame");
                var crest = Load("Late/G/G_TeamGateCrest");
                if (frame != null)
                {
                    var at = new GameObject("Team portal frame").transform;
                    at.SetParent(env, false);
                    at.localPosition = new Vector3(s * 12f, 0f, BackN);
                    at.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);   // face (holder -z) to the room
                    Hang(at, RideName, frame, cache);
                    if (crest != null)
                    {
                        var c = new GameObject("Team portal crest").transform;
                        c.SetParent(at, false);
                        c.localPosition = new Vector3(0f, 10.4f, -1.33f);   // on the panel above the arch (front 1.31 m)
                        Hang(c, RideName, crest, cache);
                    }
                }
                else if (marble != null)
                    MarbleBlock(env, "Portal lintel", new Vector3(s * 12f, (5.8f + Hall) * .5f, BackN - .8f), new Vector3(6.93f, Hall - 5.8f, 1.6f), marble);
            }
            if (marble != null)   // behind and above the centre gate (10.4 m) up to the ceiling
                MarbleBlock(env, "Centre wall", new Vector3(0f, Hall * .5f, BackN - .3f), new Vector3(2f * 8.535f, Hall, .6f), marble);
            // entry wall, per half: column beside the entry, three bays to the corner column
            foreach (float s in new[] { -1f, 1f })
            {
                var cols = new[] { 7.465f, 12.105f, 16.745f, colIn };
                if (s < 0f) cols = new[] { -colIn, -16.745f, -12.105f, -7.465f };
                var colOn = s < 0f ? new[] { false, true, true, true } : new[] { true, true, true, false };
                Run(Vector3.right, Vector3.forward, BackS, cols, new[] { true, true, true }, colOn, "Entry wall");
            }
            if (marble != null)
                MarbleBlock(env, "Entry lintel", new Vector3(0f, (7.5f + Hall) * .5f, BackS + .8f), new Vector3(12.5f, Hall - 7.5f, 1.6f), marble);
        }

        // G light (R504, user "조명부터"; phase-3 standard: skylight light shafts, no chandeliers). The hall is closed at
        // 12 m now, so the scene's sun only comes in through the two skylight vaults. Under each vault: four visible
        // light shafts (two crossed additive planes each, white-gold, fading to nothing at the floor, all slanted the
        // same way as in the concept), and two warm spot lights so the floor under the skylights is bright and the
        // rest of the hall falls back. Pictures and lights only: no colliders, no shadows (12 players' frame time).
        static readonly Vector3 ShaftSlant = new Vector3(-2.6f, -Hall, 1.2f);   // top -> floor
        static Material shaftMaterial;
        static Mesh shaftMesh;

        static void RoyalProcessionLight(Transform env)
        {
            if (env.Find("Skylight light") != null) return;
            var root = new GameObject("Skylight light").transform;
            root.SetParent(env, false);
            if (shaftMaterial == null)
            {
                // white, alpha: bright at the skylight, gone at the floor, soft at both sides
                var fade = new Texture2D(32, 64, TextureFormat.RGBA32, false) { name = "PR_LightShaftFade", wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float v = y / 63f, side = Mathf.Sin(Mathf.PI * (x + .5f) / 32f);
                        fade.SetPixel(x, y, new Color(1f, 1f, 1f, v * v * side * side));
                    }
                fade.Apply();
                shaftMaterial = ChessFight.Game.LitMaterials.NewAdditiveParticles(fade, new Color(1f, .93f, .78f, .28f));
                if (shaftMaterial != null) shaftMaterial.SetFloat("_Cull", 0f);   // both faces of each plane
            }
            if (shaftMesh == null) shaftMesh = ShaftMesh(1.3f, 2f);
            if (shaftMaterial != null)
                foreach (float x in new[] { -8f, 8f })
                    foreach (float z in new[] { 3f, 9f, 15f, 21f })
                    {
                        var go = new GameObject("Light shaft", typeof(MeshFilter), typeof(MeshRenderer));
                        go.transform.SetParent(root, false);
                        go.transform.localPosition = new Vector3(x, Hall, z);
                        go.GetComponent<MeshFilter>().sharedMesh = shaftMesh;
                        var r = go.GetComponent<MeshRenderer>();
                        r.sharedMaterial = shaftMaterial;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        r.receiveShadows = false;
                    }
            foreach (float x in new[] { -8f, 8f })
                foreach (float z in new[] { 6f, 18f })
                {
                    var go = new GameObject("Skylight sun");
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = new Vector3(x, Hall - .3f, z);
                    go.transform.localRotation = Quaternion.LookRotation(ShaftSlant.normalized, Vector3.forward);
                    var l = go.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.color = new Color(1f, .94f, .84f);
                    l.intensity = 2.6f;
                    l.range = 18f;
                    l.spotAngle = 80f;
                    l.shadows = LightShadows.None;
                }
        }

        // G cloth (R504): hanging cloth is code, not model (user 11-07 via the B session: "천도 … 코드로 해야 해").
        // MiniGameBannerCloth (R505) sways on its own on every PC; each hangs from a matte brass rod. On the 12 m walls
        // (ProcessionWalls): a purple velvet curtain either side of every side- and north-wall window, a purple velvet
        // royal banner in the middle of every side-wall plain bay (the team banner over each portal gave way to the gate crest; was: white team
        // x -12 blue, black team x +12 orange: MiniGamePalaceArt.TeamColor, team colour only on team things).
        static void RoyalProcessionCloth(Transform env)
        {
            if (env.Find("Hanging cloth") != null) return;
            var velvet = Resources.Load<Material>(OwnMaterials + "PR_Late_Velvet");
            var brass = Resources.Load<Material>(OwnMaterials + "PR_Late_Brass");
            var royal = Resources.Load<Material>(OwnMaterials + "PR_Late_G_RoyalBanner");   // crown, border, V hem (g_banner_tex_v1.py)
            if (velvet == null) return;
            var root = new GameObject("Hanging cloth").transform;
            root.SetParent(env, false);
            // a holder on a wall face: local x along the wall, y up, cloth facing the room (local -z)
            Transform Face(string name, Vector3 at, Vector3 inward)
            {
                var h = new GameObject(name).transform;
                h.SetParent(root, false);
                h.localPosition = at;
                h.localRotation = Quaternion.LookRotation(-inward, Vector3.up);
                return h;
            }
            void Cloth(Transform face, float along, float top, float w, float h, Material m)
            {
                MiniGameBannerCloth.Hang(face, new Vector3(along, top, 0f), w, h, m);
                if (brass == null) return;
                var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rod.name = "Cloth rod";
                if (Application.isPlaying) Object.Destroy(rod.GetComponent<Collider>());
                else Object.DestroyImmediate(rod.GetComponent<Collider>());   // a prefab build runs this outside Play
                rod.transform.SetParent(face, false);
                rod.transform.localPosition = new Vector3(along, top + .05f, 0f);
                rod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                rod.transform.localScale = new Vector3(.09f, w * .5f + .2f, .09f);
                rod.GetComponent<Renderer>().sharedMaterial = brass;
            }
            const float pad = .15f;   // in front of the bay's face
            foreach (float s in new[] { -1f, 1f })
            {
                var inward = new Vector3(-s, 0f, 0f);
                // side walls: bays between the columns .45 / 6.3 / 12.15 / 18 / 23.4 are window, plain, window, plain
                foreach (float z in new[] { 3.375f, 15.075f })
                {
                    var face = Face("Side window curtains", new Vector3(s * (BackX - BayDepth[0] - pad), 0f, z), inward);
                    foreach (float side in new[] { -1f, 1f }) Cloth(face, side * 1.75f, 9.4f, 1.25f, 8.4f, velvet);
                }
                foreach (float z in new[] { 9.225f, 20.7f })
                    Cloth(Face("Royal banner", new Vector3(s * (BackX - BayDepth[1] - pad), 0f, z), inward), 0f, 9.6f, 1.9f, 5.2f, royal != null ? royal : velvet);
                // north wall: the window bay at x +-18.43
                var north = Face("North window curtains", new Vector3(s * 18.425f, 0f, BackN - BayDepth[0] - pad), Vector3.back);
                foreach (float side in new[] { -1f, 1f }) Cloth(north, side * 1.75f, 9.4f, 1.25f, 8.4f, velvet);
                // (the team banner over the portal is gone: the team gate's rook crest hangs there and its door wears the
                // team colour, TeamGateDoor)
            }
        }

        // Two crossed planes from the origin (the skylight) down along ShaftSlant: halfTop wide at the top, halfFloor at
        // the floor; v = 1 at the top, 0 at the floor (the fade texture).
        static Mesh ShaftMesh(float halfTop, float halfFloor)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            foreach (var across in new[] { Vector3.right, Vector3.forward })
            {
                int s = v.Count;
                v.Add(-across * halfTop); v.Add(across * halfTop);
                v.Add(ShaftSlant + across * halfFloor); v.Add(ShaftSlant - across * halfFloor);
                uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(0f, 0f));
                tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            var mesh = new Mesh { name = "PR_LightShaft" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // A box drawn in a tiling swatch material: every face's UVs are its size in metres / 2 (the swatches are 2 m tiles),
        // so long walls repeat the texture instead of stretching it. Picture only, no collider.
        static readonly Dictionary<Vector3, Mesh> tiledBoxes = new Dictionary<Vector3, Mesh>();

        static GameObject MarbleBlock(Transform parent, string name, Vector3 centre, Vector3 size, Material material)
        {
            if (!tiledBoxes.TryGetValue(size, out var mesh) || mesh == null)
            {
                mesh = new Mesh { name = "PR_TiledBox " + size };
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
                var h = size * .5f;
                // (normal, u axis, v axis) for the six faces
                var faces = new[] { (Vector3.right, Vector3.forward, Vector3.up), (Vector3.left, Vector3.back, Vector3.up),
                                    (Vector3.up, Vector3.right, Vector3.forward), (Vector3.down, Vector3.right, Vector3.back),
                                    (Vector3.forward, Vector3.left, Vector3.up), (Vector3.back, Vector3.right, Vector3.up) };
                foreach (var (normal, u, w) in faces)
                {
                    int start = v.Count;
                    var c = Vector3.Scale(normal, h);
                    float su = Mathf.Abs(Vector3.Dot(size, u)), sw = Mathf.Abs(Vector3.Dot(size, w));
                    foreach (var (a, b) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                    {
                        v.Add(c + u * (a * su * .5f) + w * (b * sw * .5f));
                        n.Add(normal);
                        uv.Add(new Vector2((a + 1f) * su * .25f, (b + 1f) * sw * .25f));
                    }
                    tri.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
                mesh.RecalculateTangents(); mesh.RecalculateBounds();
                tiledBoxes[size] = mesh;
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // G floors (R504, user "바닥·길 칸부터"): the phase-3 swatches (ArtSource/.../Late/Materials, installed by
        // g_install_unity.py) on the game's own floor bodies. A body's picture stops drawing and a tiled copy of the same
        // box (MarbleBlock: UVs in metres / 2, so the 2 m swatches repeat instead of stretching) draws in its place; every
        // collider and CourseFloor stays the game's.
        //   station dais, gate strip, garage floors -> navy marble #29334A     road squares -> maple / walnut by square
        //   colour, the four slow squares -> ermine (the code's ripple stripes stop drawing, the fur has the tails)
        //   road curbs -> purple velvet      ramps -> navy marble (their own sloped mesh keeps its UVs)
        //   hall paving, aisle and entry band -> cream marble     central runner -> purple velvet
        const string TiledName = "MODEL_Tiled";

        static void TileOver(Transform body, Material material)
        {
            var renderer = body != null ? body.GetComponent<Renderer>() : null;
            if (renderer == null || material == null || body.parent == null) return;
            renderer.enabled = false;
            if (body.Find(TiledName) != null) return;
            var s = body.localScale;
            var copy = MarbleBlock(body.parent, TiledName, body.localPosition, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)), material);
            copy.transform.localRotation = body.localRotation;
            copy.transform.SetParent(body, true);       // rides with the body (and is found on a re-apply)
        }

        static void RoyalProcessionFloor(MiniGameSection owner, Transform content)
        {
            Material Swatch(string name) => Resources.Load<Material>(OwnMaterials + name);
            var navy = Swatch("PR_Late_MarbleNavy");
            var cream = Swatch("PR_Late_MarbleCream");
            var maple = Swatch("PR_Late_Maple");
            var walnut = Swatch("PR_Late_Walnut");
            var ermine = Swatch("PR_Late_Ermine");
            var velvet = Swatch("PR_Late_Velvet");
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                foreach (var r in station.GetComponentsInChildren<Renderer>(true))
                {
                    var t = r.transform;
                    string n = t.name;
                    if (n == "Station floor" || n == "Gate strip" || n == "Garage") TileOver(t, navy);
                    else if (n == "Curb") TileOver(t, velvet);
                    else if (n == "Sand ripple") r.enabled = false;
                    else if (n == "Palace station ramp" && navy != null) r.sharedMaterial = navy;
                    else if (n.StartsWith("Road ") && n.Length <= 7)
                    {
                        string cell = n.Substring(5);
                        PawnRushBoard.Parse(cell, out int f, out int rank);
                        bool slow = System.Array.IndexOf(KingEscortPath.Sand, cell) >= 0;
                        TileOver(t, slow ? ermine : PawnRushBoard.IsLight(f, rank) ? maple : walnut);
                    }
                }
            }
            foreach (var name in new[] { "Entry band", "Central aisle", "Rear corridor" }) TileOver(content.Find(name), cream);
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell == null) return;
            foreach (var r in shell.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Paving module" && cream != null) r.sharedMaterial = cream;   // 1.97 m slabs, one 2 m tile each
                else if (r.name == "Central velvet runner") TileOver(r.transform, velvet);
            }
        }

        // Stops the pictures under `root` drawing except those under a child named in `keep` (or an earlier model).
        static void HideExcept(Transform root, params string[] keep)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bool kept = false;
                for (var t = renderer.transform; t != null && t != root && !kept; t = t.parent)
                    kept = t.name == RideName || System.Array.IndexOf(keep, t.name) >= 0;
                if (!kept) renderer.enabled = false;
            }
        }

        // B · 보수 중인 동관 (R503, ArtSource/PawnRush/MiniGames/Mid/b_center_gate_v1.py, b_wall_arch_v1.py).
        // The user's archway door dresses the closed central block (it never opens; the teams leave through their own
        // gates), and the user's arched-window wall module repeats along both side walls: slab middle at x +-22.2 (in the
        // shell's wall backing), centres z 2, 8, 14, 20 and 33 so no pilaster stands at the plank piles (z 6..8).
        // The shell's side wainscot, its insets and the full-height wall pilasters stop drawing; the high windows stay
        // above the modules. Every collider stays the game's.
        static readonly float[] WallModuleZ = { 2f, 8f, 14f, 20f, 33f };

        static void RepairWing(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            if (content == null) return;
            var cache = new Dictionary<string, Material>();
            var gate = Load("Mid/B/B_CenterGate");
            if (gate != null && content.Find(GateName) == null)
            {
                var at = new GameObject(GateName).transform;
                at.SetParent(content, false);
                at.localPosition = new Vector3(0f, 0f, 23.69f);   // the closed central door's face (MiniGamePalaceArt.Shell)
                Hang(at, RideName, gate, cache);
            }
            if (gate != null) HideCentreDoor(content, 10f);   // the 16 x 9.9 m gate: the wall and its ashlar stay above it
            RepairWingStations(owner, cache);
            RepairWingHall(content, cache);
            RepairWingUpper(content, cache);
            RepairWingFloors(owner);

            var wall = Load("Mid/B/B_WallArch");
            if (wall == null) return;
            if (content.Find(EnvName) == null)
            {
                var env = new GameObject(EnvName).transform;
                env.SetParent(content, false);
                foreach (float side in new[] { -1f, 1f })
                    for (int i = 0; i < WallModuleZ.Length; i++)
                    {
                        // Blender -y (the module's face) is the holder's -z; turn it to face the room (+x on the west).
                        // Continuous wall (user, 2026-11-07): 3 % longer than the 6 m pitch so neighbours overlap, every
                        // other one 1 cm further back so the overlaps never share a plane (no flicker).
                        var at = new GameObject("Side wall module").transform;
                        at.SetParent(env, false);
                        at.localPosition = new Vector3(side * (22.2f + .01f * (i % 2)), 0f, WallModuleZ[i]);
                        at.localRotation = Quaternion.Euler(0f, side < 0f ? -90f : 90f, 0f);
                        at.localScale = new Vector3(1.03f, 1f, 1f);
                        Hang(at, RideName, wall, cache);
                    }
            }
            // The R312 shell is removed for B (user, 2026-11-07: "기존에 있던 구조물 치우고 다시 재배치 해"): every one of its
            // pictures stops drawing (its colliders were never the shell's; the section's walls are MiniGameSectionBuilder's),
            // and B_ShellEnv (b_shell_v1.py) closes every gap the models leave up to the 13 m walnut ceiling in one cream
            // ashlar, keeping the entry, the exit and the two team-gate tunnels open.
            var fill = Load("Mid/B/B_ShellEnv");
            if (fill == null) return;
            if (content.Find("MODEL_Shell") == null) Hang(content, "MODEL_Shell", fill, cache);
            Hide(content.Find(MiniGamePalaceArt.ShellName));
        }

        // B's core scene (R503, b_core_scene_v1.py), in each station's game frame: the chasm's basement 4 m down with
        // joists, scaffold bays and work lamps, and the work props in the corners (B_StationEnv), and the walnut plank
        // model inside every plank box the game makes - laid, piled and carried (B_Plank, a unit cube the box's scale
        // sizes). The boxes keep their colliders and their SetActive; only their flat picture stops drawing.
        static void RepairWingStations(MiniGameSection owner, Dictionary<string, Material> cache)
        {
            var env = Load("Mid/B/B_StationEnv");
            foreach (var station in owner.Stations)
            {
                if (station == null || station.Game == null) continue;
                var game = station.Game.transform;
                if (env != null && game.Find(EnvName) == null)
                {
                    Hang(game, EnvName, env, cache);
                    MiniGameFixtures.LightSockets(game.Find(EnvName));      // the basement work lamps' flames and lights
                }
                // The team gate (4 x 4.6 m, rises 4.6 m when the team finishes): a swaying dust sheet made in code rides it.
                var teamGate = station.transform.Find("Team gate");
                if (teamGate != null && env != null)
                {
                    var flat = teamGate.GetComponent<Renderer>();
                    if (flat != null) flat.enabled = false;
                    MiniGameFixtures.GateCloth(teamGate, 4f, 4.6f);
                    // B's moments (pick, lay, drop, return, quarters, the dust sheet going up): MiniGamePlankFx, which is
                    // also this station's IGateFx so the shared beam/burst gives way to the dust-sheet effect
                    var fx = station.GetComponent<MiniGamePlankFx>();
                    if (fx == null) fx = station.gameObject.AddComponent<MiniGamePlankFx>();
                    fx.Configure(station, station.Game as MgPlanks);
                    // The shell's team banners are gone with the shell; a team-coloured cloth made in code hangs from the
                    // arch's cross beam (x +-12, underside 6.46 m, front z 23.8) so each team still finds its own gate.
                    var section = owner.transform.Find(MiniGameSection.ContentName);
                    float x = station.transform.localPosition.x;
                    if (section != null && section.Find(x < 0f ? "MODEL_Pennant_W" : "MODEL_Pennant_E") == null)
                        MiniGameFixtures.Pennant(section, x < 0f ? "MODEL_Pennant_W" : "MODEL_Pennant_E",
                            new Vector3(x, 6.4f, 23.55f), 2.4f, 3.0f, MiniGamePalaceArt.TeamColor(station.Team));   // hem point 3.3 m up
                }
                foreach (var t in game.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Plank (laid)" || t.name == "Pile plank") DressPlank(t.gameObject);
                    // The gold "Plank place" outlines over the chasm: the user asked them gone (2026-11-07); the joists
                    // under the planks now show where the bridge goes. Picture only, they never had a collider.
                    else if (t.name == "Plank place" && env != null) Hide(t);
                }
            }
        }

        // B's hall (R503, b_hall_v1.py): a stone-and-scaffold arch frame at each team portal (x +-12, z 23.8; its timber
        // posts stand 4 m apart, the gate's width), and the hall dressing in the section frame (B_HallEnv: walkway tiles,
        // the gallery balustrade on the side-wall modules, four chandeliers, eight sconces, stools and rolled sheets).
        // Flames and lights come from code at the models' SOCKET_ empties. The shell's portal arches and portal
        // sconces stop drawing; the portal upper wall, the gate pocket and the team banners stay.
        // B's upper floor, ceiling and arches (R503, b_upper_v1.py; B_UpperEnv in the section frame): the gallery deck on
        // the side modules (top 5.3 m), the arched-window upper walls and cornice behind it, five walnut ceiling beams
        // with wall brackets, coffered panels over the chandeliers, the open grand arch at the entry and the exit
        // (inner 12.4 m, the game's openings are 12 m) and the rear corridor wall modules. The shell's side high windows,
        // its whole ceiling group (beams and unlit code chandeliers) and its entry and exit arches stop drawing.
        static void RepairWingUpper(Transform content, Dictionary<string, Material> cache)
        {
            var upper = Load("Mid/B/B_UpperEnv");
            if (upper == null) return;
            if (content.Find("MODEL_Upper") == null) Hang(content, "MODEL_Upper", upper, cache);
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell == null) return;
            foreach (var t in shell.GetComponentsInChildren<Transform>(true))
                if (t.name == "ENV_HighWindow" || t.name == MiniGamePalaceArt.CeilingName ||
                    t.name == "ENV_OpenEntryArch" || t.name == "ENV_CommonExitArch")
                    Hide(t);
        }

        // B's floors (R503, r503_material_swatches.py): the shell's paving went with the shell, so the game's own floor
        // boxes take the baked phase-2 floors - slate flagstones on the stations, the gate passages and the aisle,
        // cream-and-grey marble chequer on the entry band, cream marble on the rear corridor. Each texture is a 2 m
        // tile: the box's top face (UV 0..1) repeats size / 2 times. Colliders and the gold rims are untouched.
        static void RepairWingFloors(MiniGameSection owner)
        {
            var slate = Resources.Load<Material>(OwnMaterials + "PR_Mid_SlateFloor");
            var chequer = Resources.Load<Material>(OwnMaterials + "PR_Mid_CheckerMarble");
            var cream = Resources.Load<Material>(OwnMaterials + "PR_Mid_CreamFloor");
            if (slate == null || chequer == null || cream == null) return;
            var block = new MaterialPropertyBlock();
            foreach (var r in owner.GetComponentsInChildren<Renderer>(true))
            {
                Material m = null;
                switch (r.name)
                {
                    case "Station floor": case "Gate passage": case "Central aisle":
                    case "Central aisle south": case "Central aisle north": m = slate; break;
                    case "Entry band": m = chequer; break;
                    case "Rear corridor": m = cream; break;
                }
                if (m == null) continue;
                r.sharedMaterial = m;
                var s = r.transform.lossyScale;
                block.Clear();
                block.SetVector("_BaseMap_ST", new Vector4(Mathf.Abs(s.x) * .5f, Mathf.Abs(s.z) * .5f, 0f, 0f));
                r.SetPropertyBlock(block);
            }
            HideOldRepairWingDressing(owner);
            DressRepairWingSigns(owner, slate);
        }

        // The game-name sign over each station (MiniGamePalaceArt.GoalSign, 5.2 x 0.95 m) dressed for the hall (user,
        // 2026-11-07, "홀에 맞게 꾸미기"): walnut-grain board, matte-brass rails top and bottom, brass end caps and corner
        // studs, the rook symbol in brass and the title in cream. Only pictures; the sign's text keeps updating.
        static void DressRepairWingSigns(MiniGameSection owner, Material anyKit)
        {
            var walnut = Resources.Load<Material>(OwnMaterials + "PR_Mid_WalnutFloor");
            var brass = Resources.Load<Material>(OwnMaterials + "PR_Mid_MatteBrass");
            if (walnut == null || brass == null) return;
            var block = new MaterialPropertyBlock();
            foreach (var sign in owner.GetComponentsInChildren<Transform>(true))
            {
                if (sign.name != "DEC_GameSign" || sign.Find("MODEL_SignFrame") != null) continue;
                foreach (var r in sign.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name == "Game sign board")
                    {
                        r.sharedMaterial = walnut;
                        block.Clear();
                        block.SetVector("_BaseMap_ST", new Vector4(5.2f / 2f, .95f / 2f, 0f, .37f));   // one board row across
                        r.SetPropertyBlock(block);
                    }
                    else if (r.name == "Sign top trim") r.enabled = false;           // replaced by the brass frame below
                }
                foreach (var text in sign.GetComponentsInChildren<TextMesh>(true))
                    text.color = text.name == "Game symbol" ? new Color(.78f, .62f, .32f) : new Color(.93f, .89f, .8f);
                var frame = new GameObject("MODEL_SignFrame").transform;
                frame.SetParent(sign, false);
                foreach (float y in new[] { .5f, -.5f })
                    SignPart(PrimitiveType.Cube, frame, "Sign brass rail", new Vector3(0f, y, -.02f), new Vector3(5.5f, .11f, .24f), brass);
                foreach (float x in new[] { -2.72f, 2.72f })
                {
                    SignPart(PrimitiveType.Cube, frame, "Sign brass end cap", new Vector3(x, 0f, -.02f), new Vector3(.16f, 1.1f, .26f), brass);
                    foreach (float y in new[] { .32f, -.32f })
                        SignPart(PrimitiveType.Sphere, frame, "Sign brass stud", new Vector3(x * .955f, y, -.11f), Vector3.one * .11f, brass);
                }
                // a small brass scroll finial over the centre of the top rail
                SignPart(PrimitiveType.Sphere, frame, "Sign brass finial", new Vector3(0f, .62f, -.02f), new Vector3(.22f, .16f, .16f), brass);
            }
        }

        static void SignPart(PrimitiveType type, Transform parent, string name, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        // The R312 code-built B dressing that the models replace (user, 2026-11-07: "벽 노란색 테두리 부분이랑 여기 흰색으로
        // 된 부분 처리해줘"): the stations' gold floor rims, the flat white "dust screen" boards beside the aisle, the
        // old joists and lower-storey posts, the gold nails on the plank boxes, the old team-gate door art and the
        // threshold props. Pictures only; every collider and the gate's own TeamGate stay.
        static readonly HashSet<string> OldRepairWingNames = new HashSet<string>
        {
            "Rim W", "Rim E", "Rim S W", "Rim S E", "Rim N W", "Rim N E",
            "Dust screen cloth", "Dust screen rail", "Exposed floor joist", "Lower-storey scaffold post",
            "Lower-storey diagonal brace", "DEC_PlankJoinery", "DEC_PalaceGate", "DEC_ThresholdProps",
        };

        static void HideOldRepairWingDressing(MiniGameSection owner)
        {
            foreach (var t in owner.GetComponentsInChildren<Transform>(true))
                if (OldRepairWingNames.Contains(t.name)) Hide(t);
        }

        static void RepairWingHall(Transform content, Dictionary<string, Material> cache)
        {
            var arch = Load("Mid/B/B_TeamArch");
            if (arch != null)
                foreach (float x in new[] { -12f, 12f })
                {
                    string name = x < 0f ? "MODEL_TeamArch_W" : "MODEL_TeamArch_E";
                    if (content.Find(name) != null) continue;
                    var at = new GameObject(name).transform;
                    at.SetParent(content, false);
                    at.localPosition = new Vector3(x, 0f, 23.8f);
                    Hang(at, RideName, arch, cache);
                }
            var hall = Load("Mid/B/B_HallEnv");
            if (hall != null && content.Find("MODEL_Hall") == null)
            {
                Hang(content, "MODEL_Hall", hall, cache);
                MiniGameFixtures.LightSockets(content.Find("MODEL_Hall"));
            }
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            var facade = shell != null ? Find(shell, "ENV_NorthFacade") : null;
            if (facade == null || arch == null) return;
            foreach (var t in facade.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Team portal front") Hide(t);
                else if (t.name == "Sconce backplate" || t.name == "Lantern cage" || t.name == "Lantern glass" || t.name == "Lantern hood")
                    Hide(t);
            }
        }

        // The shell's centre door pictures in front of the block below `top` (closed door, crown arch, crown, inner
        // pilasters); the centre wall mass and its ashlar stay, framing a gate lower than the wall.
        static void HideCentreDoor(Transform content, float top)
        {
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            var facade = shell != null ? Find(shell, "ENV_NorthFacade") : null;
            if (facade == null) return;
            foreach (var renderer in facade.GetComponentsInChildren<Renderer>(true))
            {
                string n = renderer.name;
                if (n == "North wall mass" || n == "Ashlar face") continue;
                var b = renderer.bounds;
                var lo = content.InverseTransformPoint(b.min);
                var hi = content.InverseTransformPoint(b.max);
                float x0 = Mathf.Min(lo.x, hi.x), x1 = Mathf.Max(lo.x, hi.x);
                float y0 = Mathf.Min(lo.y, hi.y), z = (lo.z + hi.z) * .5f;
                if (x0 >= -9.7f && x1 <= 9.7f && z > 22.5f && z < 27f && y0 < top) renderer.enabled = false;
            }
        }

        static GameObject plankModel;

        // Also called by MgPlanks for each carried plank it makes at run time.
        public static void DressPlank(GameObject box)
        {
            if (box == null || box.transform.Find(RideName) != null) return;
            if (plankModel == null) plankModel = Resources.Load<GameObject>(Folder + "Mid/B/B_Plank");
            if (plankModel == null) return;   // no model: the flat box stays as it was
            var flat = box.GetComponent<Renderer>();
            if (flat != null) flat.enabled = false;
            Hang(box.transform, RideName, plankModel, new Dictionary<string, Material>());
        }

        // D · 정문 쇠살문 누각 (R505, ArtSource/PawnRush/MiniGames/Early/d_capstan_v1.py, d_center_gate_v1.py).
        // The user's capstan and centre gate fitted to the game's numbers. The capstan's drum stands beside the game's
        // turning "Capstan bars" (same place, base y 0); its bars ride "Capstan bars", so they turn with the pushed body.
        // The floor arrows, the overhead chain, the team gates and every collider stay the game's.
        static void Capstan(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            if (content == null) return;
            var cache = new Dictionary<string, Material>();
            var gate = Load("Early/D/D_CenterGate");
            if (gate != null && content.Find(GateName) == null)
            {
                var at = new GameObject(GateName).transform;
                at.SetParent(content, false);
                at.localPosition = new Vector3(0f, 0f, 23.69f);   // the closed central door's face (MiniGamePalaceArt.Shell)
                Hang(at, RideName, gate, cache);
            }
            if (gate != null) HideCentreFacade(content);

            // Team portals (d_team_gate_v1.py): the arch stands where the shell's "Team portal front" arch was (opening
            // 4.5 m, section x +-12, z 23.8); its piers were narrowed to 0.9 m so the inner one stands less in front of
            // the centre gate's wing.
            var arch = Load("Early/D/D_TeamArch");
            if (arch != null)
            {
                if (content.Find(ArchName) == null)
                {
                    var arches = new GameObject(ArchName).transform;
                    arches.SetParent(content, false);
                    foreach (float x in new[] { -12f, 12f })
                    {
                        var at = new GameObject("Team portal arch").transform;
                        at.SetParent(arches, false);
                        at.localPosition = new Vector3(x, 0f, 23.8f);
                        Hang(at, RideName, arch, cache);
                    }
                }
                var shell = content.Find(MiniGamePalaceArt.ShellName);
                if (shell != null)
                    foreach (var t in shell.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Team portal front") Hide(t);
            }

            Court(content, cache);
            Cypresses(owner, content, cache);
            Banners(content);
            Paving(owner, content);
            var portcullis = Load("Early/D/D_Portcullis");
            var drum = Load("Early/D/D_Capstan_Static");
            var bars = Load("Early/D/D_Capstan_Bars");
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                // The portcullis hangs from the bottom of the game's team gate (a scaled box), so it rises with the
                // progress peek and the full lift; the R312 iron-bar picture on the gate stops drawing.
                var door = station.Gate != null ? station.Gate.transform : null;
                if (door != null && portcullis != null)
                {
                    Hide(door.Find("DEC_PalaceGate"));
                    if (door.Find(PortcullisName) == null)
                    {
                        var at = new GameObject(PortcullisName).transform;
                        at.SetParent(door, false);
                        var s = door.localScale;
                        at.localScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(s.x)), 1f / Mathf.Max(.0001f, Mathf.Abs(s.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(s.z)));
                        at.localPosition = new Vector3(0f, -.5f, 0f);   // the box's bottom face, in its own (scaled) units
                        Hang(at, RideName, portcullis, cache);
                        // A jolt at every full capstan turn (picture only; the gate's own lift and collider unchanged).
                        at.gameObject.AddComponent<MiniGamePortcullisKick>().Watch(station, MgCapstan.Turns);
                    }
                }
                if (station.Game == null) continue;
                var game = station.Game.transform;
                var turning = Find(game, "Capstan bars");
                if (turning == null) continue;
                if (drum != null)
                {
                    if (turning.parent.Find(DrumName) == null)
                    {
                        var at = new GameObject(DrumName).transform;
                        at.SetParent(turning.parent, false);
                        at.localPosition = turning.localPosition;
                        Hang(at, RideName, drum, cache);
                    }
                    Hide(Find(game, "Capstan drum"));
                    Hide(Find(game, "Capstan cap"));
                }
                if (bars != null)
                {
                    foreach (Transform part in turning)
                        if (part.name != RideName) Hide(part);   // the four bars, their gold grips, the crown
                    if (turning.Find(RideName) == null) Hang(turning, RideName, bars, cache);
                }
                CapstanChain(station, game, turning, content, cache);
                // Sounds and effects (MiniGameCourtFx.cs): the turning creak and chain loops on the bars, the gate's
                // step / open moments on the station in place of the shared ones.
                if (turning.GetComponent<MiniGameCapstanSound>() == null)
                {
                    float side = Mathf.Sign(content.InverseTransformPoint(station.transform.position).x);
                    var court = content.Find(CourtName);
                    var wheel = court != null ? court.Find(side < 0f ? "Pulley wheel W" : "Pulley wheel E") : null;
                    turning.gameObject.AddComponent<MiniGameCapstanSound>().Setup(wheel);
                }
                if (station.GetComponent<MiniGameCourtGateFx>() == null) station.gameObject.AddComponent<MiniGameCourtGateFx>();
                // the portcullis hauls up slowly, as long as its chain sound (user, 11-07: "좀더 천천히 열리게 하고 타이밍맞추고")
                if (station.Gate != null) station.Gate.Seconds = MiniGameCourtGateFx.OpenSeconds;
            }
        }

        // ---- D court dressing (R505, ArtSource/PawnRush/MiniGames/Early/d_props_v1.py) ----
        // The user's remaining D parts, re-set around the game (the user, 11-07: the old shell walls may go, and walls
        // must run on without gaps). Section coordinates: x across (stations at +-12), z from the entry (0) to the exit
        // (36). Colliders, doors and every game device stay the game's; these are pictures.
        const string CourtName = "MODEL_Court";
        const float WallScale = 1.3f;              // the 4.5 x 2.5 m module -> 5.85 x 3.25 m, the shell's 3.2 m walls
        static readonly float[] SideWallZ = { 2.5f, 7.6f, 12.7f, 17.9f, 23f, 28.1f, 33.3f };   // ~0.7 m overlaps, z -0.4..36.2
        static readonly float[] EntryWallX = { 9.175f, 14.125f, 19.075f };                      // |x| 6.25..22, ~0.9 m overlaps
        static readonly Vector2 Stagger = new Vector2(.012f, .01f);                             // out, up: every other module
        static readonly Vector2 TowerAt = new Vector2(23f, 25f);   // x, z of the east corner tower
        // MiniGamePalaceArt.Facade: the early north wall's top; the scaled D_Wall's crenel floor; parapet module centres
        const float NorthTop = 11f, ParapetCrenel = 2.05f;
        static readonly float[] ParapetX = { 12.4f, 17.25f, 21.2f };
        const float TowerYaw = 37f;                                    // its window toward the room's middle
        const float ArchTop = 7.25f, PulleyZ = 23.65f, PulleyAxleUp = 1.91f;   // d_props_v1.py FORK axle z
        // d_props_v1.py: the wheel's groove (rim x 0.9), the straight chain model's length (CHAIN_LINK, 8 links of
        // 0.34 m); d_capstan_v1.py: the drum's upper band at 1.35 m, its face 0.75 m from the axis.
        const float WheelGroove = .88f, ChainOffDrumUp = 1.35f, ChainInDrum = .35f, ChainIntoArch = .6f;
        const float LinkLength = .34f, LinkWidth = .22f, LinkWire = .055f;      // as d_props_v1.py CHAIN_LINK

        static void Court(Transform content, Dictionary<string, Material> cache)
        {
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell != null)
            {
                foreach (var t in shell.GetComponentsInChildren<Transform>(true))
                    switch (t.name)
                    {
                        case "ENV_SideWall": case "Rear passage side wall": case "Entry wing wall": case "DEC_ThresholdProps":
                        // the garden edge's old ball-tier cypresses and planters (user, 11-07: "옛날 나무는 지워주고");
                        // the Blender cypresses (Cypresses) take their place
                        case "Planter foot": case "Planter bowl": case "Planter rim": case "Planter foliage": case "Planter flower":
                        case "Cypress tier":
                            Hide(t);
                            break;
                    }
                NorthWall(content, shell);
                // the garden edge's plain planters and benches at the entry and the rear corridor (replaced below)
                foreach (var renderer in shell.GetComponentsInChildren<Renderer>(true))
                {
                    var c = content.InverseTransformPoint(renderer.bounds.center);
                    foreach (var spot in new[] { new Vector2(20.8f, 2f), new Vector2(20.8f, 33f), new Vector2(18.7f, 2f), new Vector2(18.7f, 33f) })
                        if (new Vector2(Mathf.Abs(c.x) - spot.x, c.z - spot.y).magnitude < 1.2f && c.y < 3f) renderer.enabled = false;
                }
            }
            if (content.Find(CourtName) != null) return;
            var court = new GameObject(CourtName).transform;
            court.SetParent(content, false);

            Transform Place(string model, Vector3 at, float yaw, float scale = 1f, string name = null)
            {
                var asset = Load("Early/D/" + model);
                if (asset == null) return null;
                var holder = new GameObject(name ?? model).transform;
                holder.SetParent(court, false);
                holder.localPosition = at;
                holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
                holder.localScale = Vector3.one * scale;
                Hang(holder, RideName, asset, cache);
                return holder;
            }

            foreach (float s in new[] { -1f, 1f })
            {
                // Walls: the module's face (Blender -y) is the holder's -z; turned to face the room.
                // Neighbouring modules overlap ~0.7 m; with the same height and depth their merlon tops, faces and cores
                // lay in one plane there and flickered (user, 11-07: "깜빡 거리는 부분 있어", R498's rule). Every other
                // module stands Stagger.y higher and Stagger.x further out, so no two faces share a plane.
                for (int i = 0; i < SideWallZ.Length; i++)
                {
                    float k = i & 1;
                    Place("D_Wall", new Vector3(s * (22.6f + k * Stagger.x), k * Stagger.y, SideWallZ[i]), s < 0f ? -90f : 90f, WallScale);
                }
                for (int i = 0; i < EntryWallX.Length; i++)
                {
                    float k = i & 1;
                    Place("D_Wall", new Vector3(s * EntryWallX[i], k * Stagger.y, -.6f - k * Stagger.x), 180f, WallScale);
                    // Tripo made only the module's face; its back has holes. The camera starts outside the entry wall
                    // and saw them (user, 11-07: "여기 깨졌다"), so a second module stands back to back, facing out
                    // (staggered the other way, so it never shares a plane with the inner one either).
                    Place("D_Wall", new Vector3(s * EntryWallX[i], (1f - k) * Stagger.y + .005f, -1.0f - k * Stagger.x), 0f, WallScale, "D_Wall outside");
                }
                // Lanterns on the side walls' inner face (backplate on Blender -x): unlit by day; light is code's later.
                foreach (float z in new[] { 6f, 18f, 30f })
                {
                    // The lantern is an empty cage; its flame and light are code (MiniGameFlame) at the cage's middle
                    // (d_props_v1.py: cage axis 0.16 m out from the plate, its open middle ~0.22 m up).
                    var lantern = Place("D_Lantern", new Vector3(s * 21.62f, 1.8f, z), s < 0f ? 0f : 180f);
                    if (lantern != null) MiniGameFlame.Light(lantern, new Vector3(.16f, .22f, 0f), new Color(1f, .7f, .38f), 1.4f, 4.5f);
                }
                // Entrance posts flanking each station's ramp.
                foreach (float dx in new[] { -2.9f, 2.9f }) Place("D_GatePost", new Vector3(s * 12f + dx, 0f, 3f), 0f);
                // Entry-band props where the R312 threshold props were, and the rear corridor's.
                Place("D_BarRack", new Vector3(s * 19.6f, 0f, 1.4f), 180f);
                Place("D_ChainPile", new Vector3(s * 16.6f, 0f, 1.6f), 0f);
                Place("D_FlowerPot", new Vector3(s * 7.4f, 0f, 1.0f), 0f);
                Place("D_HelmetBench", new Vector3(s * 20.8f, 0f, 33f), s < 0f ? -90f : 90f);
                Place("D_FlowerPot", new Vector3(s * 18.7f, 0f, 33f), 0f);
                // The pulley over each team portal, standing on the arch's top; its wheel on the frame's axle.
                Place("D_PulleyFrame", new Vector3(s * 12f, ArchTop, PulleyZ), 0f, 1f, s < 0f ? "Pulley W" : "Pulley E");
                Place("D_PulleyWheel", new Vector3(s * 12f, ArchTop + PulleyAxleUp, PulleyZ + .02f), 0f, 1f, s < 0f ? "Pulley wheel W" : "Pulley wheel E");
                // A crenellated parapet along the north wall's 11 m top, gate block to corner tower: D_Wall modules sunk
                // into the 1.9 m deep wall mass (z 24.05..25.95) so only their merlons stand above it (crenel floor 2.05 m
                // up the scaled module), every other one staggered like the side walls.
                for (int i = 0; i < ParapetX.Length; i++)
                {
                    float k = i & 1;
                    Place("D_Wall", new Vector3(s * ParapetX[i], NorthTop - ParapetCrenel + k * Stagger.y, 24.9f + k * Stagger.x), 0f, WallScale, "D_Wall parapet");
                }
                // The corner tower (d_tower_v1.py, 13.5 m, above the 11 m north wall) where the side wall's line meets the
                // north wall's middle; its window (the holder's -z) turned toward the room. Its base (radius 2 m) reaches
                // about 1 m into the station's back corner, clear of the cypress there.
                Place("D_CornerTower", new Vector3(s * TowerAt.x, 0f, TowerAt.y), s * TowerYaw, 1f, s < 0f ? "Corner tower W" : "Corner tower E");
            }
        }

        // The concept's cypresses (ArtSource/PawnRush/MiniGames/Early/d_cypress_v1.py, built in Blender: user, 11-07),
        // in their stone planters on each station's outer strip, between the push ring (|x| < 15.6) and the side wall
        // (|x| 22), the last in the back corner beside the team arch. They stand on the
        // station floor's top. Pictures only, like the rest of the court: no colliders.
        // Three a side where the shell's old ones stood (z 8, 14, 21), clear of the lanterns at z 6, 18 (user, 11-07:
        // "2개 더 놔": one more a side).
        static readonly Vector3[] CypressSpots = { new Vector3(20.3f, 0f, 9.2f), new Vector3(20.3f, 0f, 14.6f), new Vector3(20.3f, 0f, 21.6f) };
        static readonly float[] CypressYaw = { 0f, 251f, 137f }, CypressScale = { 1f, .97f, .93f };

        static void Cypresses(MiniGameSection owner, Transform content, Dictionary<string, Material> cache)
        {
            var court = content.Find(CourtName);
            if (court == null) return;
            var old = new List<GameObject>();
            foreach (Transform t in court) if (t.name == "D_Cypress") old.Add(t.gameObject);
            if (old.Count == CypressSpots.Length * 2) return;
            foreach (var go in old) Object.DestroyImmediate(go);   // an earlier layout: lay them again
            var asset = Load("Early/D/D_Cypress");
            if (asset == null) return;
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                float s = Mathf.Sign(content.InverseTransformPoint(station.transform.position).x);
                float top = 0f;
                foreach (var r in station.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Station floor") top = content.InverseTransformPoint(r.bounds.max).y;
                for (int i = 0; i < CypressSpots.Length; i++)
                {
                    var holder = new GameObject("D_Cypress").transform;
                    holder.SetParent(court, false);
                    holder.localPosition = new Vector3(s * CypressSpots[i].x, top, CypressSpots[i].z);
                    holder.localRotation = Quaternion.Euler(0f, CypressYaw[i] * s, 0f);
                    holder.localScale = Vector3.one * CypressScale[i];
                    Hang(holder, RideName, asset, cache);
                }
            }
        }

        // The north wall's outer stretches (|x| 9.7..22, beside the centre gate model) were plain shell boxes: the old
        // square rook tower with its stepped slate roof, pilasters and flat ashlar slabs (user, 11-07: "여기도 해"). The
        // tower is replaced by the corner tower and the slabs and pilasters go; the wall masses, the walls over the team
        // portals, the gate pocket covers and the cornices wear the walls' tinted stone brick (PR_Early_WallCore, 2 m a
        // tile), repeated by each box's size through a property block. Colliders stay.
        static void NorthWall(Transform content, Transform shell)
        {
            var facade = Find(shell, "ENV_NorthFacade");
            if (facade == null) return;
            var brick = Resources.Load<Material>(OwnMaterials + "PR_Early_WallCore");
            var block = new MaterialPropertyBlock();
            foreach (var r in facade.GetComponentsInChildren<Renderer>(true))
            {
                var c = content.InverseTransformPoint(r.bounds.center);
                bool outer = Mathf.Abs(c.x) > 9.7f || r.name == "Facade cornice";
                if (!outer) continue;
                switch (r.name)
                {
                    case "Ashlar face": case "Column plinth": case "Column shaft": case "Column capital": case "Column brass fillet":
                        r.enabled = false;
                        break;
                    case "North wall mass": case "Portal upper wall": case "Gate pocket cover": case "Facade cornice":
                        if (brick == null) break;
                        var size = content.InverseTransformVector(r.bounds.size);
                        r.sharedMaterial = brick;
                        r.GetPropertyBlock(block);
                        block.SetVector(BaseMapST, new Vector4(Mathf.Abs(size.x) / 2f, Mathf.Abs(size.y) / 2f, 0f, 0f));
                        r.SetPropertyBlock(block);
                        break;
                }
            }
            foreach (var t in facade.GetComponentsInChildren<Transform>(true))
                if (t.name == "ENV_RookGatehouse") Hide(t);
        }

        // The shell's banners in D: the team banners hung where the pulley wheel now turns, so they come down onto the
        // arch's face; then every banner's stiff cloth box becomes a swaying cloth (MiniGameBannerCloth, the user's rule:
        // hanging cloth is code). The rod stays; the brass border strips and the pawn emblem on the cloth go, since
        // they cannot sway with it. Runs on every Apply (a rebuilt shell is redone) and marks what it has done.
        static void Banners(Transform content)
        {
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell == null) return;
            var facade = Find(shell, "ENV_NorthFacade");
            if (facade != null)
                foreach (Transform t in facade)
                {
                    if (!t.name.StartsWith("Banner") || t.name.Contains(Done) || t.GetComponent<MiniGameBannerCloth>() != null) continue;
                    var p = t.localPosition;
                    if (Mathf.Abs(Mathf.Abs(p.x) - 12f) < 1.6f && p.y > 5.5f) { t.localPosition = p + new Vector3(0f, -1.6f, -.9f); t.name += Done; }
                }
            var cloths = new List<Transform>();
            foreach (var t in shell.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Banner cloth") && !t.name.Contains(Swapped) && t.GetComponent<MiniGameBannerCloth>() == null) cloths.Add(t);
            foreach (var box in cloths)
            {
                var r = box.GetComponent<Renderer>();
                if (r == null) continue;
                Vector3 c = box.localPosition, size = box.localScale;
                foreach (Transform t in box.parent)
                    if ((t.name.StartsWith("Banner border") || t.name.StartsWith("Banner pawn")) &&
                        Mathf.Abs(t.localPosition.x - c.x) < size.x * .5f + .1f && Mathf.Abs(t.localPosition.y - c.y) < size.y * .5f + .1f)
                        Hide(t);
                MiniGameBannerCloth.Hang(box.parent, new Vector3(c.x, c.y + size.y * .5f, c.z - size.z * .5f - .02f), size.x, size.y, r.sharedMaterial);
                r.enabled = false;
                box.name += Swapped;
            }
        }

        const string Done = " (moved)", Swapped = " (swaying)";

        // The courtyard floor in D: cream flagstones (ArtSource/PawnRush/MiniGames/Early/Materials/r505_paving.py, 2 m a
        // tile) on the shell's paving modules (2 m each: one tile) and on the game's own floors (entry band, central
        // aisle, rear corridor, each station's floor, ramp and gate passage), repeated by each box's size through a
        // property block so the stones keep their size. Colliders and every other material stay.
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        static void Paving(MiniGameSection owner, Transform content)
        {
            var stone = Resources.Load<Material>(OwnMaterials + "PR_Early_Paving");
            if (stone == null) return;
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell != null)
                foreach (var r in shell.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Paving module") r.sharedMaterial = stone;
            void Lay(Transform t)
            {
                if (t == null) return;
                var r = t.GetComponent<Renderer>();
                if (r == null) return;
                r.sharedMaterial = stone;
                var size = t.lossyScale;
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block);
                block.SetVector(BaseMapST, new Vector4(Mathf.Abs(size.x) / 2f, Mathf.Abs(size.z) / 2f, 0f, 0f));
                r.SetPropertyBlock(block);
            }
            foreach (var name in new[] { "Entry band", "Rear corridor", "Central aisle" }) Lay(content.Find(name));
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                var passages = new List<Transform>();
                foreach (var t in station.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Station floor" || t.name == "Gate passage" || t.name == "Palace station ramp") Lay(t);
                    if (t.name == "Gate passage") passages.Add(t);
                }
                foreach (var t in passages) PassageEdges(t, Lay);
            }
        }

        // The team gate passage's floor is 4 m wide but the arch model's piers and the tunnel stand 2.25 m out: the
        // sky showed through a strip each side (user, 11-07: "이런 부분 바닥에 틈 안생기게 해"). A picture-only
        // paving strip of the floor's height and length lies along each edge, out under the piers (no collider, no
        // shared face with the floor but its edge).
        const float PassageEdge = .6f;
        const string PassageEdgeName = "Gate passage edge";

        static void PassageEdges(Transform floor, System.Action<Transform> lay)
        {
            var parent = floor.parent;
            foreach (Transform t in parent) if (t.name == PassageEdgeName) return;
            Vector3 c = floor.localPosition, size = floor.localScale;
            foreach (float side in new[] { -1f, 1f })
            {
                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(strip.GetComponent<Collider>());
                strip.name = PassageEdgeName;
                var t = strip.transform;
                t.SetParent(parent, false);
                t.localPosition = c + Vector3.right * side * (Mathf.Abs(size.x) + PassageEdge) * .5f;
                t.localRotation = floor.localRotation;
                t.localScale = new Vector3(PassageEdge, size.y, size.z);
                lay(t);
            }
        }

        // The capstan's chain (MiniGamePalaceArt "Chain link", 2.5 m high to the gate) stops drawing. A chain of links
        // (MiniGamePulleyChain) leaves the drum's upper band, wraps over the pulley wheel's groove and drops into the
        // arch; turning the capstan turns the wheel and runs the links over it (user, 11-07: wheel and chain really
        // working together). The path lies in the wheel's plane (x = the station's x), drawn in the court's space.
        static void CapstanChain(MissionStation station, Transform game, Transform turning, Transform content, Dictionary<string, Material> cache)
        {
            float side = Mathf.Sign(content.InverseTransformPoint(station.transform.position).x);
            var wheel = content.Find(CourtName + "/" + (side < 0f ? "Pulley wheel W" : "Pulley wheel E"));
            if (wheel == null) return;
            foreach (var t in game.GetComponentsInChildren<Transform>(true))
                if (t.name == "Chain link") Hide(t);
            string chainName = "Capstan chain " + (side < 0f ? "W" : "E");
            var court = wheel.parent;
            if (court.Find(chainName) != null) return;
            var root = new GameObject(chainName).transform;
            root.SetParent(court, false);

            var c = wheel.localPosition;                                    // the wheel's axle (court space)
            var drum = court.InverseTransformPoint(turning.position);       // the capstan's axis at the bars' floor
            float dz = Mathf.Sign(c.z - drum.z);
            // (z, y) in the wheel's plane. Start inside the drum so the links come out of its face.
            var start = new Vector2(drum.z + dz * ChainInDrum, drum.y + ChainOffDrumUp);
            var centre = new Vector2(c.z, c.y);
            var toStart = start - centre;
            float d = toStart.magnitude, baseAngle = Mathf.Atan2(toStart.y, toStart.x), spread = Mathf.Acos(Mathf.Clamp(WheelGroove / d, -1f, 1f));
            float t1 = baseAngle + spread, t2 = baseAngle - spread;
            float near = Mathf.Sin(t1) > Mathf.Sin(t2) ? t1 : t2;           // the tangent on the wheel's upper side
            float far = dz > 0f ? 0f : Mathf.PI;                             // leaves the wheel level, on the gate's side
            var path = new List<Vector3> { new Vector3(c.x, start.y, start.x) };
            const int steps = 24;
            // round over the top, from the near tangent to the far side
            if (dz > 0f) { while (near < far) near += Mathf.PI * 2f; while (near - far > Mathf.PI * 2f) near -= Mathf.PI * 2f; }
            else { while (near > far) near -= Mathf.PI * 2f; while (far - near > Mathf.PI * 2f) near += Mathf.PI * 2f; }
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(near, far, i / (float)steps);
                path.Add(new Vector3(c.x, centre.y + Mathf.Sin(a) * WheelGroove, centre.x + Mathf.Cos(a) * WheelGroove));
            }
            var drop = path[path.Count - 1];
            path.Add(new Vector3(c.x, ArchTop - ChainIntoArch, drop.z));   // down into the arch's top
            var mesh = MiniGamePulleyChain.LinkMesh(LinkLength, LinkWidth, LinkWire);
            var iron = Resources.Load<Material>(OwnMaterials + "PR_Early_DarkIron");
            root.gameObject.AddComponent<MiniGamePulleyChain>().Setup(turning, wheel, 2f, path, WheelGroove, LinkLength - 2f * LinkWire, mesh, iron);
        }

        // The R312 shell's pictures in the centre block's front (x -9.7..9.7, z 22.5..27): the centre wall mass and its
        // ashlar, the closed ceremonial door, the crown arch, the crown and the two inner pilasters. Picked by place so
        // the team portals (x +-12) and the full-width cornices stay.
        static void HideCentreFacade(Transform content)
        {
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            var facade = shell != null ? Find(shell, "ENV_NorthFacade") : null;
            if (facade == null) return;
            foreach (var renderer in facade.GetComponentsInChildren<Renderer>(true))
            {
                var b = renderer.bounds;
                var lo = content.InverseTransformPoint(b.min);
                var hi = content.InverseTransformPoint(b.max);
                float x0 = Mathf.Min(lo.x, hi.x), x1 = Mathf.Max(lo.x, hi.x);
                float z = (lo.z + hi.z) * .5f;
                if (x0 >= -9.7f && x1 <= 9.7f && z > 22.5f && z < 27f) renderer.enabled = false;
            }
        }

        // C · 해자 도개교 (ArtSource/PawnRush/MiniGames/Early/c_drawbridge_v1.py).
        static void Drawbridge(MiniGameSection owner)
        {
            if (DrawbridgeV2(owner)) return;                   // R502: the user's models (MiniGameModels.Drawbridge.cs)
            var content = owner.transform.Find(MiniGameSection.ContentName);
            var env = Load("Early/C_Drawbridge_Env");
            if (content == null || env == null) return;
            var cache = new Dictionary<string, Material>();
            if (content.Find(EnvName) == null) Hang(content, EnvName, env, cache);
            Hide(content.Find(MiniGamePalaceArt.ShellName));
            foreach (var floor in new[] { "Entry band", "Rear corridor", "Central aisle south", "Central aisle north" })
                Hide(content.Find(floor));

            var deck = Load("Early/C_Drawbridge_Deck");
            var drum = Load("Early/C_Drawbridge_Drum");
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                Hide(station.transform.Find("Gate passage"));
                var game = station.Game != null ? station.Game.transform : null;
                if (game == null) continue;
                foreach (var t in game.GetComponentsInChildren<Transform>(true))
                    switch (t.name)
                    {
                        case "Moat wall S look": case "Moat wall N look": case "Moat wall W": case "Moat wall E": case "Moat bottom":
                        case "Bridge tower": case "Winch frame": case "Winch post E": case "Rope lane": case "Lane tick": case "Lane arrow":
                            Hide(t);
                            break;
                    }
                var hinge = Find(game, "Drawbridge hinge");
                if (hinge != null && deck != null)
                {
                    foreach (Transform part in hinge)
                        if (part.name != RideName) Hide(part);
                    if (hinge.Find(RideName) == null) Hang(hinge, RideName, deck, cache);
                }
                var drumAt = Find(game, "Drum");
                if (drumAt != null && drum != null)
                {
                    foreach (Transform part in drumAt)
                        if (part.name != RideName) Hide(part);
                    if (drumAt.Find(RideName) == null) Hang(drumAt, RideName, drum, cache);
                }
            }
        }

        static GameObject Load(string name)
        {
            var model = Resources.Load<GameObject>(Folder + name);
            if (model == null) Debug.LogWarning("[미니게임 모델] 불러오지 못했습니다: Resources/" + Folder + name + ".fbx — 코드로 만든 그림을 그대로 씁니다");
            return model;
        }

        // The model under a pivot turned 180 degrees about y, as the garden's (Blender x, y, z = the parent's x, z, y).
        static void Hang(Transform parent, string name, GameObject model, Dictionary<string, Material> cache)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var copy = Object.Instantiate(model, pivot, false);
            copy.name = model.name;
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null) slots[i] = KitMaterial(slots[i], cache);
                renderer.sharedMaterials = slots;
            }
        }

        // "GC | c slate" (Unity imports it as "GC _ c slate") -> Materials/GC_c_slate, as the garden's ConceptMaterial.
        static Material KitMaterial(Material imported, Dictionary<string, Material> cache)
        {
            string name = System.Text.RegularExpressions.Regex.Replace(imported.name.Replace(" (Instance)", ""), "[^A-Za-z0-9]+", "_").Trim('_');
            if (!cache.TryGetValue(name, out var material))
            {
                material = Resources.Load<Material>(OwnMaterials + name);
                if (material == null) material = Resources.Load<Material>(Materials + name);
                if (material == null) Debug.LogWarning("[미니게임 모델] 재질이 없습니다: Resources/" + Materials + name);
                cache[name] = material;
            }
            return material != null ? material : imported;
        }

        // Stops `root`'s pictures drawing; colliders and the game's components are untouched.
        static void Hide(Transform root)
        {
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
