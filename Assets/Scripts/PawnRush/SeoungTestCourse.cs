using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 승규 님's Pawn Rush test scene (seoungtest branch, 10-30): course 01's road sections and its ten mini-games, each
    // built alone and stood side by side instead of chained (sections in one row, mini-games in the row in front), to
    // test the pieces' physics and redesign the course between the plazas.
    //
    //   F1      into the zone the camera looks at (flying: the one under the view; walking: the zone again from its start)
    //   [ ]     the previous / next zone
    //   1~6     pawn, knight, bishop, rook, queen, king
    //   F2      fly over the whole map (WASD, Space / C, Shift fast), again back to the pawn
    //   G       the mini-game of this zone from the start
    //   H       help on / off
    //
    // The menu ChessFight > SeoungTest > Build Pawn Rush Test Scene builds the zones into the scene (hand edits are kept
    // until the next build); a scene without them builds them when Play starts.
    [DefaultExecutionOrder(-500)]
    public sealed class SeoungTestCourse : MonoBehaviour
    {
        public const string RootName = "SeoungTest_Zones";
        const float Gap = 20f, MiniRowZ = -90f;

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
            if (buildOnPlayIfEmpty && transform.Find(RootName) == null && kit != null) Build(transform, kit);
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
            if (Input.GetKeyDown(KeyCode.F2)) Overview();
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

        // From high over the south edge, looking north over both rows; again back to the pawn.
        void Overview()
        {
            if (orbit == null) return;
            if (orbit.FreeFly) { orbit.StopFly(); return; }
            float span = Mathf.Max(all.size.x, all.size.z);
            orbit.FlyAt(new Vector3(all.center.x, all.max.y + span * .3f, all.min.z - span * .25f), 0f, 40f);
        }

        void OnGUI()
        {
            if (!help || zones.Length == 0) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            var lines = new List<string>
            {
                "<b>승규 시험 씬 · 폰 러쉬</b>  (H 도움말 끄기)",
                "F1 보고 있는 구역에 들어가기 · [ ] 이전/다음 구역 · F2 맵 전체 둘러보기",
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
            var made = new List<SeoungTestZone>();
            float x = 0f;
            for (int i = 0; i < Course01v4Builder.ZoneSections.Length; i++)
            {
                string id = Course01v4Builder.ZoneSections[i];
                var group = Zone(root.transform, "Zone " + id);
                Course01v4Builder.Section(new CourseBuilder(kit, group), id);
                made.Add(Place(group, Course01v4Builder.ZoneSectionNames[i], Course01v4Builder.ZoneStart(id), null, ref x, 0f));
            }
            x = 0f;
            for (int game = 0; game < PawnRushMissions.Count; game++)
            {
                var group = Zone(root.transform, "Zone " + PawnRushMissions.Ids[game]);
                var station = Course01v4Builder.SingleStation(new CourseBuilder(kit, group), game);
                made.Add(Place(group, "미니게임 " + PawnRushMissions.Names[game], new Vector3(0f, 0f, -3f), station, ref x, MiniRowZ));
            }
            root.SetActive(true);
            Physics.SyncTransforms();
            foreach (var zone in made) Ground(zone);
            return root;
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
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
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
