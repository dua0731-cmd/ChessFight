using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // H, checkmate hunt (mini-games v1.0 §6): six of the other side's kings flee over an 8 x 8 board (u 2..20,
    // v -9..9) with three stone pillars (c6, f3, f7). Each of our standing pawns attacks its square and the
    // eight round it; a king on its turn under attack takes a check, four checks or a mate freeze it, and a
    // dive (or a 5 m/s run) onto a frozen king takes it. An enemy pawn at a frozen king frees it.
    //
    // The kings' minds are CheckmateHunt (Core), run on the host only. Network (§6.6): 2 bytes a king (square,
    // checks, state, immunity) and the count. A client hops its king when the square changes and plays every
    // moment from what changed (a check, a freeze, a capture, a rescue, a stalemate jump); the attacked-square
    // plates are worked out on every PC from the pawns it draws. The kings are kinematic and never pushed.
    public sealed class MgCheckmate : MiniGameBase, IProgressCount
    {
        public const float KingHeight = .9f, KingRadius = .35f, TrayV = 9.6f;
        const float HopSeconds = .35f, HopApex = .4f, FlySeconds = .9f, FlyApex = 2f, Vanish = .3f;

        [SerializeField] MissionStation station;

        readonly CheckmateHunt hunt = new CheckmateHunt();
        readonly CheckmateHunt.King[] view = new CheckmateHunt.King[CheckmateHunt.Kings];
        readonly CheckmateHunt.King[] remote = new CheckmateHunt.King[CheckmateHunt.Kings];
        readonly List<StationPoint> ours = new List<StationPoint>(), theirs = new List<StationPoint>();
        bool started;
        int captured;

        Transform[] kings, looks;
        Rigidbody[] bodies;
        Collider[] colliders;
        Renderer[][] skins;
        Renderer[][] dots;
        LineRenderer[] halos;
        Transform[] attack;
        readonly float[] attackShown = new float[64];
        readonly bool[] attacked = new bool[64];
        Material skin, gold, dotOff, dotOn;

        // The look of each king's last move: from, to, when (Time.time), a stalemate jump or a hop.
        readonly Vector3[] from = new Vector3[CheckmateHunt.Kings], to = new Vector3[CheckmateHunt.Kings];
        readonly float[] movedAt = new float[CheckmateHunt.Kings], flinchAt = new float[CheckmateHunt.Kings], takenAt = new float[CheckmateHunt.Kings];
        readonly bool[] jumped = new bool[CheckmateHunt.Kings], landed = new bool[CheckmateHunt.Kings];
        readonly int[] slot = new int[CheckmateHunt.Kings];

        public int Done => captured;
        public int Total => CheckmateHunt.Kings;

        public static MgCheckmate Build(CourseBuilder b, MissionStation station)
        {
            MissionGames.Floor(b, 0f, 20f);
            var game = b.parent.gameObject.AddComponent<MgCheckmate>();
            game.station = station;
            game.Make(b);
            return game;
        }

        int Opponent => station != null && station.Team == Teams.White ? Teams.Black : Teams.White;

        Vector3 Square(int file, int rank, float y = 0f) => MiniGameParts.Local(station, PawnRushBoard.Center(file, rank), y);

        void Make(CourseBuilder b)
        {
            float m = MiniGameParts.Mirror(station);
            var board = b.Group("Board");
            var team = Arena.TeamColor(station.Team);
            var plate = Arena.Glow(new Color(team.r, team.g, team.b, .28f));
            attack = new Transform[64];
            for (int s = 0; s < 64; s++)
            {
                int f = PawnRushBoard.FileOf(s), r = PawnRushBoard.RankOf(s);
                var c = Square(f, r);
                float h = PawnRushBoard.Cell * .5f;
                Arena.Picture(board, "Square " + PawnRushBoard.Name(f, r), c + new Vector3(-h, 0f, -h), c + new Vector3(h, .04f, h), Arena.Wood(PawnRushBoard.IsLight(f, r)));
                // The attacked-square plate (§6.1: 92 % of the square, 0.065 m up, the team's colour at 0.28).
                var a = Arena.Picture(board, "Attacked " + PawnRushBoard.Name(f, r), new Vector3(-.5f, 0f, -.5f), new Vector3(.5f, .01f, .5f), plate);
                a.transform.localPosition = c + new Vector3(0f, .065f, 0f);
                a.SetActive(false);
                attack[s] = a.transform;
            }

            // The fence: wooden posts every 1.5 m 0.2 m outside the east and west edges (the north edge is the gate
            // wall; the south is open). Pawns walk between them.
            var wood = Arena.Wood(false);
            float edge = PawnRushBoard.N * PawnRushBoard.Cell * .5f + .2f;
            for (float u = PawnRushBoard.U0; u <= 20f - .3f; u += 1.5f)
                foreach (float side in new[] { -1f, 1f })
                    b.Box("Fence post", new Vector3(side * edge - .075f, 0f, u - .075f), new Vector3(side * edge + .075f, .55f, u + .075f), wood, true);

            var stone = Arena.Lit("M_PillarStone", Arena.Hex("#857E74"), 0f, .9f);
            foreach (var name in CheckmateHunt.Pillars)
            {
                PawnRushBoard.Parse(name, out int f, out int r);
                var c = Square(f, r);
                b.Box("Pillar " + name, c + new Vector3(-.8f, 0f, -.8f), c + new Vector3(.8f, 2.2f, .8f), stone, true);
            }

            // The tray outside the fence on the east (west for black).
            float tv = TrayV * m;
            b.Box("King tray", new Vector3(tv - .35f, 0f, 6f), new Vector3(tv + .35f, .3f, 14f), Arena.Wood(false), true);

            var them = Opponent;
            skin = MiniGameParts.PieceMaterial(them);
            gold = Arena.Emissive(Arena.Gold, .5f);
            dotOff = Arena.Lit("M_CheckDotOff", new Color(.25f, .22f, .22f), 0f, .6f);
            dotOn = Arena.Lit("M_CheckDotOn", Arena.Red, .9f, .4f);
            int n = CheckmateHunt.Kings;
            kings = new Transform[n];
            looks = new Transform[n];
            bodies = new Rigidbody[n];
            colliders = new Collider[n];
            skins = new Renderer[n][];
            dots = new Renderer[n][];
            halos = new LineRenderer[n];
            for (int k = 0; k < n; k++)
            {
                var go = MiniGameParts.Piece(b.parent, "King " + (k + 1), PieceKind.King, KingHeight, skin, Arena.TeamColor(them), true, KingRadius);
                kings[k] = go.transform;
                looks[k] = go.transform.Find("Look");
                bodies[k] = go.GetComponent<Rigidbody>();
                colliders[k] = go.GetComponent<Collider>();
                var list = new List<Renderer>();
                foreach (var r in looks[k].GetComponentsInChildren<Renderer>()) if (r.name != "Ring") list.Add(r);
                skins[k] = list.ToArray();
                // Four check dots over the head.
                dots[k] = new Renderer[CheckmateHunt.ChecksToFreeze];
                for (int d = 0; d < dots[k].Length; d++)
                    dots[k][d] = MiniGameParts.Part(looks[k], PrimitiveType.Sphere, new Vector3((d - 1.5f) * .14f, KingHeight + .28f, 0f), Vector3.one * .09f, dotOff).GetComponent<Renderer>();
                halos[k] = MiniGameParts.Loop(looks[k], "Frozen halo", new Vector3(0f, KingHeight + .18f, 0f), .45f, .06f, Arena.Gold);
                halos[k].transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
                halos[k].enabled = false;
                view[k] = new CheckmateHunt.King();
                remote[k] = new CheckmateHunt.King();
            }
            Restart();
        }

        // Every king back on its square, drawn there, nothing owed.
        void Restart()
        {
            hunt.Reset(0);
            started = false;
            captured = 0;
            for (int k = 0; k < CheckmateHunt.Kings; k++)
            {
                Copy(hunt.All[k], view[k]);
                Copy(hunt.All[k], remote[k]);
                var at = Square(view[k].File, view[k].Rank);
                from[k] = to[k] = at;
                movedAt[k] = -10f;
                flinchAt[k] = -10f;
                jumped[k] = false;
                landed[k] = false;
                kings[k].localPosition = at;
                kings[k].localRotation = Quaternion.identity;
                if (colliders[k] != null) colliders[k].enabled = true;
                if (bodies[k] != null) bodies[k].interpolation = RigidbodyInterpolation.Interpolate;
                Paint(k);
            }
        }

        static void Copy(CheckmateHunt.King a, CheckmateHunt.King b)
        {
            b.File = a.File; b.Rank = a.Rank; b.Checks = a.Checks; b.State = a.State; b.Immune = a.Immune; b.Mated = a.Mated;
        }

        Vector3 World(Vector3 local) => transform.TransformPoint(local);

        // ------------------------------------------------------------------ host

        void FixedUpdate()
        {
            if (station == null || kings == null) return;
            if (!Remote && !Completed) Step();
            for (int k = 0; k < kings.Length; k++)
                if (view[k].State != CheckmateHunt.KingState.Captured && bodies[k] != null) bodies[k].MovePosition(World(Ground(k)));
        }

        void Step()
        {
            double now = Time.fixedTimeAsDouble;
            if (!started)
            {
                hunt.Reset(now);
                started = true;
            }
            ours.Clear();
            theirs.Clear();
            var pawns = station.Pawns();
            foreach (var p in pawns)
            {
                var at = MiniGameParts.Board(station, p.Local);
                if (p.Own)
                {
                    if (!(p.Driver is IMiniGamePawn mg && mg.Down)) ours.Add(at);
                }
                else theirs.Add(at);
            }
            hunt.Step(now, ours, theirs);
            foreach (var p in pawns)
            {
                if (!p.Own || !MiniGameParts.Lunging(p, CheckmateHunt.CaptureSpeed)) continue;
                var at = MiniGameParts.Board(station, p.Local);
                for (int k = 0; k < CheckmateHunt.Kings; k++)
                    hunt.TryCapture(k, at, true, 0f);
            }
            for (int k = 0; k < CheckmateHunt.Kings; k++) Present(k, hunt.All[k]);
            Progress01 = captured / (float)CheckmateHunt.Kings;
            if (hunt.Done) Complete();
        }

        // ------------------------------------------------------------------ moments (every PC)

        // What changed in king k since it was last drawn, played (§6.4).
        void Present(int k, CheckmateHunt.King now)
        {
            var was = view[k];
            var at = World(kings[k].localPosition);
            if (now.State == CheckmateHunt.KingState.Captured)
            {
                if (was.State != CheckmateHunt.KingState.Captured) Take(k);
                Copy(now, was);
                return;
            }
            if (was.State == CheckmateHunt.KingState.Captured) Untake(k);
            if (now.File != was.File || now.Rank != was.Rank)
            {
                from[k] = Ground(k);
                to[k] = Square(now.File, now.Rank);
                movedAt[k] = Time.time;
                // A stalemate jumps the king away (more than one square): it vanishes and turns up there.
                jumped[k] = Mathf.Max(Mathf.Abs(now.File - was.File), Mathf.Abs(now.Rank - was.Rank)) > 1;
                if (jumped[k])
                {
                    var smoke = new[] { Arena.Hex("#7FA8FF") };
                    ArenaFx.Burst(at + Vector3.up * .4f, smoke, 16, 1.2f, 1f, .8f, .12f);
                    ArenaFx.Burst(World(to[k]) + Vector3.up * .4f, smoke, 16, 1.2f, 1f, .8f, .12f);
                    ArenaFx.Sfx("sfx_stalemate", at);
                }
                landed[k] = false;
            }
            if (now.State == CheckmateHunt.KingState.Free && was.State == CheckmateHunt.KingState.Free && now.Checks > was.Checks)
            {
                flinchAt[k] = Time.time;
                ArenaFx.Ring(at, 0f, 1.8f, .5f, Arena.Red);
                ArenaFx.Burst(at + Vector3.up * .7f, new[] { Arena.Red }, 8, 1.4f, 1.6f, .6f, .08f);
                ArenaFx.Sfx("sfx_check", at);
            }
            if (now.State == CheckmateHunt.KingState.Frozen && was.State == CheckmateHunt.KingState.Free)
            {
                ArenaFx.Beam(at, .6f, 1f, 6f, 1f, Arena.Gold);
                ArenaFx.Sfx("sfx_mate", at);
            }
            if (now.State == CheckmateHunt.KingState.Free && was.State == CheckmateHunt.KingState.Frozen)
            {
                ArenaFx.Burst(at + Vector3.up * .5f, new[] { Arena.TeamColor(station.Team == Teams.White ? Teams.Black : Teams.White) }, 10, 1.4f, 1.4f, .7f, .08f);
                ArenaFx.Sfx("sfx_rescue", at);
            }
            Copy(now, was);
            Paint(k);
        }

        void Paint(int k)
        {
            var v = view[k];
            bool frozen = v.State == CheckmateHunt.KingState.Frozen;
            var body = frozen ? gold : skin;
            foreach (var r in skins[k]) if (r.sharedMaterial != body) r.sharedMaterial = body;
            for (int d = 0; d < dots[k].Length; d++)
            {
                var want = d < v.Checks || (frozen && v.Mated) ? dotOn : dotOff;
                if (dots[k][d].sharedMaterial != want) dots[k][d].sharedMaterial = want;
            }
            halos[k].enabled = frozen;
        }

        void Take(int k)
        {
            slot[k] = captured++;
            takenAt[k] = Time.time;
            from[k] = kings[k].localPosition;
            landed[k] = false;
            if (colliders[k] != null) colliders[k].enabled = false;
            if (bodies[k] != null) bodies[k].interpolation = RigidbodyInterpolation.None;
            halos[k].enabled = false;
            var at = World(from[k] + Vector3.up * .5f);
            ArenaFx.Burst(at, new[] { Arena.Gold, Arena.TeamColor(station.Team), Color.white }, 30, 2.5f, 3.5f, 1.1f, .1f);
            ArenaFx.Sfx("sfx_king_capture", at);
        }

        void Untake(int k)
        {
            captured = Mathf.Max(0, captured - 1);
            if (colliders[k] != null) colliders[k].enabled = true;
            if (bodies[k] != null) bodies[k].interpolation = RigidbodyInterpolation.Interpolate;
            kings[k].localRotation = Quaternion.identity;
            to[k] = from[k] = Square(view[k].File, view[k].Rank);
        }

        // Where king k's body stands now: on its way from one square to the next (0.35 s), or, jumping, at the
        // old square until it vanishes and then at the new one.
        Vector3 Ground(int k)
        {
            float age = Time.time - movedAt[k];
            if (jumped[k]) return age < Vanish ? from[k] : to[k];
            return Vector3.Lerp(from[k], to[k], Mathf.Clamp01(age / HopSeconds));
        }

        Vector3 TraySpot(int k) => new Vector3(TrayV * MiniGameParts.Mirror(station), .3f + KingRadius * .6f, 6.6f + k * 1.3f);

        void Update()
        {
            if (kings == null || station == null) return;
            float t = Time.time;
            for (int k = 0; k < kings.Length; k++)
            {
                var look = looks[k];
                if (view[k].State == CheckmateHunt.KingState.Captured) { Fly(k); continue; }
                float age = t - movedAt[k];
                float y = 0f, scale = 1f;
                if (jumped[k])
                {
                    if (age < Vanish) scale = 1f - age / Vanish;
                    else if (age < 2f * Vanish) scale = (age - Vanish) / Vanish;
                }
                else if (age < HopSeconds)
                {
                    float f = age / HopSeconds;
                    y = HopApex * 4f * f * (1f - f);
                }
                if (!landed[k] && age >= (jumped[k] ? 2f * Vanish : HopSeconds) && movedAt[k] > 0f)
                {
                    landed[k] = true;
                    if (!jumped[k])
                    {
                        var at = World(to[k]);
                        ArenaFx.Burst(at + Vector3.up * .05f, new[] { new Color(.8f, .72f, .6f) }, 3, .6f, .4f, .5f, .06f);
                        ArenaFx.Sfx("sfx_king_hop", at, volume: .7f);
                    }
                }
                look.localPosition = new Vector3(0f, y, 0f);
                look.localScale = new Vector3(1f, Mathf.Max(.02f, scale), 1f);
                // A check: tips back 10 degrees and returns in 0.15 s. Frozen: shivers, 2 degrees at 12 Hz.
                float tilt = 0f, shiver = 0f;
                float fl = t - flinchAt[k];
                if (fl < .15f) tilt = -10f * Mathf.Sin(fl / .15f * Mathf.PI);
                bool frozen = view[k].State == CheckmateHunt.KingState.Frozen;
                if (frozen) shiver = 2f * Mathf.Sin(t * 12f * Mathf.PI * 2f);
                look.localRotation = Quaternion.Euler(tilt, 0f, shiver);
                if (frozen) halos[k].transform.localRotation = Quaternion.Euler(14f, t * 1440f % 360f, 0f);
            }
            Attacks();
        }

        // The squares our standing pawns attack, from the pawns this PC draws (§6.6), each plate showing or going
        // in 0.1 s.
        void Attacks()
        {
            for (int s = 0; s < 64; s++) attacked[s] = false;
            foreach (var p in station.Pawns())
            {
                if (!p.Own || p.Driver is IMiniGamePawn mg && mg.Down && !Remote) continue;
                if (!PawnRushBoard.SquareAt(MiniGameParts.Board(station, p.Local), out int f, out int r)) continue;
                for (int df = -1; df <= 1; df++)
                    for (int dr = -1; dr <= 1; dr++)
                        if (PawnRushBoard.OnBoard(f + df, r + dr)) attacked[PawnRushBoard.Index(f + df, r + dr)] = true;
            }
            float step = Time.deltaTime / .1f;
            float size = PawnRushBoard.Cell * .92f;
            for (int s = 0; s < 64; s++)
            {
                attackShown[s] = Mathf.MoveTowards(attackShown[s], attacked[s] ? 1f : 0f, step);
                bool show = attackShown[s] > .01f;
                var a = attack[s];
                if (a.gameObject.activeSelf != show) a.gameObject.SetActive(show);
                if (show) a.localScale = new Vector3(size * attackShown[s], .01f, size * attackShown[s]);
            }
        }

        // Taken: 0.9 s up (2 m) and over to its place in the tray, where it lies.
        void Fly(int k)
        {
            var s = kings[k];
            looks[k].localPosition = Vector3.zero;
            looks[k].localScale = Vector3.one;
            looks[k].localRotation = Quaternion.identity;
            float f = Mathf.Clamp01((Time.time - takenAt[k]) / FlySeconds);
            var spot = TraySpot(slot[k]);
            var p = Vector3.Lerp(from[k], spot, f);
            p.y = Mathf.Lerp(from[k].y, spot.y, f) + FlyApex * 4f * f * (1f - f);
            s.localPosition = p;
            s.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.AngleAxis(90f, Vector3.forward), f);
            if (f >= 1f && !landed[k])
            {
                landed[k] = true;
                ArenaFx.Sfx("sfx_ep_tray", World(spot));
            }
        }

        // ------------------------------------------------------------------ the wire (§6.6, 13 bytes)

        public override void WriteNet(ref NetBlock block)
        {
            foreach (var k in hunt.All) block.U16(CheckmateHunt.Pack(k));
            block.U8(captured);
        }

        public override void ReadNet(ref NetBlock block)
        {
            for (int k = 0; k < CheckmateHunt.Kings; k++) CheckmateHunt.Unpack((ushort)block.ReadU16(), remote[k]);
            block.ReadU8();
            if (kings == null) return;
            for (int k = 0; k < CheckmateHunt.Kings; k++) Present(k, remote[k]);
        }

        public override void ResetGame()
        {
            base.ResetGame();
            if (kings != null) Restart();
        }
    }
}
