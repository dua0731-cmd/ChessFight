using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Forwards physics contacts of one ragdoll body to its pawn.</summary>
    public class RagdollBodyPart : MonoBehaviour
    {
        public RagdollPawn owner;
        public BodyId id;

        void OnCollisionEnter(Collision c)
        {
            if (owner != null) owner.OnPartCollision(this, c, true);
        }

        void OnCollisionStay(Collision c)
        {
            if (owner != null) owner.OnPartCollision(this, c, false);
        }
    }
}
