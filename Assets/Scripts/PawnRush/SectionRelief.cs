using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
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
    // What a pawn rides or walks on stays as it is (boats elsewhere, bobbing platforms, floating stones, jump pads,
    // hoists, waterwheels, conveyors). Every PC does this for itself when it sees the mini-game cleared (the cleared
    // flags are the host's), so nothing more is sent; a thing switched off is its object made inactive.
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

            // An easy way opens.
            foreach (var stones in section.GetComponentsInChildren<GuessBridge>(true)) { stones.Relieve(); ways++; }
            foreach (var lanes in section.GetComponentsInChildren<BoatLanes>(true))
                if (lanes.gameObject.activeSelf && ReliefBridge.Over(lanes)) ways++;

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

    // The garden's easy way: a bridge let down across the reservoir where the boats crossed (the boats go). As wide
    // as two pawns abreast with a rail on either side, its deck at the quay's height, from the quay's edge to the far
    // bank. Built from boxes when the relief is given, on every PC; it comes down from Drop metres up in Seconds.
    public sealed class ReliefBridge : MonoBehaviour
    {
        const float Half = 2f, Thick = .4f, RailHigh = 1.1f, RailThick = .25f, Drop = 9f, Seconds = 1.4f;

        Vector3 home;
        float since;

        public static bool Over(BoatLanes lanes)
        {
            var beat = lanes.transform.parent;
            var boats = lanes.GetComponentsInChildren<GardenBoat>(true);
            if (beat == null || boats.Length == 0) return false;
            // The water to cross: the reservoir's kill volume (built axis-aligned in the beat's own frame).
            BoxCollider water = null;
            foreach (var kill in beat.GetComponentsInChildren<KillVolume>(true))
                if (kill.name.Contains("reservoir")) water = kill.GetComponent<BoxCollider>();
            if (water == null) return false;
            Vector3 middle = beat.InverseTransformPoint(water.transform.TransformPoint(water.center));
            float z0 = middle.z - water.size.z * .5f + .3f, z1 = middle.z + water.size.z * .5f + .3f;
            float deck = beat.InverseTransformPoint(boats[0].transform.position).y;
            float x = 0f;
            foreach (var gap in beat.GetComponentsInChildren<IntendedGap>(true)) x = beat.InverseTransformPoint(gap.transform.position).x;

            // The boats' own planks and hull for its looks.
            Material planks = null, hull = null;
            foreach (var r in boats[0].GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "Deck") planks = r.sharedMaterial;
                if (r.name == "Hull") hull = r.sharedMaterial;
            }
            lanes.gameObject.SetActive(false);

            var root = new GameObject("Relief bridge (easy way)");
            root.transform.SetParent(beat, false);
            root.transform.localPosition = new Vector3(x, deck, (z0 + z1) * .5f);
            float length = z1 - z0;
            Box(root.transform, "Deck", new Vector3(0f, -Thick * .5f, 0f), new Vector3(Half * 2f, Thick, length), planks ?? hull, true);
            Box(root.transform, "Rail W", new Vector3(-Half - RailThick * .5f, RailHigh * .5f - Thick, 0f), new Vector3(RailThick, RailHigh + Thick, length), hull ?? planks, false);
            Box(root.transform, "Rail E", new Vector3(Half + RailThick * .5f, RailHigh * .5f - Thick, 0f), new Vector3(RailThick, RailHigh + Thick, length), hull ?? planks, false);
            var bridge = root.AddComponent<ReliefBridge>();
            bridge.home = root.transform.localPosition;
            root.transform.localPosition = bridge.home + Vector3.up * Drop;
            return true;
        }

        static void Box(Transform parent, string name, Vector3 at, Vector3 size, Material material, bool floor)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = at;
            box.transform.localScale = size;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
            if (floor) box.AddComponent<CourseFloor>();
            box.AddComponent<NoClimbSurface>();
        }

        void Update()
        {
            since += Time.deltaTime;
            float t = Mathf.Clamp01(since / Seconds);
            // Eased down, with a little settle at the end.
            float eased = 1f - (1f - t) * (1f - t);
            transform.localPosition = home + Vector3.up * (Drop * (1f - eased));
            if (t >= 1f) enabled = false;
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
            current.line = sectionName + " · 쉬운 길이 열리고 장애물이 꺼졌습니다";
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
