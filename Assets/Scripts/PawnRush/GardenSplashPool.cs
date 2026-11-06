using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R442 (승규 11-05, "기물이 바닥에 떨어졌을 때 물에 풍덩 한 것처럼 해"): a garden pool's surface. A piece that comes down
    // through it splashes where it goes in (GardenSplashFx), once per dip. Every PC sees its own: the pieces' colliders
    // move on every PC (a remote piece's too), so nothing is sent. The box is a thin slab round the surface, read
    // axis-aligned; `surface` is the water's height in its own frame.
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GardenSplashPool : MonoBehaviour
    {
        // A remote piece moves kinematically (no velocity to read): a fall of about 2.5 m.
        const float DefaultSpeed = 7f;

        [SerializeField] float surface;

        readonly Dictionary<ICharacterDriver, int> inside = new Dictionary<ICharacterDriver, int>();
        readonly Dictionary<ICharacterDriver, float> splashedAt = new Dictionary<ICharacterDriver, float>();
        readonly List<ICharacterDriver> gone = new List<ICharacterDriver>();

        // For the builder: the surface's height over the box's middle.
        public void Configure(float surfaceOverMiddle) => surface = surfaceOverMiddle;

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        // A ragdoll is a dozen colliders: counted in and out, the first one in splashes.
        void OnTriggerEnter(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver == null) return;
            inside.TryGetValue(driver, out int count);
            inside[driver] = count + 1;
            if (count > 0) return;
            // Bobbing out and back in while it floats is not a new splash.
            if (splashedAt.TryGetValue(driver, out float last) && Time.time - last < 2f) return;
            splashedAt[driver] = Time.time;
            // R444: the whole velocity - a piece going in at a slant throws its splash forward.
            var body = other.attachedRigidbody;
            var velocity = body != null && !body.isKinematic ? body.linearVelocity : Vector3.zero;
            if (-velocity.y < 1f) velocity = new Vector3(velocity.x, -DefaultSpeed * Random.Range(.85f, 1.15f), velocity.z);
            var local = transform.InverseTransformPoint(other.bounds.center);
            var at = transform.TransformPoint(new Vector3(local.x, surface, local.z));
            GardenSplashFx.Play(at, velocity);
        }

        void OnTriggerExit(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver == null || !inside.TryGetValue(driver, out int count)) return;
            if (count <= 1) inside.Remove(driver);
            else inside[driver] = count - 1;
        }

        void FixedUpdate()
        {
            if (inside.Count == 0) return;
            gone.Clear();
            foreach (var driver in inside.Keys)
                if (driver is Object o && o == null) gone.Add(driver);
            foreach (var driver in gone)
            {
                inside.Remove(driver);
                splashedAt.Remove(driver);
            }
        }
    }
}
