using System.Collections.Generic;
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
    // R767 (승규 11-22, "1 2 3 4 해봐" - the garden's silent spots, beats 2 .. 4): the rest of a jet's cycle.
    //  warn   the grate gurgling as the warning starts (GeyserWarn)
    //  fall   the column collapsing onto the stone in the jet's last quarter second (GeyserFall)
    //  hit    a piece thrown by the column: a wet slap where it stands, once a jet a piece (GeyserHit). The throw itself
    //         is the authority's, but every PC draws every piece, so each looks for them in the column on its own
    //         (GardenGeyser's own capsule) - the same moment everywhere, nothing sent.
    // The column standing between the burst and the fall is the burst's own spray tail. waterworks_sounds.py makes the
    // three new files.
    [RequireComponent(typeof(GardenGeyser))]
    public sealed class GardenGeyserSound : MonoBehaviour
    {
        const string Folder = "PawnRushGardenSfx/";
        const float Lead = .148f;       // s from GeyserBurst.wav's start to its loudest 10 ms (geyser_sounds.py)
        const float FallLead = .05f;    // GeyserFall.wav stays level all through: it starts as the column falls
        const float WarnGain = .4f, FallGain = .5f, HitGain = .65f;
        const float Heard = 30f;        // m: past this nobody looks for pieces in the column

        int order;
        GardenGeyser geyser;
        NearFarSound burst, warn, fall, hit;
        AudioClip near, far, warnNear, warnFar, fallNear, fallFar, hitNear, hitFar;
        Transform hitAt;
        float pitch;
        // R542 (승규 11-10, "물 대포 소리가 사진 범위까지 들려서 이 구간에 오면 소리를 줄이고 회전 막대 구간은 그대로 해"):
        // the floating drums and the shortcut's posts beyond the jets. While the listener is over them the bursts play at
        // DrumGain; Edge m outside it fades back to full, so the spin bar side before the jets hears them as before.
        const float DrumGain = .3f, Edge = 4f;
        Bounds drums;
        bool hasDrums;
        double lastPhase = double.NaN;
        long lastCycle;
        readonly Collider[] overlaps = new Collider[48];
        readonly HashSet<ICharacterDriver> struck = new HashSet<ICharacterDriver>();
        long struckCycle = long.MinValue;
        static AudioListener listener;

        void Start()
        {
            geyser = GetComponent<GardenGeyser>();
            order = geyser.Order;
            near = Load("GeyserBurst");
            far = Load("GeyserBurstFar");
            warnNear = Load("GeyserWarn");
            warnFar = Load("GeyserWarnFar");
            fallNear = Load("GeyserFall");
            fallFar = Load("GeyserFallFar");
            hitNear = Load("GeyserHit");
            hitFar = Load("GeyserHitFar");
            burst = new NearFarSound(transform, "Geyser burst sound", null, null, false)
                { NearFull = 3f, NearEnd = 20f, FarStart = 6f, FarEnd = 40f, FarGain = .7f };
            warn = new NearFarSound(transform, "Geyser warn sound", null, null, false)
                { NearFull = 2f, NearEnd = 12f, FarStart = 5f, FarEnd = 22f, FarGain = .5f };
            fall = new NearFarSound(transform, "Geyser fall sound", null, null, false)
                { NearFull = 3f, NearEnd = 18f, FarStart = 6f, FarEnd = 34f, FarGain = .6f };
            hitAt = new GameObject("Geyser hit sound point").transform;
            hitAt.SetParent(transform, false);
            hit = new NearFarSound(hitAt, "Geyser hit sound", null, null, false)
                { NearFull = 4f, NearEnd = 22f, FarStart = 6f, FarEnd = 40f, FarGain = .6f };
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
            const double warnAt = 0;
            double fireAt = RoyalWaterGardenRules.GeyserWarn - Lead;
            double fallAt = RoyalWaterGardenRules.GeyserWarn + RoyalWaterGardenRules.GeyserJet - .25 - FallLead;
            bool fresh = !double.IsNaN(lastPhase);
            bool warned = fresh && Crossed(warnAt, phase, cycle);
            bool fired = fresh && Crossed(fireAt, phase, cycle);
            bool fell = fresh && Crossed(fallAt, phase, cycle);
            lastPhase = phase;
            lastCycle = cycle;
            float gain = warned || fired || fell ? Gain() : 1f;
            if (warned && warnNear != null) warn.Shot(warnNear, warnFar, WarnGain * gain, pitch * Random.Range(.96f, 1.04f));
            if (fired && near != null) burst.Shot(near, far, gain, pitch);
            if (fell && fallNear != null) fall.Shot(fallNear, fallFar, FallGain * gain, pitch * Random.Range(.96f, 1.04f));
            if (state == RoyalWaterGardenRules.Jet.Jet && into <= RoyalWaterGardenRules.GeyserJet - .25) Watch(cycle);
        }

        // Once per cycle, when the phase crosses the moment; a late frame inside 0.25 s still fires, later not.
        bool Crossed(double moment, double phase, long cycle) =>
            phase >= moment && phase < moment + .25 && (cycle != lastCycle || lastPhase < moment);

        // R767: pieces in the column while it throws (GardenGeyser.FixedUpdate's capsule), each heard once a jet.
        void Watch(long cycle)
        {
            if (hitNear == null) return;
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            if (listener == null || (listener.transform.position - transform.position).sqrMagnitude > Heard * Heard) return;
            if (cycle != struckCycle) { struck.Clear(); struckCycle = cycle; }
            var foot = transform.position;
            int n = Physics.OverlapCapsuleNonAlloc(foot + Vector3.up * .2f, foot + Vector3.up * 2.2f, geyser.Radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var driver = overlaps[i].GetComponentInParent<ICharacterDriver>();
                if (driver == null || !struck.Add(driver)) continue;
                hitAt.position = overlaps[i].bounds.center;
                hit.Shot(hitNear, hitFar, HitGain, Random.Range(.93f, 1.06f));
            }
        }

        float Gain()
        {
            if (!hasDrums) return 1f;
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            if (listener == null) return 1f;
            float outside = Mathf.Sqrt(drums.SqrDistance(listener.transform.position));
            return Mathf.Lerp(DrumGain, 1f, outside / Edge);
        }
    }
}
