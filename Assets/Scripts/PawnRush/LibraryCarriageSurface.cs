using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    // R396 (승규 11-04 "컨베이어 벨트 북쪽 금색 쪽을 밟으면 충돌이 인식 되지 않고 떨어져"): the conveyor carriage's own
    // frame - the chassis (4.8 x 13.9 m, wider and longer than the belt's deck box), its end drums, side beads and
    // fittings - collides as drawn (Course01v4Builder.LibraryReplacement), and a pawn standing on it rides the
    // carriage's travel (no belt: that is the deck's, LibraryConveyorSurface, which a collider of the deck finds first).
    public sealed class LibraryCarriageSurface : MonoBehaviour, IMovingSurface
    {
        LibraryShuttleDriver driver;

        public Vector3 PointVelocity(Vector3 point)
        {
            if (driver == null) driver = GetComponentInParent<LibraryShuttleDriver>();
            return driver != null ? driver.CarriageVelocity : Vector3.zero;
        }

        public Quaternion DeltaRotation => Quaternion.identity;
    }
}
