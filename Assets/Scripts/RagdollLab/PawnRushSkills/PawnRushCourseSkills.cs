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

        /// <summary>The skills' numbers here: the design (v0.1 previz, D2) cooldowns, not the test bed's 2 s.</summary>
        public readonly PawnRushSkillParams skills = new PawnRushSkillParams { testCooldown = 0f };

        /// <summary>The Pawn Rush pieces' own movement (jump, gravity, lips and ledges; seoungtest 10-30). Sword Fight
        /// and the other modes keep the shared tuning. Shown in the Inspector during Play to try numbers live.</summary>
        public PawnRushPhysicsParams physics = new PawnRushPhysicsParams();

        PawnRushSkillFx fx;
        PawnRushSkillSfx sfx;
        PlaytestSpawner spawner;
        PawnRushSkillWindow window;
        const int OwnLoadout = -2;
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
            fx = gameObject.AddComponent<PawnRushSkillFx>();
            fx.ParamsOverride = skills;
            fx.ViewerOverride = () => Viewer;
            sfx = gameObject.AddComponent<PawnRushSkillSfx>();
            gameObject.AddComponent<PawnRushStepSfx>();   // R652: footsteps, timed on every PC from what it sees
            // JY-skill-lab: the 2.0 skill plan prototypes' lines and rings (plan 1-나, RagdollPawn.SkillPlans.cs).
            var marks = gameObject.AddComponent<PawnRushPlanMarks>();
            marks.Viewer = () => Viewer;
            // JY-skill-lab: the developers' skill tree (K), here too - alone on the course and in a room's match, where
            // everyone opens it and the room's manager may keep the changes to himself (the match link carries them).
            window = gameObject.AddComponent<PawnRushSkillWindow>();
            window.Skills = skills;
            window.OwnCursor = true;
            window.KeyName = CourseKey;
            window.CanEdit = op => skills.loadout.Allows(op, Manager);
            window.LockedNote = op => op == PawnRushLoadout.OpHostOnly ? "잠금은 방장만 바꿀 수 있어요" : "방장만 바꿀 수 있게 잠겨 있어요";
            window.ShowLock = () => Online;
            window.Blocked = () => ChessFight.Game.LoadingScreenView.Showing;
            window.Changed = OnLoadoutChanged;
            window.Send = loadoutAsk;
            // The promotion cards name the skills in the piece's slots.
            PawnRushPromotion.NameNow = CardName;
            PawnRushPromotion.LineNow = CardLine;
            spawner = FindFirstObjectByType<PlaytestSpawner>(FindObjectsInactive.Include);
        }

        void OnDestroy()
        {
            if (Current != this) return;
            Current = null;
            PawnRushPromotion.NameNow = PawnRushPromotion.LineNow = null;
        }

        /// <summary>The key of a slot as the settings bind it: 기물 스킬 (Use), then the two ability keys (Skill, Skill2). The
        /// third slot (the queen's R, JY-skill-lab's kits' second version) has no key on the course yet.</summary>
        static string CourseKey(int slot) =>
            slot == 2 ? "R (아직 코스 없음)"
            : ChessFight.Game.GameSettings.KeyCap(slot == 0 ? ChessFight.Game.GameKey.Use : slot == 1 ? ChessFight.Game.GameKey.Skill : ChessFight.Game.GameKey.Skill2);

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
            // JY-skill-lab: what a room's match starts with is the room's rule (PawnRushRules.Skills, the rules panel): the
            // skills as they are, plan 1-나's, or the loadout this PC last made in the skill tree (the host's reaches the
            // others over the match link). Alone on the course there is no room: this PC's own last loadout. From there
            // the skill tree (K) changes it. (Looked at every frame: the match link may say "online" a moment after the
            // course is up.)
            int start = Online ? PawnRushRules.Current.Skills : OwnLoadout;
            if (start != loadoutPreset)
            {
                loadoutPreset = start;
                if (start == OwnLoadout || start == PawnRushRules.SkillsTree) PawnRushSkillWindow.LoadSaved(skills);
                else skills.loadout.CopyFrom(PawnRushLoadout.Preset(start == PawnRushRules.SkillsChessMoves ? 1 : 0));
                // (Alone, the lock means nothing; a room's is its manager's to set.)
                if (start == OwnLoadout) skills.loadout.hostOnly = false;
                OnLoadoutChanged();
            }
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null) continue;
                if (pawn.PawnRushSkills == null) pawn.PawnRushSkills = skills;
                if (pawn.PawnRushPhysics == null) pawn.PawnRushPhysics = physics;
            }
        }

        /// <summary>The skill key card at the bottom right, in the same colours as the skill test bed's (PawnRushSkillFx.SkillCard,
        /// the board look). Not while the entrance screen still covers the course (R148: it showed through at 100 %).</summary>
        void OnGUI()
        {
            if (ChessFight.Game.LoadingScreenView.Showing) return;
            var me = Viewer;
            if (me == null || me.PawnRushSkills == null || me.Piece == PieceKind.King) return;
            // R674: the keys as the settings window has them bound (F, E, Q and the left click unless rebound).
            // JY-skill-lab: a card for each of the piece's slots that holds a skill (PawnRushSkillBed.SlotCards).
            PawnRushSkillBed.SlotCards(me, CourseKey, ChessFight.Game.GameSettings.KeyCap(ChessFight.Game.GameKey.Shove), false,
                ChessPieces.Name(me.Piece), me.Piece == PieceKind.Pawn ? " · 승격 발판에 서서 승격" : null);
        }
    }
}
