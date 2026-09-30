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
    static bool Reserve(TeamReservations r, int first, int count, double now = 0) => r.Reserve((ulong)first, (ulong)first + 100, "t" + first, Ids(first, count), now, out _);
    public static int Main()
    {
        try
        {
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
            Test("Game mode catalog: unique lobby keys, a playable default, safe lookups", () => {
                Check(GameModes.All.Select(m => m.Key).Distinct().Count() == GameModes.All.Length, "duplicate key");
                Check(GameModes.All.All(m => m.Key.Length > 0 && m.Key.Length <= 16 && m.Key.All(ch => ch >= 'a' && ch <= 'z')), "lobby keys are short lowercase words");
                Check(GameModes.Default.Playable && GameModes.Find("kingrush") == GameModes.KingRush && GameModes.KingRush.Scene == "KingRush", "default");
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
                var king=ChessPieces.Stats(PieceKind.King); Check(king.Weight==3f&&king.Move==0.8f&&!king.Sprint&&king.StatusImmune,"king");
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
                HostElection.Candidate C(ulong id, int fit, int ping = -1) => new HostElection.Candidate { Id = id, Fitness = fit, PingMs = ping };
                Check(HostElection.Rank(new[] { C(1, 500), C(2, 900), C(3, 700) }).SequenceEqual(new ulong[] { 2, 3, 1 }), "fitness order");
                Check(HostElection.Rank(new[] { C(5, 800), C(3, 800) }).SequenceEqual(new ulong[] { 3, 5 }), "tie to the lower id");
                Check(HostElection.Rank(new[] { C(1, 900, 200), C(2, 500, 20) }).SequenceEqual(new ulong[] { 2, 1 }), "far host discounted");
                Check(HostElection.Rank(new[] { C(1, 900, 60), C(2, 800, 10) }).SequenceEqual(new ulong[] { 1, 2 }), "within the ping budget only fitness counts");
                Check(HostElection.Rank(new[] { C(1, 0), C(2, 400) }).SequenceEqual(new ulong[] { 2, 1 }), "unmeasured below measured");
                Check(!HostElection.Rank(new[] { C(BotIdentity.Id(7, 0), 99999), C(0, 5000), C(4, 1) }).Any(id => id != 4), "bots and zero never rank");
                Check(HostElection.Worth(C(1, 500), C(2, 575), HostElection.StartMargin) && !HostElection.Worth(C(1, 500), C(2, 574), HostElection.StartMargin), "start margin");
                Check(!HostElection.Worth(C(1, 500), C(1, 5000), 1), "never worth handing to yourself"); });
            Test("Host election: a slow host goes only to a clearly stronger machine, so the role cannot bounce back", () => {
                HostElection.Candidate M(ulong id, int now, int machine) => new HostElection.Candidate { Id = id, Fitness = now, BaseFitness = machine, PingMs = -1 };
                var host = M(1, 150, 400);   // bogged down by hosting
                Check(HostElection.Takeover(host, new[] { M(2, 900, 900), M(3, 1200, 500) }, HostElection.StruggleMargin) == 2, "only a machine 1.5x stronger qualifies");
                Check(HostElection.Takeover(host, new[] { M(3, 1200, 500) }, HostElection.StruggleMargin) == 0, "fast right now is not enough");
                // After the move the new host bogs down in turn; the old one looks fast again, but its machine is weaker.
                Check(HostElection.Takeover(M(2, 150, 900), new[] { M(1, 1000, 400) }, HostElection.StruggleMargin) == 0, "no bounce back");
                Check(HostElection.Takeover(M(1, 150, 0), new[] { M(2, 900, 900) }, HostElection.StruggleMargin) == 0, "unmeasured host keeps it"); });
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
                Check(ChatText.Next(ChatChannel.Party, c => true) == ChatChannel.All && ChatText.Next(ChatChannel.Team, c => true) == ChatChannel.Party &&
                      ChatText.Next(ChatChannel.Party, c => c != ChatChannel.All) == ChatChannel.Team && ChatText.Next(ChatChannel.All, c => false) == ChatChannel.All, "tab order"); });
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
            Console.WriteLine($"{passed} core tests passed."); return 0;
        }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
