using System;
using System.Collections.Generic;
using System.IO;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// One pawn's replicated pose. Only the hips position travels: every joint locks linear motion at
    /// the child's own origin, so the other ten bodies are rebuilt from the parent's rotation.
    /// </summary>
    public sealed class RagdollPose
    {
        public ulong id;
        public Vector3 hips;
        public readonly Quaternion[] rotations = new Quaternion[RagdollPawn.Count];
        public byte state;      // 0 active, 1 ragdoll, 2 getting up
        public bool grounded;
        public bool grabbing;
        public bool snap;       // teleport: render without interpolation
        public bool sprinting;
        public bool exhausted;
        public bool held;       // someone has this pawn by a hand
        public float stamina;   // 0..1, sent as a byte so a client can draw its own gauge
        public float escape;    // 0..1, the struggle meter, so a client draws the escape bar too
        public byte hook;       // HookPhase: put away, held, swinging, flying, reeling in, stuck
        public float hookCharge;    // 0..1, the swing gauge (a byte on the wire)
        public Vector3 hookPoint;   // the flying hook, or where it is stuck
        public byte piece;          // PieceKind (M11): what the pawn has been promoted to
        public uint ack;        // the owner's own send time echoed back, milliseconds

        public void CopyFrom(RagdollPose other)
        {
            id = other.id;
            hips = other.hips;
            Array.Copy(other.rotations, rotations, rotations.Length);
            state = other.state;
            grounded = other.grounded;
            grabbing = other.grabbing;
            snap = other.snap;
            sprinting = other.sprinting;
            exhausted = other.exhausted;
            held = other.held;
            stamina = other.stamina;
            escape = other.escape;
            hook = other.hook;
            hookCharge = other.hookCharge;
            hookPoint = other.hookPoint;
            piece = other.piece;
            ack = other.ack;
        }

        public static void Blend(RagdollPose from, RagdollPose to, float t, RagdollPose into)
        {
            into.id = to.id;
            into.hips = Vector3.Lerp(from.hips, to.hips, t);
            for (int i = 0; i < into.rotations.Length; i++)
                into.rotations[i] = Quaternion.Slerp(from.rotations[i], to.rotations[i], t);
            into.state = t < 0.5f ? from.state : to.state;
            into.grounded = t < 0.5f ? from.grounded : to.grounded;
            into.grabbing = t < 0.5f ? from.grabbing : to.grabbing;
            into.snap = to.snap;
            into.sprinting = t < 0.5f ? from.sprinting : to.sprinting;
            into.exhausted = t < 0.5f ? from.exhausted : to.exhausted;
            into.held = t < 0.5f ? from.held : to.held;
            into.stamina = Mathf.Lerp(from.stamina, to.stamina, t);
            into.escape = Mathf.Lerp(from.escape, to.escape, t);
            into.hook = t < 0.5f ? from.hook : to.hook;
            into.hookCharge = Mathf.Lerp(from.hookCharge, to.hookCharge, t);
            // A new throw starts from the hand: blend only while both ends show the same hook.
            into.hookPoint = from.hook == to.hook ? Vector3.Lerp(from.hookPoint, to.hookPoint, t) : to.hookPoint;
            into.piece = to.piece;
            into.ack = to.ack;
        }
    }

    /// <summary>One client's controls as they travel to the host. Move is world XZ, Aim a world unit vector. Used by the
    /// Sword Fight link; the lab link sends <see cref="RagdollLabInput"/> since R111.</summary>
    public struct RagdollNetInput
    {
        public Vector2 move;
        public bool jump, shove, grab, sprint;
        public bool shoveHeld;           // held: the hook's swing
        public bool ability, ability2;   // edges
        public bool interact;            // held
        public Vector3 aim;

        public static RagdollNetInput From(PawnInput input) => new RagdollNetInput
        {
            move = new Vector2(input.move.x, input.move.z), jump = input.jump, shove = input.shove, grab = input.grab,
            sprint = input.sprint, ability = input.ability, ability2 = input.ability2, interact = input.interact,
            aim = input.aim, shoveHeld = input.shoveHeld,
        };

        public PawnInput ToPawnInput() => new PawnInput
        {
            move = new Vector3(move.x, 0f, move.y), jump = jump, shove = shove, grab = grab, sprint = sprint,
            ability = ability, ability2 = ability2, interact = interact, aim = aim, shoveHeld = shoveHeld,
        };
    }

    /// <summary>
    /// The lab link's input (R111). The presses travel as counts (D-S7, like the capsule motor's jumps): every packet
    /// carries the running count of each press, so a lost packet only delays a press by one packet (1/60 s) instead of
    /// losing it - the left click that fires a rook, a pawn's second step, the skill key. The held buttons are plain bits.
    /// It also carries the piece the player asked for and the newest skill moment it has every one up to (the host keeps
    /// sending the rest).
    /// </summary>
    public struct RagdollLabInput
    {
        public Vector2 move;
        public bool grab, sprint;
        public bool shoveHeld;           // held: the hook's swing
        public bool interact;            // held (latched until sent)
        public Vector3 aim;
        public byte jumps, shoves, abilities, abilities2, skills;   // press counts (wrapping)
        public byte pieceAsk, pieceSeq;  // the piece asked for, and a number that changes with every ask (0 = none yet)
        public ushort eventAck;          // skill moments: everything up to this one has arrived

        /// <summary>The held part of a pawn's input (the presses are counted separately).</summary>
        public void Hold(PawnInput input)
        {
            move = new Vector2(input.move.x, input.move.z);
            grab = input.grab;
            sprint = input.sprint;
            shoveHeld = input.shoveHeld;
            aim = input.aim;
        }

        /// <summary>The held part as a pawn's input; the host adds the presses as edges, one per physics step.</summary>
        public PawnInput Held() => new PawnInput
        {
            move = new Vector3(move.x, 0f, move.y), grab = grab, sprint = sprint, interact = interact, aim = aim, shoveHeld = shoveHeld,
        };
    }

    /// <summary>A decoded snapshot with reusable pose objects (no per-packet allocation).</summary>
    public sealed class RagdollSnapshot
    {
        public uint tick;
        public uint hostTimeMs;
        // The host's obstacle clock (ObstacleClock.Now) when these poses were captured, so a
        // client draws every moving obstacle where the host had it for these poses.
        public double obstacleTime;
        public int count;
        readonly List<RagdollPose> poses = new List<RagdollPose>();

        public RagdollPose At(int index)
        {
            while (poses.Count <= index) poses.Add(new RagdollPose());
            return poses[index];
        }

        public void CopyFrom(RagdollSnapshot other)
        {
            tick = other.tick;
            hostTimeMs = other.hostTimeMs;
            obstacleTime = other.obstacleTime;
            count = other.count;
            for (int i = 0; i < count; i++) At(i).CopyFrom(other.At(i));
        }
    }

    /// <summary>
    /// Wire format for the ragdoll lab. Same shape as MotionProtocol: fixed length, magic, type,
    /// match id, and range checks on every field, so a malformed packet is dropped rather than trusted.
    /// </summary>
    public static class RagdollNetProtocol
    {
        // Bumped with each change to either packet so an older lab build's packets are dropped at the
        // door instead of half-read. CFR2: abilities, interact and aim in the input (2026-09-26).
        // CFR3: the struggle meter in the snapshot (2026-09-26).
        // CFR4: the left button held in the input; the grappling hook in the snapshot (2026-09-27).
        // CFR5: the piece kind in the snapshot (Queen of the Hill M11, 2026-09-27).
        // CFR6: the host's obstacle time in the snapshot header (2026-09-30).
        // CFR7: presses as counts, the piece asked for and the skill moments' confirmation in the input; the skills
        // packet (R111, 2026-10-10).
        public const uint Magic = 0x43465237;   // "CFR7"
        public const byte TypeInput = 1;
        public const byte TypeSnapshot = 2;
        // The Queen of the Hill round's opened sections (QueenHillMatch.Encode), host to clients.
        public const byte TypeMatch = 3;
        // The piece skills (R111): every busy piece's state, the bishops' wires, the barricades and the moments a client
        // has not confirmed yet (SkillWire), host to each client with every snapshot.
        public const byte TypeSkills = 4;
        // A development command to a loopback test client (LoopbackLabLink only; a Steam match drops it).
        public const byte TypeDev = 5;
        // The lab link's input with press counts (R111, RagdollLabInput); TypeInput stays the Sword Fight link's.
        public const byte TypeLabInput = 6;
        public const int HeaderBytes = 4 + 1 + 8;
        public const int MatchHeaderBytes = 4 + 1 + 8 + 2;
        public const int MaxMatchText = 400;
        public const int MaxBytes = 1024;
        public const int MaxPawns = 12;
        public const float PositionRange = 80f; // metres, symmetric around the arena origin

        public const int PoseBytes = 8 + 6 + RagdollPawn.Count * 4 + 1 + 1 + 1 + 1 + 1 + 6 + 1 + 4;  // 74
        public const int SnapshotHeaderBytes = 4 + 1 + 8 + 4 + 4 + 8 + 1;        // 30
        public const int InputBytes = 4 + 1 + 8 + 4 + 4 + 1 + 1 + 1 + 3;         // 27
        public const int LabInputBytes = 4 + 1 + 8 + 4 + 4 + 2 + 1 + 3 + 5 + 2 + 2;   // 36

        const float SmallestThreeRange = 0.70710678f;

        public static int SnapshotBytes(int pawns) => SnapshotHeaderBytes + pawns * PoseBytes;

        public static bool Newer(uint value, uint previous) => unchecked((int)(value - previous)) > 0;

        // ---------------------------------------------------------------- input

        public static byte[] Input(ulong session, uint sequence, uint clientTimeMs, in RagdollNetInput input)
        {
            using (var stream = new MemoryStream(InputBytes))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic);
                w.Write(TypeInput);
                w.Write(session);
                w.Write(sequence);
                w.Write(clientTimeMs);
                w.Write(Signed(input.move.x));
                w.Write(Signed(input.move.y));
                w.Write((byte)((input.jump ? 1 : 0) | (input.shove ? 2 : 0) | (input.grab ? 4 : 0) | (input.sprint ? 8 : 0)
                               | (input.ability ? 16 : 0) | (input.ability2 ? 32 : 0) | (input.interact ? 64 : 0)
                               | (input.shoveHeld ? 128 : 0)));
                // The aim as a direction, a byte per axis: about half a degree, plenty for a thrown hook.
                Vector3 aim = input.aim.sqrMagnitude > 1e-6f ? input.aim.normalized : Vector3.zero;
                w.Write(Signed(aim.x));
                w.Write(Signed(aim.y));
                w.Write(Signed(aim.z));
                return stream.ToArray();
            }
        }

        public static bool ReadInput(byte[] bytes, ulong session, out uint sequence, out uint clientTimeMs, out RagdollNetInput input)
        {
            sequence = 0;
            clientTimeMs = 0;
            input = default;
            if (bytes == null || bytes.Length != InputBytes) return false;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (r.ReadUInt32() != Magic || r.ReadByte() != TypeInput || r.ReadUInt64() != session) return false;
                sequence = r.ReadUInt32();
                clientTimeMs = r.ReadUInt32();
                input.move = new Vector2(r.ReadSByte() / 127f, r.ReadSByte() / 127f);
                // All eight bits are buttons now (128 = the left button held).
                byte buttons = r.ReadByte();
                input.jump = (buttons & 1) != 0;
                input.shove = (buttons & 2) != 0;
                input.grab = (buttons & 4) != 0;
                input.sprint = (buttons & 8) != 0;
                input.ability = (buttons & 16) != 0;
                input.ability2 = (buttons & 32) != 0;
                input.interact = (buttons & 64) != 0;
                input.shoveHeld = (buttons & 128) != 0;
                if (input.move.sqrMagnitude > 1.05f) input.move = input.move.normalized;
                var aim = new Vector3(r.ReadSByte() / 127f, r.ReadSByte() / 127f, r.ReadSByte() / 127f);
                // Anything but a unit vector or nothing at all is not an aim.
                float length = aim.magnitude;
                input.aim = length < 0.5f ? Vector3.zero : aim / length;
                return true;
            }
        }

        public static byte[] LabInput(ulong session, uint sequence, uint clientTimeMs, in RagdollLabInput input)
        {
            using (var stream = new MemoryStream(LabInputBytes))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic);
                w.Write(TypeLabInput);
                w.Write(session);
                w.Write(sequence);
                w.Write(clientTimeMs);
                w.Write(Signed(input.move.x));
                w.Write(Signed(input.move.y));
                w.Write((byte)((input.grab ? 1 : 0) | (input.sprint ? 2 : 0) | (input.interact ? 4 : 0) | (input.shoveHeld ? 8 : 0)));
                Vector3 aim = input.aim.sqrMagnitude > 1e-6f ? input.aim.normalized : Vector3.zero;
                w.Write(Signed(aim.x));
                w.Write(Signed(aim.y));
                w.Write(Signed(aim.z));
                w.Write(input.jumps);
                w.Write(input.shoves);
                w.Write(input.abilities);
                w.Write(input.abilities2);
                w.Write(input.skills);
                w.Write(input.pieceAsk);
                w.Write(input.pieceSeq);
                w.Write(input.eventAck);
                return stream.ToArray();
            }
        }

        public static bool ReadLabInput(byte[] bytes, ulong session, out uint sequence, out uint clientTimeMs, out RagdollLabInput input)
        {
            sequence = 0;
            clientTimeMs = 0;
            input = default;
            if (bytes == null || bytes.Length != LabInputBytes) return false;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (r.ReadUInt32() != Magic || r.ReadByte() != TypeLabInput || r.ReadUInt64() != session) return false;
                sequence = r.ReadUInt32();
                clientTimeMs = r.ReadUInt32();
                input.move = new Vector2(r.ReadSByte() / 127f, r.ReadSByte() / 127f);
                byte held = r.ReadByte();
                if (held > 15) return false;
                input.grab = (held & 1) != 0;
                input.sprint = (held & 2) != 0;
                input.interact = (held & 4) != 0;
                input.shoveHeld = (held & 8) != 0;
                if (input.move.sqrMagnitude > 1.05f) input.move = input.move.normalized;
                var aim = new Vector3(r.ReadSByte() / 127f, r.ReadSByte() / 127f, r.ReadSByte() / 127f);
                float length = aim.magnitude;
                input.aim = length < 0.5f ? Vector3.zero : aim / length;
                input.jumps = r.ReadByte();
                input.shoves = r.ReadByte();
                input.abilities = r.ReadByte();
                input.abilities2 = r.ReadByte();
                input.skills = r.ReadByte();
                input.pieceAsk = r.ReadByte();
                input.pieceSeq = r.ReadByte();
                input.eventAck = r.ReadUInt16();
                if (input.pieceSeq != 0 && !ChessPieces.IsValid(input.pieceAsk)) return false;
                return true;
            }
        }

        // ---------------------------------------------------------------- skills (R111)

        /// <summary>The skills packet: this header, then SkillWire's body (Core, tested without Unity).</summary>
        public static byte[] Skills(ulong session, SkillWirePacket packet)
        {
            using (var stream = new MemoryStream(256))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic);
                w.Write(TypeSkills);
                w.Write(session);
                SkillWire.Write(w, packet);
                w.Flush();
                return stream.ToArray();
            }
        }

        public static bool ReadSkills(byte[] bytes, ulong session, SkillWirePacket into)
        {
            if (into == null || bytes == null || bytes.Length < HeaderBytes + SkillWire.HeaderBytes || bytes.Length > MaxBytes) return false;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (r.ReadUInt32() != Magic || r.ReadByte() != TypeSkills || r.ReadUInt64() != session) return false;
                try
                {
                    return SkillWire.Read(r, bytes.Length - HeaderBytes, into);
                }
                catch (EndOfStreamException)
                {
                    return false;
                }
            }
        }

        // ---------------------------------------------------------------- development (loopback test link)

        public static byte[] Dev(ulong session, string text)
        {
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(text ?? "");
            if (payload.Length > MaxMatchText) return null;
            using (var stream = new MemoryStream(HeaderBytes + 2 + payload.Length))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic);
                w.Write(TypeDev);
                w.Write(session);
                w.Write((ushort)payload.Length);
                w.Write(payload);
                return stream.ToArray();
            }
        }

        public static bool ReadDev(byte[] bytes, ulong session, out string text)
        {
            text = null;
            if (bytes == null || bytes.Length < HeaderBytes + 2 || bytes.Length > HeaderBytes + 2 + MaxMatchText) return false;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (r.ReadUInt32() != Magic || r.ReadByte() != TypeDev || r.ReadUInt64() != session) return false;
                int length = r.ReadUInt16();
                if (bytes.Length != HeaderBytes + 2 + length) return false;
                text = System.Text.Encoding.UTF8.GetString(r.ReadBytes(length));
                return true;
            }
        }

        /// <summary>The packet's type (0 if it is not one of ours).</summary>
        public static byte TypeOf(byte[] bytes)
        {
            if (bytes == null || bytes.Length < HeaderBytes) return 0;
            uint magic = (uint)(bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24);
            return magic == Magic ? bytes[4] : (byte)0;
        }

        static sbyte Signed(float value) => (sbyte)Mathf.Clamp(Mathf.RoundToInt(value * 127f), -127, 127);

        // ---------------------------------------------------------------- snapshot

        public static byte[] Snapshot(ulong session, uint tick, uint hostTimeMs, IReadOnlyList<RagdollPose> poses,
                                      double obstacleTime = 0d)
        {
            int count = Mathf.Min(poses.Count, MaxPawns);
            using (var stream = new MemoryStream(SnapshotBytes(count)))
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
                    var pose = poses[i];
                    w.Write(pose.id);
                    w.Write(Quantize(pose.hips.x));
                    w.Write(Quantize(pose.hips.y));
                    w.Write(Quantize(pose.hips.z));
                    for (int b = 0; b < RagdollPawn.Count; b++) w.Write(PackRotation(pose.rotations[b]));
                    w.Write((byte)((pose.state & 3) | (pose.grounded ? 4 : 0) | (pose.grabbing ? 8 : 0) | (pose.snap ? 16 : 0)
                                   | (pose.sprinting ? 32 : 0) | (pose.exhausted ? 64 : 0) | (pose.held ? 128 : 0)));
                    w.Write((byte)Mathf.Clamp(Mathf.RoundToInt(pose.stamina * 255f), 0, 255));
                    w.Write((byte)Mathf.Clamp(Mathf.RoundToInt(pose.escape * 255f), 0, 255));
                    w.Write(pose.hook);
                    w.Write((byte)Mathf.Clamp(Mathf.RoundToInt(pose.hookCharge * 255f), 0, 255));
                    w.Write(Quantize(pose.hookPoint.x));
                    w.Write(Quantize(pose.hookPoint.y));
                    w.Write(Quantize(pose.hookPoint.z));
                    w.Write(pose.piece);
                    w.Write(pose.ack);
                }
                return stream.ToArray();
            }
        }

        public static bool ReadSnapshot(byte[] bytes, ulong session, RagdollSnapshot into)
        {
            if (into == null || bytes == null || bytes.Length < SnapshotHeaderBytes || bytes.Length > MaxBytes) return false;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (r.ReadUInt32() != Magic || r.ReadByte() != TypeSnapshot || r.ReadUInt64() != session) return false;
                uint tick = r.ReadUInt32();
                uint hostTimeMs = r.ReadUInt32();
                double obstacleTime = r.ReadDouble();
                if (double.IsNaN(obstacleTime) || double.IsInfinity(obstacleTime)) return false;
                int count = r.ReadByte();
                if (count > MaxPawns || bytes.Length != SnapshotBytes(count)) return false;
                var seen = new HashSet<ulong>();
                for (int i = 0; i < count; i++)
                {
                    var pose = into.At(i);
                    pose.id = r.ReadUInt64();
                    if (pose.id == 0 || !seen.Add(pose.id)) return false;
                    pose.hips = new Vector3(Dequantize(r.ReadInt16()), Dequantize(r.ReadInt16()), Dequantize(r.ReadInt16()));
                    for (int b = 0; b < RagdollPawn.Count; b++) pose.rotations[b] = UnpackRotation(r.ReadUInt32());
                    // Every bit of the flags byte is used; the state's two bits are range-checked below.
                    byte flags = r.ReadByte();
                    pose.state = (byte)(flags & 3);
                    pose.grounded = (flags & 4) != 0;
                    pose.grabbing = (flags & 8) != 0;
                    pose.snap = (flags & 16) != 0;
                    pose.sprinting = (flags & 32) != 0;
                    pose.exhausted = (flags & 64) != 0;
                    pose.held = (flags & 128) != 0;
                    pose.stamina = r.ReadByte() / 255f;
                    pose.escape = r.ReadByte() / 255f;
                    pose.hook = r.ReadByte();
                    pose.hookCharge = r.ReadByte() / 255f;
                    pose.hookPoint = new Vector3(Dequantize(r.ReadInt16()), Dequantize(r.ReadInt16()), Dequantize(r.ReadInt16()));
                    pose.piece = r.ReadByte();
                    pose.ack = r.ReadUInt32();
                    if (pose.state > 2 || pose.hook > 5 || !ChessPieces.IsValid(pose.piece)) return false;
                }
                into.tick = tick;
                into.hostTimeMs = hostTimeMs;
                into.obstacleTime = obstacleTime;
                into.count = count;
                return true;
            }
        }

        // ---------------------------------------------------------------- match state

        /// <summary>
        /// Which sections are open and since when (QueenHillMatch.Encode: short text, the shared clock's
        /// milliseconds). Sent by the host twice a second: an opening is final for the round, so repeating
        /// the whole list makes it arrive over an unreliable channel and reach late joiners. Null if too long.
        /// </summary>
        public static byte[] Match(ulong session, string text)
        {
            byte[] payload = System.Text.Encoding.UTF8.GetBytes(text ?? "");
            if (payload.Length > MaxMatchText) return null;
            using (var stream = new MemoryStream(MatchHeaderBytes + payload.Length))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic);
                w.Write(TypeMatch);
                w.Write(session);
                w.Write((ushort)payload.Length);
                w.Write(payload);
                return stream.ToArray();
            }
        }

        public static bool ReadMatch(byte[] bytes, ulong session, out string text)
        {
            text = null;
            if (bytes == null || bytes.Length < MatchHeaderBytes || bytes.Length > MatchHeaderBytes + MaxMatchText) return false;
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (r.ReadUInt32() != Magic || r.ReadByte() != TypeMatch || r.ReadUInt64() != session) return false;
                int length = r.ReadUInt16();
                if (bytes.Length != MatchHeaderBytes + length) return false;
                try
                {
                    text = new System.Text.UTF8Encoding(false, true).GetString(r.ReadBytes(length));
                }
                catch (ArgumentException)
                {
                    return false;
                }
                return true;
            }
        }

        // ---------------------------------------------------------------- quantisation

        public static short Quantize(float metres) =>
            (short)Mathf.Clamp(Mathf.RoundToInt(metres / PositionRange * short.MaxValue), short.MinValue + 1, short.MaxValue);

        public static float Dequantize(short value) => value / (float)short.MaxValue * PositionRange;

        /// <summary>Smallest-three: 2 bits for the dropped component, 10 bits for each of the others.</summary>
        public static uint PackRotation(Quaternion q)
        {
            float x = q.x, y = q.y, z = q.z, w = q.w;
            float norm = Mathf.Sqrt(x * x + y * y + z * z + w * w);
            if (norm < 1e-6f || float.IsNaN(norm))
            {
                x = y = z = 0f;
                w = 1f;
            }
            else
            {
                x /= norm; y /= norm; z /= norm; w /= norm;
            }
            int largest = 0;
            float max = Mathf.Abs(x);
            if (Mathf.Abs(y) > max) { largest = 1; max = Mathf.Abs(y); }
            if (Mathf.Abs(z) > max) { largest = 2; max = Mathf.Abs(z); }
            if (Mathf.Abs(w) > max) { largest = 3; }
            float sign = largest == 0 ? x : largest == 1 ? y : largest == 2 ? z : w;
            if (sign < 0f) { x = -x; y = -y; z = -z; w = -w; }
            float a, b, c;
            switch (largest)
            {
                case 0: a = y; b = z; c = w; break;
                case 1: a = x; b = z; c = w; break;
                case 2: a = x; b = y; c = w; break;
                default: a = x; b = y; c = z; break;
            }
            return ((uint)largest << 30) | ((uint)Encode(a) << 20) | ((uint)Encode(b) << 10) | (uint)Encode(c);
        }

        public static Quaternion UnpackRotation(uint value)
        {
            int largest = (int)(value >> 30);
            float a = Decode((int)((value >> 20) & 1023));
            float b = Decode((int)((value >> 10) & 1023));
            float c = Decode((int)(value & 1023));
            float d = Mathf.Sqrt(Mathf.Max(0f, 1f - a * a - b * b - c * c));
            switch (largest)
            {
                case 0: return new Quaternion(d, a, b, c);
                case 1: return new Quaternion(a, d, b, c);
                case 2: return new Quaternion(a, b, d, c);
                default: return new Quaternion(a, b, c, d);
            }
        }

        static int Encode(float value) =>
            Mathf.Clamp(Mathf.RoundToInt((value / SmallestThreeRange * 0.5f + 0.5f) * 1023f), 0, 1023);

        static float Decode(int value) => (value / 1023f * 2f - 1f) * SmallestThreeRange;
    }
}
