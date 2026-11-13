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
        // R624: Sword Fight's pick moved here from the match scene (LoadingScreenView.SwordPick.cs).
        public bool SwordPick;

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
                        // R508: the match is a round of random sections ending in the war room (course 01's own tips -
                        // its plazas, pads, spin bars, rook walls and 8th-rank tower - went with it).
                        Rule = "팀원 <color=#FFC93D>" + need + "명</color>이 먼저 작전 회의실의 골에 서면 승리",
                        Note = "장애물 구간과 미니게임 구간은 판마다 무작위로 골라져요",
                        Tips = new[]
                        {
                            new LoadingTip("TIP · 부축", "두 걸음으로 넘어진 팀원에게 닿으면 바로 일으켜요. 둘 다 1.5초 동안 빨라져요.", PieceKind.Pawn),
                            new LoadingTip("TIP · 과반 승리", "1등 혼자로는 못 이겨요. 우리 팀 " + need + "명이 먼저 작전 회의실의 골에 서야 해요.", PieceKind.Rook),
                            new LoadingTip("TIP · 무작위 구간", "판마다 장애물 구간과 미니게임이 새로 골라져요. 한 판의 구성은 모두에게 똑같아요.", PieceKind.Bishop),
                            // 10-25 (승규 님: more tips useful to players), from course 01 v0.4
                            new LoadingTip("TIP · 벽타기", "줄눈이 보이는 돌 블록 벽은 오를 수 있어요. 매끈한 벽은 못 올라가요.", PieceKind.Rook),
                            new LoadingTip("TIP · 미니게임", "팀 문은 우리 팀 미니게임을 끝내야 열려요. 여럿이 같이 하면 훨씬 빨라요.", PieceKind.Bishop),
                            new LoadingTip("TIP · 방해", "상대 팀 스테이션에 들어가 밀고 막을 수 있어요. 대신 그동안 우리 팀 일은 못 해요.", PieceKind.Knight),
                            new LoadingTip("TIP · 체크포인트", "떨어지면 마지막으로 지난 체크포인트에서 다시 시작해요. 낭떠러지 옆에선 침착하게.", PieceKind.Pawn)
                        }
                    };
                case "swordfight":
                    return new LoadingContent
                    {
                        Title = title,
                        SwordPick = true,
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
