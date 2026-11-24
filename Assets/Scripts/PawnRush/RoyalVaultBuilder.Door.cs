using UnityEngine;
using R = ChessFight.Network.RoyalVaultRules;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // Beats 5 and 6 of the royal vault, on the terrace tier (y 12): the terrace (the regroup before the door), the door wall
    // with its octagonal doorway, the time-lock door - a disc rolling DoorTravel east into the wall and back (VaultMover from
    // DoorOpen, turning as it rolls) with its doorway-clearing shove on a separate translate-only mover - then the treasure
    // room, its open arch with "7" (rank 7; beyond, the audience hall is rank 8, promotion) and the level runout to the exit.
    // No obstacle stands in the last 12 m before the arch, and the room holds no props. The countdowns are the Cues partial's.
    public static partial class RoyalVaultBuilder
    {
        static void Door(CourseBuilder b, Transform section)
        {
            float t0 = F(R.TerraceZ0), d0 = F(R.DoorZ0), d1 = F(R.DoorZ1);
            // 28 x 10 m; open south for stair 3.
            Slab(b, "RV terrace", -14f, 14f, t0, d0, Tier3);
            Rail(b, "RV rail terrace S-W", -14f, -4.3f, t0 - .3f, t0, Tier3);
            Rail(b, "RV rail terrace S-E", 4.3f, 14f, t0 - .3f, t0, Tier3);
            Rail(b, "RV rail terrace W", -14.3f, -14f, t0, d0, Tier3);
            Rail(b, "RV rail terrace E", 14f, 14.3f, t0, d0, Tier3);

            DoorWall(b);
            DoorDisc(b, section);
            TreasureRoom(b);
        }

        // The wall across the hall at DoorZ0..DoorZ1 round a regular octagon (inradius DoorWayInradius, centred DoorCentreAbove
        // over the floor; the floor cuts its bottom side): the solid west part, the east part split into two skins round the
        // disc's slot (a foot under it and a cap over it), the top over the doorway, and four 3 x 3 m boxes turned 45 degrees
        // whose inner faces are the octagon's slanted sides. Rolled open, the disc passes behind the east corners' faces, so it
        // reads as rolling behind the frame.
        static void DoorWall(CourseBuilder b)
        {
            float d0 = F(R.DoorZ0), d1 = F(R.DoorZ1), centre = Tier3 + F(R.DoorCentreAbove), r = F(R.DoorWayInradius);
            float disc = F(R.DoorRadius), skin = .4f;
            Block(b, "RV door wall W", -14f, -r, Pit, RoofY, d0, d1);
            Block(b, "RV door wall E skin S", r, 14f, Pit, RoofY, d0, d0 + skin);
            Block(b, "RV door wall E skin N", r, 14f, Pit, RoofY, d1 - skin, d1);
            Block(b, "RV door wall E foot", r, 14f, Pit, centre - disc - .1f, d0 + skin, d1 - skin);
            Block(b, "RV door wall E cap", r, 14f, centre + disc + .1f, RoofY, d0 + skin, d1 - skin);
            Block(b, "RV door wall top", -r, r, centre + r, RoofY, d0, d1);
            const float corner = 3f;
            float off = (r + corner * .5f) / Mathf.Sqrt(2f);   // 3.818: the turned box's inner face lies r from the centre
            var turned = Quaternion.Euler(0f, 0f, 45f);
            float z = (d0 + d1) * .5f, deep = d1 - d0;
            YardKit.Box(b.parent, "RV doorway corner UL", V(-off, centre + off, z), V(corner, corner, deep), RoyalVaultLook.Greybox, Solid.NoClimb, turned);
            YardKit.Box(b.parent, "RV doorway corner UR", V(off, centre + off, z), V(corner, corner, deep), RoyalVaultLook.Greybox, Solid.NoClimb, turned);
            YardKit.Box(b.parent, "RV doorway corner LL", V(-off, centre - off, z), V(corner, corner, deep), RoyalVaultLook.Greybox, Solid.NoClimb, turned);
            YardKit.Box(b.parent, "RV doorway corner LR", V(off, centre - off, z), V(corner, corner, deep), RoyalVaultLook.Greybox, Solid.NoClimb, turned);
            FloorNC(b, "RV door sill", -r, r, Tier3 - 1f, Tier3, d0, d1);
        }

        // V10: the disc (VaultMover root on its centre when shut) slides DoorTravel east as DoorOpen goes 0..1 and turns about
        // the way on by -DoorTravel / DoorRadius radians over it, so it rolls without slipping (negative for +x travel). It is
        // kinematic: it passes the static sill and walls without touching them, its bottom 1.5 m under the floor never seen. A
        // spoke across its face shows the roll. The shove that clears the doorway rides a sibling mover that only translates
        // (a shell turning with the disc would miss about a fifth of the roll angles): its leading face runs just ahead of the
        // rim, and it pushes out of the doorway along -+z, to the side the pawn is on (alongShare 0), never knocks down.
        static void DoorDisc(CourseBuilder b, Transform section)
        {
            var at = V(0f, Tier3 + F(R.DoorCentreAbove), F((R.DoorZ0 + R.DoorZ1) / 2));
            float travel = F(R.DoorTravel), radius = F(R.DoorRadius), thick = F(R.DoorThick);
            var disc = Root(b, "RV door disc", at);
            disc.gameObject.AddComponent<VaultMover>().Configure(V(travel, 0f, 0f), t => R.DoorOpen(t), Vector3.forward,
                t => -R.DoorOpen(t) * R.DoorTravel / R.DoorRadius * 180.0 / System.Math.PI);
            var body = YardKit.Cylinder(disc, "RV door disc body", Vector3.zero, Vector3.forward, 2f * radius, thick, RoyalVaultLook.Greybox, Solid.NoClimb);
            body.AddComponent<ChessFight.Game.CameraSeeThrough>().enabled = false;   // VaultMover: on while moving
            Part(disc, "RV door spoke", Vector3.zero, V(2f * radius - .4f, .6f, thick + .05f), Solid.None);

            var shove = Root(b, "RV door shove", at);
            shove.gameObject.AddComponent<VaultMover>().Configure(V(travel, 0f, 0f), t => R.DoorOpen(t));
            // Axis-aligned, y 11.7 .. 14.5 in the section: 0.07 m ahead of the rim at 1 m over the floor, 0.5 m at hip height.
            var impact = YardKit.Shell(shove, "RV door (hit)", V(0f, -1.9f, 0f), V(8.2f, 2.8f, 1.6f), 3f);
            impact.body = YardImpact.Body.Box;   // no solid: the shell's own faces
            impact.knockdownSpeed = 99f;   // a shove only
            impact.alongShare = 0f;
            impact.speedScale = .3f;
            impact.maxPush = 4.5f;
            impact.lift = .5f;
            impact.replaceSpeed = true;
            impact.cooldown = .8f;
            impact.course = section;
            impact.forwardShare = .6f;
        }

        // The treasure room (12 m wide, flush with the Late corridor's walls, |x| 6..6.6 and 7.5 m tall) to the exit: CP6 2.5 m
        // past the door, the open arch (12 x 6 m, no door) with "7" on its lintel, then the 4 m level strip.
        static void TreasureRoom(CourseBuilder b)
        {
            float d1 = F(R.DoorZ1), exit = F(R.ExitZ), arch = F(R.ArchZ);
            const float tall = 7.5f;
            FloorNC(b, "RV room floor", -6f, 6f, Tier3 - 1f, Tier3, d1, exit);
            Block(b, "RV room wall W", -6.6f, -6f, Tier3 - 1f, Tier3 + tall, d1, exit);
            Block(b, "RV room wall E", 6f, 6.6f, Tier3 - 1f, Tier3 + tall, d1, exit);
            Block(b, "RV arch pier W", -7.2f, -6f, Tier3, Tier3 + tall, arch - .6f, arch + .6f);
            Block(b, "RV arch pier E", 6f, 7.2f, Tier3, Tier3 + tall, arch - .6f, arch + .6f);
            Block(b, "RV arch lintel", -7.2f, 7.2f, Tier3 + 6f, Tier3 + tall, arch - .6f, arch + .6f);
            RoyalVaultLook.Sign(b.parent, "7", V(0f, Tier3 + 6.75f, arch - .65f), 2.5f);
        }
    }
}
