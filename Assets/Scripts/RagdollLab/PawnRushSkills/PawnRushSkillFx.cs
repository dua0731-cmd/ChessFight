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

        // Outside the lab (Pawn Rush course 01, R125, PawnRushCourseSkills): the numbers, the person at this PC, and
        // whether a hit stop may only hold the two pieces' picture (online) instead of the game.
        public PawnRushSkillParams ParamsOverride { get; set; }
        public Func<RagdollPawn> ViewerOverride { get; set; }
        public bool OnlineOverride { get; set; }

        PawnRushSkillParams Params
        {
            get
            {
                if (ParamsOverride != null) return ParamsOverride;
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
                // R146: the king's call (PawnRushSkillFx.KingCall.cs).
                case SkillFxKind.KingCall: KingCallFx(e); break;
                case SkillFxKind.KingCallGo: KingCallGoFx(e); break;
                case SkillFxKind.KingCallLand: KingCallLandFx(e); break;
                case SkillFxKind.KingCallEnd: KingCallEndFx(e); break;
                // R153: the rook's chain (PawnRushSkillFx.Chain.cs).
                case SkillFxKind.RookThrow: RookThrowFx(e); break;
                case SkillFxKind.RookHook: RookHookFx(e); break;
                case SkillFxKind.RookPullEnd: RookPullEndFx(e); break;
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
                // R138: no wind ribbon from the knight's hips while it leaps (승규 님: "엉덩이 부분에 흰색을 빼주고").
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
        // R138 (승규 님: "저 폭발 같은 거 빼주고 퀸 스킬에 맞는 충격파 같은 게 펑~하고"): no fire at all any more — the knot of
        // fire, the fireball, the ring of fire puffs, the cracks and the scorch are gone; the air gathers into her while she
        // charges and goes out as a shock wave (PawnRushSkillFx.Shock.cs); her pieces swirl in polished and are swept out.
        // R142 (승규 님: "지금 공기 충격파 좋은데 전기+공기 충격파 같은 거였으면 좋겠어"): the air is charged: electric arcs
        // (SkillArc) crackle round her as she charges, shoot out along her eight ways and run along the ring's front at the
        // "펑", and over each piece it hits.
        // R143 (승규 님, a screen recording of her in play: "적용 된거야 난 안된거 같은데"): in play the arcs were two faint pale
        // flickers a tenth of a second each. Now solid gold, longer, staying round her (down to the floor as she charges,
        // flickering on, crawling over the floor and crackling on her after the "펑"), and lighting what is round them.

        void QueenCharge(RagdollPawn q)
        {
            float windup = Params != null ? Params.queenWindup : 0.35f;
            Vector3 floor = FloorUnder(q);
            // At her chest (A; R88 held it up over her head).
            Vector3 Heart() => q != null ? ChestOf(q) + Vector3.up * 0.05f : floor + Vector3.up;
            // How far into her charge she is, in game time, 1 once it is over (R88: timed by her stage, so a slowed
            // film shows it in step with her).
            bool Charging() => QueenCharging(q);
            float Charge() => Charging() ? Mathf.Clamp01(q.SkillStageTime / windup) : 1f;
            ShockGather(q, Charging, Charge, Heart, floor);
            GatherChess(Charging, Charge, () => q != null ? Flat(q.Hips.position) + Vector3.up * floor.y : floor, Heart);
        }

        /// <summary>
        /// The queen's chess pieces gathering (R88; R90 design A): eight of the six kinds pop up standing on the floor
        /// round her at about her inner reach, at uneven angles (not a compass), and spiral in 1.25 turns, standing and
        /// spinning like tops (R81's "회전하면서"), easing in, up into <paramref name="heart"/>, shrinking to 60% and
        /// heating up as they come (fire glowing in their creases, a hot edge); a short inked gold trail behind each shows
        /// the swirl. R142: an electric arc leaps from each to her chest every 0.08–0.16 s once it is a fifth of the way in.
        /// Timed by <paramref name="charge"/> (her charge, 0..1 in game time), gone if the charge is called off.
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
                float nextArc = Random.Range(0.06f, 0.2f);
                var piece = Add(new Puff(root, "Queen gathering chess piece", chessMeshes[kind], ChessMaterial(i))
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
                        // R138: no fire: polished, catching a little more light as they come in.
                        p.heat = 0f;
                        p.rim = Hdr(board ? BQueen.light : Gold, 0.6f * k * k);
                        // R142: charged: an arc leaps from it to her chest every 0.08–0.16 s once a fifth of the way in.
                        if (arc != null && k > 0.2f && age >= nextArc)
                        {
                            nextArc = age + Random.Range(0.08f, 0.16f);
                            arc.Strike(p.at, heart() + Random.insideUnitSphere * 0.06f, ArcGold, 0.13f, 0.08f, 0.6f + 0.4f * k, 0.18f, 0);
                        }
                    },
                });
                if (kit != null) InkTrail(() => piece.Alive ? piece.at : (Vector3?)null, kit.queen, 0.1f, 0.07f);
            }
        }

        void QueenBlast(SkillFxEvent e)
        {
            Vector3 c = Ground(e.at);
            float inner = Params != null ? Params.queenInner : 1.5f, outer = e.size > 0f ? e.size : 3f;
            // R138: the air goes out of her as a shock wave ("펑"), not a fireball: the dome and the floor rings of bent air
            // and the swept chess pieces (PawnRushSkillFx.Shock.cs), the crown ring of pearls riding the front. She springs
            // open. R139: no dust anywhere in it; the shock wave easier to see.
            if (e.by != null) kit.AddSquash(e.by, Kit.SquashKind.Spring);
            ShockBlast(c, outer, e.by);
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
            kit.FlashBody(e.target, kit.queen.main);
            kit.Drops(at, away + Vector3.up * 0.25f, kit.queen, 3, 6f);
            // R139: a piece she knocks down throws up no dust ("퀸이 팔방 밀치기 사용 할 때 먼지 vfx 지워주고").
            if (e.count == 1) kit.Down(e.target, kit.queen, e.at, Square, false);
            // R142: her shock is charged ("전기+공기 충격파"): its electricity runs over the piece, R143 for 0.6 s (a third of a
            // second went by unseen in play).
            ArcThrough(e.target, 0.6f);
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
            anims.Add(new Streak(root, matTrail, board ? Hdr(BOrange.main, 1.05f) : Hdr(Fire, 1.25f), 0.75f, 0.2f, Where) { paint = board ? 0.85f : 0.8f });
            anims.Add(new Streak(root, matTrail, board ? Hdr(new Color(0.92f, 0.95f, 1f), 2.2f) : Hdr(Volt, 3.4f), 0.16f, 0.32f, Where) { paint = 0.25f });
            anims.Add(new PathBolts(root, matVolt, Chest, 1.6f));
            Vector3 heading = Flat(r.transform.forward).sqrMagnitude > 0.01f ? Flat(r.transform.forward).normalized : Vector3.forward;
            float endedAt = -1f;
            // The spindle of light ahead of it.
            Add(new Beam(root, matFlare)
            {
                life = 5f,
                color = board ? Hdr(BOrange.main, 1.15f) : Hdr(Fire, 1.4f),
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
                            Random.Range(0.35f, 0.55f), Random.Range(0.5f, 0.8f), (Color32)(board ? new Color(0.92f, 0.84f, 0.74f) : Dust), Random.Range(-60f, 60f));
                return true;
            }));
        }

        void RookHit(SkillFxEvent e)
        {
            int i = Mathf.Clamp(e.count, 1, 3);
            var p = kit.rook;
            Vector3 fly = RookFlight(e);
            Vector3 at = e.target != null ? ChestOf(e.target) : e.at;
            // R138 (승규 님: "모든 기물이 스킬 사용 후 적 맞출 때 나오는 반원 이펙트인데 빼주고"): no C half ring.
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
            var p = kit.rook;
            Vector3 along = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Vector3 at = e.target != null ? ChestOf(e.target) : e.at;
            // R138: no C half ring.
            InkHit(e.target, at, along, p, 5, 0.12f, 8, 4);
            anims.Add(new Bolts(root, matVolt, 6, () => e.at, Hdr(Volt, 2f), 1f, 0.16f) { toon = true });
            kit.PuffBurst(Ground(e.at) + Vector3.up * 0.15f, kit.dust, 4, 0.5f, 0.25f);
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
        // R138 (승규 님: "밧줄에 걸려서 넘어 지는 게 아니라 밧줄에 묶이게 해줘 (실제 밧줄에 묶이는게 잘 보여야 돼)"): the catch ties
        // the piece up: the wire's lines close on its waist from all four pegs and a rope winds round it
        // (PawnRushSkillFx.Rope.cs); no crescent, ring or fall.
        // R142 (승규 님: "밧줄 지탱하는 곳을 좀 더 사실 적으로 묘사해줘"): the rope is tied with clove hitches to wooden stakes
        // driven into the floor as the X lands (PawnRushSkillFx.Stake.cs), not to grey cylinders.

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
                {
                    // R149: each square on the floor under it (up or down a step within 1 m), as the wire's pegs: laid at
                    // the middle's height, a square past a mini-game dais edge was inside the dais.
                    Vector3 at = center + (r * j + f * i) * size;
                    if (Kit.FloorUnder(at, 1f, out Vector3 floor) && Mathf.Abs(floor.y - at.y) <= 1f) at = floor;
                    yield return (at + Vector3.up * 0.02f, Mathf.Abs(i), size, rotation);
                }
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
            var p = kit.bishop;
            Vector3 run = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            if (e.source != null && tileSets.TryGetValue(e.source, out var set)) set.Pop(e.at);
            Vector3 waist = e.target != null ? SkillTripwire.Waist(e.target) : e.at + Vector3.up * 0.5f;
            // R138: the piece is tied up (e.size = how long; 0 = it could not be: down, or just up and only staggered).
            InkHit(e.target, waist, run, p, 4, 0.08f, 6, 0);
            if (e.size > 0f) TieUp(e, e.size);
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
        // R138 (승규 님: "나이트 스킬 쓸 때 엉덩이 부분에 흰색을 빼주고 좀 더 나이트다! 라는 연출을 보여줘. 그리고 착지 할 때 지금
        // 있는 원 모양 말고 마지막 사진 같은 로고로 해줘"): no wind ribbon from its hips, no wind puffs, swooshes, white rings
        // or crescents. It is a horse: it rears before it springs and flies in a jumping horse's pose (RagdollPawn's skill
        // pose); a ghost of a chess knight rears up behind it and lunges after it as it springs, its hind hooves leave their
        // prints and kick dust back; the kick off the air stamps a horseshoe badge on the air; the landing mark is the
        // horseshoe badge with an arrow over it, stamped when it comes down with dust and its front hooves' prints; a head it
        // stamps on gets the badge. PawnRushSkillFx.Knight.cs.
        // R139 (승규 님: "뒤에 말 같은 거 지워주고 뭘 생성은 하지마 그냥 vfx만 다른 연출로 … 테두리는 광택나는 검은 색 안에는 광택나는
        // 어두운 금색으로 … 머리를 밟을 때나 꺾을 때 하지마 착지 할 때만 … 밟았다! 라는 느낌으로"): no chess knight rising behind
        // it (nothing is made, only effects, and nothing round): its hooves kick back force lines (brush strokes of black
        // lacquer with a dark gold core); the badge glossy black and dark gold, and only where it lands; the stomp slams the
        // same strokes down onto the head and two shoot out sideways as it flattens.
        // R140 (승규 님: "나이트 위에 사진 좀 어색해 자연스러운 걸로 바꿔줘"): the stomp is told by the bodies, with no strokes and no
        // black flash: the head squashed flat, dust squeezed out from under the hooves and round its feet, the usual hit flash.
        // R141 (승규 님, Pawn Rush and Queen of the Hill both: "f 키를 누르면 뒤에(엉덩이 부분)에 임펙트가 나가는데 다른 임펙트로
        // 해줘(공기 임팩트)"): no force lines: the air its spring shoves back from behind its hips (SkillAir: bent air and white
        // vapour), at the take-off and where it kicks off the air.

        /// <summary>"다": the leap's take-off. R138: the hind hooves' prints where it pushed off and dust kicked back off them.
        /// R141: and the air its spring shoves back and down from behind its hips (in place of R139's four force lines);
        /// off the air (R81) the air it kicks back.</summary>
        void KnightTakeOff(SkillFxEvent e)
        {
            var k = e.by;
            Vector3 floor = Ground(e.at + Vector3.up * 0.3f);
            if (k != null) leapPaths[k] = new LeapPath { start = floor, launchedAt = Clock };
            Shake(0.04f, 4f * Frame);
            Vector3 dir = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : k != null ? Kit.FlatDir(k.Facing, Vector3.forward) : Vector3.forward;
            bool fromFloor = e.count != 1;
            if (fromFloor)
            {
                Hoofprints(floor, dir, -0.2f, 1.3f);
                HoofDust(floor, -dir, 0.8f);
                AirKick(k != null ? k.Hips.position : floor + Vector3.up * 0.55f, dir, 1f, 0.3f);
            }
            else AirKick(k != null ? k.Hips.position : e.at, dir, 0.85f, 0.2f);
        }

        /// <summary>"당" (R80): the second press kicks off the air, turned or straight on: a two-frame pause. R141: the air it
        /// kicks back off (R139's three force lines; the badge it stamped on the air in R138 is gone: "꺾을 때 하지마"; its
        /// way's squares keep the corner: an L).</summary>
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
            AirKick(k != null ? k.Hips.position : e.at, Flat(d).sqrMagnitude > 1e-4f ? Flat(d).normalized : Vector3.forward, 0.85f, 0.15f);
        }

        void KnightHome(SkillFxEvent e)
        {
            // Homing onto a head: its wind ribbon runs on; the stomp is the moment (A).
            if (e.by != null && leapPaths.TryGetValue(e.by, out var path)) path.launchedAt = Clock;
        }

        void KnightStomp(SkillFxEvent e)
        {
            var p = kit.knight;
            // A: flat in four frames (to 60%), three drops down and out, the body white then sky, a 5-frame stop; then three
            // little cream pawns over its head. R138: no ring at head height: the horseshoe badge stamped on its head.
            // R139 ("밟을 때 나오는 vfx도 다른 그래픽 디자인으로 만들어줘 밟았다! 라는 느낌으로"; no badge on a head): no drops, a
            // 6-frame stop and a harder shake, flat a little longer. R140 ("어색해 자연스러운 걸로"): R139's force lines slamming
            // down and white-then-black flash gone; dust squeezed out from under the hooves and round its feet
            // (StompImpact), the body white then the knight's colour as any hit.
            kit.AddSquash(e.target, Kit.SquashKind.Flatten, 0.6f);
            StompImpact(e.target, e.at, Flat(e.dir));
            InkHit(e.target, e.at, Vector3.down, p, 6, 0.15f, 9, 0);
            kit.Down(e.target, p, e.at, Square);
            // R153 (승규 님 10-22: "머리 빙빙 돌리게 하는 거 지워줘"): no little pawns circling its head any more.
        }

        void KnightLand(SkillFxEvent e)
        {
            var k = e.by;
            if (k != null) leapPaths.Remove(k);
            // R139: the floor under it, past any piece lying there (Ground stops at a piece and gives the knight's feet: the
            // badge stamped beside a head it had stomped floated over the fallen piece).
            Vector3 g = Kit.FloorUnder(e.at, 0.8f) + Vector3.up * 0.02f;
            float radius = e.size > 0f ? e.size : 1.5f;
            var p = kit.knight;
            // R138: the horseshoe badge it was coming down on stamps (white for two frames, a little pop) and fades over a
            // second; dust rolls out to the stagger reach; its front hooves' two prints just ahead. Each piece it staggers
            // flashes, with a 3-frame stop. R139: the badge glossy black and dark gold with its sheen sweeping over it, and
            // less dust (nothing round: "착지 할 때 지금 있는 원 모양 말고").
            Vector3 along = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            kit.DustRing(g, 5, radius * 0.85f, kit.dust, 0.26f, 0.55f, 10f);
            BadgeStamp(g, along, k);
            Hoofprints(g, along, 0.45f, 1.5f);
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
            /// <summary>R139: the lines as hemp rope (design A and the board look), and a knot round each peg.</summary>
            readonly List<RopeSpan> ropes = new List<RopeSpan>();
            readonly List<SkillInkKit.Prop> knots = new List<SkillInkKit.Prop>();
            /// <summary>R142: the wooden stakes the rope is tied to (SkillTripwire's own pegs hidden), and how high it is tied.</summary>
            readonly List<SkillInkKit.Prop> stakes = new List<SkillInkKit.Prop>();
            float stakeTie;
            /// <summary>The board look's pads (R125): one per square.</summary>
            readonly List<SkillInkKit.Tile> pads = new List<SkillInkKit.Tile>();
            Vector3 along = Vector3.forward;
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
                    if (fx.board && !ghost) pads.Add(new SkillInkKit.Tile(fx.kit, "Bishop board square"));
                }
                Place(center, forward, length);
            }

            public void Place(Vector3 center, Vector3 forward, float length)
            {
                along = Flat(forward).sqrMagnitude > 1e-4f ? Flat(forward).normalized : Vector3.forward;
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
            /// pulled (R81); placed from the wire every frame. R139: with design A's kit, as the tie's hemp rope instead
            /// (승규 님: "비숍의 x자 줄도 실제 밧줄 모양으로 바꿔줘 (상대 묶는 밧줄이랑 동일)"), knotted round the pegs. R142: the
            /// pegs are wooden stakes, the knots clove hitches (PawnRushSkillFx.Stake.cs).</summary>
            public void AddWires(Vector3 center, Vector3 forward, float length, float height)
            {
                if (fx.kit != null)
                {
                    stakeTie = height;
                    for (int i = 0; i < 4; i++)
                    {
                        ropes.Add(new RopeSpan(fx, "Bishop rope"));
                        var knot = fx.RopeProp(fx.RopeKnotMesh(), "Bishop rope hitch", 0.006f);
                        knot.Show(false);
                        knots.Add(knot);
                        var stake = fx.StakeProp(height, "Bishop stake");
                        stake.Show(false);
                        stakes.Add(stake);
                    }
                    return;
                }
                for (int i = 0; i < 4; i++)
                    wires.Add(new Shape(fx.root, "Bishop wire light", fx.meshCylinder, fx.board ? fx.matRodBoard : fx.matRod) { life = float.MaxValue, paint = 0.3f });
            }

            /// <summary>
            /// The rope lines this frame (R139): each half from its peg to where the line's middle is now, hanging 3.5 cm
            /// at the middle while slack and straight once pulled or tied; the second line lies over the first where they
            /// cross (its middle a rope's thickness higher); the knots on the pegs' tops. R142: each peg a wooden stake
            /// leaning out a little from the X's middle (and in with the line's pull), driven in as the X lands; the hitch
            /// on it turned to its line, the line leaving it from the stake's side.
            /// </summary>
            void StepRopes(SkillTripwire tripwire, float fade)
            {
                if (tripwire == null)
                {
                    foreach (var r in ropes) r.Place(Vector3.zero, Vector3.zero, 0f, 0f);
                    foreach (var k in knots) k.Show(false);
                    foreach (var s in stakes) s.Show(false);
                    return;
                }
                // Its own thin lines and pegs stay hidden while these show (its pegs still lean).
                tripwire.LinesDrawnElsewhere = true;
                tripwire.PegsDrawnElsewhere = stakes.Count > 0;
                float shown = hidden ? 0f : fade;
                // Driven in as the X lands: falling faster and faster, into the floor in three frames.
                float driven = Mathf.Clamp01(age / (StakeDriveFrames * Frame));
                Vector3 drive = Vector3.up * (StakeDrive * (1f - driven * driven));
                Vector3 middle = tripwire.Center;
                for (int line = 0; line < 2; line++)
                {
                    tripwire.LinePoints(line, out _, out var mid, out _);
                    float taut = Mathf.Clamp01(tripwire.Tension(line) * 3f);
                    bool tied = tripwire.BoundTo(line) != null;
                    float sag = tied ? 0f : 0.035f * (1f - taut);
                    if (!tied) mid += Vector3.up * (line == 1 ? RopeThick * (1f - taut) : 0f);
                    for (int end = 0; end < 2; end++)
                    {
                        int i = line * 2 + end;
                        // The stake: leaning out, and in with the line's pull (the peg's lean).
                        tripwire.PegTie(line, end, out var pull);
                        Vector3 foot = tripwire.PegFoot(line, end) + drive;
                        Quaternion stand = StakeStand(foot, middle, pull);
                        Vector3 tie = foot + stand * (Vector3.up * stakeTie);
                        if (i < stakes.Count)
                        {
                            var stake = stakes[i];
                            stake.t.SetPositionAndRotation(foot, stand * Quaternion.Euler(0f, StakeYaw(i), 0f));
                            stake.fade = shown;
                            stake.Show(shown > 0.02f);
                            stake.Apply();
                        }
                        // The hitch turned to its line; the line leaves it from the stake's side.
                        Vector3 toLine = mid - tie;
                        toLine.y = 0f;
                        if (toLine.sqrMagnitude < 1e-6f) toLine = new Vector3(middle.x - foot.x, 0f, middle.z - foot.z);
                        Quaternion hitch = stand * Quaternion.LookRotation(toLine.sqrMagnitude > 1e-8f ? toLine.normalized : Vector3.forward, Vector3.up);
                        ropes[i].Place(tie + hitch * HitchExit, mid, sag, shown);
                        var knot = knots[i];
                        knot.t.SetPositionAndRotation(tie, hitch);
                        knot.fade = shown;
                        knot.Show(shown > 0.02f);
                        knot.Apply();
                    }
                }
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
                    if (pads.Count > 0)
                    {
                        // The board look: a dark board square standing as a pad, lifted and turned orange as it pops (the
                        // box of light did that before), a silver band inside it lit with the wire's level.
                        tile.bright = 0f;
                        var pad = pads[i];
                        float lift = 0.22f * Mathf.Sin(Mathf.Clamp01(pop) * Mathf.PI);
                        pad.Floor(at + Vector3.up * (0.004f + lift), along, Vector2.one * (square * 0.94f));
                        pad.shape = 0f;
                        pad.round = 0.05f;
                        pad.fill = SkillInkKit.A(Color.Lerp(BSquare.main, BOrange.main, pop), 0.94f);
                        pad.bevel = SkillInkKit.A(Color.Lerp(BSquare.light, BOrange.deep, pop), 0.8f);
                        pad.bevelWidth = 0.05f;
                        pad.ink = SkillInkKit.A(BNavy, 1f);
                        pad.inkWidth = 0.03f;
                        fx.kit.BoardSide(pad, owner);
                        pad.core = SkillInkKit.A(BSilver.light, Mathf.Clamp01((level - 0.45f) / 0.55f));
                        pad.coreWidth = 0.05f;
                        pad.thick = 0.045f + 0.05f * pop;
                        pad.fade = hidden ? 0f : on * fade * Mathf.Clamp01(0.8f + 0.3f * level);
                        pad.Apply();
                    }
                    tile.Step(dt, cam);
                    if (box != null && pads.Count > 0)
                    {
                        box.bright = 0f;
                        box.Step(dt, cam);
                    }
                    else if (box != null)
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
                if (ropes.Count > 0) StepRopes(tripwire, fade);
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
                    if (fx.board)
                    {
                        // The board look: a polished silver wire (Skill Gloss: rgb tints, alpha fades by a dither).
                        wire.color = Color.white;
                        wire.bright = fade;
                    }
                    wire.Step(dt, cam);
                }
                if (!ghost && !hidden && squares.Count > 0 && !fx.board)
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
                foreach (var r in ropes) r.Destroy();
                foreach (var k in knots) k.Destroy();
                foreach (var s in stakes) s.Destroy();
                foreach (var pad in pads) pad.Destroy();
                pads.Clear();
                squares.Clear();
                wires.Clear();
                ropes.Clear();
                knots.Clear();
                stakes.Clear();
            }
        }

        // ---------------------------------------------------------------- shared parts

        void HitStop(float seconds)
        {
            if (!hitStop || seconds <= 0f) return;
            if ((game != null && game.NetworkControlled) || OnlineOverride)
            {
                // Online (R111, D-S2) the game must not stop (the host's physics, everyone's inputs): only the hitter's and
                // the hit piece's picture holds still, on this screen by itself.
                if (stopBy != null) stopBy.HoldVisual(seconds);
                if (stopTarget != null) stopTarget.HoldVisual(seconds);
                if (LabNetLink.Trace) LabNetLink.Log($"hit stop{seconds * 60f:0} frames on {(stopBy != null ? stopBy.DisplayName : "-")} and {(stopTarget != null ? stopTarget.DisplayName : "-")} (this screen only)");
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
            air?.Step(dt);
            arc?.Step(dt, cam);

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
            if (shader == null) shader = Shader.Find("Sprites/Default");   // R111: the two above are not in a player build
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
