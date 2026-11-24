using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // R736 (user 11-21, the lobby's 전적 tab): this player's record of online matches, kept on this PC, one book for each
    // Steam account (NetworkRuntime sets Owner). The rules and the text are Core's (RecordBook, RecordBuild, RecordText).
    //
    // How a match gets in: the match's network link arms it once the match has started with this player seated in it
    // (never a spectator, never an offline scene), the mode writes its end (Finish: Pawn Rush when the race is decided,
    // Sword Fight on its result), and a link that goes away with the match still armed writes "나감" (Abandon). The mode
    // keeps Note up to date for that: the piece and the sides as they are now.
    //
    // R745 (user 11-21: a game killed from the task manager, or one that crashed, left no "나감" - only the link's
    // OnDestroy wrote it): the "나감" goes into the book, and onto the disk, the moment the match is armed (Hold), and
    // again shortly after the piece or the sides change (nothing is written on a clock: no save in the middle of play
    // that the match did not ask for); the match's end takes its place. So whatever stops the game mid-match leaves
    // the "나감" behind, and coming back into the same match and seeing its end still replaces it. While a match is
    // armed its held "나감" is not this player's result yet: the Steam side (SteamLeaderboards) uploads nothing then.
    // The link also says, when it arms, whether the match is a ranked one (RankRules.RankedMatch); the record carries
    // it.
    public static class MatchRecords
    {
        const string KeyPrefix = "ChessFight.Records.";
        // The held "나감" of a match under way is written again this soon after the piece or the sides changed.
        const float HoldAfterNote = 2f;

        static string owner = "0";
        static RecordBook book;
        static ulong armed;
        static string armedMode = "";
        static bool armedRanked;
        static bool written;
        static PieceKind notedPiece;
        static int notedOurs, notedTheirs;
        static MatchDetail log;
        static float holdAt;

        // R742: the armed match's detail, written by the modes as it goes (null when no match is armed). It goes into
        // the match's record (its end, or its "나감").
        public static MatchDetail Log => Armed ? log : null;

        // The book changed (a match was written, another account's book was loaded, Steam's count came back).
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            owner = "0";
            book = null;
            armed = 0;
            armedMode = "";
            armedRanked = false;
            written = false;
            log = null;
            holdAt = 0f;
            Changed = null;
        }

        // Whose book: the Steam account (its id as text).
        public static string Owner
        {
            get => owner;
            set
            {
                string next = string.IsNullOrEmpty(value) ? "0" : value;
                if (next == owner) return;
                owner = next;
                book = null;
                Changed?.Invoke();
            }
        }

        public static RecordBook Book => book ?? (book = Load());

        // Editor captures only (PawnRushEditor/LobbyTabsCapture): this book is shown instead of the saved one until the
        // owner changes. Nothing is saved.
        public static void Preview(RecordBook shown)
        {
            book = shown ?? new RecordBook();
            Changed?.Invoke();
        }

        static RecordBook Load()
        {
            try { return RecordBook.Decode(PlayerPrefs.GetString(KeyPrefix + owner, "")); }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new RecordBook();
            }
        }

        static void Save()
        {
            PlayerPrefs.SetString(KeyPrefix + owner, Book.Encode());
            PlayerPrefs.Save();
        }

        static double Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // A started online match (its room) this player plays in, of this mode; `ranked` whether it counts for the
        // 순위 boards (settled at its start, the same for a player who comes back). Called every frame by the links;
        // only a new match, or the same one again after its "나감" (the player came back), arms it anew. R745: arming
        // writes the match's "나감" at once (Hold), and the calls after it keep that up to date.
        public static void Arm(ulong match, string mode, bool ranked = false)
        {
            if (match == 0 || string.IsNullOrEmpty(mode)) return;
            if (armed == match && armedMode == mode && !written)
            {
                if (Time.realtimeSinceStartup >= holdAt) Hold();
                return;
            }
            if (armed == match && armedMode == mode && written && !LeftLast(match, mode)) return;
            armed = match;
            armedMode = mode;
            armedRanked = ranked;
            written = false;
            notedPiece = PieceKind.Pawn;
            notedOurs = notedTheirs = 0;
            log = new MatchDetail();
            Hold();
        }

        static bool LeftLast(ulong match, string mode)
        {
            foreach (var r in Book.Recent)
                if (r.Match == match && r.Mode == mode) return r.Result == RecordResult.Left;
            return false;
        }

        // A match is armed and not yet written: its "나감" is held in the book until its end (or the leaving) is.
        public static bool Armed => armed != 0 && !written;

        // The armed match counts for the 순위 boards.
        public static bool ArmedRanked => Armed && armedRanked;

        // The piece this player is now and the players a side, for a "나감".
        public static void Note(PieceKind piece, int ours, int theirs)
        {
            if (!Armed || (piece == notedPiece && ours == notedOurs && theirs == notedTheirs)) return;
            notedPiece = piece;
            notedOurs = ours;
            notedTheirs = theirs;
            // The held "나감" follows shortly (not at once: the sides fill up a player a frame as a match starts).
            holdAt = Mathf.Min(holdAt, Time.realtimeSinceStartup + HoldAfterNote);
        }

        static MatchRecord LeftNow()
        {
            var left = RecordBuild.Left(armedMode, notedPiece, notedOurs, notedTheirs, log);
            left.Match = armed;
            left.Ranked = armedRanked;
            left.When = Now;
            return left;
        }

        // R745: the armed match's "나감" as it stands now, into the book and onto the disk.
        static void Hold()
        {
            holdAt = float.PositiveInfinity;
            try
            {
                if (Book.Hold(LeftNow())) Save();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // The armed match's end, as its mode saw it: it takes the held "나감"'s place. Nothing when no match of that
        // mode is armed.
        public static void Finish(MatchRecord record)
        {
            if (!Armed || record == null || record.Mode != armedMode) return;
            written = true;
            record.Match = armed;
            record.Ranked = armedRanked;
            record.When = Now;
            try
            {
                if (!Book.Add(record)) return;
                Save();
                Debug.Log($"[ChessFight] 전적 기록: {RecordText.ModeName(record.Mode)} {RecordText.Badge(record.Result)} · {RecordText.Headline(record)}" +
                          (record.Ranked ? " · 순위 판" : ""));
                Changed?.Invoke();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // The armed match is going away before its end: this player left it ("나감", a loss). The held "나감" stays,
        // with the piece and the sides as they were last.
        public static void Abandon()
        {
            if (!Armed) return;
            written = true;
            try
            {
                var left = LeftNow();
                // Nothing to write for a match whose end the book already has (seen before the game was started again).
                if (Book.Hold(left))
                {
                    Save();
                    Debug.Log($"[ChessFight] 전적 기록: {RecordText.ModeName(left.Mode)} {RecordText.Badge(left.Result)} · {RecordText.Headline(left)}" +
                              (left.Ranked ? " · 순위 판" : ""));
                }
                // The held "나감" is this player's result from here on: the Steam side may send it.
                Changed?.Invoke();
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // R745: this account's count as Steam kept it (the progress board, RankProgress) into the book, mode by mode
        // where Steam kept more matches - a new PC, a reinstall. Never while a match is armed (its held "나감" is in
        // the book and not on Steam). Returns whether the book changed.
        public static bool Restore(RankProgress kept)
        {
            if (kept == null || Armed) return false;
            try
            {
                int before = Book.All.Games;
                if (!kept.RestoreInto(Book)) return false;
                Save();
                Debug.Log($"[ChessFight] 전적 판수 되살림: Steam에 보관된 {kept.Games}판 (이 PC {before}판 → {Book.All.Games}판)");
                Changed?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }
    }
}
