using ChessFight.Gameplay;
using UnityEngine;
using R = ChessFight.Network.RoyalVaultRules;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // Beat 0 and fork 1 of the royal vault: the forecourt (y 0), then the risky way up - two lanes of round coin towers, one
    // TowerStep higher a row, with the shared rest platform as row 4 - or the safe one: the gold-bar hurdle corridor west of
    // the towers (two counter-turning Spinner bars, then two castling bulkheads of pocket panels) and its 12 m stair. Both
    // arrive on J1 (y 4). The towers' cues (gold rims and lips), the hurdle bars' gold tops and the bulkheads' floor strips are
    // the Cues partial's.
    public static partial class RoyalVaultBuilder
    {
        static void Fork1(CourseBuilder b, Transform section)
        {
            Forecourt(b);
            Towers(b);
            Corridor(b, section);
            JunctionOne(b);
        }

        // The tower lanes' take-off openings, |x -+ TowerLaneX| <= TowerTakeoffHalf: 1.2 .. 3.2 either side.
        static float OpeningInner => F(R.TowerLaneX - R.TowerTakeoffHalf);
        static float OpeningOuter => F(R.TowerLaneX + R.TowerTakeoffHalf);
        // The towers' outer and inner reach across, |x| 0.4 .. 4.0: the rest platform's width, J1's tower landings.
        static float TowerOuterX => F(R.TowerLaneX + R.TowerDiameter / 2);
        static float TowerInnerX => F(R.TowerLaneX - R.TowerDiameter / 2);

        // ------------------------------------------------------------------ beat 0: the entrance and the forecourt (y 0)

        // The forecourt's west edge is open for z 0.6 .. 8 (the corridor's entrance); the corridor's east rail stands there on.
        static void Forecourt(CourseBuilder b)
        {
            float z1 = F(R.ForecourtZ1), inner = OpeningInner, outer = OpeningOuter;
            FloorNC(b, "RV entrance sill", -6f, 6f, Tier0 - 1f, Tier0, 0f, .6f);
            Slab(b, "RV forecourt", -14f, 14f, .6f, z1, Tier0);
            Rail(b, "RV rail forecourt E", 14f, 14.3f, .6f, z1, Tier0);
            Rail(b, "RV rail forecourt N-W", -13.7f, -outer, z1 - .3f, z1, Tier0);
            Rail(b, "RV rail forecourt N-mid", -inner, inner, z1 - .3f, z1, Tier0);
            Rail(b, "RV rail forecourt N-E", outer, 14.3f, z1 - .3f, z1, Tier0);
        }

        // ------------------------------------------------------------------ beat 1, risky: the coin towers (y 0 -> 4)

        // One round tower per lane in rows 1..7 but the rest: walkable tops, unclimbable sides, from the pit's bottom. Row 4 is
        // one rest platform across both lanes (CP1), railed but for its north take-off openings; its south edge only receives
        // jumps, so it gets no stubs.
        static void Towers(CourseBuilder b)
        {
            float lane = F(R.TowerLaneX), d = F(R.TowerDiameter);
            for (int k = 1; k <= R.TowerCount; k++)
            {
                if (k == R.TowerRest) continue;
                float z = F(R.TowerZ(k)), height = F(R.TowerTop(k) - R.PitBottom);
                b.Cylinder("RV tower W" + k, V(-lane, Pit, z), d, height, RoyalVaultLook.Greybox, true, true).AddComponent<CourseFloor>();
                b.Cylinder("RV tower E" + k, V(lane, Pit, z), d, height, RoyalVaultLook.Greybox, true, true).AddComponent<CourseFloor>();
            }
            float top = F(R.TowerTop(R.TowerRest)), mid = F(R.TowerZ(R.TowerRest)), half = d * .5f;
            float z0 = mid - half, z1 = mid + half, x = TowerOuterX, inner = OpeningInner, outer = OpeningOuter;
            Slab(b, "RV tower rest", -x, x, z0, z1, top);
            Rail(b, "RV rail rest W", -x - .3f, -x, z0, z1, top);
            Rail(b, "RV rail rest E", x, x + .3f, z0, z1, top);
            Rail(b, "RV rail rest N stub W", -x, -outer, z1 - .3f, z1, top);
            Rail(b, "RV rail rest N stub mid", -inner, inner, z1 - .3f, z1, top);
            Rail(b, "RV rail rest N stub E", outer, x, z1 - .3f, z1, top);
        }

        // ------------------------------------------------------------------ beat 1, safe: the hurdle corridor (y 0 -> 4)

        // 8 m between the rail faces (CorridorX -+ BulkheadHalfWidth), from the forecourt's south-west corner to stair 1. The rails
        // stop at the bulkheads' pockets.
        // R725 bot probe: beside the hurdles, wherever the pit lies behind the rail (the west side all along, the east side
        // north of the forecourt), the rail stands HurdleGuardHigh, not 1.2 m: a pawn the bar meets in the air (a jump timed a
        // little late) goes at the rail at up to 5 m/s with its hips 1.5 m up, and went over a 1.2 m rail into the pit - a
        // fall on the safe branch, which must have none (spec 5.3). Beside the forecourt a throw over the rail only lands on
        // the forecourt, so the rail there keeps the 1.2 m that lets the corridor be seen from it.
        static void Corridor(CourseBuilder b, Transform section)
        {
            float cx = F(R.CorridorX), w = F(R.BulkheadHalfWidth), w0 = cx - w, w1 = cx + w, end = F(R.J1Z0) - StairRun;
            float p1a = F(R.BulkheadZ[0]) - .6f, p1b = F(R.BulkheadZ[0]) + .6f, p2a = F(R.BulkheadZ[1]) - .6f, p2b = F(R.BulkheadZ[1]) + .6f;
            float court = F(R.ForecourtZ1);
            Slab(b, "RV corridor floor", w0 - .3f, w1, .6f, end, Tier0);
            Block(b, "RV rail corridor W 1", w0 - .3f, w0, Tier0 - 1f, Tier0 + HurdleGuardHigh, .6f, p1a);
            Rail(b, "RV rail corridor W 2", w0 - .3f, w0, p1b, p2a, Tier0);
            Rail(b, "RV rail corridor W 3", w0 - .3f, w0, p2b, end, Tier0);
            Rail(b, "RV rail corridor E 1", w1, w1 + .3f, 8f, court, Tier0);
            Block(b, "RV rail corridor E 1 guard", w1, w1 + .3f, Tier0 - 1f, Tier0 + HurdleGuardHigh, court, p1a);
            Rail(b, "RV rail corridor E 2", w1, w1 + .3f, p1b, p2a, Tier0);
            Rail(b, "RV rail corridor E 3", w1, w1 + .3f, p2b, end, Tier0);
            for (int h = 0; h < R.HurdleHubZ.Length; h++) Hurdle(b, section, h);
            for (int k = 0; k < R.BulkheadZ.Length; k++) Bulkhead(b, section, k);
            Stair(b, "RV stair 1", "RV rail stair 1", w0, w1, end, Tier0);
        }

        // The corridor's rail beside the hurdles where the pit lies behind it, over its floor: over the hips of any pawn a hurdle
        // throws (a hit at the start of a jump: 4.9 + 1.5 m/s up, the hips 1.75 m up at most) and the body above them.
        const float HurdleGuardHigh = 2.4f;

        // V02: a full gold bar through a hub, turning on the floor (Spinner, phase 0: its angle is HurdleAngle). The bar is a
        // picture over a round log collider (the garden's capsule-log pattern), its gold top the Cues partial's ("Cue hurdle
        // bar k"); the trigger round it reaches HurdleBodyHalf out, so a hit lands as the bar meets the body, and only staggers
        // near the hub, where the bar is slow, and knocks down further out. The tips pass 5 cm from the rail faces, moving along
        // them, so nobody is pinched.
        // R725 bot probe, two changes from the spec's drum:
        // - The hit is judged by the bar's own box (YardImpact Body.Box; `solid` is a trigger box the bar's size beside the
        //   shell), not as a drum round the shell. The drum judged a pawn jumping the bar by the shell's round, which stands
        //   0.3 m over the bar's top, so a jump that cleared the bar was still knocked down or thrown in mid-air; the box faces a
        //   body over the bar with its top, which never comes at it.
        // - A knocked-down pawn is let through the log (YardImpact's let-through needs `solid`; the log is `alsoSolid`): without
        //   it the log caught the pawns it had floored and carried them round to the rails at the tips' 7.6 m/s (flung at over
        //   10 m/s, 6-8 m back). The log itself stays the spec's round one.
        // R725 fun pass (fun1 baseline: hurdle 1 floored every blind runner three times, 1.5-2 s apart, before it gave up and
        // jumped, and its corridor took 29 s against 17-18): every point of the corridor is swept once each half turn
        // (HurdleHalfTurn, 1.64 / 1.8 s), just after the 1.5 s chain grace (YardKit.RecentlyDown) and the old 1 s cooldown had
        // run out, so the bar's other arm floored the pawn again as it stood up. Three changes:
        // - cooldown HurdleHitCooldown, half a turn and 0.6 s (2.24 / 2.4; HurdleFreePass has why not 0.3). YardImpact keeps
        //   it per body, so the pawn just knocked down gets one free pass of the other arm, and nobody else's hits change. A
        //   body the bar only staggered (near the hub) keeps the spec's 1 s (staggerCooldown, under the half turn): a stagger
        //   lets nothing through, and with the long wait the other arm's log met it solid while the knock was blind to it
        //   (R725 review); within that second the same arm may still meet a body it pushed on ahead, as before the fun pass.
        // - downBack, downPush 1.5 (SlewJib's R684 knockdown of a low load sweeping the legs): the floored pawn tips back over
        //   the bar at 1.5 m/s and the bar passes on, instead of being thrown on ahead of it at up to 5 m/s, 30 % of that out
        //   towards the rails. Kept: the let-through at once (ghostDelay 0), lift 1.5, the staggers near the hub.
        // - the let-through lasts through the free pass (ghostSeconds HurdleLetThroughSeconds, and the zone). Past 0.35 s
        //   YardImpact keeps a body let through only while it overlaps the axis-aligned box round the knock's solids, and for a
        //   turning bar that box is thin across the bar's line twice each half turn: run on the 99 hurdle hits of the fun1
        //   probes, that rule let every body go 0.35-1.06 s after the hit, long before the other arm came - whose log, solid
        //   again and with no hit inside the cooldown to let the body through, would bat the pawn just up (before the
        //   let-through the log carried floored pawns 4.7-8.1 m). So the zone, a trigger over the hurdle's whole square as high
        //   as a hurdle throws a body (HurdleGuardHigh), is listed with the log (`alsoSolid`): it collides with nothing, but its
        //   box keeps a body anywhere over the hurdle let through until ghostSeconds; one that leaves the square is clear of the
        //   bar.
        //   Timeline from a knockdown by arm A, hurdle 1 (hurdle 2), seconds: 0 down for 1 s, pushed back over the bar, let
        //   through at once; ~0.2 A has passed under it; ~1.1-1.3 back up (the second counts lying down); ~1.5-1.64 (1.65-1.8)
        //   arm B over the spot, a little sooner for the back push's drift towards it: no hit, B passes through - the free pass;
        //   2.24 (2.4) the cooldown ends, B 60-66 deg on; 2.45 (2.7) the let-through ends at three quarters of a turn, the bar's
        //   line square to the spot and both arms as far from it as it is from the hub (1.6 m and more wherever the bar knocks
        //   down); 3.27 (3.6) A back over the spot, a hit as before. The let-through outlasts the cooldown, so the log is never
        //   solid while the knock is blind to a body it knocked down.
        //   What that buys (R725 review): no knockdown while getting up - a second and more on its feet before the next one
        //   (1.0-1.5 s in the review's model). Not one knockdown a crossing: a blind pawn that walks on into the first arm's
        //   return (hurdle 1's east half, hurdle 2's west half, where the bar meets it head-on) is met by it after the
        //   let-through, about 1.3-1.5 s after standing up, and floored as before - a hit it sees coming and can jump; about two
        //   knockdowns a blind crossing there in the model.
        static void Hurdle(CourseBuilder b, Transform section, int h)
        {
            var root = Root(b, "RV hurdle " + (h + 1), V(F(R.HurdleHubX), Tier0, F(R.HurdleHubZ[h])));
            root.gameObject.AddComponent<Spinner>().Configure(Vector3.up, F(R.HurdleDegreesPerSecond[h]));
            YardKit.Cylinder(root, "RV hurdle hub", V(0f, .45f, 0f), Vector3.up, .8f, .9f, RoyalVaultLook.Greybox, Solid.NoClimb);

            float y = F((R.HurdleBarLow + R.HurdleBarHigh) / 2), thick = F(R.HurdleBarHigh - R.HurdleBarLow), length = F(2 * R.HurdleReach);
            Part(root, "RV hurdle bar", V(0f, y, 0f), V(length, thick, thick), Solid.None);
            var log = new GameObject("RV hurdle bar log");
            log.transform.SetParent(root, false);
            var round = log.AddComponent<CapsuleCollider>();
            round.direction = 0;   // along the bar (x)
            round.radius = thick * .5f;
            round.height = length;
            round.center = V(0f, y, 0f);
            log.AddComponent<NoClimbSurface>();
            // Its own object, so the knock's OnTriggerStay never hears it (only the shell and the faces box beside it judge).
            var zone = new GameObject("RV hurdle " + (h + 1) + " zone");
            zone.transform.SetParent(root, false);
            var square = zone.AddComponent<BoxCollider>();
            square.isTrigger = true;
            square.center = V(0f, HurdleGuardHigh * .5f, 0f);
            square.size = V(length, HurdleGuardHigh, length);

            var hit = new GameObject("RV hurdle " + (h + 1) + " (hit)");
            hit.transform.SetParent(root, false);
            var shell = hit.AddComponent<CapsuleCollider>();   // first: YardImpact takes its object's first collider as the shell
            shell.isTrigger = true;
            shell.direction = 0;
            shell.radius = .45f;
            shell.height = 8f;
            shell.center = V(0f, y, 0f);
            var faces = hit.AddComponent<BoxCollider>();   // the bar's box, inside the shell: only judges, collides with nothing
            faces.isTrigger = true;
            faces.center = V(0f, y, 0f);
            faces.size = V(length, thick, thick);
            var impact = hit.AddComponent<YardImpact>();   // the Spinner above is its moving surface
            impact.body = YardImpact.Body.Box;
            impact.solid = faces;
            impact.alsoSolid = new Collider[] { round, square };
            impact.knockdownSpeed = 3f;   // hips beyond ~1.6 m from the hub
            impact.knockdownSeconds = 1f;
            impact.knockbackForce = 3f;
            impact.speedScale = .3f;
            impact.maxPush = 5f;
            impact.lift = 1.5f;
            impact.alongShare = .7f;
            impact.replaceSpeed = true;
            impact.downPush = 1.5f;
            impact.downBack = true;
            impact.cooldown = F(R.HurdleHitCooldown(h));
            impact.staggerCooldown = 1f;   // the spec's cooldown, for a stagger (see above)
            impact.ghostSeconds = F(R.HurdleLetThroughSeconds(h));
            impact.course = section;
            impact.forwardShare = .6f;
        }

        // V03: a castling bulkhead - two pocket panels half a cycle apart (BulkheadOut), each closing its half of the corridor
        // toward the half that is opening, so a 4 m gap always stays open and slides from half to half. The panels wait in
        // pockets outside the rails (static, the rails stop there); three merlons on each read as a rook. The hit only shoves
        // (no knockdown), along the panel's motion, out of the closing half.
        static void Bulkhead(CourseBuilder b, Transform section, int k)
        {
            float cx = F(R.CorridorX), w = F(R.BulkheadHalfWidth), travel = F(R.BulkheadTravel), z = F(R.BulkheadZ[k]);
            const float tall = 3f, gap = .02f, deep = .8f;
            Block(b, "RV bulkhead pocket W" + (k + 1), cx - w - travel - .3f, cx - w, Tier0 - 1f, Tier0 + 3.2f, z - .6f, z + .6f);
            Block(b, "RV bulkhead pocket E" + (k + 1), cx + w, cx + w + travel + .3f, Tier0 - 1f, Tier0 + 3.2f, z - .6f, z + .6f);
            for (int s = -1; s <= 1; s += 2)
            {
                int bb = k, side = s;   // copied: each lambda keeps its own
                var root = Root(b, "RV bulkhead panel " + (side < 0 ? "W" : "E") + (bb + 1),
                                V(cx + side * (w + travel * .5f), Tier0 + gap + tall * .5f, z));
                root.gameObject.AddComponent<VaultMover>().Configure(V(-side * travel, 0f, 0f), t => R.BulkheadOut(bb, side, t));
                var panel = Part(root, "RV bulkhead panel", Vector3.zero, V(travel, tall, deep), Solid.NoClimb);
                panel.AddComponent<ChessFight.Game.CameraSeeThrough>().enabled = false;   // VaultMover: on while moving
                for (int m = -1; m <= 1; m++)
                    Part(root, "RV bulkhead merlon", V(1.4f * m, tall * .5f + .25f, 0f), V(.6f, .5f, deep), Solid.None);
                var impact = YardKit.Shell(root, "RV bulkhead (hit)", Vector3.zero, V(travel + .4f, tall + .2f, deep + .4f), 3f);
                impact.solid = panel.GetComponent<BoxCollider>();
                impact.knockdownSpeed = 99f;   // a shove only
                impact.alongShare = 1f;
                impact.speedScale = .3f;
                impact.maxPush = 4.5f;
                impact.lift = .8f;
                impact.replaceSpeed = true;
                impact.cooldown = .6f;
                impact.course = section;
                impact.forwardShare = .6f;
            }
            // Castling: queenside west, kingside east.
            RoyalVaultLook.Sign(b.parent, "O-O-O", V(cx - w * .5f, Tier0 + 3.4f, z - .5f), .8f);
            RoyalVaultLook.Sign(b.parent, "O-O", V(cx + w * .5f, Tier0 + 3.4f, z - .5f), .8f);
        }

        // ------------------------------------------------------------------ J1 (y 4)

        // 30 x 10 m. Its south edge is open for the two tower lanes' landings and stair 1, its north edge for the walkway.
        static void JunctionOne(CourseBuilder b)
        {
            float z0 = F(R.J1Z0), z1 = F(R.J1Z1), x = TowerOuterX, inner = TowerInnerX;
            Slab(b, "RV J1", -22f, 8f, z0, z1, Tier1);
            Rail(b, "RV rail J1 S-mid", -13.7f, -x, z0 - .3f, z0, Tier1);
            Rail(b, "RV rail J1 S-stub", -inner, inner, z0 - .3f, z0, Tier1);
            Rail(b, "RV rail J1 S-E", x, 8f, z0 - .3f, z0, Tier1);
            Rail(b, "RV rail J1 W", -22.3f, -22f, z0, z1 + .3f, Tier1);
            Rail(b, "RV rail J1 E", 8f, 8.3f, z0 - .3f, z1 + .3f, Tier1);
            Rail(b, "RV rail J1 N-W", -22f, -4f, z1, z1 + .3f, Tier1);
            Rail(b, "RV rail J1 N-E", 4f, 8f, z1, z1 + .3f, Tier1);
        }
    }
}
