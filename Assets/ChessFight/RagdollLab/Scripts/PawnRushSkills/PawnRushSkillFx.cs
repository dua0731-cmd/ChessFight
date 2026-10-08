using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Hit-feel effects for the Pawn Rush skills, in the skill test scene only (Docs/Skills/EFFECTS.md; picked
    /// 2026-10-07: queen A "two rings", rook A "bowling pins", bishop B "diagonal squares", knight B "head
    /// stomp"; the pawn is not picked yet and keeps its plain look).
    ///
    /// R79: 3D light effects in place of the flat pictures (승규 님: "3D, 입체적인 느낌"): walls of light that run
    /// out over the floor, beams and shells on meshes, sparks that bounce off the floor, motes and smoke as
    /// particles, point lights that light the floor and the pieces, trails, lightning, all drawn brighter than white
    /// for the camera's bloom (<see cref="PawnRushSkillBloom"/>). R80: the queen's spell look (magic circle, pillar,
    /// walls) became little gold chess pieces that gather and pop; the rook charges in the air and slams; the
    /// knight's second press kicks off with a "당". Parts: PawnRushSkillFx.Parts.cs.
    /// R86 (승규 님's three reference shorts, "이런 식의 형태로"): the queen is a cartoon explosion (fire that cools to
    /// dark smoke with glowing veins, cracks in the floor), the rook an electric dash (speed lines, lightning along
    /// its path), the knight a hand-drawn wind burst (inked puffs and swooshes); the bishop is unchanged ("지금
    /// 좋아"). Cartoon parts: PawnRushSkillFx.Toon.cs.
    /// No star shapes anywhere (R78, R79), no words or numbers (R74), no chips or blocks (R75).
    ///
    /// Shared parts: a hit stop (the whole game holds still for a few hundredths of a second, a test-bed stand-in
    /// for holding only the two pieces), camera shake and a white flash on the piece that is hit. It listens to
    /// RagdollPawn.SkillFx, which only the skills raise, and watches the pieces' skill stages (the queen's charge,
    /// the rook's dash, the knight's leap), so no other scene changes. No sound yet.
    /// </summary>
    [DefaultExecutionOrder(210)]   // after the lab camera (150) and the skeleton pose (100)
    public partial class PawnRushSkillFx : MonoBehaviour
    {
        public bool effects = true;
        [Tooltip("맞는 순간 게임 전체를 잠깐 멈춤 (시험용: 실제로는 때린 쪽·맞은 쪽만)")]
        public bool hitStop = true;
        public bool shake = true;

        /// <summary>The film's camera while it records; the lab camera otherwise.</summary>
        public static Camera ViewOverride;

        static readonly Color Ink = new Color(0.05f, 0.08f, 0.19f);
        // Each piece's light. Brightness above white is given where it is used (Hdr).
        static readonly Color Gold = new Color(1f, 0.7f, 0.2f);
        static readonly Color GoldDeep = new Color(1f, 0.42f, 0.06f);
        static readonly Color Fire = new Color(1f, 0.4f, 0.08f);
        static readonly Color Violet = new Color(0.6f, 0.3f, 1f);
        static readonly Color Magenta = new Color(1f, 0.32f, 0.85f);
        static readonly Color Sky = new Color(0.04f, 0.32f, 1f);       // deep enough to read on a white floor
        static readonly Color SkyCore = new Color(0.55f, 0.85f, 1f);
        static readonly Color Dust = new Color(0.72f, 0.66f, 0.58f, 1f);   // a little darker than the floors it rolls over

        static Color Hdr(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        LabGame game;
        Transform root;
        Font font;
        Material lineMat, whiteMat;
        Material matPillar, matRing, matHalo, matTile, matCurtain, matTrail, matBolt, matRod;
        Camera prepared;

        // hit stop and shake
        float stopLeft, stopResume = 1f, shakeAmp, shakeLeft, shakeTotal, shakeClock;
        bool stopping;

        readonly List<Anim> anims = new List<Anim>();
        readonly Dictionary<RagdollPawn, Flash> flashes = new Dictionary<RagdollPawn, Flash>();
        readonly List<Squash> squashes = new List<Squash>();
        readonly Dictionary<Object, Squares3D> tileSets = new Dictionary<Object, Squares3D>();
        readonly Dictionary<RagdollPawn, float> stompedAt = new Dictionary<RagdollPawn, float>();
        readonly Dictionary<RagdollPawn, Squares3D> ghosts = new Dictionary<RagdollPawn, Squares3D>();
        readonly Dictionary<RagdollPawn, Watch> watched = new Dictionary<RagdollPawn, Watch>();
        EdgeFlash edge;

        /// <summary>What a piece was doing last frame, to start its ongoing effects once.</summary>
        class Watch { public bool charging, dashing, leaping; }

        // Real time, frame by frame: a hit stop does not stop it, and a recording (Time.captureFramerate)
        // steps it one frame at a time like the video it makes.
        static float Dt => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
        float clock;
        float Clock => clock;

        public Camera ViewCamera => ViewOverride != null ? ViewOverride : game != null && game.labCamera != null ? game.labCamera.Cam : Camera.main;

        PawnRushSkillParams Params
        {
            get
            {
                var bed = GetComponent<PawnRushSkillBed>();
                return bed != null ? bed.skills : null;
            }
        }

        void Awake()
        {
            game = GetComponent<LabGame>();
            if (game == null) game = FindFirstObjectByType<LabGame>();
            root = new GameObject("Pawn Rush skill effects").transform;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 64);
            lineMat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            whiteMat = Unlit(Color.white);
            BuildParts();
            var white = Texture2D.whiteTexture;
            matPillar = Glow(texBeam, noise: 0.5f, noiseST: new Vector4(2f, 0.7f, 0f, -2.6f), rim: -0.8f, rimPower: 1.4f, soft: 0.2f);
            matRing = Glow(texRing, noise: 0.3f, noiseST: new Vector4(2f, 2f, 0.3f, 0.15f));
            matHalo = Glow(texSoft);
            matTile = Glow(texTile, opacity: 0.4f, noise: 0.25f, noiseST: new Vector4(1f, 1f, 0.1f, 0.35f));
            matCurtain = Glow(texBeam, noise: 0.55f, noiseST: new Vector4(2f, 1f, 0f, -1.1f), soft: 0.08f);
            matTrail = Glow(texBand, noise: 0.35f, noiseST: new Vector4(3f, 1f, -2.5f, 0f));
            matBolt = Glow(texBand);
            matRod = Glow(white, noise: 0.5f, noiseST: new Vector4(1f, 6f, 0f, -3f), rim: -0.7f, rimPower: 1.2f);
            BuildToon();
            RagdollPawn.SkillFx += OnSkillFx;
        }

        void OnDestroy()
        {
            RagdollPawn.SkillFx -= OnSkillFx;
            if (stopping) Time.timeScale = stopResume;
            foreach (var f in flashes.Values) f.Restore();
            foreach (var s in squashes) s.Restore();
            foreach (var a in anims) a.Destroy();
            foreach (var s in tileSets.Values) s.Destroy();
            foreach (var s in ghosts.Values) s.Destroy();
            if (root != null) Destroy(root.gameObject);
            DestroyParts();
        }

        // ---------------------------------------------------------------- the skills' moments

        void OnSkillFx(SkillFxEvent e)
        {
            if (!effects) return;
            switch (e.kind)
            {
                case SkillFxKind.QueenBlast: QueenBlast(e); break;
                case SkillFxKind.QueenHit: QueenHit(e); break;
                case SkillFxKind.RookHit: RookHit(e); break;
                case SkillFxKind.RookStop: RookStop(e); break;
                case SkillFxKind.RookWall: RookWall(e, 1f); break;
                case SkillFxKind.RookBarricade: RookWall(e, 0.7f); break;
                case SkillFxKind.BishopWire: BishopWire(e); break;
                case SkillFxKind.BishopTrip: BishopTrip(e); break;
                case SkillFxKind.KnightTurn: KnightTurn(e); break;
                case SkillFxKind.KnightHome: KnightHome(e); break;
                case SkillFxKind.KnightStomp: KnightStomp(e); break;
                case SkillFxKind.KnightLand: KnightLand(e); break;
                case SkillFxKind.KnightLeap: KnightTakeOff(e); break;
                case SkillFxKind.RookSlam: RookSlam(e); break;
            }
        }

        /// <summary>The ongoing effects follow the pieces' skill stages: started once when a stage begins, ended
        /// by themselves when it is over.</summary>
        void WatchPieces()
        {
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null) continue;
                if (!watched.TryGetValue(pawn, out var w)) watched[pawn] = w = new Watch();
                bool charging = effects && QueenCharging(pawn);
                bool dashing = effects && RookDashing(pawn);
                bool leaping = effects && KnightLeaping(pawn);
                if (charging && !w.charging) QueenCharge(pawn);
                if (dashing && !w.dashing) RookCharge(pawn);
                if (leaping && !w.leaping) KnightLeap(pawn);
                w.charging = charging;
                w.dashing = dashing;
                w.leaping = leaping;
            }
        }

        static bool QueenCharging(RagdollPawn p) => p != null && p.Piece == PieceKind.Queen && p.SkillStage == SkillStage.Windup;
        static bool RookDashing(RagdollPawn p) => p != null && p.Piece == PieceKind.Rook && p.SkillDashing;
        static bool KnightLeaping(RagdollPawn p) => p != null && p.Piece == PieceKind.Knight && p.SkillStage == SkillStage.Active;

        // How the light is mixed (R79, after the first film washed out): the test floor is white and the course's
        // cream squares nearly so, and light added to white is only white. So each effect has a coloured body that
        // covers the floor behind it like paint (light about 1.2, paint 0.6-0.9: the colour shows on any floor) and
        // thin hot cores far above white (light 3-6, little paint) that the bloom spreads into a coloured glow.
        // Point lights are kept low (1-2): a bright light on a white floor blooms the whole picture.

        // ---- Queen (R86, after the reference's stylized explosion; 승규 님 "바꿔줘": the gold chess pieces and the
        // embers are gone): while she charges, a ball of fire gathers at her chest, boiling and growing, thin lines of
        // light are sucked into it, and cracks begin to glow in the floor under her. The blast: a white-hot flash; a
        // fireball of lumpy puffs that bursts up and out and within a few tenths of a second cools to dark smoke laced
        // with glowing veins and hot edges, then is eaten away; a ring of smoke rolling out over the floor to her outer
        // radius, with the two thin gold rings that show 1.5 m and 3 m; the cracks run out, glow, cool to scorch lines
        // on a darkened floor and fade; sparks shoot up in arcs, embers drift. The stop, the shake and the red/cyan
        // edges stay. Her rings on the floor while she charges are the skill's telegraph (SkillMarks), not these.

        static readonly Color FireGlow = new Color(1f, 0.55f, 0.12f);

        /// <summary>The cracks under a charging queen, carried on into her blast.</summary>
        readonly Dictionary<RagdollPawn, Crack> queenCracks = new Dictionary<RagdollPawn, Crack>();

        void QueenCharge(RagdollPawn q)
        {
            float windup = Params != null ? Params.queenWindup : 0.35f;
            Vector3 floor = FloorUnder(q);
            Vector3 Heart() => q != null ? ChestOf(q) + Flat(q.transform.forward).normalized * 0.22f : floor + Vector3.up;
            // The ball of fire: small, white-hot and boiling, growing faster towards the end.
            Add(new Puff(root, "Queen gathering fire", meshSphere, matFire)
            {
                life = windup + 0.03f,
                lump = 0.42f,
                flow = 6f,
                heat = 1.15f,
                rim = Hdr(Gold, 2f),
                animate = (p, t) =>
                {
                    float k = Mathf.Clamp01(p.Age / windup);
                    p.at = Heart();
                    p.scale = Vector3.one * Mathf.Lerp(0.1f, 0.5f, k * k) * (1f + 0.1f * Mathf.Sin(p.Age * 75f));
                },
            });
            Add(new Shape(root, "Queen gathering glow", meshQuad, matHalo)
            {
                life = windup + 0.03f,
                billboard = true,
                color = Hdr(Gold, 1.4f),
                paint = 0.35f,
                animate = (s, t) =>
                {
                    float k = Mathf.Clamp01(s.Age / windup);
                    s.at = Heart();
                    s.scale = Vector3.one * (0.3f + 1.3f * k * k);
                    s.bright = 0.4f + 0.6f * k;
                },
            });
            // Thin lines of light sucked in from all round, arriving as she lets go.
            float due = 0f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!QueenCharging(q) || age > windup) return false;
                Vector3 heart = Heart();
                for (due += dt * 110f; due >= 1f; due -= 1f)
                {
                    var dir = Random.onUnitSphere;
                    dir.y *= 0.6f;
                    float arrive = Mathf.Clamp(windup - age, 0.05f, Random.Range(0.1f, 0.2f));
                    Vector3 from = heart + dir.normalized * Random.Range(1.1f, 1.9f);
                    streaks.Emit(from, (heart - from) / arrive, Random.Range(0.035f, 0.055f), arrive, Tint(Gold, Random.value * 0.5f));
                }
                return true;
            }));
            if (queenCracks.TryGetValue(q, out var old)) old.life = 0f;
            queenCracks[q] = QueenCracks(floor, windup);
        }

        /// <summary>The queen's cracks: creeping out dimly while she charges (fading if the charge is called off), then,
        /// once blasted, running right out hot, cooling to scorch lines and fading.</summary>
        Crack QueenCracks(Vector3 floor, float windup)
        {
            var crack = new Crack(root, meshQuad, matCrack) { at = floor, radius = 2.4f, life = 10f, paint = 0.3f };
            crack.animate = (c, t) =>
            {
                if (c.BlastAt < 0f)
                {
                    float k = Mathf.Clamp01(c.Age / windup);
                    c.reveal = 0.3f * k * k;
                    c.hot = Hdr(Gold, 1.2f + 0.8f * k);
                    if (c.Age > windup + 0.25f) c.fade = Mathf.Clamp01(1f - (c.Age - windup - 0.25f) / 0.2f);
                    if (c.fade <= 0f) c.life = c.Age;
                    return;
                }
                float b = c.Age - c.BlastAt;
                c.reveal = Mathf.Lerp(c.RevealAtBlast, 1.02f, EaseOut(b / 0.09f));
                c.hot = Hdr(Gold, Keys(b, 0f, 3.6f, 0.3f, 2.2f));
                c.cool = Keys(b, 0.25f, 0f, 0.9f, 1f);
                c.fade = Keys(b, 1.1f, 1f, 1.7f, 0f);
                if (b > 1.72f) c.life = c.Age;
            };
            anims.Add(crack);
            return crack;
        }

        void QueenBlast(SkillFxEvent e)
        {
            Vector3 c = Ground(e.at);
            float inner = Params != null ? Params.queenInner : 1.5f, outer = e.size > 0f ? e.size : 3f;
            Vector3 pop = c + Vector3.up * 0.9f;

            // The cracks under her run right out (from now, if her charge was not seen).
            if (e.by == null || !queenCracks.TryGetValue(e.by, out var crack) || crack.Gone) crack = QueenCracks(c, 0.01f);
            if (e.by != null) queenCracks.Remove(e.by);
            crack.Blast();
            Add(new Shape(root, "Queen scorch", meshQuad, matScorch)
            {
                life = 1.9f,
                at = c + Vector3.up * 0.015f,
                rotation = FlatOnFloor() * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)),
                color = new Color(0.06f, 0.035f, 0.02f),
                paint = 0.55f,
                animate = (s, t) =>
                {
                    s.scale = Vector3.one * 4.4f * Mathf.Lerp(0.5f, 1f, EaseOut(s.Age / 0.1f));
                    s.bright = Keys(s.Age, 0.9f, 1f, 1.9f, 0f);
                },
            });

            // The flash: a white-hot puff that bursts and is gone within a sixth of a second.
            Add(new Puff(root, "Queen flash", meshSphere, matFire)
            {
                life = 0.16f,
                at = pop,
                lump = 0.3f,
                flow = 3f,
                rim = Hdr(Gold, 3f),
                animate = (p, t) =>
                {
                    float a = p.Age;
                    p.scale = Vector3.one * Mathf.Lerp(0.5f, 2.1f, EaseOut(a / 0.06f));
                    p.heat = Keys(a, 0.04f, 1.2f, 0.12f, 0.6f);
                    p.dissolve = Keys(a, 0.06f, 0f, 0.16f, 1f);
                },
            });

            // The fireball: lumpy puffs bursting up and out, burning, cooling to dark smoke with glowing veins and hot
            // edges, rising, eaten away last.
            for (int i = 0; i < 12; i++)
            {
                Vector3 dir = i < 2 ? (Vector3.up + Random.insideUnitSphere * 0.3f).normalized : OnSphere(0.15f);
                float reach = Random.Range(0.45f, 1.05f), size = Random.Range(0.75f, 1.25f), rise = Random.Range(0.25f, 0.55f);
                Vector3 from = pop + dir * 0.15f, to = pop + dir * reach + Vector3.up * Random.Range(0f, 0.3f);
                Add(new Puff(root, "Queen fireball", meshSphere, matFire)
                {
                    life = Random.Range(1.05f, 1.4f),
                    lump = Random.Range(0.32f, 0.45f),
                    flow = 1.6f,
                    rotation = Random.rotation,
                    animate = (p, t) =>
                    {
                        float a = p.Age;
                        p.at = Vector3.Lerp(from, to, EaseOut(a / 0.32f)) + Vector3.up * rise * a;
                        p.scale = Vector3.one * size * Mathf.Lerp(0.3f, 1f, EaseOut(a / 0.25f)) * Mathf.Lerp(1f, 1.15f, t);
                        p.heat = Keys(a, 0.03f, 1.1f, 0.12f, 0.5f, 0.32f, 0.18f, 0.6f, 0f);
                        p.rim = Hdr(FireGlow, 2.4f * Sq(Mathf.Clamp01(1f - a / 0.75f)));
                        p.dissolve = Keys(t, 0.45f, 0f, 1f, 1f);
                    },
                });
            }

            // A ring of smoke rolling out over the floor to her outer radius, hot at its front at first.
            const int ring = 18;
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < ring; i++)
            {
                float a = (turn + (i + Random.Range(-0.3f, 0.3f)) * 360f / ring) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float reach = outer * Random.Range(0.86f, 0.98f), size = Random.Range(0.5f, 0.75f);
                Add(new Puff(root, "Queen smoke ring", meshSphere, matFire)
                {
                    life = Random.Range(0.75f, 0.95f),
                    lump = 0.4f,
                    flow = 2f,
                    rotation = Quaternion.LookRotation(radial),
                    animate = (p, t) =>
                    {
                        float k = EaseOut(p.Age / 0.3f);
                        p.at = c + radial * Mathf.Lerp(0.5f, reach, k) + Vector3.up * (0.12f + 0.2f * k);
                        p.scale = new Vector3(size, size * 0.7f, size * 1.2f) * Mathf.Lerp(0.4f, 1f, k);
                        p.heat = Keys(p.Age, 0.02f, 0.9f, 0.22f, 0f);
                        p.rim = Hdr(FireGlow, 2f * Mathf.Clamp01(1f - p.Age / 0.4f));
                        p.dissolve = Keys(t, 0.3f, 0f, 1f, 1f);
                    },
                });
            }
            GroundRing(c, 0.2f, inner, 0.15f, Gold, 0.45f, 2.4f);
            GroundRing(c, 0.3f, outer, 0.3f, Gold, 0.6f, 2.2f);

            for (int i = 0; i < 30; i++)
            {
                var dir = InCone(Vector3.up, 70f);
                sparks.Emit(pop + dir * 0.3f, dir * Random.Range(6f, 12f), Random.Range(0.05f, 0.075f), Random.Range(0.5f, 0.9f), Tint(Gold, Random.value * 0.5f));
            }
            MoteBurst(pop, 26, GoldDeep, 0.8f, 0.6f, 1.6f, 1.4f, 0.08f);
            Flare(pop, Gold, 1.6f, outer * 2.4f, 0.45f);
            HitStop(0.06f);
            Shake(0.14f, 0.28f);
            Edge(0.22f);
        }

        void QueenHit(SkillFxEvent e)
        {
            FlashWhite(e.target, 0.07f);
            Vector3 at = e.target != null ? ChestOf(e.target) : e.at + Vector3.up * 0.3f;
            Vector3 away = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.up;
            SparkBurst(at, 14, Gold, away + Vector3.up * 0.4f, 65f, 3f, 7f, 0.45f, 0.05f);
            // A small burst of fire on the piece, cooling to a puff of smoke.
            Add(new Puff(root, "Queen hit puff", meshSphere, matFire)
            {
                life = 0.45f,
                lump = 0.35f,
                flow = 2f,
                animate = (p, t) =>
                {
                    p.at = at + away * 0.3f * EaseOut(t);
                    p.scale = Vector3.one * Mathf.Lerp(0.25f, 0.6f, EaseOut(p.Age / 0.12f));
                    p.heat = Keys(p.Age, 0.02f, 1.1f, 0.15f, 0f);
                    p.rim = Hdr(FireGlow, 2f * (1f - t));
                    p.dissolve = Keys(t, 0.35f, 0f, 1f, 1f);
                },
            });
        }

        // ---- Rook (R86, after the reference's electric dash; it was a shield and a tail of fire): while it charges, a
        // bright spindle of light rides ahead of it, a white-hot core and an orange body trail behind, speed lines
        // stream back off it and lightning crawls along its path, crackling. Each piece it sends flying: a flash,
        // lightning bursting out, speed lines and sparks thrown along its line and a ring rushing out, each bigger and
        // with a longer stop (0.04, 0.06, 0.08 s) and shake than the one before; the fourth, which stops it, adds
        // lightning running out over the floor. Into a wall: lightning spreading over the wall's face.

        /// <summary>The white-orange of the rook's lightning.</summary>
        static readonly Color Volt = new Color(1f, 0.86f, 0.55f);

        void RookCharge(RagdollPawn r)
        {
            Vector3? Where() => RookDashing(r) ? r.Hips.position : (Vector3?)null;
            Vector3? Chest() => RookDashing(r) ? ChestOf(r) : (Vector3?)null;
            anims.Add(new Streak(root, matTrail, Hdr(Fire, 1.25f), 0.75f, 0.2f, Where) { paint = 0.8f });
            anims.Add(new Streak(root, matTrail, Hdr(Volt, 3.4f), 0.16f, 0.32f, Where) { paint = 0.25f });
            anims.Add(new PathBolts(root, matBolt, Chest, 1.6f, Hdr(Volt, 3.2f), Hdr(Fire, 1.6f)));
            Vector3 heading = Flat(r.transform.forward).sqrMagnitude > 0.01f ? Flat(r.transform.forward).normalized : Vector3.forward;
            float endedAt = -1f;
            // The spindle of light ahead of it.
            Add(new Beam(root, matFlare)
            {
                life = 5f,
                color = Hdr(Volt, 2.4f),
                paint = 0.35f,
                animate = (b, t) =>
                {
                    float age = b.Age;
                    if (endedAt < 0f && !RookDashing(r)) endedAt = age;
                    if (endedAt >= 0f && age - endedAt > 0.1f) { b.life = age; return; }
                    if (r != null)
                    {
                        Vector3 v = r.Hips.linearVelocity;   // up or down too: the charge in the air (R80)
                        if (v.sqrMagnitude > 1f) heading = v.normalized;
                        Vector3 chest = ChestOf(r);
                        b.head = chest + heading * 0.75f;
                        b.tail = chest - heading * 0.15f;
                    }
                    b.width = 0.6f * Mathf.Min(1f, age / 0.05f) * (0.85f + 0.15f * Mathf.Sin(age * 90f));
                    b.bright = endedAt < 0f ? 1f : 1f - (age - endedAt) / 0.1f;
                },
            });
            float line = 0f, zap = 0f, kick = 0f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!RookDashing(r)) return false;
                Vector3 chest = ChestOf(r);
                Vector3 side = Vector3.Cross(heading, Vector3.up);
                side = side.sqrMagnitude > 1e-4f ? side.normalized : Vector3.right;
                Vector3 over = Vector3.Cross(side, heading);
                // Speed lines streaming back off it: thin white-hot ones and wider orange ones (they show on white).
                for (line += dt * 80f; line >= 1f; line -= 1f)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f), reach = Random.Range(0.25f, 0.65f);
                    Vector3 at = chest + (side * Mathf.Cos(a) + over * Mathf.Sin(a)) * reach + heading * Random.Range(-0.2f, 0.5f);
                    bool hot = Random.value < 0.6f;
                    SpeedLine(at, -heading, Random.Range(0.8f, 1.9f), Random.Range(1f, 3f), hot ? Random.Range(0.02f, 0.04f) : Random.Range(0.04f, 0.07f),
                        hot ? Hdr(Volt, 3f) : Hdr(Fire, 1.4f), hot ? 0.25f : 0.85f, Random.Range(0.12f, 0.2f));
                }
                // Crackling sparks off its body.
                for (zap += dt * 70f; zap >= 1f; zap -= 1f)
                    sparks.Emit(chest + Random.insideUnitSphere * 0.4f, Random.onUnitSphere * Random.Range(2f, 5f) - heading * 2f, Random.Range(0.03f, 0.05f),
                        Random.Range(0.1f, 0.2f), Tint(Volt, Random.value * 0.4f));
                if (r.Grounded)
                    for (kick += dt * 25f; kick >= 1f; kick -= 1f)
                        smoke.Emit(Ground(r.Hips.position) - heading * 0.3f + Random.insideUnitSphere * 0.2f, -heading * Random.Range(0.5f, 1.5f) + Vector3.up * 0.4f,
                            Random.Range(0.35f, 0.55f), Random.Range(0.5f, 0.8f), (Color32)Dust, Random.Range(-60f, 60f));
                return true;
            }));
        }

        /// <summary>The flash where the rook's lightning strikes: a white-hot point in an orange glow.</summary>
        void Zap(Vector3 at, float size, float life)
        {
            Halo(at, size * 1.6f, Fire, life * 1.5f);
            Add(new Shape(root, "Zap", meshQuad, matHalo)
            {
                life = life,
                at = at,
                billboard = true,
                color = Hdr(Volt, 3.2f),
                paint = 0.3f,
                animate = (s, t) =>
                {
                    s.scale = Vector3.one * size * Mathf.Lerp(1f, 0.4f, t);
                    s.bright = 1f - t;
                },
            });
        }

        void RookHit(SkillFxEvent e)
        {
            int i = Mathf.Clamp(e.count, 1, 3);
            HitStop(new[] { 0.04f, 0.06f, 0.08f }[i - 1]);
            Shake(new[] { 0.05f, 0.08f, 0.11f }[i - 1], 0.16f + 0.03f * i);
            FlashWhite(e.target, 0.06f);
            Vector3 d = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Zap(e.at, 0.9f + 0.3f * i, 0.12f);
            anims.Add(new Bolts(root, matBolt, 4 + 2 * i, () => e.at, Hdr(Volt, 3.2f), 0.8f + 0.3f * i, 0.16f + 0.03f * i));
            LineBurst(e.at, d, 50f, 6 + 3 * i, 0.5f + 0.15f * i, 1.2f + 0.3f * i, Hdr(Volt, 3f), Hdr(Fire, 1.4f));
            SonicRing(e.at, d, 0.2f, 1f + 0.35f * i, Fire, 0.26f);
            SparkBurst(e.at, 18 + 8 * i, Volt, d + Vector3.up * 0.3f, 55f, 4f, 9f + 2f * i, 0.5f, 0.06f);
            Flare(e.at, Fire, 0.8f + 0.4f * i, 6f, 0.3f);
        }

        void RookStop(SkillFxEvent e)
        {
            HitStop(0.09f);
            Shake(0.17f, 0.32f);
            FlashWhite(e.target, 0.07f);
            Vector3 d = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Vector3 g = Ground(e.at);
            Zap(e.at, 1.8f, 0.16f);
            anims.Add(new Bolts(root, matBolt, 10, () => e.at, Hdr(Volt, 3.2f), 1.6f, 0.3f));
            anims.Add(new Bolts(root, matBolt, 7, () => g + Vector3.up * 0.05f, Hdr(Volt, 3f), 1.8f, 0.35f) { plane = Vector3.up });
            LineBurst(e.at, d, 60f, 16, 0.8f, 1.9f, Hdr(Volt, 3f), Hdr(Fire, 1.4f), 0.2f);
            SonicRing(e.at, d, 0.3f, 2.2f, Fire, 0.3f);
            SparkBurst(e.at, 40, Volt, d + Vector3.up * 0.4f, 70f, 4f, 11f, 0.6f, 0.07f);
            GroundRing(g, 0.3f, 1.3f, 0.2f, Fire, 0.5f, 2.6f);
            DustRing(g, 12, 0.4f, 3f, Dust, 0.6f, 0.8f);
            Flare(e.at, Fire, 1.8f, 7f, 0.4f);
        }

        /// <summary>A charge down out of the air into the floor (R80): lightning running out over the floor, speed
        /// lines thrown up, a ring, sparks, dust, a flash and a big shake.</summary>
        void RookSlam(SkillFxEvent e)
        {
            HitStop(0.07f);
            Shake(0.22f, 0.35f);
            Vector3 g = Ground(e.at + Vector3.up * 0.3f);
            Zap(g + Vector3.up * 0.4f, 2f, 0.16f);
            anims.Add(new Bolts(root, matBolt, 9, () => g + Vector3.up * 0.05f, Hdr(Volt, 3.2f), 2.1f, 0.38f) { plane = Vector3.up });
            LineBurst(g + Vector3.up * 0.1f, Vector3.up, 55f, 16, 0.8f, 2f, Hdr(Volt, 3f), Hdr(Fire, 1.4f), 0.22f);
            GroundRing(g, 0.2f, 1.8f, 0.2f, Fire, 0.55f, 2.6f);
            SparkBurst(g + Vector3.up * 0.2f, 40, Volt, Vector3.up, 75f, 4f, 10f, 0.6f, 0.07f);
            DustRing(g, 16, 0.4f, 4f, Dust, 0.75f, 0.9f);
            Flare(g + Vector3.up * 0.6f, Fire, 2f, 7f, 0.4f);
        }

        /// <summary>Into a wall (or, smaller, a barricade): the hardest thud of the rook's (R75: a clear shake), lightning
        /// spreading over the wall's face.</summary>
        void RookWall(SkillFxEvent e, float k)
        {
            HitStop(0.08f * k);
            Shake(0.25f * k, 0.4f);
            Vector3 d = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Vector3 at = e.at - d * 0.1f + Vector3.up * (k < 1f ? 0.5f : 0f);
            Zap(at, 2f * k, 0.16f);
            anims.Add(new Bolts(root, matBolt, Mathf.RoundToInt(9 * k), () => at, Hdr(Volt, 3.2f), 1.8f * k, 0.35f) { plane = d });
            LineBurst(at, -d, 65f, Mathf.RoundToInt(14 * k), 0.6f, 1.6f * k, Hdr(Volt, 3f), Hdr(Fire, 1.4f), 0.2f);
            SonicRing(at, -d, 0.3f, 2.4f * k, Fire, 0.3f);
            SparkBurst(at, Mathf.RoundToInt(45 * k), Volt, -d + Vector3.up * 0.25f, 80f, 4f, 11f, 0.6f, 0.07f);
            DustRing(Ground(at), Mathf.RoundToInt(12 * k), 0.3f, 2.5f, Dust, 0.7f, 0.8f);
            Flare(at - d * 0.4f, Fire, 2f * k, 8f, 0.4f);
        }

        // ---- Bishop B: the board squares under the X turn into boxes of light, lit from the middle out and pulsing
        // while the wire lasts, the two lines into beams of light; the square an enemy trips on throws up a pillar.

        void BishopWire(SkillFxEvent e)
        {
            if (e.source == null) return;
            var p = Params;
            float height = p != null ? p.bishopHeight : 0.3f;
            var set = new Squares3D(this, Ground(e.at), e.dir, e.size, false) { source = e.source };
            set.AddWires(e.at, e.dir, e.size, height);
            if (tileSets.TryGetValue(e.source, out var old)) old.Destroy();
            tileSets[e.source] = set;
            MoteBurst(Ground(e.at), 24, Violet, e.size * 0.3f, 1.4f, 0.6f, 1f);
            Flare(Ground(e.at) + Vector3.up * 0.6f, Violet, 1.2f, 5f, 0.4f);
        }

        /// <summary>The board squares the X's two lines cross: the middle one and two out along each arm.</summary>
        static IEnumerable<(Vector3 at, int ring, float size, Quaternion rotation)> Squares(Vector3 center, Vector3 forward, float lineLength)
        {
            Vector3 f = forward;
            f.y = 0f;
            f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            float half = (lineLength > 0f ? lineLength : 4.2f) * 0.5f;
            float size = half / (2f * Mathf.Sqrt(2f));   // the X's arms run corner to corner across two squares
            var rotation = Quaternion.LookRotation(f, Vector3.up);
            for (int i = -2; i <= 2; i++)
                foreach (int j in i == 0 ? new[] { 0 } : new[] { i, -i })
                    yield return (center + (r * j + f * i) * size + Vector3.up * 0.02f, Mathf.Abs(i), size, rotation);
        }

        /// <summary>While a bishop aims, its squares show faintly where the X would go (B's preview).</summary>
        void GhostSquares(float dt, Camera cam)
        {
            float length = Params != null ? Params.bishopLineLength : 4.2f;
            var gone = new List<RagdollPawn>();
            foreach (var kv in ghosts) if (kv.Key == null) { kv.Value.Destroy(); gone.Add(kv.Key); }
            foreach (var p in gone) ghosts.Remove(p);
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null) continue;
                bool show = effects && pawn.BishopAiming && pawn.BishopAimValid;
                if (!ghosts.TryGetValue(pawn, out var ghost))
                {
                    if (!show) continue;
                    ghosts[pawn] = ghost = new Squares3D(this, pawn.BishopAimPoint, pawn.BishopAimYaw, length, true);
                }
                ghost.hidden = !show;
                if (show) ghost.Place(pawn.BishopAimPoint, pawn.BishopAimYaw, length);
                ghost.Step(dt, cam, false);
            }
        }

        void BishopTrip(SkillFxEvent e)
        {
            HitStop(0.05f);
            Shake(0.06f, 0.16f);
            FlashWhite(e.target, 0.06f);
            Vector3 at = Ground(e.at);
            float square = 0.74f;
            if (e.source != null && tileSets.TryGetValue(e.source, out var set))
            {
                at = set.Pop(e.at);
                square = set.square;
            }
            // The pull is the moment now (R81, R82): a small pillar and flash, so the stretched line and its twang show.
            Pillar(at, square * 0.2f, 1f, Magenta, 0.3f);
            SparkBurst(at + Vector3.up * 0.2f, 18, Magenta, Vector3.up, 50f, 2f, 6f, 0.5f, 0.05f);
            MoteBurst(at, 20, Violet, square * 0.4f, 2.2f, 0.6f, 1f);
            Halo(at + Vector3.up * 0.4f, 0.75f, Violet, 0.18f);
            // No shell on the piece and a dim light: up close they washed the moment of the pull out white.
            Flare(at + Vector3.up * 0.6f, Violet, 0.45f, 3.5f, 0.25f);
        }

        // ---- Knight (R86, after the reference's hand-drawn wind burst; it was trails, rings and walls of blue light):
        // wind in cartoon puffs and swooshes, white, sky and deep blue in hard bands, inked in navy, bursting out and
        // eaten away (the reference's "afterimage"). The take-off throws puffs out round the feet with a swoosh on the
        // floor and a swirl rising round the knight; in the air small puffs drop behind it; the second press ("당")
        // kicks puffs back from under its feet with a ring of wind it bursts through; the stomp throws a crown of puffs
        // and two swooshes out round the head; a landing bursts puffs out over the floor to 1.5 m with swooshes
        // sweeping round. A dazed piece has three little puffs and a swoosh circling over its head (no stars).

        void KnightLeap(RagdollPawn k)
        {
            Vector3? Where() => KnightLeaping(k) ? k.Hips.position : (Vector3?)null;
            anims.Add(new Streak(root, matTrail, Hdr(Sky, 1.2f), 0.55f, 0.3f, Where) { paint = 0.85f });
            anims.Add(new Streak(root, matTrail, Hdr(SkyCore, 2.6f), 0.18f, 0.22f, Where) { paint = 0.4f });
            float due = 0f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!KnightLeaping(k)) return false;
                Vector3 v = k.Hips.linearVelocity;
                for (due += dt * 22f; due >= 1f; due -= 1f)
                {
                    Vector3 at = k.Hips.position - v * 0.04f + Random.insideUnitSphere * 0.2f;
                    float s = Random.Range(0.14f, 0.24f);
                    Add(new Puff(root, "Wind trail puff", meshSphere, matWind)
                    {
                        life = 0.32f,
                        at = at,
                        lump = 0.4f,
                        flow = 2f,
                        rotation = Random.rotation,
                        animate = (p, t) =>
                        {
                            p.scale = Vector3.one * s * Mathf.Lerp(0.5f, 1f, EaseOut(t * 3f));
                            p.dissolve = Keys(t, 0.2f, 0f, 1f, 1f);
                        },
                    });
                }
                return true;
            }));
        }

        /// <summary>"다": the leap's take-off, puffs out round the feet, a swoosh on the floor and a swirl rising round
        /// the knight; off the air (R81) a swoosh and puffs kicked away under its feet instead.</summary>
        void KnightTakeOff(SkillFxEvent e)
        {
            Shake(0.04f, 0.12f);
            if (e.count == 1)
            {
                WindArc(e.at, Vector3.up, 0.25f, 0.95f, 0.26f, 300f, 0.4f);
                WindPuffs(e.at, Vector3.down, 6, 0.15f, 0.6f, 0.25f, 0.3f, 0.4f);
                return;
            }
            var k = e.by;
            Vector3 g = Ground(e.at + Vector3.up * 0.3f);
            WindPuffs(g, Vector3.up, 9, 0.25f, 0.95f, 0.15f, 0.42f, 0.5f, 0.75f);
            WindArc(g + Vector3.up * 0.05f, Vector3.up, 0.35f, 1.05f, 0.28f, 300f, 0.42f);
            WindArc(g, Vector3.up, 0.6f, 0.75f, 0.22f, 420f, 0.38f, 0f, 0.85f, 1.3f, () => k != null ? k.Hips.position - Vector3.up * 0.7f : g);
        }

        /// <summary>"당" (R80): the second press kicks off the air, turned or straight on: a short stop and a shake,
        /// puffs kicked back from under its feet, a swoosh under them and a ring of wind it bursts through.</summary>
        void KnightTurn(SkillFxEvent e)
        {
            HitStop(0.045f);
            Shake(0.1f, 0.2f);
            Vector3 d = e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : Vector3.forward;
            Vector3 feet = e.at - Vector3.up * 0.35f;
            WindPuffs(feet, -d + Vector3.down * 0.5f, 8, 0.1f, 0.5f, 0.6f, 0.36f, 0.45f, 0.85f);
            WindArc(feet, Vector3.up, 0.25f, 1.1f, 0.3f, 320f, 0.4f);
            WindArc(e.at + d * 0.3f, d, 0.35f, 0.95f, 0.24f, 330f, 0.35f);
            SparkBurst(e.at, 10, SkyCore, -d, 60f, 2f, 5f, 0.3f, 0.04f);
        }

        void KnightHome(SkillFxEvent e)
        {
            var k = e.by;
            if (k == null) return;
            anims.Add(new Streak(root, matTrail, Hdr(SkyCore, 2.6f), 0.28f, 0.2f, () => KnightLeaping(k) ? k.Hips.position : (Vector3?)null) { paint = 0.4f });
            Vector3 line = e.at - ChestOf(k);
            WindArc(ChestOf(k), line, 0.45f, 0.7f, 0.2f, 380f, 0.3f, 0f, 0f, 0f, () => k != null ? ChestOf(k) : e.at);
        }

        void KnightStomp(SkillFxEvent e)
        {
            HitStop(0.09f);
            Shake(0.1f, 0.2f);
            FlashWhite(e.target, 0.07f);
            StartSquash(e.target);
            Vector3 at = e.at;
            // A crown of wind bursting out round the head, two swooshes sweeping round it.
            WindPuffs(at, Vector3.up, 10, 0.15f, 0.9f, 0.25f, 0.4f, 0.55f, 0.8f);
            WindArc(at, Vector3.up, 0.35f, 1.2f, 0.3f, 320f, 0.45f);
            WindArc(at - Vector3.up * 0.15f, Vector3.up, 0.3f, 0.95f, 0.22f, 300f, 0.42f, 0.04f);
            SparkBurst(at, 14, SkyCore, Vector3.up, 70f, 2f, 5f, 0.35f, 0.045f);
            if (e.target != null)
            {
                Vector3 g = Ground(e.target.Hips.position);
                WindPuffs(g, Vector3.up, 8, 0.3f, 1.1f, 0.1f, 0.38f, 0.5f, 0.7f);
                WindArc(g + Vector3.up * 0.05f, Vector3.up, 0.4f, 1.25f, 0.26f, 300f, 0.45f, 0.03f);
            }
            Flare(at, Sky, 1f, 5f, 0.3f);
            if (e.target != null) Daze(e.target, 0.45f, 1.8f);
            if (e.by != null) stompedAt[e.by] = Clock;
        }

        void KnightLand(SkillFxEvent e)
        {
            Vector3 g = Ground(e.at);
            if (e.by != null && stompedAt.TryGetValue(e.by, out float t) && Clock - t < 2.5f)
            {
                stompedAt.Remove(e.by);
                return;
            }
            float radius = e.size > 0f ? e.size : 1.5f;
            // The burst: puffs out over the floor to the landing's reach and a few up round the feet, swooshes
            // sweeping round, all eaten away.
            WindPuffs(g, Vector3.up, 14, 0.3f, radius * 0.95f, 0.2f, 0.5f, 0.7f, 0.7f);
            WindPuffs(g + Vector3.up * 0.1f, Vector3.up, 5, 0.1f, 0.4f, 0.8f, 0.5f, 0.65f, 1f);
            WindArc(g + Vector3.up * 0.05f, Vector3.up, 0.35f, radius * 1.05f, 0.34f, 300f, 0.5f);
            WindArc(g + Vector3.up * 0.08f, Vector3.up, 0.3f, radius * 0.8f, 0.26f, 280f, 0.46f, 0.05f);
            WindArc(g + Vector3.up * 0.5f, (Vector3.up + Flat(Random.onUnitSphere) * 0.45f).normalized, 0.45f, 0.9f, 0.22f, 260f, 0.4f, 0.03f);
            Flare(g + Vector3.up * 0.5f, Sky, 0.7f, 4f, 0.3f);
        }

        /// <summary>Three little puffs of wind circling over a dazed piece's head with a swoosh going round with them
        /// (R86; orbs of light before, R79, and stars before that).</summary>
        void Daze(RagdollPawn pawn, float delay, float life)
        {
            for (int i = 0; i < 3; i++)
            {
                float phase = i * Mathf.PI * 2f / 3f;
                Add(new Puff(root, "Daze puff", meshSphere, matWind)
                {
                    life = delay + life,
                    lump = 0.3f,
                    flow = 2f,
                    animate = (p, t) =>
                    {
                        float age = p.Age;
                        if (pawn == null || age < delay) { p.scale = Vector3.zero; return; }
                        float a = age * 6f + phase;
                        p.at = HeadOf(pawn) + Vector3.up * 0.4f + new Vector3(Mathf.Cos(a) * 0.32f, Mathf.Sin(a * 2f) * 0.04f, Mathf.Sin(a) * 0.32f);
                        p.scale = Vector3.one * 0.15f * Mathf.Clamp01((age - delay) / 0.12f);
                        p.dissolve = 1f - Mathf.Clamp01((delay + life - age) / 0.3f);
                    },
                });
            }
            Add(new Swoosh(root, matSwoosh)
            {
                life = delay + life,
                sweep = 280f,
                width = 0.08f,
                radius = 0.32f,
                center = () => pawn != null ? HeadOf(pawn) + Vector3.up * 0.4f : Vector3.zero,
                animate = (s, t) =>
                {
                    float age = s.Age;
                    s.fade = pawn == null || age < delay ? 0f : Mathf.Clamp01((age - delay) / 0.15f) * Mathf.Clamp01((delay + life - age) / 0.3f);
                    s.startAngle = -age * 6f * Mathf.Rad2Deg;
                },
            });
        }

        // ---------------------------------------------------------------- effect pieces

        static Quaternion FlatOnFloor() => Quaternion.LookRotation(Vector3.down, Vector3.forward);

        static Vector3 ChestOf(RagdollPawn p) => p != null ? p.bodies[(int)BodyId.Chest].position : Vector3.zero;

        static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        /// <summary>A beam of light standing up from <paramref name="c"/>, narrowing as it fades: a coloured sheath and
        /// a white-hot core inside it.</summary>
        void Pillar(Vector3 c, float width, float height, Color color, float life)
        {
            foreach (var (k, light, paint) in new[] { (1f, 1.2f, 0.75f), (0.3f, 3.2f, 0.25f) })
                Add(new Shape(root, "Pillar of light", meshCylinder, matPillar)
                {
                    life = life,
                    at = c,
                    color = Hdr(color, light),
                    paint = paint,
                    animate = (s, t) =>
                    {
                        float w = Mathf.Lerp(width, width * 0.1f, t * t) * k;
                        s.scale = new Vector3(w, height * EaseOut(t * 5f), w);
                        s.bright = 1f - t * t;
                        s.dissolve = t * 0.5f;
                    },
                });
        }

        /// <summary>A ring of light flat on the floor growing out to <paramref name="to"/> metres: a hot line
        /// (<paramref name="light"/>) the bloom spreads.</summary>
        void GroundRing(Vector3 c, float from, float to, float grow, Color color, float life, float light) =>
            FlatRing(c + Vector3.up * 0.035f, from, to, grow, color, life, light);

        /// <summary>A level ring of light at <paramref name="at"/> (on the floor, or in the air under a kick).</summary>
        void FlatRing(Vector3 at, float from, float to, float grow, Color color, float life, float light)
        {
            var flat = FlatOnFloor();
            Add(new Shape(root, "Ring", meshQuad, matRing)
            {
                life = life,
                at = at,
                rotation = flat,
                color = Hdr(color, light),
                paint = 0.45f,
                animate = (s, t) =>
                {
                    float age = s.Age, r = Mathf.Lerp(from, to, EaseOut(age / grow));
                    s.scale = Vector3.one * r * 2f / 0.86f;   // the ring texture's line is at 86 % of its radius
                    s.bright = age < grow ? 1f : 1f - (age - grow) / Mathf.Max(0.01f, life - grow);
                },
            });
        }

        /// <summary>A ring of light standing across <paramref name="facing"/>, rushing outwards (a shock along a line).</summary>
        void SonicRing(Vector3 at, Vector3 facing, float from, float to, Color color, float life)
        {
            var rotation = Quaternion.LookRotation(facing);
            Add(new Shape(root, "Shock ring", meshQuad, matRing)
            {
                life = life,
                at = at,
                rotation = rotation,
                color = Hdr(color, 2.6f),
                paint = 0.5f,
                animate = (s, t) =>
                {
                    s.at = at + facing * 0.6f * EaseOut(t);
                    s.scale = Vector3.one * Mathf.Lerp(from, to, EaseOut(t)) * 2f / 0.86f;
                    s.bright = (1f - t) * (1f - t);
                },
            });
        }

        /// <summary>A soft glow facing the camera around a flash: it tints rather than whitens.</summary>
        void Halo(Vector3 at, float size, Color color, float life)
        {
            Add(new Shape(root, "Halo", meshQuad, matHalo)
            {
                life = life,
                at = at,
                billboard = true,
                color = Hdr(color, 1.2f),
                paint = 0.45f,
                animate = (s, t) =>
                {
                    s.scale = Vector3.one * size * (0.7f + 0.3f * EaseOut(t * 3f));
                    s.bright = 1f - t;
                },
            });
        }

        /// <summary>
        /// A bishop's X of squares in light: each square a lit tile with a box of light standing on it, the middle
        /// first; the wire's two lines as beams. While aiming (<see cref="ghost"/>) the same squares, faint, follow the
        /// mouse. Fades out in 0.35 s once its wire is gone.
        /// </summary>
        class Squares3D
        {
            readonly PawnRushSkillFx fx;
            readonly bool ghost;
            readonly List<(Shape tile, Shape box, int ring, Vector3 at)> squares = new List<(Shape, Shape, int, Vector3)>();
            readonly List<Shape> wires = new List<Shape>();
            public Object source;
            public float square;
            public bool hidden;
            float age, fadeLeft = -1f, rise;
            readonly Dictionary<int, float> popped = new Dictionary<int, float>();

            public Squares3D(PawnRushSkillFx fx, Vector3 center, Vector3 forward, float length, bool ghost)
            {
                this.fx = fx;
                this.ghost = ghost;
                foreach (var (at, ring, _, _) in Squares(center, forward, length))
                {
                    var tile = new Shape(fx.root, "Bishop square", fx.meshQuad, fx.matTile) { life = float.MaxValue, paint = ghost ? 0.5f : 0.9f };
                    var box = ghost ? null : new Shape(fx.root, "Bishop light box", fx.meshBox, fx.matCurtain) { life = float.MaxValue, paint = 0.55f };
                    squares.Add((tile, box, ring, at));
                }
                Place(center, forward, length);
            }

            public void Place(Vector3 center, Vector3 forward, float length)
            {
                int i = 0;
                foreach (var (at, ring, size, rotation) in Squares(center, forward, length))
                {
                    square = size;
                    var s = squares[i];
                    squares[i++] = (s.tile, s.box, ring, at);
                    s.tile.at = at + Vector3.up * 0.01f;
                    s.tile.rotation = rotation * Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    s.tile.scale = Vector3.one * size * 0.92f;
                    if (s.box != null)
                    {
                        s.box.at = at;
                        s.box.rotation = rotation;
                    }
                }
            }

            /// <summary>The two lines of the wire as thin beams of light, each in two halves that bend where the wire is
            /// pulled (R81); placed from the wire every frame.</summary>
            public void AddWires(Vector3 center, Vector3 forward, float length, float height)
            {
                for (int i = 0; i < 4; i++)
                    wires.Add(new Shape(fx.root, "Bishop wire light", fx.meshCylinder, fx.matRod) { life = float.MaxValue, paint = 0.3f });
            }

            static void Rod(Shape rod, Vector3 from, Vector3 to)
            {
                Vector3 d = to - from;
                rod.at = from;
                rod.rotation = Quaternion.FromToRotation(Vector3.up, d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up);
                rod.scale = new Vector3(0.03f, d.magnitude, 0.03f);
            }

            /// <summary>The square nearest <paramref name="at"/> jumps and flares; returns its middle.</summary>
            public Vector3 Pop(Vector3 at)
            {
                int best = 0;
                float bestD = float.MaxValue;
                for (int i = 0; i < squares.Count; i++)
                {
                    float d = Vector3.Distance(Flat(squares[i].at), Flat(at));
                    if (d < bestD) { bestD = d; best = i; }
                }
                popped[best] = age;
                return squares[best].at;
            }

            public bool Step(float dt, Camera cam, bool sourceGone)
            {
                age += dt;
                if (sourceGone && fadeLeft < 0f) fadeLeft = 0.35f;
                if (fadeLeft >= 0f)
                {
                    fadeLeft -= dt;
                    if (fadeLeft <= 0f) return false;
                }
                float fade = fadeLeft >= 0f ? fadeLeft / 0.35f : 1f;
                bool armed = source is SkillTripwire w && w != null && w.Armed;
                for (int i = 0; i < squares.Count; i++)
                {
                    var (tile, box, ring, at) = squares[i];
                    float on = ghost ? 1f : Mathf.Clamp01((age - ring * 0.09f) / 0.12f);
                    float pulse = 0.85f + 0.15f * Mathf.Sin(age * 6f + ring * 1.3f);
                    float pop = 0f;
                    if (popped.TryGetValue(i, out float when) && age - when < 0.5f) pop = 1f - (age - when) / 0.5f;
                    float bright = hidden ? 0f : (ghost ? 0.55f + 0.1f * Mathf.Sin(age * 5f) : on * pulse * fade);
                    // A popped square turns pink rather than white-hot (it washed the pull out up close, R81).
                    tile.color = Hdr(Color.Lerp(Violet, Magenta, pop), 1.5f + 0.7f * pop);
                    tile.bright = bright;
                    tile.Step(dt, cam);
                    if (box != null)
                    {
                        float lift = 0.22f * Mathf.Sin(Mathf.Clamp01(pop) * Mathf.PI);
                        box.at = at + Vector3.up * lift;
                        box.scale = new Vector3(square * 0.9f, (0.45f + 0.5f * pop) * EaseOut(on), square * 0.9f);
                        box.color = Hdr(Color.Lerp(Violet, Magenta, 0.25f + 0.75f * pop), 1.3f + 0.6f * pop);
                        box.bright = bright;
                        box.Step(dt, cam);
                    }
                }
                float wireBright = (armed ? 1f : 0.4f) * (0.8f + 0.2f * Mathf.Sin(age * 9f)) * fade;
                var tripwire = source as SkillTripwire;
                for (int i = 0; i < wires.Count; i++)
                {
                    var wire = wires[i];
                    float tension = 0f;
                    if (tripwire != null)
                    {
                        int line = i / 2;
                        tripwire.LinePoints(line, out var a, out var mid, out var b);
                        if (i % 2 == 0) Rod(wire, a, mid);
                        else Rod(wire, mid, b);
                        tension = tripwire.Tension(line);
                    }
                    // Pulled taut, a line glows a little hotter along its length (only a little since R82: pulled far
                    // out it bloomed the stretched line away).
                    wire.color = Hdr(Color.Lerp(Color.Lerp(Violet, Magenta, 0.5f), Color.white, 0.2f * tension), 3f + 0.5f * tension);
                    wire.bright = wireBright;
                    wire.Step(dt, cam);
                }
                if (!ghost && !hidden && squares.Count > 0)
                    for (rise += dt * 14f * fade; rise >= 1f; rise -= 1f)
                    {
                        var s = squares[Random.Range(0, squares.Count)];
                        var off = new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f)) * square;
                        fx.motes.Emit(s.at + off, Vector3.up * Random.Range(0.5f, 1.2f), Random.Range(0.05f, 0.09f), Random.Range(0.6f, 1f), Tint(Violet, Random.value * 0.3f));
                    }
                return true;
            }

            public void Destroy()
            {
                foreach (var s in squares) { s.tile.Destroy(); s.box?.Destroy(); }
                foreach (var w in wires) w.Destroy();
                squares.Clear();
                wires.Clear();
            }
        }

        // ---------------------------------------------------------------- shared parts

        void HitStop(float seconds)
        {
            if (!hitStop || seconds <= 0f) return;
            if (!stopping)
            {
                stopResume = Time.timeScale;
                stopping = true;
            }
            stopLeft = Mathf.Max(stopLeft, seconds);
            Time.timeScale = 0f;
        }

        void Shake(float amplitude, float seconds)
        {
            // Settings → 카메라 흔들림 / 화면 흔들림 줄이기 (R77).
            amplitude *= ChessFight.Game.GameSettings.ShakeScale;
            if (amplitude <= 0f) return;
            if (!shake) return;
            float now = shakeTotal > 0f ? shakeAmp * Mathf.Clamp01(shakeLeft / shakeTotal) : 0f;
            if (amplitude < now) return;
            shakeAmp = amplitude;
            shakeLeft = shakeTotal = seconds;
        }

        void Edge(float seconds)
        {
            if (edge == null || !edge.Alive) edge = new EdgeFlash(lineMat);
            edge.left = edge.total = seconds;
        }

        void FlashWhite(RagdollPawn pawn, float seconds)
        {
            if (pawn == null) return;
            if (!flashes.TryGetValue(pawn, out var f))
            {
                f = new Flash();
                foreach (var r in pawn.GetComponentsInChildren<Renderer>())
                    if ((r is SkinnedMeshRenderer || r is MeshRenderer) && r.enabled)
                    {
                        f.renderers.Add(r);
                        f.materials.Add(r.sharedMaterials);
                        var white = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < white.Length; i++) white[i] = whiteMat;
                        r.sharedMaterials = white;
                    }
                flashes[pawn] = f;
            }
            f.left = Mathf.Max(f.left, seconds);
        }

        void StartSquash(RagdollPawn pawn)
        {
            if (pawn == null || pawn.skin == null) return;
            Transform bone = pawn.skin.rootBone != null ? pawn.skin.rootBone : pawn.skin.transform;
            foreach (var s in squashes) if (s.bone == bone) { s.born = Clock; return; }
            int axis = 1;
            float best = -1f;
            for (int a = 0; a < 3; a++)
            {
                Vector3 dir = a == 0 ? bone.right : a == 1 ? bone.up : bone.forward;
                float d = Mathf.Abs(Vector3.Dot(dir, Vector3.up));
                if (d > best) { best = d; axis = a; }
            }
            squashes.Add(new Squash { pawn = pawn, bone = bone, baseScale = bone.localScale, axis = axis, born = Clock });
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            float dt = Dt;
            clock += dt;
            if (stopping)
            {
                stopLeft -= dt;
                if (stopLeft <= 0f)
                {
                    stopping = false;
                    if (Time.timeScale == 0f) Time.timeScale = stopResume;
                }
            }
            var done = new List<RagdollPawn>();
            foreach (var kv in flashes)
            {
                kv.Value.left -= dt;
                if (kv.Value.left <= 0f || kv.Key == null) { kv.Value.Restore(); done.Add(kv.Key); }
            }
            foreach (var p in done) flashes.Remove(p);
        }

        void LateUpdate()
        {
            float dt = Dt;
            var cam = ViewCamera;
            if (cam != prepared)
            {
                PrepareCamera(cam);
                prepared = cam;
            }
            if (cam != null)
            {
                var bloom = cam.GetComponent<PawnRushSkillBloom>();
                if (bloom != null) bloom.enabled = effects;
            }

            ShineToon();
            WatchPieces();

            // Squash after the skeleton has been posed this frame.
            for (int i = squashes.Count - 1; i >= 0; i--)
                if (!squashes[i].Apply(Clock)) squashes.RemoveAt(i);

            sparks.Step(dt);
            motes.Step(dt);
            smoke.Step(dt);
            streaks.Step(dt);

            for (int i = anims.Count - 1; i >= 0; i--)
                if (!anims[i].Step(dt, cam)) { anims[i].Destroy(); anims.RemoveAt(i); }

            var gone = new List<Object>();
            foreach (var kv in tileSets)
                if (!kv.Value.Step(dt, cam, kv.Key == null)) { kv.Value.Destroy(); gone.Add(kv.Key); }
            foreach (var k in gone) tileSets.Remove(k);

            if (edge != null && edge.Alive) edge.Step(dt, cam);
            GhostSquares(dt, cam);

            if (shakeLeft > 0f && cam != null)
            {
                shakeLeft -= dt;
                shakeClock += dt;
                bool free = ViewOverride == null && game != null && game.labCamera != null && game.labCamera.freeMode;
                if (!free)
                {
                    // Strong at once, gone fast (a thud). Two sine waves a side, out of step: a rough judder over
                    // -1..1 (the Perlin noise before mostly stayed near the middle: a 0.1 m shake moved the view a
                    // few centimetres and could not be seen, R75). It turns the view as well as moving it: a turn
                    // shows on far things too, a step aside hardly does. 0.1 = up to 0.1 m and 1° (0.6° roll).
                    float k = Mathf.Clamp01(shakeLeft / Mathf.Max(0.01f, shakeTotal));
                    float a = shakeAmp * k * k, t = shakeClock;
                    float x = Mathf.Sin(t * 61f) * 0.6f + Mathf.Sin(t * 97f + 1.3f) * 0.4f;
                    float y = Mathf.Sin(t * 71f + 2.1f) * 0.6f + Mathf.Sin(t * 113f + 0.4f) * 0.4f;
                    float roll = Mathf.Sin(t * 53f + 4.2f);
                    var view = cam.transform;
                    view.position += (view.right * x + view.up * y * 0.7f) * a;
                    view.rotation *= Quaternion.Euler(y * a * 10f, x * a * 10f, roll * a * 6f);
                }
            }
        }

        // ---------------------------------------------------------------- building blocks

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static Vector3 HeadOf(RagdollPawn p) => p != null ? p.bodies[(int)BodyId.Head].position : Vector3.zero;

        /// <summary>The floor under a piece, not the piece itself.</summary>
        static Vector3 FloorUnder(RagdollPawn p)
        {
            Vector3 at = p.Hips.position, floor = at - Vector3.up * p.standHeight;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 0.3f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.distance < best && !hit.collider.GetComponentInParent<RagdollPawn>())
                {
                    best = hit.distance;
                    floor = hit.point;
                }
            return floor;
        }

        static Vector3 Ground(Vector3 at)
        {
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.GetComponentInParent<RagdollPawn>())
                return hit.point;
            return at;
        }

        static Material Unlit(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            m.color = c;
            return m;
        }

        /// <summary>A word that always faces the camera, with a dark outline (eight copies behind it).</summary>
        public class Text3D
        {
            public readonly Transform root;
            readonly TextMesh main;
            readonly TextMesh[] outline = new TextMesh[8];
            readonly Color color;
            readonly float height;

            public Text3D(Transform parent, Font font, string text, Color color, float height)
            {
                this.color = color;
                this.height = height;
                root = new GameObject($"Text {text}").transform;
                root.SetParent(parent, false);
                for (int i = 0; i < 8; i++) outline[i] = Make(font, text, Ink, i);
                main = Make(font, text, color, -1);
            }

            TextMesh Make(Font font, string text, Color c, int index)
            {
                var go = new GameObject(index < 0 ? "Main" : "Outline");
                go.transform.SetParent(root, false);
                var tm = go.AddComponent<TextMesh>();
                tm.font = font;
                tm.text = text;
                tm.fontSize = 64;
                tm.characterSize = height * 10f / 64f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontStyle = FontStyle.Bold;
                tm.color = c;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = font.material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (index >= 0)
                {
                    float a = index * Mathf.PI / 4f, o = height * 0.07f;
                    go.transform.localPosition = new Vector3(Mathf.Cos(a) * o, Mathf.Sin(a) * o, 0.02f);
                }
                return tm;
            }

            public void Place(Vector3 at, Camera cam, float scale, float alpha)
            {
                root.position = at;
                if (cam != null) root.rotation = cam.transform.rotation;
                root.localScale = Vector3.one * Mathf.Max(0.001f, scale);
                main.color = new Color(color.r, color.g, color.b, color.a * alpha);
                var ink = new Color(Ink.r, Ink.g, Ink.b, alpha * 0.9f);
                foreach (var o in outline) o.color = ink;
            }

            public void Destroy() { if (root != null) Object.Destroy(root.gameObject); }
        }

        abstract class Anim
        {
            protected float age;
            /// <summary>False once it is over.</summary>
            public abstract bool Step(float dt, Camera cam);
            public abstract void Destroy();
        }

        class Flash
        {
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<Material[]> materials = new List<Material[]>();
            public float left;

            public void Restore()
            {
                for (int i = 0; i < renderers.Count; i++)
                    if (renderers[i] != null) renderers[i].sharedMaterials = materials[i];
            }
        }

        class Squash
        {
            public RagdollPawn pawn;
            public Transform bone;
            public Vector3 baseScale, lastOffset, lastSet;
            public int axis;
            public float born;

            /// <summary>Flat at once, held a moment, then a springy way back (0.8 s in all). False once it is over.</summary>
            public bool Apply(float clock)
            {
                if (bone == null) return false;
                if (bone.position == lastSet) bone.position -= lastOffset;   // nothing re-posed it since last frame
                float t = clock - born;
                if (t > 0.8f) { Restore(); return false; }
                const float down = 0.05f, hold = 0.18f;
                float flat = t < down ? 0.58f * (t / down)
                    : t < hold ? 0.58f
                    : 0.58f * Mathf.Exp(-(t - hold) * 6f) * Mathf.Cos((t - hold) * 20f);
                float sy = Mathf.Clamp(1f - flat, 0.4f, 1.35f), sx = 1f + (1f - sy) * 0.6f;
                var s = baseScale;
                for (int a = 0; a < 3; a++) s[a] *= a == axis ? sy : sx;
                bone.localScale = s;
                float above = Mathf.Max(0f, bone.position.y - (pawn.Hips.position.y - pawn.standHeight));
                lastOffset = Vector3.down * above * (1f - sy);
                bone.position += lastOffset;
                lastSet = bone.position;
                return true;
            }

            public void Restore()
            {
                if (bone == null) return;
                if (bone.position == lastSet) bone.position -= lastOffset;
                bone.localScale = baseScale;
                lastOffset = Vector3.zero;
            }
        }

        /// <summary>The queen's blast splits the screen's edges red and cyan for a moment: two frames hung just
        /// in front of whichever camera is looking.</summary>
        class EdgeFlash
        {
            readonly LineRenderer red, cyan;
            public float left, total;
            /// <summary>False once the camera it hung on is gone (the film's camera, after a film).</summary>
            public bool Alive => red != null && cyan != null;

            public EdgeFlash(Material material)
            {
                red = MakeFrame(material, "Edge red");
                cyan = MakeFrame(material, "Edge cyan");
            }

            static LineRenderer MakeFrame(Material material, string name)
            {
                var lr = new GameObject(name).AddComponent<LineRenderer>();
                lr.sharedMaterial = material;
                lr.useWorldSpace = false;
                lr.loop = true;
                lr.positionCount = 4;
                lr.alignment = LineAlignment.TransformZ;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.enabled = false;
                return lr;
            }

            public void Step(float dt, Camera cam)
            {
                left -= dt;
                bool on = left > 0f && cam != null;
                red.enabled = cyan.enabled = on;
                if (!on) return;
                float a = Mathf.Clamp01(left / Mathf.Max(0.01f, total));
                Place(red, cam, -1f, new Color(1f, 0.25f, 0.25f, 0.85f * a));
                Place(cyan, cam, 1f, new Color(0.3f, 0.9f, 1f, 0.85f * a));
            }

            static void Place(LineRenderer lr, Camera cam, float side, Color color)
            {
                if (lr.transform.parent != cam.transform) lr.transform.SetParent(cam.transform, false);
                float z = cam.nearClipPlane + 0.02f;
                float h = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * z, w = h * cam.aspect;
                float o = h * 0.012f * side, inset = h * 0.025f;
                lr.transform.localPosition = new Vector3(o, -o * 0.5f, z);
                lr.transform.localRotation = Quaternion.identity;
                lr.SetPosition(0, new Vector3(-w + inset, -h + inset, 0f));
                lr.SetPosition(1, new Vector3(w - inset, -h + inset, 0f));
                lr.SetPosition(2, new Vector3(w - inset, h - inset, 0f));
                lr.SetPosition(3, new Vector3(-w + inset, h - inset, 0f));
                lr.startColor = lr.endColor = color;
                lr.widthMultiplier = h * 0.05f;
            }
        }
    }
}
