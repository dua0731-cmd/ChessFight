using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A plank bridge that tips its riders off (목재 다리, Royal Carriage Yard; not on the map's main route yet - its
    // own prefab for later courses): it lies flat, then one edge rises on its hinge until the deck is steep, holds,
    // lowers again, waits, and again. Characters on it slide and tumble off by the deck's real turning, gravity and a
    // slippery deck while it is steep - nobody is moved by script. A small pull down the slope (slideAssist, the
    // existing ImportedObstacleActor.Accelerate, capped) stands in for the ragdoll's feet holding on better than wet
    // planks would. The pose is a pure function of ObstacleClock.
    //
    // The object's position is on the hinge line. LongSide (the default): the deck runs along local +z for `length`
    // and out to local +x for `width`; its +x edge rises and riders go off over the hinge edge at x 0. NearEnd: the
    // deck runs along +z from the hinge, `width` across x, and its far end rises (a drawbridge).
    public sealed class TiltingWoodenBridge : Obstacle
    {
        public enum Hinge { LongSide, NearEnd }

        [SerializeField] Hinge hinge = Hinge.LongSide;
        [SerializeField] float length = 8f;
        [SerializeField] float width = 3f;
        [Tooltip("Highest angle of the deck (degrees)")]
        [SerializeField] float raiseAngle = 40f;
        [Tooltip("Seconds from flat to raiseAngle")]
        [SerializeField] float raiseDuration = 1.4f;
        [Tooltip("Seconds it stays up")]
        [SerializeField] float holdDuration = .6f;
        [Tooltip("Seconds from raiseAngle back to flat")]
        [SerializeField] float lowerDuration = 2f;
        [Tooltip("Seconds it lies flat between tips")]
        [SerializeField] float waitingDuration = 3f;
        [Tooltip("Steeper than this (degrees) the deck turns slippery and pulls riders down it")]
        [SerializeField] float slipAngle = 12f;
        [Tooltip("Pull down the slope on riders while slippery (m/s², 0 = none)")]
        [SerializeField] float slideAssist = 6f;

        GameObject deck;
        Collider deckCollider;
        PhysicsMaterial slippery;
        bool built;
        float angle;
        readonly HashSet<Collider> riders = new HashSet<Collider>();
        readonly HashSet<ImportedObstacleActor> pulled = new HashSet<ImportedObstacleActor>();

        public float CycleSeconds => waitingDuration + raiseDuration + holdDuration + lowerDuration;
        // Average turning speed while it rises (degrees per second), the same thing raiseDuration sets.
        public float RotationSpeed => raiseAngle / Mathf.Max(.01f, raiseDuration);
        public float Angle => angle;

        public void Configure(Hinge hinge, float length, float width, float raiseAngle, float raiseDuration, float holdDuration,
                              float lowerDuration, float waitingDuration)
        {
            this.hinge = hinge;
            this.length = length;
            this.width = width;
            this.raiseAngle = raiseAngle;
            this.raiseDuration = raiseDuration;
            this.holdDuration = holdDuration;
            this.lowerDuration = lowerDuration;
            this.waitingDuration = waitingDuration;
        }

        protected override void Awake()
        {
            Build();
            base.Awake();
        }

        void Build()
        {
            if (built) return;
            built = true;
            var centre = hinge == Hinge.LongSide ? new Vector3(width * .5f, -.15f, length * .5f) : new Vector3(0f, -.15f, length * .5f);
            deck = YardKit.Box(transform, "Deck", centre, new Vector3(width, .3f, length), YardKit.WoodLight);
            deck.AddComponent<CourseFloor>();
            deckCollider = deck.GetComponent<Collider>();
            slippery = new PhysicsMaterial("Wet planks")
            {
                dynamicFriction = .04f,
                staticFriction = .04f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f
            };
            // Planks across the deck and the hinge beam: pictures on the one deck collider.
            int planks = Mathf.Max(1, Mathf.RoundToInt(length / .5f));
            for (int i = 0; i < planks; i++)
            {
                float z = (i + .5f) * length / planks;
                var plank = hinge == Hinge.LongSide
                    ? new Vector3(width * .5f, .01f, z)
                    : new Vector3(0f, .01f, z);
                YardKit.Box(transform, "Plank", plank, new Vector3(width - .04f, .04f, length / planks - .05f), i % 2 == 0 ? YardKit.WoodLight : YardKit.WoodDark, YardKit.Solid.None);
            }
            var beam = hinge == Hinge.LongSide ? new Vector3(0f, -.2f, length * .5f) : new Vector3(0f, -.2f, 0f);
            var beamAxis = hinge == Hinge.LongSide ? Vector3.forward : Vector3.right;
            float beamLength = hinge == Hinge.LongSide ? length + .3f : width + .3f;
            YardKit.Cylinder(transform, "Hinge beam", beam, beamAxis, .3f, beamLength, YardKit.Iron, YardKit.Solid.None);
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition;
            float a = AngleAt(time);
            // LongSide: +z turning lifts the +x edge. NearEnd: -x turning lifts the far (+z) end.
            rotation = StartRotation * (hinge == Hinge.LongSide ? Quaternion.AngleAxis(a, Vector3.forward) : Quaternion.AngleAxis(-a, Vector3.right));
        }

        float AngleAt(double time)
        {
            float cycle = CycleSeconds;
            if (cycle <= 0f) return 0f;
            float t = (float)(time % cycle);
            if (t < waitingDuration) return 0f;
            t -= waitingDuration;
            if (t < raiseDuration) return raiseAngle * Smooth(t / raiseDuration);
            t -= raiseDuration;
            if (t < holdDuration) return raiseAngle;
            t -= holdDuration;
            return raiseAngle * (1f - Smooth(t / Mathf.Max(.01f, lowerDuration)));
        }

        static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            angle = AngleAt(ObstacleClock.Now + phase);
            bool slip = angle > slipAngle;
            if (deckCollider != null) deckCollider.sharedMaterial = slip ? slippery : null;
            if (!slip || slideAssist <= 0f || !ObstacleClock.Authority)
            {
                riders.Clear();
                return;
            }
            // Down the deck's slope, along its surface.
            var up = deck.transform.up;
            var downhill = Vector3.ProjectOnPlane(Vector3.down, up).normalized;
            pulled.Clear();
            foreach (var c in riders)
            {
                var actor = ImportedObstacleActor.Find(c);
                if (actor == null || !pulled.Add(actor)) continue;
                actor.Accelerate(downhill * slideAssist, 4f, Time.fixedDeltaTime);
            }
            riders.Clear();
        }

        // The deck's collisions arrive here (it is this body's collider): whoever touches it this step.
        void OnCollisionStay(Collision c)
        {
            if (c.collider != null && c.collider.GetComponentInParent<ICharacterDriver>() != null) riders.Add(c.collider);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, .55f, .2f, .8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            var centre = hinge == Hinge.LongSide ? new Vector3(width * .5f, 0f, length * .5f) : new Vector3(0f, 0f, length * .5f);
            Gizmos.DrawWireCube(centre, new Vector3(width, .3f, length));
        }
    }
}
