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
    // Explicit Play Mode checks using the project's real pawn and physics.
    // Does not run on startup and never serializes player tuning or test input.
    public static class RoyalGateRushProbe
    {
        const string Report="Temp/RoyalGateRush.runtime.txt";
        static LabGame game;
        static RagdollPawn pawn;
        static PawnRushSection section;
        static ObstacleMotion[] motions;
        static Vector3[] first;
        static readonly List<string> lines=new List<string>();
        static readonly List<string> errors=new List<string>();
        static float start,stageStart,lowest,highest,drift,hitAt;
        static int stage,hits,waypoint;
        static bool oldEnabled,walking;
        static bool recovering;
        static Vector3 rideStart;
        static Vector3[] activeRoute;
        static readonly Vector3[] recoveryRoute={
            new Vector3(-12,0,89),new Vector3(-8,0,89),new Vector3(-5,0,88),
            new Vector3(5,0,88),new Vector3(8,0,97),new Vector3(12,0,97),
            new Vector3(12,0,110),new Vector3(12,0,97),new Vector3(8,0,97)
        };
        static readonly Vector3[] route={
            new Vector3(-4.8f,0,18),new Vector3(-4.8f,0,47),
            new Vector3(-8,0,52),new Vector3(-8,0,80),
            new Vector3(-5,0,88),new Vector3(-7.6f,0,98),new Vector3(-7.6f,0,115),
            new Vector3(0,0,125),new Vector3(0,0,141)
        };

        [MenuItem("ChessFight/Pawn Rush/왕성 정문 돌파/Play 중 물리 점검")]
        public static void Start(){Begin(false,false);}
        [MenuItem("ChessFight/Pawn Rush/왕성 정문 돌파/Play 중 기본 이동 완주 점검")]
        public static void Walk(){Begin(true,false);}
        [MenuItem("ChessFight/Pawn Rush/왕성 정문 돌파/Play 중 복귀 경사로 점검")]
        public static void Recovery(){Begin(true,true);}
        static string WalkReport => recovering ? "Temp/RoyalGateRush.recovery.txt" : "Temp/RoyalGateRush.walk.txt";
        static void Begin(bool walk,bool recovery)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play Mode에서 실행하세요.");
            if(game!=null)throw new InvalidOperationException("A probe is already running.");
            section=PawnRushWorkspaceTools.FindCourse().Sections[0];
            if(section.sectionId!=RoyalGateRushBuilder.Id)throw new InvalidOperationException("Royal Gate Rush must be A1.");
            game=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<LabGame>(true)).Single();
            pawn=game.players[0].pawn;
            if(pawn==null){game=null;throw new InvalidOperationException("Wait for player spawn.");}
            oldEnabled=game.enabled;game.enabled=false;
            walking=walk;stage=0;waypoint=0;start=stageStart=Time.time;
            recovering=recovery;activeRoute=recovery?recoveryRoute:route;
            lines.Clear();errors.Clear();lines.Add(walk?"Real pawn waypoint traversal (no sprint/jump/abilities)":"Real pawn / moving-platform / physical mallet contact checks");
            motions=section.GetComponentsInChildren<ObstacleMotion>();first=motions.Select(m=>m.transform.position).ToArray();
            MoveTo(recovery?new Vector3(-12,-1.2f,102):new Vector3(0,0,3));
            Application.logMessageReceived+=Log;
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=PlayState;
            File.WriteAllText(walk?WalkReport:Report,"RUNNING");
        }
        static void MoveTo(Vector3 floor)
        {
            pawn.SetInput(default);
            pawn.Teleport(section.transform.TransformPoint(floor+Vector3.up*(pawn.standHeight+.04f)),section.transform.forward);
        }
        static void Log(string condition,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(condition);}
        static void PlayState(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish(false,"Play stopped during probe");}
        static void Check(bool ok,string label)
        {lines.Add((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);}
        static void Tick()
        {
            if(pawn==null||game==null||!EditorApplication.isPlaying)return;
            try
            {
                if(walking){WalkTick();return;}
                float elapsed=Time.time-stageStart;
                pawn.SetInput(default);
                if(stage==0&&elapsed>=1.6f)
                {
                    var local=section.transform.InverseTransformPoint(pawn.Hips.position);
                    Check(local.y>0&&local.y<1.2f&&Mathf.Abs(local.z-3)<1,"existing pawn remains grounded at entry");
                    Check(motions.Length==6&&motions.Where((m,i)=>(m.transform.position-first[i]).sqrMagnitude>.0001f).Count()==6,"all six kinematic walls/tiles advance");
                    Check(motions.All(m=>m.GetComponent<Rigidbody>().isKinematic),"moving geometry uses kinematic bodies");
                    var tile=motions.First(m=>m.name=="WaveTile_01");
                    float top=section.transform.InverseTransformPoint(tile.GetComponentInChildren<Collider>().bounds.max).y;
                    MoveTo(new Vector3(0,top,57));rideStart=pawn.Hips.position;lowest=float.MaxValue;highest=float.MinValue;drift=0;
                    stage=1;stageStart=Time.time;
                }
                else if(stage==1)
                {
                    var local=section.transform.InverseTransformPoint(pawn.Hips.position);
                    lowest=Mathf.Min(lowest,local.y);highest=Mathf.Max(highest,local.y);
                    drift=Mathf.Max(drift,Vector3.ProjectOnPlane(pawn.Hips.position-rideStart,Vector3.up).magnitude);
                    if(elapsed>=6.5f)
                    {
                        Check(lowest>-.2f&&highest<1.6f&&drift<1.2f,$"pawn rides full low-tile cycle: hips {lowest:F2}..{highest:F2}m, drift {drift:F2}m");
                        hits=pawn.Hits;hitAt=-100;MoveTo(new Vector3(-5,0,102));
                        lowest=0;highest=0;rideStart=pawn.Hips.position;
                        stage=2;stageStart=Time.time;
                    }
                }
                else if(stage==2)
                {
                    highest=Mathf.Max(highest,Vector3.ProjectOnPlane(pawn.Hips.position-rideStart,Vector3.up).magnitude);
                    if(pawn.Hits>hits)
                    {
                        Check(pawn.Hits-hits==1,"ragdoll compound colliders register one hit per contact");
                        Check(Time.time-hitAt>=1.20f,"mallet hit cooldown prevents immediate repeat");
                        hitAt=Time.time;hits=pawn.Hits;lowest++;
                    }
                    if(elapsed>=6.5f)
                    {
                        Check(lowest>=1,"real physical mallet contact reaches existing hit receiver");
                        lines.Add($"INFO mallet contact horizontal displacement: {highest:F2}m; contact count {lowest}");
                        Check(pawn.Hips.position.y>section.transform.position.y-3,"mallet victim remains on course/recovery geometry");
                        Check(errors.Count==0,"no runtime errors during probe");
                        Finish(true,"Physics smoke test complete; human/12-player/Steam checks pending.");
                    }
                }
                if(Time.time-start>30)throw new TimeoutException("Physics probe timed out");
            }
            catch(Exception e){Finish(false,e.Message);}
        }
        static void WalkTick()
        {
            var local=section.transform.InverseTransformPoint(pawn.Hips.position);
            Vector3 target=activeRoute[waypoint];Vector3 delta=Vector3.ProjectOnPlane(target-local,Vector3.up);
            if(delta.magnitude<.6f)
            {
                lines.Add($"PASS waypoint {waypoint+1} at {Time.time-start:F2}s (x={local.x:F2},y={local.y:F2},z={local.z:F2})");
                if(++waypoint>=activeRoute.Length){Check(errors.Count==0,"no runtime errors during traversal");Finish(true,$"Completed without jump/sprint/abilities in {Time.time-start:F2}s");return;}
                stageStart=Time.time;delta=Vector3.ProjectOnPlane(activeRoute[waypoint]-local,Vector3.up);
            }
            pawn.SetInput(new PawnInput{move=section.transform.TransformDirection(delta.normalized)});
            if(local.y< -3)throw new InvalidOperationException("Traversal fell below recovery floor");
            if(Time.time-start>90||Time.time-stageStart>25)throw new TimeoutException($"Traversal stalled at waypoint {waypoint+1}, local position {local}");
        }
        static void Finish(bool ok,string message)
        {
            EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;Application.logMessageReceived-=Log;
            lines.Add((ok?"PASS ":"FAIL ")+message);lines.AddRange(errors.Select(e=>"ERROR "+e));
            File.WriteAllLines(walking?WalkReport:Report,lines);
            if(pawn!=null&&section!=null)MoveTo(new Vector3(0,0,3));
            if(game!=null)game.enabled=oldEnabled;
            game=null;pawn=null;
        }
    }
}
