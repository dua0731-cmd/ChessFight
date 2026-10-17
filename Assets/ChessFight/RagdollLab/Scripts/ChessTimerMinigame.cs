using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // StateDrivenMover: standalone offline fixture. Switches respond to unique rider counts.
    // Assignment keys place test teammates, not network players.
    public sealed class ChessTimerMinigame : MonoBehaviour
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
        readonly List<Material> materials = new List<Material>();
        readonly int[] roles = new int[12]; // 0: wait/manual, 1: switch, 2: auto-defuse fixture.
        readonly int[] riders = new int[2], workers = new int[2];
        readonly bool[] exited = new bool[12];
        readonly BoxCollider[] pads = new BoxCollider[2];
        readonly Rigidbody[] plateBodies = new Rigidbody[2];
        readonly GameObject[] gates = new GameObject[2];
        readonly TextMesh[] clocks = new TextMesh[2];
        readonly TextMesh[,] panelLabels = new TextMesh[2, 3];
        readonly Material[] teamMaterials = new Material[2];
        ChessTimerRules rules;
        LabCamera cameraRig;
        Vector3 savedGravity;
        CursorLockMode savedLock;
        bool savedCursor, initialized, menu = true, started;
        int selected;
        GUIStyle text, title, buttonStyle;
        string notice = "준비 완료 · Enter로 시작";
        static float TeamX(int team) => team == 0 ? -5 : 5;
        static Vector3 Panel(int team, int stage) => new Vector3(TeamX(team) + (stage - 1) * 2.5f, .7f, -2.5f);
        static Vector3 Waiting(int index) => new Vector3(TeamX(index / 6) + (index % 3 - 1) * 1.2f, 0, -6 - index % 2);

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
            if (rules.Teams[team].Recovery > 0 || rules.Teams[team].Complete) return;
            roles[index] = role;
            if (role == 1)
                Place(index, new Vector3(TeamX(team) + (index % 3 - 1) * 1.3f, pads[team].bounds.max.y, 1 + index % 2 * 1.4f));
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
            { exited[i] = false; roles[i] = 0; Place(i, Waiting(i)); }
            // Equal initial allocation. Switch to each teammate to change it with 1/2/3.
            for (int team = 0; team < 2; team++)
            { Assign(team * 6 + 1, 1); Assign(team * 6 + 2, 1); Assign(team * 6 + 3, 2); }
            for (int team = 0; team < 2; team++) gates[team].SetActive(true);
        }
        void Update()
        {
            if (players.Count == 0) return;
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
                if (Input.GetKeyDown(KeyCode.R) && rules.Teams[selected / 6].Recovery <= 0)
                { roles[selected] = 0; Place(selected, Waiting(selected)); }
            }
            foreach (var pawn in players) pawn.SetInput(default);
            var local = players[selected];
            if (Application.isFocused && !menu && rules.Teams[selected / 6].Recovery <= 0)
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
                if (!pawn.IsFinite() || pawn.Hips.position.y < -5)
                { roles[i] = 0; Place(i, Waiting(i)); }
                if (rules.Teams[i / 6].Complete && pawn.Hips.position.z > 9 && Mathf.Abs(pawn.Hips.position.x - TeamX(i / 6)) < 2)
                    exited[i] = true;
            }
            UpdateViews();
        }
        void FixedUpdate()
        {
            if (players.Count != 12) return;
            for (int team = 0; team < 2; team++)
            {
                var target = new Vector3(TeamX(team), rules.PressedTeam == team ? 3f : 3.2f, 2);
                plateBodies[team].MovePosition(Vector3.MoveTowards(plateBodies[team].position, target, .3f * Time.fixedDeltaTime));
            }
            riders[0] = riders[1] = workers[0] = workers[1] = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var pawn = players[i]; int team = i / 6;
                if (rules.Teams[team].Complete || rules.Teams[team].Recovery > 0 || pawn.State != PawnState.Active) continue;
                // Check the actual plate collider, counting each pawn only once.
                var origin = pawn.Hips.position;
                if (pawn.Grounded && pads[team].Raycast(new Ray(origin, Vector3.down), out var hit, pawn.standHeight + .35f)) riders[team]++;
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
                        if (recovered) Assign(i, roles[i]);
                        else if (roles[i] == 2) PlaceAtPanel(i);
            }
        }
        void Explode(int team)
        {
            notice = (team == 0 ? "청팀" : "홍팀") + " 폭발! 완료 단계 유지 · 현재 단계 초기화";
            for (int i = team * 6; i < team * 6 + 6; i++)
            {
                var away = players[i].Hips.position - new Vector3(TeamX(team), 0, 2);
                away.y = 0;
                players[i].TakeHit(away.normalized * blastSpeed + Vector3.up * blastSpeed, stunSeconds, 0, true);
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
                clocks[team].text = (team == 0 ? "BLUE" : "RED") + $"  {state.Remaining:00.0}\n{status}";
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
        void BuildArena()
        {
            var ivory = Mat(new Color(.83f, .76f, .60f)); var dark = Mat(new Color(.12f, .10f, .09f));
            var gold = Mat(new Color(.9f, .65f, .15f));
            teamMaterials[0] = Mat(new Color(.2f, .55f, .95f)); teamMaterials[1] = Mat(new Color(.95f, .3f, .25f));
            for (int x = 0; x < 12; x++) for (int z = 0; z < 12; z++)
                Box("Chess floor", new Vector3((x - 5.5f) * 2, -.25f, (z - 5.5f) * 2), new Vector3(2, .5f, 2), (x + z) % 2 == 0 ? ivory : dark);
            Box("Shared giant chess clock", new Vector3(0, 1.5f, 3), new Vector3(18, 3, 3), dark);
            for (int team = 0; team < 2; team++)
            {
                float x = TeamX(team); var color = teamMaterials[team];
                var pad = Box("Team switch standing plate", new Vector3(x, 3.2f, 2), new Vector3(4.8f, .5f, 3.6f), color);
                pads[team] = pad.GetComponent<BoxCollider>();
                var body = pad.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate; plateBodies[team] = body;
                ImportedMovingSurface.BindDynamic(body, point => body.GetPointVelocity(point), () => Quaternion.identity, () => started);
                Box("Switch rim", new Vector3(x, 3.05f, 3.9f), new Vector3(4.8f, .15f, .15f), gold, false);
                for (int step = 0; step < 12; step++)
                    Box("Access step", new Vector3(x + (team == 0 ? -3.5f : 3.5f), (step + 1) * .14f, -5.5f + step * .6f),
                        new Vector3(1.8f, (step + 1) * .28f, .65f), ivory);
                Box("Top access landing", new Vector3(x + (team == 0 ? -2.8f : 2.8f), 3.2f, 1.7f), new Vector3(1.4f, .5f, 2), ivory);
                clocks[team] = Label("Clock display", new Vector3(x, 2, 1.46f), "45.0", .5f);
                for (int stage = 0; stage < 3; stage++)
                {
                    Box("Defuse console " + stage, Panel(team, stage), new Vector3(1.2f, 1.4f, .65f), color);
                    panelLabels[team, stage] = Label("Console instruction", Panel(team, stage) + new Vector3(0, 1.2f, -.4f), "", .18f);
                }
                // A wall across the full arena makes the team's gate a physical exit.
                Box("Exit wall left", new Vector3(x - 4f, 2, 8), new Vector3(4, 4, .5f), dark);
                Box("Exit wall right", new Vector3(x + 4f, 2, 8), new Vector3(4, 4, .5f), dark);
                gates[team] = Box("Team exit gate", new Vector3(x, 2, 8), new Vector3(4, 4, .5f), color);
                Label("Exit label", new Vector3(x, 4.5f, 7.7f), "NEXT STAGE", .25f);
            }
            Box("Team divider", new Vector3(0, 2, 0), new Vector3(.25f, 4, 24), dark);
            Box("Left boundary", new Vector3(-11, 2, 0), new Vector3(.5f, 4, 24), dark);
            Box("Right boundary", new Vector3(11, 2, 0), new Vector3(.5f, 4, 24), dark);
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
                    $"{(team == 0 ? "청팀" : "홍팀")} {state.Remaining:0.0}초 | 발판 {riders[team]}명 · 해체 {workers[team]}명 | {state.Stage}/3단계 · {state.Work / rules.WorkSeconds:P0} | " +
                    (state.Complete ? $"문 열림 · 통과 {escaped}/6" : state.Recovery > 0 ? $"재정비 {state.Recovery:0.0}초" : rules.PressedTeam == team ? "시간 정지" : "시간 감소"), text);
            }
            GUI.Label(new Rect(24, 138, 750, 40), $"선택: {players[selected].DisplayName} · {(roles[selected] == 1 ? "발판" : roles[selected] == 2 ? "자동 해체" : "직접 조작 / 대기")}\n{notice}", text);
            GUI.Label(new Rect(24, 183, 750, 74), "WASD 이동 · Space 점프 · F 길게 해체 · Tab 다음 팀원 · T 팀 전환\n시험 배치: 1 발판 / 2 자동 해체 / 3 대기 · R 복귀 · F3 전체 초기화\nEnter 시작 · Esc 마우스 해제 (진행은 계속됨) · 이동하면 자동 역할 해제", text);
            if (menu)
            {
                GUI.Box(new Rect(12, 275, 780, 170), GUIContent.none);
                GUI.Label(new Rect(24, 282, 745, 65), "동수면 양쪽 시간이 감소합니다. 발판 다수 팀은 시간이 멈춥니다.\n아래 PAWN → KNIGHT → ROOK 장치에서 해체하세요. 폭발 시 현재 단계만 초기화됩니다.\n시험 배치 키는 팀원을 순간 이동시킵니다. 자동 해체 팀원은 단계가 끝나면 다음 장치로 이동합니다.", text);
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
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
