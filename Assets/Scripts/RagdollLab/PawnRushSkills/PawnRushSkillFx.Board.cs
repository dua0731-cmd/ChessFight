using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R126: the Pawn Rush skill effects in a board look (승규 님 10-12, after a five-player test: "스킬 그래픽이 체스
    /// 느낌이 났으면 좋겠어 … 폰 러쉬로만 그래픽을 사진 색과 같이 이런 느낌으로 시험으로 … 지금 구현된 것보다 조금 더 좋게 …
    /// 연출 액션은 아무것도 건드리지 마", then "광택 있는 골드색 실버색도"). The colours are the course's arrow pads in the
    /// photo: orange pads (#FDA23D) with a darker bevel (#D28833) on navy blocks (#24242F), a mauve-rimmed one (#634C51), the
    /// cream (#F2D9BD) and walnut (#9A644C) board; plus polished gold and silver. Only what the effects look like changes:
    /// every effect keeps its moment, place, size, motion and length, and the hit stops, shakes and poses are untouched.
    ///
    /// - Warnings, stamps and captured squares are pads standing up as slabs on their blocks (Skill Tile Board): mine on
    ///   navy, my side's with a cream rim, the other side's on mauve with a mauve rim and mauve stripes; chevrons run along
    ///   a rook's charge and point along a knight's way and a pawn's step, like the course's arrow pads.
    /// - Piece colours: queen polished gold, rook orange with silver battlements, bishop silver on dark board squares,
    ///   knight silver, pawn orange (the other side's pawn mauve). The ink is the photo's navy everywhere.
    /// - Props are polished metal or glossy toys (Skill Gloss): the queen's chess pieces alternate gold and silver, the
    ///   crown's pearls, the bishop's caps, its pegs and its wire silver, the daze pawns ivory; the help pad and the rook's
    ///   little pads orange on navy.
    /// - Puffs: the queen's fire cools to cream and walnut board dust (no charcoal), the knight's wind is silver-white, the
    ///   rook's lightning and speed lines orange with a silver-white core.
    /// Design A comes back with <see cref="BoardLook"/> false (or if the board shaders are missing).
    /// </summary>
    public partial class PawnRushSkillFx
    {
        /// <summary>The R126 look; false = design A as before (read when the effects start).</summary>
        public static bool BoardLook = true;

        static Kit.Palette Pal(string main, string light, string deep, float metal = 0f) => new Kit.Palette(main, light, deep, "#24242F", metal);

        // 10-12: "주황색 패드가 채도가 높다", "아직도 높아 더 낮춰" - far less saturated than the photo's #FDA23D (bevel #D28833).
        static readonly Kit.Palette BOrange = Pal("#D3B596", "#ECDCC8", "#A3876B");
        static readonly Kit.Palette BGold = Pal("#E9B23C", "#FFF1B8", "#9E6914", 1f);
        static readonly Kit.Palette BSilver = Pal("#C2C9D3", "#FAFBFD", "#7B8491", 1f);
        static readonly Kit.Palette BMauve = Pal("#634C51", "#8E7177", "#45343A");
        /// <summary>The bishop's squares: the board's dark squares.</summary>
        static readonly Kit.Palette BSquare = Pal("#2E3142", "#5A5F75", "#1C1D26");
        /// <summary>Board dust: cream, walnut in the shade.</summary>
        static readonly Kit.Palette BCream = Pal("#EBD3B5", "#FFF4E4", "#9A644C");
        static readonly Kit.Palette BIvory = Pal("#F3E7D3", "#FFFFFF", "#C4AE90", 0.35f);
        static readonly Kit.Palette BPearl = Pal("#EEF1F5", "#FFFFFF", "#9AA3B0", 0.8f);
        static readonly Color BNavy = Kit.Hex("#24242F"), BCreamRim = Kit.Hex("#F2D9BD");

        /// <summary>These effects draw the board look (its shaders were found).</summary>
        bool board;
        Material matChessGold, matChessSilver, matRodBoard;

        /// <summary>Set the board look up, once design A's kit and the cartoon materials exist.</summary>
        void BuildBoardLook()
        {
            board = BoardLook && kit != null && kit.UseBoardLook();
            if (!board) return;

            kit.queen = BGold;
            kit.king = BGold;
            kit.rook = BOrange;
            kit.bishop = BSilver;
            kit.knight = BSilver;
            kit.ally = BOrange;
            kit.enemy = BMauve;
            kit.dust = BCream;
            kit.daze = BIvory;
            kit.selfFill = BCreamRim;
            kit.selfInk = BNavy;
            kit.boardBase = BNavy;
            kit.boardInk = BNavy;
            kit.allyRim = BCreamRim;
            kit.boardTile = BOrange;

            // The queen's fire cools to board dust, inked in navy (it was charcoal smoke).
            matFire.SetColor("_Lit", Kit.Hex("#F2DCC0"));
            matFire.SetColor("_Mid", Kit.Hex("#C9A27E"));
            matFire.SetColor("_Shade", Kit.Hex("#8A5E45"));
            matFire.SetFloat("_Ink", 0.2f);
            matFire.SetColor("_InkColor", BNavy);
            matDust.SetColor("_Lit", Kit.Hex("#FFF1DE"));
            matDust.SetColor("_Mid", Kit.Hex("#E0C3A0"));
            matDust.SetColor("_Shade", Kit.Hex("#A9805F"));
            matDust.SetColor("_Bite", Kit.Hex("#A9805F"));
            matDust.SetFloat("_Ink", 0.2f);
            matDust.SetColor("_InkColor", BNavy);
            // The knight's wind in silver-white.
            foreach (var m in new[] { matWind, matSwoosh })
            {
                m.SetColor("_Lit", BSilver.light);
                m.SetColor("_Mid", BSilver.main);
                m.SetColor("_Shade", BSilver.deep);
                m.SetColor("_InkColor", BNavy);
            }
            matWind.SetColor("_Bite", BSilver.main);
            // The rook's lightning and speed lines: a silver-white core, orange, navy ink.
            matVolt.SetColor("_Lit", new Color(1.3f, 1.33f, 1.4f));
            matVolt.SetColor("_Mid", BOrange.main);
            matVolt.SetColor("_Shade", BOrange.deep);
            matVolt.SetColor("_InkColor", BNavy);
            matCrack.SetColor("_Cold", Kit.Hex("#3A2E33"));

            // Polished chess pieces (gold and silver in turn) and the bishop's silver wire.
            var gloss = kit.matGloss.shader;
            matChessGold = GlossMaterial(gloss, BGold);
            matChessSilver = GlossMaterial(gloss, BSilver);
            matRodBoard = GlossMaterial(gloss, BSilver);
            matRodBoard.SetFloat("_Ink", 0f);
        }

        Material GlossMaterial(Shader shader, Kit.Palette p)
        {
            var m = Mat(shader);
            m.SetColor("_Lit", p.light);
            m.SetColor("_Mid", p.main);
            m.SetColor("_Shade", p.deep);
            m.SetFloat("_Metal", p.metal);
            m.SetColor("_Spec", new Color(1.45f, 1.42f, 1.3f));
            m.SetFloat("_Gloss", 60f);
            m.SetFloat("_Ink", 0.2f);
            m.SetColor("_InkColor", BNavy);
            m.SetColor("_HotColor", new Color(1.35f, 0.62f, 0.2f));
            m.SetFloat("_RimPower", 2.4f);
            return m;
        }

        /// <summary>The bottom-right skill card in the board look (10-12, 승규 님: "f 스킬 설명 UI 색깔도 그에 맞게"): the key in
        /// the piece's pad colour (pawn and rook the soft orange pad, queen gold, bishop and knight silver) on the pads' navy.</summary>
        public static void SkillCard(Rect r, string key, string name, string line, PieceKind kind, bool lit, float cooldown01)
        {
            if (!BoardLook)
            {
                SkillKeyCard.Draw(r, key, name, line, SkillKeyCard.Colors(kind), lit, false, cooldown01);
                return;
            }
            var pal = kind == PieceKind.Queen || kind == PieceKind.King ? BGold : kind == PieceKind.Bishop || kind == PieceKind.Knight ? BSilver : BOrange;
            SkillKeyCard.Draw(r, key, name, line, pal, lit, false, cooldown01, true);
        }

        /// <summary>The queen's chess piece material: gold and silver in turn in the board look, cartoon gold before.</summary>
        Material ChessMaterial(int i) => !board ? matChess : i % 2 == 0 ? matChessGold : matChessSilver;
    }
}
