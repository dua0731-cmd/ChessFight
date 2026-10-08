using UnityEngine;

namespace ChessFight.RagdollLab
{
    public partial class RagdollPawn
    {
        // Explicit mode entry point. Normal launch pads and shared movement never call it.
        // A previously downed ally needs active pose drives for feet-first cannon landing.
        // Do not teleport or clear the victim's struggle meter during the aiming phase.
        public void LaunchForKingRush(Vector3 velocity)
        {
            if (NetworkPuppet || Floating) return;
            State = PawnState.Active; StateFactor = Stiffness = 1;
            stateTimer = knockdownHold = knockdownLying = 0;
            Diving = false; SetRagdollFriction(false);
            Launch(velocity);
            FreeKingRushFlightTranslation();
        }
        public void FreeKingRushFlightTranslation()
        {
            // Keep rotational balance and all body collisions, but do not tow a ballistic
            // shot with the walking anchor. Normal Drives restores these next step on exit.
            var free = new JointDrive();
            anchorJoint.xDrive = anchorJoint.yDrive = anchorJoint.zDrive = free;
        }
    }
}
