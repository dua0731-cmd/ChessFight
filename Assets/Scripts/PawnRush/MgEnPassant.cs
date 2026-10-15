using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // F, en passant hunt (mini-games v1.0 §4): sixteen statues of the other side's pawn step two squares south on
    // a 14 s cycle with staggered offsets; for 1.2 s after the step (plus the host's 0.15 s grace) a dive or a
    // fast run (5 m/s: a pawn's two steps count, a knight's landing within reach counts) takes one. Sixteen taken:
    // the gate opens.
    //
    // Network (§1.2, §4.7): every statue's place, its warning and its window are functions of the shared obstacle
    // clock (EnPassantRules), worked out on every PC; a client reads the clock where it draws the pawns, so a
    // statue it sees step is where the host's statue stepped. The statues are kinematic and moved with
    // MovePosition, never pushed. Only the host judges; it sends which statues are taken (16 bits), how many,
    // and a bounce counter. A client plays the capture and the clank from the bits that change.
    public sealed class MgEnPassant : MiniGameBase, IProgressCount
    {
        public const float StatueHeight = .9f, StatueRadius = .38f, TrayV = 9.2f;
        const float TipSeconds = .4f, FlySeconds = .8f, FlyApex = 1.5f, LandingGrace = .15f;

        [SerializeField] MissionStation station;

        Transform[] statues, looks;
        Rigidbody[] bodies;
        Collider[] colliders;
        Renderer[][] skins;
        Renderer[] rings;
        LineRenderer[] halos;
        TextMesh counter;
        Material skin, glowSkin, ring, warnRing;

        readonly bool[] taken = new bool[EnPassantRules.Statues];
        readonly float[] guard = new float[EnPassantRules.Statues];
        readonly float[] takenAt = new float[EnPassantRules.Statues];
        readonly int[] slot = new int[EnPassantRules.Statues];
        readonly Vector3[] fallFrom = new Vector3[EnPassantRules.Statues];
        readonly Vector3[] fallDir = new Vector3[EnPassantRules.Statues];
        readonly bool[] landed = new bool[EnPassantRules.Statues];
        readonly int[] stage = new int[EnPassantRules.Statues];
        readonly float[] secondStep = new float[EnPassantRules.Statues];
        readonly float[] shake = new float[EnPassantRules.Statues];
        // One capture per rook charge (§4.6): the skill use a charge captured on.
        readonly Dictionary<ICharacterDriver, int> chargeTook = new Dictionary<ICharacterDriver, int>();
        int count, bounces, lastBounce = -1;

        public int Done => count;
        public int Total => EnPassantRules.Statues;

        public static MgEnPassant Build(CourseBuilder b, MissionStation station)
        {
            MissionGames.Floor(b, 0f, 20f);
            var game = b.parent.gameObject.AddComponent<MgEnPassant>();
            game.station = station;
            game.Make(b);
            return game;
        }

        int Opponent => station != null && station.Team == Teams.White ? Teams.Black : Teams.White;

        void Make(CourseBuilder b)
        {
            int n = EnPassantRules.Statues;
            float m = MiniGameParts.Mirror(station);
            var lanes = b.Group("Lanes");
            for (int i = 0; i < n; i++)
            {
                var home = EnPassantRules.Home(i);
                for (int s = 0; s < 3; s++)
                {
                    float u = home.U - s * 1.1f, v = home.V * m;
                    Arena.Picture(lanes, "Lane " + i + " square " + s, new Vector3(v - .55f, 0f, u - .55f), new Vector3(v + .55f, .04f, u + .55f), Arena.Wood(((i + s) & 1) == 0));
                }
            }

            // The tray for the taken pawns, on the east side (west for black), with its count.
            float tv = TrayV * m;
            b.Box("Tray", new Vector3(tv - .7f, 0f, 3f), new Vector3(tv + .7f, .3f, 19f), Arena.Wood(false), true);
            Arena.Picture(b.parent, "Tray rim W", new Vector3(tv - .74f, 0f, 2.96f), new Vector3(tv - .66f, .34f, 19.04f), Arena.TrimGold);
            Arena.Picture(b.parent, "Tray rim E", new Vector3(tv + .66f, 0f, 2.96f), new Vector3(tv + .74f, .34f, 19.04f), Arena.TrimGold);
            counter = Arena.Text(b.parent, "Tray count", new Vector3(tv, 1.5f, 19f), .5f, Arena.TeamColor(station.Team));

            var them = Opponent;
            skin = MiniGameParts.PieceMaterial(them);
            glowSkin = Arena.Lit("M_PieceWindow", Color.Lerp(MiniGameParts.PieceColor(them), Arena.Gold, .35f), .3f, .4f);
            ring = Arena.Lit("M_PieceRing", Arena.TeamColor(them), .4f, .5f);
            warnRing = Arena.Lit("M_PieceRingWarn", Color.Lerp(Arena.TeamColor(them), Color.black, .55f), .2f, .5f);
            statues = new Transform[n];
            looks = new Transform[n];
            bodies = new Rigidbody[n];
            colliders = new Collider[n];
            skins = new Renderer[n][];
            rings = new Renderer[n];
            halos = new LineRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = MiniGameParts.Piece(b.parent, "Statue " + (i + 1), PieceKind.Pawn, StatueHeight, skin, Arena.TeamColor(them), true, StatueRadius);
                statues[i] = go.transform;
                statues[i].localPosition = MiniGameParts.Local(station, EnPassantRules.Home(i));
                looks[i] = go.transform.Find("Look");
                bodies[i] = go.GetComponent<Rigidbody>();
                colliders[i] = go.GetComponent<Collider>();
                var all = new List<Renderer>();
                foreach (var r in looks[i].GetComponentsInChildren<Renderer>())
                {
                    if (r.name == "Ring") rings[i] = r;
                    else all.Add(r);
                }
                skins[i] = all.ToArray();
                halos[i] = MiniGameParts.Loop(looks[i], "Window halo", new Vector3(0f, StatueHeight + .2f, 0f), .55f, .07f, Arena.Gold);
                halos[i].transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
                halos[i].enabled = false;
            }
            ShowCount();
        }

        Vector3 StatueLocal(int i, double now) => MiniGameParts.Local(station, EnPassantRules.Position(now, i));

        Vector3 World(Vector3 local) => transform.TransformPoint(local);

        void FixedUpdate()
        {
            if (station == null || statues == null) return;
            double now = ObstacleClock.Now;
            for (int i = 0; i < statues.Length; i++)
                if (!taken[i] && bodies[i] != null) bodies[i].MovePosition(World(StatueLocal(i, now)));
            if (Remote || Completed) return;
            Judge(now);
        }

        // §4.3, on the host every physics step.
        void Judge(double now)
        {
            var pawns = station.Pawns();
            float t = Time.fixedTime;
            foreach (var p in pawns)
            {
                var mg = p.Driver as IMiniGamePawn;
                bool lunging = MiniGameParts.Lunging(p, EnPassantRules.Speed);
                // A knight's leap that just landed counts where it landed (§4.6).
                bool knight = mg != null && mg.KnightLandedAt >= 0f && Time.time - mg.KnightLandedAt <= LandingGrace;
                var knightAt = knight ? transform.InverseTransformPoint(mg.KnightLandedWhere) : Vector3.zero;
                if (!lunging && !knight) continue;
                if (mg != null && mg.Down) continue;
                for (int i = 0; i < statues.Length; i++)
                {
                    if (taken[i] || guard[i] > t) continue;
                    var at = StatueLocal(i, now);
                    bool near = MiniGameParts.Flat(p.Local, at) <= EnPassantRules.Reach;
                    bool landedNear = knight && MiniGameParts.Flat(knightAt, at) <= EnPassantRules.Reach;
                    if (!near && !landedNear) continue;
                    guard[i] = t + EnPassantRules.Repeat;
                    var away = p.Local - at;
                    away.y = 0f;
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.back;
                    bool open = EnPassantRules.InWindow(EnPassantRules.Phase(now, i), EnPassantRules.Grace);
                    bool rook = mg != null && mg.SkillDashing && MiniGameParts.PieceOf(p.Driver) == PieceKind.Rook;
                    bool chargeSpent = rook && chargeTook.TryGetValue(p.Driver, out int use) && use == mg.SkillUses;
                    if (p.Own && open && !chargeSpent)
                    {
                        if (rook) chargeTook[p.Driver] = mg.SkillUses;
                        Take(i, -away);
                        if (count >= EnPassantRules.Statues) { Progress01 = 1f; Complete(); return; }
                        break;
                    }
                    // Out of the window, an enemy, or a second statue in one charge: the pawn bounces off, upright.
                    if (mg != null) mg.AddVelocity(transform.TransformDirection(away) * EnPassantRules.Bounce);
                    bounces = (bounces + 1) & 15;
                    lastBounce = i;
                    Clank(i);
                    break;
                }
            }
            Progress01 = count / (float)EnPassantRules.Statues;
        }

        void Take(int i, Vector3 fall)
        {
            taken[i] = true;
            slot[i] = count++;
            takenAt[i] = Time.time;
            fallFrom[i] = statues[i].localPosition;
            fallDir[i] = fall.sqrMagnitude > 1e-4f ? fall.normalized : Vector3.forward;
            landed[i] = false;
            if (colliders[i] != null) colliders[i].enabled = false;
            if (bodies[i] != null) bodies[i].interpolation = RigidbodyInterpolation.None;
            halos[i].enabled = false;
            Paint(i, false, false);
            var at = World(fallFrom[i] + Vector3.up * .5f);
            ArenaFx.Burst(at, new[] { MiniGameParts.PieceColor(Opponent), Arena.TeamColor(Opponent), Arena.Gold }, 22, 2.5f, 3f, 1f, .1f);
            ArenaFx.Ring(World(fallFrom[i] + Vector3.up * .05f), 0f, 1.8f, .5f, Arena.Gold);
            ArenaFx.Sfx("sfx_ep_capture", at);
            ShowCount();
        }

        void Clank(int i)
        {
            shake[i] = .1f;
            ArenaFx.Burst(World(statues[i].localPosition + Vector3.up * .2f), new[] { new Color(.55f, .55f, .55f) }, 8, 1.2f, .8f, .6f, .07f);
            ArenaFx.Sfx("sfx_ep_clank", World(statues[i].localPosition));
        }

        Vector3 TraySpot(int k) => new Vector3(TrayV * MiniGameParts.Mirror(station), .3f + StatueRadius * .6f, 3.4f + k * 1f);

        void Update()
        {
            if (statues == null) return;
            double now = ObstacleClock.Now;
            for (int i = 0; i < statues.Length; i++)
            {
                if (taken[i]) { Fly(i); continue; }
                float ph = EnPassantRules.Phase(now, i);
                Moments(i, ph);
                // The bob before the step, the hop on landing and a bounce's shake are the look's, not the body's.
                float y = 0f;
                if (EnPassantRules.Warning(ph)) y = .05f * Mathf.Abs(Mathf.Sin((ph - EnPassantRules.Warn) * Mathf.PI * 6f));
                else if (ph >= EnPassantRules.Dash && ph < EnPassantRules.Dash + .15f) y = .05f * Mathf.Sin((ph - EnPassantRules.Dash) / .15f * Mathf.PI);
                float x = 0f;
                if (shake[i] > 0f)
                {
                    shake[i] -= Time.deltaTime;
                    x = .04f * Mathf.Sin(Time.time * 90f);
                }
                looks[i].localPosition = new Vector3(x, y, 0f);
                bool open = EnPassantRules.InWindow(ph);
                bool blink = open && ph > EnPassantRules.WindowEnd - .3f && Mathf.Repeat(Time.time * 8f, 1f) < .5f;
                halos[i].enabled = open && !blink;
                if (open) halos[i].transform.localRotation = Quaternion.Euler(12f, (float)(now * 1080.0 % 360.0), 0f);
                bool warnBlink = EnPassantRules.Warning(ph) && Mathf.Repeat(Time.time * 6f, 1f) < .5f;
                Paint(i, open, warnBlink);
            }
        }

        // The statue's clock moments, played on every PC from the shared clock (§4.5).
        void Moments(int i, float ph)
        {
            int now = EnPassantRules.Warning(ph) ? 1 : ph < EnPassantRules.Dash ? 2 : EnPassantRules.InWindow(ph) ? 3 : 0;
            if (secondStep[i] > 0f && Time.time >= secondStep[i])
            {
                secondStep[i] = 0f;
                ArenaFx.Sfx("sfx_ep_step", World(statues[i].localPosition), pitch: .95f);
            }
            if (now == stage[i]) return;
            int before = stage[i];
            stage[i] = now;
            // A statue first seen mid-cycle (a new game, a late join) plays nothing until its next change.
            if (before < 0) return;
            var at = World(statues[i].localPosition);
            if (now == 1) ArenaFx.Sfx("sfx_ep_ready", at, volume: .6f);
            else if (now == 2)
            {
                ArenaFx.Sfx("sfx_ep_step", at);
                secondStep[i] = Time.time + .15f;
            }
            else if (now == 3)
            {
                ArenaFx.Burst(at + Vector3.up * .05f, new[] { new Color(.8f, .72f, .6f) }, 4, .6f, .4f, .5f, .06f);
                ArenaFx.Sfx("sfx_ep_window", at, volume: .8f);
            }
        }

        void Paint(int i, bool glow, bool warn)
        {
            var body = glow ? glowSkin : skin;
            foreach (var r in skins[i]) if (r.sharedMaterial != body) r.sharedMaterial = body;
            if (rings[i] != null)
            {
                var want = warn ? warnRing : ring;
                if (rings[i].sharedMaterial != want) rings[i].sharedMaterial = want;
            }
        }

        // Taken: tips over away from the hit in 0.4 s, flies to its tray place in 0.8 s (1.5 m up, a quarter turn)
        // and lies there.
        void Fly(int i)
        {
            var s = statues[i];
            float age = Time.time - takenAt[i];
            var axis = Vector3.Cross(Vector3.up, fallDir[i]);
            var lying = Quaternion.AngleAxis(90f, axis);
            var spot = TraySpot(slot[i]);
            looks[i].localPosition = Vector3.zero;
            if (age < TipSeconds)
            {
                float k = age / TipSeconds;
                s.localRotation = Quaternion.AngleAxis(90f * k * k, axis);
                s.localPosition = fallFrom[i];
                return;
            }
            float f = Mathf.Clamp01((age - TipSeconds) / FlySeconds);
            var flat = Vector3.Lerp(fallFrom[i], spot, f);
            flat.y = Mathf.Lerp(fallFrom[i].y, spot.y, f) + FlyApex * 4f * f * (1f - f);
            s.localPosition = flat;
            // From lying where it fell to lying across the tray: about a quarter turn in the air.
            s.localRotation = Quaternion.Slerp(lying, Quaternion.AngleAxis(90f, Vector3.forward), f);
            if (f >= 1f && !landed[i])
            {
                landed[i] = true;
                ArenaFx.Sfx("sfx_ep_tray", World(spot));
            }
        }

        void ShowCount()
        {
            if (counter != null) counter.text = $"잡은 폰 {count} / {EnPassantRules.Statues}";
        }

        public override void WriteNet(ref NetBlock block)
        {
            int bits = 0;
            for (int i = 0; i < taken.Length; i++) if (taken[i]) bits |= 1 << i;
            block.U16(bits);
            block.U8(count);
            block.U8(lastBounce < 0 ? 0 : bounces << 4 | lastBounce);
        }

        public override void ReadNet(ref NetBlock block)
        {
            int bits = block.ReadU16();
            block.ReadU8();   // the host's count; the bits say the same
            int bounce = block.ReadU8();
            if (statues == null) return;
            // New bits in index order take the next tray places (§4.7).
            for (int i = 0; i < taken.Length; i++)
            {
                bool now = (bits >> i & 1) != 0;
                if (now == taken[i]) continue;
                if (now) Take(i, FallAway(i));
                else Restore(i);
            }
            if (bounce != 0 && bounce >> 4 != bounces)
            {
                bounces = bounce >> 4;
                Clank(bounce & 15);
            }
        }

        // A client does not know which pawn hit: the statue falls away from the nearest one it draws.
        Vector3 FallAway(int i)
        {
            var at = statues[i].localPosition;
            Vector3 best = Vector3.forward;
            float d = 3f;
            foreach (var p in station.Pawns())
            {
                float f = MiniGameParts.Flat(p.Local, at);
                if (f < d) { d = f; best = at - p.Local; best.y = 0f; }
            }
            return best;
        }

        void Restore(int i)
        {
            taken[i] = false;
            if (colliders[i] != null) colliders[i].enabled = true;
            if (bodies[i] != null) bodies[i].interpolation = RigidbodyInterpolation.Interpolate;
            statues[i].localRotation = Quaternion.identity;
            statues[i].localPosition = StatueLocal(i, ObstacleClock.Now);
            stage[i] = -1;
            count = 0;
            for (int k = 0; k < taken.Length; k++) if (taken[k]) count++;
            ShowCount();
        }

        public override void ResetGame()
        {
            base.ResetGame();
            for (int i = 0; i < taken.Length; i++)
            {
                guard[i] = 0f;
                if (taken[i]) Restore(i);
            }
            count = 0;
            bounces = 0;
            lastBounce = -1;
            chargeTook.Clear();
            ShowCount();
        }

        void Awake()
        {
            for (int i = 0; i < stage.Length; i++) stage[i] = -1;
        }
    }
}
