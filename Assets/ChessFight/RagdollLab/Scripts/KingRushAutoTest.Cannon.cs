using System.Collections;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class KingRushAutoTest
    {
        Vector3 AimDistance(float distance) => new Vector3(0, -(game.Local.Pawn.standHeight + .8f), distance).normalized;
        IEnumerator Catch(bool ally)
        {
            game.ArrangeCannonTest(ally);
            yield return new WaitForSeconds(.7f);
            for (int i = 0; i < 168; i++)
            {
                game.Local.SetInput(new PawnInput { grab = true, move = Vector3.forward * .25f, aim = AimDistance(10) });
                yield return new WaitForFixedUpdate();
            }
            game.Local.SetInput(new PawnInput { grab = true, aim = AimDistance(10) });
        }
        IEnumerator TestCannon()
        {
            var rook = game.Players[0]; var ally = game.Players[1]; var enemy = game.Players[6];
            game.ArrangeCannonTest(); yield return new WaitForSeconds(.4f);
            Check(game.CountBlueBodies(0) == 1 && game.CountBlueBodies(1) == 1, "Cannon lane bodies are included in blue-area HUD counts");
            int shots = rook.CannonShots;
            rook.SetInput(new PawnInput { ability = true, aim = AimDistance(10) }); yield return new WaitForSeconds(.7f);
            Check(rook.CannonShots == shots && !rook.AbilityActive, "Rook E without a real grip cannot fire");
            foreach (int distance in new[] {5, 10, 20})
            {
                yield return Catch(true);
                Check(ally.Pawn.BeingHeld, "Real hand grip for allied " + distance + "m shot");
                int landed = ally.CannonLandings; shots = rook.CannonShots;
                rook.SetInput(new PawnInput { grab = true, ability = true, aim = AimDistance(distance) });
                yield return new WaitForSeconds(.25f);
                Check(rook.CannonAiming && !ally.CannonFlight && rook.CannonShots == shots, "Cannon telegraphs before launch at " + distance + "m");
                if (distance == 10) yield return Capture("cannon-aim");
                Vector3 marker = rook.CannonLanding;
                for (int i = 0; i < 55; i++)
                {
                    if (rook.CannonAiming) marker = rook.CannonLanding;
                    rook.SetInput(new PawnInput { grab = true, aim = AimDistance(distance) }); yield return new WaitForFixedUpdate();
                }
                Check(rook.CannonShots == shots + 1 && ally.CannonFlight && !ally.CountsAsBody, "Cannon launches once and excludes airborne body at " + distance + "m");
                Check(Physics.GetIgnoreCollision(rook.BodyColliders[0], ally.BodyColliders[0]), "Brief muzzle clearance is scoped to the launching pair");
                for (int i = 0; i < 360 && ally.CannonLandings == landed; i++)
                {
                    if (i % 24 == 0) Debug.Log($"[CannonTrace] range={distance} t={i / 120f:0.00} feet={ally.LaunchOrigin} v={ally.Pawn.Hips.linearVelocity} ground={ally.Pawn.Grounded} launched={ally.Pawn.Launched} marker={marker}");
                    yield return new WaitForFixedUpdate();
                }
                float error = Vector3.Distance(new Vector3(marker.x, 0, marker.z), new Vector3(ally.LastCannonLanding.x, 0, ally.LastCannonLanding.z));
                Check(ally.CannonLandings == landed + 1 && error <= 1,
                    $"Ally lands within 1m at {distance}m (error={error:0.00}, marker={marker}, actual={ally.LastCannonLanding}, state={ally.Pawn.State}, tilt={ally.Pawn.HipsTilt:0})");
                Check(ally.Pawn.State == PawnState.Active && ally.Pawn.HipsTilt < 35, "Allied feet-first landing at " + distance + "m");
                Check(!Physics.GetIgnoreCollision(rook.BodyColliders[0], ally.BodyColliders[0]), "Launcher collision is restored after clearance");
                Check(rook.CooldownSeconds > 4 && rook.CooldownSeconds <= 8, "Rook starts eight-second cooldown after successful shot");
            }
            yield return Catch(false); Check(enemy.Pawn.BeingHeld, "Real enemy grip");
            int enemyLandings = enemy.CannonLandings;
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = AimDistance(10) });
            yield return new WaitForSeconds(.75f);
            Check(enemy.CannonFlight, "Enemy flies before landing knockdown");
            for (int i = 0; i < 360 && enemy.CannonLandings == enemyLandings; i++) yield return new WaitForFixedUpdate();
            Check(enemy.CannonLandings == enemyLandings + 1 && enemy.Pawn.State == PawnState.Ragdoll, "Enemy knocked down at actual landing");
            yield return new WaitForSeconds(.7f);
            Check(enemy.Pawn.State == PawnState.Ragdoll, "Enemy remains down for first 0.7 seconds after landing");
            yield return new WaitForSeconds(2.5f);
            Check(enemy.Pawn.State == PawnState.Active, "Enemy eventually recovers without a stuck flight state");

            yield return Catch(false); shots = rook.CannonShots;
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = AimDistance(10) }); yield return new WaitForSeconds(.2f);
            rook.SetInput(default); yield return new WaitForSeconds(.6f);
            Check(rook.CannonShots == shots && !rook.CannonAiming, "Releasing RMB cancels windup");
            yield return Catch(false); shots = rook.CannonShots;
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = AimDistance(10) }); yield return new WaitForSeconds(.2f);
            rook.Pawn.TakeHit(Vector3.zero, 1, 0, true); yield return new WaitForSeconds(.6f);
            Check(rook.CannonShots == shots && !rook.CannonAiming, "Hitting the rook cancels windup");

            yield return Catch(false); shots = rook.CannonShots;
            for (int i = 0; i < 6; i++) { enemy.SetInput(new PawnInput { shove = true }); yield return new WaitForSeconds(.12f); }
            float progress = enemy.Pawn.EscapeProgress;
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = AimDistance(10) });
            yield return new WaitForFixedUpdate();
            Check(progress > .4f && enemy.Pawn.EscapeProgress >= progress - .01f && rook.CannonAiming, "Starting cannon preserves the existing struggle meter");
            for (int i = 0; i < 4; i++) { enemy.SetInput(new PawnInput { shove = true }); yield return new WaitForSeconds(.1f); }
            yield return new WaitForSeconds(.3f);
            Check(rook.CannonShots == shots && !rook.CannonAiming && !enemy.Pawn.BeingHeld, "Real mashing escapes during aim and prevents launch");

            yield return Catch(false); shots = rook.CannonShots;
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = AimDistance(10) }); yield return new WaitForSeconds(.15f);
            Check(rook.CannonAiming, "Zone cancellation begins with a real pending cannon shot");
            rook.AbilitiesEnabled = false; yield return new WaitForSeconds(.7f);
            Check(rook.CannonShots == shots && !rook.CannonAiming, "Ability-zone exit cancels without a delayed shot on reentry");

            yield return Catch(false); shots = rook.CannonShots;
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = AimDistance(10) }); yield return new WaitForSeconds(.15f);
            game.Respawn(rook); yield return new WaitForSeconds(.7f);
            Check(rook.CannonShots == shots && !rook.CannonAiming && !enemy.Pawn.BeingHeld, "Respawning the rook releases its pending capture");

            yield return Catch(true); shots = rook.CannonShots;
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = AimDistance(5) }); yield return new WaitForSeconds(.2f);
            Vector3 originalAim = rook.CannonLanding;
            Vector3 turned = Quaternion.AngleAxis(20, Vector3.up) * AimDistance(5);
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = turned }); yield return new WaitForSeconds(.15f);
            Check(rook.CannonAiming && rook.CannonLanding.x > originalAim.x + 1, "Camera turn updates the landing marker during windup");
            yield return new WaitForSeconds(.3f);
            Check(rook.CannonShots == shots + 1, "Repeated E does not restart or duplicate the 0.6-second windup");
            shots = rook.CannonShots;
            ally.Respawn(rook.LaunchOrigin + Vector3.forward * .7f);
            for (int i = 0; i < 100; i++)
            { rook.SetInput(new PawnInput { grab = true, move = Vector3.forward * .25f, aim = AimDistance(10) }); yield return new WaitForFixedUpdate(); }
            Check(ally.Pawn.BeingHeld && rook.CooldownSeconds > 0, "Cooldown retest obtains another real grip without resetting the round");
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = AimDistance(10) }); yield return new WaitForSeconds(.7f);
            Check(rook.CannonShots == shots && !rook.CannonAiming, "Cooldown blocks a second shot even with a valid grip");
            game.ResetRound(); yield return new WaitForFixedUpdate();
            Check(!ally.CannonFlight && !rook.CannonAiming && rook.CooldownSeconds == 0, "Reset clears flight, aim and cooldown");

            yield return Catch(true); shots = rook.CannonShots;
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = Vector3.right }); yield return new WaitForSeconds(.8f);
            Check(rook.CannonShots == shots && rook.CooldownSeconds == 0, "No landing surface means no shot or spent cooldown");

            yield return Catch(true);
            var wall = new GameObject("Cannon collision regression wall");
            wall.transform.position = new Vector3(22, 4, 14);
            wall.AddComponent<BoxCollider>().size = new Vector3(10, 8, .5f);
            shots = rook.CannonShots;
            rook.SetInput(new PawnInput { grab = true, ability = true, aim = AimDistance(20) });
            float furthest = ally.BodyPosition.z;
            for (int i = 0; i < 360; i++)
            { furthest = Mathf.Max(furthest, ally.BodyPosition.z); yield return new WaitForFixedUpdate(); }
            Check(rook.CannonShots == shots + 1 && furthest < 14 && ally.Pawn.IsFinite() && !ally.CannonFlight,
                $"Cannon retains physical wall collision and releases flight lock (furthest={furthest:0.00})");
            Destroy(wall);

            game.ArrangeCannonTest(true); yield return new WaitForSeconds(.5f);
            ally.Pawn.Knockdown("Cannon downed-ally regression", 1); yield return new WaitForSeconds(.5f);
            int recoveredLandings = ally.CannonLandings;
            Vector3 destination = ally.LaunchOrigin + Vector3.forward * 10; destination.y = 0;
            bool launched = ally.LaunchFromCannon(ChessFight.Gameplay.LaunchPad.ArcVelocity(ally.LaunchOrigin, destination, 3, -Physics.gravity.y), false);
            for (int i = 0; i < 360 && ally.CannonLandings == recoveredLandings; i++) yield return new WaitForFixedUpdate();
            Check(launched && ally.CannonLandings > recoveredLandings && ally.Pawn.State == PawnState.Active && ally.Pawn.HipsTilt < 35,
                "A previously downed ally also recovers a feet-first cannon landing");
            ally.SetInput(new PawnInput { move = Vector3.forward }); float startZ = ally.BodyPosition.z;
            yield return new WaitForSeconds(.7f); ally.SetInput(default);
            Check(ally.BodyPosition.z > startZ + .5f, "Walking drive is restored after cannon flight");
            game.ResetRound();
        }
    }
}
