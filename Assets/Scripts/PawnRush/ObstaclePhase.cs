using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Phase in SECONDS on the shared obstacle clock (design doc §4: "위상 = ObstacleClock에 더하는
    // 초"), whatever each obstacle keeps internally: ObstacleMotion stores a fraction of its cycle
    // (a rotation, a fraction of a turn), the imported hazards a phaseOffset in seconds. Shift
    // moves everything under `root` by the same time, so a prefab with its own built-in
    // offsets between parts (the clock gates' two doors) keeps them.
    public static class ObstaclePhase
    {
        public static void Shift(GameObject root, float seconds)
        {
            if (root == null || Mathf.Approximately(seconds, 0f)) return;
            foreach (var m in root.GetComponentsInChildren<ObstacleMotion>(true))
            {
                float cycles = m.kind == MotionKind.Rotate
                    ? seconds * m.degreesPerSecond / 360f
                    : seconds / Mathf.Max(.5f, m.period);
                m.phase = Mathf.Repeat(m.phase + cycles, 1f);
            }
            foreach (var x in root.GetComponentsInChildren<FallingChessPiece>(true)) x.phaseOffset += seconds;
            foreach (var x in root.GetComponentsInChildren<PendulumSwing>(true)) x.phaseOffset += seconds;
            foreach (var x in root.GetComponentsInChildren<AirVent>(true)) x.phaseOffset += seconds;
            foreach (var x in root.GetComponentsInChildren<FoldingBridge>(true)) x.phaseOffset += seconds;
            foreach (var x in root.GetComponentsInChildren<KnightCavalryCharge>(true)) x.phaseOffset += seconds;
        }

        // Half the cycle of the first timed obstacle under `root` (0 if none): the offset that makes a
        // mirrored back-and-forth pair a mirror image at every moment.
        public static float HalfPeriod(GameObject root)
        {
            var motion = root.GetComponentInChildren<ObstacleMotion>(true);
            if (motion != null && motion.kind != MotionKind.Rotate) return Mathf.Max(.5f, motion.period) * .5f;
            var pendulum = root.GetComponentInChildren<PendulumSwing>(true);
            if (pendulum != null) return Mathf.Max(.3f, pendulum.SwingDuration) + Mathf.Max(0f, pendulum.PauseAtEnds);
            var piece = root.GetComponentInChildren<FallingChessPiece>(true);
            if (piece != null) return piece.Period * .5f;
            var vent = root.GetComponentInChildren<AirVent>(true);
            if (vent != null) return vent.CycleDuration * .5f;
            var bridge = root.GetComponentInChildren<FoldingBridge>(true);
            if (bridge != null) return bridge.CycleDuration * .5f;
            var knight = root.GetComponentInChildren<KnightCavalryCharge>(true);
            if (knight != null) return knight.CycleDuration * .5f;
            return 0f;
        }
    }
}
