using System;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class ChessTimerMinigame
    {
        public bool Networked { get; private set; }
        public bool Authority { get; private set; } = true;
        public bool NetworkReady { get; set; }
        bool networkPanelOpen;
        public bool NetworkPanelOpen { get => networkPanelOpen; set { if (networkPanelOpen == value) return; networkPanelOpen = value; if (initialized) RefreshCursor(); } }
        public bool NetworkInitialized => players.Count == 12;
        public Action LeaveNetwork;
        public readonly ulong[] NetworkIds = new ulong[12];
        readonly bool[] networkBots = new bool[12], heldCut = new bool[12];
        readonly uint[] blastSerial = new uint[12];
        readonly Vector3[] blastPositions = new Vector3[12];
        uint networkRound;
        bool receivedState;
        float remoteRocker;
        public bool NetworkHasState => receivedState;
        bool Present(int i) => !Networked || NetworkIds[i] != 0;
        bool CanDriveBot(int i) => !Networked ? i != selected : Present(i) && networkBots[i];
        public RagdollPawn NetworkPawn(int i) => players[i];
        public int NetworkSlot(ulong id) => id == 0 ? -1 : Array.IndexOf(NetworkIds, id);
        public void BeginNetwork(bool authority)
        {
            Networked = true; Authority = authority; NetworkReady = false; receivedState = authority;
            menu = false; NetworkPanelOpen = false; networkRound = 1;
            timeLimit = Mathf.Clamp(timeLimit, 1, 3600); switchDelay = Mathf.Clamp(switchDelay, 0, 60);
            soloRequiredRiders = Mathf.Clamp(soloRequiredRiders, 1, 12);
            cutSeconds = Mathf.Clamp(cutSeconds, .1f, 120); wrongWireLock = Mathf.Clamp(wrongWireLock, 0, 120);
            Array.Clear(NetworkIds, 0, 12); Array.Clear(heldCut, 0, 12); Array.Clear(blastSerial, 0, 12);
            ResetRound();
            for (int i = 0; i < 12; i++) { players[i].SetNetworkPuppet(!authority); players[i].gameObject.SetActive(false); }
            if (!authority) foreach (var root in boardRoots) root.SetActive(false);
            notice = "멀티플레이 준비 중"; RefreshCursor();
        }
        public void EndNetwork()
        {
            Networked = false; Authority = true; NetworkReady = false; receivedState = false;
            for (int i = 0; i < 12; i++) { players[i].gameObject.SetActive(true); players[i].SetNetworkPuppet(false); }
            Array.Clear(NetworkIds, 0, 12); Array.Clear(heldCut, 0, 12);
            ResetRound(); Select(0); RefreshCursor();
        }
        public void SetNetworkSlot(int slot, ulong id, bool bot, string displayName, bool local)
        {
            if (slot < 0 || slot >= 12) return;
            var pawn = players[slot];
            if (NetworkIds[slot] != id)
            {
                pawn.handL.Release(); pawn.handR.Release();
                foreach (var other in players)
                {
                    if (Holds(other.handL, pawn)) other.handL.Release();
                    if (Holds(other.handR, pawn)) other.handR.Release();
                }
                NetworkIds[slot] = id; networkBots[slot] = bot; heldCut[slot] = false;
                respawnAt[slot] = 0; exited[slot] = false; roles[slot] = 0; LoseCutter(slot);
                pawn.gameObject.SetActive(id != 0);
                if (id != 0)
                {
                    pawn.SetNetworkPuppet(!Authority); Place(slot, Waiting(slot));
                    if (Authority && bot && (slot % 6 == 1 || slot % 6 == 2)) Assign(slot, 1);
                }
            }
            if (id != 0) { pawn.DisplayName = displayName; if (local && selected != slot) Select(slot); }
        }
        public void ChangeNetworkAuthority(bool authority)
        {
            Authority = authority; if (authority) rockerAngle = remoteRocker; occupancy.ClearContacts(); Array.Clear(heldCut, 0, 12);
            for (int i = 0; i < 12; i++)
            {
                players[i].SetInput(default); players[i].SetNetworkPuppet(!authority);
                // Respawn countdown survives migration even though local ragdoll recovery restarts.
                if (authority && Present(i) && respawnAt[i] > Time.time)
                    players[i].TakeHit(Vector3.zero, respawnAt[i] - Time.time, 0, true);
            }
            notice = authority ? "새 방장에서 타이머와 퍼즐을 이어갑니다" : "방장 변경 · 상태 동기화 중";
        }
        public PawnInput ReadNetworkInput()
        {
            if (!NetworkReady || !Present(selected) || menu || NetworkPanelOpen || ChatBox.KeysHeld || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked) return default;
            var move = GameSettings.Move;
            return new PawnInput { move = Vector3.ClampMagnitude(cameraRig.FlatRight * move.x + cameraRig.FlatForward * move.y, 1), aim = cameraRig.AimForward,
                jump = GameSettings.Pressed(GameKey.Jump), sprint = GameSettings.Held(GameKey.Sprint), grab = Input.GetMouseButton(1),
                shove = Input.GetMouseButtonDown(0), shoveHeld = Input.GetMouseButton(0), interact = Input.GetKey(KeyCode.F) };
        }
        public void ApplyNetworkInput(ulong id, PawnInput input)
        {
            int i = NetworkSlot(id); if (!Authority || i < 0 || networkBots[i]) return;
            if (!NetworkReady || respawnAt[i] > 0) input = default;
            // Only movement, grab, shove and interaction are accepted. No client chooses a wire or sends progress.
            input.ability = input.ability2 = false;
            heldCut[i] = input.interact; input.interact = false; players[i].SetInput(input);
            if (heldCut[i]) TryEquipCutter(i);
        }
        int NetworkCutWire(int i)
        {
            int team = i / 6;
            if (!Present(i) || !heldCut[i] || !hasCutter[i] || respawnAt[i] > 0 || rules.Teams[team].Finished ||
                players[i].State != PawnState.Active || !players[i].Grounded) return -1;
            for (int n = 0; n < puzzles[team].Board.WireCount; n++)
                if (!puzzles[team].Cut(n) && Near(players[i].Hips.position, WirePoint(team, n))) return n;
            return -1;
        }
        void NetworkUpdate()
        {
            if (Application.isFocused && !NetworkPanelOpen && !ChatBox.KeysHeld)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { menu = !menu; RefreshCursor(); }
                if (Input.GetKeyDown(KeyCode.H) && receivedState) attackHints[selected / 6].SetActive(!attackHints[selected / 6].activeSelf);
            }
            RefreshCursor();
            if (Authority)
            {
                if (NetworkReady) started = true;
                for (int i = 0; i < 12; i++)
                {
                    if (!Present(i)) continue;
                    if (respawnAt[i] > 0 && Time.time >= respawnAt[i]) { respawnAt[i] = 0; roles[i] = 0; Place(i, Waiting(i)); }
                    if (!players[i].IsFinite() || respawnAt[i] <= 0 && players[i].Hips.position.y < -5) { roles[i] = 0; Place(i, Waiting(i)); }
                    if (CanDriveBot(i)) players[i].SetInput(default);
                    if (rules.Teams[i / 6].GateOpen && players[i].Hips.position.z > ExitZ + 1 && Mathf.Abs(players[i].Hips.position.x - BoardCentreX(i / 6)) < 3) exited[i] = true;
                }
                if (NetworkReady) { DriveSparringBots(); DriveCuttingBots(); }
            }
            else if (receivedState)
            {
                rockerAngle = Mathf.Lerp(rockerAngle, remoteRocker, 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
                rocker.transform.rotation = Quaternion.AngleAxis(rockerAngle, Vector3.forward);
                for (int side = 0; side < 2; side++) PositionEntryFlap(side, rocker.transform.rotation, true);
            }
            if (receivedState) UpdateViews();
        }
        public ChessTimerState CaptureNetworkState(uint tick)
        {
            var s = new ChessTimerState { Tick = tick, Round = networkRound, Started = started, Elapsed = elapsed, Rocker = rockerAngle, Rules = rules };
            for (int t = 0; t < 2; t++) { s.Courses[t] = courses[t]; s.Riders[t] = riders[t]; s.Workers[t] = workers[t]; s.SolveTimes[t] = solveTimes[t]; }
            for (int i = 0; i < 12; i++) s.Players[i] = new ChessTimerPlayerState { Id = NetworkIds[i], Cutter = hasCutter[i], Exited = exited[i], Role = roles[i],
                Respawn = Mathf.Max(0, respawnAt[i] - Time.time), Blast = blastSerial[i], BlastX = blastPositions[i].x, BlastY = blastPositions[i].y, BlastZ = blastPositions[i].z };
            return s;
        }
        public void ApplyNetworkState(ChessTimerState s)
        {
            if (Authority || !Networked) return;
            bool initial = !receivedState || networkRound != s.Round;
            rules = s.Rules; started = s.Started; elapsed = s.Elapsed; remoteRocker = s.Rocker;
            for (int t = 0; t < 2; t++)
            {
                bool rebuild = initial || courses[t].Stage != s.Courses[t].Stage;
                courses[t] = s.Courses[t]; puzzles[t] = courses[t].Current;
                riders[t] = s.Riders[t]; workers[t] = s.Workers[t]; solveTimes[t] = s.SolveTimes[t];
                if (rebuild) RebuildBoard(t);
                gates[t].SetActive(!rules.Teams[t].GateOpen);
                for (int w = 0; w < puzzles[t].Board.WireCount; w++) wireCentres[t, w].SetActive(!puzzles[t].Cut(w));
            }
            for (int i = 0; i < 12; i++)
            {
                var p = s.Players[i]; if (NetworkIds[i] != p.Id || p.Id == 0) continue;
                if (hasCutter[i] != p.Cutter)
                {
                    LoseCutter(i);
                    if (p.Cutter) { hasCutter[i] = true; carriedCutters[i] = MakeCutter("Equipped wire cutter", players[i].handR.transform); carriedCutters[i].transform.localPosition = new Vector3(0, 0, .2f); }
                }
                if (!initial && p.Blast != blastSerial[i]) StartCoroutine(BlastFlash(new Vector3(p.BlastX, p.BlastY, p.BlastZ)));
                blastSerial[i] = p.Blast; blastPositions[i] = new Vector3(p.BlastX, p.BlastY, p.BlastZ);
                respawnAt[i] = p.Respawn > 0 ? Time.time + p.Respawn : 0; exited[i] = p.Exited; roles[i] = p.Role;
            }
            networkRound = s.Round; receivedState = true;
            notice = rules.Winner >= 0 ? (rules.Winner == 0 ? "백팀" : "흑팀") + " 해체 승리" : rules.Winner == -1 ? "동률 · 양 팀 폭발" : "멀티플레이 · 발판 인원과 절단 진행 동기화";
        }
        void DrawNetworkHud()
        {
            if (text == null) text = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 17, wordWrap = true };
            GUI.Box(new Rect(12, 12, 760, 190), "");
            GUI.Label(new Rect(24, 20, 735, 32), "체스타이머 멀티플레이 · " + (Authority ? "방장" : "참가자") + (NetworkReady ? "" : " · 동시 출발 대기"), text);
            for (int t = 0; t < 2; t++)
                GUI.Label(new Rect(24, 55 + t * 32, 735, 32), $"{(t == 0 ? "백팀" : "흑팀")} {rules.Teams[t].Remaining:0.0}초 · 발판 {riders[t]}명 · {courses[t].Stage + 1}/3단계 · 안전 전선 {puzzles[t].CorrectCuts}/{puzzles[t].Board.RequiredCuts} · {(rules.Teams[t].GateOpen ? "문 열림" : "진행 중")}", text);
            GUI.Label(new Rect(24, 120, 735, 70), notice + "\nWASD 이동 · 좌클릭 밀치기 · 우클릭 잡기 · F 도구/절단 · H 힌트 · Esc 메뉴 · F2 접속 패널", text);
            if (menu)
            {
                if (GUI.Button(new Rect(24, 215, 180, 42), "계속 조작")) { menu = false; RefreshCursor(); }
                if (GUI.Button(new Rect(215, 215, 180, 42), "방 나가기")) LeaveNetwork?.Invoke();
            }
        }
    }
}
