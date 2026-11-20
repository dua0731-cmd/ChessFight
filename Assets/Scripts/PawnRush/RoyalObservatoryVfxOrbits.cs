using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx2 (Royal Observatory R7, device VFX: the bowl's crossing orbits, PLAN 5 "톱니 별 · 궤도 · 구르는 행성"):
    // now and then a few sparks where a platform's brass rim runs over its orbit's gold track - at the rim's leading
    // edge, flung on ahead and down into the dark. The ring turns by the shared obstacle clock (ObstacleMotion); the
    // moments are seeded slots of that clock, the same on every PC, nothing sent. Only draws.
    [DisallowMultipleComponent]
    public sealed class RoyalObservatoryVfxOrbits : MonoBehaviour
    {
        // RoyalObservatoryBuilder's: platforms 3.6 m out, 1.45 m across the half, the track 0.5 m under the ring.
        const float OrbitRadius = 3.6f, PlatformRadius = 1.45f, TrackDepth = .5f;
        const int Platforms = 3;

        public static bool Enabled = true;

        [Tooltip("Seconds between a platform's chances to spark.")]
        public float period = 1f;
        [Range(0f, 1f), Tooltip("Share of those chances that spark.")]
        public float chance = .12f;
        [Range(2, 30)] public int sparkCount = 8;
        public float sparkLight = 4.5f;
        [Header("Cost")]
        public float farDistance = 60f;

        ObstacleMotion motion;
        Quaternion start;
        int seed;
        float span;
        Mesh sprayMesh;
        readonly MeshRenderer[] sparks = new MeshRenderer[Platforms];
        MaterialPropertyBlock block;
        bool ready;

        public void Configure(int seed) => this.seed = seed;

        void Start()
        {
            motion = GetComponent<ObstacleMotion>();
            ready = motion != null && RoyalObservatoryVfxSparks.Material != null;
            if (!ready)
            {
                enabled = false;
                return;
            }
            block = new MaterialPropertyBlock();
            start = transform.rotation * Quaternion.Inverse(Quaternion.AngleAxis(Turned(ObstacleClock.Now), Vector3.up));
            sprayMesh = RoyalObservatoryVfxSparks.Spray("RO orbit sparks", sparkCount, 71 + seed, 30f, new Vector2(2.4f, 4.5f),
                                                        new Vector2(.1f, 1.2f), new Vector2(.22f, .42f), false, out span);
            for (int n = 0; n < Platforms; n++)
            {
                sparks[n] = RoyalObservatoryVfxSparks.Part("Track sparks " + (n + 1) + " (" + name + ")", transform.parent, sprayMesh);
                RoyalObservatoryVfxSparks.SparkLook(sparks[n], block, sparkLight);
            }
        }

        float Turned(double time) => (float)((time * motion.degreesPerSecond + motion.phase * 360.0) % 360.0);

        void LateUpdate()
        {
            if (!ready) return;
            var eye = RoyalObservatoryVfxSparks.Eye(transform);
            bool on = Enabled && (transform.position - eye).sqrMagnitude < farDistance * farDistance;
            double now = ObstacleClock.Now;
            float way = Mathf.Sign(motion.degreesPerSecond);
            // Where the track leaves the platform's rim ahead of it: this far round from the platform's middle.
            float lead = way * PlatformRadius / OrbitRadius * Mathf.Rad2Deg;
            for (int n = 0; n < Platforms; n++)
            {
                var r = sparks[n];
                double offset = n / (double)Platforms + seed * .173;
                double s = now / period + offset;
                long slot = (long)System.Math.Floor(s);
                if (!on || !RoyalObservatoryVfxSparks.Fires(slot, seed * 3 + n, chance))
                {
                    r.enabled = false;
                    continue;
                }
                double at = (slot - offset) * period;
                // The ring as it was when it sparked; the sparks stay where they were flung.
                var pose = start * Quaternion.AngleAxis(Turned(at), Vector3.up);
                var point = Quaternion.Euler(0f, n * 120f + lead, 0f) * Vector3.forward * OrbitRadius + Vector3.down * TrackDepth;
                var run = way * Vector3.Cross(Vector3.up, point);
                r.transform.SetPositionAndRotation(transform.position + pose * point, RoyalObservatoryVfxSparks.Level(pose * run));
                RoyalObservatoryVfxSparks.Seek(r, block, (float)(now - at), span);
            }
        }

        void OnDisable()
        {
            foreach (var r in sparks) if (r != null) r.enabled = false;
        }

        void OnDestroy()
        {
            if (sprayMesh != null) Destroy(sprayMesh);
            foreach (var r in sparks) if (r != null) Destroy(r.gameObject);
        }
    }
}
