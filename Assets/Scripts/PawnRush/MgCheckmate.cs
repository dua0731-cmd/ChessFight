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
    //
    // R543 (user 11-11: "체크메이트 몰이는 도망다니는 킹의 개수를 조절해줘"): the kings go with the team's size - four, five,
    // six (as before) or seven (MiniGameScale.CheckmateKings), the first so many of CheckmateHunt.Starts.
    public sealed class MgCheckmate : MiniGameBase, IProgressCount
    {
        public const float KingHeight = .9f, KingRadius = .35f, TrayV = 9.6f;

        // R510 art hook (MiniGameHallBell): a king took a check (false) or froze (true). Raised from Present, so on every
        // PC from its own view of the hunt; pictures only, the rule and the network do not use it.
        public static event System.Action<bool> KingChecked;

        // R510: H's own soft particle effects (CheckmateFx) in place of ArenaFx's bursts, ring and solid beam; added on
        // first use, so the saved prefab carries nothing new. Sounds stay ArenaFx.Sfx.
        CheckmateFx fx;
        CheckmateFx Fx => fx != null ? fx : (fx = gameObject.AddComponent<CheckmateFx>());
        const float HopSeconds = .35f, HopApex = .4f, FlySeconds = .9f, FlyApex = 2f, Vanish = .3f;

        [SerializeField] MissionStation station;

        // The hunt of this game's kings (made with the game: Make).
        CheckmateHunt hunt = new CheckmateHunt();
        readonly CheckmateHunt.King[] view = new CheckmateHunt.King[CheckmateHunt.MaxKings];
        readonly CheckmateHunt.King[] remote = new CheckmateHunt.King[CheckmateHunt.MaxKings];
        readonly List<StationPoint> ours = new List<StationPoint>(), theirs = new List<StationPoint>();
        bool started, receivedState;
        int captured;
        float nextHopSound, nextCheckSound;
        string captureNotice;
        float noticeUntil;
        const string RevealSfx = "Loading/CourseReveal/Sound/", BoardSfx = "PawnRushMiniGameSfx/";

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
        readonly Vector3[] from = new Vector3[CheckmateHunt.MaxKings], to = new Vector3[CheckmateHunt.MaxKings];
        readonly float[] movedAt = new float[CheckmateHunt.MaxKings], flinchAt = new float[CheckmateHunt.MaxKings], takenAt = new float[CheckmateHunt.MaxKings];
        readonly bool[] jumped = new bool[CheckmateHunt.MaxKings], landed = new bool[CheckmateHunt.MaxKings];
        readonly int[] slot = new int[CheckmateHunt.MaxKings];
        readonly float[] nextTrailAt = new float[CheckmateHunt.MaxKings];

        public int Done => captured;
        public int Total => hunt.Count;
        public string HuntHint
        {
            get
            {
                if (Time.time < noticeUntil) return captureNotice;
                for (int k = 0; k < hunt.Count; k++)
                    if (view[k].State == CheckmateHunt.KingState.Frozen) return "금빛 킹에게 다이빙하거나 질주하세요";
                return "킹의 퇴로를 막아 체크하세요";
            }
        }

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
            var team = station.Accent(station.Team);
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
            // From u 2.75: nothing solid in the entrance band (u 0..2.5, mini-games v1.0 §1.5).
            for (float u = PawnRushBoard.U0 + .75f; u <= 20f - .3f; u += 1.5f)
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
            hunt = new CheckmateHunt(MiniGameScale.CheckmateKings(station.TeamSize));
            int n = hunt.Count;
            kings = new Transform[n];
            looks = new Transform[n];
            bodies = new Rigidbody[n];
            colliders = new Collider[n];
            skins = new Renderer[n][];
            dots = new Renderer[n][];
            halos = new LineRenderer[n];
            for (int k = 0; k < n; k++)
            {
                var go = MiniGameParts.Piece(b.parent, "King " + (k + 1), PieceKind.King, KingHeight, skin, station.Accent(them), true, KingRadius);
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
            receivedState = false;
            captured = 0;
            noticeUntil = nextHopSound = nextCheckSound = 0f;
            for (int k = 0; k < hunt.Count; k++)
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
                for (int k = 0; k < hunt.Count; k++)
                    hunt.TryCapture(k, at, true, 0f);
            }
            for (int k = 0; k < hunt.Count; k++) Present(k, hunt.All[k]);
            Progress01 = captured / (float)hunt.Count;
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
                    Fx.Vanish(at);
                    Fx.Vanish(World(to[k]));
                    Sound("sfx_stalemate", RevealSfx + "RevealOpen", at, .31f, 1.12f);
                }
                landed[k] = false;
            }
            if (now.State == CheckmateHunt.KingState.Free && was.State == CheckmateHunt.KingState.Free && now.Checks > was.Checks)
            {
                flinchAt[k] = Time.time;
                Fx.Check(at, PawnRushBoard.Cell, transform.eulerAngles.y);
                if (Time.time >= nextCheckSound)
                {
                    Sound("sfx_check", RevealSfx + "RevealTick2", at, .48f, .90f + .14f * now.Checks);
                    nextCheckSound = Time.time + .07f;
                }
                KingChecked?.Invoke(false);
            }
            if (now.State == CheckmateHunt.KingState.Frozen && was.State == CheckmateHunt.KingState.Free)
            {
                Fx.Freeze(at, PawnRushBoard.Cell, transform.eulerAngles.y);
                Sound("sfx_mate", RevealSfx + "RevealLock", at, .65f, .85f);
                KingChecked?.Invoke(true);
            }
            if (now.State == CheckmateHunt.KingState.Free && was.State == CheckmateHunt.KingState.Frozen)
            {
                Fx.Rescue(at, station.Accent(station.Team == Teams.White ? Teams.Black : Teams.White));
                Sound("sfx_rescue", BoardSfx + "sfx_paint_blocked", at, .38f, 1.18f);
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

        static void Sound(string name, string fallback, Vector3 at, float gain, float pitch = 1f) =>
            ArenaFx.Sfx(name, at, volume: gain * ChessFight.Game.GameSettings.Effects, pitch: pitch, fallbackPath: fallback);

        void Take(int k, bool instant = false)
        {
            slot[k] = captured++;
            takenAt[k] = instant ? Time.time - FlySeconds - .1f : Time.time;
            nextTrailAt[k] = Time.time;
            from[k] = kings[k].localPosition;
            landed[k] = instant;
            if (colliders[k] != null) colliders[k].enabled = false;
            if (bodies[k] != null) bodies[k].interpolation = RigidbodyInterpolation.None;
            halos[k].enabled = false;
            if (instant) { Fly(k); return; }
            var at = World(from[k] + Vector3.up * .5f);
            Fx.Capture(World(from[k]), PawnRushBoard.Cell, transform.eulerAngles.y, station.Accent(station.Team));
            bool final = captured == hunt.Count;
            if (final) Sound("sfx_king_sweep", BoardSfx + "sfx_paint_team_complete", at, .82f);
            else Sound("sfx_king_capture", RevealSfx + "RevealSlam", at, .68f, .92f + .04f * captured);
            captureNotice = $"킹 포획   {captured} / {hunt.Count}";
            noticeUntil = Time.time + 1.2f;
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

        // Six lie 1.3 m apart on the 8 m tray (u 6.6 .. 13.1); seven a little closer.
        Vector3 TraySpot(int k) =>
            new Vector3(TrayV * MiniGameParts.Mirror(station), .3f + KingRadius * .6f, 6.6f + k * Mathf.Min(1.3f, 6.5f / Mathf.Max(1, hunt.Count - 1)));

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
                        Fx.Land(at);
                        if (Time.time >= nextHopSound)
                        {
                            Sound("sfx_king_hop", RevealSfx + "RevealDot", at, .24f, .82f);
                            nextHopSound = Time.time + .12f;
                        }
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
                if (frozen)
                {
                    float pulse = .5f + .5f * Mathf.Sin(t * 5f);
                    var halo = halos[k].transform;
                    halo.localScale = Vector3.one * (1.08f + .28f * pulse);
                    halo.localRotation = Quaternion.Euler(25f, t * 130f % 360f, 0f);
                }
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
            if (f < 1f && Time.time >= nextTrailAt[k])
            {
                nextTrailAt[k] = Time.time + .05f;
                Fx.CaptureTrail(World(p + Vector3.up * .35f), station.Accent(station.Team));
            }
            if (f >= 1f && !landed[k])
            {
                landed[k] = true;
                Fx.TrayLand(World(spot), station.Accent(station.Team));
                Sound("sfx_ep_tray", BoardSfx + "sfx_ratchet_click", World(spot), .34f, .9f);
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
            if (kings == null || !block.Left(hunt.Count * 2 + 1)) return;
            for (int k = 0; k < hunt.Count; k++) CheckmateHunt.Unpack((ushort)block.ReadU16(), remote[k]);
            block.ReadU8();
            // A reconnect first sees a whole board, possibly already cleared. Establish that picture silently.
            if (!receivedState)
            {
                receivedState = true;
                for (int k = 0; k < hunt.Count; k++)
                {
                    Copy(remote[k], view[k]);
                    to[k] = from[k] = Square(view[k].File, view[k].Rank);
                    kings[k].localPosition = to[k];
                    Paint(k);
                    if (view[k].State == CheckmateHunt.KingState.Captured) Take(k, true);
                }
                return;
            }
            for (int k = 0; k < hunt.Count; k++) Present(k, remote[k]);
        }

        public override void ResetGame()
        {
            base.ResetGame();
            if (kings != null) Restart();
        }
    }
}
