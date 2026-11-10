using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // R529: how loud a sound played at a world spot should be for this PC's listener, for sounds that are not placed
    // AudioSources of their own (the skill and pawn sounds play on shared 2D voices). Pawn Rush's SectionSoundGate fills
    // it in: inside a course section, heard only by a listener in the same section and quieter with distance; anywhere
    // else (the lobby, sword fight, a skill test) 1, as before.
    public static class CourseSoundPlace
    {
        public static Func<Vector3, float> Rule;

        public static float Gain(Vector3 at)
        {
            var rule = Rule;
            return rule != null ? Mathf.Clamp01(rule(at)) : 1f;
        }
    }
}
