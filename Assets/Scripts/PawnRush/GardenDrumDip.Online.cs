using System.Collections.Generic;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R544 (승규 11-10, "온라인은 따로 만들어"): who is on a floating drum and how they move, the same way on every PC.
    // The host runs every piece's physics; a client draws them as puppets posed from the host's snapshots (RagdollPawn
    // puppet mode: kinematic bodies, no velocity of their own). So each PC watches the pieces it draws:
    //   the host's pieces   their physics velocity (ImportedObstacleActor.Velocity)
    //   a client's puppets  how far they moved since the last frame, smoothed over the snapshots' steps
    // and every PC leans and sinks its drums from the same positions - a client a snapshot's delay after the host, on
    // the moment it sees the piece land. Nothing new goes over the wire: the positions are already there.
    // Pieces are followed from Watch m above the top, so a puppet's fall speed is known by the time it lands.
    public sealed partial class GardenDrumDip
    {
        const float Watch = 4f;                 // m over the top where pieces are followed
        const float OnTop = .45f, OffTop = .65f; // m over the top: a piece's feet count as on it / off it again
        const float Rearm = .5f;                // s out of sight before a piece is followed afresh

        struct Rider { public Vector2 offset, walk; }
        sealed class Track { public Vector3 last, velocity; public float seen; public bool on, fresh; }

        readonly List<Rider> riders = new List<Rider>();
        int riderKey;                           // R635: which pieces are on it (changes when one steps on or off)
        readonly Dictionary<ImportedObstacleActor, Track> tracks = new Dictionary<ImportedObstacleActor, Track>();
        readonly HashSet<ImportedObstacleActor> seen = new HashSet<ImportedObstacleActor>();
        readonly Collider[] hits = new Collider[128];

        void Gather(float t, float dt)
        {
            riders.Clear();
            riderKey = 0;
            seen.Clear();
            var box = deck.bounds;
            float top = box.max.y, radius = Mathf.Max(.1f, box.extents.x);
            int n = Physics.OverlapBoxNonAlloc(new Vector3(box.center.x, top + Watch * .5f, box.center.z),
                new Vector3(box.extents.x + .3f, Watch * .5f, box.extents.z + .3f), hits, Quaternion.identity, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(hits[i]);
                if (actor == null || !actor.isActiveAndEnabled || !seen.Add(actor)) continue;
                var p = actor.Position;
                // A piece the host simulates here has its own velocity; a puppet (kinematic) is read from its moves.
                bool simulated = actor.CanReceiveObstacle;
                if (!tracks.TryGetValue(actor, out var k) || t - k.seen > Rearm)
                {
                    k = new Track { last = p, velocity = simulated ? actor.Velocity : Vector3.zero, fresh = true };
                    tracks[actor] = k;
                }
                else if (simulated) k.velocity = actor.Velocity;
                else if (dt > 1e-4f)
                {
                    k.velocity = Vector3.Lerp(k.velocity, (p - k.last) / dt, 1f - Mathf.Exp(-12f * dt));
                    k.fresh = false;
                }
                k.last = p;
                k.seen = t;

                var off = new Vector2(p.x - box.center.x, p.z - box.center.z);
                bool over = off.magnitude < radius + .2f && p.y > top - .3f;
                bool on = over && p.y < top + (k.on ? OffTop : OnTop);
                var offset = Vector2.ClampMagnitude(off / radius, 1f);
                var walk = Vector2.ClampMagnitude(new Vector2(k.velocity.x, k.velocity.z) / LeanSpeed, 1f);
                // A puppet first seen already on it came on from the side, its fall unknown: a light landing.
                if (on && !k.on) Land(offset, walk, k.fresh && !simulated ? 3f : -k.velocity.y);
                k.on = on;
                if (!on) continue;
                riders.Add(new Rider { offset = offset, walk = walk });
                // the same whatever order the overlap lists them in (the actor is a plain class: its identity hash)
                riderKey ^= System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(actor) * 16777619;
            }
            // A piece gone from over the drum is off it, even if it comes back before it is followed afresh.
            foreach (var pair in tracks)
                if (!seen.Contains(pair.Key)) pair.Value.on = false;
            if (tracks.Count > 32) tracks.Clear();
        }
    }
}
