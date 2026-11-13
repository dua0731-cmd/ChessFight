using System;
using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The timing a falling piece's effects share (R341, 11-04, W2 of the grand stair hall's falling knights): the piece
    /// hangs <see cref="height"/> m over its floor until <c>lead - fall</c> seconds after its warning starts, falls in
    /// <see cref="fall"/> seconds, speeding up, to <see cref="Rest"/> m at <see cref="lead"/>, and lies there
    /// <see cref="hold"/> seconds, as FallingChessPiece moves it. That obstacle is in Gameplay, which this assembly cannot
    /// see, so the game copies its values in (PawnRush/FallingPieceFx).
    /// </summary>
    [Serializable]
    public struct VfxDrop
    {
        /// <summary>Where the piece comes to rest over its floor (m), FallingChessPiece's landing height.</summary>
        public const float Rest = .15f;

        [Min(.1f), Tooltip("Seconds from the warning's start to the landing.")]
        public float lead;
        [Min(.05f), Tooltip("Seconds the fall takes.")]
        public float fall;
        [Min(.5f), Tooltip("Metres over its floor it falls from.")]
        public float height;
        [Min(0f), Tooltip("Seconds it lies there after landing.")]
        public float hold;

        /// <summary>The obstacle's own values: warning 1.4 s, fall 0.65 s from 9 m, shock 0.7 s.</summary>
        public static VfxDrop Default => new VfxDrop { lead = 2.05f, fall = .65f, height = 9f, hold = .7f };

        /// <summary>0 while it hangs, 1 as it lands.</summary>
        public float Progress(float age) => Mathf.Clamp01((age - (lead - fall)) / fall);

        /// <summary>Its height over its floor (m) <paramref name="age"/> seconds after the warning starts.</summary>
        public float Height(float age)
        {
            float d = Progress(age);
            return Mathf.Lerp(height, Rest, d * d);
        }
    }
}
