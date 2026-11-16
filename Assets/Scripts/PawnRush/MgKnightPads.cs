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
    // Floor only on tubs a knight's move apart; water everywhere else. In the middle of every tub a gold pad: Space on
    // it is a much higher jump that keeps the pawn's own run (no aiming - run the way you want and jump), and a dive
    // in the air carries it the rest of the way to the next tub. On some tubs stand the other side's pieces; a pawn
    // that comes to stand on a piece's tub takes it (R687, as a chess piece is taken by moving onto its square: no
    // dive needed any more), and a gold mark turns over every piece still to take. All taken: the gate opens.
    //
    // R686 (the user after the 6 vs 6 test, 11-16: "현재 나이트의 길 문제는 길이 하나로 이어져 있어서 팀원과의 협동은 없고
    // 난이도도 어려워"; the proposal's version B, "B안으로 진행해줘"; Core KnightRoad):
    //   - two to four roads side by side, by the team's size, each four tubs from the entrance floor's edge, the pieces
    //     spread over them - a piece a player - so a team that splits up is done sooner;
    //   - the last piece, the king, on a tub of its own in front of the gate that lies under the water: it rises
    //     while both gold tubs beside it are pressed (a teammate on each, or one who stood there within eight
    //     seconds) and sinks again when they are not; once the game is done it stays up, the way to the gate;
    //   - easier: larger tubs (2.4 to 2.6 m), a wider pad, a longer reach, and a pawn that falls in the water starts
    //     again from the last tub it stood on (MgWaterReturn.ReturnPoint), not from the entrance floor.
    //
    // Physics and the network: the jump is the lab's launch (ILaunchable.Launch: the run plus 7 m/s up), given on
    // the host from the jump the host reads (MiniGameHooks.JumpIntercept); the flight and the dive are the
    // ragdoll's own, seen by everyone in the snapshots. The pieces are kinematic and never pushed. The host sends
    // which pieces are taken, a launch counter and the pad it was on, which gold tubs are pressed and how far up the
    // king's tub is (5 bytes); every PC plays the moments and moves the king's tub from that.
    public sealed class MgKnightPads : MiniGameBase, IProgressCount
    {
        public const float Surface = -.6f, Bottom = -1.8f, South = 2.1f, North = 19.3f, PieceHeight = 1.1f;

        [SerializeField] MissionStation station;

        static readonly List<MgKnightPads> live = new List<MgKnightPads>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => live.Clear();

        // This game's road (its roads, the tubs' size, the pieces on them).
        KnightRoad road = KnightRoad.Baseline;
        public KnightRoad Road => road;

        int taken, launches, launchSquare;
        // R686: which gold tubs are pressed (bits 0 and 1) and how far up the king's tub is (0 under .. 1 level) -
        // the host's, and what a client last heard.
        int pressed;
        float raise;
        // the host's: the tub of the roads each of this team's pawns stood on last, and when each gold tub was stood on
        readonly Dictionary<ICharacterDriver, int> lastTub = new Dictionary<ICharacterDriver, int>();
        readonly float[] stoodAt = { -100f, -100f };

        // The look. (Up to eight pieces - the block's one byte - on up to sixteen tubs and the king's.)
        Transform[] pads;
        Transform[] pieces;
        Collider[] pieceColliders;
        Transform[] marks;                      // R687: the gold mark over each piece still to take
        Vector3[] pieceTub;                     // the middle of the tub each piece stands on (the king's: its own tub, up)
        Transform kingTub;
        Rigidbody kingBody;
        Vector3 kingRest;                       // the king's tub's place when it is up, in this game's frame
        readonly Transform[] pressRings = new Transform[2];
        readonly Vector3[] pressAt = new Vector3[2];
        readonly float[] takenAt = new float[8];
        readonly Vector3[] takenFrom = new Vector3[8], takenDir = new Vector3[8];
        readonly bool[] splashed = new bool[8];
        readonly float[] bounceAt = new float[20];
        int shownTaken, shownLaunches, shownPressed;
        float shownRaise;
        bool shownUp;

        public int Done { get { int n = 0; for (int b = taken; b != 0; b &= b - 1) n++; return n; } }
        public int Total => road.Pieces;
        /// <summary>The king's tub, 0 under the water .. 1 level with the others (the checks).</summary>
        public float KingRaise => raise;
        public int PressedTubs => pressed;

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
            road = KnightRoad.For(station.TeamSize);
            // The south floor (the way in and the promotion pad) and a strip by the gate; water between them.
            MissionGames.Floor(b, 0f, South);
            b.Box("Gate strip", new Vector3(-3.2f, -1f, North), new Vector3(3.2f, 0f, 20f),
                station.PalaceStyle ? MiniGamePalaceArt.DaisMaterial(PawnRushMissions.KnightPads) : Arena.DaisMaterial).AddComponent<CourseFloor>();
            var stone = Arena.Lit("M_MoatStone", Arena.Hex("#6E6A64"), 0f, .9f);
            b.Box("Water bottom", new Vector3(-10f, Bottom - 1f, South), new Vector3(10f, Bottom, North), stone, true);
            // The S wall lies inside the south floor's edge, the N wall inside the gate strip's: their pictures stand
            // off those faces (R498).
            MissionGames.LinedWall(b, "Water wall S", new Vector3(-10f, Bottom, South - .1f), new Vector3(10f, 0f, South), stone, 1f);
            MissionGames.LinedWall(b, "Water wall N", new Vector3(-10f, Bottom, North), new Vector3(10f, -Arena.DaisHeight, North + .1f), stone, -1f);
            b.Box("Water wall W", new Vector3(-10f, Bottom, South), new Vector3(-9.9f, -Arena.DaisHeight, North), stone, true);
            b.Box("Water wall E", new Vector3(9.9f, Bottom, South), new Vector3(10f, -Arena.DaisHeight, North), stone, true);
            Arena.Picture(b.parent, "Water", new Vector3(-9.9f, Surface - .05f, South), new Vector3(9.9f, Surface, North), MapLook.Water != null ? MapLook.Water : Arena.Glow(new Color(.35f, .62f, .78f, .6f)));
            // R686: a pawn that falls in starts again from the last tub it stood on
            MgWaterReturn.Make(transform, station, Surface, Bottom, South, North).ReturnPoint = ReturnPoint;

            // The roads: tubs standing out of the water on their own pillars, numbered along their road, each with a pad.
            var padMat = Arena.Lit("M_KnightPad", Arena.Hex("#D9CDB4"), 0f, .6f);
            var spring = Arena.Lit("M_PadSpring", new Color(.55f, .55f, .58f), 0f, .4f);
            float h = road.SquareSize * .5f;
            pads = new Transform[road.Squares.Length];
            int at = 0;
            foreach (var lane in road.Lanes)
                for (int step = 0; step < lane.Length; step++, at++)
                {
                    string name = lane[step];
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
                    MiniGameParts.Loop(pad, "Pad ring", new Vector3(0f, .11f, 0f), .8f, .08f, Arena.Gold);
                    pads[at] = pad;
                    var number = Arena.Text(b.parent, "Road number", c + new Vector3(0f, .04f, -h + .3f), .35f, Arena.Gold);
                    number.text = (step + 1).ToString();
                    number.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    // A gold tub: a wide gold ring round its rim that swells while it is pressed (Update), and its word.
                    int press = road.PressOf(at);
                    if (press < 0) continue;
                    var ring = new GameObject("Press ring " + name).transform;
                    ring.SetParent(b.parent, false);
                    ring.localPosition = c + new Vector3(0f, .14f, 0f);
                    MiniGameParts.Loop(ring, "Press loop", Vector3.zero, h * .96f, .16f, Arena.Gold);
                    MiniGameParts.Loop(ring, "Press loop inner", Vector3.zero, h * .8f, .06f, Arena.Gold);
                    var word = Arena.Text(b.parent, "Press word", c + new Vector3(0f, .05f, h - .35f), .32f, Arena.Gold);
                    word.text = "함께 밟기";
                    word.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    pressRings[press] = ring;
                    pressAt[press] = c;
                }

            // The king's tub in front of the gate: its own body, under the water until both gold tubs are pressed.
            kingRest = Square(KnightRoad.KingSquare);
            kingTub = new GameObject("King tub").transform;
            kingTub.SetParent(b.parent, false);
            kingTub.localPosition = kingRest + Vector3.down * KnightRoad.SunkDepth;
            kingBody = kingTub.gameObject.AddComponent<Rigidbody>();
            kingBody.isKinematic = true;
            kingBody.interpolation = RigidbodyInterpolation.Interpolate;
            var kingBox = b.Box("Road " + KnightRoad.KingSquare, kingRest + new Vector3(-h, Bottom, -h), kingRest + new Vector3(h, 0f, h), Arena.TrimGold, true);
            kingBox.AddComponent<CourseFloor>();
            kingBox.transform.SetParent(kingTub, false);
            kingBox.transform.localPosition = new Vector3(0f, Bottom * .5f, 0f);
            var crown = new GameObject("King ring").transform;
            crown.SetParent(kingTub, false);
            crown.localPosition = new Vector3(0f, .12f, 0f);
            MiniGameParts.Loop(crown, "King loop", Vector3.zero, h * .9f, .12f, Arena.Gold);

            // The other side's pieces: on the roads' tubs a little off the pad, the king in the middle of its own tub.
            var them = Opponent;
            pieces = new Transform[road.Pieces];
            pieceColliders = new Collider[pieces.Length];
            marks = new Transform[pieces.Length];
            pieceTub = new Vector3[pieces.Length];
            var gem = Arena.Emissive(Arena.Gold, 1.4f);
            for (int i = 0; i < pieces.Length; i++)
            {
                var go = MiniGameParts.Piece(b.parent, "Piece " + road.PieceSquares[i], road.PieceKinds[i], PieceHeight, MiniGameParts.PieceMaterial(them), station.Accent(them), true, .3f);
                go.transform.localPosition = PieceRest(i);
                pieces[i] = go.transform;
                pieceColliders[i] = go.GetComponent<Collider>();
                takenAt[i] = -10f;
                pieceTub[i] = i == road.KingPiece ? kingRest : Square(road.PieceSquares[i]);
                // R687: a gold gem turning over the piece says "this one" (Update moves it; gone once the piece is taken)
                var mark = new GameObject("Target mark " + road.PieceSquares[i]).transform;
                mark.SetParent(b.parent, false);
                MiniGameParts.Part(mark, PrimitiveType.Cube, Vector3.zero, new Vector3(.3f, .3f, .3f), gem).transform.localRotation = Quaternion.Euler(45f, 0f, 35.26f);
                marks[i] = mark;
            }
        }

        // Where piece i stands: beside its tub's pad, or - the king - on its own tub, as far down as that is.
        Vector3 PieceRest(int i) =>
            i == road.KingPiece ? kingRest + Vector3.down * (KnightRoad.SunkDepth * (1f - shownRaise))
                                : Square(road.PieceSquares[i]) + new Vector3(.55f, 0f, .55f);

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
                launches = (launches + 1) & 255;
                launchSquare = i;
                return true;
            }
            return false;
        }

        // R686: where a pawn that fell in the water starts again (this game's frame): the last tub of the roads it
        // stood on, or the entrance floor if it has stood on none (MgWaterReturn asks, on the host).
        Vector3 ReturnPoint(ICharacterDriver driver) =>
            driver != null && pads != null && lastTub.TryGetValue(driver, out int tub) && tub >= 0 && tub < pads.Length
                ? pads[tub].localPosition : new Vector3(0f, 0f, 1f);

        // ------------------------------------------------------------------ host: where pawns stand, taking pieces

        void FixedUpdate()
        {
            if (station == null || pieces == null) return;
            if (!Remote) Judge();
            // StateDrivenMover: the king's tub goes where the gold tubs' state puts it - the host's own, a client's as
            // it heard it (shownRaise glides there on every PC, so the body never jumps).
            shownRaise = Mathf.MoveTowards(shownRaise, raise, Time.fixedDeltaTime / KnightRoad.RaiseSeconds);
            if (kingBody != null) kingBody.MovePosition(World(kingRest + Vector3.down * (KnightRoad.SunkDepth * (1f - shownRaise))));
        }

        void Judge()
        {
            float t = Time.time, half = road.SquareSize * .5f;
            foreach (var p in station.Pawns())
            {
                if (!p.Own) continue;
                // standing on a tub of the roads (hips over its top - a pawn's hips stand only 0.26 m up, and a pawn lying
                // on the tub counts - and not flying past): it is where a fall starts again, and a gold tub is pressed
                if (p.Local.y > -.2f && p.Local.y < 1.7f && Mathf.Abs(p.Velocity.y) < 2.5f)
                    for (int i = 0; i < pads.Length; i++)
                    {
                        var c = pads[i].localPosition;
                        if (Mathf.Abs(p.Local.x - c.x) > half || Mathf.Abs(p.Local.z - c.z) > half) continue;
                        lastTub[p.Driver] = i;
                        int press = road.PressOf(i);
                        if (press >= 0) stoodAt[press] = t;
                        break;
                    }
                // R687: a piece is taken by a pawn on its tub (over the tub's top, not in the water beside it, not passing
                // high over it), or with its hips within reach of the piece
                if (Completed || p.Local.y > PieceHeight + 1f || p.Local.y < -.3f) continue;
                for (int i = 0; i < pieces.Length; i++)
                {
                    if ((taken >> i & 1) != 0) continue;
                    // the king only once its tub is up
                    if (i == road.KingPiece && raise < KnightRoad.RaisedEnough) continue;
                    bool onTub = Mathf.Abs(p.Local.x - pieceTub[i].x) <= half && Mathf.Abs(p.Local.z - pieceTub[i].z) <= half;
                    if (!KnightRoute.Takes(MiniGameParts.Flat(p.Local, pieces[i].localPosition), onTub)) continue;
                    taken |= 1 << i;
                }
            }
            pressed = (KnightRoad.Pressed(t - stoodAt[0]) ? 1 : 0) | (KnightRoad.Pressed(t - stoodAt[1]) ? 2 : 0);
            // up while both are pressed; for good once the game is done (the way to the gate)
            raise = KnightRoad.Raise(raise, pressed == 3 || Completed, Time.fixedDeltaTime);
            if (Completed) return;
            Progress01 = Done / (float)Total;
            if (Done >= Total) Complete();
        }

        // ------------------------------------------------------------------ moments (every PC)

        // StateDrivenMover: the pieces go when the host says they are taken; the pads bounce on the host's launches;
        // the gold tubs' rings and the king's piece follow the pressed state.
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
                else if (i == road.KingPiece) pieces[i].localPosition = PieceRest(i);     // rides its tub
                // R687: the mark over a piece still to take turns and bobs (the king's goes down and up with its tub)
                if (marks[i] == null) continue;
                if (marks[i].gameObject.activeSelf == now) marks[i].gameObject.SetActive(!now);
                if (now) continue;
                marks[i].localPosition = pieces[i].localPosition + Vector3.up * (PieceHeight + .55f + .08f * Mathf.Sin(t * 3f + i));
                marks[i].localRotation = Quaternion.Euler(0f, t * 120f + i * 40f, 0f);
            }
            shownTaken = taken;
            if (launches != shownLaunches)
            {
                shownLaunches = launches;
                if (launchSquare >= 0 && launchSquare < pads.Length)
                {
                    bounceAt[launchSquare] = t;
                    var at = World(pads[launchSquare].localPosition);
                    // a station may play its own picture (J's soapy jet, MiniGameLaundryFx); else the shared burst and ring
                    var own = station != null ? station.GetComponent<IWaterGameFx>() : null;
                    if (own != null) own.PadLaunch(at);
                    else
                    {
                        ArenaFx.Burst(at + Vector3.up * .1f, new[] { new Color(.8f, .72f, .6f), Arena.Gold }, 8, 1.4f, 1.5f, .5f, .07f);
                        ArenaFx.Ring(at, 0f, 1.6f, .5f, Arena.Gold);
                    }
                    ArenaFx.Sfx("sfx_pad_launch", at);
                }
            }
            for (int i = 0; i < pads.Length; i++)
            {
                float k = (t - bounceAt[i]) / .2f;
                float y = k >= 0f && k < 1f ? 1f + .8f * Mathf.Sin(k * Mathf.PI) : 1f;
                pads[i].localScale = new Vector3(1f, y, 1f);
            }
            Pressing(t);
        }

        // R686: a gold tub clicks and its ring swells as it is pressed; the king's tub comes up through the water with a
        // splash once both are, and goes under with a quieter one.
        void Pressing(float t)
        {
            for (int k = 0; k < 2; k++)
            {
                bool now = (pressed >> k & 1) != 0, was = (shownPressed >> k & 1) != 0;
                if (pressRings[k] != null)
                {
                    float beat = now ? 1.08f + .05f * Mathf.Sin(t * 6f) : 1f;
                    pressRings[k].localScale = new Vector3(beat, 1f, beat);
                }
                if (now == was) continue;
                var at = World(pressAt[k] + Vector3.up * .1f);
                if (now)
                {
                    ArenaFx.Ring(at, .4f, road.SquareSize * .75f, .5f, Arena.Gold);
                    ArenaFx.Sfx("sfx_ratchet_click", at);
                }
            }
            shownPressed = pressed;
            bool up = shownRaise > .5f;
            if (up != shownUp)
            {
                shownUp = up;
                var at = World(new Vector3(kingRest.x, Surface, kingRest.z));
                var own = station != null ? station.GetComponent<IWaterGameFx>() : null;
                if (own != null) own.Splash(at, up ? 1.2f : .6f);
                else
                {
                    ArenaFx.Burst(at, new[] { Arena.Hex("#7FC8E8"), Color.white }, up ? 26 : 12, 1.6f, up ? 4.5f : 2f, .8f, .08f);
                    ArenaFx.Ring(at, .4f, road.SquareSize, .7f, up ? Arena.Gold : Arena.Hex("#BFE6F5"));
                }
                ArenaFx.Sfx("sfx_moat_water", at, volume: up ? 1f : .5f);
            }
        }

        void Take(int i)
        {
            takenAt[i] = Time.time;
            takenFrom[i] = pieces[i].localPosition;
            // Knocked away from the nearest pawn this PC draws (toward the water).
            var away = takenFrom[i] - (i == road.KingPiece ? kingRest + Vector3.back : Square(road.PieceSquares[i]));
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
            var own = station.GetComponent<IWaterGameFx>();
            if (own != null) own.PieceTaken(World(takenFrom[i]), transform.TransformDirection(takenDir[i]));
            else
            {
                ArenaFx.Burst(at, new[] { MiniGameParts.PieceColor(Opponent), station.Accent(Opponent), Arena.Gold }, 22, 2.4f, 3f, 1f, .1f);
                ArenaFx.Ring(World(takenFrom[i] + Vector3.up * .05f), 0f, 1.8f, .5f, Arena.Gold);
            }
            ArenaFx.Sfx("sfx_ep_capture", at, pitch: Mathf.Pow(2f, (Done - Total) / 12f));
        }

        // Taken: tips over and flies 3 m off the road into the water, where it sinks.
        void Fly(int i, float t)
        {
            float k = Mathf.Clamp01((t - takenAt[i]) / 1f);
            var to = takenFrom[i] + takenDir[i] * 3f;
            to.y = Surface - .2f;
            var p = Vector3.Lerp(takenFrom[i], to, k);
            p.y += 1.6f * 4f * k * (1f - k);
            if (k >= 1f) p.y -= Mathf.Min(1.5f, (t - takenAt[i] - 1f) * .8f);
            pieces[i].localPosition = p;
            pieces[i].localRotation = Quaternion.AngleAxis(90f * Mathf.Min(1f, k * 2f), Vector3.Cross(Vector3.up, takenDir[i])) * Quaternion.Euler(0f, 360f * k, 0f);
            if (k >= 1f && !splashed[i])
            {
                splashed[i] = true;
                var at = World(new Vector3(to.x, Surface, to.z));
                var own = station.GetComponent<IWaterGameFx>();
                if (own != null) own.Splash(at, .7f);
                else ArenaFx.Burst(at, new[] { Arena.Hex("#7FC8E8"), Color.white }, 16, 1.4f, 4f, .8f, .07f);
                ArenaFx.Sfx("sfx_splash", at, volume: .7f);
            }
        }

        void Restore(int i)
        {
            pieces[i].localPosition = PieceRest(i);
            pieces[i].localRotation = Quaternion.identity;
            if (pieceColliders[i] != null) pieceColliders[i].enabled = true;
            takenAt[i] = -10f;
        }

        // ------------------------------------------------------------------ the wire (5 bytes)

        public override void WriteNet(ref NetBlock block)
        {
            block.U8(taken);
            block.U8(launches);
            block.U8(launchSquare);
            block.U8(pressed);
            block.U8(Mathf.RoundToInt(Mathf.Clamp01(raise) * 255f));
        }

        public override void ReadNet(ref NetBlock block)
        {
            taken = block.ReadU8();
            launches = block.ReadU8();
            launchSquare = block.ReadU8();
            pressed = block.ReadU8() & 3;
            raise = block.ReadU8() / 255f;
        }

        public override void ResetGame()
        {
            base.ResetGame();
            taken = 0;
            pressed = 0;
            raise = 0f;
            stoodAt[0] = stoodAt[1] = -100f;
            lastTub.Clear();
        }
    }
}
