using System;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Pawn Rush piece skills' numbers (Docs/Skills/DESIGN.md). They are the previz v0.1 values, kept as
    /// they are (decision D2, 2026-10-07): the only change is that a step, a charge or a leap never makes the
    /// piece slower than it was already going. One copy lives on the skill test bed, so they can be changed in
    /// the Inspector while playing. The king's skill is not here yet (D1).
    /// </summary>
    [Serializable]
    public class PawnRushSkillParams
    {
        [Header("공통")]
        [Tooltip("넘어졌다 일어난 뒤 이 시간 동안 스킬에 넘어지지 않고 휘청만 (초)")]
        public float getUpGuard = 1.1f;
        [Tooltip("기상 보호 중 넘어짐이 낮아진 휘청 (초)")]
        public float guardStagger = 0.3f;

        [Header("폰 — 첫 두 걸음")]
        public float pawnStepDistance = 1.5f;
        public float pawnStepSpeed = 7.5f;
        [Tooltip("첫 걸음이 끝나고 두 번째를 받는 시간 (초): 이보다 빨리 누르면 이때 나간다")]
        public float pawnLinkMin = 0.1f;
        public float pawnLinkMax = 0.8f;
        public float pawnCooldown = 5f;
        public float pawnCooldownOneStep = 3.5f;
        [Tooltip("이 각도까지는 곧게 = 밀치기, 대각 각도까지는 넘어뜨리기 (상대 몸의 앞뒤 선 기준, 도)")]
        public float pawnFrontAngle = 20f;
        public float pawnDiagonalAngle = 80f;
        public float pawnFrontPush = 3.5f;
        public float pawnHitRadius = 0.5f;
        [Tooltip("부축: 일어난 팀원의 기상 보호, 둘 다 빨라지는 시간과 배율, 폰 쿨 감소")]
        public float helpGuard = 1.6f;
        public float hasteTime = 1.5f;
        public float hasteScale = 1.15f;
        public float helpCooldownCut = 1.5f;

        [Header("나이트 — 꺾어 도약")]
        public float knightWindup = 0.15f;
        [Tooltip("최고 높이 (골반이 이만큼 오른다, m)")]
        public float knightHeight = 1.6f;
        [Tooltip("평지 비거리 (m): 높이와 함께 출발 속도를 정한다")]
        public float knightDistance = 5.5f;
        [Tooltip("래그돌 몸이 공중에서 잃는 만큼 출발 속도를 더한다 (위·앞 배율). 10-07 실측: 보정 없이 높이 1.53 m · 비거리 5.05 m")]
        public float knightLiftCorrection = 1.03f;
        public float knightCarryCorrection = 1.07f;
        public float knightTurnMax = 90f;
        [Tooltip("이륙 뒤 이 시간부터 꺾을 수 있다 (초)")]
        public float knightTurnAfter = 0.2f;
        public float knightStompReach = 0.45f;
        public float knightStompBounce = 4f;
        public float knightLandRadius = 1.5f;
        public float knightLandStagger = 0.4f;
        public float knightLandRecovery = 0.35f;
        public float knightCooldown = 7f;

        [Header("비숍 — 교차 밧줄")]
        public float bishopRange = 9f;
        public float bishopLineLength = 4.2f;
        public float bishopHeight = 0.25f;
        [Tooltip("최대 사거리까지 날아가는 시간 (초), 가까우면 그만큼 짧다")]
        public float bishopFlight = 0.3f;
        public float bishopArm = 0.5f;
        public float bishopActive = 6f;
        public int bishopTrips = 2;
        public float bishopCooldown = 8f;
        [Tooltip("이보다 가파른 바닥에는 깔리지 않는다 (도)")]
        public float bishopMaxSlope = 30f;

        [Header("룩 — 직선 돌파")]
        [Tooltip("예고 앞부분: 카메라로 방향을 돌릴 수 있다 (초)")]
        public float rookAim = 0.4f;
        [Tooltip("예고 뒷부분: 방향 고정 (초)")]
        public float rookLock = 0.3f;
        public float rookSpeed = 8.5f;
        public float rookTime = 0.7f;
        public int rookMaxHits = 3;
        public float rookSlowPerHit = 0.2f;
        public float rookBarricadeSlow = 0.4f;
        public float rookWallStagger = 0.6f;
        public float rookRecovery = 0.4f;
        public float rookCooldown = 7f;
        [Tooltip("튕겨낼 때 옆·위 속도 (m/s). v0.1에 숫자가 없어 제안값")]
        public float rookSidePush = 5f;
        public float rookUpPush = 1f;
        public float rookWidth = 0.8f;
        [Tooltip("부서진 바리케이드가 다시 생기는 시간 (초)")]
        public float barricadeRegrow = 15f;

        [Header("퀸 — 팔방 밀치기")]
        public float queenWindup = 0.35f;
        public float queenRadius = 3f;
        public float queenInner = 1.5f;
        public float queenInnerPush = 6.5f;
        public float queenOuterPush = 3.8f;
        [Tooltip("위아래로 이만큼 안에 있어야 맞는다 (m)")]
        public float queenHeight = 1.5f;
        public float queenRecovery = 0.45f;
        public float queenCooldown = 8f;
    }
}
