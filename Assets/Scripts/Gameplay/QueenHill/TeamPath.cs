using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A team's shortcut up a floor (8th revision, R50): hidden until the team's bell
    // at the top of that floor is rung, then shown for the rest of the round. It is
    // the team's alone for good: the other team's players pass straight through it
    // (their colliders ignore its colliders, from TeamBodies), and it wears a band
    // of the team's colour.
    //
    // A MovingPlatform among the shown objects runs on the shared clock like any
    // other, so it needs nothing more.
    public sealed class TeamPath : MonoBehaviour
    {
        [SerializeField] int floor = 1;
        [SerializeField] int team;
        [Tooltip("Shown, and made solid for the team, when its bell is rung.")]
        [SerializeField] GameObject[] appear = new GameObject[0];

        readonly List<Collider> pathColliders = new List<Collider>();
        readonly HashSet<Collider> ignoring = new HashSet<Collider>();
        bool started, shown;

        public int Floor => floor;
        public int Team => team;
        public bool Shown => shown;

        public void Configure(int floor, int team, params GameObject[] appear)
        {
            this.floor = floor;
            this.team = team;
            this.appear = appear;
        }

        void Start()
        {
            foreach (var go in appear)
                if (go != null) pathColliders.AddRange(go.GetComponentsInChildren<Collider>(true));
            started = true;
            Show(false);
        }

        void Show(bool on)
        {
            shown = on;
            foreach (var go in appear)
                if (go != null) go.SetActive(on);
        }

        void FixedUpdate()
        {
            if (!started) return;
            var match = QueenHillRaceMatch.Current;
            bool open = match != null && match.IsOpen(floor, team);
            if (open != shown) Show(open);
            if (!open) return;
            // Enemies, including ones that joined since, never stand on it.
            foreach (var body in TeamBodies.All)
            {
                if (!Teams.AreEnemies(team, body.Team)) continue;
                foreach (var c in body.Colliders)
                {
                    if (c == null || !ignoring.Add(c)) continue;
                    foreach (var p in pathColliders) Physics.IgnoreCollision(p, c, true);
                }
            }
        }
    }
}
