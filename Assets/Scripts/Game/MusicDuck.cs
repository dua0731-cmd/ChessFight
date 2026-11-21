using UnityEngine;

namespace ChessFight.Game
{
    /// <summary>
    /// How loud the race music may be now, 0..1 (JY-skill-lab, 2026-11-21): an ultimate's cast takes it down while it gathers,
    /// to nothing in its beat of silence, and lets it come back from the call (RagdollLab's PawnRushSkillFx.UltScreen.cs sets
    /// it; the race music multiplies its volume by it). 1 when nothing holds it.
    /// </summary>
    public static class MusicDuck
    {
        public static float Level = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Level = 1f;
    }
}
