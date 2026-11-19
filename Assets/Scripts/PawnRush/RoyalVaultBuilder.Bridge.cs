using UnityEngine;
using R = ChessFight.Network.RoyalVaultRules;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // J2 (y 8), where the two ways of fork 2 meet, then beat 4 of the royal vault: the flipping plates over the pit - two rows
    // of PlateCount plates, hinged on their outer edges so the inner edge lifts (VaultPlate from PlateAngle), the rows half a
    // cycle apart so one always lies flat - and stair 3 up to the terrace. The plate rims are the Cues partial's.
    public static partial class RoyalVaultBuilder
    {
        // The plates' bridge is 8 m wide: the rows' hinge lines stand at x -+4.
        const float BridgeHalf = 4f;

        static void Bridge(CourseBuilder b)
        {
            float z0 = F(R.J2Z0), z1 = F(R.J2Z1), plates = F(R.PlatesZ1);
            // 28 x 10 m; open south for stair 2 and the shelf, north for the plates.
            Slab(b, "RV J2", -14f, 14f, z0, z1, Tier2);
            Rail(b, "RV rail J2 S-W", -14f, -6.3f, z0 - .3f, z0, Tier2);
            Rail(b, "RV rail J2 S-mid", 2.3f, 2.7f, z0 - .3f, z0, Tier2);
            Rail(b, "RV rail J2 S-E", 7.3f, 14f, z0 - .3f, z0, Tier2);
            Rail(b, "RV rail J2 W", -14.3f, -14f, z0, z1, Tier2);
            Rail(b, "RV rail J2 E", 14f, 14.3f, z0, z1, Tier2);
            Rail(b, "RV rail J2 N-W", -14f, -BridgeHalf, z1, z1 + .3f, Tier2);
            Rail(b, "RV rail J2 N-E", BridgeHalf, 14f, z1, z1 + .3f, Tier2);

            // V09: each plate's root is on its hinge line; its deck lies in local x 0..PlateWide (inward) and z 0..PlateLong.
            // The west row (0) faces the way on; the east row (1) is turned round (yaw 180), so its local +x points west, inward,
            // too. Seams: (PlatePitch - PlateLong) along z, 2 (BridgeHalf - PlateWide) at x 0. VaultPlate builds its deck at once
            // in Configure, so the decks stand in Edit mode as well.
            float seam = F((R.PlatePitch - R.PlateLong) / 2), pitch = F(R.PlatePitch);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < R.PlateCount; i++)
                {
                    float z = F(R.PlateZ0) + pitch * i;
                    var at = row == 0 ? V(-BridgeHalf, Tier2, z + seam) : V(BridgeHalf, Tier2, z + pitch - seam);
                    var root = Root(b, "RV plate " + (row == 0 ? "W" : "E") + (i + 1), at, row == 0 ? 0f : 180f);
                    root.gameObject.AddComponent<VaultPlate>().Configure(row, F(R.PlateWide), F(R.PlateLong));
                }
            YardKit.Cylinder(b.parent, "RV plate hinge W", V(-BridgeHalf - .05f, Tier2 - .15f, (z1 + plates) * .5f), Vector3.forward, .3f,
                             plates - z1, RoyalVaultLook.Greybox, Solid.None);
            YardKit.Cylinder(b.parent, "RV plate hinge E", V(BridgeHalf + .05f, Tier2 - .15f, (z1 + plates) * .5f), Vector3.forward, .3f,
                             plates - z1, RoyalVaultLook.Greybox, Solid.None);

            Stair(b, "RV stair 3", "RV rail stair 3", -BridgeHalf, BridgeHalf, plates, Tier2);
        }
    }
}
