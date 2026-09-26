using System.Collections;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The climb changes of 2026-09-27 (R41): hanging still costs no stamina, jump on the wall is a lunge
    /// up it (stamina spent, still on the wall), and on the wall W/S are up and down and A/D sideways
    /// whatever the camera's angle - only A/D takes the pawn round a bend.
    /// </summary>
    public partial class LabAutoTest
    {
        static void ClimbInput(RagdollPawn pawn, Vector3 move, Vector3 aim, bool jump = false) =>
            pawn.SetInput(new PawnInput { move = move, aim = aim, grab = true, jump = jump });

        /// <summary>A pawn on the 4 m wall, hanging on a little way up, facing +X.</summary>
        IEnumerator StartOnWall(string name, float z, System.Action<RagdollPawn> got)
        {
            var pawn = Spawn(new Vector3(LabLayout.WallFrontX - 1.2f, 0f, z), Vector3.right, name);
            yield return Sim(0.6f);
            yield return Sim(1.2f, () => ClimbInput(pawn, Vector3.right, Vector3.right));
            yield return Sim(0.4f, () => ClimbInput(pawn, Vector3.zero, Vector3.right));
            got(pawn);
        }

        IEnumerator ClimbMoves()
        {
            var p = game.tuning.values;
            RagdollPawn pawn = null;

            // 1. Still on the wall: no drain. Climbing: climbDrainMove a second.
            yield return StartOnWall("climb-still", LabLayout.WallZ[2], x => pawn = x);
            bool on = pawn.Climbing;
            float before = pawn.Stamina;
            yield return Sim(2f, () => ClimbInput(pawn, Vector3.zero, Vector3.right));
            float still = before - pawn.Stamina;
            bool stayed = pawn.Climbing;
            before = pawn.Stamina;
            float y0 = pawn.Hips.position.y;
            yield return Sim(1.5f, () => ClimbInput(pawn, Vector3.right, Vector3.right));
            float moving = (before - pawn.Stamina) * p.climbStaminaMax / 1.5f;
            Report("등반: 매달려 멈춰 있으면 스테미나가 줄지 않고, 움직이면 줄어듦",
                on && stayed && Mathf.Abs(still) < 0.002f && Mathf.Abs(moving - p.climbDrainMove) < 0.1f,
                $"매달림 {on}, 2초 가만히 {still * 100f:F1}% 줄어듦(0이어야 함), 계속 매달림 {stayed}, "
                + $"오를 때 초당 {moving:F2} 줄어듦 (설정 {p.climbDrainMove:F2}, 최대 {p.climbStaminaMax:F0}), 1.5초에 {pawn.Hips.position.y - y0:F2} m 오름");
            yield return Clear();

            // 2. Jump on the wall: a lunge up it, stamina spent, still hanging on.
            yield return StartOnWall("climb-lunge", LabLayout.WallZ[2], x => pawn = x);
            before = pawn.Stamina;
            y0 = pawn.Hips.position.y;
            int lunges = pawn.ClimbLunges;
            ClimbInput(pawn, Vector3.zero, Vector3.right, jump: true);
            yield return Sim(Dt);
            // A second press mid-lunge does nothing.
            ClimbInput(pawn, Vector3.zero, Vector3.right, jump: true);
            yield return Sim(Dt);
            float t = 0f, rose = 0f, riseAt = -1f;
            bool fell = false;
            yield return Sim(0.6f, () =>
            {
                t += Dt;
                ClimbInput(pawn, Vector3.zero, Vector3.right);
                rose = pawn.Hips.position.y - y0;
                if (riseAt < 0f && rose >= p.climbLungeHeight * 0.9f) riseAt = t;
                fell |= !pawn.Climbing;
            });
            float spent = (before - pawn.Stamina) * p.climbStaminaMax;
            int done = pawn.ClimbLunges - lunges;
            // For scale: the same height climbing with W.
            float plain = p.climbLungeHeight / Mathf.Max(0.1f, p.climbSpeed);
            Report("등반: 점프 = 스테미나를 써서 벽에 붙은 채 위로 도약 (W로 오르기보다 빠름)",
                done == 1 && !fell && rose > p.climbLungeHeight * 0.85f && riseAt > 0f && riseAt < plain * 0.6f
                && Mathf.Abs(spent - p.climbLungeStamina) < 0.1f,
                $"도약 {done}회(연타해도 1회), {rose:F2} m 오름 (설정 {p.climbLungeHeight:F1} m), 90%까지 {Fmt(riseAt)} (W로는 약 {plain:F2}s), "
                + $"벽에서 안 떨어짐 {!fell}, 스테미나 {spent:F2} 씀 (설정 {p.climbLungeStamina:F1})");
            yield return Clear();

            // 3. The camera at 50 degrees to the wall: W still goes straight up, D goes sideways.
            yield return StartOnWall("climb-camera", LabLayout.WallZ[2] - 1f, x => pawn = x);
            Vector3 view = Quaternion.AngleAxis(-50f, Vector3.up) * Vector3.right;
            Vector3 camRight = Vector3.Cross(Vector3.up, view);
            Vector3 start = pawn.Hips.position;
            yield return Sim(1.5f, () => ClimbInput(pawn, view, view));
            float sideways = Mathf.Abs(pawn.Hips.position.z - start.z);
            float up = pawn.Hips.position.y - start.y;
            float yaw = Vector3.Angle(Flat(pawn.Facing), Vector3.right);
            Vector3 mid = pawn.Hips.position;
            yield return Sim(1f, () => ClimbInput(pawn, camRight, view));
            float along = Mathf.Abs(pawn.Hips.position.z - mid.z);
            Report("등반: 카메라가 비스듬해도 W는 위로만(옆으로 안 감), A/D는 옆으로",
                up > 1f && sideways < 0.1f && yaw < 5f && along > 0.5f && pawn.Climbing,
                $"카메라 50° 비스듬히 W 1.5초: {up:F2} m 오름, 옆으로 {sideways:F2} m (0.1 미만), 몸 방향 {yaw:F1}° 돌아감, "
                + $"D 1초: 옆으로 {along:F2} m, 매달림 {pawn.Climbing}");
            yield return Clear();
        }
    }
}
