using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // StateDrivenMover: standalone offline fixture. Switches respond to unique rider counts.
    // Assignment keys place test teammates, not network players.
    public sealed partial class ChessTimerMinigame : MonoBehaviour
    {
        public RagdollPawn pawnPrefab;
        public RagdollTuning tuning;
        [Header("Round settings (F3 applies changes)")]
        [SerializeField, Min(1)] float timeLimit = 45;
        [SerializeField, Min(1)] float secondsPerStage = 10;
        [SerializeField, Min(0)] float switchDelay = .35f;
        [SerializeField, Min(0)] float stunSeconds = 3;
        [SerializeField, Min(0)] float retryGrace = 3;
        [SerializeField, Min(0)] float blastSpeed = 5;
        [SerializeField, Min(1)] int soloRequiredRiders = 2;
        [SerializeField, Min(.1f)] float interactDistance = 1.8f;
        readonly List<RagdollPawn> players = new List<RagdollPawn>();
        [SerializeField, Tooltip("B toggles rooftop sparring for unselected switch occupants.")] bool sparringBots = true;
        [SerializeField, Min(1)] float successBlastSpeed = 22f;
        [SerializeField, Range(.5f, 2.8f)] float successRespawnSeconds = 2.4f;
        readonly float[] respawnAt = new float[12];
        readonly float[] nextShove = new float[12];
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        readonly int[] roles = new int[12]; // 0: manual, 1: own plate, 2: defuse, 4: opposite plate.
        readonly int[] riders = new int[2], workers = new int[2];
        readonly bool[] exited = new bool[12];
        Rigidbody rocker;
        ChessTimerPlate occupancy;
        readonly Rigidbody[] entryFlaps = new Rigidbody[2];
        Quaternion rockerDelta = Quaternion.identity;
        Vector3 rockerAngularVelocity;
        float rockerAngle;
        [SerializeField, Range(0f, 3f)] float rockerTilt = 3f;
        [SerializeField, Min(.1f)] float rockerSpeed = 3f;
        readonly GameObject[] gates = new GameObject[2];
        readonly TextMesh[] clocks = new TextMesh[2], clockStatus = new TextMesh[2], crowdDisplays = new TextMesh[2];
        readonly TextMesh[,] panelLabels = new TextMesh[2, 3];
        readonly Material[] teamMaterials = new Material[2];
        Material blastMaterial;
        ChessTimerRules rules;
        LabCamera cameraRig;
        Vector3 savedGravity;
        CursorLockMode savedLock;
        bool savedCursor, initialized, menu = true, started;
        int selected;
        GUIStyle text, title, buttonStyle;
        string notice = "준비 완료 · Enter로 시작";
        static float TeamX(int team) => team == 0 ? -5.5f : 5.5f;
        static Vector3 Panel(int team, int stage) => new Vector3(TeamX(team) + (stage - 1) * 3f, .7f, 12);
        static Vector3 Waiting(int index) => new Vector3(TeamX(index / 6) + (index < 6 ? -9.5f : 9.5f) + (index % 3 - 1) * 1.6f, 0, -24 - index % 2);

        void Awake()
        {
            cameraRig = FindFirstObjectByType<LabCamera>();
            if (pawnPrefab == null || tuning == null || cameraRig == null)
            { Debug.LogError("ChessTimerMinigame requires pawn, tuning and LabCamera.", this); enabled = false; return; }
            savedGravity = Physics.gravity; savedLock = Cursor.lockState; savedCursor = Cursor.visible;
            Physics.gravity = Vector3.down * 9.81f * tuning.values.gravityScale;
            initialized = true;
            rules = new ChessTimerRules(timeLimit, secondsPerStage, stunSeconds + retryGrace, switchDelay, soloRequiredRiders);
            BuildArena();
            blastMaterial = Mat(new Color(1f, .45f, .08f));
            blastMaterial.EnableKeyword("_EMISSION");
            blastMaterial.SetColor("_EmissionColor", new Color(3f, 1.2f, .1f));
        }
        void Start()
        {
            if (!initialized) return;
            for (int i = 0; i < 12; i++)
            {
                var pawn = Instantiate(pawnPrefab, Waiting(i) + Vector3.up, Quaternion.identity);
                pawn.transform.SetParent(transform, true);
                pawn.tuning = tuning; pawn.Team = i / 6;
                pawn.DisplayName = (i < 6 ? "청팀 " : "홍팀 ") + (i % 6 + 1);
                if (pawn.skin != null) pawn.skin.sharedMaterial = teamMaterials[i / 6];
                foreach (var body in pawn.bodies) { body.solverIterations = 24; body.solverVelocityIterations = 6; }
                players.Add(pawn);
            }
            ResetRound(); Select(0); RefreshCursor();
        }
        void Select(int index)
        {
            if (players.Count == 0) return;
            players[selected].SetInput(default);
            selected = (index + players.Count) % players.Count;
            cameraRig.soloTarget = players[selected]; cameraRig.distance = 7; cameraRig.maxDistance = 20;
        }
        void Place(int index, Vector3 ground)
        {
            players[index].SetInput(default);
            players[index].Teleport(ground + Vector3.up * (players[index].standHeight + .04f), Vector3.forward);
        }
        void Assign(int index, int role)
        {
            int team = index / 6;
            if (respawnAt[index] > 0 || rules.Teams[team].Recovery > 0 || players[index].State != PawnState.Active) return;
            if (role == 2 && rules.Teams[team].Complete) return;
            roles[index] = role;
            if (role == 1 || role == 4)
            {
                int side = role == 4 ? 1 - team : team;
                Place(index, rocker.transform.TransformPoint(new Vector3(TeamX(side) + (index % 3 - 1) * 1.6f, RockerLocalTop, -1 + index % 2 * 2)));
            }
            else if (role == 2) PlaceAtPanel(index);
            else Place(index, Waiting(index));
        }
        void PlaceAtPanel(int index)
        {
            var p = Panel(index / 6, Mathf.Min(2, rules.Teams[index / 6].Stage));
            Place(index, new Vector3(p.x + (index % 3 - 1) * .45f, 0, p.z - 1));
        }
        void ResetRound()
        {
            rules = new ChessTimerRules(timeLimit, secondsPerStage, stunSeconds + retryGrace, switchDelay, soloRequiredRiders);
            started = false; notice = "준비 완료 · Enter로 시작";
            for (int i = 0; i < players.Count; i++)
            { respawnAt[i] = 0; exited[i] = false; roles[i] = 0; Place(i, Waiting(i)); }
            occupancy.ClearContacts();
            // Equal initial allocation. Switch to each teammate to change it with 1/2/3.
            for (int team = 0; team < 2; team++)
            { Assign(team * 6 + 1, 1); Assign(team * 6 + 2, 1); Assign(team * 6 + 3, 2); }
            for (int side = 0; side < 2; side++)
            {
                gates[side].SetActive(true);
                riders[side] = workers[side] = 0;
            }
        }
        void Update()
        {
            if (players.Count == 0) return;
            for (int i = 0; i < players.Count; i++)
                if (respawnAt[i] > 0 && Time.time >= respawnAt[i])
                {
                    respawnAt[i] = 0; roles[i] = 0; exited[i] = false;
                    Place(i, Waiting(i));
                }
            if (Application.isFocused)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { menu = !menu; RefreshCursor(); }
                if (Input.GetKeyDown(KeyCode.Return)) { started = true; notice = "해체 진행 중 · 발판과 해체 인원을 배분하세요"; menu = false; RefreshCursor(); }
                if (Input.GetKeyDown(KeyCode.Tab)) Select(selected + 1);
                if (Input.GetKeyDown(KeyCode.T)) Select((selected + 6) % 12);
                if (Input.GetKeyDown(KeyCode.F3)) ResetRound();
                if (Input.GetKeyDown(KeyCode.Alpha1)) Assign(selected, 1);
                if (Input.GetKeyDown(KeyCode.Alpha2)) Assign(selected, 2);
                if (Input.GetKeyDown(KeyCode.Alpha3)) Assign(selected, 0);
                if (Input.GetKeyDown(KeyCode.Alpha4)) Assign(selected, 4);
                if (Input.GetKeyDown(KeyCode.B)) sparringBots = !sparringBots;
                if (Input.GetKeyDown(KeyCode.R) && respawnAt[selected] <= 0 && rules.Teams[selected / 6].Recovery <= 0 && players[selected].State == PawnState.Active)
                { roles[selected] = 0; Place(selected, Waiting(selected)); }
            }
            foreach (var pawn in players) pawn.SetInput(default);
            DriveSparringBots();
            var local = players[selected];
            if (Application.isFocused && !menu && respawnAt[selected] <= 0 && rules.Teams[selected / 6].Recovery <= 0)
            {
                var move = GameSettings.Move;
                if (move.sqrMagnitude > .01f) roles[selected] = 0;
                local.SetInput(new PawnInput {
                    move = Vector3.ClampMagnitude(cameraRig.FlatRight * move.x + cameraRig.FlatForward * move.y, 1),
                    aim = cameraRig.AimForward, jump = GameSettings.Pressed(GameKey.Jump),
                    sprint = GameSettings.Held(GameKey.Sprint), grab = Input.GetMouseButton(1),
                    shove = Input.GetMouseButtonDown(0), shoveHeld = Input.GetMouseButton(0)
                });
            }
            for (int i = 0; i < players.Count; i++)
            {
                var pawn = players[i];
                if (!pawn.IsFinite() || (respawnAt[i] <= 0 && pawn.Hips.position.y < -5))
                { roles[i] = 0; Place(i, Waiting(i)); }
                if (rules.Teams[i / 6].Complete && pawn.Hips.position.z > 24 && Mathf.Abs(pawn.Hips.position.x - TeamX(i / 6)) < 2.5f)
                    exited[i] = true;
            }
            UpdateViews();
        }
        void FixedUpdate()
        {
            if (players.Count != 12) return;
            MoveRocker();
            occupancy.Count(out riders[0], out riders[1]);
            workers[0] = workers[1] = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var pawn = players[i]; int team = i / 6;
                if (respawnAt[i] > 0 || rules.Teams[team].Complete || rules.Teams[team].Recovery > 0 || pawn.State != PawnState.Active) continue;
                var origin = pawn.Hips.position;
                bool working = roles[i] == 2 || (i == selected && !menu && Application.isFocused && Input.GetKey(KeyCode.F));
                if (working && pawn.Grounded && Vector3.Distance(origin, Panel(team, rules.Teams[team].Stage)) <= interactDistance)
                    workers[team]++;
            }
            if (!started) return;
            int blueStage = rules.Teams[0].Stage, redStage = rules.Teams[1].Stage;
            float blueRecovery = rules.Teams[0].Recovery, redRecovery = rules.Teams[1].Recovery;
            int blasts = rules.Step(Time.fixedDeltaTime, riders[0], riders[1], workers[0], workers[1]);
            for (int team = 0; team < 2; team++)
            {
                if ((blasts & (1 << team)) != 0) Explode(team);
                bool recovered = (team == 0 ? blueRecovery : redRecovery) > 0 && rules.Teams[team].Recovery <= 0;
                bool changed = (team == 0 ? blueStage : redStage) != rules.Teams[team].Stage;
                if (rules.Teams[team].Complete) { gates[team].SetActive(false); continue; }
                if (recovered || changed)
                    for (int i = team * 6; i < team * 6 + 6; i++)
                        if (respawnAt[i] > 0) continue;
                        else if (recovered) Assign(i, roles[i]);
                        else if (roles[i] == 2) PlaceAtPanel(i);
            }
            if (rules.CompletedTeams != 0) CompletionBlast(rules.CompletedTeams);
        }
        void Explode(int team)
        {
            notice = (team == 0 ? "청팀" : "홍팀") + " 폭발! 완료 단계 유지 · 현재 단계 초기화";
            for (int i = team * 6; i < team * 6 + 6; i++)
            {
                if (respawnAt[i] > 0) continue;
                var position = players[i].Hips.position;
                var away = position - new Vector3(TeamX(team), 0, 3); away.y = 0;
                StartCoroutine(BlastFlash(position));
                players[i].TakeHit(away.normalized * blastSpeed + Vector3.up * blastSpeed, stunSeconds, 0, true);
            }
        }
        static bool Holds(PawnHand hand, RagdollPawn target) =>
            hand != null && hand.IsHolding && hand.HeldCollider != null &&
            RagdollPawn.ColliderOwner.TryGetValue(hand.HeldCollider, out var owner) && owner == target;

        void CompletionBlast(int completedTeams)
        {
            var direct = new bool[players.Count];
            var impulses = new Vector3[players.Count];
            float delay = Mathf.Clamp(successRespawnSeconds, .5f, 2.8f);
            float speed = Mathf.Max(1, successBlastSpeed);
            // Snapshot victims and grips before knockdown releases any hands.
            for (int i = 0; i < players.Count; i++)
            {
                var position = players[i].Hips.position;
                direct[i] = (completedTeams & (1 << (1 - i / 6))) != 0 &&
                    Mathf.Abs(position.x) <= ArenaWidth * .5f && Mathf.Abs(position.z) <= ArenaDepth * .5f &&
                    position.y >= -5 && position.y <= 30;
                if (!direct[i]) continue;
                var direction = Waiting(i) - position; direction.y = 0;
                impulses[i] = direction.normalized * speed + Vector3.up * speed;
            }
            for (int i = 0; i < players.Count; i++)
            {
                if (direct[i]) continue;
                for (int victim = 0; victim < players.Count; victim++)
                    if (direct[victim] && (Holds(players[i].handL, players[victim]) || Holds(players[i].handR, players[victim])))
                    {
                        // Transfer the held victim's launch once, without detonating or respawning the holder.
                        impulses[i] = impulses[victim]; break;
                    }
            }
            for (int i = 0; i < players.Count; i++)
            {
                if (direct[i])
                {
                    roles[i] = 0;
                    respawnAt[i] = Time.time + delay;
                    StartCoroutine(BlastFlash(players[i].Hips.position));
                    players[i].TakeHit(impulses[i], delay, 0, true);
                }
                else if (impulses[i].sqrMagnitude > 0)
                    players[i].TakeHit(impulses[i], 0, 0, false);
            }
            notice = "해체 성공! 문 열림 · 적팀 폭발 후 입구 복귀 · 잡고 있으면 함께 날아갑니다";
        }
        System.Collections.IEnumerator BlastFlash(Vector3 position)
        {
            var root = new GameObject("Bomb explosion");
            root.transform.SetParent(transform, false); root.transform.position = position;
            var sparks = new Transform[12];
            var directions = new Vector3[12];
            for (int i = 0; i < sparks.Length; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.transform.SetParent(root.transform, false);
                var collider = spark.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                spark.GetComponent<Renderer>().sharedMaterial = blastMaterial;
                sparks[i] = spark.transform;
                directions[i] = Quaternion.Euler(i * 137.5f, i * 67f, 0) * Vector3.up;
            }
            for (float elapsed = 0; elapsed < .9f; elapsed += Time.deltaTime)
            {
                float t = elapsed / .9f;
                for (int i = 0; i < sparks.Length; i++)
                {
                    sparks[i].localPosition = directions[i] * (t * 5f);
                    sparks[i].localScale = Vector3.one * Mathf.Lerp(1.4f, .05f, t);
                }
                yield return null;
            }
            Destroy(root);
        }
        void DriveSparringBots()
        {
            if (!started || !sparringBots) return;
            for (int i = 0; i < players.Count; i++)
            {
                var pawn = players[i];
                if (respawnAt[i] > 0 || i == selected || (roles[i] != 1 && roles[i] != 4) || rules.Teams[i / 6].Recovery > 0 ||
                    pawn.State != PawnState.Active || pawn.Hips.position.y < 5.2f) continue;
                RagdollPawn enemy = null; float nearest = float.PositiveInfinity;
                foreach (var other in players)
                {
                    if (other.Team == pawn.Team || other.Hips.position.y < 5.2f) continue;
                    float distance = (other.Hips.position - pawn.Hips.position).sqrMagnitude;
                    if (distance < nearest) { enemy = other; nearest = distance; }
                }
                if (enemy == null) continue;
                Vector3 delta = enemy.Hips.position - pawn.Hips.position; delta.y = 0;
                Vector3 walk = delta;
                bool shove = delta.magnitude < 1.4f && Time.time >= nextShove[i];
                if (shove) nextShove[i] = Time.time + 1.3f;
                pawn.SetInput(new PawnInput { move = delta.magnitude > .7f ? walk.normalized * .65f : Vector3.zero,
                    aim = delta.normalized, shove = shove, shoveHeld = shove });
            }
        }
        void RefreshCursor() { Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = menu; }
        void OnApplicationFocus(bool focus) { if (initialized && !focus) { menu = true; RefreshCursor(); } }
        void UpdateViews()
        {
            for (int team = 0; team < 2; team++)
            {
                var state = rules.Teams[team];
                string status = state.Complete ? "해체 완료 / 문 열림" : state.Recovery > 0 ? "폭발 / 재정비" : rules.PressedTeam == team ? "시간 정지" : "시간 감소";
                int seconds = Mathf.CeilToInt(state.Remaining);
                clocks[team].text = $"{seconds / 60:00}:{seconds % 60:00}";
                UpdateDigits(team, seconds);
                crowdDisplays[team].text = $"{riders[team]:00}\nPLAYERS";
                clockStatus[team].text = (team == 0 ? "BLUE · " : "RED · ") + status;
                for (int stage = 0; stage < 3; stage++)
                    panelLabels[team, stage].text = (stage == 0 ? "PAWN" : stage == 1 ? "KNIGHT" : "ROOK") + "\n" +
                        (stage < state.Stage ? "OK" : stage == state.Stage ? $"F : {state.Work / rules.WorkSeconds:P0}" : "LOCKED");
            }
        }
        Material Mat(Color color)
        {
            var material = new Material(Shader.Find("Standard")) { color = color };
            materials.Add(material); return material;
        }
        GameObject Box(string name, Vector3 pos, Vector3 scale, Material material, bool solid = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(transform, false);
            box.transform.localPosition = pos; box.transform.localScale = scale; box.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { var collider = box.GetComponent<Collider>(); collider.enabled = false; Destroy(collider); }
            return box;
        }
        TextMesh Label(string name, Vector3 pos, string value, float size = .3f)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false); go.transform.localPosition = pos;
            var label = go.AddComponent<TextMesh>(); label.text = value; label.characterSize = size; label.fontSize = 48;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.font = RuntimePanels.KoreanFont;
            if (label.font != null) go.GetComponent<Renderer>().sharedMaterial = label.font.material;
            return label;
        }
        void OnGUI()
        {
            if (players.Count == 0) return;
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 16, wordWrap = true };
                title = new GUIStyle(text) { fontSize = 22, fontStyle = FontStyle.Bold };
                buttonStyle = new GUIStyle(GUI.skin.button) { font = RuntimePanels.KoreanFont, fontSize = 16 };
            }
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Min(Screen.width / 1200f, Screen.height / 800f));
            GUI.Box(new Rect(12, 12, 780, 250), GUIContent.none);
            GUI.Label(new Rect(24, 20, 750, 30), "체스 타이머 폭탄 · 오프라인 6 대 6 프로토타입", title);
            for (int team = 0; team < 2; team++)
            {
                var state = rules.Teams[team]; int escaped = 0;
                for (int i = team * 6; i < team * 6 + 6; i++) if (exited[i]) escaped++;
                GUI.Label(new Rect(24, 58 + team * 38, 750, 36),
                    $"{(team == 0 ? "청팀" : "홍팀")} {state.Remaining:0.0}초 | 발판 전체 {riders[team]}명 · 해체 {workers[team]}명 | {state.Stage}/3단계 · {state.Work / rules.WorkSeconds:P0} | " +
                    (state.Complete ? $"문 열림 · 통과 {escaped}/6" : state.Recovery > 0 ? $"재정비 {state.Recovery:0.0}초" : rules.PressedTeam == team ? "시간 정지" : "시간 감소"), text);
            }
            GUI.Label(new Rect(24, 138, 750, 40), $"선택: {players[selected].DisplayName} · {(roles[selected] == 1 ? "발판" : roles[selected] == 2 ? "자동 해체" : roles[selected] == 4 ? "상대 발판" : "직접 조작 / 대기")}\n{notice}", text);
            GUI.Label(new Rect(24, 183, 750, 74), "WASD 이동 · Space 점프 · 좌클릭 밀치기 · 우클릭 잡기 · F 해체\nTab 팀원 · T 팀 변경 · 1 우리 발판 / 2 해체 / 3 대기 / 4 상대 발판\nB 대련 봇 · R 복귀 · F3 초기화 · Enter 시작 · Esc 메뉴", text);
            if (menu)
            {
                GUI.Box(new Rect(12, 275, 780, 170), GUIContent.none);
                GUI.Label(new Rect(24, 282, 745, 65), $"발판 위 인원은 적·아군 모두 셉니다. 동수면 양쪽 시간 감소. 대련 봇: {(sparringBots ? "켜짐" : "꺼짐")} (B)\n한 타이머 위에서 싸웁니다. 상대 쪽으로 가면 우리 시간이 흐를 수 있습니다.\n타이머 뒤쪽 PAWN → KNIGHT → ROOK에서 F 해체. 숫자키 배치는 시험용 순간이동입니다.", text);
                if (GUI.Button(new Rect(24, 363, 235, 55), started ? "계속 조작" : "게임 시작", buttonStyle))
                { started = true; notice = "해체 진행 중 · 발판과 해체 인원을 배분하세요"; menu = false; RefreshCursor(); }
                if (GUI.Button(new Rect(275, 363, 235, 55), "다음 팀원", buttonStyle)) Select(selected + 1);
                if (GUI.Button(new Rect(526, 363, 235, 55), "전체 초기화", buttonStyle)) ResetRound();
            }
            GUI.matrix = previous;
        }
        void OnDestroy()
        {
            if (!initialized) return;
            Physics.gravity = savedGravity; Cursor.lockState = savedLock; Cursor.visible = savedCursor;
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
