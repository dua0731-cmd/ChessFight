using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // I, check block (mini-games v1.0 §7): our king on rank 7 is in check from three to five sides, a different
    // position every round (CheckBlockGenerator, from a 2-byte seed). A rook's, bishop's or queen's line is blocked
    // by pushing (or pulling by its handle) a shield cart along its rail onto the square between; a knight is
    // dragged off by the ring on its neck; a pawn is taken by a dive. With all that done a hidden double check
    // rises, and the king is dragged on its base to the safe square. Every check solved: the gate opens.
    //
    // Network (§7.7): the host draws the seed and sends it with each situation's progress, the solved bits and the
    // king's drag (10 bytes at most). A client builds the same position from the seed the moment it arrives, eases
    // its carts and pieces to the progress, and plays the moments from the bits. Everything that moves is
    // kinematic and moved by those numbers.
    public sealed class MgCheckBlock : MiniGameBase, IProgressCount
    {
        public const float KnightDrag = 2.25f, Reach = .8f, Speed = 5f, Cart = 1.25f, PushDepth = 1.2f, BeamHeight = 1.1f;
        const int MaxSituations = 6;

        [SerializeField] MissionStation station;

        CheckBlockGenerator.Layout layout;
        int seed = -1;
        Transform root;

        readonly float[] progress = new float[MaxSituations], shown = new float[MaxSituations];
        int solvedBits, lastSolved;
        bool doubleUp, doubleDone, wasDoubleUp, wasDoubleDone;
        float kingDrag, kingShown, doubleAt = -1f;
        readonly float[] solvedAt = new float[MaxSituations], lastShown = new float[MaxSituations];

        // Per situation: the thing that moves (cart, knight, pawn), its handle, the attack beam.
        readonly Rigidbody[] movers = new Rigidbody[MaxSituations];
        readonly Transform[] moverLooks = new Transform[MaxSituations];
        readonly GrabHandle[] handles = new GrabHandle[MaxSituations];
        readonly Rigidbody[] handleBodies = new Rigidbody[MaxSituations];
        readonly Transform[] beams = new Transform[MaxSituations];
        readonly Renderer[] beamLooks = new Renderer[MaxSituations];
        readonly AudioSource[] hums = new AudioSource[MaxSituations + 2];
        Rigidbody kingBase;
        GrabHandle kingRing;
        Rigidbody kingRingBody;
        LineRenderer checkRing;
        readonly Transform[] doublePieces = new Transform[2];
        readonly Transform[] doubleBeams = new Transform[2];
        GameObject escapePlate;
        Material beamMat, beamGrey, beamRed;
        float sparkAt;
        AudioSource roll;

        public int Done
        {
            get
            {
                int n = 0;
                if (layout == null) return 0;
                for (int i = 0; i < layout.Situations.Count; i++) if ((solvedBits >> i & 1) != 0) n++;
                return n + (doubleDone ? 1 : 0);
            }
        }

        public int Total => layout == null ? 0 : layout.Situations.Count + (layout.Double ? 1 : 0);
        public int Seed => seed;

        public static MgCheckBlock Build(CourseBuilder b, MissionStation station)
        {
            MissionGames.Floor(b, 0f, 20f);
            // The board in low contrast (§7.1), so the beams and pieces stand out.
            var light = Arena.Lit("M_CheckBoardLight", Arena.Hex("#CBBF9F"), 0f, .7f);
            var dark = Arena.Lit("M_CheckBoardDark", Arena.Hex("#A79A7C"), 0f, .7f);
            var board = b.Group("Board");
            float h = PawnRushBoard.Cell * .5f;
            for (int s = 0; s < 64; s++)
            {
                int f = PawnRushBoard.FileOf(s), r = PawnRushBoard.RankOf(s);
                var c = MiniGameParts.Local(station, PawnRushBoard.Center(f, r));
                Arena.Picture(board, "Square " + PawnRushBoard.Name(f, r), c + new Vector3(-h, 0f, -h), c + new Vector3(h, .04f, h), PawnRushBoard.IsLight(f, r) ? light : dark);
            }
            var game = b.parent.gameObject.AddComponent<MgCheckBlock>();
            game.station = station;
            // The host (or offline) draws the round's position now; a client waits for the host's seed.
            if (!Remote) game.Use(Random.Range(0, 65536));
            return game;
        }

        int Opponent => station.Team == Teams.White ? Teams.Black : Teams.White;

        Vector3 At(CheckBlockGenerator.Square q, float y = 0f) => MiniGameParts.Local(station, PawnRushBoard.Center(q.File, q.Rank), y);

        Vector3 World(Vector3 local) => transform.TransformPoint(local);

        // ------------------------------------------------------------------ the position from the seed

        void Use(int newSeed)
        {
            seed = newSeed;
            layout = CheckBlockGenerator.Generate(seed);
            for (int i = 0; i < MaxSituations; i++) { progress[i] = shown[i] = 0f; solvedAt[i] = -10f; }
            solvedBits = lastSolved = 0;
            doubleUp = doubleDone = wasDoubleUp = wasDoubleDone = false;
            kingDrag = kingShown = 0f;
            foreach (var h in hums) ArenaFx.Cut(h);
            if (root != null) DestroyImmediate(root.gameObject);
            root = new GameObject("Position " + seed).transform;
            root.SetParent(transform, false);
            Make();
        }

        void Make()
        {
            var them = Opponent;
            var themMat = MiniGameParts.PieceMaterial(them);
            var usMat = MiniGameParts.PieceMaterial(station.Team);
            var ring = Arena.TeamColor(them);
            beamMat = Arena.Lit("M_AttackBeam", Arena.TeamColor(them), 1.2f, .3f);
            beamGrey = Arena.Lit("M_AttackBeamOff", Arena.Hex("#7A7067"), .2f, .6f);
            beamRed = Arena.Lit("M_AttackBeamDouble", Arena.Red, 1.2f, .3f);
            var railFloor = Arena.Lit("M_RailFloor", Arena.Hex("#6A6152"), 0f, .8f);
            var groove = Arena.Lit("M_Groove", Arena.Hex("#1D2028"), 0f, .8f);

            // Our king on its base, with the red check ring round it and the base's gold ring (the double check's handle).
            var kingAt = At(layout.King);
            var baseGo = new GameObject("King base");
            baseGo.transform.SetParent(root, false);
            baseGo.transform.localPosition = kingAt;
            kingBase = baseGo.AddComponent<Rigidbody>();
            kingBase.isKinematic = true;
            kingBase.useGravity = false;
            kingBase.interpolation = RigidbodyInterpolation.Interpolate;
            var col = baseGo.AddComponent<CapsuleCollider>();
            col.radius = .9f;
            col.height = 2.25f;
            col.center = new Vector3(0f, 1.125f, 0f);
            baseGo.AddComponent<NoClimbSurface>();
            MiniGameParts.Part(baseGo.transform, PrimitiveType.Cylinder, new Vector3(0f, .175f, 0f), new Vector3(1.8f, .175f, 1.8f), Arena.Lit("M_PillarStone", Arena.Hex("#857E74"), 0f, .9f));
            MiniGameParts.Piece(baseGo.transform, "Our king", PieceKind.King, 1.9f, usMat, Arena.TeamColor(station.Team), false).transform.localPosition = new Vector3(0f, .35f, 0f);
            checkRing = MiniGameParts.Loop(baseGo.transform, "Check ring", new Vector3(0f, .05f, 0f), 1.3f, .3f, Arena.Red, true, 48);
            kingRing = MiniGameParts.Handle(root, "King ring", kingAt + Vector3.up * .3f, new Vector3(1.95f, .1f, 1.95f), this, 7, GrabHandle.HandleKind.Drag);
            kingRingBody = kingRing.GetComponent<Rigidbody>();

            var list = layout.Situations;
            for (int i = 0; i < list.Count && i < MaxSituations; i++)
            {
                var s = list[i];
                var from = At(s.From);
                float tall = s.Attacker == PieceKind.Pawn ? 1f : 1.5f;
                bool solid = true;
                var attacker = MiniGameParts.Piece(root, "Attacker " + (i + 1), s.Attacker, tall, themMat, ring, solid, .4f);
                attacker.transform.localPosition = from;
                Face(attacker.transform, kingAt);
                beams[i] = Beam("Beam " + (i + 1), s.Attacker == PieceKind.Queen ? .34f : .12f, out beamLooks[i]);
                switch (s.Kind)
                {
                    case CheckBlockGenerator.Kind.Rail:
                    {
                        // The rail: dark squares under the start, the middle and the square to fill, a groove, a gold
                        // diamond round the square to fill.
                        foreach (var q in new[] { s.RailStart, s.RailMid, s.Block })
                        {
                            var c = At(q);
                            Arena.Picture(root, "Rail square", c + new Vector3(-1.025f, 0f, -1.025f), c + new Vector3(1.025f, .05f, 1.025f), railFloor);
                        }
                        var a = At(s.RailStart);
                        var z = At(s.Block);
                        var mid = (a + z) * .5f;
                        var line = Arena.Picture(root, "Rail groove", new Vector3(-.18f, 0f, -.5f), new Vector3(.18f, .01f, .5f), groove);
                        line.transform.localPosition = mid + Vector3.up * .055f;
                        line.transform.localRotation = Quaternion.LookRotation(z - a);
                        line.transform.localScale = new Vector3(.36f, .01f, (z - a).magnitude);
                        var diamond = MiniGameParts.Loop(root, "Block diamond", z + Vector3.up * .07f, 1.02f, .13f, Arena.Gold, true, 4);
                        diamond.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                        // The shield: a walnut cart with our piece on it and a gold handle on its front.
                        var cart = new GameObject("Shield cart " + (i + 1));
                        cart.transform.SetParent(root, false);
                        cart.transform.localPosition = a;
                        cart.transform.localRotation = Quaternion.LookRotation(z - a);
                        var body = cart.AddComponent<Rigidbody>();
                        body.isKinematic = true;
                        body.useGravity = false;
                        body.interpolation = RigidbodyInterpolation.Interpolate;
                        var box = cart.AddComponent<BoxCollider>();
                        box.center = new Vector3(0f, .4f, 0f);
                        box.size = new Vector3(Cart, .8f, Cart);
                        cart.AddComponent<NoClimbSurface>();
                        var look = new GameObject("Look").transform;
                        look.SetParent(cart.transform, false);
                        MiniGameParts.Part(look, PrimitiveType.Cube, new Vector3(0f, .4f, 0f), new Vector3(Cart, .8f, Cart), Arena.Wood(false));
                        foreach (float x in new[] { -.66f, .66f })
                            foreach (float zz in new[] { -.4f, .4f })
                                MiniGameParts.Part(look, PrimitiveType.Cylinder, new Vector3(x, .2f, zz), new Vector3(.4f, .04f, .4f), Arena.Wood(true)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        MiniGameParts.Piece(look, "Shield", s.Shield, 1.2f, usMat, Arena.TeamColor(station.Team), false).transform.localPosition = new Vector3(0f, .8f, 0f);
                        movers[i] = body;
                        moverLooks[i] = look;
                        var h = MiniGameParts.Handle(root, "Cart handle " + (i + 1), Vector3.zero, new Vector3(.5f, .14f, .14f), this, (byte)i, GrabHandle.HandleKind.Pull);
                        handles[i] = h;
                        handleBodies[i] = h.GetComponent<Rigidbody>();
                        break;
                    }
                    case CheckBlockGenerator.Kind.Knight:
                    {
                        var body = attacker.GetComponent<Rigidbody>();
                        movers[i] = body;
                        moverLooks[i] = attacker.transform.Find("Look");
                        var h = MiniGameParts.Handle(root, "Knight ring " + (i + 1), Vector3.zero, new Vector3(.9f, .12f, .9f), this, (byte)i, GrabHandle.HandleKind.Drag);
                        handles[i] = h;
                        handleBodies[i] = h.GetComponent<Rigidbody>();
                        break;
                    }
                    default:   // the pawn
                        movers[i] = attacker.GetComponent<Rigidbody>();
                        moverLooks[i] = attacker.transform.Find("Look");
                        break;
                }
            }

            // The double check, hidden under the floor until every other check is solved.
            if (layout.Double)
            {
                var sq = new[] { layout.DoubleRook, layout.DoubleBishop };
                var kinds = new[] { PieceKind.Rook, PieceKind.Bishop };
                for (int k = 0; k < 2; k++)
                {
                    var p = MiniGameParts.Piece(root, "Double check " + kinds[k], kinds[k], 1.5f, themMat, ring, true, .4f);
                    p.transform.localPosition = At(sq[k], -2f);
                    Face(p.transform, kingAt);
                    p.SetActive(false);
                    doublePieces[k] = p.transform;
                    doubleBeams[k] = Beam("Double beam", .12f, out var r);
                    r.sharedMaterial = beamRed;
                    doubleBeams[k].gameObject.SetActive(false);
                }
                var e = At(layout.Escape);
                var team = Arena.TeamColor(station.Team);
                escapePlate = Arena.Picture(root, "Escape square", e + new Vector3(-1.05f, .05f, -1.05f), e + new Vector3(1.05f, .07f, 1.05f), Arena.Glow(new Color(team.r, team.g, team.b, .35f)));
                escapePlate.SetActive(false);
            }
            Place(true);
        }

        Transform Beam(string name, float thick, out Renderer look)
        {
            var go = MiniGameParts.Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(thick, thick, 1f), beamMat);
            go.name = name;
            look = go.GetComponent<Renderer>();
            return go.transform;
        }

        static void Face(Transform t, Vector3 target)
        {
            var d = target - t.localPosition;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) t.localRotation = Quaternion.LookRotation(d);
        }

        static void Stretch(Transform beam, Vector3 a, Vector3 b)
        {
            var d = b - a;
            beam.localPosition = (a + b) * .5f;
            beam.localRotation = d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d) : Quaternion.identity;
            var s = beam.localScale;
            beam.localScale = new Vector3(s.x, s.y, Mathf.Max(.01f, d.magnitude));
        }

        // ------------------------------------------------------------------ where things stand

        Vector3 RailDir(CheckBlockGenerator.Situation s) => (At(s.Block) - At(s.RailStart)).normalized;
        Vector3 KnightDir(CheckBlockGenerator.Situation s) { var d = At(s.From) - At(layout.King); d.y = 0f; return d.normalized; }
        float KingDistance => layout == null ? 1f : (At(layout.Escape) - At(layout.King)).magnitude;

        Vector3 MoverAt(int i, float frac)
        {
            var s = layout.Situations[i];
            switch (s.Kind)
            {
                case CheckBlockGenerator.Kind.Rail: return Vector3.Lerp(At(s.RailStart), At(s.Block), frac);
                case CheckBlockGenerator.Kind.Knight: return At(s.From) + KnightDir(s) * (KnightDrag * frac);
                default: return At(s.From);
            }
        }

        Vector3 KingAt(float frac) => Vector3.Lerp(At(layout.King), layout.Double ? At(layout.Escape) : At(layout.King), frac);

        void Place(bool snap)
        {
            if (layout == null) return;
            for (int i = 0; i < layout.Situations.Count && i < MaxSituations; i++)
            {
                var s = layout.Situations[i];
                if (movers[i] == null || s.Kind == CheckBlockGenerator.Kind.Pawn) continue;
                var p = World(MoverAt(i, shown[i]));
                if (snap) movers[i].transform.position = p; else movers[i].MovePosition(p);
                if (handleBodies[i] == null) continue;
                var hp = s.Kind == CheckBlockGenerator.Kind.Rail
                    ? p + transform.TransformDirection(RailDir(s)) * (Cart * .5f + .1f) + transform.up * .6f
                    : p + transform.up * 1.05f;
                if (snap) handleBodies[i].transform.position = hp; else handleBodies[i].MovePosition(hp);
            }
            var k = World(KingAt(kingShown));
            if (snap) { kingBase.transform.position = k; kingRingBody.transform.position = k + transform.up * .3f; }
            else { kingBase.MovePosition(k); kingRingBody.MovePosition(k + transform.up * .3f); }
        }

        // ------------------------------------------------------------------ host

        void FixedUpdate()
        {
            if (station == null || layout == null) return;
            if (!Remote && !Completed) Step(Time.fixedDeltaTime);
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i < MaxSituations; i++) shown[i] = Remote ? Mathf.MoveTowards(shown[i], progress[i], dt * .5f) : progress[i];
            kingShown = Remote ? Mathf.MoveTowards(kingShown, kingDrag, dt * .5f) : kingDrag;
            Place(false);
        }

        void Step(float dt)
        {
            var pawns = station.Pawns();
            var list = layout.Situations;
            for (int i = 0; i < list.Count && i < MaxSituations; i++)
            {
                if ((solvedBits >> i & 1) != 0) continue;
                var s = list[i];
                switch (s.Kind)
                {
                    case CheckBlockGenerator.Kind.Rail:
                    {
                        var dir = RailDir(s);
                        if (handles[i] != null) handles[i].PullAxis = transform.TransformDirection(dir);
                        Count(pawns, i, MoverAt(i, progress[i]), dir, true, out int own, out int against);
                        progress[i] = Drive(progress[i], own, against, s.SoloSpeed, s.SoloSpeed * 1.8f, s.RailLength, dt);
                        break;
                    }
                    case CheckBlockGenerator.Kind.Knight:
                    {
                        var dir = KnightDir(s);
                        if (handles[i] != null) handles[i].PullAxis = transform.TransformDirection(dir);
                        Count(pawns, i, MoverAt(i, progress[i]), dir, false, out int own, out int against);
                        progress[i] = Drive(progress[i], own, against, .35f, .65f, KnightDrag, dt);
                        break;
                    }
                    default:
                    {
                        var at = At(s.From);
                        foreach (var p in pawns)
                            if (p.Own && MiniGameParts.Flat(p.Local, at) <= Reach && MiniGameParts.Lunging(p, Speed)) progress[i] = 1f;
                        break;
                    }
                }
                if (progress[i] >= 1f) solvedBits |= 1 << i;
            }
            bool others = true;
            for (int i = 0; i < list.Count; i++) others &= (solvedBits >> i & 1) != 0;
            if (others && layout.Double && !doubleUp) doubleUp = true;
            if (doubleUp && !doubleDone)
            {
                var dir = (At(layout.Escape) - At(layout.King)).normalized;
                kingRing.PullAxis = transform.TransformDirection(dir);
                Count(pawns, -1, KingAt(kingDrag), dir, false, out int own, out int against);
                kingDrag = Drive(kingDrag, own, against, .25f, .45f, KingDistance, dt);
                if (kingDrag >= 1f) doubleDone = true;
            }
            Progress01 = Total == 0 ? 0f : Mathf.Min(.999f, (Done + Partial()) / Total);
            if (others && (!layout.Double || doubleDone)) Complete();
        }

        // The unsolved situations' share, so the gate's bar moves while a cart rolls.
        float Partial()
        {
            float sum = 0f;
            for (int i = 0; i < layout.Situations.Count; i++) if ((solvedBits >> i & 1) == 0) sum += progress[i];
            if (doubleUp && !doubleDone) sum += kingDrag;
            return sum;
        }

        // Who moves situation i's thing (i -1: the king): our pawns gripping its handle and pulling along `dir`, or
        // (a cart) pushing from the box behind it; the other team's the other way, each taking one off. At most two
        // of ours count.
        void Count(List<MissionStation.Pawn> pawns, int i, Vector3 at, Vector3 dir, bool push, out int own, out int against)
        {
            own = against = 0;
            var handle = i < 0 ? kingRing : handles[i];
            var across = new Vector3(dir.z, 0f, -dir.x);
            foreach (var p in pawns)
            {
                var mg = p.Driver as IMiniGamePawn;
                if (mg == null || mg.Down) continue;
                int effort = handle != null ? handle.Effort(p.Driver) : 0;
                if (effort == 0 && push)
                {
                    var rel = p.Local - at;
                    float along = Vector3.Dot(rel, dir), side = Mathf.Abs(Vector3.Dot(rel, across));
                    var input = transform.InverseTransformDirection(mg.MoveInput);
                    input.y = 0f;
                    float into = input.sqrMagnitude > .01f ? Vector3.Dot(input.normalized, dir) : 0f;
                    if (side <= Cart * .5f && p.Own && along <= -Cart * .5f && along >= -Cart * .5f - PushDepth && into >= GrabHandle.Along) effort = 1;
                    if (side <= Cart * .5f && !p.Own && along >= Cart * .5f && along <= Cart * .5f + PushDepth && into <= -GrabHandle.Along) effort = -1;
                }
                if (p.Own && effort > 0) own++;
                else if (!p.Own && effort < 0) against++;
            }
            own = Mathf.Min(2, own);
        }

        // One worker at `one` m/s, two at `two`; each of the other side takes one off; more against than for runs it
        // back at `one`, never under the last quarter reached (§7.3).
        static float Drive(float frac, int own, int against, float one, float two, float length, float dt)
        {
            int net = own - against;
            float v = net >= 2 ? two : net == 1 ? one : net == 0 ? 0f : -one;
            if (v == 0f) return frac;
            return (float)PawnRushMissions.Latched(frac, frac + v * dt / Mathf.Max(.01f, length));
        }

        // ------------------------------------------------------------------ moments (every PC)

        // StateDrivenMover: the carts, the knight and the king follow the host's progress, not the shared clock.
        void Update()
        {
            if (layout == null || root == null) return;
            float t = Time.time;
            var king = KingAt(kingShown) + Vector3.up * BeamHeight;
            var list = layout.Situations;
            bool rolling = false;
            for (int i = 0; i < list.Count && i < MaxSituations; i++)
            {
                var s = list[i];
                bool solved = (solvedBits >> i & 1) != 0;
                if (solved && (lastSolved >> i & 1) == 0) Solved(i);
                var from = At(s.From) + Vector3.up * BeamHeight;
                if (s.Kind == CheckBlockGenerator.Kind.Knight) from = MoverAt(i, shown[i]) + Vector3.up * BeamHeight;
                // A blocked line ends at the shield, grey; a knight dragged off or a pawn taken has none.
                if (s.Kind == CheckBlockGenerator.Kind.Rail && solved)
                {
                    var to = At(s.Block) + Vector3.up * BeamHeight;
                    Stretch(beams[i], from, to - (to - from).normalized * (Cart * .5f));
                    if (beamLooks[i].sharedMaterial != beamGrey) beamLooks[i].sharedMaterial = beamGrey;
                }
                else
                {
                    if (beamLooks[i].sharedMaterial != beamMat) beamLooks[i].sharedMaterial = beamMat;
                    bool on = !solved;
                    if (beams[i].gameObject.activeSelf != on) beams[i].gameObject.SetActive(on);
                    if (on) Stretch(beams[i], from, king);
                }
                Hum(i, !solved, from);
                if (s.Kind == CheckBlockGenerator.Kind.Rail && moverLooks[i] != null)
                {
                    bool moving = !solved && Mathf.Abs(shown[i] - lastShown[i]) > 1e-5f;
                    lastShown[i] = shown[i];
                    rolling |= moving;
                    moverLooks[i].localPosition = moving ? new Vector3(0f, .02f * Mathf.Sin(t * 30f), 0f) : Vector3.zero;
                    float settle = (t - solvedAt[i]) / .25f;
                    if (solved && settle < 1f) moverLooks[i].localPosition = new Vector3(0f, .08f * (1f - settle), 0f);
                    // Past 75 % the line burns on the shield's face.
                    if (moving && shown[i] > .75f && t - sparkAt >= .12f)
                    {
                        sparkAt = t;
                        var face = MoverAt(i, shown[i]) + Vector3.up * BeamHeight;
                        ArenaFx.Burst(World(face), new[] { Arena.TeamColor(Opponent) }, 3, .8f, .8f, .3f, .05f);
                    }
                }
                if (s.Kind != CheckBlockGenerator.Kind.Rail && !solved && moverLooks[i] != null)
                {
                    moverLooks[i].localRotation = Quaternion.identity;
                    moverLooks[i].localPosition = Vector3.zero;
                }
                if (s.Kind != CheckBlockGenerator.Kind.Rail && solved && moverLooks[i] != null)
                {
                    // A knight slides off and topples sideways in 0.4 s; a pawn topples and sinks 0.3 m.
                    float k = Mathf.Clamp01((t - solvedAt[i]) / (s.Kind == CheckBlockGenerator.Kind.Knight ? .4f : .3f));
                    moverLooks[i].localRotation = Quaternion.Euler(0f, 0f, 90f * k);
                    moverLooks[i].localPosition = new Vector3(0f, s.Kind == CheckBlockGenerator.Kind.Pawn ? -.3f * k : 0f, 0f);
                }
            }
            lastSolved = solvedBits;
            if (rolling)
            {
                if (roll == null || !roll.isPlaying) roll = ArenaFx.Hold("sfx_cart_roll", World(king), 1f, .6f);
            }
            else { ArenaFx.Cut(roll); roll = null; }

            // The check ring pulses (0.35 - 0.65 at 4 Hz) while any check is on.
            bool checkOn = !Completed;
            if (checkRing != null)
            {
                checkRing.enabled = checkOn;
                var c = Arena.Red;
                c.a = .5f + .15f * Mathf.Sin(t * 4f * Mathf.PI * 2f);
                checkRing.startColor = checkRing.endColor = c;
            }
            Double(t, king);
        }

        void Hum(int i, bool on, Vector3 at)
        {
            if (on)
            {
                if (hums[i] == null || !hums[i].isPlaying) hums[i] = ArenaFx.Hold("sfx_beam_hum", World(at), 1f, .35f);
            }
            else if (hums[i] != null) { ArenaFx.Cut(hums[i]); hums[i] = null; }
        }

        void Solved(int i)
        {
            solvedAt[i] = Time.time;
            var s = layout.Situations[i];
            var at = World(MoverAt(i, 1f));
            switch (s.Kind)
            {
                case CheckBlockGenerator.Kind.Rail:
                    ArenaFx.Ring(at, 0f, 2.2f, .5f, Arena.Gold);
                    ArenaFx.Burst(at + Vector3.up * .8f, new[] { Arena.Gold, Arena.TeamColor(Opponent), Color.white }, 24, 2f, 2.5f, .8f, .08f);
                    ArenaFx.Sfx("sfx_shield_lock", at);
                    break;
                case CheckBlockGenerator.Kind.Knight:
                    ArenaFx.Burst(at + Vector3.up * .6f, new[] { MiniGameParts.PieceColor(Opponent), Arena.TeamColor(Opponent) }, 20, 1.8f, 2f, .8f, .08f);
                    ArenaFx.Sfx("sfx_piece_fall", at);
                    break;
                default:
                    ArenaFx.Burst(at + Vector3.up * .5f, new[] { MiniGameParts.PieceColor(Opponent), Arena.TeamColor(Opponent), Arena.Gold }, 18, 1.8f, 2f, .8f, .08f);
                    ArenaFx.Sfx("sfx_ep_capture", at);
                    break;
            }
        }

        // The double check: two pieces rise in 0.6 s with red lines, two red rings, the safe square lit; the king
        // dragged there turns them grey.
        void Double(float t, Vector3 king)
        {
            if (!layout.Double) return;
            if (doubleUp && !wasDoubleUp)
            {
                doubleAt = t;
                var k = World(KingAt(0f));
                ArenaFx.Ring(k, 0f, 5f, .6f, Arena.Red);
                ArenaFx.Sfx("sfx_double_check", k);
                for (int i = 0; i < 2; i++) doublePieces[i].gameObject.SetActive(true);
                escapePlate.SetActive(true);
            }
            if (doubleUp && doubleAt >= 0f && t - doubleAt >= .2f && t - doubleAt - Time.deltaTime < .2f)
                ArenaFx.Ring(World(KingAt(0f)), 0f, 3f, .6f, Arena.Red);
            wasDoubleUp = doubleUp;
            if (!doubleUp)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (doublePieces[i].gameObject.activeSelf) doublePieces[i].gameObject.SetActive(false);
                    if (doubleBeams[i].gameObject.activeSelf) doubleBeams[i].gameObject.SetActive(false);
                    Hum(MaxSituations + i, false, Vector3.zero);
                }
                if (escapePlate.activeSelf) escapePlate.SetActive(false);
                return;
            }
            float rise = Mathf.Clamp01((t - doubleAt) / .6f);
            var squares = new[] { layout.DoubleRook, layout.DoubleBishop };
            for (int i = 0; i < 2; i++)
            {
                doublePieces[i].localPosition = At(squares[i], -1.5f * (1f - rise));
                bool show = rise >= 1f;
                if (doubleBeams[i].gameObject.activeSelf != show) doubleBeams[i].gameObject.SetActive(show);
                if (show) Stretch(doubleBeams[i], At(squares[i], BeamHeight), king);
                var r = doubleBeams[i].GetComponent<Renderer>();
                var want = doubleDone ? beamGrey : beamRed;
                if (r.sharedMaterial != want) r.sharedMaterial = want;
                Hum(MaxSituations + i, show && !doubleDone, At(squares[i], BeamHeight));
            }
            if (doubleDone && !wasDoubleDone)
            {
                var at = World(At(layout.Escape));
                ArenaFx.Ring(at, 0f, 3f, .6f, Arena.TeamColor(station.Team));
                ArenaFx.Sfx("sfx_resolve", at);
                escapePlate.SetActive(false);
            }
            wasDoubleDone = doubleDone;
        }

        // ------------------------------------------------------------------ the wire (§7.7, 10 bytes)

        public override void WriteNet(ref NetBlock block)
        {
            block.U16(seed < 0 ? 0 : seed);
            int n = layout == null ? 0 : Mathf.Min(MaxSituations, layout.Situations.Count);
            block.U8(n | (seed >= 0 ? 0x80 : 0));
            for (int i = 0; i < n; i++) block.U8(Mathf.RoundToInt(progress[i] * 255f));
            block.U8(solvedBits & 0x3F | (doubleUp ? 0x40 : 0) | (doubleDone ? 0x80 : 0));
            block.U8(Mathf.RoundToInt(kingDrag * 255f));
        }

        public override void ReadNet(ref NetBlock block)
        {
            int s = block.ReadU16();
            int head = block.ReadU8();
            if ((head & 0x80) == 0) return;
            if (s != seed) Use(s);
            int n = head & 0x7F;
            for (int i = 0; i < n && i < MaxSituations; i++) progress[i] = block.ReadU8() / 255f;
            int bits = block.ReadU8();
            solvedBits = bits & 0x3F;
            doubleUp = (bits & 0x40) != 0;
            doubleDone = (bits & 0x80) != 0;
            kingDrag = block.ReadU8() / 255f;
        }

        public override void ResetGame()
        {
            base.ResetGame();
            // The same position again from the start (a new round's game draws a new one).
            if (!Remote && seed >= 0) Use(seed);
        }
    }
}
