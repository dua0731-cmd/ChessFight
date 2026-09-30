using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Steamworks;
using UnityEngine;

namespace ChessFight.Network
{
    // Steam supplies discovery, invitations and authenticated peer IDs. This class
    // supplies party-sized admissions; lobby search itself is NOT matchmaking.
    public sealed class SteamSession : IDisposable
    {
        // v2: jump travels as a press count (MotionProtocol "CFF2").
        // v3: parties and matches carry a game mode, and search filters on it.
        //     A v2 build would ignore the filter and walk into another mode's room.
        // v4: the ragdoll lab's input packet carries the ability keys, interact and
        //     the aim (RagdollNetProtocol "CFR2"). A v3 lab build would drop them.
        // v5: the ragdoll lab's snapshot carries the struggle meter ("CFR3"), so a
        //     client sees how close a grabbed pawn is to breaking free.
        // v6: the lab's input carries the left button held and its snapshot the pawn's
        //     grappling hook (RagdollNetProtocol "CFR4", Queen of the Hill M5).
        // v7: the lab's snapshot carries each pawn's piece ("CFR5", Queen of the Hill M11).
        // v8: physical Sword Fight weapon poses (CFS2, channel 33); CFR4 input is unchanged.
        //     v8-v10 grew on JY-gpt_gamemode/JY-kingrush, v7 on JY-ragdoll_v2, side by side.
        // v11: JY-kingrush with feature/ui-sample-b merged in: the Sword Fight rules of
        //      v10 and the lab snapshot's piece byte of v7 ("CFR5") in one build.
        // v12: the host role can move (Docs/Network/HOST.md; "v8" on claude/host-migration):
        //      the match room carries "epoch" and "successors", members publish "fit",
        //      "base", "loc" and "claim". A v11 build would end the match where a v12
        //      build hands it over.
        // v13: the match loads behind a loading screen and starts together: members
        //      publish "load" (0-100) and the host "go", the shared start time
        //      (MatchStart). A v12 build never reports ready, so a v13 room would sit
        //      out its whole wait for it.
        // v14: a started match refills the seats of players who left from matchmaking
        //      (Backfill): the room carries "open", "seats" and "held", and search
        //      filters on "open" instead of "phase". A v13 host would ignore requests
        //      for its empty seats.
        public const string Protocol = "chessfight.dua0731.network.v14";
        // Which build made a lobby. Two builds of the same protocol can still
        // disagree on game rules, so rooms and parties only admit the same build.
        public string Build { get; }
        // Release rule M6: public matches are people only. Development builds
        // keep bots everywhere, because there are not twelve testers.
        public bool AllowPublicBots { get; }
        public ulong Self { get; private set; }
        public ulong Party { get; private set; }
        public ulong Match { get; private set; }
        public ulong Host { get; private set; }
        public bool Online { get; private set; }
        public bool Searching { get; private set; }
        public bool Started { get; private set; }
        public bool IsHost => Match != 0 && Host == Self;
        // Bots the party leader takes into the queue. They occupy team slots of
        // the party's own team exactly like absent members would.
        public int PartyBots { get; private set; }
        public int MaxPartyBots => Math.Max(0, TeamReservations.TeamSize - (Party == 0 ? 1 : Members(Party).Count));
        public int RoomBots => reservations.Bots;
        // A private test room starts at two pawns; a public room only at twelve.
        public bool PrivateRoom => privateRoom;
        public bool IsLeader => Party != 0 && Owner(Party) == Self;
        public ulong PartyLeader => Owner(Party);
        public bool BotsBlockPublicMatch => !AllowPublicBots && PartyBots > 0;
        public bool CanUseRoomBots => IsHost && !Started && (privateRoom || AllowPublicBots);
        public bool CanEditTestBots => IsHost && privateRoom && !Started && Roster.ContainsKey(Self);
        public bool CanStartGame => IsHost && !Started && reservations.Groups.All(g => g.Committed) &&
            (privateRoom ? Roster.Count >= 2 : reservations.Ready);
        public int TestBots(int team) => reservations.FillerBots(team);
        public bool CanAddTestBot(int team) => CanEditTestBots && team >= 0 && team <= 1 && reservations.Used(team) < TeamReservations.TeamSize;
        public bool Busy => pending || Searching || Match != 0 || Route(Party) != "idle";
        // The mode the party queues for. Everyone in the party reads it from the
        // party lobby; only the leader writes it (SetMode).
        public GameModeInfo PartyMode => GameModes.Resolve(Data(Party, "mode"));
        // The mode of the match room we are in, which is the host's choice. A room
        // joined by its number keeps its own mode whatever the party had picked.
        public GameModeInfo MatchMode => Match == 0 ? null : GameModes.Resolve(Data(Match, "mode"));
        // A searching player inside a started match, waiting for the host to seat it.
        public bool JoiningLive => Match != 0 && !Started && Live(Match);
        // Empty seats a started match offers to matchmaking right now (Backfill).
        public int EmptySeats => Match == 0 || !Started || Data(Match, "open") != "1" ? 0
            : ParseInt(Data(Match, "free0")) + ParseInt(Data(Match, "free1"));
        public string Status { get; private set; } = "Steam 연결 중...";
        public string Error { get; private set; } = "";
        public readonly Dictionary<ulong, PawnState> Roster = new Dictionary<ulong, PawnState>();
        public event Action SessionChanged;

        // ---- Host election and migration (Docs/Network/HOST.md) ----
        // The host role is numbered: every change of host raises the epoch, and
        // the highest epoch a player can see wins. Only the lobby owner can write
        // the room's "host"/"epoch", so a successor that does not own the lobby
        // yet announces itself with its own member data "claim" first.
        public int Epoch => epoch;
        // This machine's HostFitness.Base (benchmark and hardware), set by whoever
        // runs the session once HostFitnessProbe has finished.
        public int LocalFitness { get; set; }
        // The same with this PC's live frame rate, which is what the election uses.
        public int CurrentFitness => HostFitness.WithFrames(LocalFitness, FrameMs);
        // This PC's smoothed frame time (ReportFrame), -1 until measured.
        public double FrameMs => frames.AverageMs;
        // Raised while in a match when the host changes: (previous, next).
        public event Action<ulong, ulong> HostChanged;
        // A few seconds of news in the status line: a new host, a filled seat.
        public string Note { get; private set; } = "";
        // How long a host may be silent before its successor steps in, and how
        // long after any start or handover that rule stays off: a scene load
        // freezes every PC for a few seconds and must not look like a dead host.
        public const float MigrateAfterSilence = 4f, MigrationGrace = 10f;
        // A host gone from the room whose successor never claims: give up.
        public const float OrphanTimeout = 10f;
        // Between a start or handover and the next handover for slowness.
        public const float StruggleCooldown = 45f;
        // ---- Loading and a shared start (MatchStart) ----
        // How far this PC's match scene has loaded; published as member data "load".
        public void ReportLoading(int percent) => loadPercent = MatchStart.Clamp(percent);
        // How far a player's match scene has loaded, 0-100. Bots have nothing to load.
        public int LoadPercent(ulong id) =>
            BotIdentity.IsBot(id) ? MatchStart.Ready : id == Self ? loadPercent : MatchStart.Parse(MemberData(Match, id, "load"));
        // The shared-clock time the host picked for everyone to start; 0 until then.
        public double StartAt => MatchStart.ParseTime(Data(Match, "go"));
        // Only a host that owns the room can write it (HOST.md), so a successor
        // still waiting for ownership leaves the start to the next poll.
        public bool CanAnnounceStart => IsHost && Started && Owner(Match) == Self;
        public void AnnounceStart(double at)
        {
            if (CanAnnounceStart && StartAt == 0) Set(Match, "go", MatchStart.FormatTime(at));
        }
        int loadPercent, publishedLoad = -1;
        // ---- Empty seats of a started match (Backfill) ----
        // Seats the host keeps for a joining party's members still on their way.
        // The room carries the same list ("held"), so a new host picks it up.
        readonly List<Backfill.Seat> held = new List<Backfill.Seat>();
        bool heldLoaded;
        // What this host last set the room's Steam joinable flag to: -1 unknown, 0 or 1.
        int joinable = -1;

        readonly FrameMonitor frames = new FrameMonitor();
        int epoch, publishedFit, publishedBase;
        string publishedLoc = "", localLoc = "";
        float hostSilence, hostSince = -1, orphanSince = -1, nextFitPublish, nextSuccessors, nextOwnerFix, lastHandoff = -1000, noteUntil;
        readonly TeamReservations reservations = new TeamReservations();
        readonly List<IDisposable> callbacks = new List<IDisposable>();
        readonly List<IDisposable> calls = new List<IDisposable>();
        readonly Queue<ulong> candidates = new Queue<ulong>();
        readonly Dictionary<ulong, string> cancelBaseline = new Dictionary<ulong, string>();
        readonly byte[] chatBuffer = new byte[2048];
        ulong[] queuedMembers = Array.Empty<ulong>();   // humans + declared party bots
        ulong[] queuedHumans = Array.Empty<ulong>();    // humans only, for roster-change detection
        ulong partyOwner;
        string ticket = "", presence, carriedError, queuedMode = GameModes.Default.Key;
        bool pending, admitted, seenRoster, privateRoom, cancelledFollower, disposed;
        int generation;
        float nextPoll, nextSearch, deadline, nextRequest, mergeAt, invalidHostSince = -1;
        int emptySearches;

        [Serializable] sealed class Request { public string kind, ticket; public ulong party; public ulong[] members; }
        [Serializable] sealed class RosterData { public Member[] members; }
        [Serializable] sealed class Member { public ulong id; public int team, slot; }

        public SteamSession(string build = "", bool allowPublicBots = true)
        { Build = build ?? ""; AllowPublicBots = allowPublicBots; }

        static CSteamID Id(ulong id) => new CSteamID(id);
        public string Name(ulong id)
        {
            if (BotIdentity.IsBot(id)) return Roster.TryGetValue(id, out var bot) ? "BOT " + (bot.Slot + 1) : "BOT";
            return Online ? SteamFriends.GetFriendPersonaName(Id(id)) : id.ToString();
        }
        public ulong[] PartyMembers => Party == 0 ? Array.Empty<ulong>() : Members(Party).OrderBy(x => x).ToArray();
        static ulong Owner(ulong lobby) => lobby == 0 ? 0 : SteamMatchmaking.GetLobbyOwner(Id(lobby)).m_SteamID;
        static string Data(ulong lobby, string key) => lobby == 0 ? "" : SteamMatchmaking.GetLobbyData(Id(lobby), key);
        static string Route(ulong lobby) { string r = Data(lobby, "route"); return string.IsNullOrEmpty(r) ? "idle" : r; }
        static HashSet<ulong> Members(ulong lobby)
        {
            var ids = new HashSet<ulong>();
            for (int i = 0, n = SteamMatchmaking.GetNumLobbyMembers(Id(lobby)); i < n && i < 12; i++)
                ids.Add(SteamMatchmaking.GetLobbyMemberByIndex(Id(lobby), i).m_SteamID);
            return ids;
        }
        static void Set(ulong lobby, string key, string value) => SteamMatchmaking.SetLobbyData(Id(lobby), key, value);
        static bool Live(ulong lobby) => Data(lobby, "phase") == "playing";
        bool Compatible(ulong lobby, string kind) => Incompatibility(lobby, kind) == null;
        // Null when the lobby is ours to join, otherwise the reason for the player.
        string Incompatibility(ulong lobby, string kind)
        {
            if (Data(lobby, "kind") != kind) return "방이 가득 찼거나 닫혔습니다.";
            if (Data(lobby, "protocol") != Protocol || Data(lobby, "build") != Build)
            {
                string theirs = Data(lobby, "build");
                return $"게임 버전이 다릅니다. (내 버전 {Show(Build)} / 상대 {Show(theirs)}) 같은 빌드로 맞춘 뒤 다시 시도하세요.";
            }
            return null;
        }
        static string Show(string build) => string.IsNullOrEmpty(build) ? "알 수 없음" : build;
        // "+connect_lobby <id>", the form Steam passes both on the command line
        // and through a Rich Presence join.
        public static ulong ParseConnect(string text)
        {
            var parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i + 1 < parts.Length; i++)
                if (parts[i] == "+connect_lobby" && ulong.TryParse(parts[i + 1], out ulong lobby)) return lobby;
            return 0;
        }
        public bool IsPeer(ulong id) => Match != 0 && (id == Host || Roster.ContainsKey(id));
        static string MemberData(ulong lobby, ulong user, string key) => lobby == 0 ? "" : SteamMatchmaking.GetLobbyMemberData(Id(lobby), Id(user), key);
        static int ParseInt(string text) => int.TryParse(text, out int value) ? value : 0;
        static ulong ParseId(string text) => ulong.TryParse(text, out ulong value) ? value : 0;
        // Steam's server clock in whole seconds: the same on every PC, so a time a
        // host writes into the room still means the same to the next host.
        static double ServerNow() => SteamUtils.GetServerRealTime();

        // Every frame, from whoever runs the session: the frame time feeds the
        // host's slowness check and, through LocalFitness, the election.
        public void ReportFrame(float frameMs) => frames.Add(frameMs, Time.realtimeSinceStartup);
        // Every frame on a client, from the movement layer: seconds since the
        // host's last snapshot. A host silent this long is replaced.
        public void ReportHostSilence(float seconds) => hostSilence = seconds;

        // Steam's estimate of where this PC sits in the relay network, as text.
        // Empty until Steam has measured it (a few seconds after start).
        string LocalPingLocation()
        {
            if (localLoc != "") return localLoc;
            if (SteamNetworkingUtils.GetLocalPingLocation(out SteamNetworkPingLocation_t location) < 0) return "";
            SteamNetworkingUtils.ConvertPingLocationToString(ref location, out string text, Constants.k_cchMaxSteamNetworkingPingLocationString);
            return localLoc = text ?? "";
        }
        // Estimated round trip between two published locations, -1 when unknown.
        static int PingBetween(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return -1;
            if (!SteamNetworkingUtils.ParsePingLocationString(a, out SteamNetworkPingLocation_t first) ||
                !SteamNetworkingUtils.ParsePingLocationString(b, out SteamNetworkPingLocation_t second)) return -1;
            int ping = SteamNetworkingUtils.EstimatePingTimeBetweenTwoLocations(ref first, ref second);
            return ping >= 0 ? ping : -1;
        }

        // The people in the room who could run it, with what the host needs to
        // rank them. Our own fitness is the live value; everyone else's is what
        // they published.
        List<HostElection.Candidate> Candidates(HashSet<ulong> present)
        {
            var humans = present.Where(id => !BotIdentity.IsBot(id) && (id == Self || Roster.ContainsKey(id))).OrderBy(id => id).ToList();
            var locations = humans.ToDictionary(id => id, id => id == Self ? LocalPingLocation() : MemberData(Match, id, "loc"));
            var list = new List<HostElection.Candidate>();
            foreach (ulong id in humans)
            {
                int sum = 0, known = 0;
                foreach (ulong other in humans)
                {
                    if (other == id) continue;
                    int ping = PingBetween(locations[id], locations[other]);
                    if (ping >= 0) { sum += ping; known++; }
                }
                list.Add(new HostElection.Candidate
                {
                    Id = id,
                    Fitness = id == Self ? CurrentFitness : ParseInt(MemberData(Match, id, "fit")),
                    BaseFitness = id == Self ? LocalFitness : ParseInt(MemberData(Match, id, "base")),
                    PingMs = known > 0 ? sum / known : -1
                });
            }
            return list;
        }
        static HostElection.Candidate Pick(List<HostElection.Candidate> candidates, ulong id) =>
            candidates.FirstOrDefault(c => c.Id == id);

        // Who takes over from `leaving`. Every PC must reach the same answer from
        // the same room data, so our own fitness here is the published one.
        ulong SuccessorOf(ulong leaving, HashSet<ulong> present)
        {
            var humans = present.Where(id => !BotIdentity.IsBot(id) && Roster.ContainsKey(id)).ToList();
            var fallback = humans.Select(id => new HostElection.Candidate
            { Id = id, Fitness = id == Self ? publishedFit : ParseInt(MemberData(Match, id, "fit")), PingMs = -1 });
            return HostElection.Successor(HostElection.Decode(Data(Match, "successors")), humans, leaving, fallback);
        }

        public void Initialize()
        {
            try
            {
                if (!Packsize.Test() || !DllCheck.Test() || !SteamAPI.Init())
                { Error = "Steam 초기화 실패. Steam을 실행하고 로그인한 뒤 다시 연결하세요. (개발용 App ID 480)"; return; }
                Online = true; Self = SteamUser.GetSteamID().m_SteamID;
                SteamNetworkingUtils.InitRelayNetworkAccess();
                callbacks.Add(Callback<LobbyChatMsg_t>.Create(OnChat));
                callbacks.Add(Callback<GameLobbyJoinRequested_t>.Create(c => JoinParty(c.m_steamIDLobby.m_SteamID)));
                // "Join game" on a friend in the Steam friends list, while we are running.
                callbacks.Add(Callback<GameRichPresenceJoinRequested_t>.Create(c =>
                { ulong target = ParseConnect(c.m_rgchConnect); if (target != 0 && target != Party) JoinParty(target); }));
                ulong lobby = ParseConnect(string.Join(" ", Environment.GetCommandLineArgs()));
                if (lobby != 0) { JoinParty(lobby); return; }
                CreateParty();
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is BadImageFormatException || ex is EntryPointNotFoundException)
            { Error = "Steamworks 플러그인을 불러오지 못했습니다: " + ex.Message; }
        }

        // Steam has to be running before Play starts. Without this the only way out
        // of a failed init was to stop and re-enter Play mode.
        public void Retry()
        {
            if (disposed || Online) return;
            Error = ""; Status = "Steam 연결 중...";
            Initialize();
        }

        public void Tick()
        {
            if (!Online || disposed) return;
            SteamAPI.RunCallbacks();
            float now = Time.realtimeSinceStartup;
            if (pending && now > deadline) { generation++; pending = false; Fail("Steam 응답이 없습니다. 다시 시도하세요."); }
            if (now < nextPoll) return;
            nextPoll = now + .25f;
            if (!SteamUser.BLoggedOn()) { Cancel(); Error = "Steam 연결이 끊겼습니다. 다시 로그인한 뒤 Play를 재시작하세요."; return; }
            PollParty();
            if (Match != 0) PollMatch(now);
            UpdatePresence();
            if (Searching && Match == 0 && !pending && now >= nextSearch) Search();
            // A host with only its own party periodically tries an older room. This
            // converges simultaneous room creation without dismantling occupied rooms.
            if (IsHost && Searching && !Started && !privateRoom && reservations.Groups.Count == 1 && !pending && now > mergeAt)
            { mergeAt = now + 8; Search(true); }
        }

        public void CreateParty()
        {
            if (!Online || Busy) return;
            LeavePartyInternal();
            CreateLobby(false);
        }
        void CreateLobby(bool match)
        {
            int op = ++generation; pending = true; deadline = Time.realtimeSinceStartup + 20;
            var call = CallResult<LobbyCreated_t>.Create(); calls.Add(call);
            call.Set(SteamMatchmaking.CreateLobby(match && !privateRoom ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypePrivate, match ? 12 : 6), (c, failed) =>
            {
                calls.Remove(call); call.Dispose();
                if (disposed || op != generation) { if (!failed && c.m_eResult == EResult.k_EResultOK) SteamMatchmaking.LeaveLobby(Id(c.m_ulSteamIDLobby)); return; }
                pending = false;
                if (failed || c.m_eResult != EResult.k_EResultOK) { Fail("Steam 방을 만들지 못했습니다: " + c.m_eResult); return; }
                ulong lobby = c.m_ulSteamIDLobby;
                Set(lobby, "protocol", Protocol); Set(lobby, "build", Build); Set(lobby, "kind", match ? "match" : "party");
                if (!match)
                { Party = lobby; partyOwner = Self; Set(Party, "route", "idle"); Set(Party, "mode", GameModes.Default.Key); Status = "파티 준비 완료. 친구를 초대하거나 매칭을 시작하세요."; Error = carriedError ?? ""; carriedError = null; }
                else
                {
                    Match = lobby; Host = Self; admitted = true; Started = false; ResetHostState();
                    reservations.Clear(); reservations.Reserve(Self, Party, ticket, queuedMembers, Time.realtimeSinceStartup, out _);
                    Set(Match, "host", Self.ToString()); Set(Match, "phase", "waiting"); Set(Match, "mode", queuedMode);
                    // A waiting room takes players until it starts; after that, only
                    // into the seats of players who left (Backfill).
                    Set(Match, "private", privateRoom ? "1" : "0"); Set(Match, "open", "1"); PublishRoster();
                    Set(Party, "route", Match.ToString()); mergeAt = Time.realtimeSinceStartup + 6;
                    SessionChanged?.Invoke();
                }
            });
        }

        // Only the leader picks the mode, and only while the party is idle: the
        // mode is frozen with the queue for the whole search. Any mode this build
        // knows is accepted; whether it can be played yet is the lobby's call.
        public bool SetMode(string key)
        {
            if (!Online || !IsLeader || Busy || GameModes.Find(key) == null) return false;
            Set(Party, "mode", key);
            return true;
        }

        // Only the leader decides the party's bot count, and only while the party
        // is idle: queuedMembers is frozen for the whole search.
        public void SetPartyBots(int count)
        {
            if (!Online || !IsLeader || Busy) return;
            PartyBots = Math.Max(0, Math.Min(count, MaxPartyBots));
        }

        // Host-side filler so one machine can exercise a full 12-pawn room.
        public void FillRoomWithBots()
        {
            if (!CanUseRoomBots) return;
            for (int guard = 0; guard < 12 && reservations.Count < 12; guard++)
            {
                int room = 12 - reservations.Count;
                int free = Math.Max(TeamReservations.TeamSize - reservations.Used(0), TeamReservations.TeamSize - reservations.Used(1));
                int size = Math.Min(Math.Min(room, free), TeamReservations.TeamSize);
                if (size <= 0) break;
                // Offset past the leader's own party bots so identifiers never repeat.
                if (!reservations.ReserveBots(Self, BotIdentity.Fill(Self, size, NextFillerIndex()), Time.realtimeSinceStartup, out _)) break;
            }
            PublishRoster();
        }
        public void ClearRoomBots()
        {
            if (!IsHost || Started) return;
            reservations.RemoveFillerBots(); PublishRoster();
        }
        public bool AddTestBot(int team)
        {
            if (!CanAddTestBot(team)) return false;
            if (!reservations.ReserveBots(Self, BotIdentity.Fill(Self, 1, NextFillerIndex()), Time.realtimeSinceStartup, team, out var group)) return false;
            // Filler bots have no asynchronous Steam arrival to wait for.
            group.Committed = true;
            PublishRoster();
            return true;
        }
        public bool RemoveTestBot(int team)
        {
            if (!CanEditTestBots || team < 0 || team > 1 || !reservations.RemoveFillerBot(team)) return false;
            PublishRoster();
            return true;
        }
        int NextFillerIndex()
        {
            int next = BotIdentity.MaxPerParty;
            foreach (var g in reservations.Groups)
                foreach (ulong id in g.Members)
                    if (BotIdentity.IsBot(id) && BotIdentity.Index(id) >= next) next = BotIdentity.Index(id) + 1;
            return next;
        }

        // Kept as an escape hatch. The in-game friend list is the normal path: the
        // overlay renders at its own resolution and hides presence behind a search box.
        public void Invite()
        {
            if (Party != 0 && !Busy) SteamFriends.ActivateGameOverlayInviteDialog(Id(Party));
        }

        // A snapshot of the friend list, most invitable first. Steam is polled here
        // rather than cached: presence changes while the panel is open.
        public List<FriendInfo> Friends()
        {
            var list = new List<FriendInfo>();
            if (!Online) return list;
            uint appId = SteamUtils.GetAppID().m_AppId;
            int count = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < count; i++)
            {
                var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                if (id.m_SteamID == 0 || id.m_SteamID == Self) continue;
                var state = SteamFriends.GetFriendPersonaState(id);
                // The app id is the low 24 bits of a game id; comparing it avoids
                // depending on the exact shape of CGameID.AppID().
                bool here = SteamFriends.GetFriendGamePlayed(id, out FriendGameInfo_t game) &&
                            (uint)(game.m_gameID.m_GameID & 0xFFFFFF) == appId;
                list.Add(new FriendInfo
                {
                    Id = id.m_SteamID,
                    Name = SteamFriends.GetFriendPersonaName(id),
                    // Our own Rich Presence line, only meaningful inside this game.
                    Detail = here ? SteamFriends.GetFriendRichPresence(id, "status") : "",
                    Presence = here ? FriendPresence.InGame
                             : state == EPersonaState.k_EPersonaStateOffline ? FriendPresence.Offline
                             : state == EPersonaState.k_EPersonaStateOnline ? FriendPresence.Online
                             : FriendPresence.Away
                });
            }
            list.Sort((a, b) =>
            {
                int byPresence = b.Presence.CompareTo(a.Presence);
                return byPresence != 0 ? byPresence
                     : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        // Sends the Steam invite straight to one friend, no overlay. The receiving
        // client already handles it through GameLobbyJoinRequested_t.
        public bool InviteToParty(ulong friend)
        {
            if (!Online || Party == 0 || friend == 0 || Busy) return false;
            return SteamMatchmaking.InviteUserToLobby(Id(Party), Id(friend));
        }
        public void JoinParty(ulong lobby)
        {
            if (!Online || Busy || lobby == 0) { Error = "다른 파티에 들어가려면 먼저 매칭/경기를 취소하세요."; return; }
            LeavePartyInternal();
            Join(lobby, true);
        }
        void Join(ulong lobby, bool party)
        {
            if (!party && IsLeader) ticket = Guid.NewGuid().ToString("N");
            int op = ++generation; pending = true; deadline = Time.realtimeSinceStartup + 20;
            var call = CallResult<LobbyEnter_t>.Create(); calls.Add(call);
            call.Set(SteamMatchmaking.JoinLobby(Id(lobby)), (c, failed) =>
            {
                calls.Remove(call); call.Dispose();
                bool success = !failed && c.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
                if (disposed || op != generation) { if (success) SteamMatchmaking.LeaveLobby(Id(lobby)); return; }
                pending = false;
                string problem = success ? Incompatibility(lobby, party ? "party" : "match") : "방이 가득 찼거나 닫혔습니다.";
                if (problem != null)
                {
                    if (success) SteamMatchmaking.LeaveLobby(Id(lobby));
                    if (!party && IsLeader && Searching && !privateRoom) { nextSearch = Time.realtimeSinceStartup + UnityEngine.Random.Range(1f, 3f); TryCandidate(); }
                    // The old party is already left; a fresh solo party keeps the
                    // player usable, and the reason survives its creation.
                    else if (party) { Error = carriedError = problem; CreateParty(); }
                    else Fail(problem);
                    return;
                }
                if (party)
                {
                    Party = lobby; partyOwner = Owner(Party); cancelledFollower = false;
                    Status = "파티에 참가했습니다."; Error = "";
                }
                else
                {
                    Match = lobby; ulong.TryParse(Data(Match, "host"), out ulong host); Host = host; ResetHostState();
                    // Joining a started match for an empty seat: its host record is
                    // ours from the start, so it is not reported as a change of host.
                    if (Live(Match)) epoch = ParseInt(Data(Match, "epoch"));
                    admitted = seenRoster = false; Started = false; privateRoom = Data(Match, "private") == "1";
                    deadline = Time.realtimeSinceStartup + 28; nextRequest = 0;
                    SessionChanged?.Invoke();
                }
            });
        }

        public void FindMatch(bool privateTest = false)
        {
            if (!Online || !IsLeader || Busy) return;
            if (!privateTest && BotsBlockPublicMatch)
            { Error = "봇이 있는 파티는 공개 매칭에 들어갈 수 없습니다. 봇을 빼거나 테스트 방을 만드세요."; return; }
            Error = ""; Searching = true; privateRoom = privateTest; emptySearches = 0;
            FreezeQueue(); ticket = Guid.NewGuid().ToString("N");
            SteamMatchmaking.SetLobbyJoinable(Id(Party), false); Set(Party, "route", "search");
            Status = privateTest ? "비공개 테스트 방을 만드는 중..." : "파티 전체가 들어갈 자리를 찾는 중...";
            if (privateTest) CreateLobby(true); else { nextSearch = 0; Search(); }
        }
        public void JoinPrivateMatch(ulong lobby)
        {
            if (!IsLeader || Busy || lobby == 0) return;
            Searching = true; privateRoom = true; FreezeQueue(); ticket = Guid.NewGuid().ToString("N");
            SteamMatchmaking.SetLobbyJoinable(Id(Party), false); Set(Party, "route", "search"); Join(lobby, false);
        }
        // The queue is fixed for the whole search: humans for change detection,
        // humans + bots for the reservation the host has to honour.
        void FreezeQueue()
        {
            queuedHumans = PartyMembers;
            queuedMode = PartyMode.Key;
            PartyBots = Math.Min(PartyBots, Math.Max(0, TeamReservations.TeamSize - queuedHumans.Length));
            queuedMembers = queuedHumans.Concat(BotIdentity.Fill(Self, PartyBots)).ToArray();
            cancelBaseline.Clear();
            foreach (ulong id in queuedHumans) cancelBaseline[id] = SteamMatchmaking.GetLobbyMemberData(Id(Party), Id(id), "cancel");
        }
        void Search(bool merge = false)
        {
            if (!IsLeader || pending) return;
            int op = ++generation; pending = true; deadline = Time.realtimeSinceStartup + 20;
            SteamMatchmaking.AddRequestLobbyListStringFilter("protocol", Protocol, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("build", Build, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("kind", "match", ELobbyComparison.k_ELobbyComparisonEqual);
            // Waiting rooms, and started matches with the empty seat of a player who left.
            SteamMatchmaking.AddRequestLobbyListStringFilter("open", "1", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("private", "0", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("mode", queuedMode, ELobbyComparison.k_ELobbyComparisonEqual);
            // Steam counts real lobby members; bots only consume our own reservation
            // slots, which the free0/free1 check below enforces.
            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(queuedHumans.Length);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterDefault);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
            var call = CallResult<LobbyMatchList_t>.Create(); calls.Add(call);
            call.Set(SteamMatchmaking.RequestLobbyList(), (c, failed) =>
            {
                calls.Remove(call); call.Dispose();
                if (disposed || op != generation) return;
                pending = false;
                if (failed) { nextSearch = Time.realtimeSinceStartup + 4; Status = "Steam 검색 실패. 다시 시도합니다..."; return; }
                var found = new List<ulong>();
                for (int i = 0; i < c.m_nLobbiesMatching; i++)
                {
                    ulong l = SteamMatchmaking.GetLobbyByIndex(i).m_SteamID;
                    // Waiting rooms merge only into older ones, so two can never swap.
                    // A started match's empty seat is worth leaving any waiting room for.
                    if (l == Match || (merge && !Live(l) && l >= Match)) continue;
                    if (int.TryParse(Data(l, "free0"), out int a) && int.TryParse(Data(l, "free1"), out int b) && Math.Max(a, b) >= queuedMembers.Length) found.Add(l);
                }
                if (merge && (found.Count == 0 || !IsHost || reservations.Groups.Count != 1 || Started)) return;
                // Seats of players who left come first: that match is short-handed now.
                candidates.Clear(); foreach (ulong l in found.OrderBy(x => Live(x) ? 0 : 1).ThenBy(x => x)) candidates.Enqueue(l);
                if (merge) { LeaveMatchInternal(); Set(Party, "route", "search"); }
                if (candidates.Count > 0) TryCandidate();
                else if (++emptySearches >= 2) CreateLobby(true);
                else nextSearch = Time.realtimeSinceStartup + UnityEngine.Random.Range(1f, 3f);
            });
        }
        void TryCandidate()
        {
            if (Match != 0 || pending || !Searching) return;
            if (candidates.Count > 0) Join(candidates.Dequeue(), false);
            else nextSearch = Time.realtimeSinceStartup + UnityEngine.Random.Range(1f, 3f);
        }

        void PollParty()
        {
            if (Party == 0) return;
            ulong owner = Owner(Party);
            if (owner != partyOwner)
            {
                partyOwner = owner; Cancel(); Error = "파티장이 나갔습니다. 매칭이 취소되었습니다."; return;
            }
            if (IsLeader && Searching)
            {
                if (!queuedHumans.SequenceEqual(PartyMembers)) { Cancel(); Error = "파티 구성이 바뀌어 매칭을 취소했습니다."; return; }
                foreach (var pair in cancelBaseline)
                    if (pair.Value != SteamMatchmaking.GetLobbyMemberData(Id(Party), Id(pair.Key), "cancel")) { Cancel(); Status = "파티원이 취소했습니다."; return; }
            }
            if (IsLeader) return;
            string route = Route(Party);
            if (route == "idle")
            {
                if (Match != 0 || pending) { generation++; pending = false; LeaveMatchInternal(); }
                cancelledFollower = false; Status = "파티 준비 완료. 파티장을 기다리는 중."; return;
            }
            if (cancelledFollower) return;
            if (route == "search")
            {
                if (pending) { generation++; pending = false; }
                if (Match != 0) LeaveMatchInternal(); Status = "파티장이 매칭을 찾는 중..."; return;
            }
            if (ulong.TryParse(route, out ulong target) && target != Match && !pending)
            { LeaveMatchInternal(); Join(target, false); }
        }

        void PollMatch(float now)
        {
            string phase = Data(Match, "phase");
            // Before the start the room's creator is the only possible host: it
            // holds the reservations, which cannot move, so its leaving still ends
            // the waiting room. From the start on the host role moves instead
            // (FollowHost), so a missing or silent host no longer ends the match.
            bool live = Started || phase == "playing";
            if (phase == "closed" || (!live && (Host == 0 || Owner(Match) != Host)))
            {
                // Party-route and match-close notifications can arrive in either order
                // while the host merges an otherwise empty waiting room.
                if (invalidHostSince < 0) invalidHostSince = now;
                if (now - invalidHostSince > 2) Fail("방장이 나가 경기가 끝났습니다. 파티로 돌아갑니다.");
                return;
            }
            invalidHostSince = -1;
            var present = Members(Match);
            PublishFitness(now);
            if (live && !FollowHost(present, now)) return;
            // Players seated before this poll: only they hear about newcomers, not a
            // client that is just seeing the start (the last arrivals come with it).
            bool wasStarted = Started;
            // Only a seated player is in the match: one who came for an empty seat
            // stays in the lobby until the host seats it. A client promoted to host
            // must not fall into the waiting-room branch below: it has no
            // reservations and would end the match, so it is in the match too.
            if (live && (IsHost || Roster.ContainsKey(Self))) Started = true;
            if (Started) PublishLoad();
            if (IsHost)
            {
                if (!Started)
                {
                    foreach (var removed in reservations.Reconcile(present, now)) Set(Match, "grant_" + removed.Leader, removed.Ticket + "|expired");
                    if (reservations.Find(Self) == null) { Fail("예약 시간 안에 파티가 모두 입장하지 못했습니다."); return; }
                    PublishRoster();
                    if (!privateRoom && reservations.Ready) StartGame();
                }
                else
                {
                    // A player who left leaves an empty seat on its team.
                    foreach (ulong id in Roster.Keys.ToArray())
                        if (!present.Contains(id) && !BotIdentity.IsBot(id)) Roster.Remove(id);
                    // Only the room's owner can write the seats (HOST.md).
                    if (Owner(Match) == Self) Refill(present);
                    PublishMembers();
                    PublishSuccessors(present, now);
                    CheckStruggle(present, now);
                }
            }
            else
            {
                if (IsLeader && !admitted)
                {
                    string grant = Data(Match, "grant_" + Self);
                    if (grant == ticket + "|ok") { admitted = true; Set(Party, "route", Match.ToString()); }
                    else if (grant.StartsWith(ticket + "|", StringComparison.Ordinal))
                    { RetryAdmission("파티 전체가 들어갈 자리가 없습니다. 다시 찾는 중..."); return; }
                    else if (now >= nextRequest)
                    {
                        nextRequest = now + 2;
                        SendChat(new Request { kind = "reserve", ticket = ticket, party = Party, members = queuedMembers });
                    }
                }
                var before = wasStarted ? new HashSet<ulong>(Roster.Keys) : null;
                ReadRoster();
                if (before != null) NoteArrivals(Roster.Keys.Where(id => !before.Contains(id)));
                if (Roster.ContainsKey(Self))
                {
                    admitted = true; seenRoster = true;
                    // Steam can deliver roster and grant metadata in either order.
                    if (IsLeader && Route(Party) != Match.ToString()) Set(Party, "route", Match.ToString());
                }
                if (!seenRoster && now > deadline) { RetryAdmission("입장 승인이 시간 초과되었습니다."); return; }
                if (seenRoster && !Roster.ContainsKey(Self)) { Fail("예약이 해제되었거나 파티원 연결이 끊겼습니다."); return; }
                Started = phase == "playing" && Roster.ContainsKey(Self);
            }
            int empty = EmptySeats;
            Status = now < noteUntil ? Note
                   : Started ? (empty > 0 ? $"경기 중 · 빈자리 {empty}개 · 매칭 중인 플레이어가 들어올 수 있습니다." : "경기 시작! WASD 이동, Space 점프.")
                   : live ? "진행 중인 경기의 빈자리에 들어가는 중..."
                   : $"대기실 {Roster.Count}/12명. 대기 중에도 움직일 수 있습니다.";
        }

        // ---- Host election and migration (Docs/Network/HOST.md) ----

        // Our fitness and ping location, as member data of the match room, so the
        // host can rank everyone. Republished only on a real change.
        void PublishFitness(float now)
        {
            int current = CurrentFitness;
            if (current > 0 && now >= nextFitPublish &&
                (publishedFit <= 0 || Math.Abs(current - publishedFit) > publishedFit * .15))
            {
                SteamMatchmaking.SetLobbyMemberData(Id(Match), "fit", current.ToString());
                publishedFit = current; nextFitPublish = now + 2;
            }
            if (LocalFitness > 0 && LocalFitness != publishedBase)
            {
                SteamMatchmaking.SetLobbyMemberData(Id(Match), "base", LocalFitness.ToString());
                publishedBase = LocalFitness;
            }
            if (publishedLoc == "")
            {
                string location = LocalPingLocation();
                if (location != "") { SteamMatchmaking.SetLobbyMemberData(Id(Match), "loc", location); publishedLoc = location; }
            }
        }

        // Our loading progress as member data "load", written only when it moved
        // enough to matter (MatchStart.WorthPublishing).
        void PublishLoad()
        {
            if (!MatchStart.WorthPublishing(loadPercent, publishedLoad)) return;
            SteamMatchmaking.SetLobbyMemberData(Id(Match), "load", MatchStart.Format(loadPercent));
            publishedLoad = loadPercent;
        }

        // Keeps Host on the newest host record once the match is live, replaces
        // a host that is gone or silent, and moves the lobby's ownership to the
        // host. False when the match had to end.
        bool FollowHost(HashSet<ulong> present, float now)
        {
            if (hostSince < 0) hostSince = now;
            // 1. The newest record wins: the room's own ("host", "epoch", written by
            //    the owner) or a claim by a player still here. Equal epochs go to
            //    the room's record, then to the lower Steam ID, so two successors
            //    that both stepped in settle on one.
            ulong recordHost = ParseId(Data(Match, "host"));
            int recordEpoch = ParseInt(Data(Match, "epoch"));
            ulong newest = present.Contains(recordHost) || recordHost == Self ? recordHost : 0;
            int newestEpoch = newest != 0 ? recordEpoch : 0;
            foreach (ulong id in present.OrderBy(id => id))
            {
                int claim = ParseInt(MemberData(Match, id, "claim"));
                if (claim <= 0) continue;
                if (claim > newestEpoch || (claim == newestEpoch && newest != recordHost && id < newest)) { newest = id; newestEpoch = claim; }
            }
            if (newest != 0 && (newestEpoch > epoch || (newestEpoch == epoch && newest != Host)))
                ChangeHost(newest, newestEpoch, newest == Self ? "이 PC가 방장을 맡았습니다." : $"방장이 {Name(newest)} 님으로 바뀌었습니다.");

            // 2. A host gone from the room, or silent past the grace period, is
            //    replaced by its successor. Only the successor acts; everyone else
            //    follows its claim through step 1 on a later poll. A player still
            //    waiting for an empty seat is not in the match yet and stays out.
            if (!IsHost && Roster.ContainsKey(Self))
            {
                bool gone = !present.Contains(Host);
                bool silent = hostSilence >= MigrateAfterSilence && now - hostSince >= MigrationGrace;
                if (gone || silent)
                {
                    ulong next = SuccessorOf(Host, present);
                    if (next == Self) Promote(present);
                    else if (next == 0 || (gone && orphanSince >= 0 && now - orphanSince > OrphanTimeout))
                    { Fail("방장이 나가 경기가 끝났습니다. 파티로 돌아갑니다."); return false; }
                    else if (orphanSince < 0) orphanSince = now;
                }
                else orphanSince = -1;
            }

            // 3. Only the owner can write the room's data, and Steam hands a
            //    departed owner's lobby to anyone, so ownership follows the host.
            if (Host != Self && Owner(Match) == Self && present.Contains(Host) && now >= nextOwnerFix)
            { nextOwnerFix = now + 1; SteamMatchmaking.SetLobbyOwner(Id(Match), Id(Host)); }
            // 4. A host that owns the room keeps its record current.
            if (IsHost && Owner(Match) == Self && (recordHost != Self || recordEpoch != epoch))
            { Set(Match, "host", Self.ToString()); Set(Match, "epoch", epoch.ToString()); }
            return true;
        }

        // The successor steps in: one epoch above anything it can see, announced
        // as a claim until it owns the room and can write the record itself.
        void Promote(HashSet<ulong> present)
        {
            int top = Math.Max(epoch, ParseInt(Data(Match, "epoch")));
            foreach (ulong id in present) top = Math.Max(top, ParseInt(MemberData(Match, id, "claim")));
            int claim = top + 1;
            SteamMatchmaking.SetLobbyMemberData(Id(Match), "claim", claim.ToString());
            ChangeHost(Self, claim, "방장 연결이 끊겨 이 PC가 방장을 이어받았습니다.");
            if (Owner(Match) == Self) { Set(Match, "host", Self.ToString()); Set(Match, "epoch", claim.ToString()); }
        }

        // The current host gives the role away: at the start, when it is too
        // slow, or when it leaves a live match.
        void HandOff(ulong next, IEnumerable<ulong> ranked, string note, bool notify = true)
        {
            int nextEpoch = epoch + 1;
            Set(Match, "host", next.ToString()); Set(Match, "epoch", nextEpoch.ToString());
            Set(Match, "successors", HostElection.Encode(ranked.Where(id => id != next)));
            SteamMatchmaking.SetLobbyOwner(Id(Match), Id(next));
            ChangeHost(next, nextEpoch, note, notify);
        }

        void ChangeHost(ulong next, int newEpoch, string note, bool notify = true)
        {
            ulong previous = Host;
            Host = next; epoch = newEpoch; hostSilence = 0; orphanSince = -1;
            float now = Time.realtimeSinceStartup;
            hostSince = now;
            if (previous == next) return;
            lastHandoff = now;
            Note = note; noteUntil = now + 6;
            // The next host reads the held seats and the room's state afresh.
            heldLoaded = false; joinable = -1;
            if (notify) HostChanged?.Invoke(previous, next);
        }

        // Every 2 seconds the host publishes the order it would hand over in, so
        // a crash (which leaves no time to decide) still has an agreed successor.
        void PublishSuccessors(HashSet<ulong> present, float now)
        {
            if (now < nextSuccessors || Owner(Match) != Self) return;
            nextSuccessors = now + 2;
            string text = HostElection.Encode(HostElection.Rank(Candidates(present).Where(c => c.Id != Self)));
            if (text != Data(Match, "successors")) Set(Match, "successors", text);
        }

        // A host that has stayed slow for a while (FrameMonitor) hands the match
        // to a player with a clearly stronger machine instead of making everyone
        // lag (HostElection.Takeover says why machines, not frame rates).
        void CheckStruggle(HashSet<ulong> present, float now)
        {
            if (Owner(Match) != Self || now - lastHandoff < StruggleCooldown || !frames.Struggling(now)) return;
            var candidates = Candidates(present);
            ulong next = HostElection.Takeover(Pick(candidates, Self), candidates.Where(c => c.Id != Self).ToList(), HostElection.StruggleMargin);
            if (next != 0) HandOff(next, HostElection.Rank(candidates), $"방장 PC가 느려져 {Name(next)} 님이 방장을 이어받습니다.");
        }

        void ResetHostState()
        {
            epoch = 0; publishedFit = publishedBase = 0; publishedLoc = ""; hostSilence = 0; hostSince = -1; orphanSince = -1;
            nextFitPublish = nextSuccessors = nextOwnerFix = noteUntil = 0; lastHandoff = -1000; Note = "";
            held.Clear(); heldLoaded = false; joinable = -1;
        }

        // ---- Empty seats of a started match (Backfill) ----

        // The host's round in a started match: held seats whose player arrived are
        // taken, expired ones are freed, and the room says what it offers.
        void Refill(HashSet<ulong> present)
        {
            LoadHeld();
            double now = ServerNow();
            var arrived = new List<ulong>();
            for (int i = held.Count - 1; i >= 0; i--)
            {
                var seat = held[i];
                if (present.Contains(seat.Id))
                { Roster[seat.Id] = PawnMotor.Spawn(seat.Id, seat.Team, seat.Slot); arrived.Add(seat.Id); held.RemoveAt(i); }
                else if (now > seat.Until) held.RemoveAt(i);
            }
            NoteArrivals(arrived);
            PublishSeats();
        }

        // Empty seats per team, whether searches may find the room ("open"), the
        // held seats, and whether Steam lets anyone in: while seats are offered, or
        // a party's members are still on their way to theirs. Written on change only.
        void PublishSeats()
        {
            var capacity = Backfill.DecodeCapacity(Data(Match, "seats"));
            var seats = Seats();
            int free0 = Backfill.Free(capacity, seats, 0), free1 = Backfill.Free(capacity, seats, 1);
            bool open = free0 + free1 > 0 && Backfill.Open(StartAt, ServerNow());
            SetChanged("free0", free0.ToString()); SetChanged("free1", free1.ToString());
            SetChanged("open", open ? "1" : "0"); SetChanged("held", Backfill.EncodeHeld(held));
            int door = open || held.Count > 0 ? 1 : 0;
            if (joinable != door) { SteamMatchmaking.SetLobbyJoinable(Id(Match), door == 1); joinable = door; }
        }
        void SetChanged(string key, string value) { if (Data(Match, key) != value) Set(Match, key, value); }

        // A new host takes over the seats its predecessor held (rule 15: nothing
        // of the match lives on the host PC alone).
        void LoadHeld()
        {
            if (heldLoaded) return;
            heldLoaded = true;
            held.Clear(); held.AddRange(Backfill.DecodeHeld(Data(Match, "held")));
        }

        // Everyone seated plus the seats held for players on their way.
        List<Backfill.Seat> Seats()
        {
            var seats = Roster.Values.Select(p => new Backfill.Seat { Id = p.Id, Team = p.Team, Slot = p.Slot }).ToList();
            seats.AddRange(held);
            return seats;
        }

        // A request for the empty seats of a started match: the whole party on the
        // team that lost players, up to the size that team started with. Members
        // already here sit down at once; the rest keep their seats for HoldSeconds.
        void SeatLate(ulong sender, Request request)
        {
            LoadHeld();
            string grant = "grant_" + sender;
            // The same request again while its answer is on the way.
            if (Roster.ContainsKey(sender) || held.Exists(s => s.Id == sender)) { Set(Match, grant, request.ticket + "|ok"); return; }
            int team; int[] slots;
            if (!TeamReservations.ValidRequest(sender, request.party, request.ticket, request.members) ||
                request.members.Any(id => Roster.ContainsKey(id) || held.Exists(s => s.Id == id)) ||
                !Backfill.Open(StartAt, ServerNow()) ||
                !Backfill.Place(Backfill.DecodeCapacity(Data(Match, "seats")), Seats(), request.members.Length, out team, out slots))
            { Set(Match, grant, request.ticket + "|full"); return; }
            var present = Members(Match);
            var seated = new List<ulong>();
            double until = ServerNow() + Backfill.HoldSeconds;
            for (int i = 0; i < request.members.Length; i++)
            {
                ulong id = request.members[i];
                // Bots never enter the Steam lobby, so they are always here.
                if (present.Contains(id) || BotIdentity.IsBot(id)) { Roster[id] = PawnMotor.Spawn(id, team, slots[i]); seated.Add(id); }
                else held.Add(new Backfill.Seat { Id = id, Team = team, Slot = slots[i], Until = until });
            }
            Set(Match, grant, request.ticket + "|ok");
            NoteArrivals(seated);
            PublishMembers();
            PublishSeats();
        }

        // "○○ 님이 빈자리를 채웠습니다" for players seated in a started match.
        void NoteArrivals(IEnumerable<ulong> ids)
        {
            var list = ids.Where(id => id != Self).ToList();
            if (list.Count == 0) return;
            Note = list.Count == 1 ? $"{Name(list[0])} 님이 빈자리를 채웠습니다." : $"{list.Count}명이 빈자리를 채웠습니다.";
            noteUntil = Time.realtimeSinceStartup + 6;
        }
        void RetryAdmission(string message)
        {
            if (IsLeader && Searching && !privateRoom)
            { LeaveMatchInternal(); Set(Party, "route", "search"); Status = message; nextSearch = Time.realtimeSinceStartup + UnityEngine.Random.Range(1f, 3f); }
            else Fail(message);
        }
        void SendChat(Request request)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
            if (Match != 0 && bytes.Length <= chatBuffer.Length) SteamMatchmaking.SendLobbyChatMsg(Id(Match), bytes, bytes.Length);
        }
        void OnChat(LobbyChatMsg_t c)
        {
            if (!IsHost || c.m_ulSteamIDLobby != Match) return;
            // A started match only takes requests for its empty seats, which only the
            // room's owner can grant. A host still waiting for ownership (HOST.md)
            // leaves them to the joiner's next try, two seconds later.
            if (Started && Owner(Match) != Self) return;
            int length = SteamMatchmaking.GetLobbyChatEntry(Id(Match), (int)c.m_iChatID, out CSteamID sender, chatBuffer, chatBuffer.Length, out EChatEntryType type);
            if (length <= 0 || length >= chatBuffer.Length || type != EChatEntryType.k_EChatEntryTypeChatMsg || !Members(Match).Contains(sender.m_SteamID)) return;
            Request request;
            try { request = JsonUtility.FromJson<Request>(Encoding.UTF8.GetString(chatBuffer, 0, length)); }
            catch (ArgumentException) { return; }
            if (request == null || request.kind != "reserve" || string.IsNullOrEmpty(request.ticket) || request.ticket.Length > 64) return;
            // The host enforces M6 too, whatever the joining client decided.
            if (!privateRoom && !AllowPublicBots && request.members != null && request.members.Any(BotIdentity.IsBot))
            { Set(Match, "grant_" + sender.m_SteamID, request.ticket + "|bots"); return; }
            if (Started) { SeatLate(sender.m_SteamID, request); return; }
            bool ok = reservations.Reserve(sender.m_SteamID, request.party, request.ticket, request.members, Time.realtimeSinceStartup, out _);
            Set(Match, "grant_" + sender.m_SteamID, request.ticket + (ok ? "|ok" : "|full"));
            if (ok) PublishRoster();
        }
        void PublishRoster()
        {
            Roster.Clear(); var present = Members(Match); int[] slots = { 0, 0 };
            foreach (var g in reservations.Groups)
                foreach (ulong id in g.Members)
                {
                    int slot = slots[g.Team]++;
                    if (present.Contains(id) || BotIdentity.IsBot(id)) Roster[id] = PawnMotor.Spawn(id, g.Team, slot);
                }
            Set(Match, "free0", (6 - reservations.Used(0)).ToString()); Set(Match, "free1", (6 - reservations.Used(1)).ToString());
            PublishMembers();
        }
        void PublishMembers()
        {
            Set(Match, "roster", JsonUtility.ToJson(new RosterData { members = Roster.Values.Select(p => new Member { id = p.Id, team = p.Team, slot = p.Slot }).ToArray() }));
        }
        void ReadRoster()
        {
            string data = Data(Match, "roster"); if (string.IsNullOrEmpty(data) || data.Length > 4096) return;
            try
            {
                var parsed = JsonUtility.FromJson<RosterData>(data);
                if (parsed?.members == null || parsed.members.Length > 12 || parsed.members.Select(p => p.id).Distinct().Count() != parsed.members.Length || parsed.members.Any(p => p.id == 0 || p.team < 0 || p.team > 1 || p.slot < 0 || p.slot > 5)) return;
                Roster.Clear(); foreach (var p in parsed.members) Roster[p.id] = PawnMotor.Spawn(p.id, p.team, p.slot);
            }
            catch (ArgumentException) { }
        }
        public void StartGame()
        {
            // A private test needs two pawns; bots count, so one tester plus a bot works.
            if (!CanStartGame) return;
            Started = true;
            // The host role goes to the PC that can carry the match best, if that
            // is clearly not the room's creator. Nothing is simulated yet, so this
            // is the cheapest moment to move it. The record is written before the
            // phase, so a client that sees "playing" already sees the right host,
            // and ownership moves last, because after it only the new host can write.
            var present = Members(Match);
            var candidates = Candidates(present);
            var ranked = HostElection.Rank(candidates);
            ulong next = Self;
            if (ranked.Count > 0 && HostElection.Worth(Pick(candidates, Self), Pick(candidates, ranked[0]), HostElection.StartMargin)) next = ranked[0];
            epoch = 1;
            Set(Match, "host", next.ToString()); Set(Match, "epoch", "1");
            Set(Match, "successors", HostElection.Encode(ranked.Where(id => id != next)));
            // Each team refills to the size it starts with (Backfill). Nobody has
            // left yet, so the room closes to searches and joins for now.
            Set(Match, "seats", Backfill.EncodeCapacity(Roster.Values.Count(p => p.Team == 0), Roster.Values.Count(p => p.Team == 1)));
            PublishSeats();
            Set(Match, "phase", "playing");
            float now = Time.realtimeSinceStartup;
            hostSince = lastHandoff = now;
            if (next == Self) return;
            SteamMatchmaking.SetLobbyOwner(Id(Match), Id(next));
            ChangeHost(next, 1, $"성능이 더 좋은 {Name(next)} 님의 PC가 방장을 맡습니다.");
        }
        public void Cancel()
        {
            if (!Online) return;
            generation++; pending = false; Searching = false; candidates.Clear();
            if (IsLeader) { Set(Party, "route", "idle"); SteamMatchmaking.SetLobbyJoinable(Id(Party), true); }
            else if (Party != 0) { cancelledFollower = true; SteamMatchmaking.SetLobbyMemberData(Id(Party), "cancel", Guid.NewGuid().ToString("N")); }
            LeaveMatchInternal(); Status = "파티로 돌아왔습니다.";
        }
        public void LeaveParty()
        {
            if (!Online) return;
            Cancel(); LeavePartyInternal(); CreateParty();
        }
        void LeavePartyInternal()
        { if (Party != 0) SteamMatchmaking.LeaveLobby(Id(Party)); Party = 0; partyOwner = 0; PartyBots = 0; }
        void LeaveMatchInternal()
        {
            if (Match != 0)
            {
                if (IsHost)
                {
                    // A live match carries on without us: the fittest player left
                    // takes over. A waiting room, or a room with nobody else, closes.
                    ulong next = 0;
                    List<ulong> ranked = null;
                    if (Started)
                    {
                        ranked = HostElection.Rank(Candidates(Members(Match)).Where(c => c.Id != Self));
                        if (ranked.Count > 0) next = ranked[0];
                    }
                    if (next != 0) HandOff(next, ranked, "", notify: false);
                    else { Set(Match, "phase", "closed"); Set(Match, "open", "0"); SteamMatchmaking.SetLobbyJoinable(Id(Match), false); }
                }
                SteamMatchmaking.LeaveLobby(Id(Match));
            }
            Match = Host = 0; Started = admitted = seenRoster = false; invalidHostSince = -1; Roster.Clear(); reservations.Clear();
            loadPercent = 0; publishedLoad = -1;
            ResetHostState();
            SessionChanged?.Invoke();
        }
        void Fail(string error) { Cancel(); Error = error; Status = error; }
        // For the scene flow: leave the match and tell the player why.
        public void Abort(string reason) { if (Online) Fail(reason); }

        // Steam friends see "Join game" on us while our party can take them.
        // A search locks the party lobby, so the offer is withdrawn meanwhile.
        void UpdatePresence()
        {
            string connect = Party != 0 && !Busy ? "+connect_lobby " + Party : "";
            // Friends see this under our name in the lobby's friend panel.
            string mode = (MatchMode ?? PartyMode).Name;
            string status = Match != 0 ? mode + (Started ? " 경기 중" : " 대기실") : Busy ? mode + " 매칭 찾는 중" : $"로비에서 대기 중 · 파티 {PartyMembers.Length}/6";
            string next = connect + "|" + status;
            if (next == presence) return;
            presence = next;
            // An empty value removes the key.
            SteamFriends.SetRichPresence("connect", connect);
            SteamFriends.SetRichPresence("status", status);
        }
        public void Dispose()
        {
            if (disposed) return;
            if (Online) { Cancel(); LeavePartyInternal(); SteamFriends.ClearRichPresence(); }
            disposed = true;
            foreach (var c in callbacks) c.Dispose(); foreach (var c in calls) c.Dispose();
            callbacks.Clear(); calls.Clear();
            if (Online) SteamAPI.Shutdown(); Online = false;
        }
    }
}
