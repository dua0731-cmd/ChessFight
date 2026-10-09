using System;
using System.IO;
using System.Linq;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.RagdollLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Maps.Editor
{
    /// <summary>Copies the lab's saved player configuration, without importing its practice arena.</summary>
    public static class ImportedMapRagdollSetup
    {
        const string MapPath = "Assets/Scenes/ImportedChessFightMap.unity";
        const string LabPath = "Assets/Scenes/RagdollTest.unity";

        [MenuItem("ChessFight/Imported Map/Use RagdollTest Character %&F12")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != MapPath)
                throw new InvalidOperationException("Open ImportedChessFightMap in Edit Mode first.");
            if (scene.isDirty)
                throw new InvalidOperationException("Save your scene edits before applying the character setup.");

            var fixture = scene.GetRootGameObjects().Single(g => g.name == "Core Playtest");
            var start = fixture.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Core Playtest Spawn");
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true))
                .Single(c => c.CompareTag("MainCamera"));
            var preview = EditorSceneManager.OpenPreviewScene(LabPath);
            try
            {
                var source = preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LabGame>(true)).Single();
                if (source.pawnPrefab == null || source.tuning == null || source.labCamera == null || source.players.Length == 0)
                    throw new InvalidOperationException("RagdollTest is missing its player configuration.");

                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Use RagdollTest character on imported map");
                int group = Undo.GetCurrentGroup();
                var game = GetOrAdd<LabGame>(fixture);
                var follow = GetOrAdd<LabCamera>(camera.gameObject);
                var slots = GetOrAdd<LabParamSlots>(fixture);
                var panel = GetOrAdd<LabPanel>(fixture);
                Undo.RecordObjects(new UnityEngine.Object[] { game, follow, slots, panel, camera }, "Copy lab configuration");
                EditorUtility.CopySerialized(source, game);
                EditorUtility.CopySerialized(source.labCamera, follow);
                camera.CopyFrom(source.labCamera.Cam);
                camera.enabled = true;
                game.labCamera = follow;
                game.slots = slots;
                game.bar = null;
                game.players = new[] { new LabGame.Slot {
                    name = source.players[0].name, device = source.players[0].device,
                    spawn = start.position, material = source.players[0].material
                } };
                game.spawnPracticeObjects = false;
                game.fallLimit = -7f;
                follow.game = game;
                follow.playerIndex = 0;
                follow.soloTarget = null;
                follow.freeMode = false;
                slots.game = game;
                slots.tuning = game.tuning;
                panel.game = game;

                // These components would otherwise spawn a second pawn or compete for the camera/time.
                Remove<PlaytestSpawner>(fixture);
                Remove<CameraRig>(fixture);
                Remove<PhysicsProfile>(fixture);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the map scene.");
                Undo.CollapseUndoOperations(group);
                Selection.activeGameObject = fixture;
                Debug.Log("Imported map now uses RagdollTest P1, tuning, physics, input and orbit camera. Play and click the Game view.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Validate();
        }

        [MenuItem("ChessFight/Imported Map/Validate Ragdoll Character %&F11")]
        public static void Validate()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MapPath) throw new InvalidOperationException("Open ImportedChessFightMap first.");
            var roots = scene.GetRootGameObjects();
            var game = roots.SelectMany(g => g.GetComponentsInChildren<LabGame>(true)).Single();
            var preview = EditorSceneManager.OpenPreviewScene(LabPath);
            try
            {
                var source = preview.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LabGame>(true)).Single();
                Require(game.pawnPrefab == source.pawnPrefab && game.tuning == source.tuning, "shared pawn and tuning");
                Require(game.players.Length == 1 && game.players[0].material == source.players[0].material, "P1 appearance");
                Require(game.players[0].device == source.players[0].device, "P1 controls");
                Require(game.physicsRate == source.physicsRate && game.solverIterations == source.solverIterations, "physics settings");
                Require(!game.spawnPracticeObjects && game.fallLimit == -7f, "course respawn configuration");
                Require(game.labCamera != null && game.labCamera.game == game && game.labCamera.Cam.enabled, "camera wiring");
                Require(game.slots != null && game.slots.game == game && game.GetComponent<LabPanel>().game == game, "panel wiring");
                Require(!roots.SelectMany(g => g.GetComponentsInChildren<PlaytestSpawner>(true)).Any(), "no duplicate spawner");
                foreach (var t in roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "scripts on " + t.name);
                if (Application.isPlaying)
                {
                    var pawn = game.players[0].pawn;
                    Require(pawn != null && RagdollPawn.All.Count == 1, "one runtime pawn");
                    Require(pawn.skin.sharedMaterial == source.players[0].material, "runtime material");
                    Require(pawn.tuning == source.tuning && game.labCamera.Target == pawn, "runtime tuning and follow");
                    Require(pawn.bodies.All(b => b.solverIterations == source.solverIterations), "runtime solvers");
                    Require(Mathf.Abs(Time.fixedDeltaTime - 1f / source.physicsRate) < 0.00001f, "runtime physics rate");
                    Require(Mathf.Abs(Physics.gravity.y + 9.81f * source.tuning.values.gravityScale) < 0.0001f, "runtime gravity");
                    Require(game.GetComponent<QueenHillTestBed>() == null && game.dummies.Count == 0, "no practice objects");
                    Require(pawn.Hips.position.y > game.fallLimit, "pawn on course");
                }
                Directory.CreateDirectory("Logs/ImportedMapValidation");
                File.WriteAllText("Logs/ImportedMapValidation/RagdollCharacter" + (Application.isPlaying ? "Runtime" : "Scene") + ".txt",
                    "PASS: RagdollTest character configuration and references match. " +
                    (Application.isPlaying ? "Runtime pawn, material, camera, gravity and solver checks passed. " : "Scene checks passed. ") +
                    "Human play/feel confirmation pending.\n");
                Debug.Log("Imported map ragdoll character checks passed (" + (Application.isPlaying ? "Play Mode" : "Edit Mode") + ").");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        static T GetOrAdd<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? Undo.AddComponent<T>(go);
        static void Remove<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null) Undo.DestroyObjectImmediate(component);
        }
        static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("Ragdoll map check failed: " + label);
        }
    }
}
