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
    // A modified client in the host's room asks for seats with whatever members it likes; the host's answer.
    static string Ask(SteamSession host,ulong sender,string ticket,params ulong[] members)
    {
        byte[] bytes=System.Text.Encoding.UTF8.GetBytes("{\"kind\":\"reserve\",\"ticket\":\""+ticket+"\",\"party\":"+sender+",\"members\":["+string.Join(",",members)+"]}");
        FakeSteam.User=sender;SteamMatchmaking.SendLobbyChatMsg(new CSteamID(host.Match),bytes,bytes.Length);
        Step(2);return RoomData(host,"grant_"+sender);
    }
    // R533, the user's matching rules 1 ~ 2 (11-10) in a release build's public matchmaking, for a room that starts
    // `perTeam` a side: the manager may start from three a side with the sides even (rule 1), the room then has that
    // many seats (1-1, 1-4), a searcher finds no place in it and gets a room of its own (1-3, 2), a leaver's seat goes
    // to a searching player (1-2), and nobody beyond its seats gets in (1-3).
    static void PublicRoomRule(int perTeam)
    {
        int seats=perTeam*2;
        Setup(seats+2,i=>new SteamSession("r",false));
        var maker=clients[0];
        As(maker,()=>maker.FindMatch());Step(20);
        Check(maker.Match!=0&&maker.IsHost&&maker.IsManager&&!maker.PrivateRoom,"setup: the first searcher makes the room and manages it");
        ulong room=maker.Match;
        for(int i=1;i<seats;i++)
        {
            int n=i;bool may=n%2==0&&n/2>=3;
            Check(maker.CanStartGame==may&&clients.Take(n).All(c=>!c.Started)&&clients.Take(n).Count(c=>c.CanStartGame)==(may?1:0),
                  "with "+n+" in the room: the manager may start "+maker.CanStartGame+", "+clients.Take(n).Count(c=>c.Started)+" started");
            var next=clients[i];As(next,()=>next.FindMatch());Step(20);
            Check(next.Match==room&&maker.Roster.Count==i+1,"searcher "+(i+1)+" did not come into the room");
        }
        var players=clients.Take(seats).ToList();
        Check(players.All(c=>c.TeamCount(0)==perTeam&&c.TeamCount(1)==perTeam)||seats==12,"everyone reads the same sides: "+players[1].TeamCount(0)+" v "+players[1].TeamCount(1));
        // Short of twelve the room waits for its manager; twelve start by themselves.
        if(seats<12){Check(maker.CanStartGame&&players.All(c=>!c.Started),"not startable at "+perTeam+" a side");As(maker,maker.StartGame);}
        Step(5);
        Check(players.All(c=>c.Started)&&RoomData(maker,"seats")==perTeam+","+perTeam&&RoomData(maker,"open")=="0"&&!FakeSteam.Lobbies[room].Joinable,
              "the started room's seats: "+RoomData(maker,"seats")+" open "+RoomData(maker,"open"));
        var extra=clients[seats];As(extra,()=>extra.FindMatch());Step(40);
        Check(extra.Match!=0&&extra.Match!=room&&extra.IsHost&&extra.IsManager&&!extra.Started&&maker.Roster.Count==seats,"a searcher with the room full should get a room of its own");
        foreach(var c in players){var x=c;As(x,()=>x.ReportLoading(100));}
        Step(10);Check(maker.StartAt>0,"setup: the shared start");
        var leaver=players[3];int team=maker.Roster[leaver.Self].Team;
        As(leaver,leaver.Cancel);Step(5);
        Check(maker.Roster.Count==seats-1&&RoomData(maker,"open")=="1"&&RoomData(maker,"free"+team)=="1"&&RoomData(maker,"free"+(1-team))=="0","the leaver's seat is not offered");
        for(int t=0;t<80&&!extra.Started;t++)Step(1);
        Check(extra.Match==room&&extra.Started&&maker.Roster.Count==seats&&maker.Roster[extra.Self].Team==team,"the searching player did not take the leaver's seat");
        Step(3);
        Check(RoomData(maker,"open")=="0"&&!FakeSteam.Lobbies[room].Joinable,"the refilled room is still offered");
        var last=clients[seats+1];As(last,()=>last.FindMatch());Step(40);
        Check(last.Match!=0&&last.Match!=room&&maker.Roster.Count==seats,"one more than its seats got into the room");
    }
    // R533, rule 3: a session that counts its desertions (a build; the Editor's does not), kept in `store`.
    static void Penalised(SteamSession c,Dictionary<ulong,string> store)
    {c.LeavePenaltyOn=true;c.LoadLeaves=()=>store.TryGetValue(c.Self,out var v)?v:"";c.SaveLeaves=v=>store[c.Self]=v;}
    // A public match of two under way (a development build starts from one a side), `a` its maker.
    static void Duel(SteamSession a,SteamSession b)
    {
        As(a,()=>a.FindMatch());Step(20);As(b,()=>b.FindMatch());Step(20);
        Check(a.Match!=0&&b.Match==a.Match&&a.IsManager&&a.CanStartGame,"setup: a public room of two: "+a.Error+" / "+b.Error);
        As(a,a.StartGame);Step(6);Check(a.Started&&b.Started,"setup: the match under way");
    }
    public static int Main()
    {
        try
        {
            Test("Private two-player party joins together and retains party after cancel",()=>{
                Setup(2);JoinParty(1,0);var leader=clients[0];As(leader,()=>leader.FindMatch(true));Step(20);
                Check(leader.Match!=0&&clients[1].Match==leader.Match&&leader.Roster.Count==2,"party follow failed");
                Check(leader.Roster[1].Team==leader.Roster[2].Team,"party split");
                As(leader,leader.StartGame);Step();Check(clients.All(c=>c.Started),"private start failed");
                // R60: leaving a started match takes only the leaver out.
                As(leader,leader.Cancel);Step();Check(leader.Match==0&&clients[1].Started&&clients[1].IsHost,"the match should go on without the leaver");
                As(clients[1],clients[1].Cancel);Step();Check(clients.All(c=>c.Match==0)&&clients[1].Party==leader.Party&&!clients[1].Busy,"party lost");});
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
            // Before R159 any waiting-room host's departure ended the room. Now a host that quits hands it on
            // with the reservations; only a crash (no word, nothing handed over) still ends it.
            Test("A waiting-room host that quits hands the room to the next player, who can start it",()=>{
                Setup(2);As(clients[0],()=>clients[0].FindMatch(true));Step();As(clients[1],()=>clients[1].JoinPrivateMatch(clients[0].Match));Step(20);
                var host=clients[0];var next=clients[1];ulong room=host.Match;As(host,host.Dispose);clients.Remove(host);Step(20);
                Check(next.Match==room&&next.IsHost&&Owner(next)==next.Self&&next.Roster.Count==1&&next.Roster.ContainsKey(next.Self),"the room did not go on under the next player");
                bool ok=false;As(next,()=>ok=next.AddTestBot(1));Check(ok&&next.CanStartGame,"the new host cannot run the room");});
            Test("A crashed waiting-room host still ends the room without accidental host migration",()=>{
                Setup(2);As(clients[0],()=>clients[0].FindMatch(true));Step();As(clients[1],()=>clients[1].JoinPrivateMatch(clients[0].Match));Step(20);
                var host=clients[0];FakeSteam.Crash(host.Self);clients.Remove(host);Step(20);
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
                Setup(3);As(clients[0],()=>clients[0].SetMode("swordfight"));Step();
                foreach(var c in clients)As(c,()=>c.FindMatch());Step(80);
                Check(clients.All(c=>c.Match!=0),"someone never got a room");
                Check(clients[1].Match==clients[2].Match&&clients[0].Match!=clients[1].Match,"different modes shared a room");
                Check(clients[0].MatchMode==GameModes.SwordFight&&clients[1].MatchMode==GameModes.KingRush,"room modes");});
            Test("A room joined by its number keeps the host's mode",()=>{
                Setup(2);As(clients[0],()=>clients[0].SetMode("swordfight"));As(clients[0],()=>clients[0].FindMatch(true));Step();
                As(clients[1],()=>clients[1].JoinPrivateMatch(clients[0].Match));Step(20);
                Check(clients[1].Match==clients[0].Match&&clients[1].MatchMode==GameModes.SwordFight&&clients[1].PartyMode==GameModes.KingRush,"joined room mode");});
            Test("Sword Fight private lobby starts alone with bots and returns to the party",()=>{
                Setup(1); var c=clients[0]; ulong party=c.Party;
                As(c,()=>c.SetMode("swordfight")); As(c,()=>c.FindMatch(true)); Step(20);
                As(c,c.FillRoomWithBots); Step(); As(c,c.StartGame); Step();
                Check(c.Started&&c.Roster.Count==12&&c.MatchMode.Playable&&c.MatchMode.Scene=="SwordFight","sword mode start");
                As(c,c.Cancel); Step(); Check(c.Match==0&&c.Party==party&&c.PartyMode==GameModes.SwordFight,"return party");});
            Test("Solo Sword Fight starts immediately with one chosen enemy dummy",()=>{
                Setup(1);var c=clients[0];ulong party=c.Party;
                As(c,()=>c.SetMode("swordfight"));As(c,()=>c.FindMatch(true));Step();
                Check(!c.CanStartGame,"empty test starts");int team=c.Roster[c.Self].Team;
                bool ok=false;As(c,()=>ok=c.AddTestBot(1-team));
                Check(ok&&c.Roster.Count==2&&c.Roster.Values.Count(p=>p.Team==1-team)==1&&c.CanStartGame,"1v1 dummy admission");
                As(c,c.StartGame);Check(c.Started,"immediate start requires another click");
                As(c,()=>ok=c.AddTestBot(team));Check(!ok&&c.Roster.Count==2,"edit after start");
                As(c,()=>ok=c.RemoveTestBot(1-team));Check(!ok,"remove during match");
                As(c,c.Cancel);Step();Check(c.Party==party&&c.Match==0&&c.RoomBots==0,"cancel leaked dummy");});
            Test("Solo private dummy counts can be changed independently up to six per team",()=>{
                Setup(1);var c=clients[0];As(c,()=>c.FindMatch(true));Step();int team=c.Roster[c.Self].Team;
                for(int i=0;i<8;i++)As(c,()=>c.AddTestBot(team));
                for(int i=0;i<8;i++)As(c,()=>c.AddTestBot(1-team));
                Check(c.Roster.Count==12&&c.TestBots(team)==5&&c.TestBots(1-team)==6&&!c.Started,"overfill or private auto-start");
                As(c,()=>c.RemoveTestBot(1-team));Check(c.TestBots(1-team)==5&&c.TestBots(team)==5,"wrong team removed");
                As(c,c.ClearRoomBots);Check(c.Roster.Count==1&&!c.CanStartGame,"clear removed self or retained start");
                As(c,()=>c.AddTestBot(team));Check(c.CanStartGame&&c.Roster.Values.All(p=>p.Team==team),"ally-only test refused");});
            Test("Dummy controls are host-only and preserve real players and party bots",()=>{
                Setup(2);var h=clients[0];var guest=clients[1];As(h,()=>h.SetPartyBots(1));As(h,()=>h.FindMatch(true));Step();
                As(guest,()=>guest.JoinPrivateMatch(h.Match));Step(20);int team=h.Roster[h.Self].Team;
                bool ok=true;As(guest,()=>ok=guest.AddTestBot(team));Check(!ok,"guest added dummy");
                As(h,()=>h.AddTestBot(1-team));Step();
                Check(guest.Roster.Count==4&&guest.Roster.Keys.Count(BotIdentity.IsBot)==2,"follower roster stale");
                As(guest,()=>ok=guest.RemoveTestBot(1-team));Check(!ok,"guest removed dummy");
                As(h,()=>h.RemoveTestBot(1-team));As(h,()=>ok=h.RemoveTestBot(team));Step();
                Check(!ok&&h.Roster.Count==3&&h.Roster.ContainsKey(guest.Self)&&h.RoomBots==1,"human or party bot removed");});
            Test("Selective dummy tools cannot bypass public matching or invalid team rules",()=>{
                Setup(1,i=>new SteamSession("r",false));var c=clients[0];bool ok=true;
                As(c,()=>ok=c.AddTestBot(0));Check(!ok,"bot outside room");
                As(c,()=>c.FindMatch());Step(20);As(c,()=>ok=c.AddTestBot(1));Check(!ok&&c.Roster.Count==1&&!c.CanStartGame,"release public bypass");
                As(c,c.Cancel);As(c,()=>c.FindMatch(true));Step();
                As(c,()=>ok=c.AddTestBot(2));Check(!ok,"invalid team");
                As(c,()=>ok=c.AddTestBot(1));Check(ok&&c.CanStartGame,"release private refused");});
            // R159 (승규: "입장씬부터 PC 성능 1등이 방장"): the waiting room already hands the role to the best PC,
            // reservations and all; the start keeps it there.
            // R533 (user 11-10, rule 4): the best PC simulates; whoever made the room still manages it - the start is theirs,
            // asked of the host.
            Test("The waiting room hands the host role to the best PC, the start stays with the room's maker, and the match keeps that host",()=>{
                int changes=0;Room(new[]{500,900,700},()=>{foreach(var c in clients)c.HostChanged+=(a,b)=>changes++;});
                var creator=clients[0];var fit=clients[1];
                Check(clients.All(c=>c.Host==fit.Self&&!c.Started)&&Owner(fit)==fit.Self,"the waiting room did not go to the best PC");
                Check(clients.All(c=>c.Manager==creator.Self)&&creator.IsManager&&creator.CanStartGame&&!fit.CanStartGame,"the start did not stay with the room's maker");
                As(fit,fit.StartGame);Step(4);Check(clients.All(c=>!c.Started),"the host started a room it does not manage");
                As(creator,creator.StartGame);Step(10);
                Check(clients.All(c=>c.Started&&c.Host==fit.Self),"host did not move to the fittest PC");
                Check(fit.IsHost&&!creator.IsHost&&Owner(fit)==fit.Self,"lobby ownership did not follow the host");
                Check(clients.All(c=>c.Epoch==1)&&RoomData(fit,"host")==fit.Self.ToString(),"host record");
                Check(RoomData(fit,"successors")==$"{clients[2].Self},{creator.Self}","successor order: "+RoomData(fit,"successors"));
                Check(clients.All(c=>c.Roster.Count==3)&&changes==3,"roster or HostChanged: "+changes);});
            // R159 (승규, 10-25): the room's creator kept the host role although a stronger PC came in later.
            Test("A stronger PC that came in later takes the host role though its frame rate dipped as it joined",()=>{
                Setup(2);var creator=clients[0];var strong=clients[1];creator.LocalFitness=1000;strong.LocalFitness=1400;
                // Coming into the room: the entrance screen and the gate walk cost frames (60 ms on average).
                for(int i=0;i<60;i++)strong.ReportFrame(60);
                As(creator,()=>creator.FindMatch(true));Step();As(strong,()=>strong.JoinPrivateMatch(creator.Match));Step(20);
                Check(strong.CurrentFitness<1000,"the dip should show in the live score: "+strong.CurrentFitness);
                Check(clients.All(c=>c.Host==strong.Self),"the waiting room stayed with the creator");
                As(creator,creator.StartGame);Step(10);
                Check(clients.All(c=>c.Started&&c.Host==strong.Self)&&Owner(strong)==strong.Self,"host stayed with the creator");});
            Test("The start waits for a newcomer's machine score, and goes on without it after a while",()=>{
                Room(new[]{1000,0});var creator=clients[0];var strong=clients[1];
                As(creator,creator.StartGame);Step(2);
                Check(!creator.Started&&creator.Status.Contains("물리 계산을 맡을 PC"),"started before knowing the newcomer: "+creator.Status);
                strong.LocalFitness=1400;Step(10);
                Check(clients.All(c=>c.Started&&c.Host==strong.Self),"the score that came in late was not used");
                Room(new[]{1000,0});creator=clients[0];
                As(creator,creator.StartGame);Step(25);
                Check(clients.All(c=>c.Started&&c.Host==creator.Self),"a newcomer that never reports must not hold the start up");});
            Test("Nobody leaves: a better PC that comes in later becomes the waiting room's host, then a still better one",()=>{
                Setup(3);var creator=clients[0];var mid=clients[1];var best=clients[2];
                creator.LocalFitness=900;mid.LocalFitness=1000;best.LocalFitness=1400;
                As(creator,()=>creator.FindMatch(true));Step();
                Check(creator.IsHost,"setup: the creator hosts alone");
                As(mid,()=>mid.JoinPrivateMatch(creator.Match));Step(20);
                Check(new[]{creator,mid}.All(c=>c.Host==mid.Self&&c.Match==creator.Match)&&Owner(mid)==mid.Self&&creator.Match!=0,"the better newcomer did not take the room");
                As(best,()=>best.JoinPrivateMatch(creator.Match));Step(20);
                Check(clients.All(c=>c.Host==best.Self&&c.Roster.Count==3)&&Owner(best)==best.Self,"the best newcomer did not take the room");
                Check(clients.All(c=>c.Manager==creator.Self),"the room's manager moved with its host");
                As(creator,creator.StartGame);Step(10);
                Check(clients.All(c=>c.Started&&c.Host==best.Self),"the start moved the host away from first place");});
            // R294 (승규, 11-02: "매칭이 되면 먼저 들어온 사람이 방장"): the public matchmaking room, not only a private one.
            Test("Public matchmaking: a stronger PC the search brings in later hosts the room; its maker fills it with bots and starts it",()=>{
                Setup(2);var first=clients[0];var strong=clients[1];first.LocalFitness=900;strong.LocalFitness=1300;
                As(first,()=>first.FindMatch());Step(20);
                Check(first.IsHost&&first.Match!=0&&!first.PrivateRoom,"setup: the first searcher makes the room");
                int lines=UnityEngine.Debug.Lines.Count;
                As(strong,()=>strong.FindMatch());Step(30);
                Check(strong.Match==first.Match&&clients.All(c=>c.Host==strong.Self&&!c.Started&&c.Roster.Count==2)&&Owner(strong)==strong.Self,"the public room stayed with the first player");
                Check(UnityEngine.Debug.Lines.Skip(lines).Any(l=>l.Contains("물리 담당 정하기(대기실)")&&l.Contains("넘김")),"the election left no line in the log");
                // R533: the room's tools are its manager's (the one who made it), asked of the host.
                Check(first.CanAddRoomBot&&!strong.CanAddRoomBot,"the room's tools did not stay with its maker");
                bool ok=true;for(int i=0;i<4;i++)As(first,()=>ok&=first.AddRoomBot());Step(4);
                Check(ok&&first.CanStartGame&&!strong.CanStartGame&&first.Roster.Count==6,"the manager cannot start three against three: "+first.Roster.Count);
                As(first,first.StartGame);Step(10);
                Check(clients.All(c=>c.Started&&c.Host==strong.Self&&c.Roster.Count==6),"the start moved the host away from first place");});
            // Real Steam takes a moment to move a room's ownership. A waiting-room handover whose ownership comes
            // through late must not read as a host that left: the room used to end 2 seconds into it.
            Test("A waiting-room handover survives a slow change of the room's owner",()=>{
                Setup(3);var creator=clients[0];var strong=clients[1];var other=clients[2];
                creator.LocalFitness=900;strong.LocalFitness=1300;other.LocalFitness=800;FakeSteam.OwnerDelay=5;
                As(creator,()=>creator.FindMatch(true));Step();
                As(other,()=>other.JoinPrivateMatch(creator.Match));Step(20);
                As(strong,()=>strong.JoinPrivateMatch(creator.Match));Step(50);
                Check(clients.All(c=>c.Match==creator.Match&&c.Host==strong.Self&&c.Roster.Count==3)&&Owner(strong)==strong.Self,"the room ended or the host did not move");
                Check(creator.CanStartGame&&!strong.CanStartGame,"the room's maker cannot start it under the new host");
                As(creator,creator.StartGame);Step(30);
                Check(clients.All(c=>c.Started&&c.Host==strong.Self),"the start after a slow handover");});
            // R525 (user 11-09, the first public test with six PCs: "10초 안에 전부 들어와서 시작해도 방장PC만 시작되고 나머지는
            // 튕김"): their machines' scores and their order, Steam taking a second to move a room's owner. The room went to a
            // better PC twice while players came in; the host started as soon as it could with six in the room, and the
            // players whose seats had not been granted yet (asked during the handover) were thrown out, one of them later by
            // the new host's old copy of the roster. Now the start waits for them and the new host reads the room's roster.
            Test("Six public searchers: the start waits for everyone in the room to be seated, through slow handovers to the best PC",()=>{
                Setup(6);int[] fit={845,701,737,697,635,529};float[] at={2,0,3,4,5,6};
                for(int i=0;i<6;i++){clients[i].LocalFitness=fit[i];FakeSteam.PingLocations[clients[i].Self]="loc"+i;}
                for(int a=0;a<6;a++)for(int b=a+1;b<6;b++)FakeSteam.Pings[("loc"+a,"loc"+b)]=25;
                FakeSteam.OwnerDelay=1;var searching=new bool[6];bool pressed=false;
                for(int tick=0;tick<240;tick++)
                {
                    for(int i=0;i<6;i++)if(!searching[i]&&tick*.25f>=at[i]){searching[i]=true;var c=clients[i];As(c,()=>c.FindMatch());}
                    UnityEngine.Time.realtimeSinceStartup+=.25f;foreach(var c in clients.ToArray())As(c,c.Tick);
                    // R533: the room's manager presses (whoever made the room that everyone ended up in), not its host.
                    var host=clients.FirstOrDefault(c=>c.IsManager&&c.Match!=0&&!c.Started&&c.CanStartGame);
                    if(host!=null&&!pressed&&clients.Count(c=>c.Match==host.Match)==6){pressed=true;As(host,host.StartGame);}
                }
                Check(pressed,"the manager never could start with six in the room");
                Check(clients.All(c=>c.Started&&c.Match==clients[0].Match&&c.Error==""),"someone was thrown out at the start");
                Check(clients.All(c=>c.Host==clients[0].Self)&&clients[0].Roster.Count==6,"not everyone in one match under the best PC");
                Check(clients.All(c=>c.EmptySeats==0)&&RoomData(clients[0],"seats")=="3,3","the match started short: "+RoomData(clients[0],"seats"));});
            Test("A player in the waiting room who never asks for a seat holds the start only for a while",()=>{
                Room(new[]{900,800});var host=clients[0];
                FakeSteam.Lobbies[host.Match].Members.Add(99);Step(2);
                Check(!host.CanStartGame&&host.SeatingJoiners==1,"a player in the room without a seat should hold the start");
                Step(30);
                Check(host.CanStartGame&&host.SeatingJoiners==0,"one who never asks must not hold it for ever");
                As(host,host.StartGame);Step(10);Check(clients.All(c=>c.Started),"the start after the wait");});
            // R295: the PC a waiting room was handed to leaves (its party cancels) before Steam has moved the room to it.
            // The old host still owns the room and takes it back; the room used to end 2 seconds later.
            Test("A waiting-room handover whose new host leaves before the room is theirs goes back to the old host",()=>{
                Setup(3);var creator=clients[0];var strong=clients[1];var other=clients[2];
                creator.LocalFitness=900;strong.LocalFitness=1300;other.LocalFitness=800;FakeSteam.OwnerDelay=30;
                As(creator,()=>creator.FindMatch(true));Step();
                As(other,()=>other.JoinPrivateMatch(creator.Match));Step(20);
                ulong room=creator.Match;As(strong,()=>strong.JoinPrivateMatch(room));
                for(int i=0;i<60&&!clients.All(c=>c.Host==strong.Self);i++)Step(1);
                Check(clients.All(c=>c.Match==room&&c.Host==strong.Self)&&Owner(creator)==creator.Self,"setup: handed over, the room not moved yet");
                As(strong,strong.Cancel);clients.Remove(strong);Step(20);
                Check(clients.All(c=>c.Match==room&&c.Host==creator.Self&&c.Roster.Count==2)&&Owner(creator)==creator.Self,"the room ended or did not go back to its old host");
                Check(creator.CanStartGame,"the old host cannot run the room again");
                As(creator,creator.StartGame);Step(10);
                Check(clients.All(c=>c.Started&&c.Host==creator.Self),"the start after taking the room back");});
            Test("A PC that leaves a waiting room just after it became the room's owner still hands it on with everyone's seats",()=>{
                Setup(3);var creator=clients[0];var strong=clients[1];var other=clients[2];
                creator.LocalFitness=900;strong.LocalFitness=1300;other.LocalFitness=800;FakeSteam.OwnerDelay=3;
                As(creator,()=>creator.FindMatch(true));Step();
                As(other,()=>other.JoinPrivateMatch(creator.Match));Step(20);
                ulong room=creator.Match;As(strong,()=>strong.JoinPrivateMatch(room));
                for(int i=0;i<60&&strong.Host!=strong.Self;i++)Step(1);
                // Steam moves the room to the strong PC, which leaves before it has looked at the room again.
                UnityEngine.Time.realtimeSinceStartup+=FakeSteam.OwnerDelay+1;
                As(strong,strong.Cancel);clients.Remove(strong);Step(20);
                Check(clients.All(c=>c.Match==room&&c.Host==creator.Self&&c.Roster.Count==2)&&Owner(creator)==creator.Self,"the room ended or lost its seats");});
            // R294: each PC measures itself again while it waits in the lobby (HostFitnessProbe). A first
            // measurement taken while the PC was busy loading reads slow; the better one, when it comes, counts.
            Test("A PC whose machine score improves later takes the waiting room then",()=>{
                Room(new[]{1000,800});var creator=clients[0];var other=clients[1];
                Check(clients.All(c=>c.Host==creator.Self),"setup: the creator is first");
                other.LocalFitness=1300;Step(10);
                Check(clients.All(c=>c.Host==other.Self&&!c.Started)&&Owner(other)==other.Self,"the better measurement did not move the host");});
            Test("The best PC hosts even by a small lead (R159: no 15% rule before the match)",()=>{
                Room(new[]{800,850});var best=clients[1];As(clients[0],clients[0].StartGame);Step(10);
                Check(clients.All(c=>c.Started&&c.Host==best.Self)&&Owner(best)==best.Self,"the 6% better PC does not host");});
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
            // R295: a slow PC still loading reports no silence (SteamMotion.Loading, HostSilence in the match scenes): a
            // host that has gone on to its own scene stops the lobby's stream, which used to read as a silent host.
            Test("A successor still loading reports no silence and does not take over, however long the load; a silent host is still replaced afterwards",()=>{
                Room(new[]{900,700,500});var a=clients[0];var b=clients[1];var c=clients[2];As(a,a.StartGame);Step(2);
                for(int t=0;t<200;t++){As(b,()=>{b.ReportLoading(30);b.ReportHostSilence(0);});Step(1);}
                Check(clients.All(x=>x.Host==a.Self&&x.Started&&x.Match!=0)&&a.IsHost,"a PC still loading moved the host or left");
                As(b,()=>{b.ReportLoading(100);b.ReportHostSilence(5);});Step(3);
                Check(b.IsHost&&c.Host==b.Self&&a.Host==b.Self,"a silent host is no longer replaced");});
            // R295: a member's claim used to be followed whoever made it and whatever its number.
            Test("A forged claim (outside the roster, out of turn, too large, int.MaxValue) is ignored, and the successor in line still takes over",()=>{
                Room(new[]{900,700,500});var a=clients[0];var b=clients[1];var c=clients[2];As(a,a.StartGame);Step(20);
                Check(a.IsHost&&RoomData(a,"successors")==$"{b.Self},{c.Self}","setup: "+RoomData(a,"successors"));
                var room=FakeSteam.Lobbies[a.Match];
                // Someone in the room without a seat.
                room.Members.Add(99);room.MemberData[(99,"claim")]="2";Step(4);
                Check(clients.All(x=>x.Host==a.Self&&x.Epoch==1),"a player outside the roster took the match");
                room.MemberData[(c.Self,"claim")]="2";Step(4);
                Check(clients.All(x=>x.Host==a.Self&&x.Epoch==1),"a claim out of turn took the match");
                room.MemberData[(b.Self,"claim")]="5";Step(4);
                Check(clients.All(x=>x.Host==a.Self&&x.Epoch==1),"too large a claim took the match");
                room.MemberData[(c.Self,"claim")]=room.MemberData[(99,"claim")]=int.MaxValue.ToString();Step(4);
                Check(clients.All(x=>x.Host==a.Self&&x.Epoch==1),"int.MaxValue took the match");
                room.Members.Remove(99);FakeSteam.Crash(a.Self);clients.Remove(a);Step(10);
                Check(b.IsHost&&c.Host==b.Self&&clients.All(x=>x.Started&&x.Epoch==2),"the successor in line did not take over: "+b.Epoch);
                Check(Owner(b)==b.Self&&RoomData(b,"epoch")=="2"&&room.MemberData[(b.Self,"claim")]=="2","the new epoch went past the forged ones");});
            Test("Successors missing one after another: the next in line takes over, also for a PC that never heard of the one before",()=>{
                Room(new[]{900,800,700,600});var a=clients[0];var b=clients[1];var c=clients[2];var d=clients[3];As(a,a.StartGame);Step(20);
                Check(RoomData(a,"successors")==$"{b.Self},{c.Self},{d.Self}","setup: "+RoomData(a,"successors"));
                FakeSteam.Crash(a.Self);FakeSteam.Crash(b.Self);clients.Remove(a);clients.Remove(b);Step(10);
                Check(c.IsHost&&d.Host==c.Self&&c.Epoch==2&&d.Epoch==2&&clients.All(x=>x.Started),"the second successor did not take over");
                Check(Owner(c)==c.Self&&RoomData(c,"epoch")=="2","record");
                Room(new[]{900,800,700,600});a=clients[0];b=clients[1];c=clients[2];d=clients[3];As(a,a.StartGame);Step(20);
                FakeSteam.Crash(a.Self);clients.Remove(a);
                Action<SteamSession> alone=x=>{UnityEngine.Time.realtimeSinceStartup+=.3f;As(x,x.Tick);};
                // B steps in and C follows it, then B is gone before D has heard of it.
                alone(b);Check(b.IsHost&&b.Epoch==2,"setup: B steps in");
                alone(c);Check(c.Host==b.Self&&c.Epoch==2,"setup: C follows B");
                FakeSteam.Crash(b.Self);clients.Remove(b);
                alone(c);Check(c.IsHost&&c.Epoch==3,"C did not step in after B");
                alone(d);Check(d.Host==c.Self&&d.Epoch==3,"D, which never heard of B, did not follow C");
                Step(10);Check(c.IsHost&&d.Host==c.Self&&Owner(c)==c.Self&&RoomData(c,"epoch")=="3"&&clients.All(x=>x.Started),"the match did not go on under C");});
            Test("A host that stays slow hands the match to a much stronger machine, not to a slightly stronger one",()=>{
                foreach(int other in new[]{1000,500})
                {
                    // The stronger machine has no score yet at the start (its probe still running, past the
                    // start's wait), so the weaker creator keeps the host; then the creator bogs down.
                    // (Before R159 a stronger machine busy in the lobby did this; the start now compares machines alone.)
                    Room(new[]{400,0});var a=clients[0];var b=clients[1];
                    As(a,a.StartGame);Step(20);Check(a.IsHost&&a.Started,"setup: the creator keeps the host");
                    b.LocalFitness=other;
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
            // R502: the host's session picks the start (it was the loading screen's job, which stops waiting after
            // MatchStart.ClientGiveUp, so a host that took over later never picked one).
            Test("The host's session sets the one start itself: not before its own load, at once when everyone has loaded",()=>{
                Room(new[]{900,800,700});var host=clients[0];
                As(host,host.StartGame);Step(2);
                Check(clients.All(c=>c.Started)&&host.IsHost,"setup: started with the creator as host");
                As(clients[1],()=>clients[1].ReportLoading(100));As(clients[2],()=>clients[2].ReportLoading(100));As(host,()=>host.ReportLoading(80));
                Step(40);
                Check(clients.All(c=>c.StartAt==0),"a start before the host itself had loaded");
                As(host,()=>host.ReportLoading(100));Step(2);
                double now=SteamUtils.GetServerRealTime();
                Check(clients.All(c=>c.StartAt>0&&Math.Abs(c.StartAt-host.StartAt)<.001),"no shared start once everyone had loaded");
                Check(host.StartAt>now-1&&host.StartAt<=now+MatchStart.Lead,"the start is not just ahead of the shared clock: "+host.StartAt+" at "+now);
                double first=host.StartAt;Step(10);
                Check(clients.All(c=>c.StartAt==first),"the start was written again");});
            Test("A host that takes over after the loading screens stopped waiting still sets the start: at once, or MaxWait after its takeover for a slow player",()=>{
                Room(new[]{900,700,500,400});var a=clients[0];var b=clients[1];var c=clients[2];var d=clients[3];
                As(a,a.StartGame);Step(20);
                Check(a.IsHost&&clients.All(x=>x.Started)&&RoomData(a,"successors").StartsWith(b.Self+","),"setup: "+RoomData(a,"successors"));
                // The host never finishes its load; the others wait past the loading screen's give-up.
                As(a,()=>a.ReportLoading(30));foreach(var x in new[]{b,c,d})As(x,()=>x.ReportLoading(100));
                Step((int)(MatchStart.ClientGiveUp/.3)+10);
                Check(clients.All(x=>x.StartAt==0),"a start without the host");
                FakeSteam.Crash(a.Self);clients.Remove(a);Step(4);
                Check(b.IsHost&&c.Host==b.Self&&d.Host==b.Self&&Owner(b)==b.Self,"setup: the successor did not take over");
                Check(clients.All(x=>x.StartAt>0&&Math.Abs(x.StartAt-b.StartAt)<.001),"the new host never set the start");
                // Once more with a player still loading: the new host waits MaxWait from its takeover, not from its own load.
                Room(new[]{900,700,500,400});a=clients[0];b=clients[1];c=clients[2];d=clients[3];
                As(a,a.StartGame);Step(20);
                As(a,()=>a.ReportLoading(30));As(b,()=>b.ReportLoading(100));As(c,()=>c.ReportLoading(100));As(d,()=>d.ReportLoading(60));
                Step((int)(MatchStart.ClientGiveUp/.3)+10);
                FakeSteam.Crash(a.Self);clients.Remove(a);Step(4);
                Check(b.IsHost&&clients.All(x=>x.StartAt==0),"the new host started at once with a player still loading");
                // About 16 s after the takeover (the new host had loaded more than 30 s before it).
                Step(50);
                Check(clients.All(x=>x.StartAt==0),"the new host counted the wait from its own load");
                // About 22 s after it: past MaxWait (20 s).
                Step(20);
                Check(clients.All(x=>x.StartAt>0&&Math.Abs(x.StartAt-b.StartAt)<.001),"the slow player held the start past MaxWait");});
            // R503 [4]: a PC the waiting room had just been handed to could start it, or add bots, before Steam had moved
            // the room to it: the start was written nowhere and it went into the match alone.
            // R533: the new host does not manage the room at all; its maker does, and a start it asks for while the room is
            // still on its way to the host happens once the host owns it (it is asked again until then).
            Test("A waiting-room handover: the new host has no room tools, and the manager's start asked meanwhile happens once Steam has given the host the room",()=>{
                Setup(3);var creator=clients[0];var strong=clients[1];var other=clients[2];
                creator.LocalFitness=900;strong.LocalFitness=1300;other.LocalFitness=800;FakeSteam.OwnerDelay=5;
                As(creator,()=>creator.FindMatch(true));Step();
                As(other,()=>other.JoinPrivateMatch(creator.Match));Step(20);
                ulong room=creator.Match;As(strong,()=>strong.JoinPrivateMatch(room));
                for(int i=0;i<60&&strong.Host!=strong.Self;i++)Step(1);
                Check(clients.All(c=>c.Match==room&&c.Host==strong.Self&&c.Manager==creator.Self)&&Owner(strong)==creator.Self,"setup: handed over, the room not moved yet");
                Check(!strong.CanStartGame&&!strong.CanUseRoomBots&&!strong.CanEditTestBots,"the host has the tools of a room it does not manage");
                bool added=true;
                As(strong,strong.StartGame);As(strong,()=>added=strong.AddTestBot(0));As(strong,strong.FillRoomWithBots);As(strong,strong.ClearRoomBots);Step(1);
                Check(!added&&clients.All(c=>!c.Started&&c.Roster.Count==3),"the host started or filled a room it does not manage");
                Check(creator.CanStartGame,"the manager cannot start during the handover");
                As(creator,creator.StartGame);Step(2);
                Check(Owner(strong)==creator.Self&&clients.All(c=>!c.Started&&c.Roster.Count==3),"a start before the room was the new host's");
                Step(30);
                Check(Owner(strong)==strong.Self&&clients.All(c=>c.Started&&c.Host==strong.Self&&c.Roster.Count==3),"the manager's start was lost in the handover");});
            // R503 [5]: a successor whose own connection dropped took the match from a host everyone else still heard.
            Test("A successor that alone stops hearing the host does not take over; once another player does not hear it either, it does",()=>{
                Room(new[]{900,700,500});var a=clients[0];var b=clients[1];var c=clients[2];As(a,a.StartGame);
                foreach(var x in clients)As(x,()=>x.ReportLoading(100));Step(40);
                Check(a.IsHost&&clients.All(x=>x.Started)&&RoomData(a,"successors").StartsWith(b.Self+","),"setup: "+RoomData(a,"successors"));
                // Only B's own connection is down: the host and C go on, and C still hears the host.
                for(int t=0;t<30;t++){As(b,()=>b.ReportHostSilence(5));Step(1);}
                Check(clients.All(x=>x.Host==a.Self&&x.Epoch==1)&&!b.IsHost,"a successor's own outage took the host");
                // B hears the host again; then the host really goes quiet, for B and for C.
                As(b,()=>b.ReportHostSilence(0));Step(2);
                for(int t=0;t<4;t++){As(b,()=>b.ReportHostSilence(5));As(c,()=>c.ReportHostSilence(5));Step(1);}
                Check(b.IsHost&&c.Host==b.Self&&a.Host==b.Self&&!a.IsHost,"a host nobody hears was not replaced");});
            // R503 [7]: a room joined by its number that could not take the party sent it on to public matchmaking.
            Test("A party that joins a room by its number goes back with the reason when the room cannot take it, never to a public search",()=>{
                // A public waiting room where no team has two seats left.
                Setup(3);var host=clients[0];var leader=clients[1];var member=clients[2];JoinParty(2,1);
                As(host,()=>host.FindMatch());Step(20);
                for(int i=0;i<10;i++)As(host,()=>host.AddRoomBot());Step(2);
                Check(host.Roster.Count==11&&!host.PrivateRoom&&!host.Started,"setup: eleven in a public waiting room");
                As(leader,()=>leader.JoinPrivateMatch(host.Match));Step(30);
                Check(leader.Match==0&&member.Match==0&&!leader.Busy&&leader.Error.Contains("자리"),"the party went on to public matchmaking: "+leader.Status+" / "+leader.Error);
                Check(FakeSteam.Lobbies.Values.Count(l=>l.Data.TryGetValue("kind",out var k)&&k=="match")==1&&host.Roster.Count==11,"another room was made, or the host's changed");
                // Release builds: a party with bots, at a public room's number.
                foreach(var x in clients.ToArray())As(x,x.Dispose);
                Setup(2,i=>new SteamSession("r",false));host=clients[0];var guest=clients[1];
                As(host,()=>host.FindMatch());Step(20);
                As(guest,()=>guest.SetPartyBots(2));As(guest,()=>guest.JoinPrivateMatch(host.Match));Step(30);
                Check(guest.Match==0&&!guest.Busy&&guest.Error.Contains("봇")&&host.Roster.Count==1,"bots came to a public room by its number: "+guest.Status+" / "+guest.Error);
                Check(FakeSteam.Lobbies.Values.Count(l=>l.Data.TryGetValue("kind",out var k)&&k=="match")==1,"a public room was made for the bots");});
            // R503 [8]: a waiting room that closed took every other party in it out of the queue.
            Test("A waiting room that ends sends the other parties in it on to another room, not back to the lobby",()=>{
                Setup(3);var host=clients[0];var leader=clients[1];var member=clients[2];JoinParty(2,1);
                As(host,()=>host.FindMatch());Step(20);
                As(leader,()=>leader.FindMatch());Step(40);
                ulong room=host.Match;
                Check(room!=0&&leader.Match==room&&member.Match==room&&host.Roster.Count==3&&host.IsHost&&!host.Started,"setup: one public waiting room");
                FakeSteam.Crash(host.Self);clients.Remove(host);Step(60);
                Check(leader.Searching&&leader.Error==""&&member.Error==""&&leader.Match!=0&&leader.Match!=room&&member.Match==leader.Match,
                      "the party left the queue with the room: "+leader.Status+" / "+leader.Error+" / "+member.Error);});
            // R503 [9]: a short drop of Steam's connection threw the player out of a live match at once, and Cancel ran on every poll.
            Test("A short Steam logoff is waited out; a long one leaves the match, once",()=>{
                Room(new[]{900,700});var a=clients[0];var b=clients[1];As(a,a.StartGame);Step(20);
                Check(clients.All(x=>x.Started)&&a.IsHost,"setup");
                FakeSteam.LoggedOff.Add(b.Self);Step(10);
                Check(b.Started&&b.Match==a.Match&&b.Error==""&&b.Status.Contains("Steam"),"a short logoff threw the player out: "+b.Status+" / "+b.Error);
                FakeSteam.LoggedOff.Remove(b.Self);Step(4);
                Check(b.Started&&b.Match==a.Match&&a.Roster.Count==2&&!b.Status.Contains("끊겼"),"the match did not go on after the logoff: "+b.Status);
                int changes=0;b.SessionChanged+=()=>changes++;
                FakeSteam.LoggedOff.Add(b.Self);Step((int)(SteamSession.LogoffGrace/.3f)+4);
                Check(b.Match==0&&!b.Started&&b.Error.Contains("Steam")&&a.Started&&a.Roster.Count==1,"a long logoff kept the player in the match");
                int after=changes;Step(20);
                Check(after>0&&changes==after,"the match was left again and again while logged off: "+changes);});
            // R504: a next host in line that never stepped in (frozen, still loading) ended the match for everyone after it.
            Test("A next host in line that never steps in is passed over by the one after it, and follows it once it runs again",()=>{
                Room(new[]{900,700,500,400});var a=clients[0];var b=clients[1];var c=clients[2];var d=clients[3];As(a,a.StartGame);
                foreach(var x in clients)As(x,()=>x.ReportLoading(100));Step(40);
                Check(a.IsHost&&clients.All(x=>x.Started)&&RoomData(a,"successors").StartsWith(b.Self+","+c.Self),"setup: "+RoomData(a,"successors"));
                // The host's game dies while B, next in line, is frozen: it stays in the room and runs nothing.
                clients.Remove(b);FakeSteam.Crash(a.Self);clients.Remove(a);Step(4);
                Check(!c.IsHost&&c.Host==a.Self&&d.Host==a.Self&&c.Started&&d.Started,"the one after the next in line did not give it its time");
                Step(12);
                Check(c.IsHost&&d.Host==c.Self&&d.Started&&c.Error==""&&d.Error=="","the frozen next in line ended the match: "+d.Status+" / "+d.Error);
                clients.Add(b);Step(4);
                Check(b.Host==c.Self&&!b.IsHost&&c.IsHost&&d.Host==c.Self&&b.Started,"the one passed over took the match back");
                // Again with the host still in the room, silent for everyone, and B frozen behind it.
                Room(new[]{900,700,500,400});a=clients[0];b=clients[1];c=clients[2];d=clients[3];As(a,a.StartGame);
                foreach(var x in clients)As(x,()=>x.ReportLoading(100));Step(40);
                clients.Remove(b);
                // Only C's own connection is down: still nobody passes anyone over (R503 [5]).
                for(int t=0;t<30;t++){As(c,()=>c.ReportHostSilence(5));Step(1);}
                Check(clients.All(x=>x.Host==a.Self)&&a.IsHost,"a successor's own outage passed the next in line over");
                for(int t=0;t<4;t++){As(c,()=>c.ReportHostSilence(5));As(d,()=>d.ReportHostSilence(5));Step(1);}
                Check(a.IsHost&&!c.IsHost,"passed over before the next in line had its time");
                for(int t=0;t<12;t++){As(c,()=>c.ReportHostSilence(5));As(d,()=>d.ReportHostSilence(5));Step(1);}
                Check(c.IsHost&&d.Host==c.Self&&a.Host==c.Self&&!a.IsHost,"a silent host with a frozen next in line was not replaced");});
            // R504: a match decided while Steam was still moving the room to its new host was never marked over.
            Test("A match decided before Steam has given the new host the room is marked over once it has, and offers no seats",()=>{
                Room(new[]{900,500,700});var a=clients[0];var b=clients[1];var c=clients[2];As(a,a.StartGame);Step(20);
                Check(a.IsHost&&clients.All(x=>x.Started)&&RoomData(a,"successors").StartsWith(c.Self+","),"setup: "+RoomData(a,"successors"));
                FakeSteam.OwnerDelay=5;FakeSteam.Crash(a.Self);clients.Remove(a);Step(2);
                Check(c.IsHost&&Owner(c)==b.Self,"setup: the new host before the room is its own");
                As(c,c.EndMatch);Step(1);
                Check(RoomData(c,"ended")!="1","setup: the end was written before the room was the new host's");
                Step(30);
                Check(Owner(c)==c.Self&&RoomData(c,"ended")=="1"&&RoomData(c,"open")=="0","the end was lost with the room's move: ended="+RoomData(c,"ended")+" open="+RoomData(c,"open"));});
            // R504: accepting an old invite to one's own party left it and joined it again, and a leader lost the lead.
            Test("An invite to the party one is already in changes nothing, and the leader keeps the lead",()=>{
                Setup(2);JoinParty(1,0);var leader=clients[0];var member=clients[1];ulong party=leader.Party;
                Check(party!=0&&member.Party==party&&leader.IsLeader&&!member.IsLeader,"setup: a party of two");
                As(leader,()=>leader.JoinParty(party));Step(10);
                Check(leader.Party==party&&leader.IsLeader&&member.Party==party&&!member.IsLeader&&leader.Error==""&&member.Error=="",
                      "the leader lost the lead to its own party's invite: owner "+FakeSteam.Lobbies[party].Owner);});
            Test("A player who leaves a started match leaves a seat on its team, and a searching player takes it",()=>{
                Setup(3);var host=clients[0];var leaver=clients[1];var joiner=clients[2];
                As(host,()=>host.FindMatch());Step(20);As(leaver,()=>leaver.FindMatch());Step(20);
                Check(leaver.Match==host.Match&&host.Roster.Count==2,"setup: one waiting room");
                As(host,host.FillRoomWithBots);Step(5);
                Check(host.Started&&leaver.Started&&RoomData(host,"seats")=="6,6"&&RoomData(host,"open")=="0"&&!FakeSteam.Lobbies[host.Match].Joinable,"a full match should start closed");
                Check(!leaver.Note.Contains("빈자리"),"players who came with the start were announced as filling seats: "+leaver.Note);
                var seat=host.Roster[leaver.Self];
                As(leaver,leaver.Cancel);Step(5);
                Check(host.Roster.Count==11&&RoomData(host,"open")=="1"&&RoomData(host,"free"+seat.Team)=="1"&&RoomData(host,"free"+(1-seat.Team))=="0"&&FakeSteam.Lobbies[host.Match].Joinable,"the leaver's seat is not offered");
                Check(host.EmptySeats==1&&host.Status.Contains("빈자리"),"the match does not show its empty seat: "+host.Status);
                As(joiner,()=>joiner.FindMatch());
                for(int t=0;t<40&&!joiner.Started;t++){Step(1);Check(!joiner.Started||joiner.Roster.ContainsKey(joiner.Self),"in the match before being seated");}
                Check(joiner.Match==host.Match&&joiner.Started&&host.Roster.Count==12,"the searching player did not take the seat");
                var taken=host.Roster[joiner.Self];Check(taken.Team==seat.Team&&taken.Slot==seat.Slot,"the seat went elsewhere");
                Check(host.Note.Contains("빈자리를 채웠"),"no note for the new player: "+host.Note);
                Step(2);Check(RoomData(host,"open")=="0"&&!FakeSteam.Lobbies[host.Match].Joinable&&joiner.Roster.Count==12,"a refilled match is still offered");});
            Test("A decided race offers no empty seats: players leaving for the lobby are not replaced",()=>{
                Setup(3);var host=clients[0];var leaver=clients[1];var joiner=clients[2];
                As(host,()=>host.FindMatch());Step(20);As(leaver,()=>leaver.FindMatch());Step(20);
                As(host,host.FillRoomWithBots);Step(5);
                Check(host.Started&&leaver.Started,"setup: a started match");
                As(leaver,leaver.EndMatch);Step();
                Check(!leaver.MatchEnded&&RoomData(host,"ended")!="1","only the host ends a match");
                As(host,host.EndMatch);Step(2);
                Check(host.MatchEnded&&leaver.MatchEnded,"everyone in the room sees it ended");
                As(leaver,leaver.Cancel);Step(5);
                Check(host.Roster.Count==11&&RoomData(host,"open")=="0"&&!FakeSteam.Lobbies[host.Match].Joinable,"an ended match offered the leaver's seat");
                As(joiner,()=>joiner.FindMatch());Step(40);
                Check(joiner.Match!=host.Match,"a searching player was let into an ended match");});
            Test("Each team refills only to the size it started with: a 5 v 5 match takes one player back, not two",()=>{
                Setup(4);var host=clients[0];var leaver=clients[1];var first=clients[2];var second=clients[3];
                As(host,()=>host.FindMatch(true));Step();As(leaver,()=>leaver.JoinPrivateMatch(host.Match));Step(20);
                for(int i=0;i<4;i++){As(host,()=>host.AddTestBot(0));As(host,()=>host.AddTestBot(1));}
                As(host,host.StartGame);Step(5);
                Check(host.Started&&leaver.Started&&RoomData(host,"seats")=="5,5"&&RoomData(host,"open")=="0","setup: a closed 5 v 5");
                Check(!leaver.Note.Contains("빈자리"),"dummies added before the start were announced as filling seats: "+leaver.Note);
                int lost=host.Roster[leaver.Self].Team;
                As(leaver,leaver.Cancel);Step(5);
                Check(RoomData(host,"open")=="1"&&RoomData(host,"free"+lost)=="1"&&RoomData(host,"free"+(1-lost))=="0","a 5 v 5 match should offer exactly one seat");
                As(first,()=>first.JoinPrivateMatch(host.Match));Step(20);
                Check(first.Started&&host.Roster[first.Self].Team==lost&&host.Roster.Values.Count(p=>p.Team==lost)==5,"the empty seat was not refilled");
                Check(RoomData(host,"open")=="0"&&RoomData(host,"free0")=="0"&&RoomData(host,"free1")=="0","a refilled 5 v 5 still offers a seat");
                As(second,()=>second.JoinPrivateMatch(host.Match));Step(20);
                Check(second.Match==0&&!second.Started&&host.Roster.Count==10,"a sixth player got into a 5 v 5 team");});
            Test("A party that joins a started match sits together, and its held seat outlives a host change",()=>{
                Setup(6);var fit=new[]{900,500,800,400,300,200};for(int i=0;i<6;i++)clients[i].LocalFitness=fit[i];
                var host=clients[0];var next=clients[2];var leader=clients[4];var friend=clients[5];
                JoinParty(5,4);As(host,()=>host.FindMatch(true));Step();
                for(int i=1;i<=3;i++){var c=clients[i];As(c,()=>c.JoinPrivateMatch(host.Match));}
                Step(20);Check(host.Roster.Count==4,"setup: four solo players");
                As(host,host.StartGame);Step(10);Check(host.IsHost&&RoomData(host,"seats")=="2,2","setup: 2 v 2, the fittest hosts");
                int lost=host.Roster[clients[1].Self].Team;Check(host.Roster[clients[3].Self].Team==lost,"setup: both leavers on one team");
                foreach(var c in new[]{clients[1],clients[3]})As(c,c.Cancel);
                Step(5);Check(RoomData(host,"free"+lost)=="2","two empty seats not offered");
                As(leader,()=>leader.JoinPrivateMatch(host.Match));
                for(int t=0;t<40&&RoomData(host,"held")=="";t++)Step(1);
                Check(host.Roster.ContainsKey(leader.Self)&&RoomData(host,"held").StartsWith(friend.Self+":"),"the friend's seat is not held: "+RoomData(host,"held"));
                FakeSteam.Crash(host.Self);clients.Remove(host);Step(20);
                Check(next.IsHost&&leader.Started&&friend.Started,"the party did not get in after the host change");
                Check(next.Roster[leader.Self].Team==lost&&next.Roster[friend.Self].Team==lost&&next.Roster.Count==3&&RoomData(next,"held")=="","party split or held seat lost");});
            Test("Empty seats are offered only for four minutes after the shared start",()=>{
                Room(new[]{900,500,400});var host=clients[0];As(host,host.StartGame);Step(5);
                As(host,()=>host.AnnounceStart(SteamUtils.GetServerRealTime()));Step();
                As(clients[2],clients[2].Cancel);Step(5);Check(RoomData(host,"open")=="1","seat not offered within the window");
                Step((int)(Backfill.OpenSeconds/.3)+5);
                Check(RoomData(host,"open")=="0"&&!FakeSteam.Lobbies[host.Match].Joinable&&host.EmptySeats==0,"seat still offered after the window");});
            // Work order [4] (11-02): a modified client declares made-up party members. They hold their seats for 25 s (and keep a
            // waiting room from starting), and the client used to ask again with new ones each time they ran out.
            Test("Made-up party members hold waiting-room seats once: their sender waits out a cooldown while a real party and the bots go on",()=>{
                Setup(3);var host=clients[0];var leader=clients[1];var friend=clients[2];
                JoinParty(2,1);As(leader,()=>leader.SetPartyBots(1));As(host,()=>host.FindMatch(true));Step();
                ulong cheat=99;FakeSteam.Lobbies[host.Match].Members.Add(cheat);
                Check(Ask(host,cheat,"a1",cheat,501,502,503,504,505)=="a1|ok"&&host.Roster.ContainsKey(cheat)&&host.TeamCount(host.Roster[cheat].Team)==6&&!host.CanStartGame,
                      "setup: six seats held and the room cannot start");
                Check(Ask(host,cheat,"a2",cheat,601)=="a2|full","a second group from the same sender");
                Step((int)(26/.3));
                Check(RoomData(host,"grant_"+cheat)=="a1|expired"&&!host.Roster.ContainsKey(cheat)&&host.TeamCount(0)+host.TeamCount(1)==1,"setup: the made-up party ran out");
                Check(Ask(host,cheat,"a3",cheat,701,702,703,704,705)=="a3|full"&&Ask(host,cheat,"a4",cheat)=="a4|full","asked again during the cooldown");
                As(leader,()=>leader.JoinPrivateMatch(host.Match));Step(30);
                Check(host.Roster.ContainsKey(leader.Self)&&host.Roster.ContainsKey(friend.Self)&&host.Roster[friend.Self].Team==host.Roster[leader.Self].Team&&
                      host.Roster.Values.Count(p=>BotIdentity.IsBot(p.Id)&&p.Team==host.Roster[leader.Self].Team)==1,"a real party with its bot was refused or split");
                bool ok=false;As(host,()=>ok=host.AddTestBot(1-host.Roster[host.Self].Team));
                Check(ok&&host.CanStartGame,"the host's bots or the start were held up");
                Step((int)(TeamReservations.Cooldown/.3));
                Check(Ask(host,cheat,"a5",cheat)=="a5|ok"&&host.Roster.ContainsKey(cheat),"the cooldown never ended");});
            // The same in a started match (Backfill): the sender sits down at once, so it has to leave and come back to ask
            // again, while the seat it asked for is still held; then nobody comes for that seat.
            Test("Empty seats of a started match: one group on the way per sender, a cooldown when nobody came, and a real party still sits down",()=>{
                Setup(8);var host=clients[0];var leader=clients[6];var friend=clients[7];
                JoinParty(7,6);As(host,()=>host.FindMatch(true));Step();
                for(int i=1;i<=5;i++){var c=clients[i];As(c,()=>c.JoinPrivateMatch(host.Match));}
                Step(20);Check(host.Roster.Count==6,"setup: six solo players");
                As(host,host.StartGame);Step(5);Check(host.Started&&host.IsHost&&RoomData(host,"seats")=="3,3","setup: 3 v 3");
                var leavers=clients.Skip(1).Take(5).GroupBy(c=>host.Roster[c.Self].Team).SelectMany(g=>g.Take(2)).ToList();
                foreach(var c in leavers)As(c,c.Cancel);
                Step(5);Check(leavers.Count==4&&RoomData(host,"free0")=="2"&&RoomData(host,"free1")=="2","setup: two empty seats a side");
                var room=FakeSteam.Lobbies[host.Match];ulong cheat=99;room.Members.Add(cheat);
                Check(Ask(host,cheat,"b1",cheat,501)=="b1|ok"&&host.Roster.ContainsKey(cheat)&&RoomData(host,"held").StartsWith("501:"),"setup: the sender sits down and holds a seat");
                Ask(host,cheat,"b2",cheat,601);
                Check(host.Roster.Count==3&&!RoomData(host,"held").Contains("601:"),"a seated sender got more seats");
                room.Members.Remove(cheat);Step(2);Check(!host.Roster.ContainsKey(cheat),"setup: the sender left its seat");
                room.Members.Add(cheat);
                Check(Ask(host,cheat,"b3",cheat,701)=="b3|full"&&!host.Roster.ContainsKey(cheat)&&!RoomData(host,"held").Contains("701:"),"a second group from the same sender while its first is held");
                Step((int)(Backfill.HoldSeconds/.3)+2);Check(RoomData(host,"held")=="","setup: nobody came for the held seat");
                Check(Ask(host,cheat,"b4",cheat)=="b4|full"&&!host.Roster.ContainsKey(cheat),"asked again during the cooldown");
                As(leader,()=>leader.JoinPrivateMatch(host.Match));Step(30);
                Check(leader.Started&&friend.Started&&host.Roster[friend.Self].Team==host.Roster[leader.Self].Team,"a real party was refused or split");
                Step((int)(TeamReservations.Cooldown/.3));
                Check(Ask(host,cheat,"b5",cheat)=="b5|ok"&&host.Roster.ContainsKey(cheat),"the cooldown never ended");});
            Test("In a started match a party member who leaves goes alone, and the party gathers again afterwards",()=>{
                Setup(3);var leader=clients[0];var member=clients[1];var solo=clients[2];
                JoinParty(1,0);As(leader,()=>leader.FindMatch(true));Step(20);
                As(solo,()=>solo.JoinPrivateMatch(leader.Match));Step(20);
                Check(leader.Roster.Count==3,"setup: a party of two and a solo player");
                As(leader,leader.StartGame);Step(5);Check(clients.All(c=>c.Started),"setup: started");ulong room=leader.Match;
                As(member,member.Cancel);Step(20);
                Check(member.Match==0&&member.Party==leader.Party&&member.WaitingForParty,"the member did not simply step out");
                Check(leader.Match==room&&leader.Started&&solo.Match==room&&solo.Started&&leader.Roster.Count==2,"the member's leaving took others with it");
                Check(RoomData(leader,"open")=="1","the member's seat is not offered");
                As(leader,leader.Cancel);Step(20);
                Check(leader.Match==0&&!member.WaitingForParty&&!member.Busy&&member.Party==leader.Party,"the party did not gather again");
                Check(solo.Match==room&&solo.Started&&solo.IsHost,"the match ended with the party leader");});
            Test("A host who leads a party leaves a started match alone: hosting moves on and the party waits for its member",()=>{
                Setup(3);var leader=clients[0];var member=clients[1];var solo=clients[2];
                JoinParty(1,0);As(leader,()=>leader.FindMatch(true));Step(20);
                As(solo,()=>solo.JoinPrivateMatch(leader.Match));Step(20);
                As(leader,leader.StartGame);Step(5);Check(leader.IsHost,"setup: the leader hosts");ulong room=leader.Match;
                As(leader,leader.Cancel);Step(20);
                Check(leader.Match==0&&member.Match==room&&member.Started&&solo.Started,"the party left with its leader");
                Check((member.IsHost||solo.IsHost)&&member.Host==solo.Host&&member.Host!=leader.Self,"the host role did not move on");
                Check(leader.PartyStillPlaying,"the leader does not see its member still playing");
                As(leader,()=>leader.FindMatch());Check(!leader.Searching&&leader.Error.Contains("경기 중"),"the leader queued a member who is still playing");
                As(member,member.Cancel);Step(20);
                Check(!leader.PartyStillPlaying&&!member.Busy,"the member is not back");
                As(leader,()=>leader.FindMatch(true));Step(20);Check(leader.Match!=0&&member.Match==leader.Match,"the party did not queue together again");});
            Test("A party leader's crash does not pull its party out of a started match",()=>{
                Setup(3);var fit=new[]{900,700,500};for(int i=0;i<3;i++)clients[i].LocalFitness=fit[i];
                var leader=clients[0];var member=clients[1];var solo=clients[2];
                JoinParty(1,0);As(leader,()=>leader.FindMatch(true));Step(20);
                As(solo,()=>solo.JoinPrivateMatch(leader.Match));Step(20);
                As(leader,leader.StartGame);Step(10);ulong room=leader.Match;
                FakeSteam.Crash(leader.Self);clients.Remove(leader);Step(20);
                Check(member.Match==room&&member.Started&&solo.Match==room&&solo.Started,"the crash ended the match for the party");
                Check(member.IsLeader&&member.IsHost,"party lead or hosting did not pass on");});
            Test("A party member's crash does not pull the rest of its party out of a started match",()=>{
                Setup(3);var leader=clients[0];var member=clients[1];var solo=clients[2];
                JoinParty(1,0);As(leader,()=>leader.FindMatch(true));Step(20);
                As(solo,()=>solo.JoinPrivateMatch(leader.Match));Step(20);
                As(leader,leader.StartGame);Step(5);ulong room=leader.Match;
                FakeSteam.Crash(member.Self);clients.Remove(member);Step(20);
                Check(leader.Match==room&&leader.Started&&solo.Started&&leader.Roster.Count==2&&RoomData(leader,"open")=="1","a member's crash ended the match for its party");});
            // R529: an invite used to reach the game only once accepted in Steam's overlay.
            Test("A party invite reaches the friend's game: listed with its sender, a party chat line, declined, accepted, expired",()=>{
                Setup(3);var a=clients[0];var b=clients[1];var c=clients[2];
                As(a,()=>a.InviteToParty(b.Self));Step();
                Check(b.Invites.Count==1&&b.Invites[0].From==a.Self&&b.Invites[0].Lobby==a.Party,"the invite did not arrive: "+b.Invites.Count);
                Check(b.Chat.Entries.Any(e=>e.Notice&&e.Text.Contains("파티에 초대했어요")),"no line in the party chat");
                As(b,()=>b.DeclineInvite(a.Party));Check(b.Invites.Count==0,"a declined invite is still listed");
                As(a,()=>a.InviteToParty(b.Self));As(a,()=>a.InviteToParty(b.Self));Step();
                Check(b.Invites.Count==1,"one invite per sender and party: "+b.Invites.Count);
                As(b,()=>b.AcceptInvite(b.Invites[0].Lobby));Step(10);
                Check(b.Party==a.Party&&b.Invites.Count==0&&b.Error=="","accepting did not take b into a's party: "+b.Error);
                As(a,()=>a.InviteToParty(c.Self));Step();
                Check(c.Invites.Count==1,"the second invite did not arrive");
                UnityEngine.Time.realtimeSinceStartup+=SteamSession.InviteLasts+1;Step(1);
                Check(c.Invites.Count==0,"an invite left alone never goes away");});
            Test("Party chat reaches every member under the sender's Steam name, notes joins and leaves, and allows one line a second",()=>{
                Setup(3);JoinParty(1,0);JoinParty(2,0);var leader=clients[0];var member=clients[1];
                Check(leader.Chat.In(ChatChannel.Party).Count(e=>e.Notice&&e.Text.Contains("파티에 들어왔어요"))==2,"joins not noted");
                string said="";As(member,()=>said=member.Say(ChatChannel.Party,"  2랭크까지 같이 가자\n"));Check(said=="","party line refused: "+said);
                As(member,()=>said=member.Say(ChatChannel.Party,"또"));Check(said.Contains("1초"),"a second line within a second went out");
                Step();
                foreach(var c in clients){var line=c.Chat.In(ChatChannel.Party).LastOrDefault(e=>!e.Notice);
                    Check(line!=null&&line.Text=="2랭크까지 같이 가자"&&line.Sender==member.Self&&line.Name=="Player "+member.Self&&line.Mine==(c==member),"party line not shown the same everywhere");}
                Check(leader.Chat.In(ChatChannel.All).Count()==0&&!leader.CanChat(ChatChannel.All)&&!leader.CanChat(ChatChannel.Team),"match channels open without a match");
                As(leader,()=>said=leader.Say(ChatChannel.All,"hi"));Check(said!="","all-chat line sent without a match");
                As(clients[2],clients[2].LeaveParty);Step();
                Check(leader.Chat.In(ChatChannel.Party).Last().Text=="Player 3 님이 파티를 나갔어요"&&!clients[2].Chat.In(ChatChannel.Party).Any(e=>!e.Notice),"leave not noted, or the old party's lines followed the leaver");});
            Test("Team chat shows only on the sender's team, all chat in the whole room, and forged team lines are dropped",()=>{
                Room(new[]{900,800,700,600});var host=clients[0];
                var mates=clients.Where(c=>host.Roster[c.Self].Team==host.Roster[host.Self].Team).ToList();var rivals=clients.Except(mates).ToList();
                Check(mates.Count==2&&rivals.Count==2,"setup: two against two");
                string said="";As(host,()=>said=host.Say(ChatChannel.Team,"왼쪽 길 막혔음"));Check(said=="","team line refused: "+said);Step();
                Check(mates.All(c=>c.Chat.In(ChatChannel.Team).Any(e=>e.Text=="왼쪽 길 막혔음"))&&rivals.All(c=>!c.Chat.In(ChatChannel.Team).Any()),"team line leaked or lost");
                var rival=rivals[0];As(rival,()=>said=rival.Say(ChatChannel.All,"gg"));Step();
                Check(clients.All(c=>c.Chat.In(ChatChannel.All).Any(e=>e.Text=="gg"&&e.Sender==rival.Self)),"all-chat line lost");
                Check(clients.All(c=>!c.Chat.In(ChatChannel.Party).Any(e=>!e.Notice)),"a match line landed in party chat");
                // A modified client on the other team claims to speak for ours.
                int ourTeam=host.Roster[host.Self].Team;byte[] forged=System.Text.Encoding.UTF8.GetBytes(ChatText.Encode(ChatChannel.Team,ourTeam,"가짜"));
                As(rival,()=>SteamMatchmaking.SendLobbyChatMsg(new CSteamID(host.Match),forged,forged.Length));Step();
                Check(clients.All(c=>!c.Chat.In(ChatChannel.Team).Any(e=>e.Text=="가짜")),"forged team line shown");
                // A party line sent into the match room is not party chat.
                byte[] stray=System.Text.Encoding.UTF8.GetBytes(ChatText.Encode(ChatChannel.Party,0,"길 잃은 줄"));
                As(rival,()=>SteamMatchmaking.SendLobbyChatMsg(new CSteamID(host.Match),stray,stray.Length));Step();
                Check(clients.All(c=>!c.Chat.Entries.Any(e=>e.Text=="길 잃은 줄")),"a party line through the match room was shown");
                As(host,host.StartGame);Step(5);Check(clients.All(c=>c.Started),"setup: started");
                var leaver=rivals[1];As(leaver,leaver.Cancel);Step(10);
                Check(clients.Where(c=>c!=leaver).All(c=>c.Chat.In(ChatChannel.All).Any(e=>e.Notice&&e.Text=="Player "+leaver.Self+" 님이 경기를 나갔어요")),"a player leaving the match was not noted");
                Check(!leaver.Chat.In(ChatChannel.All).Any()&&!leaver.Chat.In(ChatChannel.Team).Any(),"the old room's lines followed the leaver");});
            Test("Chosen names travel with the party and the match room, Steam names otherwise",()=>{
                Setup(2);JoinParty(1,0);var a=clients[0];var b=clients[1];
                Check(b.Name(a.Self)=="Player "+a.Self,"no chosen name yet: the Steam name");
                a.LocalName="철벽 룩";b.LocalName="관리자";Step();
                Check(a.Name(a.Self)=="철벽 룩"&&b.Name(a.Self)=="철벽 룩","the chosen name did not reach the party");
                Check(b.LocalName==""&&a.Name(b.Self)=="Player "+b.Self,"an invalid name was taken");
                As(a,()=>a.FindMatch(true));Step(20);Check(a.Match!=0&&b.Match==a.Match,"setup: room");
                Check(FakeSteam.Lobbies[a.Match].MemberData.TryGetValue((a.Self,"nick"),out var nick)&&nick=="철벽 룩","not published in the match room");
                a.LocalName="번개 폰";Step();
                Check(b.Name(a.Self)=="번개 폰","a changed name did not follow");
                a.LocalName="";Step();
                Check(b.Name(a.Self)=="Player "+a.Self,"clearing the name kept the old one");});
            // R533 (user 11-10, the matching rules list). Rules 1 ~ 2 were already how matchmaking worked (R148, R59, R60);
            // these pin them as the user wrote them, in a release build's public matchmaking.
            Test("Matching rules 1 ~ 2: three a side start a six-seat room by the manager's word; a seventh gets its own room; a leaver's seat is refilled",()=>PublicRoomRule(3));
            Test("Matching rule 1-4: four a side make an eight-seat room",()=>PublicRoomRule(4));
            Test("Matching rule 1-4: five a side make a ten-seat room",()=>PublicRoomRule(5));
            Test("Matching rule 1-4: twelve start by themselves, and the room has twelve seats",()=>PublicRoomRule(6));
            Test("The room's manager (rule 4): its maker; handed on by choice; passed on when the manager leaves the waiting room or the match",()=>{
                Room(new[]{900,800,700});var a=clients[0];var b=clients[1];var c=clients[2];
                Check(clients.All(x=>x.Manager==a.Self&&x.Host==a.Self)&&a.IsManager&&!b.IsManager,"setup: the maker manages and hosts");
                Check(a.CanGiveManager(b.Self)&&!b.CanGiveManager(a.Self)&&!a.CanGiveManager(a.Self)&&!a.CanGiveManager(99),"who may hand the role on, and to whom");
                As(b,()=>b.GiveManager(b.Self));As(b,()=>b.GiveManager(c.Self));Step(3);
                Check(clients.All(x=>x.Manager==a.Self),"a player who does not manage handed the role on");
                As(a,()=>a.GiveManager(b.Self));Step(3);
                Check(clients.All(x=>x.Manager==b.Self&&x.Host==a.Self)&&b.IsManager&&!a.IsManager,"the role was not handed on");
                Check(b.Note.Contains("방장")&&c.Note.Contains("방장"),"no word of the new manager: "+b.Note+" / "+c.Note);
                Check(b.CanStartGame&&!a.CanStartGame,"the start did not go with the role");
                As(a,a.StartGame);Step(3);Check(clients.All(x=>!x.Started),"the old manager still starts the room");
                // The manager (not the host) leaves the waiting room: the host names the player seated longest.
                ulong room=a.Match;As(b,b.Cancel);clients.Remove(b);Step(5);
                Check(clients.All(x=>x.Match==room&&x.Manager==a.Self&&x.Roster.Count==2),"the manager's leaving left the room without one: "+a.Manager);
                As(b,b.Dispose);
                As(a,()=>a.GiveManager(c.Self));Step(3);Check(c.IsManager&&c.CanStartGame,"setup: the other player manages");
                As(c,c.StartGame);Step(10);
                Check(clients.All(x=>x.Started&&x.Manager==c.Self&&x.Host==a.Self),"the manager's start, asked of the host");
                // In the match the role stays, can be handed on, and a manager who leaves passes it on.
                As(c,()=>c.GiveManager(a.Self));Step(3);Check(clients.All(x=>x.Manager==a.Self),"handing the role on in the match");
                As(a,a.Cancel);clients.Remove(a);Step(10);
                Check(c.Started&&c.IsHost&&c.IsManager,"a manager who hosts left: the one left should host and manage");
                As(a,a.Dispose);});
            Test("A manager on another PC sees the players still being seated, and its start waits for them (R525 through the manager)",()=>{
                Room(new[]{500,900});var m=clients[0];var h=clients[1];
                Check(m.IsManager&&h.IsHost&&!h.IsManager&&m.CanStartGame,"setup: the maker manages, the better PC hosts");
                FakeSteam.Lobbies[m.Match].Members.Add(99);Step(2);
                Check(h.SeatingJoiners==1&&m.SeatingJoiners==1&&!m.CanStartGame,"the manager does not see the unseated player: "+m.SeatingJoiners);
                Step(30);Check(m.SeatingJoiners==0&&m.CanStartGame,"one who never asks must not hold it for ever");
                As(m,m.StartGame);Step(10);Check(clients.All(c=>c.Started),"the start after the wait");});
            Test("The manager puts a player out of the waiting room: through the host, a party as one, never back into that room; not the host's right, not after the start",()=>{
                Setup(5);var a=clients[0];var h=clients[1];var solo=clients[2];var lead=clients[3];var mate=clients[4];
                a.LocalFitness=500;h.LocalFitness=900;solo.LocalFitness=600;lead.LocalFitness=400;mate.LocalFitness=300;
                JoinParty(4,3);
                As(a,()=>a.FindMatch());Step(20);ulong room=a.Match;
                foreach(var c in new[]{h,solo,lead}){var x=c;As(x,()=>x.FindMatch());Step(20);}
                Check(clients.All(c=>c.Match==room&&c.Roster.Count==5&&c.Host==h.Self&&c.Manager==a.Self),"setup: five in the room, the best PC hosting, its maker managing");
                Check(clients.All(c=>c.TeamCount(0)+c.TeamCount(1)==5),"every PC should count the room's seats alike: "+solo.TeamCount(0)+" + "+solo.TeamCount(1));
                Check(a.CanKick(solo.Self)&&a.CanKick(mate.Self)&&a.CanKick(h.Self)&&!a.CanKick(a.Self)&&!h.CanKick(solo.Self)&&!solo.CanKick(a.Self)&&!lead.CanKick(mate.Self),"who may put whom out");
                As(h,()=>h.Kick(solo.Self));As(solo,()=>solo.Kick(a.Self));Step(3);
                Check(clients.All(c=>c.Match==room&&c.Roster.Count==5),"a player who does not manage put someone out");
                // One of a party of two: the party goes as one, and both are told why.
                As(a,()=>a.Kick(mate.Self));Step(6);
                Check(lead.Match==0&&mate.Match==0&&!lead.Searching&&lead.Error==SteamSession.KickedMessage&&mate.Error==SteamSession.KickedMessage,"the party was not put out: "+lead.Error+" / "+mate.Error);
                Check(new[]{a,h,solo}.All(c=>c.Match==room&&c.Roster.Count==3),"the room after it");
                // Back by matchmaking: this room is skipped, they get a room of their own.
                As(lead,()=>lead.FindMatch());Step(40);
                Check(lead.Match!=0&&lead.Match!=room&&mate.Match==lead.Match&&a.Roster.Count==3,"the party came back into the room it was put out of");
                As(lead,lead.Cancel);Step(5);
                // The room's number does not get them in either.
                As(lead,()=>lead.JoinPrivateMatch(room));Step(20);
                Check(lead.Match==0&&mate.Match==0&&a.Roster.Count==3,"the room's number let them back in");
                // The host itself can be put out: it hands the room on to the next best PC.
                As(a,()=>a.Kick(h.Self));Step(20);
                Check(h.Match==0&&h.Error==SteamSession.KickedMessage,"the host was not put out: "+h.Error);
                Check(new[]{a,solo}.All(c=>c.Match==room&&c.Roster.Count==2&&c.Host==solo.Self&&c.Manager==a.Self),"the room did not go on under the next best PC");
                // Not once the match has started.
                As(a,()=>a.AddRoomBot());As(a,()=>a.AddRoomBot());Step(6);
                Check(a.Roster.Count==4&&a.CanStartGame,"setup: two a side with bots: "+a.Roster.Count);
                As(a,a.StartGame);Step(10);Check(a.Started&&solo.Started,"setup: started");
                Check(!a.CanKick(solo.Self),"a player can be put out of a running match");
                As(a,()=>a.Kick(solo.Self));Step(4);Check(solo.Started&&a.Roster.ContainsKey(solo.Self),"a player was put out of a running match");});
            Test("A manager that crashes: the host names the next one, in the waiting room and in the match",()=>{
                Room(new[]{500,900,700});var a=clients[0];var h=clients[1];var c=clients[2];
                Check(clients.All(x=>x.Manager==a.Self&&x.Host==h.Self),"setup: the maker manages, the best PC hosts");
                FakeSteam.Crash(a.Self);clients.Remove(a);Step(10);
                Check(clients.All(x=>x.Match!=0&&x.Manager!=0&&x.Manager!=a.Self&&x.Manager==h.Manager)&&clients.Count(x=>x.IsManager)==1,"no manager after the manager crashed: "+h.Manager);
                var m=clients.First(x=>x.IsManager);As(m,m.StartGame);Step(10);
                Check(clients.All(x=>x.Started),"the new manager cannot start the room");
                var left=clients.First(x=>x!=m);FakeSteam.Crash(m.Self);clients.Remove(m);Step(20);
                Check(left.Started&&left.IsManager&&left.IsHost,"the last player should manage and host: manager "+left.Manager+" host "+left.Host);});
            Test("Deserters (rule 3): leaving a public match under way is counted; the third stops matchmaking 10 minutes, then 30, then an hour; a day clears it",()=>{
                Setup(2);var a=clients[0];var b=clients[1];var store=new Dictionary<ulong,string>();
                Penalised(a,store);Penalised(b,store);
                // Leaving a room that has not started costs nothing.
                As(a,()=>a.FindMatch());Step(20);As(a,a.LeaveByChoice);Step(5);
                Check(a.Match==0&&a.Desertions==0&&a.Error=="","cancelling a waiting room was counted: "+a.Error);
                // The first and the second: written down, nothing stopped.
                Duel(a,b);
                Check(a.WouldDesert&&a.LeaveWarning.Contains("1회"),"the menu's warning: "+a.LeaveWarning);
                As(a,a.LeaveByChoice);Step(5);
                Check(a.Match==0&&a.Desertions==1&&a.PenaltySeconds==0&&a.Error.Contains("탈주 1회")&&store[a.Self]!="","the first desertion: "+a.Error);
                // The one left alone leaves: there is nobody to desert.
                Check(b.Started&&!b.WouldDesert&&b.LeaveWarning=="","alone in the match");
                As(b,b.LeaveByChoice);Step(5);Check(b.Desertions==0&&b.Error=="","leaving an empty match was counted");
                Duel(a,b);As(a,a.LeaveByChoice);Step(5);As(b,b.Cancel);Step(5);
                Check(a.Desertions==2&&a.PenaltySeconds==0&&a.PenaltyNotice=="","the second desertion");
                // The third: ten minutes.
                Duel(a,b);Check(a.LeaveWarning.Contains("10분"),"the warning before the third: "+a.LeaveWarning);
                As(a,a.LeaveByChoice);Step(5);As(b,b.Cancel);Step(5);
                Check(a.Desertions==3&&a.PenaltySeconds>590&&a.PenaltySeconds<=600&&a.Error.Contains("10분"),"the third desertion: "+a.PenaltySeconds+" "+a.Error);
                As(a,()=>a.FindMatch());Step(5);
                Check(!a.Searching&&a.Match==0&&a.Error.Contains("정지")&&a.PenaltyNotice.Contains("정지"),"matchmaking was not stopped: "+a.Error);
                // A private room is not matchmaking.
                As(a,()=>a.FindMatch(true));Step(5);Check(a.Match!=0&&a.PrivateRoom,"a private room was stopped too");As(a,a.Cancel);Step(5);
                // A public room's number is no way round it.
                As(b,()=>b.FindMatch());Step(20);ulong room=b.Match;As(a,()=>a.JoinPrivateMatch(room));Step(20);
                Check(room!=0&&a.Match==0&&a.Error.Contains("정지")&&b.Roster.Count==1,"a room's number got round the stop: "+a.Error);
                As(b,b.Cancel);Step(5);
                UnityEngine.Time.realtimeSinceStartup+=600;Step(2);
                Check(a.PenaltySeconds==0&&a.PenaltyNotice=="","the stop did not end");
                // The fourth: thirty minutes. The fifth and the sixth: an hour.
                foreach(int minutes in new[]{30,60,60})
                {
                    Duel(a,b);As(a,a.LeaveByChoice);Step(5);As(b,b.Cancel);Step(5);
                    Check(Math.Abs(a.PenaltySeconds-minutes*60)<15,"expected "+minutes+" minutes, got "+a.PenaltySeconds);
                    UnityEngine.Time.realtimeSinceStartup+=minutes*60;Step(2);
                }
                Check(a.Desertions==6&&a.PenaltySeconds==0,"six in the day: "+a.Desertions);
                // A day on, nothing counts any more.
                UnityEngine.Time.realtimeSinceStartup+=(float)LeavePenalty.Window;Step(2);
                Check(a.Desertions==0&&a.PenaltySeconds==0,"a day did not clear it");
                Duel(a,b);As(a,a.LeaveByChoice);Step(5);As(b,b.Cancel);Step(5);
                Check(a.Desertions==1&&a.PenaltySeconds==0,"after a day the count starts over: "+a.Desertions);
                // Development builds: the count can be wiped (to test the rule itself).
                As(a,a.ClearLeavePenalty);Check(a.Desertions==0&&store[a.Self]=="","the wipe");});
            Test("A party does not search while one of its members' matchmaking is stopped; a private room still takes it",()=>{
                Setup(2);var lead=clients[0];var mate=clients[1];var store=new Dictionary<ulong,string>();
                double now=SteamUtils.GetServerRealTime();
                store[mate.Self]=LeavePenalty.Encode(new double[]{now-30,now-20,now-10});Penalised(mate,store);
                Check(mate.Desertions==3&&mate.PenaltySeconds>580,"setup: a member with a stop running: "+mate.PenaltySeconds);
                JoinParty(1,0);Step(4);
                As(lead,()=>lead.FindMatch());Step(5);
                Check(!lead.Searching&&lead.Match==0&&lead.Error.Contains("파티원")&&lead.Error.Contains("정지")&&lead.PenaltyNotice.Contains("파티원"),"the party searched with a stopped member: "+lead.Error);
                As(lead,()=>lead.FindMatch(true));Step(10);Check(lead.Match!=0&&mate.Match==lead.Match,"a private room should still take the party");
                As(lead,lead.Cancel);Step(5);
                UnityEngine.Time.realtimeSinceStartup+=600;Step(4);
                Check(lead.PenaltyNotice=="","the party's stop did not end with its member's");
                As(lead,()=>lead.FindMatch());Step(5);Check(lead.Searching||lead.Match!=0,"the party still cannot search after the stop: "+lead.Error);});
            Test("What is not a desertion: a match that is over, a private room, an error, a plain leave, the Editor; closing the game in a match is one",()=>{
                Setup(2);var a=clients[0];var b=clients[1];var store=new Dictionary<ulong,string>();Penalised(a,store);Penalised(b,store);
                Duel(a,b);As(a,a.EndMatch);Step(3);Check(a.MatchEnded&&b.MatchEnded&&!b.WouldDesert,"setup: a decided match");
                As(b,b.LeaveByChoice);Step(5);As(a,a.LeaveByChoice);Step(5);Check(a.Desertions==0&&b.Desertions==0,"leaving a decided match was counted");
                As(a,()=>a.FindMatch(true));Step();As(b,()=>b.JoinPrivateMatch(a.Match));Step(20);As(a,a.StartGame);Step(5);
                Check(a.Started&&b.Started&&!b.WouldDesert,"setup: a private match");
                As(b,b.LeaveByChoice);Step(5);As(a,a.Cancel);Step(5);Check(b.Desertions==0,"a private room was counted");
                Duel(a,b);As(b,()=>b.Abort("연결이 끊어졌습니다."));Step(5);As(a,a.Cancel);Step(5);
                Check(a.Desertions==0&&b.Desertions==0,"an error or a plain leave was counted");
                // A player who saw the result may go before the room's own mark arrives (only its owner writes it).
                Duel(a,b);Check(b.WouldDesert,"setup: a match under way");
                As(b,b.SawResult);Check(!b.MatchEnded&&!b.WouldDesert&&b.LeaveWarning=="","a player who saw the result still deserts");
                As(b,b.LeaveByChoice);Step(5);As(a,a.Cancel);Step(5);Check(b.Desertions==0,"leaving after the result was counted");
                b.LeavePenaltyOn=false;Duel(a,b);Check(!b.WouldDesert&&b.LeaveWarning=="","the Editor counts");
                As(b,b.LeaveByChoice);Step(5);As(a,a.Cancel);Step(5);
                b.LeavePenaltyOn=true;Check(b.Desertions==0,"the Editor wrote a desertion down");
                Duel(a,b);As(b,b.Dispose);clients.Remove(b);Step(5);
                Check(store.TryGetValue(b.Self,out var kept)&&LeavePenalty.Decode(kept).Count==1,"closing the game in a match was not counted");});
            // R536 (user 11-10, rule 4 as written, "바로 넘기기"): in a running match too the best PC hosts whenever the
            // players in it change.
            Test("Rule 4 in a running match: a stronger PC that takes an empty seat takes the host role once its scene has been up a moment, the manager stays; a PC of the same class does not",()=>{
                foreach(int late in new[]{1300,950})
                {
                    Setup(4);var a=clients[0];var b=clients[1];var c=clients[2];var n=clients[3];
                    a.LocalFitness=900;b.LocalFitness=700;c.LocalFitness=500;n.LocalFitness=late;
                    As(a,()=>a.FindMatch(true));Step();As(b,()=>b.JoinPrivateMatch(a.Match));As(c,()=>c.JoinPrivateMatch(a.Match));Step(20);
                    Check(a.Roster.Count==3&&a.IsHost&&a.IsManager,"setup: a room of three, made by the best PC");
                    As(a,a.StartGame);Step(5);foreach(var x in new[]{a,b,c}){var y=x;As(y,()=>y.ReportLoading(100));}
                    Step(60);
                    Check(a.Started&&a.StartAt>0&&clients.Take(3).All(x=>x.Host==a.Self&&x.Epoch==1),"setup: the match under way under its maker (nobody is 10% better)");
                    As(c,c.Cancel);Step(5);
                    As(n,()=>n.JoinPrivateMatch(a.Match));for(int t=0;t<40&&!n.Started;t++)Step(1);
                    Check(n.Started&&a.Roster.ContainsKey(n.Self)&&a.IsHost,"setup: the newcomer has the empty seat");
                    var room=new[]{a,b,n};
                    // Its scene still loading: however long that takes, it is not weighed.
                    As(n,()=>n.ReportLoading(40));Step(60);
                    Check(room.All(x=>x.Host==a.Self&&x.Epoch==1),"a PC still loading was given the match");
                    As(n,()=>n.ReportLoading(100));Step(6);
                    Check(room.All(x=>x.Host==a.Self),"the role moved before the newcomer's scene had been up a moment");
                    Step(30);
                    bool moved=n.IsHost&&a.Host==n.Self&&b.Host==n.Self;
                    Check(moved==(late==1300),$"a {late} PC into a 900 host's match: moved={moved}");
                    if(moved)Check(Owner(n)==n.Self&&RoomData(n,"host")==n.Self.ToString()&&RoomData(n,"epoch")=="2"&&room.All(x=>x.Started&&x.Epoch==2),"the room did not follow the new host");
                    Check(room.All(x=>x.Manager==a.Self)&&a.IsManager,"the manager moved with the host");
                    // And it rests there.
                    Step(80);Check(room.All(x=>x.Started&&x.Host==(moved?n.Self:a.Self)&&x.Epoch==(moved?2:1)),"the role moved again");
                    foreach(var x in clients.ToArray())As(x,x.Dispose);
                }});
            Test("A running match weighs a machine score that reaches it late, but not before the shared start has passed; a leaver moves nothing",()=>{
                // B's probe is still running at the start (past the start's wait), so the weaker maker keeps the host.
                Room(new[]{900,0,500});var a=clients[0];var b=clients[1];var c=clients[2];
                As(a,a.StartGame);Step(20);Check(a.IsHost&&a.Started&&b.Started&&a.StartAt==0,"setup: started under its maker, still loading");
                b.LocalFitness=1400;As(a,()=>a.ReportLoading(100));As(b,()=>b.ReportLoading(100));As(c,()=>c.ReportLoading(40));
                // C is slow to load: the start waits for it, and so does the role (the loading screens wait on this host).
                Step(40);
                Check(a.StartAt==0&&clients.All(x=>x.Host==a.Self&&x.Epoch==1),"the role moved before the shared start was set");
                As(c,()=>c.ReportLoading(100));Step(4);
                double go=a.StartAt;Check(go>0&&a.IsHost,"setup: the shared start is set, by the same host");
                for(int t=0;t<80&&!b.IsHost;t++)Step(1);
                Check(b.IsHost&&a.Host==b.Self&&c.Host==b.Self&&clients.All(x=>x.Started&&x.Epoch==2)&&b.StartAt==go,"the stronger PC did not take the running match: host "+a.Host);
                Check(SteamUtils.GetServerRealTime()>=go+SteamSession.LiveElectSettle-1,"the role moved at the start line, not a moment into the match");
                Check(clients.All(x=>x.Manager==a.Self),"the manager moved with the host");
                // A player gone is a change as well: weighed, and the best PC already hosts.
                As(c,c.Cancel);Step(40);
                Check(b.IsHost&&a.Host==b.Self&&a.Epoch==2&&b.Roster.Count==2,"a leaver moved the host");});
            Console.WriteLine($"{passed} simulated session tests passed (not Steam integration tests).");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
