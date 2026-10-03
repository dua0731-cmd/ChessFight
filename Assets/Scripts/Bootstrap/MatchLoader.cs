using System;
using System.Linq;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    // Lobby -> match scene behind the loading screen, then one shared start
    // (Docs/Architecture/UI.md "로딩 화면", MatchStart).
    //
    // Since R63 (2026-10-03) the same screen already covers a public match's
    // matchmaking ("상대 팀 찾는 중" is no longer a lobby banner): players walk onto
    // its stage as they join, "매칭 취소" or Esc calls the search off, and when
    // the match starts that screen carries straight on into the load. Private
    // rooms keep the lobby's room panel (room number, bots, the host's start).
    //
    // The old LoadScene froze every PC for the whole load, 5 to 10 seconds. Now
    // the scene loads in the background under LoadingScreenView; only switching
    // it on still freezes a frame, and the screen covers that too. Then the PC
    // waits until its frames run smoothly, reports ready ("load" = 100), and the
    // host announces one start time on the shared Steam clock ("go") once all
    // are ready or the slowest has had MatchStart.MaxWait. Every PC lifts its
    // screen at that moment, and local input stays blocked until then.
    sealed class MatchLoader
    {
        // The screen is drawn at least this long before the scene may switch on,
        // since switching on freezes the frame.
        const float MinimumShow = .4f;

        enum Phase { Idle, Matching, Loading, Opening, WarmingUp, Waiting }

        readonly NetworkRuntime runtime;
        readonly Func<double> clock;
        Phase phase;
        AsyncOperation load;
        string scene = "";
        LoadingScreenView view;
        WarmupMeter warmup;
        ThreadPriority loadingPriority;
        bool skipFrame;
        float shownAt, readyAt = -1, hostSince = -1, matchingSince;

        public MatchLoader(NetworkRuntime runtime, Func<double> clock)
        {
            this.runtime = runtime;
            this.clock = clock;
        }

        // Local input waits for the start, like everyone else's.
        public bool Blocking => phase >= Phase.Loading;
        // The screen is up (matching or loading): the chat hides under it.
        public bool Covering => phase != Phase.Idle;

        // False when the scene cannot be loaded at all (missing from the build list).
        public bool Begin(string sceneName, GameModeInfo mode)
        {
            // The matchmaking screen carries on as the loading screen.
            var kept = phase == Phase.Matching ? view : null;
            if (kept != null) { view = null; phase = Phase.Idle; }
            Cancel();
            var session = runtime.Session;
            scene = sceneName;
            int white = session.Roster.Values.Count(p => p.Team == 0), black = session.Roster.Values.Count(p => p.Team != 0);
            var content = LoadingContent.For(mode, Math.Max(white, black));
            // The start time is already set: this player takes the empty seat of
            // someone who left (Backfill) and goes in as soon as it has loaded.
            if (session.StartAt > 0) content.Kicker += " · 경기 중 합류";

            if (kept != null)
            {
                view = kept;
                view.SetContent(content);
            }
            else view = Show(content);

            session.ReportLoading(0);
            // Loading in the background is throttled by default; nothing else
            // needs the frame while the screen is up.
            loadingPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            load = SceneManager.LoadSceneAsync(sceneName);
            phase = Phase.Loading;
            shownAt = Time.unscaledTime;
            readyAt = hostSince = -1;
            if (load == null)
            {
                Debug.LogError("[ChessFight] 경기 씬을 불러오지 못했습니다: " + sceneName + " (File > Build Profiles의 씬 목록 확인)");
                Cancel();
                return false;
            }
            load.allowSceneActivation = false;
            return true;
        }

        // A screen that fails to build must not stop the match: go on without it.
        LoadingScreenView Show(LoadingContent content)
        {
            var host = new GameObject("Loading Screen");
            host.transform.SetParent(runtime.transform, false);
            try
            {
                var screen = host.AddComponent<LoadingScreenView>();
                screen.Build(content);
                screen.CancelRequested += () => runtime.Session.Cancel();
                screen.AddBotRequested += () => runtime.Session.AddRoomBot();
                return screen;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                UnityEngine.Object.Destroy(host);
                return null;
            }
        }

        // A public search, or the public room it found, from the lobby: the
        // entrance screen instead of a lobby banner. Not for private rooms (their
        // host needs the lobby's room tools), nor for a member back early from a
        // match waiting for the rest of the party.
        static bool PublicMatching(SteamSession session)
        {
            if (!session.Online || session.PrivateRoom || session.WaitingForParty || session.Started) return false;
            if (SceneManager.GetActiveScene().name != SceneNames.Lobby) return false;
            return session.Searching || session.Match != 0 || Following(session);
        }

        static bool Following(SteamSession session) => session.Party != 0 && session.Busy && !session.IsLeader && session.Match == 0;

        void Matchmaking()
        {
            var session = runtime.Session;
            bool wanted = PublicMatching(session);
            if (phase == Phase.Idle)
            {
                if (!wanted) return;
                var mode = session.MatchMode ?? session.PartyMode;
                // Built once per search: if it fails, the busy card under it still cancels.
                view = Show(LoadingContent.For(mode, TeamReservations.TeamSize));
                phase = Phase.Matching;
                matchingSince = Time.unscaledTime;
            }
            else if (!wanted) { Finish(); return; }
            if (view == null) return;

            var state = view.State;
            state.Matching = true;
            state.Expected = TeamReservations.TeamSize * 2;
            state.CanCancel = session.Busy && !session.WaitingForParty;
            // Development builds let public rooms take bots (M6); the leader sees
            // the button from the start, usable once it hosts the room it made.
            state.ShowAddBot = session.AllowPublicBots && (session.Match == 0 ? session.IsLeader : session.IsHost);
            state.CanAddBot = session.CanAddRoomBot;
            int seconds = Mathf.FloorToInt(Time.unscaledTime - matchingSince);
            state.Clock = $"{seconds / 60:00}:{seconds % 60:00}";
            state.Step = session.JoiningLive ? "진행 중인 경기에 들어가는 중" : Following(session) ? "파티장이 방을 찾는 중" : "상대 팀 찾는 중";
            state.Clear();
            if (session.Match != 0 && session.Roster.Count > 0)
            {
                foreach (var p in session.Roster.Values.OrderBy(p => p.Team).ThenBy(p => p.Slot))
                    Add(state, session, p.Id, p.Team, false);
            }
            else
            {
                // Still searching: the party, which always plays on one team.
                foreach (ulong id in session.PartyMembers.OrderBy(id => id == session.Self ? 0 : 1)) Add(state, session, id, 0, false);
                for (int i = 1; i <= session.PartyBots; i++)
                {
                    state.Teams.Add(0); state.Joined.Add(true); state.Ready.Add(false);
                    state.Names.Add("BOT " + i); state.Bots.Add(true);
                }
                if (session.Party == 0) Add(state, session, session.Self, 0, false);
            }
        }

        static void Add(LoadingState state, SteamSession session, ulong id, int team, bool ready)
        {
            if (id == session.Self) state.Me = state.Teams.Count;
            state.Teams.Add(team);
            state.Joined.Add(true);
            state.Ready.Add(ready);
            state.Names.Add(session.Name(id));
            state.Bots.Add(BotIdentity.IsBot(id));
        }

        // From NetworkRuntime.OnSceneLoaded, for every scene.
        public void SceneLoaded(string name)
        {
            if (phase != Phase.Opening || name != scene) return;
            Application.backgroundLoadingPriority = loadingPriority;
            warmup = new WarmupMeter();
            // The frame the scene switched on is the load itself, not a warm-up frame.
            skipFrame = true;
            phase = Phase.WarmingUp;
        }

        // Takes the screen down at once: the match ended, or the runtime is going.
        public void Cancel()
        {
            if (phase == Phase.Idle && view == null) return;
            if (phase == Phase.Loading || phase == Phase.Opening)
            {
                Application.backgroundLoadingPriority = loadingPriority;
                // A load held back from switching on would hold up every later
                // scene load, so let it finish.
                if (load != null) load.allowSceneActivation = true;
            }
            Finish();
        }

        public void Update()
        {
            if (phase <= Phase.Matching) { Matchmaking(); return; }
            var session = runtime.Session;
            float now = Time.unscaledTime;
            int percent = 0;
            string step = "";
            switch (phase)
            {
                case Phase.Loading:
                    percent = LoadProgress.Percent(LoadStage.Loading, load.progress);
                    step = "코스를 불러오는 중";
                    // A match that ended meanwhile still has to finish this load (Unity
                    // cannot abandon one); NetworkRuntime then heads back to the lobby.
                    if ((load.progress >= .9f && now - shownAt >= MinimumShow) || session.Match == 0)
                    {
                        load.allowSceneActivation = true;
                        phase = Phase.Opening;
                    }
                    break;
                case Phase.Opening:
                    percent = LoadProgress.Percent(LoadStage.Opening, 0);
                    step = "코스를 여는 중";
                    break;
                case Phase.WarmingUp:
                    if (session.Match == 0) { Finish(); return; }
                    if (skipFrame) skipFrame = false;
                    else warmup.Add(Time.unscaledDeltaTime * 1000.0);
                    percent = LoadProgress.Percent(LoadStage.WarmingUp, warmup.Fraction);
                    step = "캐릭터를 준비하는 중";
                    if (warmup.Done) { phase = Phase.Waiting; readyAt = now; percent = MatchStart.Ready; }
                    break;
                case Phase.Waiting:
                    percent = MatchStart.Ready;
                    step = Wait(session, now);
                    break;
            }
            if (phase == Phase.Idle) return;
            session.ReportLoading(percent);
            if (view == null) return;
            view.State.Percent = percent;
            view.State.Step = step;
            var state = view.State;
            state.Matching = false;
            state.Expected = 0;
            state.Clear();
            foreach (var p in session.Roster.Values.OrderBy(p => p.Team).ThenBy(p => p.Slot))
                Add(state, session, p.Id, p.Team, session.LoadPercent(p.Id) >= MatchStart.Ready);
        }

        // Ready: wait for the host's start time, and as the host, pick it.
        string Wait(SteamSession session, float now)
        {
            // Nobody to wait for without a live match.
            if (!session.Online || session.Match == 0) { Finish(); return ""; }
            // A player who takes the host role while waiting counts its wait from then.
            if (session.IsHost) { if (hostSince < 0) hostSince = now; }
            else hostSince = -1;

            double start = session.StartAt;
            if (start == 0 && session.CanAnnounceStart &&
                MatchStart.ShouldStart(session.Roster.Keys.Select(session.LoadPercent), now - Mathf.Max(readyAt, hostSince)))
            {
                session.AnnounceStart(clock() + MatchStart.Lead);
                start = session.StartAt;
            }
            // A late player finds the start already past and goes at once.
            if (start > 0 && clock() >= start) { Finish(); return ""; }
            if (start == 0 && now - readyAt >= MatchStart.ClientGiveUp) { Finish(); return ""; }

            bool everyone = session.Roster.Keys.All(id => session.LoadPercent(id) >= MatchStart.Ready);
            if (start == 0) return "다른 플레이어를 기다리는 중";
            return everyone ? "모두 준비 완료 · 곧 출발해요" : "곧 출발해요 · 늦는 플레이어는 준비되는 대로 따라와요";
        }

        void Finish()
        {
            phase = Phase.Idle;
            load = null;
            if (view != null) view.Hide();
            view = null;
        }
    }
}
