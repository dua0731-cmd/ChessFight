using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // G, king escort (mini-games v1.0 §5, reworked in R128): our king on a square sled is pushed and pulled along
    // fifteen board squares (KingEscortPath, d1 to e8 in front of the gate); two of the other side's rooks stand in the
    // road until they are pushed or dragged into their garages; on e8 our rook tower jumps over the king (castling) and
    // the gate opens. R524: the road ends on e8, the sled whole on the board (a gold frame shows where it stops); it used
    // to run on into the team door's tunnel, so the sled had to be pushed into the north wall.
    //
    // R128 (user, after playing it): no ropes and no gold handle blocks. The ropes left no room by the walls, the
    // small blocks were hard to catch, grab + forward climbed them, and turning the sled pulled the grips off. Now
    // the king statue and the rooks are themselves the things to move: a pawn holds them (any side) or presses on
    // them from behind and moves the way the thing has to go (MiniGameParts.Shove); the sled never turns - at a
    // corner the team moves round to push it the new way. They are GrabOnlySurface: held by hand, never climbed.
    //
    // Physics and the network (§1.2): the sled is one distance s along the road, worked out on the host; the sled
    // and the rooks are kinematic bodies moved to the host's numbers, and a hand that holds one is dragged with
    // it. The host sends s, both rooks' drag, flags and how many push each way (7 bytes); a client eases to them
    // and plays every moment from what changed.
    public sealed class MgKingEscort : MiniGameBase
    {
        public const float SledSize = 1.6f, SledHalf = SledSize * .5f, RookHalf = .45f;
        public const float CastleSeconds = 1.6f, CastleApex = 3.4f, PathCell = 2.22f;
        // R522: the castling jump's beats - the crouch, the flight (the landing's squash and bounces take the rest of
        // CastleSeconds) - and where it lands: c8, beside the team door, not d8 in front of the door's open half.
        const float CastleCrouch = .22f, CastleFlight = 1f, TowerMiddle = 1.1f;
        const string CastleFrom = "g8", CastleTo = "c8";

        [SerializeField] MissionStation station;

        Rigidbody sled;
        Transform sledLook;
        LineRenderer sledRing;
        readonly Rigidbody[] rooks = new Rigidbody[2];
        readonly Renderer[] rookRings = new Renderer[2];
        readonly Transform[] stops = new Transform[2];
        Transform tower;
        Renderer towerRing;
        Material ringPlain, ringWarn;

        // Host state (a client's copy from the wire).
        float s;
        readonly float[] rookD = new float[2];
        readonly bool[] garaged = new bool[2];
        bool castling;
        float castleAt = -1f;
        int pushers, holders;                  // ours moving the sled on, theirs holding it back
        readonly int[] rookPushers = new int[2];
        readonly float[] rookN = new float[2];

        // What is drawn (eased on a client) and the moments already played.
        float shownS;
        readonly float[] shownRook = new float[2];
        float lastS, dustAt, sandAt, bumpAt = -1f;
        readonly float[] lastRook = new float[2];
        readonly bool[] wasGaraged = new bool[2], wasBlocked = new bool[2];
        readonly float[] popAt = { -1f, -1f };
        bool wasCastling, tookOff, landed;
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

        // A road direction (white's view) in the game's frame (flat, unit), and the rook grooves'.
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
                // Curbs on the outer sides: not toward the road's neighbours, d1's south (the start) or a rook square's
                // garage side. The end, e8, is closed all round (R524: it was open north, the road going on to the gate).
                for (int side = 0; side < 4; side++)
                {
                    int df = side == 0 ? 0 : side == 1 ? 1 : side == 2 ? 0 : -1, dr = side == 0 ? 1 : side == 1 ? 0 : side == 2 ? -1 : 0;
                    string next = PawnRushBoard.OnBoard(f + df, r + dr) ? PawnRushBoard.Name(f + df, r + dr) : "";
                    if (i > 0 && cells[i - 1] == next || i < cells.Length - 1 && cells[i + 1] == next) continue;
                    if (i == 0 && side == 2) continue;
                    int rook = System.Array.IndexOf(KingEscortPath.RookCells, cells[i]);
                    if (rook >= 0 && KingEscortPath.RookGarages[rook] == next) continue;
                    var n = MiniGameParts.Local(station, new StationPoint(df, dr)) - MiniGameParts.Local(station, new StationPoint(0f, 0f));
                    var along = new Vector3(Mathf.Abs(n.z), 0f, Mathf.Abs(n.x));
                    // R498: the curb's outer face stands a centimetre past the road square's, not in its plane.
                    var mid = c + n * (h - .09f + MissionGames.Proud * .5f);
                    var half = along * h + new Vector3(Mathf.Abs(n.x), 0f, Mathf.Abs(n.z)) * (.09f + MissionGames.Proud * .5f);
                    Arena.Picture(road, "Curb", mid - half, mid + half + Vector3.up * .16f, curb);
                }
                // A corner: a gold ring and an arrow the way the road goes on (the king does not turn; it is pushed
                // the new way from its other side).
                if (KingEscortPath.Turns(i))
                {
                    MiniGameParts.Loop(road, "Corner " + cells[i], c + Vector3.up * .1f, .9f, .16f, Arena.Gold);
                    var p = KingEscortPath.Points;
                    var to = Dir(p[i + 1].V - p[i].V, p[i + 1].U - p[i].U);
                    float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var arm = Arena.Picture(road, "Corner arrow", new Vector3(-.04f, 0f, -.3f), new Vector3(.04f, .02f, .3f), gold);
                        arm.transform.localPosition = c + Quaternion.Euler(0f, yaw, 0f) * new Vector3(side * .18f, .1f, .1f);
                        arm.transform.localRotation = Quaternion.Euler(0f, yaw - side * 40f, 0f);
                    }
                }
                // The end (R524): a gold frame just round the sled's footprint, where it stops for the castling. The
                // bars across run between the bars along, so no two tops overlap.
                if (i == cells.Length - 1)
                {
                    const float edge = SledHalf + .1f, bar = .05f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Arena.Picture(road, "Goal frame", c + new Vector3(-edge - bar, .081f, side * edge - bar), c + new Vector3(edge + bar, .1f, side * edge + bar), gold);
                        Arena.Picture(road, "Goal frame", c + new Vector3(side * edge - bar, .081f, -edge + bar), c + new Vector3(side * edge + bar, .1f, edge - bar), gold);
                    }
                }
            }
            // The start pad south of d1.
            var d1 = Cell("d1");
            Arena.Picture(road, "Start pad", new Vector3(d1.x - 1.5f, 0f, .8f), new Vector3(d1.x + 1.5f, .06f, 2f), Arena.Lit("M_StartPad", Arena.Hex("#5B6170"), 0f, .8f));

            // The rooks in the way (grab-only: held and pushed, never climbed), their grooves, garages and gold stops.
            var them = Opponent;
            ringPlain = Arena.Lit("M_PieceRing", station.Accent(them), .4f, .5f);
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
                var rook = MiniGameParts.Piece(b.parent, "Rook in the way " + (k + 1), PieceKind.Rook, 1.6f, MiniGameParts.PieceMaterial(them), station.Accent(them), true, .4f, grabbable: true);
                rook.transform.localPosition = from;
                rooks[k] = rook.GetComponent<Rigidbody>();
                foreach (var r in rook.GetComponentsInChildren<Renderer>()) if (r.name == "Ring") rookRings[k] = r;
            }

            // Our rook tower on g8.
            var g8 = Cell("g8");
            b.Box("Rook tower base", g8 + new Vector3(-.9f, 0f, -.9f), g8 + new Vector3(.9f, .3f, .9f), Arena.Lit("M_TowerBase", Arena.Hex("#CFC5B3"), 0f, .8f), true);
            tower = MiniGameParts.Piece(b.parent, "Rook tower", PieceKind.Rook, 2.2f, MiniGameParts.PieceMaterial(station.Team), station.Accent(station.Team), false).transform;
            tower.localPosition = g8 + Vector3.up * .3f;
            foreach (var r in tower.GetComponentsInChildren<Renderer>(true)) if (r.name == "Ring") towerRing = r;

            // The sled: a square walnut base on runners with our king on it, one kinematic body whose colliders (the
            // base, and the king at hand height) are held by hand or pushed, never climbed. It never turns.
            var sledGo = new GameObject("King sled");
            sledGo.transform.SetParent(b.parent, false);
            sled = sledGo.AddComponent<Rigidbody>();
            sled.isKinematic = true;
            sled.useGravity = false;
            sled.interpolation = RigidbodyInterpolation.Interpolate;
            var box = sledGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, .3f, 0f);
            box.size = new Vector3(SledSize, .5f, SledSize);
            var king = sledGo.AddComponent<CapsuleCollider>();
            king.radius = .42f;
            king.height = 1.7f;
            king.center = new Vector3(0f, .55f + .85f, 0f);
            sledGo.AddComponent<GrabOnlySurface>();
            sledLook = new GameObject("Look").transform;
            sledLook.SetParent(sledGo.transform, false);
            MiniGameParts.Part(sledLook, PrimitiveType.Cube, new Vector3(0f, .3f, 0f), new Vector3(SledSize, .5f, SledSize), Arena.Wood(false));
            foreach (float side in new[] { -.6f, .6f })
                MiniGameParts.Part(sledLook, PrimitiveType.Cube, new Vector3(side, .03f, 0f), new Vector3(.08f, .06f, SledSize + .1f), Arena.Lit("M_Runner", new Color(.4f, .4f, .42f), 0f, .4f));
            MiniGameParts.Piece(sledLook, "Our king", PieceKind.King, 1.7f, MiniGameParts.PieceMaterial(station.Team), station.Accent(station.Team), false).transform.localPosition = new Vector3(0f, .55f, 0f);
            // A ring round the base: our colour while it is pushed on, theirs while they hold it back.
            sledRing = MiniGameParts.Loop(sledLook, "Push ring", new Vector3(0f, .06f, 0f), SledHalf + .45f, .08f, station.Accent(station.Team));
            sledRing.enabled = false;
            Place(0f, true);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        Vector3 World(Vector3 local) => transform.TransformPoint(local);

        // The sled's place in the game's frame at s, and the way the road goes there (at a corner, the way on).
        Vector3 SledAt(float at, out Vector3 dir)
        {
            var p = KingEscortPath.Position(at, out float dv, out float du);
            dir = Dir(dv, du);
            return MiniGameParts.Local(station, p);
        }

        Vector3 RookAt(int k, float d) => Cell(KingEscortPath.RookCells[k]) + Groove(k) * d;

        // The sled and the rooks where the numbers put them (kinematic moves, or set outright).
        void Place(float at, bool snap)
        {
            var pos = World(SledAt(at, out _));
            if (snap) sled.transform.SetPositionAndRotation(pos, transform.rotation);
            else { sled.MovePosition(pos); sled.MoveRotation(transform.rotation); }
            for (int k = 0; k < 2; k++)
            {
                var rp = World(RookAt(k, shownRook[k]));
                if (snap) rooks[k].transform.position = rp; else rooks[k].MovePosition(rp);
            }
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
                for (int k = 0; k < 2; k++) shownRook[k] = Mathf.MoveTowards(shownRook[k], rookD[k], dt * 1.6f);
            }
            else
            {
                shownS = s;
                for (int k = 0; k < 2; k++) shownRook[k] = rookD[k];
            }
            Place(shownS, false);
        }

        void Step(float dt)
        {
            var at = SledAt(s, out var dir);
            int n = 0, e = 0;
            rookN[0] = rookN[1] = 0f;
            rookPushers[0] = rookPushers[1] = 0;
            foreach (var p in station.Pawns())
            {
                int on = MiniGameParts.Shove(p, transform, sled, at, dir, SledHalf);
                if (p.Own && on > 0) n++;
                else if (!p.Own && on < 0) e++;
                for (int k = 0; k < 2; k++)
                {
                    if (garaged[k]) continue;
                    int r = MiniGameParts.Shove(p, transform, rooks[k], RookAt(k, rookD[k]), Groove(k), RookHalf);
                    if (p.Own && r > 0) { rookN[k] += 1f; rookPushers[k]++; }
                    else if (!p.Own && r < 0) rookN[k] -= .5f;
                }
            }
            pushers = Mathf.Min(PawnRushMissions.WorkerCap, n);
            holders = e;

            // The rooks: 0.6 m/s for one, 1.1 for two or more, into the garage and latched there.
            for (int k = 0; k < 2; k++)
            {
                if (garaged[k]) continue;
                float r = rookN[k], v = r <= 0f ? 0f : r < 1f ? KingEscortPath.RookDragOne * r : Mathf.Min(KingEscortPath.RookDragTwo, KingEscortPath.RookDragOne + (KingEscortPath.RookDragTwo - KingEscortPath.RookDragOne) * (r - 1f));
                rookD[k] = Mathf.Min(KingEscortPath.RookDrag, rookD[k] + v * dt);
                if (rookD[k] >= KingEscortPath.RookDrag - 1e-4f) garaged[k] = true;
            }

            // The sled: the speed table for (ours - half of theirs), slower on sand, never back, held at a rook.
            float effective = Mathf.Max(0f, pushers - .5f * holders);
            float speed = KingEscortPath.Speed(effective, KingEscortPath.IsSand(s));
            if (speed > 0f)
            {
                float next = s + speed * dt;
                for (int k = 0; k < 2; k++)
                    if (!garaged[k]) next = Mathf.Min(next, KingEscortPath.RookStop(k));
                s = Mathf.Max(s, Mathf.Min(KingEscortPath.Length, next));
            }
            Progress01 = Mathf.Min(.999f, s / KingEscortPath.Length);
            if (!castling && s >= KingEscortPath.Length - 1e-3f)
            {
                castling = true;
                castleAt = Time.time;
            }
            if (castling && Time.time - castleAt >= CastleSeconds) Complete();
        }

        // ------------------------------------------------------------------ moments (every PC)

        // StateDrivenMover: the sled and the rooks follow the host's numbers (s, drag), not the shared obstacle clock.
        void Update()
        {
            if (sled == null) return;
            float t = Time.time;
            var sledAt = sled.transform.position;

            float speed = Time.deltaTime > 0f ? (shownS - lastS) / Time.deltaTime : 0f;
            lastS = shownS;
            bool moving = speed > .05f && !Completed;
            bool onSand = KingEscortPath.IsSand(shownS);

            // The push ring: ours while pushed on, theirs blinking while held back.
            bool held = holders > 0 && Mathf.Repeat(t * 3f, 1f) < .5f;
            sledRing.enabled = pushers > 0 || held;
            var rc = held ? station.Accent(Opponent) : station.Accent(station.Team);
            sledRing.startColor = sledRing.endColor = rc;

            // Moving: dust behind (sand: more, and sand-coloured), the drag loop pitched by speed.
            SledAt(shownS, out var dir);
            var behind = sledAt - transform.TransformDirection(dir) * (SledHalf + .1f) + Vector3.up * .05f;
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

            // The rooks: stopped in front of one, pushing one, one in its garage.
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

        // At the road's end our rook tower castles over the king. R522 (the user's list, 11-09, 6: "왕의 행차 회랑
        // 마지막구간 수정 필요. 룩이 점프뛰는 연출도 별로임"): it crouches (0.22 s, down and wide, leaning back), leaps from g8 in an arc
        // over the king (on e8 since R524, so straight along rank 8) - stretched as it leaves and as it comes down, one
        // forward somersault round its middle, its floor band left off while it flies - and lands on c8 beside the team door
        // with a squash and two bounces, dust and its ring; the whoosh goes with the leap. Before, it floated at one speed
        // along the rank, spun like a top with its band stuck under it, landed without a jolt, and stood on d8, in front of
        // the half of the door the sled leaves open. Every PC plays it from when it saw the castling start (no clock on the
        // wire).
        void Castle(float t)
        {
            if (tower == null) return;
            if (castling && !wasCastling)
            {
                if (castleAt < 0f) castleAt = t;
                ArenaFx.Beam(World(Cell(CastleFrom)), 1.2f, 1.8f, 7f, CastleCrouch + CastleFlight, station.Accent(station.Team));
                tookOff = landed = false;
            }
            wasCastling = castling;
            var from = Cell(CastleFrom, .3f);
            if (!castling) { PoseTower(from, Quaternion.identity, Vector3.one, true); return; }
            float e = t - castleAt;
            var to = Cell(CastleTo);
            var dir = to - from;
            dir.y = 0f;
            var axis = Vector3.Cross(Vector3.up, dir.normalized);   // a positive turn round it tips the top the way on
            if (e < CastleCrouch)
            {
                float c = Mathf.SmoothStep(0f, 1f, e / CastleCrouch);
                PoseTower(from, Quaternion.AngleAxis(-8f * c, axis), new Vector3(1f + .1f * c, 1f - .18f * c, 1f + .1f * c), true);
                return;
            }
            if (e < CastleCrouch + CastleFlight)
            {
                float u = (e - CastleCrouch) / CastleFlight;
                if (!tookOff)
                {
                    tookOff = true;
                    ArenaFx.Sfx("sfx_castle_whoosh", World(from));
                }
                // On the flat a quadratic curve through the king's place at its middle; up and down a parabola.
                var king = SledAt(KingEscortPath.Length, out _);
                Vector3 p0 = Flat(from), p2 = Flat(to), bend = Flat(king) * 2f - (p0 + p2) * .5f;
                var p = Vector3.Lerp(Vector3.Lerp(p0, bend, u), Vector3.Lerp(bend, p2, u), u);
                p.y = Mathf.Lerp(from.y, to.y, u) + CastleApex * 4f * u * (1f - u);
                // out of the crouch's lean, then one somersault, upright again before it lands
                float turn = Mathf.Lerp(-8f, 0f, Mathf.Clamp01(u / .1f)) + 360f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.1f, .9f, u));
                var flip = Quaternion.AngleAxis(turn, axis);
                float stretch = u < .3f ? 1f - u / .3f : u > .8f ? (u - .8f) / .2f : 0f;
                PoseTower(p, flip, new Vector3(1f - .06f * stretch, 1f + .14f * stretch, 1f - .06f * stretch), false);
                return;
            }
            float a = e - CastleCrouch - CastleFlight;
            if (!landed)
            {
                landed = true;
                var at = World(to);
                ArenaFx.Ring(at, 0f, 2.5f, .6f, station.Accent(station.Team));
                ArenaFx.Burst(at + Vector3.up * .2f, new[] { station.Accent(station.Team), Arena.Gold, new Color(.85f, .8f, .7f) }, 24, 2.2f, 2.5f, .9f, .1f);
                ArenaFx.Sfx("sfx_castle_land", at);
            }
            // squashed flat on landing, springing back through a stretch in two bounces
            float squash = .22f * Mathf.Exp(-a / .12f) * Mathf.Cos(a * 2f * Mathf.PI / .18f);
            PoseTower(to, Quaternion.identity, new Vector3(1f + squash * .6f, 1f - squash, 1f + squash * .6f), true);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // The tower with its foot at `foot` (the game's frame), scaled from its foot. Standing it leans on its foot and shows
        // its floor band; flying it turns round its middle (`foot` is then where the foot would be, upright) without it.
        void PoseTower(Vector3 foot, Quaternion turn, Vector3 scale, bool standing)
        {
            tower.localRotation = turn;
            if (standing) tower.localPosition = foot;
            else
            {
                var middle = Vector3.up * (TowerMiddle * scale.y);
                tower.localPosition = foot + middle - turn * middle;
            }
            tower.localScale = scale;
            if (towerRing != null && towerRing.enabled != standing) towerRing.enabled = standing;
        }

        // ------------------------------------------------------------------ the wire (7 bytes)

        public override void WriteNet(ref NetBlock block)
        {
            block.U16(Mathf.Clamp(Mathf.RoundToInt(s * 1000f), 0, 65535));
            block.U8(Mathf.RoundToInt(rookD[0] * 100f));
            block.U8(Mathf.RoundToInt(rookD[1] * 100f));
            block.U8((garaged[0] ? 1 : 0) | (garaged[1] ? 2 : 0) | (castling ? 4 : 0) | (KingEscortPath.IsSand(s) ? 8 : 0));
            block.U8(Mathf.Min(15, pushers) | Mathf.Min(15, holders) << 4);
            block.U8(Mathf.Min(15, rookPushers[0]) | Mathf.Min(15, rookPushers[1]) << 4);
        }

        public override void ReadNet(ref NetBlock block)
        {
            s = block.ReadU16() / 1000f;
            rookD[0] = block.ReadU8() / 100f;
            rookD[1] = block.ReadU8() / 100f;
            int flags = block.ReadU8();
            garaged[0] = (flags & 1) != 0;
            garaged[1] = (flags & 2) != 0;
            bool castle = (flags & 4) != 0;
            if (castle && !castling) castleAt = Time.time;
            castling = castle;
            int who = block.ReadU8();
            pushers = who & 15;
            holders = who >> 4;
            int rp = block.ReadU8();
            rookPushers[0] = rp & 15;
            rookPushers[1] = rp >> 4;
        }

        public override void ResetGame()
        {
            base.ResetGame();
            s = shownS = lastS = 0f;
            for (int k = 0; k < 2; k++)
            {
                rookD[k] = shownRook[k] = lastRook[k] = 0f;
                garaged[k] = wasGaraged[k] = wasBlocked[k] = false;
                popAt[k] = -1f;
                rookPushers[k] = 0;
            }
            castling = wasCastling = tookOff = landed = false;
            castleAt = -1f;
            pushers = holders = 0;
            if (sled != null) Place(0f, true);
            if (tower != null) PoseTower(Cell(CastleFrom, .3f), Quaternion.identity, Vector3.one, true);
        }
    }
}
