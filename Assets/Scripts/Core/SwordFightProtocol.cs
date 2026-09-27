using System;
using System.Collections.Generic;
using System.IO;

namespace ChessFight.Network
{
    public struct SwordFighterState
    {
        public ulong Id;
        public bool Alive, Drawn;
        public float Protection, Respawn, Flash;
        // Weapon pose relative to the hips; Core has no Unity dependency.
        public float X, Y, Z, RX, RY, RZ, RW;
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
        public const uint Magic = 0x43465332; // CFS2: physical sword pose and held guard
        public const int HeaderBytes = 26, FighterBytes = 40, MaxBytes = HeaderBytes + 12 * FighterBytes;
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
                    w.Write(f.Id); w.Write((byte)((f.Alive ? 1 : 0) | (f.Drawn ? 2 : 0)));
                    w.Write(Q(f.Protection, 100)); w.Write(Q(f.Respawn, 20)); w.Write(Q(f.Flash, 100));
                    w.Write(f.X); w.Write(f.Y); w.Write(f.Z);
                    w.Write(f.RX); w.Write(f.RY); w.Write(f.RZ); w.Write(f.RW);
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
                    ulong id = r.ReadUInt64(); byte flags = r.ReadByte();
                    var f = new SwordFighterState { Id = id, Alive = (flags & 1) != 0, Drawn = (flags & 2) != 0,
                        Protection = r.ReadByte() / 100f, Respawn = r.ReadByte() / 20f, Flash = r.ReadByte() / 100f,
                        X = r.ReadSingle(), Y = r.ReadSingle(), Z = r.ReadSingle(),
                        RX = r.ReadSingle(), RY = r.ReadSingle(), RZ = r.ReadSingle(), RW = r.ReadSingle() };
                    float norm = f.RX * f.RX + f.RY * f.RY + f.RZ * f.RZ + f.RW * f.RW;
                    if (id == 0 || !ids.Add(id) || flags > 3 || (f.Drawn && !f.Alive) ||
                        !Bound(f.X, 4) || !Bound(f.Y, 4) || !Bound(f.Z, 4) ||
                        !Finite(norm) || norm < .9f || norm > 1.1f) return false;
                    s.Fighters.Add(f);
                }
                state = s; return true;
            }
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Bound(float value, float bound) => Finite(value) && Math.Abs(value) <= bound;
    }
}
