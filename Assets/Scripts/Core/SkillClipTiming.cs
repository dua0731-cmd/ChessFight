using System;

namespace ChessFight.Network
{
    /// <summary>
    /// Where a skill's animation clip is (R190, the Sword Fight skills played from Mixamo clips): the clip is cut into
    /// three spans by four marks in it (seconds), one span per stage of the skill - the windup from mark 0 to mark 1, the
    /// release (the dash, the flight, the cut) from mark 1 to mark 2, the recovery from mark 2 to mark 3. So the blow in
    /// the clip lands where the skill's own does, whatever the stage lengths are tuned to.
    /// <list type="bullet">
    /// <item>A stage whose length is known (a windup, a recovery) plays its span stretched over that length.</item>
    /// <item>A stage of no set length (the king's guard waits for a blow; the queen's dash runs as far as it goes) plays
    /// its span at the clip's own speed and holds the span's last frame once it is through.</item>
    /// </list>
    /// No Unity here, so the tests run it on Linux.
    /// </summary>
    public static class SkillClipTiming
    {
        public const int Windup = 0, Active = 1, Recovery = 2;

        /// <summary>The clip time (s) for <paramref name="stageTime"/> seconds into stage <paramref name="stage"/>
        /// (<see cref="Windup"/>, <see cref="Active"/>, <see cref="Recovery"/>) that lasts
        /// <paramref name="stageLength"/> seconds (0 or less: not set). <paramref name="marks"/> holds the four marks; out
        /// of order or missing marks are put in order and inside [0, <paramref name="clipLength"/>].</summary>
        public static float At(float[] marks, int stage, float stageTime, float stageLength, float clipLength)
        {
            if (stage < Windup || stage > Recovery) throw new ArgumentOutOfRangeException(nameof(stage));
            float length = Math.Max(0f, clipLength);
            float from = Mark(marks, stage, length), to = Math.Max(from, Mark(marks, stage + 1, length));
            for (int i = 0; i < stage; i++) from = Math.Max(from, Mark(marks, i, length));
            to = Math.Max(to, from);
            float t = Math.Max(0f, stageTime);
            if (stageLength > 0f) return from + (to - from) * Math.Min(1f, t / stageLength);
            return Math.Min(to, from + t);
        }

        /// <summary>Mark <paramref name="i"/> inside the clip; a missing one is spread evenly (0, a third, two thirds, the end).</summary>
        public static float Mark(float[] marks, int i, float clipLength)
        {
            float m = marks != null && i < marks.Length && !float.IsNaN(marks[i]) ? marks[i] : clipLength * i / 3f;
            return Math.Min(clipLength, Math.Max(0f, m));
        }

        /// <summary>The weight a clip is played with <paramref name="time"/> seconds into the skill when it takes
        /// <paramref name="blendIn"/> seconds to come in: eased, never more than <paramref name="weight"/>.</summary>
        public static float In(float time, float blendIn, float weight)
        {
            float w = Math.Max(0f, Math.Min(1f, weight));
            if (blendIn <= 0f) return w;
            float k = Math.Max(0f, Math.Min(1f, time / blendIn));
            return w * k * k * (3f - 2f * k);
        }
    }
}
