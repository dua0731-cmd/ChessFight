using System;
using System.IO;
using System.Linq;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    public static partial class KingRushOpeningBuilder
    {
        public const string ScenePath = "Assets/Scenes/KingRushOpening.unity";
        const string Folder = "Assets/Prefabs/KingRushOpening";
        static Material cream, wood, coral, mint, blue, gold, dark, white, black, water;
        // Its menu (ChessFight/King Rush/Open Opening Course) went with the scene on 10-08: the scene was
        // deleted and this would rebuild it. The code is kept for now.
        public static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateScene(); else EditorSceneManager.OpenScene(ScenePath);
            ExtendCourse();
            ExtendFinal();
        }
        static Material Mat(string name, Color color)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { color = color };
            material.SetFloat("_Glossiness", .15f); AssetDatabase.CreateAsset(material, path); return material;
        }
        static void Palette()
        {
            Directory.CreateDirectory(Folder + "/Materials"); AssetDatabase.Refresh();
            cream = Mat("Cream", new Color(.97f, .86f, .65f)); wood = Mat("Wood", new Color(.6f, .34f, .18f));
            coral = Mat("Coral", new Color(.92f, .35f, .27f)); mint = Mat("Mint", new Color(.27f, .72f, .56f));
            blue = Mat("Felt", new Color(.18f, .42f, .6f)); gold = Mat("Gold", new Color(1, .71f, .16f));
            dark = Mat("Ink", new Color(.12f, .2f, .27f)); white = Mat("WhiteTeam", new Color(.94f, .91f, .8f));
            black = Mat("BlackTeam", new Color(.16f, .2f, .28f)); water = Mat("Water", new Color(.22f, .64f, .84f));
        }
        static GameObject Shape(Transform parent, string name, PrimitiveType shape, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(shape); go.name = name; go.transform.SetParent(parent);
            go.transform.localPosition = at; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Material m) => Shape(parent, name, PrimitiveType.Cube, at, size, m);
        static void Floor(Transform t, string name, float x, float y, float z, float width, float length, Material m)
        { Box(t, name, new Vector3(x, y - .3f, z), new Vector3(width, .6f, length), m); }
        static TextMesh Label(Transform t, string name, Vector3 at, string text, float size = .035f)
        {
            var label = new GameObject(name).AddComponent<TextMesh>(); label.transform.SetParent(t); label.transform.position = at;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.fontSize = 64; label.characterSize = size;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material; label.text = text; label.color = dark.color; return label;
        }
        static void Sign(Transform t, Vector3 at, string text)
        {
            Box(t, "Sign board", at, new Vector3(7, 1.5f, .22f), cream);
            Label(t, "Directions", at + Vector3.back * .13f, text, .06f);
            Box(t, "Sign post", at + Vector3.down * 1.2f, new Vector3(.2f, 1.8f, .2f), wood);
        }
        static void Checkpoint(Transform t, int index, string title, float z, float y = 0)
        {
            var go = new GameObject("Checkpoint " + index + " · " + title); go.transform.SetParent(t); go.transform.position = new Vector3(0, y, z);
            var cp = go.AddComponent<KingRushCourseCheckpoint>(); cp.order = index; cp.title = title;
            Box(t, "Checkpoint stripe", new Vector3(0, y + .007f, z), new Vector3(15.8f, .014f, .2f), gold);
        }
        static void Rails(Transform t, float from, float to, float width = 16, float y = 0)
        {
            foreach (int side in new[] { -1, 1 })
                Box(t, "Toy edge", new Vector3(side * (width * .5f + .18f), y + .2f, (from + to) * .5f), new Vector3(.35f, .4f, to - from), cream);
        }
        static GameObject Section(Transform parent, string name, Action<Transform> build)
        {
            string path = Folder + "/" + name + ".prefab";
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (saved != null) return (GameObject)PrefabUtility.InstantiatePrefab(saved, parent);
            var root = new GameObject(name); root.transform.SetParent(parent); build(root.transform);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction); return root;
        }
        static void PawnProp(Transform t, string name, Vector3 at, Material m, bool moving)
        {
            var root = new GameObject(name); root.transform.SetParent(t); root.transform.position = at;
            Shape(root.transform, "Base", PrimitiveType.Cylinder, new Vector3(0, .25f, 0), new Vector3(1.8f, .25f, 1.8f), m);
            Shape(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0, 1.1f, 0), new Vector3(1, .8f, 1), m);
            Shape(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 2.1f, 0), Vector3.one * 1.3f, m);
            if (moving)
            {
                var motion = root.AddComponent<KingRushToyMotion>(); motion.period = 7; motion.radius = 2; motion.offset = at.z * .1f;
                root.GetComponent<Rigidbody>().isKinematic = true;
            }
        }
        static void StartArea(Transform t)
        {
            Floor(t, "Toy box lid · start", 0, 0, 0, 40, 40, coral); Rails(t, -20, 20, 40);
            Box(t, "Rear wall", new Vector3(0, 2, -20), new Vector3(40, 4, .5f), cream);
            Label(t, "Start title", new Vector3(0, 3, 12), "01 / 장난감 상자\n정면 노란 화살표를 따라 달려요", .055f);
            for (int i = 0; i < 12; i++)
                Box(t, "Spawn marker " + i, KingRushOpening.Spawn(i) + Vector3.up * .01f, new Vector3(1.4f, .02f, 1.4f), i < 6 ? white : black);
            Checkpoint(t, 0, "출발 광장", 0);
            for (int z = 7; z <= 19; z += 4) Arrow(t, new Vector3(0, .02f, z));
        }
        static void Arrow(Transform t, Vector3 p)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                var b = Box(t, "Route chevron", p + Vector3.right * s * .3f, new Vector3(.12f, .025f, 1), gold);
                b.transform.localRotation = Quaternion.Euler(0, -s * 40, 0);
            }
        }
        static void PawnField(Transform t)
        {
            Floor(t, "Spinning pawn field", 0, 0, 32, 16, 24, mint); Rails(t, 20, 44); Checkpoint(t, 1, "빙글빙글 폰", 22);
            foreach (float z in new[] { 28f, 36f })
                foreach (int s in new[] { -1, 1 }) PawnProp(t, "Orbit pawn", new Vector3(s * 3.5f, 0, z), z < 30 ? cream : coral, true);
            Sign(t, new Vector3(-4, 2.2f, 24), "빙글빙글 폰\n빈틈으로 지나가요");
            Arrow(t, new Vector3(0, .02f, 40));
        }
        static void Ramp(Transform t, string name, float from, float to, float y0, float y1, Material m)
        {
            float run = to - from, rise = y1 - y0;
            var ramp = Box(t, name, new Vector3(0, (y0 + y1) * .5f - .3f, (from + to) * .5f), new Vector3(16, .6f, Mathf.Sqrt(run * run + rise * rise)), m);
            ramp.transform.rotation = Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0, 0);
        }
        static void Heads(Transform t)
        {
            Ramp(t, "15 degree climb", 44, 66, 0, 5.9f, blue); Floor(t, "Ramp summit", 0, 5.9f, 68, 16, 4, mint);
            Checkpoint(t, 2, "굴러오는 머리", 43);
            for (int i = 0; i < 3; i++)
            {
                var head = Shape(t, "Rolling head " + i, PrimitiveType.Sphere, new Vector3((i - 1) * 4, 7.1f, 66), Vector3.one * 2.4f, i == 1 ? gold : cream);
                var motion = head.AddComponent<KingRushToyMotion>(); motion.motion = KingRushToyMotion.Motion.RollingHead;
                motion.period = 8; motion.offset = i * 2.4f; head.GetComponent<Rigidbody>().isKinematic = true;
                head.AddComponent<RagdollHazard>().minImpact = 1;
            }
            Sign(t, new Vector3(5, 2.2f, 43), "굴러오는 머리\n레인을 바꿔 피해요");
        }
        static void Slide(Transform t)
        {
            Checkpoint(t, 3, "펠트 미끄럼틀", 68, 5.9f); Ramp(t, "Felt slide", 70, 94, 5.9f, 0, coral);
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = Box(t, "Slide trim", new Vector3(side * 8.15f, 3.1f, 82), new Vector3(.3f, .45f, 24.7f), cream);
                rail.transform.rotation = Quaternion.Euler(13.8f, 0, 0);
            }
            Sign(t, new Vector3(-4, 8.1f, 69), "펠트 미끄럼틀\n달리거나 태클로 내려가요");
        }
        static void Clock(Transform t)
        {
            Floor(t, "Clock approach", 0, 0, 96, 16, 4, mint); Checkpoint(t, 4, "체스 시계", 96);
            for (int i = 0; i < 2; i++)
            {
                var button = Box(t, "Clock button " + i, new Vector3(0, -.3f, 102 + i * 8), new Vector3(16, .6f, 7.5f), i == 0 ? gold : coral);
                var motion = button.AddComponent<KingRushToyMotion>(); motion.motion = KingRushToyMotion.Motion.ClockButton;
                motion.period = 3; motion.offset = i * 1.5f; button.GetComponent<Rigidbody>().isKinematic = true;
            }
            Floor(t, "Clock exit", 0, 0, 117, 16, 6, mint);
            Sign(t, new Vector3(-4, 2.2f, 96), "체스 시계\n오르내리는 버튼을 건너요");
        }
        static void Pond(Transform t)
        {
            Floor(t, "Pond approach", 0, 0, 123, 16, 6, blue); Checkpoint(t, 5, "도약대 연못", 121);
            Floor(t, "Soft landing", 0, 0, 139, 20, 10, mint);
            Box(t, "Spring face", new Vector3(0, .04f, 124), new Vector3(8, .08f, 2.5f), gold);
            var root = new GameObject("Pond launch pad"); root.transform.SetParent(t); root.transform.position = new Vector3(0, .1f, 124);
            var trigger = root.AddComponent<BoxCollider>(); trigger.size = new Vector3(8, .6f, 2.5f); trigger.center = Vector3.up * .3f; trigger.isTrigger = true;
            root.AddComponent<LaunchPad>().Configure(new Vector3(0, -.1f, 15), 4);
            Sign(t, new Vector3(-4, 2.2f, 121), "도약대 연못\n노란 발판을 밟아 건너요");
            Arrow(t, new Vector3(0, .11f, 124));
        }
        static void Promotion(Transform t)
        {
            Floor(t, "Promotion approach", 0, 0, 157, 20, 26, coral); Rails(t, 144, 165, 20); Checkpoint(t, 6, "승격 발판", 145);
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = new Vector3(i == 0 ? -3 : 3, .03f, 155);
                var padRoot = new GameObject("Promotion rule " + i); padRoot.transform.SetParent(t); padRoot.transform.position = p;
                padRoot.AddComponent<KingRushPad>().index = i;
                Box(t, "Promotion pad", p, new Vector3(2.5f, .06f, 2.5f), gold);
                Label(t, "Promotion label " + i, p + Vector3.up * 2, "승격", .06f);
            }
            Sign(t, new Vector3(0, 3, 160), "혼자 1.5초 서면 승격\n나이트 / 비숍 · 능력은 제작 전");
            Arrow(t, new Vector3(0, .02f, 163));
        }
        static void Arena(Transform t)
        {
            Floor(t, "Blue arena south", 0, 0, 179, 30, 18, blue);
            // Leave real openings in the felt: never put a solid floor under the capture mouths.
            Floor(t, "Blue arena center north", 0, 0, 190, 15.4f, 4, blue);
            Floor(t, "Blue arena north lip", 0, 0, 193, 30, 2, blue);
            foreach (int side in new[] { -1, 1 }) Floor(t, "Blue arena side lip", side * 13.65f, 0, 190, 2.7f, 4, blue);
            foreach (int side in new[] { -1, 1 }) Box(t, "Toy box side", new Vector3(side * 15.25f, 2.5f, 182), new Vector3(.5f, 5, 24), wood);
            Label(t, "Arena banner", new Vector3(0, 4, 177), "02 / 잡은 말 상자\n상대를 우리 팀 상자에 넣어 다리를 만들어요", .045f);
            foreach (Vector3 p in new[] { new Vector3(-5, 0, 181), new Vector3(5, 0, 181), new Vector3(0, 0, 187) })
            {
                var fallen = Box(t, "Fallen giant piece", p + Vector3.up * .45f, new Vector3(1.5f, .9f, 4), cream);
                fallen.transform.rotation = Quaternion.Euler(0, p.x == 0 ? 90 : 35, 0);
                Shape(t, "Giant head cover", PrimitiveType.Sphere, p + new Vector3(1.2f, .65f, 1.6f), Vector3.one * 1.3f, cream);
            }
            for (int team = 0; team < 2; team++)
            {
                float x = team == 0 ? -10 : 10; Material m = team == 0 ? white : black;
                var volume = new GameObject("Capture box " + team); volume.transform.SetParent(t); volume.transform.position = new Vector3(x, -.2f, 190);
                volume.AddComponent<KingRushCaptureBox>().team = team;
                Floor(t, "Capture pit bottom", x, -1.2f, 190, 4.6f, 4.6f, m);
                foreach (int side in new[] { -1, 1 })
                {
                    Box(t, "Pit side", new Vector3(x + side * 2.15f, -.2f, 190), new Vector3(.3f, 2, 4.6f), m);
                    Box(t, "Pit rim", new Vector3(x, -.2f, 190 + side * 2.15f), new Vector3(4, 2, .3f), m);
                }
                Label(t, "Box label " + team, new Vector3(x, 3.8f, 192), "", .08f).color = team == 0 ? dark.color : cream.color;
                Box(t, "Box sign", new Vector3(x, 3.8f, 192.2f), new Vector3(5.4f, 2.4f, .2f), m);
            }
            for (int i = 0; i < 12; i++)
            {
                var p = KingRushOpening.Shelf(i);
                Floor(t, "Captured shelf " + i, p.x, p.y, p.z, 2.7f, 2.5f, cream);
                Box(t, "Shelf back", p + new Vector3(0, .5f, -1.2f), new Vector3(2.7f, 1, .15f), wood);
            }
            Label(t, "Shelf caption", new Vector3(0, 8.8f, 164.7f), "잡힌 말 대기 선반 · 3초 뒤 복귀", .027f);
        }
        static void Exit(Transform t)
        {
            for (int team = 0; team < 2; team++)
            {
                float x = team == 0 ? -5 : 5; var m = team == 0 ? white : black;
                for (int i = 0; i < 6; i++)
                {
                    var plank = Box(t, "Bridge " + team + " " + i, new Vector3(x, -.2f, 195 + i * 2), new Vector3(4, .4f, 2), m); plank.SetActive(false);
                }
                var door = Box(t, "Gate " + team, new Vector3(x, 3, 207), new Vector3(4, 6, .4f), m);
                var gate = door.AddComponent<PawnGate>(); gate.team = team; gate.barrier = door.GetComponent<Collider>();
                gate.exitBounds = new Bounds(new Vector3(x, 2, 208.5f), new Vector3(4, 5, 2));
                Label(t, "Gate label " + team, new Vector3(x, 3, 206.7f), "", .07f).color = team == 0 ? dark.color : cream.color;
            }
            Floor(t, "Next course staging", 0, 0, 213, 30, 14, coral);
            foreach (float x in new[] { -11f, 0, 11f }) Box(t, "Exit wall", new Vector3(x, 3, 207), new Vector3(x == 0 ? 6 : 8, 6, .4f), wood);
            Rails(t, 206, 220, 30);
            Box(t, "End wall", new Vector3(0, 2, 220), new Vector3(30, 4, .5f), cream);
            Label(t, "First course end", new Vector3(0, 3, 217), "첫 코스 통과!\n다음: 성곽 갈림길 · 제작 예정", .045f);
        }
        static void BoxApproaches(Transform t)
        {
            // A narrow approach makes the draft 0.8 m rim usable with the unmodified shared grab.
            foreach (int side in new[] { -1, 1 })
            {
                var ramp = Box(t, "Box approach ramp", new Vector3(side * 10, .1f, 186.05f), new Vector3(2.5f, .6f, Mathf.Sqrt(3.3f * 3.3f + .8f * .8f)), gold);
                ramp.transform.rotation = Quaternion.Euler(-Mathf.Atan2(.8f, 3.3f) * Mathf.Rad2Deg, 0, 0);
                Arrow(t, new Vector3(side * 10, .035f, 183.8f));
            }
        }
        static void CreateScene()
        {
            if (File.Exists(ScenePath)) return; // Artist edits and existing prefabs always win.
            Palette(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("King Rush · opening course"); root.AddComponent<PhysicsProfile>(); var game = root.AddComponent<KingRushOpening>();
            const string lab = "Assets/";
            game.pawnPrefab = AssetDatabase.LoadAssetAtPath<RagdollPawn>(lab + "Prefabs/RagdollPawn.prefab");
            game.tuning = AssetDatabase.LoadAssetAtPath<RagdollTuning>(lab + "Settings/RagdollTuning.asset"); game.white = white; game.black = black;
            if (game.pawnPrefab == null || game.tuning == null) throw new Exception("Missing ragdoll assets");
            Section(root.transform, "01_Start", StartArea); Section(root.transform, "02_PawnField", PawnField);
            Section(root.transform, "03_RollingHeads", Heads); Section(root.transform, "04_FeltSlide", Slide);
            Section(root.transform, "05_Clock", Clock); Section(root.transform, "06_Pond", Pond);
            Section(root.transform, "07_Promotion", Promotion); Section(root.transform, "08_CaptureArena", Arena);
            Section(root.transform, "08b_BoxApproaches", BoxApproaches); Section(root.transform, "09_Exit", Exit);
            Section(root.transform, "10_Water", t => {
                Box(t, "Red course water", new Vector3(0, -4, 70), new Vector3(100, 4, 196), water).AddComponent<WaterZone>();
                Box(t, "Bridge water", new Vector3(0, -4, 202), new Vector3(100, 4, 16), water).AddComponent<WaterZone>();
            });
            game.checkpoints = root.GetComponentsInChildren<KingRushCourseCheckpoint>().OrderBy(c => c.order).ToArray();
            game.pads = root.GetComponentsInChildren<KingRushPad>().OrderBy(p => p.index).ToArray();
            game.gates = root.GetComponentsInChildren<PawnGate>().OrderBy(g => g.team).ToArray();
            game.boxes = root.GetComponentsInChildren<KingRushCaptureBox>().OrderBy(b => b.team).ToArray();
            game.waters = root.GetComponentsInChildren<WaterZone>();
            game.planks = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Bridge ") && t.name != "Bridge water").OrderBy(t => t.name).Select(t => t.gameObject).ToArray();
            var labels = root.GetComponentsInChildren<TextMesh>();
            game.boxLabels = labels.Where(l => l.name.StartsWith("Box label")).OrderBy(l => l.name).ToArray();
            game.gateLabels = labels.Where(l => l.name.StartsWith("Gate label")).OrderBy(l => l.name).ToArray();
            game.padLabels = labels.Where(l => l.name.StartsWith("Promotion label")).OrderBy(l => l.name).ToArray();
            var camera = new GameObject("Main Camera"); camera.tag = "MainCamera";
            var view = camera.AddComponent<Camera>(); view.backgroundColor = new Color(.64f, .82f, .93f); view.clearFlags = CameraClearFlags.SolidColor; view.farClipPlane = 450;
            camera.AddComponent<AudioListener>(); var rig = camera.AddComponent<LabCamera>(); rig.distance = 5.5f; rig.pitch = 18;
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.1f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48, -30, 0); RenderSettings.ambientLight = new Color(.65f, .69f, .72f);
            RenderSettings.fog = true; RenderSettings.fogColor = view.backgroundColor; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 160; RenderSettings.fogEndDistance = 380;
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
        }
        public static void BuildBatch()
        {
            try
            {
                CreateScene(); EditorSceneManager.OpenScene(ScenePath); ExtendCourse(); ExtendFinal(); AssetDatabase.SaveAssets();
                string output = Path.GetFullPath("Builds/KingRushOpening"); Directory.CreateDirectory(output);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { ScenePath, KingRushBuilder.ScenePath, "Assets/Scenes/Lobby.unity", "Assets/Scenes/PawnRush_Course01.unity", "Assets/Scenes/SwordFight.unity" },
                    locationPathName = Path.Combine(output, "ChessFight.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
                if (report.summary.result != BuildResult.Succeeded) throw new Exception("Opening course build failed: " + report.summary.result);
                Debug.Log("[KingRushOpeningBuild] PASS -> " + output); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
