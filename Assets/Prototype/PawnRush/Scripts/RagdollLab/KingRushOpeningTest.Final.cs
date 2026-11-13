using System.Collections;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class KingRushOpeningTest
    {
        IEnumerator SkyFlow()
        {
            game.ArrangeFinal(false); yield return new WaitForSeconds(.5f); var p = game.Local;
            game.CameraRig.enabled = false; game.CameraRig.Cam.transform.position = new Vector3(90, 95, 470);
            game.CameraRig.Cam.transform.LookAt(new Vector3(0, 23, 540)); yield return Capture("sky-overview"); game.CameraRig.enabled = true;
            yield return Walk(p, new Vector3(4, 3, 452), 6);
            var route = new[] { new Vector3(20, 7.5f, 452), new Vector3(20, 12, 480), new Vector3(4, 16.5f, 480), new Vector3(4, 21, 452) };
            bool steps = true;
            foreach (var point in route) { yield return Walk(p, point, 14); steps &= walkOK && p.BodyPosition.y > point.y - .5f; Debug.Log($"[SkyRoute] target={point} actual={p.BodyPosition} walk={walkOK}"); }
            Check(steps, $"Climb all four turning stair flights with actual inputs ({p.BodyPosition})");
            yield return Walk(p, new Vector3(0, 21, 453), 6);
            yield return Walk(p, new Vector3(0, 21, 540), 35);
            Check(walkOK && game.CheckpointOf(p) == 13, $"Long safe route joins at sky checkpoint ({p.BodyPosition})");
            // Independently test the shortcut; do not describe this placement as a continuous run.
            game.ArrangeFinal(false); p.Respawn(new Vector3(-8, 3, 452)); yield return new WaitForSeconds(.4f);
            yield return Walk(p, new Vector3(-8, 21, 540), 38);
            Check(walkOK && p.BodyPosition.y > 20 && game.CheckpointOf(p) == 13, $"Walk narrow windy shortcut without changing shared physics ({p.BodyPosition})");
            // Each following obstacle has its own checkpoint fixture after any earlier failed traversal.
            game.ArrangeFinal(false); game.NextCheckpoint();
            p.Respawn(new Vector3(-1.5f, 21, 543)); yield return new WaitForSeconds(.4f);
            float until = Time.time + 9;
            while (Time.time < until && (KingRushSkyMotion.Cycle(ObstacleClock.Now, 4) < .4f || KingRushSkyMotion.Cycle(ObstacleClock.Now, 4) > .9f)) yield return new WaitForFixedUpdate();
            yield return Walk(p, new Vector3(-1.5f, 21, 572), 12, true);
            Check(walkOK && p.BodyPosition.y > 20 && game.CheckpointOf(p) == 14, $"Cross timed returning tiles by reading their phase ({p.BodyPosition})");
            yield return Capture("sky-tiles");
            game.ArrangeFinal(false); p.Respawn(new Vector3(0, 21, 573)); yield return new WaitForSeconds(.4f);
            until = Time.time + 9;
            while (Time.time < until && (KingRushSkyMotion.Cycle(ObstacleClock.Now, 0) < 3.6f || KingRushSkyMotion.Cycle(ObstacleClock.Now, 0) > 3.9f)) yield return new WaitForFixedUpdate();
            yield return Walk(p, new Vector3(0, 21, 591), 8);
            Check(walkOK && p.BodyPosition.y > 20, $"Pass giant hand after shadow and sweep ({p.BodyPosition})");
            yield return Walk(p, new Vector3(0, 40, 635), 25);
            Check(walkOK && p.BodyPosition.y > 39 && game.CheckpointOf(p) == 15, $"Final stair reaches four promotion slots at forty metres ({p.BodyPosition})");
            yield return Capture("final-promotion");
        }
        IEnumerator FinalFlow()
        {
            game.ArrangeFinal(true); yield return new WaitForSeconds(.5f); var king = game.Local; var enemy = game.Players[6];
            Check(game.Final.Started && game.Final.Progress(0) == 0 && game.Final.Progress(1) == 0, "Both actual kings start finale, not the rally itself");
            game.CameraRig.enabled = false; game.CameraRig.Cam.transform.position = new Vector3(32, 69, 642);
            game.CameraRig.Cam.transform.LookAt(new Vector3(0, 40, 674)); yield return Capture("final-overview"); game.CameraRig.enabled = true;
            // A live opposing body contests the dais while the king physically walks to the throne.
            enemy.Respawn(new Vector3(2, 41, 674));
            yield return Walk(king, new Vector3(0, 40, 668), 8);
            yield return Walk(king, new Vector3(0, 41, 674), 7, true);
            float settle = Time.time + 3;
            while (Time.time < settle && (!king.Pawn.Grounded || game.Final.Seated != 0)) yield return new WaitForFixedUpdate();
            Check(walkOK && game.finalBoard.throne.Contains(king.BodyPosition) && game.Final.Seated == 0 && game.Final.Progress(0) == 0,
                $"King climbs dais and occupies throne, enemy pauses gauge ({king.BodyPosition})");
            yield return new WaitForSeconds(.7f);
            enemy.Respawn(new Vector3(8, 40, 669)); yield return new WaitForSeconds(1.5f);
            double earned = game.Final.Progress(0); Check(earned > .1 && earned < .35, $"Clear dais accumulates crown time ({earned:0.000}, {king.Pawn.State}, ground {king.Pawn.Grounded}, {king.BodyPosition})");
            enemy.Respawn(new Vector3(2, 41, 674)); yield return new WaitForSeconds(.8f);
            Check(System.Math.Abs(game.Final.Progress(0) - earned) < .04, "Enemy return pauses without erasing earned time");
            king.SetInput(new PawnInput { ability = true }); yield return new WaitForSeconds(.7f);
            Check(king.Checks == 1 && game.finalBoard.throne.Contains(king.BodyPosition), "Seated king can Check without pinning the body");
            enemy.Respawn(new Vector3(8, 40, 669)); yield return new WaitForSeconds(8);
            Check(game.Final.Ended && game.Final.Winner == 0 && game.finalBoard.crown.gameObject.activeSelf, "Eight clear seconds end the match with winner crown");
            yield return Capture("final-winner");
            game.ArrangeFinal(true); yield return new WaitForSeconds(.5f);
            var p = game.Players[0]; p.Respawn(new Vector3(18, 31, 674)); yield return null;
            Check(game.ReentryPending(p) && p.Captured, "Fall schedules reentry outside physics callbacks");
            game.Respawn(p); yield return new WaitForSeconds(4.5f);
            Check(game.Reentries == 0 && game.ReentryPending(p), "R cannot bypass the five-second fall penalty");
            bool flew = false; float end = Time.time + 1.2f;
            while (Time.time < end) { flew |= p.CannonFlight && !p.Pawn.Grounded; yield return new WaitForFixedUpdate(); }
            Check(game.Reentries == 1 && flew, $"Tower launches the fallen pawn after five seconds (launches {game.Reentries}, airborne {flew})");
            yield return new WaitForSeconds(3);
            Check(p.Pawn.Grounded && !p.CannonFlight && game.finalBoard.arena.Contains(p.BodyPosition), $"Reentry physically lands on surviving board ({p.BodyPosition})");
            game.Step(47 - (float)game.Final.Elapsed(game.Match.Now)); yield return new WaitForFixedUpdate();
            Check(game.finalBoard.Warnings == 30 && game.finalBoard.Remaining == 64, "Thirty white tiles warn three seconds before collapse");
            game.Step(3); yield return new WaitForFixedUpdate();
            Check(game.finalBoard.Remaining == 34, "Fifty seconds removes white cells except the dais");
            yield return Capture("final-collapse");
            game.Step(50); yield return new WaitForFixedUpdate();
            Check(game.finalBoard.Remaining == 10, "Hundred seconds leaves six black cells and four dais cells");
            Vector3 aim = game.finalBoard.ReentryTarget(new Vector3(18, 40, 692), game.Final, game.Match.Now);
            Check(Mathf.Abs(aim.x) <= 4.6f && Mathf.Abs(aim.z - 674) <= 4.6f, "Late reentry target avoids already fallen outer cells");
            // Pending falls are intentionally cleared by reset rather than direct body teleports.
            game.ArrangeFinal(true); yield return new WaitForSeconds(.5f);
            foreach (var member in game.Players) member.Respawn(new Vector3(member.Team == 0 ? -1.8f : 1.8f, 41, 672 + (member.Id % 5) * .8f));
            game.Step(140); yield return new WaitForFixedUpdate();
            Check(game.Final.Sudden(game.Match.Now) && game.finalBoard.Remaining == 4 && !game.finalBoard.approaches.activeSelf && !game.Final.Ended, "Sudden death leaves dais alone and keeps live kings playing");
            var victim = game.Players[11]; victim.Respawn(new Vector3(12, 31, 674)); yield return new WaitForFixedUpdate(); yield return null;
            Check(game.Final.Ended && game.Final.Winner == 0, "King fall during sudden death immediately loses for that team");
            game.ResetRound(); yield return new WaitForFixedUpdate();
            Check(!game.Final.Started && !game.Final.Ended && game.finalBoard.Remaining == 64 && !game.finalBoard.crown.gameObject.activeSelf && game.rallies.Length == 3,
                "Reset restores final board, result, all three rallies and ordinary inputs");
        }
    }
}
