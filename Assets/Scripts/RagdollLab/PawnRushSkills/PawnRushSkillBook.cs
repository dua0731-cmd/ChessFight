using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>What a skill is for, the way the user sorted skills (2026-11-14): its own movement, hindering an enemy's,
    /// helping a teammate's, answering an enemy's skill, a thing put down, a state put on a body.</summary>
    [Flags]
    public enum SkillTag { None = 0, Move = 1, Block = 2, Ally = 4, Counter = 8, Place = 16, Buff = 32 }

    /// <summary>One line of the skill book: a skill's number (PawnRushSkillIds), name, the piece it was thought up for, its
    /// tags, whether it is made yet, and how to use it.</summary>
    public sealed class SkillEntry
    {
        public int id;
        public string name;
        public PieceKind home;
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
    /// lists - the five as they are, plan 1-나's five, and the candidates of Docs/Skills/PAWN_RUSH_2_SKILL_PLANS.md that
    /// are not made yet (shown dim; they cannot be put in a slot). A new skill is one more line here, its number in
    /// PawnRushSkillIds and its code in RagdollPawn.SkillPlans.cs (RunNewSkill).
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

        static SkillEntry Made(int id, string name, PieceKind home, SkillTag tags, string how) =>
            new SkillEntry { id = id, name = name, home = home, tags = tags, built = true, how = how };

        static SkillEntry Idea(int id, string name, PieceKind home, SkillTag tags, string how) =>
            new SkillEntry { id = id, name = name, home = home, tags = tags, built = false, how = how };

        public static readonly SkillEntry[] All =
        {
            // The skills as they are.
            Made(PawnRushSkillIds.TwoSteps, "첫 두 걸음", PieceKind.Pawn, SkillTag.Block | SkillTag.Ally,
                "키 = 1.5 m 걸음 · 0.1~0.8초 안에 한 번 더 = 앞 3 m 안의 서 있는 적이나 넘어진 팀원에게 휘어 들어감\n곧게 부딪히면 밀기, 비스듬히 부딪히면 넘어뜨림 · 넘어진 팀원은 일으킴"),
            Made(PawnRushSkillIds.TurnLeap, "꺾어 도약", PieceKind.Knight, SkillTag.Block,
                "앞쪽 1.5~9 m의 서 있는 적 머리 위에 빨간 고리가 뜨면 키 = 그 적 머리로 도약 (대상이 없으면 무시)\n찍힌 적은 선 채로 2초 해롱해롱 · 착지 1.5 m 안의 다른 적은 휘청"),
            Made(PawnRushSkillIds.CrossRope, "교차 밧줄", PieceKind.Bishop, SkillTag.Block | SkillTag.Buff,
                "키 = 조준 (7 m) · 좌클릭 = X자 밧줄을 적의 허리에 걸기 · 우클릭/키 = 취소\n6초 동안 걸어 다니며 끌고 다님 · 휠↑ 느슨하게 · 휠↓ 팽팽하게 · 키/우클릭 = 놓기 (팽팽하면 튕겨 옴)"),
            Made(PawnRushSkillIds.ChainPull, "직선 사슬 견인", PieceKind.Rook, SkillTag.Block | SkillTag.Ally,
                "키 = 조준 (10.5 m) · 좌클릭 = 사슬 던지기 · 우클릭/키 = 취소\n적은 앞 1.5 m로 끌려와 휘청 · 팀원은 2초 안에 우클릭으로 손잡이를 잡으면 옆으로 옴"),
            Made(PawnRushSkillIds.RingShove, "팔방 밀치기", PieceKind.Queen, SkillTag.Block,
                "키 = 0.35초 예고 뒤 둘레 3 m 충격파 · 안쪽 1.8 m는 넘어지며 날아가고 바깥은 밀림"),
            // Skill plan 1-나.
            Made(PawnRushSkillIds.EnPassant, "앙파상", PieceKind.Pawn, SkillTag.Block | SkillTag.Counter,
                "적이 내 옆 2 m 안을 나보다 빠르게 스쳐 지나가면 그 적 발밑에 주황 고리가 1초 뜸 → 그동안 키 = 뛰어들어 발 걸기\n걸린 적은 달리던 쪽으로 넘어져 미끄러짐 · 스쳐 지나간 적이 없으면 무시\n혼자 시험: 왼쪽 창 \"앙파상 시험\" (M)"),
            Made(PawnRushSkillIds.Fork, "포크", PieceKind.Knight, SkillTag.Block | SkillTag.Move,
                "키 = 0.2초 준비 뒤 앞으로 3 m 도약, 착지하며 앞 좌우 두 자리(주황 고리)를 참\n두 자리에 적이 한꺼번에 있으면 둘 다 넘어지며 날아감 · 한 자리만이면 밀기만"),
            Made(PawnRushSkillIds.Pin, "핀", PieceKind.Bishop, SkillTag.Block | SkillTag.Buff,
                "키 = 조준 (보라 선, 9 m) · 좌클릭 = 쏘기 · 우클릭/키 = 취소\n처음 맞은 적 뒤 4 m 안, 같은 줄 위에 다른 적이나 벽 · 기둥이 있으면 둘이 2 m 줄로 5초 묶임\n뒤에 아무것도 없으면 맞은 적만 0.5초 휘청"),
            Made(PawnRushSkillIds.Castling, "캐슬링", PieceKind.Rook, SkillTag.Ally | SkillTag.Move,
                "키 = 조준 (팀원을 보면 청록 선, 12 m) · 좌클릭 = 제안 · 우클릭/키 = 취소\n팀원이 2초 안에 자기 스킬 키(F)로 받으면 둘이 날아 자리를 바꿈 (더미 팀원은 0.6초 뒤 스스로 받음)"),
            Made(PawnRushSkillIds.Check, "체크!", PieceKind.Queen, SkillTag.Block,
                "키 = 조준 (적을 보면 빨간 고리, 9 m) · 좌클릭 = 체크! · 우클릭/키 = 취소\n그 적이 서 있던 자리가 빨간 칸 → 1.5초 안에 못 나가면 넘어지며 날아감\n막기: 사이에 선 적 팀 기물이 대신 맞음 · 잡기: 그 전에 내가 맞으면 취소"),
            // Candidates, not made yet.
            Idea(21, "길막", PieceKind.Pawn, SkillTag.Counter | SkillTag.Place, "양팔을 벌리고 버팀: 정면에서 온 적은 튕겨 나감, 대각선에서 맞으면 크게 넘어짐"),
            Idea(22, "폰 사슬", PieceKind.Pawn, SkillTag.Ally | SkillTag.Buff, "팀원과 팔짱: 한 명이 넘어질 일을 당해도 다른 한 명이 붙잡아 줌"),
            Idea(23, "갬빗", PieceKind.Pawn, SkillTag.Ally | SkillTag.Block, "공처럼 웅크림: 팀원이 집어 던지면 볼링공"),
            Idea(24, "뜀틀", PieceKind.Knight, SkillTag.Move | SkillTag.Block, "앞 기물의 머리를 짚고 넘음, 세 번까지 연달아"),
            Idea(25, "태우기", PieceKind.Knight, SkillTag.Ally | SkillTag.Move, "팀원을 태우고 달리다 앞 위로 던져 올림"),
            Idea(26, "스머더드 메이트", PieceKind.Knight, SkillTag.Block, "뭉친 적 한가운데로 내리꽂아 전부 흩음"),
            Idea(27, "대각선 미끄럼길", PieceKind.Bishop, SkillTag.Place | SkillTag.Block | SkillTag.Ally, "대각선 빛길: 적은 끝까지 미끄러지고 팀원은 빨리 지나감"),
            Idea(28, "구원의 밧줄", PieceKind.Bishop, SkillTag.Ally | SkillTag.Counter, "떨어지는 팀원을 밧줄로 건져 올림"),
            Idea(29, "직선 돌파", PieceKind.Rook, SkillTag.Move | SkillTag.Block, "4칸 곧게 돌진, 부딪힌 적은 양옆으로 (옛 스킬)"),
            Idea(30, "성벽", PieceKind.Rook, SkillTag.Counter | SkillTag.Place, "단단한 탑이 됨: 길을 막고 팀원의 디딤돌이 됨"),
            Idea(31, "밀어내기", PieceKind.Rook, SkillTag.Block, "팔을 벌리고 천천히 밀고 감: 앞의 무리가 통째로 밀림"),
            Idea(32, "꼬챙이", PieceKind.Queen, SkillTag.Move | SkillTag.Block, "4칸 곧게 지나가며 줄 위의 적을 차례로 날림"),
            Idea(33, "호위", PieceKind.Queen, SkillTag.Ally | SkillTag.Buff, "팀원 한 명이 3초 동안 넘어지지 않음"),
            Idea(34, "퀸 희생", PieceKind.Queen, SkillTag.Block, "대폭발하고 폰으로 돌아감"),
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
