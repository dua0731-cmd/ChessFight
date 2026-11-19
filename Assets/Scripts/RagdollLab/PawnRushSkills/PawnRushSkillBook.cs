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
    /// is theirs: a sword in the hand, a tower to climb), the 2.0 plans (Docs/Skills/PAWN_RUSH_2_SKILL_PLANS.md), or the
    /// skill idea book (Docs/Skills/PAWN_RUSH_2_SKILL_IDEAS.md).</summary>
    public enum SkillOrigin { PawnRush, QueenHill, SwordFight, Plan2, IdeaBook }

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
        public static readonly SkillOrigin[] Origins = { SkillOrigin.PawnRush, SkillOrigin.IdeaBook, SkillOrigin.QueenHill, SkillOrigin.SwordFight, SkillOrigin.Plan2 };

        public static string OriginName(SkillOrigin origin) => origin switch
        {
            SkillOrigin.PawnRush => "폰 러쉬",
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
                "키 = 0.2초 준비 뒤 앞으로 3 m 도약, 착지하며 앞 좌우 두 자리(주황 고리)를 참\n두 자리에 적이 한꺼번에 있으면 둘 다 넘어지며 날아감 · 한 자리만이면 밀기만"),
            Made(PawnRushSkillIds.Pin, "핀", PieceKind.Bishop, SkillOrigin.Plan2, SkillTag.Block | SkillTag.Buff,
                "키 = 조준 (보라 선, 9 m) · 좌클릭 = 쏘기 · 우클릭/키 = 취소\n처음 맞은 적 뒤 4 m 안, 같은 줄 위에 다른 적이나 벽 · 기둥이 있으면 둘이 2 m 줄로 5초 묶임\n뒤에 아무것도 없으면 맞은 적만 0.5초 휘청"),
            Made(PawnRushSkillIds.Castling, "캐슬링", PieceKind.Rook, SkillOrigin.Plan2, SkillTag.Ally | SkillTag.Move,
                "키 = 조준 (팀원을 보면 청록 선, 12 m) · 좌클릭 = 제안 · 우클릭/키 = 취소\n팀원이 2초 안에 자기 스킬 키(F)로 받으면 둘이 날아 자리를 바꿈 (더미 팀원은 0.6초 뒤 스스로 받음)"),
            Made(PawnRushSkillIds.Check, "체크!", PieceKind.Queen, SkillOrigin.Plan2, SkillTag.Block,
                "키 = 조준 (적을 보면 빨간 고리, 9 m) · 좌클릭 = 체크! · 우클릭/키 = 취소\n그 적이 서 있던 자리가 빨간 칸 → 1.5초 안에 못 나가면 넘어지며 날아감\n막기: 사이에 선 적 팀 기물이 대신 맞음 · 잡기: 그 전에 내가 맞으면 취소"),

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

            // 승규 님's Pawn Rush skills that are not the five above: the king's, and the earlier forms of three pieces'.
            Idea(41, "왕의 부름", PieceKind.King, SkillOrigin.PawnRush, SkillTag.Ally | SkillTag.Move,
                "같은 구간 팀원을 2명까지 골라 부름. 불린 팀원이 3초 안에 자기 스킬 키로 받으면 0.6초 만에 킹 뒤로 옴 (오는 동안 무적)" + OnMainKing),
            Idea(42, "교차 밧줄 (설치형 X자 덫)", PieceKind.Bishop, SkillOrigin.PawnRush, SkillTag.Place | SkillTag.Block,
                "4.5 m 안에 X자 밧줄을 6초 깔아 둠. 지나가는 적이 걸려 묶이고, 룩은 끊고 지나감 · 쿨 8초 (10-07의 첫 모양)" + OnMainSwitch),
            Idea(29, "직선 돌파", PieceKind.Rook, SkillOrigin.PawnRush, SkillTag.Move | SkillTag.Block,
                "조준 뒤 4칸 돌진, 최대 3명을 양옆으로 튕김, 바리케이드를 부숨, 공중 돌진도 됨 · 쿨 7초 (10-07의 첫 모양)" + InHistory),
            Idea(43, "꺾어 도약 (옛 자유 도약)", PieceKind.Knight, SkillOrigin.PawnRush, SkillTag.Move | SkillTag.Block,
                "1.6 m 높이로 4.5 m 도약. 공중에서 한 번 더 누르면 3 m 안 적의 머리를 찍거나 90° 꺾어 다시 차고 나감 · 쿨 7초 (10-07의 첫 모양)" + InHistory),

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
