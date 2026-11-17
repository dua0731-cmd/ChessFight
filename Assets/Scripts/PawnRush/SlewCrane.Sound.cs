using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "9·10번 크레인·승강대" - the crane turning): a timber crane's post and ropes groaning as it slews, a loop
    // made with the ElevenLabs MCP (Resources/PawnRushCarriageYardSfx/CraneSlew, bake_carriage_yard.py; prompts:
    // Docs/PawnRush/PAWN_RUSH_AUDIO.md section 14). At the post; its volume and pitch follow how fast the jib turns, read off
    // the heading each frame (the slew's ramps ease it in and out; at rest it is silent). The code-made bell stays.
    // Through SectionSoundGate; the same on every PC (the heading is the shared clock's).
    public sealed partial class SlewCrane
    {
        static AudioClip slewClip;
        AudioSource slew;
        float lastHeading = float.NaN, slewRate;

        void SlewSound(float heading)
        {
            if (slew == null)
            {
                if (slewClip == null) slewClip = Resources.Load<AudioClip>("PawnRushCarriageYardSfx/CraneSlew");
                if (slewClip == null) return;
                slew = gameObject.AddComponent<AudioSource>();
                slew.clip = slewClip;
                slew.loop = true;
                slew.playOnAwake = false;
                slew.spatialBlend = 1f;
                slew.rolloffMode = AudioRolloffMode.Linear;
                slew.minDistance = 5f;
                slew.maxDistance = 35f;
                slew.dopplerLevel = 0f;
                slew.volume = 0f;
                slew.time = slewClip.length * Mathf.Repeat(transform.position.x * .13f, 1f);
                SectionSoundGate.Gate(slew);
            }
            float dt = Time.deltaTime;
            if (!float.IsNaN(lastHeading) && dt > 0f)
            {
                float turned = Mathf.Abs(Mathf.DeltaAngle(lastHeading, heading));
                // A cut (a jump of the clock) is not a turn.
                float rate = turned < 30f ? turned / dt : 0f;
                slewRate = Mathf.Lerp(slewRate, rate, 1f - Mathf.Exp(-dt * 6f));
            }
            lastHeading = heading;
            // Full at the slew's top speed (half a turn in 6 s with 1 s ramps: ~36 deg/s).
            float share = Mathf.Clamp01(slewRate / 36f);
            slew.volume = share;
            slew.pitch = Mathf.Lerp(.85f, 1f, share);
            if (share > .01f) { if (!slew.isPlaying && slew.isActiveAndEnabled) slew.Play(); }
            else if (slew.isPlaying) slew.Pause();
        }
    }
}
