using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Marks a float field as adjustable from the lab's debug panel.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TunableAttribute : Attribute
    {
        public readonly string Group;
        public readonly string Label;

        public TunableAttribute(string group, string label)
        {
            Group = group;
            Label = label;
        }
    }

    /// <summary>
    /// Every feel parameter of the ragdoll lab. The first four groups use the names and
    /// starting values from kingrush-ragdoll-prototype-spec.md; the rest are lab helpers.
    /// </summary>
    [Serializable]
    public class RagdollParams
    {
        public const string GroupStiffness = "강성";
        public const string GroupDynamic = "동적 강성";
        public const string GroupDown = "다운 / 기상";
        public const string GroupMove = "이동";
        public const string GroupAssist = "보조 (명세 외)";
        public const string GroupPose = "퍼펫 포즈 (명세 외)";
        public const string GroupWeight = "발과 무게감 (명세 외)";
        public const string GroupAction = "버둥대기 / 등반 (명세 외)";

        [Tunable(GroupStiffness, "골반 앵커 hipAnchorStrength")] [Range(0f, 20000f)] public float hipAnchorStrength = 3000f;
        [Tunable(GroupStiffness, "하체 lowerBodySpring")] [Range(0f, 10000f)] public float lowerBodySpring = 2000f;
        [Tunable(GroupStiffness, "상체 upperBodySpring")] [Range(0f, 10000f)] public float upperBodySpring = 400f;
        [Tunable(GroupStiffness, "팔 armSpring")] [Range(0f, 5000f)] public float armSpring = 80f;
        [Tunable(GroupStiffness, "댐퍼 비율 damperRatio")] [Range(0.02f, 0.5f)] public float damperRatio = 0.1f;

        [Tunable(GroupDynamic, "접촉 시 배율")] [Range(0f, 1f)] public float contactStiffnessMultiplier = 0.35f;
        [Tunable(GroupDynamic, "잡는 중 배율")] [Range(0f, 1f)] public float grabStiffnessMultiplier = 0.5f;
        [Tunable(GroupDynamic, "경사 배율")] [Range(0f, 1f)] public float slopeStiffnessMultiplier = 0.6f;
        [Tunable(GroupDynamic, "배율 보간 속도")] [Range(0.5f, 30f)] public float stiffnessLerpSpeed = 8f;

        [Tunable(GroupDown, "넉다운 충격 (m/s)")] [Range(1f, 30f)] public float knockdownImpulseThreshold = 8f;
        [Tunable(GroupDown, "기상 대기 getUpDelay (초)")] [Range(0f, 5f)] public float getUpDelay = 1.2f;
        [Tunable(GroupDown, "기상 보간 (초)")] [Range(0.05f, 2f)] public float getUpBlendTime = 0.35f;
        [Tunable(GroupDown, "기상 시 속도 유지")] [Range(0f, 1f)] public float momentumRetention = 0.7f;

        [Tunable(GroupMove, "이동 속도 (m/s)")] [Range(0f, 15f)] public float moveSpeed = 5f;
        [Tunable(GroupMove, "가속")] [Range(0f, 100f)] public float acceleration = 30f;
        [Tunable(GroupMove, "방향 전환")] [Range(0f, 40f)] public float turnResponsiveness = 12f;
        [Tunable(GroupMove, "점프 (m/s)")] [Range(0f, 15f)] public float jumpImpulse = 6f;
        [Tunable(GroupMove, "공중 제어")] [Range(0f, 1f)] public float airControl = 0.25f;

        [Tunable(GroupAssist, "동적 배율의 하체 적용 비율")] [Range(0f, 1f)] public float lowerBodyDynamicShare = 0.6f;
        [Tunable(GroupAssist, "골반 직립 토크")] [Range(0f, 5000f)] public float balanceStrength = 1200f;
        [Tunable(GroupAssist, "골반 직립 감쇠")] [Range(0f, 500f)] public float balanceDamper = 90f;
        [Tunable(GroupAssist, "몸 방향 토크")] [Range(0f, 3000f)] public float yawStrength = 350f;
        [Tunable(GroupAssist, "과속 감쇠 (m/s²)")] [Range(0f, 40f)] public float overspeedDecay = 4f;
        [Tunable(GroupAssist, "래그돌 마찰")] [Range(0f, 1f)] public float ragdollFriction = 0.1f;
        [Tunable(GroupAssist, "발 마찰 (서 있을 때)")] [Range(0f, 1.5f)] public float footFrictionIdle = 0.6f;
        [Tunable(GroupAssist, "발 마찰 (이동·밀치기·피격 중)")] [Range(0f, 1.5f)] public float footFrictionMoving = 0.1f;
        [Tunable(GroupAssist, "달릴 때 골반 들기 (m)")] [Range(0f, 0.1f)] public float runLift = 0.03f;
        [Tunable(GroupAssist, "피격 배율")] [Range(0f, 1f)] public float hitStiffnessMultiplier = 0.3f;
        [Tunable(GroupAssist, "피격 회복 (초)")] [Range(0f, 3f)] public float hitRecoveryTime = 1f;
        [Tunable(GroupAssist, "피격 판정 충격 (m/s)")] [Range(0.5f, 15f)] public float hitImpactThreshold = 3f;
        [Tunable(GroupAssist, "접촉 유지 (초)")] [Range(0f, 1f)] public float contactLinger = 0.15f;
        [Tunable(GroupAssist, "경사 기준 (도)")] [Range(0f, 60f)] public float slopeAngleThreshold = 20f;
        [Tunable(GroupAssist, "잡기 반경 (m)")] [Range(0.05f, 0.6f)] public float grabRadius = 0.25f;
        [Tunable(GroupAssist, "잡기 접촉 거리 (m)")] [Range(0f, 0.2f)] public float grabContactDistance = 0.05f;
        [Tunable(GroupAssist, "잡기 breakForce")] [Range(200f, 30000f)] public float grabBreakForce = 6000f;
        // Split from grabBreakForce so escape difficulty can be tuned without changing how a
        // hand holds a ledge - the same number was doing both jobs.
        [Tunable(GroupAssist, "상대를 잡는 힘 breakForce")] [Range(200f, 20000f)] public float pawnGrabBreakForce = 2400f;
        [Tunable(GroupAssist, "잡은 팔 강성 배율")] [Range(1f, 20f)] public float grabArmMultiplier = 6f;
        [Tunable(GroupAssist, "뻗는 팔 강성 배율")] [Range(1f, 20f)] public float reachArmMultiplier = 3f;
        [Tunable(GroupAssist, "밀치기 팔 배율")] [Range(1f, 20f)] public float shoveArmMultiplier = 5f;
        [Tunable(GroupAssist, "밀치기 시간 (초)")] [Range(0.05f, 1f)] public float shoveDuration = 0.2f;
        [Tunable(GroupAssist, "밀치기 몸 기울기 (도)")] [Range(0f, 45f)] public float shoveLean = 22f;
        [Tunable(GroupAssist, "밀치기 내딛기 (m)")] [Range(0f, 1f)] public float shoveLunge = 0.6f;
        [Tunable(GroupAssist, "잡기 몸 기울기 (도)")] [Range(0f, 45f)] public float grabLean = 14f;
        [Tunable(GroupAssist, "매달려 점프 배율")] [Range(0f, 2f)] public float pullUpFactor = 1f;
        [Tunable(GroupAssist, "앵커 최대 거리 (m)")] [Range(0.1f, 2f)] public float anchorLeash = 0.6f;
        [Tunable(GroupAssist, "정지 시 앵커 따라오기")] [Range(0f, 20f)] public float anchorIdleFollow = 6f;
        [Tunable(GroupAssist, "중력 배율")] [Range(0.5f, 3f)] public float gravityScale = 1f;

        [Tunable(GroupPose, "보폭 (m/주기)")] [Range(0.2f, 3f)] public float strideLength = 0.9f;
        [Tunable(GroupPose, "다리 스윙 (도)")] [Range(0f, 80f)] public float legSwing = 35f;
        [Tunable(GroupPose, "팔 스윙 (도)")] [Range(0f, 90f)] public float armSwing = 35f;
        [Tunable(GroupPose, "팔 내림 (도)")] [Range(-30f, 80f)] public float armRestDown = 20f;
        [Tunable(GroupPose, "상체 기울기 (도)")] [Range(0f, 40f)] public float chestLean = 10f;
        [Tunable(GroupPose, "골반 기울기 (도)")] [Range(0f, 30f)] public float runLean = 6f;

        // Everything below defaults to "off", so the feel only changes when it is dialled up.
        [Tunable(GroupWeight, "발 고정 (0=미끄러짐)")] [Range(0f, 1f)] public float stepLock;
        [Tunable(GroupWeight, "걸음당 추진 (0=일정)")] [Range(0f, 1f)] public float stanceThrust;
        [Tunable(GroupWeight, "걸음 들썩임 (m)")] [Range(0f, 0.12f)] public float stepBob;
        [Tunable(GroupWeight, "걸음 좌우 기울기 (도)")] [Range(0f, 12f)] public float stepRoll;
        [Tunable(GroupWeight, "회전 시 기울기 (도)")] [Range(0f, 30f)] public float turnLean;
        [Tunable(GroupWeight, "착지 주저앉기 (m)")] [Range(0f, 0.2f)] public float landingDip;
        [Tunable(GroupWeight, "정지 감속 (m/s²)")] [Range(1f, 100f)] public float stopDeceleration = 30f;
        [Tunable(GroupWeight, "전속력 회전 속도 (도/초)")] [Range(60f, 1080f)] public float turnRateTopSpeed = 1080f;
        [Tunable(GroupWeight, "보폭 (발 고정용, m)")] [Range(0.05f, 0.6f)] public float stepLength = 0.22f;
        [Tunable(GroupWeight, "두 발 모아 도약 (0=교대걸음)")] [Range(0f, 1f)] public float boundGait;
        [Tunable(GroupWeight, "관절 속도 예측 (0=꺼짐)")] [Range(0f, 1.5f)] public float driveFeedForward;
        [Tunable(GroupWeight, "걸음 주기 고정 (회/초, 0=보폭기준)")] [Range(0f, 5f)] public float hopCadence;
        [Tunable(GroupWeight, "다리 전용 댐퍼비 (0=전체값 사용)")] [Range(0f, 0.2f)] public float legDamperRatio;
        [Tunable(GroupWeight, "허벅지 무게 (kg, 0=원래값)")] [Range(0f, 6f)] public float thighMass;
        [Tunable(GroupWeight, "발 무게 (kg, 0=원래값)")] [Range(0f, 5f)] public float footMass;
        [Tunable(GroupWeight, "역방향 제동 거리 (m, 0=앵커 거리와 동일)")] [Range(0f, 0.6f)] public float anchorBrakeLeash;
        // Not a feel knob: without it a hard direction change lets the anchor spring launch the pawn
        // at 143% of its run speed. On by default because that is a bug, not a mechanic.
        [Tunable(GroupWeight, "최고 속도 상한 (달리기 배수, 0=무제한)")] [Range(0f, 2f)] public float overspeedClamp = 1.1f;

        [Tunable(GroupAction, "버둥 1회 지속 (초)")] [Range(0.05f, 0.8f)] public float struggleBurst = 0.22f;
        [Tunable(GroupAction, "연타 보너스 상한 (배)")] [Range(1f, 4f)] public float struggleRushBonus = 2f;
        [Tunable(GroupAction, "반대 방향 입력 보너스 (배)")] [Range(1f, 3f)] public float struggleAwayBonus = 1.7f;
        [Tunable(GroupAction, "점프 보너스 (배)")] [Range(1f, 3f)] public float struggleJumpBonus = 1.6f;
        [Tunable(GroupAction, "버둥 1회 스테미나 (초)")] [Range(0f, 2f)] public float struggleStamina = 0.35f;
        [Tunable(GroupAction, "버둥 팔 진폭 (도)")] [Range(0f, 160f)] public float struggleSwing = 95f;
        [Tunable(GroupAction, "버둥 몸부림 충격")] [Range(0f, 400f)] public float struggleShake = 150f;
        [Tunable(GroupAction, "버둥 중 팔 강성 배율")] [Range(1f, 20f)] public float struggleArmMultiplier = 8f;
        [Tunable(GroupAction, "등반 속도 (m/s)")] [Range(0.2f, 4f)] public float climbSpeed = 0.85f;
        [Tunable(GroupAction, "등반 스테미나 (초)")] [Range(1f, 30f)] public float climbStaminaMax = 8f;
        // Drain and recovery are in stamina-seconds per second, so the numbers read directly:
        // hanging 0.35 means the 8 s bar lasts 23 s of just hanging, 6.4 s of full climbing.
        [Tunable(GroupAction, "매달리기 소모 (/초)")] [Range(0f, 1f)] public float climbDrainHold = 0.35f;
        [Tunable(GroupAction, "오르기 추가 소모 (/초)")] [Range(0f, 2f)] public float climbDrainMove = 0.9f;
        [Tunable(GroupAction, "스테미나 회복 (/초)")] [Range(0.05f, 4f)] public float climbRecover = 2f;
        [Tunable(GroupAction, "손 번갈아 잡기 (회/초)")] [Range(0.3f, 5f)] public float climbCadence = 1.25f;
        [Tunable(GroupAction, "등반 자세 팔 높이 (도)")] [Range(0f, 120f)] public float climbArmRaise = 70f;
        [Tunable(GroupAction, "등반 가능 경사 (도, 수평 기준)")] [Range(30f, 89f)] public float climbGripAngle = 55f;
        [Tunable(GroupAction, "바위 위로 올라타기 (초)")] [Range(0f, 1.2f)] public float climbTopOut = 0.5f;
        [Tunable(GroupAction, "등반 허우적 (도)")] [Range(0f, 90f)] public float climbFlail = 18f;
        [Tunable(GroupAction, "손 짚는 간격 (m)")] [Range(0.1f, 0.6f)] public float climbHandStep = 0.3f;
        [Tunable(GroupAction, "손 좌우 벌림 (m)")] [Range(0.05f, 0.5f)] public float climbHandSpread = 0.19f;
        [Tunable(GroupAction, "지칠 때 미끄러짐 (m)")] [Range(0f, 0.5f)] public float climbSlip = 0.18f;
    }

    [CreateAssetMenu(menuName = "ChessFight/Ragdoll Tuning", fileName = "RagdollTuning")]
    public class RagdollTuning : ScriptableObject
    {
        public RagdollParams values = new RagdollParams();

        public string ToJson() => JsonUtility.ToJson(values, true);

        public void LoadJson(string json) => JsonUtility.FromJsonOverwrite(json, values);

        public void ResetToSpec() => LoadJson(JsonUtility.ToJson(new RagdollParams()));

        public static IEnumerable<(FieldInfo field, TunableAttribute tag, RangeAttribute range)> Fields()
        {
            foreach (var field in typeof(RagdollParams).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var tag = field.GetCustomAttribute<TunableAttribute>();
                var range = field.GetCustomAttribute<RangeAttribute>();
                if (tag != null && range != null && field.FieldType == typeof(float))
                    yield return (field, tag, range);
            }
        }
    }
}
