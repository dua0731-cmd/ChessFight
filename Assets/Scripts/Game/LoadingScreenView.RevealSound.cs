using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // R583 (11-12): the course reveal's sound. 승규 님 first asked for sounds made in code ("오케이 이제 쫀득하게 코딩으로
    // 사운드 넣어줘 ai 티가 나는 소리는 안돼"), heard that set and found it dull and cheap ("둔하고 먹먹함 · 소리 종류가 안 맞음
    // · 너무 싸 보임", wanting game-UI matchmaking sounds), then asked for ElevenLabs takes ("일레븐 랩스로 쫀득하게 만들어봐").
    // The clips are Resources/Loading/CourseReveal/Sound/Reveal*.wav, cut from the takes in
    // ArtSource/Loading/CourseReveal/SoundC/raw by SoundC/process.py, which also writes where each clip's hit is (leads.json,
    // the Lead* constants below). The code-made set is kept in SoundC/rejected_code_synth.
    //
    // Every sound is a cue at a reveal time worked out from the same numbers as the picture (LoadingScreenView.Reveal.cs),
    // started on the audio clock (AudioSource.PlayScheduled) a little ahead, so a slow frame does not make it late, and
    // panned to where it happens on the stage:
    //   0 s          the screen darkens: a deep whoosh in with a crack
    //   ~0.18 s      the title swipes in, its loudest point as it first reaches its place
    //   panels       each swooshes in and hits as it lands (left, middle, right)
    //   each cut     a crisp UI tick (three takes in turn; softer while the cuts are fast, fuller as they slow down)
    //   each stop    the lock-in: a heavy steel bolt clunking shut (R591, picked from eight candidates)
    //   squares      a soft short tap as an obstacle's or the final's square fills (a mini-game's comes with its stop)
    //   countdown    the number shows on a thud, a dark low drone throbs under it up to the 1 and stops dead there,
    //                3 and 2 a heavy steel clunk, 1 a deep boom (R594, set D of the countdown sound page)
    //   music        R605 (11-12): an electronic game show bed (four on the floor kick, tight hi-hats, warm pulsing
    //                synth chords; K of the music pages, "K로 해주고 실제 인게임이 시작 될 때 까지 사운드가 유지 되야 해")
    //                under all of it from 0 s, and on past the 1 until the match really starts: RevealMusic is the first
    //                9.8 s, RevealMusicLoop one bar that follows it seamlessly and repeats (SoundC/bgm/bake.py), both
    //                put on the audio clock so the join is sample exact; it fades out with the screen. It follows the
    //                music volume, not the effects one. (R600 had D and R601 G, both cut dead on the 1.)
    // R625: the versus scene (LoadingScreenView.Versus.cs) now plays first, and every time above is that much later
    // (RevealOffset); its own cues reuse these clips: a light slam per player's panel, a tap as this PC's bar fills, the
    // bolt as the blade lands, a steel clunk on VS, the title swipe as the reveal's title comes in, and the three panel
    // slams as the versus panels gather into them.
    // Repeated hits keep one pitch (no rising scale). A cue found already past (a PC that joins the reveal late, or a
    // long freeze) starts part-way in so the rest still keeps time, or is left out when its hit is already gone.
    public sealed partial class LoadingScreenView
    {
        const string RevealSoundPath = "Loading/CourseReveal/Sound/";
        // How far ahead cues are put on the audio clock; how late a click or a hit may still start.
        const float SoundAhead = .25f, LateClick = .03f, LateHit = .1f;
        const int RevealVoices = 24, RevealTicks = 3;
        // Where each clip's hit is, in seconds from its start (SoundC/leads.json).
        const float LeadSharp = .002f, LeadTitle = .06f, LeadSlam = .0596f, LeadCountIn = .045f;
        // The music clip is peak normalised; this puts it back at the page's level under the cues (RMS -25 dBFS there,
        // bake.py's MUSIC_GAIN, 0.2525 for K, here scaled by the default effects / music volumes .8 / .7 so the defaults
        // sound like the page). Bake another take and this changes with it.
        const float MusicGain = .289f;
        // How far ahead the music's first start is put on the audio clock.
        const float MusicLead = .05f;
        // A reveal time this far on is a skipped reveal, not a slow one.
        const float MusicSkipped = 1e4f;

        struct RevealCue
        {
            public float Start;   // reveal time the clip starts
            public float Hit;     // reveal time its hit must sound
            public AudioClip Clip;
            public float Pitch, Gain, Pan;
            public bool Click;
        }

        // Loaded once per run (the same every round and on every PC).
        static readonly Dictionary<string, AudioClip> RevealClips = new Dictionary<string, AudioClip>();

        List<RevealCue> revealCues;
        AudioSource[] revealVoices;
        double[] revealVoiceFree;
        float revealSoundUpTo = float.NegativeInfinity;
        // the audio clock moves in whole mix buffers: carried on between them by the real clock
        double dspSeen = -1, dspSeenAt;
        AudioListener revealEar;
        float earCheck;

        static AudioClip RevealClip(string name)
        {
            if (RevealClips.TryGetValue(name, out var clip) && clip != null) return clip;
            clip = Resources.Load<AudioClip>(RevealSoundPath + name);
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            RevealClips[name] = clip;
            return clip;
        }

        // Loads every clip now (while matching), so the reveal's first frame does not wait for them.
        static void WarmRevealClips()
        {
            foreach (var name in new[] { "RevealOpen", "RevealTitle", "RevealSlam", "RevealLock", "RevealDot", "RevealCountIn", "RevealCount", "RevealCountLast", "RevealCountBed", "RevealMusic", "RevealMusicLoop" })
                RevealClip(name);
            for (int i = 0; i < RevealTicks; i++) RevealClip("RevealTick" + i);
        }

        // Where something at stage x sounds (-1 left .. 1 right), a little narrower than the picture.
        static float StagePan(float x) => Mathf.Clamp(.9f * (x - StageWide / 2f) / (StageWide / 2f), -1f, 1f);

        // The cues of this round (which panels cut depends on its draw), in order of start.
        void BuildRevealCues()
        {
            var cues = new List<RevealCue>();
            void Add(string clip, float hit, float lead, float gain, float pan = 0f, float pitch = 1f, bool click = false)
            {
                var c = RevealClip(clip);
                if (c == null) return;
                cues.Add(new RevealCue { Start = hit - lead / pitch, Hit = hit, Clip = c, Pitch = pitch, Gain = gain, Pan = pan, Click = click });
            }

            // R625: cue times are the screen's: the versus scene's (LoadingScreenView.Versus.cs) as they are, the reveal's
            // RevealOffset later. The reveal's own entrance (title swipe, panel slams) no longer plays: the versus scene's
            // cues stand in for it, made of the same clips until 승규 님 has heard them.
            const float O = RevealOffset;
            // where a plate sliding in with EoBack first reaches its place
            float backLands = 1f - BackPull / (BackPull + 1f);
            Add("RevealOpen", 0f, LeadSharp, .8f);
            Add("RevealTitle", VTitleAt + VPlateSlide * backLands, LeadTitle, .55f, StagePan(200f));
            // R632: one slam per pair of panels landing together (white's k-th and black's k-th), from between them
            var pairs = new Dictionary<int, (float at, float x, int n)>();
            foreach (var s in versusStrips)
            {
                pairs.TryGetValue(s.Order, out var pr);
                pairs[s.Order] = (s.At, pr.x + s.X + s.W / 2f, pr.n + 1);
            }
            foreach (var pr in pairs.Values)
                Add("RevealSlam", pr.at + StripDrop, LeadSlam, pr.n > 1 ? .4f : .32f, StagePan(pr.x / pr.n));
            foreach (var s in versusStrips)
                if (s.Me) Add("RevealDot", MeAt, LeadSharp, .5f, StagePan(s.X + s.W / 2f), 1f, true);
            // the VS panel lands: the bolt and the steel clunk together (VsAt = BladeAt + BladeDrop)
            Add("RevealLock", VsAt, LeadSharp, .7f);
            Add("RevealCount", VsAt + .02f, LeadSharp, .85f);
            float swapHalf = TitleSwap / 2f;
            Add("RevealTitle", TitleSwapAt + swapHalf + swapHalf * backLands, LeadTitle, .5f, StagePan(200f));
            foreach (var p in revealPanels)
                Add("RevealSlam", VersusMerge, LeadSlam, .6f, StagePan(PanelLeft + p.Index * PanelStep + PanelWide / 2f));

            foreach (var p in revealPanels)
            {
                int i = p.Index;
                float pan = StagePan(PanelLeft + i * PanelStep + PanelWide / 2f);
                if (p.Names.Length == 0 || p.Pick < 0) continue;
                int last = p.Hops.Length - 1;
                for (int j = 0; j < last; j++)
                {
                    // fuller as the cuts slow down; three takes in turn, each a hair apart in pitch (never a rising run)
                    float slow = last > 1 ? j / (float)(last - 1) : 1f;
                    float wobble = ((j * 7 + i * 3) % 5 - 2) * .012f;
                    Add("RevealTick" + (j + i) % RevealTicks, O + p.Hops[j], LeadSharp, .5f + .3f * slow * slow, pan * .9f, 1f + wobble, true);
                }
                Add("RevealLock", O + RollLand[i], LeadSharp, .85f, pan * .8f);
            }
            for (int k = 0; k < DotAt.Length && k < PawnRushSections.SlotCount; k++)
                if (PawnRushSections.Kind[k] != PawnRushSections.KindMiniGame)
                    Add("RevealDot", O + DotAt[k] - DotFill + .012f, LeadSharp, .4f, StagePan(DotLeft + k * DotStep + DotWide / 2f), 1f, true);
            Add("RevealCountIn", O + CountAt[0] - CountIn, LeadCountIn, .5f);
            // the drone's hit is its end, on the 1, so it starts its own length before (from the number's first showing)
            var bed = RevealClip("RevealCountBed");
            if (bed != null) Add("RevealCountBed", O + CountAt[CountAt.Length - 1], bed.length, .32f);
            for (int q = 0; q < CountAt.Length; q++)
            {
                bool last = q == CountAt.Length - 1;
                Add(last ? "RevealCountLast" : "RevealCount", O + CountAt[q], LeadSharp, last ? 1f : .9f);
            }
            cues.Sort((a, b) => a.Start.CompareTo(b.Start));
            revealCues = cues;
        }

        // Called every frame with the reveal's time (r < 0: not showing).
        void SyncRevealSound(float r)
        {
            if (r < 0f) { StopRevealSound(); return; }
            if (revealCues == null) BuildRevealCues();
            // the time went back a long way: a new reveal
            if (r < revealSoundUpTo - SoundAhead - 1f) StopRevealSound();
            KeepEar();
            double now = DspNow();
            float until = r + SoundAhead;
            foreach (var cue in revealCues)
            {
                if (cue.Start <= revealSoundUpTo) continue;
                if (cue.Start > until) break;
                float early = cue.Start - r;
                if (early >= 0f) Voice(cue, 0f, now + early);
                else if (cue.Click ? -early <= LateClick : r - cue.Hit <= LateHit) Voice(cue, -early, 0d);
            }
            revealSoundUpTo = Mathf.Max(revealSoundUpTo, until);
            SyncRevealMusic(r, now);
        }

        // ---- the music (R605) ----

        AudioSource musicIntro, musicLoop;
        bool musicStarted;

        // Starts the music once per reveal, where the reveal is now: the intro from its matching point and the loop
        // scheduled right where the intro ends, or (a PC that comes in past the 1) the loop alone at the matching beat.
        // Then only its volume follows the setting, and the screen's fade-out.
        void SyncRevealMusic(float r, double now)
        {
            var intro = RevealClip("RevealMusic");
            var loop = RevealClip("RevealMusicLoop");
            if (intro == null || loop == null) return;
            // a player going back into a live match skips the reveal (MatchLoader.RevealSkipped): no music for them
            if (!musicStarted && r >= MusicSkipped) return;
            if (!musicStarted)
            {
                musicStarted = true;
                MusicStarts();
                if (musicIntro == null) musicIntro = MusicSource(false);
                if (musicLoop == null) musicLoop = MusicSource(true);
                double at = now + MusicLead;
                float from = r + MusicLead;
                musicIntro.clip = intro;
                musicLoop.clip = loop;
                if (from < intro.length)
                {
                    musicIntro.time = from;
                    musicIntro.PlayScheduled(at);
                    musicLoop.time = 0f;
                    musicLoop.PlayScheduled(at + (intro.length - from));
                }
                else
                {
                    musicLoop.time = (from - intro.length) % loop.length;
                    musicLoop.PlayScheduled(at);
                }
            }
            float fade = Hiding ? Mathf.Clamp01(1f - (Time.unscaledTime - hideAt) / FadeSeconds) : 1f;
            musicIntro.volume = musicLoop.volume = GameSettings.Music * MusicGain * fade;
        }

        AudioSource MusicSource(bool loop, GameObject on = null)
        {
            var a = (on != null ? on : gameObject).AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = loop;
            a.spatialBlend = 0f;
            a.priority = 0;
            a.dopplerLevel = 0f;
            a.volume = GameSettings.Music * MusicGain;
            return a;
        }

        void StopRevealMusic()
        {
            musicStarted = false;
            if (musicIntro != null) musicIntro.Stop();
            if (musicLoop != null) musicLoop.Stop();
        }

        void StopRevealSound()
        {
            if (float.IsNegativeInfinity(revealSoundUpTo)) return;
            revealSoundUpTo = float.NegativeInfinity;
            StopRevealMusic();
            if (revealVoices == null) return;
            for (int i = 0; i < revealVoices.Length; i++)
            {
                if (revealVoices[i] != null) revealVoices[i].Stop();
                revealVoiceFree[i] = 0d;
            }
        }

        // Starts `cue` at DSP time `at` (0: now, `from` seconds into it).
        void Voice(RevealCue cue, float from, double at) => VoiceOn(ref revealVoices, ref revealVoiceFree, cue, from, at);

        // R631: the same on a set of voices of its own (Sword Fight's entrance screen keeps its own, so stopping one
        // screen's sound never cuts the other's).
        void VoiceOn(ref AudioSource[] voices, ref double[] free, RevealCue cue, float from, double at)
        {
            if (voices == null)
            {
                voices = new AudioSource[RevealVoices];
                free = new double[RevealVoices];
                for (int i = 0; i < RevealVoices; i++)
                {
                    var a = gameObject.AddComponent<AudioSource>();
                    a.playOnAwake = false;
                    a.spatialBlend = 0f;
                    a.priority = 0;
                    a.dopplerLevel = 0f;
                    voices[i] = a;
                }
            }
            double now = AudioSettings.dspTime;
            int pick = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (free[i] <= now) { pick = i; break; }
                if (free[i] < free[pick]) pick = i;
            }
            var voice = voices[pick];
            float into = from * cue.Pitch;
            if (into >= cue.Clip.length) return;
            voice.Stop();
            voice.clip = cue.Clip;
            voice.pitch = cue.Pitch;
            voice.panStereo = cue.Pan;
            voice.volume = GameSettings.Effects * cue.Gain;
            voice.time = into;
            if (at > 0d) voice.PlayScheduled(at);
            else voice.Play();
            free[pick] = (at > 0d ? at : now) + (cue.Clip.length - into) / cue.Pitch + .02;
        }

        // The audio clock now: AudioSettings.dspTime only moves once per mix buffer, so between moves it is carried on
        // by the real clock (never past one buffer).
        double DspNow()
        {
            double dsp = AudioSettings.dspTime, real = Time.realtimeSinceStartupAsDouble;
            if (dsp != dspSeen) { dspSeen = dsp; dspSeenAt = real; }
            AudioSettings.GetDSPBufferSize(out int buffer, out _);
            double most = buffer / (double)Mathf.Max(1, AudioSettings.outputSampleRate);
            return dsp + System.Math.Min(real - dspSeenAt, most);
        }

        // The title scene's camera normally hears the reveal; without any listener nothing would, so one is added here
        // and taken away again once the match scene brings its own.
        void KeepEar()
        {
            if (Time.unscaledTime < earCheck) return;
            earCheck = Time.unscaledTime + .5f;
            if (revealEar == null)
            {
                if (FindAnyObjectByType<AudioListener>() == null) revealEar = gameObject.AddComponent<AudioListener>();
            }
            else if (FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length > 1)
            {
                Destroy(revealEar);
                revealEar = null;
            }
        }
    }
}
