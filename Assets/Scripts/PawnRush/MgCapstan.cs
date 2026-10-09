using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // D, the capstan gate: a capstan in the middle of the station (u 10, v 0) with four 2.25 m bars at
    // waist height. The bars are a real body: pawns push them round with their own bodies (the marked
    // way - clockwise from above in white's station, the mirror in black's). Eight turns raise the
    // portcullis in the team gate and open it.
    //
    // Physics moves the bars; the rules only cap how fast: each of the team's pawns at the bars (at
    // most four) lets it turn MaxTurnRate a second more, so one pawn needs about a minute and four
    // about 15 s. The other team can push it back the same way (MissionStation.ReverseSabotage),
    // never below the last quarter reached (the latch).
    //
    // 2026-10-09 (R92): was 16 turns read from walking round the ring, with the bars a decoration
    // at head height and no collider. Halved, lowered, and pushed for real.
    public sealed class MgCapstan : MiniGameBase
    {
        public const float Turns = 8f, MaxTurnRate = .13f, BarInner = .75f, BarRadius = 3f, U = 10f;
        public const float BarBottom = .4f, BarTop = .62f;
        // A pawn whose hips are this far from the drum's axis counts as at the bars.
        public const float Ring0 = .4f, Ring1 = BarRadius + .6f;
        public const float PortcullisPeek = .4f;

        [SerializeField] MissionStation station;
        [SerializeField] Rigidbody bars;

        float turned;     // degrees the bars have gone the team's way, net
        float lastYaw;
        float progress;
        bool shown;       // last step showed the host's capstan (online, not the host)

        static Vector3 V(float x, float y, float z) => MissionGames.V(x, y, z);

        public static MgCapstan Build(CourseBuilder b, MissionStation station)
        {
            var k = b.kit;
            MissionGames.Floor(b, 0f, 20f);
            b.Cylinder("Capstan drum", V(0f, 0f, U), 1.4f, 1.8f, k.wallNoClimb, true, true);
            b.Cylinder("Capstan cap", V(0f, 1.8f, U), 1.8f, .3f, k.rankGold, false);
            var turning = b.Group("Capstan bars");
            turning.localPosition = V(0f, 0f, U);
            var before = b.parent;
            b.parent = turning;
            for (int i = 0; i < 4; i++)
            {
                // Solid, so a pawn walking into one pushes it (they start clear of the drum).
                var bar = b.Box("Bar", V(BarInner, BarBottom, -.1f), V(BarRadius, BarTop, .1f), k.hazard, true, true);
                bar.transform.localPosition = Quaternion.Euler(0f, i * 90f, 0f) * bar.transform.localPosition;
                bar.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            }
            b.parent = before;
            // It only turns about its own axis; heavy enough that a bump does not spin it far.
            var body = turning.gameObject.AddComponent<Rigidbody>();
            body.mass = 60f;
            body.useGravity = false;
            body.angularDamping = 1.5f;
            body.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.centerOfMass = Vector3.zero;
            // The way round, marked on the floor: arrows at the ring's middle.
            float sign = station.Mirrored ? -1f : 1f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var radial = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var tangent = Vector3.Cross(Vector3.up, radial) * sign;
                var at = V(0f, 0f, U) + radial * 2.6f;
                var mark = b.Box("Way round", at + V(-.12f, 0f, -.5f), at + V(.12f, .02f, .5f), k.rankGold, false, false);
                mark.transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
                var tip = b.Box("Way round tip", V(-.3f, 0f, -.1f), V(.3f, .02f, .1f), k.rankGold, false, false);
                tip.transform.localPosition = at + tangent * .45f + V(0f, .01f, 0f);
                tip.transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
            }

            var game = b.parent.gameObject.AddComponent<MgCapstan>();
            game.station = station;
            game.bars = body;
            return game;
        }

        static bool AtBars(MissionStation.Pawn p)
        {
            float r = new Vector2(p.Local.x, p.Local.z - U).magnitude;
            return r >= Ring0 && r <= Ring1 && p.Local.y > -.5f && p.Local.y < 2.5f;
        }

        float Yaw => bars.transform.localEulerAngles.y;

        void Start()
        {
            if (bars != null) lastYaw = Yaw;
        }

        // The bars' angle (degrees, as float bits): the host's capstan drawn on every other PC.
        public override ulong StateBits => (uint)System.BitConverter.SingleToInt32Bits(bars != null ? Yaw : 0f);

        public override void ApplyRemote(float progress, bool completed, ulong bits)
        {
            this.progress = progress;
            if (bars != null)
            {
                bars.isKinematic = true;
                bars.MoveRotation(bars.transform.parent.rotation * Quaternion.Euler(0f, System.BitConverter.Int32BitsToSingle((int)(uint)bits), 0f));
            }
            if (station != null && station.Gate != null) station.Gate.SetLift(PortcullisPeek * progress);
            base.ApplyRemote(progress, completed, bits);
        }

        // StateDrivenMover: the bars turn when pawns push them (physics), not on the obstacle clock.
        // Online the host simulates them and sends their angle (StateBits).
        void FixedUpdate()
        {
            if (station == null || bars == null) return;
            if (Remote)
            {
                shown = true;
                return;
            }
            if (shown)
            {
                // This PC has just become the host: carry on from the capstan it was showing.
                shown = false;
                bars.isKinematic = Completed;
                turned = progress * Turns * 360f;
                lastYaw = Yaw;
            }
            float sign = station.Mirrored ? -1f : 1f;
            float yaw = Yaw;
            turned += Mathf.DeltaAngle(lastYaw, yaw) * sign;
            lastYaw = yaw;
            if (Completed) return;

            int own = 0, other = 0;
            foreach (var p in station.Pawns())
                if (AtBars(p)) { if (p.Own) own++; else other++; }
            // The team's way is positive. Cap the spin by who is pushing; nobody pushes it backwards
            // past the latch, nor at all while sabotage is off.
            var spin = bars.angularVelocity;
            float omega = Vector3.Dot(spin, bars.transform.up) * sign;
            float latchDegrees = (float)PawnRushMissions.Latched(progress, 0.0) * Turns * 360f;
            float limit = omega >= 0f
                ? PawnRushMissions.Workers(own) * MaxTurnRate * 360f * Mathf.Deg2Rad
                : (MissionStation.ReverseSabotage && turned > latchDegrees ? PawnRushMissions.Workers(other) * MaxTurnRate * 360f * Mathf.Deg2Rad : 0f);
            if (Mathf.Abs(omega) > limit)
                bars.angularVelocity = bars.transform.up * (Mathf.Sign(omega) * limit * sign);
            if (turned < latchDegrees)
            {
                // Pushed back past the latch in the last step: hold it there.
                bars.MoveRotation(bars.rotation * Quaternion.Euler(0f, (latchDegrees - turned) * sign, 0f));
                turned = latchDegrees;
            }

            progress = (float)PawnRushMissions.Latched(progress, turned / (Turns * 360f));
            Progress01 = progress;
            if (station.Gate != null) station.Gate.SetLift(PortcullisPeek * progress);
            if (progress >= 1f)
            {
                Complete();
                // Done: the capstan stays where it stopped.
                bars.angularVelocity = Vector3.zero;
                bars.isKinematic = true;
            }
        }

        public override void ResetGame()
        {
            base.ResetGame();
            progress = turned = 0f;
            if (bars == null) return;
            bars.isKinematic = false;
            bars.angularVelocity = Vector3.zero;
            bars.transform.localRotation = Quaternion.identity;
            lastYaw = Yaw;
        }
    }
}
