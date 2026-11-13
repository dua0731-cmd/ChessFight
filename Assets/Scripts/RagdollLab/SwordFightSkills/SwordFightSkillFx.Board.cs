using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Sword Fight skill effects in the Pawn Rush board look (R126; 승규 님 10-13: "소드 파이트 그래픽 색도 폰 러쉬 퀸
    /// 오브 더 힐 같은 색으로 바꿔줘"). The same piece wears the same colours in all three modes (the values are Queen of the
    /// Hill's board look): king and queen polished gold, rook the soft beige pad (#D3B596), bishop silver with its warnings on
    /// dark board squares, knight silver; the other side's on a mauve block with a mauve rim and mauve stripes, my side's
    /// with a cream rim; ink the course's navy everywhere. Only what the effects look like changes: every effect keeps its
    /// moment, place, size, motion and length, and the hit stops, slow motions and shakes are untouched.
    ///
    /// Every colour of the look is in this one table so it can be changed in one place (the next direction may be ivory,
    /// ebony, walnut, gold and silver only). Design A comes back with <see cref="BoardLook"/> false (or if the board shaders
    /// are missing).
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>The board look; false = design A as before (read when the effects start).</summary>
        public static bool BoardLook = true;

        static Kit.Palette BPal(string main, string light, string deep, float metal = 0f) => new Kit.Palette(main, light, deep, "#24242F", metal);

        static readonly Kit.Palette BOrange = BPal("#D3B596", "#ECDCC8", "#A3876B");
        static readonly Kit.Palette BGold = BPal("#E9B23C", "#FFF1B8", "#9E6914", 1f);
        /// <summary>R129 (승규 님 10-13: "킹이랑 퀸 색을 구별 해줘 퀸이 더 빛이 강하게"): the king's gold is deeper and
        /// quieter (antique), the queen's brighter and glowing (over 1, so the bloom catches it where there is one) (the same values in all three modes).</summary>
        static Kit.Palette BKing => PawnRushSkillFx.BKing;
        static Kit.Palette BQueen => PawnRushSkillFx.BQueen;
        static readonly Kit.Palette BSilver = BPal("#C2C9D3", "#FAFBFD", "#7B8491", 1f);
        /// <summary>The bishop's warning squares: the board's dark squares.</summary>
        static readonly Kit.Palette BSquare = BPal("#2E3142", "#5A5F75", "#1C1D26");
        /// <summary>Board dust: cream, walnut in the shade.</summary>
        static readonly Kit.Palette BCream = BPal("#EBD3B5", "#FFF4E4", "#9A644C");
        static readonly Kit.Palette BGrey = BPal("#9AA3B5", "#DDE2EC", "#6B7385");
        static readonly Kit.Palette BIvory = BPal("#F3E7D3", "#FFFFFF", "#C4AE90", 0.35f);
        static readonly Color BNavy = Kit.Hex("#24242F"), BCreamRim = Kit.Hex("#F2D9BD");

        /// <summary>These effects draw the board look (its shaders were found).</summary>
        bool board;

        /// <summary>Set the board look up, right after the kit is made (before any part is).</summary>
        void BuildBoardLook()
        {
            board = BoardLook && kit != null && kit.UseBoardLook();
            if (!board) return;
            kit.king = BKing;
            kit.queen = BQueen;
            kit.rook = BOrange;
            kit.bishop = BSilver;
            kit.knight = BSilver;
            kit.ally = BOrange;
            kit.enemy = Kit.EnemyRed;   // R130: red, not mauve ("빨간색 계열로")
            kit.dust = BCream;
            kit.grey = BGrey;
            kit.daze = BIvory;
            kit.selfFill = BCreamRim;
            kit.selfInk = BNavy;
            kit.boardBase = BNavy;
            kit.boardInk = BNavy;
            kit.allyRim = BCreamRim;
            kit.boardTile = BOrange;
            // The king's sparks off the stone: his gold.
            Spark = BKing;
        }

        /// <summary>The bishop's warning fill: dark board squares in the board look, its own colour in design A.</summary>
        Kit.Palette? BishopWarnFill => board ? BSquare : (Kit.Palette?)null;

        /// <summary>A piece's colours for its effects (the cast, the windup, the edge skill's marks, the head mark): the
        /// kit's, so the board look's once it is on (10-13, 승규 님: the edge marks and the head marks still wore design
        /// A's, they read the static colours).</summary>
        Kit.Palette PieceColors(PieceKind kind) => kind switch
        {
            PieceKind.King => kit.king,
            PieceKind.Queen => kit.queen,
            PieceKind.Rook => kit.rook,
            PieceKind.Bishop => kit.bishop,
            PieceKind.Knight => kit.knight,
            _ => kit.grey,
        };

        /// <summary>A piece's colours for the bottom-right cards and the blade (its skill's glow and its edge skill's):
        /// the board look's, or design A's.</summary>
        public static Kit.Palette CardColors(PieceKind kind)
        {
            if (!BoardLook) return SwordFightSkills.Colors(kind);
            return kind == PieceKind.King ? BKing : kind == PieceKind.Queen ? BQueen
                : kind == PieceKind.Bishop || kind == PieceKind.Knight ? BSilver
                : BOrange;
        }
    }
}
