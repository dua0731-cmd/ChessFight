using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // D, the capstan gate: a capstan in the middle of the station (u 10, v 0) with 2.25 m bars at
    // waist height. The bars are a real body: pawns push them round with their own bodies (the marked
    // way - clockwise from above in white's station, the mirror in black's). Turning them raises the
    // portcullis in the team gate and opens it.
    //
    // Physics moves the bars; the rules only cap how fast: each of the team's pawns at the bars (at
    // most four) lets it turn MaxTurnRate a second more, so one pawn needs about a minute and four
    // about 16 s. The other team can push it back the same way (MissionStation.ReverseSabotage),
    // never below the last quarter reached (the latch).
    //
    // 2026-10-09 (R92): was 16 turns read from walking round the ring, with the bars a decoration
    // at head height and no collider. Halved, lowered, and pushed for real.
    //
    // R543 (user 11-11: "캡스턴은 막대는 인원수 만큼으로 정해주고 도개교와 같이 진척도로 조절해줘. 그리고 지금 회전 속도가 조금
    // 빠른데 지금의 70%정도로 줄여줘"): a bar for each player of the team (three to six), the turns that open the gate
    // go with the team's size (3.5, 5, 6 and 7), and the bars turn at 70 % of what they did: 0.091 turns a second a
    // pusher (it was 0.13), 0.364 at most (0.52). Five a side's eight turns became six, which take what eight took.
    public sealed class MgCapstan : MiniGameBase
    {
        // Five a side's turns (a capstan saved in a scene before R543 has no number of its own), and the rate.
        public const float Turns = 6f, MaxTurnRate = MiniGameScale.CapstanTurnRate, BarInner = .75f, BarRadius = 3f, U = 10f;
        public const float BarBottom = .4f, BarTop = .62f;
        // A pawn whose hips are this far from the drum's axis counts as at the bars.
        public const float Ring0 = .4f, Ring1 = BarRadius + .6f;
        public const float PortcullisPeek = .4f;

        [SerializeField] MissionStation station;
        [SerializeField] Rigidbody bars;
        [Tooltip("Turns that open the gate (MiniGameScale.CapstanTurns for the team's size).")]
        [SerializeField] float turns = Turns;

        // The turns this capstan takes (the portcullis' jolt at every one of them: MiniGamePortcullisKick).
        public float TurnsToOpen => turns;

        float turned;     // degrees the bars have gone the team's way, net
        float lastYaw;
        float progress;
        bool shown;       // last step showed the host's capstan (online, not the host)
        float targetYaw, shownYaw;   // online, not the host: the host's angle and the one drawn, eased toward it

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
            // R543: a bar a player, evenly round the drum.
            int count = MiniGameScale.CapstanBars(station.TeamSize);
            for (int i = 0; i < count; i++)
            {
                // Solid, so a pawn walking into one pushes it (they start clear of the drum).
                var bar = b.Box("Bar", V(BarInner, BarBottom, -.1f), V(BarRadius, BarTop, .1f), k.hazard, true, true);
                bar.transform.localPosition = Quaternion.Euler(0f, i * 360f / count, 0f) * bar.transform.localPosition;
                bar.transform.localRotation = Quaternion.Euler(0f, i * 360f / count, 0f);
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
            game.turns = MiniGameScale.CapstanTurns(station.TeamSize);
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

        // The bars' angle (16 bits of a turn): the host's capstan drawn on every other PC.
        public override void WriteNet(ref NetBlock block) => block.Unit16(bars != null ? Mathf.Repeat(Yaw, 360f) : 0f, 360f);

        public override void ReadNet(ref NetBlock block)
        {
            targetYaw = block.ReadUnit16(360f);
            if (bars != null) bars.isKinematic = true;
        }

        public override void ApplyRemote(float progress, bool completed)
        {
            this.progress = progress;
            if (station != null && station.Gate != null) station.Gate.SetLift(PortcullisPeek * progress);
            base.ApplyRemote(progress, completed);
        }

        // Not the host: the bars ease to the host's angle every frame (a kinematic picture: nothing on a
        // client pushes them; the host's pawns did).
        void Update()
        {
            if (!Remote || bars == null) return;
            shownYaw = EaseAngle(shownYaw, targetYaw, 25f);
            bars.transform.localRotation = Quaternion.Euler(0f, shownYaw, 0f);
        }

        // StateDrivenMover: the bars turn when pawns push them (physics), not on the obstacle clock.
        // Online the host simulates them and sends their angle (WriteNet).
        void FixedUpdate()
        {
            if (station == null || bars == null) return;
            if (Remote)
            {
                if (!shown) shownYaw = targetYaw = Yaw;
                shown = true;
                return;
            }
            if (shown)
            {
                // This PC has just become the host: carry on from the capstan it was showing.
                shown = false;
                bars.isKinematic = Completed;
                turned = progress * turns * 360f;
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
            float latchDegrees = (float)PawnRushMissions.Latched(progress, 0.0) * turns * 360f;
            float limit = omega >= 0f
                ? Pushers(own) * MaxTurnRate * 360f * Mathf.Deg2Rad
                : (MissionStation.ReverseSabotage && turned > latchDegrees ? Pushers(other) * MaxTurnRate * 360f * Mathf.Deg2Rad : 0f);
            if (Mathf.Abs(omega) > limit)
                bars.angularVelocity = bars.transform.up * (Mathf.Sign(omega) * limit * sign);
            if (turned < latchDegrees)
            {
                // Pushed back past the latch in the last step: hold it there.
                bars.MoveRotation(bars.rotation * Quaternion.Euler(0f, (latchDegrees - turned) * sign, 0f));
                turned = latchDegrees;
            }

            progress = (float)PawnRushMissions.Latched(progress, turned / (turns * 360f));
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

        // The pushers that count toward the bars' speed: four at most, however many bars there are.
        static int Pushers(int count) => Mathf.Clamp(count, 0, MiniGameScale.CapstanSpeedPushers);

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
