using System;
using System.Collections.Generic;
using System.Linq;
using ChessFight.Network;
using Steamworks;

public static class SessionFlowTests
{
    static List<SteamSession> clients;
    static int passed;
    static void Check(bool value,string error) {if(!value)throw new Exception(error+"\n"+string.Join("\n",clients.Select(c=>$"{c.Self}: match={c.Match} {c.Status} {c.Error}")));}
    static void As(SteamSession client,Action action){FakeSteam.User=client.Self;action();}
    static void Step(int ticks=8)
    {for(int t=0;t<ticks;t++){UnityEngine.Time.realtimeSinceStartup+=.3f;foreach(var c in clients.ToArray())As(c,c.Tick);}}
    static void Setup(int count,Func<int,SteamSession> make=null)
    {FakeSteam.Reset();clients=new List<SteamSession>();for(int i=1;i<=count;i++){FakeSteam.User=(ulong)i;var c=make!=null?make(i):new SteamSession();c.Initialize();clients.Add(c);}Step();}
    static string Presence(SteamSession c,string key)=>FakeSteam.Presence.TryGetValue((c.Self,key),out var v)?v:"";
    static void JoinParty(int follower,int leader){As(clients[follower],()=>clients[follower].JoinParty(clients[leader].Party));Step();}
    static void Test(string name,Action test)
    {test();passed++;Console.WriteLine("PASS "+name);foreach(var c in clients.ToArray())As(c,c.Dispose);}
    // Host election and migration (Docs/Network/HOST.md): a private room of solo players
    // with the given fitness, the first one its creator. `configure` runs before the room.
    static void Room(int[] fitness,Action configure=null)
    {
        Setup(fitness.Length);configure?.Invoke();
        for(int i=0;i<fitness.Length;i++)clients[i].LocalFitness=fitness[i];
        As(clients[0],()=>clients[0].FindMatch(true));Step();
        for(int i=1;i<clients.Count;i++){var c=clients[i];As(c,()=>c.JoinPrivateMatch(clients[0].Match));}
        Step(20);Check(clients.All(c=>c.Match==clients[0].Match&&c.Roster.Count==fitness.Length),"room did not fill");
    }
    static ulong Owner(SteamSession c)=>FakeSteam.Lobbies[c.Match].Owner;
    static string RoomData(SteamSession c,string key)=>FakeSteam.Lobbies[c.Match].Data.TryGetValue(key,out var v)?v:"";
    public static int Main()
    {
        try
        {
            Test("Private two-player party joins together and retains party after cancel",()=>{
                Setup(2);JoinParty(1,0);var leader=clients[0];As(leader,()=>leader.FindMatch(true));Step(20);
                Check(leader.Match!=0&&clients[1].Match==leader.Match&&leader.Roster.Count==2,"party follow failed");
                Check(leader.Roster[1].Team==leader.Roster[2].Team,"party split");
                As(leader,leader.StartGame);Step();Check(clients.All(c=>c.Started),"private start failed");
                As(leader,leader.Cancel);Step();Check(clients.All(c=>c.Match==0)&&clients[1].Party==leader.Party,"party lost");});
            Test("Independent solo player joins private match on other team",()=>{
                Setup(2);As(clients[0],()=>clients[0].FindMatch(true));Step();
                As(clients[1],()=>clients[1].JoinPrivateMatch(clients[0].Match));Step(20);
                Check(clients.All(c=>c.Roster.Count==2),"admission failed");Check(clients[0].Roster[1].Team!=clients[0].Roster[2].Team,"team balance");});
            Test("Follower cancellation returns whole party to idle",()=>{
                Setup(2);JoinParty(1,0);As(clients[0],()=>clients[0].FindMatch(true));Step(20);
                As(clients[1],clients[1].Cancel);Step(20);Check(clients.All(c=>c.Match==0&&!c.Searching),"cancel did not propagate");});
            Test("Cancelled asynchronous lobby join is not resurrected",()=>{
                Setup(2);As(clients[0],()=>clients[0].FindMatch(true));Step();
                As(clients[1],()=>{clients[1].JoinPrivateMatch(clients[0].Match);clients[1].Cancel();});Step(20);
                Check(clients[1].Match==0&&!clients[1].Searching,"late callback revived cancelled queue");
                Check(clients[0].Roster.Count==1,"cancelled player still admitted");});
            Test("Party member join failure cancels whole reservation",()=>{
                Setup(2);JoinParty(1,0);FakeSteam.Denied.Add(2);As(clients[0],()=>clients[0].FindMatch(true));Step(40);
                Check(clients.All(c=>c.Match==0&&!c.Searching),"partial party remained");});
            Test("Host departure aborts session without accidental host migration",()=>{
                Setup(2);As(clients[0],()=>clients[0].FindMatch(true));Step();As(clients[1],()=>clients[1].JoinPrivateMatch(clients[0].Match));Step(20);
                var host=clients[0];As(host,host.Dispose);clients.Remove(host);Step(20);
                Check(clients[0].Match==0,"new lobby owner incorrectly became game host");});
            Test("Twelve simultaneous solo searches converge and start six versus six",()=>{
                Setup(12);foreach(var c in clients)As(c,()=>c.FindMatch());Step(400);
                Check(clients.All(c=>c.Match!=0&&c.Started)&&clients.Select(c=>c.Match).Distinct().Count()==1,"queues fragmented");
                var roster=clients[0].Roster;Check(roster.Count==12&&roster.Values.Count(p=>p.Team==0)==6,"not 6v6");});
            Test("A party of another build is refused with a version message and a fresh party",()=>{
                Setup(2,i=>new SteamSession(i==1?"0.1.0-dev":"0.2.0-dev"));var host=clients[0];var guest=clients[1];ulong own=guest.Party;
                As(guest,()=>guest.JoinParty(host.Party));Step();
                Check(guest.Party!=0&&guest.Party!=host.Party&&guest.Party!=own,"guest should be alone in a new party");
                Check(guest.Error.Contains("버전")&&guest.Error.Contains("0.2.0-dev")&&guest.Error.Contains("0.1.0-dev"),"version message lost: "+guest.Error);
                Check(FakeSteam.Lobbies[host.Party].Members.Count==1,"guest left inside host party");});
            Test("Public search never matches a room of another build",()=>{
                Setup(2,i=>new SteamSession(i==1?"a":"b"));foreach(var c in clients)As(c,()=>c.FindMatch());Step(60);
                Check(clients.All(c=>c.Match!=0)&&clients[0].Match!=clients[1].Match,"different builds shared a room");});
            Test("Release rule keeps bots out of public matches but not test rooms",()=>{
                Setup(1,i=>new SteamSession("r",false));var c=clients[0];
                As(c,()=>c.SetPartyBots(2));As(c,()=>c.FindMatch());Step();
                Check(!c.Searching&&c.Match==0&&c.Error.Contains("봇"),"bots entered a public search");
                As(c,()=>c.FindMatch(true));Step(10);Check(c.Match!=0&&c.Roster.Count==3,"private room lost the party bots");
                As(c,c.FillRoomWithBots);Check(c.Roster.Count==12,"private filler refused");
                As(c,c.Cancel);As(c,()=>c.SetPartyBots(0));As(c,()=>c.FindMatch());Step(20);
                Check(c.IsHost&&!c.PrivateRoom&&!c.CanUseRoomBots,"public host should not offer room bots");
                As(c,c.FillRoomWithBots);Check(c.RoomBots==0&&c.Roster.Count==1,"public room took filler bots");});
            Test("Development builds may still take bots into public matches",()=>{
                Setup(1,i=>new SteamSession("d",true));var c=clients[0];
                As(c,()=>c.SetPartyBots(2));As(c,()=>c.FindMatch());Step(20);Check(c.Match!=0&&c.Roster.Count==3,"dev bots refused");});
            Test("Rich presence offers the idle party and a Steam join request follows it",()=>{
                Setup(2);var host=clients[0];var guest=clients[1];
                Check(Presence(host,"connect")=="+connect_lobby "+host.Party,"no join offer");
                As(guest,()=>FakeSteam.Raise(guest.Self,new GameRichPresenceJoinRequested_t{m_rgchConnect=Presence(host,"connect")}));Step();
                Check(guest.Party==host.Party,"rich presence join failed");
                As(host,()=>host.FindMatch(true));Step();Check(Presence(host,"connect")==""&&Presence(host,"status")!="","offer kept while busy");
                As(host,host.Cancel);Step();Check(Presence(host,"connect")=="+connect_lobby "+host.Party,"offer not restored");
                Check(SteamSession.ParseConnect("+connect_lobby 42")==42&&SteamSession.ParseConnect("x.exe -foo +connect_lobby 7 -bar")==7&&SteamSession.ParseConnect("+connect_lobby")==0,"parse");});
            Test("Followers see the leader's mode and only an idle leader can change it",()=>{
                Setup(2);JoinParty(1,0);var leader=clients[0];var follower=clients[1];
                Check(leader.PartyMode==GameModes.Default&&follower.PartyMode==GameModes.Default,"fresh party mode");
                bool changed=false;As(leader,()=>changed=leader.SetMode("swordfight"));Step();
                Check(changed&&follower.PartyMode==GameModes.SwordFight,"follower did not see the mode");
                As(follower,()=>changed=follower.SetMode("kingrush"));Check(!changed&&leader.PartyMode==GameModes.SwordFight,"follower changed the mode");
                As(leader,()=>changed=leader.SetMode("bogus"));Check(!changed,"unknown mode accepted");
                As(leader,()=>leader.FindMatch(true));Step(20);As(leader,()=>changed=leader.SetMode("kingrush"));
                Check(!changed&&leader.MatchMode==GameModes.SwordFight&&follower.MatchMode==GameModes.SwordFight,"room lost the mode");});
            Test("Public search only pairs parties that picked the same mode",()=>{
                Setup(3);As(clients[0],()=>clients[0].SetMode("queenhill"));Step();
                foreach(var c in clients)As(c,()=>c.FindMatch());Step(80);
                Check(clients.All(c=>c.Match!=0),"someone never got a room");
                Check(clients[1].Match==clients[2].Match&&clients[0].Match!=clients[1].Match,"different modes shared a room");
                Check(clients[0].MatchMode==GameModes.QueenOfTheHill&&clients[1].MatchMode==GameModes.KingRush,"room modes");});
            Test("A room joined by its number keeps the host's mode",()=>{
                Setup(2);As(clients[0],()=>clients[0].SetMode("swordfight"));As(clients[0],()=>clients[0].FindMatch(true));Step();
                As(clients[1],()=>clients[1].JoinPrivateMatch(clients[0].Match));Step(20);
                Check(clients[1].Match==clients[0].Match&&clients[1].MatchMode==GameModes.SwordFight&&clients[1].PartyMode==GameModes.KingRush,"joined room mode");});
            Test("Match start hands the host role to a clearly fitter PC",()=>{
                Room(new[]{500,900,700});int changes=0;foreach(var c in clients)c.HostChanged+=(a,b)=>changes++;
                var creator=clients[0];var fit=clients[1];As(creator,creator.StartGame);Step(10);
                Check(clients.All(c=>c.Started&&c.Host==fit.Self),"host did not move to the fittest PC");
                Check(fit.IsHost&&!creator.IsHost&&Owner(fit)==fit.Self,"lobby ownership did not follow the host");
                Check(clients.All(c=>c.Epoch==1)&&RoomData(fit,"host")==fit.Self.ToString(),"host record");
                Check(RoomData(fit,"successors")==$"{clients[2].Self},{creator.Self}","successor order: "+RoomData(fit,"successors"));
                Check(clients.All(c=>c.Roster.Count==3)&&changes==3,"roster or HostChanged: "+changes);});
            Test("A small fitness lead does not move the host",()=>{
                Room(new[]{800,850});As(clients[0],clients[0].StartGame);Step(10);
                Check(clients.All(c=>c.Started&&c.Host==clients[0].Self)&&Owner(clients[0])==clients[0].Self,"host moved for 6%");});
            Test("Ping weighs in: a fast PC far from everyone does not host",()=>{
                Room(new[]{500,900,600},()=>{
                    FakeSteam.PingLocations[1]="a";FakeSteam.PingLocations[2]="b";FakeSteam.PingLocations[3]="c";
                    FakeSteam.Pings[("a","b")]=200;FakeSteam.Pings[("b","c")]=190;FakeSteam.Pings[("a","c")]=20;});
                As(clients[0],clients[0].StartGame);Step(10);
                Check(clients.All(c=>c.Host==clients[2].Self),"expected the near PC to host, got "+clients[0].Host);});
            Test("A crashed host is replaced by its published successor and the match goes on",()=>{
                Room(new[]{900,700,500});var host=clients[0];var next=clients[1];var other=clients[2];
                As(host,host.StartGame);Step(20);
                Check(host.IsHost&&RoomData(host,"successors").StartsWith(next.Self+","),"no successor published: "+RoomData(host,"successors"));
                FakeSteam.Crash(host.Self);clients.Remove(host);Step(10);
                Check(next.IsHost&&other.Host==next.Self&&clients.All(c=>c.Started&&c.Match!=0),"match did not continue");
                Check(Owner(next)==next.Self&&RoomData(next,"host")==next.Self.ToString()&&RoomData(next,"epoch")=="2","record not taken over");
                Check(next.Roster.Count==2&&other.Roster.Count==2,"crashed host still on the roster");});
            Test("When Steam hands the room to someone else, ownership still reaches the successor",()=>{
                Room(new[]{900,500,700});var host=clients[0];var steamPick=clients[1];var next=clients[2];
                As(host,host.StartGame);Step(20);FakeSteam.Crash(host.Self);clients.Remove(host);
                Check(Owner(steamPick)==steamPick.Self,"setup: Steam should pick the first member");Step(10);
                Check(clients.All(c=>c.Host==next.Self)&&next.IsHost&&Owner(next)==next.Self,"ownership stuck with "+Owner(next));
                Check(RoomData(next,"host")==next.Self.ToString()&&RoomData(next,"epoch")=="2","record");});
            Test("A host that leaves a live match hands over instead of closing it",()=>{
                Room(new[]{900,700,500});var host=clients[0];As(host,host.StartGame);Step(20);
                As(host,host.Cancel);Step(10);
                Check(host.Match==0,"leaver still in the room");
                Check(clients[1].IsHost&&clients[2].Host==clients[1].Self&&clients[1].Started&&clients[2].Started&&clients[1].Match!=0,"match closed with its host");
                Check(RoomData(clients[1],"phase")=="playing","phase");});
            Test("A silent host is replaced only after the grace period, and steps down when it hears of it",()=>{
                Room(new[]{900,700,500});var a=clients[0];var b=clients[1];var c=clients[2];As(a,a.StartGame);
                As(b,()=>b.ReportHostSilence(5));Step(1);Check(a.IsHost&&b.Host==a.Self,"silence during the start moved the host");
                Step(40);As(b,()=>b.ReportHostSilence(5));Step(3);
                Check(b.IsHost&&c.Host==b.Self&&a.Host==b.Self&&!a.IsHost,"silent host not replaced everywhere");
                Check(Owner(b)==b.Self&&RoomData(b,"host")==b.Self.ToString()&&RoomData(b,"epoch")=="2","old host kept the room");});
            Test("A host that stays slow hands the match to a much stronger machine, not to a slightly stronger one",()=>{
                foreach(int other in new[]{1000,500})
                {
                    // The stronger machine happens to be busy in the lobby (100 ms frames), so the
                    // weaker creator keeps the host at the start; then the creator bogs down.
                    Room(new[]{400,other});var a=clients[0];var b=clients[1];
                    for(int t=0;t<10;t++){As(b,()=>b.ReportFrame(100));Step(1);}
                    As(a,a.StartGame);Step(2);Check(a.IsHost,"setup: the creator keeps the host");
                    for(int t=0;t<180;t++){As(a,()=>a.ReportFrame(60));As(b,()=>b.ReportFrame(16));Step(1);}
                    bool moved=b.IsHost&&a.Host==b.Self;
                    Check(moved==(other==1000),$"slow host with a {other} peer: moved={moved}");
                    foreach(var c in clients.ToArray())As(c,c.Dispose);
                }});
            Test("Loading: everyone sees each other's progress, bots are ready, and only the host sets the one start time",()=>{
                Room(new[]{900,800,700});var host=clients[0];
                As(host,host.FillRoomWithBots);Step();As(host,host.StartGame);Step();
                Check(clients.All(c=>c.Started)&&host.IsHost,"setup: started with the creator as host");
                var bots=host.Roster.Keys.Where(BotIdentity.IsBot).ToList();
                Check(bots.Count>0&&clients.All(c=>bots.All(b=>c.LoadPercent(b)==MatchStart.Ready)),"bots are not ready");
                foreach(var c in clients)As(c,()=>c.ReportLoading(40));Step();
                Check(clients.All(c=>clients.All(o=>c.LoadPercent(o.Self)==40)),"progress not shared");
                As(clients[1],()=>clients[1].ReportLoading(100));Step();
                Check(clients.All(c=>c.LoadPercent(clients[1].Self)==100),"ready not shared");
                As(clients[1],()=>clients[1].AnnounceStart(1234.5));Step();
                Check(clients.All(c=>c.StartAt==0),"a client set the start");
                As(host,()=>host.AnnounceStart(2000.25));As(host,()=>host.AnnounceStart(3000));Step();
                Check(clients.All(c=>Math.Abs(c.StartAt-2000.25)<.001),"start not shared, or overwritten");
                As(host,host.Cancel);Step();
                Check(host.Match==0&&host.StartAt==0&&host.LoadPercent(host.Self)==0,"leaving the match kept its loading state");});
            Console.WriteLine($"{passed} simulated session tests passed (not Steam integration tests).");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
