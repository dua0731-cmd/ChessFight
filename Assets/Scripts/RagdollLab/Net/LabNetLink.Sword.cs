using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Sword Fight skill test online (R121; 승규 님 10-11: "소드 파이트 테스트 씬에서만 온라인 테스트", Docs/Network/
    /// SKILLS_ONLINE.md §15). That scene has a SwordFightGame instead of a LabGame, and this same link runs it: the same
    /// carriers (a Steam test room, or the loopback test on one PC), transport, lag simulator, jitter buffer, poses and
    /// numbered skill moments as the other two skill tests. What is its own:
    /// <list type="bullet">
    /// <item>the fighters: the host's SwordFightPawns and their swords, the clients' puppets. The host puts every player on
    /// a side at random, as even as it goes - five players are three against two, the bigger side white or black at random
    /// (승규 님: "무작위로 흑3 백2 흑2 백3") - and F5 or the panel draws again. There are no dummies online: the players are
    /// each other's allies and enemies (being hit, the bishop saving a falling ally);</item>
    /// <item>the input (TypeSwordInput): the sword's button held and its clicks, the skill keys (F, E, the right click) as
    /// press counts, the control style, and the point the player's own camera aims at (the host has no camera of theirs);</item>
    /// <item>the state packet (SwordWire), with every snapshot: each fighter's side, life and sword (drawn with the poses
    /// it came with), its F and E skills as the effects and key cards read them (written when they arrive, D-S4), and the
    /// floor the rook breaks (each client runs the same collapse from the moment its bodies reach its start);</item>
    /// <item>the moments (SfFxEvent as SkillWire.ModeSwordFight), raised on a client when the bodies it draws reach them,
    /// so the effects and the sounds come with the bodies, each once;</item>
    /// <item>no slow motion and a hit stop that holds only the two pieces' picture on each screen (SwordFightSkillFx).</item>
    /// </list>
    /// The match scene's own Sword Fight link (SteamSwordFightLink, the lobby's) is not touched.
    /// </summary>
    public abstract partial class LabNetLink
    {
        SwordFightGame sword;
        SwordFightSkillBed swordBed;

        // The sides: the host's draw by player, and a client's copy of it by roster place.
        readonly Dictionary<ulong, int> swordTeams = new Dictionary<ulong, int>();
        readonly Dictionary<byte, int> remoteTeams = new Dictionary<byte, int>();
        readonly System.Random teamDraw = new System.Random();
        readonly List<ulong> teamScratch = new List<ulong>();
        // The host: each player's piece (kept through a new draw of the sides). A client: the piece it last asked for.
        readonly Dictionary<ulong, PieceKind> swordPieces = new Dictionary<ulong, PieceKind>();
        PieceKind swordPieceAsk, swordStartPiece;
        byte swordPieceSeq;
        bool swordStartAsked;

        readonly SwordWirePacket swordOut = new SwordWirePacket(), swordIn = new SwordWirePacket();
        uint lastSwordTime;
        bool swordApplied;
        int swordStateBytes, swordStateRate;

        // A client's swords, kept with the time of the snapshot they came with (drawn with those poses), and the two
        // snapshots Playback drew this frame.
        struct Weapon
        {
            public bool Alive, Drawn, Classic;
            public float Protection, Respawn, Flash;
            public Vector3 Grip;
            public Quaternion Turn;
        }

        sealed class WeaponFrame
        {
            public uint Time;
            public readonly Dictionary<byte, Weapon> ByRef = new Dictionary<byte, Weapon>();
        }

        readonly List<WeaponFrame> weaponFrames = new List<WeaponFrame>();
        readonly Stack<WeaponFrame> spareWeapons = new Stack<WeaponFrame>();
        uint playFrom, playTo;
        float playT;

        // The floor: the host's numbers for its collapses; a client's copies, and the ones waiting for their moment.
        readonly Dictionary<SwordFightEdgeFloor.Collapse, byte> collapseIds = new Dictionary<SwordFightEdgeFloor.Collapse, byte>();
        readonly List<SwordFightEdgeFloor.Collapse> collapseScratch = new List<SwordFightEdgeFloor.Collapse>();
        byte nextCollapseId = 1;
        readonly Dictionary<byte, SwordFightEdgeFloor.Collapse> remoteCollapses = new Dictionary<byte, SwordFightEdgeFloor.Collapse>();
        readonly Dictionary<byte, float> collapseSeen = new Dictionary<byte, float>();
        readonly List<(byte id, SwordWireCollapse data, double beginMs)> collapseWaiting = new List<(byte, SwordWireCollapse, double)>();
        readonly List<byte> collapseGone = new List<byte>();

        // Development presses (the loopback tests' commands, RunDev). For a while after any command the camera's aim goes
        // with every input: a test window is not in front, so the game reads no aim from it, and the aim of the one frame
        // a command came in was overwritten before the next input packet left (a queen dashing the camera's old way).
        bool devSkill, devEdge, devClick, devCancel, devJump;
        float devHoldUntil, devMoveUntil, devAimUntil;
        Vector3 devMove;

        // A client's own press, timed until the host's state of its piece changes (the test screen).
        float swordPressAt = -1f;
        SfStage swordPressStage, swordLastStage;
        SfEdge swordPressEdge, swordLastEdge;

        // ---------------------------------------------------------------- what the bed and the test screen read

        /// <summary>A match is on in the Sword Fight skill test scene.</summary>
        public static bool SwordOnline => Current != null && Current.sword != null && Current.matchActive;

        /// <summary>... and this PC is a client: its presses go to the host, it draws what the host sends.</summary>
        public static bool SwordClient => SwordOnline && !Current.NetIsHost;

        /// <summary>The online panel's text field has the keyboard: the scene's own keys (the piece numbers) wait.</summary>
        public static bool KeysTaken { get; private set; }

        /// <summary>This link runs the Sword Fight skill test (its scene has no LabGame).</summary>
        public bool SwordScene => sword != null;

        /// <summary>The fighters on each side now (the host's draw).</summary>
        public int WhiteCount => CountSide(0);
        public int BlackCount => CountSide(1);

        /// <summary>The Sword Fight state packets' bytes a second (sent to all clients on the host, received on a client).</summary>
        public int SwordStateBytesPerSecond => swordStateRate;

        /// <summary>How many times the host has drawn the sides this match.</summary>
        public int TeamDraws { get; private set; }

        /// <summary>The scene wants this link: a lab scene, or the Sword Fight skill test (R121).</summary>
        public static bool SceneWantsLink() =>
            FindFirstObjectByType<LabGame>() != null || FindFirstObjectByType<SwordFightSkillBed>() != null;

        /// <summary>The carrier's text field: the lab's own keys and the Sword Fight bed's wait while it has the keyboard.</summary>
        protected void SuppressKeys(bool on)
        {
            if (game != null) game.SuppressInput = on;
            KeysTaken = on;
        }

        float SwordPanelX => 390f;   // right of the bed's panel (8..378)

        int CountSide(int team)
        {
            int n = 0;
            if (sword == null) return 0;
            foreach (var f in sword.Fighters.Values) if (f != null && f.Pawn != null && f.Pawn.Team == team) n++;
            return n;
        }

        // ---------------------------------------------------------------- lifecycle

        void SwordAwake()
        {
            sword = FindFirstObjectByType<SwordFightGame>();
            if (sword == null) return;
            swordBed = FindFirstObjectByType<SwordFightSkillBed>();
            if (cam == null) cam = sword.CameraRig;
            // A host whose window is not in front must go on simulating (one player alt-tabbing would stop everyone),
            // and a client must go on receiving.
            Application.runInBackground = true;
        }

        void SwordEnter()
        {
            matchActive = true;
            // The piece this player was trying offline is asked for again once its fighter is there.
            swordStartPiece = sword.Local != null ? sword.Local.Pawn.Piece : PieceKind.Pawn;
            swordStartAsked = false;
            sword.ClearFighters();
            pawns.Clear();
            idOf.Clear();
            sword.Networked = true;
            sword.Authority = NetIsHost;
            sword.LocalId = NetSelf;
            sword.LeaveMatch = () => NetLeave("경기 나가기");
            sword.ResetRound();
            SwordFightEdgeFloor.Current?.ClearAll();
            ResetStream();
            ClearSwordStream();
            swordPieces.Clear();
            swordPieceSeq = 0;
            TeamDraws = 0;
            if (NetIsHost)
            {
                if (!RealMatch) DrawTeams();
                StartHostingGuard();
            }
            // Hand the mouse to the fighter: the panel would otherwise keep the cursor free all match.
            hudOpen = false;
            swordMatchSent = 0f;
            message = NetIsHost
                ? "방장으로 소드 파이트 시험을 시작했어요. 팀은 무작위예요 (F5로 다시 섞기)."
                : "방장에 접속했어요. 팀은 방장이 무작위로 나눠요.";
            if (Trace) Log($"sword match on ({NetKind}), {(NetIsHost ? "host" : "client")}, self {NetSelf}, host {NetHost}");
        }

        void SwordExit()
        {
            matchActive = false;
            StopHostingGuard();
            var piece = sword.Local != null ? sword.Local.Pawn.Piece : PieceKind.Pawn;
            sword.ClearFighters();
            pawns.Clear();
            idOf.Clear();
            SwordFightEdgeFloor.Current?.ClearAll();
            ClearSwordStream();
            sword.Networked = false;
            sword.Authority = true;
            sword.LocalId = 1;
            sword.LeaveMatch = null;
            sword.ResetRound();
            sword.SpawnOffline();
            // Back offline with the piece it had.
            if (sword.Local != null && piece != PieceKind.Pawn && swordBed != null) swordBed.SetPieceNow(sword.Local, piece);
            ResetStream();
            if (Trace) Log("sword match off");
        }

        void ClearSwordStream()
        {
            swordTeams.Clear();
            remoteTeams.Clear();
            foreach (var w in weaponFrames) spareWeapons.Push(w);
            weaponFrames.Clear();
            swordApplied = false;
            lastSwordTime = 0;
            playFrom = playTo = 0;
            playT = 0f;
            collapseIds.Clear();
            remoteCollapses.Clear();
            collapseSeen.Clear();
            collapseWaiting.Clear();
            swordPressAt = -1f;
            swordLastStage = SfStage.None;
            swordLastEdge = SfEdge.None;
            devSkill = devEdge = devClick = devCancel = devJump = false;
            devHoldUntil = devMoveUntil = devAimUntil = 0f;
        }

        // ---------------------------------------------------------------- the roster and the sides

        void SyncSwordRoster()
        {
            scratchIds.Clear();
            scratchIds.AddRange(pawns.Keys);
            foreach (ulong id in scratchIds)
            {
                if (NetRoster.ContainsKey(id) && pawns[id] != null) continue;
                RemoveFighter(id);
                inputs.Remove(id);
            }
            if (NetIsHost) KeepTeams();
            foreach (var entry in NetRoster)
            {
                int team = SwordTeamOf(entry.Key, entry.Value);
                if (team < 0) continue;   // a client waits for the host's word on the side
                if (pawns.TryGetValue(entry.Key, out var pawn) && pawn != null)
                {
                    if (pawn.Team == team) continue;
                    RemoveFighter(entry.Key);   // drawn onto the other side: made again there
                }
                AddFighter(entry.Key, team);
            }
        }

        int SwordTeamOf(ulong id, LabSeat seat)
        {
            if (RealMatch) return seat.Team;   // R149: the room's sides (parties together, SESSION.md)
            if (NetIsHost) return swordTeams.TryGetValue(id, out int t) ? t : -1;
            byte place = SkillWire.Ref(seat.Team, seat.Slot);
            return remoteTeams.TryGetValue(place, out int team) ? team : -1;
        }

        void AddFighter(ulong id, int team)
        {
            int slot = 0;
            foreach (var other in sword.Fighters.Values) if (other != null && other.Pawn.Team == team) slot++;
            var f = sword.Add(id, team, Mathf.Min(slot, 5), RealMatch && BotIdentity.IsBot(id), NetName(id));
            pawns[id] = f.Pawn;
            idOf[f.Pawn] = id;
            if (NetIsHost && swordPieces.TryGetValue(id, out var piece) && piece != f.Pawn.Piece)
            {
                if (swordBed != null) swordBed.SetPieceNow(f, piece);
                else f.Pawn.SetPiece(piece);
            }
            if (id == NetSelf && !swordStartAsked)
            {
                swordStartAsked = true;
                if (swordStartPiece != PieceKind.Pawn) AskSwordPiece(swordStartPiece);
            }
        }

        void RemoveFighter(ulong id)
        {
            if (pawns.TryGetValue(id, out var pawn) && pawn != null) idOf.Remove(pawn);
            pawns.Remove(id);
            sword.Remove(id);
        }

        /// <summary>The host: a player who left frees its place; one who joined goes to the smaller side (a tie: either).</summary>
        void KeepTeams()
        {
            teamScratch.Clear();
            foreach (ulong id in swordTeams.Keys) if (!NetRoster.ContainsKey(id)) teamScratch.Add(id);
            foreach (ulong id in teamScratch) swordTeams.Remove(id);
            foreach (ulong id in NetRoster.Keys)
            {
                if (swordTeams.ContainsKey(id)) continue;
                int white = 0, black = 0;
                foreach (int t in swordTeams.Values) if (t == 0) white++; else black++;
                swordTeams[id] = white < black ? 0 : black < white ? 1 : teamDraw.Next(2);
            }
        }

        /// <summary>Everyone on a side at random, as even as it goes: five players are three against two, the bigger
        /// side white or black at random.</summary>
        void DrawTeams()
        {
            teamScratch.Clear();
            teamScratch.AddRange(NetRoster.Keys);
            for (int i = teamScratch.Count - 1; i > 0; i--)
            {
                int j = teamDraw.Next(i + 1);
                (teamScratch[i], teamScratch[j]) = (teamScratch[j], teamScratch[i]);
            }
            int big = (teamScratch.Count + 1) / 2, bigSide = teamDraw.Next(2);
            swordTeams.Clear();
            for (int i = 0; i < teamScratch.Count; i++) swordTeams[teamScratch[i]] = i < big ? bigSide : 1 - bigSide;
            TeamDraws++;
            int white = 0;
            foreach (int t in swordTeams.Values) if (t == 0) white++;
            message = $"팀을 무작위로 나눴어요: 백 {white}명 · 흑 {swordTeams.Count - white}명";
            if (Trace) Log($"sides drawn: white {white}, black {swordTeams.Count - white}");
        }

        /// <summary>The host draws the sides again (F5, the panel): everyone starts again from their side's line with the
        /// piece they had, and the score from nothing.</summary>
        public void DrawSwordTeams()
        {
            if (!SwordOnline || !NetIsHost) return;
            foreach (var pair in sword.Fighters)
                if (pair.Value != null) swordPieces[pair.Key] = pair.Value.Pawn.Piece;
            DrawTeams();
            sword.ClearFighters();
            pawns.Clear();
            idOf.Clear();
            sword.ResetRound();
            SyncSwordRoster();
        }

        /// <summary>The bed's piece keys online: the host's own piece changes here; a client asks the host.</summary>
        public void AskSwordPiece(PieceKind kind)
        {
            if (!SwordOnline || !ChessPieces.IsValid((byte)kind)) return;
            if (NetIsHost)
            {
                ApplySwordPiece(NetSelf, kind);
                return;
            }
            swordPieceAsk = kind;
            swordPieceSeq = (byte)(swordPieceSeq % 255 + 1);
        }

        /// <summary>The host switches a player's piece (and starts its skills over).</summary>
        void ApplySwordPiece(ulong id, PieceKind kind)
        {
            if (sword == null || !ChessPieces.IsValid((byte)kind)) return;
            // R149: the prototype match decides (a pick, or the development key F8).
            if (sword.PieceRequest != null) { sword.PieceRequest(id, kind); return; }
            swordPieces[id] = kind;
            if (!sword.Fighters.TryGetValue(id, out var f) || f == null || f.Pawn.Piece == kind) return;
            if (swordBed != null) swordBed.SetPieceNow(f, kind);
            else
            {
                f.Pawn.SetPiece(kind);
                f.Skills?.ResetSkill();
            }
            if (Trace) Log($"{f.Pawn.DisplayName} → {kind}");
        }

        // ---------------------------------------------------------------- input

        /// <summary>This PC's input: the game's own reading (mouse and keys, nothing while a menu or the panel has the
        /// mouse), with the development presses on top. <paramref name="keys"/>: also read the skill keys here (a client;
        /// on the host the bed presses them itself, as offline).</summary>
        PawnInput SwordReadLocal(bool keys, out bool skill, out bool edge, out bool cancel)
        {
            var input = sword.ReadLocalInput();
            bool free = keys && (swordBed != null ? swordBed.KeysFree : sword.KeysFree && !KeysTaken);
            skill = (free && Input.GetKeyDown(swordBed != null ? swordBed.skillKey : KeyCode.F)) || devSkill;
            edge = (free && GameSettings.Pressed(GameKey.Skill)) || devEdge;
            cancel = (free && GameSettings.Pressed(GameKey.Grab)) || devCancel;
            float now = Time.unscaledTime;
            if (devSkill || devEdge || devCancel || devClick || devJump || now < devHoldUntil || now < devMoveUntil || now < devAimUntil)
            {
                // A loopback test's window is not in front: its input is empty unless the commands make it.
                if (cam != null) input.aim = cam.AimForward;
                input.ability2 = sword.ClassicControls;
                if (devClick) input.shove = true;
                if (devJump) input.jump = true;
                if (now < devHoldUntil) input.shoveHeld = true;
                if (now < devMoveUntil) input.move = devMove;
            }
            devSkill = devEdge = devCancel = devClick = devJump = false;
            return input;
        }

        /// <summary>The host's physics step: every other player's input, its presses one a step (two quick ones stay two).</summary>
        void SwordFixedUpdate()
        {
            float now = Time.realtimeSinceStartup;
            foreach (var pair in pawns)
            {
                if (pair.Key == NetSelf || pair.Value == null) continue;
                if (!sword.Fighters.TryGetValue(pair.Key, out var f) || f == null) continue;
                if (!inputs.TryGetValue(pair.Key, out var r) || now - r.received > InputTimeout)
                {
                    // Nothing heard for a moment: the sword goes back and the piece stands (as the match's link does).
                    f.StopCombat();
                    continue;
                }
                var held = r.input;
                var s = f.Skills;
                // One skill key a step, and a click not in the same step as one: F then a quick click in one packet is
                // the aim and then its use, not the click spent on the sword before the aim exists.
                bool key = false;
                if (s != null)
                {
                    // The bishop's and the knight's point: where that player's camera aims, not this PC's.
                    if (held.aimPointSet) s.OverrideAim(null, held.aimPoint);
                    if (Take(ref r.skill)) { s.PressKey(); key = true; }
                    else if (Take(ref r.ability)) { s.PressEdge(); key = true; }
                    else if (Take(ref r.ability2)) { s.CancelAim(); key = true; }
                }
                var input = new PawnInput
                {
                    move = new Vector3(held.move.x, 0f, held.move.y),
                    aim = held.aim,
                    sprint = held.sprint,
                    shoveHeld = held.shoveHeld,
                    ability2 = held.interact,   // the control style, absolute in every packet
                    jump = Take(ref r.jump),
                    shove = !key && Take(ref r.shove),
                };
                f.SetInput(input);
            }
        }

        // ---------------------------------------------------------------- host

        void SwordHostFrame()
        {
            var me = sword.Local;
            var local = SwordReadLocal(false, out bool skill, out bool edge, out bool cancel);
            if (me != null)
            {
                me.SetInput(local);
                if (me.Skills != null)
                {
                    if (skill) me.Skills.PressKey();
                    if (edge) me.Skills.PressEdge();
                    if (cancel) me.Skills.CancelAim();
                }
            }
            if (!RealMatch && Input.GetKeyDown(KeyCode.F5) && !KeysTaken && !ChatBox.KeysHeld) DrawSwordTeams();
            SendWorld();
            SendSwordMatch();
        }

        float swordMatchSent;

        /// <summary>R149: the prototype match's state (phase, clock, seed, score, pieces, the feed) as text, ten times a
        /// second (unreliable: the next one replaces a lost one).</summary>
        void SendSwordMatch()
        {
            if (sword.Match == null) return;
            float now = Time.realtimeSinceStartup;
            if (now - swordMatchSent < 0.1f) return;
            swordMatchSent = now;
            byte[] bytes = RagdollNetProtocol.Match(NetMatch, sword.Match.WireText(SwordRef));
            if (bytes == null) return;
            foreach (ulong id in NetRoster.Keys) if (id != NetSelf) Send(id, bytes);
        }

        /// <summary>With every snapshot: every fighter's side, life, sword and skills, and the floor's collapses.</summary>
        void SendSwordState(uint stamp)
        {
            swordOut.Clear();
            swordOut.HostTimeMs = stamp;
            swordOut.White = sword.White;
            swordOut.Black = sword.Black;
            swordOut.Remaining = sword.Remaining;
            swordOut.Finished = sword.Finished;
            foreach (var f in sword.Fighters.Values)
            {
                if (f == null || f.Pawn == null) continue;
                byte place = RefOf(f.Pawn);
                if (place == SkillWire.None) continue;
                Vector3 grip = f.WeaponOffset;
                var w = new SwordWireFighter
                {
                    Ref = place, Team = (byte)Mathf.Clamp(f.Pawn.Team, 0, 1),
                    Alive = f.Alive, Drawn = f.Drawn && f.Alive, Classic = f.ClassicControls,
                    Protection = f.Protection, Respawn = f.RespawnSeconds, Flash = f.HitFlash,
                    Wx = grip.x, Wy = grip.y, Wz = grip.z, WRot = RagdollNetProtocol.PackRotation(f.WeaponRotation),
                    Why = 0, EdgeTarget = SkillWire.None, PinCandidate = SkillWire.None, Carried = SkillWire.None,
                };
                if (f.Skills != null) f.Skills.CaptureNet(ref w, RefOf);
                swordOut.Fighters.Add(w);
            }
            var floor = SwordFightEdgeFloor.Current;
            collapseScratch.Clear();
            if (floor != null)
                foreach (var c in floor.Collapses)
                {
                    if (c.phase == SwordFightEdgeFloor.Phase.Done) continue;
                    if (!collapseIds.TryGetValue(c, out byte id))
                    {
                        id = nextCollapseId;
                        nextCollapseId = (byte)(nextCollapseId % 255 + 1);
                        collapseIds[c] = id;
                    }
                    collapseScratch.Add(c);
                    swordOut.Collapses.Add(new SwordWireCollapse
                    {
                        Id = id, By = c.by != null ? RefOf(c.by.Pawn) : SkillWire.None,
                        X0 = c.rect.xMin, Z0 = c.rect.yMin, X1 = c.rect.xMax, Z1 = c.rect.yMax,
                        Ox = c.outward.x, Oy = c.outward.y, Oz = c.outward.z,
                        Ax = c.along.x, Ay = c.along.y, Az = c.along.z,
                        Lx = c.lip.x, Ly = c.lip.y, Lz = c.lip.z,
                        Age = c.age, Crack = c.crack, Open = c.open,
                    });
                }
            // Numbers of collapses that are over are let go.
            if (collapseIds.Count > collapseScratch.Count)
            {
                var gone = new List<SwordFightEdgeFloor.Collapse>();
                foreach (var c in collapseIds.Keys) if (!collapseScratch.Contains(c)) gone.Add(c);
                foreach (var c in gone) collapseIds.Remove(c);
            }
            SwordWire.Fit(swordOut, RagdollNetProtocol.MaxBytes - RagdollNetProtocol.HeaderBytes);
            byte[] bytes = RagdollNetProtocol.SwordState(NetMatch, swordOut);
            foreach (ulong id in NetRoster.Keys)
            {
                if (id == NetSelf) continue;
                swordStateBytes += bytes.Length;
                Send(id, bytes);
            }
        }

        // ---------------------------------------------------------------- client

        void SwordClientFrame()
        {
            var local = SwordReadLocal(true, out bool skill, out bool edge, out bool cancel);
            if (local.jump) myJumps = PressCount.Add(myJumps);
            if (local.shove) myShoves = PressCount.Add(myShoves);
            if (skill) mySkills = PressCount.Add(mySkills);
            if (edge) myAbilities = PressCount.Add(myAbilities);
            if (cancel) myAbilities2 = PressCount.Add(myAbilities2);
            pending.Hold(local);
            pending.grab = false;
            pending.interact = local.ability2;   // the control style (F6), absolute: never a toggle that a lost packet flips

            var me = sword.Local;
            var s = me != null ? me.Skills : null;
            if (s != null)
            {
                s.NetPuppet = true;
                s.NetOwn = true;
                // D-S3: this player's aim from this PC's camera at once, and the point it sends for the bishop and the knight.
                s.NetViewAim(local.aim, cam != null ? cam.Cam : null);
                pending.aimPoint = s.AimPoint;
                pending.aimPointSet = s.HasSkill;
                TimeSwordPress(s, skill, edge, local.shove, cancel);
                float hold = Mathf.Clamp(roundTripMs / 1000f + 0.25f, 0.25f, 0.8f);
                if (skill) s.PredictPress(0, hold);
                if (local.shove) s.PredictPress(1, hold);
                if (cancel) s.PredictPress(2, hold);
            }
            if (skill || edge) SkillPresses++;

            float clock = Time.realtimeSinceStartup;
            if (clock >= nextInput)
            {
                nextInput += InputInterval;
                if (nextInput <= clock) nextInput = clock + InputInterval;
                pending.jumps = myJumps;
                pending.shoves = myShoves;
                pending.abilities = myAbilities;
                pending.abilities2 = myAbilities2;
                pending.skills = mySkills;
                pending.pieceAsk = (byte)swordPieceAsk;
                pending.pieceSeq = swordPieceSeq;
                pending.eventAck = inbox.Started ? inbox.Acked : (ushort)0;
                pending.roundTripMs = (ushort)Mathf.Clamp(Mathf.RoundToInt(roundTripMs), 0, ushort.MaxValue);
                pending.delayMs = (byte)Mathf.Clamp((int)Math.Round(delay.Milliseconds), 0, 255);
                pending.lossPercent = (byte)Mathf.Clamp((int)Math.Round(delay.LossShare * 100d), 0, 100);
                Send(NetHost, RagdollNetProtocol.SwordInput(NetMatch, ++sequence, NowMs(), pending));
            }

            Playback();
            SwordPlayback();
            DispatchEvents();

            // A host silent for a few seconds is replaced by its successor (Steam, HOST.md); leaving is the last resort.
            NetHostSilence(clock - lastReceive);
            if (clock - lastReceive > SilenceTimeout)
            {
                message = "방장이 자세 전송을 멈췄어요. 경기를 나갑니다.";
                NetLeave(message);
            }
        }

        /// <summary>Every client copy runs its skill timers between the host's packets (and knows whether it is this PC's own).</summary>
        void AdvanceSwordViews(float dt)
        {
            foreach (var pair in sword.Fighters)
            {
                var s = pair.Value != null ? pair.Value.Skills : null;
                if (s == null) continue;
                s.NetPuppet = true;
                s.NetOwn = pair.Key == NetSelf;
                s.AdvanceNetView(dt);
            }
        }

        /// <summary>Time a press that can do something (start a skill, go or call off an aim, an edge skill that is ready)
        /// until the host's state of this piece answers it.</summary>
        void TimeSwordPress(SwordFightSkills s, bool skill, bool edge, bool click, bool cancel)
        {
            var stage = s.HostStage;
            bool aiming = stage == SfStage.Aim;
            bool starts = skill && stage == SfStage.None && s.Cooldown <= 0.05f && s.HasSkill && s.EdgeStage == SfEdge.None
                          && s.Pawn != null && s.Pawn.State == PawnState.Active;
            bool timed = starts || (aiming && (skill || click || cancel)) || (edge && s.EdgeReady);
            float now = Time.realtimeSinceStartup;
            if (timed && swordPressAt < 0f)
            {
                swordPressAt = now;
                swordPressStage = stage;
                swordPressEdge = s.EdgeStage;
            }
            if (swordPressAt >= 0f && now - swordPressAt > 1.5f)
            {
                // The host never acted on it (a cooldown, knocked down, the condition gone meanwhile).
                ReactionsLost++;
                swordPressAt = -1f;
            }
        }

        /// <summary>The host's state of this client's own piece arrived: a skill started, and the timed press answered.</summary>
        void SwordStateAnswered(SwordFightSkills s)
        {
            var stage = s.HostStage;
            var edge = s.EdgeStage;
            if ((swordLastStage == SfStage.None && stage != SfStage.None) || (swordLastEdge == SfEdge.None && edge != SfEdge.None)) SkillStarts++;
            swordLastStage = stage;
            swordLastEdge = edge;
            if (swordPressAt < 0f || (stage == swordPressStage && edge == swordPressEdge)) return;
            LastReactionMs = (Time.realtimeSinceStartup - swordPressAt) * 1000f;
            Push(reactions, ref reactionNext, ref reactionCount, LastReactionMs);
            swordPressAt = -1f;
        }

        void ReceiveSwordState(byte[] bytes)
        {
            if (!RagdollNetProtocol.ReadSwordState(bytes, NetMatch, swordIn)) return;
            swordStateBytes += bytes.Length;
            // The swords and the lives go on the bodies' timeline: kept with the time of the snapshot they came with.
            KeepWeapons(swordIn);
            // The rest is news when it is newer than what was applied (an overtaken packet's sides and skills are old).
            bool newer = !swordApplied || unchecked((int)(swordIn.HostTimeMs - lastSwordTime)) > 0;
            if (!newer) return;
            swordApplied = true;
            lastSwordTime = swordIn.HostTimeMs;
            sword.ApplyScore(swordIn.White, swordIn.Black, swordIn.Remaining, swordIn.Finished);
            foreach (var w in swordIn.Fighters)
            {
                remoteTeams[w.Ref] = w.Team;
                var pawn = PawnOf(w.Ref);
                var s = pawn != null ? pawn.GetComponent<SwordFightSkills>() : null;
                if (s == null) continue;
                bool own = idOf.TryGetValue(pawn, out ulong id) && id == NetSelf;
                s.NetPuppet = true;
                s.NetOwn = own;
                s.ApplyNet(w, PawnOf);
                if (own) SwordStateAnswered(s);
            }
            // The floor: a collapse not seen before waits for the moment the bodies drawn reach its start.
            float now = Time.realtimeSinceStartup;
            foreach (var c in swordIn.Collapses)
            {
                collapseSeen[c.Id] = now;
                if (remoteCollapses.ContainsKey(c.Id) || collapseWaiting.Exists(x => x.id == c.Id)) continue;
                collapseWaiting.Add((c.Id, c, swordIn.HostTimeMs - c.Age * 1000d));
            }
            collapseGone.Clear();
            foreach (var pair in collapseSeen) if (now - pair.Value > 3f) collapseGone.Add(pair.Key);
            foreach (byte id in collapseGone)
            {
                collapseSeen.Remove(id);
                remoteCollapses.Remove(id);
            }
        }

        void KeepWeapons(SwordWirePacket p)
        {
            var frame = spareWeapons.Count > 0 ? spareWeapons.Pop() : new WeaponFrame();
            frame.Time = p.HostTimeMs;
            frame.ByRef.Clear();
            foreach (var w in p.Fighters)
                frame.ByRef[w.Ref] = new Weapon
                {
                    Alive = w.Alive, Drawn = w.Drawn, Classic = w.Classic, Protection = w.Protection, Respawn = w.Respawn, Flash = w.Flash,
                    Grip = new Vector3(w.Wx, w.Wy, w.Wz), Turn = RagdollNetProtocol.UnpackRotation(w.WRot),
                };
            // In time order (they come in order but for the odd overtaken one).
            int at = weaponFrames.Count;
            while (at > 0 && unchecked((int)(weaponFrames[at - 1].Time - frame.Time)) > 0) at--;
            if (at > 0 && weaponFrames[at - 1].Time == frame.Time)
            {
                spareWeapons.Push(frame);
                return;
            }
            weaponFrames.Insert(at, frame);
            while (weaponFrames.Count > 24)
            {
                spareWeapons.Push(weaponFrames[0]);
                weaponFrames.RemoveAt(0);
            }
        }

        /// <summary>The newest sword frame at or before <paramref name="time"/> (null if none).</summary>
        WeaponFrame WeaponsAt(uint time)
        {
            for (int i = weaponFrames.Count - 1; i >= 0; i--)
                if (unchecked((int)(weaponFrames[i].Time - time)) <= 0) return weaponFrames[i];
            return null;
        }

        /// <summary>After Playback drew the bodies: every sword where it was against its owner's hips at that moment
        /// (between the same two snapshots), and the floor's collapses whose start the bodies have reached.</summary>
        void SwordPlayback()
        {
            if (playbackMs <= 0d) return;
            var to = WeaponsAt(playTo);
            var from = WeaponsAt(playFrom);
            if (to != null)
                foreach (var f in sword.Fighters.Values)
                {
                    if (f == null || f.Pawn == null) continue;
                    byte place = RefOf(f.Pawn);
                    if (place == SkillWire.None || !to.ByRef.TryGetValue(place, out var b)) continue;
                    Vector3 grip = b.Grip;
                    Quaternion turn = b.Turn;
                    if (from != null && from != to && from.ByRef.TryGetValue(place, out var a) && a.Alive == b.Alive && a.Drawn == b.Drawn && a.Classic == b.Classic)
                    {
                        grip = Vector3.Lerp(a.Grip, b.Grip, playT);
                        turn = Quaternion.Slerp(a.Turn, b.Turn, playT);
                    }
                    f.ApplyRemote(b.Alive, b.Drawn, b.Protection, grip, turn, b.Respawn, b.Flash, b.Classic);
                }
            // Frames older than the two drawn go.
            while (weaponFrames.Count > 2 && unchecked((int)(weaponFrames[1].Time - playFrom)) <= 0)
            {
                spareWeapons.Push(weaponFrames[0]);
                weaponFrames.RemoveAt(0);
            }

            var floor = SwordFightEdgeFloor.Current;
            for (int i = collapseWaiting.Count - 1; i >= 0; i--)
            {
                var (id, c, beginMs) = collapseWaiting[i];
                if (playbackMs < beginMs) continue;
                collapseWaiting.RemoveAt(i);
                if (floor == null) continue;
                var rect = Rect.MinMaxRect(c.X0, c.Z0, c.X1, c.Z1);
                // A host change: the one this client already runs under the new host's number.
                var same = floor.Same(rect);
                if (same != null)
                {
                    remoteCollapses[id] = same;
                    continue;
                }
                var rook = PawnOf(c.By);
                var made = floor.BeginRemote(rect, new Vector3(c.Ox, c.Oy, c.Oz), new Vector3(c.Ax, c.Ay, c.Az), new Vector3(c.Lx, c.Ly, c.Lz),
                    c.Crack, c.Open, rook != null ? rook.GetComponent<SwordFightSkills>() : null, (float)((playbackMs - beginMs) / 1000d));
                if (made != null) remoteCollapses[id] = made;
                if (Trace) Log($"collapse {id} begins here {(playbackMs - beginMs):0} ms into it");
            }
        }

        /// <summary>A moment of the Sword Fight skills, raised here when the bodies drawn reach it. False if its piece is
        /// not here (it left).</summary>
        bool PlaySword(SkillWireEvent w, RagdollPawn by, RagdollPawn target, Vector3 at, Vector3 dir)
        {
            var s = by != null ? by.GetComponent<SwordFightSkills>() : null;
            if (s == null) return false;
            var e = new SfFxEvent { kind = (SfFxKind)w.Kind, by = s, target = target, at = at, dir = dir, size = w.Size, order = w.Count };
            // A pin and a slow start with their moment here, so the hands fly from the bishop (SwordFightSkillFx.PinMarks).
            var hit = target != null ? target.GetComponent<SwordFightSkills>() : null;
            if (hit != null && e.kind == SfFxKind.BishopPin) hit.NetPinned(e.size);
            if (hit != null && e.kind == SfFxKind.BishopSlow) hit.NetSlowed(e.size);
            SwordFightSkills.RaiseNet(e);
            if (Trace) Log($"moment {w.Seq} {e.kind} by {by.DisplayName} on {(target != null ? target.DisplayName : "-")}, {playbackMs - w.TimeMs:0} ms after its pose");
            return true;
        }

        // The host's own moments, recorded as they happen (its own effects listen to the same event).
        void OnSwordFx(SfFxEvent e)
        {
            if (!matchActive || sword == null || !NetIsHost || replaying || e.by == null) return;
            eventLog.Add(new SkillWireEvent
            {
                Mode = SkillWire.ModeSwordFight, Kind = (byte)e.kind, By = RefOf(e.by.Pawn), Target = RefOf(e.target),
                X = e.at.x, Y = e.at.y, Z = e.at.z, Dx = e.dir.x, Dy = e.dir.y, Dz = e.dir.z,
                Count = (byte)Mathf.Clamp(e.order, 0, 255), Size = e.size,
            }, NowMs());
        }

        // ---------------------------------------------------------------- the host role moving (Steam, HOST.md)

        void SwordHostChanged(bool nowHost, ulong next)
        {
            var floor = SwordFightEdgeFloor.Current;
            if (nowHost)
            {
                // The newest pose, not the drawn one, which is the playback delay behind.
                if (buffer.Count > 0)
                {
                    var newest = buffer[buffer.Count - 1];
                    for (int i = 0; i < newest.count; i++)
                    {
                        var pose = newest.At(i);
                        if (pawns.TryGetValue(pose.id, out var pawn) && pawn != null) pawn.ApplyNetworkPose(pose);
                    }
                }
                // The sides as they were drawn; the round from where the old host left it.
                swordTeams.Clear();
                foreach (var pair in pawns) if (pair.Value != null) swordTeams[pair.Key] = pair.Value.Team;
                foreach (var pair in sword.Fighters)
                {
                    var f = pair.Value;
                    if (f == null) continue;
                    swordPieces[pair.Key] = f.Pawn.Piece;
                    f.Authority = true;
                    if (f.Alive) f.Pawn.SetNetworkPuppet(false);
                    f.Skills?.AdoptNet();
                }
                sword.Authority = true;
                sword.TakeOverRound();
                // The collapses it was drawing are its own now; one still waiting for its moment starts at once.
                foreach (var (_, c, beginMs) in collapseWaiting)
                    if (floor != null && floor.Same(Rect.MinMaxRect(c.X0, c.Z0, c.X1, c.Z1)) == null)
                    {
                        var rook = PawnOf(c.By);
                        floor.BeginRemote(Rect.MinMaxRect(c.X0, c.Z0, c.X1, c.Z1), new Vector3(c.Ox, c.Oy, c.Oz), new Vector3(c.Ax, c.Ay, c.Az),
                            new Vector3(c.Lx, c.Ly, c.Lz), c.Crack, c.Open, rook != null ? rook.GetComponent<SwordFightSkills>() : null,
                            (float)Math.Max(0d, (lastSwordTime - beginMs) / 1000d));
                    }
                if (floor != null) foreach (var c in floor.Collapses) c.remote = false;
                tick = lastTick;   // clients keep accepting "newer" snapshots
                StartHostingGuard();
                message = "이 PC가 새 방장이 되었어요. 물리와 스킬을 이어서 계산해요.";
            }
            else
            {
                remoteTeams.Clear();
                foreach (var f in sword.Fighters.Values)
                {
                    if (f == null) continue;
                    byte place = RefOf(f.Pawn);
                    if (place != SkillWire.None) remoteTeams[place] = f.Pawn.Team;
                    f.Skills?.YieldNet();
                    f.StopCombat();
                    f.Pawn.SetNetworkPuppet(true);
                    f.Authority = false;
                }
                sword.Authority = false;
                if (floor != null) foreach (var c in floor.Collapses) c.remote = true;
                StopHostingGuard();
                lastTick = 0;
                message = $"방장이 {NetName(next)} 님으로 바뀌었어요.";
            }
            foreach (var w in weaponFrames) spareWeapons.Push(w);
            weaponFrames.Clear();
            collapseIds.Clear();
            remoteCollapses.Clear();
            collapseWaiting.Clear();
            collapseSeen.Clear();
            swordApplied = false;
            swordPressAt = -1f;
        }

        // ---------------------------------------------------------------- development commands (loopback tests)

        /// <summary>The Sword Fight test's own commands: <c>piece Bishop</c>, <c>skill</c> (F), <c>edge</c> (E),
        /// <c>click</c>, <c>hold 0.5</c> (the left button held), <c>rclick</c>, <c>jump</c>, <c>move x z seconds</c>,
        /// <c>teams</c> (the host draws the sides again), <c>classic</c> (F6). False for the others (look, lag, shot, trace,
        /// report...).</summary>
        bool SwordDev(string[] p, Func<int, float, float> number)
        {
            devAimUntil = Time.unscaledTime + 3f;   // "look" included: the aim it sets goes with the inputs after it
            switch (p[0])
            {
                case "piece":
                    if (p.Length > 1 && Enum.TryParse(p[1], true, out PieceKind kind)) AskSwordPiece(kind);
                    return true;
                case "skill":
                    devSkill = true;
                    return true;
                case "edge":
                    devEdge = true;
                    return true;
                case "click":
                    devClick = true;
                    return true;
                case "hold":
                    devHoldUntil = Time.unscaledTime + number(1, 0.5f);
                    return true;
                case "rclick":
                    devCancel = true;
                    return true;
                case "jump":
                    devJump = true;
                    return true;
                case "move":
                    devMove = Vector3.ClampMagnitude(new Vector3(number(1, 0f), 0f, number(2, 0f)), 1f);
                    devMoveUntil = Time.unscaledTime + number(3, 0.5f);
                    return true;
                case "teams":
                    DrawSwordTeams();
                    return true;
                case "classic":
                    sword.ToggleControls();   // F6: the click cut or the physical drag
                    return true;
            }
            return false;
        }

        void SwordReport(System.Text.StringBuilder sb)
        {
            var floor = SwordFightEdgeFloor.Current;
            sb.Append($", sword state {swordStateRate} B/s, sides white {WhiteCount} black {BlackCount} (draws {TeamDraws}), collapses {(floor != null ? floor.Collapses.Count : 0)}");
            foreach (var f in sword.Fighters.Values)
            {
                if (f == null || f.Pawn == null) continue;
                var s = f.Skills;
                sb.Append($", {f.Pawn.DisplayName} ({(f.Pawn.Team == 0 ? "white" : "black")}): {f.Pawn.Piece} {(f.Alive ? f.Pawn.State.ToString() : "out")}");
                if (s != null)
                    sb.Append($" {s.Stage} {s.StageTime:0.00}s cd {s.Cooldown:0.0}s, edge {s.EdgeStage} {s.EdgeTime:0.00}s cd {s.EdgeCooldown:0.0}s{(s.EdgeCondition ? " ready" : "")}"
                              + (s.PinLeft > 0f ? $" pinned {s.PinLeft:0.0}s" : "") + (s.SlowLeft > 0f ? $" slowed {s.SlowLeft:0.0}s" : ""));
                if (f.Pawn.NetworkPuppet) sb.Append(" (puppet)");
            }
            // The sounds this PC has played (the AI cannot hear them: the loopback checks count them on each client).
            var sfx = SwordFightSkillSfx.Current;
            if (sfx != null && sfx.Count.Count > 0)
            {
                sb.Append(", sounds");
                foreach (var pair in sfx.Count) sb.Append($" {pair.Key}×{pair.Value}");
            }
        }

        // ---------------------------------------------------------------- the panel

        void DrawSwordExtras()
        {
            var me = sword.Local;
            string mine = me == null ? "입장 중" : me.Pawn.Team == 0 ? "백팀" : "흑팀";
            GUILayout.Label($"팀 (방장이 무작위로 나눔): 백 {WhiteCount}명 · 흑 {BlackCount}명 · 나는 {mine}", Small);
            if (NetIsHost && GUILayout.Button("팀 다시 섞기 (F5) — 모두 출발선으로, 점수 처음부터", Button)) DrawSwordTeams();
        }
    }
}
