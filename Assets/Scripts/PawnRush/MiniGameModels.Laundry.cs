using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // J · 왕궁 세탁장 (R509, ArtSource/PawnRush/MiniGames/Mid/J/j_props_v1.py): the user's Tripo wash tub, sluice gate leaf
    // and sluice wheel from the GPT asset sheet (Docs/PawnRush/ConceptArt/R509_PalaceLaundry/AssetRefs), and a bubble
    // nozzle built in Blender, fitted to MgKnightPads' numbers. Pictures only: every road square's collider, the pads'
    // jump, the pieces, the water and the team gate stay the game's.
    //   "Road xx"     -> wash tub 2.2 m across, its lid top on the square's top; the square box, its R312 barrel and the
    //                    square's gold rims stop drawing. The gold road number stays (a game marker).
    //   "Jump pad xx" -> brass bubble nozzle on the lid; the pad's discs stop drawing, its gold "Pad ring" stays. The
    //                    nozzle rides the pad, so it squashes with the pad's bounce.
    //   team gate     -> the sluice gate leaf (3.85 x 4.55 m like every team door, its lifting bar above); it rises with
    //                    the gate (TeamGate lifts it).
    //   beside it     -> the brass sluice wheel on the wall face, outside each team door.
    //   room / basin  -> LaundryHall, LaundryBasin; props (LaundryHall), washing lines (LaundryLines), the dolls
    //                    on the pieces (LaundryDolls) below.
    public static partial class MiniGameModels
    {
        const string LaundryFolder = "Mid/J/";
        const string TubName = "MODEL_Tub ";

        static void PalaceLaundry(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            if (content == null) return;
            var cache = new Dictionary<string, Material>();
            var tub = Load(LaundryFolder + "J_Tub");
            var nozzle = Load(LaundryFolder + "J_Nozzle");
            var leaf = Load(LaundryFolder + "J_Leaf");
            var wheel = Load(LaundryFolder + "J_Wheel");
            var basin = Load(LaundryFolder + "J_BasinEnv");
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                var door = station.Gate != null ? station.Gate.transform : null;
                if (door != null && leaf != null)
                {
                    Hide(door.Find("DEC_PalaceGate"));
                    if (door.Find(RideName + " holder") == null)
                    {
                        // The leaf model is the game's door size, centred: undo the door box's scale (as A's team door).
                        var at = new GameObject(RideName + " holder").transform;
                        at.SetParent(door, false);
                        var s = door.localScale;
                        at.localScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(s.x)), 1f / Mathf.Max(.0001f, Mathf.Abs(s.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(s.z)));
                        Hang(at, RideName, leaf, cache);
                    }
                }
                if (station.Game == null) continue;
                LaundryRoad(station.Game.transform, tub, nozzle, cache);
                LaundryDolls(station, station.Game.transform, cache);
                LaundryBasin(station, basin, cache);
            }
            LaundryWheels(content, wheel, cache);
            LaundryHall(owner, content, cache);
            LaundryLines(content);
        }

        // The concept's high washing lines with white sheets and towels (user 11-08: hanging cloth is code). One line per
        // side-wall window bay (j_hall_v1 / e_hall_v1: 5 m modules centred z 2.5..22.5), 0.8 m out from the wall face
        // (x +-22) at 4.2 m, under the gallery and over the basins' wall edge, so nothing hangs over the jump road or
        // across the camera's view. Each sheet is the swaying two-sided cloth (MiniGameBannerCloth, R505) in the undyed
        // linen (PR_Mid_J_Linen, 0.5 m tile); a cream line between two brass arms from the wall. Pictures only.
        static readonly float[] LineZ = { 2.5f, 7.5f, 12.5f, 17.5f, 22.5f };
        // width, length of the cloths on a line (repeated in turn along the lines): sheets and towels
        static readonly Vector2[] Wash = { new Vector2(1.3f, 1.5f), new Vector2(.7f, .9f), new Vector2(1.1f, 1.25f), new Vector2(.6f, .8f), new Vector2(1.2f, 1.4f) };

        static void LaundryLines(Transform content)
        {
            if (content.Find("MODEL_WashingLines") != null) return;
            var linen = Resources.Load<Material>(OwnMaterials + "PR_Mid_J_Linen");
            var rope = Resources.Load<Material>(OwnMaterials + "PR_Mid_Canvas");
            var brass = Resources.Load<Material>(OwnMaterials + "PR_Mid_MatteBrass");
            if (linen == null) return;
            var root = new GameObject("MODEL_WashingLines").transform;
            root.SetParent(content, false);
            var block = new MaterialPropertyBlock();
            int k = 0;
            const float half = 2.15f, height = 4.2f, outFromWall = .8f;
            foreach (float side in new[] { -1f, 1f })
                foreach (float z in LineZ)
                {
                    // the cloth faces its local -z: turned so that is the room (+x on the west wall, -x on the east)
                    var line = new GameObject("Washing line").transform;
                    line.SetParent(root, false);
                    line.localPosition = new Vector3(side * (22f - outFromWall), height, z);
                    line.localRotation = Quaternion.Euler(0f, side < 0f ? -90f : 90f, 0f);
                    if (rope != null)
                    {
                        var cord = LaundryPart(PrimitiveType.Cylinder, line, "Line cord", Vector3.zero, new Vector3(.03f, half, .03f), rope);
                        cord.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                    if (brass != null)
                        foreach (float x in new[] { -half, half })
                        {
                            // an arm from the wall (local +z) to the line's end, and a ball where the line ties on
                            var arm = LaundryPart(PrimitiveType.Cylinder, line, "Line arm", new Vector3(x, 0f, outFromWall * .5f), new Vector3(.05f, outFromWall * .5f, .05f), brass);
                            arm.localRotation = Quaternion.Euler(90f, 0f, 0f);
                            LaundryPart(PrimitiveType.Sphere, line, "Line knob", new Vector3(x, 0f, 0f), Vector3.one * .1f, brass);
                        }
                    // cloths along the line, a small gap between them, centred
                    var sizes = new List<Vector2>();
                    float used = 0f;
                    while (true)
                    {
                        var s = Wash[k % Wash.Length];
                        if (used + s.x + (sizes.Count > 0 ? .15f : 0f) > 2f * half - .3f) break;
                        used += s.x + (sizes.Count > 0 ? .15f : 0f);
                        sizes.Add(s);
                        k++;
                    }
                    float at = -used * .5f;
                    foreach (var s in sizes)
                    {
                        var cloth = MiniGameBannerCloth.Hang(line, new Vector3(at + s.x * .5f, -.02f, 0f), s.x, s.y, linen);
                        var r = cloth.GetComponent<Renderer>();
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        block.Clear();
                        block.SetVector("_BaseMap_ST", new Vector4(s.x / .5f, s.y / .5f, 0f, 0f));
                        r.SetPropertyBlock(block);
                        if (rope != null)
                            foreach (float px in new[] { at + .08f, at + s.x - .08f })
                                LaundryPart(PrimitiveType.Cube, line, "Clothes peg", new Vector3(px, -.02f, -.02f), new Vector3(.03f, .09f, .04f), rope);
                        at += s.x + .15f;
                    }
                }
        }

        static Transform LaundryPart(PrimitiveType type, Transform parent, string name, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        // The room (R509, J/j_hall_v1.py; the user 11-08: reuse the other mini-games' models). J's section is E's, so the
        // room is E's hall (the user's R507 walls, gate, arches, beams, ashlar fill, walnut ceiling) without its wall
        // bookcases and with a white tile dado under a walnut cap rail. E's chandeliers and sconces (flames from code),
        // E's stone frames round the team gate tunnels, E's team pennants and E's floors on the game's floor boxes are
        // reused as they are. The R312 shell stops drawing; every collider stays the section builder's.
        static void LaundryHall(MiniGameSection owner, Transform content, Dictionary<string, Material> cache)
        {
            var hall = Load(LaundryFolder + "J_HallEnv");
            if (hall == null) return;
            if (content.Find("MODEL_Hall") == null) Hang(content, "MODEL_Hall", hall, cache);
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            LibraryFloors(owner, content);     // before the shell is hidden: it finds the shell's paving there
            Hide(shell);
            var lights = Load("Mid/E/E_LightsEnv");
            if (lights != null && content.Find("MODEL_Lights") == null)
            {
                Hang(content, "MODEL_Lights", lights, cache);
                MiniGameFixtures.LightSockets(content.Find("MODEL_Lights"));
            }
            // Props modelled in Blender (user 11-08, j_laundry_props_v1.py: laundry baskets, folded sheets, washboards, soap
            // crates, drying racks, buckets, benches) in the entry band's outer corners and the rear corridor's ends,
            // off the pawns' ways. Pictures only. The R312 threshold props stop drawing (they went with the shell too).
            var props = Load(LaundryFolder + "J_PropsEnv");
            if (props != null && content.Find("MODEL_Props") == null) Hang(content, "MODEL_Props", props, cache);
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
                if (t.name == "DEC_ThresholdProps") Hide(t);
            var arch = Load("Mid/E/E_DoorArch");
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
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                float x = station.transform.localPosition.x;
                string name = x < 0f ? "MODEL_Pennant_W" : "MODEL_Pennant_E";
                if (content.Find(name) == null)
                    MiniGameFixtures.Pennant(content, name, new Vector3(x, 7.0f, 23.25f), 2.0f, 1.6f, MiniGamePalaceArt.TeamColor(station.Team));
            }
        }

        // The basin's white tile (J_BasinEnv, built in the white station's game frame: x = v, z = u, y 0 = the dais top) on
        // the game's basin walls and floor; black's station is the mirror (x = -v), so its copy is flipped in x. The
        // game's stone basin pictures and the R312 tile strips stop drawing; the water and every collider stay.
        static void LaundryBasin(MissionStation station, GameObject basin, Dictionary<string, Material> cache)
        {
            if (basin == null) return;
            var game = station.Game.transform;
            if (game.Find("MODEL_Basin") == null)
            {
                var at = new GameObject("MODEL_Basin").transform;
                at.SetParent(game, false);
                at.localScale = new Vector3(MiniGameParts.Mirror(station), 1f, 1f);
                Hang(at, RideName, basin, cache);
            }
            foreach (var t in game.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Water wall") || t.name == "Water bottom") Hide(t);
            // The soapy water (user 11-08 "비눗물 셰이더", then "물 처럼 안보여"): J's indoor water shader (PR_SoapyWater.shader,
            // C's moat water kept alive under the ceiling: caustics, room-light glints, a window sheen, wobbling tiles), milky
            // pale teal with white foam along the tiles and round every tub (PR_Mid_J_SoapyWater.mat, j_install_unity.py).
            // Only the game's own water picture changes its material; it stays where the game put it (MgKnightPads.Surface).
            var soapy = Resources.Load<Material>(Folder + LaundryFolder + "PR_Mid_J_SoapyWater");
            if (soapy != null)
                foreach (var r in game.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Water")
                    {
                        r.sharedMaterial = soapy;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
            foreach (var t in station.GetComponentsInChildren<Transform>(true))
                if (t.name == "White washhouse wall tile") Hide(t);
        }

        // The six cloth dolls (user 11-08 "Blender로 직접", J/j_dolls_v1.py) on the game's pieces: each doll rides its piece
        // (the game's own transform), so it is knocked off, tumbles and sinks as before; the piece's turned body and the
        // R312 seams stop drawing, its team "Ring" at the foot stays, its collider is untouched. Tinted per station like
        // the pieces: the linen body in the other side's piece colour (charcoal or cream), the collars, bands, seams,
        // crowns and mane in the other side's team colour (orange or blue).
        static void LaundryDolls(MissionStation station, Transform game, Dictionary<string, Material> cache)
        {
            int them = station.Team == Teams.White ? Teams.Black : Teams.White;
            var linen = Resources.Load<Material>(OwnMaterials + "PR_Mid_J_Linen");
            var trimSource = Resources.Load<Material>(OwnMaterials + "PR_Mid_J_DollTrim");
            if (linen == null || trimSource == null) return;
            Material body = null, trim = null;
            for (int i = 0; i < KnightRoute.PieceSquares.Length; i++)
            {
                var piece = Find(game, "Piece " + KnightRoute.PieceSquares[i]);
                var doll = Load(LaundryFolder + "J_Doll_" + KnightRoute.PieceKinds[i]);
                if (piece == null || doll == null) continue;
                HideExcept(piece, "Ring");
                if (piece.Find(RideName) != null) continue;
                Hang(piece, RideName, doll, cache);
                if (body == null)
                {
                    body = new Material(linen) { name = "J doll body " + station.Team };
                    body.SetColor("_BaseColor", MiniGameParts.PieceColor(them));
                    trim = new Material(trimSource) { name = "J doll trim " + station.Team };
                    trim.SetColor("_BaseColor", station.Accent(them));
                }
                foreach (var r in piece.Find(RideName).GetComponentsInChildren<Renderer>(true))
                {
                    var slots = r.sharedMaterials;
                    for (int k = 0; k < slots.Length; k++)
                        if (slots[k] == linen) slots[k] = body;
                        else if (slots[k] == trimSource) slots[k] = trim;
                    r.sharedMaterials = slots;
                }
            }
        }

        static void LaundryRoad(Transform game, GameObject tub, GameObject nozzle, Dictionary<string, Material> cache)
        {
            var roads = new List<Transform>();
            var pads = new List<Transform>();
            foreach (var t in game.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Road ") && t.name.Length <= 7) roads.Add(t);           // "Road d1", not "Road rim"
                else if (t.name == "Road rim" && tub != null) Hide(t);
                else if (t.name.StartsWith("Jump pad ")) pads.Add(t);
            }
            if (tub != null)
                foreach (var road in roads)
                {
                    Hide(road);                                     // the square box and the R312 barrel under it
                    string name = TubName + road.name.Substring(5);
                    if (game.Find(name) != null) continue;
                    // the square's top middle (a unit cube scaled to the square), in the game's frame; the box's scale is
                    // not inherited, the tub is modelled at its size
                    var at = new GameObject(name).transform;
                    at.SetParent(game, false);
                    at.localPosition = game.InverseTransformPoint(road.TransformPoint(new Vector3(0f, .5f, 0f)));
                    Hang(at, RideName, tub, cache);
                }
            if (nozzle != null)
                foreach (var pad in pads)
                {
                    HideExcept(pad, "Pad ring");
                    if (pad.Find(RideName) == null) Hang(pad, RideName, nozzle, cache);
                }
        }

        // The wheel's back face on the north wall's white tile dado (front z 24.32, j_hall_v1.py), 3.6 m outside each team
        // door's middle (x +-12): past the door frame (opening 4.5 m + 0.9 m piers), its foot on the dais (0.4 m).
        static void LaundryWheels(Transform content, GameObject wheel, Dictionary<string, Material> cache)
        {
            if (wheel == null) return;
            foreach (float x in new[] { -15.6f, 15.6f })
            {
                string name = x < 0f ? "MODEL_SluiceWheel_W" : "MODEL_SluiceWheel_E";
                if (content.Find(name) != null) continue;
                var at = new GameObject(name).transform;
                at.SetParent(content, false);
                at.localPosition = new Vector3(x, .4f, 24.32f);
                Hang(at, RideName, wheel, cache);
            }
        }
    }
}
