using System;
using System.Collections.Generic;
using System.Text;

namespace ChessFight.Network
{
    // R548 (11-11, 승규 님: "방장이 게임 시작을 누른 후 모두 보이는 화면 ... 이 번에는 어떤 맵이지? 라고 기대감을 가질 수
    // 있게"): the round's sections by name, known before the match scene and its section set have loaded, so the entrance
    // screen can reveal them while the scene loads. A text table (Resources/Loading/PawnRushCourse.txt, written from
    // Assets/Settings/PawnRushSections.asset by PawnRushCourseTableWriter in the editor), one line for each prefab a slot
    // may draw: "slot<TAB>key<TAB>name" ('#' starts a comment). The keys are numbered in the order they first appear, slot
    // after slot, as PawnRushSectionSet.Options numbers its prefabs, and the draw is PawnRushSections.Choose itself: the
    // same seed gives the same sections as the round (PawnRushRound checks it as it builds).
    public sealed class PawnRushCourseTable
    {
        public struct Row
        {
            public int Slot;
            public string Key, Name;
            public Row(int slot, string key, string name) { Slot = slot; Key = key; Name = name; }
        }

        readonly List<string>[] keys = new List<string>[PawnRushSections.SlotCount];
        readonly List<string>[] names = new List<string>[PawnRushSections.SlotCount];

        public PawnRushCourseTable()
        {
            for (int s = 0; s < PawnRushSections.SlotCount; s++)
            {
                keys[s] = new List<string>();
                names[s] = new List<string>();
            }
        }

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var list in keys) n += list.Count;
                return n;
            }
        }

        // The names a slot may draw, in the table's order.
        public IReadOnlyList<string> Names(int slot) => slot >= 0 && slot < names.Length ? names[slot] : (IReadOnlyList<string>)Array.Empty<string>();

        // R551: the keys (section prefabs) a slot may draw, in the same order as Names: the course reveal finds each
        // section's picture or emblem by its key.
        public IReadOnlyList<string> Keys(int slot) => slot >= 0 && slot < keys.Length ? keys[slot] : (IReadOnlyList<string>)Array.Empty<string>();

        // A line the table did not take (a slot out of range, a missing key) is left out; a key listed twice in a slot
        // counts once there, as a prefab listed twice does.
        public void Add(int slot, string key, string name)
        {
            if (slot < 0 || slot >= keys.Length || string.IsNullOrEmpty(key) || keys[slot].Contains(key)) return;
            keys[slot].Add(key);
            names[slot].Add(string.IsNullOrEmpty(name) ? key : name);
        }

        public static PawnRushCourseTable Parse(string text)
        {
            var table = new PawnRushCourseTable();
            if (string.IsNullOrEmpty(text)) return table;
            foreach (var raw in text.TrimStart('﻿').Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split('\t');
                if (parts.Length < 2 || !int.TryParse(parts[0].Trim(), out int slot)) continue;
                table.Add(slot, parts[1].Trim(), parts.Length > 2 ? parts[2].Trim() : "");
            }
            return table;
        }

        // The ids each slot may take (distinct keys numbered by first appearance across the slots), for PawnRushSections.Choose.
        public int[][] Options(List<string> distinct)
        {
            distinct.Clear();
            var options = new int[keys.Length][];
            for (int s = 0; s < keys.Length; s++)
            {
                var ids = new int[keys[s].Count];
                for (int k = 0; k < ids.Length; k++)
                {
                    int id = distinct.IndexOf(keys[s][k]);
                    if (id < 0)
                    {
                        id = distinct.Count;
                        distinct.Add(keys[s][k]);
                    }
                    ids[k] = id;
                }
                options[s] = ids;
            }
            return options;
        }

        // The round's pick for every slot as an index into Names(slot), -1 for a slot with nothing to draw.
        public int[] Picks(int seed)
        {
            var distinct = new List<string>();
            var ids = PawnRushSections.Choose(seed, Options(distinct));
            var picks = new int[keys.Length];
            for (int s = 0; s < picks.Length; s++)
                picks[s] = ids[s] >= 0 ? keys[s].IndexOf(distinct[ids[s]]) : -1;
            return picks;
        }

        // The round's section names, slot by slot ("" for a slot with nothing to draw).
        public string[] PickNames(int seed)
        {
            var picks = Picks(seed);
            var shown = new string[picks.Length];
            for (int s = 0; s < shown.Length; s++) shown[s] = picks[s] >= 0 ? names[s][picks[s]] : "";
            return shown;
        }

        // The table's text, as the editor writes it.
        public static string Format(IEnumerable<Row> rows, string header)
        {
            var text = new StringBuilder();
            if (!string.IsNullOrEmpty(header))
                foreach (var line in header.Split('\n')) text.Append("# ").Append(line.TrimEnd('\r')).Append('\n');
            foreach (var row in rows)
                text.Append(row.Slot).Append('\t').Append(Clean(row.Key)).Append('\t').Append(Clean(row.Name)).Append('\n');
            return text.ToString();
        }

        static string Clean(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ').Trim();
    }
}
