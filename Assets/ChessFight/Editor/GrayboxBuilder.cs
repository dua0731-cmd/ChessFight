using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.ProtectKing.Editor
{
    public static class GrayboxBuilder
    {
        public const string ScenePath = "Assets/Scenes/ProtectTheKing_Graybox.unity";
        const string Root = "Assets/ChessFight";
        static Material light, dark, stone, blue, red, gold, danger;
        static StageMap map;
        static ProtectTheKingMatchController match;
        static readonly List<CheckpointGate> gates = new List<CheckpointGate>();

        [MenuItem("CHESS FIGHT/01 Build or Open Graybox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i=0; i<SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your current scene before opening the graybox.");
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                FrameMap();
                return;
            }
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            light = Mat("BoardLight", new Color(.70f,.74f,.76f));
            dark = Mat("BoardDark", new Color(.19f,.235f,.28f));
            stone = Mat("Stone", new Color(.38f,.43f,.48f));
            blue = Mat("Blue", new Color(.08f,.47f,.85f));
            red = Mat("Red", new Color(.83f,.17f,.22f));
            gold = Mat("CheckpointGold", new Color(.96f,.64f,.16f));
            danger = Mat("Hazard", new Color(.91f,.37f,.10f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var environment = Group("Environment", null);
            var sun = Group("Directional Light", environment).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1,.94f,.84f);
            sun.intensity = 1.8f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48,-32,0);
            sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            var volume = Group("Global Volume", environment).gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f,.62f,.7f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.25f,.31f,.38f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 100;
            RenderSettings.fogEndDistance = 260;

            var systems = Group("Systems", null);
            match = Group("MatchController", systems).gameObject.AddComponent<ProtectTheKingMatchController>();
            var mapRoot = Group("Map", null);
            map = mapRoot.gameObject.AddComponent<StageMap>();
            map.match = match;
            match.map = map;
            gates.Clear();

            var start = Group("Start_CP0", mapRoot);
            Floor(start, -16, -10, 0, 14);
            Floor(start, 16, -10, 0, 14);
            var startPoints = new Transform[12];
            for (int i=0; i<12; i++)
            {
                float x = i<6 ? -16 : 16;
                startPoints[i] = Group(((TeamId)(i/6)) + "_" + (PieceType)(i%6), start);
                startPoints[i].position = new Vector3(x+((i%6)%3-1)*2, .15f, -5+((i%6)/3)*2);
            }
            map.startPoints = startPoints;
            Banner(start, "CHESS FIGHT", 0, -7, 50, gold);
            Label(start, "BLUE TEAM", new Vector3(-16,.05f,-8), 1, blue.color, true);
            Label(start, "RED TEAM", new Vector3(16,.05f,-8), 1, red.color, true);

            Zone1(mapRoot);
            Zone2(mapRoot);
            Zone3(mapRoot);
            Zone4(mapRoot);
            Zone5(mapRoot);
            Zone6(mapRoot);
            map.checkpoints = gates.OrderBy(g => g.index).ToArray();

            var recovery = Group("Recovery", null);
            var below = Box("Fall boundary - respawn below Y -7", recovery,
                new Vector3(0,-7.2f,390), new Vector3(70,.1f,810), danger, false);
            below.GetComponent<Renderer>().enabled = false;
            Label(recovery, "CP0 > CP1 > CP2 > CP3 > CP4 > CP5 > THRONE",
                new Vector3(0,12,-12), .25f, gold.color);

            var playersRoot = Group("Players", null);
            var template = CreatePlayerTemplate();
            map.players = new PlayerIdentity[12];
            for (int i=0; i<12; i++)
            {
                var player = (GameObject)PrefabUtility.InstantiatePrefab(template);
                player.transform.SetParent(playersRoot);
                player.transform.position = startPoints[i].position;
                var identity = player.GetComponent<PlayerIdentity>();
                identity.playerId = i;
                identity.team = (TeamId)(i/6);
                identity.piece = (PieceType)(i%6);
                player.name = identity.DisplayName;
                player.GetComponent<PlayerMotor>().map = map;
                var material = i<6 ? blue : red;
                foreach (var renderer in player.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = material;
                PieceMarker(player.transform, identity.piece, material);
                Label(player.transform, identity.DisplayName, player.transform.position + Vector3.up*2.9f,
                    .25f, material.color);
                map.players[i] = identity;
            }
            var cameras = Group("Cameras", null);
            var cam = Group("Main Camera", cameras).gameObject.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 65;
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 350;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.transform.position = new Vector3(-16,8,-16);
            cam.transform.rotation = Quaternion.Euler(30,0,0);
            cam.gameObject.AddComponent<AudioListener>();
            cam.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var local = Group("LocalPlaySession", systems).gameObject.AddComponent<LocalPlaySession>();
            local.map = map;
            local.viewCamera = cam;
            local.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var hud = Group("UI", null).gameObject.AddComponent<MatchHud>();
            hud.session = local;
            hud.match = match;

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            // Preserve the original scene entry; make the playable map the first build scene.
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath,true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            FrameMap();
            Validate();
            Debug.Log("[CHESS FIGHT] Graybox ready: " + ScenePath);
        }

        static Material Mat(string name, Color color)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = name;
            material.SetColor("_BaseColor",color);
            material.SetFloat("_Smoothness",.18f);
            AssetDatabase.CreateAsset(material,path);
            return material;
        }

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent,false);
            return go.transform;
        }

        static GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collision=true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent,false);
            go.transform.position = position;
            var ps = parent != null ? parent.lossyScale : Vector3.one;
            go.transform.localScale = new Vector3(scale.x / ps.x, scale.y / ps.y, scale.z / ps.z);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool collision=true)
            => Shape(name,parent,PrimitiveType.Cube,pos,size,mat,collision);

        static void Floor(Transform p,float x,float z0,float z1,float width)
        {
            Box("Floor",p,new Vector3(x,-.5f,(z0+z1)/2),new Vector3(width,1,z1-z0),dark);
            const float tile=4;
            int row=0;
            for(float z=z0;z<z1-.01f;z+=tile,row++)
            {
                int col=0;
                for(float xx=x-width/2;xx<x+width/2-.01f;xx+=tile,col++)
                {
                    if((row+col)%2!=0)continue;
                    float w=Mathf.Min(tile,x+width/2-xx), d=Mathf.Min(tile,z1-z);
                    Box("BoardTile",p,new Vector3(xx+w/2,.008f,z+d/2),new Vector3(w,.015f,d),light,false);
                }
            }
        }

        static void Rails(Transform p,float x,float z0,float z1,float width,Material mat)
        {
            foreach(int side in new[]{-1,1})
            {
                Box("Boundary",p,new Vector3(x+side*(width/2+.3f),1.6f,(z0+z1)/2),
                    new Vector3(.6f,3.2f,z1-z0),stone);
                Box("Team stripe",p,new Vector3(x+side*(width/2+.31f),3.23f,(z0+z1)/2),
                    new Vector3(.65f,.1f,z1-z0),mat,false);
            }
        }
        static void Label(Transform parent,string text,Vector3 pos,float size,Color color,bool ground=false)
        {
            var t=Group(text,parent);
            t.position=pos;
            if(ground)t.rotation=Quaternion.Euler(90,0,0);
            var mesh=t.gameObject.AddComponent<TextMesh>();
            mesh.text=text;
            mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize=64;
            mesh.characterSize=size*.55f;
            mesh.anchor=TextAnchor.MiddleCenter;
            mesh.alignment=TextAlignment.Center;
            mesh.color=color;
            var renderer=t.GetComponent<MeshRenderer>();
            renderer.sharedMaterial=mesh.font.material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        static void Banner(Transform p,string text,float x,float z,float width,Material mat)
        {
            Box("Banner",p,new Vector3(x,8,z),new Vector3(width,2,.5f),dark,false);
            Label(p,text,new Vector3(x,8,z-.3f),.9f,mat.color);
        }

        static void Pawn(Transform parent,float x,float z,float radius=1.8f)
        {
            Shape("Pawn base",parent,PrimitiveType.Cylinder,new Vector3(x,.3f,z),new Vector3(radius*2,.3f,radius*2),stone);
            Shape("Pawn body",parent,PrimitiveType.Cylinder,new Vector3(x,1.8f,z),new Vector3(radius*1.25f,1.2f,radius*1.25f),light);
            Shape("Pawn crown",parent,PrimitiveType.Cylinder,new Vector3(x,3.4f,z),new Vector3(radius*1.7f,.4f,radius*1.7f),stone);
        }

        static ObstacleMotion Motion(GameObject go,MotionKind kind,Vector3 axis,float distance,float period,float phase=0)
        {
            go.AddComponent<Rigidbody>().isKinematic=true;
            var motion=go.AddComponent<ObstacleMotion>();
            motion.kind=kind; motion.axis=axis; motion.distance=distance;
            motion.period=period; motion.phase=phase; motion.match=match;
            return motion;
        }

        static void JumpBar(Transform parent,float x,float z,float length,float speed)
        {
            var pivot=Group("Rotating jump bar",parent);
            pivot.position=new Vector3(x,.65f,z);
            Box("Bar",pivot,new Vector3(x,.65f,z),new Vector3(length,.32f,.45f),danger);
            Shape("Hub",pivot,PrimitiveType.Cylinder,new Vector3(x,.65f,z),new Vector3(.85f,.5f,.85f),stone);
            var motion=Motion(pivot.gameObject,MotionKind.Rotate,Vector3.up,0,4);
            motion.degreesPerSecond=speed;
        }

        static void Gate(Transform parent,float x,float z,float width,float phase)
        {
            var go=Box("Rook gate",parent,new Vector3(x,1.5f,z),new Vector3(width,3,.85f),stone);
            for(int i=-1;i<=1;i++)
                Box("Battlement",go.transform,new Vector3(x+i*width*.32f,3.25f,z),new Vector3(width*.16f,.6f,.9f),light);
            Motion(go,MotionKind.Gate,Vector3.up,4.6f,5,phase);
            Label(parent,"TIMED GATE",new Vector3(x,6,z-.6f),.4f,gold.color);
        }

        static void ExitFunnel(Transform parent,float z,float width=56)
        {
            Box("Left exit wall",parent,new Vector3(-(width+8)/4,3,z),new Vector3((width-8)/2,6,1.2f),stone);
            Box("Right exit wall",parent,new Vector3((width+8)/4,3,z),new Vector3((width-8)/2,6,1.2f),stone);
            Box("Exit left flank",parent,new Vector3(-4.5f,2,z+5),new Vector3(1,4,10),stone);
            Box("Exit right flank",parent,new Vector3(4.5f,2,z+5),new Vector3(1,4,10),stone);
            Label(parent,"SHARED EXIT",new Vector3(0,6.2f,z-.8f),.55f,gold.color);
        }

        static void Checkpoint(Transform parent,int index,float z,float x,float width,bool split,TeamId team)
        {
            var cp=Group("CP"+index+(split ? "_"+team : "_Shared"),parent);
            cp.position=new Vector3(x,0,z);
            var trigger=cp.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger=true;
            trigger.center=new Vector3(0,4,0);
            trigger.size=new Vector3(width,16,2);
            var gate=cp.gameObject.AddComponent<CheckpointGate>();
            gate.index=index; gate.teamRestricted=split; gate.team=team;
            Box("Checkpoint strip",cp,new Vector3(x,.035f,z),new Vector3(width,.04f,1),gold,false);
            foreach(int sign in new[]{-1,1})
                Box("Checkpoint post",cp,new Vector3(x+sign*(width/2-.3f),2.3f,z),new Vector3(.4f,4.6f,.4f),gold,false);
            Label(cp,"CP "+index,new Vector3(x,4.8f,z-.2f),.65f,gold.color);
            gate.blueRecovery=Group("BlueRecovery",cp);
            gate.redRecovery=Group("RedRecovery",cp);
            float bx=split?x:width<=10?-2: -10;
            float rx=split?x:width<=10?2:10;
            gate.blueRecovery.position=new Vector3(bx,.1f,z+4);
            gate.redRecovery.position=new Vector3(rx,.1f,z+4);
            gates.Add(gate);
        }

        static void Zone1(Transform parent)
        {
            var zone=Group("Zone01_Control_0-150m",parent);
            Banner(zone,"01  /  CONTROL",0,10,50,light);
            foreach(int side in new[]{-1,1})
            {
                float x=side*16;
                var route=Group(side<0?"BlueRoute":"RedRoute",zone);
                var mat=side<0?blue:red;
                Floor(route,x,0,22,14); Floor(route,x,24,150,14);
                Rails(route,x,0,150,14,mat);
                Label(route,"JUMP",new Vector3(x,.05f,18),.7f,danger.color,true);
                for(int i=0;i<4;i++)Pawn(route,x+(i%2==0?-3:3),42+i*15);
                JumpBar(route,x,103,11,65);
                Gate(route,x,119,13.7f,0);
                var tile=Box("Rising chess tile",route,new Vector3(x,.25f,137),new Vector3(8,.5f,5),light);
                Motion(tile,MotionKind.Translate,Vector3.up,.55f,4).shove=false;
                Checkpoint(zone,1,150,x,14,true,side<0?TeamId.Blue:TeamId.Red);
            }
        }

        static void Zone2(Transform parent)
        {
            var zone=Group("Zone02_FirstClash_150-250m",parent);
            Floor(zone,0,150,250,56); Rails(zone,0,150,250,56,stone);
            Banner(zone,"02  /  FIRST CLASH",0,164,48,gold);
            for(int side=-1;side<=1;side+=2)
            {
                var wall=Box("Converging wall",zone,new Vector3(side*18,2,168),new Vector3(20,4,1.5f),stone);
                wall.transform.rotation=Quaternion.Euler(0,side*28,0);
            }
            var a=Box("Moving rook wall A",zone,new Vector3(0,1.6f,188),new Vector3(11,3.2f,2),stone);
            Motion(a,MotionKind.Translate,Vector3.right,12,6);
            var b=Box("Moving rook wall B",zone,new Vector3(0,1.6f,213),new Vector3(11,3.2f,2),stone);
            Motion(b,MotionKind.Translate,Vector3.right,12,6,.5f);
            JumpBar(zone,0,201,18,38);
            Pawn(zone,-20,206,2.2f); Pawn(zone,20,206,2.2f);
            ExitFunnel(zone,239);
            Checkpoint(zone,2,250,0,8,false,TeamId.Blue);
        }

        static void Zone3(Transform parent)
        {
            var zone=Group("Zone03_PieceTerrain_250-430m",parent);
            Floor(zone,0,250,430,56); Rails(zone,0,250,430,56,stone);
            Banner(zone,"03  /  USE THE TERRAIN",0,264,49,light);
            Label(zone,"STRAIGHT",new Vector3(-18,.05f,272),.65f,blue.color,true);
            Label(zone,"DIAGONAL",new Vector3(0,.05f,272),.65f,gold.color,true);
            Label(zone,"ELEVATION",new Vector3(18,.05f,272),.65f,red.color,true);
            var straight=Group("StraightLane_and_SideCover",zone);
            for(int i=0;i<6;i++)
            {
                Box("Side cover",straight,new Vector3(-24,1.25f,287+i*18),new Vector3(3,2.5f,5),stone);
                Box("Side cover",straight,new Vector3(-11,1.25f,296+i*18),new Vector3(2,2.5f,5),stone);
            }
            var diagonal=Group("DiagonalColumns",zone);
            for(int i=0;i<7;i++)
                for(int j=-1;j<=1;j++)
                {
                    float x=j*6+(i%2)*3-1.5f;
                    Shape("Diagonal column",diagonal,PrimitiveType.Cylinder,new Vector3(x,2,288+i*15+j*3),
                        new Vector3(2.4f,2,2.4f),stone);
                }
            var steps=Group("LowWalls_and_Steps",zone);
            for(int i=0;i<7;i++)
            {
                float y=i%3==0?.35f:i%3==1?.65f:.9f;
                Box("Jump step",steps,new Vector3(18,y/2,288+i*15),new Vector3(10,y,5),light);
                if(i%2==0)Box("Low wall",steps,new Vector3(16,.55f,294+i*15),new Vector3(6,1.1f,1),stone);
            }
            var escort=Group("Escort_crossing_and_Queen_plaza",zone);
            Box("Cross cover L",escort,new Vector3(-15,1.4f,406),new Vector3(14,2.8f,2),stone);
            Box("Cross cover R",escort,new Vector3(15,1.4f,416),new Vector3(14,2.8f,2),stone);
            Label(escort,"ALL PIECES CAN PASS",new Vector3(0,.05f,403),.7f,gold.color,true);
            Checkpoint(zone,3,430,0,56,false,TeamId.Blue);
        }

        static void Zone4(Transform parent)
        {
            var zone=Group("Zone04_SecondClash_430-540m",parent);
            Floor(zone,0,430,444,56); Floor(zone,0,446,540,56);
            Rails(zone,0,430,540,56,stone);
            Banner(zone,"04  /  HOLD THE LINE",0,443,49,danger);
            Label(zone,"JUMP",new Vector3(0,.05f,440),.7f,danger.color,true);
            var board=Group("Rotating chessboard",zone);
            board.position=new Vector3(0,.55f,480);
            Box("Board",board,board.position,new Vector3(23,.5f,23),dark);
            for(int i=-2;i<=2;i++)for(int j=-2;j<=2;j++)
                if((i+j)%2==0)Box("Tile",board,new Vector3(i*4.5f,.81f,480+j*4.5f),
                    new Vector3(4.45f,.025f,4.45f),light,false);
            Motion(board.gameObject,MotionKind.Rotate,Vector3.up,0,5).degreesPerSecond=18;
            JumpBar(zone,-17,462,13,-55); JumpBar(zone,17,495,13,55);
            var block=Box("Rook blocker",zone,new Vector3(0,1.7f,514),new Vector3(15,3.4f,2),stone);
            Motion(block,MotionKind.Translate,Vector3.right,11,5);
            ExitFunnel(zone,529);
            Checkpoint(zone,4,540,0,8,false,TeamId.Blue);
        }

        static void Zone5(Transform parent)
        {
            var zone=Group("Zone05_FinalControl_540-690m",parent);
            Floor(zone,0,540,558,56); Rails(zone,0,540,558,56,stone);
            Banner(zone,"05  /  FINAL CONTROL",0,551,49,light);
            foreach(int side in new[]{-1,1})
            {
                float x=side*16;
                var route=Group(side<0?"BlueRoute":"RedRoute",zone);
                var mat=side<0?blue:red;
                Floor(route,x,558,564,14); Floor(route,x,566,690,14);
                Rails(route,x,558,690,14,mat);
                Gate(route,x-3.5f,590,6.9f,0);
                Gate(route,x+3.5f,590,6.9f,.5f);
                for(int i=0;i<3;i++)
                {
                    var tile=Box("Moving platform",route,new Vector3(x,.35f,608+i*7),new Vector3(8,.7f,4),light);
                    Motion(tile,MotionKind.Translate,Vector3.right,2.6f,4,i*.2f).shove=false;
                }
                Gate(route,x-3.5f,642,6.9f,.5f);
                Gate(route,x+3.5f,642,6.9f,0);
                JumpBar(route,x,662,12,85);
                Pawn(route,x-3,676,1.5f);
                Checkpoint(zone,5,690,x,14,true,side<0?TeamId.Blue:TeamId.Red);
            }
        }

        static void Zone6(Transform parent)
        {
            var zone=Group("Zone06_FinalThrone_690-790m",parent);
            var plaza=Group("FinalPlaza",zone);
            Floor(plaza,0,690,738,56); Rails(plaza,0,690,738,56,stone);
            Banner(plaza,"06  /  ONE THRONE",0,703,49,gold);
            Box("Central cover",plaza,new Vector3(0,2,725),new Vector3(14,4,6),stone);
            foreach(int side in new[]{-1,1})
            {
                var route=Group(side<0?"LeftApproach":"RightApproach",zone);
                float x=side*12;
                Floor(route,x,738,766,10); Rails(route,x,738,766,10, gold);
                Label(route,side<0?"LEFT":"RIGHT",new Vector3(x,.05f,745),.8f,gold.color,true);
            }
            var deck=Group("ThroneDeck",zone);
            Floor(deck,0,766,790,38); Rails(deck,0,766,790,38,gold);
            Box("Rear boundary",deck,new Vector3(0,2,790),new Vector3(38,4,1),stone);
            Box("Podium",deck,new Vector3(0,.25f,781),new Vector3(10,.5f,8),light);
            var throne=Group("SharedThrone",deck);
            throne.position=new Vector3(0,0,777);
            Box("Seat",throne,new Vector3(0,1.1f,781),new Vector3(3,1.2f,2.5f),gold);
            Box("Backrest",throne,new Vector3(0,3,782.2f),new Vector3(3.3f,4.8f,.6f),gold);
            Box("Left arm",throne,new Vector3(-1.75f,1.8f,781),new Vector3(.5f,2.5f,3),stone);
            Box("Right arm",throne,new Vector3(1.75f,1.8f,781),new Vector3(.5f,2.5f,3),stone);
            for(int i=-1;i<=1;i++)Box("Crown",throne,new Vector3(i*.95f,5.65f,782.2f),
                new Vector3(.55f,i==0?1.5f:1,.65f),gold);
            Shape("Interaction area",throne,PrimitiveType.Cylinder,new Vector3(0,.035f,777),
                new Vector3(6,.02f,6),gold,false);
            var interact=throne.gameObject.AddComponent<ThroneInteraction>();
            interact.match=match; match.throne=interact;
            Banner(deck,"KING ONLY  /  HOLD E",0,786,32,gold);
        }

        static GameObject CreatePlayerTemplate()
        {
            string path=Root+"/Prefabs/Player_Graybox.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(existing!=null)return existing;
            var player=new GameObject("Player_Graybox");
            player.layer=2;
            var controller=player.AddComponent<CharacterController>();
            controller.height=2;
            controller.radius=.5f;
            controller.center=new Vector3(0,1,0);
            controller.stepOffset=.3f;
            controller.slopeLimit=50;
            controller.skinWidth=.04f;
            player.AddComponent<PlayerIdentity>();
            player.AddComponent<PlayerMotor>();
            Shape("Body",player.transform,PrimitiveType.Cylinder,new Vector3(0,.9f,0),
                new Vector3(.8f,.8f,.8f),stone,false).layer=2;
            Shape("Base",player.transform,PrimitiveType.Cylinder,new Vector3(0,.12f,0),
                new Vector3(1,.12f,1),stone,false).layer=2;
            var prefab=PrefabUtility.SaveAsPrefabAsset(player,path);
            Object.DestroyImmediate(player);
            return prefab;
        }

        static void PieceMarker(Transform player,PieceType piece,Material mat)
        {
            var top=Group("Piece marker - "+piece,player);
            var origin=player.position;
            float h=1.85f;
            if(piece==PieceType.King)
            {
                Box("King cross V",top,origin+new Vector3(0,h+.25f,0),new Vector3(.18f,.8f,.18f),mat,false);
                Box("King cross H",top,origin+new Vector3(0,h+.38f,0),new Vector3(.6f,.17f,.18f),mat,false);
            }
            else if(piece==PieceType.Queen)
            {
                for(int i=-1;i<=1;i++)Box("Crown point",top,origin+new Vector3(i*.22f,h+.2f,0),
                    new Vector3(.13f,.5f,.22f),mat,false);
            }
            else if(piece==PieceType.Rook)
                Box("Rook crown",top,origin+new Vector3(0,h+.1f,0),new Vector3(.85f,.32f,.85f),mat,false);
            else if(piece==PieceType.Knight)
            {
                Box("Knight neck",top,origin+new Vector3(0,h+.1f,0),new Vector3(.35f,.6f,.35f),mat,false);
                Box("Knight head",top,origin+new Vector3(0,h+.38f,.2f),new Vector3(.35f,.25f,.6f),mat,false);
            }
            else
                Shape("Crown",top,PrimitiveType.Cylinder,origin+new Vector3(0,h+.12f,0),
                    new Vector3(piece==PieceType.Bishop?.35f:.55f,.22f,piece==PieceType.Bishop?.35f:.55f),mat,false);
        }

        [MenuItem("CHESS FIGHT/02 Frame Entire Map")]
        public static void FrameMap()
        {
            if(SceneView.lastActiveSceneView!=null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,390),Quaternion.Euler(65,-25,0),420);
        }

        [MenuItem("CHESS FIGHT/03 Validate Graybox")]
        public static void Validate()
        {
            var stage=Object.FindFirstObjectByType<StageMap>();
            if(stage==null)throw new Exception("StageMap missing");
            var errors=new List<string>();
            void Check(bool valid,string message) { if(!valid)errors.Add(message); }
            Check(stage.players.Length==12,"Must have 12 player slots");
            foreach(TeamId team in Enum.GetValues(typeof(TeamId)))
                foreach(PieceType piece in Enum.GetValues(typeof(PieceType)))
                    Check(stage.players.Count(p=>p.team==team&&p.piece==piece)==1,"Roster mismatch: "+team+" "+piece);
            Check(stage.startPoints.Length==12,"Must have 12 CP0 spawn points");
            for(int cp=1;cp<=5;cp++)
                foreach(TeamId team in Enum.GetValues(typeof(TeamId)))
                {
                    var gate=stage.FindCheckpoint(cp,team);
                    Check(gate!=null,"Missing CP "+cp+" "+team);
                    if(gate!=null)Check(gate.blueRecovery!=null&&gate.redRecovery!=null,"Recovery anchor missing");
                }
            Check(Object.FindObjectsByType<ThroneInteraction>(FindObjectsSortMode.None).Length==1,"Exactly one shared throne required");
            Check(stage.transform.childCount==7,"Start plus six zones required");
            Check(GameObject.Find("LeftApproach")!=null&&GameObject.Find("RightApproach")!=null,"Two throne approaches required");
            Check(stage.match.throne.holdSeconds>=1.5f&&stage.match.throne.holdSeconds<=2,"Throne duration range");
            Check(Object.FindFirstObjectByType<LocalPlaySession>().inputActions!=null,"Existing input asset is not connected");
            Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1,"Exactly one camera required");
            foreach(var t in stage.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script: "+t.name);
            var report="CHESS FIGHT STATIC VALIDATION\n"+DateTime.Now.ToString("s")+"\n"+
                (errors.Count==0?"PASS":"FAIL")+"\nPlayers: "+stage.players.Length+"\nCheckpoints: "+stage.checkpoints.Length+
                "\nObstacles: "+Object.FindObjectsByType<ObstacleMotion>(FindObjectsSortMode.None).Length+
                "\nMap extent: Z -10 to 790 (800 units)\nMode: local prototype, no networking\n"+string.Join("\n",errors);
            Directory.CreateDirectory("Artifacts/ChessFight");
            File.WriteAllText("Artifacts/ChessFight/StaticValidation.txt",report);
            if(errors.Count>0)throw new Exception(report);
            Debug.Log(report);
        }
    }
}
