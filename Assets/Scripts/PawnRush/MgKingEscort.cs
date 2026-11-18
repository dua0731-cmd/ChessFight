using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // G, king escort (mini-games v1.0 §5, reworked in R128): our king on a square sled is pushed and pulled along a road
    // of board squares (KingRoad, d1 to e8 in front of the gate); the other side's rooks stand in the road until they
    // are pushed or dragged into their garages; with the sled on e8 the gate opens. R524: the road ends on e8, the sled
    // whole on the board (a gold frame shows where it stops); it used to run on into the team door's tunnel, so the
    // sled had to be pushed into the north wall.
    //
    // R128 (user, after playing it): no ropes and no gold handle blocks. The ropes left no room by the walls, the
    // small blocks were hard to catch, grab + forward climbed them, and turning the sled pulled the grips off. Now
    // the king statue and the rooks are themselves the things to move: a pawn holds them (any side) or presses on
    // them from behind and moves the way the thing has to go (MiniGameParts.Shove); the sled never turns - at a
    // corner the team moves round to push it the new way. They are GrabOnlySurface: held by hand, never climbed.
    //
    // R543 (user 11-11: "킹 호위는 룩 탑은 제거해주고(왜 있던건지 모르겠음) 킹이 움직이는 속도(무게)와 길의 거리, 장애물 등을
    // 적절히 조절해줘. 그리고 매번 똑같은 패턴의 맵이 안나오게 해줘"):
    //   - our rook tower on g8 and its castling jump over the king are gone: the sled on the gold frame is the end;
    //   - the road is drawn anew for every game from a seed (KingRoad.Generate): 11, 13, 15 or 17 squares for three to
    //     six a side, with 2 to 5 squares of sand and 1 to 3 rooks in the way;
    //   - the sled's weight goes with the team: its whole crew (3, 4, 4 and 5 pushers) moves it at full speed.
    // The seed is drawn where the game is built: a section's games are built from the section's seed on every PC, so
    // every PC and both teams draw the same road. The host sends the seed anyway, and a PC that drew another builds
    // the host's.
    //
    // Physics and the network (§1.2): the sled is one distance s along the road, worked out on the host; the sled
    // and the rooks are kinematic bodies moved to the host's numbers, and a hand that holds one is dragged with
    // it. The host sends the seed, s, the rooks' drag, flags and how many push each way (11 bytes); a client eases to
    // them and plays every moment from what changed.
    public sealed class MgKingEscort : MiniGameBase
    {
        public const float SledSize = 1.6f, SledHalf = SledSize * .5f, RookHalf = .45f;
        public const float PathCell = 2.22f;
        const int MaxRooks = KingRoad.MaxRooks;

        [SerializeField] MissionStation station;

        // This game's road and the seed it was drawn from.
        KingRoad road = KingRoad.Baseline;
        int seed = -1, crew = PawnRushMissions.WorkerCap;
        Transform board;

        public KingRoad Road => road;
        public int Seed => seed;

        Rigidbody sled;
        Transform sledLook;
        LineRenderer sledRing;
        readonly Rigidbody[] rooks = new Rigidbody[MaxRooks];
        readonly Renderer[] rookRings = new Renderer[MaxRooks];
        readonly Transform[] stops = new Transform[MaxRooks];
        Material ringPlain, ringWarn;

        // Host state (a client's copy from the wire).
        float s;
        readonly float[] rookD = new float[MaxRooks];
        readonly bool[] garaged = new bool[MaxRooks];
        int pushers, holders;                  // ours moving the sled on, theirs holding it back
        readonly int[] rookPushers = new int[MaxRooks];
        readonly float[] rookN = new float[MaxRooks];

        // What is drawn (eased on a client) and the moments already played.
        float shownS;
        readonly float[] shownRook = new float[MaxRooks];
        float lastS, dustAt, sandAt, bumpAt = -1f, bumpFxAt = -10f;
        readonly float[] lastRook = new float[MaxRooks];
        readonly bool[] wasGaraged = new bool[MaxRooks], wasBlocked = new bool[MaxRooks];
        readonly float[] popAt = { -1f, -1f, -1f };
        bool arrived;
        // R688 (seoungtest, 승규: "8번 stop하면 소리가 갑자기 멈춰 자연스럽게 이어"): the drag loops ease out (FadingLoop)
        // instead of being cut dead the frame the sled stops, and a step's flicker no longer cuts and restarts them.
        readonly FadingLoop drag = new FadingLoop(), sand = new FadingLoop(), stone = new FadingLoop();

        // R688 (seoungtest, 승규 님 11-17: "8번 9번 0번 vfx 가 필요해 아직 작업 x"): the room's own effects
        // (MiniGameProcessionFx: dust, sand grains, marble chips, brass sparks, gold glints) in place of ArenaFx's bursts
        // and ring; added on first use, so the saved prefab carries nothing new. Sounds stay ArenaFx's.
        MiniGameProcessionFx fx;
        MiniGameProcessionFx Fx => fx != null ? fx : (fx = gameObject.AddComponent<MiniGameProcessionFx>());
        // A steadier read of the sled's motion for the pictures: s moves in FixedUpdate, so a frame's own step is often
        // nought; the sled counts as rolling for 0.15 s after its last step.
        float fxS, fxT, fxSpeed, rollAt = -10f, stillAt = -10f;
        bool rolling;
        readonly float[] grindAt = new float[MaxRooks];

        public float Distance => s;

        public static MgKingEscort Build(CourseBuilder b, MissionStation station)
        {
            // First of all, before anything else takes from the stream: a section's builder has seeded it with the
            // section's seed, so every PC and both of its stations draw the same number here.
            int seed = Random.Range(0, 65536);
            MissionGames.Floor(b, 0f, 20f);
            var game = b.parent.gameObject.AddComponent<MgKingEscort>();
            game.station = station;
            game.Use(seed);
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
            var from = Cell(road.RookCells[k]);
            var to = Cell(road.RookGarages[k]);
            return (to - from).normalized;
        }

        // ------------------------------------------------------------------ the road from the seed

        // Builds the road of this seed for the station's team size in place of the one standing, back at its start.
        void Use(int newSeed)
        {
            seed = newSeed;
            int size = station != null ? station.TeamSize : MiniGameScale.Baseline;
            road = KingRoad.Generate(seed, size);
            crew = MiniGameScale.KingSledCrew(size);
            s = shownS = lastS = 0f;
            for (int k = 0; k < MaxRooks; k++)
            {
                rookD[k] = shownRook[k] = lastRook[k] = 0f;
                garaged[k] = wasGaraged[k] = wasBlocked[k] = false;
                popAt[k] = -1f;
                rookPushers[k] = 0;
                rooks[k] = null;
                rookRings[k] = null;
                stops[k] = null;
            }
            pushers = holders = 0;
            arrived = false;
            bumpAt = -1f;
            ResetFx();
            CutSounds();
            if (board != null) DestroyImmediate(board.gameObject);
            board = new GameObject("Road " + seed).transform;
            board.SetParent(transform, false);
            Make();
        }

        void Make()
        {
            var lane = new GameObject("Road").transform;
            lane.SetParent(board, false);
            var cells = road.Cells;
            var sandTop = Arena.Lit("M_Sand", Arena.Hex("#D9C07A"), 0f, 1f);
            var ripple = Arena.Lit("M_SandRipple", Arena.Hex("#BF9F55"), 0f, 1f);
            var curb = Arena.Lit("M_Curb", Arena.Hex("#CFC5B3"), 0f, .8f);
            var gold = Arena.Emissive(Arena.Gold, .5f);
            float h = PathCell * .5f;
            for (int i = 0; i < cells.Length; i++)
            {
                PawnRushBoard.Parse(cells[i], out int f, out int r);
                var c = Cell(cells[i]);
                bool isSand = road.IsSandCell(i);
                Arena.Picture(lane, "Road " + cells[i], c + new Vector3(-h, 0f, -h), c + new Vector3(h, .08f, h), isSand ? sandTop : Arena.Wood(PawnRushBoard.IsLight(f, r)));
                if (isSand)
                    for (int w = -1; w <= 1; w++)
                        Arena.Picture(lane, "Sand ripple", c + new Vector3(-h + .2f, .08f, w * .6f - .04f), c + new Vector3(h - .2f, .09f, w * .6f + .04f), ripple);
                // Curbs on the outer sides: not toward the road's neighbours, d1's south (the start) or a rook square's
                // garage side. The end, e8, is closed all round (R524: it was open north, the road going on to the gate).
                for (int side = 0; side < 4; side++)
                {
                    int df = side == 0 ? 0 : side == 1 ? 1 : side == 2 ? 0 : -1, dr = side == 0 ? 1 : side == 1 ? 0 : side == 2 ? -1 : 0;
                    string next = PawnRushBoard.OnBoard(f + df, r + dr) ? PawnRushBoard.Name(f + df, r + dr) : "";
                    if (i > 0 && cells[i - 1] == next || i < cells.Length - 1 && cells[i + 1] == next) continue;
                    if (i == 0 && side == 2) continue;
                    int rook = System.Array.IndexOf(road.RookCells, cells[i]);
                    if (rook >= 0 && road.RookGarages[rook] == next) continue;
                    var n = MiniGameParts.Local(station, new StationPoint(df, dr)) - MiniGameParts.Local(station, new StationPoint(0f, 0f));
                    var along = new Vector3(Mathf.Abs(n.z), 0f, Mathf.Abs(n.x));
                    // R498: the curb's outer face stands a centimetre past the road square's, not in its plane.
                    var mid = c + n * (h - .09f + MissionGames.Proud * .5f);
                    var half = along * h + new Vector3(Mathf.Abs(n.x), 0f, Mathf.Abs(n.z)) * (.09f + MissionGames.Proud * .5f);
                    Arena.Picture(lane, "Curb", mid - half, mid + half + Vector3.up * .16f, curb);
                }
                // A corner: a gold ring and an arrow the way the road goes on (the king does not turn; it is pushed
                // the new way from its other side).
                if (road.Turns(i))
                {
                    // R688 (seoungtest, 승규 with a shot of it: "8번 ... 사진 효과 교체"): the flat yellow line ring and
                    // the two flat bars became a bevelled brass ring and chevron inlaid in the road, a warm light round them
                    MiniGameMarks.Ring(lane, "Corner " + cells[i], c + Vector3.up * .08f, .9f, .16f);
                    var p = road.Points;
                    var to = Dir(p[i + 1].V - p[i].V, p[i + 1].U - p[i].U);
                    float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    // 2nd (승규, a shot of the dim little chevron on the dark wood: "화살표 잘 보이는 걸로 만들어"): two
                    // bigger, thicker chevrons one behind the other ("»" the way on) with a steady warm glow of their own
                    // (not enough to bloom over the screen), so they read on the dark wood as well as on the sand
                    foreach (float along in new[] { -.24f, .2f })
                    {
                        var arrow = MiniGameMarks.Chevron(lane, "Corner arrow", c + Vector3.up * .08f + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, along),
                                                          .42f, yaw, null, .4f);
                        var shine = arrow.GetComponent<MarkGlow>();
                        if (shine != null) shine.Boost = along > 0f ? .5f : .35f;
                    }
                }
                // The end (R524): a gold frame just round the sled's footprint, where it stops and the gate opens. The
                // bars across run between the bars along, so no two tops overlap.
                if (i == cells.Length - 1)
                {
                    const float edge = SledHalf + .1f, bar = .05f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Arena.Picture(lane, "Goal frame", c + new Vector3(-edge - bar, .081f, side * edge - bar), c + new Vector3(edge + bar, .1f, side * edge + bar), gold);
                        Arena.Picture(lane, "Goal frame", c + new Vector3(side * edge - bar, .081f, -edge + bar), c + new Vector3(side * edge + bar, .1f, edge - bar), gold);
                    }
                }
            }
            // The start pad south of the first square.
            var first = Cell(cells[0]);
            Arena.Picture(lane, "Start pad", new Vector3(first.x - 1.5f, 0f, .8f), new Vector3(first.x + 1.5f, .06f, 2f), Arena.Lit("M_StartPad", Arena.Hex("#5B6170"), 0f, .8f));

            // The rooks in the way (grab-only: held and pushed, never climbed), their grooves, garages and gold stops.
            var them = Opponent;
            ringPlain = Arena.Lit("M_PieceRing", station.Accent(them), .4f, .5f);
            ringWarn = Arena.Lit("M_PieceRingWarn", Arena.Hex("#F08A2C"), .9f, .5f);
            var garageFloor = Arena.Lit("M_Garage", Arena.Hex("#2F333D"), 0f, .8f);
            var grooveMat = Arena.Lit("M_Groove", Arena.Hex("#1D2028"), 0f, .8f);
            for (int k = 0; k < road.Rooks; k++)
            {
                var from = Cell(road.RookCells[k]);
                var garage = Cell(road.RookGarages[k]);
                var g = Groove(k);
                var across = new Vector3(g.z, 0f, g.x);
                Arena.Picture(lane, "Garage", garage + new Vector3(-h, 0f, -h), garage + new Vector3(h, .05f, h), garageFloor);
                var mid = (from + garage) * .5f;
                var half = g * (KingEscortPath.RookDrag * .5f) + across * .2f;
                Arena.Picture(lane, "Rook groove", mid - Abs(half), mid + Abs(half) + Vector3.up * .09f, grooveMat);
                var stop = Arena.Picture(lane, "Rook stop", -Abs(across * .8f + g * .07f), Abs(across * .8f + g * .07f) + Vector3.up * .2f, gold);
                stop.transform.localPosition = garage + g * (h - .1f);
                stops[k] = stop.transform;
                var rook = MiniGameParts.Piece(board, "Rook in the way " + (k + 1), PieceKind.Rook, 1.6f, MiniGameParts.PieceMaterial(them), station.Accent(them), true, .4f, grabbable: true);
                rook.transform.localPosition = from;
                rooks[k] = rook.GetComponent<Rigidbody>();
                foreach (var r in rook.GetComponentsInChildren<Renderer>()) if (r.name == "Ring") rookRings[k] = r;
            }

            // The sled: a square walnut base on runners with our king on it, one kinematic body whose colliders (the
            // base, and the king at hand height) are held by hand or pushed, never climbed. It never turns.
            var sledGo = new GameObject("King sled");
            sledGo.transform.SetParent(board, false);
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
            var p = road.Position(at, out float dv, out float du);
            dir = Dir(dv, du);
            return MiniGameParts.Local(station, p);
        }

        Vector3 RookAt(int k, float d) => Cell(road.RookCells[k]) + Groove(k) * d;

        // The sled and the rooks where the numbers put them (kinematic moves, or set outright).
        void Place(float at, bool snap)
        {
            var pos = World(SledAt(at, out _));
            if (snap) sled.transform.SetPositionAndRotation(pos, transform.rotation);
            else { sled.MovePosition(pos); sled.MoveRotation(transform.rotation); }
            for (int k = 0; k < road.Rooks; k++)
            {
                if (rooks[k] == null) continue;
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
                for (int k = 0; k < road.Rooks; k++) shownRook[k] = Mathf.MoveTowards(shownRook[k], rookD[k], dt * 1.6f);
            }
            else
            {
                shownS = s;
                for (int k = 0; k < road.Rooks; k++) shownRook[k] = rookD[k];
            }
            Place(shownS, false);
        }

        void Step(float dt)
        {
            var at = SledAt(s, out var dir);
            int n = 0, e = 0, count = road.Rooks;
            for (int k = 0; k < count; k++) { rookN[k] = 0f; rookPushers[k] = 0; }
            foreach (var p in station.Pawns())
            {
                int on = MiniGameParts.Shove(p, transform, sled, at, dir, SledHalf);
                if (p.Own && on > 0) n++;
                else if (!p.Own && on < 0) e++;
                for (int k = 0; k < count; k++)
                {
                    if (garaged[k]) continue;
                    int r = MiniGameParts.Shove(p, transform, rooks[k], RookAt(k, rookD[k]), Groove(k), RookHalf);
                    if (p.Own && r > 0) { rookN[k] += 1f; rookPushers[k]++; }
                    else if (!p.Own && r < 0) rookN[k] -= .5f;
                }
            }
            // R543: the sled's crew is as many as the team's size asks for (three to five), and no more count.
            pushers = Mathf.Min(crew, n);
            holders = e;

            // The rooks: 0.6 m/s for one, 1.1 for two or more, into the garage and latched there.
            for (int k = 0; k < count; k++)
            {
                if (garaged[k]) continue;
                float r = rookN[k], v = r <= 0f ? 0f : r < 1f ? KingEscortPath.RookDragOne * r : Mathf.Min(KingEscortPath.RookDragTwo, KingEscortPath.RookDragOne + (KingEscortPath.RookDragTwo - KingEscortPath.RookDragOne) * (r - 1f));
                rookD[k] = Mathf.Min(KingEscortPath.RookDrag, rookD[k] + v * dt);
                if (rookD[k] >= KingEscortPath.RookDrag - 1e-4f) garaged[k] = true;
            }

            // The sled: the speed table for its crew's share pushing (theirs each take half a pusher off), slower on
            // sand, never back, held at a rook.
            float speed = KingEscortPath.Speed(pushers, holders, road.IsSand(s), crew);
            if (speed > 0f)
            {
                float next = s + speed * dt;
                for (int k = 0; k < count; k++)
                    if (!garaged[k]) next = Mathf.Min(next, road.RookStop(k));
                s = Mathf.Max(s, Mathf.Min(road.Length, next));
            }
            Progress01 = Mathf.Min(.999f, s / road.Length);
            // R543: on the gold frame the escort is done (no castling: our rook tower is gone).
            if (s >= road.Length - 1e-3f) Complete();
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
            bool onSand = road.IsSand(shownS);

            // The push ring: ours while pushed on, theirs blinking while held back.
            bool held = holders > 0 && Mathf.Repeat(t * 3f, 1f) < .5f;
            sledRing.enabled = !Completed && (pushers > 0 || held);
            var rc = held ? station.Accent(Opponent) : station.Accent(station.Team);
            sledRing.startColor = sledRing.endColor = rc;

            // Moving: dust behind (sand: more, and sand-coloured), the drag loop pitched by speed.
            SledAt(shownS, out var dir);
            var fwd = transform.TransformDirection(dir);
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : transform.forward;
            var behind = sledAt - fwd * (SledHalf + .05f);
            // R688: the motion the pictures read (see fxS), and the sled breaking loose after standing a while.
            if (shownS > fxS + 1e-5f)
            {
                fxSpeed = (shownS - fxS) / Mathf.Max(1e-3f, t - fxT);
                fxS = shownS;
                fxT = t;
                if (fxSpeed > .05f) rollAt = t;
            }
            else if (shownS < fxS - 1e-5f) { fxS = shownS; fxT = t; }
            bool wasRolling = rolling;
            rolling = !Completed && t - rollAt < .15f;
            if (rolling && !wasRolling && t - stillAt >= .4f) Fx.Breakaway(sledAt, fwd, SledHalf, onSand);
            if (!rolling && wasRolling) stillAt = t;
            // R688: ArenaFx.Burst's cubes gave way to a soft dust trail off the runners' tails, grains kicked up on sand.
            if (rolling && !onSand && t - dustAt >= .2f)
            {
                dustAt = t;
                Fx.Scrape(behind, -fwd, false, Mathf.Clamp01(fxSpeed / 1.1f));
            }
            if (rolling && onSand && t - sandAt >= .1f)
            {
                sandAt = t;
                Fx.Scrape(behind, -fwd, true, Mathf.Clamp01(fxSpeed / 1.1f));
            }
            bool sounding = (moving || rolling) && !Completed;
            drag.Tick(sounding, "sfx_sled_drag", sledAt, t, moving ? Mathf.Lerp(.8f, 1.2f, Mathf.Clamp01(speed / 1.1f)) : -1f);
            sand.Tick(sounding && onSand, "sfx_sand_crunch", sledAt, t);

            // The rooks: stopped in front of one, pushing one, one in its garage.
            bool rookMoving = false, atRook = false;
            Vector3 movingAt = sledAt;
            for (int k = 0; k < road.Rooks; k++)
            {
                if (rooks[k] == null) continue;
                bool g = shownRook[k] >= KingEscortPath.RookDrag - .01f;
                bool blocked = !g && !moving && Mathf.Abs(shownS - road.RookStop(k)) < .02f;
                if (!g && Mathf.Abs(shownS - road.RookStop(k)) < .05f) atRook = true;
                if (blocked && !wasBlocked[k])
                {
                    bumpAt = t;
                    ArenaFx.Sfx("sfx_sled_blocked", sledAt);
                    // R688: the sled's front against the rook - brass sparks, marble chips. Once per 1.5 s at most
                    // (승규, a shot of a white blow-out in 8: "빛이 너무 강해" - `moving` flickers frame to frame while the
                    // team pushes on the rook, so `blocked` came back on and on and the flashes piled up).
                    if (t - bumpFxAt >= 1.5f)
                    {
                        bumpFxAt = t;
                        Fx.Bump(sledAt + fwd * (SledHalf + .05f), fwd);
                    }
                }
                wasBlocked[k] = blocked;
                var ring = blocked && Mathf.Repeat(t / .5f, 1f) < .5f ? ringWarn : ringPlain;
                if (rookRings[k] != null && rookRings[k].sharedMaterial != ring) rookRings[k].sharedMaterial = ring;
                float rv = Time.deltaTime > 0f ? (shownRook[k] - lastRook[k]) / Time.deltaTime : 0f;
                lastRook[k] = shownRook[k];
                if (rv > .05f)
                {
                    rookMoving = true;
                    movingAt = rooks[k].transform.position;
                    // R688: stone dust at its foot and marble grit skipping off, a few times a second.
                    if (t - grindAt[k] >= .14f)
                    {
                        grindAt[k] = t;
                        Fx.Grind(rooks[k].transform.position, transform.TransformDirection(Groove(k)));
                    }
                }
                if (g && !wasGaraged[k])
                {
                    popAt[k] = t;
                    var at = World(stops[k].localPosition);
                    // R688: no gold ring on the floor ("원은 사용 하지 않는 vfx") and no cube burst: a gold flash on the
                    // brass stop, brass sparks, the garage's dust shoved out, gold glints.
                    Fx.Latch(at, transform.TransformDirection(Groove(k)));
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
            stone.Tick(rookMoving, "sfx_stone_drag", movingAt, t);

            // R688: the sled coming to rest on the open road (not at a rook: that is the bump; not on the frame: arrive).
            if (!rolling && wasRolling && !Completed && !atRook) Fx.Settle(sledAt + fwd * SledHalf, fwd, onSand);

            // A bump when the sled hits a rook's stop: a 0.1 s jolt of its look.
            float bump = (t - bumpAt) / .1f;
            sledLook.localPosition = bumpAt >= 0f && bump < 1f ? new Vector3(0f, .04f * Mathf.Sin(bump * Mathf.PI), 0f) : Vector3.zero;

            // The sled on its frame: its landing sound (every PC plays it when it sees the game done). R643: the team's
            // ring and burst round it went (승규 님 11-14, "8번 아직 파란색 폭죽 터지고"); the door's banners are the moment
            // (MiniGameProcessionGateFx).
            if (Completed && !arrived)
            {
                arrived = true;
                var end = World(SledAt(road.Length, out _));
                ArenaFx.Sfx("sfx_castle_land", end);
                // R688: no team colour (R643); its dust settling round it and gold glints off the frame's corners.
                Fx.Arrive(end, fwd, SledHalf + .1f, road.IsSand(road.Length - 1e-3f));
            }
            if (!Completed) arrived = false;
        }

        // R675: the drag loops run until cut, so a game taken away while one plays stops it here.
        void OnDisable() => CutSounds();

        void CutSounds()
        {
            drag.Stop();
            sand.Stop();
            stone.Stop();
        }

        // ------------------------------------------------------------------ the wire (11 bytes)

        public override void WriteNet(ref NetBlock block)
        {
            block.U16(seed < 0 ? 0 : seed);
            block.U16(Mathf.Clamp(Mathf.RoundToInt(s * 1000f), 0, 65535));
            int flags = (seed >= 0 ? 0x80 : 0) | (road.IsSand(s) ? 8 : 0), who = 0;
            for (int k = 0; k < MaxRooks; k++)
            {
                block.U8(Mathf.RoundToInt(rookD[k] * 100f));
                if (garaged[k]) flags |= 1 << k;
                who |= Mathf.Min(15, rookPushers[k]) << (4 * k);
            }
            block.U8(flags);
            block.U8(Mathf.Min(15, pushers) | Mathf.Min(15, holders) << 4);
            block.U16(who);
        }

        public override void ReadNet(ref NetBlock block)
        {
            int hostSeed = block.ReadU16();
            float hostS = block.ReadU16() / 1000f;
            int d0 = block.ReadU8(), d1 = block.ReadU8(), d2 = block.ReadU8();
            int flags = block.ReadU8();
            int who = block.ReadU8();
            int rp = block.ReadU16();
            if ((flags & 0x80) == 0) return;
            // Another road than the host's (a station whose game was not drawn from a shared seed: course 01): the
            // host's is built in its place, and the pictures that ride it are dressed again.
            if (hostSeed != seed)
            {
                Use(hostSeed);
                Redress();
            }
            s = hostS;
            rookD[0] = d0 / 100f;
            rookD[1] = d1 / 100f;
            rookD[2] = d2 / 100f;
            for (int k = 0; k < MaxRooks; k++)
            {
                garaged[k] = (flags & 1 << k) != 0;
                rookPushers[k] = rp >> (4 * k) & 15;
            }
            pushers = who & 15;
            holders = who >> 4;
        }

        // The palace pictures of a road built after the section was dressed (MiniGamePalaceArt's decoration follows the
        // road it was made for; the models are hung again where they are missing).
        void Redress()
        {
            if (station == null || !station.PalaceStyle) return;
            var old = transform.Find(MiniGamePalaceArt.DressName);
            if (old != null) DestroyImmediate(old.gameObject);
            MiniGamePalaceArt.Dress(station, PawnRushMissions.KingEscort);
            var section = station.GetComponentInParent<MiniGameSection>(true);
            if (section != null)
            {
                MiniGameModels.Apply(section);
                MiniGameSectionBuilder.RefreshText(section);
            }
        }

        public override void ResetGame()
        {
            base.ResetGame();
            s = shownS = lastS = 0f;
            for (int k = 0; k < MaxRooks; k++)
            {
                rookD[k] = shownRook[k] = lastRook[k] = 0f;
                garaged[k] = wasGaraged[k] = wasBlocked[k] = false;
                popAt[k] = -1f;
                rookPushers[k] = 0;
            }
            arrived = false;
            pushers = holders = 0;
            ResetFx();
            if (sled != null) Place(0f, true);
        }

        // R688: the pictures' read of the sled's motion back at the start (no break-loose puff from a reset).
        void ResetFx()
        {
            fxS = fxSpeed = 0f;
            fxT = Time.time;
            rolling = false;
            rollAt = stillAt = -10f;
            for (int k = 0; k < MaxRooks; k++) grindAt[k] = -1f;
        }
    }

    // R688 (seoungtest, 승규: "8번 stop하면 소리가 갑자기 멈춰 자연스럽게 이어"): a looping ArenaFx voice that keeps going
    // Hold seconds after its cause stops (a step's flicker never cuts it), then eases out over Fade seconds, its
    // pitch sagging a little as it goes, the way a sled grinding to a halt sounds; back on while fading, it swells up
    // again without a restart. Stop() still cuts at once (the game taken away).
    sealed class FadingLoop
    {
        const float Hold = .2f, Fade = .45f;
        AudioSource voice;
        float baseVolume, basePitch = 1f, lastOn = -10f;

        public void Tick(bool on, string name, Vector3 at, float t, float pitch = -1f)
        {
            if (on)
            {
                lastOn = t;
                if (voice == null || !voice.isPlaying)
                {
                    voice = ArenaFx.Hold(name, at, 1f, .6f, loop: true);
                    if (voice != null) { baseVolume = voice.volume; basePitch = voice.pitch; }
                }
                if (pitch > 0f) basePitch = pitch;
            }
            if (voice == null) return;
            voice.transform.position = at;
            float off = t - lastOn - Hold;
            float k = off <= 0f ? 1f : 1f - off / Fade;
            if (k <= 0f) { Stop(); return; }
            voice.volume = baseVolume * k * k;
            voice.pitch = basePitch * (.85f + .15f * k);
        }

        public void Stop()
        {
            ArenaFx.Cut(voice);
            voice = null;
            lastOn = -10f;
        }
    }
}
