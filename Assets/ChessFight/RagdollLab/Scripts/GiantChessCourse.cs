using System;
using System.Collections.Generic;
using System.Globalization;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // StateDrivenMover: offline authored chess replay; not a network-authoritative course.
    public sealed class GiantChessCourse : MonoBehaviour
    {
        public RagdollPawn pawnPrefab;
        public RagdollTuning tuning;
        [SerializeField, Min(10)] float tileSize = 18;
        [SerializeField, Range(.25f, 4f), Tooltip("Replay speed only; does not speed up players or physics.")] float replaySpeed = 2;
        [SerializeField, Min(1)] float moveSeconds = 3;
        [SerializeField, Min(1)] float warningSeconds = 5;
        [SerializeField, Min(.5f)] float restSeconds = 2;
        [SerializeField, Min(1)] float knockbackSpeed = 24;
        [SerializeField, Min(0)] float upwardSpeed = 9;
        [SerializeField] bool botsEnabled = true;
        readonly List<Material> materials = new List<Material>();
        readonly List<RagdollPawn> players = new List<RagdollPawn>();
        readonly GameObject[] pieces = new GameObject[64], warnings = new GameObject[64];
        readonly GameObject[] occupiedMarks = new GameObject[64];
        readonly bool[] finished = new bool[12];
        readonly Vector3[] botGoals = new Vector3[12];
        readonly float[] nextShove = new float[12];
        readonly Transform[] hands = new Transform[2], arms = new Transform[2];
        readonly Vector3[] shoulders = new Vector3[2];
        Material white, black, gold, red;
        LabCamera cameraRig;
        string[][] score;
        int selected, ply, phase, from, to, rookFrom, rookTo;
        float elapsed;
        ulong attack;
        bool running, menu = true, initialized;
        GameObject moving;
        Vector3 savedGravity;
        CursorLockMode savedLock;
        bool savedCursor;
        GUIStyle hud;
        // Rotate chess coordinates 90 degrees: white home rank faces the left GM.
        Vector3 Square(int square) => new Vector3((square / 8 - 3.5f) * tileSize, 0, (7 - square % 8 + .5f) * tileSize);
        int BoardSquare(Vector3 p)
        {
            int rank = Mathf.FloorToInt(p.x / tileSize + 4), row = Mathf.FloorToInt(p.z / tileSize);
            return rank >= 0 && rank < 8 && row >= 0 && row < 8 ? rank * 8 + 7 - row : -1;
        }
        bool Occupied(int s) => pieces[s] && !(phase == 1 && pieces[s] == moving);
        void RefreshOccupiedMarks()
        {
            for (int s = 0; s < 64; s++) occupiedMarks[s].SetActive(Occupied(s));
        }
        Vector3 Spawn(int i) => new Vector3((i / 6 == 0 ? -1 : 1) * tileSize + (i % 3 - 1) * 2.5f, 0, -10 - i % 2 * 3);
        Material Mat(Color color) { var m = new Material(Shader.Find("Standard")) { color = color }; materials.Add(m); return m; }
        GameObject Shape(string name, Vector3 pos, Vector3 size, Material mat, PrimitiveType type = PrimitiveType.Cube, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(transform, false);
            go.transform.localPosition = pos; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!solid) { var c = go.GetComponent<Collider>(); c.enabled = false; Destroy(c); }
            return go;
        }
        void Awake()
        {
            cameraRig = FindFirstObjectByType<LabCamera>();
            var data = Resources.Load<TextAsset>("GiantChessScore");
            if (!pawnPrefab || !tuning || !cameraRig || !data) { Debug.LogError("Giant chess requires pawn, tuning, camera and score.", this); enabled = false; return; }
            var lines = data.text.Trim().Split('\n'); score = new string[lines.Length][];
            for (int i = 0; i < lines.Length; i++) score[i] = lines[i].Trim().Split(',');
            savedGravity = Physics.gravity; savedLock = Cursor.lockState; savedCursor = Cursor.visible; initialized = true;
            Physics.gravity = Vector3.down * 9.81f * tuning.values.gravityScale;
            white = Mat(new Color(.88f, .85f, .74f)); black = Mat(new Color(.10f, .13f, .18f)); gold = Mat(new Color(.7f, .46f, .16f));
            red = Mat(new Color(.9f, .08f, .025f)); red.EnableKeyword("_EMISSION");
            for (int s = 0; s < 64; s++)
            {
                Shape("Board " + (char)('a' + s % 8) + (s / 8 + 1), Square(s) - Vector3.up * .4f,
                    new Vector3(tileSize, .8f, tileSize), (s % 8 + s / 8) % 2 == 0 ? white : black);
                warnings[s] = Shape("Attack warning", Square(s) + Vector3.up * .025f, new Vector3(tileSize - .3f, .04f, tileSize - .3f), red, solid: false);
                warnings[s].SetActive(false);
                var mark = new GameObject("Occupied square border"); mark.transform.SetParent(transform, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    var edge = Shape("Blocked square edge", Square(s) + new Vector3(side * (tileSize * .5f - .2f), .09f, 0), new Vector3(.35f, .12f, tileSize), gold, solid: false);
                    edge.transform.SetParent(mark.transform, true);
                    edge = Shape("Blocked square edge", Square(s) + new Vector3(0, .09f, side * (tileSize * .5f - .2f)), new Vector3(tileSize, .12f, .35f), gold, solid: false);
                    edge.transform.SetParent(mark.transform, true);
                }
                occupiedMarks[s] = mark;
            }
            Shape("Entry platform", new Vector3(0, -.4f, -10), new Vector3(tileSize * 8, .8f, 20), gold);
            Shape("Exit platform", new Vector3(0, -.4f, tileSize * 8 + 10), new Vector3(tileSize * 8, .8f, 20), gold);
            Shape("Finish stripe", new Vector3(0, .03f, tileSize * 8 + 3), new Vector3(tileSize * 8, .04f, 1), white, solid: false);
            for (int side = -1; side <= 1; side += 2)
                Shape("Exit arch pillar", new Vector3(side * 12, 5, tileSize * 8 + 12), new Vector3(2, 10, 2), black);
            Shape("Exit arch header", new Vector3(0, 10, tileSize * 8 + 12), new Vector3(26, 2, 2), gold);
            var sign = new GameObject("Exit sign"); sign.transform.SetParent(transform, false);
            sign.transform.localPosition = new Vector3(0, 10, tileSize * 8 + 10.9f);
            var label = sign.AddComponent<TextMesh>(); label.text = "다음 구간 · EXIT"; label.fontSize = 48; label.characterSize = .18f;
            label.anchor = TextAnchor.MiddleCenter; label.font = RuntimePanels.KoreanFont;
            if (label.font) sign.GetComponent<Renderer>().sharedMaterial = label.font.material;
            for (int side = 0; side < 2; side++) BuildGiant(side);
            ResetBoard();
        }
        void Start()
        {
            if (!initialized) return;
            for (int i = 0; i < 12; i++)
            {
                var pawn = Instantiate(pawnPrefab, Spawn(i) + Vector3.up * 2, Quaternion.identity, transform);
                pawn.tuning = tuning; pawn.Team = i / 6; pawn.DisplayName = (i < 6 ? "백팀 " : "흑팀 ") + (i % 6 + 1);
                if (pawn.skin) pawn.skin.sharedMaterial = i < 6 ? white : black;
                players.Add(pawn);
            }
            ResetPlayers(); Select(0); CursorState();
        }
        void BuildGiant(int side)
        {
            float x = (side == 0 ? -1 : 1) * (tileSize * 4 + 22), z = tileSize * 4;
            var suit = side == 0 ? black : white;
            Shape("GM seated torso", new Vector3(x, 16, z), new Vector3(18, 26, 22), suit, PrimitiveType.Capsule, false);
            Shape("GM head", new Vector3(x, 44, z), new Vector3(14, 17, 14), white, PrimitiveType.Sphere, false);
            for (int eye = -1; eye <= 1; eye += 2)
                Shape("GM eye", new Vector3(x + (side == 0 ? 6 : -6), 46, z + eye * 3), Vector3.one * 2, black, PrimitiveType.Sphere, false);
            shoulders[side] = new Vector3(x, 30, z - 7);
            arms[side] = Shape("GM reaching arm", shoulders[side], Vector3.one, suit, solid: false).transform;
            hands[side] = Shape("GM moving hand", shoulders[side], new Vector3(7, 3, 7), white, PrimitiveType.Sphere, false).transform;
        }
        void ResetBoard()
        {
            for (int s = 0; s < 64; s++) { if (pieces[s]) Destroy(pieces[s]); pieces[s] = null; warnings[s].SetActive(false); }
            string setup = "RNBQKBNRPPPPPPPP................................pppppppprnbqkbnr";
            for (int s = 0; s < 64; s++) if (setup[s] != '.') pieces[s] = BuildPiece(setup[s], s);
            ply = phase = 0; elapsed = 0; attack = 0; moving = null; running = false;
            RefreshOccupiedMarks();
        }
        GameObject BuildPiece(char code, int square)
        {
            bool light = char.IsUpper(code); char kind = char.ToUpperInvariant(code);
            string name = kind == 'P' ? "Pawn" : kind == 'R' ? "Rook" : kind == 'N' ? "Knight" : kind == 'B' ? "Bishop" : "Queen";
            var root = new GameObject(kind == 'K' ? "King" : name); root.transform.SetParent(transform, false); root.transform.position = Square(square);
            var prefab = Resources.Load<GameObject>("ChessTimerPieces/" + name);
            float height = 15;
            if (prefab)
            {
                var model = Instantiate(prefab, root.transform, false); // Preserve the importer axis correction.
                var renderers = model.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    bounds.Encapsulate(r.bounds); var slots = r.sharedMaterials;
                    for (int j = 0; j < slots.Length; j++) slots[j] = slots[j] && slots[j].name.Contains("Trim") ? gold : light ? white : black;
                    r.sharedMaterials = slots;
                }
                model.transform.localScale *= tileSize * .48f / Mathf.Max(bounds.size.x, bounds.size.z);
                bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                model.transform.position += new Vector3(root.transform.position.x - bounds.center.x, -bounds.min.y, root.transform.position.z - bounds.center.z);
                height = bounds.size.y;
            }
            else
            {
                var fallback = Shape(name, Square(square) + Vector3.up * 7.5f, new Vector3(7, 15, 7), light ? white : black);
                fallback.transform.SetParent(root.transform, true);
            }
            if (kind == 'K')
            {
                var cross = Shape("King cross vertical", Square(square) + Vector3.up * (height + 2), new Vector3(1, 5, 1), gold, solid: false); cross.transform.SetParent(root.transform, true);
                cross = Shape("King cross horizontal", Square(square) + Vector3.up * (height + 2.5f), new Vector3(4, 1, 1), gold, solid: false); cross.transform.SetParent(root.transform, true);
            }
            var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(tileSize, Mathf.Max(32, height), tileSize); collider.center = Vector3.up * collider.size.y * .5f;
            root.transform.rotation = Quaternion.Euler(0, light ? -90 : 90, 0);
            return root;
        }
        void ResetPlayers() { for (int i = 0; i < players.Count; i++) { finished[i] = false; Place(i); } }
        void Place(int i) { players[i].SetInput(default); players[i].Teleport(Spawn(i) + Vector3.up * (players[i].standHeight + .1f), Vector3.forward); botGoals[i] = Spawn(i); }
        void Select(int i) { selected = i % players.Count; cameraRig.soloTarget = players[selected]; cameraRig.distance = 12; cameraRig.maxDistance = 45; }
        void CursorState() { Cursor.visible = menu; Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked; }
        void OnApplicationFocus(bool focus) { if (initialized && !focus) { menu = true; CursorState(); } }
        void BeginMove()
        {
            if (ply >= score.Length) { running = false; return; }
            var row = score[ply]; from = int.Parse(row[0]); to = int.Parse(row[1]); rookFrom = int.Parse(row[2]); rookTo = int.Parse(row[3]);
            moving = pieces[from]; moving.GetComponent<Collider>().enabled = false;
            phase = 1; elapsed = 0; warnings[to].SetActive(true);
            if (rookTo >= 0) warnings[rookTo].SetActive(true);
            RefreshOccupiedMarks();
        }
        void Land()
        {
            if (pieces[to]) { pieces[to].GetComponent<Collider>().enabled = false; Destroy(pieces[to]); }
            pieces[to] = moving; pieces[from] = null; moving.transform.position = Square(to); moving.GetComponent<Collider>().enabled = true;
            if (rookFrom >= 0) { pieces[rookTo] = pieces[rookFrom]; pieces[rookFrom] = null; pieces[rookTo].transform.position = Square(rookTo); }
            // Evacuate before the next physics step: a full-square collider must not spawn inside a ragdoll.
            phase = 2;
            for (int i = 0; i < players.Count; i++)
                if (OnSquare(players[i], to) || (rookTo >= 0 && OnSquare(players[i], rookTo)))
                {
                    Vector3 p = players[i].Hips.position;
                    Vector3 safe = new Vector3(Mathf.Clamp(p.x, -tileSize * 4 + 2, tileSize * 4 - 2), 0, -3);
                    float distance = (safe - p).sqrMagnitude;
                    for (int s = 0; s < 64; s++) if (!Occupied(s))
                    {
                        Vector3 candidate = Square(s);
                        float score = (candidate - p).sqrMagnitude + (candidate.z > p.z ? tileSize : 0);
                        if (score < distance) { distance = score; safe = candidate; }
                    }
                    players[i].Teleport(safe + Vector3.up * (players[i].standHeight + .1f), Vector3.forward);
                    players[i].TakeHit(Vector3.back * knockbackSpeed + Vector3.up * upwardSpeed, 1, 0, true);
                }
            RefreshOccupiedMarks();
            attack = ulong.Parse(score[ply][4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            for (int s = 0; s < 64; s++) warnings[s].SetActive((attack & (1UL << s)) != 0);
            phase = 2; elapsed = 0;
        }
        bool OnSquare(RagdollPawn pawn, int s)
        {
            var p = pawn.Hips.position; var c = Square(s);
            return Mathf.Abs(p.x - c.x) < tileSize * .5f && Mathf.Abs(p.z - c.z) < tileSize * .5f && p.y > -2 && p.y < 25;
        }
        bool Dangerous(Vector3 p)
        {
            int s = BoardSquare(p);
            return s >= 0 && ((phase == 1 && (s == to || s == rookTo)) || (attack & (1UL << s)) != 0);
        }
        void Update()
        {
            if (players.Count == 0) return;
            if (Application.isFocused)
            {
                if (Input.GetKeyDown(KeyCode.Return)) { running = ply < score.Length; menu = false; CursorState(); }
                if (Input.GetKeyDown(KeyCode.Escape)) { menu = !menu; CursorState(); }
                if (Input.GetKeyDown(KeyCode.Tab)) Select((selected + 1) % 12);
                if (Input.GetKeyDown(KeyCode.B)) botsEnabled = !botsEnabled;
                if (Input.GetKeyDown(KeyCode.R)) Place(selected);
                if (Input.GetKeyDown(KeyCode.F3)) { ResetBoard(); ResetPlayers(); }
                if (Input.GetKeyDown(KeyCode.N) && running) elapsed = 1000; // Advance one phase, never skip a chess move.
            }
            bool active = running && !menu && Application.isFocused;
            if (active)
            {
                elapsed += Time.deltaTime * Mathf.Clamp(replaySpeed, .25f, 4f);
                if (phase == 0 && elapsed >= restSeconds) BeginMove();
                else if (phase == 1)
                {
                    float t = Mathf.Clamp01(elapsed / moveSeconds);
                    moving.transform.position = Vector3.Lerp(Square(from), Square(to), t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 22);
                    if (t >= 1) Land();
                }
                else if (phase == 2 && elapsed >= warningSeconds)
                {
                    for (int i = 0; i < players.Count; i++) if (Dangerous(players[i].Hips.position)) players[i].TakeHit(Vector3.back * knockbackSpeed + Vector3.up * upwardSpeed, 1.2f, 0, true);
                    phase = 3; elapsed = 0;
                }
                else if (phase == 3 && elapsed >= .5f)
                {
                    foreach (var w in warnings) w.SetActive(false); attack = 0; ply++; phase = 0; elapsed = 0;
                    if (ply == score.Length) running = false;
                }
            }
            red.SetColor("_EmissionColor", Color.red * (phase == 3 ? 4 : .4f + Mathf.PingPong(Time.time * 2, 1)));
            for (int side = 0; side < 2; side++)
            {
                Vector3 target = phase == 1 && ply % 2 == side && moving ? moving.transform.position + Vector3.up * 19 : shoulders[side] + Vector3.up * 3;
                hands[side].position = target; arms[side].position = (shoulders[side] + target) * .5f;
                arms[side].rotation = Quaternion.LookRotation(target - shoulders[side]); arms[side].localScale = new Vector3(5, 5, Vector3.Distance(target, shoulders[side]));
            }
            for (int i = 0; i < players.Count; i++)
            {
                var pawn = players[i]; pawn.SetInput(default);
                if (!pawn.IsFinite() || pawn.Hips.position.y < -8) Place(i);
                if (pawn.Hips.position.z > tileSize * 8 + 3 && Mathf.Abs(pawn.Hips.position.x) < tileSize * 4) finished[i] = true;
                if (i != selected && active && botsEnabled && !finished[i]) DriveBot(i);
            }
            // Manual movement remains available after the last move and after crossing the finish.
            if (!menu && Application.isFocused)
            {
                var input = GameSettings.Move;
                players[selected].SetInput(new PawnInput { move = Vector3.ClampMagnitude(cameraRig.FlatRight * input.x + cameraRig.FlatForward * input.y, 1), aim = cameraRig.AimForward,
                    jump = GameSettings.Pressed(GameKey.Jump), sprint = GameSettings.Held(GameKey.Sprint), grab = Input.GetMouseButton(1), shove = Input.GetMouseButtonDown(0), shoveHeld = Input.GetMouseButton(0) });
            }
        }
        void DriveBot(int i)
        {
            var pawn = players[i]; var p = pawn.Hips.position;
            Vector3 best = Vector3.zero; float bestScore = float.NegativeInfinity;
            // Short obstacle-aware steering; bots are test opponents, not puzzle-solving teammates.
            for (int direction = 0; direction < 16; direction++)
            {
                var d = Quaternion.Euler(0, direction * 22.5f, 0) * Vector3.forward;
                var future = p + d * 4;
                if (Mathf.Abs(future.x) > tileSize * 4 - 2 || future.z < -17) continue;
                int square = BoardSquare(future);
                bool blocked = square >= 0 && Occupied(square);
                if (blocked) continue;
                float value = d.z * 2 - (Dangerous(future) ? 8 : 0) + Vector3.Dot(d, botGoals[i]) * .2f;
                if (value > bestScore) { bestScore = value; best = d; }
            }
            botGoals[i] = best;
            bool shove = false; Vector3 aim = best;
            for (int other = 0; other < players.Count; other++) if (other / 6 != i / 6 && (players[other].Hips.position - p).sqrMagnitude < 9)
            { aim = (players[other].Hips.position - p).normalized; shove = Time.time > nextShove[i]; if (shove) nextShove[i] = Time.time + 2; break; }
            pawn.SetInput(new PawnInput { move = best, aim = aim, shove = shove });
        }
        void OnGUI()
        {
            if (!initialized) return;
            if (hud == null) hud = new GUIStyle(GUI.skin.label) { fontSize = 18, font = RuntimePanels.KoreanFont, wordWrap = true };
            int a = 0, b = 0; for (int i = 0; i < 12; i++) if (finished[i]) { if (i < 6) a++; else b++; }
            GUI.Box(new Rect(12, 12, 720, 155), "");
            string move = ply < score.Length ? $"{ply / 2 + 1}. {(ply % 2 == 0 ? "백" : "흑")} {score[ply][5]}" : "Rc2# · 기보 종료";
            GUI.Label(new Rect(24, 20, 690, 140), $"거인 GM 체스판 · {move}\n{(phase == 1 ? "기물 착지 예고" : phase == 2 ? "빨간 칸 공격 예고" : phase == 3 ? "충격파!" : "다음 수 준비")} · 백팀 도착 {a}/6 / 흑팀 {b}/6\nEnter 시작 · WASD 이동 · 마우스 좌 밀치기 / 우 잡기 · Space 점프\nTab 캐릭터 · B 봇 · R 입구 복귀 · F3 초기화 · N 다음 연출 단계 · Esc 기보 일시정지", hud);
        }
        void OnDestroy()
        {
            if (!initialized) return;
            Physics.gravity = savedGravity; Cursor.lockState = savedLock; Cursor.visible = savedCursor;
            foreach (var m in materials) if (m) Destroy(m);
        }
    }
}
