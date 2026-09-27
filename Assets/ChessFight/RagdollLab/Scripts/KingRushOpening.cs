using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab
{
    // First connected course. The mechanics sandbox and online routing are untouched.
    [DefaultExecutionOrder(-90)]
    public sealed partial class KingRushOpening : MonoBehaviour
    {
        public RagdollPawn pawnPrefab;
        public RagdollTuning tuning;
        public Material white, black;
        public KingRushCourseCheckpoint[] checkpoints;
        public KingRushPad[] pads;
        public PawnGate[] gates;
        public KingRushCaptureBox[] boxes;
        public GameObject[] planks;
        public WaterZone[] waters;
        public TextMesh[] boxLabels, gateLabels, padLabels;
        public readonly List<KingRushPawn> Players = new List<KingRushPawn>();
        public KingRushMatch Match { get; private set; }
        public KingRushCaptureRules Mission { get; private set; }
        public LabCamera CameraRig { get; private set; }
        public KingRushPawn Local => Players[Selected];
        public int Selected { get; private set; }
        public bool Automated { get; private set; }
        public bool ManualSteps { get; set; }
        public readonly Bounds Arena = new Bounds(new Vector3(0, 3, 182), new Vector3(30, 10, 24));
        readonly Dictionary<KingRushPawn, int> progress = new Dictionary<KingRushPawn, int>();
        readonly Dictionary<KingRushPawn, double> wetUntil = new Dictionary<KingRushPawn, double>();
        readonly List<KingRushPawn> pendingCapture = new List<KingRushPawn>();
        KingRushOpeningHud hud;
        Material courseText;
        Vector3 oldGravity;
        bool oldBackground, oldVisible, menu;
        CursorLockMode oldLock;
        public int CheckpointOf(KingRushPawn pawn) => progress.TryGetValue(pawn, out int value) ? value : 0;
        public static Vector3 Spawn(int index) => new Vector3((index < 6 ? -1 : 1) * (2 + index % 3 * 2), 0, 2 - index % 6 / 3 * 2);
        public static Vector3 Entry(int index) => new Vector3((index < 6 ? -1 : 1) * (2 + index % 3 * 2), 0, 173 + index % 6 / 3 * 2);
        public static Vector3 Shelf(int index) => new Vector3(-16.5f + index * 3, 7, 166);
        void Awake()
        {
            Automated = Array.IndexOf(Environment.GetCommandLineArgs(), "-kingRushOpeningTest") >= 0;
            oldGravity = Physics.gravity; Physics.gravity = Vector3.down * 9.81f * tuning.values.gravityScale;
            oldBackground = Application.runInBackground; Application.runInBackground = true;
            oldLock = Cursor.lockState; oldVisible = Cursor.visible;
            Match = gameObject.AddComponent<KingRushMatch>(); Mission = new KingRushCaptureRules(Match.Rules);
            Seesaw = new KingRushSeesawRules(Match.Rules);
            Final = new KingRushFinalRules();
            CameraRig = FindFirstObjectByType<LabCamera>();
            // OS-created font atlases are runtime objects, not serializable prefab assets.
            // World signs must depth-test; the default GUI font shader shows through boards/walls.
            courseText = new Material(Resources.Load<Shader>("KingRushCourseText"));
            Font.textureRebuilt += RefreshFont;
            foreach (var label in GetComponentsInChildren<TextMesh>())
            { label.font = RuntimePanels.KoreanFont; label.GetComponent<Renderer>().sharedMaterial = courseText; }
            RefreshFont(RuntimePanels.KoreanFont);
            foreach (var pad in pads) pad.match = Match;
            foreach (var gate in gates) gate.match = Match;
            WaterZone.Entered += EnterWater;
        }
        void Start()
        {
            for (int i = 0; i < 12; i++)
            {
                var pawn = Instantiate(pawnPrefab, Spawn(i) + Vector3.up * .02f, Quaternion.identity);
                pawn.tuning = tuning; pawn.Team = i / 6; pawn.DisplayName = (i < 6 ? "백팀 " : "흑팀 ") + (i % 6 + 1);
                pawn.skin.sharedMaterial = i < 6 ? white : black;
                foreach (var body in pawn.bodies) { body.solverIterations = 24; body.solverVelocityIterations = 6; }
                var member = pawn.gameObject.AddComponent<KingRushPawn>(); member.Initialize(Match, (ulong)i + 1, i % 6 == 5);
                Players.Add(member); progress[member] = 0;
            }
            Select(0); hud = gameObject.AddComponent<KingRushOpeningHud>(); RefreshCursor();
            if (Automated) gameObject.AddComponent<KingRushOpeningTest>();
        }
        public void Select(int index)
        {
            Players[Selected].SetInput(default); Selected = index % Players.Count;
            CameraRig.soloTarget = Local.Pawn;
        }
        void FixedUpdate() { if (!ManualSteps) Step(Time.fixedDeltaTime); }
        public void Step(float dt)
        {
            Match.Advance(dt);
            foreach (var p in Players)
            {
                if (p.Captured || Mission.Detained(p.Id)) { p.AbilitiesEnabled = false; continue; }
                bool inside = Arena.Contains(p.BodyPosition) && !p.Pawn.Floating;
                if (inside && p.Section == KingRushSection.Red1 && rallies[0].Rules.Released) p.Section = KingRushSection.Blue1;
                if (p.Section != KingRushSection.Blue2 && p.Section != KingRushSection.Final) p.AbilitiesEnabled = inside && p.Section == KingRushSection.Blue1;
                if (p.Section == KingRushSection.Blue1 && p.AbilitiesEnabled) Mission.Begin(Match.Now);
                if (!KingRushPieces.IsBlue(p.Section) && !p.Pawn.Floating)
                    for (int i = progress[p] + 1; i < checkpoints.Length; i++)
                        if (CheckpointWave(i) == RallyWave(p.Section) && checkpoints[i].Contains(p.BodyPosition)) progress[p] = i;
            }
            Mission.Advance(Match.Now);
            foreach (var p in Players)
                if (p.Section == KingRushSection.Blue1 && !p.Captured && !p.Pawn.Floating)
                    foreach (var box in boxes)
                        if (box.Contains(p.BodyPosition) && Mission.Deposit(p.Id, p.Team, box.team, Match.Now))
                        { p.SetCaptured(); pendingCapture.Add(p); break; }
            foreach (var pad in pads) pad.Step(dt);
            foreach (var rally in rallies) rally.Sample(Match.Rules, Match.Now);
            StepCastle(dt);
            StepFinal(dt);
            for (int team = 0; team < 2; team++)
                for (int i = 0; i < 6; i++)
                {
                    bool visible = i < Mission.VisiblePlanks(team, Match.Now);
                    if (planks[team * 6 + i].activeSelf != visible) planks[team * 6 + i].SetActive(visible);
                }
            foreach (var gate in gates) gate.Step();
            foreach (var p in Players) p.Step(dt);
        }
        // No teleport or joint destruction takes place in a physics callback.
        void Update()
        {
            GatherReadyWaves();
            UpdateFinal();
            foreach (var p in pendingCapture)
            {
                foreach (var holder in Players) holder.ReleaseHoldOn(p);
                wetUntil.Remove(p); p.Respawn(Shelf((int)p.Id - 1)); p.SetCaptured();
            }
            pendingCapture.Clear();
            foreach (var p in Players)
            {
                if (p.Section == KingRushSection.Final) continue;
                if (Mission.Detained(p.Id))
                {
                    if (Match.Now >= Mission.ReleaseAt(p.Id)) { Mission.Release(p.Id); Respawn(p); }
                    else if (p.BodyPosition.y < 5) { p.Respawn(Shelf((int)p.Id - 1)); p.SetCaptured(); }
                    continue;
                }
                if ((wetUntil.TryGetValue(p, out double at) && Match.Now >= at) || p.BodyPosition.y < -16 || !p.Pawn.IsFinite()) Respawn(p);
            }
            if (Automated || Players.Count == 0) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { menu = !menu; RefreshCursor(); }
            if (menu) { Local.SetInput(default); if (Input.GetKeyDown(KeyCode.Backspace)) SceneManager.LoadScene("Lobby"); return; }
            if (Input.GetKeyDown(KeyCode.Tab)) Select(Selected + 1);
            if (Input.GetKeyDown(KeyCode.F3)) ResetRound();
            if (Input.GetKeyDown(KeyCode.F4)) ArrangeMission();
            if (Input.GetKeyDown(KeyCode.F5)) ArrangeCastle(Input.GetKey(KeyCode.LeftShift));
            if (Input.GetKeyDown(KeyCode.F7)) ArrangeRally(RallyWave(Local.Section));
            if (Input.GetKeyDown(KeyCode.F8)) ArrangeFinal(Input.GetKey(KeyCode.LeftShift));
            if (Input.GetKeyDown(KeyCode.F2)) Select(Input.GetKey(KeyCode.LeftShift) ? 11 : 5);
            if (Input.GetKeyDown(KeyCode.F6)) NextCheckpoint();
            if (Input.GetKeyDown(KeyCode.R)) Respawn(Local);
            if (Final.Ended || eliminated.Contains(Local) || finalFalls.ContainsKey(Local)) { Local.SetInput(default); return; }
            if (!Application.isFocused) { Local.SetInput(default); return; }
            float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            Local.SetInput(new PawnInput { move = Vector3.ClampMagnitude(CameraRig.FlatRight * x + CameraRig.FlatForward * z, 1),
                aim = CameraRig.AimForward, jump = Input.GetKeyDown(KeyCode.Space), sprint = Input.GetKey(KeyCode.LeftShift),
                shove = Input.GetMouseButtonDown(0), shoveHeld = Input.GetMouseButton(0), grab = Input.GetMouseButton(1),
                ability = Input.GetKeyDown(KeyCode.E), interact = Input.GetKey(KeyCode.F) }, Input.GetKey(KeyCode.E));
        }
        void EnterWater(ICharacterDriver driver, WaterZone water)
        {
            if (Array.IndexOf(waters, water) < 0) return;
            foreach (var p in Players)
                if (ReferenceEquals(p.HitReceiver, driver) && !wetUntil.ContainsKey(p) && !Mission.Detained(p.Id))
                { wetUntil[p] = Match.Now + water.RespawnDelay; p.CancelAbility(); break; }
        }
        public void Respawn(KingRushPawn p)
        {
            if (Final.Ended || eliminated.Contains(p) || finalFalls.ContainsKey(p)) return;
            if (Mission.Detained(p.Id)) return; // R cannot skip the capture penalty.
            if (p.Section == KingRushSection.Final)
            {
                foreach (var holder in Players) holder.ReleaseHoldOn(p);
                wetUntil.Remove(p); finalFalls[p] = Match.Now + 5; p.SetCaptured(); return;
            }
            wetUntil.Remove(p);
            int index = (int)p.Id - 1;
            Vector3 spot = p.Section == KingRushSection.Final ? FinalEntry(index) : p.Section == KingRushSection.Blue2 ? SeesawEntry(index) : p.Section == KingRushSection.Red3 && CheckpointOf(p) < 12 ?
                new Vector3(p.Team == 0 ? -12 : 12, 3, 436) : p.Section == KingRushSection.Blue1 ? Entry(index) :
                p.Section == KingRushSection.Red2 && CheckpointOf(p) < 7 ? new Vector3(p.Team == 0 ? -5 : 5, 0, 214) : CheckpointOf(p) == 0 ? Spawn(index) :
                checkpoints[CheckpointOf(p)].transform.position + Vector3.right * ((index % 6 - 2.5f) * .9f);
            p.Respawn(spot + Vector3.up * .02f);
        }
        public void ResetRound()
        {
            pendingCapture.Clear(); wetUntil.Clear(); Match.ResetRound(); Mission = new KingRushCaptureRules(Match.Rules);
            Seesaw = new KingRushSeesawRules(Match.Rules); seesawBoard.ResetBoard();
            ResetFinal();
            foreach (var rally in rallies) rally.ResetGate();
            foreach (var pad in pads) pad.Charge.Reset();
            foreach (var p in Players) { progress[p] = 0; p.ResetForRound(); Respawn(p); }
            foreach (var plank in planks) plank.SetActive(false);
            foreach (var gate in gates) gate.Step();
            CameraRig.yaw = 0; CameraRig.pitch = 18;
        }
        public void ArrangeMission()
        {
            ResetRound(); Select(0);
            rallies[0].OpenAfterGather(Match.Now, true);
            // Reposition real, stationary bodies; this shortcut awards no score.
            for (int i = 0; i < 12; i++)
            {
                var p = Players[i]; p.Section = KingRushSection.Blue1; progress[p] = 6;
                p.Respawn(i < 6 ? Entry(i) : new Vector3(-11 + (i - 6) % 3 * 2.3f, 0, 184 - (i - 6) / 3 * 2.5f));
            }
            Local.Respawn(new Vector3(-10, 0, 180));
        }
        public void NextCheckpoint()
        {
            if (Mission.Detained(Local.Id)) return;
            if (Local.Section == KingRushSection.Red1) progress[Local] = Mathf.Min(progress[Local] + 1, 6);
            else if (Local.Section == KingRushSection.Red2) progress[Local] = Mathf.Clamp(progress[Local] + 1, 7, 11);
            else if (Local.Section == KingRushSection.Red3) progress[Local] = Mathf.Clamp(progress[Local] + 1, 12, 15);
            else return;
            Respawn(Local);
        }
        void RefreshCursor()
        { Cursor.lockState = menu || Automated ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = menu || Automated; }
        void RefreshFont(Font font)
        { if (font == RuntimePanels.KoreanFont && courseText != null) courseText.mainTexture = font.material.mainTexture; }
        public string DoorText(int team)
        {
            double left = Match.Rules.OpensAt(0, team) - Match.Now;
            return double.IsInfinity(left) ? "다리 만드는 중" : left <= 0 ? "출구 열림" : $"{left:0}초 뒤 열림";
        }
        void LateUpdate()
        {
            if (Players.Count == 0) return;
            DrawFinalCrown();
            for (int i = 0; i < 2; i++)
            {
                string team = i == 0 ? "백팀" : "흑팀";
                boxLabels[i].text = $"{team} 상자\n상대 말을 넣으세요\n{Mission.Planks(i)} / 6";
                gateLabels[i].text = team + " 전용\n" + DoorText(i);
                gates[i].barrier.GetComponent<Renderer>().enabled = !(gates[i].Open && i == Local.Team);
            }
            for (int i = 0; i < pads.Length; i++)
                padLabels[i].text = KingRushPieces.Name(pads[i].Reward) + "\n" + (pads[i].Claimed ? "사용 완료" : pads[i].Charge.Contested ? "쟁탈 중" : $"혼자 1.5초 · {pads[i].Charge.Seconds / 1.5:0%}");
            string objective = Final.Ended ? "체크메이트 · 경기 종료" : Local.Section == KingRushSection.Final ? "우리 킹이 왕좌 점유 + 단상의 적을 밀어내기 → 누적 8초" :
                Local.Section == KingRushSection.Red3 ? "왕의 계단 · 외나무/긴 계단 → 붕괴 칸 → 거대한 손 → 마지막 승격" :
                Local.Section == KingRushSection.Blue2 ? "반대쪽을 무겁게 → 우리 쪽 높은 출구로 4명 통과" :
                Local.Section == KingRushSection.Red2 ? "성벽 갈림길 · 위/아래 길 → 교차 다리 → 승격 발판" :
                Local.Section == KingRushSection.Blue1 ? "상대를 우리 팀 상자에 넣어 다리 6칸 완성 → 팀 출구" :
                $"장난감 상자 · {checkpoints[CheckpointOf(Local)].title} → 정면 코스를 따라 승격 발판으로";
            string ability = !Local.AbilitiesEnabled ? KingRushPieces.IsBlue(Local.Section) ? "미션 구역 밖 · 능력 잠김" : "빨강 구간 · 능력 잠김" : Local.Piece == KingRushPiece.King ? "E 체크! · 아군도 밀려남" :
                Local.Piece == KingRushPiece.Rook ? "잡고 E · 인간 대포" : "태클·잡기로 미션 진행 · 이 기물 E 능력은 제작 전";
            double elapsed = Mission.Started ? Match.Now - Mission.StartedAt : 0;
            string timer = !Mission.Started ? "첫 입장부터 미션 시계 시작" : elapsed < 120 ? $"미션 {elapsed:0}초 · 120초 이후 10초마다 자동 다리" :
                $"자동 다리 진행 · 다음 칸까지 {10 - (elapsed - 120) % 10:0}초";
            string detention = Mission.Detained(Local.Id) ? $"상자에 잡힘 · 선반에서 {Math.Max(0, Mission.ReleaseAt(Local.Id) - Match.Now):0.0}초 대기" : "";
            if (Final.Ended) detention = "경기 종료 · F3 다시 시작";
            else if (eliminated.Contains(Local)) detention = "초읽기 탈락 · 재진입 불가";
            else if (finalFalls.TryGetValue(Local, out double due)) detention = $"추락 · 탑 재진입까지 {Math.Max(0, due - Match.Now):0.0}초";
            hud.Draw((Local.Team == 0 ? "백팀" : "흑팀") + " · " + KingRushPieces.Name(Local.Piece), objective, ability, detention,
                MissionText($"백팀 {Mission.Planks(0)}/6 · {DoorText(0)}\n흑팀 {Mission.Planks(1)}/6 · {DoorText(1)}\n{timer}"), menu);
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= RefreshFont; if (courseText != null) Destroy(courseText);
            WaterZone.Entered -= EnterWater; Physics.gravity = oldGravity; Application.runInBackground = oldBackground;
            Cursor.lockState = oldLock; Cursor.visible = oldVisible;
            foreach (var p in Players) if (p != null) Destroy(p.gameObject);
        }
    }
}
