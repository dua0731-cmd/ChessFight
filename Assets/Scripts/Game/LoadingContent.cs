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
        // The entrance screen shows the course hologram (Pawn Rush only, 10-27: the other modes keep the chessboard
        // until their own designs are settled).
        public bool CourseModel;

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
                        CourseModel = true,
                        Title = title,
                        Kicker = versus + " · 팀 레이스",
                        Rule = "팀원 <color=#FFC93D>" + need + "명</color>이 먼저 결승선을 넘으면 승리",
                        Note = "승격 지점에 먼저 닿은 폰은 다른 기물이 될 수 있어요",
                        Tips = new[]
                        {
                            new LoadingTip("TIP · 부축", "두 걸음으로 넘어진 팀원에게 닿으면 바로 일으켜요. 둘 다 1.5초 동안 빨라져요.", PieceKind.Pawn),
                            new LoadingTip("TIP · 승격", "승격 지점에 팀에서 가장 먼저 닿은 폰이 발판을 밟아 기물을 골라요.", PieceKind.Queen),
                            new LoadingTip("TIP · 과반 승리", "1등 혼자로는 못 이겨요. 우리 팀 " + need + "명이 먼저 결승선을 넘어야 해요.", PieceKind.Rook),
                            new LoadingTip("TIP · 몸싸움", "결승 직전 8m에서는 잡기가 안 돼요. 밀기는 돼요.", PieceKind.Knight),
                            // 10-25 (승규 님: more tips useful to players), from course 01 v0.4
                            new LoadingTip("TIP · 벽타기", "줄눈이 보이는 돌 블록 벽은 오를 수 있어요. 매끈한 대리석 벽은 못 올라가요.", PieceKind.Rook),
                            new LoadingTip("TIP · 점핑 발판", "벽 앞 발판을 밟으면 3m 위로 튀어 올라요. 벽타기보다 빠를 때가 많아요.", PieceKind.Pawn),
                            new LoadingTip("TIP · 미션 광장", "팀 문은 우리 팀 미니게임을 끝내야 열려요. 여럿이 같이 하면 훨씬 빨라요.", PieceKind.Bishop),
                            new LoadingTip("TIP · 방해", "상대 팀 스테이션에 들어가 밀고 막을 수 있어요. 대신 그동안 우리 팀 일은 못 해요.", PieceKind.Knight),
                            new LoadingTip("TIP · 회전 막대", "낮게 도는 막대는 점프로 넘어요. 턱 위에 서 있어도 맞아요.", PieceKind.Pawn),
                            new LoadingTip("TIP · 룩 벽", "C 구간의 룩 벽은 길 절반을 막고 좌우로 오가요. 열린 쪽을 보고 달리세요.", PieceKind.Rook),
                            new LoadingTip("TIP · 체크포인트", "떨어지면 마지막으로 지난 체크포인트에서 다시 시작해요. 낭떠러지 옆에선 침착하게.", PieceKind.Pawn),
                            new LoadingTip("TIP · 8랭크 탑", "결승 탑은 남쪽 벽을 9m 기어오르거나 양옆 경사로로 올라요. 경사로엔 해머가 돌아요.", PieceKind.Queen)
                        }
                    };
                case "swordfight":
                    return new LoadingContent
                    {
                        Title = title,
                        Kicker = versus + " · 떨어뜨리고 빼앗기",
                        Rule = "상대를 판 밖으로 떨어뜨리면 1점 · 먼저 10점 또는 4분",
                        Note = "시작할 때 기물을 고르고, 떨어지면 폰으로 돌아와요",
                        Tips = new[]
                        {
                            new LoadingTip("TIP · 빼앗기", "폰일 때 친 상대가 떨어지면 그 기물을 빼앗아요.", PieceKind.Pawn),
                            new LoadingTip("TIP · 스킬", "F는 기물 스킬(조준은 좌클릭으로 발동), E는 떨어질 때 쓰는 가장자리 스킬이에요.", PieceKind.Knight),
                            new LoadingTip("TIP · 판 축소", "2분부터 30초마다 유령 기물이 칸을 지워요. 빛나는 칸에서 피하세요.", PieceKind.Rook)
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
