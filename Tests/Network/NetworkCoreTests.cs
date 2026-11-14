using System;
using System.Collections.Generic;
using System.Linq;
using ChessFight.Network;

public static class NetworkCoreTests
{
    static int passed;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Test(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    static ulong[] Ids(int start, int count) => Enumerable.Range(start, count).Select(x => (ulong)x).ToArray();
    // Host election candidates. Class-level rather than local functions: mcs, which runs these
    // tests on Linux (Tools/run-tests-linux.sh), cannot parse local functions.
    static HostElection.Candidate C(ulong id, int fit, int ping = -1) => new HostElection.Candidate { Id = id, Fitness = fit, PingMs = ping };
    static HostElection.Candidate M(ulong id, int now, int machine) => new HostElection.Candidate { Id = id, Fitness = now, BaseFitness = machine, PingMs = -1 };
    // R624: one player's pick on the entrance screen.
    static SwordPickDraft.Entry Pick(ulong id, int team, PieceKind piece, double at, bool bot = false) =>
        new SwordPickDraft.Entry { Id = id, Team = team, Piece = piece, At = at, Bot = bot };
    static bool Reserve(TeamReservations r, int first, int count, double now = 0) => r.Reserve((ulong)first, (ulong)first + 100, "t" + first, Ids(first, count), now, out _);
    // An input packet as far as the skill key goes (no tuples: mcs runs these tests on Linux).
    struct PressPacket { public uint Seq; public byte Count; }
    // A snapshot pawn with every field set from its id, the hook's gauge and point too (R297). R541: the points far out on
    // both sides of the old +-320 m (580 m on, 400 m back, a hook up to 4 km off) and a height at its edge.
    static PoseWire WirePose(int id, byte hook)
    {
        var p = new PoseWire
        {
            Id = (ulong)id * 7919UL + 76561198000000000UL, HipsX = id * 101 - 400000, HipsY = (short)-id, HipsZ = 580000 + id * 7,
            Flags = (byte)((id % 3) | ((id * 37) & 0xFC)), Stamina = (byte)(id * 13), Escape = (byte)(id * 29), Hook = hook,
            HookCharge = (byte)(id * 3 + 1), HookX = id * -77, HookY = short.MaxValue, HookZ = id * 300000 - WirePoint.FlatSteps / 2,
            Piece = (byte)(id % ChessPieces.Count), Ack = (ushort)(id * 977),
        };
        for (int b = 0; b < RagdollWire.Bodies; b++) p.Rotations[b] = (uint)id * 2654435761u + (uint)b * 40503u;
        return p;
    }
    static bool SameFixed(PoseWire a, PoseWire b)
    {
        for (int i = 0; i < RagdollWire.Bodies; i++) if (a.Rotations[i] != b.Rotations[i]) return false;
        return a.Id == b.Id && a.HipsX == b.HipsX && a.HipsY == b.HipsY && a.HipsZ == b.HipsZ && a.Flags == b.Flags && a.Stamina == b.Stamina
               && a.Escape == b.Escape && a.Hook == b.Hook && a.Piece == b.Piece && a.Ack == b.Ack;
    }
    static byte[] Resized(byte[] bytes, int length)
    {
        var copy = new byte[length];
        Array.Copy(bytes, copy, Math.Min(length, bytes.Length));
        return copy;
    }
    // Every Core packet's bytes for a fixed input, pinned before R297 [3] reused their buffers (FNV-1a 64).
    static string Fnv(byte[] bytes)
    {
        ulong h = 14695981039346656037UL;
        foreach (byte b in bytes) { h ^= b; h *= 1099511628211UL; }
        return h.ToString("x16");
    }
    static byte[] Through(Action<System.IO.BinaryWriter> write)
    {
        var ms = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(ms);
        write(w);
        w.Flush();
        return ms.ToArray();
    }
    static SkillWirePacket GoldenSkills()
    {
        var p = new SkillWirePacket { HostTimeMs = 123456, FirstSeq = 65530 };
        p.States.Add(new SkillWireState { Pawn = SkillWire.Ref(1, 3), Stage = 2, Flags = 0xA5, Flags2 = 3, StageTime = 1.234f, Cooldown = 7.25f, CooldownTotal = 12f,
            Dx = 0.6f, Dy = 0.8f, Px = -12.345f, Py = 1.5f, Pz = 33.3f, Qx = 4, Qy = -0.5f, Qz = 79, Target = SkillWire.Ref(0, 5), Other = SkillWire.None,
            A = 2.5f, B = 25.5f, C = 200, D = 7 });
        p.States.Add(new SkillWireState { Pawn = SkillWire.Ref(0, 0), Stage = 1, StageTime = 0.5f, Cooldown = 3f, CooldownTotal = 9f, Dx = -1f, Target = SkillWire.None,
            Other = SkillWire.None });
        p.Wires.Add(new SkillWireObject { Id = 9, Owner = SkillWire.Ref(0, 1), X = 3, Y = 0.02f, Z = -4, Yaw = 271, Age = 2.5f, Flags = 1, Trips = 1, Along0 = 0.33f,
            Along1 = 0.5f, Out0 = 0.62f, Out1 = -0.31f });
        p.Barricades.Add(new SkillWireBarricade { Index = 0, Standing = false, RegrowLeft = 3.4f });
        p.Barricades.Add(new SkillWireBarricade { Index = 2, Standing = true });
        p.Events.Add(new SkillWireEvent { Seq = 65531, TimeMs = 123000, Mode = SkillWire.ModeRetiredQueenHill, Kind = 9, By = SkillWire.Ref(1, 3), Target = SkillWire.None,
            X = 1, Y = 2, Z = 3, Dx = 0, Dy = 0, Dz = -1, Ux = -5, Uy = 0, Uz = 5, Count = 3, Size = 6.5f, Source = 9 });
        p.Events.Add(new SkillWireEvent { Seq = 65532, TimeMs = 123010, Mode = SkillWire.ModeSwordFight, Kind = 36, By = SkillWire.Ref(0, 2), Target = SkillWire.Ref(1, 0),
            X = 6.5f, Y = 0.4f, Z = -1, Dx = 1, Count = 1, Size = 3.5f });
        return p;
    }
    static SwordWirePacket GoldenSword()
    {
        var p = new SwordWirePacket { HostTimeMs = 987654, White = 3, Black = 12, Remaining = 3599.9f, Finished = false };
        p.Fighters.Add(new SwordWireFighter { Ref = SkillWire.Ref(0, 0), Team = 1, Alive = true, Drawn = true, Protection = 1.25f, Flash = 0.16f, Wx = 0.31f, Wy = -0.42f,
            Wz = 1.234f, WRot = 0xC0FFEE11u, EdgeCondition = true, Why = 6, EdgeTarget = SkillWire.Ref(1, 2) });
        p.Fighters.Add(new SwordWireFighter { Ref = SkillWire.Ref(1, 2), Team = 0, Alive = true,
            Skill = true, Stage = 3, Detail = 10, SkillFlags = 2, StageTime = 0.37f, Cooldown = 1.5f, CooldownTotal = 2f, Dx = 0.6f, Dz = 0.8f, Px = 4.5f, Py = 0.01f,
            Pz = -6.25f, Ox = -1, Oz = 2, Reach = 5.95f, Wave = 3.3f, Dash = 2.05f, PinCandidate = SkillWire.None, Slow = 1.5f, Pin = 0.8f,
            Edge = true, EdgeStage = 2, EdgeDetail = 12, EdgeFlags = 3, EdgeTime = 0.45f, EdgeCooldownTotal = 16f, Carried = SkillWire.Ref(0, 0), Ax = 7, Az = 1, Outx = 1,
            Lx = 5.6f, Lz = 1.2f, Sx = 6.9f, Sy = 0.6f, Sz = 0.4f,
            Glow = true, Gx = 7, Gz = -0.5f, GOx = 1, GAz = 1, GAt = 6.5f, GTo = 14f });
        p.Collapses.Add(new SwordWireCollapse { Id = 7, By = SkillWire.Ref(1, 2), X0 = 5, Z0 = -1.5f, X1 = 7, Z1 = 1.5f, Ox = 1, Az = 1, Lx = 7, Age = 1.23f, Crack = 0.8f,
            Open = 5f });
        return p;
    }
    static PawnRushNetState GoldenRush()
    {
        var s = new PawnRushNetState { Tick = 4242, HostTimeMs = 7654321, First = 2, Second = 0, PadFirst = 3, PadSecond = 1, PadsUsed = 2, Winner = 1, Clock = 87.25f,
                                       StationCount = 4 };
        s.PadChooser[1] = SkillWire.Ref(0, 3);
        s.PadChooser[2] = SkillWire.Ref(1, 1);
        for (int i = 0; i < 5; i++) { s.FinisherSeat[i] = SkillWire.Ref(i % 2, i); s.FinisherTime[i] = 60f + i * 1.7f; }
        s.FinisherCount = 5;
        for (int st = 0; st < s.StationCount; st++)
        {
            s.Game[st] = (sbyte)(st == 3 ? -1 : st);
            s.Progress[st] = st * 0.3f;
            s.Completed[st] = st == 1;
            s.BlockLength[st] = st * 9;
            for (int k = 0; k < s.BlockLength[st]; k++) s.Block[st][k] = (byte)(st * 50 + k * 7);
        }
        return s;
    }
    static string GoldenBytes()
    {
        var poses = new List<PoseWire>();
        for (int i = 0; i < 12; i++) poses.Add(WirePose(i + 1, (byte)(i % 6)));
        var motion = new List<PawnState>();
        for (int i = 0; i < 4; i++)
        {
            var m = PawnMotor.Spawn((ulong)(i + 11), i % 2, i);
            m.Ack = (uint)(i * 1000 + 7); m.X += i * 0.37f; m.Y = 1.5f + i; m.Vertical = -2.5f * i;
            motion.Add(m);
        }
        return string.Join(" ", new[]
        {
            Fnv(RagdollWire.Snapshot(77, 123456, 987654, 4321.25, poses, 12)),
            Fnv(RagdollWire.Snapshot(5, 1, 1, 0, poses, 3)),
            Fnv(MotionProtocol.Input(42, new MoveInput { Sequence = 10, X = 1, Z = -1, Jumps = 7 })),
            Fnv(MotionProtocol.Input(9, new MoveInput { Sequence = 4000000000u, X = -0.25f, Z = 0.75f, Jumps = 255 })),
            Fnv(MotionProtocol.Snapshot(42, 7, motion)),
            Fnv(GoldenRush().Write(42UL)),
            Fnv(new PawnRushNetState().Write(7UL)),
            Fnv(Through(w => SkillWire.Write(w, GoldenSkills()))),
            Fnv(Through(w => SkillWire.Write(w, new SkillWirePacket { HostTimeMs = 1, FirstSeq = 2 }))),
            Fnv(Through(w => SwordWire.Write(w, GoldenSword()))),
        });
    }
    // R538: the two course states (the sixth and the seventh) are CFP6's - a new magic and a seat count in the header.
    // R541: the two ragdoll snapshots (the first two) and the busy skills packet (the eighth) carry WirePoints - CFRE, x and
    // z in 24 bits; the empty skills packet and the sword state (the last two) have no such point and did not change.
    // The other five are the ones pinned at R297.
    const string Golden = "5692cf432cb34ea9 1bc69825b8c5743e a58dbecbc6b998b2 93eb863d4250712f 8286075e86dc6fe2 d8194986b34e5938 6aa7acd5e02fe864 "
                          + "3d8feeaec99034d7 0a5aaf2e798a59b6 52d23acd7ef29404";
    // The Royal Carriage Yard's four roaming carriages as RoyalCarriageYard and CarriageFleet build them (R286, R287):
    // RoamingFleet.Yard, the yard the seeds are vetted on; the map checks its own fleet against it.
    static RoamingFleet YardFleet(double start, uint seed) => RoamingFleet.Yard(seed, start);
    public static int Main()
    {
        try
        {
            Test("Pawn Rush draws two different mini-games and leaves out the previous round's", () => {
                var seen = new HashSet<int>();
                for (int seed = 0; seed < 400; seed++)
                {
                    var d = PawnRushMissions.Choose(seed);
                    Check(d.First != d.Second, "two different games");
                    Check(d.First >= 0 && d.First < PawnRushMissions.Count && d.Second >= 0 && d.Second < PawnRushMissions.Count, "in the pool");
                    var e = PawnRushMissions.Choose(seed + 1000, d.First, d.Second);
                    Check(e.First != d.First && e.First != d.Second && e.Second != d.First && e.Second != d.Second, "shuffle bag");
                    seen.Add(d.First); seen.Add(d.Second);
                }
                Check(seen.Count == PawnRushMissions.Count, "every game comes up");
                var a = PawnRushMissions.Choose(77, 1, 3); var b = PawnRushMissions.Choose(77, 1, 3);
                Check(a.First == b.First && a.Second == b.Second, "same seed, same draw"); });
            Test("Pawn Rush draw falls back to 'different games' when too few are enabled", () => {
                var on = new[] { true, false, true, false, false, false, false, false, false, false };
                for (int seed = 0; seed < 50; seed++)
                {
                    var d = PawnRushMissions.Choose(seed, 0, 2, on);
                    Check((d.First == 0 && d.Second == 2) || (d.First == 2 && d.Second == 0), "both enabled games, once each");
                }
                bool threw = false;
                try { PawnRushMissions.Choose(1, -1, -1, new[] { true, false, false, false, false }); } catch (ArgumentException) { threw = true; }
                Check(threw, "one game cannot fill two plazas"); });
            Test("Pawn Rush draw (v1.0 §2.5): never two games of the same group in a round, 1,000 seeds", () => {
                Check(PawnRushMissions.Group.Length == PawnRushMissions.Count && PawnRushMissions.Ids.Length == PawnRushMissions.Count
                      && PawnRushMissions.Names.Length == PawnRushMissions.Count && PawnRushMissions.Hints.Length == PawnRushMissions.Count, "tables");
                int last1 = -1, last2 = -1;
                for (int seed = 0; seed < 1000; seed++)
                {
                    var d = PawnRushMissions.Choose(seed, last1, last2);
                    Check(d.First != d.Second && PawnRushMissions.Group[d.First] != PawnRushMissions.Group[d.Second], "same group, seed " + seed);
                    Check(d.First != last1 && d.First != last2 && d.Second != last1 && d.Second != last2, "previous round, seed " + seed);
                    last1 = d.First; last2 = d.Second;
                }
                // Only pulling games on: the group rule gives way, two different games still.
                var pulls = new bool[PawnRushMissions.Count];
                pulls[PawnRushMissions.Drawbridge] = pulls[PawnRushMissions.Capstan] = true;
                var p = PawnRushMissions.Choose(5, PawnRushMissions.Drawbridge, PawnRushMissions.Capstan, pulls);
                Check(p.First != p.Second, "a short pool still gives two"); });
            Test("Pawn Rush mini-game progress keeps its quarter latches and caps workers at four", () => {
                Check(PawnRushMissions.Latched(.30, .20) == .25, "back to the latch only");
                Check(PawnRushMissions.Latched(.30, .27) == .27, "above the latch it goes back");
                Check(PawnRushMissions.Latched(.50, .10) == .50, "exactly on a latch stays");
                Check(PawnRushMissions.Latched(.10, .0) == .0 && PawnRushMissions.Latched(.9, 1.4) == 1.0, "clamped to 0..1");
                Check(PawnRushMissions.Latched(.4, double.NaN) == .4, "NaN ignored");
                Check(PawnRushMissions.Workers(6) == 4 && PawnRushMissions.Workers(3) == 3 && PawnRushMissions.Workers(-1) == 0, "four at most"); });
            Test("Section random is mulberry32: the blockout page's numbers, same seed same stream", () => {
                var r = new SeededRandom(1);
                Check(r.NextUInt() == 2693262067u && r.NextUInt() == 11749833u && r.NextUInt() == 2265367787u, "mulberry32(1)");
                var q = new SeededRandom(123456789);
                Check(q.NextUInt() == 1107202814u && q.NextUInt() == 4169434471u, "mulberry32(123456789)");
                var a = new SeededRandom(SeededSchedule.Hash(7, 2)); var b = new SeededRandom(SeededSchedule.Hash(7, 2));
                for (int i = 0; i < 50; i++) Check(a.Next(10) == b.Next(10), "same seed");
                Check(SeededSchedule.Hash(7, 2) != SeededSchedule.Hash(7, 3) && SeededSchedule.Hash(7, 2) != SeededSchedule.Hash(8, 2), "mixed");
                Check(PawnRushSections.SectionSeed(5, 2) != PawnRushSections.SectionSeed(5, 4), "slots differ"); });
            Test("Pawn Rush round: one prefab per section, shared games once a round, the previous pick left out", () => {
                Check(PawnRushSections.Kind.Length == PawnRushSections.SlotCount && PawnRushSections.SlotNames.Length == PawnRushSections.SlotCount, "tables");
                // Obstacle slots 1-x / 2-x / 3-x, the three mini-game slots share ten games, one final.
                var games = new[] { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109 };
                var options = new[] { new[] { 10, 11, 12 }, games, new[] { 20, 21 }, games, new[] { 30 }, games, new[] { 40 } };
                int[] last = null;
                var seenGames = new HashSet<int>();
                for (int seed = 0; seed < 500; seed++)
                {
                    var p = PawnRushSections.Choose(seed, options, last);
                    Check(p.Length == 7 && p[4] == 30 && p[6] == 40, "single options");
                    Check(p[1] != p[3] && p[1] != p[5] && p[3] != p[5], "three different games, seed " + seed);
                    for (int s = 0; s < 7; s++) Check(Array.IndexOf(options[s], p[s]) >= 0, "from its own list");
                    if (last != null) Check(p[0] != last[0] && p[2] != last[2] && p[1] != last[1], "previous round left out, seed " + seed);
                    seenGames.Add(p[1]); seenGames.Add(p[3]); seenGames.Add(p[5]);
                    last = p;
                }
                Check(seenGames.Count == 10, "every game comes up");
                var x = PawnRushSections.Choose(9, options); var y = PawnRushSections.Choose(9, options);
                for (int s = 0; s < 7; s++) Check(x[s] == y[s], "same seed, same round");
                var holes = PawnRushSections.Choose(3, new[] { new int[0], null, new[] { 5 }, new[] { 5 } });
                Check(holes[0] == -1 && holes[1] == -1 && holes[2] == 5 && holes[3] == 5, "empty slots and a one-prefab list"); });
            Test("Pawn Rush course table (R548): read as written, the round's own draw by name", () => {
                var rows = new List<PawnRushCourseTable.Row>
                {
                    new PawnRushCourseTable.Row(0, "A1", "왕실 수로 정원"), new PawnRushCourseTable.Row(0, "A2", "왕실 마차뜰"),
                    new PawnRushCourseTable.Row(1, "G1", "해자 도개교"), new PawnRushCourseTable.Row(1, "G2", "정문 쇠살문 누각"),
                    new PawnRushCourseTable.Row(1, "G3", "정문 의장 광장"), new PawnRushCourseTable.Row(2, "B1", "대계단 현관홀"),
                    new PawnRushCourseTable.Row(3, "G2", "정문 쇠살문 누각"), new PawnRushCourseTable.Row(3, "G4", "왕궁 서고"),
                    new PawnRushCourseTable.Row(4, "C1", "후반 장애물"), new PawnRushCourseTable.Row(5, "G1", "해자 도개교"),
                    new PawnRushCourseTable.Row(5, "G5", "국왕 침실"), new PawnRushCourseTable.Row(6, "F", "작전 회의실"),
                };
                // A byte order mark, Windows line ends, comments, a line that is not one, a slot out of range and a key
                // listed twice in its slot: none of them changes the table.
                string text = "﻿" + PawnRushCourseTable.Format(rows, "two lines\nof header").Replace("\n", "\r\n")
                            + "junk line\n9\tX\tout of range\n0\tA1\ttwice\n";
                var table = PawnRushCourseTable.Parse(text);
                Check(table.Count == rows.Count, "every row once: " + table.Count);
                Check(table.Names(0)[0] == "왕실 수로 정원" && table.Names(1).Count == 3 && table.Names(1)[1] == "정문 쇠살문 누각", "names in order");
                // PawnRushSectionSet.Options numbers the prefabs as they first appear, slot after slot: the same by hand.
                var options = new[] { new[] { 0, 1 }, new[] { 2, 3, 4 }, new[] { 5 }, new[] { 3, 6 }, new[] { 7 }, new[] { 2, 8 }, new[] { 9 } };
                for (int seed = -50; seed < 400; seed++)
                {
                    var ids = PawnRushSections.Choose(seed, options);
                    var picks = table.Picks(seed);
                    var shown = table.PickNames(seed);
                    for (int s = 0; s < PawnRushSections.SlotCount; s++)
                    {
                        int k = Array.IndexOf(options[s], ids[s]);
                        Check(picks[s] == k && shown[s] == table.Names(s)[k], "seed " + seed + " slot " + s);
                    }
                }
                var empty = PawnRushCourseTable.Parse("# nothing yet\n");
                Check(empty.Count == 0 && empty.PickNames(3).Length == PawnRushSections.SlotCount && empty.PickNames(3)[2] == "", "an empty table names nothing"); });
            Test("Grand stair hall rolls: ten minutes keep the three rules, use all four lanes, about 40% statues", () => {
                for (int seed = 1; seed <= 30; seed++)
                {
                    var s = new RollingSchedule(SeededSchedule.Hash(seed, 1), 1000.0);
                    s.Advance(1600.0);
                    Check(s.Count > 230 && s.Count < 380, "about one every 2.1 s: " + s.Count);
                    var lanes = new int[4];
                    int statues = 0;
                    for (int i = 0; i < s.Count; i++)
                    {
                        var e = s[i];
                        lanes[e.Lane]++;
                        if (e.Statue) statues++;
                        Check(e.Serial == i, "serials in order");
                        if (i > 0) Check(e.Time - s[i - 1].Time >= 1.6 - 1e-9 && e.Time - s[i - 1].Time <= 2.6 + 1e-9, "gap 1.6 ~ 2.6 s");
                        int inWindow = e.Statue ? 1 : 0;
                        for (int j = i - 1; j >= 0 && e.Time - s[j].Time < 4.0; j--)
                        {
                            var o = s[j];
                            if (e.Time - o.Time < 3.0) Check(o.Lane != e.Lane, "rule 1, seed " + seed);
                            if (e.Statue && o.Statue && e.Time - o.Time < 1.5) Check(Math.Abs(o.Lane - e.Lane) != 1, "rule 2, seed " + seed);
                            if (e.Statue && o.Statue) inWindow++;
                        }
                        Check(inWindow <= 2, "rule 3, seed " + seed);
                    }
                    for (int l = 0; l < 4; l++) Check(lanes[l] > 30, "lane " + l + " used");
                    double share = statues / (double)s.Count;
                    Check(share > .3 && share <= .41, "statue share " + share);
                    // Same seed, same list (every PC); a later Advance does not change what was written.
                    var t = new RollingSchedule(SeededSchedule.Hash(seed, 1), 1000.0);
                    t.Advance(1300.0); t.Advance(1600.0);
                    Check(t.Count == s.Count, "same count");
                    for (int i = 0; i < s.Count; i++) Check(t[i].Time == s[i].Time && t[i].Lane == s[i].Lane && t[i].Statue == s[i].Statue, "same list");
                }
                var u = new RollingSchedule(SeededSchedule.Hash(1, 1), 0.0); var v = new RollingSchedule(SeededSchedule.Hash(2, 1), 0.0);
                u.Advance(60); v.Advance(60);
                bool differs = false;
                for (int i = 0; i < Math.Min(u.Count, v.Count); i++) differs |= u[i].Lane != v[i].Lane || u[i].Statue != v[i].Statue;
                Check(differs, "another seed, another order");
                u.Prune(30.0);
                Check(u.Count > 0 && u[0].Time >= 30.0, "pruned"); });
            Test("Grand stair hall chandeliers: ten minutes keep 6 m apart, inside the landing, off the king's base", () => {
                for (int seed = 1; seed <= 30; seed++)
                {
                    var c = new ChandelierSchedule(SeededSchedule.Hash(seed, 2), 50.0);
                    c.Advance(650.0);
                    Check(c.Count > 380 && c.Count < 900, "about one a second: " + c.Count);
                    for (int i = 0; i < c.Count; i++)
                    {
                        var d = c[i];
                        Check(d.X >= -13 && d.X <= 13 && d.Z >= 54 && d.Z <= 65, "in the area");
                        Check(!(d.X > -2 && d.X < 2 && d.Z > 61 && d.Z < 65), "not on the king's base");
                        if (i > 0) Check(d.Time - c[i - 1].Time >= .7 - 1e-9, "at least 0.7 s apart");
                        for (int j = i - 1; j >= 0 && d.Time - c[j].Time < 2.2; j--)
                        {
                            double dx = d.X - c[j].X, dz = d.Z - c[j].Z;
                            Check(dx * dx + dz * dz >= 36 - 1e-9, "6 m from a drop still warning or just fallen, seed " + seed);
                        }
                    }
                }
                var a = new ChandelierSchedule(9, 0); var b = new ChandelierSchedule(9, 0);
                a.Advance(30); b.Advance(10); b.Advance(30);
                Check(a.Count == b.Count, "same count");
                for (int i = 0; i < a.Count; i++) Check(a[i].X == b[i].X && a[i].Z == b[i].Z && a[i].Time == b[i].Time, "same drops"); });
            Test("Grand stair hall wings: one for 3 and 4 a side (either, by the seed), both from 5", () => {
                Check(!GrandStairHallRules.BothWings(3) && !GrandStairHallRules.BothWings(4), "one wing");
                Check(GrandStairHallRules.BothWings(5) && GrandStairHallRules.BothWings(6), "two wings");
                var seen = new HashSet<int>();
                for (int seed = 0; seed < 64; seed++)
                {
                    int open = GrandStairHallRules.OpenWing(seed);
                    seen.Add(open);
                    Check(GrandStairHallRules.WingOpen(open, 3, seed) && !GrandStairHallRules.WingOpen(1 - open, 3, seed), "the other is shut");
                    Check(GrandStairHallRules.WingOpen(0, 6, seed) && GrandStairHallRules.WingOpen(1, 6, seed), "six a side: both");
                }
                Check(seen.Count == 2, "west and east both come up"); });
            Test("Royal water garden boats: lanes from the seed, both ways, 24 m apart, sunk while they wrap, a wrap a cut (R531)", () => {
                var speeds = new HashSet<double>(RoyalWaterGardenRules.Speeds);
                var dirs = new int[2];
                for (int seed = 1; seed <= 500; seed++)
                {
                    var lanes = RoyalWaterGardenRules.RollBoatLanes(seed);
                    Check(lanes.Length == RoyalWaterGardenRules.Lanes, "four lanes");
                    bool east = false, west = false;
                    foreach (var l in lanes)
                    {
                        Check(l.Dir == 1 || l.Dir == -1, "a way");
                        Check(speeds.Contains(l.Speed), "a listed speed");
                        Check(l.Offset >= 0 && l.Offset < RoyalWaterGardenRules.Spacing, "offset inside one spacing");
                        east |= l.Dir > 0; west |= l.Dir < 0;
                        dirs[l.Dir > 0 ? 0 : 1]++;
                    }
                    Check(east && west, "both ways, seed " + seed);
                    var again = RoyalWaterGardenRules.RollBoatLanes(seed);
                    for (int i = 0; i < lanes.Length; i++)
                        Check(again[i].Dir == lanes[i].Dir && again[i].Speed == lanes[i].Speed && again[i].Offset == lanes[i].Offset, "same seed, same lanes");
                }
                Check(dirs[0] > 600 && dirs[1] > 600, "east and west about as often: " + dirs[0] + " / " + dirs[1]);
                Check(RoyalWaterGardenRules.WaterBetween == 18.0, "18 m of water between the boats of a lane");
                // Along a lane: inside the loop, the two boats a spacing apart, moving at the lane's speed, and sunk
                // whenever one wraps from one end to the other. Scene time and Unix time alike.
                foreach (double start in new[] { 0.0, 1.7e9 })
                    for (int seed = 1; seed <= 20; seed++)
                        foreach (var l in RoyalWaterGardenRules.RollBoatLanes(seed))
                        {
                            double dt = .01, half = RoyalWaterGardenRules.Loop * .5;
                            double[] before = { RoyalWaterGardenRules.BoatX(l, 0, start), RoyalWaterGardenRules.BoatX(l, 1, start) };
                            for (double t = start + dt; t < start + 60; t += dt)
                                for (int k = 0; k < 2; k++)
                                {
                                    double x = RoyalWaterGardenRules.BoatX(l, k, t);
                                    Check(x >= -half - 1e-6 && x < half + 1e-6, "inside the loop");
                                    double step = x - before[k];
                                    if (Math.Abs(step) > 1)
                                        Check(RoyalWaterGardenRules.BoatDrop(x) == RoyalWaterGardenRules.DiveDepth
                                              && RoyalWaterGardenRules.BoatDrop(before[k]) == RoyalWaterGardenRules.DiveDepth, "wraps only when sunk");
                                    else Check(Math.Abs(step - l.Dir * l.Speed * dt) < 1e-3, "lane speed, way");
                                    // R531: the wrap is told as a cut (GardenBoat puts the body there), a step never is.
                                    Check(RoyalWaterGardenRules.BoatWraps(l, k, t - dt, t) == (Math.Abs(step) > 1), "a wrap and only a wrap is a cut");
                                    before[k] = x;
                                    if (k == 1)
                                    {
                                        double apart = Math.Abs(before[1] - before[0]);
                                        apart = Math.Min(apart, RoyalWaterGardenRules.Loop - apart);
                                        Check(Math.Abs(apart - RoyalWaterGardenRules.Spacing) < 1e-3, "a spacing apart");
                                    }
                                }
                        }
                Check(RoyalWaterGardenRules.BoatDrop(0) == 0 && RoyalWaterGardenRules.BoatDrop(-20.5) == 0, "afloat in sight");
                Check(RoyalWaterGardenRules.BoatDrop(21.7) > 0 && RoyalWaterGardenRules.BoatDrop(21.7) < RoyalWaterGardenRules.DiveDepth, "sinking under the tunnel");
                bool differs = false;
                var one = RoyalWaterGardenRules.RollBoatLanes(1); var two = RoyalWaterGardenRules.RollBoatLanes(2);
                for (int i = 0; i < one.Length; i++) differs |= one[i].Dir != two[i].Dir || one[i].Speed != two[i].Speed;
                Check(differs, "another seed, other lanes"); });
            Test("Royal water garden geysers: one after another, 0.8 s bubbling then 1 s jet every 5 s", () => {
                double into;
                for (int k = 0; k < RoyalWaterGardenRules.Geysers; k++)
                    foreach (double cycle in new[] { 0.0, 5.0, 1.7e9 })
                    {
                        double t0 = cycle + k * RoyalWaterGardenRules.GeyserStep;
                        Check(RoyalWaterGardenRules.GeyserState(k, t0 + .01, out into) == RoyalWaterGardenRules.Jet.Warn && Math.Abs(into - .01) < 1e-4, "bubbling first, geyser " + k);
                        Check(RoyalWaterGardenRules.GeyserState(k, t0 + .79, out into) == RoyalWaterGardenRules.Jet.Warn, "0.8 s of bubbling");
                        Check(RoyalWaterGardenRules.GeyserState(k, t0 + .81, out into) == RoyalWaterGardenRules.Jet.Jet, "then the jet");
                        Check(RoyalWaterGardenRules.GeyserState(k, t0 + 1.79, out into) == RoyalWaterGardenRules.Jet.Jet, "1 s of jet");
                        Check(RoyalWaterGardenRules.GeyserState(k, t0 + 1.81, out into) == RoyalWaterGardenRules.Jet.Rest, "then rest");
                        Check(RoyalWaterGardenRules.GeyserState(k, t0 - .01, out into) == RoyalWaterGardenRules.Jet.Rest, "rest before");
                        Check(RoyalWaterGardenRules.GeyserCycleIndex(k, t0 + 5.01) == RoyalWaterGardenRules.GeyserCycleIndex(k, t0 + .01) + 1, "next eruption");
                    }
                // At any moment at most two jets are up, so there is always room between them.
                for (double t = 0; t < 20; t += .01)
                {
                    int up = 0;
                    for (int k = 0; k < RoyalWaterGardenRules.Geysers; k++) if (RoyalWaterGardenRules.GeyserState(k, t, out into) == RoyalWaterGardenRules.Jet.Jet) up++;
                    Check(up <= 2, "at most two jets at once: " + up);
                } });
            Test("Royal water garden robots: rest, wind up, sweep, hold over the water, back; low only while crossing", () => {
                double into, last = RoyalWaterGardenRules.RobotAngle(0);
                for (double t = .001; t < 12; t += .001)
                {
                    double a = RoyalWaterGardenRules.RobotAngle(t);
                    Check(a >= RoyalWaterGardenRules.RobotDown - 1e-9 && a <= RoyalWaterGardenRules.RobotWind + 1e-9, "inside its swing");
                    Check(Math.Abs(a - last) < .5, "no jump at " + t);
                    var phase = RoyalWaterGardenRules.RobotPhase(t, out into);
                    if (Math.Abs(a) < 30) Check(phase == RoyalWaterGardenRules.Swing.Sweep || phase == RoyalWaterGardenRules.Swing.Back, "low only while crossing");
                    last = a;
                }
                Check(RoyalWaterGardenRules.RobotAngle(2.2) == RoyalWaterGardenRules.RobotDown, "holds over the water");
                Check(RoyalWaterGardenRules.RobotAngle(5.0) == RoyalWaterGardenRules.RobotRest, "rests raised");
                Check(RoyalWaterGardenRules.RobotPhase(6.0 * 283333333 + 1.5, out into) == RoyalWaterGardenRules.Swing.Sweep, "Unix time"); });
            Test("Royal Carriage Yard roaming fleet: four carriages share the yard, never meet, stay in it, never jump, roam all of it, one plan however it is advanced", () => {
                foreach (double origin in new[] { 3.0, 1762000003.25 })
                {
                    var fleet = YardFleet(origin, RoamingFleet.YardSeeds[0]);
                    var r = fleet.Settings;
                    double spacing = fleet.Spacing;
                    const double dt = 1.0 / 60.0;
                    int n = fleet.Count;
                    Check(n == 4, "four carriages");
                    var before = new RoamingFleet.Pose[n];
                    for (int i = 0; i < n; i++) before[i] = fleet.At(i, origin - 1);
                    var reached = new bool[n, 4];   // within 3 m of the west, east, south, north edge of the centres' area
                    double last = origin;
                    for (double s = -1 + dt; s < 600; s += dt)
                    {
                        last = origin + s;
                        var now = new RoamingFleet.Pose[n];
                        for (int i = 0; i < n; i++)
                        {
                            var p = fleet.At(i, last);
                            now[i] = p;
                            Check(p.X >= r.MinX - 1e-6 && p.X <= r.MaxX + 1e-6 && p.Z >= r.MinZ - 1e-6 && p.Z <= r.MaxZ + 1e-6, "inside the yard");
                            var b = before[i];
                            double moved = Math.Sqrt((p.X - b.X) * (p.X - b.X) + (p.Z - b.Z) * (p.Z - b.Z));
                            Check(moved <= (9 + .3 * i) * dt * 1.02 + 1e-6, "no jump: " + moved.ToString("0.000") + " m at " + s.ToString("0.00"));
                            Check(p.Speed <= 9 + .3 * i + 1e-6, "never over its top speed");
                            double turned = Math.Abs(RoamingFleet.Delta(b.Yaw, p.Yaw));
                            if (p.Stage != RoamingFleet.Stage.Turning && b.Stage != RoamingFleet.Stage.Turning)
                                Check(turned < 1e-6, "turns only while turning");
                            else Check(turned <= 180.0 * 1.5 / r.Turn * dt + 1e-6, "turns smoothly");
                            if (p.Stage != RoamingFleet.Stage.Charging && b.Stage != RoamingFleet.Stage.Charging)
                                Check(moved < 1e-9, "stands still off the charge");
                            if (p.X < r.MinX + 3) reached[i, 0] = true;
                            if (p.X > r.MaxX - 3) reached[i, 1] = true;
                            if (p.Z < r.MinZ + 3) reached[i, 2] = true;
                            if (p.Z > r.MaxZ - 3) reached[i, 3] = true;
                        }
                        for (int i = 0; i < n; i++)
                            for (int j = i + 1; j < n; j++)
                            {
                                double d = Math.Sqrt((now[i].X - now[j].X) * (now[i].X - now[j].X) + (now[i].Z - now[j].Z) * (now[i].Z - now[j].Z));
                                Check(d >= spacing - 1e-6, "carriages " + i + " and " + j + " never meet: " + d.ToString("0.00") + " m at " + s.ToString("0.00"));
                            }
                        before = now;
                    }
                    for (int i = 0; i < n; i++)
                        for (int e = 0; e < 4; e++)
                            Check(reached[i, e], "carriage " + i + " comes within 3 m of side " + e + " of its area: it roams the whole yard");
                    // Every leg is a real charge, or now and then a short wait when nothing is free.
                    int charges = 0, waits = 0;
                    for (int i = 0; i < n; i++)
                        for (int k = 0; k < fleet.LegCount(i); k++)
                        {
                            var l = fleet.LegAt(i, k);
                            double d = Math.Sqrt((l.ToX - l.FromX) * (l.ToX - l.FromX) + (l.ToZ - l.FromZ) * (l.ToZ - l.FromZ));
                            if (l.Charge > 0)
                            {
                                charges++;
                                Check(d >= r.MinCharge - 1e-6 && d <= r.MaxCharge + 1e-6, "charge length " + d.ToString("0.0"));
                            }
                            else waits++;
                        }
                    Check(charges > 200 && waits * 10 < charges, "mostly charging: " + charges + " charges, " + waits + " waits");
                    // One plan however the clock moves on: in one jump instead of step by step.
                    var whole = YardFleet(origin, RoamingFleet.YardSeeds[0]);
                    whole.Advance(last);
                    for (int i = 0; i < n; i++)
                    {
                        Check(whole.LegCount(i) == fleet.LegCount(i), "same number of legs");
                        for (int k = 0; k < whole.LegCount(i); k++)
                        {
                            var a = whole.LegAt(i, k);
                            var b = fleet.LegAt(i, k);
                            Check(a.Start == b.Start && a.ToX == b.ToX && a.ToZ == b.ToZ && a.Aim == b.Aim && a.Rest == b.Rest, "same legs");
                        }
                    }
                    var other = YardFleet(origin, RoamingFleet.YardSeeds[1]);
                    other.Advance(origin + 30);
                    Check(other.LegAt(0, 0).ToX != fleet.LegAt(0, 0).ToX, "another seed, another plan");
                }
                // The segment distance the plan keeps carriages apart with.
                Check(Math.Abs(RoamingFleet.SegmentDistance(0, 0, 10, 0, 5, -3, 5, 3)) < 1e-9, "crossing segments touch");
                Check(Math.Abs(RoamingFleet.SegmentDistance(0, 0, 10, 0, 12, 0, 20, 0) - 2) < 1e-9, "end to end");
                Check(Math.Abs(RoamingFleet.SegmentDistance(0, 0, 10, 0, 4, 3, 4, 3) - 3) < 1e-9, "point over a segment"); });
            Test("Royal Carriage Yard roaming fleet: a clock leaping to the match clock or back starts the plan again there", () => {
                var fleet = YardFleet(3.0, 101);
                for (double t = 3.0; t < 63.0; t += .5) fleet.At(0, t);   // a minute on scene time
                foreach (double leap in new[] { 1762000100.0, 200.0 })   // to the match clock, then back
                {
                    var fresh = YardFleet(leap, 101);
                    for (double s = 0; s < 120; s += .25)
                        for (int i = 0; i < 4; i++)
                        {
                            var a = fleet.At(i, leap + s);
                            var b = fresh.At(i, leap + s);
                            Check(a.X == b.X && a.Z == b.Z && a.Yaw == b.Yaw && a.Stage == b.Stage, "as a fleet started at the leap");
                        }
                    Check(fleet.StartTime == leap, "starts again at the leap");
                }
                // Under an hour is no leap: planned through, the same plan.
                var gap = YardFleet(3.0, 101);
                gap.At(0, 10.0);
                var p = gap.At(2, 3000.0);
                var q = YardFleet(3.0, 101).At(2, 3000.0);
                Check(gap.StartTime == 3.0 && p.X == q.X && p.Z == q.Z && p.Yaw == q.Yaw, "a 50 minute gap is planned through"); });
            Test("Royal Carriage Yard roaming fleet: every vetted seed dances well, a match draws them all, a turn takes its share of a half turn", () => {
                // Each seed's first leg end per carriage, as vetted: a change to the yard's numbers lands here first.
                var firstLegs = new Dictionary<uint, double[]>
                {
                    { 38, new[] { -16.321, 38.928, -2.125, 14.221, 6.854, 25.727, 16.157, 25.131 } },
                    { 41, new[] { 13.202, 27.596, -7.393, 9.162, 3.671, 45.379, -7.809, 31.444 } },
                    { 160, new[] { 15.929, 27.580, 2.979, 8.514, 3.319, 38.004, 9.403, 37.028 } },
                    { 44, new[] { -15.679, 39.021, -8.416, 8.170, .619, 27.839, -5.639, 46.785 } },
                    { 18, new[] { -3.830, 31.294, 11.426, 28.510, 3.925, 42.585, 1.663, 23.373 } },
                    { 252, new[] { 1.483, 30.537, 17.563, 38.619, 7.423, 42.731, .910, 11.091 } },
                    { 20, new[] { 3.671, 18.924, 14.004, 28.901, -4.335, 26.815, 14.970, 45.768 } },
                    { 113, new[] { -.181, 36.691, 12.110, 28.487, -4.218, 45.895, 2.736, 26.924 } },
                };
                Check(firstLegs.Count == RoamingFleet.YardSeeds.Length, "every listed seed is pinned");
                foreach (uint seed in RoamingFleet.YardSeeds)
                {
                    const double start = 3.0, dt = 1.0 / 30.0, match = 1762000003.37;
                    var fleet = YardFleet(start, seed);
                    var late = YardFleet(match, seed);
                    var r = fleet.Settings;
                    int n = fleet.Count;
                    var reached = new bool[n, 4];
                    var charged = new bool[n];
                    var still = new double[n];
                    double charging = 0, middle = 0, total = 0, west = 0, east = 0, longest = 0, closest = double.MaxValue;
                    var p = new RoamingFleet.Pose[n];
                    for (double s = 0; s < 600; s += dt)
                    {
                        bool any = false, overMiddle = false;
                        for (int i = 0; i < n; i++)
                        {
                            p[i] = fleet.At(i, start + s);
                            if (p[i].Stage == RoamingFleet.Stage.Charging)
                            {
                                any = true;
                                charged[i] = true;
                                still[i] = 0;
                                // A body over the straight way up the middle, |x| <= 3.
                                if (Math.Abs(p[i].X) < 3 + r.BodyRadius) overMiddle = true;
                                // The first 20 s near the gate: which half of the yard the charges are in.
                                if (s < 20 && p[i].Z < r.MinZ + 20) { if (p[i].X < 0) west += dt; else east += dt; }
                            }
                            else if (charged[i]) longest = Math.Max(longest, still[i] += dt);
                            if (p[i].X < r.MinX + 3) reached[i, 0] = true;
                            if (p[i].X > r.MaxX - 3) reached[i, 1] = true;
                            if (p[i].Z < r.MinZ + 3) reached[i, 2] = true;
                            if (p[i].Z > r.MaxZ - 3) reached[i, 3] = true;
                        }
                        for (int i = 0; i < n; i++)
                            for (int j = i + 1; j < n; j++)
                                closest = Math.Min(closest, Math.Sqrt((p[i].X - p[j].X) * (p[i].X - p[j].X) + (p[i].Z - p[j].Z) * (p[i].Z - p[j].Z)));
                        total += dt;
                        if (any) charging += dt;
                        if (overMiddle) middle += dt;
                    }
                    int charges = 0, waits = 0;
                    for (int i = 0; i < n; i++)
                        for (int k = 0; k < fleet.LegCount(i); k++)
                            if (fleet.LegAt(i, k).Charge > 0) charges++; else waits++;
                    // The same legs, the same times after the start, on the match clock.
                    late.Advance(match + 600);
                    bool same = true;
                    for (int i = 0; i < n; i++)
                    {
                        if (late.LegCount(i) < fleet.LegCount(i) - 1) { same = false; continue; }
                        for (int k = 0; k < fleet.LegCount(i) - 1; k++)
                        {
                            var a = fleet.LegAt(i, k);
                            var b = late.LegAt(i, k);
                            if (a.ToX != b.ToX || a.ToZ != b.ToZ || Math.Abs((a.Start - start) - (b.Start - match)) > 1e-5) same = false;
                        }
                    }
                    string name = "seed " + seed + ": ";
                    Check(charging / total >= .85, name + "someone charging " + (charging / total).ToString("0.000") + " of the time");
                    Check(middle / total >= .3, name + "a charge over the middle way " + (middle / total).ToString("0.000") + " of the time");
                    Check(waits < .04 * (charges + waits), name + waits + " waits in " + (charges + waits) + " legs");
                    Check(longest <= 5.5, name + "a carriage stood " + longest.ToString("0.0") + " s after its first charge");
                    Check(closest >= fleet.Spacing - 1e-6, name + "two carriages came " + closest.ToString("0.000") + " m apart");
                    Check(same, name + "the same dance on the match clock");
                    for (int i = 0; i < n; i++)
                        for (int e = 0; e < 4; e++)
                            Check(reached[i, e], name + "carriage " + i + " reaches side " + e);
                    Check(west + east > 0 && Math.Min(west, east) >= .4 * (west + east), name + "the gate's halves get " + west.ToString("0.0") + " / " + east.ToString("0.0") + " s");
                    var pin = firstLegs[seed];
                    for (int i = 0; i < n; i++)
                    {
                        var l = fleet.LegAt(i, 0);
                        Check(Math.Abs(l.ToX - pin[2 * i]) < 2e-3 && Math.Abs(l.ToZ - pin[2 * i + 1]) < 2e-3,
                              name + "carriage " + i + "'s first leg ends at " + l.ToX.ToString("0.000") + ", " + l.ToZ.ToString("0.000") + " as vetted");
                    }
                }
                // The map's fleet as CarriageFleet.Plan builds it from the Inspector, in floats, matches the vetted yard;
                // another area does not.
                Func<float, RoamingFleet> mapFleet = margin =>
                {
                    float reach = .5f * (float)Math.Sqrt(4.2f * 4.2f + 2.3f * 2.3f), keep = reach + margin;
                    var rules = new RoamingFleet.Rules
                    {
                        MinX = -(22f - keep), MaxX = 22f - keep, MinZ = 2f + keep + 2.5f, MaxZ = 50f - keep, BodyRadius = reach, Turn = 2.2f,
                        MinAim = .6f, MaxAim = 1.2f, MinRest = .4f, MaxRest = 1.4f, MinCharge = 10f, MaxCharge = 32f,
                    };
                    var speeds = new double[4]; var hx = new double[4]; var hz = new double[4]; var yaw = new double[4];
                    for (int i = 0; i < 4; i++)
                    {
                        speeds[i] = 9f + .3f * i;
                        double side = i % 2 == 0 ? -1 : 1;
                        hx[i] = (rules.MinX + rules.MaxX) * .5 + side * (rules.MaxX - rules.MinX) * .25;
                        hz[i] = rules.MinZ + (i / 2 + .5) * (rules.MaxZ - rules.MinZ) / 2;
                        yaw[i] = side < 0 ? 90 : -90;
                    }
                    return new RoamingFleet(38, 3, speeds, hx, hz, yaw, rules);
                };
                Check(mapFleet(.8f).Matches(RoamingFleet.Yard(38, 3)) && RoamingFleet.Yard(41, 9).Matches(RoamingFleet.Yard(38, 3)), "the map's yard is the vetted one");
                Check(!mapFleet(.5f).Matches(RoamingFleet.Yard(38, 3)), "another area is not");
                // Held (no start yet): every carriage at home, nothing planned, until a fleet with a start replaces it.
                var held = new RoamingFleet(12, double.NaN, new double[] { 9, 9.3 }, new double[] { -5, 5 }, new double[] { 20, 30 }, new double[] { 90, -90 },
                                            new RoamingFleet.Rules());
                var home = held.At(1, 1762000100.0);
                Check(held.Held && home.Stage == RoamingFleet.Stage.Waiting && home.X == 5 && home.Z == 30 && home.Yaw == -90
                      && double.IsNaN(home.LegStart) && held.LegCount(1) == 0, "held at home on any clock");
                // A match draws every listed seed for some round seed, and only listed ones.
                var drawn = new HashSet<uint>();
                for (int round = 0; round < 400; round++)
                {
                    uint s = RoamingFleet.YardSeed(round);
                    Check(Array.IndexOf(RoamingFleet.YardSeeds, s) >= 0, "a listed seed");
                    drawn.Add(s);
                }
                Check(drawn.Count == RoamingFleet.YardSeeds.Length, "every listed seed comes up");
                // A half turn takes Turn, a quarter its share, a nudge MinTurn; every leg turns so.
                var f = YardFleet(3.0, RoamingFleet.YardSeeds[0]);
                Check(Math.Abs(f.TurnSeconds(0, 180) - 2.2) < 1e-9 && Math.Abs(f.TurnSeconds(-45, 45) - 1.1) < 1e-9
                      && Math.Abs(f.TurnSeconds(150, -150) - 2.2 * 60 / 180) < 1e-9 && Math.Abs(f.TurnSeconds(170, -170) - .4) < 1e-9
                      && Math.Abs(f.TurnSeconds(0, 5) - .4) < 1e-9, "turn times: the short way round, never under MinTurn");
                f.Advance(120);
                int legAt = -1;
                for (int k = 0; k < f.LegCount(1); k++)
                {
                    var l = f.LegAt(1, k);
                    if (l.Charge <= 0) continue;
                    Check(Math.Abs(l.Turn - f.TurnSeconds(l.HeadingFrom, l.Heading)) < 1e-9, "each charge's turn is its share of a half turn");
                    Check(l.Turn + l.Aim >= f.Settings.MinLead - 1e-9 && l.Aim >= f.Settings.MinAim - 1e-9, "each charge shows its line MinLead ahead");
                    if (legAt < 0 && k > 0) legAt = k;
                }
                // A pose names its leg: what a hit counts by and where the charge line ends.
                var leg = f.LegAt(1, legAt);
                var mid = f.At(1, leg.ChargeStart + leg.Charge * .5);
                Check(mid.Stage == RoamingFleet.Stage.Charging && mid.LegStart == leg.Start && mid.ToX == leg.ToX && mid.ToZ == leg.ToZ, "a pose names its leg");
                var aim = f.At(1, leg.ChargeStart - 1e-3);
                Check(aim.LegStart == leg.Start && aim.X == leg.FromX && aim.Z == leg.FromZ, "aiming, it is on the same leg, where it starts");
                Check(double.IsNaN(YardFleet(3.0, 12).At(3, 2.0).LegStart), "no leg before the start"); });
            Test("King Rush final starts with both kings or exactly first plus twenty-five", () => {
                var r = new KingRushFinalRules(); r.Advance(1, 0, 0, 0); Check(!r.Started, "no kings");
                r.ArriveKing(0, 2); r.Advance(26.999, 1, 0, 0); Check(!r.Started && r.Progress(0) == 0, "not early");
                r.Advance(27, 1, 0, 0); Check(r.StartedAt == 27 && r.Progress(0) == 0, "exact timeout");
                var b = new KingRushFinalRules(); b.ArriveKing(0, 3); b.ArriveKing(1, 7); b.Advance(7, 0, 0, 0); Check(b.StartedAt == 7, "both"); });
            Test("King Rush final accumulates eight unblocked seconds without decay or double seats", () => {
                var r = new KingRushFinalRules(); r.ArriveKing(0, 0); r.ArriveKing(1, 0); r.Advance(0, 0, 0, 0);
                r.Advance(2, 1, 0, 0); Check(r.Progress(0) == .25, "two seconds");
                r.Advance(4, 3, 3, 0); Check(r.Progress(0) == .25 && r.Seated == 0 && r.Progress(1) == 0, "contested single seat");
                r.Advance(5, 0, 0, 0); r.Advance(6, 2, 0, 0); Check(r.Progress(1) == .125 && r.Progress(0) == .25, "takeover retains progress");
                r.Advance(12, 1, 0, 0); Check(r.Winner == 0, "eight total wins"); r.Advance(200, 2, 0, 1); Check(r.Winner == 0, "immutable result"); });
            Test("King Rush final doubles only time after 140 and resolves king falls atomically", () => {
                var r = new KingRushFinalRules(); r.ArriveKing(0, 0); r.ArriveKing(1, 0); r.Advance(139, 0, 0, 1);
                Check(!r.Ended, "early fall respawns"); r.Advance(141, 1, 0, 0); Check(Math.Abs(r.Progress(0) - .375) < 1e-8, "one normal plus one doubled second");
                r.Advance(141.1, 0, 0, 1); Check(r.Winner == 1, "white king falls");
                var b = new KingRushFinalRules(); b.ArriveKing(0, 0); b.ArriveKing(1, 0); b.Advance(140, 0, 0, 3); Check(b.Winner == -1, "simultaneous draw"); });
            Test("King Rush final timeout compares retained gauges and rejects invalid clocks", () => {
                var r = new KingRushFinalRules(); r.ArriveKing(0, 0); r.ArriveKing(1, 0); r.Advance(0, 0, 0, 0); r.Advance(1, 2, 0, 0);
                r.Advance(double.NaN, 1, 0, 0); r.Advance(.5, 1, 0, 0); r.Advance(double.PositiveInfinity, 1, 0, 0);
                Check(r.Progress(0) == 0, "invalid times ignored"); r.Advance(180, 0, 0, 0); Check(r.Winner == 1, "larger gauge");
                var b = new KingRushFinalRules(); b.ArriveKing(0, 0); b.ArriveKing(1, 0); b.Advance(180, 0, 0, 0); Check(b.Winner == -1, "timeout tie"); });
            Test("King Rush final collapse leaves thirty black cells then six then dais only", () => {
                int at50 = 0, at100 = 0, at140 = 0;
                for (int x = 0; x < 8; x++) for (int z = 0; z < 8; z++) { double drop = KingRushFinalRules.TileFallsAt(x, z); if (drop > 50) at50++; if (drop > 100) at100++; if (drop > 140) at140++; }
                Check(at50 == 34 && at100 == 10 && at140 == 4, "central four permanent cells are included"); });
            Test("King Rush last rally needs all four final promotions", () => {
                var g = new KingRushRules(); var r = new KingRushRallyRules(2);
                for (int i = 5; i < 8; i++) g.TryClaim(i, (ulong)i, i % 2);
                r.Advance(g, 4); Check(r.StartedAt < 0 && r.Claimed == 3, "three insufficient"); g.TryClaim(8, 8, 0); r.Advance(g, 5);
                Check(!r.Ready(14.999) && r.Release(15), "four then ten seconds"); });
            Test("King Rush rally waits for every slot and releases once after exactly ten seconds", () => {
                var g = new KingRushRules(); var r = new KingRushRallyRules(0);
                g.TryClaim(0, 1, 0); r.Advance(g, 20); Check(r.StartedAt < 0 && !r.Ready(500), "partial claims cannot start");
                g.TryClaim(1, 2, 1); r.Advance(g, 22); r.Advance(g, 25);
                Check(r.StartedAt == 22 && !r.Ready(31.999) && !r.Release(31.999), "ten full seconds");
                Check(r.Ready(32) && !r.Released && r.Release(32) && !r.Release(33), "gather before one-shot release");
                Check(!new KingRushRallyRules(0).Released, "round reset"); });
            Test("King Rush rally waves are independent and reject invalid clocks", () => {
                var g = new KingRushRules(); var r = new KingRushRallyRules(1);
                g.TryClaim(0, 1, 0); g.TryClaim(1, 2, 0); g.TryClaim(2, 1, 0); g.TryClaim(3, 2, 1);
                r.Advance(g, 20); Check(r.Claimed == 2 && r.StartedAt < 0, "second wave needs three");
                g.TryClaim(4, 3, 1); r.Advance(g, double.NaN); r.Advance(g, 19); Check(r.StartedAt < 0, "invalid and stale");
                r.Advance(g, 21); Check(r.Required == 3 && r.Ready(31) && !r.Ready(double.PositiveInfinity), "valid clock"); });
            Test("King Rush seesaw clamps angle, limits speed and uses exit hysteresis", () => {
                var r = new KingRushSeesawRules(new KingRushRules()); r.Advance(100, 90); Check(r.Angle == 0, "no early movement");
                r.Begin(0); r.Advance(1, 90); Check(r.Angle == 8 && !r.Access(0), "speed limit");
                r.Advance(2, 90); Check(r.Angle == 16 && r.Access(0) && !r.Access(1), "own side");
                r.Advance(3, 33.6); Check(Math.Abs(r.Angle - 14) < .001 && r.Access(0), "hysteresis");
                r.Advance(4, 24); Check(!r.Access(0), "closes below twelve");
                r.Advance(20, -100); Check(r.Angle == -25 && r.Access(1), "opposite clamp"); });
            Test("King Rush seesaw needs four unique exits and opens fallback after twenty", () => {
                var g = new KingRushRules(); var r = new KingRushSeesawRules(g); r.Begin(5);
                for (ulong i = 1; i <= 3; i++) Check(r.Cross(i, 0, 6), "first three");
                Check(!g.IsOpen(1, 0, 6) && !r.Cross(1, 0, 7) && r.Count(0) == 3, "duplicates");
                Check(r.Cross(4, 0, 8) && g.IsOpen(1, 0, 8) && !r.Bridges(27.99), "four completes");
                Check(r.Bridges(28) && g.IsOpen(1, 1, 28), "fallback"); });
            Test("King Rush seesaw overtime lowers threshold and deploys both bridges", () => {
                var r = new KingRushSeesawRules(new KingRushRules()); r.Begin(10); r.Advance(129, 26.4);
                Check(!r.Access(0), "eleven degrees initially closed"); r.Advance(130, 26.4); Check(r.Access(0), "ten degree overtime");
                r.Advance(159.9, 0); Check(!r.Bridges(159.9) && !r.Access(0), "overtime keeps hysteresis gap");
                r.Advance(160, 0); Check(r.Bridges(160) && r.Access(0) && r.Access(1), "time fallback");
                r.Advance(double.NaN, 90); Check(r.Angle == 0, "bad time ignored"); });
            Test("King Rush capture requires entry and reserves each body until release", () => {
                var g = new KingRushRules(); var r = new KingRushCaptureRules(g);
                Check(!r.Deposit(1, 1, 0, 0), "before mission"); r.Begin(10); r.Begin(20);
                Check(r.StartedAt == 10 && r.Deposit(1, 1, 0, 11), "begin once");
                Check(r.Planks(0) == 1 && r.ReleaseAt(1) == 14 && !r.Deposit(1, 1, 0, 15), "dedupe does not expire in pit");
                r.Release(1); Check(r.Deposit(1, 1, 0, 15) && r.Planks(0) == 2, "new capture after real respawn"); });
            Test("King Rush allies return after two seconds without scoring", () => {
                var r = new KingRushCaptureRules(new KingRushRules()); r.Begin(0);
                Check(r.Deposit(1, 0, 0, 1) && r.Planks(0) == 0 && r.Captures(0) == 0 && r.ReleaseAt(1) == 3, "ally");
                Check(!r.Deposit(0, 1, 0, 1) && !r.Deposit(2, 2, 0, 1) && !r.Deposit(2, 1, -1, 1) &&
                    !r.Deposit(2, 1, 0, double.NaN) && !r.Deposit(2, 1, 0, .5), "invalid input"); });
            Test("King Rush six captures finish bridge and fallback supplies missing planks", () => {
                var g = new KingRushRules(); var r = new KingRushCaptureRules(g); r.Begin(0);
                for (ulong id = 1; id <= 6; id++) Check(r.Deposit(id, 1, 0, id), "capture");
                Check(r.Planks(0) == 6 && g.IsOpen(0, 0, 6) && !g.IsOpen(0, 1, 25.99), "six opens own");
                Check(r.VisiblePlanks(1, 26) == 6 && r.Planks(1) == 0 && g.IsOpen(0, 1, 26), "fallback path without fake score");
                r.Deposit(7, 1, 0, 7); Check(r.Planks(0) == 6 && r.Captures(0) == 6, "cap"); });
            Test("King Rush overtime starts at 130s and catches up at exact timestamps", () => {
                var g = new KingRushRules(); var r = new KingRushCaptureRules(g); r.Advance(1000); Check(!r.Started, "no arrival no timer");
                r.Begin(20); r.Advance(149.99); Check(r.Planks(0) == 0, "not early"); r.Advance(150);
                Check(r.Planks(0) == 1 && r.Planks(1) == 1, "first auto plank"); r.Advance(400);
                Check(r.Planks(0) == 6 && r.Planks(1) == 6 && g.OpensAt(0, 0) == 200 && g.OpensAt(0, 1) == 200, "exact completion"); });
            Test("King Rush earned planks combine with overtime without replacing completion", () => {
                var g = new KingRushRules(); var r = new KingRushCaptureRules(g); r.Begin(0);
                for (ulong id = 1; id <= 5; id++) r.Deposit(id, 1, 0, 1);
                r.Advance(130); Check(g.OpensAt(0, 0) == 130 && g.OpensAt(0, 1) == 150 && r.Captures(0) == 5, "mixed completion");
                r.Advance(double.NaN); r.Advance(120); Check(r.Planks(1) == 1, "bad clock ignored"); });
            Test("King Rush gates open at own completion or first plus twenty", () => {
                var r = new KingRushRules(); Check(!r.IsOpen(0, 0, 59), "not yet");
                Check(r.Complete(0, 0, 60) && r.OpensAt(0, 0) == 60 && r.OpensAt(0, 1) == 80, "first team");
                Check(!r.Complete(0, 0, 90) && r.Complete(0, 1, 70) && r.OpensAt(0, 1) == 70, "own sooner");
                Check(!r.IsOpen(1, 0, 900) && !r.Complete(0, 1, double.NaN) && !r.Complete(3, 0, 1), "separate zones"); });
            Test("King Rush pads pause contest, reset departure, and never inherit another pawn's progress", () => {
                var c = new KingRushPadCharge(); c.Step(new ulong[] {1}, 1);
                Check(c.Step(new ulong[] {1,2}, 3) == 0 && c.Seconds == 1 && c.Contested, "contest pause");
                Check(c.Step(new ulong[] {1}, .5) == 1, "resume original"); c.Reset(); c.Step(new ulong[] {1}, 1);
                c.Step(new ulong[] {2,3}, .1); Check(c.Seconds == 0, "original left");
                Check(c.Step(new ulong[] {2}, 1) == 0 && c.Step(new ulong[] {2}, .5) == 2, "new full hold");
                c.Step(new ulong[0], .1); Check(c.Candidate == 0 && c.Seconds == 0, "empty reset"); });
            Test("King Rush limited promotion includes queen but never king", () => {
                var r = new KingRushRules(); Check(r.TryClaim(0, 1, 0) && !r.TryClaim(0, 2, 1) && !r.TryClaim(1, 1, 0), "exclusive and one per wave");
                Check(r.TryClaim(2, 1, 0) && !r.TryClaim(9, 3, 0) && !r.TryClaim(3, 0, 0), "later wave and invalid IDs");
                Check(KingRushPieces.CanPromote(KingRushPiece.Pawn, KingRushPiece.Queen) && !KingRushPieces.CanPromote(KingRushPiece.Pawn, KingRushPiece.King) &&
                    !KingRushPieces.CanPromote(KingRushPiece.King, KingRushPiece.Queen), "promotion types");
                Check(KingRushRules.PadPiece(5) == KingRushPiece.Queen && KingRushRules.PadPiece(0) == KingRushPiece.Knight, "visible schedule"); });
            Test("King Rush facts roundtrip atomically and stale snapshots cannot undo them", () => {
                var a = new KingRushRules(); a.Complete(0, 0, 60); a.TryClaim(0, 123, 1);
                var b = new KingRushRules(); Check(b.Apply(a.Encode()) && b.Encode() == a.Encode(), "roundtrip");
                string before = b.Encode(); Check(b.Apply(new KingRushRules().Encode()) && before == b.Encode(), "stale");
                foreach (var bad in new[] {"", "KR1|NaN|-1|-1|-1", "KR1|60|-1|-1|-1|1,5,0|0,999,1", "KR1|70|-1|-1|-1", "KR1|60|-1|-1|-1|1,123,1", "KR1|-2|-1|-1|-1"})
                    Check(!b.Apply(bad) && b.Encode() == before, "atomic reject: " + bad);
                var random = new Random(32); for (int i = 0; i < 300; i++) { var chars = new char[i]; for (int j = 0; j < i; j++) chars[j] = (char)random.Next(32, 127); b.Apply(new string(chars)); }
                Check(b.Encode() == before, "fuzz preserved"); });
            Test("Sword Fight is selectable and Queen remains planned", () => {
                Check(GameModes.SwordFight.Playable && GameModes.SwordFight.Scene == "SwordFight" && GameModes.Find("queenhill") == null, "scene routing"); });
            Test("Sword ring-out scores once per life and respawns on time", () => {
                var r = new SwordFightRules(); Check(r.RingOut(1, 0) && !r.RingOut(1, 0) && r.Black == 1, "duplicate score");
                r.Advance(2.99); Check(!r.TryRespawn(1), "early respawn"); r.Advance(.02);
                Check(r.TryRespawn(1) && !r.TryRespawn(1) && r.RingOut(1, 0) && r.Black == 2, "new life"); });
            Test("Sword score and timeout end the round without extra scores", () => {
                var r = new SwordFightRules(1); Check(!r.RingOut(0, 0) && !r.RingOut(2, 5), "bad victim");
                Check(r.RingOut(1, 1) && r.Finished && r.Winner == 0 && !r.RingOut(2, 1), "target");
                r = new SwordFightRules(20, 2); r.Advance(double.NaN); r.Advance(double.PositiveInfinity); r.Advance(2);
                Check(r.Finished && r.Winner == -1 && !r.RingOut(1, 0), "time tie"); });
            Test("Sword mode state roundtrips twelve pawns and isolates matches", () => {
                var s = new SwordFightState { Tick = 55, White = 3, Black = 7, Remaining = 123.5f };
                for (ulong i = 1; i <= 12; i++) s.Fighters.Add(new SwordFighterState { Id = i, Alive = true, Drawn = true, X = .3f, Y = .2f, Z = .4f, RW = 1, Flash = .1f });
                var b = SwordFightProtocol.Write(42, s);
                Check(b.Length == SwordFightProtocol.MaxBytes && SwordFightProtocol.Read(b, 42, out var p) && p.Fighters.Count == 12 && p.Fighters[0].Drawn && p.Fighters[0].X == .3f && p.Fighters[0].RW == 1 && p.Black == 7, "roundtrip");
                Check(!SwordFightProtocol.Read(b, 43, out _), "session isolation");
                for (int n = 0; n < b.Length; n++) Check(!SwordFightProtocol.Read(b.Take(n).ToArray(), 42, out _), "truncation"); });
            Test("Sword mode state rejects duplicate and nonfinite data without throwing", () => {
                var s = new SwordFightState(); s.Fighters.Add(new SwordFighterState { Id = 1, RW = 1 }); s.Fighters.Add(new SwordFighterState { Id = 1, RW = 1 });
                Check(!SwordFightProtocol.Read(SwordFightProtocol.Write(1, s), 1, out _), "duplicates");
                s.Fighters.Clear(); s.Remaining = float.NaN;
                Check(!SwordFightProtocol.Read(SwordFightProtocol.Write(1, s), 1, out _), "nan");
                var rng = new Random(9); for (int i = 0; i < 500; i++) { var bytes = new byte[i]; rng.NextBytes(bytes); SwordFightProtocol.Read(bytes, 1, out _); } });
            Test("Physical sword state rejects old magic, invalid rotation and remote offsets", () => {
                var s = new SwordFightState(); s.Fighters.Add(new SwordFighterState { Id = 1, Alive = true, RW = 1 });
                var b = SwordFightProtocol.Write(1, s); b[0] = 0x31;
                Check(!SwordFightProtocol.Read(b, 1, out _), "CFS1 accepted");
                b[0] = 0x32; Check(!SwordFightProtocol.Read(b, 1, out _), "CFS2 accepted");
                foreach (var f in new[] {
                    new SwordFighterState { Id = 1, RW = 0 },
                    new SwordFighterState { Id = 1, RW = float.NaN },
                    new SwordFighterState { Id = 1, RW = 1, X = 5 },
                    new SwordFighterState { Id = 1, RW = 1, Z = float.PositiveInfinity },
                    new SwordFighterState { Id = 1, RW = 1, Drawn = true, Alive = true, Classic = true },
                    new SwordFighterState { Id = 1, RW = 1, Drawn = true, Alive = false } }) {
                    s.Fighters[0] = f; Check(!SwordFightProtocol.Read(SwordFightProtocol.Write(1, s), 1, out _), "bad weapon accepted");
                } });
            Test("Both sword control styles survive absolute full-state updates", () => {
                var s = new SwordFightState();
                s.Fighters.Add(new SwordFighterState { Id = 1, Alive = true, Classic = true, RW = 1 });
                s.Fighters.Add(new SwordFighterState { Id = 2, Alive = true, Drawn = true, RW = 1 });
                var b = SwordFightProtocol.Write(1, s);
                Check(SwordFightProtocol.Read(b, 1, out var p) && p.Fighters[0].Classic && !p.Fighters[0].Drawn &&
                    !p.Fighters[1].Classic && p.Fighters[1].Drawn, "mixed styles");
                b[SwordFightProtocol.HeaderBytes + 8] = 9;
                Check(!SwordFightProtocol.Read(b, 1, out _), "unknown flag"); });
            Test("Two six-player parties stay on opposite teams", () => {
                var r = new TeamReservations(); Check(Reserve(r, 1, 6) && Reserve(r, 7, 6), "admission");
                Check(r.Used(0) == 6 && r.Used(1) == 6 && r.Find(1).Team != r.Find(7).Team, "split");
                Check(!Reserve(r, 13, 1), "overfill"); });
            Test("4+4+4 rejects third party although total free slots are four", () => {
                var r = new TeamReservations(); Reserve(r, 1, 4); Reserve(r, 5, 4);
                Check(!Reserve(r, 9, 4) && r.Count == 8, "party split"); });
            Test("3+3+2+2+1+1 fills exactly six per side", () => {
                var r = new TeamReservations(); int id = 1; foreach (int n in new[] {3,3,2,2,1,1}) { Check(Reserve(r,id,n), "no fit"); id += n; }
                r.Reconcile(new HashSet<ulong>(Ids(1,12)), 1); Check(r.Ready, "not ready"); });
            Test("Pawn Rush early start (R148): three or more a side, even sides, everyone arrived", () => {
                var r = new TeamReservations(); Reserve(r, 1, 3); Reserve(r, 4, 2);
                r.Reconcile(new HashSet<ulong>(Ids(1, 5)), 1); Check(!r.ReadyEarly, "3 v 2");
                Reserve(r, 6, 1); Check(!r.ReadyEarly, "the last one still connecting");
                r.Reconcile(new HashSet<ulong>(Ids(1, 6)), 2); Check(r.Used(0) == 3 && r.Used(1) == 3 && r.ReadyEarly && !r.Ready, "3 v 3");
                Reserve(r, 7, 1); r.Reconcile(new HashSet<ulong>(Ids(1, 7)), 3); Check(!r.ReadyEarly, "4 v 3");
                var two = new TeamReservations(); Reserve(two, 1, 2); Reserve(two, 3, 2); two.Reconcile(new HashSet<ulong>(Ids(1, 4)), 1);
                Check(!two.ReadyEarly, "2 v 2 is too few"); });
            Test("Reservations count members still connecting", () => {
                var r = new TeamReservations(); Reserve(r, 1, 6); Reserve(r, 7, 6);
                r.Reconcile(new HashSet<ulong> {1,7}, 1); Check(!r.Ready && !Reserve(r,13,1), "premature fill"); });
            Test("Retry is idempotent and cannot renew the lease", () => {
                var r = new TeamReservations(); Reserve(r,1,3); Check(Reserve(r,1,3,24) && r.Count == 3, "retry");
                r.Reconcile(new HashSet<ulong> {1},26); Check(r.Count == 0, "lease extended"); });
            Test("Partial join timeout releases whole party", () => {
                var r = new TeamReservations(); Reserve(r,1,4); var removed=r.Reconcile(new HashSet<ulong> {1,2,3},26);
                Check(removed.Count == 1 && r.Count == 0 && Reserve(r,8,6,27), "partial group retained"); });
            // R644 (user 11-14: "그 사람만 나가고, 나머지 파티원과 방은 그대로"): it used to release the whole party.
            Test("A member gone after its party was seated gives up only its own seat; a party with nobody left goes, bots and all", () => {
                var r=new TeamReservations(); Reserve(r,1,3); r.Reconcile(new HashSet<ulong>(Ids(1,3)),1);
                var removed=r.Reconcile(new HashSet<ulong> {1,2},2);
                Check(removed.Count==0&&r.Count==2&&r.Find(3)==null&&r.Find(1)!=null&&r.Find(2)!=null,"the mates lost their seats, or the one gone kept its own");
                // The leader gone: the others keep their seats under the same reservation.
                removed=r.Reconcile(new HashSet<ulong> {2},3);
                Check(removed.Count==0&&r.Count==1&&r.Find(2)!=null&&r.Groups[0].Leader==1,"the party did not outlast its leader");
                removed=r.Reconcile(new HashSet<ulong>(),4);
                Check(removed.Count==1&&r.Count==0,"an empty party kept a seat");
                var bots=new TeamReservations(); ulong lead=500;
                Check(bots.Reserve(lead,77,"t",new[] {lead,BotIdentity.Id(lead,0)},0,out _),"setup: a leader and its bot");
                bots.Reconcile(new HashSet<ulong> {lead},1); bots.Reconcile(new HashSet<ulong>(),2);
                Check(bots.Count==0,"a party's bot stayed without its people");
                var one=new TeamReservations(); Reserve(one,1,3); one.Reconcile(new HashSet<ulong>(Ids(1,3)),1);
                Check(one.RemoveMember(2)&&one.Count==2&&one.Find(2)==null&&!one.RemoveMember(9)&&one.RemoveMember(1)&&one.RemoveMember(3)&&one.Count==0,"RemoveMember"); });
            Test("R644: the waiting room's line of succession, a claim that passes over several in it, and the order by machines alone", () => {
                Func<ulong,int,HostElection.Candidate> Heir=(id,fit)=>new HostElection.Candidate{Id=id,Fitness=fit,PingMs=-1};
                var heirs=new[]{Heir(2,900),Heir(3,800),Heir(4,700),Heir(5,600)};
                var published=new List<ulong>{3,2};
                Check(HostElection.Line(published,new List<ulong>{2,3,4,5},1,heirs).SequenceEqual(new ulong[]{3,2,4,5}),"the line: the published order, then the rest by score");
                Check(HostElection.Line(published,new List<ulong>{2,4,5},3,heirs).SequenceEqual(new ulong[]{2,4,5})&&HostElection.Line(null,new List<ulong>(),1,heirs).Count==0,"the line without the one leaving");
                // Host 1 (number 1) is gone; 3 and 2 before 4 in line are dead as well. 4 claims three up and names both.
                var claims=new Dictionary<ulong,int>{{2,0},{3,0},{4,4},{5,0}};
                var passed=new Dictionary<ulong,ICollection<ulong>>{{4,new List<ulong>{3,2}}};
                int e;
                Check(HostElection.FollowLine(1,1,1,published,claims,heirs,out e,passed)==4&&e==4,"a claim that names everyone before it in line: "+e);
                passed[4]=new List<ulong>{3};
                Check(HostElection.FollowLine(1,1,1,published,claims,heirs,out e,passed)==1&&e==1,"a claim that names only one of the two before it was followed");
                passed.Clear();
                Check(HostElection.FollowLine(1,1,1,published,claims,heirs,out e,passed)==1&&HostElection.FollowLine(1,1,1,published,claims,heirs,out e,null)==1,"a claim out of turn that names nobody was followed");
                passed[4]=new List<ulong>{3,2};claims[4]=9;
                Check(HostElection.FollowLine(1,1,1,published,claims,heirs,out e,passed)==1,"a claim far above what it names was followed");
                // The first in line claims plainly; one passed over that claims after all is followed first, then the one that passed it over.
                claims[4]=0;claims[3]=2;
                Check(HostElection.FollowLine(1,1,1,published,claims,heirs,out e,passed)==3&&e==2,"the first in line: "+e);
                claims[4]=4;
                Check(HostElection.FollowLine(1,1,1,published,claims,heirs,out e,passed)==4&&e==4,"the one passed over came first, then the one that passed it over: "+e);
                // Those it named that Steam has dropped since are no obstacle.
                claims=new Dictionary<ulong,int>{{4,4},{5,0}};
                Check(HostElection.FollowLine(1,1,1,published,claims,heirs,out e,passed)==4&&e==4,"a claim whose passed-over PCs have left the room");
                // Machines alone, best first, whatever they measure against an unknown one's 300 (a room of weak laptops).
                Func<ulong,int,int,HostElection.Candidate> Machine=(id,fit,machine)=>new HostElection.Candidate{Id=id,Fitness=fit,BaseFitness=machine,PingMs=-1};
                Check(HostElection.RankAlone(new[]{Machine(1,120,250),Machine(2,400,200),Machine(3,90,280)}).SequenceEqual(new ulong[]{3,1,2}),"RankAlone");
                Check(HostElection.WaitingMargin>HostElection.StartMargin&&HostElection.StartHost(1,new[]{Machine(1,500,500),Machine(2,540,540)},HostElection.WaitingMargin)==1&&
                      HostElection.StartHost(1,new[]{Machine(1,500,500),Machine(2,560,560)},HostElection.WaitingMargin)==2,"a room that has moved its host moves it again only for 10% more"); });
            Test("Reject spoofed leader, zero IDs, repeated IDs and overlaps", () => {
                var r=new TeamReservations(); Check(!r.Reserve(9,99,"x",new ulong[]{1,2},0,out _),"spoof");
                Check(!r.Reserve(1,99,"x",new ulong[]{1,1},0,out _),"duplicate");
                Check(!r.Reserve(1,99,"x",new ulong[]{1,0},0,out _),"zero");
                Reserve(r,1,3); Check(!Reserve(r,3,2),"overlap"); });
            Test("Existing party cannot change ticket or roster during admission", () => {
                var r=new TeamReservations(); Reserve(r,1,3);
                Check(!r.Reserve(1,101,"new",Ids(1,3),1,out _),"ticket changed"); Check(!Reserve(r,1,4),"roster changed"); });
            // Work order [4] (11-02): made-up members hold seats for 25 s, then the same sender asks again.
            Test("A sender whose party never came waits out the cooldown; a party that came and lost a member does not, nor do bots", () => {
                var r=new TeamReservations(); Reserve(r,1,6); r.Reconcile(new HashSet<ulong> {1},26);
                Check(r.Count==0&&r.CoolingDown(1,26)&&!Reserve(r,1,1,27)&&!Reserve(r,1,6,26+TeamReservations.Cooldown-.5),"asked again during the cooldown");
                Check(Reserve(r,7,6,27)&&Reserve(r,1,1,26+TeamReservations.Cooldown+.5),"the cooldown held up others or never ended");
                var came=new TeamReservations(); Reserve(came,1,2); came.Reconcile(new HashSet<ulong>(Ids(1,2)),1); came.Reconcile(new HashSet<ulong> {1},2);
                Check(came.Count==1&&!came.CoolingDown(1,2),"people who came were made to wait");
                came.Reconcile(new HashSet<ulong>(),3);
                Check(came.Count==0&&!came.CoolingDown(1,3)&&Reserve(came,1,1,4),"people who came and left were made to wait");
                var bots=new TeamReservations(); ulong host=900;
                Check(bots.Reserve(host,77,"t",new[] {host,BotIdentity.Id(host,0)},0,out _)&&bots.ReserveBots(host,BotIdentity.Fill(host,2,6),0,out _),"setup: bots");
                bots.Reconcile(new HashSet<ulong> {host},26);
                Check(bots.Count==4&&!bots.CoolingDown(host,26)&&!bots.CoolingDown(BotIdentity.Id(host,6),26),"bots made someone wait");
                r.StartCooldown(5,0); r.Clear(); Check(!r.CoolingDown(5,1)&&!r.CoolingDown(1,1),"a new room kept the old room's cooldowns"); });
            Test("Fuzzed concurrent admissions never split or exceed team capacity", () => {
                var random=new Random(17);
                for(int run=0;run<1000;run++) { var r=new TeamReservations(); int id=1;
                    for(int request=0;request<30;request++) { int n=random.Next(1,7); Reserve(r,id,n); id+=n; Check(r.Used(0)<=6&&r.Used(1)<=6&&r.Count<=12,"capacity"); }
                } });
            Test("Motion input roundtrip and session isolation", () => {
                var bytes=MotionProtocol.Input(42,new MoveInput {Sequence=10,X=1,Z=-1,Jumps=7});
                Check(MotionProtocol.ReadInput(bytes,42,out var i)&&i.Sequence==10&&i.Jumps==7&&i.Z==-1,"input");
                Check(!MotionProtocol.ReadInput(bytes,43,out _),"cross session"); });
            Test("Reject invalid, oversized and non-finite input", () => {
                Check(!MotionProtocol.ReadInput(new byte[25],1,out _),"truncated");
                foreach(float x in new[]{float.NaN,float.PositiveInfinity,2f}) Check(!MotionProtocol.ReadInput(MotionProtocol.Input(1,new MoveInput {X=x}),1,out _),"invalid input"); });
            Test("Twelve-player snapshot roundtrip stays within packet budget", () => {
                var pawns=Enumerable.Range(1,12).Select(i=>PawnMotor.Spawn((ulong)i,(i-1)/6,(i-1)%6)).ToList();
                var bytes=MotionProtocol.Snapshot(42,7,pawns);
                Check(bytes.Length<MotionProtocol.MaxBytes && MotionProtocol.ReadSnapshot(bytes,42,out uint tick,out var parsed)&&tick==7&&parsed.Count==12,"snapshot"); });
            Test("Malformed snapshots never throw and never accept partial packets", () => {
                var bytes=MotionProtocol.Snapshot(1,1,new[]{PawnMotor.Spawn(1,0,0)});
                for(int n=0;n<bytes.Length;n++) Check(!MotionProtocol.ReadSnapshot(bytes.Take(n).ToArray(),1,out _,out _),"truncation");
                var rng=new Random(4); for(int n=0;n<1200;n++) {var b=new byte[n];rng.NextBytes(b);MotionProtocol.ReadSnapshot(b,1,out _,out _);} });
            Test("Duplicate player IDs and impossible positions are rejected", () => {
                var p=PawnMotor.Spawn(1,0,0); Check(!MotionProtocol.ReadSnapshot(MotionProtocol.Snapshot(1,1,new[]{p,p}),1,out _,out _),"duplicates");
                p.X=float.NaN; Check(!MotionProtocol.ReadSnapshot(MotionProtocol.Snapshot(1,1,new[]{p}),1,out _,out _),"nan"); });
            Test("Old and out-of-order sequence numbers rejected across wraparound", () => {
                Check(!MotionProtocol.Newer(8,10)&&!MotionProtocol.Newer(10,10)&&MotionProtocol.Newer(0,uint.MaxValue),"sequence"); });
            Test("Diagonal motion has the same speed as straight motion", () => {
                var origin=PawnMotor.Spawn(1,0,0); var p=PawnMotor.Advance(origin,new MoveInput {X=1,Z=1},1);
                float distance=(float)Math.Sqrt(Math.Pow(p.X-origin.X,2)+Math.Pow(p.Z-origin.Z,2)); Check(Math.Abs(distance-6)<.001,"diagonal boost"); });
            Test("Jump lands and player stays inside arena", () => {
                var p=PawnMotor.Spawn(1,0,0); p=PawnMotor.Advance(p,new MoveInput{Jump=true},PawnMotor.Step); Check(p.Y>1,"jump");
                for(int i=0;i<600;i++) p=PawnMotor.Advance(p,new MoveInput{X=1,Z=1},PawnMotor.Step);
                Check(p.Y==1&&p.X<=19&&p.Z<=19,"bounds/ground"); });
            Test("Bot IDs cannot collide with Steam IDs and stay owner scoped", () => {
                ulong steam = 76561198000000000UL;
                Check(!BotIdentity.IsBot(steam) && !BotIdentity.IsBot(0), "steam id classified as a bot");
                ulong mine = BotIdentity.Id(steam, 0), theirs = BotIdentity.Id(steam + 1, 0);
                Check(BotIdentity.IsBot(mine) && mine != theirs, "owner not encoded in the id");
                Check(BotIdentity.OwnedBy(mine, steam) && !BotIdentity.OwnedBy(mine, steam + 1), "ownership");
                Check(BotIdentity.Index(BotIdentity.Id(steam, 5)) == 5, "index");
                Check(BotIdentity.Fill(steam, 6).Distinct().Count() == 6, "fill repeats ids"); });
            Test("Declared party bots hold team slots without joining the lobby", () => {
                var r = new TeamReservations(); ulong leader = 500;
                var members = new[] { leader, BotIdentity.Id(leader, 0), BotIdentity.Id(leader, 1) };
                Check(r.Reserve(leader, 77, "t", members, 0, out _), "bot party refused");
                Check(r.Count == 3 && r.Bots == 2, "bot slots not reserved");
                r.Reconcile(new HashSet<ulong> { leader }, 26);
                Check(r.Count == 3 && r.Groups[0].Committed, "absent bots expired the lease"); });
            Test("A leader cannot claim another party's bots and bots cannot send", () => {
                var r = new TeamReservations(); ulong mine = 500, other = 600;
                Check(!r.Reserve(mine, 77, "t", new[] { mine, BotIdentity.Id(other, 0) }, 0, out _), "foreign bot accepted");
                ulong bot = BotIdentity.Id(mine, 0);
                Check(!r.Reserve(bot, 77, "t", new[] { bot }, 0, out _), "bot accepted as sender");
                Check(!r.ReserveBots(mine, new[] { mine }, 0, out _), "human accepted as filler bot");
                Check(r.Count == 0, "rejected requests changed state"); });
            Test("Host filler bots reach a full 6v6 alone and can be removed", () => {
                var r = new TeamReservations(); ulong host = 900;
                Check(r.Reserve(host, 77, "t", new[] { host }, 0, out _), "host reservation");
                for (int guard = 0; guard < 12 && r.Count < 12; guard++) {
                    int size = Math.Min(Math.Min(12 - r.Count, Math.Max(6 - r.Used(0), 6 - r.Used(1))), 6);
                    Check(size > 0 && r.ReserveBots(host, BotIdentity.Fill(host, size, 6 + r.Bots), 0, out _), "filler refused"); }
                r.Reconcile(new HashSet<ulong> { host }, 1);
                Check(r.Count == 12 && r.Used(0) == 6 && r.Used(1) == 6 && r.Ready, "not a full 6v6");
                r.RemoveFillerBots();
                Check(r.Count == 1 && r.Bots == 0, "filler bots survived removal"); });
            Test("Targeted filler respects team choice, capacity and idempotence", () => {
                var r = new TeamReservations(); Reserve(r, 1, 1);
                var bots = BotIdentity.Fill(1, 5, 6);
                Check(r.ReserveBots(1, bots, 0, 0, out var g) && g.Team == 0 && r.Used(0) == 6 && r.Used(1) == 0, "rebalanced explicit team");
                Check(r.ReserveBots(1, bots, 24, 0, out _) && r.Count == 6 && g.Deadline == 25, "retry");
                Check(!r.ReserveBots(1, bots, 24, 1, out _), "retry moved teams");
                Check(!r.ReserveBots(1, BotIdentity.Fill(1, 1, 11), 0, 0, out _) && r.Count == 6, "spilled onto enemy");
                Check(r.ReserveBots(1, BotIdentity.Fill(1, 6, 12), 0, 1, out _) && r.Count == 12, "enemy capacity"); });
            Test("Targeted filler removal preserves human and party-bot reservations", () => {
                var r = new TeamReservations(); ulong host = 1, partyBot = BotIdentity.Id(host, 0);
                r.Reserve(host, 100, "party", new[] { host, partyBot }, 0, out _);
                r.ReserveBots(host, BotIdentity.Fill(host, 3, 6), 0, 0, out _);
                Check(r.RemoveFillerBot(0) && r.FillerBots(0) == 2 && r.Count == 4, "remove one, not group");
                Check(r.RemoveFillerBot(0) && r.RemoveFillerBot(0) && !r.RemoveFillerBot(0), "remove to zero");
                Check(r.Count == 2 && r.Find(host) == r.Find(partyBot) && !r.RemoveFillerBot(1), "party modified"); });
            Test("Targeted filler rejects malformed teams, spoofed owners and humans", () => {
                var r = new TeamReservations(); var bot = BotIdentity.Fill(1, 1, 6);
                Check(!r.ReserveBots(1, bot, 0, 2, out _) && !r.ReserveBots(1, bot, 0, -2, out _), "invalid team");
                Check(!r.ReserveBots(2, bot, 0, 0, out _) && !r.ReserveBots(1, new ulong[] { 1 }, 0, 0, out _), "spoofed filler");
                Check(!r.RemoveFillerBot(2) && r.Count == 0, "invalid mutation"); });
            Test("Bot movement is a valid in-bounds input stream", () => {
                ulong id = BotIdentity.Id(900, 0);
                var brain = new BotBrain(id, 0); var state = PawnMotor.Spawn(id, 0, 0);
                float startX = state.X, startZ = state.Z; bool moved = false;
                for (int i = 0; i < 4000; i++) {
                    var input = brain.Think(state, i * (double)PawnMotor.Step);
                    Check(Math.Abs(input.X) <= 1.01f && Math.Abs(input.Z) <= 1.01f, "input out of range");
                    Check(!float.IsNaN(input.X) && !float.IsNaN(input.Z), "input not finite");
                    state = PawnMotor.Advance(state, input, PawnMotor.Step);
                    moved |= Math.Abs(state.X - startX) > 1 || Math.Abs(state.Z - startZ) > 1; }
                Check(moved, "bot never moved");
                Check(Math.Abs(state.X) <= PawnMotor.Radius && Math.Abs(state.Z) <= PawnMotor.Radius && state.Y >= 1, "bot left the arena"); });
            Test("A jump survives the loss of the packet that first carried it", () => {
                // Client presses on sequence 2; the packet is lost; sequence 3 still carries the count.
                byte consumed=0; var sent=new[]{ new MoveInput{Sequence=1,Jumps=0}, new MoveInput{Sequence=2,Jumps=1}, new MoveInput{Sequence=3,Jumps=1}, new MoveInput{Sequence=4,Jumps=1} };
                int jumps=0; foreach(var input in sent) { if(input.Sequence==2) continue;
                    MotionProtocol.ReadInput(MotionProtocol.Input(5,input),5,out var got); if(MotionProtocol.TakeJump(got.Jumps,ref consumed)) jumps++; }
                Check(jumps==1,"lost press must jump exactly once, got "+jumps);
                byte wrap=255; Check(MotionProtocol.TakeJump(0,ref wrap)&&!MotionProtocol.TakeJump(0,ref wrap),"count wraps"); });
            Test("Old protocol packets are rejected", () => {
                var bytes=MotionProtocol.Input(1,new MoveInput{Sequence=1}); bytes[0]=0x31; // "CFF1" magic, little endian
                Check(!MotionProtocol.ReadInput(bytes,1,out _),"v1 input accepted"); });
            Test("Host silence escalates: unstable, frozen, lost", () => {
                Check(LinkMonitor.Classify(.1f)==LinkHealth.Ok&&LinkMonitor.Classify(.6f)==LinkHealth.Unstable,"unstable");
                Check(LinkMonitor.Classify(2.5f)==LinkHealth.Frozen&&LinkMonitor.Classify(12f)==LinkHealth.Lost,"frozen/lost"); });
            Test("Host silence in a match scene (R295) counts from the host's first packet or the shared start, never while loading", () => {
                Func<float, float, bool> Near = (a, b) => Math.Abs(a - b) < 1e-3f;
                var s = new HostSilence();
                s.Enter(100);
                Check(s.Seconds(110, false) == 0, "a host still loading is not silent");
                s.Heard(112);
                Check(Near(s.Seconds(115, false), 3), "counted from its first packet");
                s.Heard(116);
                Check(Near(s.Seconds(116.5f, false), .5f), "and from each one after");
                s.Enter(200);
                Check(s.Seconds(205, false) == 0 && s.Seconds(206, true) == 0 && Near(s.Seconds(210, true), 4), "or from the shared start");
                s.Enter(300);
                float giveUp = 300 + (float)MatchStart.ClientGiveUp;
                Check(s.Seconds(giveUp - 1, false) == 0 && s.Seconds(giveUp, false) == 0 && Near(s.Seconds(giveUp + 12, false), 12), "a host that never comes is still given up on");
                s.Enter(400);
                Check(s.Seconds(400, true) == 0 && Near(s.Seconds(402, true), 2), "a new host mid-match: counted from the change"); });
            Test("Host watch (R295): every PC notices a host change, not only the two whose roles swap", () => {
                var w = new HostWatch();
                Check(!w.Changed(1, false) && !w.Changed(1, false), "the first look and the same host are no change");
                Check(w.Changed(2, false) && !w.Changed(2, false), "a third PC: the host moved from 1 to 2");
                Check(w.Changed(3, true) && !w.Changed(3, true), "this PC took over");
                Check(w.Changed(4, false), "and handed on");
                var late = new HostWatch();
                Check(!late.Changed(5, true) && late.Changed(5, false), "the role alone"); });
            Test("Response time measures send to acknowledgement and ignores stale acks", () => {
                var t=new ResponseTimer(); Check(t.Milliseconds<0,"empty");
                for(uint s=1;s<=5;s++) t.Sent(s,s*0.033);
                t.Acknowledged(3,0.099+0.1); Check(Math.Abs(t.Milliseconds-100)<0.5,"first sample "+t.Milliseconds);
                t.Acknowledged(2,1.0); Check(Math.Abs(t.Milliseconds-100)<0.5,"stale ack changed estimate");
                t.Acknowledged(5,0.165+0.180); Check(t.Milliseconds>100&&t.Milliseconds<180,"smoothing "+t.Milliseconds); });
            Test("Pawn Rush course state (CFP5) round-trips stations with their blocks and refuses damaged packets", () => {
                var a = new PawnRushNetState { Tick = 77, HostTimeMs = 123456, First = 3, Second = 0, StationCount = 4 };
                a.Game[0] = 3; a.Game[1] = 3; a.Game[2] = 0; a.Game[3] = 0;
                a.Progress[0] = .25f; a.Progress[3] = 1f; a.Completed[3] = true;
                var w = new NetBlock(a.Block[0], 0, PawnRushNetState.BlockBytes);
                w.U16(0x2A5); w.U8(0x1D); w.U8(2); w.U8(SkillWire.Ref(0, 1)); w.U8(SkillWire.Ref(0, 4)); w.Unit16(271.5f, 360f);
                a.BlockLength[0] = w.Written;
                var full = new NetBlock(a.Block[2], 0, PawnRushNetState.BlockBytes);
                for (int k = 0; k < PawnRushNetState.BlockBytes; k++) full.U8(k + 1);
                full.U8(99);   // past the end: dropped, never written
                a.BlockLength[2] = full.Written;
                Check(a.BlockLength[0] == 8 && a.BlockLength[2] == PawnRushNetState.BlockBytes, "block lengths " + a.BlockLength[0] + " " + a.BlockLength[2]);
                byte[] bytes = a.Write(42UL);
                Check(bytes.Length == a.Bytes && bytes.Length <= PawnRushNetState.MaxBytes, "size " + bytes.Length);
                var b = new PawnRushNetState();
                Check(PawnRushNetState.Read(bytes, 42UL, b), "read");
                Check(b.Tick == 77 && b.HostTimeMs == 123456 && b.First == 3 && b.Second == 0, "header");
                Check(b.Game[0] == 3 && b.Game[2] == 0 && Math.Abs(b.Progress[0] - .25f) < 1e-4f && b.Progress[3] == 1f && b.Completed[3] && !b.Completed[0], "stations");
                var r = new NetBlock(b.Block[0], 0, b.BlockLength[0]);
                Check(r.ReadU16() == 0x2A5 && r.ReadU8() == 0x1D && r.ReadU8() == 2 && r.ReadU8() == SkillWire.Ref(0, 1) && r.ReadU8() == SkillWire.Ref(0, 4)
                      && Math.Abs(r.ReadUnit16(360f) - 271.5f) < .01f && r.ReadU8() == 0, "block 0 fields, then nothing");
                Check(b.BlockLength[2] == PawnRushNetState.BlockBytes && b.Block[2][31] == 32 && b.BlockLength[1] == 0, "a full block and an empty one");
                var copy = new PawnRushNetState(); copy.CopyFrom(b);
                // R297: a written state's array is reused for the next one of its length, so compare with a copy.
                var sent = (byte[])bytes.Clone();
                Check(copy.Write(42UL).SequenceEqual(sent), "copy writes the same bytes");
                Check(!PawnRushNetState.Read(bytes, 43UL, b), "another match");
                var cut = new byte[bytes.Length - 1]; Array.Copy(bytes, cut, cut.Length);
                Check(!PawnRushNetState.Read(cut, 42UL, b), "short packet");
                var longer = new byte[bytes.Length + 1]; Array.Copy(bytes, longer, bytes.Length);
                Check(!PawnRushNetState.Read(longer, 42UL, b), "long packet");
                bytes[0] ^= 1; Check(!PawnRushNetState.Read(bytes, 42UL, b), "wrong magic"); });
            Test("Pawn Rush course state (CFP5) carries the pads and their choosers, the winner, the clock and the crown finishers", () => {
                var a = new PawnRushNetState { Tick = 9, First = 1, Second = 4, PadFirst = (byte)PieceKind.Knight, PadSecond = (byte)PieceKind.Queen,
                                               PadsUsed = 0x5, Winner = 1, Clock = 187.25f, FinisherCount = 3, StationCount = 4 };
                a.PadChooser[2] = SkillWire.Ref(0, 3);
                a.FinisherSeat[0] = SkillWire.Ref(1, 0); a.FinisherTime[0] = 170.04f;
                a.FinisherSeat[1] = SkillWire.Ref(1, 2); a.FinisherTime[1] = 181.5f;
                a.FinisherSeat[2] = SkillWire.Ref(0, 5); a.FinisherTime[2] = 186.96f;
                byte[] bytes = a.Write(7UL);
                Check(bytes.Length == PawnRushNetState.HeaderBytes + 4 + 3 * PawnRushNetState.FinisherBytes + 4 * PawnRushNetState.StationFixedBytes, "size " + bytes.Length);
                var b = new PawnRushNetState();
                Check(PawnRushNetState.Read(bytes, 7UL, b), "read");
                Check(b.PadFirst == (byte)PieceKind.Knight && b.PadSecond == (byte)PieceKind.Queen && b.PadsUsed == 0x5, "pads");
                Check(b.PadChooser[2] == SkillWire.Ref(0, 3) && b.PadChooser[0] == PawnRushNetState.NoChooser && b.PadChooser[3] == PawnRushNetState.NoChooser, "choosers");
                Check(b.Winner == 1 && Math.Abs(b.Clock - 187.25f) < 1e-4f, "winner and clock");
                Check(b.FinisherCount == 3 && b.FinisherSeat[1] == SkillWire.Ref(1, 2) && Math.Abs(b.FinisherTime[0] - 170f) < 0.051f
                      && Math.Abs(b.FinisherTime[2] - 187f) < 0.051f, "finishers to a tenth");
                var none = new PawnRushNetState().Write(7UL);
                Check(PawnRushNetState.Read(none, 7UL, b) && b.Winner == -1 && b.FinisherCount == 0, "a fresh state: nobody yet");
                bytes[PawnRushNetState.FinisherCountAt] = 13; Check(!PawnRushNetState.Read(bytes, 7UL, b), "more finishers than seats"); });
            Test("Pawn Rush course state (CFP6, R538): each seat's checkpoint and piece round-trip after everything else, too many are refused", () => {
                var a = new PawnRushNetState { Tick = 3, HostTimeMs = 99, StationCount = 2, PartsLength = 5, SeatCount = 3 };
                for (int k = 0; k < 5; k++) a.Parts[k] = (byte)(40 + k);
                a.SeatRef[0] = SkillWire.Ref(0, 0); a.SeatCheckpoint[0] = PawnRushNetState.CheckpointOf(int.MinValue); a.SeatPiece[0] = (byte)PieceKind.Pawn;
                a.SeatRef[1] = SkillWire.Ref(1, 4); a.SeatCheckpoint[1] = PawnRushNetState.CheckpointOf(203); a.SeatPiece[1] = (byte)PieceKind.Queen;
                a.SeatRef[2] = SkillWire.Ref(0, 5); a.SeatCheckpoint[2] = PawnRushNetState.CheckpointOf(-1); a.SeatPiece[2] = (byte)PieceKind.Rook;
                byte[] sent = (byte[])a.Write(11UL).Clone();
                Check(sent.Length == a.Bytes && sent.Length == PawnRushNetState.HeaderBytes + 2 + 2 * PawnRushNetState.StationFixedBytes + 5 + 3 * PawnRushNetState.SeatBytes, "size " + sent.Length);
                var b = new PawnRushNetState();
                Check(PawnRushNetState.Read(sent, 11UL, b) && b.SeatCount == 3 && b.PartsLength == 5 && b.Parts[4] == 44, "read");
                Check(b.SeatRef[0] == SkillWire.Ref(0, 0) && b.SeatCheckpoint[0] == PawnRushNetState.NoCheckpoint && b.SeatPiece[0] == (byte)PieceKind.Pawn, "a seat that has reached no checkpoint");
                Check(b.SeatRef[1] == SkillWire.Ref(1, 4) && b.SeatCheckpoint[1] == 203 && b.SeatPiece[1] == (byte)PieceKind.Queen && b.SeatCheckpoint[2] == -1 && b.SeatPiece[2] == (byte)PieceKind.Rook, "checkpoints and pieces");
                Check(PawnRushNetState.CheckpointOf(100000) == short.MaxValue && PawnRushNetState.CheckpointOf(int.MinValue) == PawnRushNetState.NoCheckpoint, "the checkpoint's range");
                var c = new PawnRushNetState(); c.CopyFrom(b);
                Check(c.SeatCount == 3 && c.SeatCheckpoint[1] == 203 && c.SeatPiece[2] == (byte)PieceKind.Rook, "a copy carries them");
                // Most states carry none: the same packet a seat table shorter.
                a.SeatCount = 0; byte[] bare = (byte[])a.Write(11UL).Clone();
                Check(bare.Length == sent.Length - 3 * PawnRushNetState.SeatBytes && PawnRushNetState.Read(bare, 11UL, b) && b.SeatCount == 0, "a state without the seats");
                var thirteen = (byte[])sent.Clone(); thirteen[PawnRushNetState.FinisherCountAt + 3] = 13;
                Check(!PawnRushNetState.Read(thirteen, 11UL, b), "thirteen seats");
                var cut = new byte[sent.Length - 1]; Array.Copy(sent, cut, cut.Length);
                Check(!PawnRushNetState.Read(cut, 11UL, b), "a seat byte short"); });
            Test("Pawn Rush course state (CFP5, R508): a round's six stations and its sections' bytes round-trip, too many are refused", () => {
                var a = new PawnRushNetState { Tick = 5, HostTimeMs = 99, StationCount = 6, PadsUsed = 0x3f };
                for (int st = 0; st < 6; st++) { a.Game[st] = (sbyte)(st / 2 * 3); a.Progress[st] = st / 6f; a.Completed[st] = st == 5; a.PadChooser[st] = (byte)(st == 4 ? 7 : PawnRushNetState.NoChooser); }
                a.BlockLength[5] = 3; a.Block[5][0] = 9; a.Block[5][1] = 8; a.Block[5][2] = 7;
                a.PartsLength = 17;
                for (int k = 0; k < a.PartsLength; k++) a.Parts[k] = (byte)(200 - k);
                byte[] bytes = a.Write(11UL);
                Check(bytes.Length == a.Bytes && bytes.Length == PawnRushNetState.HeaderBytes + 6 + 6 * PawnRushNetState.StationFixedBytes + 3 + 17, "size " + bytes.Length);
                var b = new PawnRushNetState();
                Check(PawnRushNetState.Read(bytes, 11UL, b), "read");
                Check(b.StationCount == 6 && b.Game[4] == 6 && b.Completed[5] && !b.Completed[4] && Math.Abs(b.Progress[3] - .5f) < 1e-4f && b.PadsUsed == 0x3f
                      && b.PadChooser[4] == 7 && b.PadChooser[0] == PawnRushNetState.NoChooser, "six stations");
                Check(b.BlockLength[5] == 3 && b.Block[5][2] == 7, "the last station's block");
                Check(b.PartsLength == 17 && b.Parts[0] == 200 && b.Parts[16] == 184, "the sections' bytes");
                var copy = new PawnRushNetState(); copy.CopyFrom(b);
                var sent = (byte[])bytes.Clone();
                Check(copy.Write(11UL).SequenceEqual(sent), "copy writes the same bytes");
                Check(new PawnRushNetState().Write(11UL).Length == PawnRushNetState.HeaderBytes, "no stations, no parts: the header alone");
                var seven = (byte[])sent.Clone(); seven[PawnRushNetState.FinisherCountAt + 1] = 7;
                Check(!PawnRushNetState.Read(seven, 11UL, b), "seven stations");
                var cut = new byte[sent.Length - 1]; Array.Copy(sent, cut, cut.Length);
                Check(!PawnRushNetState.Read(cut, 11UL, b), "a part byte short"); });
            Test("Board (v1.0 A/G/H/I): a1 dark, h1 light, 32 of each, index and square round trips", () => {
                Check(!PawnRushBoard.IsLight(0, 1) && PawnRushBoard.IsLight(7, 1) && PawnRushBoard.IsLight(0, 8) && !PawnRushBoard.IsLight(7, 8), "corners");
                int light = 0;
                for (int f = 0; f < 8; f++)
                    for (int r = 1; r <= 8; r++)
                    {
                        if (PawnRushBoard.IsLight(f, r)) light++;
                        int i = PawnRushBoard.Index(f, r);
                        Check(PawnRushBoard.FileOf(i) == f && PawnRushBoard.RankOf(i) == r, "index " + i);
                        Check(PawnRushBoard.SquareAt(PawnRushBoard.Center(f, r), out int f2, out int r2) && f2 == f && r2 == r, "square at the middle");
                        Check(PawnRushBoard.Parse(PawnRushBoard.Name(f, r), out f2, out r2) && f2 == f && r2 == r, "name");
                    }
                Check(light == 32, "32 light squares");
                Check(Math.Abs(PawnRushBoard.Center(0, 1).V + 7.875f) < 1e-4f && Math.Abs(PawnRushBoard.Center(0, 1).U - 3.125f) < 1e-4f, "a1's middle");
                Check(!PawnRushBoard.SquareAt(new StationPoint(-9.5f, 5f), out _, out _) && !PawnRushBoard.SquareAt(new StationPoint(0f, 1.9f), out _, out _), "off the board"); });
            Test("F en passant: sixteen different offsets, capture only in the window, a function of the clock", () => {
                var offsets = new HashSet<float>();
                for (int i = 0; i < EnPassantRules.Statues; i++) offsets.Add(EnPassantRules.Offset(i));
                Check(offsets.Count == 16, "offsets " + offsets.Count);
                Check(!EnPassantRules.InWindow(.1f) && EnPassantRules.InWindow(.31f) && EnPassantRules.InWindow(1.5f) && !EnPassantRules.InWindow(1.6f)
                      && EnPassantRules.InWindow(1.6f, EnPassantRules.Grace) && !EnPassantRules.InWindow(2f, EnPassantRules.Grace) && !EnPassantRules.InWindow(10f, .15f), "window");
                Check(Math.Abs(EnPassantRules.Displacement(1f) - 2.2f) < 1e-4f && EnPassantRules.Displacement(8f) == 0f
                      && Math.Abs(EnPassantRules.Displacement(3f) - 1.1f) < 1e-3f && EnPassantRules.Displacement(0f) == 0f, "displacement");
                Check(EnPassantRules.Captures(.7f, true, 0f, 1f) && EnPassantRules.Captures(.7f, false, 5.2f, 1f) && !EnPassantRules.Captures(.7f, false, 3f, 1f)
                      && !EnPassantRules.Captures(.9f, true, 0f, 1f) && !EnPassantRules.Captures(.5f, true, 0f, 6f), "capture rule");
                var p = EnPassantRules.Position(100.0, 3); var q = EnPassantRules.Position(100.0, 3);
                Check(p.V == q.V && p.U == q.U && p.V == EnPassantRules.Columns[3], "same time, same place");
                int open = 0; for (int i = 0; i < 16; i++) if (EnPassantRules.InWindow(EnPassantRules.Phase(57.3, i))) open++;
                Check(open <= 3, "a handful open at once: " + open); });
            Test("C drawbridge rope: the spec's speeds, a lone puller never stopped", () => {
                Check(Math.Abs(DrawbridgeRope.Speed(1, 0) - .045f) < .001f && Math.Abs(DrawbridgeRope.Speed(2, 0) - .115f) < .002f
                      && Math.Abs(DrawbridgeRope.Speed(3, 0) - .199f) < .002f && Math.Abs(DrawbridgeRope.Speed(4, 0) - .292f) < .003f, "table");
                Check(Math.Abs(DrawbridgeRope.Speed(1, .5f) - .04f) < 1e-4f && DrawbridgeRope.Speed(0, .5f) == 0f && DrawbridgeRope.Speed(7.2f, 0) <= .35f, "minimum and cap");
                Check(DrawbridgeRope.Speed(1.8f, 0) > DrawbridgeRope.Speed(1, 0) * 1.8f, "sprinting pulls harder than it costs"); });
            Test("J knight's road: every square a knight's move from the last, d1 to f8, pieces on the road", () => {
                Check(KnightRoute.Joined(), "joined by knight moves");
                Check(KnightRoute.Squares[0] == "d1" && KnightRoute.Squares[KnightRoute.Squares.Length - 1] == "f8", "entrance to gate");
                Check(KnightRoute.PieceSquares.Length == KnightRoute.PieceKinds.Length, "a kind per piece");
                foreach (var q in KnightRoute.PieceSquares) Check(Array.IndexOf(KnightRoute.Squares, q) > 0, "piece on the road: " + q);
                Check(KnightRoute.SquareAt(KnightRoute.Center("c5")) == 3 && KnightRoute.SquareAt(new StationPoint(0f, 10f)) < 0, "squares and water");
                Check(KnightRoute.Takes(.5f, true, 0f, false) && KnightRoute.Takes(.5f, false, 0f, true) && !KnightRoute.Takes(.5f, false, 2.5f, false) && !KnightRoute.Takes(1.2f, true, 9f, true), "takes"); });
            Test("G king escort: a road of fifteen joined squares, 31.5 m, five turntables, ends on e8 on the board, the speed table", () => {
                Check(KingEscortPath.Connected() && KingEscortPath.Cells.Length == 15, "joined");
                Check(Math.Abs(KingEscortPath.Length - 31.5f) < .01f, "length " + KingEscortPath.Length);
                int turns = 0; for (int i = 0; i < KingEscortPath.Cells.Length; i++) if (KingEscortPath.Turns(i)) turns++;
                Check(turns == 5 && !KingEscortPath.Turns(KingEscortPath.CellIndex("e8")), "turntables " + turns);
                var p = KingEscortPath.Position(0f, out float dv, out float du);
                Check(Math.Abs(p.V - PawnRushBoard.Center(3, 1).V) < 1e-4f && dv == 0f && du == 1f, "starts on d1 going north");
                // R524: the end is e8's middle, reached going west from f8; the 1.6 m sled (half 0.8) stays on the board,
                // short of its north edge (u 20) where the team door's tunnel starts.
                var g = KingEscortPath.Position(KingEscortPath.Length, out dv, out du);
                var e8 = PawnRushBoard.Center(4, 8);
                Check(g.DistanceTo(e8) < 1e-3f && Math.Abs(dv + 1f) < 1e-4f && Math.Abs(du) < 1e-4f, "ends on e8 going west");
                Check(g.U + .8f < PawnRushBoard.U0 + PawnRushBoard.Cell * PawnRushBoard.N, "the sled stops on the board");
                Check(Math.Abs(KingEscortPath.At(KingEscortPath.Cells.Length - 1) - KingEscortPath.Length) < 1e-4f, "the last cell's s is the end");
                Check(Math.Abs(KingEscortPath.Speed(1.5f, false) - .735f) < 1e-4f && KingEscortPath.Speed(.5f, true) == .11f && KingEscortPath.Speed(9f, false) == 1.1f, "speeds");
                Check(KingEscortPath.IsSand(KingEscortPath.At(5)) && !KingEscortPath.IsSand(KingEscortPath.At(0)), "sand");
                Check(KingEscortPath.RookStop(0) < KingEscortPath.At(8) && KingEscortPath.RookStop(1) < KingEscortPath.At(12), "rooks stop the sled short"); });
            Test("B plank bridge (R528): lenient laying in the nearest free slot, the chasm, the station, the piles", () => {
                Check(PlankBridge.Slots == 10 && PlankBridge.Planks == 10 && PlankBridge.Chasm1 == 16f, "ten slots, ten planks, a 10 m chasm");
                Check(Math.Abs(PlankBridge.SlotX(0) + 1.25f) < 1e-5f && Math.Abs(PlankBridge.SlotX(1) - 1.25f) < 1e-5f && Math.Abs(PlankBridge.SlotZ(0) - 7f) < 1e-5f
                      && Math.Abs(PlankBridge.SlotZ(9) - 15f) < 1e-5f, "slot centres");
                // A carrier on the near floor 1.4 m short of the edge (hips z 4.6): the plank in front of it (1.2 m ahead) lays row 0.
                Check(PlankBridge.Snap(-.3f, 6.0f, 0) == 0 && PlankBridge.Snap(.3f, 6.0f, 0) == 1, "row 0 from the floor, the nearer column");
                Check(PlankBridge.Snap(-.3f, 5.6f, 0) == 0 && PlankBridge.Snap(0f, 5.4f, 0) == -1 && PlankBridge.Snap(2.9f, 7f, 0) == -1, "not from further, not beside the bridge");
                Check(PlankBridge.Snap(0f, 6.0f, 1) == 1 && PlankBridge.Snap(-.3f, 6.0f, 1) == -1 && PlankBridge.Snap(-1.25f, 7f, 3) == -1, "the free column (within 1.5 m), nothing when the row is laid");
                Check(PlankBridge.Snap(-1.25f, 8.6f, 3) == 2 && PlankBridge.Snap(0f, 5.0f, 0, 1.7f) == 0, "the next row from the first, a wider preview");
                Check(PlankBridge.OverChasm(0f, 7f, 0) && !PlankBridge.OverChasm(0f, 7f, 3) && PlankBridge.OverChasm(3f, 7f, 3) && !PlankBridge.OverChasm(0f, 5.9f, 0)
                      && !PlankBridge.OverChasm(0f, 16.1f, 0), "the chasm, a laid row, beside the bridge, the floors");
                Check(PlankBridge.InArea(10f, 20f) && !PlankBridge.InArea(10.5f, 3f) && !PlankBridge.InArea(0f, -.5f), "the station");
                Check(PlankBridge.InCarryArea(-11.5f, 3f) && !PlankBridge.InCarryArea(-11.7f, 3f) && PlankBridge.InCarryArea(0f, 21.5f), "held out a little further");
                Check(PlankBridge.OnPile(-8f, 3.5f) && PlankBridge.OnPile(9f, 4.3f) && !PlankBridge.OnPile(0f, 3.5f), "the piles");
                Check(PlankBridge.Count(0x3FF) == 10 && PlankBridge.Count(0x5) == 2, "counted"); });
            Test("B plank block (R528, R543): every state round-trips, a lying plank on the 0.1 m grid, 31 bytes at most", () => {
                var a = new PlankBoard();
                Check(a.Stock(0) == 5 && a.Stock(1) == 5 && a.TopOf(0) == 4 && a.TopOf(1) == 9, "two full piles");
                a.Laid = 0x3;
                a.State[4] = PlankState.Laid; a.State[9] = PlankState.Laid;
                a.State[3] = PlankState.Carried; a.Seat[3] = 7;
                a.State[8] = PlankState.Lying; a.X[8] = PlankBoard.GridX(-3.04f); a.Z[8] = PlankBoard.GridZ(4.46f); a.Turned[8] = true;
                a.State[0] = PlankState.Lying; a.X[0] = PlankBoard.GridX(9.9f); a.Z[0] = PlankBoard.GridZ(18.2f);
                var buffer = new byte[PawnRushNetState.BlockBytes];
                var w = new NetBlock(buffer, 0, buffer.Length);
                a.Write(ref w);
                Check(w.Written == 2 + 5 + 1 + 2 * 2, "bytes " + w.Written);
                var b = new PlankBoard();
                var r = new NetBlock(buffer, 0, w.Written);
                b.Read(ref r);
                Check(b.Laid == 0x3 && b.LaidCount == 2 && b.Stock(0) == 2 && b.Stock(1) == 3 && b.TopOf(0) == 2, "laid and stock");
                Check(b.State[3] == PlankState.Carried && b.Seat[3] == 7 && b.Seat[8] == PlankBoard.NoSeat, "the carrier");
                Check(b.State[8] == PlankState.Lying && b.Turned[8] && Math.Abs(b.X[8] + 3.0f) < 1e-4f && Math.Abs(b.Z[8] - 4.5f) < 1e-4f, "lying, turned, on the grid");
                Check(b.State[0] == PlankState.Lying && !b.Turned[0] && Math.Abs(b.X[0] - 9.9f) < 1e-4f && Math.Abs(b.Z[0] - 18.2f) < 1e-4f, "lying far side");
                // The worst case: every plank lying.
                var all = new PlankBoard();
                for (int i = 0; i < PlankBridge.Planks; i++) { all.State[i] = PlankState.Lying; all.X[i] = -12.8f + i; all.Z[i] = 23.5f - i; }
                var big = new NetBlock(buffer, 0, buffer.Length);
                all.Write(ref big);
                Check(big.Written == all.Bytes && all.Bytes == 27 && PlankBoard.MaxBytes <= PawnRushNetState.BlockBytes, "worst case " + big.Written);
                var back = new PlankBoard();
                var rb = new NetBlock(buffer, 0, big.Written);
                back.Read(ref rb);
                Check(Math.Abs(back.X[0] + 12.8f) < 1e-4f && Math.Abs(back.Z[0] - 23.5f) < 1e-4f && Math.Abs(back.X[9] + 3.8f) < 1e-4f, "the grid's ends");
                back.Reset();
                Check(back.Stock(0) == 5 && back.LaidCount == 0 && back.State[7] == PlankState.Pile, "reset"); });
            Test("H checkmate hunt: mate in a corner, stalemate with nowhere to go, four checks freeze, deterministic", () => {
                var hunt = new CheckmateHunt();
                foreach (var k in hunt.All) k.State = CheckmateHunt.KingState.Captured;
                var king = hunt.All[0]; king.State = CheckmateHunt.KingState.Free; king.File = 0; king.Rank = 1; king.NextTurn = 0;
                hunt.Step(.1, new[] { PawnRushBoard.Center(1, 2) }, new StationPoint[0]);
                Check(king.State == CheckmateHunt.KingState.Frozen && king.Mated, "a pawn diagonally inside mates a1");
                Check(!hunt.TryCapture(0, PawnRushBoard.Center(5, 5), true, 0f) && hunt.TryCapture(0, PawnRushBoard.Center(0, 1), true, 0f), "a dive captures a frozen king");
                // Stalemate: a1 with its three neighbours held by other kings, a pawn just off the board.
                hunt = new CheckmateHunt();
                for (int i = 0; i < 6; i++) hunt.All[i].State = CheckmateHunt.KingState.Captured;
                string[] at = { "a1", "a2", "b1", "b2" };
                for (int i = 0; i < 4; i++) { var k = hunt.All[i]; k.State = CheckmateHunt.KingState.Free; PawnRushBoard.Parse(at[i], out k.File, out k.Rank); k.NextTurn = i == 0 ? 0 : 99; }
                hunt.Step(.1, new[] { new StationPoint(-9.8f, 3f) }, new StationPoint[0]);
                Check(hunt.All[0].State == CheckmateHunt.KingState.Free && !(hunt.All[0].File == 0 && hunt.All[0].Rank == 1), "stalemate moves it away");
                // Four checks: a pawn next to the king every time its immunity is over.
                hunt = new CheckmateHunt();
                for (int i = 1; i < 6; i++) hunt.All[i].State = CheckmateHunt.KingState.Captured;
                king = hunt.All[0]; king.File = 3; king.Rank = 4; king.NextTurn = 0;
                double t = 0;
                for (int n = 0; n < 4; n++)
                {
                    var below = PawnRushBoard.Center(king.File, king.Rank); below.U -= PawnRushBoard.Cell;
                    hunt.Step(t += .05, new[] { below }, new StationPoint[0]);
                    hunt.Step(t += .3, new[] { below }, new StationPoint[0]);   // its turn comes 0.2 s after the attack
                    hunt.Step(t += 3.5, new[] { new StationPoint(9.9f, 1f) }, new StationPoint[0]);
                }
                Check(king.State == CheckmateHunt.KingState.Frozen && king.Checks == 4 && !king.Mated, "four checks freeze: " + king.Checks + " " + king.State);
                hunt.Step(t += .05, new StationPoint[0], new[] { PawnRushBoard.Center(king.File, king.Rank) });
                Check(king.State == CheckmateHunt.KingState.Free && king.Checks == 0, "an enemy frees it");
                // Same pawns in, same moves out.
                var a = new CheckmateHunt(); var b = new CheckmateHunt();
                var walk = new List<StationPoint>();
                for (int i = 0; i < 400; i++)
                {
                    var pawn = new StationPoint((float)Math.Sin(i * .05) * 7f, 11f + (float)Math.Cos(i * .037) * 8f);
                    a.Step(i * .05, new[] { pawn }, new StationPoint[0]); b.Step(i * .05, new[] { pawn }, new StationPoint[0]);
                }
                for (int i = 0; i < 6; i++) Check(CheckmateHunt.Pack(a.All[i]) == CheckmateHunt.Pack(b.All[i]), "deterministic king " + i);
                var u = new CheckmateHunt.King(); CheckmateHunt.Unpack(CheckmateHunt.Pack(a.All[2]), u);
                Check(u.File == a.All[2].File && u.Rank == a.All[2].Rank && u.Checks == a.All[2].Checks && u.State == a.All[2].State, "pack round trip"); });
            Test("H checkmate hunt: one pawn running at the nearest king finishes within ten minutes", () => {
                var hunt = new CheckmateHunt();
                var me = new StationPoint(0f, 1f);
                double t = 0; const float dt = .05f;
                while (!hunt.Done && t < 600)
                {
                    int target = -1; float best = 1e9f;
                    for (int k = 0; k < 6; k++)
                    {
                        if (hunt.All[k].State == CheckmateHunt.KingState.Captured) continue;
                        float d = PawnRushBoard.Center(hunt.All[k].File, hunt.All[k].Rank).DistanceTo(me);
                        if (d < best) { best = d; target = k; }
                    }
                    var king = hunt.All[target];
                    var goal = PawnRushBoard.Center(king.File, king.Rank);
                    float stop = king.State == CheckmateHunt.KingState.Frozen ? 0f : 1.2f;
                    float dist = goal.DistanceTo(me);
                    if (dist > stop + .01f)
                    {
                        float step = Math.Min(2.5f * dt, dist - stop);
                        me.V += (goal.V - me.V) / dist * step;
                        me.U += (goal.U - me.U) / dist * step;
                    }
                    if (king.State == CheckmateHunt.KingState.Frozen) hunt.TryCapture(target, me, true, 0f);
                    hunt.Step(t += dt, new[] { me }, new StationPoint[0]);
                }
                Check(hunt.Done, "captured " + hunt.Captured + " of 6 in 10 minutes");
                Console.WriteLine("    (one pawn caught all six kings in " + (int)t + " s)"); });
            Test("I check block: seeds 0..999 all give a position; rails off every line, escape off the new lines, same seed same position", () => {
                int doubles = 0, knights = 0, pawns = 0;
                for (int seed = 0; seed < 1000; seed++)
                {
                    var l = CheckBlockGenerator.Generate(seed);
                    Check(l.King.Rank == 7 && l.King.File >= 2 && l.King.File <= 5, "king on c7..f7, seed " + seed);
                    Check(l.Points >= CheckBlockGenerator.MinPoints && l.Points <= CheckBlockGenerator.MaxPoints, "budget " + l.Points);
                    var line = new HashSet<int>();
                    var stand = new List<int>();
                    int rails = 0;
                    foreach (var s in l.Situations)
                    {
                        stand.Add(PawnRushBoard.Index(s.From.File, s.From.Rank));
                        if (s.Kind == CheckBlockGenerator.Kind.Knight) knights++;
                        if (s.Kind == CheckBlockGenerator.Kind.Pawn) pawns++;
                        if (s.Kind != CheckBlockGenerator.Kind.Rail) continue;
                        rails++;
                        foreach (var q in l.LineSquares(s)) line.Add(PawnRushBoard.Index(q.File, q.Rank));
                        Check(l.LineSquares(s).Any(q => q.Is(s.Block.File, s.Block.Rank)), "block square on its line, seed " + seed);
                        bool diagonal = s.DirF != 0 && s.DirR != 0;
                        Check(diagonal ? (s.Shield == PieceKind.Bishop || s.Shield == PieceKind.Queen) : (s.Shield == PieceKind.Rook || s.Shield == PieceKind.Queen || s.Shield == PieceKind.Pawn), "shield moves the rail's way");
                        if (s.Attacker == PieceKind.Queen) Check(s.Shield == (diagonal ? PieceKind.Queen : PieceKind.Rook), "a queen's line needs a heavy shield");
                        if (s.Shield == PieceKind.Pawn) Check(s.Block.Rank > s.RailStart.Rank && s.Block.File == s.RailStart.File, "a pawn only pushes north");
                    }
                    Check(rails == 2 || rails == 3, "rails " + rails);
                    foreach (var s in l.Situations)
                    {
                        if (s.Kind != CheckBlockGenerator.Kind.Rail) continue;
                        Check(!line.Contains(PawnRushBoard.Index(s.RailStart.File, s.RailStart.Rank)) && !line.Contains(PawnRushBoard.Index(s.RailMid.File, s.RailMid.Rank)), "a rail square on a line, seed " + seed);
                        stand.Add(PawnRushBoard.Index(s.RailStart.File, s.RailStart.Rank)); stand.Add(PawnRushBoard.Index(s.RailMid.File, s.RailMid.Rank)); stand.Add(PawnRushBoard.Index(s.Block.File, s.Block.Rank));
                    }
                    Check(stand.Distinct().Count() == stand.Count, "two things on one square, seed " + seed);
                    if (l.Double)
                    {
                        doubles++;
                        Check(l.Escape.File != l.DoubleRook.File && Math.Abs(l.Escape.File - l.DoubleBishop.File) != Math.Abs(l.Escape.Rank - l.DoubleBishop.Rank), "escape square attacked, seed " + seed);
                        Check(Math.Abs(l.Escape.File - l.King.File) <= 1 && Math.Abs(l.Escape.Rank - l.King.Rank) <= 1, "escape next to the king");
                    }
                    var again = CheckBlockGenerator.Generate(seed);
                    Check(again.King.File == l.King.File && again.Situations.Count == l.Situations.Count && again.Double == l.Double
                          && again.Situations.Zip(l.Situations, (x, y) => x.From.Is(y.From.File, y.From.Rank) && x.Kind == y.Kind).All(ok => ok), "same seed, seed " + seed);
                }
                Check(doubles > 50 && knights > 100 && pawns > 50, "every kind comes up: double " + doubles + ", knight " + knights + ", pawn " + pawns); });
            Test("R543 scale: three to six a side, the baseline five; one or two a side play the smallest", () => {
                Check(MiniGameScale.Clamp(1) == 3 && MiniGameScale.Clamp(2) == 3 && MiniGameScale.Clamp(5) == 5 && MiniGameScale.Clamp(9) == 6, "clamp");
                Check(MiniGameScale.Workers(3) == 4 && MiniGameScale.Workers(5) == PawnRushMissions.WorkerCap && MiniGameScale.Workers(6) == 5, "who counts at once");
                // What one player has to do stays about the baseline's share (within a fifth of it).
                Func<Func<int, float>, string, bool> even = (amount, what) =>
                {
                    float share = amount(5) / 5f;
                    for (int n = 3; n <= 6; n++)
                    {
                        float mine = amount(n) / n;
                        if (mine < share * .8f || mine > share * 1.2f) throw new Exception(what + " at " + n + " a side: " + mine + " a player, baseline " + share);
                        if (n > 3 && amount(n) < amount(n - 1)) throw new Exception(what + " shrinks at " + n);
                    }
                    return true;
                };
                even(n => MiniGameScale.PaintBoard(n).CountOf(true), "A squares");
                even(n => MiniGameScale.Planks(n).Planks, "B planks");
                even(n => 1f / MiniGameScale.RopeForce(n), "C rope");
                even(n => MiniGameScale.CapstanTurns(n), "D turns");
                even(n => MiniGameScale.BellTowerCount(n) * MiniGameScale.BellTowerHeight(n), "E metres to climb");
                Check(MiniGameScale.BellTowerCount(5) * MiniGameScale.BellTowerHeight(5) == 6f + 6f + 7.5f + 7.5f + 9f + 9f, "five a side climbs the 45 m the six old towers were");
                even(n => MiniGameScale.EnPassantStatues(n), "F statues");
                even(n => MiniGameScale.KingRoadCells(n) - 1, "G steps");
                even(n => MiniGameScale.CheckmateKings(n), "H kings");
                even(n => MiniGameScale.KnightSquares(n) - 2, "J pieces");
                Check(MiniGameScale.PaintBoard(5).Count == 64 && MiniGameScale.Planks(5) == PlankLayout.Baseline && MiniGameScale.RopeForce(5) == 1f
                      && MiniGameScale.EnPassantStatues(5) == EnPassantRules.Statues && MiniGameScale.KingRoadCells(5) == KingEscortPath.Cells.Length
                      && MiniGameScale.CheckmateKings(5) == CheckmateHunt.Kings && MiniGameScale.KnightSquares(5) == KnightRoute.Squares.Length
                      && MiniGameScale.KnightSquareSize(5) == KnightRoute.SquareSize && MiniGameScale.KingSledCrew(5) == PawnRushMissions.WorkerCap, "five a side is what there was"); });
            Test("R543 A paint board: as many light squares as dark, the 8 x 8 where it was, bigger boards centred on it", () => {
                for (int n = 3; n <= 6; n++)
                {
                    var g = MiniGameScale.PaintBoard(n);
                    Check(g.CountOf(true) == g.CountOf(false) && g.CountOf(true) * 2 == g.Count, "equal halves at " + n);
                    Check(Math.Abs(g.V0 + g.V1) < 1e-4f && Math.Abs((g.U0 + g.U1) * .5f - 11f) < 1e-4f && g.U0 >= 2f - 1e-4f && g.U1 <= 20f + 1e-4f && g.HalfWidth <= 11.25f + 1e-4f, "centred, on the dais, at " + n);
                    for (int i = 0; i < g.Count; i++)
                    {
                        int f = g.FileOf(i), r = g.RankOf(i);
                        Check(g.Index(f, r) == i && g.OnBoard(f, r) && g.SquareAt(g.Center(f, r), out int f2, out int r2) && f2 == f && r2 == r, "index and square at " + n);
                    }
                    Check(g.Count <= 80, "the block's ten bytes of squares at " + n);
                }
                var eight = MiniGameScale.PaintBoard(5);
                for (int f = 0; f < 8; f++)
                    for (int r = 1; r <= 8; r++)
                        Check(eight.Index(f, r) == PawnRushBoard.Index(f, r) && eight.Center(f, r).DistanceTo(PawnRushBoard.Center(f, r)) < 1e-5f && eight.IsLight(f, r) == PawnRushBoard.IsLight(f, r), "the 8 x 8 board");
                Check(MiniGameScale.PaintBoard(3).Files == 6 && MiniGameScale.PaintBoard(4).Ranks == 6 && MiniGameScale.PaintBoard(6).Files == 10, "6 x 6, 8 x 6, 10 x 8"); });
            Test("R543 B planks: two a player over the same 10 m chasm - far rows laid from the start, or six shorter rows", () => {
                int[] planks = { 6, 8, 10, 12 };
                for (int n = 3; n <= 6; n++)
                {
                    var l = PlankLayout.For(n);
                    Check(l.Planks == planks[n - 3] && l.PerPile * 2 == l.Planks && Math.Abs(l.Chasm1 - 16f) < 1e-4f, "planks at " + n + ": " + l.Planks);
                    Check(l.Count(l.PreLaid) == l.Slots - l.Planks && l.Carried(l.PreLaid) == 0 && l.Carried(l.AllLaid) == l.Planks, "laid from the start at " + n);
                    var board = new PlankBoard(l);
                    Check(board.Laid == l.PreLaid && board.Carried == 0 && board.Stock(0) == l.PerPile && board.Stock(1) == l.PerPile && board.TopOf(1) == l.Planks - 1, "a fresh board at " + n);
                    // Every plank lying on a floor is the longest block.
                    for (int i = 0; i < l.Planks; i++) { board.State[i] = PlankState.Lying; board.X[i] = i - 5f; board.Z[i] = 20f - i; board.Turned[i] = (i & 1) != 0; }
                    board.Laid = l.PreLaid | 1;
                    var buffer = new byte[PawnRushNetState.BlockBytes];
                    var w = new NetBlock(buffer, 0, buffer.Length);
                    board.Write(ref w);
                    Check(w.Written == board.Bytes && board.Bytes <= PlankBoard.MaxBytes, "bytes at " + n + ": " + w.Written);
                    var back = new PlankBoard(l);
                    var r = new NetBlock(buffer, 0, w.Written);
                    back.Read(ref r);
                    Check(back.Laid == board.Laid && back.Carried == 1, "laid round trip at " + n);
                    for (int i = 0; i < l.Planks; i++)
                        Check(back.State[i] == PlankState.Lying && back.Turned[i] == board.Turned[i] && Math.Abs(back.X[i] - board.X[i]) < 1e-4f && Math.Abs(back.Z[i] - board.Z[i]) < 1e-4f, "plank " + i + " at " + n);
                    // A block that says nothing is laid still has the rows that were laid from the start.
                    var none = new PlankBoard(l);
                    var zero = new NetBlock(new byte[PawnRushNetState.BlockBytes], 0, 7);
                    none.Read(ref zero);
                    Check(none.Laid == l.PreLaid, "the rows laid from the start stay at " + n);
                }
                var three = PlankLayout.For(3);
                Check(three.PreLaid == 0x3C0 && !three.OverChasm(0f, 13f, three.PreLaid) && three.OverChasm(0f, 11f, three.PreLaid) && three.Snap(-1f, 13f, three.PreLaid) == -1
                      && three.Snap(-1f, 11f, three.PreLaid) == 4, "three a side: rows four and five are there");
                var six = PlankLayout.For(6);
                Check(six.Rows == 6 && Math.Abs(six.Row - 10f / 6f) < 1e-5f && six.PreLaid == 0 && Math.Abs(six.SlotZ(11) - (16f - 5f / 6f)) < 1e-4f && six.Snap(1f, 6.2f, 0) == 1
                      && six.OverChasm(0f, 15.9f, 0x3FF) && !six.OverChasm(0f, 15.9f, 0xFFF), "six a side: six rows of 1.67 m");
                Check(PlankBoard.MaxBytes == 31 && PlankBoard.MaxBytes <= PawnRushNetState.BlockBytes, "the block fits"); });
            Test("R543 C rope and D capstan: a small team's pull is worth more, a big team's less; the bars turn at 70 %", () => {
                Check(Math.Abs(DrawbridgeRope.Speed(3, 0, MiniGameScale.RopeForce(3)) - DrawbridgeRope.Speed(5, 0)) < 1e-4f, "three of three pull as five did");
                Check(DrawbridgeRope.Speed(4, 0, MiniGameScale.RopeForce(6)) < DrawbridgeRope.Speed(4, 0) && DrawbridgeRope.Speed(4, 0, MiniGameScale.RopeForce(4)) > DrawbridgeRope.Speed(4, 0), "heavier at six, lighter at four");
                Check(DrawbridgeRope.Speed(9, 0, MiniGameScale.RopeForce(3)) <= DrawbridgeRope.MaxSpeed + 1e-6f, "never faster than the rope runs");
                Check(Math.Abs(DrawbridgeRope.Speed(1, 5, MiniGameScale.RopeForce(6)) - DrawbridgeRope.MinSpeed) < 1e-6f && DrawbridgeRope.Speed(0, 0, 2f) == 0f, "one puller always gets somewhere");
                Check(Math.Abs(MiniGameScale.CapstanTurnRate - .091f) < 1e-5f && MiniGameScale.CapstanSpeedPushers == 4, "70 % of 0.13 turns a second a pusher");
                for (int n = 3; n <= 6; n++) Check(MiniGameScale.CapstanBars(n) == n, "a bar a player at " + n);
                // Five a side: six turns at the new top speed take about what eight took at the old one.
                float before = 8f / (4 * .13f), now = MiniGameScale.CapstanTurns(5) / (MiniGameScale.CapstanSpeedPushers * MiniGameScale.CapstanTurnRate);
                Check(Math.Abs(now - before) < 2f, "the baseline's time: " + before + " s -> " + now + " s"); });
            Test("R543 E bell towers: a 9 m tower a player, every top out of a dive's reach of every other, clear of the room's gallery and chandeliers", () => {
                Check(BellTowers.DiveReach(0f) > 5f && BellTowers.DiveReach(0f) < BellTowers.MinGap - .5f, "a dive's reach on the level: " + BellTowers.DiveReach(0f));
                Check(BellTowers.DiveReach(1.5f) > BellTowers.MinGap, "a top 1.5 m lower would be in reach: " + BellTowers.DiveReach(1.5f));
                for (int n = 3; n <= 6; n++)
                {
                    var towers = BellTowers.Layout(n);
                    Check(towers.Length == n, "a tower a player at " + n);
                    for (int i = 0; i < towers.Length; i++)
                    {
                        var t = towers[i];
                        float half = BellTowers.Top * .5f;
                        Check(t.Height == 9f, "9 m, every one, at " + n);
                        float south = t.U - half - (BellTowers.Storeys(t.Height) - 1) * BellTowers.Ledge;
                        Check(t.V - half >= BellTowers.WallSide - 1e-4f && t.V + half <= BellTowers.AisleSide + 1e-4f && south >= 2.5f && t.U + half <= 17.5f,
                              "off the gallery's side, inside the station, clear of the entrance band and the gate, at " + n + ": " + t.V + ", " + t.U);
                        // Nothing of a top under a chandelier: a pawn on it (its head 10 m up, higher when it jumps) would stand in the lights.
                        foreach (float cu in BellTowers.ChandelierU)
                        {
                            float dv = Math.Max(0f, Math.Abs(t.V) - half), du = Math.Max(0f, Math.Abs(t.U - cu) - half);
                            Check(t.Height + 1.3f < BellTowers.ChandelierBottom || Math.Sqrt(dv * dv + du * du) >= BellTowers.ChandelierRadius, "under a chandelier at " + n + ": " + t.V + ", " + t.U);
                        }
                        for (int k = i + 1; k < towers.Length; k++)
                            Check(BellTowers.Gap(t, towers[k]) >= BellTowers.MinGap - 1e-4f, "towers " + i + " and " + k + " at " + n + ": " + BellTowers.Gap(t, towers[k]) + " m apart");
                    }
                }
                Check(BellTowers.Storeys(7.5f) == 3 && BellTowers.Storeys(9f) == 3 && BellTowers.Storeys(6f) == 2 && BellTowers.Top == 2f, "storeys of 3 m, tops of 2 m");
                // The towers there were (v +-5; u 5, 11, 17; 6, 7.5 and 9 m) were in reach of each other: 3.6 m apart down a row's step.
                Check(new BellTowers.Tower(-5f, 17f, 9f).Height - 7.5f == 1.5f && 6f - 2.4f < BellTowers.DiveReach(1.5f), "what was wrong before"); });
            Test("R543 F en passant: ten to nineteen statues, every one its own place and its own moment", () => {
                int[] count = { 10, 13, 16, 19 };
                for (int n = 3; n <= 6; n++)
                {
                    var field = EnPassantField.For(n);
                    Check(field.Count == count[n - 3], "statues at " + n);
                    var offsets = new HashSet<float>();
                    for (int i = 0; i < field.Count; i++)
                    {
                        offsets.Add(field.Offset(i));
                        var home = field.Home(i);
                        Check(Math.Abs(home.V) <= 6.75f + 1e-4f && home.U >= 6f - 1e-4f && home.U <= 19.2f + 1e-4f && home.U - EnPassantRules.Advance >= 3.5f, "home on the floor at " + n);
                        for (int k = i + 1; k < field.Count; k++)
                        {
                            var other = field.Home(k);
                            Check(Math.Abs(other.V - home.V) >= 2.2f || Math.Abs(other.U - home.U) >= 3.3f - 1e-3f, "statues " + i + " and " + k + " apart at " + n);
                        }
                    }
                    Check(offsets.Count == field.Count, "offsets at " + n);
                }
                for (int i = 0; i < 16; i++)
                {
                    var was = new StationPoint(EnPassantRules.Columns[i % 4], EnPassantRules.Homes[i / 4]);
                    Check(EnPassantField.Baseline.Home(i).DistanceTo(was) < 1e-5f && Math.Abs(EnPassantField.Baseline.Offset(i) - ((i * 7) % 16) / 16f * 14f) < 1e-5f, "sixteen as they stood, " + i);
                }
                Check(Math.Abs(EnPassantField.For(3).Home(8).V + 2.25f) < 1e-4f && Math.Abs(EnPassantField.For(4).Home(12).V) < 1e-4f && Math.Abs(EnPassantField.For(6).Home(17).V) < 1e-4f
                      && Math.Abs(EnPassantField.For(6).HomeU(1) - 9.3f) < 1e-4f, "a short last row in the middle, five rows 3.3 m apart"); });
            Test("R543 J knight's road: six to nine squares, a knight's move apart, none touching another", () => {
                int[] squares = { 6, 7, 8, 9 };
                for (int n = 3; n <= 6; n++)
                {
                    var road = KnightRoad.For(n);
                    Check(road.Squares.Length == squares[n - 3] && road.Pieces == squares[n - 3] - 2 && road.PieceKinds.Length == road.Pieces, "squares at " + n);
                    Check(road.Joined() && road.Spread(), "joined and apart at " + n);
                    Check(road.Squares[0] == "d1" && (road.Squares[road.Squares.Length - 1] == "f8" || road.Squares[road.Squares.Length - 1] == "e8"), "entrance to gate at " + n);
                    Check(road.SquareSize <= PawnRushBoard.Cell - .04f && road.SquareSize >= 1.8f, "a square's width at " + n);
                    Check(road.SquareAt(road.Center(2)) == 2 && road.SquareAt(new StationPoint(9.9f, 10f)) < 0, "squares and water at " + n);
                    Check(road.PieceKinds[road.Pieces - 1] == PieceKind.King && road.PieceKinds[0] == PieceKind.Pawn, "a pawn first, the king last, at " + n);
                }
                Check(KnightRoad.Baseline.Squares == KnightRoute.Squares && KnightRoad.Baseline.PieceSquares.SequenceEqual(KnightRoute.PieceSquares) && KnightRoad.For(5) == KnightRoad.Baseline, "eight as they were");
                Check(!new KnightRoad(new[] { "d1", "e3", "d5", "f4", "e6", "f8" }, new[] { PieceKind.Pawn, PieceKind.Knight, PieceKind.Rook, PieceKind.King }, 2f).Spread(), "corner to corner is touching"); });
            Test("R543 G king's road: 2,000 seeds a team size - joined, never along itself, rooks and garages fit, a different road nearly every time", () => {
                Check(KingRoad.Baseline.Connected() && KingRoad.Baseline.Apart() && KingRoad.Baseline.RooksFit() && KingRoad.Baseline.TurnCount == 5, "the old road keeps the rules");
                int[] cells = { 11, 13, 15, 17 }, sand = { 2, 3, 4, 5 }, rooks = { 1, 2, 2, 3 };
                for (int n = 3; n <= 6; n++)
                {
                    var seen = new HashSet<string>();
                    int most = 0;
                    for (int seed = 0; seed < 2000; seed++)
                    {
                        var road = KingRoad.Generate(seed, n);
                        string id = string.Join("", road.Cells) + "|" + string.Join("", road.Sand) + "|" + string.Join("", road.RookCells) + string.Join("", road.RookGarages);
                        Check(road != KingRoad.Baseline, "no road for seed " + seed + " at " + n);
                        Check(road.Cells.Length == cells[n - 3] && road.Sand.Length == sand[n - 3] && road.Rooks == rooks[n - 3], "the size's numbers, seed " + seed + " at " + n + ": " + id);
                        Check(road.Cells[0] == "d1" && road.Cells[1] == "d2" && road.Cells[road.Cells.Length - 1] == "e8", "d1 north to e8, seed " + seed + ": " + id);
                        Check(road.Connected() && road.Apart(), "joined and apart, seed " + seed + ": " + id);
                        Check(road.RooksFit(), "rooks and garages, seed " + seed + ": " + id);
                        Check(road.TurnCount >= 3 && road.TurnCount <= road.Cells.Length / 3 + 1, "turns " + road.TurnCount + ", seed " + seed);
                        for (int k = 0; k < road.Rooks; k++)
                        {
                            Check(road.RookStop(k) > (k == 0 ? 0f : road.RookStop(k - 1)) && Array.IndexOf(road.Sand, road.RookCells[k]) < 0, "rook " + k + " stops the sled in turn, seed " + seed);
                            Check(Array.IndexOf(road.Sand, road.RookGarages[k]) < 0, "a garage is no sand, seed " + seed);
                        }
                        foreach (var c in road.Sand) Check(road.CellIndex(c) >= 2 && road.CellIndex(c) < road.Cells.Length - 2, "sand in the middle, seed " + seed);
                        for (int i = 1; i < road.Cells.Length; i++) Check(road.Cells[i][1] != '1', "rank 1 only at the start, seed " + seed);
                        // Files b..g only (the hall's columns and rope posts stand along a and h), a garage never on rank 1.
                        foreach (var c in road.Cells) Check(c[0] >= 'b' && c[0] <= 'g', "the road off files a and h, seed " + seed + ": " + id);
                        foreach (var c in road.RookGarages) Check(c[0] >= 'b' && c[0] <= 'g' && c[1] != '1', "a garage off files a and h and rank 1, seed " + seed + ": " + id);
                        var again = KingRoad.Generate(seed, n);
                        Check(string.Join("", again.Cells) + "|" + string.Join("", again.Sand) + "|" + string.Join("", again.RookCells) + string.Join("", again.RookGarages) == id, "same seed, same road");
                        seen.Add(id);
                        most = Math.Max(most, road.TurnCount);
                    }
                    Check(seen.Count > 250, "roads at " + n + " a side: " + seen.Count + " different in 2,000");
                    Console.WriteLine("    (" + n + " a side: " + seen.Count + " different roads in 2,000 seeds, up to " + most + " turns)");
                }
                Check(KingEscortPath.Speed(4, 0, false, 4) == 1.1f && KingEscortPath.Speed(3, 0, false, 3) == 1.1f && KingEscortPath.Speed(5, 0, false, 5) == 1.1f && KingEscortPath.Speed(9, 0, false, 5) == 1.1f, "the whole crew at full speed");
                Check(Math.Abs(KingEscortPath.Speed(2, 0, false, 4) - .85f) < 1e-5f && Math.Abs(KingEscortPath.Speed(1, 2, false, 4)) < 1e-6f && KingEscortPath.Speed(1, 0, false, 3) > KingEscortPath.Speed(1, 0, false, 4)
                      && KingEscortPath.Speed(1, 0, false, 5) < KingEscortPath.Speed(1, 0, false, 4), "lighter for three, heavier for six"); });
            Test("R543 H kings and I checks: four to seven kings on their own squares; a position for every seed at every team size", () => {
                for (int n = 3; n <= 6; n++)
                {
                    var hunt = new CheckmateHunt(MiniGameScale.CheckmateKings(n));
                    Check(hunt.Count == n + 1 && hunt.All.Length == hunt.Count && !hunt.Done, "kings at " + n);
                    var on = new HashSet<int>();
                    foreach (var k in hunt.All) { Check(!CheckmateHunt.IsPillar(k.File, k.Rank), "not on a pillar"); on.Add(PawnRushBoard.Index(k.File, k.Rank)); }
                    Check(on.Count == hunt.Count, "a square each at " + n);
                    foreach (var k in hunt.All) k.State = CheckmateHunt.KingState.Captured;
                    Check(hunt.Done && hunt.Captured == hunt.Count, "all taken at " + n);
                }
                int[] least = { 1, 2, 2, 3 }, most = { 2, 2, 3, 3 };
                float[] work = new float[4];
                for (int n = 3; n <= 6; n++)
                {
                    var budget = CheckBlockGenerator.BudgetFor(n);
                    for (int seed = 0; seed < 65536; seed += n == 5 ? 1 : 7)
                    {
                        var l = CheckBlockGenerator.Generate(seed, n);
                        int rails = l.Situations.Count(s => s.Kind == CheckBlockGenerator.Kind.Rail);
                        Check(rails >= least[n - 3] && rails <= most[n - 3] && l.Situations.Count <= 6, "lines to block at " + n + ", seed " + seed + ": " + rails);
                        Check(l.Points >= budget.MinPoints && l.Points <= budget.MaxPoints, "budget at " + n + ", seed " + seed + ": " + l.Points);
                        work[n - 3] += l.Points;
                    }
                }
                Check(work[0] / (65536 / 7 + 1) < work[1] / (65536 / 7 + 1) && work[1] / (65536 / 7 + 1) < work[2] / 65536 && work[2] / 65536 < work[3] / (65536 / 7 + 1), "more to do the bigger the team");
                Console.WriteLine("    (a position's points, 3 to 6 a side: " + (int)(work[0] / (65536 / 7 + 1)) + " / " + (int)(work[1] / (65536 / 7 + 1)) + " / " + (int)(work[2] / 65536) + " / " + (int)(work[3] / (65536 / 7 + 1)) + ")");
                for (int seed = 0; seed < 300; seed++)
                {
                    var a = CheckBlockGenerator.Generate(seed); var b = CheckBlockGenerator.Generate(seed, 5);
                    Check(a.King.File == b.King.File && a.Points == b.Points && a.Situations.Count == b.Situations.Count && a.Double == b.Double, "five a side is the position it always was, seed " + seed);
                } });
            Test("Promotion pads: two different pieces a round, the same for every seed holder, never the pawn or the king", () => {
                var seen = new HashSet<PieceKind>();
                for (int seed = 0; seed < 400; seed++)
                {
                    var d = PawnRushPromotion.Choose(seed);
                    var again = PawnRushPromotion.Choose(seed);
                    Check(d.First != d.Second, "the same piece twice, seed " + seed);
                    Check(d.First == again.First && d.Second == again.Second, "not repeatable, seed " + seed);
                    Check(PawnRushPromotion.Gives(d.First) && PawnRushPromotion.Gives(d.Second), "a piece a pad cannot give, seed " + seed);
                    seen.Add(d.First); seen.Add(d.Second);
                }
                Check(seen.Count == 4 && !seen.Contains(PieceKind.Pawn) && !seen.Contains(PieceKind.King), "all four pieces come up");
                Check(PawnRushPromotion.CanPromote(PieceKind.Pawn) && !PawnRushPromotion.CanPromote(PieceKind.Rook), "only a pawn promotes"); });
            Test("Crown platform: more than half the team's players on it at once wins", () => {
                Check(PawnRushFinish.Needed(1) == 1 && PawnRushFinish.Needed(2) == 2 && PawnRushFinish.Needed(3) == 2 && PawnRushFinish.Needed(4) == 3
                      && PawnRushFinish.Needed(5) == 3 && PawnRushFinish.Needed(6) == 4, "majorities");
                Check(PawnRushFinish.Winner(0, 3, 0, 2) == PawnRushFinish.None, "nobody");
                Check(PawnRushFinish.Winner(2, 3, 1, 2) == 0, "white 2 of 3");
                Check(PawnRushFinish.Winner(1, 3, 2, 2) == 1, "black 2 of 2");
                Check(PawnRushFinish.Winner(1, 2, 0, 3) == PawnRushFinish.None, "half is not more than half");
                Check(PawnRushFinish.Winner(3, 5, 2, 3) == 0 && PawnRushFinish.Winner(2, 3, 3, 4) == 1, "both at once: more on the platform");
                Check(PawnRushFinish.Winner(2, 3, 2, 3) == PawnRushFinish.None, "both at once, level: nobody yet");
                Check(PawnRushFinish.Winner(0, 0, 0, 3) == PawnRushFinish.None && !PawnRushFinish.Reached(1, 0), "a team with no players never wins");
                Check(PawnRushFinish.OnPlatform(2f, 0.9f, 2f) && !PawnRushFinish.OnPlatform(2.2f, 0.9f, 2.2f) && !PawnRushFinish.OnPlatform(0f, -1f, 0f)
                      && !PawnRushFinish.OnPlatform(0f, 4f, 0f), "on the platform");
                Check(PawnRushFinish.OnPlatform(3f, 0.9f, 2f, 4.25f) && !PawnRushFinish.OnPlatform(3.1f, 0.9f, 3.1f, 4.25f)
                      && !PawnRushFinish.OnPlatform(3f, 0.9f, 2f), "a wider platform (the 8.5 m throne circle)"); });
            Test("Game mode catalog: unique lobby keys, a playable default, safe lookups", () => {
                Check(GameModes.All.Select(m => m.Key).Distinct().Count() == GameModes.All.Length, "duplicate key");
                Check(GameModes.All.All(m => m.Key.Length > 0 && m.Key.Length <= 16 && m.Key.All(ch => ch >= 'a' && ch <= 'z')), "lobby keys are short lowercase words");
                Check(GameModes.Default.Playable && GameModes.Find("kingrush") == GameModes.KingRush && GameModes.KingRush.Scene == "PawnRush_Round", "default (the 폰 러쉬 match is the section round since R508; course 01 before)");
                Check(GameModes.Find("nope") == null && GameModes.Resolve("") == GameModes.Default && GameModes.IndexOf("swordfight") == 1, "lookup");
                Check(GameModes.All.All(m => m.Playable == (m.Scene.Length > 0)), "playable means a scene"); });
            Test("Link simulator delays in order and drops about the configured share", () => {
                var sim=new LinkSimulator<int>(7){Profile=new LinkProfile{RoundTripMs=200}}; var got=new List<int>();
                for(int n=0;n<5;n++) sim.Push(n,n*0.01);
                sim.Release(0.099,got.Add); Check(got.Count==0,"released early");
                sim.Release(0.125,got.Add); Check(got.SequenceEqual(new[]{0,1,2}),"order/partial");
                sim.Release(1,got.Add); Check(got.Count==5&&sim.Count==0,"rest");
                var lossy=new LinkSimulator<int>(11){Profile=new LinkProfile{LossPercent=10}}; int kept=0;
                for(int n=0;n<10000;n++) if(lossy.Push(n,0)) kept++;
                Check(kept>8700&&kept<9300,"loss rate "+kept);
                Check(!new LinkProfile().Active&&LinkProfile.Presets.Skip(1).All(p=>p.Active),"presets"); });
            Test("Link simulator jitter lets later packets overtake, still releasing each once", () => {
                var sim=new LinkSimulator<int>(5){Profile=new LinkProfile{RoundTripMs=100,JitterMs=60}}; var got=new List<int>();
                for(int n=0;n<400;n++) sim.Push(n,n*0.005);
                sim.Release(10,got.Add);
                Check(got.Count==400&&got.Distinct().Count()==400,"each once");
                bool overtaken=false; for(int i=1;i<got.Count;i++) if(got[i]<got[i-1]) overtaken=true;
                Check(overtaken,"jitter reorders"); });
            Test("Skill presses: 1000 presses over 10% loss and reordering arrive exactly 1000 times (C1)", () => {
                foreach (var profile in new[]{ new LinkProfile{RoundTripMs=200,LossPercent=5,JitterMs=30}, new LinkProfile{RoundTripMs=300,LossPercent=10,JitterMs=50} })
                {
                    var link=new LinkSimulator<PressPacket>(17){Profile=profile};
                    var rng=new Random(3); byte count=0; int pressed=0, taken=0; uint seq=0, newest=0; byte consumed=0; bool first=true;
                    for(int tick=0; pressed<1000 || tick<pressed*12+600; tick++)
                    {
                        double now=tick/60.0;
                        if(pressed<1000 && rng.Next(8)==0) { count=PressCount.Add(count); pressed++; }
                        link.Push(new PressPacket{Seq=++seq,Count=count},now);
                        link.Release(now,p => {
                            if(!first && !MotionProtocol.Newer(p.Seq,newest)) return;   // an older packet overtaken: its count is stale
                            if(first) { first=false; consumed=0; }
                            newest=p.Seq; taken+=PressCount.Take(p.Count,ref consumed); });
                    }
                    link.Release(1e9,p => { if(MotionProtocol.Newer(p.Seq,newest)) { newest=p.Seq; taken+=PressCount.Take(p.Count,ref consumed); } });
                    Check(taken==1000,$"{profile}: {taken} presses for 1000");
                }
                byte wrap=255; Check(PressCount.Take(0,ref wrap)==1&&PressCount.Take(0,ref wrap)==0,"count wraps"); });
            Test("Skill moments: 1000 over 10% loss both ways, reordering and repeats are drawn exactly once each (C5)", () => {
                foreach (int loss in new[]{5,10})
                {
                    var down=new LinkSimulator<byte[]>(21){Profile=new LinkProfile{RoundTripMs=300,LossPercent=loss,JitterMs=50}};
                    var up=new LinkSimulator<ushort>(22){Profile=new LinkProfile{RoundTripMs=300,LossPercent=loss,JitterMs=50}};
                    var log=new SkillEventLog(); var inbox=new SkillEventInbox(); var drawn=new Dictionary<ushort,int>();
                    var rng=new Random(9); ushort hostAck=0; bool hostAckSet=false; int made=0; var batch=new List<SkillWireEvent>(); var packet=new SkillWirePacket(); var read=new SkillWirePacket();
                    for(int tick=0; tick<60*90; tick++)
                    {
                        double now=tick/60.0; uint ms=(uint)(tick*1000/60);
                        // Bursts: up to 6 moments at once (a rook through a crowd), 1000 in all.
                        if(made<1000 && rng.Next(6)==0) { int n=Math.Min(1000-made,1+rng.Next(6)); for(int i=0;i<n;i++) { log.Add(new SkillWireEvent{Kind=(byte)(made%17),By=1,Target=2,X=made},ms); made++; } }
                        log.Expire(ms);
                        if(tick%2==0)   // 30 Hz, like the snapshots
                        {
                            log.After(hostAckSet?hostAck:(ushort)(log.First-1),SkillWire.EventsThatFit(1011,4,2,1,999),batch);
                            packet.Clear(); packet.HostTimeMs=ms; packet.FirstSeq=log.First; packet.Events.AddRange(batch);
                            for(int i=0;i<4;i++) packet.States.Add(new SkillWireState{Pawn=(byte)i,Stage=1});
                            packet.Wires.Add(new SkillWireObject{Id=1}); packet.Wires.Add(new SkillWireObject{Id=2}); packet.Barricades.Add(new SkillWireBarricade());
                            var ms2=new System.IO.MemoryStream(); var w=new System.IO.BinaryWriter(ms2); SkillWire.Write(w,packet); w.Flush();
                            var bytes=ms2.ToArray(); Check(bytes.Length<=1011,"packet too big "+bytes.Length);
                            down.Push(bytes,now);
                        }
                        down.Release(now,b => {
                            var r=new System.IO.BinaryReader(new System.IO.MemoryStream(b)); Check(SkillWire.Read(r,b.Length,read),"read back");
                            inbox.Window(read.FirstSeq);
                            foreach(var e in read.Events) if(inbox.Accept(e.Seq)) drawn[e.Seq]=drawn.TryGetValue(e.Seq,out int c)?c+1:1; });
                        if(inbox.Started) up.Push(inbox.Acked,now);   // 60 Hz inputs carry the confirmation
                        up.Release(now,a => { if(!hostAckSet||SkillWire.Newer(a,hostAck)) { hostAck=a; hostAckSet=true; } });
                    }
                    Check(made==1000,"made all");
                    Check(drawn.Count==1000,$"loss {loss}%: drawn {drawn.Count} of 1000");
                    Check(drawn.Values.All(c=>c==1),"drawn twice");
                    for(ushort s=1;s<=1000;s++) Check(drawn.ContainsKey(s),"missed "+s);
                    // R112: the test screen's counters say the same.
                    Check(inbox.Missed==0,$"loss {loss}%: {inbox.Missed} counted as missed");
                    Check(inbox.Repeats>0,"the resends until confirmed show as repeats");
                } });
            Test("Skill moments: a late joiner starts from what is kept and gives up on what is gone", () => {
                var log=new SkillEventLog(); for(int i=0;i<30;i++) log.Add(new SkillWireEvent{Kind=1},(uint)(i*100));
                log.Expire(3000);   // older than 1.5 s are dropped: 15 left (1500..2900 ms)
                Check(log.Count==15&&log.First==16,"kept "+log.Count+" from "+log.First);
                var inbox=new SkillEventInbox(); Check(!inbox.Accept(16),"nothing before the first window");
                inbox.Window(log.First); Check(inbox.Acked==15,"starts before the first kept");
                Check(inbox.Accept(17)&&inbox.Acked==15,"a gap holds the confirmation");
                Check(inbox.Accept(16)&&inbox.Acked==17,"filled");
                Check(!inbox.Accept(16)&&!inbox.Accept(17),"repeats");
                inbox.Window(40); Check(inbox.Acked==39,"moments the host gave up on are skipped");
                Check(inbox.Accept(41)&&!inbox.Accept(30),"older than the window");
                // R112: 18..39 never came (22), and 16, 17 and 30 came again or too late (3).
                Check(inbox.Missed==22&&inbox.Repeats==3,$"counted {inbox.Missed} missed, {inbox.Repeats} repeats");
                inbox.Clear(); Check(inbox.Missed==0&&inbox.Repeats==0,"cleared"); });
            Test("Skills packet: twelve busy pieces, twelve wires and a burst of moments fit in 1024 bytes (C12)", () => {
                const int header=4+1+8;   // the ragdoll protocol's magic, type and match id
                int fit=SkillWire.EventsThatFit(1024-header,12,12,4,999);
                Check(fit>=8,"room for at least eight moments next to everything, got "+fit);
                Check(header+SkillWire.Size(12,12,4,fit)<=1024,"over 1024");
                Check(SkillWire.Size(12,0,0,0)<1024-header,"twelve states alone");
                Check(SkillWire.StateBytes==33&&SkillWire.EventBytes==32&&SkillWire.WireBytes==20,"sizes as documented (R541: 29, 28, 18 before)"); });
            Test("Skills packet round trip keeps every field within its step and rejects bad packets", () => {
                var p=new SkillWirePacket{HostTimeMs=123456,FirstSeq=65530};
                p.States.Add(new SkillWireState{Pawn=SkillWire.Ref(1,3),Stage=2,Flags=0xA5,Flags2=3,StageTime=1.234f,Cooldown=7.25f,CooldownTotal=12f,Dx=0.6f,Dy=0.8f,Px=-12.345f,Py=1.5f,Pz=33.3f,Qx=4,Qy=-0.5f,Qz=79,Target=SkillWire.Ref(0,5),Other=SkillWire.None,A=2.5f,B=25.5f,C=200,D=7});
                p.Wires.Add(new SkillWireObject{Id=9,Owner=SkillWire.Ref(0,1),X=3,Y=0.02f,Z=-4,Yaw=271,Age=2.5f,Flags=1,Trips=1,Along0=0.33f,Along1=0.5f,Out0=0.62f,Out1=-0.31f});
                p.Barricades.Add(new SkillWireBarricade{Index=0,Standing=false,RegrowLeft=3.4f});
                p.Events.Add(new SkillWireEvent{Seq=65531,TimeMs=123000,Mode=SkillWire.ModeRetiredQueenHill,Kind=9,By=SkillWire.Ref(1,3),Target=SkillWire.None,X=1,Y=2,Z=3,Dx=0,Dy=0,Dz=-1,Ux=-5,Uy=0,Uz=5,Count=3,Size=6.5f,Source=9});
                var ms=new System.IO.MemoryStream(); var w=new System.IO.BinaryWriter(ms); SkillWire.Write(w,p); w.Flush(); var b=ms.ToArray();
                Check(b.Length==SkillWire.Size(1,1,1,1),"length");
                var q=new SkillWirePacket(); Check(SkillWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(b)),b.Length,q),"read");
                var s=q.States[0]; var o=q.Wires[0]; var e=q.Events[0];
                Check(s.Pawn==SkillWire.Ref(1,3)&&s.Stage==2&&s.Flags==0xA5&&s.Flags2==3&&Math.Abs(s.StageTime-1.23f)<0.006f,"state head");
                Check(Math.Abs(s.Cooldown-7.25f)<0.051f&&Math.Abs(s.CooldownTotal-12f)<0.01f&&Math.Abs(s.Dx-0.6f)<0.01f&&Math.Abs(s.Dy-0.8f)<0.01f,"state times, dir");
                Check(Math.Abs(s.Px+12.345f)<0.006f&&Math.Abs(s.Pz-33.3f)<0.006f&&Math.Abs(s.Qz-79f)<0.006f,"state points");
                Check(s.Target==SkillWire.Ref(0,5)&&s.Other==SkillWire.None&&Math.Abs(s.A-2.5f)<0.01f&&Math.Abs(s.B-25.5f)<0.01f&&s.C==200&&s.D==7,"state slots");
                Check(o.Id==9&&Math.Abs(o.Yaw-271f)<1.5f&&Math.Abs(o.Age-2.5f)<0.006f&&Math.Abs(o.Out0-0.62f)<0.011f&&Math.Abs(o.Out1+0.31f)<0.011f&&Math.Abs(o.Along0-0.33f)<0.003f,"wire");
                Check(!q.Barricades[0].Standing&&Math.Abs(q.Barricades[0].RegrowLeft-3.4f)<0.051f,"barricade");
                Check(e.Seq==65531&&e.TimeMs==123000&&e.Mode==1&&e.Kind==9&&Math.Abs(e.Dz+1f)<0.01f&&Math.Abs(e.Ux+5f)<0.006f&&e.Count==3&&Math.Abs(e.Size-6.5f)<0.006f&&e.Source==9,"event");
                Check(SkillWire.Newer(2,65530)&&!SkillWire.Newer(65530,2),"seq wraps");
                var bad=(byte[])b.Clone(); bad[6]=99;   // 99 states
                Check(!SkillWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(bad)),bad.Length,q),"too many states");
                Check(!SkillWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(b,0,b.Length-1)),b.Length-1,q),"short");
                var badStage=(byte[])b.Clone(); badStage[SkillWire.HeaderBytes+1]=9;
                Check(!SkillWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(badStage)),badStage.Length,q),"stage out of range");
                SkillWire.Place(SkillWire.Ref(1,5),out int team,out int slot); Check(team==1&&slot==5&&SkillWire.Ref(2,0)==SkillWire.None,"roster places"); });
            Test("Skills packet (R541): points far down a seven-section round come back to the millimetre (pinned at 320 m before)", () => {
                var p=new SkillWirePacket{HostTimeMs=1,FirstSeq=1};
                p.States.Add(new SkillWireState{Pawn=SkillWire.Ref(1,3),Stage=2,Px=-2000.25f,Py=15.2f,Pz=580.125f,Qx=12,Qy=-0.5f,Qz=-579.5f,Target=SkillWire.None,Other=SkillWire.None});
                p.Wires.Add(new SkillWireObject{Id=9,Owner=SkillWire.Ref(0,1),X=4.5f,Y=7f,Z=566.75f});
                p.Events.Add(new SkillWireEvent{Seq=1,TimeMs=1,Mode=SkillWire.ModeRetiredQueenHill,Kind=9,By=SkillWire.Ref(1,3),Target=SkillWire.None,X=-3,Y=15,Z=575.5f,Dz=-1,Ux=1,Uy=2,Uz=600.25f,Count=1});
                byte[] b=Through(w=>SkillWire.Write(w,p));
                var q=new SkillWirePacket(); Check(b.Length==SkillWire.Size(1,1,0,1)&&SkillWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(b)),b.Length,q),"read");
                var s=q.States[0]; var o=q.Wires[0]; var e=q.Events[0];
                Check(Math.Abs(s.Px+2000.25f)<0.0011f&&Math.Abs(s.Pz-580.125f)<0.0011f&&Math.Abs(s.Qz+579.5f)<0.0011f&&Math.Abs(s.Py-15.2f)<0.005f,"state points");
                Check(Math.Abs(o.X-4.5f)<0.0011f&&Math.Abs(o.Z-566.75f)<0.0011f&&Math.Abs(o.Y-7f)<0.005f,"wire");
                Check(Math.Abs(e.Z-575.5f)<0.0011f&&Math.Abs(e.Uz-600.25f)<0.0011f&&Math.Abs(e.Y-15f)<0.005f,"event points"); });
            Test("Wire point (R541): a millimetre across +-8388 m and a centimetre up +-320 m; the edges, the signs, NaN", () => {
                Check(WirePoint.Bytes==8&&WirePoint.FlatSteps==8388607&&Math.Abs(WirePoint.FlatRange-8388.607f)<0.001f,"sizes");
                foreach(float m in new[]{0f,0.0004f,-0.0006f,1.5f,-1.5f,319.9f,320.5f,580.25f,-580.25f,-2000f,8388.6f,-8388.6f})
                {
                    float back=WirePoint.FromFlat(WirePoint.ToFlat(m));
                    Check(Math.Abs(back-m)<=0.0005f+Math.Abs(m)*1e-6f,"across "+m+" came back as "+back);
                }
                Check(WirePoint.ToFlat(9000f)==WirePoint.FlatSteps&&WirePoint.ToFlat(-9000f)==-WirePoint.FlatSteps&&WirePoint.ToFlat(float.NaN)==0,"across: clamped, NaN");
                Check(WirePoint.ToFlat(float.PositiveInfinity)==WirePoint.FlatSteps&&WirePoint.ToFlat(float.NegativeInfinity)==-WirePoint.FlatSteps,"across: infinities");
                foreach(float m in new[]{0f,2.8f,15f,-40f,319f,-319f})
                    Check(Math.Abs(WirePoint.FromHeight(WirePoint.ToHeight(m))-m)<0.005f,"up "+m);
                Check(WirePoint.ToHeight(400f)==short.MaxValue&&WirePoint.ToHeight(-400f)==-short.MaxValue&&WirePoint.ToHeight(float.NaN)==0,"up: clamped, NaN");
                foreach(int x in new[]{0,1,-1,32767,32768,-32768,-32769,65535,65536,-65536,580000,-400000,WirePoint.FlatSteps,-WirePoint.FlatSteps})
                {
                    byte[] bytes=Through(w=>WirePoint.Write(w,x,(short)-7,-x));
                    WirePoint.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(bytes)),out int qx,out short qy,out int qz);
                    Check(bytes.Length==WirePoint.Bytes&&qx==x&&qy==-7&&qz==-x,"24 bits: "+x+" came back as "+qx+", "+qz);
                } });
            Test("Skills packet carries the Sword Fight test's moments (R121) and nothing past them", () => {
                var p=new SkillWirePacket{HostTimeMs=5000,FirstSeq=1};
                p.Events.Add(new SkillWireEvent{Seq=1,TimeMs=4990,Mode=SkillWire.ModeSwordFight,Kind=36,By=SkillWire.Ref(0,2),Target=SkillWire.Ref(1,0),X=6.5f,Y=0.4f,Z=-1,Dx=1,Count=1,Size=3.5f});
                var ms=new System.IO.MemoryStream(); var w=new System.IO.BinaryWriter(ms); SkillWire.Write(w,p); w.Flush(); var b=ms.ToArray();
                var q=new SkillWirePacket(); Check(SkillWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(b)),b.Length,q),"read");
                var e=q.Events[0]; Check(e.Mode==SkillWire.ModeSwordFight&&e.Kind==36&&e.Target==SkillWire.Ref(1,0)&&Math.Abs(e.Size-3.5f)<0.006f&&e.TimeMs==4990,"the moment");
                var bad=(byte[])b.Clone(); bad[SkillWire.HeaderBytes+4]=3;   // a mode nobody has
                Check(!SkillWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(bad)),bad.Length,q),"unknown mode"); });
            Test("Sword Fight state packet (R121): round trip, skill parts only when present, the budget, bad packets", () => {
                var p=new SwordWirePacket{HostTimeMs=987654,White=3,Black=12,Remaining=3599.9f,Finished=false};
                p.Fighters.Add(new SwordWireFighter{Ref=SkillWire.Ref(0,0),Team=1,Alive=true,Drawn=true,Protection=1.25f,Flash=0.16f,Wx=0.31f,Wy=-0.42f,Wz=1.234f,WRot=0xC0FFEE11u,EdgeCondition=true,Why=6,EdgeTarget=SkillWire.Ref(1,2)});
                p.Fighters.Add(new SwordWireFighter{Ref=SkillWire.Ref(1,2),Team=0,Alive=true,
                    Skill=true,Stage=3,Detail=10,SkillFlags=2,StageTime=0.37f,Cooldown=1.5f,CooldownTotal=2f,Dx=0.6f,Dz=0.8f,Px=4.5f,Py=0.01f,Pz=-6.25f,Ox=-1,Oz=2,Reach=5.95f,Wave=3.3f,Dash=2.05f,PinCandidate=SkillWire.None,Slow=1.5f,Pin=0.8f,
                    Edge=true,EdgeStage=2,EdgeDetail=12,EdgeFlags=3,EdgeTime=0.45f,EdgeCooldownTotal=16f,Carried=SkillWire.Ref(0,0),Ax=7,Az=1,Outx=1,Lx=5.6f,Lz=1.2f,Sx=6.9f,Sy=0.6f,Sz=0.4f,
                    Glow=true,Gx=7,Gz=-0.5f,GOx=1,GAz=1,GAt=6.5f,GTo=14f});
                p.Collapses.Add(new SwordWireCollapse{Id=7,By=SkillWire.Ref(1,2),X0=5,Z0=-1.5f,X1=7,Z1=1.5f,Ox=1,Az=1,Lx=7,Age=1.23f,Crack=0.8f,Open=5f});
                var ms=new System.IO.MemoryStream(); var w=new System.IO.BinaryWriter(ms); SwordWire.Write(w,p); w.Flush(); var b=ms.ToArray();
                Check(b.Length==SwordWire.Size(p)&&b.Length==SwordWire.HeaderBytes+SwordWire.BaseBytes*2+SwordWire.SkillBytes+SwordWire.EdgeBytes+SwordWire.GlowBytes+SwordWire.CollapseBytes,"length "+b.Length);
                var q=new SwordWirePacket(); Check(SwordWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(b)),b.Length,q),"read");
                Check(q.HostTimeMs==987654&&q.White==3&&q.Black==12&&Math.Abs(q.Remaining-3599.9f)<0.06f&&!q.Finished,"header");
                var a=q.Fighters[0]; var c=q.Fighters[1]; var k=q.Collapses[0];
                Check(a.Team==1&&a.Alive&&a.Drawn&&!a.Classic&&!a.Skill&&!a.Edge&&!a.Glow&&a.EdgeCondition&&a.Why==6&&a.EdgeTarget==SkillWire.Ref(1,2),"idle fighter");
                Check(Math.Abs(a.Protection-1.25f)<0.006f&&Math.Abs(a.Flash-0.16f)<0.006f&&Math.Abs(a.Wz-1.234f)<0.0002f&&Math.Abs(a.Wy+0.42f)<0.0002f&&a.WRot==0xC0FFEE11u,"life and sword");
                Check(c.Skill&&c.Stage==3&&c.Detail==10&&c.SkillFlags==2&&Math.Abs(c.StageTime-0.37f)<0.006f&&Math.Abs(c.Cooldown-1.5f)<0.051f&&Math.Abs(c.Dx-0.6f)<0.01f&&Math.Abs(c.Pz+6.25f)<0.006f&&Math.Abs(c.Reach-5.95f)<0.026f&&Math.Abs(c.Wave-3.3f)<0.026f&&Math.Abs(c.Pin-0.8f)<0.051f,"F skill");
                Check(c.Edge&&c.EdgeStage==2&&c.EdgeDetail==12&&c.EdgeFlags==3&&Math.Abs(c.EdgeTime-0.45f)<0.006f&&Math.Abs(c.EdgeCooldownTotal-16f)<0.051f&&c.Carried==SkillWire.Ref(0,0)&&Math.Abs(c.Outx-1f)<0.01f&&Math.Abs(c.Sy-0.6f)<0.006f,"E skill");
                Check(c.Glow&&Math.Abs(c.GAt-6.5f)<0.006f&&Math.Abs(c.GTo-14f)<0.006f&&Math.Abs(c.GAz-1f)<0.01f,"glow");
                Check(k.Id==7&&Math.Abs(k.X1-7f)<0.006f&&Math.Abs(k.Z0+1.5f)<0.006f&&Math.Abs(k.Age-1.23f)<0.006f&&Math.Abs(k.Crack-0.8f)<0.051f&&Math.Abs(k.Open-5f)<0.051f,"collapse");
                Check(!SwordWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(b,0,b.Length-1)),b.Length-1,q),"short");
                var twice=(byte[])b.Clone(); twice[SwordWire.HeaderBytes+SwordWire.BaseBytes]=twice[SwordWire.HeaderBytes];   // the second fighter's place = the first's
                Check(!SwordWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(twice)),twice.Length,q),"a place twice");
                var stage=(byte[])b.Clone(); stage[SwordWire.HeaderBytes+SwordWire.BaseBytes*2]=7;   // the second fighter's F stage
                Check(!SwordWire.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(stage)),stage.Length,q),"stage out of range");
                // Twelve busy fighters and six collapses fit 1024 bytes once what does not fit is left off; lives and swords always go.
                var big=new SwordWirePacket(); for(int i=0;i<12;i++) big.Fighters.Add(new SwordWireFighter{Ref=(byte)i,Alive=true,Skill=true,Edge=true,Glow=true});
                for(int i=0;i<6;i++) big.Collapses.Add(new SwordWireCollapse{Id=(byte)(i+1),X1=1,Z1=1});
                Check(SwordWire.Size(big)>1011,"over before fitting");
                SwordWire.Fit(big,1024-13); Check(SwordWire.Size(big)<=1011&&big.Fighters.Count==12&&big.Fighters[0].Skill&&big.Fighters[0].Edge&&big.Fighters[0].Glow,"fitted, the first ones whole: "+SwordWire.Size(big));
                Check(SwordWire.BaseBytes==17&&SwordWire.SkillBytes==28&&SwordWire.EdgeBytes==29&&SwordWire.GlowBytes==16&&SwordWire.CollapseBytes==26,"sizes as documented"); });
            Test("Playback delay: 70 ms on a steady link, more as the link wobbles, never past 160 ms", () => {
                var d=new PlaybackDelay(); var rng=new Random(4);
                for(int i=0;i<300;i++) d.Arrived(1000+i*33.3+rng.NextDouble()*2,(uint)(i*33.3));
                Check(Math.Abs(d.Milliseconds-70)<0.001,"steady: "+d.Milliseconds);
                for(int i=300;i<600;i++) d.Arrived(1000+i*33.3+rng.NextDouble()*60,(uint)(i*33.3));
                Check(d.Milliseconds>95&&d.Milliseconds<=160,"wobbly: "+d.Milliseconds);
                for(int i=600;i<700;i++) d.Arrived(1000+i*33.3+rng.NextDouble()*400,(uint)(i*33.3));
                Check(d.Milliseconds==160,"capped: "+d.Milliseconds);
                d.Reset(); Check(d.Milliseconds==70,"reset");
                // Losses: one in twenty is ridden out by 70 ms; one in eight waits a snapshot more.
                var lossy=new PlaybackDelay(); var r=new Random(8); uint t=0;
                for(int i=0;i<600;i++) { t+=33; if(r.Next(20)==0) continue; lossy.Arrived(1000+t,t,0); }
                Check(lossy.Milliseconds==70,"5% loss keeps 70: "+lossy.Milliseconds);
                var bad=new PlaybackDelay(); uint t2=0, last=0; int lost=0;
                for(int i=0;i<600;i++) { t2+=33; if(i%8==7) { lost++; continue; } bad.Arrived(1000+t2,t2,lost); lost=0; last=t2; }
                Check(Math.Abs(bad.Milliseconds-103.3)<0.5,"12% loss waits one more snapshot: "+bad.Milliseconds+" share "+bad.LossShare); });
            Test("Playback delay: a 10% link (F12's worst) waits one snapshot more steadily, not on and off, and stops when the losses stop", () => {
                var d=new PlaybackDelay(); var r=new Random(3); uint t=0; int lost=0, on=0, arrivals=0, flips=0; bool was=false;
                for(int i=0;i<30*120;i++) {
                    t+=33; if(r.Next(10)==0) { lost++; continue; }
                    d.Arrived(1000+t,t,lost); lost=0;
                    if(i>30*20) { arrivals++; if(d.Lossy) on++; if(d.Lossy!=was) flips++; }
                    was=d.Lossy; }
                Check(on>arrivals*0.95&&flips<=2,"10% loss: on for "+on+" of "+arrivals+" snapshots, "+flips+" flips");
                Check(Math.Abs(d.Milliseconds-103.3)<0.5,"one more snapshot: "+d.Milliseconds);
                int offAt=-1;
                for(int i=0;i<30*20;i++) { t+=33; d.Arrived(1000+t,t,0); if(offAt<0&&!d.Lossy) offAt=i; if(offAt>=0) Check(!d.Lossy,"stays off"); }
                Check(offAt>=0&&offAt<30*10,"off within 10 s of a clean link: "+offAt+" snapshots");
                var light=new PlaybackDelay(); var r3=new Random(5); uint t3=0; int lost3=0, on3=0;
                for(int i=0;i<30*120;i++) { t3+=33; if(r3.Next(33)==0) { lost3++; continue; } light.Arrived(1000+t3,t3,lost3); lost3=0; if(light.Lossy) on3++; }
                Check(on3==0,"3% loss never waits: "+on3); });
            Test("Playback clock (R123): a host that stops a second is caught up at once, a wobbly link only drifts", () => {
                double rate; bool jumped;
                double p=PlaybackDelay.Advance(1000,1000,1070,10,out rate,out jumped);
                Check(!jumped&&rate==1&&Math.Abs(p-1010)<1e-9,"on target: one frame on at 1x");
                p=PlaybackDelay.Advance(900,1000,1070,10,out rate,out jumped);
                Check(!jumped&&rate==1.15&&Math.Abs(p-911.5)<1e-9,"100 ms behind: 1.15x, no jump");
                p=PlaybackDelay.Advance(750,1000,1070,10,out rate,out jumped);
                Check(!jumped&&rate==1.15,"exactly 250 ms behind: still catching up");
                p=PlaybackDelay.Advance(0,1000,1070,10,out rate,out jumped);
                Check(jumped&&rate==1&&Math.Abs(p-1010)<1e-9,"1 s behind: jumps to the target");
                p=PlaybackDelay.Advance(1065,1000,1070,10,out rate,out jumped);
                Check(!jumped&&rate==0.9&&p==1070,"ahead: slower, never past the newest");
                // A 30 Hz stream at 60 fps, the host stops 1 s: drawn about the delay behind again within a frame of the
                // next snapshot (it used to take about 7 s at 1.15x).
                double play=0, newest=0, worst=0; int jumps=0;
                for(int f=0;f<600;f++) {
                    double now=f*1000.0/60;
                    bool stopped=now>=4000&&now<5000;
                    if(!stopped) newest=Math.Floor(now/33.3)*33.3;
                    double target=newest-70;
                    if(play<=0) play=target;
                    play=PlaybackDelay.Advance(play,target,newest,1000.0/60,out rate,out jumped);
                    if(jumped) jumps++;
                    if(now>5100) worst=Math.Max(worst,target-play); }
                Check(jumps==1,"one jump: "+jumps);
                Check(worst<60,"behind at most "+worst.ToString("0")+" ms from 0.1 s after the stop"); });
            Test("Send schedule (R306 [1]): 30 a second at 60 fps with uneven frames, not 20-25; a slow host sends every frame; no burst after a hitch", () => {
                var r=new Random(11); var s=new SendSchedule(); int sent=0; double last=-1; int old=0;
                for(int f=0;f<600;f++) {
                    double now=f/60.0+(r.NextDouble()-0.5)*0.002;   // ten seconds of frames 1 ms early or late
                    if(s.Due(now,1.0/30)) sent++;
                    if(last<0||now-last>=1.0/30) { old++; last=now; } }   // the old "an interval since the last send"
                Check(sent>=299&&sent<=301,"30 a second: "+sent);
                Check(old<270,"the old way slipped to the next frame: "+old);
                var even=new SendSchedule(); int sixty=0;
                for(int f=0;f<600;f++) if(even.Due(f/60.0+(r.NextDouble()-0.5)*0.002,1.0/60)) sixty++;
                Check(sixty>=599&&sixty<=601,"60 a second at 60 fps, none skipped: "+sixty);
                var slow=new SendSchedule(); int slowSent=0;
                for(int f=0;f<100;f++) if(slow.Due(f*0.05,1.0/30)) slowSent++;
                Check(slowSent==100,"20 fps: every frame, "+slowSent);
                var hitch=new SendSchedule();
                for(int f=0;f<60;f++) hitch.Due(f/60.0,1.0/60);
                Check(hitch.Due(2.0,1.0/60)&&!hitch.Due(2.0,1.0/60)&&!hitch.Due(2.01,1.0/60)&&hitch.Due(2.017,1.0/60),"after a second's stop: one send, then the schedule from there"); });
            Test("Send schedule (R306 [1]): uneven frames near the rate - 5 ms, then 30 ms - keep the beat instead of every other send", () => {
                var s=new SendSchedule(); var before=new SendSchedule(); var g=new InputSendGate(); int sent=0, old=0, inputs=0; double now=0;
                for(int f=0;f<572;f++) {   // ten seconds at about 57 fps (a loopback host with four clients sent 30 of 60 a second)
                    if(s.Due(now,1.0/60)) sent++;
                    if(before.Due(now,1.0/60,0)) old++;
                    if(g.Due(now,false)) inputs++;
                    now+=f%2==0?0.005:0.030; }
                Check(sent>=520,"close to every frame: "+sent);
                Check(old<300,"the schedule before R306 started over after each late frame: "+old);
                Check(inputs>=520,"the input's slots too: "+inputs); });
            Test("Input send gate (R306 [2]): 60 a second when nothing changes, a press sent in its own frame, never over 120 a second", () => {
                var g=new InputSendGate(); int sent=0;
                for(int f=0;f<600;f++) if(g.Due(f/60.0,false)) sent++;   // 60 fps, ten seconds
                Check(sent>=599&&sent<=601,"60 a second: "+sent);
                var p=new InputSendGate(); p.Due(0,false);
                Check(!p.Due(0.005,false),"between slots, nothing new: waits");
                Check(p.Due(0.009,true),"a press 9 ms after a send goes at once");
                Check(!p.Due(0.0167,false)&&!p.Due(0.020,false)&&p.Due(0.022,false),"the next slot about an interval after it");
                var busy=new InputSendGate(); int many=0;
                for(int f=0;f<2400;f++) if(busy.Due(f/240.0,true)) many++;   // 240 fps, the input changing every frame, ten seconds
                Check(many>700&&many<=1201,"more than 60 and at most 120 a second: "+many);
                var fast=new InputSendGate(); fast.Due(0,false);
                Check(!fast.Due(0.004,true)&&fast.Due(0.009,true),"two within 1/120 s: the second waits"); });
            Test("Input change (R306 [2]): starting, stopping and turning 20 degrees are new; a turn of 10, a slower walk and the dead zone are not", () => {
                Check(InputChange.Moved(0,0,1,0)&&InputChange.Moved(1,0,0,0),"start, stop");
                Check(!InputChange.Moved(1,0,0.985f,0.174f),"10 degrees");
                Check(InputChange.Moved(1,0,0.9336f,0.3584f)&&InputChange.Moved(0,1,0.7071f,0.7071f),"21 and 45 degrees");
                Check(!InputChange.Moved(1,0,0.5f,0)&&!InputChange.Moved(0.05f,0,0,0.05f)&&!InputChange.Moved(0,0,0,0),"slower, the dead zone, standing");
                Check(InputChange.Moved(1,0,-1,0),"turned round"); });
            Test("Snapshot rate (R306 [5]): 60 a second up to six players or a small party with bots, 30 above", () => {
                Check(SnapshotRate.SnapshotBytes(4)==298&&SnapshotRate.SnapshotBytes(12)==834,"sizes as HOST_AUTHORITY §6 (R541)");
                for(int players=2;players<=6;players++) Check(SnapshotRate.Interval(players-1,players)==SnapshotRate.Fast,players+" players: 60");
                for(int players=7;players<=12;players++) Check(SnapshotRate.Interval(players-1,players)==SnapshotRate.Normal,players+" players: 30");
                Check(SnapshotRate.Interval(1,12)==SnapshotRate.Fast&&SnapshotRate.Interval(2,12)==SnapshotRate.Fast&&SnapshotRate.Interval(3,12)==SnapshotRate.Normal,"bots filling up to twelve");
                Check(SnapshotRate.Interval(0,1)==SnapshotRate.Fast,"nobody to send to"); });
            Test("Playback delay (R306 [5]): the floor is two intervals and 4 ms - 70 at 30 a second, 37 at 60; a lost snapshot waits one interval more", () => {
                var d=new PlaybackDelay(); var r=new Random(6); uint host=0;
                for(int i=0;i<600;i++) { host=(uint)Math.Round(i*1000.0/60); d.Arrived(5000+i*1000.0/60+r.NextDouble()*2,host); }
                Check(Math.Abs(d.IntervalMs-16.67)<0.3,"interval: "+d.IntervalMs);
                Check(Math.Abs(d.Milliseconds-37.33)<0.5&&Math.Abs(d.FloorMs-37.33)<0.5,"60 a second: "+d.Milliseconds);
                Check(Math.Abs(d.LeadMs-16.6)<0.6,"own pawn 16-17 ms sooner: "+d.LeadMs);
                var thirty=new PlaybackDelay();
                for(int i=0;i<300;i++) thirty.Arrived(1000+i*33.3,(uint)(i*33.3));
                Check(thirty.Milliseconds==70&&Math.Abs(thirty.IntervalMs-33.3)<0.3&&Math.Abs(thirty.LeadMs-32.7)<0.4,"30 a second: "+thirty.Milliseconds+" lead "+thirty.LeadMs);
                // The host goes from 30 to 60 a second (a player left): the floor follows within a few seconds.
                uint t=(uint)(300*33.3); double local=1000+300*33.3;
                for(int i=0;i<180;i++) { t+=(uint)(i%3==0?16:17); local+=i%3==0?16:17; thirty.Arrived(local,t); }
                Check(thirty.Milliseconds<40,"down to 60 a second's floor in 3 s: "+thirty.Milliseconds);
                // A one-second stop says nothing about the rate.
                thirty.Arrived(local+1000,t+1000); Check(thirty.IntervalMs<17.5,"a stop is not an interval: "+thirty.IntervalMs);
                // One in eight lost at 60 a second: one more snapshot is 17 ms, not 33.
                var bad=new PlaybackDelay(); uint t2=0; int lost=0;
                for(int i=0;i<1200;i++) { t2+=(uint)(i%3==0?16:17); if(i%8==7) { lost++; continue; } bad.Arrived(1000+t2,t2,lost); lost=0; }
                Check(bad.Lossy&&Math.Abs(bad.Milliseconds-54)<1,"12% lost at 60: "+bad.Milliseconds);
                bad.Reset(); Check(bad.Milliseconds==70&&Math.Abs(bad.IntervalMs-1000.0/30)<1e-9,"reset"); });
            Test("Press timer (R306 [0]): answered by the first echo of the press's input or a later one, seen when the drawing reaches that snapshot", () => {
                var p=new PressTimer();
                p.Pressed(1000); p.Pressed(1005);   // the second is not timed: one at a time
                p.Arrived(1010,990,5000); Check(p.Answered.Count==0,"an echo from before the press");
                p.Arrived(1040,1000,5030); Check(p.Answered.Last==40,"answered: "+p.Answered.Last);
                p.Drawn(1050,4990); Check(p.Seen.Count==0,"not drawn yet");
                p.Drawn(1081,5030); Check(p.Seen.Last==81&&p.Lost==0,"seen: "+p.Seen.Last);
                p.Pressed(2000); p.Arrived(2010,-1,6000); p.Drawn(3400,6000); Check(p.Lost==0,"not given up yet at 1.4 s");
                p.Drawn(3600,6000); Check(p.Lost==1,"given up after 1.5 s");
                p.Pressed(4000); p.Arrived(4030,4001,7000); Check(p.Answered.Count==2&&Math.Abs(p.Answered.Average-35)<1e-9,"timed again, averaged");
                var a=new LatencyAverage(3); a.Add(1); a.Add(2); a.Add(3); a.Add(10); Check(a.Average==5&&a.Last==10&&a.Count==4,"the last three");
                a.Clear(); Check(a.Average==-1&&a.Last==-1,"cleared"); });
            Test("Latency before (R306): a development build puts items back from the command line, all or some", () => {
                var none=LatencyBefore.Parse(new[]{"game.exe","-autopilot"}); Check(!none.Any&&none.Items=="","none");
                var all=LatencyBefore.Parse(new[]{"-latencyBefore"}); Check(all.Slipping&&all.SlotsOnly&&all.ReadInUpdate&&all.Always30&&all.Items=="[1][2][3][5]","all");
                var some=LatencyBefore.Parse(new[]{"x","-latencyBefore=3,5"}); Check(!some.Slipping&&!some.SlotsOnly&&some.ReadInUpdate&&some.Always30&&some.Items=="[3][5]","some");
                Check(!LatencyBefore.Parse(null).Any&&!LatencyBefore.Parse(new[]{"-latencyBeforeX"}).Any,"nothing asked"); });
            Test("Pioneer checkpoints: the first team to ring a bell opens its section, 20 s for that team", () => {
                var q=new PioneerRules(8);
                Check(q.TryOpen(3,PioneerRules.Black,100)&&!q.TryOpen(3,PioneerRules.White,101),"first ring wins");
                Check(q.IsOpen(3)&&q.Pioneer(3)==PioneerRules.Black&&!q.IsOpen(2),"state");
                Check(q.CanUse(3,PioneerRules.Black,105)&&!q.CanUse(3,PioneerRules.White,119.9)&&q.CanUse(3,PioneerRules.White,120),"exclusive window");
                Check(!q.CanUse(2,PioneerRules.Black,500),"closed section");
                Check(!q.TryOpen(0,0,1)&&!q.TryOpen(9,0,1)&&!q.TryOpen(1,-1,1)&&!q.TryOpen(1,2,1)&&!q.TryOpen(1,0,double.NaN),"bad input"); });
            Test("Pioneer checkpoints: respawn at the higher of own checkpoint and one below the team's best", () => {
                var q=new PioneerRules(8);
                Check(q.RespawnSection(1,PioneerRules.White)==0,"nothing yet = the bridge");
                q.Reach(1,2); q.Reach(1,1); Check(q.PersonalBest(1)==2&&q.RespawnSection(1,PioneerRules.White)==2,"own best, never lowered");
                q.TryOpen(1,PioneerRules.White,1); q.TryOpen(5,PioneerRules.White,2); q.TryOpen(6,PioneerRules.Black,3);
                Check(q.TeamBest(PioneerRules.White)==5&&q.TeamBest(PioneerRules.Black)==6,"team best");
                Check(q.RespawnSection(2,PioneerRules.White)==4&&q.RespawnSection(1,PioneerRules.White)==4,"one below the leader");
                q.Reach(3,7); Check(q.RespawnSection(3,PioneerRules.White)==7,"own checkpoint higher");
                q.Reach(4,0); q.Reach(4,99); Check(q.PersonalBest(4)==0,"bad sections ignored");
                q.Reset(); Check(!q.IsOpen(5)&&q.RespawnSection(3,PioneerRules.White)==0,"reset"); });
            Test("Pioneer checkpoints: openings travel as text and only ever open", () => {
                var host=new PioneerRules(8); host.TryOpen(2,PioneerRules.White,12.5); host.TryOpen(4,PioneerRules.Black,40.25);
                var client=new PioneerRules(8); Check(client.Apply(host.Encode())&&!client.Apply(host.Encode()),"applied once");
                Check(client.Pioneer(2)==PioneerRules.White&&Math.Abs(client.OpenedAt(4)-40.25)<1e-6,"same state");
                Check(client.Apply("")==false&&client.IsOpen(2),"empty keeps state");
                foreach(string bad in new[]{"2:0","x:0:1","2:5:1","9:0:1","2:0:1;junk",new string('1',600)})
                    Check(!new PioneerRules(8).Apply(bad),"rejected "+bad);
                var partial=new PioneerRules(8); Check(!partial.Apply("1:0:10;3:9:10")&&!partial.IsOpen(1),"all or nothing"); });
            Test("Shared chess pieces: the starting values of DESIGN 6.2", () => {
                var pawn=ChessPieces.Stats(PieceKind.Pawn); Check(pawn.Weight==1f&&pawn.Move==1f&&pawn.Climb==1f&&pawn.Sprint&&!pawn.StatusImmune,"pawn");
                var rook=ChessPieces.Stats(PieceKind.Rook); Check(rook.Weight==1.5f&&rook.Climb==0.7f&&rook.Sprint,"rook");
                var king=ChessPieces.Stats(PieceKind.King); Check(king.Weight==3f&&king.Move==0.8f&&king.Sprint&&king.StatusImmune,"king");
                Check(ChessPieces.Stats(PieceKind.Knight).Weight==0.8f&&ChessPieces.Stats(PieceKind.Queen).Weight==2f,"knight, queen");
                Check(ChessPieces.Stats((PieceKind)9).Weight==1f&&!ChessPieces.IsValid(6)&&ChessPieces.IsValid(5)&&!ChessPieces.IsValid(-1),"out of range");
                Check(ChessPieces.Name(PieceKind.Knight)=="나이트"&&ChessPieces.Name(PieceKind.Pawn)=="폰","names"); });
            Test("Shared chess pieces: a pawn promotes to a rook, bishop, knight or the team's one king", () => {
                foreach(var to in new[]{PieceKind.Rook,PieceKind.Bishop,PieceKind.Knight,PieceKind.King})
                    Check(ChessPieces.CanPromote(PieceKind.Pawn,to,0),"pawn to "+to);
                Check(!ChessPieces.CanPromote(PieceKind.Pawn,PieceKind.King,1),"one king per team");
                Check(!ChessPieces.CanPromote(PieceKind.Pawn,PieceKind.Queen,0)&&!ChessPieces.CanPromote(PieceKind.Pawn,PieceKind.Pawn,0),"not the queen, not a pawn");
                Check(!ChessPieces.CanPromote(PieceKind.Rook,PieceKind.Knight,0)&&!ChessPieces.CanPromote(PieceKind.King,PieceKind.Rook,0),"only pawns"); });
            Test("Host fitness: a faster benchmark, more cores and smooth frames score higher", () => {
                int reference = HostFitness.Score(HostFitness.ReferenceBenchMs, 8, 16000, false, 16);
                Check(reference == 1000, "reference machine should score 1000, got " + reference);
                Check(HostFitness.Score(20, 8, 16000, false, 16) > reference && HostFitness.Score(80, 8, 16000, false, 16) < reference, "benchmark");
                Check(HostFitness.Score(40, 2, 16000, false, 16) < reference && HostFitness.Score(40, 64, 16000, false, 16) == reference, "cores capped at 8");
                Check(HostFitness.Score(40, 8, 4000, false, 16) < reference && HostFitness.Score(40, 8, 16000, true, 16) < reference, "memory, editor");
                Check(HostFitness.Score(40, 8, 16000, false, 0) == reference && HostFitness.Score(40, 8, 16000, false, 25) == reference, "no penalty at 40 fps or better");
                Check(HostFitness.Score(40, 8, 16000, false, 50) == reference / 2, "20 fps halves the score");
                Check(HostFitness.Score(0, 8, 16000, false, 16) == 0 && HostFitness.Score(double.NaN, 8, 16000, false, 16) == 0, "unmeasured is zero"); });
            Test("Host election: fitness decides, ping discounts, ties go to the lower ID, bots never host", () => {
                Check(HostElection.Rank(new[] { C(1, 500), C(2, 900), C(3, 700) }).SequenceEqual(new ulong[] { 2, 3, 1 }), "fitness order");
                Check(HostElection.Rank(new[] { C(5, 800), C(3, 800) }).SequenceEqual(new ulong[] { 3, 5 }), "tie to the lower id");
                Check(HostElection.Rank(new[] { C(1, 900, 200), C(2, 500, 20) }).SequenceEqual(new ulong[] { 2, 1 }), "far host discounted");
                Check(HostElection.Rank(new[] { C(1, 900, 60), C(2, 800, 10) }).SequenceEqual(new ulong[] { 1, 2 }), "within the ping budget only fitness counts");
                Check(HostElection.Rank(new[] { C(1, 0), C(2, 400) }).SequenceEqual(new ulong[] { 2, 1 }), "unmeasured below measured");
                Check(!HostElection.Rank(new[] { C(BotIdentity.Id(7, 0), 99999), C(0, 5000), C(4, 1) }).Any(id => id != 4), "bots and zero never rank");
                // R159: before the match the best PC simply hosts (no 15% lead needed any more).
                Check(HostElection.Worth(C(1, 500), C(2, 501), HostElection.StartMargin) && !HostElection.Worth(C(1, 500), C(2, 499), HostElection.StartMargin), "start: the better PC");
                Check(!HostElection.Worth(C(1, 500), C(1, 5000), 1), "never worth handing to yourself"); });
            Test("Host election: a slow host goes only to a clearly stronger machine, so the role cannot bounce back", () => {
                var host = M(1, 150, 400);   // bogged down by hosting
                Check(HostElection.Takeover(host, new[] { M(2, 900, 900), M(3, 1200, 500) }, HostElection.StruggleMargin) == 2, "only a machine 1.5x stronger qualifies");
                Check(HostElection.Takeover(host, new[] { M(3, 1200, 500) }, HostElection.StruggleMargin) == 0, "fast right now is not enough");
                // After the move the new host bogs down in turn; the old one looks fast again, but its machine is weaker.
                Check(HostElection.Takeover(M(2, 150, 900), new[] { M(1, 1000, 400) }, HostElection.StruggleMargin) == 0, "no bounce back");
                Check(HostElection.Takeover(M(1, 150, 0), new[] { M(2, 900, 900) }, HostElection.StruggleMargin) == 0, "unmeasured host keeps it"); });
            Test("Host election at the start (R159): a stronger PC that came in later takes the role, a moment's slow frame does not stop it", () => {
                // The room's creator (1) and a stronger machine (2) that just came in: its frame time dipped as it joined.
                Check(HostElection.StartHost(1, new[] { M(1, 1000, 1000), M(2, 600, 1400) }, HostElection.StartMargin) == 2, "machines compared alone");
                Check(HostElection.StartHost(1, new[] { M(1, 1000, 1000), M(2, 1100, 1100) }, HostElection.StartMargin) == 2, "first place hosts even by a small lead");
                Check(HostElection.StartHost(1, new[] { M(1, 1100, 1100), M(2, 1000, 1000) }, HostElection.StartMargin) == 1, "first place keeps it");
                Check(HostElection.StartHost(2, new[] { M(1, 1000, 1000), M(2, 1000, 1000) }, HostElection.StartMargin) == 1 &&
                      HostElection.StartHost(1, new[] { M(1, 1000, 1000), M(2, 1000, 1000) }, HostElection.StartMargin) == 1, "a tie goes to the lower ID on every PC");
                var bed = new TeamReservations(); TeamReservations.Group g1, g2;
                bool first = bed.Reserve(10, 100, "a", new ulong[] { 10, 11 }, 0, out g1);
                bool second = bed.Reserve(20, 200, "b", new ulong[] { 20 }, 0, out g2);
                Check(first && second, "setup");
                g1.Committed = true;
                var moved = new TeamReservations(); moved.Restore(bed.Groups);
                Check(moved.Count == 3 && moved.Find(11).Leader == 10 && moved.Find(11).Committed && moved.Find(20).Team == g2.Team && !moved.Find(20).Committed, "reservations move whole");
                moved.Restore(new[] { g1, new TeamReservations.Group { Leader = 30, Party = 300, Ticket = "c", Members = new ulong[] { 11, 30 }, Team = 0 } });
                Check(moved.Count == 2 && moved.Find(30) == null, "a clashing group is dropped");
                Check(HostElection.StartHost(1, new[] { M(1, 1000, 1000), M(2, 0, 0) }, HostElection.StartMargin) == 1, "an unknown machine never takes it");
                Check(HostElection.Unknown(M(2, 0, 0)) && !HostElection.Unknown(M(2, 0, 900)) && !HostElection.Unknown(M(2, 700, 0)), "unknown = nothing published yet");
                Check(HostElection.StartHost(1, new[] { M(1, 1000, 1000), M(BotIdentity.Id(1, 0), 9000, 9000) }, HostElection.StartMargin) == 1, "bots never host");
                Check(HostElection.StartHost(1, new[] { M(2, 1400, 1400) }, HostElection.StartMargin) == 2, "the creator without a score counts as unknown");
                var far = new HostElection.Candidate { Id = 2, Fitness = 1400, BaseFitness = 1400, PingMs = 200 };
                Check(HostElection.StartHost(1, new[] { new HostElection.Candidate { Id = 1, Fitness = 1000, BaseFitness = 1000, PingMs = 40 }, far }, HostElection.StartMargin) == 1, "a far-away PC is still discounted"); });
            Test("Host election: the successor is the first published player still here, else the fittest", () => {
                var fallback = new[] { new HostElection.Candidate { Id = 2, Fitness = 300 }, new HostElection.Candidate { Id = 3, Fitness = 900 } };
                Check(HostElection.Successor(new ulong[] { 3, 2 }, new HashSet<ulong> { 1, 2 }, 1, fallback) == 2, "absent published player skipped");
                Check(HostElection.Successor(new ulong[] { 1 }, new HashSet<ulong> { 1, 2, 3 }, 1, fallback) == 3, "the leaver never succeeds itself");
                Check(HostElection.Successor(null, new HashSet<ulong>(), 1, null) == 0, "nobody left");
                Check(HostElection.Decode("3,x,,2,3,0").SequenceEqual(new ulong[] { 3, 2 }) && HostElection.Encode(new ulong[] { 3, 2 }) == "3,2", "encoding"); });
            // R504: a next in line that never claimed stopped the walk, and the match ended with healthy players after it.
            Test("Host claims (R504): the one right after a next in line that never claims may pass it over, saying so, two epochs up", () => {
                var order = new List<ulong> { 2, 3, 4 };
                int e;
                var claims = new Dictionary<ulong, int> { { 1, 0 }, { 2, 0 }, { 3, 3 }, { 4, 0 } };
                var passed = new Dictionary<ulong, ulong> { { 1, 0 }, { 2, 0 }, { 3, 2 }, { 4, 0 } };
                Check(HostElection.PassOver(order, claims.Keys, 1, 2, null) == 3, "who passes 2 over");
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e, passed) == 3 && e == 3, "the one after it passes it over");
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e) == 1 && e == 1, "a walk that knows no passing over");
                passed[3] = 0;
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e, passed) == 1, "without the word (a PC that took 2 for gone steps back down)");
                passed[3] = 2; claims[3] = 4;
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e, passed) == 1, "more than two up");
                claims[3] = 0; passed[3] = 0; claims[4] = 3; passed[4] = 2;
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e, passed) == 1, "only the one right after it may pass it over");
                claims[3] = 3; passed[3] = 2; claims[4] = 0; passed[4] = 0; claims[2] = 2;
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e, passed) == 3 && e == 3, "the one passed over claims after all: it, then the one after it");
                Check(HostElection.Follow(3, 3, 3, order, claims, null, out e, passed) == 3 && e == 3, "from the new host's record the one passed over is not reached again");
                Check(HostElection.PassOver(order, new List<ulong> { 1, 2, 4 }, 1, 2, null) == 4 &&
                      HostElection.Follow(1, 1, 1, order, new Dictionary<ulong, int> { { 2, 0 }, { 4, 3 } }, null, out e, new Dictionary<ulong, ulong> { { 2, 0 }, { 4, 2 } }) == 4 && e == 3,
                      "a successor gone between them is skipped as well"); });
            Test("Host claims (R295): only the next in line counts, one epoch up, and successors are followed one after another", () => {
                var order = new List<ulong> { 2, 3, 4 };
                int e;
                var claims = new Dictionary<ulong, int> { { 2, 0 }, { 3, 2 }, { 4, 0 }, { 9, 2 } };
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e) == 1 && e == 1, "out of turn (3 while 2 is here) or outside the order (9)");
                claims[2] = 2;
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e) == 2 && e == 2, "the next in line takes over");
                claims[2] = 5;
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e) == 1 && e == 1, "too large a number");
                claims[2] = int.MaxValue;
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e) == 1 && e == 1, "int.MaxValue");
                claims[2] = 2; claims[3] = 2;
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e) == 2 && e == 2, "two that both stepped in settle on the one in line");
                claims[3] = 3;
                Check(HostElection.Follow(1, 1, 1, order, claims, null, out e) == 3 && e == 3, "a successor that went quiet is followed by its own");
                // 2 took over at epoch 2 and left before this PC heard of it.
                Check(HostElection.Follow(1, 1, 1, order, new Dictionary<ulong, int> { { 3, 3 }, { 4, 0 } }, null, out e) == 3 && e == 3, "one more for a successor gone");
                Check(HostElection.Follow(1, 1, 1, order, new Dictionary<ulong, int> { { 3, 4 }, { 4, 0 } }, null, out e) == 1, "but not two");
                Check(HostElection.Follow(1, 1, 2, order, new Dictionary<ulong, int> { { 2, 3 } }, null, out e) == 2 && e == 3, "this PC's own epoch counts as seen");
                Check(HostElection.Follow(2, 3, 3, order, new Dictionary<ulong, int> { { 2, 2 }, { 3, 2 } }, null, out e) == 2 && e == 3, "old claims below the record");
                var fallback = new[] { new HostElection.Candidate { Id = 7, Fitness = 900 }, new HostElection.Candidate { Id = 8, Fitness = 300 } };
                Check(HostElection.Follow(1, 1, 1, order, new Dictionary<ulong, int> { { 7, 2 }, { 8, 0 } }, fallback, out e) == 7 && e == 2, "nobody published here: the fittest");
                Check(HostElection.NextEpoch(1) == 2 && HostElection.NextEpoch(-4) == 1 && HostElection.NextEpoch(int.MaxValue) == int.MaxValue, "next epoch never wraps");
                Check(HostElection.ClaimLimit(2, 1) == 4 && HostElection.ClaimLimit(int.MaxValue, 3) == int.MaxValue, "limit never wraps"); });
            Test("Frame monitor: five slow seconds are struggling, one loading hitch is not", () => {
                var m = new FrameMonitor(); double t = 0;
                for (; t < 10; t += .1) m.Add(16, t);
                m.Add(5000, t); Check(m.AverageMs < FrameMonitor.StruggleMs && !m.Struggling(t + 10), "one hitch");
                double slowFrom = -1;
                for (int i = 0; i < 200; i++, t += .1) { m.Add(60, t); if (slowFrom < 0 && m.AverageMs > FrameMonitor.StruggleMs) slowFrom = t; }
                Check(slowFrom > 0 && m.Struggling(t) && m.Struggling(slowFrom + 5.01) && !m.Struggling(slowFrom + 4.9), "sustained slowness");
                for (int i = 0; i < 100; i++, t += .1) m.Add(16, t);
                Check(!m.Struggling(t) && m.AverageMs < 20, "recovered"); });
            Test("Match start: the host starts when everyone is ready or the slowest had its wait", () => {
                Check(MatchStart.ShouldStart(new[] { 100, 100, 100 }, 0), "all ready");
                Check(!MatchStart.ShouldStart(new[] { 100, 99, 100 }, MatchStart.MaxWait - .01), "one short, still waiting");
                Check(MatchStart.ShouldStart(new[] { 100, 20 }, MatchStart.MaxWait), "waited long enough");
                Check(MatchStart.ShouldStart(new int[0], 0), "nobody to wait for");
                Check(MatchStart.ClientGiveUp > MatchStart.MaxWait + MatchStart.Lead, "a client outlasts the host's wait"); });
            Test("Match start: load reports and start times survive the lobby as text", () => {
                Check(MatchStart.Parse("64") == 64 && MatchStart.Parse("140") == 100 && MatchStart.Parse("-3") == 0 && MatchStart.Parse("x") == 0 && MatchStart.Parse(null) == 0, "percent");
                Check(MatchStart.Format(250) == "100", "clamped");
                double at = 1790700000.25;
                Check(Math.Abs(MatchStart.ParseTime(MatchStart.FormatTime(at)) - at) < .001, "time round trip");
                Check(MatchStart.ParseTime("") == 0 && MatchStart.ParseTime("NaN") == 0 && MatchStart.ParseTime("-5") == 0 && MatchStart.ParseTime("Infinity") == 0, "unusable times");
                Check(MatchStart.WorthPublishing(0, -1) && !MatchStart.WorthPublishing(9, 0) && MatchStart.WorthPublishing(10, 0) &&
                      MatchStart.WorthPublishing(100, 95) && !MatchStart.WorthPublishing(100, 100), "publishing"); });
            Test("Load progress fills 0-70 while loading, holds at 72 while switching on, then warms up to 100", () => {
                Check(LoadProgress.Percent(LoadStage.Loading, 0) == 0 && LoadProgress.Percent(LoadStage.Loading, .45) == 35 &&
                      LoadProgress.Percent(LoadStage.Loading, .9) == 70 && LoadProgress.Percent(LoadStage.Loading, 1) == 70, "loading");
                Check(LoadProgress.Percent(LoadStage.Opening, .5) == 72, "opening");
                Check(LoadProgress.Percent(LoadStage.WarmingUp, 0) == 75 && LoadProgress.Percent(LoadStage.WarmingUp, 1) == 99, "warming up never claims ready");
                Check(LoadProgress.Percent(LoadStage.Ready, 0) == 100 && LoadProgress.Percent(LoadStage.Loading, double.NaN) == 0, "ready and bad input"); });
            Test("Warm-up waits for a run of smooth frames, restarts the run on a stutter, and gives up waiting", () => {
                var w = new WarmupMeter();
                for (int i = 0; i < 40; i++) w.Add(16);
                Check(w.Done && w.Fraction == 1, "smooth frames");
                var s = new WarmupMeter();
                for (int i = 0; i < 30; i++) s.Add(16);
                s.Add(400); Check(!s.Done && s.Stable == 0, "a stutter restarts the run");
                for (int i = 0; i < WarmupMeter.StableFrames; i++) s.Add(16);
                Check(s.Done, "smooth again");
                var slow = new WarmupMeter();
                for (int i = 0; i < 200 && !slow.Done; i++) slow.Add(80);
                Check(slow.Done && slow.Elapsed >= WarmupMeter.MaxSeconds && slow.Elapsed < WarmupMeter.MaxSeconds + .1, "never smooth, stops waiting");
                var early = new WarmupMeter();
                for (int i = 0; i < 20; i++) early.Add(5);
                Check(!early.Done && early.Fraction < 1, "too soon even when smooth"); });
            Test("Starters (R538): a started match knows who it started with, who is away, and gives a player back only their own seat", () => {
                var starters = new List<Starters.Seat>();
                for (int k = 0; k < 3; k++) starters.Add(new Starters.Seat { Id = (ulong)(10 + k), Team = 0, Slot = k });
                for (int k = 0; k < 3; k++) starters.Add(new Starters.Seat { Id = (ulong)(20 + k), Team = 1, Slot = k });
                var seated = new HashSet<ulong> { 10, 11, 12, 20, 22 };
                Check(Starters.Away(starters, seated, 0) == 0 && Starters.Away(starters, seated, 1) == 1, "who is away");
                seated.Add(99);   // someone the match did not start with changes nothing
                Check(Starters.Away(starters, seated, 1) == 1 && Starters.Away(null, seated, 0) == 0 && Starters.Away(starters, null, 0) == 0, "strangers and nothing known");
                Check(Starters.SeatOf(starters, 21, out var seat) && seat.Team == 1 && seat.Slot == 1, "a starter's own seat");
                Check(!Starters.SeatOf(starters, 99, out _) && !Starters.SeatOf(starters, 0, out _) && !Starters.SeatOf(null, 21, out _), "nobody else has one");
                Check(TeamReservations.ValidRequest(1, 101, "t", new ulong[] { 1, 2 }) && !TeamReservations.ValidRequest(1, 101, "t", new ulong[] { 2 }) &&
                      !TeamReservations.ValidRequest(1, 101, "t", new ulong[] { 1, 1 }) && !TeamReservations.ValidRequest(1, 101, "t", null), "request shape"); });
            Test("Starters (R538): the room keeps the starters and the team sizes as text", () => {
                var two = new[] { new Starters.Seat { Id = 76561198000000001, Team = 1, Slot = 3 }, new Starters.Seat { Id = 5, Team = 0, Slot = 0 } };
                Check(Starters.Encode(two) == "76561198000000001:1:3;5:0:0", "as text: " + Starters.Encode(two));
                var back = Starters.Decode(Starters.Encode(two));
                Check(back.Count == 2 && back[0].Id == 76561198000000001 && back[0].Team == 1 && back[0].Slot == 3 && back[1].Id == 5 && back[1].Team == 0, "round trip");
                Check(Starters.Encode(new Starters.Seat[0]) == "" && Starters.Decode(null).Count == 0 && Starters.Decode("").Count == 0, "nobody");
                // A zero id, a third team, a seventh seat, a word for a number, a seat taken twice, an id twice, a field too many.
                var messy = Starters.Decode("0:0:0;7:2:0;8:0:6;9:0:x;10:1:1;11:1:1;10:0:2;12:0:0:5");
                Check(messy.Count == 1 && messy[0].Id == 10 && messy[0].Team == 1 && messy[0].Slot == 1, "bad entries kept: " + messy.Count);
                var many = string.Join(";", Enumerable.Range(0, 20).Select(k => (100 + k) + ":" + (k % 2) + ":" + (k / 2 % 6)));
                Check(Starters.Decode(many).Count <= 12, "more starters than a match has seats");
                Check(Starters.EncodeCapacity(6, 5) == "6,5" && Starters.DecodeCapacity("6,5").SequenceEqual(new[] { 6, 5 }), "sizes");
                Check(Starters.DecodeCapacity("") == null && Starters.DecodeCapacity("7,6") == null && Starters.DecodeCapacity("6") == null &&
                      Starters.DecodeCapacity("-1,6") == null && Starters.DecodeCapacity("a,b") == null, "bad sizes"); });
            Test("Chat: a line is one clean line of at most eighty characters, and only chat lines decode", () => {
                Check(ChatText.Clean("  안녕\n하세요\t\t 폰 러시  ") == "안녕 하세요 폰 러시", "white space");
                Check(ChatText.Clean("a\u202Eb\u200Bc\u0007d") == "abc d", "control and format characters");
                Check(ChatText.Clean(new string('가', 200)).Length == ChatText.MaxLength && ChatText.Clean(null) == "" && ChatText.Clean(" \n ") == "", "length");
                string emoji = new string('a', ChatText.MaxLength - 1) + "\U0001F600";
                Check(ChatText.Clean(emoji) == new string('a', ChatText.MaxLength - 1), "surrogate pair kept whole or dropped");
                Check(ChatText.TryDecode(ChatText.Encode(ChatChannel.Party, 1, " 2랭크까지 같이 가자 "), out var channel, out int team, out string text) &&
                      channel == ChatChannel.Party && text == "2랭크까지 같이 가자", "party round trip");
                Check(ChatText.TryDecode(ChatText.Encode(ChatChannel.Team, 1, "부축 갈게"), out channel, out team, out text) &&
                      channel == ChatChannel.Team && team == 1 && text == "부축 갈게", "team round trip");
                Check(ChatText.TryDecode(ChatText.Encode(ChatChannel.All, 0, "gg"), out channel, out _, out _) && channel == ChatChannel.All, "all round trip");
                Check(!ChatText.TryDecode("{\"kind\":\"reserve\"}", out _, out _, out _) && !ChatText.TryDecode("CFC1|x|hi", out _, out _, out _) &&
                      !ChatText.TryDecode("CFC1|a|   ", out _, out _, out _) && !ChatText.TryDecode("CFC1|a", out _, out _, out _) &&
                      !ChatText.TryDecode(null, out _, out _, out _), "not chat");
                Check(ChatText.Label(ChatChannel.Party) == "파티" && ChatText.Label(ChatChannel.All) == "전체" && ChatText.Label(ChatChannel.Team) == "팀", "labels");
                Check(ChatText.Next(ChatChannel.Party, c => true) == ChatChannel.Team && ChatText.Next(ChatChannel.Team, c => true) == ChatChannel.All && ChatText.Next(ChatChannel.All, c => true) == ChatChannel.Party &&
                      ChatText.Next(ChatChannel.Party, c => c != ChatChannel.Team) == ChatChannel.All && ChatText.Next(ChatChannel.All, c => false) == ChatChannel.All, "tab order"); });
            Test("Chat: one line a second, numbered per channel, fifty kept per channel, unread counted", () => {
                var throttle = new ChatThrottle();
                Check(throttle.TryPass(10) && !throttle.TryPass(10.5) && throttle.TryPass(11) && !throttle.TryPass(11.99), "one a second");
                var log = new ChatLog();
                log.Add(ChatChannel.Party, 0, "", "캐슬링 님이 파티에 들어왔어요", true, false, 0);
                log.Add(ChatChannel.Party, 7, "퀸사이드", "폰 러시 한 판", false, false, 1);
                var mine = log.Add(ChatChannel.Party, 8, "나이트메어", "ㄱㄱ", false, true, 2);
                var all = log.Add(ChatChannel.All, 7, "퀸사이드", "hi", false, false, 3);
                Check(mine.Number == 3 && all.Number == 1 && log.Unread(ChatChannel.Party) == 2 && log.Unread(ChatChannel.All) == 1, "numbers and unread");
                log.MarkRead(ChatChannel.Party); Check(log.Unread(ChatChannel.Party) == 0 && log.Unread(ChatChannel.All) == 1, "mark read");
                int version = log.Version;
                for (int i = 0; i < ChatLog.Capacity; i++) log.Add(ChatChannel.Team, 7, "퀸사이드", "t" + i, false, false, 4);
                Check(log.In(ChatChannel.Team).Count() == ChatLog.Capacity && log.In(ChatChannel.Party).Count() == 3 && log.Version > version, "capacity");
                log.Add(ChatChannel.Team, 7, "퀸사이드", "last", false, false, 5);
                Check(log.In(ChatChannel.Team).Count() == ChatLog.Capacity && log.In(ChatChannel.Team).First().Text == "t1" && log.In(ChatChannel.Team).Last().Number == 51, "oldest dropped");
                log.Clear(ChatChannel.Team); version = log.Version; log.Clear(ChatChannel.Team);
                Check(!log.In(ChatChannel.Team).Any() && log.Version == version && log.Add(ChatChannel.Team, 7, "", "again", false, false, 6).Number == 1 && log.In(ChatChannel.Party).Count() == 3, "clear"); });
            Test("Result screen: crossing order, the winners' first four marked and the fourth clinches", () => {
                var r = MatchResult.Example(0);
                var order = r.FinishOrder();
                Check(string.Join(",", order.Select(l => l.Player.Name)) == "퀸사이드,프로모션,폰돌이,블리츠,갬빗,오프닝", "crossing order");
                Check(order.Select(l => l.Place).SequenceEqual(new[] { 1, 2, 3, 4, 5, 6 }), "overall places");
                Check(order.Where(l => l.CountsToWin).Select(l => l.Player.Name).SequenceEqual(new[] { "퀸사이드", "폰돌이", "블리츠", "오프닝" }), "winners' four");
                Check(order.Count(l => l.Clinched) == 1 && order.Single(l => l.Clinched).Player.Name == "오프닝" && order.Single(l => l.Clinched).TeamPlace == 4, "fourth clinches");
                Check(order.Single(l => l.Player.Name == "갬빗").TeamPlace == 2 && !order.Single(l => l.Player.Name == "갬빗").CountsToWin, "losers never count");
                Check(r.FinishedCount(0) == 4 && r.FinishedCount(1) == 2 && r.GoalCount(0) == 4 && r.GoalCount(1) == 2, "goal counts");
                Check(r.Players.Single(p => p.Self).Name == "폰돌이" && MatchResult.Example(1).Players.Single(p => p.Self).Name == "갬빗", "whose screen"); });
            Test("Result screen: non-finishers nearest first, distances rounded up, clock, majority and draws", () => {
                var r = MatchResult.Example(1);
                Check(string.Join(",", r.NotFinished(1).Select(p => p.Name)) == "스테일메이트,캐슬링,스큐어,포크", "black nearest first");
                Check(string.Join(",", r.NotFinished(0).Select(p => p.Name)) == "룩앤롤,앙파상", "white nearest first");
                r.Players.Add(new ResultPlayer { Name = "봇", Team = 1, MetersLeft = -1 });
                Check(r.NotFinished(1).Last().Name == "봇", "unknown distance last");
                Check(MatchResult.Meters(12f) == "12m" && MatchResult.Meters(11.2f) == "12m" && MatchResult.Meters(-1) == "-", "meters");
                Check(MatchResult.Clock(252) == "4:12" && MatchResult.Clock(59.99) == "0:59" && MatchResult.Clock(-1) == "-" && MatchResult.Clock(double.NaN) == "-", "clock");
                Check(MatchResult.MajorityOf(2) == 2 && MatchResult.MajorityOf(3) == 2 && MatchResult.MajorityOf(4) == 3 && MatchResult.MajorityOf(5) == 3 && MatchResult.MajorityOf(6) == 4, "majority");
                var draw = MatchResult.Example(0); draw.WinningTeam = -1;
                Check(!draw.FinishOrder().Any(l => l.CountsToWin || l.Clinched), "a draw marks nobody"); });
            Test("Client obstacles follow the host's stamped time, not the Steam clock", () => {
                // Host: obstacle time = its physics time + an offset; snapshots every 1/30 s.
                // Client: receives them late by a varying delay, draws 110 ms behind the newest.
                const double offset = 1759000000.0;
                Func<double, double> Host = ms => ms / 1000.0 + offset;
                double playback = 5000 - 110;
                // The drawn moment (4890) lies between the snapshots stamped 4866.667 and 4900.
                double obstacle = HostObstacleClock.Interpolate(Host(4866.667), Host(4900), (playback - 4866.667) / 33.333);
                Check(Math.Abs(obstacle - Host(playback)) < 1e-5, "interpolated obstacle time is not the drawn moment");
                // The old estimate (Steam clock - 110 ms) misses by the one-way delay plus any clock skew.
                double oneWay = 0.08, skew = 0.3;
                double steamEstimate = Host(5000 + oneWay * 1000) + skew - 0.110;
                Check(Math.Abs(steamEstimate - Host(playback)) > 0.35, "scenario should reproduce the old error");
                Check(HostObstacleClock.Interpolate(1, 2, -1) == 1 && HostObstacleClock.Interpolate(1, 2, 3) == 2, "clamp"); });
            Test("Host obstacle clock runs between samples, never shakes back, resets on a new stream", () => {
                var c = new HostObstacleClock(); Check(!c.HasSample && c.Now(1) == 0, "empty");
                c.Sample(100.0, 10.0, 1.0);
                Check(Math.Abs(c.Now(10.05) - 100.05) < 1e-9, "runs on between samples");
                Check(Math.Abs(c.Now(10.5) - 100.1) < 1e-9, "extrapolation is capped");
                c.Sample(100.02, 10.06, 1.0);   // slightly behind what was already shown
                Check(Math.Abs(c.Now(10.06) - 100.1) < 1e-9, "stepped backwards");
                Check(Math.Abs(c.Now(10.2) - 100.12) < 1e-9, "did not resume");
                c.Sample(50.0, 11.0, 1.0);      // a new match or a teleport: far behind
                Check(Math.Abs(c.Now(11.0) - 50.0) < 1e-9, "large jump back not taken");
                c.Sample(200.0, 12.0, 1.15);
                Check(Math.Abs(c.Now(12.04) - (200.0 + 0.04 * 1.15)) < 1e-9, "playback rate ignored");
                c.Sample(double.NaN, 13.0, 1.0); Check(Math.Abs(c.Now(12.04) - (200.0 + 0.04 * 1.15)) < 1e-9, "NaN accepted");
                // R504: a host that became a client holds the time it showed until the new host's stream (the playback
                // delay behind) passes it; a cleared clock took that stream at once and every obstacle stepped back.
                var h = new HostObstacleClock(); h.Hold(500.0, 20.0);
                Check(h.HasSample && Math.Abs(h.Now(20.0) - 500.0) < 1e-9, "not held from the host's own time");
                h.Sample(499.86, 20.02, 1.0);
                Check(h.Now(20.02) >= 500.0 && h.Now(20.1) >= 500.0, "stepped back to the new host's stream");
                h.Sample(500.05, 20.2, 1.0);
                Check(Math.Abs(h.Now(20.2) - 500.05) < 1e-9, "did not take the new stream once it passed");
                h.Hold(double.NaN, 21.0); Check(Math.Abs(h.Now(20.2) - 500.05) < 1e-9, "NaN held");
                c.Clear(); Check(!c.HasSample, "clear"); });
            Test("Player name: two to twelve Hangul, Latin letters or digits, one space between words", () => {
                Check(!PlayerNames.Check("", false).Ok && PlayerNames.Check("   ", true).Error == "", "empty says nothing");
                Check(PlayerNames.Check("체스러버", false).Ok && PlayerNames.Check(" Knight 7 ", false).Ok, "plain names");
                Check(PlayerNames.Check("룩", false).Error == "2자 이상", "too short");
                Check(PlayerNames.Check("가나다라마바사아자차카타파", false).Error == "12자까지", "too long");
                Check(PlayerNames.Check("룩!", false).Error == "한글·영어·숫자만" && PlayerNames.Check("<b>퀸</b>", false).Error == "한글·영어·숫자만", "symbols");
                Check(PlayerNames.Check("철벽  룩", false).Error == "띄어쓰기는 한 칸", "double space"); });
            Test("Player name: an unfinished syllable waits while typing, then reads as an error", () => {
                var typing = PlayerNames.Check("체스ㄹ", true);
                Check(!typing.Ok && typing.Error == "", "quiet while the IME composes");
                Check(PlayerNames.Check("체스ㄹ", false).Error == "다 쓰지 않은 글자", "named once the field is left");
                Check(PlayerNames.Check("ㅋㅋ", true).Error == "" && PlayerNames.Check("ㅋㅋ", false).Error == "다 쓰지 않은 글자", "jamo only"); });
            Test("Player name: banned words anywhere, and gm only as a whole word", () => {
                Check(PlayerNames.Check("바보 폰", false).Error == "쓸 수 없는 말" && PlayerNames.Check("운 영 자", false).Error == "쓸 수 없는 말", "Korean, spaces ignored");
                Check(PlayerNames.Check("ADMIN1", false).Error == "쓸 수 없는 말" && PlayerNames.Check("gm King", false).Error == "쓸 수 없는 말", "Latin");
                Check(PlayerNames.Check("Sigma", false).Ok && PlayerNames.Check("Ogm2", false).Ok, "gm inside a word is fine"); });
            Test("Player name: tidy keeps one space, others' names show only when already valid and tidy", () => {
                Check(PlayerNames.Tidy("  철벽   룩 ") == "철벽 룩" && PlayerNames.Tidy(null) == "", "tidy");
                Check(PlayerNames.Showable("철벽 룩") && !PlayerNames.Showable(" 철벽 룩") && !PlayerNames.Showable("철벽  룩"), "untidy refused");
                Check(!PlayerNames.Showable("") && !PlayerNames.Showable("관리자") && !PlayerNames.Showable("ㅋㅋ"), "invalid refused"); });
            Test("Player name: suggestions are valid and never repeat the current name", () => {
                foreach (var s in PlayerNames.Suggestions) Check(PlayerNames.Check(s, false).Ok, "suggestion " + s);
                var random = new Random(7);
                string current = PlayerNames.Suggestions[0];
                for (int i = 0; i < 200; i++) { string next = PlayerNames.Suggest(current, random); Check(next != current, "repeated"); current = next; }
                Check(PlayerNames.Suggest("체스러버", null) != "체스러버", "any other name"); });
            Test("Voice packet: round trip, and anything else is refused", () => {
                var voice = new byte[] { 9, 8, 7, 6, 5 };
                var b = VoiceWire.Encode(VoiceChannel.Team, 150, 65535, voice, voice.Length);
                Check(b.Length == VoiceWire.HeaderBytes + 5, "size");
                VoiceChannel ch; int gain, at, len; ushort seq;
                Check(VoiceWire.TryDecode(b, b.Length, out ch, out gain, out seq, out at, out len), "decode");
                Check(ch == VoiceChannel.Team && gain == 150 && seq == 65535 && at == VoiceWire.HeaderBytes && len == 5 && b[at + 4] == 5, "fields");
                Check(VoiceWire.Encode(VoiceChannel.Party, 999, 1, voice, 5)[5] == VoiceWire.MaxGain, "gain capped");
                var bad = (byte[])b.Clone(); bad[0] ^= 1; Check(!VoiceWire.TryDecode(bad, bad.Length, out ch, out gain, out seq, out at, out len), "magic");
                bad = (byte[])b.Clone(); bad[4] = 2; Check(!VoiceWire.TryDecode(bad, bad.Length, out ch, out gain, out seq, out at, out len), "channel");
                Check(!VoiceWire.TryDecode(b, VoiceWire.HeaderBytes, out ch, out gain, out seq, out at, out len), "no voice");
                int big = VoiceWire.HeaderBytes + VoiceWire.MaxPayload + 1;
                Check(!VoiceWire.TryDecode(new byte[big], big, out ch, out gain, out seq, out at, out len), "too big");
                Check(VoiceWire.Newer(1, 65535) && !VoiceWire.Newer(65535, 1) && !VoiceWire.Newer(5, 5), "sequence wraps"); });
            Test("Voice channels: the party when someone else is in it, the team otherwise, the pick when it can be used", () => {
                Check(VoiceRules.Default(true) == VoiceChannel.Party && VoiceRules.Default(false) == VoiceChannel.Team, "default rule");
                Check(VoiceRules.Resolve(null, true, true, true) == VoiceChannel.Party, "party first with a party");
                Check(VoiceRules.Resolve(null, false, true, false) == VoiceChannel.Team, "alone: the team");
                Check(VoiceRules.Resolve(VoiceChannel.Team, true, true, true) == VoiceChannel.Team, "picked the whole team");
                Check(VoiceRules.Resolve(VoiceChannel.Party, false, true, false) == VoiceChannel.Team, "the party left: the team");
                Check(VoiceRules.Resolve(null, true, false, true) == VoiceChannel.Party && VoiceRules.Resolve(VoiceChannel.Team, true, false, true) == VoiceChannel.Party, "lobby: the party");
                Check(VoiceRules.Resolve(null, false, false, false) == null, "nobody to talk to"); });
            Test("Voice heard only from the party or a teammate, and only in a channel I am in", () => {
                Check(VoiceRules.Hear(VoiceChannel.Party, true, false, true, true), "party member");
                Check(!VoiceRules.Hear(VoiceChannel.Party, false, true, true, true), "a teammate cannot speak as party");
                Check(VoiceRules.Hear(VoiceChannel.Team, false, true, true, true), "teammate");
                Check(!VoiceRules.Hear(VoiceChannel.Team, true, false, true, true), "the other team, even a party friend");
                Check(!VoiceRules.Hear(VoiceChannel.Party, true, true, false, true) && !VoiceRules.Hear(VoiceChannel.Team, true, true, true, false), "channel left");
                Check(VoiceRules.Label(VoiceChannel.Party) == "파티" && VoiceRules.Label(VoiceChannel.Team) == "팀", "labels"); });
            Test("Voice level: -60 dB and below is 0, full scale 1; 16-bit PCM measured", () => {
                Check(VoiceLevel.FromRms(0) == 0 && VoiceLevel.FromRms(double.NaN) == 0 && VoiceLevel.FromRms(.0005) == 0, "floor");
                Check(Math.Abs(VoiceLevel.FromRms(1) - 1) < 1e-6 && Math.Abs(VoiceLevel.FromRms(.031623) - .5f) < .01, "scale");
                var pcm = new byte[400];
                for (int i = 0; i < 200; i++) { short s = (short)(i % 2 == 0 ? 16384 : -16384); pcm[2 * i] = (byte)s; pcm[2 * i + 1] = (byte)(s >> 8); }
                Check(Math.Abs(VoiceLevel.Rms(pcm, pcm.Length) - .5) < 1e-6 && VoiceLevel.Rms(pcm, 1) == 0 && VoiceLevel.Rms(null, 4) == 0, "rms"); });
            Test("Voice activation opens at the threshold and holds a moment after", () => {
                var g = new VoiceGate();
                Check(!g.Update(0, .2f, .3f), "quiet");
                Check(g.Update(1, .31f, .3f), "loud opens");
                Check(g.Update(1 + VoiceGate.Hold - .01, .1f, .3f), "held after");
                Check(!g.Update(1 + VoiceGate.Hold + .01, .1f, .3f), "then closes");
                g.Update(5, .9f, .3f); g.Reset(); Check(!g.Open && !g.Update(5.01, 0f, .3f), "reset"); });
            Test("Voice mutes: kept as Steam IDs, junk ignored, capped", () => {
                var m = VoiceMuteList.Parse(" 76561198000000001, x, 76561198000000003,,76561198000000001");
                Check(m.Count == 2 && m.Contains(76561198000000001) && m.Contains(76561198000000003), "parse");
                Check(m.Format() == "76561198000000001,76561198000000003", "format sorted");
                Check(m.Set(5, true) && !m.Set(5, true) && m.Set(5, false) && !m.Set(5, false) && !m.Set(0, true), "set");
                Check(VoiceMuteList.Parse(null).Count == 0 && VoiceMuteList.Parse("").Format() == "", "empty");
                var full = new VoiceMuteList();
                for (ulong id = 1; id <= VoiceMuteList.Capacity + 10; id++) full.Set(id, true);
                Check(full.Count == VoiceMuteList.Capacity, "capped"); });
            Test("Voice stream: waits for its cushion, resamples, drops the oldest past the limit, refills after running dry", () => {
                var s = new VoiceStream(8000, .05, .2);   // 400 samples to start, 1600 at most
                var pcm = new byte[600];   // 300 samples of 0.5
                for (int i = 0; i < 300; i++) { pcm[2 * i] = 0; pcm[2 * i + 1] = 0x40; }
                s.Write(pcm, pcm.Length, 1f, 0);
                var stereo = new float[200];   // 100 stereo frames
                Check(s.Read(stereo, 100, 2, 1, 1f) == 0 && stereo[0] == 0f, "silent until primed");
                s.Write(pcm, pcm.Length, 1f, 0);   // 600 waiting now
                Check(s.Read(stereo, 100, 2, 1, 1f) == 100 && Math.Abs(stereo[0] - .5f) < 1e-4 && stereo[0] == stereo[1], "plays, both channels");
                Check(s.Buffered == 500, "a hundred taken");
                var mono = new float[100];
                Check(s.Read(mono, 100, 1, .5, 1f) == 100 && s.Buffered == 450, "half speed takes half as many");
                for (int i = 0; i < 10; i++) s.Write(pcm, pcm.Length, 1f, 1);
                Check(s.Buffered == 1600, "capped at the limit");
                var lots = new float[2000];
                Check(s.Read(lots, 2000, 1, 1, 1f) < 2000 && !s.Playing, "ran dry");
                s.Write(pcm, pcm.Length, 2f, 2);
                Check(s.Read(mono, 10, 1, 1, 1f) == 0, "waits for the cushion again");
                s.Write(pcm, pcm.Length, 2f, 2);
                Check(s.Read(mono, 10, 1, 1, 1f) == 10, "and plays again");
                s.Clear(); Check(s.Buffered == 0 && !s.Playing, "clear");
                var loud = new VoiceStream(8000, .001, .2);   // starts at once
                loud.Write(pcm, pcm.Length, 4f, 0);
                Check(loud.Read(mono, 1, 1, 1, .5f) == 1 && Math.Abs(mono[0] - .5f) < 1e-4, "gain clipped to 1, then volume"); });
            Test("Skill grades: a ring-out or a crowd is !!, a lone hit earns nothing, a miss on an attack is ?", () => {
                var attack=SkillGradeRule.Attack(3); var move=SkillGradeRule.Move(2);
                Check(SkillGrades.Decide(new SkillTally{Enemies=1,RingOuts=1},attack)==SkillGrade.Brilliant,"ring-out");
                Check(SkillGrades.Decide(new SkillTally{Enemies=3},attack)==SkillGrade.Brilliant,"three at once");
                Check(SkillGrades.Decide(new SkillTally{Enemies=2},attack)==SkillGrade.Great,"two of a big three");
                Check(SkillGrades.Decide(new SkillTally{Enemies=1},attack)==SkillGrade.None,"one is plain");
                Check(SkillGrades.Decide(new SkillTally{Enemies=1},move)==SkillGrade.None&&SkillGrades.Decide(new SkillTally{Enemies=2},move)==SkillGrade.Brilliant,"big two");
                Check(SkillGrades.Decide(new SkillTally(),attack)==SkillGrade.Mistake&&SkillGrades.Decide(new SkillTally(),move)==SkillGrade.None,"miss: attacks only");
                Check(SkillGrades.Decide(new SkillTally{Failed=true},move)==SkillGrade.Mistake,"a skill's own whiff");
                Check(SkillGrades.Decide(new SkillTally{Helped=1},move)==SkillGrade.Great,"a save");
                Check(SkillGrades.Decide(new SkillTally{Enemies=1},new SkillGradeRule{Big=3,OneIsGreat=true})==SkillGrade.Great,"edge skills: one is enough"); });
            Test("Skill grades: the caster's fall is ?? or !?, allies' falls ??, and only !! shows to everyone", () => {
                var attack=SkillGradeRule.Attack(3);
                Check(SkillGrades.Decide(new SkillTally{SelfFell=true},attack)==SkillGrade.Blunder,"fell for nothing");
                Check(SkillGrades.Decide(new SkillTally{SelfFell=true,Enemies=1,RingOuts=1},attack)==SkillGrade.Interesting,"took one with it");
                Check(SkillGrades.Decide(new SkillTally{AllyFalls=1},attack)==SkillGrade.Blunder,"only allies fell");
                Check(SkillGrades.Decide(new SkillTally{AllyFalls=1,RingOuts=1,Enemies=1},attack)==SkillGrade.Brilliant,"an enemy out outweighs");
                Check(SkillGrades.ShowsTo(SkillGrade.Brilliant,false)&&!SkillGrades.ShowsTo(SkillGrade.Mistake,false)&&SkillGrades.ShowsTo(SkillGrade.Mistake,true),"who sees");
                Check(!SkillGrades.ShowsTo(SkillGrade.None,true)&&!SkillGrades.ShowsTo(SkillGrade.Missed,true),"nothing to show");
                Check(SkillGrades.Raises(SkillGrade.Mistake,SkillGrade.Brilliant)&&SkillGrades.Raises(SkillGrade.None,SkillGrade.Brilliant),"a late ring-out raises");
                Check(!SkillGrades.Raises(SkillGrade.Brilliant,SkillGrade.Brilliant)&&!SkillGrades.Raises(SkillGrade.Interesting,SkillGrade.Brilliant)&&!SkillGrades.Raises(SkillGrade.None,SkillGrade.Great),"only to !!, once");
                Check(SkillGrades.Symbol(SkillGrade.Interesting)=="!?"&&SkillGrades.Symbol(SkillGrade.Blunder)=="??","symbols"); });
            Test("Sword Fight prototype: pick, countdown, every fall a point, ten wins, and the respawn is the piece it picked (R640)", () => {
                var r = new SwordMatchRules(7); r.Add(1, 0); r.Add(2, 0); r.Add(3, 1);
                Check(r.Pick(1, PieceKind.Knight) && !r.Pick(2, PieceKind.Knight) && r.Pick(3, PieceKind.Knight), "no doubles in a team, across teams fine");
                Check(!r.Pick(1, PieceKind.Queen) && !r.Pick(2, PieceKind.Pawn), "one pick, never a pawn");
                Check(r.InputLocked && r.RingOut(3).Valid == false, "no points before play");
                r.Advance(15.01); Check(r.Phase == SwordPhase.Countdown && r.Get(2).Picked && r.Get(2).Piece != PieceKind.Knight && r.Get(2).Piece != PieceKind.Pawn, "time up: the rest picked");
                r.Advance(3.01); Check(r.Phase == SwordPhase.Playing && !r.InputLocked, "playing");
                for (int i = 0; i < 9; i++)
                {
                    var o = r.RingOut(3); Check(o.Valid && !o.Stole && o.Feed.Cause == SwordCause.Self, "self fall " + i);
                    Check(!r.RingOut(3).Valid, "once per life " + i);
                    Check(!r.TryRespawn(3), "not before 3 s"); r.Advance(3.01); Check(r.TryRespawn(3) && r.Get(3).Piece == PieceKind.Knight && r.Get(3).Alive, "back as its own knight, flag cleared " + i);
                }
                Check(r.White == 9 && r.Phase == SwordPhase.Playing, "nine");
                r.RingOut(3); Check(r.Phase == SwordPhase.Result && r.Winner == 0 && r.Outs[0] == 10, "ten wins"); });
            Test("Sword Fight prototype: the last enemy within 3 s takes the credit, nobody steals a piece (R640), an ally or a stale hit is no credit", () => {
                var r = new SwordMatchRules(1); r.Add(1, 0); r.Add(2, 1); r.Add(3, 1); r.Pick(1, PieceKind.Rook); r.Pick(2, PieceKind.Queen); r.Pick(3, PieceKind.King);
                r.Advance(1); r.Advance(15); r.Advance(3.01);
                r.Get(1).Piece = PieceKind.Pawn;
                r.Hit(1, 2, SwordCause.PawnStep, 0); r.Advance(2.5);
                var o = r.RingOut(2); Check(o.Credit == 1 && !o.Stole && r.Get(1).Piece == PieceKind.Pawn && r.Steals[0] == 0, "a pawn's credit takes no piece");
                Check(o.Feed.Stolen == 255 && o.Feed.Cause == SwordCause.PawnStep, "feed");
                r.Get(1).Piece = PieceKind.Rook;
                r.Hit(1, 3, SwordCause.Sword, 5); o = r.RingOut(3); Check(o.Credit == 1 && !o.Stole && r.Get(1).Piece == PieceKind.Rook, "a piece's credit: no steal");
                r.Advance(3.1); r.TryRespawn(2); Check(r.Get(2).Piece == PieceKind.Queen, "the fallen queen is back a queen");
                r.Advance(3.1); r.TryRespawn(2); r.TryRespawn(3);
                r.Hit(3, 2, SwordCause.Sword, 0); o = r.RingOut(2); Check(o.Credit == 0 && o.Feed.Cause == SwordCause.Self, "an ally's hit is no credit");
                r.Advance(3.1); r.TryRespawn(2);
                r.Get(1).Piece = PieceKind.Pawn; r.Hit(1, 3, SwordCause.Sword, 0); r.Advance(3.2); o = r.RingOut(3); Check(o.Credit == 0 && !o.Stole, "stale after 3 s");
                r.Advance(3.1); r.TryRespawn(3); r.Hit(1, 3, SwordCause.Sword, 0); o = r.RingOut(3); Check(o.Credit == 1 && !o.Stole, "a pawn that fell is no steal"); });
            Test("Sword Fight prototype: a tie at 4:00 goes to sudden death, the next point wins, 60 s more is a draw; the wire carries it", () => {
                var r = new SwordMatchRules(3); r.Add(1, 0); r.Add(2, 1); r.Advance(15.1); r.Advance(3.1);
                r.RingOut(2); r.RingOut(1); for (int i = 0; i < 2401; i++) r.Advance(.1);
                Check(r.Phase == SwordPhase.SuddenDeath, "tie → sudden death");
                r.Advance(3.1); r.TryRespawn(1); r.TryRespawn(2); r.RingOut(1); Check(r.Phase == SwordPhase.Result && r.Winner == 1, "next point wins");
                var d = new SwordMatchRules(3); d.Add(1, 0); d.Add(2, 1); d.Advance(15.1); d.Advance(3.1); d.Advance(240); Check(d.Phase == SwordPhase.SuddenDeath, "0:0 tie");
                d.Advance(60.1); Check(d.Draw, "60 s → draw");
                var w = new SwordMatchRules(5); w.Add(1, 0); w.Add(2, 1); w.Advance(15.1); w.Advance(3.1); w.Advance(200); w.RingOut(2); w.RingOut(1); w.Advance(10);
                Check(w.Phase == SwordPhase.Playing && w.Remaining > 29 && w.Remaining < 31, "clock");
                string text = SwordMatchWire.Encode(w.State(), id => (byte)id); SwordMatchState back = null;
                Check(text.Length < 400 && SwordMatchWire.Decode(text, b => (ulong)b, out back), "round trip " + text);
                Check(back.White == 1 && back.Black == 1 && back.Phase == SwordPhase.Playing && Math.Abs(back.Clock - w.Clock) < .01 && back.Fighters.Count == 2 && back.Feed.Count == 2 && back.Feed[0].Victim == 2, "same state");
                Check(!SwordMatchWire.Decode("9,0,0|", b => 1, out back) && !SwordMatchWire.Decode("x", b => 1, out back), "junk refused"); });
            // R502: the old host's last text can be a tenth of a second older than the bodies a new host takes over.
            Test("Sword Fight takeover: the new host's rules take who is out and each piece from the bodies, so no piece stays out or falls for ever", () => {
                var r = new SwordMatchRules(4); r.Add(1, 0); r.Add(2, 1); r.Add(3, 1); r.Pick(1, PieceKind.Rook); r.Pick(2, PieceKind.Queen); r.Pick(3, PieceKind.King);
                r.Advance(15.1); r.Advance(3.1);
                Check(!r.Adopt(1, true, PieceKind.Rook, 0) && !r.Adopt(2, true, PieceKind.Queen, 0) && r.White == 0 && r.Black == 0, "bodies that agree change nothing");
                Check(r.Adopt(2, false, PieceKind.Queen, 2.8) && !r.Get(2).Alive && r.White == 1 && r.Feed.Count == 1 && r.Feed[0].Credit == 0 && r.Feed[0].Cause == SwordCause.Self, "a fall in the gap: the point, no credit");
                Check(!r.TryRespawn(2), "back before the body's own count"); r.Advance(2.85); Check(r.TryRespawn(2) && r.Get(2).Piece == PieceKind.Queen, "back after the body's count, as its own queen (R640)");
                r.RingOut(3); Check(!r.Get(3).Alive && r.White == 2, "setup: out in the rules");
                Check(r.Adopt(3, true, PieceKind.King, 0) && r.Get(3).Alive && r.Get(3).Piece == PieceKind.King && r.RespawnLeft(3) == 0, "a comeback in the gap is not taken (its own king, R640)");
                Check(r.RingOut(3).Valid && r.White == 3, "a piece taken back does not fall again");
                r.Get(1).Piece = PieceKind.Pawn; Check(r.Adopt(1, true, PieceKind.Queen, 0) && r.Get(1).Piece == PieceKind.Queen, "a steal in the gap: the body's piece");
                Check(r.Adopt(2, false, PieceKind.Pawn, double.NaN) && Math.Abs(r.RespawnLeft(2) - r.Settings.RespawnDelay) < 1e-9, "no count on the body: the full delay");
                var p = new SwordMatchRules(2); p.Add(1, 0); p.Add(2, 1); p.Pick(1, PieceKind.Rook);
                Check(!p.Adopt(1, true, PieceKind.Pawn, 0) && p.Get(1).Piece == PieceKind.Rook, "a pick is not a body's piece yet");
                p.Advance(15.1); Check(p.Phase == SwordPhase.Countdown && p.Adopt(1, false, PieceKind.Rook, 1) && !p.Get(1).Alive && p.White == 0 && p.Black == 0, "out of play: no point, the piece waits");
                p.Advance(3.1); Check(p.Phase == SwordPhase.Playing && p.TryRespawn(1), "and comes back");
                var w = new SwordMatchRules(6); w.Add(1, 0); w.Add(2, 1); w.Advance(15.1); w.Advance(3.1);
                for (int i = 0; i < 9; i++) { w.RingOut(2); w.Advance(3.01); w.TryRespawn(2); }
                Check(w.White == 9 && w.Phase == SwordPhase.Playing, "setup: nine");
                Check(w.Adopt(2, false, PieceKind.Pawn, 1) && w.White == 10 && w.Phase == SwordPhase.Result && w.Winner == 0, "the tenth point in the gap ends the match"); });
            // R504: a client's screen ran the match clock on without end, a new host went back to the last text's time, and a
            // text overtaken by the next one was shown after it.
            Test("Sword Fight clocks: a client's runs on a second at most past the last text, a new host goes on from what it showed, an overtaken text is left", () => {
                var r = new SwordMatchRules(9); r.Add(1, 0); r.Add(2, 1); r.Advance(15.1); r.Advance(3.1); r.Advance(20);
                var s = r.State();
                Check(s.Phase == SwordPhase.Playing && Math.Abs(s.ClockAfter(0.4) - (s.Clock + 0.4)) < 1e-9 && Math.Abs(s.PhaseTimeAfter(0.4) - (s.PhaseTime + 0.4)) < 1e-9, "runs on between texts");
                Check(Math.Abs(s.ClockAfter(4.5) - (s.Clock + SwordMatchState.MoveOnLimit)) < 1e-9 && s.ClockAfter(-1) == s.Clock && s.ClockAfter(double.NaN) == s.Clock, "a silent host's clock ran on");
                var n = new SwordMatchRules(s.Seed); n.Add(1, 0); n.Add(2, 1); n.Restore(s, 4.5);
                Check(n.Phase == SwordPhase.Playing && Math.Abs(n.Clock - s.ClockAfter(4.5)) < 1e-9 && Math.Abs(n.PhaseTime - s.PhaseTimeAfter(4.5)) < 1e-9, "the new host went back to the text's time");
                var pick = new SwordMatchRules(3).State();
                Check(pick.ClockAfter(2) == 0 && Math.Abs(pick.PhaseTimeAfter(0.5) - 0.5) < 1e-9, "the match clock runs in play only");
                var end = new SwordMatchRules(9); end.Add(1, 0); end.Add(2, 1); end.Advance(15.1); end.Advance(3.1); end.Advance(end.Settings.Duration - 0.3);
                var last = end.State(); var e2 = new SwordMatchRules(last.Seed); e2.Add(1, 0); e2.Add(2, 1); e2.Restore(last, 0.8);
                Check(e2.Phase == SwordPhase.Playing && e2.Clock <= e2.Settings.Duration && e2.Advance(0.02) && e2.Phase != SwordPhase.Playing, "past the end of play: the rules end it");
                var t1 = r.State(); r.Advance(0.1); var t2 = r.State();
                Check(SwordMatchWire.Older(t1, t2) && !SwordMatchWire.Older(t2, t1) && !SwordMatchWire.Older(t2, t2), "an overtaken text in the same phase");
                var play = new SwordMatchState { Phase = SwordPhase.Playing, PhaseTime = 50, Seed = 5 };
                var sudden = new SwordMatchState { Phase = SwordPhase.SuddenDeath, PhaseTime = 0.1, Seed = 5 };
                Check(SwordMatchWire.Older(play, sudden) && !SwordMatchWire.Older(sudden, play), "a text from the phase before");
                Check(!SwordMatchWire.Older(new SwordMatchState { Phase = SwordPhase.Pick, Seed = 6 }, sudden) && !SwordMatchWire.Older(play, null), "a new round (another seed) is not older"); });
            Test("Sword Fight board: the ghost passes over 20 seeds never leave an island and keep two respawn squares a side", () => {
                for (int seed = 0; seed < 20; seed++)
                {
                    var plan = SwordGhosts.Plan(seed); Check(plan.Count == 4 && plan[3].Cells.Length == 28, "four passes, the rim " + seed);
                    Check(plan[0].Cells.Length >= 4 && plan[0].Cells.Length <= 6 && plan[1].Cells.Length <= 4 && plan[2].Cells.Length <= 3, "rook 6, bishop 4, knight 3 less pedestals (or skipped after 20 tries) " + seed);
                    foreach (var g in plan) foreach (int cell in g.Cells) Check(!SwordBoard.Obstacle(cell), "a pedestal's square stays " + seed);
                    var again = SwordGhosts.Plan(seed); Check(string.Join(",", again[1].Cells) == string.Join(",", plan[1].Cells), "same seed, same squares");
                    foreach (double t in new[] { 0.0, 121.6, 151.6, 181.6, 211.6 })
                    {
                        var e = SwordGhosts.ErasedAt(plan, t); bool q = SwordGhosts.QueenDone(plan, t);
                        Check(SwordBoard.Connected(e), "one piece at " + t + " seed " + seed);
                        Check(SwordBoard.SpawnCells(0, e, q).Count >= 2 && SwordBoard.SpawnCells(1, e, q).Count >= 2, "respawn squares " + seed);
                    }
                }
                Check(SwordBoard.StartCell(0, 0) == SwordBoard.Cell(3, 1) && SwordBoard.StartCell(1, 0) == SwordBoard.Cell(3, 6), "start squares");
                for (int t2 = 0; t2 < 2; t2++) for (int sl = 0; sl < 12; sl++) Check(!SwordBoard.Obstacle(SwordBoard.StartCell(t2, sl)), "no start on a pedestal");
                foreach (int cell in SwordBoard.SpawnCells(0, new bool[64], false)) Check(!SwordBoard.Obstacle(cell), "no respawn on a pedestal");
                Check(SwordBoard.Rail(SwordBoard.Cell(0, 2)) && SwordBoard.Rail(SwordBoard.Cell(7, 5)) && !SwordBoard.Rail(SwordBoard.Cell(0, 1)), "rails on a3-a6, h3-h6");
                var none = new bool[64];
                int c = SwordBoard.ChooseSpawn(0, none, false, 2, new double[] { 0 }, new double[] { -5 }, new double[] { -7 }, new double[] { -3 });
                double cx, cz; SwordBoard.Centre(c, 2, out cx, out cz); Check(SwordBoard.Y(c) == 1 || SwordBoard.Y(c) == 2, "own ranks"); Check(cx > 0, "away from the enemy");
                Check(SwordBoard.At(cx, cz, 2) == c && SwordBoard.At(9, 0, 2) == -1, "square under a point"); });
            Test("Piece emotes: slots 1 and 2 play for every piece but the pawn, 3 and 4 stay locked, slot 0 is none", () => {
                foreach (PieceKind kind in new[] { PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight, PieceKind.King, PieceKind.Queen })
                {
                    Check(PieceEmotes.Available(kind, 1) && PieceEmotes.Available(kind, 2), "first two open " + kind);
                    Check(!PieceEmotes.Available(kind, 0) && !PieceEmotes.Available(kind, 3) && !PieceEmotes.Available(kind, 4) && !PieceEmotes.Available(kind, 5), "rest locked " + kind);
                }
                for (int slot = 0; slot <= 5; slot++) Check(!PieceEmotes.Available(PieceKind.Pawn, slot), "pawn has none yet");
                Check(!PieceEmotes.Available((PieceKind)9, 1), "unknown piece"); });
            Test("Piece emotes: every piece names all four slots and each slot has a length", () => {
                for (int kind = 0; kind < ChessPieces.Count; kind++)
                    for (int slot = 1; slot <= PieceEmotes.Slots; slot++)
                        Check(PieceEmotes.Name((PieceKind)kind, slot).Length > 0, "name " + kind + "/" + slot);
                for (int slot = 1; slot <= PieceEmotes.Slots; slot++) Check(PieceEmotes.Duration(slot) > 0.5f, "length " + slot);
                Check(PieceEmotes.Name(PieceKind.Rook, 0) == "" && PieceEmotes.Duration(0) == 0f && PieceEmotes.Duration(5) == 0f, "slot 0 and 5 are nothing"); });
            Test("Skill clips (R190): each stage plays its span of the clip, stretched over a set length or at 1x and held", () => {
                var m = new[] { 0f, 0.6f, 0.9f, 1.5f };
                Func<float, float, bool> Near = (a, b) => Math.Abs(a - b) < 1e-4f;
                Check(Near(SkillClipTiming.At(m, SkillClipTiming.Windup, 0f, 0.4f, 1.5f), 0f), "windup starts at mark 0");
                Check(Near(SkillClipTiming.At(m, SkillClipTiming.Windup, 0.2f, 0.4f, 1.5f), 0.3f), "windup stretched: half way");
                Check(Near(SkillClipTiming.At(m, SkillClipTiming.Windup, 0.9f, 0.4f, 1.5f), 0.6f), "windup over: held at mark 1");
                Check(Near(SkillClipTiming.At(m, SkillClipTiming.Active, 0.1f, 0f, 1.5f), 0.7f), "active of no set length: 1x");
                Check(Near(SkillClipTiming.At(m, SkillClipTiming.Active, 2f, 0f, 1.5f), 0.9f), "active held at mark 2");
                Check(Near(SkillClipTiming.At(m, SkillClipTiming.Recovery, 0.45f, 0.45f, 1.5f), 1.5f), "recovery ends at the clip's end");
                Check(Near(SkillClipTiming.At(null, SkillClipTiming.Active, 0f, 0f, 3f), 1f), "no marks: thirds");
                var bad = new[] { 0f, 2f, 1f, 9f };
                Check(Near(SkillClipTiming.At(bad, SkillClipTiming.Active, 5f, 0f, 1.5f), 1.5f), "out of order and out of the clip: in order, inside it");
                Check(Near(SkillClipTiming.At(m, SkillClipTiming.Recovery, -1f, 0.3f, 1.5f), 0.9f), "no time before a stage");
                Check(Near(SkillClipTiming.In(0f, 0.1f, 0.8f), 0f) && Near(SkillClipTiming.In(0.05f, 0.1f, 0.8f), 0.4f)
                      && Near(SkillClipTiming.In(1f, 0.1f, 0.8f), 0.8f) && Near(SkillClipTiming.In(0f, 0f, 2f), 1f), "blend in, capped"); });
            Test("Autopilot (R297): the same seed and steps give the same keys, another seed other keys", () => {
                var a = new AutoPilot(1234); var b = new AutoPilot(1234); var c = new AutoPilot(99);
                bool differs = false;
                for (int i = 0; i < 20000; i++)
                {
                    float dt = i % 7 == 0 ? 0.033f : 0.0167f;
                    var x = a.Step(dt); var y = b.Step(dt); var z = c.Step(dt);
                    Check(x.MoveX == y.MoveX && x.MoveY == y.MoveY && x.Sprint == y.Sprint && x.Jump == y.Jump && x.Skill == y.Skill
                          && x.Click == y.Click, "same seed, step " + i);
                    if (x.MoveX != z.MoveX || x.Jump != z.Jump || x.Skill != z.Skill) differs = true;
                }
                Check(differs, "another seed plays differently"); });
            Test("Autopilot (R297): walks forward on the whole, presses now and then, a move never longer than 1", () => {
                var p = new AutoPilot(7);
                int jumps = 0, skills = 0, clicks = 0, moving = 0, steps = 60 * 600;   // ten minutes at 60 frames a second
                double forward = 0;
                for (int i = 0; i < steps; i++)
                {
                    var f = p.Step(1f / 60f);
                    float length = (float)Math.Sqrt(f.MoveX * f.MoveX + f.MoveY * f.MoveY);
                    Check(length <= 1.0001f, "length " + length);
                    if (length > 0.5f) { moving++; forward += f.MoveY; }
                    if (f.Sprint) Check(length > 0.5f, "no sprint standing still");
                    if (f.Jump) jumps++;
                    if (f.Skill) skills++;
                    if (f.Click) clicks++;
                }
                double minutes = steps / 3600.0;
                Check(moving > steps * 0.7, "mostly walking: " + moving);
                Check(forward / moving > 0.6, "forward on the whole: " + forward / moving);
                Check(jumps / minutes >= 20 && jumps / minutes <= 80, "jumps a minute " + jumps / minutes);
                Check(skills / minutes >= 5 && skills / minutes <= 16, "skill presses a minute " + skills / minutes);
                Check(clicks / minutes >= 9 && clicks / minutes <= 31, "clicks a minute " + clicks / minutes);
                // A press is one step: the next step never repeats it.
                var q = new AutoPilot(7);
                bool last = false;
                for (int i = 0; i < 6000; i++) { var f = q.Step(1f / 60f); Check(!(f.Jump && last), "jump held"); last = f.Jump; } });
            Test("Autopilot (R297): asked on the command line, with a seed of its own or one made from the PC's id", () => {
                uint seed, s1, s2;
                Check(!AutoPilot.Requested(new[] { "game.exe" }, 5, out seed) && !AutoPilot.Requested(null, 5, out seed), "not asked");
                Check(AutoPilot.Requested(new[] { "game.exe", "-autopilot=42" }, 5, out seed) && seed == 42, "given seed");
                Check(!AutoPilot.Requested(new[] { "-autopilot=x" }, 5, out seed), "not a number");
                Check(AutoPilot.Requested(new[] { "-autopilot" }, 76561198000000001UL, out s1)
                      && AutoPilot.Requested(new[] { "-autopilot" }, 76561198000000002UL, out s2) && s1 != s2 && s1 != 0, "every PC its own");
                Check(AutoPilot.Requested(new[] { "-autopilot" }, 0, out seed) && seed != 0, "never 0"); });
            Test("Ragdoll snapshot (R297, R541): 30-byte header, 67 bytes a pawn and 76 while its hook is out of the hand; 12 fit in 1024", () => {
                Check(RagdollWire.HeaderBytes == 30 && RagdollWire.PoseBytes == 67 && RagdollWire.MaxPoseBytes == 76 && RagdollWire.HookBytes == 9, "sizes");
                Check(RagdollWire.HeaderBytes + 12 * RagdollWire.MaxPoseBytes <= RagdollWire.MaxBytes, "12 swung hooks fit");
                var poses = new List<PoseWire>();
                for (int i = 0; i < 14; i++) poses.Add(WirePose(i + 1, 0));
                Check(RagdollWire.Snapshot(9, 1, 2, 0, poses, 12).Length == 834, "12 pawns, hooks put away: 834 bytes (810 from R297, 918 before)");
                poses[3].Hook = 1;
                Check(RagdollWire.Snapshot(9, 1, 2, 0, poses, 12).Length == 834, "a hook in the hand adds nothing");
                poses[5].Hook = 3;
                Check(RagdollWire.Snapshot(9, 1, 2, 0, poses, 12).Length == 843, "a flying hook adds its 9");
                Check(RagdollWire.Snapshot(9, 1, 2, 0, poses, 14).Length == 843, "never more than 12 pawns");
                Check(RagdollWire.Snapshot(9, 1, 2, 0, poses, 0).Length == 30, "none"); });
            Test("Ragdoll snapshot (R297): every field comes back; a hook put away or in the hand reads 0 for its gauge and point", () => {
                var poses = new List<PoseWire>();
                for (int i = 0; i < 12; i++) poses.Add(WirePose(i + 1, (byte)(i % 6)));
                byte[] bytes = RagdollWire.Snapshot(77, 123456, 987654, 4321.25, poses, 12);
                var into = new SnapshotWire();
                Check(RagdollWire.ReadSnapshot(bytes, 77, into), "read");
                Check(into.Tick == 123456 && into.HostTimeMs == 987654 && into.ObstacleTime == 4321.25 && into.Count == 12, "header");
                for (int i = 0; i < 12; i++)
                {
                    PoseWire a = poses[i], b = into.At(i);
                    Check(SameFixed(a, b), "pawn " + i);
                    bool tail = a.Hook >= RagdollWire.HookTail;
                    Check(b.HookCharge == (tail ? a.HookCharge : 0) && b.HookX == (tail ? a.HookX : 0) && b.HookY == (tail ? a.HookY : 0)
                          && b.HookZ == (tail ? a.HookZ : 0), "hook gauge and point, pawn " + i);
                } });
            Test("Ragdoll snapshot (R297): read only if it ends exactly where its last pawn does, every field in range", () => {
                var into = new SnapshotWire();
                byte[] good = RagdollWire.Snapshot(5, 1, 1, 0, new List<PoseWire> { WirePose(1, 0), WirePose(2, 4), WirePose(3, 1) }, 3);
                Check(good.Length == 30 + 67 + 76 + 67 && RagdollWire.ReadSnapshot(good, 5, into), "good");
                Check(!RagdollWire.ReadSnapshot(Resized(good, good.Length - 1), 5, into), "a byte short");
                Check(!RagdollWire.ReadSnapshot(Resized(good, good.Length + 1), 5, into), "a byte over");
                Check(!RagdollWire.ReadSnapshot(Resized(good, good.Length - RagdollWire.HookBytes), 5, into), "a hook's tail short");
                Check(!RagdollWire.ReadSnapshot(Resized(good, 1025), 5, into) && !RagdollWire.ReadSnapshot(Resized(good, 29), 5, into), "too long, too short");
                Check(!RagdollWire.ReadSnapshot(good, 6, into), "another match");
                var bad = (byte[])good.Clone(); bad[0] ^= 1; Check(!RagdollWire.ReadSnapshot(bad, 5, into), "magic");
                bad = (byte[])good.Clone(); bad[4] = 3; Check(!RagdollWire.ReadSnapshot(bad, 5, into), "type");
                bad = (byte[])good.Clone(); bad[29] = 2; Check(!RagdollWire.ReadSnapshot(bad, 5, into), "count 2");
                bad = (byte[])good.Clone(); bad[29] = 4; Check(!RagdollWire.ReadSnapshot(bad, 5, into), "count 4");
                bad = (byte[])good.Clone(); bad[29] = 13; Check(!RagdollWire.ReadSnapshot(bad, 5, into), "count 13");
                // The second pawn (from byte 97): its hook at +63 (R541: id 8, point 8, rotations 44, flags, stamina,
                // escape). As 1 its tail would be read as the third pawn.
                bad = (byte[])good.Clone(); bad[97 + 63] = 1; Check(!RagdollWire.ReadSnapshot(bad, 5, into), "a tail behind a hook in the hand");
                bad = (byte[])good.Clone(); bad[97 + 63] = 6; Check(!RagdollWire.ReadSnapshot(bad, 5, into), "hook 6");
                bad = (byte[])good.Clone(); bad[30 + 60] = 3; Check(!RagdollWire.ReadSnapshot(bad, 5, into), "state 3");
                bad = (byte[])good.Clone(); bad[30 + 64] = 200; Check(!RagdollWire.ReadSnapshot(bad, 5, into), "no such piece");
                bad = (byte[])good.Clone(); Array.Clear(bad, 30, 8); Check(!RagdollWire.ReadSnapshot(bad, 5, into), "id 0");
                bad = (byte[])good.Clone(); Array.Copy(bad, 30, bad, 30 + 67 + 76, 8); Check(!RagdollWire.ReadSnapshot(bad, 5, into), "an id twice");
                Check(!RagdollWire.ReadSnapshot(RagdollWire.Snapshot(5, 1, 1, double.NaN, new List<PoseWire> { WirePose(1, 0) }, 1), 5, into), "NaN clock");
                Check(RagdollWire.ReadSnapshot(good, 5, into) && into.Count == 3, "a refused packet leaves the next one readable"); });
            Test("Ragdoll snapshot (R297): the 16-bit echo of a send time still gives the round trip across its wrap", () => {
                Check(RagdollWire.AckAge(70000, 69950u & 0xFFFF) == 50, "plain");
                Check(RagdollWire.AckAge(65540u, 65500u & 0xFFFF) == 40, "across 65536");
                Check(RagdollWire.AckAge(4000000120u, 4000000020u & 0xFFFF) == 100, "late in the clock");
                Check(RagdollWire.AckAge(123, 0) == -1, "no echo");
                var poses = new List<PoseWire> { WirePose(1, 0) };
                uint sent = 1234567890u;
                poses[0].Ack = (ushort)sent;
                var into = new SnapshotWire();
                Check(RagdollWire.ReadSnapshot(RagdollWire.Snapshot(1, 1, 1, 0, poses, 1), 1, into) && RagdollWire.AckAge(sent + 87, into.At(0).Ack) == 87,
                      "through a packet"); });
            Test("Packets (R297 [3]): every Core packet's bytes for a fixed input are the ones pinned before their buffers were reused", () => {
                string got = GoldenBytes();
                Check(got == Golden, "bytes changed: " + got);
                Check(GoldenBytes() == Golden, "and again, through the reused buffers"); });
            Test("Packets (R297 [3]): reused streams write and read like fresh ones, a short packet after a long one too", () => {
                var streams = new PacketStreams(64);
                var w = streams.Begin();
                for (int i = 0; i < 40; i++) w.Write((ulong)i * 0x0101010101010101UL);   // 320 bytes: past its first capacity
                Check(streams.Finish().Length == 320, "a long packet");
                w = streams.Begin(); w.Write(0xDEADBEEFu); w.Write((short)-2); w.Write(1.5f); w.Write(-0.25d); w.Write((sbyte)-7); w.Write(true);
                byte[] shortOne = streams.Finish();
                byte[] fresh = Through(f => { f.Write(0xDEADBEEFu); f.Write((short)-2); f.Write(1.5f); f.Write(-0.25d); f.Write((sbyte)-7); f.Write(true); });
                Check(shortOne.SequenceEqual(fresh), "the short one's bytes are a fresh writer's");
                var r = streams.Open(new byte[300]);
                r.ReadBytes(300);
                r = streams.Open(shortOne);
                Check(r.ReadUInt32() == 0xDEADBEEFu && r.ReadInt16() == -2 && r.ReadSingle() == 1.5f && r.ReadDouble() == -0.25d && r.ReadSByte() == -7 && r.ReadBoolean(), "read back");
                bool ended = false;
                try { r.ReadByte(); } catch (System.IO.EndOfStreamException) { ended = true; }
                Check(ended, "the short packet ends where it does, not where the long one did");
                r = streams.Open(new byte[] { 1, 2, 3, 4, 5 }, 3);
                ended = false;
                try { r.ReadUInt32(); } catch (System.IO.EndOfStreamException) { ended = true; }
                Check(ended, "only the length asked for");
                var pool = new PacketPool(16);
                Check(pool.Get(10) == pool.Get(10) && pool.Get(10).Length == 10 && pool.Get(9) != pool.Get(10), "one array per length");
                Check(pool.Get(17) != pool.Get(17) && pool.Get(0).Length == 0, "past its largest length: a new array each time"); });
            Test("Flat motion snapshot (R297 [5]): never more than twelve pawns, so a thirteenth no longer makes it unreadable", () => {
                var many = new List<PawnState>();
                for (int i = 1; i <= 13; i++) many.Add(PawnMotor.Spawn((ulong)i, (i - 1) % 2, (i - 1) % 6));
                var bytes = MotionProtocol.Snapshot(5, 1, many);
                Check(bytes.Length == 18 + 12 * 30, "twelve pawns' bytes, got " + bytes.Length);
                Check(MotionProtocol.ReadSnapshot(bytes, 5, out uint tick, out var got) && got.Count == 12 && got[11].Id == 12, "the first twelve, readable"); });
            Test("Skill moments (R297 [5]): a client's 0 is nothing confirmed yet, so a host past 32767 moments still reaches it", () => {
                var log = new SkillEventLog();
                for (uint ms = 0; ms < 40000; ms++) { log.Add(new SkillWireEvent { Mode = SkillWire.ModePawnRush, Kind = 1 }, ms); log.Expire(ms); }
                Check(SkillWire.Newer(log.First, 0) == false, "the log is numbered past 32767 (first " + log.First + ")");
                var batch = new List<SkillWireEvent>();
                log.After(0, 255, batch);
                Check(batch.Count == 0, "taken as a number, 0 hid every moment");
                ushort acked = 0; bool set = false;
                SkillWire.TakeAck(0, ref acked, ref set);
                Check(!set, "0 sets nothing");
                log.After(set ? acked : unchecked((ushort)(log.First - 1)), 255, batch);
                Check(batch.Count == 255, "so the client gets the moments kept, got " + batch.Count);
                ushort confirmed = unchecked((ushort)(log.First + 9));
                SkillWire.TakeAck(confirmed, ref acked, ref set);
                Check(set && acked == confirmed, "its first real confirmation counts, though not newer than 0");
                SkillWire.TakeAck(0, ref acked, ref set);
                SkillWire.TakeAck(unchecked((ushort)(confirmed - 3)), ref acked, ref set);
                Check(acked == confirmed, "a later 0 or an older number changes nothing");
                SkillWire.TakeAck(unchecked((ushort)(confirmed + 5)), ref acked, ref set);
                Check(acked == unchecked((ushort)(confirmed + 5)), "a newer one moves it on"); });
            Test("Metrics box grades (R521): green, orange and red at each number's two lines; grey while nothing is measured", () => {
                Check(NetHealth.Fps(60) == NetGrade.Good && NetHealth.Fps(55) == NetGrade.Good && NetHealth.Fps(45) == NetGrade.Warn
                      && NetHealth.Fps(29) == NetGrade.Bad && NetHealth.Fps(0) == NetGrade.None, "frames: green from 55, orange from 30");
                Check(NetHealth.Ping(40) == NetGrade.Good && NetHealth.Ping(80) == NetGrade.Good && NetHealth.Ping(81) == NetGrade.Warn
                      && NetHealth.Ping(150) == NetGrade.Warn && NetHealth.Ping(151) == NetGrade.Bad, "ping: 80 and 150 ms");
                Check(NetHealth.Ping(0) == NetGrade.None && NetHealth.Ping(-1) == NetGrade.None, "no round trip yet is grey, not green");
                Check(NetHealth.AtMost(0, NetHealth.PingGood, NetHealth.PingWarn) == NetGrade.Good, "Steam's 0 ms on a LAN is green");
                Check(NetHealth.Felt(120) == NetGrade.Good && NetHealth.Felt(200) == NetGrade.Warn && NetHealth.Felt(300) == NetGrade.Bad
                      && NetHealth.Felt(-1) == NetGrade.None, "a press until it shows: 150 and 250 ms");
                Check(NetHealth.Loss(0) == NetGrade.Good && NetHealth.Loss(0.01) == NetGrade.Good && NetHealth.Loss(0.03) == NetGrade.Warn
                      && NetHealth.Loss(0.06) == NetGrade.Bad, "loss: 1% and 5%");
                Check(NetHealth.Jitter(4) == NetGrade.Good && NetHealth.Jitter(20) == NetGrade.Warn && NetHealth.Jitter(31) == NetGrade.Bad, "jitter: 10 and 30 ms");
                Check(NetHealth.Snapshots(60, 60) == NetGrade.Good && NetHealth.Snapshots(54, 60) == NetGrade.Good
                      && NetHealth.Snapshots(40, 60) == NetGrade.Warn && NetHealth.Snapshots(19, 60) == NetGrade.Bad, "60 aimed at: green from 54, orange from 20");
                Check(NetHealth.Snapshots(27, 30) == NetGrade.Good && NetHealth.Snapshots(25, 30) == NetGrade.Warn
                      && NetHealth.Snapshots(0, 30) == NetGrade.Bad && NetHealth.Snapshots(5, 0) == NetGrade.None, "30 aimed at: green from 27; no rate is grey");
                Check(NetHealth.Pace(1.0) == NetGrade.Good && NetHealth.Pace(0.9) == NetGrade.Warn && NetHealth.Pace(0.5) == NetGrade.Bad
                      && NetHealth.Pace(-1) == NetGrade.None, "physics against real time: 95% and 80%");
                Check(NetHealth.Upload(261) == NetGrade.Good && NetHealth.Upload(500) == NetGrade.Warn && NetHealth.Upload(900) == NetGrade.Bad, "upload: 350 and 700 KB/s");
                Check(NetHealth.Worse(NetGrade.None, NetGrade.Good) == NetGrade.Good && NetHealth.Worse(NetGrade.Warn, NetGrade.Good) == NetGrade.Warn
                      && NetHealth.Worse(NetGrade.Warn, NetGrade.Bad) == NetGrade.Bad, "the head shows the worst; grey counts for nothing");
                Check(NetHealth.AtMost(double.NaN, 1, 2) == NetGrade.None && NetHealth.AtLeast(double.NaN, 2, 1) == NetGrade.None, "NaN is not measured"); });
            Test("Metrics box grades (R521): the lines agree with the snapshot rates, the jitter buffer and twelve players' upload", () => {
                Check(NetHealth.TargetFromInterval(1000.0 / 60) == 60 && NetHealth.TargetFromInterval(1000.0 / 30) == 30
                      && NetHealth.TargetFromInterval(0) == 30 && NetHealth.TargetFromInterval(PlaybackDelay.DefaultIntervalMs) == 30, "a client reads 30 or 60 from the interval");
                int slow = (int)Math.Round(1 / SnapshotRate.Normal);
                Check(NetHealth.Snapshots(slow, slow) == NetGrade.Good && NetHealth.RateWarn < slow, "the slow rate itself is green, and its shortfall orange before red");
                Check(NetHealth.LossWarn <= PlaybackDelay.LossyShare, "red by the time the jitter buffer waits a snapshot more for losses");
                Check(NetHealth.JitterGood > PlaybackDelay.FreeJitterMs, "the jitter that costs nothing is green");
                double twelve = SnapshotRate.SnapshotBytes(12) * (1 / SnapshotRate.Normal) * 11 / 1024.0;
                Check(twelve < NetHealth.UploadGood && twelve > NetHealth.UploadGood / 2, "twelve players at 30 a second (" + twelve.ToString("0") + " KB/s) are green, not far under the line");
                Check(NetHealth.FpsGood <= 1 / SnapshotRate.Fast, "60 frames a second is green");
                var none = RouteSummary.None;
                Check(!none.Known && none.PingMs < 0, "no route known yet");
                Check(new RouteSummary { Relayed = 1, PingMs = 30 }.Known, "a relayed route is a known one"); });
            Test("Runaway pawn (R531): flung hips are past every speed a course gives, broken numbers too", () => {
                Check(!PawnRushRunaway.Flung(0f, 0f, 0f) && !PawnRushRunaway.Flung(9.6f, 0f, 0f), "standing, the sprint");
                Check(!PawnRushRunaway.Flung(0f, 14f, 8f) && !PawnRushRunaway.Flung(9.6f, 18f, 0f), "a launch pad, one taken at a sprint");
                Check(!PawnRushRunaway.Flung(0f, -34.3f, 0f) && !PawnRushRunaway.Flung(28f, 6f, -4f), "a 60 m fall, the robot arm's hardest hit (29 m/s)");
                Check(!PawnRushRunaway.Flung(32f, 0f, 9.6f) && !PawnRushRunaway.Flung(0f, -49.9f, 0f), "the rook's chain at a sprint, just under the line");
                Check(PawnRushRunaway.Flung(1800f, 550f, -465f) && PawnRushRunaway.Flung(40f, 0f, 40f) && PawnRushRunaway.Flung(0f, -50.5f, 0f),
                      "a wrapping boat's bat (11-09), fast sideways, a fall past the line");
                Check(PawnRushRunaway.Flung(float.NaN, 0f, 0f) && PawnRushRunaway.Flung(0f, float.PositiveInfinity, 0f), "broken numbers"); });
            Test("Host fitness (R533): the graphics card counts too - a fast one costs nothing, a slow one the square root of how much slower, never under 40%", () => {
                int cpu = HostFitness.Base(40, 8, 16000, false);
                Check(cpu == 1000 && HostFitness.Base(40, 8, 16000, false, 0) == cpu && HostFitness.Base(40, 8, 16000, false, -1) == cpu, "an unmeasured card costs nothing");
                Check(HostFitness.Base(40, 8, 16000, false, 3) == cpu && HostFitness.Base(40, 8, 16000, false, HostFitness.ReferenceGpuMs) == cpu, "a card at or under the reference time costs nothing");
                Check(HostFitness.Base(40, 8, 16000, false, 24) == cpu / 2, "four times slower than the reference: half, got " + HostFitness.Base(40, 8, 16000, false, 24));
                int entry = HostFitness.Base(40, 8, 16000, false, 9), chip = HostFitness.Base(40, 8, 16000, false, 80), worse = HostFitness.Base(40, 8, 16000, false, 400);
                Check(entry < cpu && entry > cpu * 3 / 4, "a somewhat slower card loses a little: " + entry);
                Check(chip == (int)Math.Round(cpu * HostFitness.GpuFloor) && worse == chip, "an integrated chip is floored: " + chip + " / " + worse);
                // The comparison the user asked for (CPU and GPU together): a faster processor with an integrated chip against a
                // somewhat slower processor with a real card.
                Check(HostFitness.Base(30, 8, 16000, false, 80) < HostFitness.Base(40, 8, 16000, false, 4), "a fast CPU does not make up for an integrated chip");
                Check(HostFitness.Base(30, 8, 16000, false, 4) > HostFitness.Base(40, 8, 16000, false, 4), "with good cards on both, the processor decides");
                Check(HostFitness.GpuFactor(double.NaN) == 1 && HostFitness.Base(0, 8, 16000, false, 4) == 0, "nothing measured"); });
            Test("The host of a running match (R536): a PC a tenth better takes it, one of the same class does not; pings and unknown machines as at the start", () => {
                var host = new HostElection.Candidate { Id = 5, Fitness = 400, BaseFitness = 1000, PingMs = 20 };
                Func<int, int, int, HostElection.Candidate> other = (machine, ping, now) => new HostElection.Candidate { Id = 3, Fitness = now > 0 ? now : machine, BaseFitness = machine, PingMs = ping };
                Func<HostElection.Candidate, ulong> elect = c => HostElection.StartHost(5, new[] { host, c }, HostElection.LiveMargin);
                Check(HostElection.LiveMargin > HostElection.StartMargin && HostElection.LiveMargin < HostElection.StruggleMargin, "between the start's margin and a struggling host's");
                Check(elect(other(1099, 20, 0)) == 5 && elect(other(1000, 20, 0)) == 5, "a PC of the same class (a lower Steam ID, too) leaves the host where it is");
                Check(elect(other(1101, 20, 0)) == 3 && elect(other(1500, 20, 0)) == 3, "a tenth better takes the match");
                // Machines alone, as at the start: the newcomer's frame rate has just dipped, the host's is low because it hosts.
                Check(elect(other(1500, 20, 300)) == 3, "frame rates of the moment decided it");
                Check(elect(other(1500, 240, 0)) == 5, "a far-away PC is not the better host");
                Check(elect(other(0, 20, 0)) == 5, "an unmeasured machine took the match"); });
            Test("Leave penalty (R533, rule 3): two desertions free, then 10 minutes, 30 minutes, an hour; each counts for 24 hours", () => {
                Check(LeavePenalty.BanSeconds(0) == 0 && LeavePenalty.BanSeconds(1) == 0 && LeavePenalty.BanSeconds(2) == 0, "two chances");
                Check(LeavePenalty.BanSeconds(3) == 600 && LeavePenalty.BanSeconds(4) == 1800 && LeavePenalty.BanSeconds(5) == 3600 && LeavePenalty.BanSeconds(9) == 3600,
                      "10 minutes, 30 minutes, an hour and an hour from then on");
                double t0 = 1.7e9;
                var leaves = new List<double>();
                Check(LeavePenalty.Count(leaves, t0) == 0 && LeavePenalty.SecondsLeft(leaves, t0) == 0 && LeavePenalty.SecondsLeft(null, t0) == 0, "nothing yet");
                leaves = LeavePenalty.Add(leaves, t0);
                leaves = LeavePenalty.Add(leaves, t0 + 1000);
                Check(LeavePenalty.Count(leaves, t0 + 1001) == 2 && LeavePenalty.SecondsLeft(leaves, t0 + 1001) == 0, "the first two cost nothing");
                leaves = LeavePenalty.Add(leaves, t0 + 2000);
                Check(LeavePenalty.Count(leaves, t0 + 2000) == 3 && LeavePenalty.SecondsLeft(leaves, t0 + 2000) == 600, "the third: 10 minutes from its moment");
                Check(LeavePenalty.SecondsLeft(leaves, t0 + 2599) == 1 && LeavePenalty.SecondsLeft(leaves, t0 + 2600) == 0, "and over after them");
                leaves = LeavePenalty.Add(leaves, t0 + 3000);
                Check(LeavePenalty.SecondsLeft(leaves, t0 + 3000) == 1800, "the fourth: 30 minutes");
                leaves = LeavePenalty.Add(leaves, t0 + 5000);
                Check(LeavePenalty.SecondsLeft(leaves, t0 + 5000) == 3600, "the fifth: an hour");
                leaves = LeavePenalty.Add(leaves, t0 + 9000);
                Check(LeavePenalty.SecondsLeft(leaves, t0 + 9000) == 3600 && LeavePenalty.Count(leaves, t0 + 9000) == 6, "the sixth: an hour again");
                // 24 hours after each one it stops counting: the first two have dropped out, so the next is a fifth, not a seventh.
                double day = LeavePenalty.Window;
                Check(LeavePenalty.Count(leaves, t0 + day - 1) == 6 && LeavePenalty.Count(leaves, t0 + day) == 5 && LeavePenalty.Count(leaves, t0 + day + 1000) == 4, "each drops out after 24 hours");
                Check(LeavePenalty.Count(leaves, t0 + 9000 + day) == 0 && LeavePenalty.SecondsLeft(leaves, t0 + 9000 + day) == 0, "a day after the last: clean");
                var next = LeavePenalty.Add(leaves, t0 + 9000 + day);
                Check(next.Count == 1 && LeavePenalty.SecondsLeft(next, t0 + 9000 + day) == 0, "then the count starts over, and what no longer counts is dropped");
                // A desertion a day after two others is a second (the first of those has dropped out), not a third.
                var spread = LeavePenalty.Add(LeavePenalty.Add(LeavePenalty.Add(null, t0), t0 + 10), t0 + day + 5);
                Check(spread.Count == 2 && LeavePenalty.SecondsLeft(spread, t0 + day + 5) == 0, "the day window slides: " + spread.Count);
                // Kept between runs as text; nonsense is dropped, and so is the far future (a clock set forward).
                string text = LeavePenalty.Encode(leaves);
                var back = LeavePenalty.Decode(text);
                Check(back.SequenceEqual(leaves) && LeavePenalty.Encode(back) == text, "round trip: " + text);
                Check(LeavePenalty.Decode(null).Count == 0 && LeavePenalty.Decode("").Count == 0 && LeavePenalty.Decode("x,-5,1e9,,7").SequenceEqual(new double[] { 7 }), "unreadable entries");
                Check(LeavePenalty.Decode(string.Join(",", Enumerable.Range(1, 40))).Count == LeavePenalty.Kept, "no more than it keeps");
                Check(LeavePenalty.Count(new double[] { t0 + 500 }, t0) == 0 && LeavePenalty.Add(new double[] { t0 + 500 }, t0).Count == 1, "an entry dated after now does not count and is dropped");
                var many = new List<double>();
                for (int i = 0; i < 30; i++) many = LeavePenalty.Add(many, t0 + i);
                Check(many.Count == LeavePenalty.Kept && many[many.Count - 1] == t0 + 29, "the newest are kept");
                Check(LeavePenalty.Clock(600) == "10:00" && LeavePenalty.Clock(59.2) == "1:00" && LeavePenalty.Clock(3600) == "1:00:00" && LeavePenalty.Clock(-3) == "0:00" && LeavePenalty.Clock(3725) == "1:02:05",
                      "the clock: " + LeavePenalty.Clock(600) + " " + LeavePenalty.Clock(59.2) + " " + LeavePenalty.Clock(3725)); });
            Test("R616 turret volleys: a turret every three seconds, never the one before, every turret as often, the same on every PC", () => {
                Check(TurretVolleys.Period == 3.0 && TurretVolleys.Volley(0) == 0 && TurretVolleys.Volley(2.999) == 0 && TurretVolleys.Volley(3) == 1
                      && TurretVolleys.Volley(-.001) == -1 && TurretVolleys.Start(7) == 21.0, "a volley every three seconds of the clock");
                foreach (int turrets in new[] { 3, 4, 5, 8 })
                    foreach (int seed in new[] { 1, 2, 77, -5, 20261112, int.MaxValue })
                    {
                        var count = new int[turrets];
                        // Across zero and far along the clock (a shared clock may count from anywhere), through many blocks.
                        foreach (long from in new long[] { -400, 0, 1L << 33 })
                        {
                            int before = TurretVolleys.Turret(seed, from - 1, turrets);
                            for (long n = from; n < from + 4000; n++)
                            {
                                int t = TurretVolleys.Turret(seed, n, turrets);
                                Check(t >= 0 && t < turrets, "one of the turrets: " + t);
                                Check(t != before, "the same turret twice running at volley " + n + ", seed " + seed + ", " + turrets + " turrets");
                                Check(t == TurretVolleys.Turret(seed, n, turrets), "the same answer whenever it is asked");
                                count[t]++;
                                before = t;
                            }
                        }
                        // 12,000 volleys: each turret near its share (a quarter each of four: 2,700 .. 3,300).
                        foreach (int c in count)
                            Check(c > 12000 / turrets * .9 && c < 12000 / turrets * 1.1, "an even share, seed " + seed + ": " + string.Join(" ", count));
                    }
                // What comes after a turret is any of the other three, not a fixed order.
                var follows = new int[4, 4];
                for (long n = 1; n < 6000; n++) follows[TurretVolleys.Turret(9, n - 1, 4), TurretVolleys.Turret(9, n, 4)]++;
                for (int a = 0; a < 4; a++)
                    for (int b = 0; b < 4; b++)
                        Check(a == b ? follows[a, b] == 0 : follows[a, b] > 350 && follows[a, b] < 650, "after turret " + a + " comes " + b + ": " + follows[a, b]);
                // Another round, another order.
                int same = 0;
                for (long n = 0; n < 400; n++) if (TurretVolleys.Turret(11, n, 4) == TurretVolleys.Turret(12, n, 4)) same++;
                Check(same > 60 && same < 140, "two seeds agree only by chance: " + same + " of 400");
                // Two turrets take turns, one fires every time.
                for (long n = -20; n < 20; n++)
                    Check(TurretVolleys.Turret(3, n, 2) != TurretVolleys.Turret(3, n + 1, 2) && TurretVolleys.Turret(3, n, 1) == 0, "two take turns"); });
            Test("R624 sword pick on the entrance screen: the earlier pick keeps a piece, the close draws the rest, the same on every PC", () => {
                // The member data's text, both ways; a broken or unpickable one is no pick.
                PieceKind kind; double at;
                Check(SwordPickDraft.Decode(SwordPickDraft.Encode(PieceKind.Queen, 1794508000.125), out kind, out at) && kind == PieceKind.Queen && at == 1794508000.125, "the pick's text round trip");
                foreach (var bad in new[] { "", "x", "1,", ",5", "0,10", "9,10", "2,abc", "2,NaN" })
                    Check(!SwordPickDraft.Decode(bad, out kind, out at) && kind == PieceKind.Pawn, "not a pick: '" + bad + "'");
                const double begun = 1000;
                // Two teammates on the queen: the earlier keeps it; the same moment: the lower id. The other side may take it too.
                var picks = new List<SwordPickDraft.Entry> { Pick(1, 0, PieceKind.Queen, begun + 5.2), Pick(2, 0, PieceKind.Queen, begun + 5.1),
                                                             Pick(3, 1, PieceKind.Queen, begun + 9), Pick(4, 0, PieceKind.Rook, begun + 6), Pick(5, 0, PieceKind.Pawn, 0) };
                var held = SwordPickDraft.Resolve(picks, begun);
                Check(held[2] == PieceKind.Queen && !held.ContainsKey(1) && held[3] == PieceKind.Queen && held[4] == PieceKind.Rook && !held.ContainsKey(5), "earlier keeps it");
                picks[0] = Pick(1, 0, PieceKind.Queen, begun + 5.1);
                held = SwordPickDraft.Resolve(picks, begun);
                Check(held[1] == PieceKind.Queen && !held.ContainsKey(2), "a tie goes to the lower id");
                // A pick left over from an earlier round in the same room does not count, nor a bot's.
                held = SwordPickDraft.Resolve(new List<SwordPickDraft.Entry> { Pick(1, 0, PieceKind.King, begun - 30), Pick(2, 0, PieceKind.King, begun + 1, true) }, begun);
                Check(held.Count == 0, "stale or bot picks count for nothing");
                // Every human holds a piece: the picks close early; the stages from the host's start.
                var all = new List<SwordPickDraft.Entry> { Pick(1, 0, PieceKind.King, begun + 4), Pick(2, 1, PieceKind.Rook, begun + 4), Pick(9, 0, PieceKind.Pawn, 0, true) };
                Check(SwordPickDraft.AllPicked(all, begun) && !SwordPickDraft.AllPicked(picks, begun), "all picked counts humans only");
                Check(SwordPickDraft.StageAt(0, false) == SwordPickDraft.Stage.Found && SwordPickDraft.StageAt(4.99, true) == SwordPickDraft.Stage.Found
                      && SwordPickDraft.StageAt(5, false) == SwordPickDraft.Stage.Picking && SwordPickDraft.StageAt(19.9, false) == SwordPickDraft.Stage.Picking
                      && SwordPickDraft.StageAt(5, true) == SwordPickDraft.Stage.Closed && SwordPickDraft.StageAt(20, false) == SwordPickDraft.Stage.Closed, "found 5 s, cards 15 s");
                Check(SwordPickDraft.Left(0) == 15 && SwordPickDraft.Left(12) == 8 && SwordPickDraft.Left(30) == 0, "seconds left on the cards");
                // The close: picks kept, everyone else one of their side's pieces left, the same draw for the same room.
                foreach (int seed in new[] { SwordPickDraft.Seed(109775240000000001UL), SwordPickDraft.Seed(2), 7 })
                    for (int size = 1; size <= 6; size++)
                    {
                        var room = new List<SwordPickDraft.Entry>();
                        for (int i = 0; i < size; i++)
                        {
                            room.Add(Pick((ulong)(10 + i), 0, i == 0 ? PieceKind.Knight : PieceKind.Pawn, i == 0 ? begun + 3 : 0));
                            room.Add(Pick((ulong)(20 + i), 1, PieceKind.Pawn, 0, i % 2 == 1));
                        }
                        var final = SwordPickDraft.Final(room, begun, seed);
                        Check(final.Count == room.Count && final[10] == PieceKind.Knight, "everyone gets a piece, the pick kept");
                        foreach (int team in new[] { 0, 1 })
                        {
                            var pieces = room.Where(e => e.Team == team).Select(e => final[e.Id]).ToList();
                            Check(pieces.All(p => SwordMatchRules.Pickable.Contains(p)), "a pickable piece");
                            Check(pieces.Distinct().Count() == Math.Min(5, pieces.Count), "no piece twice on a side while five go round: " + string.Join(",", pieces));
                        }
                        var again = SwordPickDraft.Final(room.AsEnumerable().Reverse().ToList(), begun, seed);
                        Check(room.All(e => again[e.Id] == final[e.Id]), "the same draw on every PC, whatever order it reads the room in");
                    }
                // The scene's rules take the draw: a sixth player may double a piece up.
                var rules = new SwordMatchRules(5);
                rules.Add(1, 0); rules.Add(2, 0);
                Check(rules.Assign(1, PieceKind.Queen) && rules.Assign(2, PieceKind.Queen) && !rules.Assign(3, PieceKind.King) && !rules.Assign(1, PieceKind.Pawn), "assign within the pick");
                Check(rules.AllPicked && rules.Advance(.02) && rules.Phase == SwordPhase.Countdown, "all assigned: straight to 3, 2, 1");
                Check(!rules.Assign(1, PieceKind.King), "no assigning after the pick");
                Check(SwordSkillNames.Skill(PieceKind.Queen) == "꼬치 베기" && SwordSkillNames.Edge(PieceKind.Knight) == "벼랑 끝 역전", "skill names"); });
            Console.WriteLine($"{passed} core tests passed."); return 0;
        }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
