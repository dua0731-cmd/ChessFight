using System.Collections.Generic;
using System.Linq;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // What the last scene's pieces and camera do, as a function of the time since the
    // result began (so a replay is the same show). The beats are the ones on the design
    // samples (Docs/Architecture/UI.md "결과 화면", beat table).
    public abstract class LastSceneCeremony
    {
        // My own piece, for the marker over it; null when it is not on stage.
        public LastSceneFigure Self { get; protected set; }

        // `time` runs on, `tp` restarts with each replay, `shift` 0..1 makes room for the board.
        public abstract void Update(float time, float tp, float shift, Camera view);

        // The samples were drawn with the camera on +Z; this scene's camera looks along +Z.
        protected static Vector3 P(float x, float y, float z) => new Vector3(x, y, -z);
        protected static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        protected static float BounceOut(float x)
        {
            x = Mathf.Clamp01(x);
            const float n = 7.5625f, d = 2.75f;
            if (x < 1 / d) return n * x * x;
            if (x < 2 / d) { x -= 1.5f / d; return n * x * x + .75f; }
            if (x < 2.5f / d) { x -= 2.25f / d; return n * x * x + .9375f; }
            x -= 2.625f / d;
            return n * x * x + .984375f;
        }

        // A short shake after each event, decaying: (time, strength).
        protected static Vector2 Shake(float tp, List<Vector2> events)
        {
            float x = 0, y = 0;
            foreach (var e in events)
            {
                float d = tp - e.x;
                if (d < 0 || d > 1.2f) continue;
                float a = e.y * Mathf.Exp(-d * 7f);
                x += a * Mathf.Sin(d * 55f + e.x * 7f);
                y += a * Mathf.Cos(d * 47f + e.x * 3f);
            }
            return new Vector2(x, y);
        }

        // Kings and queens in the middle, then rooks, bishops, knights and pawns outward.
        protected static List<ResultPlayer> Lineup(MatchResult result, int team)
        {
            int Rank(PieceKind kind) => kind == PieceKind.King ? 0 : kind == PieceKind.Queen ? 1 : kind == PieceKind.Rook ? 2 : kind == PieceKind.Bishop ? 3 : kind == PieceKind.Knight ? 4 : 5;
            return result.Players.Where(p => p.Team == team).OrderBy(p => Rank(p.Piece)).ThenBy(p => p.Name, System.StringComparer.Ordinal).Take(6).ToList();
        }

        // Each piece's own celebration, `tc` seconds after it started.
        protected static FigurePose Cheer(PieceKind kind, float tc, int i)
        {
            float wave = Mathf.Sin(tc * 8f + i * 1.3f);
            switch (kind)
            {
                case PieceKind.King:
                {
                    // Big jumps, every second one a backflip.
                    const float period = 1.3f, air = .62f;
                    float t = tc + .22f;
                    int n = Mathf.FloorToInt(t / period);
                    float ph = t / period - n, up = ph < air ? Mathf.Sin(ph / air * Mathf.PI) : 0;
                    return new FigurePose
                    {
                        Jump = up * 1.5f, Flip = n % 2 == 1 && ph < air ? -Ease(ph / air) * Mathf.PI * 2 : 0, Arm = 1.2f + wave * .25f, Leg = up * .5f,
                        Squash = ph >= air ? Mathf.Max(0, 1 - (ph - air) / .14f) * .16f : 0
                    };
                }
                case PieceKind.Queen:
                {
                    // Hops while spinning.
                    float ph = tc % .78f / .78f;
                    return new FigurePose { Jump = Mathf.Sin(ph * Mathf.PI) * .55f, Yaw = tc * 4.6f, Arm = 1.05f + Mathf.Sin(tc * 6.5f) * .3f, Leg = Mathf.Sin(ph * Mathf.PI * 2) * .35f, Squash = ph > .92f ? .08f : 0 };
                }
                case PieceKind.Rook:
                {
                    // Heavy stomps that shake the camera.
                    const float period = 1.6f, air = .55f;
                    float ph = (tc + .4f) % period / period, up = ph < air ? Mathf.Sin(ph / air * Mathf.PI) : 0, land = ph >= air ? Mathf.Exp(-(ph - air) * period * 9f) : 0;
                    return new FigurePose { Jump = up * 1.1f, Squash = land * .22f, Arm = .5f + up * .8f, Lean = -up * .1f, Leg = up * .45f };
                }
                case PieceKind.Bishop:
                {
                    // Diagonal hops, like a bishop moves.
                    const float period = .72f;
                    float t = tc + .25f;
                    int n = Mathf.FloorToInt(t / period);
                    float ph = t / period - n, dir = n % 2 == 1 ? 1 : -1, off = (-dir + 2 * dir * Ease(ph)) * .6f;
                    return new FigurePose { Jump = Mathf.Sin(ph * Mathf.PI) * .55f, Dx = off, Dz = off, Arm = .95f + wave * .25f, Yaw = dir * .45f, Leg = Mathf.Sin(ph * Mathf.PI * 2) * .4f };
                }
                case PieceKind.Knight:
                {
                    // An L: two squares forward, one across, then back.
                    Vector2[] path = { new Vector2(0, 0), new Vector2(0, 1.5f), new Vector2(-.8f, 1.5f) };
                    const float period = .62f;
                    float t = tc + .1f;
                    int n = Mathf.FloorToInt(t / period), k = n % 3;
                    float ph = t / period - n, e = Ease(ph);
                    Vector2 a = path[k], b = path[(k + 1) % 3];
                    return new FigurePose
                    {
                        Jump = Mathf.Sin(ph * Mathf.PI) * (k == 0 ? 1f : .65f), Dx = Mathf.Lerp(a.x, b.x, e), Dz = Mathf.Lerp(a.y, b.y, e),
                        Arm = .9f + wave * .3f, Lean = Mathf.Sin(ph * Mathf.PI) * .12f, Leg = Mathf.Sin(ph * Mathf.PI * 2) * .5f
                    };
                }
                default:
                {
                    // A pawn's opening move: two squares forward, then back to try again.
                    float ph = (tc + i * .07f) % 1.25f / 1.25f, dz, jump;
                    if (ph < .28f) { float e = ph / .28f; dz = .75f * Ease(e); jump = Mathf.Sin(e * Mathf.PI) * .6f; }
                    else if (ph < .56f) { float e = (ph - .28f) / .28f; dz = .75f + .75f * Ease(e); jump = Mathf.Sin(e * Mathf.PI) * .6f; }
                    else { float e = (ph - .56f) / .44f; dz = 1.5f * (1 - Ease(e)); jump = Mathf.Abs(Mathf.Sin(e * Mathf.PI * 3)) * .12f; }
                    return new FigurePose { Jump = jump, Dz = dz, Arm = .7f + Mathf.Sin(tc * 10f) * .45f, Leg = Mathf.Sin(ph * Mathf.PI * 4) * .45f };
                }
            }
        }
    }

    // The winners in front of the arch: a beat of anticipation, then every piece's own
    // celebration, confetti cannons, and a camera that swings in from the side.
    public sealed class LastSceneVictory : LastSceneCeremony
    {
        static readonly float[,] Slots = { { -.8f, 1.5f }, { .8f, 1.5f }, { -2.4f, 1f }, { 2.4f, 1f }, { -4f, .2f }, { 4f, .2f } };
        readonly List<LastSceneFigure> figures = new List<LastSceneFigure>();
        readonly List<LastSceneDust> dust = new List<LastSceneDust>();
        readonly List<Vector2> shakes = new List<Vector2>();

        public LastSceneVictory(Transform world, MatchResult result, int team)
        {
            var lineup = Lineup(result, team);
            var camera = P(0, 0, 15);
            for (int i = 0; i < lineup.Count; i++)
            {
                var figure = LastSceneFigure.Build(lineup[i].Piece, team, world);
                figure.Place(P(Slots[i, 0], LastSceneStage.Top, Slots[i, 1]), camera);
                figures.Add(figure);
                dust.Add(new LastSceneDust(world));
                if (lineup[i].Self) Self = figure;
            }
            shakes.Add(new Vector2(.62f, .18f));
            for (int n = 0; n < 30; n++) shakes.Add(new Vector2(LastSceneCannons.First + n * LastSceneCannons.Period, .07f));
            if (figures.Any(f => f.Kind == PieceKind.Rook))
                for (int k = 0; k < 60; k++) shakes.Add(new Vector2(.9f + 1.6f * k + .48f, .05f));
        }

        public override void Update(float time, float tp, float shift, Camera view)
        {
            float tc = tp - .9f;
            for (int i = 0; i < figures.Count; i++)
            {
                var f = figures[i];
                FigurePose p;
                if (tp < .9f)
                {
                    float crouch = Ease((tp - .45f) / .3f);
                    p = new FigurePose { Squash = .13f * crouch, Arm = -.35f * crouch, Lean = .1f * crouch };
                }
                else
                {
                    p = Cheer(f.Kind, tc, i);
                    float on = Ease(tc / .15f);
                    if (on < 1)
                    {
                        p.Jump *= on; p.Flip *= on; p.Yaw *= on; p.Dx *= on; p.Dz *= on; p.Lean *= on; p.Leg *= on; p.Squash *= on;
                        p.Arm = -.35f + (p.Arm + .35f) * on;
                    }
                }
                f.Pose(p);
                // A ring of dust where the rook stomps and the king lands.
                float landed = 9f;
                if (tc > 0 && f.Kind == PieceKind.Rook) { float ph = (tc + .4f) % 1.6f / 1.6f; landed = ph >= .55f ? (ph - .55f) * 1.6f / .6f : 9f; }
                if (tc > 0 && f.Kind == PieceKind.King) { float ph = (tc + .22f) % 1.3f / 1.3f; landed = ph >= .62f ? (ph - .62f) * 1.3f / .5f : 9f; }
                var foot = f.Position;
                dust[i].Show(new Vector3(foot.x, LastSceneStage.Top, foot.z), landed, f.Kind == PieceKind.Rook ? 1.1f : 1f);
            }
            // Swings in from the side, punches in on the title, ends low looking up at the
            // castle, and steps aside for the board.
            float k = Ease(tp / 2.4f), punch = Mathf.Exp(-Mathf.Pow((tp - .66f) / .2f, 2));
            float th = (1 - k) * .95f + Mathf.Sin(time * .21f) * .05f;
            float r = 16.6f - 2f * k - 1.1f * punch + 1.4f * shift, h = 7.6f - 4.9f * k + .2f * shift + Mathf.Sin(time * .37f) * .08f;
            var s = Shake(tp, shakes);
            view.transform.position = P(r * Mathf.Sin(th) + 1.4f * shift + s.x, h + s.y, 1f + r * Mathf.Cos(th));
            view.transform.LookAt(P(2.7f * shift + s.x * .6f, 1.6f + 1.4f * k + s.y * .6f, 1f));
            view.fieldOfView = 33f - 4f * punch;
        }
    }

    // The losers: the king trembles, its crown pops off and it falls back, the rest go
    // down like dominoes, one sits and pounds the floor, and the winners celebrate at
    // the arch behind while it rains.
    public sealed class LastSceneDefeat : LastSceneCeremony
    {
        // Centre first. The `fall` time, or a negative one for the piece that sits.
        static readonly float[,] Slots = { { .75f, 2.08f, 0 }, { -.75f, 2.38f, 2.1f }, { 2.25f, 2.38f, 2.25f }, { -2.25f, 2.08f, 2.45f }, { 3.75f, 2.08f, -2.5f }, { -3.75f, 2.38f, 2.75f } };
        readonly List<LastSceneFigure> losers = new List<LastSceneFigure>(), winners = new List<LastSceneFigure>();
        readonly List<(LastSceneDust ring, float at, Vector3 where, float size)> dust = new List<(LastSceneDust, float, Vector3, float)>();
        readonly List<Vector2> shakes = new List<Vector2>();
        readonly LastSceneFigure king;
        readonly GameObject looseCrown;
        readonly List<(Vector3 p, Vector3 r)> crownPath = new List<(Vector3, Vector3)>();

        struct Key { public float t; public Vector3 pos, look; public float fov; }
        static readonly Key[] Keys =
        {
            new Key { t = 0, pos = P(0, 2.6f, 10.2f), look = P(.6f, 1.6f, 1.8f), fov = 34 },
            new Key { t = .8f, pos = P(0, 2.45f, 9.2f), look = P(.7f, 1.8f, 1.9f), fov = 33 },
            new Key { t = 1f, pos = P(.7f, 2.3f, 6.9f), look = P(.8f, 2.1f, 2.1f), fov = 30 },
            new Key { t = 2.3f, pos = P(1.3f, 2.7f, 7.6f), look = P(.5f, 1.2f, 2f), fov = 32 },
            new Key { t = 3.5f, pos = P(.4f, 3.8f, 11f), look = P(.2f, 1.2f, 1f), fov = 33 },
            new Key { t = 5.6f, pos = P(-1f, 4.4f, 17.5f), look = P(.6f, 2.3f, -2.5f), fov = 33 },
        };

        public LastSceneDefeat(Transform world, MatchResult result, int team)
        {
            var lineup = Lineup(result, team);
            var camera = P(0, 0, 13);
            for (int i = 0; i < lineup.Count; i++)
            {
                var figure = LastSceneFigure.Build(lineup[i].Piece, team, world);
                figure.Place(P(Slots[i, 0], LastSceneStage.Top, Slots[i, 1]), camera);
                losers.Add(figure);
                if (lineup[i].Self) Self = figure;
            }
            var champions = Lineup(result, 1 - team);
            for (int i = 0; i < champions.Count; i++)
            {
                var figure = LastSceneFigure.Build(champions[i].Piece, 1 - team, world);
                figure.Place(P((i - 2.5f) * 1.45f, LastSceneStage.Top, -6.5f), camera);
                winners.Add(figure);
            }
            king = losers.Count > 0 && losers[0].Crown != null ? losers[0] : null;
            shakes.Add(new Vector2(1.855f, .17f));
            for (int i = 1; i < losers.Count; i++)
            {
                float at = Slots[i, 2];
                if (at > 0)
                {
                    shakes.Add(new Vector2(at + .27f, .06f));
                    float side = Slots[i, 0] < Slots[0, 0] ? -1 : 1;
                    var spot = losers[i].Position;
                    dust.Add((new LastSceneDust(world), at + .27f, new Vector3(spot.x + side, LastSceneStage.Top, spot.z), 1.1f));
                }
                else shakes.Add(new Vector2(-at + .35f, .04f));
            }
            if (losers.Count > 0)
            {
                var centre = losers[0].Position;
                dust.Add((new LastSceneDust(world), 1.855f, new Vector3(centre.x, LastSceneStage.Top, centre.z + 1.2f), 1.6f));
            }
            if (king != null)
            {
                shakes.Add(new Vector2(1f, .07f));
                looseCrown = LastSceneArt.Group(world, "Crown (popped)", Vector3.zero);
                LastSceneFigure.KingCrown(looseCrown.transform);
                looseCrown.SetActive(false);
                float firstHit = Simulate(king.Position + new Vector3(0, 1.85f, 0), new Vector3(1.8f, 5.4f, -.9f), new Vector3(5f, 1.5f, 7f));
                if (firstHit >= 0)
                {
                    var hit = crownPath[Mathf.Min(crownPath.Count - 1, Mathf.RoundToInt(firstHit * 120f))].p;
                    dust.Add((new LastSceneDust(world), 1f + firstHit, new Vector3(hit.x, LastSceneStage.Top, hit.z), .55f));
                }
            }
        }

        // The crown's flight, bounces and rest, worked out once at 120 steps a second.
        float Simulate(Vector3 start, Vector3 velocity, Vector3 spin)
        {
            const float dt = 1 / 120f;
            Vector3 p = start, v = velocity, rot = Vector3.zero, w = spin;
            bool resting = false;
            float firstHit = -1;
            for (int i = 0; i < 600; i++)
            {
                if (!resting)
                {
                    v.y -= 14f * dt;
                    p += v * dt;
                    rot += w * dt;
                    float ground = LastSceneStage.GroundY(p.x, p.z) + .07f;
                    if (p.y < ground)
                    {
                        p.y = ground;
                        if (firstHit < 0) firstHit = i * dt;
                        if (Mathf.Abs(v.y) < 1.2f) { resting = true; rot.x = .35f; rot.z = 0; }
                        else { v.y = -v.y * .42f; v.x *= .7f; v.z *= .7f; w *= .55f; }
                    }
                }
                crownPath.Add((p, rot));
            }
            return firstHit;
        }

        public override void Update(float time, float tp, float shift, Camera view)
        {
            float Bump(float at) => Mathf.Max(0, Mathf.Sin(Mathf.Clamp01((tp - at) / .3f) * Mathf.PI));
            float flinch = Bump(1f) + Bump(1.855f);
            for (int i = 0; i < losers.Count; i++)
            {
                FigurePose p;
                float fall = Slots[i, 2];
                if (i == 0)
                {
                    if (tp < 1f) p = new FigurePose { Yaw = Mathf.Sin(tp * 38f) * .02f * Mathf.Clamp01(tp / .6f), Squash = .04f * Mathf.Sin(tp * 9f) };
                    else if (tp < 1.6f) { float d = tp - 1f; p = new FigurePose { Tip = .17f * Mathf.Sin(d * 15f) * Mathf.Exp(-d * 2.4f), Arm = .9f * Mathf.Exp(-d * 3f) }; }
                    else
                    {
                        float d = tp - 1.6f, k = BounceOut(d / .7f);
                        p = new FigurePose { Pitch = -Mathf.PI / 2 * k, Arm = k < .99f ? .9f * Mathf.Sin(d * 22f) * (1 - k) - .2f * k : -.2f + Mathf.Sin(tp * 1.3f) * .05f, Leg = k < .99f ? Mathf.Sin(d * 18f) * .6f * (1 - k) : 0 };
                    }
                }
                else if (fall < 0)
                {
                    float at = -fall, sit = Ease((tp - at) / .35f), pound = tp > at + .35f ? Mathf.Abs(Mathf.Sin((tp - at - .35f) * 9f)) * Mathf.Exp(-(tp - at - .35f) * .35f) : 0;
                    p = new FigurePose { Sit = sit, Arm = -.9f * (1 - sit) + (.3f - pound) * sit + flinch * .5f * (1 - sit), Lean = .25f * (1 - sit), Jump = flinch * .12f * (1 - sit), Leg = sit * .5f };
                }
                else
                {
                    float dir = Slots[i, 0] < Slots[0, 0] ? 1 : -1;
                    if (tp < fall)
                    {
                        float slump = Ease((tp - .2f) / 1f);
                        p = new FigurePose { Lean = .26f * slump - flinch * .15f, Arm = -.95f * slump + flinch * .9f, Jump = flinch * .14f, Yaw = -dir * .2f * Mathf.Clamp01((tp - 1f) / .4f) };
                    }
                    else
                    {
                        float k = BounceOut((tp - fall) / .75f), twitch = tp > 4.6f ? Mathf.Pow(Mathf.Max(0, Mathf.Sin((tp + i * 1.3f) * 1.7f)), 12) * .12f : 0;
                        p = new FigurePose { Tip = dir * (Mathf.PI / 2 * k - twitch), Arm = -.5f * k + .4f * (1 - k), Lean = .2f * (1 - k), Leg = twitch * 3f };
                    }
                }
                losers[i].Pose(p);
            }
            for (int i = 0; i < winners.Count; i++) winners[i].Pose(Cheer(winners[i].Kind, tp + 3f, i));
            if (king != null)
            {
                bool popped = tp >= 1f;
                king.Crown.SetActive(!popped);
                looseCrown.SetActive(popped);
                if (popped)
                {
                    var step = crownPath[Mathf.Clamp(Mathf.FloorToInt((tp - 1f) * 120f), 0, crownPath.Count - 1)];
                    looseCrown.transform.position = step.p;
                    looseCrown.transform.rotation = Quaternion.Euler(step.r * Mathf.Rad2Deg);
                }
            }
            foreach (var d in dust) d.ring.Show(d.where, (tp - d.at) / .6f, d.size);
            var key = Camera(tp);
            var s = Shake(tp, shakes);
            view.transform.position = key.pos + new Vector3(1.8f * shift + s.x, s.y, -1.2f * shift);
            view.transform.LookAt(key.look + new Vector3(3.6f * shift + s.x * .6f, s.y * .6f, 0));
            view.fieldOfView = key.fov;
        }

        static Key Camera(float tp)
        {
            if (tp <= Keys[0].t) return Keys[0];
            for (int i = 0; i < Keys.Length - 1; i++)
            {
                var a = Keys[i];
                var b = Keys[i + 1];
                if (tp <= b.t)
                {
                    float k = Ease((tp - a.t) / (b.t - a.t));
                    return new Key { t = tp, pos = Vector3.Lerp(a.pos, b.pos, k), look = Vector3.Lerp(a.look, b.look, k), fov = Mathf.Lerp(a.fov, b.fov, k) };
                }
            }
            return Keys[Keys.Length - 1];
        }
    }
}
