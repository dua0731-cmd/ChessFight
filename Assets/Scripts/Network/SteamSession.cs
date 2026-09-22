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
        public const string Protocol = "chessfight.dua0731.network.v1";
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
        public bool Busy => pending || Searching || Match != 0 || Route(Party) != "idle";
        public string Status { get; private set; } = "Starting Steam...";
        public string Error { get; private set; } = "";
        public readonly Dictionary<ulong, PawnState> Roster = new Dictionary<ulong, PawnState>();
        public event Action SessionChanged;
        readonly TeamReservations reservations = new TeamReservations();
        readonly List<IDisposable> callbacks = new List<IDisposable>();
        readonly List<IDisposable> calls = new List<IDisposable>();
        readonly Queue<ulong> candidates = new Queue<ulong>();
        readonly Dictionary<ulong, string> cancelBaseline = new Dictionary<ulong, string>();
        readonly byte[] chatBuffer = new byte[2048];
        ulong[] queuedMembers = Array.Empty<ulong>();   // humans + declared party bots
        ulong[] queuedHumans = Array.Empty<ulong>();    // humans only, for roster-change detection
        ulong partyOwner;
        string ticket = "";
        bool pending, admitted, seenRoster, privateRoom, cancelledFollower, disposed;
        int generation;
        float nextPoll, nextSearch, deadline, nextRequest, mergeAt, invalidHostSince = -1;
        int emptySearches;

        [Serializable] sealed class Request { public string kind, ticket; public ulong party; public ulong[] members; }
        [Serializable] sealed class RosterData { public Member[] members; }
        [Serializable] sealed class Member { public ulong id; public int team, slot; }

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
        static bool Compatible(ulong lobby, string kind) => Data(lobby, "protocol") == Protocol && Data(lobby, "kind") == kind;
        public bool IsPeer(ulong id) => Match != 0 && (id == Host || Roster.ContainsKey(id));

        public void Initialize()
        {
            try
            {
                if (!Packsize.Test() || !DllCheck.Test() || !SteamAPI.Init())
                { Error = "Steam init failed. Start Steam, sign in, and check steam_appid.txt (480 for development)."; return; }
                Online = true; Self = SteamUser.GetSteamID().m_SteamID;
                SteamNetworkingUtils.InitRelayNetworkAccess();
                callbacks.Add(Callback<LobbyChatMsg_t>.Create(OnChat));
                callbacks.Add(Callback<GameLobbyJoinRequested_t>.Create(c => JoinParty(c.m_steamIDLobby.m_SteamID)));
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                    if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out ulong lobby)) { JoinParty(lobby); return; }
                CreateParty();
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is BadImageFormatException || ex is EntryPointNotFoundException)
            { Error = "Steamworks native plugin unavailable: " + ex.Message; }
        }

        // Steam has to be running before Play starts. Without this the only way out
        // of a failed init was to stop and re-enter Play mode.
        public void Retry()
        {
            if (disposed || Online) return;
            Error = ""; Status = "Starting Steam...";
            Initialize();
        }

        public void Tick()
        {
            if (!Online || disposed) return;
            SteamAPI.RunCallbacks();
            float now = Time.realtimeSinceStartup;
            if (pending && now > deadline) { generation++; pending = false; Fail("Steam request timed out. Try again."); }
            if (now < nextPoll) return;
            nextPoll = now + .25f;
            if (!SteamUser.BLoggedOn()) { Cancel(); Error = "Steam disconnected. Sign in again and restart Play mode."; return; }
            PollParty();
            if (Match != 0) PollMatch(now);
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
                if (failed || c.m_eResult != EResult.k_EResultOK) { Fail("Could not create Steam lobby: " + c.m_eResult); return; }
                ulong lobby = c.m_ulSteamIDLobby;
                Set(lobby, "protocol", Protocol); Set(lobby, "kind", match ? "match" : "party");
                if (!match)
                { Party = lobby; partyOwner = Self; Set(Party, "route", "idle"); Status = "Party ready. Invite friends or find a match."; Error = ""; }
                else
                {
                    Match = lobby; Host = Self; admitted = true; Started = false;
                    reservations.Clear(); reservations.Reserve(Self, Party, ticket, queuedMembers, Time.realtimeSinceStartup, out _);
                    Set(Match, "host", Self.ToString()); Set(Match, "phase", "waiting");
                    Set(Match, "private", privateRoom ? "1" : "0"); PublishRoster();
                    Set(Party, "route", Match.ToString()); mergeAt = Time.realtimeSinceStartup + 6;
                    SessionChanged?.Invoke();
                }
            });
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
            if (!IsHost || Started) return;
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
        int NextFillerIndex()
        {
            int next = BotIdentity.MaxPerParty;
            foreach (var g in reservations.Groups)
                foreach (ulong id in g.Members)
                    if (BotIdentity.IsBot(id) && BotIdentity.Index(id) >= next) next = BotIdentity.Index(id) + 1;
            return next;
        }

        public void Invite()
        {
            if (Party != 0 && !Busy) SteamFriends.ActivateGameOverlayInviteDialog(Id(Party));
        }
        public void JoinParty(ulong lobby)
        {
            if (!Online || Busy || lobby == 0) { Error = "Leave the current queue/session before joining another party."; return; }
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
                if (!success || !Compatible(lobby, party ? "party" : "match"))
                {
                    if (success) SteamMatchmaking.LeaveLobby(Id(lobby));
                    if (!party && IsLeader && Searching && !privateRoom) { nextSearch = Time.realtimeSinceStartup + UnityEngine.Random.Range(1f, 3f); TryCandidate(); }
                    else Fail("Lobby is full, unavailable, or uses another network version.");
                    return;
                }
                if (party)
                {
                    Party = lobby; partyOwner = Owner(Party); cancelledFollower = false;
                    Status = "Joined party."; Error = "";
                }
                else
                {
                    Match = lobby; ulong.TryParse(Data(Match, "host"), out ulong host); Host = host;
                    admitted = seenRoster = false; Started = false; privateRoom = Data(Match, "private") == "1";
                    deadline = Time.realtimeSinceStartup + 28; nextRequest = 0;
                    SessionChanged?.Invoke();
                }
            });
        }

        public void FindMatch(bool privateTest = false)
        {
            if (!Online || !IsLeader || Busy) return;
            Error = ""; Searching = true; privateRoom = privateTest; emptySearches = 0;
            FreezeQueue(); ticket = Guid.NewGuid().ToString("N");
            SteamMatchmaking.SetLobbyJoinable(Id(Party), false); Set(Party, "route", "search");
            Status = privateTest ? "Creating private test room..." : "Finding a team slot for your entire party...";
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
            SteamMatchmaking.AddRequestLobbyListStringFilter("kind", "match", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("phase", "waiting", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter("private", "0", ELobbyComparison.k_ELobbyComparisonEqual);
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
                if (failed) { nextSearch = Time.realtimeSinceStartup + 4; Status = "Steam search failed. Retrying..."; return; }
                var found = new List<ulong>();
                for (int i = 0; i < c.m_nLobbiesMatching; i++)
                {
                    ulong l = SteamMatchmaking.GetLobbyByIndex(i).m_SteamID;
                    if (l == Match || (merge && l >= Match)) continue;
                    if (int.TryParse(Data(l, "free0"), out int a) && int.TryParse(Data(l, "free1"), out int b) && Math.Max(a, b) >= queuedMembers.Length) found.Add(l);
                }
                if (merge && (found.Count == 0 || !IsHost || reservations.Groups.Count != 1 || Started)) return;
                candidates.Clear(); foreach (ulong l in found.OrderBy(x => x)) candidates.Enqueue(l);
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
                partyOwner = owner; Cancel(); Error = "Party leader left. Queue/session cancelled; invite or queue again."; return;
            }
            if (IsLeader && Searching)
            {
                if (!queuedHumans.SequenceEqual(PartyMembers)) { Cancel(); Error = "Party roster changed. Queue cancelled."; return; }
                foreach (var pair in cancelBaseline)
                    if (pair.Value != SteamMatchmaking.GetLobbyMemberData(Id(Party), Id(pair.Key), "cancel")) { Cancel(); Status = "A party member cancelled."; return; }
            }
            if (IsLeader) return;
            string route = Route(Party);
            if (route == "idle")
            {
                if (Match != 0 || pending) { generation++; pending = false; LeaveMatchInternal(); }
                cancelledFollower = false; Status = "Party ready. Waiting for the leader."; return;
            }
            if (cancelledFollower) return;
            if (route == "search")
            {
                if (pending) { generation++; pending = false; }
                if (Match != 0) LeaveMatchInternal(); Status = "Party leader is finding a match..."; return;
            }
            if (ulong.TryParse(route, out ulong target) && target != Match && !pending)
            { LeaveMatchInternal(); Join(target, false); }
        }

        void PollMatch(float now)
        {
            if (Host == 0 || Owner(Match) != Host || Data(Match, "phase") == "closed")
            {
                // Party-route and match-close notifications can arrive in either order
                // while the host merges an otherwise empty waiting room.
                if (invalidHostSince < 0) invalidHostSince = now;
                if (now - invalidHostSince > 2) Fail("Match host left. Returned to your party. Host migration is not enabled.");
                return;
            }
            invalidHostSince = -1;
            var present = Members(Match);
            if (IsHost)
            {
                if (!Started)
                {
                    foreach (var removed in reservations.Reconcile(present, now)) Set(Match, "grant_" + removed.Leader, removed.Ticket + "|expired");
                    if (reservations.Find(Self) == null) { Fail("Party could not join before the reservation expired."); return; }
                    PublishRoster();
                    if (!privateRoom && reservations.Ready) StartGame();
                }
                else
                {
                    foreach (ulong id in Roster.Keys.ToArray())
                        if (!present.Contains(id) && !BotIdentity.IsBot(id)) Roster.Remove(id);
                    PublishMembers();
                }
            }
            else
            {
                if (IsLeader && !admitted)
                {
                    string grant = Data(Match, "grant_" + Self);
                    if (grant == ticket + "|ok") { admitted = true; Set(Party, "route", Match.ToString()); }
                    else if (grant.StartsWith(ticket + "|", StringComparison.Ordinal))
                    { RetryAdmission("Room could not fit the entire party. Retrying..."); return; }
                    else if (now >= nextRequest)
                    {
                        nextRequest = now + 2;
                        SendChat(new Request { kind = "reserve", ticket = ticket, party = Party, members = queuedMembers });
                    }
                }
                ReadRoster();
                if (Roster.ContainsKey(Self))
                {
                    admitted = true; seenRoster = true;
                    // Steam can deliver roster and grant metadata in either order.
                    if (IsLeader && Route(Party) != Match.ToString()) Set(Party, "route", Match.ToString());
                }
                if (!seenRoster && now > deadline) { RetryAdmission("Party admission timed out."); return; }
                if (seenRoster && !Roster.ContainsKey(Self)) { Fail("Party reservation ended or a party member disconnected."); return; }
                Started = Data(Match, "phase") == "playing";
            }
            Status = Started ? "Session started. WASD to move, Space to jump." : $"Waiting room: {Roster.Count}/12. You can move while waiting.";
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
            if (!IsHost || c.m_ulSteamIDLobby != Match || Started) return;
            int length = SteamMatchmaking.GetLobbyChatEntry(Id(Match), (int)c.m_iChatID, out CSteamID sender, chatBuffer, chatBuffer.Length, out EChatEntryType type);
            if (length <= 0 || length >= chatBuffer.Length || type != EChatEntryType.k_EChatEntryTypeChatMsg || !Members(Match).Contains(sender.m_SteamID)) return;
            Request request;
            try { request = JsonUtility.FromJson<Request>(Encoding.UTF8.GetString(chatBuffer, 0, length)); }
            catch (ArgumentException) { return; }
            if (request == null || request.kind != "reserve" || string.IsNullOrEmpty(request.ticket) || request.ticket.Length > 64) return;
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
            if (!IsHost || Started || reservations.Groups.Any(g => !g.Committed) || (privateRoom ? Roster.Count < 2 : !reservations.Ready)) return;
            Started = true; SteamMatchmaking.SetLobbyJoinable(Id(Match), false); Set(Match, "phase", "playing");
        }
        public void Cancel()
        {
            if (!Online) return;
            generation++; pending = false; Searching = false; candidates.Clear();
            if (IsLeader) { Set(Party, "route", "idle"); SteamMatchmaking.SetLobbyJoinable(Id(Party), true); }
            else if (Party != 0) { cancelledFollower = true; SteamMatchmaking.SetLobbyMemberData(Id(Party), "cancel", Guid.NewGuid().ToString("N")); }
            LeaveMatchInternal(); Status = "Returned to party.";
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
                if (IsHost) { Set(Match, "phase", "closed"); SteamMatchmaking.SetLobbyJoinable(Id(Match), false); }
                SteamMatchmaking.LeaveLobby(Id(Match));
            }
            Match = Host = 0; Started = admitted = seenRoster = false; invalidHostSince = -1; Roster.Clear(); reservations.Clear();
            SessionChanged?.Invoke();
        }
        void Fail(string error) { Cancel(); Error = error; Status = error; }
        public void Dispose()
        {
            if (disposed) return;
            if (Online) { Cancel(); LeavePartyInternal(); }
            disposed = true;
            foreach (var c in callbacks) c.Dispose(); foreach (var c in calls) c.Dispose();
            callbacks.Clear(); calls.Clear();
            if (Online) SteamAPI.Shutdown(); Online = false;
        }
    }
}
