using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A dash another mode's skill drives from outside (the Sword Fight queen's 꼬치 베기, R99): the Pawn Rush dash's own
    /// driver (a fixed speed along a fixed flat line with the anchor carried along, so the piece runs on its feet instead
    /// of being thrown and landing at full speed; contacts with pieces are the skill's, not a collision knockdown), run
    /// each step after the run, where the Pawn Rush skills run theirs.
    /// </summary>
    public partial class RagdollPawn
    {
        bool outerDash;

        /// <summary>A dash started with <see cref="BeginDash"/> is running.</summary>
        public bool OuterDashing => outerDash && dashing;

        /// <summary>Dash flat along <paramref name="dir"/> at <paramref name="speed"/> m/s until <see cref="StopDash"/>
        /// (or a knockdown, a teleport).</summary>
        public void BeginDash(Vector3 dir, float speed)
        {
            if (NetworkPuppet || State != PawnState.Active) return;
            skillDir = FlatDir(dir);
            StartDash(speed, float.MaxValue);
            outerDash = true;
        }

        /// <summary>End the dash and plant the feet: <paramref name="keep"/> of its speed carries on into the stop.</summary>
        public void StopDash(float keep = 0.2f)
        {
            if (!outerDash) return;
            outerDash = false;
            if (dashing && State == PawnState.Active)
            {
                Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
                AddVelocity(-own * (1f - keep));
                anchorVel = carryVel + own * keep;
            }
            EndDash();
        }

        void UpdateOuterDash(float dt)
        {
            // The grace after a dash (contacts still the skill's) is counted down by the Pawn Rush skills when there are
            // any; without them, here.
            if (PawnRushSkills == null) skillGrace -= dt;
            UpdateOuterFlight(dt);   // a flight another mode's skill drives (the Sword Fight edge skills, R103)
            if (!outerDash) return;
            if (State != PawnState.Active || !dashing)
            {
                outerDash = false;
                EndDash();
                return;
            }
            DriveDash(dt);
        }
    }
}
