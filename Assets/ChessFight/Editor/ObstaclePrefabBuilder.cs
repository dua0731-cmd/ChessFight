using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.ProtectKing.Editor
{
    public static class ObstaclePrefabBuilder
    {
        public const string Folder = "Assets/ChessFight/Prefabs/Obstacles";
        public static readonly string[] Names = { "01_SpinningDisc", "02_JumpPad", "03_ReciprocatingPusher", "04_RotatingHammer",
            "05_RisingFallingTiles", "06_SlidingWalls", "07_WeightedBridge", "08_ConveyorChessboard", "09_ChessClockGates", "10_FallingChessPiece" };
        static Material light, dark, stone, gold, hazard;
        static Mesh ringMesh;
        static ObstacleContext context;

        [MenuItem("CHESS FIGHT/Obstacles/Create Missing Prefabs (No Map Placement)")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring assets.");
            var original = SceneManager.GetActiveScene();
            bool wasDirty = original.isDirty;
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            light = Material("BoardLight"); dark = Material("BoardDark"); stone = Material("Stone"); gold = Material("CheckpointGold"); hazard = Material("Hazard");
            ringMesh = LoadRing();
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scratch);
            int created = 0;
            try
            {
                for (int i = 0; i < Names.Length; i++)
                {
                    string path = Folder + "/" + Names[i] + ".prefab";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                    var root = new GameObject(Names[i]); context = root.AddComponent<ObstacleContext>();
                    switch (i)
                    {
                        case 0: Disc(root.transform); break;
                        case 1: Jump(root.transform); break;
                        case 2: Pusher(root.transform); break;
                        case 3: Hammer(root.transform); break;
                        case 4: Tiles(root.transform); break;
                        case 5: Walls(root.transform); break;
                        case 6: Bridge(root.transform); break;
                        case 7: Conveyor(root.transform); break;
                        case 8: Clock(root.transform); break;
                        case 9: Falling(root.transform); break;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    Object.DestroyImmediate(root);
                    if (!success) throw new Exception("Failed to save " + path);
                    created++;
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                EditorSceneManager.CloseScene(scratch, true);
            }
            if (original.isDirty != wasDirty) throw new Exception("Authoring unexpectedly changed the original scene dirty state.");
            Validate();
            Debug.Log("[CHESS FIGHT] Created " + created + " obstacle prefabs. No instances placed in the map.");
        }
        static Material Material(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ChessFight/Materials/" + name + ".mat");
            if (material == null) throw new Exception("Required material missing: " + name);
            return material;
        }
        static Transform Group(string name, Transform parent, Vector3 position = default)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }
        static GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            else if (type == PrimitiveType.Cylinder)
            {
                // A flattened CapsuleCollider balloons with nonuniform scale. Use the actual disc mesh.
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            }
            return go;
        }
        static GameObject Box(string n, Transform p, Vector3 at, Vector3 size, Material mat, bool solid = true)
            => Shape(n, p, PrimitiveType.Cube, at, size, mat, solid);
        static GameObject Cylinder(string n, Transform p, Vector3 at, Vector3 size, Material mat, bool solid = true)
            => Shape(n, p, PrimitiveType.Cylinder, at, size, mat, solid);
        static ObstacleMotion Motion(Transform t, MotionKind kind, Vector3 axis, float distance, float period, float phase = 0)
        {
            var body = t.gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var motion = t.gameObject.AddComponent<ObstacleMotion>();
            motion.kind = kind; motion.axis = axis; motion.distance = distance; motion.period = period; motion.phase = phase; motion.shove = false;
            motion.useLocalTranslationAxes = true;
            return motion;
        }
        static void Impact(Transform t, ObstacleMotion motion, float strength)
        {
            var impact = t.gameObject.AddComponent<ObstacleImpact>(); impact.motion = motion; impact.context = context; impact.strength = strength;
        }
        static void Trigger(Transform parent, Vector3 position, Vector3 size)
        {
            var go = Group("Impact sensor", parent, position).gameObject;
            var trigger = go.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = size;
        }
        static void Arrow(Transform parent, Vector3 center, float yaw, Material material)
        {
            var root = Group("Direction arrow", parent, center); root.localRotation = Quaternion.Euler(0, yaw, 0);
            Box("Shaft", root, new Vector3(0, 0, -.2f), new Vector3(.13f, .03f, .65f), material, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var wing = Box("Arrowhead", root, new Vector3(side * .17f, 0, .12f), new Vector3(.12f, .03f, .5f), material, false);
                wing.transform.localRotation = Quaternion.Euler(0, -side * 45, 0);
            }
        }
        static void Checker(Transform parent, int columns, int rows, float tile, float y)
        {
            for (int x = 0; x < columns; x++) for (int z = 0; z < rows; z++)
                Box("Chess tile " + x + "-" + z, parent, new Vector3((x - (columns - 1) * .5f) * tile, y, (z - (rows - 1) * .5f) * tile),
                    new Vector3(tile - .025f, .025f, tile - .025f), (x + z) % 2 == 0 ? light : dark, false);
        }
        static void Disc(Transform root)
        {
            Cylinder("Solid disc", root, new Vector3(0, .2f, 0), new Vector3(8, .2f, 8), stone);
            var surface = root.gameObject.AddComponent<ObstacleSurface>(); surface.context = context; surface.kind = ObstacleSurfaceKind.SpinningDisc;
            surface.rotors = new Transform[3];
            for (int i = 2; i >= 0; i--)
            {
                float diameter = i == 2 ? 8 : i == 1 ? 5.2f : 2.5f;
                var rotor = Group(i == 0 ? "Fast center" : i == 1 ? "Middle ring" : "Slow outer ring", root, new Vector3(0, .43f + (2 - i) * .045f, 0));
                Cylinder("Ring plate", rotor, Vector3.zero, new Vector3(diameter, .018f, diameter), i == 0 ? hazard : i == 1 ? light : dark, false);
                for (int arrow = 0; arrow < 6; arrow++)
                {
                    float angle = arrow * 60; var pos = Quaternion.Euler(0, angle, 0) * (Vector3.right * diameter * .4f);
                    Arrow(rotor, pos + Vector3.up * .025f, -angle, gold);
                }
                surface.rotors[i] = rotor;
            }
        }
        static void Jump(Transform root)
        {
            Box("Jump pad base", root, new Vector3(0, .175f, 0), new Vector3(3, .35f, 3), dark);
            Box("Spring plate", root, new Vector3(0, .37f, 0), new Vector3(2.6f, .08f, 2.6f), gold);
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                for (int ring = 0; ring < 3; ring++) Cylinder("Spring", root, new Vector3(x * 1.15f, .16f + ring * .07f, z * 1.15f), new Vector3(.32f, .024f, .32f), stone, false);
            Arrow(root, new Vector3(0, .425f, 0), 0, dark);
            var surface = root.gameObject.AddComponent<ObstacleSurface>(); surface.context = context; surface.kind = ObstacleSurfaceKind.JumpPad;
        }
        static void Pusher(Transform root)
        {
            for (int z = -1; z <= 1; z += 2) Box("Travel rail", root, new Vector3(0, .15f, z * 4.4f), new Vector3(12, .3f, .35f), dark);
            var moving = Group("Moving pusher", root, new Vector3(0, 1.05f, 0));
            Box("Long bar", moving, Vector3.zero, new Vector3(.8f, 1.6f, 8), hazard);
            for (int i = -3; i <= 3; i++) Box("Chess band", moving, new Vector3(0, 0, i), new Vector3(.84f, 1.63f, .3f), i % 2 == 0 ? light : dark, false);
            var motion = Motion(moving, MotionKind.Translate, Vector3.right, 4.5f, 4); Impact(moving, motion, 9);
            Trigger(moving, Vector3.zero, new Vector3(1.05f, 1.7f, 8.2f));
        }
        static void Hammer(Transform root)
        {
            Box("Back pedestal", root, new Vector3(0, 1.9f, 1.6f), new Vector3(.7f, 3.8f, .7f), stone);
            var pivot = Group("Hammer pivot", root, new Vector3(0, 3.8f, 0));
            Cylinder("Axle", pivot, Vector3.zero, new Vector3(.85f, .5f, .85f), gold, false).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Box("Handle", pivot, new Vector3(0, -1.5f, 0), new Vector3(.4f, 3, .45f), dark);
            Box("Hammer head", pivot, new Vector3(0, -3, 0), new Vector3(3, 1.25f, 1.4f), stone);
            for (int x = -1; x <= 1; x += 2) Box("Golden striking face", pivot, new Vector3(x * 1.48f, -3, 0), new Vector3(.15f, 1.3f, 1.5f), hazard, false);
            var motion = Motion(pivot, MotionKind.Rotate, Vector3.forward, 0, 5); motion.degreesPerSecond = 70; Impact(pivot, motion, 12);
            Trigger(pivot, new Vector3(0, -3, 0), new Vector3(3.25f, 1.5f, 1.65f));
        }
        static void Tiles(Transform root)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Pit edge", root, new Vector3(side * 3.25f, -.15f, 0), new Vector3(.2f, .3f, 6.7f), gold);
                Box("Pit edge", root, new Vector3(0, -.15f, side * 3.25f), new Vector3(6.7f, .3f, .2f), gold);
            }
            for (int x = 0; x < 2; x++) for (int z = 0; z < 2; z++)
            {
                var tile = Group("Lift tile " + x + "-" + z, root, new Vector3((x - .5f) * 3.15f, -.2f, (z - .5f) * 3.15f));
                Box("Tile body", tile, Vector3.zero, new Vector3(3, .4f, 3), (x + z) % 2 == 0 ? light : dark);
                Box("Direction stripe", tile, new Vector3(0, .22f, 0), new Vector3(.2f, .03f, 2.4f), gold, false);
                Motion(tile, MotionKind.Translate, Vector3.up, 3, 6, (x + z) % 2 == 0 ? 0 : .5f);
            }
        }
        static void Walls(Transform root)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Wall housing", root, new Vector3(side * 7.1f, 1.6f, side * 1.8f), new Vector3(.6f, 3.2f, 3.4f), dark);
                var moving = Group(side < 0 ? "Left sliding wall" : "Right sliding wall", root, new Vector3(side * 5.5f, 1.5f, side * 1.8f));
                Box("Rook wall", moving, Vector3.zero, new Vector3(3, 3, 3), stone);
                for (int i = -1; i <= 1; i++) Box("Battlement", moving, new Vector3(i, 1.7f, 0), new Vector3(.65f, .5f, 3), light, false);
                var motion = Motion(moving, MotionKind.Gate, Vector3.left * side, 3.5f, 4.5f, side < 0 ? 0 : .5f); Impact(moving, motion, 5);
                Trigger(moving, Vector3.zero, new Vector3(3.15f, 3.1f, 3.15f));
            }
        }
        static void Bridge(Transform root)
        {
            Box("Central support", root, new Vector3(0, .35f, 0), new Vector3(1.4f, .7f, 2), stone);
            var pivot = Group("Tilting deck", root, new Vector3(0, 1, 0));
            var rb = pivot.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var bridge = pivot.gameObject.AddComponent<WeightedBridge>(); bridge.context = context;
            Box("Solid deck", pivot, Vector3.zero, bridge.deckSize, dark); Checker(pivot, 4, 8, 1.48f, .216f);
            for (int side = -1; side <= 1; side += 2)
            {
                var ramp = Box("Entry ramp", root, new Vector3(0, .52f, side * 7.2f), new Vector3(6, .25f, 2.9f), stone);
                ramp.transform.localRotation = Quaternion.Euler(side * 24, 0, 0);
                Box("Balance stripe", pivot, new Vector3(side * 2.96f, .23f, 0), new Vector3(.09f, .03f, 12), gold, false);
            }
        }
        static void Conveyor(Transform root)
        {
            Box("Conveyor base", root, new Vector3(0, .2f, 0), new Vector3(6, .4f, 8), stone); Checker(root, 6, 8, 1, .416f);
            var surface = root.gameObject.AddComponent<ObstacleSurface>(); surface.context = context; surface.kind = ObstacleSurfaceKind.Conveyor;
            surface.direction = Vector3.back; surface.beltLength = 7.5f;
            var markers = new List<Transform>();
            for (int z = -3; z <= 3; z++)
            {
                var marker = Group("Moving belt arrow", root, new Vector3(0, .455f, z)); Arrow(marker, Vector3.zero, 180, hazard); markers.Add(marker);
            }
            surface.beltMarkers = markers.ToArray();
            for (int side = -1; side <= 1; side += 2) Box("Belt rim", root, new Vector3(side * 3.1f, .25f, 0), new Vector3(.2f, .5f, 8), dark);
        }
        static void Clock(Transform root)
        {
            for (int i = -1; i <= 1; i++) Box("Gate pillar", root, new Vector3(i * 4.15f, 2.8f, 0), new Vector3(.3f, 5.6f, 1), dark);
            Box("Clock arch", root, new Vector3(0, 5.2f, 0), new Vector3(8.6f, 1.2f, 1.2f), stone);
            for (int side = -1; side <= 1; side += 2)
            {
                var gate = Group(side < 0 ? "Left gate" : "Right gate", root, new Vector3(side * 2.08f, 1.6f, 0));
                Box("Gate panel", gate, Vector3.zero, new Vector3(3.8f, 3.2f, .6f), side < 0 ? light : dark);
                for (int x = -1; x <= 1; x++) Box("Vertical inset", gate, new Vector3(x, 0, -.32f), new Vector3(.12f, 2.8f, .04f), gold, false);
                Motion(gate, MotionKind.Gate, Vector3.up, 4.1f, 5, side < 0 ? 0 : .5f);
                var face = Cylinder("Clock face", root, new Vector3(side * 2.08f, 5.4f, -.7f), new Vector3(1.65f, .08f, 1.65f), gold, false);
                face.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var hand = Group("Clock hand", root, new Vector3(side * 2.08f, 5.4f, -.8f));
                Box("Hand", hand, new Vector3(0, .29f, 0), new Vector3(.09f, .65f, .05f), dark, false);
                var clock = Motion(hand, MotionKind.Rotate, Vector3.forward, 0, 5, side < 0 ? 0 : .5f); clock.degreesPerSecond = -72;
            }
        }
        static void Falling(Transform root)
        {
            var falling = root.gameObject.AddComponent<FallingChessPiece>(); falling.context = context;
            var piece = Group("Falling pawn", root, Vector3.up * falling.dropHeight); falling.piece = piece;
            Cylinder("Pawn foot", piece, new Vector3(0, .15f, 0), new Vector3(1.8f, .15f, 1.8f), dark, false);
            Cylinder("Pawn body", piece, new Vector3(0, .8f, 0), new Vector3(.7f, .55f, .7f), light, false);
            Cylinder("Pawn head", piece, new Vector3(0, 1.6f, 0), new Vector3(1.05f, .3f, 1.05f), gold, false);
            falling.warning = Cylinder("Landing warning", root, new Vector3(0, .035f, 0), new Vector3(10, .04f, 10), hazard, false).transform;
            var ring = Group("Expanding shockwave", root, new Vector3(0, .095f, 0)); falling.shockRing = ring;
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh = ringMesh; ring.gameObject.AddComponent<MeshRenderer>().sharedMaterial = gold;
            for (int i = 0; i < 4; i++) Arrow(root, Quaternion.Euler(0, i * 90, 0) * new Vector3(0, .08f, 4.5f), i * 90 + 180, dark);
        }
        static Mesh LoadRing()
        {
            const string path = Folder + "/ShockwaveRing.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) return existing;
            const int n = 64; var vertices = new Vector3[n * 2]; var triangles = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                var radial = Quaternion.Euler(0, i * 360f / n, 0) * Vector3.forward;
                vertices[i * 2] = radial * .43f; vertices[i * 2 + 1] = radial * .5f;
                int next = (i + 1) % n * 2, j = i * 6;
                triangles[j] = i * 2; triangles[j + 1] = next + 1; triangles[j + 2] = next;
                triangles[j + 3] = i * 2; triangles[j + 4] = i * 2 + 1; triangles[j + 5] = next + 1;
            }
            var mesh = new Mesh { name = "Shockwave ring", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        [MenuItem("CHESS FIGHT/Obstacles/Validate Prefab Assets")]
        public static void Validate()
        {
            var lines = new List<string>();
            foreach (string name in Names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".prefab");
                if (prefab == null || prefab.GetComponent<ObstacleContext>() == null) throw new Exception("Missing obstacle prefab/context: " + name);
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0) throw new Exception("Missing script in " + name);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    if (renderer.sharedMaterial == null) throw new Exception("Missing material in " + name);
                if (prefab.GetComponent<ObstacleContext>().match != null) throw new Exception("Unexpected scene reference in " + name);
                lines.Add("PASS: " + name + " | renderers=" + prefab.GetComponentsInChildren<Renderer>(true).Length + " colliders=" + prefab.GetComponentsInChildren<Collider>(true).Length);
            }
            Directory.CreateDirectory("Artifacts/ChessFight");
            File.WriteAllText("Artifacts/ChessFight/ObstaclePrefabValidation.txt", DateTime.Now.ToString("s") + "\n" + string.Join("\n", lines));
            Debug.Log("[CHESS FIGHT] Ten obstacle prefab assets validated.");
        }
    }
}
