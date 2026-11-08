using System;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Pawn Rush pieces' own movement numbers (seoungtest, 10-30, 승규 님: "소드 파이트 물리는 건들지 말고 폰 러쉬만
    /// … 넘어짐 · 뜀 · 장애물 오르고 내릴 때"). The shared RagdollTuning stays as it is for Sword Fight and the other
    /// modes; a pawn only moves by these while it holds them (<see cref="RagdollPawn.PawnRushPhysics"/>, handed out by
    /// PawnRushCourseSkills on the Pawn Rush course and the seoungtest scene). Shown in the Inspector on the
    /// "Pawn Rush skills (course)" object during Play, so they can be tried live.
    /// </summary>
    [Serializable]
    public sealed class PawnRushPhysicsParams
    {
        // The jump. The shared 3.8 m/s under plain gravity rose 0.74 m and hung 0.77 s, which read as floaty.
        // Stronger gravity and a stronger kick go a little higher and come back sooner. R246 had 5.4 m/s under 1.7 g
        // (x1.25 falling); 승규 님 found that pull too strong (R247), so 4.9 m/s under 1.4 g (x1.15 falling): the same
        // 0.87 m up, about 0.7 s in the air.
        [Tooltip("점프 (m/s)")] [Range(0f, 15f)] public float jumpSpeed = 4.9f;
        [Tooltip("공중 중력 배율 (1 = 기본)")] [Range(1f, 3f)] public float gravityScale = 1.4f;
        [Tooltip("떨어질 때 중력 더 (배, 1 = 같음)")] [Range(1f, 2f)] public float fallGravity = 1.15f;

        // R513 (the user, 11-09: "점프 높이가 매번 다른 문제 … 폴가이즈를 레퍼런스로"). Fall Guys' jump is the same arc every time
        // and a press a moment before landing still goes. Here the jump tops the hips out the same height over the floor
        // wherever the stride had them (the sprint's bound rides them up to 0.12 m higher), anything but gravity that
        // changes the climb on the way up comes off again (a foot kicking the floor as the jump pose swings the legs), and
        // a press up to jumpBuffer before the pawn can jump goes as soon as it can.
        [Tooltip("점프 높이 고정: 발 디딘 바닥에서 늘 같은 높이 (켜기)")] public bool jumpExact = true;
        [Tooltip("점프 미리 누르기: 착지 이만큼 전에 누른 점프도 닿자마자 나감 (초, 0 = 끔)")] [Range(0f, 0.3f)] public float jumpBuffer = 0.12f;

        // Walking into a low lip (the 0.15 m curbs, a plank edge) the bell of the body stopped dead against it. Up to
        // this high the pawn hops onto it by itself; the 0.3 m jump stairs still take a jump.
        [Tooltip("저절로 올라가는 턱 높이 (m, 0 = 끔)")] [Range(0f, 0.5f)] public float stepUpHeight = 0.25f;
        // A jump that comes up just short of a ledge top caught the body's edge on the lip and dropped it back. When
        // the feet are this close under the top, it is carried over.
        [Tooltip("점프로 모서리에 걸렸을 때 넘겨 주는 높이 (m, 0 = 끔)")] [Range(0f, 0.5f)] public float ledgeAssistHeight = 0.2f;
        [Tooltip("모서리 도움: 이보다 빠르게 오르는 중이면 아직 안 함 (m/s)")] [Range(0f, 5f)] public float ledgeAssistMaxRise = 1f;
        [Tooltip("턱을 찾는 거리 (골반 중심에서, m)")] [Range(0.2f, 1f)] public float stepProbe = 0.4f;
        [Tooltip("턱 위로 더 띄우기 (m)")] [Range(0f, 0.2f)] public float stepClearance = 0.06f;
        [Tooltip("턱을 넘을 때 앞으로 최소 속도 (m/s)")] [Range(0f, 5f)] public float stepForward = 1.5f;

        // Stepping off an edge the floor under the hips is the low one while a foot is still on the top; the standing
        // spring pulled the hips down at the low floor and pitched the body over the lip. It stands on the highest
        // floor under the hips or a foot instead, and falls once both feet are off.
        [Tooltip("내려갈 때 발밑 높이 기준 (켜기)")] public bool standOnFeet = true;

        // R398 (승규 님 11-04: "턱 정도 폰 발 정도 있는 건물에서 붙어서 점프 하면 뒤로 밀려나 … 미니 게임에도 같은 증상").
        // Pressed against a lip lower than the chest, the locomotion anchor stood the whole standing leash (0.6 m) inside
        // it - the chest ray that notices walls passes over a lip - and a jump cut that to the air leash (0.12 m) in one
        // step: the kinematic anchor went back at 24 m/s and the hips joint's damper threw the pawn backwards with it.
        // On: a lip from a kerb up counts as blocked, as a wall does (the anchor eases in to the blocked leash), and off the
        // ground the anchor comes back no faster than its damper can take.
        [Tooltip("낮은 턱에 붙어 점프해도 뒤로 안 튕김 (켜기)")] public bool lipNoRecoil = true;

        // The run's arms (R246, 승규 님: "팔도 실제 뛰는 것처럼"). The shared run held the arms out at 35 degrees and the
        // sprint flailed them round the vertical; here they hang by the sides and swing forward and back against the
        // legs, further the faster the pawn goes, a little forward of straight down as a runner's do.
        // 65 from the horizontal is 25 degrees out from the body's side (R247, 승규 님: "팔을 옆으로 25도"), every piece.
        [Tooltip("뛸 때 팔 내림 (수평에서 아래로, 도; 65 = 몸 옆에서 25도 벌림)")] [Range(20f, 85f)] public float runArmDown = 65f;
        // R253 (승규 님: "폰 손이 몸이랑 너무 붙어 있어"): the pawn's bell-shaped skirt is wider than the other pieces', so
        // at 25 degrees its mittens lay against it. The pawn opens this much further: 55 degrees out from its side (20 still touched it, 40 looked like a T).
        [Tooltip("폰만 팔을 더 벌리기 (도, 0 = 다른 기물과 같음)")] [Range(0f, 45f)] public float pawnArmSpread = 30f;
        [Tooltip("달리기 팔 앞뒤 흔들기 (도)")] [Range(0f, 90f)] public float runArmSwing = 45f;
        [Tooltip("전력질주 팔 앞뒤 흔들기 (도)")] [Range(0f, 90f)] public float sprintArmSwing = 65f;
        [Tooltip("팔 앞으로 치우침 (도)")] [Range(-20f, 40f)] public float runArmForward = 12f;

        // The jump pose follows the jump: on the way up one knee drives up and the opposite arm swings up with it (both
        // arms and knees, from standing); on the way down the arms open out for balance and the legs reach for the floor.
        [Tooltip("점프 자세 바뀌는 속도 기준 (m/s)")] [Range(0.5f, 6f)] public float jumpPoseSpeed = 3f;

        // The dive (left click). The shared one goes limp but for the arms and topples, which reads as a fall. Here the
        // body holds itself straight like a plank, arms out in front, head up, legs together behind, springs off the
        // floor and lands on its chest to slide.
        [Tooltip("다이빙 앞으로 가속 (m/s)")] [Range(0f, 8f)] public float diveBoost = 3f;
        [Tooltip("다이빙 위로 뜨기 (m/s)")] [Range(0f, 6f)] public float diveLift = 2.2f;
        [Tooltip("다이빙 최고 속도 (m/s)")] [Range(3f, 20f)] public float diveMaxSpeed = 8f;
        [Tooltip("다이빙 중 몸 곧게 (0 = 흐물, 1 = 서 있을 때만큼)")] [Range(0f, 1f)] public float diveBodyStiffness = 0.5f;
        // R250 (승규 님: "고개 들고 다이빙 하고 손은 다이빙 하듯이 위로 그리고 착지 할 때 착지 동작"): in the air both arms go
        // straight up past the head, the head held up; down on the floor the hands come down ahead to catch it.
        [Tooltip("다이빙 고개 들기 (도)")] [Range(0f, 70f)] public float diveHeadUp = 45f;
        [Tooltip("다이빙 착지 자세로 바뀌는 시간 (초)")] [Range(0.02f, 0.5f)] public float diveLandBlend = 0.12f;
        // R513 (the user: "다이빙 할 때 캐릭터가 높이서 떨어진다고 공중에서 공중제비가 돌아지면 안돼. 높이가 어쨌든 배치기하는 형태로
        // 낙하"). The shared dive tips the body over at diveTip (5 rad/s) and lets it go: a dive from a jump's top or off a
        // ledge kept turning, past flat, over its head and onto its back. Here the body turns until it lies flat on its
        // front along the dive and is held there, in the air however long it falls and sliding on the floor (Fall Guys'
        // belly flop), and the dive does not end while it is still up in the air.
        [Tooltip("다이빙: 몸을 배로 수평하게 잡아 공중제비 없이 (켜기)")] public bool diveHoldFlat = true;
        [Tooltip("다이빙 몸을 수평으로 돌리는 세기 (남은 각도의 초당 몇 배)")] [Range(1f, 30f)] public float diveTurnGain = 9f;
        [Tooltip("다이빙 몸 돌리는 최고 속도 (rad/s)")] [Range(1f, 30f)] public float diveTurnRate = 9f;
        [Tooltip("다이빙 자세: 수평에서 머리 숙이기 (도, + = 머리 아래)")] [Range(-30f, 30f)] public float diveFlatPitch = 0f;
        [Tooltip("점프 착지 자세 시간 (초)")] [Range(0f, 0.8f)] public float landPoseTime = 0.25f;

        // The slide and after it (R248, 승규 님: "슬라이딩 하고 난 후 조금 쓸려야 돼"). On its front the body slides on
        // further (the shared 0.35 stopped it short), and getting up at speed it skids on its feet a moment before they
        // catch, instead of standing up on the spot.
        [Tooltip("다이빙 배로 미끄러질 때 마찰 (낮을수록 멀리)")] [Range(0f, 1f)] public float diveSlideFriction = 0.15f;
        [Tooltip("일어나며 남기는 속도 (배)")] [Range(0f, 1f)] public float skidKeep = 0.9f;
        [Tooltip("일어난 뒤 쓸리는 시간 (초)")] [Range(0f, 1.5f)] public float skidTime = 0.5f;
        [Tooltip("쓸리는 동안 멈추는 힘 (m/s²)")] [Range(1f, 30f)] public float skidDeceleration = 7f;

        // Walk and run (R248, 승규 님: "걷기 동작과 뛰기 동작 자연스럽게"). The shared gait steps at one fixed rate that
        // only shrinks toward a stop, and swings the legs the hip's full 60 degrees at any speed. Here a slow pawn takes
        // slow short steps, the run quicker and longer ones, the sprint the quickest and longest, and the feet roll heel
        // to toe.
        [Tooltip("걷기 걸음 (초당)")] [Range(0.5f, 5f)] public float walkCadence = 1.8f;
        [Tooltip("달리기 걸음 (초당)")] [Range(0.5f, 5f)] public float runCadence = 2.6f;
        [Tooltip("전력질주 걸음 (초당)")] [Range(0.5f, 5f)] public float sprintCadence = 3.3f;
        [Tooltip("걷기 다리 흔들기 (도)")] [Range(0f, 60f)] public float walkLegSwing = 25f;
        [Tooltip("달리기 다리 흔들기 (도)")] [Range(0f, 60f)] public float runLegSwing = 42f;
        [Tooltip("전력질주 다리 흔들기 (도)")] [Range(0f, 60f)] public float sprintLegSwing = 56f;
        [Tooltip("발 굴리기 (뒤꿈치 → 앞꿈치, 도)")] [Range(0f, 40f)] public float footRoll = 15f;

        // Getting up (R249, 승규 님: "일어날 때 속도를 빨리"). Shared: 1 s down after a knockdown, 0.22 s to stand, then
        // 0.4 s before the run is let go; a dive lies at least 0.7 s (0.51 s flat). All shorter here.
        [Tooltip("넘어진 뒤 일어나기까지 (초)")] [Range(0f, 3f)] public float getUpDelay = 0.6f;
        // R252 (승규 님: "다이빙 후 일어나는 속도를 지금보다 빠르게"): the dive's lie, the stand-up and the wait before the run
        // all shorter again, and the slide (now longer, R248) no longer has to slow under 1 m/s first - the pawn gets
        // up out of it under 3 m/s and skids on its feet the rest of the way.
        [Tooltip("일어나는 데 걸리는 시간 (초)")] [Range(0.05f, 1f)] public float getUpBlendTime = 0.1f;
        [Tooltip("일어난 뒤 달리기까지 기다림 (초)")] [Range(0f, 1f)] public float upSettleTime = 0.1f;
        [Tooltip("다이빙 최소 누워 있기 (초)")] [Range(0.1f, 2f)] public float diveMinTime = 0.3f;
        [Tooltip("다이빙 배를 대고 있는 시간 (초)")] [Range(0f, 2f)] public float diveLieTime = 0.1f;
        [Tooltip("다이빙: 이보다 느리면 일어남 (m/s)")] [Range(0.5f, 6f)] public float diveGetUpSpeed = 3f;
        [Tooltip("다이빙 최대 시간 (초, 이 뒤엔 미끄러지는 중이어도 일어남)")] [Range(0.3f, 3f)] public float diveMaxTime = 0.7f;

        // Slopes (R249, "경사로에서 뛰거나 걷는게 약간 부자연스러워"). Shared: the body went 40 % softer on any slope over
        // 20 degrees and wobbled, stood bolt upright, and swung its legs as on the flat (uphill toe in the ground).
        [Tooltip("경사에서 몸 힘 (1 = 평지와 같음)")] [Range(0.3f, 1f)] public float slopeStiffness = 1f;
        [Tooltip("오르막에서 앞으로 기울기 (경사의 몇 배)")] [Range(0f, 1f)] public float slopeLeanUp = 0.35f;
        [Tooltip("내리막에서 뒤로 기울기 (경사의 몇 배)")] [Range(0f, 1f)] public float slopeLeanDown = 0.2f;
        [Tooltip("다리 흔들기를 경사에 맞추기 (0~1)")] [Range(0f, 1f)] public float slopeLegFollow = 0.6f;
    }
}
