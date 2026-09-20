using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace ChessFight.ProtectKing.Editor
{
    [InitializeOnLoad]
    public static class OnlineVerification
    {
        static readonly List<string> results=new List<string>();
        static IEnumerator routine;
        static double deadline;
        static float resume;
        static string currentReport="OnlinePlayVerification.txt";
        static OnlineVerification()
        {
            EditorApplication.playModeStateChanged+=s=>{
                if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("ChessFight.OnlineVerify",false)) {
                    SessionState.SetBool("ChessFight.OnlineVerify",false);routine=SessionState.GetBool("ChessFight.BotSoak",false)?BotSoak():PlayChecks();SessionState.SetBool("ChessFight.BotSoak",false);resume=0;deadline=EditorApplication.timeSinceStartup+240;
                    EditorApplication.update+=Pump;
                }
            };
        }
        static void Check(bool value,string name) { if(!value)throw new Exception(name);results.Add("PASS: "+name); }
        [MenuItem("CHESS FIGHT/Online/Verify Rules and Scene")]
        public static void Rules()
        {
            results.Clear();
            var r=new PlayerRoster();
            Check(r.Count==0,"All twelve seats initially AI");
            Check(r.Assign(100)==0&&r.Assign(101)==6,"First two humans fill opposing Kings");
            Check(r.Assign(100)==0&&r.Count==2,"Duplicate join is idempotent");
            for(ulong i=102;i<112;i++)Check(r.Assign(i)>=0,"Assign human "+i);
            Check(r.Count==12&&r.Assign(999)==-1,"Thirteenth human rejected");
            int seat=r.Find(105);r.Release(105);
            Check(r.Owner(seat)==0&&r.Count==11,"Disconnect returns same seat to AI");
            Check(r.Assign(999)==seat,"Replacement claims released seat");
            Check(r.Assign(0)==-1,"AI sentinel cannot claim a seat");
            var command=new PlayerCommand {sequence=10,jump=3,respawn=2,move=new Vector2(.5f,.5f),yaw=37,interact=true};
            var wire=MatchProtocol.Encode(MatchMessage.Input,command);
            Check(MatchProtocol.Decode(wire,out var kind,out var restored,out _)&&kind==MatchMessage.Input&&restored.jump==3&&restored.move==command.move,"Input survives serialization");
            Check(!MatchProtocol.Decode(new byte[1201],out _,out _,out _),"Oversized packet rejected");
            Check(!MatchProtocol.Decode(new byte[]{1,2,3},out _,out _,out _),"Truncated packet rejected");
            wire[0]^=1;Check(!MatchProtocol.Decode(wire,out _,out _,out _),"Wrong protocol rejected");
            command.move=new Vector2(float.NaN,0);
            Check(!MatchProtocol.Decode(MatchProtocol.Encode(MatchMessage.Input,command),out _,out _,out _),"NaN input rejected");
            command.move=new Vector2(10,0);
            Check(!MatchProtocol.Decode(MatchProtocol.Encode(MatchMessage.Input,command),out _,out _,out _),"Overspeed input rejected");
            Check(!MatchProtocol.Newer(9,10)&&!MatchProtocol.Newer(10,10)&&MatchProtocol.Newer(1,uint.MaxValue),"Stale duplicate and wrapped sequence rules");
            var snapshot=new MatchSnapshot {sequence=8,phase=MatchPhase.Running,elapsed=75,remaining=225};
            for(int i=0;i<12;i++) { snapshot.owners[i]=(ulong)(100+i);snapshot.positions[i]=new Vector3(i,1,250);snapshot.checkpoint[i]=2;snapshot.falls[i]=3;snapshot.hold[i]=.5f; }
            wire=MatchProtocol.Encode(MatchMessage.Snapshot,snapshot:snapshot);
            Check(wire.Length<1200&&MatchProtocol.Decode(wire,out _,out _,out var decoded)&&decoded.owners[11]==111&&decoded.positions[11].z==250&&decoded.checkpoint[11]==2,"Late join full twelve-player snapshot");
            Array.Resize(ref wire,wire.Length-1);
            Check(!MatchProtocol.Decode(wire,out _,out _,out _),"Truncated snapshot rejected without mutation");
            var online=UnityEngine.Object.FindFirstObjectByType<OnlineMatchSession>();
            Check(online!=null&&online.local.online==online&&online.transport!=null,"Scene session references connected");
            Check(online.map.players.Length==12,"Scene contains exactly twelve player slots");
            foreach(var player in online.map.players) {
                var bot=player.GetComponent<BotDriver>();
                Check(bot!=null&&bot.motor!=null&&bot.route.Length>=30,"AI route connected: "+player.DisplayName);
            }
            Save("OnlineRulesVerification.txt");
            Debug.Log("[CHESS FIGHT] Online rules and scene checks passed: "+results.Count);
        }
        [MenuItem("CHESS FIGHT/Online/Run Play Verification")]
        public static void Play()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode first.");
            Rules();results.Clear();SessionState.SetBool("ChessFight.OnlineVerify",true);EditorApplication.isPlaying=true;
        }
        static IEnumerator PlayChecks()
        {
            yield return .3f;
            var online=UnityEngine.Object.FindFirstObjectByType<OnlineMatchSession>();
            var map=online.map;var local=online.local;
            local.enabled=false;
            online.StartMatch();
            Check(map.match.IsRunning,"Host/offline starts match");
            Vector3 start=map.players[6].transform.position;
            yield return 3f;
            Check(map.players[6].transform.position.z>start.z+5,"AI moves through existing controller");
            Check(map.players[0].transform.position.z<0,"AI does not drive human seat");
            ScreenCapture.CaptureScreenshot("Artifacts/ChessFight/OnlineHud.png");
            yield return 7f;
            Check(map.players[6].transform.position.z>25,"AI jumps Zone 1 fall gap");
            Vector3 takeover=map.players[6].transform.position;
            int checkpoint=map.players[6].checkpoint;
            local.SelectPlayer(6);
            Check(!online.IsBot(6)&&online.IsBot(0),"Local takeover hands previous seat back to AI");
            Check(map.players[6].transform.position==takeover&&map.players[6].checkpoint==checkpoint,"Takeover preserves position and checkpoint");
            online.enabled=false;
            foreach(var player in map.players) { var m=player.GetComponent<PlayerMotor>();m.ClearInput();m.ExternalControl=false; }
            map.match.HasAuthority=false;float elapsed=map.match.Elapsed;map.match.AdvanceClock(20);
            Check(map.match.Elapsed==elapsed,"Non-authority cannot advance timer");
            map.Respawn(map.players[6].GetComponent<PlayerMotor>());
            Check(map.players[6].transform.position==takeover,"Non-authority cannot respawn");
            map.match.ReturnToReady();
            Check(map.match.IsRunning,"Non-authority cannot reset match");
            map.match.HasAuthority=true;
            var blue=map.players[0];var red=map.players[6];blue.checkpoint=5;red.checkpoint=5;
            blue.GetComponent<PlayerMotor>().Teleport(map.match.throne.transform.position+Vector3.left);
            red.GetComponent<PlayerMotor>().Teleport(map.match.throne.transform.position+Vector3.right);
            Physics.SyncTransforms();
            var throne=map.match.throne;throne.ResetProgress();
            throne.TickInteraction(blue,true,.8f);throne.TickInteraction(red,true,.6f);
            Check(throne.GetFraction(0)>0&&throne.GetFraction(6)>0,"Both Kings channel independently");
            throne.TickInteraction(red,false,.1f);
            Check(throne.GetFraction(0)>0&&throne.GetFraction(6)==0,"One King releasing does not reset the other");
            throne.TickInteraction(blue,true,1f);
            Check(map.match.Phase==MatchPhase.Finished&&map.match.Result=="BLUE WINS","Host awards throne victory once");
            throne.TickInteraction(red,true,2f);
            Check(map.match.Result=="BLUE WINS","Second claimant cannot override winner");
            Save("OnlinePlayVerification.txt");
            online.enabled=true;
            var steam=online.transport;
            if(!steam.Initialize()) {
                results.Add("BLOCKED: Live Steam initialization: "+steam.Status);Save("OnlinePlayVerification.txt");yield break;
            }
            Check(steam.Initialized && steam.LocalId!=0,"Steam API initializes with active Steam client");
            steam.Host();float steamDeadline=Time.unscaledTime+25;
            while(steam.Busy && Time.unscaledTime<steamDeadline)yield return .1f;
            Check(steam.InLobby && steam.IsHost,"Actual Steam lobby creation callback succeeds");
            Check(Steamworks.SteamMatchmaking.GetLobbyMemberLimit(new Steamworks.CSteamID(steam.LobbyId))==12,"Steam lobby has twelve-human member limit");
            Check(online.Roster.Count==1&&online.LocalSlot==0,"Steam host claims one human seat, eleven AI remain");
            steam.Leave();
            Check(!steam.InLobby&&online.Roster.Count==0,"Leaving Steam lobby clears network ownership");
            Save("OnlinePlayVerification.txt");
        }
        [MenuItem("CHESS FIGHT/Online/Run AI Course Test")]
        public static void Soak()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play Mode first.");
            results.Clear();SessionState.SetBool("ChessFight.BotSoak",true);SessionState.SetBool("ChessFight.OnlineVerify",true);EditorApplication.isPlaying=true;
        }
        static IEnumerator BotSoak()
        {
            currentReport="AICourseVerification.txt";
            yield return .3f;
            var online=UnityEngine.Object.FindFirstObjectByType<OnlineMatchSession>();
            online.local.enabled=false;online.StartMatch();Time.timeScale=2;
            float end=Time.time+340;
            while(Time.time<end&&online.map.match.IsRunning) {
                yield return 20f;
                results.Add("t="+online.map.match.Elapsed.ToString("F1"));
                foreach(var p in online.map.players)if(online.IsBot(p.playerId))
                    results.Add(p.DisplayName+" CP"+p.checkpoint+" falls="+p.falls+" pos="+p.transform.position);
                Save("AICourseVerification.txt");
            }
            Check(online.map.match.Result=="RED WINS","AI King traverses CP1-CP5 and claims throne");
            Save("AICourseVerification.txt");
        }
        static void Pump()
        {
            try {
                if(!EditorApplication.isPlaying)throw new Exception("Play verification interrupted.");
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Play verification timed out.");
                if(Time.time<resume)return;
                if(routine.MoveNext()) { resume=Time.time+(routine.Current is float seconds?seconds:0);return; }
                Finish();
            } catch(Exception e) { results.Add("FAIL: "+e);Save(currentReport);Debug.LogException(e);Finish(); }
        }
        static void Finish() { EditorApplication.update-=Pump;routine=null;Time.timeScale=1;EditorApplication.isPlaying=false; }
        static void Save(string name) {
            Directory.CreateDirectory("Artifacts/ChessFight");
            File.WriteAllText("Artifacts/ChessFight/"+name,DateTime.Now.ToString("s")+"\n"+string.Join("\n",results));
        }
    }
}