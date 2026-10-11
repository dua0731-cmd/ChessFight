using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A flight another mode's skill drives from outside (the Sword Fight edge skills, R103: the king hanging on his
    /// sword and vaulting back up, the bishop's hands pulling a falling ally in, the knight's somersault). The skill hands
    /// the hips a target every step - where they should be and how fast that point moves - and the whole body is carried
    /// there: its velocity is steered so the hips follow (gravity taken out), every limb keeping its own swing, and the
    /// locomotion anchor rides along so it never pulls back. The body can also be turned about a flat axis (a flip, a
    /// lean into a kick): the anchor's balance target turns and the bodies are given the turn's spin. A knocked-down
    /// pawn is carried limp. The skill ends it with <see cref="EndFlight"/>; a flight nobody asks for this step ends by
    /// itself. Runs each step right after the run, from <see cref="UpdateOuterDash"/>.
    /// </summary>
    public partial class RagdollPawn
    {
        bool flight;
        int flightAsked;            // the physics step the target was last set for
        Vector3 flightPos, flightVel, flightAxis = Vector3.right;
        float flightTurn, flightGain = 14f;
        Vector3 spinApplied;        // the spin (rad/s about an axis) the bodies were last given

        /// <summary>A flight started with <see cref="Fly"/> is carrying the pawn.</summary>
        public bool OuterFlying => flight;

        /// <summary>Carry the hips to <paramref name="hips"/> this step (the point moving at <paramref name="velocity"/>),
        /// the body turned <paramref name="turn"/> degrees about the flat <paramref name="axis"/> (default: the pawn's own
        /// right; 0 = upright). Call it every step while the flight lasts; <paramref name="gain"/> is how hard (1/s) the
        /// hips are pulled back onto the point.</summary>
        public void Fly(Vector3 hips, Vector3 velocity, float turn = 0f, Vector3 axis = default, float gain = 14f)
        {
            if (NetworkPuppet) return;
            if (!flight)
            {
                // Off the wall, out of anyone's hands, nothing held: the flight is the only thing moving it.
                LetGo(0.3f);
                ReleaseHolders(0.5f);
                heldTimer = struggleTimer = escapeProgress = 0f;
                holder = null;
                heldCollider = null;
                spinApplied = Vector3.zero;
            }
            flight = true;
            flightAsked = StepCount;
            flightPos = hips;
            flightVel = velocity;
            flightTurn = turn;
            flightAxis = axis.sqrMagnitude > 1e-6f ? Flat(axis).normalized : Vector3.Cross(Vector3.up, facing).normalized;
            flightGain = gain;
        }

        /// <summary>End the flight. The body keeps the speed it has, or every part is set to
        /// <paramref name="velocity"/>; a turn still under way is taken back off.</summary>
        public void EndFlight(Vector3? velocity = null)
        {
            if (!flight) return;
            flight = false;
            foreach (var rb in bodies)
            {
                if (velocity.HasValue) rb.linearVelocity = velocity.Value;
                rb.angularVelocity -= spinApplied;
            }
            spinApplied = Vector3.zero;
            freeFlight = Mathf.Max(freeFlight, 0.25f);
            anchorPos = bodies[0].position;
            anchorVel = Flat(bodies[0].linearVelocity);
        }

        /// <summary>A knocked-down pawn starts getting up at once, wherever it is (it stiffens over getUpBlendTime).</summary>
        public void StandUp()
        {
            if (NetworkPuppet || Floating || State != PawnState.Ragdoll) return;
            BeginGetUp(P);
        }

        /// <summary>Face this way from now on (a skill turning the pawn while it is carried).</summary>
        public void Face(Vector3 direction)
        {
            Vector3 f = Flat(direction);
            if (f.sqrMagnitude > 1e-6f) facing = f.normalized;
        }

        /// <summary>Physics steps run so far (the flight ends if a step goes by without anyone asking for it).</summary>
        static int StepCount => Mathf.RoundToInt(Time.fixedTime / Mathf.Max(1e-4f, Time.fixedDeltaTime));

        void UpdateOuterFlight(float dt)
        {
            if (!flight) return;
            if (NetworkPuppet || StepCount - flightAsked > 1)
            {
                EndFlight();
                return;
            }
            Rigidbody hips = bodies[0];
            Vector3 want = flightVel + (flightPos - hips.position) * flightGain;
            if (want.sqrMagnitude > 30f * 30f) want = want.normalized * 30f;
            // The physics step adds gravity after this: ask for that much less, and the hips come out at want.
            AddVelocity(want - Physics.gravity * dt - hips.linearVelocity);

            // The turn: the balance target turns, and the whole body is spun about the hips by the change in the turn's
            // rate, so a stiff pawn flips as one piece instead of the drives dragging it round.
            Quaternion upright = Quaternion.LookRotation(facing, Vector3.up);
            Quaternion target = Mathf.Abs(flightTurn) > 0.01f ? Quaternion.AngleAxis(flightTurn, flightAxis) * upright : upright;
            Vector3 spin = SpinTowards(hips.rotation, target, dt);
            Vector3 change = spin - spinApplied;
            if (change.sqrMagnitude > 1e-6f)
            {
                Vector3 c = hips.position;
                foreach (var rb in bodies)
                {
                    rb.angularVelocity += change;
                    rb.linearVelocity += Vector3.Cross(change, rb.position - c);
                }
            }
            spinApplied = spin;

            // The anchor rides with the hips (no pull either way) and holds the turned balance target.
            anchorPos = hips.position + want * dt;
            anchorVel = Flat(want);
            anchor.MovePosition(anchorPos);
            anchor.MoveRotation(target);
            carryVel = Vector3.zero;
            carryRise = 0f;
            launched = false;
            freeFlight = Mathf.Max(freeFlight, 0.2f);
            airTimer = Mathf.Max(airTimer, 0.1f);
            coyote = 0f;
            riseBudget = 0f;
        }

        /// <summary>The spin (rad/s) that turns <paramref name="from"/> onto <paramref name="to"/> over a few steps, capped
        /// (none while knocked down: a limp body just tumbles).</summary>
        Vector3 SpinTowards(Quaternion from, Quaternion to, float dt)
        {
            if (State == PawnState.Ragdoll || dt <= 0f) return Vector3.zero;
            Quaternion delta = to * Quaternion.Inverse(from);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (float.IsNaN(axis.x) || axis.sqrMagnitude < 1e-8f) return Vector3.zero;
            if (angle > 180f) angle -= 360f;
            if (Mathf.Abs(angle) < 0.5f) return Vector3.zero;
            float rate = Mathf.Clamp(angle * Mathf.Deg2Rad / (3f * dt), -40f, 40f);
            return axis.normalized * rate;
        }
    }
}
