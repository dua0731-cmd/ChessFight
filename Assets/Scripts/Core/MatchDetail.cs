using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ChessFight.Network
{
    // R742 (user 11-21: "전적에서 그 판을 더블 클릭 하면 내가 그 판에서 했던 상세 내역" → mock-up A): what one match held for
    // this player, kept with its record (MatchRecord.Detail) and shown when its line in the 전적 tab is double-clicked.
    // Written as the match goes (the Game side's MatchRecords.Log, from the modes' own events) and completed at the end
    // (the players' line-up and results, the match's length). No distances: the user asked for none ("거리 몇 미터 이런건 빼").
    //   Pawn Rush    the sections in the order this player entered them (when, falls in each), promotions, the falls,
    //                checkpoints, skill uses, dives and grabs, and every player's place and crossing time
    //   Sword Fight  every fall of the match (when, which side scored, who and how where this player took part), this
    //                player's F and E uses, and every player's falls caused and taken
    public sealed class DetailLeg
    {
        public string Name = "";
        public double At;          // the race clock when this player came into it
        public int Falls;
    }

    public sealed class DetailPromotion
    {
        public PieceKind Piece;
        public double At;
        public string Leg = "";
    }

    public sealed class DetailPlayer
    {
        public string Name = "";
        public bool Ours, Self;
        public PieceKind Piece;
        // Pawn Rush: place over both teams (0: did not reach the platform) and the race clock then.
        public int Place;
        public double Seconds = -1;
        // Sword Fight: falls caused and taken.
        public int Knockouts, Falls;
    }

    public sealed class DetailFall
    {
        public double At;
        public bool Ours;           // this player's side scored (the victim was on the other side)
        public bool ByMe, OfMe;     // this player was the credit / the victim
        public string Victim = "", Credit = "", Cause = "";
    }

    public sealed class MatchDetail
    {
        public const int MaxLegs = 16, MaxPromotions = 8, MaxPlayers = 12, MaxFeed = 80;
        const string Version = "D1";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The match clock at its end (Pawn Rush: the deciding crossing; Sword Fight: the result).
        public double Duration = -1;
        // Pawn Rush: the player whose crossing decided it.
        public string Clincher = "";
        public int Falls, Checkpoints, Skills, Dives, Grabs, SwordF, SwordE;
        public readonly List<DetailLeg> Legs = new List<DetailLeg>();
        public readonly List<DetailPromotion> Promotions = new List<DetailPromotion>();
        public readonly List<DetailPlayer> Players = new List<DetailPlayer>();
        public readonly List<DetailFall> Feed = new List<DetailFall>();

        // ---------- written as the match goes ----------

        // This player is in a section now (a new one only when it differs from the last).
        public void Enter(string leg, double at)
        {
            if (string.IsNullOrEmpty(leg) || (Legs.Count > 0 && Legs[Legs.Count - 1].Name == leg) || Legs.Count >= MaxLegs) return;
            Legs.Add(new DetailLeg { Name = Clean(leg), At = Math.Max(0, at) });
        }

        public void Fall()
        {
            Falls++;
            if (Legs.Count > 0) Legs[Legs.Count - 1].Falls++;
        }

        public void Promote(PieceKind piece, double at)
        {
            if (Promotions.Count >= MaxPromotions) return;
            Promotions.Add(new DetailPromotion { Piece = piece, At = Math.Max(0, at), Leg = Legs.Count > 0 ? Legs[Legs.Count - 1].Name : "" });
        }

        // The clock the times were taken on moved (a client hearing the host's race clock): every time by this much.
        public void Shift(double by)
        {
            if (by == 0 || double.IsNaN(by) || double.IsInfinity(by)) return;
            foreach (var l in Legs) l.At = Math.Max(0, l.At + by);
            foreach (var p in Promotions) p.At = Math.Max(0, p.At + by);
        }

        public void Score(DetailFall fall)
        {
            if (fall == null || Feed.Count >= MaxFeed) return;
            fall.Victim = Clean(fall.Victim); fall.Credit = Clean(fall.Credit); fall.Cause = Clean(fall.Cause);
            Feed.Add(fall);
        }

        public void Add(DetailPlayer p)
        {
            if (p == null || Players.Count >= MaxPlayers) return;
            p.Name = Clean(p.Name);
            Players.Add(p);
        }

        // ---------- read for the screen ----------

        public bool Empty => Legs.Count == 0 && Players.Count == 0 && Feed.Count == 0 && Falls == 0 && Checkpoints == 0;

        // When this player left a section: the next one's start, or (the last) `end`.
        public double LegEnd(int i, double end) => i + 1 < Legs.Count ? Legs[i + 1].At : end;

        public int Score(bool ours) => Feed.Count(f => f.Ours == ours);

        // ---------- kept as text ----------

        static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        static string N(double v) => v.ToString("0.##", Inv);

        public string Encode()
        {
            var b = new StringBuilder(256);
            b.Append(Version).Append('\n');
            b.Append("T\t").Append(N(Duration)).Append('\t').Append(Clean(Clincher)).Append('\n');
            b.Append("C\t").Append(Falls).Append('\t').Append(Checkpoints).Append('\t').Append(Skills).Append('\t').Append(Dives)
             .Append('\t').Append(Grabs).Append('\t').Append(SwordF).Append('\t').Append(SwordE).Append('\n');
            foreach (var l in Legs) b.Append("L\t").Append(Clean(l.Name)).Append('\t').Append(N(l.At)).Append('\t').Append(l.Falls).Append('\n');
            foreach (var p in Promotions) b.Append("R\t").Append((int)p.Piece).Append('\t').Append(N(p.At)).Append('\t').Append(Clean(p.Leg)).Append('\n');
            foreach (var p in Players)
                b.Append("P\t").Append(Clean(p.Name)).Append('\t').Append(p.Ours ? 1 : 0).Append('\t').Append(p.Self ? 1 : 0).Append('\t').Append((int)p.Piece)
                 .Append('\t').Append(p.Place).Append('\t').Append(N(p.Seconds)).Append('\t').Append(p.Knockouts).Append('\t').Append(p.Falls).Append('\n');
            foreach (var f in Feed)
                b.Append("F\t").Append(N(f.At)).Append('\t').Append(f.Ours ? 1 : 0).Append('\t').Append(f.ByMe ? 1 : 0).Append('\t').Append(f.OfMe ? 1 : 0)
                 .Append('\t').Append(Clean(f.Victim)).Append('\t').Append(Clean(f.Credit)).Append('\t').Append(Clean(f.Cause)).Append('\n');
            return b.ToString();
        }

        // Null for nothing, another version or no text; a line that does not read is skipped.
        public static MatchDetail Decode(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var lines = text.Replace("\r", "").Split('\n');
            if (lines[0] != Version) return null;
            var d = new MatchDetail();
            for (int i = 1; i < lines.Length; i++)
            {
                var f = lines[i].Split('\t');
                switch (f[0])
                {
                    case "T":
                        if (f.Length >= 3 && Num(f[1], out double duration)) { d.Duration = duration; d.Clincher = f[2]; }
                        break;
                    case "C":
                        if (f.Length < 8) break;
                        Int(f[1], out d.Falls); Int(f[2], out d.Checkpoints); Int(f[3], out d.Skills); Int(f[4], out d.Dives);
                        Int(f[5], out d.Grabs); Int(f[6], out d.SwordF); Int(f[7], out d.SwordE);
                        break;
                    case "L":
                        if (f.Length >= 4 && Num(f[2], out double at) && Int(f[3], out int falls) && d.Legs.Count < MaxLegs)
                            d.Legs.Add(new DetailLeg { Name = f[1], At = at, Falls = falls });
                        break;
                    case "R":
                        if (f.Length >= 4 && Int(f[1], out int piece) && ChessPieces.IsValid(piece) && Num(f[2], out double pat) && d.Promotions.Count < MaxPromotions)
                            d.Promotions.Add(new DetailPromotion { Piece = (PieceKind)piece, At = pat, Leg = f[3] });
                        break;
                    case "P":
                        if (f.Length >= 9 && Int(f[4], out int ppiece) && ChessPieces.IsValid(ppiece) && Int(f[5], out int place) &&
                            Num(f[6], out double secs) && Int(f[7], out int ko) && Int(f[8], out int pf) && d.Players.Count < MaxPlayers)
                            d.Players.Add(new DetailPlayer { Name = f[1], Ours = f[2] == "1", Self = f[3] == "1", Piece = (PieceKind)ppiece,
                                                             Place = place, Seconds = secs, Knockouts = ko, Falls = pf });
                        break;
                    case "F":
                        if (f.Length >= 8 && Num(f[1], out double fat) && d.Feed.Count < MaxFeed)
                            d.Feed.Add(new DetailFall { At = fat, Ours = f[2] == "1", ByMe = f[3] == "1", OfMe = f[4] == "1", Victim = f[5], Credit = f[6], Cause = f[7] });
                        break;
                }
            }
            return d;
        }

        // The same as one field of a record's line (no '|', no line breaks).
        public string Pack() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Encode()));

        public static MatchDetail Unpack(string field)
        {
            if (string.IsNullOrEmpty(field)) return null;
            try { return Decode(Encoding.UTF8.GetString(Convert.FromBase64String(field))); }
            catch (FormatException) { return null; }
        }

        static bool Int(string s, out int value) => int.TryParse(s, NumberStyles.AllowLeadingSign, Inv, out value);
        static bool Num(string s, out double value) =>
            double.TryParse(s, NumberStyles.Float, Inv, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
