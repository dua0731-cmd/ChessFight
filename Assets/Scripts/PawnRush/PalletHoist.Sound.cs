using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "9·10번 크레인·승강대" - the hoists going up and down): a loft hoist pair's ropes running over the
    // pulleys while the pair swaps (one pallet up, the other down, `move` s), and each pallet's clunk as it arrives - the
    // one coming down onto the floor full, the one reaching the deck softer. Made with the ElevenLabs MCP
    // (Resources/PawnRushCarriageYardSfx/HoistMove0..1, HoistLand0..1, bake_carriage_yard.py; prompts:
    // Docs/PawnRush/PAWN_RUSH_AUDIO.md section 14). The code-made ratchet clack at the start stays. Timed off the shared
    // clock's swap (the same on every PC); a PC that first sees a move part way plays it from there (up to Late in).
    public sealed partial class PalletHoist
    {
        const string SfxFolder = "PawnRushCarriageYardSfx/";
        const float Late = .3f;
        static AudioClip[] moveClips, landClips;
        AudioSource ropesSound;
        AudioSource[] landSounds;
        double movedSwap = double.NaN, landedSwap = double.NaN;

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

        AudioSource Voice(Vector3 local, float near, float far)
        {
            var go = new GameObject("Hoist (sound)");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.dopplerLevel = 0f;
            SectionSoundGate.Gate(a);
            return a;
        }

        static AudioClip Pick(AudioClip[] set, double swap) =>
            set == null || set.Length == 0 ? null : set[(int)(System.Math.Abs(System.Math.Round(swap * 10.0)) % set.Length)];

        void HoistSounds(double now, double swap, double move, double half)
        {
            if (ropesSound == null && landSounds == null)
            {
                if (moveClips == null)
                {
                    moveClips = Clips("HoistMove");
                    landClips = Clips("HoistLand");
                }
                if (moveClips.Length > 0) ropesSound = Voice(new Vector3(0f, armHeight - .5f, 0f), 3f, 28f);
                landSounds = new AudioSource[pallets.Length];
                if (landClips.Length > 0)
                    for (int k = 0; k < pallets.Length; k++) landSounds[k] = Voice(new Vector3(PlateX(k), .3f, 0f), 3f, 25f);
                // First seen: no move or landing "just now" unless it really is (the windows below).
            }
            // The ropes from the swap's start.
            if (now >= swap && swap != movedSwap)
            {
                movedSwap = swap;
                var clip = Pick(moveClips, swap);
                float late = (float)(now - swap);
                if (ropesSound != null && clip != null && late <= Late && late < clip.length && ropesSound.isActiveAndEnabled)
                {
                    ropesSound.Stop();
                    ropesSound.clip = clip;
                    ropesSound.time = late;
                    ropesSound.Play();
                }
            }
            // Both pallets arrive as the swap ends: the one now down on the floor, the other at the deck.
            // (the last swap: once a period turns over, `swap` is already the next one)
            double arrive = (now >= swap ? swap : swap - half) + move;
            if (now >= arrive && now < arrive + Late && arrive != landedSwap)
            {
                landedSwap = arrive;
                var clip = Pick(landClips, arrive);
                if (clip == null) return;
                for (int k = 0; k < pallets.Length && k < landSounds.Length; k++)
                {
                    var a = landSounds[k];
                    if (a == null || !a.isActiveAndEnabled) continue;
                    bool down = Top(k, now) < low + .2f;
                    if (!down) a.transform.localPosition = new Vector3(PlateX(k), high, 0f);
                    else a.transform.localPosition = new Vector3(PlateX(k), .3f, 0f);
                    a.PlayOneShot(clip, down ? 1f : .5f);
                }
            }
        }
    }
}
