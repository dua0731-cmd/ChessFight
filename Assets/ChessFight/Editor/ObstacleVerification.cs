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
    public static class ObstacleVerification
    {
        const string Pending = "ChessFight.ObstacleVerification";
        static readonly List<string> results = new List<string>();
        static IEnumerator routine;
        static float resumeAt;
        static double deadline;
        static StageMap map;
        static ProtectTheKingMatchController match;
        static PlayerMotor[] motors;
        static GameObject obstacle;
        static Camera previewCamera;
        static readonly Vector3 origin = new Vector3(150, 0, 100);

        static ObstacleVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                {
                    SessionState.SetBool(Pending, false); results.Clear(); routine = Verify();
                    resumeAt = 0; deadline = EditorApplication.timeSinceStartup + 100;
                    EditorApplication.update -= Pump; EditorApplication.update += Pump;
                }
            };
        }

        [MenuItem("CHESS FIGHT/Obstacles/Run Isolated Play Verification %#o")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            ObstaclePrefabBuilder.Validate();
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
            File.WriteAllText("Artifacts/ChessFight/ObstaclePlayVerification.txt", DateTime.Now.ToString("s") + "\n" +
                (error == null ? "PASS" : "FAIL") + "\n" + string.Join("\n", results) + "\n" + error +
                "\nIsolated runtime scene; no map instances saved. Does not replace two-PC Steam testing.");
            if (error != null) Debug.LogError(error); else Debug.Log("[CHESS FIGHT] Obstacle play verification passed.");
            EditorApplication.isPlaying = false;
        }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("FAIL: " + message);
            results.Add("PASS: " + message);
        }
        static void At(float time)
        {
            match.HasAuthority = false; match.ApplyRemote(MatchPhase.Running, time, 300 - time, 0); match.HasAuthority = true;
        }
        static void Park()
        {
            for (int i = 0; i < motors.Length; i++) motors[i].Teleport(origin + new Vector3(20 + i, .05f, 10));
        }
        static GameObject Spawn(int index, float yaw = 0)
        {
            Park(); if (obstacle != null) Object.DestroyImmediate(obstacle);
            At(0);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ObstaclePrefabBuilder.Folder + "/" + AssetName(index) + ".prefab");
            obstacle = Object.Instantiate(prefab, origin, Quaternion.Euler(0, yaw, 0));
            CapturePreview(index);
            Physics.SyncTransforms(); return obstacle;
        }
        static string AssetName(int index) => index == 10 ? "11_PendulumBall" : ObstaclePrefabBuilder.Names[index];
        static void CapturePreview(int index)
        {
            if (previewCamera == null)
            {
                previewCamera = new GameObject("Asset preview camera").AddComponent<Camera>();
                previewCamera.enabled = false; previewCamera.orthographic = true; previewCamera.cullingMask = 1 << 30;
                previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(.12f, .15f, .2f);
                previewCamera.nearClipPlane = .1f; previewCamera.farClipPlane = 100;
                var light = new GameObject("Asset preview light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 2; light.cullingMask = 1 << 30;
                light.transform.rotation = Quaternion.Euler(45, -35, 0);
            }
            var transforms = obstacle.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms) t.gameObject.layer = 30;
            var renderers = obstacle.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            previewCamera.orthographicSize = Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z)) * 1.15f + .7f;
            previewCamera.transform.position = bounds.center + new Vector3(1.1f, .9f, -1.4f).normalized * 30;
            previewCamera.transform.LookAt(bounds.center);
            var rt = RenderTexture.GetTemporary(640, 440, 24); var previous = RenderTexture.active;
            var texture = new Texture2D(640, 440, TextureFormat.RGB24, false);
            try
            {
                previewCamera.aspect = 640f / 440;
                RenderPipeline.SubmitRenderRequest(previewCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 640, 440), 0, 0); texture.Apply();
                Directory.CreateDirectory("Artifacts/ChessFight/ObstaclePreviews");
                File.WriteAllBytes("Artifacts/ChessFight/ObstaclePreviews/" + AssetName(index) + ".png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(texture);
                foreach (var t in transforms) t.gameObject.layer = 0;
            }
        }
        static IEnumerator Verify()
        {
            yield return .2f;
            // Separate scene and distant coordinates prevent contact with the loaded gameplay map.
            SceneManager.SetActiveScene(SceneManager.CreateScene("Obstacle verification (temporary)"));
            map = new GameObject("Test rules").AddComponent<StageMap>();
            match = map.gameObject.AddComponent<ProtectTheKingMatchController>(); map.match = match; match.map = map;
            match.throne = new GameObject("Test throne").AddComponent<ThroneInteraction>(); match.throne.match = match;
            match.throne.transform.position = new Vector3(150, 0, 750);
            map.checkpoints = Array.Empty<CheckpointGate>(); map.outerLimit = 1000; map.killHeight = -100;
            map.players = new PlayerIdentity[12]; map.startPoints = new Transform[12]; motors = new PlayerMotor[12];
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChessFight/Prefabs/Player_Graybox.prefab");
            for (int i = 0; i < 12; i++)
            {
                map.startPoints[i] = new GameObject("Start " + i).transform;
                map.startPoints[i].position = origin + new Vector3(20 + i, .05f, 10);
                motors[i] = Object.Instantiate(playerPrefab, map.startPoints[i].position, Quaternion.identity).GetComponent<PlayerMotor>();
                motors[i].map = map; motors[i].ExternalControl = true; motors[i].IsLocal = false;
                map.players[i] = motors[i].Identity; map.players[i].playerId = i;
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Test floor"; floor.transform.position = origin + Vector3.down * .5f; floor.transform.localScale = new Vector3(90, 1, 70);
            match.StartMatch(); foreach (var identity in map.players) identity.checkpoint = 5;
            var player = motors[0];

            var disc = Spawn(0).GetComponent<ObstacleSurface>();
            Check(disc.context.match == match, "Prefab automatically resolves its own scene's match");
            player.Teleport(origin + new Vector3(1, .42f, 0)); yield return .5f;
            Check(Vector3.ProjectOnPlane(player.transform.position - origin, Vector3.up).magnitude > 1.3f, "Disc actually moves a stationary CharacterController outward");
            Check(disc.VelocityAt(origin + Vector3.right).magnitude > disc.VelocityAt(origin + Vector3.right * 3.5f).magnitude, "Disc inner region moves faster than rim");

            Spawn(1, 90); player.Teleport(origin + Vector3.up * .45f); yield return .4f;
            Check(player.transform.position.y > 2 && player.transform.position.x > origin.x + .7f, "Rotated jump pad launches player upward and along local direction");

            Spawn(2); player.Teleport(origin + new Vector3(3, .05f, 0)); yield return 1.1f;
            Check(player.transform.position.x > origin.x + 4, "Reciprocating pusher hits and displaces stationary player");

            Spawn(3); player.Teleport(origin + new Vector3(2.3f, .05f, 0)); yield return 1.1f;
            Check(Vector3.ProjectOnPlane(player.transform.position - origin - Vector3.right * 2.3f, Vector3.up).magnitude > .8f, "Rotating hammer transfers impact to stationary player");

            var tileRoot = Spawn(4); match.enabled = false;
            At(0); yield return .15f;
            var tile = tileRoot.GetComponentsInChildren<ObstacleMotion>()[0];
            player.Teleport(tile.transform.position + Vector3.up * .22f); yield return .15f;
            float beforeY = player.transform.position.y;
            match.enabled = true; yield return .7f;
            Check(player.transform.position.y > beforeY + .7f, "Rising tile carries the existing CharacterController upward");
            match.enabled = false; At(4.5f); yield return .15f;
            Check(tile.transform.position.y < -2.5f, "Tile descends below floor level");
            match.enabled = true;

            var walls = Spawn(5).GetComponentsInChildren<ObstacleMotion>();
            match.enabled = false; At(1.125f); yield return .15f;
            float firstLeft = walls[0].transform.localPosition.x, firstRight = walls[1].transform.localPosition.x;
            At(3.375f); yield return .15f;
            Check(Mathf.Abs(walls[0].transform.localPosition.x - firstLeft) > 3 && Mathf.Abs(walls[1].transform.localPosition.x - firstRight) > 3,
                "Sliding walls alternate opposite phases");
            match.enabled = true;

            var bridge = Spawn(6).GetComponentInChildren<WeightedBridge>(); yield return .15f;
            player.Teleport(origin + new Vector3(2, 1.23f, -2)); motors[1].Teleport(origin + new Vector3(2, 1.23f, 2)); yield return .22f;
            Check(bridge.RiderCount == 2 && bridge.CalculateTarget() < -8, "Bridge measures two players on the right side");
            yield return .25f; float rightAngle = bridge.Angle;
            Check(rightAngle < -3, "Bridge actually tilts under player weight");
            player.Teleport(bridge.transform.TransformPoint(new Vector3(-2, .23f, -2)));
            motors[1].Teleport(bridge.transform.TransformPoint(new Vector3(-2, .23f, 2))); yield return .4f;
            Check(bridge.Angle > rightAngle + 2, "Moving players to opposite side reverses tilt");
            Park(); yield return 1f;
            Check(Mathf.Abs(bridge.Angle) < .5f, "Empty bridge returns to neutral");
            WeightedBridge.Capture(bridge.gameObject.scene, out var ids, out var angles);
            Check(ids.Length == 1 && ids[0] == bridge.NetworkId, "Bridge state captured for online snapshots");
            match.HasAuthority = false; WeightedBridge.ApplyRemote(bridge.gameObject.scene, ids, new[] { 12f }); yield return .5f;
            Check(Mathf.Abs(bridge.Angle - 12) < 1, "Client bridge follows host angle without local weight simulation");
            match.HasAuthority = true;

            Spawn(7, 90); player.Teleport(origin + Vector3.up * .43f); yield return .6f;
            Check(player.transform.position.x < origin.x - 1, "Rotated conveyor transports player along its local belt direction");
            var conveyor = obstacle.GetComponent<ObstacleSurface>(); conveyor.enabled = false;
            var before = player.transform.position; yield return .3f;
            Check(Mathf.Abs(player.transform.position.x - before.x) < .2f, "Disabled surface stops applying conveyor force");

            var clock = Spawn(8); match.enabled = false; At(1.25f); yield return .15f;
            var leftGate = clock.transform.Find("Left gate"); var rightGate = clock.transform.Find("Right gate");
            Check(leftGate.localPosition.y > 5 && rightGate.localPosition.y < 2, "Clock opens left route while closing right");
            At(3.75f); yield return .15f;
            Check(leftGate.localPosition.y < 2 && rightGate.localPosition.y > 5, "Clock swaps both routes on half-period");
            match.enabled = true;

            var falling = Spawn(9).GetComponent<FallingChessPiece>();
            player.Teleport(origin + new Vector3(2, .03f, 0)); yield return 1f;
            Check(Mathf.Abs(player.transform.position.x - origin.x - 2) < .1f && falling.warning.gameObject.activeSelf, "Falling piece warns before applying force");
            yield return 1.65f;
            Check(player.transform.position.x > origin.x + 3, "Expanding impact shockwave knocks nearby player outward");
            Park(); player.Teleport(origin + new Vector3(2, .03f, 0));
            match.HasAuthority = false; At(falling.Period); match.HasAuthority = false;
            match.ApplyRemote(MatchPhase.Running, falling.Period + 2.5f, 280, 0); yield return .3f;
            Check(Mathf.Abs(player.transform.position.x - origin.x - 2) < .1f, "Non-authoritative client does not apply duplicate shockwave forces");
            match.HasAuthority = true;
            Check(Array.TrueForAll(map.players, p => p.falls == 0), "Obstacle tests did not bypass map bounds or trigger unwanted respawns");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PendulumPrefabBuilder.Path) != null)
            {
                var swing = Spawn(10).GetComponentInChildren<PendulumSwing>();
                var impact = swing.GetComponent<ObstacleImpact>();
                Check(swing.Ball.GetComponent<SphereCollider>() != null && impact.pendulum == swing && swing.context.match == match,
                    "Pendulum solid ball collider, existing impact system and scene context are connected");
                Check(swing.EvaluateAngle(.2f) == -55 && Mathf.Abs(swing.EvaluateAngle(1.7f)) < .01f &&
                    Mathf.Abs(swing.EvaluateAngle(2.8f) - 55) < .01f && Mathf.Abs(swing.EvaluateAngle(4.9f) + 55) < .01f,
                    "Pendulum cosine timing includes start delay, center crossings and pauses at both ends");
                player.Teleport(origin + new Vector3(0, .05f, 0)); yield return .4f;
                Check(swing.BallVelocity.sqrMagnitude < .001f && Mathf.Abs(swing.Angle + 55) < .01f, "Pendulum remains still during start delay");
                yield return 1.5f;
                Check(player.transform.position.x > origin.x + 1 && player.Identity.falls == 0,
                    "Pendulum rightward crossing knocks stationary CharacterController right without direct death");
                Park(); match.enabled = false; At(2.8f); yield return .85f;
                Check(Mathf.Abs(swing.Angle - 55) < .2f && swing.BallVelocity.sqrMagnitude < .001f, "Pendulum pauses at right extreme");
                player.Teleport(origin + new Vector3(0, .05f, 0)); match.enabled = true; yield return 1.6f;
                Check(player.transform.position.x < origin.x - 1 && player.Identity.falls == 0,
                    "Pendulum reverse crossing knocks player left using current ball velocity");
                var receiver = motors[2]; receiver.Teleport(origin + new Vector3(20, .05f, 0));
                Check(impact.Hit(receiver, Vector3.right) && !impact.Hit(receiver, Vector3.right), "Shared impact cooldown rejects duplicate pendulum hit in same frame");
                swing.Active = false; float stopped = swing.Angle; yield return .2f;
                Check(Mathf.Abs(swing.Angle - stopped) < .01f && !impact.Hit(motors[3], Vector3.right), "Inactive pendulum freezes and rejects knockback");
                swing.Active = true; Park(); yield return .1f;
                match.HasAuthority = false;
                Check(!impact.Hit(motors[4], Vector3.right), "Pendulum client cannot apply authoritative knockback");
                match.HasAuthority = true;
                // A rotated instance must swing across its local X axis, not world X.
                swing = Spawn(10, 90).GetComponentInChildren<PendulumSwing>(); match.enabled = false; At(1.7f); yield return 1f;
                Check(Mathf.Abs(swing.Ball.position.x - origin.x) < .03f && Mathf.Abs(swing.Ball.position.z - origin.z) < .2f,
                    "Rotated pendulum reaches the correct central passage");
                match.enabled = true;
            }
            var snapshot = new MatchSnapshot { bridgeIds = new uint[64], bridgeAngles = new float[64] };
            for (int i = 0; i < 64; i++) { snapshot.bridgeIds[i] = (uint)i + 1; snapshot.bridgeAngles[i] = i % 2 == 0 ? 24 : -24; }
            var packet = MatchProtocol.Encode(MatchMessage.Snapshot, snapshot: snapshot);
            Check(packet.Length <= MatchProtocol.MaxPacket && MatchProtocol.Decode(packet, out _, out _, out var received) &&
                received.bridgeIds.Length == 64 && received.bridgeAngles[63] == -24, "Maximum bridge snapshot fits packet budget and round-trips");
            snapshot.bridgeAngles[0] = float.NaN;
            Check(!MatchProtocol.Decode(MatchProtocol.Encode(MatchMessage.Snapshot, snapshot: snapshot), out _, out _, out _), "Invalid remote bridge angle is rejected");
        }
    }
}
