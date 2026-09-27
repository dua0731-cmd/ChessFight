using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChessFight.Network
{
    // Foundation milestone: irreversible promotion claims and Blue1/Blue2 exits.
    // Mission scoring, throne and winner rules are deliberately not invented here.
    public sealed class KingRushRules
    {
        public const double DoorDelay = 20;
        public readonly struct Claim
        {
            public readonly ulong Player;
            public readonly int Team;
            public Claim(ulong player, int team) { Player = player; Team = team; }
        }
        readonly double[] completed = { -1, -1, -1, -1 };
        readonly Dictionary<int, Claim> claims = new Dictionary<int, Claim>();
        static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        public int ClaimedCount => claims.Count;
        public bool TryGetClaim(int pad, out Claim claim) => claims.TryGetValue(pad, out claim);
        public static int Wave(int pad) => pad < 0 || pad > 8 ? -1 : pad < 2 ? 0 : pad < 5 ? 1 : 2;
        public static KingRushPiece PadPiece(int pad)
        {
            switch (pad)
            {
                case 0: case 4: case 8: return KingRushPiece.Knight;
                case 1: case 3: case 7: return KingRushPiece.Bishop;
                case 2: case 6: return KingRushPiece.Rook;
                case 5: return KingRushPiece.Queen;
                default: return KingRushPiece.Pawn;
            }
        }
        public bool TryClaim(int pad, ulong player, int team)
        {
            if (Wave(pad) < 0 || player == 0 || team < 0 || team > 1 || claims.ContainsKey(pad)) return false;
            foreach (var entry in claims)
                if (Wave(entry.Key) == Wave(pad) && entry.Value.Player == player) return false;
            claims.Add(pad, new Claim(player, team)); return true;
        }
        public bool Complete(int blue, int team, double now)
        {
            if (blue < 0 || blue > 1 || team < 0 || team > 1 || !ValidTime(now)) return false;
            int index = blue * 2 + team;
            if (completed[index] >= 0) return false;
            completed[index] = now; return true;
        }
        public double OpensAt(int blue, int team)
        {
            if (blue < 0 || blue > 1 || team < 0 || team > 1) return double.PositiveInfinity;
            double own = completed[blue * 2 + team], other = completed[blue * 2 + 1 - team];
            return Math.Min(own < 0 ? double.PositiveInfinity : own,
                other < 0 ? double.PositiveInfinity : other + DoorDelay);
        }
        public bool IsOpen(int blue, int team, double now) => ValidTime(now) && now >= OpensAt(blue, team);
        static bool ValidTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 86400;

        // Not wired to Steam yet. Bounded, invariant-culture, atomic, monotonic merge.
        public string Encode()
        {
            var s = new StringBuilder("KR1");
            foreach (double time in completed) s.Append('|').Append(time.ToString("R", Culture));
            for (int i = 0; i < 9; i++) if (claims.TryGetValue(i, out var c))
                s.Append('|').Append(i).Append(',').Append(c.Player).Append(',').Append(c.Team);
            return s.ToString();
        }
        public bool Apply(string data)
        {
            if (string.IsNullOrEmpty(data) || data.Length > 512) return false;
            string[] parts = data.Split('|');
            if (parts.Length < 5 || parts.Length > 14 || parts[0] != "KR1") return false;
            var incoming = new KingRushRules();
            for (int i = 0; i < 4; i++)
            {
                if (!double.TryParse(parts[i + 1], NumberStyles.Float, Culture, out double t) || (t != -1 && !ValidTime(t))) return false;
                incoming.completed[i] = t;
                if (t >= 0 && completed[i] >= 0 && t != completed[i]) return false;
            }
            for (int i = 5; i < parts.Length; i++)
            {
                string[] c = parts[i].Split(',');
                if (c.Length != 3 || !int.TryParse(c[0], NumberStyles.None, Culture, out int pad) ||
                    !ulong.TryParse(c[1], NumberStyles.None, Culture, out ulong id) ||
                    !int.TryParse(c[2], NumberStyles.None, Culture, out int team) || !incoming.TryClaim(pad, id, team)) return false;
            }
            // Validate against local facts too before changing anything.
            var merged = new KingRushRules();
            foreach (var c in claims) merged.TryClaim(c.Key, c.Value.Player, c.Value.Team);
            foreach (var c in incoming.claims)
            {
                if (claims.TryGetValue(c.Key, out var old))
                { if (old.Player != c.Value.Player || old.Team != c.Value.Team) return false; }
                else if (!merged.TryClaim(c.Key, c.Value.Player, c.Value.Team)) return false;
            }
            for (int i = 0; i < 4; i++) if (completed[i] < 0) completed[i] = incoming.completed[i];
            foreach (var c in incoming.claims) claims[c.Key] = c.Value;
            return true;
        }
    }

    // Supply unique eligible pawn IDs, never individual ragdoll colliders.
    public sealed class KingRushPadCharge
    {
        public const double HoldSeconds = 1.5;
        public ulong Candidate { get; private set; }
        public double Seconds { get; private set; }
        public bool Contested { get; private set; }
        public void Reset() { Candidate = 0; Seconds = 0; Contested = false; }
        public ulong Step(IReadOnlyList<ulong> eligible, double dt)
        {
            if (dt <= 0 || double.IsNaN(dt) || double.IsInfinity(dt)) return 0;
            bool present = false;
            for (int i = 0; i < eligible.Count; i++) if (eligible[i] == Candidate) present = true;
            if (!present) { Candidate = 0; Seconds = 0; }
            Contested = eligible.Count > 1;
            if (eligible.Count != 1 || eligible[0] == 0) return 0;
            Candidate = eligible[0]; Seconds = Math.Min(HoldSeconds, Seconds + dt);
            return Seconds + 1e-9 >= HoldSeconds ? Candidate : 0;
        }
    }
}
