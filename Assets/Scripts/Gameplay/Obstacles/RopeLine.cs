using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A rope, a chain or a pendulum swing (Queen of the Hill M6), hanging from this
    // object's position: the swing of S4, the chains of S2, S6 and S8, the clock
    // pendulum of S5. A character takes hold of it with the grab button and hangs
    // on, climbs it with W/S, and lets go (Space, or the button) with the speed of
    // the rope where its hands were.
    //
    // Like every Obstacle its pose is a pure function of the shared clock (G2): the
    // line swings about its local X axis, swingDegrees * sin(2 pi (t + phase) / period)
    // toward local forward, so every PC sees it in the same place without a packet.
    // 0 degrees is a still rope or chain to climb. The line itself has no collider:
    // it never pushes anyone; characters find it through All.
    public sealed class RopeLine : MonoBehaviour
    {
        [Tooltip("From the top (this object) to the bottom end, metres.")]
        [SerializeField] float length = 6f;
        [Tooltip("How far it swings each way, degrees. 0: it hangs still.")]
        [SerializeField] float swingDegrees;
        [Tooltip("Seconds for a whole swing, there and back.")]
        [SerializeField] float period = 5f;
        [Tooltip("Seconds added to the shared clock, so swings side by side are out of step.")]
        [SerializeField] float phase;
        [Tooltip("Drawn as a cylinder this thick along the line. 0: draw nothing (the scene has its own art).")]
        [SerializeField] float thickness = 0.08f;
        [SerializeField] Material material;

        static readonly List<RopeLine> active = new List<RopeLine>();
        Transform drawn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        public static IReadOnlyList<RopeLine> All => active;

        public float Length => length;
        public float SwingDegrees => swingDegrees;
        public float Period => period;
        public Vector3 Top => transform.position;

        // For ropes built from code (test beds).
        public void Configure(float length, float swingDegrees = 0f, float period = 5f, float phase = 0f,
                              float thickness = 0.08f, Material material = null)
        {
            this.length = Mathf.Max(0.1f, length);
            this.swingDegrees = swingDegrees;
            this.period = period;
            this.phase = phase;
            this.thickness = thickness;
            this.material = material;
            if (drawn != null) Destroy(drawn.gameObject);
            drawn = null;
            Draw(ObstacleClock.Now);
        }

        bool Swings => Mathf.Abs(swingDegrees) > 1e-3f && period > 1e-3f;

        double Cycle(double time) => 2.0 * Math.PI * (time + phase) / period;

        // Radians off straight down at `time`, positive toward local forward.
        public float Angle(double time) => Swings ? swingDegrees * Mathf.Deg2Rad * (float)Math.Sin(Cycle(time)) : 0f;

        float AngleRate(double time) =>
            Swings ? swingDegrees * Mathf.Deg2Rad * (float)(2.0 * Math.PI / period * Math.Cos(Cycle(time))) : 0f;

        // Down the line, a unit vector.
        public Vector3 Direction(double time)
        {
            float a = Angle(time);
            return transform.rotation * new Vector3(0f, -Mathf.Cos(a), Mathf.Sin(a));
        }

        // The point `s` metres down the line.
        public Vector3 Point(float s, double time) => Top + Direction(time) * Mathf.Clamp(s, 0f, length);

        // How fast that point is moving.
        public Vector3 PointVelocity(float s, double time)
        {
            float a = Angle(time);
            return transform.rotation * new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * (Mathf.Clamp(s, 0f, length) * AngleRate(time));
        }

        // How far `point` is from the line, and how far down the line (0..length) its nearest point is.
        public float Distance(Vector3 point, double time, out float s)
        {
            Vector3 down = Direction(time);
            s = Mathf.Clamp(Vector3.Dot(point - Top, down), 0f, length);
            return Vector3.Distance(point, Top + down * s);
        }

        // Seconds until it is next at its far end (swung furthest toward local forward), or at the near end.
        public float SecondsToEnd(bool far, double time)
        {
            if (!Swings) return 0f;
            double quarter = period * (far ? 0.25 : 0.75);
            double t = (time + phase - quarter) / period;
            return (float)((Math.Ceiling(t) - t) * period);
        }

        void OnEnable()
        {
            active.Add(this);
            Draw(ObstacleClock.Now);
        }

        void OnDisable() => active.Remove(this);

        void LateUpdate() => Draw(ObstacleClock.Now);

        void Draw(double time)
        {
            if (thickness <= 0f) return;
            if (drawn == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "Line";
                var c = go.GetComponent<Collider>();
                if (c != null) DestroyImmediate(c);   // not even for a frame: it would push whoever it met
                if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
                drawn = go.transform;
                drawn.SetParent(transform, false);
            }
            Vector3 down = Direction(time);
            // The primitive is 2 m tall along its own up: half the length, centred halfway down.
            drawn.SetPositionAndRotation(Top + down * (length * 0.5f), Quaternion.FromToRotation(Vector3.up, -down));
            Vector3 parent = transform.lossyScale;
            drawn.localScale = new Vector3(thickness / Mathf.Max(1e-4f, parent.x), length * 0.5f / Mathf.Max(1e-4f, parent.y),
                thickness / Mathf.Max(1e-4f, parent.z));
        }
    }
}
