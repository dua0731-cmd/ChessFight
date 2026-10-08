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

        /// <summary>The six pieces of a Queen of the Hill team, the pawn first.</summary>
        public static readonly PieceKind[] Pieces = { PieceKind.Pawn, PieceKind.King, PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight };

        /// <summary>Pieces kept on the Pawn Rush skills (the probe's charging rook the pawn dodges, 6.B).</summary>
        public static readonly HashSet<RagdollPawn> PawnRushOnly = new HashSet<RagdollPawn>();

        static readonly List<string> log = new List<string>();
        const int LogLines = 9;

        LabGame game;
        QueenHillSkillFx fx;
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

        void Awake()
        {
            log.Clear();
            PawnRushOnly.Clear();
            RagdollPawn.SkillLog += OnSkill;
        }

        void OnDestroy()
        {
            RagdollPawn.SkillLog -= OnSkill;
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
            if (game != null && skillKey == KeyCode.F) game.InteractKeyOff = true;
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

            var p1 = P1;
            if (p1 == null || game.SuppressInput || game.NetworkControlled) return;
            if (Input.GetKeyDown(previousPieceKey)) StepPiece(p1, -1);
            if (Input.GetKeyDown(nextPieceKey)) StepPiece(p1, 1);
            bool free = game.labCamera != null && game.labCamera.freeMode;
            if (!free) p1.SetSkillInput(Input.GetKeyDown(skillKey));
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
                Place(d, spots[i], i < 3 ? ahead : -ahead);
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
            PieceKind.King => "F = 홀을 들었다 내리꽂음 (0.3초) → 반경 2.5 m 아군에게 2초 호위\n호위: 넘어지지 않고 밀림도 1/4 · 킹 자신은 빠짐 (B)",
            PieceKind.Queen => "F = 조준: 검격 줄(체스판 4칸)이 마우스 방향을 따라감 (걸을 수 있음)\n좌클릭 = 칼을 들고 0.45초 준비 → 6 m 검기 · 회복 0.6초 · 우클릭/F = 취소\n벽에 막힘 · 줄 위 적은 넘어짐 (A)",
            PieceKind.Rook => "F = 조준: 마우스 방향 6 m 안의 아군을 고름 (폰도 됨, B)\n좌클릭 = 교대 요청 → 상대가 수락하면(더미는 0.4초 뒤 자동) 둘이 날아 자리 바꿈\n길이나 설 자리가 막히면 안 됨 · 수락 없으면 취소(쿨 없음)",
            PieceKind.Bishop => "F = 1.3 m 떠오름 (3초, 더 오를 수 없음) · 마우스로 조준\n좌클릭 = 견제탄 → 맞은 곳에 X자 파동 (가운데 + 대각선 두 줄)\n맞은 적은 밀려 넘어짐: 벽·탑 가장자리면 떨어짐 (B)",
            PieceKind.Knight => "F = 착지점 조준 (6 m, 지금 바닥보다 한 층 0.9 m까지만, 그보다 높으면 회색)\n좌클릭 = 도약 → 착지 원 1.3 m 안 적은 0.7초 납작 + 바깥으로 밀림\n높은 곳 가장자리에서 납작해지면 떨어짐 (B)",
            _ => "",
        };

        static string StageName(RagdollPawn pawn)
        {
            if (pawn.SkillStage == SkillStage.None) return pawn.SkillCooldown > 0f ? "쿨타임" : "준비됨";
            return string.IsNullOrEmpty(pawn.SkillDetail) ? pawn.SkillStage.ToString() : pawn.SkillDetail;
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
            var p1 = P1;
            if (p1 == null) return;
            EnsureStyles();
            DrawOverheads();

            const float width = 380f;
            panelRect = new Rect(8f, 44f, width, Mathf.Min(Screen.height - 180f, 680f));
            GUILayout.BeginArea(panelRect, box);
            GUILayout.Label("퀸 오브 더 힐 스킬 시험 (R89)", header);
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
                GUILayout.EndHorizontal();
            }

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
                if (pawn == P1)
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
