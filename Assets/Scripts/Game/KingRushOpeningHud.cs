using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    public sealed class KingRushOpeningHud : MonoBehaviour
    {
        PanelSettings owned;
        VisualElement root;
        Label state, objective, mission, ability, help;
        void Awake()
        {
            root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("KingRushOpeningHud"),
                Resources.Load<ThemeStyleSheet>("KingRushTheme"), null, new Vector2Int(1280, 720), out owned);
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;
            state = root.Q<Label>("opening-state"); objective = root.Q<Label>("opening-objective");
            mission = root.Q<Label>("opening-mission"); ability = root.Q<Label>("opening-ability"); help = root.Q<Label>("opening-help");
        }
        public void Draw(string who, string goal, string skill, string detention, string score, bool menu)
        {
            if (root == null) return;
            state.text = who; objective.text = goal; ability.text = detention.Length > 0 ? detention : skill; mission.text = score;
            help.text = menu ? "시간은 계속 흐릅니다 · Esc 돌아가기 / Backspace 로비" :
                "WASD 이동 · Shift 질주 · Space 점프 · 좌클릭 태클 · 우클릭 잡기 → 좌클릭 던지기\nR 복귀 · Tab 다른 말 · F3 초기화 · F4 상자 · F5 성벽 / Shift+F5 시소 · F6 체크포인트 · F7 승격 집결 시험 · Esc";
        }
        void OnDestroy() { if (owned != null) Destroy(owned); }
    }
}
