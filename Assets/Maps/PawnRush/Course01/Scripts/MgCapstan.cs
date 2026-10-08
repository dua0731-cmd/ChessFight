using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // D, the capstan gate: a capstan in the middle of the station (u 10, v 0) with four 3 m bars.
    // Walking round it the marked way (clockwise from above in white's station, the mirror in
    // black's) turns it; sixteen turns raise the portcullis in the team gate and open it. One pawn
    // turns it about 0.13 a second at most, four count at once. The other team walking round the
    // other way turns it back by as much (MissionStation.ReverseSabotage), never below the last
    // four turns reached.
    //
    // "Pushing a bar" is read as walking the ring round the drum (MissionGames).
    public sealed class MgCapstan : MiniGameBase
    {
        public const float Turns = 16f, MaxTurnRate = .13f, Ring0 = 1f, Ring1 = 4.2f, BarRadius = 3f, U = 10f;
        public const float PortcullisPeek = .4f;

        [SerializeField] MissionStation station;
        [SerializeField] Transform bars;

        readonly List<float> own = new List<float>(), other = new List<float>();
        float progress;

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
                var bar = b.Box("Bar", V(.7f, .92f, -.08f), V(BarRadius, 1.08f, .08f), k.hazard, false, false);
                bar.transform.localPosition = Quaternion.Euler(0f, i * 90f, 0f) * bar.transform.localPosition;
                bar.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            }
            b.parent = before;
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
            game.bars = turning;
            return game;
        }

        // Turns a second one pawn adds (the team's way) or takes off (the other way).
        float Rate(MissionStation.Pawn p, float sign)
        {
            var d = new Vector3(p.Local.x, 0f, p.Local.z - U);
            float r = d.magnitude;
            if (r < Ring0 || r > Ring1 || p.Local.y < -.5f || p.Local.y > 2.5f) return 0f;
            var tangent = Vector3.Cross(Vector3.up, d / r) * sign;
            float along = Vector3.Dot(new Vector3(p.Velocity.x, 0f, p.Velocity.z), tangent);
            return Mathf.Clamp(along / (2f * Mathf.PI * r), -MaxTurnRate, MaxTurnRate);
        }

        static float Top(List<float> rates)
        {
            rates.Sort();
            float sum = 0f;
            for (int i = rates.Count - 1, n = 0; i >= 0 && n < PawnRushMissions.WorkerCap; i--, n++) sum += rates[i];
            return sum;
        }

        void FixedUpdate()
        {
            if (station == null) return;
            if (!Completed)
            {
                float sign = station.Mirrored ? -1f : 1f;
                own.Clear();
                other.Clear();
                foreach (var p in station.Pawns())
                {
                    float rate = Rate(p, sign);
                    if (p.Own && rate > .4f / (2f * Mathf.PI * Ring1)) own.Add(rate);
                    else if (!p.Own && rate < 0f) other.Add(-rate);
                }
                float turns = Top(own) - (MissionStation.ReverseSabotage ? Top(other) : 0f);
                progress = (float)PawnRushMissions.Latched(progress, progress + turns / Turns * Time.fixedDeltaTime);
                Progress01 = progress;
                if (station.Gate != null) station.Gate.SetLift(PortcullisPeek * progress);
                if (progress >= 1f) Complete();
            }
            // StateDrivenMover (visual only): the bars show the turns made.
            if (bars != null) bars.localRotation = Quaternion.Euler(0f, (station.Mirrored ? -360f : 360f) * Turns * Progress01, 0f);
        }

        public override void ResetGame()
        {
            base.ResetGame();
            progress = 0f;
        }
    }
}
