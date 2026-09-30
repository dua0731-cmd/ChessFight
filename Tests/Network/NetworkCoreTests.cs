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
            Test("Queen of the Hill race: each team opens its own shortcut, the first ring of the team counts", () => {
                var r = new QueenHillRace(7);
                Check(r.TryOpen(3, QueenHillRace.Black, 11, 50) && !r.TryOpen(3, QueenHillRace.Black, 12, 51), "first of the team");
                Check(r.IsOpen(3, QueenHillRace.Black) && !r.IsOpen(3, QueenHillRace.White) && r.OpenedBy(3, QueenHillRace.Black) == 11, "per team");
                Check(r.TryOpen(3, QueenHillRace.White, 21, 80) && r.OpenCount(QueenHillRace.White) == 1, "the other team opens its own later");
                Check(!r.TryOpen(0, QueenHillRace.White, 1, 1) && !r.TryOpen(8, QueenHillRace.White, 1, 1) && !r.TryOpen(2, -1, 1, 1), "bad input");
                var copy = new QueenHillRace(7);
                Check(copy.Apply(r.Encode()) && copy.IsOpen(3, QueenHillRace.Black) && copy.IsOpen(3, QueenHillRace.White) && !copy.Apply(r.Encode()), "travels as text, only opens");
                Check(!copy.Apply("3:1") && !copy.Apply("9:0:1"), "malformed dropped"); });
            Test("Queen of the Hill race: a player's tube goes to its own highest rank, never a teammate's", () => {
                var r = new QueenHillRace(7);
                Check(r.ReachedRank(1) == 0 && !r.Landed(1) && r.TubeTarget(1) == 1, "nothing yet = the bridge");
                Check(r.Reach(1, 1) && r.Landed(1) && r.TubeTarget(1) == 1, "landed on rank 1");
                r.Reach(1, 3); r.Reach(1, 2); r.Reach(2, 6);
                Check(r.ReachedRank(1) == 3 && r.TubeTarget(1) == 3 && r.TubeTarget(2) == 6, "own best, never lowered, not shared");
                Check(r.Reach(2, 8) && r.TubeTarget(2) == 7 && !r.Reach(2, 9) && !r.Reach(3, 0), "the summit is climbed, not ridden");
                r.Reset(); Check(r.TubeTarget(2) == 1 && r.OpenCount(QueenHillRace.White) == 0, "a new round"); });
            Test("Queen of the Hill course: seven floors, one per rank, the summit is rank 8", () => {
                Check(QueenHillCourse.Floors == 7 && QueenHillCourse.Sections == 7 && QueenHillCourse.All.Length == 7, "seven floors");
                Check(Math.Abs(QueenHillCourse.RankHeight(1) - 16f) < 1e-3f && Math.Abs(QueenHillCourse.TopHeight - 198f) < 1e-3f, "heights");
                Check(Enumerable.Range(1, 7).All(r => Math.Abs(QueenHillCourse.RankHeight(r + 1) - QueenHillCourse.RankHeight(r) - QueenHillCourse.FloorHeight) < 1e-3f), "even floors");
                Check(QueenHillCourse.All.Select((f, i) => f.Number == i + 1).All(ok => ok), "numbering");
                Check(QueenHillCourse.Floor(0) == null && QueenHillCourse.Floor(8) == null && QueenHillCourse.Floor(4).Shared && QueenHillCourse.Floor(7).Shared, "lookup");
                Check(QueenHillCourse.RankAt(16.5f) == 1 && QueenHillCourse.RankAt(42.3f) == 2 && QueenHillCourse.RankAt(41.9f) == 1
                      && QueenHillCourse.RankAt(200f) == 8 && QueenHillCourse.RankAt(0f) == 1, "rank at a height"); });
            Test("Queen of the Hill course: the hook reaches the cliff easily and a shortcut lift takes about seven seconds", () => {
                Check(QueenHillCourse.HookDistanceToRim < QueenHillCourse.HookFullReach * 0.8f, "the rim is out of an easy throw");
                Check(QueenHillCourse.LiftSeconds > 6f && QueenHillCourse.LiftSeconds < 8f, "the shortcut lift takes about seven seconds"); });
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
            Console.WriteLine($"{passed} core tests passed."); return 0;
        }
        catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
