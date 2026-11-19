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

        void OnDestroy() => RagdollPawn.SkillLog -= OnSkill;

        static void OnSkill(RagdollPawn pawn, string line) => Report(line);

        void Start()
        {
            game = GetComponent<LabGame>();
            if (game == null) game = FindFirstObjectByType<LabGame>();
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
            // JY-skill-lab: the dummy sent running past (RunPast) keeps its keys down until its time is up.
            if (passRunner != null)
            {
                bool run = Time.time < passRunUntil;
                passRunner.SetInput(run ? new PawnInput { move = passRunDir, aim = passRunDir, sprint = true } : new PawnInput());
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
            Report($"앙파상 시험: {d.DisplayName}가 뒤에서 내 오른쪽을 달려 지나감 — 지나친 뒤 1초 안에 {skillKey} ({passRunKey}로 다시)");
        }

        PawnRushSkillWindow window;

        /// <summary>The key of a slot in this scene: the bed's skill key, then the lab's two ability keys.</summary>
        string SlotKey(int slot) => slot == 0 ? skillKey.ToString() : slot == 1 ? "E" : "Q";

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
            if (pawn.Piece == PieceKind.King) return RagdollPawn.SkillName(PieceKind.King);
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
            if (pawn.Piece == PieceKind.King)
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
            if (p1.Piece == PieceKind.King)
            {
                // The king has no slots: its call's card, as it was.
                bool running = p1.SkillStage != SkillStage.None, ready = !running && p1.SkillCooldown <= 0f;
                PawnRushSkillFx.SkillCard(SkillKeyCard.Slot(0), skillKey.ToString(), RagdollPawn.SkillName(p1.Piece),
                    SkillKeyCard.Line(running ? StageName(p1) : null, p1.SkillCooldown, $"{skillKey} 사용"), p1.Piece,
                    ready || running, p1.SkillCooldownTotal > 0f ? p1.SkillCooldown / p1.SkillCooldownTotal : 0f);
                return;
            }
            SlotCards(p1, SlotKey, "좌클릭", true, null, null);
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
                string key = keyName != null ? keyName(k) : k == 0 ? "F" : k == 1 ? "E" : "Q";
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
                        use = $"{key} 사용";
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
            if (p1.Piece == PieceKind.King)
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
                    GUILayout.Label($"<b>{SlotKey(k)} · {PawnRushSkillBook.Name(id)}</b>" + (p1.SlotCooldown(k) > 0f ? $"   쿨 {p1.SlotCooldown(k):0.0}초" : ""), text);
                    GUILayout.Label(PawnRushSkillBook.How(id), small);
                }
                if (!anySkill) GUILayout.Label($"이 기물의 칸이 비어 있어요. 스킬트리({(window != null ? window.toggleKey : KeyCode.K)})에서 넣어요", small);
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
                ? "3안: 스킬을 넣은 둘째 · 셋째 칸은 E · Q가 스킬 키 (빈 칸이면 랩의 원래 기능)"
                : "E · Q · 마우스는 랩의 원래 기능 그대로 (이 씬에서 F는 스킬만)", small);
            if (fx != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(fx.effects ? "이펙트 켜짐" : "이펙트 꺼짐", fx.effects ? selected : button)) fx.effects = !fx.effects;
                if (GUILayout.Button(fx.hitStop ? "멈춤 켜짐" : "멈춤 꺼짐", fx.hitStop ? selected : button)) fx.hitStop = !fx.hitStop;
                if (GUILayout.Button(fx.shake ? "흔들기 켜짐" : "흔들기 꺼짐", fx.shake ? selected : button)) fx.shake = !fx.shake;
                if (sfx != null && GUILayout.Button(sfx.sound ? "소리 켜짐" : "소리 꺼짐", sfx.sound ? selected : button)) sfx.sound = !sfx.sound;
                GUILayout.EndHorizontal();
            }

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
                    // JY-skill-lab: plan 1-나's holds on it.
                    if (pawn.Pinned) line = $"핀 {pawn.PinLeft:0.0}" + (line.Length > 0 ? " · " + line : "");
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
