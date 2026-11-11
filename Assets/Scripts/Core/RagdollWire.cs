using System;
using System.Collections.Generic;
using System.IO;

namespace ChessFight.Network
{
    /// <summary>
    /// A world point on the wire (R541): x and z in 24 bits, a millimetre a step over +-8388 m around the scene origin,
    /// y in 16 bits, about 1 cm a step over +-320 m - 8 bytes. All three were 16 bits over +-320 m until R541 (+-80 m
    /// until R124), and the Pawn Rush section round (R508: seven sections and the corridors between them) runs to about
    /// z 580 m: every client drew each pawn past z 320 m pinned there, an unseen wall on the grand stair hall's last
    /// glass stones, while the host saw it go on (PITFALLS 38). The height keeps its 16 bits so a pawn grows by two
    /// bytes, not three, and six players still get 60 snapshots a second (SnapshotRate).
    /// </summary>
    public static class WirePoint
    {
        public const int Bytes = 3 + 2 + 3;                    // x, y, z
        public const int FlatSteps = (1 << 23) - 1;            // the furthest a 24-bit x or z goes, in millimetres
        public const float FlatRange = FlatSteps / 1000f;      // 8388.607 m
        public const float HeightRange = 320f;

        public static int ToFlat(float metres)
        {
            if (float.IsNaN(metres)) return 0;
            return (int)Math.Max(-FlatSteps, Math.Min(FlatSteps, Math.Round(metres * 1000.0)));
        }

        public static float FromFlat(int steps) => steps / 1000f;

        public static short ToHeight(float metres)
        {
            if (float.IsNaN(metres)) return 0;
            return (short)Math.Max(short.MinValue + 1, Math.Min(short.MaxValue, Math.Round(metres / HeightRange * short.MaxValue)));
        }

        public static float FromHeight(short value) => value / (float)short.MaxValue * HeightRange;

        public static void Write(BinaryWriter w, int x, short y, int z)
        {
            Write24(w, x);
            w.Write(y);
            Write24(w, z);
        }

        public static void Read(BinaryReader r, out int x, out short y, out int z)
        {
            x = Read24(r);
            y = r.ReadInt16();
            z = Read24(r);
        }

        public static void Write(BinaryWriter w, float x, float y, float z) => Write(w, ToFlat(x), ToHeight(y), ToFlat(z));

        public static void Read(BinaryReader r, out float x, out float y, out float z)
        {
            Read(r, out int qx, out short qy, out int qz);
            x = FromFlat(qx);
            y = FromHeight(qy);
            z = FromFlat(qz);
        }

        // Little-endian like the rest: the low 16 bits, then the signed top 8.
        static void Write24(BinaryWriter w, int value)
        {
            w.Write((ushort)(value & 0xFFFF));
            w.Write((sbyte)(value >> 16));
        }

        static int Read24(BinaryReader r)
        {
            int low = r.ReadUInt16();
            return (r.ReadSByte() << 16) | low;
        }
    }

    /// <summary>One pawn's pose as it travels, already quantised (RagdollNetProtocol turns a RagdollPose into it and back).</summary>
    public sealed class PoseWire
    {
        public ulong Id;
        public int HipsX, HipsZ;                                // WirePoint.ToFlat: a millimetre over +-8388 m (R541)
        public short HipsY;                                     // WirePoint.ToHeight: about 1 cm over +-320 m
        public readonly uint[] Rotations = new uint[RagdollWire.Bodies];   // smallest-three (RagdollNetProtocol.PackRotation)
        public byte Flags;                                      // state (2 bits), grounded, grabbing, snap, sprinting, exhausted, held
        public byte Stamina, Escape;                            // 0..255
        public byte Hook;                                       // HookPhase 0..5
        public byte HookCharge;                                 // travels only from RagdollWire.HookTail on, read as 0 otherwise
        public int HookX, HookZ;                                // the same as the hips
        public short HookY;
        public byte Piece;                                      // PieceKind
        public ushort Ack;                                      // the owner's send time echoed back, its low 16 bits (0 = none)
    }

    /// <summary>A decoded snapshot with reusable pose objects.</summary>
    public sealed class SnapshotWire
    {
        public uint Tick, HostTimeMs;
        public double ObstacleTime;
        public int Count;
        readonly List<PoseWire> poses = new List<PoseWire>();

        public PoseWire At(int index)
        {
            while (poses.Count <= index) poses.Add(new PoseWire());
            return poses[index];
        }
    }

    /// <summary>
    /// The ragdoll snapshot's bytes (R297: here so that the Core tests check them; RagdollNetProtocol quantises the poses):
    /// a 30-byte header, then every pawn in 67 bytes, or 76 while its hook is swung round, flying, pulling or stuck - the
    /// swing gauge and the hook's point travel only then, as a client draws neither while the hook is put away or hangs
    /// from the hand. The owner's echoed send time is its low 16 bits (AckAge). A packet is read only if it ends exactly
    /// where its last pawn does.
    /// </summary>
    public static class RagdollWire
    {
        // Every ragdoll packet's magic (RagdollNetProtocol's too), bumped with each change to any of them so an older
        // build's packets are dropped at the door instead of half-read. CFR2: abilities, interact and aim in the input
        // (2026-09-26). CFR3: the struggle meter in the snapshot (2026-09-26).
        // CFR4: the left button held in the input; the grappling hook in the snapshot (2026-09-27).
        // CFR5: the piece kind in the snapshot (Queen of the Hill M11, 2026-09-27).
        // CFR6: the host's obstacle time in the snapshot header (2026-09-30).
        // CFR7: presses as counts, the piece asked for and the skill moments' confirmation in the input; the skills
        // packet (R111, 2026-10-10).
        // CFR8: the client's round trip, playback delay and loss in the input, for the host's test screen (R112).
        // CFR9: the Sword Fight skill test online (R121): its state packet, its input with the aimed point, and its skill
        // moments in the skills packet (SkillWire.ModeSwordFight).
        // CFRA: positions span +-320 m instead of +-80 m (R124). Pawn Rush course 01 runs to z 246 m, and clients drew
        // every pawn past 80 m pinned at 80 m while the host saw them where they were.
        // CFRB: the skill packet's positions span +-320 m as well (SkillWire.PositionRange, R125: the skills on course 01).
        // CFRC: the emote ask (slot and a changing number) in the lab input, two bytes (emotes, from skill-network-test).
        // CFRD: the snapshot's pawn is 65 bytes instead of 74 (R297, HOST_AUTHORITY P6): the hook's gauge and point only
        // while it is out of the hand, the echoed send time in 16 bits.
        // CFRE: every point is a WirePoint (R541): x and z over +-8388 m instead of +-320 m - the hips and the hook in the
        // snapshot (67 and 76 bytes a pawn), the skills packet's points, the Sword Fight input's aimed point. The Pawn Rush
        // section round runs to z 580 m, and clients drew every pawn past 320 m pinned at 320 m.
        public const uint Magic = 0x43465245;   // "CFRE"
        public const byte TypeSnapshot = 2;
        public const int MaxBytes = 1024;
        public const int MaxPawns = 12;
        public const int Bodies = 11;   // RagdollPawn.Count (RagdollNetProtocol checks it when it compiles)
        public const int HeaderBytes = 4 + 1 + 8 + 4 + 4 + 8 + 1;                               // 30
        public const int PoseBytes = 8 + WirePoint.Bytes + Bodies * 4 + 1 + 1 + 1 + 1 + 1 + 2;  // 67
        public const int HookBytes = 1 + WirePoint.Bytes;                                       // the gauge and the point
        public const int MaxPoseBytes = PoseBytes + HookBytes;                                  // 76
        public const byte HookTail = 2;   // HookPhase.Charging: from here on the gauge and the point travel
        public const byte MaxHook = 5;    // HookPhase.Stuck
        /// <summary>A send time older than this is not echoed (0 instead): a client counts no round trip over 10 s, and a
        /// 16-bit echo would come round again after 65.5 s.</summary>
        public const float AckFreshSeconds = 10f;

        public static int PoseLength(byte hook) => hook >= HookTail ? MaxPoseBytes : PoseBytes;

        // R297 [3]: one stream, writer and reader for every snapshot (a snapshot's array lasts until the next one).
        static readonly PacketStreams streams = new PacketStreams(MaxBytes);

        public static byte[] Snapshot(ulong session, uint tick, uint hostTimeMs, double obstacleTime, IReadOnlyList<PoseWire> poses, int count)
        {
            count = Math.Max(0, Math.Min(Math.Min(count, poses.Count), MaxPawns));
            var w = streams.Begin();
            w.Write(Magic);
            w.Write(TypeSnapshot);
            w.Write(session);
            w.Write(tick);
            w.Write(hostTimeMs);
            w.Write(obstacleTime);
            w.Write((byte)count);
            for (int i = 0; i < count; i++)
            {
                var p = poses[i];
                w.Write(p.Id);
                WirePoint.Write(w, p.HipsX, p.HipsY, p.HipsZ);
                for (int b = 0; b < Bodies; b++) w.Write(p.Rotations[b]);
                w.Write(p.Flags);
                w.Write(p.Stamina);
                w.Write(p.Escape);
                w.Write(p.Hook);
                if (p.Hook >= HookTail)
                {
                    w.Write(p.HookCharge);
                    WirePoint.Write(w, p.HookX, p.HookY, p.HookZ);
                }
                w.Write(p.Piece);
                w.Write(p.Ack);
            }
            return streams.Finish();
        }

        public static bool ReadSnapshot(byte[] bytes, ulong session, SnapshotWire into)
        {
            if (into == null || bytes == null || bytes.Length < HeaderBytes || bytes.Length > MaxBytes) return false;
            var r = streams.Open(bytes);
            if (r.ReadUInt32() != Magic || r.ReadByte() != TypeSnapshot || r.ReadUInt64() != session) return false;
            uint tick = r.ReadUInt32();
            uint hostTimeMs = r.ReadUInt32();
            double obstacleTime = r.ReadDouble();
            if (double.IsNaN(obstacleTime) || double.IsInfinity(obstacleTime)) return false;
            int count = r.ReadByte();
            if (count > MaxPawns || bytes.Length < HeaderBytes + count * PoseBytes || bytes.Length > HeaderBytes + count * MaxPoseBytes)
                return false;
            // Read so far: every pawn's length comes from its own hook, and the packet must end with the last one.
            int at = HeaderBytes;
            for (int i = 0; i < count; i++)
            {
                if (bytes.Length - at < PoseBytes) return false;
                var p = into.At(i);
                p.Id = r.ReadUInt64();
                if (p.Id == 0) return false;
                // Each pawn once: the ones read so far, at most 11 (no new set for every snapshot, R297).
                for (int j = 0; j < i; j++) if (into.At(j).Id == p.Id) return false;
                WirePoint.Read(r, out p.HipsX, out p.HipsY, out p.HipsZ);
                for (int b = 0; b < Bodies; b++) p.Rotations[b] = r.ReadUInt32();
                // Every bit of the flags byte is used; the state's two bits are range-checked.
                p.Flags = r.ReadByte();
                p.Stamina = r.ReadByte();
                p.Escape = r.ReadByte();
                p.Hook = r.ReadByte();
                if ((p.Flags & 3) > 2 || p.Hook > MaxHook) return false;
                if (p.Hook >= HookTail)
                {
                    if (bytes.Length - at < MaxPoseBytes) return false;
                    p.HookCharge = r.ReadByte();
                    WirePoint.Read(r, out p.HookX, out p.HookY, out p.HookZ);
                }
                else
                {
                    p.HookCharge = 0;
                    p.HookX = p.HookZ = 0;
                    p.HookY = 0;
                }
                p.Piece = r.ReadByte();
                p.Ack = r.ReadUInt16();
                if (!ChessPieces.IsValid(p.Piece)) return false;
                at += PoseLength(p.Hook);
            }
            if (at != bytes.Length) return false;
            into.Tick = tick;
            into.HostTimeMs = hostTimeMs;
            into.ObstacleTime = obstacleTime;
            into.Count = count;
            return true;
        }

        /// <summary>A client's round trip from the snapshot's echo of its own send time: the milliseconds since then on its
        /// clock, counted in 16 bits like the echo (right while the trip is under 65.5 s), or -1 for no echo (0).</summary>
        public static int AckAge(uint nowMs, uint ack) => ack == 0 ? -1 : (ushort)(nowMs - ack);
    }
}
