using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab
{
    [DefaultExecutionOrder(-90)]
    public sealed class SwordFightGame : MonoBehaviour
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
        public int White => Authority ? Rules.White : remoteWhite;
        public int Black => Authority ? Rules.Black : remoteBlack;
        public float Remaining => Authority ? Mathf.Max(0, roundSeconds - (float)Rules.Elapsed) : remoteRemaining;
        public bool Finished => Authority ? Rules.Finished : remoteFinished;
        public SwordFightPawn Local => Fighters.TryGetValue(LocalId, out var f) ? f : null;
        public bool MenuOpen { get; private set; }
        public int Revision { get; private set; }
        Material white, black;
        SwordFightHud hud;
        int remoteWhite, remoteBlack;
        float remoteRemaining;
        bool remoteFinished;
        Vector3 savedGravity;
        CursorLockMode savedLock;
        bool savedCursor;

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
        }
        void Start()
        {
            hud = gameObject.AddComponent<SwordFightHud>();
            hud.Resume += () => { MenuOpen = false; RefreshCursor(); };
            hud.Leave += Exit;
            if (!Networked && !Automated)
            {
                Add(1, 0, 0, false, "나 · 폰");
                Add(2, 0, 1, true, "아군 더미");
                Add(3, 1, 0, true, "적군 더미 1");
                Add(4, 1, 1, true, "적군 더미 2");
            }
            RefreshCursor();
            if (Automated) gameObject.AddComponent<SwordFightAutoTest>();
        }

        public static Vector3 SpawnPoint(int team, int slot) => new Vector3((slot % 3 - 1) * 1.8f, 0, (team == 0 ? -1 : 1) * (3.3f + slot / 3 * 1.4f));
        public static Vector3 Facing(int team) => team == 0 ? Vector3.forward : Vector3.back;
        public SwordFightPawn Add(ulong id, int team, int slot, bool bot, string displayName)
        {
            if (Fighters.TryGetValue(id, out var old)) return old;
            var pawn = Instantiate(pawnPrefab, SpawnPoint(team, slot) + Vector3.up * .02f, Quaternion.LookRotation(Facing(team)));
            pawn.name = displayName; pawn.DisplayName = displayName; pawn.tuning = tuning; pawn.Team = team;
            pawn.skin.sharedMaterial = team == 0 ? white : black;
            foreach (var rb in pawn.bodies) { rb.solverIterations = 24; rb.solverVelocityIterations = 6; }
            var fighter = pawn.gameObject.AddComponent<SwordFightPawn>();
            fighter.Initialize(id, slot, bot); fighter.Respawn(SpawnPoint(team, slot), Facing(team), Authority);
            Fighters.Add(id, fighter);
            if (id == LocalId && CameraRig != null) CameraRig.soloTarget = pawn;
            return fighter;
        }
        public void Remove(ulong id)
        {
            if (!Fighters.TryGetValue(id, out var f)) return;
            Destroy(f.gameObject); Fighters.Remove(id); Rules.Remove(id);
        }

        void Update()
        {
            if (Automated) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { MenuOpen = !MenuOpen; RefreshCursor(); }
            if (!Networked && !Finished && Local != null) Local.SetInput(ReadLocalInput());
            if (Finished) RefreshCursor();
        }
        public PawnInput ReadLocalInput()
        {
            if (MenuOpen || Finished || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked) return default;
            float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float y = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            Vector3 move = CameraRig.FlatRight * x + CameraRig.FlatForward * y;
            return new PawnInput { move = Vector3.ClampMagnitude(move, 1), aim = CameraRig.FlatForward,
                jump = Input.GetKeyDown(KeyCode.Space), sprint = Input.GetKey(KeyCode.LeftShift), shove = Input.GetMouseButtonDown(0) };
        }
        void FixedUpdate()
        {
            if (!Authority || Automated) return;
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
                if (f.Bot && f.Alive) f.SetInput(default);
            }
        }
        public void ApplyScore(int w, int b, float remaining, bool finished)
        { remoteWhite = w; remoteBlack = b; remoteRemaining = remaining; remoteFinished = finished; }

        void RefreshCursor()
        {
            bool free = MenuOpen || Finished || Automated;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = free;
        }
        void Exit()
        {
            if (LeaveMatch != null) LeaveMatch();
            else SceneManager.LoadScene("Lobby");
        }
        void LateUpdate()
        {
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
            string status = local == null ? "입장 중" : !local.Alive ? $"{local.RespawnSeconds:0.0}초 뒤 부활"
                : local.Protection > 0 ? "부활 보호 (공격하면 해제)" : local.Pawn.State != PawnState.Active ? "넘어짐 · 일어나는 중" : local.Attacking ? "휘두르는 중" : "";
            hud.Draw(White, Black, Remaining, targetScore, local == null ? -1 : local.Pawn.Team, status, MenuOpen, Finished);
        }
        void OnDestroy()
        {
            Physics.gravity = savedGravity; Cursor.lockState = savedLock; Cursor.visible = savedCursor;
            if (white != null) Destroy(white); if (black != null) Destroy(black);
        }
    }
}
