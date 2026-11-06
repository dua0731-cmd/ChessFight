using UnityEngine;

namespace ChessFight.PawnRush
{
    // Near/far sound (R290): one sound as two layers on the same spot, mixed by the listener's distance.
    //  near  the close, detailed take: full volume inside NearFull, then thins out (volume and a low-pass that
    //        closes like air swallowing the highs) until it is gone at NearEnd.
    //  far   a muffled, roomy take (low-passed with a reverb tail, made offline): fades in from FarStart, holds,
    //        and fades out by FarEnd, so across the hall you hear the machine's rumble, not its clicks.
    // Unity's own distance roll-off is off (flat custom curve): this class sets every layer's volume itself, and
    // the sources stay 3D only for left/right panning. The owner calls Mix each frame with the sound's own gain
    // (speed, on/off) and Shot for one-off hits.
    public sealed class NearFarSound
    {
        public float NearFull = 4f, NearEnd = 22f, FarStart = 6f, FarEnd = 60f;
        public float FarGain = .8f;

        readonly AudioSource near, far;
        readonly AudioLowPassFilter nearAir;
        // R459: in the war room, its LibraryMuffle (looked for once, on the first sound) and a low-pass on the far layer
        LibraryMuffle muffle;
        AudioLowPassFilter farWall;
        bool muffleLooked;
        float distance;

        static AudioListener listener;
        static readonly AnimationCurve Flat = AnimationCurve.Constant(0f, 1f, 1f);

        public NearFarSound(Transform at, string name, AudioClip nearClip, AudioClip farClip, bool loop)
        {
            near = Source(at, name + " (near)", nearClip, loop, 0f);
            far = Source(at, name + " (far)", farClip, loop, 120f);
            nearAir = near.gameObject.AddComponent<AudioLowPassFilter>();
            nearAir.cutoffFrequency = 22000f;
        }

        static AudioSource Source(Transform at, string name, AudioClip clip, bool loop, float spread)
        {
            var go = new GameObject(name);
            go.transform.SetParent(at, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.clip = clip;
            a.loop = loop;
            a.spatialBlend = 1f;
            a.spread = spread;   // the far layer comes from a wide area, the near one from the machine
            a.dopplerLevel = 0f;
            a.rolloffMode = AudioRolloffMode.Custom;
            a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Flat);
            a.minDistance = 1f;
            a.maxDistance = 500f;
            a.volume = 0f;
            if (loop && clip != null)
            {
                a.time = Random.value * clip.length;   // loops on one machine never share a seam
                a.Play();
            }
            return a;
        }

        // A stone hall's ring on both layers (R316): `decay` seconds of tail, `wet` the tail's level in millibels
        // against the dry sound (0 = as loud as it). The far layer already carries its own offline reverb, so it
        // gets the same tail a little quieter.
        public NearFarSound Hall(float decay, float wet)
        {
            Ring(near, decay, wet);
            Ring(far, decay, wet - 300f);
            return this;
        }

        static void Ring(AudioSource a, float decay, float wet)
        {
            var r = a.gameObject.AddComponent<AudioReverbFilter>();
            r.reverbPreset = AudioReverbPreset.User;
            r.dryLevel = 0f;
            r.room = 0f;
            r.roomHF = -300f;           // stone keeps the lows longer than the highs
            r.decayTime = decay;
            r.decayHFRatio = .6f;
            r.reflectionsLevel = -400f;
            r.reflectionsDelay = .02f;
            r.reverbLevel = wet;
            r.reverbDelay = .04f;
            r.diffusion = 100f;
            r.density = 100f;
        }

        public float Distance => distance;
        public float NearWeight => 1f - Ramp(NearFull, NearEnd, distance);
        public float FarWeight => Ramp(FarStart, FarStart + 10f, distance) * (1f - Ramp(FarEnd * .6f, FarEnd, distance)) * FarGain;

        // gain: the sound's own loudness this frame (0 = silent), pitch the same for both layers.
        public void Mix(float gain, float pitch)
        {
            Measure();
            float m = Muffle();
            gain *= LibraryMuffle.Gain(m) * Presence();
            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            Set(near, gain * NearWeight, pitch, k);
            Set(far, gain * FarWeight, pitch, k);
            // Air absorption: 22 kHz up close down to about 1.5 kHz where the near layer ends; through stone (R459)
            // no higher than the muffle's cutoff.
            float t = Ramp(NearFull, NearEnd, distance);
            float air = Mathf.Min(Mathf.Lerp(22000f, 1500f, Mathf.Sqrt(t)), LibraryMuffle.CutoffAt(m));
            nearAir.cutoffFrequency = Mathf.Lerp(nearAir.cutoffFrequency, air, k);
            if (farWall != null) farWall.cutoffFrequency = LibraryMuffle.CutoffAt(m);
        }

        public void Shot(AudioClip nearClip, AudioClip farClip, float volume, float pitch)
        {
            Measure();
            float m = Muffle();
            volume *= LibraryMuffle.Gain(m) * Presence();
            if (volume <= .001f) return;
            if (farWall != null) farWall.cutoffFrequency = LibraryMuffle.CutoffAt(m);
            // A one-shot plays at volume x the source's volume, so a shot's own source carries the layer weight.
            Fire(near, nearClip, NearWeight, volume, pitch);
            Fire(far, farClip, FarWeight, volume, pitch);
            nearAir.cutoffFrequency = Mathf.Min(Mathf.Lerp(22000f, 1500f, Mathf.Sqrt(Ramp(NearFull, NearEnd, distance))),
                LibraryMuffle.CutoffAt(m));
        }

        static void Fire(AudioSource a, AudioClip clip, float weight, float volume, float pitch)
        {
            if (clip == null || weight <= .01f) return;
            a.volume = weight;
            a.pitch = pitch;
            a.PlayOneShot(clip, volume);
        }

        // R495: a war room's sounds only in and just around it (1 for every other sound).
        float Presence()
        {
            Muffle();
            return muffle != null ? muffle.Presence : 1f;
        }

        // R459: how muffled the war room's sounds are now (0 outside a war room).
        float Muffle()
        {
            if (!muffleLooked)
            {
                muffleLooked = true;
                muffle = near.GetComponentInParent<LibraryMuffle>();
                if (muffle != null)
                {
                    farWall = far.gameObject.AddComponent<AudioLowPassFilter>();
                    farWall.cutoffFrequency = 22000f;
                }
            }
            return muffle != null ? muffle.Amount : 0f;
        }

        void Measure()
        {
            if (listener == null || !listener.isActiveAndEnabled) listener = Object.FindAnyObjectByType<AudioListener>();
            distance = listener != null ? Vector3.Distance(listener.transform.position, near.transform.position) : 0f;
        }

        static void Set(AudioSource a, float volume, float pitch, float k)
        {
            if (a.clip == null) return;
            a.volume = Mathf.Lerp(a.volume, volume, k);
            a.pitch = Mathf.Lerp(a.pitch, pitch, k);
        }

        static float Ramp(float from, float to, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, x));
    }
}
