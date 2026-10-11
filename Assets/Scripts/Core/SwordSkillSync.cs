using System;
using System.Collections.Generic;
using System.IO;

namespace ChessFight.Network
{
    // The Sword Fight skill test online (R121, Docs/Network/SKILLS_ONLINE.md §15). The wire side only, with no Unity,
    // like SkillSync: what the host tells each client about the fighters (their teams, lives and swords, and the F and E
    // skills as far as the effects need them) and about the floor the rook breaks. The skill moments themselves go in the
    // skills packet (SkillWire, ModeSwordFight), numbered and repeated until confirmed like the other two modes'.

    /// <summary>One fighter as the clients draw it. The skill parts are only on the wire while they say something
    /// (<see cref="Skill"/>, <see cref="Edge"/>, <see cref="Glow"/>), like the other modes' idle pieces (D-S5).</summary>
    public struct SwordWireFighter
    {
        public byte Ref;                    // the roster place (SkillWire.Ref)
        public byte Team;                   // 0 white, 1 black: the host's draw (R121), not the roster's
        public bool Alive, Drawn, Classic;
        public float Protection, Respawn, Flash;   // s
        // The sword against the hips: its grip in the hips' space (m) and its turn (RagdollNetProtocol.PackRotation).
        public float Wx, Wy, Wz;
        public uint WRot;
        // The edge skill's condition, for its owner's key card and glow: true, or why not (a code the Unity side reads).
        public bool EdgeCondition;
        public byte Why;
        public byte EdgeTarget;             // the queen's and the knight's enemy, the bishop's falling ally (SkillWire.None)

        // ---- the F skill (Skill)
        public bool Skill;
        public byte Stage, Detail, SkillFlags;   // SfStage; a detail code; 1 the rook's wave stopped, 2 the king answered
        public float StageTime;             // s, 0..655
        public float Cooldown, CooldownTotal;   // s, 0..25.5
        public float Dx, Dy, Dz;            // the aim or the skill's way (unit)
        public float Px, Py, Pz;            // the aimed point or the skill's point
        public float Ox, Oy, Oz;            // where it is aimed from (the floor under the hips)
        public float Reach, Wave, Dash;     // m, 0..12.75: its line's reach, the rook's wave front, the queen's run
        public byte PinCandidate;           // the bishop's enemy on the point
        public float Slow, Pin;             // s, 0..25.5: what a bishop has done to this piece

        // ---- the E skill while it runs or cools down (Edge)
        public bool Edge;
        public byte EdgeStage, EdgeDetail, EdgeFlags;   // SfEdge; a detail code; 1 the bishop holds the ally, 2 the king has vaulted
        public float EdgeTime;              // s, 0..655
        public float EdgeCooldown, EdgeCooldownTotal;   // s, 0..25.5
        public byte Carried;                // the bishop's ally in its hands
        public float Ax, Ay, Az;            // EdgeAt
        public float Outx, Outy, Outz;      // EdgeOut (unit)
        public float Lx, Ly, Lz;            // EdgeLand
        public float Sx, Sy, Sz;            // EdgeStart

        // ---- the edge it would use or uses (Glow): a point on the lip, the way out, along its side, where on it
        public bool Glow;
        public float Gx, Gy, Gz;
        public float GOx, GOy, GOz;
        public float GAx, GAy, GAz;
        public float GAt, GTo;              // m from the side's start, 0..655
    }

    /// <summary>A chunk of floor a rook broke off (SwordFightEdgeFloor): a client runs the same collapse from the same
    /// start, so it cracks, falls and comes back when the host's does.</summary>
    public struct SwordWireCollapse
    {
        public byte Id;                     // the host's number for it
        public byte By;                     // the rook's roster place
        public float X0, Z0, X1, Z1;        // the chunk, x / z on the floor
        public float Ox, Oy, Oz;            // the way out of the edge it breaks off (unit)
        public float Ax, Ay, Az;            // along that edge (unit)
        public float Lx, Ly, Lz;            // the middle of its outer edge, on the lip
        public float Age, Crack, Open;      // s: since it started (0..655), the crack's and the open hole's lengths (0..25.5)
    }

    public sealed class SwordWirePacket
    {
        /// <summary>The time of the snapshot it goes with: a client keeps the swords with those poses.</summary>
        public uint HostTimeMs;
        public int White, Black;
        public float Remaining;             // s, 0..6553
        public bool Finished;
        public readonly List<SwordWireFighter> Fighters = new List<SwordWireFighter>();
        public readonly List<SwordWireCollapse> Collapses = new List<SwordWireCollapse>();

        public void Clear()
        {
            HostTimeMs = 0;
            White = Black = 0;
            Remaining = 0f;
            Finished = false;
            Fighters.Clear();
            Collapses.Clear();
        }
    }

    /// <summary>
    /// The Sword Fight state packet's body, host to each client with every snapshot (30 Hz, unreliable: a newer one
    /// replaces a lost one). Every fighter is in it with its life and sword (18 bytes); its skill parts only while they
    /// say something; the floor's collapses while they last. Quantised like SkillWire: times to 1/100 s or 1/10 s,
    /// points to 2.4 mm, directions to a byte per axis, the sword's grip to 0.13 mm.
    /// </summary>
    public static class SwordWire
    {
        public const int HeaderBytes = 4 + 2 + 2 + 2 + 1 + 1 + 1;   // host time, scores, time left, finished, two counts
        public const int BaseBytes = 1 + 1 + 1 + 1 + 1 + 6 + 4 + 1 + 1;   // 17
        public const int SkillBytes = 1 + 1 + 1 + 2 + 1 + 1 + 3 + 6 + 6 + 1 + 1 + 1 + 1 + 1 + 1;   // 28
        public const int EdgeBytes = 1 + 1 + 1 + 2 + 1 + 1 + 1 + 6 + 3 + 6 + 6;   // 29
        public const int GlowBytes = 6 + 3 + 3 + 2 + 2;   // 16
        public const int CollapseBytes = 1 + 1 + 8 + 3 + 3 + 6 + 2 + 1 + 1;   // 26
        public const int MaxFighters = 12, MaxCollapses = 6;
        public const float GripRange = 4.096f;
        const byte FAlive = 1, FDrawn = 2, FClassic = 4, FBlack = 8, FSkill = 16, FEdge = 32, FGlow = 64, FCondition = 128;

        public static int FighterSize(in SwordWireFighter f) =>
            BaseBytes + (f.Skill ? SkillBytes : 0) + (f.Edge ? EdgeBytes : 0) + (f.Glow ? GlowBytes : 0);

        public static int Size(SwordWirePacket p)
        {
            int n = HeaderBytes + Math.Min(p.Collapses.Count, MaxCollapses) * CollapseBytes;
            for (int i = 0; i < Math.Min(p.Fighters.Count, MaxFighters); i++) n += FighterSize(p.Fighters[i]);
            return n;
        }

        /// <summary>Within <paramref name="budget"/> bytes: a fighter that does not fit with its skill parts goes with
        /// fewer (its glow first, then its E skill, then its F skill); its life and sword always go.</summary>
        public static void Fit(SwordWirePacket p, int budget)
        {
            while (p.Fighters.Count > MaxFighters) p.Fighters.RemoveAt(p.Fighters.Count - 1);
            while (p.Collapses.Count > MaxCollapses) p.Collapses.RemoveAt(p.Collapses.Count - 1);
            for (int i = p.Fighters.Count - 1; i >= 0 && Size(p) > budget; i--)
            {
                var f = p.Fighters[i];
                f.Glow = false;
                if (Size(p) - FighterSize(p.Fighters[i]) + FighterSize(f) > budget) f.Edge = false;
                if (Size(p) - FighterSize(p.Fighters[i]) + FighterSize(f) > budget) f.Skill = false;
                p.Fighters[i] = f;
            }
        }

        // ---------------------------------------------------------------- write

        public static void Write(BinaryWriter w, SwordWirePacket p)
        {
            int fighters = Math.Min(p.Fighters.Count, MaxFighters), collapses = Math.Min(p.Collapses.Count, MaxCollapses);
            w.Write(p.HostTimeMs);
            w.Write((ushort)Math.Max(0, Math.Min(ushort.MaxValue, p.White)));
            w.Write((ushort)Math.Max(0, Math.Min(ushort.MaxValue, p.Black)));
            w.Write(Tenths(p.Remaining));
            w.Write((byte)(p.Finished ? 1 : 0));
            w.Write((byte)fighters);
            w.Write((byte)collapses);
            for (int i = 0; i < fighters; i++)
            {
                var f = p.Fighters[i];
                w.Write(f.Ref);
                w.Write((byte)((f.Alive ? FAlive : 0) | (f.Drawn ? FDrawn : 0) | (f.Classic ? FClassic : 0) | (f.Team == 1 ? FBlack : 0)
                               | (f.Skill ? FSkill : 0) | (f.Edge ? FEdge : 0) | (f.Glow ? FGlow : 0) | (f.EdgeCondition ? FCondition : 0)));
                w.Write(Byte(f.Protection, 100f));
                w.Write(Byte(f.Respawn, 20f));
                w.Write(Byte(f.Flash, 100f));
                w.Write(Grip(f.Wx));
                w.Write(Grip(f.Wy));
                w.Write(Grip(f.Wz));
                w.Write(f.WRot);
                w.Write(f.Why);
                w.Write(f.EdgeTarget);
                if (f.Skill)
                {
                    w.Write(f.Stage);
                    w.Write(f.Detail);
                    w.Write(f.SkillFlags);
                    w.Write(Centi(f.StageTime));
                    w.Write(Byte(f.Cooldown, 10f));
                    w.Write(Byte(f.CooldownTotal, 10f));
                    WriteDir(w, f.Dx, f.Dy, f.Dz);
                    WritePoint(w, f.Px, f.Py, f.Pz);
                    WritePoint(w, f.Ox, f.Oy, f.Oz);
                    w.Write(Byte(f.Reach, 20f));
                    w.Write(Byte(f.Wave, 20f));
                    w.Write(Byte(f.Dash, 20f));
                    w.Write(f.PinCandidate);
                    w.Write(Byte(f.Slow, 10f));
                    w.Write(Byte(f.Pin, 10f));
                }
                if (f.Edge)
                {
                    w.Write(f.EdgeStage);
                    w.Write(f.EdgeDetail);
                    w.Write(f.EdgeFlags);
                    w.Write(Centi(f.EdgeTime));
                    w.Write(Byte(f.EdgeCooldown, 10f));
                    w.Write(Byte(f.EdgeCooldownTotal, 10f));
                    w.Write(f.Carried);
                    WritePoint(w, f.Ax, f.Ay, f.Az);
                    WriteDir(w, f.Outx, f.Outy, f.Outz);
                    WritePoint(w, f.Lx, f.Ly, f.Lz);
                    WritePoint(w, f.Sx, f.Sy, f.Sz);
                }
                if (f.Glow)
                {
                    WritePoint(w, f.Gx, f.Gy, f.Gz);
                    WriteDir(w, f.GOx, f.GOy, f.GOz);
                    WriteDir(w, f.GAx, f.GAy, f.GAz);
                    w.Write(Centi(f.GAt));
                    w.Write(Centi(f.GTo));
                }
            }
            for (int i = 0; i < collapses; i++)
            {
                var c = p.Collapses[i];
                w.Write(c.Id);
                w.Write(c.By);
                w.Write(Metres(c.X0));
                w.Write(Metres(c.Z0));
                w.Write(Metres(c.X1));
                w.Write(Metres(c.Z1));
                WriteDir(w, c.Ox, c.Oy, c.Oz);
                WriteDir(w, c.Ax, c.Ay, c.Az);
                WritePoint(w, c.Lx, c.Ly, c.Lz);
                w.Write(Centi(c.Age));
                w.Write(Byte(c.Crack, 10f));
                w.Write(Byte(c.Open, 10f));
            }
        }

        // ---------------------------------------------------------------- read

        /// <summary>False for anything malformed (a bad count, a length that does not add up, a value out of range).</summary>
        public static bool Read(BinaryReader r, int bodyLength, SwordWirePacket p)
        {
            p.Clear();
            if (bodyLength < HeaderBytes) return false;
            p.HostTimeMs = r.ReadUInt32();
            p.White = r.ReadUInt16();
            p.Black = r.ReadUInt16();
            p.Remaining = r.ReadUInt16() / 10f;
            byte finished = r.ReadByte();
            int fighters = r.ReadByte(), collapses = r.ReadByte();
            if (finished > 1 || fighters > MaxFighters || collapses > MaxCollapses) return false;
            p.Finished = finished == 1;
            int length = HeaderBytes + collapses * CollapseBytes;
            var refs = new HashSet<byte>();
            for (int i = 0; i < fighters; i++)
            {
                if (length + BaseBytes > bodyLength) return false;
                var f = new SwordWireFighter();
                f.Ref = r.ReadByte();
                byte flags = r.ReadByte();
                f.Alive = (flags & FAlive) != 0;
                f.Drawn = (flags & FDrawn) != 0;
                f.Classic = (flags & FClassic) != 0;
                f.Team = (byte)((flags & FBlack) != 0 ? 1 : 0);
                f.Skill = (flags & FSkill) != 0;
                f.Edge = (flags & FEdge) != 0;
                f.Glow = (flags & FGlow) != 0;
                f.EdgeCondition = (flags & FCondition) != 0;
                f.Protection = r.ReadByte() / 100f;
                f.Respawn = r.ReadByte() / 20f;
                f.Flash = r.ReadByte() / 100f;
                f.Wx = Grip(r.ReadInt16());
                f.Wy = Grip(r.ReadInt16());
                f.Wz = Grip(r.ReadInt16());
                f.WRot = r.ReadUInt32();
                f.Why = r.ReadByte();
                f.EdgeTarget = r.ReadByte();
                if (f.Ref == SkillWire.None || !refs.Add(f.Ref) || (f.Drawn && !f.Alive)) return false;
                length += FighterSize(f);
                if (length > bodyLength) return false;
                if (f.Skill)
                {
                    f.Stage = r.ReadByte();
                    f.Detail = r.ReadByte();
                    f.SkillFlags = r.ReadByte();
                    f.StageTime = r.ReadUInt16() / 100f;
                    f.Cooldown = r.ReadByte() / 10f;
                    f.CooldownTotal = r.ReadByte() / 10f;
                    ReadDir(r, out f.Dx, out f.Dy, out f.Dz);
                    ReadPoint(r, out f.Px, out f.Py, out f.Pz);
                    ReadPoint(r, out f.Ox, out f.Oy, out f.Oz);
                    f.Reach = r.ReadByte() / 20f;
                    f.Wave = r.ReadByte() / 20f;
                    f.Dash = r.ReadByte() / 20f;
                    f.PinCandidate = r.ReadByte();
                    f.Slow = r.ReadByte() / 10f;
                    f.Pin = r.ReadByte() / 10f;
                    if (f.Stage > 4) return false;
                }
                if (f.Edge)
                {
                    f.EdgeStage = r.ReadByte();
                    f.EdgeDetail = r.ReadByte();
                    f.EdgeFlags = r.ReadByte();
                    f.EdgeTime = r.ReadUInt16() / 100f;
                    f.EdgeCooldown = r.ReadByte() / 10f;
                    f.EdgeCooldownTotal = r.ReadByte() / 10f;
                    f.Carried = r.ReadByte();
                    ReadPoint(r, out f.Ax, out f.Ay, out f.Az);
                    ReadDir(r, out f.Outx, out f.Outy, out f.Outz);
                    ReadPoint(r, out f.Lx, out f.Ly, out f.Lz);
                    ReadPoint(r, out f.Sx, out f.Sy, out f.Sz);
                    if (f.EdgeStage > 3 || f.EdgeFlags > 3) return false;
                }
                if (f.Glow)
                {
                    ReadPoint(r, out f.Gx, out f.Gy, out f.Gz);
                    ReadDir(r, out f.GOx, out f.GOy, out f.GOz);
                    ReadDir(r, out f.GAx, out f.GAy, out f.GAz);
                    f.GAt = r.ReadUInt16() / 100f;
                    f.GTo = r.ReadUInt16() / 100f;
                }
                p.Fighters.Add(f);
            }
            if (length != bodyLength) return false;
            for (int i = 0; i < collapses; i++)
            {
                var c = new SwordWireCollapse();
                c.Id = r.ReadByte();
                c.By = r.ReadByte();
                c.X0 = Unmetres(r.ReadInt16());
                c.Z0 = Unmetres(r.ReadInt16());
                c.X1 = Unmetres(r.ReadInt16());
                c.Z1 = Unmetres(r.ReadInt16());
                ReadDir(r, out c.Ox, out c.Oy, out c.Oz);
                ReadDir(r, out c.Ax, out c.Ay, out c.Az);
                ReadPoint(r, out c.Lx, out c.Ly, out c.Lz);
                c.Age = r.ReadUInt16() / 100f;
                c.Crack = r.ReadByte() / 10f;
                c.Open = r.ReadByte() / 10f;
                if (c.X1 < c.X0 || c.Z1 < c.Z0) return false;
                p.Collapses.Add(c);
            }
            return true;
        }

        // ---------------------------------------------------------------- quantisation

        static byte Byte(float value, float scale)
        {
            if (float.IsNaN(value)) return 0;
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value * scale)));
        }

        static ushort Centi(float seconds)
        {
            if (float.IsNaN(seconds)) return 0;
            return (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (int)Math.Round(seconds * 100f)));
        }

        static ushort Tenths(float seconds)
        {
            if (float.IsNaN(seconds)) return 0;
            return (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (int)Math.Round(seconds * 10f)));
        }

        static short Grip(float metres)
        {
            if (float.IsNaN(metres)) return 0;
            return (short)Math.Max(short.MinValue + 1, Math.Min(short.MaxValue, (int)Math.Round(metres / GripRange * short.MaxValue)));
        }

        static float Grip(short value) => value / (float)short.MaxValue * GripRange;

        static short Metres(float value)
        {
            if (float.IsNaN(value)) return 0;
            int q = (int)Math.Round(value / SkillWire.PositionRange * short.MaxValue);
            return (short)Math.Max(short.MinValue + 1, Math.Min(short.MaxValue, q));
        }

        static float Unmetres(short value) => value / (float)short.MaxValue * SkillWire.PositionRange;

        static void WritePoint(BinaryWriter w, float x, float y, float z)
        {
            w.Write(Metres(x));
            w.Write(Metres(y));
            w.Write(Metres(z));
        }

        static void ReadPoint(BinaryReader r, out float x, out float y, out float z)
        {
            x = Unmetres(r.ReadInt16());
            y = Unmetres(r.ReadInt16());
            z = Unmetres(r.ReadInt16());
        }

        static sbyte Signed(float value)
        {
            if (float.IsNaN(value)) return 0;
            return (sbyte)Math.Max(-127, Math.Min(127, (int)Math.Round(value * 127f)));
        }

        static void WriteDir(BinaryWriter w, float x, float y, float z)
        {
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            if (length > 1e-6f && !float.IsNaN(length)) { x /= length; y /= length; z /= length; }
            else x = y = z = 0f;
            w.Write(Signed(x));
            w.Write(Signed(y));
            w.Write(Signed(z));
        }

        static void ReadDir(BinaryReader r, out float x, out float y, out float z)
        {
            x = r.ReadSByte() / 127f;
            y = r.ReadSByte() / 127f;
            z = r.ReadSByte() / 127f;
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            if (length < 0.5f) { x = y = z = 0f; return; }
            x /= length; y /= length; z /= length;
        }
    }
}
