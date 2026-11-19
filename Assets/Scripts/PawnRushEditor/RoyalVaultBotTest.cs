using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using R = ChessFight.Network.RoyalVaultRules;

namespace ChessFight.PawnRush.Editor
{
    // R725: the Royal Vault (Late_3-3_RoyalVault, a draft for the late obstacle slot) crossed by twelve test bots in Play
    // Mode. The lifecycle, the scene, the bodies, the unstick jump, the watches and the records are the carriage yard's
    // harness (CarriageYardBotTest); the routes and every decision are the vault's, worked out in the section frame
    // (section.transform.InverseTransformPoint) from RoyalVaultRules alone - the bots never retype a device number. The
    // vault is instantiated alone at the origin under an inactive holder and configured as slot 5 before it wakes (option A),
    // and twelve real ragdoll pawns with the Pawn Rush movement and skills cross it:
    //   Run     blind: walks into every device as it comes (it still jumps the towers, it could not cross them without)
    //   Sprint  sprints, and times each device with the sprint's speed and small margins
    //   Jumper  a careful sprinter: times each device at the run's speed (its stamina may be out) with wide margins,
    //           waits for a free tower top and changes lane on the rest
    // Every combination of fork 1 (coin towers / hurdle corridor) and fork 2 (launch pad and drawer shelf / belts and press)
    // is run by each policy in each run. A probe runs one beat (fork1, hammers, fork2safe, fork2risky, plates, door) or the
    // whole vault (full; walk, milestone M0b's route check, is full with the per-beat report). A fall goes back, after the
    // kill volume's delay, to the highest checkpoint the bot reached, as the playtest does; every hit (YardKit.HitGiven),
    // wait, fall and anomaly - stuck, flung, pinned (also under the press die or in the door disc) - is counted per beat.
    // Not people and not online: the bots never shove, grab or look at a cue; they ask the rules the cues show.
    //
    // Batch (on a copy of the project, so the user's Editor can keep the original open; never -quit, it exits after Play
    // Mode; with a graphics device for the pictures):
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.RoyalVaultBotTest.RunPlayBatch
    //         [-vaultBotRuns 4] [-vaultBotSeed 7] [-vaultBotBeat walk|fork1|hammers|fork2safe|fork2risky|plates|door|full]
    //         [-vaultBotPieces pawn|mix] [-vaultBotNoCapture] -logFile <file>
    // Results in Logs/RoyalVaultBots: report.txt (read it - the exit code is 1 only for Play errors and harness failures),
    // crossings.csv, beats.csv, door.csv, hits.csv, trace.csv and the pictures.
    [InitializeOnLoad]
    public static class RoyalVaultBotTest
    {
        const string ActiveKey = "ChessFight.R725.VaultBots.Play";
        const string ExitKey = "ChessFight.R725.VaultBots.Exit";
        const string CodeKey = "ChessFight.R725.VaultBots.Code";
        const string Folder = "Logs/RoyalVaultBots";
        const string Report = "report.txt", CrossingsFile = "crossings.csv", BeatsFile = "beats.csv", DoorFile = "door.csv",
                     HitsFile = "hits.csv", TraceFile = "trace.csv";
        // The spec's columns first, in its order; the harness's additions after them.
        const string CrossingsHeader = "run,seed,bot,policy,piece,branch1,branch2,lane,finished,seconds,downs,staggers,falls,respawns,drop_to_belt," +
                                       "hammer1_wait,hammer2_wait,press_wait,belt_s,drawer_wait,plate_wait,door_wait,alone_s,max_speed,max_rise," +
                                       "stuck,flung,pinned,blind_gaveup,probe,bulkhead_wait,tower_wait,fork1_s,fork2_s,launches,shelf_landings," +
                                       "abyss_falls,hammer_double,both_hammers,press_pinches,door_pinches,unrecovered_falls,lost_s";
        const string BeatsHeader = "run,bot,beat,t_in,t_out,downs,falls,waits,probe,policy,piece,hits,lost_s";
        const string DoorHeader = "run,opening,opens_at,bots_waiting,first_arrival,last_arrival,first_pass,last_pass,bots_passed,probe";
        const string HitsHeader = "probe,run,bot,policy,t,kind,knockdown_s,push,x,y,z,piece,beat,back_m";
        const string TraceHeader = "probe,bot,policy,t,x,y,z,vx,vy,vz,tilt,ground,on_feet,move_x,move_z,jump,waiting,run,piece,beat,step," +
                                   "event,top_from,top_to,edge_m,length_m";
        const string SectionPath = "Assets/Prefabs/PawnRushSections/Late_3-3_RoyalVault.prefab";
        const string PawnPath = "Assets/Prefabs/RagdollPawn.prefab";
        const int BotCount = 12, Slot = 4, TeamSize = 6;
        // Seconds: the whole test; a full (or walk) crossing; a one-beat probe; the trace's step; the settle before a run.
        const float RuntimeLimit = 3600f, FullLimit = 240f, BeatLimit = 60f, TraceStep = .2f, SettleSeconds = 1f;

        // The vault's beats by the names section.BeatAt returns (RoyalVaultBuilder's SectionBeats, spec 1.16).
        const string BeatShelf = "3 서랍 선반", BeatTowers = "1 금화 탑", BeatCorridor = "1 허들 회랑", BeatForecourt = "0 앞뜰",
                     BeatJ1 = "J1 합류", BeatHammers = "2 다이얼 망치", BeatFork2 = "3 둘째 갈림", BeatSafe = "3 벨트 · 왕관 프레스",
                     BeatJ2 = "J2 합류", BeatPlates = "4 뒤집히는 판", BeatTerrace = "5 테라스", BeatDoor = "6 시간 자물쇠 문";
        static readonly string[] BeatOrder =
            { BeatForecourt, BeatTowers, BeatCorridor, BeatJ1, BeatHammers, BeatFork2, BeatSafe, BeatShelf, BeatJ2, BeatPlates, BeatTerrace, BeatDoor };
        // The same boxes, for a point the section's own beats miss (first match wins, as PawnRushSection.BeatAt).
        static readonly SectionBeat[] SpecBeats =
        {
            new SectionBeat(BeatShelf, new Vector3(2.5f, 7f, 89f), new Vector3(11.5f, 12f, 126f)),
            new SectionBeat(BeatTowers, new Vector3(-4.5f, -9f, 18f), new Vector3(4.5f, 6f, 50f)),
            new SectionBeat(BeatCorridor, new Vector3(-27f, -9f, .6f), new Vector3(-13.5f, 10f, 50f)),
            new SectionBeat(BeatForecourt, new Vector3(-15f, -9f, -.5f), new Vector3(15f, 9f, 18f)),
            new SectionBeat(BeatJ1, new Vector3(-23f, -9f, 50f), new Vector3(9f, 12f, 60f)),
            new SectionBeat(BeatHammers, new Vector3(-8f, -9f, 60f), new Vector3(8f, 16f, 83f)),
            new SectionBeat(BeatFork2, new Vector3(-15f, -9f, 83f), new Vector3(15f, 14f, 89f)),
            new SectionBeat(BeatSafe, new Vector3(-15f, -9f, 89f), new Vector3(15f, 18f, 126f)),
            new SectionBeat(BeatJ2, new Vector3(-15f, -9f, 126f), new Vector3(15f, 16f, 136f)),
            new SectionBeat(BeatPlates, new Vector3(-8f, -9f, 136f), new Vector3(8f, 16f, 156f)),
            new SectionBeat(BeatTerrace, new Vector3(-15f, -9f, 156f), new Vector3(15f, 20f, 178f)),
            new SectionBeat(BeatDoor, new Vector3(-15f, -9f, 178f), new Vector3(15f, 24f, 196.5f)),
        };

        // ------------------------------------------------------------------ the bots' rules (harness policy; device numbers are RoyalVaultRules')

        // Bulkheads: the wait line stands BulkheadWaitBack before a panel's middle; a crossing ends with the body past its far
        // face (the panel is 2 * PanelHalf thick): 2.1 m from the wait line straight through. Three places across each half,
        // BulkheadPitch apart.
        const double PanelHalf = .4, BulkheadWaitBack = 1.4;
        const float BulkheadPitch = .35f;
        // Hurdles, the line (HurdleLine): each hurdle 2 m off its hub, in the middle of a half - for a runner blind to the bars
        // the east half all the way (spec 5.2), for the others the half where that hurdle's bar comes at them head-on
        // (HurdleSide: east of hurdle 1, west of hurdle 2). From behind, a bar 2 m out moves about as fast as a runner (3.5 m/s
        // for hurdle 2 against 2.5-3.6) and closes on the body so slowly that over much of its sweep it stays at the body longer
        // than any jump stays over it (the startup check: from 20-40 % of the bar's phases no press gets through). Along
        // a hurdle's line a bot makes for the point HurdleLookAhead on, so one a hit threw off comes back to its line instead
        // of cutting across the hub's slow middle to the next point; it changes lines between the hurdles, out of both bars'
        // reach (HurdleClear from a hub), and is on a line HurdleRunIn before and after the reach, so it runs straight there
        // (the flight check takes the hips on straight: turning into the reach, the bar's tip met them on the ground).
        const float HurdleLookAhead = 2f, HurdleRunIn = .75f;
        // Hurdles, the jump (HurdleFlight): a press stepped over its flight every HurdleStep s, and HurdleAfter s past the
        // landing. Every length of the bar and of its shell in it is RoyalVaultRules', so the check follows a change of the bar
        // (R725 tuning: HurdleBarHigh 0.55 -> 0.5); the body's own lengths are the harness's.
        // - YardImpact judges the hit by the bar's own box (the builder's `solid`, HurdleBarLow..HurdleBarHigh): only while the
        //   bar comes at the hips (not once it has passed under them), and no face comes at a point more than YardBeside past
        //   one of its edges (YardImpact.Facing's "beside"). So it does not hit with the hips more than HurdleOver over the
        //   bar's middle: its box's half height and YardBeside, which is YardBeside over the bar's top.
        // - The hips stand HurdleHips over the feet. So while the bar is within HurdleLead ahead of the hips the feet must be
        //   over HurdleLeadFeet (the bar's top + YardBeside - HurdleHips: 0.6 m at the spec's 0.55 m top, 0.55 m at 0.5), and
        //   while its log is within HurdleTrail behind them (the body's half, R.BodyHalf: its feet and skirt), over its top.
        // - HurdleLead: the shell reaches HurdleBodyHalf out round the bar's line, and the probes' hurdle hits were logged with
        //   the hips up to 0.78 m off that line, a physics step after the shell caught the body (arms and skirt reach past
        //   it): HurdleArms more. HurdleTip: past the bar's tip a body is still met (the shell's cap).
        // - The bar has passed under the hips once it is behind them by no more than HurdleUnder (several steps of its fastest
        //   sweep). A good press is taken once no more than HurdleCentre s of its window's start is left over its end: the
        //   middle of the window.
        const double YardBeside = .3, HurdleArms = .45, HurdleHips = .25, HurdleTip = .2, HurdleUnder = .5, HurdleStep = .01, HurdleAfter = .1,
                     HurdleCentre = .02;
        static double HurdleOver => (R.HurdleBarHigh - R.HurdleBarLow) / 2 + YardBeside;
        static double HurdleLeadFeet => (R.HurdleBarLow + R.HurdleBarHigh) / 2 + HurdleOver - HurdleHips;
        static double HurdleLead => R.HurdleBodyHalf + HurdleArms;   // 0.9
        static double HurdleTrail => R.BodyHalf;
        static double HurdleClear => Math.Sqrt((R.HurdleReach + HurdleTip) * (R.HurdleReach + HurdleTip) + HurdleLead * HurdleLead);   // 4.25
        // Hammers: each wait line stands this far before its zone.
        const double HammerWaitBack = .5;
        // Drawers: each wait line stands DrawerWaitBack before where the drawer reaches a body's front (its box's near end less
        // the body's half), and a crossing is timed from it to past the drawer (DrawerCross). The spec's 0.05 m let a careful
        // bot that stood a little past its line be grazed by the face (four shoves at the line in 12 careful crossings); since
        // the drawers knock down (R725 tuning), the line stands 0.3 m further back.
        const double DrawerWaitBack = .35;
        // A drawer's knockdown carries the body off the shelf's open edge and 4 m down onto the belts. Until it is on its feet
        // again, and at most DrawerCarry s, what the carry does to it is reported besides (CarryWatch); it is judged stuck,
        // pinned and flung as everywhere else (Watch). Hit to running is about 2.7 s (RoyalVaultRules.DrawerKnockdown).
        const float DrawerCarry = 5f;
        // The pad's throw lasts 1.72 s. A thrown bot that has neither stood on the shelf nor come down short of it on its feet
        // FlightStall s after the throw is logged where its body is and how it lies (FLIGHT STALL), then goes on from there
        // under the usual watches. R725: twice in 192 crowded pad crossings a bot waited out the run in the Flight step (no
        // input there, so never judged stuck), and nothing said where it was.
        const float FlightStall = 6f;
        // Fork 2's belt choice: the speed Sprint and Jumper average once their stamina is out (spec 1.18).
        const double ForkSpeed = 3.1;
        // Plates: bots walk the inner side of each row, this far from the middle (a row switch is twice this).
        const float PlateInner = .8f;
        // Flung on the plates' terms (PlatesTerms): with the hips within PlateReach of the plate field (a foot or the skirt
        // still on a deck: seed 23's lift threw a pawn up at 8.4 m/s with its hips 0.26 m short of row 1, in J2's box), or
        // within PlateThrow s of standing on a deck (the hips no more than DeckStand over it, OnFeet's height).
        const float PlateReach = .5f, PlateThrow = .5f, DeckStand = .8f;
        // Towers: a jump goes within this of the edge; the Jumper waits while another stands this near the next top's middle; a
        // hop has come down once the hips are this near their ground (a pawn stands them 0.26 m up).
        const float TakeoffEdge = .2f, TopTaken = 1.3f, Touchdown = .45f;
        // Falls: a bot whose hips drop this far under where they were when it last stood is on its way down (no floor of the
        // vault lies that far under the one next to it, except the drawers' drop onto the belts, which it stands up from).
        const float DropBelow = 1.5f;
        // The hurdle corridor's opening from the forecourt: its east rail starts this far in (build spec 1.2 and 1.4, the
        // builder's layout; not a RoyalVaultRules number).
        const float CorridorOpenZ = 8f;
        // The press's crush shell lets a floored pawn through the die this long (spec 2.6: the sit and 0.6 s).
        static double PressGhost => R.PressSit + .6;
        // The hurdle corridor's chains (HurdleChains, information): the most knockdowns one bot takes from one hurdle within
        // this many seconds.
        const float ChainWindow = 4f;

        // The speed a policy times a crossing at: the Jumper the run's, the Sprint the sprint's.
        static double Speed(Policy rule) => rule == Policy.Jumper ? R.RunSpeed : R.SprintSpeed;
        static double BulkheadCross => BulkheadWaitBack + PanelHalf + R.BodyHalf;
        static double BulkheadNeed(Policy rule, double metres) => metres / Speed(rule) + (rule == Policy.Jumper ? .3 : .1);
        static double HammerNeed(Policy rule) => 2 * R.HammerZoneHalf / Speed(rule) + (rule == Policy.Jumper ? .6 : .3);
        static double DrawerLine(int i) => R.DrawerZ[i] - R.DrawerLong / 2 - R.BodyHalf - DrawerWaitBack;
        static double DrawerCross => R.DrawerLong + 2 * R.BodyHalf + DrawerWaitBack;
        static double DrawerNeed(Policy rule) => DrawerCross / Speed(rule) + (rule == Policy.Jumper ? .3 : .15);
        static double PressCross(Policy rule) => (R.PressBandZ1 - R.PressBandZ0) / Speed(rule) + (rule == Policy.Jumper ? .4 : .2);
        static double PlateEnter(Policy rule) => rule == Policy.Jumper ? 2.0 : 1.2;
        static double PlateLeave(Policy rule) => rule == Policy.Jumper ? 1.2 : .8;
        static double PlateTake(Policy rule) => rule == Policy.Jumper ? 1.5 : 1.0;
        static double DoorNeed(Policy rule) => rule == Policy.Jumper ? 1.5 : .8;

        // The vault climbs ExitY in three equal tiers: 0 the forecourt, 4 J1 to the belts, 8 J2 and the shelf, 12 the terrace.
        static float Level(int tier) => (float)(R.ExitY * tier / 3);

        enum Probe { Walk, Fork1, Hammers, Fork2Safe, Fork2Risky, Plates, Door, Full }
        enum Policy { Run, Sprint, Jumper }
        enum Step { Pass, Towers, BulkheadWait, HammerWait, Pad, Flight, DrawerWait, Belts, PressWait, PlateWait, Plates, DoorWait, Hurdle }

        // A point of a bot's way (section frame) and what it does there; a Pass is reached within Reach metres flat.
        sealed class Node
        {
            public readonly Step Kind;
            public readonly Vector3 At;
            public readonly int Device;
            public readonly float Reach;

            public Node(Step kind, Vector3 at, int device = 0, float reach = 1.5f)
            {
                Kind = kind;
                At = at;
                Device = device;
                Reach = reach;
            }
        }

        // One stay in one beat (beats.csv), or a crossing's sum per beat (In the first entry, Out the last exit).
        sealed class BeatVisit
        {
            public string Beat;
            public float In, Out = float.NaN, Waits, Lost;
            public int Downs, Falls, Hits;
        }

        // A bot: who it is (kept across runs) and the crossing it is on (Place makes a fresh one for every run).
        sealed class Bot
        {
            public int Index, Team;
            public Policy Policy;
            public PieceKind Piece = PieceKind.Pawn;
            public GameObject Body;
            public ICharacterDriver Driver;
            public Rigidbody Hips;
            public Collider[] Parts = new Collider[0];

            // the way
            public readonly List<Node> Route = new List<Node>();
            public int Next;
            public bool Risky1, Risky2, Going;
            public float Lane, LaneSwitchedAt = -99f;
            public int Half = 1, Belt = -1, Row;
            public int TowerNode;
            public bool InAir, Thrown, OnShelf, DropPending;
            public Vector3 TakeoffAt;
            public float TakeoffEdge, TakeoffTime = -99f, SettledOn = -99f, RowSwitchedAt = -99f, ThrownAt = -99f;
            // when its hips last stood over a plate's deck (PlatesTerms), and when a drawer last knocked it down while it has
            // not been on its feet since (DrawerCarry; -99 none)
            public float OnDeckAt = -99f, DrawerDownAt = -99f;
            public int TakeoffFrom, TakeoffTo;
            // this frame
            public bool Feet, Waiting;
            public float Ground = float.NaN;
            public string WaitKind;
            // the crossing
            public bool Running, Done;
            public float Started, FinishedAt, LastHit = -99f, LastJump = -99f, CheckTime, PressSince = -1f;
            public Vector3 CheckAt;
            public int Downs, Staggers, Falls, Respawns, Drops, Stuck, Flung, Pinned, GaveUpCount, Launches, ShelfLandings, AbyssFalls,
                       HammerDouble, PressPinches, DoorPinches, Recovered;
            public float HammerWait0, HammerWait1, PressWait, BeltTime, DrawerWait, PlateWait, DoorWait, BulkheadWait, TowerWait, Alone,
                         MaxSpeed, MaxRise, Lost;
            public float AtJ1 = -1f, LeftWalk = -1f, AtJ2 = -1f;
            public bool DownByHammer0, DownByHammer1;
            public int HammerPassOf = -1, HammerPassHits;
            public readonly Dictionary<string, int> DownsBy = new Dictionary<string, int>();
            // every knockdown: its Kind ("hurdle 1", "drawer 2", ...) and Time.time (HurdleChains, the drawers' line)
            public readonly List<KeyValuePair<string, float>> DownLog = new List<KeyValuePair<string, float>>();
            // HurdleChains (R725 review): when it first stood again after its last knockdown, and the seconds it had stood before
            // each hurdle knockdown that repeats the hurdle of the one before
            public float UpAt = -99f;
            public readonly List<float> RepeatFeet = new List<float>();
            public readonly Dictionary<string, int> FallsAt = new Dictionary<string, int>(), DownsAt = new Dictionary<string, int>();
            public readonly HashSet<string> GaveUp = new HashSet<string>();
            public readonly List<BeatVisit> Visits = new List<BeatVisit>();
            public BeatVisit Visit;
            // Where a fall counts: the stay in which the hips first dropped DropBelow under their height when the bot last stood
            // (DropVisit: over the pit, beside the edge it went off), else the stay it last stood in (FootVisit). Neither the box
            // the hips are falling through when they reach the kill volume (off the last plates into the terrace's, say) nor
            // the beat the bot last stood in (stepping off J2 into a lifted row's gap, it never stands on a plate) is it.
            public BeatVisit FootVisit, DropVisit;
            public float FootY = float.NaN;
            // falls (OpenFalls: the falls since the bot last made its ground good again, all recovered together)
            public bool FallPending, RespawnQueued, FallOpen;
            public int FallRun, OpenFalls;
            public float RespawnAt, FellAt, SettledAt = -99f, FallProgress, ProgressZ = float.MinValue;
            public BeatVisit FallVisit;
            public CourseCheckpoint Highest;
            public int HighestNumber;
            // watches
            public bool InFlung, SolidPinned, PressPinched, DoorPinched;
            public float FlungAt, InsideSince = -1f, DieInsideSince = -1f, DieSolidSince = -1f, DiscInsideSince = -1f, CarryInsideSince = -1f;
            // the door
            public float DoorArrived = -1f;
            public double DoorArrivedClock;
        }

        // A finished run's crossing, for the report.
        sealed class Crossing
        {
            public int Run, Bot;
            public Policy Policy;
            public PieceKind Piece;
            public string Branch1, Branch2, Lane;
            public bool Finished, BothHammers;
            public float Seconds, Fork1, Fork2;
            public int Downs, Falls, Drops, Launches, ShelfLandings, AbyssFalls, HammerDouble, Stuck, Flung, Pinned, PressPinches, DoorPinches,
                       Unrecovered;
            public float HammerWait0, HammerWait1, PressWait, BeltTime, DrawerWait, PlateWait, DoorWait, BulkheadWait;
            public Dictionary<string, int> DownsBy;
            public List<KeyValuePair<string, float>> DownLog;
            public List<float> RepeatFeet;
            public Dictionary<string, BeatVisit> Beats;
        }

        // A hit waiting 1.5 s for how far back it pushed the bot (hits.csv's back_m).
        sealed class PendingHit
        {
            public Bot Bot;
            public float At, Z, MinZ;
            public string Row;
            public bool HammerDown;
        }

        // A bot through the door: which opening (DoorPeriod since clock 0), when it came to the wait line and when it passed.
        sealed class DoorPass
        {
            public int Run;
            public long Opening;
            public double ArrivedClock;
            public float Arrived, Passed;
        }

        static readonly List<Bot> bots = new List<Bot>();
        static readonly List<Crossing> crossings = new List<Crossing>();
        static readonly List<string> errors = new List<string>();
        static readonly List<string> anomalies = new List<string>(), stalls = new List<string>();
        static readonly StringBuilder crossingRows = new StringBuilder(), beatRows = new StringBuilder(), doorRows = new StringBuilder(),
                                      hitRows = new StringBuilder(), traceRows = new StringBuilder();
        static readonly Queue<Action> pictures = new Queue<Action>();
        static readonly HashSet<string> pictured = new HashSet<string>();
        static readonly List<PendingHit> pendingHits = new List<PendingHit>();
        static readonly List<DoorPass> doorPasses = new List<DoorPass>();
        static readonly List<KeyValuePair<PieceKind, float>> hammerBacks = new List<KeyValuePair<PieceKind, float>>();
        static readonly List<float> leaders = new List<float>();
        static readonly List<LaunchPad> pads = new List<LaunchPad>();
        static readonly Dictionary<int, CourseCheckpoint> checkpoints = new Dictionary<int, CourseCheckpoint>();
        static readonly RaycastHit[] under = new RaycastHit[16];
        static readonly Collider[] overlaps = new Collider[32];
        static PawnRushSection section;
        static Transform sectionRoot;
        static Camera captureCamera;
        static Collider dieBox, doorDisc;
        static Probe probe = Probe.Full;
        static bool mix;
        static int phase, run, runs, seed, seedCp;
        static float limit, finishZ, finishY, runBegan, startAt, lastTick, nextTrace;
        static double beganAt, playRequestedAt, editorIdleAt, runClock;
        static bool stopping;
        // What drawers' knockdowns did to the bodies they carried (CarryWatch, over the whole test): reported (the watches
        // judge those bodies too).
        static float carryAcross, carryUp, carryInside;
        static string carryInsideOf;

        static string ProbeName => probe.ToString().ToLowerInvariant();

        static RoyalVaultBotTest()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Royal Vault Bots (Play)", false, 208)]
        public static void RunMenu() => RunPlayBatch();

        public static void RunPlayBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, Report),
                "Royal Vault bot test (R725): twelve ragdoll pawns cross the Royal Vault (Late_3-3_RoyalVault) in Play Mode - blind runners, " +
                "sprinters and careful jumpers on every branch of both forks, their routes and timing worked out from RoyalVaultRules.\n" +
                "Bot proxies: not people, not online, not verification.\n");
            File.WriteAllText(Path.Combine(Folder, CrossingsFile), CrossingsHeader + "\n");
            File.WriteAllText(Path.Combine(Folder, BeatsFile), BeatsHeader + "\n");
            File.WriteAllText(Path.Combine(Folder, DoorFile), DoorHeader + "\n");
            File.WriteAllText(Path.Combine(Folder, HitsFile), HitsHeader + "\n");
            File.WriteAllText(Path.Combine(Folder, TraceFile), TraceHeader + "\n");
            // The vault's numbers are code defaults the prefab does not serialize, and Unity bakes a prefab into its imported
            // artifact without importing it again when only code changes (the carriage yard's lesson, R661): imported again here.
            if (File.Exists(SectionPath)) AssetDatabase.ImportAsset(SectionPath, ImportAssetOptions.ForceUpdate);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 0);
            playRequestedAt = editorIdleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginPlayWhenIdle;
            EditorApplication.update += BeginPlayWhenIdle;
        }

        static void BeginPlayWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - playRequestedAt > 120f)
            {
                EditorApplication.update -= BeginPlayWhenIdle;
                SessionState.SetBool(ActiveKey, false);
                Log("FAIL: Editor startup did not settle before Play Mode");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { editorIdleAt = now; return; }
            if (now - editorIdleAt < 2f) return;
            EditorApplication.update -= BeginPlayWhenIdle;
            Log("Beginning Play Mode after Editor startup settled");
            EditorApplication.isPlaying = true;
        }

        static int Arg(string name, int fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int value) ? value : fallback;
        }

        static string ArgText(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("-") ? args[at + 1] : fallback;
        }

        static bool NoCapture => Array.IndexOf(Environment.GetCommandLineArgs(), "-vaultBotNoCapture") >= 0;

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                phase = 0;
                run = 0;
                bots.Clear(); crossings.Clear(); errors.Clear(); anomalies.Clear(); stalls.Clear(); pictures.Clear(); pictured.Clear();
                pendingHits.Clear(); doorPasses.Clear(); hammerBacks.Clear(); leaders.Clear(); pads.Clear(); checkpoints.Clear();
                crossingRows.Clear(); beatRows.Clear(); doorRows.Clear(); hitRows.Clear(); traceRows.Clear();
                section = null;
                sectionRoot = null;
                dieBox = doorDisc = null;
                nextTrace = lastTick = 0f;
                stopping = false;
                carryAcross = carryUp = carryInside = 0f;
                carryInsideOf = null;
                beganAt = EditorApplication.timeSinceStartup;
                Application.logMessageReceived += OnLog;
                YardKit.HitGiven += OnHit;
                KillVolume.Entered += OnFall;
                Checkpoint.Reached += OnCheckpoint;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            // The render pipeline's editor package about its own shader on the first capture in batch mode (R516): not the game's.
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                errors.Add(condition + "\n" + stack);
        }

        // ------------------------------------------------------------------ the run

        static void Tick()
        {
            if (!EditorApplication.isPlaying || stopping) return;
            // R516: the Editor's Error Pause stops Play on a render pipeline message that is not the game's.
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (EditorApplication.timeSinceStartup - beganAt > RuntimeLimit)
                    throw new InvalidOperationException("the bot test exceeded " + RuntimeLimit + " seconds");
                if (pictures.Count > 0) { pictures.Dequeue()(); return; }
                float now = Time.time, dt = Mathf.Max(0f, now - lastTick);
                lastTick = now;
                switch (phase)
                {
                    case 0:
                        Build();
                        phase = 1;
                        return;
                    case 1:
                        if (now < startAt) return;
                        StartRun(now);
                        phase = 2;
                        return;
                    case 2:
                        Respawns(now);
                        Drive(now);
                        Watch(now, dt);
                        FlushHits(now, false);
                        if (bots.All(b => b.Done) || now - runBegan > limit)
                        {
                            EndRun(now);
                            run++;
                            if (run >= runs) { phase = 3; return; }
                            Place();
                            phase = 1;
                        }
                        return;
                    case 3:
                        Finish();
                        return;
                }
            }
            catch (Exception e)
            {
                Log("FAIL: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
                StopPlay(1);
            }
        }

        static void Build()
        {
            ParseArgs();
            Log("Probe " + ProbeName + ": " + runs + " run(s), seed " + seed + ", pieces " + (mix ? "mix" : "pawn") + ", " + S(limit, "0") + " s a run");
            // 1. The ragdoll lab's physics rate, before any body exists (PhysicsProfile: 120 Hz, 24 solver iterations).
            new GameObject("Physics profile (bot test)").AddComponent<PhysicsProfile>();
            // 2. The Pawn Rush movement every pawn of a Pawn Rush scene has, and the piece skills: PawnRushCourseSkills hands
            // them out and attaches itself only in a course scene with a playtest spawner, so it is added here, by name (this
            // editor assembly does not reference RagdollLab).
            CourseSkills.PawnRush = true;
            var skills = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ChessFight.RagdollLab.PawnRushCourseSkills")).FirstOrDefault(t => t != null);
            if (skills == null) throw new InvalidOperationException("PawnRushCourseSkills not found: the bots would not move as Pawn Rush pawns");
            new GameObject("Pawn Rush skills (bot test)").AddComponent(skills);
            // 3. Sky, sun and the capture camera.
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            DynamicGI.UpdateEnvironment();
            var sun = new GameObject("Bot test sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.color = Color.white; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.sun = sun;
            captureCamera = new GameObject("Bot test camera").AddComponent<Camera>();
            captureCamera.clearFlags = CameraClearFlags.Skybox;
            captureCamera.nearClipPlane = .1f; captureCamera.farClipPlane = 500f;
            captureCamera.gameObject.AddComponent<AudioListener>();
            // 4. What the bodies run with.
            Log("Physics: Time.fixedDeltaTime " + S(Time.fixedDeltaTime, "0.#####") + " s (" + Mathf.RoundToInt(1f / Time.fixedDeltaTime) + " Hz), "
                + "Physics.defaultSolverIterations " + Physics.defaultSolverIterations);
            Log("Pawn Rush movement and skills: " + skills.Name + " added (every pawn gets PawnRushPhysicsParams on its first frame)");
            // 5. The vault alone at the origin, configured as the round configures slot 5 before anything in it wakes.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SectionPath);
            if (prefab == null) throw new InvalidOperationException("no section prefab at " + SectionPath);
            var holder = new GameObject("Royal Vault (bot test)");
            holder.SetActive(false);
            var go = Object.Instantiate(prefab, holder.transform);
            go.name = prefab.name;
            section = go.GetComponent<PawnRushSection>();
            if (section == null) throw new InvalidOperationException(prefab.name + " has no PawnRushSection at its root");
            sectionRoot = section.transform;
            section.Configure(Slot, seed, TeamSize, ObstacleClock.Now);
            holder.SetActive(true);
            FindParts();
            // 6. Every bot threshold must be one its rule can meet, or the bots would wait for ever.
            AssertReachable();

            var pawn = AssetDatabase.LoadAssetAtPath<GameObject>(PawnPath);
            if (pawn == null) throw new InvalidOperationException("no pawn prefab at " + PawnPath);
            for (int i = 0; i < BotCount; i++)
            {
                var body = Object.Instantiate(pawn, World(new Vector3(0f, 0f, -12f)), sectionRoot.rotation);
                body.name = "Bot " + (i + 1);
                var bot = new Bot
                {
                    Index = i, Team = i % 2 == 0 ? Teams.White : Teams.Black, Body = body,
                    Driver = body.GetComponentInChildren<ICharacterDriver>(),
                    Policy = i < 4 ? Policy.Run : i < 8 ? Policy.Sprint : Policy.Jumper,
                    Piece = mix ? MixPiece(i) : PieceKind.Pawn,
                };
                if (bot.Driver == null) throw new InvalidOperationException("the pawn prefab has no ICharacterDriver");
                if (bot.Driver is ITeamAssignable member) member.AssignTeam(bot.Team);
                bot.Hips = bot.Driver.FollowTarget != null ? bot.Driver.FollowTarget.GetComponent<Rigidbody>() : null;
                bot.Parts = body.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
                bots.Add(bot);
            }
            Place();
        }

        static void ParseArgs()
        {
            runs = Mathf.Clamp(Arg("-vaultBotRuns", 4), 1, 10);
            seed = Arg("-vaultBotSeed", 7);
            string beat = ArgText("-vaultBotBeat", "full").ToLowerInvariant();
            bool known = false;
            foreach (Probe p in Enum.GetValues(typeof(Probe)))
                if (p.ToString().ToLowerInvariant() == beat) { probe = p; known = true; }
            if (!known) throw new ArgumentException("-vaultBotBeat " + beat + " is none of walk|fork1|hammers|fork2safe|fork2risky|plates|door|full");
            string pieces = ArgText("-vaultBotPieces", "pawn").ToLowerInvariant();
            if (pieces != "pawn" && pieces != "mix") throw new ArgumentException("-vaultBotPieces " + pieces + " is neither pawn nor mix");
            mix = pieces == "mix";
            // Where each probe's bots start (their highest checkpoint) and where they are through: on the floor past that line.
            switch (probe)
            {
                case Probe.Fork1: seedCp = 0; finishZ = (float)R.J1Z0 + 1f; finishY = Level(1); break;
                case Probe.Hammers: seedCp = 2; finishZ = (float)R.WalkZ1 + 1f; finishY = Level(1); break;
                case Probe.Fork2Safe:
                case Probe.Fork2Risky: seedCp = 3; finishZ = (float)R.J2Z0 + 2f; finishY = Level(2); break;
                case Probe.Plates: seedCp = 4; finishZ = (float)R.PlatesZ1 + 1f; finishY = Level(2); break;
                case Probe.Door: seedCp = 5; finishZ = (float)R.ExitZ - .5f; finishY = Level(3); break;
                default: seedCp = 0; finishZ = (float)R.ExitZ - .5f; finishY = Level(3); break;
            }
            finishY -= .3f;
            limit = probe == Probe.Full || probe == Probe.Walk ? FullLimit : BeatLimit;
        }

        // The mix: one queen per policy, rooks among the runners and sprinters, a knight jumper, a bishop runner.
        static PieceKind MixPiece(int index)
        {
            switch (index)
            {
                case 2: case 6: case 10: return PieceKind.Queen;
                case 3: case 7: return PieceKind.Rook;
                case 11: return PieceKind.Knight;
                case 1: return PieceKind.Bishop;
                default: return PieceKind.Pawn;
            }
        }

        static void FindParts()
        {
            var content = sectionRoot.GetComponentsInChildren<Collider>(true);
            if (content.Length == 0) throw new InvalidOperationException("the vault built nothing (RoyalVaultSection did not build its Content)");
            foreach (var pad in sectionRoot.GetComponentsInChildren<LaunchPad>(true))
            {
                pads.Add(pad);
                pad.Thrown += OnThrown;
            }
            foreach (var cp in sectionRoot.GetComponentsInChildren<CourseCheckpoint>(true)) checkpoints[cp.Number] = cp;
            dieBox = Named(content, "RV press die box");
            doorDisc = Named(content, "RV door disc body");
            var movers = sectionRoot.GetComponentsInChildren<Obstacle>(true);
            Log("Section " + section.Id + " (" + section.DisplayName + ") at the origin as slot " + (Slot + 1) + ": " + content.Length + " colliders, "
                + movers.Length + " moving parts (" + string.Join(", ", movers.GroupBy(o => o.GetType().Name).Select(g => g.Count() + " " + g.Key)) + "), "
                + pads.Count + " launch pad(s), " + sectionRoot.GetComponentsInChildren<KillVolume>(true).Length + " kill volume(s), checkpoints "
                + string.Join(" ", checkpoints.Keys.OrderBy(k => k).Select(k => "CP" + k)) + ", " + section.Beats.Length + " beats; press die box "
                + (dieBox != null ? "found" : "MISSING (no press pinch watch)") + ", door disc " + (doorDisc != null ? "found" : "MISSING (no door pinch watch)"));
            if (!checkpoints.ContainsKey(seedCp)) throw new InvalidOperationException("the vault has no CP" + seedCp + " for this probe's start and respawns");
        }

        static Collider Named(Collider[] all, string name) =>
            all.FirstOrDefault(c => c.name == name && !c.isTrigger) ?? all.FirstOrDefault(c => c.name.StartsWith(name) && !c.isTrigger);

        // Every bot to its start with a fresh crossing: branches, lane, highest checkpoint and way.
        static void Place()
        {
            var rng = new System.Random(seed * 7919 + run * 131 + (int)probe * 17);
            for (int i = 0; i < bots.Count; i++)
            {
                var old = bots[i];
                var bot = bots[i] = new Bot
                {
                    Index = old.Index, Team = old.Team, Policy = old.Policy, Piece = old.Piece, Body = old.Body, Driver = old.Driver,
                    Hips = old.Hips, Parts = old.Parts,
                };
                var spot = SpawnSpot(bot.Index) + new Vector3((float)(rng.NextDouble() - .5) * .3f, 0f, (float)(rng.NextDouble() - .5) * .3f);
                bot.Driver.Teleport(World(spot), sectionRoot.rotation);
                bot.Driver.SetCommand(default);
                // Every combination per policy per run (spec 5.2): the risky-1 bots three east, three west.
                bot.Risky1 = (bot.Index + run) % 2 == 0;
                bot.Risky2 = probe != Probe.Fork2Safe && (probe == Probe.Fork2Risky || (bot.Index / 2 + run) % 2 == 0);
                bot.Lane = ((bot.Index / 2) % 2 == 0 ? 1f : -1f) * (float)R.TowerLaneX;
                bot.HighestNumber = seedCp;
                bot.Highest = checkpoints.TryGetValue(seedCp, out var cp) ? cp : null;
                Resume(bot, seedCp);
            }
            // The seed varies when each run starts, so the bots meet the devices at other phases of the shared clock.
            startAt = Time.time + SettleSeconds + (float)rng.NextDouble() * 3f;
        }

        // Two rows of six (spec 5.2), on the probe's starting floor.
        static Vector3 SpawnSpot(int index)
        {
            int col = index % 6, row = index / 6;
            switch (probe)
            {
                case Probe.Hammers: return new Vector3(-8f + 2f * col, Level(1), row == 0 ? 53f : 56f);
                case Probe.Fork2Safe:
                case Probe.Fork2Risky: return new Vector3(-12f + 2f * col, Level(1), row == 0 ? 85f : 86.5f);
                case Probe.Plates: return new Vector3(-5f + 2f * col, Level(2), row == 0 ? 128.5f : 131f);
                case Probe.Door: return new Vector3(-5f + 2f * col, Level(3), row == 0 ? 170f : 172f);
                default:
                {
                    // PawnRushRound.StartSpots: each team three across at 1.5 / 3 / 4.5 m, rows 8 and 4 m in, white to the west.
                    int k = index / 2;
                    float x = 1.5f * (1 + k % 3), z = k < 3 ? 8f : 4f;
                    return new Vector3(index % 2 == 0 ? -x : x, Level(0), z);
                }
            }
        }

        static void StartRun(float now)
        {
            runBegan = now;
            runClock = ObstacleClock.Now;
            if (run == 0 && mix)
            {
                foreach (var bot in bots)
                    if (bot.Piece != PieceKind.Pawn && bot.Driver is IPromotable piece) piece.Promote(bot.Piece);
                Log("Pieces: " + string.Join(", ", bots.Select(b => (b.Index + 1) + " " + b.Policy + " " + b.Piece)));
            }
            foreach (var bot in bots)
            {
                bot.Running = true;
                bot.Started = now;
                bot.CheckTime = now;
                bot.CheckAt = bot.Hips != null ? Local(bot.Hips.position) : Vector3.zero;
            }
            Log("Run " + (run + 1) + " started at " + S(now) + " s (clock " + runClock.ToString("0.00", CultureInfo.InvariantCulture) + ")");
            if (run == 0 && !NoCapture)
                Later(20f, () => Picture("top", new Vector3(-6f, 190f, 98f), new Vector3(-6f, 0f, 98.01f), 60f, 161f));
        }

        // ------------------------------------------------------------------ the ways (section frame)

        // The way on from checkpoint `cp` (the probe's start, or a respawn), every beat after it to the exit.
        static void Resume(Bot bot, int cp)
        {
            bot.Route.Clear();
            bot.Next = 0;
            bot.Going = bot.InAir = bot.Thrown = bot.OnShelf = bot.DropPending = false;
            bot.Belt = -1;
            if (cp <= 1) AddFork1(bot, cp == 1);
            if (cp <= 2) AddHammers(bot);
            if (cp <= 3) AddFork2(bot);
            if (cp <= 4) AddPlates(bot);
            if (cp <= 5) AddDoor(bot, cp == 5);
            AddRoom(bot);
        }

        // Fork 1. Risky: to the lane's take-off opening, then top to top (Towers), onto J1. Safe: into the corridor by its
        // forecourt opening, through both hurdles on the bot's lines (HurdleLine), both bulkheads, up stair 1.
        static void AddFork1(Bot bot, bool fromRest)
        {
            if (bot.Risky1 || fromRest)
            {
                if (!fromRest) bot.Route.Add(new Node(Step.Pass, new Vector3(bot.Lane, Level(0), (float)R.ForecourtZ1 - 1.5f)));
                bot.TowerNode = fromRest ? R.TowerRest : 0;
                bot.Route.Add(new Node(Step.Towers, Vector3.zero));
                bot.Route.Add(new Node(Step.Pass, new Vector3(bot.Lane, Level(1), (float)R.J1Z0 + 1.5f)));
                return;
            }
            float hurdles = HalfX(1);
            bot.Half = 1;
            bot.Route.Add(new Node(Step.Pass, new Vector3(-10f, Level(0), 5f)));
            bot.Route.Add(new Node(Step.Pass, new Vector3(hurdles, Level(0), 5f)));
            bot.Route.Add(new Node(Step.Pass, new Vector3(hurdles, Level(0), 9f)));
            // Along hurdle 1's line out of its reach, straight for hurdle 2's line before its reach, along it out of that
            // (At.x is the blind runner's line; HurdleLine picks each bot's).
            for (int h = 0; h < R.HurdleHubZ.Length; h++)
            {
                if (h > 0) bot.Route.Add(new Node(Step.Hurdle, new Vector3(hurdles, Level(0), (float)(R.HurdleHubZ[h] - HurdleChord) - HurdleRunIn), h));
                bot.Route.Add(new Node(Step.Hurdle, new Vector3(hurdles, Level(0), (float)(R.HurdleHubZ[h] + HurdleChord) + HurdleRunIn), h));
            }
            for (int b = 0; b < R.BulkheadZ.Length; b++) bot.Route.Add(new Node(Step.BulkheadWait, Vector3.zero, b));
            // Every stair is 12 m long: stair 1 rises from z J1Z0 - 12 to J1.
            bot.Route.Add(new Node(Step.Pass, new Vector3((float)R.CorridorX, Level(0), (float)R.J1Z0 - 12f)));
            bot.Route.Add(new Node(Step.Pass, new Vector3((float)R.CorridorX, Level(1), (float)R.J1Z0 + .5f)));
            bot.Route.Add(new Node(Step.Pass, new Vector3(-4f, Level(1), (float)R.CheckpointZ[2])));
        }

        // The walkway: each hammer's wait line, the rest cell between the zones, out onto the approach.
        static void AddHammers(Bot bot)
        {
            float x = Spread(bot, 3, 1.2f), y = Level(1);
            for (int i = 0; i < R.HammerAxleZ.Length; i++)
            {
                bot.Route.Add(new Node(Step.HammerWait, new Vector3(x, y, (float)(R.HammerAxleZ[i] - R.HammerZoneHalf - HammerWaitBack)), i));
                if (i == 0) bot.Route.Add(new Node(Step.Pass, new Vector3(x, y, (float)((R.HammerAxleZ[0] + R.HammerAxleZ[1]) / 2))));
            }
            bot.Route.Add(new Node(Step.Pass, new Vector3(x, y, (float)R.WalkZ1 - .5f)));
            bot.Route.Add(new Node(Step.Pass, new Vector3(x, y, (float)R.WalkZ1 + .8f), 0, .7f));
        }

        static void AddFork2(Bot bot)
        {
            if (!bot.Risky2) { AddSafe(bot); return; }
            // The pad: from its south-west, a body's width off its west face (where the pad catches a bot coming from the
            // walkway); then nothing but the throw until on its feet on the shelf.
            bot.Route.Add(new Node(Step.Pad, new Vector3((float)(R.PadX - R.PadSize / 2 - .2), Level(1), (float)(R.PadZ - R.PadSize / 2 + .2))));
            bot.Route.Add(new Node(Step.Flight, Vector3.zero));
            AddShelf(bot);
        }

        // The shelf: past the landing zone, each drawer's wait line, past the cabinet, onto J2.
        static void AddShelf(Bot bot)
        {
            float x = (float)(R.ShelfX0 + R.ShelfX1) / 2f + Spread(bot, 3, .6f), y = Level(2);
            bot.Route.Add(new Node(Step.Pass, new Vector3(x, y, (float)R.ShelfOpenZ0 - 1.3f)));
            for (int i = 0; i < R.DrawerZ.Length; i++)
                bot.Route.Add(new Node(Step.DrawerWait, new Vector3(x, y, (float)DrawerLine(i)), i));
            bot.Route.Add(new Node(Step.Pass, new Vector3(x, y, (float)R.ShelfOpenZ1 + 1.2f)));
            bot.Route.Add(new Node(Step.Pass, new Vector3(x, y, (float)R.StairTwoZ1 - .5f)));
            bot.Route.Add(new Node(Step.Pass, new Vector3((float)R.ShelfX0, y, (float)R.J2Z0 + 3f)));
        }

        // The belts (one chosen at the mouth), the press's wait line and band, stair 2, J2.
        static void AddSafe(Bot bot)
        {
            bot.Route.Add(new Node(Step.Belts, Vector3.zero));
            bot.Route.Add(new Node(Step.PressWait, new Vector3((float)R.PressX + Spread(bot, 5, 1.2f), Level(1), (float)(R.PressBandZ0 - 2 * R.BodyHalf))));
            bot.Route.Add(new Node(Step.Pass, new Vector3((float)R.PressX, Level(2), (float)R.StairTwoZ1 + .5f)));
            bot.Route.Add(new Node(Step.Pass, new Vector3((float)R.PressX, Level(2), (float)R.J2Z0 + 3f)));
        }

        static void AddPlates(Bot bot)
        {
            bot.Route.Add(new Node(Step.PlateWait, new Vector3(0f, Level(2), (float)R.PlateZ0 - .5f)));
            bot.Route.Add(new Node(Step.Plates, Vector3.zero));
            bot.Route.Add(new Node(Step.Pass, new Vector3(0f, Level(2), (float)R.PlatesZ1 + .5f)));
        }

        // Up stair 3 onto the terrace (not from the terrace's own checkpoint: already there), the door's wait line (three rows
        // of four, west of the middle: the doorway opens from its west side).
        static void AddDoor(Bot bot, bool onTerrace)
        {
            if (!onTerrace) bot.Route.Add(new Node(Step.Pass, new Vector3(0f, Level(3), (float)R.TerraceZ0 + .5f)));
            var spot = new Vector3(-.5f + Spread(bot, 4, 1f), Level(3), DoorLineZ - DoorRowPitch * (bot.Index / 4 % 3));
            bot.Route.Add(new Node(Step.DoorWait, spot));
        }

        // The door's wait line (spec 5.2: 1.5 m before the door wall) and the rows behind it, DoorRowPitch apart. A bot in a row
        // behind the line adds that walk to the door's threshold, so the threshold means the same in every row.
        static float DoorLineZ => (float)(R.DoorZ0 - 1.5);
        const float DoorRowPitch = .9f;
        static double DoorNeedFrom(Policy rule, float waitZ) => DoorNeed(rule) + Math.Max(0f, DoorLineZ - waitZ) / Speed(rule);

        // The room to the exit: the last node, never "reached"; the finish line is the probe's.
        static void AddRoom(Bot bot) => bot.Route.Add(new Node(Step.Pass, new Vector3(0f, Level(3), (float)R.ExitZ)));

        // Off the shelf onto the belts (a drawer's shove, a launch that came down short): on along the safe way, on `belt` (the
        // one it came down on; -1: the one under it, or the choice at the mouth).
        static void FromBelts(Bot bot, int belt = -1)
        {
            bot.Route.Clear();
            bot.Next = 0;
            bot.Going = false;
            bot.Belt = belt;
            AddSafe(bot);
            AddPlates(bot);
            AddDoor(bot, false);
            AddRoom(bot);
        }

        // Bots side by side at a wait line instead of all on one spot (twelve bodies on one spot shove each other in).
        static float Spread(Bot bot, int lanes, float pitch) => ((bot.Index % lanes) - (lanes - 1) * .5f) * pitch;

        // A bulkhead half's middle, across the corridor (-1 the west half, +1 the east).
        static float HalfX(int side) => (float)(R.CorridorX + side * R.BulkheadHalfWidth / 2);

        // Half the chord of a hurdle's reach (HurdleClear) along a half's middle, 2 m off the hub: 3.75.
        static double HurdleChord => Math.Sqrt(HurdleClear * HurdleClear - (HalfX(1) - R.HurdleHubX) * (HalfX(1) - R.HurdleHubX));

        // The half where hurdle h's bar comes head-on at a bot going +z: Unity yaw turns +x toward -z, so a bar turning on
        // positive degrees sweeps -z east of its hub, one turning on negative west of it.
        static int HurdleSide(int h) => R.HurdleDegreesPerSecond[h] > 0 ? 1 : -1;

        static float BeltLaneX(int belt) => (float)(belt == 0 ? (R.BeltWestX0 + R.BeltSplitX) / 2 : (R.BeltSplitX + R.BeltEastX1) / 2);

        // Where a bot that is through stands out of the way of those still coming.
        static Vector3 ParkSpot(Bot bot)
        {
            float s = Spread(bot, 6, 1.2f);
            switch (probe)
            {
                case Probe.Fork1: return new Vector3(-10f + s, Level(1), 58f);
                case Probe.Hammers: return new Vector3(-10f + s * .5f, Level(1), 86f);
                case Probe.Fork2Safe:
                case Probe.Fork2Risky: return new Vector3(-10f + s * .5f, Level(2), 133f);
                case Probe.Plates: return new Vector3(-9f + s, Level(3), 173f);
                default: return new Vector3(bot.Index % 2 == 0 ? -4.5f : 4.5f, Level(3), 186f + .9f * (bot.Index / 2));
            }
        }

        // ------------------------------------------------------------------ driving

        static void Drive(float now)
        {
            bool tracing = run == 0 && now >= nextTrace;
            if (tracing) nextTrace = now + TraceStep;
            double clock = ObstacleClock.Now;
            foreach (var bot in bots)
            {
                if (!bot.Running || bot.Hips == null) continue;
                var p = Local(bot.Hips.position);
                if (bot.Done) { Park(bot, p); continue; }
                if (bot.FallPending) { bot.Driver.SetCommand(default); continue; }
                bot.Feet = OnFeet(bot);
                if (bot.Feet && bot.DownLog.Count > 0 && bot.UpAt < bot.DownLog[bot.DownLog.Count - 1].Value) bot.UpAt = now;   // up again
                bot.Ground = GroundY(bot.Hips.position, out bool deck);
                if (deck && p.y - bot.Ground <= DeckStand) bot.OnDeckAt = now;
                if (bot.Feet && now - bot.DrawerDownAt > .3f) bot.DrawerDownAt = -99f;   // up again after a drawer's knockdown
                bot.Waiting = false;
                bot.WaitKind = null;
                UpdateBeat(bot, p, now);
                if (bot.Feet)
                {
                    bot.FootVisit = bot.Visit;
                    bot.FootY = p.y;
                    bot.DropVisit = null;
                }
                else if (bot.DropVisit == null && p.y < bot.FootY - DropBelow) bot.DropVisit = bot.Visit;   // false while FootY is NaN
                Progress(bot, p, now);
                ShelfDrop(bot, p);
                Steer(bot, p, now, clock, out var dir, out bool jump);
                var step = bot.Next < bot.Route.Count ? bot.Route[bot.Next].Kind : Step.Pass;
                jump |= HurdleJump(bot, p, now, clock);
                // Pressing on without getting anywhere: a jump (not where the jumps are the way's own, or there is no input).
                if (step != Step.Towers && step != Step.Pad && step != Step.Flight) jump |= Unstick(bot, dir, now);
                if (jump) bot.LastJump = now;
                if (tracing) Trace(bot, p, dir, jump, now, step);
                var move = WorldDir(dir);
                move.y = 0f;
                var aim = move.sqrMagnitude > 1e-4f ? move.normalized : sectionRoot.forward;
                bot.Driver.SetCommand(new CharacterCommand { Move = move, Sprint = bot.Policy != Policy.Run, Jump = jump, Aim = aim });
                if (p.z >= finishZ && p.y >= finishY)
                {
                    bot.Done = true;
                    bot.FinishedAt = now;
                    MadeGood(bot, now);
                    CloseVisit(bot, now);
                }
            }
        }

        static void Steer(Bot bot, Vector3 p, float now, double clock, out Vector3 dir, out bool jump)
        {
            dir = Vector3.zero;
            jump = false;
            for (int guard = 0; guard < 12 && bot.Next < bot.Route.Count; guard++)
            {
                var node = bot.Route[bot.Next];
                bool active;
                switch (node.Kind)
                {
                    case Step.Pass:
                        active = bot.Next == bot.Route.Count - 1 || Flat(node.At - p).magnitude >= node.Reach;
                        if (active) dir = Toward(p, Detour(p, node.At));
                        break;
                    case Step.Towers: active = Towers(bot, p, now, out dir, out jump); break;
                    case Step.Hurdle: active = HurdleLine(bot, node, p, out dir); break;
                    case Step.BulkheadWait: active = Bulkhead(bot, node, p, clock, out dir); break;
                    case Step.HammerWait: active = Hammer(bot, node, p, clock, out dir); break;
                    case Step.Pad: active = Pad(bot, node, p, out dir); break;
                    case Step.Flight: active = Flight(bot, p, now, out dir); break;
                    case Step.DrawerWait: active = Drawer(bot, node, p, clock, out dir); break;
                    case Step.Belts: active = Belts(bot, p, clock, out dir); break;
                    case Step.PressWait: active = Press(bot, node, p, clock, out dir); break;
                    case Step.PlateWait: active = PlateEntry(bot, node, p, clock, out dir); break;
                    case Step.Plates: active = Plates(bot, p, now, clock, out dir); break;
                    case Step.DoorWait: active = Door(bot, node, p, now, clock, out dir); break;
                    default: active = false; break;
                }
                if (active) return;
                bot.Next++;
                bot.Going = false;
            }
        }

        // A corridor bot shoved back out through the corridor's opening into the forecourt would make for its next corridor
        // point straight across the corridor's east rail and press into the forecourt's north-west corner: round by the
        // opening instead - down the rail's forecourt side to it, then in. Any other way is left as it is.
        static Vector3 Detour(Vector3 p, Vector3 target)
        {
            float rail = (float)(R.CorridorX + R.BulkheadHalfWidth), open = CorridorOpenZ - .5f, low = Level(1) - 1f;
            if (p.x <= rail || target.x >= rail || p.y > low || target.y > low) return target;
            float z = p.z + (target.z - p.z) * (p.x - rail) / (p.x - target.x);   // where the straight way meets the rail's line
            if (z <= open) return target;
            return new Vector3(p.z > open ? rail + 1f : rail - 1f, Level(0), open - 1.5f);
        }

        static void Wait(Bot bot, string kind)
        {
            bot.Waiting = true;
            bot.WaitKind = kind;
        }

        // Standing at a spot: there, or back onto it.
        static Vector3 Hold(Vector3 p, Vector3 at) => Flat(at - p).magnitude > .3f ? Toward(p, at) : Vector3.zero;

        // ---- V01 towers. Tops: 0 the forecourt, 1..TowerCount the rows (TowerRest is the rest platform), TowerCount + 1 J1.
        // Aimed at the next top's middle; a jump on its feet within TakeoffEdge of the current top's edge toward it, or once a
        // ray 0.25 m ahead of the hips no longer finds the current top. The Jumper waits while another bot stands on the next
        // top's middle, and on the rest takes the other lane if that one is free.
        static bool Towers(Bot bot, Vector3 p, float now, out Vector3 dir, out bool jump)
        {
            dir = Vector3.zero;
            jump = false;
            int j1 = R.TowerCount + 1;
            if (bot.Feet && !float.IsNaN(bot.Ground))
            {
                int top = TopUnder(p, bot.Ground);
                // Down on it, not just over it at the top of the hop (on its feet already counts 0.8 m up); never stuck in the air.
                bool down = p.y - bot.Ground < Touchdown || now - bot.TakeoffTime > 1.2f;
                if (bot.InAir && now - bot.TakeoffTime > .25f && down) JumpLanded(bot, p, top, now);
                // The top it stands on, forward or back (knocked back down a row, it climbs on from there).
                if (!bot.InAir && top >= 0) bot.TowerNode = top;
            }
            if (bot.TowerNode >= j1) return false;
            int k = bot.TowerNode, next = k + 1;
            bool careful = Rule(bot, BeatTowers) == Policy.Jumper;
            var aim = TopAim(bot.Lane, next);
            if ((k == 0 || k == R.TowerRest) && !bot.InAir && bot.Feet)
            {
                if (k == R.TowerRest && careful && !bot.Going && now - bot.LaneSwitchedAt > 2f && Taken(bot, aim) && !Taken(bot, TopAim(-bot.Lane, next)))
                {
                    bot.Lane = -bot.Lane;
                    bot.LaneSwitchedAt = now;
                    aim = TopAim(bot.Lane, next);
                }
                // Lined up with the lane's take-off opening before the flat north edge (the forecourt's, the rest's: rail stubs
                // either side of the opening).
                if (Mathf.Abs(p.x - bot.Lane) > .5f)
                {
                    dir = Toward(p, new Vector3(bot.Lane, p.y, FlatEdgeZ(k) - 1.4f));
                    return true;
                }
            }
            dir = Toward(p, aim);
            if (bot.InAir || !bot.Feet) return true;
            float edge = EdgeAhead(k, p, dir);
            // The Jumper waits for a free top only before its run-up (Going: set off for this jump). Once it has set off it
            // jumps, as a careful player does: stopping on the way, it slid off the edge (walk seed 7: the top it had waited
            // for was taken again by a pawn walking on there while it ran at it, 0.4 m before the edge at 3.4 m/s).
            if (careful && next < j1 && edge < 1.2f && !bot.Going)
            {
                if (Taken(bot, aim))
                {
                    Wait(bot, "tower");
                    dir = Vector3.zero;
                    return true;
                }
                bot.Going = true;
            }
            if (now - bot.SettledOn < .15f || now - bot.LastJump < .6f) return true;
            if (edge <= TakeoffEdge || !TopAhead(p, dir, TopY(k)))
            {
                jump = true;
                bot.InAir = true;
                bot.Going = false;
                bot.TakeoffAt = p;
                bot.TakeoffEdge = edge;
                bot.TakeoffFrom = k;
                bot.TakeoffTo = next;
                bot.TakeoffTime = now;
                JumpRow(bot, "takeoff", p, k, next, edge, float.NaN, now);
            }
            return true;
        }

        // The forecourt is 0 high, row k TowerTop(k) (the rest is row TowerRest, J1 the next step after the last row).
        static float TopY(int k) => k <= 0 ? Level(0) : (float)R.TowerTop(k);

        // The flat take-off edges: the forecourt's north edge (k 0) and the rest platform's.
        static float FlatEdgeZ(int k) => k == 0 ? (float)R.ForecourtZ1 : (float)(R.TowerZ(R.TowerRest) + R.TowerDiameter / 2);

        static Vector3 TopAim(float lane, int next) =>
            new Vector3(lane, TopY(next), next > R.TowerCount ? (float)R.J1Z0 + 1.5f : (float)R.TowerZ(next));

        // Which top the bot stands on (its ground's height and footprint), -1 none of them.
        static int TopUnder(Vector3 p, float ground)
        {
            float r = (float)R.TowerDiameter / 2f, half = (float)R.TowerLaneX + r;
            for (int k = 0; k <= R.TowerCount + 1; k++)
            {
                if (Mathf.Abs(ground - TopY(k)) > .2f) continue;
                if (k == 0) { if (p.z <= R.ForecourtZ1 + .1f) return k; continue; }
                if (k > R.TowerCount) { if (p.z >= R.J1Z0 - .1f) return k; continue; }
                float z = (float)R.TowerZ(k);
                if (k == R.TowerRest) { if (Mathf.Abs(p.x) <= half + .1f && Mathf.Abs(p.z - z) <= r + .1f) return k; continue; }
                for (int lane = -1; lane <= 1; lane += 2)
                    if (Flat(p - new Vector3(lane * (float)R.TowerLaneX, 0f, z)).magnitude <= r + .1f) return k;
            }
            return -1;
        }

        // Metres along `dir` from the hips to the edge of top k (the forecourt's and the rest's flat north edges, a tower's rim).
        static float EdgeAhead(int k, Vector3 p, Vector3 dir)
        {
            float r = (float)R.TowerDiameter / 2f;
            if (k == 0 || k == R.TowerRest)
            {
                float edgeZ = FlatEdgeZ(k);
                return dir.z > .05f ? (edgeZ - p.z) / dir.z : edgeZ - p.z;
            }
            var c = new Vector3(p.x >= 0f ? (float)R.TowerLaneX : -(float)R.TowerLaneX, 0f, (float)R.TowerZ(k));
            var d = Flat(p - c);
            float b = Vector3.Dot(d, dir), disc = b * b - (d.sqrMagnitude - r * r);
            return disc < 0f ? 0f : -b + Mathf.Sqrt(disc);
        }

        // How far inside top k's edge a point is (negative: outside).
        static float InsideTop(int k, Vector3 p)
        {
            float r = (float)R.TowerDiameter / 2f;
            if (k == 0) return (float)R.ForecourtZ1 - p.z;
            if (k > R.TowerCount) return p.z - (float)R.J1Z0;
            float z = (float)R.TowerZ(k);
            if (k == R.TowerRest) return Mathf.Min(r - Mathf.Abs(p.z - z), (float)R.TowerLaneX + r - Mathf.Abs(p.x));
            return r - Flat(p - new Vector3(p.x >= 0f ? (float)R.TowerLaneX : -(float)R.TowerLaneX, 0f, z)).magnitude;
        }

        // A ray down 0.25 m ahead of the hips still finds the current top (or something as high).
        static bool TopAhead(Vector3 p, Vector3 dir, float topY)
        {
            var from = World(p + dir * .25f + Vector3.up * .1f);
            float length = Mathf.Max(.3f, p.y - topY + .5f);
            int n = Physics.RaycastNonAlloc(from, -sectionRoot.up, under, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (under[i].collider.GetComponentInParent<ICharacterDriver>() != null) continue;
                if (Local(under[i].point).y >= topY - .15f) return true;
            }
            return false;
        }

        // Another bot standing on that top's middle.
        static bool Taken(Bot self, Vector3 top)
        {
            foreach (var other in bots)
            {
                if (other == self || !other.Running || other.Done || other.FallPending || other.Hips == null) continue;
                var q = Local(other.Hips.position);
                if (q.y < top.y - .3f || q.y > top.y + 1.6f) continue;
                if (Flat(q - top).magnitude < TopTaken) return true;
            }
            return false;
        }

        static void JumpLanded(Bot bot, Vector3 p, int top, float now)
        {
            bot.InAir = false;
            bot.Going = false;
            bot.SettledOn = now;
            JumpRow(bot, "land", p, bot.TakeoffFrom, top, top >= 0 ? InsideTop(top, p) : float.NaN, Flat(p - bot.TakeoffAt).magnitude, now);
        }

        // ---- V02 hurdles (Sprint and Jumper alike, and a runner that gave up being blind there), as a player who watches the
        // bar: a jump pressed now is followed through its flight with the hips going on at the velocity they have (a jump is
        // now: no speed to assume) and both bars turning (HurdleFlight), and pressed once it is good and the middle of its
        // window has come. Spec 5.2's rule (HurdleArrivesIn at the hips' current point in HurdleJumpWindow) left out the bot's
        // own run through the 0.69 s flight, so the bars met clean jumps in mid-air: hurdle 1's head-on early, hurdle 2's from
        // behind late.
        static bool HurdleJump(Bot bot, Vector3 p, float now, double clock)
        {
            if (Rule(bot, BeatCorridor) == Policy.Run || !bot.Feet || now - bot.LastJump < .6f || bot.Hips == null) return false;
            if (p.x > R.CorridorX + R.BulkheadHalfWidth + .5 || p.z > R.BulkheadZ[0] - 1) return false;
            var v = Flat(LocalDir(bot.Hips.linearVelocity));
            return HurdleFlight(p.x, p.z, v.x, v.z, clock, out double early, out double late) && early <= late + HurdleCentre;
        }

        // A jump pressed at `clock` from (x, z) on the corridor floor, the hips going on at (vx, vz) through the flight: true
        // when a bar comes at the body while it is in the air and at every step it does the feet are high enough - over
        // HurdleLeadFeet while the bar is within HurdleLead ahead of the hips, over the bar's top while its log is within
        // HurdleTrail behind them (a bar still at the body after the landing fails it). early: the seconds from the feet
        // clearing HurdleLeadFeet to the bar coming within HurdleLead; late: from the bar passing under the hips, or its log
        // clearing the feet, to the feet coming down past the same heights; both the least over the two bars.
        static bool HurdleFlight(double x, double z, double vx, double vz, double clock, out double early, out double late)
        {
            early = late = double.MaxValue;
            bool met = false;
            double span = R.HurdleReach + HurdleTip, high = HurdleLeadFeet;
            int steps = (int)Math.Ceiling((R.JumpAirSeconds(0) + HurdleAfter) / HurdleStep);
            double travel = Math.Sqrt(vx * vx + vz * vz) * steps * HurdleStep;
            for (int h = 0; h < R.HurdleHubZ.Length; h++)
            {
                double hx = x - R.HurdleHubX, hz = z - R.HurdleHubZ[h];
                if (Math.Sqrt(hx * hx + hz * hz) > HurdleClear + travel) continue;
                // The bar's angle, turned on by one step's w each step.
                double w = R.HurdleDegreesPerSecond[h] * Math.PI / 180, a = R.HurdleAngle(h, clock) * Math.PI / 180;
                double ca = Math.Cos(a), sa = Math.Sin(a), cw = Math.Cos(w * HurdleStep), sw = Math.Sin(w * HurdleStep);
                double first = -1, last = -1, under = -1, clear = -1;
                for (int k = 0; k <= steps; k++)
                {
                    double t = k * HurdleStep;
                    HurdleFrame(hx + vx * t, hz + vz * t, ca, sa, w, out double along, out double ahead);
                    double turned = ca * cw - sa * sw;
                    sa = sa * cw + ca * sw;
                    ca = turned;
                    if (Math.Abs(along) > span) continue;
                    double feet = JumpFeet(t);
                    if (ahead > 0)
                    {
                        if (ahead > HurdleLead) continue;
                        if (feet <= high) return false;
                        if (first < 0) first = t;
                        last = t;
                        continue;
                    }
                    if (first < 0) continue;   // gone by before it came near: not this jump's bar
                    if (under < 0 && -ahead < HurdleUnder) under = t;
                    if (under < 0) continue;
                    if (-ahead <= HurdleTrail)
                    {
                        if (feet <= R.HurdleBarHigh) return false;
                    }
                    else if (clear < 0) clear = t;
                }
                if (first < 0) continue;
                met = true;
                early = Math.Min(early, first - JumpFeetUp(high));
                late = Math.Min(late, R.JumpAirSeconds(high) - (under >= 0 ? under : last));
                if (clear >= 0) late = Math.Min(late, R.JumpAirSeconds(R.HurdleBarHigh) - clear);
            }
            return met;
        }

        // Hips dx, dz from a hurdle's hub against its bar at angle a (cos, sin), turning at w (any unit a second): how far
        // along the bar from the hub (Unity yaw: its way is (cos a, -sin a)), and how far off its line along the normal
        // (sin a, cos a), counted positive on the side the bar comes at (it moves along that normal at -w * along there).
        static void HurdleFrame(double dx, double dz, double ca, double sa, double w, out double along, out double ahead)
        {
            along = dx * ca - dz * sa;
            double off = dx * sa + dz * ca;
            ahead = -w * along * off > 0 ? Math.Abs(off) : -Math.Abs(off);
        }

        // Hurdle h's bar within HurdleLead ahead of hips at (x, z) with the feet not over HurdleLeadFeet: met (a hit).
        static bool HurdleMeets(int h, double x, double z, double feet, double clock)
        {
            double a = R.HurdleAngle(h, clock) * Math.PI / 180;
            HurdleFrame(x - R.HurdleHubX, z - R.HurdleHubZ[h], Math.Cos(a), Math.Sin(a), R.HurdleDegreesPerSecond[h], out double along, out double ahead);
            return Math.Abs(along) <= R.HurdleReach + HurdleTip && ahead > 0 && ahead <= HurdleLead && feet <= HurdleLeadFeet;
        }

        // The Pawn Rush jump off the floor, read off RoyalVaultRules.JumpAirSeconds (the seconds in the air landing on a top dh
        // higher) instead of retyped: the apex's time (any dh over the apex), the fall's gravity and the apex from two landings,
        // and the rise as the same apex at the same time (v = 2 apex / T under g = v / T).
        static readonly double JumpApexTime = R.JumpAirSeconds(99), JumpFallFloor = R.JumpAirSeconds(0) - JumpApexTime,
                               JumpFallHalf = R.JumpAirSeconds(.5) - JumpApexTime,
                               JumpFallGravity = 2 * .5 / (JumpFallFloor * JumpFallFloor - JumpFallHalf * JumpFallHalf),
                               JumpApex = JumpFallGravity * JumpFallFloor * JumpFallFloor / 2;

        // The feet's height over the floor t s after the take-off (0 before it and from the landing on).
        static double JumpFeet(double t)
        {
            if (t <= 0) return 0;
            if (t <= JumpApexTime)
            {
                double u = 1 - t / JumpApexTime;
                return JumpApex * (1 - u * u);
            }
            double fall = t - JumpApexTime;
            return Math.Max(0, JumpApex - JumpFallGravity * fall * fall / 2);
        }

        // When the feet pass y on the way up (on the way down it is R.JumpAirSeconds(y)).
        static double JumpFeetUp(double y) => JumpApexTime * (1 - Math.Sqrt(Math.Max(0, 1 - y / JumpApex)));

        // ---- V02 the line through the hurdles (see HurdleLookAhead): a blind runner's the east half's middle, everyone else's
        // the half where each bar comes at it head-on (and its bulkhead wait in the half it comes out in). Along a hurdle's
        // line the point HurdleLookAhead on; for the next line's start, straight.
        static bool HurdleLine(Bot bot, Node node, Vector3 p, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (p.z >= node.At.z) return false;
            int h = node.Device;
            float x = Rule(bot, BeatCorridor) == Policy.Run ? node.At.x : HalfX(HurdleSide(h));
            if (h == R.HurdleHubZ.Length - 1) bot.Half = x < R.CorridorX ? -1 : 1;
            var target = node.At.z < R.HurdleHubZ[h] ? new Vector3(x, p.y, node.At.z) : new Vector3(x, p.y, Mathf.Min(node.At.z, p.z + HurdleLookAhead));
            dir = Toward(p, Detour(p, target));
            return true;
        }

        // HurdleJump's rule against its own model: of the bar's phases (every 0.02 s of a half turn), the share from which a
        // bot going straight up x at `speed` through hurdle h, pressing as the rule does, is never met (HurdleMeets).
        static double HurdleCleanShare(int h, float x, double speed, double t0)
        {
            double period = 180 / Math.Abs(R.HurdleDegreesPerSecond[h]), from = R.HurdleHubZ[h] - HurdleClear - 1, to = R.HurdleHubZ[h] + HurdleClear + 1;
            double air = R.JumpAirSeconds(0);
            int phases = 0, clean = 0;
            for (double phase = 0; phase < period; phase += .02)
            {
                phases++;
                bool ok = true;
                double takeoff = -99;
                for (double t = 0, z = from; ok && z < to; t += HurdleStep, z += speed * HurdleStep)
                {
                    double clock = t0 + phase + t;
                    if (t - takeoff >= air && HurdleFlight(x, z, 0, speed, clock, out double early, out double late) && early <= late + HurdleCentre)
                        takeoff = t;
                    ok = !HurdleMeets(h, x, z, JumpFeet(t - takeoff), clock);
                }
                if (ok) clean++;
            }
            return (double)clean / phases;
        }

        // ---- V03 bulkheads. Run walks the middle blind. The others wait at the line in their half and go through the half
        // whose BulkheadClearFor covers the way from where they stand to past the panel, the larger one if both do.
        static bool Bulkhead(Bot bot, Node node, Vector3 p, double clock, out Vector3 dir)
        {
            int b = node.Device;
            float past = (float)(R.BulkheadZ[b] + PanelHalf + R.BodyHalf), wait = (float)(R.BulkheadZ[b] - BulkheadWaitBack);
            dir = Vector3.zero;
            if (p.z > past) return false;
            var rule = Rule(bot, BeatCorridor);
            if (rule == Policy.Run)
            {
                dir = Toward(p, new Vector3((float)R.CorridorX, p.y, past + .5f));
                return true;
            }
            if (bot.Going && p.z < wait - .6f && bot.Feet) bot.Going = false;   // shoved back out: decide again
            if (!bot.Going)
            {
                var spot = new Vector3(BulkheadLane(bot, bot.Half), p.y, wait);
                if (p.z < wait - .4f)
                {
                    dir = Toward(p, spot);
                    return true;
                }
                double best = -1;
                int pick = 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    // Judged where the bot will walk through that half (its place in it), from where it stands to past the panel.
                    float lane = BulkheadLane(bot, side);
                    double clear = R.BulkheadClearFor(b, lane - R.CorridorX, clock);
                    double metres = Flat(new Vector3(lane, p.y, past) - p).magnitude;
                    if (clear >= BulkheadNeed(rule, metres) && clear > best) { best = clear; pick = side; }
                }
                if (pick == 0)
                {
                    Wait(bot, "bulkhead");
                    dir = Hold(p, spot);
                    return true;
                }
                bot.Half = pick;
                bot.Going = true;
            }
            dir = Toward(p, new Vector3(BulkheadLane(bot, bot.Half), p.y, past + .5f));
            return true;
        }

        // Where in a bulkhead half a bot waits and walks through: the half's middle, three places BulkheadPitch apart.
        static float BulkheadLane(Bot bot, int side) => HalfX(side) + Spread(bot, 3, BulkheadPitch);

        // ---- V04 dial hammers. Run walks on blind; the others wait at the line before each zone until HammerClearFor covers
        // the zone at their speed and margin, and go once without stopping.
        static bool Hammer(Bot bot, Node node, Vector3 p, double clock, out Vector3 dir)
        {
            int i = node.Device;
            float end = (float)(R.HammerAxleZ[i] + R.HammerZoneHalf);
            dir = Vector3.zero;
            if (p.z > end) return false;
            var rule = Rule(bot, BeatHammers);
            var through = new Vector3(node.At.x, p.y, end + .5f);
            if (rule == Policy.Run)
            {
                dir = Toward(p, through);
                return true;
            }
            if (bot.Going && p.z < node.At.z - .6f && bot.Feet) bot.Going = false;   // knocked back out: decide again
            if (!bot.Going)
            {
                if (p.z < node.At.z - .4f)
                {
                    dir = Toward(p, node.At);
                    return true;
                }
                if (R.HammerClearFor(i, clock) >= HammerNeed(rule)) bot.Going = true;
                else
                {
                    Wait(bot, i == 0 ? "hammer1" : "hammer2");
                    dir = Hold(p, node.At);
                    return true;
                }
            }
            dir = Toward(p, through);
            return true;
        }

        // ---- V07 the pad: at it until it throws (LaunchPad.Thrown), nudged onto it if it has not caught the bot at the point.
        static bool Pad(Bot bot, Node node, Vector3 p, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (bot.Thrown) return false;
            dir = Flat(node.At - p).magnitude > .35f ? Toward(p, node.At) : Toward(p, new Vector3((float)R.PadX, p.y, (float)R.PadZ));
            return true;
        }

        // No input in the air; on its feet above PadLandY - 1 it is on the shelf. Down short of it: on from where it is.
        static bool Flight(Bot bot, Vector3 p, float now, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (!bot.Thrown) return false;
            if (bot.Feet && p.y > R.PadLandY - 1)
            {
                bot.ShelfLandings++;
                bot.OnShelf = true;
                bot.Thrown = false;
                return false;
            }
            if (now - bot.ThrownAt > 2.5f && bot.Feet && p.y < R.PadLandY - 2)
            {
                bot.Thrown = false;
                if (p.z >= R.BeltZ0) FromBelts(bot);
                else Resume(bot, 3);
                return true;
            }
            if (now - bot.ThrownAt > FlightStall && bot.Hips != null)
            {
                string line = "run " + (run + 1) + " bot " + (bot.Index + 1) + " (" + bot.Policy + ", " + bot.Piece + ") at " + V(p) + " in "
                              + (bot.Visit != null ? bot.Visit.Beat : "?") + ", " + S(now - bot.ThrownAt) + " s after the throw: on its feet "
                              + (bot.Feet ? "yes" : "no") + ", down " + (bot.Driver is IMiniGamePawn pawn && pawn.Down ? "yes" : "no")
                              + ", tilt " + S(Vector3.Angle(bot.Hips.transform.up, Vector3.up), "0") + " deg, ground "
                              + (float.IsNaN(bot.Ground) ? "none" : S(p.y - bot.Ground, "0.00") + " m under the hips")
                              + ", speed " + S(bot.Hips.linearVelocity.magnitude) + " m/s, near " + Near(bot.Hips.position);
                stalls.Add(line);
                Log("FLIGHT STALL " + line);
                bot.Thrown = false;
                if (p.y > R.PadLandY - 1.5f)
                {
                    bot.OnShelf = true;   // at the shelf's height: its shelf route from here
                    return false;
                }
                if (p.z >= R.BeltZ0) FromBelts(bot);
                else Resume(bot, 3);
                return true;
            }
            Wait(bot, "flight");
            return true;
        }

        // ---- V08 drawers. Run walks on blind (and is knocked down and swept off the open edge); the others wait at each
        // drawer's line (DrawerLine) until DrawerClearFor at their lane covers the way from it to past the drawer
        // (DrawerCross) at their speed and margin.
        static bool Drawer(Bot bot, Node node, Vector3 p, double clock, out Vector3 dir)
        {
            int i = node.Device;
            float back = (float)(R.DrawerZ[i] + R.DrawerLong / 2 + R.BodyHalf);
            dir = Vector3.zero;
            if (p.z > back) return false;
            var rule = Rule(bot, BeatShelf);
            var through = new Vector3(node.At.x, p.y, back + .5f);
            if (rule == Policy.Run)
            {
                dir = Toward(p, through);
                return true;
            }
            if (!bot.Going)
            {
                if (p.z < node.At.z - .4f)
                {
                    dir = Toward(p, node.At);
                    return true;
                }
                if (R.DrawerClearFor(i, node.At.x, clock) >= DrawerNeed(rule)) bot.Going = true;
                else
                {
                    Wait(bot, "drawer");
                    dir = Hold(p, node.At);
                    return true;
                }
            }
            dir = Toward(p, through);
            return true;
        }

        // ---- V06 belts: the one chosen at the mouth (Run its own side's, the others the shorter BeltRideSeconds when they
        // get there), ridden to its end.
        static bool Belts(Bot bot, Vector3 p, double clock, out Vector3 dir)
        {
            if (bot.Belt < 0) bot.Belt = ChooseBelt(bot, p, clock);
            float x = BeltLaneX(bot.Belt) + Spread(bot, 3, .8f), end = (float)R.BeltZ1 - .5f;
            dir = Vector3.zero;
            if (p.z >= end - 1.5f) return false;
            dir = p.z < R.BeltZ0 ? Toward(p, new Vector3(x, p.y, (float)R.BeltZ0 + .5f)) : Toward(p, new Vector3(x, p.y, end));
            return true;
        }

        static int ChooseBelt(Bot bot, Vector3 p, double clock)
        {
            if (p.z >= R.BeltZ0 - .5 && p.y < Level(1) + 1.5f) return p.x < R.BeltSplitX ? 0 : 1;   // already on one
            if (Rule(bot, BeatSafe) == Policy.Run) return bot.Index % 2;   // its own side: white (even) west
            int pick = 0;
            double best = double.MaxValue;
            for (int belt = 0; belt < 2; belt++)
            {
                double eta = Flat(new Vector3(BeltLaneX(belt), p.y, (float)R.BeltZ0) - p).magnitude / ForkSpeed;
                double ride = R.BeltRideSeconds(belt, clock + eta, ForkSpeed, R.BeltZ1 - R.BeltZ0);
                if (ride < best) { best = ride; pick = belt; }
            }
            return pick;
        }

        // ---- V05 the press. Run walks on blind; the others wait at the line before the band (spread across the neck) until
        // PressSafeToCross for the band at their speed and margin, and decide again if the wave puts them back out.
        static bool Press(Bot bot, Node node, Vector3 p, double clock, out Vector3 dir)
        {
            float z0 = (float)R.PressBandZ0, z1 = (float)R.PressBandZ1;
            dir = Vector3.zero;
            if (p.z > z1 + R.BodyHalf) return false;
            var rule = Rule(bot, BeatSafe);
            var through = new Vector3(node.At.x, p.y, z1 + .8f);
            if (rule == Policy.Run)
            {
                dir = Toward(p, through);
                return true;
            }
            if (bot.Going && p.z < z0 - 2 * R.BodyHalf - .3f && bot.Feet) bot.Going = false;
            if (!bot.Going)
            {
                if (p.z < node.At.z - .4f)
                {
                    dir = Toward(p, node.At);
                    return true;
                }
                if (R.PressSafeToCross(clock, PressCross(rule))) bot.Going = true;
                else
                {
                    Wait(bot, "press");
                    dir = Hold(p, node.At);
                    return true;
                }
            }
            dir = Toward(p, through);
            return true;
        }

        // ---- V09 plates. At the wait line the others take the row with the longer PlateFlatFor once it is long enough; Run
        // the west row whatever it does.
        static bool PlateEntry(Bot bot, Node node, Vector3 p, double clock, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (p.z > R.PlateZ0 + .3f) return false;
            var rule = Rule(bot, BeatPlates);
            if (rule == Policy.Run)
            {
                bot.Row = 0;
                return false;
            }
            var spot = new Vector3(RowX(bot), p.y, node.At.z);
            if (p.z < node.At.z - .4f)
            {
                dir = Toward(p, spot);
                return true;
            }
            double west = R.PlateFlatFor(0, clock), east = R.PlateFlatFor(1, clock);
            int best = east > west ? 1 : 0;
            if (Math.Max(west, east) >= PlateEnter(rule))
            {
                bot.Row = best;
                bot.RowSwitchedAt = Time.time;
                return false;
            }
            Wait(bot, "plate");
            dir = Hold(p, spot);
            return true;
        }

        // On the plates: along the inner side of the row, a node every PlatePitch; the others step across when their row is
        // about to rise and the other will lie flat long enough, Run once its row has started to rise.
        static bool Plates(Bot bot, Vector3 p, float now, double clock, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (p.z >= R.PlatesZ1) return false;
            var rule = Rule(bot, BeatPlates);
            int other = 1 - bot.Row;
            if (rule == Policy.Run)
            {
                if (R.PlateAngle(bot.Row, clock) > 0 && now - bot.RowSwitchedAt > 1f)
                {
                    bot.Row = other;
                    bot.RowSwitchedAt = now;
                }
            }
            else if (R.PlateFlatFor(bot.Row, clock) <= PlateLeave(rule) && R.PlateFlatFor(other, clock) >= PlateTake(rule))
            {
                bot.Row = other;
                bot.RowSwitchedAt = now;
            }
            float z = (float)R.PlatesZ1 + .5f, x = RowX(bot);
            for (int k = 0; k < R.PlateCount; k++)
            {
                float node = (float)(R.PlateZ0 + R.PlatePitch * (k + .5));
                if (node > p.z + 1f) { z = node; break; }
            }
            // Changing rows: across first (spec 5.2: a switch is a 1.6 m step), not on the slant to a node up to 5 m ahead,
            // which took over a second to leave the row it was leaving because it was about to lift (walk seed 7: a sprinter
            // still on the east row when it rose, 1 s after setting off with 0.8 s of it flat).
            if (Mathf.Abs(p.x - x) > .4f) z = Mathf.Min(z, p.z + .6f);
            dir = Toward(p, new Vector3(x, p.y, z));
            return true;
        }

        static float RowX(Bot bot) => (bot.Row == 0 ? -PlateInner : PlateInner) + Spread(bot, 3, .35f);

        // ---- V10 the door. Run walks on blind (pressing on a shut door counts as waiting there); the others wait at the line
        // until DoorPassableFor is long enough, and decide again if the shove puts them back out.
        static bool Door(Bot bot, Node node, Vector3 p, float now, double clock, out Vector3 dir)
        {
            float past = (float)(R.DoorZ1 + R.BodyHalf);
            dir = Vector3.zero;
            if (p.z > past)
            {
                DoorPassed(bot, clock, now);
                return false;
            }
            if (bot.DoorArrived < 0f && p.z >= node.At.z - .6f)
            {
                bot.DoorArrived = now - bot.Started;
                bot.DoorArrivedClock = clock;
            }
            var rule = Rule(bot, BeatDoor);
            var through = new Vector3(node.At.x, p.y, past + 1.2f);
            bool shut = p.z >= R.DoorZ0 - 1 && R.DoorPassableFor(clock) <= 0;
            if (rule == Policy.Run)
            {
                if (shut) Wait(bot, "door");
                dir = Toward(p, through);
                return true;
            }
            if (bot.Going && p.z < node.At.z - 1f && bot.Feet) bot.Going = false;
            if (!bot.Going)
            {
                if (p.z < node.At.z - .4f)
                {
                    dir = Toward(p, node.At);
                    return true;
                }
                if (R.DoorPassableFor(clock) >= DoorNeedFrom(rule, node.At.z)) bot.Going = true;
                else
                {
                    Wait(bot, "door");
                    dir = Hold(p, node.At);
                    return true;
                }
            }
            if (shut) Wait(bot, "door");
            dir = Toward(p, through);
            return true;
        }

        static void DoorPassed(Bot bot, double clock, float now)
        {
            if (bot.DoorArrived < 0f)
            {
                bot.DoorArrived = now - bot.Started;
                bot.DoorArrivedClock = clock;
            }
            doorPasses.Add(new DoorPass
            {
                Run = run, Opening = (long)Math.Floor(clock / R.DoorPeriod), ArrivedClock = bot.DoorArrivedClock,
                Arrived = bot.DoorArrived, Passed = now - bot.Started,
            });
        }

        // The rules a bot uses at a beat: its own, or the Jumper's once it gave up being blind there (2 falls or 3 knockdowns).
        static Policy Rule(Bot bot, string beat) => bot.GaveUp.Contains(beat) ? Policy.Jumper : bot.Policy;

        static void GiveUp(Bot bot, string beat)
        {
            if (beat == null || bot.Policy == Policy.Jumper || bot.GaveUp.Contains(beat)) return;
            bot.FallsAt.TryGetValue(beat, out int falls);
            bot.DownsAt.TryGetValue(beat, out int downs);
            if (falls < 2 && downs < 3) return;
            bot.GaveUp.Add(beat);
            bot.GaveUpCount++;
        }

        // A bot pushing on (not waiting) but not moving along its way for half a second jumps, at most every 1.2 s.
        static bool Unstick(Bot bot, Vector3 dir, float now)
        {
            if (bot.Waiting || dir.sqrMagnitude < .25f || bot.Hips == null || !bot.Feet) { bot.PressSince = -1f; return false; }
            if (Vector3.Dot(Flat(LocalDir(bot.Hips.linearVelocity)), dir) > .8f) { bot.PressSince = -1f; return false; }
            if (bot.PressSince < 0f) bot.PressSince = now;
            if (now - bot.PressSince < .5f || now - bot.LastJump < 1.2f) return false;
            bot.PressSince = -1f;
            return true;
        }

        static void Park(Bot bot, Vector3 p)
        {
            var spot = ParkSpot(bot);
            var move = WorldDir(Flat(spot - p).magnitude > .6f ? Toward(p, spot) : Vector3.zero);
            move.y = 0f;
            bot.Driver.SetCommand(new CharacterCommand { Move = move, Aim = move.sqrMagnitude > 1e-4f ? move.normalized : sectionRoot.forward });
        }

        // Fork times (spec 1.18: fork 1 from the start to J1, fork 2 from the walkway's end to J2) and the time a fall lost:
        // from going in to back on its feet as far on as it was.
        static void Progress(Bot bot, Vector3 p, float now)
        {
            float t = now - bot.Started;
            if (bot.AtJ1 < 0f && p.z >= R.J1Z0 + 1 && p.y >= Level(1) - .3f) bot.AtJ1 = t;
            if (bot.LeftWalk < 0f && p.z >= R.WalkZ1 && p.y >= Level(1) - .3f && p.y < Level(2) - 1f) bot.LeftWalk = t;
            if (bot.AtJ2 < 0f && p.z >= R.J2Z0 + 2.5 && p.y >= Level(2) - .3f) bot.AtJ2 = t;
            if (!bot.Feet || now < bot.SettledAt) return;
            if (bot.FallOpen && p.z >= bot.FallProgress) MadeGood(bot, now);
            if (!bot.FallOpen) bot.ProgressZ = Mathf.Max(bot.ProgressZ, p.z);
        }

        // Back as far on as before the falls (or through the finish): their lost time is closed and they count as recovered.
        static void MadeGood(Bot bot, float now)
        {
            if (!bot.FallOpen) return;
            float lost = now - bot.FellAt;
            bot.Lost += lost;
            bot.Recovered += bot.OpenFalls;
            bot.OpenFalls = 0;
            bot.FallOpen = false;
            if (bot.FallVisit != null) bot.FallVisit.Lost += lost;
        }

        // Off the shelf's open edge onto the belts (drop_to_belt): a bot on the shelf whose hips leave its level downward beside
        // the drawers (the open edge's stretch, ShelfOpenZ0..ShelfOpenZ1, and a body's half either way) and that next stands
        // on the belts' level before J2 - on a belt, on the landing at their end or in the press neck - has dropped, once a
        // crossing, whenever it stands up and wherever on that level. Spec 5.2 counted it only on its feet inside the belts
        // within 2 s of being up on the shelf, but a drawer's knockdown and the 4 m fall keep a body down longer, and the belts
        // carry a body that lies on them (seed 23, run 4: a runner pushed off by drawer 2 at z 102.9 got up in the neck,
        // uncounted). Either way it goes on along the safe way from where it came down.
        static void ShelfDrop(Bot bot, Vector3 p)
        {
            if (bot.DropPending && bot.Feet)
            {
                bot.DropPending = false;
                bool level = !float.IsNaN(bot.Ground) && Mathf.Abs(bot.Ground - Level(1)) <= .6f;
                if (level && p.z >= R.BeltZ0 && p.z < R.J2Z0) bot.Drops = 1;
                // Came down past the belts' sides (never over them low enough to change ways): on from where it stands.
                if (bot.OnShelf && level)
                {
                    bot.OnShelf = false;
                    FromBelts(bot);
                    return;
                }
            }
            if (!bot.OnShelf) return;
            if (p.z > R.StairTwoZ1) { bot.OnShelf = false; return; }
            if (p.y > Level(2) - 1f) return;   // on the shelf's level
            if (p.z >= R.ShelfOpenZ0 - R.BodyHalf && p.z <= R.ShelfOpenZ1 + R.BodyHalf) bot.DropPending = true;
            bool overBelts = p.x >= R.BeltWestX0 - .3 && p.x <= R.BeltEastX1 + .3 && p.z >= R.BeltZ0 && p.z <= R.NeckZ0;
            if (p.y > Level(1) + 2f || !overBelts) return;
            bot.OnShelf = false;
            // Still falling: the belt it is coming down on, not the choice a bot makes at the mouth.
            FromBelts(bot, p.x < R.BeltSplitX ? 0 : 1);
        }

        // ------------------------------------------------------------------ watching

        static void Watch(float now, float dt)
        {
            foreach (var bot in bots)
            {
                if (!bot.Running || bot.Done || bot.Hips == null) continue;
                var world = bot.Hips.position;
                var p = Local(world);
                var v = LocalDir(bot.Hips.linearVelocity);
                if (bot.Waiting) AddWait(bot, dt);
                if (OnBelt(p)) bot.BeltTime += dt;
                if (Alone(bot, p)) bot.Alone += dt;
                if (bot.HammerPassOf >= 0)
                {
                    double axle = R.HammerAxleZ[bot.HammerPassOf];
                    if (p.z < axle - R.HammerZoneHalf || p.z > axle + R.HammerZoneHalf) bot.HammerPassOf = -1;
                }
                // Between a fall and the respawn's settling nothing is judged.
                if (bot.FallPending || now < bot.SettledAt)
                {
                    bot.CheckAt = p;
                    bot.CheckTime = now;
                    bot.InsideSince = bot.DieInsideSince = bot.DieSolidSince = bot.DiscInsideSince = -1f;
                    continue;
                }
                FlungWatch(bot, p, v, now);
                PinWatch(bot, world, now);
                // Down from a drawer's knockdown (DrawerCarry): judged as everywhere (R725 review: a carry exempted from the
                // watches could never fail spec 5.3 for a box through a body, a fling or a pinch at the face), and what the carry
                // did is also reported (CarryWatch).
                if (Carried(bot, now)) CarryWatch(bot, world, v, now);
                else bot.CarryInsideSince = -1f;
                // Stuck: under 0.4 m in 4 s, pressing on (a wait is not stuck).
                if (bot.Waiting)
                {
                    bot.CheckAt = p;
                    bot.CheckTime = now;
                }
                if (now - bot.CheckTime >= 4f)
                {
                    if (Flat(p - bot.CheckAt).magnitude < .4f && now - bot.LastHit > 2.5f)
                    {
                        bot.Stuck++;
                        Anomaly(bot, "stuck 4 s pressing on, near " + Near(world));
                    }
                    bot.CheckAt = p;
                    bot.CheckTime = now;
                }
            }
        }

        static void AddWait(Bot bot, float dt)
        {
            switch (bot.WaitKind)
            {
                case "hammer1": bot.HammerWait0 += dt; break;
                case "hammer2": bot.HammerWait1 += dt; break;
                case "press": bot.PressWait += dt; break;
                case "drawer": bot.DrawerWait += dt; break;
                case "plate": bot.PlateWait += dt; break;
                case "door": bot.DoorWait += dt; break;
                case "bulkhead": bot.BulkheadWait += dt; break;
                case "tower": bot.TowerWait += dt; break;
                default: return;   // the flight is no wait
            }
            if (bot.Visit != null) bot.Visit.Waits += dt;
        }

        static bool OnBelt(Vector3 p) =>
            p.x >= R.BeltWestX0 && p.x <= R.BeltEastX1 && p.z >= R.BeltZ0 && p.z <= R.BeltZ1 && Mathf.Abs(p.y - Level(1)) < 1f;

        // No other bot on the course within 3 m.
        static bool Alone(Bot self, Vector3 p)
        {
            foreach (var other in bots)
            {
                if (other == self || !other.Running || other.Done || other.FallPending || other.Hips == null) continue;
                if (Flat(Local(other.Hips.position) - p).magnitude < 3f) return false;
            }
            return true;
        }

        // Flung (spec 5.2): judged only at or above the beat's lowest floor less 1 m, never in the 2.5 s after the pad throws
        // the bot; over 10 m/s across, over 8 m/s up, or the hips more than 3 m above the beat's highest floor. On the plates
        // the spec excludes rises up to 5 m: there the hips' height alone judges a rise (5 m), not its speed - a lifting
        // plate's throw to 2.5-5 m starts at 8-10 m/s up, so the speed rule flagged every rise the spec allows (walk seed 7).
        // A lift throws whoever has a foot on its deck, wherever the hips are, so the plates' terms (their floors too) hold
        // beside the plate field and just after a deck as well (PlatesTerms); nowhere else.
        static void FlungWatch(Bot bot, Vector3 p, Vector3 v, float now)
        {
            string beat = bot.Visit != null ? bot.Visit.Beat : null;
            bool plates = PlatesTerms(bot, p, now, beat);
            Floors(plates ? BeatPlates : beat, out float lowest, out float highest);
            bool judged = p.y >= lowest - 1f && now - bot.ThrownAt >= 2.5f;
            float across = Flat(v).magnitude, rise = p.y - highest, most = plates ? 5f : 3f;
            bool flung = judged && (across > 10f || (!plates && v.y > 8f) || rise > most);
            if (judged)
            {
                bot.MaxSpeed = Mathf.Max(bot.MaxSpeed, across);
                bot.MaxRise = Mathf.Max(bot.MaxRise, rise);
            }
            if (flung && !bot.InFlung)
            {
                bot.InFlung = true;
                bot.FlungAt = now;
                bot.Flung++;
                Anomaly(bot, "flung: " + S(across) + " m/s across, " + S(v.y) + " m/s up, hips " + S(rise, "0.00") + " m over the "
                             + (plates && beat != BeatPlates ? "plates' floor (on their terms: beside the plate field or just off a deck)"
                                                             : "beat's highest floor"));
            }
            else if (!flung && bot.InFlung && now - bot.FlungAt > 1f) bot.InFlung = false;
        }

        // The plates' terms for FlungWatch: in their beat; with the hips (flat) within PlateReach of the plate field - both rows,
        // PlateWide either side of the middle (the seam between the rows is the builder's few cm), from PlateZ0 to the last
        // plate's far end; or within PlateThrow s of standing on a deck (OnDeckAt).
        static bool PlatesTerms(Bot bot, Vector3 p, float now, string beat)
        {
            if (beat == BeatPlates || now - bot.OnDeckAt <= PlateThrow) return true;
            float z0 = (float)R.PlateZ0, z1 = (float)(R.PlateZ0 + R.PlateCount * R.PlatePitch);
            float dx = Mathf.Max(0f, Mathf.Abs(p.x) - (float)R.PlateWide), dz = Mathf.Max(0f, Mathf.Max(z0 - p.z, p.z - z1));
            return dx * dx + dz * dz <= PlateReach * PlateReach;
        }

        // Knocked down by a drawer and not on its feet since, for at most DrawerCarry s.
        static bool Carried(Bot bot, float now) => bot.DrawerDownAt >= 0f && now - bot.DrawerDownAt < DrawerCarry;

        // Lowest and highest floor of each beat: 0 the forecourt; 4 towers, corridor, J1, hammers, approach; 8 belts and press,
        // shelf, J2, plates; 12 terrace and door (the lowest is where the beat begins).
        static void Floors(string beat, out float lowest, out float highest)
        {
            switch (beat)
            {
                case BeatForecourt: lowest = Level(0); highest = Level(0); return;
                case BeatTowers:
                case BeatCorridor: lowest = Level(0); highest = Level(1); return;
                case BeatJ1:
                case BeatHammers:
                case BeatFork2: lowest = Level(1); highest = Level(1); return;
                case BeatSafe: lowest = Level(1); highest = Level(2); return;
                case BeatShelf:
                case BeatJ2:
                case BeatPlates: lowest = Level(2); highest = Level(2); return;
                case BeatTerrace: lowest = Level(2); highest = Level(3); return;
                case BeatDoor: lowest = Level(3); highest = Level(3); return;
                default: lowest = -1e4f; highest = 1e4f; return;   // outside every beat: only the speeds are judged
            }
        }

        // Pinned: as the yard (inside something solid 1.5 s - not the die or the disc, which have their own watches, so one
        // event counts once); under the press die while it still collides with any part of the body, or 1.3 s under it at all
        // (the crush shell lets a floored pawn through for PressGhost); in the door disc 0.3 s.
        static void PinWatch(Bot bot, Vector3 world, float now)
        {
            var inside = SolidAt(world);
            if (inside == null) { bot.InsideSince = -1f; bot.SolidPinned = false; }
            else if (bot.InsideSince < 0f) bot.InsideSince = now;
            else if (!bot.SolidPinned && now - bot.InsideSince > 1.5f)
            {
                bot.SolidPinned = true;
                bot.Pinned++;
                Anomaly(bot, "pinned: inside " + inside.name + " for 1.5 s");
            }
            if (dieBox != null && Inside(dieBox, world))
            {
                if (bot.DieInsideSince < 0f) bot.DieInsideSince = now;
                bool solid = false;
                if (dieBox.enabled && dieBox.gameObject.activeInHierarchy)
                    foreach (var part in bot.Parts)
                        if (part != null && part.enabled && part.gameObject.activeInHierarchy && !Physics.GetIgnoreCollision(dieBox, part)) { solid = true; break; }
                if (!solid) bot.DieSolidSince = -1f;
                else if (bot.DieSolidSince < 0f) bot.DieSolidSince = now;
                bool caught = bot.DieSolidSince >= 0f && now - bot.DieSolidSince >= .1f;
                if (!bot.PressPinched && (caught || now - bot.DieInsideSince >= PressGhost + .3))
                {
                    bot.PressPinched = true;
                    bot.PressPinches++;
                    bot.Pinned++;
                    Anomaly(bot, caught ? "press pinch: hips inside the die and not let through" : "press pinch: hips inside the die " + S((float)(PressGhost + .3)) + " s");
                }
            }
            else { bot.DieInsideSince = bot.DieSolidSince = -1f; bot.PressPinched = false; }
            if (doorDisc != null && Inside(doorDisc, world))
            {
                if (bot.DiscInsideSince < 0f) bot.DiscInsideSince = now;
                else if (!bot.DoorPinched && now - bot.DiscInsideSince >= .3f)
                {
                    bot.DoorPinched = true;
                    bot.DoorPinches++;
                    bot.Pinned++;
                    Anomaly(bot, "door pinch: hips inside the disc 0.3 s");
                }
            }
            else { bot.DiscInsideSince = -1f; bot.DoorPinched = false; }
        }

        static bool Inside(Collider c, Vector3 p)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) return false;
            if (c is MeshCollider mesh && !mesh.convex) return c.bounds.Contains(p);
            return (c.ClosestPoint(p) - p).sqrMagnitude < 1e-6f;
        }

        // The solid thing the hips are in (within 0.12 m of their middle; not a pawn, the press die or the door disc), or null.
        static Collider SolidAt(Vector3 world)
        {
            int n = Physics.OverlapSphereNonAlloc(world, .12f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (overlaps[i] != dieBox && overlaps[i] != doorDisc && overlaps[i].GetComponentInParent<ICharacterDriver>() == null) return overlaps[i];
            return null;
        }

        // A body a drawer's knockdown carries (Carried), judged by the watches as any other: what the carry did to it is also
        // kept for the report - its fastest speeds, and its longest stay inside something solid (a drawer's box sliding through
        // a body would show here, and as pinned once it lasts 1.5 s).
        static void CarryWatch(Bot bot, Vector3 world, Vector3 v, float now)
        {
            carryAcross = Mathf.Max(carryAcross, Flat(v).magnitude);
            carryUp = Mathf.Max(carryUp, v.y);
            var inside = SolidAt(world);
            if (inside == null) { bot.CarryInsideSince = -1f; return; }
            if (bot.CarryInsideSince < 0f) bot.CarryInsideSince = now;
            if (now - bot.CarryInsideSince > carryInside)
            {
                carryInside = now - bot.CarryInsideSince;
                carryInsideOf = inside.name;
            }
        }

        // The beat the hips are in (section.BeatAt, else the spec's boxes); a change closes one beats.csv visit and opens the next.
        static void UpdateBeat(Bot bot, Vector3 p, float now)
        {
            string beat = BeatOf(p);
            if (bot.Visit != null && bot.Visit.Beat == beat) return;
            float t = now - bot.Started;
            if (bot.Visit != null) bot.Visit.Out = t;
            bot.Visit = new BeatVisit { Beat = beat, In = t };
            bot.Visits.Add(bot.Visit);
            if (run == 0 && !NoCapture && pictured.Add(beat)) SchedulePicture(beat);
        }

        static string BeatOf(Vector3 local)
        {
            string beat = section != null ? section.BeatAt(World(local)) : null;
            if (beat != null) return beat;
            foreach (var box in SpecBeats)
                if (box.Contains(local)) return box.name;
            return "(none)";
        }

        static void CloseVisit(Bot bot, float now)
        {
            if (bot.Visit != null && float.IsNaN(bot.Visit.Out)) bot.Visit.Out = now - bot.Started;
        }

        // ------------------------------------------------------------------ events

        static Bot Find(ICharacterDriver driver) => driver == null ? null : bots.FirstOrDefault(b => b.Driver == driver);

        static void OnHit(ICharacterDriver driver, Component source, Vector3 push, float knockdown)
        {
            var bot = Find(driver);
            if (bot == null || !bot.Running || bot.Done || sectionRoot == null) return;
            float now = Time.time;
            string kind = Kind(source), device = Device(kind);
            var p = bot.Hips != null ? Local(bot.Hips.position) : Vector3.zero;
            string beat = bot.Visit != null ? bot.Visit.Beat : BeatOf(p);
            bot.LastHit = now;
            if (bot.Visit != null) bot.Visit.Hits++;
            int hammer = device == "hammer" ? Nearest(R.HammerAxleZ, ObstacleZ(source)) : -1;
            if (hammer >= 0)
            {
                // A pass: one stay in that hammer's zone; a second hit in it is a double hit.
                if (bot.HammerPassOf != hammer)
                {
                    bot.HammerPassOf = hammer;
                    bot.HammerPassHits = 0;
                }
                if (++bot.HammerPassHits == 2) bot.HammerDouble++;
            }
            if (knockdown > 0f)
            {
                bot.Downs++;
                Count(bot.DownsBy, device);
                // Floored again by the hurdle that floored it last, once up (HurdleChains): how long it had been on its feet.
                var last = bot.DownLog.Count > 0 ? bot.DownLog[bot.DownLog.Count - 1] : default(KeyValuePair<string, float>);
                if (device == "hurdle" && last.Key == kind && bot.UpAt > last.Value) bot.RepeatFeet.Add(now - bot.UpAt);
                bot.DownLog.Add(new KeyValuePair<string, float>(kind, now));
                // A drawer's knockdown carries the body off the shelf (Carried) until it is up again; any other ends that.
                bot.DrawerDownAt = device == "drawer" ? now : -99f;
                if (bot.Visit != null) bot.Visit.Downs++;
                string at = DeviceBeat(device) ?? beat;
                Count(bot.DownsAt, at);
                GiveUp(bot, at);
                if (hammer == 0) bot.DownByHammer0 = true;
                if (hammer == 1) bot.DownByHammer1 = true;
            }
            else bot.Staggers++;
            string row = string.Join(",", ProbeName, run + 1, bot.Index + 1, bot.Policy, F(now - bot.Started), kind, F(knockdown), F(push.magnitude),
                                     F(p.x), F(p.y), F(p.z), bot.Piece, beat);
            pendingHits.Add(new PendingHit { Bot = bot, At = now, Z = p.z, MinZ = p.z, Row = row, HammerDown = hammer >= 0 && knockdown > 0f });
        }

        // Which device a hit came from, by the "RV ..." object it hangs under (a drawer's shell, "RV drawer k (hit)", is the
        // drawer's: its knockdowns count as "drawer" in DownsBy and against the shelf's beat); which of a pair by its mover's z.
        static string Kind(Component source)
        {
            if (source == null) return "?";
            if (source is VaultPressWave) return "press wave";
            for (var t = source.transform; t != null && t != sectionRoot; t = t.parent)
            {
                string n = t.name;
                if (n.StartsWith("RV hurdle")) return HurdleKind(Nearest(R.HurdleHubZ, ObstacleZ(source)));
                if (n.StartsWith("RV bulkhead")) return "bulkhead " + (Nearest(R.BulkheadZ, ObstacleZ(source)) + 1);
                if (n.StartsWith("RV hammer")) return "hammer " + (Nearest(R.HammerAxleZ, ObstacleZ(source)) + 1);
                if (n.StartsWith("RV press")) return "press crush";
                if (n.StartsWith("RV drawer")) return DrawerKind(Nearest(R.DrawerZ, ObstacleZ(source)));
                if (n.StartsWith("RV door")) return "door";
            }
            return source.name;
        }

        static string HurdleKind(int h) => "hurdle " + (h + 1);
        static string DrawerKind(int i) => "drawer " + (i + 1);

        static string Device(string kind)
        {
            int space = kind.IndexOf(' ');
            string first = space > 0 ? kind.Substring(0, space) : kind;
            switch (first)
            {
                case "hurdle":
                case "bulkhead":
                case "hammer":
                case "press":
                case "drawer":
                case "door": return first;
                default: return "other";
            }
        }

        // The beat whose rules a device's knockdowns count against (the door's wait line stands on the terrace).
        static string DeviceBeat(string device)
        {
            switch (device)
            {
                case "hurdle":
                case "bulkhead": return BeatCorridor;
                case "hammer": return BeatHammers;
                case "press": return BeatSafe;
                case "drawer": return BeatShelf;
                case "door": return BeatDoor;
                default: return null;
            }
        }

        static float ObstacleZ(Component source)
        {
            var mover = source.GetComponentInParent<Obstacle>();
            return Local((mover != null ? mover.transform : source.transform).position).z;
        }

        static int Nearest(double[] zs, float z)
        {
            int best = 0;
            for (int i = 1; i < zs.Length; i++)
                if (Math.Abs(zs[i] - z) < Math.Abs(zs[best] - z)) best = i;
            return best;
        }

        static void Count(Dictionary<string, int> counts, string key)
        {
            if (key == null) return;
            counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
        }

        // A fall: counted at its beat, then back on the highest checkpoint reached after the kill volume's delay - never
        // inside this callback (absolute rule 14): Respawns queues the teleport for the next editor update.
        static void OnFall(ICharacterDriver driver, KillVolume volume)
        {
            var bot = Find(driver);
            if (bot == null || !bot.Running || bot.Done || bot.FallPending || sectionRoot == null) return;
            float now = Time.time;
            var p = bot.Hips != null ? Local(bot.Hips.position) : Vector3.zero;
            // Where it fell: where its hips went down over the pit (else where it last stood), not the box they end in.
            var visit = bot.DropVisit ?? bot.FootVisit ?? bot.Visit;
            string beat = visit != null ? visit.Beat : BeatOf(p);
            bot.Falls++;
            if (visit != null) visit.Falls++;
            Count(bot.FallsAt, beat);
            GiveUp(bot, beat);
            var step = bot.Next < bot.Route.Count ? bot.Route[bot.Next].Kind : Step.Pass;
            if (bot.Risky2 && (step == Step.Flight || bot.OnShelf)) bot.AbyssFalls++;
            if (bot.InAir)
            {
                JumpRow(bot, "fall", p, bot.TakeoffFrom, -1, float.NaN, Flat(p - bot.TakeoffAt).magnitude, now);
                bot.InAir = false;
            }
            // The lost time runs from the first of falls before the bot made the ground good again.
            if (!bot.FallOpen)
            {
                bot.FallOpen = true;
                bot.FellAt = now;
                bot.FallProgress = bot.ProgressZ;
                bot.FallVisit = visit;
            }
            bot.OpenFalls++;
            bot.FallPending = true;
            bot.RespawnQueued = false;
            bot.RespawnAt = now + volume.RespawnDelay;
            bot.FallRun = run;
        }

        static void Respawns(float now)
        {
            foreach (var bot in bots)
            {
                if (!bot.FallPending || bot.RespawnQueued || now < bot.RespawnAt) continue;
                bot.RespawnQueued = true;
                var b = bot;
                EditorApplication.delayCall += () => Respawn(b);
            }
        }

        // Spot index % 6 of the highest checkpoint reached, and on from that checkpoint's way.
        static void Respawn(Bot bot)
        {
            if (!EditorApplication.isPlaying || stopping || bot.FallRun != run || !bot.Running || !bot.FallPending || bot.Driver == null) return;
            if (!bots.Contains(bot)) return;
            var at = bot.Highest != null ? bot.Highest.SpotWorld(bot.Index % 6) : World(SpawnSpot(bot.Index));
            bot.Driver.Teleport(at, sectionRoot.rotation);
            bot.Driver.SetCommand(default);
            float now = Time.time;
            bot.FallPending = bot.RespawnQueued = false;
            bot.FootVisit = bot.DropVisit = null;   // it stands again where it respawns
            bot.FootY = float.NaN;
            bot.DrawerDownAt = -99f;
            bot.Respawns++;
            bot.SettledAt = now + .6f;
            bot.CheckAt = Local(at);
            bot.CheckTime = now;
            Resume(bot, bot.HighestNumber);
        }

        // The highest checkpoint each bot reached in the vault (Checkpoint.Reached fires once per part: kept idempotently).
        static void OnCheckpoint(ICharacterDriver driver, Checkpoint checkpoint)
        {
            if (checkpoint == null || sectionRoot == null) return;
            var bot = Find(driver);
            if (bot == null || !bot.Running || bot.Done) return;
            var course = checkpoint.GetComponent<CourseCheckpoint>();
            if (course == null || !course.transform.IsChildOf(sectionRoot)) return;
            if (bot.Highest != null && course.Number <= bot.HighestNumber) return;
            bot.HighestNumber = course.Number;
            bot.Highest = course;
        }

        // The pad threw a body: a bot that was not making for it (shoved onto it) is on the shelf's way now.
        static void OnThrown(Component who, Vector3 velocity)
        {
            if (who == null) return;
            var bot = bots.FirstOrDefault(b => b.Running && !b.Done && ReferenceEquals(who, b.Driver as Component));
            if (bot == null) return;
            bot.ThrownAt = Time.time;
            bot.Thrown = true;
            bot.Launches++;
            var step = bot.Next < bot.Route.Count ? bot.Route[bot.Next].Kind : Step.Pass;
            if (step == Step.Pad || step == Step.Flight) return;
            bot.Route.Clear();
            bot.Next = 0;
            bot.Going = false;
            bot.Route.Add(new Node(Step.Flight, Vector3.zero));
            AddShelf(bot);
            AddPlates(bot);
            AddDoor(bot, false);
            AddRoom(bot);
        }

        // ------------------------------------------------------------------ startup reachability

        // Each bot threshold against its query's maximum over one period, sampled at 0.02 s (spec 5.2): one it can never
        // meet would leave the bots waiting for ever, so the run fails at once.
        static void AssertReachable()
        {
            double t0 = ObstacleClock.Now;
            var failed = new List<string>();
            Log("Startup reachability: every bot threshold against its query's maximum over one period (0.02 s samples from clock "
                + t0.ToString("0.00", CultureInfo.InvariantCulture) + "):");
            Reach("towers: gap + take-off distance (m) against a run's hop onto the next row", R.TowerGap + TakeoffEdge,
                  R.JumpAirSeconds(R.TowerStep) * R.RunSpeed, failed);
            // Hurdles: HurdleJump's rule against its own flight model on each bot line, at the run's and the sprint's speed
            // (HurdleCleanShare). None at all on a line the bots that watch the bars take: the rule could never jump it. First
            // the model's lengths, every one worked out from RoyalVaultRules' bar (HurdleFlight).
            Log("  note hurdle flight check: bar " + D(R.HurdleBarLow, "0.00") + "-" + D(R.HurdleBarHigh, "0.00") + " m, the feet over "
                + D(HurdleLeadFeet, "0.000") + " m while a bar is within " + D(HurdleLead, "0.00") + " m ahead of the hips (the bar's top + "
                + D(YardBeside, "0.0") + " - the hips' " + D(HurdleHips, "0.00") + "), over the bar's top while its log is within " + D(HurdleTrail, "0.00")
                + " m behind them; a bar reaches " + D(HurdleClear, "0.00") + " m from its hub, " + D(HurdleChord, "0.00") + " m either way along a line "
                + D(HalfX(1) - R.HurdleHubX, "0.0") + " m off it");
            for (int h = 0; h < R.HurdleHubZ.Length; h++)
                for (int side = 1; side >= -1; side -= 2)
                {
                    bool watched = side == HurdleSide(h), blind = side == 1;
                    if (!watched && !blind) continue;
                    double run = HurdleCleanShare(h, HalfX(side), R.RunSpeed, t0), sprint = HurdleCleanShare(h, HalfX(side), R.SprintSpeed, t0);
                    bool ok = !watched || (run > 0 && sprint > 0);
                    Log("  " + (ok ? "ok   " : "FAIL ") + "hurdle " + (h + 1) + ", " + (side > 0 ? "east" : "west") + " line ("
                        + (watched && blind ? "every policy's" : watched ? "Sprint's and Jumper's" : "the blind runner's") + "): through unmet at "
                        + D(R.RunSpeed, "0.0") + " / " + D(R.SprintSpeed, "0.0") + " m/s from " + D(100 * run, "0") + " / " + D(100 * sprint, "0")
                        + " % of the bar's phases by the jump's flight check");
                    if (!ok) failed.Add("hurdle " + (h + 1) + " flight check");
                }
            for (int b = 0; b < R.BulkheadZ.Length; b++)
                for (int side = -1; side <= 1; side += 2)
                    for (int place = -1; place <= 1; place++)
                    {
                        int bb = b;
                        double x = side * R.BulkheadHalfWidth / 2 + place * BulkheadPitch;   // across from the corridor's middle
                        double max = MaxOver(t0, R.BulkheadCycle, t => R.BulkheadClearFor(bb, x, t));
                        string name = "bulkhead " + (b + 1) + (side < 0 ? " west" : " east") + " half ClearFor at x " + D(x, "+0.00;-0.00") + " (s)";
                        Reach(name + ", Jumper", BulkheadNeed(Policy.Jumper, BulkheadCross), max, failed);
                        Reach(name + ", Sprint", BulkheadNeed(Policy.Sprint, BulkheadCross), max, failed);
                    }
            for (int i = 0; i < R.HammerAxleZ.Length; i++)
            {
                int ii = i;
                double max = MaxOver(t0, 360 / R.HammerDegreesPerSecond, t => R.HammerClearFor(ii, t));
                Reach("hammer " + (i + 1) + " ClearFor (s), Jumper", HammerNeed(Policy.Jumper), max, failed);
                Reach("hammer " + (i + 1) + " ClearFor (s), Sprint", HammerNeed(Policy.Sprint), max, failed);
            }
            double press = PressLongestCross(t0);
            Reach("press: crossing time PressSafeToCross ever allows (s), Jumper", PressCross(Policy.Jumper), press, failed);
            Reach("press: crossing time PressSafeToCross ever allows (s), Sprint", PressCross(Policy.Sprint), press, failed);
            for (int i = 0; i < R.DrawerZ.Length; i++)
                for (int lane = -1; lane <= 1; lane++)
                {
                    int ii = i;
                    double x = (R.ShelfX0 + R.ShelfX1) / 2 + lane * .6;
                    double max = MaxOver(t0, R.DrawerCycle, t => R.DrawerClearFor(ii, x, t));
                    string name = "drawer " + (i + 1) + " ClearFor at x " + D(x, "0.0") + " (s)";
                    Reach(name + ", Jumper", DrawerNeed(Policy.Jumper), max, failed);
                    Reach(name + ", Sprint", DrawerNeed(Policy.Sprint), max, failed);
                }
            for (int row = 0; row < 2; row++)
            {
                int rr = row;
                double max = MaxOver(t0, R.PlateCycle, t => R.PlateFlatFor(rr, t));
                string name = "plate row " + (row == 0 ? "west" : "east") + " FlatFor (s)";
                Reach(name + " to enter, Jumper", PlateEnter(Policy.Jumper), max, failed);
                Reach(name + " to enter, Sprint", PlateEnter(Policy.Sprint), max, failed);
                Reach(name + " to step onto, Jumper", PlateTake(Policy.Jumper), max, failed);
                Reach(name + " to step onto, Sprint", PlateTake(Policy.Sprint), max, failed);
            }
            double door = MaxOver(t0, R.DoorPeriod, R.DoorPassableFor);
            float backRow = DoorLineZ - 2 * DoorRowPitch;
            Reach("door PassableFor (s), Jumper", DoorNeed(Policy.Jumper), door, failed);
            Reach("door PassableFor (s), Sprint", DoorNeed(Policy.Sprint), door, failed);
            Reach("door PassableFor (s) from the third row, Jumper", DoorNeedFrom(Policy.Jumper, backRow), door, failed);
            Reach("door PassableFor (s) from the third row, Sprint", DoorNeedFrom(Policy.Sprint, backRow), door, failed);
            for (int belt = 0; belt < 2; belt++)
            {
                int bb = belt;
                Func<double, double> ride = t => R.BeltRideSeconds(bb, t, ForkSpeed, R.BeltZ1 - R.BeltZ0);
                Log("  note " + (belt == 0 ? "west" : "east") + " belt: " + D(R.BeltZ1 - R.BeltZ0, "0") + " m at " + D(ForkSpeed, "0.0") + " m/s in "
                    + D(-MaxOver(t0, 2 * R.EastHalf, t => -ride(t)), "0.00") + "-" + D(MaxOver(t0, 2 * R.EastHalf, ride), "0.00") + " s");
            }
            if (failed.Count > 0) throw new InvalidOperationException("startup reachability failed: " + string.Join("; ", failed));
        }

        static void Reach(string what, double threshold, double max, List<string> failed)
        {
            bool ok = threshold <= max;
            Log("  " + (ok ? "ok   " : "FAIL ") + what + ": threshold " + D(threshold, "0.000") + ", max " + D(max, "0.000"));
            if (!ok) failed.Add(what);
        }

        static double MaxOver(double t0, double period, Func<double, double> f)
        {
            double max = double.MinValue;
            for (double s = 0; s < period; s += .02) max = Math.Max(max, f(t0 + s));
            return max;
        }

        // The longest crossing (0.05 s steps) PressSafeToCross allows at some moment of a period.
        static double PressLongestCross(double t0)
        {
            double longest = 0;
            for (double cross = .05; cross < R.PressPeriod; cross += .05)
            {
                bool any = false;
                for (double s = 0; s < R.PressPeriod && !any; s += .02) any = R.PressSafeToCross(t0 + s, cross);
                if (!any) break;
                longest = cross;
            }
            return longest;
        }

        // ------------------------------------------------------------------ records

        static void EndRun(float now)
        {
            FlushHits(now, true);
            float leader = float.NaN;
            Bot first = null;
            foreach (var bot in bots)
            {
                CloseVisit(bot, bot.Done ? bot.FinishedAt : now);
                var c = MakeCrossing(bot);
                crossings.Add(c);
                if (c.Finished && (float.IsNaN(leader) || c.Seconds < leader)) { leader = c.Seconds; first = bot; }
                crossingRows.Append(string.Join(",",
                    run + 1, seed, bot.Index + 1, bot.Policy, bot.Piece, c.Branch1, c.Branch2, c.Lane, c.Finished ? 1 : 0, c.Finished ? F(c.Seconds) : "",
                    bot.Downs, bot.Staggers, bot.Falls, bot.Respawns, bot.Drops, F(bot.HammerWait0), F(bot.HammerWait1), F(bot.PressWait), F(bot.BeltTime),
                    F(bot.DrawerWait), F(bot.PlateWait), F(bot.DoorWait), F(bot.Alone), F(bot.MaxSpeed), F(bot.MaxRise), bot.Stuck, bot.Flung, bot.Pinned,
                    bot.GaveUpCount, ProbeName, F(bot.BulkheadWait), F(bot.TowerWait), F(c.Fork1), F(c.Fork2), bot.Launches, bot.ShelfLandings,
                    bot.AbyssFalls, bot.HammerDouble, c.BothHammers ? 1 : 0, bot.PressPinches, bot.DoorPinches, c.Unrecovered, F(bot.Lost))).Append('\n');
                foreach (var visit in bot.Visits)
                    beatRows.Append(string.Join(",", run + 1, bot.Index + 1, visit.Beat, F(visit.In), F(visit.Out), visit.Downs, visit.Falls, F(visit.Waits),
                                                ProbeName, bot.Policy, bot.Piece, visit.Hits, F(visit.Lost))).Append('\n');
                bot.Running = false;
                bot.Driver.SetCommand(default);
            }
            leaders.Add(leader);
            DoorRows();
            File.AppendAllText(Path.Combine(Folder, CrossingsFile), crossingRows.ToString());
            File.AppendAllText(Path.Combine(Folder, BeatsFile), beatRows.ToString());
            File.AppendAllText(Path.Combine(Folder, DoorFile), doorRows.ToString());
            File.AppendAllText(Path.Combine(Folder, HitsFile), hitRows.ToString());
            File.AppendAllText(Path.Combine(Folder, TraceFile), traceRows.ToString());
            crossingRows.Clear(); beatRows.Clear(); doorRows.Clear(); hitRows.Clear(); traceRows.Clear();
            var mine = crossings.Where(c => c.Run == run).ToList();
            Log("Run " + (run + 1) + ": " + mine.Count(c => c.Finished) + " of " + mine.Count + " through, leader "
                + (first != null ? S(leader) + " s (bot " + (first.Index + 1) + ", " + first.Policy + ", " + Branches(mine.First(c => c.Bot == first.Index)) + ")" : "none")
                + ", knockdowns " + mine.Sum(c => c.Downs) + ", falls " + mine.Sum(c => c.Falls) + ", drops to the belts " + mine.Sum(c => c.Drops)
                + ", stuck " + mine.Sum(c => c.Stuck) + ", flung " + mine.Sum(c => c.Flung) + ", pinned " + mine.Sum(c => c.Pinned));
        }

        static Crossing MakeCrossing(Bot bot)
        {
            bool fork1 = probe == Probe.Full || probe == Probe.Walk || probe == Probe.Fork1;
            bool fork2 = probe == Probe.Full || probe == Probe.Walk || probe == Probe.Fork2Safe || probe == Probe.Fork2Risky;
            var sums = new Dictionary<string, BeatVisit>();
            foreach (var visit in bot.Visits)
            {
                if (visit.Beat == null) continue;
                if (!sums.TryGetValue(visit.Beat, out var sum)) sums[visit.Beat] = sum = new BeatVisit { Beat = visit.Beat, In = visit.In };
                if (!float.IsNaN(visit.Out)) sum.Out = visit.Out;
                sum.Downs += visit.Downs;
                sum.Falls += visit.Falls;
                sum.Waits += visit.Waits;
                sum.Lost += visit.Lost;
                sum.Hits += visit.Hits;
            }
            return new Crossing
            {
                Run = run, Bot = bot.Index, Policy = bot.Policy, Piece = bot.Piece,
                Branch1 = fork1 ? (bot.Risky1 ? "risky" : "safe") : "",
                Branch2 = fork2 ? (bot.Risky2 ? "risky" : "safe") : "",
                Lane = fork1 && bot.Risky1 ? (bot.Lane > 0f ? "E" : "W") : "",
                Finished = bot.Done, Seconds = bot.Done ? bot.FinishedAt - bot.Started : float.NaN,
                Fork1 = fork1 && bot.AtJ1 >= 0f ? bot.AtJ1 : float.NaN,
                Fork2 = fork2 && bot.AtJ2 >= 0f && bot.LeftWalk >= 0f ? bot.AtJ2 - bot.LeftWalk : float.NaN,
                Downs = bot.Downs, Falls = bot.Falls, Drops = bot.Drops, Launches = bot.Launches, ShelfLandings = bot.ShelfLandings,
                AbyssFalls = bot.AbyssFalls, HammerDouble = bot.HammerDouble, Stuck = bot.Stuck, Flung = bot.Flung, Pinned = bot.Pinned,
                PressPinches = bot.PressPinches, DoorPinches = bot.DoorPinches, Unrecovered = bot.Falls - bot.Recovered,
                BothHammers = bot.DownByHammer0 && bot.DownByHammer1,
                HammerWait0 = bot.HammerWait0, HammerWait1 = bot.HammerWait1, PressWait = bot.PressWait, BeltTime = bot.BeltTime,
                DrawerWait = bot.DrawerWait, PlateWait = bot.PlateWait, DoorWait = bot.DoorWait, BulkheadWait = bot.BulkheadWait,
                DownsBy = new Dictionary<string, int>(bot.DownsBy), DownLog = new List<KeyValuePair<string, float>>(bot.DownLog),
                RepeatFeet = new List<float>(bot.RepeatFeet), Beats = sums,
            };
        }

        static string Branches(Crossing c) =>
            (c.Branch1.Length > 0 ? (c.Branch1 == "risky" ? "towers " + c.Lane : "corridor") : "") +
            (c.Branch1.Length > 0 && c.Branch2.Length > 0 ? " + " : "") +
            (c.Branch2.Length > 0 ? (c.Branch2 == "risky" ? "pad" : "belts") : "");

        // door.csv: per opening of this run, the bots through it - those already at the line before it opened, the first and
        // last to arrive and to pass.
        static void DoorRows()
        {
            foreach (var opening in doorPasses.Where(d => d.Run == run).GroupBy(d => d.Opening).OrderBy(g => g.Key))
            {
                double opens = opening.Key * R.DoorPeriod + R.DoorPassableStart;
                var list = opening.ToList();
                doorRows.Append(string.Join(",", run + 1, opening.Key, D(opens - runClock, "0.###"), list.Count(d => d.ArrivedClock < opens),
                                            F(list.Min(d => d.Arrived)), F(list.Max(d => d.Arrived)), F(list.Min(d => d.Passed)), F(list.Max(d => d.Passed)),
                                            list.Count, ProbeName)).Append('\n');
            }
        }

        // hits.csv rows, each written 1.5 s after the hit with how far back (along -z) it pushed the bot.
        static void FlushHits(float now, bool all)
        {
            int kept = 0;
            for (int i = 0; i < pendingHits.Count; i++)
            {
                var hit = pendingHits[i];
                if (hit.Bot.Hips != null && !hit.Bot.FallPending) hit.MinZ = Mathf.Min(hit.MinZ, Local(hit.Bot.Hips.position).z);
                if (!all && now - hit.At < 1.5f)
                {
                    pendingHits[kept++] = hit;
                    continue;
                }
                float back = hit.Z - hit.MinZ;
                hitRows.Append(hit.Row).Append(',').Append(F(back)).Append('\n');
                if (hit.HammerDown) hammerBacks.Add(new KeyValuePair<PieceKind, float>(hit.Bot.Piece, back));
            }
            pendingHits.RemoveRange(kept, pendingHits.Count - kept);
        }

        static void Trace(Bot bot, Vector3 p, Vector3 dir, bool jump, float now, Step step)
        {
            var v = LocalDir(bot.Hips.linearVelocity);
            float tilt = Vector3.Angle(bot.Hips.transform.up, sectionRoot.up);
            float ground = float.IsNaN(bot.Ground) ? float.NaN : p.y - bot.Ground;
            traceRows.Append(string.Join(",", ProbeName, bot.Index + 1, bot.Policy, F(now - bot.Started), F(p.x), F(p.y), F(p.z), F(v.x), F(v.y), F(v.z),
                                         F(tilt), F(ground), bot.Feet ? 1 : 0, F(dir.x), F(dir.z), jump ? 1 : 0, bot.Waiting ? 1 : 0, run + 1, bot.Piece,
                                         bot.Visit != null ? bot.Visit.Beat : "", step, "", "", "", "", "")).Append('\n');
        }

        // A tower jump's take-off (metres to the edge), landing (how far inside the top it came down, and the jump's length)
        // or fall, in every run.
        static void JumpRow(Bot bot, string what, Vector3 p, int from, int to, float edge, float length, float now)
        {
            var v = bot.Hips != null ? LocalDir(bot.Hips.linearVelocity) : Vector3.zero;
            traceRows.Append(string.Join(",", ProbeName, bot.Index + 1, bot.Policy, F(now - bot.Started), F(p.x), F(p.y), F(p.z), F(v.x), F(v.y), F(v.z),
                                         "", "", bot.Feet ? 1 : 0, "", "", what == "takeoff" ? 1 : 0, 0, run + 1, bot.Piece,
                                         bot.Visit != null ? bot.Visit.Beat : "", Step.Towers, what, from, to, F(edge), F(length))).Append('\n');
        }

        static void Anomaly(Bot bot, string what)
        {
            var p = bot.Hips != null ? Local(bot.Hips.position) : Vector3.zero;
            string line = "run " + (run + 1) + " bot " + (bot.Index + 1) + " (" + bot.Policy + ", " + bot.Piece + ") at " + V(p) + " in "
                          + (bot.Visit != null ? bot.Visit.Beat : "?") + ": " + what;
            anomalies.Add(line);
            Log("ANOMALY " + line);
        }

        // What solid things are within 1.5 m (for a stuck bot): what it may be caught on.
        static string Near(Vector3 world)
        {
            var names = Physics.OverlapSphere(world, 1.5f, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => c.GetComponentInParent<ICharacterDriver>() == null).Select(c => c.name).Distinct().Take(5).ToArray();
            return names.Length > 0 ? string.Join(", ", names) : "nothing solid (other pawns?)";
        }

        // ------------------------------------------------------------------ the body

        // How high the hips' ground is (section frame): the nearest solid thing under them that is not a pawn, NaN within 3 m none;
        // `deck`: that ground is a flipping plate's deck.
        static float GroundY(Vector3 hips, out bool deck)
        {
            int n = Physics.RaycastNonAlloc(hips + sectionRoot.up * .1f, -sectionRoot.up, under, 3f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, y = float.NaN;
            deck = false;
            for (int i = 0; i < n; i++)
            {
                if (under[i].collider.GetComponentInParent<ICharacterDriver>() != null || under[i].distance >= best) continue;
                best = under[i].distance;
                y = Local(under[i].point).y;
                deck = under[i].collider.name == VaultPlate.DeckName;
            }
            return y;
        }

        // On its feet on something (the yard's test): not in the air (the hips over 0.8 m above whatever is under them, or
        // rising or falling fast), not knocked down or getting up, and not lying (tilted over 60 degrees with the hips low).
        static bool OnFeet(Bot bot)
        {
            if (bot.Hips == null || Mathf.Abs(bot.Hips.linearVelocity.y) > 2.5f) return false;
            if (bot.Driver is IMiniGamePawn pawn && pawn.Down) return false;
            var from = bot.Hips.position + Vector3.up * .1f;
            int n = Physics.RaycastNonAlloc(from, Vector3.down, under, 1.2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (under[i].collider.GetComponentInParent<ICharacterDriver>() == null) best = Mathf.Min(best, under[i].distance - .1f);
            if (best > .8f) return false;
            bool lying = Vector3.Angle(bot.Hips.transform.up, Vector3.up) > 60f && best < .31f;
            return !lying;
        }

        static Vector3 Local(Vector3 world) => sectionRoot.InverseTransformPoint(world);
        static Vector3 World(Vector3 local) => sectionRoot.TransformPoint(local);
        static Vector3 WorldDir(Vector3 local) => sectionRoot.TransformDirection(local);
        static Vector3 LocalDir(Vector3 world) => sectionRoot.InverseTransformDirection(world);
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static Vector3 Toward(Vector3 from, Vector3 to)
        {
            var d = Flat(to - from);
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.zero;
        }

        // ------------------------------------------------------------------ pictures

        // One fixed view per beat, the first time a bot enters it in the first run, and a view from above (under the roof).
        static void SchedulePicture(string beat)
        {
            if (!View(beat, out string name, out var eye, out var target)) return;
            Later(1f, () => Picture(name, eye, target, 60f, .1f));
        }

        static bool View(string beat, out string name, out Vector3 eye, out Vector3 target)
        {
            switch (beat)
            {
                case BeatForecourt: name = "beat0_forecourt"; eye = new Vector3(0f, 9f, -8f); target = new Vector3(0f, 0f, 12f); return true;
                case BeatTowers: name = "beat1_towers"; eye = new Vector3(13f, 12f, 16f); target = new Vector3(0f, 2f, 34f); return true;
                case BeatCorridor: name = "beat1_corridor"; eye = new Vector3(-6f, 11f, 4f); target = new Vector3(-18f, 0f, 24f); return true;
                case BeatJ1: name = "beatJ1"; eye = new Vector3(-7f, 13f, 42f); target = new Vector3(-7f, 4f, 56f); return true;
                case BeatHammers: name = "beat2_hammers"; eye = new Vector3(13f, 11f, 58f); target = new Vector3(0f, 5f, 72f); return true;
                case BeatFork2: name = "beat3_approach"; eye = new Vector3(-10f, 13f, 74f); target = new Vector3(2f, 5f, 90f); return true;
                case BeatSafe: name = "beat3_belts_press"; eye = new Vector3(-13f, 13f, 94f); target = new Vector3(-1f, 4f, 108f); return true;
                case BeatShelf: name = "beat3_shelf"; eye = new Vector3(-6f, 15f, 88f); target = new Vector3(5f, 8f, 100f); return true;
                case BeatJ2: name = "beatJ2"; eye = new Vector3(0f, 15f, 118f); target = new Vector3(0f, 8f, 133f); return true;
                case BeatPlates: name = "beat4_plates"; eye = new Vector3(11f, 15f, 131f); target = new Vector3(0f, 8f, 146f); return true;
                case BeatTerrace: name = "beat5_terrace"; eye = new Vector3(0f, 19f, 158f); target = new Vector3(0f, 12f, 175f); return true;
                case BeatDoor: name = "beat6_door"; eye = new Vector3(9f, 17f, 169f); target = new Vector3(0f, 14f, 180f); return true;
                default: name = null; eye = target = Vector3.zero; return false;
            }
        }

        static void Later(float after, Action shot)
        {
            float at = Time.time + after;
            EditorApplication.CallbackFunction wait = null;
            wait = () =>
            {
                if (!EditorApplication.isPlaying) { EditorApplication.update -= wait; return; }
                if (Time.time < at) return;
                EditorApplication.update -= wait;
                pictures.Enqueue(shot);
            };
            EditorApplication.update += wait;
        }

        // A view in the section frame; `near` clips what is between the eye and the course (the roof, from above).
        static void Picture(string name, Vector3 eyeLocal, Vector3 targetLocal, float fieldOfView, float near)
        {
            if (NoCapture || captureCamera == null || sectionRoot == null) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Log("NOT RUN: capture " + name + " (null graphics device)"); return; }
            const int width = 1600, height = 1000;
            var eye = World(eyeLocal);
            var look = World(targetLocal) - eye;
            captureCamera.transform.position = eye;
            captureCamera.transform.rotation = Quaternion.LookRotation(look, Mathf.Abs(Vector3.Dot(look.normalized, Vector3.up)) > .99f ? sectionRoot.forward : Vector3.up);
            captureCamera.orthographic = false;
            captureCamera.fieldOfView = fieldOfView;
            captureCamera.nearClipPlane = near;
            captureCamera.farClipPlane = near + 500f;
            captureCamera.aspect = (float)width / height;
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var before = RenderTexture.active;
            Texture2D picture = null;
            try
            {
                texture.Create();
                var request = new RenderPipeline.StandardRequest { destination = texture };
                if (GraphicsSettings.currentRenderPipeline != null)
                {
                    if (!RenderPipeline.SupportsRenderRequest(captureCamera, request)) { Log("NOT RUN: capture " + name + " (render request unsupported)"); return; }
                    RenderPipeline.SubmitRenderRequest(captureCamera, request);
                }
                else { captureCamera.targetTexture = texture; captureCamera.Render(); captureCamera.targetTexture = null; }
                RenderTexture.active = texture;
                picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                picture.Apply();
                string path = Path.Combine(Folder, name + ".png");
                File.WriteAllBytes(path, picture.EncodeToPNG());
                Log("Captured " + path + " at run time " + S(Time.time - runBegan) + " s");
            }
            finally
            {
                RenderTexture.active = before;
                if (picture != null) Object.DestroyImmediate(picture);
                texture.Release(); Object.DestroyImmediate(texture);
                captureCamera.nearClipPlane = .1f;
                captureCamera.farClipPlane = 500f;
            }
        }

        // ------------------------------------------------------------------ the report

        static void Finish()
        {
            Log("");
            Log("RESULTS: probe " + ProbeName + ", " + runs + " run(s) x " + BotCount + " bots, seed " + seed + ", pieces " + (mix ? "mix" : "pawn")
                + ", " + S(limit, "0") + " s a run (" + crossings.Count + " crossings)");
            Summary();
            BeatTable();
            HurdleChains();
            Waits();
            Acceptance();
            ForkModel();
            Log("Anomalies: " + anomalies.Count);
            foreach (var a in anomalies) Log("  " + a);
            Log("Pad throws with no landing after " + S(FlightStall, "0") + " s (FLIGHT STALL; the bot went on from where it was): " + stalls.Count);
            foreach (var a in stalls) Log("  " + a);
            foreach (var e in errors) Log("ERROR logged in Play: " + e.Split('\n')[0]);
            int code = errors.Count == 0 ? 0 : 1;
            Log("Bot test result: " + (code == 0 ? "DONE" : "DONE with " + errors.Count + " errors in Play") + " (bot proxies; read the acceptance lines above)");
            CourseSkills.PawnRush = false;
            StopPlay(code);
        }

        static void Summary()
        {
            foreach (var policy in new Policy?[] { null, Policy.Run, Policy.Sprint, Policy.Jumper })
            {
                var set = crossings.Where(c => policy == null || c.Policy == policy).ToList();
                if (set.Count == 0) continue;
                var done = set.Where(c => c.Finished).Select(c => c.Seconds).ToList();
                Log("  " + (policy == null ? "all" : policy.ToString()).PadRight(7) + " through " + Pct(done.Count, set.Count)
                    + (done.Count > 0 ? ", seconds median " + S(Median(done)) + " (" + S(done.Min()) + "-" + S(done.Max()) + ")" : "")
                    + ", knockdowns per crossing " + S((float)set.Sum(c => c.Downs) / set.Count, "0.00") + ", falls per crossing "
                    + S((float)set.Sum(c => c.Falls) / set.Count, "0.00") + ", unrecovered falls " + set.Sum(c => c.Unrecovered)
                    + ", stuck " + set.Sum(c => c.Stuck) + ", flung " + set.Sum(c => c.Flung) + ", pinned " + set.Sum(c => c.Pinned));
            }
            Log("  leader per run: " + string.Join(", ", leaders.Select((l, i) => "run " + (i + 1) + " " + S(l) + " s")));
            foreach (var combo in crossings.GroupBy(Branches).OrderBy(g => g.Key))
            {
                if (combo.Key.Length == 0) continue;
                var done = combo.Where(c => c.Finished).ToList();
                var best = done.OrderBy(c => c.Seconds).FirstOrDefault();
                Log("  " + combo.Key.PadRight(20) + " " + combo.Count() + " crossings, through " + Pct(done.Count, combo.Count())
                    + (best != null ? ", median " + S(Median(done.Select(c => c.Seconds))) + " s, leader " + S(best.Seconds) + " s (run " + (best.Run + 1)
                                      + " bot " + (best.Bot + 1) + ", " + best.Policy + ")" : ""));
            }
        }

        // Per beat: crossings that entered it, with a knockdown or a fall there, and the median time from the first entry to
        // the last exit.
        static void BeatTable()
        {
            Log("Beats (share of the crossings that entered each beat):");
            foreach (var beat in BeatOrder.Concat(crossings.SelectMany(c => c.Beats.Keys).Distinct().Where(b => !BeatOrder.Contains(b))))
            {
                var set = Visited(crossings, beat);
                if (set.Count == 0) continue;
                Log("  " + beat.PadRight(14) + " " + set.Count + " crossings, median " + S(Median(set.Select(c => Duration(At(c, beat))))) + " s"
                    + ", knocked down " + Pct(set.Count(c => At(c, beat).Downs > 0), set.Count) + ", fell " + Pct(set.Count(c => At(c, beat).Falls > 0), set.Count)
                    + ", hits " + set.Sum(c => At(c, beat).Hits) + ", waits mean " + S(Mean(set.Select(c => At(c, beat).Waits))) + " s"
                    + ", time lost to falls " + S(set.Sum(c => At(c, beat).Lost)) + " s");
            }
            // Each device's knockdowns, and which of its parts gave them (hurdle 1 / 2, drawer 1 / 2, the press's crush / wave).
            var kinds = crossings.SelectMany(c => c.DownLog).GroupBy(d => d.Key).OrderBy(g => g.Key)
                                 .Select(g => new KeyValuePair<string, int>(g.Key, g.Count())).ToList();
            var by = crossings.SelectMany(c => c.DownsBy).GroupBy(p => p.Key).Select(g =>
            {
                var parts = kinds.Where(k => Device(k.Key) == g.Key && k.Key != g.Key).Select(k => k.Key + " " + k.Value).ToList();
                return g.Key + " " + g.Sum(p => p.Value) + (parts.Count > 0 ? " (" + string.Join(", ", parts) + ")" : "");
            }).ToList();
            if (by.Count > 0) Log("  knockdowns by device: " + string.Join(", ", by));
        }

        // The hurdle corridor's knockdown chains, to see what the hurdle hit's cooldown does (information, not acceptance; the
        // blind runner still gives up being blind after 3 knockdowns there): per policy over its corridor crossings, the
        // knockdowns per crossing from each hurdle, the most one bot took from one hurdle within ChainWindow s, how long a bot
        // had stood before a hurdle floored it again (RepeatFeet), and the corridor's median beat time.
        static void HurdleChains()
        {
            var corridor = Visited(crossings, BeatCorridor).Where(c => c.Branch1 == "safe").ToList();
            if (corridor.Count == 0) return;
            Log("Hurdle corridor (information, not acceptance; " + corridor.Count + " corridor crossings):");
            Crossing worst = null;
            int worstChain = 0, worstHurdle = -1;
            foreach (var policy in new[] { Policy.Run, Policy.Sprint, Policy.Jumper })
            {
                var set = Of(corridor, policy);
                if (set.Count == 0) continue;
                int most = 0;
                foreach (var c in set)
                    for (int h = 0; h < R.HurdleHubZ.Length; h++)
                    {
                        int chain = Chain(c, HurdleKind(h));
                        most = Math.Max(most, chain);
                        if (chain > worstChain) { worstChain = chain; worst = c; worstHurdle = h; }
                    }
                var each = Enumerable.Range(0, R.HurdleHubZ.Length)
                                     .Select(h => HurdleKind(h) + " " + S((float)set.Average(c => KindDowns(c, HurdleKind(h))), "0.00"));
                // R725 review: the seconds on its feet before each knockdown by the hurdle that floored it last (none while
                // getting up is the fun pass's aim: a second and more, by the hurdle model).
                var feet = set.SelectMany(c => c.RepeatFeet).ToList();
                Log("  " + policy.ToString().PadRight(7) + " hurdle knockdowns per crossing: " + string.Join(", ", each)
                    + "; most from one hurdle within " + S(ChainWindow, "0") + " s: " + most
                    + "; on its feet before a repeat by the same hurdle: " + (feet.Count == 0 ? "no repeats"
                        : "shortest " + S(feet.Min(), "0.00") + " s, median " + S(Median(feet), "0.00") + " s (n " + feet.Count + ")")
                    + "; corridor beat median " + S(Median(set.Select(c => Duration(At(c, BeatCorridor))))) + " s (n " + set.Count + ")");
            }
            Log("  most knockdowns one bot took from one hurdle within " + S(ChainWindow, "0") + " s: " + (worst == null ? "0 (no hurdle knockdowns)"
                : worstChain + " (" + HurdleKind(worstHurdle) + ", run " + (worst.Run + 1) + " bot " + (worst.Bot + 1) + ", " + worst.Policy + ")"));
        }

        // A crossing's knockdowns of one Kind, and the most of them within ChainWindow s of each other.
        static int KindDowns(Crossing c, string kind) => c.DownLog.Count(d => d.Key == kind);

        static int Chain(Crossing c, string kind)
        {
            var times = c.DownLog.Where(d => d.Key == kind).Select(d => d.Value).OrderBy(t => t).ToList();
            int most = 0;
            for (int i = 0, j = 0; j < times.Count; j++)
            {
                while (times[j] - times[i] > ChainWindow) i++;
                most = Math.Max(most, j - i + 1);
            }
            return most;
        }

        static void Waits()
        {
            Log("Waits (mean seconds per crossing that met the device):");
            foreach (var policy in new[] { Policy.Run, Policy.Sprint, Policy.Jumper })
            {
                var set = Of(crossings, policy);
                if (set.Count == 0) continue;
                Log("  " + policy.ToString().PadRight(7)
                    + " bulkheads " + S(Mean(Visited(set, BeatCorridor).Where(c => c.Branch1 == "safe").Select(c => c.BulkheadWait)))
                    + ", hammer 1 " + S(Mean(Visited(set, BeatHammers).Select(c => c.HammerWait0)))
                    + ", hammer 2 " + S(Mean(Visited(set, BeatHammers).Select(c => c.HammerWait1)))
                    + ", belts ridden " + S(Mean(set.Where(c => c.BeltTime > 0f).Select(c => c.BeltTime)))
                    + ", press " + S(Mean(set.Where(c => c.Beats.ContainsKey(BeatSafe) && c.Branch2 != "risky").Select(c => c.PressWait)))
                    + ", drawers " + S(Mean(set.Where(c => c.ShelfLandings > 0).Select(c => c.DrawerWait)))
                    + ", plates " + S(Mean(Visited(set, BeatPlates).Select(c => c.PlateWait)))
                    + ", door " + S(Mean(Visited(set, BeatDoor).Select(c => c.DoorWait))));
            }
        }

        static void Acceptance()
        {
            var all = crossings;
            bool whole = probe == Probe.Full || probe == Probe.Walk;
            const float inf = float.PositiveInfinity;
            Log("");
            Log("ACCEPTANCE (spec 5.3, bot proxies: [in] inside the range, [OUT] outside it, [n/a] not met in this probe; meant for 'full' with pawns)");
            Check("stuck", all.Sum(c => c.Stuck), 0, 0, "", all.Count);
            Check("flung", all.Sum(c => c.Flung), 0, 0, "", all.Count);
            Check("pinned", all.Sum(c => c.Pinned), 0, 0, "", all.Count);
            Check("unrecovered falls", all.Sum(c => c.Unrecovered), 0, 0, "", all.Count);
            Check("Play errors", errors.Count, 0, 0, "", 1);
            Check("crossings through within " + S(limit, "0") + " s", Share(all, c => c.Finished), 95, 100, " %", all.Count);
            if (whole)
            {
                for (int r = 0; r < leaders.Count; r++) Check("run " + (r + 1) + " leader", leaders[r], 70, 90, " s", float.IsNaN(leaders[r]) ? 0 : 1);
                var through = all.Where(c => c.Finished).Select(c => c.Seconds).ToList();
                Check("median of all finishers", Median(through), 105, 140, " s", through.Count);
            }

            var forecourt = Visited(all, BeatForecourt);
            Check("0 forecourt: knockdowns", forecourt.Sum(c => At(c, BeatForecourt).Downs), 0, 0, "", forecourt.Count);
            Check("0 forecourt: falls", forecourt.Sum(c => At(c, BeatForecourt).Falls), 0, 0, "", forecourt.Count);

            var towers = Visited(all, BeatTowers).Where(c => c.Branch1 == "risky").ToList();
            Check("1 towers: Jumper crossings with a fall", Share(Of(towers, Policy.Jumper), c => At(c, BeatTowers).Falls > 0), 10, 35, " %", Of(towers, Policy.Jumper).Count);
            Check("1 towers: Sprint crossings with a fall", Share(Of(towers, Policy.Sprint), c => At(c, BeatTowers).Falls > 0), -inf, 45, " %", Of(towers, Policy.Sprint).Count);
            Check("1 towers: Run crossings with a fall", Share(Of(towers, Policy.Run), c => At(c, BeatTowers).Falls > 0), -inf, 65, " %", Of(towers, Policy.Run).Count);
            var clean = Of(towers, Policy.Jumper).Where(c => At(c, BeatTowers).Falls == 0).Select(c => Duration(At(c, BeatTowers))).ToList();
            Check("1 towers: Jumper beat time without a fall (median)", Median(clean), 10, 16, " s", clean.Count);

            var corridor = Visited(all, BeatCorridor).Where(c => c.Branch1 == "safe").ToList();
            var careful = corridor.Where(c => c.Policy != Policy.Run).ToList();
            Check("1 corridor: crossings knocked down by a hurdle", Share(corridor, c => Downs(c, "hurdle") > 0), 15, 55, " %", corridor.Count);
            Check("1 corridor: falls", corridor.Sum(c => At(c, BeatCorridor).Falls), 0, 0, "", corridor.Count);
            Check("1 corridor: bulkhead wait (mean, Sprint and Jumper)", Mean(careful.Select(c => c.BulkheadWait)), -inf, 2.5f, " s", careful.Count);
            Check("1 corridor: beat time (median)", Median(corridor.Select(c => Duration(At(c, BeatCorridor)))), 18, 32, " s", corridor.Count);

            foreach (var quiet in new[] { BeatJ1, BeatJ2, BeatTerrace })
            {
                var set = Visited(all, quiet);
                Check(quiet + ": hits", set.Sum(c => At(c, quiet).Hits), 0, 0, "", set.Count);
                Check(quiet + ": falls", set.Sum(c => At(c, quiet).Falls), 0, 0, "", set.Count);
            }

            var hammers = Visited(all, BeatHammers);
            var timing = hammers.Where(c => c.Policy != Policy.Run).ToList();
            Check("2 hammers: Jumper crossings knocked down", Share(Of(hammers, Policy.Jumper), c => Downs(c, "hammer") > 0), -inf, 20, " %", Of(hammers, Policy.Jumper).Count);
            Check("2 hammers: Sprint crossings knocked down", Share(Of(hammers, Policy.Sprint), c => Downs(c, "hammer") > 0), -inf, 35, " %", Of(hammers, Policy.Sprint).Count);
            Check("2 hammers: Run crossings knocked down", Share(Of(hammers, Policy.Run), c => Downs(c, "hammer") > 0), 30, inf, " %", Of(hammers, Policy.Run).Count);
            Check("2 hammers: crossings with a fall", Share(hammers, c => At(c, BeatHammers).Falls > 0), -inf, 10, " %", hammers.Count);
            Check("2 hammers: hammer-1 wait (mean, Sprint and Jumper)", Mean(timing.Select(c => c.HammerWait0)), .5f, 3f, " s", timing.Count);
            Check("2 hammers: hammer-2 wait (mean, Run; blind unless it gave up)", Mean(Of(hammers, Policy.Run).Select(c => c.HammerWait1)), -inf, 4.5f, " s", Of(hammers, Policy.Run).Count);
            Check("2 hammers: hammer-2 wait (mean, Sprint and Jumper)", Mean(timing.Select(c => c.HammerWait1)), -inf, 1.5f, " s", timing.Count);
            Check("2 hammers: double hits in one pass", hammers.Sum(c => c.HammerDouble), 0, 0, "", hammers.Count);

            var safe = Visited(all, BeatSafe).Where(c => c.Branch2 == "safe").ToList();
            var waiting = safe.Where(c => c.Policy != Policy.Run).ToList();
            Check("3 safe: Jumper crossings knocked down by the press", Share(Of(safe, Policy.Jumper), c => Downs(c, "press") > 0), -inf, 15, " %", Of(safe, Policy.Jumper).Count);
            Check("3 safe: Run crossings knocked down by the press", Share(Of(safe, Policy.Run), c => Downs(c, "press") > 0), 25, inf, " %", Of(safe, Policy.Run).Count);
            Check("3 safe: press wait (mean, Sprint and Jumper)", Mean(waiting.Select(c => c.PressWait)), .3f, 2.5f, " s", waiting.Count);
            Check("3 safe: belt time (mean)", Mean(safe.Select(c => c.BeltTime)), 4, 10, " s", safe.Count);
            Check("3 safe: press pinches", all.Sum(c => c.PressPinches), 0, 0, "", safe.Count);

            var risky = all.Where(c => c.Branch2 == "risky" && c.Launches > 0).ToList();
            var shelf = risky.Where(c => c.ShelfLandings > 0).ToList();
            Check("3 risky: Jumper crossings dropped to the belts", Share(Of(shelf, Policy.Jumper), c => c.Drops > 0), 5, 30, " %", Of(shelf, Policy.Jumper).Count);
            Check("3 risky: Run crossings dropped to the belts", Share(Of(shelf, Policy.Run), c => c.Drops > 0), 40, inf, " %", Of(shelf, Policy.Run).Count);
            int launches = risky.Sum(c => c.Launches), landed = risky.Sum(c => c.ShelfLandings);
            Check("3 risky: launches landing on the shelf", launches > 0 ? 100f * landed / launches : float.NaN, 98, 100, " %", launches);
            Check("3 risky: crossings with an abyss fall", Share(risky, c => c.AbyssFalls > 0), -inf, 2, " %", risky.Count);
            // The drawers knock down since the R725 tuning (spec 5.3 has no line for it): how many crossings on the shelf a
            // drawer floored, per policy, and which drawer.
            if (shelf.Count > 0)
            {
                var floored = new[] { Policy.Run, Policy.Sprint, Policy.Jumper }.Select(policy =>
                    policy + " " + Pct(Of(shelf, policy).Count(c => Downs(c, "drawer") > 0), Of(shelf, policy).Count) + " (n " + Of(shelf, policy).Count + ")");
                var byDrawer = Enumerable.Range(0, R.DrawerZ.Length).Select(i => DrawerKind(i) + " " + shelf.Sum(c => KindDowns(c, DrawerKind(i))));
                Log("  [info] 3 risky: crossings on the shelf knocked down by a drawer: " + string.Join(", ", floored)
                    + "; knockdowns " + string.Join(", ", byDrawer));
                int carried = all.Sum(c => Downs(c, "drawer"));
                if (carried > 0)
                    Log("  [info] 3 risky: bodies a drawer's knockdown carried until up (" + carried + " knockdowns; judged stuck, pinned and flung as everywhere): "
                        + "fastest " + S(carryAcross) + " m/s across and " + S(carryUp) + " m/s up, longest inside something solid "
                        + S(carryInside, "0.00") + " s" + (carryInsideOf != null ? " (" + carryInsideOf + ")" : ""));
            }

            Log("  fork balance:");
            foreach (var policy in new[] { Policy.Sprint, Policy.Jumper })
            {
                var byTowers = Of(all, policy).Where(c => c.Branch1 == "risky" && !float.IsNaN(c.Fork1)).Select(c => c.Fork1).ToList();
                var byCorridor = Of(all, policy).Where(c => c.Branch1 == "safe" && !float.IsNaN(c.Fork1)).Select(c => c.Fork1).ToList();
                Check("fork 1, " + policy + ": corridor median - towers median", Median(byCorridor) - Median(byTowers), 3, inf, " s", Math.Min(byTowers.Count, byCorridor.Count));
            }
            var fork1 = all.Where(c => c.Branch1.Length > 0).ToList();
            float towerFalls = Share(fork1.Where(c => c.Branch1 == "risky").ToList(), c => At(c, BeatTowers).Falls > 0);
            float corridorFalls = Share(fork1.Where(c => c.Branch1 == "safe").ToList(), c => At(c, BeatCorridor).Falls > 0);
            Check("fork 1: towers' fall share - corridor's", towerFalls - corridorFalls, .01f, inf, " points", fork1.Count);
            foreach (var policy in new[] { Policy.Jumper, Policy.Sprint, Policy.Run })
            {
                var byPad = Of(all, policy).Where(c => c.Branch2 == "risky" && !float.IsNaN(c.Fork2)).Select(c => c.Fork2).ToList();
                var byBelts = Of(all, policy).Where(c => c.Branch2 == "safe" && !float.IsNaN(c.Fork2)).Select(c => c.Fork2).ToList();
                float gain = Median(byBelts) - Median(byPad);
                int n = Math.Min(byPad.Count, byBelts.Count);
                if (policy == Policy.Jumper) Check("fork 2, Jumper: safe median - risky median", gain, 0, inf, " s", n);
                else if (policy == Policy.Sprint) Check("fork 2, Sprint: safe median - risky median", gain, -.5f, inf, " s", n);
                else Log("  [info] fork 2, Run (blind at the drawers): safe median - risky median " + S(gain) + " s (n " + n + ")");
            }
            Check("fork 2: drops to the belts on the risky branch", risky.Sum(c => c.Drops), 1, inf, "", risky.Count);

            var plates = Visited(all, BeatPlates);
            Check("4 plates: Jumper crossings with a fall", Share(Of(plates, Policy.Jumper), c => At(c, BeatPlates).Falls > 0), -inf, 25, " %", Of(plates, Policy.Jumper).Count);
            Check("4 plates: Sprint crossings with a fall", Share(Of(plates, Policy.Sprint), c => At(c, BeatPlates).Falls > 0), -inf, 40, " %", Of(plates, Policy.Sprint).Count);
            Check("4 plates: Run crossings with a fall", Share(Of(plates, Policy.Run), c => At(c, BeatPlates).Falls > 0), 40, inf, " %", Of(plates, Policy.Run).Count);
            Check("4 plates: beat time (median)", Median(plates.Select(c => Duration(At(c, BeatPlates)))), 7, 14, " s", plates.Count);
            var plateWaiting = plates.Where(c => c.Policy != Policy.Run).ToList();
            Check("4 plates: plate wait (mean, Sprint and Jumper)", Mean(plateWaiting.Select(c => c.PlateWait)), -inf, 3, " s", plateWaiting.Count);

            var door = Visited(all, BeatDoor);
            var openings = doorPasses.GroupBy(d => d.Run * 1000000L + d.Opening).Select(g => g.Count()).ToList();
            Check("6 door: wait (mean)", Mean(door.Select(c => c.DoorWait)), 1.5f, 4.5f, " s", door.Count);
            Check("6 door: bots per opening (mean)", openings.Count > 0 ? (float)openings.Average() : float.NaN, 2, inf, "", openings.Count);
            Check("6 door: door pinches", all.Sum(c => c.DoorPinches), 0, 0, "", door.Count);

            if (mix)
            {
                var heavy = all.Where(c => c.Piece == PieceKind.Queen || c.Piece == PieceKind.Rook).ToList();
                Check("mix: queens and rooks stuck or pinned", heavy.Sum(c => c.Stuck + c.Pinned), 0, 0, "", heavy.Count);
                var pawns = all.Where(c => c.Piece == PieceKind.Pawn).ToList();
                float pawnDowns = Share(pawns, c => c.Downs > 0), pawnFalls = Share(pawns, c => c.Falls > 0);
                foreach (var kind in new[] { PieceKind.Queen, PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop })
                {
                    var set = all.Where(c => c.Piece == kind).ToList();
                    Check("mix: " + kind + " knocked-down share - the pawns'", Share(set, c => c.Downs > 0) - pawnDowns, -inf, 10, " points", set.Count);
                    Check("mix: " + kind + " fall share - the pawns'", Share(set, c => c.Falls > 0) - pawnFalls, -inf, 10, " points", set.Count);
                }
                var backs = hammerBacks.Select(b => b.Value).ToList();
                Check("mix: hammer knockdowns that pushed the piece back under 0.5 m", backs.Count(b => b < .5f), 0, 0, "", backs.Count);
                foreach (var kind in hammerBacks.GroupBy(b => b.Key))
                    Log("  [info] " + kind.Key + " pushed back by hammer knockdowns: min " + S(kind.Min(b => b.Value), "0.00") + " m, median " + S(Median(kind.Select(b => b.Value)), "0.00") + " m");
                Check("mix: crossings knocked down by both hammers", all.Count(c => c.BothHammers), 0, 0, "", all.Count);
            }

            if (probe == Probe.Walk)
            {
                Log("  M0b walk (the devices are present, so the times include them):");
                Check("walk: stuck + flung + pinned", all.Sum(c => c.Stuck + c.Flung + c.Pinned), 0, 0, "", all.Count);
                var jumpers = Of(towers, Policy.Jumper);
                Check("walk: Jumper tower crossings through all 8 jumps without a fall", Share(jumpers, c => At(c, BeatTowers).Falls == 0 && !float.IsNaN(c.Fork1)), 65, 100, " %", jumpers.Count);
                for (int r = 0; r < leaders.Count; r++) Check("walk: run " + (r + 1) + " leader (60-75 s without hazards)", leaders[r], 60, 75, " s", float.IsNaN(leaders[r]) ? 0 : 1);
            }
        }

        // The Core fork-2 model (Core test 10's sampling: 200 start times 0.37 s apart) beside what the bots measured.
        static void ForkModel()
        {
            Log("Fork 2 model (RoyalVaultRules.ForkTwoSafeSeconds / ForkTwoRiskySeconds / ForkTwoDropCost, mean of 200 starts 0.37 s apart):");
            double c0 = ObstacleClock.Now;
            foreach (double v in new[] { R.RunSpeed, ForkSpeed, R.SprintSpeed })
            {
                double safe = 0, risky = 0, drop = 0;
                for (int k = 0; k < 200; k++)
                {
                    double t0 = c0 + .37 * k;
                    safe += R.ForkTwoSafeSeconds(t0, v);
                    risky += R.ForkTwoRiskySeconds(t0, v);
                    drop += R.ForkTwoDropCost(t0, v);
                }
                Log("  " + D(v, "0.0") + " m/s: safe " + D(safe / 200, "0.0") + " s, risky " + D(risky / 200, "0.0") + " s, gain " + D((safe - risky) / 200, "0.0")
                    + " s, a knockdown off at drawer 1 costs " + D(drop / 200, "0.0") + " s");
            }
            Log("  measured fork 2 (walkway end to J2), median seconds:");
            foreach (var policy in new[] { Policy.Run, Policy.Sprint, Policy.Jumper })
            {
                var set = Of(crossings, policy).Where(c => !float.IsNaN(c.Fork2)).ToList();
                if (set.Count == 0) continue;
                var pad = set.Where(c => c.Branch2 == "risky" && c.Drops == 0).Select(c => c.Fork2).ToList();
                var dropped = set.Where(c => c.Branch2 == "risky" && c.Drops > 0).Select(c => c.Fork2).ToList();
                var belts = set.Where(c => c.Branch2 == "safe").Select(c => c.Fork2).ToList();
                Log("  " + policy.ToString().PadRight(7) + " belts " + S(Median(belts)) + " (n " + belts.Count + "), pad kept on the shelf " + S(Median(pad)) + " (n " + pad.Count
                    + "), pad dropped to the belts " + S(Median(dropped)) + " (n " + dropped.Count + "), gain " + S(Median(belts) - Median(pad))
                    + ", drop cost " + S(Median(dropped) - Median(pad)));
            }
        }

        static void Check(string what, float value, float lo, float hi, string unit, int n)
        {
            if (n <= 0 || float.IsNaN(value)) { Log("  [n/a] " + what); return; }
            bool ok = value >= lo - 1e-4f && value <= hi + 1e-4f;
            Log("  [" + (ok ? "in" : "OUT") + "] " + what + ": " + S(value, "0.##") + unit + " (want " + Want(lo, hi, unit) + "; n " + n + ")");
        }

        static string Want(float lo, float hi, string unit)
        {
            if (lo == hi) return "= " + S(lo, "0.##") + unit;
            if (float.IsNegativeInfinity(lo)) return "<= " + S(hi, "0.##") + unit;
            if (float.IsPositiveInfinity(hi)) return ">= " + S(lo, "0.##") + unit;
            return S(lo, "0.##") + "-" + S(hi, "0.##") + unit;
        }

        static List<Crossing> Of(List<Crossing> set, Policy policy) => set.Where(c => c.Policy == policy).ToList();
        static List<Crossing> Visited(List<Crossing> set, string beat) => set.Where(c => c.Beats.ContainsKey(beat)).ToList();
        static readonly BeatVisit NoVisit = new BeatVisit();
        static BeatVisit At(Crossing c, string beat) => c.Beats.TryGetValue(beat, out var sum) ? sum : NoVisit;
        static int Downs(Crossing c, string device) => c.DownsBy.TryGetValue(device, out int n) ? n : 0;
        static float Duration(BeatVisit v) => float.IsNaN(v.Out) ? float.NaN : v.Out - v.In;
        static float Share(List<Crossing> set, Func<Crossing, bool> counted) => set.Count == 0 ? float.NaN : 100f * set.Count(counted) / set.Count;

        static float Median(IEnumerable<float> values)
        {
            var sorted = values.Where(v => !float.IsNaN(v)).OrderBy(v => v).ToList();
            if (sorted.Count == 0) return float.NaN;
            return sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) * .5f;
        }

        static float Mean(IEnumerable<float> values)
        {
            var list = values.Where(v => !float.IsNaN(v)).ToList();
            return list.Count == 0 ? float.NaN : list.Average();
        }

        static string Pct(int part, int whole) => whole > 0 ? (100f * part / whole).ToString("0", CultureInfo.InvariantCulture) + "%" : "-";
        static string F(float v) => float.IsNaN(v) || float.IsInfinity(v) ? "" : v.ToString("0.###", CultureInfo.InvariantCulture);
        static string S(float v, string format = "0.0") => float.IsNaN(v) ? "-" : v.ToString(format, CultureInfo.InvariantCulture);
        static string D(double v, string format) => double.IsNaN(v) ? "-" : v.ToString(format, CultureInfo.InvariantCulture);
        static string V(Vector3 v) => "(" + S(v.x, "0.00") + ", " + S(v.y, "0.00") + ", " + S(v.z, "0.00") + ")";

        static void Log(string value)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path.Combine(Folder, Report), value + "\n");
            if (value.Length > 0) Debug.Log("[RoyalVaultBots] " + value);
        }

        static void StopPlay(int code)
        {
            stopping = true;
            Application.logMessageReceived -= OnLog;
            YardKit.HitGiven -= OnHit;
            KillVolume.Entered -= OnFall;
            Checkpoint.Reached -= OnCheckpoint;
            foreach (var pad in pads)
                if (pad != null) pad.Thrown -= OnThrown;
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ExitKey, true);
            SessionState.SetInt(CodeKey, code);
            EditorApplication.isPlaying = false;
        }

        static void CompleteExit()
        {
            if (!SessionState.GetBool(ExitKey, false)) return;
            SessionState.SetBool(ExitKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
        }
    }
}
