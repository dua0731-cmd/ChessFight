using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // J, the knight's road (R129, the user after playing the v1.0 knight pads: the flags took no progress and
    // stood up again, eight directions were fiddly, the islands were a mess and not fun; "jump pads with no
    // direction, only height, floor only on the knight's path, water elsewhere, dive onto things to take them").
    //
    // Floor only on eight board squares that follow one another by a knight's move (KnightRoute: d1 at the
    // entrance to f8 by the gate); water everywhere else. In the middle of every square a gold pad: Space on it is
    // a much higher jump that keeps the pawn's own run (no aiming - run the way you want and jump), and a dive in
    // the air carries it the rest of the way to the next square. On six squares stand the other side's pieces; a
    // dive onto one (or landing on it from a pad jump) takes it. Six taken: the gate opens. A fall puts the pawn
    // back on the south floor after 1.2 s afloat (MgWaterReturn).
    //
    // Physics and the network: the jump is the lab's launch (ILaunchable.Launch: the run plus 7 m/s up), given on
    // the host from the jump the host reads (MiniGameHooks.JumpIntercept); the flight and the dive are the
    // ragdoll's own, seen by everyone in the snapshots. The pieces are kinematic and never pushed. The host sends
    // which pieces are taken, a launch counter and the pad it was on (3 bytes); every PC plays the moments.
    public sealed class MgKnightPads : MiniGameBase, IProgressCount
    {
        public const float Surface = -.6f, Bottom = -1.8f, South = 2.1f, North = 19.3f, PieceHeight = 1.1f;

        [SerializeField] MissionStation station;

        static readonly List<MgKnightPads> live = new List<MgKnightPads>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => live.Clear();

        int taken, launches, launchSquare;
        readonly Dictionary<ICharacterDriver, float> jumpedAt = new Dictionary<ICharacterDriver, float>();

        // The look.
        Transform[] pads;
        Transform[] pieces;
        Collider[] pieceColliders;
        readonly float[] takenAt = new float[6];
        readonly Vector3[] takenFrom = new Vector3[6], takenDir = new Vector3[6];
        readonly bool[] splashed = new bool[6];
        readonly float[] bounceAt = new float[8];
        int shownTaken, shownLaunches;

        public int Done { get { int n = 0; for (int b = taken; b != 0; b &= b - 1) n++; return n; } }
        public int Total => KnightRoute.PieceSquares.Length;

        public static MgKnightPads Build(CourseBuilder b, MissionStation station)
        {
            var game = b.parent.gameObject.AddComponent<MgKnightPads>();
            game.station = station;
            game.Make(b);
            return game;
        }

        int Opponent => station.Team == Teams.White ? Teams.Black : Teams.White;
        Vector3 Square(string name, float y = 0f) => MiniGameParts.Local(station, KnightRoute.Center(name), y);
        Vector3 World(Vector3 local) => transform.TransformPoint(local);

        void Make(CourseBuilder b)
        {
            // The south floor (the way in and the promotion pad) and a strip by the gate; water between them.
            MissionGames.Floor(b, 0f, South);
            // The strip runs from the gate (v -3.2) to f8 (v 4.4), mirrored for black.
            float m = MiniGameParts.Mirror(station), v0 = Mathf.Min(-3.2f * m, 4.4f * m), v1 = Mathf.Max(-3.2f * m, 4.4f * m);
            b.Box("Gate strip", new Vector3(v0, -1f, North), new Vector3(v1, 0f, 20f), Arena.DaisMaterial).AddComponent<CourseFloor>();
            var stone = Arena.Lit("M_MoatStone", Arena.Hex("#6E6A64"), 0f, .9f);
            b.Box("Water bottom", new Vector3(-10f, Bottom - 1f, South), new Vector3(10f, Bottom, North), stone, true);
            b.Box("Water wall S", new Vector3(-10f, Bottom, South - .1f), new Vector3(10f, 0f, South), stone, true);
            b.Box("Water wall N", new Vector3(-10f, Bottom, North), new Vector3(10f, -Arena.DaisHeight, North + .1f), stone, true);
            b.Box("Water wall W", new Vector3(-10f, Bottom, South), new Vector3(-9.9f, -Arena.DaisHeight, North), stone, true);
            b.Box("Water wall E", new Vector3(9.9f, Bottom, South), new Vector3(10f, -Arena.DaisHeight, North), stone, true);
            Arena.Picture(b.parent, "Water", new Vector3(-9.9f, Surface - .05f, South), new Vector3(9.9f, Surface, North), Arena.Glow(new Color(.35f, .62f, .78f, .6f)));
            MgWaterReturn.Make(transform, station, Surface, Bottom, South, North);

            // The road: eight squares standing out of the water on their own pillars, numbered, each with a pad.
            var padMat = Arena.Lit("M_KnightPad", Arena.Hex("#D9CDB4"), 0f, .6f);
            var spring = Arena.Lit("M_PadSpring", new Color(.55f, .55f, .58f), 0f, .4f);
            float h = KnightRoute.SquareSize * .5f;
            pads = new Transform[KnightRoute.Squares.Length];
            for (int i = 0; i < KnightRoute.Squares.Length; i++)
            {
                string name = KnightRoute.Squares[i];
                PawnRushBoard.Parse(name, out int f, out int r);
                var c = Square(name);
                b.Box("Road " + name, c + new Vector3(-h, Bottom, -h), c + new Vector3(h, 0f, h), Arena.Wood(PawnRushBoard.IsLight(f, r)), true).AddComponent<CourseFloor>();
                foreach (var side in new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h - .08f) })
                    Arena.Picture(b.parent, "Road rim", c + side, c + side + new Vector3(2f * h, .03f, .08f), Arena.TrimGold);
                foreach (var side in new[] { new Vector3(-h, 0f, -h), new Vector3(h - .08f, 0f, -h) })
                    Arena.Picture(b.parent, "Road rim", c + side, c + side + new Vector3(.08f, .03f, 2f * h), Arena.TrimGold);
                // The pad: a disc on a spring, a gold ring; Space here jumps high (any way the pawn runs).
                var pad = new GameObject("Jump pad " + name).transform;
                pad.SetParent(b.parent, false);
                pad.localPosition = c;
                MiniGameParts.Part(pad, PrimitiveType.Cylinder, new Vector3(0f, .03f, 0f), new Vector3(1.3f, .03f, 1.3f), spring);
                MiniGameParts.Part(pad, PrimitiveType.Cylinder, new Vector3(0f, .08f, 0f), new Vector3(1.2f, .02f, 1.2f), padMat);
                MiniGameParts.Loop(pad, "Pad ring", new Vector3(0f, .11f, 0f), KnightRoute.PadRadius, .08f, Arena.Gold);
                pads[i] = pad;
                var number = Arena.Text(b.parent, "Road number", c + new Vector3(0f, .04f, -h + .3f), .35f, Arena.Gold);
                number.text = (i + 1).ToString();
                number.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // The other side's pieces, one on each of six squares, a little off the pad.
            var them = Opponent;
            pieces = new Transform[KnightRoute.PieceSquares.Length];
            pieceColliders = new Collider[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                var go = MiniGameParts.Piece(b.parent, "Piece " + KnightRoute.PieceSquares[i], KnightRoute.PieceKinds[i], PieceHeight, MiniGameParts.PieceMaterial(them), Arena.TeamColor(them), true, .3f);
                go.transform.localPosition = Square(KnightRoute.PieceSquares[i]) + new Vector3(.55f, 0f, .55f);
                pieces[i] = go.transform;
                pieceColliders[i] = go.GetComponent<Collider>();
                takenAt[i] = -10f;
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

        // Space on a pad is the high jump instead of the plain one (any J game that has the pawn on a pad).
        static bool Intercept(ICharacterDriver driver)
        {
            foreach (var game in live)
                if (game != null && game.TryJump(driver)) return true;
            return false;
        }

        // The host (or offline): on a pad, standing -> thrown up 7 m/s with its own run kept (no aiming).
        bool TryJump(ICharacterDriver driver)
        {
            if (Remote || pads == null || driver?.FollowTarget == null || !(driver is ILaunchable launchable)) return false;
            if (driver is IMiniGamePawn mg && mg.Down) return false;
            var local = transform.InverseTransformPoint(driver.FollowTarget.position);
            if (local.y < -.2f || local.y > 1.8f) return false;
            for (int i = 0; i < pads.Length; i++)
            {
                if (MiniGameParts.Flat(local, pads[i].localPosition) > KnightRoute.PadRadius) continue;
                var run = driver is IMiniGamePawn p ? p.HipsVelocity : Vector3.zero;
                run.y = 0f;
                launchable.Launch(run + transform.up * KnightRoute.PadUp);
                jumpedAt[driver] = Time.time;
                launches = (launches + 1) & 255;
                launchSquare = i;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ host: taking pieces

        void FixedUpdate()
        {
            if (Remote || Completed || station == null || pieces == null) return;
            float t = Time.time;
            foreach (var p in station.Pawns())
            {
                if (!p.Own) continue;
                var mg = p.Driver as IMiniGamePawn;
                bool padJump = jumpedAt.TryGetValue(p.Driver, out float at) && t - at < KnightRoute.JumpCatch;
                bool diving = mg != null && mg.Diving;
                float speed = MiniGameParts.FlatSpeed(mg != null ? mg.HipsVelocity : p.Velocity);
                for (int i = 0; i < pieces.Length; i++)
                {
                    if ((taken >> i & 1) != 0) continue;
                    if (p.Local.y > PieceHeight + 1f || p.Local.y < -.5f) continue;
                    if (!KnightRoute.Takes(MiniGameParts.Flat(p.Local, pieces[i].localPosition), diving, speed, padJump)) continue;
                    taken |= 1 << i;
                }
            }
            Progress01 = Done / (float)Total;
            if (Done >= Total) Complete();
        }

        // ------------------------------------------------------------------ moments (every PC)

        // StateDrivenMover: the pieces go when the host says they are taken; the pads bounce on the host's launches.
        void Update()
        {
            if (pieces == null) return;
            float t = Time.time;
            for (int i = 0; i < pieces.Length; i++)
            {
                bool now = (taken >> i & 1) != 0, was = (shownTaken >> i & 1) != 0;
                if (now && !was) Take(i);
                if (!now && was) Restore(i);
                if (now) Fly(i, t);
            }
            shownTaken = taken;
            if (launches != shownLaunches)
            {
                shownLaunches = launches;
                if (launchSquare >= 0 && launchSquare < pads.Length)
                {
                    bounceAt[launchSquare] = t;
                    var at = World(pads[launchSquare].localPosition);
                    ArenaFx.Burst(at + Vector3.up * .1f, new[] { new Color(.8f, .72f, .6f), Arena.Gold }, 8, 1.4f, 1.5f, .5f, .07f);
                    ArenaFx.Ring(at, 0f, 1.6f, .5f, Arena.Gold);
                    ArenaFx.Sfx("sfx_pad_launch", at);
                }
            }
            for (int i = 0; i < pads.Length; i++)
            {
                float k = (t - bounceAt[i]) / .2f;
                float y = k >= 0f && k < 1f ? 1f + .8f * Mathf.Sin(k * Mathf.PI) : 1f;
                pads[i].localScale = new Vector3(1f, y, 1f);
            }
        }

        void Take(int i)
        {
            takenAt[i] = Time.time;
            takenFrom[i] = pieces[i].localPosition;
            // Knocked away from the nearest pawn this PC draws (toward the water).
            var away = takenFrom[i] - Square(KnightRoute.PieceSquares[i]);
            float best = 3f;
            foreach (var p in station.Pawns())
            {
                float d = MiniGameParts.Flat(p.Local, takenFrom[i]);
                if (d < best) { best = d; away = takenFrom[i] - p.Local; }
            }
            away.y = 0f;
            takenDir[i] = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
            splashed[i] = false;
            if (pieceColliders[i] != null) pieceColliders[i].enabled = false;
            var at = World(takenFrom[i] + Vector3.up * .6f);
            ArenaFx.Burst(at, new[] { MiniGameParts.PieceColor(Opponent), Arena.TeamColor(Opponent), Arena.Gold }, 22, 2.4f, 3f, 1f, .1f);
            ArenaFx.Ring(World(takenFrom[i] + Vector3.up * .05f), 0f, 1.8f, .5f, Arena.Gold);
            ArenaFx.Sfx("sfx_ep_capture", at, pitch: Mathf.Pow(2f, (Done - Total) / 12f));
        }

        // Taken: tips over and flies 3 m off the road into the water, where it sinks.
        void Fly(int i, float t)
        {
            float k = Mathf.Clamp01((t - takenAt[i]) / 1f);
            var to = takenFrom[i] + takenDir[i] * 3f + Vector3.up * (Surface - .2f);
            var p = Vector3.Lerp(takenFrom[i], to, k);
            p.y += 1.6f * 4f * k * (1f - k);
            if (k >= 1f) p.y -= Mathf.Min(1.5f, (t - takenAt[i] - 1f) * .8f);
            pieces[i].localPosition = p;
            pieces[i].localRotation = Quaternion.AngleAxis(90f * Mathf.Min(1f, k * 2f), Vector3.Cross(Vector3.up, takenDir[i])) * Quaternion.Euler(0f, 360f * k, 0f);
            if (k >= 1f && !splashed[i])
            {
                splashed[i] = true;
                var at = World(new Vector3(to.x, Surface, to.z));
                ArenaFx.Burst(at, new[] { Arena.Hex("#7FC8E8"), Color.white }, 16, 1.4f, 4f, .8f, .07f);
                ArenaFx.Sfx("sfx_splash", at, volume: .7f);
            }
        }

        void Restore(int i)
        {
            pieces[i].localPosition = Square(KnightRoute.PieceSquares[i]) + new Vector3(.55f, 0f, .55f);
            pieces[i].localRotation = Quaternion.identity;
            if (pieceColliders[i] != null) pieceColliders[i].enabled = true;
            takenAt[i] = -10f;
        }

        // ------------------------------------------------------------------ the wire (3 bytes)

        public override void WriteNet(ref NetBlock block)
        {
            block.U8(taken);
            block.U8(launches);
            block.U8(launchSquare);
        }

        public override void ReadNet(ref NetBlock block)
        {
            taken = block.ReadU8();
            launches = block.ReadU8();
            launchSquare = block.ReadU8();
        }

        public override void ResetGame()
        {
            base.ResetGame();
            taken = 0;
            jumpedAt.Clear();
        }
    }
}
