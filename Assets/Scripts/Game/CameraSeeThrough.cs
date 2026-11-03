using UnityEngine;

namespace ChessFight.Game
{
    // A collider the follow cameras do not pull in for while this is switched on (R287, the Royal Carriage Yard): a
    // moving part that sweeps between the camera and its pawn - a charging carriage, a giant waterwheel's spoke - and
    // would otherwise yank the view in for the moment it passes. The camera looks through it instead. Its owner
    // switches it off while the part stands still (a carriage turning or aiming), so the camera pulls in as for a
    // wall. Only colliders that carry it: walls and floors pull the camera in as before.
    public sealed class CameraSeeThrough : MonoBehaviour
    {
        public static bool Skips(Collider collider) => collider != null && collider.TryGetComponent(out CameraSeeThrough see) && see.enabled;
    }
}
