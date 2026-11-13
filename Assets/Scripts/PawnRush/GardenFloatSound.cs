using System.Collections.Generic;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R536 (승규 11-10, "다음 사운드"): the garden's beat 3 - the eight bobbing drums, the shortcut's three bobbing posts and
    // its two lotus pads.
    //  float  water sloshing against a floating wooden drum: one loop per drum or post, as loud as it is moving up or
    //         down (still at the top and bottom of its bob, fullest half way), a little louder going down (it pushes the
    //         water away); the posts bob faster, so they slosh more
    //  tip    a lotus pad tipping into the water: one slap-and-splash as it tips, the same splash softer and lower as it
    //         rights itself
    // NearFarSound like the plaza's sounds; the motion is ObstacleMotion / TiltingPad, this only listens. GardenPlazaSound
    // adds this on waking (the garden is built into the scene, so nothing new has to be built in).
    public sealed class GardenFloatSound : MonoBehaviour
    {
        const string Folder = "PawnRushGardenSfx/";

        sealed class Float { public Transform at; public NearFarSound sound; public float lastY, speed, top, pitch; }
        sealed class Pad { public TiltingPad pad; public NearFarSound sound; public bool tipped; public float pitch; }

        readonly List<Float> floats = new List<Float>();
        readonly List<Pad> pads = new List<Pad>();
        AudioClip splash, splashFar;

        void Start()
        {
            var section = GetComponentInParent<PawnRushSection>();
            var root = section != null ? section.transform : transform.root;
            AudioClip loop = Load("FloatSlosh"), loopFar = Load("FloatSloshFar");
            splash = Load("PadSplash");
            splashFar = Load("PadSplashFar");
            int n = 0;
            foreach (var motion in root.GetComponentsInChildren<ObstacleMotion>(true))
            {
                string name = motion.name;
                bool drum = name.StartsWith("Bobbing platform P"), post = name.StartsWith("Shortcut post K") && !name.Contains("spin");
                if (!drum && !post) continue;
                // R543: the drums also answer a piece landing on them (GardenDrumDip; R635: they tip about its feet).
                if (drum && motion.GetComponent<GardenDrumDip>() == null) motion.gameObject.AddComponent<GardenDrumDip>();
                // top speed of a sine bob: 2 pi (amplitude) / period
                float top = 2f * Mathf.PI * Mathf.Max(.05f, motion.distance) / Mathf.Max(.5f, motion.period);
                floats.Add(new Float
                {
                    at = motion.transform, lastY = motion.transform.position.y, top = top,
                    pitch = (drum ? .92f : 1.08f) + .08f * ((n++ * .618034f) % 1f),
                    sound = new NearFarSound(motion.transform, "Float slosh sound", loop, loopFar, true)
                        { NearFull = 2f, NearEnd = 12f, FarStart = 5f, FarEnd = 24f, FarGain = .5f },
                });
            }
            foreach (var pad in root.GetComponentsInChildren<TiltingPad>(true))
                pads.Add(new Pad
                {
                    pad = pad, pitch = 1f + .06f * pads.Count,
                    sound = new NearFarSound(pad.transform, "Pad splash sound", null, null, false)
                        { NearFull = 3f, NearEnd = 18f, FarStart = 6f, FarEnd = 30f, FarGain = .6f },
                });
        }

        static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogWarning("[정원] 소리 파일이 없습니다: Resources/" + Folder + name);
            return clip;
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            foreach (var f in floats)
            {
                float y = f.at.position.y, v = (y - f.lastY) / dt;
                f.lastY = y;
                f.speed = Mathf.Lerp(f.speed, v, 1f - Mathf.Exp(-8f * dt));
                float k = Mathf.Clamp01(Mathf.Abs(f.speed) / f.top);
                float gain = (.12f + .5f * k * (f.speed < 0f ? 1.2f : .85f)) * (f.top > .6f ? 1.1f : 1f);
                f.sound.Mix(gain, f.pitch * (.96f + .06f * k));
            }
            foreach (var p in pads)
            {
                bool tipped = p.pad.Tipped;
                if (tipped != p.tipped && splash != null)
                    p.sound.Shot(splash, splashFar, tipped ? 1f : .45f, tipped ? p.pitch : p.pitch * .85f);
                p.tipped = tipped;
            }
        }
    }
}
