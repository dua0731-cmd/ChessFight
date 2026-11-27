using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // R863 (2026-11-27, "소드 파이트 인게임 BGM"): the match drives its music (Game/SwordFightMusic.cs). It plays from the
    // start of 3·2·1 through sudden death; the result stops it (ResultMusic takes over) and so does a new round. It ducks
    // under what the fight is about: a sword cut landing (SwordFightPawn.Cut), a skill's hit (SwordFightSkills.Fx) and,
    // deepest, a point (the score going up). Every one of these is seen on every PC (the cut and the skills' effects on
    // every machine, the score in the host's state), so every screen hears the same thing: no protocol change.
    public sealed partial class SwordFightMatch
    {
        // How far the music drops and for how long (unscaled seconds) before it comes back (SwordFightMusic.Duck).
        const float DuckHit = .72f, DuckHitHold = .2f;
        const float DuckSkill = .65f, DuckSkillHold = .3f;
        const float DuckPoint = .45f, DuckPointHold = .7f;

        SwordFightMusic music;
        int musicScore = -1;

        void StartMusic()
        {
            if (music == null) music = SwordFightMusic.On(gameObject);
            SwordFightPawn.Cut += OnMusicCut;
            SwordFightSkills.Fx += OnMusicFx;
        }

        void StopMusicHooks()
        {
            SwordFightPawn.Cut -= OnMusicCut;
            SwordFightSkills.Fx -= OnMusicFx;
        }

        void StepMusic()
        {
            if (music == null) return;
            var phase = view.Phase;
            // From 3·2·1's start - the moment the entrance music is let go (Update's SwordMusicCarry.Release) - so the two
            // cross-fade with no gap, through sudden death.
            music.Play(Live(phase) || (phase == SwordPhase.Countdown && !Flying));
            int score = view.White + view.Black;
            if (musicScore >= 0 && score > musicScore && Live(phase)) music.Duck(DuckPoint, DuckPointHold);
            musicScore = score;
        }

        void OnMusicCut(SwordFightPawn victim, SwordFightPawn by)
        {
            if (music != null) music.Duck(DuckHit, DuckHitHold);
        }

        void OnMusicFx(SfFxEvent e)
        {
            if (music == null) return;
            switch (e.kind)
            {
                case SfFxKind.KingCounterHit: case SfFxKind.KingRepel: case SfFxKind.QueenHit: case SfFxKind.RookHit:
                case SfFxKind.BishopPin: case SfFxKind.KnightHit: case SfFxKind.KingLandHit: case SfFxKind.QueenCut:
                case SfFxKind.KnightKickHit: case SfFxKind.PawnHit: case SfFxKind.RookBreak: case SfFxKind.BishopHit:
                case SfFxKind.BishopSlideHit:
                    music.Duck(DuckSkill, DuckSkillHold);
                    break;
            }
        }
    }
}
