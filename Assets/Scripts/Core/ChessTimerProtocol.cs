using System;
using System.IO;
using System.Collections.Generic;

namespace ChessFight.Network
{
    public sealed class ChessTimerPlayerState
    {
        public ulong Id;
        public bool Cutter, Exited;
        public int Role;
        public float Respawn, BlastX, BlastY, BlastZ;
        public uint Blast;
    }
    public sealed class ChessTimerState
    {
        public uint Tick, Round;
        public bool Started;
        public float Elapsed, Rocker;
        public ChessTimerRules Rules;
        public readonly ChessWirePuzzle.Course[] Courses = new ChessWirePuzzle.Course[2];
        public readonly int[] Riders = new int[2], Workers = new int[2];
        public readonly float[] SolveTimes = new float[2];
        public readonly ChessTimerPlayerState[] Players = new ChessTimerPlayerState[12];
    }
    // Full, bounded recovery snapshots. No client-supplied game state is ever applied by the host.
    public static class ChessTimerProtocol
    {
        public const uint Magic = 0x43544331; // CTC1, dedicated Steam channel 34
        public const int MaxBytes = 4096;
        public static byte[] Write(ulong match, ChessTimerState s)
        {
            using (var stream = new MemoryStream()) using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic); w.Write(match); w.Write(s.Tick); w.Write(s.Round); w.Write(s.Started);
                w.Write(s.Elapsed); w.Write(s.Rocker); s.Rules.WriteNetwork(w);
                for (int t = 0; t < 2; t++)
                { s.Courses[t].WriteNetwork(w); w.Write((byte)s.Riders[t]); w.Write((byte)s.Workers[t]); w.Write(s.SolveTimes[t]); }
                foreach (var p in s.Players)
                {
                    w.Write(p.Id); w.Write(p.Cutter); w.Write(p.Exited); w.Write((byte)p.Role); w.Write(p.Respawn);
                    w.Write(p.Blast); w.Write(p.BlastX); w.Write(p.BlastY); w.Write(p.BlastZ);
                }
                if (stream.Length > MaxBytes) throw new InvalidDataException("Timer state too large");
                return stream.ToArray();
            }
        }
        public static bool Read(byte[] data, ulong match, out ChessTimerState state)
        {
            state = null;
            if (data == null || data.Length < 30 || data.Length > MaxBytes) return false;
            try
            {
                using (var r = new BinaryReader(new MemoryStream(data)))
                {
                    if (r.ReadUInt32() != Magic || r.ReadUInt64() != match) return false;
                    var s = new ChessTimerState { Tick = r.ReadUInt32(), Round = r.ReadUInt32(), Started = Flag(r),
                        Elapsed = Number(r, 0, 86400), Rocker = Number(r, -3, 3), Rules = ChessTimerRules.ReadNetwork(r) };
                    for (int t = 0; t < 2; t++)
                    {
                        s.Courses[t] = ChessWirePuzzle.Course.ReadNetwork(r);
                        s.Riders[t] = Integer(r.ReadByte(), 0, 12); s.Workers[t] = Integer(r.ReadByte(), 0, 6);
                        s.SolveTimes[t] = Number(r, -1, 86400);
                        for (int n = 0; n < s.Courses[t].Current.Board.WireCount; n++)
                        {
                            int worker = s.Courses[t].Current.WorkerAt(n);
                            if (worker != -1 && worker / 6 != t) return false;
                        }
                    }
                    var ids = new HashSet<ulong>();
                    for (int i = 0; i < 12; i++)
                    {
                        var p = new ChessTimerPlayerState { Id = r.ReadUInt64(), Cutter = Flag(r), Exited = Flag(r), Role = Integer(r.ReadByte(), 0, 5),
                            Respawn = Number(r, 0, 3), Blast = r.ReadUInt32(), BlastX = Number(r, -1000, 1000), BlastY = Number(r, -1000, 1000), BlastZ = Number(r, -1000, 1000) };
                        if (p.Id != 0 && !ids.Add(p.Id)) return false;
                        s.Players[i] = p;
                    }
                    if (r.BaseStream.Position != data.Length) return false;
                    state = s; return true;
                }
            }
            catch (InvalidDataException) { return false; }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
        }
        internal static bool Flag(BinaryReader r) => Integer(r.ReadByte(), 0, 1) == 1;
        internal static int Integer(int value, int min, int max)
        { if (value < min || value > max) throw new InvalidDataException("Timer integer range"); return value; }
        internal static float Number(BinaryReader r, float min, float max)
        { float v = r.ReadSingle(); if (float.IsNaN(v) || float.IsInfinity(v) || v < min || v > max) throw new InvalidDataException("Timer number range"); return v; }
    }
    public sealed partial class ChessTimerRules
    {
        internal void WriteNetwork(BinaryWriter w)
        {
            w.Write(Limit); w.Write(WorkSeconds); w.Write(SwitchDelay); w.Write((byte)SoloRiders);
            w.Write((sbyte)Winner); w.Write((sbyte)PressedTeam); w.Write((sbyte)candidate); w.Write(stable);
            foreach (var t in Teams)
            { w.Write(t.Remaining); w.Write(t.Work); w.Write((byte)t.Stage); w.Write(t.Explosions); w.Write(t.Expired); w.Write(t.GateOpen); }
        }
        internal static ChessTimerRules ReadNetwork(BinaryReader r)
        {
            var s = new ChessTimerRules(ChessTimerProtocol.Number(r, 1, 3600), ChessTimerProtocol.Number(r, 1, 3600),
                ChessTimerProtocol.Number(r, 0, 60), ChessTimerProtocol.Integer(r.ReadByte(), 1, 12));
            s.Winner = ChessTimerProtocol.Integer(r.ReadSByte(), -2, 1); s.PressedTeam = ChessTimerProtocol.Integer(r.ReadSByte(), -1, 1);
            s.candidate = ChessTimerProtocol.Integer(r.ReadSByte(), -1, 1); s.stable = ChessTimerProtocol.Number(r, 0, 86400);
            foreach (var t in s.Teams)
            {
                t.Remaining = ChessTimerProtocol.Number(r, 0, s.Limit); t.Work = ChessTimerProtocol.Number(r, 0, s.WorkSeconds);
                t.Stage = ChessTimerProtocol.Integer(r.ReadByte(), 0, 3); t.Explosions = ChessTimerProtocol.Integer(r.ReadInt32(), 0, 1000);
                t.Expired = ChessTimerProtocol.Flag(r); t.GateOpen = ChessTimerProtocol.Flag(r);
            }
            return s;
        }
    }
    public sealed partial class ChessWirePuzzle
    {
        public sealed partial class Course
        {
            internal void WriteNetwork(BinaryWriter w)
            {
                w.Write(cutSeconds); w.Write(lockSeconds); w.Write((byte)Stage);
                for (int stage = 0; stage < 3; stage++)
                {
                    var b = stages[stage]; w.Write((sbyte)b.PawnDirection);
                    w.Write((byte)b.PieceCount);
                    for (int i = 0; i < b.PieceCount; i++) { w.Write((byte)(b.PieceRank(i) * 8 + b.PieceFile(i))); w.Write((byte)b.PieceKind(i)); }
                    for (int i = 0; i < b.WireCount; i++) w.Write((byte)(b.Rank(i) * 8 + b.File(i)));
                }
                Current.WriteNetwork(w);
            }
            internal static Course ReadNetwork(BinaryReader r)
            {
                float cutTime = ChessTimerProtocol.Number(r, .1f, 120), lockTime = ChessTimerProtocol.Number(r, 0, 120);
                int stage = ChessTimerProtocol.Integer(r.ReadByte(), 0, 2);
                var boards = new Layout[3];
                for (int n = 0; n < 3; n++)
                {
                    int direction = r.ReadSByte(); if (direction != -1 && direction != 1) throw new InvalidDataException("Pawn direction");
                    int count = r.ReadByte(); if (count != (n == 0 ? 4 : n == 1 ? 11 : 15)) throw new InvalidDataException("Piece count");
                    var pieces = new int[count]; var kinds = new Kind[count]; var used = new HashSet<int>();
                    for (int i = 0; i < count; i++)
                    {
                        pieces[i] = ChessTimerProtocol.Integer(r.ReadByte(), 0, 63); kinds[i] = (Kind)ChessTimerProtocol.Integer(r.ReadByte(), 0, 4);
                        if (!used.Add(pieces[i])) throw new InvalidDataException("Duplicate piece");
                    }
                    var wires = new int[n + 4];
                    for (int i = 0; i < wires.Length; i++) { wires[i] = ChessTimerProtocol.Integer(r.ReadByte(), 0, 63); if (!used.Add(wires[i])) throw new InvalidDataException("Occupied wire"); }
                    boards[n] = new Layout(pieces, kinds, wires, direction);
                    int safe = 0; for (int i = 0; i < wires.Length; i++) if (!boards[n].Attacked(wires[i] % 8, wires[i] / 8)) safe++;
                    if (safe != boards[n].RequiredCuts) throw new InvalidDataException("Invalid answers");
                }
                var c = new Course(boards, cutTime, lockTime); c.Stage = stage;
                c.Current = new ChessWirePuzzle(cutTime, lockTime, boards[stage]); c.Current.ReadNetwork(r);
                return c;
            }
        }
        void WriteNetwork(BinaryWriter w)
        {
            w.Write(LockLeft);
            for (int i = 0; i < cut.Length; i++) { w.Write(cut[i]); w.Write(progress[i]); w.Write((sbyte)owners[i]); }
        }
        void ReadNetwork(BinaryReader r)
        {
            LockLeft = ChessTimerProtocol.Number(r, 0, LockSeconds); CorrectCuts = 0; Progress = 0;
            var workers = new HashSet<int>();
            for (int i = 0; i < cut.Length; i++)
            {
                cut[i] = ChessTimerProtocol.Flag(r); progress[i] = ChessTimerProtocol.Number(r, 0, CutSeconds);
                owners[i] = ChessTimerProtocol.Integer(r.ReadSByte(), -1, 11);
                if (cut[i] && (progress[i] != 0 || owners[i] != -1) || owners[i] == -1 && progress[i] > 0 || owners[i] >= 0 && !workers.Add(owners[i])) throw new InvalidDataException("Invalid cut state");
                if (cut[i] && !Board.Attacked(Board.File(i), Board.Rank(i))) CorrectCuts++;
                Progress = Math.Max(Progress, progress[i]);
            }
        }
    }
}
