using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The pieces' abilities (Queen of the Hill M12, DESIGN §6.2). E is each piece's main ability (the pawn's is
    /// the hook, in RagdollPawn.Hook.cs); Q the king's and the queen's second one.
    ///
    /// Knight. E: the L-jump, "up two, over one" - 6 m up and 3 m on along the aim, on the launch pads' fixed
    /// arc (M7), every 5 s. Where it will come down is worked out at take-off and marked on the ground for
    /// everyone to see. Passive: landing on an enemy's head squashes it (M13) and the knight bounces off.
    /// </summary>
    public partial class RagdollPawn
    {
        float abilityCooldown;
        bool knightFlying;
        Vector3 knightLanding;
        Transform landingMarker;
        static Material landingMaterial;

        public float AbilityCooldown => Mathf.Max(0f, abilityCooldown);
        public int KnightJumps { get; private set; }
        public int Stomps { get; private set; }
        /// <summary>Where the knight's L-jump will come down, while it is in the air.</summary>
        public bool KnightLandingShown => knightFlying;
        public Vector3 KnightLanding => knightLanding;

        void UpdateAbilities(RagdollParams p, float dt)
        {
            abilityCooldown -= dt;
            switch (piece)
            {
                case PieceKind.Knight:
                    if (input.ability) KnightJump(p);
                    Stomp(p);
                    break;
            }
            // The marker stays until the knight is down again (or anything else took it off the arc).
            if (knightFlying && (!launched || State == PawnState.Ragdoll)) knightFlying = false;
            ShowLanding();
        }

        bool CanUseAbility => State == PawnState.Active && !Climbing && rope == null && !Floating && !BeingHeld && !Squashed
                              && !Staggered && abilityCooldown <= 0f;

        // ---------------------------------------------------------------- knight

        void KnightJump(RagdollParams p)
        {
            if (!CanUseAbility || !(Grounded || coyote > 0f)) return;
            Vector3 dir = Flat(input.aim);
            if (dir.sqrMagnitude < 1e-4f) dir = facing;
            dir.Normalize();
            float gravity = Mathf.Max(0.01f, -Physics.gravity.y);
            Vector3 from = bodies[0].position - Vector3.up * standHeight;
            Vector3 to = from + dir * p.knightJumpForward + Vector3.up * p.knightJumpUp;
            Vector3 velocity = LaunchPad.ArcVelocity(from, to, p.knightJumpClearance, gravity);
            facing = dir;
            Launch(velocity);
            abilityCooldown = p.knightJumpCooldown;
            KnightJumps++;
            knightLanding = PredictLanding(bodies[0].position, velocity, gravity);
            knightFlying = true;
        }

        /// <summary>Where a body thrown from <paramref name="hips"/> at <paramref name="velocity"/> first comes
        /// down on something it can stand on (the feet's point), stepping the arc against the world.</summary>
        Vector3 PredictLanding(Vector3 hips, Vector3 velocity, float gravity)
        {
            Vector3 at = hips, v = velocity;
            const float step = 0.02f;
            for (int i = 0; i < 400; i++)
            {
                Vector3 next = at + v * step;
                v += Vector3.down * (gravity * step);
                Vector3 feet = at - Vector3.up * standHeight, nextFeet = next - Vector3.up * standHeight;
                Vector3 seg = nextFeet - feet;
                if (v.y < 0f && Physics.SphereCast(feet, 0.15f, seg.normalized, out var hit, seg.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    && !ownSet.Contains(hit.collider) && hit.normal.y > 0.5f && (hit.rigidbody == null || hit.rigidbody.isKinematic))
                    return hit.point;
                at = next;
            }
            return at - Vector3.up * standHeight;
        }

        /// <summary>Knight's passive: coming down on an enemy's head squashes it; the knight bounces off.</summary>
        void Stomp(RagdollParams p)
        {
            if (State != PawnState.Active || Grounded || Climbing || rope != null || bodies[0].linearVelocity.y > -1f) return;
            Vector3 feet = bodies[0].position - Vector3.up * standHeight;
            foreach (var other in All)
            {
                if (other == null || other == this || !Teams.AreEnemies(team, other.team) || other.State == PawnState.Ragdoll) continue;
                var headCollider = other.bodies[(int)BodyId.Head].GetComponent<Collider>();
                Vector3 head = other.bodies[(int)BodyId.Head].position;
                float top = headCollider != null ? headCollider.bounds.max.y : head.y + 0.12f;
                if (Flat(feet - head).magnitude > p.stompReach || feet.y < top - 0.15f || feet.y > top + 0.35f) continue;
                var receiver = other.GetComponent<IStatusReceiver>();
                if (receiver == null || !receiver.Squash(p.stompSquash, p.stompImmunity)) continue;
                Stomps++;
                // Off its head, up and a little on, so it does not stand on the squashed pawn.
                Vector3 v = bodies[0].linearVelocity;
                Vector3 bounce = new Vector3(0f, p.stompBounce - v.y, 0f) + Flat(facing).normalized * 1f;
                foreach (var rb in bodies) rb.linearVelocity += bounce;
                freeFlight = Mathf.Max(freeFlight, 0.4f);
                airTimer = Mathf.Max(airTimer, 0.15f);
                return;
            }
        }

        /// <summary>The knight's landing spot, on the ground for everyone to see while it is in the air.</summary>
        void ShowLanding()
        {
            if (!knightFlying)
            {
                if (landingMarker != null) landingMarker.gameObject.SetActive(false);
                return;
            }
            if (landingMarker == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                var c = go.GetComponent<Collider>();
                if (c != null) DestroyImmediate(c);
                go.name = $"{name} landing";
                if (landingMaterial == null)
                {
                    var shader = Shader.Find("Standard");
                    landingMaterial = new Material(shader != null ? shader : Shader.Find("Legacy Shaders/Diffuse")) { color = new Color(1f, 0.3f, 0.2f) };
                }
                go.GetComponent<MeshRenderer>().sharedMaterial = landingMaterial;
                go.transform.localScale = new Vector3(1f, 0.01f, 1f);
                landingMarker = go.transform;
            }
            landingMarker.gameObject.SetActive(true);
            landingMarker.position = knightLanding + Vector3.up * 0.02f;
        }

        void ClearAbilities()
        {
            knightFlying = false;
            if (landingMarker != null) landingMarker.gameObject.SetActive(false);
        }
    }
}
