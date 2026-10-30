using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab
{
    [DefaultExecutionOrder(-90)]
    public sealed partial class SwordFightGame : MonoBehaviour
    {
        public RagdollPawn pawnPrefab;
        public RagdollTuning tuning;
        public int targetScore = 20;
        public float roundSeconds = 240, respawnDelay = 3;
        public bool Networked { get; set; }
        public bool Authority { get; set; } = true;
        public bool Automated { get; set; }
        public ulong LocalId { get; set; } = 1;
        public Action LeaveMatch;
        public readonly Dictionary<ulong, SwordFightPawn> Fighters = new Dictionary<ulong, SwordFightPawn>();
        public SwordFightRules Rules { get; private set; }
        public LabCamera CameraRig { get; private set; }
        public int White => Match != null ? Match.White : Authority ? Rules.White : remoteWhite;
        public int Black => Match != null ? Match.Black : Authority ? Rules.Black : remoteBlack;
        public float Remaining => Match != null ? Match.Remaining : Authority ? Mathf.Max(0, roundSeconds - (float)Rules.Elapsed) : remoteRemaining;
        public bool Finished => Match != null ? Match.Finished : Authority ? Rules.Finished : remoteFinished;
        /// <summary>R149: the prototype match (spec v1.0) runs this scene: the match scene from the lobby, or played alone
        /// in the editor. Null in the skill test scene (its bed) and the automated sword checks.</summary>
        public SwordFightMatch Match { get; private set; }
        /// <summary>R149: the countdown and the result hold every fighter still (SwordFightPawn.SetInput, the skills).</summary>
        public static bool InputLocked;
        /// <summary>R149: a player asks for a piece (online: LabNetLink hands a client's ask to this on the host).</summary>
        public Func<ulong, PieceKind, bool> PieceRequest;
        /// <summary>This PC's keys go to its fighter: no menu, no chat, the window in front with the mouse taken.</summary>
        public bool KeysFree => !MenuOpen && !Finished && !ChatBox.KeysHeld && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        public SwordFightPawn Local => Fighters.TryGetValue(LocalId, out var f) ? f : null;
        public bool MenuOpen { get; private set; }
        public bool ClassicControls { get; private set; }
        public int Revision { get; private set; }
        /// <summary>The skill test's online panel is open (LabNetLink, R121): the mouse is free for its buttons.</summary>
        public bool UiWantsCursor { get; set; }
        bool cursorWanted;
        Material white, black;
        SwordFightHud hud;
        int remoteWhite, remoteBlack;
        float remoteRemaining;
        bool remoteFinished;
        Vector3 savedGravity;
        CursorLockMode savedLock;
        bool savedCursor;
        bool waitForAttackRelease;
        bool menuOnline;

        void Awake()
        {
            Rules = new SwordFightRules(targetScore, roundSeconds, respawnDelay);
            savedGravity = Physics.gravity;
            if (tuning != null) Physics.gravity = Vector3.down * 9.81f * tuning.values.gravityScale;
            savedLock = Cursor.lockState; savedCursor = Cursor.visible;
            white = new Material(pawnPrefab.skin.sharedMaterial) { color = new Color(.96f, .93f, .84f) };
            black = new Material(pawnPrefab.skin.sharedMaterial) { color = new Color(.15f, .18f, .24f) };
            CameraRig = FindFirstObjectByType<LabCamera>();
            Automated = Array.IndexOf(Environment.GetCommandLineArgs(), "-swordFightTest") >= 0;
            if (Automated) Application.runInBackground = true;
            InputLocked = false;
            // R149: the match scene plays the prototype match (the skill test scene has its bed instead).
            if (!Automated && FindFirstObjectByType<SwordFightSkillBed>() == null)
            {
                Match = gameObject.AddComponent<SwordFightMatch>();
                ClassicControls = true;   // spec §6: the click cut only (the drag stays in the code, switched off)
            }
        }
        void Start()
        {
            hud = gameObject.AddComponent<SwordFightHud>();
            if (Match != null) hud.SetVisible(false);   // the match draws its own HUD
            hud.Resume += () => SetMenu(false);
            hud.Leave += Exit;
            hud.SwitchControls += ToggleControls;
            // R148: Esc is the shared in-game menu (back, settings, leave with a confirmation).
            menuOnline = Networked || LeaveMatch != null;
            GameMenu.Use(gameObject, Exit, menuOnline);
            if (!Networked && !Automated) SpawnOffline();
            RefreshCursor();
            if (Automated) gameObject.AddComponent<SwordFightAutoTest>();
        }

        /// <summary>The offline line-up: me and three dummies (also what an online skill test goes back to, R121).</summary>
        public void SpawnOffline()
        {
            Add(1, 0, 0, false, "나 · 폰");
            Add(2, 0, 1, true, "아군 더미");
            Add(3, 1, 0, true, "적군 더미 1");
            Add(4, 1, 1, true, "적군 더미 2");
        }

        /// <summary>Every fighter gone (an online skill test starting or ending, R121).</summary>
        public void ClearFighters()
        {
            foreach (var id in new List<ulong>(Fighters.Keys)) Remove(id);
        }

        /// <summary>A fresh round: no score, the clock from the start, nobody waiting to come back.</summary>
        public void ResetRound()
        {
            Rules = new SwordFightRules(targetScore, roundSeconds, respawnDelay);
            remoteWhite = remoteBlack = 0;
            remoteRemaining = roundSeconds;
            remoteFinished = false;
            Revision++;
            Match?.Restart();
        }

        /// <summary>This PC became the host mid-round (R121): the round goes on from the score and the clock the old host
        /// last sent, and whoever was out comes back when they would have.</summary>
        public void TakeOverRound()
        {
            Rules = new SwordFightRules(targetScore, roundSeconds, respawnDelay);
            Rules.Restore(remoteWhite, remoteBlack, roundSeconds - remoteRemaining);
            foreach (var f in Fighters.Values) if (f != null && !f.Alive) Rules.Wait(f.Id, f.RespawnSeconds);
            Revision++;
            Match?.TakeOver();
        }

        public static Vector3 SpawnPoint(int team, int slot) => new Vector3((slot % 3 - 1) * 1.8f, 0, (team == 0 ? -1 : 1) * (3.3f + slot / 3 * 1.4f));
        public static Vector3 Facing(int team) => team == 0 ? Vector3.forward : Vector3.back;
        public SwordFightPawn Add(ulong id, int team, int slot, bool bot, string displayName)
        {
            if (Fighters.TryGetValue(id, out var old)) return old;
            Vector3 at = Match != null ? Match.StartPoint(team, slot) : SpawnPoint(team, slot);
            var pawn = Instantiate(pawnPrefab, at + Vector3.up * .02f, Quaternion.LookRotation(Facing(team)));
            pawn.name = displayName; pawn.DisplayName = displayName; pawn.tuning = tuning; pawn.Team = team;
            pawn.skin.sharedMaterial = team == 0 ? white : black;
            foreach (var rb in pawn.bodies) { rb.solverIterations = 24; rb.solverVelocityIterations = 6; }
            var fighter = pawn.gameObject.AddComponent<SwordFightPawn>();
            fighter.Initialize(id, slot, bot); fighter.Respawn(at, Facing(team), Authority);
            Fighters.Add(id, fighter);
            Match?.Joined(fighter);
            if (id == LocalId && CameraRig != null) CameraRig.soloTarget = pawn;
            return fighter;
        }
        public void Remove(ulong id)
        {
            if (!Fighters.TryGetValue(id, out var f)) return;
            Destroy(f.gameObject); Fighters.Remove(id); Rules.Remove(id); Match?.Left(id);
        }

        void Update()
        {
            if (Automated) return;
            // The match sheet (VoiceHud, R131) frees the mouse like the HUD's own panels.
            bool wants = UiWantsCursor || GameSettings.PointerTaken;
            if (wants != cursorWanted)
            {
                cursorWanted = wants;
                waitForAttackRelease = true;
                RefreshCursor();
            }
            // R148: the shared in-game menu owns Esc (it leaves the chat's and the match sheet's Esc alone).
            if (GameMenu.IsOpen != MenuOpen) SetMenu(GameMenu.IsOpen);
            bool online = Networked || LeaveMatch != null;
            if (online != menuOnline) { menuOnline = online; GameMenu.Use(gameObject, Exit, online); }
            if (Match == null && !Finished && !ChatBox.KeysHeld && Input.GetKeyDown(KeyCode.F6)) ToggleControls();
            // 10-29 test key (승규 님): B puts my sword in the black team's look, again back to my own team's.
            if (Match == null && !ChatBox.KeysHeld && Local != null && Input.GetKeyDown(KeyCode.B)) Local.BlackSwordTest = !Local.BlackSwordTest;
            if (!Networked && !Finished && Local != null) Local.SetInput(ReadLocalInput());
            if (Finished) RefreshCursor();
        }
        public PawnInput ReadLocalInput()
        {
            if (MenuOpen || Finished || ChatBox.KeysHeld || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked)
            { waitForAttackRelease = true; return new PawnInput { ability2 = ClassicControls }; }
            if (!GameSettings.Held(GameKey.Shove)) waitForAttackRelease = false;
            float x = GameSettings.Move.x;
            float y = GameSettings.Move.y;
            Vector3 move = CameraRig.FlatRight * x + CameraRig.FlatForward * y;
            return new PawnInput { move = Vector3.ClampMagnitude(move, 1), aim = CameraRig.AimForward,
                jump = GameSettings.Pressed(GameKey.Jump), sprint = GameSettings.Sprint, ability2 = ClassicControls,
                shove = !waitForAttackRelease && GameSettings.Pressed(GameKey.Shove), shoveHeld = !waitForAttackRelease && GameSettings.Held(GameKey.Shove) };
        }
        public void ToggleControls()
        {
            if (Finished) return;
            ClassicControls = !ClassicControls; waitForAttackRelease = true;
            ResetSwordLook();
            if (Authority) Local?.SetControlStyle(ClassicControls);
        }
        void FixedUpdate()
        {
            if (!Authority || Automated || Match != null) return;   // the prototype match steps its own round
            StepRound(Time.fixedDeltaTime);
        }
        public void StepRound(float dt)
        {
            Rules.Advance(dt);
            foreach (var f in Fighters.Values)
            {
                if (Finished) { f.StopCombat(); continue; }
                if (f.Alive && (f.Pawn.Hips.position.y < -3 || !f.Pawn.IsFinite()))
                {
                    if (Rules.RingOut(f.Id, f.Pawn.Team)) { f.Eliminate(); Revision++; }
                }
                if (!f.Alive)
                {
                    f.RespawnSeconds = (float)Rules.RespawnRemaining(f.Id);
                    if (Rules.TryRespawn(f.Id)) { f.Respawn(SpawnPoint(f.Pawn.Team, f.Slot), Facing(f.Pawn.Team), true); Revision++; }
                }
                // Test dummies are physical pawns, not pursuing or attacking AI.
                // Neutral input still permits normal hits, knockdowns and respawns.
                if (f.Bot && f.Alive && !f.Scripted) f.SetInput(default);
            }
        }
        public void ApplyScore(int w, int b, float remaining, bool finished)
        { remoteWhite = w; remoteBlack = b; remoteRemaining = remaining; remoteFinished = finished; }

        public void SetMenu(bool open)
        { MenuOpen = open; waitForAttackRelease = true; if (open) ResetSwordLook(); RefreshCursor(); }
        void RefreshCursor()
        {
            bool free = MenuOpen || Finished || Automated || UiWantsCursor || GameSettings.PointerTaken;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = free;
        }
        public void Exit()
        {
            if (LeaveMatch != null) LeaveMatch();
            else SceneManager.LoadScene("Lobby");
        }
        void LateUpdate()
        {
            if (!Automated)
                StepSwordLook(!ClassicControls && Local != null && Local.Alive && Local.Pawn.State == PawnState.Active &&
                    !MenuOpen && !Finished && !waitForAttackRelease && Application.isFocused &&
                    Cursor.lockState == CursorLockMode.Locked && GameSettings.Held(GameKey.Shove),
                    new Vector2(GameSettings.LookX, -GameSettings.LookY), Time.unscaledDeltaTime);
            if (hud == null || Rules == null) return;
            var local = Local;
            // During the respawn wait, watch an alive teammate instead of staring under the floor.
            if (local != null && CameraRig != null)
            {
                var focus = local;
                if (!local.Alive)
                    foreach (var candidate in Fighters.Values)
                    {
                        if (!candidate.Alive) continue;
                        focus = candidate;
                        if (candidate.Pawn.Team == local.Pawn.Team) break;
                    }
                CameraRig.soloTarget = focus.Pawn;
            }
            if (Match != null) return;   // R149: the match draws its own HUD
            string status = local == null ? "입장 중" : !local.Alive ? $"{local.RespawnSeconds:0.0}초 뒤 부활"
                : local.Protection > 0 ? "부활 보호 (발도 / 공격 시 해제)" : local.Pawn.State != PawnState.Active ? "넘어짐 · 일어나는 중"
                : ClassicControls ? "좌클릭 → 기물마다 4연속 베기 (누를 때마다 방향이 바뀜)" : local.Drawn ? "넓고 빠르게 휘두르면 넘어뜨립니다 · 무게감 있는 드래그" : "좌클릭을 누르고 드래그 → 물리 칼 휘두르기";
            // The HUD's own menu now shows only at the end (Esc opens GameMenu instead).
            hud.Draw(White, Black, Remaining, targetScore, local == null ? -1 : local.Pawn.Team, status, false, Finished, ClassicControls);
        }
        void OnDestroy()
        {
            ResetSwordLook();
            InputLocked = false;
            Physics.gravity = savedGravity; Cursor.lockState = savedLock; Cursor.visible = savedCursor;
            if (white != null) Destroy(white); if (black != null) Destroy(black);
        }
    }
}
