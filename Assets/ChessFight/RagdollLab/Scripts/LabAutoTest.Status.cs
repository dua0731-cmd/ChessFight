using System.Collections;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Automated checks for status effects (Queen of the Hill M13), through IStatusReceiver the way an
    /// ability will reach them: a squash stops a running pawn dead for 1.2 s - no moving, jumping or
    /// grabbing - then it runs again and shrugs off another squash for a second; a squash drops it off the
    /// wall; a stagger takes its footing for 0.4 s without knocking it down; the king's immunity ignores both.
    /// </summary>
    public partial class LabAutoTest
    {
        static readonly Vector3 StatusLane = new Vector3(38f, 0f, -61f);

        IEnumerator StatusChecks()
        {
            yield return SquashRun();
            yield return SquashWall();
            yield return StaggerRun();
            yield return StatusImmunity();
        }

        IEnumerator SquashRun()
        {
            var pawn = Spawn(StatusLane, Vector3.right, "squash");
            var receiver = pawn.GetComponent<IStatusReceiver>();
            yield return Sim(0.5f);
            yield return Sim(1f, () => pawn.SetInput(new PawnInput { move = Vector3.right }));
            float speedBefore = Flat(pawn.Hips.linearVelocity).magnitude;
            bool took = receiver != null && receiver.Squash(1.2f, 1f);
            // Keys held all through: W, a jump, the right button.
            float t = 0f, low = float.MaxValue, high = float.MinValue;
            Vector3 settled = Vector3.zero;
            bool climbed = false;
            yield return Sim(1.15f, () =>
            {
                pawn.SetInput(new PawnInput { move = Vector3.right, grab = true, jump = t > 0.4f && t < 0.4f + Dt });
                t += Dt;
                if (t >= 0.3f && settled == Vector3.zero) settled = pawn.Hips.position;
                if (t >= 0.3f)
                {
                    low = Mathf.Min(low, pawn.Hips.position.y);
                    high = Mathf.Max(high, pawn.Hips.position.y);
                }
                climbed |= pawn.Climbing || pawn.Grabbing;
            });
            float crept = Flat(pawn.Hips.position - settled).magnitude;
            bool stillSquashed = pawn.Squashed;
            bool again = receiver.Squash(1.2f, 1f);           // at 1.15 s: still squashed / immune
            yield return Sim(0.1f, () => pawn.SetInput(new PawnInput { move = Vector3.right }));
            Vector3 from = pawn.Hips.position;
            yield return Sim(0.8f, () => pawn.SetInput(new PawnInput { move = Vector3.right }));
            float ranAfter = Flat(pawn.Hips.position - from).magnitude;
            bool immune = !receiver.Squash(1.2f, 1f);           // 0.9 s into the 1 s immunity
            yield return Sim(0.3f, () => pawn.SetInput(default));
            bool takesAgain = receiver.Squash(1.2f, 1f);
            Report("M13 찌그러짐 1.2초: 달리던 폰이 멈추고 이동·점프·잡기가 안 됨, 끝나면 다시 달림, 그 뒤 1초 면역",
                took && speedBefore > 4f && crept < 0.15f && high - low < 0.08f && !climbed && stillSquashed && !again
                && ranAfter > 1.5f && immune && takesAgain && pawn.State == PawnState.Active,
                $"달리던 속도 {speedBefore:F1} m/s → 찌그러짐 {took}: 0.3초 뒤부터 W를 눌러도 {crept:F2} m, 점프 높이 차 {high - low:F2} m, "
                + $"우클릭 잡기·등반 {climbed}; 끝난 뒤 0.8초 {ranAfter:F1} m 달림; "
                + $"도중 다시 {again}, 끝나고 0.9초 면역 {immune}, 1.2초 뒤 다시 먹힘 {takesAgain}");
            yield return Clear();
        }

        IEnumerator SquashWall()
        {
            RagdollPawn pawn = null;
            yield return StartOnWall("squash-wall", LabLayout.WallZ[2], x => pawn = x);
            bool on = pawn.Climbing;
            float y0 = pawn.Hips.position.y;
            bool took = pawn.GetComponent<IStatusReceiver>().Squash(1.2f, 1f);
            yield return Sim(0.5f, () => ClimbInput(pawn, Vector3.right, Vector3.right));
            bool off = !pawn.Climbing;
            float fell = y0 - pawn.Hips.position.y;
            Report("M13 찌그러짐: 벽에 매달려 있었으면 떨어짐 (우클릭을 계속 눌러도)",
                on && took && off && fell > 0.3f,
                $"매달림 {on} → 찌그러짐 {took}, 0.5초 뒤 벽에서 떨어짐 {off}, {fell:F2} m 내려옴");
            yield return Clear();
        }

        IEnumerator StaggerRun()
        {
            var pawn = Spawn(StatusLane + new Vector3(0f, 0f, 2f), Vector3.right, "stagger");
            yield return Sim(0.5f);
            yield return Sim(1f, () => pawn.SetInput(new PawnInput { move = Vector3.right }));
            float before = Flat(pawn.Hips.linearVelocity).magnitude;
            bool took = pawn.GetComponent<IStatusReceiver>().Stagger(0.4f);
            yield return Sim(0.35f, () => pawn.SetInput(new PawnInput { move = Vector3.right }));
            float during = Flat(pawn.Hips.linearVelocity).magnitude;
            yield return Sim(0.65f, () => pawn.SetInput(new PawnInput { move = Vector3.right }));
            float after = Flat(pawn.Hips.linearVelocity).magnitude;
            Report("M13 비틀 0.4초: 발을 잃었다가(넘어지지 않음) 다시 달림",
                took && before > 4f && during < before * 0.4f && after > 3.5f && pawn.State == PawnState.Active && pawn.Knockdowns == 0,
                $"{before:F1} m/s → 비틀 {took}, 0.35초 {during:F1} m/s, 1초 {after:F1} m/s, 넘어짐 {pawn.Knockdowns}회");
            yield return Clear();
        }

        IEnumerator StatusImmunity()
        {
            var pawn = Spawn(StatusLane + new Vector3(0f, 0f, 4f), Vector3.right, "immune");
            yield return Sim(0.5f);
            pawn.StatusImmune = true;
            var receiver = pawn.GetComponent<IStatusReceiver>();
            bool squash = receiver.Squash(1.2f, 1f), stagger = receiver.Stagger(0.4f);
            Vector3 from = pawn.Hips.position;
            yield return Sim(0.6f, () => pawn.SetInput(new PawnInput { move = Vector3.right }));
            float ran = Flat(pawn.Hips.position - from).magnitude;
            Report("M13 면역(킹의 패시브): 찌그러짐·비틀이 안 먹힘",
                !squash && !stagger && !pawn.Squashed && !pawn.Staggered && ran > 1f,
                $"찌그러짐 {squash}, 비틀 {stagger}, 바로 0.6초 {ran:F1} m 달림");
            yield return Clear();
        }
    }
}
