using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush
{
    // I · 국왕 침실 (R511). A phase-3 room on the same board squares as H. Its walls, windows, columns, cornice and ceiling
    // are I's own (user 11-08: each room's architecture follows its own concept; BedchamberHall, i_hall_v1.py); only the
    // parts that are really the same come from G / H: the raised aisle and entry stairs, the team portal frames and doors,
    // the centre gate, the light shafts (code) and H's king crest over the portals.
    // I's own models were all made in Blender (ArtSource/PawnRush/MiniGames/Late/I, i_hall_v1.py / i_props_v1.py /
    // i_devices_v1.py, installed by i_install_unity.py):
    //   room shell      -> I_HallEnv: lattice arched windows (plum curtains in code), fluted columns, blind arches with
    //                      the I banner (king over a crescent, code cloth), arched skylight lanterns
    //   board squares   -> plum-room carpet swatches PR_Late_I_CarpetLight / Dark (the game's low-contrast squares)
    //   rail squares    -> walnut (the groove and the gold diamond stay the game's)
    //   king base       -> I_CanopyBed (round base 1.8 m = the 0.9 m collider, gold grip band, headboard north where no
    //                      beam comes from) with our king asleep on the pillow in a team-colour nightcap
    //   shield carts    -> I_ShieldCart (screen turned to face its attacker, our piece on the front of the deck)
    //   attackers       -> the game's turned pieces in ebony (the blue team's opponents) or ivory marble
    //   entry corners   -> wardrobe, washstand, nightstand (code candle flame), velvet bench with the folded robe, slippers;
    //                      dressing screens against the side walls beside the boards
    // The game rebuilds its whole position on every seed and reset (MgCheckBlock.Use), so the position's dressing is done
    // by MiniGameBedchamberDress, which redresses whenever a new position appears. Pictures only: every collider, the
    // check rules, the beams and the network stay the game's.
    public static partial class MiniGameModels
    {
        static void KingsBedchamber(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            if (content == null) return;
            var cache = new Dictionary<string, Material>();
            ChessHallGate(content, cache);               // G's centre gate (the same part in the I concept)
            BedchamberHall(content, cache);
            BedchamberStations(owner, cache);
            RoyalProcessionFloor(owner, content);
        }

        // I's own room (user 11-08: "행차 회랑 체스 홀 침실 컨셉 아트에 있는 벽 구조물이 다른데 왜 구조물이 다 같아?"):
        // I_HallEnv (i_hall_v1.py: lattice-window side walls, fluted columns with brass rings, blind arches on the north
        // corners and the entry wall, cornice, flat ceiling with arched skylight lanterns) in place of G's walls, vaults
        // and ceiling. Kept from G because they are the same parts: the raised aisle and entry stairs, the team portal
        // frames and doors, the centre gate and the light shafts (code). Curtains and banners are code cloth.
        static void BedchamberHall(Transform content, Dictionary<string, Material> cache)
        {
            if (content.Find(EnvName) != null) return;
            var env = new GameObject(EnvName).transform;
            env.SetParent(content, false);
            RoyalProcessionDais(content, env, cache);
            var shell = content.Find(MiniGamePalaceArt.ShellName);
            if (shell != null)
                foreach (Transform t in shell)
                {
                    if (t.name == "ENV_NorthFacade") HideExcept(t, PortalTunnel);
                    else if (System.Array.IndexOf(ShellKeep, t.name) < 0 && t.localPosition.z < 25.5f) Hide(t);
                }
            var hall = Load("Late/I/I_HallEnv");
            if (hall != null)
            {
                var at = new GameObject("I hall").transform;
                at.SetParent(env, false);
                Hang(at, RideName, hall, cache);
            }
            // the team portals (G's frame and door, H's king crest above the arch)
            var frame = Load("Late/G/G_TeamGateFrame");
            var crest = Load("Late/H/H_TeamGateCrest");
            foreach (float s in new[] { -1f, 1f })
            {
                if (frame == null) break;
                var at = new GameObject("Team portal frame").transform;
                at.SetParent(env, false);
                at.localPosition = new Vector3(s * 12f, 0f, BackN);
                at.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                Hang(at, RideName, frame, cache);
                if (crest == null) continue;
                var c = new GameObject("Team portal crest").transform;
                c.SetParent(at, false);
                c.localPosition = new Vector3(0f, 10.4f, -1.33f);
                Hang(c, RideName, crest, cache);
            }
            // the strip between the boards' end (z 24) and the north wall at dais height (the team tunnels keep theirs)
            var marble = Resources.Load<Material>(OwnMaterials + "PR_Late_MarbleCream");
            if (marble != null)
                foreach (var (x0, x1) in new[] { (-BackX, -14.25f), (-9.75f, 9.75f), (14.25f, BackX) })
                    MarbleBlock(env, "North floor strip", new Vector3((x0 + x1) * .5f, Arena.DaisHeight * .5f, (24f + BackN) * .5f),
                                new Vector3(x1 - x0, Arena.DaisHeight, BackN - 24f), marble);
            RoyalProcessionLight(env);       // light shafts at x +-8, z 3/9/15/21: under I's lanterns (x +-5..11, z 1.2..11.2 / 13.2..23.2)
            BedchamberCloth(env);
            BedchamberProps(env, cache);
        }

        // I cloth (code, 11-07 rule): a plum velvet curtain either side of every side-wall window, tied back by its
        // swaying; the I banner (king over a crescent, i_hall_v1.py banner_texture) in every blind arch: the north corner
        // bays and the entry wall bays. Each hangs from a matte brass rod held off the wall, as G's.
        static void BedchamberCloth(Transform env)
        {
            if (env.Find("Hanging cloth") != null) return;
            var velvet = Resources.Load<Material>(OwnMaterials + "PR_Late_I_Velvet");
            var banner = Resources.Load<Material>(OwnMaterials + "PR_Late_I_Banner");
            var brass = Resources.Load<Material>(OwnMaterials + "PR_Late_Brass");
            if (velvet == null) return;
            var root = new GameObject("Hanging cloth").transform;
            root.SetParent(env, false);
            Transform Face(string name, Vector3 at, Vector3 inward)
            {
                var h = new GameObject(name).transform;
                h.SetParent(root, false);
                h.localPosition = at;
                h.localRotation = Quaternion.LookRotation(-inward, Vector3.up);
                return h;
            }
            void Brass(PrimitiveType kind, string name, Transform face, Vector3 at, Quaternion turn, Vector3 scale)
            {
                if (brass == null) return;
                var go = GameObject.CreatePrimitive(kind);
                go.name = name;
                if (Application.isPlaying) Object.Destroy(go.GetComponent<Collider>());
                else Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(face, false);
                go.transform.localPosition = at;
                go.transform.localRotation = turn;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = brass;
            }
            void Cloth(Transform face, float along, float top, float w, float h, Material m, float reach)
            {
                MiniGameBannerCloth.Hang(face, new Vector3(along, top, 0f), w, h, m);
                float y = top + .05f, half = w * .5f + .15f;
                Brass(PrimitiveType.Cylinder, "Cloth rod", face, new Vector3(along, y, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(.08f, half, .08f));
                foreach (float end in new[] { -1f, 1f })
                {
                    float x = along + end * (half - .06f);
                    Brass(PrimitiveType.Cylinder, "Rod bracket", face, new Vector3(x, y, reach * .5f), Quaternion.Euler(90f, 0f, 0f), new Vector3(.06f, reach * .5f, .06f));
                    Brass(PrimitiveType.Sphere, "Rod knob", face, new Vector3(along + end * (half + .05f), y, 0f), Quaternion.identity, Vector3.one * .14f);
                }
            }
            float[] cols = { .45f, 6.64f, 12.83f, 19.01f, BackN - 1.2f };   // i_hall_v1.py SIDECOLS
            const float wallFace = BackX - .6f;                              // the walls' room face (22.0 / 25.8 / -0.15)
            foreach (float s in new[] { -1f, 1f })
            {
                var inward = new Vector3(-s, 0f, 0f);
                for (int i = 0; i < 4; i++)
                {
                    // in front of the window's reveal (0.22 m proud), either side of the 3.5 m opening
                    var face = Face("Window curtains", new Vector3(s * (wallFace - .4f), 0f, (cols[i] + cols[i + 1]) * .5f), inward);
                    foreach (float side in new[] { -1f, 1f }) Cloth(face, side * 2.3f, 10.45f, .85f, 8.4f, velvet, .4f);
                }
                if (banner == null) continue;
                // blind arches: north corner bay, entry wall bays (field 0.02 m behind the wall face)
                Cloth(Face("Royal banner", new Vector3(s * (15.465f + wallFace) * .5f, 0f, BackN - .6f - .25f), Vector3.back), 0f, 8.9f, 1.9f, 4.8f, banner, .25f);
                Cloth(Face("Royal banner", new Vector3(s * 10.775f, 0f, BackS + .6f + .25f), Vector3.forward), 0f, 8.9f, 1.9f, 4.8f, banner, .25f);
                Cloth(Face("Royal banner", new Vector3(s * (14.6f + wallFace) * .5f, 0f, BackS + .6f + .25f), Vector3.forward), 0f, 8.9f, 1.9f, 4.8f, banner, .25f);
            }
        }

        // Entry corners (section frame): between the entry wall's columns (z > 1.65) and the dais (z < 4), outside the
        // team ramps (|x| > 14.2), faces into the hall (+z). Screens stand on the dais strip between each board and its side
        // wall (x +-21..22), folded, faces toward the board.
        static void BedchamberProps(Transform env, Dictionary<string, Material> cache)
        {
            Transform Put(string model, string name, Vector3 at, float yaw)
            {
                var m = Load(model);
                if (m == null) return null;
                var holder = new GameObject(name).transform;
                holder.SetParent(env, false);
                holder.localPosition = at;
                holder.localRotation = Quaternion.Euler(0f, 180f + yaw, 0f);   // model face = holder -z, turned to face +z
                Hang(holder, RideName, m, cache);
                return holder;
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Put("Late/I/I_Prop_Wardrobe", "Wardrobe", new Vector3(s * 20.5f, 0f, 2.05f), 0f);
                Put("Late/I/I_Prop_Washstand", "Washstand", new Vector3(s * 18.8f, 0f, 1.95f), 0f);
                var night = Put("Late/I/I_Prop_Nightstand", "Nightstand", new Vector3(s * 17.45f, 0f, 1.95f), 0f);
                // the candle's flame and light are code (11-07 rule): the wick tip, i_props_v1.py nightstand()
                // Blender (-0.13, 0.06, 1.04) = holder (-0.13, 1.04, 0.06)
                if (night != null) MiniGameFlame.Light(night, new Vector3(-.13f, 1.075f, .06f), new Color(1f, .72f, .4f), .9f, 3.5f);
                Put("Late/I/I_Prop_Slippers", "Slippers", new Vector3(s * 17.3f, 0f, 2.65f), -s * 12f);
                var bench = Put("Mid/F/F_Bench", "Velvet bench", new Vector3(s * 15.7f, 0f, 2.2f), 0f);
                if (bench != null)
                {
                    // the folded robe on the bench's seat (its top from the bench model's bounds)
                    float seat = .45f;
                    var rs = bench.GetComponentsInChildren<Renderer>();
                    if (rs.Length > 0)
                    {
                        var b = rs[0].bounds;
                        foreach (var r in rs) b.Encapsulate(r.bounds);
                        seat = b.max.y - bench.position.y;
                    }
                    Put("Late/I/I_Robe", "Folded robe", new Vector3(s * 15.7f, seat - .01f, 2.2f), s * 8f);
                }
                // mid bay (the side columns stand at z 6.64 / 12.83), under the window's sill
                Put("Late/I/I_Prop_Screen", "Dressing screen", new Vector3(s * 21.5f, Arena.DaisHeight, 9.7f), -s * 90f);   // face to the board (-x on the east)
            }
        }

        static void BedchamberStations(MiniGameSection owner, Dictionary<string, Material> cache)
        {
            Material Swatch(string name) => Resources.Load<Material>(OwnMaterials + name);
            var light = Swatch("PR_Late_I_CarpetLight");
            var dark = Swatch("PR_Late_I_CarpetDark");
            foreach (var station in owner.Stations)
            {
                if (station == null) continue;
                TeamGateDoor(station, cache);
                ChessHallRamp(station, cache);
                if (station.Game == null) continue;
                var game = station.Game.transform;
                var board = game.Find("Board");
                if (board != null)
                    foreach (Transform t in board)
                    {
                        if (!t.name.StartsWith("Square ")) continue;
                        PawnRushBoard.Parse(t.name.Substring(7), out int f, out int r);
                        TileOver(t, PawnRushBoard.IsLight(f, r) ? light : dark);
                    }
                if (!game.TryGetComponent<MiniGameBedchamberDress>(out var dress)) dress = game.gameObject.AddComponent<MiniGameBedchamberDress>();
                dress.Setup(station);
            }
        }

        // ---------------------------------------------------------------- the position (redressed on every new seed)

        internal static void DressBedchamberPosition(MissionStation station, Transform position)
        {
            var cache = new Dictionary<string, Material>();
            Material Swatch(string name) => Resources.Load<Material>(OwnMaterials + name);
            var ivory = Swatch("PR_Late_MarbleCream");
            var ebony = Swatch("PR_Late_Ebony");
            var walnut = Swatch("PR_Late_Walnut");
            bool white = station.Team == Teams.White;
            var ours = white ? ivory : ebony;
            var theirs = white ? ebony : ivory;

            // the bed on the king's base; the palace dressing's code bed and sleeper stop drawing
            var kingBase = Find(position, "King base");
            var bed = Load("Late/I/I_CanopyBed");
            if (kingBase != null && bed != null && kingBase.Find(RideName) == null)
            {
                Hide(kingBase.Find("DEC_CanopyBed"));
                Hide(kingBase.Find("Our king"));
                Hang(kingBase, RideName, bed, cache);
                // our king asleep, foot south, head on the pillow (i_devices_v1.py SOCKET_KingPillow = base (0, 1.0, 0.75))
                const float tall = 1.1f;
                var sleeper = MiniGameParts.Piece(kingBase, "MODEL_SleepingKing", PieceKind.King, tall, ours != null ? ours : MiniGameParts.PieceMaterial(station.Team), new Color(0f, 0f, 0f, 0f), false).transform;
                sleeper.localPosition = new Vector3(0f, .97f, .78f - tall);
                sleeper.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Nightcap(sleeper, tall, station.Accent(station.Team));
            }

            foreach (Transform t in position.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                // the shield carts: the walnut cart model on the moving Look, its screen toward the cart's attacker
                if (n.StartsWith("Shield cart ") && int.TryParse(n.Substring(12), out int k))
                {
                    var look = t.Find("Look");
                    var cart = Load("Late/I/I_ShieldCart");
                    if (look == null || cart == null || look.Find(RideName) != null) continue;
                    Transform shield = look.Find("Shield");
                    foreach (Transform part in look)
                        if (part != shield) Hide(part);
                    float yaw = 0f;
                    var attacker = position.Find("Attacker " + k);
                    if (attacker != null)
                    {
                        var d = t.InverseTransformPoint(attacker.position);
                        d.y = 0f;
                        if (d.sqrMagnitude > 1e-4f) yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    }
                    var turn = new GameObject("MODEL_CartTurn").transform;
                    turn.SetParent(look, false);
                    turn.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    Hang(turn, RideName, cart, cache);
                    look.Find("MODEL_CartTurn").name = RideName;   // found on a re-apply
                    if (shield != null)
                    {
                        // our piece on the front half of the deck (i_devices_v1.py SOCKET_CartPiece = (0, 0.8, -0.15))
                        shield.localPosition = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, .8f, -.15f);
                        Paint(shield, ours);
                    }
                }
                else if (n.StartsWith("Attacker ") || n.StartsWith("Double check ")) Paint(t, theirs);
                else if (n == "Rail square") TileOver(t, walnut);
            }
        }

        // The piece's body (not its team ring) in another material.
        static void Paint(Transform piece, Material material)
        {
            if (material == null) return;
            foreach (var r in piece.GetComponentsInChildren<Renderer>(true))
                if (r.name == "Body") r.sharedMaterial = material;
        }

        // A soft team-colour nightcap drooping off the sleeping king's crown, with a white pompom (code, so it takes the
        // team colour). The king lies along +z: its crown at local y = tall.
        static void Nightcap(Transform king, float tall, Color team)
        {
            var cap = new GameObject("Nightcap").transform;
            cap.SetParent(king, false);
            cap.localPosition = new Vector3(0f, tall * .86f, 0f);
            var cloth = Arena.Lit("PR_Late_I_Nightcap_" + ColorUtility.ToHtmlStringRGB(team), team, 0f, .85f);
            var mesh = new Mesh { name = "Nightcap" };
            const int seg = 14, rings = 7;
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int j = 0; j <= rings; j++)
            {
                float t = j / (float)rings;
                float r = Mathf.Lerp(.17f, .03f, t);
                // the cone bends over toward one side as it rises
                var c = new Vector3(Mathf.Sin(t * 1.7f) * .16f, t * .3f - t * t * .12f, 0f);
                for (int i = 0; i < seg; i++)
                {
                    float a = 2f * Mathf.PI * i / seg;
                    v.Add(c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a = j * seg + i, b = j * seg + (i + 1) % seg;
                    tri.AddRange(new[] { a, a + seg, b, b, a + seg, b + seg });
                }
            mesh.SetVertices(v);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            var go = new GameObject("Cap", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(cap, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = cloth;
            var brim = MiniGameParts.Part(cap, PrimitiveType.Cylinder, new Vector3(0f, -.01f, 0f), new Vector3(.37f, .03f, .37f), Arena.Lit("PR_Late_I_NightcapBrim", new Color(.96f, .94f, .9f), 0f, .9f));
            brim.name = "Brim";
            var pom = MiniGameParts.Part(cap, PrimitiveType.Sphere, new Vector3(Mathf.Sin(1.7f) * .16f, .18f, 0f), Vector3.one * .1f, Arena.Lit("PR_Late_I_NightcapBrim", new Color(.96f, .94f, .9f), 0f, .9f));
            pom.name = "Pompom";
            foreach (var c in cap.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Object.Destroy(c);
                else Object.DestroyImmediate(c);
            }
        }
    }

    // I (R511): redresses the check-block position whenever the game builds a new one (a new seed from the host, a reset),
    // because MgCheckBlock replaces its whole "Position <seed>" with code pictures each time. Watches only; never touches
    // the game, its colliders or the network.
    public sealed class MiniGameBedchamberDress : MonoBehaviour
    {
        MissionStation station;
        Transform dressed;

        public void Setup(MissionStation owner)
        {
            station = owner;
            dressed = null;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (station == null) return;
            Transform position = null;
            foreach (Transform t in transform)
                if (t.name.StartsWith("Position ")) position = t;
            if (position == null || position == dressed) return;
            dressed = position;
            MiniGameModels.DressBedchamberPosition(station, position);
        }
    }
}
