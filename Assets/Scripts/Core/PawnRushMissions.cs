using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // The Pawn Rush mission draw and the mini-games' shared counting rules (course 01 level
    // design v0.4 §5, mini-game spec v0.1/v0.2). No Unity: the host runs it, the tests run it.
    //
    // Every round two of the five games are drawn, one per mission plaza; both teams get the same
    // game in a plaza. The two are different, and a game played in the previous round of the same
    // room is left out while there are enough others (the shuffle bag).
    public static class PawnRushMissions
    {
        public const int Count = 5;
        public const int None = -1;
        public const int Paint = 0, Planks = 1, Drawbridge = 2, Capstan = 3, Bells = 4;

        // Stable keys: saved and sent, never renamed.
        public static readonly string[] Ids = { "A_Paint", "B_Planks", "C_Drawbridge", "D_Capstan", "E_Bells" };
        public static readonly string[] Names = { "A 체스판 칠하기", "B 판자 다리 놓기", "C 도개교 줄당기기", "D 캡스턴 성문", "E 종탑 여섯 개" };
        // The picture card's one line (mini-game spec: never more than one line).
        public static readonly string[] Hints =
        {
            "칸 위에 1.5초 서 있으면 금색으로 칠해진다. 64칸을 모두 칠하라",
            "판자 더미에서 판자를 들고(1초) 다리 끝에 놓아라(1.5초). 10장이면 건넌다",
            "줄 길(가운데)에서 해자 반대쪽으로 걸으면 도개교가 내려온다",
            "캡스턴 둘레를 화살표 방향으로 돌면 쇠살문이 올라간다. 16바퀴",
            "탑 여섯 개를 올라 꼭대기 종을 모두 울려라",
        };

        // At most this many of a team count toward a game at once (v0.4 §5: a team of six would
        // otherwise finish in 20 s).
        public const int WorkerCap = 4;

        public struct Draw
        {
            public int First, Second;   // plaza 1, plaza 2
        }

        // Two different games. Seeded: every PC that gets the seed draws the same two (the host
        // draws once and sends the result anyway). `enabled` may switch games off (null = all on).
        public static Draw Choose(int seed, int lastFirst = None, int lastSecond = None, bool[] enabled = null)
        {
            var rng = new Random(seed);
            var pool = new List<int>();
            for (int i = 0; i < Count; i++)
                if (enabled == null || (i < enabled.Length && enabled[i])) pool.Add(i);
            if (pool.Count < 2) throw new ArgumentException("Pawn Rush needs at least two mini-games enabled.");
            int first = Pick(rng, pool, lastFirst, lastSecond, None);
            int second = Pick(rng, pool, lastFirst, lastSecond, first);
            return new Draw { First = first, Second = second };
        }

        static int Pick(Random rng, List<int> pool, int lastFirst, int lastSecond, int taken)
        {
            var fresh = new List<int>();
            foreach (int g in pool)
                if (g != taken && g != lastFirst && g != lastSecond) fresh.Add(g);
            if (fresh.Count == 0)   // too few games left: only the "two different" rule stays
                foreach (int g in pool)
                    if (g != taken) fresh.Add(g);
            return fresh[rng.Next(fresh.Count)];
        }

        public static int Workers(int count) => Math.Max(0, Math.Min(count, WorkerCap));

        // Progress that the other team can push back (C's rope, D's capstan) never falls below the
        // last latch it has passed: one every quarter.
        public static double Latched(double before, double after, double step = .25)
        {
            if (double.IsNaN(after)) return before;
            if (after >= before) return Math.Min(1.0, after);
            double floor = Math.Floor(before / step + 1e-9) * step;
            return Math.Max(floor, Math.Max(0.0, after));
        }
    }
}
