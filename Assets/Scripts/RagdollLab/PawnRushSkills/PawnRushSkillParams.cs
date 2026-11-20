using System;
using ChessFight.Network;
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
        [Tooltip("이 각도까지는 곧게 = 밀치기, 대각 각도까지는 넘어뜨리기 (상대 몸의 앞뒤 선 기준, 도). R641 승규 님 11-14 \"상대가 살짝 더 넘어지게\": 20 → 14")]
        public float pawnFrontAngle = 14f;
        public float pawnDiagonalAngle = 80f;
        [Tooltip("곧게 맞은 상대를 미는 속도 (m/s, 약 0.6 m 밀림). R641에도 그대로")]
        public float pawnFrontPush = 3.5f;
        [Tooltip("대각으로 넘어뜨릴 때 미는 속도 · 위로 띄우는 속도 (m/s). R641: 3.5 · 1.5 → 4.5 · 2.2 (더 크게 쓰러짐)")]
        public float pawnKnockPush = 4.5f;
        public float pawnKnockLift = 2.2f;
        public float pawnHitRadius = 0.5f;
        [Tooltip("부축: 일어난 팀원의 기상 보호 (R153: 1.6 → 1.5, 속도 버프는 지움), 폰 쿨 감소")]
        public float helpGuard = 1.5f;
        public float helpCooldownCut = 1.5f;

        [Header("나이트 — 꺾어 도약 (R153, R666 전 이름 포크 도약)")]
        public float knightWindup = 0.15f;
        [Tooltip("최고 높이 (m). R646: 1.6 → 2.1 (먼 도약 1.3초가 이 높이 아래로 들어가게: g·t²/8 ≈ 2.07 m)")]
        public float knightHeight = 2.1f;
        [Tooltip("대상: 조준 방향 좌우 이 각도 안 (도), 땅 위 거리 이 사이 (m), 높이 차 이 안 (m), 가려지지 않은 서 있는 적. R646 승규 님 11-14 \"나이트는 1.5~9m로\": 5 → 9 m")]
        public float knightTargetAngle = 60f;
        public float knightTargetMin = 1.5f;
        public float knightTargetMax = 9f;
        public float knightTargetHeight = 2f;
        [Tooltip("체공: 가까울 때와 멀 때 (초). R646: 먼 쪽 1.14 → 1.3 (9 m를 약 6.9 m/s로)")]
        public float knightFlightNear = 0.8f;
        public float knightFlightFar = 1.3f;
        [Tooltip("체공 중 대상의 지금 위치로 착지점을 옮기는 최대 빠르기 (m/s). R646 승규 님 \"상대가 점프나 빠르게 움직여 버리면 무용지물\": 3 → 10 (전력질주 9.6 m/s · 점프도 따라잡음)")]
        public float knightCorrection = 10f;
        [Tooltip("머리 찍기 가로 판정 (m). 잡힌 대상은 여기에 knightLockReach를 더함")]
        public float knightStompReach = 0.45f;
        [Tooltip("R646: 잡힌 대상에게만 더하는 가로 판정 (m, 0.25 → 0.45), 머리 꼭대기 아래 · 위 여유 (m, 0.45 · 0.5 → 0.7 · 0.8): 점프 중 · 착지 순간에도 맞게")]
        public float knightLockReach = 0.45f;
        public float knightLockBelow = 0.7f;
        public float knightLockAbove = 0.8f;
        [Tooltip("R646 승규 님 \"상대가 점프를 해도 맞을 수 있게\": 잡힌 대상의 머리가 나이트 골반에서 이 거리 안에 닿으면 맞음 (m, 점프로 올라와 부딪힌 때), 머리를 못 찍고 착지했어도 대상이 이 가로 거리 안이면 맞음 (m)")]
        public float knightLockContact = 0.8f;
        public float knightLandCatch = 1.0f;
        [Tooltip("R646 승규 님 \"해롱해롱을 좀 더 길게 … 넘어지면 안되고 일어나 있어야 돼\": 머리 찍힌 상대는 넘어지지 않고 선 채로 조작 없이 휘청이는 시간 (초). 예전엔 넘어짐(누워 0.6초 + 일어나기)")]
        public float knightStompDaze = 2.0f;
        [Tooltip("머리 찍은 뒤 위로 튀는 속도와 옆으로 비키는 속도 (m/s)")]
        public float knightStompBounce = 4f;
        public float knightStompHop = 0.3f;
        [Tooltip("포크: 착지 반경 안의 다른 적이 휘청 (m, 초). 빗나가면 없음")]
        public float knightLandRadius = 1.5f;
        public float knightLandStagger = 0.4f;
        public float knightLandRecovery = 0.35f;
        public float knightCooldown = 7f;

        [Header("비숍 — 예전 X자 덫 (bishopRope를 끌 때, R153)")]
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

        [Header("비숍 — 교차 밧줄 = X자 밧줄 걸기 (10-26, R666 이름)")]
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
        [Tooltip("사슬이 닿는 거리 (m), 위아래 조준 한계 (도). 기획서 6 m → 10-22 승규 님 \"룩 사거리가 좀 많이 짧은 거 같아 많이 늘려줘\" 12 m → \"8m로 줄여줘\" 8 m → R646 11-14 \"사슬 거리를 10.5로\" 10.5 m (빠르기는 그대로)")]
        public float rookChainRange = 10.5f;
        public float rookChainPitch = 45f;
        [Tooltip("조준 도움 (10-22 승규 님 \"마우스를 좀 많이 내려야 걸려\"): 조준 방향 좌우 이 각도 안 (도)에 걸 수 있는 기물이 있으면 사슬이 그 가슴으로. 없으면 카메라 기본 각도(내려다보는 각도, 도)일 때 수평으로 나감")]
        public float rookAimAssist = 10f;
        public float rookAimNeutralPitch = 14f;
        [Tooltip("좌클릭 뒤 팔 휘두르기 (초), 사슬 빠르기 (m/s), 빗나가면 되감기 (초)")]
        public float rookSwing = 0.1f;
        public float rookChainSpeed = 32f;   // 24 → 32 with the longer reach (8 m in 0.25 s; R646 kept: 10.5 m in 0.33 s)
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
        [Tooltip("세게 밀리는 안쪽 원 (m). R646 3차 승규 님 11-14 \"세게 밀리는 범위를 살짝 높여주고\": 1.5 → 1.8")]
        public float queenInner = 1.8f;
        [Tooltip("미는 속도: 안쪽 원 · 바깥 (m/s). R646 승규 님 11-14 \"범위 · 높이는 그대로, 미는 힘은 더 세게\": 6.5 · 3.8 → 8.5 · 5.0")]
        public float queenInnerPush = 8.5f;
        public float queenOuterPush = 5.0f;
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

        // ---------------------------------------------------------------- Pawn Rush 2.0 skill plans (JY-skill-lab, 2026-11-19)

        [Header("스킬 구성 (개발자 폰러쉬 스킬트리, K)")]
        [Tooltip("기물마다 칸에 들어 있는 스킬과 스킬 방식 (1 = 칸 하나 · 2 = 하나가 강해짐 · 3 = 칸 셋). 스킬트리(K)에서 바꾼다")]
        public PawnRushLoadout loadout = PawnRushLoadout.Preset(0);
        [Tooltip("2안(하나가 강해짐) 시험: 단계 1 · 2 · 3에서 쿨타임에 곱하는 값, 스킬이 미는 힘에 곱하는 값. 임시 값이다 (스킬마다 고유한 강화는 아직 없다)")]
        public float[] stageCooldown = { 1f, 0.75f, 0.55f };
        public float[] stagePower = { 1f, 1.25f, 1.5f };

        [Header("퀸 오브 더 힐에서 옮긴 스킬 (승규 님 R89~R156의 수치 그대로)")]
        [Tooltip("이 여섯 스킬에 넘어지면 이 시간 동안 누워 있음 (초). 0이면 래그돌 기본")]
        public float hillDownHold = 1f;

        [Header("옮긴 스킬 — 근접 호위 (킹)")]
        [Tooltip("준비 동작 (초)")]
        public float wardWindup = 0.45f;
        [Tooltip("이 반경(m) 안의 팀원이 호위를 받는다. 호위 받는 팀원이 준 사람에게서 이 반경 안에 서 있으면 준 사람이 맞을 것을 대신 받는다")]
        public float wardRadius = 2.5f;
        [Tooltip("위아래로 이만큼 안이면 받는다 (m)")]
        public float wardHeight = 2f;
        [Tooltip("호위 시간 (초)")]
        public float wardTime = 2.9f;
        [Tooltip("호위 중 밀림 배율")]
        public float wardPush = 0.25f;
        [Tooltip("호위 중 넘어질 일이 대신 이만큼 휘청 (초)")]
        public float wardStagger = 0.2f;
        public float wardRecovery = 0.45f;
        public float wardCooldown = 9f;

        [Header("옮긴 스킬 — 팔방 검격 (퀸)")]
        [Tooltip("좌클릭 뒤 베기까지 (초)")]
        public float slashWindup = 0.45f;
        [Tooltip("검격 길이 (m) = 4칸")]
        public float slashLength = 6f;
        public float slashWidth = 1.2f;
        [Tooltip("검기가 끝까지 가는 시간 (초)")]
        public float slashTravel = 0.2f;
        public float slashPush = 5f;
        public float slashLift = 1.5f;
        [Tooltip("검기가 지나가는 바닥보다 이만큼 높은 곳(점프 중인 적)까지 맞는다 (m)")]
        public float slashHeight = 1.2f;
        [Tooltip("검기가 지나가는 바닥보다 이만큼 낮게 선 적까지 맞는다 (m)")]
        public float slashDrop = 0.45f;
        [Tooltip("검기가 타고 오르는 턱의 높이 (m). 이보다 높은 면에서 막힌다. 낭떠러지는 얼마든 따라 내려간다")]
        public float slashClimb = 1.05f;
        public float slashRecovery = 0.6f;
        public float slashCooldown = 6f;

        [Header("옮긴 스킬 — 캐슬링 교대 (룩, 1-나 캐슬링과 같은 코드 · 수치만 다름)")]
        public float swapRange = 9f;
        [Tooltip("조준선에서 이 각도 안의 팀원을 고른다 (도)")]
        public float swapCone = 35f;
        [Tooltip("팀원이 받을 때까지 기다리는 시간 (초). 넘으면 취소, 쿨타임 없음")]
        public float swapWait = 3f;
        public float swapDummyAnswer = 0.4f;
        [Tooltip("두 기물이 날아가 자리를 바꾸는 시간 (초)")]
        public float swapTime = 0.55f;
        public float swapArc = 0.9f;
        public float swapCooldown = 12f;

        [Header("옮긴 스킬 — 교차 공중 포격 (비숍)")]
        [Tooltip("떠오르는 높이 (발이 바닥에서, m)")]
        public float volleyHover = 1.3f;
        public float volleyRise = 0.25f;
        [Tooltip("첫 발을 쏘기 전까지 떠 있는 시간 (초). 다 되면 쏘지 않고 내려옴. 첫 발을 쏜 뒤에는 시간 제한 없음")]
        public float volleyHoverTime = 3f;
        [Tooltip("떠 있는 동안 이동 키로 미끄러지는 속도 (m/s)")]
        public float volleyDrift = 0.7f;
        public float volleyDriftEase = 0.9f;
        [Tooltip("한 번 떠올라 쏠 수 있는 견제탄 수 (1~3)")]
        public int volleyShots = 2;
        public float volleyRange = 9f;
        [Tooltip("견제탄이 날아가는 시간 (초)")]
        public float volleyFlight = 0.65f;
        [Tooltip("X자 파동 한쪽 팔 길이 (m)와 폭 (m)")]
        public float volleyArm = 1.6f;
        public float volleyArmWidth = 0.6f;
        [Tooltip("가운데(조준점)에서 이 반경 안은 맞는다 (m)")]
        public float volleyCenter = 0.75f;
        public float volleyPush = 6.5f;
        public float volleyLift = 2.8f;
        public float volleyCooldown = 9f;

        [Header("옮긴 스킬 — 도약 압착 (나이트)")]
        public float pressRange = 6f;
        [Tooltip("지금 바닥보다 이만큼 높은 곳까지만 뛰어오른다 (m)")]
        public float pressMaxRise = 1.05f;
        [Tooltip("호의 높이 (m)와 호를 나는 중력 배율 (클수록 짧고 굵게)")]
        public float pressArc = 1.6f;
        public float pressGravity = 1.6f;
        public float pressWindup = 0.3f;
        [Tooltip("착지 원 반경 (m)")]
        public float pressRadius = 1.3f;
        [Tooltip("조준점에서 이 거리(m) 안에 적이 있으면 그 적의 머리 위로 자동 조준")]
        public float pressSnap = 1.05f;
        [Tooltip("납작해지는 시간 (초)")]
        public float pressFlatten = 0.7f;
        [Tooltip("납작해진 적이 바깥으로 미끄러지는 속도 (m/s)와 시간 (초)")]
        public float pressSlide = 2.6f;
        public float pressSlideTime = 0.35f;
        public float pressRecovery = 0.3f;
        public float pressCooldown = 7f;

        [Header("옮긴 스킬 — 비집고 돌파 (폰)")]
        public float squeezeCrouch = 0.08f;
        public float squeezeDistance = 1.8f;
        public float squeezeSpeed = 8f;
        public float squeezeRise = 0.22f;
        [Tooltip("지나가며 부딪힌 기물을 옆으로 비키게 미는 속도 (m/s)")]
        public float squeezeNudge = 1.4f;
        public float squeezeCooldown = 5f;

        [Header("1-나 폰 — 앙파상 (지나치는 놈 발 걸기)")]
        [Tooltip("적이 이 거리 안을 스쳐 지나가면 (m), 서로 지나치는 빠르기가 이 이상일 때 (m/s) 걸 수 있다")]
        public float passRange = 2f;
        public float passSpeed = 1.2f;
        [Tooltip("지나친 뒤 걸 수 있는 시간 (초)")]
        public float passWindow = 1f;
        [Tooltip("뛰어드는 최대 거리 (m)와 빠르기 (m/s), 닿았다고 보는 거리 (m)")]
        public float passLunge = 3.5f;
        public float passLungeSpeed = 10f;
        public float passReach = 0.75f;
        [Tooltip("걸린 적: 달리던 쪽으로 적어도 이 빠르기로 미끄러진다 (m/s), 위로 뜨는 빠르기 (m/s)")]
        public float passTripSpeed = 4.5f;
        public float passTripLift = 1.6f;
        public float passCooldown = 4f;

        [Header("1-나 나이트 — 포크 (두 갈래 차기)")]
        public float forkWindup = 0.2f;
        [Tooltip("도약: 앞으로 이만큼 (m, 2칸), 체공 (초), 최고 높이 (m)")]
        public float forkLeap = 3f;
        public float forkFlight = 0.55f;
        public float forkHeight = 1.2f;
        [Tooltip("두 자리: 착지점에서 진행 방향 좌우 이 각도 (도)로 이만큼 앞 (m), 자리 반경 (m)")]
        public float forkAngle = 50f;
        public float forkAhead = 1.6f;
        public float forkRadius = 1.1f;
        [Tooltip("둘을 한꺼번에 맞혔을 때: 바깥으로 미는 빠르기 · 위로 (m/s), 넘어뜨림. 한 자리만 맞혔을 때: 밀기만 (m/s)")]
        public float forkPush = 6.5f;
        public float forkLift = 2.2f;
        public float forkSinglePush = 3.5f;
        public float forkRecovery = 0.35f;
        public float forkCooldown = 7f;

        [Header("1-나 비숍 — 핀 (꿰어 묶기)")]
        [Tooltip("빛줄이 닿는 거리 (m, 6칸), 굵기 (m), 조준 도움 각도 (도)")]
        public float pinRange = 9f;
        public float pinRadius = 0.3f;
        public float pinAssist = 10f;
        [Tooltip("처음 맞은 적 뒤로 이 거리 안 (m)의 다른 적이나 벽 · 기둥에 꽂히면 묶인다")]
        public float pinBehind = 4f;
        [Tooltip("묶는 줄 길이 (m), 묶인 시간 (초)")]
        public float pinRest = 2f;
        public float pinSeconds = 5f;
        [Tooltip("줄이 1 m 늘어날 때마다 끌려가는 빠르기 (m/s), 그 최대 (m/s), 이만큼 넘게 늘어나면 끊어짐 (m)")]
        public float pinStiffness = 6f;
        public float pinPull = 6f;
        public float pinBreak = 3.5f;
        [Tooltip("뒤에 아무것도 없을 때 맞은 적이 휘청이는 시간 (초)")]
        public float pinMissStagger = 0.5f;
        public float pinRecovery = 0.3f;
        public float pinCooldown = 8f;

        [Header("1-나 룩 — 캐슬링 (자리 바꾸기)")]
        [Tooltip("팀원까지 거리 (m, 8칸), 조준 도움 각도 (도)")]
        public float castleRange = 12f;
        public float castleAssist = 15f;
        [Tooltip("팀원이 자기 스킬 키로 받을 시간 (초), 시험 더미가 스스로 받는 데 걸리는 시간 (초)")]
        public float castleWindow = 2f;
        public float castleDummyAnswer = 0.6f;
        [Tooltip("자리 바꾸는 비행 시간 (초), 호의 높이 (m), 서로 비켜 가는 옆 너비 (m)")]
        public float castleFlight = 0.6f;
        public float castleArc = 1.5f;
        public float castleSide = 0.6f;
        [Tooltip("변형 시험: 적과도 바꾼다 (동의 없음). 예고 (초) 동안 둘 사이에 선이 보이고, 끝에 적이 공중이거나 가려지면 실패")]
        public bool castleEnemies = false;
        public float castleEnemyWindup = 0.8f;
        public float castleEnemyStagger = 0.4f;
        public float castleCooldown = 12f;
        [Tooltip("팀원이 안 받았을 때 쿨타임 (초)")]
        public float castleRefusedCooldown = 4f;

        [Header("1-나 퀸 — 체크!")]
        [Tooltip("적까지 거리 (m, 8칸. 후보 문서 Q-기1: 9 → 12), 조준 도움 각도 (도)")]
        public float checkRange = 12f;
        public float checkAssist = 20f;
        [Tooltip("칸의 반경 (m): 체크 순간 적이 서 있던 자리. 이 시간 (초) 안에 벗어나지 않으면 맞는다")]
        public float checkSquare = 1.5f;
        public float checkSeconds = 1.5f;
        [Tooltip("맞으면: 퀸 반대쪽으로 미는 빠르기 · 위로 (m/s), 넘어뜨림")]
        public float checkPush = 7.5f;
        public float checkLift = 2.5f;
        [Tooltip("막기: 퀸과 그 적 사이, 줄에서 이 거리 안 (m)에 선 적 팀 기물이 대신 맞는다")]
        public float checkBlock = 0.6f;
        public float checkRecovery = 0.3f;
        public float checkCooldown = 8f;
        [Tooltip("후보 문서 Q-기1: 확정하는 순간 퀸이 적 쪽으로 미끄러지는 거리 (m), 시간 (초)")]
        public float checkLunge = 1.5f, checkLungeTime = 0.25f;
        [Tooltip("후보 문서 Q-기1: 칸이 터질 때 멈칫 (초), 퀸이 튕겨 물러나는 거리 (m)")]
        public float checkStop = 0.08f, checkBounce = 0.6f;
        [Tooltip("후보 문서 Q-기1: 적이 칸을 비켜 나가면 이 시간 (초) 안에 쿨 없이 한 번 더 걸 수 있다 (체크를 잇는다, 한 번만)")]
        public float checkAgainWindow = 2f;

        [Header("기획서 A 묶음 폰 — 터치 무브 · 자두브 (P5)")]
        [Tooltip("손을 내밀기까지 (초), 손이 닿는 거리 (m), 조준에서 좌우 각도 (도)")]
        public float touchWindup = 0.25f;
        public float touchReach = 1.5f;
        public float touchCone = 55f;
        [Tooltip("맞은 적이 멈출 수 없는 시간 (초)")]
        public float touchSeconds = 2f;
        [Tooltip("자두브: 건드린 팀원이 바로 선 뒤 스킬에 안 넘어지는 시간 (초, 기상 보호)")]
        public float touchGuard = 1f;
        public float touchRecovery = 0.25f;
        [Tooltip("쿨타임 (초), 아무도 못 건드렸을 때 쿨타임 (초)")]
        public float touchCooldown = 6f;
        public float touchMissCooldown = 2.5f;

        [Header("기획서 A 묶음 나이트 — 뒷발차기 (N4)")]
        [Tooltip("등을 돌리는 예고 (초), 발이 닿는 거리 (m)와 폭 (m)")]
        public float kickWindup = 0.35f;
        public float kickReach = 2f;
        public float kickWidth = 1.2f;
        [Tooltip("미는 빠르기 (m/s, 넘어뜨리지 않음), 손이 저려 아무것도 못 잡는 시간 (초)")]
        public float kickPush = 3.5f;
        public float kickNumb = 3f;
        public float kickRecovery = 0.3f;
        public float kickCooldown = 6f;

        [Header("기획서 A 묶음 비숍 — 반대색 비숍 (B1)")]
        public float ghostWindup = 0.15f;
        [Tooltip("적의 몸과 스킬이 통과하는 시간 (초)")]
        public float ghostSeconds = 3f;
        [Tooltip("풀릴 때 이 거리 (m) 안에 겹쳐 있던 적과 서로 튕기는 빠르기 (m/s)")]
        public float ghostPopRadius = 0.7f;
        public float ghostPop = 4f;
        public float ghostRecovery = 0.1f;
        public float ghostCooldown = 12f;

        [Header("기획서 A 묶음 룩 — 뒤에서 밀기 (R1)")]
        [Tooltip("바로 앞 기물까지 거리 (m), 조준에서 좌우 각도 (도)")]
        public float pushReach = 1.8f;
        public float pushCone = 50f;
        [Tooltip("받치는 예고 (초). 이 사이에 점프하거나 멀어지면 빠진다")]
        public float pushWindup = 0.5f;
        [Tooltip("쏘는 거리 (m, 여덟 걸음쯤), 그 거리를 나는 시간 (초), 호의 높이 (m)")]
        public float pushDistance = 6f;
        public float pushFlight = 0.55f;
        public float pushArc = 0.5f;
        [Tooltip("날아가는 길에 있던 적을 옆으로 미는 빠르기 (m/s)")]
        public float pushBump = 4f;
        [Tooltip("켜면 벽 앞에서 멈추지 않고 끝까지 쏜다 (벽에 부딪힌다). 틈 앞에서는 늘 멈춘다")]
        public bool pushIntoWalls = false;
        public float pushRecovery = 0.3f;
        [Tooltip("쿨타임 (초), 아무도 못 쐈을 때 쿨타임 (초)")]
        public float pushCooldown = 9f;
        public float pushMissCooldown = 3f;

        [Header("기획서 A 묶음 퀸 — 두 마리 토끼 (Q4)")]
        [Tooltip("두 적까지 거리 (m), 조준에서 좌우 각도 (도), 두 선이 보이는 예고 (초)")]
        public float hareRange = 8f;
        public float hareCone = 40f;
        public float hareWindup = 0.45f;
        [Tooltip("고무줄 길이 (m), 묶인 시간 (초)")]
        public float hareRest = 3f;
        public float hareSeconds = 4f;
        [Tooltip("줄이 1 m 늘어날 때마다 끌려가는 빠르기 (m/s), 그 최대 (m/s)")]
        public float hareStiffness = 7f;
        public float harePull = 7f;
        [Tooltip("줄이 이만큼 (m) 넘게 늘어나면 튕겨 서로에게 날아간다: 빠르기 (m/s), 휘청 (초), 다음 튕김까지 (초)")]
        public float hareSnap = 1f;
        public float hareSnapPush = 5f;
        public float hareSnapStagger = 0.4f;
        public float hareSnapEvery = 1.2f;
        public float hareRecovery = 0.3f;
        [Tooltip("쿨타임 (초), 둘을 못 묶었을 때 쿨타임 (초)")]
        public float hareCooldown = 9f;
        public float hareMissCooldown = 3f;

        // The kits by piece, the basic row (Docs/Skills/PAWN_RUSH_SKILL_KITS.md §6; RagdollPawn.KitSkills.cs).
        [Header("기물별 한 벌 · 기본기 — 나이트 ㄱ자 도약 (10-07 첫 모양의 수치)")]
        [Tooltip("뒷발로 서는 준비 (초). 공중에서 쓰면 준비 없이 바로 뛴다")]
        public float leapWindup = 0.15f;
        [Tooltip("도약 높이 (m), 거리 (m)")]
        public float leapHeight = 1.6f;
        public float leapDistance = 4.5f;
        [Tooltip("래그돌이 공중에서 잃는 만큼 보태는 값: 위로, 앞으로 (10-07에 잰 값)")]
        public float leapLift = 1.03f;
        public float leapCarry = 1.07f;
        [Tooltip("공중에서 한 번 더 누르기: 뜬 뒤 이만큼 (초) 지나야 됨, 꺾을 수 있는 최대 각도 (도), 새로 차고 나가는 빠르기 (m/s), 다시 뜨는 빠르기 (m/s)")]
        public float leapTurnAfter = 0.2f;
        public float leapTurnMax = 90f;
        public float leapTurnSpeed = 6.2f;
        public float leapTurnLift = 4f;
        [Tooltip("머리 밟기: 발이 머리에서 이만큼 (m) 안, 밟힌 적 휘청 (초), 다시 튀어 오르는 빠르기 (m/s)")]
        public float leapStompReach = 0.45f;
        public float leapStompStagger = 0.4f;
        public float leapStompBounce = 4f;
        [Tooltip("이만큼 (초) 지나도 못 내려오면 (낭떠러지로 뜀) 스킬은 끝난다")]
        public float leapLongest = 3f;
        [Tooltip("착지 후딜 (초), 쿨타임 (초)")]
        public float leapRecovery = 0.35f;
        public float leapCooldown = 7f;
        [Tooltip("후보 문서 N-기1: 머리를 밟을 때 멈칫 (초)")]
        public float leapStompStop = 0.08f;
        [Tooltip("후보 문서 N-기1: 착지 구르기 - 내려오다 땅에 닿기 이만큼 (초) 전부터, 닿은 뒤 이만큼 (초)까지 점프를 누르면 후딜 없이 구른다")]
        public float leapRollEarly = 0.15f, leapRollLate = 0.12f;
        [Tooltip("착지 구르기 거리 (m), 빠르기 (m/s). 앞에 바닥이 없으면 그만큼 짧게")]
        public float leapRollDistance = 2f, leapRollSpeed = 7f;
        [Tooltip("후보 문서 N-기1: 착지할 곳 아래 이 깊이 (m) 안에 바닥이 없으면 '블런더'")]
        public float leapBlunderDepth = 6f;

        [Header("기물별 한 벌 · 기본기 — 비숍 피앙케토")]
        [Tooltip("자리를 잡자마자의 사거리 (m), 다 겨눴을 때의 사거리 (m), 다 겨누는 데 걸리는 시간 (초)")]
        public float fianNear = 8f;
        public float fianFar = 25f;
        public float fianCharge = 1f;
        [Tooltip("이만큼 (초) 넘게 안 쏘면 자리에서 일어난다 (쿨타임 없음)")]
        public float fianHold = 5f;
        [Tooltip("조준 보정: 위에서 본 각도 (도), 그 적이 위아래로 이만큼 (m) 안이면 줄이 그 적에게 곧게 간다 (계단 위아래). 줄의 굵기 (m: 허리가 줄에서 이만큼 안이면 맞음)")]
        public float fianAssist = 6f;
        public float fianTall = 8f;
        public float fianRadius = 0.5f;
        [Tooltip("켜면 팀원도 줄을 막는다 (체스의 비숍은 자기 편 말에 막힌다)")]
        public bool fianAlliesBlock = true;
        [Tooltip("맞은 적의 달리기 빠르기 (배, 전력질주도 안 됨), 그 시간 (초)")]
        public float fianSlow = 0.6f;
        public float fianSlowSeconds = 2f;
        [Tooltip("쏜 뒤 후딜 (초), 쿨타임 (초)")]
        public float fianRecovery = 0.25f;
        public float fianCooldown = 6f;

        [Header("기물별 한 벌 · 기본기 — 킹 왕의 한 걸음")]
        [Tooltip("준비 (초), 걸음 빠르기 (m/s), 거리 (m, 한 칸), 후딜 (초)")]
        public float stepWindup = 0.05f;
        public float stepSpeed = 7.5f;
        public float stepDistance = 1.5f;
        public float stepRecovery = 0.15f;
        [Tooltip("쿨타임 (초), 공중에서 쓰면 더해지는 쿨 (초), 잡힌 손을 뿌리치거나 넘어진 채 쓰면 더해지는 쿨 (초)")]
        public float stepCooldown = 4f;
        public float stepAirExtra = 1f;
        public float stepDownExtra = 1.5f;
        [Tooltip("넘어진 채 쓰기: 넘어지고 이만큼 (초) 뒤부터, 일어난 뒤 기상 보호 (초)")]
        public float stepDownAfter = 0.35f;
        public float stepGuard = 1.1f;
        [Tooltip("뿌리친 손이 다시 잡기까지 (초)")]
        public float stepShakeOff = 0.8f;
        [Tooltip("후보 문서 K-기1: 디딜 때 '쿵' 멈칫 (초); 걸음이 적에 닿으면 그 적을 미는 거리 (m, 킹은 그 자리에 섬)")]
        public float stepStompStop = 0.05f, stepShove = 3f;
        [Tooltip("후보 문서 K-기1: 걸음이 끝나고 이 시간 (초) 안의 점프 · 다이빙은 걸음 쪽으로 이만큼 (m/s) 더 나감")]
        public float stepChainWindow = 0.3f, stepChainBoost = 3f;
        [Tooltip("후보 문서 K-기1: 넘어진 채 쓰면 이동 키 쪽으로 굴러 일어나는 빠르기 (m/s)")]
        public float stepRollSpeed = 3.5f;

        [Header("기물별 한 벌 · 기본기 — 룩 캐슬링의 덤 (킹과 룩이 바꿀 때)")]
        [Tooltip("킹과 룩이 자리를 바꾸면 내려앉은 뒤 둘 다 이만큼 (초) 넘어지지 않는다 (기상 보호와 같은 효과)")]
        public float castleRoyalGuard = 2f;

        [Header("손맛 공통 장치 (후보 문서 1.3의 2 · 4 · 6 · 8번, 11-20. 5번 맞히면 쿨 돌려받기는 뺐다)")]
        [Tooltip("끄면 아래 장치가 모두 꺼진다 (켜고 끄며 비교). 스킬 시험 창의 '손맛 장치' 버튼과 같다")]
        public bool feel = true;
        [Tooltip("멈칫 (초): 약한 CC (밀침 · 휘청 · 발 무거움) · 하드 CC (넘어뜨림) · 궁극기. 맞힌 기물과 맞은 기물의 화면에서만")]
        public float feelStopWeak = 0.06f, feelStopHard = 0.10f, feelStopUltimate = 0.15f;
        [Tooltip("싼 기물이 비싼 적을 맞히면 값 차이 1점마다 더 멈칫 (초; 폰 1 · 나이트 3 · 비숍 3 · 룩 5 · 퀸 9, 킹은 뺌), 멈칫의 상한 (초)")]
        public float feelStopPerValue = 0.01f, feelStopMax = 0.20f;
        [Tooltip("한 번에 여럿을 맞히면 하나 더마다 더 멈칫 (초, 셋까지)")]
        public float feelStopPerExtra = 0.02f;
        [Tooltip("이 시간 안에 또 맞히면 멈칫이 절반 (초)")]
        public float feelStopRepeat = 0.3f;
        [Tooltip("반동: 쏘는 스킬(피앙케토 · 체크!)이 쏜 기물을 뒤로 미는 거리 (m)")]
        public float feelKickShot = 0.4f;
        [Tooltip("반동: 몸으로 맞힌 폰이 되튀는 거리 (m). 나이트는 원래 머리를 밟고 위로 튄다")]
        public float feelKickBounce = 0.5f;
        [Tooltip("반동: 적에게 가던 몸 스킬(폰의 두 번째 걸음)이 헛치면 가던 쪽으로 더 쏠리는 거리 (m)")]
        public float feelKickMiss = 1f;
        [Tooltip("반동이 걸리는 시간 (초, 처음이 빠르고 점점 멎는다), 가장자리 몇 m 앞에서 멈추는지")]
        public float feelKickTime = 0.15f, feelKickEdge = 0.3f;
        [Tooltip("미리 누르기: 쿨이 끝나기 이만큼 안에, 또는 휘청이는 중에 누른 키는 쓸 수 있게 되는 첫 순간에 나간다 (초)")]
        public float feelBuffer = 0.4f;
        [Tooltip("미리 누르기: 넘어져 있거나 일어나는 중에 누른 키를 기다려 주는 시간 (초)")]
        public float feelBufferDown = 2f;

        [Header("후보 문서 1안 · 폰 첫 두 걸음 (꺾기, P-기1)")]
        [Tooltip("첫 걸음 뒤 둘째 걸음을 받는 시간 (초), 이동 키로 꺾는 최대 각도 (도), 한 걸음만 쓰면 쿨 (초). 거리 · 빠르기 · 밀기는 위의 폰 첫 두 걸음 수치")]
        public float steerLinkMax = 0.5f, steerTurn = 45f, steerOneStepCooldown = 3f;

        [Header("후보 문서 1안 · 비숍 긴 대각선 저격 (B-중7)")]
        [Tooltip("줄 길이: 모으기 전 (m), 다 모으면 (m), 다 모으는 시간 (초)")]
        public float snipeNear = 10f, snipeFar = 30f, snipeCharge = 1f;
        [Tooltip("이만큼 (초) 안 쏘면 스스로 일어남 (모으는 중이면 쏨)")]
        public float snipeHold = 5f;
        [Tooltip("조준 도움 각도 (도, 위에서 봄), 위아래로 노릴 수 있는 높이 (m), 줄의 반지름 (m)")]
        public float snipeAssist = 6f, snipeTall = 8f, snipeRadius = 0.45f;
        [Tooltip("멀수록 세다: 이 거리 (m) 안은 휘청, 이 거리 (m)까지는 넘어짐, 그 너머는 넘어지며 날아감")]
        public float snipeStaggerUpTo = 12f, snipeKnockUpTo = 22f;
        [Tooltip("휘청 (초); 넘어뜨릴 때 미는 빠르기 · 위로 (m/s); 멀리서 날릴 때 미는 빠르기 · 위로 (m/s, 약 2 m)")]
        public float snipeStagger = 0.4f, snipePush = 4.5f, snipeLift = 1.5f, snipeFlyPush = 7f, snipeFlyLift = 3.5f;
        [Tooltip("쏜 반동: 비숍이 뒤로 미끄러지는 거리 (m), 시간 (초), 그 뒤 엉덩방아 (초, 못 움직임). 중급기라 가장자리에서 안 멈춘다")]
        public float snipeRecoil = 2f, snipeRecoilTime = 0.3f, snipeSit = 0.4f;
        [Tooltip("모은 지 이만큼 (초)부터 표적 발밑에 빛점이 보인다 (표적에게 알림)")]
        public float snipeWarnAfter = 0.6f;
        public float snipeCooldown = 18f;
        [Tooltip("팀원이 줄을 막는다")]
        public bool snipeAlliesBlock = true;

        [Header("후보 문서 1안 · 룩 뒤에서 잡기 (R-중2, 사슬 견인의 고침. 사거리 · 조준 · 휘청은 위의 룩 수치)")]
        [Tooltip("끌어오는 빠르기 (m/s, 멀수록 오래 걸림), 끌려온 적이 내려서는 거리 (m, 룩 앞. 가까워서 우클릭으로 바로 붙잡힘)")]
        public float behindPullSpeed = 12f, behindFront = 0.9f;
        [Tooltip("룩이 끌려갈 때: 무거운 적 앞 몇 m에서, 벽 · 턱 몇 m 앞에서 멈추는지")]
        public float behindHeavyGap = 0.9f, behindWallGap = 0.5f;
        [Tooltip("걸린 뒤 좌클릭을 누르고 있으면 감는 빠르기 배수 (기획은 마우스 휠인데 휠은 네트워크로 안 가서)")]
        public float behindReel = 1.8f;
        [Tooltip("끌려가는 적이 이만큼 좌클릭하면 사슬을 털어낸다 (절반만 끌려옴)")]
        public int behindShake = 3;
        public float behindCooldown = 16f;

        [Header("후보 문서 1안 · 킹 왕의 부름 (K-중1, 지금 왕의 부름의 고침. 목록 · 대답 시간 · 오는 길은 위의 킹 수치)")]
        [Tooltip("불린 팀원이 내려서는 자리: 킹 앞 이 거리 (m, 킹이 부를 때 보던 쪽). 좌우 간격은 kingCallSpread")]
        public float summonFront = 1.5f;
        [Tooltip("부를 때 나팔 반동으로 킹이 물러나는 거리 (m)")]
        public float summonRecoil = 0.5f;
        [Tooltip("팀원이 내려서는 순간 둘레 이 반경 (m)의 적이 밀리는 빠르기 (m/s, 넘어지지 않음)")]
        public float summonPushRadius = 2f, summonShove = 4f;
        [Tooltip("내려선 뒤 이 시간 (초) 안에 키를 한 번 더 누르면 킹과 내려선 팀원이 함께 이만큼 (m) 밀고 나감, 걸리는 시간 (초)")]
        public float summonPushWindow = 0.6f, summonPushDistance = 3f, summonPushTime = 0.45f;
        public float summonCooldown = 20f;

        [Header("후보 문서 3안 기본기 · 비숍 옆구리 사격 (B-기6)")]
        [Tooltip("빛줄기 길이 (m), 조준 도움 (도), 위아래 (m), 반지름 (m); 키를 누르고 나가기까지 (초)")]
        public float flankRange = 12f, flankAssist = 5f, flankTall = 3f, flankRadius = 0.4f, flankWindup = 0.15f;
        [Tooltip("맞은 적이 줄 방향으로 밀리는 거리 (m), 시간 (초); 비숍이 뒤로 밀리는 거리 (m, 가장자리에서 안 멈춤)")]
        public float flankPush = 2f, flankPushTime = 0.25f, flankRecoil = 1f;
        [Tooltip("블런더: 이 빠르기 (m/s) 넘게 달리던 적을 그 달리는 쪽 이 각도 (도) 안으로 밀면 앞으로 밀어 준 것")]
        public float flankBlunderSpeed = 2f, flankBlunderAngle = 35f;
        [Tooltip("쏜 줄이 보이는 시간 (초), 쿨 (초)")]
        public float flankShow = 0.25f, flankCooldown = 7f;

        [Header("후보 문서 3안 기본기 · 룩 넘어오는 캐슬링 (R-기2)")]
        [Tooltip("팀원을 찾는 거리 (m), 앞쪽 각도 (도), 팀원 너머 내려서는 거리 (m)")]
        public float overRange = 6f, overAngle = 75f, overBeyond = 1.5f;
        [Tooltip("웅크림 (초), 나는 빠르기 (m/s), 포물선 높이 (m)")]
        public float overCrouch = 0.15f, overSpeed = 9f, overArc = 1.3f;
        [Tooltip("내려선 둘레 이 반경 (m)의 적이 밀리는 빠르기 (m/s); 내려서는 순간 키를 누르면 이 반경 (m)까지 1.5배로")]
        public float overRadius = 2f, overShove = 4f, overBigRadius = 3f;
        [Tooltip("내려서는 순간의 창: 땅에 닿기 이만큼 (초) 전부터, 닿은 뒤 이만큼 (초)까지")]
        public float overPressEarly = 0.15f, overPressLate = 0.15f;
        [Tooltip("넘어간 팀원이 넘어질 일이 휘청이 되는 시간 (초), 후딜 (초), 쿨 (초)")]
        public float overGuard = 1.5f, overRecovery = 0.2f, overCooldown = 10f;

        [Header("후보 문서 3안 기본기 · 퀸 스콜라 찌르기 (Q-기2)")]
        [Tooltip("옆걸음: 거리 (m), 시간 (초), 조준에서 벌어지는 각도 (도, 비스듬히 앞옆); 첫 키부터 찌르기 키를 받는 시간 (초)")]
        public float scholarSide = 2.5f, scholarSideTime = 0.25f, scholarSideAngle = 60f, scholarWindow = 0.6f;
        [Tooltip("찌르기: 거리 (m), 빠르기 (m/s), 닿는 반지름 (m)")]
        public float scholarReach = 5f, scholarSpeed = 14f, scholarRadius = 0.5f;
        [Tooltip("적이 퀸을 마주 본다고 치는 각도 (도): 그러면 밀리기만 하고 퀸이 튕겨 휘청 (초), 튕기는 거리 (m), 미는 빠르기 (m/s)")]
        public float scholarFacing = 60f, scholarBlockedStagger = 0.6f, scholarBounce = 0.8f, scholarShove = 3f;
        [Tooltip("등 · 옆을 찔린 적: 앞으로 밀리는 거리 (m), 휘청 (초). 서 있었고 (이 빠르기 m/s 아래) 이 거리 (m) 안에 그 적의 팀원이 없으면 넘어짐 (미는 빠르기 · 위로 m/s)")]
        public float scholarPush = 3f, scholarStagger = 0.5f, scholarStillSpeed = 1.5f, scholarMateGap = 4f, scholarKnockPush = 5f, scholarKnockLift = 2f;
        [Tooltip("후딜 (초), 쿨 (초), 옆걸음만 쓴 쿨 (초)")]
        public float scholarRecovery = 0.25f, scholarCooldown = 9f, scholarStepCooldown = 4f;
    }
}
