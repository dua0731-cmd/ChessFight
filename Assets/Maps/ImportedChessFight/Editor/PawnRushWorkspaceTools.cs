using System;
using System.IO;
using System.Linq;
using ChessFight.Gameplay.PawnRush;
using ChessFight.RagdollLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ChessFight.Maps.Editor
{
    public static class PawnRushWorkspaceTools
    {
        public const string ScenePath = "Assets/Scenes/ImportedChessFightMap.unity";
        public const string Root = "Assets/Maps/PawnRush";
        const string Obstacles = "Assets/Maps/ImportedChessFight/Prefabs/Obstacles/";
        static Material floor, rim, hazard, gold, white, black;

        [MenuItem("ChessFight/Pawn Rush/작업환경 열기")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            var course = FindCourse();
            Selection.activeGameObject = course.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
        public static PawnRushCourse FindCourse()
        {
            var all = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PawnRushCourse>(true)).ToArray();
            if (all.Length != 1) throw new InvalidOperationException("ImportedChessFightMap의 Pawn Rush Workspace 하나를 선택하세요.");
            return all[0];
        }

        // Explicit provisioning entry point. Never runs automatically on import.
        public static void SetupBatch()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (scene.GetRootGameObjects().Any(r => r.GetComponent<PawnRushCourse>() != null))
                throw new InvalidOperationException("작업환경이 이미 있습니다. 재설치 대신 Inspector의 미리보기 버튼을 사용하세요.");
            Directory.CreateDirectory(Root + "/Sections/Obstacles");
            Directory.CreateDirectory(Root + "/Sections/Missions");
            Directory.CreateDirectory(Root + "/Templates");
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();
            floor = Material("Floor", new Color(.72f, .72f, .76f));
            rim = Material("Border", new Color(.20f, .23f, .30f));
            hazard = Material("Obstacle", new Color(.72f, .28f, .16f));
            gold = Material("Mission", new Color(.92f, .66f, .15f));
            white = Material("WhiteTeam", new Color(.94f, .94f, .90f));
            black = Material("BlackTeam", new Color(.08f, .10f, .14f));
            var obstacleSections = new[] {
                CreateSection("O01_Slalom", SectionKind.Obstacle, 48, 0, false),
                CreateSection("O02_Timing", SectionKind.Obstacle, 48, 1, false),
                CreateSection("O03_Wind", SectionKind.Obstacle, 56, 2, false) };
            var missionSections = new[] {
                CreateSection("M01_Courtyard", SectionKind.Mission, 36, 0, false),
                CreateSection("M02_Columns", SectionKind.Mission, 40, 1, false),
                CreateSection("M03_Terraces", SectionKind.Mission, 40, 2, false) };
            CreateSection("Template_Obstacle", SectionKind.Obstacle, 48, -1, true);
            CreateSection("Template_Mission", SectionKind.Mission, 40, -1, true);
            var catalog = ScriptableObject.CreateInstance<PawnRushCatalog>();
            catalog.obstacles = obstacleSections; catalog.missions = missionSections;
            AssetDatabase.CreateAsset(catalog, Root + "/PawnRushCatalog.asset");
            var roots = scene.GetRootGameObjects();
            float right = 0;
            foreach (var renderer in roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true))) right = Mathf.Max(right, renderer.bounds.max.x);
            var workspace = new GameObject("Pawn Rush Workspace");
            workspace.transform.position = new Vector3(Mathf.Ceil(right / 10) * 10 + 40, 0, 0);
            var course = workspace.AddComponent<PawnRushCourse>();
            course.catalog = catalog;
            course.courseEntrance = Child("Course Entrance", workspace.transform, Vector3.zero);
            var start = Child("Start", workspace.transform, Vector3.zero);
            Cube("Start Deck", start, new Vector3(0, -.5f, -6), new Vector3(20, 1, 12), floor);
            Label("PAWN RUSH", start, new Vector3(0, 4, -10), 1);
            var spawn = Child("Playtest Spawn", start, new Vector3(-3, .04f, -5));
            course.finishPlatform = Child("Finish", workspace.transform, Vector3.zero);
            Cube("Finish Deck", course.finishPlatform, new Vector3(0, -.5f, 6), new Vector3(20, 1, 12), gold);
            Label("FINISH", course.finishPlatform, new Vector3(0, 4, 9), 1);
            BuildPreview(course, course.seed);
            SetPlaytestStart(spawn.position);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("맵 씬 저장 실패");
            AssetDatabase.SaveAssets();
            Validate(course);
            Debug.Log("Pawn Rush workspace created in ImportedChessFightMap. Original map retained beside it.");
        }

        static Material Material(string id, Color color)
        {
            string path = Root + "/Materials/" + id + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var result = new Material(Shader.Find("Standard")) { color = color };
            result.SetFloat("_Glossiness", .18f);
            AssetDatabase.CreateAsset(result, path);
            return result;
        }
        static Transform Child(string name, Transform parent, Vector3 local)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = local; return t;
        }
        static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material mat, bool collision = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static GameObject Cube(string n, Transform p, Vector3 at, Vector3 size, Material mat, bool collision = true) => Shape(n, PrimitiveType.Cube, p, at, size, mat, collision);
        static void Label(string text, Transform parent, Vector3 at, float scale)
        {
            var t = Child(text, parent, at);
            var mesh = t.gameObject.AddComponent<TextMesh>(); mesh.text = text; mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center; mesh.fontSize = 64; mesh.characterSize = .12f * scale; mesh.color = Color.white;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.GetComponent<Renderer>().sharedMaterial = mesh.font.material;
        }
        static void ExistingObstacle(string file, Transform parent, Vector3 at, float yaw = 0)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Obstacles + file + ".prefab");
            if (prefab == null) throw new InvalidOperationException("장애물 없음: " + file);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = at; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }
        static PawnRushSection CreateSection(string id, SectionKind kind, float length, int variant, bool template)
        {
            string path = Root + (template ? "/Templates/" : kind == SectionKind.Obstacle ? "/Sections/Obstacles/" : "/Sections/Missions/") + id + ".prefab";
            if (File.Exists(path)) throw new InvalidOperationException("기존 구역 프리팹을 덮어쓰지 않습니다: " + path);
            var go = new GameObject(id);
            try
            {
                var section = go.AddComponent<PawnRushSection>(); section.sectionId = id; section.kind = kind;
                section.entrance = Child("Entrance", go.transform, Vector3.zero);
                section.exit = Child("Exit", go.transform, Vector3.forward * length);
                section.authoringBounds = new Bounds(new Vector3(0, 4, length / 2), new Vector3(20, 8, length));
                var geo = Child("Geometry", go.transform, Vector3.zero);
                Cube("Deck", geo, new Vector3(0, -.5f, length / 2), new Vector3(20, 1, length), floor);
                for (int side = -1; side <= 1; side += 2)
                    Cube("Side Rail", geo, new Vector3(side * 10, kind == SectionKind.Mission ? 2.5f : .5f, length / 2), new Vector3(.4f, kind == SectionKind.Mission ? 5 : 1, length), rim);
                Cube("Entrance Line", geo, new Vector3(0, .02f, .4f), new Vector3(19.5f, .04f, .5f), kind == SectionKind.Mission ? gold : rim, false);
                Label(kind == SectionKind.Mission ? "MISSION - F : ACTIVATE 3 SEALS" : "OBSTACLE", go.transform, new Vector3(0, 4, 2), .8f);
                var content = Child(kind == SectionKind.Mission ? "Mission Content" : "Obstacles", go.transform, Vector3.zero);
                if (kind == SectionKind.Obstacle)
                {
                    if (variant == 0)
                    {
                        for (int i = 0; i < 4; i++) Shape("Pawn Column", PrimitiveType.Cylinder, content, new Vector3(i % 2 == 0 ? -3 : 3, 1.5f, 10 + i * 8), new Vector3(5, 1.5f, 5), hazard);
                    }
                    if (variant == 1)
                    {
                        ExistingObstacle("09_ChessClockGates", content, new Vector3(0, 0, 14));
                        ExistingObstacle("04_RotatingHammer", content, new Vector3(0, 0, 34));
                    }
                    if (variant == 2)
                    {
                        ExistingObstacle("08_ConveyorChessboard", content, new Vector3(0, 0, 18), 180);
                        ExistingObstacle("15_AirVent", content, new Vector3(-7, 0, 34), 90);
                        for (int side = -1; side <= 1; side += 2) Cube("Cover", content, new Vector3(side * 5, 1, 40), new Vector3(2, 2, 4), rim);
                    }
                }
                else
                {
                    var mission = go.AddComponent<TeamMission>();
                    if (variant == 1) for (int i = 0; i < 4; i++) Shape("Cover Column", PrimitiveType.Cylinder, content, new Vector3(i % 2 == 0 ? -3 : 3, 2, 10 + i * 4), new Vector3(2, 2, 2), rim);
                    for (int i = 0; i < 3; i++)
                    {
                        float level = variant == 2 ? (i + 1) * .35f : 0;
                        var at = new Vector3((i - 1) * 6, level, i == 1 ? 23 : 13);
                        if (level > 0) Cube("Terrace", content, new Vector3(at.x, level / 2, at.z), new Vector3(5, level, 5), rim);
                        var seal = Cube("Seal " + (i + 1), content, at + Vector3.up * .65f, new Vector3(1.2f, 1.3f, 1.2f), gold);
                        var objective = seal.AddComponent<MissionObjective>(); objective.mission = mission; objective.objectiveId = mission.objectiveIds[i];
                        objective.whiteIndicator = Shape("White Progress", PrimitiveType.Sphere, seal.transform, new Vector3(-.28f, .8f, 0), Vector3.one * .25f, white, false).GetComponent<Renderer>();
                        objective.blackIndicator = Shape("Black Progress", PrimitiveType.Sphere, seal.transform, new Vector3(.28f, .8f, 0), Vector3.one * .25f, black, false).GetComponent<Renderer>();
                        Label("F", content, at + Vector3.up * 2, .75f);
                    }
                    var gates = Child("Team Gates", go.transform, Vector3.forward * (length - 2));
                    for (int team = 0; team < 2; team++)
                    {
                        var gate = Child(team == 0 ? "White Gate" : "Black Gate", gates, new Vector3(team == 0 ? -5 : 5, 0, 0)).gameObject.AddComponent<MissionTeamGate>();
                        gate.mission = mission; gate.allowedTeam = team;
                        var barrier = Cube("Passage Barrier", gate.transform, new Vector3(0, 2.5f, 0), new Vector3(10, 5, .6f), rim);
                        barrier.GetComponent<Renderer>().enabled = false; gate.barrier = barrier.GetComponent<Collider>();
                        gate.doorVisual = Cube("Door Visual", gate.transform, new Vector3(0, 2.5f, 0), new Vector3(9.7f, 4.9f, .45f), team == 0 ? white : black, false).transform;
                        Label(team == 0 ? "WHITE" : "BLACK", gate.transform, new Vector3(0, 5.6f, -.5f), .9f);
                    }
                    Cube("Gate Lintel", gates, new Vector3(0, 5.25f, 0), new Vector3(20, .5f, 3), gold);
                }
                section.ValidateDefinition();
                return PrefabUtility.SaveAsPrefabAsset(go, path).GetComponent<PawnRushSection>();
            }
            finally { Object.DestroyImmediate(go); }
        }

        public static void BuildPreview(PawnRushCourse course, int seed)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료한 뒤 미리보기를 바꾸세요.");
            var plan = course.Plan(seed);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Compose Pawn Rush sections");
            var root = new GameObject("Generated Sections"); root.SetActive(false); root.transform.SetParent(course.transform, false);
            var instances = new PawnRushSection[6];
            var pose = new Pose(course.courseEntrance.position, course.courseEntrance.rotation);
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(plan[i].gameObject, root.transform);
                    instances[i] = go.GetComponent<PawnRushSection>();
                    pose = course.Place(instances[i], pose, i);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instances[i].transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                }
            }
            catch { Object.DestroyImmediate(root); throw; }
            Undo.RegisterCreatedObjectUndo(root, "New section preview");
            Undo.RegisterCompleteObjectUndo(course, "Record section preview");
            if (course.finishPlatform != null) Undo.RegisterCompleteObjectUndo(course.finishPlatform, "Move finish");
            if (course.GeneratedRoot != null) Undo.DestroyObjectImmediate(course.GeneratedRoot.gameObject);
            course.SetGenerated(root.transform, instances, seed); root.SetActive(true);
            EditorUtility.SetDirty(course); EditorSceneManager.MarkSceneDirty(course.gameObject.scene);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
        }
        public static void SetPlaytestStart(Vector3 position)
        {
            var game = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LabGame>(true)).Single();
            if (game.players.Length != 1) throw new InvalidOperationException("기존 1인 LabGame 설정을 확인하세요.");
            Undo.RecordObject(game, "Set playtest start"); game.players[0].spawn = position; EditorUtility.SetDirty(game);
            var marker = game.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Core Playtest Spawn");
            if (marker != null) { Undo.RecordObject(marker, "Move playtest marker"); marker.position = position; }
        }
        public static void Validate(PawnRushCourse course)
        {
            course.Plan(course.seed);
            var sections = course.Sections;
            if (sections.Length != 6) throw new InvalidOperationException("배치된 구역은 6개여야 합니다.");
            var pose = new Pose(course.courseEntrance.position, course.courseEntrance.rotation);
            for (int i = 0; i < sections.Length; i++)
            {
                var section = sections[i]; section.ValidateDefinition();
                if (section.kind != (i % 2 == 0 ? SectionKind.Obstacle : SectionKind.Mission)) throw new InvalidOperationException("슬롯 유형 불일치");
                if (Vector3.Distance(pose.position, section.entrance.position) > .001f || Quaternion.Angle(pose.rotation, section.entrance.rotation) > .01f)
                    throw new InvalidOperationException(PawnRushCourse.SlotName(i) + ": 연결 틈/회전 불일치");
                foreach (var t in section.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new InvalidOperationException("Missing script: " + t.name);
                var mission = section.GetComponent<TeamMission>();
                if (mission != null)
                {
                    var gates = section.GetComponentsInChildren<MissionTeamGate>(true);
                    if (gates.Length != 2 || gates.Select(g => g.allowedTeam).Distinct().Count() != 2 || gates.Any(g => g.mission != mission || g.barrier == null || g.barrier.isTrigger || g.doorVisual == null))
                        throw new InvalidOperationException(section.name + ": 팀별 문 연결이 잘못됐습니다.");
                    foreach (var objective in section.GetComponentsInChildren<MissionObjective>(true))
                        if (objective.mission != mission || !mission.objectiveIds.Contains(objective.objectiveId)) throw new InvalidOperationException("미션 목표 참조 불일치");
                }
                pose = new Pose(section.exit.position, section.exit.rotation);
            }
            if (course.finishPlatform == null || Vector3.Distance(course.finishPlatform.position, pose.position) > .001f) throw new InvalidOperationException("도착 발판 연결 오류");
            Debug.Log("Pawn Rush: A1~C2 유형·참조·입출구 연결 검사 통과.", course);
        }
    }
}
