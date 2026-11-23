using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Pawn Rush skill test bed (scene PawnRush_SkillTest, a copy of RagdollTest). It gives every pawn in the
    /// lab the Pawn Rush skills, feeds P1's skill key, and draws a panel on the left of the screen to switch P1
    /// between the pieces Pawn Rush uses - pawn (the default), queen, rook, bishop, knight - and to set up the
    /// dummies. The king's skill is not made yet (D1). Every skill is on F for now (R74, temporary): in this scene F
    /// is not the lab's interact key (bells, levers). The rook and the bishop aim on F and go on the left click.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class PawnRushSkillBed : MonoBehaviour
    {
        public PawnRushSkillParams skills = new PawnRushSkillParams();
        // The test cooldown the panel's cooldown button gives back after "기획 쿨" (JY-skill-lab 11-20).
        float keptTestCooldown = 2f;

        [Header("키 (임시, 나중에 다시 정함)")]
        public KeyCode skillKey = KeyCode.F;
        public KeyCode previousPieceKey = KeyCode.Z;
        public KeyCode nextPieceKey = KeyCode.X;
        [Tooltip("룩 시험: 더미 4명을 내 앞에 뭉쳐 세운다")]
        public KeyCode clusterKey = KeyCode.V;
        [Tooltip("R153 (승규 님 10-16: \"테스트 해볼 수 있게 적과 아군 소환 하게 해줘\"): 내 앞에 적 / 아군 더미 하나 부르기")]
        public KeyCode summonEnemyKey = KeyCode.B;
        public KeyCode summonAllyKey = KeyCode.N;
        [Tooltip("소환: 내 앞 거리 (m)")]
        public float summonAhead = 4.5f;
        [Tooltip("R153 (승규 님 10-22: \"스킬넷에서 룩 시험 해볼 수 있는 환경 만들어줘\"): 룩 시험장으로 가기 / 시험장의 높은 단 위로")]
        public KeyCode rookGroundKey = KeyCode.H;
        public KeyCode rookLedgeKey = KeyCode.J;
        [Tooltip("앙파상 시험 (JY-skill-lab): 적 더미 하나가 뒤에서 내 옆을 달려 지나간다")]
        public KeyCode passRunKey = KeyCode.M;
        [Tooltip("궁극기 게이지 시험 (JY-skill-lab, 2판): 맵의 왕관 여섯을 내 둘레에 다시 놓는다")]
        public KeyCode crownKey = KeyCode.C;
        [Tooltip("지원 칸 (JY-skill-lab 11-22, 선두만): 팀 꼴찌를 부르는 키. 이 씬의 이모트 휠은 Y로 옮김")]
        public KeyCode supportKey = KeyCode.G;
        [Tooltip("지원 시험: 아군 더미 하나를 내 뒤 한 구간 너머에 팀 꼴찌로 세운다 (부르면 더미가 F를 대신 눌러 받음)")]
        public KeyCode supportTestKey = KeyCode.L;
        [Tooltip("지원 시험: 이 씬엔 코스가 없어 앞뒤(z)를 이 길이(m)로 끊어 구간으로 친다")]
        public float supportTestSection = 10f;

        [Header("시험 장치")]
        [Tooltip("룩만 부수는 바리케이드 (바닥 가운데)")]
        public Vector3 barricadeAt = new Vector3(-9f, 0f, 2f);
        public Vector3 barricadeFacing = Vector3.right;
        [Tooltip("룩 시험 뭉치: 내 앞 거리 (m)와 서로 간격 (m)")]
        public float clusterAhead = 3.5f;
        public float clusterGap = 0.7f;

        /// <summary>The pieces Pawn Rush promotes to, the pawn first (no king, D1). R144 (승규 님 10-15: "이제 폰 러쉬 에서도
        /// 왕 스킬 구현 할 건데 왕 캐릭터 넣어줘"): the king can be picked too, last, ahead of its skill (F does nothing yet).</summary>
        // R153 (skill plan v0.3): the king is out of Pawn Rush (its call's code stays, the rook's chain moves pieces its way).
        public static readonly PieceKind[] Pieces = { PieceKind.Pawn, PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight };

        static readonly List<string> log = new List<string>();
        const int LogLines = 9;

        LabGame game;
        PawnRushSkillFx fx;
        PawnRushSkillSfx sfx;
        GUIStyle text, small, header, button, selected, box;
        Texture2D panelTexture, barTexture;
        Rect panelRect;
        bool wroteCursor;
        int dummyPieceIndex;

        public static void Report(string line)
        {
            log.Add($"{Time.time:0.0}s  {line}");
            if (log.Count > LogLines) log.RemoveAt(0);
        }

        void Awake()
        {
            log.Clear();
            // The numbers come from the code every Play (10-25): the scene saves none of them, but an open scene kept the
            // old ones in memory, so a changed default (the bishop's bind) did not show until the scene was reopened.
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new PawnRushSkillParams()), skills);
            RagdollPawn.SkillLog += OnSkill;
            // The skill network test (R112, branch skill-network-test): the lab's tools stand down before LabGame.Start,
            // and the test screen draws the numbers.
            if (GetComponent<SkillNetTest>() == null) gameObject.AddComponent<SkillNetTest>();
        }

        void OnDestroy()
        {
            RagdollPawn.SkillLog -= OnSkill;
            RagdollPawn.SkillSendBack = null;
            RagdollPawn.TeamLosing = null;
            RagdollPawn.TeamComeback = null;
            RagdollPawn.RaceSection = null;
            PawnRushGaugeCrown.ClearAll();
        }

        // ---------------------------------------------------------------- the ultimate gauge's test (the kits' second version)

        // The test's "my team is losing" (time fills the gauge faster for it), and my team's comeback multiplier.
        bool losingTest;
        static readonly float[] ComebackBands = { 1f, 0.5f, 1.5f, 2f };
        int comebackTest;

        /// <summary>Six crowns round the start, the same on every PC (the network test places them alike).</summary>
        void PlaceStartCrowns()
        {
            PawnRushGaugeCrown.ClearAll();
            Vector2[] spots = { new Vector2(-4f, 6f), new Vector2(4f, 7f), new Vector2(-7f, 11f), new Vector2(7f, 12f), new Vector2(0f, 9f), new Vector2(0f, 15f) };
            for (int i = 0; i < spots.Length; i++)
                PawnRushGaugeCrown.Place(i, FloorAt(LabLayout.SpawnP1 + new Vector3(spots[i].x, 0f, spots[i].y)));
        }

        /// <summary>Six crowns in a ring round the piece played here (offline: the C key).</summary>
        void PlaceCrownsRound(RagdollPawn me)
        {
            PawnRushGaugeCrown.ClearAll();
            Vector3 c = me.FeetPoint;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                PawnRushGaugeCrown.Place(i, FloorAt(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 5f));
            }
            Report("왕관 여섯을 내 둘레 5 m에 놓았어요 (하나에 게이지 10%, 주우면 30초 뒤 다시 생김)");
        }

        static Vector3 FloorAt(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
            return p;
        }

        static void OnSkill(RagdollPawn pawn, string line) => Report(line);

        void Start()
        {
            game = GetComponent<LabGame>();
            if (game == null) game = FindFirstObjectByType<LabGame>();
            // The pool's queen sacrifice sends the queen back: here, the lab's own respawn (JY-skill-lab 11-20).
            RagdollPawn.SkillSendBack = p => { if (game != null) game.Respawn(p); };
            // The kits' second version's ultimate gauge: map crowns round the start, and the test's losing team and comeback
            // multiplier (the scene has no race to measure them by).
            RagdollPawn.ResetTeamGauges();
            if (game == null || !game.AutoTest) PlaceStartCrowns();
            RagdollPawn.TeamLosing = t => losingTest && Me != null && t == Me.Team;
            RagdollPawn.TeamComeback = t => Me != null && t == Me.Team ? ComebackBands[comebackTest] : 1f;
            fx = GetComponent<PawnRushSkillFx>();
            if (fx == null) fx = gameObject.AddComponent<PawnRushSkillFx>();
            // The skills' sounds (R114, 승규 님's picks on the R107 page).
            sfx = GetComponent<PawnRushSkillSfx>();
            if (sfx == null) sfx = gameObject.AddComponent<PawnRushSkillSfx>();
            // R153: a teammate rook's chain handle held out to the piece played here ("우클릭: 잡기").
            if (GetComponent<PawnRushChainPrompt>() == null) gameObject.AddComponent<PawnRushChainPrompt>();
            // JY-skill-lab: the new skills' lines and rings, and the developers' skill window (K) with the loadout this PC
            // last had (an automated run keeps the game's own: the skills as they are).
            var marks = GetComponent<PawnRushPlanMarks>();
            if (marks == null) marks = gameObject.AddComponent<PawnRushPlanMarks>();
            marks.Viewer = () => Me;
            if (game == null || !game.AutoTest) PawnRushSkillWindow.LoadSaved(skills);
            window = GetComponent<PawnRushSkillWindow>();
            if (window == null) window = gameObject.AddComponent<PawnRushSkillWindow>();
            window.Skills = skills;
            // (This scene's own online test, the skill net, does not carry the loadout yet: a room's Pawn Rush match does.)
            window.CanEdit = op => !Online;
            window.LockedNote = op => "스킬넷 온라인에서는 아직 못 바꿔요 (방에서 하는 폰 러쉬 경기에서는 돼요)";
            window.KeyName = SlotKey;
            window.Changed = OnLoadoutChanged;
            // F is the skills' key here, not the lab's interact (bells, levers, cutting a hook).
            if (game != null && skillKey == KeyCode.F) game.InteractKeyOff = true;
            // The key goes in with the rest of P1's input (R111): offline straight to the pawn, online to the host.
            if (game != null) game.SkillKey = skillKey;
            // 11-22: the support slot. G is the team leader's key here (the emote wheel the net test adds moves to Y in this
            // scene), and with no course to cut it, the scene's sections are bands of z supportTestSection long.
            if (game != null) game.SupportKey = supportKey;
            MoveEmoteWheel();
            RagdollPawn.RaceSection = p => Mathf.FloorToInt(p.Hips.position.z / Mathf.Max(1f, supportTestSection));
            RagdollPawn.ResetSupport();
            Report($"폰 러쉬 스킬 시험 준비 완료 — 왼쪽 창에서 기물을 고르고 {skillKey}");
        }

        RagdollPawn P1 => game != null && game.players.Length > 0 ? game.players[0].pawn : null;

        /// <summary>The pawn the person here plays: P1 offline, this PC's own pawn in an online match (R111).</summary>
        RagdollPawn Me => game != null ? game.LocalPawn : null;

        bool Online => game != null && game.NetworkControlled;

        void Update()
        {
            if (game == null || game.AutoTest) return;
            foreach (var pawn in RagdollPawn.All)
                if (pawn != null && pawn.PawnRushSkills == null) pawn.PawnRushSkills = skills;
            // R146: the dummies answer a king's call by themselves (standing in for the other PC's F).
            foreach (var d in game.dummies)
                if (d != null) d.SkillBot = true;
            if (game.SkillKey != skillKey) game.SkillKey = skillKey;
            if (game.SupportKey != supportKey) game.SupportKey = supportKey;
            MoveEmoteWheel();
            // R is the third slot's key (the queen's) while the piece played has a skill there; the lab's "everyone back"
            // otherwise (JY-skill-lab, the kits' second version).
            var played = Me;
            game.Ability3Key = played != null && played.SlotSkill(2) != 0 ? KeyCode.R : KeyCode.None;
            // JY-skill-lab: the dummy sent running past (RunPast) keeps its keys down until its time is up.
            if (passRunner != null)
            {
                bool run = Time.time < passRunUntil;
                passRunner.SetInput(run ? new PawnInput { move = passRunDir, aim = passRunDir, sprint = true } : new PawnInput());
                // (The kits' second version trips only a piece on a skill's dash: this run counts as one.)
                passRunner.TestDashing = run;
                if (!run) passRunner = null;
            }

            var me = Me;
            if (me == null || game.SuppressInput) return;
            if (Input.GetKeyDown(previousPieceKey)) StepPiece(me, -1);
            if (Input.GetKeyDown(nextPieceKey)) StepPiece(me, 1);
            if (!Online && Input.GetKeyDown(clusterKey)) ClusterDummies(me);
            if (!Online && Input.GetKeyDown(summonEnemyKey)) Summon(me, false);
            if (!Online && Input.GetKeyDown(summonAllyKey)) Summon(me, true);
            if (!Online && Input.GetKeyDown(rookGroundKey)) RookGround(me, false);
            if (!Online && Input.GetKeyDown(rookLedgeKey)) RookGround(me, true);
            if (!Online && Input.GetKeyDown(passRunKey)) RunPast(me);
            if (!Online && Input.GetKeyDown(crownKey)) PlaceCrownsRound(me);
            if (!Online && Input.GetKeyDown(supportTestKey)) SupportTest(me);
            // The skill key itself: LabGame.ReadInput puts it into P1's input (R111).
        }

        void LateUpdate()
        {
            // The lab locks the cursor on any click; over this panel the click is for its buttons.
            if (game == null) return;
            Vector2 mouse = Input.mousePosition;
            mouse.y = Screen.height - mouse.y;
            bool over = (Cursor.lockState != CursorLockMode.Locked && !game.PanelOpen && panelRect.Contains(mouse))
                        || (window != null && window.Open);   // the skill window: the mouse is its own while it is open
            if (over)
            {
                game.UiWantsCursor = true;
                wroteCursor = true;
            }
            else if (wroteCursor)
            {
                game.UiWantsCursor = false;
                wroteCursor = false;
            }
        }

        RagdollPawn passRunner;
        float passRunUntil;
        Vector3 passRunDir;

        /// <summary>JY-skill-lab: an enemy dummy runs up from behind and past the piece played here, a little to its right, so
        /// a pawn's en passant (plan 1-나) can be tried alone: its key within a second of the dummy going by.</summary>
        public void RunPast(RagdollPawn p1)
        {
            if (game == null || p1 == null) return;
            if (game.dummies.Count == 0) game.AddDummy();
            var d = game.dummies.Count > 0 ? game.dummies[0] : null;
            if (d == null) return;
            Vector3 ahead = Flat(p1.Facing, Vector3.forward);
            Vector3 side = Vector3.Cross(Vector3.up, ahead);
            Vector3 ground = new Vector3(p1.Hips.position.x, p1.FeetPoint.y, p1.Hips.position.z) - ahead * 5f + side * 1.1f;
            d.Team = Teams.None;
            if (d.skin != null) d.skin.sharedMaterial = game.dummyMaterial;
            d.SkillBot = true;
            d.ResetSkill();
            d.Teleport(ground + Vector3.up * (d.standHeight + 0.02f), ahead);
            passRunner = d;
            passRunDir = ahead;
            passRunUntil = Time.time + 3.2f;
            Report($"앙파상 시험: {d.DisplayName}가 뒤에서 내 오른쪽을 달려 지나감 (돌진으로 침) — 지나친 뒤 고리가 뜬 동안 {skillKey} ({passRunKey}로 다시)");
        }

        EmoteWheel emoteWheel;

        /// <summary>11-22: the emote wheel (G, added by the net test) moves to Y in this scene: G is the support's key.</summary>
        void MoveEmoteWheel()
        {
            if (game == null) return;
            if (emoteWheel == null) emoteWheel = game.GetComponent<EmoteWheel>();
            if (emoteWheel != null && supportKey != KeyCode.None && emoteWheel.key == supportKey) emoteWheel.key = KeyCode.Y;
        }

        RagdollPawn supportDummy;

        /// <summary>
        /// 11-22: the support slot's test alone (the planning session's ask: "더미 팀원 하나를 한 구간 뒤 꼴찌로 두고 받기(F)를
        /// 대신 눌러 주는 시험 키"). An ally dummy stands a section and a bit behind the piece played here as its team's last
        /// runner, facing the way ahead (+z); G calls it and the dummy answers by itself (RagdollPawn.SupportBotsAnswer, the other
        /// PC's F). With no room behind (the floor is 30 m), the piece played steps forward instead. Every section's use is
        /// given back, so it can be tried again and again. A piece with no team joins White (a support is a team's).
        /// </summary>
        public void SupportTest(RagdollPawn me)
        {
            if (game == null || me == null) return;
            if (me.Team == Teams.None)
            {
                me.Team = Teams.White;
                if (me.skin != null) me.skin.sharedMaterial = game.TeamMaterial(me.Team);
            }
            var d = supportDummy != null && game.dummies.Contains(supportDummy) ? supportDummy : null;
            if (d == null)
            {
                int before = game.dummies.Count;
                game.AddDummy();
                d = game.dummies.Count > before ? game.dummies[game.dummies.Count - 1]
                  : game.dummies.Count > 0 ? game.dummies[game.dummies.Count - 1] : null;
            }
            if (d == null) return;
            supportDummy = d;
            float band = Mathf.Max(1f, supportTestSection), half = LabLayout.MainHalfSize - 1.5f, gap = band + 3f;
            Vector3 feet = new Vector3(me.Hips.position.x, me.FeetPoint.y, me.Hips.position.z);
            float back = Mathf.Max(-half, feet.z - gap);
            if (Mathf.FloorToInt(back / band) >= Mathf.FloorToInt(feet.z / band))
            {
                // No room behind: the piece played steps forward, a section and a bit ahead of where the dummy can stand.
                float ahead = Mathf.Min(half, back + gap);
                Vector3 to = FloorAt(new Vector3(feet.x, feet.y, ahead));
                me.Teleport(to + Vector3.up * (me.standHeight + 0.02f), Vector3.forward);
                feet = to;
            }
            Vector3 ground = FloorAt(new Vector3(feet.x, feet.y, back));
            d.Team = me.Team;
            if (d.skin != null) d.skin.sharedMaterial = game.TeamMaterial(me.Team);
            d.SkillBot = true;
            d.ResetSkill();
            d.SetInput(new PawnInput());
            d.Teleport(ground + Vector3.up * (d.standHeight + 0.02f), Vector3.forward);
            RagdollPawn.ResetSupport();
            Report($"지원 시험: {d.DisplayName}가 {feet.z - back:0} m 뒤 팀 꼴찌 — {supportKey}로 부르면 더미가 {skillKey}를 대신 눌러 받아요 ({supportTestKey}로 다시)");
        }

        /// <summary>11-22: what a piece's support does, for the panel.</summary>
        static string SupportHowTo(PieceKind kind, KeyCode g, KeyCode f) => kind switch
        {
            PieceKind.Knight => $"휘파람 1초 → 빈 말이 꼴찌에게 달려가요. 꼴찌는 {f}로 올라타 {f}를 누르고 있어요 (점프 = 뛰어넘기, 이동 키 = 몰기)",
            PieceKind.Rook => $"{g}를 누르고 있는 동안 꼴찌 발밑에서 나에게로 도개교 (폭 3 m, 35 m/s). 위에 선 팀원도 같이 타요. 놓거나 밀려나면 끊겨요",
            PieceKind.Queen => $"{g}를 누르고 있는 동안 꼴찌 15 m 위에서 나에게로 빛의 줄 · 왕관 도르래로 45 m/s. 적이 줄을 치면 끊겨요",
            PieceKind.Bishop => $"드론이 꼴찌에게 날아가요. 꼴찌가 {f}로 매달리면 이동 키 · 카메라로 몰아요 (6초, {g} = 내려놓기)",
            PieceKind.Pawn => $"{g}를 누르고 있는 동안 돌 폰이 10 m마다 솟아 꼴찌를 다음 폰으로 던져요. 적이 넘어뜨린 폰은 틈",
            _ => "",
        };

        PawnRushSkillWindow window;

        /// <summary>The key of a slot in this scene: the bed's skill key, then the lab's two ability keys.</summary>
        string SlotKey(int slot) => slot == 0 ? skillKey.ToString() : slot == 1 ? "E" : slot == 2 ? "R" : "Q";

        /// <summary>The skill window changed the loadout: every skill under way and every cooldown starts over.</summary>
        void OnLoadoutChanged()
        {
            foreach (var pawn in RagdollPawn.All)
                if (pawn != null && !pawn.NetworkPuppet) pawn.ResetSkill();
            Report("스킬 구성이 바뀜 — 쓰던 스킬과 쿨타임은 처음으로");
        }

        /// <summary>The names of the skills in a piece's slots.</summary>
        static string SlotNames(RagdollPawn pawn)
        {
            // (The king's row empty: his call, as it was. With a skill in it he has slots like any piece.)
            if (pawn.Piece == PieceKind.King && !pawn.KingPlaysSlots) return RagdollPawn.SkillName(PieceKind.King);
            string names = "";
            for (int k = 0; k < pawn.SlotCount; k++)
            {
                int id = pawn.SlotSkill(k);
                if (id == 0) continue;
                names += (names.Length > 0 ? " · " : "") + PawnRushSkillBook.Name(id);
            }
            return names.Length > 0 ? names : "스킬 없음";
        }

        /// <summary>The slots still cooling down, by their skills' names.</summary>
        static string CooldownLine(RagdollPawn pawn)
        {
            if (pawn.Piece == PieceKind.King && !pawn.KingPlaysSlots)
                return pawn.SkillCooldown > 0f ? $"{RagdollPawn.SkillName(PieceKind.King)} {pawn.SkillCooldown:0.0}" : "";
            string line = "";
            for (int k = 0; k < pawn.SlotCount; k++)
            {
                int id = pawn.SlotSkill(k);
                float cd = pawn.SlotCooldown(k);
                if (id == 0 || cd <= 0f) continue;
                line += (line.Length > 0 ? " · " : "") + $"{PawnRushSkillBook.Name(id)} {cd:0.0}";
            }
            return line;
        }

        void StepPiece(RagdollPawn pawn, int step)
        {
            int i = System.Array.IndexOf(Pieces, pawn.Piece);
            if (i < 0) i = 0;
            else i = (i + step + Pieces.Length) % Pieces.Length;
            SetPiece(pawn, Pieces[i]);
        }

        void SetPiece(RagdollPawn pawn, PieceKind kind)
        {
            if (Online)
            {
                // Online the host switches pieces (it runs them): ask it; the pawn turns when its snapshot says so.
                game.AskPiece(kind);
                Report($"기물 바꾸기 요청: {ChessPieces.Name(kind)} ({RagdollPawn.SkillName(kind)}) — 방장이 바꿔요");
                return;
            }
            pawn.SetPiece(kind);
            pawn.ResetSkill();
            Report($"{pawn.DisplayName} → {ChessPieces.Name(kind)} ({SlotNames(pawn)})");
            if (kind == PieceKind.Rook) ClusterDummies(pawn);
        }

        // ---------------------------------------------------------------- the dummies

        void ResetDummies(float turn)
        {
            var spots = LabLayout.DummySpawns;
            for (int i = 0; i < game.dummies.Count; i++)
            {
                var d = game.dummies[i];
                if (d == null) continue;
                Vector3 face = Quaternion.Euler(0f, turn, 0f) * Flat(d.Facing, Vector3.back);
                Vector3 ground = i < spots.Length ? spots[i] : new Vector3(d.Hips.position.x, 0f, d.Hips.position.z);
                d.Teleport(ground + Vector3.up * (d.standHeight + 0.02f), face);
            }
            Report(turn == 0f ? "더미를 제자리에 다시 세움" : $"더미 방향 {turn:+0;-0}°");
        }

        /// <summary>Four enemy dummies packed 2 x 2 right in front of the piece, facing it: the rook's charge
        /// meets them as one lump, to see how the ragdolls knock into each other.</summary>
        public void ClusterDummies(RagdollPawn p1)
        {
            if (game == null || p1 == null) return;
            while (game.dummies.Count < 4) game.AddDummy();
            Vector3 ahead = Flat(p1.Facing, Vector3.forward);
            Vector3 side = Vector3.Cross(Vector3.up, ahead);
            Vector3 center = new Vector3(p1.Hips.position.x, 0f, p1.Hips.position.z) + ahead * clusterAhead;
            for (int i = 0; i < 4; i++)
            {
                var d = game.dummies[i];
                if (d == null) continue;
                Vector3 offset = ahead * ((i / 2 - 0.5f) * clusterGap) + side * ((i % 2 - 0.5f) * clusterGap);
                if (d.Team == p1.Team && p1.Team != Teams.None)
                {
                    d.Team = Teams.None;
                    if (d.skin != null) d.skin.sharedMaterial = game.dummyMaterial;
                }
                d.ResetSkill();
                d.SetInput(new PawnInput());
                d.Teleport(center + offset + Vector3.up * (d.standHeight + 0.02f), -ahead);
            }
            Report($"더미 4명을 앞 {clusterAhead:0.#} m에 뭉쳐 세움 (간격 {clusterGap:0.#} m, 적) — {clusterKey}로 다시");
        }

        int summoned;

        /// <summary>R153: a dummy of the other side (no team) or of this player's team, standing summonAhead in front of it
        /// and facing it, a little to one side then the other each time. Teammates grab a rook's chain handle by themselves.</summary>
        public void Summon(RagdollPawn p1, bool ally)
        {
            if (game == null || p1 == null) return;
            int before = game.dummies.Count;
            game.AddDummy();
            // The lab has room for so many dummies: past that, the oldest one is called again.
            var d = game.dummies.Count > before ? game.dummies[game.dummies.Count - 1]
                  : game.dummies.Count > 0 ? game.dummies[summoned % game.dummies.Count] : null;
            if (d == null) return;
            Vector3 ahead = Flat(p1.Facing, Vector3.forward);
            Vector3 side = Vector3.Cross(Vector3.up, ahead);
            float off = ((summoned++ % 5) - 2) * 0.9f;
            Vector3 ground = new Vector3(p1.Hips.position.x, p1.FeetPoint.y, p1.Hips.position.z) + ahead * summonAhead + side * off;
            d.Team = ally && p1.Team != Teams.None ? p1.Team : Teams.None;
            if (d.skin != null) d.skin.sharedMaterial = ally ? game.TeamMaterial(p1.Team) : game.dummyMaterial;
            d.SkillBot = true;
            d.ResetSkill();
            d.SetInput(new PawnInput());
            d.Teleport(ground + Vector3.up * (d.standHeight + 0.02f), -ahead);
            Report($"{(ally ? "아군" : "적")} 소환: {d.DisplayName} — 앞 {summonAhead:0.#} m ({(ally ? summonAllyKey : summonEnemyKey)})");
        }

        // ---------------------------------------------------------------- the rook's test ground (R153)

        /// <summary>Where the rook's test ground is: the open floor west of the start (x −14..−4, z −14..−4).</summary>
        static readonly Vector3 RookGroundStart = new Vector3(-9f, 0f, -13f);
        static readonly Vector3 RookLedgeAt = new Vector3(-12.6f, 0f, -12.6f);
        const float RookLedgeHeight = 0.8f;   // the pieces stand about 0.6 m
        Transform rookGround;

        /// <summary>
        /// R153: the rook's test ground. P1 turns rook and stands at its start facing north; the lab's four dummies take
        /// their places: an enemy 7 m straight ahead, an enemy behind a low wall ahead to the right (the chain meets the wall
        /// and winds back), a teammate 4 m to the right (it grabs the handle by itself), and a teammate at the foot of a 0.8 m
        /// ledge to the left (<paramref name="onLedge"/>: P1 stands on the ledge facing it, to pull it up, plan v0.3 §5.7).
        /// (A crate the chain passes over and the pulled piece does not cannot be had on flat floor: the pull's low path runs
        /// above the chain's line, so an obstacle in its way meets the chain first.)
        /// The ground's props are built once and stay.
        /// </summary>
        public void RookGround(RagdollPawn p1, bool onLedge)
        {
            if (game == null || p1 == null) return;
            BuildRookGround();
            if (p1.Piece != PieceKind.Rook) SetPiece(p1, PieceKind.Rook);
            p1.ResetSkill();
            while (game.dummies.Count < 4) { int n = game.dummies.Count; game.AddDummy(); if (game.dummies.Count == n) break; }
            var spots = new (Vector3 at, bool ally)[]
            {
                (new Vector3(-9f, 0f, -6f), false),     // enemy, 7 m straight ahead (the reach is 8 m)
                (new Vector3(-5.4f, 0f, -8.6f), false),  // enemy behind the low wall, ahead to the right
                (new Vector3(-5.2f, 0f, -11.2f), true),  // teammate, 4 m to the right
                (new Vector3(-12.6f, 0f, -9.8f), true),  // teammate at the foot of the ledge
            };
            for (int i = 0; i < game.dummies.Count; i++)
            {
                var d = game.dummies[i];
                if (d == null) continue;
                bool ally = i < spots.Length && spots[i].ally;
                Vector3 at = i < spots.Length ? spots[i].at : new Vector3(-10.6f + i, 0f, -6.5f);
                d.Team = ally && p1.Team != Teams.None ? p1.Team : Teams.None;
                if (d.skin != null) d.skin.sharedMaterial = ally ? game.TeamMaterial(p1.Team) : game.dummyMaterial;
                d.SkillBot = true;
                d.ResetSkill();
                d.SetInput(new PawnInput());
                Vector3 face = Flat((onLedge ? RookLedgeAt : RookGroundStart) - at, Vector3.back);
                d.Teleport(at + Vector3.up * (d.standHeight + 0.02f), face);
            }
            if (onLedge)
            {
                // On the ledge's edge, facing the teammate below.
                Vector3 top = RookLedgeAt + new Vector3(0f, RookLedgeHeight, 0.9f);
                p1.Teleport(top + Vector3.up * (p1.standHeight + 0.02f), Vector3.forward);
                Report($"룩 시험장: 높이 {RookLedgeHeight:0.#} m 단 위 — 아래 팀원에게 사슬 (F → 아래로 조준 → 좌클릭), {rookGroundKey}로 처음 자리");
            }
            else
            {
                p1.Teleport(RookGroundStart + Vector3.up * (p1.standHeight + 0.02f), Vector3.forward);
                Report($"룩 시험장: 앞 7 m 적 · 오른쪽 앞 낮은 벽 뒤 적 (사슬이 벽에 막힘) · 오른쪽 4 m 팀원 · 왼쪽 단 아래 팀원 — {rookLedgeKey}로 단 위");
            }
        }

        void BuildRookGround()
        {
            if (rookGround != null) return;
            rookGround = new GameObject("룩 시험장 (R153)").transform;
            var stone = new Material(LitMaterials.Lit) { color = new Color(0.78f, 0.72f, 0.62f) };
            var wood = new Material(LitMaterials.Lit) { color = new Color(0.55f, 0.38f, 0.22f) };
            void Block(string label, Vector3 center, Vector3 size, Material m)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = label;
                b.transform.SetParent(rookGround, false);
                b.transform.position = center;
                b.transform.localScale = size;
                b.GetComponent<MeshRenderer>().sharedMaterial = m;
            }
            // The ledge (to pull a teammate up onto), and a step up to it.
            Block("높은 단 1.4 m", RookLedgeAt + new Vector3(0f, RookLedgeHeight * 0.5f, 0f), new Vector3(2.4f, RookLedgeHeight, 2.4f), stone);
            Block("단 계단", RookLedgeAt + new Vector3(1.8f, 0.2f, -0.6f), new Vector3(1.2f, 0.4f, 1.2f), stone);
            // A low wall in the way (the chain meets it and winds back), square to the line to the enemy behind it.
            var crate = new Vector3(-6.9f, 0.45f, -10.4f);
            Block("낮은 벽 0.9 m", crate, new Vector3(1.6f, 0.9f, 0.3f), wood);
            rookGround.Find("낮은 벽 0.9 m").rotation = Quaternion.LookRotation(Flat(new Vector3(-5.4f, 0f, -8.6f) - RookGroundStart, Vector3.forward));
        }

        void ToggleFirstDummyTeam()
        {
            var p1 = P1;
            if (game.dummies.Count == 0 || game.dummies[0] == null || p1 == null) return;
            var d = game.dummies[0];
            bool ally = d.Team == p1.Team;
            d.Team = ally ? Teams.None : p1.Team;
            if (d.skin != null) d.skin.sharedMaterial = ally ? game.dummyMaterial : game.TeamMaterial(p1.Team);
            Report($"{d.DisplayName}: {(ally ? "적 (팀 없음)" : "내 팀 (부축 시험)")}");
        }

        /// <summary>R146: every dummy on this player's team, so the king has teammates to call.</summary>
        void AllDummiesMine(RagdollPawn p1)
        {
            if (p1 == null || p1.Team == Teams.None) return;
            foreach (var d in game.dummies)
            {
                if (d == null) continue;
                d.Team = p1.Team;
                if (d.skin != null) d.skin.sharedMaterial = game.TeamMaterial(p1.Team);
            }
            Report($"더미 {game.dummies.Count}명 모두 내 팀 (왕의 부름 시험)");
        }

        /// <summary>R146: the scene as one checkpoint section, or two split at x = 0 (to see the list keep to the king's).</summary>
        static void ToggleSections()
        {
            RagdollPawn.KingCallSection = RagdollPawn.KingCallSection == null
                ? (a, b) => (a.Hips.position.x >= 0f) == (b.Hips.position.x >= 0f)
                : (System.Func<RagdollPawn, RagdollPawn, bool>)null;
            Report(RagdollPawn.KingCallSection == null ? "구간: 씬 전체가 한 구간" : "구간: x = 0을 경계로 둘 (같은 쪽 팀원만 목록에)");
        }

        void KnockDummies()
        {
            foreach (var d in game.dummies)
                if (d != null) d.Knockdown("시험: 넘어뜨리기", 3f);
            Report("더미를 3초 동안 넘어뜨림 (내 팀 더미로 부축 시험)");
        }

        void CycleDummyPiece()
        {
            PieceKind[] kinds = { PieceKind.Pawn, PieceKind.Rook, PieceKind.Queen, PieceKind.Knight };
            dummyPieceIndex = (dummyPieceIndex + 1) % kinds.Length;
            foreach (var d in game.dummies)
                if (d != null) d.SetPiece(kinds[dummyPieceIndex]);
            Report($"더미 기물: {ChessPieces.Name(kinds[dummyPieceIndex])} (무게 {ChessPieces.Stats(kinds[dummyPieceIndex]).Weight:0.#})");
        }

        static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : fallback;
        }

        // ---------------------------------------------------------------- the panel

        // How each skill is used is in the skill book now (PawnRushSkillBook): the panel shows the lines of the skills in
        // the piece's slots. The king has no slots (out of Pawn Rush since R153); its call's line stays here.
        const string KingHowTo = "F = 같은 구간 팀원 목록 → 좌클릭으로 2명까지 고르기 → F로 부르기 · 우클릭 = 닫기\n불린 팀원이 3초 안에 자기 F → 12 cm 떠서 0.6초 만에 킹 뒤 칸으로 (벽 통과 · 오는 동안 무적)\n부르는 동안 킹이 맞으면 출발 안 한 팀원 취소 · 더미는 0.8초 뒤 스스로 대답";

        static string PlanPhaseName(PlanPhase phase) => phase switch
        {
            PlanPhase.Aim => "조준",
            PlanPhase.Windup => "예고",
            PlanPhase.Active => "발동",
            PlanPhase.Recovery => "후딜",
            _ => "",
        };

        /// <summary>The skill key card at the bottom right of the view, the same card as Sword Fight's F and E
        /// (<see cref="SkillKeyCard"/>): the key in the piece's colour when it can be used, grey with the cooldown
        /// draining it from the top otherwise; under the name, what it is doing, the cooldown left, or how to use it.</summary>
        void KeyCard(RagdollPawn p1)
        {
            if (p1.Piece == PieceKind.King && !p1.KingPlaysSlots)
            {
                // The king's row is empty: his call's card, as it was.
                bool running = p1.SkillStage != SkillStage.None, ready = !running && p1.SkillCooldown <= 0f;
                PawnRushSkillFx.SkillCard(SkillKeyCard.Slot(0), skillKey.ToString(), RagdollPawn.SkillName(p1.Piece),
                    SkillKeyCard.Line(running ? StageName(p1) : null, p1.SkillCooldown, $"{skillKey} 사용"), p1.Piece,
                    ready || running, p1.SkillCooldownTotal > 0f ? p1.SkillCooldown / p1.SkillCooldownTotal : 0f);
                return;
            }
            SlotCards(p1, SlotKey, "좌클릭", true, null, null);
            SupportCard(p1);
        }

        /// <summary>11-22: the support key's card over the skills' (KITS 6.10): lit while G can call the team's last runner,
        /// with "데려오면 게이지 +15%" then; otherwise why not, or the support under way. The rescues so far after it.</summary>
        void SupportCard(RagdollPawn me)
        {
            if (!RagdollPawn.HasSupport(me.Piece)) return;
            int row = 0;
            for (int k = 0; k < me.SlotCount; k++)
                if (me.SlotSkill(k) != 0) row++;
            string why = me.SupportWhyNot(out var last);
            bool busy = me.SupportMate != null || (fx != null && fx.SupportBusy(me.Team));
            bool lit = why == null && !busy;
            string g = supportKey.ToString();
            string line = busy ? (me.SupportMate != null ? $"{me.SupportMate.DisplayName} 데려오는 중" : "팀이 지원 중")
                        : lit ? $"{g} → {last.DisplayName} · " + (me.HasUltimate ? $"데려오면 게이지 +{skills.supGauge * 100f:0}%" : "데려오면 둘 다 빨라져요")
                        : why;
            if (me.Rescues > 0) line += $" · 구조 {me.Rescues}";
            PawnRushSkillFx.SkillCard(SkillKeyCard.Slot(row), g, "지원 · " + RagdollPawn.SupportName(me.Piece), line, me.Piece, lit || busy, 0f);
        }

        /// <summary>JY-skill-lab: the key cards at the bottom right, one for each of the piece's slots that holds a skill,
        /// the first lowest (with one skill, the card as it always was). A card is lit while its skill can be used or is
        /// under way; the en passant only with an enemy that has just brushed past. <paramref name="lab"/>: the test
        /// bed's wording (who the key would go to by name, and the knight's key lit only with an enemy to leap at, R153);
        /// the course's cards say what they said.</summary>
        public static void SlotCards(RagdollPawn me, System.Func<int, string> keyName, string click, bool lab, string firstName, string firstExtra)
        {
            int row = 0;
            for (int k = 0; k < me.SlotCount; k++)
            {
                int id = me.SlotSkill(k);
                if (id == 0) continue;
                bool running = me.ActiveSlot == k;
                float cd = me.SlotCooldown(k), total = me.SlotCooldownTotal(k);
                bool can = !me.SkillRunning && cd <= 0f;
                string key = keyName != null ? keyName(k) : k == 0 ? "F" : k == 1 ? "E" : k == 2 ? "R" : "Q";
                string use;
                switch (id)
                {
                    case PawnRushSkillIds.EnPassant:
                    {
                        var passing = me.PassTarget;
                        if (passing == null && !running) can = false;
                        use = lab ? (passing != null ? $"{key} → {passing.DisplayName}" : "스쳐 지나간 적 없음")
                            : passing != null ? $"{key} 발 걸기" : "스쳐 지나가는 적에게 " + key;
                        break;
                    }
                    case PawnRushSkillIds.TurnLeap:
                    {
                        var marked = me.KnightMarked;
                        if (lab && marked == null && !running) can = false;
                        use = !lab ? $"{key} 사용" : marked != null ? $"{key} → {marked.DisplayName}" : "대상 없음";
                        break;
                    }
                    case PawnRushSkillIds.TwoSteps:
                    case PawnRushSkillIds.RingShove:
                    case PawnRushSkillIds.Fork:
                    case PawnRushSkillIds.Ward:
                    case PawnRushSkillIds.Squeeze:
                    case PawnRushSkillIds.KingStep:
                        use = $"{key} 사용";
                        break;
                    case PawnRushSkillIds.BentLeap:
                        use = me.LeapCanTurn ? $"공중에서 {key} = 꺾기" : $"{key} 사용";
                        break;
                    case PawnRushSkillIds.Fianchetto:
                        use = running ? $"{click} 쏘기 · {key} 일어남" : $"{key} 자리 잡기 → {click}";
                        break;
                    case PawnRushSkillIds.AirVolley:
                        use = running ? $"{click} 발사 ({me.VolleyShotsLeft}발) · {key} 내려옴" : $"{key} 떠오름 → {click} 발사";
                        break;
                    // The kits' second version (RagdollPawn.KitTwo.cs).
                    case PawnRushSkillIds.SteeredSteps:
                    {
                        var passing = me.PassTarget;
                        use = passing != null ? (lab ? $"{key} → {passing.DisplayName} 앙파상" : $"{key} 앙파상") : $"{key} 사용 (0.5초 안에 한 번 더)";
                        break;
                    }
                    case PawnRushSkillIds.QueenGlide:
                        use = running ? $"{key} = 딱 서기" : $"이동 키 + {key}";
                        break;
                    case PawnRushSkillIds.EightLines:
                        use = running ? "이동 키 (좌우) = 줄 돌리기" : $"{key} 홀 찍기";
                        break;
                    case PawnRushSkillIds.WallCharge:
                        use = running && me.PlanPhaseNow == PlanPhase.Active ? $"{click} = 여기서 끊기" : $"{key} 겨눔 → {click} 출발";
                        break;
                    case PawnRushSkillIds.SeventhRank:
                        use = running ? $"벽 뒤에서 달리기 · {click} 모아 치기" : $"{key} 벽 세우기";
                        break;
                    case PawnRushSkillIds.Promote:
                        use = running && me.PlanPhaseNow == PlanPhase.Windup ? $"{key} = 카드 바꾸기" : $"{key} 승격";
                        break;
                    case PawnRushSkillIds.KingMarch:
                        use = running ? $"{key} = 행진 걸음" : $"{key} 행진";
                        break;
                    // The team's comeback ultimates (RagdollPawn.TeamUlts.cs).
                    case PawnRushSkillIds.PassantBang:
                    case PawnRushSkillIds.KnightTour:
                    case PawnRushSkillIds.FamilyFork:
                    case PawnRushSkillIds.GreekGift:
                    case PawnRushSkillIds.RankWall:
                    case PawnRushSkillIds.GeneralCall:
                        use = $"{key} 궁극기 (×{RagdollPawn.Comeback(me.Team):0.#})";
                        break;
                    // The six driven ultimates (RagdollPawn.DriveUlts.cs): what the keys do then is under the time bar.
                    case PawnRushSkillIds.PromotionRun:
                    case PawnRushSkillIds.Nightmare:
                    case PawnRushSkillIds.GreatDiagonal:
                    case PawnRushSkillIds.RookRoll:
                    case PawnRushSkillIds.QueenDescent:
                    case PawnRushSkillIds.KingWalk:
                        use = running ? "궁극기 중 (화면 가운데 아래 안내)" : $"{key} 궁극기 (×{RagdollPawn.Comeback(me.Team):0.#})";
                        break;
                    case PawnRushSkillIds.QueenDrop:
                        use = running ? $"{click} = 다음 무리" : $"{key} 궁극기 (×{RagdollPawn.Comeback(me.Team):0.#})";
                        break;
                    // The set 3판 (RagdollPawn.SetThree.cs).
                    case PawnRushSkillIds.Bolas:
                        use = me.BolasChopOpen ? $"{click} = 뒷목 치기!" : me.BolasPulling ? $"끌어오는 중 · 머리가 반짝이면 {click}" : $"{key} 볼라스 (조준점 쪽)";
                        break;
                    case PawnRushSkillIds.HighStomp:
                        use = !running ? $"{key} 하이점프"
                            : me.PlanTarget != null ? $"공중에서 {key} = 내려찍기" + (lab ? $" → {me.PlanTarget.DisplayName}" : "")
                            : $"공중에서 {key} = 곧장 내려오기";
                        break;
                    case PawnRushSkillIds.HolySwords:
                        use = $"{key} 성검 (적 모두의 곁에)";
                        break;
                    case PawnRushSkillIds.StoneThrow:
                        use = running && me.PlanPhaseNow == PlanPhase.Windup ? "카메라로 겨누는 중" : $"{key} 떠올라 돌멩이";
                        break;
                    case PawnRushSkillIds.HolyWater:
                        use = $"{key} 성수병 (10 m 앞)";
                        break;
                    case PawnRushSkillIds.RollShort:
                        use = running && me.RollingShort ? $"{key} = 쿵 일어서기 · 이동 키 = 조금 꺾기" : $"{key} 굴러가기";
                        break;
                    case PawnRushSkillIds.TowerTrap:
                        use = $"{key} 땅 가르기 → 탑";
                        break;
                    case PawnRushSkillIds.QueenStar:
                        use = $"{key} 팔각별";
                        break;
                    case PawnRushSkillIds.CheckChain:
                        use = running && me.PlanPhaseNow == PlanPhase.Recovery ? $"{key} = 다음 체크 ({me.CheckChainDone}/{me.PawnRushSkills.ccMax})" : $"{key} 체크 (앞의 적)";
                        break;
                    default:
                        use = $"{key} 조준 → {click}";
                        break;
                }
                string name = PawnRushSkillBook.Name(id);
                if (row == 0)
                {
                    if (!string.IsNullOrEmpty(firstName)) name = $"{firstName} · {name}";
                    if (!string.IsNullOrEmpty(firstExtra)) use += firstExtra;
                }
                if (me.UltOnGauge(k))
                {
                    // The kits' second version: an ultimate's card is its gauge (lit when full), not a cooldown.
                    bool ready = me.UltReady;
                    PawnRushSkillFx.SkillCard(SkillKeyCard.Slot(row), key, name,
                        running ? StageName(me) : ready ? use : $"게이지 {me.UltGauge * 100f:0}%", me.Piece,
                        (ready && !me.SkillRunning) || running, ready ? 0f : 1f - me.UltGauge);
                    row++;
                    continue;
                }
                PawnRushSkillFx.SkillCard(SkillKeyCard.Slot(row), key, name,
                    SkillKeyCard.Line(running ? StageName(me) : null, cd, use), me.Piece,
                    can || running, total > 0f ? Mathf.Clamp01(cd / total) : 0f);
                row++;
            }
        }

        public static string StageName(RagdollPawn pawn)
        {
            if (pawn.PlanBusy) return !string.IsNullOrEmpty(pawn.SkillDetail) ? pawn.SkillDetail : PlanPhaseName(pawn.PlanPhaseNow);
            if (pawn.SkillStage == SkillStage.None) return pawn.SkillCooldown > 0f ? "쿨타임" : "준비됨";
            if (!string.IsNullOrEmpty(pawn.SkillDetail)) return pawn.SkillDetail;
            // An online client's puppet has the stage but not the host's wording (R111).
            switch (pawn.SkillStage)
            {
                case SkillStage.Windup: return pawn.SkillAiming ? "조준" : "예고";
                case SkillStage.Active: return "발동";
                case SkillStage.Link: return "연결 (F 한 번 더)";
                case SkillStage.Recovery: return "후딜";
                default: return pawn.SkillStage.ToString();
            }
        }

        void EnsureStyles()
        {
            if (text != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 13);
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13, richText = true, wordWrap = true };
            text.normal.textColor = Color.white;
            small = new GUIStyle(text) { fontSize = 12 };
            small.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            header = new GUIStyle(text) { fontSize = 15, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 12 };
            selected = new GUIStyle(button) { fontStyle = FontStyle.Bold };
            selected.normal.textColor = selected.hover.textColor = new Color(1f, 0.85f, 0.3f);
            panelTexture = Solid(new Color(0.05f, 0.07f, 0.12f, 0.82f));
            barTexture = Solid(Color.white);
            box = new GUIStyle { padding = new RectOffset(10, 10, 8, 8) };
            box.normal.background = panelTexture;
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void OnGUI()
        {
            if (game == null || game.AutoTest || game.PanelOpen) return;
            var p1 = Me;
            if (p1 == null) return;
            EnsureStyles();
            DrawOverheads();
            KeyCard(p1);
            if (LabGame.CleanView) return;

            const float width = 360f;
            panelRect = new Rect(8f, 44f, width, Mathf.Min(Screen.height - 180f, 640f));
            GUILayout.BeginArea(panelRect, box);
            GUILayout.Label("폰 러쉬 스킬 · 네트워크 시험", header);
            GUILayout.Label($"기물 바꾸기: 버튼 또는 {previousPieceKey} ◀ ▶ {nextPieceKey}   ·   스킬: <b>{skillKey}</b>", small);
            GUILayout.BeginHorizontal();
            foreach (var kind in Pieces)
            {
                string label = kind == PieceKind.Pawn ? "폰(기본)" : ChessPieces.Name(kind);
                if (GUILayout.Button(label, p1.Piece == kind ? selected : button, GUILayout.Height(26f))) SetPiece(p1, kind);
            }
            GUILayout.EndHorizontal();
            // JY-skill-lab: the developers' skill tree (PawnRushSkillWindow), and the skills in this piece's slots.
            GUILayout.BeginHorizontal();
            if (window != null && GUILayout.Button(window.Open ? $"스킬트리 닫기 ({window.toggleKey})" : $"스킬트리 열기 ({window.toggleKey})", window.Open ? selected : button, GUILayout.Height(24f), GUILayout.Width(150f)))
                window.Toggle();
            GUILayout.Label($"  방식 {skills.loadout.system}안" + (skills.loadout.system == PawnRushLoadout.Grow ? $" · 단계 {p1.SkillStageLevel}" : ""), small);
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label($"<b>{ChessPieces.Name(p1.Piece)}</b>   (팀 {Teams.Name(p1.Team)}, 무게 {p1.Weight:0.#})", text);
            if (p1.Piece == PieceKind.King && !p1.KingPlaysSlots)
            {
                GUILayout.Label($"<b>{skillKey} · {RagdollPawn.SkillName(PieceKind.King)}</b>", text);
                GUILayout.Label(KingHowTo, small);
            }
            else
            {
                bool anySkill = false;
                for (int k = 0; k < p1.SlotCount; k++)
                {
                    int id = p1.SlotSkill(k);
                    if (id == 0) continue;
                    anySkill = true;
                    // (An ultimate's cooldown is its gauge: how full it is, "!!" when full. The kits' second version's gauge
                    // is its own: RagdollPawn.UltGauge.cs.)
                    string wait = p1.UltOnGauge(k)
                        ? (p1.UltReady ? "   <color=#fc4>!! 게이지 가득</color>" : $"   게이지 {p1.UltGauge * 100f:0}%")
                        : PawnRushSkillIds.Ultimate(id) && !skills.realGauge
                        ? (p1.SlotCooldown(k) > 0f ? $"   게이지 {100f * (1f - p1.SlotCooldown(k) / Mathf.Max(0.01f, p1.SlotCooldownTotal(k))):0}%" : "   <color=#fc4>!!</color>")
                        : p1.SlotCooldown(k) > 0f ? $"   쿨 {p1.SlotCooldown(k):0.0}초" : "";
                    GUILayout.Label($"<b>{SlotKey(k)} · {PawnRushSkillBook.Name(id)}</b>" + wait
                                    + (p1.SlotKept(k) ? "   <color=#6cf>미리 누름</color>" : ""), text);
                    GUILayout.Label(PawnRushSkillBook.How(id), small);
                }
                if (!anySkill) GUILayout.Label($"이 기물의 칸이 비어 있어요. 스킬트리({(window != null ? window.toggleKey : KeyCode.K)})에서 넣어요", small);
            }
            if (RagdollPawn.HasSupport(p1.Piece))
            {
                // 11-22: the support slot (leader only).
                string why = p1.SupportWhyNot(out var lastOne);
                GUILayout.Label($"<b>{supportKey} · 지원 {RagdollPawn.SupportName(p1.Piece)}</b>   "
                                + (why == null ? $"<color=#4fd>→ {lastOne.DisplayName}</color>" : why)
                                + (p1.Rescues > 0 ? $"   구조 {p1.Rescues}" : ""), text);
                GUILayout.Label(SupportHowTo(p1.Piece, supportKey, skillKey), small);
                if (!Online)
                    GUILayout.Label($"선두만 · 꼴찌가 다른 구간 ({supportTestSection:0} m 띠)일 때 · 구간마다 한 번 · {supportTestKey}: 아군 더미를 한 구간 뒤 꼴찌로 (더미가 {skillKey}를 대신 눌러 받아요) · 이모트 휠은 Y", small);
            }

            GUILayout.Space(2f);
            GUILayout.Label($"단계: <b>{StageName(p1)}</b>" + (p1.PlanBusy ? $"  {p1.PlanTime:0.00}초" : p1.SkillStage != SkillStage.None ? $"  {p1.SkillStageTime:0.00}초" : ""), text);
            float cd = p1.SkillCooldownTotal > 0f ? p1.SkillCooldown / p1.SkillCooldownTotal : 0f;
            Bar(1f - cd, cd > 0f ? new Color(0.45f, 0.5f, 0.6f) : new Color(0.3f, 0.85f, 1f),
                cd > 0f ? $"쿨타임 {p1.SkillCooldown:0.0}초" : "사용 가능");
            GUILayout.Label(skills.testCooldown > 0f ? $"시험용 쿨타임 {skills.testCooldown:0.#}초 (기획 쿨은 Inspector 시험용 쿨을 0으로)" : "기획 쿨타임 (v0.3)", small);
            string extra = "";
            if (p1.GetUpGuardLeft > 0f) extra += $"기상 보호 {p1.GetUpGuardLeft:0.0}초   ";
            if (!string.IsNullOrEmpty(extra)) GUILayout.Label(extra, small);
            GUILayout.Label(skills.loadout.system == PawnRushLoadout.Many
                ? "3안: 스킬을 넣은 둘째 · 셋째 · 넷째 칸은 E · R · Q가 스킬 키 (빈 칸이면 랩의 원래 기능, R은 모두 리스폰)"
                : "1안 · 2안: F는 일반 칸, Q는 궁극 칸 (넣었을 때). E와 마우스는 랩의 원래 기능 그대로", small);
            if (skills.loadout.system == PawnRushLoadout.Grow)
            {
                // The set 2안-a: the middlegame shapes, or (after the second mini-game on the course) the endgame shapes.
                bool end = true;
                for (int row = 0; row < PawnRushLoadout.Rows; row++)
                    if (skills.loadout.stages[row] < 2) end = false;
                if (!Online && GUILayout.Button(end ? "모양: 엔드게임 (누르면 미들게임으로)" : "모양: 미들게임 (누르면 엔드게임으로)", end ? selected : button))
                {
                    for (int row = 0; row < PawnRushLoadout.Rows; row++) skills.loadout.SetStage(row, end ? 1 : 2);
                    OnLoadoutChanged();
                    Report(end ? "모든 기물이 미들게임 모양으로" : "모든 기물이 엔드게임 모양으로 (코스에서는 팀이 미니게임 2를 끝냈을 때)");
                }
            }
            if (p1.HasUltimate)
            {
                // The kits' second version's ultimate gauge (RagdollPawn.UltGauge.cs).
                GUILayout.BeginHorizontal();
                if (!Online && GUILayout.Button("게이지 가득", button)) p1.FillGauge();
                if (!Online && GUILayout.Button($"왕관 다시 놓기 ({crownKey})", button)) PlaceCrownsRound(p1);
                if (GUILayout.Button(losingTest ? "지는 팀: 시간 2배" : "지는 팀: 아님", losingTest ? selected : button)) losingTest = !losingTest;
                if (GUILayout.Button($"역전 배율 ×{ComebackBands[comebackTest]:0.#}", comebackTest != 0 ? selected : button))
                    comebackTest = (comebackTest + 1) % ComebackBands.Length;
                GUILayout.EndHorizontal();
                GUILayout.Label(skills.testCooldown > 0f ? "팀 게이지: 시험용 쿨이 켜져 있어 늘 가득 (쿨: 기획 쿨로 바꾸면 왕관 · 시간 · 팀원이 당해서 넘어짐으로 참)"
                    : $"{Teams.Name(p1.Team)}팀 게이지 {p1.UltGauge * 100f:0}% · 왕관 4% · 시간 1초 0.1% · 팀원이 적 스킬에 넘어짐 1.5% (한 사람 10초에 6%까지)", small);
            }
            if (fx != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(fx.effects ? "이펙트 켜짐" : "이펙트 꺼짐", fx.effects ? selected : button)) fx.effects = !fx.effects;
                if (GUILayout.Button(fx.hitStop ? "멈춤 켜짐" : "멈춤 꺼짐", fx.hitStop ? selected : button)) fx.hitStop = !fx.hitStop;
                if (GUILayout.Button(fx.shake ? "흔들기 켜짐" : "흔들기 꺼짐", fx.shake ? selected : button)) fx.shake = !fx.shake;
                if (sfx != null && GUILayout.Button(sfx.sound ? "소리 켜짐" : "소리 꺼짐", sfx.sound ? selected : button)) sfx.sound = !sfx.sound;
                GUILayout.EndHorizontal();
            }
            // JY-skill-lab 11-20: the hand feel's devices on and off, and the test's flat cooldown or the design's own.
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(skills.feel ? "손맛 장치 켜짐" : "손맛 장치 꺼짐", skills.feel ? selected : button)) skills.feel = !skills.feel;
            if (GUILayout.Button(skills.testCooldown > 0f ? $"쿨: 시험용 {skills.testCooldown:0.#}초" : "쿨: 기획 쿨", button))
            {
                if (skills.testCooldown > 0f)
                {
                    keptTestCooldown = skills.testCooldown;
                    skills.testCooldown = 0f;
                }
                else skills.testCooldown = keptTestCooldown > 0f ? keptTestCooldown : 2f;
            }
            GUILayout.EndHorizontal();

            if (Online)
            {
                // Online (R111): the host runs every skill; the dummies are offline only. Who hosts and the link's numbers
                // are on the test screen at the top right (R112).
                GUILayout.Space(6f);
                GUILayout.Label("기물 바꾸기는 방장에게 요청해요 (Z · X 또는 위 버튼). 지연 넣기: F12", small);
                GUILayout.Space(6f);
                GUILayout.Label("<b>최근 결과</b>" + (game.NetworkHost ? "" : " (참가자 화면에는 내 요청만 나와요)"), text);
                for (int i = log.Count - 1; i >= 0; i--) GUILayout.Label(log[i], small);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label("<b>더미</b> (팀 없는 더미 = 적)", text);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("＋", button, GUILayout.Width(36f))) { game.AddDummy(); Report("더미 추가"); }
            if (GUILayout.Button("－", button, GUILayout.Width(36f))) { game.RemoveDummy(); Report("더미 빼기"); }
            if (GUILayout.Button("다시 세우기", button)) ResetDummies(0f);
            if (GUILayout.Button("방향 +45°", button)) ResetDummies(45f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool ally = game.dummies.Count > 0 && game.dummies[0] != null && game.dummies[0].Team == p1.Team;
            if (GUILayout.Button(ally ? "더미1: 내 팀" : "더미1: 적", button)) ToggleFirstDummyTeam();
            if (GUILayout.Button("모두 넘어뜨리기", button)) KnockDummies();
            if (GUILayout.Button("더미 기물 바꾸기", button)) CycleDummyPiece();
            GUILayout.EndHorizontal();
            if (GUILayout.Button($"룩 시험: 앞에 4명 뭉치기 ({clusterKey})", button)) ClusterDummies(p1);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"적 소환 ({summonEnemyKey})", button)) Summon(p1, false);
            if (GUILayout.Button($"아군 소환 ({summonAllyKey})", button)) Summon(p1, true);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"룩 시험장 ({rookGroundKey})", button)) RookGround(p1, false);
            if (GUILayout.Button($"단 위로 ({rookLedgeKey})", button)) RookGround(p1, true);
            GUILayout.EndHorizontal();
            if (p1.HasSkill(PawnRushSkillIds.EnPassant) && GUILayout.Button($"앙파상 시험: 적이 내 옆을 달려 지나감 ({passRunKey})", button)) RunPast(p1);
            if (p1.Piece == PieceKind.King)
            {
                // R146: the king's call needs teammates (and, to see the rule, two sections).
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("더미 모두 내 팀 (부름 시험)", button)) AllDummiesMine(p1);
                if (GUILayout.Button(RagdollPawn.KingCallSection == null ? "구간: 하나" : "구간: x=0으로 둘", button)) ToggleSections();
                GUILayout.EndHorizontal();
            }
            if (Cursor.lockState == CursorLockMode.Locked) GUILayout.Label("버튼을 누르려면 Esc로 마우스를 풀어요", small);

            GUILayout.Space(6f);
            GUILayout.Label("<b>최근 결과</b>", text);
            for (int i = log.Count - 1; i >= 0; i--) GUILayout.Label(log[i], small);
            GUILayout.EndArea();
        }

        void Bar(float fill, Color color, string label)
        {
            var r = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(r, barTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), barTexture);
            GUI.color = Color.white;
            var dark = new GUIStyle(small);
            dark.normal.textColor = fill >= 0.999f ? new Color(0.02f, 0.1f, 0.16f) : Color.white;
            GUI.Label(new Rect(r.x + 6f, r.y, r.width, r.height), label, dark);
        }

        /// <summary>A short line over each piece: its skill state (P1) or what last hit it (the dummies).</summary>
        void DrawOverheads()
        {
            var cam = game.labCamera != null ? game.labCamera.Cam : null;
            if (cam == null) return;
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(pawn.Hips.position + Vector3.up * 1.1f);
                if (sp.z <= 0f) continue;
                string line;
                if (pawn == Me)
                    line = pawn.SkillStage != SkillStage.None || pawn.PlanBusy ? StageName(pawn) : CooldownLine(pawn);
                else
                {
                    line = pawn.SkillBound ? "묶임" : pawn.State == PawnState.Ragdoll ? "넘어짐" : pawn.Staggered ? "휘청" : pawn.GetUpGuardLeft > 0f ? "기상 보호" : "";
                    // JY-skill-lab: the new skills' holds on it.
                    if (pawn.Squashed) line = "납작" + (line.Length > 0 ? " · " + line : "");
                    if (pawn.Warded) line = $"호위 {pawn.WardLeft:0.0}" + (line.Length > 0 ? " · " + line : "");
                    if (pawn.Pinned) line = $"{(pawn.Tied ? "토끼" : "핀")} {pawn.PinLeft:0.0}" + (line.Length > 0 ? " · " + line : "");
                    // The idea book's bundle A (RagdollPawn.BookSkills.cs).
                    if (pawn.Touched) line = $"터치 {pawn.TouchLeft:0.0}" + (line.Length > 0 ? " · " + line : "");
                    if (pawn.Numb) line = $"손 저림 {pawn.NumbLeft:0.0}" + (line.Length > 0 ? " · " + line : "");
                    if (pawn.Ghosted) line = $"유령 {pawn.GhostLeft:0.0}" + (line.Length > 0 ? " · " + line : "");
                    // The kits' basic row (RagdollPawn.KitSkills.cs).
                    if (pawn.Slowed) line = $"발 무거움 {pawn.SlowLeft:0.0}" + (line.Length > 0 ? " · " + line : "");
                    if (pawn.CheckedBy() != null) line = "체크!" + (line.Length > 0 ? " · " + line : "");
                    // R146: a king's call to it, and its trip in.
                    if (pawn.KingCarried) line = "오는 중 · 무적";
                    else if (pawn.KingCaller != null) line = $"부름 {pawn.KingCallLeft:0.0}";
                    if (pawn.Piece != PieceKind.Pawn) line = ChessPieces.Name(pawn.Piece) + (line.Length > 0 ? " · " + line : "");
                }
                if (string.IsNullOrEmpty(line)) continue;
                var size = small.CalcSize(new GUIContent(line));
                var r = new Rect(sp.x - size.x * 0.5f, Screen.height - sp.y - size.y, size.x, size.y);
                GUI.color = new Color(0f, 0f, 0f, 0.7f);
                GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), line, small);
                GUI.color = Color.white;
                GUI.Label(r, line, small);
            }
        }
    }
}
