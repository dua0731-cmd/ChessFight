using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx2 (Royal Observatory R7, device VFX: Cassiopeia's star gears, PLAN 5 "맞물리는 자리 작은 불티(드물게)"):
    // now and then a few sparks where this gear's cogs mesh with the one before it (or the standing star's teeth) -
    // under the rim, where the R4 model's teeth stand out, so it never reads as something on the way. The gear turns
    // by the shared obstacle clock (ObstacleMotion: its start pose turned degreesPerSecond * clock); a tooth of this
    // gear comes to the mesh 1.25 times a second, worked out from the same clock, and a seeded few of those moments
    // spark - the same ones on every PC, nothing sent. Only draws: no collider, no device value touched.
    [DisallowMultipleComponent]
    public sealed class RoyalObservatoryVfxGears : MonoBehaviour
    {
        // Off: the gears as modelling left them.
        public static bool Enabled = true;

        [Range(0f, 1f), Tooltip("Share of the teeth coming to the mesh that spark.")]
        public float chance = .2f;
        [Range(2, 40)] public int sparkCount = 12;
        public float sparkLight = 5f;
        [Tooltip("Metres under the deck's top where the cogs mesh (the builder's cog band, -0.95 to -0.3).")]
        public float depth = .62f;
        [Header("Cost")]
        [Tooltip("Gears further than this from the camera (m) do not spark.")]
        public float farDistance = 60f;

        ObstacleMotion motion;
        Transform partner;
        int seed;
        float pitch, rate, meshYaw, span;
        double yaw0;
        MeshRenderer sparks;
        Mesh sprayMesh;
        MaterialPropertyBlock block;
        bool ready;

        // `partner`: what it meshes with (the gear before it, or the standing star); `seed`: this mesh's own.
        public void Configure(Transform partner, int seed)
        {
            this.partner = partner;
            this.seed = seed;
        }

        void Start()
        {
            motion = GetComponent<ObstacleMotion>();
            int teeth = 0;
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).name.StartsWith("Tooth ", System.StringComparison.Ordinal)) teeth++;
            ready = motion != null && partner != null && teeth >= 3 && RoyalObservatoryVfxSparks.Material != null;
            if (!ready)
            {
                enabled = false;
                return;
            }
            block = new MaterialPropertyBlock();
            pitch = 360f / teeth;
            float radius = teeth * .25f;   // RoyalObservatoryBuilder.Gear.Radius: where it meshes
            rate = Mathf.Abs(motion.degreesPerSecond) / pitch;

            var toward = Vector3.ProjectOnPlane(partner.position - transform.position, Vector3.up).normalized;
            meshYaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            // Its start pose (ObstacleMotion keeps it to itself): the pose now with the clock's turn taken off.
            float turned = (float)((ObstacleClock.Now * motion.degreesPerSecond + motion.phase * 360.0) % 360.0);
            yaw0 = (transform.rotation * Quaternion.Inverse(Quaternion.AngleAxis(turned, Vector3.up))).eulerAngles.y;

            // The rims run side by side at the mesh, this gear's way: the sparks are flung along it.
            var run = Mathf.Sign(motion.degreesPerSecond) * Vector3.Cross(Vector3.up, toward);
            sprayMesh = RoyalObservatoryVfxSparks.Spray("RO gear sparks", sparkCount, 61 + seed, 38f, new Vector2(2.5f, 6f),
                                                        new Vector2(.3f, 2.2f), new Vector2(.25f, .5f), true, out span);
            sparks = RoyalObservatoryVfxSparks.Part("Mesh sparks (" + name + ")", transform.parent, sprayMesh);
            sparks.transform.SetPositionAndRotation(transform.position + toward * radius + Vector3.down * depth,
                                                    RoyalObservatoryVfxSparks.Level(run));
            RoyalObservatoryVfxSparks.SparkLook(sparks, block, sparkLight);
        }

        void LateUpdate()
        {
            if (!ready) return;
            var eye = RoyalObservatoryVfxSparks.Eye(transform);
            if (!Enabled || (sparks.transform.position - eye).sqrMagnitude > farDistance * farDistance)
            {
                sparks.enabled = false;
                return;
            }
            // Teeth of this gear past the mesh line: a whole number as one is right at it.
            double now = ObstacleClock.Now;
            double s = Mathf.Sign(motion.degreesPerSecond) * (yaw0 + now * motion.degreesPerSecond + motion.phase * 360.0 - meshYaw) / pitch;
            long slot = (long)System.Math.Floor(s);
            float age = (float)((s - slot) / rate);
            if (RoyalObservatoryVfxSparks.Fires(slot, seed, chance)) RoyalObservatoryVfxSparks.Seek(sparks, block, age, span);
            else sparks.enabled = false;
        }

        void OnDisable()
        {
            if (sparks != null) sparks.enabled = false;
        }

        void OnDestroy()
        {
            if (sprayMesh != null) Destroy(sprayMesh);
            if (sparks != null) Destroy(sparks.gameObject);
        }
    }
}
