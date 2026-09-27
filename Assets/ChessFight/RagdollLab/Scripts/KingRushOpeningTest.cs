using System.Collections;
using System.IO;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.RagdollLab
{
    // Real player/PhysX checks. Teleports are fixtures, never evidence of course traversal.
    public sealed class KingRushOpeningTest : MonoBehaviour
    {
        KingRushOpening game;
        int passed, failed;
        bool walkOK;
        void Check(bool ok, string message)
        { if (ok) { passed++; Debug.Log("[OpeningTest] PASS " + message); } else { failed++; Debug.LogError("[OpeningTest] FAIL " + message); } }
        IEnumerator Start()
        {
            game = GetComponent<KingRushOpening>(); yield return new WaitForSeconds(1.5f);
            var a = game.Local; var enemy = game.Players[6]; var ally = game.Players[1];
            Check(game.Players.Count == 12 && game.checkpoints.Length == 7 && game.planks.Length == 12, "Course references and twelve stationary bodies");
            Check(!game.Mission.Started && a.Section == KingRushSection.Red1, "Mission waits for first arrival");
            var state = GetComponent<UIDocument>().rootVisualElement.Q<Label>("opening-objective");
            Check(state != null && state.worldBound.width > 100 && state.text.Contains("장난감"), "Opening HUD has visible Korean objective");
            yield return Capture("start");
            yield return Overview();

            float standing = a.BodyPosition.y, peak = standing;
            a.SetInput(new PawnInput { jump = true });
            for (int i = 0; i < 240; i++)
            { yield return new WaitForFixedUpdate(); peak = Mathf.Max(peak, a.BodyPosition.y); }
            a.SetInput(default);
            Check(peak - standing > .8f && peak - standing < 3, $"Measured unmodified standing jump clears button rise: {peak - standing:0.00} m");

            // One uninterrupted path from the spawn through the red course, using only movement and jump.
            yield return Walk(a, new Vector3(0, 0, 42), 14);
            Check(walkOK && game.CheckpointOf(a) >= 1, "Walk from spawn through moving pawn field");
            yield return Walk(a, new Vector3(2, 0, 43), 3);
            yield return Walk(a, new Vector3(2, 0, 68), 14);
            Check(walkOK && a.BodyPosition.y > 5 && game.CheckpointOf(a) == 3, $"Climb rolling-head slope on a clear lane ({a.BodyPosition})");
            yield return Capture("slope");
            yield return Walk(a, new Vector3(0, 0, 96), 12);
            Check(walkOK && game.CheckpointOf(a) == 4, "Descend felt slide to clock checkpoint");
            yield return Walk(a, new Vector3(0, 0, 121), 15, true);
            Check(walkOK && game.CheckpointOf(a) == 5, $"Cross two alternating clock buttons ({a.BodyPosition})");
            yield return Walk(a, new Vector3(0, 0, 124), 3);
            yield return new WaitForSeconds(2.5f);
            Check(a.BodyPosition.z > 132 && a.BodyPosition.y > -.3f && !a.Pawn.Floating, $"Pond launch lands on the far platform ({a.BodyPosition})");
            yield return Walk(a, new Vector3(0, 0, 145), 8);
            Check(walkOK && game.CheckpointOf(a) == 6, "Connected red course reaches promotion checkpoint");
            yield return Capture("promotion");

            // Independent fixtures keep a route failure from hiding mission regressions.
            game.ResetRound(); a.Respawn(new Vector3(0, 0, 96)); yield return new WaitForSeconds(.5f);
            Check(game.CheckpointOf(a) == 4 && game.CheckpointOf(ally) == 0, "Personal checkpoints do not advance other players");
            a.Respawn(new Vector3(20, -1.7f, 96)); yield return new WaitForSeconds(.7f);
            Check(a.Pawn.Floating, "Off-course water floats the pawn");
            yield return new WaitForSeconds(3.7f); Check(a.Pawn.Floating, "Water does not return early");
            yield return new WaitForSeconds(1.2f);
            Check(!a.Pawn.Floating && Mathf.Abs(a.BodyPosition.z - 96) < 2, "Five-second water returns to personal checkpoint");
            a.Respawn(new Vector3(-3, .04f, 155)); yield return new WaitForSeconds(2);
            Check(a.Piece == KingRushPiece.Knight && game.pads[0].Claimed, "Real course pad promotes after standing");
            a.Respawn(new Vector3(-3, 0, 174)); yield return new WaitForSeconds(.4f);
            Check(a.Section == KingRushSection.Blue1 && a.AbilitiesEnabled && game.Mission.Started && !ally.AbilitiesEnabled, "First arena entry starts timer and personal ability zone");
            a.SetInput(new PawnInput { ability = true }); yield return new WaitForSeconds(.2f);
            Check(a.Pawn.Hook == HookPhase.None, "Unimplemented knight ability does not leak to hook");

            game.ArrangeMission(); yield return new WaitForSeconds(.5f);
            Check(game.Mission.Planks(0) == 0 && game.Mission.Started, "F4 brings real dummies without granting points");
            yield return Capture("arena");
            game.CameraRig.enabled = false;
            game.CameraRig.Cam.transform.position = new Vector3(-10, 3.8f, 187);
            game.CameraRig.Cam.transform.LookAt(new Vector3(-10, 3.8f, 192));
            yield return Capture("box-sign"); game.CameraRig.enabled = true;
            // Prove that the shared grab/jump/throw can clear the authored 0.8 m rim.
            a.Respawn(new Vector3(-10, 0, 183.3f)); enemy.Respawn(new Vector3(-10, 0, 184));
            enemy.Pawn.Teleport(enemy.BodyPosition, Vector3.back);
            yield return new WaitForSeconds(.7f);
            for (int i = 0; i < 168; i++)
            { a.SetInput(new PawnInput { grab = true, move = Vector3.forward * .25f }); yield return new WaitForFixedUpdate(); }
            Check(enemy.Pawn.BeingHeld, "Real grip on enemy next to capture rim");
            for (int i = 0; i < 300 && enemy.BodyPosition.z < 187.3f && !enemy.Captured; i++)
            { a.SetInput(new PawnInput { grab = true, move = Vector3.forward }); yield return new WaitForFixedUpdate(); }
            a.SetInput(new PawnInput { grab = true, jump = true, move = Vector3.forward });
            yield return new WaitForSeconds(.18f);
            a.SetInput(new PawnInput { grab = true, shove = true, move = Vector3.forward });
            yield return new WaitForSeconds(.3f); a.SetInput(default);
            yield return new WaitForSeconds(1.5f);
            Check(game.Mission.Planks(0) == 1, $"Grab, approach and throw physically deposits enemy over rim ({enemy.BodyPosition})");
            game.ArrangeMission(); yield return new WaitForSeconds(.5f);
            // Hip outside the box must not count even if a hand/head overlaps its rim.
            enemy.Respawn(new Vector3(-10, .9f, 190)); yield return new WaitForFixedUpdate();
            Check(!game.boxes[0].Contains(enemy.BodyPosition) && game.Mission.Planks(0) == 0, "Above-rim body is not a capture");
            enemy.Respawn(new Vector3(-10, -.5f, 190)); enemy.Section = KingRushSection.Blue1;
            yield return new WaitForSeconds(.2f);
            Check(game.Mission.Planks(0) == 1 && enemy.Captured && !enemy.CountsAsBody && enemy.BodyPosition.y > 6, "Enemy hip capture scores once and moves to shelf in Update");
            enemy.SetInput(new PawnInput { move = Vector3.forward, ability = true }); game.Respawn(enemy);
            yield return new WaitForSeconds(2.3f);
            Check(enemy.Captured && game.Mission.Planks(0) == 1, "Movement and R cannot skip capture delay or duplicate points");
            yield return new WaitForSeconds(.7f);
            Check(!enemy.Captured && !game.Mission.Detained(enemy.Id) && enemy.BodyPosition.z < 177 && enemy.BodyPosition.y < 2, "Enemy returns to arena entry after three seconds");
            ally.Respawn(new Vector3(-10, -.5f, 190)); ally.Section = KingRushSection.Blue1;
            yield return new WaitForSeconds(.2f); Check(ally.Captured && game.Mission.Planks(0) == 1, "Own ally falls into box without score");
            yield return new WaitForSeconds(2.1f); Check(!ally.Captured && ally.BodyPosition.z < 177, "Own ally returns after two seconds");
            var king = game.Players[11]; king.Respawn(new Vector3(-10, -.5f, 190)); king.Section = KingRushSection.Blue1;
            yield return new WaitForSeconds(.2f); Check(game.Mission.Planks(0) == 2 && king.Captured, "Enemy king is worth exactly one plank");
            for (int i = 7; i <= 10; i++)
            { game.Players[i].Respawn(new Vector3(-10, -.5f, 190)); game.Players[i].Section = KingRushSection.Blue1; yield return new WaitForSeconds(.12f); }
            Check(game.Mission.Planks(0) == 6 && game.gates[0].Open && !game.gates[1].Open, "Six real box deposits complete own bridge only");
            int count = 0; foreach (var plank in game.planks) if (plank.activeSelf) count++;
            Check(count == 6 && !Physics.GetIgnoreCollision(game.gates[0].barrier, enemy.BodyColliders[0]), "Six physical planks and opponent gate collision remain correct");
            a.Promote(KingRushPiece.Knight); a.Respawn(new Vector3(-5, 0, 193)); a.Section = KingRushSection.Blue1;
            yield return new WaitForSeconds(.5f); yield return Walk(a, new Vector3(-5, 0, 212), 9);
            Check(walkOK && a.Section == KingRushSection.Red2 && a.Piece == KingRushPiece.Pawn && !a.AbilitiesEnabled, "Walk completed bridge and own gate, demote on exit");
            yield return Capture("exit");
            game.Step(21); yield return new WaitForFixedUpdate();
            count = 0; foreach (var plank in game.planks) if (plank.activeSelf) count++;
            Check(game.gates[1].Open && count == 12 && game.Mission.Planks(1) == 0, "Twenty-second fallback supplies traversable bridge without fake points");
            game.ResetRound(); yield return new WaitForSeconds(.5f);
            Check(!game.Mission.Started && !game.gates[0].Open && !game.pads[0].Claimed && !enemy.Captured && game.CheckpointOf(a) == 0, "Reset clears mission, captures, checkpoints, pads and gates");
            bool finite = true; foreach (var p in game.Players) finite &= p.Pawn.IsFinite(); Check(finite, "All twelve ragdolls remain finite");
            Debug.Log($"[OpeningTest] RESULT {passed} passed / {failed} failed");
            if (failed == 0 && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-kingRushTest") >= 0)
            { UnityEngine.SceneManagement.SceneManager.LoadScene("KingRushPrototype"); yield break; }
            Application.Quit(failed == 0 ? 0 : 1);
        }
        IEnumerator Walk(KingRushPawn pawn, Vector3 target, float limit, bool jumpGaps = false)
        {
            float end = Time.time + limit, nextJump = 0; walkOK = false;
            while (Time.time < end)
            {
                Vector3 delta = target - pawn.BodyPosition; delta.y = 0;
                if (delta.magnitude < .5f) { walkOK = true; break; }
                bool jump = jumpGaps && pawn.Pawn.Grounded && Time.time > nextJump;
                if (jump) nextJump = Time.time + .85f;
                pawn.SetInput(new PawnInput { move = delta.normalized, jump = jump }); yield return new WaitForFixedUpdate();
            }
            pawn.SetInput(default);
        }
        IEnumerator Overview()
        {
            var rig = game.CameraRig; rig.enabled = false;
            var camera = rig.Cam; camera.transform.position = new Vector3(100, 115, 35); camera.transform.LookAt(new Vector3(0, 0, 100));
            yield return Capture("overview"); rig.enabled = true; yield return null;
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string dir = Path.Combine(Application.persistentDataPath, "KingRushOpeningTest"); Directory.CreateDirectory(dir);
            var rt = RenderTexture.GetTemporary(1280, 720, 24); var camera = game.CameraRig.Cam;
            var previous = camera.targetTexture; var active = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
            camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Destroy(image);
            if (name != "arena") yield break;
            var settings = GetComponent<UIDocument>().panelSettings;
            var ui = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32); ui.Create(); settings.targetTexture = ui;
            yield return null; yield return new WaitForEndOfFrame(); active = RenderTexture.active; RenderTexture.active = ui;
            image = new Texture2D(1280, 720, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(dir, "hud.png"), image.EncodeToPNG());
            RenderTexture.active = active; settings.targetTexture = null; Destroy(image); ui.Release(); Destroy(ui);
        }
    }
}
