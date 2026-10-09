using UnityEngine;

namespace ChessFight.PawnRush
{
    // Marks a collider whose top is walked on. The validator measures a path's width across
    // these (an obstacle must sweep all of it, design doc v0.4 §3).
    [DisallowMultipleComponent]
    public sealed class CourseFloor : MonoBehaviour
    {
        public bool sloped;
    }
}
