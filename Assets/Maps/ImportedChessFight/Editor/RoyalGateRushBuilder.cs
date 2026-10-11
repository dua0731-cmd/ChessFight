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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.Maps.Editor
{
    // Explicit authoring only: never rebuilds edited geometry on import or Play.
    public static class RoyalGateRushBuilder
    {
        public const string Id = "O04_RoyalGateRush";
        public const string PrefabPath = "Assets/Maps/PawnRush/Sections/Obstacles/" + Id + ".prefab";
        const string Art = "Assets/Maps/PawnRush/RoyalGateRush";
        const string Report = "Temp/RoyalGateRush.validation.txt";
        static Material stone, trim, slate, blue, red, gold, iron, pale, warning;
        static Mesh rampMesh, linkMesh;

        [MenuItem("ChessFight/Pawn Rush/왕성 정문 돌파/프리팹 생성 및 출발 구역 배치")]
        public static void BuildAndPlace()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != PawnRushWorkspaceTools.ScenePath)
                throw new InvalidOperationException("ImportedChessFightMap을 Edit Mode에서 여세요.");
            var course = PawnRushWorkspaceTools.FindCourse();
            var preserved = scene.GetRootGameObjects().Where(g => g != course.gameObject).ToArray();
            Directory.CreateDirectory("Temp/RoyalGateRushBackup");
            // Preserve current edits before recording the exact scene baseline.
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new IOException("기존 씬 변경 저장 실패");
            File.Copy(scene.path, "Temp/RoyalGateRushBackup/BeforeRoyalGateRush.unity", true);
            File.Copy(AssetDatabase.GetAssetPath(course.catalog), "Temp/RoyalGateRushBackup/PawnRushCatalog.asset", true);
            var section = AssetDatabase.LoadAssetAtPath<PawnRushSection>(PrefabPath);
            if (section == null) section = CreatePrefab();
            section.ValidateDefinition();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Place Royal Gate Rush at A1");
            Undo.RecordObject(course.catalog, "Register Royal Gate Rush");
            if (!course.catalog.obstacles.Contains(section))
                course.catalog.obstacles = course.catalog.obstacles.Concat(new[] { section }).ToArray();
            course.catalog.ValidateCatalog();
            EditorUtility.SetDirty(course.catalog);
            Undo.RecordObject(course, "Pin Royal Gate Rush preview");
            // Preserve all other authored selections while reserving this section for A1.
            for (int i = 0; i < course.fixedSlots.Length; i++)
                if (course.fixedSlots[i] == section) course.fixedSlots[i] = null;
            course.fixedSlots[0] = section;
            course.playMode = CoursePlayMode.SavedPreview;
            PawnRushWorkspaceTools.BuildPreview(course, course.seed);
            Vector3 spawn = course.courseEntrance.TransformPoint(new Vector3(0, .04f, 3));
            PawnRushWorkspaceTools.SetPlaytestStart(spawn);
            var marker = course.transform.Find("Start/Playtest Spawn");
            if (marker != null) { Undo.RecordObject(marker, "Move start marker"); marker.position = spawn; }
            var game = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LabGame>(true)).Single();
            var camera = game.labCamera;
            // Only the edit-time view is changed; the existing orbit controller owns Play Mode.
            Undo.RecordObject(camera.transform, "Show course in edit-time Game view");
            camera.transform.SetPositionAndRotation(spawn + new Vector3(0, 2.5f, -5), Quaternion.Euler(9, 0, 0));
            EditorUtility.SetDirty(course);
            if (preserved.Any(g => g == null)) throw new InvalidOperationException("An original scene root was removed.");
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장 실패");
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = course.Sections[0].gameObject;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(course.courseEntrance.TransformPoint(new Vector3(0, 0, 67)), Quaternion.Euler(47, -28, 0), 95);
            Validate();
            Capture();
            Debug.Log("Royal Gate Rush: prefab saved, A1 pinned, spawn at entrance. Press Play to test.");
        }

        static PawnRushSection CreatePrefab()
        {
            EnsureFolder(Art + "/Materials"); EnsureFolder(Art + "/Meshes");
            stone = Mat("IvoryStone", new Color(.80f, .75f, .64f));
            trim = Mat("LightStone", new Color(.94f, .89f, .77f));
            slate = Mat("SlateStone", new Color(.32f, .39f, .46f));
            pale = Mat("PaleTile", new Color(.86f, .83f, .75f));
            blue = Mat("RoyalBlue", new Color(.06f, .29f, .70f));
            red = Mat("RoyalRed", new Color(.68f, .13f, .16f));
            gold = Mat("CrownGold", new Color(.93f, .65f, .19f));
            iron = Mat("ChainIron", new Color(.12f, .16f, .20f));
            warning = Mat("WarningAmber", new Color(1, .54f, .07f));
            rampMesh = MakeRamp(); linkMesh = MakeLink();
            var root = new GameObject(Id);
            try
            {
                var section = root.AddComponent<PawnRushSection>();
                section.sectionId = Id; section.kind = SectionKind.Obstacle;
                section.entrance = Child("Entrance", root.transform, Vector3.zero);
                section.exit = Child("Exit", root.transform, new Vector3(0, 0, 144));
                section.authoringBounds = new Bounds(new Vector3(0, 8, 72), new Vector3(34, 22, 144));
                var geo = Child("Geometry", root.transform, Vector3.zero);
                var hazards = Child("Obstacles", root.transform, Vector3.zero);
                var decor = Child("Decoration", root.transform, Vector3.zero);
                var read = Child("Readability", root.transform, Vector3.zero);
                // A continuous floor eliminates mandatory gaps and exposed tile undersides.
                Cube("Continuous Deck", geo, new Vector3(0, -.5f, 72), new Vector3(20, 1, 144), stone);
                Checker(geo);
                foreach (int side in new[] { -1, 1 })
                {
                    Rail(geo, side * 10, 0, side < 0 ? 88 : 96);
                    Rail(geo, side * 10, side < 0 ? 108 : 116, 144);
                    Cube("Rook Side Bastion", geo, new Vector3(side * 8, .95f, 32), new Vector3(4, 1.9f, 32), stone);
                    Cube("Bastion Coping", decor, new Vector3(side * 8, 1.96f, 32), new Vector3(4.1f, .12f, 32), trim, false);
                    for (int z = 18; z < 48; z += 4)
                        Cube("Rook Merlon", decor, new Vector3(side * 9, 2.24f, z), new Vector3(1.3f, .55f, 1.5f), trim, false);
                }
                for (int i = 0; i < 3; i++) Rook(hazards, i);
                Wave(geo, hazards, decor, read);
                Gate(geo, decor, read);
                Hammer(hazards, read, -5, 102, .5f, blue, "LeftRoyalHammer");
                Hammer(hazards, read, 5, 110, 1.85f, red, "RightRoyalHammer");
                Recovery(geo, read, -1, 102); Recovery(geo, read, 1, 110);
                foreach (int side in new[] { -1, 1 })
                {
                    foreach (float z in new[] { 8f, 52f, 82f, 136f })
                    {
                        Statue(decor, new Vector3(side * 12, 0, z));
                        Banner(decor, new Vector3(side * 11.8f, 5.5f, z + 3), side < 0 ? blue : red, 1.4f, 2.8f);
                    }
                }
                Stripe(read, "Entrance Line", 0, .018f, 1, 18, .32f, gold);
                Stripe(read, "Exit Line", 0, .018f, 140, 18, .32f, gold);
                foreach (float z in new[] { 7f, 13f, 50f, 81f, 88f, 124f, 134f }) Arrow(read, new Vector3(0, .024f, z), 1.1f);
                Label(read, "ROYAL GATE RUSH", new Vector3(0, 4.5f, 13), .44f);
                Label(read, "CHOOSE EITHER PASSAGE", new Vector3(0, 5.8f, 91), .27f);
                Label(read, "NEXT ZONE", new Vector3(0, 4, 140), .28f);
                section.ValidateDefinition();
                var asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (asset == null) throw new IOException("구역 프리팹 저장 실패");
                return asset.GetComponent<PawnRushSection>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Rook(Transform parent, int i)
        {
            var t = Child("RookWall_0" + (i + 1), parent, new Vector3(0, .6f, 24 + i * 10));
            Body(t); var m = t.gameObject.AddComponent<ObstacleMotion>();
            m.kind = MotionKind.Translate; m.axis = Vector3.right; m.distance = 1.5f;
            m.period = 6; m.phase = i / 3f; m.useLocalTranslationAxes = true; m.shove = false;
            Cube("Solid Rook Body", t, Vector3.zero, new Vector3(3, 1.2f, 1.6f), stone);
            Cube("Gold Belt", t, new Vector3(0, .37f, -.815f), new Vector3(3, .15f, .04f), gold, false);
            for (int tooth = -1; tooth <= 1; tooth++)
                Cube("Rook Crown", t, new Vector3(tooth, .75f, 0), new Vector3(.6f, .3f, 1.6f), trim, false);
            Stripe(parent, "Wall Travel Track", 0, .022f, 24 + i * 10, 8, .22f, slate);
            // No ObstacleImpact: these walls only push by physical contact.
        }

        static void Wave(Transform geo, Transform hazards, Transform decor, Transform read)
        {
            var routes = Child("WaveRoute", geo, Vector3.zero);
            for (int i = 0; i < 3; i++)
            {
                var t = Child("WaveTile_0" + (i + 1), hazards, new Vector3(0, -.35f, 57 + 8 * i));
                Body(t); var m = t.gameObject.AddComponent<ObstacleMotion>();
                m.kind = MotionKind.Translate; m.axis = Vector3.up; m.distance = .2f; m.period = 6; m.phase = i / 3f; m.shove = false;
                Cube("Thick Solid Tile", t, Vector3.zero, new Vector3(6, 1.2f, 6), stone);
                Cube("Blue Carpet", t, new Vector3(-1.5f, .608f, 0), new Vector3(2.85f, .014f, 5.7f), blue, false);
                Cube("Red Carpet", t, new Vector3(1.5f, .608f, 0), new Vector3(2.85f, .014f, 5.7f), red, false);
                Cube("Leading Gold Edge", t, new Vector3(0, .62f, -2.84f), new Vector3(5.9f, .02f, .12f), gold, false);
            }
            foreach (int side in new[] { -1, 1 })
            {
                Cube("Route Garden Island", routes, new Vector3(side * 4.1f, .6f, 66), new Vector3(1.8f, 1.2f, 24), stone);
                Cube("Island Coping", decor, new Vector3(side * 4.1f, 1.26f, 66), new Vector3(1.9f, .12f, 24), trim, false);
                Ramp("Side Route Up", routes, side * 8, 56.5f, 0, 4, 5, .8f, false);
                Cube("Side Route Terrace", routes, new Vector3(side * 8, .15f, 66), new Vector3(4, 1.3f, 14), stone);
                Ramp("Side Route Down", routes, side * 8, 75.5f, 0, 4, 5, .8f, true);
                Cube("Side Carpet", read, new Vector3(side * 8, .812f, 66), new Vector3(2.6f, .014f, 14), side < 0 ? blue : red, false);
                Arrow(read, new Vector3(side * 8, .831f, 66), .8f);
            }
        }

        static void Gate(Transform geo, Transform decor, Transform read)
        {
            var gate = Child("CastleFacade", decor, Vector3.zero);
            foreach (int side in new[] { -1, 1 })
            {
                Cube("Passage Divider Approach", geo, new Vector3(side * 8.9f, .7f, 91), new Vector3(2.2f, 1.4f, 2), stone);
                Cube("Gate Pier", geo, new Vector3(side * 11.1f, 5.5f, 119), new Vector3(3.8f, 11, 4), stone);
                Cube("Gate Pier Foot", geo, new Vector3(side * 11.1f, .4f, 119), new Vector3(4.1f, .8f, 4.5f), trim);
                Tower(gate, new Vector3(side * 14, 0, 116));
                Banner(gate, new Vector3(side * 11.1f, 10, 116.8f), side < 0 ? blue : red, 2.5f, 4.8f);
            }
            Cube("Center Route Island", geo, new Vector3(0, .7f, 105), new Vector3(1, 1.4f, 26), stone);
            Cube("Center Island Cap", decor, new Vector3(0, 1.45f, 105), new Vector3(1.1f, .1f, 26), gold, false);
            Cube("Open Gate Lintel", gate, new Vector3(0, 12, 119), new Vector3(24, 2, 3), stone);
            Cube("Lintel Gold Trim", gate, new Vector3(0, 10.94f, 117.45f), new Vector3(20, .18f, .12f), gold, false);
            for (int i = -5; i <= 5; i++)
                Cube("Battlement", gate, new Vector3(i * 2, 13.75f, 119), new Vector3(1.2f, 1.5f, 3), trim, false);
            Crown(gate, new Vector3(0, 14.5f, 117.35f), 2.2f);
            // Two sloped keystones frame a broad open arch without lowering camera clearance.
            foreach (int side in new[] { -1, 1 })
            {
                var arch = Cube("Arch Shoulder", gate, new Vector3(side * 8.4f, 10.05f, 119), new Vector3(3.3f, 1.4f, 3), trim);
                arch.transform.localRotation = Quaternion.Euler(0, 0, side * 28);
            }
            foreach (float x in new[] { -5f, 5f })
            {
                Stripe(read, "Passage Carpet", x, .018f, 105, 4.6f, 26, x < 0 ? blue : red);
                Arrow(read, new Vector3(x, .041f, 94), 1);
                Arrow(read, new Vector3(x, .041f, 116), 1);
            }
        }

        static void Hammer(Transform parent, Transform read, float x, float z, float delay, Material color, string name)
        {
            var root = Child(name, parent, Vector3.zero);
            var context = root.gameObject.AddComponent<ObstacleContext>();
            var pivot = Child("Pivot", root, new Vector3(x, 6.15f, z)); Body(pivot);
            var swing = pivot.gameObject.AddComponent<PendulumSwing>();
            swing.context = context; swing.SwingAngle = 35; swing.SwingDuration = 2.5f;
            swing.PauseAtEnds = .2f; swing.StartDelay = delay; swing.KnockbackForce = 5.5f; swing.UpwardForce = 1;
            var chain = Child("ChainVisual", pivot, Vector3.zero);
            for (int i = 0; i < 11; i++)
            {
                var link = Child("Iron Link", chain, new Vector3(0, -.2f - i * .34f, 0));
                link.localRotation = Quaternion.Euler(90, i % 2 == 0 ? 0 : 90, 0);
                link.localScale = new Vector3(1, 1, 1.5f);
                link.gameObject.AddComponent<MeshFilter>().sharedMesh = linkMesh;
                link.gameObject.AddComponent<MeshRenderer>().sharedMaterial = iron;
            }
            Shape("Handle", PrimitiveType.Cylinder, pivot, new Vector3(0, -4.0f, 0), new Vector3(.24f, .65f, .24f), gold, false);
            var head = Child("HammerHead", pivot, new Vector3(0, -4.8f, 0));
            swing.Ball = head;
            var solid = head.gameObject.AddComponent<CapsuleCollider>(); solid.direction = 2; solid.radius = 1.2f; solid.height = 2.8f;
            var visual = Shape("Royal Mallet", PrimitiveType.Cylinder, head, Vector3.zero, new Vector3(2.4f, 1.4f, 2.4f), color, false);
            visual.transform.localRotation = Quaternion.Euler(90, 0, 0);
            foreach (float end in new[] { -1.28f, 1.28f })
            {
                var ring = Shape("Gold End Band", PrimitiveType.Cylinder, head, new Vector3(0, 0, end), new Vector3(2.48f, .11f, 2.48f), gold, false);
                ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            Crown(head, new Vector3(0, -.26f, -1.412f), .8f);
            var hit = Child("HitArea", head, Vector3.zero);
            var area = hit.gameObject.AddComponent<CapsuleCollider>(); area.direction = 2; area.radius = 1.27f; area.height = 2.94f; area.isTrigger = true;
            var impact = hit.gameObject.AddComponent<ObstacleImpact>(); impact.context = context; impact.pendulum = swing; impact.hitCooldown = 1.25f;
            Cube("Support Beam", root, new Vector3(x, 6.65f, z), new Vector3(9, .7f, 1), stone);
            foreach (int side in new[] { -1, 1 })
                Cube("Support Post", root, new Vector3(x + side * 4.6f, 3.1f, z), new Vector3(.5f, 6.2f, .8f), stone);
            Shape("Pivot Hub", PrimitiveType.Sphere, root, new Vector3(x, 6.15f, z), Vector3.one * .7f, gold, false);
            Stripe(read, "Hammer Sweep Warning", x, .04f, z, 8.2f, 3.15f, warning);
            for (int i = 0; i < 7; i++)
                Stripe(read, "Warning Dash", x - 3.3f + i * 1.1f, .051f, z - 1.65f, .6f, .12f, iron);
        }

        static void Recovery(Transform geo, Transform read, int side, float hazardZ)
        {
            var t = Child(side < 0 ? "Left Recovery Terrace" : "Right Recovery Terrace", geo, Vector3.zero);
            float rampEnd = hazardZ - 4, end = hazardZ + 6;
            Cube("Recovery Landing", t, new Vector3(side * 12, -1.6f, (rampEnd + end) * .5f), new Vector3(4, .8f, end - rampEnd), stone);
            Ramp("Return Upstream", t, side * 12, rampEnd - 4, -1.2f, 4, 8, 1.2f, true);
            Cube("Return Connection", t, new Vector3(side * 11.5f, -.4f, rampEnd - 9), new Vector3(5, .8f, 2), stone);
            Cube("Outer Recovery Rail", t, new Vector3(side * 14.15f, -.55f, hazardZ + 1), new Vector3(.3f, 1.3f, 10), trim);
            Cube("Recovery Dead End", t, new Vector3(side * 12, -.3f, end), new Vector3(4.3f, 1.8f, .4f), stone);
            // Prevent a continuous outside lane connecting the two ends of the section.
            Cube("Ramp Outer Rail", t, new Vector3(side * 14.15f, .35f, rampEnd - 4), new Vector3(.3f, 1.8f, 8), trim);
            var arrow = Child("Return Arrow", read, new Vector3(side * 12, -1.175f, hazardZ));
            arrow.localRotation = Quaternion.Euler(0, 180, 0); Arrow(arrow, Vector3.zero, .9f);
            Label(read, "RETURN", new Vector3(side * 12, .5f, end - .3f), .2f);
        }

        static void Tower(Transform parent, Vector3 position)
        {
            var t = Child("Rook Tower", parent, position);
            Cube("Tower Shaft", t, new Vector3(0, 6.5f, 0), new Vector3(5, 13, 5), stone);
            Cube("Tower Crown Rim", t, new Vector3(0, 13.2f, 0), new Vector3(5.8f, .8f, 5.8f), trim, false);
            for (int i = -1; i <= 1; i++) foreach (int side in new[] { -1, 1 })
            {
                Cube("Tower Merlon", t, new Vector3(i * 2, 14.25f, side * 2.2f), new Vector3(1.2f, 1.4f, 1.2f), trim, false);
                if (i == 0) Cube("Tower Side Merlon", t, new Vector3(side * 2.2f, 14.25f, 0), new Vector3(1.2f, 1.4f, 1.2f), trim, false);
            }
            for (int level = 2; level <= 11; level += 3)
                Cube("Tower Masonry Band", t, new Vector3(0, level, 0), new Vector3(5.12f, .15f, 5.12f), trim, false);
            Cube("Tower Window", t, new Vector3(0, 10, -2.52f), new Vector3(.8f, 1.8f, .04f), iron, false);
        }
        static void Statue(Transform parent, Vector3 at)
        {
            var t = Child("Pawn Statue", parent, at);
            Cube("Plinth", t, new Vector3(0, .55f, 0), new Vector3(2.8f, 1.1f, 2.8f), stone);
            Shape("Pawn Base", PrimitiveType.Cylinder, t, new Vector3(0, 1.3f, 0), new Vector3(2, .2f, 2), trim, false);
            Shape("Pawn Stem", PrimitiveType.Cylinder, t, new Vector3(0, 2.1f, 0), new Vector3(.8f, .65f, .8f), trim, false);
            Shape("Pawn Collar", PrimitiveType.Cylinder, t, new Vector3(0, 2.72f, 0), new Vector3(1.4f, .15f, 1.4f), gold, false);
            Shape("Pawn Head", PrimitiveType.Sphere, t, new Vector3(0, 3.45f, 0), Vector3.one * 1.4f, trim, false);
        }
        static void Banner(Transform parent, Vector3 at, Material material, float width, float height)
        {
            var t = Child("Royal Banner", parent, at);
            Cube("Fabric", t, Vector3.zero, new Vector3(width, height, .05f), material, false);
            Cube("Banner Rod", t, new Vector3(0, height * .5f, 0), new Vector3(width + .5f, .13f, .15f), gold, false);
            Crown(t, new Vector3(0, -.2f, -.04f), width * .53f);
        }
        static void Crown(Transform parent, Vector3 at, float size)
        {
            var t = Child("Crown Emblem", parent, at); t.localScale = Vector3.one * size;
            Cube("Crown Base", t, Vector3.zero, new Vector3(1, .16f, .035f), gold, false);
            for (int i = -1; i <= 1; i++)
            {
                var tooth = Cube("Crown Point", t, new Vector3(i * .37f, .22f, 0), new Vector3(.2f, i == 0 ? .55f : .4f, .035f), gold, false);
                tooth.transform.localRotation = Quaternion.Euler(0, 0, -i * 20);
            }
        }
        static void Rail(Transform p, float x, float start, float end) => Cube("Safe Outer Parapet", p, new Vector3(x, .55f, (start + end) / 2), new Vector3(.3f, 1.1f, end - start), trim);
        static void Stripe(Transform p, string n, float x, float y, float z, float w, float d, Material m) => Cube(n, p, new Vector3(x, y, z), new Vector3(w, .008f, d), m, false);
        static void Arrow(Transform p, Vector3 at, float size)
        {
            var t = Child("Forward Arrow", p, at); t.localScale = Vector3.one * size;
            foreach (int side in new[] { -1, 1 })
            {
                var a = Cube("Arrow Arm", t, new Vector3(side * .3f, 0, 0), new Vector3(.16f, .008f, 1.1f), gold, false);
                a.transform.localRotation = Quaternion.Euler(0, side * -35, 0);
            }
        }
        static void Label(Transform parent, string text, Vector3 at, float size)
        {
            var t = Child(text, parent, at);
            var m = t.gameObject.AddComponent<TextMesh>(); m.text = text; m.fontSize = 64; m.characterSize = size * 10f / 64f;
            m.anchor = TextAnchor.MiddleCenter; m.alignment = TextAlignment.Center; m.color = new Color(.16f, .19f, .24f);
            m.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m.GetComponent<Renderer>().sharedMaterial = m.font.material;
        }
        static void Body(Transform t)
        {
            var body = t.gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        static Transform Child(string name, Transform parent, Vector3 at)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = at; return t;
        }
        static GameObject Cube(string n, Transform p, Vector3 at, Vector3 scale, Material m, bool solid = true) => Shape(n, PrimitiveType.Cube, p, at, scale, m, solid);
        static GameObject Shape(string n, PrimitiveType type, Transform p, Vector3 at, Vector3 scale, Material m, bool solid)
        {
            var go = GameObject.CreatePrimitive(type); go.name = n; go.transform.SetParent(p, false); go.transform.localPosition = at; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
        static Material Mat(string name, Color color)
        {
            string path = Art + "/Materials/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path); if (result != null) return result;
            result = new Material(Shader.Find("Standard")) { name = name, color = color };
            result.SetFloat("_Glossiness", .18f); AssetDatabase.CreateAsset(result, path); return result;
        }
        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = Art + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { Object.DestroyImmediate(mesh); return existing; }
            mesh.name = name; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        static Mesh MakeRamp()
        {
            var v = new[] { new Vector3(-.5f,-1,-.5f),new Vector3(.5f,-1,-.5f),new Vector3(.5f,-1,.5f),new Vector3(-.5f,-1,.5f),
                new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(.5f,1,.5f),new Vector3(-.5f,1,.5f) };
            return SaveMesh(new Mesh { vertices=v, triangles=new[]{4,7,6,4,6,5,0,1,2,0,2,3,0,4,5,0,5,1,3,2,6,3,6,7,0,3,7,0,7,4,1,5,6,1,6,2}},"UnitRamp");
        }
        static void Ramp(string name, Transform p, float x, float z, float low, float width, float length, float rise, bool down)
        {
            var t = Child(name, p, new Vector3(x, low, z)); t.localScale = new Vector3(width, rise, length);
            if (down) t.localRotation = Quaternion.Euler(0, 180, 0);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = rampMesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = stone;
            t.gameObject.AddComponent<MeshCollider>().sharedMesh = rampMesh;
        }
        static Mesh MakeLink()
        {
            var v=new List<Vector3>();var t=new List<int>(); const int n=16,k=6;
            for(int i=0;i<n;i++) for(int j=0;j<k;j++){
                float a=i*Mathf.PI*2/n,b=j*Mathf.PI*2/k,r=.18f+.045f*Mathf.Cos(b);
                v.Add(new Vector3(r*Mathf.Cos(a),.045f*Mathf.Sin(b),r*Mathf.Sin(a)));
                int c=i*k+j,d=((i+1)%n)*k+j,e=((i+1)%n)*k+(j+1)%k,f=i*k+(j+1)%k;
                t.AddRange(new[]{c,d,e,c,e,f});
            }
            return SaveMesh(new Mesh{vertices=v.ToArray(),triangles=t.ToArray()},"ChainLink");
        }
        static void Checker(Transform parent)
        {
            var v = new List<Vector3>(); var light = new List<int>(); var dark = new List<int>();
            for (int row=0;row<36;row++) for(int col=0;col<5;col++)
            {
                float x=-10+col*4,z=row*4; int a=v.Count;
                v.AddRange(new[]{new Vector3(x,.009f,z),new Vector3(x,.009f,z+4),new Vector3(x+4,.009f,z+4),new Vector3(x+4,.009f,z)});
                ((row+col)%2==0?light:dark).AddRange(new[]{a,a+1,a+2,a,a+2,a+3});
            }
            var mesh=new Mesh{vertices=v.ToArray(),subMeshCount=2};mesh.SetTriangles(light,0);mesh.SetTriangles(dark,1);
            mesh=SaveMesh(mesh,"CheckerDeck");var t=Child("Checkerboard Surface",parent,Vector3.zero);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterials=new[]{pale,slate};
        }

        [MenuItem("ChessFight/Pawn Rush/왕성 정문 돌파/배치 검사")]
        public static void Validate()
        {
            var lines=new List<string>(); Action<bool,string> check=(ok,label)=>{lines.Add((ok?"PASS ":"FAIL ")+label);if(!ok)throw new InvalidOperationException(label);};
            try
            {
                var course=PawnRushWorkspaceTools.FindCourse(); var s=AssetDatabase.LoadAssetAtPath<PawnRushSection>(PrefabPath);
                check(s!=null,"section prefab asset exists"); s.ValidateDefinition(); course.catalog.ValidateCatalog();
                check(s.exit.localPosition==new Vector3(0,0,144),"144m flat +Z connector contract");
                check(s.GetComponentsInChildren<ObstacleMotion>(true).Length==6,"three walls and three low moving tiles");
                check(s.GetComponentsInChildren<PendulumSwing>(true).Length==2,"two independent pendulum mallets");
                check(s.GetComponentsInChildren<ObstacleImpact>(true).Length==2,"only mallets apply explicit knockback");
                check(s.GetComponentsInChildren<TeamMission>(true).Length==0,"no mission gate or duplicate game rules");
                foreach(var swing in s.GetComponentsInChildren<PendulumSwing>(true))
                    check(swing.Ball!=null && swing.context!=null && swing.Ball.GetComponent<Collider>()!=null,"mallet body and impact references resolve");
                foreach(var t in s.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new InvalidOperationException("Missing script "+t.name);
                check(true,"no missing scripts in prefab");
                check(s.GetComponentsInChildren<Renderer>(true).All(r=>r.sharedMaterials.All(m=>m!=null && m.shader!=null)),"materials and shaders resolve");
                for(int slot=0;slot<6;slot+=2){var pins=new PawnRushSection[6];pins[slot]=s;var plan=course.catalog.Choose(18731,pins);check(plan[slot]==s && plan.Distinct().Count()==6,"unique selection compatible with "+PawnRushCourse.SlotName(slot));}
                PawnRushWorkspaceTools.Validate(course);
                check(course.Sections[0].sectionId==Id && course.fixedSlots[0]==s && course.playMode==CoursePlayMode.SavedPreview,"A1 saved preview is pinned for immediate play");
                var game=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LabGame>(true)).Single();
                check(game.players.Length==1 && Vector3.Distance(game.players[0].spawn,course.courseEntrance.TransformPoint(new Vector3(0,.04f,3)))<.01f,"existing player starts inside new entrance");
                lines.Add("User/12-player/Steam tests pending.");
            }
            finally{File.WriteAllLines(Report,lines);}
        }

        public static void Capture()
        {
            var s=PawnRushWorkspaceTools.FindCourse().Sections[0];
            Directory.CreateDirectory("Temp/RoyalGateRushShots");
            Shot(s.transform,new Vector3(83,110,-8),new Vector3(0,1,72),"overview",true);
            Shot(s.transform,new Vector3(-5,2.8f,85),new Vector3(0,4.5f,113),"gate",false);
            Shot(s.transform,new Vector3(0,2.3f,4),new Vector3(0,3,67),"entrance",false);
        }
        static void Shot(Transform space, Vector3 eye, Vector3 target, string name, bool ortho)
        {
            var go=new GameObject("Royal Gate preview camera"){hideFlags=HideFlags.HideAndDontSave};
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.46f,.68f,.91f);
            camera.nearClipPlane=.1f;camera.farClipPlane=500;camera.fieldOfView=65;camera.orthographic=ortho;camera.orthographicSize=88;
            go.transform.position=space.TransformPoint(eye);go.transform.LookAt(space.TransformPoint(target));
            var rt=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;
            Texture2D image=null;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes("Temp/RoyalGateRushShots/"+name+".png",image.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;if(image!=null)Object.DestroyImmediate(image);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }

        // Explicit Editor-only probes never run automatically or change player tuning.
        public static void RuntimeProbe(){ RoyalGateRushProbe.Start(); }
        public static void WalkProbe(){ RoyalGateRushProbe.Walk(); }
    }
}
