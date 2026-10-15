using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChessFight.Gameplay.PawnRush;
using ChessFight.ProtectKing;
using ChessFight.RagdollLab;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Maps.Editor
{
    // Explicit offline checks in an isolated scene. Uses real player input and collision.
    public static class RoyalFountainHallProbe
    {
        static LabGame game;static RagdollPawn pawn;static PawnRushSection section;
        static readonly List<string> lines=new List<string>(),errors=new List<string>();
        static Vector3[] route;static string report;static float start,stageStart,lastHit,low,high;
        static int mode,stage,waypoint,hitCount,hits;static bool oldEnabled,sawCrack,sawMissing,sawRestore;
        static CrumblingRoyalFloor tile;static Vector3 origin;static Transform[] bays;static int bayIndex;
        public static void Start(){Begin(0);}
        public static void Walk(bool right){Begin(right?2:1);}
        public static void Recover(){Begin(3);}
        static void Begin(int kind)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play first.");
            if(game!=null)throw new InvalidOperationException("A probe is already running.");
            var scene=SceneManager.GetSceneByName(RoyalFountainHallBuilder.TestName);
            if(!scene.IsValid())throw new InvalidOperationException("Open the isolated test scene first.");
            game=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LabGame>()).Single();pawn=game.players[0].pawn;
            section=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PawnRushSection>()).Single();
            if(pawn==null){game=null;throw new InvalidOperationException("Wait for spawn.");}
            mode=kind;stage=waypoint=0;start=stageStart=Time.time;lines.Clear();errors.Clear();oldEnabled=game.enabled;game.enabled=false;
            report="Temp/RoyalFountain."+new[]{"physics","left","right","recovery"}[mode]+".txt";File.WriteAllText(report,"RUNNING");
            lines.Add("Real pawn / no sprint or abilities / isolated offline test");
            Move(new Vector3(0,0,3));
            if(mode==1||mode==2){var path=(mode==1?RoyalFountainHallBuilder.Left:RoyalFountainHallBuilder.Right).Skip(1).Take(10).ToList();path.AddRange(new[]{new Vector3(2.8f,0,121),new Vector3(2.8f,0,131),new Vector3(0,0,141)});route=path.ToArray();}
            if(mode==3){bays=section.transform.Find("Recovery").Cast<Transform>().ToArray();bayIndex=0;SetRecovery();}
            Application.logMessageReceived+=Log;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;
        }
        static void Move(Vector3 floor){pawn.SetInput(default);pawn.Teleport(section.transform.TransformPoint(floor+Vector3.up*(pawn.standHeight+.04f)),section.transform.forward);}
        static void Log(string s,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(s);}
        static void PlayState(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish(false,"Play stopped during probe");}
        static void Check(bool ok,string message){lines.Add((ok?"PASS ":"FAIL ")+message);if(!ok)throw new InvalidOperationException(message);}
        static Vector3 Local=>section.transform.InverseTransformPoint(pawn.Hips.position);
        static void Tick()
        {
            if(game==null||pawn==null||!EditorApplication.isPlaying)return;
            try{
                if(mode!=0){Traverse();return;}
                float age=Time.time-stageStart;pawn.SetInput(default);
                if(stage==0&&age>1.5f){
                    Check(Local.y>0&&Local.y<1.2f,"pawn grounds on entrance deck");
                    tile=section.GetComponentsInChildren<CrumblingRoyalFloor>().First();
                    origin=section.transform.InverseTransformPoint(tile.transform.position);Move(origin);sawCrack=sawMissing=sawRestore=false;
                    stage=1;stageStart=Time.time;
                }else if(stage==1){
                    if(tile.State==RoyalFloorState.Cracking)sawCrack=true;
                    if(tile.State==RoyalFloorState.Missing){sawMissing=true;Check(!tile.SolidCollider.enabled&&!tile.Visual.gameObject.activeSelf,"collapsed tile has no solid or visible deck");Move(new Vector3(0,0,3));stage=2;stageStart=Time.time;}
                    if(age>6)throw new TimeoutException("Contact tile did not collapse: "+tile.State+" pawn="+Local);
                }else if(stage==2){
                    if(tile.State==RoyalFloorState.Restoring)sawRestore=true;
                    if(age>3.3f){Check(sawCrack&&sawMissing&&sawRestore&&tile.State==RoyalFloorState.Stable&&tile.SolidCollider.enabled,"foot contact / crack / missing / warning / restoration cycle");
                        var pendulum=section.GetComponentsInChildren<PendulumSwing>().First();
                        origin=section.transform.InverseTransformPoint(pendulum.transform.parent.position);Move(origin);
                        hits=pawn.Hits;hitCount=0;lastHit=-100;high=0;stage=3;stageStart=Time.time;}
                }else if(stage==3){
                    high=Mathf.Max(high,Vector3.ProjectOnPlane(Local-origin,Vector3.up).magnitude);
                    if(pawn.Hits>hits){Check(pawn.Hits-hits==1,"compound pawn receives one mallet hit");Check(Time.time-lastHit>=1.16f,"mallet cooldown prevents repeated contact spam");lastHit=Time.time;hitCount++;hits=pawn.Hits;}
                    if(age>6){Check(hitCount>0,"pendulum reaches existing IHitReceiver");lines.Add("INFO hit displacement "+high.ToString("F2")+"m; final height "+Local.y.ToString("F2"));
                        Check(Local.y> -3,"victim stays on bridge or recovery bay");
                        var disc=section.GetComponentsInChildren<ObstacleSurface>().First();origin=section.transform.InverseTransformPoint(disc.transform.position)+Vector3.right*2;Move(origin);stage=4;stageStart=Time.time;}
                }else if(stage==4&&age>1.5f){
                    float displacement=Vector3.ProjectOnPlane(Local-origin,Vector3.up).magnitude;Check(displacement>.15f&&displacement<3,"gentle turntable carries pawn once: "+displacement.ToString("F2")+"m");Check(errors.Count==0,"no runtime errors during physics probe");Finish(true,"Physics probe finished; multiplayer remains untested.");
                }
                if(Time.time-start>35)throw new TimeoutException("Physics probe timed out");
            }catch(Exception e){Finish(false,e.Message);}
        }
        static void SetRecovery()
        {
            var bay=bays[bayIndex];var at=section.transform.InverseTransformPoint(bay.position);
            Move(at+Vector3.down*2.4f);
            route=new[]{new Vector3(3.75f,-2.4f,0),new Vector3(3.75f,-2.4f,-2),new Vector3(3.75f,0,-11),new Vector3(0,0,-12)}.Select(p=>section.transform.InverseTransformPoint(bay.TransformPoint(p))).ToArray();
            waypoint=0;stageStart=Time.time;low=Time.time;lines.Add("INFO recovery bay "+bay.name);
        }
        static void Traverse()
        {
            var local=Local;var delta=Vector3.ProjectOnPlane(route[waypoint]-local,Vector3.up);
            if(delta.magnitude<.5f){
                lines.Add($"PASS waypoint {waypoint+1} at {Time.time-start:F2}s x={local.x:F2} y={local.y:F2} z={local.z:F2}");
                if(++waypoint>=route.Length){
                    if(mode==3){Check(local.y>-.2f,"recovery ramp reaches upper deck");lines.Add("INFO bay traversal "+(Time.time-low).ToString("F2")+"s");if(++bayIndex<bays.Length){SetRecovery();return;}}
                    Check(errors.Count==0,"no runtime errors during traversal");Finish(true,"Traversal completed in "+(Time.time-start).ToString("F2")+"s");return;
                }stageStart=Time.time;delta=Vector3.ProjectOnPlane(route[waypoint]-local,Vector3.up);
            }
            pawn.SetInput(new PawnInput{move=section.transform.TransformDirection(delta.normalized)});
            if(local.y< -3)throw new InvalidOperationException("Fell below recovery deck at "+local);
            if(Time.time-start>100||Time.time-stageStart>20)throw new TimeoutException("Stalled at waypoint "+(waypoint+1)+" position "+local);
        }
        static void Finish(bool ok,string message)
        {
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;Application.logMessageReceived-=Log;
            lines.Add((ok?"PASS ":"FAIL ")+message);lines.AddRange(errors.Select(e=>"ERROR "+e));File.WriteAllLines(report,lines);
            if(pawn!=null&&section!=null)Move(new Vector3(0,0,3));if(game!=null)game.enabled=oldEnabled;game=null;pawn=null;
        }
    }
}
