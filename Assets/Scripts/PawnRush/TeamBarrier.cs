using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The door of a team section (design doc §5 "팀 장벽"): its own team walks
    // straight through it both ways, the other team meets a wall. It cannot be
    // climbed (NoClimbSurface sits next to it). Uses the same mechanism as Queen of
    // the Hill's OpenPath: the bodies of the team that may pass ignore the barrier's
    // colliders (TeamBodies lists every pawn's colliders by team), and the pawn's own
    // probes skip ignored colliders too (RagdollPawn.PassesThrough).
    public sealed class TeamBarrier : MonoBehaviour
    {
        [Tooltip("0 = white, 1 = black: the team that may pass.")]
        [SerializeField, Range(0, 1)] int team = Teams.White;

        readonly List<Collider> own = new List<Collider>();
        readonly Dictionary<Collider, bool> passing = new Dictionary<Collider, bool>();
        readonly List<Collider> stale = new List<Collider>();

        public int Team => team;
        public void SetTeam(int team) => this.team = team;

        void Start() => GetComponentsInChildren(own);

        // Every step: a pawn can change sides (the playtest's F5) or be spawned at any time.
        void FixedUpdate()
        {
            foreach (var body in TeamBodies.All)
            {
                bool pass = body.Team == team;
                foreach (var c in body.Colliders)
                {
                    if (c == null) continue;
                    if (passing.TryGetValue(c, out bool was) && was == pass) continue;
                    passing[c] = pass;
                    foreach (var b in own)
                        if (b != null) Physics.IgnoreCollision(b, c, pass);
                }
            }
            stale.Clear();
            foreach (var c in passing.Keys) if (c == null) stale.Add(c);
            foreach (var c in stale) passing.Remove(c);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = team == Teams.White ? new Color(1f, 1f, 1f, .25f) : new Color(.1f, .1f, .12f, .35f);
            foreach (var box in GetComponentsInChildren<BoxCollider>())
            {
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
        }
    }
}
