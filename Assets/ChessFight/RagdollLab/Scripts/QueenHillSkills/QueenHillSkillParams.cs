using System;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Queen of the Hill piece skills' numbers (design doc §7 "승격 쟁탈전과 스킬 6개", reviewed 2026-10-08; R89 picks
    /// 1.B 2.A 3.B 4.B 5.B 6.B from the previz page). Test start values, not balance: the skill test bed keeps one copy so
    /// they can be changed in the Inspector while playing. Only the Queen of the Hill skill test scene hands these to the
    /// pawns (<see cref="RagdollPawn.QueenHillSkills"/>); every other scene runs as before.
    /// </summary>
    [Serializable]
    public class QueenHillSkillParams
    {
        [Header("공통")]
        [Tooltip("시험용: 0보다 크면 모든 기물의 쿨타임을 이 값(초)으로 바꾼다. 0이면 아래 기획 쿨타임을 쓴다")]
        public float testCooldown = 2f;
        [Tooltip("체스판 한 칸 (m). 경고 칸·잡힌 칸의 크기 (A 디자인: 진짜 체스판 한 칸)")]
        public float square = 1.5f;

        [Header("킹 — 근접 호위 (B: 킹 자신은 빠짐)")]
        public float kingWindup = 0.3f;
        [Tooltip("이 반경(땅 위, m) 안의 아군이 호위를 받는다. 기획서 '짧은 원형 범위'의 시험값")]
        public float kingRadius = 2.5f;
        [Tooltip("위아래로 이만큼 안이면 받는다 (탑 위·아래 층, m)")]
        public float kingHeight = 2f;
        [Tooltip("호위 시간 (초)")]
        public float wardTime = 2f;
        [Tooltip("호위 중 밀림 배율: 스킬·부딪힘의 미는 힘에 곱한다")]
        public float wardPush = 0.25f;
        [Tooltip("호위 중 넘어질 일이 대신 이만큼 휘청 (초)")]
        public float wardStagger = 0.2f;
        public float kingRecovery = 0.2f;
        public float kingCooldown = 9f;

        [Header("퀸 — 팔방 검격 (A: 조준한 한 방향으로 긴 검격)")]
        [Tooltip("좌클릭 뒤 베기까지 준비 동작 (초)")]
        public float queenWindup = 0.45f;
        [Tooltip("검격 길이 (m) = 체스판 4칸")]
        public float queenLength = 6f;
        [Tooltip("검격 폭 (m)")]
        public float queenWidth = 1.2f;
        [Tooltip("검기가 끝까지 가는 시간 (초)")]
        public float queenTravel = 0.2f;
        public float queenPush = 5f;
        public float queenLift = 1.5f;
        [Tooltip("위아래로 이만큼 안이면 맞는다 (m)")]
        public float queenHeight = 1.2f;
        public float queenRecovery = 0.6f;
        public float queenCooldown = 6f;

        [Header("룩 — 캐슬링 교대 (B: 가까운 아군 누구나, 동의)")]
        [Tooltip("교대할 수 있는 아군까지 거리 (m)")]
        public float rookRange = 6f;
        [Tooltip("조준선에서 이 각도 안의 아군을 고른다 (도)")]
        public float rookCone = 35f;
        [Tooltip("상대가 수락할 때까지 기다리는 최대 시간 (초). 넘으면 취소, 쿨타임 없음")]
        public float rookWait = 3f;
        [Tooltip("시험장 더미(사람 아님)가 수락하는 데 걸리는 시간 (초): 상대 PC → 방장 판정까지의 지연을 흉내")]
        public float dummyAccept = 0.4f;
        [Tooltip("두 기물이 날아가 자리를 바꾸는 시간 (초)")]
        public float rookSwapTime = 0.55f;
        [Tooltip("자리 바꾸는 호의 높이: 높은 쪽 바닥보다 이만큼 위 (m)")]
        public float rookArc = 1.4f;
        public float rookRecovery = 0.25f;
        public float rookCooldown = 12f;

        [Header("비숍 — 교차 공중 포격 (B: 맞으면 밀려 넘어짐)")]
        [Tooltip("떠오르는 높이 (발이 바닥에서, m). 더 오를 수 없다")]
        public float bishopHover = 1.3f;
        public float bishopRise = 0.25f;
        [Tooltip("떠 있는 시간 (초). 기획: 먼저 3초, 6초와 비교")]
        public float bishopHoverTime = 3f;
        [Tooltip("조준 최대 거리 (땅 위, m)")]
        public float bishopRange = 9f;
        [Tooltip("견제탄이 날아가는 시간 (초)")]
        public float bishopFlight = 0.5f;
        [Tooltip("X자 파동 한쪽 팔 길이 (m) = 체스판 1.5칸")]
        public float bishopArm = 2.25f;
        [Tooltip("X자 팔의 폭 (m)")]
        public float bishopArmWidth = 0.7f;
        [Tooltip("가운데(조준점)에서 이 반경 안은 맞는다 (m)")]
        public float bishopCenter = 0.9f;
        public float bishopPush = 4.5f;
        public float bishopLift = 2f;
        public float bishopCooldown = 9f;

        [Header("나이트 — 도약 압착 (B: 한 층 높이까지, 높은 곳에서 납작하면 떨어짐)")]
        [Tooltip("착지점까지 최대 거리 (땅 위, m)")]
        public float knightRange = 6f;
        [Tooltip("지금 바닥보다 이만큼 높은 곳까지만 뛰어오른다 (m). 탑 한 층 0.9 m")]
        public float knightMaxRise = 1.05f;
        [Tooltip("호의 높이: 높은 쪽 바닥보다 이만큼 위 (m)")]
        public float knightArc = 1.5f;
        public float knightWindup = 0.12f;
        [Tooltip("착지 원 반경 (m)")]
        public float knightRadius = 1.3f;
        [Tooltip("납작해지는 시간 (초)")]
        public float knightFlatten = 0.7f;
        [Tooltip("납작해진 적을 착지점 바깥으로 미는 속도 (m/s): 높은 곳 가장자리면 떨어진다")]
        public float knightSlide = 3.2f;
        public float knightRecovery = 0.3f;
        public float knightCooldown = 7f;

        [Header("폰 — 비집고 돌파 (B: 몸을 낮추는 새 동작)")]
        [Tooltip("몸을 낮추는 시간 (초)")]
        public float pawnCrouch = 0.08f;
        public float pawnDistance = 2.6f;
        public float pawnSpeed = 8f;
        [Tooltip("일어나는 시간 (초)")]
        public float pawnRise = 0.22f;
        [Tooltip("골반을 내리는 높이 (m): 다리를 앞뒤로 벌려 발은 바닥에 그대로")]
        public float pawnDrop = 0.075f;
        [Tooltip("지나가며 부딪힌 적을 옆으로 비키게 미는 속도 (m/s)")]
        public float pawnNudge = 1.4f;
        public float pawnCooldown = 5f;
    }
}
