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
        public const string GroupSprint = "전력질주 · 스테미나 (명세 외)";
        public const string GroupDive = "다이빙 태클 (좌클릭, 명세 외)";

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

        // The everyday run. Holding sprint blends toward sprintSpeed (and the sprint gait) while
        // stamina lasts; every "speed" below that is a fraction of top speed means the current one.
        [Tunable(GroupMove, "달리기 속도 (m/s)")] [Range(0f, 15f)] public float moveSpeed = 5f;
        [Tunable(GroupMove, "전력질주 속도 (m/s, Shift)")] [Range(0f, 15f)] public float sprintSpeed = 9.6f;
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
        [Tunable(GroupAssist, "달릴 때 골반 들기 (m, 전력질주는 따로)")] [Range(0f, 0.1f)] public float runLift = 0.03f;
        [Tunable(GroupAssist, "피격 배율")] [Range(0f, 1f)] public float hitStiffnessMultiplier = 0.3f;
        [Tunable(GroupAssist, "피격 회복 (초)")] [Range(0f, 3f)] public float hitRecoveryTime = 1f;
        [Tunable(GroupAssist, "피격 판정 충격 (m/s)")] [Range(0.5f, 15f)] public float hitImpactThreshold = 3f;
        // Running into another pawn: both bounce apart by this share of the speed they met at, so a
        // bump reads as a bump instead of two bodies grinding. Pushing someone by walking into them
        // still works - the run builds up again at once.
        [Tunable(GroupAssist, "부딪힘 튕김 (만난 속도 배)")] [Range(0f, 1f)] public float bumpBounce = 0.2f;
        [Tunable(GroupAssist, "부딪힘 튕김 최대 (m/s)")] [Range(0f, 4f)] public float bumpBounceMax = 1.5f;
        [Tunable(GroupAssist, "접촉 유지 (초)")] [Range(0f, 1f)] public float contactLinger = 0.15f;
        [Tunable(GroupAssist, "경사 기준 (도)")] [Range(0f, 60f)] public float slopeAngleThreshold = 20f;
        [Tunable(GroupAssist, "잡기 반경 (m)")] [Range(0.05f, 0.6f)] public float grabRadius = 0.25f;
        [Tunable(GroupAssist, "잡기 접촉 거리 (m)")] [Range(0f, 0.2f)] public float grabContactDistance = 0.05f;
        [Tunable(GroupAssist, "잡기 breakForce")] [Range(200f, 30000f)] public float grabBreakForce = 6000f;
        // Split from grabBreakForce so escape difficulty can be tuned without changing how a
        // hand holds a ledge - the same number was doing both jobs.
        // Only a safety valve now (a throw off a cliff, a pile of pawns): escaping is the struggle
        // meter's job, and a grip that snapped at random made one click enough to get free.
        [Tunable(GroupAssist, "상대를 잡는 힘 breakForce")] [Range(200f, 20000f)] public float pawnGrabBreakForce = 20000f;
        [Tunable(GroupAssist, "잡은 팔 강성 배율")] [Range(1f, 20f)] public float grabArmMultiplier = 6f;
        [Tunable(GroupAssist, "뻗는 팔 강성 배율")] [Range(1f, 20f)] public float reachArmMultiplier = 3f;
        [Tunable(GroupAssist, "밀치기 팔 배율")] [Range(1f, 20f)] public float shoveArmMultiplier = 5f;
        [Tunable(GroupAssist, "밀치기 시간 (초)")] [Range(0.05f, 1f)] public float shoveDuration = 0.2f;
        [Tunable(GroupAssist, "밀치기 몸 기울기 (도)")] [Range(0f, 45f)] public float shoveLean = 22f;
        [Tunable(GroupAssist, "밀치기 내딛기 (m)")] [Range(0f, 1f)] public float shoveLunge = 0.6f;
        [Tunable(GroupAssist, "잡기 몸 기울기 (도)")] [Range(0f, 45f)] public float grabLean = 14f;
        [Tunable(GroupAssist, "매달려 점프 배율")] [Range(0f, 2f)] public float pullUpFactor = 1f;
        [Tunable(GroupAssist, "앵커 최대 거리 (m)")] [Range(0.1f, 2f)] public float anchorLeash = 0.6f;
        // Pressing into a wall and getting nowhere, the anchor may only lead this far (a full leash
        // pinned the body into the wall, and it sprang back off a ledge when the key came up).
        [Tunable(GroupAssist, "벽에 막혔을 때 앵커 거리 (m)")] [Range(0.05f, 0.6f)] public float anchorBlockedLeash = 0.15f;
        [Tunable(GroupAssist, "정지 시 앵커 따라오기")] [Range(0f, 20f)] public float anchorIdleFollow = 6f;
        [Tunable(GroupAssist, "중력 배율")] [Range(0.5f, 3f)] public float gravityScale = 1f;

        [Tunable(GroupPose, "보폭 (m/주기)")] [Range(0.2f, 3f)] public float strideLength = 0.9f;
        // The shipped run commands 140 (the hip limit caps what actually comes out at ~95), so the
        // slider has to reach past it or touching it once would clamp the default away.
        // Capped at 60 in RagdollPawn: that is as far as the hip joint turns (see HipSwingLimit).
        [Tunable(GroupPose, "다리 스윙 (도, 최대 60)")] [Range(0f, 60f)] public float legSwing = 35f;
        [Tunable(GroupPose, "팔 스윙 (도)")] [Range(0f, 90f)] public float armSwing = 35f;
        [Tunable(GroupPose, "팔 내림 (도)")] [Range(-30f, 80f)] public float armRestDown = 20f;
        [Tunable(GroupPose, "상체 기울기 (도)")] [Range(0f, 40f)] public float chestLean = 10f;
        [Tunable(GroupPose, "골반 기울기 (도)")] [Range(0f, 30f)] public float runLean = 6f;
        // The run's own touches (the sprint has none of them): arms hang lower instead of flapping out
        // at the sides, and the shoulders turn against the hips with each step, the way a person's do.
        [Tunable(GroupPose, "달릴 때 팔 내림 (도)")] [Range(-30f, 80f)] public float runArmDown = 20f;
        [Tunable(GroupPose, "달리기 상체 비틀기 (도)")] [Range(0f, 20f)] public float runTwist;
        // A little extra forward pitch as each foot takes the weight, then the body rises off it:
        // reads as pushing off the ground rather than being slid along.
        [Tunable(GroupPose, "달리기 딛을 때 앞으로 숙임 (도)")] [Range(0f, 15f)] public float runDrive;
        // The foot on the back swing is kicked out to the side, so it shows beside the skirt - the
        // legs are otherwise hidden under it from behind. Deliberate and inside the hip's 35 degree
        // sideways range, unlike the old sprint where the same flick came from ramming the joint.
        [Tunable(GroupPose, "달리기 뒤로 찬 발 바깥으로 (도)")] [Range(0f, 25f)] public float runSplay;

        // Everything below defaults to "off", so the feel only changes when it is dialled up.
        [Tunable(GroupWeight, "발 고정 (0=미끄러짐)")] [Range(0f, 1f)] public float stepLock;
        [Tunable(GroupWeight, "걸음당 추진 (0=일정)")] [Range(0f, 1f)] public float stanceThrust;
        [Tunable(GroupWeight, "걸음 들썩임 (m)")] [Range(0f, 0.12f)] public float stepBob;
        // A walking body is highest as the legs pass each other and lowest when they are spread. A
        // one-piece leg swung out by an angle is reach * (1 - cos angle) shorter vertically (7.5 cm
        // at 60 degrees), so unless the hips come down with it the feet leave the floor at every
        // stride - which is what made the first run float. 1 rides exactly on the legs.
        [Tunable(GroupWeight, "달리기: 다리 벌어진 만큼 골반 내림 (1=다리 길이대로)")] [Range(0f, 1.5f)] public float runLegDrop;
        [Tunable(GroupWeight, "걸음 좌우 기울기 (도)")] [Range(0f, 20f)] public float stepRoll;
        // Leaning into the acceleration (see RagdollPawn.LeanIntoAcceleration): 1 balances the push
        // exactly, and turnLean / sprintTurnLean cap the angle.
        [Tunable(GroupWeight, "가속·회전 방향으로 쏠림 (1=힘과 균형)")] [Range(0f, 1.5f)] public float accelLean;
        [Tunable(GroupWeight, "가속·회전 쏠림 최대 (도)")] [Range(0f, 30f)] public float turnLean;
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
        // The body hangs on its anchor by a spring. 0.25 is about critical for this 50 kg pawn:
        // it follows the path without swinging past it and back after every change of direction.
        [Tunable(GroupWeight, "앵커 감쇠비 (0=전체값, 0.25≈흔들림 없음)")] [Range(0f, 0.6f)] public float anchorDamperRatio;
        // Not a feel knob: without it a hard direction change lets the anchor spring launch the pawn
        // at 143% of its run speed. On by default because that is a bug, not a mechanic.
        [Tunable(GroupWeight, "최고 속도 상한 (달리기 배수, 0=무제한)")] [Range(0f, 2f)] public float overspeedClamp = 1.1f;

        // Escaping a grab is a meter, not a physics accident: every left click fills it by
        // 1 / struggleTaps (times the bonuses), it drains when the clicks stop, and full = free. The
        // grip itself no longer breaks from a thrash (pawnGrabBreakForce is only a safety valve).
        [Tunable(GroupAction, "버둥 탈출 연타 수 (가만히)")] [Range(2f, 20f)] public float struggleTaps = 9f;
        [Tunable(GroupAction, "버둥 게이지 감소 (1/초)")] [Range(0f, 2f)] public float struggleDecay = 0.6f;
        [Tunable(GroupAction, "버둥 게이지 유지 (초)")] [Range(0f, 1.5f)] public float struggleGrace = 0.25f;
        [Tunable(GroupAction, "탈출 뒤 다시 안 잡히는 시간 (초)")] [Range(0f, 3f)] public float struggleFreeTime = 1f;
        [Tunable(GroupAction, "탈출할 때 튕겨 나가는 속도 (m/s)")] [Range(0f, 6f)] public float struggleEscapePush = 3.5f;
        [Tunable(GroupAction, "버둥 1회 지속 (초)")] [Range(0.05f, 0.8f)] public float struggleBurst = 0.22f;
        [Tunable(GroupAction, "연타 보너스 상한 (배, 흔들림만)")] [Range(1f, 4f)] public float struggleRushBonus = 1.5f;
        [Tunable(GroupAction, "반대로 당기며 연타 (게이지 배)")] [Range(1f, 3f)] public float struggleAwayBonus = 1.35f;
        [Tunable(GroupAction, "점프하며 연타 (게이지 배)")] [Range(1f, 3f)] public float struggleJumpBonus = 1.3f;
        [Tunable(GroupAction, "버둥 1회 스테미나 (초)")] [Range(0f, 2f)] public float struggleStamina = 0.15f;
        [Tunable(GroupAction, "버둥 팔 진폭 (도)")] [Range(0f, 160f)] public float struggleSwing = 95f;
        [Tunable(GroupAction, "버둥 몸부림 흔들림")] [Range(0f, 120f)] public float struggleShake = 35f;
        [Tunable(GroupAction, "버둥 중 팔 강성 배율")] [Range(1f, 20f)] public float struggleArmMultiplier = 8f;
        // The climb moves the body itself (RagdollPawn.UpdateClimb): up at climbSpeed (slower while
        // a hand is in the air, faster once it lands), down and sideways at their own speeds.
        [Tunable(GroupAction, "오르는 속도 (m/s)")] [Range(0.2f, 4f)] public float climbSpeed = 1.2f;
        [Tunable(GroupAction, "내려가는 속도 (m/s)")] [Range(0.2f, 4f)] public float climbDownSpeed = 1.6f;
        [Tunable(GroupAction, "옆으로 가는 속도 (m/s)")] [Range(0.2f, 4f)] public float climbSideSpeed = 0.9f;
        // One pool for everything that costs effort: climbing, sprinting, thrashing and diving.
        // The name stays climbStaminaMax so saved JSON and the asset keep loading.
        [Tunable(GroupAction, "스테미나 최대 (초, 등반·질주·버둥·슬라이딩 공용)")] [Range(1f, 30f)] public float climbStaminaMax = 8f;
        // Drain and recovery are in stamina-seconds per second, so the numbers read directly:
        // hanging 0.35 means the 8 s bar lasts 23 s of just hanging, 6.4 s of full climbing.
        [Tunable(GroupAction, "매달리기 소모 (/초)")] [Range(0f, 1f)] public float climbDrainHold = 0.35f;
        [Tunable(GroupAction, "오르기 추가 소모 (/초)")] [Range(0f, 2f)] public float climbDrainMove = 0.9f;
        [Tunable(GroupAction, "스테미나 회복 (/초)")] [Range(0.05f, 4f)] public float climbRecover = 2f;
        // Short arms, so short quick pats, one hand after the other, rather than long reaches.
        [Tunable(GroupAction, "손 바꿔 짚는 속도 (회/초)")] [Range(0.3f, 12f)] public float climbCadence = 8f;
        // Reaching with one arm lifts that shoulder and drops the other, and the head leans away
        // from the reach. Without these the pawn is a rigid post with arms bolted to it.
        [Tunable(GroupAction, "뻗는 쪽 어깨 올림 (도)")] [Range(0f, 45f)] public float climbShoulderLift = 21f;
        [Tunable(GroupAction, "머리 반대쪽 기울임 (도)")] [Range(0f, 45f)] public float climbHeadTilt = 22f;
        [Tunable(GroupAction, "등반 가능 경사 (도, 수평 기준)")] [Range(30f, 89f)] public float climbGripAngle = 55f;
        // How long climbing over the lip onto the top takes (up past the edge, then in over it).
        [Tunable(GroupAction, "꼭대기로 올라서기 (초)")] [Range(0.1f, 1.2f)] public float climbTopOut = 0.45f;
        // The arms are 0.19 m from shoulder to palm and the head (a 0.195 m ball) sticks out further
        // than that, so the body hugs the face as closely as the head allows and the hands pat the
        // wall beside the chin, hand over hand, instead of reaching over the head. A reaching hand
        // plants climbHandStep above the shoulder, climbHandSpread out beyond it.
        [Tunable(GroupAction, "벽과 몸 사이 거리 (m)")] [Range(0.18f, 0.45f)] public float climbHug = 0.23f;
        [Tunable(GroupAction, "어깨 위로 짚는 높이 (m)")] [Range(0f, 0.7f)] public float climbHandStep = 0.12f;
        [Tunable(GroupAction, "손 좌우 벌림 (m)")] [Range(0f, 0.5f)] public float climbHandSpread = 0.06f;
        // How far a palm may be from its shoulder on the wall. The arm is drawn out to meet the
        // hand (RagdollVisualSync): 0.19 is its own length, 0.24 a barely visible stretch. The old
        // 0.7 (3.6 times the arm) is what looked grotesque.
        [Tunable(GroupAction, "팔이 늘어나는 최대 길이 (m)")] [Range(0.15f, 1f)] public float climbArmReach = 0.24f;
        // Between one hand letting go and the other catching, the pawn sags a little and then
        // jerks back up. Pure comedy, but it is also what a real climber does.
        [Tunable(GroupAction, "손 바꿀 때 쳐짐 (m)")] [Range(0f, 0.2f)] public float climbSag = 0.035f;
        [Tunable(GroupAction, "손 뻗을 때 오버슛")] [Range(0f, 1f)] public float climbOvershoot = 0.35f;

        // The sprint is the approved big run; these are its gait numbers. The run uses the ones in
        // the pose and weight groups (legSwing, armSwing, runLean, hopCadence), and the pawn blends
        // between the two sets, so each can be tuned without touching the other.
        [Tunable(GroupSprint, "전력질주 걸음 주기 (회/초)")] [Range(0f, 5f)] public float sprintCadence = 3.5f;
        [Tunable(GroupSprint, "전력질주 다리 스윙 (도, 최대 60)")] [Range(0f, 60f)] public float sprintLegSwing = 60f;
        [Tunable(GroupSprint, "전력질주 팔 스윙 (도)")] [Range(0f, 90f)] public float sprintArmSwing = 76f;
        [Tunable(GroupSprint, "전력질주 골반 기울기 (도)")] [Range(0f, 30f)] public float sprintLean = 12f;
        [Tunable(GroupSprint, "전력질주 골반 들기 (m)")] [Range(0f, 0.1f)] public float sprintLift = 0.05f;
        [Tunable(GroupSprint, "전력질주 걸음 들썩임 (m)")] [Range(0f, 0.12f)] public float sprintBob = 0.07f;
        [Tunable(GroupSprint, "전력질주 걸음 좌우 기울기 (도)")] [Range(0f, 20f)] public float sprintRoll = 6f;
        [Tunable(GroupSprint, "전력질주 가속·회전 쏠림 최대 (도)")] [Range(0f, 30f)] public float sprintTurnLean = 15f;
        [Tunable(GroupSprint, "전력질주 상체 앞 숙임 (도)")] [Range(0f, 40f)] public float sprintChestLean = 16f;
        [Tunable(GroupSprint, "전력질주 딛을 때 앞으로 숙임 (도)")] [Range(0f, 15f)] public float sprintDrive = 7f;
        [Tunable(GroupSprint, "전력질주 뒤로 찬 발 바깥으로 (도)")] [Range(0f, 25f)] public float sprintSplay = 22f;
        [Tunable(GroupSprint, "달리기↔전력질주 전환 (/초)")] [Range(0.5f, 20f)] public float sprintBlendSpeed = 4f;
        // Stamina-seconds per second, like the climb: 1.0 empties the 8 s pool in 8 s of sprinting.
        [Tunable(GroupSprint, "전력질주 스테미나 소모 (/초)")] [Range(0f, 3f)] public float sprintDrain = 1f;
        // Running dry leaves the pawn winded: no sprint and no new climb until the pool is back to
        // this fraction. Without it the bar flickers at zero and sprint stutters on and off.
        [Tunable(GroupSprint, "지친 뒤 다시 쓸 수 있는 스테미나 (0~1)")] [Range(0f, 1f)] public float sprintResume = 0.3f;
        [Tunable(GroupSprint, "스테미나 회복 시작 대기 (초)")] [Range(0f, 3f)] public float staminaRecoverDelay = 0.8f;
        // Not a feel knob: the fix for pawns hopping on their own. While standing on something and not
        // jumping, the body may not leave the ground faster than this. 1.2 m/s is a 7 cm bump.
        [Tunable(GroupSprint, "저절로 튀어오름 방지 (m/s, 0=끔)")] [Range(0f, 5f)] public float launchClamp = 1.2f;

        // The left click is a dive: the pawn throws itself forward head first with both arms
        // reaching out to shove, lands on its chest and slides, limp everywhere but the arms until
        // it gets up. (It used to be a feet-first slide that caught its skirt and flipped.)
        [Tunable(GroupDive, "앞으로 가속 (m/s)")] [Range(0f, 8f)] public float diveBoost = 3f;
        [Tunable(GroupDive, "위로 뜨기 (m/s)")] [Range(0f, 6f)] public float diveLift = 1.2f;
        [Tunable(GroupDive, "최고 속도 (m/s)")] [Range(3f, 20f)] public float diveMaxSpeed = 10f;
        // Head forward, feet back: the upper body gets this much more forward speed per metre above
        // the hips and the legs this much less per metre below them, so it tips onto its front.
        [Tunable(GroupDive, "앞으로 엎어지는 회전 (rad/s)")] [Range(0f, 15f)] public float diveTip = 5f;
        [Tunable(GroupDive, "옆으로 기우는 회전 (rad/s)")] [Range(0f, 8f)] public float diveSideTip;
        // The arms stay out in front, reaching to shove, while everything else is limp.
        [Tunable(GroupDive, "팔 뻗는 힘 (팔 강성 배)")] [Range(0f, 10f)] public float diveArmStiffness = 5f;
        // Rolling about the direction of travel, taken out per second while diving (so it slides on
        // its front instead of rolling over on its round head).
        [Tunable(GroupDive, "옆으로 구르기 억제 (1/초)")] [Range(0f, 30f)] public float diveRollDamping = 10f;
        [Tunable(GroupDive, "평지에서 마찰 (높을수록 짧게)")] [Range(0f, 1f)] public float diveFriction = 0.35f;
        // Downhill the slide gets slippery and keeps going for as long as it is fast, which is what
        // makes throwing yourself down a hill quicker than running it.
        [Tunable(GroupDive, "내리막에서 마찰")] [Range(0f, 1f)] public float diveSlopeFriction = 0.08f;
        [Tunable(GroupDive, "최소 시간 (초)")] [Range(0.1f, 2f)] public float diveMinTime = 0.7f;
        [Tunable(GroupDive, "평지 최대 시간 (초)")] [Range(0.3f, 4f)] public float diveMaxTime = 1.5f;
        [Tunable(GroupDive, "이 속도 밑으로 느려지면 일어남 (m/s)")] [Range(0f, 6f)] public float diveGetUpSpeed = 1f;
        // And it has to have lain on the floor this long first, so it never stands up in mid-flop.
        [Tunable(GroupDive, "엎드려 있는 최소 시간 (초)")] [Range(0f, 1.5f)] public float diveLieTime = 0.35f;
        [Tunable(GroupDive, "재사용 대기 (초)")] [Range(0f, 2f)] public float diveCooldown = 0.35f;
        [Tunable(GroupDive, "스테미나 소모 (초)")] [Range(0f, 3f)] public float diveStamina = 0.6f;
        // Lower than knockdownImpulseThreshold on purpose: a tackle should floor someone that an
        // ordinary bump would not.
        [Tunable(GroupDive, "태클 넉다운 속도 (m/s)")] [Range(0.5f, 15f)] public float diveTackleImpact = 3f;
        [Tunable(GroupDive, "태클 추가 밀기 (m/s)")] [Range(0f, 8f)] public float diveTacklePush = 2.5f;
        // How long the tackled pawn stays down once it has landed (0 = the usual getUpDelay).
        [Tunable(GroupDive, "태클 맞은 상대 누워 있기 (초)")] [Range(0f, 4f)] public float diveTackleHold = 1.2f;
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
