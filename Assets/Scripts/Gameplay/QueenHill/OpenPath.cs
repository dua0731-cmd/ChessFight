using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A section's fast path (DESIGN §3.3 B): the light pillar's lift cell, a chain
    // ladder, a light bridge... Hidden until the section's bell is rung, then
    // shown for the rest of the round. A MovingPlatform among the shown objects
    // runs on the shared clock like any other, so it needs nothing more.
    //
    // For the first exclusive seconds only the team that opened it can use it:
    // everyone else passes straight through it (their colliders ignore its
    // colliders, from TeamBodies), and it wears that team's colour.
    public sealed class OpenPath : MonoBehaviour
    {
        [SerializeField] int section = 1;
        [Tooltip("Shown, and made solid, when the section opens.")]
        [SerializeField] GameObject[] appear = new GameObject[0];

        static readonly Color WhiteTeam = new Color(0.96f, 0.96f, 0.94f);
        static readonly Color BlackTeam = new Color(0.12f, 0.12f, 0.14f);

        readonly List<Collider> pathColliders = new List<Collider>();
        readonly HashSet<Collider> passing = new HashSet<Collider>();
        Renderer[] renderers;
        MaterialPropertyBlock block;
        bool started, shown, tinted;

        public int Section => section;

        // True while an object of this path is shown.
        public bool Shown => shown;

        public void Configure(int section, params GameObject[] appear)
        {
            this.section = section;
            this.appear = appear;
        }

        void Start()
        {
            var list = new List<Renderer>();
            foreach (var go in appear)
            {
                if (go == null) continue;
                pathColliders.AddRange(go.GetComponentsInChildren<Collider>(true));
                list.AddRange(go.GetComponentsInChildren<Renderer>(true));
            }
            renderers = list.ToArray();
            block = new MaterialPropertyBlock();
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
            var match = QueenHillMatch.Current;
            bool open = match != null && match.IsOpen(section);
            if (open != shown) Show(open);
            bool exclusive = open && match.Exclusive(section);
            int pioneer = open ? match.Pioneer(section) : Teams.None;
            if (exclusive)
            {
                foreach (var body in TeamBodies.All)
                {
                    if (!Teams.AreEnemies(pioneer, body.Team)) continue;
                    foreach (var c in body.Colliders)
                    {
                        if (c == null || !passing.Add(c)) continue;
                        foreach (var p in pathColliders) Physics.IgnoreCollision(p, c, true);
                    }
                }
            }
            else if (passing.Count > 0)
            {
                foreach (var c in passing)
                    if (c != null)
                        foreach (var p in pathColliders) Physics.IgnoreCollision(p, c, false);
                passing.Clear();
            }
            Tint(exclusive, pioneer);
        }

        void Tint(bool exclusive, int team)
        {
            if (exclusive == tinted || renderers == null) return;
            tinted = exclusive;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (exclusive)
                {
                    block.SetColor("_Color", team == Teams.Black ? BlackTeam : WhiteTeam);
                    r.SetPropertyBlock(block);
                }
                else r.SetPropertyBlock(null);
            }
        }
    }
}
