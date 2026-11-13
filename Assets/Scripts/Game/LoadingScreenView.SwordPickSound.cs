using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // R631 (11-13): Sword Fight's entrance-screen sound (LoadingScreenView.SwordPick.cs). 승규 님 asked for a matching
    // sound like Pawn Rush's but a little tenser ("폰 러쉬 보다는 약간의 긴장감이 있는 사운드"), picked A (blade duel) of
    // the first mockup page, asked for it a little more game-matchmaking-like ("A가 좋아 여기서 좀만 매칭 스럽게") and
    // picked A2 of the second: game-UI matchmaking takes (a swoosh into a snap, a tight sub thump, a dry click countdown,
    // a pulse beating every second underneath) in front, the blade takes layered under them.
    // The clips are Resources/Loading/SwordPick/Sound/Sword*.wav, baked from the ElevenLabs takes in
    // ArtSource/Loading/SwordPick/Sound/raw by bake.py, which also prints each clip's hit (the Lead* constants) and the
    // gains below (the page's balance, the clash at full). The pick's own moments reuse Pawn Rush's clips.
    // Cues are put on the audio clock a little ahead (as the course reveal's, LoadingScreenView.RevealSound.cs), at the
    // same times the picture moves:
    //   ① 0 s        the screen opens: a rising whoosh into a sub hit and a blade's shing
    //      plates    each team's plate swooshes in from its own side and snaps into place (left ours, right theirs)
    //      VS        a hard thump with a choked sword clang as it slams
    //      squares   a soft tap as each fills (Pawn Rush's)
    //   ②            the pick cards rise on a swipe and thump; marking a card is Pawn Rush's tick, confirming it the
    //                steel bolt, a teammate's pick a softer tick from their side
    //      last 5 s  a dry click countdown, louder each second
    //      close     a lock-in slam: a snap, a sheathed sword, a deep thump
    //   ③            the title swipes in, each card thumps down
    //   ④            a reverse whoosh into the clash's boom and steel crack where the halves meet, the VS a little after
    //   music        R634 (11-13, "ost도 시안 줘" → "소드 파이트 느낌나는 ost로" → "4번으로" → "아니야 1번으로"): O1 "칼싸움 게임 비트" of the
    //                OST page (a driving sword-fight game beat, punchy drums, blade hits on the beat) in place of
    //                R631's pulse bed, from the open, swelling through the last seconds of the pick, a little lower after
    //                the close, ducked hard on the clash and back up, and on until the match really starts (it fades with
    //                the screen, as Pawn Rush's music does). SwordMusic is the take up to its loop point,
    //                SwordMusicLoop fourteen bars (1.72 s each) that follow it seamlessly and repeat (bake.py), both on the
    //                audio clock so the join is sample exact. It follows the music volume, the cues the effects one.
    // ③ and ④ hang on the close, which is this PC's own moment when every player picked early: they are timed from the
    // close (the end of the pick once it is that near, or the moment the picks closed early).
    public sealed partial class LoadingScreenView
    {
        const string SwordSoundPath = "Loading/SwordPick/Sound/";
        // where each clip's hit is (bake.py)
        const float LeadSwordPlate = .043f, LeadSwordVs = .067f, LeadSwordClose = .0112f, LeadSwordCard = .0785f,
                    LeadSwordClash = .43f;
        // the page's gains (bake.py's, scaled so the clash is 1)
        const float GainOpen = .891f, GainPlate = .447f, GainVs = .794f, GainTick = .562f, GainClose = .841f,
                    GainCard = .501f, GainClash = 1f, GainMark = .282f, GainLock = .631f, GainMate = .2f, GainDot = .178f;
        // the music (RMS -24 dB under the clash's peak on the page, at the default volumes: bake.py's gain); on the clash it
        // drops to DuckLow, then comes back to DuckBack over DuckRise from DuckHold after the hit
        const float SwordMusicGain = .1861f, BedSwellFrom = 14.5f, BedSwell = .4f, BedAfterClose = .85f;
        const float DuckLow = .3f, DuckBack = .75f, DuckHold = .3f, DuckRise = 1.2f;
        // The five last seconds of the pick tick, quieter first.
        const int SwordTicks = 5;
        const float TickSoft = .55f;
        // ① when each plate is first in place (it slides in for .3 s), the VS's slam (.18 s from FoundVsAt)
        const float PlateSlide = .3f, VsSlam = .18f;
        // ② the cards finish rising; ③ the title is in, each card lands (SyncTeam); ④ the VS lands (.2 s from ClashVsAt)
        const float CardsUp = .3f, TeamTitleIn = .24f, TeamCardAt = .3f, TeamCardStep = .07f, ClashVsLand = .2f;

        List<RevealCue> swordCues, swordAfterCues;
        AudioSource[] swordVoices;
        double[] swordVoiceFree;
        float swordSoundUpTo = float.NegativeInfinity, swordAfterUpTo = float.NegativeInfinity;
        // the close the cues after it are timed from, -1 while not known yet
        float swordSoundClose = -1f;
        AudioSource swordMusic, swordMusicLoop;
        bool swordBedOn;
        readonly List<PieceKind> swordHeard = new List<PieceKind>();
        int swordHeardMine = -1;

        // Sword* from this screen's folder, Reveal* (Pawn Rush's) from the course reveal's; loaded once per run.
        static AudioClip SwordClip(string name)
        {
            if (name.StartsWith("Reveal")) return RevealClip(name);
            string key = "Sword:" + name;
            if (RevealClips.TryGetValue(key, out var clip) && clip != null) return clip;
            clip = Resources.Load<AudioClip>(SwordSoundPath + name);
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            RevealClips[key] = clip;
            return clip;
        }

        static void WarmSwordClips()
        {
            foreach (var name in new[] { "SwordOpen", "SwordPlate", "SwordVs", "SwordTick", "SwordClose", "SwordCard", "SwordClash", "SwordMusic", "SwordMusicLoop" })
                SwordClip(name);
            foreach (var name in new[] { "RevealTick0", "RevealTick1", "RevealLock", "RevealDot" }) RevealClip(name);
        }

        static void AddCue(List<RevealCue> cues, string clip, float hit, float lead, float gain, float pan = 0f, bool click = false)
        {
            var c = SwordClip(clip);
            if (c == null) return;
            cues.Add(new RevealCue { Start = hit - lead, Hit = hit, Clip = c, Pitch = 1f, Gain = gain, Pan = pan, Click = click });
        }

        // ① and ② up to the close, in seconds since the host's start (the cards' own moments come as they happen).
        void BuildSwordCues()
        {
            var cues = new List<RevealCue>();
            AddCue(cues, "SwordOpen", 0f, LeadSharp, GainOpen);
            for (int side = 0; side < 2; side++)
            {
                int n = Mathf.Min(side == 0 ? sideUs.Count : sideThem.Count, foundCards[side].Count);
                for (int k = 0; k < n && k < 3; k++)
                    AddCue(cues, "SwordPlate", FoundPlateAt + FoundPlateStep * k + PlateSlide, LeadSwordPlate,
                           GainPlate * (1f - .17f * k), side == 0 ? -.55f : .55f);
            }
            AddCue(cues, "SwordVs", FoundVsAt + VsSlam, LeadSwordVs, GainVs);
            float step = (float)SwordPickDraft.FoundSeconds / Mathf.Max(1, foundSquares.Count);
            for (int k = 0; k < foundSquares.Count; k++)
                AddCue(cues, "RevealDot", (k + 1) * step, LeadSharp, GainDot, 0f, true);
            AddCue(cues, "SwordCard", (float)SwordPickDraft.FoundSeconds + CardsUp, LeadSwordCard, GainCard * .7f);
            for (int k = 0; k < SwordTicks; k++)
                AddCue(cues, "SwordTick", (float)SwordPickDraft.CloseAt - SwordTicks + k, LeadSharp,
                       GainTick * (TickSoft + (1f - TickSoft) * k / (SwordTicks - 1f)), 0f, true);
            cues.Sort((a, b) => a.Start.CompareTo(b.Start));
            swordCues = cues;
        }

        // The close, ③ and ④, in seconds since the close.
        void BuildSwordAfterCues()
        {
            var cues = new List<RevealCue>();
            AddCue(cues, "SwordClose", 0f, LeadSwordClose, GainClose);
            AddCue(cues, "SwordPlate", TeamTitleIn, LeadSwordPlate, GainPlate * .7f, -.3f);
            int n = Mathf.Min(sideUs.Count, teamCards.Count);
            for (int k = 0; k < n; k++)
                AddCue(cues, "SwordCard", TeamCardAt + TeamCardStep * k, LeadSwordCard, GainCard * Mathf.Max(.4f, .8f - .15f * k),
                       Mathf.Clamp(-.4f + .3f * k, -.6f, .6f));
            float clash = TeamSeconds + ClashHit;
            AddCue(cues, "SwordClash", clash, LeadSwordClash, GainClash);
            AddCue(cues, "SwordVs", TeamSeconds + ClashVsAt + ClashVsLand, LeadSwordVs, GainVs * .45f);
            AddCue(cues, "SwordPlate", TeamSeconds + ClashTitleAt, LeadSwordPlate, GainPlate * .35f);
            cues.Sort((a, b) => a.Start.CompareTo(b.Start));
            swordAfterCues = cues;
        }

        // Called every frame with the screen's time (t < 0: not showing).
        void SyncSwordSound(float t)
        {
            if (t < 0f) { StopSwordSound(); return; }
            if (swordCues == null || float.IsNegativeInfinity(swordSoundUpTo)) BuildSwordCues();
            // the time went back a long way: a new match
            if (t < swordSoundUpTo - SoundAhead - 1f) { StopSwordSound(); BuildSwordCues(); }
            KeepEar();
            double now = DspNow();
            // the close: the picks closed early, or the end of the pick is that near and nobody closed it yet
            if (swordSoundClose < 0f)
            {
                if (swordClosedAt >= 0f) swordSoundClose = swordClosedAt;
                else if (t >= (float)SwordPickDraft.CloseAt - SoundAhead - .05f) swordSoundClose = (float)SwordPickDraft.CloseAt;
                if (swordSoundClose >= 0f) BuildSwordAfterCues();
            }
            float close = swordSoundClose >= 0f ? swordSoundClose : float.PositiveInfinity;
            SyncCues(swordCues, t, ref swordSoundUpTo, now, close);
            if (swordSoundClose >= 0f) SyncCues(swordAfterCues, t - swordSoundClose, ref swordAfterUpTo, now, float.PositiveInfinity);
            SwordMoments(t);
            SyncSwordBed(t, now);
        }

        void SyncCues(List<RevealCue> cues, float r, ref float upTo, double now, float before)
        {
            float until = r + SoundAhead;
            foreach (var cue in cues)
            {
                if (cue.Start <= upTo) continue;
                if (cue.Start > until) break;
                // a tick after an early close is left out
                if (cue.Hit >= before) continue;
                float early = cue.Start - r;
                if (early >= 0f) VoiceOn(ref swordVoices, ref swordVoiceFree, cue, 0f, now + early);
                else if (cue.Click ? -early <= LateClick : r - cue.Hit <= LateHit) VoiceOn(ref swordVoices, ref swordVoiceFree, cue, -early, 0d);
            }
            upTo = Mathf.Max(upTo, until);
        }

        // ② the pick's own moments, as they happen: a teammate's pick showing (their side of the stage), this PC's own
        // marks and confirm come from MarkPiece / ConfirmPiece (SwordMark).
        void SwordMoments(float t)
        {
            bool picking = swordScreen == 1;
            for (int k = 0; k < sideUs.Count; k++)
            {
                var p = PickOf(sideUs[k]);
                if (k >= swordHeard.Count) swordHeard.Add(p);
                // a pick seen first while picking sounds; one already there when the screen came is only noted
                if (picking && k > 0 && swordHeard[k] == PieceKind.Pawn && p != PieceKind.Pawn)
                    Now("RevealTick1", GainMate, .3f);
                swordHeard[k] = p;
            }
            // this PC's own pick shown by the session (sent on a confirm, or drawn at the close): the bolt, once
            var mine = PickOf(State.Me);
            int mineNow = mine == PieceKind.Pawn ? 0 : 1;
            if (picking && swordHeardMine == 0 && mineNow == 1 && !swordLockHeard) Now("RevealLock", GainLock, -.2f);
            swordHeardMine = mineNow;
        }

        bool swordLockHeard;

        // A mark (1~5, a click) or a confirm by this PC: the tick or the bolt now.
        void SwordMark(bool confirm)
        {
            if (confirm)
            {
                if (swordLockHeard) return;
                swordLockHeard = true;
                Now("RevealLock", GainLock, -.2f);
            }
            else Now("RevealTick0", GainMark, -.2f);
        }

        void Now(string clip, float gain, float pan)
        {
            var c = SwordClip(clip);
            if (c == null) return;
            VoiceOn(ref swordVoices, ref swordVoiceFree, new RevealCue { Clip = c, Pitch = 1f, Gain = gain, Pan = pan, Click = true }, 0f, 0d);
        }

        // The music: started once, where the screen is now (the intro from its matching point and the loop scheduled
        // right where the intro ends, or for a PC that comes in past the intro the loop alone at the matching point),
        // then only its volume follows the pick's last seconds, the close, the clash's duck and the screen's fade.
        void SyncSwordBed(float t, double now)
        {
            var intro = SwordClip("SwordMusic");
            var loop = SwordClip("SwordMusicLoop");
            if (intro == null || loop == null) return;
            if (!swordBedOn)
            {
                swordBedOn = true;
                if (swordMusic == null) swordMusic = MusicSource(false);
                if (swordMusicLoop == null) swordMusicLoop = MusicSource(true);
                double at = now + MusicLead;
                float from = t + MusicLead;
                swordMusic.clip = intro;
                swordMusicLoop.clip = loop;
                if (from < intro.length)
                {
                    swordMusic.time = from;
                    swordMusic.PlayScheduled(at);
                    swordMusicLoop.time = 0f;
                    swordMusicLoop.PlayScheduled(at + (intro.length - from));
                }
                else
                {
                    swordMusicLoop.time = (from - intro.length) % loop.length;
                    swordMusicLoop.PlayScheduled(at);
                }
            }
            float close = swordSoundClose >= 0f ? swordSoundClose : (float)SwordPickDraft.CloseAt;
            float swell = 1f + BedSwell * Mathf.Clamp01((Mathf.Min(t, close) - BedSwellFrom) / Mathf.Max(.01f, close - BedSwellFrom));
            float after = swordSoundClose >= 0f && t >= swordSoundClose ? BedAfterClose : 1f;
            float duck = 1f;
            if (swordSoundClose >= 0f)
            {
                float since = t - (swordSoundClose + TeamSeconds + ClashHit);
                if (since >= -.05f) duck = Mathf.Clamp(DuckLow + (DuckBack - DuckLow) * (since - DuckHold) / DuckRise, DuckLow, DuckBack);
            }
            float fade = Hiding ? Mathf.Clamp01(1f - (Time.unscaledTime - hideAt) / FadeSeconds) : 1f;
            swordMusic.volume = swordMusicLoop.volume = GameSettings.Music * SwordMusicGain * swell * after * duck * fade;
        }

        void StopSwordSound()
        {
            if (float.IsNegativeInfinity(swordSoundUpTo) && !swordBedOn) return;
            swordSoundUpTo = swordAfterUpTo = float.NegativeInfinity;
            swordSoundClose = -1f;
            swordHeard.Clear();
            swordHeardMine = -1;
            swordLockHeard = false;
            swordBedOn = false;
            if (swordMusic != null) swordMusic.Stop();
            if (swordMusicLoop != null) swordMusicLoop.Stop();
            if (swordVoices == null) return;
            for (int i = 0; i < swordVoices.Length; i++)
            {
                if (swordVoices[i] != null) swordVoices[i].Stop();
                swordVoiceFree[i] = 0d;
            }
        }
    }
}
