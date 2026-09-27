using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    public sealed class KingRushHud : MonoBehaviour
    {
        PanelSettings owned;
        VisualElement root;
        Label state, ability, doors, help;
        void Awake()
        {
            root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("KingRushHud"),
                Resources.Load<ThemeStyleSheet>("KingRushTheme"), null, new Vector2Int(1280, 720), out owned);
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;
            state = root.Q<Label>("kr-state"); ability = root.Q<Label>("kr-ability");
            doors = root.Q<Label>("kr-doors"); help = root.Q<Label>("kr-help");
        }
        public void Draw(string piece, int team, string section, bool enabled, bool active, float cooldown,
            bool implemented, string whiteDoor, string blackDoor, bool menu, int whiteBodies, int blackBodies)
        {
            if (root == null) return;
            string area = section == "Red1" ? "빨강 1" : section == "Blue1" ? "파랑 1" : "빨강 2";
            state.text = $"{(team == 0 ? "백팀" : "흑팀")} · {piece}  |  {area}  |  파랑 안 몸 수 {whiteBodies} : {blackBodies}";
            ability.text = !enabled ? "능력 잠김 · 파란 구역에서만 E" : !implemented ? "이 기물의 능력은 다음 단계에서 추가됩니다" :
                active ? "체크! · 범위 예고 중" : cooldown > 0 ? $"체크! · {cooldown * 12:0.0}초 뒤" : "E · 체크! (아군도 밀려납니다)";
            doors.text = $"백팀 출구 {whiteDoor}    /    흑팀 출구 {blackDoor}\nF7 백팀 완료 · F8 흑팀 완료\n실제 미션 대신 쓰는 시험 버튼입니다";
            help.text = menu ? "시험은 계속 진행 중 · Esc 돌아가기 / Backspace 로비로" :
                "WASD 이동 · Shift 질주 · Space 점프 · 좌클릭 태클 · 우클릭 잡기\nF4 능력장에 킹 + 더미 배치 · Tab 다른 캐릭터 · F2 백팀 킹\nR 현재 구간 부활 · F3 전체 초기화 · Esc";
        }
        void OnDestroy() { if (owned != null) Destroy(owned); }
    }
}
