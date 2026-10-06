using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // MG_Placeholder (design doc §6): a 3 m button in the middle of the slot. Every
    // pawn standing on it adds 0.1 a second; at 1 the game is done and the slot's door
    // opens. Alone that is 10 s, three pawns a little over 3 s.
    public sealed class MiniGamePlaceholder : MiniGameBase
    {
        [SerializeField] float radius = 1.5f;
        [Tooltip("Progress per pawn on the button, per second.")]
        [SerializeField] float ratePerPawn = .1f;
        [Tooltip("The button top, moved down a little while someone stands on it.")]
        [SerializeField] Transform cap;

        readonly Collider[] buffer = new Collider[64];
        readonly HashSet<ICharacterDriver> on = new HashSet<ICharacterDriver>();
        Vector3 capRest;

        public int PawnsOn => on.Count;

        public void Configure(Transform cap, float radius)
        {
            this.cap = cap;
            this.radius = radius;
        }

        void Awake()
        {
            if (cap != null) capRest = cap.localPosition;
        }

        void FixedUpdate()
        {
            on.Clear();
            var center = transform.position + Vector3.up * .9f;
            int n = Physics.OverlapCapsuleNonAlloc(center, center + Vector3.up * .8f, radius, buffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var driver = buffer[i].GetComponentInParent<ICharacterDriver>();
                if (driver == null || driver.FollowTarget == null) continue;
                var offset = driver.FollowTarget.position - transform.position;
                offset.y = 0f;
                if (offset.magnitude <= radius) on.Add(driver);
            }
            if (!Completed && on.Count > 0)
            {
                Progress01 = Mathf.Min(1f, Progress01 + on.Count * ratePerPawn * Time.fixedDeltaTime);
                if (Progress01 >= 1f) Complete();
            }
            if (cap != null) cap.localPosition = capRest - Vector3.up * (on.Count > 0 || Completed ? .12f : 0f);
        }
    }
}
