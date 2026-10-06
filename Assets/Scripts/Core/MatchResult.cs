using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessFight.Network
{
    // One player's line on the result screen.
    public sealed class ResultPlayer
    {
        public string Name = "";
        // 0 white, 1 black.
        public int Team;
        public PieceKind Piece;
        // Match clock when they crossed the finish line; negative when they never did.
        public double FinishSeconds = -1;
        // How far they still were from the line when the match ended (non-finishers only).
        public float MetersLeft = -1;
        public bool Self, Host;

        public bool Finished => FinishSeconds >= 0;
    }

    // One finisher in crossing order and what that crossing meant for the match.
    public readonly struct FinishLine
    {
        public readonly ResultPlayer Player;
        // 1-based, over both teams.
        public readonly int Place;
        // 1-based, within the player's team.
        public readonly int TeamPlace;
        // One of the winning team's first `Needed` finishers: the heads that won it.
        public readonly bool CountsToWin;
        // The crossing that decided the match (the winning team's `Needed`-th).
        public readonly bool Clinched;

        public FinishLine(ResultPlayer player, int place, int teamPlace, bool countsToWin, bool clinched)
        {
            Player = player;
            Place = place;
            TeamPlace = teamPlace;
            CountsToWin = countsToWin;
            Clinched = clinched;
        }
    }

    // The end of a Pawn Rush match as the last scene shows it (Docs/Architecture/UI.md
    // "결과 화면"). Plain data with no Unity in it, so the ordering rules are tested on
    // their own. Nothing fills it from a live match yet: the course does not network
    // its finish line (the scene shows Example until then).
    public sealed class MatchResult
    {
        public string Mode = "폰 러쉬";
        // 0 white, 1 black, -1 a draw.
        public int WinningTeam = -1;
        // Finishers a team needs to win: more than half of it.
        public int Needed = 4;
        public double MatchSeconds;
        public readonly List<ResultPlayer> Players = new List<ResultPlayer>();

        // More than half the team: 2 of 2, 2 of 3, 3 of 4, 3 of 5, 4 of 6 (as the loading screen says).
        public static int MajorityOf(int teamSize) => Math.Max(1, teamSize) / 2 + 1;

        public int FinishedCount(int team) => Players.Count(p => p.Team == team && p.Finished);

        // Toward the team goal: never more than Needed.
        public int GoalCount(int team) => Math.Min(Needed, FinishedCount(team));

        public List<FinishLine> FinishOrder()
        {
            var order = Players.Where(p => p.Finished)
                               .OrderBy(p => p.FinishSeconds).ThenBy(p => p.Team).ThenBy(p => p.Name, StringComparer.Ordinal)
                               .ToList();
            var teamPlace = new int[2];
            var lines = new List<FinishLine>(order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                var player = order[i];
                int place = ++teamPlace[player.Team == 1 ? 1 : 0];
                bool counts = player.Team == WinningTeam && place <= Needed;
                lines.Add(new FinishLine(player, i + 1, place, counts, counts && place == Needed));
            }
            return lines;
        }

        // Who never reached the line, nearest first; an unknown distance goes last.
        public List<ResultPlayer> NotFinished(int team) =>
            Players.Where(p => p.Team == team && !p.Finished)
                   .OrderBy(p => p.MetersLeft < 0 ? float.MaxValue : p.MetersLeft).ThenBy(p => p.Name, StringComparer.Ordinal)
                   .ToList();

        // "4:12".
        public static string Clock(double seconds)
        {
            if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return "-";
            long whole = (long)Math.Floor(seconds + 1e-6);
            return whole / 60 + ":" + (whole % 60).ToString("00");
        }

        // "12m", rounded up so a player 0.2 m short never reads as having arrived.
        public static string Meters(float meters) =>
            meters < 0 || float.IsNaN(meters) || float.IsInfinity(meters) ? "-" : (int)Math.Ceiling(meters - 1e-4) + "m";

        // The match the last scene shows until a real one is passed in: the one on
        // the design samples (white wins 4 to 2 at 4:12). `selfTeam` picks whose
        // screen it is: 폰돌이 on white, 갬빗 on black.
        public static MatchResult Example(int selfTeam)
        {
            var result = new MatchResult { WinningTeam = 0, Needed = MajorityOf(6), MatchSeconds = 252 };
            // A delegate rather than a local function: mcs, which runs the Core tests on Linux
            // (Tools/run-tests-linux.sh), cannot parse local functions.
            Action<string, int, PieceKind, double, float, bool> add = (name, team, piece, finish, left, host) =>
                result.Players.Add(new ResultPlayer
                {
                    Name = name, Team = team, Piece = piece, FinishSeconds = finish, MetersLeft = left, Host = host,
                    Self = (selfTeam == 1 ? name == "갬빗" : name == "폰돌이")
                });
            add("폰돌이", 0, PieceKind.Pawn, 232, -1, false);
            add("퀸사이드", 0, PieceKind.Queen, 221, -1, false);
            add("룩앤롤", 0, PieceKind.Rook, -1, 12, false);
            add("앙파상", 0, PieceKind.Bishop, -1, 27, false);
            add("블리츠", 0, PieceKind.Knight, 243, -1, false);
            add("오프닝", 0, PieceKind.King, 252, -1, false);
            add("캐슬링", 1, PieceKind.King, -1, 18, true);
            add("갬빗", 1, PieceKind.Knight, 248, -1, false);
            add("프로모션", 1, PieceKind.Queen, 227, -1, false);
            add("스테일메이트", 1, PieceKind.Rook, -1, 9, false);
            add("포크", 1, PieceKind.Bishop, -1, 33, false);
            add("스큐어", 1, PieceKind.Pawn, -1, 21, false);
            return result;
        }
    }
}
