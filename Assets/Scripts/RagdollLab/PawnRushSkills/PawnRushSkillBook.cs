using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>What a skill is for, the way the user sorted skills (2026-11-14): its own movement, hindering an enemy's,
    /// helping a teammate's, answering an enemy's skill, a thing put down, a state put on a body.</summary>
    [Flags]
    public enum SkillTag { None = 0, Move = 1, Block = 2, Ally = 4, Counter = 8, Place = 16, Buff = 32 }

    /// <summary>Where a skill comes from: Pawn Rush as it is or was, the two other modes 승규 님 made skills for (their code
    /// is theirs: a sword in the hand, a tower to climb), the 2.0 plans (Docs/Skills/PAWN_RUSH_2_SKILL_PLANS.md), the
    /// skill idea book (Docs/Skills/PAWN_RUSH_2_SKILL_IDEAS.md), or the kits by piece
    /// (Docs/Skills/PAWN_RUSH_SKILL_KITS.md: only the skills made for them; a kit also uses skills from the others).</summary>
    public enum SkillOrigin { PawnRush, QueenHill, SwordFight, Plan2, IdeaBook, Kits, Pool }

    /// <summary>One line of the skill book: a skill's number (PawnRushSkillIds), name, the piece it was thought up for, where
    /// it comes from, its tags, whether it can be put in a slot yet, and how to use it.</summary>
    public sealed class SkillEntry
    {
        public int id;
        public string name;
        public PieceKind home;
        public SkillOrigin origin;
        public SkillTag tags;
        public bool built;
        public string how;
        /// <summary>One line for the promotion card (unset: the first line of <see cref="how"/>).</summary>
        public string card;

        /// <summary>One of the skills as they are: its effects and sounds are its home piece's.</summary>
        public bool Old => PawnRushSkillIds.Old(id);
    }

    /// <summary>
    /// The skill book (JY-skill-lab, 2026-11-19): every Pawn Rush skill the developers' skill tree (PawnRushSkillWindow, K)
    /// lists - the five as they are, plan 1-나's five, the candidates of Docs/Skills/PAWN_RUSH_2_SKILL_PLANS.md that are
    /// not made yet, and (the user: "승규님이 옛날에 스킬 만들 때 ... 되게 많이 만들었었는데 그것들 다 여기다 넣어줘", "문서 말고
    /// 실제로 만들었던 것들") every skill 승규 님 made as code in any mode: Pawn Rush's earlier ones, Queen of the Hill's six
    /// (brought over to the slots: RagdollPawn.HillSkills.cs), Sword Fight's F and E skills (on main, for pieces with a
    /// sword). What cannot be put in a slot yet is shown dim, with where its code is. A new skill is one more line here,
    /// its number in PawnRushSkillIds and its code in RagdollPawn.SkillPlans.cs (RunNewSkill).
    /// </summary>
    public static class PawnRushSkillBook
    {
        public static readonly SkillTag[] Tags = { SkillTag.Move, SkillTag.Block, SkillTag.Ally, SkillTag.Counter, SkillTag.Place, SkillTag.Buff };

        public static string TagName(SkillTag tag) => tag switch
        {
            SkillTag.Move => "내 이동",
            SkillTag.Block => "방해",
            SkillTag.Ally => "팀원 돕기",
            SkillTag.Counter => "맞받아치기",
            SkillTag.Place => "설치 · 소환",
            SkillTag.Buff => "버프 · 디버프",
            _ => "전체",
        };

        public static Color TagColor(SkillTag tag) => tag switch
        {
            SkillTag.Move => new Color(0.22f, 0.54f, 0.87f),
            SkillTag.Block => new Color(0.85f, 0.35f, 0.19f),
            SkillTag.Ally => new Color(0.11f, 0.62f, 0.46f),
            SkillTag.Counter => new Color(0.50f, 0.47f, 0.87f),
            SkillTag.Place => new Color(0.73f, 0.46f, 0.09f),
            SkillTag.Buff => new Color(0.83f, 0.33f, 0.49f),
            _ => new Color(0.53f, 0.53f, 0.50f),
        };

        // (In the order the skill tree lists them.)
        public static readonly SkillOrigin[] Origins = { SkillOrigin.Pool, SkillOrigin.PawnRush, SkillOrigin.Kits, SkillOrigin.IdeaBook, SkillOrigin.QueenHill, SkillOrigin.SwordFight, SkillOrigin.Plan2 };

        public static string OriginName(SkillOrigin origin) => origin switch
        {
            SkillOrigin.Pool => "후보 문서",
            SkillOrigin.PawnRush => "폰 러쉬",
            SkillOrigin.Kits => "기물별 한 벌",
            SkillOrigin.QueenHill => "퀸 오브 더 힐",
            SkillOrigin.SwordFight => "소드 파이트",
            SkillOrigin.IdeaBook => "스킬 기획서",
            _ => "2.0 기획안",
        };

        static SkillEntry Made(int id, string name, PieceKind home, SkillOrigin origin, SkillTag tags, string how) =>
            new SkillEntry { id = id, name = name, home = home, origin = origin, tags = tags, built = true, how = how };

        static SkillEntry Idea(int id, string name, PieceKind home, SkillOrigin origin, SkillTag tags, string how) =>
            new SkillEntry { id = id, name = name, home = home, origin = origin, tags = tags, built = false, how = how };

        // Where the code of a skill that cannot be put in a slot yet is.
        const string OnMainKing = "\n코드: main에 있음 (킹 기물 전용이라 칸에는 아직 못 넣음)";
        const string OnMainSwitch = "\n코드: main에 있음 (bishopRope 스위치를 끄면 나옴, 칸에는 아직 못 넣음)";
        const string InHistory = "\n코드: git 기록에만 있음 (R153에 지금 스킬로 바뀜)";
        const string OnMainSword = "\n코드: main에 있음 (칼을 든 소드 파이트 기물용이라 폰 러쉬로 옮겨야 함)";

        public static readonly SkillEntry[] All =
        {
            // The skill pool's plan 1, one skill a piece (Docs/Skills/PAWN_RUSH_SKILL_POOL.md; RagdollPawn.PoolSkills.cs). The
            // knight's and the queen's are the bent leap and the check below, changed to the pool's.
            Made(PawnRushSkillIds.SteeredSteps, "첫 두 걸음 (꺾기 · 앙파상)", PieceKind.Pawn, SkillOrigin.Pool, SkillTag.Move | SkillTag.Block | SkillTag.Ally,
                "키 = 첫 걸음 (1.5 m) · 0.5초 안에 키 한 번 더 = 둘째 걸음, 이동 키 쪽으로 45°까지 꺾임\n곧게 부딪히면 밀고 비스듬히 부딪히면 1.2초 넘어뜨림 · 넘어진 팀원에 닿으면 일으킴 · 쿨 5초 (한 걸음만 3초)\n적이 돌진 스킬로 내 옆 2 m를 지나치면 발밑에 주황 고리 0.4초 → 그때 키 = 앙파상 (발 걸기, 1.2초) (후보 문서 P-기1 + 2판)"),
            Made(PawnRushSkillIds.LongSnipe, "긴 대각선 저격", PieceKind.Bishop, SkillOrigin.Pool, SkillTag.Block,
                "키 = 무릎 꿇고 겨눔 (못 걸음) · 좌클릭 누르고 있기 = 모으기 (1초 동안 10 m → 30 m) · 떼기 = 쏘기 · 우클릭/키 = 일어남\n처음 닿은 적이 맞음: 12 m 안 휘청, 22 m까지 넘어짐, 그 너머는 넘어지며 날아감 · 쏘면 2 m 뒤로 미끄러져 엉덩방아\n모은 지 0.6초부터 표적 발밑에 빛점 · 팀원과 벽에서 막힘 · 쿨 18초 (후보 문서 B-중7)"),
            Made(PawnRushSkillIds.ChainBehind, "뒤에서 잡기", PieceKind.Rook, SkillOrigin.Pool, SkillTag.Block | SkillTag.Move,
                "키 = 조준 · 좌클릭 = 사슬 던지기 (10.5 m, 처음 닿은 것에 걸림)\n나보다 가벼운 적(폰 · 나이트 · 비숍)은 내 앞 0.9 m로 끌려와 휘청, 2초 동안 무거움 (점프가 낮음) · 무거운 적(퀸 · 킹)과 벽 · 기둥 · 턱에는 내가 날아감\n걸린 뒤 휠 감기 또는 좌클릭 누르고 있기 = 빨리 감기 · 끌려가는 적은 좌클릭 3번이면 절반에서 털어냄 · 팀원에게는 손잡이 · 쿨 16초 (후보 문서 R-중2 + 2판)"),
            // The pool's plan 3, its basic row's new ones (RagdollPawn.PoolBasics.cs).
            Made(PawnRushSkillIds.FlankShot, "옆구리 사격", PieceKind.Bishop, SkillOrigin.Pool, SkillTag.Block,
                "키 = 달리면서 0.15초 뒤 카메라 쪽으로 12 m 빛줄기 (멈추지 않음)\n처음 닿은 적은 줄 방향으로 2 m 밀리고 1.5초 발이 무거움 (0.7배), 비숍은 0.4 m 뒤로 밀림 · 등 뒤에서 쏘면 앞으로 밀어 줌 (블런더) · 돌진 중인 적은 돌진이 끊김\n팀원 · 기둥에서 막힘 · 쿨 6초 (후보 문서 B-기6 + 2판)"),
            Made(PawnRushSkillIds.CastleOver, "넘어오는 캐슬링", PieceKind.Rook, SkillOrigin.Pool, SkillTag.Move | SkillTag.Ally | SkillTag.Block,
                "키 = 앞쪽 6 m 안의 가장 가까운 팀원을 0.15초 웅크렸다 낮게 넘어 1.5 m 앞에 쿵\n내려선 둘레 2 m의 적이 밀림 (내려서는 순간 키 = 3 m까지) · 넘어간 팀원은 1.5초 동안 넘어지지 않음\n사이에 적이 있으면 그 등에 박고 멈춤 · 체크 중에는 못 씀 · 쿨 10초 (후보 문서 R-기2)"),
            Made(PawnRushSkillIds.ScholarThrust, "스콜라 찌르기", PieceKind.Queen, SkillOrigin.Pool, SkillTag.Block | SkillTag.Move,
                "옆 이동 키 + 키 = 비스듬히 옆으로 2.5 m 미끄러짐 · 0.6초 안에 키(또는 좌클릭) 한 번 더 = 카메라 쪽으로 5 m 찌르기\n등 · 옆을 찔린 적은 3 m 밀리고 휘청 0.5초 (서 있었고 4 m 안에 그 팀원이 없으면 넘어짐)\n마주 보던 적은 밀리기만 하고 퀸이 튕겨 휘청 0.6초 · 쿨 9초 (옆걸음만 4초, 후보 문서 Q-기2)"),
            // ...and its mid row's new ones (RagdollPawn.PoolMids.cs; the knight's and the bishop's are the fork and the pin).
            Made(PawnRushSkillIds.ChaseAway, "쫓아내기", PieceKind.Pawn, SkillOrigin.Pool, SkillTag.Block | SkillTag.Move,
                "키 = 창 겨눔 (달리면서) · 좌클릭 누르고 있기 = 모으기 (0.2 → 0.8초: 3 → 5 m) · 떼기 = 찌르며 돌진 · 우클릭/키 = 취소\n처음 찔린 적은 자기 값만큼 날아감: 폰 1 m, 나이트 · 비숍 3 m, 킹 4 m, 룩 5 m, 퀸 6 m (룩 · 퀸은 2초 넘어지고 머리 위에 '+5' · '+9')\n폰은 반동으로 0.5 m 튕김 · 헛치면 앞으로 엎어짐 · 쿨 15초 (후보 문서 P-중4 + 2판)"),
            Made(PawnRushSkillIds.CheckSeries, "연속 체크", PieceKind.Queen, SkillOrigin.Pool, SkillTag.Block,
                "키 = 조준 (10 m) · 좌클릭 = 체크 · 적 발밑에 퀸 쪽으로 길쭉한 빨간 칸 (2 × 3.5 m, 하얀 먼 끝이 나갈 곳), 0.9초 뒤 터짐\n비켜 나가면 1초 안에 키 = 퀸이 빈 칸으로 미끄러져 다음 칸 (나갈 곳은 카메라 쪽, 0.7 → 0.5초, 세 번까지)\n맞으면 2초 넘어지며 날아감 · 셋째가 맞으면 '최선수' · 쿨 24초 (퀸 1.5배, 후보 문서 Q-중5 + 2판)"),
            // The pool's plan 3, its ultimates on a gauge (RagdollPawn.PoolUltimates.cs).
            Made(PawnRushSkillIds.Promote, "승격", PieceKind.Pawn, SkillOrigin.Pool, SkillTag.Buff | SkillTag.Move,
                "궁극기 (게이지 3분) · 키 = 앞 3 m 끝줄로 마지막 한 칸을 내딛음\n줄에 닿는 순간 멈칫, 둘레 3 m의 적이 밀리고 15초 동안 퀸이 됨 (퀸 줄의 스킬) · 끝나면 원래 기물로 (후보 문서 P-궁1)"),
            Made(PawnRushSkillIds.Smother, "스머더드 메이트", PieceKind.Knight, SkillOrigin.Pool, SkillTag.Block | SkillTag.Move,
                "궁극기 (게이지 3분) · 18 m 안의 적 하나를 그 팀원 둘 이상이 둘러쌌을 때만 (그 무리에 금색 고리)\n키 = 높이 솟아 그 적 위로 떨어짐 (이동 키로 3 m 쫓기) · 가운데 적 납작 3초, 둘레는 날아감 · 닿기 직전 좌클릭 = 납작 +1초 · 헛치면 머리부터 박힘 (후보 문서 N-궁1)"),
            Made(PawnRushSkillIds.LongDiagonal, "긴 대각선", PieceKind.Bishop, SkillOrigin.Pool, SkillTag.Block | SkillTag.Place,
                "궁극기 (게이지 3분) · 키 = 조준 · 좌클릭 = 구간 끝까지 (60 m, 사람은 뚫고 벽에서 멈춤) 빛의 줄\n1.5초 뒤 8초 동안 줄 위에 있거나 줄을 밟는 적은 2.5초 발이 박힘 (후보 문서 B-궁1, 검토 칸의 체스 안)"),
            Made(PawnRushSkillIds.Chariot, "활발한 룩 (전차)", PieceKind.Rook, SkillOrigin.Pool, SkillTag.Move | SkillTag.Block,
                "궁극기 (게이지 3분) · 키 = 10초 동안 전차: 6 m/s로 곧게만 · A · D = 직각 꺾기 · S = 한 번 1초 멈춤 · 키 한 번 더 = 끝\n부딪힌 적은 옆으로 튕겨 넘어짐, 룩은 안 넘어짐 · 벽에 박으면 1초 휘청하고 끝 (후보 문서 R-궁6)"),
            Made(PawnRushSkillIds.QueenSac, "퀸 희생", PieceKind.Queen, SkillOrigin.Pool, SkillTag.Block,
                "궁극기 (게이지 3분) · 키 = 떨어질 곳 겨눔 (12 m) · 좌클릭 = 1.2초 몸을 던짐 (이동 키로 3 m 옮김)\n떨어지며 둘레 9 m의 적이 모두 넘어지며 날아감 (넷 이상 '탁월한 수 !!', 없으면 '블런더 ??') · 퀸은 잡혀서 돌아감 (후보 문서 Q-궁1)"),
            Made(PawnRushSkillIds.KingMarch, "킹 행진", PieceKind.King, SkillOrigin.Pool, SkillTag.Ally | SkillTag.Buff,
                "궁극기 (게이지 3분) · 키 = 10초 동안 행진: 곁(4 m)의 팀원 수에 따라 1.3 · 1.5 · 1.7배로 걸음\n킹과 곁의 팀원은 넘어지지 않음 · 2 m 안으로 적이 못 들어옴 · 키 다시 = 행진 걸음 (앞 2.5 m 적을 밀어냄) (후보 문서 K-궁1)"),

            // The pool's plan 2, the three the others did not make (RagdollPawn.PoolPlan2.cs).
            Made(PawnRushSkillIds.BackRank, "뒷줄 한 수", PieceKind.Rook, SkillOrigin.Pool, SkillTag.Move | SkillTag.Block,
                "옆 · 뒤 이동 키 + 키 = 그쪽으로 3.5 m 곧게 미끄러짐 (앞으로는 안 됨, 가장자리에서 안 멈춤)\n처음 닿은 것에서 딱 멈춤: 적이면 3 m 밀어내고 룩은 그 자리에 섬 · 쿨 6초 (후보 문서 R-기5)"),
            Made(PawnRushSkillIds.FileCharge, "직선 돌파", PieceKind.Rook, SkillOrigin.Pool, SkillTag.Move | SkillTag.Block,
                "키 = 자세 · 좌클릭 누르고 있기 = 바닥 줄 4 → 9 m · 떼기 = 곧게 돌진 · 우클릭/키 = 취소\n길 위의 적 셋까지 양옆으로 튕겨 넘어짐 (칠 때마다 멈칫, 1 m 더) · 벽에 박으면 룩 휘청 · 끝나면 바로 다이빙 가능 · 쿨 16초 (후보 문서 R-중1)"),
            Made(PawnRushSkillIds.KingRun, "킹 런", PieceKind.King, SkillOrigin.Pool, SkillTag.Move | SkillTag.Counter,
                "키 = 한 걸음, 박자 맞춰 키 = 다음 걸음 (2.5초 동안 다섯 걸음, 걸음마다 이동 키 쪽)\n박자에 맞으면 2.2 m, 어긋나면 짧게 · 딛을 때마다 잡은 손 · 묶임 · 핀 · 발 무거움이 떨어짐 · 체크 중의 걸음은 하나 더 · 쿨 18초 (후보 문서 K-중7)"),
            Made(PawnRushSkillIds.KingSummon, "왕의 부름 (앞으로)", PieceKind.King, SkillOrigin.Pool, SkillTag.Ally | SkillTag.Move | SkillTag.Block,
                "키 = 같은 구간 팀원 목록 · 2명까지 골라 키로 부름 (나팔 반동으로 킹이 반 걸음 물러남) · 팀원은 3초 안에 자기 키로 받음\n불린 팀원은 킹 앞 좌우(부를 때 보던 쪽)에 내려서고, 내려서는 순간 둘레 2 m의 적이 밀려남\n그때 0.6초 안에 키 한 번 더 = 킹과 팀원이 함께 3 m 밀고 나감 · 킹만 씀 · 쿨 20초 (후보 문서 K-중1)"),

            // The skills as they are.
            Made(PawnRushSkillIds.TwoSteps, "첫 두 걸음", PieceKind.Pawn, SkillOrigin.PawnRush, SkillTag.Block | SkillTag.Ally,
                "키 = 1.5 m 걸음 · 0.1~0.8초 안에 한 번 더 = 앞 3 m 안의 서 있는 적이나 넘어진 팀원에게 휘어 들어감\n곧게 부딪히면 밀기, 비스듬히 부딪히면 넘어뜨림 · 넘어진 팀원은 일으킴"),
            Made(PawnRushSkillIds.TurnLeap, "꺾어 도약", PieceKind.Knight, SkillOrigin.PawnRush, SkillTag.Block,
                "앞쪽 1.5~9 m의 서 있는 적 머리 위에 빨간 고리가 뜨면 키 = 그 적 머리로 도약 (대상이 없으면 무시)\n찍힌 적은 선 채로 2초 해롱해롱 · 착지 1.5 m 안의 다른 적은 휘청"),
            Made(PawnRushSkillIds.CrossRope, "교차 밧줄", PieceKind.Bishop, SkillOrigin.PawnRush, SkillTag.Block | SkillTag.Buff,
                "키 = 조준 (7 m) · 좌클릭 = X자 밧줄을 적의 허리에 걸기 · 우클릭/키 = 취소\n6초 동안 걸어 다니며 끌고 다님 · 휠↑ 느슨하게 · 휠↓ 팽팽하게 · 키/우클릭 = 놓기 (팽팽하면 튕겨 옴)"),
            Made(PawnRushSkillIds.ChainPull, "직선 사슬 견인", PieceKind.Rook, SkillOrigin.PawnRush, SkillTag.Block | SkillTag.Ally,
                "키 = 조준 (10.5 m) · 좌클릭 = 사슬 던지기 · 우클릭/키 = 취소\n적은 앞 1.5 m로 끌려와 휘청 · 팀원은 2초 안에 우클릭으로 손잡이를 잡으면 옆으로 옴"),
            Made(PawnRushSkillIds.RingShove, "팔방 밀치기", PieceKind.Queen, SkillOrigin.PawnRush, SkillTag.Block,
                "키 = 0.35초 예고 뒤 둘레 3 m 충격파 · 안쪽 1.8 m는 넘어지며 날아가고 바깥은 밀림"),
            // Skill plan 1-나.
            Made(PawnRushSkillIds.EnPassant, "앙파상", PieceKind.Pawn, SkillOrigin.Plan2, SkillTag.Block | SkillTag.Counter,
                "적이 내 옆 2 m 안을 나보다 빠르게 스쳐 지나가면 그 적 발밑에 주황 고리가 1초 뜸 → 그동안 키 = 뛰어들어 발 걸기\n걸린 적은 달리던 쪽으로 넘어져 미끄러짐 · 스쳐 지나간 적이 없으면 무시\n혼자 시험: 왼쪽 창 \"앙파상 시험\" (M)"),
            Made(PawnRushSkillIds.Fork, "포크", PieceKind.Knight, SkillOrigin.Plan2, SkillTag.Block | SkillTag.Move,
                "키 = 내릴 점 겨눔 (9 m, 앞 좌우 두 자리와 차일 적 머리에 고리) · 좌클릭 = 도약 · 우클릭/키 = 취소\n내린 점 앞 좌우 두 자리 (ㄱ자로 2.5 m)를 양발로 참: 두 자리에 다 적이면 둘 다 2초 넘어지며 날아감 (킹과 퀸이면 '로열 포크')\n한 명만이면 밀기만 하고 나이트가 기우뚱 · 헛치면 주저앉음 · 내리꽂는 순간 좌클릭 = 1.5배 멀리 · 쿨 16초 (후보 문서 N-중1 + 2판)"),
            Made(PawnRushSkillIds.Pin, "핀", PieceKind.Bishop, SkillOrigin.Plan2, SkillTag.Block | SkillTag.Buff,
                "키 = 조준 · 좌클릭 누르고 있기 = 모으기 (10 → 18 m) · 떼기 = 쏘기 (비숍 1 m 반동) · 우클릭/키 = 취소\n맞은 적 뒤 4 m 안에 그 팀원이 있으면 3초 안에 움직일 때 뒤의 팀원이 넘어짐 · 뒤가 벽 · 기둥이면 거기에 3초 꿰임 · 비면 2초 발 무거움\n꿴 뒤 3초 안에 키 한 번 더 = 줄을 타고 6 m까지 다가감 · 쿨 16초 (후보 문서 B-중1)"),
            Made(PawnRushSkillIds.Castling, "캐슬링", PieceKind.Rook, SkillOrigin.Plan2, SkillTag.Ally | SkillTag.Move,
                "키 = 조준 (팀원을 보면 청록 선, 12 m) · 좌클릭 = 제안 · 우클릭/키 = 취소\n팀원이 2초 안에 자기 스킬 키(F)로 받으면 둘이 날아 자리를 바꿈 (더미 팀원은 0.6초 뒤 스스로 받음)"),
            Made(PawnRushSkillIds.Check, "체크!", PieceKind.Queen, SkillOrigin.Plan2, SkillTag.Block,
                "키 = 조준 (적을 보면 빨간 고리, 12 m) · 좌클릭 = 체크! (퀸이 그쪽으로 1.5 m 미끄러짐) · 우클릭/키 = 취소\n그 적이 서 있던 자리가 빨간 칸 → 1.5초 안에 못 나가면 넘어지며 날아감 (퀸도 한 발 튕김)\n막기: 사이에 선 적 팀 기물이 대신 맞음 · 잡기: 그 전에 내가 맞으면 취소 · 비켜 나가면 2초 안에 쿨 없이 한 번 더 (후보 문서 Q-기1)"),

            // The skill idea book's bundle A (Docs/Skills/PAWN_RUSH_2_SKILL_IDEAS.md 13장; RagdollPawn.BookSkills.cs).
            Made(PawnRushSkillIds.TouchMove, "터치 무브 · 자두브", PieceKind.Pawn, SkillOrigin.IdeaBook, SkillTag.Buff | SkillTag.Ally,
                "키 = 0.25초 뒤 손바닥을 내밂 (1.5 m) · 맞은 적은 2초 동안 멈출 수 없음 (방향은 틀 수 있고, 벽을 잡으면 멈춤)\n넘어졌거나 휘청이거나 묶인 팀원을 건드리면 바로 서고 기상 보호 1초 · 쿨 6초 (헛치면 2.5초)"),
            Made(PawnRushSkillIds.BackKick, "뒷발차기", PieceKind.Knight, SkillOrigin.IdeaBook, SkillTag.Block | SkillTag.Buff,
                "키 = 보는 쪽으로 등을 돌리고 0.35초 뒤 뒷발 (2 m) · 맞은 적은 잡고 있던 것을 놓치고 한 걸음 밀림 (넘어지지 않음)\n3초 동안 손이 저려 아무것도 못 잡음 (매달리기 · 등반 · 밧줄) · 쿨 6초"),
            Made(PawnRushSkillIds.Ghost, "반대색 비숍", PieceKind.Bishop, SkillOrigin.IdeaBook, SkillTag.Counter,
                "키 = 3초 동안 적의 몸과 스킬이 나를 통과함 (공중에서도 됨) · 나도 그동안 적을 밀거나 잡지 못하고 스킬도 못 씀\n왕관 발판에 서 있어도 사람 수에 안 들어감 · 적과 겹친 채 풀리면 서로 튕김 · 팀의 스킬도 안 닿음 · 쿨 12초"),
            Made(PawnRushSkillIds.BackPush, "뒤에서 밀기", PieceKind.Rook, SkillOrigin.IdeaBook, SkillTag.Ally | SkillTag.Block,
                "바로 앞 1.8 m 안의 기물을 보고 키 = 0.5초 받친 뒤 곧게 6 m 쏨 (낮게 날아 서서 착지, 길에 있던 적은 옆으로 튕김)\n사이에 끼어든 적이 있으면 그 적이 대신 날아감 · 받기 싫으면 점프 · 틈과 벽 앞에서 멈춤 · 쿨 9초 (헛치면 3초)"),
            Made(PawnRushSkillIds.TwoHares, "두 마리 토끼", PieceKind.Queen, SkillOrigin.IdeaBook, SkillTag.Block | SkillTag.Buff,
                "앞쪽 8 m 안의 적 둘을 보고 키 = 두 선이 0.45초 보인 뒤 둘의 허리를 3 m 고무줄로 4초 묶음\n줄이 1 m 넘게 늘어나면 튕겨서 서로에게 날아가 휘청 · 적이 둘이 아니면 무시 · 쿨 9초 (놓치면 3초)"),

            // The kits by piece, the basic row - what each piece does in a chess opening (Docs/Skills/PAWN_RUSH_SKILL_KITS.md §6;
            // RagdollPawn.KitSkills.cs). The other three basics are above: the pawn's two steps, the rook's castling, the
            // queen's check.
            Made(PawnRushSkillIds.BentLeap, "ㄱ자 도약", PieceKind.Knight, SkillOrigin.Kits, SkillTag.Move | SkillTag.Block,
                "키 = 0.15초 준비 뒤 높이 1.6 m로 4.5 m 도약 (공중에서도 됨) · 공중에서 한 번 더 = 이동 키 쪽으로 90°까지 꺾어 다시 차고 나감\n적의 머리를 밟으면 그 적은 휘청 0.4초에 3초 동안 가벼움 (밀림 · 날림 두 배), 나는 다시 튀어 오름 · 착지 순간 점프 = 2 m 구르기 (후딜 없음) · 내려설 곳이 없으면 '블런더' · 쿨 6초\n(승규 님의 10-07 첫 모양을 칸에서 돌게 다시 만든 것, 후보 문서 N-기1의 고침 + 2판의 가벼운 발굽. 머리로 저절로 날아가지는 않음)"),
            Made(PawnRushSkillIds.Fianchetto, "피앙케토", PieceKind.Bishop, SkillOrigin.Kits, SkillTag.Block | SkillTag.Buff,
                "키 = 자리를 잡고 겨눔 (못 걸음 · 줄이 1초 동안 8 m에서 25 m로 자람) · 좌클릭 = 쏘기 · 우클릭/키 = 일어남\n줄에 처음 닿은 적은 2초 동안 발이 무거움 (0.6배, 전력질주 안 됨) · 팀원이나 벽에 먼저 닿으면 거기서 막힘 · 쿨 6초"),
            Made(PawnRushSkillIds.KingStep, "왕의 한 걸음", PieceKind.King, SkillOrigin.Kits, SkillTag.Move | SkillTag.Counter,
                "키 = 이동 키 쪽으로 한 칸 (1.5 m) 비킴, 디딜 때 쿵 · 공중에서도 됨 (쿨 +1초) · 걸음에 닿은 적은 3 m 밀림, 킹은 그 자리에 섬 (한 칸 잡기)\n곁 1.5 m에 그 팀원이 선 적은 받쳐진 말이라 안 밀림 · 체크 칸 · 저격 빛점에서 걸어 나오면 '체크 탈출'\n잡혀 있으면 손을 뿌리치고 비킴 · 넘어져 있으면 이동 키 쪽으로 굴러 바로 일어남 (둘 다 쿨 +1.5초) · 걸음 끝 0.3초 안의 점프 · 다이빙은 더 멀리 · 쿨 4초 (후보 문서 K-기1 + 2판)"),

            // The kits' second version, its new ones (Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6; RagdollPawn.KitTwo.cs).
            Made(PawnRushSkillIds.QueenGlide, "여왕의 활주", PieceKind.Queen, SkillOrigin.Kits, SkillTag.Move,
                "이동 키 + 키 = 여덟 방향 가운데 그쪽으로 1초에 7 m 빠르기로 곧게 미끄러짐 (이동 키가 없으면 카메라 쪽)\n키 한 번 더 = 그 자리에 딱 섬 (가장 멀리 7 m) · 사람이나 벽에 닿으면 거기서 멈추고, 닿은 적은 1.5 m 밀림 · 쿨 9초 (2판 Q-초)"),
            Made(PawnRushSkillIds.EightLines, "여덟 줄", PieceKind.Queen, SkillOrigin.Kits, SkillTag.Block,
                "키 (퀸의 셋째 칸 R) = 홀을 찍어 여덟 방향으로 7 m 빛줄 · 0.5초 동안 이동 키(좌우)로 줄 전체를 22.5°까지 돌림\n그 뒤 한꺼번에 터짐: 줄마다 처음 닿은 적이 줄을 따라 날아가 2.5초 넘어짐 · 줄 사이에 선 적은 멀쩡 · 셋 넘게 맞히면 0.15초 멈칫 · 쿨 60초 (2판 Q-상)"),
            Made(PawnRushSkillIds.WallCharge, "성벽 돌진", PieceKind.Rook, SkillOrigin.Kits, SkillTag.Place | SkillTag.Block,
                "키 = 겨눔 (바닥 줄 9 m) · 좌클릭 = 0.4초 바닥 줄이 깜박인 뒤 곧게 돌진 · 우클릭/키 = 취소\n지나간 자리에 가슴 높이 (1.2 m) 성벽이 솟아 5초 섬 · 돌진 중 좌클릭 = 거기서 끊기 · 부딪힌 적은 옆으로 2 m 밀림\n상대 룩의 돌진이 성벽을 부숨 · 쿨 40초 (2판 R-상)"),
            Made(PawnRushSkillIds.SnipeHigh, "긴 대각선 저격 (상급)", PieceKind.Bishop, SkillOrigin.Kits, SkillTag.Block,
                "후보 문서의 긴 대각선 저격과 같은 한 방, 상급 수치: 쿨 40초\n키 = 무릎 꿇고 겨눔 · 좌클릭 누르고 있기 = 모으기 (1초 동안 10 m → 30 m) · 떼기 = 쏘기 · 우클릭/키 = 일어남\n12 m 안 휘청 0.4초, 22 m까지 넘어짐 2초, 그 너머는 넘어지며 날아감 2.5초 ('최선수') · 쏘면 2 m 미끄러져 엉덩방아 (2판 B-상)"),

            // 승규 님's Pawn Rush skills that are not the five above: the king's, and the earlier forms of three pieces'.
            Idea(41, "왕의 부름", PieceKind.King, SkillOrigin.PawnRush, SkillTag.Ally | SkillTag.Move,
                "같은 구간 팀원을 2명까지 골라 부름. 불린 팀원이 3초 안에 자기 스킬 키로 받으면 0.6초 만에 킹 뒤로 옴 (오는 동안 무적)" + OnMainKing),
            Idea(42, "교차 밧줄 (설치형 X자 덫)", PieceKind.Bishop, SkillOrigin.PawnRush, SkillTag.Place | SkillTag.Block,
                "4.5 m 안에 X자 밧줄을 6초 깔아 둠. 지나가는 적이 걸려 묶이고, 룩은 끊고 지나감 · 쿨 8초 (10-07의 첫 모양)" + OnMainSwitch),
            Idea(29, "직선 돌파", PieceKind.Rook, SkillOrigin.PawnRush, SkillTag.Move | SkillTag.Block,
                "조준 뒤 4칸 돌진, 최대 3명을 양옆으로 튕김, 바리케이드를 부숨, 공중 돌진도 됨 · 쿨 7초 (10-07의 첫 모양)" + InHistory),
            // (The knight's earlier leap, 43, is in the kits' block above: made again for the slots as "ㄱ자 도약".)

            // 승규 님's Queen of the Hill skills (R89, 10-09), brought over to the slots (RagdollPawn.HillSkills.cs).
            Made(PawnRushSkillIds.Ward, "근접 호위", PieceKind.King, SkillOrigin.QueenHill, SkillTag.Ally | SkillTag.Buff,
                "키 = 0.45초 준비 뒤, 둘레 2.5 m 안의 팀원이 2.9초 동안 호위를 받음: 넘어질 일은 휘청으로, 밀림은 4분의 1만\n쓴 사람 자신은 호위를 못 받지만, 호위 받는 팀원이 2.5 m 안에 서 있으면 그 팀원이 대신 맞아 줌 · 쿨 9초"),
            Made(PawnRushSkillIds.Slash, "팔방 검격", PieceKind.Queen, SkillOrigin.QueenHill, SkillTag.Block,
                "키 = 조준 (빨간 띠, 6 m) · 좌클릭 = 0.45초 뒤 검기가 띠를 따라 나감 · 우클릭/키 = 취소\n띠 위의 적은 넘어지며 밀려남. 턱(1 m까지)은 타고 오르고 낭떠러지는 따라 내려감 · 벽과 호위 받는 적에서 막힘 · 쿨 6초"),
            Made(PawnRushSkillIds.CastleSwap, "캐슬링 교대", PieceKind.Rook, SkillOrigin.QueenHill, SkillTag.Ally | SkillTag.Move,
                "키 = 조준 (팀원을 보면 청록 선, 9 m) · 좌클릭 = 요청 · 우클릭/키 = 취소\n팀원이 3초 안에 자기 스킬 키(F)로 받으면 서로 자리를 바꿈 · 안 받으면 쿨타임 없이 취소 · 쿨 12초\n(1-나 캐슬링과 같은 방식이고 수치만 다름: 더 가깝고, 더 오래 기다리고, 낮고 빠르게 낢)"),
            Made(PawnRushSkillIds.AirVolley, "교차 공중 포격", PieceKind.Bishop, SkillOrigin.QueenHill, SkillTag.Block | SkillTag.Move,
                "키 = 1.3 m 떠오름 (이동 키로 천천히 미끄러짐) · 좌클릭 = 조준점에 견제탄, 2발까지 · 우클릭/키 = 내려옴\n떨어진 곳의 X자와 가운데에 있던 적은 넘어지며 밀려남 · 3초 안에 한 발도 안 쏘면 취소 · 쿨 9초"),
            Made(PawnRushSkillIds.PressLeap, "도약 압착", PieceKind.Knight, SkillOrigin.QueenHill, SkillTag.Move | SkillTag.Block | SkillTag.Buff,
                "키 = 착지점 조준 (주황 원, 6 m, 한 층 높이까지) · 좌클릭 = 도약 · 우클릭/키 = 취소\n착지 원 1.3 m 안의 적은 0.7초 납작해지며 바깥으로 미끄러짐 (가장자리면 떨어짐)\n조준 근처에 적이 있으면 그 머리를 밟음 · 쿨 7초"),
            Made(PawnRushSkillIds.Squeeze, "비집고 돌파", PieceKind.Pawn, SkillOrigin.QueenHill, SkillTag.Move,
                "키 = 몸을 낮추고 가는 방향으로 1.8 m 돌파 · 부딪힌 기물은 옆으로 비킴 (넘어뜨리지 않음) · 벽에서 멈춤 · 쿨 5초"),

            // 승규 님's Sword Fight skills: F (R91, 10-09) and the edge skills on E (R103, 10-10; the bishop's R236).
            Idea(50, "왕의 반격", PieceKind.King, SkillOrigin.SwordFight, SkillTag.Counter | SkillTag.Block,
                "F · 0.5초 받아내기 자세. 근거리 공격을 막으면 반경 2.5 m를 밀어냄 · 쿨 8초" + OnMainSword),
            Idea(51, "꼬치 베기", PieceKind.Queen, SkillOrigin.SwordFight, SkillTag.Move | SkillTag.Block,
                "F · 6 m 줄을 따라 돌진하며 지나가는 적을 벰 · 쿨 8초" + OnMainSword),
            Idea(52, "열린 파일 포격", PieceKind.Rook, SkillOrigin.SwordFight, SkillTag.Block | SkillTag.Place,
                "F · 10 m 통로에 돌탑이 솟아 적을 띄워 밀어냄 · 쿨 7초" + OnMainSword),
            Idea(53, "관통 핀", PieceKind.Bishop, SkillOrigin.SwordFight, SkillTag.Block | SkillTag.Buff,
                "F · 8 m 안 지점의 발목을 금색 손이 잡음: 0.8초 묶임 + 감속 · 쿨 8초" + OnMainSword),
            Idea(54, "포크 강하", PieceKind.Knight, SkillOrigin.SwordFight, SkillTag.Move | SkillTag.Block,
                "F · 도약해 착지점 앞 두 자리의 적을 함께 밀어냄 · 쿨 7초 (1-나 포크와 같은 생각)" + OnMainSword),
            Idea(55, "왕의 귀환", PieceKind.King, SkillOrigin.SwordFight, SkillTag.Counter | SkillTag.Move,
                "E · 장외로 떨어지는 0.6초 안에 칼을 박고 매달렸다가 뛰어올라 착지 · 쿨 20초" + OnMainSword),
            Idea(56, "체크메이트 일섬", PieceKind.Queen, SkillOrigin.SwordFight, SkillTag.Block,
                "E · 가장자리의 적이 5 m 안이면 0.5초 금색 선 뒤 베어 장외로 · 쿨 15초" + OnMainSword),
            Idea(57, "성벽 붕괴", PieceKind.Rook, SkillOrigin.SwordFight, SkillTag.Place | SkillTag.Block,
                "E · 가장자리 바닥 3 × 2 m가 무너져 위의 기물이 떨어짐, 5초 뒤 복구 · 쿨 18초" + OnMainSword),
            Idea(58, "구원의 손", PieceKind.Bishop, SkillOrigin.SwordFight, SkillTag.Ally | SkillTag.Counter,
                "E · 떨어지는 아군을 금색 손으로 끌어올림 · 쿨 18초\n코드: R236(10-29)에 대각선 미끄럼으로 바뀌어, main에는 그림 부분만 남음"),
            Idea(59, "대각선 미끄럼", PieceKind.Bishop, SkillOrigin.SwordFight, SkillTag.Move | SkillTag.Block,
                "E · 가장자리에서 안쪽 대각선 2칸까지 미끄러져 빠져나옴. 길 위의 적은 옆으로 1.5 m 밀림 · 쿨 18초" + OnMainSword),
            Idea(60, "벼랑 끝 역전", PieceKind.Knight, SkillOrigin.SwordFight, SkillTag.Counter | SkillTag.Move | SkillTag.Block,
                "E · 벼랑 밖으로 공중제비해 적 뒤에 착지, 뒷발차기 · 쿨 16초" + OnMainSword),

            // The 2.0 plans' candidates, not made yet.
            Idea(21, "길막", PieceKind.Pawn, SkillOrigin.Plan2, SkillTag.Counter | SkillTag.Place, "양팔을 벌리고 버팀: 정면에서 온 적은 튕겨 나감, 대각선에서 맞으면 크게 넘어짐"),
            Idea(22, "폰 사슬", PieceKind.Pawn, SkillOrigin.Plan2, SkillTag.Ally | SkillTag.Buff, "팀원과 팔짱: 한 명이 넘어질 일을 당해도 다른 한 명이 붙잡아 줌"),
            Idea(23, "갬빗", PieceKind.Pawn, SkillOrigin.Plan2, SkillTag.Ally | SkillTag.Block, "공처럼 웅크림: 팀원이 집어 던지면 볼링공"),
            Idea(24, "뜀틀", PieceKind.Knight, SkillOrigin.Plan2, SkillTag.Move | SkillTag.Block, "앞 기물의 머리를 짚고 넘음, 세 번까지 연달아"),
            Idea(25, "태우기", PieceKind.Knight, SkillOrigin.Plan2, SkillTag.Ally | SkillTag.Move, "팀원을 태우고 달리다 앞 위로 던져 올림"),
            Idea(26, "스머더드 메이트", PieceKind.Knight, SkillOrigin.Plan2, SkillTag.Block, "뭉친 적 한가운데로 내리꽂아 전부 흩음"),
            Idea(27, "대각선 미끄럼길", PieceKind.Bishop, SkillOrigin.Plan2, SkillTag.Place | SkillTag.Block | SkillTag.Ally, "대각선 빛길: 적은 끝까지 미끄러지고 팀원은 빨리 지나감"),
            Idea(28, "구원의 밧줄", PieceKind.Bishop, SkillOrigin.Plan2, SkillTag.Ally | SkillTag.Counter, "떨어지는 팀원을 밧줄로 건져 올림"),
            Idea(30, "성벽", PieceKind.Rook, SkillOrigin.Plan2, SkillTag.Counter | SkillTag.Place, "단단한 탑이 됨: 길을 막고 팀원의 디딤돌이 됨"),
            Idea(31, "밀어내기", PieceKind.Rook, SkillOrigin.Plan2, SkillTag.Block, "팔을 벌리고 천천히 밀고 감: 앞의 무리가 통째로 밀림"),
            Idea(32, "꼬챙이", PieceKind.Queen, SkillOrigin.Plan2, SkillTag.Move | SkillTag.Block, "4칸 곧게 지나가며 줄 위의 적을 차례로 날림"),
            Idea(33, "호위", PieceKind.Queen, SkillOrigin.Plan2, SkillTag.Ally | SkillTag.Buff, "팀원 한 명이 3초 동안 넘어지지 않음"),
            Idea(34, "퀸 희생", PieceKind.Queen, SkillOrigin.Plan2, SkillTag.Block, "대폭발하고 폰으로 돌아감"),
        };

        public static SkillEntry Find(int id)
        {
            foreach (var e in All)
                if (e.id == id) return e;
            return null;
        }

        public static string Name(int id)
        {
            var e = Find(id);
            return e != null ? e.name : id == 0 ? "" : "스킬 " + id;
        }

        public static string How(int id)
        {
            var e = Find(id);
            return e != null ? e.how : "";
        }

        // The promotion cards' lines as the game has them (PawnRushPromotion.SkillLine), by skill.
        static readonly System.Collections.Generic.Dictionary<int, string> cards = new System.Collections.Generic.Dictionary<int, string>
        {
            { PawnRushSkillIds.TwoSteps, "두 칸을 빠르게 내딛는다" },
            { PawnRushSkillIds.TurnLeap, "높이 뛰어 꺾어 착지한다. 틈과 턱을 넘기 좋다" },
            { PawnRushSkillIds.CrossRope, "대각선 밧줄을 깔아 길을 막는다. 상대 견제용" },
            { PawnRushSkillIds.ChainPull, "사슬을 던져 적은 끌어오고 팀원은 옆으로 데려온다" },
            { PawnRushSkillIds.RingShove, "둘레의 상대를 모두 밀쳐 낸다. 몸싸움과 방해에 강하다" },
            { PawnRushSkillIds.EnPassant, "옆을 스쳐 지나간 적의 발을 건다" },
            { PawnRushSkillIds.Fork, "뛰어올라 앞 좌우 두 자리를 찬다. 둘을 한꺼번에 맞혀야 넘어진다" },
            { PawnRushSkillIds.Pin, "빛줄로 적을 꿰어 뒤의 적이나 벽에 묶는다" },
            { PawnRushSkillIds.Castling, "팀원과 자리를 바꾼다. 팀원이 스킬 키로 받아야 한다" },
            { PawnRushSkillIds.Check, "적 한 명을 지목한다. 제때 그 칸을 벗어나지 못하면 날아간다" },
            { PawnRushSkillIds.Ward, "곁의 팀원을 잠깐 호위한다. 호위 받는 팀원은 넘어지지 않는다" },
            { PawnRushSkillIds.Slash, "조준한 쪽으로 긴 검기를 날려 줄 위의 적을 넘어뜨린다" },
            { PawnRushSkillIds.CastleSwap, "가까운 팀원과 자리를 바꾼다. 팀원이 스킬 키로 받아야 한다" },
            { PawnRushSkillIds.AirVolley, "떠올라 견제탄을 두 발 쏜다. X자 파동에 맞으면 넘어진다" },
            { PawnRushSkillIds.PressLeap, "조준한 곳으로 뛰어내려 둘레의 적을 납작하게 만든다" },
            { PawnRushSkillIds.Squeeze, "몸을 낮추고 짧게 비집고 나간다. 부딪힌 기물은 옆으로 비킨다" },
            { PawnRushSkillIds.TouchMove, "건드린 적은 잠깐 멈추지 못한다. 넘어진 팀원은 바로 세운다" },
            { PawnRushSkillIds.BackKick, "뒷발로 차서 잡고 있던 것을 놓치게 한다. 한동안 아무것도 못 잡는다" },
            { PawnRushSkillIds.Ghost, "잠깐 적의 몸과 스킬이 나를 그냥 통과한다" },
            { PawnRushSkillIds.BackPush, "바로 앞의 팀원을 받쳐 곧게 쏘아 보낸다" },
            { PawnRushSkillIds.TwoHares, "적 둘을 고무줄로 묶는다. 멀어지면 튕겨 서로 부딪힌다" },
            { PawnRushSkillIds.BentLeap, "높이 뛰어 장애물과 사람을 넘는다. 공중에서 한 번 꺾는다" },
            { PawnRushSkillIds.Fianchetto, "자리를 잡고 멀리 겨눈다. 맞은 적은 발이 무거워진다" },
            { PawnRushSkillIds.KingStep, "한 칸 비킨다. 잡혀도 넘어져도 쓴다" },
            { PawnRushSkillIds.SteeredSteps, "두 걸음을 끊어 달린다. 둘째 걸음은 원하는 쪽으로 꺾는다" },
            { PawnRushSkillIds.LongSnipe, "멀리서 길게 모아 쏜다. 멀수록 세게 맞는다" },
            { PawnRushSkillIds.ChainBehind, "가벼운 적은 끌어오고, 무거운 적과 벽에는 내가 날아간다" },
            { PawnRushSkillIds.KingSummon, "팀원을 내 앞 좌우로 불러 세우고 함께 밀고 나간다" },
            { PawnRushSkillIds.FlankShot, "달리면서 빛줄기를 쏴 옆에서 밀어낸다" },
            { PawnRushSkillIds.CastleOver, "앞의 팀원을 넘어 내려서며 둘레를 밀어낸다" },
            { PawnRushSkillIds.ScholarThrust, "옆으로 비켜 나왔다가 등을 찔러 넘어뜨린다" },
            { PawnRushSkillIds.ChaseAway, "창으로 찔러 쫓아낸다. 비싼 기물일수록 멀리 날아간다" },
            { PawnRushSkillIds.CheckSeries, "체크를 이어 가며 적을 원하는 쪽으로 몰아간다" },
            { PawnRushSkillIds.BackRank, "옆이나 뒤로 곧게 미끄러져 닿은 적을 밀어낸다" },
            { PawnRushSkillIds.FileCharge, "줄을 그어 곧게 돌진한다. 길 위의 적을 양옆으로 튕긴다" },
            { PawnRushSkillIds.KingRun, "박자 맞춰 다섯 걸음. 딛을 때마다 붙잡힌 것을 떨친다" },
            { PawnRushSkillIds.Promote, "마지막 한 칸을 내딛어 잠깐 퀸이 된다" },
            { PawnRushSkillIds.Smother, "뭉친 적 한가운데로 떨어져 납작하게 만든다" },
            { PawnRushSkillIds.LongDiagonal, "끝까지 닿는 빛의 줄로 밟는 적의 발을 박는다" },
            { PawnRushSkillIds.Chariot, "10초 동안 전차가 되어 곧게 달리며 들이받는다" },
            { PawnRushSkillIds.QueenSac, "몸을 던져 둘레를 모두 날리고 퀸은 잡힌다" },
            { PawnRushSkillIds.KingMarch, "행진하며 곁의 팀원을 지키고 앞을 밀어낸다" },
            { PawnRushSkillIds.QueenGlide, "여덟 방향 가운데 하나로 미끄러져 원하는 자리에 딱 선다" },
            { PawnRushSkillIds.EightLines, "여덟 방향 빛줄을 터뜨린다. 줄 사이에 선 적은 멀쩡하다" },
            { PawnRushSkillIds.WallCharge, "곧게 달리며 뒤에 가슴 높이 성벽을 세운다" },
            { PawnRushSkillIds.SnipeHigh, "멀리서 길게 모아 쏜다. 멀수록 세게 맞는다" },
        };

        /// <summary>A skill's line for the promotion card.</summary>
        public static string Card(int id)
        {
            var e = Find(id);
            if (e == null) return "";
            if (!string.IsNullOrEmpty(e.card)) return e.card;
            if (cards.TryGetValue(id, out string line)) return line;
            int cutAt = e.how.IndexOf('\n');
            return cutAt > 0 ? e.how.Substring(0, cutAt) : e.how;
        }
    }
}
