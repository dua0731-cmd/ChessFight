using System;
using System.Collections.Generic;
using System.IO;

namespace ChessFight.Network
{
    public struct MoveInput { public uint Sequence; public float X, Z; public bool Jump; }
    public struct PawnState { public ulong Id; public uint Ack; public float X, Y, Z, Vertical; public int Team, Slot; }

    // Flat test arena motor. Only the host advances authoritative positions.
    // This is intentionally independent of ragdoll/physics and future game abilities.
    public static class PawnMotor
    {
        public const float Step = 1f / 30f, Speed = 6f, Radius = 19f;
        public static PawnState Spawn(ulong id, int team, int slot) => new PawnState
        { Id = id, Team = team, Slot = slot, X = team == 0 ? -8 : 8, Z = (slot - 2.5f) * 2f, Y = 1 };
        public static PawnState Advance(PawnState s, MoveInput input, float dt)
        {
            float length = (float)Math.Sqrt(input.X * input.X + input.Z * input.Z);
            float scale = length > 1 ? 1 / length : 1;
            s.X = Clamp(s.X + input.X * scale * Speed * dt, -Radius, Radius);
            s.Z = Clamp(s.Z + input.Z * scale * Speed * dt, -Radius, Radius);
            if (s.Y <= 1.001f && input.Jump) s.Vertical = 7;
            s.Vertical -= 22 * dt;
            s.Y += s.Vertical * dt;
            if (s.Y < 1) { s.Y = 1; s.Vertical = 0; }
            s.Ack = input.Sequence;
            return s;
        }
        static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(max, x));
    }

    public static class MotionProtocol
    {
        const uint Magic = 0x43464631;
        public const int MaxBytes = 1024;
        public static bool Newer(uint value, uint previous) => unchecked((int)(value - previous)) > 0;
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        public static byte[] Input(ulong session, MoveInput input)
        {
            using (var stream = new MemoryStream()) using (var w = new BinaryWriter(stream))
            { w.Write(Magic); w.Write((byte)1); w.Write(session); w.Write(input.Sequence);
              w.Write(input.X); w.Write(input.Z); w.Write(input.Jump); return stream.ToArray(); }
        }
        public static bool ReadInput(byte[] bytes, ulong session, out MoveInput input)
        {
            input = default;
            if (bytes == null || bytes.Length != 26) return false;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (r.ReadUInt32() != Magic || r.ReadByte() != 1 || r.ReadUInt64() != session) return false;
                input.Sequence = r.ReadUInt32(); input.X = r.ReadSingle(); input.Z = r.ReadSingle(); input.Jump = r.ReadBoolean();
                return Finite(input.X) && Finite(input.Z) && Math.Abs(input.X) <= 1.01f && Math.Abs(input.Z) <= 1.01f;
            }
        }
        public static byte[] Snapshot(ulong session, uint tick, IEnumerable<PawnState> pawns)
        {
            var list = new List<PawnState>(pawns);
            using (var stream = new MemoryStream()) using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic); w.Write((byte)2); w.Write(session); w.Write(tick); w.Write((byte)list.Count);
                foreach (var p in list) { w.Write(p.Id); w.Write(p.Ack); w.Write(p.X); w.Write(p.Y); w.Write(p.Z); w.Write(p.Vertical); w.Write((byte)p.Team); w.Write((byte)p.Slot); }
                return stream.ToArray();
            }
        }
        public static bool ReadSnapshot(byte[] bytes, ulong session, out uint tick, out List<PawnState> pawns)
        {
            tick = 0; pawns = new List<PawnState>();
            if (bytes == null || bytes.Length < 18 || bytes.Length > MaxBytes) return false;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (r.ReadUInt32() != Magic || r.ReadByte() != 2 || r.ReadUInt64() != session) return false;
                tick = r.ReadUInt32(); int count = r.ReadByte();
                if (count > 12 || bytes.Length != 18 + count * 30) return false;
                var ids = new HashSet<ulong>();
                for (int i = 0; i < count; i++)
                {
                    var p = new PawnState { Id = r.ReadUInt64(), Ack = r.ReadUInt32(), X = r.ReadSingle(), Y = r.ReadSingle(), Z = r.ReadSingle(), Vertical = r.ReadSingle(), Team = r.ReadByte(), Slot = r.ReadByte() };
                    if (p.Id == 0 || !ids.Add(p.Id) || p.Team > 1 || p.Slot > 5 || !Finite(p.X) || !Finite(p.Y) || !Finite(p.Z) || !Finite(p.Vertical) || Math.Abs(p.X) > 20 || Math.Abs(p.Z) > 20 || p.Y < 0 || p.Y > 10) return false;
                    pawns.Add(p);
                }
                return true;
            }
        }
    }
}
