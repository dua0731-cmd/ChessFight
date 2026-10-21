using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Sword Fight prototype match (R149; spec "소드 파이트 프로토타입 구현 명세 v1.0 — 떨어뜨리고 빼앗기",
    /// Docs/GameModes/SwordFight/PROTOTYPE.md). SwordFightGame adds it in the match scene (the lobby's, or played alone in
    /// the editor); the skill test scene keeps its bed instead. The rules are Core's <see cref="SwordMatchRules"/>: pick a
    /// piece (15 s) → 3·2·1 → play: every fall is a point for the other side, ten wins, at 4:00 the higher score wins,
    /// a tie is sudden death (60 s more is a draw). A fallen piece comes back a pawn after 3 s; a pawn whose hit made an
    /// enemy piece fall takes that piece. From 2:00 a ghost piece erases squares every 30 s.
    /// <list type="bullet">
    /// <item>The host runs the rules (this PC offline). Online (the lobby's room, LabNetLink with SteamMatchLabLink) its
    /// state goes to every client as text ten times a second (<see cref="WireText"/>, <see cref="ApplyRemote"/>); the
    /// clients' screens read only that and the bodies. The squares erased come from the seed and the clock on every PC.</item>
    /// <item>The fighters carry the piece skills (F, E) of the skill test scene with the plan's cooldowns, the pawn's F
    /// is its first two steps, the king's guard takes swords only, the sword is the click cut only.</item>
    /// </list>
    /// The HUD, the board's pictures and the development keys are this class's other files.
    /// </summary>
    public sealed partial class SwordFightMatch : MonoBehaviour
    {
        public SwordFightSkillParams skills = new SwordFightSkillParams { testCooldown = 0f };
        public SwordFightEdgeParams edge = new SwordFightEdgeParams { testCooldown = 0f };
        [Tooltip("칸 크기 (m): 3 대 3까지")]
        public float cellSize = 2f;
        [Tooltip("칸 크기 (m): 한 팀 4명 이상 (명세 §5)")]
        public float bigCellSize = 2.5f;
        public SwordMatchSettings settings = new SwordMatchSettings();

        SwordFightGame game;
        SwordFightEdgeFloor floor;
        SwordMatchRules rules;
        SwordMatchState view = new SwordMatchState();
        float viewAt;               // when a client's view arrived (realtime)
        bool slowing;
        float slowUntil;
        bool overReported;

        public float Cell { get; private set; }
        public SwordPhase Phase => view.Phase;
        public int White => view.White;
        public int Black => view.Black;
        public bool Finished => view.Phase == SwordPhase.Result;
        /// <summary>The match clock now: the host's rules, a client's view moved on since it arrived.</summary>
        public double Clock => Authority ? rules.Clock : view.Clock + (Live(view.Phase) ? Time.realtimeSinceStartup - viewAt : 0f);
        public double PhaseTime => Authority ? rules.PhaseTime : view.PhaseTime + (view.Phase != SwordPhase.Result ? Time.realtimeSinceStartup - viewAt : 0f);
        public float Remaining => view.Phase == SwordPhase.Pick || view.Phase == SwordPhase.Countdown ? (float)settings.Duration
            : Mathf.Max(0f, (float)(settings.Duration - Clock));
        bool Authority => game != null && game.Authority && rules != null;
        static bool Live(SwordPhase p) => p == SwordPhase.Playing || p == SwordPhase.SuddenDeath;

        void Awake()
        {
            game = GetComponent<SwordFightGame>();
            if (GetComponent<SwordFightSkillFx>() == null) gameObject.AddComponent<SwordFightSkillFx>();
            if (GetComponent<SwordFightSkillSfx>() == null) gameObject.AddComponent<SwordFightSkillSfx>();
            floor = GetComponent<SwordFightEdgeFloor>();
            if (floor == null) floor = gameObject.AddComponent<SwordFightEdgeFloor>();
            SwordFightSkills.GuardTakesSwordOnly = true;
            SwordFightSkills.PawnSteps = true;
            SizeBoard(cellSize);
            game.PieceRequest = OnPieceRequest;
            NewRules();
        }

        void OnEnable()
        {
            SwordFightPawn.Struck += OnStruck;
            SwordFightSkills.Fx += OnSkillFx;
            SwordFightEdgeFloor.KnockedLoose += OnKnockedLoose;
        }

        void OnDisable()
        {
            SwordFightPawn.Struck -= OnStruck;
            SwordFightSkills.Fx -= OnSkillFx;
            SwordFightEdgeFloor.KnockedLoose -= OnKnockedLoose;
        }

        void OnDestroy()
        {
            SwordFightSkills.GuardTakesSwordOnly = false;
            SwordFightSkills.PawnSteps = false;
            SwordFightGame.InputLocked = false;
            if (slowing) Time.timeScale = 1f;
            DestroyBoardPictures();
        }

        // ---------------------------------------------------------------- the round's start and its owners

        void NewRules()
        {
            rules = new SwordMatchRules(Random.Range(1, int.MaxValue), settings);
            view = rules.State();
            overReported = false;
            ResetBoard();
        }

        /// <summary>The round starts again (SwordFightGame.ResetRound: an online match starting or ending). A client keeps
        /// no rules: it waits for the host's state.</summary>
        public void Restart()
        {
            NewRules();
            if (game.Networked && !game.Authority) { rules = null; view = new SwordMatchState(); }
            else foreach (var f in game.Fighters.Values) if (f != null) rules.Add(f.Id, f.Pawn.Team);
            if (slowing) { Time.timeScale = 1f; slowing = false; }
        }

        /// <summary>This PC became the host mid-match (HOST.md): the rules go on from the last state the old host sent.</summary>
        public void TakeOver()
        {
            var last = view;
            rules = new SwordMatchRules(last.Seed != 0 ? last.Seed : Random.Range(1, int.MaxValue), settings);
            foreach (var f in game.Fighters.Values) if (f != null) rules.Add(f.Id, f.Pawn.Team);
            rules.Restore(last);
            foreach (var s in last.Fighters)
            {
                var r = rules.Get(s.Id);
                if (r == null) continue;
                r.Piece = s.Piece;
                r.Picked = s.Picked;
                r.Alive = s.Alive;
                r.SpawnCell = s.SpawnCell;
                if (!s.Alive) r.RespawnAt = rules.Time + s.Respawn;
            }
            overReported = last.Phase == SwordPhase.Result;
        }

        /// <summary>A fighter came into the match (SwordFightGame.Add).</summary>
        public void Joined(SwordFightPawn f)
        {
            if (rules != null && (game == null || game.Authority)) rules.Add(f.Id, f.Pawn.Team);
            if (Cell > 0f && TeamSize() >= 4 && !Mathf.Approximately(Cell, bigCellSize) && view.Phase == SwordPhase.Pick) SizeBoard(bigCellSize);
        }

        public void Left(ulong id) => rules?.Remove(id);

        int TeamSize()
        {
            int w = 0, b = 0;
            foreach (var f in game.Fighters.Values) if (f != null) { if (f.Pawn.Team == 0) w++; else b++; }
            return Mathf.Max(w, b);
        }

        // ---------------------------------------------------------------- where pieces stand

        public Vector3 CellCentre(int cell)
        {
            SwordBoard.Centre(cell, Cell, out double x, out double z);
            return new Vector3((float)x, floor != null && floor.Found ? floor.Top : 0f, (float)z);
        }

        public Rect CellRect(int cell)
        {
            float x0 = (SwordBoard.X(cell) - SwordBoard.Size / 2) * Cell, z0 = (SwordBoard.Y(cell) - SwordBoard.Size / 2) * Cell;
            return new Rect(x0, z0, Cell, Cell);
        }

        public Vector3 StartPoint(int team, int slot) => CellCentre(SwordBoard.StartCell(team, slot));

        // ---------------------------------------------------------------- every frame

        void Update()
        {
            if (game == null) return;
            // Every fighter carries the piece skills (as the skill test bed gives them).
            foreach (var f in game.Fighters.Values)
            {
                if (f == null || f.Skills != null) continue;
                f.gameObject.AddComponent<SwordFightSkills>().Init(f, skills, edge);
            }
            if (Authority) view = HostView();
            var phase = view.Phase;
            SwordFightGame.InputLocked = phase == SwordPhase.Pick || phase == SwordPhase.Countdown || phase == SwordPhase.Result;
            // The pick and the result want the mouse for their buttons.
            game.UiWantsCursor = phase == SwordPhase.Pick || phase == SwordPhase.Result;

            var me = game.Local;
            if (me != null && me.Skills != null)
            {
                if (game.CameraRig != null) me.Skills.SetAimCamera(game.CameraRig.Cam);
                // F starts the aim (the pawn's steps, the king's guard), the left click uses it, the right click calls it
                // off, E is the edge skill. A client's presses go to the host (LabNetLink.Sword.cs reads them).
                if (!LabNetLink.SwordClient && game.KeysFree && !SwordFightGame.InputLocked)
                {
                    if (Input.GetKeyDown(KeyCode.F)) me.Skills.PressKey();
                    if (GameSettings.Pressed(GameKey.Grab)) me.Skills.CancelAim();
                    if (GameSettings.Pressed(GameKey.Skill)) me.Skills.PressEdge();
                }
            }
            if (phase == SwordPhase.Pick) PickKeys();
            DevKeys();
            if (slowing && Time.realtimeSinceStartup >= slowUntil) { Time.timeScale = 1f; slowing = false; }
            StepBoard(Clock, view.Seed);
            StepPictures();
            StepFeed();
            StepProtection();
            StepClockSound();
        }

        SwordMatchState HostView()
        {
            var s = rules.State();
            for (int i = 0; i < s.Fighters.Count; i++)
            {
                var v = s.Fighters[i];
                if (game.Fighters.TryGetValue(v.Id, out var f) && f != null && f.Skills != null)
                    v.EReady = v.Alive && v.Piece != PieceKind.Pawn && f.Skills.EdgeCooldown <= 0f && f.Skills.EdgeStage == SfEdge.None;
                s.Fighters[i] = v;
            }
            return s;
        }

        void FixedUpdate()
        {
            if (!Authority) return;
            float dt = Time.fixedDeltaTime;
            // Dummies pick at once.
            if (rules.Phase == SwordPhase.Pick)
                foreach (var f in game.Fighters.Values)
                {
                    if (f == null || !f.Bot) continue;
                    var r = rules.Get(f.Id);
                    if (r == null || r.Picked) continue;
                    foreach (var p in SwordMatchRules.Pickable) if (rules.Pick(f.Id, p)) break;
                }
            var before = rules.Phase;
            rules.Advance(dt);
            if (rules.Phase != before) EnterPhase(rules.Phase);
            foreach (var f in game.Fighters.Values)
            {
                if (f == null) continue;
                bool fell = f.Alive && (f.Pawn.Hips.position.y < floor.Top - 3f || !f.Pawn.IsFinite());
                if (fell)
                {
                    if (rules.Live) RingOut(f);
                    // Off the board before or after play: back on its square.
                    else if (rules.Phase != SwordPhase.Result) Place(f, StartPoint(f.Pawn.Team, f.Slot));
                }
                if (!f.Alive)
                {
                    var r = rules.Get(f.Id);
                    f.RespawnSeconds = (float)rules.RespawnLeft(f.Id);
                    if (r != null && rules.WantsCell(f.Id)) r.SpawnCell = ChooseSpawn(f);
                    if (rules.TryRespawn(f.Id)) Respawn(f, r);
                }
            }
        }

        void EnterPhase(SwordPhase phase)
        {
            switch (phase)
            {
                case SwordPhase.Countdown:
                    // Everyone on their square with the piece picked, F and E ready (spec §7.3).
                    foreach (var f in game.Fighters.Values)
                    {
                        if (f == null) continue;
                        var r = rules.Get(f.Id);
                        if (r == null) continue;
                        f.Pawn.SetPiece(r.Piece);
                        f.Skills?.Ready(0f);
                        Place(f, StartPoint(f.Pawn.Team, f.Slot));
                    }
                    break;
                case SwordPhase.Result:
                    // The 1.5 s slow motion everyone sees (the host's physics, so the bodies on every screen).
                    Time.timeScale = 0.3f;
                    slowing = true;
                    slowUntil = Time.realtimeSinceStartup + 1.5f;
                    foreach (var f in game.Fighters.Values) f?.StopCombat();
                    if (!overReported) { overReported = true; LabNetLink.Current?.ReportMatchOver(); }
                    break;
            }
        }

        void Place(SwordFightPawn f, Vector3 floorPoint)
        {
            if (!f.Alive) return;
            f.Respawn(floorPoint, SwordFightGame.Facing(f.Pawn.Team), true);
        }

        void RingOut(SwordFightPawn f)
        {
            var result = rules.RingOut(f.Id);
            if (!result.Valid) return;
            f.Eliminate();
            if (!result.Stole || !game.Fighters.TryGetValue(result.Credit, out var credit) || credit == null) return;
            // The steal (spec §8.3): the pawn becomes the fallen piece at once, F ready, E half its cooldown away.
            credit.Pawn.SetPiece(result.NewPiece);
            credit.Skills?.Ready(0.5f);
        }

        void Respawn(SwordFightPawn f, SwordMatchRules.Fighter r)
        {
            int cell = r != null && r.SpawnCell >= 0 ? r.SpawnCell : ChooseSpawn(f);
            if (r != null) r.SpawnCell = -1;
            f.Respawn(CellCentre(cell), SwordFightGame.Facing(f.Pawn.Team), true);
            f.Pawn.SetPiece(PieceKind.Pawn);
            f.Skills?.Ready(0f);
        }

        int ChooseSpawn(SwordFightPawn who)
        {
            var ox = new List<double>(); var oz = new List<double>(); var ex = new List<double>(); var ez = new List<double>();
            foreach (var f in game.Fighters.Values)
            {
                if (f == null || f == who || !f.Alive) continue;
                var p = f.Pawn.Hips.position;
                ox.Add(p.x); oz.Add(p.z);
                if (f.Pawn.Team != who.Pawn.Team) { ex.Add(p.x); ez.Add(p.z); }
            }
            int cell = SwordBoard.ChooseSpawn(who.Pawn.Team, ErasedNow, QueenDone, Cell, ox.ToArray(), oz.ToArray(), ex.ToArray(), ez.ToArray());
            return cell >= 0 ? cell : SwordBoard.StartCell(who.Pawn.Team, who.Slot);
        }

        // ---------------------------------------------------------------- who hit whom (the credit, spec §8.2)

        void OnStruck(SwordFightPawn by, SwordFightPawn victim)
        {
            if (Authority && by != null && victim != null) rules.Hit(by.Id, victim.Id, SwordCause.Sword, (byte)by.Pawn.Piece);
        }

        void OnSkillFx(SfFxEvent e)
        {
            if (!Authority || e.by == null || e.by.Fighter == null || e.target == null) return;
            var victim = e.target.GetComponent<SwordFightPawn>();
            if (victim == null) return;
            SwordCause cause;
            switch (e.kind)
            {
                case SfFxKind.KingCounterHit: case SfFxKind.KingRepel: case SfFxKind.QueenHit: case SfFxKind.RookHit:
                case SfFxKind.BishopPin: case SfFxKind.BishopSlow: case SfFxKind.KnightHit:
                    cause = SwordCause.Skill; break;
                case SfFxKind.KingLandHit: case SfFxKind.QueenCut: case SfFxKind.KnightKickHit:
                    cause = SwordCause.Edge; break;
                case SfFxKind.PawnHit: case SfFxKind.PawnBounce:
                    cause = SwordCause.PawnStep; break;
                default: return;
            }
            rules.Hit(e.by.Fighter.Id, victim.Id, cause, (byte)e.by.Piece);
        }

        void OnKnockedLoose(SwordFightEdgeFloor.Collapse c, RagdollPawn p)
        {
            if (!Authority || c.by == null || c.by.Fighter == null || p == null) return;
            var victim = p.GetComponent<SwordFightPawn>();
            if (victim != null) rules.Hit(c.by.Fighter.Id, victim.Id, SwordCause.Collapse, (byte)PieceKind.Rook);
        }

        // ---------------------------------------------------------------- picks

        /// <summary>The host takes a pick (or, in a development build, F8's piece change at any time).</summary>
        bool OnPieceRequest(ulong id, PieceKind kind)
        {
            if (!Authority) return false;
            if (rules.Phase == SwordPhase.Pick) return rules.Pick(id, kind);
            if (!Debug.isDebugBuild) return false;
            DevSetPiece(id, kind);
            return true;
        }

        public void PickLocal(PieceKind kind)
        {
            if (Authority) rules.Pick(game.LocalId, kind);
            else LabNetLink.Current?.AskSwordPiece(kind);
            pickAsked = kind;
        }

        PieceKind? pickAsked;

        void PickKeys()
        {
            if (ChatBox.KeysHeld || game.MenuOpen) return;
            for (int i = 0; i < SwordMatchRules.Pickable.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) PickLocal(SwordMatchRules.Pickable[i]);
        }

        // ---------------------------------------------------------------- the wire

        /// <summary>The host's state for the clients (LabNetLink.Sword.cs sends it).</summary>
        public string WireText(System.Func<ulong, byte> refOf) => SwordMatchWire.Encode(view, refOf);

        /// <summary>A client: the host's state arrived.</summary>
        public void ApplyRemote(string text)
        {
            if (game.Authority && rules != null) return;
            var link = LabNetLink.Current;
            if (link == null || !SwordMatchWire.Decode(text, link.SwordIdOf, out var state)) return;
            view = state;
            viewAt = Time.realtimeSinceStartup;
            if (TeamSize() >= 4 && !Mathf.Approximately(Cell, bigCellSize) && state.Phase == SwordPhase.Pick) SizeBoard(bigCellSize);
        }
    }
}
