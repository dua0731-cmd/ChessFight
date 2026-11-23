using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R703 (the user's options 1 and 2, 2026-11-17 - a player left behind in an obstacle section has no way back;
    // "쉬워짐의 뜻은 장애물이 꺼진다기 보단 쉬운 길이 열리는거야. 예를들어 정원에 배를 건너는 파트에선 도개교같은 다리가
    // 내려와 길이 생기고, 대계단의 오징어게임 다리의 경우 길이 이어지던가 모두 정답 발판이 되는 식 ... 장애물이 꺼지는
    // 것도 괜찮은 것 같아"): the relief of an obstacle section, given once the mini-game after it has been cleared
    // (PawnRushRules.Relief; MiniGameSection.Rules.cs decides when). Two things happen in the section:
    //
    //   an easy way opens   the garden's boat crossing gets a bridge let down across the reservoir (the boats go);
    //                       the grand stair hall's glass stepping stones all hold.
    //   what strikes pawns  the garden's spin bars, geysers, sluice gates with their current and robot arms; the
    //   switches off        hall's rolling statues, falling chandeliers, falling pieces, pendulums, wind vents and
    //                       guard knights; the carriage yard's roaming teams and its slamming chest.
    //
    // R783 (the user 2026-11-23: the garden's boats went and no bridge was there to cross by, the carriage yard had
    // no easy way at all - "플레이어가 컨트롤 부담 없이 바로바로 건널 수 있도록 현재 구현된 모든 맵에 길을 기획하고
    // 만들어줘"): with the relief given a section is WALKED from its entry to its exit. What a pawn had to ride,
    // jump, time or push goes as well - the yard's two waterwheels, conveyor, storage rows, timber cranes and loft
    // hoists, the hall's jump pad and clock gate doors; the yard's final gate opens; the garden's drums and the
    // hall's floating stones come to rest - and ReliefWay lays decks over every gap and up every height that is
    // left (its plan, section by section, is there).
    //
    // Every PC does this for itself when it sees the mini-game cleared (the cleared flags are the host's), so nothing
    // more is sent; a thing switched off is its object made inactive.
    public static class SectionRelief
    {
        static readonly HashSet<PawnRushSection> done = new HashSet<PawnRushSection>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => done.Clear();

        public static bool Given(PawnRushSection section) => section != null && done.Contains(section);

        // Once a section. Returns what it did, for the log and the test ("" for nothing to do).
        public static string Apply(PawnRushSection section)
        {
            if (section == null || done.Contains(section)) return "";
            done.Add(section);
            int off = 0, ways = 0;

            // What strikes pawns: off.
            off += Off<GardenSpinBarFit>(section);
            off += Off<GardenGeyser>(section);
            off += Off<RobotSweep>(section);
            off += Off<WaterCurrent>(section);
            foreach (var span in section.GetComponentsInChildren<ObstacleSpan>(true))
                if (span.name.StartsWith("Sluice gate") && span.gameObject.activeSelf) { span.gameObject.SetActive(false); off++; }
            off += Off<RollingSpawner>(section);
            off += Off<ChandelierRain>(section);
            off += Off<FallingChessPiece>(section);
            off += Off<AirVent>(section);
            off += Off<KnightCavalryCharge>(section);
            foreach (var swing in section.GetComponentsInChildren<PendulumSwing>(true))
            {
                // The whole pendulum: its frame stands a level above the swinging head.
                var context = swing.GetComponentInParent<ObstacleContext>(true);
                var whole = context != null ? context.gameObject : swing.gameObject;
                if (whole.activeSelf) { whole.SetActive(false); off++; }
            }
            off += Off<CarriageFleet>(section);
            off += Off<SlamChest>(section);
            // R783: what a walk through the section still met.
            off += Off<HorizontalWaterwheel>(section);
            off += Off<VerticalWaterwheel>(section);
            off += Off<CargoConveyor>(section);
            off += Off<StackRow>(section);
            off += Off<SlewCrane>(section);
            off += Off<PalletHoist>(section);
            foreach (var context in section.GetComponentsInChildren<ObstacleContext>(true))
                if (context.name.Contains("Jump pad") && context.gameObject.activeSelf) { context.gameObject.SetActive(false); off++; }
            foreach (var motion in section.GetComponentsInChildren<ObstacleMotion>(true))
            {
                // The clock gate's two doors (they took turns to shut).
                if (motion.name == "Left gate" || motion.name == "Right gate")
                {
                    if (motion.gameObject.activeSelf) { motion.gameObject.SetActive(false); off++; }
                }
                // The garden's floating drums and the hall's floating stones come to rest: a deck lies over them.
                else if (motion.GetComponent<GardenDrumDip>() != null || motion.name.StartsWith("Floating stone"))
                {
                    if (ReliefWay.Still(motion)) off++;
                }
            }
            // A gate that had to be pushed open (the yard's last) swings open by itself.
            foreach (var gate in section.GetComponentsInChildren<PushGate>(true))
                if (!gate.IsOpen && gate.GetComponent<ReliefOpen>() == null) { gate.gameObject.AddComponent<ReliefOpen>(); off++; }

            // An easy way opens.
            foreach (var stones in section.GetComponentsInChildren<GuessBridge>(true)) { stones.Relieve(); ways++; }
            // The reservoir's boats go; its bridge is one of ReliefWay's decks.
            foreach (var lanes in section.GetComponentsInChildren<BoatLanes>(true))
                if (lanes.gameObject.activeSelf) { lanes.gameObject.SetActive(false); off++; }
            ways += ReliefWay.Open(section);

            string what = $"{section.DisplayName}: 쉬운 길 {ways}곳 · 꺼진 장애물 {off}개";
            Debug.Log("[PawnRush] 낙오자 구제 - " + what);
            if (off + ways > 0) SectionReliefNotice.Show(section.DisplayName);
            return what;
        }

        static int Off<T>(PawnRushSection section) where T : Component
        {
            int n = 0;
            foreach (var part in section.GetComponentsInChildren<T>(true))
                if (part.gameObject.activeSelf) { part.gameObject.SetActive(false); n++; }
            return n;
        }
    }

    // Says so on every screen for a few seconds: "○○ - 쉬운 길이 열렸습니다".
    public sealed class SectionReliefNotice : MonoBehaviour
    {
        const float Seconds = 6f;
        static SectionReliefNotice current;
        string line = "";
        float until = -1f;
        GUIStyle style;

        public static void Show(string sectionName)
        {
            if (current == null)
            {
                var go = new GameObject("Section relief notice");
                current = go.AddComponent<SectionReliefNotice>();
            }
            current.line = Loc.F("{0} · 쉬운 길이 열리고 장애물이 꺼졌습니다", Loc.T(sectionName));
            current.until = Time.unscaledTime + Seconds;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || Time.unscaledTime > until || LoadingScreenView.Showing) return;
            float unit = Screen.height / 1080f;
            if (style == null) style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = false };
            style.fontSize = Mathf.Max(11, Mathf.RoundToInt(24f * unit));
            float left = until - Time.unscaledTime, alpha = Mathf.Clamp01(left / .6f) * Mathf.Clamp01((Seconds - left) / .3f);
            var box = new Rect(Screen.width * .5f - 430f * unit, 150f * unit, 860f * unit, 52f * unit);
            var was = GUI.color;
            GUI.color = new Color(.06f, .08f, .05f, .8f * alpha);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(.55f, .9f, .6f, alpha);
            GUI.DrawTexture(new Rect(box.x, box.y, 5f * unit, box.height), Texture2D.whiteTexture);
            GUI.color = new Color(.92f, 1f, .9f, alpha);
            GUI.Label(box, line, style);
            GUI.color = was;
        }
    }
}
