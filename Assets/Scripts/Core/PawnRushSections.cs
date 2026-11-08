using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // A Pawn Rush round is seven sections in a fixed order (2026-11-02 user: "장애물 구간 3개와 미니게임
    // 구간 3개, 마지막 구간 1개 총 7개의 구간이 한 판을 이룰거야 ... 해당 구간에서 나올 수 있는 프리팹 목록
    // 중 랜덤으로 한 가지 프리팹을 뽑아"): obstacle, mini-game, obstacle, mini-game, obstacle, mini-game,
    // final. Each slot draws one prefab from its own list. The draw is a seeded pure function so the host
    // draws once and every PC can check it. No Unity: tested by the Core tests.
    public static class PawnRushSections
    {
        public const int SlotCount = 7;
        public const int KindObstacle = 0, KindMiniGame = 1, KindFinal = 2;

        public static readonly int[] Kind =
        {
            KindObstacle, KindMiniGame, KindObstacle, KindMiniGame, KindObstacle, KindMiniGame, KindFinal,
        };

        // The obstacle slots follow the palace's three phases (concept art v2: 바깥뜰 1-x, 아랫궁 2-x, 윗궁 3-x).
        public static readonly string[] SlotNames =
        {
            "장애물 1 (초반 · 바깥뜰)", "미니게임 1", "장애물 2 (중반 · 아랫궁)", "미니게임 2",
            "장애물 3 (후반 · 윗궁)", "미니게임 3", "마지막 구간",
        };

        // One pick per slot. options[slot] lists the ids the slot may take (one id per prefab; the caller
        // numbers them). An id offered to several slots (the three mini-game slots share their games) comes
        // up at most once a round while the slot has another; the slot's pick of the previous round
        // (last[slot]) is left out while it has another too. A slot with no options gets -1.
        public static int[] Choose(int seed, int[][] options, int[] last = null)
        {
            var picks = new int[options == null ? 0 : options.Length];
            var rng = new SeededRandom(SeededSchedule.Hash(seed, 0x5EC7));
            var used = new List<int>();
            var pool = new List<int>();
            for (int slot = 0; slot < picks.Length; slot++)
            {
                var list = options[slot];
                picks[slot] = -1;
                if (list == null || list.Length == 0) continue;
                int previous = last != null && slot < last.Length ? last[slot] : -1;
                Fill(pool, list, used, previous, true);
                if (pool.Count == 0) Fill(pool, list, used, previous, false);
                if (pool.Count == 0) pool.AddRange(list);
                int pick = pool[rng.Next(pool.Count)];
                picks[slot] = pick;
                used.Add(pick);
            }
            return picks;
        }

        static void Fill(List<int> pool, int[] list, List<int> used, int previous, bool avoidPrevious)
        {
            pool.Clear();
            foreach (int id in list)
                if (!used.Contains(id) && (!avoidPrevious || id != previous) && !pool.Contains(id)) pool.Add(id);
        }

        // A section's own seed: the round's seed mixed with the slot number. Its obstacles mix in their own
        // number again (SeededSchedule.Hash), so no two streams in a round are the same.
        public static int SectionSeed(int roundSeed, int slot) => unchecked((int)SeededSchedule.Hash(roundSeed, slot + 1));

        // R508: a match's round seed, the same on every PC without a packet - from the match's number, which every
        // member of the match room holds.
        public static int MatchSeed(ulong match) => unchecked((int)SeededSchedule.Hash(unchecked((int)(match ^ (match >> 32))), 0x4D41));
    }

    // Grand stair hall team-size rules (level design v0.3 §6): with three or four a side only one wing opens
    // (the round's seed picks which), so each wing has five to eight pawns whatever the team size; the lift
    // landing has one bundle then.
    public static class GrandStairHallRules
    {
        public const int BothWingsFrom = 5;
        public const int West = 0, East = 1;

        public static bool BothWings(int teamSize) => teamSize >= BothWingsFrom;

        // The wing that opens when only one does.
        public static int OpenWing(int sectionSeed) => (int)(SeededSchedule.Hash(sectionSeed, 3) & 1u);

        public static bool WingOpen(int wing, int teamSize, int sectionSeed) => BothWings(teamSize) || OpenWing(sectionSeed) == wing;
    }
}
