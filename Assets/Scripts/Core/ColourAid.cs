using System;

namespace ChessFight.Network
{
    // R792 (user 11-23, "1 2 3 진행": settings → 접근성 → 색약 모드; Docs/Architecture/UI.md §12 R792): the colour
    // correction ("daltonization") of the game picture for a colour vision deficiency, as one 3 x 3 matrix on linear RGB
    // that GameLook hands URP's channel mixer. The colours such an eye cannot tell apart are found by simulating its
    // view (Machado, Oliveira and Fernandes 2009, severity 1), and what it loses is moved into the channels it still
    // sees (Fidaner, Lin and Ozguven's error shift): corrected = c + strength * E (c - S c). A grey, which every eye sees
    // alike, stays as it is. No Unity: the tests run it.
    public static class ColourAid
    {
        public const int Off = 0, Protan = 1, Deutan = 2, Tritan = 3;

        // The simulated view, rows = out R, G, B.
        static readonly float[][] Simulated =
        {
            null,
            new[] { 0.152286f, 1.052583f, -0.204868f, 0.114503f, 0.786281f, 0.099216f, -0.003882f, -0.048116f, 1.051998f },
            new[] { 0.367322f, 0.860646f, -0.227968f, 0.280085f, 0.672501f, 0.047413f, -0.011820f, 0.042940f, 0.968881f },
            new[] { 1.255528f, -0.076749f, -0.178779f, -0.078411f, 0.930809f, 0.147602f, 0.004733f, 0.691367f, 0.303900f },
        };

        // Where the lost difference goes: red-green into green and blue; blue-yellow into red and green.
        static readonly float[] RedGreenShift = { 0f, 0f, 0f, .7f, 1f, 0f, .7f, 0f, 1f };
        static readonly float[] BlueYellowShift = { 1f, 0f, .7f, 0f, 1f, .7f, 0f, 0f, 0f };

        // The correction for a kind at a strength (0..1), row-major (out = m[0..2] . rgb for red, ...). Off or no
        // strength: the identity.
        public static float[] Matrix(int kind, float strength)
        {
            var m = new float[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };
            if (kind < Protan || kind > Tritan || !(strength > 0f)) return m;
            strength = Math.Min(1f, strength);
            var s = Simulated[kind];
            var e = kind == Tritan ? BlueYellowShift : RedGreenShift;
            // lost = I - S
            var lost = new float[9];
            for (int i = 0; i < 9; i++) lost[i] = (i % 4 == 0 ? 1f : 0f) - s[i];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    float sum = 0f;
                    for (int k = 0; k < 3; k++) sum += e[r * 3 + k] * lost[k * 3 + c];
                    m[r * 3 + c] += strength * sum;
                }
            return m;
        }

        // URP's channel mixer works in the grading's own space: with ACES tonemapping (the game's look, Settings/
        // GameLook.asset) that is ACEScg (AP1 primaries), not linear sRGB. The same correction there: AP1 <- sRGB, the
        // matrix, sRGB <- AP1 (Unity's own constants, Core RP ShaderLibrary/ACES.hlsl).
        static readonly float[] SrgbToAp1 = { 0.61319f, 0.33951f, 0.04737f, 0.07021f, 0.91634f, 0.01345f, 0.02062f, 0.10957f, 0.86961f };
        static readonly float[] Ap1ToSrgb = { 1.70505f, -0.62179f, -0.08326f, -0.13026f, 1.14080f, -0.01055f, -0.02400f, -0.12897f, 1.15297f };

        public static float[] InAces(float[] m) => Multiply(SrgbToAp1, Multiply(m, Ap1ToSrgb));

        static float[] Multiply(float[] a, float[] b)
        {
            var o = new float[9];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    o[r * 3 + c] = a[r * 3] * b[c] + a[r * 3 + 1] * b[3 + c] + a[r * 3 + 2] * b[6 + c];
            return o;
        }

        public static void Apply(float[] m, float r, float g, float b, out float or, out float og, out float ob)
        {
            or = m[0] * r + m[1] * g + m[2] * b;
            og = m[3] * r + m[4] * g + m[5] * b;
            ob = m[6] * r + m[7] * g + m[8] * b;
        }
    }
}
