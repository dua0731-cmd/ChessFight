using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R684: a Royal Carriage Yard water's surface (the concept: "one that misses a jump splashes into the water"). It sits
    // on the same thin trigger slab as a GardenSplashPool, which throws the garden's splash (crown, fingers, spray,
    // mist, sound - GardenSplashFx); this hands the same moment to YardWater, so the yard's own water shader rings and
    // churns where the pawn went in (_RcySplashes; the garden's rings are in the garden's shader only). Once per pawn per
    // 2 s (a ragdoll is a dozen colliders); strength from how fast it came down, V / 7 m/s, 0.4 .. 1.4. A picture only.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class YardWaterSplash : MonoBehaviour
    {
        [SerializeField] YardWater water;
        [SerializeField] float surface;   // the surface's height over the box's middle (its own frame)

        readonly Dictionary<ICharacterDriver, float> last = new Dictionary<ICharacterDriver, float>();

        public void Configure(YardWater owner, float surfaceOverMiddle)
        {
            water = owner;
            surface = surfaceOverMiddle;
        }

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (water == null) return;
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver == null) return;
            if (last.TryGetValue(driver, out float at) && Time.time - at < 2f) return;
            last[driver] = Time.time;
            if (last.Count > 32) last.Clear();
            var body = other.attachedRigidbody;
            float down = body != null && !body.isKinematic ? -body.linearVelocity.y : 7f;
            var local = transform.InverseTransformPoint(other.bounds.center);
            water.Splash(transform.TransformPoint(new Vector3(local.x, surface, local.z)), Mathf.Clamp(down / 7f, .4f, 1.4f));
        }
    }
}
