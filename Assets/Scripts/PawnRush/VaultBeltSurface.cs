using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The top of one of the royal vault's jewel belts (V06, R725, build spec §2.1 and §2.7), on the belt's static box like
    // ConveyorBeltSurface: the box does not move, but says it does (IMovingSurface), so a pawn standing on it is carried at
    // the belt's speed along this object's +z (the ragdoll adds the surface's velocity under its feet). The speed is
    // RoyalVaultRules.BeltSpeed on the obstacle clock (belt 0 west: always back; belt 1 east: stopped, then back), so every
    // PC carries its riders alike. No state, nothing kinematic.
    //
    // Unlike ConveyorBeltSurface (R685) it answers for any point, not only over the top: the vault's belts are NoClimb and
    // every side of them is covered by a rail, the shelf's support, the approach or the landing, so nothing touches a side.
    public sealed class VaultBeltSurface : MonoBehaviour, IMovingSurface
    {
        [Tooltip("0 the west belt, 1 the east belt (RoyalVaultRules.BeltSpeed)")]
        [SerializeField] int belt;

        public int Belt => belt;

        public void Configure(int belt) => this.belt = belt;

        public Vector3 PointVelocity(Vector3 worldPoint) => transform.forward * (float)RoyalVaultRules.BeltSpeed(belt, ObstacleClock.Now);

        public Quaternion DeltaRotation => Quaternion.identity;
    }
}
