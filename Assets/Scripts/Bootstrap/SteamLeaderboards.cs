using System;
using System.Collections.Generic;
using ChessFight.Network;
using Steamworks;
using UnityEngine;

namespace ChessFight.Game
{
    // R743 (user 11-21: "A"; Docs/Architecture/UI.md §3-3, Docs/Project/DECISIONS.md U22): the 순위 tab's Steam
    // leaderboards, one for each RankBoard (RankRules.BoardName; R746: Pawn Rush's and Sword Fight's, none over both).
    // This PC puts its own line up from its record book whenever the book or the chosen name changes (a match written;
    // the first lobby after a start catches up), and fetches a board when the tab shows it: the global top, this
    // player's Steam friends, and this player's own line with its neighbours.
    //
    // App ID 480 is Valve's shared test app: the boards are found, or made, there by name (the ChessFight_ prefix);
    // the release App ID makes the same names. There is no server: each PC uploads its own line, Steam keeps the newest
    // one ("ForceUpdate": a rate goes down too), and every line read is checked against itself (RankDetails.Read).
    // Steam keeps the boards, so they go on working with nobody looking after them.
    //
    // R745 (user 11-21, the limits of R743):
    //  - a fourth board, RankRules.ProgressBoard, keeps every player's whole count (RankProgress). Before anything
    //    goes up this account's line on it comes down and goes into the book where Steam kept more (a new PC, a
    //    reinstall: MatchRecords.Restore) - so a small book never writes over a larger count. Nothing is uploaded
    //    until that read has answered;
    //  - nothing goes up while a match is under way: its held "나감" (MatchRecords.Armed) is not a result yet;
    //  - uploads go one at a time, a failed one waits longer each time (Steam takes one call at a time and ten in
    //    ten minutes);
    //  - the friends' lines on the progress board come down for the 친구 view ("6/20판" before they are on a board);
    //  - a board's global list is fetched a page at a time past the first ten, this player's line comes with the
    //    three above and below it, and a line's Steam profile picture is handed to the tab (Rankings.Avatar);
    //  - a board is fetched anew after 15 seconds (60 before).
    public sealed class SteamLeaderboards : IDisposable
    {
        // A board, a page or the friends fetched this recently are shown as they are.
        const float FreshFor = 15f;
        // A failed find, read or upload is tried again after this long; an upload that goes on failing waits twice as
        // long each time, up to RetryMax.
        const float RetryAfter = 30f, RetryMax = 300f;
        // The book is looked at for an upload at most this often while one is due.
        const float UploadEvery = 1f;
        const int PageKey = 100000;
        const string SentKey = "ChessFight.RankSent.";
        const string Unknown = "Steam 사용자";
        const string FindFailed = "Steam 순위표를 열지 못했어요. 잠시 뒤에 다시 볼게요.";
        const string FetchFailed = "Steam 순위표를 받지 못했어요. 잠시 뒤에 다시 볼게요.";

        readonly SteamSession session;
        readonly Action<RankBoard> fetch;
        readonly Action<RankBoard, int> fetchPage;
        readonly Func<ulong, Texture2D> avatar;
        // Steam's handle of each board by its name: the three rank boards and the progress board.
        readonly Dictionary<string, SteamLeaderboard_t> handles = new Dictionary<string, SteamLeaderboard_t>();
        readonly HashSet<string> finding = new HashSet<string>();
        readonly Dictionary<string, float> findAfter = new Dictionary<string, float>();
        // Boards the tab asked for while their handle was still being found.
        readonly HashSet<RankBoard> wanted = new HashSet<RankBoard>();
        readonly HashSet<RankBoard> fetching = new HashSet<RankBoard>();
        readonly Dictionary<RankBoard, float> fetchedAt = new Dictionary<RankBoard, float>();
        // Further pages of a board's global list (board * PageKey + page): being fetched, and when each last was.
        readonly HashSet<int> pagesFetching = new HashSet<int>();
        readonly Dictionary<int, float> pageFetchedAt = new Dictionary<int, float>();
        // Players whose Steam name, or whose picture, Steam is still fetching.
        readonly HashSet<ulong> nameless = new HashSet<ulong>(), faceless = new HashSet<ulong>();
        readonly List<IDisposable> calls = new List<IDisposable>();
        readonly Callback<PersonaStateChange_t> personas;
        readonly Callback<AvatarImageLoaded_t> pictures;
        readonly int[] buffer = new int[RankRules.MaxDetails];
        bool due = true, disposed, uploading;
        float uploadAfter, uploadRetry = RetryAfter;
        // This account's count on the progress board: read once, before anything goes up.
        bool restored, restoring;
        float restoreAfter;
        // The friends' lines on the progress board.
        bool friendsWanted, friendsFetching;
        float friendsFetchedAt = float.NegativeInfinity;

        public SteamLeaderboards(SteamSession session)
        {
            this.session = session;
            fetch = Fetch;
            fetchPage = FetchPage;
            avatar = Face;
            Rankings.Fetch = fetch;
            Rankings.FetchPage = fetchPage;
            Rankings.Avatar = avatar;
            MatchRecords.Changed += OnChanged;
            PlayerProfile.Changed += OnChanged;
            personas = Callback<PersonaStateChange_t>.Create(OnPersona);
            pictures = Callback<AvatarImageLoaded_t>.Create(OnPicture);
        }

        void OnChanged() => due = true;

        public void Tick()
        {
            if (disposed || !session.Online || session.Self == 0) return;
            float now = Time.realtimeSinceStartup;
            if (!restored) Restore(now);
            // Not under a match: its "나감" is held in the book until its end is written.
            else if (due && !MatchRecords.Armed && now >= uploadAfter) Upload();
            foreach (var board in RankRules.Boards)
                if (wanted.Contains(board) && handles.ContainsKey(RankRules.BoardName(board)))
                {
                    wanted.Remove(board);
                    Download(board);
                }
            if (friendsWanted && Handle(RankRules.ProgressBoard, out var progress))
            {
                friendsWanted = false;
                DownloadFriends(progress);
            }
        }

        // ---------- finding a board ----------

        // The board's handle; false while it is being found (one find at a time for a board, a failed one tried again
        // later).
        bool Handle(string name, out SteamLeaderboard_t handle)
        {
            if (handles.TryGetValue(name, out handle)) return true;
            if (finding.Contains(name) || (findAfter.TryGetValue(name, out float after) && Time.realtimeSinceStartup < after)) return false;
            finding.Add(name);
            var call = CallResult<LeaderboardFindResult_t>.Create(); calls.Add(call);
            call.Set(SteamUserStats.FindOrCreateLeaderboard(name, ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                                                            ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric), (c, failed) =>
            {
                calls.Remove(call); call.Dispose();
                if (disposed) return;
                finding.Remove(name);
                if (failed || c.m_bLeaderboardFound == 0)
                {
                    findAfter[name] = Time.realtimeSinceStartup + RetryAfter;
                    Debug.LogWarning("[ChessFight] Steam 순위표를 찾지 못함: " + name);
                    foreach (var board in RankRules.Boards)
                        if (RankRules.BoardName(board) == name && wanted.Remove(board)) Fail(board, FindFailed);
                    return;
                }
                handles[name] = c.m_hSteamLeaderboard;
            });
            return false;
        }

        // ---------- this account's count on Steam (R745) ----------

        // Steam's count of this account's matches comes down once and goes into the book where it is the larger. Asked
        // again until Steam answers: nothing goes up before, or a book that was reinstalled empty would write its
        // small numbers over Steam's.
        void Restore(float now)
        {
            if (restoring || now < restoreAfter || MatchRecords.Armed) return;
            if (!Handle(RankRules.ProgressBoard, out var handle)) return;
            restoring = true;
            var call = CallResult<LeaderboardScoresDownloaded_t>.Create(); calls.Add(call);
            call.Set(SteamUserStats.DownloadLeaderboardEntries(handle, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser, 0, 0), (c, failed) =>
            {
                calls.Remove(call); call.Dispose();
                if (disposed) return;
                restoring = false;
                if (failed)
                {
                    restoreAfter = Time.realtimeSinceStartup + RetryAfter;
                    Debug.LogWarning("[ChessFight] Steam에 보관된 판수를 받지 못함 · 잠시 뒤에 다시 받음(그때까지 올리지 않음)");
                    return;
                }
                RankProgress mine = null;
                for (int i = 0; i < c.m_cEntryCount; i++)
                {
                    Array.Clear(buffer, 0, buffer.Length);
                    if (!SteamUserStats.GetDownloadedLeaderboardEntry(c.m_hSteamLeaderboardEntries, i, out var raw, buffer, buffer.Length)) continue;
                    if (raw.m_steamIDUser.m_SteamID != session.Self) continue;
                    mine = RankProgress.Read(session.Self, raw.m_nScore, buffer, Mathf.Min(raw.m_cDetails, buffer.Length));
                }
                // A match that started meanwhile holds its "나감" in the book: read again once it is over.
                if (MatchRecords.Armed) { restoreAfter = Time.realtimeSinceStartup + UploadEvery; return; }
                if (mine != null) MatchRecords.Restore(mine);
                restored = true;
                due = true;
            });
        }

        // ---------- this player's lines ----------

        // The whole count first (every player, whatever their matches), then each board this player is on. One call at
        // a time (Steam's rule), each line only when it differs from what last went up (kept for each Steam account).
        void Upload()
        {
            uploadAfter = Time.realtimeSinceStartup + UploadEvery;
            if (uploading) return;
            var book = MatchRecords.Book;
            string name = PlayerProfile.Name;
            var progress = RankProgress.From(book);
            if (progress.Games > 0 && Send(RankRules.ProgressBoard, SentKey + session.Self + ".p", progress.Score, progress.Pack(name), c =>
                {
                    friendsFetchedAt = float.NegativeInfinity;
                    Debug.Log($"[ChessFight] 판수 보관(Steam): {progress.Games}판 · 순위 판 폰 러쉬 {progress.Rush.RankedGames} · 소드 파이트 {progress.Sword.RankedGames}");
                })) return;
            foreach (var each in RankRules.Boards)
            {
                var board = each;
                var line = RankUpload.From(book, board);
                if (line == null) continue;
                if (Send(RankRules.BoardName(board), SentKey + session.Self + "." + RankRules.BoardName(board), line.Score, RankDetails.Pack(line, name), c =>
                    {
                        // A board on show is fetched anew with the new line; any other at its next look.
                        fetchedAt.Remove(board);
                        if (Rankings.Get(board).Status == Rankings.Status.Ready && !fetching.Contains(board)) Download(board);
                        Debug.Log($"[ChessFight] 순위 올림: {RankRules.Title(board)} {RankText.Rate(line.Wins, line.Games)}% · {line.Games}판 → {c.m_nGlobalRankNew}위");
                    })) return;
            }
            due = false;
        }

        // One line up when it differs from what last went up. True while the line still has to go (its call is out, or
        // its board is still being found): the others wait for it. False when Steam already has it.
        bool Send(string board, string key, int score, int[] details, Action<LeaderboardScoreUploaded_t> done)
        {
            string sent = score + ":" + string.Join(",", details);
            if (PlayerPrefs.GetString(key, "") == sent) return false;
            if (!Handle(board, out var handle)) return true;
            uploading = true;
            var call = CallResult<LeaderboardScoreUploaded_t>.Create(); calls.Add(call);
            call.Set(SteamUserStats.UploadLeaderboardScore(handle, ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate,
                                                           score, details, details.Length), (c, failed) =>
            {
                calls.Remove(call); call.Dispose();
                if (disposed) return;
                uploading = false;
                if (failed || c.m_bSuccess == 0)
                {
                    Debug.LogWarning("[ChessFight] Steam 순위 올리기 실패: " + board);
                    due = true;
                    uploadAfter = Time.realtimeSinceStartup + uploadRetry;
                    uploadRetry = Mathf.Min(uploadRetry * 2f, RetryMax);
                    return;
                }
                uploadRetry = RetryAfter;
                PlayerPrefs.SetString(key, sent);
                PlayerPrefs.Save();
                // The next line, if one is waiting, at the next look.
                uploadAfter = Time.realtimeSinceStartup;
                done?.Invoke(c);
            });
            return true;
        }

        // ---------- fetching a board ----------

        void Fetch(RankBoard board)
        {
            if (disposed) return;
            if (!session.Online) { Rankings.Put(board, new Rankings.Board()); return; }
            FetchFriends();
            if (fetching.Contains(board) || wanted.Contains(board)) return;
            var shown = Rankings.Get(board);
            if (shown.Status == Rankings.Status.Ready && fetchedAt.TryGetValue(board, out float at) && Time.realtimeSinceStartup - at < FreshFor) return;
            // A board already shown stays up while it is fetched anew.
            if (shown.Status != Rankings.Status.Ready) Rankings.Put(board, new Rankings.Board { Status = Rankings.Status.Loading });
            string name = RankRules.BoardName(board);
            if (Handle(name, out _)) Download(board);
            else if (finding.Contains(name)) wanted.Add(board);
            else Fail(board, FindFailed);
        }

        sealed class Job
        {
            public readonly Rankings.Board Data = new Rankings.Board { Status = Rankings.Status.Ready };
            public int Pending = 3;
            public bool Failed;
        }

        // The top, the friends and this player's line with its neighbours, put up together once all three are in.
        void Download(RankBoard board)
        {
            if (!handles.TryGetValue(RankRules.BoardName(board), out var handle)) return;
            fetching.Add(board);
            var job = new Job();
            Request(board, handle, job, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, 1, Rankings.PageSize, job.Data.Top);
            Request(board, handle, job, ELeaderboardDataRequest.k_ELeaderboardDataRequestFriends, 0, 0, job.Data.Friends);
            Request(board, handle, job, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser, -Rankings.AroundSpan, Rankings.AroundSpan, job.Data.Around);
        }

        void Request(RankBoard board, SteamLeaderboard_t handle, Job job, ELeaderboardDataRequest kind, int from, int to, List<RankEntry> into)
        {
            var call = CallResult<LeaderboardScoresDownloaded_t>.Create(); calls.Add(call);
            call.Set(SteamUserStats.DownloadLeaderboardEntries(handle, kind, from, to), (c, failed) =>
            {
                calls.Remove(call); call.Dispose();
                if (disposed) return;
                if (failed) job.Failed = true;
                else Read(c, into);
                if (--job.Pending > 0) return;
                fetching.Remove(board);
                if (job.Failed) { Fail(board, FetchFailed); return; }
                // This player's own line is the one among its neighbours; without it (not on the board, or a line that
                // does not hold together) there are no neighbours to show.
                job.Data.Mine = job.Data.Around.Find(e => e.Self);
                if (job.Data.Mine == null) job.Data.Around.Clear();
                job.Data.Total = SteamUserStats.GetLeaderboardEntryCount(handle);
                // The pages the tab already turned to stay up until each is fetched anew.
                foreach (var page in Rankings.Get(board).Pages) job.Data.Pages[page.Key] = page.Value;
                fetchedAt[board] = Time.realtimeSinceStartup;
                Rankings.Put(board, job.Data);
            });
        }

        // R745: a page of the global list past the first (page 1 is the ranks 11 to 20), into the board on show.
        void FetchPage(RankBoard board, int page)
        {
            if (disposed || !session.Online || page <= 0) return;
            int key = (int)board * PageKey + page;
            var shown = Rankings.Get(board);
            if (pagesFetching.Contains(key) || shown.Status != Rankings.Status.Ready) return;
            if (shown.Pages.ContainsKey(page) && pageFetchedAt.TryGetValue(key, out float at) && Time.realtimeSinceStartup - at < FreshFor) return;
            if (!handles.TryGetValue(RankRules.BoardName(board), out var handle)) return;
            pagesFetching.Add(key);
            int from = page * Rankings.PageSize + 1;
            var call = CallResult<LeaderboardScoresDownloaded_t>.Create(); calls.Add(call);
            call.Set(SteamUserStats.DownloadLeaderboardEntries(handle, ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal, from, from + Rankings.PageSize - 1), (c, failed) =>
            {
                calls.Remove(call); call.Dispose();
                if (disposed) return;
                pagesFetching.Remove(key);
                var lines = new List<RankEntry>();
                // A page that failed shows empty and is asked for again at the next turn to it.
                if (failed) pageFetchedAt.Remove(key);
                else
                {
                    Read(c, lines);
                    pageFetchedAt[key] = Time.realtimeSinceStartup;
                }
                Rankings.Get(board).Pages[page] = lines;
                Rankings.Touch();
            });
        }

        // The lines of a download that hold together, by rank.
        void Read(LeaderboardScoresDownloaded_t c, List<RankEntry> into)
        {
            for (int i = 0; i < c.m_cEntryCount; i++)
            {
                Array.Clear(buffer, 0, buffer.Length);
                if (!SteamUserStats.GetDownloadedLeaderboardEntry(c.m_hSteamLeaderboardEntries, i, out var raw, buffer, buffer.Length)) continue;
                var entry = RankDetails.Read(raw.m_steamIDUser.m_SteamID, raw.m_nGlobalRank, raw.m_nScore, buffer, Mathf.Min(raw.m_cDetails, buffer.Length));
                if (entry == null) continue;
                entry.Self = entry.Id == session.Self;
                entry.Friend = !entry.Self && session.IsFriend(entry.Id);
                entry.Name = NameOf(entry.Id, entry.Name);
                into.Add(entry);
            }
            into.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        }

        void Fail(RankBoard board, string why)
        {
            var shown = Rankings.Get(board);
            // A board already shown stays; only an empty one says why.
            if (shown.Status == Rankings.Status.Ready) return;
            Rankings.Put(board, new Rankings.Board { Status = Rankings.Status.Failed, Error = why });
        }

        // ---------- the friends' counts (R745) ----------

        // This player's Steam friends as the progress board has them (and this player): the 친구 view shows the ones
        // who are not on a board yet with their matches so far. Fetched with the boards, not more often than they are.
        void FetchFriends()
        {
            if (friendsFetching || friendsWanted || Time.realtimeSinceStartup - friendsFetchedAt < FreshFor) return;
            if (Handle(RankRules.ProgressBoard, out var handle)) DownloadFriends(handle);
            else friendsWanted = true;
        }

        void DownloadFriends(SteamLeaderboard_t handle)
        {
            friendsFetching = true;
            var call = CallResult<LeaderboardScoresDownloaded_t>.Create(); calls.Add(call);
            call.Set(SteamUserStats.DownloadLeaderboardEntries(handle, ELeaderboardDataRequest.k_ELeaderboardDataRequestFriends, 0, 0), (c, failed) =>
            {
                calls.Remove(call); call.Dispose();
                if (disposed) return;
                friendsFetching = false;
                // The list fetched before stays when this one fails.
                if (failed) return;
                var lines = new List<RankProgress>();
                for (int i = 0; i < c.m_cEntryCount; i++)
                {
                    Array.Clear(buffer, 0, buffer.Length);
                    if (!SteamUserStats.GetDownloadedLeaderboardEntry(c.m_hSteamLeaderboardEntries, i, out var raw, buffer, buffer.Length)) continue;
                    var line = RankProgress.Read(raw.m_steamIDUser.m_SteamID, raw.m_nScore, buffer, Mathf.Min(raw.m_cDetails, buffer.Length));
                    if (line == null) continue;
                    line.Self = line.Id == session.Self;
                    line.Friend = !line.Self && session.IsFriend(line.Id);
                    line.Name = NameOf(line.Id, line.Name);
                    lines.Add(line);
                }
                friendsFetchedAt = Time.realtimeSinceStartup;
                Rankings.PutProgress(lines);
            });
        }

        // ---------- names and pictures ----------

        // The line's own (chosen) name when it carries one, else the Steam name; one Steam is still fetching shows as
        // "Steam 사용자" until it comes (OnPersona).
        string NameOf(ulong id, string chosen)
        {
            if (!string.IsNullOrEmpty(chosen)) return chosen;
            if (SteamFriends.RequestUserInformation(new CSteamID(id), true))
            {
                nameless.Add(id);
                return Unknown;
            }
            return SteamName(id);
        }

        static string SteamName(ulong id)
        {
            string name = SteamFriends.GetFriendPersonaName(new CSteamID(id));
            return string.IsNullOrEmpty(name) || name == "[unknown]" ? Unknown : name;
        }

        // R745: a player's Steam profile picture for the tab (SteamSession.Avatar keeps each once it is read); null
        // while Steam is still fetching it - the tab is told when it comes (OnPersona, OnPicture).
        Texture2D Face(ulong id)
        {
            if (disposed || id == 0 || BotIdentity.IsBot(id)) return null;
            var picture = session.Avatar(id);
            if (picture == null) faceless.Add(id); else faceless.Remove(id);
            return picture;
        }

        void OnPicture(AvatarImageLoaded_t c)
        {
            if (!disposed && faceless.Contains(c.m_steamID.m_SteamID)) Rankings.Touch();
        }

        void OnPersona(PersonaStateChange_t c)
        {
            if (disposed) return;
            bool changed = faceless.Contains(c.m_ulSteamID);
            if (nameless.Remove(c.m_ulSteamID))
            {
                string name = SteamName(c.m_ulSteamID);
                foreach (var board in RankRules.Boards)
                {
                    var shown = Rankings.Get(board);
                    var lists = new List<List<RankEntry>> { shown.Top, shown.Friends, shown.Around };
                    lists.AddRange(shown.Pages.Values);
                    foreach (var list in lists)
                        foreach (var e in list)
                            if (e.Id == c.m_ulSteamID && e.Name == Unknown) { e.Name = name; changed = true; }
                }
                foreach (var line in Rankings.Progress)
                    if (line.Id == c.m_ulSteamID && line.Name == Unknown) { line.Name = name; changed = true; }
            }
            if (changed) Rankings.Touch();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            MatchRecords.Changed -= OnChanged;
            PlayerProfile.Changed -= OnChanged;
            if (Rankings.Fetch == fetch) Rankings.Fetch = null;
            if (Rankings.FetchPage == fetchPage) Rankings.FetchPage = null;
            if (Rankings.Avatar == avatar) Rankings.Avatar = null;
            personas?.Dispose();
            pictures?.Dispose();
            foreach (var call in calls) call.Dispose();
            calls.Clear();
        }
    }
}
