using System;

namespace ChessFight.Network
{
    // The Royal Library's giant chess (pr/2 GiantChessCourse, a Pawn Rush round section since seoungtest R689): two giant
    // grandmasters replay one of the candidates' games (Resources/GiantChessGames.json) move by move, and every move is a
    // timed hazard - the piece flies (Move), lands and lights the squares it attacks (Warn), the lit squares strike
    // (Strike), a short pause (Rest) and the next move. 승규 11-18: the game starts when the first pawn reaches the chess
    // room, and when it ends the next game follows ("첫 도착 때 시작, 끝나면 다음 대국").
    //
    // Everything here is a pure function of the seconds since the chess started, so every PC of a match draws the same
    // game, the same move and the same phase from the section's seed and one start time the host sends - a late joiner or
    // a new host lands on the same move at once. Times are replay seconds (real seconds times the replay speed).
    public static class GiantChessReplay
    {
        public const int Rest = 0, Move = 1, Warn = 2, Strike = 3, End = 4;

        // How long each phase of a move lasts, and the pause after a game's last move before the board is set up again.
        public struct Timing
        {
            public double Rest, Move, Warn, Strike, End;

            public Timing(double rest, double move, double warn, double strike, double end)
            {
                Rest = Math.Max(.01, rest);
                Move = Math.Max(.01, move);
                Warn = Math.Max(.01, warn);
                Strike = Math.Max(.01, strike);
                End = Math.Max(.01, end);
            }

            public double Ply => Rest + Move + Warn + Strike;
            public double Game(int plies) => Math.Max(0, plies) * Ply + End;

            public double Length(int phase) =>
                phase == Rest ? Rest : phase == Move ? Move : phase == Warn ? Warn : phase == Strike ? Strike : End;
        }

        // Where the replay is: the game's turn in the series (cycle), which game of the library that is, the move (ply,
        // = the game's move count during End), the phase and how far into it.
        public struct Spot
        {
            public int Cycle, Game, Ply, Phase;
            public double T;

            public bool Before(Spot other) =>
                Cycle != other.Cycle ? Cycle < other.Cycle : Ply != other.Ply ? Ply < other.Ply : Phase < other.Phase;
        }

        // The game for a turn of the series: the seed's own order through the library, never the same game twice in a row
        // (while there are two). Turn 0 is the section's first game.
        public static int Game(int seed, int cycle, int count)
        {
            if (count <= 0) return -1;
            int game = (int)(SeededSchedule.Hash(seed, 0x6C3E) % (uint)count);
            for (int k = 1; k <= cycle && count > 1; k++)
            {
                int r = (int)(SeededSchedule.Hash(seed, 0x6C3E + k) % (uint)(count - 1));
                game = r >= game ? r + 1 : r;
            }
            return game;
        }

        // The spot `seconds` replay seconds after the chess started (negative: not yet - the first game's first rest).
        // plies(game) is the length of a game of the library.
        public static Spot Locate(double seconds, int seed, int count, Func<int, int> plies, Timing timing, int maxCycles = 100000)
        {
            var spot = new Spot { Cycle = 0, Game = Game(seed, 0, count), Ply = 0, Phase = Rest, T = 0 };
            if (count <= 0 || plies == null || double.IsNaN(seconds) || seconds <= 0) return spot;
            double left = seconds;
            int game = spot.Game;
            for (int cycle = 0; cycle < maxCycles; cycle++)
            {
                if (cycle > 0) game = Next(seed, cycle, count, game);
                int n = Math.Max(0, plies(game));
                double length = timing.Game(n);
                if (left >= length) { left -= length; continue; }
                spot.Cycle = cycle;
                spot.Game = game;
                double moves = n * timing.Ply;
                if (left >= moves)
                {
                    spot.Ply = n;
                    spot.Phase = End;
                    spot.T = left - moves;
                    return spot;
                }
                spot.Ply = (int)Math.Floor(left / timing.Ply);
                if (spot.Ply >= n) spot.Ply = n - 1;
                double t = left - spot.Ply * timing.Ply;
                if (t < timing.Rest) { spot.Phase = Rest; spot.T = t; }
                else if ((t -= timing.Rest) < timing.Move) { spot.Phase = Move; spot.T = t; }
                else if ((t -= timing.Move) < timing.Warn) { spot.Phase = Warn; spot.T = t; }
                else { spot.Phase = Strike; spot.T = Math.Min(t - timing.Warn, timing.Strike); }
                return spot;
            }
            spot.Cycle = maxCycles;
            return spot;
        }

        // The seconds from the chess's start to the start of a spot's phase (for skipping ahead offline).
        public static double PhaseStart(Spot spot, int seed, int count, Func<int, int> plies, Timing timing)
        {
            double at = 0;
            int game = Game(seed, 0, count);
            for (int cycle = 0; cycle < spot.Cycle; cycle++)
            {
                at += timing.Game(Math.Max(0, plies(game)));
                game = Next(seed, cycle + 1, count, game);
            }
            at += Math.Max(0, spot.Ply) * timing.Ply;
            if (spot.Phase == End) return at;
            if (spot.Phase > Rest) at += timing.Rest;
            if (spot.Phase > Move) at += timing.Move;
            if (spot.Phase > Warn) at += timing.Warn;
            return at;
        }

        static int Next(int seed, int cycle, int count, int previous)
        {
            if (count <= 1) return 0;
            int r = (int)(SeededSchedule.Hash(seed, 0x6C3E + cycle) % (uint)(count - 1));
            return r >= previous ? r + 1 : r;
        }

        // The host's start time on the wire: tenths of a second after the round's start, 0 = not started. The host keeps the
        // same rounded time it sends, so every PC counts from exactly the same moment.
        public const double Step = .1;

        public static int Encode(double start, double origin)
        {
            if (double.IsNaN(start) || double.IsNaN(origin)) return 0;
            return (int)Math.Max(1, Math.Min(65535, Math.Round((start - origin) / Step) + 1));
        }

        public static double Decode(int value, double origin) => value <= 0 || double.IsNaN(origin) ? double.NaN : origin + (value - 1) * Step;

        public static double Round(double start, double origin) => Decode(Encode(start, origin), origin);
    }
}
