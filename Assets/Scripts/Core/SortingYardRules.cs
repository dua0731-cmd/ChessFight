using System;

namespace ChessFight.Network
{
    // The Royal Carriage Yard's sorting yard, R662 (the user: "2구역의 세로길이를 현재의 1.5배 정도 늘려주고 조금 더 재구성",
    // "좌우로 한칸씩 이동하며 플레이어를 방해하는 장애물과 말고도 다른 매커니즘의 장애물이 한두개 정도는 더"): the two new
    // machines' motion, worked out from numbers alone so every PC moves them alike from the shared obstacle clock and the
    // Core tests check them without Unity.
    //   cranes   two treadwheel jib cranes slew low timber beams round their drums: rest, slew half a turn through the
    //            north, rest, slew back. Both jibs always point the same way (parallel), so their sweeps overlap in the
    //            middle of the yard and the beams never meet.
    //   hoists   counterweighted loft hoists in pairs: a pallet sits down (boarded), rises to the loft deck, sits up
    //            there, comes down; its partner does the same half a period later, so one of each pair is always at an
    //            end and one is down whenever the other is up.
    // Map numbers are in the sorting yard's own frame (z north, the arena starting at z 106).
    public static class SortingYardRules
    {
        // ------------------------------------------------------------------ slewing cranes

        public struct Slew
        {
            public double HeadingA, HeadingB;   // Unity yaw, degrees: 0 points along +x, -90 along +z (north)
            public double Rest, Seconds, Ramp;  // s
            public double Phase;                // s added to the clock
            public bool TurnOn;                 // each slew goes on round the same way instead of back
        }

        // How far through a slew (0..1) `t` seconds into it: `ramp` seconds to full turning speed, a steady turn, `ramp`
        // seconds to a stop - smooth at both ends (the load is lifted off and set down, never jerked).
        public static double Profile(double t, double seconds, double ramp)
        {
            double s = Math.Max(1e-3, seconds), r = Math.Max(0.0, Math.Min(ramp, s * .5));
            if (t <= 0.0) return 0.0;
            if (t >= s) return 1.0;
            double v = 1.0 / (s - r);
            if (t < r) return .5 * v / r * t * t;
            if (t < s - r) return .5 * v * r + v * (t - r);
            double left = s - t;
            return 1.0 - .5 * v / r * left * left;
        }

        // The jib's heading at `time` (the slew's phase added), and whether the load is moving then.
        public static double Heading(in Slew s, double time, out bool moving)
        {
            double t = time + s.Phase, swing = s.HeadingB - s.HeadingA;
            double half = Math.Max(.1, s.Rest + s.Seconds);
            if (s.TurnOn)
            {
                double k = Math.Floor(t / half), into = t - k * half;
                int turns = Math.Max(1, (int)Math.Round(360.0 / Math.Max(1.0, Math.Abs(swing))));
                long step = (((long)k % turns) + turns) % turns;
                moving = into >= s.Rest;
                return s.HeadingA + swing * (step + (moving ? Profile(into - s.Rest, s.Seconds, s.Ramp) : 0.0));
            }
            double period = 2.0 * half;
            double u = t - Math.Floor(t / period) * period;
            if (u < s.Rest) { moving = false; return s.HeadingA; }
            u -= s.Rest;
            if (u < s.Seconds) { moving = true; return s.HeadingA + swing * Profile(u, s.Seconds, s.Ramp); }
            u -= s.Seconds;
            if (u < s.Rest) { moving = false; return s.HeadingB; }
            u -= s.Rest;
            moving = true;
            return s.HeadingB - swing * Profile(u, s.Seconds, s.Ramp);
        }

        public static double Heading(in Slew s, double time) => Heading(s, time, out _);

        // Seconds until the load next lifts off a rest (0 while it moves).
        public static double UntilLift(in Slew s, double time)
        {
            double half = Math.Max(.1, s.Rest + s.Seconds), t = time + s.Phase;
            double into = t - Math.Floor(t / half) * half;
            return into < s.Rest ? s.Rest - into : 0.0;
        }

        // The direction a heading points (x, z).
        public static void Along(double heading, out double x, out double z)
        {
            double a = heading * Math.PI / 180.0;
            x = Math.Cos(a);
            z = -Math.Sin(a);
        }

        // The sorting yard's crane field (R662): two cranes on drums at (+-CraneX, CraneZ), timber beams from CraneInner to
        // CraneOuter, both jibs parallel - rest pointing west, slew through the north to rest pointing east, and back.
        public const double CraneX = 9.5, CraneZ = 123.0, CraneDrum = 1.2, CraneInner = 2.0, CraneOuter = 10.0, CraneHalfWidth = .21;
        public const double CraneRest = 1.4, CraneSlew = 6.0, CraneRamp = 1.0;
        // The timber stores along both walls beside the field, from x +-StoreFront to the stalls, StoreZ0..StoreZ1.
        public const double StoreFront = 20.11, StoreZ0 = 121.8, StoreZ1 = 137.4;

        public static Slew YardCrane(double rest = CraneRest, double seconds = CraneSlew, double ramp = CraneRamp) =>
            new Slew { HeadingA = -180.0, HeadingB = 0.0, Rest = rest, Seconds = seconds, Ramp = ramp };

        // ------------------------------------------------------------------ loft hoists

        public struct Hoist
        {
            public double Down, Move;   // s sitting at each end, s moving between them
            public double Low, High;    // the plate's top down (walked onto) and up (flush with the loft deck), m
            public double Phase;        // s added to the clock
        }

        // The sorting yard's loft hoists (R662): two mirrored pairs in front of the loft, each pair an inner and an outer
        // pallet whose partners run half a period apart.
        public const double HoistDown = 2.2, HoistMove = 1.5, HoistLow = .22, HoistHigh = 2.40;
        public const double HoistZ0 = 134.97, HoistZ1 = 137.37, LoftZ0 = 137.4, LoftZ1 = 140.6;
        public static readonly double[] HoistInner = { .9, 3.27 }, HoistOuter = { 3.73, 6.13 };

        public static Hoist YardHoist(bool partner, double down = HoistDown, double move = HoistMove) =>
            new Hoist { Down = down, Move = move, Low = HoistLow, High = HoistHigh, Phase = partner ? down + move : 0.0 };

        public static double Period(in Hoist h) => 2.0 * (h.Down + h.Move);

        static double Smooth(double u)
        {
            u = Math.Max(0.0, Math.Min(1.0, u));
            return u * u * (3.0 - 2.0 * u);
        }

        // Where in its cycle a pallet is at `time` (0 .. Period).
        static double Cycle(in Hoist h, double time)
        {
            double p = Math.Max(.1, Period(h)), t = time + h.Phase;
            return t - Math.Floor(t / p) * p;
        }

        // The plate's top over the floor at `time`.
        public static double Top(in Hoist h, double time)
        {
            double u = Cycle(h, time), rise = h.High - h.Low;
            if (u < h.Down) return h.Low;
            u -= h.Down;
            if (u < h.Move) return h.Low + rise * Smooth(u / h.Move);
            u -= h.Move;
            if (u < h.Down) return h.High;
            u -= h.Down;
            return h.High - rise * Smooth(u / h.Move);
        }

        // Seconds the pallet still sits down from `time` (0 if it is not down then).
        public static double BoardingFor(in Hoist h, double time)
        {
            double u = Cycle(h, time);
            return u < h.Down ? h.Down - u : 0.0;
        }

        // Seconds until the pallet next sits down (0 while it is down).
        public static double NextBoarding(in Hoist h, double time)
        {
            double u = Cycle(h, time);
            return u < h.Down ? 0.0 : Period(h) - u;
        }

        // Seconds until the pallet next starts down from the deck (0 while it is coming down).
        public static double ComesDownIn(in Hoist h, double time)
        {
            double u = Cycle(h, time), start = 2.0 * h.Down + h.Move;
            if (u >= start) return 0.0;
            return start - u;
        }

        public static bool IsDown(in Hoist h, double time) => Cycle(h, time) < h.Down;
        public static bool IsUp(in Hoist h, double time) { double u = Cycle(h, time); return u >= h.Down + h.Move && u < 2.0 * h.Down + h.Move; }
    }
}
