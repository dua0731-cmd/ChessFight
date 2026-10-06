using UnityEngine;

namespace ChessFight.PawnRush
{
    // The door of a mini-game slot, at the exit height (design doc §5): opened once by
    // the game's done signal and never closed again in the round.
    // StateDrivenMover: opened by the mini-game result, not by ObstacleClock. Offline only
    // until the host sends the slot's state.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SlotDoor : MonoBehaviour
    {
        [SerializeField] float rise = 3.7f;
        [SerializeField] float seconds = 1.2f;

        Rigidbody body;
        Vector3 closed;
        float openedAt = -1f;

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

        // A new round shuts it again.
        public void Close()
        {
            openedAt = -1f;
            if (body != null) body.position = closed;
        }

        void FixedUpdate()
        {
            if (openedAt < 0f) return;
            float t = Mathf.SmoothStep(0f, 1f, (Time.time - openedAt) / seconds);
            body.MovePosition(closed + Vector3.up * (rise * t));
        }
    }
}
