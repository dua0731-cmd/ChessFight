using System;
using System.Collections.Generic;
using System.IO;

namespace ChessFight.Network
{
    /// <summary>One pawn's pose as it travels, already quantised (RagdollNetProtocol turns a RagdollPose into it and back).</summary>
    public sealed class PoseWire
    {
        public ulong Id;
        public short HipsX, HipsY, HipsZ;                       // RagdollNetProtocol.Quantize: about 1 cm over +-320 m
        public readonly uint[] Rotations = new uint[RagdollWire.Bodies];   // smallest-three (RagdollNetProtocol.PackRotation)
        public byte Flags;                                      // state (2 bits), grounded, grabbing, snap, sprinting, exhausted, held
        public byte Stamina, Escape;                            // 0..255
        public byte Hook;                                       // HookPhase 0..5
        public byte HookCharge;                                 // travels only from RagdollWire.HookTail on, read as 0 otherwise
        public short HookX, HookY, HookZ;                       // the same
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
    /// a 30-byte header, then every pawn in 65 bytes, or 72 while its hook is swung round, flying, pulling or stuck - the
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
        public const uint Magic = 0x43465244;   // "CFRD"
        public const byte TypeSnapshot = 2;
        public const int MaxBytes = 1024;
        public const int MaxPawns = 12;
        public const int Bodies = 11;   // RagdollPawn.Count (RagdollNetProtocol checks it when it compiles)
        public const int HeaderBytes = 4 + 1 + 8 + 4 + 4 + 8 + 1;                  // 30
        public const int PoseBytes = 8 + 6 + Bodies * 4 + 1 + 1 + 1 + 1 + 1 + 2;   // 65
        public const int HookBytes = 1 + 6;                                        // the gauge and the point
        public const int MaxPoseBytes = PoseBytes + HookBytes;                     // 72
        public const byte HookTail = 2;   // HookPhase.Charging: from here on the gauge and the point travel
        public const byte MaxHook = 5;    // HookPhase.Stuck
        /// <summary>A send time older than this is not echoed (0 instead): a client counts no round trip over 10 s, and a
        /// 16-bit echo would come round again after 65.5 s.</summary>
        public const float AckFreshSeconds = 10f;

        public static int PoseLength(byte hook) => hook >= HookTail ? MaxPoseBytes : PoseBytes;

        public static byte[] Snapshot(ulong session, uint tick, uint hostTimeMs, double obstacleTime, IReadOnlyList<PoseWire> poses, int count)
        {
            count = Math.Max(0, Math.Min(Math.Min(count, poses.Count), MaxPawns));
            int length = HeaderBytes;
            for (int i = 0; i < count; i++) length += PoseLength(poses[i].Hook);
            using (var stream = new MemoryStream(length))
            using (var w = new BinaryWriter(stream))
            {
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
                    w.Write(p.HipsX);
                    w.Write(p.HipsY);
                    w.Write(p.HipsZ);
                    for (int b = 0; b < Bodies; b++) w.Write(p.Rotations[b]);
                    w.Write(p.Flags);
                    w.Write(p.Stamina);
                    w.Write(p.Escape);
                    w.Write(p.Hook);
                    if (p.Hook >= HookTail)
                    {
                        w.Write(p.HookCharge);
                        w.Write(p.HookX);
                        w.Write(p.HookY);
                        w.Write(p.HookZ);
                    }
                    w.Write(p.Piece);
                    w.Write(p.Ack);
                }
                return stream.ToArray();
            }
        }

        public static bool ReadSnapshot(byte[] bytes, ulong session, SnapshotWire into)
        {
            if (into == null || bytes == null || bytes.Length < HeaderBytes || bytes.Length > MaxBytes) return false;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
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
                var seen = new HashSet<ulong>();
                for (int i = 0; i < count; i++)
                {
                    if (bytes.Length - at < PoseBytes) return false;
                    var p = into.At(i);
                    p.Id = r.ReadUInt64();
                    if (p.Id == 0 || !seen.Add(p.Id)) return false;
                    p.HipsX = r.ReadInt16();
                    p.HipsY = r.ReadInt16();
                    p.HipsZ = r.ReadInt16();
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
                        p.HookX = r.ReadInt16();
                        p.HookY = r.ReadInt16();
                        p.HookZ = r.ReadInt16();
                    }
                    else
                    {
                        p.HookCharge = 0;
                        p.HookX = p.HookY = p.HookZ = 0;
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
        }

        /// <summary>A client's round trip from the snapshot's echo of its own send time: the milliseconds since then on its
        /// clock, counted in 16 bits like the echo (right while the trip is under 65.5 s), or -1 for no echo (0).</summary>
        public static int AckAge(uint nowMs, uint ack) => ack == 0 ? -1 : (ushort)(nowMs - ack);
    }
}
