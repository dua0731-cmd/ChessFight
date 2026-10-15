using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChessFight.Gameplay.PawnRush;
using ChessFight.ProtectKing;
using ChessFight.RagdollLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.Maps.Editor
{
    // Explicit authoring only. Existing hand-edited assets are never rebuilt on import.
    public static class RoyalFountainHallBuilder
    {
        public const string Id = "O05_RoyalFountainHall";
        public const string PrefabPath = "Assets/Maps/PawnRush/Sections/Obstacles/" + Id + ".prefab";
        public const string Art = "Assets/Maps/PawnRush/RoyalFountainHall";
        public const string TestName = "RoyalFountainHall_Test";
        static Material stone, ivory, slate, blue, red, gold, iron, water, foam, leaf, flower, amber, flame;
        static Mesh bevel, arch, ring, crown, pawnMesh, kingMesh;
        static Transform geometry, hazards, recovery, architecture, decor, waters, read;
        public static readonly Vector3[] Left = Points(new float[]{0,0, 0,24, -8,34, -14,42, -17,52, -10,64, -17,76, -10,88, -14,100, -7,110, 0,118, 0,144});
        public static readonly Vector3[] Right = Points(new float[]{0,0, 0,24, 8,34, 14,42, 16,54, 11,66, 16,78, 11,90, 14,100, 7,110, 0,118, 0,144});
        public static readonly List<Vector3> RecoveryStarts = new List<Vector3>();
        public static readonly List<Vector3> RecoveryEnds = new List<Vector3>();
        static Vector3[] Points(float[] values) {var p=new Vector3[values.Length/2];for(int i=0;i<p.Length;i++)p[i]=new Vector3(values[2*i],0,values[2*i+1]);return p;}

        [MenuItem("ChessFight/Pawn Rush/왕궁 분수 회랑/1 프리팹 생성 또는 선택")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before authoring.");
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null){Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);Validate();return;}
            Folder(Art+"/Materials");Folder(Art+"/Meshes");
            Materials();Meshes();
            var root=new GameObject(Id);
            try {
                var section=root.AddComponent<PawnRushSection>();section.sectionId=Id;section.kind=SectionKind.Obstacle;
                section.entrance=Child("Entrance",root.transform,Vector3.zero);section.exit=Child("Exit",root.transform,new Vector3(0,0,144));
                section.authoringBounds=new Bounds(new Vector3(0,8,72),new Vector3(52,28,144));
                geometry=Child("Geometry",root.transform,Vector3.zero);hazards=Child("Obstacles",root.transform,Vector3.zero);
                recovery=Child("Recovery",root.transform,Vector3.zero);architecture=Child("Architecture",root.transform,Vector3.zero);
                decor=Child("Decoration",root.transform,Vector3.zero);waters=Child("WaterVisuals",root.transform,Vector3.zero);read=Child("Readability",root.transform,Vector3.zero);
                Commons();Bridges();Palace();Fountain();Gallery();
                // Static decorative renderers are merged per material and region; collision stays simple.
                foreach(var group in new[]{geometry,architecture,decor,waters,read,recovery})
                    foreach(Transform region in group.Cast<Transform>().ToArray()) Combine(region);
                section.ValidateDefinition();
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
                if(prefab==null)throw new IOException("Prefab save failed.");
                AssetDatabase.SaveAssets();Selection.activeObject=prefab;
            } finally {Object.DestroyImmediate(root);}
            Validate();Debug.Log("Royal Fountain Hall prefab created. Existing scene and Catalog were preserved.");
        }
        static void Materials()
        {
            stone=Mat("WarmLimestone",new Color(.71f,.65f,.53f));ivory=Mat("IvoryMarble",new Color(.94f,.89f,.76f));
            slate=Mat("MidnightMarble",new Color(.13f,.20f,.27f));blue=Mat("AzureVelvet",new Color(.025f,.22f,.64f));red=Mat("CrimsonVelvet",new Color(.66f,.035f,.075f));
            gold=Mat("BrushedGold",new Color(.88f,.53f,.12f),.65f,.38f);iron=Mat("DarkBronze",new Color(.12f,.10f,.075f),.45f);
            water=Mat("TurquoiseWater",new Color(.015f,.29f,.45f),.15f,.78f);foam=Mat("WaterHighlights",new Color(.33f,.79f,.87f),0,.48f,.12f);
            leaf=Mat("IvyGreen",new Color(.10f,.26f,.07f));flower=Mat("RoyalFlowers",new Color(.67f,.37f,.74f));
            amber=Mat("WarningAmber",new Color(1,.46f,.025f),0,.2f,.5f);flame=Mat("BrazierFlame",new Color(1,.53f,.065f),0,.25f,1.4f);
        }
        static Material Mat(string name,Color color,float metal=0,float smooth=.18f,float emission=0)
        {
            var shader=Shader.Find(GraphicsSettings.currentRenderPipeline!=null?"Universal Render Pipeline/Lit":"Standard");
            if(shader==null)throw new InvalidOperationException("No compatible shader. Render pipeline is unchanged.");
            var m=new Material(shader){name=name,color=color};m.SetFloat("_Metallic",metal);m.SetFloat(m.HasProperty("_Smoothness")?"_Smoothness":"_Glossiness",smooth);
            if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}
            AssetDatabase.CreateAsset(m,Art+"/Materials/"+name+".mat");return m;
        }
        static void Meshes()
        {
            // Chamfered box, curved architectural arch, turned chess silhouettes and crown heraldry.
            bevel=Save(BeveledBox(),"ChamferedStone");arch=Save(ArchMesh(),"RomanArch");ring=Save(Torus(1,.07f,48,6),"TrimRing");
            crown=Save(Extrude(new[]{new Vector2(-.5f,0),new Vector2(.5f,0),new Vector2(.65f,.65f),new Vector2(.25f,.39f),new Vector2(0,.85f),new Vector2(-.25f,.39f),new Vector2(-.65f,.65f)},.06f),"CrownEmblem");
            pawnMesh=Save(Lathe(new[]{new Vector2(.58f,0),new Vector2(.65f,.1f),new Vector2(.57f,.23f),new Vector2(.39f,.32f),new Vector2(.21f,.85f),new Vector2(.34f,.98f),new Vector2(.24f,1.12f),new Vector2(.36f,1.32f),new Vector2(.40f,1.55f),new Vector2(.32f,1.77f),new Vector2(0,1.91f)},24),"TurnedPawn");
            kingMesh=Save(Lathe(new[]{new Vector2(.75f,0),new Vector2(.83f,.15f),new Vector2(.72f,.35f),new Vector2(.5f,.48f),new Vector2(.29f,1.28f),new Vector2(.52f,1.39f),new Vector2(.39f,1.55f),new Vector2(.37f,1.92f),new Vector2(.48f,2.08f),new Vector2(0,2.10f)},32),"TurnedKing");
        }
        static void Commons()
        {
            var entry=Child("EntryConnector",geometry,Vector3.zero);Deck(entry,0,12,20);Carpet(entry,0,6,4,11,red);
            var hall=Child("EntranceHall",geometry,Vector3.zero);Deck(hall,12,30,28);Carpet(hall,0,21,4,18,red);
            var branch=Child("BranchPlaza",geometry,Vector3.zero);Deck(branch,30,40,32);Carpet(branch,0,34,4,8,red);
            var merge=Child("MergePlaza",geometry,Vector3.zero);Deck(merge,108,120,22);Carpet(merge,0,114,4,12,red);
            var exit=Child("ExitConnector",geometry,Vector3.zero);Deck(exit,132,144,20);Carpet(exit,0,138,4,12,red);
            var symbols=Child("RouteSymbols",read,Vector3.zero);
            foreach(float z in new[]{6f,19,29,114,135,141})Arrow(symbols,new Vector3(0,.045f,z),Vector3.forward,1.1f);
            foreach(int side in new[]{-1,1}){
                Strip(symbols,new Vector3(side*4,.034f,28),new Vector3(side*13,.034f,40),1.8f,side<0?blue:red);
                Arrow(symbols,new Vector3(side*8,.052f,34),new Vector3(side*.5f,0,1),.85f);
                foreach(var span in new[]{new Vector2(1,28),new Vector2(112,143)})
                    Rail(entry,new Vector3(side*(span.x<10?14:11),0,span.x),new Vector3(side*(span.x<10?14:11),0,span.y),1.05f);
            }
        }
        static void Deck(Transform p,float a,float b,float width)
        {
            Cube("Stone foundation",p,new Vector3(0,-.4f,(a+b)/2),new Vector3(width,.8f,b-a),stone);
            CheckerRect(p,Vector3.zero,width,a,b);
        }
        static void CheckerRect(Transform p,Vector3 origin,float width,float a,float b)
        {
            for(float z=a;z<b-.01f;z+=2)for(float x=-width/2;x<width/2-.01f;x+=2){
                float w=Mathf.Min(2,width/2-x),d=Mathf.Min(2,b-z);
                Cube("Marble inlay",p,origin+new Vector3(x+w/2,.012f,z+d/2),new Vector3(w-.025f,.018f,d-.025f),(((int)((x+width/2)/2)+(int)((z-a)/2))%2==0)?ivory:slate,false);
            }
        }
        static void Carpet(Transform p,float x,float z,float w,float len,Material color)
        {
            Cube("Velvet runner",p,new Vector3(x,.031f,z),new Vector3(w,.015f,len),color,false);
            foreach(int s in new[]{-1,1})Cube("Embroidered edge",p,new Vector3(x+s*(w/2-.10f),.041f,z),new Vector3(.10f,.012f,len),gold,false);
            for(float at=z-len/2+3;at<z+len/2;at+=6)FloorCrown(p,new Vector3(x,.048f,at),1.25f);
        }
        static void Bridges()
        {
            var left=Child("LeftZigzagBridge",geometry,Vector3.zero);var right=Child("RightCurvedBridge",geometry,Vector3.zero);
            var tiles=Child("LeftCrumblingTiles",hazards,Vector3.zero);var discs=Child("CircularPlatforms",hazards,Vector3.zero);
            var mallets=Child("RightPendulumHammers",hazards,Vector3.zero);
            // Exact straight cuts leave no collider or decorative carpet over the missing bands.
            for(int i=2;i<10;i++){
                var a=Left[i];var b=Left[i+1];bool patch=i==4||i==5||i==6;
                if(patch){
                    var dir=(b-a).normalized;float len=Vector3.Distance(a,b),start=len*.5f-1.95f;
                    WalkStrip(left,a,a+dir*start,5,blue);
                    WalkStrip(left,a+dir*(start+1.2f),a+dir*(start+2.7f),5,blue);
                    WalkStrip(left,a+dir*(start+3.9f),b,5,blue);
                    for(int band=0;band<2;band++)for(int lane=0;lane<2;lane++)
                        Tile(tiles,a+dir*(start+.6f+band*2.7f)+Vector3.Cross(Vector3.up,dir)*((lane==0?-1:1)*1.25f),dir,i,band,lane);
                    RecoveryBay("LeftBay_"+i,a+dir*(start+1.95f),dir,5);
                } else WalkStrip(left,a,b,5,blue);
                if(i>=3&&i<9&&i!=3&&i!=8){Cube("Fixed corner refuge",left,a+Vector3.down*.35f,new Vector3(5.8f,.7f,5.8f),ivory);Cube("Blue corner inlay",left,a+Vector3.up*.018f,new Vector3(3.6f,.025f,3.6f),blue,false);}
            }
            var smooth=Smooth(Right.Skip(2).Take(9).ToArray(),8);
            Ribbon(right,"Curved marble bridge",smooth,5.8f,.7f,stone,true);
            Ribbon(right,"Crimson route runner",smooth,2.15f,.02f,red,false,.025f);
            for(int side=-1;side<=1;side+=2)Ribbon(right,"Continuous gold edge "+side,Offset(smooth,side*2.72f),.13f,.035f,gold,false,.025f);
            foreach(int side in new[]{-1,1})foreach(float z in new[]{42f,100f})Disc(discs,new Vector3(side*14,0,z));
            Hammer(mallets,new Vector3(11.7f,0,64),new Vector3(-5,0,12).normalized,.4f,blue,0);
            Hammer(mallets,new Vector3(12.7f,0,86),new Vector3(-5,0,12).normalized,1.6f,red,1);
            var pools=Child("LowerWaterCourt",waters,Vector3.zero);
            Cube("Decorative water surface",pools,new Vector3(0,-4,72),new Vector3(45,.12f,72),water,false);
            // Discrete waterfalls visually explain the void; none is a WaterZone.
            foreach(int side in new[]{-1,1})for(int i=0;i<6;i++){
                float z=44+i*10;var at=new Vector3(side*21,-2.1f,z);
                Cube("Waterfall veil",pools,at,new Vector3(.10f,3.6f,2.2f),water,false);
                for(int k=0;k<4;k++)Cube("Waterfall glint",pools,at+new Vector3(-side*.07f,0,-.8f+k*.5f),new Vector3(.03f,3.65f,.07f),foam,false);
            }
        }
        static void WalkStrip(Transform p,Vector3 a,Vector3 b,float width,Material color)
        {
            if(Vector3.Distance(a,b)<.01f)return;
            var t=Child("Fixed bridge span",p,(a+b)/2);t.localRotation=Quaternion.LookRotation(b-a);
            float length=Vector3.Distance(a,b);
            Cube("Solid deck",t,new Vector3(0,-.35f,0),new Vector3(width,.7f,length),stone);
            int n=Mathf.CeilToInt(length/1.5f);float step=length/n;
            for(int row=0;row<n;row++)for(int col=0;col<2;col++)
                Cube("Checker slab",t,new Vector3((col==0?-1:1)*width/4,.013f,-length/2+step*(row+.5f)),new Vector3(width/2-.025f,.02f,step-.025f),(row+col)%2==0?ivory:slate,false);
            foreach(int side in new[]{-1,1})Cube("Gold bridge lip",t,new Vector3(side*(width/2-.08f),.027f,0),new Vector3(.10f,.035f,length),gold,false);
            if(length>5)Arrow(t,new Vector3(0,.038f,0),Vector3.forward,.8f);
        }
        static void Tile(Transform p,Vector3 at,Vector3 dir,int patch,int band,int lane)
        {
            var t=Child($"CrackingTile_{patch}_{band}_{lane}",p,at);t.localRotation=Quaternion.LookRotation(dir);
            var floor=t.gameObject.AddComponent<CrumblingRoyalFloor>();floor.context=t.GetComponent<ObstacleContext>();
            floor.CrackDuration=1.6f;floor.CollapseVisualDuration=.25f;floor.MissingDuration=2;floor.RestoreWarningDuration=.6f;floor.VisualDrop=1.2f;
            floor.SolidCollider=t.gameObject.AddComponent<BoxCollider>();floor.SolidCollider.center=new Vector3(0,-.17f,0);floor.SolidCollider.size=new Vector3(2.5f,.34f,1.2f);
            floor.Visual=Child("Visual",t,Vector3.zero);Cube("Marble slab",floor.Visual,new Vector3(0,-.17f,0),new Vector3(2.48f,.34f,1.18f),lane==band?ivory:slate,false);
            var cracks=Child("CrackVisual",floor.Visual,Vector3.zero);floor.CrackVisual=cracks.gameObject;
            foreach(int side in new[]{-1,1})Strip(cracks,new Vector3(-.9f,.016f,side*.43f),new Vector3(.65f,.016f,-side*.18f),.045f,amber);
            cracks.gameObject.SetActive(false);
            var warn=Child("WarningOutline",t,Vector3.zero);floor.WarningVisual=warn.gameObject;
            foreach(int side in new[]{-1,1}){
                Cube("Amber seam",warn,new Vector3(side*1.21f,.022f,0),new Vector3(.055f,.025f,1.15f),amber,false);
                Cube("Amber seam",warn,new Vector3(0,.022f,side*.56f),new Vector3(2.45f,.025f,.055f),amber,false);
            }warn.gameObject.SetActive(false);
            floor.OccupancyArea=Volume("OccupancyArea",t,new Vector3(0,.45f,0),new Vector3(2.5f,1.1f,1.2f));
            floor.RestoreClearanceArea=Volume("RestoreClearanceArea",t,new Vector3(0,.35f,0),new Vector3(2.5f,1.5f,1.2f));
            floor.VFXPoint=Child("VFXPoint",t,Vector3.up*.06f);
            Combine(floor.Visual,false); // Keep crack visibility independently controlled.
        }
        static BoxCollider Volume(string name,Transform parent,Vector3 center,Vector3 size)
        {var box=Child(name,parent,center).gameObject.AddComponent<BoxCollider>();box.size=size;box.isTrigger=true;box.enabled=false;return box;}
        static void Disc(Transform p,Vector3 at)
        {
            var t=Child("RoyalTurntable_"+at.x+"_"+at.z,p,at);
            var surface=t.gameObject.AddComponent<ObstacleSurface>();surface.context=t.gameObject.AddComponent<ObstacleContext>();surface.kind=ObstacleSurfaceKind.SpinningDisc;
            surface.radius=4;surface.centerSpeed=1.2f;surface.edgeSpeed=.5f;surface.outwardSpeed=.15f;surface.rotors=Array.Empty<Transform>();
            Cylinder("Solid circular deck",t,new Vector3(0,-.36f,0),8,.72f,stone,true);
            Cylinder("Ivory circular paving",t,new Vector3(0,.01f,0),7.8f,.02f,ivory,false);
            Cylinder("Dark central inlay",t,new Vector3(0,.025f,0),5.8f,.02f,slate,false);
            Ring(t,new Vector3(0,.05f,0),3.8f,gold);Ring(t,new Vector3(0,.05f,0),2.8f,gold);
            FloorCrown(t,new Vector3(0,.055f,0),2.4f);
            // Surface provides the sole physical velocity. Only the filigree rotates visually.
            var filigree=Child("RotatingFiligree",t,Vector3.zero);
            for(int j=0;j<8;j++){float a=j*Mathf.PI/4;Arrow(filigree,new Vector3(Mathf.Cos(a)*3.3f,.052f,Mathf.Sin(a)*3.3f),new Vector3(Mathf.Sin(a),0,-Mathf.Cos(a)),.3f);}
            Body(filigree);var spin=filigree.gameObject.AddComponent<ObstacleMotion>();spin.kind=MotionKind.Rotate;spin.axis=Vector3.up;spin.degreesPerSecond=10;spin.shove=false;
            Combine(filigree); // No Collider and no moving Rigidbody floor.
        }
        static void Hammer(Transform p,Vector3 at,Vector3 forward,float delay,Material color,int number)
        {
            var t=Child("RoyalPendulum_"+number,p,at);t.localRotation=Quaternion.LookRotation(forward);
            var context=t.gameObject.AddComponent<ObstacleContext>();
            var pivot=Child("Pivot",t,new Vector3(0,5.3f,0));Body(pivot);
            var swing=pivot.gameObject.AddComponent<PendulumSwing>();swing.context=context;swing.SwingAngle=32;swing.SwingDuration=2.4f;swing.StartDelay=delay;swing.PauseAtEnds=.3f;swing.KnockbackForce=4.5f;swing.UpwardForce=.6f;
            var chain=Child("Chain",pivot,Vector3.zero);
            for(int i=0;i<10;i++){
                var link=MeshObj("Forged chain link",chain,new Vector3(0,-.22f-i*.32f,0),new Vector3(.22f,.30f,.22f),ring,iron);
                link.localRotation=Quaternion.Euler(90,i%2*90,0);
            }
            Cylinder("Mallet handle",chain,new Vector3(0,-3.35f,0),.26f,1.2f,gold,false);
            var head=Child("Ball",chain,new Vector3(0,-4,0));swing.Ball=head;
            var solid=head.gameObject.AddComponent<CapsuleCollider>();solid.direction=2;solid.height=2.6f;solid.radius=1.1f;
            var drum=Cylinder("Velvet drum",head,Vector3.zero,2.2f,2.6f,color,false);drum.localRotation=Quaternion.Euler(90,0,0);
            foreach(float end in new[]{-1.18f,1.18f}){var cap=Cylinder("Gold end band",head,new Vector3(0,0,end),2.27f,.22f,gold,false);cap.localRotation=Quaternion.Euler(90,0,0);}
            MeshObj("Royal crest",head,new Vector3(0,-.38f,-1.31f),Vector3.one*.8f,crown,gold);
            var hit=Child("HitArea",head,Vector3.zero);var trigger=hit.gameObject.AddComponent<CapsuleCollider>();trigger.direction=2;trigger.radius=1.16f;trigger.height=2.72f;trigger.isTrigger=true;
            var impact=hit.gameObject.AddComponent<ObstacleImpact>();impact.context=context;impact.pendulum=swing;impact.hitCooldown=1.2f;
            var support=Child("Support",t,Vector3.zero);
            foreach(int side in new[]{-1,1})Column(support,new Vector3(side*3.9f,0,0),5.5f,.38f);
            Cube("Support lintel",support,new Vector3(0,5.7f,0),new Vector3(8.5f,.7f,1.1f),stone);
            Cube("Sweep warning inlay",t,new Vector3(0,.065f,0),new Vector3(5.2f,.012f,2.8f),amber,false);
            for(int i=-4;i<=4;i++)Cube("Warning dash",t,new Vector3(i*.58f,.075f,-1.5f),new Vector3(.3f,.015f,.13f),iron,false);
            Combine(chain);Combine(support);
            RecoveryBay("RightBay_"+number,at,forward,5.8f);
        }
        static void RecoveryBay(string name,Vector3 center,Vector3 forward,float bridgeWidth)
        {
            var t=Child(name,recovery,center);t.localRotation=Quaternion.LookRotation(forward);
            // Two isolated landing wings return upstream. No longitudinal lower bypass exists.
            Cube("Catch deck",t,new Vector3(0,-2.8f,1),new Vector3(9,.8f,6),stone);
            foreach(int side in new[]{-1,1}){
                float x=side*3.75f;
                Ramp(t,new Vector3(x,-2.4f,-2),new Vector3(x,0,-11),2.8f);
                Cube("Return landing",t,new Vector3(side*2.2f,-.35f,-12),new Vector3(6,.7f,2.3f),ivory);
                Rail(t,new Vector3(side*5.1f,-2.4f,-2),new Vector3(side*5.1f,-2.4f,4),.85f);
                Arrow(t,new Vector3(x,-2.36f,.2f),Vector3.back,.7f);
                // No ramp is built directly under the bridge; 2.4m head clearance at the catch deck.
            }
            Cube("Downstream bay wall",t,new Vector3(0,-1.65f,4),new Vector3(10.3f,1.5f,.35f),ivory);
            RecoveryStarts.Add(center+Vector3.down*2.4f);RecoveryEnds.Add(center-forward*12);
        }
        static void Gallery()
        {
            var p=Child("SharedGallery",geometry,Vector3.zero);Deck(p,120,132,12);Carpet(p,0,126,4,12,red);
            foreach(int side in new[]{-1,1}){
                Rail(p,new Vector3(side*6,0,120),new Vector3(side*6,0,132),1.15f);
                Cube("Short gallery shoulder",p,new Vector3(side*5,.5f,125),new Vector3(2,1,6),stone);
                Cube("Shoulder coping",p,new Vector3(side*5,1.05f,125),new Vector3(2.1f,.1f,6),gold,false);
            }
            var rook=Child("GalleryRook",hazards,new Vector3(0,.35f,125));Body(rook);
            var motion=rook.gameObject.AddComponent<ObstacleMotion>();motion.kind=MotionKind.Translate;motion.axis=Vector3.right;motion.distance=1.2f;motion.period=5.5f;motion.useLocalTranslationAxes=true;motion.shove=false;
            Cube("Low solid rook",rook,Vector3.zero,new Vector3(2.2f,.7f,1.8f),ivory);
            foreach(int side in new[]{-1,1})Cube("Gold face belt",rook,new Vector3(0,.12f,side*.91f),new Vector3(2.2f,.08f,.03f),gold,false);
            for(int i=-1;i<=1;i++)Cube("Decorative crenel",rook,new Vector3(i*.7f,.40f,0),new Vector3(.4f,.10f,1.7f),gold,false);
        }
        static void Palace()
        {
            var walls=Child("PalaceWalls",architecture,Vector3.zero);var columns=Child("ColumnsAndArches",architecture,Vector3.zero);
            var banners=Child("Banners",decor,Vector3.zero);var statues=Child("ChessStatues",decor,Vector3.zero);var plants=Child("Plants",decor,Vector3.zero);var braziers=Child("Braziers",decor,Vector3.zero);
            foreach(int side in new[]{-1,1}){
                Cube("Palace wall",walls,new Vector3(side*24,6.5f,72),new Vector3(1.4f,17,140),stone);
                foreach(float y in new[]{.4f,4.8f,12.6f,15f})Cube("Golden cornice",walls,new Vector3(side*23.22f,y,72),new Vector3(.18f,.16f,140),gold,false);
                foreach(float z in new[]{7f,25,43,61,79,97,115,137}){
                    Column(columns,new Vector3(side*22.4f,0,z),14, .9f);
                    var window=Child("Recessed arched window",walls,new Vector3(side*23.23f,6.2f,z+6));window.localRotation=Quaternion.Euler(0,side*90,0);
                    Cube("Shadowed recess",window,new Vector3(0,2,0),new Vector3(2.5f,5,.08f),slate,false);
                    var wa=MeshObj("Window archivolt",window,new Vector3(0,4.5f,-.1f),new Vector3(1.5f,1.5f,.35f),arch,gold);
                    Cube("Window mullion",window,new Vector3(0,2,-.1f),new Vector3(.10f,5,.12f),gold,false);
                    Banner(banners,new Vector3(side*21.4f,10,z),side<0?blue:red,2.25f,4.6f,side*90);
                    Plant(plants,new Vector3(side*20.3f,0,z+2.5f),1.1f);
                    Brazier(braziers,new Vector3(side*19.8f,0,z-2.5f));
                    // Open vault ribs preserve the high-angle player camera and overview visibility.
                    if(z==25||z==115){
                        var rib=MeshObj("Open vaulted rib",columns,new Vector3(0,12,z),new Vector3(22,7,.5f),arch,ivory);
                        MeshObj("Vault gold lining",columns,new Vector3(0,11.9f,z-.28f),new Vector3(22,7,.08f),arch,gold);
                    }
                }
                foreach(float z in new[]{16f,34,110,135})Statue(statues,new Vector3(side*16,0,z),2.1f,false);
                for(int z=5;z<142;z+=6){
                    Cube("Masonry vertical joint",walls,new Vector3(side*23.28f,8,z),new Vector3(.015f,15,.025f),ivory,false);
                    for(int y=2;y<15;y+=3)Cube("Masonry course",walls,new Vector3(side*23.25f,y,z+2.8f),new Vector3(.025f,.035f,5.8f),ivory,false);
                }
            }
            var gate=Child("ExitGate",architecture,new Vector3(0,0,138.5f));
            foreach(int side in new[]{-1,1}){
                Column(gate,new Vector3(side*8,0,0),13,1.05f);
                var door=Child("Open golden door",gate,new Vector3(side*7.3f,0,0));door.localRotation=Quaternion.Euler(0,side*68,0);
                Cube("Door frame",door,new Vector3(-side*2.8f,5,0),new Vector3(5.6f,10,.25f),gold,false);
                Cube("Door inset",door,new Vector3(-side*2.8f,5,-.14f),new Vector3(4.8f,9.2f,.05f),iron,false);
                for(int i=1;i<5;i++)Cube("Door lattice",door,new Vector3(-side*i*1.1f,5,-.19f),new Vector3(.10f,9.2f,.10f),gold,false);
                MeshObj("Door crest",door,new Vector3(-side*2.8f,4.5f,-.23f),Vector3.one*2.1f,crown,gold);
            }
            MeshObj("Grand exit arch",gate,new Vector3(0,10,0),new Vector3(8.5f,7,1.2f),arch,ivory);
            MeshObj("Gold exit archivolt",gate,new Vector3(0,10,-.7f),new Vector3(8.1f,6.7f,.15f),arch,gold);
            MeshObj("Royal exit crest",gate,new Vector3(0,17.2f,-.6f),Vector3.one*2.2f,crown,gold);
        }
        static void Fountain()
        {
            var t=Child("CentralFountain",architecture,new Vector3(0,-1.2f,72));
            // Fountain plinth is surrounded by a water moat, separated from both playable routes.
            Cylinder("Oval island",t,new Vector3(0,-1.6f,0),14,1.2f,stone,true).localScale=new Vector3(14,.6f,24);
            Cylinder("Lower marble basin",t,new Vector3(0,.1f,0),12,.8f,ivory,false).localScale=new Vector3(12,.4f,20);
            var rim=MeshObj("Oval basin rim",t,new Vector3(0,.58f,0),new Vector3(6,.5f,10),ring,gold);
            Cylinder("Basin water",t,new Vector3(0,.52f,0),11.6f,.03f,water,false).localScale=new Vector3(11.6f,.015f,19.6f);
            foreach(var tier in new[]{new Vector3(7,2,0),new Vector3(4.6f,4.2f,0)}){
                Cylinder("Fountain pedestal",t,new Vector3(0,tier.y*.5f,0),1.5f,tier.y,ivory,false);
                Cylinder("Tier bowl",t,new Vector3(0,tier.y,0),tier.x,.45f,ivory,false);
                Ring(t,new Vector3(0,tier.y+.22f,0),tier.x/2,gold);
                Cylinder("Tier water",t,new Vector3(0,tier.y+.23f,0),tier.x-.25f,.025f,water,false);
                for(int i=0;i<16;i++){float a=i*Mathf.PI/8;float r=tier.x*.47f;
                    var at=new Vector3(Mathf.Cos(a)*r,tier.y-1,Mathf.Sin(a)*r);
                    Cylinder("Falling stream",t,at,.075f,2.1f,foam,false);
                }
            }
            Statue(t,new Vector3(0,4.6f,0),1.9f,true);
            foreach(float z in new[]{-11f,11f})foreach(int side in new[]{-1,1})Plant(t,new Vector3(side*4,1,z),1.15f);
            for(int i=0;i<8;i++){
                float a=i*Mathf.PI/4;var p=new Vector3(Mathf.Cos(a)*5,.65f,Mathf.Sin(a)*8.5f);
                var arc=new List<Vector3>();for(int j=0;j<=12;j++){float q=j/12f;arc.Add(Vector3.Lerp(p,new Vector3(p.x*.55f,1,p.z*.55f),q)+Vector3.up*(Mathf.Sin(q*Mathf.PI)*1.8f));}
                for(int j=1;j<arc.Count;j++)Rod("Arcing water jet",t,arc[j-1],arc[j],.06f,foam);
            }
        }
        static void Column(Transform p,Vector3 at,float height,float radius)
        {
            var t=Child("Fluted column",p,at);
            Cube("Plinth",t,new Vector3(0,.3f,0),new Vector3(radius*2.8f,.6f,radius*2.8f),ivory);
            Cylinder("Column shaft",t,new Vector3(0,height*.5f,0),radius*1.7f,height-.8f,ivory,true);
            for(int j=0;j<8;j++){float a=j*Mathf.PI/4;Cylinder("Stone flute",t,new Vector3(Mathf.Cos(a)*radius*.78f,height*.5f,Mathf.Sin(a)*radius*.78f),radius*.18f,height-1.4f,stone,false);}
            foreach(float y in new[]{.68f,height-.7f,height-.35f})Cylinder("Capital moulding",t,new Vector3(0,y,0),radius*2.25f,.22f,gold,false);
            Cube("Capital",t,new Vector3(0,height-.12f,0),new Vector3(radius*2.7f,.24f,radius*2.7f),ivory,false);
        }
        static void Banner(Transform p,Vector3 at,Material color,float width,float height,float yaw)
        {
            var t=Child("Crown banner",p,at);t.localRotation=Quaternion.Euler(0,yaw,0);
            Cube("Velvet hanging",t,Vector3.zero,new Vector3(width,height,.055f),color,false);
            foreach(int side in new[]{-1,1})Cube("Gold braid",t,new Vector3(side*(width*.5f-.06f),0,-.04f),new Vector3(.08f,height,.035f),gold,false);
            Cube("Banner rod",t,new Vector3(0,height*.5f,0),new Vector3(width+.6f,.15f,.16f),gold,false);
            MeshObj("Embroidered crown",t,new Vector3(0,-.4f,-.055f),Vector3.one*(width*.62f),crown,gold);
        }
        static void Statue(Transform p,Vector3 at,float scale,bool king)
        {
            var t=Child(king?"Fountain Monarch":"Royal chess statue",p,at);
            Cube("Carved pedestal",t,new Vector3(0,.35f,0),new Vector3(scale*1.2f,.7f,scale*1.2f),stone);
            var piece=MeshObj(king?"King":"Pawn",t,new Vector3(0,.7f,0),Vector3.one*scale,king?kingMesh:pawnMesh,ivory);
            if(king){Cube("King cross stem",piece,new Vector3(0,2.32f,0),new Vector3(.18f,.62f,.18f),gold,false);Cube("King cross arm",piece,new Vector3(0,2.45f,0),new Vector3(.58f,.18f,.18f),gold,false);}
        }
        static void Plant(Transform p,Vector3 at,float scale)
        {
            var t=Child("Royal planter",p,at);t.localScale=Vector3.one*scale;
            Cube("Planter",t,new Vector3(0,.4f,0),new Vector3(1.6f,.8f,1.6f),ivory,false);Cube("Planter rim",t,new Vector3(0,.82f,0),new Vector3(1.72f,.12f,1.72f),gold,false);
            for(int i=0;i<7;i++){float a=i*2.39996f;var pos=new Vector3(Mathf.Sin(a)*.55f,1.1f+(i%3)*.22f,Mathf.Cos(a)*.55f);
                Sphere("Topiary foliage",t,pos,new Vector3(.9f,.9f,.9f),leaf);Sphere("Flower cluster",t,pos+Vector3.up*.34f,Vector3.one*.22f,i%2==0?ivory:flower);
            }
        }
        static void Brazier(Transform p,Vector3 at)
        {
            var t=Child("Royal brazier",p,at);Cylinder("Pedestal",t,new Vector3(0,.8f,0),.6f,1.6f,ivory,false);
            Cylinder("Fire bowl",t,new Vector3(0,1.7f,0),1.2f,.3f,gold,false);
            for(int i=0;i<3;i++)Sphere("Flame tongue",t,new Vector3((i-1)*.25f,2.2f+i%2*.15f,0),new Vector3(.35f,.9f,.35f),flame);
        }
        static void Rail(Transform p,Vector3 a,Vector3 b,float height)
        {
            var t=Child("Safety balustrade",p,(a+b)/2);t.localRotation=Quaternion.LookRotation(b-a);float len=Vector3.Distance(a,b);
            Cube("Solid parapet",t,new Vector3(0,height*.5f,0),new Vector3(.28f,height,len),ivory);
            Cube("Gold coping",t,new Vector3(0,height+.03f,0),new Vector3(.38f,.10f,len),gold,false);
            for(float z=-len/2;z<=len/2;z+=2)Cube("Balustrade pier",t,new Vector3(0,height*.5f,z),new Vector3(.52f,height+.15f,.52f),stone,false);
        }
        static void Ramp(Transform p,Vector3 low,Vector3 high,float width)
        {
            var dir=Vector3.ProjectOnPlane(high-low,Vector3.up).normalized;float len=Vector3.ProjectOnPlane(high-low,Vector3.up).magnitude;
            var t=Child("Upstream return ramp",p,(low+high)/2);t.localRotation=Quaternion.LookRotation(dir);
            var path=new[]{new Vector3(0,-(high.y-low.y)/2,-len/2),new Vector3(0,(high.y-low.y)/2,len/2)};
            Ribbon(t,"Sloping solid",path,width,.45f,stone,true);Ribbon(t,"Return gold stripe",path,.14f,.015f,gold,false,.025f);
        }
        static void Arrow(Transform p,Vector3 at,Vector3 dir,float size)
        {
            var t=Child("Direction chevron",p,at);t.localRotation=Quaternion.LookRotation(dir);t.localScale=Vector3.one*size;
            foreach(int side in new[]{-1,1}){var a=Cube("Chevron",t,new Vector3(side*.25f,0,0),new Vector3(.14f,.014f,.85f),gold,false);a.localRotation=Quaternion.Euler(0,-side*40,0);}
        }
        static void FloorCrown(Transform p,Vector3 at,float scale){var t=MeshObj("Floor crown",p,at,Vector3.one*scale,crown,gold);t.localRotation=Quaternion.Euler(90,0,0);}
        static void Strip(Transform p,Vector3 a,Vector3 b,float width,Material mat){var t=Cube("Inlaid strip",p,(a+b)/2,new Vector3(width,.01f,Vector3.Distance(a,b)),mat,false);t.localRotation=Quaternion.LookRotation(b-a);}
        static void Rod(string name,Transform p,Vector3 a,Vector3 b,float diameter,Material mat){var t=Cylinder(name,p,(a+b)/2,diameter,Vector3.Distance(a,b),mat,false);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        static void Ring(Transform p,Vector3 at,float radius,Material mat)=>MeshObj("Circular moulding",p,at,new Vector3(radius,.7f,radius),ring,mat);
        static Transform Sphere(string n,Transform p,Vector3 at,Vector3 size,Material m)=>Primitive(n,PrimitiveType.Sphere,p,at,size,m,false);
        static Transform Cylinder(string n,Transform p,Vector3 at,float diameter,float height,Material m,bool solid)=>Primitive(n,PrimitiveType.Cylinder,p,at,new Vector3(diameter,height/2,diameter),m,solid);
        static Transform Primitive(string name,PrimitiveType type,Transform p,Vector3 at,Vector3 size,Material m,bool solid)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;
        }
        static Transform Cube(string name,Transform p,Vector3 at,Vector3 size,Material m,bool solid=true)
        {var t=MeshObj(name,p,at,size,bevel,m);if(solid)t.gameObject.AddComponent<BoxCollider>().size=Vector3.one;return t;}
        static Transform MeshObj(string name,Transform p,Vector3 at,Vector3 size,Mesh mesh,Material material)
        {var t=Child(name,p,at);t.localScale=size;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return t;}
        static Transform Child(string n,Transform p,Vector3 at){var t=new GameObject(n).transform;t.SetParent(p,false);t.localPosition=at;return t;}
        static void Body(Transform t){var b=t.gameObject.AddComponent<Rigidbody>();b.isKinematic=true;b.useGravity=false;b.interpolation=RigidbodyInterpolation.Interpolate;b.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;}
        static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=path.Substring(0,path.LastIndexOf('/'));Folder(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));}
        static Mesh Save(Mesh mesh,string name){mesh.name=name;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Art+"/Meshes/"+name+".asset"));return mesh;}
        static List<Vector3> Smooth(Vector3[] p,int steps)
        {
            var result=new List<Vector3>();for(int i=0;i<p.Length-1;i++)for(int j=0;j<steps;j++){
                float t=j/(float)steps,t2=t*t,t3=t2*t;var a=p[Mathf.Max(i-1,0)];var b=p[i];var c=p[i+1];var d=p[Mathf.Min(i+2,p.Length-1)];
                result.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));
            }result.Add(p[p.Length-1]);return result;
        }
        static List<Vector3> Offset(IList<Vector3> points,float offset)
        {var r=new List<Vector3>();for(int i=0;i<points.Count;i++){var dir=points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)];r.Add(points[i]+Vector3.Cross(Vector3.up,dir.normalized)*offset);}return r;}
        static void Ribbon(Transform p,string name,IList<Vector3> points,float width,float depth,Material mat,bool solid,float lift=0)
        {
            var v=new List<Vector3>();var tr=new List<int>();
            for(int i=0;i<points.Count;i++){
                var tangent=points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)];var side=Vector3.Cross(Vector3.up,tangent).normalized*width/2;var top=points[i]+Vector3.up*lift;
                v.Add(top-side);v.Add(top+side);v.Add(top-side-Vector3.up*depth);v.Add(top+side-Vector3.up*depth);
                if(i==0)continue;int a=(i-1)*4,b=i*4;
                tr.AddRange(new[]{a,b,b+1,a,b+1,a+1,a+2,a+3,b+3,a+2,b+3,b+2,a,a+2,b+2,a,b+2,b,a+1,b+1,b+3,a+1,b+3,a+3});
            }
            int e=(points.Count-1)*4;tr.AddRange(new[]{0,1,3,0,3,2,e,e+2,e+3,e,e+3,e+1});
            var mesh=Save(new Mesh{vertices=v.ToArray(),triangles=tr.ToArray()},name.Replace(' ','_'));
            var obj=MeshObj(name,p,Vector3.zero,Vector3.one,mesh,mat);if(solid)obj.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        static Mesh Torus(float radius,float tube,int n,int k)
        {
            var v=new List<Vector3>();var tr=new List<int>();for(int i=0;i<n;i++)for(int j=0;j<k;j++){
                float a=i*2*Mathf.PI/n,b=j*2*Mathf.PI/k,r=radius+tube*Mathf.Cos(b);v.Add(new Vector3(r*Mathf.Cos(a),tube*Mathf.Sin(b),r*Mathf.Sin(a)));
                int c=i*k+j,d=((i+1)%n)*k+j,e=((i+1)%n)*k+(j+1)%k,f=i*k+(j+1)%k;tr.AddRange(new[]{c,e,d,c,f,e});
            }return new Mesh{vertices=v.ToArray(),triangles=tr.ToArray()};
        }
        static Mesh Lathe(Vector2[] profile,int count)
        {
            var v=new List<Vector3>();var t=new List<int>();for(int i=0;i<profile.Length;i++)for(int j=0;j<=count;j++){
                float a=j*2*Mathf.PI/count;v.Add(new Vector3(Mathf.Cos(a)*profile[i].x,profile[i].y,Mathf.Sin(a)*profile[i].x));
                if(i>0&&j<count){int b=i*(count+1)+j,a0=b-count-1;t.AddRange(new[]{a0,b,b+1,a0,b+1,a0+1});}
            }return new Mesh{vertices=v.ToArray(),triangles=t.ToArray()};
        }
        static Mesh ArchMesh()
        {
            var v=new List<Vector3>();var t=new List<int>();const int count=32;
            for(int i=0;i<=count;i++){
                float a=i*Mathf.PI/count;foreach(float z in new[]{-.5f,.5f})foreach(float r in new[]{.86f,1f})v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,z));
                if(i==0)continue;int b=i*4,a0=b-4;t.AddRange(new[]{a0,b,a0+1,a0+1,b,b+1,a0+2,a0+3,b+2,a0+3,b+3,b+2,a0,a0+2,b+2,a0,b+2,b,a0+1,b+1,b+3,a0+1,b+3,a0+3});
            }return new Mesh{vertices=v.ToArray(),triangles=t.ToArray()};
        }
        static Mesh Extrude(Vector2[] shape,float depth)
        {
            var v=new List<Vector3>();var t=new List<int>();foreach(float z in new[]{-depth/2,depth/2})foreach(var p in shape)v.Add(new Vector3(p.x,p.y,z));
            int n=shape.Length;for(int i=1;i<n-1;i++){t.AddRange(new[]{0,i+1,i,n,n+i,n+i+1});}for(int i=0;i<n;i++){int j=(i+1)%n;t.AddRange(new[]{i,j,n+j,i,n+j,n+i});}
            return new Mesh{vertices=v.ToArray(),triangles=t.ToArray()};
        }
        static Mesh BeveledBox()
        {
            var v=new List<Vector3>();var t=new List<int>();float h=.5f,b=.475f;
            // Eight-point chamfered footprint with a slightly inset top and bottom.
            var ring2=new[]{new Vector2(-b,-h),new Vector2(b,-h),new Vector2(h,-b),new Vector2(h,b),new Vector2(b,h),new Vector2(-b,h),new Vector2(-h,b),new Vector2(-h,-b)};
            for(int level=0;level<4;level++)foreach(var p in ring2){float factor=level==0||level==3?.95f:1;v.Add(new Vector3(p.x*factor,new[]{-.5f,-.475f,.475f,.5f}[level],p.y*factor));}
            for(int l=0;l<3;l++)for(int i=0;i<8;i++){int j=(i+1)%8,a=l*8+i,c=l*8+j;t.AddRange(new[]{a,a+8,c+8,a,c+8,c});}
            for(int i=1;i<7;i++){t.AddRange(new[]{0,i,i+1,24,24+i+1,24+i});}return new Mesh{vertices=v.ToArray(),triangles=t.ToArray()};
        }
        static void Combine(Transform root,bool children=true)
        {
            var filters=root.GetComponentsInChildren<MeshFilter>(false).Where(f=>f.GetComponent<MeshRenderer>()!=null && f.GetComponent<MeshRenderer>().sharedMaterial!=null && (children||f.transform.parent==root)).ToArray();
            foreach(var group in filters.GroupBy(f=>f.GetComponent<MeshRenderer>().sharedMaterial)){
                var pieces=group.ToArray();if(pieces.Length<2)continue;
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pieces.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
                mesh=Save(mesh,root.name.Replace(' ','_')+"_"+group.Key.name);
                MeshObj("Batched "+group.Key.name,root,Vector3.zero,Vector3.one,mesh,group.Key);
                foreach(var f in pieces){Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
            }
        }

        [MenuItem("ChessFight/Pawn Rush/왕궁 분수 회랑/2 프리팹 검사")]
        public static void Validate()
        {
            var g=PrefabUtility.LoadPrefabContents(PrefabPath);var lines=new List<string>();
            Action<bool,string> check=(ok,label)=>{lines.Add((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);};
            try{
                var s=g.GetComponent<PawnRushSection>();s.ValidateDefinition();check(s.exit.localPosition==new Vector3(0,0,144),"144m module connector contract");
                check(g.GetComponentsInChildren<CrumblingRoyalFloor>(true).Length==12,"12 collapse tiles in three split bands");
                check(g.GetComponentsInChildren<PendulumSwing>(true).Length==2,"two pendulum mallets");
                check(g.GetComponentsInChildren<ObstacleImpact>(true).Length==2,"only two mallet impact sources");
                check(g.GetComponentsInChildren<ObstacleSurface>(true).Length==4,"four gentle turntables");
                check(g.GetComponentsInChildren<TeamMission>(true).Length==0,"no mission or victory rules");
                check(g.GetComponentsInChildren<LabGame>(true).Length==0,"no player or game manager embedded");
                foreach(var tile in g.GetComponentsInChildren<CrumblingRoyalFloor>(true))check(tile.Visual&&tile.SolidCollider&&tile.OccupancyArea&&tile.RestoreClearanceArea&&tile.CrackVisual&&tile.WarningVisual,"tile references "+tile.name);
                check(g.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"no missing scripts");
                var renderers=g.GetComponentsInChildren<MeshRenderer>(true);
                check(renderers.All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported)),"compatible materials and shaders");
                check(g.GetComponentsInChildren<Rigidbody>(true).All(b=>b.isKinematic),"all obstacle rigidbodies kinematic");
                var bounds=new Bounds();bool init=false;foreach(var r in renderers){if(!init){bounds=r.bounds;init=true;}else bounds.Encapsulate(r.bounds);}
                lines.Add("INFO render bounds "+bounds);lines.Add("INFO mesh renderers "+renderers.Length+", colliders "+g.GetComponentsInChildren<Collider>(true).Length);
                lines.Add("INFO left reference centerline "+Length(Left).ToString("F2")+"m; right "+Length(Right).ToString("F2")+"m");
                check(bounds.min.x>=-26.1f&&bounds.max.x<=26.1f&&bounds.min.z>=-.1f&&bounds.max.z<=144.1f,"geometry within module footprint");
                var catalog=AssetDatabase.FindAssets("t:PawnRushCatalog").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<PawnRushCatalog>).FirstOrDefault();
                if(catalog!=null){var temp=Object.Instantiate(catalog);try{temp.obstacles=temp.obstacles.Concat(new[]{s}).Distinct().ToArray();temp.ValidateCatalog();foreach(int slot in new[]{0,2,4}){var pins=new PawnRushSection[6];pins[slot]=s;var plan=temp.Choose(18731,pins);check(plan[slot]==s&&plan.Distinct().Count()==6,"temporary catalog selection slot "+slot);}}finally{Object.DestroyImmediate(temp);}}
                lines.Add("Human/12-player/Steam checks pending. Catalog and Imported scene unchanged.");
            }finally{PrefabUtility.UnloadPrefabContents(g);File.WriteAllLines("Temp/RoyalFountain.validation.txt",lines);}
        }
        static float Length(Vector3[] path){float total=0;for(int i=1;i<path.Length;i++)total+=Vector3.Distance(path[i-1],path[i]);return total;}

        [MenuItem("ChessFight/Pawn Rush/왕궁 분수 회랑/3 별도 시험 공간 열기")]
        public static void OpenTest()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            var existing=SceneManager.GetSceneByName(TestName);if(existing.IsValid()){SceneManager.SetActiveScene(existing);return;}
            if(Object.FindObjectsByType<LabGame>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Length>0)
                throw new InvalidOperationException("Close other LabGame test scenes before opening the isolated test. Existing scenes are not changed.");
            var source=EditorSceneManager.OpenPreviewScene("Assets/Scenes/ImportedChessFightMap.unity");
            var target=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);target.name=TestName;SceneManager.SetActiveScene(target);
            try{
                var original=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LabGame>(true)).Single();
                var camera=Object.Instantiate(original.labCamera);camera.name="Fountain test camera";SceneManager.MoveGameObjectToScene(camera.gameObject,target);
                var game=new GameObject("Existing LabGame test rig").AddComponent<LabGame>();SceneManager.MoveGameObjectToScene(game.gameObject,target);
                EditorUtility.CopySerialized(original,game);game.labCamera=camera;camera.game=game;game.spawnPracticeObjects=false;game.bar=null;game.slots=null;
                game.players=new[]{new LabGame.Slot{name="P1",device=LabDevice.KeyboardMouse,material=original.players[0].material,spawn=new Vector3(1000,.04f,3)}};
                var section=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),target);section.transform.position=new Vector3(1000,0,0);
                camera.transform.SetPositionAndRotation(new Vector3(1000,2.5f,-2),Quaternion.Euler(8,0,0));
                Lighting(target);Selection.activeGameObject=section;
                if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(1000,0,72),Quaternion.Euler(52,-15,0),90);
            }finally{EditorSceneManager.ClosePreviewScene(source);}
        }
        static void Lighting(Scene scene)
        {
            var light=new GameObject("Fountain preview daylight").AddComponent<Light>();SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.4f;light.color=new Color(1,.90f,.75f);light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(42,-32,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.54f,.62f,.73f);RenderSettings.fog=false;
        }
        public static void Capture()
        {
            var old=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try{
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),scene);root.transform.position=new Vector3(2000,0,0);Lighting(scene);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                var light=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).First();light.cullingMask=1<<31;
                Directory.CreateDirectory("Temp/RoyalFountainShots");
                Shot(scene,root.transform,new Vector3(66,110,-12),new Vector3(0,0,72),"Overview",true);
                Shot(scene,root.transform,new Vector3(0,2.7f,9),new Vector3(0,5,70),"Entrance",false);
                Shot(scene,root.transform,new Vector3(-16,2.7f,49),new Vector3(-11,1.5f,66),"LeftBridge",false);
                Shot(scene,root.transform,new Vector3(15,2.7f,53),new Vector3(11,3,70),"RightHammers",false);
                Shot(scene,root.transform,new Vector3(0,2.7f,111),new Vector3(0,5,140),"Gallery",false);
            }finally{EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(old);}
        }
        static void Shot(Scene scene,Transform root,Vector3 eye,Vector3 target,string name,bool ortho)
        {
            var go=new GameObject("Capture camera");SceneManager.MoveGameObjectToScene(go,scene);var c=go.AddComponent<Camera>();c.enabled=false;c.cullingMask=1<<31;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.095f,.15f,.21f);c.nearClipPlane=.1f;c.farClipPlane=400;c.fieldOfView=65;c.orthographic=ortho;c.orthographicSize=82;
            go.transform.position=root.TransformPoint(eye);go.transform.LookAt(root.TransformPoint(target));
            var rt=new RenderTexture(1600,1100,24);var previous=RenderTexture.active;Texture2D image=null;
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes("Temp/RoyalFountainShots/"+name+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=previous;c.targetTexture=null;if(image!=null)Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
        public static void RuntimeProbe(){RoyalFountainHallProbe.Start();}
        public static void WalkLeft(){RoyalFountainHallProbe.Walk(false);}
        public static void WalkRight(){RoyalFountainHallProbe.Walk(true);}
        public static void Recover(){RoyalFountainHallProbe.Recover();}
        public static void CloseTest(){var s=SceneManager.GetSceneByName(TestName);if(s.IsValid()&&!EditorApplication.isPlaying)EditorSceneManager.CloseScene(s,true);Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);}
    }
}
