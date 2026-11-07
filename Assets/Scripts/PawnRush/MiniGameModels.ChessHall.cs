using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    // H · 국왕 체스 홀 (R510). H is a phase-3 room like G, so it reuses G's hall as it is (user 11-08: reuse other chats'
    // assets, Docs/PawnRush/ConceptArt/R510_KingsChessHall/REUSE.md): the 12 m marble walls, skylight vaults and light
    // shafts, ceiling, hanging cloth, raised aisle with its stairs, team portal frames, team gate doors, the centre gate
    // and the phase-3 floors (RoyalProcessionHall / RoyalProcessionFloor / TeamGateDoor in MiniGameModels.cs). Only the
    // things G does not have are H's own (ArtSource/PawnRush/MiniGames/Late/h_props_v1.py, h_board_kit_v1.py):
    //   board squares   -> maple / walnut swatches (the attacked-square plates stay the game's)
    //   pillars c6 f3 f7 -> H_BoardPillar (1.6 x 1.6 x 2.2 m, the collider's box)      fence posts -> H_FencePost
    //   king tray       -> H_KingTray (its top at 0.3 m, where the captured kings lie)
    //   six kings       -> G_King (1.7 m) at the game's 0.9 m: ebony for the white (blue) team's opponents, ivory for
    //                      the black (orange) team's; frozen -> grey stone with glowing gold cracks (MiniGameKingLook).
    //                      The check dots, the frozen halo and the team ring stay the game's.
    //   entry corners   -> chess clock, spare-piece chest, notation tablet (G's trumpets, crown pedestals and aisle
    //                      stanchions are taken out: no purple within 1 m of the boards, MINIGAME_MODELING §H)
    //   above the gate  -> the bronze hall bell on its wall bracket
    // Pictures only: every collider, the board rule, the kings' moves and the team gates stay the game's.
    public static partial class MiniGameModels
    {
        const float KingModelHeight = 1.7f;   // G_King as exported by g_props_v1.py

        static void ChessHall(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            if (content == null) return;
            var cache = new Dictionary<string, Material>();
            ChessHallGate(content, cache);
            bool fresh = content.Find(EnvName) == null;
            RoyalProcessionHall(content, cache);
            var env = content.Find(EnvName);
            if (fresh && env != null)
            {
                var drop = new List<GameObject>();
                foreach (Transform t in env)
                    if (t.name == "Fanfare trumpets" || t.name == "Crown pedestal" || t.name == "Aisle stanchions"
                        || t.name == "Skylight vault" || t.name == "Ceiling") drop.Add(t.gameObject);   // H's own ceiling below
                foreach (var go in drop)
                {
                    if (Application.isPlaying) Object.Destroy(go);
                    else Object.DestroyImmediate(go);
                }
                ChessHallProps(env, cache);
                ChessHallCeiling(env, cache);
                ChessHallCrests(env, cache);
            }
            ChessHallStations(owner, cache);
            RoyalProcessionFloor(owner, content);
        }

        // The centre gate as G's (RoyalProcession): the user's royal stone gateway, front at the boards' end (z 24), and
        // the shell's door pictures in front of it stop drawing.
        static void ChessHallGate(Transform content, Dictionary<string, Material> cache)
        {
            var gate = Load("Late/G/G_CenterGate");
            if (gate == null || content.Find(GateName) != null) return;
            var at = new GameObject(GateName).transform;
            at.SetParent(content, false);
            at.localPosition = new Vector3(0f, 0f, 26f);
            Hang(at, RideName, gate, cache);
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
                if (x0 >= -9.7f && x1 <= 9.7f && z > 22.5f && z < 27f && y0 < 10.6f) renderer.enabled = false;
            }
        }

        // Entry corners (section frame): in front of the dais (z < 4), behind the entry wall's columns (z > 1.65) and
        // outside the team ramps (|x| > 14.2): tablet, chest, clock from the ramp outward, faces into the hall (+z).
        // The bell hangs on the centre wall above the gate (gate top 10.4 m, ceiling 12 m), plate on the wall face.
        static void ChessHallProps(Transform env, Dictionary<string, Material> cache)
        {
            // `writing`: the model's text plate (h_decals_v1.py, same origin): notation on the tablet, numerals on the dials
            void Put(string model, string name, Vector3 at, string writing = null)
            {
                var m = Load("Late/H/" + model);
                if (m == null) return;
                var holder = new GameObject(name).transform;
                holder.SetParent(env, false);
                holder.localPosition = at;
                // the model's face is the holder's -z: turned round so it faces into the hall (+z), not the entry wall
                // behind it (user 11-08: "소품 배치가 지금 벽쪽 보고 있어")
                holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Hang(holder, RideName, m, cache);
                var plate = writing != null ? Load("Late/H/" + writing) : null;
                if (plate != null) Hang(holder, "MODEL_Writing", plate, cache);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Put("H_NotationTablet", "Notation tablet", new Vector3(s * 15.9f, 0f, 2.75f), "H_NotationText");
                Put("H_PieceChest", "Spare-piece chest", new Vector3(s * 18f, 0f, 2.75f));
                Put("H_ChessClock", "Chess clock", new Vector3(s * 20.2f, 0f, 2.75f), "H_ClockDials");
            }
            // the bell: bracket on the wall, the bell itself on its own hinge so it can swing (h_props_v1.py split_bell:
            // hinge at Blender (0.036, -1.977, 1.33) = holder (0.036, 1.33, -1.977)); a check rings it, a mate harder
            var bracket = Load("Late/H/H_HallBellBracket");
            var bell = Load("Late/H/H_HallBellBody");
            if (bracket == null || bell == null) return;
            var hall = new GameObject("Hall bell").transform;
            hall.SetParent(env, false);
            hall.localPosition = new Vector3(0f, 10.48f, BackN - .6f);
            hall.localScale = Vector3.one * .9f;
            Hang(hall, RideName, bracket, cache);
            var hinge = new GameObject("Bell hinge").transform;
            hinge.SetParent(hall, false);
            hinge.localPosition = new Vector3(.036f, 1.33f, -1.977f);
            Hang(hinge, RideName, bell, cache);
            hinge.gameObject.AddComponent<MiniGameHallBell>();
        }

        // H ceiling (user 11-08 with the concept H_01: "천장을 이런 천장 구조물로"): instead of G's open barrel vaults, a flat
        // 0.5 m cream marble ceiling at the walls' 12 m with framed skylight wells (h_board_kit_v1.py H_SkylightWell:
        // 6 x 4.4 m opening, stepped coffer frame and brass band under the ceiling, 1.3 m well, brass pane grid, no
        // glass) over each team half: x +-8, z 3 / 9 / 15 / 21, right over G's light shafts and skylight suns, which stay.
        // The slab's holes are 0.15 m bigger than the openings so their edges sit inside the wells' 0.3 m walls.
        static readonly float[] WellZ = { 3f, 9f, 15f, 21f };
        const float WellX = 8f, WellHalfW = 3f, WellHalfD = 2.2f, WellSlack = .15f;

        static void ChessHallCeiling(Transform env, Dictionary<string, Material> cache)
        {
            var well = Load("Late/H/H_SkylightWell");
            foreach (float s in new[] { -1f, 1f })
                foreach (float z in WellZ)
                {
                    if (well == null) break;
                    var at = new GameObject("Skylight well").transform;
                    at.SetParent(env, false);
                    at.localPosition = new Vector3(s * WellX, Hall, z);
                    Hang(at, RideName, well, cache);
                }
            var marble = Resources.Load<Material>(OwnMaterials + "PR_Late_MarbleCream");
            if (marble == null) return;
            const float slab = .5f;
            void Ceiling(float x0, float x1, float z0, float z1) =>
                MarbleBlock(env, "H ceiling", new Vector3((x0 + x1) * .5f, Hall + slab * .5f, (z0 + z1) * .5f), new Vector3(x1 - x0, slab, z1 - z0), marble);
            float a = WellX - WellHalfW - WellSlack, b = WellX + WellHalfW + WellSlack;   // 4.85 .. 11.15
            Ceiling(-BackX, -b, BackS, BackN);
            Ceiling(-a, a, BackS, BackN);
            Ceiling(b, BackX, BackS, BackN);
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = Mathf.Min(s * a, s * b), x1 = Mathf.Max(s * a, s * b);
                float from = BackS;
                foreach (float z in WellZ)
                {
                    Ceiling(x0, x1, from, z - WellHalfD - WellSlack);
                    from = z + WellHalfD + WellSlack;
                }
                Ceiling(x0, x1, from, BackN);
            }
        }

        // The team portal crest (G's ProcessionWalls hangs G's rook medallion above each team arch): H's king medallion
        // (h_crest_v1.py, the same 2.2 m seat and origin) in its place.
        static void ChessHallCrests(Transform env, Dictionary<string, Material> cache)
        {
            var crest = Load("Late/H/H_TeamGateCrest");
            if (crest == null) return;
            foreach (Transform frame in env)
            {
                if (frame.name != "Team portal frame") continue;
                var seat = frame.Find("Team portal crest");
                if (seat == null) continue;
                var old = seat.Find(RideName);
                if (old != null)
                {
                    old.name = "G rook crest (replaced)";
                    if (Application.isPlaying) Object.Destroy(old.gameObject);
                    else Object.DestroyImmediate(old.gameObject);
                }
                Hang(seat, RideName, crest, cache);
            }
        }

        static void ChessHallStations(MiniGameSection owner, Dictionary<string, Material> cache)
        {
            Material Swatch(string name) => Resources.Load<Material>(OwnMaterials + name);
            var maple = Swatch("PR_Late_Maple");
            var walnut = Swatch("PR_Late_Walnut");
            var ebony = Swatch("PR_Late_Ebony");
            var frozen = Swatch("PR_Late_FrozenStone");
            var king = Load("Late/G/G_King");
            var pillar = Load("Late/H/H_BoardPillar");
            var post = Load("Late/H/H_FencePost");
            var tray = Load("Late/H/H_KingTray");
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                TeamGateDoor(station, cache);
                ChessHallRamp(station, cache);
                if (station.Game == null) continue;
                var game = station.Game.transform;
                bool white = station.Team == Teams.White;

                var board = game.Find("Board");
                if (board != null)
                    foreach (Transform t in board)
                    {
                        if (!t.name.StartsWith("Square ")) continue;
                        PawnRushBoard.Parse(t.name.Substring(7), out int f, out int r);
                        TileOver(t, PawnRushBoard.IsLight(f, r) ? maple : walnut);
                    }

                // the game's boxes: the model stands on the box's bottom, the box stops drawing (its collider stays)
                var boxes = new List<Transform>();
                foreach (Transform t in game) boxes.Add(t);
                int posts = 0;
                foreach (var t in boxes)
                {
                    GameObject model = null;
                    if (t.name.StartsWith("Pillar ")) model = pillar;
                    else if (t.name == "Fence post") model = post;
                    else if (t.name == "King tray") model = tray;
                    if (model == null) continue;
                    string holderName = "MODEL_" + t.name + (t.name == "Fence post" ? " " + posts++ : "");
                    Hide(t);
                    if (game.Find(holderName) != null) continue;
                    var at = new GameObject(holderName).transform;
                    at.SetParent(game, false);
                    at.localPosition = t.localPosition - t.localRotation * new Vector3(0f, t.localScale.y * .5f, 0f);
                    at.localRotation = t.localRotation;
                    Hang(at, RideName, model, cache);
                }

                // kings: the model rides each king's Look (moves, hops, tips over and flies to the tray with it)
                if (king == null) continue;
                for (int k = 1; k <= CheckmateHunt.Kings; k++)
                {
                    var look = Find(game, "King " + k)?.Find("Look");
                    if (look == null || look.Find(RideName) != null) continue;
                    var holder = new GameObject(RideName).transform;
                    holder.SetParent(look, false);
                    holder.localScale = Vector3.one * (MgCheckmate.KingHeight / KingModelHeight);
                    Hang(holder, "MODEL_Pivot", king, cache);
                    if (white && ebony != null)
                        foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
                        {
                            var slots = r.sharedMaterials;
                            for (int i = 0; i < slots.Length; i++) slots[i] = ebony;
                            r.sharedMaterials = slots;
                        }
                    look.gameObject.AddComponent<MiniGameKingLook>().Watch(holder, frozen);
                }
            }
        }

        // The team ramp dressed as stairs, as G's (RoyalProcessionStations): its slope collider stays the walking surface.
        static void ChessHallRamp(MissionStation station, Dictionary<string, Material> cache)
        {
            var ramp = Find(station.transform, "Palace station ramp");
            var stairs = Load("Late/G/G_StairsRamp");
            if (ramp == null || stairs == null || ramp.parent.Find("MODEL_RampStairs") != null) return;
            if (ramp.TryGetComponent<Renderer>(out var flat)) flat.enabled = false;
            var at = new GameObject("MODEL_RampStairs").transform;
            at.SetParent(ramp.parent, false);
            at.localPosition = new Vector3(0f, 0f, -Arena.RampLength);
            at.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Hang(at, RideName, stairs, cache);
        }
    }

    // H (R510): the hall bell swings on its hinge (about the hinge's x, along the wall: toward the room and back) when a
    // king takes a check, harder when one freezes (MINIGAME_ZONES §H: "체크마다 홀 종이 낮게 한 번 울리고, 체크메이트는
    // 큰 화음"). Each ring adds to the swing, which dies away. Picture only; the game's own check and mate sounds play.
    public sealed class MiniGameHallBell : MonoBehaviour
    {
        const float CheckKick = 9f, MateKick = 20f, MaxSwing = 28f, Period = 1.6f, Fade = 1.3f;
        float amplitude, phase;

        void OnEnable() => MgCheckmate.KingChecked += Ring;
        void OnDisable() => MgCheckmate.KingChecked -= Ring;

        void Ring(bool mate)
        {
            amplitude = Mathf.Min(MaxSwing, amplitude + (mate ? MateKick : CheckKick));
            if (amplitude <= (mate ? MateKick : CheckKick) + .01f) phase = 0f;   // from rest: start the swing out
        }

        void Update()
        {
            if (amplitude < .05f)
            {
                if (amplitude > 0f) { amplitude = 0f; transform.localRotation = Quaternion.identity; }
                return;
            }
            phase += Time.deltaTime * 2f * Mathf.PI / Period;
            amplitude *= Mathf.Exp(-Time.deltaTime / Fade);
            transform.localRotation = Quaternion.Euler(amplitude * Mathf.Sin(phase), 0f, 0f);
        }
    }

    // H (R510): a king model on the game king's Look. MgCheckmate.Paint gives the king's body the gold material while it
    // is frozen and its own skin otherwise; this hides the game's body, keeps watching its material and puts the model in
    // the frozen look (grey stone, glowing gold cracks: PR_Late_FrozenStone) while it is frozen. The check dots, the
    // frozen halo and the team ring are not the body and keep drawing. Nothing here touches the game or the network.
    public sealed class MiniGameKingLook : MonoBehaviour
    {
        Renderer source;
        Material free, frozenLook;
        Renderer[] parts;
        Material[][] plain;
        bool shown;

        public void Watch(Transform model, Material frozen)
        {
            frozenLook = frozen;
            // the game's body: the renderers wearing the same material as "Body" (or the first plain part)
            Material skin = null;
            var body = transform.Find("Body");
            if (body != null && body.TryGetComponent<Renderer>(out var b)) skin = b.sharedMaterial;
            var all = GetComponentsInChildren<Renderer>(true);
            if (skin == null)
                foreach (var r in all)
                    if (!r.transform.IsChildOf(model) && !(r is LineRenderer) && r.name != "Ring") { skin = r.sharedMaterial; break; }
            foreach (var r in all)
            {
                if (r.transform.IsChildOf(model) || r is LineRenderer || r.sharedMaterial != skin) continue;
                if (source == null) source = r;
                r.enabled = false;
            }
            free = skin;
            parts = model.GetComponentsInChildren<Renderer>(true);
            plain = new Material[parts.Length][];
            for (int i = 0; i < parts.Length; i++) plain[i] = parts[i].sharedMaterials;
            Show(IsFrozen());
        }

        bool IsFrozen() => source != null && free != null && source.sharedMaterial != free;

        void LateUpdate()
        {
            bool f = IsFrozen();
            if (f != shown) Show(f);
        }

        void Show(bool frozen)
        {
            shown = frozen;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                if (!frozen || frozenLook == null) { parts[i].sharedMaterials = plain[i]; continue; }
                var slots = new Material[plain[i].Length];
                for (int k = 0; k < slots.Length; k++) slots[k] = frozenLook;
                parts[i].sharedMaterials = slots;
            }
        }
    }
}
