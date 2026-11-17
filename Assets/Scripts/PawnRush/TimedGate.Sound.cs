using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "2·12번 성문" - the outer gate opening): the bolt and the long creak of the hinges as the gate swings
    // open at the race's start, made with the ElevenLabs MCP (Resources/PawnRushCarriageYardSfx/GateOpen0..1,
    // bake_carriage_yard.py; prompts: Docs/PawnRush/PAWN_RUSH_AUDIO.md section 15). Played once by the leaf that runs to +x
    // (a pair's two leaves would play it twice), from the gate's middle (the leaf's free edge when shut), at the shared
    // start; a PC that first sees it part way plays it from there (up to Late in). Through SectionSoundGate.
    public sealed partial class TimedGate
    {
        const float Late = .5f;
        static AudioClip[] openClips;
        AudioSource openSound;
        double soundedAt = double.NaN;

        void GateSound()
        {
            if (extendsLeft || double.IsNaN(openAt)) return;
            double into = ObstacleClock.Now + phase - openAt;
            if (into < 0 || openAt == soundedAt) return;
            soundedAt = openAt;
            if (openClips == null)
            {
                var found = new System.Collections.Generic.List<AudioClip>();
                for (int i = 0; i < 2; i++)
                {
                    var c = Resources.Load<AudioClip>("PawnRushCarriageYardSfx/GateOpen" + i);
                    if (c != null) found.Add(c);
                }
                openClips = found.ToArray();
            }
            if (openClips.Length == 0 || into > Late) return;
            if (openSound == null)
            {
                var go = new GameObject("Gate (sound)");
                go.transform.SetParent(transform.parent, false);
                go.transform.localPosition = transform.localPosition + transform.localRotation * new Vector3(width, height * .45f, 0f);
                openSound = go.AddComponent<AudioSource>();
                openSound.playOnAwake = false;
                openSound.spatialBlend = 1f;
                openSound.rolloffMode = AudioRolloffMode.Linear;
                openSound.minDistance = 6f;
                openSound.maxDistance = 45f;
                openSound.dopplerLevel = 0f;
                SectionSoundGate.Gate(openSound);
            }
            var clip = openClips[(int)(System.Math.Abs(System.Math.Floor(openAt)) % openClips.Length)];
            openSound.clip = clip;
            openSound.time = Mathf.Min((float)into, clip.length - .01f);
            openSound.Play();
        }
    }
}
