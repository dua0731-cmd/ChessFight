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
    public static class FoldingBridgeVerification
    {
        const string Pending = "ChessFight.FoldingBridgeTest";
        static IEnumerator routine;
        static double deadline;
        static float resumeAt;
        static readonly List<string> results = new List<string>();
        static readonly Vector3 origin = new Vector3(200, 0, 100);
        static StageMap map;
        static ProtectTheKingMatchController match;
        static FoldingBridge bridge;
        static PlayerMotor[] players;
        static bool excessiveStep;
        static readonly Vector3[] previous = new Vector3[3];
        static Camera camera;
        static int warningEvents, foldingEvents;
        static FoldingBridgeVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                {
                    SessionState.SetBool(Pending, false); results.Clear(); routine = Verify(); resumeAt = 0;
                    deadline = EditorApplication.timeSinceStartup + 100;
                    EditorApplication.update -= Pump; EditorApplication.update += Pump;
                }
                else if (state == PlayModeStateChange.ExitingPlayMode && routine != null) Finish("Interrupted by Play Mode exit");
            };
        }
        [MenuItem("CHESS FIGHT/Obstacles/Verify Folding Bridge (Isolated Play)")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FoldingBridgePrefabBuilder.Path) == null) throw new Exception("Create folding bridge prefab first.");
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
            EditorApplication.update -= Pump; routine = null;
            Directory.CreateDirectory("Artifacts/ChessFight");
            File.WriteAllText("Artifacts/ChessFight/FoldingBridgeVerification.txt", DateTime.Now.ToString("s") + "\n" +
                (error == null ? "PASS" : "FAIL") + "\n" + string.Join("\n", results) + "\n" + error +
                "\nTemporary runtime scene only. No map placement. Not a two-PC Steam test.");
            if (error != null) Debug.LogError(error); else Debug.Log("[CHESS FIGHT] Folding bridge verification passed.");
            EditorApplication.isPlaying = false;
        }
        static void Check(bool value, string label)
        {
            if (!value) throw new Exception("FAIL: " + label);
            results.Add("PASS: " + label);
        }
        static void At(float time)
        {
            match.HasAuthority = false; match.ApplyRemote(MatchPhase.Running, time, 300 - time, 0); match.HasAuthority = true;
        }
        static void Park()
        {
            for (int i = 0; i < players.Length; i++) players[i].Teleport(origin + new Vector3(-6, .05f, i - 1));
        }
        static IEnumerator Verify()
        {
            yield return .2f;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Folding bridge verification (temporary)"));
            map = new GameObject("Test rules").AddComponent<StageMap>();
            match = map.gameObject.AddComponent<ProtectTheKingMatchController>(); map.match = match; match.map = map;
            match.throne = new GameObject("Test throne").AddComponent<ThroneInteraction>(); match.throne.match = match;
            map.killHeight = -20; map.outerLimit = 1000; map.checkpoints = Array.Empty<CheckpointGate>();
            map.players = new PlayerIdentity[3]; map.startPoints = new Transform[3]; players = new PlayerMotor[3];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FoldingBridgePrefabBuilder.Path);
            bool missingScript = false;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                missingScript |= GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0;
            Check(!missingScript, "All prefab script references are valid");
            bridge = Object.Instantiate(prefab, origin, Quaternion.identity).GetComponent<FoldingBridge>();
            Check(bridge.context.match == match && bridge.LeftPivot.isKinematic && bridge.RightPivot.isKinematic, "Scene context and two kinematic bodies connected");
            Check(bridge.GetComponentsInChildren<Collider>().Length == 4, "Only two panel boxes and two landing boxes collide");
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChessFight/Prefabs/Player_Graybox.prefab");
            for (int i = 0; i < 3; i++)
            {
                map.startPoints[i] = new GameObject("Recovery " + i).transform;
                map.startPoints[i].position = origin + new Vector3(-6, .05f, i - 1);
                players[i] = Object.Instantiate(playerPrefab, map.startPoints[i].position, Quaternion.identity).GetComponent<PlayerMotor>();
                players[i].map = map; players[i].ExternalControl = true; players[i].IsLocal = false;
                map.players[i] = players[i].Identity; map.players[i].playerId = i;
            }
            match.StartMatch(); foreach (var p in map.players) p.checkpoint = 5;
            match.enabled = false; At(0); yield return .15f;
            Check(bridge.State == FoldingBridgeState.Open && bridge.LeftAngle == 0, "Start delay holds an open bridge");
            Check(bridge.Evaluate(6.1f).state == FoldingBridgeState.Warning && bridge.Evaluate(8).state == FoldingBridgeState.Folding &&
                bridge.Evaluate(10.1f).state == FoldingBridgeState.Closed && bridge.Evaluate(12.1f).state == FoldingBridgeState.Unfolding &&
                bridge.Evaluate(14.6f).state == FoldingBridgeState.Open, "Complete default state sequence and repeat timing");
            Capture("FoldingBridge_Open");

            // Walk over both hinges and the small center clearance before testing folding.
            players[0].SetInput(Vector2.right, false); yield return 1.75f; players[0].SetInput(Vector2.zero, false);
            Check(players[0].transform.position.x > origin.x + 4 && players[0].Identity.falls == 0, "Existing CharacterController walks across both open panels and center seam");
            Park(); warningEvents = foldingEvents = 0;
            bridge.OnWarning.AddListener(() => warningEvents++); bridge.OnFolding.AddListener(() => foldingEvents++);
            At(6.02f); yield return .12f;
            Check(bridge.WarningLit && warningEvents == 1 && bridge.LeftAngle == 0, "Warning hook fires once before any panel motion");
            At(6.18f); yield return .12f;
            Check(!bridge.WarningLit && warningEvents == 1, "Warning strips blink without repeating sound hook each frame");
            Capture("FoldingBridge_Warning");

            // Actual per-frame simulation with three passengers on both panels.
            At(0); bridge.StartDelay = .2f; bridge.OpenDuration = .3f; bridge.WarningDuration = .5f; bridge.FoldDuration = 2; bridge.ClosedDuration = 1;
            yield return .2f;
            players[0].Teleport(origin + new Vector3(-2, .05f, -1));
            players[1].Teleport(origin + new Vector3(-2, .05f, 1));
            players[2].Teleport(origin + new Vector3(2, .05f, 0));
            yield return .15f;
            for (int i = 0; i < 3; i++) previous[i] = players[i].transform.position;
            excessiveStep = false; match.enabled = true;
            float until = Time.time + 2.1f;
            while (Time.time < until)
            {
                for (int i = 0; i < 3; i++)
                {
                    var delta = players[i].transform.position - previous[i];
                    if (delta.magnitude > 35 * Time.deltaTime + .25f) excessiveStep = true;
                    previous[i] = players[i].transform.position;
                }
                yield return null;
            }
            Check(bridge.LeftAngle > 25 && Mathf.Abs(bridge.LeftAngle - bridge.RightAngle) < .1f, "Two panels fold upward simultaneously with smooth timing");
            Check(players[0].transform.position.y > .5f && players[2].transform.position.y > .5f, "Unmodified player motor carries passengers on both rotating panels");
            Check(!excessiveStep && Array.TrueForAll(map.players, p => p.falls == 0), "Folding passengers have no teleport-sized frame or unexpected respawn");
            yield return 1.1f;
            Check(bridge.State == FoldingBridgeState.Closed && Mathf.Abs(bridge.LeftAngle - 70) < .1f, "Both panels reach closed angle");
            Capture("FoldingBridge_Closed");
            Check(!Physics.Raycast(origin + Vector3.up * 10, Vector3.down, 30, 1, QueryTriggerInteraction.Ignore), "Closed bridge exposes an unobstructed central fall gap");
            players[0].Teleport(origin + Vector3.up * .5f); match.enabled = false; yield return 2f;
            Check(players[0].Identity.falls == 1 && players[0].transform.position.x < origin.x - 4, "Falling through bridge uses existing personal respawn system");
            Park(); match.enabled = true; yield return 3.1f;
            Check(bridge.LeftAngle < .1f && bridge.RightAngle < .1f, "Unfolding restores the open walkway");

            match.enabled = false; At(0); bridge.Repeat = false; bridge.Simultaneous = false; bridge.RightDelay = .6f;
            yield return 1.5f;
            At(bridge.StartDelay + bridge.OpenDuration + bridge.WarningDuration + .4f); yield return .3f;
            Check(bridge.LeftAngle > 3 && bridge.RightAngle == 0, "Independent mode delays right panel while left folds");
            At(bridge.StartDelay + bridge.CycleDuration + 3); yield return 1.5f;
            Check(bridge.State == FoldingBridgeState.Open && bridge.LeftAngle == 0 && bridge.RightAngle == 0, "Repeat false completes one full cycle and stays open");
            bridge.FoldDuration = .01f;
            Check(bridge.EffectiveFoldDuration >= 1.75f, "Unsafe short fold duration is limited to safe angular speed");
            bridge.FoldDuration = 2;
            // Fresh rotated instance validates local hinge axes and child collider synchronization.
            Park(); Object.DestroyImmediate(bridge.gameObject); At(0);
            bridge = Object.Instantiate(prefab, origin, Quaternion.Euler(0, 90, 0)).GetComponent<FoldingBridge>();
            At(10.1f); yield return 1.6f;
            var panel = bridge.LeftPivot.GetComponentInChildren<BoxCollider>();
            Check(panel.attachedRigidbody == bridge.LeftPivot && panel.transform.position.y > 1 &&
                Mathf.Abs(panel.transform.position.x - origin.x) < .1f, "Rotated bridge collider follows its local hinge and visual model");
            match.HasAuthority = false; match.ApplyRemote(MatchPhase.Running, 12.8f, 250, 0); yield return .6f;
            Check(bridge.LeftAngle < 68, "Client animates from existing shared match time without new network protocol");
            match.HasAuthority = true; match.ReturnToReady(); yield return 1.5f;
            Check(bridge.LeftAngle == 0 && bridge.RightAngle == 0, "Returning match to Ready safely restores flat panels");
        }
        static void Capture(string name)
        {
            if (camera == null)
            {
                camera = new GameObject("Preview camera").AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = 6.2f; camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.12f, .15f, .2f); camera.farClipPlane = 100;
                camera.transform.position = origin + new Vector3(11, 10, -15); camera.transform.LookAt(origin + Vector3.up);
                var light = new GameObject("Preview light").AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = 1.5f; light.cullingMask = 1 << 30; light.transform.rotation = Quaternion.Euler(45, -35, 0);
            }
            var transforms = bridge.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms) t.gameObject.layer = 30;
            var rt = RenderTexture.GetTemporary(900, 600, 24); var old = RenderTexture.active;
            var texture = new Texture2D(900, 600, TextureFormat.RGB24, false);
            try
            {
                camera.aspect = 1.5f;
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 900, 600), 0, 0); texture.Apply();
                Directory.CreateDirectory("Artifacts/ChessFight/ObstaclePreviews");
                File.WriteAllBytes("Artifacts/ChessFight/ObstaclePreviews/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(texture);
                foreach (var t in transforms) t.gameObject.layer = 0;
            }
        }
    }
}
