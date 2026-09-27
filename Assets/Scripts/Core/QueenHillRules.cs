using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChessFight.Network
{
    // The pure rules of Queen of the Hill's pioneer bells and checkpoints
    // (Docs/GameModes/QueenOfTheHill/DESIGN.md §3.3 B and C), with no Unity in them
    // so they are tested on their own (Tests/Network/NetworkCoreTests.cs).
    //
    // Sections are numbered from 1 (S1) up; 0 is the start (the team's bridge).
    // - The first team to ring a section's bell opens it: its fast path (the light
    //   pillar's cell, a ladder, a bridge...) appears. For the first
    //   ExclusiveSeconds only that team may use it, then anyone.
    // - A character's own checkpoint is the highest section it has reached; its
    //   team's is one below the highest section the team opened. It comes back at
    //   the higher of the two (0 = back to the bridge).
    //
    // Only the machine that simulates the match judges; the others apply what it
    // sends (Encode / Apply). Times are the shared obstacle clock's seconds, so an
    // opening happens at the same moment on every PC.
    public sealed class QueenHillRules
    {
        public const double DefaultExclusiveSeconds = 20;
        public const int White = 0, Black = 1;

        readonly int[] pioneer;
        readonly double[] openedAt;
        readonly Dictionary<ulong, int> reached = new Dictionary<ulong, int>();

        public QueenHillRules(int sections, double exclusiveSeconds = DefaultExclusiveSeconds)
        {
            if (sections < 1) throw new ArgumentOutOfRangeException(nameof(sections));
            Sections = sections;
            ExclusiveSeconds = Math.Max(0, exclusiveSeconds);
            pioneer = new int[sections + 1];
            openedAt = new double[sections + 1];
            Reset();
        }

        public int Sections { get; }
        public double ExclusiveSeconds { get; }

        // Closes every section and forgets every checkpoint (a new round).
        public void Reset()
        {
            for (int i = 0; i <= Sections; i++)
            {
                pioneer[i] = -1;
                openedAt[i] = double.NaN;
            }
            reached.Clear();
        }

        bool Valid(int section) => section >= 1 && section <= Sections;
        static bool ValidTeam(int team) => team == White || team == Black;

        // A bell rung by a character of `team`. True when this opened the section:
        // false when it was open already (only the first ring counts), or on bad input.
        public bool TryOpen(int section, int team, double now)
        {
            if (!Valid(section) || !ValidTeam(team) || IsOpen(section) || double.IsNaN(now) || double.IsInfinity(now)) return false;
            pioneer[section] = team;
            openedAt[section] = now;
            return true;
        }

        public bool IsOpen(int section) => Valid(section) && pioneer[section] >= 0;
        public int Pioneer(int section) => Valid(section) ? pioneer[section] : -1;
        public double OpenedAt(int section) => Valid(section) ? openedAt[section] : double.NaN;

        // Still the pioneer team's alone.
        public bool Exclusive(int section, double now) => IsOpen(section) && now < openedAt[section] + ExclusiveSeconds;

        public bool CanUse(int section, int team, double now) =>
            IsOpen(section) && (team == pioneer[section] || !Exclusive(section, now));

        // A character stood on a section's landing: its own checkpoint.
        public void Reach(ulong character, int section)
        {
            if (!Valid(section)) return;
            if (!reached.TryGetValue(character, out int best) || section > best) reached[character] = section;
        }

        public int PersonalBest(ulong character) => reached.TryGetValue(character, out int best) ? best : 0;

        // The highest section this team opened, 0 for none.
        public int TeamBest(int team)
        {
            for (int s = Sections; s >= 1; s--)
                if (pioneer[s] == team && ValidTeam(team)) return s;
            return 0;
        }

        // Where a character comes back after the water: the higher of its own
        // checkpoint and one below its team's best. 0 = its team's bridge.
        public int RespawnSection(ulong character, int team) =>
            Math.Max(PersonalBest(character), Math.Max(0, TeamBest(team) - 1));

        // The opened sections for the wire: "section:team:openedAt;..." (openedAt in
        // milliseconds of the shared clock). Checkpoints stay on the machine that judges.
        public string Encode()
        {
            var text = new StringBuilder();
            for (int s = 1; s <= Sections; s++)
            {
                if (pioneer[s] < 0) continue;
                if (text.Length > 0) text.Append(';');
                text.Append(s.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(pioneer[s].ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(Math.Round(openedAt[s] * 1000.0).ToString("0", CultureInfo.InvariantCulture));
            }
            return text.ToString();
        }

        // What the judging machine sent. Only ever opens (an opening is final for
        // the round); anything malformed is dropped whole, before anything changes.
        // True when something new opened.
        public bool Apply(string encoded)
        {
            if (encoded == null || encoded.Length > 512) return false;
            var parsed = new List<(int section, int team, double at)>();
            if (encoded.Length > 0)
            {
                foreach (string part in encoded.Split(';'))
                {
                    string[] f = part.Split(':');
                    if (f.Length != 3
                        || !int.TryParse(f[0], NumberStyles.None, CultureInfo.InvariantCulture, out int section)
                        || !int.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out int team)
                        || !long.TryParse(f[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long ms)
                        || !Valid(section) || !ValidTeam(team)) return false;
                    parsed.Add((section, team, ms / 1000.0));
                }
            }
            bool changed = false;
            // Tuple deconstruction in foreach is not supported by mcs (Core stays mcs-compatible).
            foreach (var open in parsed)
                changed |= TryOpen(open.Item1, open.Item2, open.Item3);
            return changed;
        }
    }
}
