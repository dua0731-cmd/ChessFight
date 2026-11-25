using ChessFight.Game;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The lessons' sounds (user 11-25 "2번 3번 진행" of the tutorial sound survey): all from sounds the game already has,
    /// none new. A lesson done: the rank-complete stamp (design 2-6 "끝나면 도장"); the next lesson's gold square lighting up,
    /// a beat later; the 20 s hint and the opening "튜토리얼" banner: the menu's own sounds (MenuSound, which keeps the
    /// user's UI volume); the Esc menu's refused leave: the blocked tick; the whole tutorial done: the mini-game
    /// complete. A fall: the fall sound, and when the checkpoint puts the pawn back, the respawn sound - the
    /// tutorial has no PawnRushFallNotice, which plays those in a round. They play flat (2D) from one voice on the course.
    /// </summary>
    public sealed partial class TutorialCourse
    {
        float chimeAt = -1f, respawnAt = -1f;
        bool hintHeard, bannerHeard;

        // ArenaFx is in the PawnRush assembly, which this one does not reference: the same files (Resources/PawnRushMiniGameSfx),
        // played flat from one voice here.
        readonly System.Collections.Generic.Dictionary<string, AudioClip> cueClips = new System.Collections.Generic.Dictionary<string, AudioClip>();
        AudioSource cueVoice;

        void Cue(string name, float volume, float pitch = 1f)
        {
            if (Application.isBatchMode) return;
            if (!cueClips.TryGetValue(name, out var clip))
                cueClips[name] = clip = Resources.Load<AudioClip>(name.Contains("/") ? name : "PawnRushMiniGameSfx/" + name);
            if (clip == null) return;
            if (cueVoice == null)
            {
                cueVoice = gameObject.AddComponent<AudioSource>();
                cueVoice.playOnAwake = false;
                cueVoice.spatialBlend = 0f;
            }
            cueVoice.pitch = pitch * Random.Range(.97f, 1.03f);
            cueVoice.PlayOneShot(clip, volume);
        }

        // The ledge catch '톡탁', the struggle clicks' '탁' and judder and the held pawn's '뿍' (user 11-25, picks 21, 77
        // and 82) are the game's own now (RagdollPawn.SoundGrab, PawnRushCourseSkills.Mash, PawnRushSkillSfx): the
        // tutorial has them from there.


        /// <summary>The plank bridge's footsteps (user 11-25 "외나무다리 이 소리가 아니야", the rope creak taken off): its
        /// colliders get a WoodFloor, so PawnRushStepSfx plays the woody knock on it. The beam is the lab's own
        /// (RagdollLabBuilder "Beam 0.6m", 0.5 m thick under y 0, LabLayout.BeamStartZ to BeamEndZ).</summary>
        void MarkBridge()
        {
            var middle = new Vector3(0f, -.25f, (LabLayout.BeamStartZ + LabLayout.BeamEndZ) * .5f);
            var half = new Vector3(LabLayout.BeamWidth * .4f, .2f, (LabLayout.BeamStartZ - LabLayout.BeamEndZ) * .45f);
            foreach (var c in Physics.OverlapBox(middle, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (c.attachedRigidbody == null && c.GetComponent<ChessFight.Gameplay.WoodFloor>() == null)
                    c.gameObject.AddComponent<ChessFight.Gameplay.WoodFloor>();
        }

        /// <summary>A lesson was done (before the next one begins).</summary>
        void CueDone()
        {
            Cue("sfx_rank_complete", .8f);
            chimeAt = Time.time + .45f;
        }

        /// <summary>The whole tutorial was done.</summary>
        void CueGoal() => Cue("sfx_mg_complete", .85f);

        /// <summary>A lesson began (resets what its own clock plays).</summary>
        void CueBegan() => hintHeard = false;

        /// <summary>The Esc menu's leave was refused (lesson 7 is not done).</summary>
        void CueRefused() => Cue("sfx_paint_blocked", .7f);

        /// <summary>The player fell into a kill volume; it comes back after `delay` seconds.</summary>
        void CueFell(float delay)
        {
            Cue("sfx_fall", .8f);
            respawnAt = Time.time + delay;
        }

        /// <summary>Every frame the lessons run (not during the opening).</summary>
        void CueUpdate()
        {
            if (!bannerHeard && current == 0)
            {
                bannerHeard = true;
                MenuSound.Play(MenuSound.Kind.Open);
            }
            if (chimeAt >= 0f && Time.time >= chimeAt) { chimeAt = -1f; Cue("sfx_ep_ready", .6f); }
            if (respawnAt >= 0f && Time.time >= respawnAt) { respawnAt = -1f; Cue("sfx_respawn", .7f); }
            if (!hintHeard && current >= 0 && Time.time - lessonStart >= HintAfter)
            {
                hintHeard = true;
                MenuSound.Play(MenuSound.Kind.Toggle);
            }
        }
    }
}
