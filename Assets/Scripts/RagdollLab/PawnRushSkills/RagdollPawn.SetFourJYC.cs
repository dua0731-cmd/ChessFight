using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skills the user wrote (USER_SKILLS 4.4), bundle 3: the rook's giant, its strongman, its T wall and its wall
    /// building. Being built: until then a press only says so.
    /// </summary>
    public partial class RagdollPawn
    {
        void UpdateGiant(PawnRushSkillParams s, bool press, float dt) => JyNotYet(press, "거대화");
        void UpdateStrongman(PawnRushSkillParams s, bool press, float dt) => JyNotYet(press, "힘세지기");
        void UpdateTWall(PawnRushSkillParams s, bool press, float dt) => JyNotYet(press, "벨코즈 q 성벽");
        void UpdateWallBuild(PawnRushSkillParams s, bool press, float dt) => JyNotYet(press, "성벽 짓기");

        void JyNotYet(bool press, string name)
        {
            if (press) SkillDetail = $"{name}: 아직 만드는 중이에요";
        }

        void UpdateSetFourJYCStates(PawnRushSkillParams s, float dt) { }

        void ClearSetFourJYCStates() { }
    }
}
