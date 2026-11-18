using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's focus beam (R720, obstacle plan v0.1 #8): the great telescope's objective gathers the
    // light that falls through the dome's slit into a hot spot on the tube's deck, and the spot wanders over it -
    // across the deck with one period, along it with another, so it does not come back to the same place. A pawn
    // that stays in the spot longer than `dwell` is thrown back down the deck.
    //
    // This object lies on the deck: its +z goes up along it, +x across it, +y off it. Where the spot is, is a pure
    // function of the shared obstacle clock (the same on every PC, nothing sent); who it throws is the authority's
    // to say (ObstacleClock.Authority), as with every hazard.
    public sealed class FocusBeam : MonoBehaviour
    {
        [SerializeField] float radius = 1.3f;
        [Tooltip("How far the spot's middle goes either side of this object, across the deck and along it.")]
        [SerializeField] float across = 1.9f, along = 2f;
        [SerializeField] float acrossPeriod = 3.1f, alongPeriod = 4.7f;
        [Tooltip("Seconds added to the shared clock, so two beams do not move alike.")]
        [SerializeField] float phase;
        [Tooltip("Where the light comes from (the objective), in this object's frame.")]
        [SerializeField] Vector3 lens = new Vector3(0f, 3f, 14f);

        [Header("Hits")]
        [Tooltip("Seconds a pawn may stay in the spot.")]
        [SerializeField] float dwell = .25f;
        [Tooltip("Thrown back down the deck at this speed, and up at `lift`.")]
        [SerializeField] float launch = 6f, lift = 3.5f, knockdown = .8f;
        [Tooltip("Seconds before the same pawn can be thrown again.")]
        [SerializeField] float rest = 1f;

        Transform spot, shaft;
        Dictionary<ImportedObstacleActor, float> held = new Dictionary<ImportedObstacleActor, float>();
        Dictionary<ImportedObstacleActor, float> next = new Dictionary<ImportedObstacleActor, float>();
        readonly Dictionary<ImportedObstacleActor, double> resting = new Dictionary<ImportedObstacleActor, double>();
        readonly Collider[] overlaps = new Collider[64];

        public float Radius => radius;
        // How many pawns it has thrown (the authority's count).
        public int Thrown { get; private set; }

        // Call it on an inactive object (the builder's root is switched on at the end).
        public void Configure(float radius, float across, float along, float acrossPeriod, float alongPeriod, float phaseSeconds, Vector3 lens,
                              Material spotMaterial, Material shaftMaterial)
        {
            this.radius = radius;
            this.across = across;
            this.along = along;
            this.acrossPeriod = Mathf.Max(.5f, acrossPeriod);
            this.alongPeriod = Mathf.Max(.5f, alongPeriod);
            phase = phaseSeconds;
            this.lens = lens;
            spot = Shape("Hot spot", spotMaterial);
            shaft = Shape("Light shaft", shaftMaterial);
            Place(ObstacleClock.Now);
        }

        // The spot's middle at a time, in this object's frame.
        public Vector3 SpotAt(double time)
        {
            double t = time + phase;
            return new Vector3(across * (float)Math.Sin(2.0 * Math.PI * (t / acrossPeriod % 1.0)), 0f,
                               along * (float)Math.Sin(2.0 * Math.PI * (t / alongPeriod % 1.0)));
        }

        void Update() => Place(ObstacleClock.Now);

        void Place(double now)
        {
            var at = SpotAt(now);
            if (spot != null)
            {
                spot.localPosition = at + Vector3.up * .03f;
                spot.localScale = new Vector3(radius * 2f, .008f, radius * 2f);
            }
            if (shaft != null)
            {
                // A cone would be truer; a cylinder from the lens to the spot reads the same in a graybox.
                var reach = lens - at;
                float length = reach.magnitude;
                shaft.localPosition = at + reach * .5f;
                shaft.localRotation = length > .01f ? Quaternion.FromToRotation(Vector3.up, reach / length) : Quaternion.identity;
                shaft.localScale = new Vector3(radius * 1.2f, length * .5f, radius * 1.2f);
            }
        }

        void FixedUpdate()
        {
            if (!ObstacleClock.Authority)
            {
                held.Clear();
                return;
            }
            double now = ObstacleClock.Now;
            var at = SpotAt(now);
            var center = transform.TransformPoint(at);
            int n = Physics.OverlapSphereNonAlloc(center + transform.up, radius + 1f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            next.Clear();
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || next.ContainsKey(actor)) continue;
                var offset = transform.InverseTransformPoint(actor.Position) - at;
                // On the deck (not under it, not jumping far over it) and inside the spot.
                if (offset.y < -.6f || offset.y > 2.6f) continue;
                offset.y = 0f;
                if (offset.magnitude > radius) continue;
                if (resting.TryGetValue(actor, out double until) && now < until) continue;
                float stayed = (held.TryGetValue(actor, out float before) ? before : 0f) + Time.fixedDeltaTime;
                if (stayed >= dwell)
                {
                    actor.AddObstacleHit(transform.TransformDirection(Vector3.back) * launch + Vector3.up * lift, knockdown);
                    resting[actor] = now + rest;
                    Thrown++;
                    stayed = 0f;
                }
                next[actor] = stayed;
            }
            // Who was not in the spot this step starts again from nothing.
            var swap = held;
            held = next;
            next = swap;
            if (resting.Count > 64) resting.Clear();
        }

        Transform Shape(string name, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void OnDisable()
        {
            held.Clear();
            next.Clear();
            resting.Clear();
        }
    }
}
