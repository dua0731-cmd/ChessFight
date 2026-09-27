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
    public static class SpringAndVentVerification
    {
        const string Pending = "ChessFight.SpringVentTest";
        static IEnumerator routine;
        static double deadline;
        static float resumeAt;
        static readonly List<string> results = new List<string>();
        static readonly Vector3 origin = new Vector3(400, 0, 100);
        static StageMap map;
        static ProtectTheKingMatchController match;
        static PlayerMotor[] players;
        static Camera camera;
        static SpringAndVentVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                {
                    SessionState.SetBool(Pending, false); results.Clear(); routine = Verify(); resumeAt = 0;
                    deadline = EditorApplication.timeSinceStartup + 100; EditorApplication.update -= Pump; EditorApplication.update += Pump;
                }
                else if (state == PlayModeStateChange.ExitingPlayMode && routine != null) Finish("Interrupted");
            };
        }
        [MenuItem("CHESS FIGHT/Obstacles/Verify Spring Pillar and Air Vent (Isolated Play)")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first.");
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
            File.WriteAllText("Artifacts/ChessFight/SpringAndVentVerification.txt", DateTime.Now.ToString("s") + "\n" +
                (error == null ? "PASS" : "FAIL") + "\n" + string.Join("\n", results) + "\n" + error +
                "\nTemporary runtime scene only. No two-PC Steam test.");
            if (error != null) Debug.LogError(error); else Debug.Log("[CHESS FIGHT] Spring pillar and air vent verification passed.");
            EditorApplication.isPlaying = false;
        }
        static void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); results.Add("PASS: " + label); }
        static void Park() { for (int i = 0; i < players.Length; i++) players[i].Teleport(origin + new Vector3(-15, .05f, i * 2)); }
        static void At(float time) { match.HasAuthority = false; match.ApplyRemote(MatchPhase.Running, time, 300 - time, 0); match.HasAuthority = true; }
        static IEnumerator Verify()
        {
            yield return .2f;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Spring and vent verification (temporary)"));
            map = new GameObject("Test rules").AddComponent<StageMap>(); match = map.gameObject.AddComponent<ProtectTheKingMatchController>();
            map.match = match; match.map = map; map.outerLimit = 1000; map.killHeight = -50; map.checkpoints = Array.Empty<CheckpointGate>();
            match.throne = new GameObject("Test throne").AddComponent<ThroneInteraction>(); match.throne.match = match;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.position = origin + Vector3.down * .5f;
            ground.transform.localScale = new Vector3(240, 1, 180);
            map.players = new PlayerIdentity[3]; map.startPoints = new Transform[3]; players = new PlayerMotor[3];
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChessFight/Prefabs/Player_Graybox.prefab");
            for (int i = 0; i < 3; i++)
            {
                map.startPoints[i] = new GameObject("Start " + i).transform; map.startPoints[i].position = origin + new Vector3(-15, .05f, i * 2);
                players[i] = Object.Instantiate(playerPrefab, map.startPoints[i].position, Quaternion.identity).GetComponent<PlayerMotor>();
                players[i].map = map; players[i].ExternalControl = true; map.players[i] = players[i].Identity; map.players[i].playerId = i;
            }
            match.StartMatch(); foreach (var p in map.players) p.checkpoint = 5;
            var springPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpringAndVentPrefabBuilder.SpringPath);
            var ventPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpringAndVentPrefabBuilder.VentPath);
            Check(springPrefab != null && ventPrefab != null, "Both prefab assets exist");
            bool valid = true;
            foreach (var prefab in new[] { springPrefab, ventPrefab })
            {
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) valid &= GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0;
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true)) valid &= r.sharedMaterial != null;
            }
            Check(valid, "Prefab script and material references valid");
            var spring = Object.Instantiate(springPrefab, origin, Quaternion.Euler(0, 90, 0)).GetComponent<SpringPillar>();
            spring.WarningDuration = .2f; spring.ExtendDuration = .2f; spring.HoldDuration = .6f; spring.RetractDuration = .3f; spring.Cooldown = .4f;
            yield return .1f; Check(spring.ActivationCount == 0, "Spring remains Ready with no nearby player");
            players[0].Teleport(origin + new Vector3(2, .05f, 0)); yield return .08f;
            Check(spring.State == SpringPillarState.Warning && spring.ActivationCount == 1 && spring.WarningVisual.activeSelf, "Entering TriggerArea starts warning");
            Capture(spring.gameObject, "SpringPillar_Warning"); yield return .45f;
            Check(spring.HitsThisActivation == 1 && players[0].transform.position.x > origin.x + 3.5f, "Rotated spring hits stationary player in local Forward direction");
            Capture(spring.gameObject, "SpringPillar_Extended");
            players[0].Teleport(spring.transform.TransformPoint(new Vector3(1.4f, .05f, 2))); yield return .15f;
            Check(spring.HitsThisActivation == 1, "Hold does not repeat knockback on side contact");
            yield return .65f; Check(!spring.Trigger(), "Manual trigger is rejected during cooldown");
            yield return 1.2f;
            Check(spring.State == SpringPillarState.Ready && spring.ActivationCount == 1, "Standing in trigger does not repeatedly activate with RequireReentry");
            Park(); yield return .12f; players[0].Teleport(origin + new Vector3(2, .05f, 0)); yield return .08f;
            Check(spring.ActivationCount == 2, "Exit and re-entry rearms spring after cooldown");
            Park(); yield return 1.8f; spring.Repeat = false;
            Check(!spring.Trigger(), "Repeat false prevents another activation");
            spring.Repeat = true; spring.AutoTrigger = false; players[0].Teleport(origin + new Vector3(2, .05f, 0)); yield return .1f;
            Check(spring.State == SpringPillarState.Ready && spring.Trigger(), "AutoTrigger off supports explicit manual activation");
            Park(); yield return 1.8f; spring.AutoTrigger = true; spring.RequireReentry = false;
            players[0].Teleport(spring.transform.TransformPoint(new Vector3(1.4f, .05f, 2))); int before = spring.ActivationCount;
            yield return 2f; Check(spring.ActivationCount >= before + 2, "Optional continuous occupancy reactivation still passes through full cooldown");
            Park(); Object.DestroyImmediate(spring.gameObject);

            At(0); var vent = Object.Instantiate(ventPrefab, origin, Quaternion.Euler(0, 90, 0)).GetComponent<AirVent>();
            vent.StartDelay = 0; vent.IdleDuration = .2f; vent.WarningDuration = .3f; vent.BlowDuration = 10; vent.MaxPushSpeed = 6;
            vent.WindTrigger.size = new Vector3(3, 5, 100); vent.WindTrigger.center = Vector3.forward * 45;
            players[0].Teleport(origin + new Vector3(2, .05f, 0)); players[1].Teleport(origin + new Vector3(2, .05f, 5));
            yield return .3f; Check(vent.State == AirVentState.Warning && vent.WarningVisual.activeSelf && players[0].transform.position.x < origin.x + 2.1f, "Vent warning produces no wind force");
            Capture(vent.gameObject, "AirVent_Warning"); yield return .35f;
            float earlySpeed = players[0].GetComponent<CharacterController>().velocity.x;
            Check(earlySpeed > .1f && earlySpeed < 5, "Wind accelerates gradually rather than applying one large impulse");
            yield return 1f;
            Check(players[0].transform.position.x > origin.x + 5 && Mathf.Abs(players[1].transform.position.x - origin.x - 2) < .1f,
                "Rotated vent pushes only players inside WindTrigger");
            float maxSpeed = 0, until = Time.time + 2;
            while (Time.time < until) { maxSpeed = Mathf.Max(maxSpeed, players[0].GetComponent<CharacterController>().velocity.x); yield return null; }
            Check(maxSpeed <= 6.3f && vent.PeakWindContribution <= 6.001f, "Sustained wind stays within configured contribution and observed speed limits");
            Capture(vent.gameObject, "AirVent_Blowing");
            players[0].Teleport(origin + new Vector3(2, .05f, 5)); var outside = players[0].transform.position; yield return .3f;
            Check(Mathf.Abs(players[0].transform.position.x - outside.x) < .1f, "Leaving wind volume stops adding force");
            Park(); Object.DestroyImmediate(vent.gameObject); At(0);
            vent = Object.Instantiate(ventPrefab, origin, Quaternion.Euler(-90, 0, 0)).GetComponent<AirVent>();
            vent.StartDelay = vent.IdleDuration = vent.WarningDuration = 0; vent.BlowDuration = 5;
            vent.WindStrength = 40; vent.MaxPushSpeed = 8; vent.AirborneMultiplier = vent.GroundedMultiplier = 1;
            vent.WindTrigger.size = new Vector3(5, 5, 20);
            players[0].Teleport(origin + new Vector3(0, .15f, -1.3f)); yield return .65f;
            Check(players[0].transform.position.y > 1 && vent.PeakWindContribution <= 8.001f, "Floor-oriented upward vent lifts CharacterController with capped speed: y=" + players[0].transform.position.y + ", peak=" + vent.PeakWindContribution + ", velocity=" + players[0].GetComponent<CharacterController>().velocity);
            Park(); Object.DestroyImmediate(vent.gameObject); At(0);
            vent = Object.Instantiate(ventPrefab, origin, Quaternion.identity).GetComponent<AirVent>();
            vent.StartDelay = vent.IdleDuration = vent.WarningDuration = 0; vent.BlowDuration = 3;
            vent.WindDirection = new Vector3(0, 1, 1); vent.WindStrength = 50; vent.VerticalForce = 10;
            vent.AirborneMultiplier = 1; vent.GroundedMultiplier = 0; vent.WindTrigger.size = new Vector3(6, 15, 20);
            players[0].Teleport(origin + new Vector3(0, .05f, 2)); yield return .3f;
            Check(players[0].transform.position.z < origin.z + 2.1f, "GroundedMultiplier zero suppresses grounded wind");
            players[0].Teleport(origin + new Vector3(0, 2, 2)); yield return .65f;
            Check(players[0].transform.position.y > 2.5f && players[0].transform.position.z > origin.z + 3, "Airborne multiplier and diagonal/upward force combine");
            Park(); match.HasAuthority = false; players[0].Teleport(origin + new Vector3(0, .05f, 2)); vent.GroundedMultiplier = 1; yield return .3f;
            Check(players[0].transform.position.z < origin.z + 2.1f, "Client does not apply authoritative wind impulses");
            match.HasAuthority = true; Park(); vent.Repeat = false; At(20); yield return .1f;
            Check(vent.State == AirVentState.Idle && !vent.WindVisual.activeSelf, "Repeat false returns vent to Idle after one blow");
            Check(Array.TrueForAll(map.players, p => p.falls == 0), "Tests do not directly kill or respawn players");
        }
        static void Capture(GameObject target, string name)
        {
            if (camera == null)
            {
                camera = new GameObject("Preview camera").AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
                camera.orthographicSize = 4.5f; camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.12f, .15f, .2f); camera.farClipPlane = 100;
                camera.transform.position = origin + new Vector3(10, 7, -10); camera.transform.LookAt(origin + new Vector3(1, 1, 0));
                var light = new GameObject("Preview light").AddComponent<Light>(); light.type = LightType.Directional;
                light.intensity = 1.5f; light.cullingMask = 1 << 30; light.transform.rotation = Quaternion.Euler(45, -35, 0);
            }
            var transforms = target.GetComponentsInChildren<Transform>(true); foreach (var t in transforms) t.gameObject.layer = 30;
            var rt = RenderTexture.GetTemporary(800, 600, 24); var old = RenderTexture.active; var texture = new Texture2D(800, 600, TextureFormat.RGB24, false);
            try
            {
                camera.aspect = 4f / 3; RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 800, 600), 0, 0); texture.Apply();
                Directory.CreateDirectory("Artifacts/ChessFight/ObstaclePreviews"); File.WriteAllBytes("Artifacts/ChessFight/ObstaclePreviews/" + name + ".png", texture.EncodeToPNG());
            }
            finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(texture); foreach (var t in transforms) t.gameObject.layer = 0; }
        }
    }
}
