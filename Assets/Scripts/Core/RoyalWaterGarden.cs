using System;

namespace ChessFight.Network
{
    // Pawn Rush early obstacle section 1-1, the royal water garden v31 (2026-11-02 user: the garden is one of the
    // obstacle prefabs like the grand stair hall; "회전 막대 사이즈를 줄이고 여러개로", a very hard shortcut off the
    // platforms, "팀 보트는 가로로 여러 개가 랜덤한 방향으로 움직여서 플레이어가 다음 보트로 넘어가면서", four lanes with
    // the boats well apart, both teams riding the same boats). The parts whose motion is worked out from numbers
    // alone live here, so every PC moves them alike from the round's seed and the shared obstacle clock, and the
    // Core tests check them without Unity:
    //   boats    four lanes across the reservoir, two boats a lane 24 m apart (18 m of water between them). The
    //            section's seed picks each lane's way (east or west, at least one lane each way) and speed (1.6,
    //            2.0 or 2.4 m/s). A boat runs out of sight under the tunnel at either end, sinks there, comes round
    //            under the water and rises in the other tunnel.
    //   geysers  eight jets along the canal walk and its ramp, firing one after another every 0.55 s (the far
    //            end first, a wave running at the pawns), each once every 5 s: 0.8 s of bubbling, then 1 s of jet.
    //   robots   the robot arms' 6 s swing: rest raised on their own side, wind up, sweep across the dyke just
    //            over the floor, hold over the water, swing back across, rest.
    public static class RoyalWaterGardenRules
    {
        // ------------------------------------------------------------------ boats

        public const int Lanes = 4, BoatsPerLane = 2, BoatSalt = 6;
        // Metres along a lane: the loop a boat goes round (x -24 .. 24 in the section), its length, where it is
        // out of sight (under a tunnel roof from |x| 18; it starts sinking past SightEdge and is DiveDepth down by
        // DiveEnd, before it wraps at Loop / 2).
        public const double Loop = 48.0, BoatLength = 6.0, SightEdge = 20.5, DiveEnd = 23.0, DiveDepth = 9.8;
        public static readonly double[] Speeds = { 1.6, 2.0, 2.4 };

        public struct BoatLane
        {
            public int Dir;          // +1 east, -1 west
            public double Speed;     // m/s
            public double Offset;    // metres along the loop at time 0
        }

        public static double Spacing => Loop / BoatsPerLane;
        public static double WaterBetween => Spacing - BoatLength;

        // The lanes for a section seed: the same on every PC. At least one lane runs each way.
        public static BoatLane[] RollBoatLanes(int sectionSeed)
        {
            var rng = new SeededRandom(SeededSchedule.Hash(sectionSeed, BoatSalt));
            var lanes = new BoatLane[Lanes];
            for (int i = 0; i < Lanes; i++)
            {
                lanes[i].Dir = (rng.NextUInt() & 1u) == 0u ? 1 : -1;
                lanes[i].Speed = Speeds[rng.Next(Speeds.Length)];
                lanes[i].Offset = rng.Range(0.0, Spacing);
            }
            bool allSame = true;
            for (int i = 1; i < Lanes; i++) allSame &= lanes[i].Dir == lanes[0].Dir;
            if (allSame) lanes[rng.Next(Lanes)].Dir *= -1;
            return lanes;
        }

        // Where boat `boat` of a lane is along it at `time` on the shared clock: -Loop/2 .. Loop/2 (the middle of
        // the reservoir is 0). The shared clock is Unix time in a match: kept in double all the way.
        public static double BoatX(BoatLane lane, int boat, double time)
        {
            double u = (lane.Offset + boat * Spacing + lane.Dir * lane.Speed * time) % Loop;
            if (u < 0) u += Loop;
            return u - Loop * .5;
        }

        // How far below the deck's height a boat is at x: 0 in sight, sinking smoothly under the tunnels.
        public static double BoatDrop(double x)
        {
            double a = Math.Abs(x);
            if (a <= SightEdge) return 0;
            if (a >= DiveEnd) return DiveDepth;
            double t = (a - SightEdge) / (DiveEnd - SightEdge);
            return DiveDepth * t * t * (3 - 2 * t);
        }

        // ------------------------------------------------------------------ geysers

        public const int Geysers = 8;
        public const double GeyserCycle = 5.0, GeyserStep = .55, GeyserWarn = .8, GeyserJet = 1.0;

        public enum Jet { Rest, Warn, Jet }

        // Geyser `order` (0 fires first) at `time`; `into` = seconds since that state began.
        public static Jet GeyserState(int order, double time, out double into)
        {
            double c = (time - order * GeyserStep) % GeyserCycle;
            if (c < 0) c += GeyserCycle;
            if (c < GeyserWarn) { into = c; return Jet.Warn; }
            if (c < GeyserWarn + GeyserJet) { into = c - GeyserWarn; return Jet.Jet; }
            into = c - GeyserWarn - GeyserJet;
            return Jet.Rest;
        }

        // Which eruption a time falls in (to hit a pawn once per jet).
        public static long GeyserCycleIndex(int order, double time) => (long)Math.Floor((time - order * GeyserStep) / GeyserCycle);

        // ------------------------------------------------------------------ robot arms

        // Degrees from hanging straight down, + towards the robot's own (north) side.
        public const double RobotCycle = 6.0, RobotRest = 60, RobotWind = 82, RobotDown = -75;
        public const double WindUntil = 1.0, SweepUntil = 1.85, HoldUntil = 2.6, BackUntil = 4.4;

        public enum Swing { Rest, Wind, Sweep, Hold, Back }

        public static Swing RobotPhase(double time, out double into)
        {
            double c = time % RobotCycle;
            if (c < 0) c += RobotCycle;
            if (c < WindUntil) { into = c; return Swing.Wind; }
            if (c < SweepUntil) { into = c - WindUntil; return Swing.Sweep; }
            if (c < HoldUntil) { into = c - SweepUntil; return Swing.Hold; }
            if (c < BackUntil) { into = c - HoldUntil; return Swing.Back; }
            into = c - BackUntil;
            return Swing.Rest;
        }

        public static double RobotAngle(double time)
        {
            double into;
            switch (RobotPhase(time, out into))
            {
                case Swing.Wind: return RobotRest + (RobotWind - RobotRest) * Smooth(into / WindUntil);
                case Swing.Sweep:
                    double u = into / (SweepUntil - WindUntil);
                    return RobotWind + (RobotDown - RobotWind) * u * u;
                case Swing.Hold: return RobotDown;
                case Swing.Back: return RobotDown + (RobotRest - RobotDown) * Smooth(into / (BackUntil - HoldUntil));
                default: return RobotRest;
            }
        }

        // Which swing a time falls in (to hit a pawn once per pass).
        public static long RobotCycleIndex(double time) => (long)Math.Floor(time / RobotCycle);

        static double Smooth(double t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }
    }
}
