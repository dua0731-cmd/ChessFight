using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // One leaf of a gate that opens by itself when the race starts (the Royal Carriage Yard's outer gate): closed,
    // then swung open over openDuration, then open for good. The race start is a clock time: offline it is openDelay
    // seconds after the leaf is switched on; on a shared match clock the leaf stays shut until the match hands every PC
    // the same start (OpenAt), and then all of them see the gate open together, because the pose is a pure function of
    // ObstacleClock like any obstacle's. A new start moves the leaf at once (a cut) instead of sweeping it.
    //
    // The object's position is the hinge. The leaf runs `width` from it along local +x (or -x with extendsLeft) and
    // swings to local +z, away from the players waiting behind it.
    public sealed class TimedGate : Obstacle
    {
        [SerializeField] float width = 3f;
        [SerializeField] float height = 5.4f;
        [SerializeField] float thickness = .35f;
        [Tooltip("The leaf runs to local -x from its hinge (the right-hand leaf of a pair)")]
        [SerializeField] bool extendsLeft;
        [Tooltip("Degrees it swings open")]
        [SerializeField] float openAngle = 90f;
        [Tooltip("Seconds after the gate is switched on that it starts to open (offline)")]
        [SerializeField] float openDelay = 3f;
        [Tooltip("Seconds the swing takes")]
        [SerializeField] float openDuration = 2.5f;
        [Tooltip("Shows the seconds left over the gate (optional)")]
        [SerializeField] TextMesh countdown;

        double openAt = double.NaN, evaluated = double.NaN, previous = double.NaN;   // the starts of the last two poses
        bool built;

        // Seconds until the gate starts to open (0 once it has).
        public float SecondsToOpen => double.IsNaN(openAt) ? 0f : Mathf.Max(0f, (float)(openAt - (ObstacleClock.Now + phase)));

        public void Configure(float width, float height, bool extendsLeft, float openDelay, float openDuration, TextMesh countdown = null)
        {
            this.width = width;
            this.height = height;
            this.extendsLeft = extendsLeft;
            this.openDelay = openDelay;
            this.openDuration = openDuration;
            this.countdown = countdown;
        }

        // The shared start of a match (a clock time): the gate starts to open then.
        public void OpenAt(double clockTime) => openAt = clockTime + phase;

        protected override void Awake()
        {
            if (double.IsNaN(openAt) && !ObstacleClock.Shared) openAt = ObstacleClock.Now + phase + openDelay;
            Build();
            base.Awake();
        }

        protected override bool JumpsBetween(double from, double to) => !Same(previous, evaluated);

        static bool Same(double a, double b) => a == b || (double.IsNaN(a) && double.IsNaN(b));

        void Build()
        {
            if (built) return;
            built = true;
            float side = extendsLeft ? -1f : 1f;
            YardKit.Box(transform, "Leaf", new Vector3(side * width * .5f, height * .5f, 0f), new Vector3(width, height, thickness), YardKit.WoodDark, YardKit.Solid.NoClimb);
            for (int i = 0; i < 3; i++)
                YardKit.Box(transform, "Iron band", new Vector3(side * width * .5f, height * (.18f + i * .32f), -thickness * .5f - .03f),
                            new Vector3(width - .1f, .16f, .06f), YardKit.Iron, YardKit.Solid.None);
            YardKit.Box(transform, "Crown plate", new Vector3(side * width * .55f, height * .62f, -thickness * .5f - .06f), new Vector3(.9f, .9f, .05f),
                        YardKit.Gold, YardKit.Solid.None);
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            previous = evaluated;
            evaluated = openAt;
            position = StartPosition;
            float u = double.IsNaN(openAt) ? 0f : Mathf.Clamp01((float)((time - openAt) / Mathf.Max(.01f, openDuration)));
            float swing = openAngle * u * u * (3f - 2f * u);
            // A leaf running to +x swings its free edge to +z by turning negatively about y; the other one positively.
            rotation = StartRotation * Quaternion.AngleAxis(extendsLeft ? swing : -swing, Vector3.up);
        }

        void Update()
        {
            if (countdown == null) return;
            float left = SecondsToOpen;
            string text = left > 0f ? Mathf.CeilToInt(left).ToString() : "";
            if (countdown.text != text) countdown.text = text;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, .84f, .2f, .8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            float side = extendsLeft ? -1f : 1f;
            Gizmos.DrawWireCube(new Vector3(side * width * .5f, height * .5f, 0f), new Vector3(width, height, thickness));
        }
    }
}
