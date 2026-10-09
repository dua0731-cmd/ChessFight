using UnityEngine;

namespace ChessFight.Gameplay.PawnRush
{
    // Sample mission: each team activates all seals with the existing F interaction.
    public sealed class MissionObjective : MonoBehaviour, IInteractable
    {
        public TeamMission mission;
        public string objectiveId = "seal_1";
        public Renderer whiteIndicator;
        public Renderer blackIndicator;
        MaterialPropertyBlock block;
        int previous = -1;
        public bool Interact(ICharacterDriver who, int team)
        {
            if (!isActiveAndEnabled || mission == null || who == null || !(who is ITeamMember member) || member.Team != team || who.FollowTarget == null) return false;
            if ((who.FollowTarget.position - transform.position).sqrMagnitude > 2.5f * 2.5f) return false;
            return mission.CompleteObjective(team, objectiveId);
        }
        void Update()
        {
            if (mission == null) return;
            int state = (mission.HasObjective(Teams.White, objectiveId) ? 1 : 0) | (mission.HasObjective(Teams.Black, objectiveId) ? 2 : 0);
            if (state == previous) return;
            previous = state;
            if (block == null) block = new MaterialPropertyBlock();
            Tint(whiteIndicator, (state & 1) != 0, Color.white);
            Tint(blackIndicator, (state & 2) != 0, new Color(.12f, .12f, .16f));
        }
        void Tint(Renderer indicator, bool complete, Color teamColor)
        {
            if (indicator == null) return;
            block.SetColor("_Color", complete ? new Color(.2f, .95f, .35f) : teamColor);
            indicator.SetPropertyBlock(block);
        }
    }
}
