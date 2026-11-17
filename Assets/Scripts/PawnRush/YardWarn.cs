using UnityEngine;

namespace ChessFight.PawnRush
{
    // R685 (승규 11-16 "말 예고는 지워주고 나머지 예고도 실사로 바꿔"): the Royal Carriage Yard's warnings look like things that
    // are there instead of game marks. The neon floor sheets (a carriage's charge line, a stack row's gap, a hoist's well,
    // a slewing crane's sector, the giant chest's circle) are now the shadow of what is coming, a dark soft stain that
    // deepens as it closes in instead of flashing; what showed a safe place is a pale patch of swept sand; the lamps are
    // coloured-glass lanterns whose light flickers like a flame instead of blinking; the giant chest's ring is a wall of
    // dust. The timing is untouched: each obstacle still shows them when it did, from ObstacleClock.
    public static class YardWarn
    {
        // A shadow on the yard's paving (not black: lifted a touch by the bounce light) and swept sand.
        public static readonly Color Shadow = new Color(.05f, .04f, .035f, 1f);
        public static readonly Color Sand = new Color(.82f, .76f, .64f, 1f);
        // Coloured lantern glass, lit by a flame: softer than the old signal greens and reds.
        // 승규 11-17 "색이 희미해서 좀 더 색을 구분할 수 있게 진하게": deep, fully saturated glass so green, amber and red
        // never read alike, even through the bloom.
        public static readonly Color Go = new Color(.08f, 1f, .18f, 1f), Wait = new Color(1f, .55f, 0f, 1f), Stop = new Color(1f, .05f, .03f, 1f);
        // The giant chest's dust wall.
        public static readonly Color Dust = new Color(.74f, .67f, .55f, 1f);

        const double Tau = 2.0 * System.Math.PI;

        // A flame's flicker, .78 .. 1, on the shared clock (two sines that never line up, offset per lamp by `seed`), so
        // every PC sees the same lanterns.
        public static float Flicker(double now, float seed)
        {
            float a = Mathf.Sin((float)((now * 11.3 + seed) % Tau)), b = Mathf.Sin((float)((now * 23.9 + seed * 2.7) % Tau));
            return .89f + .11f * (.6f * a + .4f * b);
        }

        // How far a shadow has deepened, 0 .. 1, eased, from how close the thing casting it is (0 far .. 1 here).
        public static float Deepen(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        // `colour` at alpha `a`.
        public static Color With(Color colour, float a)
        {
            colour.a = a;
            return colour;
        }
    }
}
