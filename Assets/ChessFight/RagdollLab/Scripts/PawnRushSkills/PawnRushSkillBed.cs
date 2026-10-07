using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Pawn Rush skill test bed (scene PawnRush_SkillTest, a copy of RagdollTest). It gives every pawn in the
    /// lab the Pawn Rush skills, feeds P1's skill key, and draws a panel on the left of the screen to switch P1
    /// between the pieces Pawn Rush uses - pawn (the default), queen, rook, bishop, knight - and to set up the
    /// dummies. The king's skill is not made yet (D1). The skill key is a temporary pick (D3): E, Q, F and the
    /// mouse keep what they already do in the lab.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class PawnRushSkillBed : MonoBehaviour
    {
        public PawnRushSkillParams skills = new PawnRushSkillParams();

        [Header("키 (임시, 나중에 다시 정함)")]
        public KeyCode skillKey = KeyCode.G;
        public KeyCode previousPieceKey = KeyCode.Z;
        public KeyCode nextPieceKey = KeyCode.X;

        [Header("시험 장치")]
        [Tooltip("룩만 부수는 바리케이드 (바닥 가운데)")]
        public Vector3 barricadeAt = new Vector3(-9f, 0f, 2f);
        public Vector3 barricadeFacing = Vector3.right;

        /// <summary>The pieces Pawn Rush promotes to, the pawn first (no king, D1).</summary>
        public static readonly PieceKind[] Pieces = { PieceKind.Pawn, PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight };

        static readonly List<string> log = new List<string>();
        const int LogLines = 9;

        LabGame game;
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
            RagdollPawn.SkillLog += OnSkill;
        }

        void OnDestroy() => RagdollPawn.SkillLog -= OnSkill;

        static void OnSkill(RagdollPawn pawn, string line) => Report(line);

        void Start()
        {
            game = GetComponent<LabGame>();
            if (game == null) game = FindFirstObjectByType<LabGame>();
            SkillBarricade.Build(barricadeAt, barricadeFacing, "Pawn Rush 바리케이드 (룩만 부숨)");
            Report("폰 러쉬 스킬 시험 준비 완료 — 왼쪽 창에서 기물을 고르고 G");
        }

        RagdollPawn P1 => game != null && game.players.Length > 0 ? game.players[0].pawn : null;

        void Update()
        {
            if (game == null || game.AutoTest) return;
            foreach (var pawn in RagdollPawn.All)
                if (pawn != null && pawn.PawnRushSkills == null) pawn.PawnRushSkills = skills;

            var p1 = P1;
            if (p1 == null || game.SuppressInput || game.NetworkControlled) return;
            if (Input.GetKeyDown(previousPieceKey)) StepPiece(p1, -1);
            if (Input.GetKeyDown(nextPieceKey)) StepPiece(p1, 1);
            bool free = game.labCamera != null && game.labCamera.freeMode;
            if (!free) p1.SetSkillInput(Input.GetKeyDown(skillKey), Input.GetKey(skillKey));
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
            if (i < 0) i = 0;
            else i = (i + step + Pieces.Length) % Pieces.Length;
            SetPiece(pawn, Pieces[i]);
        }

        void SetPiece(RagdollPawn pawn, PieceKind kind)
        {
            pawn.SetPiece(kind);
            pawn.ResetSkill();
            Report($"{pawn.DisplayName} → {ChessPieces.Name(kind)} ({RagdollPawn.SkillName(kind)})");
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

        static string HowTo(PieceKind kind) => kind switch
        {
            PieceKind.Pawn => "G = 1칸 걸음 · 0.1~0.8초 안에 G 한 번 더 = 두 번째 걸음\n(걸음마다 방향키 방향) · 곧게 부딪히면 밀침, 대각선이면 넘어뜨림\n넘어진 팀원에게 닿으면 부축 (둘 다 +15%)",
            PieceKind.Queen => "G = 0.35초 예고(빛이 조여듦) 뒤 반경 3 m 충격파\n1.5 m 안은 넘어지고 바깥은 밀림 · 후딜 0.45초",
            PieceKind.Rook => "G = 0.7초 예고 (앞 0.4초는 카메라로 조준) 뒤 4칸 돌진\n적 3명까지 옆으로 튕김 · 벽은 휘청 0.6초\n바리케이드는 부숨 (x −9, z 2)",
            PieceKind.Bishop => "G를 누르고 있으면 조준 (X 미리보기), 떼면 설치\n조준 중 우클릭 = 취소 · 0.5초 뒤 무장, 6초, 적 2명 걸면 끊김",
            PieceKind.Knight => "G = 도약 (카메라 방향, 높이 1.6 m · 약 5.5 m)\n공중에서 G + 방향키 = 최대 90° 꺾기 (1회)\n머리 밟기 = 넘어뜨림 · 착지 1.5 m 안 휘청",
            _ => "킹 스킬은 아직 만들지 않았어요 (D1)",
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
            var p1 = P1;
            if (p1 == null) return;
            EnsureStyles();
            DrawOverheads();

            const float width = 360f;
            panelRect = new Rect(8f, 44f, width, Mathf.Min(Screen.height - 180f, 640f));
            GUILayout.BeginArea(panelRect, box);
            GUILayout.Label("폰 러쉬 스킬 시험", header);
            GUILayout.Label($"기물 바꾸기: 버튼 또는 {previousPieceKey} ◀ ▶ {nextPieceKey}   ·   스킬: <b>{skillKey}</b>", small);
            GUILayout.BeginHorizontal();
            foreach (var kind in Pieces)
            {
                string label = kind == PieceKind.Pawn ? "폰(기본)" : ChessPieces.Name(kind);
                if (GUILayout.Button(label, p1.Piece == kind ? selected : button, GUILayout.Height(26f))) SetPiece(p1, kind);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label($"<b>{ChessPieces.Name(p1.Piece)} — {RagdollPawn.SkillName(p1.Piece)}</b>   (팀 {Teams.Name(p1.Team)}, 무게 {p1.Weight:0.#})", text);
            GUILayout.Label(HowTo(p1.Piece), small);

            GUILayout.Space(2f);
            GUILayout.Label($"단계: <b>{StageName(p1)}</b>" + (p1.SkillStage != SkillStage.None ? $"  {p1.SkillStageTime:0.00}초" : ""), text);
            float cd = p1.SkillCooldownTotal > 0f ? p1.SkillCooldown / p1.SkillCooldownTotal : 0f;
            Bar(1f - cd, cd > 0f ? new Color(0.45f, 0.5f, 0.6f) : new Color(0.3f, 0.85f, 1f),
                cd > 0f ? $"쿨타임 {p1.SkillCooldown:0.0}초" : "사용 가능");
            string extra = "";
            if (p1.HasteLeft > 0f) extra += $"진군 +{(skills.hasteScale - 1f) * 100f:0}% {p1.HasteLeft:0.0}초   ";
            if (p1.GetUpGuardLeft > 0f) extra += $"기상 보호 {p1.GetUpGuardLeft:0.0}초   ";
            if (!string.IsNullOrEmpty(extra)) GUILayout.Label(extra, small);
            GUILayout.Label("E · Q · F · 마우스는 랩의 원래 기능 그대로", small);

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
                if (pawn == P1)
                    line = pawn.SkillStage != SkillStage.None ? StageName(pawn)
                        : pawn.SkillCooldown > 0f ? $"{RagdollPawn.SkillName(pawn.Piece)} {pawn.SkillCooldown:0.0}" : "";
                else
                {
                    line = pawn.State == PawnState.Ragdoll ? "넘어짐" : pawn.Staggered ? "휘청" : pawn.GetUpGuardLeft > 0f ? "기상 보호" : "";
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
