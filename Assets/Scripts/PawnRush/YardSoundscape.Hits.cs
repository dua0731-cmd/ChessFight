using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "13번 타격음"): a hit sound when one of the yard's moving things knocks a pawn away (YardKit.HitGiven,
    // from every YardImpact shell): a carriage or its horses a hollow wooden thud (CarriageHit, the "1번 말 마차" takes), a
    // waterwheel's paddle a wet wooden whack, a crate, a stack, a pallet or a timber load a hollow wooden bump. The giant
    // chest's wave has its thud and rumble (SlamChest.Sound.cs). Made with the ElevenLabs MCP
    // (Resources/PawnRushCarriageYardSfx/HitBump0..2, HitPaddle0..2, CarriageHit0..1; prompts: Docs/PawnRush/PAWN_RUSH_AUDIO.md
    // sections 9 and 16). At the pawn's hips; a knockdown full, a stagger at 0.6.
    //
    // 승규 "온라인 참가자 pc 에서도 다 들리게 해": a knock-back is given only on the PC that moves the pawn (offline, a match's
    // host), so the hit goes out through Gameplay.CourseHitSound - the pawn's timed effect, which the host's stream sends to
    // every client - and is played where it comes back (Heard): at once on the PC that gave it, at the moment each client
    // draws it there. A few voices in turn, so hits close together do not cut each other off. Through SectionSoundGate.
    public sealed partial class YardSoundscape
    {
        // The yard's kinds of hit (CourseHitSound's `kind`, sent as a byte).
        const int KindBump = 0, KindPaddle = 1, KindCarriage = 2;
        const int HitVoices = 4;
        static AudioClip[] bumps, paddles, carriages;
        AudioSource[] hitVoices;
        int nextVoice;

        static AudioClip[] Clips(string stem, int count)
        {
            var found = new System.Collections.Generic.List<AudioClip>();
            for (int i = 0; i < count; i++)
            {
                var clip = Resources.Load<AudioClip>(SfxFolder + stem + i);
                if (clip != null) found.Add(clip);
            }
            return found.ToArray();
        }

        void HitSounds()
        {
            if (bumps == null)
            {
                bumps = Clips("HitBump", 3);
                paddles = Clips("HitPaddle", 3);
                carriages = Clips("CarriageHit", 2);
            }
            hitVoices = new AudioSource[HitVoices];
            for (int i = 0; i < HitVoices; i++)
            {
                var go = new GameObject("Hit (sound)");
                go.transform.SetParent(transform, false);
                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 1f;
                a.rolloffMode = AudioRolloffMode.Linear;
                a.minDistance = 3f;
                a.maxDistance = 30f;
                a.dopplerLevel = 0f;
                SectionSoundGate.Gate(a);
                hitVoices[i] = a;
            }
            YardKit.HitGiven += OnHitGiven;
            CourseHitSound.Heard += OnHitHeard;
        }

        void OnDestroy()
        {
            YardKit.HitGiven -= OnHitGiven;
            CourseHitSound.Heard -= OnHitHeard;
        }

        // On the PC that gave the knock-back: which of the yard's hits it was, sent on to every PC.
        void OnHitGiven(ICharacterDriver driver, Component source, Vector3 push, float knockdown)
        {
            if (source == null || frame == null || !source.transform.IsChildOf(frame)) return;
            // The giant chest's wave sounds its own thud and rumble (on every PC, off the clock).
            if (source.GetComponentInParent<SlamChest>() != null) return;
            int kind = source.GetComponentInParent<RoamingCarriage>() != null ? KindCarriage
                     : source.GetComponentInParent<HorizontalWaterwheel>() != null || source.GetComponentInParent<VerticalWaterwheel>() != null ? KindPaddle
                     : KindBump;
            var at = driver != null && driver.FollowTarget != null ? driver.FollowTarget.position : source.transform.position;
            CourseHitSound.Hit(driver as Component, kind, at, knockdown > 0f ? 1f : .6f);
        }

        // On every PC, when the hit is shown here: played if it is in this yard.
        void OnHitHeard(int kind, Vector3 at, float strength)
        {
            if (hitVoices == null || frame == null || !InYard(at)) return;
            var set = kind == KindCarriage ? carriages : kind == KindPaddle ? paddles : bumps;
            if (set == null || set.Length == 0) set = bumps;
            if (set == null || set.Length == 0) return;
            var a = hitVoices[nextVoice];
            nextVoice = (nextVoice + 1) % hitVoices.Length;
            if (a == null || !a.isActiveAndEnabled) return;
            a.transform.position = at;
            a.pitch = Random.Range(.94f, 1.06f);
            a.PlayOneShot(set[Random.Range(0, set.Length)], strength);
        }

        // The yard's whole ground in its frame (the start court to the final gate's runout, wall to wall, with room).
        bool InYard(Vector3 world)
        {
            var p = frame.InverseTransformPoint(world);
            return p.x > -32f && p.x < 32f && p.z > -24f && p.z < 200f && p.y > -12f && p.y < 40f;
        }
    }
}
