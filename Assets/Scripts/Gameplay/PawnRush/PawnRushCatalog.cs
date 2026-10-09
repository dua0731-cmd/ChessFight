using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay.PawnRush
{
    [CreateAssetMenu(menuName = "ChessFight/Pawn Rush/구역 목록", fileName = "PawnRushCatalog")]
    public sealed class PawnRushCatalog : ScriptableObject
    {
        public PawnRushSection[] obstacles = new PawnRushSection[0];
        public PawnRushSection[] missions = new PawnRushSection[0];

        public void ValidateCatalog()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            ValidatePool(obstacles, SectionKind.Obstacle, ids);
            ValidatePool(missions, SectionKind.Mission, ids);
        }
        static void ValidatePool(PawnRushSection[] pool, SectionKind kind, HashSet<string> ids)
        {
            if (pool == null || pool.Length < 3) throw new InvalidOperationException(kind + ": 서로 다른 구역 프리팹을 3개 이상 등록하세요.");
            foreach (var section in pool)
            {
                if (section == null || section.kind != kind) throw new InvalidOperationException(kind + ": 빈 항목 또는 다른 유형의 구역이 있습니다.");
                section.ValidateDefinition();
                if (!ids.Add(section.sectionId)) throw new InvalidOperationException("중복 Section ID: " + section.sectionId);
            }
        }

        // Reserve fixed slots before drawing, so random slots cannot consume a pinned section.
        // Sorting IDs makes a seed independent of the Inspector list order.
        public PawnRushSection[] Choose(int seed, PawnRushSection[] fixedSlots = null)
        {
            ValidateCatalog();
            if (fixedSlots != null && fixedSlots.Length != 6) throw new InvalidOperationException("고정 슬롯은 A1~C2의 6개입니다.");
            var result = new PawnRushSection[6];
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 6; i++)
            {
                var selected = fixedSlots == null ? null : fixedSlots[i];
                if (selected == null) continue;
                var pool = i % 2 == 0 ? obstacles : missions;
                if (Array.IndexOf(pool, selected) < 0 || !used.Add(selected.sectionId))
                    throw new InvalidOperationException(PawnRushCourse.SlotName(i) + ": 목록 밖/유형 불일치/중복 프리팹입니다.");
                result[i] = selected;
            }
            var random = new System.Random(seed);
            for (int type = 0; type < 2; type++)
            {
                var available = new List<PawnRushSection>();
                foreach (var s in type == 0 ? obstacles : missions) if (!used.Contains(s.sectionId)) available.Add(s);
                available.Sort((a, b) => StringComparer.Ordinal.Compare(a.sectionId, b.sectionId));
                for (int n = available.Count - 1; n > 0; n--)
                { int j = random.Next(n + 1); var swap = available[n]; available[n] = available[j]; available[j] = swap; }
                int next = 0;
                for (int i = type; i < 6; i += 2) if (result[i] == null) result[i] = available[next++];
            }
            return result;
        }
    }
}
