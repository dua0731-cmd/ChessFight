using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class KingRushPawn
    {
        IKingRushCharacter cannonTarget;
        Vector3 cannonAim, cannonVelocity;
        bool cannonGrabHeld, cannonValid, cannonEnemy;
        float flightAge;
        int flightHits;
        LineRenderer cannonArc, cannonMark;
        Func<int, Quaternion?> previousPose;
        readonly RaycastHit[] cannonGroundHits = new RaycastHit[32];
        struct ClearedPair { public Collider a, b; public bool ignored; }
        readonly List<ClearedPair> cannonClearance = new List<ClearedPair>();
        float clearanceTime;
        public Vector3 LaunchOrigin => BodyPosition - Vector3.up * Pawn.standHeight;
        public Vector3 CannonLanding { get; private set; }
        public bool CannonFlight { get; private set; }
        public int CannonShots { get; private set; }
        public int CannonLandings { get; private set; }
        public Vector3 LastCannonLanding { get; private set; }
        public bool CannonAiming => Piece == KingRushPiece.Rook && AbilityActive && cannonTarget != null;

        void InitializeCannon()
        {
            cannonArc = Ring("Cannon predicted arc", .025f, Color.cyan);
            cannonMark = Ring("Cannon landing marker", .05f, Color.cyan);
            previousPose = Pawn.PoseOverride; Pawn.PoseOverride = CannonPose;
        }
        Quaternion? CannonPose(int part)
        {
            if (CannonAiming && part == (int)BodyId.Chest) return Quaternion.Euler(12, 0, 0);
            return previousPose?.Invoke(part);
        }
        bool Holds(IKingRushCharacter target)
        {
            foreach (var c in target.BodyColliders)
                if (c != null && ((Pawn.handL.IsHolding && Pawn.handL.HeldCollider == c) ||
                    (Pawn.handR.IsHolding && Pawn.handR.HeldCollider == c))) return true;
            return false;
        }
        void BeginCannon()
        {
            if (!cannonGrabHeld) return;
            foreach (var member in match.Characters)
            {
                if (member.Id == Id || !Holds(member)) continue;
                cannonTarget = member; windup = .6f; hitsAtStart = Pawn.Hits;
                UpdateCannonAim(); return;
            }
        }
        void StepCannonAim(float dt)
        {
            // A real hand joint owns the capture. Escape/release cannot be replaced by proximity.
            if (cannonTarget == null || !cannonGrabHeld || !Holds(cannonTarget)) { CancelAbility(); return; }
            UpdateCannonAim(); windup -= dt;
            if (windup > 0) return;
            var target = cannonTarget;
            if (cannonValid && target.LaunchFromCannon(cannonVelocity, target.Team != Team))
            {
                ClearCannonMuzzle(target);
                CannonShots++; cooldownDuration = cooldown = 8;
            }
            CancelAbility();
        }
        void ClearCannonMuzzle(IKingRushCharacter target)
        {
            RestoreCannonClearance();
            // For the first 0.2s only the firing rook cannot catch the projectile with
            // its still-reaching arms. Scenery and every other pawn remain solid.
            foreach (var a in BodyColliders)
                foreach (var b in target.BodyColliders)
                {
                    if (a == null || b == null) continue;
                    cannonClearance.Add(new ClearedPair { a = a, b = b, ignored = Physics.GetIgnoreCollision(a, b) });
                    Physics.IgnoreCollision(a, b, true);
                }
            clearanceTime = .2f;
        }
        void RestoreCannonClearance()
        {
            foreach (var pair in cannonClearance)
                if (pair.a != null && pair.b != null) Physics.IgnoreCollision(pair.a, pair.b, pair.ignored);
            cannonClearance.Clear(); clearanceTime = 0;
        }
        void StepCannonClearance(float dt)
        {
            if (clearanceTime <= 0) return;
            clearanceTime -= dt;
            if (clearanceTime <= 0) RestoreCannonClearance();
        }
        void OnDisable() => RestoreCannonClearance();
        void UpdateCannonAim()
        {
            Vector3 aim = cannonAim.sqrMagnitude > .01f ? cannonAim.normalized : Pawn.Facing;
            Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up);
            if (flat.sqrMagnitude < .001f) flat = Pawn.Facing;
            flat.Normalize();
            // Downward pitch selects a close landing; level/upward view selects maximum range.
            float distance = aim.y < -.04f ? (Pawn.standHeight + .8f) * new Vector2(aim.x, aim.z).magnitude / -aim.y : 20;
            distance = Mathf.Clamp(distance, 4, 20);
            Vector3 from = cannonTarget.LaunchOrigin;
            Vector3 candidate = from + flat * distance;
            int n = Physics.RaycastNonAlloc(candidate + Vector3.up * 20, Vector3.down, cannonGroundHits, 40, ~0, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity; cannonValid = false;
            for (int i = 0; i < n; i++)
            {
                var hit = cannonGroundHits[i];
                if (hit.normal.y < .65f || RagdollPawn.ColliderOwner.ContainsKey(hit.collider) || hit.distance >= best) continue;
                best = hit.distance; candidate.y = hit.point.y; cannonValid = true;
            }
            CannonLanding = candidate;
            cannonVelocity = LaunchPad.ArcVelocity(from, candidate, 3, -Physics.gravity.y);
        }
        public bool LaunchFromCannon(Vector3 velocity, bool enemy)
        {
            if (!match.Authority || Pawn.NetworkPuppet || Pawn.Floating || !Pawn.IsFinite() ||
                !float.IsFinite(velocity.x) || !float.IsFinite(velocity.y) || !float.IsFinite(velocity.z)) return false;
            CancelAbility(); foreach (var holder in match.Characters) holder.ReleaseHoldOn(this);
            cannonEnemy = enemy; flightAge = 0; flightHits = Pawn.Hits; CannonFlight = true;
            Pawn.SetInput(default); Pawn.LaunchForKingRush(velocity); return true;
        }
        void StepCannonFlight(float dt)
        {
            if (!CannonFlight) return;
            flightAge += dt;
            if (Pawn.Floating || Pawn.State != PawnState.Active || Pawn.Hits != flightHits || flightAge > 6)
            { CannonFlight = false; return; }
            if (flightAge < .15f || !Pawn.Grounded || Pawn.Hips.linearVelocity.y > .5f) return;
            CannonFlight = false; CannonLandings++; LastCannonLanding = LaunchOrigin;
            if (cannonEnemy) HitReceiver.ApplyHit(Vector3.zero, 1, 0, false);
        }
        void DrawCannon()
        {
            bool show = CannonAiming;
            cannonArc.enabled = show;
            DrawRing(cannonMark, CannonLanding + Vector3.up * .05f, .45f, show ? 1 : 0);
            if (!show) return;
            Color color = cannonValid ? Color.cyan : new Color(1, .25f, .1f);
            cannonArc.startColor = cannonArc.endColor = cannonMark.startColor = cannonMark.endColor = color;
            Vector3 from = cannonTarget.LaunchOrigin;
            float duration = LaunchPad.ArcSeconds(CannonLanding.y - from.y, 3, -Physics.gravity.y);
            cannonArc.positionCount = 33;
            for (int i = 0; i <= 32; i++)
            {
                float t = duration * i / 32;
                cannonArc.SetPosition(i, from + Vector3.up * Pawn.standHeight + cannonVelocity * t + Physics.gravity * (.5f * t * t));
            }
        }
    }
}
