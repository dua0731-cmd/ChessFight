using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The stamina gauge: a ring floating beside each local player's pawn, drawn through that
    /// player's own camera, so in split screen each half shows its own. It fills clockwise from the
    /// top, goes green to yellow to red as it empties, flashes while the pawn is winded, and fades
    /// away once it has been full for a moment. Being held adds the "mash to escape" prompt under it.
    /// Online it reads the stamina the host sends, so a client sees its own gauge too.
    /// LabGame adds this at start-up; nothing in the scene needs to change.
    /// </summary>
    public class StaminaHud : MonoBehaviour
    {
        public LabGame game;

        const int TextureSize = 128;
        const float ShowAfterFull = 0.8f;

        sealed class Gauge
        {
            public Texture2D ring;
            public Color32[] pixels;
            public float painted = -1f;
            public float fill = 1f;
            public float alpha;
            public float fullFor = 99f;
        }

        static readonly Color Green = new Color(0.4f, 0.92f, 0.46f);
        static readonly Color Yellow = new Color(1f, 0.8f, 0.22f);
        static readonly Color Red = new Color(1f, 0.32f, 0.26f);
        static readonly Color32 Track = new Color32(20, 24, 30, 165);
        static readonly Color32 Lit = new Color32(255, 255, 255, 255);

        readonly Dictionary<RagdollPawn, Gauge> gauges = new Dictionary<RagdollPawn, Gauge>();
        readonly List<RagdollPawn> gone = new List<RagdollPawn>();
        GUIStyle label;
        Texture2D white;

        void Update()
        {
            if (game == null) return;
            float dt = Time.unscaledDeltaTime;
            foreach (var (pawn, _) in game.HudTargets())
            {
                if (!gauges.TryGetValue(pawn, out var g)) gauges[pawn] = g = new Gauge();
                float stamina = pawn.Stamina;
                // Eased, so a tap of stamina (a thrash, a dive) slides the ring instead of snapping it.
                g.fill = Mathf.Lerp(g.fill, stamina, 1f - Mathf.Exp(-14f * dt));
                bool busy = stamina < 0.995f || pawn.Sprinting || pawn.Climbing || pawn.BeingHeld || pawn.Exhausted;
                g.fullFor = busy ? 0f : g.fullFor + dt;
                float want = g.fullFor < ShowAfterFull ? 1f : 0f;
                g.alpha = Mathf.MoveTowards(g.alpha, want, dt * (want > g.alpha ? 6f : 2f));
            }
            gone.Clear();
            foreach (var pawn in gauges.Keys)
                if (pawn == null) gone.Add(pawn);
            foreach (var pawn in gone)
            {
                if (gauges[pawn].ring != null) Destroy(gauges[pawn].ring);
                gauges.Remove(pawn);
            }
        }

        void OnDestroy()
        {
            foreach (var g in gauges.Values)
                if (g.ring != null) Destroy(g.ring);
            if (white != null) Destroy(white);
        }

        void OnGUI()
        {
            if (game == null || game.AutoTest || Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            foreach (var (pawn, view) in game.HudTargets())
            {
                if (!gauges.TryGetValue(pawn, out var g) || g.alpha <= 0.01f) continue;
                var cam = view != null ? view.Cam : null;
                if (cam == null || !cam.enabled || view.freeMode) continue;
                Draw(pawn, g, cam);
            }
        }

        void Draw(RagdollPawn pawn, Gauge g, Camera cam)
        {
            Vector3 head = pawn.bodies[(int)BodyId.Head].transform.position;
            Vector3 sp = cam.WorldToScreenPoint(head + Vector3.up * 0.08f);
            if (sp.z <= 0f) return;

            // GUI space is top-down; the camera's pixel rect is bottom-up.
            Rect px = cam.pixelRect;
            var area = new Rect(px.x, Screen.height - px.yMax, px.width, px.height);
            float size = Mathf.Clamp(area.height * 0.05f, 32f, 96f);
            // Up and to the right of the head, like a thought bubble, and never off its own half.
            float cx = Mathf.Clamp(sp.x + size * 0.95f, area.xMin + size * 0.6f, area.xMax - size * 0.6f);
            float cy = Mathf.Clamp(Screen.height - sp.y - size * 0.3f, area.yMin + size * 0.6f, area.yMax - size * 1.6f);

            if (Mathf.Abs(g.fill - g.painted) > 0.004f) Paint(g, g.fill);

            float stamina = pawn.Stamina;
            Color tint = stamina > 0.5f ? Color.Lerp(Yellow, Green, (stamina - 0.5f) * 4f)
                : stamina > 0.25f ? Yellow
                : Color.Lerp(Red, Yellow, stamina * 4f);
            float alpha = g.alpha;
            if (pawn.Exhausted)
            {
                // Winded: red and blinking until it has refilled enough to sprint again.
                tint = Red;
                alpha *= 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 7f));
            }
            var rect = new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size);
            var old = GUI.color;
            GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
            GUI.DrawTexture(rect, g.ring);

            float line = size * 0.62f;
            if (pawn.BeingHeld)
            {
                // The one thing a held player needs to know, right where they are already looking.
                GUI.color = new Color(1f, 1f, 1f, alpha);
                label.fontSize = Mathf.RoundToInt(Mathf.Clamp(size * 0.32f, 12f, 26f));
                var text = new Rect(cx - size * 2f, cy + line, size * 4f, size * 0.45f);
                Shadowed(text, "잡힘! 좌클릭 연타");
                float escape = Mathf.Clamp01(pawn.EscapeProgress);
                var bar = new Rect(cx - size * 0.9f, cy + line + size * 0.48f, size * 1.8f, Mathf.Max(4f, size * 0.12f));
                GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
                GUI.DrawTexture(bar, white);
                GUI.color = new Color(1f, 0.85f, 0.3f, alpha);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * escape, bar.height), white);
            }
            else if (pawn.Exhausted)
            {
                GUI.color = new Color(1f, 0.55f, 0.5f, alpha);
                label.fontSize = Mathf.RoundToInt(Mathf.Clamp(size * 0.3f, 11f, 24f));
                Shadowed(new Rect(cx - size, cy + line, size * 2f, size * 0.42f), "지침");
            }
            GUI.color = old;
        }

        void Shadowed(Rect rect, string text)
        {
            var c = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, c.a * 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, label);
            GUI.color = c;
            GUI.Label(rect, text, label);
        }

        /// <summary>
        /// Bakes the ring for one fill level: the lit arc in white (tinted when drawn) over a dark
        /// track, anti-aliased on every edge. 16k pixels, and only when the value actually moves.
        /// </summary>
        static void Paint(Gauge g, float fill)
        {
            const int n = TextureSize;
            if (g.ring == null)
            {
                g.ring = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                g.pixels = new Color32[n * n];
            }
            fill = Mathf.Clamp01(fill);
            float c = n * 0.5f, outer = c - 1.5f, inner = outer * 0.62f;
            const float Turn = Mathf.PI * 2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01(outer - r + 0.5f) * Mathf.Clamp01(r - inner + 0.5f);
                    if (edge <= 0f)
                    {
                        g.pixels[y * n + x] = default;
                        continue;
                    }
                    // Angle from twelve o'clock, clockwise (texture rows run bottom to top).
                    float a = Mathf.Atan2(dx, dy);
                    if (a < 0f) a += Turn;
                    float on;
                    if (fill >= 0.999f) on = 1f;
                    else if (fill <= 0.001f) on = 0f;
                    else on = Mathf.Clamp01((fill * Turn - a) * r + 0.5f) * Mathf.Clamp01(a * r + 0.5f);
                    Color32 col = Color32.Lerp(Track, Lit, on);
                    col.a = (byte)(col.a * edge);
                    g.pixels[y * n + x] = col;
                }
            }
            g.ring.SetPixels32(g.pixels);
            g.ring.Apply(false);
            g.painted = fill;
        }

        void EnsureStyles()
        {
            if (label != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 16);
            label = new GUIStyle(GUI.skin.label) { font = font, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }
    }
}
