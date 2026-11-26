using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Pawn Rush piece skills on Pawn Rush course 01 (R125), offline and online, as they are in the skill test
    /// scene (PawnRushSkillBed) but without its test tools: every pawn gets the skills (the design cooldowns, not the
    /// test bed's 2 s), the effects (design A) and the sounds, and the person at this PC sees the same skill key card
    /// at the bottom right. F is the skill key (the playtest and the match link send it as the skill press); the rook
    /// and the bishop aim on F and go on the left click. Every piece starts as a pawn; a station's single-use pad
    /// promotes it (StationPromotion, ChessFight.PawnRush).
    ///
    /// Made when a scene with the course loads (the course raises CourseSkills.PawnRush; this assembly cannot see the
    /// course's types). Online the match link tells it whose pawn is this PC's and that a hit stop may only hold the
    /// picture (the host's physics must not stop).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public sealed class PawnRushCourseSkills : MonoBehaviour
    {
        public static PawnRushCourseSkills Current { get; private set; }

        /// <summary>The round is the mode 폰 러쉬-스킬 테스트 (GameModes.PawnRushSkillTest; JY-skill-test-mode 2026-11-26, the
        /// user: "기존에 있던 폰 러쉬 게임모드랑 똑같은데 k키로 스킬트리를 열 수 있고, 원하는걸로 넣었다 뺐다 할수있는거야. 물론
        /// 그건 방장만 가능하고, 모든 유저 똑같이 적용되야해"): the developers' skill tree (K) of the skill lab, starting from
        /// the host's last loadout; only the room's manager changes it (everyone else may look), and the host's loadout is
        /// everyone's. Any other round plays the skills as they are - main's: the loadout 0, no skill tree, no hand feel,
        /// no edge guard - and so does a course scene opened by itself. Set by the match link before the round is
        /// built.</summary>
        public static bool SkillTest;

        /// <summary>The skills' numbers here: the design (v0.1 previz, D2) cooldowns, not the test bed's 2 s.</summary>
        public readonly PawnRushSkillParams skills = new PawnRushSkillParams { testCooldown = 0f };

        /// <summary>The Pawn Rush pieces' own movement (jump, gravity, lips and ledges; seoungtest 10-30). Sword Fight
        /// and the other modes keep the shared tuning. Shown in the Inspector during Play to try numbers live.</summary>
        public PawnRushPhysicsParams physics = new PawnRushPhysicsParams();

        PawnRushSkillFx fx;
        PawnRushSkillSfx sfx;
        PlaytestSpawner spawner;
        PawnRushSkillWindow window;
        const int TreeLoadout = 1, PlainLoadout = 0;
        int loadoutPreset = -1;
        System.Action<int, int> loadoutAsk;

        // ---- the developers' skill tree over a match (JY-skill-lab; the match link sets these) ----

        /// <summary>Set by the match link on a PC that does not run the skills: a change made in the skill tree is asked
        /// of the host with it (unset - offline, the host - the change is made here).</summary>
        public System.Action<int, int> LoadoutAsk
        {
            get => loadoutAsk;
            set
            {
                loadoutAsk = value;
                if (window != null) window.Send = value;
            }
        }

        /// <summary>Set by the match link: this PC is the room's manager, who may lock the loadout and change a locked
        /// one. Unset (offline): yes.</summary>
        public System.Func<bool> LoadoutManager { get; set; }

        /// <summary>Goes up with every change to the loadout made on this PC (the host sends it to everyone again).</summary>
        public int LoadoutVersion { get; private set; }

        bool Manager => LoadoutManager == null || LoadoutManager();

        /// <summary>The loadout changed on the PC that runs the skills: every skill under way and every cooldown start over.</summary>
        void OnLoadoutChanged()
        {
            foreach (var pawn in RagdollPawn.All)
                if (pawn != null && !pawn.NetworkPuppet) pawn.ResetSkill();
            LoadoutVersion++;
        }

        /// <summary>The host's: a change a player asked for (PawnRushLoadout.Apply's two numbers), if that player may make
        /// it. Kept as this PC's own loadout for the next play, as its own changes are.</summary>
        public bool EditLoadout(int op, int value, bool manager)
        {
            // (Only in the skill test, and only the manager's: the loadout is kept locked to him there.)
            if (!SkillTest || !manager || op == PawnRushLoadout.OpHostOnly) return false;
            if (!skills.loadout.Allows(op, manager) || !skills.loadout.Apply(op, value)) return false;
            PawnRushSkillWindow.Save(skills.loadout);
            OnLoadoutChanged();
            return true;
        }

        /// <summary>A client's: the host's loadout as it arrived.</summary>
        public void TakeLoadout(PawnRushLoadout from)
        {
            if (from != null && !skills.loadout.Same(from)) skills.loadout.CopyFrom(from);
        }

        string CardName(PieceKind piece)
        {
            var lo = skills.loadout;
            string names = "";
            for (int k = 0; k < lo.SlotCount; k++)
            {
                int id = lo.Skill(piece, k);
                if (id != 0) names += (names.Length > 0 ? " · " : "") + PawnRushSkillBook.Name(id);
            }
            return names.Length > 0 ? names : "스킬 없음";
        }

        string CardLine(PieceKind piece)
        {
            var lo = skills.loadout;
            for (int k = 0; k < lo.SlotCount; k++)
            {
                int id = lo.Skill(piece, k);
                if (id != 0) return PawnRushSkillBook.Card(id);
            }
            return "칸이 비어 있다 (스킬트리 K)";
        }

        /// <summary>The pawn played at this PC (set by the online link; offline it is the playtest's).</summary>
        public RagdollPawn Local { get; set; }

        /// <summary>Online: a hit stop holds only the two pieces' picture on each screen (D-S2).</summary>
        public bool Online
        {
            get => fx != null && fx.OnlineOverride;
            set { if (fx != null) fx.OnlineOverride = value; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            Attach(SceneManager.GetActiveScene());
        }

        static void OnScene(Scene scene, LoadSceneMode mode) => Attach(scene);

        static void Attach(Scene scene)
        {
            if (!CourseSkills.PawnRush || Current != null || !scene.IsValid()) return;
            // The course's own scene (it has the playtest spawner), not whatever loads while the course is going.
            bool course = false;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<PlaytestSpawner>(true) != null) { course = true; break; }
            if (!course) return;
            var go = new GameObject("Pawn Rush skills (course)");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<PawnRushCourseSkills>();
        }

        void Awake()
        {
            Current = this;
            RagdollPawn.ResetTeamGauges();   // JY-skill-lab: the teams' ultimate gauges start empty with the round
            ChessFight.Gameplay.CourseSkills.MiniGameDone += OnMiniGameDone;   // the set 2안-a's endgame shapes
            fx = gameObject.AddComponent<PawnRushSkillFx>();
            fx.ParamsOverride = skills;
            fx.ViewerOverride = () => Viewer;
            sfx = gameObject.AddComponent<PawnRushSkillSfx>();
            gameObject.AddComponent<PawnRushStepSfx>();   // R652: footsteps, timed on every PC from what it sees
            // JY-skill-lab: the 2.0 skill plan prototypes' lines and rings (plan 1-나, RagdollPawn.SkillPlans.cs).
            var marks = gameObject.AddComponent<PawnRushPlanMarks>();
            marks.Viewer = () => Viewer;
            // JY-skill-lab: the developers' skill tree (K) - in the skill test mode only (JY-skill-test-mode): everyone opens
            // it, the room's manager alone changes it (no lock to switch: it is his), and the match link carries it.
            window = gameObject.AddComponent<PawnRushSkillWindow>();
            window.Skills = skills;
            window.OwnCursor = true;
            window.KeyName = CourseKey;
            window.CanEdit = op => SkillTest && Manager && op != PawnRushLoadout.OpHostOnly;
            window.LockedNote = op => "방장만 바꿀 수 있어요 (보기만 해요)";
            window.ShowLock = () => false;
            window.Blocked = () => !SkillTest || ChessFight.Game.LoadingScreenView.Showing;
            window.Changed = OnLoadoutChanged;
            window.Send = loadoutAsk;
            // The promotion cards name the skills in the piece's slots (the plain round: their own names, as on main).
            PawnRushPromotion.NameNow = piece => SkillTest ? CardName(piece) : null;
            PawnRushPromotion.LineNow = piece => SkillTest ? CardLine(piece) : null;
            spawner = FindFirstObjectByType<PlaytestSpawner>(FindObjectsInactive.Include);
        }

        // JY-skill-lab (the set 2안-a, Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.9: "미니게임 2가 끝나면 엔드게임 모양"): how many
        // mini-games each team has cleared; at its second, every piece of the team plays its endgame shapes.
        readonly int[] gamesDone = new int[2];

        void OnMiniGameDone(int team)
        {
            if (team < 0 || team > 1) return;
            gamesDone[team]++;
            if (gamesDone[team] != 2) return;
            foreach (var pawn in RagdollPawn.All)
                if (pawn != null && pawn.Team == team) pawn.ShapeStage = 2;
            Debug.Log($"[PawnRush] {ChessFight.Gameplay.Teams.Name(team)}팀 미니게임 2 끝 — 2안 스킬이 엔드게임 모양으로");
        }

        void OnDestroy()
        {
            ChessFight.Gameplay.CourseSkills.MiniGameDone -= OnMiniGameDone;
            if (Current != this) return;
            Current = null;
            PawnRushPromotion.NameNow = PawnRushPromotion.LineNow = null;
        }

        /// <summary>The key of a slot as the settings bind it: 기물 스킬 (Use), then 두 번째 스킬 (Skill). The third slot (the
        /// queen's R, JY-skill-lab's kits' second version) has no key on the course (R is main's way back when stuck, R786),
        /// and neither has the ultimate's since main's R786 took Q (Skill2) out of the settings.</summary>
        static string CourseKey(int slot) =>
            slot == 2 ? "R (아직 코스 없음)"
            : slot == PawnRushLoadout.UltSlot ? "Q (코스에 키 없음)"
            : ChessFight.Game.GameSettings.KeyCap(slot == 0 ? ChessFight.Game.GameKey.Use : ChessFight.Game.GameKey.Skill);

        RagdollPawn Viewer
        {
            get
            {
                if (Local != null) return Local;
                return spawner != null && spawner.Driver is RagdollDriver driver ? driver.Pawn : null;
            }
        }

        void Update()
        {
            if (!CourseSkills.PawnRush)
            {
                Destroy(gameObject);
                return;
            }
            // JY-skill-test-mode: the skill test starts from the loadout this PC last made in the skill tree (the host's
            // reaches the others over the match link and replaces theirs), locked to the room's manager, with the skill
            // lab's hand feel and edge guard; any other round plays main's skills as main plays them. (Looked at every
            // frame: the match link may set the mode a moment after the course is up.)
            int start = SkillTest ? TreeLoadout : PlainLoadout;
            if (start != loadoutPreset)
            {
                loadoutPreset = start;
                if (start == TreeLoadout)
                {
                    PawnRushSkillWindow.LoadSaved(skills);
                    skills.loadout.hostOnly = true;
                }
                else
                {
                    skills.loadout.CopyFrom(PawnRushLoadout.Preset(0));
                    if (window != null && window.Open) window.Toggle();
                }
                skills.feel = skills.basicEdgeSafe = start == TreeLoadout;
                OnLoadoutChanged();
            }
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null) continue;
                if (pawn.PawnRushSkills == null) pawn.PawnRushSkills = skills;
                if (pawn.PawnRushPhysics == null) pawn.PawnRushPhysics = physics;
            }
            Mash();
        }

        // 2026-11-25 (the tutorial's struggle feel, then user "이건 실제 인게임에서도 플레이어들끼리 작동이 되야해 단 플레이
        // 연타 같은 건 자기 화면에만 나타나게"): each struggle click of this PC's own pawn while it is held - the key the
        // struggle reads (GameKey.Shove, the left click), pressed here, so a client hears it at once without waiting for
        // the host - is a '탁' (PawnRushSkillSfx.PlayMash) and a short judder of this PC's view (0.06 m and 0.6°, gone in
        // 0.14 s; a skill hit is 0.1 - 0.2 over 0.2 - 0.3 s). Nothing is sent: the other players do not hear or feel it.
        const float MashShake = .06f, MashShakeFor = .14f;

        void Mash()
        {
            var me = Viewer;
            if (me == null || !me.BeingHeld || me.State == PawnState.Ragdoll) return;
            if (ChessFight.Game.GameMenu.IsOpen || ChessFight.Game.SettingsWindow.IsOpen) return;
            if (!ChessFight.Game.GameSettings.Pressed(ChessFight.Game.GameKey.Shove)) return;
            if (sfx != null) sfx.PlayMash(me.Hips.position);
            if (fx != null) fx.ShakeView(MashShake, MashShakeFor);
        }

        /// <summary>The skill key card at the bottom right, in the same colours as the skill test bed's (PawnRushSkillFx.SkillCard,
        /// the board look). Not while the entrance screen still covers the course (R148: it showed through at 100 %).</summary>
        void OnGUI()
        {
            if (ChessFight.Game.LoadingScreenView.Showing) return;
            if (ChessFight.Game.SettingsWindow.IsOpen) return;     // OnGUI draws over the settings window's canvas (user 11-23)
            var me = Viewer;
            if (me == null || me.PawnRushSkills == null || me.Piece == PieceKind.King) return;
            if (SkillTest)
            {
                // R674: the keys as the settings window has them bound (F, E, Q and the left click unless rebound).
                // JY-skill-lab: a card for each of the piece's slots that holds a skill (PawnRushSkillBed.SlotCards).
                PawnRushSkillBed.SlotCards(me, CourseKey, ChessFight.Game.GameSettings.KeyCap(ChessFight.Game.GameKey.Shove), false,
                    ChessPieces.Name(me.Piece), me.Piece == PieceKind.Pawn ? " · 승격 발판에 서서 승격" : null);
                return;
            }
            bool running = me.SkillStage != SkillStage.None, ready = !running && me.SkillCooldown <= 0f;
            // R674: the keys as the settings window has them bound (F and the left click unless rebound).
            string key = ChessFight.Game.GameSettings.KeyCap(ChessFight.Game.GameKey.Use);
            string click = ChessFight.Game.GameSettings.KeyCap(ChessFight.Game.GameKey.Shove);
            string how = me.Piece == PieceKind.Rook || me.Piece == PieceKind.Bishop ? Loc.F("{0} 조준 → {1}", key, click) : Loc.F("{0} 사용", key);
            if (me.Piece == PieceKind.Pawn) how += " · " + Loc.T("승격 발판에 서서 승격");
            PawnRushSkillFx.SkillCard(SkillKeyCard.Slot(0), key, Loc.T(ChessPieces.Name(me.Piece)) + " · " + Loc.T(RagdollPawn.SkillName(me.Piece)),
                SkillKeyCard.Line(running ? PawnRushSkillBed.StageName(me) : null, me.SkillCooldown, how), me.Piece,
                ready || running, me.SkillCooldownTotal > 0f ? me.SkillCooldown / me.SkillCooldownTotal : 0f);
        }
    }
}
