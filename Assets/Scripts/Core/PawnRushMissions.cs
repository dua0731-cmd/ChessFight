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
    //
    // Mini-games v1.0 (R126): ten games (A..E kept, F..J new), each in a group of games that feel the same in
    // the hands (§2.5). The two games of a round are never of the same group. Priority when the pool is short:
    // two different games, then different groups, then the previous round's left out.
    public static class PawnRushMissions
    {
        public const int Count = 10;
        public const int None = -1;
        public const int Paint = 0, Planks = 1, Drawbridge = 2, Capstan = 3, Bells = 4,
                         EnPassant = 5, KingEscort = 6, Checkmate = 7, CheckBlock = 8, KnightPads = 9;

        // Stable keys: saved and sent, never renamed.
        public static readonly string[] Ids =
        {
            "A_Paint", "B_Planks", "C_Drawbridge", "D_Capstan", "E_Bells",
            "F_EnPassant", "G_KingEscort", "H_Checkmate", "I_CheckBlock", "J_KnightPads",
        };
        public static readonly string[] Names =
        {
            "A 체스판 칠하기", "B 판자 다리 놓기", "C 도개교", "D 캡스턴 성문", "E 종탑 여섯 개",
            "F 앙파상 사냥", "G 킹 호위", "H 체크메이트 몰이", "I 체크 막기", "J 나이트의 길",
        };
        // The picture card's one line (mini-game spec: never more than one line).
        public static readonly string[] Hints =
        {
            "우리 색 칸 위에 1.5초 서 있으면 칠해진다. 우리 색 32칸을 모두 칠하라",
            "판자 더미에서 판자를 들고(1초) 다리 끝에 놓아라(1.5초). 10장이면 건넌다",
            "밧줄 매듭을 잡고(우클릭) 뒤로 걸어 7 m를 당겨라. 질주하면 더 세다",
            "캡스턴 막대를 화살표 방향으로 밀어 돌리면 쇠살문이 올라간다. 8바퀴",
            "탑 여섯 개를 올라 꼭대기 종을 모두 울려라",
            "두 칸 전진한 폰이 빛나는 1.2초 안에 다이빙으로 들이받아 잡아라. 16개",
            "밧줄을 잡고(우클릭) 킹의 썰매를 끌어 문까지. 길을 막은 룩은 옆 차고로 끌어 넣어라",
            "우리 폰 둘레 3×3칸이 공격 칸. 킹을 몰아 체크 4번이나 체크메이트로 굳히고 다이빙으로 잡아라",
            "방패 수레를 밀어 공격선을 막고, 나이트는 끌어내리고, 폰은 들이받아 모든 체크를 풀어라",
            "나이트 길에만 바닥이 있다: 점프대에서 Space로 높이 뛰고 다이빙으로 건너가, 길 위 상대 기물 6개를 다이빙으로 잡아라",
        };

        // §2.5 groups: pulling, painting, carrying, climbing, diving, chasing, flying.
        public const int GroupPull = 0, GroupPaint = 1, GroupCarry = 2, GroupClimb = 3, GroupDive = 4, GroupChase = 5, GroupFly = 6;
        public static readonly int[] Group =
        {
            GroupPaint, GroupCarry, GroupPull, GroupPull, GroupClimb,
            GroupDive, GroupPull, GroupChase, GroupPull, GroupFly,
        };

        // A plaza where A is drawn becomes one arena over both stations (§3.1).
        public static bool Shared(int game) => game == Paint;

        // At most this many of a team count toward a game at once (v0.4 §5: a team of six would
        // otherwise finish in 20 s).
        public const int WorkerCap = 4;

        public struct Draw
        {
            public int First, Second;   // plaza 1, plaza 2
        }

        // Two different games of different groups. Seeded: every PC that gets the seed draws the same two
        // (the host draws once and sends the result anyway). `enabled` may switch games off (null = all on).
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
            // Strictest first: not taken, not of taken's group, not in the previous round. Then drop the
            // previous-round rule, then the group rule.
            for (int level = 0; level < 3; level++)
            {
                var fresh = new List<int>();
                foreach (int g in pool)
                {
                    if (g == taken) continue;
                    if (level < 2 && taken != None && Group[g] == Group[taken]) continue;
                    if (level < 1 && (g == lastFirst || g == lastSecond)) continue;
                    fresh.Add(g);
                }
                if (fresh.Count > 0) return fresh[rng.Next(fresh.Count)];
            }
            throw new ArgumentException("Pawn Rush needs at least two mini-games enabled.");
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
