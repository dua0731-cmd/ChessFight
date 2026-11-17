using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "1번 말 마차" - hooves, a call before the charge, a hit - "너가 만들어 일레븐랩스 mcp 연결해서"): the
    // horses' own sounds, on top of the code-made wheel rumble and whip crack. Takes made with ElevenLabs sound effects,
    // baked by ArtSource/PawnRush/Sfx/bake_carriage_yard.py into Resources/PawnRushCarriageYardSfx (prompts:
    // Docs/PawnRush/PAWN_RUSH_AUDIO.md section 10).
    //   hooves  one strike each time a hoof comes down in the drawn stride (YardHorse.Footfall): a walk's or trot's
    //           lighter clop, a gallop's heavier one (from half a gallop), louder the harder it runs; at the horses'
    //           heads, so a team is heard from its front. From the pose, so every PC hears the legs it draws.
    //   call    a snort or a neigh as the aim starts (its lamps red, the charge 0.6 - 1.2 s off), chosen by the aim's
    //           start time so every PC hears the same one; not when first seen more than Late into the aim.
    //   hit     (its CarriageHit thud) sounds from the yard's hits with the others, sent to every PC
    //           (YardSoundscape.Hits.cs: 승규 "온라인 참가자 pc 에서도 다 들리게 해").
    // Every source goes through SectionSoundGate (heard only in this section, times the effects volume).
    public sealed partial class RoamingCarriage
    {
        const string SfxFolder = "PawnRushCarriageYardSfx/";
        const float Late = .3f;   // s into the aim past which its call is skipped
        static AudioClip[] hoofLight, hoofHeavy, calls;
        AudioSource hooves, call;
        int lastHoof = -1;

        static AudioClip[] Clips(params string[] names)
        {
            var found = new System.Collections.Generic.List<AudioClip>();
            foreach (var name in names)
            {
                var clip = Resources.Load<AudioClip>(SfxFolder + name);
                if (clip != null) found.Add(clip);
            }
            return found.ToArray();
        }

        static AudioClip[] Numbered(string stem, int count)
        {
            var names = new string[count];
            for (int i = 0; i < count; i++) names[i] = stem + i;
            return Clips(names);
        }

        void HorseSounds()
        {
            if (horses == null || horses.Length == 0) return;
            if (hoofLight == null)
            {
                hoofLight = Numbered("HoofLight", 6);
                hoofHeavy = Numbered("HoofHeavy", 6);
                calls = Clips("HorseSnort0", "HorseNeigh0", "HorseSnort1", "HorseNeigh1");
            }
            // At the horses' heads, carried with the forecarriage (a team) or the horse.
            var head = new GameObject("Horses (sound)");
            head.transform.SetParent(fore != null ? fore : transform, false);
            Vector3 mid = Vector3.zero;
            int n = 0;
            foreach (var horse in horses)
                if (horse != null && horse.Root != null) { mid += horse.Root.position; n++; }
            head.transform.position = (n > 0 ? mid / n : transform.position) + transform.forward * .8f + Vector3.up * .6f;
            if (hoofLight.Length > 0 || hoofHeavy.Length > 0)
            {
                hooves = Source(head, 3f, 30f);
                foreach (var horse in horses)
                    if (horse != null) horse.Footfall = Footfall;
            }
            if (calls.Length > 0) call = Source(head, 5f, 40f);
        }

        static AudioSource Source(GameObject on, float near, float far)
        {
            var a = on.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.dopplerLevel = 0f;
            SectionSoundGate.Gate(a);
            return a;
        }

        void Footfall(int leg, float gallop, float stir)
        {
            if (hooves == null || !hooves.isActiveAndEnabled) return;
            var set = (gallop >= .5f && hoofHeavy.Length > 0) || hoofLight.Length == 0 ? hoofHeavy : hoofLight;
            if (set.Length == 0) return;
            int k = Random.Range(0, set.Length);
            if (set.Length > 1 && k == lastHoof) k = (k + 1) % set.Length;
            lastHoof = k;
            // Fore hooves land on the head's side of the pair, the hinds a little softer.
            float volume = Mathf.Lerp(.55f, 1f, gallop) * Mathf.Clamp01(stir) * (leg < 2 ? 1f : .85f);
            hooves.PlayOneShot(set[k], volume);
        }

        void Calls(RoamingFleet.Pose pose)
        {
            if (call == null || calls.Length == 0 || !call.isActiveAndEnabled) return;
            if (pose.Stage != RoamingFleet.Stage.Aiming || shown == RoamingFleet.Stage.Aiming || double.IsNaN(pose.StageStart)) return;
            if (ObstacleClock.Now + phase - pose.StageStart > Late) return;
            // The same call on every PC: picked by when the aim started.
            int k = (int)(System.Math.Abs(System.Math.Floor(pose.StageStart * 3.7)) + index) % calls.Length;
            call.PlayOneShot(calls[k], 1f);
        }
    }
}
