using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A bishop's stone (Queen of the Hill M12, DESIGN §6.2): a real physics ball thrown on a slow arc from a
    /// hovering bishop. It never touches its thrower, bounces off the world like any ball, and the first enemy
    /// pawn it meets takes the stone's effect (RagdollPawn.ShardHit: a stagger and a push, stamina off a wall,
    /// off a moving platform at the second stone). Gone after ShardLife seconds.
    ///
    /// The contact is only noted in the physics callback and handed on in FixedUpdate. Only the machine
    /// that simulates the pawns throws stones; clients do not see them yet (M14).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PieceShard : MonoBehaviour
    {
        static readonly List<PieceShard> live = new List<PieceShard>();
        static Material material;

        RagdollPawn thrower;
        Rigidbody body;
        float born;
        bool spent;
        RagdollPawn hitPawn;
        Vector3 lastVelocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => live.Clear();

        public RagdollPawn Thrower => thrower;
        public static IReadOnlyList<PieceShard> Live => live;

        /// <summary>Stones of this bishop still flying and not yet spent on a pawn.</summary>
        public static int LiveFrom(RagdollPawn pawn)
        {
            int n = 0;
            foreach (var s in live)
                if (s != null && s.thrower == pawn && !s.spent) n++;
            return n;
        }

        public static PieceShard Throw(RagdollPawn thrower, Vector3 at, Vector3 velocity)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bishop Stone";
            go.transform.position = at;
            go.transform.localScale = Vector3.one * 0.36f;
            if (material == null)
            {
                var shader = Shader.Find("Standard");
                material = new Material(shader != null ? shader : Shader.Find("Legacy Shaders/Diffuse")) { color = new Color(0.62f, 0.58f, 0.52f) };
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = velocity;
            var ball = go.GetComponent<Collider>();
            if (thrower.Colliders != null)
                foreach (var c in thrower.Colliders)
                    if (c != null) Physics.IgnoreCollision(ball, c, true);
            var shard = go.AddComponent<PieceShard>();
            shard.thrower = thrower;
            shard.body = rb;
            shard.born = Time.time;
            shard.lastVelocity = velocity;
            live.Add(shard);
            return shard;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (spent || thrower == null) return;
            if (!RagdollPawn.ColliderOwner.TryGetValue(collision.collider, out var pawn) || pawn == thrower) return;
            // One pawn per stone; a teammate only gets the bump of the ball itself.
            spent = true;
            if (Teams.AreEnemies(thrower.Team, pawn.Team)) hitPawn = pawn;
        }

        void FixedUpdate()
        {
            if (hitPawn != null)
            {
                hitPawn.ShardHit(lastVelocity);
                hitPawn = null;
            }
            if (body != null && body.linearVelocity.sqrMagnitude > 0.01f && !spent) lastVelocity = body.linearVelocity;
            if (Time.time - born > PieceAbilities.ShardLife) Destroy(gameObject);
        }

        void OnDestroy() => live.Remove(this);
    }
}
