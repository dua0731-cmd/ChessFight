using UnityEngine;

namespace ChessFight.ProtectKing
{
    public sealed class StageMap : MonoBehaviour
    {
        public CheckpointGate[] checkpoints;
        public Transform[] startPoints;
        public PlayerIdentity[] players;
        public ProtectTheKingMatchController match;
        public float killHeight = -7f;
        public float outerLimit = 35f;

        public CheckpointGate FindCheckpoint(int index, TeamId team)
        {
            foreach (var gate in checkpoints)
                if (gate.index == index && (!gate.teamRestricted || gate.team == team)) return gate;
            return null;
        }

        public void CheckTravel(PlayerMotor motor, Vector3 from, Vector3 to)
        {
            var player = motor.Identity;
            if (to.y < killHeight || Mathf.Abs(to.x) > outerLimit || to.z < -15 || to.z > 805)
            { Respawn(motor); return; }
            if (!match.IsRunning || player.checkpoint >= 5) return;
            var next = FindCheckpoint(player.checkpoint + 1, player.team);
            if (next == null) return;
            if (next.Crossed(from, to))
            {
                player.checkpoint = next.index;
                player.checkpointTimes[next.index] = match.Elapsed;
                match.Notify(player.DisplayName + " passed CP" + next.index);
            }
            // Flying above, bypassing the sides, or entering the other team's gate is invalid.
            else if (to.z > next.transform.position.z + 3)
            {
                match.Notify("Checkpoint order required - returning to CP" + player.checkpoint);
                Respawn(motor);
            }
        }

        public void Respawn(PlayerMotor motor, bool countFall = true)
        {
            var player = motor.Identity;
            var gate = FindCheckpoint(player.checkpoint, player.team);
            var point = gate != null ? gate.Recovery(player) : startPoints[player.playerId].position;
            if (countFall) player.falls++;
            motor.Teleport(point);
        }

        public void ResetPlayers()
        {
            foreach (var player in players)
            {
                player.checkpoint = 0;
                player.falls = 0;
                System.Array.Clear(player.checkpointTimes, 0, player.checkpointTimes.Length);
                Respawn(player.GetComponent<PlayerMotor>(), false);
            }
        }
    }
}
