using ChessFight.Gameplay;
using UnityEngine;
using R = ChessFight.Network.RoyalVaultRules;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // Beat 3 of the royal vault, fork 2, from the approach (y 4): the safe way rides one of two jewel belts back against the
    // way (VaultBeltSurface), queues on the landing and crosses the crown press's band in the 8 m neck, then climbs stair 2;
    // the risky way is thrown by the knight's launch pad onto the shelf (y 8, railed over its landing zone), passes the two
    // drawers, whose faces knock down whoever they meet at speed and sweep them westward off its open edge onto the belts, and
    // runs on over the east cabinet's top and the north shelf. Both arrive on J2. The shelf overhangs the east belt by 1 m
    // only, never the neck, so a drawer drop lands on a belt or the landing's first 0.3 m - always before the press band. Cues
    // (pad rim, shelf lip, belt strips and slats, press band, drawer faces) are the Cues partial's.
    public static partial class RoyalVaultBuilder
    {
        static void Fork2(CourseBuilder b, Transform section)
        {
            Approach(b);
            Belts(b);
            Press(b, section);
            Shelf(b, section);
        }

        // ------------------------------------------------------------------ the approach and the knight's launch pad

        // 28 x 6 m; open south for the walkway and north for the belts. The pad lies 1.2 m clear of the walkway's line (x 4): a
        // sameThrow LaunchPad whose trigger stands on the floor, thrown to (PadLandX, PadLandY, PadLandZ) on the shelf.
        static void Approach(CourseBuilder b)
        {
            float z0 = F(R.WalkZ1), z1 = F(R.ApproachZ1), west = F(R.BeltWestX0), east = F(R.BeltEastX1);
            Slab(b, "RV approach", -14f, 14f, z0, z1, Tier1);
            Rail(b, "RV rail approach S-W", -14f, -4f, z0 - .3f, z0, Tier1);
            Rail(b, "RV rail approach S-E", 4f, 14f, z0 - .3f, z0, Tier1);
            Rail(b, "RV rail approach W", -14.3f, -14f, z0, z1, Tier1);
            Rail(b, "RV rail approach E", 14f, 14.3f, z0, z1, Tier1);
            Rail(b, "RV rail approach N-W", -14f, west - .3f, z1, z1 + .3f, Tier1);
            Rail(b, "RV rail approach N-E", east + .3f, 14f, z1, z1 + .3f, Tier1);

            float x = F(R.PadX), z = F(R.PadZ), half = F(R.PadSize / 2), floor = F(R.PadFloor);
            Picture(b, "RV pad", V(x - half, floor, z - half), V(x + half, floor + .04f, z + half));
            var pad = b.Trigger<LaunchPad>("RV pad launch", V(x - half, floor, z - half), V(x + half, floor + .6f, z + half));
            pad.Configure(V(F(R.PadLandX - R.PadX), F(R.PadLandY - R.PadFloor), F(R.PadLandZ - R.PadZ)), F(R.PadClearance), 0f, 0f, true);
        }

        // ------------------------------------------------------------------ the jewel belts and the landing (y 4)

        // V06: two 5 m belts side by side, never moving forward (west always back, east stopped / back): static floors whose
        // VaultBeltSurface hands riders RoyalVaultRules.BeltSpeed. The shelf's support is the east belt's and the landing's
        // east wall.
        static void Belts(CourseBuilder b)
        {
            float x0 = F(R.BeltWestX0), split = F(R.BeltSplitX), x1 = F(R.BeltEastX1), z0 = F(R.BeltZ0), z1 = F(R.BeltZ1);
            Slab(b, "RV belt W", x0, split, z0, z1, Tier1).AddComponent<VaultBeltSurface>().Configure(0);
            Slab(b, "RV belt E", split, x1, z0, z1, Tier1).AddComponent<VaultBeltSurface>().Configure(1);
            Rail(b, "RV rail belt W", x0 - .3f, x0, z0, F(R.NeckZ0), Tier1);
            Rail(b, "RV rail belt E", x1, x1 + .3f, z0, F(R.ShelfZ0), Tier1);
            Block(b, "RV shelf support", x1, F(R.ShelfX1), Pit, ShelfBottom, F(R.ShelfZ0), F(R.NeckZ0));
            Slab(b, "RV landing", x0, x1, z1, F(R.NeckZ0), Tier1);
        }

        // ------------------------------------------------------------------ the crown press (V05) and stair 2

        // A full-width die over the 8 m neck between two cabinets (the east one's top is the risky way), driven by VaultMover
        // from PressLowered; a static head beam on two frames above it, the screw sliding in the beam. One crush shell round
        // the die (SlamChest's crush) pushes whoever it comes down on straight out along the way, -z or +z, never into the
        // walls, and lets a floored pawn through the die until it lifts; the static wave (VaultPressWave) shoves the rest of
        // the band at each landing and leaves whoever the crush hit alone.
        static void Press(CourseBuilder b, Transform section)
        {
            float x = F(R.PressX), z = F(R.PressZ), half = F(R.PressDieAcross / 2), n0 = F(R.NeckZ0), n1 = F(R.NeckZ1);
            float west = x - half, east = x + half;
            Block(b, "RV cabinet W", -14f, west, Pit, Tier3, n0, n1);
            FloorNC(b, "RV cabinet E", east, 14f, Pit, Tier2, n0, n1);
            Slab(b, "RV neck", west, east, n0, n1, Tier1);

            float tall = F(R.PressDieTall), top = tall * .5f;
            var die = Root(b, "RV press die", V(x, Tier1 + F(R.PressDrop) + top, z));   // the die's centre when up
            die.gameObject.AddComponent<VaultMover>().Configure(V(0f, -F(R.PressDrop), 0f), t => R.PressLowered(t));
            var box = Part(die, "RV press die box", Vector3.zero, V(F(R.PressDieAcross) - .02f, tall, F(R.PressDieAlong)), Solid.NoClimb);
            box.AddComponent<ChessFight.Game.CameraSeeThrough>().enabled = false;   // VaultMover: on while moving
            Part(die, "RV press crown band", V(0f, top + .15f, 0f), V(3f, .3f, 1.2f), Solid.None);
            for (int k = -2; k <= 2; k++)
                Part(die, "RV press crown point", V(.6f * k, top + .6f, 0f), V(.3f, .6f, .3f), Solid.None);
            // Local y 0.8 .. 11.2: inside the head beam when the die is down, standing out over it when the die is up.
            YardKit.Cylinder(die, "RV press screw", V(0f, 6f, 0f), Vector3.up, .8f, 10.4f, RoyalVaultLook.Greybox, Solid.None);
            // One shell for the whole die, so a landing hits a character once: a YardImpact's cooldown is its own, and
            // YardKit.RecentlyDown stops only a second knockdown, not a second push - two half shells meeting under the die's
            // middle each hit a pawn standing across it in the same physics step, -z then +z (R725 bot probe: 6.3 then 9.6 m/s).
            // It reaches PressCrushBeyond past the die's faces and ends and PressCrushBelow under it (PressSafeToCross keeps a
            // body out of that). Its lane runs across the neck and alongShare is 0, so the push goes straight out from the die's
            // middle line along the way, to the side the hips are on; dead on that line (within 1 cm), back along -z
            // (centreSide 1: up x across is -z).
            float beyond = F(R.PressCrushBeyond), below = F(R.PressCrushBelow);
            var crush = YardKit.Shell(die, VaultPressWave.CrushName, V(0f, -below * .5f, 0f),
                                      V(F(R.PressDieAcross) + 2f * beyond, tall + below, F(R.PressDieAlong) + 2f * beyond), F(R.PressCrushPush));
            var lane = new GameObject("lane").transform;
            lane.SetParent(crush.transform, false);
            lane.localRotation = Quaternion.Euler(0f, 90f, 0f);   // its forward the section's +x
            crush.lane = lane;
            crush.alongShare = 0f;
            crush.centreSide = 1f;
            crush.solid = box.GetComponent<BoxCollider>();
            crush.onFloor = false;
            crush.judgePart = true;
            crush.fromCentre = true;
            crush.speedScale = 0f;
            crush.maxPush = F(R.PressCrushPush);
            crush.lift = 1.5f;
            crush.replaceSpeed = true;
            crush.knockdownSpeed = 3f;
            crush.knockdownSeconds = F(R.PressKnockdown);
            crush.cooldown = .5f;   // longer than the fall: one hit a landing
            crush.ghostSeconds = F(R.PressSit) + .6f;   // through the die until it lifts
            crush.course = section;
            crush.forwardShare = .6f;
            Picture(b, "RV press head", V(x - 5f, 15f, z - 1f), V(x + 5f, 16.4f, z + 1f));
            Picture(b, "RV press frame W", V(west - .8f, Tier3, z - .4f), V(west - .2f, 15f, z + .4f));
            Picture(b, "RV press frame E", V(east, Tier2, z - .4f), V(east + .6f, 15f, z + .4f));
            Root(b, "RV press wave", V(x, Tier1, z)).gameObject.AddComponent<VaultPressWave>().Configure(section, Tier1, crush);

            Stair(b, "RV stair 2", "RV rail stair 2", west, east, n1, Tier1);
        }

        // ------------------------------------------------------------------ the shelf and the vault drawers (V08), y 8

        // 4 m wide (ShelfX0..ShelfX1) from ShelfZ0 to J2, over the east cabinet's top in the neck's stretch; railed both sides but
        // for its west edge beside the drawers (ShelfOpenZ0..ShelfOpenZ1, over the belts), where the drawer cabinet is its east
        // wall. Each drawer (VaultMover from DrawerOut) sweeps the shelf's full width out of the cabinet; its hit knocks down
        // whoever its face comes at 3 m/s or more (the face cruises at 4 / 0.75 = 5.33 m/s; the ramps' slow ends do not hit) and
        // pushes west, low, so the 4 m drop onto the belts lands under the 9 m/s landing knockdown.
        // R725 fun pass (spec 6 "drops too cheap"; the critic's risk 2): the hit used to shove only (knockdownSpeed 99), and a
        // run swept off the shelf still beat a clean belt run by about 1.2 s (fork 2 safe minus risky +1.7 / +1.9 / +2.8 s for
        // Sprint / Jumper / Run over 96 crossings; the Core model: a drop costs 1.3-1.5 s against the pad's 2.0 s at a run), so
        // nobody had a reason to ride the belts and the crown press met only swept-off pawns. Now a sweep also costs the
        // knockdown (DrawerKnockdown), which the ragdoll counts lying down (on the shelf, then on the belt after the fall),
        // and the getting up. No `solid`: YardImpact lets a body it knocks down through `solid` only, and a box sliding
        // through a body reads as passing through (R684); the face carries the floored pawn off the open edge itself, where
        // its stroke ends, with nothing to pinch it against. Facing then judges by the shell's own box (reaching
        // DrawerShellBeyond, 0.3 m, west of the drawer's face, 0.1 m larger across): a body on the shelf in the drawer's way is
        // first met by that box's west face, 0.3 m ahead of the drawer's and coming at it at the drawer's speed, standing or
        // lying, as the drawer's own box judged it. A face slower than the knockdown does not hit at all (minSpeed), so every
        // drawer hit knocks down.
        static void Shelf(CourseBuilder b, Transform section)
        {
            float x0 = F(R.ShelfX0), x1 = F(R.ShelfX1), z0 = F(R.ShelfZ0), open0 = F(R.ShelfOpenZ0), open1 = F(R.ShelfOpenZ1);
            float n0 = F(R.NeckZ0), n1 = F(R.NeckZ1), end = F(R.StairTwoZ1);
            const float cabinetEnd = 103.5f;
            FloorNC(b, "RV shelf S", x0, x1, ShelfBottom, Tier2, z0, n0);
            FloorNC(b, "RV shelf N", x0, x1, ShelfBottom, Tier2, n1, end);
            Rail(b, "RV rail shelf W 1", x0 - .3f, x0, z0, open0, Tier2, Tier2 - ShelfBottom);
            Rail(b, "RV rail shelf W 2", x0 - .3f, x0, open1, end, Tier2, Tier2 - ShelfBottom);
            Rail(b, "RV rail shelf E 1", x1, x1 + .3f, z0, open0, Tier2, Tier2 - ShelfBottom);
            Rail(b, "RV rail shelf E 2", x1, x1 + .3f, cabinetEnd, end, Tier2, Tier2 - ShelfBottom);
            Block(b, "RV drawer cabinet", x1, 11.2f, ShelfBottom, 11f, open0, cabinetEnd);

            float travel = F(R.DrawerTravel), length = F(R.DrawerLong), reach = F(R.DrawerShellBeyond);
            const float tall = 1.8f, lift = .05f;
            for (int k = 0; k < R.DrawerZ.Length; k++)
            {
                int d = k;   // copied: each lambda keeps its own
                var root = Root(b, "RV drawer " + (d + 1), V(x1 + travel * .5f, Tier2 + lift + tall * .5f, F(R.DrawerZ[d])));
                root.gameObject.AddComponent<VaultMover>().Configure(V(-travel, 0f, 0f), t => R.DrawerOut(d, t));
                var box = Part(root, "RV drawer box", Vector3.zero, V(travel, tall, length), Solid.NoClimb);
                box.AddComponent<ChessFight.Game.CameraSeeThrough>().enabled = false;   // VaultMover: on while moving
                var impact = YardKit.Shell(root, "RV drawer " + (d + 1) + " (hit)", V(-reach * .5f, 0f, 0f), V(travel + reach, tall + .1f, length + .1f), 3f);
                impact.knockdownSpeed = 3f;
                // R725 review: a pawn hugging the cabinet (hips within about 0.5 m of it) was met while the face was still slow,
                // only staggered, then shoved by the box at up to 5.33 m/s for about 0.4 s (no `solid`, inside the cooldown) before
                // the knockdown came, 0.6 s later than anywhere else: a box ploughing a standing body, as R684 removed from the
                // timber loads. With no hit below the knockdown speed the box alone moves it at most 0.21 m in the stroke's first
                // 0.14 s, then the face, at 3 m/s, knocks it down.
                impact.minSpeed = impact.knockdownSpeed;
                // RoyalVaultRules.DrawerKnockdown, 1.8 s: the ragdoll counts it lying on the shelf from the hit and on the belt
                // after the fall, not in the fall, and a Pawn Rush pawn stands and settles in 0.2 s, so a sweep adds the 1.5 s it
                // was modelled to add (the critic: at a run the belts then beat a swept pad run by 0.9 s, and a clean one still
                // wins by 2 s). YardImpact's default 0.9 s, which this was first, added only about 0.6 s (R725 review).
                impact.knockdownSeconds = F(R.DrawerKnockdown);
                impact.speedScale = .4f;
                impact.maxPush = 5f;
                impact.lift = .3f;
                impact.alongShare = 1f;   // west, off the open edge
                impact.replaceSpeed = true;
                impact.cooldown = .6f;
            }
        }
    }
}
