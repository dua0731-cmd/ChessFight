using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 4판 bundle 나's moments on every screen (RagdollPawn.SetFourB.cs; 11-26), in the comic parts of
    /// PawnRushSkillFx.Comic.cs and the set's own (PawnRushSkillFx.SetFour.cs). Not seen in Unity by anyone yet.
    ///
    ///   폰      독 든 폰 a glossy green little pawn turning at the feet, sparkling, bubbling; taken: a green cloud and the
    ///           one who took it drawn pale blue while it lasts; 폰 폭풍 three wind-up toy pawns (half a metre) waddling on
    ///           with a gold key turning in each back - one that catches climbs a leg and hugs it, one that stops topples
    ///           over, one knocked over flies off spinning
    ///   나이트  엉덩방아 a shadow growing where it comes down, then a seat-first thump ("쿵!", dust ring, shake); 히힝! a
    ///           streak round the back, then shock lines and a "!" over the one startled
    ///   비숍    먹구름 a dark cloud over the head raining on it, splashing at its feet; 향로 a censer swinging at its side,
    ///           lilac smoke from it and left lying behind for its seconds; 종 씌우기 a bronze bell with its shadow, dropping
    ///           over a head and staying on, ringing in waves ("댕~"), or clanging on the floor; 퇴장! a crozier shooting
    ///           out and yanking back, a red card over the one sent off
    ///   룩      성벽! dust along its foot and battlements on its top (on another PC its picture too); 수평 룩 a sideways
    ///           streak and skid; 잔디깎이 a saw blade spinning on its head, grass flying behind
    ///   퀸      스콜라 메이트 · 영구 체크 a red folding fan: a smack ("탁!"), a whiff over a ducked head, a red square under
    ///           each check; 퀸 희생!! a gold glow, a bang, the queen in chunks lying round and flying back together
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly Kit.Palette CmPoison = new Kit.Palette("#8FE05A", "#E5FFC8", "#4F9A2A", "#1E3A0E");
        static readonly Kit.Palette CmCloud = new Kit.Palette("#5A6070", "#8C93A3", "#3A3F4C", "#14171F");
        static readonly Kit.Palette CmIncense = new Kit.Palette("#C9B8E8", "#F2ECFF", "#9C88C4", "#3A2E55");
        static readonly Kit.Palette CmBronze = new Kit.Palette("#D0923F", "#F7D896", "#8E5A1E", "#2E1A06");
        static readonly Kit.Palette CmGrass = new Kit.Palette("#5BC236", "#C8F5A8", "#2F8A1A", "#123A08");

        sealed class Toy4 { public Vector3 pos, vel; public int state; public float t0; public RagdollPawn who; public float hold; public Kit.Prop body, key; }
        sealed class Storm4Fx { public RagdollPawn by; public Toy4[] toys; }
        sealed class Bell4Fx { public RagdollPawn by, on; public Vector3 at, pos; public float born, delay, landed; public int state; }

        readonly Dictionary<RagdollPawn, Kit.Fx> s4bTraps = new Dictionary<RagdollPawn, Kit.Fx>();
        readonly Dictionary<RagdollPawn, Storm4Fx> s4bStorms = new Dictionary<RagdollPawn, Storm4Fx>();
        readonly List<Bell4Fx> s4bBells = new List<Bell4Fx>();
        readonly Dictionary<RagdollPawn, float> s4bCrooks = new Dictionary<RagdollPawn, float>();   // a staff out: when it was told to come back
        readonly Dictionary<RagdollPawn, float> s4bBlades = new Dictionary<RagdollPawn, float>();   // a mower on: until
        readonly Dictionary<RagdollPawn, float> s4bFans = new Dictionary<RagdollPawn, float>();     // a fan out: until
        Mesh s4bBellMesh, s4bBladeMesh, s4bFanMesh, s4bKeyMesh, s4bCardMesh;
        Material s4bSick;

        // ---------------------------------------------------------------- the moments (SetFourFx)

        void SetFourBFx(SkillFxEvent e, float lvl)
        {
            switch (e.kind)
            {
                case SkillFxKind.S4Poison: S4PoisonFx(e, lvl); break;
                case SkillFxKind.S4Storm: S4StormFx(e, lvl); break;
                case SkillFxKind.S4Slam: S4SlamFx(e, lvl); break;
                case SkillFxKind.S4Neigh: S4NeighFx(e, lvl); break;
                case SkillFxKind.S4Cloud: S4CloudFx(e, lvl); break;
                case SkillFxKind.S4Censer: S4CenserFx(e, lvl); break;
                case SkillFxKind.S4Bell: S4BellFx(e, lvl); break;
                case SkillFxKind.S4Crook: S4CrookFx(e, lvl); break;
                case SkillFxKind.S4Wall: S4WallFx(e, lvl); break;
                case SkillFxKind.S4Slide: S4SlideFx(e, lvl); break;
                case SkillFxKind.S4Mower: S4MowerFx(e, lvl); break;
                case SkillFxKind.S4Fan: S4FanFx(e, lvl); break;
                case SkillFxKind.S4Sac: S4SacFx(e, lvl); break;
                default: SetFourCFx(e, lvl); break;   // bundle 다 (PawnRushSkillFx.SetFourC.cs)
            }
        }

        // ---------------------------------------------------------------- 폰

        void S4PoisonFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count == 0)
            {
                if (by != null && s4bTraps.TryGetValue(by, out var old)) old.dead = true;
                if (kit.pawnMesh == null) return;
                var pawn = CmProp(kit.pawnMesh, CmPoison, "Poisoned pawn", 0.012f);
                Vector3 at = e.at;
                float life = Mathf.Max(0.5f, e.size), nextGlint = 0.1f, nextFizz = 0.3f;
                CmPoof(at + Vector3.up * 0.1f, CmPoison, 4, 0.35f, 0.16f, 0.35f, 0.3f);
                UltClip(UltClipId.Shimmer, 0.4f * lvl, 1.8f);
                var f = kit.Run(life + 0.3f, (fx, d) =>
                {
                    float pop = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.2f)), outk = Mathf.Clamp01((fx.age - life) / 0.3f);
                    if (outk >= 1f) return false;
                    CmPlace(pawn, at + Vector3.up * (0.02f + 0.03f * Mathf.Sin(fx.age * 4f)), Quaternion.Euler(0f, fx.age * 60f, 6f * Mathf.Sin(fx.age * 3f)),
                        Vector3.one * (0.34f * Mathf.Max(0.02f, pop) * (1f - outk)));
                    if (fx.age >= nextGlint)
                    {
                        nextGlint = fx.age + 0.3f;
                        Vector2 r = Random.insideUnitCircle * 0.15f;
                        CmFlash(at + new Vector3(r.x, 0.2f + Random.value * 0.15f, r.y), 0.09f, 8);
                    }
                    if (fx.age >= nextFizz)
                    {
                        nextFizz = fx.age + 0.45f;
                        CmPoof(at + Vector3.up * 0.32f, CmPoison, 1, 0.12f, 0.06f, 0.6f, 1.5f);
                    }
                    return true;
                });
                f.props.Add(pawn);
                if (by != null)
                {
                    s4bTraps[by] = f;
                    f.end = () => { if (s4bTraps.TryGetValue(by, out var x) && x == f) s4bTraps.Remove(by); };
                }
                return;
            }
            if (by != null && s4bTraps.TryGetValue(by, out var trap)) trap.dead = true;
            CmPoof(e.at + Vector3.up * 0.15f, CmPoison, e.count == 1 ? 9 : 4, e.count == 1 ? 0.9f : 0.4f, e.count == 1 ? 0.34f : 0.2f, 0.5f, 0.6f);
            if (e.count != 1 || e.target == null) return;
            // Taken: green gas, and its taker pale blue while it lasts ("얼굴이 파래진다"), green bubbles over its head.
            var who = e.target;
            float seconds = Mathf.Max(0.3f, e.size);
            var sick = S4bSickMaterial();
            UltClip(UltClipId.Wah, 0.6f * lvl, 0.6f);
            float nextBubble = 0f;
            kit.Run(seconds, (fx, d) =>
            {
                if (who == null) return false;
                if (sick != null) WantGiant(who, 1f, 8f, sick);
                if (fx.age >= nextBubble)
                {
                    nextBubble = fx.age + 0.35f;
                    CmPoof(DrawnHead(who) + Vector3.up * 0.3f + Random.insideUnitSphere * 0.1f, CmPoison, 1, 0.15f, 0.07f, 0.55f, 1.6f);
                }
                return true;
            });
        }

        void S4StormFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count == 0)
            {
                if (by == null || kit.pawnMesh == null) return;
                if (s4bStorms.TryGetValue(by, out var old))
                    foreach (var t in old.toys) if (t.state == 0) { t.state = 2; t.t0 = Clock; }
                float speed = Mathf.Max(0.5f, e.size), range = e.normal.x > 0f ? e.normal.x : 15f, time = e.normal.y > 0f ? e.normal.y : 6f;
                float gap = e.normal.z > 0f ? e.normal.z : 0.6f;
                int n = Params != null ? Mathf.Clamp(Params.stormCount, 1, 5) : 3;
                Vector3 dir = FlatDir(e.dir), right = Vector3.Cross(Vector3.up, dir);
                var p = S3Colors(by);
                var storm = new Storm4Fx { by = by, toys = new Toy4[n] };
                for (int i = 0; i < n; i++)
                {
                    storm.toys[i] = new Toy4
                    {
                        pos = e.at + right * ((i - (n - 1) * 0.5f) * gap),
                        body = CmProp(kit.pawnMesh, p, "Clockwork pawn", 0.012f),
                        key = CmProp(S4bKeyMesh(), CmGold, "Clockwork key", 0.01f),
                    };
                    CmPoof(storm.toys[i].pos + Vector3.up * 0.1f, CmDust, 2, 0.3f, 0.15f, 0.3f, 0.2f);
                }
                s4bStorms[by] = storm;
                UltClip(UltClipId.March, 0.5f * lvl, 1.6f);
                float nextTick = 0f;
                var f = kit.Run(time + 6f, (fx, d) =>
                {
                    bool any = false;
                    for (int i = 0; i < n; i++)
                    {
                        var toy = storm.toys[i];
                        if (toy.state < 0) continue;
                        any = true;
                        StepToy4(toy, dir, speed, fx.age, i, d);
                    }
                    if (fx.age >= nextTick && storm.toys[0].state == 0)
                    {
                        nextTick = fx.age + 0.25f;
                        UltClip(UltClipId.Tick, 0.25f * S3Heard(storm.toys[0].pos), 2.2f);
                    }
                    // (The host's word ends each; a march outrunning its range or time stops by itself.)
                    foreach (var toy in storm.toys)
                        if (toy.state == 0 && fx.age > time + 0.5f) { toy.state = 2; toy.t0 = Clock; }
                    return any;
                });
                foreach (var toy in storm.toys)
                {
                    f.props.Add(toy.body);
                    f.props.Add(toy.key);
                }
                f.end = () => { if (s4bStorms.TryGetValue(by, out var x) && x == storm) s4bStorms.Remove(by); };
                return;
            }
            if (by == null || !s4bStorms.TryGetValue(by, out var st)) return;
            int index = e.count % 10, kind = e.count / 10;
            if (index < 0 || index >= st.toys.Length) return;
            var it = st.toys[index];
            it.pos = e.at;
            it.t0 = Clock;
            it.who = e.target;
            if (kind == 1)
            {
                // Caught one: up its leg, hugging it while the hindrance lasts.
                it.state = 1;
                it.hold = Mathf.Max(0.3f, e.size);
                CmPoof(e.at + Vector3.up * 0.2f, CmDust, 4, 0.4f, 0.2f, 0.35f, 0.4f);
                UltClip(UltClipId.Tap, 0.7f * lvl, 1.4f);
            }
            else if (kind == 3)
            {
                // Knocked over: off it flies, spinning.
                it.state = 3;
                Vector3 away = e.target != null ? FlatDir(e.at - e.target.bodies[0].position) : -FlatDir(e.dir);
                it.vel = away * 4f + Vector3.up * 4f;
                CmFlash(e.at + Vector3.up * 0.25f, 0.3f, 7);
                UltClip(UltClipId.Tap, 0.6f * lvl, 0.9f);
            }
            else it.state = 2;   // stopped or off an edge: topples
        }

        /// <summary>One clockwork pawn this frame: marching (waddle, key turning), hugging a leg, toppling or flying.</summary>
        void StepToy4(Toy4 toy, Vector3 dir, float speed, float age, int i, float d)
        {
            const float Tall = 0.5f;
            float since = Clock - toy.t0;
            switch (toy.state)
            {
                case 0:
                {
                    Vector3 next = toy.pos + dir * (speed * d);
                    Vector3 floor = FloorBelow(next + Vector3.up * 0.6f, 1.4f);
                    toy.pos = floor.y > next.y - 1.2f ? floor : next + Vector3.down * (speed * d);
                    float waddle = Mathf.Sin(age * 14f + i * 1.7f);
                    Quaternion face = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, 9f * waddle);
                    CmPlace(toy.body, toy.pos + Vector3.up * (0.03f * Mathf.Abs(waddle)), face, Vector3.one * (Tall * Kit.EaseOutBack(Mathf.Clamp01(age / 0.2f))));
                    CmPlace(toy.key, toy.pos + Vector3.up * (Tall * 0.55f) - dir * (Tall * 0.22f), face * Quaternion.Euler(0f, 0f, age * 400f), Vector3.one * Tall);
                    return;
                }
                case 1:
                {
                    var who = toy.who;
                    if (who == null || since > toy.hold)
                    {
                        if (who != null) CmPoof(who.FeetPoint + Vector3.up * 0.25f, CmDust, 3, 0.3f, 0.15f, 0.3f, 0.3f);
                        toy.state = -1;
                        toy.body.Show(false);
                        toy.key.Show(false);
                        return;
                    }
                    // On its shin, hugging: a little up, toward us, squeezing in time.
                    Vector3 feet = who.FeetPoint, toCam = ViewCamera != null ? FlatDir(ViewCamera.transform.position - feet) : Vector3.forward;
                    Vector3 at = feet + Vector3.up * (0.12f + 0.03f * Mathf.Sin(since * 10f)) + toCam * 0.22f;
                    Quaternion face = Quaternion.LookRotation(-toCam, Vector3.up) * Quaternion.Euler(-15f, 0f, 6f * Mathf.Sin(since * 9f));
                    float squeeze = 1f + 0.08f * Mathf.Sin(since * 10f);
                    CmPlace(toy.body, at, face, new Vector3(Tall * squeeze, Tall / squeeze, Tall * squeeze) * 0.9f);
                    CmPlace(toy.key, at + Vector3.up * (Tall * 0.5f) + toCam * (Tall * 0.2f), face * Quaternion.Euler(0f, 0f, since * 900f), Vector3.one * (Tall * 0.9f));
                    return;
                }
                case 2:
                {
                    // Toppled: over on its face, then gone in a poof.
                    if (since > 0.6f)
                    {
                        CmPoof(toy.pos + Vector3.up * 0.15f, CmDust, 3, 0.3f, 0.16f, 0.3f, 0.3f);
                        toy.state = -1;
                        toy.body.Show(false);
                        toy.key.Show(false);
                        return;
                    }
                    float fall = Kit.EaseIn(Mathf.Clamp01(since / 0.3f));
                    Quaternion face = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(85f * fall, 0f, 0f);
                    CmPlace(toy.body, toy.pos, face, Vector3.one * Tall);
                    CmPlace(toy.key, toy.pos + face * (Vector3.up * (Tall * 0.55f)) - dir * 0.05f, face * Quaternion.Euler(0f, 0f, since * 120f), Vector3.one * Tall);
                    return;
                }
                default:
                {
                    // Flying off, spinning, then a poof.
                    toy.vel += Physics.gravity * d;
                    toy.pos += toy.vel * d;
                    if (since > 0.7f)
                    {
                        CmPoof(toy.pos, CmDust, 3, 0.3f, 0.16f, 0.3f, 0.3f);
                        toy.state = -1;
                        toy.body.Show(false);
                        toy.key.Show(false);
                        return;
                    }
                    Quaternion spin = Quaternion.Euler(since * 720f, since * 300f, 0f);
                    CmPlace(toy.body, toy.pos, spin, Vector3.one * Tall);
                    CmPlace(toy.key, toy.pos + spin * (Vector3.up * (Tall * 0.55f)), spin, Vector3.one * Tall);
                    return;
                }
            }
        }

        // ---------------------------------------------------------------- 나이트

        void S4SlamFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count == 0)
            {
                if (by == null) return;
                float flight = Mathf.Max(0.2f, e.size);
                Vector3 to = e.normal;
                float reach = Params != null ? Params.slamReach : 1.5f;
                CmPoof(e.at + Vector3.up * 0.1f, CmDust, 5, 0.7f, 0.28f, 0.4f, 0.25f);
                CmStreak(by, S3Colors(by), flight, 0.18f, 0.16f);
                kit.AddSquash(by, Kit.SquashKind.Spring);
                UltClip(UltClipId.Whoosh, 0.6f * lvl, 1.1f);
                // Where its seat comes down: a shadow growing, the reach flashing.
                kit.FlashRing(to + Vector3.up * 0.04f, reach, S3Colors(by));
                kit.Run(flight, (fx, d) =>
                {
                    float k = Mathf.Clamp01(fx.age / flight);
                    DrawShade(to + Vector3.up * 0.02f, Mathf.Lerp(0.3f, 0.7f, k), Mathf.Lerp(0.15f, 0.45f, k));
                    return true;
                });
                return;
            }
            // Down on its seat: "쿵!", a ring of dust, a shake.
            if (by != null) kit.AddSquash(by, Kit.SquashKind.Flatten, 0.15f);
            kit.DustRing(e.at + Vector3.up * 0.05f, 12, 1.2f, CmDust, 0.36f, 0.55f);
            CmFlash(e.at + Vector3.up * 0.3f, 0.6f, 8);
            PopWord("쿵!", e.at + Vector3.up * 1.1f, Kit.Hex("#FF8A3D"), 0.5f, 0.8f);
            Shake(0.08f, 10f * Frame);
            UltClip(UltClipId.SubDrop, 0.9f * lvl, 0.8f);
            UltClip(UltClipId.Crack, 0.5f * lvl, 0.7f);
        }

        void S4NeighFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count == 0)
            {
                if (by != null) CmStreak(by, S3Colors(by), Mathf.Max(0.15f, e.size) + 0.1f, 0.2f, 0.16f);
                CmPoof(e.at + Vector3.up * 0.1f, CmDust, 3, 0.4f, 0.2f, 0.3f, 0.2f);
                UltClip(UltClipId.Whoosh, 0.7f * lvl, 1.5f);
                return;
            }
            // 히힝!: the one in front jumps out of its skin - a flash, shock lines all round its head, a "!".
            var who = e.target;
            if (who == null) return;
            Vector3 head = e.at;
            CmFlash(head, 0.45f, 7);
            kit.FlashBody(who, Color.white);
            S4bShockLines(who, 0.45f);
            PopWord("!", head + Vector3.up * 0.75f, Kit.Hex("#FF4A3D"), 0.55f, 0.7f, who, 6f);
            UltClip(UltClipId.Wah, 0.7f * lvl, 1.8f);
            UltClip(UltClipId.Tap, 0.6f * lvl, 1.6f);
        }

        /// <summary>Short ink-cored lines darting out all round a head for <paramref name="life"/> seconds: a startle.</summary>
        void S4bShockLines(RagdollPawn who, float life)
        {
            const int N = 8;
            var lines = new Kit.Strip[N];
            for (int i = 0; i < N; i++) lines[i] = new Kit.Strip(kit, CmWhite) { core = Color.white, coreShare = 0.5f, inkShare = 0.3f };
            var pts = new List<Vector3>(2);
            var f = kit.Run(life, (fx, d) =>
            {
                if (who == null) return false;
                var cam = ViewCamera;
                if (cam == null) return true;
                Vector3 head = DrawnHead(who);
                float t = fx.age / life, r0 = 0.3f + 0.25f * Kit.EaseOut(t), r1 = r0 + 0.22f * (1f - t);
                for (int i = 0; i < N; i++)
                {
                    float a = i * Mathf.PI * 2f / N + 0.3f;
                    Vector3 dir = cam.transform.right * Mathf.Cos(a) + cam.transform.up * Mathf.Sin(a);
                    pts.Clear();
                    pts.Add(head + dir * r0);
                    pts.Add(head + dir * r1);
                    lines[i].Build(pts, j => 0.06f * (1f - t), kit.Eye);
                    lines[i].Apply();
                }
                return true;
            });
            foreach (var l in lines) f.strips.Add(l);
        }

        // ---------------------------------------------------------------- 비숍

        void S4CloudFx(SkillFxEvent e, float lvl)
        {
            var who = e.target;
            if (who == null) return;
            float total = Mathf.Max(0.5f, e.size), delay = Params != null ? Params.cloudDelay : 0.5f;
            const int N = 6;
            var puffs = new Kit.Puff[N];
            var offs = new Vector3[N];
            for (int i = 0; i < N; i++)
            {
                puffs[i] = new Kit.Puff(kit, CmCloud) { lump = 0.15f, ink = 0.3f };
                float a = i * Mathf.PI * 2f / N;
                offs[i] = new Vector3(Mathf.Cos(a) * 0.38f, Random.Range(-0.05f, 0.08f), Mathf.Sin(a) * 0.26f);
            }
            UltClip(UltClipId.Hum, 0.4f * lvl, 0.6f);
            float nextDrop = delay, nextSplash = delay + 0.1f;
            bool thunder = false;
            var f = kit.Run(total + 0.4f, (fx, d) =>
            {
                if (who == null) return false;
                float k = GiantScaleOf(who), grow = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.3f)), outk = Mathf.Clamp01((fx.age - total) / 0.4f);
                if (outk >= 1f) return false;
                Vector3 c = DrawnHead(who) + Vector3.up * (1.0f * k + 0.05f * Mathf.Sin(fx.age * 2f));
                for (int i = 0; i < N; i++)
                {
                    var p = puffs[i];
                    p.t.position = c + offs[i] * k + new Vector3(0.03f * Mathf.Sin(fx.age * 3f + i), 0f, 0f);
                    p.t.localScale = Vector3.one * ((i == 0 ? 0.55f : 0.42f) * k * Mathf.Max(0.02f, grow) * (1f - outk * 0.6f));
                    p.dissolve = outk;
                    p.Apply(fx.age);
                }
                if (fx.age < delay || outk > 0f) return true;
                if (!thunder)
                {
                    thunder = true;
                    UltClip(UltClipId.SubDrop, 0.4f * S3Heard(c), 1.6f);
                }
                if (fx.age >= nextDrop)
                {
                    nextDrop = fx.age + 0.05f;
                    Vector2 r = Random.insideUnitCircle * 0.35f * k;
                    kit.Drops(c + new Vector3(r.x, -0.15f, r.y), Vector3.down, CmWater, 1, 5f);
                }
                if (fx.age >= nextSplash)
                {
                    nextSplash = fx.age + 0.22f;
                    CmPoof(who.FeetPoint + Vector3.up * 0.05f, CmWater, 2, 0.3f, 0.12f, 0.3f, 0.3f);
                }
                return true;
            });
            foreach (var p in puffs) f.puffs.Add(p);
        }

        void S4CenserFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count == 1)
            {
                // Caught in the smoke: a cough of it at the face, a hunch.
                if (e.target != null)
                {
                    CmPoof(DrawnHead(e.target) + FlatDir(e.target.Facing) * 0.25f, CmIncense, 3, 0.35f, 0.18f, 0.45f, 0.3f);
                    kit.AddSquash(e.target, Kit.SquashKind.Bump);
                }
                UltClip(UltClipId.Tap, 0.4f * lvl, 0.7f);
                return;
            }
            if (by == null) return;
            float seconds = Mathf.Max(0.3f, e.size), linger = Params != null ? Params.censerLinger : 3f;
            float width = Params != null ? Params.censerWidth : 2f;
            var ball = CmProp(kit.meshBall, CmGold, "Censer", 0.012f);
            var chain = new Kit.Strip(kit, CmGold) { core = CmGold.light, coreShare = 0.3f, inkShare = 0.3f };
            var pts = new List<Vector3>(2);
            float nextPuff = 0f, nextTrail = 0f;
            UltClip(UltClipId.Shimmer, 0.4f * lvl, 0.9f);
            var f = kit.Run(seconds + 0.3f, (fx, d) =>
            {
                if (by == null) return false;
                float outk = Mathf.Clamp01((fx.age - seconds) / 0.3f);
                if (outk >= 1f) return false;
                Vector3 face = FlatDir(by.Facing), right = Vector3.Cross(Vector3.up, face);
                Vector3 hand = by.bodies[(int)BodyId.Chest].position + right * 0.38f + Vector3.up * 0.05f;
                float swing = 50f * Mathf.Sin(fx.age * Mathf.PI * 2f * 1.1f) * Mathf.Deg2Rad;
                Vector3 bob = hand + (Vector3.down * Mathf.Cos(swing) + face * Mathf.Sin(swing)) * 0.45f;
                pts.Clear();
                pts.Add(hand);
                pts.Add(bob);
                chain.Build(pts, i => 0.03f, kit.Eye);
                chain.fade = 1f - outk;
                chain.Apply();
                CmPlace(ball, bob, Quaternion.identity, Vector3.one * (0.17f * (1f - outk)));
                if (fx.age >= nextPuff && outk <= 0f)
                {
                    nextPuff = fx.age + 0.12f;
                    CmPoof(bob, CmIncense, 1, 0.2f, 0.12f, 0.7f, 1.2f);
                }
                if (fx.age >= nextTrail && outk <= 0f)
                {
                    // The smoke it leaves where it walks: wide, low, lingering its seconds.
                    nextTrail = fx.age + 0.22f;
                    S4bSmokePuff(by.FeetPoint + Vector3.up * 0.25f, width * 0.5f, linger);
                }
                return true;
            });
            f.props.Add(ball);
            f.strips.Add(chain);
        }

        /// <summary>One soft lilac cloud lying on the floor for <paramref name="life"/> seconds, swelling and fading.</summary>
        void S4bSmokePuff(Vector3 at, float radius, float life)
        {
            var puff = new Kit.Puff(kit, CmIncense) { lump = 0.25f, ink = 0.15f };
            Vector3 drift = new Vector3(Random.Range(-0.1f, 0.1f), 0.05f, Random.Range(-0.1f, 0.1f));
            float spin = Random.Range(0f, 360f);
            var f = kit.Run(life, (fx, d) =>
            {
                float t = fx.age / life;
                puff.t.position = at + drift * fx.age;
                puff.t.rotation = Quaternion.Euler(0f, spin + fx.age * 20f, 0f);
                float s = radius * 1.4f * Kit.EaseOut(Mathf.Clamp01(t * 4f)) * (1f + 0.25f * t);
                puff.t.localScale = new Vector3(s, s * 0.55f, s);
                puff.dissolve = Mathf.Clamp01((t - 0.55f) / 0.45f);
                puff.Apply(fx.age);
                return true;
            });
            f.puffs.Add(puff);
        }

        void S4BellFx(SkillFxEvent e, float lvl)
        {
            if (e.count == 0)
            {
                var b = new Bell4Fx { by = e.by, at = e.at, pos = e.at, born = Clock, delay = Mathf.Max(0.05f, e.size) };
                s4bBells.Add(b);
                var bell = CmProp(S4bBellMesh(), CmBronze, "Bell", 0.016f);
                var clapper = CmProp(kit.meshBall, CmBronze, "Bell clapper", 0.01f);
                float size = Params != null ? Params.bellSize : 1.5f, fallTime = Params != null ? Params.bellFall : 0.45f;
                Vector3 floor = FloorBelow(e.at, 16f);
                float spin = Random.Range(0f, 360f);
                CmPoof(e.at, CmDust, 5, 0.7f, 0.3f, 0.4f, 0.3f);
                UltClip(UltClipId.Shimmer, 0.4f * lvl, 0.6f);
                var f = kit.Run(b.delay + fallTime + 6f, (fx, d) =>
                {
                    float age = Clock - b.born;
                    Vector3 at;
                    Quaternion rot = Quaternion.Euler(0f, spin, 0f);
                    float scale = size;
                    switch (b.state)
                    {
                        case 0:
                        {
                            // Hanging, swaying; then dropping, its shadow growing under it.
                            float drop = Mathf.Clamp01((age - b.delay) / Mathf.Max(0.05f, fallTime));
                            if (age > b.delay + fallTime + 1f) return false;   // (no word from the host)
                            at = b.at;
                            at.y = Mathf.Lerp(b.at.y, floor.y + 1.2f, drop * drop);
                            rot *= Quaternion.Euler(6f * Mathf.Sin(age * 5f) * (1f - drop), 0f, 0f);
                            scale *= Kit.EaseOutBack(Mathf.Clamp01(age / 0.2f));
                            DrawShade(floor + Vector3.up * 0.02f, Mathf.Lerp(0.45f, 0.8f, drop) * size * 0.6f, Mathf.Lerp(0.18f, 0.45f, drop));
                            break;
                        }
                        case 1:
                        {
                            // Over a head: sitting on it down to the shoulders, wobbling, ringing.
                            var who = b.on;
                            float t = Clock - b.landed;
                            if (who == null || t > b.delay + 0.3f) { CmPoof(b.pos, CmBronze, 5, 0.6f, 0.25f, 0.4f, 0.4f); return false; }
                            float k = GiantScaleOf(who), lift = Mathf.Clamp01((t - b.delay) / 0.3f);
                            Vector3 up = HeadUp(who);
                            at = DrawnHead(who) + up * (0.45f * k) + Vector3.up * (1.5f * lift * lift);
                            rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(8f * Mathf.Sin(t * 14f) * Mathf.Exp(-t * 1.5f), spin, 0f);
                            scale = size * 0.62f * k;
                            b.pos = at;
                            break;
                        }
                        default:
                        {
                            // On the floor: a clang, a bounce, a tilt, gone.
                            float t = Clock - b.landed;
                            if (t > 0.7f) { CmPoof(b.pos + Vector3.up * 0.5f, CmBronze, 5, 0.7f, 0.28f, 0.4f, 0.4f); return false; }
                            at = b.pos + Vector3.up * (0.35f * Mathf.Abs(Mathf.Sin(t * 9f)) * Mathf.Exp(-t * 4f));
                            rot *= Quaternion.Euler(18f * Mathf.Sin(t * 12f) * Mathf.Exp(-t * 3f), 0f, 0f);
                            break;
                        }
                    }
                    // (The bell's mouth down: its foot at its rim; the mesh stands from 0 to 1 m tall.)
                    CmPlace(bell, at - rot * (Vector3.up * (0.75f * scale)), rot, Vector3.one * scale);
                    CmPlace(clapper, at - rot * (Vector3.up * (0.62f * scale)) + rot * (Vector3.forward * (0.06f * scale * Mathf.Sin(fx.age * 9f))), Quaternion.identity, Vector3.one * (0.16f * scale));
                    return true;
                });
                f.props.Add(bell);
                f.props.Add(clapper);
                f.end = () => s4bBells.Remove(b);
                return;
            }
            Bell4Fx ours = null;
            foreach (var x in s4bBells)
                if (x.by == e.by && x.state == 0) { ours = x; break; }
            if (e.count == 1)
            {
                if (ours != null)
                {
                    ours.state = 1;
                    ours.on = e.target;
                    ours.landed = Clock;
                    ours.delay = Mathf.Max(0.3f, e.size);   // (now: how long it stays on)
                }
                if (e.target != null) S4bRings(e.target, Mathf.Max(0.3f, e.size));
                Shake(0.06f, 10f * Frame);
                UltClip(UltClipId.Tock, lvl, 0.55f);
                UltClip(UltClipId.Hum, 0.6f * lvl, 1.1f);
                return;
            }
            if (ours != null)
            {
                ours.state = 2;
                ours.landed = Clock;
                ours.pos = e.at;
            }
            CmPoof(e.at + Vector3.up * 0.1f, CmDust, 7, 1f, 0.32f, 0.45f, 0.25f);
            UltClip(UltClipId.Tock, 0.8f * lvl, 0.45f);
        }

        /// <summary>The bell ringing on a head: rings of sound going out from it every half second, fading as it does.</summary>
        void S4bRings(RagdollPawn who, float seconds)
        {
            float next = 0f;
            kit.Run(seconds, (fx, d) =>
            {
                if (who == null) return false;
                if (fx.age < next) return true;
                next = fx.age + 0.5f;
                float k = 1f - fx.age / seconds;
                var ring = new Kit.Strip(kit, CmBronze) { core = Color.white, coreShare = 0.45f, inkShare = 0.2f };
                var pts = new List<Vector3>(25);
                Vector3 c = DrawnHead(who) + Vector3.up * 0.3f;
                var f = kit.Run(0.5f, (gx, gd) =>
                {
                    float t = gx.age / 0.5f, r = Mathf.Lerp(0.5f, 1.3f, Kit.EaseOut(t));
                    pts.Clear();
                    for (int i = 0; i <= 24; i++)
                    {
                        float a = i * Mathf.PI * 2f / 24f;
                        pts.Add(c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    }
                    ring.Build(pts, j => 0.08f * k * (1f - t), kit.Eye, true);
                    ring.fade = (1f - t) * k;
                    ring.Apply();
                    return true;
                });
                f.strips.Add(ring);
                return true;
            });
        }

        void S4CrookFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count == 0)
            {
                if (by == null) return;
                s4bCrooks[by] = -1f;
                float time = Mathf.Max(0.05f, e.size), reach = (e.normal - e.at).magnitude;
                Vector3 dir = FlatDir(e.dir);
                var shaft = new Kit.Strip(kit, Kit.Bishop) { core = Kit.Bishop.light, coreShare = 0.25f, inkShare = 0.3f };
                var hook = CmProp(kit.meshTorus, CmGold, "Crook hook", 0.012f);
                var pts = new List<Vector3>(2);
                UltClip(UltClipId.Whoosh, 0.6f * lvl, 1.3f);
                float back = -1f, outAt = 0f;
                var f = kit.Run(time + 2f, (fx, d) =>
                {
                    if (by == null) return false;
                    float told = s4bCrooks.TryGetValue(by, out var w) ? w : Clock;
                    if (told >= 0f && back < 0f) { back = Clock; outAt = Mathf.Clamp01(fx.age / time); }
                    float len;
                    if (back < 0f) len = reach * Kit.EaseOut(Mathf.Clamp01(fx.age / time));
                    else
                    {
                        float t = (Clock - back) / 0.22f;
                        if (t >= 1f) return false;
                        len = reach * outAt * (1f - Kit.EaseIn(t));
                    }
                    if (back < 0f && fx.age > time + 0.6f) { back = Clock; outAt = 1f; }   // (no word from the host)
                    Vector3 root = by.bodies[(int)BodyId.Chest].position + Vector3.up * 0.1f, tip = root + dir * Mathf.Max(0.05f, len);
                    pts.Clear();
                    pts.Add(root);
                    pts.Add(tip);
                    shaft.Build(pts, i => 0.07f, kit.Eye);
                    shaft.Apply();
                    // The crook's hook at its tip, curled toward the bishop.
                    CmPlace(hook, tip + Vector3.up * 0.12f, Quaternion.LookRotation(Vector3.up, dir) * Quaternion.Euler(0f, 0f, 90f), new Vector3(0.28f, 0.28f, 0.28f));
                    return true;
                });
                f.strips.Add(shaft);
                f.props.Add(hook);
                f.end = () => s4bCrooks.Remove(by);
                return;
            }
            if (by != null) s4bCrooks[by] = Clock;   // (it comes back)
            if (e.count == 2)
            {
                CmPoof(e.at, CmWhite, 3, 0.3f, 0.14f, 0.3f, 0.3f);
                return;
            }
            // Hooked by the scruff: yanked over, a red card over its head all the way.
            var who = e.target;
            if (who == null) return;
            float flight = Mathf.Max(0.3f, e.size);
            CmStreak(who, CmRed, flight, 0.2f, 0.18f);
            CmFlash(e.at, 0.4f, 7);
            UltClip(UltClipId.Tick, 0.9f * lvl, 2.6f);   // (a whistle's peep)
            if (s4bCardMesh == null) s4bCardMesh = kit.RoundBox(new Vector3(0.24f, 0.34f, 0.03f), 0.02f);
            var card = CmProp(s4bCardMesh, CmRed, "Red card", 0.012f);
            var f2 = kit.Run(flight + 0.6f, (fx, d) =>
            {
                if (who == null) return false;
                float pop = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.15f)), outk = Mathf.Clamp01((fx.age - flight) / 0.3f);
                var cam = ViewCamera;
                Quaternion face = cam != null ? cam.transform.rotation * Quaternion.Euler(0f, 0f, 12f * Mathf.Sin(fx.age * 8f)) : Quaternion.identity;
                CmPlace(card, DrawnHead(who) + Vector3.up * 0.6f, face, Vector3.one * Mathf.Max(0.01f, pop * (1f - outk)));
                return outk < 1f;
            });
            f2.props.Add(card);
        }

        // ---------------------------------------------------------------- 룩

        void S4WallFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            float tall = Params != null ? Params.rampartTall : 2f, thick = Params != null ? Params.rampartThick : 0.5f;
            float seconds = Mathf.Max(0.3f, e.size);
            // On another PC: the wall's picture (the host's is the solid one).
            if (by != null && by.NetworkPuppet) PawnRushKitWall.Raise(e.count, by, e.at, e.normal, tall, thick, seconds, 0.25f, false);
            Vector3 a = e.at, b = e.normal, along = b - a;
            along.y = 0f;
            float length = along.magnitude;
            along = along.sqrMagnitude > 1e-4f ? along.normalized : Vector3.right;
            for (int i = 0; i <= 4; i++) CmPoof(Vector3.Lerp(a, b, i / 4f) + Vector3.up * 0.15f, CmDust, 3, 0.6f, 0.3f, 0.45f, 0.3f);
            Shake(0.05f, 8f * Frame);
            UltClip(UltClipId.Castle, 0.8f * lvl, 0.8f);
            // Battlements along its top, rising and sinking with it.
            const int N = 4;
            var merlons = new Kit.Prop[N];
            var mesh = S4CapMesh();
            for (int i = 0; i < N; i++) merlons[i] = CmProp(mesh, CmRock, "Battlement", 0.014f);
            Quaternion turn = Quaternion.LookRotation(along, Vector3.up);
            var f = kit.Run(seconds + 0.6f, (fx, d) =>
            {
                float rise = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.25f)), sink = Mathf.Clamp01((fx.age - 0.25f - seconds) / 0.3f);
                if (sink >= 1f)
                {
                    foreach (var m in merlons) m.Show(false);
                    return false;
                }
                float top = tall * Mathf.Clamp01(fx.age / 0.25f) * (1f - sink);
                for (int i = 0; i < N; i++)
                {
                    Vector3 at = a + along * (length * (i + 0.5f) / N) + Vector3.up * top;
                    CmPlace(merlons[i], at, turn, new Vector3(1.2f, 1.1f, thick / 0.36f) * Mathf.Max(0.02f, rise) * (1f - sink));
                }
                return true;
            });
            foreach (var m in merlons) f.props.Add(m);
        }

        void S4SlideFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count == 0)
            {
                if (by == null) return;
                float time = Mathf.Max(0.1f, e.size);
                CmStreak(by, S3Colors(by), time + 0.15f, 0.24f, 0.2f);
                CmSkid(by, time, 0.04f);
                kit.AddSquash(by, Kit.SquashKind.Wide);
                UltClip(UltClipId.Whoosh, 0.7f * lvl, 1.2f);
                return;
            }
            CmPoof(e.at + Vector3.up * 0.1f, CmDust, 4, 0.5f, 0.22f, 0.35f, 0.25f);
        }

        void S4MowerFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count == 1)
            {
                // Mown: a burst of grass off it.
                kit.Drops(e.at, Vector3.up + FlatDir(e.dir) * 0.4f, CmGrass, 6, 7f);
                UltClip(UltClipId.Crack, 0.6f * lvl, 1.5f);
                return;
            }
            if (e.count == 2)
            {
                if (by != null) s4bBlades[by] = Mathf.Min(s4bBlades.TryGetValue(by, out var u) ? u : Clock, Clock);
                return;
            }
            if (by == null) return;
            float seconds = Mathf.Max(0.3f, e.size);
            s4bBlades[by] = Clock + seconds;
            var blade = CmProp(S4bBladeMesh(), CmSteel, "Mower blade", 0.012f);
            UltClip(UltClipId.Riser, 0.5f * lvl, 1.4f);
            float nextGrass = 0f, nextPuff = 0.1f, nextBuzz = 0f;
            var f = kit.Run(seconds + 0.5f, (fx, d) =>
            {
                if (by == null) return false;
                float until = s4bBlades.TryGetValue(by, out var u) ? u : Clock;
                float outk = Mathf.Clamp01((Clock - until) / 0.25f);
                if (outk >= 1f) return false;
                float k = GiantScaleOf(by);
                Vector3 up = HeadUp(by), top = DrawnHead(by) + up * (0.24f * k);
                float pop = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.2f));
                CmPlace(blade, top, Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, fx.age * 1500f, 0f), Vector3.one * (0.55f * k * Mathf.Max(0.02f, pop) * (1f - outk)));
                if (outk > 0f) return true;
                if (fx.age >= nextBuzz)
                {
                    nextBuzz = fx.age + 0.18f;
                    UltClip(UltClipId.Tick, 0.18f * S3Heard(top), 3f);
                }
                if (fx.age >= nextGrass && by.HorizontalSpeed > 1f)
                {
                    // Grass flying off behind it as it runs.
                    nextGrass = fx.age + 0.06f;
                    Vector3 back = -FlatDir(by.Facing);
                    kit.Drops(by.FeetPoint + Vector3.up * 0.15f + back * 0.3f, (back + Vector3.up * 1.2f).normalized, CmGrass, 1, 4.5f);
                }
                if (fx.age >= nextPuff)
                {
                    nextPuff = fx.age + 0.3f;
                    CmPoof(top + Vector3.up * 0.1f, CmSteam, 1, 0.15f, 0.08f, 0.35f, 0.8f);
                }
                return true;
            });
            f.props.Add(blade);
            f.end = () => s4bBlades.Remove(by);
        }

        // ---------------------------------------------------------------- 퀸

        void S4FanFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            var who = e.target;
            switch (e.count)
            {
                case 0:
                    if (by != null)
                    {
                        CmStreak(by, S3Colors(by), Mathf.Max(0.15f, e.size) + 0.1f, 0.2f, 0.16f);
                        S4bHoldFan(by, Mathf.Max(0.15f, e.size) + 0.6f);
                    }
                    UltClip(UltClipId.Whoosh, 0.6f * lvl, 1.7f);
                    break;
                case 1:
                    // The fan to the back of its head: "탁!", down on its face.
                    if (by != null) S4bSwingFan(by, e.at);
                    if (who != null) kit.AddSquash(who, Kit.SquashKind.Down);
                    PopWord("탁!", e.at + Vector3.up * 0.5f, Kit.Hex("#FF5A8A"), 0.42f, 0.7f, who);
                    UltClip(UltClipId.Tap, lvl, 1.2f);
                    break;
                case 2:
                    // Ducked: the fan whiffs over its head.
                    if (by != null) S4bSwingFan(by, e.at + Vector3.up * 0.35f);
                    if (who != null) kit.AddSquash(who, Kit.SquashKind.Flatten, 0.25f);
                    PopWord("휙", e.at + Vector3.up * 0.7f, Kit.Hex("#BFE6FF"), 0.36f, 0.6f, who);
                    UltClip(UltClipId.Whoosh, 0.6f * lvl, 2f);
                    break;
                case 3:
                {
                    // A check: the fan, a stagger, a red square under it.
                    if (by != null) S4bSwingFan(by, e.at);
                    CmFlash(e.at + Vector3.up * 0.1f, 0.3f, 6);
                    if (who != null)
                    {
                        kit.AddSquash(who, Kit.SquashKind.Bump);
                        var tile = new Kit.Tile(kit, "Check square");
                        Vector3 at = Ground(who.FeetPoint) + Vector3.up * 0.03f, along = FlatDir(e.dir);
                        var f = kit.Run(0.5f, (fx, d) =>
                        {
                            float k = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.12f));
                            tile.Floor(at, along, Vector2.one * (1.4f * Mathf.Max(0.05f, k)));
                            tile.shape = 0f;
                            tile.fill = Kit.A(CmRed.main, 0.85f);
                            tile.core = Kit.A(Color.white, 1f);
                            tile.coreWidth = 0.03f;
                            tile.ink = Kit.A(CmRed.ink, 1f);
                            tile.inkWidth = 0.03f;
                            tile.rim = Color.clear;
                            tile.stripe = Color.clear;
                            tile.fade = 1f - Mathf.Clamp01((fx.age - 0.3f) / 0.2f);
                            tile.Apply();
                            return true;
                        });
                        f.tiles.Add(tile);
                    }
                    UltClip(UltClipId.Check, 0.7f * lvl, 1f + 0.06f * e.size);
                    break;
                }
                case 4:
                    if (by != null) S4bHoldFan(by, Mathf.Max(0.3f, e.size) + 0.3f);
                    UltClip(UltClipId.March, 0.4f * lvl, 1.8f);
                    break;
                default:
                    if (by != null) s4bFans[by] = Mathf.Min(s4bFans.TryGetValue(by, out var u) ? u : Clock, Clock);
                    break;
            }
        }

        /// <summary>A red folding fan held open beside the queen while <paramref name="seconds"/> last (or until put away).</summary>
        void S4bHoldFan(RagdollPawn by, float seconds)
        {
            bool had = s4bFans.TryGetValue(by, out var held);
            s4bFans[by] = Mathf.Max(had ? held : 0f, Clock + seconds);   // (only ever longer: a swing mid-chase keeps it out)
            if (had) return;
            var fan = CmProp(S4bFanMesh(), CmRed, "Fan", 0.012f);
            var f = kit.Run(seconds + 6f, (fx, d) =>
            {
                if (by == null) return false;
                float until = s4bFans.TryGetValue(by, out var u) ? u : Clock;
                float outk = Mathf.Clamp01((Clock - until) / 0.2f);
                if (outk >= 1f) return false;
                Vector3 face = FlatDir(by.Facing), right = Vector3.Cross(Vector3.up, face);
                float k = GiantScaleOf(by);
                Vector3 at = by.bodies[(int)BodyId.Chest].position + (right * 0.32f + Vector3.up * 0.15f) * k;
                float flutter = 10f * Mathf.Sin(fx.age * 12f);
                CmPlace(fan, at, Quaternion.LookRotation(face, Vector3.up) * Quaternion.Euler(-20f + flutter, 0f, -30f),
                    Vector3.one * (0.42f * k * Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.15f)) * (1f - outk)));
                return true;
            });
            f.props.Add(fan);
            f.end = () => s4bFans.Remove(by);
        }

        /// <summary>The fan swung down at <paramref name="at"/>: a quick arc of white whoosh from over the queen's shoulder.</summary>
        void S4bSwingFan(RagdollPawn by, Vector3 at)
        {
            S4bHoldFan(by, 0.4f);
            var arc = new Kit.Strip(kit, CmWhite) { core = Color.white, coreShare = 0.5f, inkShare = 0.2f };
            var pts = new List<Vector3>(9);
            Vector3 from = by.bodies[(int)BodyId.Chest].position + Vector3.up * 0.6f;
            var f = kit.Run(0.22f, (fx, d) =>
            {
                float t = fx.age / 0.22f;
                pts.Clear();
                for (int i = 0; i <= 8; i++)
                {
                    float k = Mathf.Clamp01(t * 1.6f - (8 - i) * 0.06f);
                    Vector3 mid = Vector3.Lerp(from, at, 0.5f) + Vector3.up * 0.45f;
                    pts.Add(Vector3.Lerp(Vector3.Lerp(from, mid, k), Vector3.Lerp(mid, at, k), k));
                }
                arc.Build(pts, i => 0.1f * i / 8f, kit.Eye);
                arc.fade = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                arc.Apply();
                return true;
            });
            f.strips.Add(arc);
            CmFlash(at, 0.3f, 6);
        }

        void S4SacFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (by == null) return;
            switch (e.count)
            {
                case 0:
                {
                    // The glow: gold all over, rays turning, a rising note.
                    float glow = Mathf.Max(0.1f, e.size);
                    UltClip(UltClipId.Riser, 0.8f * lvl, 1.3f);
                    const int N = 10;
                    var rays = new Kit.Strip[N];
                    for (int i = 0; i < N; i++) rays[i] = new Kit.Strip(kit, CmGold) { core = Color.white, coreShare = 0.5f, inkShare = 0.15f };
                    var pts = new List<Vector3>(2);
                    float nextFlash = 0f;
                    var f = kit.Run(glow, (fx, d) =>
                    {
                        if (by == null) return false;
                        float t = fx.age / glow;
                        kit.Coil(by, 0.5f * t);
                        if (fx.age >= nextFlash)
                        {
                            nextFlash = fx.age + 0.1f;
                            kit.FlashBody(by, CmGold.main);
                        }
                        var cam = ViewCamera;
                        if (cam == null) return true;
                        Vector3 c = by.bodies[(int)BodyId.Chest].position;
                        for (int i = 0; i < N; i++)
                        {
                            float a = i * Mathf.PI * 2f / N + fx.age * 3f;
                            Vector3 dir = cam.transform.right * Mathf.Cos(a) + cam.transform.up * Mathf.Sin(a);
                            pts.Clear();
                            pts.Add(c + dir * 0.4f);
                            pts.Add(c + dir * (0.6f + 0.6f * t));
                            rays[i].Build(pts, j => 0.08f * (0.5f + t), kit.Eye);
                            rays[i].Apply();
                        }
                        return true;
                    });
                    foreach (var r in rays) f.strips.Add(r);
                    break;
                }
                case 1:
                    S4bSacBang(by, Mathf.Max(1f, e.size), lvl);
                    break;
                default:
                    if (sacs4.TryGetValue(by, out var until)) sacs4[by] = Mathf.Min(until, Clock);
                    break;
            }
        }

        readonly Dictionary<RagdollPawn, float> sacs4 = new Dictionary<RagdollPawn, float>();

        /// <summary>퀸 희생!!'s bang: a flash, rings, gold cloud; the queen hidden, her chunks flung round her and lying there,
        /// then flying back together with a pop.</summary>
        void S4bSacBang(RagdollPawn by, float range, float lvl)
        {
            Vector3 c = by.bodies[(int)BodyId.Chest].position, feet = by.FeetPoint;
            CmFlash(c, 1.4f, 10, CmGold);
            CmFlash(c, 0.9f, 7);
            kit.FlashRing(Ground(feet) + Vector3.up * 0.05f, range, CmGold);
            kit.HalfRing(c, Vector3.up, CmGold, 1.2f, 0.25f);
            CmPoof(c, CmGold, 12, range * 0.35f, 0.55f, 0.6f, 0.5f);
            CmPoof(c, CmWhite, 8, range * 0.25f, 0.45f, 0.5f, 0.6f);
            HitStop(6f * Frame);
            Shake(0.16f, 16f * Frame);
            UltClip(UltClipId.Crack, lvl, 0.5f);
            UltClip(UltClipId.Capture, 0.8f * lvl, 0.7f);
            float scatter = Params != null ? Params.sac4Scatter : 1.5f;
            sacs4[by] = Clock + scatter;
            var p = S3Colors(by);
            const int N = 7;
            var chunks = new Kit.Prop[N];
            var lie = new Vector3[N];
            var vel = new Vector3[N];
            var pos = new Vector3[N];
            var spinAxis = new Vector3[N];
            for (int i = 0; i < N; i++)
            {
                chunks[i] = CmProp(i % 2 == 0 ? kit.meshBall : S4CapMesh(), i == 0 ? CmGold : p, "Queen chunk", 0.012f);
                float a = (i + Random.value * 0.6f) * Mathf.PI * 2f / N;
                Vector3 outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                lie[i] = FloorBelow(feet + outDir * Random.Range(1f, 1.8f) + Vector3.up * 1.5f, 4f) + Vector3.up * 0.08f;
                pos[i] = c;
                vel[i] = outDir * Random.Range(3f, 4.5f) + Vector3.up * Random.Range(3f, 5f);
                spinAxis[i] = Random.onUnitSphere;
            }
            bool reformed = false;
            var f = kit.Run(scatter + 3f, (fx, d) =>
            {
                if (by == null) return false;
                float until = sacs4.TryGetValue(by, out var u) ? u : Clock;
                if (by.skin != null) by.skin.forceRenderingOff = true;
                float back = Clock - until;   // < 0: lying in pieces; >= 0: flying back
                Vector3 home = by.bodies[(int)BodyId.Chest].position;
                for (int i = 0; i < N; i++)
                {
                    Vector3 at;
                    if (back < 0f)
                    {
                        // Flung out, bouncing to rest on the floor.
                        vel[i] += Physics.gravity * d;
                        pos[i] += vel[i] * d;
                        if (pos[i].y < lie[i].y)
                        {
                            pos[i].y = lie[i].y;
                            vel[i] = new Vector3(vel[i].x * 0.4f, Mathf.Abs(vel[i].y) * 0.3f, vel[i].z * 0.4f);
                        }
                        at = pos[i];
                    }
                    else
                    {
                        float t = Mathf.Clamp01(back / 0.3f);
                        at = Vector3.Lerp(pos[i], home, Kit.EaseIn(t)) + Vector3.up * (0.6f * Mathf.Sin(t * Mathf.PI));
                    }
                    CmPlace(chunks[i], at, Quaternion.AngleAxis(fx.age * 400f, spinAxis[i]), Vector3.one * (i % 2 == 0 ? 0.26f : 0.7f));
                }
                if (back >= 0.3f && !reformed)
                {
                    // Put together again: a gold pop, and there she is.
                    reformed = true;
                    if (by.skin != null) by.skin.forceRenderingOff = false;
                    CmPoof(home, CmGold, 8, 0.8f, 0.32f, 0.45f, 0.5f);
                    CmFlash(home, 0.6f, 8, CmGold);
                    kit.AddSquash(by, Kit.SquashKind.Spring);
                    UltClip(UltClipId.Promote, 0.7f * S3Heard(home), 1.2f);
                    return false;
                }
                return true;
            });
            foreach (var ch in chunks) f.props.Add(ch);
            f.end = () =>
            {
                if (by != null && by.skin != null) by.skin.forceRenderingOff = false;
                sacs4.Remove(by);
            };
        }

        // ---------------------------------------------------------------- shapes (made once, in code)

        /// <summary>A bell, mouth down, its rim at the origin and its crown at 1 m; 1 m across at the rim.</summary>
        Mesh S4bBellMesh()
        {
            if (s4bBellMesh != null) return s4bBellMesh;
            // (Lathe profile from the crown down to the flared lip; then the lip turned in a little.)
            var outer = new[]
            {
                new Vector2(0f, 1f), new Vector2(0.16f, 0.99f), new Vector2(0.26f, 0.95f), new Vector2(0.3f, 0.86f), new Vector2(0.31f, 0.72f),
                new Vector2(0.33f, 0.55f), new Vector2(0.37f, 0.38f), new Vector2(0.43f, 0.2f), new Vector2(0.5f, 0.07f), new Vector2(0.52f, 0f),
                new Vector2(0.46f, 0.02f),
            };
            // CmLathe goes foot first: reverse the list so it starts at the lip.
            System.Array.Reverse(outer);
            s4bBellMesh = CmLathe(outer, 28, "Bell");
            return s4bBellMesh;
        }

        /// <summary>A saw blade lying flat, 1 m across, sixteen teeth, a hub.</summary>
        Mesh S4bBladeMesh()
        {
            if (s4bBladeMesh != null) return s4bBladeMesh;
            const int Teeth = 16;
            var pts = new Vector2[Teeth * 2];
            for (int i = 0; i < Teeth * 2; i++)
            {
                float a = i * Mathf.PI / Teeth, r = i % 2 == 0 ? 0.5f : 0.4f;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            var flat = CmExtrude(pts, 0.04f, "Mower blade");
            var v = flat.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = new Vector3(v[i].x, v[i].z, v[i].y);
            flat.vertices = v;
            var tri = flat.triangles;
            for (int i = 0; i < tri.Length; i += 3) (tri[i + 1], tri[i + 2]) = (tri[i + 2], tri[i + 1]);
            flat.triangles = tri;
            flat.RecalculateNormals();
            flat.RecalculateBounds();
            return s4bBladeMesh = flat;
        }

        /// <summary>A folding fan open in a half circle, 1 m from its rivet to its edge, standing in the XY plane.</summary>
        Mesh S4bFanMesh()
        {
            if (s4bFanMesh != null) return s4bFanMesh;
            const int N = 14;
            var pts = new List<Vector2> { new Vector2(0f, 0f) };
            for (int i = 0; i <= N; i++)
            {
                float a = Mathf.Lerp(15f, 165f, i / (float)N) * Mathf.Deg2Rad, r = i % 2 == 0 ? 1f : 0.93f;   // (pleats)
                pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            return s4bFanMesh = CmExtrude(pts.ToArray(), 0.03f, "Fan");
        }

        /// <summary>A wind-up key: a ring and its stem, 1 m from the stem's end to the ring's far side (scaled to a pawn).</summary>
        Mesh S4bKeyMesh()
        {
            if (s4bKeyMesh != null) return s4bKeyMesh;
            var stem = kit.RoundBox(new Vector3(0.06f, 0.06f, 0.25f), 0.02f);
            var parts = new[]
            {
                new CombineInstance { mesh = stem, transform = Matrix4x4.Translate(new Vector3(0f, 0f, -0.12f)) },
                new CombineInstance { mesh = kit.meshTorus, transform = Matrix4x4.TRS(new Vector3(0f, 0.12f, -0.26f), Quaternion.Euler(90f, 0f, 0f), Vector3.one * 0.18f) },
                new CombineInstance { mesh = kit.meshTorus, transform = Matrix4x4.TRS(new Vector3(0f, -0.12f, -0.26f), Quaternion.Euler(90f, 0f, 0f), Vector3.one * 0.18f) },
            };
            s4bKeyMesh = new Mesh { name = "Clockwork key", hideFlags = HideFlags.HideAndDontSave };
            s4bKeyMesh.CombineMeshes(parts, true, true);
            s4bKeyMesh.RecalculateNormals();
            s4bKeyMesh.RecalculateBounds();
            Destroy(stem);
            return s4bKeyMesh;
        }

        /// <summary>Pale blue (lit) for one poisoned (ROUND1: "얼굴이 파래진다").</summary>
        Material S4bSickMaterial()
        {
            if (s4bSick != null) return s4bSick;
            if (LitMaterials.Lit == null) return null;
            s4bSick = LitMaterials.NewLit(Kit.Hex("#9CC8F0"), 0.35f, 0f);
            s4bSick.hideFlags = HideFlags.HideAndDontSave;
            return s4bSick;
        }

        void DestroySetFourB()
        {
            foreach (var kv in sacs4)
                if (kv.Key != null && kv.Key.skin != null) kv.Key.skin.forceRenderingOff = false;
            sacs4.Clear();
            s4bTraps.Clear();
            s4bStorms.Clear();
            s4bBells.Clear();
            s4bCrooks.Clear();
            s4bBlades.Clear();
            s4bFans.Clear();
            foreach (var m in new UnityEngine.Object[] { s4bBellMesh, s4bBladeMesh, s4bFanMesh, s4bKeyMesh, s4bCardMesh, s4bSick })
                if (m != null) Destroy(m);
            s4bBellMesh = s4bBladeMesh = s4bFanMesh = s4bKeyMesh = s4bCardMesh = null;
            s4bSick = null;
            DestroySetFourC();
        }
    }
}
