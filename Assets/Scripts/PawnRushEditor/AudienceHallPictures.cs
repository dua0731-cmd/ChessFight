using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush.Editor
{
    // Pictures of the audience halls A to D (JY-audience-hall), for a project copy in batch mode:
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.AudienceHallPictures.Run
    // (no -quit, no -nographics). For each hall: a new scene with the prefab built as a round builds it, a sun, and still
    // chess pieces where the throne contest stands each team (its own sums: IThroneStage.StandRow, the floor under each
    // spot). The pieces are pictures, not players. From above the roof and the dome are left out so the inside shows.
    // A few numbers the pictures cannot show: the platform as the finish reads it, the walking slopes, the largest rise
    // along the ways a pawn walks, where the rows stand. Pictures and notes: Logs/AudienceHall.
    public static class AudienceHallPictures
    {
        const string Folder = "Assets/Prefabs/PawnRushSections/";
        static readonly PieceKind[] Row = { PieceKind.Pawn, PieceKind.Knight, PieceKind.Bishop, PieceKind.Rook, PieceKind.Queen, PieceKind.King };
        static readonly float[] Tall = { 1f, 1.1f, 1.15f, 1.1f, 1.2f, 1.25f };
        static string Output => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "AudienceHall");
        static readonly StringBuilder notes = new StringBuilder();
        static Camera cam;
        static Transform hallRoot;

        struct Way
        {
            public string Name;
            public Vector3 From, To;
            public float Limit;
            public Way(string name, Vector3 from, Vector3 to, float limit) { Name = name; From = from; To = to; Limit = limit; }
        }

        struct View
        {
            public string Name;
            public Vector3 Eye, Target;
            public float Field;
            public bool Plan, Cutaway;
            public int Width, Height;
            public static View Look(string name, Vector3 eye, Vector3 target, float fov, bool cutaway = false) =>
                new View { Name = name, Eye = eye, Target = target, Field = fov, Cutaway = cutaway, Width = 1600, Height = 900 };
            public static View Above(string name, Vector3 middle, float halfHeight, int width, int height) =>
                new View { Name = name, Eye = middle + Vector3.up * 80f, Target = middle, Field = halfHeight, Plan = true, Cutaway = true, Width = width, Height = height };
        }

        struct Hall
        {
            public string Letter, Prefab;
            public Way[] Ways;
            public View[] Views;
        }

        static readonly Hall[] Halls =
        {
            new Hall { Letter = "A", Prefab = "Final_AudienceHall", Ways = new[]
                {
                    new Way("정문 → 발판 (x 0)", new Vector3(0f, 0f, .5f), new Vector3(0f, 0f, 15f), .26f),
                    new Way("서쪽 바닥 → 발판 (z 15)", new Vector3(-11f, 0f, 15f), new Vector3(-3f, 0f, 15f), .26f),
                    new Way("동쪽 바닥 → 발판 (z 15)", new Vector3(11f, 0f, 15f), new Vector3(3f, 0f, 15f), .26f),
                }, Views = new[]
                {
                    View.Look("01_overview", new Vector3(-17f, 17f, -9f), new Vector3(0f, 1f, 12.5f), 58f),
                    View.Above("02_plan", new Vector3(0f, 0f, 11.25f), 12.6f, 1300, 1200),
                    View.Look("03_from_the_door", new Vector3(0f, 1.7f, -1.5f), new Vector3(0f, 1.9f, 15f), 72f),
                    View.Look("04_behind_white_row", new Vector3(-11.4f, 2.4f, 15f), new Vector3(0f, 1.4f, 15f), 72f),
                    View.Look("05_platform", new Vector3(2.5f, 6.2f, 6.2f), new Vector3(0f, 1.3f, 15.2f), 55f),
                } },
            new Hall { Letter = "B", Prefab = "Final_AudienceHall_B_Cathedral", Ways = new[]
                {
                    new Way("정문 → 발판 (x 0, 큰 계단)", new Vector3(0f, 0f, .5f), new Vector3(0f, 0f, 55.5f), .26f),
                    new Way("서쪽 회랑 → 발판 (z 55, 팀 계단)", new Vector3(-14f, 0f, 55f), new Vector3(-3f, 0f, 55f), .26f),
                    new Way("동쪽 회랑 → 발판 (z 55, 팀 계단)", new Vector3(14f, 0f, 55f), new Vector3(3f, 0f, 55f), .26f),
                }, Views = new[]
                {
                    View.Look("01_overview", new Vector3(-30f, 42f, -12f), new Vector3(0f, 2f, 34f), 55f, true),
                    View.Above("02_plan", new Vector3(0f, 0f, 32f), 33f, 800, 1600),
                    View.Look("03_from_the_door", new Vector3(0f, 1.7f, -1.5f), new Vector3(0f, 6f, 55f), 70f),
                    View.Look("04_behind_white_row", new Vector3(-14.2f, 2.4f, 48.5f), new Vector3(0f, 2.5f, 54f), 72f),
                    View.Look("05_platform", new Vector3(0f, 9f, 43f), new Vector3(0f, 2.5f, 56f), 60f),
                    View.Look("06_up_the_grand_stair", new Vector3(0f, 1.4f, 40.5f), new Vector3(0f, 3.5f, 56f), 70f),
                } },
            new Hall { Letter = "C", Prefab = "Final_AudienceHall_C_Rotunda", Ways = new[]
                {
                    new Way("정문 → 섬 (x 0, 남쪽 다리)", new Vector3(0f, 0f, .5f), new Vector3(0f, 0f, 22f), .26f),
                    new Way("서쪽 바닥 → 섬 (z 22, 서쪽 다리)", new Vector3(-13f, 0f, 22f), new Vector3(-2f, 0f, 22f), .26f),
                    new Way("동쪽 바닥 → 섬 (z 22, 동쪽 다리)", new Vector3(13f, 0f, 22f), new Vector3(2f, 0f, 22f), .26f),
                }, Views = new[]
                {
                    View.Look("01_overview", new Vector3(-30f, 36f, -10f), new Vector3(0f, 0f, 22f), 55f, true),
                    View.Above("02_plan", new Vector3(0f, 0f, 22f), 24f, 1500, 1200),
                    View.Look("03_from_the_door", new Vector3(0f, 1.7f, -1.5f), new Vector3(0f, 5f, 22f), 75f),
                    View.Look("04_behind_white_row", new Vector3(-15.5f, 2.4f, 22f), new Vector3(0f, 1f, 22f), 72f),
                    View.Look("05_platform", new Vector3(7f, 7f, 12.5f), new Vector3(0f, .6f, 22f), 55f),
                    View.Look("06_dome_up", new Vector3(0f, 1.7f, 9f), new Vector3(0f, 30f, 22f), 80f),
                } },
            new Hall { Letter = "D", Prefab = "Final_AudienceHall_D_GiantThrone", Ways = new[]
                {
                    new Way("정문 → 발받침 (x 0, 책 계단 · 뛰어 오름)", new Vector3(0f, 0f, .5f), new Vector3(0f, 0f, 28f), .55f),
                    new Way("서쪽 띠 경사 → 팔걸이 (x -5.75)", new Vector3(-5.75f, 0f, 13f), new Vector3(-5.75f, 0f, 33f), .26f),
                    new Way("동쪽 띠 경사 → 팔걸이 (x 5.75)", new Vector3(5.75f, 0f, 13f), new Vector3(5.75f, 0f, 33f), .26f),
                }, Views = new[]
                {
                    View.Look("01_overview", new Vector3(-30f, 38f, -10f), new Vector3(0f, 3f, 28f), 55f, true),
                    View.Above("02_plan", new Vector3(0f, 0f, 22f), 23f, 1000, 1300),
                    View.Look("03_from_the_door", new Vector3(0f, 1.7f, -1.5f), new Vector3(0f, 7f, 33f), 72f),
                    View.Look("04_behind_white_row", new Vector3(-15.5f, 2.4f, 29f), new Vector3(0f, 3f, 30f), 72f),
                    View.Look("05_platform", new Vector3(8f, 9f, 22f), new Vector3(0f, 3f, 32f), 55f),
                    View.Look("06_way_up", new Vector3(-3f, 1.2f, 16.5f), new Vector3(0f, 2.5f, 28f), 70f),
                } },
        };

        [MenuItem("ChessFight/Pawn Rush/Validate/Audience hall pictures", false, 213)]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            notes.Clear();
            bool ok = true;
            foreach (var hall in Halls)
            {
                try { ok &= Take(hall); }
                catch (System.Exception e) { ok = false; Note("FAIL " + hall.Letter + ": " + e); }
            }
            Note(ok ? "RESULT PASS" : "RESULT FAIL");
            File.WriteAllText(Path.Combine(Output, "notes.txt"), notes.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Take(Hall hall)
        {
            Note("== " + hall.Letter + " " + hall.Prefab);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + hall.Prefab + ".prefab");
            if (asset == null) { Note("FAIL: no prefab"); return false; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            hallRoot = go.transform;
            var stage = go.GetComponent<IThroneStage>();
            if (stage == null) { Note("FAIL: no IThroneStage on the prefab"); return false; }
            // A round's section builds itself when it wakes; Awake does not run in the Editor, so call it.
            var builder = (MonoBehaviour)stage;
            builder.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(builder, null);
            Physics.SyncTransforms();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, .95f, .86f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.55f, .57f, .63f);
            RenderSettings.ambientEquatorColor = new Color(.42f, .4f, .37f);
            RenderSettings.ambientGroundColor = new Color(.22f, .2f, .18f);

            bool ok = Measure(hall, stage);
            Stand(stage);
            Physics.SyncTransforms();

            cam = new GameObject("Capture camera").AddComponent<Camera>();
            cam.enabled = false;
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.5f, .57f, .67f);
            foreach (var view in hall.Views) Capture(hall.Letter + "_" + view.Name, view);
            Summons(hall, stage);
            Object.DestroyImmediate(cam.gameObject);
            return ok;
        }

        // The kings' summons on a hall with a board (ThroneSummonsShow.Preview): the rows' stand-ins taken away, the show
        // stopped at three moments - both kings struck (aurora, pillars, the board's light), the pieces coming in, the
        // rays to the crown - with stand-ins on the squares whose pieces are in by then (rook, knight, bishop, queen and
        // two pawns a team, the previz's six). Each from behind white's side, looking across the board.
        static readonly PieceKind[] Six = { PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop, PieceKind.Queen, PieceKind.Pawn, PieceKind.Pawn };

        static void Summons(Hall hall, IThroneStage stage)
        {
            var board = stage.Board;
            var section = hallRoot.GetComponent<PawnRushSection>();
            if (!board.Exists || section == null) { Note("summons: no board"); return; }
            var rows = GameObject.Find("Stand-in pieces (pictures only)");
            if (rows != null) Object.DestroyImmediate(rows);
            var show = ThroneSummonsShow.Ready(section, board);
            float s = board.Square;
            Vector3 m = board.Middle;
            Note($"summons: board a1 {board.A1}, {s:0.##} m squares; white king e1 {board.King(Teams.White)}, black king e8 {board.King(Teams.Black)}");
            foreach (var (name, t) in new[] { ("S1_kings_strike", 1.7f), ("S2_coming_in", 3.25f), ("S3_rays_to_crown", 5.2f) })
            {
                var standIns = new GameObject("Summons stand-ins (pictures only)").transform;
                int arrived = 0;
                foreach (var p in show.Preview(t, Six, Six, cam))
                {
                    if (!p.arrived) continue;
                    arrived++;
                    var piece = MiniGameParts.Piece(standIns, Teams.Name(p.team) + " " + p.kind, p.kind, Tall[System.Array.IndexOf(Row, p.kind)],
                                                    MiniGameParts.PieceMaterial(p.team), Arena.TeamColor(p.team, true), false);
                    piece.transform.position = p.spot;
                    piece.transform.rotation = Quaternion.LookRotation(hallRoot.TransformDirection(board.Facing(p.team)));
                }
                Vector3 eye = m - board.Rank * Mathf.Min(5f * s, 12.5f) - board.File * Mathf.Min(3.5f * s, 8f) + Vector3.up * Mathf.Min(2.2f * s + 1f, 6.5f);   // inside the rotunda too
                var view = name == "S3_rays_to_crown"
                    ? View.Look(name, eye, hallRoot.InverseTransformPoint(show.CrownAt) * .5f + m * .5f, 64f)
                    : View.Look(name, eye, m + board.Rank * s + Vector3.up * .6f, 62f);
                Note($"summons {name}: {t:0.00} s, {arrived} of 12 in");
                Capture(hall.Letter + "_" + name, view);
                Object.DestroyImmediate(standIns.gameObject);
            }
            show.Stop();
        }

        static bool Measure(Hall hall, IThroneStage stage)
        {
            bool ok = true;
            var zone = hallRoot.GetComponentInChildren<FinishZone>(true);
            ok &= Check(zone != null, "the hall has a finish (FinishZone)");
            if (zone != null && CrownFinish.Platform(zone, out var middle, out var radius))
                Note($"the platform as the finish reads it: middle {hallRoot.InverseTransformPoint(middle)}, radius {radius:0.00} m");
            foreach (var t in hallRoot.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("walking slope") || t.name.StartsWith("Sash") || t.name.StartsWith("Bridge"))
                {
                    float angle = Vector3.Angle(t.up, Vector3.up);
                    ok &= Check(angle <= 19.5f, $"{t.name}: {angle:0.0} degrees");
                }
            foreach (var way in hall.Ways)
            {
                float worst = Rise(way.From, way.To);
                ok &= Check(worst <= way.Limit, $"{way.Name}: largest rise {worst:0.00} m (limit {way.Limit:0.00})");
            }
            int teamCheckpoints = 0;
            foreach (var c in hallRoot.GetComponentsInChildren<CourseCheckpoint>(true))
                if (c.Team == Teams.White || c.Team == Teams.Black) teamCheckpoints++;
            if (teamCheckpoints > 0) Note("team checkpoints (a team's falls come back there): " + teamCheckpoints);
            return ok;
        }

        static float Rise(Vector3 from, Vector3 to)
        {
            float worst = 0f;
            int n = Mathf.CeilToInt(Vector3.Distance(from, to) / .25f);
            float last = Ground(from);
            for (int i = 1; i <= n; i++)
            {
                float y = Ground(Vector3.Lerp(from, to, i / (float)n));
                worst = Mathf.Max(worst, y - last);
                last = y;
            }
            return worst;
        }

        static float Ground(Vector3 local)
        {
            var at = hallRoot.TransformPoint(local);
            float best = float.MaxValue, y = -99f;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 12f, Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.distance < best) { best = hit.distance; y = hit.point.y; }
            return y;
        }

        // Each team's row where ThroneContest.Place stands it (the ending's StandRow, Along 1.2 m apart along the way in),
        // on the floor ThroneContest.Ground finds there.
        static void Stand(IThroneStage stage)
        {
            var zone = hallRoot.GetComponentInChildren<FinishZone>(true);
            if (zone == null || !CrownFinish.Platform(zone, out var middle, out _)) return;
            var root = new GameObject("Stand-in pieces (pictures only)").transform;
            for (int team = 0; team < 2; team++)
            {
                var body = MiniGameParts.PieceMaterial(team);
                var row = hallRoot.TransformPoint(stage.StandRow(team));
                for (int i = 0; i < 6; i++)
                {
                    var at = row + hallRoot.forward * ((i - 2.5f) * 1.2f);
                    string under = "nothing";
                    float best = float.MaxValue, y = at.y;
                    foreach (var hit in Physics.RaycastAll(at + Vector3.up * 2.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
                        if (hit.distance < best) { best = hit.distance; y = hit.point.y; under = hit.collider.name; }
                    at.y = y;
                    if (i == 0 || i == 5) Note($"{Teams.Name(team)}팀 row end {i + 1}: {hallRoot.InverseTransformPoint(at)} on {under}");
                    var piece = MiniGameParts.Piece(root, Teams.Name(team) + " " + Row[i], Row[i], Tall[i], body, Arena.TeamColor(team, true), false);
                    piece.transform.position = at;
                    var look = middle - at;
                    look.y = 0f;
                    if (look.sqrMagnitude > .01f) piece.transform.rotation = Quaternion.LookRotation(look);
                }
            }
        }

        static void Cutaway(bool on)
        {
            foreach (var r in hallRoot.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Roof") || r.name.StartsWith("Dome")) r.enabled = !on;
        }

        static void Capture(string name, View view)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Note("NOT RUN: picture " + name + " (no graphics device)"); return; }
            Cutaway(view.Cutaway);
            var eye = hallRoot.TransformPoint(view.Eye);
            var target = hallRoot.TransformPoint(view.Target);
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation(target - eye, view.Plan ? hallRoot.forward : Vector3.up);
            cam.orthographic = view.Plan;
            if (view.Plan) cam.orthographicSize = view.Field;
            else cam.fieldOfView = view.Field;
            cam.aspect = (float)view.Width / view.Height;
            var texture = new RenderTexture(view.Width, view.Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var before = RenderTexture.active;
            Texture2D picture = null;
            try
            {
                texture.Create();
                var request = new RenderPipeline.StandardRequest { destination = texture };
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    if (!RenderPipeline.SupportsRenderRequest(cam, request)) { Note("NOT RUN: picture " + name + " (render request unsupported)"); return; }
                    RenderPipeline.SubmitRenderRequest(cam, request);
                }
                else { cam.targetTexture = texture; cam.Render(); cam.targetTexture = null; }
                RenderTexture.active = texture;
                picture = new Texture2D(view.Width, view.Height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, view.Width, view.Height), 0, 0);
                picture.Apply();
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), picture.EncodeToPNG());
                Note("picture " + name + ".png");
            }
            finally
            {
                Cutaway(false);
                RenderTexture.active = before;
                if (picture != null) Object.DestroyImmediate(picture);
                texture.Release();
                Object.DestroyImmediate(texture);
            }
        }

        static bool Check(bool ok, string what)
        {
            Note((ok ? "PASS " : "FAIL ") + what);
            return ok;
        }

        static void Note(string line)
        {
            notes.AppendLine(line);
            Debug.Log("[AudienceHall] " + line);
        }
    }
}
