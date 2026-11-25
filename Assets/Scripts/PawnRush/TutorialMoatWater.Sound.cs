using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-25 (the tutorial sound survey's 5, the moat): the Royal Carriage Yard's water loops (YardSoundscape), reused:
    //   falls  WaterPour at each of the eight two-tier falls, halfway down the lower sheet, heard from 40 m, full by 5 m
    //   moat   WaterFlow lapping, from the point of the moat's ring nearest the listener, heard from 22 m, full by 3 m
    // Each loop starts at its own place in the clip, so the eight falls do not drone together. Sounds only, nothing sent.
    public sealed partial class TutorialMoatWater
    {
        const string SfxFolder = "PawnRushCarriageYardSfx/";
        const float FallVolume = .7f, FlowVolume = .7f;   // YardSoundscape.WaterVolume
        readonly List<AudioSource> pours = new List<AudioSource>();
        AudioSource lap;
        AudioListener ear;
        AudioClip pourClip;
        bool pourLoaded;

        void AddPour(Vector3 at)
        {
            if (Application.isBatchMode) return;
            if (!pourLoaded) { pourLoaded = true; pourClip = Resources.Load<AudioClip>(SfxFolder + "WaterPour"); }
            if (pourClip == null) return;
            var go = new GameObject("Fall (sound) " + pours.Count);
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            pours.Add(Loop(go, pourClip, 5f, 40f));
        }

        void AddLap()
        {
            if (Application.isBatchMode) return;
            var clip = Resources.Load<AudioClip>(SfxFolder + "WaterFlow");
            if (clip == null) return;
            var go = new GameObject("Moat (sound)");
            go.transform.SetParent(transform, false);
            lap = Loop(go, clip, 3f, 22f);
        }

        static AudioSource Loop(GameObject on, AudioClip clip, float near, float far)
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
            a.time = Random.Range(0f, clip.length * .95f);
            return a;
        }

        void LateUpdate()
        {
            float effects = ChessFight.Game.GameSettings.Effects;
            foreach (var a in pours) Keep(a, FallVolume * effects);
            if (lap == null) return;
            if (ear == null || !ear.isActiveAndEnabled) ear = FindAnyObjectByType<AudioListener>();
            if (ear != null)
            {
                // the nearest point of the moat's ring (between the rim's inner edge and the yard): the nearest edge of the rectangle
                var p = ear.transform.position;
                float x = Mathf.Clamp(p.x, moat.x, moat.z), z = Mathf.Clamp(p.z, moat.y, moat.w);
                float dx = Mathf.Min(x - moat.x, moat.z - x), dz = Mathf.Min(z - moat.y, moat.w - z);
                if (dx < dz) x = x - moat.x < moat.z - x ? moat.x : moat.z;
                else z = z - moat.y < moat.w - z ? moat.y : moat.w;
                lap.transform.position = new Vector3(x, surfaceY, z);
            }
            Keep(lap, FlowVolume * effects);
        }

        static void Keep(AudioSource a, float volume)
        {
            if (a == null) return;
            a.volume = volume;
            if (!a.isPlaying && a.isActiveAndEnabled) a.Play();
        }
    }
}
