using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ChessFight.Gameplay.PawnRush
{
    // Local section state; future mission types report objective IDs here.
    // No inventory, player controller or victory rules are introduced.
    public sealed class TeamMission : MonoBehaviour
    {
        public string missionTitle = "세 봉인을 활성화하세요";
        public string[] objectiveIds = { "seal_1", "seal_2", "seal_3" };
        public UnityEvent<int> onTeamCompleted = new UnityEvent<int>();
        readonly uint[] progress = new uint[2];
        public bool Authority => !ObstacleClock.Shared && !PlaytestSpawner.NetworkDriven;
        public bool IsComplete(int team) => ValidTeam(team) && objectiveIds != null && objectiveIds.Length > 0 && objectiveIds.Length <= 32 && progress[team] == Required;
        uint Required => objectiveIds.Length == 32 ? uint.MaxValue : (1u << objectiveIds.Length) - 1;
        public static bool ValidTeam(int team) => team == Teams.White || team == Teams.Black;
        public bool HasObjective(int team, string id)
        { int index = Array.IndexOf(objectiveIds, id); return ValidTeam(team) && index >= 0 && index < 32 && (progress[team] & (1u << index)) != 0; }
        public void ValidateDefinition()
        {
            if (objectiveIds == null || objectiveIds.Length == 0 || objectiveIds.Length > 32)
                throw new InvalidOperationException(name + ": 미션 목표 ID는 1~32개입니다.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in objectiveIds)
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) throw new InvalidOperationException(name + ": 비어 있거나 중복된 목표 ID입니다.");
        }
        public bool CompleteObjective(int team, string id)
        {
            if (!isActiveAndEnabled || !Authority || !ValidTeam(team)) return false;
            int index = Array.IndexOf(objectiveIds, id);
            if (index < 0 || index >= 32 || IsComplete(team)) return false;
            uint bit = 1u << index;
            if ((progress[team] & bit) != 0) return false;
            progress[team] |= bit;
            if (IsComplete(team)) onTeamCompleted.Invoke(team);
            return true;
        }
        public void ResetProgress() { progress[0] = progress[1] = 0; }
        void Awake() { ValidateDefinition(); ResetProgress(); }
    }
}
