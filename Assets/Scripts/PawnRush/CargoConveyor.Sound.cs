using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "7번 컨베이어" - the belt's loop and the loads' rattle, made new with the ElevenLabs MCP): the
    // sorting yard conveyor's sounds, baked by ArtSource/PawnRush/Sfx/bake_carriage_yard.py into
    // Resources/PawnRushCarriageYardSfx (prompts: Docs/PawnRush/PAWN_RUSH_AUDIO.md section 12).
    //   belt   one seamless loop of wooden rollers turning, from the point of the belt nearest the listener (so the
    //          whole 47 m belt sounds as one line, not one spot), always running
    //   loads  a loop of a crate rattling over the rollers on each load while it is on the belt (out of sight under the
    //          floor it fades out), carried with it; each starts at its own place in the loop so they do not drone
    //          together
    // Pictures-and-sounds only, read off what is drawn: the same on every PC. Every source goes through SectionSoundGate.
    public sealed partial class CargoConveyor
    {
        const string SfxFolder = "PawnRushCarriageYardSfx/";
        const float Fade = 6f;   // 1/s: how fast a load's rattle comes and goes at the hatches
        static AudioClip beltClip, rideClip;

        AudioSource belt;
        AudioSource[] rides = new AudioSource[0];
        float[] rideGain = new float[0];
        static AudioListener ear;

        void BeltSounds()
        {
            if (beltClip == null || rideClip == null)
            {
                beltClip = Resources.Load<AudioClip>(SfxFolder + "ConveyorBelt");
                rideClip = Resources.Load<AudioClip>(SfxFolder + "ConveyorRide");
            }
            if (beltClip != null)
            {
                var go = new GameObject("Belt (sound)");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, height, 0f);
                belt = LoopSource(go, beltClip, 4f, 30f, 0f);
            }
            if (rideClip == null) return;
            rides = new AudioSource[crates.Length];
            rideGain = new float[crates.Length];
            for (int i = 0; i < crates.Length; i++)
                if (crates[i] != null) rides[i] = LoopSource(crates[i].gameObject, rideClip, 2.5f, 22f, (i * .37f) % 1f);
        }

        static AudioSource LoopSource(GameObject on, AudioClip clip, float near, float far, float startShare)
        {
            var a = on.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = true;
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.dopplerLevel = 0f;
            a.volume = 0f;
            a.time = clip.length * startShare;
            SectionSoundGate.Gate(a);
            return a;
        }

        void LateUpdate()
        {
            if (belt != null)
            {
                if (ear == null || !ear.isActiveAndEnabled) ear = FindAnyObjectByType<AudioListener>();
                if (ear != null)
                {
                    float x = Mathf.Clamp(transform.InverseTransformPoint(ear.transform.position).x, -length * .5f, length * .5f);
                    belt.transform.localPosition = new Vector3(x, height, 0f);
                }
                belt.volume = 1f;   // the effects volume and the section are the gate's
                if (!belt.isPlaying && belt.isActiveAndEnabled) belt.Play();
            }
            for (int i = 0; i < rides.Length; i++)
            {
                var a = rides[i];
                if (a == null) continue;
                // On the belt it is drawn over it; out of sight it waits under the floor (ConveyorCrate.Evaluate).
                bool on = a.transform.localPosition.y > 0f;
                rideGain[i] = Mathf.MoveTowards(rideGain[i], on ? 1f : 0f, Fade * Time.deltaTime);
                a.volume = rideGain[i];
                if (rideGain[i] > 0f) { if (!a.isPlaying && a.isActiveAndEnabled) a.Play(); }
                else if (a.isPlaying) a.Pause();
            }
        }
    }
}
