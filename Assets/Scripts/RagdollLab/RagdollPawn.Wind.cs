using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Being carried by a wind (IWindReceiver; an air vent with a drift speed, Pawn Rush grand stair hall
    /// R301). A vent that only pushed the bodies moved a running pawn about 0.2 m: the hips hang on the
    /// anchor spring (hipAnchorStrength ~4500 N/m), and 20 m/s² on a 50 kg pawn is 1000 N. Here the wind's
    /// velocity joins the frame the pawn runs in, like a moving floor's, so the anchor drifts with it. The pawn
    /// may run against it with its own speed (a sprint holds against a little less than the sprint speed).
    /// Eased in and out so a gust does not snap.
    /// </summary>
    public partial class RagdollPawn
    {
        const float WindEaseIn = 14f, WindEaseOut = 10f;   // m/s²

        Vector3 windVel;            // the drift being added to the frame now (horizontal)
        Vector3 windWant;           // what the vents asked for this physics step, summed
        float windStamp = -1f;      // the fixed time windWant was asked at

        /// <summary>The wind's drift under the pawn right now (for readouts).</summary>
        public Vector3 WindDrift => windVel;

        public bool Blow(Vector3 velocity)
        {
            if (State == PawnState.Ragdoll || Climbing || Floating || rope != null || hookPhase == HookPhase.Pulling) return false;
            if (windStamp != Time.fixedTime) { windWant = Vector3.zero; windStamp = Time.fixedTime; }
            windWant += Flat(velocity);
            return true;
        }

        // Before the frame is worked out. The vents may run before or after the pawn in a physics step, so the
        // last step's ask still counts.
        void UpdateWind(float dt)
        {
            bool asked = windStamp >= 0f && Time.fixedTime - windStamp <= Time.fixedDeltaTime * 1.5f;
            Vector3 want = asked && State != PawnState.Ragdoll ? windWant : Vector3.zero;
            float rate = want.sqrMagnitude > windVel.sqrMagnitude ? WindEaseIn : WindEaseOut;
            windVel = Vector3.MoveTowards(windVel, want, rate * dt);
        }
    }
}
