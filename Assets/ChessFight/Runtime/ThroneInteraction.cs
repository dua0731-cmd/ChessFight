using UnityEngine;

namespace ChessFight.ProtectKing
{
    public sealed class ThroneInteraction : MonoBehaviour
    {
        public ProtectTheKingMatchController match;
        [Range(1.5f, 2f)] public float holdSeconds = 1.75f;
        public float interactionRadius = 3f;
        public float heightTolerance = 1.6f;
        PlayerIdentity candidate;
        float progress;
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
            if (!held || !IsEligible(player))
            { ResetProgress(); return; }
            if (candidate != player) { candidate = player; progress = 0; }
            progress += Mathf.Max(0, deltaTime);
            if (progress >= holdSeconds) match.TryFinish(player);
        }

        public bool IsComplete(PlayerIdentity player) => candidate == player &&
            progress >= holdSeconds && IsEligible(player);
        public void ResetProgress() { candidate = null; progress = 0; }
    }
}
