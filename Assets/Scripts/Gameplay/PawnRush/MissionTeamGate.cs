using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay.PawnRush
{
    // Keep the barrier solid globally. Only the completed team ignores it.
    // Existing ragdoll raycasts already honor Physics.GetIgnoreCollision.
    public sealed class MissionTeamGate : MonoBehaviour
    {
        public TeamMission mission;
        public int allowedTeam = Teams.White;
        public Collider barrier;
        public Transform doorVisual;
        public float liftHeight = 5;
        readonly Dictionary<Collider, bool> previousIgnore = new Dictionary<Collider, bool>();
        readonly HashSet<Collider> allowed = new HashSet<Collider>();
        readonly List<Collider> removed = new List<Collider>();
        Vector3 closedPosition;
        bool initialized;
        public bool OpenFor(int team) => isActiveAndEnabled && mission != null && team == allowedTeam && mission.IsComplete(team);
        void Awake() { if (doorVisual != null) closedPosition = doorVisual.localPosition; initialized = true; }
        void FixedUpdate() => RefreshPassage();
        public void RefreshPassage()
        {
            if (!isActiveAndEnabled || barrier == null) return;
            allowed.Clear();
            foreach (var body in TeamBodies.All)
            {
                if (!OpenFor(body.Team)) continue;
                foreach (var collider in body.Colliders)
                {
                    if (collider == null || collider == barrier) continue;
                    allowed.Add(collider);
                    if (!previousIgnore.ContainsKey(collider)) previousIgnore.Add(collider, Physics.GetIgnoreCollision(barrier, collider));
                    Physics.IgnoreCollision(barrier, collider, true);
                }
            }
            removed.Clear();
            foreach (var pair in previousIgnore)
                if (pair.Key == null || !allowed.Contains(pair.Key))
                { if (pair.Key != null) Physics.IgnoreCollision(barrier, pair.Key, pair.Value); removed.Add(pair.Key); }
            foreach (var collider in removed) previousIgnore.Remove(collider);
            if (initialized && doorVisual != null)
                doorVisual.localPosition = closedPosition + (OpenFor(allowedTeam) ? Vector3.up * liftHeight : Vector3.zero);
        }
        void OnDisable()
        {
            if (barrier != null) foreach (var pair in previousIgnore) if (pair.Key != null) Physics.IgnoreCollision(barrier, pair.Key, pair.Value);
            previousIgnore.Clear(); allowed.Clear();
            if (initialized && doorVisual != null) doorVisual.localPosition = closedPosition;
        }
    }
}
