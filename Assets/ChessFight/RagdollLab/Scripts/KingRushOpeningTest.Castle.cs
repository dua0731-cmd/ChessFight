using System.Collections;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class KingRushOpeningTest
    {
        IEnumerator RallyFlow(int wave)
        {
            game.ArrangeRally(wave); yield return new WaitForSeconds(2.2f);
            var rally = game.rallies[wave]; var a = game.Local; var arrived = game.Players[1]; var king = game.Players[11];
            Check(rally.Rules.Claimed == wave + 2 && rally.Rules.StartedAt >= 0 && !rally.Rules.Released, $"Wave {wave}: actual pad claims start countdown");
            arrived.Section = wave == 0 ? KingRushSection.Red1 : KingRushSection.Red2;
            arrived.Respawn(new Vector3(-8, 0, rally.arrival.center.z));
            king.Respawn(new Vector3(20, -1.7f, 96));
            yield return new WaitForSeconds(.5f);
            Check(king.Pawn.Floating, $"Wave {wave}: late king is actually in water");
            // Keep the water reservation active at the countdown deadline, not merely a prior fixture.
            double until = rally.Rules.StartedAt + 10;
            while (game.Match.Now < until - .45) yield return new WaitForFixedUpdate();
            king.Respawn(new Vector3(20, -1.7f, 96));
            var detained = game.Players[2];
            if (wave == 1)
            {
                detained.Section = KingRushSection.Blue1; game.Mission.Begin(game.Match.Now);
                game.Mission.Deposit(detained.Id, detained.Team, 1, game.Match.Now); detained.SetCaptured();
            }
            yield return new WaitForSeconds(.15f);
            Vector3 arrivalBefore = arrived.BodyPosition;
            Check(!rally.Rules.Released && rally.wall.GetComponent<Collider>().enabled && king.BodyPosition.z < 120,
                $"Wave {wave}: wall and late players remain unchanged before ten seconds");
            yield return new WaitForSeconds(.55f);
            bool gathered = true;
            foreach (var p in game.Players) gathered &= rally.arrival.Contains(p.BodyPosition) && !p.Pawn.Floating && !p.Captured;
            Check(rally.Rules.Released && !rally.wall.GetComponent<Collider>().enabled && gathered && game.GatheredLast >= 8,
                $"Wave {wave}: both teams including wet/detained bodies gather before wall opens ({game.GatheredLast})");
            Check((arrived.BodyPosition - arrivalBefore).magnitude < .3f && a.Piece == (wave == 0 ? KingRushPiece.Knight : KingRushPiece.Rook) &&
                king.Piece == KingRushPiece.King && detained.Piece == KingRushPiece.Pawn, $"Wave {wave}: arrivals stay put; promotions/king preserved; late pawn stays pawn");
            yield return Capture(wave == 0 ? "rally-open" : "castle-rally");
            game.Players[3].Respawn(new Vector3(0, 0, rally.arrival.center.z - 9));
            yield return new WaitForSeconds(5.2f);
            Check(Mathf.Abs(game.Players[3].BodyPosition.z - (rally.arrival.center.z - 9)) < 1 && rally.arrival.Contains(king.BodyPosition) &&
                !game.Mission.Detained(detained.Id), $"Wave {wave}: no repeated gather or stale water/capture respawn");
        }
        IEnumerator CastleFlow()
        {
            game.ArrangeCastle(false); yield return new WaitForSeconds(.5f); var a = game.Local;
            game.CameraRig.enabled = false; game.CameraRig.Cam.transform.position = new Vector3(85, 95, 278);
            game.CameraRig.Cam.transform.LookAt(new Vector3(0, 0, 305)); yield return Capture("castle-overview"); game.CameraRig.enabled = true;
            yield return Walk(a, new Vector3(10, 0, 236), 7);
            KingRushCastleMotion ferry = null;
            foreach (var motion in game.GetComponentsInChildren<KingRushCastleMotion>())
                if (motion.kind == KingRushCastleMotion.Kind.Ferry) ferry = motion;
            float waitUntil = Time.time + 9;
            while (Time.time < waitUntil && ferry.transform.position.z > 243.6f) yield return new WaitForFixedUpdate();
            waitUntil = Time.time + 3;
            while (Time.time < waitUntil && (!a.Pawn.Riding || a.BodyPosition.z < 239))
            { a.SetInput(new PawnInput { move = Vector3.forward }); yield return new WaitForFixedUpdate(); }
            a.SetInput(default);
            Check(a.Pawn.Riding && !a.Pawn.Floating, $"Board the arriving ferry ({a.BodyPosition}; platform {ferry.transform.position}; {a.Pawn.State})");
            a.SetInput(default); waitUntil = Time.time + 9;
            while (Time.time < waitUntil && (ferry.transform.position.z < 249 || ferry.PointVelocity(a.BodyPosition).z < 0)) yield return new WaitForFixedUpdate();
            Check(a.Pawn.Riding && a.BodyPosition.z > 243, $"Ride ferry toward far landing ({a.BodyPosition}; platform {ferry.transform.position})");
            // Move to the bow while the ferry approaches, rather than waiting at its stern until it reverses.
            yield return Walk(a, new Vector3(10, 0, 262), 6, true);
            Check(walkOK && game.CheckpointOf(a) == 8, $"Lower branch: ride ferry across real moat ({a.BodyPosition})");
            yield return Walk(a, new Vector3(12.6f, 0, 275), 9, true);
            waitUntil = Time.time + 11;
            while (Time.time < waitUntil && (ObstacleClock.Now % 10 < 5.2 || ObstacleClock.Now % 10 > 6.2)) yield return new WaitForFixedUpdate();
            yield return Walk(a, new Vector3(10, 0, 303), 10, true);
            Check(walkOK, $"Lower branch: sliding wall and drawbridge ({a.BodyPosition})");
            yield return Walk(a, new Vector3(10, 0, 315), 6);
            yield return Walk(a, new Vector3(-10, 0, 343), 12);
            Check(walkOK && game.CheckpointOf(a) == 10, $"Walk shared X intersection ({a.BodyPosition})");
            yield return Walk(a, new Vector3(0, 0, 361), 15);
            Check(walkOK && game.CheckpointOf(a) == 11, $"Timed castle gate leads to second promotion ({a.BodyPosition})");
            // The upper route is a separate physical fixture, never counted as a continuous first-route traversal.
            game.ArrangeCastle(false);
            a.Respawn(new Vector3(-10, 0, 237)); yield return new WaitForSeconds(.5f);
            float end = Time.time + 9;
            while (Time.time < end && a.BodyPosition.z < 240)
            { a.SetInput(new PawnInput { move = Vector3.forward, grab = true }); yield return new WaitForFixedUpdate(); }
            a.SetInput(default);
            Check(a.BodyPosition.y > 3.5f && a.BodyPosition.z >= 240, $"Upper route: actual 3.5 m wall climb ({a.BodyPosition})");
            yield return Walk(a, new Vector3(-10, 3.5f, 283), 15);
            Check(walkOK && a.BodyPosition.y > 3.4f, $"Upper route temporary narrow bridge is traversable ({a.BodyPosition})");
            yield return Capture("castle-upper");
            yield return Walk(a, new Vector3(-7, 3.5f, 300), 12);
            yield return Walk(a, new Vector3(-10, 0, 315), 8);
            Check(walkOK && a.BodyPosition.y < 2, $"Upper route mace and descent reach crossing ({a.BodyPosition})");
            yield return RallyFlow(1);
            game.ArrangeCastle(true); yield return new WaitForSeconds(4);
            Check(game.Seesaw.Started && game.Seesaw.Angle > 15 && game.SeesawWeight > 0 && game.Seesaw.Access(0),
                $"Real counterweight bodies raise white exit ({game.Seesaw.Angle:0.0} deg, {game.SeesawWeight:0.0} m)");
            yield return Capture("seesaw");
            game.CameraRig.enabled = false; game.CameraRig.Cam.transform.position = new Vector3(36, 34, 383);
            game.CameraRig.Cam.transform.LookAt(new Vector3(0, 0, 413)); yield return Capture("seesaw-overview"); game.CameraRig.enabled = true;
            Check(Mathf.Abs(game.seesawBoard.Weight(new Vector3(0, .6f, 412))) < .3f && !game.seesawBoard.Supports(new Vector3(0, 6, 412)), "Neutral axis and airborne body are not counterweights");
            var probe = game.Players[10];
            probe.Respawn(new Vector3(8, 5, 415)); yield return new WaitForFixedUpdate();
            Check(!game.IsSeesawWeight(probe), "Actual airborne pawn is excluded from board weight");
            probe.Respawn(game.seesawBoard.transform.TransformPoint(new Vector3(8, .02f, 3)));
            yield return new WaitForSeconds(.5f);
            probe.HitReceiver.ApplyHit(Vector3.zero, 2, 0, true); yield return new WaitForSeconds(.4f);
            Check(probe.Pawn.State == PawnState.Ragdoll && game.IsSeesawWeight(probe), "Supported knocked-down pawn still weighs on the board");
            probe.Respawn(game.seesawBoard.transform.TransformPoint(new Vector3(9, .02f, 6)));
            yield return new WaitForSeconds(.4f);
            // Walk along neutral axis, then climb raised tiles diagonally toward the white ledge.
            yield return Walk(a, new Vector3(0, 0, 398), 4);
            yield return Walk(a, new Vector3(0, 0, 422), 9);
            Check(walkOK, $"Seesaw neutral-axis approach ({a.BodyPosition}, {game.Seesaw.Angle:0.0} deg)");
            yield return Walk(a, new Vector3(-10.4f, 3, 422.8f), 10);
            Check(walkOK && !a.Pawn.Floating, $"Seesaw uphill crossing ({a.BodyPosition}, {game.Seesaw.Angle:0.0} deg)");
            yield return Walk(a, new Vector3(-11, 3, 428), 7, true);
            // Walk ends on planar proximity: platform-assisted jumps can still be above the exit volume.
            // Require actual landing/crossing, without changing the shared moving-platform jump physics.
            end = Time.time + 4;
            while (Time.time < end && (!a.Pawn.Grounded || game.Seesaw.Count(0) != 1)) yield return new WaitForFixedUpdate();
            Check(walkOK && a.Pawn.Grounded && !a.Pawn.Floating && game.Seesaw.Count(0) == 1 && !game.gates[2].Open,
                $"Physically land on high ledge; one unique exit is not completion ({a.BodyPosition}; count {game.Seesaw.Count(0)})");
            yield return Capture("seesaw-exit");
            // Unique-body counting fixtures independently test completion and team filtering.
            for (int i = 0; i < 4; i++)
            { var p = game.Players[i]; p.Section = KingRushSection.Blue2; p.Respawn(new Vector3(-12, 3, 428)); yield return new WaitForSeconds(.1f); p.Respawn(new Vector3(-13 + i, 3, 430)); }
            Check(game.Seesaw.Count(0) == 4 && game.gates[2].Open && !game.gates[3].Open, "Four unique white exits open next-course door only for white");
            a.Respawn(new Vector3(-12, 3, 429)); a.Promote(KingRushPiece.Rook);
            yield return Walk(a, new Vector3(-12, 3, 436), 5);
            Check(walkOK && a.Section == KingRushSection.Red3 && a.Piece == KingRushPiece.Pawn, "Seesaw completion gate demotes on actual traversal");
            game.Step(21); yield return new WaitForFixedUpdate();
            Check(game.Seesaw.Bridges(game.Match.Now) && game.seesawExits[0].rescueBridge.activeSelf && game.seesawExits[1].rescueBridge.activeSelf && game.gates[3].Open,
                "Twenty-second relief deploys both physical ramps and opponent door");
            var enemy = game.Players[6]; enemy.Respawn(new Vector3(0, 0, 424)); enemy.Section = KingRushSection.Blue2;
            yield return new WaitForSeconds(.5f); yield return Walk(enemy, new Vector3(0, 0, 429), 4);
            Check(walkOK, $"Rescue center pier connects to neutral axis ({enemy.BodyPosition})");
            yield return Walk(enemy, new Vector3(12, 3, 429), 9);
            Check(walkOK && enemy.BodyPosition.y > 3, $"Rescue ramp reaches the ledge ({enemy.BodyPosition})");
            yield return Walk(enemy, new Vector3(12, 3, 436), 7);
            Check(walkOK && enemy.Section == KingRushSection.Red3, $"Physical rescue ramp works independently of tilt ({enemy.BodyPosition})");
        }
    }
}
