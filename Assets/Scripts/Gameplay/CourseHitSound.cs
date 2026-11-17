using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // 2026-11-17 (승규 "온라인 참가자 pc 에서도 다 들리게 해"): a course obstacle knocking a character away, as a moment every
    // PC hears. A knock-back is given only on the PC that moves the character (offline, or a match's host), so its sound
    // has to be sent: the course (Pawn Rush's yard) calls Hit there; the character's own assembly (ChessFight.RagdollLab,
    // CourseHitRelay) fills in Send, which raises it as one of the pawn's timed effects - the host's effect stream sends
    // those on, and each client raises it again at the moment it draws the pawn there - and calls Hear wherever the
    // effect is raised (here and on every client), where the course plays it (Heard). With no Send (a character that is
    // not a ragdoll), Hit plays it here only. Gameplay knows nothing of what the kinds sound like: `kind` is the course's
    // own number, `strength` 0..1 how hard.
    public static class CourseHitSound
    {
        /// <summary>Raise the hit as the character's timed effect; false when this character cannot (then it is only
        /// heard here). Set by the character's assembly.</summary>
        public static Func<Component, int, Vector3, float, bool> Send;

        /// <summary>A hit to sound now: its kind, where, how hard. On every PC, at the moment it shows the hit.</summary>
        public static event Action<int, Vector3, float> Heard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Send = null;
            Heard = null;
        }

        /// <summary>On the PC that gave the knock-back: send it on (every PC hears it), or hear it here only.</summary>
        public static void Hit(Component character, int kind, Vector3 at, float strength)
        {
            var send = Send;
            if (send != null && character != null && send(character, kind, at, strength)) return;
            Hear(kind, at, strength);
        }

        /// <summary>Where the effect is raised (the character's assembly): sound it.</summary>
        public static void Hear(int kind, Vector3 at, float strength) => Heard?.Invoke(kind, at, Mathf.Clamp01(strength));
    }
}
