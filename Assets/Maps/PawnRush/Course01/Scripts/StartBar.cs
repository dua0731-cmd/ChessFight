using UnityEngine;

namespace ChessFight.PawnRush
{
    // The start line's bar (design doc M0): it holds everyone in the start square until
    // the countdown reaches 0, then sinks under the floor. Offline the countdown starts
    // when the scene does; a match will call Release at its shared start time instead.
    // StateDrivenMover: dropped by the countdown, not by ObstacleClock. Offline only.
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

        public static StartBar Current { get; private set; }

        // Seconds until the bar drops; 0 once it has; negative when nothing has started it.
        public float Remaining => releaseAt == float.MaxValue ? -1f : Mathf.Max(0f, releaseAt - Time.time);

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
            float t = Mathf.Clamp01((Time.time - releaseAt) / dropSeconds);
            body.MovePosition(up - Vector3.up * (drop * t));
        }
    }
}
