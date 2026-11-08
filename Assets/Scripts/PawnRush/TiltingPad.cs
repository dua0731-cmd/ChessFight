using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A lotus pad on the royal water garden's shortcut (v31): a pawn that stands on it for half a second tips it 40°
    // sideways - whoever is still on it is shoved off into the water - and it rights itself 1.2 s later. Jump on and
    // off again at once, and it never tips.
    //
    // StateDrivenMover: it tips because someone stands on it, not on the shared clock. The authority (offline, or the
    // host) decides; online (R508) the host sends when it tipped in the course state (ISectionNetPart, as the grand stair
    // hall's glass stones: a byte, 0 at rest, else how long ago it tipped in fiftieths of a second).
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TiltingPad : MonoBehaviour, ISectionNetPart
    {
        [Tooltip("Local axis it tips about (the shortcut's way along, so it tips to the side).")]
        [SerializeField] Vector3 axis = Vector3.forward;
        [SerializeField] float radius = .8f;
        [SerializeField] float delay = .5f, tilt = 40f, tipping = .15f, hold = 1.2f, righting = .6f;
        [SerializeField] float shove = 3.5f;

        readonly Collider[] overlaps = new Collider[32];
        readonly HashSet<ImportedObstacleActor> riders = new HashSet<ImportedObstacleActor>();
        Rigidbody body;
        Quaternion rest;
        float standing, tippedAt = -1f;

        public bool Tipped => tippedAt >= 0f;

        // For the builder: on an inactive object, before it wakes.
        public void Configure(Vector3 localAxis, float radius)
        {
            axis = localAxis;
            this.radius = radius;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            rest = transform.rotation;
        }

        void FixedUpdate()
        {
            float now = Time.time;
            if (tippedAt >= 0f)
            {
                float age = now - tippedAt;
                if (age >= tipping + hold + righting) { tippedAt = -1f; standing = 0f; }
                float a = age < tipping ? age / tipping : age < tipping + hold ? 1f : 1f - Mathf.Clamp01((age - tipping - hold) / righting);
                body.MoveRotation(rest * Quaternion.AngleAxis(tilt * Mathf.SmoothStep(0f, 1f, a), axis));
                return;
            }
            body.MoveRotation(rest);
            if (!ObstacleClock.Authority) return;
            Riders();
            standing = riders.Count > 0 ? standing + Time.fixedDeltaTime : 0f;
            if (standing < delay) return;
            tippedAt = now;
            // Off to the side it tips down to.
            var down = rest * Vector3.Cross(axis.normalized, Vector3.up);
            foreach (var actor in riders)
            {
                actor.AddObstacleImpulse(down * shove + Vector3.up);
                GardenHits.Report(actor.gameObject, "연꽃 발판");
            }
        }

        // ------------------------------------------------------------------ online (R508)

        public int NetBytes => 1;

        public void WriteNet(ref ChessFight.Network.NetBlock block)
        {
            block.U8(tippedAt < 0f ? 0 : Mathf.Clamp(1 + Mathf.FloorToInt((Time.time - tippedAt) / .02f), 1, 254));
        }

        // Not the host: the host's tip, counted from when it tipped there. A tip that has run its course here ends by
        // itself, as the host's does.
        public void ReadNet(ref ChessFight.Network.NetBlock block)
        {
            int v = block.ReadU8();
            if (v == 0) return;
            float at = Time.time - (v - 1) * .02f;
            if (tippedAt < 0f || Mathf.Abs(tippedAt - at) > .15f) tippedAt = at;
        }

        // Pawns standing just over the pad.
        void Riders()
        {
            riders.Clear();
            var top = transform.position;
            int n = Physics.OverlapBoxNonAlloc(top + Vector3.up * .5f, new Vector3(radius, .5f, radius), overlaps, rest, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor != null && actor.CanReceiveObstacle && actor.Grounded) riders.Add(actor);
            }
        }
    }
}
