using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // J, knight pads (mini-games v1.0 §8): 25 islands in water, 5 x 5, 3 m apart, each with a pad in its middle.
    // Standing on a pad, a pawn aims at one of the knight's eight moves (its move input, or where it faces);
    // Space winds the pad up (0.9 s, 0.2 for a knight) instead of a jump and throws it a knight's move away. A
    // landing on a flag island takes the flag (the other team's landing knocks it down for 4 s); off the board
    // is the water, and the water puts it back on the south floor (MgWaterReturn). Sixteen flags: a bridge rises
    // to the north landing and the gate opens.
    //
    // Physics and the network (§1.2, §8.6): the throw is the lab's launch (ILaunchable.Launch, the arc of the
    // jump pads), given on the host; the flight is the ragdoll's own and every PC sees it in the snapshots. The
    // host judges the pads, the landings and the bumps and sends the flags, the flags down, every pawn's aim and
    // wind-up on a pad, the last launch and the last bump (28 bytes at most). Each PC draws the arrows, the pads
    // and the trails from those and the pawns it draws; the dotted aiming arc only for its own pawn.
    public sealed class MgKnightPads : MiniGameBase, IProgressCount
    {
        public const float IslandTop = .3f, IslandSize = 2f, PadTop = .42f, Surface = -.25f, Bottom = -1.6f;
        public const float WaterU0 = 2.6f, WaterU1 = 19.4f, MidAir = .8f, Crowd = 1f, CrowdPush = 2.4f, Bumped = .9f, LandReach = 1.2f;
        const int MaxAims = 6;

        [SerializeField] MissionStation station;

        static readonly List<MgKnightPads> live = new List<MgKnightPads>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => live.Clear();

        sealed class Aim
        {
            public ICharacterDriver Driver;
            public int Island, Dir, Team;
            public bool Charging;
            public float ChargeStart, ChargeEnd;
            public float Wound;   // 0..1 of the wind-up (a client's from the wire)
        }

        sealed class Flight
        {
            public ICharacterDriver Driver;
            public int ToCol, ToRow, Team;
            public bool Own, OnGrid;
            public float Start, Seconds;
        }

        // Every PC: a trail behind a launched pawn, its landing judged by eye for the moment.
        sealed class Trail
        {
            public ICharacterDriver Driver;
            public LineRenderer Line;
            public readonly List<Vector3> Points = new List<Vector3>();
            public float Start, Seconds, Ended = -1f;
            public Color Color;
        }

        readonly List<Aim> aims = new List<Aim>();
        readonly List<Flight> flights = new List<Flight>();
        readonly List<Trail> trails = new List<Trail>();
        readonly HashSet<Flight> bumped = new HashSet<Flight>();
        readonly List<Aim> keep = new List<Aim>();
        readonly Dictionary<ICharacterDriver, float> steadyAt = new Dictionary<ICharacterDriver, float>();
        int collected, down;
        readonly float[] downUntil = new float[KnightPads.Flags.Length];
        int launches, launchCode, launchSeat = NoSeat, bonks, bonkCode;
        ICharacterDriver launchDriver;

        // The look.
        Transform[] pads;
        LineRenderer[] padRings;
        Transform[][] arrows;
        Renderer[][] arrowLooks;
        Transform[] flags;
        Renderer[] flagCloths;
        float[] flagSpinAt, flagFallAt, flagRiseAt;
        Rigidbody bridge;
        Transform[] dots;
        Material arrowOn, arrowOff, arrowNone, flagGold;
        int shownCollected, shownDown, shownLaunches, shownBonks, localDir = -1;
        float bridgeAt = -1f;
        readonly float[] bounceAt = new float[25];
        readonly float[] chargeShown = new float[25];

        public int Done => Count(collected);
        public int Total => KnightPads.Flags.Length;

        static int Count(int bits) { int n = 0; for (; bits != 0; bits &= bits - 1) n++; return n; }

        public static MgKnightPads Build(CourseBuilder b, MissionStation station)
        {
            var game = b.parent.gameObject.AddComponent<MgKnightPads>();
            game.station = station;
            game.Make(b);
            return game;
        }

        float M => MiniGameParts.Mirror(station);
        Vector3 IslandLocal(int col, int row, float y) => MiniGameParts.Local(station, KnightPads.Island(col, row), y);
        Vector3 World(Vector3 local) => transform.TransformPoint(local);
        int Opponent => station.Team == Teams.White ? Teams.Black : Teams.White;

        static int FlagOf(int col, int row)
        {
            for (int i = 0; i < KnightPads.Flags.Length; i++)
            {
                KnightPads.Parse(KnightPads.Flags[i], out int c, out int r);
                if (c == col && r == row) return i;
            }
            return -1;
        }

        void Make(CourseBuilder b)
        {
            var k = b.kit;
            // The south floor and the north landing are dais; between them the water, its walls a hollow frame.
            MissionGames.Floor(b, 0f, WaterU0);
            MissionGames.Floor(b, WaterU1, 20f);
            var stone = Arena.DaisMaterial;
            b.Box("Frame W", new Vector3(-10f, Bottom, WaterU0), new Vector3(-9.4f, 0f, WaterU1), stone, true).AddComponent<CourseFloor>();
            b.Box("Frame E", new Vector3(9.4f, Bottom, WaterU0), new Vector3(10f, 0f, WaterU1), stone, true).AddComponent<CourseFloor>();
            b.Box("Water bottom", new Vector3(-10f, Bottom - 1f, WaterU0), new Vector3(10f, Bottom, WaterU1), stone, true);
            b.Box("Water wall S", new Vector3(-9.4f, Bottom, WaterU0 - .01f), new Vector3(9.4f, 0f, WaterU0), stone, true, false);
            b.Box("Water wall N", new Vector3(-9.4f, Bottom, WaterU1), new Vector3(9.4f, 0f, WaterU1 + .01f), stone, true, false);
            Arena.Picture(b.parent, "Water", new Vector3(-9.4f, Surface - .05f, WaterU0), new Vector3(9.4f, Surface, WaterU1), Arena.Glow(new Color(.35f, .62f, .78f, .55f)));
            MgWaterReturn.Make(transform, station, Surface, Bottom, WaterU0, WaterU1);

            // The pier from the south floor to c1, a step of 0.15 m each way.
            var c1 = IslandLocal(KnightPads.StartColumn, KnightPads.StartRow, 0f);
            b.Box("Pier", new Vector3(c1.x - .6f, Bottom, WaterU0), new Vector3(c1.x + .6f, .15f, c1.z - IslandSize * .5f), Arena.Wood(false), true).AddComponent<CourseFloor>();

            var padMat = Arena.Lit("M_KnightPad", Arena.Hex("#D9CDB4"), 0f, .6f);
            arrowOff = Arena.Lit("M_PadArrow", Arena.Hex("#8F8676"), 0f, .6f);
            arrowNone = Arena.Glow(new Color(.56f, .53f, .46f, .3f));
            arrowOn = Arena.Emissive(Arena.TeamColor(station.Team), 1.2f);
            var deco = Arena.Lit("M_DecoKnight", Arena.Hex("#BCB2A0"), 0f, .6f);
            flagGold = Arena.Emissive(Arena.Gold, .5f);
            int n = KnightPads.Columns * KnightPads.Rows;
            pads = new Transform[n];
            padRings = new LineRenderer[n];
            arrows = new Transform[n][];
            arrowLooks = new Renderer[n][];
            for (int row = 0; row < KnightPads.Rows; row++)
                for (int col = 0; col < KnightPads.Columns; col++)
                {
                    int i = KnightPads.Index(col, row);
                    var c = IslandLocal(col, row, 0f);
                    float h = IslandSize * .5f;
                    b.Box("Island " + (char)('a' + col) + (row + 1), c + new Vector3(-h, Bottom, -h), c + new Vector3(h, IslandTop, h), Arena.Wood(((col + row) & 1) != 0), true).AddComponent<CourseFloor>();
                    var pad = new GameObject("Pad").transform;
                    pad.SetParent(b.parent, false);
                    pad.localPosition = c + Vector3.up * IslandTop;
                    MiniGameParts.Part(pad, PrimitiveType.Cylinder, new Vector3(0f, .06f, 0f), new Vector3(1.1f, .06f, 1.1f), padMat);
                    pads[i] = pad;
                    padRings[i] = MiniGameParts.Loop(b.parent, "Pad ring", c + Vector3.up * (PadTop + .01f), .9f, .06f, Arena.TeamColor(station.Team));
                    padRings[i].enabled = false;
                    arrows[i] = new Transform[8];
                    arrowLooks[i] = new Renderer[8];
                    for (int d = 0; d < 8; d++)
                    {
                        var dir = new Vector3(KnightPads.DCol[d] * M, 0f, KnightPads.DRow[d]).normalized;
                        var a = MiniGameParts.Part(b.parent, PrimitiveType.Cube, c + dir * .72f + Vector3.up * (PadTop + .05f), new Vector3(.12f, .06f, .22f), arrowOff);
                        a.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, 45f);
                        bool there = KnightPads.Destination(col, row, d, out _, out _);
                        a.GetComponent<Renderer>().sharedMaterial = there ? arrowOff : arrowNone;
                        arrows[i][d] = a.transform;
                        arrowLooks[i][d] = a.GetComponent<Renderer>();
                    }
                    MiniGameParts.Piece(b.parent, "Deco knight", PieceKind.Knight, .32f, deco, new Color(0f, 0f, 0f, 0f), false).transform.localPosition = c + new Vector3(.7f * M, IslandTop, .7f);
                }

            // The flags in their islands' south-west corners.
            int f = KnightPads.Flags.Length;
            flags = new Transform[f];
            flagCloths = new Renderer[f];
            flagSpinAt = new float[f];
            flagFallAt = new float[f];
            flagRiseAt = new float[f];
            for (int i = 0; i < f; i++)
            {
                KnightPads.Parse(KnightPads.Flags[i], out int col, out int row);
                var root = new GameObject("Flag " + KnightPads.Flags[i]).transform;
                root.SetParent(b.parent, false);
                root.localPosition = IslandLocal(col, row, IslandTop) + new Vector3(-.7f * M, 0f, -.7f);
                MiniGameParts.Part(root, PrimitiveType.Cylinder, new Vector3(0f, .8f, 0f), new Vector3(.05f, .8f, .05f), Arena.Wood(false));
                flagCloths[i] = MiniGameParts.Part(root, PrimitiveType.Cube, new Vector3(.36f * M, 1.37f, 0f), new Vector3(.7f, .45f, .03f), flagGold).GetComponent<Renderer>();
                flags[i] = root;
                flagSpinAt[i] = flagFallAt[i] = flagRiseAt[i] = -10f;
            }

            // The bridge from c5 to the north landing, under the water until the end.
            var c5 = IslandLocal(2, 4, 0f);
            var bridgeGo = new GameObject("North bridge");
            bridgeGo.transform.SetParent(b.parent, false);
            bridgeGo.transform.localPosition = new Vector3(c5.x, -1.4f, 0f);
            bridge = bridgeGo.AddComponent<Rigidbody>();
            bridge.isKinematic = true;
            bridge.useGravity = false;
            bridge.interpolation = RigidbodyInterpolation.Interpolate;
            var before = b.parent;
            b.parent = bridgeGo.transform;
            b.Box("Deck", new Vector3(-1f, -.25f, c5.z + IslandSize * .5f), new Vector3(1f, .15f, WaterU1 + .05f), Arena.Wood(false), true).AddComponent<CourseFloor>();
            b.parent = before;

            // The dotted aiming arc (own pawn only).
            dots = new Transform[12];
            var team = Arena.TeamColor(station.Team);
            var dot = Arena.Glow(new Color(team.r, team.g, team.b, .6f));
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = MiniGameParts.Part(b.parent, PrimitiveType.Sphere, Vector3.zero, Vector3.one * .1f, dot).transform;
                dots[i].gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            if (!live.Contains(this)) live.Add(this);
            MiniGameHooks.JumpIntercept = Intercept;
        }

        void OnDisable()
        {
            live.Remove(this);
            if (live.Count == 0 && MiniGameHooks.JumpIntercept == (System.Func<ICharacterDriver, bool>)Intercept) MiniGameHooks.JumpIntercept = null;
        }

        // Space on a pad winds it up instead of a jump (any station's J game that has the pawn on a pad).
        static bool Intercept(ICharacterDriver driver)
        {
            foreach (var game in live)
                if (game != null && game.TryWind(driver)) return true;
            return false;
        }

        bool TryWind(ICharacterDriver driver)
        {
            if (Remote || Completed) return false;
            foreach (var a in aims)
            {
                if (a.Driver != driver || a.Charging) continue;
                float seconds = MiniGameParts.PieceOf(driver) == PieceKind.Knight ? KnightPads.KnightCharge : KnightPads.Charge;
                a.Charging = true;
                a.ChargeStart = Time.time;
                a.ChargeEnd = Time.time + seconds;
                if (driver is IMiniGamePawn mg) mg.HoldStill(seconds + .05f);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ host

        void FixedUpdate()
        {
            if (station == null || pads == null) return;
            if (!Remote && !Completed) Step();
            if (Completed && bridge != null)
            {
                if (bridgeAt < 0f) bridgeAt = Time.time;
                float k = Mathf.Clamp01((Time.time - bridgeAt) / .8f);
                var p = IslandLocal(2, 4, 0f);
                bridge.MovePosition(World(new Vector3(p.x, Mathf.Lerp(-1.4f, 0f, 1f - (1f - k) * (1f - k)), 0f)));
            }
        }

        void Step()
        {
            float t = Time.time;
            var pawns = station.Pawns();
            // Who stands on which pad: the hips within 0.55 m of its middle, standing, steady again after a landing.
            keep.Clear();
            foreach (var p in pawns)
            {
                var mg = p.Driver as IMiniGamePawn;
                if (mg == null || mg.Down || Flying(p.Driver)) continue;
                if (steadyAt.TryGetValue(p.Driver, out float steady) && t < steady) continue;
                var at = MiniGameParts.Board(station, p.Local);
                int island = IslandUnder(at, out int col, out int row);
                if (island < 0 || p.Local.y < IslandTop - .3f || p.Local.y > IslandTop + 2f) continue;
                if (KnightPads.Island(col, row).DistanceTo(at) > KnightPads.PadRadius) continue;
                var aim = Find(p.Driver);
                if (aim == null || aim.Island != island) aim = new Aim { Driver = p.Driver, Island = island };
                aim.Team = p.Team;
                if (!aim.Charging) aim.Dir = AimOf(p, mg);
                keep.Add(aim);
            }
            // A wind-up that lost its pawn (knocked off) is gone; one that is done throws.
            aims.Clear();
            foreach (var a in keep)
            {
                if (a.Charging && t >= a.ChargeEnd) { Throw(a); continue; }
                a.Wound = a.Charging ? Mathf.Clamp01((t - a.ChargeStart) / Mathf.Max(.01f, a.ChargeEnd - a.ChargeStart)) : 0f;
                if (aims.Count < MaxAims) aims.Add(a);
            }
            Fly(t, pawns);
            for (int i = 0; i < downUntil.Length; i++)
                if ((down >> i & 1) != 0 && t >= downUntil[i]) down &= ~(1 << i);
            Progress01 = Done / (float)Total;
            if (Done >= Total) Complete();
        }

        Aim Find(ICharacterDriver d)
        {
            foreach (var a in aims) if (a.Driver == d) return a;
            return null;
        }

        bool Flying(ICharacterDriver d)
        {
            foreach (var f in flights) if (f.Driver == d) return true;
            return false;
        }

        static int IslandUnder(StationPoint at, out int col, out int row)
        {
            col = Mathf.RoundToInt((at.V - KnightPads.FirstV) / KnightPads.Spacing);
            row = Mathf.RoundToInt((at.U - KnightPads.FirstU) / KnightPads.Spacing);
            return KnightPads.OnGrid(col, row) ? KnightPads.Index(col, row) : -1;
        }

        // The knight move nearest the pawn's move input (white's view), or where it faces without one.
        int AimOf(MissionStation.Pawn p, IMiniGamePawn mg)
        {
            var dir = transform.InverseTransformDirection(mg.MoveInput);
            if (new Vector2(dir.x, dir.z).sqrMagnitude < .01f && p.Driver.FollowTarget != null) dir = transform.InverseTransformDirection(p.Driver.FollowTarget.forward);
            return KnightPads.Nearest(dir.x * M, dir.z);
        }

        Vector3 Landing(int col, int row) => World(IslandLocal(col, row, PadTop));

        void Throw(Aim a)
        {
            int col = a.Island % KnightPads.Columns, row = a.Island / KnightPads.Columns;
            bool onGrid = KnightPads.Destination(col, row, a.Dir, out int toCol, out int toRow);
            if (!(a.Driver is ILaunchable launchable)) return;
            var from = launchable.LaunchFrom;
            var to = Landing(toCol, toRow);
            float g = -Physics.gravity.y;
            launchable.Launch(LaunchPad.ArcVelocity(from, to, KnightPads.Apex, g));
            flights.Add(new Flight
            {
                Driver = a.Driver, ToCol = toCol, ToRow = toRow, OnGrid = onGrid, Team = a.Team, Own = a.Team == station.Team,
                Start = Time.time, Seconds = LaunchPad.ArcSeconds(to.y - from.y, KnightPads.Apex, g),
            });
            launches = (launches + 1) & 255;
            launchCode = a.Island | a.Dir << 5;
            launchSeat = Seat(a.Driver);
            launchDriver = a.Driver;
        }

        // §8.2.7-10: bumps in the air, landings, crowded pads, flags.
        void Fly(float t, List<MissionStation.Pawn> pawns)
        {
            flights.RemoveAll(f => f.Driver.FollowTarget == null || t - f.Start >= f.Seconds + .5f);
            // Two in the air within 0.8 m: both fall, and neither flight counts any more.
            bumped.Clear();
            for (int i = 0; i < flights.Count; i++)
                for (int j = i + 1; j < flights.Count; j++)
                {
                    var a = flights[i];
                    var b = flights[j];
                    if (bumped.Contains(a) || bumped.Contains(b) || t - a.Start >= a.Seconds || t - b.Start >= b.Seconds) continue;
                    var pa = a.Driver.FollowTarget.position;
                    var pb = b.Driver.FollowTarget.position;
                    if (Vector3.Distance(pa, pb) > MidAir) continue;
                    Knock(a.Driver, Vector3.zero, 1.2f);
                    Knock(b.Driver, Vector3.zero, 1.2f);
                    Bonk((pa + pb) * .5f);
                    bumped.Add(a);
                    bumped.Add(b);
                }
            flights.RemoveAll(f => bumped.Contains(f));

            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                if (t - f.Start < f.Seconds - .05f) continue;
                flights.RemoveAt(i);
                if (!f.OnGrid) continue;
                var pad = Landing(f.ToCol, f.ToRow);
                var hips = f.Driver.FollowTarget.position;
                if (MiniGameParts.Flat(hips, pad) > LandReach) continue;
                // Landed. Anyone standing on that pad is thrown off; the lander wobbles longer.
                bool crowded = false;
                foreach (var p in pawns)
                {
                    if (p.Driver == f.Driver || p.Driver.FollowTarget == null) continue;
                    var at = p.Driver.FollowTarget.position;
                    if (MiniGameParts.Flat(at, pad) > Crowd || at.y < pad.y - .5f) continue;
                    var away = at - pad;
                    away.y = 0f;
                    if (away.sqrMagnitude < 1e-4f) away = transform.forward;
                    Knock(p.Driver, away.normalized * CrowdPush, 1f);
                    crowded = true;
                }
                float wobble = crowded ? Bumped : KnightPads.Wobble;
                steadyAt[f.Driver] = t + wobble;
                if (f.Driver is IMiniGamePawn mg) mg.HoldStill(wobble);
                if (crowded) Bonk(pad);
                int flag = FlagOf(f.ToCol, f.ToRow);
                if (flag < 0 || (collected >> flag & 1) != 0) continue;
                if (f.Own && !crowded && (down >> flag & 1) == 0) collected |= 1 << flag;
                else if (!f.Own)
                {
                    down |= 1 << flag;
                    downUntil[flag] = t + KnightPads.FlagDownSeconds;
                }
            }
        }

        static void Knock(ICharacterDriver d, Vector3 push, float seconds)
        {
            if (d is IHitReceiver hit) hit.ApplyHit(push, seconds, 0f, true);
        }

        void Bonk(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            int v = Mathf.Clamp(Mathf.FloorToInt((local.x + 10f) / 1.25f), 0, 15), u = Mathf.Clamp(Mathf.FloorToInt(local.z / 1.25f), 0, 15);
            bonks = (bonks + 1) & 255;
            bonkCode = v | u << 4;
        }

        // ------------------------------------------------------------------ moments (every PC)

        // StateDrivenMover: pads, arrows, flags and the bridge follow the host's pads, aims and flags; flights are the
        // ragdolls' own.
        void Update()
        {
            if (pads == null) return;
            float t = Time.time;
            int n = pads.Length;
            for (int i = 0; i < n; i++) chargeShown[i] = 0f;
            for (int i = 0; i < n; i++)
            {
                for (int d = 0; d < 8; d++)
                {
                    arrows[i][d].localScale = new Vector3(.12f, .06f, .22f);
                    int col = i % KnightPads.Columns, row = i / KnightPads.Columns;
                    var want = KnightPads.Destination(col, row, d, out _, out _) ? arrowOff : arrowNone;
                    if (arrowLooks[i][d].sharedMaterial != want) arrowLooks[i][d].sharedMaterial = want;
                }
                padRings[i].enabled = false;
            }
            int mine = -1, mineIsland = -1;
            foreach (var a in aims)
            {
                if (a.Island < 0 || a.Island >= n) continue;
                arrows[a.Island][a.Dir].localScale = new Vector3(.12f, .06f, .22f) * 1.15f;
                var lit = a.Team == station.Team ? arrowOn : Arena.Emissive(Arena.TeamColor(a.Team), 1.2f);
                if (arrowLooks[a.Island][a.Dir].sharedMaterial != lit) arrowLooks[a.Island][a.Dir].sharedMaterial = lit;
                if (a.Charging)
                {
                    chargeShown[a.Island] = a.Wound;
                    var ring = padRings[a.Island];
                    if (!ring.enabled) ArenaFx.Sfx("sfx_pad_charge", World(pads[a.Island].localPosition), volume: .7f);
                    ring.enabled = true;
                    ring.transform.localScale = Vector3.one * Mathf.Lerp(1f, .65f, a.Wound);
                }
                if (a.Driver != null && a.Driver == LocalDriver) { mine = a.Dir; mineIsland = a.Island; }
            }
            // The pads: pressed to 40 % while winding up, a 1.8x bounce for 0.15 s on the throw.
            for (int i = 0; i < n; i++)
            {
                float bounce = (t - bounceAt[i]) / .15f;
                float y = bounce >= 0f && bounce < 1f ? 1f + .8f * Mathf.Sin(bounce * Mathf.PI) : Mathf.Lerp(1f, .4f, chargeShown[i]);
                pads[i].localScale = new Vector3(1f, y, 1f);
            }
            if (mine != localDir && mine >= 0 && localDir >= 0) ArenaFx.Sfx("sfx_pad_tick", World(pads[mineIsland].localPosition), volume: .5f);
            localDir = mine;
            Dots(mineIsland, mine);
            Moments(t);
            Trails(t);
        }

        // The dotted arc to where the throw would land: on this PC only, for its own pawn.
        void Dots(int island, int dir)
        {
            bool show = island >= 0;
            for (int i = 0; i < dots.Length; i++) if (dots[i].gameObject.activeSelf != show) dots[i].gameObject.SetActive(show);
            if (!show) return;
            int col = island % KnightPads.Columns, row = island / KnightPads.Columns;
            KnightPads.Destination(col, row, dir, out int tc, out int tr);
            var a = IslandLocal(col, row, PadTop + .2f);
            var b = IslandLocal(tc, tr, PadTop + .2f);
            for (int i = 0; i < dots.Length; i++)
            {
                float k = (i + 1f) / (dots.Length + 1f);
                var p = Vector3.Lerp(a, b, k);
                p.y += KnightPads.Apex * 4f * k * (1f - k);
                dots[i].localPosition = p;
            }
        }

        void Moments(float t)
        {
            // Flags taken: a spin into the team's colour, confetti, a bell a semitone higher each time.
            for (int i = 0; i < flags.Length; i++)
            {
                bool got = (collected >> i & 1) != 0, was = (shownCollected >> i & 1) != 0;
                bool isDown = (down >> i & 1) != 0, wasDown = (shownDown >> i & 1) != 0;
                var at = World(flags[i].localPosition + Vector3.up * 1.2f);
                if (got && !was)
                {
                    flagSpinAt[i] = t;
                    flagCloths[i].sharedMaterial = Arena.Emissive(Arena.TeamColor(station.Team), .7f);
                    ArenaFx.Burst(at, new[] { Arena.TeamColor(station.Team), Arena.Gold, Color.white }, 26, 2f, 3f, 1f, .08f);
                    ArenaFx.Sfx("sfx_flag_get", at, pitch: Mathf.Pow(2f, (Count(collected) - 16) / 12f));
                }
                if (!got && was) flagCloths[i].sharedMaterial = flagGold;
                if (isDown && !wasDown)
                {
                    flagFallAt[i] = t;
                    ArenaFx.Burst(at, new[] { Arena.TeamColor(Opponent) }, 6, 1.2f, 1.2f, .6f, .07f);
                    ArenaFx.Sfx("sfx_flag_fall", at);
                }
                if (!isDown && wasDown) flagRiseAt[i] = t;
                float spin = Mathf.Clamp01((t - flagSpinAt[i]) / .3f);
                float fall = isDown ? Mathf.Clamp01((t - flagFallAt[i]) / .3f) : 1f - Mathf.Clamp01((t - flagRiseAt[i]) / .3f);
                flags[i].localRotation = Quaternion.Euler(0f, spin < 1f ? 360f * spin : 0f, 90f * fall * -M);
            }
            shownCollected = collected;
            shownDown = down;
            // A throw: the pad bounces, dust, a ring, the boing; a trail follows the pawn.
            if (launches != shownLaunches)
            {
                shownLaunches = launches;
                int island = launchCode & 31;
                if (island < pads.Length)
                {
                    bounceAt[island] = t;
                    var at = World(pads[island].localPosition);
                    var who = Remote ? Driver((byte)launchSeat) : launchDriver;
                    int team = who is ITeamMember m ? m.Team : station.Team;
                    ArenaFx.Burst(at + Vector3.up * .1f, new[] { new Color(.8f, .72f, .6f) }, 8, 1.4f, 1f, .5f, .07f);
                    ArenaFx.Ring(at, 0f, 1.6f, .5f, Arena.TeamColor(team));
                    ArenaFx.Sfx("sfx_pad_launch", at);
                    if (who != null) StartTrail(who, team);
                }
            }
            if (bonks != shownBonks)
            {
                shownBonks = bonks;
                var at = World(new Vector3((bonkCode & 15) * 1.25f - 10f + .6f, 1f, (bonkCode >> 4) * 1.25f + .6f));
                ArenaFx.Burst(at, new[] { Color.white }, 8, 1.5f, 1.5f, .6f, .08f);
                ArenaFx.Sfx("sfx_midair_bonk", at);
            }
        }

        void StartTrail(ICharacterDriver who, int team)
        {
            var line = new GameObject("Flight trail").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = true;
            line.widthMultiplier = .15f;
            line.sharedMaterial = Arena.Glow(Color.white);
            var c = Arena.TeamColor(team);
            line.startColor = line.endColor = c;
            line.positionCount = 0;
            trails.Add(new Trail { Driver = who, Line = line, Start = Time.time, Seconds = LaunchPad.ArcSeconds(0f, KnightPads.Apex, -Physics.gravity.y), Color = c });
        }

        // Each trail follows its pawn through the flight, then stays 1.8 s, fading. Where it ends on a pad: the
        // thud and the ring of a landing.
        void Trails(float t)
        {
            for (int i = trails.Count - 1; i >= 0; i--)
            {
                var tr = trails[i];
                if (tr.Ended < 0f)
                {
                    if (tr.Driver?.FollowTarget != null) tr.Points.Add(tr.Driver.FollowTarget.position);
                    if (t - tr.Start >= tr.Seconds || tr.Driver?.FollowTarget == null)
                    {
                        tr.Ended = t;
                        if (tr.Points.Count > 0) Landed(tr.Points[tr.Points.Count - 1], tr.Color);
                    }
                    tr.Line.positionCount = tr.Points.Count;
                    tr.Line.SetPositions(tr.Points.ToArray());
                    continue;
                }
                float k = (t - tr.Ended) / 1.8f;
                if (k >= 1f)
                {
                    Destroy(tr.Line.gameObject);
                    trails.RemoveAt(i);
                    continue;
                }
                var c = tr.Color;
                c.a = 1f - k;
                tr.Line.startColor = tr.Line.endColor = c;
            }
        }

        void Landed(Vector3 hips, Color color)
        {
            var local = transform.InverseTransformPoint(hips);
            int island = IslandUnder(MiniGameParts.Board(station, local), out int col, out int row);
            if (island < 0) return;
            var pad = Landing(col, row);
            if (MiniGameParts.Flat(hips, pad) > LandReach) return;
            ArenaFx.Ring(pad, 0f, 1.3f, .45f, color);
            ArenaFx.Sfx("sfx_land_wood", pad);
        }

        // ------------------------------------------------------------------ the wire (§8.6)

        public override void WriteNet(ref NetBlock block)
        {
            block.U16(collected);
            block.U16(down);
            block.U8(aims.Count);
            foreach (var a in aims)
            {
                block.U8(a.Island | a.Dir << 5);
                block.U8((a.Charging ? 1 : 0) | (a.Team == Teams.Black ? 2 : 0) | Mathf.Clamp(Mathf.RoundToInt(a.Wound * 63f), 0, 63) << 2);
                block.U8(Seat(a.Driver));
            }
            block.U8(launches);
            block.U8(launchCode);
            block.U8(launchSeat);
            block.U8(bonks);
            block.U8(bonkCode);
        }

        public override void ReadNet(ref NetBlock block)
        {
            collected = block.ReadU16();
            down = block.ReadU16();
            int n = Mathf.Min(MaxAims, block.ReadU8());
            aims.Clear();
            for (int i = 0; i < n; i++)
            {
                int a = block.ReadU8(), b = block.ReadU8(), seat = block.ReadU8();
                aims.Add(new Aim
                {
                    Island = a & 31, Dir = a >> 5 & 7, Charging = (b & 1) != 0, Team = (b & 2) != 0 ? Teams.Black : Teams.White,
                    Wound = (b >> 2) / 63f, Driver = Driver((byte)seat),
                });
            }
            launches = block.ReadU8();
            launchCode = block.ReadU8();
            launchSeat = block.ReadU8();
            bonks = block.ReadU8();
            bonkCode = block.ReadU8();
        }

        public override void ResetGame()
        {
            base.ResetGame();
            collected = down = 0;
            aims.Clear();
            flights.Clear();
            steadyAt.Clear();
            bridgeAt = -1f;
            if (bridge != null)
            {
                var p = IslandLocal(2, 4, 0f);
                bridge.transform.localPosition = new Vector3(p.x, -1.4f, 0f);
            }
        }
    }
}
