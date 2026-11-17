using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 2026-11-17 (승규 "4·5번 물레방아·물"): the Royal Carriage Yard's standing loops (RoyalCarriageYard.Sound.cs makes it).
    //   wheels   a wooden axle groaning and rumbling on each waterwheel (the giant crossing wheel, the lifting wheel), at
    //            its axle, its pitch with how fast it turns (measured off its transform, so it stops when the wheel does)
    //   falls    pouring water where the water falls (YardWaterfall: the giant wheel's buckets, the lifting wheel and the
    //            cliff, each sluice), at the foot of what is drawn - with the water look off, at the wheels' feet
    //   flow     calm water lapping in the canal and the mill pond, from the point of each surface nearest the listener
    // Every loop starts at its own place in its clip, so two of one kind do not drone together. Pictures-and-sounds only,
    // read off what is drawn: the same on every PC. Every source goes through SectionSoundGate.
    public sealed partial class YardSoundscape : MonoBehaviour
    {
        const string SfxFolder = "PawnRushCarriageYardSfx/";
        const float Refind = 1f;   // s between looking again where a fall's water lands

        sealed class Wheel
        {
            public Transform t;
            public AudioSource a;
            public Quaternion last;
            public float rate, still;   // deg/s now, and the rate it turns at in the yard (the pitch's 1)
        }

        sealed class Fall
        {
            public Renderer r;
            public AudioSource a;
        }

        readonly List<Wheel> wheels = new List<Wheel>();
        readonly List<Fall> falls = new List<Fall>();
        readonly List<AudioSource> flows = new List<AudioSource>();
        Transform frame;
        Bounds[] waters = new Bounds[0];
        AudioListener ear;
        float refound = -99f;
        int started;

        public void Configure(Transform frame, Bounds[] waters)
        {
            this.frame = frame;
            this.waters = waters;
            var turn = Resources.Load<AudioClip>(SfxFolder + "WheelTurn");
            var pour = Resources.Load<AudioClip>(SfxFolder + "WaterPour");
            var flow = Resources.Load<AudioClip>(SfxFolder + "WaterFlow");
            if (turn != null)
            {
                foreach (var w in frame.GetComponentsInChildren<HorizontalWaterwheel>(true))
                    wheels.Add(new Wheel { t = w.transform, a = Loop(w.gameObject, turn, 7f, 45f), last = w.transform.rotation, still = 15f });
                foreach (var w in frame.GetComponentsInChildren<VerticalWaterwheel>(true))
                    wheels.Add(new Wheel { t = w.transform, a = Loop(w.gameObject, turn, 5f, 35f), last = w.transform.rotation, still = 28f });
            }
            if (pour != null)
            {
                foreach (var f in frame.GetComponentsInChildren<YardWaterfall>(true))
                {
                    var r = f.GetComponentInChildren<Renderer>(true);
                    var go = new GameObject("Fall (sound): " + f.name);
                    go.transform.SetParent(transform, false);
                    go.transform.position = f.transform.position;
                    falls.Add(new Fall { r = r, a = Loop(go, pour, 5f, 40f) });
                }
                if (falls.Count == 0)
                    foreach (var w in wheels)
                    {
                        var go = new GameObject("Fall (sound): " + w.t.name);
                        go.transform.SetParent(w.t.parent, false);
                        go.transform.position = w.t.position + Vector3.down * 5f;
                        falls.Add(new Fall { a = Loop(go, pour, 5f, 40f) });
                    }
            }
            HitSounds();   // 13번 타격음 (YardSoundscape.Hits.cs)
            if (flow != null)
                foreach (var b in waters)
                {
                    var go = new GameObject("Water (sound)");
                    go.transform.SetParent(transform, false);
                    flows.Add(Loop(go, flow, 3f, 22f));
                }
        }

        AudioSource Loop(GameObject on, AudioClip clip, float near, float far)
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
            a.volume = 1f;
            a.time = clip.length * ((started++ * .37f) % 1f);
            SectionSoundGate.Gate(a);
            return a;
        }

        // 승규 11-17 "물 소리가 30퍼 줄여": the falling and the flowing water at 0.7 (about -3 dB).
        const float WaterVolume = .7f;

        static void Keep(AudioSource a, float volume = 1f)
        {
            if (a == null) return;
            a.volume = volume;   // the effects volume and the section are the gate's
            if (!a.isPlaying && a.isActiveAndEnabled) a.Play();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            foreach (var w in wheels)
            {
                if (w.t == null) continue;
                float turned = Quaternion.Angle(w.last, w.t.rotation);
                w.last = w.t.rotation;
                if (dt > 0f) w.rate = Mathf.Lerp(w.rate, turned / dt, 1f - Mathf.Exp(-dt * 4f));
                float share = Mathf.Clamp01(w.rate / Mathf.Max(1f, w.still));
                Keep(w.a);
                w.a.volume = share;
                w.a.pitch = Mathf.Lerp(.85f, 1f, share);
            }
            if (Time.time - refound >= Refind)
            {
                refound = Time.time;
                foreach (var f in falls)
                    if (f.r != null && f.r.bounds.size.sqrMagnitude > .01f)
                    {
                        var b = f.r.bounds;
                        f.a.transform.position = new Vector3(b.center.x, b.min.y + .5f, b.center.z);
                    }
            }
            foreach (var f in falls) Keep(f.a, WaterVolume);
            if (flows.Count > 0)
            {
                if (ear == null || !ear.isActiveAndEnabled) ear = FindAnyObjectByType<AudioListener>();
                for (int i = 0; i < flows.Count && i < waters.Length; i++)
                {
                    var b = waters[i];
                    if (ear != null && frame != null)
                    {
                        var p = frame.InverseTransformPoint(ear.transform.position);
                        var near = new Vector3(Mathf.Clamp(p.x, b.min.x, b.max.x), b.center.y, Mathf.Clamp(p.z, b.min.z, b.max.z));
                        flows[i].transform.position = frame.TransformPoint(near);
                    }
                    Keep(flows[i], WaterVolume);
                }
            }
        }
    }
}
