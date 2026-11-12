using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        //      for its empty seats. Leaving a started match takes only the leaver
        //      out, and party members publish the match they play ("match").
        // v15: the lab's snapshot carries the host's obstacle time ("CFR6", "v8" on JY-lobby), so
        //      clients draw moving obstacles where the host had them instead of on their own
        //      Steam clock.
        // v16 (main, R92): the 폰 러쉬 match is Pawn Rush course 01 run by the host over SteamPawnRushLink
        //      (ragdoll inputs and snapshots plus the course state "CFP1" on channel 34). A v15 build
        //      would load the old capsule scene for the same mode.
        // v16 (R111, skill branch): the lab's piece skills online ("CFR7"): the lab input carries press
        //      counts, the piece asked for and the skill moments' confirmation; a skills packet goes
        //      with every snapshot.
        // v17 (R112, branch skill-network-test): the lab input also carries the client's round trip,
        //      playback delay and loss ("CFR8") for the host's test screen.
        // v18 (R121, branch skill-network-test): the Sword Fight skill test online ("CFR9").
        // v19 (R124): main and skill-network-test merged; ragdoll positions carry a +-320 m range
        //      ("CFRA") so Pawn Rush course 01 (z up to 246 m) is no longer clamped at 80 m on clients.
        // v20 (R125): the piece skills, the promotion pads and the crown finish in the 폰 러쉬 match: its input is
        //      the lab's (press counts, the skill press), a skills packet goes with every snapshot, the course
        //      state is "CFP2" (pads, race clock, finishers, winner), skill positions span +-320 m ("CFRB").
        // v21 (R126): the 폰 러쉬 course state is "CFP3" (the host's time, each station's game block), the snapshot
        //      carries the host's obstacle time, the start bar drops on the shared start, mini-games v1.0.
        // v22 (R130): the course state is "CFP4" (who chooses a piece on each promotion pad), and a client's lab
        //      input's pieceAsk / pieceSeq is that player's chosen piece in the 폰 러쉬 match.
        // v23 (R153 on urp-conversion, from skill-network-test): the lab input carries the emote ask ("CFRC").
        // v24 (R149·R150, merged with v23 on 10-24): the Sword Fight match is the prototype (spec v1.0) on the lab's link over this session
        //      (SteamMatchLabLink, channel 35): the skills online, and its match state as text (TypeMatch).
        // v25 (R295): a member's "claim" counts only from the next host in line and only one epoch above what a PC
        //      can see (HostElection.Follow). A v24 build follows any claim, so the two would pick different hosts.
        // v26 (R297): the ragdoll snapshot's pawn is 65 bytes instead of 74 ("CFRD", RagdollWire): a v25 build drops
        //      every snapshot of a v26 host, so the two must not meet in one room.
        // v27 (R503 [5]): a successor takes over a silent host only once another seated player says it does not hear
        //      that host either (member data "quiet"). A v26 player never says so, so a v27 successor would never
        //      take over a silent host in a room with one.
        //      R504 (the same number: neither was ever built or sent out): the one after a next host in line that never
        //      steps in may pass it over (member data "past", HostElection.Follow). A v26 player does not follow it.
        // v28 (R508): the 폰 러쉬 match is the section round (PawnRush_Round, drawn from the match's number) and its
        //      course state is CFP5 (as many stations as the course has, the sections' own bytes). A v27 build would load
        //      course 01 and drop every CFP5 state.
        // v29 (R516): the section round's mini-game stations have promotion pads; their spent bits and choosers ride the
        //      course state (the same CFP5 bytes, now read for a round). A v28 host never sends them, so a v29 PC's pads
        //      would never work with it.
        // v30 (R517): a corridor (10 m) stands between every two sections of the round, so every section after the first
        //      lies further on. A v29 PC would put the host's pawns beside its own course; a build's tag is only its
        //      version (the same between two dev builds), so the protocol keeps them apart.
        //      R518 (the same number: v30 was pushed the same hour and never built or sent out): the round's war room
        //      conveyor belt stands still on its carriage. The host moves every pawn, so a build from between the two
        //      would only show a belt the host does not push with.
        // v31 (R524): G's king escort road ends on e8 (31.5 m), no longer 1.4 m on into the team door's tunnel. The wire
        //      is the same, but a v30 host would keep the sled moving past where a v31 PC draws it stopped, and a v30
        //      build was handed out (the 11-09 build, R523), so the two must not meet in one room.
        // v32 (R528): B's planks are things to grab and carry (user 11-09 list 2): the station's block is a PlankBoard
        //      (every plank's state, its carrier's seat or where it lies) in place of the carriers' seats and the piles'
        //      stock, so a v31 PC would read a v32 host's block as nonsense.
        // v33 (R532): the Royal Carriage Yard's centre way is a loading yard (stable carriages, a bolting horse, crane
        //      crates) in place of the carousel. The wire is the same, but a v32 PC would draw the carousel where a v33
        //      host knocks pawns with carriages and crates, and v32 builds were handed out, so the two must not meet.
        // v34 (R533): the room's manager is apart from its host (room data "manager", "kicked"; the manager's asks as
        //      "manage" requests in the room's chat). A v33 host would never answer a v34 manager's start, and a v33
        //      player would take the host for the one who starts.
        // v35 (R538): a started match takes back only the players it started with (room data "starters"); it is never
        //      offered to matchmaking again ("open" stays 0 after the start, "held" is gone). The Pawn Rush course state
        //      carries every seat's checkpoint and piece (CFP6). A v34 searcher would still walk into a v34 host's
        //      started match, and a v34 host would seat a stranger in a v35 player's match.
        // v36 (R541): every point on the ragdoll wire is a WirePoint - x and z over +-8388 m, y over +-320 m - instead of
        //      16 bits over +-320 m ("CFRE": the snapshot's hips and hook, 67 and 76 bytes a pawn; the skills packet; the
        //      Sword Fight input's aimed point). The section round runs to z 580 m and a v35 client drew every pawn past
        //      z 320 m pinned there (11-11 online test: an unseen wall on the grand stair hall's last glass stones). A v35
        //      PC drops every v36 ragdoll packet.
        // v37 (R543): the Pawn Rush mini-games are built for the size the teams started with (room data "seats": 3 to 6
        //      a side, MiniGameScale) - another board, other towers, another number of statues, kings and planks, the
        //      king's road drawn from a seed - and several games' blocks are laid out anew (A, B, F, G). A v36 PC would
        //      build six a side's old games beside a v37 host's and read its blocks wrongly.
        // v38 (R616): the round's last section - four turrets on the corners of the conveyor's well firing in turn at the
        //      carriage (TurretVolleys, drawn from the section's seed) instead of the goal's three rooks, and the finish
        //      beyond the great door, a PushGate whose progress rides in the course state (two more bytes of the
        //      sections' parts) with a second checkpoint behind it. A v37 PC would draw other shots, end the round on
        //      the star platform and read the parts' bytes two short.
        public const string Protocol = "chessfight.dua0731.network.v38";
        // The Steam App ID this project runs under until it has one of its own: Valve's public test app (Spacewar),
        // the same number as the project root's steam_appid.txt. A real App ID changes both (and see EnsureAppId).
        public const uint DevAppId = 480;
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
        // R533 (user 11-10, rule 4: "게임 관리 권한이 있는 방장 PC와 … 물리연산을 담당하는 PC를 구분"): the room's manager -
        // who may start it, put a player out of it and hand the role on - is no longer its host. The host is the PC
        // that simulates: the best one in the room, moved without anybody's say (HostElection). The manager is a
        // person: whoever made the room, until they hand the role over or leave (then the player seated longest).
        // The room carries it ("manager"). Only the host can write the room, so a manager that does not host asks
        // the host for what it wants (Ask), the way a party asks for its seats.
        public ulong Manager { get; private set; }
        public bool IsManager => Match != 0 && Manager != 0 && Manager == Self;
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
        // R503 [4]: the waiting room is ours to run: we host it, Steam has moved the room to us, and we have read
        // the reservations we took over with it. A host the room had just been handed to (R159) could start it or
        // add bots before that: every write to the room failed without a word (only the owner writes), so it went
        // into the match alone while everyone else stayed in the waiting room, or rebuilt the roster from nothing.
        bool RunsRoom => IsHost && !Started && Owner(Match) == Self && !adoptReservations;
        // This host has the room's books: it owns the room and has read the reservations that came with it.
        bool HoldsBooks => IsHost && Owner(Match) == Self && !adoptReservations;
        // R533: the waiting room is this player's to manage. A manager that also hosts waits for the room as before
        // (RunsRoom); one that does not asks the host, which answers once the room is its own.
        bool ManagesRoom => IsManager && !Started && (IsHost ? RunsRoom : Data(Match, "phase") == "waiting");
        public bool CanUseRoomBots => ManagesRoom && (privateRoom || AllowPublicBots);
        // One more filler bot from the entrance screen while the room fills.
        public bool CanAddRoomBot => CanUseRoomBots && Roster.ContainsKey(Self) && TeamCount(0) + TeamCount(1) < 12;
        public bool CanEditTestBots => ManagesRoom && privateRoom && Roster.ContainsKey(Self);
        public bool CanClearRoomBots => ManagesRoom && TestBots(0) + TestBots(1) > 0;
        // The manager's "게임 시작". On the host the room's own books decide (HostCanStart); a manager elsewhere reads
        // the same from what the room shows everyone (SeesRoomReady).
        public bool CanStartGame => IsManager && (IsHost ? HostCanStart : SeesRoomReady);
        bool HostCanStart => RunsRoom && reservations.Groups.All(g => g.Committed) && SeatingJoiners == 0 &&
            (privateRoom ? Roster.Count >= 2 : reservations.Ready || CanStartEarly);
        // What every PC can see of a waiting room: the seats reserved a team (free0 / free1) and who has arrived (the
        // roster lists only those). Ready when everyone reserved is in and the sizes allow a start.
        bool SeesRoomReady
        {
            get
            {
                if (Match == 0 || Started || Data(Match, "phase") != "waiting" || !Roster.ContainsKey(Self)) return false;
                int used0 = TeamCount(0), used1 = TeamCount(1);
                int in0 = Roster.Values.Count(p => p.Team == 0), in1 = Roster.Count - in0;
                if (in0 != used0 || in1 != used1 || SeatingJoiners > 0) return false;
                return privateRoom ? Roster.Count >= 2
                     : used0 + used1 == 12 || (EarlyStartMode && used0 >= MinTeamToStart && used0 == used1);
            }
        }
        // R525 (user 11-09 build test: "10초 안에 전부 들어와서 시작해도 방장PC만 시작되고 나머지는 튕김"): players in the
        // room whose seat request the host has not answered yet - they have just come in, or their request went
        // unanswered while the room was being handed to its best PC (only the owner grants seats) - for up to JoinGrace
        // each. The start closes the room, so it used to throw them out ("파티 전체가 들어갈 자리가 없습니다" or, 28 s
        // later, "입장 승인이 시간 초과되었습니다") although they stood in the room on every screen; a six-player
        // simulation with Steam's owner change taking a second did exactly that. A player that never asks holds it once.
        public const float JoinGrace = 8f;
        public int SeatingJoiners
        {
            get
            {
                if (Match == 0 || Started) return 0;
                // R533: the host counts them and tells the room ("seating"), so a manager on another PC sees them too.
                if (!HoldsBooks) return ParseInt(Data(Match, "seating"));
                if (reservations.Count >= 12) return 0;
                float now = Time.realtimeSinceStartup;
                return unseatedSince.Values.Count(since => now - since < JoinGrace);
            }
        }
        // R148: a public 폰 러쉬 room's host may start it short of twelve - three or more a side, the sides even.
        // R149: a 소드 파이트 room too (its prototype is played three against three).
        public bool EarlyStartMode => !privateRoom && Match != 0 && ((MatchMode ?? PartyMode)?.Key == GameModes.KingRush.Key
                                                                    || (MatchMode ?? PartyMode)?.Key == GameModes.SwordFight.Key);
        public bool CanStartEarly => EarlyStartMode && reservations.ReadyEarlyFrom(MinTeamToStart);
        // R525 (user 11-09 build test, 3: "1대1로도 테스트 가능하도록 임시 조치"): a development build's host may start a public
        // room from one a side (the sides still even); a release build keeps three (TeamReservations.MinTeamToStart).
        // The same switch that lets development builds take bots into public rooms (M6), so nothing has to be undone.
        // R540: the game passes that switch as true in every build for now (NetworkRuntime.TestRulesInEveryBuild).
        public int MinTeamToStart => AllowPublicBots ? 1 : TeamReservations.MinTeamToStart;
        // Seats reserved a team in the waiting room, players still on their way included. The host counts its
        // reservations; everyone else reads what the room says is left of six (free0 / free1) - before R533 a PC
        // that did not host read 0 here, and its entrance screen said "0 대 0".
        public int TeamCount(int team) => HoldsBooks ? reservations.Used(team) : WaitingUsed(team);
        int WaitingUsed(int team) =>
            Match == 0 || Started || team < 0 || team > 1 || Data(Match, "free" + team) == "" ? 0
            : Math.Max(0, TeamReservations.TeamSize - ParseInt(Data(Match, "free" + team)));
        // The room's own filler bots on a team (not a party's): their numbers start past a party's own bots.
        public int TestBots(int team) => HoldsBooks ? reservations.FillerBots(team)
            : Roster.Values.Count(p => p.Team == team && BotIdentity.IsBot(p.Id) && BotIdentity.Index(p.Id) >= BotIdentity.MaxPerParty);
        public bool CanAddTestBot(int team) => CanEditTestBots && team >= 0 && team <= 1 && TeamCount(team) < TeamReservations.TeamSize;
        public bool Busy => pending || Searching || Match != 0 || Route(Party) != "idle";
        // The mode the party queues for. Everyone in the party reads it from the
        // party lobby; only the leader writes it (SetMode).
        public GameModeInfo PartyMode => GameModes.Resolve(Data(Party, "mode"));
        // The mode of the match room we are in, which is the host's choice. A room
        // joined by its number keeps its own mode whatever the party had picked.
        public GameModeInfo MatchMode => Match == 0 ? null : GameModes.Resolve(Data(Match, "mode"));
        // A player back inside the match it left, waiting for the host to give it its seat again (Rejoin).
        public bool JoiningLive => Match != 0 && !Started && Live(Match);
        // Players of this started match who are away right now: they started it, left, and may still come back
        // (the room's free0 / free1 after the start, Starters.Away).
        public int Away => Match == 0 || !Started ? 0 : ParseInt(Data(Match, "free0")) + ParseInt(Data(Match, "free1"));
        // Someone else in the party is still playing a started match it stayed in
        // (party member data "match"). The party queues again once they are back.
        public bool PartyStillPlaying =>
            Party != 0 && PartyMembers.Any(id => id != Self && MemberData(Party, id, "match") != "");
        public const string PartyPlayingMessage = "파티원이 아직 경기 중입니다. 경기가 끝나고 돌아오면 다시 시작하세요.";
        // A member that left the party's started match early, waiting in the lobby
        // for the others (R60).
        public bool WaitingForParty => !IsLeader && cancelledFollower && Match == 0 && ulong.TryParse(Route(Party), out _);
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
        // R503 [5]: after this long without the host, a seated player says so (member data "quiet"), well before the
        // successor's own MigrateAfterSilence, so the word has time to reach it.
        public const float QuietAfter = MigrateAfterSilence / 2f;
        // A host gone from the room whose successor never claims: give up.
        public const float OrphanTimeout = 10f;
        // R504: how long the next host in line has to step in before the one after it passes it over (it is frozen,
        // still loading, or behind the same broken router as the host). Well inside OrphanTimeout and the scene
        // links' 12 s silence, with time for the claim to reach the room.
        public const float PassOverAfter = 3f;
        // R503 [9]: how long Steam's connection may be down before this PC leaves its match or search.
        public const float LogoffGrace = 10f;
        // R503 [8]: how long a party member in a waiting room that closed waits for its leader's next room.
        public const float ClosedRoomWait = 8f;
        // R159: how long the start waits for a newcomer's machine score before electing without it.
        public const float StartFitnessWait = 5f;
        // R159: between two moves of the waiting room's host (the record and the room's ownership settle).
        public const float WaitingHandoffCooldown = 3f;
        // R294: how long a waiting-room handover may wait for Steam to move the room's ownership before
        // the room counts as lost (a host that has gone gets 2 seconds, as before).
        public const float WaitingHandoffGrace = 10f;
        // Between a start or handover and the next handover for slowness.
        public const float StruggleCooldown = 45f;
        // R536: a change in who is in a running match (a player into an empty seat, a player gone, a machine score
        // that has just reached the room) is weighed once it has stood LiveElectSettle - a newcomer's match scene
        // has then been up that long, so it holds the poses a new host carries on from - and not within
        // LiveElectCooldown of the start or of a handover.
        public const float LiveElectSettle = 5f, LiveElectCooldown = 10f;
        // ---- Loading and a shared start (MatchStart) ----
        // How far this PC's match scene has loaded; published as member data "load".
        public void ReportLoading(int percent)
        {
            loadPercent = MatchStart.Clamp(percent);
            if (loadPercent < MatchStart.Ready) readySince = -1;
            else if (readySince < 0) readySince = Time.realtimeSinceStartup;
        }
        // R502: the clock the start time is written on, the runtime's shared clock
        // (Steam's whole seconds plus the local fraction). Steam's whole seconds without one.
        public Func<double> SharedClock { get; set; }
        double SharedNow() => SharedClock != null ? SharedClock() : ServerNow();
        // How far a player's match scene has loaded, 0-100. Bots have nothing to load.
        public int LoadPercent(ulong id) =>
            BotIdentity.IsBot(id) ? MatchStart.Ready : id == Self ? loadPercent : MatchStart.Parse(MemberData(Match, id, "load"));
        // The shared-clock time the host picked for everyone to start; 0 until then.
        public double StartAt => MatchStart.ParseTime(Data(Match, "go"));
        // R548: the shared-clock time the host started the match (StartNow), 0 until then: every PC's entrance screen plays
        // Pawn Rush's course reveal from it, so all show the same beat at the same moment.
        public double BegunAt => MatchStart.ParseTime(Data(Match, "begun"));
        // R543: the size the teams started the match with, the larger of the two (the room's "seats", written at the
        // start before "playing": every PC in the match reads the same number, and a player who comes back reads it
        // again). The Pawn Rush mini-games are scaled to it (MiniGameScale). While the room does not say - a start
        // from an older build - the players seated now.
        public string StartSeats => Data(Match, "seats");
        public int StartTeamSize
        {
            get
            {
                var seats = Starters.DecodeCapacity(Data(Match, "seats"));
                if (seats != null && Math.Max(seats[0], seats[1]) > 0) return Math.Max(seats[0], seats[1]);
                return Math.Max(1, Math.Max(Roster.Values.Count(p => p.Team == 0), Roster.Values.Count(p => p.Team == 1)));
            }
        }
        // Only a host that owns the room can write it (HOST.md), so a successor
        // still waiting for ownership leaves the start to the next poll.
        public bool CanAnnounceStart => IsHost && Started && Owner(Match) == Self;
        public void AnnounceStart(double at)
        {
            if (CanAnnounceStart && StartAt == 0) Set(Match, "go", MatchStart.FormatTime(at));
        }
        // R502: the host picks the start here, on every poll, not on its loading screen. A
        // successor that took over after its own screen had stopped waiting
        // (MatchStart.ClientGiveUp) never picked one, and the match never started. Once this
        // PC has loaded: when every player has, or MaxWait after its own load or its takeover.
        void AnnounceStartWhenDue(float now)
        {
            if (StartAt != 0 || !CanAnnounceStart || readySince < 0) return;
            if (MatchStart.ShouldStart(Roster.Keys.Select(LoadPercent), now - Math.Max(readySince, hostSince)))
                AnnounceStart(SharedNow() + MatchStart.Lead);
        }
        int loadPercent, publishedLoad = -1;
        // R502: since when this PC has had its match scene ready (realtime), -1 while loading.
        float readySince = -1;
        // ---- Coming back into a started match (R538, Starters) ----
        // The started match this player was seated in last, 0 for none: the one it may come back to. Kept between
        // runs by the game (LoadRejoin / SaveRejoin), so a crash or a closed game can come back as well.
        ulong rejoinMatch;
        bool rejoinLoaded;
        // This player is on its way back into that match, or back in it (until it leaves again): its own business,
        // not its party's - the party's route is neither followed nor written meanwhile.
        bool rejoining;
        // What the lobby knows of that match so far: asked from Steam every RejoinPoll, and whether Steam has said the
        // room is gone.
        float nextRejoinPoll;
        bool rejoinGone;
        // R525: waiting-room players without a seat yet, by when the host first saw them (SeatingJoiners).
        readonly Dictionary<ulong, float> unseatedSince = new Dictionary<ulong, float>();
        // What this host last set the room's Steam joinable flag to: -1 unknown, 0 or 1.
        int joinable = -1;
        // ---- Chat (Docs/Architecture/UI.md "채팅", R61) ----
        // Lines arrive through Steam lobby chat, which echoes our own lines back to
        // us too: the log shows what Steam actually delivered.
        public readonly ChatLog Chat = new ChatLog();

        // R529 (user 11-09: "파티초대 메시지가 인게임에서 출력될 수 있게"): party invites this PC received (Steam's
        // LobbyInvite_t, sent by InviteToParty), oldest first, each for InviteLasts seconds. The lobby shows the newest
        // with 수락 / 거절 and a match scene's chat gets a party line; Steam's own overlay notice still comes as well.
        // An invite used to reach the game only once accepted in the overlay (GameLobbyJoinRequested_t).
        public struct PartyInvite { public ulong From, Lobby; public float At; }
        public const float InviteLasts = 300f;
        public const int InvitesKept = 3;
        readonly List<PartyInvite> invites = new List<PartyInvite>();
        public IReadOnlyList<PartyInvite> Invites => invites;
        public bool CanChat(ChatChannel channel) =>
            Online && (channel == ChatChannel.Party ? Party != 0 :
                       channel == ChatChannel.All ? Match != 0 : Match != 0 && Roster.ContainsKey(Self));
        // Sends one line. "" once it is on its way (or there was nothing to send),
        // otherwise why not, for the chat box to show.
        public string Say(ChatChannel channel, string text)
        {
            text = ChatText.Clean(text);
            if (text == "") return "";
            if (!CanChat(channel)) return $"지금은 {ChatText.Label(channel)} 채팅을 쓸 수 없어요.";
            if (!chatThrottle.TryPass(Time.realtimeSinceStartup)) return "1초에 한 번까지 보낼 수 있어요.";
            int team = channel == ChatChannel.Team ? Roster[Self].Team : 0;
            byte[] bytes = Encoding.UTF8.GetBytes(ChatText.Encode(channel, team, text));
            ulong lobby = channel == ChatChannel.Party ? Party : Match;
            return SteamMatchmaking.SendLobbyChatMsg(Id(lobby), bytes, bytes.Length) ? "" : "메시지를 보내지 못했어요.";
        }
        readonly ChatThrottle chatThrottle = new ChatThrottle();
        // The party and match the log's channels belong to, and who was in them,
        // for the "들어왔어요 / 나갔어요" notices.
        ulong chatParty, chatMatch;
        bool chatMatchLive;
        readonly HashSet<ulong> chatPartyMembers = new HashSet<ulong>(), chatMatchPlayers = new HashSet<ulong>();

        readonly FrameMonitor frames = new FrameMonitor();
        int epoch, publishedFit, publishedBase;
        // R295: the claim this PC made (Promote), read from here rather than from
        // the member data, which Steam may not have echoed back yet.
        int ownClaim;
        // R503 [5]: what this PC last wrote as its member data "quiet".
        string publishedQuiet = "";
        // R503 [9]: since when Steam's connection has been down (-1 while up), and whether that has left the match.
        float loggedOffSince = -1;
        bool logoffLeft;
        // R503 [7]: the party came into this room by its number (JoinPrivateMatch) and asked for this room only.
        bool joinedByNumber;
        // R504: the next host in line this PC passed over (member data "past"), and since when it has been waiting for
        // that one to step in (-1 while there is nothing to wait for).
        ulong ownPast;
        float passOverSince = -1;
        // R504: the match is over and this host has yet to write it into the room (it does not own it yet).
        bool endWanted;
        string publishedLoc = "", localLoc = "";
        // R159: a start that waits for a newcomer's machine score (StartGame); a
        // waiting-room host that took the room over and reads its reservations once
        // it owns it; the next waiting-room election.
        bool startWanted, adoptReservations;
        // R295: the player this waiting-room host handed the room to, while the
        // room is still ours (Steam has not moved it yet).
        ulong waitingHandedTo;
        float startWaitSince = -1, nextWaitElect;
        // R294: what the waiting-room election last wrote to the log.
        string lastWaitElection = "";
        float hostSilence, hostSince = -1, orphanSince = -1, nextFitPublish, nextSuccessors, nextOwnerFix, lastHandoff = -1000, noteUntil;
        // R536: the players this host weighed last in the running match (null: none yet - it takes the room as it
        // finds it), the players as they stand now, and since when they have stood so (ElectLive).
        string liveElectorate, liveSeen = "";
        float liveSeenSince;
        readonly TeamReservations reservations = new TeamReservations();
        readonly List<IDisposable> callbacks = new List<IDisposable>();
        readonly List<IDisposable> calls = new List<IDisposable>();
        readonly Queue<ulong> candidates = new Queue<ulong>();
        readonly Dictionary<ulong, string> cancelBaseline = new Dictionary<ulong, string>();
        readonly byte[] chatBuffer = new byte[2048];
        ulong[] queuedMembers = Array.Empty<ulong>();   // humans + declared party bots
        ulong[] queuedHumans = Array.Empty<ulong>();    // humans only, for roster-change detection
        ulong partyOwner, playingParty;
        string publishedPlaying = "";
        string ticket = "", presence, carriedError, queuedMode = GameModes.Default.Key;
        bool pending, admitted, seenRoster, privateRoom, cancelledFollower, disposed;
        int generation;
        float nextPoll, nextSearch, deadline, nextRequest, mergeAt, invalidHostSince = -1;
        int emptySearches;

        // "reserve": a party's seats (ticket, party, members). "manage" (R533): the manager's ask of the host (op, and
        // for some a target player or a team; seq numbers the ask, so one sent a few times over is done once).
        [Serializable] sealed class Request { public string kind, ticket; public ulong party; public ulong[] members; public string op; public ulong target; public int team, seq; }
        // R533: the manager seen last (for the notice when it changes); this manager's asks on their way to the host
        // (each repeated for a while); and, on the host, the asks it has done, by who asked.
        ulong lastManager;
        sealed class PendingAsk { public string Op; public ulong Target; public int Team, Seq; public float Until, Next; }
        readonly List<PendingAsk> asks = new List<PendingAsk>();
        int askSeq;
        readonly Dictionary<ulong, HashSet<int>> asksDone = new Dictionary<ulong, HashSet<int>>();
        // Players a manager put out of a room, as the room keeps them ("kicked"): this many at most, the oldest dropped.
        const int KickedKept = 24;
        [Serializable] sealed class RosterData { public Member[] members; }
        [Serializable] sealed class Member { public ulong id; public int team, slot; }

        public SteamSession(string build = "", bool allowPublicBots = true)
        { Build = build ?? ""; AllowPublicBots = allowPublicBots; }

        static CSteamID Id(ulong id) => new CSteamID(id);
        public string Name(ulong id)
        {
            if (BotIdentity.IsBot(id)) return Roster.TryGetValue(id, out var bot) ? "BOT " + (bot.Slot + 1) : "BOT";
            if (!Online) return id.ToString();
            if (id == Self && localName != "") return localName;
            // The name each player chose (R84), published with the party and the match
            // room; anyone who has none, or an older build, keeps their Steam name.
            string nick = Nick(Match, id);
            if (nick == "") nick = Nick(Party, id);
            return nick != "" ? nick : SteamFriends.GetFriendPersonaName(Id(id));
        }
        static string Nick(ulong lobby, ulong id)
        {
            string nick = MemberData(lobby, id, "nick");
            return PlayerNames.Showable(nick) ? nick : "";
        }

        // The name chosen on the name screen (R84), set by the game: shown for this
        // player here and published as member data "nick" of the party and the match
        // room for everyone else. Empty (or not a valid name) means the Steam name.
        public string LocalName
        {
            get => localName;
            set => localName = PlayerNames.Showable(value) ? value : "";
        }
        string localName = "", nickPartyValue = "", nickMatchValue = "";
        ulong nickParty, nickMatch;

        // Written when the room or the name changed; Steam clears member data when a
        // player leaves, so a new room always gets it again.
        void PublishNick()
        {
            if (Party == 0) nickParty = 0;
            else if (Party != nickParty || nickPartyValue != localName)
            {
                SteamMatchmaking.SetLobbyMemberData(Id(Party), "nick", localName);
                nickParty = Party; nickPartyValue = localName;
            }
            if (Match == 0) nickMatch = 0;
            else if (Match != nickMatch || nickMatchValue != localName)
            {
                SteamMatchmaking.SetLobbyMemberData(Id(Match), "nick", localName);
                nickMatch = Match; nickMatchValue = localName;
            }
        }
        // Voice chat (R131, SteamVoice): whether this player has it on, published as member data "vc" of
        // the party and the match room, so the others' match sheets can say "음성 꺼짐". Nothing
        // published (an older build) reads as on.
        public bool VoiceOn { get => voiceOn; set => voiceOn = value; }
        public bool VoiceOff(ulong id)
        {
            if (id == 0 || BotIdentity.IsBot(id)) return false;
            if (id == Self) return !voiceOn;
            string value = MemberData(Match, id, "vc");
            if (value == "") value = MemberData(Party, id, "vc");
            return value == "0";
        }
        bool voiceOn = true;
        string voicePartyValue = "", voiceMatchValue = "";
        ulong voiceParty, voiceMatch;

        void PublishVoice()
        {
            string value = voiceOn ? "1" : "0";
            if (Party == 0) voiceParty = 0;
            else if (Party != voiceParty || voicePartyValue != value)
            {
                SteamMatchmaking.SetLobbyMemberData(Id(Party), "vc", value);
                voiceParty = Party; voicePartyValue = value;
            }
            if (Match == 0) voiceMatch = 0;
            else if (Match != voiceMatch || voiceMatchValue != value)
            {
                SteamMatchmaking.SetLobbyMemberData(Id(Match), "vc", value);
                voiceMatch = Match; voiceMatchValue = value;
            }
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
        // R118 (the skill network test screen): everyone in the match as the
        // election ranks them right now, and the published order of successors.
        public List<HostElection.Candidate> HostCandidates() =>
            Match == 0 ? new List<HostElection.Candidate>() : Candidates(Members(Match));
        public List<ulong> PublishedSuccessors() => HostElection.Decode(Data(Match, "successors"));
        // R533: a player's machine score as the room has it (what the waiting room's election compares), 0 = not
        // measured yet or a bot. A development build's entrance screen shows it on the player's card.
        public int MachineScore(ulong id) =>
            Match == 0 || BotIdentity.IsBot(id) ? 0 : id == Self ? LocalFitness : ParseInt(MemberData(Match, id, "base"));
        // The host-change line (Note) is still on show.
        public bool NoteShowing => Time.realtimeSinceStartup < noteUntil && Note != "";

        static HostElection.Candidate Pick(List<HostElection.Candidate> candidates, ulong id) =>
            candidates.FirstOrDefault(c => c.Id == id);

        // Who takes over from `leaving`. Every PC must reach the same answer from
        // the same room data, so our own fitness here is the published one.
        ulong SuccessorOf(ulong leaving, HashSet<ulong> present)
        {
            var heirs = Heirs(present);
            return HostElection.Successor(PublishedSuccessors(), heirs.Select(c => c.Id).ToList(), leaving, heirs);
        }

        // The seated players here, the only ones who can take the host role over.
        List<HostElection.Candidate> Heirs(HashSet<ulong> present) =>
            present.Where(id => !BotIdentity.IsBot(id) && Roster.ContainsKey(id)).Select(id => new HostElection.Candidate
            { Id = id, Fitness = id == Self ? publishedFit : ParseInt(MemberData(Match, id, "fit")), PingMs = -1 }).ToList();

        public void Initialize()
        {
            try
            {
                string appId = EnsureAppId();
                string why = "Packsize/DllCheck";
                var result = Packsize.Test() && DllCheck.Test() ? SteamAPI.InitEx(out why) : ESteamAPIInitResult.k_ESteamAPIInitResult_FailedGeneric;
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    // Steam's own reason goes to the log (Player.log in a build): before R523 a build refused for a
                    // missing App ID only ever said "log in", with nothing in the log.
                    Error = InitFailure(result, appId);
                    Debug.LogWarning("[ChessFight] Steam 초기화 실패: " + result + " · App ID " + appId + " · " + why);
                    return;
                }
                Online = true; Self = SteamUser.GetSteamID().m_SteamID;
                SteamNetworkingUtils.InitRelayNetworkAccess();
                callbacks.Add(Callback<LobbyChatMsg_t>.Create(OnChat));
                callbacks.Add(Callback<GameLobbyJoinRequested_t>.Create(c => JoinParty(c.m_steamIDLobby.m_SteamID)));
                callbacks.Add(Callback<LobbyInvite_t>.Create(c => ReceiveInvite(c.m_ulSteamIDUser, c.m_ulSteamIDLobby)));
                // R538: Steam's word on the match this player may come back to (RequestLobbyData): gone, or still there.
                callbacks.Add(Callback<LobbyDataUpdate_t>.Create(c =>
                { if (c.m_ulSteamIDLobby == rejoinMatch && c.m_ulSteamIDMember == c.m_ulSteamIDLobby) rejoinGone = c.m_bSuccess == 0; }));
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

        const string AppIdFile = "steam_appid.txt";

        // R523: Steam takes the App ID from SteamAppId in the environment (Steam sets it when it starts a game), else
        // from a steam_appid.txt beside the game. The Editor runs in the project root, next to that file; a player
        // started outside Steam had neither, since Unity's own Build never copies the file (Steamworks.NET's build step
        // neither). Steam then refused with "No appID found" while it was running and logged in, so the build looked as
        // if it never saw the login. With neither, SteamAppId is set here, before the init: from the file next to the
        // exe, else DevAppId (Steam reads the variable even with its library already loaded). Once the game has a real
        // App ID and ships through Steam, a copy started outside Steam should be sent through Steam instead
        // (SteamAPI.RestartAppIfNecessary). Returns the App ID Steam is going to use, for the messages.
        static string EnsureAppId()
        {
            string id = Environment.GetEnvironmentVariable("SteamAppId");
            if (!string.IsNullOrEmpty(id)) return id;
            id = ReadAppId(Path.Combine(Directory.GetCurrentDirectory(), AppIdFile));
            if (id != null) return id;
            // Application.dataPath is <exe folder>/<name>_Data in a player.
            string exeFolder = string.IsNullOrEmpty(Application.dataPath) ? null : Path.GetDirectoryName(Application.dataPath);
            id = (exeFolder != null ? ReadAppId(Path.Combine(exeFolder, AppIdFile)) : null) ?? DevAppId.ToString();
            Environment.SetEnvironmentVariable("SteamAppId", id);
            return id;
        }

        // The App ID written in a steam_appid.txt, or null when the file is missing or holds no App ID.
        static string ReadAppId(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string text = File.ReadAllText(path).Trim();
                return uint.TryParse(text, out uint id) && id != 0 ? id.ToString() : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return null; }
        }

        // What the player is told when Steam refuses to start, by Steam's reason.
        static string InitFailure(ESteamAPIInitResult result, string appId) =>
            result == ESteamAPIInitResult.k_ESteamAPIInitResult_VersionMismatch
                ? "Steam 클라이언트가 이 게임보다 오래되었습니다. Steam을 업데이트한 뒤 다시 연결하세요."
                : "Steam 초기화 실패. Steam을 실행하고 로그인한 뒤 다시 연결하세요. (App ID " + appId + ")";

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
            if (invites.Count > 0) invites.RemoveAll(i => now - i.At > InviteLasts || i.Lobby == Party);
            if (pending && now > deadline) { generation++; pending = false; Fail("Steam 응답이 없습니다. 다시 시도하세요."); }
            if (now < nextPoll) return;
            nextPoll = now + .25f;
            if (!SteamUser.BLoggedOn()) { WaitOutLogoff(now); return; }
            loggedOffSince = -1; logoffLeft = false;
            // R533: the manager put this player (its party) out of the waiting room. Before the party's own route: a
            // member would otherwise follow its leader out without ever being told why.
            if (Match != 0 && !Started && KickedIds(Match).Contains(Self)) Fail(KickedMessage);
            PollParty();
            if (Match != 0) PollMatch(now);
            TrackRejoin(now);
            PublishNick();
            PublishVoice();
            PublishPlaying();
            PublishPenalty();
            FollowChat();
            UpdatePresence();
            if (Searching && Match == 0 && !pending && now >= nextSearch) Search();
            // A host with only its own party periodically tries an older room. This
            // converges simultaneous room creation without dismantling occupied rooms.
            // R503 [7]: not a room the party came to by its number: it asked for that one.
            if (IsHost && Searching && !Started && !privateRoom && !joinedByNumber && reservations.Groups.Count == 1 && !pending && now > mergeAt)
            { mergeAt = now + 8; Search(true); }
        }

        // R503 [9]: Steam's connection dropping for a moment threw the player out of a live match at once (a host
        // handed it on), and Cancel ran again on every poll while it lasted (a party member wrote a new "cancel"
        // each time). A drop is waited out for LogoffGrace. Nothing is read from the rooms meanwhile: what Steam
        // shows of them is out of date, and a host or a successor must not act on it. A longer drop leaves, once.
        void WaitOutLogoff(float now)
        {
            if (loggedOffSince < 0) loggedOffSince = now;
            if (now - loggedOffSince < LogoffGrace) { Status = "Steam 연결이 잠시 끊겼습니다. 다시 연결되기를 기다리는 중..."; return; }
            if (logoffLeft) return;
            logoffLeft = true;
            Cancel($"Steam 연결이 {LogoffGrace:0}초 넘게 끊겨 나감"); Error = "Steam 연결이 끊겼습니다. 다시 로그인한 뒤 Play를 재시작하세요.";
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
                    // R533: whoever makes the room manages it (start, kick, hand over), whichever PC comes to host it.
                    Set(Match, "manager", Self.ToString()); Manager = lastManager = Self;
                    // A waiting room takes players until it starts; after that it takes
                    // back only its own players who left (R538, Starters).
                    Set(Match, "private", privateRoom ? "1" : "0"); Set(Match, "open", "1"); PublishRoster();
                    Set(Party, "route", Match.ToString()); mergeAt = Time.realtimeSinceStartup + 6;
                    Diagnose(privateRoom ? "비공개 방을 만듦" : "대기실을 만듦(찾은 방이 없음)");
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
            if (CanRejoin) { Error = RejoinFirstMessage; return false; }
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
            if (!IsHost) { Ask("fill"); return; }
            FillNow();
        }
        void FillNow()
        {
            if (!RunsRoom) return;
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
        // One filler bot on whichever team has room; a public room that reaches
        // twelve this way starts on its own like any full room.
        public bool AddRoomBot()
        {
            if (!CanAddRoomBot) return false;
            if (!IsHost) { Ask("addbot"); return true; }
            return AddRoomBotNow();
        }
        bool AddRoomBotNow()
        {
            if (!RunsRoom || reservations.Count >= 12) return false;
            if (!reservations.ReserveBots(Self, BotIdentity.Fill(Self, 1, NextFillerIndex()), Time.realtimeSinceStartup, out _)) return false;
            PublishRoster();
            return true;
        }
        public void ClearRoomBots()
        {
            if (!ManagesRoom) return;
            if (!IsHost) { Ask("clearbots"); return; }
            ClearRoomBotsNow();
        }
        void ClearRoomBotsNow()
        {
            if (!RunsRoom) return;
            reservations.RemoveFillerBots(); PublishRoster();
        }
        public bool AddTestBot(int team)
        {
            if (!CanAddTestBot(team)) return false;
            if (!IsHost) { Ask("addtest", 0, team); return true; }
            return AddTestBotNow(team);
        }
        bool AddTestBotNow(int team)
        {
            if (!RunsRoom || !privateRoom || team < 0 || team > 1 || reservations.Used(team) >= TeamReservations.TeamSize) return false;
            if (!reservations.ReserveBots(Self, BotIdentity.Fill(Self, 1, NextFillerIndex()), Time.realtimeSinceStartup, team, out var group)) return false;
            // Filler bots have no asynchronous Steam arrival to wait for.
            group.Committed = true;
            PublishRoster();
            return true;
        }
        public bool RemoveTestBot(int team)
        {
            if (!CanEditTestBots || team < 0 || team > 1 || TestBots(team) <= 0) return false;
            if (!IsHost) { Ask("removetest", 0, team); return true; }
            return RemoveTestBotNow(team);
        }
        bool RemoveTestBotNow(int team)
        {
            if (!RunsRoom || !privateRoom || team < 0 || team > 1 || !reservations.RemoveFillerBot(team)) return false;
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
        // R529: an invite came in (LobbyInvite_t). One per sender and per party, the newest last.
        void ReceiveInvite(ulong from, ulong lobby)
        {
            if (lobby == 0 || lobby == Party || from == Self) return;
            float now = Time.realtimeSinceStartup;
            invites.RemoveAll(i => i.Lobby == lobby || i.From == from);
            invites.Add(new PartyInvite { From = from, Lobby = lobby, At = now });
            while (invites.Count > InvitesKept) invites.RemoveAt(0);
            Chat.Add(ChatChannel.Party, 0, "", $"{Name(from)} 님이 파티에 초대했어요 · " + (Busy ? "끝나고 로비에서 수락할 수 있어요" : "로비에서 수락할 수 있어요"),
                     true, false, now);
            Debug.Log($"[ChessFight] 파티 초대 받음: {Name(from)} ({from}) → 파티 {lobby}");
        }

        // 수락: into that party (JoinParty says why not, as an Error, while matching or in a match).
        public void AcceptInvite(ulong lobby)
        {
            invites.RemoveAll(i => i.Lobby == lobby);
            JoinParty(lobby);
        }

        public void DeclineInvite(ulong lobby) => invites.RemoveAll(i => i.Lobby == lobby);

        public void JoinParty(ulong lobby)
        {
            // R504: an invite to the party this PC is already in (an old one accepted from Steam) changes nothing. It used
            // to leave the party and join it again: a leader handed the lead to a member on the way out.
            if (Online && lobby != 0 && lobby == Party) return;
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
                    Diagnose($"{(party ? "파티" : "방")} {lobby}에 못 들어감: {problem}");
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
                    // Coming into a started match (back to our seat, R538): its host record
                    // is ours from the start, so it is not reported as a change of host.
                    if (Live(Match)) epoch = ParseInt(Data(Match, "epoch"));
                    admitted = seenRoster = false; Started = false; privateRoom = Data(Match, "private") == "1";
                    deadline = Time.realtimeSinceStartup + 28; nextRequest = 0;
                    // R503 [7]: a public room's number is no way round M6 (public matches are people only).
                    if (joinedByNumber && !privateRoom && BotsBlockPublicMatch) { Fail(BotsInPublicMessage); return; }
                    // R533: nor round a matchmaking stop (LeavePenalty).
                    if (joinedByNumber && !privateRoom && PenaltyBlock() != null) { Fail(PenaltyBlock()); return; }
                    Diagnose(Live(Match) ? "진행 중인 경기 방에 들어감(자리 요청 전)" : "대기실에 들어감");
                    SessionChanged?.Invoke();
                }
            });
        }

        public void FindMatch(bool privateTest = false)
        {
            if (!Online || !IsLeader || Busy) return;
            if (CanRejoin) { Error = RejoinFirstMessage; return; }
            if (PartyStillPlaying) { Error = PartyPlayingMessage; return; }
            if (!privateTest && BotsBlockPublicMatch)
            { Error = BotsInPublicMessage; return; }
            // R533 (rule 3): a deserter's matchmaking is stopped for a while, and its party's with it.
            if (!privateTest && PenaltyBlock() != null) { Error = PenaltyBlock(); return; }
            Error = ""; Searching = true; privateRoom = privateTest; emptySearches = 0; joinedByNumber = false;
            FreezeQueue(); ticket = Guid.NewGuid().ToString("N");
            SteamMatchmaking.SetLobbyJoinable(Id(Party), false); Set(Party, "route", "search");
            Status = privateTest ? "비공개 테스트 방을 만드는 중..." : "파티 전체가 들어갈 자리를 찾는 중...";
            if (privateTest) CreateLobby(true); else { nextSearch = 0; Search(); }
        }
        public void JoinPrivateMatch(ulong lobby)
        {
            if (!IsLeader || Busy || lobby == 0) return;
            if (PartyStillPlaying) { Error = PartyPlayingMessage; return; }
            if (CanRejoin) { Error = RejoinFirstMessage; return; }
            Error = ""; Searching = true; privateRoom = true; joinedByNumber = true; FreezeQueue(); ticket = Guid.NewGuid().ToString("N");
            SteamMatchmaking.SetLobbyJoinable(Id(Party), false); Set(Party, "route", "search"); Join(lobby, false);
        }
        // 11-11 (the user): while the match this player left still runs, the only way into a game is back into it.
        public const string RejoinFirstMessage = "진행 중인 경기가 있어요. 그 경기로 돌아가거나 끝날 때까지 다른 경기는 할 수 없어요.";
        public const string BotsInPublicMessage = "봇이 있는 파티는 공개 매칭에 들어갈 수 없습니다. 봇을 빼거나 테스트 방을 만드세요.";
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
            // Waiting rooms only. R538: a started match is never offered (its "open" is 0 from the start on): it takes
            // back its own players, and those come by Rejoin, not by a search.
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
                    // Waiting rooms merge only into older ones, so two can never swap. A started match is
                    // nobody's to walk into (R538), whatever a stale search result says.
                    if (l == Match || Live(l) || (merge && l >= Match)) continue;
                    // R533: not a room whose manager put one of us out.
                    if (KickedIds(l).Overlaps(queuedHumans)) continue;
                    if (int.TryParse(Data(l, "free0"), out int a) && int.TryParse(Data(l, "free1"), out int b) && Math.Max(a, b) >= queuedMembers.Length) found.Add(l);
                }
                if (merge && (found.Count == 0 || !IsHost || reservations.Groups.Count != 1 || Started)) return;
                candidates.Clear(); foreach (ulong l in found.OrderBy(x => x)) candidates.Enqueue(l);
                if (merge) { Diagnose("우리 파티뿐인 대기실을 닫고 더 오래된 방으로 옮김"); LeaveMatchInternal(); Set(Party, "route", "search"); }
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
                partyOwner = owner;
                // A started match goes on whoever leads the party now (R60).
                if (Started) return;
                Cancel("파티장이 바뀌어 매칭 취소"); Error = "파티장이 나갔습니다. 매칭이 취소되었습니다."; return;
            }
            // Before the start the party moves as one. Once the match has started,
            // everyone plays it out on their own: a member who leaves, or whose
            // game dies, takes only its own seat with it (R60).
            if (IsLeader && Searching && !Started)
            {
                if (!queuedHumans.SequenceEqual(PartyMembers)) { Cancel("파티 구성이 바뀌어 매칭 취소"); Error = "파티 구성이 바뀌어 매칭을 취소했습니다."; return; }
                foreach (var pair in cancelBaseline)
                    if (pair.Value != SteamMatchmaking.GetLobbyMemberData(Id(Party), Id(pair.Key), "cancel")) { Cancel($"파티원 {Name(pair.Key)} 님이 취소해 매칭 취소"); Status = "파티원이 취소했습니다."; return; }
            }
            // R538: nor while this player goes back into the match it left - that is its own way, not the party's.
            if (IsLeader || Started || rejoining) return;
            string route = Route(Party);
            if (route == "idle")
            {
                if (Match != 0) Diagnose("파티장이 매칭을 멈춰 방에서 나옴");
                if (Match != 0 || pending) { generation++; pending = false; LeaveMatchInternal(); }
                cancelledFollower = false; Status = "파티 준비 완료. 파티장을 기다리는 중."; return;
            }
            if (cancelledFollower)
            {
                // Left the party's started match: back together when it ends.
                if (WaitingForParty) Status = "파티원들이 아직 경기 중입니다. 경기가 끝나면 파티로 다시 모입니다.";
                return;
            }
            if (route == "search")
            {
                if (pending) { generation++; pending = false; }
                if (Match != 0) { Diagnose("파티장이 다른 방을 찾기 시작해 방에서 나옴"); LeaveMatchInternal(); }
                Status = "파티장이 매칭을 찾는 중..."; return;
            }
            if (ulong.TryParse(route, out ulong target) && target != Match && !pending)
            { if (Match != 0) Diagnose($"파티장을 따라 방 {target}(으)로 옮김"); LeaveMatchInternal(); Join(target, false); }
        }

        void PollMatch(float now)
        {
            string phase = Data(Match, "phase");
            // Before the start the host is the best PC in the waiting room (R159,
            // ElectWaiting): the role and the reservations move to it through the
            // room's record ("host", "resv"), and a host who leaves hands the room
            // on. Only a host that vanishes without a word (a crash) still ends the
            // waiting room. From the start on the host role moves instead
            // (FollowHost), so a missing or silent host no longer ends the match.
            bool live = Started || phase == "playing";
            if (!live && phase != "closed") FollowWaitingHost(now);
            if (phase == "closed" || (!live && (Host == 0 || Owner(Match) != Host)))
            {
                // R294: a waiting-room handover on its way (the record names a host still in the
                // room, Steam has not moved the ownership yet) is not a host that has gone.
                bool handing = phase != "closed" && Host != 0 && ParseId(Data(Match, "host")) == Host && Members(Match).Contains(Host);
                float grace = handing ? WaitingHandoffGrace : 2;
                // Party-route and match-close notifications can arrive in either order
                // while the host merges an otherwise empty waiting room.
                if (invalidHostSince < 0)
                {
                    invalidHostSince = now;
                    Diagnose(phase == "closed" ? "대기실이 닫힘" : handing ? $"방 주인이 물리 담당 PC에게 넘어오기를 기다림(최대 {grace:0}초)" : $"물리 담당 PC가 방에 없음({grace:0}초 기다림)");
                }
                if (now - invalidHostSince > grace) LeaveClosedRoom(live, now - invalidHostSince - grace);
                return;
            }
            invalidHostSince = -1;
            var present = Members(Match);
            FollowManager(present);
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
                    // R159: a host that has just taken the waiting room over writes nothing
                    // until the room is its own (only the owner can write it).
                    if (Owner(Match) == Self)
                    {
                        if (adoptReservations) { LoadReservations(); adoptReservations = false; }
                        foreach (var removed in reservations.Reconcile(present, now))
                        {
                            Diagnose($"자리 예약 풀림: {Who(removed.Leader)} 님 파티 {removed.Members.Length}명(기한 안에 다 오지 못했거나, 다 온 뒤 한 명이 나감)");
                            Set(Match, "grant_" + removed.Leader, removed.Ticket + "|expired");
                        }
                        if (reservations.Find(Self) == null) { Fail("예약 시간 안에 파티가 모두 입장하지 못했습니다."); return; }
                        PublishRoster();
                        KeepManager(present);
                        TrackJoiners(present, now);
                        SetChanged("seating", SeatingJoiners.ToString());
                        // R159: a start that is waiting for a newcomer's machine score goes on by itself.
                        if ((!privateRoom && reservations.Ready) || startWanted) StartNow();
                        if (!Started) ElectWaiting(present, now);
                    }
                }
                else
                {
                    // A player who left leaves an empty seat on its team.
                    foreach (ulong id in Roster.Keys.ToArray())
                        if (!present.Contains(id) && !BotIdentity.IsBot(id)) Roster.Remove(id);
                    // Only the room's owner can write the seats (HOST.md). R504: the match's end first, if it came first.
                    if (Owner(Match) == Self) { WriteEnded(); PublishSeats(); KeepManager(present); Beat(now); }
                    AnnounceStartWhenDue(now);
                    PublishMembers();
                    PublishSuccessors(present, now);
                    CheckStruggle(present, now);
                    ElectLive(present, now);
                }
            }
            else
            {
                // R538: a player coming back asks for its own seat itself, whoever leads its party, and leaves the
                // party's route alone (its party did not come with it).
                if ((IsLeader || rejoining) && !admitted)
                {
                    string grant = Data(Match, "grant_" + Self);
                    if (grant == ticket + "|ok") { admitted = true; if (!rejoining) Set(Party, "route", Match.ToString()); }
                    else if (rejoining && grant.StartsWith(ticket + "|", StringComparison.Ordinal))
                    {
                        // Refused: the match is decided, or it never was this player's. Nothing to come back to.
                        ForgetRejoin(); Fail(RejoinRefused);
                        return;
                    }
                    else if (grant.StartsWith(ticket + "|", StringComparison.Ordinal))
                    {
                        RetryAdmission("파티 전체가 들어갈 자리가 없습니다. 다시 찾는 중...",
                                       grant == ticket + "|bots" ? BotsInPublicMessage : "이 방에는 파티 전체가 들어갈 자리가 없습니다.");
                        return;
                    }
                    else if (now >= nextRequest)
                    {
                        nextRequest = now + 2;
                        SendRequest(new Request { kind = "reserve", ticket = ticket, party = Party, members = rejoining ? new[] { Self } : queuedMembers });
                    }
                }
                var before = wasStarted ? new HashSet<ulong>(Roster.Keys) : null;
                ReadRoster();
                if (before != null) NoteArrivals(Roster.Keys.Where(id => !before.Contains(id)));
                if (Roster.ContainsKey(Self))
                {
                    admitted = true; seenRoster = true;
                    // Steam can deliver roster and grant metadata in either order.
                    if (IsLeader && !rejoining && Route(Party) != Match.ToString()) Set(Party, "route", Match.ToString());
                }
                if (!seenRoster && now > deadline) { RetryAdmission("입장 승인이 시간 초과되었습니다."); return; }
                if (seenRoster && !Roster.ContainsKey(Self)) { Fail("예약이 해제되었거나 파티원 연결이 끊겼습니다."); return; }
                Started = phase == "playing" && Roster.ContainsKey(Self);
            }
            SendAsk(now);
            int away = Away;
            Status = now < noteUntil ? Note
                   : Started ? (away > 0 ? $"경기 중 · {away}명이 자리를 비움 · 경기가 끝나기 전에 돌아올 수 있습니다." : "경기 시작! WASD 이동, Space 점프.")
                   : live ? "나갔던 경기로 돌아가는 중..."
                   : startWanted ? "경기 시작 준비 중: 물리 계산을 맡을 PC를 고르는 중..."
                   : $"대기실 {Roster.Count}/12명. 대기 중에도 움직일 수 있습니다.";
        }

        // A room whose host has gone without handing it on, or that its host closed. R503 [8]: it is a waiting
        // room (a host alone in it searched for an older room or cancelled before another party had its seats, or
        // it crashed), so a party searching in public matchmaking looks for another room instead of leaving the
        // queue: the leader goes on searching, and its members wait for the leader's next room rather than call
        // the party's search off (a member's own Cancel writes "cancel", which stops its leader's search too).
        void LeaveClosedRoom(bool live, float overdue)
        {
            const string ended = "방이 닫혔습니다. 파티로 돌아갑니다.";
            if (!live && IsLeader && Searching) { RetryAdmission("대기실이 닫혀 다른 방을 찾는 중...", ended); return; }
            if (!live && !IsLeader && Party != 0 && Route(Party) == Match.ToString() && overdue < ClosedRoomWait)
            { Status = "대기실이 닫혀 파티장이 다른 방을 찾는 중..."; return; }
            Fail(ended);
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
            // 1. The room's record ("host", "epoch", written by the owner), then the
            //    claims of the hosts that took over after it, one after another
            //    (HostElection.Follow). A claim counts only from the next host in
            //    line and only one epoch above what this PC can see (R295: any
            //    member's claim of any size used to be followed). Every PC reads the
            //    same room, so two successors that both stepped in settle on the one
            //    in line, and a PC that stepped in on a passing view of the room
            //    (a player it took for gone was not) steps back down.
            ulong recordHost = ParseId(Data(Match, "host"));
            int recordEpoch = ParseInt(Data(Match, "epoch"));
            var heirs = Heirs(present);
            var claims = heirs.ToDictionary(c => c.Id, c => c.Id == Self ? ownClaim : ParseInt(MemberData(Match, c.Id, "claim")));
            // R504: and whom each one passed over when it claimed (PassOver).
            var passed = heirs.ToDictionary(c => c.Id, c => c.Id == Self ? ownPast : ParseId(MemberData(Match, c.Id, "past")));
            ulong newest = HostElection.Follow(recordHost, recordEpoch, Math.Max(recordEpoch, epoch), PublishedSuccessors(), claims, heirs, out int newestEpoch, passed);
            bool here = newest != 0 && (newest == Self || present.Contains(newest));
            if (here && (newestEpoch > epoch || (newest != Host && (newestEpoch == epoch || Host == Self))))
                ChangeHost(newest, newestEpoch, newest == Self ? "이 PC가 물리 계산을 맡았습니다." : $"물리 담당 PC가 {Name(newest)} 님으로 바뀌었습니다.");

            // 2. A host gone from the room, or silent past the grace period, is
            //    replaced by its successor. Only the successor acts; everyone else
            //    follows its claim through step 1 on a later poll. A player still
            //    waiting for an empty seat is not in the match yet and stays out.
            //    R503 [5]: a silent host only once someone else does not hear it either (SilenceShared).
            bool seated = !IsHost && Roster.ContainsKey(Self);
            PublishQuiet(seated && hostSilence >= QuietAfter ? epoch : 0);
            if (seated)
            {
                bool gone = !present.Contains(Host);
                bool silent = hostSilence >= MigrateAfterSilence && now - hostSince >= MigrationGrace;
                if (gone || silent)
                {
                    ulong next = SuccessorOf(Host, present);
                    if (next == Self) { if (gone || SilenceShared(present)) Promote(); }
                    // R504: the next in line did not step in, and this PC is the one after it.
                    else if (next != 0 && PassOverDue(next, present, gone, now)) PassOver(next);
                    else if (next == 0 || (gone && orphanSince >= 0 && now - orphanSince > OrphanTimeout))
                    { Fail("물리 계산을 맡은 PC가 사라지고 이어받을 PC가 없어 경기가 끝났습니다. 파티로 돌아갑니다."); return false; }
                    else if (orphanSince < 0) orphanSince = now;
                }
                else { orphanSince = -1; passOverSince = -1; }
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

        // R503 [5]: member data "quiet" says which host this PC has not heard from for QuietAfter: that host's epoch,
        // empty while it hears it. Written on change only.
        void PublishQuiet(int silentEpoch)
        {
            string value = silentEpoch > 0 ? silentEpoch.ToString() : "";
            if (value == publishedQuiet) return;
            SteamMatchmaking.SetLobbyMemberData(Id(Match), "quiet", value);
            publishedQuiet = value;
        }

        // R503 [5]: a successor whose own connection dropped for a few seconds looked just like a silent host. It
        // stepped in, and once its connection was back its claim reached the room and everyone followed it, the
        // healthy host too, back to the stale match it had run alone (HOST.md §8: one bad link must not move the
        // host). Now the silence counts once another seated player who has loaded and hears the same host does
        // not hear it either. An outage freezes this PC's view of the room, so those players still look as if they
        // hear the host. With nobody to ask (two players, the rest bots or still loading), this PC's view decides.
        bool SilenceShared(HashSet<ulong> present)
        {
            bool asked = false;
            string mark = epoch.ToString();
            foreach (ulong id in present)
            {
                if (id == Self || id == Host || BotIdentity.IsBot(id) || !Roster.ContainsKey(id) || LoadPercent(id) < MatchStart.Ready) continue;
                if (MemberData(Match, id, "quiet") == mark) return true;
                asked = true;
            }
            return !asked;
        }

        // The successor steps in: one epoch above the newest it knows (step 1 has
        // already followed every claim that counts), announced as a claim until it
        // owns the room and can write the record itself. Claims that do not count
        // are not outbid (R295: a forged int.MaxValue made this one wrap round).
        void Promote()
        {
            // R504: a word left from an earlier pass-over would make this claim one too.
            if (ownPast != 0) { ownPast = 0; SteamMatchmaking.SetLobbyMemberData(Id(Match), "past", ""); }
            ownClaim = HostElection.NextEpoch(Math.Max(epoch, ParseInt(Data(Match, "epoch"))));
            SteamMatchmaking.SetLobbyMemberData(Id(Match), "claim", ownClaim.ToString());
            ChangeHost(Self, ownClaim, "물리 담당 PC의 연결이 끊겨 이 PC가 물리 계산을 이어받았습니다.");
            if (Owner(Match) == Self) { Set(Match, "host", Self.ToString()); Set(Match, "epoch", ownClaim.ToString()); }
        }

        // R504: a host gone or silent whose next in line never stepped in (frozen, still loading, or behind the same dead
        // router as the host) used to end the match: only that one may take over, and the rest gave up after
        // OrphanTimeout or their scene link's silence. The one after it in line waits PassOverAfter, then steps in.
        // A silent host counts as for Promote (SilenceShared), so this PC's own outage still moves nothing; the one
        // passed over stays a witness, so with nobody else seated a frozen next in line still blocks (as before).
        bool PassOverDue(ulong next, HashSet<ulong> present, bool gone, float now)
        {
            var heirs = Heirs(present);
            if (HostElection.PassOver(PublishedSuccessors(), heirs.Select(c => c.Id).ToList(), Host, next, heirs) != Self ||
                (!gone && !SilenceShared(present)))
            { passOverSince = -1; return false; }
            if (passOverSince < 0) passOverSince = now;
            return now - passOverSince >= PassOverAfter;
        }

        // Says whom it passed over before it claims (HostElection.Follow counts the claim only with that word), two
        // epochs up: if the one passed over claims the epoch between after all, it is followed first and this PC after it.
        void PassOver(ulong next)
        {
            ownPast = next;
            SteamMatchmaking.SetLobbyMemberData(Id(Match), "past", next.ToString());
            ownClaim = HostElection.NextEpoch(HostElection.NextEpoch(Math.Max(epoch, ParseInt(Data(Match, "epoch")))));
            SteamMatchmaking.SetLobbyMemberData(Id(Match), "claim", ownClaim.ToString());
            ChangeHost(Self, ownClaim, $"물리 담당 PC도 다음 차례({Name(next)})도 응답하지 않아 이 PC가 물리 계산을 이어받았습니다.");
            if (Owner(Match) == Self) { Set(Match, "host", Self.ToString()); Set(Match, "epoch", ownClaim.ToString()); }
        }

        // The current host gives the role away: at the start, when it is too
        // slow, or when it leaves a live match.
        void HandOff(ulong next, IEnumerable<ulong> ranked, string note, bool notify = true)
        {
            int nextEpoch = HostElection.NextEpoch(epoch);
            Set(Match, "host", next.ToString()); Set(Match, "epoch", nextEpoch.ToString());
            Set(Match, "successors", HostElection.Encode(ranked.Where(id => id != next)));
            SteamMatchmaking.SetLobbyOwner(Id(Match), Id(next));
            ChangeHost(next, nextEpoch, note, notify);
        }

        void ChangeHost(ulong next, int newEpoch, string note, bool notify = true)
        {
            ulong previous = Host;
            Host = next; epoch = newEpoch; hostSilence = 0; orphanSince = -1; passOverSince = -1;
            float now = Time.realtimeSinceStartup;
            hostSince = now;
            if (previous == next) return;
            Diagnose($"물리 담당 바뀜: {Who(previous)} → {Who(next)}" + (string.IsNullOrEmpty(note) ? "" : $" ({note})"));
            // R525: a PC that becomes the host starts from the room's roster, the one the host before it wrote last.
            // Its own copy was its last read as a player, a poll or more old: at the start (StartGame moves the role to
            // the best PC in the same poll that seats the last arrival) the new host published that copy and threw out
            // whoever had sat down since ("예약이 해제되었거나 파티원 연결이 끊겼습니다", found in a six-player simulation).
            if (next == Self && Match != 0) ReadRoster();
            lastHandoff = now;
            // R536: whoever hosts now weighs the room afresh from the players it finds (ElectLive).
            liveElectorate = null; liveSeen = "";
            Note = note; noteUntil = now + 6;
            // The next host sets the room's door afresh.
            joinable = -1;
            if (notify) HostChanged?.Invoke(previous, next);
        }

        // ---- The waiting room's host (R159, 승규: "입장씬부터 PC 성능 1등이 방장") ----

        // The waiting room's host hands the room to the best PC in it: machine
        // scores (HostElection.StartHost), each player seated and measured. A score
        // only rises (each PC's best sitting so far, R294), so the order settles and does not flicker.
        void ElectWaiting(HashSet<ulong> present, float now)
        {
            if (now < nextWaitElect || now - lastHandoff < WaitingHandoffCooldown || startWanted) return;
            nextWaitElect = now + 1;
            var candidates = Candidates(present).Where(c => c.Id == Self || Seated(c.Id, present)).ToList();
            ulong next = HostElection.StartHost(Self, candidates, HostElection.StartMargin);
            bool move = next != Self && !HostElection.Unknown(Pick(candidates, next));
            // R294: what the election compared, whenever that changes (a player in or out, a new
            // score), so Player.log on the host says why the role stayed or moved.
            string seen = string.Join(" · ", candidates.Select(c =>
                $"{Name(c.Id)}{(c.Id == Self ? "(지금 담당)" : "")} 성능 {(c.BaseFitness > 0 ? c.BaseFitness.ToString() : "측정 전")} 핑 {c.PingMs}"));
            if (seen != lastWaitElection)
            {
                lastWaitElection = seen;
                Debug.Log($"[ChessFight] 물리 담당 정하기(대기실): {seen} → " + (move ? $"{Name(next)} 님에게 넘김" : "그대로"));
            }
            if (move) HandOffWaiting(next, $"PC 성능 1등인 {Name(next)} 님의 PC가 물리 계산을 맡습니다.");
        }

        // R525: who stands in the waiting room without a seat yet, and since when (SeatingJoiners holds the start).
        void TrackJoiners(HashSet<ulong> present, float now)
        {
            var seated = new HashSet<ulong>(reservations.Groups.SelectMany(g => g.Members));
            foreach (ulong id in unseatedSince.Keys.ToList())
                if (!present.Contains(id) || seated.Contains(id)) unseatedSince.Remove(id);
            foreach (ulong id in present)
                if (id != Self && !seated.Contains(id) && !BotIdentity.IsBot(id) && !unseatedSince.ContainsKey(id)) unseatedSince[id] = now;
        }

        // A player whose whole party has arrived: only they may host the waiting room
        // (a party still on its way can be released, and its members with it).
        bool Seated(ulong id, HashSet<ulong> present)
        {
            var g = reservations.Find(id);
            return g != null && g.Committed && present.Contains(id) && !BotIdentity.IsBot(id);
        }

        // The record first (the reservations, then who hosts), ownership last:
        // after it only the new host can write the room.
        void HandOffWaiting(ulong next, string note, bool notify = true)
        {
            PublishReservations();
            Set(Match, "host", next.ToString());
            SteamMatchmaking.SetLobbyOwner(Id(Match), Id(next));
            reservations.Clear();
            startWanted = false; startWaitSince = -1;
            admitted = seenRoster = true;
            waitingHandedTo = next;
            ChangeHost(next, epoch, note, notify);
        }

        // Everyone follows the room's record while it waits. The epoch stays as it
        // is: it numbers the hosts of a started match (StartGame writes 1).
        void FollowWaitingHost(float now)
        {
            var members = Members(Match);
            ulong record = ParseId(Data(Match, "host"));
            // A handover is done once Steam has moved the room.
            if (Owner(Match) != Self) waitingHandedTo = 0;
            // R295: the player we handed the room to left before Steam moved it to
            // them, so it is still ours: we take it back, with the reservations we
            // published for them. A host that vanishes without a word (a crash) still
            // ends the room (HOST.md): whoever Steam hands it to never handed it over.
            else if (waitingHandedTo != 0 && record == waitingHandedTo && !members.Contains(record))
            {
                waitingHandedTo = 0;
                Set(Match, "host", Self.ToString());
                adoptReservations = true;
                ChangeHost(Self, epoch, $"{Name(record)} 님이 물리 계산을 받기 전에 나가 이 PC가 다시 맡았습니다.");
                return;
            }
            if (record != 0 && record != Host && (record == Self || members.Contains(record)))
            {
                bool mine = record == Self;
                if (mine) adoptReservations = true;
                ChangeHost(record, epoch, mine ? "PC 성능 1등이라 이 PC가 물리 계산을 맡았습니다." : $"PC 성능 1등인 {Name(record)} 님의 PC가 물리 계산을 맡았습니다.");
            }
            // An old host still owning the room passes it on (a missed SetLobbyOwner).
            if (Host != Self && Owner(Match) == Self && members.Contains(Host) && now >= nextOwnerFix)
            { nextOwnerFix = now + 1; SteamMatchmaking.SetLobbyOwner(Id(Match), Id(Host)); }
        }

        [Serializable] sealed class ReservationData { public Reservation[] groups; }
        [Serializable] sealed class Reservation { public ulong leader, party; public string ticket; public ulong[] members; public int team; public bool committed; public long until; }

        // The reservations as room data, so the next waiting-room host takes them
        // over. Deadlines travel as Steam server time: every PC's own clock differs.
        void PublishReservations()
        {
            double server = ServerNow(); float local = Time.realtimeSinceStartup;
            var data = new ReservationData
            {
                groups = reservations.Groups.Select(g => new Reservation
                {
                    leader = g.Leader, party = g.Party, ticket = g.Ticket, members = g.Members, team = g.Team, committed = g.Committed,
                    until = (long)Math.Round(server + (g.Deadline - local))
                }).ToArray()
            };
            SetChanged("resv", JsonUtility.ToJson(data));
        }

        void LoadReservations()
        {
            var list = new List<TeamReservations.Group>();
            string text = Data(Match, "resv");
            double server = ServerNow(); float local = Time.realtimeSinceStartup;
            try
            {
                var data = string.IsNullOrEmpty(text) || text.Length > 8000 ? null : JsonUtility.FromJson<ReservationData>(text);
                if (data?.groups != null)
                    foreach (var r in data.groups)
                        if (r != null) list.Add(new TeamReservations.Group { Leader = r.leader, Party = r.party, Ticket = r.ticket, Members = r.members,
                                                                             Team = r.team, Committed = r.committed, Deadline = local + (r.until - server) });
            }
            catch (ArgumentException) { list.Clear(); }
            // Without the record (it never arrived), everyone seated keeps a seat of their own.
            if (list.Count == 0)
                foreach (var p in Roster.Values)
                    list.Add(new TeamReservations.Group { Leader = p.Id, Party = p.Id, Ticket = "seat:" + p.Id, Members = new[] { p.Id }, Team = p.Team, Committed = true, Deadline = local + 25 });
            reservations.Restore(list);
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
            if (next != 0) HandOff(next, HostElection.Rank(candidates), $"물리 담당 PC가 느려져 {Name(next)} 님의 PC가 이어받습니다.");
        }

        // R536 (user 11-10, rule 4: "방에 존재하는 유저에 변동이 있을 때마다 제일 좋은 PC(CPU, GPU 통합)가 물리 담당 PC가 된다";
        // asked whether a better PC that comes into an empty seat of a running match should take the role at once:
        // "바로 넘기기"). R533 kept the host through a running match unless it left, went silent or stayed slow. Now
        // the host weighs the room again whenever the players who could carry the match change - seated, here, their
        // match scene loaded, their machine score known to the room - and hands the match to the best of them if that
        // PC is LiveMargin better (machines alone, as at the start: HostElection.StartHost).
        // Once for each change, not all the time: ratings move a little with the estimated pings, and a rule that
        // watched them would pass the role back and forth. Not around the shared start (the loading screens wait on
        // this host for it, R502), not right after a handover, not once the match is decided.
        void ElectLive(HashSet<ulong> present, float now)
        {
            if (Owner(Match) != Self || MatchEnded) return;
            var all = Candidates(present);
            var able = all.Where(c => c.Id == Self || (c.BaseFitness > 0 && LoadPercent(c.Id) >= MatchStart.Ready)).ToList();
            string electorate = HostElection.Encode(able.Select(c => c.Id));
            // A host that has just taken the match (the start, a handover) takes the room as it finds it: whoever
            // gave it the role had weighed them.
            if (liveElectorate == null) { liveElectorate = liveSeen = electorate; return; }
            if (electorate == liveElectorate) { liveSeen = electorate; return; }
            if (electorate != liveSeen) { liveSeen = electorate; liveSeenSince = now; }
            // Not before the match has run a moment past the shared start (every screen settles into it then), nor
            // right after a handover, nor before the change has stood a moment.
            if (StartAt == 0 || SharedNow() < StartAt + LiveElectSettle || now - lastHandoff < LiveElectCooldown || now - liveSeenSince < LiveElectSettle) return;
            liveElectorate = electorate;
            ulong next = HostElection.StartHost(Self, able, HostElection.LiveMargin);
            Debug.Log("[ChessFight] 물리 담당 정하기(경기 중): " + string.Join(" · ", able.Select(c =>
                $"{Name(c.Id)}{(c.Id == Self ? "(지금 담당)" : "")} 성능 {(c.BaseFitness > 0 ? c.BaseFitness.ToString() : "측정 전")} 핑 {c.PingMs}")) +
                " → " + (next != Self ? $"{Name(next)} 님에게 넘김" : "그대로"));
            if (next != Self) HandOff(next, HostElection.Rank(all), $"성능이 더 좋은 {Name(next)} 님의 PC가 물리 계산을 넘겨받습니다.");
        }

        void ResetHostState()
        {
            epoch = ownClaim = 0; publishedFit = publishedBase = 0; publishedLoc = publishedQuiet = ""; hostSilence = 0; hostSince = -1; orphanSince = -1;
            ownPast = 0; passOverSince = -1; endWanted = false;
            waitingHandedTo = 0;
            nextFitPublish = nextSuccessors = nextOwnerFix = noteUntil = 0; lastHandoff = -1000; Note = "";
            liveElectorate = null; liveSeen = ""; liveSeenSince = 0;
            startWanted = adoptReservations = false; startWaitSince = -1; nextWaitElect = 0; lastWaitElection = "";
            joinable = -1;
            unseatedSince.Clear();
            Manager = lastManager = 0; asks.Clear(); asksDone.Clear();
        }

        // ---- The seats of a started match (R538, Starters) ----

        // The players this match started with and the seats that stay theirs, as the room keeps them. Written once, at
        // the start; every host after reads the same list (rule 15: nothing of the match lives on the host PC alone).
        List<Starters.Seat> StartersOf() => Starters.Decode(Data(Match, "starters"));

        // The host's round in a started match: how many of each team are away, and whether Steam lets anyone into
        // the room - only while someone who may come back is away and the match is undecided. The room is never
        // offered to matchmaking again ("open" 0): nobody new comes into a started match (R538).
        void PublishSeats()
        {
            var starters = StartersOf();
            int away0 = Starters.Away(starters, Roster.Keys, 0), away1 = Starters.Away(starters, Roster.Keys, 1);
            SetChanged("free0", away0.ToString()); SetChanged("free1", away1.ToString());
            SetChanged("open", "0");
            int door = away0 + away1 > 0 && !MatchEnded ? 1 : 0;
            if (joinable != door) { SteamMatchmaking.SetLobbyJoinable(Id(Match), door == 1); joinable = door; }
        }
        void SetChanged(string key, string value) { if (Data(Match, key) != value) Set(Match, key, value); }

        // A seat request in a started match: only from a player the match started with, for themselves alone, and
        // they get the seat they had (team and place). Anyone else - a searcher with a stale result, a room's number
        // typed in, a made-up member - is refused, and so is everyone once the match is decided.
        void SeatLate(ulong sender, Request request)
        {
            string grant = "grant_" + sender;
            // The same request again while its answer is on the way.
            if (Roster.ContainsKey(sender)) { Set(Match, grant, request.ticket + "|ok"); return; }
            if (!TeamReservations.ValidRequest(sender, request.party, request.ticket, request.members) || request.members.Length != 1 ||
                MatchEnded || !Starters.SeatOf(StartersOf(), sender, out var seat) ||
                Roster.Values.Any(p => p.Team == seat.Team && p.Slot == seat.Slot))
            { Set(Match, grant, request.ticket + "|full"); return; }
            Roster[sender] = PawnMotor.Spawn(sender, seat.Team, seat.Slot);
            Set(Match, grant, request.ticket + "|ok");
            Diagnose($"나갔던 {Who(sender)} 님이 자기 자리로 돌아옴");
            NoteArrivals(new[] { sender });
            PublishMembers();
            PublishSeats();
        }

        // "○○ 님이 경기에 돌아왔습니다" for players seated again in a started match.
        void NoteArrivals(IEnumerable<ulong> ids)
        {
            var list = ids.Where(id => id != Self).ToList();
            if (list.Count == 0) return;
            Note = list.Count == 1 ? $"{Name(list[0])} 님이 경기에 돌아왔습니다." : $"{list.Count}명이 경기에 돌아왔습니다.";
            noteUntil = Time.realtimeSinceStartup + 6;
        }
        // A public search goes on to the next room; anything else ends with `alone`, the reason for the player.
        // R503 [7]: also a room joined by its number. Its party asked for that room only, but a public one turned the
        // refusal into a public search for the party's own mode, and a party with bots (release builds) was refused
        // by every public host, searched again for ever, or made a public room of its own with the bots in it.
        void RetryAdmission(string message, string alone = null)
        {
            if (IsLeader && Searching && !privateRoom && !joinedByNumber)
            {
                Diagnose("방에서 나와 다른 방을 찾음: " + message);
                LeaveMatchInternal(); Set(Party, "route", "search"); Status = message; nextSearch = Time.realtimeSinceStartup + UnityEngine.Random.Range(1f, 3f);
            }
            else Fail(alone ?? message);
        }
        // A line someone typed, from the lobby it arrived in.
        void Hear(ulong lobby, ulong sender, string wire)
        {
            if (!ChatText.TryDecode(wire, out var channel, out int team, out string text)) return;
            // Party lines count only in the party lobby, match lines only in the match room.
            if ((channel == ChatChannel.Party) != (lobby == Party)) return;
            // Every PC in the room receives team lines: show only our own team's, and
            // only from someone on that team.
            if (channel == ChatChannel.Team &&
                (!Roster.TryGetValue(Self, out var me) || me.Team != team ||
                 !Roster.TryGetValue(sender, out var them) || them.Team != team)) return;
            ChatRooms();
            Chat.Add(channel, sender, Name(sender), text, false, sender == Self, Time.realtimeSinceStartup);
        }
        // A new party or a new match room starts its channels afresh.
        void ChatRooms()
        {
            if (chatParty != Party)
            {
                chatParty = Party;
                Chat.Clear(ChatChannel.Party);
                chatPartyMembers.Clear();
                if (Party != 0) chatPartyMembers.UnionWith(Members(Party));
            }
            if (chatMatch != Match)
            {
                chatMatch = Match;
                Chat.Clear(ChatChannel.All); Chat.Clear(ChatChannel.Team);
                chatMatchPlayers.Clear(); chatMatchLive = false;
            }
        }
        // Notices in the log: who joined or left the party, and, once a match has
        // started, who left it or took an empty seat. Bots come and go silently.
        void FollowChat()
        {
            ChatRooms();
            double now = Time.realtimeSinceStartup;
            if (Party != 0)
            {
                var members = Members(Party);
                foreach (ulong id in members.Except(chatPartyMembers).OrderBy(x => x))
                    Chat.Add(ChatChannel.Party, 0, "", $"{Name(id)} 님이 파티에 들어왔어요", true, false, now);
                foreach (ulong id in chatPartyMembers.Except(members).OrderBy(x => x))
                    Chat.Add(ChatChannel.Party, 0, "", $"{Name(id)} 님이 파티를 나갔어요", true, false, now);
                chatPartyMembers.Clear(); chatPartyMembers.UnionWith(members);
            }
            if (Match == 0 || !Started) return;
            var players = new HashSet<ulong>(Roster.Keys.Where(id => !BotIdentity.IsBot(id)));
            if (chatMatchLive)
            {
                foreach (ulong id in players.Except(chatMatchPlayers).OrderBy(x => x))
                    Chat.Add(ChatChannel.All, 0, "", $"{Name(id)} 님이 빈자리에 들어왔어요", true, false, now);
                foreach (ulong id in chatMatchPlayers.Except(players).OrderBy(x => x))
                    Chat.Add(ChatChannel.All, 0, "", $"{Name(id)} 님이 경기를 나갔어요", true, false, now);
            }
            chatMatchLive = true;
            chatMatchPlayers.Clear(); chatMatchPlayers.UnionWith(players);
        }
        void SendRequest(Request request)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
            if (Match != 0 && bytes.Length <= chatBuffer.Length) SteamMatchmaking.SendLobbyChatMsg(Id(Match), bytes, bytes.Length);
        }
        // Lobby chat carries two things: what players type (ChatText, in the party
        // lobby and the match room) and, in the match room only, seat requests for
        // the host as JSON.
        void OnChat(LobbyChatMsg_t c)
        {
            ulong lobby = c.m_ulSteamIDLobby;
            if (lobby == 0 || (lobby != Match && lobby != Party)) return;
            int length = SteamMatchmaking.GetLobbyChatEntry(Id(lobby), (int)c.m_iChatID, out CSteamID sender, chatBuffer, chatBuffer.Length, out EChatEntryType type);
            if (length <= 0 || length >= chatBuffer.Length || type != EChatEntryType.k_EChatEntryTypeChatMsg || !Members(lobby).Contains(sender.m_SteamID)) return;
            string text = Encoding.UTF8.GetString(chatBuffer, 0, length);
            if (text.StartsWith(ChatText.Prefix, StringComparison.Ordinal)) { Hear(lobby, sender.m_SteamID, text); return; }
            if (lobby != Match || !IsHost) return;
            // Only the room's owner can grant a seat. A host still waiting for
            // ownership (HOST.md; since R159 in a waiting room too) leaves requests
            // to the joiner's next try, two seconds later.
            if (Owner(Match) != Self) return;
            Request request;
            try { request = JsonUtility.FromJson<Request>(text); }
            catch (ArgumentException) { return; }
            if (request == null) return;
            // R533: the manager's word to the host (start, a bot, a player put out, the role handed on).
            if (request.kind == "manage") { Manage(sender.m_SteamID, request); return; }
            if (request.kind != "reserve" || string.IsNullOrEmpty(request.ticket) || request.ticket.Length > 64) return;
            // R533: nobody the manager put out of this room comes back into it.
            if (request.members != null && KickedIds(Match).Overlaps(request.members))
            { Set(Match, "grant_" + sender.m_SteamID, request.ticket + "|full"); return; }
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
        // The manager's "게임 시작" (R533): at once on a manager that hosts the room, else asked of the host until the
        // room has started or can no longer start.
        public void StartGame()
        {
            if (!IsManager) return;
            if (IsHost) { StartNow(); return; }
            if (CanStartGame) Ask("start");
        }
        void StartNow()
        {
            // A private test needs two pawns; bots count, so one tester plus a bot works.
            if (!HostCanStart) { startWanted = false; startWaitSince = -1; return; }
            // The host role goes to the PC that can carry the match best, if that
            // is clearly not the room's creator. Nothing is simulated yet, so this
            // is the cheapest moment to move it. The record is written before the
            // phase, so a client that sees "playing" already sees the right host,
            // and ownership moves last, because after it only the new host can write.
            var present = Members(Match);
            var candidates = Candidates(present);
            float now = Time.realtimeSinceStartup;
            // R159: a player who has only just come in (the last seat filling the room
            // starts it in the same poll) has not published its machine score yet and
            // would count as unknown, so the room's creator kept the role over a
            // stronger PC. The start waits a moment for it (PollMatch calls again).
            // A host not measured itself has nothing to compare with: no waiting then.
            var unknown = LocalFitness > 0 ? candidates.Where(c => c.Id != Self && HostElection.Unknown(c)).ToList() : new List<HostElection.Candidate>();
            if (unknown.Count > 0)
            {
                if (startWaitSince < 0) startWaitSince = now;
                if (now - startWaitSince < StartFitnessWait) { startWanted = true; return; }
            }
            startWanted = false; startWaitSince = -1;
            Started = true;
            var ranked = HostElection.Rank(candidates);
            ulong next = HostElection.StartHost(Self, candidates, HostElection.StartMargin);
            Debug.Log("[ChessFight] 물리 담당 정하기(경기 시작): " + string.Join(" · ", candidates.Select(c =>
                $"{Name(c.Id)}{(c.Id == Self ? "(지금 담당)" : "")} 성능 {c.BaseFitness} 지금 {c.Fitness} 핑 {c.PingMs}")) +
                $" → {Name(next)}" + (unknown.Count > 0 ? $" (성능을 {StartFitnessWait:0}초 기다려도 모름: {string.Join(", ", unknown.Select(c => Name(c.Id)))})" : ""));
            epoch = 1;
            Set(Match, "host", next.ToString()); Set(Match, "epoch", "1");
            Set(Match, "successors", HostElection.Encode(ranked.Where(id => id != next)));
            // R538: the match is these players' from here on (Starters): the size each team starts with ("seats", the
            // room's seats of rule 1-1) and who they are, with their seats ("starters") - the only ones who may come
            // back if they leave. Nobody has left yet, so the room closes to searches and joins.
            Set(Match, "seats", Starters.EncodeCapacity(Roster.Values.Count(p => p.Team == 0), Roster.Values.Count(p => p.Team == 1)));
            Set(Match, "starters", Starters.Encode(Roster.Values.Where(p => !BotIdentity.IsBot(p.Id)).OrderBy(p => p.Team).ThenBy(p => p.Slot)
                                                         .Select(p => new Starters.Seat { Id = p.Id, Team = p.Team, Slot = p.Slot })));
            PublishSeats();
            // R548: when it started, written with the phase, for the entrance screens' course reveal (BegunAt).
            Set(Match, "begun", MatchStart.FormatTime(SharedNow()));
            Set(Match, "phase", "playing");
            hostSince = lastHandoff = now;
            if (next == Self) return;
            SteamMatchmaking.SetLobbyOwner(Id(Match), Id(next));
            ChangeHost(next, 1, $"성능이 더 좋은 {Name(next)} 님의 PC가 물리 계산을 맡습니다.");
        }

        // ---- The room's manager (R533) ----

        // Everyone reads the manager from the room: the player on record while they are in it.
        void FollowManager(HashSet<ulong> present)
        {
            ulong record = ParseId(Data(Match, "manager"));
            ulong seen = record != 0 && (record == Self || present.Contains(record)) ? record : 0;
            if (seen == Manager) return;
            Manager = seen;
            if (seen == 0) return;
            if (lastManager != 0 && lastManager != seen)
            {
                Diagnose($"방장(관리) 바뀜: {Who(lastManager)} → {Who(seen)}");
                Note = seen == Self ? "이 PC가 방장이 되었습니다. 게임 시작과 방 관리를 맡아요." : $"방장이 {Name(seen)} 님으로 바뀌었습니다.";
                noteUntil = Time.realtimeSinceStartup + 6;
            }
            lastManager = seen;
        }

        // The host keeps the room's manager a player who is here: the one on record while they stay, else the next
        // (a manager that left, crashed, or lost its seat). Only the room's owner can write it.
        void KeepManager(HashSet<ulong> present)
        {
            ulong record = ParseId(Data(Match, "manager"));
            if (record != 0 && (record == Self || present.Contains(record)) && Roster.ContainsKey(record)) return;
            ulong next = NextManager(present, 0);
            if (next != record) Set(Match, "manager", next == 0 ? "" : next.ToString());
        }

        // Who manages next: the player seated longest, as far as the seats tell - the lowest seat number, white before
        // black (seats are given out in the order parties came) - of the people in the room.
        ulong NextManager(HashSet<ulong> present, ulong not) =>
            Roster.Values.Where(p => p.Id != not && !BotIdentity.IsBot(p.Id) && (p.Id == Self || present.Contains(p.Id)))
                         .OrderBy(p => p.Slot).ThenBy(p => p.Team).ThenBy(p => p.Id).Select(p => p.Id).FirstOrDefault();

        // A manager on its way out of a room it can write names the next one itself; otherwise the host does on its
        // next poll (KeepManager).
        void LeaveManagerBehind(HashSet<ulong> present)
        {
            if (Owner(Match) != Self || ParseId(Data(Match, "manager")) != Self) return;
            ulong next = NextManager(present, Self);
            Set(Match, "manager", next == 0 ? "" : next.ToString());
        }

        // "양도": the manager hands the role to another player in the room, before the start or in the match.
        public bool CanGiveManager(ulong id) =>
            IsManager && id != 0 && id != Self && !BotIdentity.IsBot(id) && Roster.ContainsKey(id) && Members(Match).Contains(id);
        public void GiveManager(ulong id)
        {
            if (!CanGiveManager(id)) return;
            if (IsHost && Owner(Match) == Self) GiveManagerNow(id); else Ask("give", id);
        }
        void GiveManagerNow(ulong id)
        {
            if (BotIdentity.IsBot(id) || !Roster.ContainsKey(id) || !Members(Match).Contains(id)) return;
            Set(Match, "manager", id.ToString());
        }

        // "내보내기": the manager puts a player out of the waiting room - with the party they came with (a party sits
        // together, rule 8), and none of them comes back into this room. Not once the match has started (a manager
        // could put the other team out), not the manager's own party (they leave the party instead), not a bot (the
        // bot tools take those out).
        public const string KickedMessage = "방장이 방에서 내보냈습니다.";
        public bool CanKick(ulong id) =>
            ManagesRoom && id != 0 && id != Self && !BotIdentity.IsBot(id) && Roster.ContainsKey(id) && !(Party != 0 && Members(Party).Contains(id));
        public void Kick(ulong id)
        {
            if (!CanKick(id)) return;
            if (IsHost) KickNow(id); else Ask("kick", id);
        }
        void KickNow(ulong id)
        {
            if (!RunsRoom) return;
            var group = reservations.Find(id);
            ulong manager = ParseId(Data(Match, "manager"));
            if (group == null || BotIdentity.IsBot(id) || Array.IndexOf(group.Members, manager) >= 0) return;
            var list = KickedIds(Match).ToList();
            foreach (ulong member in group.Members)
                if (!BotIdentity.IsBot(member) && !list.Contains(member)) list.Add(member);
            if (list.Count > KickedKept) list.RemoveRange(0, list.Count - KickedKept);
            Set(Match, "kicked", string.Join(",", list.Select(x => x.ToString(CultureInfo.InvariantCulture))));
            Diagnose($"방장이 내보냄: {Who(id)}" + (group.Members.Length > 1 ? $" (파티 {group.Members.Length}명 함께)" : ""));
            reservations.Remove(group.Leader);
            Set(Match, "grant_" + group.Leader, group.Ticket + "|full");
            PublishRoster();
        }
        // Who a room's manager has put out, as the room says.
        static HashSet<ulong> KickedIds(ulong lobby)
        {
            var ids = new HashSet<ulong>();
            foreach (string part in Data(lobby, "kicked").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (ids.Count < KickedKept && ulong.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) && id != 0) ids.Add(id);
            return ids;
        }

        // The manager's ask, on its way to the host: sent now and again until it is done or no longer makes sense - a
        // start until the room starts (the host may be waiting for the room to become its own, or for a player to be
        // seated), anything else a few times.
        void Ask(string op, ulong target = 0, int team = 0)
        {
            float until = Time.realtimeSinceStartup + (op == "start" ? 12f : 2.5f);
            // The same thing asked again while it is on its way (a second click) only keeps it going; a bot more is
            // one more each time.
            bool once = op == "start" || op == "give" || op == "kick" || op == "fill" || op == "clearbots";
            var same = once ? asks.Find(a => a.Op == op && a.Target == target) : null;
            if (same != null) { same.Until = until; return; }
            // Numbered upwards for good, across rooms and runs: the host does each number once.
            askSeq = Math.Max(askSeq + 1, (int)((long)ServerNow() & 0x3FFFFFFF));
            asks.Add(new PendingAsk { Op = op, Target = target, Team = team, Seq = askSeq, Until = until });
        }
        void SendAsk(float now)
        {
            if (asks.Count == 0) return;
            if (!IsManager) { asks.Clear(); return; }
            asks.RemoveAll(a => now > a.Until || (a.Op == "start" && !CanStartGame));
            foreach (var ask in asks)
            {
                if (now < ask.Next) continue;
                ask.Next = now + 1;
                SendRequest(new Request { kind = "manage", op = ask.Op, target = ask.Target, team = ask.Team, seq = ask.Seq });
            }
        }

        // The host does what the manager asked. Only the manager's word counts, each ask once; one the room is not
        // ready for yet (a host that has just taken it over, a start that cannot happen yet) waits for its repeat.
        void Manage(ulong sender, Request request)
        {
            if (sender != ParseId(Data(Match, "manager")) || request.seq <= 0 || string.IsNullOrEmpty(request.op)) return;
            if (!Started && adoptReservations) return;
            if (!asksDone.TryGetValue(sender, out var done)) asksDone[sender] = done = new HashSet<int>();
            if (done.Contains(request.seq)) return;
            switch (request.op)
            {
                case "start":
                    if (Started) break;
                    if (!HostCanStart) return;
                    StartNow();
                    break;
                case "give": GiveManagerNow(request.target); break;
                case "kick": KickNow(request.target); break;
                case "fill": if (privateRoom || AllowPublicBots) FillNow(); break;
                case "addbot": if (privateRoom || AllowPublicBots) AddRoomBotNow(); break;
                case "clearbots": ClearRoomBotsNow(); break;
                case "addtest": AddTestBotNow(request.team); break;
                case "removetest": RemoveTestBotNow(request.team); break;
                default: return;
            }
            if (done.Count >= 64) done.Clear();
            done.Add(request.seq);
        }

        // ---- Coming back into the match this player left (R538) ----

        // The game keeps the match to come back to between runs (PlayerPrefs) through these two.
        public Func<string> LoadRejoin { get; set; }
        public Action<string> SaveRejoin { get; set; }
        public const float RejoinPoll = 3f;
        public const string RejoinRefused = "그 경기에는 돌아갈 수 없습니다(경기가 끝났거나 이 계정이 시작 멤버가 아닙니다).";
        ulong RejoinTarget
        {
            get
            {
                if (!rejoinLoaded) { rejoinLoaded = true; rejoinMatch = ParseId(LoadRejoin != null ? LoadRejoin() : ""); }
                return rejoinMatch;
            }
        }
        // The started match this player may go back into right now, 0 for none: it started that match, is not in a
        // room, the match is still running and undecided, on this game version. Nothing else counts - not a stop on
        // matchmaking (going back to the match one left is not matchmaking), not the party.
        public ulong RejoinMatch
        {
            get
            {
                ulong lobby = RejoinTarget;
                if (!Online || lobby == 0 || Match != 0 || pending || Searching || rejoinGone) return 0;
                // A party member goes back while its party is idle or still in that match - not while its leader has
                // the party in a search or another room (the party's seats there wait for this player).
                if (!IsLeader && Party != 0 && Route(Party) != "idle" && Route(Party) != lobby.ToString(CultureInfo.InvariantCulture)) return 0;
                if (Data(lobby, "kind") != "match" || Data(lobby, "phase") != "playing" || Data(lobby, "ended") == "1" || !BeatFresh(Data(lobby, "beat")) ||
                    Data(lobby, "protocol") != Protocol || Data(lobby, "build") != Build) return 0;
                return Starters.SeatOf(Starters.Decode(Data(lobby, "starters")), Self, out _) ? lobby : 0;
            }
        }
        public bool CanRejoin => RejoinMatch != 0;
        // The mode of that match, for the lobby's button; null when there is none.
        public GameModeInfo RejoinMode => CanRejoin ? GameModes.Resolve(Data(RejoinTarget, "mode")) : null;

        // "경기로 돌아가기": back into the room, to ask its host for the seat that is still this player's. Alone: a
        // party that also left comes back one by one, each by their own button.
        public void Rejoin()
        {
            ulong lobby = RejoinMatch;
            if (lobby == 0) return;
            Error = ""; rejoining = true; joinedByNumber = false; candidates.Clear();
            ticket = Guid.NewGuid().ToString("N");
            Status = "나갔던 경기로 돌아가는 중...";
            Diagnose($"나갔던 경기 {lobby}(으)로 돌아감");
            Join(lobby, false);
        }
        void ForgetRejoin()
        {
            if (rejoinMatch == 0 && rejoinLoaded) return;
            rejoinMatch = 0; rejoinLoaded = true; rejoinGone = false;
            SaveRejoin?.Invoke("");
        }
        // Seated in a started match: that is the one to come back to. In the lobby: ask Steam about it now and
        // again, and let it go once it is over, gone, or no longer lists this player.
        void TrackRejoin(float now)
        {
            if (Match != 0)
            {
                if (!Started || !Roster.ContainsKey(Self)) return;
                if (MatchEnded) { ForgetRejoin(); return; }
                if (RejoinTarget == Match) return;
                rejoinMatch = Match; rejoinLoaded = true; rejoinGone = false;
                SaveRejoin?.Invoke(Match.ToString(CultureInfo.InvariantCulture));
                return;
            }
            ulong lobby = RejoinTarget;
            if (lobby == 0 || pending) return;
            if (rejoinGone) { ForgetRejoin(); return; }
            // What Steam has told of the room so far (nothing until the first answer).
            if (Data(lobby, "kind") == "match" &&
                (Data(lobby, "phase") != "playing" || Data(lobby, "ended") == "1" || !BeatFresh(Data(lobby, "beat")) || !Starters.SeatOf(Starters.Decode(Data(lobby, "starters")), Self, out _)))
            { ForgetRejoin(); return; }
            if (now < nextRejoinPoll) return;
            nextRejoinPoll = now + RejoinPoll;
            SteamMatchmaking.RequestLobbyData(Id(lobby));
        }

        // ---- Deserters (R533, user 11-10 rule 3; LeavePenalty) ----

        // Counted and enforced in builds only: the game switches it on (not in the Editor) and keeps the list between
        // runs (PlayerPrefs) through these two.
        public bool LeavePenaltyOn { get; set; }
        public Func<string> LoadLeaves { get; set; }
        public Action<string> SaveLeaves { get; set; }
        List<double> leaves;
        List<double> Leaves => leaves ?? (leaves = LeavePenalty.Decode(LoadLeaves != null ? LoadLeaves() : ""));
        // Desertions that count now, and how long matchmaking is still stopped (0: it is not).
        public int Desertions => LeavePenaltyOn && Online ? LeavePenalty.Count(Leaves, ServerNow()) : 0;
        public double PenaltySeconds => LeavePenaltyOn && Online ? LeavePenalty.SecondsLeft(Leaves, ServerNow()) : 0;
        // Leaving now would be a desertion: a started public match that is not over, this player seated in it, with
        // another player still in it.
        public bool WouldDesert => LeavePenaltyOn && Online && Started && !MatchEnded && resultSeen != Match && !privateRoom &&
                                   Roster.ContainsKey(Self) && Roster.Keys.Any(id => id != Self && !BotIdentity.IsBot(id));
        // This PC saw the match's result (the match's link says so, on every PC): leaving from here on is nobody's
        // desertion, even before the room's own mark (`ended`: only the room's owner can write it, and a host that has
        // just changed writes it late) has arrived. Kept by room, so the next match starts clean.
        ulong resultSeen;
        public void SawResult() { if (Match != 0) resultSeen = Match; }
        // What leaving now would cost, for the in-game menu's warning; "" when it costs nothing.
        public string LeaveWarning
        {
            get
            {
                if (!WouldDesert) return "";
                int count = Desertions + 1;
                double ban = LeavePenalty.BanSeconds(count);
                return ban > 0 ? $"지금 나가면 24시간 안의 {count}번째 탈주라 매칭이 {ban / 60:0}분 동안 정지됩니다."
                               : $"지금 나가면 탈주 {count}회로 기록됩니다. 24시간 안에 {LeavePenalty.Free + 1}회부터 매칭이 정지됩니다.";
            }
        }
        // Why this party cannot search now: this player's stop, or a member's (party member data "ban"). Null: it can.
        string PenaltyBlock()
        {
            double left = PenaltySeconds;
            if (left > 0) return $"탈주 벌칙으로 매칭이 정지되어 있어요. {LeavePenalty.Clock(left)} 남음 (24시간 안에 {Desertions}번 나감)";
            if (Party == 0) return null;
            double now = ServerNow();
            foreach (ulong id in PartyMembers)
            {
                if (id == Self || !double.TryParse(MemberData(Party, id, "ban"), NumberStyles.None, CultureInfo.InvariantCulture, out double until) || until <= now) continue;
                return $"파티원 {Name(id)} 님이 탈주 벌칙으로 매칭 정지 중이에요. {LeavePenalty.Clock(until - now)} 남음";
            }
            return null;
        }
        // The same for the lobby's start button; "" when the party may search.
        public string PenaltyNotice => Online ? PenaltyBlock() ?? "" : "";

        // The player's own choice to leave (the in-game menu's 나가기): a desertion when the match is under way.
        public void LeaveByChoice()
        {
            string told = NoteDesertion();
            Cancel();
            if (told != null) Error = told;
        }
        // Writes the desertion down, if leaving now is one. What to tell the player, or null.
        string NoteDesertion()
        {
            if (!WouldDesert) return null;
            double now = ServerNow();
            leaves = LeavePenalty.Add(Leaves, now);
            SaveLeaves?.Invoke(LeavePenalty.Encode(leaves));
            int count = LeavePenalty.Count(leaves, now);
            double ban = LeavePenalty.BanSeconds(count);
            Diagnose($"탈주 기록: 24시간 안에 {count}번째" + (ban > 0 ? $", 매칭 정지 {ban / 60:0}분" : ""));
            return ban > 0 ? $"경기 도중 나가 탈주 {count}회째예요. 매칭이 {ban / 60:0}분 동안 정지됩니다."
                           : $"경기 도중 나가 탈주 {count}회로 기록됐어요. 24시간 안에 {LeavePenalty.Free + 1}회부터 매칭이 정지됩니다.";
        }
        // Development builds: forget this player's desertions (for testing the rule itself).
        public void ClearLeavePenalty()
        {
            leaves = new List<double>();
            SaveLeaves?.Invoke("");
        }
        // A stop still running, for the party's leader to see (party member data "ban": until when, Steam server time).
        ulong banParty;
        string publishedBan = "";
        void PublishPenalty()
        {
            if (Party == 0) { banParty = 0; return; }
            double until = LeavePenaltyOn ? LeavePenalty.BannedUntil(Leaves) : 0;
            string value = until > ServerNow() ? until.ToString("F0", CultureInfo.InvariantCulture) : "";
            if (Party == banParty && value == publishedBan) return;
            SteamMatchmaking.SetLobbyMemberData(Id(Party), "ban", value);
            banParty = Party; publishedBan = value;
        }
        // The host's race is decided (R125, Pawn Rush): nobody comes back into the room any more (R538: a player
        // who left could until now), and a new host after this one keeps it so (the mark lives in the room's data,
        // rule 15).
        // R504: the end is kept until this host owns the room. A match decided in the moment before Steam handed a new
        // host the room lost it: only the owner can write, this ran once, and once the room was its own the new host
        // opened the finished match's door again (the poll writes the end now, before the seats).
        public void EndMatch()
        {
            if (!Online || Match == 0 || !IsHost || Data(Match, "ended") == "1") return;
            endWanted = true;
            if (Owner(Match) != Self) return;
            WriteEnded();
            PublishSeats();
        }
        void WriteEnded() { if (endWanted && Data(Match, "ended") != "1") Set(Match, "ended", "1"); }
        // 11-11 (the user: an Editor host stopped Play, the room still read "playing" and the lobby still offered to go back):
        // the room's owner stamps the server clock into it every BeatSeconds while the match runs. A room whose stamp is
        // older than BeatStale has nobody running it (an Editor stopped, a crash) and is no match to go back to.
        public const float BeatSeconds = 5f, BeatStale = 20f;
        float nextBeat;
        void Beat(float now)
        {
            if (now < nextBeat || MatchEnded) return;
            nextBeat = now + BeatSeconds;
            Set(Match, "beat", ((long)ServerNow()).ToString(CultureInfo.InvariantCulture));
        }
        static bool BeatFresh(string beat) =>
            long.TryParse(beat, NumberStyles.Integer, CultureInfo.InvariantCulture, out long at) && ServerNow() - at <= BeatStale;
        public bool MatchEnded => Match != 0 && (endWanted || Data(Match, "ended") == "1");

        public void Cancel() => Cancel(Started ? "경기에서 나감" : Match != 0 ? "대기실에서 나감(취소)" : "매칭 취소");

        // The same, with why for Player.log (R525): a ConnectionStatus or a Status line was all a build kept of it.
        public void Cancel(string why)
        {
            if (!Online) return;
            Diagnose(why);
            Leave();
        }

        void Leave()
        {
            if (!Online) return;
            // Leaving a started match takes only this player out (R60). Its seat stays
            // its own (R538: it may come back, nobody else may take it), a leaving host
            // hands the match on, and party members still in it play on, whatever the
            // party's route says.
            bool alone = Started;
            generation++; pending = false; Searching = false; joinedByNumber = false; candidates.Clear();
            if (IsLeader) { Set(Party, "route", "idle"); SteamMatchmaking.SetLobbyJoinable(Id(Party), true); }
            else if (Party != 0)
            {
                cancelledFollower = true;
                // Only a search is called off for the whole party.
                if (!alone) SteamMatchmaking.SetLobbyMemberData(Id(Party), "cancel", Guid.NewGuid().ToString("N"));
            }
            LeaveMatchInternal(); Status = "파티로 돌아왔습니다.";
            PublishPlaying();
        }
        public void LeaveParty()
        {
            if (!Online) return;
            Cancel("파티를 나감"); LeavePartyInternal(); CreateParty();
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
                    // takes over. A waiting room goes to its best PC with everyone
                    // else's reservations (R159). A room with nobody else closes.
                    ulong next = 0;
                    List<ulong> ranked = null;
                    bool handed = false;
                    if (Started)
                    {
                        var inRoom = Members(Match);
                        // R533: a manager that hosts names the next manager before the room goes to the next host.
                        LeaveManagerBehind(inRoom);
                        ranked = HostElection.Rank(Candidates(inRoom).Where(c => c.Id != Self));
                        if (ranked.Count > 0) next = ranked[0];
                    }
                    else if (Owner(Match) == Self)
                    {
                        // R295: a room handed to us and left before we read its
                        // reservations: they are only in the room's record so far.
                        if (adoptReservations) { LoadReservations(); adoptReservations = false; }
                        // Our party goes with us.
                        var mine = reservations.Find(Self);
                        if (mine != null) reservations.Remove(mine.Leader);
                        var present = Members(Match);
                        PublishRoster();
                        LeaveManagerBehind(present);
                        ulong best = HostElection.StartHost(0, Candidates(present).Where(c => c.Id != Self && Seated(c.Id, present)).ToList(), HostElection.StartMargin);
                        if (best != 0) { HandOffWaiting(best, "", notify: false); handed = true; }
                    }
                    if (next != 0) HandOff(next, ranked, "", notify: false);
                    else if (!handed) { Set(Match, "phase", "closed"); Set(Match, "open", "0"); SteamMatchmaking.SetLobbyJoinable(Id(Match), false); }
                }
                SteamMatchmaking.LeaveLobby(Id(Match));
            }
            Match = Host = 0; Started = admitted = seenRoster = false; invalidHostSince = -1; Roster.Clear(); reservations.Clear();
            rejoining = false;
            loadPercent = 0; publishedLoad = -1; readySince = -1;
            ResetHostState();
            SessionChanged?.Invoke();
        }
        void Fail(string error) { Diagnose("나감: " + error); Leave(); Error = error; Status = error; }
        // For the scene flow: leave the match and tell the player why.
        public void Abort(string reason) { if (Online) Fail(reason); }

        // R525 (user 11-09, the first public test with six PCs: "매칭 중에 호스트PC가 10초 시점에서 계속 튕기는 현상 ... 제일
        // 시급"): every way out of a room, every change of its host and every wait for its owner goes to Player.log, with the
        // room as this PC sees it. Before, a player thrown out left no line anywhere (Fail only set the HUD's text), so the
        // builds' logs could not say which rule had done it. Rare lines (state changes only), never per frame.
        void Diagnose(string what)
        {
            string room = Match == 0 ? "방 없음"
                : $"방 {Match} · {Data(Match, "phase")} · 물리 담당 {Who(Host)} · 주인 {Who(Owner(Match))} · 기록 {Who(ParseId(Data(Match, "host")))}" +
                  $" · 방장 {Who(ParseId(Data(Match, "manager")))} · {(IsHost ? "내가 물리 담당" : "참가자")} · 명단 {Roster.Count}{(Roster.ContainsKey(Self) ? "(나 있음)" : "(나 없음)")}" +
                  $" · 방 인원 {Members(Match).Count}{(Started ? " · 경기 중" : "")}";
            string party = Party == 0 ? "" : IsLeader ? " · 파티장" : " · 파티원";
            Debug.Log($"[ChessFight] 세션: {what} | {room}{party} | t {Time.realtimeSinceStartup:0.0}");
        }

        string Who(ulong id) => id == 0 ? "-" : id == Self ? "나" : Name(id);

        // Tells the party which started match we are playing ("match"), so a leader
        // back in the lobby does not queue us again meanwhile (PartyStillPlaying).
        void PublishPlaying()
        {
            if (Party == 0) return;
            string value = Started ? Match.ToString() : "";
            if (Party == playingParty && value == publishedPlaying) return;
            SteamMatchmaking.SetLobbyMemberData(Id(Party), "match", value);
            playingParty = Party; publishedPlaying = value;
        }

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
            // R533: closing the game in the middle of a match is a desertion like the menu's 나가기.
            if (Online) { NoteDesertion(); Cancel("게임 종료"); LeavePartyInternal(); SteamFriends.ClearRichPresence(); }
            disposed = true;
            foreach (var c in callbacks) c.Dispose(); foreach (var c in calls) c.Dispose();
            callbacks.Clear(); calls.Clear();
            if (Online) SteamAPI.Shutdown(); Online = false;
        }
    }
}
