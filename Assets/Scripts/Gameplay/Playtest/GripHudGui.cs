using UnityEngine;

namespace ChessFight.Gameplay
{
    // R642 (승규 님 11-14, "잡혔다는 것에 대한 UX/UI 추가"): Pawn Rush only, drawn beside the stamina gauge by the
    // match (MatchStaminaBar) and the offline Pawn Rush scenes (PlaytestSpawner, PawnRushMapTest).
    //   held     an ebony plate above the gauge, "잡혔다! 좌클릭 연타" in ivory and the escape bar in brass; it slams in
    //            (big to its size in 0.09 s) and the screen's edges pinch in for a moment, ebony with a brass line
    //   holding  a small ebony plate "잡는 중"
    //   both     a brass ring with an ivory dot where the hand is, on whichever piece is held
    // Ivory, ebony and brass like the chess pieces; no dust, no sound (sound is another teammate's).
    // Everything comes from IGripReadout, which every PC reads the same way, so a client's own pawn shows it too.
    public static class GripHudGui
    {
        static readonly Color Ebony = new Color(21 / 255f, 17 / 255f, 15 / 255f, .92f);
        static readonly Color Ivory = new Color(243 / 255f, 234 / 255f, 219 / 255f);
        static readonly Color Brass = new Color(201 / 255f, 160 / 255f, 74 / 255f);
        static readonly Color BrassEdge = new Color(201 / 255f, 160 / 255f, 74 / 255f, .45f);
        static readonly Color Track = new Color(243 / 255f, 234 / 255f, 219 / 255f, .12f);

        // The stamina gauge's own sizes (StaminaBarGui at 900 px): its top is about 76 px from the bottom.
        const float AboveGauge = 92f;

        static bool wasHeld, wasHolding;
        static float heldSince, holdingSince;
        static Texture2D ring;
        static GUIStyle big, small;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            wasHeld = wasHolding = false;
            heldSince = holdingSince = 0f;
        }

        /// <summary>Draws from OnGUI (Repaint only). <paramref name="label"/> carries the Korean font.</summary>
        public static void Draw(IGripReadout grip, GUIStyle label)
        {
            if (grip == null || Event.current == null || Event.current.type != EventType.Repaint) return;
            float now = Time.unscaledTime;
            bool held = grip.GripHeld;
            Vector3 heldAt = default, holdingAt = default;
            bool heldPoint = held && grip.HeldAt(out heldAt);
            bool holding = grip.HoldingPiece(out holdingAt);
            if (held && !wasHeld) heldSince = now;
            if (holding && !wasHolding) holdingSince = now;
            wasHeld = held;
            wasHolding = holding;
            if (!held && !holding) return;

            float s = Mathf.Clamp(Screen.height / 900f, .8f, 2.2f);
            Styles(label, s);
            var old = GUI.color;
            if (held) Pinch(now - heldSince, s);
            if (heldPoint) Marker(heldAt, now - heldSince, s);
            if (holding) Marker(holdingAt, now - holdingSince, s);
            if (held) HeldPlate(grip.GripEscape01, now - heldSince, s);
            else HoldingPlate(now - holdingSince, s);
            GUI.color = old;
        }

        // Slams in: 1.4 times its size down to 0.96 in 0.09 s, then settles on 1.
        static float Slam(float t, float from = 1.4f)
        {
            if (t < .09f) return Mathf.Lerp(from, .96f, 1f - (1f - t / .09f) * (1f - t / .09f));
            if (t < .16f) return Mathf.Lerp(.96f, 1f, (t - .09f) / .07f);
            return 1f;
        }

        static void HeldPlate(float escape, float t, float s)
        {
            float k = Slam(t);
            float w = 300f * s * k, h = 64f * s * k;
            var r = new Rect((Screen.width - w) * .5f, Screen.height - AboveGauge * s - 64f * s * .5f - h * .5f, w, h);
            Plate(r, s);
            big.fontSize = Mathf.RoundToInt(21f * s * k);
            Text(new Rect(r.x, r.y + 7f * s * k, r.width, 28f * s * k), "잡혔다! 좌클릭 연타", big);
            float inset = 18f * s * k, barH = Mathf.Max(4f, 9f * s * k);
            var bar = new Rect(r.x + inset, r.yMax - 14f * s * k - barH, r.width - 2f * inset, barH);
            Fill(bar, Track);
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(escape), bar.height), Brass);
        }

        static void HoldingPlate(float t, float s)
        {
            float k = Slam(t, 1.25f);
            float w = 128f * s * k, h = 32f * s * k;
            var r = new Rect((Screen.width - w) * .5f, Screen.height - AboveGauge * s - 32f * s * .5f - h * .5f, w, h);
            Plate(r, s);
            small.fontSize = Mathf.RoundToInt(16f * s * k);
            Text(r, "잡는 중", small);
        }

        // The screen's edges pinch in when a hand takes hold: in for 0.05 s, out again over about 0.35 s.
        static void Pinch(float t, float s)
        {
            float p = t < .05f ? t / .05f : Mathf.Exp(-(t - .05f) / .12f);
            if (p < .01f) return;
            float depth = Mathf.Min(Screen.width, Screen.height) * .075f * p;
            const int Strips = 10;
            float strip = depth / Strips;
            for (int i = 0; i < Strips; i++)
            {
                float a = .62f * p * (1f - i / (float)Strips);
                var c = new Color(Ebony.r, Ebony.g, Ebony.b, a);
                float o = i * strip;
                Fill(new Rect(0f, o, Screen.width, strip + .5f), c);
                Fill(new Rect(0f, Screen.height - o - strip, Screen.width, strip + .5f), c);
                Fill(new Rect(o, depth, strip + .5f, Screen.height - 2f * depth), c);
                Fill(new Rect(Screen.width - o - strip, depth, strip + .5f, Screen.height - 2f * depth), c);
            }
            // the brass line along the pinch's inner edge
            float line = Mathf.Max(1f, 2f * s);
            var b = new Color(Brass.r, Brass.g, Brass.b, .7f * p);
            Fill(new Rect(depth, depth, Screen.width - 2f * depth, line), b);
            Fill(new Rect(depth, Screen.height - depth - line, Screen.width - 2f * depth, line), b);
            Fill(new Rect(depth, depth, line, Screen.height - 2f * depth), b);
            Fill(new Rect(Screen.width - depth - line, depth, line, Screen.height - 2f * depth), b);
        }

        // A brass ring with an ivory dot on the hand: it lands big and small in 0.12 s, then breathes a little.
        static void Marker(Vector3 world, float t, float s)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= .1f) return;
            float k = t < .12f ? Mathf.Lerp(1.7f, 1f, t / .12f) : 1f + .07f * Mathf.Sin((t - .12f) * 6f);
            float size = 30f * s * k;
            var r = new Rect(sp.x - size * .5f, Screen.height - sp.y - size * .5f, size, size);
            GUI.color = new Color(0f, 0f, 0f, .35f);
            GUI.DrawTexture(new Rect(r.x + 1.5f * s, r.y + 1.5f * s, r.width, r.height), Ring());
            GUI.color = Color.white;
            GUI.DrawTexture(r, Ring());
        }

        static void Plate(Rect r, float s)
        {
            Fill(r, Ebony);
            float f = Mathf.Max(1f, s);
            Fill(new Rect(r.x, r.y, r.width, f), BrassEdge);
            Fill(new Rect(r.x, r.yMax - f, r.width, f), BrassEdge);
            Fill(new Rect(r.x, r.y + f, f, r.height - 2f * f), BrassEdge);
            Fill(new Rect(r.xMax - f, r.y + f, f, r.height - 2f * f), BrassEdge);
            // brass corner brackets, the stamina gauge's shape in the pieces' colours
            float o = 2f * s, th = Mathf.Max(2f, 3f * s);
            var b = new Rect(r.x - o, r.y - o, r.width + 2f * o, r.height + 2f * o);
            float len = Mathf.Min(10f * s, r.height / 2.4f) + o;
            Fill(new Rect(b.x, b.y, len, th), Brass); Fill(new Rect(b.x, b.y, th, len), Brass);
            Fill(new Rect(b.xMax - len, b.y, len, th), Brass); Fill(new Rect(b.xMax - th, b.y, th, len), Brass);
            Fill(new Rect(b.xMax - len, b.yMax - th, len, th), Brass); Fill(new Rect(b.xMax - th, b.yMax - len, th, len), Brass);
            Fill(new Rect(b.x, b.yMax - th, len, th), Brass); Fill(new Rect(b.x, b.yMax - len, th, len), Brass);
        }

        static void Text(Rect r, string text, GUIStyle style)
        {
            GUI.color = new Color(0f, 0f, 0f, .6f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1.5f, r.width, r.height), text, style);
            GUI.color = Ivory;
            GUI.Label(r, text, style);
        }

        static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        static void Styles(GUIStyle label, float s)
        {
            if (big == null)
            {
                big = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = false, wordWrap = false };
                big.normal.textColor = Color.white;
                small = new GUIStyle(big);
            }
        }

        // 64 x 64: a brass ring (radius 21 to 28) and an ivory dot (radius 6), both with a soft 1.2 px edge.
        static Texture2D Ring()
        {
            if (ring != null) return ring;
            const int N = 64;
            ring = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "Grip marker", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            float c = (N - 1) * .5f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float ringA = Mathf.Clamp01((d - 20.4f) / 1.2f) * Mathf.Clamp01((28.6f - d) / 1.2f);
                    float dotA = Mathf.Clamp01((6.6f - d) / 1.2f);
                    Color col = dotA > 0f ? Color.Lerp(Brass, Ivory, dotA) : Brass;
                    col.a = Mathf.Max(ringA, dotA);
                    px[y * N + x] = col;
                }
            ring.SetPixels32(px);
            ring.Apply(false, true);
            return ring;
        }
    }
}
