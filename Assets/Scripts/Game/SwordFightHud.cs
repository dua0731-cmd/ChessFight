using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // View only; this assembly never depends on a ragdoll implementation.
    public sealed class SwordFightHud : MonoBehaviour
    {
        public event Action Resume, Leave, SwitchControls;
        VisualElement root, menu;
        Label score, clock, team, status, heading, controls, controlHelp;
        Button resume, leave, switchControls, pressed;
        int actionFrame = -1;
        PanelSettings owned;
        void Awake()
        {
            root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("SwordFightHud"),
                Resources.Load<ThemeStyleSheet>("SwordFightTheme"), null, new Vector2Int(1280, 720), out owned);
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;
            score = root.Q<Label>("sf-score"); clock = root.Q<Label>("sf-clock");
            team = root.Q<Label>("sf-team"); status = root.Q<Label>("sf-status");
            heading = root.Q<Label>("sf-heading"); menu = root.Q("sf-menu");
            resume = root.Q<Button>("sf-resume"); resume.clicked += () => Trigger(Resume);
            leave = root.Q<Button>("sf-leave"); leave.clicked += () => Trigger(Leave);
            switchControls = root.Q<Button>("sf-switch-controls"); switchControls.clicked += () => Trigger(SwitchControls);
            controls = root.Q<Label>("sf-controls"); controlHelp = root.Q<Label>("sf-control-help");
        }
        void Trigger(Action action)
        { if (actionFrame == Time.frameCount) return; actionFrame = Time.frameCount; action?.Invoke(); }
        void LateUpdate()
        {
            // Same legacy safety net as the lobby; no optional uGUI/Input System dependency.
            if (root?.panel == null || menu.resolvedStyle.display == DisplayStyle.None || UnityEngine.Cursor.lockState == CursorLockMode.Locked) { pressed = null; return; }
            Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            VisualElement hit = root.panel.Pick(point);
            while (hit != null && !(hit is Button)) hit = hit.parent;
            if (Input.GetMouseButtonDown(0)) pressed = hit as Button;
            if (Input.GetMouseButtonUp(0))
            {
                if (pressed != null && hit == pressed) Trigger(pressed == resume ? Resume : pressed == switchControls ? SwitchControls : Leave);
                pressed = null;
            }
        }
        public void Draw(int white, int black, float remaining, int target, int localTeam, string localStatus, bool menuOpen, bool finished, bool classic = false)
        {
            if (root == null) return;
            score.text = $"백팀  {white}   :   {black}  흑팀";
            clock.text = $"소드파이트 · 폰     {Mathf.FloorToInt(remaining / 60):0}:{Mathf.FloorToInt(remaining % 60):00}   /   {target}점";
            team.text = localTeam == 0 ? "나는 백팀 · 폰" : localTeam == 1 ? "나는 흑팀 · 폰" : "입장 중";
            status.text = localStatus;
            status.style.display = menuOpen || finished ? DisplayStyle.None : DisplayStyle.Flex;
            menu.style.display = menuOpen || finished ? DisplayStyle.Flex : DisplayStyle.None;
            heading.text = finished ? white == black ? "무승부" : white > black ? "백팀 승리!" : "흑팀 승리!" : "소드파이트";
            resume.style.display = finished ? DisplayStyle.None : DisplayStyle.Flex;
            controls.text = classic ? "[클릭 베기] 좌클릭 한 번 = 자동 베기 · F6 조작 전환" : "[물리 드래그] 좌클릭 유지 + 마우스 = 베기 · F6 조작 전환";
            controlHelp.text = classic ? "이전 방식과 손맛 비교 · 누르고만 있어서는 연속 공격하지 않습니다" : "무게감 있는 드래그 · 넓고 빠른 베기만 넉다운 · 놓으면 수납";
            switchControls.text = classic ? "물리 드래그로 전환 (F6)" : "이전 클릭 베기로 전환 (F6)";
            switchControls.style.display = finished ? DisplayStyle.None : DisplayStyle.Flex;
        }
        void OnDestroy() { if (owned != null) Destroy(owned); }
    }
}
