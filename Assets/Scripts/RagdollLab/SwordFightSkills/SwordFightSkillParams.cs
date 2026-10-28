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
        public float kingGuard = 0.9f;   // R130: 0.5 → 0.9 (승규 님: "왕의 반격을 할 때 시간을 더 늘려주고")
        public float kingCounterRadius = 2.5f;
        public float kingCounterPush = 2.8f;
        [Tooltip("R130: 막은 뒤 반격(한 바퀴 베기)까지 멈칫하는 박자 (초)")]
        public float kingCounterBeat = 0.16f;
        [Tooltip("반격: 막은 상대 쪽으로 크게 한 번 베는 시간 (초). R130: 0.15 → 0.34 (\"더 연출과 액션을\"). 10-25: 한 바퀴 돌기 → 앞으로 대각선 베기")]
        public float kingCounterTime = 0.34f;
        [Tooltip("10-25 (승규 님: \"회전하는 것 보다 움직이면서도 막을 수 있게\"): 받아내기 자세·반격 중 걷는 빠르기 (1 = 평소)")]
        public float kingGuardMove = 0.7f;
        [Tooltip("10-25: 반격이 닿는 앞쪽 부채꼴 (도, 막은 상대 쪽 가운데)")]
        public float kingCounterArc = 150f;
        public float kingRecovery = 0.4f;
        [Tooltip("받아낼 칼이 오지 않았을 때의 빈틈")]
        public float kingWhiff = 0.6f;

        [Header("퀸 · 꼬치 베기")]
        public float queenCooldown = 8f;
        public float queenLength = 6f;
        public float queenWidth = 1.2f;
        public float queenWindup = 0.4f;
        [Tooltip("줄을 따라 발로 달리는 돌진 속도 m/s (줄 끝이나 벽 앞에서 멈춤. R129: 가장자리에서는 안 멈춤 — 거리를 잘못 재면 장 밖으로)")]
        public float queenDashSpeed = 15f;
        [Tooltip("R129부터 쓰지 않음 (돌진이 가장자리 앞에서 멈추던 여유)")]
        public float queenEdgeMargin = 0.8f;
        [Tooltip("장 밖으로 달려 나갔을 때 남는 속도 (돌진 속도의 몫)")]
        public float queenFallCarry = 0.6f;
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

        [Header("비숍 · X자 베기 (10-27: 양손에 칼 두 자루 → 조준 → 비숍은 그 자리, 두 칼이 날아가 상대 앞에서 X자로 → 무작위로 돌아 손에)")]
        [Tooltip("발동하고 두 칼을 뒤로 젖혀 던질 준비 (s)")]
        public float bishopThrowWindup = 0.2f;
        [Tooltip("칼이 날아가는 빠르기 (m/s): 거리를 이것으로 나눈 시간 (아래 최소·최대 사이)")]
        public float bishopThrowSpeed = 14f;
        public float bishopOutMin = 0.28f;
        public float bishopOutMax = 0.6f;
        [Tooltip("두 칼이 X자로 엇갈리는 곳과 상대 사이의 여유 (m): 칼은 상대에 닿지 않음 - 맞음은 이펙트로")]
        public float bishopClear = 0.8f;
        [Tooltip("X자가 엇갈리는 높이 (바닥에서, m)")]
        public float bishopCrossHeight = 0.55f;
        [Tooltip("X자로 엇갈려 베는 시간 (s): 그 한가운데에 맞음")]
        public float bishopCrossTime = 0.14f;
        [Tooltip("X자가 맞히는 반경 (엇갈리는 곳에서, m)")]
        public float bishopCrossRadius = 1.45f;
        [Tooltip("X자 베기에 밀리는 거리 (m)")]
        public float bishopPush = 3.2f;
        [Tooltip("칼이 무작위로 돌아다니며 손으로 돌아오는 시간 (s)")]
        public float bishopBackTime = 0.85f;
        [Tooltip("날아가는 칼이 도는 빠르기 (초당 바퀴)")]
        public float bishopSpin = 6f;
        [Tooltip("칼을 받고 자세를 푸는 시간 (s)")]
        public float bishopCatchAfter = 0.35f;

        [Header("나이트 · 포크 강하 (R129 새 동작)")]
        public float knightCooldown = 7f;
        public float knightRange = 6f;
        public float knightMinRange = 1.5f;
        [Tooltip("체스판 한 칸 (m): 착지한 칸에서 나이트가 노리는 여덟 칸 (L자: 두 칸 가고 한 칸 꺾은 자리)")]
        public float knightCell = 1.25f;
        [Tooltip("뛰기 전 웅크리는 시간 (s): 0이면 바로 뜀 (10-26 몸 동작)")]
        public float knightGather = 0.07f;
        [Tooltip("높이 날아오르는 시간 (s)")]
        public float knightRise = 0.3f;
        [Tooltip("꼭대기에 머무는 시간 (s): 노리는 칸들이 드러남")]
        public float knightHover = 0.14f;
        [Tooltip("내리꽂는 시간 (s)")]
        public float knightDive = 0.16f;
        [Tooltip("꼭대기 높이 (출발·착지 중 높은 바닥 위 m)")]
        public float knightApex = 3.2f;
        [Tooltip("꼭대기가 길의 몇 % 지점 위에 오나 (클수록 거의 수직으로 내리꽂음)")]
        public float knightApexAt = 0.72f;
        [Tooltip("L자 칸 하나의 판정 반지름 (m)")]
        public float knightSquare = 0.65f;
        [Tooltip("내리찍은 칸 (착지점)의 판정 반지름 (m)")]
        public float knightStomp = 0.85f;
        [Tooltip("L자 칸의 적이 밀리는 거리 (m)")]
        public float knightPush = 2.4f;
        [Tooltip("착지점의 적이 밀리는 거리 (m)")]
        public float knightStompPush = 1.6f;
        public float knightRecovery = 0.35f;

        [Header("폰 · 첫 두 걸음 (R149 프로토타입 경기, 명세 §7.2)")]
        public float pawnCooldown = 5f;
        public float pawnStepSpeed = 7.5f;    // 1.5 m in 0.2 s
        public float pawnStepTime = 0.2f;
        public float pawnStepGap = 0.22f;
        [Tooltip("부딪힘 판정 거리 (몸 겹침 방지 0.76 m보다 커야 함)")]
        public float pawnReach = 0.84f;
        public float pawnPush = 4.5f, pawnPushUp = 1.2f;
        [Tooltip("정면 (진행 방향과 이 각도 미만): 둘 다 튕김")]
        public float pawnHeadOn = 25f;
        [Tooltip("이 각도까지는 대각선 (넘어뜨림), 넘으면 판정 없음")]
        public float pawnDiagonal = 75f;
        public float pawnBounce = 2f;

        public float Cooldown(ChessFight.Network.PieceKind kind) => kind switch
        {
            ChessFight.Network.PieceKind.Pawn => pawnCooldown,
            ChessFight.Network.PieceKind.King => kingCooldown,
            ChessFight.Network.PieceKind.Queen => queenCooldown,
            ChessFight.Network.PieceKind.Rook => rookCooldown,
            ChessFight.Network.PieceKind.Bishop => bishopCooldown,
            ChessFight.Network.PieceKind.Knight => knightCooldown,
            _ => 0f,
        };
    }
}
