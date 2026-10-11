using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A skill that carries a pawn through the air (a leap, a dash, a flight a mode's skill drives) lands it on its feet:
    /// while the skill runs and for <see cref="SkillLandingGrace"/> seconds after, hitting the floor is not an impact that
    /// knocks it down (승규, 10-11: "스킬을 사용한 후 착지하는 스킬들이 넘어지는 경우가 있어"). Walls, other pawns and
    /// hazards still count, and a skill or a hit that knocks it down on purpose still does.
    /// Pawn Rush and Queen of the Hill skills (their stage on the pawn, the Queen of the Hill arc that also carries a
    /// swapped ally), the outer dash and the outer flight keep it
    /// on by themselves; a skill that lives elsewhere (Sword Fight) calls <see cref="KeepSkillLandingSafe"/> each step.
    /// </summary>
    public partial class RagdollPawn
    {
        public const float SkillLandingGrace = 0.6f;
        float skillLandSafe;

        /// <summary>Floor impacts do not knock this pawn down for the next <paramref name="seconds"/>.</summary>
        public void KeepSkillLandingSafe(float seconds = SkillLandingGrace)
        {
            if (seconds > skillLandSafe) skillLandSafe = seconds;
        }

        bool SkillLandingSafe => skillLandSafe > 0f;

        void UpdateSkillLanding(float dt)
        {
            if (dashing || flight || flying || skillStage != SkillStage.None) KeepSkillLandingSafe();
            else if (skillLandSafe > 0f) skillLandSafe -= dt;
        }
    }
}
