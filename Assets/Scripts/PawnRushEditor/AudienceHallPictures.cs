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
    // Pictures of the king's audience hall graybox (JY-audience-hall), for a project copy in batch mode:
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.AudienceHallPictures.Run
    // (no -quit, no -nographics). A new scene with the hall prefab built as a round builds it, a sun, and still chess
    // pieces where the throne contest stands each team (its own sums: the platform's middle and radius, the hall's gap,
    // the floor under each spot). The pieces are pictures, not players. Pictures and notes: Logs/AudienceHall.
    public static class AudienceHallPictures
    {
        const string Prefab = "Assets/Prefabs/PawnRushSections/Final_AudienceHall.prefab";
        static readonly PieceKind[] Row = { PieceKind.Pawn, PieceKind.Knight, PieceKind.Bishop, PieceKind.Rook, PieceKind.Queen, PieceKind.King };
        static readonly float[] Tall = { 1f, 1.1f, 1.15f, 1.1f, 1.2f, 1.25f };
        static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "AudienceHall");
        static readonly StringBuilder notes = new StringBuilder();
        static Camera cam;

        [MenuItem("ChessFight/Pawn Rush/Validate/Audience hall pictures", false, 213)]
        public static void Run()
        {
            int code = 1;
            Directory.CreateDirectory(Folder);
            notes.Clear();
            try { code = Take() ? 0 : 1; }
            catch (System.Exception e) { Note("FAIL: " + e); }
            File.WriteAllText(Path.Combine(Folder, "notes.txt"), notes.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static bool Take()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (asset == null) { Note("FAIL: no prefab at " + Prefab); return false; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var hall = go.GetComponent<AudienceHallSection>();
            // A round's section builds itself when it wakes; Awake does not run in the Editor, so call it.
            typeof(AudienceHallSection).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hall, null);
            Physics.SyncTransforms();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.color = new Color(1f, .95f, .86f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f, .64f, .7f);
            RenderSettings.ambientEquatorColor = new Color(.48f, .45f, .42f);
            RenderSettings.ambientGroundColor = new Color(.25f, .23f, .21f);

            bool ok = Measure(hall);
            Stand(hall);
            Physics.SyncTransforms();

            cam = new GameObject("Capture camera").AddComponent<Camera>();
            cam.enabled = false;
            cam.nearClipPlane = .1f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.55f, .62f, .72f);
            var up = Vector3.up;
            Capture("01_overview", new Vector3(-17f, 17f, -9f), new Vector3(0f, 1f, 12.5f), up, 58f, false, 1600, 900);
            Capture("02_plan", new Vector3(0f, 60f, 11.25f), new Vector3(0f, 0f, 11.25f), Vector3.forward, 12.6f, true, 1300, 1200);
            Capture("03_from_the_door", new Vector3(0f, 1.7f, -1.5f), new Vector3(0f, 1.9f, 15f), up, 72f, false, 1600, 900);
            Capture("04_behind_white_row", new Vector3(-11.4f, 2.4f, 15f), new Vector3(0f, 1.4f, 15f), up, 72f, false, 1600, 900);
            Capture("05_dais_close", new Vector3(2.5f, 6.2f, 6.2f), new Vector3(0f, 1.3f, 15.2f), up, 55f, false, 1600, 900);
            Capture("06_behind_the_throne", new Vector3(5.5f, 2.3f, 21.6f), new Vector3(-1.5f, 1f, 16f), up, 72f, false, 1600, 900);
            Capture("07_west_stairs_low", new Vector3(-10.8f, 1.2f, 8.8f), new Vector3(-5f, .8f, 15f), up, 62f, false, 1600, 900);
            Object.DestroyImmediate(cam.gameObject);
            Note(ok ? "RESULT PASS" : "RESULT FAIL");
            return ok;
        }

        // The numbers the pictures cannot show.
        static bool Measure(AudienceHallSection hall)
        {
            bool ok = true;
            var zone = hall.GetComponentInChildren<FinishZone>(true);
            ok &= Check(zone != null, "the hall has a finish (FinishZone)");
            if (zone != null && CrownFinish.Platform(zone, out var middle, out var radius))
                ok &= Check(Mathf.Abs(middle.y - hall.DaisHeight) < .01f && Mathf.Abs(radius - hall.CircleRadius) < .01f,
                            $"the platform as the finish reads it: middle {middle}, radius {radius:0.00} m (dais {hall.DaisHeight:0.00} m)");
            foreach (var t in hall.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("walking slope"))
                {
                    float angle = Vector3.Angle(t.up, Vector3.up);
                    ok &= Check(angle <= 19.5f, $"{t.name}: {angle:0.0} degrees");
                }
            // Walk the floor's height along the carpet to the circle and across the west flight: the largest rise
            // between two samples 0.25 m apart (a pawn steps up 0.25 m by itself).
            ok &= Check(Rise(new Vector3(0f, 0f, .5f), new Vector3(0f, 0f, 15f), out var a) <= .26f, $"door to circle along x = 0: largest rise {a:0.000} m");
            ok &= Check(Rise(new Vector3(-11f, 0f, 15f), new Vector3(-3f, 0f, 15f), out var w) <= .26f, $"west floor to circle along z = 15: largest rise {w:0.000} m");
            ok &= Check(Rise(new Vector3(11f, 0f, 15f), new Vector3(3f, 0f, 15f), out var e) <= .26f, $"east floor to circle along z = 15: largest rise {e:0.000} m");
            float back = Ground(new Vector3(0f, 0f, 18.95f)) - Ground(new Vector3(0f, 0f, 19.3f));
            Note($"the cliff behind the throne: {back:0.00} m");
            return ok;
        }

        static float Rise(Vector3 from, Vector3 to, out float worst)
        {
            worst = 0f;
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

        static float Ground(Vector3 at)
        {
            float best = float.MaxValue, y = -99f;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 10f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.distance < best) { best = hit.distance; y = hit.point.y; }
            return y;
        }

        // Each team where ThroneContest.Place stands it (white on the left of the way in, black on the right), on the
        // floor ThroneContest.Ground finds there.
        static void Stand(AudienceHallSection hall)
        {
            var zone = hall.GetComponentInChildren<FinishZone>(true);
            if (zone == null || !CrownFinish.Platform(zone, out var middle, out var radius)) return;
            var root = new GameObject("Stand-in pieces (pictures only)").transform;
            for (int team = 0; team < 2; team++)
            {
                var body = MiniGameParts.PieceMaterial(team);
                for (int i = 0; i < 6; i++)
                {
                    var at = middle + Vector3.right * ((team == Teams.White ? -1f : 1f) * (radius + hall.StandGap)) + Vector3.forward * ((i - 2.5f) * 1.2f);
                    string under = "nothing";
                    float best = float.MaxValue, y = at.y;
                    foreach (var hit in Physics.RaycastAll(at + Vector3.up * 2.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
                        if (hit.distance < best) { best = hit.distance; y = hit.point.y; under = hit.collider.name; }
                    at.y = y;
                    if (i == 0) Note($"{Teams.Name(team)}팀 row: x {at.x:0.00}, floor y {y:0.00} ({under})");
                    var piece = MiniGameParts.Piece(root, Teams.Name(team) + " " + Row[i], Row[i], Tall[i], body, Arena.TeamColor(team, true), false);
                    piece.transform.position = at;
                    piece.transform.rotation = Quaternion.LookRotation(new Vector3(middle.x - at.x, 0f, 0f));
                }
            }
        }

        static void Capture(string name, Vector3 eye, Vector3 target, Vector3 up, float fieldOfView, bool flat, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Note("NOT RUN: picture " + name + " (no graphics device)"); return; }
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation(target - eye, up);
            cam.orthographic = flat;
            if (flat) cam.orthographicSize = fieldOfView;
            else cam.fieldOfView = fieldOfView;
            cam.aspect = (float)width / height;
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
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
                picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                picture.Apply();
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), picture.EncodeToPNG());
                Note("picture " + name + ".png");
            }
            finally
            {
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
