using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "6번 거대 짐" - the thud and its wave, the chains lowering and hoisting - made with the ElevenLabs
    // MCP): the giant chest's recorded sounds, baked by ArtSource/PawnRush/Sfx/bake_carriage_yard.py into
    // Resources/PawnRushCarriageYardSfx (prompts: Docs/PawnRush/PAWN_RUSH_AUDIO.md section 11).
    //   land    as it touches down (the moment the wave starts): a deep wooden-and-iron boom and, under it, the floor's
    //           rumble with rattling grit as the wave runs out. Without these files the code-made ThudSound plays.
    //   chains  paying out over the 1.1 s it is lowered (only when it is lowered on its chains: CargoSlam.Lowered) and
    //           winding in over the 1.4 s it is hoisted, each from its start, from where the trip already is for a PC that
    //           first sees it part way (more than Late in: skipped).
    // The two takes of each alternate trip by trip, the same on every PC (the trip number is the shared clock's).
    // Every source goes through SectionSoundGate (heard only in this section, times the effects volume).
    public sealed partial class SlamChest
    {
        const string SfxFolder = "PawnRushCarriageYardSfx/";
        const float Late = .4f;
        static AudioClip[] thuds, shocks, lowers, hoists;
        AudioSource land, chain;
        double loweredTrip = double.NaN, hoistedTrip = double.NaN;

        static AudioClip[] Clips(string stem)
        {
            var found = new System.Collections.Generic.List<AudioClip>();
            for (int i = 0; i < 2; i++)
            {
                var clip = Resources.Load<AudioClip>(SfxFolder + stem + i);
                if (clip != null) found.Add(clip);
            }
            return found.ToArray();
        }

        void ChestSounds()
        {
            if (thuds == null)
            {
                thuds = Clips("SlamThud");
                shocks = Clips("SlamShock");
                lowers = Clips("ChainLower");
                hoists = Clips("ChainHoist");
            }
            if (thuds.Length > 0 || shocks.Length > 0) land = Source(6f, 60f);
            if (lowers.Length > 0 || hoists.Length > 0) chain = Source(4f, 40f);
        }

        AudioSource Source(float near, float far)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.dopplerLevel = 0f;
            SectionSoundGate.Gate(a);
            return a;
        }

        static AudioClip Pick(AudioClip[] set, double trip) =>
            set.Length == 0 ? null : set[(int)(System.Math.Abs(trip) % set.Length)];

        // The chest touched down this trip: true when the recorded sounds played (the code-made thud is then left out).
        bool Landed(double trip)
        {
            if (land == null || !land.isActiveAndEnabled) return false;
            var boom = Pick(thuds, trip);
            var wave = Pick(shocks, trip);
            if (boom != null) land.PlayOneShot(boom, 1f);
            if (wave != null) land.PlayOneShot(wave, 1f);
            return boom != null;
        }

        void Chains(double trip, float into)
        {
            if (chain == null || slam == null || !chain.isActiveAndEnabled) return;
            float lowerAt = slam.CarrySeconds + slam.HoldSeconds, hoistAt = slam.LandsAt + slam.SitSeconds;
            if (slam.Lowered && trip != loweredTrip && into >= lowerAt && into < slam.LandsAt)
            {
                loweredTrip = trip;
                PlayChain(Pick(lowers, trip), into - lowerAt);
            }
            if (trip != hoistedTrip && into >= hoistAt)
            {
                hoistedTrip = trip;
                PlayChain(Pick(hoists, trip), into - hoistAt);
            }
        }

        // R729: not "Start" - Unity reads a method of that name as its message and wrote "Script error (SlamChest): Start() can
        // not take parameters." twice at every launch (11-20 build test).
        void PlayChain(AudioClip clip, float late)
        {
            if (clip == null || late > Late || late >= clip.length) return;
            chain.Stop();
            chain.clip = clip;
            chain.volume = 1f;
            chain.time = Mathf.Max(0f, late);
            chain.Play();
        }
    }
}
