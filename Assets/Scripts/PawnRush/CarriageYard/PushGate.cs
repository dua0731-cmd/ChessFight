using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // StateDrivenMover: the Royal Carriage Yard's final gate (최종 성문) opens as far as characters have pushed it, not
    // by the clock, so it is not an Obstacle and, like the other state-driven movers, it is offline only until the
    // host sends its state (HANDOFF absolute rule 7). Only the PC that judges (offline, or a match's host:
    // ObstacleClock.Authority) counts pushers; another PC shows what it is handed (SetProgress).
    //
    // Two heavy leaves that open away from the platform (local +z). A character opens them by holding the right mouse
    // button on a leaf - the ragdoll's own grab, a hand jointed to the leaf - from the platform side, close to it.
    // While at least one character holds on like that the leaves swing open, faster the more of them push
    // (crowdBonus); let go and they stop where they are. The leaves really turn, and the hands on them are carried
    // along, so the pushers walk the gate open. Heavy: the swing starts slowly and ends softly (smoothstep), so a pawn
    // fits through the middle only at about a third of the bar, ~3.5 s of one pusher, ~1.6 s of three (R287: the old
    // ease-out swing opened a pawn-wide slot after ~0.45 s of one pusher), and the leaves never stop dead at speed with
    // hands on them. However many push, the gate goes at most maxCrowd times one pusher's speed: the leaves' rings stay
    // under a run (~2.5 m/s). Fully open they stay open for everyone, for good. A bar over the gate shows how far it is.
    public sealed class PushGate : MonoBehaviour
    {
        [Tooltip("Width of the opening, both leaves together (m)")]
        [SerializeField] float openingWidth = 8f;
        [SerializeField] float height = 7f;
        [SerializeField] float thickness = .5f;
        [Tooltip("Degrees each leaf swings open")]
        [SerializeField] float openAngle = 100f;
        [Tooltip("Seconds of one character pushing to open it all the way (a pawn fits through after about a third of them)")]
        [SerializeField] float pushSeconds = 10.5f;
        [Tooltip("Each extra character pushing adds this much speed (0.6 = 60 %)")]
        [SerializeField] float crowdBonus = .6f;
        [Tooltip("However many push, at most this many times one pusher's speed")]
        [SerializeField] float maxCrowd = 3f;
        [Tooltip("How far a pusher's body may be from the gate (m)")]
        [SerializeField] float interactDistance = 1.8f;
        [Tooltip("How far the wall round the gate stands out on the platform side (m): the bar and its sign go in front of it")]
        [SerializeField] float frontDepth = .5f;

        Rigidbody left, right;
        Transform fill;
        float progress;
        bool built;

        public float Progress => progress;
        public bool IsOpen => progress >= 1f;
        public int Pushers { get; private set; }

        // Degrees each leaf stands open now: slow at first, soft at the end.
        public float Swing => openAngle * progress * progress * (3f - 2f * progress);

        // The judging PC's progress, on a PC that does not judge the gate (a match's client, once the host sends it).
        // It never closes.
        public void SetProgress(float value) => progress = Mathf.Max(progress, Mathf.Clamp01(value));

        public void Configure(float openingWidth, float height, float pushSeconds, float crowdBonus, float frontDepth = .5f)
        {
            this.openingWidth = openingWidth;
            this.height = height;
            this.pushSeconds = pushSeconds;
            this.crowdBonus = crowdBonus;
            this.frontDepth = frontDepth;
        }

        void Awake()
        {
            if (built) return;
            built = true;
            left = Leaf("Left leaf", -1f);
            right = Leaf("Right leaf", 1f);
            // The progress bar over the opening: a dark frame and a gold fill growing from the left, on the face of the
            // wall over the gate (R287: inside the map's 2 m wall it was never seen).
            float bar = openingWidth * .6f, front = Mathf.Max(thickness, frontDepth) + .06f;
            YardKit.Box(transform, "Bar frame", new Vector3(0f, height + .9f, -front), new Vector3(bar + .2f, .5f, .1f), YardKit.Iron, YardKit.Solid.None);
            var pivot = new GameObject("Bar fill").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(-bar * .5f, height + .9f, -front - .07f);
            YardKit.Box(pivot, "Fill", new Vector3(.5f, 0f, 0f), new Vector3(1f, .3f, .05f), YardKit.Gold, YardKit.Solid.None);
            pivot.localScale = new Vector3(.0001f, 1f, 1f);
            fill = pivot;
            // Read from the platform (looking +z), like every sign on the course.
            YardKit.Label(transform, "우클릭을 누른 채로 밀기", new Vector3(0f, height + 1.7f, -front), 1.1f, new Color(1f, .9f, .6f));
        }

        // A leaf hinged at the outer edge of the opening (side -1 left, +1 right), running to the middle.
        Rigidbody Leaf(string name, float side)
        {
            float w = openingWidth * .5f;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(side * w, 0f, 0f);
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var t = go.transform;
            // Grab-only: held and carried, never climbed.
            YardKit.Box(t, "Door", new Vector3(-side * w * .5f, height * .5f, 0f), new Vector3(w, height, thickness), YardKit.WoodDark, YardKit.Solid.GrabOnly);
            for (int i = 0; i < 4; i++)
                YardKit.Box(t, "Iron band", new Vector3(-side * w * .5f, height * (.12f + i * .25f), -thickness * .5f - .03f),
                            new Vector3(w - .1f, .18f, .06f), YardKit.Iron, YardKit.Solid.None);
            YardKit.Box(t, "Crown", new Vector3(-side * w * .55f, height * .6f, -thickness * .5f - .06f), new Vector3(1.2f, 1.2f, .05f), YardKit.Gold, YardKit.Solid.None);
            // The ring a hand goes for, at a pawn's chest height near the middle.
            YardKit.Cylinder(t, "Ring", new Vector3(-side * (w - .7f), .95f, -thickness * .5f - .12f), Vector3.forward, .55f, .1f, YardKit.Gold, YardKit.Solid.GrabOnly);
            return body;
        }

        void FixedUpdate()
        {
            if (left == null || right == null) return;
            if (progress < 1f && ObstacleClock.Authority)
            {
                Pushers = 0;
                foreach (var driver in YardKit.CharactersNear(transform.position + Vector3.up * 1.5f, openingWidth * .5f + interactDistance + 1f))
                    if (Pushes(driver)) Pushers++;
                if (Pushers > 0)
                    progress = Mathf.Min(1f, progress + Time.fixedDeltaTime / Mathf.Max(.1f, pushSeconds)
                                                        * Mathf.Min(Mathf.Max(1f, maxCrowd), 1f + (Pushers - 1) * crowdBonus));
            }
            float swing = Swing;
            left.MoveRotation(transform.rotation * Quaternion.AngleAxis(-swing, Vector3.up));
            right.MoveRotation(transform.rotation * Quaternion.AngleAxis(swing, Vector3.up));
            if (fill != null) fill.localScale = new Vector3(Mathf.Max(.0001f, progress * openingWidth * .6f), 1f, 1f);
        }

        // Holding a leaf, from the platform side, close enough.
        bool Pushes(ICharacterDriver driver)
        {
            if (driver.FollowTarget == null) return false;
            if (YardKit.GripOn(driver, left) == null && YardKit.GripOn(driver, right) == null) return false;
            var local = transform.InverseTransformPoint(driver.FollowTarget.position);
            if (Mathf.Abs(local.x) > openingWidth * .5f + .5f) return false;
            // In front of the closed leaves, or following them as they open.
            return local.z < interactDistance + openingWidth * .5f * Mathf.Sin(Mathf.Min(90f, Swing) * Mathf.Deg2Rad) && local.z > -interactDistance - thickness;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, .84f, .2f, .8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, height * .5f, 0f), new Vector3(openingWidth, height, thickness));
            Gizmos.DrawWireCube(new Vector3(0f, 1f, -interactDistance * .5f), new Vector3(openingWidth, 2f, interactDistance));
        }
    }
}
