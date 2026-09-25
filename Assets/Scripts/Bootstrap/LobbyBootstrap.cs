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
        const int InviteSpotsShown = 2;

        NetworkRuntime runtime;
        SteamSession session => runtime.Session;
        SteamMotion motion => runtime.Motion;

        GameSceneConfig config;
        LobbyStage stage;
        NetworkHudView hud;
        Func<bool> gate;
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

            // The team material doubles as the template for the stage's own colours:
            // it is the one material every PC is guaranteed to have wired.
            stage = new GameObject("Lobby Stage").AddComponent<LobbyStage>();
            stage.Build(config.PawnPrefab, config.BlueTeamMaterial, config.BlueTeamMaterial);

            hud = gameObject.AddComponent<NetworkHudView>();
            hud.Build(config.HudLayout, config.HudTheme, config.HudPanelSettings, config.HudReferenceResolution);
            WireHud();

            gate = () => false;
            runtime.MovementGate = gate;
        }

        void WireHud()
        {
            // "게임 시작" queues the whole party for the chosen mode, as in the
            // reference video; the mode card above it says what that will be.
            hud.Play += () => session.FindMatch();
            hud.CreateTest += () => session.FindMatch(true);
            hud.StartGame += () => session.StartGame();
            hud.Cancel += () => session.Cancel();
            hud.LeaveParty += () => session.LeaveParty();
            hud.JoinParty += id => session.JoinParty(id);
            hud.JoinMatch += id => session.JoinPrivateMatch(id);
            hud.PickMode += key =>
            {
                if (session.SetMode(key)) hud.ShowToast("게임 모드: " + GameModes.Resolve(key).Name);
            };

            // The in-game picker replaces the Steam overlay, which renders at its
            // own resolution and hides presence behind a search box.
            hud.FriendsOpened += () => refreshFriendsAt = 0;
            hud.RefreshFriends += () => refreshFriendsAt = 0;
            hud.InviteFriend += id =>
            {
                if (session.InviteToParty(id)) hud.ShowToast(session.Name(id) + "님에게 초대를 보냈어요");
                else hud.ShowToast("지금은 초대할 수 없습니다.", true);
            };
            hud.SteamOverlayInvite += () => session.Invite();
            hud.CopyParty += () => Copy(session.Party, "파티 코드를 복사했어요");
            hud.CopyMatch += () => Copy(session.Match, "방 번호를 복사했어요");
            hud.AddBot += () => session.SetPartyBots(session.PartyBots + 1);
            hud.RemoveBot += () => session.SetPartyBots(session.PartyBots - 1);
            hud.FillRoom += () => session.FillRoomWithBots();
            hud.ClearRoomBots += () => session.ClearRoomBots();
            hud.RetrySteam += () => session.Retry();
        }

        void Copy(ulong id, string notice)
        {
            if (id == 0) return;
            GUIUtility.systemCopyBuffer = id.ToString();
            hud.ShowToast(notice);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            PollFriends(now);
            if (now < refreshAt) return;
            refreshAt = now + RefreshSeconds;

            Notices();
            var model = BuildModel(now);
            BuildLineup(model.CanInvite);
            stage.Show(lineup);
            hud.SetLineup(lineup, stage.View);
            hud.Render(in model);
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
                foreach (ulong id in members.Except(lastMembers)) hud.ShowToast(session.Name(id) + "님이 파티에 참가했어요");
                foreach (ulong id in lastMembers.Except(members)) hud.ShowToast(session.Name(id) + "님이 파티를 나갔어요");
                if (mode != lastMode && !session.IsLeader) hud.ShowToast("파티장이 모드를 " + session.PartyMode.Name + "(으)로 바꿨어요");
            }
            else if (primed && session.Party != 0 && lastParty != 0 && members.Length > 1) hud.ShowToast("파티에 참가했어요");
            if (session.Error != lastError && !string.IsNullOrEmpty(session.Error)) hud.ShowToast(session.Error, true);

            primed = session.Online;
            lastParty = session.Party;
            lastMembers = members;
            lastMode = mode;
            lastError = session.Error;
        }

        // Me in the middle, the rest of the party round me, then the party bots,
        // then up to two empty spots with an invite button.
        void BuildLineup(bool canInvite)
        {
            lineup.Clear();
            if (!session.Online || session.Party == 0)
            {
                lineup.Add(new LineupEntry { Id = session.Self, Name = session.Online ? session.Name(session.Self) : "나", Tag = session.Online ? "파티 준비 중" : "오프라인", Me = true });
                return;
            }
            ulong leader = session.PartyLeader;
            var members = session.PartyMembers;
            int size = members.Length + session.PartyBots;
            foreach (ulong id in members.OrderBy(id => id == session.Self ? 0 : 1))
            {
                if (lineup.Count >= LobbyStage.Spots) break;
                lineup.Add(new LineupEntry
                {
                    Id = id, Name = session.Name(id), Leader = id == leader, Me = id == session.Self,
                    Tag = id == leader ? $"파티장 · {size}/6" : "대기 중"
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
            bool hostWaiting = session.IsHost && !session.Started;
            int humans = session.PartyMembers.Length;
            int party = humans + session.PartyBots;
            int roomCount = session.Roster.Count;

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
                PlayerName = session.Online ? session.Name(session.Self) : "오프라인",
                Status = session.Online ? session.Status : (string.IsNullOrEmpty(session.Error) ? session.Status : session.Error),
                Details = $"Steam: {(session.Online ? "연결됨" : "연결 안 됨 - Steam 실행 후 다시 연결")}\n" +
                          Line(motion?.ConnectionStatus) + Line(motion?.QualityLine) +
                          $"입력: {runtime.Controls?.DisplayName}\n버전 {session.Build}" +
                          (Debug.isDebugBuild ? "\nF8 지연 시뮬레이터" : ""),
                Version = $"v{session.Build} · STEAM",
                FriendsOnline = friendsOnline,

                ModeKey = mode.Key,
                CanChangeMode = idleLeader,
                ModeHint = !session.IsLeader ? "파티장만 모드를 바꿀 수 있습니다."
                         : session.Busy ? "매칭 중에는 모드를 바꿀 수 없습니다."
                         : "모드마다 따로 매칭합니다. 준비 중인 모드는 아직 고를 수 없습니다.",

                PartySize = session.Online ? party : 0,
                PartyCode = Spaced(session.Party),
                Bots = session.PartyBots.ToString(),
                BotsNote = session.BotsBlockPublicMatch ? "봇 포함 시 비공개 방 전용" : "",
                CanAddBot = idleLeader && session.PartyBots < session.MaxPartyBots,
                CanRemoveBot = idleLeader && session.PartyBots > 0,
                CanLeaveParty = session.Online && !session.Busy,
                CanCopyParty = session.Party != 0,

                Busy = busy,
                // Release builds keep bots out of public matches (M6); the test
                // room still takes them.
                CanPlay = idleLeader && !session.BotsBlockPublicMatch && partyMode.Playable,
                PlaySub = PlaySubtitle(partyMode, party),
                BusyTitle = following ? "파티장을 따라가는 중"
                          : inRoom && session.Started ? "경기 시작!"
                          : inRoom && session.PrivateRoom ? "비공개 방"
                          : "매칭 중",
                BusySub = $"{Clock(now)} · {(inRoom ? roomCount : party)}/12",
                CanCancel = session.Busy,
                ShowStart = inRoom && session.PrivateRoom && session.IsHost,
                // Mirrors StartGame's real rules, so the button is never a no-op.
                CanStart = hostWaiting && (session.PrivateRoom ? roomCount >= 2 : roomCount == 12),

                ShowMatch = busy,
                MatchKicker = (session.PrivateRoom ? "PRIVATE ROOM · " : "QUICK MATCH · ") + mode.Name,
                MatchTitle = !inRoom ? (following ? "파티장이 방을 찾는 중..." : "상대를 찾는 중...")
                           : session.Started ? "경기를 시작합니다!"
                           : session.PrivateRoom ? (session.IsHost ? "방 번호를 친구에게 알려 주세요" : "방장이 시작하기를 기다리는 중")
                           : "상대를 찾는 중...",
                MatchTimer = Clock(now),
                MatchNote = session.AllowPublicBots ? "개발 빌드 · 봇 허용" : "",
                OurTeam = ourTeam, OursFilled = ours, TheirsFilled = theirs,
                ShowRoomTools = inRoom && (session.PrivateRoom || session.CanUseRoomBots),
                RoomCode = "방 번호  " + Spaced(session.Match),
                CanFillRoom = session.CanUseRoomBots && roomCount < 12,
                CanClearRoomBots = hostWaiting && session.RoomBots > 0,

                CanInvite = session.Online && !session.Busy && session.Party != 0 && humans < 6,
                CanJoinParty = session.Online && !session.Busy,
                CanJoinMatch = idleLeader,
                CanCreateTest = idleLeader && partyMode.Playable
            };
        }

        string PlaySubtitle(GameModeInfo mode, int party)
        {
            if (!session.Online) return "Steam 연결이 필요합니다";
            if (!session.IsLeader) return "파티장이 시작하기를 기다리는 중";
            if (!mode.Playable) return mode.Name + " 모드는 준비 중입니다";
            if (session.BotsBlockPublicMatch) return "봇이 있으면 비공개 방에서만 시작할 수 있어요";
            return party > 1 ? $"{mode.Name} · 파티 {party}명 모두 같은 팀" : $"{mode.Name} · 빠른 매칭";
        }

        string Clock(float now)
        {
            int seconds = busySince < 0 ? 0 : Mathf.FloorToInt(now - busySince);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }

        // Steam lobby IDs are 17-18 digits; groups of four are far easier to read
        // aloud. Copy and join always use the plain number.
        static string Spaced(ulong id)
        {
            if (id == 0) return "—";
            string digits = id.ToString();
            var text = new StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 4 == 0) text.Append(' ');
                text.Append(digits[i]);
            }
            return text.ToString();
        }

        static string Line(string text) => string.IsNullOrEmpty(text) ? "" : text + "\n";

        void OnDestroy()
        {
            // The session outlives this scene; only the scene's own objects go.
            if (runtime != null && runtime.MovementGate == gate) runtime.MovementGate = null;
            if (stage != null) Destroy(stage.gameObject);
        }
    }
}
