using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush.Editor
{
    // R733: pictures of the royal water garden's and the grand stair hall's checkpoint triggers, each drawn as a see-through
    // green box with its number over it, from above and along the way - in Play Mode in the course prefab test scene,
    // as the sections are built there. A look at where a checkpoint is caught, not a check of it.
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.CheckpointBoxCapture.RunPlayBatch
    // (no -quit, no -nographics). Pictures and a list of the boxes: Logs/CheckpointBoxes.
    [InitializeOnLoad]
    public static class CheckpointBoxCapture
    {
        const string ActiveKey = "CheckpointBoxes.Active", ExitKey = "CheckpointBoxes.Exit", CodeKey = "CheckpointBoxes.Code";
        const double Settle = 6;

        static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "CheckpointBoxes");

        static int stage, failures;
        static double stepAt, requested, idleAt;
        static PawnRushSection section;
        static Camera captureCamera;

        static CheckpointBoxCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Checkpoint boxes - garden and grand stair (Play, pictures)", false, 213)]
        public static void RunPlayBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "report.txt"), "Checkpoint triggers of the royal water garden and the grand stair hall (R733), in Play Mode in the course prefab test scene.\n");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CoursePrefabMenu.TestScene) == null) CoursePrefabMenu.BuildScene();
            EditorSceneManager.OpenScene(CoursePrefabMenu.TestScene, OpenSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 1);
            requested = idleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.update += BeginWhenIdle;
        }

        static void BeginWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - requested > 180) { EditorApplication.update -= BeginWhenIdle; Log("FAIL: the Editor did not settle"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { idleAt = now; return; }
            if (now - idleAt < 2) return;
            EditorApplication.update -= BeginWhenIdle;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                stage = 0; failures = 0; section = null;
                stepAt = EditorApplication.timeSinceStartup;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - stepAt < Settle) return;
            stepAt = now;
            try
            {
                switch (stage++)
                {
                    case 0: if (!Open(typeof(RoyalWaterGarden))) Finish(); break;
                    case 1: Garden(); break;
                    case 2: if (!Open(typeof(GrandStairHall))) Finish(); break;
                    case 3: Hall(); Finish(); break;
                }
            }
            catch (System.Exception e)
            {
                failures++;
                Log("FAIL: the capture threw " + e);
                Finish();
            }
        }

        static bool Open(System.Type kind)
        {
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            if (test == null) { failures++; Log("FAIL: no course prefab test in the scene"); return false; }
            var entries = typeof(CoursePrefabTest).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(test) as System.Collections.IList;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                var prefab = entries[i].GetType().GetField("prefab")?.GetValue(entries[i]) as GameObject;
                if (prefab != null && prefab.GetComponent(kind) != null) { test.Select(i); Log("opened " + prefab.name + " (key " + (i + 1) + ")"); return true; }
            }
            failures++;
            Log("FAIL: " + kind.Name + " is not in the course prefab test's list");
            return false;
        }

        static void Garden()
        {
            if (!Boxes()) return;
            Begin();
            Capture("garden_00_plan", new Vector3(0f, 90f, 2.5f), new Vector3(0f, 0f, 2.5f), Vector3.forward, 70f, true, 1200, 1600, 80f);
            Capture("garden_01_west_landing", new Vector3(-12f, 16f, -27f), new Vector3(-29f, 1f, 0f), Vector3.up, 60f, false, 1600, 900);
            Capture("garden_02_east_landing_lock_dyke", new Vector3(12f, 20f, -6f), new Vector3(29f, 3f, 17f), Vector3.up, 62f, false, 1600, 900);
            Capture("garden_03_quay_and_exit", new Vector3(0f, 24f, 12f), new Vector3(0f, 3f, 46f), Vector3.up, 62f, false, 1600, 900);
            Capture("garden_04_start_and_canal", new Vector3(14f, 20f, -66f), new Vector3(-8f, 0f, -30f), Vector3.up, 62f, false, 1600, 900);
            End();
        }

        static void Hall()
        {
            if (!Boxes()) return;
            Begin();
            Capture("hall_00_plan", new Vector3(0f, 90f, 70f), new Vector3(0f, 0f, 70f), Vector3.forward, 84f, true, 900, 1600, 68f);
            Capture("hall_01_landing", new Vector3(0f, 18f, 30f), new Vector3(0f, 8f, 58f), Vector3.up, 62f, false, 1600, 900);
            Capture("hall_02_curving_stair_cp2", new Vector3(8f, 22f, 78f), new Vector3(25f, 10f, 96f), Vector3.up, 60f, false, 1600, 900);
            Capture("hall_03_dais_cp6", new Vector3(20f, 27f, 74f), new Vector3(2f, 16f, 92f), Vector3.up, 60f, false, 1600, 900);
            Capture("hall_04_spiral_from_above", new Vector3(0f, 60f, 92f), new Vector3(0f, 0f, 92f), Vector3.forward, 34f, true, 1200, 1200, 39f);
            End();
        }

        // Each checkpoint's trigger as a see-through box, its number over it; the list into the report.
        static bool Boxes()
        {
            var test = Object.FindFirstObjectByType<CoursePrefabTest>();
            section = test != null ? test.Current : null;
            if (section == null) { failures++; Log("FAIL: nothing built"); return false; }
            Log("section " + section.Id);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var fill = new Material(shader);
            fill.SetFloat("_Surface", 1f);
            fill.SetFloat("_Blend", 0f);
            fill.SetFloat("_Cull", 0f);
            fill.SetOverrideTag("RenderType", "Transparent");
            fill.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            fill.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            fill.SetInt("_ZWrite", 0);
            fill.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            fill.renderQueue = 3000;
            fill.SetColor("_BaseColor", new Color(.1f, 1f, .45f, .42f));
            var edge = new Material(shader);
            edge.SetColor("_BaseColor", new Color(.05f, .55f, .25f, 1f));
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var cp in section.GetComponentsInChildren<Checkpoint>(true))
            {
                var box = cp.GetComponent<BoxCollider>();
                if (box == null) continue;
                var shape = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var solid = shape.GetComponent<Collider>();
                solid.enabled = false;
                Object.Destroy(solid);
                shape.name = cp.name + " trigger (picture)";
                shape.transform.SetParent(cp.transform, false);
                shape.transform.localPosition = box.center;
                shape.transform.localScale = box.size;
                shape.GetComponent<MeshRenderer>().sharedMaterial = fill;
                Edges(cp.transform, box, edge);
                var label = new GameObject(cp.name + " number (picture)");
                label.transform.SetParent(cp.transform, false);
                label.transform.localPosition = box.center + Vector3.up * (box.size.y * .5f + .3f);
                label.transform.rotation = section.transform.rotation * Quaternion.Euler(90f, 0f, 0f);
                var text = label.AddComponent<TextMesh>();
                text.text = cp.name;
                text.font = font;
                text.fontSize = 64;
                text.characterSize = .12f;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = new Color(1f, .9f, .1f);
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                Log($"{cp.name}: order {cp.Order}, centre {Local(cp.transform.TransformPoint(box.center)):0.0}, size {box.size:0.0}, yaw {(Quaternion.Inverse(section.transform.rotation) * cp.transform.rotation).eulerAngles.y:0}");
            }
            return true;
        }

        // The box's twelve edges as thin bars, so the box reads from above and from the side.
        static void Edges(Transform parent, BoxCollider box, Material material)
        {
            Vector3 h = box.size * .5f, c = box.center;
            const float t = .08f;
            for (int axis = 0; axis < 3; axis++)
                for (int a = -1; a <= 1; a += 2)
                    for (int b = -1; b <= 1; b += 2)
                    {
                        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        var solid = bar.GetComponent<Collider>();
                        solid.enabled = false;
                        Object.Destroy(solid);
                        bar.transform.SetParent(parent, false);
                        Vector3 at = c, size = Vector3.one * t;
                        int u = (axis + 1) % 3, v = (axis + 2) % 3;
                        at[u] += a * h[u];
                        at[v] += b * h[v];
                        size[axis] = box.size[axis];
                        bar.transform.localPosition = at;
                        bar.transform.localScale = size;
                        bar.GetComponent<MeshRenderer>().sharedMaterial = material;
                    }
        }

        static Vector3 Local(Vector3 world) => section.transform.InverseTransformPoint(world);
        static Vector3 World(Vector3 local) => section.transform.TransformPoint(local);

        static void Begin()
        {
            var go = new GameObject("Checkpoint capture camera");
            captureCamera = go.AddComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = new Color(.06f, .07f, .11f);
        }

        static void End()
        {
            if (captureCamera != null) Object.Destroy(captureCamera.gameObject);
        }

        // One picture from `eye` toward `target` (the section's frame); orthographic with `size` as its half height when
        // `flat`. `near` cuts what is above the floors away (the hall's ceiling, the garden's towers) from a plan.
        static void Capture(string name, Vector3 eye, Vector3 target, Vector3 up, float size, bool flat, int width, int height, float near = .2f)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: picture " + name + " (no graphics device)"); return; }
            captureCamera.transform.position = World(eye);
            captureCamera.transform.rotation = Quaternion.LookRotation(World(target) - World(eye), section.transform.TransformDirection(up));
            captureCamera.orthographic = flat;
            if (flat) captureCamera.orthographicSize = size;
            else captureCamera.fieldOfView = size;
            captureCamera.nearClipPlane = near;
            captureCamera.farClipPlane = 800f;
            captureCamera.aspect = (float)width / height;
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var before = RenderTexture.active;
            Texture2D picture = null;
            try
            {
                texture.Create();
                var request = new RenderPipeline.StandardRequest { destination = texture };
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    if (!RenderPipeline.SupportsRenderRequest(captureCamera, request)) { Log("NOT RUN: picture " + name + " (render request unsupported)"); return; }
                    RenderPipeline.SubmitRenderRequest(captureCamera, request);
                }
                else { captureCamera.targetTexture = texture; captureCamera.Render(); captureCamera.targetTexture = null; }
                RenderTexture.active = texture;
                picture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                picture.Apply();
                File.WriteAllBytes(Path.Combine(Folder, name + ".png"), picture.EncodeToPNG());
                Log("picture " + name + ".png");
            }
            finally
            {
                RenderTexture.active = before;
                if (picture != null) Object.Destroy(picture);
                texture.Release();
                Object.Destroy(texture);
            }
        }

        static void Finish()
        {
            Log(failures == 0 ? "RESULT PASS" : "RESULT FAIL (" + failures + ")");
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitKey, true);
            SessionState.SetInt(CodeKey, failures == 0 ? 0 : 1);
            EditorApplication.isPlaying = false;
        }

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, "report.txt"), value + "\n");
            Debug.Log("[CheckpointBoxes] " + value);
        }

        static void CompleteExit()
        {
            if (!SessionState.GetBool(ExitKey, false)) return;
            SessionState.SetBool(ExitKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
        }
    }
}
