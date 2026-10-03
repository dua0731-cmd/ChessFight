using System;
using ChessFight.Network;

namespace ChessFight.Game
{
    public struct LoadingTip
    {
        public readonly string Title, Text;
        public readonly PieceKind Piece;
        public LoadingTip(string title, string text, PieceKind piece) { Title = title; Text = text; Piece = piece; }
    }

    // What the loading screen says for each game mode. Pawn Rush's lines come
    // from the Pawn Rush design document v1.0 (2026-09-28); "승격" there is still
    // "전직" until the team settles the word.
    public sealed class LoadingContent
    {
        // Title: the mode's name, big and gold. Kicker: the small line above it.
        public string Title = "", Kicker = "", Rule = "", Note = "";
        public LoadingTip[] Tips = Array.Empty<LoadingTip>();

        // `teamSize`: players on the larger team, for the rules that count heads.
        public static LoadingContent For(GameModeInfo mode, int teamSize)
        {
            int size = Math.Max(1, Math.Min(6, teamSize));
            // More than half the team: 2 of 2, 2 of 3, 3 of 4, 3 of 5, 4 of 6.
            int need = size / 2 + 1;
            string versus = size + " VS " + size;
            string title = mode != null ? mode.Name : "";
            switch (mode != null ? mode.Key : "")
            {
                case "kingrush":
                    return new LoadingContent
                    {
                        Title = title,
                        Kicker = versus + " · 팀 레이스",
                        Rule = "팀원 <color=#FFC93D>" + need + "명</color>이 먼저 결승선을 넘으면 승리",
                        Note = "승격 지점에 먼저 닿은 폰은 다른 기물이 될 수 있어요",
                        Tips = new[]
                        {
                            new LoadingTip("TIP · 부축", "두 걸음으로 넘어진 팀원에게 닿으면 바로 일으켜요. 둘 다 1.5초 동안 빨라져요.", PieceKind.Pawn),
                            new LoadingTip("TIP · 승격", "승격 지점에 팀에서 가장 먼저 닿은 폰이 발판을 밟아 기물을 골라요.", PieceKind.Queen),
                            new LoadingTip("TIP · 과반 승리", "1등 혼자로는 못 이겨요. 우리 팀 " + need + "명이 먼저 결승선을 넘어야 해요.", PieceKind.Rook),
                            new LoadingTip("TIP · 몸싸움", "결승 직전 8m에서는 잡기가 안 돼요. 밀기는 돼요.", PieceKind.Knight)
                        }
                    };
                case "swordfight":
                    return new LoadingContent
                    {
                        Title = title,
                        Kicker = versus + " · 팀 데스매치",
                        Rule = "칼로 상대를 넘어뜨려 장외로 보내세요",
                        Note = "지금은 폰으로 플레이해요",
                        Tips = new[]
                        {
                            new LoadingTip("TIP · 발도", "좌클릭을 누르고 있으면 칼을 빼 들어요. 떼면 허리에 넣어요.", PieceKind.Knight),
                            new LoadingTip("TIP · 조작 전환", "F6으로 클릭 베기와 물리 드래그를 바꿀 수 있어요.", PieceKind.Pawn)
                        }
                    };
                default:
                    return new LoadingContent
                    {
                        Title = title,
                        Kicker = mode != null ? mode.Tagline : "",
                        Rule = mode != null ? mode.Summary : ""
                    };
            }
        }
    }
}
