using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Films the Queen of the Hill skills for review (R89): plays the probe's runs from a fixed camera per shot, steps game
    /// time 1/fps per frame (Time.captureFramerate) so the video plays at true speed however long a frame takes, and hands
    /// every frame to whoever listens (in the editor QueenHillSkillFilmEncoder writes the .mp4). Two films in one go:
    /// every shot at full speed, then every shot again in the lab's slow motion (×0.3) from the moment it is staged.
    /// Every StillEvery-th frame is kept as a .jpg next to each video (1 = every frame, to look at an effect frame by
    /// frame). A caption at the top names the skill, the key the probe presses shows at the bottom (only in the film).
    /// </summary>
    [DefaultExecutionOrder(300)]   // after the effects shake the camera (210)
    public class QueenHillSkillFilm : MonoBehaviour
    {
        public static event Action<string, int, int, int> Began;
        public static event Action<Texture2D> Frame;
        public static event Action Ended;
        public static string Status { get; private set; } = "idle";
        public static int Frames { get; private set; }
        public static int StillEvery = 10;
        /// <summary>R141 (승규 님 10-14: "동영상은 찍지 마", as PawnRushSkillFilm's): keep the stills only and hand no frame on, so
        /// no video is written.</summary>
        public static bool StillsOnly;
        /// <summary>A film is recording this frame (the bed hides its panel).</summary>
        public static bool Rolling { get; private set; }
        /// <summary>Per shot: the frame its takes began on, in each film (to find a shot among the stills).</summary>
        public static readonly List<string> Index = new List<string>();

        struct Shot
        {
            public string run, title, note;
            public Vector3 eye, look;
            public float fov;
        }

        static readonly Shot[] Shots =
        {
            new Shot { run = "king", title = "1. 킹 · 근접 호위 (B: 킹 자신은 빠짐)", note = "에메랄드 물결 → 아군 발밑 호위 고리 · 적 퀸 검격에 아군은 버티고(초록 고리) 킹만 넘어짐",
                eye = new Vector3(3.8f, 4.6f, -7.2f), look = new Vector3(3.6f, 0.3f, -2.3f), fov = 42f },
            new Shot { run = "queen", title = "2. 퀸 · 팔방 검격 (A: 한 방향 긴 검격)", note = "체스판 4칸이 차오름 → 금색 검기가 날아감 · 줄 위 적 넘어짐 · 벽에 막혀 뒤는 안전",
                eye = new Vector3(3.9f, 4.8f, -3.2f), look = new Vector3(3.7f, 0.3f, 3.3f), fov = 44f },
            new Shot { run = "rook", title = "3. 룩 · 캐슬링 교대 (B: 아군 누구나, 수락 받고)", note = "아군 폰을 골라 요청 → 수락(시계가 참) → 함정문 열리고 두 색 리본 아치로 자리 바꿈",
                eye = new Vector3(1.4f, 4.2f, -0.6f), look = new Vector3(-4.3f, 1.3f, 4.9f), fov = 46f },
            new Shot { run = "bishop", title = "4. 비숍 · 교차 공중 포격 (B: 맞으면 밀려 넘어짐)", note = "보라 마름모 칸 위로 떠오름 → 돌 던짐 → X자 파동 → 벽을 오르던 적이 떨어짐",
                eye = new Vector3(-0.8f, 3.0f, -3.8f), look = new Vector3(-1.4f, 1.0f, 3.4f), fov = 46f },
            new Shot { run = "knight", title = "5. 나이트 · 도약 압착 (B: 한 층까지, 높은 곳에서 납작하면 떨어짐)", note = "2층은 너무 높아 회색 → 1층 말굽 표식 → 도약 → 적이 납작해져 가장자리에서 떨어짐",
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
            new Shot { run = "pawn", title = "6. 폰 · 비집고 돌파 (B: 몸을 낮추는 새 동작)", note = "적 룩 돌진 경고(빨간 줄무늬 칸) → 다리를 앞뒤로 벌리고 낮게 옆으로 빠짐 → 룩이 빗나감",
                eye = new Vector3(-4.2f, 1.9f, -4.0f), look = new Vector3(2.4f, 0.4f, -4.6f), fov = 44f },
            new Shot { run = "pawn-squeeze", title = "6. 폰 · 비집고 돌파 (좁은 틈)", note = "낮게 몸을 숙이고 두 적 사이를 비집고 지나감 · 무적 없음, 부딪힌 적은 옆으로 비킴",
                eye = new Vector3(-0.4f, 2.4f, -9.4f), look = new Vector3(0.6f, 0.3f, -5f), fov = 40f },
        };

        /// <summary>R93 (승규 님's notes on R89): only what changed.</summary>
        static readonly Shot[] ShotsR93 =
        {
            new Shot { run = "king-help", title = "1. 킹 · 근접 호위 — 시험 도우미 · 몸 윤곽선", note = "F → 아군 폰 3명이 앞에, 적 퀸이 5 m 앞에 섬 · 호위 받은 아군은 몸에 초록 윤곽선 → 검격에 아군은 버티고 킹만 넘어짐",
                eye = new Vector3(2.0f, 2.8f, -7.0f), look = new Vector3(3.2f, 0.4f, -2.5f), fov = 50f },
            new Shot { run = "queen", title = "2. 퀸 · 팔방 검격 — 아래에서 위로 올려 베기", note = "칼을 오른쪽 아래 뒤로 내려 잡고(바닥을 뚫지 않음) → 칼이 앞을 지날 때 검기 → 왼쪽 위로",
                eye = new Vector3(0.6f, 1.5f, -0.2f), look = new Vector3(1.3f, 0.5f, 3.0f), fov = 56f },
            new Shot { run = "rook-up-bishop", title = "3. 룩 · 캐슬링 교대 — 기물 고유색 리본 (비숍)", note = "F → 시험 도우미가 한 층 위에 아군 1명(기물 무작위) · 룩 주황 반 + 비숍 보라 반, 가운데 그라데이션",
                eye = new Vector3(0.6f, 2.2f, 1.4f), look = new Vector3(-3.2f, 0.9f, 6.0f), fov = 46f },
            new Shot { run = "rook-up-queen", title = "3. 룩 · 캐슬링 교대 — 기물 고유색 리본 (퀸)", note = "룩 주황 반 + 퀸 금색 반, 가운데 그라데이션",
                eye = new Vector3(0.6f, 2.2f, 1.4f), look = new Vector3(-3.2f, 0.9f, 6.0f), fov = 46f },
            new Shot { run = "rook-up-knight", title = "3. 룩 · 캐슬링 교대 — 기물 고유색 리본 (나이트)", note = "룩 주황 반 + 나이트 하늘색 반, 가운데 그라데이션",
                eye = new Vector3(0.6f, 2.2f, 1.4f), look = new Vector3(-3.2f, 0.9f, 6.0f), fov = 46f },
            new Shot { run = "knight", title = "5. 나이트 · 도약 압착 — 더 높게, 머리 위 자동 조준", note = "2층은 너무 높아 회색 → 적 근처를 조준하면 머리 위에 말굽 표식 → 높게 도약해 머리를 밟고 납작",
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
        };

        /// <summary>R94 (승규 님's notes on R93): the rook's longer reach and seamless arch, the knight's tighter catch.</summary>
        static readonly Shot[] ShotsR94 =
        {
            new Shot { run = "rook-far", title = "3. 룩 · 캐슬링 교대 — 범위 9 m", note = "6 m → 9 m(체스판 6칸) · 8.4 m 떨어진 아군 폰과 교대",
                eye = new Vector3(4.0f, 2.6f, -10.5f), look = new Vector3(4.0f, 0.8f, -3.5f), fov = 56f },
            new Shot { run = "rook-up-bishop", title = "3. 룩 · 캐슬링 교대 — 가운데 빈틈 없앰 (비숍)", note = "리본을 한 줄로 그려 두 색이 만나는 곳에 틈이 없음 · 룩 주황 → 비숍 보라 그라데이션",
                eye = new Vector3(0.6f, 2.2f, 1.4f), look = new Vector3(-3.2f, 0.9f, 6.0f), fov = 46f },
            new Shot { run = "rook-up-knight", title = "3. 룩 · 캐슬링 교대 — 가운데 빈틈 없앰 (나이트)", note = "룩 주황 → 나이트 하늘색 그라데이션, 꼭대기에서 반반",
                eye = new Vector3(0.6f, 2.2f, 1.4f), look = new Vector3(-3.2f, 0.9f, 6.0f), fov = 46f },
            new Shot { run = "knight", title = "5. 나이트 · 도약 압착 — 머리 자동 조준 1.15 m", note = "적에서 1.24 m 떨어진 곳은 안 잡힘 → 0.7 m로 옮기면 머리 위에 말굽 표식 → 머리를 밟고 납작",
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
        };

        /// <summary>R95 (승규 님's notes on R94): the queen's warning squares on the slash's own line, the bishop's two
        /// shots, the knight's catch at 1.05 m.</summary>
        static readonly Shot[] ShotsR95 =
        {
            new Shot { run = "queen-high", title = "2. 퀸 · 팔방 검격 — 탑 위에서 (경고 칸 = 실제 검기)", note = "검기는 퀸 높이에서 똑바로 → 경고 칸도 그 높이로 떠서 이어짐 · 같은 층 적만 넘어짐, 아래층·바닥 적 위로는 지나감",
                eye = new Vector3(1.5f, 3.6f, 2.5f), look = new Vector3(-5.0f, 1.4f, 6.5f), fov = 50f },
            new Shot { run = "queen-low", title = "2. 퀸 · 팔방 검격 — 아래에서 벽 쪽으로 (반대 경우)", note = "경고 칸이 검기처럼 벽에서 멈춤(위층에 안 그려짐) · 앞의 적만 넘어짐, 벽 위 1층 적은 그대로",
                eye = new Vector3(0.4f, 2.0f, -1.6f), look = new Vector3(-2.8f, 0.6f, 3.2f), fov = 52f },
            new Shot { run = "bishop", title = "4. 비숍 · 교차 공중 포격 — 2발", note = "1발: 벽을 오르던 적 → 떨어지면 다시 조준 → 2발: 1층 적 · 2발 쏘거나 시간이 다 되면 내려옴",
                eye = new Vector3(-0.8f, 3.0f, -3.8f), look = new Vector3(-1.8f, 1.0f, 3.8f), fov = 50f },
            new Shot { run = "knight", title = "5. 나이트 · 도약 압착 — 머리 자동 조준 1.05 m", note = "적에서 1.12 m 떨어진 곳은 안 잡힘 → 0.7 m로 옮기면 머리 위 말굽 → 머리를 밟고 납작",
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
        };

        /// <summary>R97 (승규 님: the queen's slash goes where its squares are): down from a tier, up onto one, and a real wall
        /// still stops it.</summary>
        static readonly Shot[] ShotsR97 =
        {
            new Shot { run = "queen-high", title = "2. 퀸 · 팔방 검격 — 위에서 아래로", note = "경고 칸이 아래층·바닥을 따라 깔림 → 검기도 칸을 따라 내려가 같은 층·아래층·바닥 적 모두 넘어짐 · 줄 끝 쪽으로 밀림",
                eye = new Vector3(1.5f, 3.6f, 2.5f), look = new Vector3(-5.0f, 1.4f, 6.5f), fov = 50f },
            new Shot { run = "queen-low", title = "2. 퀸 · 팔방 검격 — 아래에서 위로 (반대)", note = "검기가 한 층(0.9 m) 턱을 타고 올라감 → 앞의 적도, 턱 위 1층 적도 넘어짐",
                eye = new Vector3(0.4f, 2.0f, -1.6f), look = new Vector3(-2.8f, 0.6f, 3.2f), fov = 52f },
            new Shot { run = "queen", title = "2. 퀸 · 팔방 검격 — 높은 벽은 그대로 막힘", note = "한 층보다 높은 돌벽(1.6 m)은 막힘 · 벽 뒤 적은 안전, 줄 밖 적도 그대로",
                eye = new Vector3(3.9f, 4.8f, -3.2f), look = new Vector3(3.7f, 0.3f, 3.3f), fov = 44f },
        };

        /// <summary>R98 (승규 님's screenshots on the green slopes: the squares stood flat, half in the slope): the squares lie on
        /// the slope and the slash runs up and down it; then the hill again, down and up.</summary>
        static readonly Shot[] ShotsR105 =
        {
            new Shot { run = "bishop-wait", title = "4. 비숍 · 교차 공중 포격 — 양탄자처럼 천천히, 안 쏘면 취소, 2발째 기다림", note = "떠서 W로 천천히 이동 → 3초 안 쏘면 내려옴 → 다시 떠서 1발 → 5초 넘게 기다려도 안 나감 → 2발",
                eye = new Vector3(-1.0f, 3.4f, -5.5f), look = new Vector3(-1.0f, 1.2f, 4.5f), fov = 55f },
        };

        static readonly Shot[] ShotsR98 =
        {
            new Shot { run = "queen-slope-up", title = "2. 퀸 · 팔방 검격 — 경사로 아래에서 위로", note = "경고 칸이 경사로에 붙어 깔림 → 검기가 경사를 타고 올라가 칸 위 적 둘 다 넘어짐",
                eye = new Vector3(-11.8f, 2.6f, -11.8f), look = new Vector3(-19.5f, 0.8f, -9f), fov = 50f },
            new Shot { run = "queen-slope-down", title = "2. 퀸 · 팔방 검격 — 경사로 위에서 아래로", note = "경고 칸이 경사로에 붙어 깔림 → 검기가 경사를 타고 내려가 칸 위 적 둘 다 넘어짐",
                eye = new Vector3(-16.0f, 2.8f, -15.0f), look = new Vector3(-20.0f, 0.7f, -9f), fov = 50f },
            new Shot { run = "queen-high", title = "2. 퀸 · 팔방 검격 — 탑 위에서 아래로", note = "칸이 아래층·바닥에 깔림 → 검기가 칸을 따라 내려가 같은 층·아래층·바닥 적 모두 넘어짐",
                eye = new Vector3(1.5f, 3.6f, 2.5f), look = new Vector3(-5.0f, 1.4f, 6.5f), fov = 50f },
            new Shot { run = "queen-low", title = "2. 퀸 · 팔방 검격 — 탑 아래에서 위로", note = "검기가 한 층(0.9 m) 턱을 타고 올라감 → 앞의 적도, 턱 위 1층 적도 넘어짐",
                eye = new Vector3(0.4f, 2.0f, -1.6f), look = new Vector3(-2.8f, 0.6f, 3.2f), fov = 52f },
        };

        /// <summary>R130 (승규 님 10-13): the king's longer guard, chess sceptre and crown (no rings under the allies), the
        /// rook's castling shown as the swap itself (no coloured pads), the bishop's carpet and its soft shadow, the
        /// knight rearing and leaping like a horse under a solid horseshoe.</summary>
        static readonly Shot[] ShotsR130 =
        {
            new Shot { run = "king-help", title = "1. 킹 · 근접 호위 — 체스 홀 · 왕관 (R130)", note = "흑단 막대 + 왕관 머리 홀을 들어 금빛이 모였다가 내리꽂음 → 물결 두 겹 · 머리 위 왕관 · 아군 발밑 고리 없음, 호위 2.9초",
                eye = new Vector3(2.0f, 2.8f, -7.0f), look = new Vector3(3.2f, 0.6f, -2.5f), fov = 50f },
            new Shot { run = "rook", title = "3. 룩 · 캐슬링 교대 — 자리 바꾸기 연출 (R130)", note = "색 칸 없음 · 반투명 룩과 아군 말이 호를 따라 서로 자리로 뛰어 보임 + 머리 위 자리 바꿈 화살표 · 수락을 기다리면 화살표 둘레가 참",
                eye = new Vector3(1.4f, 4.2f, -0.6f), look = new Vector3(-4.3f, 1.3f, 4.9f), fov = 46f },
            new Shot { run = "bishop-wait", title = "4. 비숍 · 양탄자 (R130)", note = "벨벳 양탄자(체스판 무늬 · 금 테두리 · 술)가 출렁이며 떠 감 · 밑에는 부드러운 그림자, 반짝이가 떨어짐",
                eye = new Vector3(-2.6f, 2.4f, -1.8f), look = new Vector3(-1.0f, 1.1f, 2.6f), fov = 48f },
            new Shot { run = "knight", title = "5. 나이트 · 도약 압착 — 말처럼 (R130)", note = "앞발을 들고 일어섰다 웅크려 뜀 → 앞다리를 접고 날아 앞다리부터 내림 · 발굽 자국 · 머리 위 말굽은 진짜 말굽 모양",
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
        };

        /// <summary>R133 (승규 님 10-14): the king's ring lighting and charging as one, the carpet gliding (no more flapping),
        /// the knight's real horseshoe and hoof prints.</summary>
        static readonly Shot[] ShotsR133 =
        {
            new Shot { run = "king-help", title = "1. 킹 · 근접 호위 — 칸이 같이 차오름 (R133)", note = "둘레 24칸이 3분의 1씩 따로 켜지던 것 → 한꺼번에 켜지고 점이 같이 차오름",
                eye = new Vector3(2.0f, 2.8f, -7.0f), look = new Vector3(3.2f, 0.6f, -2.5f), fov = 50f },
            new Shot { run = "bishop-glide", title = "4. 비숍 · 양탄자 — 자연스럽게 미끄러짐 (R133)", note = "펄럭이던 빠른 물결 → 느린 큰 물결 하나 · 몸 떨림을 안 따라가고 천천히 돌며 기울어짐 · 술은 흔들이처럼",
                eye = new Vector3(3.4f, 2.7f, 0.2f), look = new Vector3(0.4f, 1.2f, 3.9f), fov = 46f },
            new Shot { run = "knight", title = "5. 나이트 · 도약 압착 — 진짜 편자 · 발굽 자국 (R133)", note = "머리 위 편자: U자 · 앞쪽이 넓은 띠 · 홈 · 못 구멍 8개 · 바닥 발굽 자국: 편자 자국 + 못 자국 + 가운데 쐐기",
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
        };

        /// <summary>R134 (승규 님 10-14): the bishop's shot made over: the mitre with its gold and gems, the crater it digs,
        /// the X its quarters plough into the floor (wide, up close, and by the hill: stopped by the tier's face, dug into a
        /// tier's board).</summary>
        static readonly Shot[] ShotsR134 =
        {
            new Shot { run = "bishop-dig", title = "4. 비숍 · 교차 공중 포격 — 모자 · 파이는 바닥 (R134)", note = "금 테 · 보석 · 금 구슬 비숍 모자가 빛 꼬리를 끌며 날아감 → 바닥을 깨고 박혀 구덩이 → 네 조각이 칼날처럼 X자 홈을 파며 나감",
                eye = new Vector3(0.6f, 3.0f, -2.4f), look = new Vector3(-1.6f, 0.5f, 3.4f), fov = 50f },
            new Shot { run = "bishop-dig", title = "4. 비숍 · 교차 공중 포격 — 가까이 (R134)", note = "구덩이 안쪽 벽 · 깨진 가장자리 · 둘레 금 · X자 홈(파고 나간 자리가 잠깐 달아오름) · 바닥 조각 · 불꽃 · 시간이 지나면 메워짐",
                eye = new Vector3(-0.5f, 2.5f, 0.2f), look = new Vector3(-1.7f, 0.0f, 2.7f), fov = 46f },
            new Shot { run = "bishop", title = "4. 비숍 · 교차 공중 포격 — 언덕 옆 · 위 (R134)", note = "벽 쪽 홈과 칼날 조각은 벽에서 멈춤 · 언덕 위에서는 체스판(크림 · 호두색) 바닥이 파이고 조각이 튐",
                eye = new Vector3(-1.0f, 2.7f, -0.6f), look = new Vector3(-3.6f, 0.7f, 4.6f), fov = 46f },
        };

        /// <summary>R137 (승규 님 10-14): every floor a chessboard; the bishop's strike breaking the hill (a tier's riser, its
        /// edge, the blades falling off it and ploughing on below) and walls (the stone wall, the lab's blue 3 m wall as in
        /// 승규 님's picture).</summary>
        static readonly Shot[] ShotsR137 =
        {
            new Shot { run = "bishop-hill", title = "4. 비숍 · 교차 공중 포격 — 언덕이 부서짐 (R137)", note = "1발: 2층 턱 옆 → 턱 아래가 깨져 나가고 칼날 둘이 턱에 박힘 · 두 칼날은 1층 끝에서 떨어져 바닥까지 홈을 파며 계속 · 2발: 1층 가장자리가 깨져 떨어짐",
                eye = new Vector3(-0.3f, 3.5f, 2.6f), look = new Vector3(-4.7f, 0.6f, 5.7f), fov = 52f },
            new Shot { run = "bishop-hill", title = "4. 비숍 · 교차 공중 포격 — 언덕 가까이 (R137)", note = "체스판 칸이 깨진 속(판 두께 · 호두색 속) · 턱 모서리가 칼날에 깨짐 · 아래층에 떨어진 칼날 자국 · 조각 · 먼지",
                eye = new Vector3(-2.4f, 2.2f, 2.0f), look = new Vector3(-5.0f, 0.7f, 4.4f), fov = 44f },
            new Shot { run = "bishop-wall", title = "4. 비숍 · 교차 공중 포격 — 돌벽이 부서짐 (R137)", note = "벽 바로 옆에 떨어짐 → 벽 아랫부분이 크게 깨져 나가고 금이 감 · 큰 조각이 튀고 화면이 크게 흔들림 · 칼날 둘이 벽에 박힘",
                eye = new Vector3(10.2f, 2.3f, -0.4f), look = new Vector3(7.0f, 0.5f, 3.0f), fov = 46f },
            new Shot { run = "bishop-wall", title = "4. 비숍 · 교차 공중 포격 — 파란 3 m 벽 (R137)", note = "사진의 자리: 구덩이가 벽에서 잘리지 않고 벽 속까지 깨짐 · 홈도 벽에 박힌 칼날까지 이어짐",
                eye = new Vector3(16.3f, 2.4f, -2.6f), look = new Vector3(19.9f, 0.6f, 0.8f), fov = 46f },
            new Shot { run = "bishop-dig", title = "4. 비숍 · 교차 공중 포격 — 체스판 바닥 (R137)", note = "모든 바닥이 체스판(크림 · 호두색 1.5 m 칸) · 구덩이와 X자 홈 안은 판 두께 아래 호두색 속",
                eye = new Vector3(-0.5f, 2.5f, 0.2f), look = new Vector3(-1.7f, 0.0f, 2.7f), fov = 46f },
        };

        /// <summary>R141 (승규 님 10-14): the air a springing piece shoves back (the pawn's dash, the knight's leap), the castling
        /// told by the pieces (no ribbon or trapdoors) and its beaded arc, the bishop's beaded arc with no X on the floor, the
        /// knight's steel horseshoe and the Pawn Rush badge where it lands.</summary>
        static readonly Shot[] ShotsR141 =
        {
            new Shot { run = "rook", title = "3. 룩 · 캐슬링 교대 — 새 연출 (R141)", note = "리본 아치 · 함정문 없음 · 조준 점은 광택 은구슬 · 둘이 공기와 먼지를 차고 뛰어 엇갈릴 때 번쩍·멈춤·공기 · 쿵 내려앉음",
                eye = new Vector3(1.4f, 4.2f, -0.6f), look = new Vector3(-4.3f, 1.3f, 4.9f), fov = 46f },
            new Shot { run = "bishop", title = "4. 비숍 · 교차 공중 포격 — 조준 (R141)", note = "바닥 X 표시 없음 · 손에서 모자까지 광택 금구슬 호, 빛이 흘러감",
                eye = new Vector3(-0.8f, 3.0f, -3.8f), look = new Vector3(-1.8f, 1.0f, 3.8f), fov = 50f },
            new Shot { run = "knight", title = "5. 나이트 · 도약 압착 (R141)", note = "편자는 실제 쇠 색 · 뛸 때 엉덩이 뒤로 공기 임팩트(바람 띠 · 흰 고리 없음) · 착지에만 폰 러쉬 검정·금 편자 로고",
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
            new Shot { run = "pawn", title = "6. 폰 · 비집고 돌파 (R141)", note = "빠질 때 엉덩이 뒤로 공기 임팩트(그린 쐐기 없음) · 적 룩 돌진도 공기",
                eye = new Vector3(-4.2f, 1.9f, -4.0f), look = new Vector3(2.4f, 0.4f, -4.6f), fov = 44f },
            // The same from behind the piece, where the player's camera is.
            new Shot { run = "knight-land", title = "5. 나이트 · 빈 바닥 착지 — 뒤에서 (R141)", note = "뛸 때 엉덩이 뒤로 공기 임팩트 · 빈 바닥에 내려앉으면 폰 러쉬 검정·금 편자 로고",
                eye = new Vector3(-7.6f, 2.0f, -7.0f), look = new Vector3(-0.5f, 0.4f, -7.0f), fov = 55f },
            new Shot { run = "knight-land", title = "5. 나이트 · 빈 바닥 착지 — 옆에서 (R141)", note = "착지에만 로고: 광택 검정 테두리 · 광택 어두운 금색 · 빛이 한 번 쓸고 감",
                eye = new Vector3(-1.2f, 3.0f, -11.6f), look = new Vector3(-0.6f, 0.0f, -7.0f), fov = 44f },
            new Shot { run = "pawn", title = "6. 폰 · 비집고 돌파 — 뒤에서 (R141)", note = "빠질 때 엉덩이 뒤로 공기 임팩트",
                eye = new Vector3(-2.0f, 1.9f, -3.5f), look = new Vector3(5.0f, 0.4f, -3.6f), fov = 55f },
            new Shot { run = "rook", title = "3. 룩 · 캐슬링 교대 — 뒤에서 (R141)", note = "조준 호의 광택 은구슬 · 엇갈릴 때 번쩍 · 쿵",
                eye = new Vector3(-9.0f, 4.6f, 3.0f), look = new Vector3(-3.2f, 0.8f, 4.9f), fov = 55f },
            new Shot { run = "bishop", title = "4. 비숍 · 조준 — 뒤에서 (R141)", note = "바닥 X 없음 · 손에서 모자까지 광택 금구슬",
                eye = new Vector3(4.8f, 2.4f, 3.3f), look = new Vector3(-3.0f, 0.9f, 3.5f), fov = 55f },
        };

        /// <summary>Which shots Run films: "r141" (the default, R141's changes), "r137", "r134", "r133", "r130", "r105", "r98", "r97",
        /// "r95", "r94", "r93" or "r89" (all six skills).</summary>
        public static string ShotSet = "r141";

        LabGame game;
        Camera cam;
        RenderTexture rt;
        Texture2D frame;
        Font font;
        PawnRushSkillFx.Text3D title, note, key;
        Transform banner, keyStrip;
        int keySerial;
        float keyLeft;
        Vector3 eye, look;
        string stills;
        int width, height;
        // The captions hang this far in front of the camera, so that every shot's field of view shows them at the same
        // spot and size as a 44° shot does at 1 m (a narrower shot pushed the title off the top).
        float depth = 1f, titleFit = 1f, noteFit = 1f, keyFit = 1f;

        /// <summary>Film every shot (or only those whose run starts with <paramref name="only"/>) into
        /// <paramref name="path"/> (full speed) and the same name + "_slow" (slow motion). Needs Play mode in
        /// QueenOfTheHill_SkillTest.</summary>
        public static string Run(string path, int width = 1280, int height = 720, int fps = 60, string only = null, bool slow = true)
        {
            var bed = FindFirstObjectByType<QueenHillSkillBed>();
            if (bed == null) return "no QueenHillSkillBed (open QueenOfTheHill_SkillTest and press Play)";
            var film = bed.GetComponent<QueenHillSkillFilm>();
            if (film == null) film = bed.gameObject.AddComponent<QueenHillSkillFilm>();
            film.StopAllCoroutines();
            // A film stopped half-way leaves its camera (and the caption strips on it) standing in the world.
            if (rig != null) Destroy(rig);
            rig = null;
            Rolling = false;
            film.StartCoroutine(film.Main(path, width, height, fps, only, slow));
            return Status = "starting";
        }

        static GameObject rig;

        /// <summary>One picture from a camera at <paramref name="eye"/> looking at <paramref name="look"/> into a .jpg
        /// (to try a shot's framing while the probe is frozen; the film's own look: HDR and bloom).</summary>
        public static string Snap(string path, Vector3 eye, Vector3 look, float fov = 44f, int width = 960, int height = 540)
        {
            var go = new GameObject("QotH snap camera");
            var c = go.AddComponent<Camera>();
            var game = FindFirstObjectByType<LabGame>();
            if (game != null && game.labCamera != null) c.CopyFrom(game.labCamera.Cam);
            c.rect = new Rect(0f, 0f, 1f, 1f);
            c.enabled = false;
            c.fieldOfView = fov;
            PawnRushSkillFx.PrepareCamera(c);
            c.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            c.targetTexture = target;
            c.Render();
            c.targetTexture = null;
            RenderTexture.active = target;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllBytes(path, tex.EncodeToJPG(88));
            DestroyImmediate(go);
            DestroyImmediate(target);
            DestroyImmediate(tex);
            return path;
        }

        IEnumerator Main(string path, int w, int h, int fps, string only, bool slowToo)
        {
            game = GetComponent<LabGame>();
            width = w;
            height = h;
            var go = new GameObject("QotH skill film camera");
            rig = go;
            cam = go.AddComponent<Camera>();
            if (game != null && game.labCamera != null) cam.CopyFrom(game.labCamera.Cam);
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.enabled = false;
            PawnRushSkillFx.PrepareCamera(cam);
            rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 64);
            title = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(go.transform, font, " ", new Color(1f, 0.9f, 0.6f), NoteSize);
            banner = Banner(go.transform, 0.345f);
            keyStrip = Banner(go.transform, -0.335f, 0.5f);
            keyStrip.gameObject.SetActive(false);
            key = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, KeySize);

            QueenHillSkillFx.ViewOverride = cam;
            if (game != null) game.SuppressInput = true;
            Time.captureFramerate = fps;
            LabCamera.GameTimeClock = true;
            Index.Clear();
            foreach (bool slow in slowToo ? new[] { false, true } : new[] { false })
            {
                string file = slow ? Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path) + "_slow.mp4") : path;
                stills = Path.Combine(Path.GetDirectoryName(file) ?? ".", Path.GetFileNameWithoutExtension(file) + "_stills");
                Directory.CreateDirectory(stills);
                Frames = 0;
                if (!StillsOnly) Began?.Invoke(file, w, h, fps);
                foreach (var shot in ShotSet == "r89" ? Shots : ShotSet == "r93" ? ShotsR93 : ShotSet == "r94" ? ShotsR94 : ShotSet == "r95" ? ShotsR95 : ShotSet == "r97" ? ShotsR97 : ShotSet == "r98" ? ShotsR98 : ShotSet == "r105" ? ShotsR105 : ShotSet == "r130" ? ShotsR130 : ShotSet == "r133" ? ShotsR133 : ShotSet == "r134" ? ShotsR134 : ShotSet == "r137" ? ShotsR137 : ShotsR141)
                {
                    if (!string.IsNullOrEmpty(only) && !shot.run.StartsWith(only)) continue;
                    Status = $"filming {shot.run}{(slow ? " (slow)" : "")}";
                    eye = shot.eye;
                    look = shot.look;
                    cam.fieldOfView = shot.fov;
                    depth = Mathf.Tan(22f * Mathf.Deg2Rad) / Mathf.Tan(shot.fov * 0.5f * Mathf.Deg2Rad);
                    banner.localPosition = new Vector3(0f, 0.345f, 1.05f * depth);
                    keyStrip.localPosition = new Vector3(0f, -0.335f, 1.05f * depth);
                    SetCaption(shot.title, slow ? $"느리게 ×{(game != null ? game.slowMotionScale : 0.3f):0.0#} · {shot.note}" : shot.note);
                    if (game != null) game.SetSlowMotion(false);
                    QueenHillSkillProbe.Run(shot.run);
                    yield return null;
                    while (!QueenHillSkillProbe.Staged && !Done) yield return null;
                    if (slow && game != null) game.SetSlowMotion(true);
                    Index.Add($"{(slow ? "slow" : "full")} {shot.run} from frame {Frames}");
                    keySerial = QueenHillSkillProbe.HintSerial;
                    Rolling = true;
                    while (!Done) yield return null;
                    Rolling = false;
                    if (game != null) game.SetSlowMotion(false);
                }
                if (!StillsOnly) Ended?.Invoke();
            }
            if (game != null) game.SuppressInput = false;
            Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            QueenHillSkillFx.ViewOverride = null;
            Destroy(go);
            rig = null;
            Destroy(rt);
            Destroy(frame);
            Status = $"done {Frames} frames → {path}";
        }

        static bool Done => QueenHillSkillProbe.Status.StartsWith("done");

        void SetCaption(string a, string b)
        {
            title.Destroy();
            note.Destroy();
            title = new PawnRushSkillFx.Text3D(cam.transform, font, a, Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(cam.transform, font, b, new Color(1f, 0.9f, 0.6f), NoteSize);
            titleFit = Fit(a, TitleSize);
            noteFit = Fit(b, NoteSize);
        }

        const float TitleSize = 0.05f, NoteSize = 0.03f, KeySize = 0.046f;

        /// <summary>The scale that keeps a caption line inside the picture (a long title shrinks, a short one stays).</summary>
        float Fit(string text, float size)
        {
            // Text3D: 64 px of the font = size metres. The picture is 2·tan(22°)·aspect wide where the captions hang.
            font.RequestCharactersInTexture(text, 64, FontStyle.Bold);
            float px = 0f;
            foreach (char c in text)
                if (font.GetCharacterInfo(c, out var info, 64, FontStyle.Bold)) px += info.advance;
            float wide = px * size / 64f, room = 2f * Mathf.Tan(22f * Mathf.Deg2Rad) * width / height * 0.92f;
            return wide > room ? room / wide : 1f;
        }

        static Transform Banner(Transform cam, float y, float width = 2f)
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
            };
            var go = new GameObject("Caption strip");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0f, y, 1.05f);
            go.transform.localScale = new Vector3(width, y > 0f ? 0.15f : 0.085f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.05f, 0.08f, 0.19f, 0.72f) };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            if (Rolling)
            {
                title.Place(cam.transform.TransformPoint(new Vector3(0f, 0.37f, depth)), cam, titleFit, 1f);
                note.Place(cam.transform.TransformPoint(new Vector3(0f, 0.313f, depth)), cam, noteFit, 1f);
                ShowKey();
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                frame.Apply();
                RenderTexture.active = null;
                if (!StillsOnly) Frame?.Invoke(frame);
                if (StillEvery > 0 && Frames % StillEvery == 0) File.WriteAllBytes(Path.Combine(stills, $"f_{Frames:D4}.jpg"), frame.EncodeToJPG(85));
                Frames++;
            }
            // Back to the shot's own pose: the effects shake it from there again next frame.
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
        }

        void ShowKey()
        {
            if (QueenHillSkillProbe.HintSerial != keySerial)
            {
                keySerial = QueenHillSkillProbe.HintSerial;
                key.Destroy();
                string hint = $"[ {QueenHillSkillProbe.Hint} ]";
                key = new PawnRushSkillFx.Text3D(cam.transform, font, hint, new Color(1f, 0.85f, 0.3f), KeySize);
                keyFit = Fit(hint, KeySize);
                keyLeft = 1.1f;
            }
            keyLeft -= Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            bool on = keyLeft > 0f;
            keyStrip.gameObject.SetActive(on);
            key.Place(cam.transform.TransformPoint(new Vector3(0f, -0.335f, depth)), cam, on ? keyFit : 0.001f, Mathf.Clamp01(keyLeft / 0.3f));
        }

        void OnDestroy()
        {
            Rolling = false;
            if (rig != null) Destroy(rig);
            rig = null;
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            if (QueenHillSkillFx.ViewOverride == cam) QueenHillSkillFx.ViewOverride = null;
        }
    }
}
