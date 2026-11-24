using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // R743 (user 11-21: "A"): what the lobby's 순위 tab shows. The Steam side (Bootstrap/SteamLeaderboards, in the
    // Steam-gated assembly) fills it and sets Fetch; this assembly never sees Steam. Without Steam (offline, the
    // Editor's captures) every board stays Offline unless a capture previews one.
    // R745: a board also holds this player's neighbours (Around) and the further pages of its global list (Pages,
    // fetched a page at a time as the tab turns to them); Progress holds this player's Steam friends as the progress
    // board has them (the 친구 view's "6/20판" lines); Avatar gives a player's Steam profile picture.
    public static class Rankings
    {
        public enum Status { Offline, Loading, Ready, Failed }

        // One board as last fetched.
        public sealed class Board
        {
            public Status Status = Status.Offline;
            // Global ranks 1 to PageSize (the global list's first page).
            public readonly List<RankEntry> Top = new List<RankEntry>();
            // R745: the further pages as they were fetched: page n holds the ranks from n * PageSize + 1.
            public readonly Dictionary<int, List<RankEntry>> Pages = new Dictionary<int, List<RankEntry>>();
            // This player's Steam friends on the board and this player, best first.
            public readonly List<RankEntry> Friends = new List<RankEntry>();
            // R745: this player's line with the AroundSpan lines above and below it, by rank; empty while this player
            // is not on the board.
            public readonly List<RankEntry> Around = new List<RankEntry>();
            // This player's own line; null while they are not on the board.
            public RankEntry Mine;
            // Lines on the board.
            public int Total;
            public string Error = "";

            // The global list's page (0 the top); null while it has not been fetched.
            public List<RankEntry> Page(int page) => page <= 0 ? Top : Pages.TryGetValue(page, out var list) ? list : null;
            public int PageCount => Mathf.Max(1, (Total + PageSize - 1) / PageSize);
        }

        // Lines on a page of the tab's list.
        public const int PageSize = 10;
        // Lines above and below this player's own in the 내 주변 view.
        public const int AroundSpan = 3;

        static Board[] boards = Fresh();
        static List<RankProgress> progress = new List<RankProgress>();
        static bool previewing;

        // Something on a board changed (fetched, a page, a name or a picture arrived).
        public static event Action Changed;
        // The Steam side: fetch a board (it answers at once from one fetched a moment ago).
        public static Action<RankBoard> Fetch;
        // R745, the Steam side: fetch a further page of a board's global list (into the board's Pages).
        public static Action<RankBoard, int> FetchPage;
        // R745, the Steam side: a player's Steam profile picture; null while there is none (still on its way).
        public static Func<ulong, Texture2D> Avatar;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            boards = Fresh();
            progress = new List<RankProgress>();
            previewing = false;
            Changed = null;
            Fetch = null;
            FetchPage = null;
            Avatar = null;
        }

        // One for each of RankRules.Boards (R746: Pawn Rush and Sword Fight, none over both).
        static Board[] Fresh() => new[] { new Board(), new Board() };

        public static Board Get(RankBoard board) => boards[Index(board)];

        static int Index(RankBoard board) => Mathf.Clamp((int)board, 0, boards.Length - 1);

        // R745: this player's Steam friends who have played (and this player), as the progress board has them.
        public static IReadOnlyList<RankProgress> Progress => progress;

        // The tab shows a board: fetch it (Offline when there is no Steam).
        public static void Request(RankBoard board)
        {
            if (previewing) return;
            if (Fetch == null)
            {
                if (Get(board).Status != Status.Offline) Put(board, new Board());
                return;
            }
            Fetch(board);
        }

        // The tab turned to a further page of a board's global list.
        public static void RequestPage(RankBoard board, int page)
        {
            if (previewing || page <= 0) return;
            FetchPage?.Invoke(board, page);
        }

        // The Steam side's fetched board.
        public static void Put(RankBoard board, Board data)
        {
            if (previewing) return;
            boards[Index(board)] = data ?? new Board();
            Changed?.Invoke();
        }

        // The Steam side's fetched friends.
        public static void PutProgress(List<RankProgress> lines)
        {
            if (previewing) return;
            progress = lines ?? new List<RankProgress>();
            Changed?.Invoke();
        }

        // A board changed in place (a page, a name or a picture arrived).
        public static void Touch() => Changed?.Invoke();

        public static Texture2D Face(ulong id) => id != 0 && Avatar != null ? Avatar(id) : null;

        // Editor captures only (PawnRushEditor/LobbyTabsCapture): these boards, friends and pictures are shown and
        // nothing fetches.
        public static void Preview(Board pawnRush, Board swordFight, List<RankProgress> friends = null, Func<ulong, Texture2D> faces = null)
        {
            previewing = true;
            boards = new[] { pawnRush ?? new Board(), swordFight ?? new Board() };
            progress = friends ?? new List<RankProgress>();
            Avatar = faces;
            Changed?.Invoke();
        }

        // Today as the boards count days (UTC).
        public static int Today => RankRules.Day(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }
}
