using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // G, king escort (mini-games v1.0 §5): our king rides a sled along fifteen board squares (KingEscortPath, d1 to
    // e8) to the gate. The team pulls it by four front ropes (or pushes on the two rear handles), the other team
    // holds it back, sand slows it, it stops to turn on six turntables, and two of the other side's rooks stand in
    // the road until they are dragged into their garages. At the gate our rook tower jumps over the king (castling)
    // and the gate opens.
    //
    // Physics and the network (§1.2, §2.2): the sled is one distance s along the road, worked out on the host; the
    // sled, its handles and the rooks are kinematic bodies moved to the host's numbers, and a hand that grips a
    // handle is dragged with it (a front rope also keeps its pawn within 2.3 m of the sled). The host sends s, the
    // turntable's phase, both rooks' drag, flags and which handles are held (9 bytes); a client eases to them and
    // plays every moment from what changed.
    public sealed class MgKingEscort : MiniGameBase
    {
        public const float SledWidth = 1.5f, SledLength = 1.9f, RopeLength = 2f, RopeLeash = 2.3f, LeashPull = 8f, LeashMax = 3f;
        public const float CastleSeconds = 1.3f, CastleApex = 3.2f, PathCell = 2.22f;
        const int Front = 4, Rear = 2, RookHandle = 6, Handles = 8;
        static readonly float[] FrontX = { -.6f, -.2f, .2f, .6f };

        [SerializeField] MissionStation station;

        Rigidbody sled;
        Transform sledLook;
        readonly GrabHandle[] handles = new GrabHandle[Handles];
        readonly Rigidbody[] handleBodies = new Rigidbody[Handles];
        readonly LineRenderer[] ropes = new LineRenderer[Front];
        readonly Rigidbody[] rooks = new Rigidbody[2];
        readonly Renderer[] rookRings = new Renderer[2];
        readonly Transform[] stops = new Transform[2];
        Transform tower;
        Material ropeSlack, ropeTaut, ringPlain, ringWarn;

        // Host state (a client's copy from the wire).
        float s, turnT;
        int turnCell = -1;
        readonly bool[] turned = new bool[KingEscortPath.Cells.Length + 1];
        readonly float[] rookD = new float[2];
        readonly bool[] garaged = new bool[2];
        bool castling;
        float castleAt = -1f;
        int heldBits, pullBits;   // handles held (bit per handle) and pulled forward by our team
        int turnByte;
        readonly float[] rookN = new float[2];

        // What is drawn (eased on a client) and the moments already played.
        float shownS, shownPhase;
        readonly float[] shownRook = new float[2];
        readonly float[] lift = new float[Front];
        float lastS, dustAt, sandAt, bumpAt = -1f;
        readonly float[] lastRook = new float[2];
        readonly bool[] wasGaraged = new bool[2], wasBlocked = new bool[2];
        readonly float[] popAt = { -1f, -1f };
        bool wasTurning, wasCastling, landed;
        AudioSource drag, sand, stone;

        public float Distance => s;

        public static MgKingEscort Build(CourseBuilder b, MissionStation station)
        {
            MissionGames.Floor(b, 0f, 20f);
            var game = b.parent.gameObject.AddComponent<MgKingEscort>();
            game.station = station;
            game.Make(b);
            return game;
        }

        int Opponent => station.Team == Teams.White ? Teams.Black : Teams.White;
        float M => MiniGameParts.Mirror(station);

        Vector3 Cell(string name, float y = 0f)
        {
            PawnRushBoard.Parse(name, out int f, out int r);
            return MiniGameParts.Local(station, PawnRushBoard.Center(f, r), y);
        }

        // The road's direction at s in the game's frame (flat, unit), and the rook grooves'.
        Vector3 Dir(float dv, float du) => new Vector3(dv * M, 0f, du).normalized;

        Vector3 Groove(int k)
        {
            var from = Cell(KingEscortPath.RookCells[k]);
            var to = Cell(KingEscortPath.RookGarages[k]);
            return (to - from).normalized;
        }

        void Make(CourseBuilder b)
        {
            var road = b.Group("Road");
            var cells = KingEscortPath.Cells;
            var sandTop = Arena.Lit("M_Sand", Arena.Hex("#D9C07A"), 0f, 1f);
            var ripple = Arena.Lit("M_SandRipple", Arena.Hex("#BF9F55"), 0f, 1f);
            var curb = Arena.Lit("M_Curb", Arena.Hex("#CFC5B3"), 0f, .8f);
            var gold = Arena.Emissive(Arena.Gold, .5f);
            float h = PathCell * .5f;
            for (int i = 0; i < cells.Length; i++)
            {
                PawnRushBoard.Parse(cells[i], out int f, out int r);
                var c = Cell(cells[i]);
                bool isSand = System.Array.IndexOf(KingEscortPath.Sand, cells[i]) >= 0;
                Arena.Picture(road, "Road " + cells[i], c + new Vector3(-h, 0f, -h), c + new Vector3(h, .08f, h), isSand ? sandTop : Arena.Wood(PawnRushBoard.IsLight(f, r)));
                if (isSand)
                    for (int w = -1; w <= 1; w++)
                        Arena.Picture(road, "Sand ripple", c + new Vector3(-h + .2f, .08f, w * .6f - .04f), c + new Vector3(h - .2f, .09f, w * .6f + .04f), ripple);
                // Curbs on the outer sides: not toward the road's neighbours, d1's south (the start), e8's north (the
                // gate) or a rook square's garage side.
                for (int side = 0; side < 4; side++)
                {
                    int df = side == 0 ? 0 : side == 1 ? 1 : side == 2 ? 0 : -1, dr = side == 0 ? 1 : side == 1 ? 0 : side == 2 ? -1 : 0;
                    string next = PawnRushBoard.OnBoard(f + df, r + dr) ? PawnRushBoard.Name(f + df, r + dr) : "";
                    if (i > 0 && cells[i - 1] == next || i < cells.Length - 1 && cells[i + 1] == next) continue;
                    if (i == 0 && side == 2 || i == cells.Length - 1 && side == 0) continue;
                    int rook = System.Array.IndexOf(KingEscortPath.RookCells, cells[i]);
                    if (rook >= 0 && KingEscortPath.RookGarages[rook] == next) continue;
                    var n = MiniGameParts.Local(station, new StationPoint(df, dr)) - MiniGameParts.Local(station, new StationPoint(0f, 0f));
                    var along = new Vector3(Mathf.Abs(n.z), 0f, Mathf.Abs(n.x));
                    var mid = c + n * (h - .09f);
                    var half = along * h + new Vector3(Mathf.Abs(n.x), 0f, Mathf.Abs(n.z)) * .09f;
                    Arena.Picture(road, "Curb", mid - half, mid + half + Vector3.up * .16f, curb);
                }
                if (KingEscortPath.Turns(i))
                {
                    MiniGameParts.Loop(road, "Turntable " + cells[i], c + Vector3.up * .1f, .9f, .16f, Arena.Gold);
                    var p = KingEscortPath.Points;
                    var to = Dir(p[i + 1].V - p[i].V, p[i + 1].U - p[i].U);
                    float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var arm = Arena.Picture(road, "Turn arrow", new Vector3(-.04f, 0f, -.3f), new Vector3(.04f, .02f, .3f), gold);
                        arm.transform.localPosition = c + Quaternion.Euler(0f, yaw, 0f) * new Vector3(side * .18f, .1f, .1f);
                        arm.transform.localRotation = Quaternion.Euler(0f, yaw - side * 40f, 0f);
                    }
                }
            }
            // The start pad south of d1.
            var d1 = Cell("d1");
            Arena.Picture(road, "Start pad", new Vector3(d1.x - 1.5f, 0f, .8f), new Vector3(d1.x + 1.5f, .06f, 2f), Arena.Lit("M_StartPad", Arena.Hex("#5B6170"), 0f, .8f));

            // The rooks in the way, their grooves, garages and gold stops.
            var them = Opponent;
            ringPlain = Arena.Lit("M_PieceRing", Arena.TeamColor(them), .4f, .5f);
            ringWarn = Arena.Lit("M_PieceRingWarn", Arena.Hex("#F08A2C"), .9f, .5f);
            var garageFloor = Arena.Lit("M_Garage", Arena.Hex("#2F333D"), 0f, .8f);
            var grooveMat = Arena.Lit("M_Groove", Arena.Hex("#1D2028"), 0f, .8f);
            for (int k = 0; k < 2; k++)
            {
                var from = Cell(KingEscortPath.RookCells[k]);
                var garage = Cell(KingEscortPath.RookGarages[k]);
                var g = Groove(k);
                var across = new Vector3(g.z, 0f, g.x);
                Arena.Picture(road, "Garage", garage + new Vector3(-h, 0f, -h), garage + new Vector3(h, .05f, h), garageFloor);
                var mid = (from + garage) * .5f;
                var half = g * (KingEscortPath.RookDrag * .5f) + across * .2f;
                Arena.Picture(road, "Rook groove", mid - Abs(half), mid + Abs(half) + Vector3.up * .09f, grooveMat);
                var stop = Arena.Picture(road, "Rook stop", -Abs(across * .8f + g * .07f), Abs(across * .8f + g * .07f) + Vector3.up * .2f, gold);
                stop.transform.localPosition = garage + g * (h - .1f);
                stops[k] = stop.transform;
                var rook = MiniGameParts.Piece(b.parent, "Rook in the way " + (k + 1), PieceKind.Rook, 1.6f, MiniGameParts.PieceMaterial(them), Arena.TeamColor(them), true, .4f);
                rook.transform.localPosition = from;
                rooks[k] = rook.GetComponent<Rigidbody>();
                foreach (var r in rook.GetComponentsInChildren<Renderer>()) if (r.name == "Ring") rookRings[k] = r;
                var handle = MiniGameParts.Handle(b.parent, "Rook ring " + (k + 1), from + Vector3.up * 1f, new Vector3(.95f, .14f, .95f), this, (byte)(RookHandle + k), GrabHandle.HandleKind.Drag);
                handles[RookHandle + k] = handle;
                handleBodies[RookHandle + k] = handle.GetComponent<Rigidbody>();
            }

            // Our rook tower on g8.
            var g8 = Cell("g8");
            b.Box("Rook tower base", g8 + new Vector3(-.9f, 0f, -.9f), g8 + new Vector3(.9f, .3f, .9f), Arena.Lit("M_TowerBase", Arena.Hex("#CFC5B3"), 0f, .8f), true);
            tower = MiniGameParts.Piece(b.parent, "Rook tower", PieceKind.Rook, 2.2f, MiniGameParts.PieceMaterial(station.Team), Arena.TeamColor(station.Team), false).transform;
            tower.localPosition = g8 + Vector3.up * .3f;

            // The sled: a walnut deck on two runners with our king on it, one kinematic box.
            var sledGo = new GameObject("Sled");
            sledGo.transform.SetParent(b.parent, false);
            sled = sledGo.AddComponent<Rigidbody>();
            sled.isKinematic = true;
            sled.useGravity = false;
            sled.interpolation = RigidbodyInterpolation.Interpolate;
            var box = sledGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, .27f, 0f);
            box.size = new Vector3(SledWidth, .22f, SledLength);
            sledGo.AddComponent<NoClimbSurface>();
            sledLook = new GameObject("Look").transform;
            sledLook.SetParent(sledGo.transform, false);
            MiniGameParts.Part(sledLook, PrimitiveType.Cube, new Vector3(0f, .27f, 0f), new Vector3(SledWidth, .22f, SledLength), Arena.Wood(false));
            foreach (float side in new[] { -.6f, .6f })
                MiniGameParts.Part(sledLook, PrimitiveType.Cube, new Vector3(side, .08f, 0f), new Vector3(.08f, .16f, SledLength + .2f), Arena.Lit("M_Runner", new Color(.4f, .4f, .42f), 0f, .4f));
            MiniGameParts.Piece(sledLook, "Our king", PieceKind.King, 1.7f, MiniGameParts.PieceMaterial(station.Team), Arena.TeamColor(station.Team), false).transform.localPosition = new Vector3(0f, .38f, 0f);

            // Front ropes with gold handles, rear grey handles.
            ropeSlack = Arena.Lit("M_SledRope", Arena.Hex("#C9A57A"), 0f, .9f);
            ropeTaut = Arena.Lit("M_SledRopeTaut", Arena.Hex("#E8C99A"), .15f, .8f);
            for (int i = 0; i < Front; i++)
            {
                var hd = MiniGameParts.Handle(b.parent, "Sled rope handle " + (i + 1), Vector3.zero, new Vector3(.26f, .18f, .14f), this, (byte)i, GrabHandle.HandleKind.Pull);
                handles[i] = hd;
                handleBodies[i] = hd.GetComponent<Rigidbody>();
                var line = new GameObject("Sled rope").AddComponent<LineRenderer>();
                line.transform.SetParent(b.parent, false);
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.widthMultiplier = .07f;
                line.sharedMaterial = ropeSlack;
                ropes[i] = line;
            }
            for (int i = 0; i < Rear; i++)
            {
                var hd = MiniGameParts.Handle(b.parent, "Sled rear handle " + (i + 1), Vector3.zero, new Vector3(.12f, .5f, .12f), this, (byte)(Front + i), GrabHandle.HandleKind.Pull);
                hd.GetComponent<Renderer>().sharedMaterial = Arena.Lit("M_RearHandle", new Color(.55f, .55f, .58f), 0f, .5f);
                handles[Front + i] = hd;
                handleBodies[Front + i] = hd.GetComponent<Rigidbody>();
            }
            Place(0f, 0f, true);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        Vector3 World(Vector3 local) => transform.TransformPoint(local);

        // ------------------------------------------------------------------ the sled's pose from s

        // The turntable cell the sled sits on, or -1.
        static int TurnAt(float s)
        {
            for (int i = 1; i < KingEscortPath.Cells.Length; i++)
                if (KingEscortPath.Turns(i) && Mathf.Abs(KingEscortPath.At(i) - s) < 1e-3f) return i;
            return -1;
        }

        // Position and yaw in the game's frame: along the road, turning on a turntable by `phase` (0 the way in,
        // 1 the way out).
        void Pose(float at, float phase, out Vector3 local, out float yaw)
        {
            var p = KingEscortPath.Position(at, out float dv, out float du);
            local = MiniGameParts.Local(station, p);
            var dir = Dir(dv, du);
            int cell = TurnAt(at);
            if (cell >= 0)
            {
                var pts = KingEscortPath.Points;
                var into = Dir(pts[cell].V - pts[cell - 1].V, pts[cell].U - pts[cell - 1].U);
                var outOf = Dir(pts[cell + 1].V - pts[cell].V, pts[cell + 1].U - pts[cell].U);
                float a = Mathf.Atan2(into.x, into.z) * Mathf.Rad2Deg, b = Mathf.Atan2(outOf.x, outOf.z) * Mathf.Rad2Deg;
                float e = phase * phase * (3f - 2f * phase);
                yaw = Mathf.LerpAngle(a, b, e);
                return;
            }
            yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        // The sled, its handles and the rooks where the numbers put them (kinematic moves, or set outright).
        void Place(float at, float phase, bool snap)
        {
            Pose(at, phase, out var local, out float yaw);
            var rot = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
            var pos = World(local);
            if (snap) sled.transform.SetPositionAndRotation(pos, rot);
            else { sled.MovePosition(pos); sled.MoveRotation(rot); }
            for (int i = 0; i < Front; i++)
            {
                // A rope lies 2 m in front of the sled; held, its handle is up at hand height.
                var anchor = pos + rot * new Vector3(FrontX[i], .3f, SledLength * .5f);
                var at2 = anchor + rot * new Vector3(0f, 0f, RopeLength);
                at2.y = pos.y + Mathf.Lerp(.12f, .8f, lift[i]);
                Move(i, at2, rot, snap);
            }
            for (int i = 0; i < Rear; i++)
                Move(Front + i, pos + rot * new Vector3(i == 0 ? -.5f : .5f, .6f, -SledLength * .5f - .1f), rot, snap);
            for (int k = 0; k < 2; k++)
            {
                var rp = World(Cell(KingEscortPath.RookCells[k]) + Groove(k) * shownRook[k]);
                if (snap) rooks[k].transform.position = rp; else rooks[k].MovePosition(rp);
                Move(RookHandle + k, rp + transform.up * 1f, transform.rotation, snap);
            }
        }

        void Move(int i, Vector3 pos, Quaternion rot, bool snap)
        {
            var b = handleBodies[i];
            if (b == null) return;
            if (snap) b.transform.SetPositionAndRotation(pos, rot);
            else { b.MovePosition(pos); b.MoveRotation(rot); }
        }

        Vector3 Anchor(int i)
        {
            Pose(shownS, shownPhase, out var local, out float yaw);
            return World(local) + transform.rotation * Quaternion.Euler(0f, yaw, 0f) * new Vector3(FrontX[i], .3f, SledLength * .5f);
        }

        // ------------------------------------------------------------------ host

        void FixedUpdate()
        {
            if (station == null || sled == null) return;
            if (!Remote && !Completed) Step(Time.fixedDeltaTime);
            float dt = Time.fixedDeltaTime;
            if (Remote)
            {
                shownS = Mathf.MoveTowards(shownS, s, dt * 1.6f);
                float phase = turnByte == 0 ? 0f : (turnByte - 1) / 254f;
                shownPhase = Mathf.MoveTowards(shownPhase, phase, dt * 4f);
                for (int k = 0; k < 2; k++) shownRook[k] = Mathf.MoveTowards(shownRook[k], rookD[k], dt * 1.6f);
            }
            else
            {
                shownS = s;
                shownPhase = turnByte == 0 ? 0f : (turnByte - 1) / 254f;
                for (int k = 0; k < 2; k++) shownRook[k] = rookD[k];
            }
            for (int i = 0; i < Front; i++) lift[i] = Mathf.MoveTowards(lift[i], (heldBits >> i & 1) != 0 ? 1f : 0f, dt / .3f);
            Place(shownS, shownPhase, false);
        }

        void Step(float dt)
        {
            Pose(s, Phase(), out _, out float yaw);
            var forward = transform.rotation * Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            for (int i = 0; i < Front + Rear; i++) if (handles[i] != null) handles[i].PullAxis = forward;
            for (int k = 0; k < 2; k++) if (handles[RookHandle + k] != null) handles[RookHandle + k].PullAxis = transform.TransformDirection(Groove(k));

            int n = 0, held = 0, pulled = 0;
            float e = 0f;
            rookN[0] = rookN[1] = 0f;
            foreach (var p in station.Pawns())
            {
                var mg = p.Driver as IMiniGamePawn;
                var h = GrabHandle.HeldBy(mg);
                if (h == null || h.Owner != this) continue;
                held |= 1 << h.Id;
                if (h.Id < Front) Leash(p, mg, h.Id);
                int effort = h.Effort(p.Driver);
                if (h.Id < RookHandle)
                {
                    if (p.Own && effort > 0 && n < PawnRushMissions.WorkerCap) { n++; pulled |= 1 << h.Id; }
                    else if (!p.Own && effort < 0) e += 1f;
                }
                else
                {
                    int k = h.Id - RookHandle;
                    if (p.Own && effort > 0) { rookN[k] += 1f; pulled |= 1 << h.Id; }
                    else if (!p.Own && effort < 0) rookN[k] -= .5f;
                }
            }
            heldBits = held;
            pullBits = pulled;

            // The rooks: 0.6 m/s for one, 1.1 for two or more, into the garage and latched there.
            for (int k = 0; k < 2; k++)
            {
                if (garaged[k]) continue;
                float r = rookN[k], v = r <= 0f ? 0f : r < 1f ? KingEscortPath.RookDragOne * r : Mathf.Min(KingEscortPath.RookDragTwo, KingEscortPath.RookDragOne + (KingEscortPath.RookDragTwo - KingEscortPath.RookDragOne) * (r - 1f));
                rookD[k] = Mathf.Min(KingEscortPath.RookDrag, rookD[k] + v * dt);
                if (rookD[k] >= KingEscortPath.RookDrag - 1e-4f) garaged[k] = true;
            }

            float effective = Mathf.Max(0f, n - .5f * e);
            if (turnCell >= 0)
            {
                if (effective > 0f) turnT += dt / KingEscortPath.TurnSeconds;
                if (turnT >= 1f) { turnT = 1f; turned[turnCell] = true; turnCell = -1; }
            }
            else
            {
                float v = KingEscortPath.Speed(effective, KingEscortPath.IsSand(s));
                if (v > 0f)
                {
                    float next = s + v * dt;
                    for (int i = 1; i < KingEscortPath.Cells.Length; i++)
                    {
                        float at = KingEscortPath.At(i);
                        if (!KingEscortPath.Turns(i) || turned[i] || at < s - 1e-4f || at > next) continue;
                        next = at;
                        turnCell = i;
                        turnT = 0f;
                        break;
                    }
                    for (int k = 0; k < 2; k++)
                        if (!garaged[k]) next = Mathf.Min(next, KingEscortPath.RookStop(k));
                    s = Mathf.Max(s, Mathf.Min(KingEscortPath.Length, next));
                }
            }
            turnByte = TurnByte();
            Progress01 = Mathf.Min(.999f, s / KingEscortPath.Length);
            if (!castling && s >= KingEscortPath.Length - 1e-3f)
            {
                castling = true;
                castleAt = Time.time;
            }
            if (castling && Time.time - castleAt >= CastleSeconds) Complete();
        }

        float Phase() => turnByte == 0 ? 0f : (turnByte - 1) / 254f;

        int TurnByte()
        {
            int cell = TurnAt(s);
            if (cell < 0) return 0;
            float phase = turnCell == cell ? turnT : turned[cell] ? 1f : 0f;
            return 1 + Mathf.RoundToInt(phase * 254f);
        }

        // §5.2.7: a pawn on a front rope stays within 2.3 m of where the rope is tied.
        void Leash(MissionStation.Pawn p, IMiniGamePawn mg, int rope)
        {
            if (mg == null || p.Driver.FollowTarget == null) return;
            var to = Anchor(rope) - p.Driver.FollowTarget.position;
            to.y = 0f;
            float over = to.magnitude - RopeLeash;
            if (over <= 0f) return;
            var dir = to.normalized;
            float want = Mathf.Min(LeashMax, over * LeashPull), has = Vector3.Dot(mg.HipsVelocity, dir);
            if (want > has) mg.AddVelocity(dir * (want - has));
        }

        // ------------------------------------------------------------------ moments (every PC)

        // StateDrivenMover: the sled, its handles and the rooks follow the host's numbers (s, turn, drag), not the
        // shared obstacle clock.
        void Update()
        {
            if (sled == null) return;
            float t = Time.time;
            var sledAt = sled.transform.position;
            for (int i = 0; i < Front; i++)
            {
                var a = Anchor(i);
                var hp = handles[i].transform.position;
                bool taut = (pullBits >> i & 1) != 0;
                float shiver = taut ? .015f * Mathf.Sin(t * 40f * Mathf.PI * 2f) : 0f;
                ropes[i].SetPosition(0, a);
                ropes[i].SetPosition(1, hp + Vector3.up * shiver);
                var want = taut ? ropeTaut : ropeSlack;
                if (ropes[i].sharedMaterial != want) ropes[i].sharedMaterial = want;
            }
            for (int i = 0; i < Handles; i++)
                if (handles[i] != null) handles[i].ShowHeld((pullBits >> i & 1) != 0 ? station.Team : (heldBits >> i & 1) != 0 ? Opponent : -1);

            // Moving: dust behind (sand: more, and sand-coloured), the drag loop pitched by speed.
            float speed = Time.deltaTime > 0f ? (shownS - lastS) / Time.deltaTime : 0f;
            lastS = shownS;
            bool moving = speed > .05f && !Completed;
            bool onSand = KingEscortPath.IsSand(shownS);
            var behind = sledAt - sled.transform.forward * (SledLength * .5f + .1f) + Vector3.up * .05f;
            if (moving && !onSand && t - dustAt >= .3f)
            {
                dustAt = t;
                ArenaFx.Burst(behind, new[] { new Color(.8f, .72f, .6f) }, 3, .7f, .5f, .5f, .06f);
            }
            if (moving && onSand && t - sandAt >= .12f)
            {
                sandAt = t;
                ArenaFx.Burst(behind, new[] { Arena.Hex("#D9C07A") }, 6, .9f, .7f, .5f, .06f);
            }
            if (moving)
            {
                if (drag == null || !drag.isPlaying) drag = ArenaFx.Hold("sfx_sled_drag", sledAt, 1f, .6f);
                if (drag != null) { drag.transform.position = sledAt; drag.pitch = Mathf.Lerp(.8f, 1.2f, Mathf.Clamp01(speed / 1.1f)); }
                if (onSand && (sand == null || !sand.isPlaying)) sand = ArenaFx.Hold("sfx_sand_crunch", sledAt, 1f, .6f);
            }
            if (!moving) { ArenaFx.Cut(drag); drag = null; }
            if (!moving || !onSand) { ArenaFx.Cut(sand); sand = null; }

            // The turntable: a ratchet at the start, a gold ring when it is round.
            bool turning = shownPhase > 0f && shownPhase < 1f;
            if (turning && !wasTurning) ArenaFx.Sfx("sfx_turntable", sledAt);
            if (!turning && wasTurning && shownPhase >= 1f) ArenaFx.Ring(sledAt, 0f, 1.2f, .35f, Arena.Gold);
            wasTurning = turning;

            // The rooks: stopped in front of one, dragging one, one in its garage.
            bool rookMoving = false;
            for (int k = 0; k < 2; k++)
            {
                bool g = shownRook[k] >= KingEscortPath.RookDrag - .01f;
                bool blocked = !g && !moving && Mathf.Abs(shownS - KingEscortPath.RookStop(k)) < .02f;
                if (blocked && !wasBlocked[k])
                {
                    bumpAt = t;
                    ArenaFx.Sfx("sfx_sled_blocked", sledAt);
                }
                wasBlocked[k] = blocked;
                var ring = blocked && Mathf.Repeat(t / .5f, 1f) < .5f ? ringWarn : ringPlain;
                if (rookRings[k] != null && rookRings[k].sharedMaterial != ring) rookRings[k].sharedMaterial = ring;
                float rv = Time.deltaTime > 0f ? (shownRook[k] - lastRook[k]) / Time.deltaTime : 0f;
                lastRook[k] = shownRook[k];
                if (rv > .05f)
                {
                    rookMoving = true;
                    if (Random.value < .2f) ArenaFx.Burst(rooks[k].transform.position + Vector3.up * .05f, new[] { new Color(.6f, .6f, .6f) }, 2, .5f, .4f, .4f, .05f);
                }
                if (g && !wasGaraged[k])
                {
                    popAt[k] = t;
                    var at = World(stops[k].localPosition);
                    ArenaFx.Ring(at, 0f, 2f, .5f, Arena.Gold);
                    ArenaFx.Burst(at + Vector3.up * .2f, new[] { Arena.Gold, new Color(.6f, .6f, .6f) }, 14, 1.6f, 1.6f, .7f, .08f);
                    ArenaFx.Sfx("sfx_latch", at);
                }
                wasGaraged[k] = g;
                if (stops[k] != null)
                {
                    float pop = popAt[k] < 0f ? 1f : (t - popAt[k]) / .1f;
                    var sp = stops[k].localPosition;
                    stops[k].localPosition = new Vector3(sp.x, .1f + (pop < 1f ? .08f * Mathf.Sin(pop * Mathf.PI) : 0f), sp.z);
                }
            }
            if (rookMoving)
            {
                if (stone == null || !stone.isPlaying) stone = ArenaFx.Hold("sfx_stone_drag", rooks[0].transform.position, 1f, .6f);
            }
            else { ArenaFx.Cut(stone); stone = null; }

            // A bump when the sled hits a rook's stop: a 0.1 s jolt of its look.
            float bump = (t - bumpAt) / .1f;
            sledLook.localPosition = bumpAt >= 0f && bump < 1f ? new Vector3(0f, .04f * Mathf.Sin(bump * Mathf.PI), 0f) : Vector3.zero;

            Castle(t);
        }

        // At the gate: our rook tower jumps from g8 over the king to d8 in 1.3 s (3.2 m up, one turn round).
        void Castle(float t)
        {
            if (tower == null) return;
            if (castling && !wasCastling)
            {
                if (castleAt < 0f) castleAt = t;
                var at = World(Cell("g8"));
                ArenaFx.Beam(at, 1.2f, 1.8f, 7f, 1.3f, Arena.TeamColor(station.Team));
                ArenaFx.Sfx("sfx_castle_whoosh", at);
                landed = false;
            }
            wasCastling = castling;
            var from = Cell("g8", .3f);
            var to = Cell("d8");
            if (!castling) { tower.localPosition = from; tower.localRotation = Quaternion.identity; return; }
            float k = Mathf.Clamp01((t - castleAt) / CastleSeconds);
            var p = Vector3.Lerp(from, to, k);
            p.y += CastleApex * 4f * k * (1f - k);
            tower.localPosition = p;
            tower.localRotation = Quaternion.Euler(0f, 360f * k, 0f);
            if (k >= 1f && !landed)
            {
                landed = true;
                var at = World(to);
                ArenaFx.Ring(at, 0f, 2.5f, .6f, Arena.TeamColor(station.Team));
                ArenaFx.Burst(at + Vector3.up * .2f, new[] { Arena.TeamColor(station.Team), Arena.Gold }, 20, 2f, 2.5f, .9f, .1f);
                ArenaFx.Sfx("sfx_castle_land", at);
            }
        }

        // ------------------------------------------------------------------ the wire (§5.6, 9 bytes)

        public override void WriteNet(ref NetBlock block)
        {
            block.U16(Mathf.Clamp(Mathf.RoundToInt(s * 1000f), 0, 65535));
            block.U8(turnByte);
            block.U8(Mathf.RoundToInt(rookD[0] * 100f));
            block.U8(Mathf.RoundToInt(rookD[1] * 100f));
            block.U8((garaged[0] ? 1 : 0) | (garaged[1] ? 2 : 0) | (castling ? 4 : 0) | (KingEscortPath.IsSand(s) ? 8 : 0));
            block.U8(heldBits);
            block.U8(pullBits);
        }

        public override void ReadNet(ref NetBlock block)
        {
            s = block.ReadU16() / 1000f;
            turnByte = block.ReadU8();
            rookD[0] = block.ReadU8() / 100f;
            rookD[1] = block.ReadU8() / 100f;
            int flags = block.ReadU8();
            garaged[0] = (flags & 1) != 0;
            garaged[1] = (flags & 2) != 0;
            bool castle = (flags & 4) != 0;
            if (castle && !castling) castleAt = Time.time;
            castling = castle;
            heldBits = block.ReadU8();
            pullBits = block.ReadU8();
        }

        public override void ResetGame()
        {
            base.ResetGame();
            s = shownS = lastS = turnT = shownPhase = 0f;
            turnCell = -1;
            turnByte = 0;
            for (int i = 0; i < turned.Length; i++) turned[i] = false;
            for (int k = 0; k < 2; k++)
            {
                rookD[k] = shownRook[k] = lastRook[k] = 0f;
                garaged[k] = wasGaraged[k] = wasBlocked[k] = false;
                popAt[k] = -1f;
            }
            castling = wasCastling = landed = false;
            castleAt = -1f;
            heldBits = pullBits = 0;
            if (sled != null) Place(0f, 0f, true);
        }
    }
}
