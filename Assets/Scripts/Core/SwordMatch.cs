using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChessFight.Network
{
    // The Sword Fight prototype match (R149, spec "소드 파이트 프로토타입 구현 명세 v1.0 — 떨어뜨리고 빼앗기",
    // Docs/GameModes/SwordFight/PROTOTYPE.md). Pure rules with no Unity, like SwordFightRules (which the skill test
    // scenes still use): the phases, the score, who gets the credit for a fall and the steal it makes, the respawn
    // queue, the board's squares and the ghost pieces that erase them, and the text the host sends the clients.

    public enum SwordPhase : byte { Pick = 0, Countdown = 1, Playing = 2, SuddenDeath = 3, Result = 4 }

    /// <summary>What made a fall: shown in the kill feed ("흑 2 ← 백 1의 폰 걸음").</summary>
    public enum SwordCause : byte { Self = 0, Sword = 1, Skill = 2, Edge = 3, PawnStep = 4, Collapse = 5, Debug = 6 }

    /// <summary>The match's numbers, in one place (spec §3–§9).</summary>
    public sealed class SwordMatchSettings
    {
        public int Target = 10;
        public double Duration = 240;
        public double PickSeconds = 15;
        public double CountdownSeconds = 3;
        public double RespawnDelay = 3;
        public double CreditWindow = 3;
        public double SuddenDeathLimit = 60;
        /// <summary>The respawn tile is chosen this long before the piece comes back (the light ring, §11).</summary>
        public double RingLead = 0.8;
    }

    /// <summary>One line of the kill feed. <see cref="Stolen"/> is the piece the credit took (a pawn's steal), or
    /// 255 when nothing was taken.</summary>
    public struct SwordFeed
    {
        public int Seq;
        public ulong Victim, Credit;   // Credit 0 = "스스로"
        public int VictimTeam;
        public SwordCause Cause;
        public byte CausePiece;        // the credit's piece when it hit (for the skill's name)
        public byte Stolen;
    }

    /// <summary>The outcome of one fall, for the Unity side to act on (the steal, the stamp).</summary>
    public struct SwordRingOut
    {
        public bool Valid;
        public ulong Credit;
        public bool Stole;
        public PieceKind NewPiece;
        public SwordFeed Feed;
    }

    public sealed class SwordMatchRules
    {
        public sealed class Fighter
        {
            public ulong Id;
            public int Team;
            public PieceKind Piece = PieceKind.Pawn;
            public bool Picked, Alive = true;
            public double RespawnAt = -1;
            public int SpawnCell = -1;
            public ulong HitBy;
            public double HitAt = double.NegativeInfinity;
            public SwordCause HitCause;
            public byte HitPiece;
        }

        public const int FeedKeep = 6;
        public static readonly PieceKind[] Pickable = { PieceKind.King, PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight };

        readonly Dictionary<ulong, Fighter> fighters = new Dictionary<ulong, Fighter>();
        readonly List<SwordFeed> feed = new List<SwordFeed>();
        readonly Random draw;
        public SwordMatchSettings Settings { get; }
        public SwordPhase Phase { get; private set; } = SwordPhase.Pick;
        /// <summary>Seconds in the current phase.</summary>
        public double PhaseTime { get; private set; }
        /// <summary>The match clock: seconds of play (Playing and Sudden Death, from the end of the countdown).</summary>
        public double Clock { get; private set; }
        /// <summary>Every second since the match scene started (respawns and credits count on it).</summary>
        public double Time { get; private set; }
        public int White { get; private set; }
        public int Black { get; private set; }
        /// <summary>-1 while playing or a draw, 0 white, 1 black.</summary>
        public int Winner { get; private set; } = -1;
        public bool Draw => Phase == SwordPhase.Result && Winner < 0;
        public readonly int[] Outs = new int[2];     // falls each team caused (its score, also from self falls)
        public readonly int[] Steals = new int[2];   // steals each team made
        public int Seed { get; }
        public int FeedSeq { get; private set; }
        public IReadOnlyList<SwordFeed> Feed => feed;
        public IEnumerable<Fighter> Fighters => fighters.Values;
        public bool Finished => Phase == SwordPhase.Result;
        /// <summary>Time left on the clock (0 in Sudden Death).</summary>
        public double Remaining => Math.Max(0, Settings.Duration - Clock);
        public bool InputLocked => Phase == SwordPhase.Pick || Phase == SwordPhase.Countdown || Phase == SwordPhase.Result;
        public bool Live => Phase == SwordPhase.Playing || Phase == SwordPhase.SuddenDeath;

        public SwordMatchRules(int seed, SwordMatchSettings settings = null)
        {
            Seed = seed;
            Settings = settings ?? new SwordMatchSettings();
            draw = new Random(seed ^ 0x5F3759DF);
        }

        public Fighter Get(ulong id)
        {
            Fighter f;
            return fighters.TryGetValue(id, out f) ? f : null;
        }

        /// <summary>A player in the match (joining late: a pawn, its pick already over).</summary>
        public Fighter Add(ulong id, int team)
        {
            Fighter f = Get(id);
            if (f != null) { f.Team = team; return f; }
            f = new Fighter { Id = id, Team = Math.Max(0, Math.Min(1, team)) };
            if (Phase != SwordPhase.Pick) f.Picked = true;
            fighters[id] = f;
            return f;
        }

        public void Remove(ulong id) { fighters.Remove(id); }

        /// <summary>A teammate already has this piece (picks only: steals may double one up, spec §3.3).</summary>
        public bool Taken(int team, PieceKind piece, ulong except = 0)
        {
            foreach (var f in fighters.Values)
                if (f.Id != except && f.Team == team && f.Picked && f.Piece == piece) return true;
            return false;
        }

        /// <summary>The pick (spec §3.2): once, in the pick phase, not a pawn, not a teammate's.</summary>
        public bool Pick(ulong id, PieceKind piece)
        {
            Fighter f = Get(id);
            if (f == null || Phase != SwordPhase.Pick || f.Picked || Array.IndexOf(Pickable, piece) < 0 || Taken(f.Team, piece, id)) return false;
            f.Piece = piece;
            f.Picked = true;
            return true;
        }

        /// <summary>Everyone still without a piece gets one of the pieces left on their side, at random.</summary>
        public void PickRest()
        {
            var ids = new List<ulong>(fighters.Keys);
            ids.Sort();
            foreach (ulong id in ids)
            {
                Fighter f = fighters[id];
                if (f.Picked) continue;
                var left = new List<PieceKind>();
                foreach (var p in Pickable) if (!Taken(f.Team, p)) left.Add(p);
                // More than five a side: the sixth doubles one up.
                f.Piece = left.Count > 0 ? left[draw.Next(left.Count)] : Pickable[draw.Next(Pickable.Length)];
                f.Picked = true;
            }
        }

        public bool AllPicked
        {
            get
            {
                if (fighters.Count == 0) return false;
                foreach (var f in fighters.Values) if (!f.Picked) return false;
                return true;
            }
        }

        /// <summary>One step. Returns true when the phase changed this step.</summary>
        public bool Advance(double dt)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt <= 0) return false;
            Time += dt;
            PhaseTime += dt;
            switch (Phase)
            {
                case SwordPhase.Pick:
                    if (AllPicked || PhaseTime >= Settings.PickSeconds) { PickRest(); Enter(SwordPhase.Countdown); return true; }
                    return false;
                case SwordPhase.Countdown:
                    if (PhaseTime >= Settings.CountdownSeconds) { Enter(SwordPhase.Playing); return true; }
                    return false;
                case SwordPhase.Playing:
                    Clock = Math.Min(Settings.Duration, Clock + dt);
                    if (Clock >= Settings.Duration)
                    {
                        if (White == Black) Enter(SwordPhase.SuddenDeath);
                        else Finish(White > Black ? 0 : 1);
                        return true;
                    }
                    return false;
                case SwordPhase.SuddenDeath:
                    Clock += dt;
                    if (PhaseTime >= Settings.SuddenDeathLimit) { Finish(-1); return true; }
                    return false;
            }
            return false;
        }

        void Enter(SwordPhase phase)
        {
            Phase = phase;
            PhaseTime = 0;
        }

        void Finish(int winner)
        {
            Winner = winner;
            Enter(SwordPhase.Result);
        }

        /// <summary>A hit that counts for the credit (spec §8.2): the sword, a skill, an edge skill, a pawn's step, a
        /// rook's collapse. An ally's hit does not move the credit (it is not "the last enemy").</summary>
        public void Hit(ulong attacker, ulong victim, SwordCause cause, byte piece)
        {
            Fighter a = Get(attacker), v = Get(victim);
            if (a == null || v == null || a == v || a.Team == v.Team) return;
            v.HitBy = attacker;
            v.HitAt = Time;
            v.HitCause = cause;
            v.HitPiece = piece;
        }

        /// <summary>The fall is final (the hips 3 m under the board, spec §8.1): the point, the credit, the steal, the
        /// respawn queue and the feed, once per life.</summary>
        public SwordRingOut RingOut(ulong id)
        {
            var result = new SwordRingOut();
            Fighter v = Get(id);
            if (v == null || !v.Alive || !Live) return result;
            v.Alive = false;
            v.RespawnAt = Time + Settings.RespawnDelay;
            v.SpawnCell = -1;
            int scorer = 1 - v.Team;
            if (scorer == 0) White++; else Black++;
            Outs[scorer]++;

            Fighter credit = Get(v.HitBy);
            bool counts = credit != null && credit.Team != v.Team && Time - v.HitAt <= Settings.CreditWindow + 1e-9;
            var line = new SwordFeed { Seq = ++FeedSeq, Victim = id, VictimTeam = v.Team, Stolen = 255,
                Cause = counts ? v.HitCause : SwordCause.Self, CausePiece = counts ? v.HitPiece : (byte)0 };
            result.Valid = true;
            if (counts)
            {
                line.Credit = credit.Id;
                result.Credit = credit.Id;
                if (credit.Piece == PieceKind.Pawn && v.Piece != PieceKind.Pawn)
                {
                    credit.Piece = v.Piece;
                    result.Stole = true;
                    result.NewPiece = v.Piece;
                    line.Stolen = (byte)v.Piece;
                    Steals[credit.Team]++;
                }
            }
            v.HitBy = 0;
            v.HitAt = double.NegativeInfinity;
            result.Feed = line;
            feed.Add(line);
            while (feed.Count > FeedKeep) feed.RemoveAt(0);

            if (White >= Settings.Target || Black >= Settings.Target) Finish(White > Black ? 0 : 1);
            else if (Phase == SwordPhase.SuddenDeath) Finish(scorer);
            return result;
        }

        /// <summary>Seconds until this piece comes back (0 when alive).</summary>
        public double RespawnLeft(ulong id)
        {
            Fighter f = Get(id);
            return f == null || f.Alive ? 0 : Math.Max(0, f.RespawnAt - Time);
        }

        /// <summary>Out and due to choose its tile now (the ring shows this long before it comes back).</summary>
        public bool WantsCell(ulong id)
        {
            Fighter f = Get(id);
            return f != null && !f.Alive && f.SpawnCell < 0 && Phase != SwordPhase.Result && f.RespawnAt - Time <= Settings.RingLead;
        }

        /// <summary>Back now: alive again, always a pawn (spec §3.3), its out flag cleared (§8.1).</summary>
        public bool TryRespawn(ulong id)
        {
            Fighter f = Get(id);
            if (f == null || f.Alive || Phase == SwordPhase.Result || Time < f.RespawnAt) return false;
            f.Alive = true;
            f.Piece = PieceKind.Pawn;
            f.RespawnAt = -1;
            f.HitBy = 0;
            f.HitAt = double.NegativeInfinity;
            return true;
        }

        /// <summary>A new host carries on from the last state the old one sent (HANDOFF rule 15).</summary>
        public void Restore(SwordMatchState s)
        {
            Phase = s.Phase;
            PhaseTime = s.PhaseTime;
            Clock = s.Clock;
            White = s.White;
            Black = s.Black;
            Winner = s.Winner;
            Outs[0] = s.OutsWhite; Outs[1] = s.OutsBlack;
            Steals[0] = s.StealsWhite; Steals[1] = s.StealsBlack;
            FeedSeq = s.FeedSeq;
        }

        /// <summary>Development (F9): the clock jumps.</summary>
        public void SetClock(double seconds)
        {
            if (!Live || double.IsNaN(seconds)) return;
            Clock = Math.Max(0, Math.Min(Settings.Duration - 0.01, seconds));
        }

        /// <summary>Everything a screen needs (the host builds it from the rules, a client reads it off the wire).</summary>
        public SwordMatchState State()
        {
            var s = new SwordMatchState
            {
                Phase = Phase, PhaseTime = PhaseTime, Clock = Clock, Seed = Seed, White = White, Black = Black, Winner = Winner,
                OutsWhite = Outs[0], OutsBlack = Outs[1], StealsWhite = Steals[0], StealsBlack = Steals[1], FeedSeq = FeedSeq,
            };
            foreach (var f in fighters.Values)
                s.Fighters.Add(new SwordMatchFighter { Id = f.Id, Piece = f.Piece, Picked = f.Picked, Alive = f.Alive,
                    SpawnCell = f.SpawnCell, Respawn = (float)RespawnLeft(f.Id) });
            s.Feed.AddRange(feed);
            return s;
        }
    }

    public struct SwordMatchFighter
    {
        public ulong Id;
        public PieceKind Piece;
        public bool Picked, Alive, EReady;
        public int SpawnCell;
        public float Respawn;
    }

    public sealed class SwordMatchState
    {
        public SwordPhase Phase;
        public double PhaseTime, Clock;
        public int Seed, White, Black, Winner = -1;
        public int OutsWhite, OutsBlack, StealsWhite, StealsBlack, FeedSeq;
        public readonly List<SwordMatchFighter> Fighters = new List<SwordMatchFighter>();
        public readonly List<SwordFeed> Feed = new List<SwordFeed>();

        public SwordMatchFighter? Find(ulong id)
        {
            foreach (var f in Fighters) if (f.Id == id) return f;
            return null;
        }
    }

    /// <summary>
    /// The match state as text (RagdollNetProtocol.TypeMatch, as Queen of the Hill's), host to every client a few times
    /// a second. Pieces go by roster place (SkillWire.Ref), not by id, to stay well under the packet's 400 characters:
    /// <c>phase,phaseTime,clock,seed,white,black,winner,outsW,outsB,stealsW,stealsB,feedSeq|ref:piece:flags:cell:respawn;…|seq:victim:credit:team:cause:piece:stolen;…</c>
    /// </summary>
    public static class SwordMatchWire
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Encode(SwordMatchState s, Func<ulong, byte> refOf)
        {
            var b = new StringBuilder(256);
            b.Append((int)s.Phase).Append(',').Append(s.PhaseTime.ToString("0.##", Inv)).Append(',').Append(s.Clock.ToString("0.##", Inv))
             .Append(',').Append(s.Seed).Append(',').Append(s.White).Append(',').Append(s.Black).Append(',').Append(s.Winner)
             .Append(',').Append(s.OutsWhite).Append(',').Append(s.OutsBlack).Append(',').Append(s.StealsWhite).Append(',').Append(s.StealsBlack)
             .Append(',').Append(s.FeedSeq).Append('|');
            bool first = true;
            foreach (var f in s.Fighters)
            {
                byte r = refOf(f.Id);
                if (r == SkillWire.None) continue;
                if (!first) b.Append(';');
                first = false;
                int flags = (f.Picked ? 1 : 0) | (f.Alive ? 2 : 0) | (f.EReady ? 4 : 0);
                b.Append(r).Append(':').Append((int)f.Piece).Append(':').Append(flags).Append(':').Append(f.SpawnCell)
                 .Append(':').Append(f.Respawn.ToString("0.#", Inv));
            }
            b.Append('|');
            first = true;
            foreach (var e in s.Feed)
            {
                if (!first) b.Append(';');
                first = false;
                b.Append(e.Seq).Append(':').Append(refOf(e.Victim)).Append(':').Append(e.Credit == 0 ? SkillWire.None : refOf(e.Credit))
                 .Append(':').Append(e.VictimTeam).Append(':').Append((int)e.Cause).Append(':').Append(e.CausePiece).Append(':').Append(e.Stolen);
            }
            return b.ToString();
        }

        public static bool Decode(string text, Func<byte, ulong> idOf, out SwordMatchState state)
        {
            state = null;
            if (string.IsNullOrEmpty(text) || text.Length > 1000) return false;
            string[] parts = text.Split('|');
            if (parts.Length != 3) return false;
            string[] h = parts[0].Split(',');
            if (h.Length != 12) return false;
            var s = new SwordMatchState();
            int phase;
            if (!int.TryParse(h[0], NumberStyles.Integer, Inv, out phase) || phase < 0 || phase > 4) return false;
            s.Phase = (SwordPhase)phase;
            if (!double.TryParse(h[1], NumberStyles.Float, Inv, out s.PhaseTime) || !double.TryParse(h[2], NumberStyles.Float, Inv, out s.Clock)) return false;
            if (!Int(h[3], out s.Seed) || !Int(h[4], out s.White) || !Int(h[5], out s.Black) || !Int(h[6], out s.Winner) ||
                !Int(h[7], out s.OutsWhite) || !Int(h[8], out s.OutsBlack) || !Int(h[9], out s.StealsWhite) || !Int(h[10], out s.StealsBlack) ||
                !Int(h[11], out s.FeedSeq)) return false;
            if (double.IsNaN(s.PhaseTime) || double.IsNaN(s.Clock) || s.Clock < 0 || s.Clock > 3600) return false;
            if (parts[1].Length > 0)
                foreach (string item in parts[1].Split(';'))
                {
                    string[] p = item.Split(':');
                    int r, piece, flags, cell;
                    float respawn;
                    if (p.Length != 5 || !Int(p[0], out r) || !Int(p[1], out piece) || !Int(p[2], out flags) || !Int(p[3], out cell) ||
                        !float.TryParse(p[4], NumberStyles.Float, Inv, out respawn)) return false;
                    if (r < 0 || r > 254 || !ChessPieces.IsValid(piece) || cell < -1 || cell > 255) return false;
                    ulong id = idOf((byte)r);
                    if (id == 0) continue;   // not seated here yet
                    s.Fighters.Add(new SwordMatchFighter { Id = id, Piece = (PieceKind)piece, Picked = (flags & 1) != 0, Alive = (flags & 2) != 0,
                        EReady = (flags & 4) != 0, SpawnCell = cell, Respawn = respawn });
                }
            if (parts[2].Length > 0)
                foreach (string item in parts[2].Split(';'))
                {
                    string[] p = item.Split(':');
                    int seq, victim, credit, team, cause, causePiece, stolen;
                    if (p.Length != 7 || !Int(p[0], out seq) || !Int(p[1], out victim) || !Int(p[2], out credit) || !Int(p[3], out team) ||
                        !Int(p[4], out cause) || !Int(p[5], out causePiece) || !Int(p[6], out stolen)) return false;
                    if (cause < 0 || cause > 6 || team < 0 || team > 1) return false;
                    s.Feed.Add(new SwordFeed { Seq = seq, Victim = victim >= 0 && victim < 255 ? idOf((byte)victim) : 0,
                        Credit = credit >= 0 && credit < 255 ? idOf((byte)credit) : 0, VictimTeam = team, Cause = (SwordCause)cause,
                        CausePiece = (byte)causePiece, Stolen = (byte)stolen });
                }
            state = s;
            return true;
        }

        static bool Int(string text, out int value) { return int.TryParse(text, NumberStyles.Integer, Inv, out value); }
    }

    /// <summary>
    /// The board (spec §5, §9): 8 × 8 squares, file x 0..7 (a..h), rank y 0..7 (1..8), white at the bottom (ranks 1–2),
    /// black at the top (7–8). A square is <c>y * 8 + x</c>. The ghost pieces' plan comes from the match's seed only,
    /// so every PC works out the same squares from the seed and the clock (nothing per tick on the wire).
    /// </summary>
    public static class SwordBoard
    {
        public const int Size = 8, Count = Size * Size;
        public static int Cell(int x, int y) => x < 0 || y < 0 || x >= Size || y >= Size ? -1 : y * Size + x;
        public static int X(int cell) => cell % Size;
        public static int Y(int cell) => cell / Size;
        public static bool Rim(int cell) => X(cell) == 0 || Y(cell) == 0 || X(cell) == Size - 1 || Y(cell) == Size - 1;

        /// <summary>Where slot <paramref name="slot"/> of a team starts: the middle files of its two home ranks, one each.</summary>
        public static int StartCell(int team, int slot)
        {
            int[] files = { 3, 4, 2, 5, 1, 6, 0, 7 };
            int s = Math.Max(0, slot) % 16;
            int x = files[s % 8], rank = s % 2 == 0 ? 1 : 0;
            if (s >= 8) rank = 1 - rank;
            return Cell(x, team == 0 ? rank : Size - 1 - rank);
        }

        /// <summary>The squares a team may come back on: its ranks 2–3 (6–7 for black); once the queen has cleared the
        /// rim, the inner squares of its half (spec §9).</summary>
        public static List<int> SpawnCells(int team, bool[] erased, bool queenDone)
        {
            var list = new List<int>();
            for (int c = 0; c < Count; c++)
            {
                if (erased != null && erased[c]) continue;
                int y = Y(c), x = X(c);
                int own = team == 0 ? y : Size - 1 - y;
                if (queenDone) { if (own >= 1 && own <= 3 && x >= 1 && x <= 6) list.Add(c); }
                else if (own == 1 || own == 2) list.Add(c);
            }
            return list;
        }

        /// <summary>The respawn square (spec §5): not erased, 1.2 m from every other piece, then the one farthest from its
        /// nearest enemy. Positions are board metres (x, z) like <see cref="Centre"/>. Falls back to the farthest square
        /// from any piece when every square is crowded.</summary>
        public static int ChooseSpawn(int team, bool[] erased, bool queenDone, double cellSize, double[] othersX, double[] othersZ,
                                      double[] enemiesX, double[] enemiesZ)
        {
            var cells = SpawnCells(team, erased, queenDone);
            if (cells.Count == 0)
                for (int c = 0; c < Count; c++) if (erased == null || !erased[c]) cells.Add(c);
            int best = -1, crowdedBest = -1;
            double bestScore = double.MinValue, crowdedScore = double.MinValue;
            foreach (int c in cells)
            {
                double cx, cz;
                Centre(c, cellSize, out cx, out cz);
                double nearestAny = Nearest(cx, cz, othersX, othersZ);
                double nearestEnemy = Nearest(cx, cz, enemiesX, enemiesZ);
                if (nearestAny >= 1.2 && nearestEnemy > bestScore) { bestScore = nearestEnemy; best = c; }
                if (nearestAny > crowdedScore) { crowdedScore = nearestAny; crowdedBest = c; }
            }
            return best >= 0 ? best : crowdedBest;
        }

        static double Nearest(double x, double z, double[] xs, double[] zs)
        {
            double best = 1e9;
            if (xs == null || zs == null) return best;
            for (int i = 0; i < Math.Min(xs.Length, zs.Length); i++)
                best = Math.Min(best, Math.Sqrt((xs[i] - x) * (xs[i] - x) + (zs[i] - z) * (zs[i] - z)));
            return best;
        }

        /// <summary>A square's centre in metres, the board centred on the world origin.</summary>
        public static void Centre(int cell, double cellSize, out double x, out double z)
        {
            x = (X(cell) - Size / 2 + 0.5) * cellSize;
            z = (Y(cell) - Size / 2 + 0.5) * cellSize;
        }

        /// <summary>The square under a point (−1 off the board).</summary>
        public static int At(double x, double z, double cellSize)
        {
            int cx = (int)Math.Floor(x / cellSize + Size / 2), cy = (int)Math.Floor(z / cellSize + Size / 2);
            return Cell(cx, cy);
        }

        /// <summary>The squares left are one piece (4-way neighbours). An empty board is not.</summary>
        public static bool Connected(bool[] erased)
        {
            int start = -1, left = 0;
            for (int c = 0; c < Count; c++) if (!erased[c]) { left++; if (start < 0) start = c; }
            if (start < 0) return false;
            var seen = new bool[Count];
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;
            int reached = 0;
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            while (stack.Count > 0)
            {
                int c = stack.Pop();
                reached++;
                for (int k = 0; k < 4; k++)
                {
                    int n = Cell(X(c) + dx[k], Y(c) + dy[k]);
                    if (n < 0 || erased[n] || seen[n]) continue;
                    seen[n] = true;
                    stack.Push(n);
                }
            }
            return reached == left;
        }
    }

    /// <summary>One ghost piece's pass (spec §9): when it starts its warning, the piece it is, its squares in path order.</summary>
    public sealed class SwordGhost
    {
        public double At;
        public PieceKind Piece;
        public int[] Cells = new int[0];
        public const double Warning = 1.5;
        public double DropAt => At + Warning;
    }

    public static class SwordGhosts
    {
        public static readonly double[] Times = { 120, 150, 180, 210 };

        /// <summary>
        /// The four passes from the seed: a rook's line of six (its ends kept), a bishop's short diagonal of four, a
        /// knight's L of three, then the queen's whole rim. A random line, diagonal or L is drawn again (20 tries, then
        /// that pass is skipped: an empty <see cref="SwordGhost.Cells"/>) unless what is left stays one piece and each team
        /// keeps two respawn squares - both right after it and after the queen's rim too, so no pass ever leaves an island.
        /// </summary>
        public static List<SwordGhost> Plan(int seed)
        {
            var rng = new Random(seed);
            var erased = new bool[SwordBoard.Count];
            var plan = new List<SwordGhost>();
            for (int wave = 0; wave < 4; wave++)
            {
                var g = new SwordGhost { At = Times[wave], Piece = wave == 0 ? PieceKind.Rook : wave == 1 ? PieceKind.Bishop : wave == 2 ? PieceKind.Knight : PieceKind.Queen };
                if (wave == 3)
                {
                    g.Cells = RimPath();
                }
                else
                {
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        int[] cells = wave == 0 ? RookLine(rng) : wave == 1 ? BishopDiagonal(rng) : KnightL(rng);
                        if (cells == null || !Fits(erased, cells)) continue;
                        g.Cells = cells;
                        break;
                    }
                }
                foreach (int c in g.Cells) erased[c] = true;
                plan.Add(g);
            }
            return plan;
        }

        /// <summary>Erasing <paramref name="cells"/> now keeps the board whole with two respawn squares a side, and so does
        /// the queen's rim after it.</summary>
        public static bool Fits(bool[] erased, int[] cells)
        {
            var now = (bool[])erased.Clone();
            foreach (int c in cells) { if (c < 0 || now[c]) return false; now[c] = true; }
            if (!Healthy(now, false)) return false;
            var later = (bool[])now.Clone();
            for (int c = 0; c < SwordBoard.Count; c++) if (SwordBoard.Rim(c)) later[c] = true;
            return Healthy(later, true);
        }

        static bool Healthy(bool[] erased, bool queenDone) =>
            SwordBoard.Connected(erased) && SwordBoard.SpawnCells(0, erased, queenDone).Count >= 2 && SwordBoard.SpawnCells(1, erased, queenDone).Count >= 2;

        static int[] RookLine(Random rng)
        {
            bool rank = rng.Next(2) == 0;
            int line = rng.Next(SwordBoard.Size);
            var cells = new int[6];
            bool forward = rng.Next(2) == 0;
            for (int i = 0; i < 6; i++)
            {
                int k = forward ? 1 + i : 6 - i;
                cells[i] = rank ? SwordBoard.Cell(k, line) : SwordBoard.Cell(line, k);
            }
            return cells;
        }

        static int[] BishopDiagonal(Random rng)
        {
            int dx = rng.Next(2) == 0 ? 1 : -1, dy = rng.Next(2) == 0 ? 1 : -1;
            int x = rng.Next(SwordBoard.Size), y = rng.Next(SwordBoard.Size);
            var cells = new int[4];
            for (int i = 0; i < 4; i++)
            {
                cells[i] = SwordBoard.Cell(x + dx * i, y + dy * i);
                if (cells[i] < 0) return null;
            }
            return cells;
        }

        static int[] KnightL(Random rng)
        {
            int[] ax = { 1, -1, 0, 0 }, ay = { 0, 0, 1, -1 };
            int a = rng.Next(4);
            int px = ay[a], py = ax[a];   // perpendicular
            if (rng.Next(2) == 0) { px = -px; py = -py; }
            int x = rng.Next(SwordBoard.Size), y = rng.Next(SwordBoard.Size);
            int[] cells = { SwordBoard.Cell(x + ax[a], y + ay[a]), SwordBoard.Cell(x + 2 * ax[a], y + 2 * ay[a]), SwordBoard.Cell(x + 2 * ax[a] + px, y + 2 * ay[a] + py) };
            foreach (int c in cells) if (c < 0) return null;
            return cells;
        }

        /// <summary>The rim in path order, round from a1.</summary>
        static int[] RimPath()
        {
            var list = new List<int>();
            int n = SwordBoard.Size - 1;
            for (int x = 0; x < n; x++) list.Add(SwordBoard.Cell(x, 0));
            for (int y = 0; y < n; y++) list.Add(SwordBoard.Cell(n, y));
            for (int x = n; x > 0; x--) list.Add(SwordBoard.Cell(x, n));
            for (int y = n; y > 0; y--) list.Add(SwordBoard.Cell(0, y));
            return list.ToArray();
        }

        /// <summary>The squares erased (dropped) by this clock (the passes whose warning is over).</summary>
        public static bool[] ErasedAt(List<SwordGhost> plan, double clock)
        {
            var erased = new bool[SwordBoard.Count];
            foreach (var g in plan) if (clock >= g.DropAt) foreach (int c in g.Cells) erased[c] = true;
            return erased;
        }

        public static bool QueenDone(List<SwordGhost> plan, double clock) => plan.Count >= 4 && clock >= plan[3].DropAt;
    }
}
