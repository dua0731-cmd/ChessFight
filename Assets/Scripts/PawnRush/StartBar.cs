using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The start line's bar (design doc M0): it holds everyone in the start square until
    // the countdown reaches 0, then sinks under the floor. Offline the countdown starts
    // when the scene does. Online (R126) the match link calls ReleaseAtClock with the match's
    // shared start: the bar is then a function of the obstacle clock, so it sinks at the same
    // moment on the host (whose physics holds the pawns) and on every client (which draws it
    // at the host's obstacle time). Before R126 every PC counted down from its own scene load,
    // so a client could see the bar gone while the host's still held everyone, or the reverse.
    // StateDrivenMover: offline it is dropped by the countdown; online by the obstacle clock.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class StartBar : MonoBehaviour
    {
        [SerializeField] float countdown = 3f;
        [SerializeField] float drop = 1.4f;
        [SerializeField] float dropSeconds = .35f;
        [Tooltip("Off: wait for Release().")]
        [SerializeField] bool startOnLoad = true;

        Rigidbody body;
        Vector3 up;
        float releaseAt = float.MaxValue;
        double releaseClock = double.NaN;   // online: the obstacle clock time it drops at

        public static StartBar Current { get; private set; }

        // Seconds until the bar drops; 0 once it has; negative when nothing has started it.
        public float Remaining => !double.IsNaN(releaseClock) ? Mathf.Max(0f, (float)(releaseClock - ObstacleClock.Now))
            : releaseAt == float.MaxValue ? -1f : Mathf.Max(0f, releaseAt - Time.time);

        // Online: drop at this obstacle-clock time (the match's shared start plus the countdown).
        public void ReleaseAtClock(double clockTime) => releaseClock = clockTime;

        public float Countdown => countdown;

        void Awake()
        {
            Current = this;
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            up = transform.position;
        }

        void Start()
        {
            if (startOnLoad) releaseAt = Time.time + countdown;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void Release(float inSeconds = 0f) => releaseAt = Time.time + inSeconds;

        // Back up for a new run (the playtest's restart counts down again).
        public void Rearm()
        {
            releaseAt = Time.time + countdown;
            body.position = up;
        }

        void FixedUpdate()
        {
            float t = !double.IsNaN(releaseClock) ? Mathf.Clamp01((float)(ObstacleClock.Now - releaseClock) / dropSeconds)
                    : Mathf.Clamp01((Time.time - releaseAt) / dropSeconds);
            body.MovePosition(up - Vector3.up * (drop * t));
        }
    }
}
