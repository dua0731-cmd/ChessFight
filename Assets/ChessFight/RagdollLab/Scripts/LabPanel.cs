using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>IMGUI debug panel: every tuning slider, presets, JSON export and live stiffness readout.</summary>
    [DefaultExecutionOrder(200)]
    public class LabPanel : MonoBehaviour
    {
        public LabGame game;

        const float PanelWidth = 470f;
        const float LiveWidth = 340f;

        Vector2 scroll;
        GUIStyle panelStyle, headerStyle, foldStyle, labelStyle, smallStyle, valueStyle, buttonStyle, hintStyle;
        Texture2D panelTexture, whiteTexture;
        readonly Dictionary<string, bool> open = new Dictionary<string, bool>();
        List<(FieldInfo field, TunableAttribute tag, RangeAttribute range)> fields;

        static readonly string[] Help =
        {
            "이동: WASD / 왼쪽 스틱 · 전력질주(누르고 있기, 스테미나 소모): 왼쪽 Shift / LT",
            "점프: Space / A",
            "슬라이딩 태클: 마우스 왼쪽 / RB·B",
            "  발부터 미끄러지며 흐물흐물 넘어짐 · 부딪힌 상대도 넘어짐 · 내리막에서는 계속 미끄러져 달리기보다 빠름",
            "잡기(누르고 있기): 마우스 오른쪽 / LB",
            "  상대 근처 = 잡고 끌기 · 잡은 채 좌클릭 = 던지기",
            "  벽 = 매달리기 · 벽 앞에서 W + 우클릭 = 등반(스테미나) · 꼭대기에서 계속 W = 올라서기",
            "  점프하며 잡기 → 벽 모서리를 잡으면 한 번 더 점프 = 기어오르기",
            "  잡혔을 때 좌클릭 연타 = 버둥대며 탈출 (반대 방향 + 점프를 섞으면 더 셈)",
            "P2 키보드: 방향키 · 오른쪽 Shift 점프 · 오른쪽 Ctrl 슬라이딩 · Enter 잡기 · / 전력질주",
            "카메라: 마우스 / 오른쪽 스틱 · 휠 확대·축소 · F2 화면 분할 (P2가 조작하면 자동)",
            "마우스 조작은 게임 화면을 한 번 클릭해야 켜져요 · Esc 마우스 풀기",
            "R 전체 리스폰 · T 슬로모션 · F 자유 카메라(WASD·Q·E)",
            "Tab / Start 패널 · 패드 Back 본인 리스폰 · F3 온라인 패널",
            "값 세트: 1~4 불러오기 · Shift+1~4 저장 · B 무작위 전환",
            "평가: ] 좋음 · [ 별로 (블라인드 모드에서 어느 세트인지 숨김)",
        };

        void OnGUI()
        {
            if (game == null || game.AutoTest) return;
            EnsureStyles();
            if (!game.PanelOpen)
            {
                DrawHint();
                return;
            }
            DrawTuning();
            DrawLive();
        }

        void EnsureStyles()
        {
            if (panelStyle != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 14);
            panelTexture = Solid(new Color(0.07f, 0.08f, 0.1f, 0.88f));
            whiteTexture = Solid(Color.white);
            panelStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10) };
            panelStyle.normal.background = panelTexture;
            labelStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13, wordWrap = false };
            labelStyle.normal.textColor = new Color(0.92f, 0.93f, 0.95f);
            smallStyle = new GUIStyle(labelStyle) { fontSize = 12, wordWrap = true };
            smallStyle.normal.textColor = new Color(0.7f, 0.74f, 0.8f);
            valueStyle = new GUIStyle(labelStyle) { fontSize = 12, alignment = TextAnchor.MiddleRight };
            headerStyle = new GUIStyle(labelStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
            foldStyle = new GUIStyle(labelStyle) { fontSize = 14, fontStyle = FontStyle.Bold };
            foldStyle.normal.textColor = new Color(0.62f, 0.8f, 1f);
            buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 12 };
            hintStyle = new GUIStyle(labelStyle) { fontSize = 13 };
            fields = RagdollTuning.Fields().ToList();
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void DrawHint()
        {
            // Stamina and the "you are held" prompt live next to the pawn now (StaminaHud).
            string text = "Tab: 튜닝 패널   R: 리스폰   T: 슬로모션   F: 자유 카메라   F2: 화면 분할";
            // Until the cursor is locked the clicks go nowhere, which looks exactly like "the
            // actions are broken". Say so where it cannot be missed.
            if (Cursor.lockState != CursorLockMode.Locked && !game.UiWantsCursor)
                text = "▶ 화면을 클릭하면 마우스 조작(시점 · 좌클릭 슬라이딩 · 우클릭 잡기)이 켜져요     " + text;
            if (game.SlowMotion) text += "   · 슬로모션 중";
            if (game.labCamera != null && game.labCamera.freeMode) text += "   · 자유 카메라 (WASD·Q·E, F로 복귀)";
            var size = hintStyle.CalcSize(new GUIContent(text));
            GUI.DrawTexture(new Rect(8, 8, size.x + 16, size.y + 8), panelTexture);
            GUI.Label(new Rect(16, 12, size.x, size.y), text, hintStyle);
        }

        void DrawTuning()
        {
            var values = game.tuning.values;
            GUILayout.BeginArea(new Rect(10, 10, PanelWidth, Screen.height - 20), panelStyle);
            GUILayout.Label("래그돌 튜닝  (Tab으로 닫기)", headerStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("명세 시작값", buttonStyle)) game.ResetParams();
            if (GUILayout.Button("앵커 0 (갱 비스트)", buttonStyle)) SetAnchor(0f);
            if (GUILayout.Button("앵커 3000", buttonStyle)) SetAnchor(3000f);
            if (GUILayout.Button("앵커 20000 (폴 가이즈)", buttonStyle)) SetAnchor(20000f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("JSON 저장", buttonStyle)) game.SaveParams();
            if (GUILayout.Button("불러오기", buttonStyle)) game.LoadParams();
            if (GUILayout.Button("클립보드 복사", buttonStyle)) game.CopyParams();
            if (GUILayout.Button("붙여넣기", buttonStyle)) game.PasteParams();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("프리셋: 두 발 모아 도약", buttonStyle)) game.ApplyWeightPreset();
            if (GUILayout.Button("프리셋: 달리기 5.5 · 질주 9.6 (기본)", buttonStyle)) game.ApplyStepPreset();
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(game.Status)) GUILayout.Label(game.Status, smallStyle);

            scroll = GUILayout.BeginScrollView(scroll);
            DrawSlots();
            string group = null;
            bool groupOpen = true;
            bool changed = false;
            foreach (var (field, tag, range) in fields)
            {
                if (tag.Group != group)
                {
                    group = tag.Group;
                    if (!open.ContainsKey(group)) open[group] = !group.Contains("명세 외");
                    GUILayout.Space(6f);
                    if (GUILayout.Button((open[group] ? "▼ " : "▶ ") + group, foldStyle)) open[group] = !open[group];
                    groupOpen = open[group];
                }
                if (!groupOpen) continue;
                float value = (float)field.GetValue(values);
                float next = SliderRow(tag.Label, value, range.min, range.max);
                if (!Mathf.Approximately(next, value))
                {
                    field.SetValue(values, next);
                    changed = true;
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label("테스트 장치", foldStyle);
            if (game.bar != null)
                game.bar.degreesPerSecond = SliderRow("회전 봉 속도 (도/초)", game.bar.degreesPerSecond, 0f, 360f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("물리 스텝", labelStyle, GUILayout.Width(120f));
            foreach (int hz in new[] { 50, 60, 90, 120 })
            {
                bool on = GUILayout.Toggle(game.physicsRate == hz, hz + "Hz", buttonStyle);
                if (on && game.physicsRate != hz)
                {
                    game.physicsRate = hz;
                    game.ApplyTime();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("솔버 반복", labelStyle, GUILayout.Width(120f));
            foreach (int iterations in new[] { 12, 16, 24, 40 })
            {
                bool on = GUILayout.Toggle(game.solverIterations == iterations, iterations + "회", buttonStyle);
                if (on && game.solverIterations != iterations) game.SetSolverIterations(iterations);
            }
            GUILayout.EndHorizontal();
            float slow = SliderRow("슬로모션 배율", game.slowMotionScale, 0.05f, 1f);
            if (!Mathf.Approximately(slow, game.slowMotionScale))
            {
                game.slowMotionScale = slow;
                game.ApplyTime();
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label($"더미 {game.dummies.Count}명", labelStyle, GUILayout.Width(120f));
            if (GUILayout.Button("+ 추가", buttonStyle)) game.AddDummy();
            if (GUILayout.Button("- 제거", buttonStyle)) game.RemoveDummy();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("플레이어 입력 (눌러서 바꾸기)", foldStyle);
            for (int i = 0; i < game.players.Length; i++)
            {
                var slot = game.players[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(slot.name, labelStyle, GUILayout.Width(40f));
                if (GUILayout.Button(LabGame.DeviceName(slot.device) + PadState(slot.device), buttonStyle)) game.CycleDevice(i);
                if (GUILayout.Button("리스폰", buttonStyle, GUILayout.Width(70f))) game.Respawn(slot.pawn);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8f);
            GUILayout.Label("조작", foldStyle);
            foreach (var line in Help) GUILayout.Label(line, smallStyle);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (changed) game.MarkTuningDirty();
        }

        void DrawSlots()
        {
            var slots = game.slots;
            if (slots == null) return;
            GUILayout.Label("값 세트 비교 (숫자 1~4 불러오기 · Shift+숫자 저장)", foldStyle);
            for (int i = 0; i < LabParamSlots.Count; i++)
            {
                GUILayout.BeginHorizontal();
                bool active = slots.Active == i && !slots.Blind;
                GUILayout.Label((active ? "▶ " : "   ") + LabParamSlots.Names[i], labelStyle, GUILayout.Width(30f));
                if (GUILayout.Button("저장", buttonStyle, GUILayout.Width(52f))) slots.Save(i);
                if (GUILayout.Button("불러오기", buttonStyle, GUILayout.Width(74f))) slots.Load(i);
                GUILayout.Label(slots.Blind ? "숨김" : slots.Summary(i), smallStyle);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            bool blind = GUILayout.Toggle(slots.Blind, "블라인드", buttonStyle, GUILayout.Width(78f));
            if (blind != slots.Blind) slots.Blind = blind;
            if (GUILayout.Button("무작위 전환 (B)", buttonStyle)) slots.Randomize();
            if (GUILayout.Button("좋음 ]", buttonStyle, GUILayout.Width(66f))) slots.Vote(true);
            if (GUILayout.Button("별로 [", buttonStyle, GUILayout.Width(66f))) slots.Vote(false);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(slots.Blind
                ? $"현재 값 세트: 숨김 · 평가 {slots.Votes}회"
                : $"현재 값 세트: {(slots.Active < 0 ? "없음" : LabParamSlots.Names[slots.Active])} · 평가 {slots.Votes}회", smallStyle);
            if (GUILayout.Button("평가 초기화", buttonStyle, GUILayout.Width(90f))) slots.ClearVotes();
            GUILayout.EndHorizontal();
        }

        float SliderRow(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelStyle, GUILayout.Width(200f));
            float next = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(150f));
            GUILayout.Label(Format(next, max), valueStyle, GUILayout.Width(56f));
            GUILayout.EndHorizontal();
            return Mathf.Approximately(next, value) ? value : Snap(next, max);
        }

        static float Snap(float v, float max)
        {
            if (max >= 1000f) return Mathf.Round(v / 10f) * 10f;
            if (max >= 100f) return Mathf.Round(v);
            if (max >= 10f) return Mathf.Round(v * 10f) / 10f;
            return Mathf.Round(v * 100f) / 100f;
        }

        static string Format(float v, float max) => v.ToString(max >= 100f ? "F0" : max >= 10f ? "F1" : "F2");

        void SetAnchor(float value)
        {
            game.tuning.values.hipAnchorStrength = value;
            game.MarkTuningDirty();
            game.Status = $"hipAnchorStrength = {value:F0}";
        }

        static string PadState(LabDevice device)
        {
            int index = LabGame.PadIndex(device);
            if (index < 0) return "";
            return XInputPad.Get(index).connected ? " (연결됨)" : " (연결 안 됨)";
        }

        void DrawLive()
        {
            var pawns = RagdollPawn.All;
            float height = Mathf.Min(40f + pawns.Count * 66f, Screen.height - 20f);
            GUILayout.BeginArea(new Rect(Screen.width - LiveWidth - 10f, 10f, LiveWidth, height), panelStyle);
            GUILayout.Label("실시간 상태", headerStyle);
            foreach (var pawn in pawns)
            {
                GUILayout.Label($"{pawn.DisplayName} · {StateName(pawn)} · {pawn.HorizontalSpeed:F1} m/s · 넘어짐 {pawn.Knockdowns}", labelStyle);
                Rect r = GUILayoutUtility.GetRect(LiveWidth - 30f, 10f);
                float k = pawn.EffectiveStiffness;
                GUI.color = new Color(0.25f, 0.27f, 0.3f);
                GUI.DrawTexture(r, whiteTexture);
                GUI.color = Color.Lerp(new Color(1f, 0.35f, 0.3f), new Color(0.35f, 0.85f, 0.45f), k);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(k), r.height), whiteTexture);
                GUI.color = Color.white;
                GUILayout.Label($"강성 {k:F2}  목표 배율 {pawn.TargetStiffness:F2}  {Flags(pawn)}", smallStyle);
            }
            GUILayout.EndArea();
        }

        static string StateName(RagdollPawn pawn)
        {
            switch (pawn.State)
            {
                case PawnState.Ragdoll: return "래그돌";
                case PawnState.GettingUp: return "기상 중";
                default: return pawn.Grounded ? "서 있음" : "공중";
            }
        }

        static string Flags(RagdollPawn pawn)
        {
            var parts = new List<string>();
            if (pawn.Touching) parts.Add("접촉");
            if (pawn.Grabbing) parts.Add("잡기");
            if (pawn.OnSlope) parts.Add($"경사 {pawn.SlopeAngle:F0}°");
            if (pawn.Stunned) parts.Add("피격");
            if (pawn.Shoving) parts.Add("던지기");
            if (pawn.Diving) parts.Add("슬라이딩");
            if (pawn.Sprinting) parts.Add("질주");
            if (pawn.Exhausted) parts.Add("지침");
            if (pawn.Climbing) parts.Add("등반");
            if (pawn.Stamina < 0.999f) parts.Add($"스테미나 {pawn.Stamina * 100f:F0}%");
            if (pawn.Tackles > 0) parts.Add($"태클 {pawn.Tackles}");
            if (pawn.HopsCaught > 0) parts.Add($"튐 방지 {pawn.HopsCaught}");
            return string.Join(" · ", parts);
        }
    }
}
