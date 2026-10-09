using UnityEngine;

namespace ChessFight.PawnRush
{
    // A mission station's team gate (design doc v0.4 §5): a door that opens on its own team's
    // game-done signal and never shuts again in the round, and a team barrier in the doorway that
    // lets only that team through even when it is open - the other team cannot follow out of a
    // gate it did not open. The door is also D's portcullis: SetLift raises it a little with
    // the capstan's progress before it opens for good.
    // StateDrivenMover: moved by the mini-game's result, not by the shared obstacle clock. Offline only until
    // the host sends the station's state.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TeamGate : MonoBehaviour
    {
        [SerializeField] float rise = 4.6f;
        [SerializeField] float seconds = 1.2f;

        Rigidbody body;
        Vector3 closed;
        float lift, openedAt = -1f;

        public bool IsOpen => openedAt >= 0f;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            closed = transform.position;
        }

        public void Open()
        {
            if (openedAt < 0f) openedAt = Time.time;
        }

        // Before it opens: how far up it stands (D's portcullis), metres.
        public void SetLift(float metres)
        {
            if (openedAt < 0f) lift = Mathf.Clamp(metres, 0f, rise);
        }

        // A new round: shut, down.
        public void Close()
        {
            openedAt = -1f;
            lift = 0f;
            if (body != null) body.position = closed;
        }

        void FixedUpdate()
        {
            float up = openedAt < 0f ? lift : Mathf.Lerp(lift, rise, Mathf.SmoothStep(0f, 1f, (Time.time - openedAt) / seconds));
            body.MovePosition(closed + Vector3.up * up);
        }
    }
}
