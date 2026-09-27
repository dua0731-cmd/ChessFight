using System;
using System.Collections.Generic;
using System.IO;

namespace ChessFight.Network
{
    public struct SwordFighterState
    {
        public ulong Id;
        public bool Alive;
        public float Age, Protection, Respawn, Yaw;
        public uint Swing;
    }
    public sealed class SwordFightState
    {
        public uint Tick;
        public int White, Black;
        public float Remaining;
        public bool Finished;
        public readonly List<SwordFighterState> Fighters = new List<SwordFighterState>();
    }
    // Independent mode state on channel 33. The existing CFR4 pose/input layout is unchanged.
    public static class SwordFightProtocol
    {
        public const uint Magic = 0x43465331; // CFS1
        public const int HeaderBytes = 26, FighterBytes = 20, MaxBytes = HeaderBytes + 12 * FighterBytes;
        static byte Q(float value, float scale) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value * scale)));
        public static byte[] Write(ulong match, SwordFightState state)
        {
            if (state.Fighters.Count > 12) throw new ArgumentOutOfRangeException(nameof(state));
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic); w.Write(match); w.Write(state.Tick);
                w.Write((ushort)state.White); w.Write((ushort)state.Black); w.Write(state.Remaining);
                w.Write(state.Finished); w.Write((byte)state.Fighters.Count);
                foreach (var f in state.Fighters)
                {
                    w.Write(f.Id); w.Write(f.Alive); w.Write(Q(f.Age, 100)); w.Write(Q(f.Protection, 100)); w.Write(Q(f.Respawn, 20));
                    w.Write(f.Swing); w.Write(f.Yaw);
                }
                return stream.ToArray();
            }
        }
        public static bool Read(byte[] bytes, ulong match, out SwordFightState state)
        {
            state = null;
            if (bytes == null || bytes.Length < HeaderBytes || bytes.Length > MaxBytes) return false;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (r.ReadUInt32() != Magic || r.ReadUInt64() != match) return false;
                var s = new SwordFightState { Tick = r.ReadUInt32(), White = r.ReadUInt16(), Black = r.ReadUInt16(), Remaining = r.ReadSingle() };
                byte finished = r.ReadByte(); int count = r.ReadByte();
                if (finished > 1 || count > 12 || bytes.Length != HeaderBytes + count * FighterBytes || !Finite(s.Remaining) || s.Remaining < 0 || s.Remaining > 3600) return false;
                s.Finished = finished == 1;
                var ids = new HashSet<ulong>();
                for (int i = 0; i < count; i++)
                {
                    ulong id = r.ReadUInt64(); byte alive = r.ReadByte();
                    var f = new SwordFighterState { Id = id, Alive = alive == 1, Age = r.ReadByte() / 100f, Protection = r.ReadByte() / 100f, Respawn = r.ReadByte() / 20f, Swing = r.ReadUInt32(), Yaw = r.ReadSingle() };
                    if (id == 0 || !ids.Add(id) || alive > 1 || !Finite(f.Yaw) || Math.Abs(f.Yaw) > 360) return false;
                    s.Fighters.Add(f);
                }
                state = s; return true;
            }
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
