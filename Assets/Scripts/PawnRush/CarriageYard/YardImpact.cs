using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A trigger shell round a moving part of the Royal Carriage Yard (a carriage, a waterwheel blade, a barrel) that
    // knocks a character away when the part runs into it: along the way the part moves where it touches, plus a little
    // straight out from the part, plus a lift. The push grows with the part's speed there - a carousel carriage hits
    // harder the faster it turns, a cart harder mid-charge than creeping back - and is capped so nothing is flung off
    // the map. A fast part also knocks the character down for a moment; a slow one only staggers it.
    //
    // Through the existing IHitReceiver (the ragdoll's TakeHit, which also lets go of whatever it holds), only on the
    // PC that simulates the character (YardKit.Movable). The part's own solid collider still pushes physically; this
    // makes the result a clear knock-back instead of a body ground against a moving box. Once per character per
    // `cooldown`, so a body lying against a part is not hit every physics step.
    [RequireComponent(typeof(Collider))]
    public sealed class YardImpact : MonoBehaviour
    {
        [Tooltip("Push (m/s) of a hit from a part that barely moves")]
        public float knockbackForce = 6f;
        [Tooltip("Extra push per m/s of the part's speed where it touches")]
        public float speedScale = .6f;
        [Tooltip("Upward part of the push (m/s)")]
        public float lift = 2.5f;
        [Tooltip("The push never goes over this (m/s), whatever the speed")]
        public float maxPush = 13f;
        [Tooltip("Parts slower than this (m/s) where they touch do not hit: a parked cart is a wall")]
        public float minSpeed = .4f;
        [Tooltip("At this speed (m/s) or faster the hit also knocks the character down")]
        public float knockdownSpeed = 3f;
        [Tooltip("How long a knockdown keeps the character down (s)")]
        public float knockdownSeconds = .9f;
        [Tooltip("Seconds before the same character can be hit by this part again")]
        public float cooldown = .7f;

        IMovingSurface surface;
        Collider shell;
        readonly Dictionary<ICharacterDriver, float> lastHit = new Dictionary<ICharacterDriver, float>();

        public int Hits { get; private set; }

        void Awake()
        {
            shell = GetComponent<Collider>();
            shell.isTrigger = true;
            surface = GetComponentInParent<IMovingSurface>();
        }

        void OnDisable() => lastHit.Clear();

        void OnTriggerStay(Collider other)
        {
            if (surface == null || !YardKit.Movable(other, out var driver, out var hits)) return;
            float now = Time.time;
            if (lastHit.TryGetValue(driver, out float before) && now - before < cooldown) return;
            var target = driver.FollowTarget != null ? driver.FollowTarget.position : other.transform.position;
            var velocity = surface.PointVelocity(target);
            float speed = velocity.magnitude;
            if (speed < minSpeed) return;

            // Along the part's motion, bent a little straight out from it so a grazing hit still clears the body.
            var along = Vector3.ProjectOnPlane(velocity, Vector3.up);
            var outward = Vector3.ProjectOnPlane(target - shell.bounds.center, Vector3.up);
            if (outward.sqrMagnitude > 1e-4f) outward.Normalize();
            var direction = along.sqrMagnitude > 1e-4f ? along.normalized * .75f + outward * .35f : outward;
            if (direction.sqrMagnitude < 1e-4f) direction = transform.forward;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;

            float push = Mathf.Min(maxPush, knockbackForce + speed * speedScale);
            hits.ApplyHit(direction * push + Vector3.up * lift, speed >= knockdownSpeed ? knockdownSeconds : 0f, 0f, true);
            lastHit[driver] = now;
            Hits++;
            if (lastHit.Count > 64) Forget(now);
        }

        void Forget(float now)
        {
            var old = new List<ICharacterDriver>();
            foreach (var pair in lastHit)
                if (now - pair.Value > cooldown || (pair.Key is Object o && o == null)) old.Add(pair.Key);
            foreach (var key in old) lastHit.Remove(key);
        }
    }
}
