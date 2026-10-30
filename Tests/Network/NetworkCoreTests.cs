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
    static bool Reserve(TeamReservations r, int first, int count, double now = 0) => r.Reserve((ulong)first, (ulong)first + 100, "t" + first, Ids(first, count), now, out _);
    // An input packet as far as the skill key goes (no tuples: mcs runs these tests on Linux).
    struct PressPacket { public uint Seq; public byte Count; }
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
                Check(GameModes.SwordFight.Playable && GameModes.SwordFight.Scene == "SwordFight" && !GameModes.QueenOfTheHill.Playable, "scene routing"); });
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
            Test("Disconnect after admission removes the whole waiting party", () => {
                var r=new TeamReservations(); Reserve(r,1,3); r.Reconcile(new HashSet<ulong>(Ids(1,3)),1);
                r.Reconcile(new HashSet<ulong> {1,2},2); Check(r.Count == 0,"disconnected slot retained"); });
            Test("Reject spoofed leader, zero IDs, repeated IDs and overlaps", () => {
                var r=new TeamReservations(); Check(!r.Reserve(9,99,"x",new ulong[]{1,2},0,out _),"spoof");
                Check(!r.Reserve(1,99,"x",new ulong[]{1,1},0,out _),"duplicate");
                Check(!r.Reserve(1,99,"x",new ulong[]{1,0},0,out _),"zero");
                Reserve(r,1,3); Check(!Reserve(r,3,2),"overlap"); });
            Test("Existing party cannot change ticket or roster during admission", () => {
                var r=new TeamReservations(); Reserve(r,1,3);
                Check(!r.Reserve(1,101,"new",Ids(1,3),1,out _),"ticket changed"); Check(!Reserve(r,1,4),"roster changed"); });
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
                var pawns=Enumerable.Range(1,12).Select(i=>PawnMotor.Spawn((ulong)i,(i-1)/6,(i-1)%6));
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
            Test("Response time measures send to acknowledgement and ignores stale acks", () => {
                var t=new ResponseTimer(); Check(t.Milliseconds<0,"empty");
                for(uint s=1;s<=5;s++) t.Sent(s,s*0.033);
                t.Acknowledged(3,0.099+0.1); Check(Math.Abs(t.Milliseconds-100)<0.5,"first sample "+t.Milliseconds);
                t.Acknowledged(2,1.0); Check(Math.Abs(t.Milliseconds-100)<0.5,"stale ack changed estimate");
                t.Acknowledged(5,0.165+0.180); Check(t.Milliseconds>100&&t.Milliseconds<180,"smoothing "+t.Milliseconds); });
            Test("Pawn Rush course state (CFP4) round-trips stations with their blocks and refuses damaged packets", () => {
                var a = new PawnRushNetState { Tick = 77, HostTimeMs = 123456, First = 3, Second = 0 };
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
                Check(copy.Write(42UL).SequenceEqual(bytes), "copy writes the same bytes");
                Check(!PawnRushNetState.Read(bytes, 43UL, b), "another match");
                var cut = new byte[bytes.Length - 1]; Array.Copy(bytes, cut, cut.Length);
                Check(!PawnRushNetState.Read(cut, 42UL, b), "short packet");
                var longer = new byte[bytes.Length + 1]; Array.Copy(bytes, longer, bytes.Length);
                Check(!PawnRushNetState.Read(longer, 42UL, b), "long packet");
                bytes[0] ^= 1; Check(!PawnRushNetState.Read(bytes, 42UL, b), "wrong magic"); });
            Test("Pawn Rush course state (CFP4) carries the pads and their choosers, the winner, the clock and the crown finishers", () => {
                var a = new PawnRushNetState { Tick = 9, First = 1, Second = 4, PadFirst = (byte)PieceKind.Knight, PadSecond = (byte)PieceKind.Queen,
                                               PadsUsed = 0x5, Winner = 1, Clock = 187.25f, FinisherCount = 3 };
                a.PadChooser[2] = SkillWire.Ref(0, 3);
                a.FinisherSeat[0] = SkillWire.Ref(1, 0); a.FinisherTime[0] = 170.04f;
                a.FinisherSeat[1] = SkillWire.Ref(1, 2); a.FinisherTime[1] = 181.5f;
                a.FinisherSeat[2] = SkillWire.Ref(0, 5); a.FinisherTime[2] = 186.96f;
                byte[] bytes = a.Write(7UL);
                Check(bytes.Length == PawnRushNetState.HeaderBytes + 3 * PawnRushNetState.FinisherBytes + 4 * PawnRushNetState.StationFixedBytes, "size " + bytes.Length);
                var b = new PawnRushNetState();
                Check(PawnRushNetState.Read(bytes, 7UL, b), "read");
                Check(b.PadFirst == (byte)PieceKind.Knight && b.PadSecond == (byte)PieceKind.Queen && b.PadsUsed == 0x5, "pads");
                Check(b.PadChooser[2] == SkillWire.Ref(0, 3) && b.PadChooser[0] == PawnRushNetState.NoChooser && b.PadChooser[3] == PawnRushNetState.NoChooser, "choosers");
                Check(b.Winner == 1 && Math.Abs(b.Clock - 187.25f) < 1e-4f, "winner and clock");
                Check(b.FinisherCount == 3 && b.FinisherSeat[1] == SkillWire.Ref(1, 2) && Math.Abs(b.FinisherTime[0] - 170f) < 0.051f
                      && Math.Abs(b.FinisherTime[2] - 187f) < 0.051f, "finishers to a tenth");
                var none = new PawnRushNetState().Write(7UL);
                Check(PawnRushNetState.Read(none, 7UL, b) && b.Winner == -1 && b.FinisherCount == 0, "a fresh state: nobody yet");
                bytes[PawnRushNetState.HeaderBytes - 1] = 13; Check(!PawnRushNetState.Read(bytes, 7UL, b), "more finishers than seats"); });
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
            Test("G king escort: a road of fifteen joined squares, 32.9 m, six turntables, the speed table", () => {
                Check(KingEscortPath.Connected() && KingEscortPath.Cells.Length == 15, "joined");
                Check(Math.Abs(KingEscortPath.Length - 32.925f) < .01f, "length " + KingEscortPath.Length);
                int turns = 0; for (int i = 0; i < KingEscortPath.Cells.Length; i++) if (KingEscortPath.Turns(i)) turns++;
                Check(turns == 6, "turntables " + turns);
                var p = KingEscortPath.Position(0f, out float dv, out float du);
                Check(Math.Abs(p.V - PawnRushBoard.Center(3, 1).V) < 1e-4f && dv == 0f && du == 1f, "starts on d1 going north");
                var g = KingEscortPath.Position(KingEscortPath.Length, out dv, out du);
                Check(Math.Abs(g.U - KingEscortPath.GateU) < 1e-3f, "ends at the gate");
                Check(Math.Abs(KingEscortPath.Speed(1.5f, false) - .735f) < 1e-4f && KingEscortPath.Speed(.5f, true) == .11f && KingEscortPath.Speed(9f, false) == 1.1f, "speeds");
                Check(KingEscortPath.IsSand(KingEscortPath.At(5)) && !KingEscortPath.IsSand(KingEscortPath.At(0)), "sand");
                Check(KingEscortPath.RookStop(0) < KingEscortPath.At(8) && KingEscortPath.RookStop(1) < KingEscortPath.At(12), "rooks stop the sled short"); });
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
                Check(GameModes.Default.Playable && GameModes.Find("kingrush") == GameModes.KingRush && GameModes.KingRush.Scene == "PawnRush_Course01", "default (the 폰 러쉬 match is Pawn Rush course 01 since 10-08)");
                Check(GameModes.Find("nope") == null && GameModes.Resolve("") == GameModes.Default && GameModes.IndexOf("swordfight") == 2, "lookup");
                Check(GameModes.All.All(m => m.Playable == (m.Scene.Length > 0)), "playable means a scene"); });
            Test("Queen of the Hill course: seven floors, one per rank, the summit is rank 8", () => {
                Check(QueenHillCourse.Floors == 7 && QueenHillCourse.Sections == 7 && QueenHillCourse.All.Length == 7, "seven floors");
                Check(Math.Abs(QueenHillCourse.RankHeight(1) - 16f) < 1e-3f && Math.Abs(QueenHillCourse.TopHeight - 156f) < 1e-3f, "heights");
                Check(Enumerable.Range(1, 7).All(r => Math.Abs(QueenHillCourse.RankHeight(r + 1) - QueenHillCourse.RankHeight(r) - QueenHillCourse.FloorHeight) < 1e-3f), "even floors");
                Check(QueenHillCourse.All.Select((f, i) => f.Number == i + 1).All(ok => ok), "numbering");
                Check(QueenHillCourse.Floor(0) == null && QueenHillCourse.Floor(8) == null && QueenHillCourse.Floor(4).Shared && QueenHillCourse.Floor(7).Shared, "lookup");
                Check(QueenHillCourse.RankAt(16.5f) == 1 && QueenHillCourse.RankAt(36.3f) == 2 && QueenHillCourse.RankAt(200f) == 8 && QueenHillCourse.RankAt(0f) == 1, "rank at a height"); });
            Test("Queen of the Hill course: the hook reaches the cliff easily and the two-squares roof only at full strength", () => {
                Check(QueenHillCourse.HookDistanceToRim < QueenHillCourse.HookFullReach * 0.8f, "the rim is out of an easy throw");
                Check(QueenHillCourse.HookDistanceToTwoSquares < QueenHillCourse.HookFullReach, "the two-squares roof is out of reach");
                Check(QueenHillCourse.HookDistanceToTwoSquares > QueenHillCourse.HookFullReach * 0.8f, "the two-squares roof is too easy");
                Check(QueenHillCourse.LiftSeconds > 5f && QueenHillCourse.LiftSeconds < 8f, "the light lift takes about six seconds"); });
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
                Check(SkillWire.StateBytes==29&&SkillWire.EventBytes==28&&SkillWire.WireBytes==18,"sizes as documented"); });
            Test("Skills packet round trip keeps every field within its step and rejects bad packets", () => {
                var p=new SkillWirePacket{HostTimeMs=123456,FirstSeq=65530};
                p.States.Add(new SkillWireState{Pawn=SkillWire.Ref(1,3),Stage=2,Flags=0xA5,Flags2=3,StageTime=1.234f,Cooldown=7.25f,CooldownTotal=12f,Dx=0.6f,Dy=0.8f,Px=-12.345f,Py=1.5f,Pz=33.3f,Qx=4,Qy=-0.5f,Qz=79,Target=SkillWire.Ref(0,5),Other=SkillWire.None,A=2.5f,B=25.5f,C=200,D=7});
                p.Wires.Add(new SkillWireObject{Id=9,Owner=SkillWire.Ref(0,1),X=3,Y=0.02f,Z=-4,Yaw=271,Age=2.5f,Flags=1,Trips=1,Along0=0.33f,Along1=0.5f,Out0=0.62f,Out1=-0.31f});
                p.Barricades.Add(new SkillWireBarricade{Index=0,Standing=false,RegrowLeft=3.4f});
                p.Events.Add(new SkillWireEvent{Seq=65531,TimeMs=123000,Mode=SkillWire.ModeQueenHill,Kind=9,By=SkillWire.Ref(1,3),Target=SkillWire.None,X=1,Y=2,Z=3,Dx=0,Dy=0,Dz=-1,Ux=-5,Uy=0,Uz=5,Count=3,Size=6.5f,Source=9});
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
            Test("Queen of the Hill: the first team to ring a bell opens its section, 20 s for that team", () => {
                var q=new QueenHillRules(8);
                Check(q.TryOpen(3,QueenHillRules.Black,100)&&!q.TryOpen(3,QueenHillRules.White,101),"first ring wins");
                Check(q.IsOpen(3)&&q.Pioneer(3)==QueenHillRules.Black&&!q.IsOpen(2),"state");
                Check(q.CanUse(3,QueenHillRules.Black,105)&&!q.CanUse(3,QueenHillRules.White,119.9)&&q.CanUse(3,QueenHillRules.White,120),"exclusive window");
                Check(!q.CanUse(2,QueenHillRules.Black,500),"closed section");
                Check(!q.TryOpen(0,0,1)&&!q.TryOpen(9,0,1)&&!q.TryOpen(1,-1,1)&&!q.TryOpen(1,2,1)&&!q.TryOpen(1,0,double.NaN),"bad input"); });
            Test("Queen of the Hill: respawn at the higher of own checkpoint and one below the team's best", () => {
                var q=new QueenHillRules(8);
                Check(q.RespawnSection(1,QueenHillRules.White)==0,"nothing yet = the bridge");
                q.Reach(1,2); q.Reach(1,1); Check(q.PersonalBest(1)==2&&q.RespawnSection(1,QueenHillRules.White)==2,"own best, never lowered");
                q.TryOpen(1,QueenHillRules.White,1); q.TryOpen(5,QueenHillRules.White,2); q.TryOpen(6,QueenHillRules.Black,3);
                Check(q.TeamBest(QueenHillRules.White)==5&&q.TeamBest(QueenHillRules.Black)==6,"team best");
                Check(q.RespawnSection(2,QueenHillRules.White)==4&&q.RespawnSection(1,QueenHillRules.White)==4,"one below the leader");
                q.Reach(3,7); Check(q.RespawnSection(3,QueenHillRules.White)==7,"own checkpoint higher");
                q.Reach(4,0); q.Reach(4,99); Check(q.PersonalBest(4)==0,"bad sections ignored");
                q.Reset(); Check(!q.IsOpen(5)&&q.RespawnSection(3,QueenHillRules.White)==0,"reset"); });
            Test("Queen of the Hill: openings travel as text and only ever open", () => {
                var host=new QueenHillRules(8); host.TryOpen(2,QueenHillRules.White,12.5); host.TryOpen(4,QueenHillRules.Black,40.25);
                var client=new QueenHillRules(8); Check(client.Apply(host.Encode())&&!client.Apply(host.Encode()),"applied once");
                Check(client.Pioneer(2)==QueenHillRules.White&&Math.Abs(client.OpenedAt(4)-40.25)<1e-6,"same state");
                Check(client.Apply("")==false&&client.IsOpen(2),"empty keeps state");
                foreach(string bad in new[]{"2:0","x:0:1","2:5:1","9:0:1","2:0:1;junk",new string('1',600)})
                    Check(!new QueenHillRules(8).Apply(bad),"rejected "+bad);
                var partial=new QueenHillRules(8); Check(!partial.Apply("1:0:10;3:9:10")&&!partial.IsOpen(1),"all or nothing"); });
            Test("Queen of the Hill pieces: the starting values of DESIGN 6.2", () => {
                var pawn=ChessPieces.Stats(PieceKind.Pawn); Check(pawn.Weight==1f&&pawn.Move==1f&&pawn.Climb==1f&&pawn.Sprint&&!pawn.StatusImmune,"pawn");
                var rook=ChessPieces.Stats(PieceKind.Rook); Check(rook.Weight==1.5f&&rook.Climb==0.7f&&rook.Sprint,"rook");
                var king=ChessPieces.Stats(PieceKind.King); Check(king.Weight==3f&&king.Move==0.8f&&king.Sprint&&king.StatusImmune,"king");
                Check(ChessPieces.Stats(PieceKind.Knight).Weight==0.8f&&ChessPieces.Stats(PieceKind.Queen).Weight==2f,"knight, queen");
                Check(ChessPieces.Stats((PieceKind)9).Weight==1f&&!ChessPieces.IsValid(6)&&ChessPieces.IsValid(5)&&!ChessPieces.IsValid(-1),"out of range");
                Check(ChessPieces.Name(PieceKind.Knight)=="나이트"&&ChessPieces.Name(PieceKind.Pawn)=="폰","names"); });
            Test("Queen of the Hill pieces: a pawn promotes to a rook, bishop, knight or the team's one king", () => {
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
            Test("Backfill: an empty seat goes to the team that lost a player, and only up to the size that team started with", () => {
                Func<int, int, ulong, Backfill.Seat> seat = (t, slot, id) => new Backfill.Seat { Id = id, Team = t, Slot = slot };
                var sixes = new[] { 6, 6 };
                var taken = new List<Backfill.Seat>();
                for (int s = 0; s < 6; s++) taken.Add(seat(0, s, (ulong)(10 + s)));
                foreach (int s in new[] { 0, 1, 2, 4, 5 }) taken.Add(seat(1, s, (ulong)(20 + s)));
                Check(Backfill.Free(sixes, taken, 0) == 0 && Backfill.Free(sixes, taken, 1) == 1, "free seats");
                Check(Backfill.Place(sixes, taken, 1, out int team, out int[] slots) && team == 1 && slots.SequenceEqual(new[] { 3 }), "the leaver's seat");
                Check(!Backfill.Place(sixes, taken, 2, out _, out _), "a party of two split or squeezed in");
                var fives = new[] { 5, 5 };
                var five = taken.Where(s => s.Slot < 5).ToList();
                Check(Backfill.Place(fives, five, 1, out team, out slots) && team == 1 && slots.SequenceEqual(new[] { 3 }), "a 5 v 5 match refills its own seat");
                five.Add(seat(1, 3, 99));
                Check(Backfill.Free(fives, five, 0) == 0 && Backfill.Free(fives, five, 1) == 0 && !Backfill.Place(fives, five, 1, out _, out _), "a sixth player got into a 5 v 5 team");
                var few = new List<Backfill.Seat> { seat(0, 1, 1), seat(1, 0, 2), seat(1, 2, 3) };
                Check(Backfill.Place(sixes, few, 2, out team, out slots) && team == 0 && slots.SequenceEqual(new[] { 0, 2 }), "more empty seats first, lowest free slots");
                Check(Backfill.Place(new[] { 1, 1 }, new List<Backfill.Seat>(), 1, out team, out _) && team == 0, "a tie goes to white");
                Check(Backfill.Free(null, few, 0) == 0 && !Backfill.Place(null, few, 1, out _, out _), "no known size, no seat");
                Check(TeamReservations.ValidRequest(1, 101, "t", new ulong[] { 1, 2 }) && !TeamReservations.ValidRequest(1, 101, "t", new ulong[] { 2 }) &&
                      !TeamReservations.ValidRequest(1, 101, "t", new ulong[] { 1, 1 }) && !TeamReservations.ValidRequest(1, 101, "t", null), "request shape"); });
            Test("Backfill: seats are offered for four minutes after the start, and the room keeps sizes and held seats as text", () => {
                Check(Backfill.OpenSeconds == 240 && Backfill.Open(0, 5) && Backfill.Open(1000, 1000 + Backfill.OpenSeconds - .5) && !Backfill.Open(1000, 1000 + Backfill.OpenSeconds), "window");
                Check(Backfill.EncodeCapacity(6, 5) == "6,5" && Backfill.DecodeCapacity("6,5").SequenceEqual(new[] { 6, 5 }), "sizes");
                Check(Backfill.DecodeCapacity("") == null && Backfill.DecodeCapacity("7,6") == null && Backfill.DecodeCapacity("6") == null &&
                      Backfill.DecodeCapacity("-1,6") == null && Backfill.DecodeCapacity("a,b") == null, "bad sizes");
                var held = new[] { new Backfill.Seat { Id = 76561198000000001, Team = 1, Slot = 3, Until = 1790743925 }, new Backfill.Seat { Id = 5, Team = 0, Slot = 0, Until = 1790743930 } };
                var back = Backfill.DecodeHeld(Backfill.EncodeHeld(held));
                Check(back.Count == 2 && back[0].Id == 76561198000000001 && back[0].Team == 1 && back[0].Slot == 3 && back[0].Until == 1790743925 && back[1].Id == 5, "held round trip");
                Check(Backfill.EncodeHeld(new Backfill.Seat[0]) == "" && Backfill.DecodeHeld(null).Count == 0, "nothing held");
                var messy = Backfill.DecodeHeld("0:0:0:1;7:2:0:1;8:0:6:1;9:0:1:x;10:1:1:5;10:0:2:5;11:1:1:5;12:0:0:5:9");
                Check(messy.Count == 1 && messy[0].Id == 10 && messy[0].Team == 1, "bad held entries kept"); });
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
            Test("Sword Fight prototype: pick, countdown, every fall a point, ten wins, and the respawn is always a pawn", () => {
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
                    Check(!r.TryRespawn(3), "not before 3 s"); r.Advance(3.01); Check(r.TryRespawn(3) && r.Get(3).Piece == PieceKind.Pawn && r.Get(3).Alive, "back as a pawn, flag cleared " + i);
                }
                Check(r.White == 9 && r.Phase == SwordPhase.Playing, "nine");
                r.RingOut(3); Check(r.Phase == SwordPhase.Result && r.Winner == 0 && r.Outs[0] == 10, "ten wins"); });
            Test("Sword Fight prototype: the last enemy within 3 s takes the credit, a pawn steals the piece, an ally or a stale hit does not", () => {
                var r = new SwordMatchRules(1); r.Add(1, 0); r.Add(2, 1); r.Add(3, 1); r.Pick(1, PieceKind.Rook); r.Pick(2, PieceKind.Queen); r.Pick(3, PieceKind.King);
                r.Advance(1); r.Advance(15); r.Advance(3.01);
                r.Get(1).Piece = PieceKind.Pawn;
                r.Hit(1, 2, SwordCause.PawnStep, 0); r.Advance(2.5);
                var o = r.RingOut(2); Check(o.Credit == 1 && o.Stole && o.NewPiece == PieceKind.Queen && r.Get(1).Piece == PieceKind.Queen && r.Steals[0] == 1, "steal");
                Check(o.Feed.Stolen == (byte)PieceKind.Queen && o.Feed.Cause == SwordCause.PawnStep, "feed");
                r.Hit(1, 3, SwordCause.Sword, 5); o = r.RingOut(3); Check(o.Credit == 1 && !o.Stole && r.Get(1).Piece == PieceKind.Queen, "not a pawn: no steal");
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
            Console.WriteLine($"{passed} core tests passed."); return 0;
        }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
