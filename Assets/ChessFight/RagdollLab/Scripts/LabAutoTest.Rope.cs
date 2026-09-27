using System.Collections;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Automated checks for ropes, chains and swings (Queen of the Hill M6) on the [7i] test bed: climbing the
    /// 6 m chain up the tower's face and stepping onto its top, crossing the 8 m gap on the swing, jumping off
    /// the swing with its speed, and whether the plain wall climb takes a 0.5 m thick pole (the question
    /// MECHANICS_TODO asks first).
    /// </summary>
    public partial class LabAutoTest
    {
        static void RopeInput(RagdollPawn pawn, bool grab, float w = 0f, bool jump = false) =>
            pawn.SetInput(new PawnInput { move = Vector3.right * w, aim = Vector3.right, grab = grab, jump = jump });

        IEnumerator RopeChecks()
        {
            var bed = Bed;
            if (bed == null || bed.ClimbRope == null || bed.Swing == null)
            {
                Report("M6 밧줄", false, "시험대나 밧줄이 없음");
                yield break;
            }
            yield return RopeClimb();
            yield return SwingCross(bed.Swing);
            yield return SwingJump(bed.Swing);
            yield return PoleClimb();
        }

        IEnumerator RopeClimb()
        {
            var p = game.tuning.values;
            Vector3 top = QueenHillTestBed.ClimbRopeTop;
            var pawn = Spawn(new Vector3(top.x - 1.4f, 0f, top.z), Vector3.right, "rope-climb");
            yield return Sim(0.5f);
            // Walk up to it with the button held: the hands take it.
            float t = 0f, grabAt = -1f;
            for (; t < 3f && !pawn.OnRope; t += Dt) yield return Sim(Dt, () => RopeInput(pawn, true, 1f));
            grabAt = pawn.OnRope ? t : -1f;
            // Hanging still costs nothing.
            float before = pawn.Stamina;
            yield return Sim(1f, () => RopeInput(pawn, true));
            float still = before - pawn.Stamina;
            bool hung = pawn.OnRope;
            // W to the top, and on up onto the tower.
            before = pawn.Stamina;
            float y0 = pawn.Hips.position.y, climbT = 0f;
            bool wall = false;
            int topOuts = pawn.RopeTopOuts;
            for (; climbT < 12f && pawn.RopeTopOuts == topOuts; climbT += Dt)
                yield return Sim(Dt, () =>
                {
                    RopeInput(pawn, true, 1f);
                    wall |= pawn.Climbing;
                });
            float spent = (before - pawn.Stamina) * p.climbStaminaMax;
            yield return Sim(0.8f, () => RopeInput(pawn, false));
            bool onTop = pawn.RopeTopOuts > topOuts && !pawn.OnRope && pawn.State == PawnState.Active
                         && pawn.Hips.position.y > QueenHillTestBed.RopeTowerTop && pawn.Hips.position.x > QueenHillTestBed.RopeTowerFaceX;
            float rise = QueenHillTestBed.RopeTowerTop - y0;
            Report("M6 밧줄 오르기: 6 m 사슬을 우클릭으로 잡고 W로 올라 꼭대기에 올라섬 (멈추면 스테미나 안 줆)",
                grabAt >= 0f && hung && Mathf.Abs(still) < 0.002f && onTop && !wall,
                $"잡음 {Fmt(grabAt)}, 1초 가만히 {still * 100f:F1}% 줄어듦, 매달림 {hung}, 꼭대기에 올라섬 {onTop} ({climbT:F1}초, 약 {rise:F1} m), "
                + $"스테미나 {spent:F1} 씀, 벽 등반으로 바뀜 {wall}, 끝 ({pawn.Hips.position.x:F1}, {pawn.Hips.position.y:F2})");
            yield return Clear();
        }

        /// <summary>A pawn on the swing's west platform, waiting with the button held until the swing comes
        /// to it and its hands take it.</summary>
        IEnumerator BoardSwing(RopeLine swing, string name, System.Action<RagdollPawn, float> got)
        {
            // Stand ready well before the swing's next visit to the west end.
            if (swing.SecondsToEnd(false, ObstacleClock.Now) < 1.5f) yield return Sim(swing.SecondsToEnd(false, ObstacleClock.Now) + 0.2f);
            var pawn = Spawn(QueenHillTestBed.SwingStart, Vector3.right, name);
            yield return Sim(0.5f);
            float t = 0f;
            for (; t < swing.Period && !pawn.OnRope; t += Dt) yield return Sim(Dt, () => RopeInput(pawn, true));
            got(pawn, pawn.OnRope ? t : -1f);
        }

        IEnumerator SwingCross(RopeLine swing)
        {
            RagdollPawn pawn = null;
            float grabAt = -1f;
            yield return BoardSwing(swing, "swing-cross", (x, at) => { pawn = x; grabAt = at; });
            bool on = pawn.OnRope;
            // Ride it to the far (east) end and let go there.
            float ride = swing.SecondsToEnd(true, ObstacleClock.Now);
            float lowest = float.MaxValue;
            yield return Sim(ride, () =>
            {
                RopeInput(pawn, true);
                lowest = Mathf.Min(lowest, pawn.Hips.position.y);
            });
            bool stayed = pawn.OnRope;
            yield return Sim(1.5f, () => RopeInput(pawn, false));
            Vector3 h = pawn.Hips.position;
            bool across = h.x > QueenHillTestBed.SwingEastEdgeX + 0.3f && h.y > QueenHillTestBed.SwingPlatformTop && pawn.State == PawnState.Active;
            Report("M6 그네: 8 m 틈을 그네로 건넘 (끝에 왔을 때 잡고, 건너편 끝에서 놓기)",
                on && stayed && across,
                $"잡음 {Fmt(grabAt)}, {ride:F1}초 타는 동안 매달림 {stayed} (가장 낮을 때 골반 {lowest:F2} m), "
                + $"놓은 뒤 ({h.x:F1}, {h.y:F2}) = 건너편 가장자리({QueenHillTestBed.SwingEastEdgeX:F0})에서 {h.x - QueenHillTestBed.SwingEastEdgeX:F2} m, 서 있음 {pawn.State == PawnState.Active}");
            yield return Clear();
        }

        IEnumerator SwingJump(RopeLine swing)
        {
            RagdollPawn pawn = null;
            yield return BoardSwing(swing, "swing-jump", (x, _) => pawn = x);
            bool on = pawn.OnRope;
            // The bottom of the swing, the fastest point: a quarter swing before the far end.
            float toBottom = Mathf.Max(0f, swing.SecondsToEnd(true, ObstacleClock.Now) - swing.Period * 0.25f);
            yield return Sim(toBottom, () => RopeInput(pawn, true));
            Vector3 ropeSpeed = swing.PointVelocity(pawn.RopeDown + 0.62f, ObstacleClock.Now);
            int jumps = pawn.RopeJumps;
            RopeInput(pawn, true, 0f, true);
            yield return Sim(Dt);
            yield return Sim(0.05f, () => RopeInput(pawn, true));
            Vector3 v1 = pawn.Hips.linearVelocity;
            yield return Sim(0.25f, () => RopeInput(pawn, true));
            Vector3 v2 = pawn.Hips.linearVelocity;
            bool off = pawn.RopeJumps == jumps + 1 && !pawn.OnRope;
            float want = Mathf.Min(7f, Flat(ropeSpeed).magnitude);
            float carried = Flat(v1).magnitude, kept = Flat(v2).magnitude;
            Report("M6 그네에서 Space: 흔들리던 속도를 이어받아 뛰어내림 (공중에서 안 멈춤)",
                on && off && carried > want * 0.8f && kept > want * 0.75f && v1.y > 1f,
                $"그네 속도 {Flat(ropeSpeed).magnitude:F1} m/s(몸은 7까지) → 놓은 직후 수평 {carried:F1} m/s·위로 {v1.y:F1} m/s, 0.3초 뒤 수평 {kept:F1} m/s");
            yield return Sim(1.5f, () => RopeInput(pawn, false));
            yield return Clear();
        }

        IEnumerator PoleClimb()
        {
            Vector3 pole = QueenHillTestBed.PoleCenter;
            var pawn = Spawn(new Vector3(pole.x - 1.3f, 0f, pole.z), Vector3.right, "pole");
            yield return Sim(0.5f);
            float y0 = pawn.Hips.position.y, peak = y0, held = 0f;
            yield return Sim(5f, () =>
            {
                ClimbInput(pawn, Vector3.right, Vector3.right);
                peak = Mathf.Max(peak, pawn.Hips.position.y);
                if (pawn.Climbing) held += Dt;
            });
            float rose = peak - y0;
            Report("M6 두꺼운 기둥(지름 0.5 m)도 지금의 벽 등반으로 오른다 (사슬 기둥은 벽 등반으로 대신할 수 있음)",
                held > 3f && rose > 3f,
                $"5초 W+우클릭: 벽 등반 {held:F1}초, {rose:F2} m 오름");
            yield return Clear();
        }
    }
}
