using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Forwards physics contacts of one ragdoll body to its pawn.</summary>
    public class RagdollBodyPart : MonoBehaviour
    {
        public RagdollPawn owner;
        public BodyId id;

        // Physics callbacks: a knockdown from here lets go of the hands, and Unity refuses to destroy a
        // joint immediately inside one, so PawnHand is told to defer it.
        void OnCollisionEnter(Collision c)
        {
            if (owner == null) return;
            PawnHand.PhysicsCallbackDepth++;
            try { owner.OnPartCollision(this, c, true); }
            finally { PawnHand.PhysicsCallbackDepth--; }
        }

        void OnCollisionStay(Collision c)
        {
            if (owner == null) return;
            PawnHand.PhysicsCallbackDepth++;
            try { owner.OnPartCollision(this, c, false); }
            finally { PawnHand.PhysicsCallbackDepth--; }
        }
    }
}
