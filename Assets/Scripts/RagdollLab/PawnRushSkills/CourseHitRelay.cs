using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 2026-11-17 (승규 "온라인 참가자 pc 에서도 다 들리게 해"): the course obstacles' hit sounds on every PC. A knock-back is given
    /// only where the pawn is simulated (offline, or the host), so <see cref="CourseHitSound.Hit"/> is called only there;
    /// this raises it as the pawn's <see cref="SkillFxKind.CourseHit"/> effect, which the host's timed effect stream sends
    /// on (LabNetLink, as the R652 movement moments) and each client raises again when it draws the pawn at that moment.
    /// Wherever the effect is raised - the simulating PC at once, every client on playback - it is handed back to the
    /// course to sound (<see cref="CourseHitSound.Hear"/>), so it plays once on each PC. Old builds skip the kind.
    /// </summary>
    static class CourseHitRelay
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            CourseHitSound.Send = Send;
            RagdollPawn.SkillFx -= OnFx;
            RagdollPawn.SkillFx += OnFx;
        }

        static bool Send(Component character, int kind, Vector3 at, float strength)
        {
            if (character == null) return false;
            var pawn = character.GetComponentInParent<RagdollPawn>();
            if (pawn == null) pawn = character.GetComponentInChildren<RagdollPawn>();
            if (pawn == null) return false;
            RagdollPawn.RaiseSkillFx(new SkillFxEvent
            {
                kind = SkillFxKind.CourseHit, by = pawn, at = at, dir = pawn.transform.forward,
                count = Mathf.Clamp(kind, 0, 255), size = Mathf.Clamp01(strength),
            });
            return true;
        }

        static void OnFx(SkillFxEvent e)
        {
            if (e.kind == SkillFxKind.CourseHit) CourseHitSound.Hear(e.count, e.at, e.size);
        }
    }
}
