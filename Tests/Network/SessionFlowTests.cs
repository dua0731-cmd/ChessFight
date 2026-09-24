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
            Console.WriteLine($"{passed} simulated session tests passed (not Steam integration tests).");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
