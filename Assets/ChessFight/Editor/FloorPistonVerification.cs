using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ChessFight.ProtectKing.Editor
{
    [InitializeOnLoad]
    public static class FloorPistonVerification
    {
        const string Pending = "ChessFight.FloorPistonTest";
        static IEnumerator routine;
        static double deadline;
        static float resumeAt;
        static readonly List<string> results = new List<string>();
        static readonly Vector3 origin = new Vector3(300, 0, 100);
        static StageMap map;
        static ProtectTheKingMatchController match;
        static FloorPiston piston;
        static PlayerMotor[] players;
        static Camera camera;
        static FloorPistonVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                {
                    SessionState.SetBool(Pending, false); results.Clear(); routine = Verify(); resumeAt = 0;
                    deadline = EditorApplication.timeSinceStartup + 90; EditorApplication.update -= Pump; EditorApplication.update += Pump;
                }
                else if (state == PlayModeStateChange.ExitingPlayMode && routine != null) Finish("Interrupted");
            };
        }
        [MenuItem("CHESS FIGHT/Obstacles/Verify Floor Piston (Isolated Play)")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FloorPistonPrefabBuilder.Path) == null) throw new Exception("Create floor piston prefab first.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        static void Pump()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out");
                if (Time.time < resumeAt) return;
                if (routine.MoveNext()) { resumeAt = Time.time + (routine.Current is float delay ? delay : 0); return; }
                Finish(null);
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        static void Finish(string error)
        {
            EditorApplication.update -= Pump; routine = null; Directory.CreateDirectory("Artifacts/ChessFight");
            File.WriteAllText("Artifacts/ChessFight/FloorPistonVerification.txt", DateTime.Now.ToString("s") + "\n" +
                (error == null ? "PASS" : "FAIL") + "\n" + string.Join("\n", results) + "\n" + error +
                "\nTemporary runtime scene only; no saved map placement or two-PC Steam test.");
            if (error != null) Debug.LogError(error); else Debug.Log("[CHESS FIGHT] Floor piston verification passed.");
            EditorApplication.isPlaying = false;
        }
        static void Check(bool condition, string name)
        { if (!condition) throw new Exception("FAIL: " + name); results.Add("PASS: " + name); }
        static void At(float time)
        { match.HasAuthority = false; match.ApplyRemote(MatchPhase.Running, time, 300 - time, 0); match.HasAuthority = true; }
        static void Park()
        { for (int i = 0; i < players.Length; i++) players[i].Teleport(origin + new Vector3(6, .05f, i * 2)); }
        static IEnumerator Verify()
        {
            yield return .2f;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Floor piston verification (temporary)"));
            map = new GameObject("Test rules").AddComponent<StageMap>(); match = map.gameObject.AddComponent<ProtectTheKingMatchController>();
            map.match = match; match.map = map; map.outerLimit = 1000; map.killHeight = -20; map.checkpoints = Array.Empty<CheckpointGate>();
            match.throne = new GameObject("Test throne").AddComponent<ThroneInteraction>(); match.throne.match = match;
            map.players = new PlayerIdentity[3]; map.startPoints = new Transform[3]; players = new PlayerMotor[3];
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChessFight/Prefabs/Player_Graybox.prefab");
            for (int i = 0; i < 3; i++)
            {
                map.startPoints[i] = new GameObject("Recovery " + i).transform; map.startPoints[i].position = origin + new Vector3(6, .05f, i * 2);
                players[i] = Object.Instantiate(playerPrefab, map.startPoints[i].position, Quaternion.identity).GetComponent<PlayerMotor>();
                players[i].map = map; players[i].ExternalControl = true; players[i].IsLocal = false;
                map.players[i] = players[i].Identity; map.players[i].playerId = i;
            }
            map.players[1].team = TeamId.Red;
            // Ground around a genuine hole, with no overlapping floor beneath the piston cap.
            for (int side = -1; side <= 1; side += 2)
            {
                Floor(new Vector3(side * 7.3f, -.5f, 0), new Vector3(10, 1, 25));
                Floor(new Vector3(0, -.5f, side * 7.3f), new Vector3(4.6f, 1, 10));
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FloorPistonPrefabBuilder.Path);
            piston = Object.Instantiate(prefab, origin, Quaternion.identity).GetComponent<FloorPiston>();
            Check(piston.context.match == match && piston.Piston.isKinematic && piston.HitArea.isTrigger && !piston.HitArea.enabled,
                "Piston, shared scene context and separate query-only HitArea are wired");
            bool valid = true;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) valid &= GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true)) valid &= renderer.sharedMaterial != null;
            Check(valid, "Prefab has no missing scripts or materials");
            Check(piston.Evaluate(.2f).state == FloorPistonState.Idle && piston.Evaluate(3.6f).state == FloorPistonState.Warning &&
                piston.Evaluate(4.4f).state == FloorPistonState.Rising && piston.Evaluate(4.7f).state == FloorPistonState.Hold &&
                piston.Evaluate(5.3f).state == FloorPistonState.Falling && piston.Evaluate(6.2f).state == FloorPistonState.Idle,
                "Default Idle-Warning-Rising-Hold-Falling-Idle sequence");
            piston.IdleDuration = .3f; piston.WarningDuration = .4f; piston.RiseDuration = .3f; piston.HoldDuration = 1; piston.FallDuration = .5f; piston.StartDelay = .1f;
            match.StartMatch(); foreach (var p in map.players) p.checkpoint = 5; match.enabled = false;
            players[0].Teleport(origin + Vector3.up * .04f); players[1].Teleport(origin + new Vector3(1.4f, .04f, 0));
            players[2].Teleport(origin + new Vector3(2.2f, .04f, 1)); yield return .15f;
            Check(players[0].Grounded && piston.Height == 0 && piston.LaunchesThisCycle == 0, "Retracted cap is walkable and does not launch during Idle");
            Capture("FloorPiston_Idle");
            int warnings = 0; piston.OnWarning.AddListener(() => warnings++);
            match.enabled = true; yield return .5f;
            Check(piston.State == FloorPistonState.Warning && piston.WarningArea.activeSelf && warnings == 1 && piston.LaunchesThisCycle == 0,
                "Warning visual and event precede motion without launching");
            Capture("FloorPiston_Warning"); yield return .55f;
            Check(piston.LaunchesThisCycle == 2 && players[0].transform.position.y > 1.8f && players[1].transform.position.y > 1.8f,
                "Rising launches both Blue and Red players standing on the top");
            Check(Mathf.Abs(players[0].transform.position.x - origin.x) < .12f && players[1].transform.position.x > origin.x + 1.65f,
                "Center launch is vertical while off-center player receives outward velocity");
            Check(players[2].transform.position.y < .3f, "Player alongside the piston is not launched");
            yield return .2f; match.enabled = false;
            Check(piston.State == FloorPistonState.Hold && Mathf.Abs(piston.Height - 2.5f) < .01f && !piston.WarningArea.activeSelf, "Raised cap holds at configured height with warning hidden");
            Capture("FloorPiston_Hold");
            Park(); players[0].Teleport(origin + Vector3.up * 2.54f); players[2].Teleport(origin + new Vector3(2.2f, .04f, 0)); yield return .35f;
            Check(piston.LaunchesThisCycle == 2 && Mathf.Abs(players[0].transform.position.y - 2.5f) < .15f && players[2].transform.position.y < .3f,
                "Hold top landing and side contact do not repeat launch");
            At(2.3f); yield return .15f;
            Check(piston.State == FloorPistonState.Falling && piston.LaunchesThisCycle == 2, "Falling only retracts and never adds launches");
            Park(); At(2.65f); yield return .15f;
            Check(piston.State == FloorPistonState.Idle && piston.Height == 0 && piston.LaunchesThisCycle == 0, "Next cycle resets launch eligibility and returns flush to floor");
            players[0].Teleport(origin + Vector3.up * .04f); yield return .1f;
            At(3.42f); yield return .12f;
            Check(piston.LaunchesThisCycle == 1 && players[0].transform.position.y > .8f, "A previously launched player can launch again next cycle");
            players[0].Teleport(origin + Vector3.up * (piston.Height + .04f)); At(3.48f); yield return .1f;
            Check(piston.LaunchesThisCycle == 1, "Re-entering during the same rise cannot launch twice");
            Park(); At(0); yield return .1f;
            players[0].Teleport(origin + Vector3.up * .04f); match.HasAuthority = false;
            match.ApplyRemote(MatchPhase.Running, .84f, 299, 0); yield return .1f;
            Check(piston.State == FloorPistonState.Rising && piston.LaunchesThisCycle == 0, "Client shows rising pose without applying duplicate authoritative launch");
            match.HasAuthority = true; Park(); piston.Repeat = false; At(20); yield return .1f;
            Check(piston.State == FloorPistonState.Idle && piston.Height == 0, "Repeat false finishes one cycle then remains Idle");
            piston.enabled = false; At(.9f); yield return .1f;
            Check(piston.Height == 0 && !piston.WarningArea.activeSelf, "Disabled piston stops movement and warning");
            piston.enabled = true; match.ReturnToReady(); yield return .1f;
            Check(piston.State == FloorPistonState.Idle && piston.Height == 0, "Ready reset restores retracted pose");
        }
        static void Floor(Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Test ground";
            go.transform.position = origin + position; go.transform.localScale = size;
        }
        static void Capture(string name)
        {
            if (camera == null)
            {
                camera = new GameObject("Preview camera").AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = 4.6f; camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.12f, .15f, .2f); camera.farClipPlane = 100;
                camera.transform.position = origin + new Vector3(8, 8, -11); camera.transform.LookAt(origin + Vector3.up * .3f);
                var light = new GameObject("Preview light").AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = 1.5f; light.cullingMask = 1 << 30; light.transform.rotation = Quaternion.Euler(45, -35, 0);
            }
            var transforms = piston.GetComponentsInChildren<Transform>(true); foreach (var t in transforms) t.gameObject.layer = 30;
            var rt = RenderTexture.GetTemporary(800, 600, 24); var old = RenderTexture.active;
            var texture = new Texture2D(800, 600, TextureFormat.RGB24, false);
            try
            {
                camera.aspect = 4f / 3; RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 800, 600), 0, 0); texture.Apply();
                Directory.CreateDirectory("Artifacts/ChessFight/ObstaclePreviews");
                File.WriteAllBytes("Artifacts/ChessFight/ObstaclePreviews/" + name + ".png", texture.EncodeToPNG());
            }
            finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(texture); foreach (var t in transforms) t.gameObject.layer = 0; }
        }
    }
}
