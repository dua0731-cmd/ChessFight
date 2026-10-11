using System;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Sword Fight piece skills' numbers (R91; design doc chapter 8 "기물 난투" on the repo's Sword Fight rules: every
    /// piece has a sword, a ring-out is one point, no health, the pawn has no skill). Test starting values, the same as the
    /// previz videos (Tools/Previz/SwordFightSkills): not balance. Distances are how far a hit piece slides, turned into a
    /// push speed by <see cref="pushPerMetre"/> (measured with SwordFightSkillProbe "calibrate").
    /// </summary>
    [Serializable]
    public class SwordFightSkillParams
    {
        [Header("공통")]
        [Tooltip("시험용 쿨타임 (0 = 기획 쿨타임)")]
        public float testCooldown = 2f;
        [Tooltip("밀림 거리 1 m 당 밀어내는 속도 (m/s) — probe calibrate로 잰 값")]
        public float pushPerMetre = 1.45f;
        [Tooltip("스킬에 맞아 넘어진 채로 있는 시간")]
        public float knockdownSeconds = 0.65f;
        public float pushLift = 1.0f;

        [Header("킹 · 왕의 반격")]
        public float kingCooldown = 8f;
        public float kingGuard = 0.5f;
        public float kingCounterRadius = 2.5f;
        public float kingCounterPush = 2.8f;
        public float kingCounterTime = 0.15f;
        public float kingRecovery = 0.4f;
        [Tooltip("받아낼 칼이 오지 않았을 때의 빈틈")]
        public float kingWhiff = 0.6f;

        [Header("퀸 · 꼬치 베기")]
        public float queenCooldown = 8f;
        public float queenLength = 6f;
        public float queenWidth = 1.2f;
        public float queenWindup = 0.4f;
        [Tooltip("줄을 따라 발로 달리는 돌진 속도 m/s (줄 끝이나 가장자리·벽 앞에서 멈춤)")]
        public float queenDashSpeed = 15f;
        [Tooltip("돌진이 가장자리 앞 이만큼에서 멈춤")]
        public float queenEdgeMargin = 0.8f;
        [Tooltip("퀸 몸보다 이만큼 앞에 있는 적이 베임 (칼끝)")]
        public float queenHitAhead = 0.9f;
        [Tooltip("밀리는 방향: 줄 방향 + 옆으로 이만큼")]
        public float queenSideShare = 0.6f;
        public float queenRecovery = 0.45f;
        [Tooltip("첫째 · 둘째 · 셋째 밀림 (m)")]
        public float[] queenPush = { 3.0f, 2.1f, 1.5f };

        [Header("룩 · 열린 파일 포격")]
        public float rookCooldown = 7f;
        public float rookLength = 10f;
        public float rookWidth = 1.5f;
        public float rookWindup = 0.5f;
        public float rookWaveSpeed = 18f;
        public float rookRecovery = 0.4f;
        public float rookNear = 4f;
        public float rookPushNear = 2.6f;
        public float rookPushFar = 1.6f;
        [Tooltip("솟는 탑이 띄우는 위 속도 (m/s)")]
        public float rookLift = 3.2f;
        [Tooltip("띄워진 기물은 공중에서 더 멀리 가서, 밀림 거리를 맞추려 수평 속도를 이만큼만 (probe rook로 잼)")]
        public float rookLiftCarry = 0.65f;

        [Header("비숍 · 관통 핀")]
        public float bishopCooldown = 8f;
        public float bishopRange = 8f;
        public float bishopRadius = 1f;
        public float bishopWindup = 0.4f;
        public float bishopRecovery = 0.35f;
        public float bishopSlow = 0.6f;
        [Tooltip("감속이 이어지는 시간 (묶임 시간 포함)")]
        public float bishopSlowTime = 1.5f;
        [Tooltip("손이 발목을 잡고 있는 시간 (맵 어디서든, R102)")]
        public float bishopPinTime = 0.8f;

        [Header("나이트 · 포크 강하")]
        public float knightCooldown = 7f;
        public float knightRange = 6f;
        public float knightMinRange = 1.5f;
        public float knightAir = 0.55f;
        public float knightSpotOffset = 1.8f;
        public float knightSpotAngle = 50f;
        public float knightSpotRadius = 1.2f;
        public float knightPush = 2.4f;
        public float knightRecovery = 0.35f;

        public float Cooldown(ChessFight.Network.PieceKind kind) => kind switch
        {
            ChessFight.Network.PieceKind.King => kingCooldown,
            ChessFight.Network.PieceKind.Queen => queenCooldown,
            ChessFight.Network.PieceKind.Rook => rookCooldown,
            ChessFight.Network.PieceKind.Bishop => bishopCooldown,
            ChessFight.Network.PieceKind.Knight => knightCooldown,
            _ => 0f,
        };
    }
}
