using System.Collections;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Where the Queen of the Hill skill test bed puts its test pieces (the bed builds them; the probe and the film
    /// stage their runs on them).</summary>
    public static class QueenHillLayout
    {
        /// <summary>The hill: three stacked tiers of 1.5 m board squares, 0.9 m each (the knight may leap one), the
        /// promotion pad on top. In the lab's free north-west quarter.</summary>
        public static readonly Vector3 HillCenter = new Vector3(-8.5f, 0f, 5.5f);
        public static readonly float[] TierSize = { 9f, 6f, 3f };
        public const float TierHeight = 0.9f;
        /// <summary>A stone wall that stops the queen's slash (east of the middle).</summary>
        public static readonly Vector3 WallCenter = new Vector3(6.6f, 0f, 3f);
        public static readonly Vector3 WallSize = new Vector3(0.7f, 1.6f, 3.2f);

        public static float TierTop(int tier) => (tier + 1) * TierHeight;
        public static float TierHalf(int tier) => TierSize[tier] * 0.5f;
    }

    /// <summary>
    /// The Queen of the Hill skill test bed (scene QueenOfTheHill_SkillTest, a copy of RagdollTest; R89). It gives every
    /// pawn in the lab the Queen of the Hill skills (design doc §7 with 승규's picks 1.B 2.A 3.B 4.B 5.B 6.B), feeds P1's
    /// skill key, builds a three-tier hill and a stone wall to try them on, and draws a panel on the left to switch P1
    /// between the six pieces and to set the dummies up as allies or enemies. Every skill is on F for now (temporary):
    /// in this scene F is not the lab's interact key. The queen, the rook and the knight aim on F and go on the left
    /// click; the bishop rises on F and throws on the left click. A dummy asked to castle says yes by itself.
    /// The effects are the "잉크 테두리 장난감 체스" design (A): QueenHillSkillFx.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class QueenHillSkillBed : MonoBehaviour
    {
        public QueenHillSkillParams skills = new QueenHillSkillParams();

        [Header("키 (임시, 나중에 다시 정함)")]
        public KeyCode skillKey = KeyCode.F;
        public KeyCode previousPieceKey = KeyCode.Z;
        public KeyCode nextPieceKey = KeyCode.X;

        [Header("시험 도우미 (R93)")]
        [Tooltip("킹이 F를 누르면 아군 폰 3명이 앞에 서고 5 m 앞의 적이 검격으로 공격 · 룩이 F를 누르면 올라갈 자리(한 층 위)에 아군 1명(기물 무작위)")]
        public bool helpers = true;

        /// <summary>The probe's runs leave the helpers alone unless a run asks for them.</summary>
        public static bool HelpersForProbe;
        /// <summary>The next rook helper's ally piece (a film picks the colours it shows); random otherwise.</summary>
        public static PieceKind? NextRookPartner;

        /// <summary>The six pieces of a Queen of the Hill team, the pawn first.</summary>
        public static readonly PieceKind[] Pieces = { PieceKind.Pawn, PieceKind.King, PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight };

        /// <summary>Pieces kept on the Pawn Rush skills (the probe's charging rook the pawn dodges, 6.B).</summary>
        public static readonly HashSet<RagdollPawn> PawnRushOnly = new HashSet<RagdollPawn>();

        static readonly List<string> log = new List<string>();
        const int LogLines = 9;

        LabGame game;
        QueenHillSkillFx fx;
        QueenHillSkillSfx sfx;
        // The machinery's own switch (stages, dashes, the aim's clicks): the Pawn Rush skill object, its cooldown kept
        // in step with ours. The Pawn Rush numbers in it are only used by PawnRushOnly pieces.
        readonly PawnRushSkillParams machinery = new PawnRushSkillParams();
        Transform built;
        GUIStyle text, small, header, button, selected, box;
        Texture2D panelTexture, barTexture;
        Rect panelRect;
        bool wroteCursor;

        public static void Report(string line)
        {
            log.Add($"{Time.time:0.0}s  {line}");
            if (log.Count > LogLines) log.RemoveAt(0);
        }

        public LabGame Game => game;
        public RagdollPawn P1 => game != null && game.players.Length > 0 ? game.players[0].pawn : null;

        /// <summary>The pawn the person here plays: P1 offline, this PC's own pawn in an online match (R111).</summary>
        public RagdollPawn Me => game != null ? game.LocalPawn : null;

        bool Online => game != null && game.NetworkControlled;

        void Awake()
        {
            log.Clear();
            PawnRushOnly.Clear();
            RagdollPawn.SkillLog += OnSkill;
            RagdollPawn.QueenHillFx += OnSkillMoment;
            // The skill network test (R112, branch skill-network-test): the lab's tools stand down before LabGame.Start,
            // and the test screen draws the numbers.
            if (GetComponent<SkillNetTest>() == null) gameObject.AddComponent<SkillNetTest>();
        }

        void OnDestroy()
        {
            RagdollPawn.SkillLog -= OnSkill;
            RagdollPawn.QueenHillFx -= OnSkillMoment;
            if (built != null) Destroy(built.gameObject);
        }

        static void OnSkill(RagdollPawn pawn, string line) => Report(line);

        void Start()
        {
            game = GetComponent<LabGame>();
            if (game == null) game = FindFirstObjectByType<LabGame>();
            built = new GameObject("Queen of the Hill skill test").transform;
            BuildHill(built);
            BuildWall(built);
            fx = GetComponent<QueenHillSkillFx>();
            if (fx == null) fx = gameObject.AddComponent<QueenHillSkillFx>();
            sfx = GetComponent<QueenHillSkillSfx>();
            if (sfx == null) sfx = gameObject.AddComponent<QueenHillSkillSfx>();
            if (game != null && skillKey == KeyCode.F) game.InteractKeyOff = true;
            // The key goes in with the rest of P1's input (R111): offline straight to the pawn, online to the host.
            if (game != null) game.SkillKey = skillKey;
            Report($"퀸 오브 더 힐 스킬 시험 준비 완료 — 왼쪽 창에서 기물을 고르고 {skillKey}");
        }

        void Update()
        {
            if (game == null || game.AutoTest) return;
            machinery.testCooldown = skills.testCooldown;
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null) continue;
                pawn.PawnRushSkills = machinery;
                pawn.QueenHillSkills = PawnRushOnly.Contains(pawn) ? null : skills;
                pawn.SkillBot = game.dummies.Contains(pawn);
            }
            if (game.SkillKey != skillKey) game.SkillKey = skillKey;

            var p1 = P1;
            if (p1 != null && !Online)
            {
                // The rook starts its aim: an ally to swap with goes up where the rook would climb to.
                if (p1.Piece == PieceKind.Rook && lastPiece == PieceKind.Rook && lastStage == SkillStage.None
                    && p1.SkillStage == SkillStage.Windup && HelpersOn) RookHelper(p1);
                lastStage = p1.SkillStage;
                lastPiece = p1.Piece;
            }
            DriveBot();
            var me = Me;
            if (me == null || game.SuppressInput) return;
            if (Input.GetKeyDown(previousPieceKey)) StepPiece(me, -1);
            if (Input.GetKeyDown(nextPieceKey)) StepPiece(me, 1);
            // The skill key itself: LabGame.ReadInput puts it into P1's input (R111). Online it also answers a castling ask.
        }

        void LateUpdate()
        {
            // The lab locks the cursor on any click; over this panel the click is for its buttons.
            if (game == null) return;
            Vector2 mouse = Input.mousePosition;
            mouse.y = Screen.height - mouse.y;
            bool over = Cursor.lockState != CursorLockMode.Locked && !game.PanelOpen && panelRect.Contains(mouse);
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

        void StepPiece(RagdollPawn pawn, int step)
        {
            int i = System.Array.IndexOf(Pieces, pawn.Piece);
            i = i < 0 ? 0 : (i + step + Pieces.Length) % Pieces.Length;
            SetPiece(pawn, Pieces[i]);
        }

        public void SetPiece(RagdollPawn pawn, PieceKind kind)
        {
            if (Online && pawn == Me)
            {
                // Online the host switches pieces (it runs them): ask it; the pawn turns when its snapshot says so.
                game.AskPiece(kind);
                Report($"기물 바꾸기 요청: {ChessPieces.Name(kind)} ({RagdollPawn.QueenHillSkillName(kind)}) — 방장이 바꿔요");
                return;
            }
            pawn.SetPiece(kind);
            pawn.ResetSkill();
            Report($"{pawn.DisplayName} → {ChessPieces.Name(kind)} ({RagdollPawn.QueenHillSkillName(kind)})");
        }

        // ---------------------------------------------------------------- the dummies

        /// <summary>Make a dummy an ally of <paramref name="of"/> (its team and colour) or an enemy (no team, grey).</summary>
        public void SetSide(RagdollPawn dummy, RagdollPawn of, bool ally)
        {
            if (dummy == null || game == null) return;
            dummy.Team = ally && of != null ? of.Team : Teams.None;
            if (dummy.skin != null) dummy.skin.sharedMaterial = ally && of != null ? game.TeamMaterial(of.Team) : game.dummyMaterial;
        }

        public RagdollPawn Dummy(int i)
        {
            while (game.dummies.Count <= i && game.dummies.Count < LabLayout.DummySpawns.Length) game.AddDummy();
            return i < game.dummies.Count ? game.dummies[i] : null;
        }

        public static void Place(RagdollPawn p, Vector3 ground, Vector3 face)
        {
            if (p != null) p.Teleport(ground + Vector3.up * (p.standHeight + 0.02f), face);
        }

        void ResetDummies()
        {
            var spots = LabLayout.DummySpawns;
            for (int i = 0; i < game.dummies.Count; i++)
            {
                var d = game.dummies[i];
                if (d == null) continue;
                d.ResetSkill();
                d.SetInput(new PawnInput());
                Place(d, i < spots.Length ? spots[i] : Vector3.zero, Vector3.back);
            }
            Report("더미를 제자리에 다시 세움");
        }

        /// <summary>Three allies close round P1 and one enemy a few metres off: the king's guard, the rook's swap.</summary>
        void AlliesAround(RagdollPawn p1)
        {
            Vector3 ahead = Flat(p1.Facing, Vector3.forward), side = Vector3.Cross(Vector3.up, ahead);
            Vector3 c = p1.FeetPoint;
            Vector3[] spots = { c + side * 1.4f + ahead * 0.6f, c - side * 1.4f + ahead * 0.6f, c + ahead * 1.6f, c + ahead * 4.5f };
            for (int i = 0; i < 4; i++)
            {
                var d = Dummy(i);
                if (d == null) continue;
                SetSide(d, p1, i < 3);
                d.ResetSkill();
                d.SetInput(new PawnInput());
                // On the floor there (R98: on a slope it is not P1's height — they stood inside it or fell from the air).
                Place(d, WalkFloor(c, spots[i]), i < 3 ? ahead : -ahead);
            }
            Report("아군 더미 3명을 내 옆에, 적 1명을 앞 4.5 m에 세움");
        }

        /// <summary>Enemies on the hill's first and second tiers, close to the edge facing P1's side.</summary>
        void EnemiesOnHill()
        {
            Vector3 h = QueenHillLayout.HillCenter;
            float e1 = QueenHillLayout.TierHalf(0) - 0.45f, e2 = QueenHillLayout.TierHalf(1) - 0.45f;
            Vector3[] spots = { new Vector3(h.x + e1, QueenHillLayout.TierTop(0), h.z - 1f), new Vector3(h.x + e2, QueenHillLayout.TierTop(1), h.z + 0.5f) };
            for (int i = 0; i < 2; i++)
            {
                var d = Dummy(i);
                if (d == null) continue;
                SetSide(d, P1, false);
                d.ResetSkill();
                d.SetInput(new PawnInput());
                Place(d, spots[i], Vector3.right);
            }
            Report("탑 1층·2층 가장자리에 적 더미를 세움 (비숍·나이트 시험)");
        }

        void KnockDummies()
        {
            foreach (var d in game.dummies)
                if (d != null) d.Knockdown("시험: 넘어뜨리기", 3f);
            Report("더미를 3초 동안 넘어뜨림");
        }

        // ---------------------------------------------------------------- test helpers (R93, 승규 님: "원활한 테스트를 위해")

        static readonly PieceKind[] RookPartners = { PieceKind.Queen, PieceKind.Bishop, PieceKind.Knight, PieceKind.King, PieceKind.Pawn };
        PieceKind lastPartner = PieceKind.Rook, lastPiece;
        SkillStage lastStage;

        // The other side's piece a helper sets on P1: the aim it holds, F once, the left click once, for a while.
        RagdollPawn bot;
        Vector3 botAim;
        bool botTap, botClick;
        float botLeft;

        bool HelpersOn => helpers && game != null && !game.AutoTest && (HelpersForProbe || !QueenHillSkillProbe.Busy);

        void OnSkillMoment(QueenHillFxEvent e)
        {
            if (e.kind == QueenHillFxKind.KingWindup && e.by != null && e.by == P1 && HelpersOn) KingHelper(e.by);
        }

        /// <summary>The king's guard to try: three of P1's pawns stand just ahead of it (inside the guard's circle), the
        /// other side's queen 5.2 m off, and once the guard is on she slashes down the line through them and the king
        /// (the allies stagger, the king falls: B).</summary>
        void KingHelper(RagdollPawn king)
        {
            Vector3 ahead = Flat(king.Facing, Vector3.forward), side = Vector3.Cross(Vector3.up, ahead);
            Vector3 c = king.FeetPoint;
            Vector3[] spots = { c + ahead * 1.25f, c + ahead * 0.95f + side * 0.7f, c + ahead * 0.95f - side * 0.7f };
            for (int i = 0; i < spots.Length; i++)
            {
                var d = Dummy(i);
                if (d == null) continue;
                Ready(d, PieceKind.Pawn, king, true);
                Place(d, WalkFloor(c, spots[i]), ahead);
            }
            var enemy = Dummy(3);
            if (enemy != null)
            {
                Ready(enemy, PieceKind.Queen, king, false);
                Place(enemy, WalkFloor(c, c + ahead * 5.2f), -ahead);
                StartCoroutine(SlashAt(enemy, king));
            }
            Report("킹 시험: 아군 폰 3명을 앞에, 적 퀸을 5 m 앞에 세움 → 호위가 켜지면 검격");
        }

        IEnumerator SlashAt(RagdollPawn enemy, RagdollPawn king)
        {
            // The guard goes on at the end of the king's windup; the queen aims a moment later and slashes.
            yield return new WaitForSeconds(skills.kingWindup + 0.12f);
            if (enemy == null || king == null) yield break;
            Vector3 to = king.Hips.position - enemy.Hips.position;
            to.y = 0f;
            bot = enemy;
            botAim = (to.normalized + Vector3.down * 0.25f).normalized;
            botLeft = 2.5f;
            botTap = true;
            yield return new WaitForSeconds(0.12f);
            if (bot == enemy) botClick = true;
        }

        void DriveBot()
        {
            if (bot == null) return;
            botLeft -= Time.deltaTime;
            if (botLeft <= 0f || bot.State == PawnState.Ragdoll)
            {
                bot.SetInput(new PawnInput());
                bot = null;
                return;
            }
            bot.SetInput(new PawnInput { aim = botAim, shove = botClick });
            bot.SetSkillInput(botTap);
            botTap = botClick = false;
        }

        /// <summary>The rook's swap to try: one ally of a random piece (a new one each time, for its colour on the
        /// arch) up where the rook would climb to: the nearest higher floor ahead (a tier of the hill), or the floor
        /// ahead if there is none. It says yes by itself (a dummy).</summary>
        void RookHelper(RagdollPawn rook)
        {
            var ally = Dummy(0);
            if (ally == null) return;
            PieceKind kind;
            if (NextRookPartner.HasValue)
            {
                kind = NextRookPartner.Value;
                NextRookPartner = null;
            }
            else
            {
                do kind = RookPartners[Random.Range(0, RookPartners.Length)];
                while (kind == lastPartner);
            }
            lastPartner = kind;
            Vector3 ahead = Flat(rook.Facing, Vector3.forward);
            Vector3 spot = ClimbSpot(rook, ahead);
            Ready(ally, kind, rook, true);
            Place(ally, spot, -ahead);
            Report($"룩 시험: 아군 {ChessPieces.Name(kind)}를 올라갈 자리(높이 {spot.y:0.0} m)에 세움 — 좌클릭으로 교대 요청");
        }

        /// <summary>The nearest higher floor ahead of the rook (at least 0.3 m up: a tier), 3–5.4 m off within 24° of
        /// its aim, with room to stand on; else the floor 4 m ahead.</summary>
        Vector3 ClimbSpot(RagdollPawn rook, Vector3 ahead)
        {
            Vector3 me = rook.FeetPoint;
            Vector3 best = WalkFloor(me, me + ahead * 4f);
            float bestRise = float.MaxValue, bestScore = float.MaxValue;
            foreach (float dist in new[] { 3f, 3.6f, 4.2f, 4.8f, 5.4f })
                foreach (float angle in new[] { 0f, -12f, 12f, -24f, 24f })
                {
                    Vector3 p = me + Quaternion.Euler(0f, angle, 0f) * ahead * dist;
                    if (!Physics.Raycast(p + Vector3.up * 8f, Vector3.down, out var hit, 16f, ~0, QueryTriggerInteraction.Ignore)
                        || hit.normal.y < 0.8f || RagdollPawn.ColliderOwner.ContainsKey(hit.collider)) continue;
                    float rise = hit.point.y - me.y;
                    if (rise < 0.3f) continue;
                    if (Blocked(hit.point)) continue;   // room to stand: nothing solid round the body there
                    // The lowest tier up first (the swap's arc stays clear), then near the aim and about 4 m off.
                    float score = Mathf.Abs(angle) + Mathf.Abs(dist - 4f) * 4f;
                    if (rise > bestRise + 0.2f || rise > bestRise - 0.2f && score >= bestScore) continue;
                    bestRise = rise;
                    bestScore = score;
                    best = hit.point;
                }
            return best;
        }

        static bool Blocked(Vector3 feet)
        {
            foreach (var c in Physics.OverlapCapsule(feet + Vector3.up * 0.35f, feet + Vector3.up * 0.75f, 0.28f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (RagdollPawn.ColliderOwner.ContainsKey(c)) continue;
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                return true;
            }
            return false;
        }

        /// <summary>The floor at <paramref name="to"/> as one would walk there from <paramref name="from"/> (R98; the
        /// helper before took a floor at about the starting height, so on a slope a dummy stood inside it or in the air):
        /// every 0.25 m the nearest floor below a step's height (1.05 m) over the last one, so a slope is followed up or
        /// down, a drop is gone down, a tier is stepped up, and a taller wall is passed through to the floor behind it.</summary>
        static Vector3 WalkFloor(Vector3 from, Vector3 to)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            float level = from.y;
            int n = Mathf.Max(1, Mathf.CeilToInt(flat.magnitude / 0.25f));
            for (int i = 1; i <= n; i++)
            {
                Vector3 p = from + flat * (i / (float)n);
                float best = float.MaxValue, top = level + 1.15f;
                foreach (var hit in Physics.RaycastAll(new Vector3(p.x, top, p.z), Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.normal.y < 0.5f || hit.distance >= best || RagdollPawn.ColliderOwner.ContainsKey(hit.collider)) continue;
                    var rb = hit.collider.attachedRigidbody;
                    if (rb != null && !rb.isKinematic) continue;
                    best = hit.distance;
                    level = hit.point.y;
                }
            }
            return new Vector3(to.x, level, to.z);
        }

        /// <summary>A dummy set to a piece and a side, its skill and keys cleared.</summary>
        void Ready(RagdollPawn d, PieceKind kind, RagdollPawn of, bool ally)
        {
            if (d.Piece != kind) d.SetPiece(kind);
            SetSide(d, of, ally);
            d.ResetSkill();
            d.SetInput(new PawnInput());
            if (d == bot) bot = null;
        }

        static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : fallback;
        }

        // ---------------------------------------------------------------- the hill and the wall

        static Material Mat(Color c, Texture tex = null, float smooth = 0.25f)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            if (tex != null) m.mainTexture = tex;
            m.SetFloat("_Glossiness", smooth);
            return m;
        }

        /// <summary>Cream and walnut board squares (the course's own colours), two squares across one texture.</summary>
        static Texture2D Board()
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var cream = new Color32(0xED, 0xE0, 0xC2, 0xFF);
            var walnut = new Color32(0x8C, 0x61, 0x3D, 0xFF);
            var px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    px[y * 64 + x] = ((x / 32) + (y / 32)) % 2 == 0 ? cream : walnut;
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        static void BuildHill(Transform parent)
        {
            var root = new GameObject("Hill (3 tiers)").transform;
            root.SetParent(parent, false);
            var board = Board();
            var side = Mat(new Color(0.43f, 0.29f, 0.18f));
            var trim = Mat(new Color(0.93f, 0.86f, 0.7f));
            Vector3 c = QueenHillLayout.HillCenter;
            for (int i = 0; i < QueenHillLayout.TierSize.Length; i++)
            {
                float size = QueenHillLayout.TierSize[i], top = QueenHillLayout.TierTop(i), bottom = i * QueenHillLayout.TierHeight;
                // The block from the floor up (a lower tier's top would show through a floating slab's gap).
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Tier {i + 1} ({top:0.0} m)";
                block.transform.SetParent(root, false);
                block.transform.position = new Vector3(c.x, (bottom + top) * 0.5f, c.z);
                block.transform.localScale = new Vector3(size, top - bottom, size);
                block.GetComponent<MeshRenderer>().sharedMaterial = side;
                // The top: board squares, 1.5 m each (the texture holds two across).
                var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
                face.name = "Board";
                Object.Destroy(face.GetComponent<Collider>());
                face.transform.SetParent(root, false);
                face.transform.SetPositionAndRotation(new Vector3(c.x, top + 0.002f, c.z), Quaternion.Euler(90f, 0f, 0f));
                face.transform.localScale = new Vector3(size, size, 1f);
                var m = Mat(Color.white, board, 0.35f);
                m.mainTextureScale = new Vector2(size / 3f, size / 3f);
                face.GetComponent<MeshRenderer>().sharedMaterial = m;
                // A light lip round the top edge.
                foreach (var (at, scale) in new[]
                         {
                             (new Vector3(0f, 0f, size * 0.5f), new Vector3(size + 0.06f, 0.06f, 0.06f)),
                             (new Vector3(0f, 0f, -size * 0.5f), new Vector3(size + 0.06f, 0.06f, 0.06f)),
                             (new Vector3(size * 0.5f, 0f, 0f), new Vector3(0.06f, 0.06f, size + 0.06f)),
                             (new Vector3(-size * 0.5f, 0f, 0f), new Vector3(0.06f, 0.06f, size + 0.06f)),
                         })
                {
                    var lip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    lip.name = "Lip";
                    Object.Destroy(lip.GetComponent<Collider>());
                    lip.transform.SetParent(root, false);
                    lip.transform.position = new Vector3(c.x, top - 0.03f, c.z) + at;
                    lip.transform.localScale = scale;
                    lip.GetComponent<MeshRenderer>().sharedMaterial = trim;
                }
            }
            // The promotion pad on top (only to be seen: the second half's gate is not part of this test).
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Promotion pad";
            Object.Destroy(pad.GetComponent<Collider>());
            pad.transform.SetParent(root, false);
            pad.transform.position = new Vector3(c.x, QueenHillLayout.TierTop(2) + 0.02f, c.z);
            pad.transform.localScale = new Vector3(1.1f, 0.02f, 1.1f);
            pad.GetComponent<MeshRenderer>().sharedMaterial = Mat(new Color(1f, 0.78f, 0.25f), null, 0.6f);
        }

        static void BuildWall(Transform parent)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Stone wall (stops the queen's slash)";
            wall.transform.SetParent(parent, false);
            var size = QueenHillLayout.WallSize;
            wall.transform.position = QueenHillLayout.WallCenter + Vector3.up * size.y * 0.5f;
            wall.transform.localScale = size;
            wall.GetComponent<MeshRenderer>().sharedMaterial = Mat(new Color(0.72f, 0.68f, 0.62f));
        }

        // ---------------------------------------------------------------- the panel

        static string HowTo(PieceKind kind) => kind switch
        {
            PieceKind.Pawn => "F = 몸을 낮추고 짧게 가속 (방향키 방향, 없으면 카메라 방향, 2.6 m)\n다리를 앞뒤로 벌려 골반이 내려감 · 무적 없음 · 벽 통과 없음\n지나가며 부딪힌 적은 옆으로 비킴",
            PieceKind.King => "F = 홀을 들었다 내리꽂음 (0.3초) → 반경 2.5 m 아군에게 2초 호위\n호위: 넘어지지 않고 밀림도 1/4 · 몸에 초록 윤곽선 · 킹 자신은 빠짐 (B)\n시험 도우미: F를 누르면 아군 폰 3명이 앞에 서고 5 m 앞 적 퀸이 검격",
            PieceKind.Queen => "F = 조준: 검격 줄(체스판 4칸)이 마우스 방향을 따라감 (걸을 수 있음)\n좌클릭 = 칼을 오른쪽 아래 뒤로 내려 0.45초 준비 → 아래에서 위로 올려 베며 6 m 검기\n경고 칸은 바닥에 붙어 깔리고(경사로도) 검기는 그 칸을 따라감: 경사로는 오르내리고, 아래층으로 내려가고, 한 층 턱은 타고 오름 · 칸 위 적은 넘어짐 (A)\n돌벽처럼 한 층보다 높은 벽에 막힘 · 회복 0.6초 · 우클릭/F = 취소",
            PieceKind.Rook => "F = 조준: 마우스 방향 9 m 안의 아군을 고름 (폰도 됨, B)\n좌클릭 = 교대 요청 → 상대가 수락하면(더미는 0.4초 뒤 자동) 둘이 날아 자리 바꿈\n길이나 설 자리가 막히면 안 됨 · 수락 없으면 취소(쿨 없음)\n시험 도우미: F를 누르면 앞쪽 한 층 위에 아군 1명(기물 무작위 → 리본 색 확인)",
            PieceKind.Bishop => "F = 1.3 m 떠오름 (조준 3초, 더 오를 수 없음) · 마우스로 조준\n좌클릭 = 견제탄 → 맞은 곳에 X자 파동 · 2발: 첫 발이 떨어지면 다시 조준\n맞은 적은 밀려 넘어짐: 벽·탑 가장자리면 떨어짐 (B) · 우클릭/F = 내려옴",
            PieceKind.Knight => "F = 착지점 조준 (6 m, 지금 바닥보다 한 층 0.9 m까지만, 그보다 높으면 회색)\n조준점 1.05 m 안에 적이 있으면 그 머리 위로 자동 조준 (말굽 표식이 머리 위)\n좌클릭 = 높게 도약 → 머리를 밟거나 착지 원 1.3 m 안 적은 0.7초 납작 + 밀림\n높은 곳 가장자리에서 납작해지면 떨어짐 (B)",
            _ => "",
        };

        static string StageName(RagdollPawn pawn)
        {
            if (pawn.SkillStage == SkillStage.None) return pawn.SkillCooldown > 0f ? "쿨타임" : "준비됨";
            if (!string.IsNullOrEmpty(pawn.SkillDetail)) return pawn.SkillDetail;
            // An online client's puppet has the stage but not the host's wording (R111).
            switch (pawn.SkillStage)
            {
                case SkillStage.Windup: return pawn.QhAimLocked ? "준비 동작" : "조준";
                case SkillStage.Active: return "발동";
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
            if (QueenHillSkillFilm.Rolling) return;
            var p1 = Me;
            if (p1 == null) return;
            EnsureStyles();
            DrawOverheads();

            const float width = 380f;
            panelRect = new Rect(8f, 44f, width, Mathf.Min(Screen.height - 180f, 680f));
            GUILayout.BeginArea(panelRect, box);
            GUILayout.Label("퀸 오브 더 힐 스킬 · 네트워크 시험", header);
            GUILayout.Label($"기물 바꾸기: 버튼 또는 {previousPieceKey} ◀ ▶ {nextPieceKey}   ·   스킬: <b>{skillKey}</b>", small);
            GUILayout.BeginHorizontal();
            foreach (var kind in Pieces)
                if (GUILayout.Button(ChessPieces.Name(kind), p1.Piece == kind ? selected : button, GUILayout.Height(26f))) SetPiece(p1, kind);
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label($"<b>{ChessPieces.Name(p1.Piece)} — {RagdollPawn.QueenHillSkillName(p1.Piece)}</b>   (팀 {Teams.Name(p1.Team)}, 무게 {p1.Weight:0.#})", text);
            GUILayout.Label(HowTo(p1.Piece), small);

            GUILayout.Space(2f);
            GUILayout.Label($"단계: <b>{StageName(p1)}</b>" + (p1.SkillStage != SkillStage.None ? $"  {p1.SkillStageTime:0.00}초" : ""), text);
            float cd = p1.SkillCooldownTotal > 0f ? p1.SkillCooldown / p1.SkillCooldownTotal : 0f;
            Bar(1f - cd, cd > 0f ? new Color(0.45f, 0.5f, 0.6f) : new Color(0.3f, 0.85f, 1f), cd > 0f ? $"쿨타임 {p1.SkillCooldown:0.0}초" : "사용 가능");
            GUILayout.Label(skills.testCooldown > 0f ? $"시험용 쿨타임 {skills.testCooldown:0.#}초 (기획 쿨은 Inspector 시험용 쿨을 0으로)" : "기획 쿨타임", small);
            if (p1.SkillStage == SkillStage.Windup && !string.IsNullOrEmpty(p1.QhAimWhy) && (p1.Piece == PieceKind.Knight || p1.Piece == PieceKind.Bishop || p1.Piece == PieceKind.Rook))
                GUILayout.Label($"조준: {p1.QhAimWhy}", small);
            if (p1.WardLeft > 0f) GUILayout.Label($"호위 {p1.WardLeft:0.0}초", small);
            if (p1.CastleAsker != null) GUILayout.Label($"<b>{p1.CastleAsker.DisplayName}의 교대 요청 — {skillKey} = 수락</b>", text);
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
                // Online (R111): the host runs every skill; the helpers and the dummies are offline only. Who hosts and the
                // link's numbers are on the test screen at the top right (R112).
                GUILayout.Space(6f);
                GUILayout.Label($"룩 교대 요청을 받으면 {skillKey} = 수락 (방장이 판정). 기물 바꾸기는 방장에게 요청 (Z · X). 지연 넣기: F12", small);
                GUILayout.Space(6f);
                GUILayout.Label("<b>최근 결과</b>" + (game.NetworkHost ? "" : " (참가자 화면에는 내 요청만 나와요)"), text);
                for (int i = log.Count - 1; i >= 0; i--) GUILayout.Label(log[i], small);
                GUILayout.EndArea();
                return;
            }
            if (GUILayout.Button(helpers ? "시험 도우미 켜짐 (킹·룩: F를 누르면 더미가 섬)" : "시험 도우미 꺼짐", helpers ? selected : button)) helpers = !helpers;

            GUILayout.Space(6f);
            GUILayout.Label("<b>더미</b> (회색 = 적, 흰색 = 내 팀)", text);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("＋", button, GUILayout.Width(36f))) { game.AddDummy(); Report("더미 추가"); }
            if (GUILayout.Button("－", button, GUILayout.Width(36f))) { game.RemoveDummy(); Report("더미 빼기"); }
            if (GUILayout.Button("다시 세우기", button)) ResetDummies();
            if (GUILayout.Button("모두 넘어뜨리기", button)) KnockDummies();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < game.dummies.Count; i++)
            {
                var d = game.dummies[i];
                if (d == null) continue;
                bool ally = d.Team == p1.Team && p1.Team != Teams.None;
                if (GUILayout.Button($"{i + 1}: {(ally ? "내 팀" : "적")}", ally ? selected : button))
                {
                    SetSide(d, p1, !ally);
                    Report($"{d.DisplayName}: {(ally ? "적" : "내 팀")}");
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("킹·룩 시험: 내 옆에 아군 3 · 적 1", button)) AlliesAround(p1);
            if (GUILayout.Button("탑 위에 적 2", button)) EnemiesOnHill();
            GUILayout.EndHorizontal();
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

        /// <summary>A short line over each piece: its skill state (P1) or what last happened to it (the dummies).</summary>
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
                    line = pawn.SkillStage != SkillStage.None ? StageName(pawn)
                        : pawn.SkillCooldown > 0f ? $"{RagdollPawn.QueenHillSkillName(pawn.Piece)} {pawn.SkillCooldown:0.0}" : "";
                else
                {
                    line = pawn.State == PawnState.Ragdoll ? "넘어짐" : pawn.Squashed ? "납작" : pawn.WardLeft > 0f ? "호위" : pawn.Staggered ? "휘청" : "";
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
