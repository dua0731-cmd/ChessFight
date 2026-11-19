using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The royal vault's generic mover (R725, build spec §2.1): a part whose pose is a Core function of the obstacle clock and
    // nothing else (HANDOFF absolute rule 7) - a bulkhead panel sliding out of its pocket, the crown press's die, a drawer, the
    // time-lock door's disc and its shove shell. It slides `travel` (in its own frame) times `along(time)` from where it was
    // placed, and turns `degrees(time)` about `axis`; both functions come from RoyalVaultRules, so the builder only names
    // them and never retypes a number. `time` is ObstacleClock.Now + phase, and the phase stays 0: every RoyalVaultRules
    // function takes the absolute clock and carries its own phase.
    //
    // The functions are continuous, so the base's JumpsBetween stays false and riders and YardImpact shells under it read its
    // motion through the base's IMovingSurface. Its CameraSeeThrough parts (any child that has one) are on while it moves
    // and off while it stands still, so the follow camera looks through a part sweeping past and pulls in for one at rest
    // as for a wall.
    //
    // Configure it on an INACTIVE object, before Awake evaluates the first pose. Lambdas handed to it must not capture a loop
    // variable: copy the index into a local inside the loop first (for (int k = 0; ...) { int d = k; ... DrawerOut(d, t) ... }),
    // or every mover reads the last index.
    public sealed class VaultMover : Obstacle
    {
        // Moving: a point of it faster than this (m/s), or turning faster than TurnSpeed (degrees per second).
        const float MoveSpeed = .01f, TurnSpeed = .1f;

        Vector3 travel, axis;
        Func<double, double> along, degrees;
        bool seeThroughWhileMoving = true;
        readonly List<CameraSeeThrough> sees = new List<CameraSeeThrough>();

        // The slide at along = 1 and the turning axis (zero: it does not turn), in its own frame.
        public Vector3 Travel => travel;
        public Vector3 Axis => axis;
        // Whether it moved this physics step (its CameraSeeThrough parts follow this while seeThroughWhileMoving).
        public bool Moving { get; private set; }

        // localTravel: the slide at along = 1, in this object's frame. along: 0..1 of it at a clock time (null: none).
        // localAxis and degrees: the turn about that axis at a clock time (null or a zero axis: none). seeThroughWhileMoving:
        // switch the CameraSeeThrough parts on while moving and off at rest; false leaves them as the builder set them.
        public void Configure(Vector3 localTravel, Func<double, double> along, Vector3 localAxis = default(Vector3),
                              Func<double, double> degrees = null, bool seeThroughWhileMoving = true)
        {
            travel = localTravel;
            this.along = along;
            axis = localAxis.sqrMagnitude > 1e-12f ? localAxis.normalized : Vector3.zero;
            this.degrees = degrees;
            this.seeThroughWhileMoving = seeThroughWhileMoving;
        }

        // The share of the slide and the turn at a clock time (what Evaluate is given), for validators and bots.
        public double AlongAt(double time) => along != null ? along(time) : 0.0;
        public double DegreesAt(double time) => degrees != null && axis != Vector3.zero ? degrees(time) : 0.0;

        protected override void Awake()
        {
            GetComponentsInChildren(true, sees);
            base.Awake();
            See(MovingAt(ObstacleClock.Now + phase));
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            position = StartPosition + StartRotation * (travel * (float)AlongAt(time));
            // Wrapped in double before narrowing (Obstacle.WrapDegrees): a float has no degrees left at clock scale.
            rotation = degrees == null || axis == Vector3.zero
                ? StartRotation
                : StartRotation * Quaternion.AngleAxis((float)(degrees(time) % 360.0), axis);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            See(MovingAt(ObstacleClock.Now + phase));
        }

        // From the functions themselves over the last step, not the body: the same answer on every PC.
        bool MovingAt(double time)
        {
            double dt = Math.Max(1e-4, Time.fixedDeltaTime), before = time - dt;
            if (along != null && Math.Abs(along(time) - along(before)) * travel.magnitude > MoveSpeed * dt) return true;
            if (degrees == null || axis == Vector3.zero) return false;
            double turned = (degrees(time) - degrees(before)) % 360.0;
            if (turned > 180.0) turned -= 360.0;
            else if (turned < -180.0) turned += 360.0;
            return Math.Abs(turned) > TurnSpeed * dt;
        }

        void See(bool moving)
        {
            Moving = moving;
            if (!seeThroughWhileMoving) return;
            for (int i = 0; i < sees.Count; i++)
            {
                var see = sees[i];
                if (see != null && see.enabled != moving) see.enabled = moving;
            }
        }
    }
}
