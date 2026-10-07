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
    /// out over the floor, beams, shells and magic circles on meshes, sparks that bounce off the floor, motes and
    /// smoke as particles, point lights that light the floor and the pieces, trails, lightning, all drawn brighter
    /// than white for the camera's bloom (<see cref="PawnRushSkillBloom"/>). Parts: PawnRushSkillFx.Parts.cs.
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
        static readonly Color FireCore = new Color(1f, 0.8f, 0.45f);
        static readonly Color Violet = new Color(0.6f, 0.3f, 1f);
        static readonly Color Magenta = new Color(1f, 0.32f, 0.85f);
        static readonly Color Sky = new Color(0.25f, 0.7f, 1f);
        static readonly Color SkyCore = new Color(0.75f, 0.95f, 1f);
        static readonly Color Dust = new Color(0.72f, 0.66f, 0.58f, 1f);   // a little darker than the floors it rolls over

        static Color Hdr(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        LabGame game;
        Transform root;
        Font font;
        Material lineMat, whiteMat;
        Material matWall, matPillar, matShell, matRing, matCircle, matGround, matHalo, matTile, matCurtain, matTrail, matBolt, matRod;
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
            matWall = Glow(texBeam, noise: 0.65f, noiseST: new Vector4(4f, 1f, 0.05f, -1.4f), rim: -0.3f, soft: 0.25f);
            matPillar = Glow(texBeam, noise: 0.5f, noiseST: new Vector4(2f, 0.7f, 0f, -2.6f), rim: -0.8f, rimPower: 1.4f, soft: 0.2f);
            matShell = Glow(white, noise: 0.4f, noiseST: new Vector4(3f, 2f, 0.4f, 0.7f), rim: 0.9f, rimPower: 2.2f);
            matRing = Glow(texRing, noise: 0.3f, noiseST: new Vector4(2f, 2f, 0.3f, 0.15f));
            matCircle = Glow(texCircle, opacity: 0.25f);
            matGround = Glow(texSoft, opacity: 0.3f);
            matHalo = Glow(texSoft);
            matTile = Glow(texTile, opacity: 0.4f, noise: 0.25f, noiseST: new Vector4(1f, 1f, 0.1f, 0.35f));
            matCurtain = Glow(texBeam, noise: 0.55f, noiseST: new Vector4(2f, 1f, 0f, -1.1f), soft: 0.08f);
            matTrail = Glow(texBand, noise: 0.35f, noiseST: new Vector4(3f, 1f, -2.5f, 0f));
            matBolt = Glow(texBand);
            matRod = Glow(white, noise: 0.5f, noiseST: new Vector4(1f, 6f, 0f, -3f), rim: -0.7f, rimPower: 1.2f);
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

        // ---- Queen A: a gold magic circle and light pulled in while she charges; then a pillar of light, a wall of
        // light out to 1.5 m (knocked down) and a thinner one out to 3 m (pushed), sparks, smoke, a flash that
        // lights the floor; the screen shakes and its edges split red and cyan.

        void QueenCharge(RagdollPawn q)
        {
            float windup = Params != null ? Params.queenWindup : 0.35f;
            float inner = Params != null ? Params.queenInner : 1.5f;
            var flat = FlatOnFloor();
            Add(new Shape(root, "Queen circle", meshQuad, matCircle)
            {
                life = windup + 0.35f,
                color = Hdr(Gold, 1.25f),
                paint = 0.9f,
                animate = (s, t) =>
                {
                    float age = s.Age;
                    if (q != null) s.at = Ground(q.Hips.position) + Vector3.up * 0.03f;
                    s.rotation = flat * Quaternion.Euler(0f, 0f, age * 140f);
                    s.scale = Vector3.one * inner * 2.1f * EaseOut(age / 0.2f);
                    s.bright = age < windup ? 0.7f + 0.3f * (age / windup) : 1f - (age - windup) / 0.35f;
                },
            });
            Add(new Shape(root, "Queen aura", meshQuad, matHalo)
            {
                life = windup + 0.08f,
                billboard = true,
                color = Hdr(Gold, 1.2f),
                paint = 0.5f,
                animate = (s, t) =>
                {
                    if (q != null) s.at = ChestOf(q);
                    s.scale = Vector3.one * (0.4f + 0.9f * t);
                    s.bright = t;
                },
            });
            float pull = 0f, rise = 0f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!QueenCharging(q)) return false;
                Vector3 feet = Ground(q.Hips.position), chest = ChestOf(q);
                // light drawn in from around her
                for (pull += dt * 110f; pull >= 1f; pull -= 1f)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(1.8f, 2.8f);
                    var from = feet + new Vector3(Mathf.Cos(a) * r, Random.Range(0.1f, 1.4f), Mathf.Sin(a) * r);
                    float life = Random.Range(0.22f, 0.32f);
                    motes.Emit(from, (chest - from) / life, Random.Range(0.06f, 0.1f), life, Tint(GoldDeep, Random.value * 0.3f));
                }
                // motes rising off the circle's edge
                for (rise += dt * 50f; rise >= 1f; rise -= 1f)
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    var from = feet + new Vector3(Mathf.Cos(a) * inner, 0.05f, Mathf.Sin(a) * inner);
                    motes.Emit(from, Vector3.up * Random.Range(0.8f, 1.8f), Random.Range(0.06f, 0.11f), Random.Range(0.5f, 0.9f), Tint(GoldDeep, Random.value * 0.3f));
                }
                return true;
            }));
        }

        void QueenBlast(SkillFxEvent e)
        {
            Vector3 c = Ground(e.at);
            float inner = Params != null ? Params.queenInner : 1.5f, outer = e.size > 0f ? e.size : 3f;
            Flare(c + Vector3.up * 1.2f, Gold, 1.2f, outer * 2.6f, 0.55f);
            Halo(c + Vector3.up * 1f, 1.8f, Gold, 0.25f);
            Shell(c + Vector3.up * 0.9f, 0.2f, 1.2f, Gold, 0.28f);
            Pillar(c, 0.45f, 5.5f, Gold, 0.55f);
            Wall(c, 0.2f, inner, 0.18f, 1.1f, 0.35f, GoldDeep, 0.55f, 0.85f);
            Wall(c, 0.3f, outer, 0.36f, 0.6f, 0.15f, GoldDeep, 0.85f, 0.55f);
            GroundRing(c, 0.2f, inner, 0.18f, Gold, 0.6f, 2.6f);
            GroundRing(c, 0.3f, outer, 0.36f, Gold, 0.9f, 2.2f);
            GroundGlow(c, inner * 0.9f, GoldDeep, 0.6f);
            for (int i = 0; i < 40; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Cos(a), Random.Range(0.05f, 0.5f), Mathf.Sin(a)).normalized;
                sparks.Emit(c + Vector3.up * 0.4f + dir * 0.3f, dir * Random.Range(6f, 12f), Random.Range(0.05f, 0.09f), Random.Range(0.4f, 0.75f), Tint(Gold, Random.value * 0.4f));
            }
            MoteBurst(c + Vector3.up * 0.3f, 40, GoldDeep, inner, 1.6f, 1.2f, 1.4f);
            DustRing(c, 18, 0.6f, 4.5f, Dust, 0.8f, 0.9f);
            HitStop(0.06f);
            Shake(0.13f, 0.28f);
            Edge(0.22f);
        }

        void QueenHit(SkillFxEvent e)
        {
            FlashWhite(e.target, 0.07f);
            Vector3 at = e.target != null ? ChestOf(e.target) : e.at + Vector3.up * 0.3f;
            Vector3 away = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.up;
            SparkBurst(at, 16, Gold, away + Vector3.up * 0.4f, 65f, 3f, 7f, 0.45f, 0.05f);
            Shell(at, 0.1f, 0.65f, Gold, 0.18f);
            Halo(at, 1.3f, Gold, 0.2f);
        }

        // ---- Rook A: a fire trail and a shield of light ahead while it charges; each piece it sends flying gives a
        // shell of light, a ring that rushes out along its line, sparks and a flash, each bigger and with a longer stop
        // (0.04, 0.06, 0.08 s) and shake than the one before; the fourth, which stops it, a wall of fire on the floor.

        void RookCharge(RagdollPawn r)
        {
            Vector3? Where() => RookDashing(r) ? r.Hips.position : (Vector3?)null;
            anims.Add(new Streak(root, matTrail, Hdr(Fire, 1.2f), 0.95f, 0.28f, Where) { paint = 0.8f });
            anims.Add(new Streak(root, matTrail, Hdr(FireCore, 3f), 0.3f, 0.16f, Where) { paint = 0.3f });
            Vector3 heading = Flat(r.transform.forward).sqrMagnitude > 0.01f ? Flat(r.transform.forward).normalized : Vector3.forward;
            float endedAt = -1f;
            Add(new Shape(root, "Rook bow", meshSphere, matShell)
            {
                life = 5f,
                color = Hdr(Fire, 1.6f),
                paint = 0.6f,
                animate = (s, t) =>
                {
                    float age = s.Age;
                    if (endedAt < 0f && !RookDashing(r)) endedAt = age;
                    if (endedAt >= 0f && age - endedAt > 0.14f) { s.life = age; return; }
                    if (r != null)
                    {
                        Vector3 v = Flat(r.Hips.linearVelocity);
                        if (v.sqrMagnitude > 1f) heading = v.normalized;
                        s.at = ChestOf(r) + heading * 0.5f;
                    }
                    s.rotation = Quaternion.LookRotation(heading);
                    s.scale = new Vector3(1.25f, 1.7f, 0.85f) * Mathf.Min(1f, age / 0.06f);
                    s.bright = (0.8f + 0.2f * Mathf.Sin(age * 47f)) * (endedAt < 0f ? 1f : 1f - (age - endedAt) / 0.14f);
                },
            });
            float flame = 0f, kick = 0f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!RookDashing(r)) return false;
                Vector3 chest = ChestOf(r), back = -heading;
                for (flame += dt * 140f; flame >= 1f; flame -= 1f)
                    motes.Emit(chest + Random.insideUnitSphere * 0.35f, back * Random.Range(2f, 5f) + Random.insideUnitSphere * 0.8f,
                        Random.Range(0.07f, 0.12f), Random.Range(0.2f, 0.35f), Tint(Fire, Random.value * 0.3f));
                for (kick += dt * 30f; kick >= 1f; kick -= 1f)
                    smoke.Emit(Ground(r.Hips.position) + back * 0.3f + Random.insideUnitSphere * 0.2f, back * Random.Range(0.5f, 1.5f) + Vector3.up * 0.4f,
                        Random.Range(0.35f, 0.55f), Random.Range(0.5f, 0.8f), (Color32)Dust, Random.Range(-60f, 60f));
                return true;
            }));
        }

        void RookHit(SkillFxEvent e)
        {
            int i = Mathf.Clamp(e.count, 1, 3);
            HitStop(new[] { 0.04f, 0.06f, 0.08f }[i - 1]);
            Shake(new[] { 0.05f, 0.08f, 0.11f }[i - 1], 0.16f + 0.03f * i);
            FlashWhite(e.target, 0.06f);
            Vector3 d = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Shell(e.at, 0.15f, 0.55f + 0.2f * i, Fire, 0.18f);
            SonicRing(e.at, d, 0.2f, 1f + 0.35f * i, Fire, 0.26f);
            SparkBurst(e.at, 18 + 8 * i, FireCore, d + Vector3.up * 0.3f, 55f, 4f, 9f + 2f * i, 0.5f, 0.06f);
            SparkBurst(e.at, 6, FireCore, -d, 60f, 2f, 4f, 0.35f, 0.05f);
            Halo(e.at, 1.4f + 0.3f * i, Fire, 0.18f);
            Flare(e.at, Fire, 0.8f + 0.4f * i, 6f, 0.3f);
        }

        void RookStop(SkillFxEvent e)
        {
            HitStop(0.09f);
            Shake(0.17f, 0.32f);
            FlashWhite(e.target, 0.07f);
            Vector3 d = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Vector3 g = Ground(e.at);
            Shell(e.at, 0.2f, 1.2f, Fire, 0.22f);
            SonicRing(e.at, d, 0.3f, 2.2f, Fire, 0.3f);
            SparkBurst(e.at, 40, FireCore, d + Vector3.up * 0.4f, 70f, 4f, 11f, 0.6f, 0.07f);
            Wall(g, 0.3f, 1.3f, 0.2f, 0.7f, 0.15f, Fire, 0.5f, 0.85f);
            GroundRing(g, 0.3f, 1.3f, 0.2f, Fire, 0.55f, 2.6f);
            DustRing(g, 18, 0.4f, 3.5f, Dust, 0.75f, 0.9f);
            Halo(e.at, 2.2f, Fire, 0.24f);
            Flare(e.at, Fire, 1.8f, 7f, 0.4f);
        }

        /// <summary>Into a wall (or, smaller, a barricade): the hardest thud of the rook's (R75: a clear shake).</summary>
        void RookWall(SkillFxEvent e, float k)
        {
            HitStop(0.08f * k);
            Shake(0.25f * k, 0.4f);
            Vector3 d = Flat(e.dir).sqrMagnitude > 1e-4f ? Flat(e.dir).normalized : Vector3.forward;
            Vector3 at = e.at - d * 0.1f + Vector3.up * (k < 1f ? 0.5f : 0f);
            Shell(at, 0.2f, 1.1f * k, Fire, 0.22f);
            SonicRing(at, -d, 0.3f, 2.4f * k, Fire, 0.3f);
            SparkBurst(at, Mathf.RoundToInt(45 * k), FireCore, -d + Vector3.up * 0.25f, 80f, 4f, 11f, 0.6f, 0.07f);
            DustRing(Ground(at), Mathf.RoundToInt(12 * k), 0.3f, 2.5f, Dust, 0.7f, 0.8f);
            Halo(at, 2.4f * k, Fire, 0.22f);
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
            Pillar(at, square * 0.42f, 2.4f, Magenta, 0.45f);
            SparkBurst(at + Vector3.up * 0.2f, 18, Magenta, Vector3.up, 50f, 2f, 6f, 0.5f, 0.05f);
            MoteBurst(at, 26, Violet, square * 0.4f, 2.6f, 0.6f, 1f);
            Halo(at + Vector3.up * 0.4f, 1.6f, Violet, 0.22f);
            if (e.target != null) Shell(ChestOf(e.target), 0.1f, 0.6f, Magenta, 0.18f);
            Flare(at + Vector3.up * 0.6f, Violet, 1.4f, 5f, 0.35f);
        }

        // ---- Knight B: a trail of light through the leap; a ring where it turns; homing in, crackling lightning;
        // the stomp throws a crown of light out from the head, a beam, lightning and sparks, and orbs of light circle
        // the dazed piece (no stars); a landing rolls dust and a low wall of light out to 1.5 m.

        void KnightLeap(RagdollPawn k)
        {
            Vector3? Where() => KnightLeaping(k) ? k.Hips.position : (Vector3?)null;
            anims.Add(new Streak(root, matTrail, Hdr(Sky, 1.2f), 0.55f, 0.3f, Where) { paint = 0.8f });
            anims.Add(new Streak(root, matTrail, Hdr(SkyCore, 3f), 0.18f, 0.2f, Where) { paint = 0.3f });
            float trail = 0f;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!KnightLeaping(k)) return false;
                Vector3 v = k.Hips.linearVelocity;
                for (trail += dt * 45f; trail >= 1f; trail -= 1f)
                    motes.Emit(k.Hips.position + Random.insideUnitSphere * 0.25f, -v * 0.15f + Random.insideUnitSphere * 0.4f,
                        Random.Range(0.05f, 0.09f), Random.Range(0.3f, 0.5f), Tint(Sky, Random.value * 0.3f));
                return true;
            }));
        }

        void KnightTurn(SkillFxEvent e)
        {
            Vector3 d = e.dir.sqrMagnitude > 1e-4f ? e.dir.normalized : Vector3.forward;
            SonicRing(e.at, d, 0.2f, 1.3f, Sky, 0.26f);
            SparkBurst(e.at, 14, SkyCore, -d, 60f, 2f, 5f, 0.4f, 0.05f);
            Halo(e.at, 1.3f, Sky, 0.2f);
        }

        void KnightHome(SkillFxEvent e)
        {
            var k = e.by;
            if (k == null) return;
            anims.Add(new Streak(root, matTrail, Hdr(SkyCore, 3f), 0.3f, 0.2f, () => KnightLeaping(k) ? k.Hips.position : (Vector3?)null) { paint = 0.3f });
            anims.Add(new Bolts(root, matBolt, 3, () => ChestOf(k), Hdr(SkyCore, 3.2f), 0.7f, 0.35f));
            Halo(ChestOf(k), 1.2f, Sky, 0.2f);
        }

        void KnightStomp(SkillFxEvent e)
        {
            HitStop(0.09f);
            Shake(0.1f, 0.2f);
            FlashWhite(e.target, 0.07f);
            StartSquash(e.target);
            Vector3 at = e.at;
            Wall(at - Vector3.up * 0.2f, 0.15f, 1.3f, 0.16f, 0.45f, 0.1f, Sky, 0.42f, 0.8f);
            Add(new Shape(root, "Knight strike", meshCylinder, matPillar)
            {
                life = 0.25f,
                at = at,
                color = Hdr(SkyCore, 3.2f),
                paint = 0.25f,
                animate = (s, t) =>
                {
                    float w = Mathf.Lerp(0.16f, 0.02f, t);
                    s.scale = new Vector3(w, 2.6f, w);
                    s.bright = 1f - t;
                },
            });
            anims.Add(new Bolts(root, matBolt, 6, () => at, Hdr(SkyCore, 3.2f), 1.1f, 0.35f));
            SparkBurst(at, 32, SkyCore, Vector3.up, 70f, 3f, 8f, 0.55f, 0.06f);
            Shell(at, 0.15f, 0.9f, Sky, 0.2f);
            Halo(at, 1.8f, Sky, 0.22f);
            if (e.target != null) GroundRing(Ground(e.target.Hips.position), 0.2f, 1.2f, 0.2f, Sky, 0.5f, 2.6f);
            Flare(at, Sky, 1.5f, 6f, 0.4f);
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
            Wall(g, 0.2f, radius, 0.2f, 0.35f, 0.08f, Sky, 0.45f, 0.7f);
            GroundRing(g, 0.2f, radius, 0.22f, Sky, 0.5f, 2.4f);
            DustRing(g, 14, 0.35f, 3.2f, Dust, 0.6f, 0.8f);
            Flare(g + Vector3.up * 0.5f, Sky, 0.9f, 4f, 0.3f);
        }

        /// <summary>Three orbs of light with tails circling over a dazed piece's head, and a faint ring (in place of
        /// the stars before).</summary>
        void Daze(RagdollPawn pawn, float delay, float life)
        {
            for (int i = 0; i < 3; i++)
            {
                float phase = i * Mathf.PI * 2f / 3f;
                float born = 0f;
                Vector3? Orb()
                {
                    if (pawn == null || born < delay || born > delay + life) return null;
                    float a = born * 6f + phase;
                    return HeadOf(pawn) + Vector3.up * 0.4f + new Vector3(Mathf.Cos(a) * 0.32f, Mathf.Sin(a * 2f) * 0.04f, Mathf.Sin(a) * 0.32f);
                }
                Add(new Shape(root, "Daze orb", meshQuad, matHalo)
                {
                    life = delay + life,
                    billboard = true,
                    color = Hdr(SkyCore, 2.6f),
                    paint = 0.5f,
                    animate = (s, t) =>
                    {
                        born = s.Age;
                        var p = Orb();
                        s.bright = p.HasValue ? Mathf.Clamp01((delay + life - born) / 0.3f) : 0f;
                        if (p.HasValue) s.at = p.Value;
                        s.scale = Vector3.one * 0.2f;
                    },
                });
                anims.Add(new Streak(root, matTrail, Hdr(Sky, 1.2f), 0.07f, 0.22f, Orb) { delay = delay + 0.02f, paint = 0.8f });
            }
            var flat = FlatOnFloor();
            Add(new Shape(root, "Daze ring", meshQuad, matRing)
            {
                life = delay + life,
                color = Hdr(Sky, 1.5f),
                paint = 0.8f,
                animate = (s, t) =>
                {
                    float age = s.Age;
                    s.bright = age < delay ? 0f : Mathf.Clamp01((delay + life - age) / 0.3f) * Mathf.Clamp01((age - delay) / 0.15f);
                    if (pawn != null) s.at = HeadOf(pawn) + Vector3.up * 0.4f;
                    s.rotation = flat * Quaternion.Euler(0f, 0f, age * 90f);
                    s.scale = Vector3.one * 0.85f;
                },
            });
        }

        // ---------------------------------------------------------------- effect pieces

        static Quaternion FlatOnFloor() => Quaternion.LookRotation(Vector3.down, Vector3.forward);

        static Vector3 ChestOf(RagdollPawn p) => p != null ? p.bodies[(int)BodyId.Chest].position : Vector3.zero;

        static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        /// <summary>A wall of light (an open cylinder) running out over the floor from <paramref name="from"/> to
        /// <paramref name="to"/> metres in <paramref name="grow"/> seconds, sinking from height h0 to h1. Its body
        /// covers the floor by <paramref name="paint"/>.</summary>
        void Wall(Vector3 c, float from, float to, float grow, float h0, float h1, Color color, float life, float paint)
        {
            Add(new Shape(root, "Wall of light", meshCylinder, matWall)
            {
                life = life,
                at = c,
                color = Hdr(color, 1.25f),
                paint = paint,
                animate = (s, t) =>
                {
                    float age = s.Age, r = Mathf.Lerp(from, to, EaseOut(age / grow));
                    s.scale = new Vector3(r, Mathf.Lerp(h0, h1, t), r);
                    float fade = age < grow ? 1f : 1f - (age - grow) / Mathf.Max(0.01f, life - grow);
                    s.bright = fade * fade;
                    s.dissolve = Mathf.Clamp01((t - 0.35f) / 0.65f) * 0.7f;
                },
            });
        }

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
        void GroundRing(Vector3 c, float from, float to, float grow, Color color, float life, float light)
        {
            var flat = FlatOnFloor();
            Add(new Shape(root, "Ring on the floor", meshQuad, matRing)
            {
                life = life,
                at = c + Vector3.up * 0.035f,
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

        /// <summary>A sphere of light that bursts out and fades, brightest at its edge.</summary>
        void Shell(Vector3 at, float from, float to, Color color, float life)
        {
            Add(new Shape(root, "Shell of light", meshSphere, matShell)
            {
                life = life,
                at = at,
                color = Hdr(color, 1.4f),
                paint = 0.6f,
                animate = (s, t) =>
                {
                    s.scale = Vector3.one * Mathf.Lerp(from, to, EaseOut(t)) * 2f;   // the sphere mesh is 1 across
                    s.bright = (1f - t) * (1f - t);
                    s.dissolve = t * 0.5f;
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

        /// <summary>Light pooled on the floor, tinting it.</summary>
        void GroundGlow(Vector3 c, float radius, Color color, float life)
        {
            var flat = FlatOnFloor();
            Add(new Shape(root, "Glow on the floor", meshQuad, matGround)
            {
                life = life,
                at = c + Vector3.up * 0.025f,
                rotation = flat,
                color = Hdr(color, 1.1f),
                paint = 0.7f,
                animate = (s, t) =>
                {
                    s.scale = Vector3.one * radius * 2f * EaseOut(t * 5f);
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

            /// <summary>The two lines of the wire as thin beams of light at <paramref name="height"/>.</summary>
            public void AddWires(Vector3 center, Vector3 forward, float length, float height)
            {
                forward.y = 0f;
                forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
                foreach (float turn in new[] { 45f, -45f })
                {
                    Vector3 d = Quaternion.Euler(0f, turn, 0f) * forward, a = center - d * length * 0.5f + Vector3.up * height;
                    wires.Add(new Shape(fx.root, "Bishop wire light", fx.meshCylinder, fx.matRod)
                    {
                        life = float.MaxValue,
                        at = a,
                        rotation = Quaternion.FromToRotation(Vector3.up, d),
                        scale = new Vector3(0.03f, length, 0.03f),
                        paint = 0.3f,
                    });
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
                    tile.color = Hdr(Color.Lerp(Violet, Magenta, pop), 1.5f + 2.5f * pop);
                    tile.bright = bright;
                    tile.Step(dt, cam);
                    if (box != null)
                    {
                        float lift = 0.22f * Mathf.Sin(Mathf.Clamp01(pop) * Mathf.PI);
                        box.at = at + Vector3.up * lift;
                        box.scale = new Vector3(square * 0.9f, (0.45f + 0.5f * pop) * EaseOut(on), square * 0.9f);
                        box.color = Hdr(Color.Lerp(Violet, Magenta, 0.25f + 0.75f * pop), 1.3f + 2f * pop);
                        box.bright = bright;
                        box.Step(dt, cam);
                    }
                }
                float wireBright = (armed ? 1f : 0.4f) * (0.8f + 0.2f * Mathf.Sin(age * 9f)) * fade;
                foreach (var wire in wires)
                {
                    wire.color = Hdr(Color.Lerp(Violet, Magenta, 0.5f), 3f);
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

            WatchPieces();

            // Squash after the skeleton has been posed this frame.
            for (int i = squashes.Count - 1; i >= 0; i--)
                if (!squashes[i].Apply(Clock)) squashes.RemoveAt(i);

            sparks.Step(dt);
            motes.Step(dt);
            smoke.Step(dt);

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
