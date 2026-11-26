using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skills the user wrote (USER_SKILLS 4.2), bundle 2: the bishop's thrown holy sword, its bow at the leader and its
    /// pin beam. Being built: until then a press only says so.
    /// </summary>
    public partial class RagdollPawn
    {
        void UpdateSwordThrow(PawnRushSkillParams s, bool press, float dt) => JyNotYet(press, "성검 던지기");
        void UpdateKettleBow(PawnRushSkillParams s, bool press, float dt) => JyNotYet(press, "케틀 궁");
        void UpdatePinBeam(PawnRushSkillParams s, bool press, float dt) => JyNotYet(press, "핀!");

        void JyNotYet(bool press, string name)
        {
            if (press) SkillDetail = $"{name}: 아직 만드는 중이에요";
        }

        void UpdateSetFourJYBStates(PawnRushSkillParams s, float dt) { }

        void ClearSetFourJYBStates() { }
    }
}
