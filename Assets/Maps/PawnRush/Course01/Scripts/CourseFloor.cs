using UnityEngine;

namespace ChessFight.PawnRush
{
    // Marks a collider whose top is walked on, for the validator: two floors stacked one over
    // the other must be at least 9 m apart (design doc v0.2 §3), and no team zone may overlap a
    // shared floor. Sloped ones (ramps) are only reported, not failed.
    [DisallowMultipleComponent]
    public sealed class CourseFloor : MonoBehaviour
    {
        public bool sloped;
    }
}
