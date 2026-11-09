using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Pawn Rush map test scene (PawnRush_MapTest, 11-02): one map prefab at a time from the map folder
    // (Assets/Prefabs/PawnRushMaps), played offline with one ragdoll pawn exactly as on the course - the Pawn Rush
    // movement (PawnRushPhysicsParams), the piece skills on F and promotion on 1~6 (PawnRushCourseSkills attaches
    // itself because CourseSkills.PawnRush is set here). Nothing of course 01 is built; the maps bring their own
    // floors, obstacles, KillVolumes and Checkpoints, and the playtest spawner already answers those.
    //
    //   [ ]        previous / next map            F5   build the map again (picks up prefab edits made during Play)
    //   1~6        pawn, knight, bishop, rook, queen, king (F skill, left click to throw the aimed ones)
    //   T          put the pawn where the camera looks  F4   free camera (the orbit camera's own key; F1 flies the pawn, R529)
    //   F2         look over the whole map        F3   slow motion 1x / 0.5x / 0.25x
    //   R          back to the checkpoint         Backspace  from the map's start again
    //   H          help on / off
    //
    // Where a map starts: its SpawnPoint with the lowest index (white first), else a child named "Start", else the
    // middle of its south edge on whatever floor is there. Maps are placed at the world origin as authored.
    [DefaultExecutionOrder(-500)]
    public sealed class PawnRushMapTest : MonoBehaviour
    {
        public const string MapFolder = "Assets/Prefabs/PawnRushMaps";
        // The map to open at the next Play: an asset path (the editor's "test this map" menu) or a map name.
        public const string LastMapKey = "ChessFight.PawnRushMapTest.LastMap";
        public const string CalibrationName = "기본 시험장 (코드)";

        static readonly PieceKind[] PieceKeys =
            { PieceKind.Pawn, PieceKind.Knight, PieceKind.Bishop, PieceKind.Rook, PieceKind.Queen, PieceKind.King };
        static readonly float[] SlowSteps = { 1f, .5f, .25f };

        [Tooltip("Materials for the built-in calibration yard (the course's kit).")]
        [SerializeField] Course01Kit kit;
        [SerializeField] PlaytestSpawner spawner;
        [SerializeField] OrbitCamera orbit;
        [Tooltip("The spawner's spawn point: moved onto the first map's start before the pawn is made.")]
        [SerializeField] Transform startPoint;
        [Tooltip("Maps outside the map folder, and the list a player build uses (the editor reads the folder itself).")]
        [SerializeField] List<GameObject> maps = new List<GameObject>();
        [Tooltip("The code-built yard of steps, gaps, slopes and walls for measuring the pawn, first in the list.")]
        [SerializeField] bool includeCalibrationYard = true;

        struct Entry
        {
            public string name, id;
            public GameObject prefab;   // null: the calibration yard
        }

        readonly List<Entry> entries = new List<Entry>();
        Transform mapRoot;
        GameObject built;
        Bounds bounds;
        string startFrom = "";
        int current = -1, slow;
        bool help = true;
        GUIStyle style;
        Texture2D white;

        public string CurrentMap => current >= 0 && current < entries.Count ? entries[current].name : "";
        public Bounds MapBounds => bounds;

        void Awake()
        {
            // The piece skills on (F) as on the course (PawnRushCourse); this PC plays white.
            CourseSkills.PawnRush = true;
            MissionStation.LocalTeam = Teams.White;
            OrbitCamera.FlyKey = OrbitCamera.DefaultFlyKey;   // F4 (R529: F1 flies the pawn, DevCheats)
            mapRoot = new GameObject("Map (test)").transform;
            mapRoot.SetParent(transform, false);
            Collect();
            if (entries.Count == 0)
            {
                Debug.LogWarning("[MapTest] 시험할 맵이 없습니다. " + MapFolder + " 폴더에 맵 프리팹을 넣으세요.");
                return;
            }
            int first = Find(Load(LastMapKey));
            // The spawner makes the pawn in its Start: at the first map's start point.
            Open(first < 0 ? 0 : first, true);
        }

        void OnDestroy()
        {
            CourseSkills.PawnRush = false;
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ the list

        void Collect()
        {
            entries.Clear();
            if (includeCalibrationYard && kit != null) entries.Add(new Entry { name = CalibrationName, id = CalibrationName });
            var seen = new HashSet<GameObject>();
#if UNITY_EDITOR
            // In the editor the folder itself is the list: a prefab saved there is in the next Play.
            if (UnityEditor.AssetDatabase.IsValidFolder(MapFolder))
            {
                var paths = new List<string>();
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { MapFolder }))
                    paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                paths.Sort(System.StringComparer.Ordinal);
                foreach (var path in paths)
                {
                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null || !seen.Add(prefab)) continue;
                    entries.Add(new Entry { name = Label(path), id = path, prefab = prefab });
                }
            }
#endif
            foreach (var prefab in maps)
                if (prefab != null && seen.Add(prefab)) entries.Add(new Entry { name = prefab.name, id = prefab.name, prefab = prefab });
        }

        // "Assets/Prefabs/PawnRushMaps/Sub/Name.prefab" -> "Sub/Name".
        static string Label(string path)
        {
            string rest = path.StartsWith(MapFolder + "/") ? path.Substring(MapFolder.Length + 1) : path;
            return rest.EndsWith(".prefab") ? rest.Substring(0, rest.Length - 7) : rest;
        }

        int Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].id == key || entries[i].name == key || (entries[i].prefab != null && entries[i].prefab.name == key)) return i;
            return -1;
        }

        static string Load(string key)
        {
            try { return PlayerPrefs.GetString(key, ""); }
            catch { return ""; }
        }

        static void Save(string key, string value)
        {
            try { PlayerPrefs.SetString(key, value); }
            catch { /* the choice is only a convenience */ }
        }

        // ------------------------------------------------------------------ building a map

        void Open(int index, bool beforeSpawn)
        {
            if (entries.Count == 0) return;
            current = (index % entries.Count + entries.Count) % entries.Count;
            var entry = entries[current];
            Save(LastMapKey, entry.id);
            if (built != null)
            {
                built.SetActive(false);
                Destroy(built);
            }
            if (orbit != null && orbit.FreeFly) orbit.StopFly();

            try
            {
                if (entry.prefab == null)
                {
                    var yard = new GameObject(CalibrationName);
                    yard.SetActive(false);
                    yard.transform.SetParent(mapRoot, false);
                    try { PawnRushCalibrationYard.Build(new CourseBuilder(kit, yard.transform)); }
                    finally { yard.SetActive(true); }
                    built = yard;
                }
                else
                {
                    built = Instantiate(entry.prefab, mapRoot, false);
                    built.name = entry.prefab.name + " (test)";
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[MapTest] 맵 '" + entry.name + "'을(를) 만들다가 오류가 났습니다.\n" + e);
            }
            Physics.SyncTransforms();
            bounds = Measure(built);
            Vector3 position = FindStart(out var rotation);
            if (beforeSpawn && startPoint != null) startPoint.SetPositionAndRotation(position, rotation);
            else if (spawner != null && spawner.Driver != null) spawner.SetStart(position, rotation);
            Debug.Log("[MapTest] 맵: " + entry.name + " · 시작점 " + startFrom + " " + position.ToString("0.0"));
        }

        Vector3 FindStart(out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (built == null) { startFrom = "원점"; return Vector3.zero; }
            SpawnPoint best = null;
            foreach (var point in built.GetComponentsInChildren<SpawnPoint>())
                if (best == null || Rank(point) < Rank(best)) best = point;
            if (best != null)
            {
                startFrom = "SpawnPoint " + best.Index;
                rotation = Flat(best.transform.rotation);
                return best.transform.position;
            }
            foreach (var t in built.GetComponentsInChildren<Transform>())
                if (t.name == "Start")
                {
                    startFrom = "Start";
                    rotation = Flat(t.rotation);
                    return Grounded(t.position, out var top) ? top : t.position;
                }
            startFrom = "자동(남쪽 가장자리)";
            var edge = new Vector3(bounds.center.x, bounds.max.y + 5f, bounds.min.z + 3f);
            return Grounded(edge, out var floor) ? floor : new Vector3(edge.x, bounds.max.y, edge.z);
        }

        static int Rank(SpawnPoint p) => p.Index * 2 + (p.Team == Teams.White ? 0 : 1);

        static Quaternion Flat(Quaternion q)
        {
            var f = q * Vector3.forward;
            f.y = 0f;
            return f.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(f.normalized) : Quaternion.identity;
        }

        // The nearest walkable top under a point of the map (from just above it).
        bool Grounded(Vector3 from, out Vector3 top)
        {
            var hits = Physics.RaycastAll(from + Vector3.up * 2f, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
                if (built != null && hit.collider.transform.IsChildOf(built.transform) && hit.normal.y > .7f)
                {
                    top = hit.point;
                    return true;
                }
            top = default;
            return false;
        }

        static Bounds Measure(GameObject map)
        {
            var box = new Bounds(Vector3.zero, Vector3.one * 10f);
            if (map == null) return box;
            bool any = false;
            foreach (var r in map.GetComponentsInChildren<Renderer>())
            {
                if (!any) { box = r.bounds; any = true; }
                else box.Encapsulate(r.bounds);
            }
            foreach (var c in map.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                if (!any) { box = c.bounds; any = true; }
                else box.Encapsulate(c.bounds);
            }
            return box;
        }

        // ------------------------------------------------------------------ keys

        void Update()
        {
            if (!Application.isFocused || GameMenu.IsOpen || ChatBox.KeysHeld) return;
            if (Input.GetKeyDown(KeyCode.H)) help = !help;
            if (Input.GetKeyDown(KeyCode.F3))
            {
                slow = (slow + 1) % SlowSteps.Length;
                Time.timeScale = SlowSteps[slow];
            }
            if (spawner == null || spawner.Driver == null || entries.Count == 0) return;
            if (Input.GetKeyDown(KeyCode.LeftBracket)) Open(current - 1, false);
            if (Input.GetKeyDown(KeyCode.RightBracket)) Open(current + 1, false);
            if (Input.GetKeyDown(KeyCode.F5)) Open(current, false);
            if (Input.GetKeyDown(KeyCode.F2)) Overview();
            if (Input.GetKeyDown(KeyCode.T)) DropWhereLooking();
            for (int i = 0; i < PieceKeys.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && spawner.Driver is IPromotable piece) piece.Promote(PieceKeys[i]);
        }

        // From high over the south edge, looking north over the map; again back to the pawn.
        void Overview()
        {
            if (orbit == null) return;
            if (orbit.FreeFly) { orbit.StopFly(); return; }
            float span = Mathf.Max(20f, Mathf.Max(bounds.size.x, bounds.size.z));
            orbit.FlyAt(new Vector3(bounds.center.x, bounds.max.y + span * .45f, bounds.min.z - span * .3f), 0f, 40f);
        }

        // The pawn onto the floor the camera looks at (free camera or not), facing the way the camera looks. R and a
        // fall still go back to the map's start or its last checkpoint.
        void DropWhereLooking()
        {
            if (orbit == null) return;
            var view = orbit.transform;
            var own = spawner.Driver.FollowTarget != null ? spawner.Driver.FollowTarget.root : null;
            var hits = Physics.RaycastAll(view.position, view.forward, 2000f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (own != null && hit.collider.transform.IsChildOf(own)) continue;
                var at = hit.point;
                if (hit.normal.y <= .7f && !Grounded(at - view.forward * .5f, out at)) continue;
                var facing = Flat(Quaternion.LookRotation(view.forward));
                if (orbit.FreeFly) orbit.StopFly();
                spawner.MoveTo(at, facing);
                return;
            }
        }

        // ------------------------------------------------------------------ help

        void OnGUI()
        {
            var driver = spawner != null ? spawner.Driver : null;
            if (driver == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true };
            if (driver is IStaminaReadout readout) StaminaBar(readout.Stamina01);
            if (!help)
            {
                Panel(new Rect(12f, 12f, 0f, 0f), "<b>" + CurrentMap + "</b>  (H 도움말)");
                return;
            }
            var lines = new List<string>
            {
                "<b>폰 러쉬 맵 시험 씬</b>  (H 도움말 끄기)",
                "WASD 이동 · Space 점프 · Shift 질주 · 좌클릭 다이빙 · 우클릭 꾹 잡기",
                "F 스킬 · 1 폰 · 2 나이트 · 3 비숍 · 4 룩 · 5 퀸 · 6 킹",
                "[ ] 이전/다음 맵 · F5 맵 다시 짓기 · T 보는 곳으로 이동",
                "F4 자유 카메라 · F1 캐릭터 비행(개발) · F2 맵 전체 보기 · F3 느리게(" + SlowSteps[slow].ToString("0.##") + "배)",
                "R 체크포인트로 · Backspace 맵 처음부터",
                ""
            };
            for (int i = 0; i < entries.Count; i++)
                lines.Add((i == current ? "<color=#FFD24A>▶ " : "<color=#DDDDDD>   ") + entries[i].name + "</color>");
            if (entries.Count <= (includeCalibrationYard ? 1 : 0))
                lines.Add("<color=#FF9A7A>" + MapFolder + "에 맵 프리팹을 넣으면 여기에 나옵니다</color>");
            lines.Add("");
            lines.Add("시작점: " + startFrom);
            var target = driver.FollowTarget;
            if (target != null)
            {
                var p = target.position;
                var body = target.GetComponent<Rigidbody>();
                float speed = body != null ? new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude : 0f;
                lines.Add($"위치 x {p.x:0.0} · y {p.y:0.0} · z {p.z:0.0} · 속도 {speed:0.0} m/s");
            }
            if (driver is IPromotable piece) lines.Add("지금 기물: <b>" + ChessPieces.Name(piece.Piece) + "</b>");
            Panel(new Rect(12f, 12f, 0f, 0f), string.Join("\n", lines));
        }

        void Panel(Rect at, string text)
        {
            var size = style.CalcSize(new GUIContent(text));
            var rect = new Rect(at.x, at.y, size.x + 16f, size.y + 12f);
            GUI.color = new Color(0f, 0f, 0f, .55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, size.x, size.y), text, style);
        }

        // The playtest's own bar (its help, which draws it, is off here): green to red as it empties.
        void StaminaBar(float stamina)
        {
            if (white == null) white = Texture2D.whiteTexture;
            const float width = 240f, height = 12f;
            var back = new Rect((Screen.width - width) * .5f, Screen.height - 44f, width, height);
            var old = GUI.color;
            GUI.color = new Color(.08f, .1f, .14f, .7f);
            GUI.DrawTexture(back, white);
            GUI.color = Color.Lerp(new Color(1f, .32f, .26f), new Color(.4f, .92f, .46f), stamina);
            GUI.DrawTexture(new Rect(back.x + 2f, back.y + 2f, (width - 4f) * Mathf.Clamp01(stamina), height - 4f), white);
            GUI.color = old;
        }
    }
}
