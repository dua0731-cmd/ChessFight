using UnityEngine;

namespace ChessFight.ChessBoxingPrototype
{
    // Runtime-only relay on the prototype's physical gloves; shared pawn colliders are untouched.
    public sealed class CBGloveContact : MonoBehaviour
    {
        [SerializeField] CBFighter owner;
        [SerializeField] Rigidbody hand;

        public void Bind(CBFighter fighter, Rigidbody glove)
        {
            owner = fighter;
            hand = glove;
        }

        void OnCollisionEnter(Collision collision) => Report(collision);
        void OnCollisionStay(Collision collision) => Report(collision);

        void Report(Collision collision)
        {
            if (owner != null && hand != null) owner.RegisterGloveContact(hand, collision);
        }
    }
}
