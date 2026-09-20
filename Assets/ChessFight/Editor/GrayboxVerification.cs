using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ChessFight.ProtectKing.Editor
{
    [InitializeOnLoad]
    public static class GrayboxVerification
    {
        const string Pending = "ChessFight.VerificationPending";
        static IEnumerator routine;
        static bool previewOnly;
        static float resumeAt;
        static double deadline;
        static readonly List<string> results = new List<string>();

        static GrayboxVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
                {
                    SessionState.SetBool(Pending,false);
                    results.Clear();
                    previewOnly=SessionState.GetBool("ChessFight.PreviewOnly",false);
                    SessionState.SetBool("ChessFight.PreviewOnly",false);
                    routine = Verify();
                    resumeAt = 0;
                    deadline = EditorApplication.timeSinceStartup + 90;
                    EditorApplication.update -= Pump;
                    EditorApplication.update += Pump;
                }
            };
        }

        [MenuItem("CHESS FIGHT/04 Run Play Verification")]
        public static void Begin()
        {
            GrayboxBuilder.Validate();
            SessionState.SetBool(Pending,true);
            if (EditorApplication.isPlaying)
            {
                SessionState.SetBool(Pending,false);
                results.Clear();
                routine=Verify();
                deadline=EditorApplication.timeSinceStartup+90;
                resumeAt=0;
                EditorApplication.update-=Pump;
                EditorApplication.update+=Pump;
            }
            else EditorApplication.isPlaying=true;
        }

        public static void CaptureBatch()
        {
            SessionState.SetBool("ChessFight.PreviewOnly",true);
            BuildAndVerify();
        }

        public static void BuildAndVerify()
        {
            try { GrayboxBuilder.Build(); Begin(); }
            catch(Exception e) { Debug.LogException(e); if(Application.isBatchMode)EditorApplication.Exit(1); }
        }

        static void Check(bool value,string name)
        {
            if(!value)throw new Exception("FAIL: "+name);
            results.Add("PASS: "+name);
            Debug.Log("[CHESS FIGHT TEST] PASS: "+name);
        }

        static void Pump()
        {
            if(routine==null)return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Verification timed out");
                if(Time.time<resumeAt)return;
                if(routine.MoveNext())
                {
                    resumeAt=Time.time+(routine.Current is float delay?delay:0);
                    return;
                }
                Finish(true,null);
            }
            catch(Exception e) { Finish(false,e.ToString()); }
        }

        static void Finish(bool success,string error)
        {
            EditorApplication.update-=Pump;
            routine=null;
            if(error!=null)results.Add(error);
            Directory.CreateDirectory("Artifacts/ChessFight");
            File.WriteAllText(previewOnly ? "Artifacts/ChessFight/PreviewVerification.txt" : "Artifacts/ChessFight/PlayVerification.txt",
                "CHESS FIGHT PLAY VERIFICATION\n"+DateTime.Now.ToString("s")+"\n"+
                (success?"PASS":"FAIL")+"\n"+string.Join("\n",results)+
                "\nCoverage: local movement and rule checks; no online 12-player or balance validation.");
            if(!success)Debug.LogError(error);
            if(Application.isBatchMode)EditorApplication.Exit(success?0:1);
            else EditorApplication.isPlaying=false;
        }

        static IEnumerator Verify()
        {
            yield return .2f;
            var map=Object.FindFirstObjectByType<StageMap>();
            var match=map.match;
            var session=Object.FindFirstObjectByType<LocalPlaySession>();
            session.enabled=false;
            if(previewOnly) { CaptureViews(session.viewCamera); results.Add("PASS: Four updated scene previews rendered"); yield break; }
            session.SelectPlayer(0);
            var motor=map.players[0].GetComponent<PlayerMotor>();
            var king=motor.Identity;
            var redKing=map.players[6];
            var queen=map.players[1];
            var throne=match.throne;
            match.StartMatch();
            Check(match.IsRunning && match.Remaining>295,"Match starts with five-minute clock");

            motor.Teleport(new Vector3(-16,.1f,8));
            yield return .4f;
            Check(motor.Grounded,"Character grounded on graybox floor: position="+motor.transform.position+", falls="+king.falls);
            float z=motor.transform.position.z;
            motor.SetInput(Vector2.up,false);
            yield return 1f;
            motor.SetInput(Vector2.zero,false);
            Check(motor.transform.position.z-z>5 && motor.transform.position.z-z<7,"Movement covers six units per second");

            motor.Teleport(new Vector3(-16,.08f,21));
            yield return .3f;
            motor.SetInput(Vector2.up,true);
            yield return .3f;
            Check(motor.transform.position.y>.6f,"Jump lifts the player");
            yield return .65f;
            motor.SetInput(Vector2.zero,false);
            Check(motor.transform.position.z>24.5f && king.falls==0,"Basic jump clears the early two-unit fall gap");
            yield return .3f;
            Check(motor.Grounded,"Player lands after the gap: position="+motor.transform.position+", falls="+king.falls);

            motor.Teleport(new Vector3(-21,.08f,30));
            yield return .2f;
            motor.SetInput(Vector2.left,false);
            yield return .8f;
            motor.SetInput(Vector2.zero,false);
            Check(motor.transform.position.x>-22.8f,"Controller cannot pass through boundary wall");

            motor.Teleport(new Vector3(-16,-8,25));
            yield return .2f;
            Check(king.falls==1 && king.checkpoint==0 && motor.transform.position.z<0,"Fall returns to personal CP0");

            // The wrong team's CP1 is crossed at an otherwise valid height and direction.
            motor.Teleport(new Vector3(16,0,155));
            map.CheckTravel(motor,new Vector3(16,0,149),motor.transform.position);
            Check(king.checkpoint==0 && motor.transform.position.z<0,"Other team's split checkpoint is rejected");

            motor.Teleport(new Vector3(-16,20,155));
            map.CheckTravel(motor,new Vector3(-16,20,149),motor.transform.position);
            Check(king.checkpoint==0 && motor.transform.position.z<0,"Jumping over checkpoint volume cannot skip it");

            motor.Teleport(new Vector3(0,0,254));
            map.CheckTravel(motor,new Vector3(0,0,249),motor.transform.position);
            Check(king.checkpoint==0 && motor.transform.position.z<0,"CP2 before CP1 is rejected");

            for(int cp=1;cp<=5;cp++)
            {
                var gate=map.FindCheckpoint(cp,TeamId.Blue);
                var from=gate.transform.position+Vector3.back;
                var to=gate.transform.position+Vector3.forward;
                motor.Teleport(to);
                map.CheckTravel(motor,from,to);
                Check(king.checkpoint==cp,"Sequential CP"+cp+" accepted");
                int falls=king.falls;
                map.Respawn(motor);
                Check(Vector3.Distance(motor.transform.position,gate.Recovery(king))<.05f && king.falls==falls+1,
                    "Personal recovery anchor at CP"+cp);
                map.CheckTravel(motor,gate.transform.position+Vector3.forward,gate.transform.position+Vector3.back);
                Check(king.checkpoint==cp,"Reverse crossing does not reduce CP"+cp);
            }
            Check(redKing.checkpoint==0 && queen.checkpoint==0,"Checkpoint progress is not shared between players");

            var rook=map.players[2].GetComponent<PlayerMotor>();
            rook.Identity.checkpoint=5;
            map.Respawn(rook,false);
            Check(Vector3.Distance(rook.transform.position,motor.transform.position)>1,"Recovery slots do not overlap");

            queen.checkpoint=5;
            queen.GetComponent<PlayerMotor>().Teleport(throne.transform.position+Vector3.back*2);
            Physics.SyncTransforms();
            Check(!throne.IsEligible(queen),"Queen cannot claim the throne");
            queen.GetComponent<PlayerMotor>().Teleport(map.startPoints[1].position);

            motor.Teleport(throne.transform.position+Vector3.back*2);
            Physics.SyncTransforms();
            king.checkpoint=4;
            Check(!throne.IsEligible(king),"King without CP5 cannot claim the throne");
            king.checkpoint=5;
            Check(throne.IsEligible(king),"Qualified King can interact from front approach");

            throne.TickInteraction(king,true,.8f);
            Check(match.IsRunning && throne.Fraction>0 && throne.Fraction<1,"Touching or short hold does not win");
            throne.TickInteraction(king,false,.01f);
            Check(throne.Fraction==0,"Releasing input resets throne progress");
            throne.TickInteraction(king,true,.8f);
            motor.Teleport(throne.transform.position+Vector3.back*8);
            throne.TickInteraction(king,true,.1f);
            Check(throne.Fraction==0,"Leaving range cancels throne progress");
            motor.Teleport(throne.transform.position+Vector3.back*2);
            Physics.SyncTransforms();
            throne.TickInteraction(king,true,throne.holdSeconds);
            Check(match.Phase==MatchPhase.Finished && match.Result=="BLUE WINS","Continuous qualified hold awards Blue once");
            Check(!match.TryFinish(redKing) && match.Result=="BLUE WINS","Result cannot be overwritten");

            match.StartMatch();
            Check(king.checkpoint==0 && king.falls==0 && match.IsRunning,"Restart resets player progression and falls");
            match.AdvanceClock(match.regulationSeconds);
            Check(match.Phase==MatchPhase.Overtime && match.Remaining<=60,"Five minutes triggers one-minute overtime");
            match.AdvanceClock(61);
            Check(match.Phase==MatchPhase.Finished && match.Result.StartsWith("DRAW"),"Unclaimed throne after overtime ends in draw");

            match.StartMatch();
            var moving=Object.FindObjectsByType<ObstacleMotion>(FindObjectsSortMode.None);
            var obstacle=Array.Find(moving, m=>m.kind!=MotionKind.Rotate);
            var before=obstacle.transform.position;
            yield return .6f;
            Check((obstacle.transform.position-before).sqrMagnitude>.001f,"Kinematic obstacle moves during play");

            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            {
                session.enabled=true;
                session.SelectPlayer(0);
                yield return .3f;
                CaptureViews(session.viewCamera);
                session.enabled=false;
            }
            results.Add("INFO: " + moving.Length + " moving obstacles; 12 local slots; 7 CP volumes for five indices.");
        }

        public static void CaptureViews(Camera camera)
        {
            Directory.CreateDirectory("Artifacts/ChessFight/Previews");
            var oldPosition=camera.transform.position;
            var oldRotation=camera.transform.rotation;
            float oldFar=camera.farClipPlane;
            bool fog=RenderSettings.fog;
            RenderSettings.fog=false;
            camera.farClipPlane=1800;
            Capture(camera,"01_Start",new Vector3(43,39,-38),new Vector3(0,0,58));
            Capture(camera,"02_FirstClash",new Vector3(42,51,139),new Vector3(0,0,201));
            Capture(camera,"04_SecondClash",new Vector3(47,52,428),new Vector3(0,0,484));
            Capture(camera,"06_Throne",new Vector3(33,31,723),new Vector3(0,1,772));
            camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
            camera.farClipPlane=oldFar;
            RenderSettings.fog=fog;
        }

        static void Capture(Camera camera,string name,Vector3 position,Vector3 target)
        {
            camera.transform.position=position;
            camera.transform.LookAt(target);
            var rt=RenderTexture.GetTemporary(1440,900,24);
            var request=new UniversalRenderPipeline.SingleCameraRequest { destination=rt };
            RenderPipeline.SubmitRenderRequest(camera,request);
            var previous=RenderTexture.active;
            RenderTexture.active=rt;
            var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1440,900),0,0);
            image.Apply();
            File.WriteAllBytes("Artifacts/ChessFight/Previews/"+name+".png",image.EncodeToPNG());
            Object.Destroy(image);
            RenderTexture.active=previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
