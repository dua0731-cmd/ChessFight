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
            gameObject.AddComponent<PawnRushStepSfx>();   // R652: footsteps, timed on every PC from what it sees
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
