using System;

namespace ChessFight.Game
{
    // R583 (11-12, "오케이 이제 쫀득하게 코딩으로 사운드 넣어줘 ai 티가 나는 소리는 안돼"): the course reveal's sounds, made
    // here in code from plain physics-like parts (no recordings and no generated takes): noise struck through damped
    // resonators for wood and stiff card (a split-flap board's flaps), sines that drop in pitch for thumps and drums,
    // swept band-passed noise for air, and two or three faint early reflections so nothing sounds bone dry. Nothing
    // rings bright (no bells, chimes or coins). Every sound is a mono buffer with its hit at Lead(kind) seconds.
    // Plain System maths only (no UnityEngine), so the same code renders offline for checking.
    public static class CourseRevealSynth
    {
        public enum Kind
        {
            Open,       // the screen darkens as the reveal starts: one deep felt-mallet drum
            Title,      // the title plate swipes in and taps into place
            Slam,       // a panel slides in (air) and lands (a heavy board)
            Flap,       // one cut between pictures: a split-flap board's flap falls and rebounds
            Lock,       // a panel stops on this round's picture: slap, wooden thunk, sub, latch, and the light slice
            Dot,        // a section square fills: a small wooden bead
            CountIn,    // the countdown plate drops in
            Count,      // 3 and 2: a low drum with a wooden knock
            CountLast,  // 1: the same, deeper and longer
        }

        // Seconds from a buffer's start to its hit.
        public static float Lead(Kind kind)
        {
            switch (kind)
            {
                case Kind.Slam: return SlamAir;
                case Kind.Title: return TitleAir;
                default: return Pre;
            }
        }

        // How long the air before a panel's landing and the title's tap lasts (the picture's PanelSlide and the time
        // the title first reaches its place), and the tiny pre-roll before every other hit.
        public const float SlamAir = .22f, TitleAir = .0774f, Pre = .0015f;

        // `pitch` (Count only): the drum's note in Hz.
        public static float[] Make(Kind kind, int rate, float pitch = 98f)
        {
            switch (kind)
            {
                case Kind.Open: return Open(rate);
                case Kind.Title: return Title(rate);
                case Kind.Slam: return Slam(rate);
                case Kind.Flap: return Flap(rate);
                case Kind.Lock: return Lock(rate);
                case Kind.Dot: return Dot(rate);
                case Kind.CountIn: return CountIn(rate);
                case Kind.Count: return Drum(rate, pitch, false);
                default: return Drum(rate, pitch, true);
            }
        }

        // ---- the sounds ----

        static float[] Open(int rate)
        {
            var b = new float[(int)(.9f * rate)];
            var n = new Noise(11);
            Add(b, Thump(rate, b.Length, Pre, 92f, 52f, .06f, .006f, .26f, 2f), 1f);
            Add(b, Breath(rate, b.Length, Pre, 170f, .012f, .2f, n), .35f);
            Add(b, Mode(rate, b.Length, Pre, 330f, .05f, .003f, n), .16f);
            Add(b, Mode(rate, b.Length, Pre, 610f, .03f, .002f, n), .08f);
            return Finish(b, rate, .2f);
        }

        static float[] Title(int rate)
        {
            float hit = TitleAir;
            var b = new float[(int)((hit + .3f) * rate)];
            var n = new Noise(23);
            // the swipe: fastest at once (the plate eases out), its colour rising
            Add(b, Air(rate, b.Length, 0f, hit, 1100f, 2800f, 1.4f, .008f, 1.6f, n), .22f);
            // the tap: a hard wooden plate set down
            Add(b, Mode(rate, b.Length, hit, 760f, .028f, .0018f, n), .5f);
            Add(b, Mode(rate, b.Length, hit, 1340f, .018f, .0012f, n), .3f);
            Add(b, Thump(rate, b.Length, hit, 210f, 130f, .01f, .0005f, .04f), .4f);
            Add(b, Click(rate, b.Length, hit, 2600f, .0012f, n), .25f);
            return Finish(b, rate, .16f);
        }

        static float[] Slam(int rate)
        {
            float hit = SlamAir;
            var b = new float[(int)((hit + .35f) * rate)];
            var n = new Noise(37);
            // air while it slides: loudest as it sets off (the picture eases out), falling in pitch as it slows
            Add(b, Air(rate, b.Length, 0f, hit + .01f, 1300f, 360f, 1.1f, .012f, 2f, n), .22f);
            // the landing: a heavy board on wood
            Add(b, Thump(rate, b.Length, hit, 118f, 58f, .02f, .0008f, .08f, 1.8f), .85f);
            Add(b, Breath(rate, b.Length, hit, 520f, .0008f, .04f, n), .4f);
            Add(b, Mode(rate, b.Length, hit, 420f, .035f, .0015f, n), .22f);
            Add(b, Click(rate, b.Length, hit, 2200f, .001f, n), .2f);
            return Finish(b, rate, .2f);
        }

        static float[] Flap(int rate)
        {
            var b = new float[(int)(.1f * rate)];
            var n = new Noise(53);
            float at = Pre;
            // the flap slaps down (stiff card: two inharmonic modes and a tick), a small plastic body under it ...
            Add(b, Mode(rate, b.Length, at, 1650f, .016f, .0012f, n), .6f);
            Add(b, Mode(rate, b.Length, at, 2930f, .009f, .0008f, n), .32f);
            Add(b, Click(rate, b.Length, at, 3200f, .0009f, n), .45f);
            Add(b, Thump(rate, b.Length, at, 270f, 190f, .008f, .0004f, .016f), .22f);
            // ... and rebounds once, 9 ms later
            Add(b, Mode(rate, b.Length, at + .009f, 1730f, .011f, .0008f, n), .26f);
            Add(b, Click(rate, b.Length, at + .009f, 3200f, .0006f, n), .18f);
            return Finish(b, rate, .1f);
        }

        static float[] Lock(int rate)
        {
            var b = new float[(int)(.7f * rate)];
            var n = new Noise(71);
            float at = Pre;
            // a big flap slap
            Add(b, Mode(rate, b.Length, at, 1080f, .03f, .002f, n), .5f);
            Add(b, Mode(rate, b.Length, at, 2050f, .017f, .0014f, n), .32f);
            Add(b, Click(rate, b.Length, at, 2600f, .0014f, n), .55f);
            // the wooden thunk and the sub under it (the panel's pop)
            Add(b, Thump(rate, b.Length, at, 150f, 74f, .025f, .0008f, .1f, 2f), .95f);
            Add(b, Thump(rate, b.Length, at, 62f, 54f, .05f, .004f, .16f), .45f);
            // the latch settling (the pop is back to size by then)
            Add(b, Mode(rate, b.Length, at + .052f, 1900f, .011f, .0009f, n), .26f);
            Add(b, Click(rate, b.Length, at + .052f, 3000f, .0008f, n), .2f);
            // the light slice crossing the panel in 0.14 s
            Add(b, Air(rate, b.Length, at, .14f, 1800f, 4200f, 1.8f, .03f, 0f, n, true), .08f);
            return Finish(b, rate, .22f);
        }

        static float[] Dot(int rate)
        {
            var b = new float[(int)(.07f * rate)];
            var n = new Noise(89);
            Add(b, Mode(rate, b.Length, Pre, 2350f, .011f, .0008f, n), .5f);
            Add(b, Mode(rate, b.Length, Pre, 3870f, .006f, .0006f, n), .22f);
            Add(b, Thump(rate, b.Length, Pre, 420f, 310f, .005f, .0003f, .011f), .25f);
            return Finish(b, rate, .08f);
        }

        static float[] CountIn(int rate)
        {
            var b = new float[(int)(.4f * rate)];
            var n = new Noise(97);
            Add(b, Thump(rate, b.Length, Pre, 128f, 82f, .02f, .002f, .08f, 1.6f), .7f);
            Add(b, Air(rate, b.Length, Pre, .09f, 700f, 260f, 1.2f, .004f, 2f, n), .3f);
            Add(b, Mode(rate, b.Length, Pre, 640f, .025f, .0015f, n), .18f);
            return Finish(b, rate, .16f);
        }

        // A low drum: a felt mallet's tap, the head dropping to `note`, an inharmonic wooden knock of the shell, a sub
        // under it and a short dark room tail. The last one is deeper and rings longer.
        static float[] Drum(int rate, float note, bool last)
        {
            float decay = last ? .42f : .25f;
            var b = new float[(int)((decay * 2.6f + .1f) * rate)];
            var n = new Noise(last ? 113u : 101u);
            float at = Pre;
            Add(b, Thump(rate, b.Length, at, note * 2.3f, note, .03f, .0008f, decay, 2.4f), 1f);
            Add(b, Thump(rate, b.Length, at, note * .55f, note * .5f, .05f, .004f, decay * .8f), last ? .6f : .38f);
            Add(b, Mode(rate, b.Length, at, 1250f, .014f, .0014f, n), .32f);
            Add(b, Click(rate, b.Length, at, 2400f, .0012f, n), .3f);
            Add(b, Mode(rate, b.Length, at, note * 4.13f, .06f, .002f, n), .26f);
            Add(b, Mode(rate, b.Length, at, note * 6.71f, .032f, .0015f, n), .12f);
            Add(b, Breath(rate, b.Length, at, 230f, .006f, decay * .8f, n), .14f);
            return Finish(b, rate, last ? .3f : .24f);
        }

        // ---- parts ----

        // A sine that starts at f0 and glides down to f1 (time constant `glide`), with an attack and an exponential decay;
        // `drive` > 0 rounds it towards a square (soft clipping), adding the overtones small speakers need to hear it.
        static float[] Thump(int rate, int length, float at, float f0, float f1, float glide, float attack, float decay, float drive = 0f)
        {
            var o = new float[length];
            int i0 = (int)(at * rate);
            double phase = 0;
            for (int i = i0; i < length; i++)
            {
                double t = (i - i0) / (double)rate;
                double f = f1 + (f0 - f1) * Math.Exp(-t / glide);
                phase += 2 * Math.PI * f / rate;
                double env = (1 - Math.Exp(-t / attack)) * Math.Exp(-t / decay);
                if (t > attack * 8 && env < 1e-5) break;
                double s = Math.Sin(phase);
                if (drive > 0f) s = Math.Tanh(drive * s) / Math.Tanh(drive);
                o[i] = (float)(env * s);
            }
            return o;
        }

        // Noise struck through a two-pole resonator (one mode of a struck wooden or card body).
        static float[] Mode(int rate, int length, float at, float freq, float decay, float burst, Noise n)
        {
            var o = new float[length];
            int i0 = (int)(at * rate), hit = Math.Max(1, (int)(burst * rate));
            double w = 2 * Math.PI * freq / rate, r = Math.Exp(-1.0 / (decay * rate)), c = 2 * r * Math.Cos(w), rr = r * r;
            double y1 = 0, y2 = 0;
            int end = Math.Min(length, i0 + (int)(decay * 9 * rate) + hit);
            for (int i = i0; i < end; i++)
            {
                int k = i - i0;
                double x = k < hit ? n.Next() * (1 - k / (double)hit) : 0;
                double y = x + c * y1 - rr * y2;
                y2 = y1;
                y1 = y;
                o[i] = (float)y;
            }
            return o;
        }

        // A very short burst of high-passed noise: the hard edge of a hit.
        static float[] Click(int rate, int length, float at, float cut, float burst, Noise n)
        {
            var o = new float[length];
            int i0 = (int)(at * rate), count = Math.Max(2, (int)(burst * rate));
            double a = Math.Exp(-2 * Math.PI * cut / rate), low = 0;
            for (int k = 0; k < count && i0 + k < length; k++)
            {
                double x = n.Next() * Math.Pow(1 - k / (double)count, 2);
                low = (1 - a) * x + a * low;
                o[i0 + k] = (float)(x - low);
            }
            return o;
        }

        // Low-passed noise with an attack and an exponential decay (the body of a soft thud, a room's tail).
        static float[] Breath(int rate, int length, float at, float cut, float attack, float decay, Noise n)
        {
            var o = new float[length];
            int i0 = (int)(at * rate);
            double a = Math.Exp(-2 * Math.PI * cut / rate), l1 = 0, l2 = 0;
            for (int i = i0; i < length; i++)
            {
                double t = (i - i0) / (double)rate;
                double env = (1 - Math.Exp(-t / attack)) * Math.Exp(-t / decay);
                if (t > attack * 8 && env < 1e-5) break;
                l1 = (1 - a) * n.Next() + a * l1;
                l2 = (1 - a) * l1 + a * l2;
                o[i] = (float)(env * l2);
            }
            return o;
        }

        // Band-passed noise over `from`..`from + length`, its centre gliding f0 → f1: air moving. `ease` > 0 shapes it
        // like a thing easing out (loudest at once, (1 - u)^ease after a short attack); `ease` 0 with `arch` swells and
        // fades (sin² over the span).
        static float[] Air(int rate, int length, float from, float span, float f0, float f1, float q, float attack, float ease, Noise n, bool arch = false)
        {
            var o = new float[length];
            int i0 = (int)(from * rate), count = (int)(span * rate);
            double low = 0, band = 0, damp = 1.0 / q;
            for (int k = 0; k < count && i0 + k < length; k++)
            {
                double u = k / (double)count, t = k / (double)rate;
                double fc = f0 * Math.Pow(f1 / f0, u);
                double f = 2 * Math.Sin(Math.PI * Math.Min(fc, rate / 6.0) / rate);
                double x = n.Next();
                low += f * band;
                double high = x - low - damp * band;
                band += f * high;
                double env = arch ? Math.Pow(Math.Sin(Math.PI * u), 2) : (1 - Math.Exp(-t / attack)) * Math.Pow(1 - u, ease);
                o[i0 + k] = (float)(env * band);
            }
            return o;
        }

        // Adds `part`, scaled so its own peak is `gain`.
        static void Add(float[] b, float[] part, float gain)
        {
            float peak = 0f;
            for (int i = 0; i < part.Length; i++) peak = Math.Max(peak, Math.Abs(part[i]));
            if (peak <= 0f) return;
            float k = gain / peak;
            for (int i = 0; i < b.Length && i < part.Length; i++) b[i] += part[i] * k;
        }

        // A few dark early reflections (a wooden room, `room` loud), a gentle limiter, the end faded and the peak at 0.95.
        static float[] Finish(float[] b, int rate, float room)
        {
            int[] delays = { (int)(.011f * rate), (int)(.017f * rate), (int)(.029f * rate), (int)(.043f * rate) };
            float[] gains = { 1f, .75f, .5f, .3f };
            var wet = new float[b.Length];
            double a = Math.Exp(-2 * Math.PI * 2500.0 / rate);
            for (int d = 0; d < delays.Length; d++)
            {
                double low = 0;
                for (int i = delays[d]; i < b.Length; i++)
                {
                    low = (1 - a) * b[i - delays[d]] + a * low;
                    wet[i] += (float)(low * gains[d] * room * (d % 2 == 0 ? 1 : -1));
                }
            }
            float peak = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = (float)Math.Tanh((b[i] + wet[i]) * 1.1);
                peak = Math.Max(peak, Math.Abs(b[i]));
            }
            int fade = Math.Min(b.Length / 4, (int)(.02f * rate));
            for (int i = 0; i < fade; i++) b[b.Length - 1 - i] *= i / (float)fade;
            if (peak > 0f) for (int i = 0; i < b.Length; i++) b[i] *= .95f / peak;
            return b;
        }

        // A fixed-seed noise source, so every PC and every run makes the same sounds.
        sealed class Noise
        {
            uint s;
            public Noise(uint seed) { s = seed == 0 ? 1u : seed * 2654435761u; }
            public double Next()
            {
                s ^= s << 13;
                s ^= s >> 17;
                s ^= s << 5;
                return s / 2147483647.5 - 1.0;
            }
        }
    }
}
