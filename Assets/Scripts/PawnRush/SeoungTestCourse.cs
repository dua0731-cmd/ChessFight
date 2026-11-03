using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 승규 님's Pawn Rush test scene (seoungtest branch, 10-30): the sections of Course01v4Builder.ZoneSections (since R243
    // only the endgame's E and F; course 01's A, B, C and tower are out) and the ten mini-games, each built alone and stood
    // side by side instead of chained (sections in one row, mini-games in the row in front), to test the pieces' physics
    // and redesign the course.
    //
    //   F1      into the zone the camera looks at (flying: the one under the view; walking: the zone again from its start)
    //   [ ]     the previous / next zone
    //   1~6     pawn, knight, bishop, rook, queen, king
    //   F2      free camera from the play view (WASD, Space / C, Shift fast), again back to the pawn
    //   G       the mini-game of this zone from the start
    //   H       help on / off
    //
    // The menu ChessFight > SeoungTest > Build Pawn Rush Test Scene builds the zones into the scene (hand edits are kept
    // until the next build); a scene without them, or without some zone the code has, builds them when Play starts.
    [DefaultExecutionOrder(-500)]
    public sealed class SeoungTestCourse : MonoBehaviour
    {
        public const string RootName = "SeoungTest_Zones";
        // Raised whenever a zone's build changes, so a scene built before it is built again (R244: the war room v2, R255: hard LD v1 and its Blender models, R258: the library shuttle loops in the physics step, R259: the shuttle driven from the shared clock, R260: the library's lower floor and its fall, R263: the royal water garden G beside the library).
        public const int Version = 61; // 61 (R338): the hall H's falling knights painted - ivory head, gold crowns, a dark plinth with gold emblems and banded rims, a cream base with gold rims - at the asset's full 534k faces. 60 (R337): the hall H's architectural walls in PR_PalaceWall.shader, the gold read per pixel from the wall module's moulding map (white gold, black stone) through UVs onto the asset's front; no banners. 59 (R336): the hall H's gold trim along the walls' own mouldings (pilaster edges, capitals, arches, window frames and sills, base panels, the entablature), the banners gone. 58 (R335): the hall H's walls in cream limestone (PR_PalaceStone.shader) with gold trim, eight crimson crown banners in the blind bays. 57 (R333): the hall H's glass stones in the ice blue Pawn Rush glass (PR_GlassIce.mat), their shards too. 56 (R332): the round hall's oculus in the crystal clear glass (PR_Glass.shader: refraction, frost and reflection off the opaque texture). 55 (R331): the hall H's floating stones' pads and jump pad textured as their assets' previews (cream tiles, dark panels, gold frames, glowing orange seams and arrow). 54 (R330): the hall H's carpet really laid (its texture had imported as a cubemap), the hub chandelier three times v10's size swaying on its chain, its crystals opaque and glowing, its bulbs and lights brighter. 53 (R329): the hall H's red floor in a crimson carpet with gold border lines round the stair's sides and in the round hall, the hub chandelier in gold, crystal, wax and glowing bulbs with a point light in each bulb and one in its middle. 52 (R328): the hall H's four statues out of the foyer, two a side of the grand stair on the red floor, on octagonal plinths up to the stair's height (queen and king by the lower flight, bishop and knight by the upper). 51 (R327): one armillary, scaled up beside the start's war map (the goal's pair gone). 50 (R326): the hall H's two swinging chandeliers hang 0.4 .. 1.7 m further in, so the ball stops 0.1 m short of the round wall at the top of its swing, their swing marks run only across the way. 49 (R325): the hall H's round wall in the architectural wall bent round the ring, its pilasters on the course's edge (r 31), a star vault of crossing ribs over it to a glass oculus, the hub chandelier twice the size under it, the chandelier beams on into the wall. 48 (R324): the library goal's armillaries turn (rings and sphere separate in the FBX). 47 (R323): zone E is labelled 작전 회의실, its cannons keep the hall so the fan whirr stays inside it. 46 (R322): the hall H's north corner columns back (R321 undone), the south walls' cut ends closed (Unity draws no back faces, you saw through them). 45 (R321): the hall H's north corner columns gone, a stone fill from the round wall to the side walls. 44 (R320): the hall H's window bars round in section as the asset's, its south corner columns gone, the side walls reaching the south walls' outer face. 43 (R319): the hall H's windows in their old tracery drawn again in clean bars, the corner columns gone, the walls running into the foyer side walls instead. 42 (R318): the hall H's entrance bases built as masonry, the entrance corner columns thicker over the wing ends, columns where the foyer side walls meet the hall's south outer wall. 41 (R315): the library's ten inner gallery piers in 승규's stone column. 40 (R317): the hall H's outer and foyer side windows cut whole with straight new bars, corner columns where the foyer meets the archway wings, the falling pieces drawn as 승규's chess knight. 39 (R313): the foyer's side walls in the architectural wall. 38 (R312): the castle archway turned round, its arch the way in and its wings the foyer's south wall (that blockout wall gone). 37 (R312): the hall H's entrance in 승규's castle archway, its wing windows open, stone bases under the wings. 36 (R311): the hall H's red floor under its walls, 승규's flat pad models on the floating stones and the jump pad. 35 (R309): the garden obstacle stand-ins in orange. 34 (R309): the garden's v31 graybox dressed with brick modules and clay parts (v33), the boats, gates, posts, pads and spin bars riding their movers. 33 (R308): the Blender hall v15 over the blockout hall H, the falling chandeliers from its model. 32 (R307): the garden G on main's v31 layout, its models moved onto it (v32). 31 (R299): the Tripo hall withdrawn (승규 님), H is the blockout hall again. 30 (R298 v7): the Tripo hall's normals recomputed and its central landing a clean box. 29 (R298): 승규 님's Tripo hall model in H, the blockout pieces it replaces stop drawing. 28 (R297): the grand stair hall H beside the garden. R287: garden v30 (side waterfalls moved back, round islands gone, east water gate and royal boat tours); 23: node names from the Blender name, so the scene built from the first v30 export is rebuilt. 24 (R288): the library conveyor slides on rail shoes instead of wheels. 25 (R289): the conveyor's sound. 26 (R291): the blower cannons fire air, their fans spin. 27 (R296): garden v31 (decimated V19 / V16 meshes) and the war room LOD switch heights.
        static string VersionMark => "Zones v" + Version;
        // Wide gaps (10-30, 승규 님: room to widen the course sections): 80 m between zones, the mini-games 160 m in front.
        const float Gap = 80f, MiniRowZ = -160f;

        static readonly PieceKind[] PieceKeys =
            { PieceKind.Pawn, PieceKind.Knight, PieceKind.Bishop, PieceKind.Rook, PieceKind.Queen, PieceKind.King };

        [SerializeField] Course01Kit kit;
        [SerializeField] PlaytestSpawner spawner;
        [SerializeField] OrbitCamera orbit;
        [SerializeField] bool buildOnPlayIfEmpty = true;

        SeoungTestZone[] zones = new SeoungTestZone[0];
        Bounds all;
        int current = -1;
        bool help = true;
        GUIStyle style;

        public Course01Kit Kit => kit;

        void Awake()
        {
            // The piece skills on (F), as on the course (PawnRushCourse); this PC plays white.
            CourseSkills.PawnRush = true;
            MissionStation.LocalTeam = Teams.White;
            OrbitCamera.FlyKey = KeyCode.None;
            var saved = transform.Find(RootName);
            // A scene whose zones are not the code's (a pull that added or took out sections, R241 / R243): built again
            // for this Play only; the scene file keeps its old zones until the menu builds it again.
            string mismatch = saved != null && buildOnPlayIfEmpty && kit != null ? Mismatch(saved) : null;
            if (mismatch != null)
            {
                Debug.Log("[SeoungTest] 씬의 구역이 코드와 달라(" + mismatch + ") 이번 Play에서 구역을 모두 다시 지었습니다. " +
                          "씬에 저장하려면 ChessFight > SeoungTest > Build Pawn Rush Test Scene.");
                saved.gameObject.SetActive(false);
                saved.SetParent(null);
                Destroy(saved.gameObject);
                saved = null;
            }
            if (buildOnPlayIfEmpty && saved == null && kit != null) Build(transform, kit);
            zones = GetComponentsInChildren<SeoungTestZone>(true);
            for (int i = 0; i < zones.Length; i++)
                if (i == 0) all = zones[i].bounds;
                else all.Encapsulate(zones[i].bounds);
            if (zones.Length > 0 && spawner != null && zones[0].start != null)
                spawner.transform.SetPositionAndRotation(zones[0].start.position, zones[0].start.rotation);
        }

        void OnDestroy()
        {
            CourseSkills.PawnRush = false;
            OrbitCamera.FlyKey = KeyCode.F1;
        }

        void Update()
        {
            if (spawner == null || spawner.Driver == null || zones.Length == 0 || !Application.isFocused) return;
            if (current < 0) current = 0;
            if (Input.GetKeyDown(KeyCode.F1)) Enter(Looked());
            if (Input.GetKeyDown(KeyCode.LeftBracket)) Enter((current + zones.Length - 1) % zones.Length);
            if (Input.GetKeyDown(KeyCode.RightBracket)) Enter((current + 1) % zones.Length);
            if (Input.GetKeyDown(KeyCode.F2)) FreeLook();
            if (Input.GetKeyDown(KeyCode.H)) help = !help;
            if (Input.GetKeyDown(KeyCode.G) && zones[current].station != null) zones[current].station.Install(zones[current].station.GameIndex);
            for (int i = 0; i < PieceKeys.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && spawner.Driver is IPromotable piece) piece.Promote(PieceKeys[i]);
        }

        void Enter(int index)
        {
            if (index < 0 || index >= zones.Length || zones[index].start == null) return;
            current = index;
            if (orbit != null && orbit.FreeFly) orbit.StopFly();
            spawner.SetStart(zones[index].start.position, zones[index].start.rotation);
        }

        // The zone the camera looks at: the one its view hits, else the nearest to where it looks.
        int Looked()
        {
            if (orbit == null) return current;
            var view = orbit.transform;
            Vector3 point = view.position + view.forward * 60f;
            if (Physics.Raycast(view.position, view.forward, out var hit, 5000f, ~0, QueryTriggerInteraction.Ignore))
            {
                var zone = hit.collider.GetComponentInParent<SeoungTestZone>();
                if (zone != null) return System.Array.IndexOf(zones, zone);
                point = hit.point;
            }
            else if (view.forward.y < -.05f)
            {
                // Over empty ground: where the view meets the floor's height.
                point = view.position + view.forward * (view.position.y / -view.forward.y);
            }
            int best = current;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < zones.Length; i++)
            {
                float d = zones[i].bounds.SqrDistance(point);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        // R308 (승규 11-03): the free camera starts where the play camera is, looking where it looks (no jump to the
        // whole map); again back to the pawn.
        void FreeLook()
        {
            if (orbit == null) return;
            if (orbit.FreeFly) { orbit.StopFly(); return; }
            var view = orbit.transform;
            float pitch = Mathf.DeltaAngle(0f, view.eulerAngles.x);
            orbit.FlyAt(view.position, view.eulerAngles.y, pitch);
        }

        void OnGUI()
        {
            if (!help || zones.Length == 0) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            var lines = new List<string>
            {
                "<b>승규 시험 씬 · 폰 러쉬</b>  (H 도움말 끄기)",
                "F1 보고 있는 구역에 들어가기 · [ ] 이전/다음 구역 · F2 이 자리에서 카메라 움직이기",
                "1 폰 · 2 나이트 · 3 비숍 · 4 룩 · 5 퀸 · 6 킹 · F 스킬 · G 미니게임 처음부터 · R 구역 처음으로",
                ""
            };
            int looked = orbit != null && orbit.FreeFly ? Looked() : -1;
            for (int i = 0; i < zones.Length; i++)
            {
                string mark = i == current ? "<color=#FFD24A>▶ " : i == looked ? "<color=#7CFF8A>◎ " : "<color=#DDDDDD>   ";
                lines.Add(mark + zones[i].label + "</color>");
            }
            if (spawner != null && spawner.Driver is IPromotable p) lines.Add("\n지금 기물: <b>" + ChessPieces.Name(p.Piece) + "</b>");
            var text = string.Join("\n", lines);
            var size = style.CalcSize(new GUIContent(text));
            var rect = new Rect(Screen.width - size.x - 24f, 16f, size.x + 12f, size.y + 12f);
            GUI.color = new Color(0f, 0f, 0f, .55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 6f, size.x, size.y), text, style);
        }

        // ------------------------------------------------------------------ building

        // Every section and mini-game in a zone of its own under RootName: the road sections in a row from x 0 east,
        // their south edges on z 0; the mini-games in the row in front (south), their south edges on MiniRowZ.
        public static GameObject Build(Transform parent, Course01Kit kit)
        {
            var root = new GameObject(RootName);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            new GameObject(VersionMark).transform.SetParent(root.transform, false);
            var made = new List<SeoungTestZone>();
            try
            {
                float x = 0f;
                for (int i = 0; i < Course01v4Builder.ZoneSections.Length; i++)
                {
                    string id = Course01v4Builder.ZoneSections[i];
                    var group = Zone(root.transform, "Zone " + id);
                    // R282: one zone that fails to build (a missing model, an exception) no longer leaves the whole
                    // test scene switched off; it is reported and the other zones are built.
                    try { Course01v4Builder.Section(new CourseBuilder(kit, group), id); }
                    catch (System.Exception e)
                    {
                        Debug.LogError("[SeoungTest] 구역 " + id + "을(를) 짓다가 오류가 나서 이 구역은 비어 있습니다. 이 메시지를 알려 주세요.\n" + e);
                    }
                    made.Add(Place(group, Course01v4Builder.ZoneSectionNames[i], Course01v4Builder.ZoneStart(id), null, ref x, 0f));
                }
                x = 0f;
                for (int game = 0; game < PawnRushMissions.Count; game++)
                {
                    var group = Zone(root.transform, "Zone " + PawnRushMissions.Ids[game]);
                    var station = Course01v4Builder.SingleStation(new CourseBuilder(kit, group), game);
                    made.Add(Place(group, "미니게임 " + PawnRushMissions.Names[game], new Vector3(0f, 0f, -3f), station, ref x, MiniRowZ));
                }
            }
            finally
            {
                root.SetActive(true);
            }
            Physics.SyncTransforms();
            foreach (var zone in made) Ground(zone);
            return root;
        }

        // How `root`'s zones differ from the code's: built by an older version of the code ("옛 버전"), the first zone the
        // code builds that it lacks ("E 없음") or the first it has that the code no longer builds ("A 빠짐"); null when
        // they are the same.
        public static string Mismatch(Transform root)
        {
            if (root.Find(VersionMark) == null) return "옛 버전, 지금 v" + Version;
            var names = new HashSet<string>();
            foreach (var id in Course01v4Builder.ZoneSections) names.Add("Zone " + id);
            foreach (var id in PawnRushMissions.Ids) names.Add("Zone " + id);
            foreach (var name in names)
                if (root.Find(name) == null) return name.Substring(5) + " 없음";
            foreach (Transform zone in root)
                if (zone.name.StartsWith("Zone ") && !names.Contains(zone.name)) return zone.name.Replace("Zone ", "") + " 빠짐";
            return null;
        }

        static Transform Zone(Transform root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        // Moves a zone built at the design doc's coordinates to its place in the row and marks where a pawn starts.
        static SeoungTestZone Place(Transform group, string label, Vector3 start, MissionStation station, ref float x, float rowZ)
        {
            var box = MeshBounds(group);
            var offset = new Vector3(x - box.min.x, 0f, rowZ - box.min.z);
            group.position += offset;
            box.center += offset;
            x += box.size.x + Gap;
            var mark = new GameObject("Start").transform;
            mark.SetParent(group, false);
            mark.localPosition = start;
            mark.localRotation = Quaternion.identity;
            var zone = group.gameObject.AddComponent<SeoungTestZone>();
            zone.label = label;
            zone.start = mark;
            zone.station = station;
            zone.bounds = box;
            return zone;
        }

        // The start on the zone's own floor (the design doc's point, else the middle of its south edge).
        static void Ground(SeoungTestZone zone)
        {
            // From just above the point, so a balcony or a tower storey overhead is not taken for its floor.
            if (TryFloor(zone, zone.start.position + Vector3.up * 2f, out var top)) { zone.start.position = top; return; }
            var b = zone.bounds;
            if (TryFloor(zone, new Vector3(b.center.x, b.max.y + 5f, b.min.z + 3f), out top)) zone.start.position = top;
        }

        static bool TryFloor(SeoungTestZone zone, Vector3 from, out Vector3 top)
        {
            // RaycastAll is unordered: take the nearest floor, not whichever one comes first (R278: the garden start
            // could land on the base slab under the moat instead of the start pad).
            var hits = Physics.RaycastAll(from, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
                if (hit.collider.transform.IsChildOf(zone.transform) && hit.normal.y > .7f)
                {
                    top = hit.point;
                    return true;
                }
            top = default;
            return false;
        }

        // The box the zone's meshes fill (works while it is still inactive).
        static Bounds MeshBounds(Transform group)
        {
            bool any = false;
            var box = new Bounds(group.position, Vector3.zero);
            foreach (var filter in group.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var local = filter.sharedMesh.bounds;
                var m = filter.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { box = new Bounds(p, Vector3.zero); any = true; }
                    else box.Encapsulate(p);
                }
            }
            return box;
        }
    }
}
