using System;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 (the user, screenshots of the great globe's rings: "로비 화면처럼 교차로 움직이게 멋있게", then "사진에 보이는
    // 고리를 로비처럼 하라고, 지금 멋이 없어"): the rings round the globe are built and move like the lobby's armillary
    // sphere (ArtSource/Lobby/KingdomInterior/armillary.py, LobbyStage.Hall.Armillary) - a fixed frame (the meridian
    // and the horizon) and three gimbals nested inside it, each pinned to the one outside it and carrying its own
    // crossing rings, so they keep crossing in depth.
    // The lobby's gimbals turn all the way round; these cannot: people walk into the globe by its west and east gates
    // and out to Cassiopeia's first star, all on the globe's east-west line, and a ring turning all the way round would
    // sweep through them. So each gimbal swings to and fro about its pin axis (a sine; periods 20, 13 and 9 s, so the
    // three never repeat together for a long while) only as far as keeps every ring 0.6 m clear of anything solid -
    // RoyalObservatoryValidation sweeps them over minutes of the clock to check it.
    // Drawn only (no collider, no shove) and a pure function of the shared obstacle clock: every PC shows the same,
    // nothing is sent.
    public sealed class GlobeArmillary : MonoBehaviour
    {
        [Serializable]
        public struct Gimbal
        {
            public Transform pivot;          // at the globe's middle, a child of the gimbal outside it
            public Vector3 axis;             // its pin axis, in its parent's frame
            public float swing, period, phase;
            public Quaternion[] lies;        // the rings it carries, each in the pivot's frame (its normal is lie * up) ...
            public float[] radii;            // ... and their radii (the validation's sweep check)
        }

        [SerializeField] Gimbal[] gimbals = new Gimbal[0];

        public Gimbal[] Gimbals => gimbals;

        public void Configure(Gimbal[] value)
        {
            gimbals = value;
            Pose(ObstacleClock.Now);
        }

        void LateUpdate() => Pose(ObstacleClock.Now);

        public void Pose(double time)
        {
            foreach (var g in gimbals)
            {
                if (g.pivot == null) continue;
                float angle = g.period > 0f ? g.swing * (float)Math.Sin(((time / g.period) + g.phase) % 1.0 * Math.PI * 2.0) : 0f;
                g.pivot.localRotation = Quaternion.AngleAxis(angle, g.axis);
            }
        }
    }
}
