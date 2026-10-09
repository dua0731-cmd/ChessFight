using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Sword Fight skill effects (R91) in design A "잉크 테두리 장난감 체스" (picked 2026-10-08 for all three modes; the
    /// shared parts are <see cref="SkillInkKit"/>, R90). Warnings are real board squares and rings on the floor along each
    /// skill's own way, in the piece's colour with the viewer's rim (cream mine, teal my side's, red with sliding stripes
    /// the other side's): they fill as the windup runs, snap full in its last six frames, flash white for two and are gone
    /// in six. The aim before it (held right click) is only shown to its owner, faint. Hits: the body white for two frames
    /// then the hitter's colour for two, round drops off the way it flies, a C half ring, a short hit stop and a shake;
    /// a piece that goes down gets the captured square in the hitter's colour. The king's guard is an emerald arc in front
    /// of him and the reach of his answer on the floor; the queen's thrust a gold ribbon down her line; the rook's wave an
    /// orange wall running along the floor lighting each square it crosses; the bishop's two violet rays meeting in an X
    /// (gold when the floor ends behind the piece: it will be pinned, four stakes and a ring); the knight's horseshoe and
    /// its two sky rings ahead. No glow, no words, no stars, no sound (승규 님, 10-09).
    /// Test-bed stand-ins: the hit stop holds the whole game, the shake moves the view camera.
    /// </summary>
    [DefaultExecutionOrder(210)]   // after the match camera (150)
    public class SwordFightSkillFx : MonoBehaviour
    {
        public bool effects = true;
        [Tooltip("맞는 순간 게임 전체를 잠깐 멈춤 (시험용: 실제로는 때린 쪽·맞은 쪽만)")]
        public bool hitStop = true;
        public bool shake = true;

        /// <summary>The film's camera while it records; the match camera otherwise.</summary>
        public static Camera ViewOverride;

        const float F = Kit.F;
        Kit kit;
        SwordFightGame game;
        float rate = 1f, stopLeft, stopResume = 1f, shakeAmp, shakeLeft, shakeTotal, shakeClock, dt;
        bool stopping;

        RagdollPawn Viewer => game != null && game.Local != null ? game.Local.Pawn : null;
        public Camera ViewCamera => ViewOverride != null ? ViewOverride : game != null && game.CameraRig != null ? game.CameraRig.Cam : Camera.main;
        Vector3 Eye => ViewCamera != null ? ViewCamera.transform.position : Vector3.up * 5f;
        SwordFightSkillParams S => GetComponent<SwordFightSkillBed>() is SwordFightSkillBed b ? b.skills : null;

        void Awake()
        {
            game = GetComponent<SwordFightGame>();
            if (game == null) game = FindFirstObjectByType<SwordFightGame>();
            kit = new Kit(new GameObject("Sword Fight skill effects").transform, () => Viewer, () => Eye);
            SwordFightSkills.Fx += OnFx;
        }

        void OnDestroy()
        {
            SwordFightSkills.Fx -= OnFx;
            if (stopping) Time.timeScale = stopResume;
            kit?.Destroy();
        }

        // ---------------------------------------------------------------- time, hit stop, shake

        void Update()
        {
            // Effect time: the game's own pace (slow motion slows the effects too), but a hit stop does not stop them.
            float real = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (Time.timeScale > 0f && !stopping) rate = Time.timeScale;
            dt = real * rate;
            if (stopping)
            {
                stopLeft -= dt;
                if (stopLeft <= 0f)
                {
                    stopping = false;
                    if (Time.timeScale == 0f) Time.timeScale = stopResume;
                }
            }
        }

        void HitStop(int frames)
        {
            if (!hitStop || frames <= 0) return;
            if (!stopping)
            {
                stopResume = Time.timeScale > 0f ? Time.timeScale : stopResume;
                stopping = true;
            }
            stopLeft = Mathf.Max(stopLeft, frames * F);
            Time.timeScale = 0f;
        }

        /// <summary>A shake of the view (A: only the hitter's and the hit piece's camera).</summary>
        void Shake(RagdollPawn a, RagdollPawn b, float metres, int frames)
        {
            if (!shake) return;
            var v = Viewer;
            if (ViewOverride == null && v != null && a != v && b != v) return;
            metres *= ChessFight.Game.GameSettings.ShakeScale;
            float now = shakeTotal > 0f ? shakeAmp * Mathf.Clamp01(shakeLeft / shakeTotal) : 0f;
            if (metres < now) return;
            shakeAmp = metres;
            shakeLeft = shakeTotal = frames * F;
        }

        void LateUpdate()
        {
            if (kit == null) return;
            if (effects) Watch();
            kit.Step(dt);
            var cam = ViewCamera;
            if (shakeLeft > 0f && cam != null)
            {
                shakeLeft -= dt;
                shakeClock += dt;
                float k = Mathf.Clamp01(shakeLeft / Mathf.Max(0.01f, shakeTotal));
                float a = shakeAmp * k * k, t = shakeClock;
                float x = Mathf.Sin(t * 61f) * 0.6f + Mathf.Sin(t * 97f + 1.3f) * 0.4f;
                float y = Mathf.Sin(t * 71f + 2.1f) * 0.6f + Mathf.Sin(t * 113f + 0.4f) * 0.4f;
                var view = cam.transform;
                view.position += (view.right * x + view.up * y * 0.7f) * a;
                view.rotation *= Quaternion.Euler(y * a * 10f, x * a * 10f, Mathf.Sin(t * 53f + 4.2f) * a * 6f);
            }
        }

        // ---------------------------------------------------------------- warnings (held while the state lasts)

        struct Mark
        {
            public Vector3 at, along;
            public Vector2 size;
            public float shape, inner, gap;
        }

        class Warning
        {
            public readonly List<Mark> marks = new List<Mark>();
            public float k, alpha = 1f;
            /// <summary>An aim: when it ends (the skill goes, or is called off) it only fades, the white is the warning's.</summary>
            public bool quiet;
            public Kit.Palette? fill;
        }

        readonly Dictionary<(object, string), Warning> warnings = new Dictionary<(object, string), Warning>();

        /// <summary>Show these marks this frame as <paramref name="caster"/>'s warning (strength <paramref name="k"/>); when it
        /// stops asking they flash white for two frames and are gone in six.</summary>
        void Warn(SwordFightSkills owner, string key, Warning w)
        {
            string id = key + "#" + w.marks.Count;
            warnings[(owner, id)] = w;
            var caster = owner.Pawn;
            kit.Keep(owner, id, () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < w.marks.Count; i++) fx.tiles.Add(new Kit.Tile(kit, key));
                float released = -1f;
                fx.step = (x, d) =>
                {
                    if (caster == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float a = released >= 0f ? (x.age - released) / F : -1f;
                    if (a >= 6f) return false;
                    var cur = warnings[(owner, id)];
                    float flash = a >= 0f && a < 2f && !cur.quiet ? 1f : 0f;
                    float fade = released < 0f ? Mathf.Clamp01(x.age / (4f * F)) : cur.quiet ? 1f - Mathf.Clamp01(a / 4f) : 1f - Mathf.Clamp01((a - 2f) / 4f);
                    for (int i = 0; i < x.tiles.Count && i < cur.marks.Count; i++)
                    {
                        var t = x.tiles[i];
                        var m = cur.marks[i];
                        t.Floor(m.at, m.along, m.size);
                        t.shape = m.shape;
                        t.inner = m.inner;
                        t.gap = m.gap;
                        t.arc = 1f;
                        t.round = 0.07f;
                        t.fade = fade * cur.alpha;
                        kit.Warn(t, caster, released >= 0f ? 1f : cur.k, m.shape == 2f ? 0f : flash, cur.fill);   // a big disc going white floods the view
                    }
                    return true;
                };
                return fx;
            });
        }

        static Mark Square(Vector3 at, Vector3 along, float width, float length) =>
            new Mark { at = at + Vector3.up * 0.016f, along = along, size = new Vector2(width, length) };

        static Mark Ring(Vector3 at, float r, float band, Vector3 along = default) =>
            new Mark { at = at + Vector3.up * 0.018f, along = along.sqrMagnitude > 0f ? along : Vector3.forward, size = Vector2.one * (2f * r), shape = 1f, inner = Mathf.Clamp01(1f - band / r) };

        static Mark Disc(Vector3 at, float r) =>
            new Mark { at = at + Vector3.up * 0.014f, along = Vector3.forward, size = Vector2.one * (2f * r), shape = 2f };

        /// <summary>Board squares along a line on the floor, <paramref name="width"/> across, about 1.5 m long each.</summary>
        static List<Mark> Lane(Vector3 from, Vector3 dir, float start, float length, float width)
        {
            var list = new List<Mark>();
            int n = Mathf.Max(1, Mathf.CeilToInt((length - start) / 1.5f - 0.15f));
            float each = (length - start) / n;
            for (int i = 0; i < n; i++)
            {
                Vector3 c = from + dir * (start + each * (i + 0.5f));
                list.Add(Square(Kit.FloorUnder(c, 1.5f), dir, width, each - 0.06f));
            }
            return list;
        }

        // ---------------------------------------------------------------- watching the pieces each frame

        void Watch()
        {
            var S = this.S;
            if (S == null) return;
            foreach (var s in SwordFightSkills.All)
            {
                if (s == null || s.Pawn == null || s.Fighter == null || !s.Fighter.Alive) continue;
                bool mine = s.Pawn == Viewer || ViewOverride != null;
                if (s.Stage == SfStage.Aim && !mine) continue;   // the aim is the owner's own
                float aimAlpha = s.Stage == SfStage.Aim ? 0.55f : 1f;
                switch (s.Piece)
                {
                    case PieceKind.King: KingMarks(s, S); break;
                    case PieceKind.Queen: QueenMarks(s, S, aimAlpha); break;
                    case PieceKind.Rook: RookMarks(s, S, aimAlpha); break;
                    case PieceKind.Bishop: BishopMarks(s, S, aimAlpha); break;
                    case PieceKind.Knight: KnightMarks(s, S, aimAlpha); break;
                }
                if (s.SlowLeft > 0f) SlowRing(s);
                if (s.PinLeft > 0f) PinMarks(s);
            }
        }

        float Charge(SwordFightSkills s, float windup) => s.Stage == SfStage.Aim ? 0.3f : Kit.Charge(s.StageTime, windup);

        // ---------------------------------------------------------------- king

        void KingMarks(SwordFightSkills s, SwordFightSkillParams S)
        {
            if (s.Stage == SfStage.Recovery && !s.KingCountered)
            {
                // The guard came to nothing: a grey ring with sliding stripes at his feet while he is open (빈틈).
                var open = s;
                kit.Keep(s, "king open", () =>
                {
                    var fx = new Kit.Fx();
                    fx.tiles.Add(new Kit.Tile(kit, "King open"));
                    fx.step = (x, d) =>
                    {
                        if (open.Pawn == null || !kit.Held(x)) return false;
                        var t = x.tiles[0];
                        t.Floor(Kit.FloorUnder(open.Pawn.Hips.position, 0.6f) + Vector3.up * 0.02f, Vector3.forward, Vector2.one * 1.3f * Kit.Pop(x.age, 1.15f, 0.5f));
                        Kit.Horseshoe(t, Kit.Grey, 0.95f, 0f);
                        t.gap = 0f;
                        t.inner = 0.6f;
                        t.stripe = Kit.A(Kit.TeamEnemy.deep, 0.6f);
                        t.stripePhase = kit.Clock * 0.8f;
                        t.fade = 1f;
                        t.Apply();
                        return true;
                    };
                    return fx;
                });
                return;
            }
            if (s.Stage != SfStage.Windup) return;
            Vector3 floor = Kit.FloorUnder(s.Pawn.Hips.position, 0.6f);
            var w = new Warning { k = Kit.Charge(s.StageTime, S.kingGuard), alpha = 0.9f };
            w.marks.Add(Ring(floor, S.kingCounterRadius, 0.16f));
            Warn(s, "king reach", w);
            kit.Coil(s.Pawn, Mathf.Clamp01(s.StageTime / 0.12f) * 0.6f);
            // The guard: an emerald arc standing in front of him, thick, pulsing a little.
            var pawn = s.Pawn;
            kit.Keep(s, "king guard", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "King guard"));
                float released = -1f;
                fx.step = (x, d) =>
                {
                    if (pawn == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float a = released >= 0f ? (x.age - released) / F : -1f;
                    if (a >= 6f) return false;
                    Vector3 f = Kit.FlatDir(pawn.Facing, Vector3.forward);
                    var t = x.tiles[0];
                    float pop = Kit.Pop(x.age, 1.15f, 0.4f) * (1f + 0.03f * Mathf.Sin(x.age * 40f));
                    // Local +x up (the horseshoe opens to −x): an arch over the top, open at his feet.
                    t.Facing(pawn.bodies[(int)BodyId.Chest].position + f * 0.42f + Vector3.up * 0.05f, f, Vector2.one * (1.5f * pop), Vector3.Cross(f, Vector3.up));
                    Kit.Horseshoe(t, Kit.King, 0.95f, 150f);
                    t.inner = 0.72f;
                    t.flash = a >= 0f && a < 2f ? 1f : 0f;
                    t.fade = released < 0f ? 1f : 1f - Mathf.Clamp01((a - 2f) / 4f);
                    t.Apply();
                    return true;
                };
                return fx;
            });
        }

        // ---------------------------------------------------------------- queen

        void QueenMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            if (s.Stage != SfStage.Aim && s.Stage != SfStage.Windup) return;
            Vector3 dir = s.Stage == SfStage.Aim ? s.AimDir : s.Dir;
            var w = new Warning { k = Charge(s, S.queenWindup), alpha = alpha, quiet = s.Stage == SfStage.Aim };
            w.marks.AddRange(Lane(s.Origin, dir, 0.3f, S.queenLength + 0.3f, S.queenWidth));
            Warn(s, s.Stage == SfStage.Aim ? "queen aim" : "queen line", w);
            if (s.Stage == SfStage.Windup) kit.Coil(s.Pawn, Mathf.Clamp01(s.StageTime / S.queenWindup));
        }

        void QueenThrust(SfFxEvent e)
        {
            var strip = new Kit.Strip(kit, Kit.Queen) { core = Color.white, coreShare = 0.35f, inkShare = 0.2f };
            Vector3 from = e.at + Vector3.up * 0.75f + e.dir * 0.35f, to = e.at + Vector3.up * 0.7f + e.dir * (e.size + 0.3f);
            float thrust = S != null ? S.queenThrust : 0.12f;
            var f = kit.Run(thrust + 0.3f, (fx, d) =>
            {
                var pts = new List<Vector3>();
                for (int i = 0; i <= 12; i++) pts.Add(Vector3.Lerp(from, to, i / 12f));
                strip.Build(pts, i => Mathf.Lerp(0.42f, 0.18f, i / 12f), kit.Eye);
                strip.head = Kit.EaseOut(fx.age / thrust);
                strip.tail = fx.age < thrust ? 0f : Kit.EaseIn((fx.age - thrust) / 0.3f);
                strip.taper = 0.6f;
                strip.Apply();
                return true;
            });
            f.strips.Add(strip);
            kit.FlashRing(e.at, 0.7f, Kit.Queen);
            kit.AddSquash(e.by.Pawn, Kit.SquashKind.Spring);
            Shake(e.by.Pawn, null, 0.06f, 5);
        }

        // ---------------------------------------------------------------- rook

        void RookMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            if (s.Stage == SfStage.Aim || s.Stage == SfStage.Windup)
            {
                Vector3 dir = s.Stage == SfStage.Aim ? s.AimDir : s.Dir;
                float reach = s.Stage == SfStage.Aim ? S.rookLength : s.Reach;
                var w = new Warning { k = Charge(s, S.rookWindup), alpha = alpha, quiet = s.Stage == SfStage.Aim };
                w.marks.AddRange(Lane(s.Origin, dir, 0.6f, reach, S.rookWidth));
                Warn(s, s.Stage == SfStage.Aim ? "rook aim" : "rook lane", w);
                if (s.Stage == SfStage.Windup) kit.Coil(s.Pawn, Mathf.Clamp01(s.StageTime / S.rookWindup) * 0.7f);
                return;
            }
            if (s.Stage != SfStage.Active) return;
            // The wave: an orange wall running down the lane, its squares lighting up white as it crosses them.
            var owner = s;
            kit.Keep(s, "rook wave", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "Rook wave"));
                fx.tiles.Add(new Kit.Tile(kit, "Rook wave core"));
                float stamped = 0.6f;
                float released = -1f;
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float a = released >= 0f ? (x.age - released) / F : -1f;
                    if (a >= 5f) return false;
                    float front = owner.Stage == SfStage.Active ? owner.WaveFront : owner.Reach;
                    Vector3 at = Kit.FloorUnder(owner.Origin + owner.Dir * front, 1.5f);
                    float fade = released < 0f ? 1f : 1f - a / 5f;
                    var wall = x.tiles[0];
                    wall.Facing(at + Vector3.up * 0.42f, owner.Dir, new Vector2(1.7f, 0.8f), Vector3.up);
                    wall.shape = 0f;
                    wall.round = 0.3f;
                    wall.fill = Kit.A(Kit.Rook.main, 0.92f);
                    wall.core = Kit.A(Kit.Rook.light, 1f);
                    wall.coreWidth = 0.05f;
                    wall.ink = Kit.A(Kit.Rook.ink, 1f);
                    wall.inkWidth = 0.05f;
                    wall.rim = Kit.A(Color.white, 0.9f);
                    wall.rimWidth = 0.1f;
                    wall.stripe = Color.clear;
                    wall.fade = fade;
                    wall.Apply();
                    var lip = x.tiles[1];
                    lip.Floor(at + Vector3.up * 0.02f - owner.Dir * 0.35f, owner.Dir, new Vector2(1.6f, 0.7f));
                    lip.shape = 0f;
                    lip.round = 0.3f;
                    lip.fill = Kit.A(Color.white, 0.85f);
                    lip.core = Color.clear;
                    lip.ink = Kit.A(Kit.Rook.ink, 0.9f);
                    lip.inkWidth = 0.05f;
                    lip.rim = Color.clear;
                    lip.stripe = Color.clear;
                    lip.fade = fade;
                    lip.Apply();
                    while (stamped + 1.5f <= front + 0.01f)
                    {
                        Vector3 c = Kit.FloorUnder(owner.Origin + owner.Dir * (stamped + 0.75f), 1.5f) + Vector3.up * 0.016f;
                        kit.Release(new List<Vector3> { c }, owner.Dir, owner.Pawn, 1.45f);
                        kit.DustRing(c, 3, 0.7f, null, 0.26f, 0.45f);
                        stamped += 1.5f;
                    }
                    return true;
                };
                return fx;
            });
        }

        // ---------------------------------------------------------------- bishop

        void BishopMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            if (s.Stage != SfStage.Aim && s.Stage != SfStage.Windup) return;
            bool aim = s.Stage == SfStage.Aim;
            Vector3 p = aim ? s.AimPoint : s.Point, dir = aim ? s.AimDir : s.Dir;
            bool pin = s.PinCondition;
            var w = new Warning { k = Charge(s, S.bishopWindup), alpha = alpha, quiet = aim, fill = pin ? Kit.Queen : (Kit.Palette?)null };
            Vector3 c = Kit.FloorUnder(p, 1.5f);
            w.marks.Add(new Mark { at = c + Vector3.up * 0.02f, along = Quaternion.AngleAxis(45f, Vector3.up) * dir, size = new Vector2(0.24f, 2.1f) });
            w.marks.Add(new Mark { at = c + Vector3.up * 0.021f, along = Quaternion.AngleAxis(-45f, Vector3.up) * dir, size = new Vector2(0.24f, 2.1f) });
            w.marks.Add(Ring(c, S.bishopRadius, 0.12f, dir));
            if (pin && s.PinCandidate != null)
            {
                // The floor ends behind it: a gold strip from the piece to the edge.
                Vector3 from = Kit.FloorUnder(s.PinCandidate.Hips.position, 0.6f);
                w.marks.Add(Square(from + dir * (S.bishopPinBehind * 0.5f), dir, 0.3f, S.bishopPinBehind));
            }
            Warn(s, aim ? "bishop aim" : "bishop x", w);
            if (aim) return;
            // The two rays gathering on the point as the windup runs (thin, then the throw makes them thick).
            var owner = s;
            kit.Keep(s, "bishop rays", () =>
            {
                var fx = new Kit.Fx();
                var pal = pin ? Kit.Queen : Kit.Bishop;
                fx.strips.Add(new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.4f });
                fx.strips.Add(new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.4f });
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x)) return false;
                    float k = Kit.Charge(owner.StageTime, S.bishopWindup);
                    for (int i = 0; i < 2; i++)
                    {
                        var (a, b) = RayEnds(owner, i == 0 ? -1f : 1f);
                        var pts = new List<Vector3> { a, Vector3.Lerp(a, b, 0.5f), b };
                        x.strips[i].Build(pts, j => 0.06f + 0.06f * k, kit.Eye);
                        x.strips[i].head = Mathf.Lerp(0.25f, 1f, k);
                        x.strips[i].opacity = 0.7f;
                        x.strips[i].Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>A ray from beside the bishop's shoulder through the point and 1.6 m on into the floor.</summary>
        static (Vector3, Vector3) RayEnds(SwordFightSkills s, float side)
        {
            Vector3 perp = Vector3.Cross(Vector3.up, s.Dir).normalized;
            Vector3 a = s.Pawn.bodies[(int)BodyId.Chest].position + perp * (side * 0.9f) + Vector3.up * 0.35f;
            Vector3 p = Kit.FloorUnder(s.Point, 1.5f) + Vector3.up * 0.5f;
            Vector3 u = (p - a).normalized;
            Vector3 b = p + u * 1.6f;
            if (b.y < p.y - 0.45f) b = p + u * ((0.45f) / Mathf.Max(0.05f, -u.y));
            return (a, b);
        }

        void BishopFire(SfFxEvent e)
        {
            var s = e.by;
            var pal = s.PinCondition ? Kit.Queen : Kit.Bishop;
            for (int i = 0; i < 2; i++)
            {
                var (a, b) = RayEnds(s, i == 0 ? -1f : 1f);
                var strip = new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.4f, inkShare = 0.22f };
                var f = kit.Run(0.3f, (fx, d) =>
                {
                    var pts = new List<Vector3> { a, Vector3.Lerp(a, b, 0.5f), b };
                    float k = fx.age / fx.life;
                    strip.Build(pts, j => fx.age < 2f * F ? 0.34f : 0.26f * (1f - k * 0.5f), kit.Eye);
                    strip.core = fx.age < 2f * F ? Color.white : pal.light;
                    strip.tail = Kit.EaseIn(k);
                    strip.Apply();
                    return true;
                });
                f.strips.Add(strip);
            }
            Vector3 c = Kit.FloorUnder(e.at, 1.5f);
            kit.FlashRing(c, e.size, pal);
            kit.ImpactRing(c + Vector3.up * 0.5f, Vector3.up, pal, 0.2f, 0.7f);
            HitStop(3);
            Shake(s.Pawn, null, 0.06f, 4);
        }

        void SlowRing(SwordFightSkills s)
        {
            var pawn = s.Pawn;
            var owner = s;
            kit.Keep(s, "slowed", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "Slow ring"));
                fx.tiles.Add(new Kit.Tile(kit, "Slow ripple"));
                fx.step = (x, d) =>
                {
                    if (pawn == null || !kit.Held(x) || owner.SlowLeft <= 0f) return false;
                    Vector3 at = Kit.FloorUnder(pawn.Hips.position, 0.6f) + Vector3.up * 0.02f;
                    var r = x.tiles[0];
                    r.Floor(at, Vector3.forward, Vector2.one * 1.1f * Kit.Pop(x.age, 1.15f, 0.4f));
                    Kit.Horseshoe(r, Kit.Bishop, 0.9f, 0f);
                    r.gap = 0f;
                    r.fade = Mathf.Clamp01(owner.SlowLeft / 0.2f);
                    r.Apply();
                    float w = (x.age * 1.6f) % 1f;
                    var rip = x.tiles[1];
                    rip.Floor(at - Vector3.up * 0.002f, Vector3.forward, Vector2.one * (1.1f + 0.9f * w));
                    rip.shape = 1f;
                    rip.inner = 0.88f;
                    rip.fill = Kit.A(Kit.Bishop.main, 0.6f * (1f - w));
                    rip.ink = Kit.A(Kit.Bishop.ink, 0.6f * (1f - w));
                    rip.inkWidth = 0.02f;
                    rip.core = Color.clear;
                    rip.rim = Color.clear;
                    rip.stripe = Color.clear;
                    rip.fade = r.fade;
                    rip.Apply();
                    return true;
                };
                return fx;
            });
        }

        void PinMarks(SwordFightSkills s)
        {
            var pawn = s.Pawn;
            var owner = s;
            kit.Keep(s, "pinned", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "Pin ring"));
                var gold = Kit.Queen;
                for (int i = 0; i < 4; i++)
                {
                    var stake = new Kit.Prop(kit, kit.RoundBox(new Vector3(0.09f, 0.55f, 0.09f), 0.03f), gold, "Pin stake") { inkWidth = 0.014f };
                    fx.props.Add(stake);
                }
                Vector3 at = Kit.FloorUnder(pawn.Hips.position, 0.6f);
                fx.step = (x, d) =>
                {
                    if (pawn == null) return false;
                    bool on = kit.Held(x) && owner.PinLeft > 0f;
                    if (!on)
                    {
                        kit.PuffBurst(at + Vector3.up * 0.3f, gold, 4, 0.5f, 0.16f, 0.35f);
                        return false;
                    }
                    float drop = Kit.EaseOut(x.age / (6f * F));
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = (45f + 90f * i) * Mathf.Deg2Rad;
                        Vector3 o = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.62f;
                        var p = x.props[i];
                        p.t.position = at + o + Vector3.up * Mathf.Lerp(1.1f, 0.2f, drop);
                        p.t.rotation = Quaternion.FromToRotation(Vector3.up, (Vector3.up * 3f - o).normalized);
                        p.Apply();
                    }
                    var r = x.tiles[0];
                    r.Floor(at + Vector3.up * 0.022f, Vector3.forward, Vector2.one * 1.5f * Kit.Pop(x.age, 1.1f, 0.6f));
                    Kit.Horseshoe(r, gold, 0.95f, 0f);
                    r.gap = 0f;
                    r.flash = x.age < 2f * F ? 1f : 0f;
                    r.fade = 1f;
                    r.Apply();
                    return true;
                };
                return fx;
            });
        }

        // ---------------------------------------------------------------- knight

        void KnightMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            bool aim = s.Stage == SfStage.Aim, air = s.Stage == SfStage.Active;
            if (!aim && !air) return;
            Vector3 p = aim ? s.AimPoint : s.Point, dir = aim ? s.AimDir : s.Dir;
            var w = new Warning { k = aim ? 0.3f : Kit.Charge(s.StageTime, S.knightAir), alpha = alpha, quiet = aim };
            foreach (var spot in s.KnightSpots)
            {
                w.marks.Add(Disc(spot, S.knightSpotRadius));
                w.marks.Add(Ring(spot, S.knightSpotRadius, 0.13f));
            }
            Warn(s, aim ? "knight aim" : "knight spots", w);
            // The landing: a horseshoe open behind, shrinking to the touchdown.
            var owner = s;
            kit.Keep(s, aim ? "knight shoe aim" : "knight shoe", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "Knight landing"));
                fx.step = (x, d) =>
                {
                    if (owner == null || !kit.Held(x)) return false;
                    bool a2 = owner.Stage == SfStage.Aim;
                    Vector3 at = Kit.FloorUnder(a2 ? owner.AimPoint : owner.Point, 1.5f) + Vector3.up * 0.024f;
                    float k = a2 ? 0f : Mathf.Clamp01(owner.StageTime / S.knightAir);
                    var t = x.tiles[0];
                    t.Floor(at, -(a2 ? owner.AimDir : owner.Dir), Vector2.one * Mathf.Lerp(1.3f, 0.75f, k) * Kit.Pop(x.age, 1.1f, 0.5f));
                    Kit.Horseshoe(t, Kit.Knight, a2 ? 0.6f : 0.95f);
                    t.fade = a2 ? 0.7f : 1f;
                    t.Apply();
                    return true;
                };
                return fx;
            });
            if (aim)
            {
                // The arc it will fly, a thin dotted line for its owner.
                kit.Keep(s, "knight arc", () =>
                {
                    var fx = new Kit.Fx();
                    fx.strips.Add(new Kit.Strip(kit, Kit.Knight) { core = Color.white, coreShare = 0.3f });
                    fx.step = (x, d) =>
                    {
                        if (owner == null || !kit.Held(x) || owner.Stage != SfStage.Aim) return false;
                        var pts = new List<Vector3>();
                        Vector3 a = owner.Origin + Vector3.up * 0.6f, b = owner.AimPoint + Vector3.up * 0.25f;
                        for (int i = 0; i <= 16; i++)
                        {
                            float u = i / 16f;
                            pts.Add(Vector3.Lerp(a, b, u) + Vector3.up * (4f * 1.4f * u * (1f - u)));
                        }
                        x.strips[0].Build(pts, j => 0.07f, kit.Eye);
                        x.strips[0].opacity = 0.75f;
                        x.strips[0].Apply();
                        return true;
                    };
                    return fx;
                });
            }
        }

        void KnightLeap(SfFxEvent e)
        {
            kit.FlashRing(e.at, 0.6f, Kit.Knight);
            kit.DustRing(e.at, 4, 0.7f);
            var pawn = e.by.Pawn;
            var owner = e.by;
            var strip = new Kit.Strip(kit, Kit.Knight) { core = Color.white, coreShare = 0.3f, inkShare = 0.22f };
            var pts = new List<Vector3>();
            var f = kit.Run(1.2f, (fx, d) =>
            {
                bool flying = owner != null && owner.Stage == SfStage.Active && pawn != null;
                if (flying) pts.Add(pawn.Hips.position);
                else if (pts.Count > 0) pts.RemoveAt(0);
                while (pts.Count > 14) pts.RemoveAt(0);
                if (pts.Count < 2) { strip.Hide(); return flying || fx.age < 0.1f; }
                strip.Build(pts, i => 0.32f * i / Mathf.Max(1f, pts.Count - 1f), kit.Eye);
                strip.Apply();
                return true;
            });
            f.strips.Add(strip);
        }

        void KnightLand(SfFxEvent e)
        {
            var s = e.by;
            kit.FlashRing(e.at, 0.8f, Kit.Knight);
            kit.DustRing(e.at, 5, 0.9f);
            foreach (var spot in s.KnightSpots)
            {
                var ring = new Kit.Tile(kit, "Knight impact");
                Vector3 c = spot + Vector3.up * 0.03f;
                float r1 = e.size;
                var f = kit.Run(14f * F, (fx, d) =>
                {
                    float a = fx.age / F;
                    float r = r1 * Kit.Pop(fx.age, 1.12f, 0.3f);
                    ring.Floor(c, s.Dir, Vector2.one * (2f * r));
                    ring.shape = 1f;
                    ring.inner = Mathf.Clamp01(1f - 0.28f / r);
                    ring.fill = Kit.A(Kit.Knight.main, 1f);
                    ring.core = Kit.A(Color.white, 1f);
                    ring.coreWidth = 0.04f;
                    ring.ink = Kit.A(Kit.Knight.ink, 1f);
                    ring.inkWidth = 0.035f;
                    ring.rim = Color.clear;
                    ring.stripe = Color.clear;
                    ring.flash = a < 2f ? 1f : 0f;
                    ring.arc = a < 6f ? 1f : 1f - (a - 6f) / 8f;
                    ring.fade = 1f;
                    ring.Apply();
                    return true;
                });
                f.tiles.Add(ring);
                kit.DustRing(spot, 4, 0.9f, null, 0.3f, 0.5f);
            }
            kit.AddSquash(s.Pawn, Kit.SquashKind.Down);
            HitStop(4);
            Shake(s.Pawn, null, 0.12f, 6);
        }

        // ---------------------------------------------------------------- events

        void OnFx(SfFxEvent e)
        {
            if (!effects || kit == null || e.by == null) return;
            switch (e.kind)
            {
                case SfFxKind.KingParry:
                    kit.ImpactRing(e.at, e.dir, Kit.King, 0.15f, 0.5f);
                    kit.FlashBody(e.by.Pawn, Kit.King.main);
                    KingWave(e.by);
                    HitStop(6);
                    Shake(e.by.Pawn, null, 0.16f, 8);
                    break;
                case SfFxKind.KingCounterHit: Hit(e, Kit.King, 0, 0.16f); break;
                case SfFxKind.KingWhiff:
                    kit.PuffBurst(e.by.Pawn.bodies[(int)BodyId.Chest].position + e.dir * 0.4f, Kit.Grey, 4, 0.4f, 0.16f, 0.4f);
                    break;
                case SfFxKind.QueenThrust: QueenThrust(e); break;
                case SfFxKind.QueenHit: Hit(e, Kit.Queen, e.order == 0 ? 5 : 0, e.order == 0 ? 0.16f : 0.1f); break;
                case SfFxKind.RookSlam:
                    kit.FlashRing(e.at, 0.9f, Kit.Rook);
                    kit.PuffBurst(e.at + Vector3.up * 0.2f, Kit.Dust, 5, 0.6f, 0.26f, 0.45f);
                    HitStop(3);
                    Shake(e.by.Pawn, null, 0.1f, 6);
                    break;
                case SfFxKind.RookHit: Hit(e, Kit.Rook, 4, 0.12f); break;
                case SfFxKind.RookBlocked:
                {
                    Vector3 at = Kit.FloorUnder(e.at, 1.5f) + Vector3.up * 0.5f;
                    kit.ImpactRing(at, -e.dir, Kit.Rook, 0.3f, 0.85f);
                    kit.Drops(at, -e.dir + Vector3.up * 0.5f, Kit.Stone, 4);
                    kit.DustRing(at - Vector3.up * 0.5f, 5, 0.8f);
                    HitStop(4);
                    break;
                }
                case SfFxKind.BishopFire: BishopFire(e); break;
                case SfFxKind.BishopSlow:
                    kit.FlashBody(e.target, Kit.Bishop.main);
                    kit.Drops(e.at, Vector3.up, Kit.Bishop, 3, 5f);
                    break;
                case SfFxKind.BishopPin:
                    kit.FlashRing(e.at, 0.8f, Kit.Queen);
                    Shake(e.by.Pawn, e.target, 0.08f, 5);
                    break;
                case SfFxKind.KnightLeap: KnightLeap(e); break;
                case SfFxKind.KnightLand: KnightLand(e); break;
                case SfFxKind.KnightHit: Hit(e, Kit.Knight, 0, 0.12f); break;
                case SfFxKind.Interrupted:
                    kit.PuffBurst(e.at, Kit.Grey, 4, 0.4f, 0.16f, 0.4f);
                    break;
            }
        }

        void KingWave(SwordFightSkills s)
        {
            var S = this.S;
            float R = S != null ? S.kingCounterRadius : 2.5f;
            Vector3 c = Kit.FloorUnder(s.Pawn.Hips.position, 0.6f) + Vector3.up * 0.03f;
            var ring = new Kit.Tile(kit, "King answer");
            var f = kit.Run(16f * F, (fx, d) =>
            {
                float a = fx.age / F;
                float r = Mathf.Lerp(0.4f, R, Kit.EaseOut(a / 8f));
                ring.Floor(c, Vector3.forward, Vector2.one * (2f * r));
                ring.shape = 1f;
                ring.inner = Mathf.Clamp01(1f - 0.32f / r);
                ring.fill = Kit.A(Kit.King.main, 1f);
                ring.core = Kit.A(Color.white, 1f);
                ring.coreWidth = 0.04f;
                ring.ink = Kit.A(Kit.King.ink, 1f);
                ring.inkWidth = 0.035f;
                ring.rim = Color.clear;
                ring.stripe = Color.clear;
                ring.flash = a < 2f ? 1f : 0f;
                ring.arc = a < 8f ? 1f : 1f - (a - 8f) / 8f;
                ring.fade = 1f;
                ring.Apply();
                return true;
            });
            f.tiles.Add(ring);
            kit.DustRing(c, 6, R * 0.7f);
            kit.AddSquash(s.Pawn, Kit.SquashKind.Spring);
        }

        /// <summary>A skill hit in design A: the body white then the hitter's colour, drops and a C half ring the way it
        /// flies, a hit stop and a shake, and the captured square if it goes down.</summary>
        void Hit(SfFxEvent e, Kit.Palette p, int stopFrames, float shakeMetres)
        {
            if (e.target == null) return;
            kit.FlashBody(e.target, p.main);
            kit.Drops(e.at, e.dir + Vector3.up * 0.4f, p, 4);
            kit.HalfRing(e.at, e.dir, p, 0.65f);
            kit.Down(e.target, p, e.at);
            HitStop(stopFrames);
            Shake(e.by.Pawn, e.target, shakeMetres, 6);
        }
    }
}
