using System;
using System.Collections.Generic;
using System.IO;

namespace ChessFight.Network
{
    // The piece skills online (Docs/Network/SKILLS_ONLINE.md, R111). Everything here is the
    // wire side only, with no Unity, so the rules can be run thousands of times through
    // LinkSimulator in Tests/Network: what a press, a skill's state and a skill's moment
    // look like on the wire, how a lost packet is survived, and how big a packet gets.

    /// <summary>
    /// A wrapping count of presses (D-S7, like MotionProtocol's jumps). The client counts every
    /// press and every input packet carries the count, so a lost packet only delays a press by
    /// one packet (1/60 s) instead of dropping it.
    /// </summary>
    public static class PressCount
    {
        public static byte Add(byte count) { return unchecked((byte)(count + 1)); }

        /// <summary>New presses since <paramref name="consumed"/> (0 = none), which then catches up.</summary>
        public static int Take(byte count, ref byte consumed)
        {
            int n = unchecked((byte)(count - consumed));
            consumed = count;
            return n;
        }
    }

    /// <summary>One piece's skill as the others need to draw it (its warnings, cooldown, aim). The
    /// slots mean different things per mode (RagdollPawn.SkillNet maps them); on the wire they are
    /// quantised: times to 1/100 s or 1/10 s, points to 2.4 mm, directions to a byte per axis.</summary>
    public struct SkillWireState
    {
        public byte Pawn;                  // the piece: its roster place (SkillWire.Ref)
        public byte Stage, Flags, Flags2;
        public float StageTime;            // s, 0..655
        public float Cooldown, CooldownTotal;   // s, 0..25.5
        public float Dx, Dy, Dz;           // a unit direction (the aim, the charge)
        public float Px, Py, Pz;           // a point (an aimed spot, a landing)
        public float Qx, Qy, Qz;           // a second point (where a slash starts, a leap's spot)
        public byte Target, Other;         // pieces it points at (roster places, SkillWire.None)
        public float A, B;                 // timers of the mode, s, 0..25.5
        public byte C, D;                  // small values of the mode, 0..255
    }

    /// <summary>A skill's moment (a hit, a blast, a landing) for the effects on every screen
    /// (D-S1). Numbered, so it is drawn once however many packets carry it.</summary>
    public struct SkillWireEvent
    {
        public ushort Seq;
        public uint TimeMs;                // host clock when it happened
        public byte Mode;                  // SkillWire.ModePawnRush, ModeQueenHill
        public byte Kind;
        public byte By, Target;            // roster places, SkillWire.None
        public float X, Y, Z;              // where
        public float Dx, Dy, Dz;           // which way (unit)
        public float Ux, Uy, Uz;           // a second point or a face's normal
        public byte Count;
        public float Size;                 // m or s, 0..655
        public ushort Source;              // the thing it belongs to (a wire's id), 0 = none
    }

    /// <summary>A bishop's crossed tripwire, the whole of it, in every packet while it lasts: a
    /// player who joins late sees it at once (C11), a new host takes it over (D-S6).</summary>
    public struct SkillWireObject
    {
        public ushort Id;
        public byte Owner;                 // roster place of the bishop
        public float X, Y, Z;              // the middle of its X
        public float Yaw;                  // degrees, which way its X lies
        public float Age;                  // s since it was laid
        public byte Flags;                 // 1 armed, 2 spent
        public byte Trips;
        public float Along0, Along1;       // where along each line it is pulled out, 0..1
        public float Out0, Out1;           // how far each line is pulled out, m (signed, across the line)
    }

    /// <summary>A wooden barricade the rook breaks: standing, or how long until it stands again.</summary>
    public struct SkillWireBarricade
    {
        public byte Index;
        public bool Standing;
        public float RegrowLeft;           // s, 0..25.5
    }

    /// <summary>Everything about the skills a host sends a client in one packet.</summary>
    public sealed class SkillWirePacket
    {
        public uint HostTimeMs;
        /// <summary>The oldest moment the host still keeps: a client that has not got the ones before
        /// it never will, and stops waiting for them.</summary>
        public ushort FirstSeq;
        public readonly List<SkillWireState> States = new List<SkillWireState>();
        public readonly List<SkillWireObject> Wires = new List<SkillWireObject>();
        public readonly List<SkillWireBarricade> Barricades = new List<SkillWireBarricade>();
        public readonly List<SkillWireEvent> Events = new List<SkillWireEvent>();

        public void Clear()
        {
            HostTimeMs = 0;
            FirstSeq = 0;
            States.Clear();
            Wires.Clear();
            Barricades.Clear();
            Events.Clear();
        }
    }

    /// <summary>
    /// The skills packet's body. The host fills it each snapshot with every piece that is not idle
    /// (D-S5: an idle one is not in it, so a full twelve-piece snapshot stays as it was), every
    /// wire and barricade, and as many unconfirmed moments as fit in MaxBytes.
    /// </summary>
    public static class SkillWire
    {
        public const byte ModePawnRush = 0, ModeQueenHill = 1;
        public const byte None = 255;
        public const int HeaderBytes = 4 + 2 + 1 + 1 + 1 + 1;   // host time, first seq, four counts
        public const int StateBytes = 1 + 1 + 1 + 1 + 2 + 1 + 1 + 3 + 6 + 6 + 1 + 1 + 1 + 1 + 1 + 1;   // 29
        public const int WireBytes = 2 + 1 + 6 + 1 + 2 + 1 + 1 + 1 + 1 + 1 + 1;   // 18
        public const int BarricadeBytes = 1 + 1 + 1;
        public const int EventBytes = 2 + 2 + 1 + 1 + 1 + 1 + 6 + 3 + 6 + 1 + 2 + 2;   // 28
        public const int MaxStates = 16, MaxWires = 16, MaxBarricades = 8;
        /// <summary>How long ago a moment may have happened and still be sent (ms).</summary>
        public const uint EventLifeMs = 1500;
        public const float PositionRange = 80f;

        /// <summary>A piece by its roster place: team and slot are unique in a match and known on every
        /// PC, so a piece costs one byte instead of its eight-byte Steam id.</summary>
        public static byte Ref(int team, int slot)
        {
            if (team < 0 || team > 1 || slot < 0 || slot > 7) return None;
            return (byte)(team * 8 + slot);
        }

        public static void Place(byte reference, out int team, out int slot)
        {
            team = reference == None ? -1 : reference / 8;
            slot = reference == None ? -1 : reference % 8;
        }

        public static bool Newer(ushort value, ushort previous) { return unchecked((short)(value - previous)) > 0; }

        public static int Size(int states, int wires, int barricades, int events)
        {
            return HeaderBytes + states * StateBytes + wires * WireBytes + barricades * BarricadeBytes + events * EventBytes;
        }

        /// <summary>How many of <paramref name="events"/> moments fit after the rest, within <paramref name="budget"/> bytes.</summary>
        public static int EventsThatFit(int budget, int states, int wires, int barricades, int events)
        {
            int left = budget - Size(states, wires, barricades, 0);
            return Math.Max(0, Math.Min(events, left / EventBytes));
        }

        // ---------------------------------------------------------------- write

        public static void Write(BinaryWriter w, SkillWirePacket p)
        {
            int states = Math.Min(p.States.Count, MaxStates), wires = Math.Min(p.Wires.Count, MaxWires);
            int barricades = Math.Min(p.Barricades.Count, MaxBarricades), events = Math.Min(p.Events.Count, 255);
            w.Write(p.HostTimeMs);
            w.Write(p.FirstSeq);
            w.Write((byte)states);
            w.Write((byte)wires);
            w.Write((byte)barricades);
            w.Write((byte)events);
            for (int i = 0; i < states; i++)
            {
                var s = p.States[i];
                w.Write(s.Pawn);
                w.Write(s.Stage);
                w.Write(s.Flags);
                w.Write(s.Flags2);
                w.Write(Centi(s.StageTime));
                w.Write(Tenth(s.Cooldown));
                w.Write(Tenth(s.CooldownTotal));
                WriteDir(w, s.Dx, s.Dy, s.Dz);
                WritePoint(w, s.Px, s.Py, s.Pz);
                WritePoint(w, s.Qx, s.Qy, s.Qz);
                w.Write(s.Target);
                w.Write(s.Other);
                w.Write(Tenth(s.A));
                w.Write(Tenth(s.B));
                w.Write(s.C);
                w.Write(s.D);
            }
            for (int i = 0; i < wires; i++)
            {
                var o = p.Wires[i];
                w.Write(o.Id);
                w.Write(o.Owner);
                WritePoint(w, o.X, o.Y, o.Z);
                w.Write((byte)(Mod(o.Yaw, 360f) / 360f * 256f) );
                w.Write(Centi(o.Age));
                w.Write(o.Flags);
                w.Write(o.Trips);
                w.Write(Unit(o.Along0));
                w.Write(Unit(o.Along1));
                w.Write(Signed(o.Out0 / 1.27f));
                w.Write(Signed(o.Out1 / 1.27f));
            }
            for (int i = 0; i < barricades; i++)
            {
                var b = p.Barricades[i];
                w.Write(b.Index);
                w.Write((byte)(b.Standing ? 1 : 0));
                w.Write(Tenth(b.RegrowLeft));
            }
            for (int i = 0; i < events; i++)
            {
                var e = p.Events[i];
                w.Write(e.Seq);
                uint back = p.HostTimeMs >= e.TimeMs ? p.HostTimeMs - e.TimeMs : 0;
                w.Write((ushort)Math.Min(back, ushort.MaxValue));
                w.Write(e.Mode);
                w.Write(e.Kind);
                w.Write(e.By);
                w.Write(e.Target);
                WritePoint(w, e.X, e.Y, e.Z);
                WriteDir(w, e.Dx, e.Dy, e.Dz);
                WritePoint(w, e.Ux, e.Uy, e.Uz);
                w.Write(e.Count);
                w.Write(Centi(e.Size));
                w.Write(e.Source);
            }
        }

        // ---------------------------------------------------------------- read

        /// <summary>False (and <paramref name="p"/> half filled) for anything malformed: a bad count, a
        /// length that does not add up, a value out of range.</summary>
        public static bool Read(BinaryReader r, int bodyLength, SkillWirePacket p)
        {
            p.Clear();
            if (bodyLength < HeaderBytes) return false;
            p.HostTimeMs = r.ReadUInt32();
            p.FirstSeq = r.ReadUInt16();
            int states = r.ReadByte(), wires = r.ReadByte(), barricades = r.ReadByte(), events = r.ReadByte();
            if (states > MaxStates || wires > MaxWires || barricades > MaxBarricades) return false;
            if (bodyLength != Size(states, wires, barricades, events)) return false;
            for (int i = 0; i < states; i++)
            {
                var s = new SkillWireState();
                s.Pawn = r.ReadByte();
                s.Stage = r.ReadByte();
                s.Flags = r.ReadByte();
                s.Flags2 = r.ReadByte();
                s.StageTime = r.ReadUInt16() / 100f;
                s.Cooldown = r.ReadByte() / 10f;
                s.CooldownTotal = r.ReadByte() / 10f;
                ReadDir(r, out s.Dx, out s.Dy, out s.Dz);
                ReadPoint(r, out s.Px, out s.Py, out s.Pz);
                ReadPoint(r, out s.Qx, out s.Qy, out s.Qz);
                s.Target = r.ReadByte();
                s.Other = r.ReadByte();
                s.A = r.ReadByte() / 10f;
                s.B = r.ReadByte() / 10f;
                s.C = r.ReadByte();
                s.D = r.ReadByte();
                if (s.Pawn == None || s.Stage > 4) return false;
                p.States.Add(s);
            }
            for (int i = 0; i < wires; i++)
            {
                var o = new SkillWireObject();
                o.Id = r.ReadUInt16();
                o.Owner = r.ReadByte();
                ReadPoint(r, out o.X, out o.Y, out o.Z);
                o.Yaw = r.ReadByte() / 256f * 360f;
                o.Age = r.ReadUInt16() / 100f;
                o.Flags = r.ReadByte();
                o.Trips = r.ReadByte();
                o.Along0 = r.ReadByte() / 255f;
                o.Along1 = r.ReadByte() / 255f;
                o.Out0 = r.ReadSByte() / 127f * 1.27f;
                o.Out1 = r.ReadSByte() / 127f * 1.27f;
                if (o.Id == 0) return false;
                p.Wires.Add(o);
            }
            for (int i = 0; i < barricades; i++)
            {
                var b = new SkillWireBarricade();
                b.Index = r.ReadByte();
                byte flags = r.ReadByte();
                if (flags > 1) return false;
                b.Standing = flags == 1;
                b.RegrowLeft = r.ReadByte() / 10f;
                p.Barricades.Add(b);
            }
            for (int i = 0; i < events; i++)
            {
                var e = new SkillWireEvent();
                e.Seq = r.ReadUInt16();
                uint back = r.ReadUInt16();
                e.TimeMs = p.HostTimeMs - back;
                e.Mode = r.ReadByte();
                e.Kind = r.ReadByte();
                e.By = r.ReadByte();
                e.Target = r.ReadByte();
                ReadPoint(r, out e.X, out e.Y, out e.Z);
                ReadDir(r, out e.Dx, out e.Dy, out e.Dz);
                ReadPoint(r, out e.Ux, out e.Uy, out e.Uz);
                e.Count = r.ReadByte();
                e.Size = r.ReadUInt16() / 100f;
                e.Source = r.ReadUInt16();
                if (e.Mode > ModeQueenHill) return false;
                p.Events.Add(e);
            }
            return true;
        }

        // ---------------------------------------------------------------- quantisation

        static ushort Centi(float seconds)
        {
            if (float.IsNaN(seconds)) return 0;
            return (ushort)Math.Max(0, Math.Min(ushort.MaxValue, (int)Math.Round(seconds * 100f)));
        }

        static byte Tenth(float seconds)
        {
            if (float.IsNaN(seconds)) return 0;
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(seconds * 10f)));
        }

        static byte Unit(float value)
        {
            if (float.IsNaN(value)) return 0;
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value * 255f)));
        }

        static sbyte Signed(float value)
        {
            if (float.IsNaN(value)) return 0;
            return (sbyte)Math.Max(-127, Math.Min(127, (int)Math.Round(value * 127f)));
        }

        static float Mod(float value, float range)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            float m = value % range;
            return m < 0f ? m + range : m;
        }

        static short Metres(float value)
        {
            if (float.IsNaN(value)) return 0;
            int q = (int)Math.Round(value / PositionRange * short.MaxValue);
            return (short)Math.Max(short.MinValue + 1, Math.Min(short.MaxValue, q));
        }

        static void WritePoint(BinaryWriter w, float x, float y, float z)
        {
            w.Write(Metres(x));
            w.Write(Metres(y));
            w.Write(Metres(z));
        }

        static void ReadPoint(BinaryReader r, out float x, out float y, out float z)
        {
            x = r.ReadInt16() / (float)short.MaxValue * PositionRange;
            y = r.ReadInt16() / (float)short.MaxValue * PositionRange;
            z = r.ReadInt16() / (float)short.MaxValue * PositionRange;
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

    /// <summary>
    /// The host's list of recent skill moments. Every moment gets the next number and stays until it
    /// is EventLifeMs old; each client is sent the ones after the number it last confirmed (its input
    /// packets carry that), so a moment keeps coming until it has arrived. Over 10% loss that is "once,
    /// late by a packet" instead of the three-times rule of thumb, which still lost one in a thousand.
    /// </summary>
    public sealed class SkillEventLog
    {
        readonly List<SkillWireEvent> log = new List<SkillWireEvent>();
        ushort next = 1;

        public int Count { get { return log.Count; } }

        /// <summary>The oldest number still kept (the next one when there is none).</summary>
        public ushort First { get { return log.Count > 0 ? log[0].Seq : next; } }

        public ushort Add(SkillWireEvent e, uint nowMs)
        {
            e.Seq = next;
            next = unchecked((ushort)(next + 1));
            e.TimeMs = nowMs;
            log.Add(e);
            return e.Seq;
        }

        public void Expire(uint nowMs)
        {
            int n = 0;
            while (n < log.Count && nowMs - log[n].TimeMs > SkillWire.EventLifeMs) n++;
            if (n > 0) log.RemoveRange(0, n);
            // Never more than a few hundred, whatever happens (a burst of hits with nobody confirming).
            if (log.Count > 512) log.RemoveRange(0, log.Count - 512);
        }

        /// <summary>The moments after <paramref name="acked"/>, oldest first, at most <paramref name="max"/>.</summary>
        public void After(ushort acked, int max, List<SkillWireEvent> into)
        {
            into.Clear();
            for (int i = 0; i < log.Count && into.Count < max; i++)
                if (SkillWire.Newer(log[i].Seq, acked)) into.Add(log[i]);
        }

        public void Clear()
        {
            log.Clear();
        }
    }

    /// <summary>
    /// A client's side of the moments: which ones it has, so each is drawn exactly once whatever the
    /// order and the repeats (C5), and the number it confirms back (everything up to it has arrived).
    /// </summary>
    public sealed class SkillEventInbox
    {
        bool started;
        ushort acked;
        readonly HashSet<ushort> ahead = new HashSet<ushort>();

        public bool Started { get { return started; } }
        /// <summary>Every moment up to this number has arrived (or will never come).</summary>
        public ushort Acked { get { return acked; } }

        /// <summary>The packet's oldest kept number: the ones before it are given up on.</summary>
        public void Window(ushort first)
        {
            ushort before = unchecked((ushort)(first - 1));
            if (!started)
            {
                started = true;
                acked = before;
                return;
            }
            if (!SkillWire.Newer(before, acked)) return;
            acked = before;
            ahead.RemoveWhere(s => !SkillWire.Newer(s, acked));
            Advance();
        }

        /// <summary>True the first time a moment's number is seen.</summary>
        public bool Accept(ushort seq)
        {
            if (!started) return false;
            if (!SkillWire.Newer(seq, acked) || ahead.Contains(seq)) return false;
            ahead.Add(seq);
            Advance();
            return true;
        }

        void Advance()
        {
            while (ahead.Remove(unchecked((ushort)(acked + 1)))) acked = unchecked((ushort)(acked + 1));
        }

        public void Clear()
        {
            started = false;
            acked = 0;
            ahead.Clear();
        }
    }

    /// <summary>
    /// How far behind the newest snapshot a client draws (the jitter buffer). R111: measured instead
    /// of a fixed 110 ms. 70 ms rides out one lost snapshot (the next is 67 ms later) or a snapshot
    /// 37 ms late; over that it grows with the measured transit jitter (RFC 3550: how much the
    /// arrival gaps differ from the host's own send gaps), up to 160 ms. The two-PC test (10-10)
    /// saw no stutter at 80..110 ms ping, so it sits near the floor: about 40 ms less between a
    /// press and seeing it.
    /// </summary>
    public sealed class PlaybackDelay
    {
        public const double MinMs = 70, MaxMs = 160, FreeJitterMs = 4;
        /// <summary>Over this share of snapshots lost, two in a row get likely enough to wait one more snapshot for; it
        /// stops waiting under <see cref="LossyOffShare"/>. One line for both, and a share averaged over a second, put
        /// a 10% link right on it: the extra snapshot went on and off more than once a second (R111 loopback).</summary>
        public const double LossyShare = 0.08, LossyOffShare = 0.05;
        /// <summary>The loss share is averaged over about 128 snapshots (4 s): a lossy link stays lossy for a while.</summary>
        public const double LossAverage = 128;
        double lastLocal = -1, jitter, loss;
        bool lossy;
        uint lastHost;
        public double Milliseconds { get; private set; }
        public double JitterMs { get { return jitter; } }
        public double LossShare { get { return loss; } }
        /// <summary>Waiting one snapshot more for the losses.</summary>
        public bool Lossy { get { return lossy; } }

        public PlaybackDelay() { Milliseconds = MinMs; }

        /// <summary>A snapshot stamped <paramref name="hostMs"/> by the host arrived at <paramref name="localMs"/>, after
        /// <paramref name="lostBefore"/> lost ones (a gap in the snapshot numbers).</summary>
        public void Arrived(double localMs, uint hostMs, int lostBefore = 0)
        {
            if (lastLocal >= 0)
            {
                double d = Math.Abs((localMs - lastLocal) - unchecked((int)(hostMs - lastHost)));
                // Up fast (a bad patch shows at once), down slowly (so it does not flicker back).
                jitter += (d - jitter) * (d > jitter ? 0.25 : 1.0 / 32);
            }
            for (int i = 0; i < Math.Min(Math.Max(0, lostBefore), 10); i++) loss += (1 - loss) / LossAverage;
            loss -= loss / LossAverage;
            if (loss > LossyShare) lossy = true;
            else if (loss < LossyOffShare) lossy = false;
            lastLocal = localMs;
            lastHost = hostMs;
            double ms = MinMs + 2.5 * Math.Max(0, jitter - FreeJitterMs) + (lossy ? 1000.0 / 30 : 0);
            Milliseconds = Math.Max(MinMs, Math.Min(MaxMs, ms));
        }

        public void Reset()
        {
            lastLocal = -1;
            jitter = 0;
            loss = 0;
            lossy = false;
            Milliseconds = MinMs;
        }
    }
}
