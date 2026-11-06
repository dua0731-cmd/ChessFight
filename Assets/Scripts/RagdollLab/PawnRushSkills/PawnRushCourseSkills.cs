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
            fx = gameObject.AddComponent<PawnRushSkillFx>();
            fx.ParamsOverride = skills;
            fx.ViewerOverride = () => Viewer;
            sfx = gameObject.AddComponent<PawnRushSkillSfx>();
            spawner = FindFirstObjectByType<PlaytestSpawner>(FindObjectsInactive.Include);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

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
            bool running = me.SkillStage != SkillStage.None, ready = !running && me.SkillCooldown <= 0f;
            string how = me.Piece == PieceKind.Rook || me.Piece == PieceKind.Bishop ? "F 조준 → 좌클릭" : "F 사용";
            if (me.Piece == PieceKind.Pawn) how += " · 승격 발판에 서서 승격";
            PawnRushSkillFx.SkillCard(SkillKeyCard.Slot(0), "F", $"{ChessPieces.Name(me.Piece)} · {RagdollPawn.SkillName(me.Piece)}",
                SkillKeyCard.Line(running ? PawnRushSkillBed.StageName(me) : null, me.SkillCooldown, how), me.Piece,
                ready || running, me.SkillCooldownTotal > 0f ? me.SkillCooldown / me.SkillCooldownTotal : 0f);
        }
    }
}
