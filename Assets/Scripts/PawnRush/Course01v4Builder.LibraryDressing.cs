using System.Collections.Generic;
using UnityEngine;
using Kind = ChessFight.PawnRush.LibraryPaint.Kind;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R386 (승규 11-04, the war room's concept art - S1 진입 회랑, S2 첫 작전교, S3 지휘 착지면 and a sheet of its
        // assets: "이제 맵 택스쳐를 입힐 건데 말 했듯이 완전 반짝이는 금색은 안입힐 거야 깃발도 안할거고"): the war room's
        // clay - every environment piece still in "CLAY | uniform neutral gray", 2,762 of its 2,896 - dressed as the
        // concept by the piece's name, in PR_LibraryPaint.shader: cream limestone walls, piers, ribs and vaults, a warmer
        // stone underfoot with darker joints, teal painted railings, crates, consoles and board frames, worn brass (never a
        // mirror) for caps, bands, rivets, inlays and the compass lines, dark iron beams, brackets, window bars and chains,
        // wood for the bookcases, desks, tables, cabinets and the great door, paper maps and scrolls, lanterns' glass lit.
        // The props of several parts (bookcases, crates, desks, lanterns, chests, balustrades, scroll bundles) are painted
        // part by part when the course plays (LibraryPaint). No banners: their cloths, rods, finials and mounts are
        // switched off. The pieces the other work dressed (the start props, the cannons, the anchors) keep their looks.
        // R402 (승규 11-04): the bookcases, the balcony chests and their scrolls are new models in their own maps and the
        // start / goal lantern posts are gone (war_room_props.py), so the "bookcase", "chest", "scrolls" and those
        // "lantern" recipes below find no clay there any more. Banners are back, as new blue rook banners on the inner
        // piers (Pier_banner_*, LibraryLooks); the model's old clay banners stay off.
        const string LibraryPaintMaterial = "PR_LibraryPaint", GoalInlayMaterial = "PR_LibraryGoalInlay", DoorMaterial = "PR_LibraryDoor",
            CeilingMedallionMaterial = "PR_LibraryCeilingMedallion";   // Assets/Art/PawnRushMap/Resources
        static Material libraryDoor;
        static readonly Dictionary<(Kind, Color), Material> libraryPaints = new Dictionary<(Kind, Color), Material>();
        static Material libraryPaintBase, libraryPaintParts;

        // The props painted part by part, by a piece of their name.
        static readonly (string key, string recipe)[] LibraryPropRecipes =
        {
            ("Bookshelf", "bookcase"), ("Arrow_crate", "crate"), ("reading_desk", "desk"), ("reference_desk", "desk"),
            ("Wall_lantern", "lantern"), ("Lantern_post", "lantern"), ("corner_lantern", "lantern"), ("_chest", "chest"),
            ("GAME_Railing_sector", "railing"), ("_scrolls", "scrolls"), ("chart_bundle", "scrolls"),
        };

        // The one-look pieces, the first piece of their name that matches (ordinal, case-sensitive) deciding; anything
        // else is the walls' limestone.
        static readonly (string key, Kind kind, Color colour)[] LibraryLookRules =
        {
            ("glass pane", Kind.Glow, LibraryPaint.Glass),
            // worn brass: small fittings, caps and bands, the inlaid lines and stars
            ("Lantern lower frame", Kind.Brass, LibraryPaint.Brass), ("Lantern upper frame", Kind.Brass, LibraryPaint.Brass),
            ("Lantern metal upright", Kind.Brass, LibraryPaint.Brass), ("rivet", Kind.Brass, LibraryPaint.Brass),
            ("bolt", Kind.Brass, LibraryPaint.Brass), ("fastener", Kind.Brass, LibraryPaint.Brass), ("stud", Kind.Brass, LibraryPaint.Brass),
            ("fitting", Kind.Brass, LibraryPaint.Brass), ("Keystone boss", Kind.Brass, LibraryPaint.Brass),
            ("outline", Kind.Brass, LibraryPaint.Brass), ("fine_line", Kind.Brass, LibraryPaint.Brass), ("corner_line", Kind.Brass, LibraryPaint.Brass),
            ("frame_line", Kind.Brass, LibraryPaint.Brass), ("route line", Kind.Brass, LibraryPaint.Brass), ("Raised route", Kind.Brass, LibraryPaint.Brass),
            ("inlay", Kind.Brass, LibraryPaint.Brass), ("Inlaid_border", Kind.Brass, LibraryPaint.Brass), ("Eight-point star", Kind.Brass, LibraryPaint.Brass),
            ("Board marker head", Kind.Brass, LibraryPaint.Brass), ("Chess marker crown", Kind.Brass, LibraryPaint.Brass),
            ("Console control collar", Kind.Brass, LibraryPaint.Brass), ("Console raised control", Kind.Brass, LibraryPaint.Brass),
            ("Console housing edge", Kind.Brass, LibraryPaint.Brass), ("square_cap", Kind.Brass, LibraryPaint.Brass),
            ("cast_post_foot", Kind.Brass, LibraryPaint.Brass), ("wide_post_band", Kind.Brass, LibraryPaint.Brass),
            ("continuous_cap", Kind.Brass, LibraryPaint.Brass), ("threshold", Kind.Brass, LibraryPaint.Brass),
            ("Table_floor_border", Kind.Brass, LibraryPaint.Brass), ("GOAL_capture_zone", Kind.Brass, LibraryPaint.Brass),
            ("Platform_corner", Kind.Brass, LibraryPaint.Brass),   // R387: the goal rim's gold corners, as the concept tile
            // R391 (승규 11-04, the ceiling concept: "천장도 이런식으로 ... 텍스쳐 채워"): brass where the steel beams meet
            // (their joint plates and the lips along them) and round the crown medallion.
            ("Bracket 40mm plate", Kind.Brass, LibraryPaint.Brass), ("Bracket side plate", Kind.Brass, LibraryPaint.Brass),
            ("Beam panel lip", Kind.Brass, LibraryPaint.Brass), ("Medallion molding", Kind.Brass, LibraryPaint.Brass),
            // dark iron: beams, brackets, supports, chains, window bars, the stairs' toes and straps
            ("Bracket", Kind.Iron, LibraryPaint.Iron), ("bracket", Kind.Iron, LibraryPaint.Iron),
            ("Diagonal_support", Kind.Iron, LibraryPaint.Iron), ("cross_girder", Kind.Iron, LibraryPaint.Iron),
            ("chain links", Kind.Iron, LibraryPaint.Iron), ("Cannon_anchor", Kind.Iron, LibraryPaint.Iron), ("cannon_mount", Kind.Iron, LibraryPaint.Iron),
            ("window mullion", Kind.Iron, LibraryPaint.Iron), ("window transom", Kind.Iron, LibraryPaint.Iron),
            ("J14_Toe", Kind.Iron, LibraryPaint.Iron), ("toe_shoe", Kind.Iron, LibraryPaint.Iron), ("strap", Kind.Iron, LibraryPaint.Iron),
            // teal paint: posts, rails and panels of the steel balustrades, the goal's star panel, consoles, board frames
            // R391: the ceiling's steel box beams in the concept's teal paint (were dark iron).
            ("steel box beam", Kind.Paint, LibraryPaint.Teal), ("beam_to_rear_wall", Kind.Paint, LibraryPaint.Teal),
            ("square_post", Kind.Paint, LibraryPaint.Teal), ("infill", Kind.Paint, LibraryPaint.DeepTeal), ("_rail", Kind.Paint, LibraryPaint.Teal),
            ("recess_panel", Kind.Paint, LibraryPaint.DeepTeal), ("Star_platform_center", Kind.Paint, LibraryPaint.DeepTeal),
("Briefing console", Kind.Paint, LibraryPaint.Teal),
            ("Strategy board cross frame", Kind.Paint, LibraryPaint.Teal), ("Strategy board vertical frame", Kind.Paint, LibraryPaint.Teal),
            ("Strategy board rear", Kind.Paint, LibraryPaint.DeepTeal), ("Tactical chess marker base", Kind.Paint, LibraryPaint.Teal),
            ("Board raised position marker", Kind.Paint, LibraryPaint.Teal),
            // paper
            ("Inset tactical map surface", Kind.Paper, LibraryPaint.Paper), ("Rolled command chart", Kind.Paper, LibraryPaint.Paper),
            // darker lines in stone and on the map
            ("floor joint", Kind.Stone, LibraryPaint.Grout), ("map column groove", Kind.Stone, LibraryPaint.Grout),
            ("map row groove", Kind.Stone, LibraryPaint.Grout),
            // wood
            ("Arched_double_door", Kind.Wood, LibraryPaint.DarkWood), ("Document cabinet", Kind.Wood, LibraryPaint.DarkWood),
            ("Table stout leg", Kind.Wood, LibraryPaint.Wood), ("Table square foot", Kind.Wood, LibraryPaint.Wood),
            ("Map table", Kind.Wood, LibraryPaint.Wood), ("map table top", Kind.Wood, LibraryPaint.DarkWood),
            ("Strategy board support", Kind.Wood, LibraryPaint.Wood), ("Map_frame_foot", Kind.Wood, LibraryPaint.Wood),
            ("Chart roll end", Kind.Wood, LibraryPaint.DarkWood),
            // R391: the vault's coursed webs paler than the walls, as the concept's lit crown
            ("Vault web", Kind.Stone, new Color(.95f, .90f, .83f)),
            // pale marble pieces
            ("Rook_goal_piece", Kind.Stone, LibraryPaint.Marble), ("Tactical chess marker body", Kind.Stone, LibraryPaint.Marble),
            ("Chess marker crenel", Kind.Stone, LibraryPaint.Marble),
            // the stone underfoot
            ("floor", Kind.Stone, LibraryPaint.FloorStone), ("Floor", Kind.Stone, LibraryPaint.FloorStone),
            ("platform", Kind.Stone, LibraryPaint.FloorStone), ("Platform", Kind.Stone, LibraryPaint.FloorStone),
            ("Side_balcony", Kind.Stone, LibraryPaint.FloorStone), ("steps", Kind.Stone, LibraryPaint.FloorStone),
            ("tread", Kind.Stone, LibraryPaint.FloorStone), ("nosing", Kind.Stone, LibraryPaint.FloorStone),
            ("Return_stair", Kind.Stone, LibraryPaint.FloorStone), ("_Step_0", Kind.Stone, LibraryPaint.FloorStone),
            ("spawn_zone", Kind.Stone, LibraryPaint.FloorStone),
        };

        static void LibraryDressing(GameObject hall)
        {
            if (libraryPaintBase == null) libraryPaintBase = Resources.Load<Material>(LibraryPaintMaterial);
            if (libraryPaintBase == null)
            {
                Debug.LogError("[작전 회의실] 맵 재질(Resources/" + LibraryPaintMaterial + ".mat)을 불러오지 못했습니다");
                return;
            }
            int looks = 0, props = 0, hidden = 0;
            var tiles = new List<Renderer>();
            var counts = new Dictionary<Kind, int>();
            foreach (var r in hall.GetComponentsInChildren<Renderer>(true))
            {
                if (!HasClay(r)) continue;
                string name = r.name;
                if (name.IndexOf("banner", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.StartsWith("Crossbar end finial"))
                {
                    // No banners (승규 님): the cloth, its rod and finials off, any collider on them too.
                    r.enabled = false;
                    foreach (var c in r.GetComponents<Collider>()) c.enabled = false;
                    hidden++;
                    continue;
                }
                if (name.StartsWith("Chess_tile"))
                {
                    // R388 ("시작점 마지막점 체스 발판은 빼주고"): the 1.05 m chess squares lying 4 cm thick on the start
                    // and goal floors (32 each) go; the stone floor under them stays.
                    r.enabled = false;
                    foreach (var c in r.GetComponents<Collider>()) c.enabled = false;
                    tiles.Add(r);
                    continue;
                }
                // R387 ("마지막 구간 발판을 2번 째 사진으로 바꿔"): the goal panel inlaid with the concept tile's gold compass
                // star (PR_LibraryInlay, goal_compass_inlay.py); its old thin brass star outline and lines would cross
                // the picture's, so they go.
                if (name.StartsWith("Goal24_Star_platform_center") || name.StartsWith("4m central medallion"))
                {
                    // R391: the vault crown's 4 m medallion takes the round compass (ceiling_medallion.py), seen from below.
                    string which = name.StartsWith("4m") ? CeilingMedallionMaterial : GoalInlayMaterial;
                    var inlay = Resources.Load<Material>(which);
                    if (inlay != null)
                    {
                        PutClay(r, inlay);
                        continue;
                    }
                    Debug.LogError("[작전 회의실] 판 그림 재질(Resources/" + which + ".mat)을 불러오지 못했습니다");
                }
                // R389 ("문을 저런 택스쳐로 해"): the great door and its stone surround in the door concept itself
                // (PR_LibraryFacade, Resources/PawnRushLibrary/Door/LibraryDoor.png), sized by the door's width and height.
                if (name.Contains("Arched_double_door") || name.Contains("stone_arch_surround"))
                {
                    var door = Resources.Load<Material>(DoorMaterial);
                    if (door != null)
                    {
                        if (libraryDoor == null)
                        {
                            libraryDoor = new Material(door) { name = "Library door" };
                            var size = r.bounds.size;   // the door faces along the hall: its width is x or z in the world
                            libraryDoor.SetVector("_Size", new Vector4(Mathf.Max(size.x, size.z), size.y, 0f, 0f));
                        }
                        PutClay(r, libraryDoor);
                        continue;
                    }
                    Debug.LogError("[작전 회의실] 문 재질(Resources/" + DoorMaterial + ".mat)을 불러오지 못했습니다");
                }
                if (name.StartsWith("Goal25_"))
                {
                    r.enabled = false;
                    foreach (var c in r.GetComponents<Collider>()) c.enabled = false;
                    continue;
                }
                string recipe = null;
                foreach (var (key, value) in LibraryPropRecipes)
                    if (name.Contains(key)) { recipe = value; break; }
                if (recipe != null && r.GetComponent<MeshFilter>() != null)
                {
                    if (libraryPaintParts == null) libraryPaintParts = new Material(libraryPaintBase) { name = "Library paint (parts)" };
                    libraryPaintParts.SetFloat("_UseVertex", 1f);
                    PutClay(r, libraryPaintParts);
                    r.gameObject.AddComponent<LibraryPaint>().Configure(recipe);
                    props++;
                    continue;
                }
                var (kind, colour) = LibraryLookFor(name, r);
                PutClay(r, LibraryPaintLook(kind, colour));
                counts[kind] = counts.TryGetValue(kind, out int k) ? k + 1 : 1;
                looks++;
            }
            // R389 ("기둥 쪽 조명이 아직 밝아 줄이고"): the FBX's ten lantern lights came in at 90 over 10 m and burned the
            // piers they hang on white; as the hall's lanterns now (6 over 9 m).
            int lamps = 0;
            foreach (var light in hall.GetComponentsInChildren<Light>(true))
            {
                if (light.type == LightType.Directional) continue;
                light.intensity = 6f;
                light.range = Mathf.Min(light.range, 9f);
                lamps++;
            }
            var summary = new System.Text.StringBuilder();
            foreach (var pair in counts) summary.Append(" ").Append(pair.Key).Append(" ").Append(pair.Value);
            Debug.Log("[작전 회의실] 회색 점토 칠함: 한 재질 " + looks + "개(" + summary.ToString().Trim() + "), 부품별 소품 " + props +
                      "개, 체스 칸 " + tiles.Count + "개 · 깃발 " + hidden + "개 끔, 등불 빛 " + lamps + "개 6으로");
        }

        static (Kind, Color) LibraryLookFor(string name, Renderer r)
        {
            // The document boxes on the shelves alternate teal and wood.
            if (name.StartsWith("Document storage box"))
            {
                var c = r.bounds.center;
                bool teal = (Mathf.FloorToInt(c.x * 1.7f) + Mathf.FloorToInt(c.y * 2.3f) + Mathf.FloorToInt(c.z * 1.3f)) % 2 == 0;
                return teal ? (Kind.Paint, LibraryPaint.Teal) : (Kind.Wood, LibraryPaint.Wood);
            }
            foreach (var (key, kind, colour) in LibraryLookRules)
                if (name.Contains(key)) return (kind, colour);
            return (Kind.Stone, LibraryPaint.WallStone);
        }

        static Material LibraryPaintLook(Kind kind, Color colour)
        {
            if (libraryPaints.TryGetValue((kind, colour), out var m) && m != null) return m;
            m = new Material(libraryPaintBase) { name = "Library paint " + kind + " " + ColorUtility.ToHtmlStringRGB(colour) };
            m.SetFloat("_UseVertex", 0f);
            m.SetFloat("_Kind", (int)kind);
            m.SetColor("_BaseColor", colour);
            return libraryPaints[(kind, colour)] = m;
        }

        static bool HasClay(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name.StartsWith("CLAY")) return true;
            return false;
        }

        // Only the clay slots: a piece another script already dressed keeps its other materials.
        static void PutClay(Renderer r, Material m)
        {
            var slots = r.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].name.StartsWith("CLAY")) slots[i] = m;
            r.sharedMaterials = slots;
        }
    }
}
