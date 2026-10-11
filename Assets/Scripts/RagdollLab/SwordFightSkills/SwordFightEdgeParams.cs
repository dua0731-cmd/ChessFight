using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The numbers of the Sword Fight edge skills (R103, 승규 님's 가장자리 스킬 plan, 2026-10-10): a second skill per
    /// piece on its own key (E), only where a fight is won or lost - at the platform's edge. Test starting values from the
    /// plan; the cooldowns are 2 s while testing (<see cref="testCooldown"/>, 0 = the plan's 15-20 s).
    /// </summary>
    [Serializable]
    public class SwordFightEdgeParams
    {
        [Header("공통")]
        [Tooltip("시험용 쿨타임 (0 = 기획 쿨타임). 조건이 깨져 예고 중에 취소되면 반만 돎")]
        public float testCooldown = 2f;
        [Tooltip("발판 끝에서 이만큼 안이 가장자리 구역 (m)")]
        public float edgeZone = 2f;
        [Tooltip("밀려 나간 뒤 이 시간 안이면 떨어지는 중에도 씀 (킹)")]
        public float fallWindow = 0.6f;
        [Tooltip("골반이 발판보다 이만큼 아래로 내려가면 늦음 (m)")]
        public float fallDepth = 2f;
        [Tooltip("극적인 순간의 슬로 모션 길이 (게임 시간 초) — 시험장에서는 게임 전체, 실제로는 쓴 사람·맞은 사람 화면만")]
        public float slowTime = 0.3f;
        [Tooltip("슬로 모션 빠르기 (1 = 보통)")]
        public float slowScale = 0.35f;

        [Header("킹 · 왕의 귀환")]
        public float kingCooldown = 20f;
        [Tooltip("칼을 박고 매달려 있는 시간 (이동안 착지할 자리가 보임)")]
        public float kingHang = 0.4f;
        [Tooltip("칼이 박히기까지 몸이 가장자리로 끌려가는 시간")]
        public float kingCatch = 0.12f;
        [Tooltip("뛰어올라 착지하기까지")]
        public float kingVault = 0.5f;
        [Tooltip("착지할 자리: 가장자리에서 안쪽으로 (m)")]
        public float kingLandIn = 1.5f;
        public float kingLandRadius = 2f;
        public float kingLandPush = 2f;
        [Tooltip("칼을 박을 수 있는 가장자리까지의 거리 (m)")]
        public float kingReach = 3f;

        [Header("퀸 · 체크메이트 일섬")]
        public float queenCooldown = 15f;
        [Tooltip("가장자리 구역의 적이 이 거리 안에 있으면 씀 (m)")]
        public float queenRange = 5f;
        [Tooltip("금색 선이 그어져 있는 예고 시간")]
        public float queenLine = 0.5f;
        [Tooltip("베인 적이 바깥으로 날아가는 거리 (m)")]
        public float queenFling = 5f;
        [Tooltip("베인 적이 위로 뜨는 속도 (m/s)")]
        public float queenFlingUp = 4.5f;
        public float queenRecovery = 0.4f;

        [Header("룩 · 성벽 붕괴")]
        public float rookCooldown = 18f;
        [Tooltip("무너지는 덩어리: 가장자리를 따라 폭 (m)")]
        public float rookChunkWidth = 3f;
        [Tooltip("무너지는 덩어리: 안쪽으로 깊이 (m)")]
        public float rookChunkDepth = 2f;
        [Tooltip("금이 가는 예고 시간")]
        public float rookCrack = 0.8f;
        [Tooltip("무너진 뒤 바닥이 다시 솟기까지")]
        public float rookRestore = 5f;
        [Tooltip("내려치는 동안 제자리")]
        public float rookSlam = 0.3f;
        [Tooltip("조준한 쪽 가장자리를 찾는 거리 (m)")]
        public float rookAimReach = 4.5f;

        [Header("비숍 · 구원의 손")]
        public float bishopCooldown = 18f;
        [Tooltip("떨어지는 아군이 이 거리 안이면 씀 (m)")]
        public float bishopRange = 6f;
        [Tooltip("아군이 떨어지기 시작하고 이 시간 안")]
        public float bishopFallWindow = 1f;
        [Tooltip("손이 아군에게 닿기까지")]
        public float bishopReach = 0.15f;
        [Tooltip("끌어당겨 발판 위에 내려놓기까지")]
        public float bishopPull = 0.6f;
        [Tooltip("내려놓는 자리: 가장자리에서 안쪽으로 (m)")]
        public float bishopSetIn = 1.2f;
        public float bishopRecovery = 0.3f;

        [Header("나이트 · 벼랑 끝 역전")]
        public float knightCooldown = 16f;
        [Tooltip("등 뒤 이 거리 안이 가장자리 (m)")]
        public float knightBackEdge = 1.5f;
        [Tooltip("앞쪽 이 거리 안에 적 (m)")]
        public float knightFront = 3f;
        [Tooltip("웅크리는 예고 시간")]
        public float knightCrouch = 0.35f;
        [Tooltip("벼랑 밖으로 뛰었다 적 머리 위를 넘어 착지하기까지")]
        public float knightFlip = 0.8f;
        [Tooltip("공중제비 높이 (발판 위, m) — 기물 키(약 0.6 m)의 두 배쯤")]
        public float knightFlipHeight = 1.1f;
        [Tooltip("착지: 적 뒤로 (m)")]
        public float knightLandBehind = 0.9f;
        [Tooltip("뒷발차기로 적이 벼랑 쪽으로 날아가는 거리 (m)")]
        public float knightKick = 3.5f;
        public float knightRecovery = 0.35f;

        public float Cooldown(PieceKind kind) => kind switch
        {
            PieceKind.King => kingCooldown,
            PieceKind.Queen => queenCooldown,
            PieceKind.Rook => rookCooldown,
            PieceKind.Bishop => bishopCooldown,
            PieceKind.Knight => knightCooldown,
            _ => 0f,
        };
    }
}
