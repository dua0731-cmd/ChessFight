using System;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Pawn Rush piece skills' numbers (Docs/Skills/DESIGN.md). They are the previz v0.1 values, kept as
    /// they are (decision D2, 2026-10-07): the only change is that a step, a charge or a leap never makes the
    /// piece slower than it was already going. One copy lives on the skill test bed, so they can be changed in
    /// the Inspector while playing. R153 (기획서 v0.3, 10-16): the pawn's homing second step, the rook's chain pull,
    /// the bishop's guarded trap and the knight's fork leap; the king is out of Pawn Rush (its numbers stay for its code).
    /// </summary>
    [Serializable]
    public class PawnRushSkillParams
    {
        [Header("공통")]
        [Tooltip("시험용: 0보다 크면 모든 기물의 쿨타임을 이 값(초)으로 바꾼다. 0이면 아래 기획 쿨타임을 쓴다")]
        public float testCooldown = 2f;
        [Tooltip("넘어졌다 일어난 뒤 이 시간 동안 스킬에 넘어지지 않고 휘청만 (초). R153 기획서 v0.3: 1.1 → 1.0")]
        public float getUpGuard = 1.0f;
        [Tooltip("기상 보호 중 넘어짐이 낮아진 휘청 (초)")]
        public float guardStagger = 0.3f;
        [Tooltip("부활 직후 스킬 대상이 되지 않는 시간 (초, 기획서 v0.3 §2.5)")]
        public float respawnGuard = 2f;

        [Header("폰 — 첫 두 걸음 (유도형, R153)")]
        [Tooltip("한 걸음 거리 (m). R129 1.5 → 1.1, R153 기획서 v0.3에서 다시 1.5 (승규 님 10-16)")]
        public float pawnStepDistance = 1.5f;
        public float pawnStepSpeed = 7.5f;
        [Tooltip("첫 걸음이 끝나고 두 번째를 받는 시간 (초): 이보다 빨리 누르면 이때 나간다")]
        public float pawnLinkMin = 0.1f;
        public float pawnLinkMax = 0.8f;
        public float pawnCooldown = 5f;
        public float pawnCooldownOneStep = 3.5f;
        [Tooltip("두 번째 걸음의 대상: 조준 방향 좌우 이 각도 안 (도), 이 거리 안 (m)의 넘어진 팀원 또는 서 있는 적")]
        public float pawnSeekAngle = 60f;
        public float pawnSeekRange = 3f;
        [Tooltip("두 번째 걸음: 대상 쪽으로 최대 이만큼 (m), 닿으면 멈춤")]
        public float pawnSeekStep = 1.5f;
        [Tooltip("이 각도까지는 곧게 = 밀치기, 대각 각도까지는 넘어뜨리기 (상대 몸의 앞뒤 선 기준, 도)")]
        public float pawnFrontAngle = 20f;
        public float pawnDiagonalAngle = 80f;
        public float pawnFrontPush = 3.5f;
        public float pawnHitRadius = 0.5f;
        [Tooltip("부축: 일어난 팀원의 기상 보호 (R153: 1.6 → 1.5, 속도 버프는 지움), 폰 쿨 감소")]
        public float helpGuard = 1.5f;
        public float helpCooldownCut = 1.5f;

        [Header("나이트 — 포크 도약 (R153)")]
        public float knightWindup = 0.15f;
        [Tooltip("최고 높이 (m)")]
        public float knightHeight = 1.6f;
        [Tooltip("대상: 조준 방향 좌우 이 각도 안 (도), 땅 위 거리 이 사이 (m), 높이 차 이 안 (m), 가려지지 않은 서 있는 적")]
        public float knightTargetAngle = 60f;
        public float knightTargetMin = 1.5f;
        public float knightTargetMax = 5f;
        public float knightTargetHeight = 2f;
        [Tooltip("체공: 가까울 때와 멀 때 (초)")]
        public float knightFlightNear = 0.8f;
        public float knightFlightFar = 1.14f;
        [Tooltip("체공 중 대상의 지금 위치로 착지점을 옮기는 최대 빠르기 (m/s)")]
        public float knightCorrection = 3f;
        public float knightStompReach = 0.45f;
        [Tooltip("머리 찍은 뒤 위로 튀는 속도와 옆으로 비키는 속도 (m/s)")]
        public float knightStompBounce = 4f;
        public float knightStompHop = 0.3f;
        [Tooltip("포크: 착지 반경 안의 다른 적이 휘청 (m, 초). 빗나가면 없음")]
        public float knightLandRadius = 1.5f;
        public float knightLandStagger = 0.4f;
        public float knightLandRecovery = 0.35f;
        public float knightCooldown = 7f;

        [Header("비숍 — 지키는 X자 덫 (R153)")]
        [Tooltip("최대 거리 (m). v0.1은 9 m(6칸), R74 근거리로 4.5 m(3칸)")]
        public float bishopRange = 4.5f;
        public float bishopLineLength = 4.2f;
        public float bishopHeight = 0.25f;
        [Tooltip("최대 사거리까지 날아가는 시간 (초), 가까우면 그만큼 짧다")]
        public float bishopFlight = 0.3f;
        public float bishopArm = 0.5f;
        public float bishopActive = 6f;
        public int bishopTrips = 2;
        [Tooltip("줄에 걸린 적은 그 자리에 묶여 서 있다 (초). R138 1.6초 → R151 1.0초 (승규 님 10-16) → 10-25 \"묶음을 0.6초 늘려줘\" 1.6초 → \"2초로 늘려줘\" 2.0초 → \"3초로 해줘\" 3.0초")]
        public float bishopBind = 3.0f;
        public float bishopCooldown = 8f;
        [Tooltip("이보다 가파른 바닥에는 깔리지 않는다 (도)")]
        public float bishopMaxSlope = 30f;
        [Tooltip("유지 거리: 비숍이 덫 가운데에서 이보다 멀어지면 줄이 풀려 사라진다 (m). 이 거리를 넘으면 실이 팽팽해지며 깜빡인다 (m)")]
        public float bishopLeash = 6f;
        public float bishopLeashWarn = 5f;

        [Header("비숍 — X자 밧줄 걸기 (10-26)")]
        [Tooltip("10-26 승규 님: \"소드파이트에서 비숍이 구원의 손 할 때 밧줄 그걸로 비숍 손에서 x 자로 상대 배에다가 걸고 wasd나 마우스 휠로 밧줄 탄력을 늘렸다 줄였다\". 켜면 X자 밧줄 걸기, 끄면 예전 X자 덫")]
        public bool bishopRope = true;
        [Tooltip("밧줄이 닿는 거리 (m), 날아가는 빠르기 (m/s), 조준 도움: 조준 방향 좌우 이 각도 안 (도)의 적에게")]
        public float bishopRopeRange = 7f;
        public float bishopRopeSpeed = 16f;
        public float bishopRopeAssist = 12f;
        [Tooltip("걸고 있을 수 있는 시간 (초). 그동안 비숍은 WASD로 걸어 다니며 끌고 다닌다 (질주 X)")]
        public float bishopRopeHold = 6f;
        [Tooltip("밧줄 길이 범위 (m): 휠로 이보다 짧게(팽팽하게) 감거나 길게(느슨하게) 풀 수 없다. 1~6 m → 10-26 승규 님 \"상한선과 하안선 범위가 넓어 많이 좁혀야 돼\" 1.5~3.5 m")]
        public float bishopRopeMin = 1.5f;
        public float bishopRopeMax = 3.5f;
        [Tooltip("10-26 승규 님 \"마우스 휠을 올리면 올릴 수록 느슨해지고 내리면 내릴 수록 줄이 팽팽해져\": 휠 한 칸에 길이가 변하는 만큼 (m, 위 = 길게; 0.4 → 0.25), 길이가 따라가는 빠르기 (m/s)")]
        public float bishopRopeWheel = 0.25f;
        public float bishopRopeReel = 4f;
        [Tooltip("걸리는 순간 밧줄 길이를 상대까지 거리보다 이만큼 짧게 잡는다 (m): 걸자마자 조금 팽팽하게 (10-26 \"조금 더 팽팽하게\")")]
        public float bishopRopeTighten = 0.5f;
        [Tooltip("탄력: 밧줄이 1 m 늘어날 때마다 상대가 끌려오는 빠르기 (m/s), 그 최대 (m/s). 4 → 10-26 \"조금 더 팽팽하게\" 6")]
        public float bishopRopeStiffness = 6f;
        public float bishopRopePull = 6f;
        [Tooltip("비숍도 이 비율만큼 상대 쪽으로 끌려간다 (끌고 걸으면 그만큼 느려진다)")]
        public float bishopRopeBrace = 0.3f;
        [Tooltip("휠로 감아서 만들 수 있는 늘어남 (m): 상대가 버티면 이보다 더 감아 들일 수 없다 (끊어짐보다 작게)")]
        public float bishopRopeHaul = 1.5f;
        [Tooltip("밧줄이 제 길이보다 이만큼 (m) 넘게 늘어나면 끊어진다 (상대가 반대로 달리거나 날아갈 때)")]
        public float bishopRopeBreak = 3f;
        [Tooltip("팽팽할 때 놓으면 늘어난 1 m마다 이 빠르기로 비숍 쪽으로 튕겨 온다 (m/s), 그 최대 (m/s)")]
        public float bishopRopeFling = 3f;
        public float bishopRopeFlingMax = 7f;

        [Header("룩 — 직선 사슬 견인 (R153)")]
        [Tooltip("사슬이 닿는 거리 (m), 위아래 조준 한계 (도). 기획서 6 m → 10-22 승규 님 \"룩 사거리가 좀 많이 짧은 거 같아 많이 늘려줘\" 12 m → \"8m로 줄여줘\" 8 m")]
        public float rookChainRange = 8f;
        public float rookChainPitch = 45f;
        [Tooltip("조준 도움 (10-22 승규 님 \"마우스를 좀 많이 내려야 걸려\"): 조준 방향 좌우 이 각도 안 (도)에 걸 수 있는 기물이 있으면 사슬이 그 가슴으로. 없으면 카메라 기본 각도(내려다보는 각도, 도)일 때 수평으로 나감")]
        public float rookAimAssist = 10f;
        public float rookAimNeutralPitch = 14f;
        [Tooltip("좌클릭 뒤 팔 휘두르기 (초), 사슬 빠르기 (m/s), 빗나가면 되감기 (초)")]
        public float rookSwing = 0.1f;
        public float rookChainSpeed = 32f;   // 24 → 32 with the longer reach (8 m in 0.25 s)
        public float rookRewind = 0.2f;
        [Tooltip("사슬 끝이 사람에게 걸리는 굵기 (m)")]
        public float rookChainRadius = 0.2f;   // (the pieces' chests are only 0.32 m up)
        [Tooltip("적: 이 시간 동안 룩 앞 이 거리까지, 바닥에서 이만큼 뜬 낮은 길로 (초, m). 놓이면 휘청")]
        public float rookPullTime = 0.35f;
        public float rookPullFront = 1.5f;
        public float rookPullHover = 0.3f;
        public float rookPullStagger = 0.3f;
        [Tooltip("이 무게 이상(퀸 2)은 끌려오는 시간이 이 배로 늘어남 (도착 지점은 같음)")]
        public float rookHeavyWeight = 2f;
        public float rookHeavySlow = 1.3f;
        [Tooltip("팀원: 사슬 손잡이가 떠 있는 시간 (초) — 그 안에 우클릭으로 잡기. 잡으면 이 시간 동안 룩 옆 이 거리까지, 최고 이 높이의 포물선 (초, m)")]
        public float rookAllyWindow = 2f;
        public float rookAllyPull = 0.5f;
        public float rookAllyBeside = 1f;
        public float rookAllyArc = 0.6f;
        [Tooltip("시험 더미가 사슬 손잡이를 스스로 잡는 데 걸리는 시간 (초)")]
        public float rookAllyDummyGrab = 0.6f;
        [Tooltip("쿨타임 (초), 팀원이 안 잡아 되감겼을 때 (초)")]
        public float rookCooldown = 8f;
        public float rookUngrabbedCooldown = 4f;
        [Tooltip("퀸 오브 더 힐 룩이 이 기계를 빌려 쓰는 예고 시간 (초): 폰 러쉬 룩은 쓰지 않음")]
        public float rookLock = 0.3f;

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

        [Header("킹 — 왕의 부름 (R146)")]
        [Tooltip("목록에서 고를 수 있는 팀원 수 (최대 2)")]
        public int kingCallMax = 2;
        [Tooltip("불린 팀원이 자기 F로 대답할 시간 (초)")]
        public float kingCallWindow = 3f;
        [Tooltip("대답하면 그 자리에서 떠오르는 시간 (초)")]
        public float kingCallLift = 0.15f;
        [Tooltip("킹 뒤 칸까지 오는 시간 (초): 사인 곡선, 가운데가 가장 빠름")]
        public float kingCallDash = 0.6f;
        [Tooltip("오는 동안 바닥에서 뜨는 높이 (m)")]
        public float kingCallHover = 0.12f;
        [Tooltip("도착 칸: 킹 뒤로 이만큼 (m), 두 명이면 서로 이만큼 떨어진 두 칸 (m). 한 칸 1.5 m")]
        public float kingCallBehind = 1.5f;
        public float kingCallSpread = 1.5f;
        [Tooltip("시험 더미가 스스로 대답하는 데 걸리는 시간 (초)")]
        public float kingCallDummyAnswer = 0.8f;
        [Tooltip("쿨타임 (초): 아직 안 정함, 시험용 2초")]
        public float kingCooldown = 2f;
    }
}
