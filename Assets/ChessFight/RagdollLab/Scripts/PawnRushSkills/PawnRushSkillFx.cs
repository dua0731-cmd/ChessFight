using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
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
    /// R90 (승규 님: "A 디자인으로 폰러쉬 스킬 디자인 맞춰줘"): design A "잉크 테두리 장난감 체스", the one picked for all
    /// three modes (PawnRushSkillFx.DesignA.cs, parts in SkillInkKit): the telegraphs become real 1.5 m board squares and
    /// rings on the floor coloured for the viewer, hits flash the body white then the hitter's colour and leave a captured
    /// square under a fallen piece, and the glowing extras (sparks, motes, flares, pillars of light, edge flashes) are gone;
    /// the approved looks stay (queen R88, rook and knight R86, bishop R82) with A's finish.
    /// No star shapes anywhere (R78, R79), no words or numbers (R74), no chips or blocks (R75).
    ///
    /// Shared parts: a hit stop of a few frames (the whole game holds still, a test-bed stand-in for holding only the
    /// two pieces), camera shake and the hit flash on the piece that is hit. It listens to
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
        Material matPillar, matRing, matHalo, matTile, matCurtain, matTrail, matBolt, matRod;
        Camera prepared;

        // hit stop and shake
        float stopLeft, stopResume = 1f, shakeAmp, shakeLeft, shakeTotal, shakeClock;
        bool stopping;

        readonly List<Anim> anims = new List<Anim>();
        readonly Dictionary<Object, Squares3D> tileSets = new Dictionary<Object, Squares3D>();
        readonly Dictionary<RagdollPawn, Squares3D> ghosts = new Dictionary<RagdollPawn, Squares3D>();
        readonly Dictionary<RagdollPawn, Watch> watched = new Dictionary<RagdollPawn, Watch>();

        /// <summary>What a piece was doing last frame, to start its ongoing effects once.</summary>
        class Watch { public bool charging, dashing, leaping, crouching; }

        // Effect time (R90, as the Queen of the Hill effects): the game's own pace, so the lab's slow motion slows the
        // effects with the pieces (they ran at full speed through a slowed take before), but a hit stop does not stop
        // them; a recording (Time.captureFramerate) steps one frame of 1/60 s at a time.
        float fxDt, rate = 1f;
        float Dt => fxDt;
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
            BuildDesignA();
            RagdollPawn.SkillFx += OnSkillFx;
        }

        void OnDestroy()
        {
            RagdollPawn.SkillFx -= OnSkillFx;
            if (stopping) Time.timeScale = stopResume;
            DestroyDesignA();
            foreach (var a in anims) a.Destroy();
            foreach (var s in tileSets.Values) s.Destroy();
            foreach (var s in ghosts.Values) s.Destroy();
            if (root != null) Destroy(root.gameObject);
            DestroyParts();
        }

        // ---------------------------------------------------------------- the skills' moments

        // The moment being drawn: online its hit stop holds only these two pieces' picture (R111, D-S2).
        RagdollPawn stopBy, stopTarget;

        void OnSkillFx(SkillFxEvent e)
        {
            if (!effects) return;
            stopBy = e.by;
            stopTarget = e.target;
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
                case SkillFxKind.PawnStep: PawnStepStamp(e); break;
                case SkillFxKind.PawnHit: PawnHit(e); break;
                case SkillFxKind.PawnHelp: PawnHelp(e); break;
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
                bool crouching = effects && pawn.Piece == PieceKind.Knight && pawn.SkillStage == SkillStage.Windup;
                if (charging && !w.charging) QueenCharge(pawn);
                if (dashing && !w.dashing) RookCharge(pawn);
                if (leaping && !w.leaping) KnightLeap(pawn);
                if (crouching && !w.crouching) KnightGather(pawn);
                w.charging = charging;
                w.dashing = dashing;
                w.leaping = leaping;
                w.crouching = crouching;
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

        // ---- Queen (R86, after the reference's stylized explosion; R88, 승규 님 "체스 같은 스킬이라는 느낌이 안 나 —
        // 체스들이 모이면서 터지는 듯한 느낌": chess pieces gather and burst with it): while she charges, gold chess
        // pieces in cartoon bands pop up round her and swirl in, spinning like tops, closer and faster, heating up as they
        // come, into a ball of fire that gathers over her head, boiling and growing, with lines of light sucked in, while
        // cracks begin to glow in the floor under her; at the last moment the ball drops into her (the reference's
        // falling fireball). The blast: a white-hot flash; the chess pieces burst back out of it the same way round, hot,
        // tumbling, bouncing over the floor, cooling to gold and burning away; a fireball of lumpy puffs that bursts up
        // and out and within a few tenths of a second cools to charcoal smoke with fire glowing in its creases and hot
        // edges, billows up and is eaten away; a ring of dust rolling out over the floor to her outer radius, with the
        // two thin gold rings that show 1.5 m and 3 m; the cracks run out, glow, cool to scorch lines on a darkened
        // floor and fade; sparks shoot up in arcs, embers drift. The stop, the shake and the red/cyan edges stay. Her
        // rings on the floor while she charges are the skill's telegraph (SkillMarks), not these.
        // R90, design A: the knot of fire gathers at her chest (not over her head), eight pieces spiral in (1.25 turns,
        // easing in), no lines of light or glow round it; she coils down while charging and springs open. The blast:
        // a white ball round her for two frames, sixteen cel fire puffs racing out to her reach in eight frames with the
        // crown ring of eight pearls riding their rim, a thick gold ring at the knockdown edge and a thin one at her
        // reach, the eight pieces bursting back out and burning away; the fireball cools to charcoal smoke, the cracks
        // run out (R86). No sparks, embers, flare or red/cyan edges. Her warning rings: PawnRushSkillFx.DesignA.cs.

        static readonly Color FireGlow = new Color(1f, 0.55f, 0.12f);

        /// <summary>The cracks under a charging queen, carried on into her blast.</summary>
        readonly Dictionary<RagdollPawn, Crack> queenCracks = new Dictionary<RagdollPawn, Crack>();

        void QueenCharge(RagdollPawn q)
        {
            float windup = Params != null ? Params.queenWindup : 0.35f;
            Vector3 floor = FloorUnder(q);
            // At her chest (A; R88 held it up over her head), grown bigger than her middle so it shows round her.
            Vector3 Heart() => q != null ? ChestOf(q) + Vector3.up * 0.05f : floor + Vector3.up;
            // How far into her charge she is, in game time, 1 once it is over (R88: timed by her stage, so a slowed
            // film shows it in step with her).
            bool Charging() => QueenCharging(q);
            float Charge() => Charging() ? Mathf.Clamp01(q.SkillStageTime / windup) : 1f;
            // The knot of fire: small and boiling, growing faster towards the end (A: 0.2 → 0.85 m across).
            Add(new Puff(root, "Queen gathering fire", meshPuff, matFire)
            {
                life = 10f,
                lump = 0.42f,
                flow = 6f,
                heat = 0.62f,
                rim = Hdr(FireGlow, 1.3f),
                animate = (p, t) =>
                {
                    if (!Charging()) { p.life = p.Age; p.scale = Vector3.zero; return; }
                    float k = Charge();
                    p.at = Heart();
                    p.scale = Vector3.one * Mathf.Lerp(0.2f, 0.85f, EaseOut(k * 1.15f)) * (1f + 0.08f * Mathf.Sin(p.Age * 75f));
                },
            });
            GatherChess(Charging, Charge, () => q != null ? Flat(q.Hips.position) + Vector3.up * floor.y : floor, Heart);
            if (queenCracks.TryGetValue(q, out var old)) old.life = 0f;
            queenCracks[q] = QueenCracks(floor, Charge, Charging);
        }

        /// <summary>
        /// The queen's chess pieces gathering (R88; R90 design A): eight of the six kinds pop up standing on the floor
        /// round her at about her inner reach, at uneven angles (not a compass), and spiral in 1.25 turns, standing and
        /// spinning like tops (R81's "회전하면서"), easing in, up into <paramref name="heart"/>, shrinking to 60% and
        /// heating up as they come (fire glowing in their creases, a hot edge); a short inked gold trail behind each shows
        /// the swirl. Timed by <paramref name="charge"/> (her charge, 0..1 in game time), gone if the charge is called off.
        /// </summary>
        void GatherChess(Func<bool> charging, Func<float> charge, Func<Vector3> feet, Func<Vector3> heart)
        {
            const int count = 8;
            float way = Random.value < 0.5f ? -1f : 1f;
            for (int i = 0; i < count; i++)
            {
                int kind = i % chessMeshes.Length;
                float a0 = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / count;
                float r0 = Random.Range(1.35f, 1.65f);
                // The share of the charge by which it has arrived.
                float arrive = Random.Range(0.85f, 0.95f), size = ChessKing * ChessHeights[kind];
                float yaw0 = Random.Range(0f, 360f), spin = Random.Range(700f, 1000f) * way;
                var piece = Add(new Puff(root, "Queen gathering chess piece", chessMeshes[kind], matChess)
                {
                    life = 10f,
                    lump = 0f,
                    flow = 2f,
                    animate = (p, t) =>
                    {
                        float k = Mathf.Clamp01(charge() / arrive), age = p.Age;
                        if (k >= 1f || !charging())
                        {
                            p.life = p.Age;
                            p.scale = Vector3.zero;
                            return;
                        }
                        // From standing on the floor round her (pieces on a board) up into the fire, easing in.
                        float e = k * k * k;
                        Vector3 center = Vector3.Lerp(feet() + Vector3.up * (size * 0.5f + 0.02f), heart(), e);
                        float a = a0 + way * 1.25f * Mathf.PI * 2f * k, r = r0 * (1f - e);
                        p.at = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                        p.rotation = Quaternion.Euler(0f, yaw0 + spin * (age + age * age * 2f), 0f) * Quaternion.Euler(8f * Mathf.Sin(age * 20f), 0f, 0f);
                        float pop = age < 0.08f ? 1f + 0.3f * Mathf.Sin(age / 0.08f * Mathf.PI) : 1f;
                        p.scale = Vector3.one * size * Mathf.Clamp01(age / 0.04f) * pop * Mathf.Lerp(1f, 0.6f, e);
                        p.heat = Keys(k, 0.45f, 0f, 1f, 0.75f);
                        p.rim = Hdr(FireGlow, 1.3f * k * k);
                    },
                });
                if (kit != null) InkTrail(() => piece.Alive ? piece.at : (Vector3?)null, Kit.Queen, 0.1f, 0.07f);
            }
        }

        /// <summary>
        /// The queen's chess pieces bursting back out of the blast (R88, the swirl the same way round as R81's): hot at
        /// first, tumbling, falling and bouncing over the floor, cooling to gold, burning away at the end.
        /// </summary>
        void BurstChess(Vector3 pop, float floorY)
        {
            const int count = 8;   // A: the eight that gathered
            float way = Random.value < 0.5f ? -1f : 1f;
            for (int i = 0; i < count; i++)
            {
                int kind = i % chessMeshes.Length;
                float a = i * Mathf.PI * 2f / count + Random.Range(-0.12f, 0.12f);
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var around = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * way;
                // Kept within about 3 m (faster ones flew into the camera, R81).
                Vector3 pos = pop + radial * 0.25f;
                Vector3 vel = radial * Random.Range(3f, 5f) + around * Random.Range(1.5f, 3f) + Vector3.up * Random.Range(2.5f, 4.5f);
                Quaternion rot = Random.rotation;
                Vector3 tumble = Random.onUnitSphere * Random.Range(400f, 800f);
                float size = ChessKing * ChessHeights[kind], last = 0f;
                Add(new Puff(root, "Queen burst chess piece", chessMeshes[kind], matChess)
                {
                    life = Random.Range(1.05f, 1.2f),   // A: gone into a small gold puff by 1.2 s
                    lump = 0f,
                    flow = 2f,
                    animate = (p, t) =>
                    {
                        float dt = p.Age - last;
                        last = p.Age;
                        vel += Physics.gravity * dt;
                        pos += vel * dt;
                        float rest = floorY + size * 0.5f;
                        if (pos.y < rest && vel.y < 0f)
                        {
                            pos.y = rest;
                            vel.y = -vel.y * 0.42f;
                            vel.x *= 0.65f;
                            vel.z *= 0.65f;
                            tumble *= 0.55f;
                        }
                        rot = Quaternion.Euler(tumble * dt) * rot;
                        p.at = pos;
                        p.rotation = rot;
                        p.scale = Vector3.one * size * Mathf.Lerp(1.3f, 1f, Mathf.Clamp01(p.Age / 0.1f));
                        p.heat = Keys(p.Age, 0.03f, 0.7f, 0.25f, 0f);
                        p.rim = Hdr(FireGlow, 1.5f * Mathf.Clamp01(1f - p.Age / 0.5f));
                        p.dissolve = Keys(t, 0.72f, 0f, 1f, 1f);
                    },
                });
            }
        }

        /// <summary>The queen's cracks: creeping out dimly as her charge goes on (<paramref name="charge"/>, 0..1; fading
        /// if it ends with no blast), then, once blasted, running right out hot, cooling to scorch lines and fading.</summary>
        Crack QueenCracks(Vector3 floor, Func<float> charge, Func<bool> charging)
        {
            var crack = new Crack(root, meshQuad, matCrack) { at = floor, radius = 2.4f, life = 10f, paint = 0.3f };
            float endedAt = -1f;
            crack.animate = (c, t) =>
            {
                if (c.BlastAt < 0f)
                {
                    float k = charge();
                    c.reveal = 0.3f * k * k;
                    c.hot = Hdr(Gold, 1f + 0.6f * k);
                    if (endedAt < 0f && !charging()) endedAt = c.Age;
                    if (endedAt >= 0f && c.Age - endedAt > 0.25f) c.fade = Mathf.Clamp01(1f - (c.Age - endedAt - 0.25f) / 0.2f);
                    if (c.fade <= 0f) c.life = c.Age;
                    return;
                }
                float b = c.Age - c.BlastAt;
                c.reveal = Mathf.Lerp(c.RevealAtBlast, 1.02f, EaseOut(b / 0.09f));
                c.hot = Hdr(Gold, Keys(b, 0f, 2f, 0.3f, 1.4f));
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
            Vector3 pop = e.by != null ? ChestOf(e.by) + Vector3.up * 0.05f : c + Vector3.up * 0.5f;

            // The cracks under her run right out (from now, if her charge was not seen).
            if (e.by == null || !queenCracks.TryGetValue(e.by, out var crack) || crack.Gone) crack = QueenCracks(c, () => 1f, () => false);
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

            // She springs open with a white ball round her for two frames (A).
            if (e.by != null) kit.AddSquash(e.by, Kit.SquashKind.Spring);
            WhiteBall(pop, 0.55f);
            BurstChess(pop, c.y);

            // The fireball: lumpy puffs bursting up and out, burning, cooling to charcoal smoke with fire in its creases
            // and hot edges, billowing up and out, eaten away from the edges in.
            for (int i = 0; i < 6; i++)
            {
                Vector3 dir = i < 2 ? (Vector3.up + Random.insideUnitSphere * 0.3f).normalized : OnSphere(0.15f);
                float reach = Random.Range(0.4f, 0.8f), size = Random.Range(0.5f, 0.75f), rise = Random.Range(0.6f, 0.95f);
                Vector3 from = pop + dir * 0.15f, to = pop + dir * reach + Vector3.up * Random.Range(0f, 0.3f);
                Add(new Puff(root, "Queen fireball", meshPuff, matFire)
                {
                    life = Random.Range(0.7f, 0.9f),
                    lump = Random.Range(0.32f, 0.45f),
                    flow = 1.6f,
                    rotation = Random.rotation,
                    animate = (p, t) =>
                    {
                        float a = p.Age;
                        p.at = Vector3.Lerp(from, to, EaseOut(a / 0.32f)) + Vector3.up * rise * a;
                        p.scale = Vector3.one * size * Mathf.Lerp(0.3f, 1f, EaseOut(a / 0.25f)) * Mathf.Lerp(1f, 1.35f, t);
                        p.heat = Keys(a, 0.03f, 1f, 0.12f, 0.5f, 0.32f, 0.18f, 0.6f, 0f);
                        p.rim = Hdr(FireGlow, 1.4f * Sq(Mathf.Clamp01(1f - a / 0.75f)));
                        p.dissolve = Keys(t, 0.25f, 0f, 1f, 1f);
                    },
                });
            }

            // A ring of sixteen cel fire puffs racing out to her reach in eight frames (A), the crown ring on its rim.
            const int ring = 16;
            float turn = Random.Range(0f, 360f);
            for (int i = 0; i < ring; i++)
            {
                float a = (turn + (i + Random.Range(-0.3f, 0.3f)) * 360f / ring) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float reach = outer * Random.Range(0.9f, 1f), size = Random.Range(0.32f, 0.46f);
                // Fire, not smoke: they stay hot and are eaten away within a third of a second.
                Add(new Puff(root, "Queen fire ring", meshPuff, matFire)
                {
                    life = Random.Range(0.3f, 0.36f),
                    lump = 0.4f,
                    flow = 2f,
                    rotation = Quaternion.LookRotation(radial),
                    animate = (p, t) =>
                    {
                        float k = EaseOut(p.Age / (8f / 60f));
                        p.at = c + radial * Mathf.Lerp(0.3f, reach, k) + Vector3.up * (0.12f + 0.15f * k);
                        p.scale = new Vector3(size, size * 0.85f, size * 1.15f) * Mathf.Lerp(0.4f, 1f, k);
                        p.heat = Keys(p.Age, 0.03f, 0.95f, 0.3f, 0.55f);
                        p.rim = Hdr(FireGlow, 1.2f * Mathf.Clamp01(1f - p.Age / 0.25f));
                        p.dissolve = Keys(t, 0.35f, 0f, 1f, 1f);
                    },
                });
            }
            QueenCrown(c, inner, outer);
            HitStop(6f * Frame);
            Shake(0.16f, 10f * Frame);
        }

        void QueenHit(SkillFxEvent e)
        {
            // A: white for two frames, then her gold for two; three drops off the way it is pushed; the captured square
            // under it once it lies (the inner ring knocks down).
            Vector3 at = e.target != null ? ChestOf(e.target) : e.at + Vector3.up * 0.3f;
            Vector3 away = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            kit.FlashBody(e.target, Kit.Queen.main);
            kit.Drops(at, away + Vector3.up * 0.25f, Kit.Queen, 3, 6f);
            if (e.count == 1) kit.Down(e.target, Kit.Queen, e.at, Square);
        }

        // ---- Rook (R86, after the reference's electric dash; it was a shield and a tail of fire): while it charges, a
        // bright spindle of light rides ahead of it, a white-hot core and an orange body trail behind, speed lines
        // stream back off it and lightning crawls along its path, crackling. Each piece it sends flying: a flash,
        // lightning bursting out, speed lines and sparks thrown along its line and a ring rushing out, each bigger and
        // with a longer stop (0.04, 0.06, 0.08 s) and shake than the one before; the fourth, which stops it, adds
        // lightning running out over the floor. Into a wall: lightning spreading over the wall's face.
        // R90, design A: the charge's lightning, spindle and speed lines stay (no crackling sparks); each piece it bats
        // aside flashes white then orange, a C half ring is flung the way it flies (20% bigger each time) with three
        // drops, a 3/4/5-frame stop, and it gets its captured square once it lies; into a wall (a barricade, the floor
        // under an air charge) the battlement ring is slammed flat on it with square stone puffs and a 5-frame stop, the
        // rook squashed wide. Its four warning squares: PawnRushSkillFx.DesignA.cs.

        /// <summary>The white-orange of the rook's lightning.</summary>
        static readonly Color Volt = new Color(1f, 0.86f, 0.55f);

        void RookCharge(RagdollPawn r)
        {
            Vector3? Where() => RookDashing(r) ? r.Hips.position : (Vector3?)null;
            Vector3? Chest() => RookDashing(r) ? ChestOf(r) : (Vector3?)null;
            anims.Add(new Streak(root, matTrail, Hdr(Fire, 1.25f), 0.75f, 0.2f, Where) { paint = 0.8f });
            anims.Add(new Streak(root, matTrail, Hdr(Volt, 3.4f), 0.16f, 0.32f, Where) { paint = 0.25f });
            anims.Add(new PathBolts(root, matVolt, Chest, 1.6f));
            Vector3 heading = Flat(r.transform.forward).sqrMagnitude > 0.01f ? Flat(r.transform.forward).normalized : Vector3.forward;
            float endedAt = -1f;
            // The spindle of light ahead of it.
            Add(new Beam(root, matFlare)
            {
                life = 5f,
                color = Hdr(Fire, 1.4f),
                paint = 0.7f,
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
            float line = 0f, kick = 0f;
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
                    SpeedLine(at, -heading, Random.Range(0.8f, 1.9f), Random.Range(1f, 3f), Random.Range(0.08f, 0.14f), Random.Range(0.12f, 0.2f));
                }
                if (r.Grounded)
                    for (kick += dt * 25f; kick >= 1f; kick -= 1f)
                        smoke.Emit(Ground(r.Hips.position) - heading * 0.3f + Random.insideUnitSphere * 0.2f, -heading * Random.Range(0.5f, 1.5f) + Vector3.up * 0.4f,
                            Random.Range(0.35f, 0.55f), Random.Range(0.5f, 0.8f), (Color32)Dust, Random.Range(-60f, 60f));
                return true;
            }));
        }

        void RookHit(SkillFxEvent e)
        {
            int i = Mathf.Clamp(e.count, 1, 3);
            var p = Kit.Rook;
            Vector3 fly = RookFlight(e);
            Vector3 at = e.target != null ? ChestOf(e.target) : e.at;
            kit.HalfRing(at, fly, p, 0.55f * (1f + 0.2f * (i - 1)), 0.15f);
            InkHit(e.target, at, fly, p, 2 + i, 0.04f * (1f + 0.5f * (i - 1)), 3 + i);
            anims.Add(new Bolts(root, matVolt, 3 + i, () => e.at, Hdr(Volt, 2f), 0.6f + 0.15f * i, 0.12f) { toon = true });
            kit.Down(e.target, p, e.at, Square);
        }

        /// <summary>Which way a piece the rook hits flies: off to the side it was on, a little along the charge.</summary>
        static Vector3 RookFlight(SkillFxEvent e)
        {
            Vector3 along = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, along);
            float side = 1f;
            if (e.by != null && e.target != null) side = Vector3.Dot(Flat(e.target.Hips.position - e.by.Hips.position), right) >= 0f ? 1f : -1f;
            return (right * side * 0.9f + along * 0.45f).normalized;
        }

        /// <summary>The piece that stops the charge (the one after its last): the strongest hit of the rook's.</summary>
        void RookStop(SkillFxEvent e)
        {
            var p = Kit.Rook;
            Vector3 along = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Vector3 at = e.target != null ? ChestOf(e.target) : e.at;
            kit.HalfRing(at, along, p, 0.8f, 0.18f);
            InkHit(e.target, at, along, p, 5, 0.12f, 8, 4);
            anims.Add(new Bolts(root, matVolt, 6, () => e.at, Hdr(Volt, 2f), 1f, 0.16f) { toon = true });
            kit.PuffBurst(Ground(e.at) + Vector3.up * 0.15f, Kit.Dust, 4, 0.5f, 0.25f);
            kit.Down(e.target, p, e.at, Square);
        }

        /// <summary>A charge down out of the air into the floor (R80): the battlement ring slammed flat on the floor,
        /// lightning running out over it, the rook squashed wide (A).</summary>
        void RookSlam(SkillFxEvent e)
        {
            Vector3 g = Ground(e.at + Vector3.up * 0.3f);
            BattlementRing(g + Vector3.up * 0.01f, Vector3.up, 1f);
            anims.Add(new Bolts(root, matVolt, 6, () => g + Vector3.up * 0.05f, Hdr(Volt, 2f), 1.6f, 0.3f) { plane = Vector3.up, toon = true });
            if (e.by != null) kit.AddSquash(e.by, Kit.SquashKind.Wide);
            HitStop(5f * Frame);
            Shake(0.16f, 8f * Frame);
        }

        /// <summary>Into a wall (or, smaller, a barricade): the battlement ring slammed flat on its face with square stone
        /// puffs and lightning spreading over it, the rook squashed wide, a 5-frame stop and the biggest shake (A).
        /// R92: flat on the face the charge met, no longer square to the charge: a wall met at a slant cut the ring in
        /// half and buried the rest in it. Fitted onto that face (<see cref="FitOnWall"/>); a barricade is gone the moment
        /// it breaks, so there only the floor counts.</summary>
        void RookWall(SkillFxEvent e, float k)
        {
            Vector3 d = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Vector3 n = WallFace(e.at, d, e.normal);
            float radius = 0.75f * k + 0.25f;
            Vector3 at = FitOnWall(e.at, n, ref radius, e.kind == SkillFxKind.RookWall);
            BattlementRing(at, n, radius);
            anims.Add(new Bolts(root, matVolt, Mathf.RoundToInt(6 * k), () => at + n * 0.06f, Hdr(Volt, 2f), Mathf.Min(1.4f * k, 1.25f * radius), 0.25f) { plane = n, toon = true });
            if (e.by != null) kit.AddSquash(e.by, Kit.SquashKind.Wide);
            HitStop(5f * Frame);
            Shake(0.16f * k, 8f * Frame);
        }

        // ---- Bishop B: the board squares under the X turn into boxes of light, lit from the middle out and pulsing
        // while the wire lasts, the two lines into beams of light; the square an enemy trips on throws up a pillar.
        // R90, design A: the squares and the wire stay (R82, "지금 좋아") and get A's wrapper (PawnRushSkillFx.DesignA.cs):
        // the aim as rows of small violet squares with a mitre diamond, the X thrown as a prop, four puffs where its rods
        // stab the floor, a team rim round the squares, a white band as it arms, half bright while arming, 40% at rest
        // and three blinks in its last second; a trip flashes the piece white then violet, a violet crescent sweeps under
        // its feet with a small ring at shin height, a 4-frame stop, and its captured square (no pillar, sparks or flare).

        void BishopWire(SkillFxEvent e)
        {
            if (e.source == null) return;
            var p = Params;
            float height = p != null ? p.bishopHeight : 0.3f;
            var set = new Squares3D(this, Ground(e.at), e.dir, e.size, false) { source = e.source, owner = e.by };
            set.AddWires(e.at, e.dir, e.size, height);
            if (tileSets.TryGetValue(e.source, out var old)) old.Destroy();
            tileSets[e.source] = set;
            // Its four rods stab the floor (A): a small violet puff at each end.
            WireLanding(Ground(e.at), e.dir, e.size);
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
            var p = Kit.Bishop;
            Vector3 run = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            if (e.source != null && tileSets.TryGetValue(e.source, out var set)) set.Pop(e.at);
            Vector3 feet = (e.target != null ? Ground(e.target.Hips.position) : Ground(e.at)) + Vector3.up * 0.03f;
            float shin = Params != null ? Params.bishopHeight : 0.25f;
            // A: a violet crescent sweeping under its feet (160°, centred on the way it ran), a small ring at shin height.
            Crescent(feet, Quaternion.Euler(0f, 80f, 0f) * run, p, 0.7f, 0.08f, 160f, 6);
            kit.ImpactRing(feet + Vector3.up * shin, run, p, 0.2f, 0.5f);
            InkHit(e.target, feet + Vector3.up * shin, run, p, 4, 0.08f, 6, 3);
            kit.Down(e.target, p, e.at, Square);
        }

        // ---- Knight (R86, after the reference's hand-drawn wind burst; it was trails, rings and walls of blue light):
        // wind in cartoon puffs and swooshes, white, sky and deep blue in hard bands, inked in navy, bursting out and
        // eaten away (the reference's "afterimage"). The take-off throws puffs out round the feet with a swoosh on the
        // floor and a swirl rising round the knight; in the air small puffs drop behind it; the second press ("당")
        // kicks puffs back from under its feet with a ring of wind it bursts through; the stomp throws a crown of puffs
        // and two swooshes out round the head; a landing bursts puffs out over the floor to 1.5 m with swooshes
        // sweeping round. A dazed piece has three little puffs and a swoosh circling over its head (no stars).
        // R90, design A: five puffs gather at its feet as it crouches; the take-off flashes a white ring at its feet,
        // five puffs squash out to 1 m and two swooshes curl up round it; in the air an inked wind ribbon trails from
        // its hips (no glowing streaks), keeping a sharp corner where it kicks off the air ("당": a two-frame pause, a
        // blue crescent round it and a small ring across the new way); the stomp flattens the piece to 60% with a level
        // blue ring at head height, three drops, a 5-frame stop, its captured square and three little cream pawns over
        // its head (no stars); a landing rolls eight puffs out to 1.5 m and stamps a horseshoe scuff, and each piece it
        // staggers flashes. Its squares and horseshoe landing mark: PawnRushSkillFx.DesignA.cs.

        void KnightLeap(RagdollPawn k)
        {
            // A: a ribbon of wind from its hips (0.45 s, 0.42 m at the head to nothing), white core, sky, navy ink.
            InkTrail(() => KnightLeaping(k) ? k.Hips.position : (Vector3?)null, Kit.Knight, 0.45f, 0.42f, Color.white);
        }

        /// <summary>"다": the leap's take-off (A): a white ring at the feet for two frames, five wind puffs squashing out
        /// to 1 m in eight frames, two swooshes curling up round the knight; off the air (R81) the same under its feet.</summary>
        void KnightTakeOff(SkillFxEvent e)
        {
            var k = e.by;
            Vector3 floor = Ground(e.at + Vector3.up * 0.3f);
            if (k != null) leapPaths[k] = new LeapPath { start = floor, launchedAt = Clock };
            Shake(0.04f, 4f * Frame);
            Vector3 at = (e.count == 1 ? e.at : floor) + Vector3.up * 0.02f;
            kit.FlashRing(at, 0.4f, Kit.Knight);
            kit.DustRing(at, 5, 1f, Kit.Knight, 0.3f, 0.45f, 8f);
            WindArc(at + Vector3.up * 0.05f, Vector3.up, 0.6f, 0.75f, 0.22f, 420f, 0.38f, 0f, 0.85f, 1.2f, () => k != null ? k.Hips.position - Vector3.up * 0.6f : at);
            WindArc(at + Vector3.up * 0.05f, Vector3.up, 0.5f, 0.68f, 0.18f, 380f, 0.36f, 0.05f, 0.85f, 1.1f, () => k != null ? k.Hips.position - Vector3.up * 0.6f : at);
        }

        /// <summary>"당" (R80): the second press kicks off the air, turned or straight on (A): a two-frame pause, a blue
        /// crescent sweeping round the knight, a small ring opening at the corner across the new way (its trail keeps
        /// the corner: an L in the sky).</summary>
        void KnightTurn(SkillFxEvent e)
        {
            var k = e.by;
            Vector3 d = e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : Vector3.forward;
            if (k != null && leapPaths.TryGetValue(k, out var path))
            {
                path.corner = Ground(e.at);
                path.launchedAt = Clock;
            }
            HitStop(2f * Frame);
            Shake(0.06f, 6f * Frame);
            Crescent(e.at, Quaternion.Euler(0f, 45f, 0f) * Flat(d).normalized, Kit.Knight, 0.75f, 0.1f, 90f, 6);
            FlatImpactRing(e.at, Kit.Knight, 0.05f, 0.45f, d);
        }

        void KnightHome(SkillFxEvent e)
        {
            // Homing onto a head: its wind ribbon runs on; the stomp is the moment (A).
            if (e.by != null && leapPaths.TryGetValue(e.by, out var path)) path.launchedAt = Clock;
        }

        void KnightStomp(SkillFxEvent e)
        {
            var p = Kit.Knight;
            // A: flat in four frames (to 60%), a level blue ring at head height, three drops down and out, the body
            // white then sky, a 5-frame stop; then its captured square and three little cream pawns over its head.
            kit.AddSquash(e.target, Kit.SquashKind.Flatten, 0.5f);
            FlatImpactRing(e.at, p, 0.3f, 0.75f);
            InkHit(e.target, e.at, Vector3.down + Flat(e.dir) * 0.6f, p, 5, 0.12f, 8, 3);
            kit.Down(e.target, p, e.at, Square);
            kit.Daze(e.target, 0.3f, 1.2f);
        }

        void KnightLand(SkillFxEvent e)
        {
            var k = e.by;
            if (k != null) leapPaths.Remove(k);
            Vector3 g = Ground(e.at) + Vector3.up * 0.02f;
            float radius = e.size > 0f ? e.size : 1.5f;
            var p = Kit.Knight;
            // A: eight wind puffs rolling out to the stagger reach in ten frames, a horseshoe scuff stamped where it came
            // down (sky, ink) fading over a second; each piece it staggers flashes, with a 3-frame stop.
            kit.DustRing(g, 8, radius, p, 0.32f, 0.6f, 10f);
            var scuff = new Kit.Tile(kit, "Horseshoe scuff");
            Vector3 along = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            var f = kit.Run(60f * Frame, (fx, d) =>
            {
                scuff.Floor(g, along, Vector2.one * Kit.Pop(fx.age, 1.1f, 0.6f));
                Kit.Horseshoe(scuff, p, 0.6f);
                scuff.fade = 1f - fx.age / fx.life;
                scuff.Apply();
                return true;
            });
            f.tiles.Add(scuff);
            Shake(0.08f, 6f * Frame);
            if (e.count > 0)
            {
                bool any = false;
                foreach (var other in RagdollPawn.All)
                    if (other != null && other != k && other.Staggered && Flat(other.Hips.position - g).magnitude <= radius + 0.5f)
                    {
                        kit.FlashBody(other, p.main);
                        any = true;
                    }
                if (any) HitStop(3f * Frame);
            }
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
            public readonly bool ghost;
            /// <summary>The bishop that laid it (its team rim is read for the viewer).</summary>
            public RagdollPawn owner;
            readonly List<(Shape tile, Shape box, int ring, Vector3 at)> squares = new List<(Shape, Shape, int, Vector3)>();
            readonly List<Shape> wires = new List<Shape>();
            public Object source;
            public float square;
            public bool hidden;
            float age, fadeLeft = -1f, rise, armedAge = -1f, lastLevel = 1f;
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

            /// <summary>Its squares: middle, size and turn (A's team rim goes round each).</summary>
            public IEnumerable<(Vector3 at, float size, Quaternion rotation)> SquarePoints
            {
                get
                {
                    foreach (var s in squares) yield return (s.at, square, s.tile.rotation * Quaternion.Inverse(Quaternion.LookRotation(Vector3.down, Vector3.forward)));
                }
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
                // Gone in twelve frames once its wire is (A).
                const float fadeTime = 12f / 60f;
                if (sourceGone && fadeLeft < 0f) fadeLeft = fadeTime;
                if (fadeLeft >= 0f)
                {
                    fadeLeft -= dt;
                    if (fadeLeft <= 0f) return false;
                }
                float fade = fadeLeft >= 0f ? fadeLeft / fadeTime : 1f;
                bool armed = source is SkillTripwire w && w != null && w.Armed;
                // A's brightness: half while it arms, a pulse as it arms, 40% at rest, three blinks in its last second.
                if (armed && armedAge < 0f) armedAge = age;
                float level = fx.kit != null && !ghost ? WireLevel(source as SkillTripwire, armed ? age - armedAge : -1f) : 1f;
                if (fadeLeft >= 0f && fx.kit != null) level = lastLevel;
                lastLevel = level;
                for (int i = 0; i < squares.Count; i++)
                {
                    var (tile, box, ring, at) = squares[i];
                    float on = ghost ? 1f : Mathf.Clamp01((age - ring * 0.09f) / 0.12f);
                    float pulse = 0.85f + 0.15f * Mathf.Sin(age * 6f + ring * 1.3f);
                    float pop = 0f;
                    if (popped.TryGetValue(i, out float when) && age - when < 0.5f) pop = 1f - (age - when) / 0.5f;
                    float bright = hidden ? 0f : (ghost ? 0.55f + 0.1f * Mathf.Sin(age * 5f) : on * pulse * fade * level);
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
                float wireBright = (fx.kit != null ? level : armed ? 1f : 0.4f) * (0.8f + 0.2f * Mathf.Sin(age * 9f)) * fade;
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
                    // out it bloomed the stretched line away), and whiter where it is pulled (A: violet → white).
                    wire.color = Hdr(Color.Lerp(Color.Lerp(Violet, Magenta, 0.5f), Color.white, 0.55f * tension), 3f + 0.3f * tension);
                    wire.bright = wireBright;
                    wire.Step(dt, cam);
                }
                if (!ghost && !hidden && squares.Count > 0)
                    for (rise += dt * 14f * fade * Mathf.Min(1f, level); rise >= 1f; rise -= 1f)
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
            if (game != null && game.NetworkControlled)
            {
                // Online (R111, D-S2) the game must not stop (the host's physics, everyone's inputs): only the hitter's and
                // the hit piece's picture holds still, on this screen by itself.
                if (stopBy != null) stopBy.HoldVisual(seconds);
                if (stopTarget != null) stopTarget.HoldVisual(seconds);
                return;
            }
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

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            float real = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (Time.timeScale > 0f && !stopping) rate = Time.timeScale;
            fxDt = real * rate;
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

            sparks.Step(dt);
            motes.Step(dt);
            smoke.Step(dt);

            for (int i = anims.Count - 1; i >= 0; i--)
                if (!anims[i].Step(dt, cam)) { anims[i].Destroy(); anims.RemoveAt(i); }

            var gone = new List<Object>();
            foreach (var kv in tileSets)
                if (!kv.Value.Step(dt, cam, kv.Key == null)) { kv.Value.Destroy(); gone.Add(kv.Key); }
            foreach (var k in gone) tileSets.Remove(k);

            // Design A's parts: what the skills show while they last, then every timed part, the hit flashes and,
            // after the skeleton has been posed this frame, the squashes.
            if (kit != null)
            {
                WatchDesignA();
                kit.Step(dt);
            }
            else GhostSquares(dt, cam);

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

    }
}
