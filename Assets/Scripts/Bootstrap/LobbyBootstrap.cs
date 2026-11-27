using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // The lobby scene: the party lineup (LobbyStage) under the lobby HUD
    // (NetworkHudView), laid out after the reference video of 2026-09-25.
    //
    // It does not own the Steam session. NetworkRuntime does, and survives scene
    // loads; this only draws what the session holds and forwards the buttons.
    // NetworkRuntime attaches it when the Lobby scene loads.
    //
    // The lobby is a menu: nothing moves here, so WASD is gated off. The movement
    // test lives on in the match scene.
    [DisallowMultipleComponent]
    public sealed class LobbyBootstrap : MonoBehaviour
    {
        const float RefreshSeconds = .2f;
        const int InviteSpotsShown = 1;      // R581: one invite spot, right after the last of the party

        NetworkRuntime runtime;
        SteamSession session => runtime.Session;
        SteamMotion motion => runtime.Motion;

        GameSceneConfig config;
        LobbyStage stage;
        NetworkHudView hud;
        Func<bool> gate, hold;
        float refreshAt, refreshFriendsAt, countFriendsAt, busySince = -1;
        int friendsOnline;
        readonly List<LineupEntry> lineup = new List<LineupEntry>();

        // What the last refresh saw, to turn changes into short notices.
        bool primed;
        ulong lastParty;
        ulong[] lastMembers = Array.Empty<ulong>();
        string lastError = "", lastMode = "";

        void Awake()
        {
            runtime = NetworkRuntime.Instance;
            if (runtime == null)
            {
                Debug.LogError("[ChessFight] NetworkRuntime 없이 로비를 열 수 없습니다.");
                enabled = false;
                return;
            }
            config = GetComponent<GameSceneConfig>();
            if (config == null) config = gameObject.AddComponent<GameSceneConfig>();
            RenderSettings.ambientLight = config.AmbientLight;

            // The stage sets its own sky, haze and light over the scene defaults.
            stage = new GameObject("Lobby Stage").AddComponent<LobbyStage>();
            stage.Build();

            hud = gameObject.AddComponent<NetworkHudView>();
            hud.Build(config.HudLayout, config.HudTheme, config.HudPanelSettings, config.HudReferenceResolution);
            WireHud();
            // R862: the friend 1:1 chat - the friends card's "대화" and its window (LobbyFriendChat).
            gameObject.AddComponent<LobbyFriendChat>().Attach(runtime, hud, () => Matching);
            // R651/R654: the lobby's music (LobbyMusic), on from the title screen; quiet while this PC is matching or
            // in a match room, and on from the start again when the lobby comes back after a match.
            hold = () => Matching;
            LobbyMusic.Hold = hold;
            LobbyMusic.Begin();

            gate = () => false;
            runtime.MovementGate = gate;
        }

        void WireHud()
        {
            // "게임 시작" queues the whole party for the chosen mode, as in the
            // reference video; the mode card above it says what that will be.
            hud.Play += () => session.FindMatch();
            // R538: back into the started match this player left (its own seat, its own checkpoint).
            hud.Rejoin += () => { session.Rejoin(); refreshAt = 0; };
            hud.CreateTest += () => session.FindMatch(true);
            hud.StartGame += () => session.StartGame();
            hud.Cancel += () => session.Cancel();
            hud.LeaveParty += () => session.LeaveParty();
            hud.JoinParty += id => session.JoinParty(id);
            hud.JoinMatch += id => session.JoinPrivateMatch(id);
            hud.PickMode += key =>
            {
                if (session.SetMode(key)) hud.ShowToast(Loc.F("게임 모드: {0}", Loc.T(GameModes.Resolve(key).Name)));
            };

            // The in-game picker replaces the Steam overlay, which renders at its
            // own resolution and hides presence behind a search box.
            hud.FriendsOpened += () => refreshFriendsAt = 0;
            hud.RefreshFriends += () => refreshFriendsAt = 0;
            hud.InviteFriend += id =>
            {
                if (session.InviteToParty(id)) hud.ShowToast(Loc.F("{0}님에게 초대를 보냈어요", session.Name(id)));
                else hud.ShowToast(Loc.T("지금은 초대할 수 없습니다."), true);
            };
            hud.SteamOverlayInvite += () => session.Invite();
            // R529: an invite this PC received, answered on the lobby's card.
            hud.AcceptInvite += lobby =>
            {
                session.AcceptInvite(lobby);
                if (!string.IsNullOrEmpty(session.Error)) hud.ShowToast(session.Error, true);
                refreshAt = 0;
            };
            hud.DeclineInvite += lobby => { session.DeclineInvite(lobby); refreshAt = 0; };
            // My name top right (R85): the name screen over the lobby, then back.
            hud.Rename += () =>
            {
                if (NameScreen.Showing || SceneTransition.Busy) return;
                if (Matching) { hud.ShowToast(Loc.T("매칭 중에는 이름을 바꿀 수 없어요"), true); return; }
                NameChange.Begin(hud.ProfileAt, () => stage != null ? stage.SelfOnScreen() : new Vector2(.5f, .62f), Loc.T("로비"));
            };
            hud.CopyParty += () => Copy(session.Party, Loc.T("파티 코드를 복사했어요"));
            hud.CopyMatch += () => Copy(session.Match, Loc.T("방 번호를 복사했어요"));
            hud.AddBot += () => session.SetPartyBots(session.PartyBots + 1);
            hud.RemoveBot += () => session.SetPartyBots(session.PartyBots - 1);
            hud.FillRoom += () => session.FillRoomWithBots();
            hud.ClearRoomBots += () => session.ClearRoomBots();
            hud.AddAllyDummy += () => ChangeDummy(false, true);
            hud.RemoveAllyDummy += () => ChangeDummy(false, false);
            hud.AddEnemyDummy += () => ChangeDummy(true, true);
            hud.RemoveEnemyDummy += () => ChangeDummy(true, false);
            hud.RetrySteam += () => session.Retry();
            // R736: 기물 도감 · 전적 - the party steps off the board, and the codex's piece stands there instead.
            hud.TabChanged += tab => stage.Showcase(tab != LobbyTab.Play, tab == LobbyTab.Codex ? hud.CodexKind : (PieceKind?)null, hud.CodexTeam);
            hud.CodexPiece += (kind, team) => { if (hud.Tab == LobbyTab.Codex) stage.Showcase(true, kind, team); };
            hud.CodexSpin += degrees => stage.SpinShowcase(degrees);
        }

        void ChangeDummy(bool enemy, bool add)
        {
            if (!session.Roster.TryGetValue(session.Self, out var me)) return;
            int team = enemy ? 1 - me.Team : me.Team;
            if (add) session.AddTestBot(team); else session.RemoveTestBot(team);
            refreshAt = 0;
        }

        void Copy(ulong id, string notice)
        {
            if (id == 0) return;
            GUIUtility.systemCopyBuffer = LobbyCode.Plain(id);   // R737: the short number, as shown
            hud.ShowToast(notice);
        }

        // Searching, following the leader, or in a match room: the lobby is about to
        // be covered or left, so the name stays as it is.
        bool Matching => session.Busy || session.Match != 0;

        void Update()
        {
            float now = Time.unscaledTime;
            // A party leader may start a search while this member changes the name.
            if (NameChange.Showing && Matching) NameChange.Abort();
            PollFriends(now);
            // R533: a development build can wipe this player's desertions (to test the matchmaking stop itself).
            if (NetworkRuntime.TestRules && session.Online && LegacyKeys.Down(KeyCode.F9) && !ChatBox.KeysHeld)
            {
                session.ClearLeavePenalty();
                hud.ShowToast(Loc.T("시험 규칙: 탈주 기록을 지웠어요"));
                refreshAt = 0;
            }
            if (now < refreshAt) return;
            refreshAt = now + RefreshSeconds;

            Notices();
            var model = BuildModel(now);
            BuildLineup(model.CanInvite);
            stage.Show(lineup);
            hud.SetLineup(lineup, stage.View);
            hud.Render(in model);
            // R529: the newest party invite this PC received (the session keeps a few, each for a while).
            var invites = session.Invites;
            if (invites.Count == 0) hud.SetInvite("", 0);
            else hud.SetInvite(session.Name(invites[invites.Count - 1].From), invites[invites.Count - 1].Lobby, invites.Count - 1);
        }

        // Presence changes while the panel sits open, so re-poll on a timer. With
        // the panel closed, a slower poll keeps the "친구 n" count honest.
        void PollFriends(float now)
        {
            if (!session.Online) return;
            bool open = hud.FriendsOpen;
            if (open ? now < refreshFriendsAt : now < countFriendsAt) return;
            refreshFriendsAt = now + 3f;
            countFriendsAt = now + 10f;
            var list = session.Friends();
            friendsOnline = list.Count(f => f.Presence != FriendPresence.Offline);
            if (open) hud.SetFriends(list, session.PartyMembers);
        }

        void Notices()
        {
            var members = session.PartyMembers;
            string mode = session.PartyMode.Key;
            if (primed && session.Party == lastParty && session.Party != 0)
            {
                foreach (ulong id in members.Except(lastMembers)) hud.ShowToast(Loc.F("{0}님이 파티에 참가했어요", session.Name(id)));
                foreach (ulong id in lastMembers.Except(members)) hud.ShowToast(Loc.F("{0}님이 파티를 나갔어요", session.Name(id)));
                if (mode != lastMode && !session.IsLeader) hud.ShowToast(Loc.F("파티장이 모드를 {0}(으)로 바꿨어요", Loc.T(session.PartyMode.Name)));
            }
            else if (primed && session.Party != 0 && lastParty != 0 && members.Length > 1) hud.ShowToast(Loc.T("파티에 참가했어요"));
            if (session.Error != lastError && !string.IsNullOrEmpty(session.Error)) hud.ShowToast(session.Error, true);

            primed = session.Online;
            lastParty = session.Party;
            lastMembers = members;
            lastMode = mode;
            lastError = session.Error;
        }

        // Me in the middle, the rest of the party round me, then the party bots,
        // then one empty spot with an invite button (R581).
        void BuildLineup(bool canInvite)
        {
            lineup.Clear();
            if (!session.Online || session.Party == 0)
            {
                lineup.Add(new LineupEntry { Id = session.Self, Name = session.Online ? session.Name(session.Self) : OfflineName(Loc.T("나")), Tag = session.Online ? Loc.T("파티 준비 중") : Loc.T("오프라인"), Me = true });
                return;
            }
            ulong leader = session.PartyLeader;
            var members = session.PartyMembers;
            int size = members.Length + session.PartyBots;
            // Alone in my own party lobby is not a party yet: no leader tag or crown
            // until a friend has joined (R796).
            bool party = members.Length > 1;
            foreach (ulong id in members.OrderBy(id => id == session.Self ? 0 : 1))
            {
                if (lineup.Count >= LobbyStage.Spots) break;
                bool leads = party && id == leader;
                lineup.Add(new LineupEntry
                {
                    Id = id, Name = session.Name(id), Leader = leads, Me = id == session.Self,
                    Tag = leads ? Loc.F("파티장 · {0}/6", size) : Loc.T("대기 중")
                });
            }
            for (int i = 1; i <= session.PartyBots && lineup.Count < LobbyStage.Spots; i++)
                lineup.Add(new LineupEntry { Id = ulong.MaxValue - (ulong)i, Name = "BOT " + i, Tag = "AI", Bot = true });
            for (int i = 0; canInvite && i < InviteSpotsShown && lineup.Count < LobbyStage.Spots; i++)
                lineup.Add(new LineupEntry { Invite = true });
        }

        HudModel BuildModel(float now)
        {
            bool inRoom = session.Match != 0;
            // A follower's party is busy while the leader searches or leads it into a room.
            bool following = session.Party != 0 && session.Busy && !session.IsLeader && !inRoom;
            bool busy = session.Searching || inRoom || following;
            if (busy && busySince < 0) busySince = now;
            if (!busy) busySince = -1;

            var partyMode = session.PartyMode;
            var mode = inRoom ? session.MatchMode : partyMode;
            bool idleLeader = session.Online && session.IsLeader && !session.Busy;
            // R60: members still playing a match they stayed in, or this member
            // back early from one, keep the party from queueing again.
            bool waiting = session.WaitingForParty;
            bool canQueue = idleLeader && !session.PartyStillPlaying;
            // R533: why matchmaking is stopped for this party just now (a deserter's stop), "" when it is not.
            string stop = session.Busy ? "" : session.PenaltyNotice;
            int humans = session.PartyMembers.Length;
            int party = humans + session.PartyBots;
            int roomCount = session.Roster.Count;
            bool sword = mode == GameModes.SwordFight;
            bool dummyRoom = inRoom && session.PrivateRoom && sword && !session.Started;

            // R693: random matching - a public search or the public room it found. The lobby stays; its card counts the
            // time and nothing else (the user 2026-11-17: "매칭 시간만 카운트").
            bool quick = busy && !session.PrivateRoom && !waiting && !following;

            int ourTeam = 0, ours = party, theirs = 0;
            if (inRoom && session.Roster.TryGetValue(session.Self, out var me))
            {
                ourTeam = me.Team;
                ours = session.Roster.Values.Count(p => p.Team == ourTeam);
                theirs = roomCount - ours;
            }

            return new HudModel
            {
                Online = session.Online,
                CanRetry = !session.Online,
                PlayerName = session.Online ? session.Name(session.Self) : OfflineName(Loc.T("오프라인")),
                Status = session.Online ? session.Status : (string.IsNullOrEmpty(session.Error) ? session.Status : session.Error),
                Details = "Steam: " + (session.Online ? Loc.T("연결됨") : Loc.T("연결 안 됨 - Steam 실행 후 다시 연결")) + "\n" +
                          Line(motion?.ConnectionStatus) + Line(motion?.QualityLine) +
                          Loc.F("입력: {0}", runtime.Controls?.DisplayName) + "\n" + Loc.F("버전 {0}", session.Build) +
                          (Debug.isDebugBuild ? "\n" + Loc.T("F8 지연 시뮬레이터 · F9 탈주 기록 지우기") : NetworkRuntime.TestRules ? "\n" + Loc.T("F9 탈주 기록 지우기") : ""),
                Version = $"v{session.Build} · STEAM",
                FriendsOnline = friendsOnline,

                ModeKey = mode.Key,
                CanChangeMode = idleLeader && !session.CanRejoin,
                ModeHint = session.CanRejoin ? Loc.T("진행 중인 경기가 끝나기 전에는 모드를 바꿀 수 없습니다.")
                         : !session.IsLeader ? Loc.T("파티장만 모드를 바꿀 수 있습니다.")
                         : session.Busy ? Loc.T("매칭 중에는 모드를 바꿀 수 없습니다.")
                         : Loc.T("모드마다 따로 매칭합니다. 준비 중인 모드는 아직 고를 수 없습니다."),

                PartySize = session.Online ? party : 0,
                PartyCode = StreamerMode.Code(Spaced(session.Party)),   // R792: dots in streamer mode (the copy button copies the real one)
                Bots = session.PartyBots.ToString(),
                BotsNote = session.BotsBlockPublicMatch ? Loc.T("봇 포함 시 비공개 방 전용") : "",
                CanAddBot = idleLeader && session.PartyBots < session.MaxPartyBots,
                CanRemoveBot = idleLeader && session.PartyBots > 0,
                CanLeaveParty = session.Online && (!session.Busy || waiting),
                CanCopyParty = session.Party != 0,

                Busy = busy,
                // Release builds keep bots out of public matches (M6); the test
                // room still takes them.
                CanPlay = canQueue && !session.CanRejoin && !session.BotsBlockPublicMatch && partyMode.Playable && stop == "",
                // 11-11: while the left match runs, "게임 시작" stays shut and says why (the green button goes back in).
                PlayNote = stop != "" ? stop : session.CanRejoin && !session.Busy ? Loc.T("진행 중인 경기가 끝나기 전에는 새 경기를 시작할 수 없어요") : "",
                RejoinNote = session.CanRejoin ? Loc.F("{0} · 나갔던 경기가 아직 진행 중이에요 · 내 자리로 돌아가요", Loc.T(session.RejoinMode.Name)) : "",
                BusyTitle = waiting ? Loc.T("파티원 경기 중")
                          : following ? Loc.T("파티장을 따라가는 중")
                          : inRoom && session.Started ? (session.PrivateRoom ? Loc.T("경기 시작!") : Loc.T("상대를 찾았습니다"))
                          : session.JoiningLive ? Loc.T("경기로 돌아가는 중")
                          : inRoom && session.PrivateRoom ? Loc.T("비공개 방")
                          : Loc.T("상대를 찾는 중"),
                BusySub = quick ? Clock(now) : $"{Clock(now)} · {(inRoom ? roomCount : party)}/12",
                CanCancel = session.Busy && !waiting,
                ShowStart = inRoom && session.PrivateRoom && session.IsManager,
                // Mirrors StartGame's real rules, so the button is never a no-op.
                CanStart = session.CanStartGame,

                // R693: a public search shows the card over the start button only; the banner is for private rooms,
                // for a member waiting for the party, and for the moment a match is found (the party walks out).
                // R782 (user 11-23: "어짜피 방 들어가는 거잖아"): not over a room either - the room screen (MatchLoader.RoomScreen)
                // already has the number, the seats and the manager's buttons; a private room keeps it only to rejoin.
                ShowMatch = busy && (waiting || session.Started || (session.PrivateRoom && session.JoiningLive)),
                MatchKicker = (session.PrivateRoom ? "PRIVATE ROOM · " : "QUICK MATCH · ") + Loc.T(mode.Name),
                MatchTitle = !inRoom ? (waiting ? Loc.T("파티원들이 경기를 마치기를 기다리는 중") : following ? Loc.T("파티장이 방을 찾는 중...") : Loc.T("상대 팀 찾는 중"))
                           : session.Started ? (session.PrivateRoom ? Loc.T("경기를 시작합니다!") : Loc.T("상대를 찾았습니다"))
                           : session.JoiningLive ? Loc.T("나갔던 경기로 돌아가는 중...")
                           : dummyRoom && session.IsManager ? Loc.T("더미를 추가하고 시작하세요")
                           : session.PrivateRoom ? (session.IsManager ? Loc.T("방 번호를 친구에게 알려 주세요") : Loc.T("방장이 시작하기를 기다리는 중"))
                           : Loc.T("상대 팀 찾는 중"),
                MatchTimer = Clock(now),
                MatchNote = waiting ? Loc.T("먼저 나왔어요 · 혼자 하려면 파티 나가기")
                          : session.JoiningLive ? Loc.T("내 자리와 체크포인트로 돌아가요")
                          : dummyRoom ? Loc.T("12명 없이 시작 가능") : session.AllowPublicBots ? Loc.T("시험 규칙 · 1 대 1부터 · 봇 허용") : "",
                OurTeam = ourTeam, OursFilled = ours, TheirsFilled = theirs,
                ShowRoomTools = inRoom && (session.PrivateRoom || session.CanUseRoomBots),
                RoomCode = Loc.F("방 번호  {0}", StreamerMode.Code(Spaced(session.Match))),   // R792
                CanFillRoom = session.CanUseRoomBots && session.Roster.ContainsKey(session.Self) && roomCount < 12,
                CanClearRoomBots = session.CanClearRoomBots,
                ShowTestBots = dummyRoom,
                AllyDummies = session.Roster.Values.Count(p => p.Team == ourTeam && BotIdentity.IsBot(p.Id)),
                EnemyDummies = session.Roster.Values.Count(p => p.Team != ourTeam && BotIdentity.IsBot(p.Id)),
                CanAddAlly = dummyRoom && session.CanAddTestBot(ourTeam),
                CanAddEnemy = dummyRoom && session.CanAddTestBot(1 - ourTeam),
                CanRemoveAlly = dummyRoom && session.CanEditTestBots && session.TestBots(ourTeam) > 0,
                CanRemoveEnemy = dummyRoom && session.CanEditTestBots && session.TestBots(1 - ourTeam) > 0,
                TestHint = !session.IsManager ? Loc.T("방장만 더미 수를 변경하고 시작할 수 있어요.")
                    : Loc.T("이동·공격 없는 더미 · 팀당 최대 6명 (나 포함)") + "\n" +
                      (roomCount < 2 ? Loc.T("아군이나 적군 더미를 1명 이상 추가하세요.")
                       : session.PartyBots > 0 ? Loc.T("파티 더미는 유지됩니다. −는 이 방에서 추가한 더미만 제거해요.")
                       : Loc.T("준비되면 오른쪽 아래 테스트 시작을 누르세요.")),

                CanInvite = session.Online && !session.Busy && session.Party != 0 && humans < 6,
                CanJoinParty = session.Online && !session.Busy,
                CanJoinMatch = canQueue,
                CanCreateTest = canQueue && partyMode.Playable
            };
        }

        string Clock(float now)
        {
            int seconds = busySince < 0 ? 0 : Mathf.FloorToInt(now - busySince);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        // Steam lobby IDs are 17-18 digits; only their lower 10 are shown, in fours (LobbyCode, R737).
        static string Spaced(ulong id) => LobbyCode.Format(id);

        static string Line(string text) => string.IsNullOrEmpty(text) ? "" : text + "\n";

        // Offline there is no Steam name, but the chosen one still shows (R85).
        static string OfflineName(string otherwise) => PlayerProfile.HasName ? PlayerProfile.Name : otherwise;

        void OnDestroy()
        {
            // The session outlives this scene; only the scene's own objects go.
            if (runtime != null && runtime.MovementGate == gate) runtime.MovementGate = null;
            if (LobbyMusic.Hold == hold) LobbyMusic.Hold = null;
            if (stage != null) Destroy(stage.gameObject);
        }
    }
}
