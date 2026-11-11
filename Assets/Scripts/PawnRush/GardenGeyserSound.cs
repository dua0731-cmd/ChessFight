using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R534 (승규 11-10, "물 소리가 팍! 튀어나오는 소리로 다시 해"): a canal geyser's burst - one sharp 팍 of water as it
    // fires, its loudest moment on the instant the jet starts throwing (GardenGeyser's hit and GardenJet's water start
    // together, R533). Timed from the shared obstacle clock like the jet itself, so every PC hears it on the same beat.
    // GeyserBurst.wav / GeyserBurstFar.wav come from geyser_sounds.py (ArtSource/PawnRush/GardenConcept); a NearFarSound
    // like the plaza's other sounds. GardenJet adds this on waking.
    [RequireComponent(typeof(GardenGeyser))]
    public sealed class GardenGeyserSound : MonoBehaviour
    {
        const string Folder = "PawnRushGardenSfx/";
        const float Lead = .148f;   // s from GeyserBurst.wav's start to its loudest 10 ms (geyser_sounds.py)

        int order;
        NearFarSound burst;
        AudioClip near, far;
        float pitch;
        // R542 (승규 11-10, "물 대포 소리가 사진 범위까지 들려서 이 구간에 오면 소리를 줄이고 회전 막대 구간은 그대로 해"):
        // the floating drums and the shortcut's posts beyond the jets. While the listener is over them the bursts play at
        // DrumGain; Edge m outside it fades back to full, so the spin bar side before the jets hears them as before.
        const float DrumGain = .3f, Edge = 4f;
        Bounds drums;
        bool hasDrums;
        double lastPhase = double.NaN;
        long lastCycle;

        void Start()
        {
            order = GetComponent<GardenGeyser>().Order;
            near = Load("GeyserBurst");
            far = Load("GeyserBurstFar");
            burst = new NearFarSound(transform, "Geyser burst sound", null, null, false)
                { NearFull = 3f, NearEnd = 20f, FarStart = 6f, FarEnd = 40f, FarGain = .7f };
            var section = GetComponentInParent<PawnRushSection>();
            foreach (var m in (section != null ? section.transform : transform.root).GetComponentsInChildren<ObstacleMotion>(true))
            {
                string n = m.name;
                if (!n.StartsWith("Bobbing platform P") && !(n.StartsWith("Shortcut post K") && !n.Contains("spin"))) continue;
                if (!hasDrums) drums = new Bounds(m.transform.position, Vector3.zero);
                drums.Encapsulate(m.transform.position);
                hasDrums = true;
            }
            drums.Expand(new Vector3(6f, 1000f, 6f));   // the drums' own width, and every height
            pitch = .94f + .12f * ((order * .618034f) % 1f);   // the eight jets do not sound like one sample
        }

        static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogWarning("[정원] 소리 파일이 없습니다: Resources/" + Folder + name);
            return clip;
        }

        void Update()
        {
            double now = ObstacleClock.Now, into;
            var state = RoyalWaterGardenRules.GeyserState(order, now, out into);
            long cycle = RoyalWaterGardenRules.GeyserCycleIndex(order, now);
            // seconds since this cycle began to warn
            double phase = state == RoyalWaterGardenRules.Jet.Warn ? into
                : state == RoyalWaterGardenRules.Jet.Jet ? RoyalWaterGardenRules.GeyserWarn + into
                : RoyalWaterGardenRules.GeyserWarn + RoyalWaterGardenRules.GeyserJet + into;
            double fire = RoyalWaterGardenRules.GeyserWarn - Lead;
            // Fired once per cycle, when the phase crosses the moment; a late frame inside 0.25 s still fires, later not.
            bool crossed = !double.IsNaN(lastPhase) && phase >= fire && phase < fire + .25
                && (cycle != lastCycle || lastPhase < fire);
            lastPhase = phase;
            lastCycle = cycle;
            if (crossed && near != null) burst.Shot(near, far, Gain(), pitch);
        }

        float Gain()
        {
            var listener = hasDrums ? FindAnyObjectByType<AudioListener>() : null;
            if (listener == null) return 1f;
            float outside = Mathf.Sqrt(drums.SqrDistance(listener.transform.position));
            return Mathf.Lerp(DrumGain, 1f, outside / Edge);
        }
    }
}
