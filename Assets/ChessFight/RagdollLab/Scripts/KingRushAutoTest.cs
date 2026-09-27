using System;
using System.Collections;
using System.IO;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.RagdollLab
{
    // Opt-in integration evidence, not a claim of human-tested game feel.
    public sealed partial class KingRushAutoTest : MonoBehaviour
    {
        int passed, failed;
        KingRushPrototype game;
        void Check(bool value, string name)
        { if (value) { passed++; Debug.Log("[KingRushTest] PASS " + name); } else { failed++; Debug.LogError("[KingRushTest] FAIL " + name); } }
        IEnumerator Start()
        {
            game = GetComponent<KingRushPrototype>();
            yield return new WaitForSeconds(1.5f);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-kingRushCannonOnly") >= 0)
            {
                yield return TestCannon();
                Debug.Log($"[KingRushTest] RESULT {passed} passed / {failed} failed (cannon only)");
                Application.Quit(failed == 0 ? 0 : 1); yield break;
            }
            Check(game.Players.Count == 12 && game.Players[5].FixedKing && game.Players[11].FixedKing, "Six per team with one fixed king each");
            bool same = true;
            foreach (var p in game.Players)
                for (int i = 0; i < p.Pawn.bodies.Length; i++) same &= p.Pawn.bodies[i].mass == game.Local.Pawn.bodies[i].mass && p.Pawn.tuning == game.tuning;
            Check(same, "All pieces use identical pawn masses and shared tuning");
            var state = GetComponent<UIDocument>().rootVisualElement.Q<Label>("kr-state");
            Check(state != null && state.worldBound.width > 100 && state.text.Contains("폰"), "Korean mechanics HUD has visible bounds");
            yield return Capture("overview");
            var a = game.Players[0]; var b = game.Players[6]; var king = game.Players[5];
            a.SetInput(new PawnInput { ability = true }, true);
            yield return new WaitForSeconds(.1f);
            Check(!a.AbilitiesEnabled && !a.AbilityActive && a.Pawn.Hook == HookPhase.None, "Red zone E never draws the Queen Hill hook");
            a.SetInput(new PawnInput { shove = true });
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(a.Pawn.Diving, "LMB still uses the shared tackle");
            game.ResetRound(); yield return new WaitForSeconds(.7f);

            Place(a, new Vector3(-3, .05f, 1));
            yield return new WaitForSeconds(1f);
            Check(a.Piece == KingRushPiece.Pawn && game.Pads[0].Charge.Seconds > .5, "Promotion requires full 1.5 seconds on the real pad");
            yield return new WaitForSeconds(.8f);
            Check(a.Piece == KingRushPiece.Knight && game.Pads[0].Claimed, "One pawn promotes once despite many body colliders");
            Check(!a.Promote(KingRushPiece.Queen) && !king.Promote(KingRushPiece.Queen), "Promoted pawn and fixed king cannot steal another piece");
            yield return Capture("promoted");
            game.ResetRound(); yield return new WaitForSeconds(.6f);
            Place(a, new Vector3(-3.6f, .05f, 1)); Place(b, new Vector3(-2.4f, .05f, 1));
            yield return new WaitForSeconds(1.8f);
            Check(game.Pads[0].Charge.Contested && !game.Pads[0].Claimed, "Two opposing pawns pause the physical pad");
            Place(b, new Vector3(5, 0, -4));
            yield return new WaitForSeconds(1.8f);
            Check(a.Piece == KingRushPiece.Knight, "Remaining pawn finishes after contest ends");

            Place(a, new Vector3(-4, 0, 14)); yield return new WaitForSeconds(.4f);
            Check(a.AbilitiesEnabled && a.Section == KingRushSection.Blue1, "Blue entry enables mode ability context per character");
            Check(!game.Players[1].AbilitiesEnabled && game.Players[1].Section == KingRushSection.Red1, "Late arrivals remain in their own section");
            var gate = game.Gates[0];
            Check(!Physics.GetIgnoreCollision(gate.barrier, a.BodyColliders[0]), "Unfinished team cannot walk through its barrier");
            game.Match.Complete(0, 0); game.Step(.01f);
            Check(Physics.GetIgnoreCollision(gate.barrier, a.BodyColliders[0]) && !Physics.GetIgnoreCollision(gate.barrier, b.BodyColliders[0]), "Only completed team passes its physical gate");
            Check(!game.Gates[1].Open && game.Match.Rules.OpensAt(0, 1) - game.Match.Now > 19, "Other team's door waits twenty seconds");
            Place(a, new Vector3(-4, 0, 19)); game.Step(.01f);
            Check(a.Piece == KingRushPiece.Pawn && a.Section == KingRushSection.Red2 && !a.AbilitiesEnabled, "Crossing the exit demotes and disables ability");
            Place(king, new Vector3(-4, 0, 14)); game.Step(.01f);
            Place(king, new Vector3(-4, 0, 19)); game.Step(.01f);
            Check(king.FixedKing && king.Piece == KingRushPiece.King, "Fixed king survives demotion gate");
            game.ResetRound(); yield return new WaitForSeconds(.6f);
            Place(king, new Vector3(0, 0, 10)); Place(a, new Vector3(-2, 0, 10)); Place(b, new Vector3(2, 0, 10));
            var far = game.Players[7]; Place(far, new Vector3(5, 0, 10));
            game.Select(5); yield return new WaitForSeconds(1f);
            int ah = a.Pawn.Hits, bh = b.Pawn.Hits, fh = far.Pawn.Hits;
            king.SetInput(new PawnInput { ability = true }, true);
            yield return new WaitForSeconds(.25f);
            Check(king.AbilityActive && king.Checks == 0 && a.Pawn.Hits == ah, "Check has a half-second warning before any hit");
            yield return Capture("check-warning");
            yield return new WaitForSeconds(.4f);
            Check(king.Checks == 1 && a.Pawn.Hits == ah + 1 && b.Pawn.Hits == bh + 1 && far.Pawn.Hits == fh,
                "Check hits nearby allies and enemies once, not distant pawns");
            Check(a.Pawn.State == PawnState.Ragdoll && b.Pawn.State == PawnState.Ragdoll && king.Cooldown01 > .9f, "Check knocks down and starts twelve-second cooldown");
            king.SetInput(new PawnInput { ability = true }); yield return new WaitForSeconds(.7f);
            Check(king.Checks == 1, "Cooldown blocks a second Check");
            game.ResetRound(); yield return new WaitForSeconds(.6f);
            Place(king, new Vector3(0, 0, 10)); yield return new WaitForSeconds(.3f);
            king.SetInput(new PawnInput { ability = true }); yield return new WaitForSeconds(.15f);
            Place(king, new Vector3(0, 0, 2)); yield return new WaitForSeconds(.6f);
            Check(!king.AbilityActive && king.Checks == 0 && !king.AbilitiesEnabled, "Leaving blue cancels the pending ability");

            game.ResetRound(); yield return new WaitForSeconds(.5f);
            Check(a.Promote(KingRushPiece.Queen), "Queen is a legal promotion in this mode");
            Place(a, new Vector3(-6, 0, 10)); yield return new WaitForSeconds(.4f);
            Place(a, new Vector3(-13, -1.7f, 10)); yield return new WaitForSeconds(.8f);
            Check(a.Pawn.Floating && !a.CountsAsBody && a.Piece == KingRushPiece.Queen, "Water floats a promoted pawn without losing its identity");
            yield return new WaitForSeconds(3.5f);
            Check(a.Pawn.Floating, "Water does not respawn early");
            yield return new WaitForSeconds(1.4f);
            Check(!a.Pawn.Floating && a.Piece == KingRushPiece.Queen && a.BodyPosition.y > 0 && a.Section == KingRushSection.Blue1, "Five-second respawn retains promotion at blue entry");
            Place(a, new Vector3(0, 0, 10)); yield return new WaitForSeconds(.3f);
            a.Pawn.Launch(Vector3.up * 5); yield return new WaitForFixedUpdate();
            Check(!a.CountsAsBody, "Launched bodies do not count toward missions");
            game.ResetRound(); yield return new WaitForSeconds(.5f);
            Check(!game.Gates[0].Open && !game.Pads[0].Claimed && !Physics.GetIgnoreCollision(game.Gates[0].barrier, a.BodyColliders[0]), "Reset restores pad claims and gate collision pairs");
            game.ArrangeAbilityTest(); yield return new WaitForSeconds(.8f);
            Check(game.Local == king && king.AbilitiesEnabled && BodyCount.InBounds(game.Match.Characters,
                new Bounds(game.Blue.transform.position, game.Blue.size)) == 6, "F4 arranges a king, two allied and three enemy dummies");
            game.ResetRound(); yield return new WaitForSeconds(.5f);
            Place(king, new Vector3(-3, .05f, 1)); yield return new WaitForSeconds(1.8f);
            Check(game.Pads[0].Charge.Seconds == 0 && !game.Pads[0].Claimed, "King alone never consumes a promotion pad");
            Place(a, new Vector3(0, 0, 9)); yield return new WaitForSeconds(.4f);
            a.SetInput(new PawnInput { ability = true, ability2 = true }, true); yield return new WaitForSeconds(.3f);
            Check(a.AbilitiesEnabled && a.Pawn.Hook == HookPhase.None && !a.AbilityActive, "Unimplemented pawn E and Q never leak to the hook even in blue");
            Place(a, new Vector3(-4, 0, 16.5f)); a.Section = KingRushSection.Blue1;
            game.Match.Complete(0, 0); yield return new WaitForFixedUpdate();
            for (int i = 0; i < 85; i++) { a.SetInput(new PawnInput { move = Vector3.forward }); yield return new WaitForFixedUpdate(); }
            a.SetInput(default);
            Check(a.BodyPosition.z > 18.5f && a.Section == KingRushSection.Red2,
                $"Physical walking crosses the opened gate and updates section (z={a.BodyPosition.z:0.00})");
            game.ResetRound(); yield return new WaitForSeconds(.5f);
            bool finite = true; foreach (var p in game.Players) finite &= p.Pawn.IsFinite();
            Check(finite, "Twelve physical pawns remain finite after ability, water and reset");
            yield return TestCannon();
            Debug.Log($"[KingRushTest] RESULT {passed} passed / {failed} failed");
            Application.Quit(failed == 0 ? 0 : 1);
        }
        static void Place(KingRushPawn pawn, Vector3 ground) => pawn.Respawn(ground);
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string dir = Path.Combine(Application.persistentDataPath, "KingRushTest"); Directory.CreateDirectory(dir);
            var rt = RenderTexture.GetTemporary(1280, 720, 24);
            var camera = game.CameraRig.Cam; var previous = camera.targetTexture; var active = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
            camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Destroy(image);
            // Separate UI render catches layout issues even in a hidden player window.
            if (name != "overview") yield break;
            var settings = GetComponent<UIDocument>().panelSettings;
            var ui = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32); ui.Create(); settings.targetTexture = ui;
            yield return null; yield return new WaitForEndOfFrame();
            active = RenderTexture.active; RenderTexture.active = ui;
            image = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(Path.Combine(dir, "hud.png"), image.EncodeToPNG());
            RenderTexture.active = active; settings.targetTexture = null; Destroy(image); ui.Release(); Destroy(ui);
        }
    }
}
