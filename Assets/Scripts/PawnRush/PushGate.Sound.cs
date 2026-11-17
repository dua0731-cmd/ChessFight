using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "2·12번 성문" - the final gate pushed open): a loop of the heavy gate's hinges groaning and its foot
    // scraping the stone while it moves, made with the ElevenLabs MCP (Resources/PawnRushCarriageYardSfx/GatePush,
    // bake_carriage_yard.py; prompts: Docs/PawnRush/PAWN_RUSH_AUDIO.md section 15). Its volume follows how fast the gate
    // is opening (the progress per second, eased - a match's other PCs get the progress in steps from the host), so it
    // stops when the pushers let go and is gone once the gate stands open. From the gate's middle. Through
    // SectionSoundGate.
    public sealed partial class PushGate
    {
        static AudioClip pushClip;
        AudioSource pushSound;
        float lastProgress = -1f, openRate;

        void LateUpdate()
        {
            if (pushSound == null)
            {
                if (pushClip == null) pushClip = Resources.Load<AudioClip>("PawnRushCarriageYardSfx/GatePush");
                if (pushClip == null) return;
                var go = new GameObject("Gate (sound)");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 2f, 0f);
                pushSound = go.AddComponent<AudioSource>();
                pushSound.clip = pushClip;
                pushSound.loop = true;
                pushSound.playOnAwake = false;
                pushSound.spatialBlend = 1f;
                pushSound.rolloffMode = AudioRolloffMode.Linear;
                pushSound.minDistance = 5f;
                pushSound.maxDistance = 40f;
                pushSound.dopplerLevel = 0f;
                pushSound.volume = 0f;
                SectionSoundGate.Gate(pushSound);
            }
            float dt = Time.deltaTime;
            if (lastProgress >= 0f && dt > 0f)
            {
                float rate = Mathf.Max(0f, progress - lastProgress) / dt;
                // Smoothed over ~0.3 s, so the host's steps (a client's) read as one movement.
                openRate = Mathf.Lerp(openRate, rate, 1f - Mathf.Exp(-dt * 3.5f));
            }
            lastProgress = progress;
            // Full at one pusher's speed (the whole bar in pushSeconds).
            float share = Mathf.Clamp01(openRate * Mathf.Max(.1f, pushSeconds) * 1.2f);
            pushSound.volume = share;
            pushSound.pitch = Mathf.Lerp(.9f, 1.05f, share);
            if (share > .02f) { if (!pushSound.isPlaying && pushSound.isActiveAndEnabled) pushSound.Play(); }
            else if (pushSound.isPlaying) pushSound.Pause();
        }
    }
}
