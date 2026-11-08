using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skill annotations on the piece token (R128): in Sword Fight a cast's grade is not stamped on its own over the
    /// head but on the lower right of the token that already pops up with the cast (R102, R108), so the piece and its
    /// annotation read together. The token stays up as long as the stamp if it is still up; R132 (승규 님: "아이콘이 지금 2번
    /// 나와 f를 누르고 스킬을 사용할 때만 나와야 돼"): if it has already gone (a ring-out found after the cast ended) it does
    /// not pop again — the stamp shows on its own over the head.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>Keep <paramref name="pawn"/>'s token up for <paramref name="seconds"/> more if it is still up (never
        /// popped again: R132).</summary>
        public void HoldMarkForGrade(RagdollPawn pawn, float seconds)
        {
            if (pawn == null || kit == null || pawn.Piece == PieceKind.Pawn) return;
            if (!headMarks.TryGetValue(pawn, out var state) || state.fx == null || state.fx.dead) return;
            state.until = Mathf.Max(state.until, kit.Clock + seconds);
        }

        /// <summary>Where <paramref name="pawn"/>'s token stands now and how tall it is (m); false if it has none up.</summary>
        public bool MarkCenter(RagdollPawn pawn, out Vector3 at, out float size)
        {
            at = Vector3.zero;
            size = PieceMarkHeight;
            if (pawn == null || kit == null || !headMarks.TryGetValue(pawn, out var state) || state.fx == null || state.fx.dead) return false;
            var head = pawn.bodies[(int)BodyId.Head];
            if (head == null) return false;
            var col = head.GetComponent<Collider>();
            float top = col != null ? col.bounds.max.y : head.position.y + 0.12f;
            // R130: the 3D piece's middle (SwordFightSkillFx.Icons.cs).
            at = new Vector3(head.position.x, top + PieceMarkLift + PieceMarkHeight * 0.5f, head.position.z);
            return true;
        }
    }
}
