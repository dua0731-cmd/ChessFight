using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Queen of the Hill skill effects in the Pawn Rush board look (R126; 승규 님 10-12: "퀸 오브 더 힐도 마찬가지로 폰
    /// 러쉬처럼 각 기물들 별로 그래픽 바꿔줘"). The same piece wears the same colours in both modes: king and queen
    /// polished gold, rook the soft beige pad (#D3B596, the colour 승규 님 turned down twice), bishop silver on dark board
    /// squares, knight silver, the pawn the beige pad; the other side's on a mauve block with a mauve rim and mauve stripes,
    /// my side's with a cream rim; ink the course's navy everywhere. Only what the effects look like changes: every effect
    /// keeps its moment, place, size, motion and length, and the hit stops, shakes and squashes are untouched.
    ///
    /// - Warnings, stamps and captured squares are pads standing up as slabs on their navy blocks (Skill Tile Board),
    ///   solid (the chessboard floor never shows through), sinking as they go. The queen's squares draped over a slope stay
    ///   flat but just as solid.
    /// - Props are glossy toys and polished metal (Skill Gloss): the sceptre and the sword's guard gold, the blade silver,
    ///   the bishop's hover tile a dark board square.
    /// - Puffs are board dust (cream, walnut in the shade); the guard's outline gold.
    /// Design A comes back with <see cref="BoardLook"/> false (or if the board shaders are missing).
    /// </summary>
    public partial class QueenHillSkillFx
    {
        /// <summary>The board look; false = design A as before (read when the effects start).</summary>
        public static bool BoardLook = true;

        static Palette BPal(string main, string light, string deep, float metal = 0f) => new Palette(main, light, deep, "#24242F", metal);

        static readonly Palette BOrange = BPal("#D3B596", "#ECDCC8", "#A3876B");
        static readonly Palette BGold = BPal("#E9B23C", "#FFF1B8", "#9E6914", 1f);
        /// <summary>R129 (승규 님 10-13: "킹이랑 퀸 색을 구별 해줘 퀸이 더 빛이 강하게"): the king's gold is deeper and
        /// quieter (antique), the queen's brighter and glowing (over 1, so the bloom catches it where there is one) (the same values as Pawn Rush's and Sword Fight's).</summary>
        static readonly Palette BKing = BPal("#C9952F", "#F1D48A", "#7C5517", 1f);
        // R130 ("퀸 색이 약간 노란색 같아 킹이랑 구분 되게 금빛 나는 색으로"): a warm polished gold (it was #FFD54A, a lemon yellow).
        static readonly Palette BQueen = BGlow(BPal("#F2B73F", "#FFF2C4", "#A8690E", 1f), 1.08f, 1.4f);

        static Palette BGlow(Palette p, float main, float light)
        {
            p.main = new Color(p.main.r * main, p.main.g * main, p.main.b * main, 1f);
            p.light = new Color(p.light.r * light, p.light.g * light, p.light.b * light, 1f);
            return p;
        }
        static readonly Palette BSilver = BPal("#C2C9D3", "#FAFBFD", "#7B8491", 1f);
        /// <summary>R130 ("적이 스킬을 쓸 때 미리보기가 … 빨간색 계열로"): the other side's colour, a crimson lacquer red
        /// (SkillInkKit.EnemyRed), not the mauve.</summary>
        static readonly Palette BEnemy = BPal("#C24A41", "#F2B8AC", "#7E2A2C");
        /// <summary>The bishop's squares: the board's dark squares.</summary>
        static readonly Palette BSquare = BPal("#2E3142", "#5A5F75", "#1C1D26");
        /// <summary>Board dust: cream, walnut in the shade.</summary>
        static readonly Palette BCream = BPal("#EBD3B5", "#FFF4E4", "#9A644C");
        static readonly Palette BStone = BPal("#9C98A6", "#D3D0DA", "#5C5868");
        static readonly Palette BGrey = BPal("#9AA3B5", "#DDE2EC", "#6B7385");
        static readonly Palette BBlade = BPal("#DCE2EA", "#FFFFFF", "#8D96A3", 1f);
        /// <summary>R130: the king's sceptre rod in polished ebony and its ivory band (a chess piece's own materials).</summary>
        static readonly Palette BEbony = BPal("#2B2833", "#7A7486", "#0F0D13", 0.35f);
        static readonly Palette BIvory = BPal("#F3E7D3", "#FFFFFF", "#C4AE90", 0.35f);
        static readonly Color BNavy = Hex("#24242F"), BCreamRim = Hex("#F2D9BD");

        /// <summary>Set the board look up, once the kit exists.</summary>
        void BuildBoardLook()
        {
            if (!BoardLook || !UseBoardLook()) return;
            look.King = BKing;
            look.Queen = BQueen;
            look.Rook = BOrange;
            look.Bishop = BSilver;
            look.Knight = BSilver;
            look.TeamAlly = BOrange;
            look.TeamEnemy = BEnemy;
            look.Dust = BCream;
            look.Grey = BGrey;
            look.Stone = BStone;
            look.SelfFill = BCreamRim;
            look.SelfInk = BNavy;
            look.SocketColor = Hex("#15151C");
            look.GoldAccent = BGold.light;
            boardTile = BOrange;
            // The guard's outline round a body: gold with a navy edge.
            matWardLine.SetColor("_InkColor", BKing.main);
            matWardInk.SetColor("_InkColor", BNavy);
        }

        /// <summary>
        /// A warning in the board look: a beige pad (a piece's own: gold or silver polished, the bishop's a dark board
        /// square) on a navy block with a darker bevel inside its edge; mine plain, my side's with a cream rim, the other
        /// side's on a mauve block with a thick mauve rim and mauve stripes. Thinner while the windup is weak.
        /// </summary>
        void BoardWarn(Tile t, RagdollPawn caster, float strength, float flash, Palette? fillOverride)
        {
            bool pawn = caster != null && caster.Piece == PieceKind.Pawn && fillOverride == null;
            bool bishop = caster != null && caster.Piece == PieceKind.Bishop && fillOverride == null;
            var p = fillOverride ?? (pawn ? boardTile : bishop ? BSquare : PieceColors(caster));
            t.fill = A(p.main, Mathf.Lerp(0.9f, 1f, strength));
            t.thickScale = Mathf.Lerp(0.45f, 1f, strength);
            t.metal = p.metal;
            t.bevel = bishop ? A(BSilver.main, 0.95f) : A(p.deep, p.metal > 0.5f ? 0f : 0.85f);
            t.bevelWidth = bishop ? 0.05f : 0.07f;
            t.ink = A(boardInk, Mathf.Lerp(0.65f, 1f, strength));
            t.inkWidth = 0.035f;
            t.round = 0.07f;
            t.shape = 0f;
            BoardSide(t, caster, strength);
            // R130: charging dots and a neon edge (SkillInkKit.Charging, the same in every mode).
            float k = Mathf.Clamp01(Mathf.InverseLerp(0.3f, 1f, strength));
            t.charge = k;
            t.dot = A(Color.Lerp(p.main, p.light, 0.35f), 0.95f);
            var side = SideOf(caster);
            Color neon = side == Side.Enemy ? BEnemy.main : side == Side.Ally ? allyRim : p.light;
            t.glow = new Color(neon.r * 1.25f, neon.g * 1.25f, neon.b * 1.25f, Mathf.Max(flash, Mathf.Lerp(0.3f, 0.9f, k)));
            t.flash = flash;
            t.Apply();
        }

        /// <summary>The side marks on a pad: its block navy, a cream rim for my side's, a mauve block, a thick mauve rim
        /// and sliding mauve stripes for the other side's.</summary>
        void BoardSide(Tile t, RagdollPawn owner, float strength = 1f)
        {
            var side = SideOf(owner);
            t.baseColor = side == Side.Enemy ? BEnemy.deep : boardBase;
            t.rim = side == Side.Enemy ? A(BEnemy.main, Mathf.Lerp(0.7f, 1f, strength))
                : side == Side.Ally ? A(allyRim, Mathf.Lerp(0.7f, 1f, strength)) : Color.clear;
            t.rimWidth = side == Side.Enemy ? 0.13f : 0.07f;
            t.stripe = side == Side.Enemy ? A(BEnemy.main, 0.5f * strength) : Color.clear;
            t.stripePhase = clock * 0.5f;
        }

        /// <summary>A stamped pad of <paramref name="p"/> (a captured or landed square): solid, polished if gold or silver,
        /// on its navy block.</summary>
        void BoardStamp(Tile t, Palette p)
        {
            t.fill = A(p.main, 0.96f);
            t.metal = p.metal;
            t.bevel = A(p.deep, p.metal > 0.5f ? 0f : 0.85f);
            t.ink = A(boardInk, 1f);
            t.inkWidth = 0.04f;
            t.baseColor = boardBase;
        }

        /// <summary>A flat floor ring (a wave, a flash, an ink ring): no slab, no shadow.</summary>
        static void FlatRing(Tile t)
        {
            t.thick = 0f;
            t.shadow = 0f;
        }

        /// <summary>The bottom-right skill card (the Pawn Rush board card: the pads' navy, the key in the piece's pad
        /// colour), or design A's.</summary>
        public static void SkillCard(Rect r, string key, string name, string line, PieceKind kind, bool lit, float cooldown01)
        {
            if (BoardLook) PawnRushSkillFx.SkillCard(r, key, name, line, kind, lit, cooldown01);
            else SkillKeyCard.Draw(r, key, name, line, SkillKeyCard.Colors(kind), lit, false, cooldown01);
        }
    }
}
