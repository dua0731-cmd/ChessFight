using UnityEngine;

namespace ChessFight.ProtectKing
{
    public sealed class ThroneInteraction : MonoBehaviour
    {
        public ProtectTheKingMatchController match;
        [Range(1.5f, 2f)] public float holdSeconds = 1.75f;
        public float interactionRadius = 3f;
        public float heightTolerance = 1.6f;
        readonly float[] playerProgress = new float[12];
        float progress; // Highest progress, for the legacy local HUD.
        public float Fraction => Mathf.Clamp01(progress / holdSeconds);

        public bool IsEligible(PlayerIdentity player)
        {
            if (player == null || !match.IsRunning || player.piece != PieceType.King || player.checkpoint != 5) return false;
            var delta = player.transform.position - transform.position;
            if (Mathf.Abs(delta.y) > heightTolerance ||
                new Vector2(delta.x, delta.z).sqrMagnitude > interactionRadius * interactionRadius) return false;
            return !Physics.Linecast(player.transform.position + Vector3.up * 1.2f,
                transform.position + Vector3.up * 1.2f, 1 << 0, QueryTriggerInteraction.Ignore);
        }

        public void TickInteraction(PlayerIdentity player, bool held, float deltaTime)
        {
            if (!match.HasAuthority || player == null || player.playerId < 0 || player.playerId >= 12) return;
            int slot = player.playerId;
            if (!held || !IsEligible(player))
            { ResetPlayer(player); return; }
            
            playerProgress[slot] += Mathf.Max(0, deltaTime);
            progress = Mathf.Max(progress, playerProgress[slot]);
            if (playerProgress[slot] >= holdSeconds) match.TryFinish(player);
        }

        public bool IsComplete(PlayerIdentity player) => player != null && player.playerId >= 0 && player.playerId < 12 &&
            playerProgress[player.playerId] >= holdSeconds && IsEligible(player);
        public float GetFraction(int slot) => Mathf.Clamp01(playerProgress[slot] / holdSeconds);
        public void ApplyRemote(int slot, float fraction) { if(!match.HasAuthority) playerProgress[slot] = Mathf.Clamp01(fraction)*holdSeconds; }
        public void ResetPlayer(PlayerIdentity player) {
            if(player == null)return;
            playerProgress[player.playerId]=0;progress=0;
            foreach(float value in playerProgress)progress=Mathf.Max(progress,value);
        }
        public void ResetProgress() { System.Array.Clear(playerProgress,0,12); progress = 0; }
    }
}
