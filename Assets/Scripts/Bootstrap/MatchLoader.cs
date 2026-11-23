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
    // R502: the session picks that time (SteamSession.AnnounceStartWhenDue), not
    // this screen, so a host that takes over after its screen has gone still does.
    sealed class MatchLoader
    {
        // The screen is drawn at least this long before the scene may switch on,
        // since switching on freezes the frame.
        const float MinimumShow = .4f;
        // R732: the longest the warm-up waits on what the scene draws behind the screen (LoadWarmup) - a guard; the
        // round's own tour gives up sooner (RoundPrewarm).
        const float WarmupHoldLimit = 40f;
        // R548: Pawn Rush's course reveal seen to its end already (a player coming back into a running match), and how
        // long before this PC's own start the host's start time may lie before it counts as left from an earlier round.
        const float RevealSkipped = 1e5f;
        const double StaleBegun = 30;

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
        float shownAt, readyAt = -1, matchingSince, gateSince = -1;
        // R693: the screen of a match found by random matching waits for the party's walk out of the lobby's gate
        // (the scene loads meanwhile): what it will show once they are through.
        LoadingContent walkContent;
        // R548: this PC's own start (the reveal's fallback clock) and whether the reveal is skipped (RevealTime).
        double revealFrom = -1;
        bool revealSkip;
        // R596: when this load reached each step (realtime, -1 not yet), for the one line it logs once warmed up.
        float loadedAt = -1, openedAt = -1, warmFrom = -1;

        public MatchLoader(NetworkRuntime runtime, Func<double> clock)
        {
            this.runtime = runtime;
            this.clock = clock;
        }

        // Local input waits for the start, like everyone else's.
        public bool Blocking => phase >= Phase.Loading;
        // The screen is up (matching or loading): the chat hides under it.
        public bool Covering => phase != Phase.Idle;
        // R698: the screen is up as a room's waiting room (RoomScreen): the chat shows over it.
        public bool RoomUp => phase == Phase.Matching && view != null && view.State.Room;

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
            // The start time is already set: this player is coming back into the match
            // it left (R538) and goes in as soon as it has loaded.
            if (session.StartAt > 0) content.Kicker += " · 경기로 복귀";

            // R693 (the user 2026-11-17: random matching only counts the time on the lobby screen, "like 배틀그라운드"):
            // the lobby stood through the search, so the party walks out of its gate now, the lobby saying "상대를
            // 찾았습니다", and this screen comes up when they are through (Update). The scene loads meanwhile and
            // every PC's screen runs on the host's start time, so the walk only eats the screen's first moments.
            walkContent = null;
            gateSince = -1;
            var stage = LobbyStage.Current;
            bool walk = kept == null && !session.PrivateRoom && session.StartAt <= 0 && stage != null && stage.HasGate
                        && SceneManager.GetActiveScene().name == SceneNames.Lobby;
            if (kept != null)
            {
                view = kept;
                view.SetContent(content);
            }
            else if (walk)
            {
                walkContent = content;
                gateSince = Time.unscaledTime;
                stage.EnterGate();
            }
            else view = Show(content);

            // R548: Pawn Rush's course reveal plays from the moment the host started the match (RevealTime), the same
            // on every PC, and shows this round's draw, worked out from the match's number like the round itself
            // (PawnRushRound.BuildForMatch). A player coming back into a running match (R538) sees it finished.
            revealSkip = session.StartAt > 0;
            revealFrom = clock();
            Prime(content);

            session.ReportLoading(0);
            // Loading in the background is throttled by default; nothing else
            // needs the frame while the screen is up.
            loadingPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            load = SceneManager.LoadSceneAsync(sceneName);
            phase = Phase.Loading;
            shownAt = Time.unscaledTime;
            readyAt = loadedAt = openedAt = warmFrom = -1;
            if (load == null)
            {
                Debug.LogError("[ChessFight] 경기 씬을 불러오지 못했습니다: " + sceneName + " (File > Build Profiles의 씬 목록 확인)");
                Cancel();
                return false;
            }
            load.allowSceneActivation = false;
            return true;
        }

        // What a screen just put up shows of this match from its first frame.
        void Prime(LoadingContent content)
        {
            if (view == null) return;
            var session = runtime.Session;
            view.State.CourseKnown = session.Match != 0;
            view.State.CourseSeed = PawnRushSections.MatchSeed(session.Match);
            view.State.Reveal = RevealTime(session);
            // R624: Sword Fight's found / pick / team / clash screens (not for a player coming back into a running match)
            view.State.SwordPick = content.SwordPick && !revealSkip;
        }

        // R693: the party is through the lobby's gate (or took too long, or the lobby is gone): the screen comes up.
        void EndWalk()
        {
            if (walkContent == null) return;
            var stage = LobbyStage.Current;
            bool lobby = SceneManager.GetActiveScene().name == SceneNames.Lobby;
            if (lobby && stage != null && !stage.GateEntered && Time.unscaledTime - gateSince < LobbyStage.MaxWalkSeconds) return;
            var content = walkContent;
            walkContent = null;
            gateSince = -1;
            view = Show(content);
            Prime(content);
            shownAt = Time.unscaledTime;
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
                screen.StartRequested += () => runtime.Session.StartGame();
                // R533: the manager's buttons on a player's card, with that player's id (LoadingState.Keys).
                screen.GiveRequested += id => runtime.Session.GiveManager(id);
                screen.KickRequested += id => runtime.Session.Kick(id);
                // R694: a room a player made - an empty seat clicked, the manager's "옮기기", the room's number, a side's bots.
                screen.SeatRequested += seat => runtime.Session.TakeSeat(seat);
                screen.MoveRequested += id => runtime.Session.MovePlayer(id);
                screen.CopyRoomRequested += () => GUIUtility.systemCopyBuffer = LobbyCode.Plain(runtime.Session.Match);
                // R703: the manager picked a flow rule.
                screen.RulesRequested += packed => runtime.Session.SetRules(packed);
                screen.TeamBotRequested += (team, more) => { if (more > 0) runtime.Session.AddTestBot(team); else runtime.Session.RemoveTestBot(team); };
                // R624: a Sword Fight piece card confirmed on this screen
                screen.SwordPickRequested += piece => runtime.Session.PickSword(piece);
                return screen;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                UnityEngine.Object.Destroy(host);
                return null;
            }
        }

        // R693: random matching no longer puts the entrance screen over the lobby while it searches - the lobby's
        // own card counts the time (LobbyBootstrap.BuildModel). The screen below (the players' cards, the manager's
        // start, hand over and kick) is kept for the rooms players create, which get it in the next stage.
        static readonly bool EntranceWhileSearching = false;

        // A public search, or the public room it found, from the lobby: the
        // entrance screen instead of a lobby banner. Not for private rooms (their
        // host needs the lobby's room tools), nor for a member back early from a
        // match waiting for the rest of the party.
        static bool PublicMatching(SteamSession session)
        {
            if (!EntranceWhileSearching) return false;
            if (!session.Online || session.PrivateRoom || session.WaitingForParty || session.Started) return false;
            if (SceneManager.GetActiveScene().name != SceneNames.Lobby) return false;
            return session.Searching || session.Match != 0 || Following(session);
        }

        static bool Following(SteamSession session) => session.Party != 0 && session.Busy && !session.IsLeader && session.Match == 0;

        // R694 (the user 2026-11-17, "방 생성": "현재 매칭 화면처럼 방이 생성되어"): the waiting room of a room a player
        // made is this screen in its room layout (LoadingScreenView.Room.cs) - the two sides, the spectators' seats,
        // the manager's buttons. From the lobby, until the room starts.
        static bool RoomScreen(SteamSession session) =>
            session.Online && session.PrivateRoom && session.Match != 0 && !session.Started && !session.WaitingForParty
            && SceneManager.GetActiveScene().name == SceneNames.Lobby;

        // A room's number in groups, as the lobby shows it (LobbyCode, R737).
        static string Spaced(ulong id) => LobbyCode.Format(id);

        void Matchmaking()
        {
            var session = runtime.Session;
            bool room = RoomScreen(session);
            bool wanted = room || PublicMatching(session);
            if (phase == Phase.Idle)
            {
                // The lobby castle's walk in (LobbyStage.EnterGate): the party goes
                // through the great gate first, then the matchmaking screen comes up.
                // Only the screen waits; the search is already running.
                var stage = LobbyStage.Current;
                if (!wanted)
                {
                    if (gateSince >= 0) { gateSince = -1; if (stage != null) stage.LeaveGate(); }
                    return;
                }
                // R694: a room comes up at once (nobody walks anywhere to sit in a room).
                if (!room && stage != null && stage.HasGate)
                {
                    if (gateSince < 0) { gateSince = Time.unscaledTime; stage.EnterGate(); }
                    if (!stage.GateEntered && Time.unscaledTime - gateSince < LobbyStage.MaxWalkSeconds) return;
                }
                gateSince = -1;
                var mode = session.MatchMode ?? session.PartyMode;
                // Built once per search: if it fails, the busy card under it still cancels.
                view = Show(LoadingContent.For(mode, TeamReservations.TeamSize));
                phase = Phase.Matching;
                matchingSince = Time.unscaledTime;
            }
            else if (!wanted)
            {
                Finish();
                // Back in the lobby: the party stands on the stage again, the gate shuts.
                if (LobbyStage.Current != null) LobbyStage.Current.LeaveGate();
                return;
            }
            if (view == null) return;

            var state = view.State;
            state.Matching = true;
            // R548/R550/R551: no course yet: the players walk onto the study's chessboard until the host starts the match,
            // then the course reveal (LoadingScreenView.Reveal.cs) covers it.
            state.CourseKnown = false;
            state.Reveal = -1f;
            state.Expected = TeamReservations.TeamSize * 2;
            state.CanCancel = session.Busy && !session.WaitingForParty;
            // Development builds let public rooms take bots (M6); the leader sees
            // the button from the start, usable once it manages the room it made (R533: the manager, not the host).
            state.ShowAddBot = session.AllowPublicBots && (session.Match == 0 ? session.IsLeader : session.IsManager);
            state.CanAddBot = session.CanAddRoomBot;
            // R148: the room's manager may start short of twelve (3+ a side, even sides). R533: the manager is whoever
            // made the room (or was handed the role), whichever PC hosts it.
            state.ShowStart = session.EarlyStartMode && session.IsManager && !session.Started;
            state.CanStart = session.CanStartGame;
            int white = session.TeamCount(0), black = session.TeamCount(1), min = session.MinTeamToStart;
            bool ready = white >= min && white == black && session.SeatingJoiners == 0;
            // R538: nor for a player on its way back into a running match (the room's numbers there are who is away).
            state.StartNote = !session.EarlyStartMode || session.Started || session.Match == 0 || session.JoiningLive ? ""
                // R644: the room's owner is a PC that has gone silent; nothing starts until Steam takes it out of the room.
                : session.RoomHeld ? Loc.T(SteamSession.RoomHeldMessage)
                : state.CanStart ? Loc.F("{0} 대 {1} · 지금 시작할 수 있어요 (12명이 차면 자동으로 시작)", white, black)
                : !session.IsManager && ready ? Loc.F("{0} 대 {1} · 방장이 시작할 수 있어요", white, black)
                // R525: the start waits (up to SteamSession.JoinGrace) for players already in the room to get their seats.
                : session.SeatingJoiners > 0 ? Loc.F("{0} 대 {1} · 방에 들어온 {2}명이 자리를 받는 중이에요", white, black, session.SeatingJoiners)
                : white < min || black < min ? Loc.F("{0} 대 {1} · 팀마다 {2}명 이상이면 시작할 수 있어요", white, black, min)
                : white != black ? Loc.F("{0} 대 {1} · 두 팀 인원이 같아야 시작할 수 있어요", white, black)
                : Loc.F("{0} 대 {1} · 모두 입장하면 시작할 수 있어요", white, black);
            int seconds = Mathf.FloorToInt(Time.unscaledTime - matchingSince);
            state.Clock = $"{seconds / 60:00}:{seconds % 60:00}";
            // R294: a new host in the waiting room says so for a few seconds (who, and why: the best PC).
            state.Step = session.NoteShowing ? session.Note
                       : session.RoomHeld ? Loc.T("끊긴 PC를 정리하는 중")
                       : session.JoiningLive ? Loc.T("나갔던 경기로 돌아가는 중") : Following(session) ? Loc.T("파티장이 방을 찾는 중") : Loc.T("상대 팀 찾는 중");
            state.Clear();
            state.Room = room;
            if (room) FillRoom(state, session);
            if (session.Match != 0 && session.Roster.Count > 0)
            {
                foreach (var p in session.Roster.Values.OrderBy(p => p.Team).ThenBy(p => p.Slot))
                {
                    Add(state, session, p.Id, p.Team, false);
                    state.CanMove.Add(room && session.CanMovePlayer(p.Id));
                    // R533: what this PC's player may do as the room's manager, and (development builds) each PC's score.
                    state.CanGive.Add(!session.Started && session.CanGiveManager(p.Id));
                    state.CanKick.Add(session.CanKick(p.Id));
                    state.Scores.Add(Debug.isDebugBuild ? session.MachineScore(p.Id) : 0);
                }
            }
            else
            {
                // Still searching: the party, which always plays on one team.
                foreach (ulong id in session.PartyMembers.OrderBy(id => id == session.Self ? 0 : 1)) Add(state, session, id, 0, false);
                for (int i = 1; i <= session.PartyBots; i++)
                {
                    state.Teams.Add(0); state.Joined.Add(true); state.Ready.Add(false);
                    state.Names.Add("BOT " + i); state.Bots.Add(true); state.Faces.Add(null);
                }
                if (session.Party == 0) Add(state, session, session.Self, 0, false);
            }
        }

        // R694: what the room layout shows beside the players' cards.
        static void FillRoom(LoadingState state, SteamSession session)
        {
            int white = session.TeamCount(0), black = session.TeamCount(1), watching = session.Spectators.Count;
            state.RoomCode = StreamerMode.Code(Spaced(session.Match));   // R792: dots in streamer mode
            state.Rules = session.RoomRules;
            state.CanSetRules = session.CanSetRules;
            state.Expected = TeamReservations.TeamSize * 2;
            state.CanCancel = true;
            state.ShowAddBot = false;
            state.ShowStart = session.IsManager;
            state.CanStart = session.CanStartGame;
            for (int seat = 0; seat < 3; seat++) state.CanSit[seat] = session.CanTakeSeat(seat);
            state.ShowTeamBots = session.CanEditTestBots;
            for (int team = 0; team < 2; team++)
            {
                state.CanAddTeamBot[team] = session.CanAddTestBot(team);
                state.CanRemoveTeamBot[team] = session.CanEditTestBots && session.TestBots(team) > 0;
            }
            string seats = Loc.F("{0} 대 {1}", white, black) + (watching > 0 ? " · " + Loc.F("관전 {0}", watching) : "");
            state.RoomNote = session.RoomHeld ? Loc.T(SteamSession.RoomHeldMessage)
                // R697: Pawn Rush's spectators go into the match and watch; another mode's room waits for the seats to empty.
                : watching > 0 && !session.SpectatingAllowed ? seats + " · " + Loc.T("이 모드는 관전석이 비어야 시작할 수 있어요")
                : white + black < 2 ? seats + " · " + Loc.T("2명 이상이면 시작할 수 있어요 (봇 포함)")
                : state.CanStart ? seats + " · " + Loc.T("지금 시작할 수 있어요")
                : session.IsManager ? seats + " · " + Loc.T("모두 입장하면 시작할 수 있어요")
                : seats + " · " + Loc.T("방장이 시작하기를 기다리는 중");
            state.Step = session.NoteShowing ? session.Note
                       : session.IsManager ? Loc.T("방 번호를 친구에게 알려 주세요") : Loc.T("방장이 시작하기를 기다리는 중");
            foreach (ulong id in session.Spectators)
            {
                if (id == session.Self) state.SpecMe = state.SpecKeys.Count;
                state.SpecKeys.Add(id);
                state.SpecNames.Add(session.Name(id));
                state.SpecCanMove.Add(session.CanMovePlayer(id));
                state.SpecCanKick.Add(session.CanKick(id));
            }
        }

        static void Add(LoadingState state, SteamSession session, ulong id, int team, bool ready)
        {
            if (id == session.Self) state.Me = state.Teams.Count;
            if (id == session.Host) state.Host = state.Teams.Count;
            if (id == session.Manager) state.Manager = state.Teams.Count;
            state.Keys.Add(id);
            state.Teams.Add(team);
            state.Joined.Add(true);
            state.Ready.Add(ready);
            state.Names.Add(session.Name(id));
            state.Bots.Add(BotIdentity.IsBot(id));
            state.Faces.Add(session.Avatar(id));
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
            warmFrom = Time.unscaledTime;
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
            EndWalk();
            if (view != null) view.State.Reveal = RevealTime(session);
            switch (phase)
            {
                case Phase.Loading:
                    percent = LoadProgress.Percent(LoadStage.Loading, load.progress);
                    if (load.progress >= .9f && loadedAt < 0) loadedAt = now;
                    // R548: the scene switches on only once Pawn Rush's course reveal has been seen: switching on freezes
                    // the frame (R551: the reels would stop mid-roll), and this PC is not ready before, so nor is the start.
                    // R624: so do Sword Fight's pick, our team's pieces and the clash (the cards must stay clickable).
                    bool picking = view != null && view.SwordHolding;
                    bool revealing = view != null && (view.RevealHolding || picking);
                    step = picking ? Loc.T("경기장을 불러오는 중") : revealing && load.progress >= .9f ? Loc.T("이번 판 코스 공개 중") : Loc.T("코스를 불러오는 중");
                    // A match that ended meanwhile still has to finish this load (Unity
                    // cannot abandon one); NetworkRuntime then heads back to the lobby.
                    // R693: nor while the party is still walking out of the lobby (the screen is not up yet).
                    if ((load.progress >= .9f && now - shownAt >= MinimumShow && !revealing && walkContent == null) || session.Match == 0)
                    {
                        load.allowSceneActivation = true;
                        phase = Phase.Opening;
                        openedAt = now;
                    }
                    break;
                case Phase.Opening:
                    percent = LoadProgress.Percent(LoadStage.Opening, 0);
                    step = Loc.T("코스를 여는 중");
                    break;
                case Phase.WarmingUp:
                    if (session.Match == 0) { Finish(); return; }
                    if (skipFrame) skipFrame = false;
                    else warmup.Add(Time.unscaledDeltaTime * 1000.0);
                    // R732: and whatever the scene draws behind the screen first (the Pawn Rush round's tour of its
                    // sections, RoundPrewarm), for WarmupHoldLimit at most.
                    bool drawing = LoadWarmup.Busy && now - warmFrom < WarmupHoldLimit;
                    percent = LoadProgress.Percent(LoadStage.WarmingUp, Math.Min(warmup.Fraction, drawing ? LoadWarmup.Fraction : 1.0));
                    step = drawing ? Loc.T("코스를 미리 그리는 중") : Loc.T("캐릭터를 준비하는 중");
                    if (warmup.Done && !drawing)
                    {
                        phase = Phase.Waiting; readyAt = now; percent = MatchStart.Ready;
                        // R596: where this PC's load went, against the reveal (the scene may switch on from
                        // RevealShownSeconds), to see which step keeps the 1 standing.
                        Debug.Log($"[ChessFight] 경기 씬 준비 완료: 불러오기 {Since(loadedAt)} · 켜기 {Since(openedAt)} · 켜짐 {Since(warmFrom)} · " +
                                  $"데우기 끝 {now - shownAt:0.00}초 (코스 공개 {(view != null ? view.State.Reveal : -1f):0.00}초, 씬 켜기는 " +
                                  $"{LoadingScreenView.RevealShownSeconds:0.0}초부터)");
                    }
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
            state.Room = false;
            state.Expected = 0;
            state.Clear();
            foreach (var p in session.Roster.Values.OrderBy(p => p.Team).ThenBy(p => p.Slot))
                Add(state, session, p.Id, p.Team, session.LoadPercent(p.Id) >= MatchStart.Ready);
            if (state.SwordPick) FillSwordPicks(state, session);
        }

        // R624: each player's Sword Fight piece for the entrance screen (State.Keys order): the picks held while the cards
        // are open, everyone's (the leftovers drawn, PickAuto) once they close - the draw the scene's host starts with.
        static void FillSwordPicks(LoadingState state, SteamSession session)
        {
            var entries = session.SwordPickEntries();
            double begun = session.BegunAt;
            var held = SwordPickDraft.Resolve(entries, begun);
            state.SwordAllPicked = SwordPickDraft.AllPicked(entries, begun);
            bool closed = SwordPickDraft.StageAt(state.Reveal, state.SwordAllPicked) == SwordPickDraft.Stage.Closed;
            var shown = closed ? SwordPickDraft.Final(entries, begun, SwordPickDraft.Seed(session.Match)) : held;
            state.Picks.Clear();
            state.PickAuto.Clear();
            foreach (ulong id in state.Keys)
            {
                state.Picks.Add(shown.TryGetValue(id, out var piece) ? piece : PieceKind.Pawn);
                state.PickAuto.Add(closed && !held.ContainsKey(id));
            }
        }

        // R548: how far into Pawn Rush's course reveal this PC is, counted from the shared-clock moment the host started
        // the match (SteamSession.BegunAt), so every PC's board shows the same hop at the same moment. From this PC's
        // own start when the host's time is missing (an older host build) or left from an earlier round.
        float RevealTime(SteamSession session)
        {
            if (revealSkip) return RevealSkipped;
            double begun = session.BegunAt;
            double from = begun > 0 && begun > revealFrom - StaleBegun ? begun : revealFrom;
            return (float)Math.Max(0, clock() - from);
        }

        string Since(float at) => at < 0 ? "-" : (at - shownAt).ToString("0.00") + "초";

        // Ready: wait for the host's start time (the session picks it as the host, R502).
        string Wait(SteamSession session, float now)
        {
            // Nobody to wait for without a live match.
            if (!session.Online || session.Match == 0) { Finish(); return ""; }

            double start = session.StartAt;
            // A late player finds the start already past and goes at once.
            if (start > 0 && clock() >= start) { Finish(); return ""; }
            if (start == 0 && now - readyAt >= MatchStart.ClientGiveUp) { Finish(); return ""; }

            bool everyone = session.Roster.Keys.All(id => session.LoadPercent(id) >= MatchStart.Ready);
            if (start == 0) return Loc.T("다른 플레이어를 기다리는 중");
            return everyone ? Loc.T("모두 준비 완료 · 곧 출발해요") : Loc.T("곧 출발해요 · 늦는 플레이어는 준비되는 대로 따라와요");
        }

        void Finish()
        {
            phase = Phase.Idle;
            load = null;
            // R693: a match that ended during the walk out: the party is back on the lobby's stage.
            if (walkContent != null)
            {
                walkContent = null;
                gateSince = -1;
                if (LobbyStage.Current != null) LobbyStage.Current.LeaveGate();
            }
            if (view != null) view.Hide();
            view = null;
        }
    }
}
