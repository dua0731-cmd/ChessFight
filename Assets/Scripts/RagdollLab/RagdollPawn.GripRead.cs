using UnityEngine;

namespace ChessFight.RagdollLab
{
    // R642 (승규 님 11-14, "잡혔다는 것에 대한 UX/UI 추가"): who has whom by a hand, and where the hand is, for the
    // Pawn Rush grab HUD (GripHudGui). Read only, nothing new on the wire.
    //   - The PC that simulates a pawn knows it exactly: its hand's joint and the held pawn (NotifyHeld).
    //   - A client's pawns are puppets: the snapshot carries only "this pawn holds something" (grabbing) and
    //     "a hand has this pawn" (held), so the pair is matched by distance: a grabbing pawn's palm right on a held
    //     pawn's body. Every pose is rebuilt from the same snapshot, so every PC finds the same pair.
    public partial class RagdollPawn
    {
        // How close a puppet's palm must be to a body of the held pawn to count as its hand (m, palm centre to
        // body origin: a chest or a thigh is about 0.3 m from its own origin to its skin).
        const float GripMatch = 0.55f;

        bool netGrabbing;   // a puppet's "a hand holds something" off the wire (pose.grabbing)

        Vector3 Palm(int slot) =>
            bodies[(int)(slot == 0 ? BodyId.HandL : BodyId.HandR)].transform.TransformPoint(palmLocal[slot]);

        /// <summary>One of this pawn's hands is on another pawn; <paramref name="grip"/> is that palm.</summary>
        public bool HoldingPawn(out Vector3 grip)
        {
            grip = default;
            if (NetworkPuppet)
            {
                if (!netGrabbing) return false;
                return NearestHeld(this, out grip, out _);
            }
            for (int slot = 0; slot < 2; slot++)
            {
                var hand = slot == 0 ? handL : handR;
                if (hand == null || !hand.IsHolding || hand.HeldCollider == null) continue;
                if (!ColliderOwner.TryGetValue(hand.HeldCollider, out var victim) || victim == this) continue;
                grip = hand.Center;
                return true;
            }
            return false;
        }

        /// <summary>Another pawn's hand is on this one; <paramref name="grip"/> is that palm.</summary>
        public bool HeldGrip(out Vector3 grip)
        {
            grip = default;
            if (!BeingHeld) return false;
            if (!NetworkPuppet && holder != null)
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    var hand = slot == 0 ? holder.handL : holder.handR;
                    if (hand == null || !hand.IsHolding || hand.HeldCollider == null) continue;
                    if (ColliderOwner.TryGetValue(hand.HeldCollider, out var victim) && victim == this)
                    {
                        grip = hand.Center;
                        return true;
                    }
                }
            }
            // A puppet (or a captor that has just let go this frame): the nearest grabbing palm on this body.
            float best = GripMatch;
            bool found = false;
            foreach (var other in All)
            {
                if (other == null || other == this || !other.GrabbingAnything) continue;
                for (int slot = 0; slot < 2; slot++)
                {
                    Vector3 palm = other.Palm(slot);
                    float d = NearestBody(palm);
                    if (d < best) { best = d; grip = palm; found = true; }
                }
            }
            return found;
        }

        bool GrabbingAnything => NetworkPuppet ? netGrabbing : Grabbing;

        // The held pawn a grabbing puppet's palm is on (for HoldingPawn on a client).
        static bool NearestHeld(RagdollPawn captor, out Vector3 grip, out RagdollPawn victim)
        {
            grip = default;
            victim = null;
            float best = GripMatch;
            foreach (var other in All)
            {
                if (other == null || other == captor || !other.BeingHeld) continue;
                for (int slot = 0; slot < 2; slot++)
                {
                    Vector3 palm = captor.Palm(slot);
                    float d = other.NearestBody(palm);
                    if (d < best) { best = d; grip = palm; victim = other; }
                }
            }
            return victim != null;
        }

        float NearestBody(Vector3 point)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                if (bodies[i] == null) continue;
                float d = (bodies[i].transform.position - point).magnitude;
                if (d < best) best = d;
            }
            return best;
        }
    }
}
