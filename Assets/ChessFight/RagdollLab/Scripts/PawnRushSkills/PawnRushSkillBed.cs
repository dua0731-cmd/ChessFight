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

        [Header("시험 장치")]
        [Tooltip("룩만 부수는 바리케이드 (바닥 가운데)")]
        public Vector3 barricadeAt = new Vector3(-9f, 0f, 2f);
        public Vector3 barricadeFacing = Vector3.right;
        [Tooltip("룩 시험 뭉치: 내 앞 거리 (m)와 서로 간격 (m)")]
        public float clusterAhead = 3.5f;
        public float clusterGap = 0.7f;

        /// <summary>The pieces Pawn Rush promotes to, the pawn first (no king, D1).</summary>
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
            SkillBarricade.Build(barricadeAt, barricadeFacing, "Pawn Rush 바리케이드 (룩만 부숨)");
            fx = GetComponent<PawnRushSkillFx>();
            if (fx == null) fx = gameObject.AddComponent<PawnRushSkillFx>();
            // The skills' sounds (R114, 승규 님's picks on the R107 page).
            sfx = GetComponent<PawnRushSkillSfx>();
            if (sfx == null) sfx = gameObject.AddComponent<PawnRushSkillSfx>();
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
            if (game.SkillKey != skillKey) game.SkillKey = skillKey;

            var me = Me;
            if (me == null || game.SuppressInput) return;
            if (Input.GetKeyDown(previousPieceKey)) StepPiece(me, -1);
            if (Input.GetKeyDown(nextPieceKey)) StepPiece(me, 1);
            if (!Online && Input.GetKeyDown(clusterKey)) ClusterDummies(me);
            // The skill key itself: LabGame.ReadInput puts it into P1's input (R111).
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
            if (Online)
            {
                // Online the host switches pieces (it runs them): ask it; the pawn turns when its snapshot says so.
                game.AskPiece(kind);
                Report($"기물 바꾸기 요청: {ChessPieces.Name(kind)} ({RagdollPawn.SkillName(kind)}) — 방장이 바꿔요");
                return;
            }
            pawn.SetPiece(kind);
            pawn.ResetSkill();
            Report($"{pawn.DisplayName} → {ChessPieces.Name(kind)} ({RagdollPawn.SkillName(kind)})");
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
            PieceKind.Pawn => "F = 1칸 걸음 · 0.1~0.8초 안에 F 한 번 더 = 두 번째 걸음\n(걸음마다 방향키 방향) · 곧게 부딪히면 밀침, 대각선이면 넘어뜨림\n넘어진 팀원에게 닿으면 부축 (둘 다 +15%)",
            PieceKind.Queen => "F = 0.35초 예고(금빛 원이 조여들고 작은 불빛이 피어오름) 뒤 반경 3 m 충격파\n1.5 m 안은 넘어지고 바깥은 밀림 · 후딜 0.45초",
            PieceKind.Rook => "F = 조준: 반투명 선이 마우스 방향을 따라감 (걸을 수 있음, Shift 질주 X)\n좌클릭 = 방향 고정 0.3초 뒤 4칸 돌진 · 우클릭/F = 취소\n공중에서도 F: 마우스 위아래로 위·아래 조준, 좌클릭 = 잠깐 멈췄다 슈퍼맨처럼 그 방향으로 돌진 (아래면 바닥에 내리꽂음)\n적 3명까지 옆으로 튕김 · 벽은 휘청 · 바리케이드 부숨 (x −9, z 2)",
            PieceKind.Bishop => "F = 조준: 반투명 X가 마우스 위치를 따라감 (근거리 4.5 m 안)\n좌클릭 = 설치 · 우클릭/F = 취소\n0.5초 뒤 무장, 6초 · 적이 지나가면 줄이 다리를 따라 늘어났다 잡아채 앞으로 넘어뜨림 · 적 2명 걸면 끊김",
            PieceKind.Knight => "F = 도약 (카메라 방향, 높이 1.6 m · 약 5.5 m) · 점프 중에도 F = 공중에서 바로 도약\n공중에서 F: 3 m 안 적에 주황 표시가 뜨면 자동으로 머리 찍고 착지\n적이 없으면 F = 다시 차고 나감 (\"다~당\", 방향키로 최대 90° 꺾기, 1회) · 착지 1.5 m 안 휘청",
            _ => "킹 스킬은 아직 만들지 않았어요 (D1)",
        };

        static string StageName(RagdollPawn pawn)
        {
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

            GUILayout.Space(4f);
            GUILayout.Label($"<b>{ChessPieces.Name(p1.Piece)} — {RagdollPawn.SkillName(p1.Piece)}</b>   (팀 {Teams.Name(p1.Team)}, 무게 {p1.Weight:0.#})", text);
            GUILayout.Label(HowTo(p1.Piece), small);

            GUILayout.Space(2f);
            GUILayout.Label($"단계: <b>{StageName(p1)}</b>" + (p1.SkillStage != SkillStage.None ? $"  {p1.SkillStageTime:0.00}초" : ""), text);
            float cd = p1.SkillCooldownTotal > 0f ? p1.SkillCooldown / p1.SkillCooldownTotal : 0f;
            Bar(1f - cd, cd > 0f ? new Color(0.45f, 0.5f, 0.6f) : new Color(0.3f, 0.85f, 1f),
                cd > 0f ? $"쿨타임 {p1.SkillCooldown:0.0}초" : "사용 가능");
            GUILayout.Label(skills.testCooldown > 0f ? $"시험용 쿨타임 {skills.testCooldown:0.#}초 (기획 쿨은 Inspector 시험용 쿨을 0으로)" : "기획 쿨타임 (v0.1)", small);
            string extra = "";
            if (p1.HasteLeft > 0f) extra += $"진군 +{(skills.hasteScale - 1f) * 100f:0}% {p1.HasteLeft:0.0}초   ";
            if (p1.GetUpGuardLeft > 0f) extra += $"기상 보호 {p1.GetUpGuardLeft:0.0}초   ";
            if (!string.IsNullOrEmpty(extra)) GUILayout.Label(extra, small);
            GUILayout.Label("E · Q · 마우스는 랩의 원래 기능 그대로 (이 씬에서 F는 스킬만)", small);
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
