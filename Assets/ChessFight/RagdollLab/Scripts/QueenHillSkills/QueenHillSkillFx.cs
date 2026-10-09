using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Queen of the Hill skill effects in the skill test scene (R89), in design A "잉크 테두리 장난감 체스" (picked
    /// 2026-10-08 for all three modes): every warning is real 1.5 m board squares on the floor along the piece's own way
    /// (the queen's line, the knight's landing, the rook's two squares, the pawn's two squares ahead), coloured for the
    /// viewer: cream for mine, teal for my side's, red with sliding stripes for the other side's; special pieces fill
    /// them in their own colour (king emerald, queen gold, rook orange, bishop violet, knight sky) with the viewer's rim.
    /// Order is always warn → wait → bang → linger → gone: the squares fill as the windup runs, snap full in its last six
    /// frames, flash white for two and are gone in six. A hit flashes the body white for two frames, then the hitter's
    /// colour for two; a short hit stop and shake by strength (weak 3 frames / 0.04 m, medium 4 / 0.08, strong 5 / 0.16);
    /// round drops fly off one way, never stars. A piece that goes down gets a captured square in the hitter's colour
    /// under it, four cream dust puffs and a squash. Everything is a thick painted shape with a dark ink edge in the
    /// piece's darkest colour (no glow, no words or numbers, no chips, no magic circles), gone within about 1.5 s.
    /// No sound (승규 님, 10-09).
    ///
    /// Test-bed stand-ins: the hit stop holds the whole game (online it would hold only the two pieces), the shake moves
    /// the view camera. It listens to <see cref="RagdollPawn.QueenHillFx"/> and watches the pieces' skill states, so no
    /// other scene changes. Parts: QueenHillSkillFx.Kit.cs.
    /// </summary>
    [DefaultExecutionOrder(210)]   // after the lab camera (150) and the skeleton pose (100)
    public partial class QueenHillSkillFx : MonoBehaviour
    {
        public bool effects = true;
        [Tooltip("맞는 순간 게임 전체를 잠깐 멈춤 (시험용: 실제로는 때린 쪽·맞은 쪽만)")]
        public bool hitStop = true;
        public bool shake = true;

        /// <summary>The film's camera while it records; the lab camera otherwise.</summary>
        public static Camera ViewOverride;

        Transform root;
        LabGame game;
        QueenHillSkillBed bed;
        Camera prepared;
        float dt, rate = 1f, clock;

        // hit stop and shake
        float stopLeft, stopResume = 1f;
        bool stopping;
        float shakeAmp, shakeLeft, shakeTotal;

        RagdollPawn Viewer => bed != null ? bed.P1 : null;
        QueenHillSkillParams S => bed != null ? bed.skills : null;
        public Camera ViewCamera => ViewOverride != null ? ViewOverride : game != null && game.labCamera != null ? game.labCamera.Cam : Camera.main;
        Vector3 Eye => ViewCamera != null ? ViewCamera.transform.position : Vector3.up * 5f;

        void Awake()
        {
            game = GetComponent<LabGame>();
            if (game == null) game = FindFirstObjectByType<LabGame>();
            bed = GetComponent<QueenHillSkillBed>();
            root = new GameObject("Queen of the Hill skill effects").transform;
            BuildKit();
            RagdollPawn.QueenHillFx += OnFx;
        }

        void OnDestroy()
        {
            RagdollPawn.QueenHillFx -= OnFx;
            if (stopping) Time.timeScale = stopResume;
            foreach (var f in live) f.Destroy();
            live.Clear();
            foreach (var b in flashes.Values) b.Restore();
            foreach (var s in squashes.Values) s.Restore();
            if (root != null) Destroy(root.gameObject);
            DestroyKit();
        }

        // ---------------------------------------------------------------- time, hit stop, shake

        void Update()
        {
            // Effect time: the game's own pace (slow motion slows the effects with it), but a hit stop does not stop
            // them; a recording steps one frame of 1/60 s at a time.
            float real = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (Time.timeScale > 0f && !stopping) rate = Time.timeScale;
            dt = real * rate;
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

        /// <summary>A hit stop of so many frames (3 weak, 4 medium, 5-6 strong).</summary>
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

        /// <summary>Camera shake for the pieces in it (A: the hitter's and the hit one's camera only): metres, frames.</summary>
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

        // ---------------------------------------------------------------- the skills' moments

        void OnFx(QueenHillFxEvent e)
        {
            if (!effects) return;
            switch (e.kind)
            {
                case QueenHillFxKind.KingWindup: KingWindup(e); break;
                case QueenHillFxKind.KingWard: KingWave(e); break;
                case QueenHillFxKind.WardOn: WardOn(e); break;
                case QueenHillFxKind.WardBlock: WardBlock(e); break;
                case QueenHillFxKind.QueenSlash: QueenSlash(e); break;
                case QueenHillFxKind.QueenHit: Hit(e, Queen, 5, 0.16f, 4); break;
                case QueenHillFxKind.QueenWall: WallBang(e, Queen); break;
                case QueenHillFxKind.RookAccept: RookAccept(e); break;
                case QueenHillFxKind.RookSwap: RookSwap(e); break;
                case QueenHillFxKind.BishopRise: BishopRise(e); break;
                case QueenHillFxKind.BishopThrow: BishopThrow(e); break;
                case QueenHillFxKind.BishopImpact: BishopImpact(e); break;
                case QueenHillFxKind.BishopHit: Hit(e, Bishop, 4, 0.12f, 3); break;
                case QueenHillFxKind.BishopDrop: BishopDrop(e); break;
                case QueenHillFxKind.BishopLand: DustRing(e.at, 6, 0.8f); break;
                case QueenHillFxKind.KnightLock: KnightGather(e); break;
                case QueenHillFxKind.KnightLeap: KnightLeap(e); break;
                case QueenHillFxKind.KnightLand: KnightLand(e); break;
                case QueenHillFxKind.KnightFlatten: KnightFlatten(e); break;
                case QueenHillFxKind.PawnCrouch: PawnSquares(e); break;
                case QueenHillFxKind.PawnDash: PawnDash(e); break;
                case QueenHillFxKind.PawnBump: PawnBump(e); break;
                case QueenHillFxKind.PawnStop: SmallBang(e.at, -e.dir, PieceColors(e.by), 0.5f); break;
                case QueenHillFxKind.Down: Down(e); break;
            }
        }

        // ---------------------------------------------------------------- telegraph squares

        /// <summary>Paint a warning square: its fill (the piece's colour, or the side's for a pawn), the viewer's side rim,
        /// stripes if it is the other side's, an ink edge. <paramref name="strength"/> 0..1 as the windup fills it.</summary>
        void Warn(Tile t, RagdollPawn caster, float strength, float flash = 0f, Palette? fillOverride = null)
        {
            var side = SideOf(caster);
            Color sideFill = side == Side.Self ? SelfFill : side == Side.Ally ? TeamAlly.main : TeamEnemy.main;
            Color sideInk = side == Side.Self ? SelfInk : side == Side.Ally ? TeamAlly.ink : TeamEnemy.ink;
            float sideAlpha = side == Side.Self ? 0.35f : side == Side.Ally ? 0.3f : 0.4f;
            bool pawn = caster != null && caster.Piece == PieceKind.Pawn && fillOverride == null;
            if (pawn)
            {
                t.fill = A(sideFill, sideAlpha * Mathf.Lerp(0.35f, 1f, strength));
                t.ink = A(sideInk, Mathf.Lerp(0.5f, 1f, strength));
                t.rim = Color.clear;
            }
            else
            {
                var p = fillOverride ?? PieceColors(caster);
                t.fill = A(p.main, Mathf.Lerp(0.14f, 0.42f, strength));
                t.ink = A(p.ink, Mathf.Lerp(0.5f, 1f, strength));
                t.rim = A(side == Side.Self ? SelfFill : sideFill, Mathf.Lerp(0.45f, 0.95f, strength));
            }
            t.inkWidth = 0.05f;
            t.rimWidth = 0.09f;
            t.round = 0.07f;
            t.shape = 0f;
            t.stripe = side == Side.Enemy ? A(TeamEnemy.deep, 0.55f * strength) : Color.clear;
            t.stripePhase = clock * 0.5f;
            t.flash = flash;
            t.Apply();
        }

        /// <summary>Squares of the board along a line from <paramref name="from"/>, each on the floor under it.</summary>
        static List<Vector3> SquaresAlong(Vector3 from, Vector3 dir, float length, float square)
        {
            var list = new List<Vector3>();
            int n = Mathf.Max(1, Mathf.CeilToInt(length / square - 0.15f));
            for (int i = 0; i < n; i++)
            {
                Vector3 c = from + dir * (square * (i + 0.5f));
                list.Add(FloorUnder(c, 1.5f) + Vector3.up * 0.016f);
            }
            return list;
        }

        /// <summary>The queen's squares on the floors her slash runs along (R97: the slash follows its squares now — down to
        /// a lower tier, up a step — as 승규 님 asked; R95's straight line at her height is undone): each square on the
        /// way's floor at its middle; past a wall that stops the slash, faint squares on whatever floor is there.</summary>
        static List<(Vector3 at, bool past)> PathSquares(IReadOnlyList<Vector3> path, Vector3 dir, float length, float square, float reach, bool blocked)
        {
            var list = new List<(Vector3, bool)>();
            if (path == null || path.Count == 0) return list;
            Vector3 start = path[0];
            int n = Mathf.Max(1, Mathf.CeilToInt(length / square - 0.15f));
            for (int i = 0; i < n; i++)
            {
                float c = square * (i + 0.5f);
                Vector3 flat = start + dir * c;
                if (blocked && c > reach + 0.3f) list.Add((FloorUnder(flat, 1.5f) + Vector3.up * 0.016f, true));
                else list.Add((new Vector3(flat.x, PathFloor(path, c) + 0.016f, flat.z), false));
            }
            return list;
        }

        /// <summary>The floor of the slash's way <paramref name="along"/> metres from her feet.</summary>
        static float PathFloor(IReadOnlyList<Vector3> path, float along)
        {
            if (path == null || path.Count == 0) return 0f;
            return path[Mathf.Clamp(Mathf.RoundToInt(along / RagdollPawn.SlashPathStep), 0, path.Count - 1)].y;
        }

        /// <summary>A windup's strength: rises with the windup, full in its last six frames (A).</summary>
        static float Charge(float time, float total)
        {
            if (total <= 0f) return 1f;
            if (time >= total - 6f * F) return 1f;
            return Mathf.Lerp(0.3f, 0.85f, Mathf.Clamp01(time / Mathf.Max(F, total - 6f * F)));
        }

        /// <summary>Squares that flash white for two frames and are gone in six (the moment a warning turns into the move).</summary>
        void Release(List<Vector3> squares, Vector3 along, RagdollPawn caster, float square, Palette? fill = null, List<float> lengths = null)
        {
            var tiles = new List<Tile>();
            for (int i = 0; i < squares.Count; i++)
            {
                var t = new Tile(this, "Warning release");
                t.Floor(squares[i], along, lengths != null && i < lengths.Count ? new Vector2(square, lengths[i]) : Vector2.one * square);
                tiles.Add(t);
            }
            var f = Run(6f * F, (fx, d) =>
            {
                float a = fx.age / F;
                foreach (var t in fx.tiles)
                {
                    t.fade = 1f - Mathf.Clamp01((a - 2f) / 4f);
                    Warn(t, caster, 1f, a < 2f ? 1f : 0f, fill);
                }
                return true;
            });
            f.tiles.AddRange(tiles);
        }

        // ---------------------------------------------------------------- shared hit kit

        /// <summary>The hit body: white for two frames, then <paramref name="color"/> for two.</summary>
        void FlashBody(RagdollPawn pawn, Color color)
        {
            if (pawn == null) return;
            if (!flashes.TryGetValue(pawn, out var b))
            {
                b = new BodyFlash();
                foreach (var r in pawn.GetComponentsInChildren<Renderer>())
                    if ((r is SkinnedMeshRenderer || r is MeshRenderer) && r.enabled)
                    {
                        b.renderers.Add(r);
                        b.materials.Add(r.sharedMaterials);
                    }
                flashes[pawn] = b;
            }
            b.age = 0f;
            b.color = color;
            b.Paint(Flat(Color.white));
        }

        class BodyFlash
        {
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<Material[]> materials = new List<Material[]>();
            public float age;
            public Color color;
            public int phase;

            public void Paint(Material m)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = m;
                    r.sharedMaterials = mats;
                }
            }

            public void Restore()
            {
                for (int i = 0; i < renderers.Count; i++)
                    if (renderers[i] != null) renderers[i].sharedMaterials = materials[i];
            }
        }

        readonly Dictionary<RagdollPawn, BodyFlash> flashes = new Dictionary<RagdollPawn, BodyFlash>();

        void StepFlashes()
        {
            var done = new List<RagdollPawn>();
            foreach (var kv in flashes)
            {
                var b = kv.Value;
                b.age += dt;
                if (kv.Key == null || b.age >= 4f * F) { b.Restore(); done.Add(kv.Key); continue; }
                int phase = b.age < 2f * F ? 0 : 1;
                if (phase != b.phase)
                {
                    b.phase = phase;
                    b.Paint(phase == 0 ? Flat(Color.white) : Flat(b.color));
                }
            }
            foreach (var p in done) flashes.Remove(p);
        }

        /// <summary>A ring standing up at the hit, facing along it: white core, the hitter's colour, ink; 110% at f3.
        /// Turned half toward the camera so a hit seen from the side is not a ring edge-on (a line).</summary>
        void ImpactRing(Vector3 at, Vector3 normal, Palette p, float r0, float r1)
        {
            Vector3 toEye = Eye - at;
            if (normal.sqrMagnitude > 1e-4f && toEye.sqrMagnitude > 1e-4f)
            {
                normal = normal.normalized;
                if (Vector3.Dot(normal, toEye) < 0f) normal = -normal;
                normal = (normal + toEye.normalized).normalized;
            }
            var t = new Tile(this, "Impact ring");
            var f = Run(14f * F, (fx, d) =>
            {
                float a = fx.age / F;
                float k = a < 3f ? Mathf.Lerp(0f, 1.1f, a / 3f) : a < 6f ? Mathf.Lerp(1.1f, 1f, (a - 3f) / 3f) : 1f;
                float r = Mathf.Lerp(r0, r1, Mathf.Min(1f, k));
                if (k > 1f) r = r1 * k;
                t.Facing(at, normal, Vector2.one * (2f * r));
                t.shape = 1f;
                t.inner = 0.62f;
                t.fill = A(p.main, 1f);
                t.core = A(Color.white, 1f);
                t.coreWidth = 0.035f;
                t.ink = A(p.ink, 1f);
                t.inkWidth = 0.03f;
                t.rim = Color.clear;
                t.stripe = Color.clear;
                t.arc = a < 6f ? 1f : 1f - (a - 6f) / 8f;   // eaten round from its start
                t.fade = 1f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
        }

        /// <summary>3-5 round drops flying off one way, at uneven angles (A: never a star).</summary>
        void Drops(Vector3 at, Vector3 dir, Palette p, int n, float speed = 7f)
        {
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.up;
            float[] spread = { -12f, 4f, 17f, -24f, 28f };
            for (int i = 0; i < n; i++)
            {
                var drop = new Puff(this, new Palette { main = p.main, light = Color.white, deep = p.deep, ink = p.ink }, meshBall) { ink = 0.35f, lump = 0f };
                Vector3 axis = Vector3.Cross(dir, Vector3.up);
                if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;
                Vector3 v = Quaternion.AngleAxis(spread[i % spread.Length], Vector3.up) * Quaternion.AngleAxis(Random.Range(-10f, 10f), axis) * dir;
                float sp = speed * Random.Range(0.8f, 1.2f), len = Random.Range(0.26f, 0.42f);
                Vector3 pos = at;
                var f = Run(0.16f + i * 0.015f, (fx, d) =>
                {
                    pos += v * sp * d;
                    float k = 1f - fx.age / fx.life;
                    drop.t.SetPositionAndRotation(pos, Quaternion.LookRotation(v));
                    drop.t.localScale = new Vector3(0.1f * k + 0.02f, 0.1f * k + 0.02f, len * k + 0.05f);
                    drop.Apply(fx.age);
                    return true;
                });
                f.puffs.Add(drop);
            }
        }

        /// <summary>Cream dust puffs rolling out on the floor at uneven angles and eaten from below.</summary>
        void DustRing(Vector3 at, int n, float radius, Palette? colors = null, float size = 0.3f)
        {
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float ang = (start + i * 360f / n + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var puff = new Puff(this, colors ?? Dust) { ink = 0.32f };
                float r = size * Random.Range(0.8f, 1.2f), far = radius * Random.Range(0.75f, 1.1f);
                var f = Run(0.5f, (fx, d) =>
                {
                    float k = fx.age / fx.life;
                    float go = EaseOut(Mathf.Min(1f, fx.age / (10f * F)));
                    puff.t.position = at + dir * (far * go) + Vector3.up * (r * 0.6f + 0.15f * k);
                    puff.t.localScale = Vector3.one * (r * (0.5f + 0.5f * Pop(fx.age, 1.15f)) * (1f + 0.3f * k));
                    puff.dissolve = Mathf.Clamp01((k - 0.35f) / 0.65f);
                    puff.Apply(fx.age);
                    return true;
                });
                f.puffs.Add(puff);
            }
        }

        /// <summary>Puffs in a piece's colours bursting from a point (an impact's dust, a pop).</summary>
        void PuffBurst(Vector3 at, Palette colors, int n, float radius, float size, float life = 0.45f, float up = 0.3f)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.6f;
                var puff = new Puff(this, colors) { ink = 0.32f };
                float r = size * Random.Range(0.75f, 1.2f);
                var f = Run(life, (fx, d) =>
                {
                    float k = fx.age / fx.life;
                    puff.t.position = at + dir * (radius * EaseOut(k * 2f)) + Vector3.up * (up * k);
                    puff.t.localScale = Vector3.one * (r * Pop(fx.age, 1.2f));
                    puff.dissolve = Mathf.Clamp01((k - 0.3f) / 0.7f);
                    puff.Apply(fx.age);
                    return true;
                });
                f.puffs.Add(puff);
            }
        }

        /// <summary>A small knock: a ring and a couple of puffs (a wall that stopped a dash).</summary>
        void SmallBang(Vector3 at, Vector3 normal, Palette p, float size)
        {
            ImpactRing(at, normal, p, size * 0.4f, size);
            PuffBurst(at, Dust, 2, 0.35f, 0.18f);
        }

        /// <summary>A skill's hit: body flash, ring, drops, hit stop and shake by strength. The knockdown kit follows
        /// on Down.</summary>
        void Hit(QueenHillFxEvent e, Palette p, int stopFrames, float shakeMetres, int drops)
        {
            if (e.count == 1) return;   // guarded (WardBlock draws it)
            FlashBody(e.target, p.main);
            ImpactRing(e.at, e.dir, p, 0.3f, 0.9f);
            Drops(e.at, e.dir, p, drops);
            HitStop(stopFrames);
            Shake(e.by, e.target, shakeMetres, stopFrames + 4);
        }

        void WallBang(QueenHillFxEvent e, Palette p)
        {
            ImpactRing(e.at - e.dir * 0.05f, -e.dir, p, 0.4f, 1.1f);
            PuffBurst(e.at - e.dir * 0.2f, Dust, 3, 0.5f, 0.3f);
            Shake(e.by, null, 0.08f, 6);
        }

        /// <summary>The knockdown kit: once the piece lies, the captured square in the hitter's colour under it (scale
        /// 1.1 → 1 in six frames, held 30, faded in 15), an ink ring opening on the floor, four cream dust puffs, a squash.</summary>
        void Down(QueenHillFxEvent e)
        {
            var target = e.target;
            if (target == null) return;
            var p = PieceColors(e.by);
            bool stamped = false;
            var tile = new Tile(this, "Captured square");
            var ring = new Tile(this, "Floor ink ring");
            float square = S != null ? S.square : 1.5f;
            Vector3 at = Vector3.zero;
            float since = 0f;
            var f = Run(3f, (fx, d) =>
            {
                if (!stamped)
                {
                    // Stamped once it has come down: settled, or soon anyway (it may lie crumpled, not flat).
                    bool lying = target == null || target.State != PawnState.Ragdoll || fx.age > 0.45f
                                 || (fx.age > 0.12f && target.Hips.linearVelocity.magnitude < 2.5f);
                    if (!lying) return true;
                    stamped = true;
                    since = fx.age;
                    at = FloorUnder(target != null ? target.Hips.position : e.at, 0.6f) + Vector3.up * 0.014f;
                    DustRing(at, 4, 0.8f);
                    AddSquash(target, SquashKind.Down);
                }
                float a = (fx.age - since) / F;
                tile.Floor(at, Vector3.forward, Vector2.one * (square * (a < 6f ? Mathf.Lerp(1.1f, 1f, a / 6f) : 1f)));
                tile.fill = A(p.main, 0.8f);
                tile.ink = A(p.ink, 1f);
                tile.inkWidth = 0.08f;
                tile.rim = Color.clear;
                tile.stripe = Color.clear;
                tile.round = 0.06f;
                tile.fade = a < 36f ? 1f : 1f - (a - 36f) / 15f;
                tile.Apply();
                float rr = Mathf.Lerp(0.3f, 0.9f, EaseOut(a / 10f));
                ring.Floor(at + Vector3.up * 0.003f, Vector3.forward, Vector2.one * (2f * rr));
                ring.shape = 1f;
                ring.inner = Mathf.Max(0.01f, 1f - 0.06f / rr);
                ring.fill = A(Dust.ink, 0.6f);
                ring.ink = Color.clear;
                ring.core = Color.clear;
                ring.fade = 1f - Mathf.Clamp01((a - 10f) / 12f);
                ring.Apply();
                return a < 51f;
            });
            f.tiles.Add(tile);
            f.tiles.Add(ring);
        }

        // ---------------------------------------------------------------- king: close guard (B)

        readonly Dictionary<RagdollPawn, Fx> kingRings = new Dictionary<RagdollPawn, Fx>();

        /// <summary>The windup: 24 small tiles in a ring at the guard's reach light up clockwise a third at a time, like a
        /// clock (A, from the king's check); emerald, with the viewer's side rim.</summary>
        void KingWindup(QueenHillFxEvent e)
        {
            var king = e.by;
            float radius = e.size, windup = Mathf.Max(0.1f, e.to.y);
            var f = Run(windup + 1f, null);
            const int n = 24;
            for (int i = 0; i < n; i++) f.tiles.Add(new Tile(this, "Guard reach tile"));
            float released = -1f;
            f.step = (fx, d) =>
            {
                if (king == null) return false;
                if (released < 0f && king.SkillStage != SkillStage.Windup) released = fx.age;
                Vector3 c = king.FeetPoint + Vector3.up * 0.018f;
                for (int i = 0; i < n; i++)
                {
                    float ang = (i / (float)n) * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                    var t = fx.tiles[i];
                    t.Floor(FloorUnder(c + dir * radius, 1f) + Vector3.up * 0.018f, Vector3.Cross(Vector3.up, dir), Vector2.one * 0.45f);
                    int third = i / (n / 3);
                    float lit = released >= 0f ? 1f : Mathf.Clamp01((fx.age - third * windup / 3f) / (2f * F));
                    float a = released >= 0f ? (fx.age - released) / F : -1f;
                    t.fade = released >= 0f ? 1f - Mathf.Clamp01((a - 2f) / 4f) : lit;
                    Warn(t, king, released >= 0f ? 1f : Charge(fx.age, windup), released >= 0f && a < 2f ? 1f : 0f);
                }
                return released < 0f || (fx.age - released) < 6f * F;
            };
        }

        /// <summary>The release: the sceptre comes down, an emerald wave rolls out to the reach (70% of the way in the first
        /// 40% of the time), puffs ride its edge.</summary>
        void KingWave(QueenHillFxEvent e)
        {
            var king = e.by;
            float radius = e.size;
            Vector3 c = e.at + Vector3.up * 0.02f;
            var wave = new Tile(this, "Guard wave");
            var f = Run(30f * F, (fx, d) =>
            {
                float a = fx.age / F;
                float k = a < 10f ? EaseOut(a / 10f) : 1f;
                float r = Mathf.Max(0.2f, radius * k);
                wave.Floor(c, Vector3.forward, Vector2.one * (2f * r));
                wave.shape = 1f;
                wave.inner = Mathf.Clamp01(1f - 0.4f / r);
                wave.fill = A(King.main, 0.9f);
                wave.core = A(Color.white, 1f);
                wave.coreWidth = 0.06f;
                wave.ink = A(King.ink, 1f);
                wave.inkWidth = 0.04f;
                wave.fade = a < 10f ? 1f : 1f - (a - 10f) / 20f;
                wave.arc = 1f;
                wave.Apply();
                return true;
            });
            f.tiles.Add(wave);
            for (int i = 0; i < 8; i++)
            {
                float ang = (i / 8f) * Mathf.PI * 2f + 0.3f;
                Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                var puff = new Puff(this, King) { ink = 0.3f };
                float size = Random.Range(0.22f, 0.3f);
                var g = Run(0.5f, (fx, d) =>
                {
                    float a = fx.age / F;
                    float k = a < 10f ? EaseOut(a / 10f) : 1f;
                    puff.t.position = c + dir * (radius * k) + Vector3.up * (size * 0.6f);
                    puff.t.localScale = Vector3.one * (size * Pop(fx.age, 1.2f));
                    puff.dissolve = Mathf.Clamp01((fx.age / fx.life - 0.4f) / 0.6f);
                    puff.Apply(fx.age);
                    return true;
                });
                g.puffs.Add(puff);
            }
            if (scepters.TryGetValue(king, out var sceptre)) sceptre.flashUntil = clock + 2f * F;
            Shake(king, null, 0.06f, 8);
        }

        /// <summary>An ally the guard reached: white for two frames, emerald for two (the guard's colour, not a hit's).</summary>
        void WardOn(QueenHillFxEvent e) => FlashBody(e.target, King.main);

        /// <summary>A guarded piece shrugging a hit off: an emerald ring at the contact facing the hit, puffs, a little squash.</summary>
        void WardBlock(QueenHillFxEvent e)
        {
            FlashBody(e.target, King.main);
            Vector3 n = e.dir.sqrMagnitude > 1e-4f ? -e.dir : Vector3.up;
            ImpactRing(e.at, n, King, 0.25f, 0.7f);
            PuffBurst(e.at, King, 3, 0.35f, 0.16f, 0.35f);
            AddSquash(e.target, SquashKind.Block);
            HitStop(3);
            Shake(e.by, e.target, 0.04f, 5);
        }

        // ---------------------------------------------------------------- queen: the long slash (A)

        /// <summary>The release: the squares flash and go, the sword's gold crescent swings across in front of her and a
        /// crescent of gold flies along the slash's way — down to a lower tier, up a step (R97) — to where it stops, eaten
        /// from its tail.</summary>
        void QueenSlash(QueenHillFxEvent e)
        {
            var queen = e.by;
            float reach = e.size, square = S != null ? S.square : 1.5f, travel = S != null ? S.queenTravel : 0.2f;
            Vector3 dir = FlatDir(e.dir, Vector3.forward);
            Vector3 floorFrom = FloorUnder(e.at, 0.6f);
            // The way it runs, as it was when it went (the queen's own list changes the next time she aims).
            var path = queen != null && queen.SlashPath.Count > 0 ? new List<Vector3>(queen.SlashPath) : new List<Vector3> { floorFrom };
            Vector3 start = path[0];
            var lit = PathSquares(path, dir, S != null ? S.queenLength : 6f, square, reach, e.count == 1).FindAll(q => !q.past);
            Release(lit.ConvertAll(q => q.at), dir, queen, square);
            if (swords.TryGetValue(queen, out var sword)) sword.flashUntil = clock + 2f * F;

            // The swing (R93): a gold crescent along the path of the sword's tip, from low behind on the right up across
            // her front to high on the left (the sword passes straight ahead right now, so half of it is drawn at once).
            var swing = new Strip(this, Queen) { core = Color.white, coreShare = 0.28f, inkShare = 0.2f };
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            var f = Run(16f * F, (fx, d) =>
            {
                float a = fx.age / F;
                var pts = new List<Vector3>();
                Vector3 c = queen != null ? queen.bodies[(int)BodyId.ArmR].position : e.at;
                float floor = (queen != null ? queen.FeetPoint.y : floorFrom.y) + 0.08f;
                for (int i = 0; i <= 20; i++)
                {
                    Vector3 l = RagdollPawn.SwingDir(Mathf.Lerp(RagdollPawn.SwingLow + 25f, RagdollPawn.SwingHigh, i / 20f));
                    Vector3 p = c + (right * l.x + Vector3.up * l.y + dir * l.z) * 1.05f;
                    p.y = Mathf.Max(p.y, floor);
                    pts.Add(p);
                }
                swing.Build(pts, i => 0.3f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(i / 20f * 0.92f + 0.04f)), Eye);
                swing.head = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(a / 3f));
                swing.tail = a < 5f ? 0f : Mathf.Clamp01((a - 5f) / 9f);
                swing.taper = 0.6f;
                swing.Apply();
                return true;
            });
            f.strips.Add(swing);

            // The flying crescent: leaning left with the swing, bowed forward along the line, with a flat twin on the
            // floor and a gold trail.
            Vector3 lean = (Vector3.up * 0.9f - right * 0.42f).normalized;
            var blade = new Strip(this, Queen) { core = Color.white, coreShare = 0.3f, inkShare = 0.2f };
            var low = new Strip(this, Queen) { core = Color.white, coreShare = 0.3f, inkShare = 0.22f };
            var trail = new Strip(this, Queen) { core = Queen.light, coreShare = 0.25f, inkShare = 0.25f };
            float life = travel + 10f * F;
            var g = Run(life, (fx, d) =>
            {
                float k = Mathf.Clamp01(fx.age / Mathf.Max(F, travel));
                float eaten = fx.age > travel ? (fx.age - travel) / (10f * F) : 0f;
                // R97: it rides the way's floor (down a drop, up a step) with its lower tip just over it.
                float dist = 0.6f + (reach - 0.6f) * k, floorY = PathFloor(path, dist);
                Vector3 front = start + dir * dist;
                front.y = floorY + 0.72f;
                var up = new List<Vector3>();
                var flat = new List<Vector3>();
                for (int i = 0; i <= 14; i++)
                {
                    float ang = Mathf.Lerp(-62f, 62f, i / 14f) * Mathf.Deg2Rad;
                    up.Add(front + dir * (0.45f * (Mathf.Cos(ang) - 1f)) + lean * (0.75f * Mathf.Sin(ang)));
                    flat.Add(new Vector3(front.x, floorY + 0.05f, front.z) + dir * (0.5f * (Mathf.Cos(ang) - 1f)) + right * (0.7f * Mathf.Sin(ang)));
                }
                blade.Build(up, i => 0.26f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(i / 14f * 0.9f + 0.05f)), Eye);
                low.Build(flat, i => 0.2f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(i / 14f * 0.9f + 0.05f)), Eye, true);
                blade.head = low.head = 1f;
                blade.tail = low.tail = Mathf.Clamp01(eaten);
                blade.Apply();
                low.Apply();
                // The gold band on the way's floor behind it, down and up the steps it took.
                var line = new List<Vector3>();
                for (float t = 0.5f; t <= dist - 0.4f + 1e-3f; t += RagdollPawn.SlashPathStep)
                {
                    Vector3 p = start + dir * t;
                    line.Add(new Vector3(p.x, PathFloor(path, t) + 0.03f, p.z));
                }
                if (line.Count < 2) line = new List<Vector3> { start + dir * 0.5f + Vector3.up * 0.03f, start + dir * 0.6f + Vector3.up * 0.03f };
                trail.Build(line, i => 0.32f, Eye, true);
                trail.taper = 1f;
                trail.head = 1f;
                trail.tail = Mathf.Clamp01(eaten * 1.2f);
                trail.fade = 1f - Mathf.Clamp01(eaten);
                trail.Apply();
                return true;
            });
            g.strips.Add(blade);
            g.strips.Add(low);
            g.strips.Add(trail);
            PuffBurst(e.at + dir * 0.8f - Vector3.up * 0.3f, Dust, 2, 0.3f, 0.16f, 0.35f);
        }

        // ---------------------------------------------------------------- rook: castling (B)

        void RookAccept(QueenHillFxEvent e)
        {
            if (e.target != null) FlashBody(e.target, PieceColors(e.target).main);
        }

        /// <summary>The swap: a trapdoor opens under each, a fat two-colour ribbon arch (the rook's orange half from its end,
        /// the ally's own colour from the other, blending over the middle) grows between them, both fly it; puffs in
        /// their own colours as they go.</summary>
        void RookSwap(QueenHillFxEvent e)
        {
            var rook = e.by;
            var ally = e.target;
            if (rook == null || ally == null) return;
            float square = S != null ? S.square : 1.5f;
            Vector3 a = e.at, b = e.to;
            Vector3 along = FlatDir(b - a, Vector3.forward);
            var allyColors = PieceColors(ally);
            Socket(a, along, square, e.size);
            Socket(b, along, square, e.size);
            PuffBurst(a + Vector3.up * 0.15f, Rook, 2, 0.5f, 0.2f);
            PuffBurst(b + Vector3.up * 0.15f, allyColors, 2, 0.5f, 0.2f);
            flyKind[rook] = "castle";
            flyKind[ally] = "castle";
            // The arch: the flight's own arc (both fly the same one), growing from both ends, the rook's half in orange and
            // the ally's in its piece's own colour (the queen gold, the bishop violet, the knight sky, the king emerald; a
            // pawn its side's), running into each other in a gradient over the middle (R93). R94: one ribbon end to end
            // (two halves with rounded heads left an empty notch where they met).
            var arch = new Strip(this, Rook) { core = Rook.light, blendColors = allyColors, blendCore = allyColors.light, blendFrom = 0.3f, blendTo = 0.7f, blendAmount = 1f, mirror = true };
            float total = Mathf.Max(0.2f, e.size);
            float top = Mathf.Max(a.y, b.y) + (S != null ? S.rookArc : 1.4f) + 0.35f;
            var f = Run(total + 8f * F, (fx, d) =>
            {
                float grow = Mathf.Clamp01(fx.age / (6f * F));
                float fade = fx.age > total ? 1f - (fx.age - total) / (8f * F) : 1f;
                var pts = new List<Vector3>();
                for (int i = 0; i <= 32; i++) pts.Add(Arc(a, b, top, i / 32f));
                arch.Build(pts, i => 0.36f, Eye);
                arch.tail = 0f;
                arch.head = grow;
                arch.fade = fade;
                arch.Apply();
                return true;
            });
            f.strips.Add(arch);
        }

        static Vector3 Arc(Vector3 a, Vector3 b, float top, float t)
        {
            Vector3 p = Vector3.Lerp(a, b, t);
            float baseY = Mathf.Lerp(a.y, b.y, t);
            p.y = baseY + 4f * (top - baseY) * t * (1f - t) + 0.35f * (1f - 4f * t * (1f - t)) + 0.35f;
            return p;
        }

        /// <summary>A trapdoor in the board square: a dark socket with a walnut lip and an ink edge opening from the middle
        /// in four frames, closing again once the swap is done.</summary>
        void Socket(Vector3 at, Vector3 along, float square, float open)
        {
            var t = new Tile(this, "Trapdoor");
            Vector3 p = FloorUnder(at, 0.6f) + Vector3.up * 0.017f;
            var f = Run(open + 10f * F, (fx, d) =>
            {
                float a = fx.age / F;
                float shut = fx.age > open ? (fx.age - open) / (4f * F) : 0f;
                float k = Mathf.Clamp01(a / 4f) * (1f - Mathf.Clamp01(shut));
                t.Floor(p, along, Vector2.one * (square * 0.92f * Mathf.Max(0.05f, k)));
                t.fill = A(SocketColor, 0.7f);
                t.rim = A(Walnut, 1f);
                t.rimWidth = 0.07f;
                t.ink = A(Dust.ink, 1f);
                t.inkWidth = 0.04f;
                t.stripe = Color.clear;
                t.shape = 0f;
                t.round = 0.05f;
                t.fade = k > 0.04f ? 1f : 0f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
        }

        /// <summary>A piece set down by the swap: its new square stamps in its own colour with an overshoot, four puffs.</summary>
        void CastleLand(RagdollPawn pawn)
        {
            var p = PieceColors(pawn);
            float square = S != null ? S.square : 1.5f;
            Vector3 at = FloorUnder(pawn.Hips.position, 0.6f) + Vector3.up * 0.015f;
            var t = new Tile(this, "Castled square");
            var f = Run(50f * F, (fx, d) =>
            {
                float a = fx.age / F;
                float s = a < 3f ? Mathf.Lerp(0.85f, 1.1f, a / 3f) : a < 6f ? Mathf.Lerp(1.1f, 1f, (a - 3f) / 3f) : 1f;
                t.Floor(at, Vector3.forward, Vector2.one * (square * s));
                t.fill = A(p.main, 0.7f);
                t.core = A(p.light, 1f);
                t.coreWidth = 0.06f;
                t.rim = Color.clear;
                t.ink = A(p.ink, 1f);
                t.inkWidth = 0.05f;
                t.stripe = Color.clear;
                t.fade = a < 20f ? 1f : 1f - (a - 20f) / 30f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
            PuffBurst(at + Vector3.up * 0.1f, p, 4, 0.6f, 0.18f);
        }

        // ---------------------------------------------------------------- bishop: the air barrage (B)

        void BishopRise(QueenHillFxEvent e)
        {
            var bishop = e.by;
            Vector3 c = e.at;
            // Two violet rings climb round the body to the hover height, then pop into puffs.
            for (int i = 0; i < 2; i++)
            {
                var ring = new Prop(this, meshTorus, Bishop, "Rising ring") { inkWidth = 0.012f };
                float delay = i * 3f * F;
                var f = Run(14f * F + delay, (fx, d) =>
                {
                    float k = Mathf.Clamp01((fx.age - delay) / (12f * F));
                    Vector3 at = bishop != null ? new Vector3(bishop.Hips.position.x, c.y, bishop.Hips.position.z) : c;
                    ring.t.position = at + Vector3.up * Mathf.Lerp(0.1f, (e.size + 0.55f), EaseOut(k));
                    ring.t.localScale = Vector3.one * 0.55f * (k > 0f ? 1f : 0f);
                    ring.Show(k > 0f);
                    ring.Apply();
                    return true;
                });
                f.props.Add(ring);
                float popAt = 12f * F + delay;
                Run(popAt + 0.5f, (fx, d) =>
                {
                    if (fx.age >= popAt && fx.age - d < popAt && bishop != null)
                        PuffBurst(bishop.Hips.position + Vector3.up * 0.4f, Bishop, 2, 0.4f, 0.13f, 0.35f);
                    return fx.age < popAt + d;
                });
            }
        }

        /// <summary>The throw: a chunky cartoon rock with a painted violet band flies its arc to the aimed square, a violet
        /// ribbon behind it.</summary>
        void BishopThrow(QueenHillFxEvent e)
        {
            Vector3 from = e.at, to = e.to;
            float time = Mathf.Max(0.1f, e.size);
            var rock = new Puff(this, Stone) { ink = 0.34f, lump = 0.32f, scale = 1.6f };
            var band = new Prop(this, meshTorus, Bishop, "Rock band") { inkWidth = 0.008f };
            var trail = new Strip(this, Bishop) { core = Bishop.light, coreShare = 0.25f };
            var path = new List<Vector3>();
            float top = Mathf.Max(from.y, to.y) + 1.2f;
            var f = Run(time, (fx, d) =>
            {
                float k = Mathf.Clamp01(fx.age / time);
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y = Mathf.Lerp(from.y, to.y, k) + 4f * (top - Mathf.Max(from.y, to.y)) * k * (1f - k) + 0.18f * (1f - k);
                rock.t.SetPositionAndRotation(p, Quaternion.Euler(fx.age * 400f, fx.age * 260f, 0f));
                rock.t.localScale = Vector3.one * 0.36f;
                rock.Apply(fx.age);
                band.t.SetPositionAndRotation(p, rock.t.rotation);
                band.t.localScale = Vector3.one * 0.19f;
                band.Apply();
                path.Add(p);
                while (path.Count > 16) path.RemoveAt(0);
                trail.Build(path, i => 0.16f, Eye);
                trail.taper = 1f;
                trail.Apply();
                return true;
            });
            f.puffs.Add(rock);
            f.props.Add(band);
            f.strips.Add(trail);
        }

        /// <summary>The landing: the aimed square flashes and goes, an X of two diagonal bars (white core, violet, ink) grows
        /// out across it in six frames and fades, the rock bursts into grey-violet puffs (never chips).</summary>
        void BishopImpact(QueenHillFxEvent e)
        {
            var bishop = e.by;
            float square = S != null ? S.square : 1.5f, arm = e.size;
            Vector3 c = FloorUnder(e.at, 0.6f) + Vector3.up * 0.02f;
            Vector3 yaw = FlatDir(e.dir, Vector3.forward);
            Release(new List<Vector3> { c - Vector3.up * 0.004f }, yaw, bishop, square);
            Vector3 d1 = Quaternion.Euler(0f, 45f, 0f) * yaw, d2 = Quaternion.Euler(0f, -45f, 0f) * yaw;
            foreach (var dir in new[] { d1, d2 })
            {
                var bar = new Tile(this, "X bar");
                var core = new Tile(this, "X bar core");
                var f = Run(30f * F, (fx, d) =>
                {
                    float a = fx.age / F;
                    float len = 2f * arm * EaseOut(a / 6f) + 0.3f;
                    float fade = a < 10f ? 1f : 1f - (a - 10f) / 20f;
                    bar.Floor(c, dir, new Vector2(0.42f, len));
                    bar.fill = A(Bishop.main, 0.95f);
                    bar.ink = A(Bishop.ink, 1f);
                    bar.inkWidth = 0.045f;
                    bar.rim = A(Bishop.light, 0.9f);
                    bar.rimWidth = 0.05f;
                    bar.stripe = Color.clear;
                    bar.round = 0.2f;
                    bar.flash = a < 2f ? 0.6f : 0f;
                    bar.fade = fade;
                    bar.Apply();
                    core.Floor(c + Vector3.up * 0.002f, dir, new Vector2(0.1f, len * 0.92f));
                    core.fill = A(Color.white, 1f);
                    core.ink = Color.clear;
                    core.rim = Color.clear;
                    core.stripe = Color.clear;
                    core.round = 0.05f;
                    core.fade = fade * (a < 8f ? 1f : 1f - (a - 8f) / 8f);
                    core.Apply();
                    return true;
                });
                f.tiles.Add(bar);
                f.tiles.Add(core);
            }
            var stoneDust = new Palette("#B9B0C9", "#E8E2F2", "#6E6380", "#2A0F57");
            PuffBurst(c + Vector3.up * 0.2f, stoneDust, 3, 0.55f, 0.4f, 0.6f, 0.35f);
            DustRing(c, 4, 1.1f, stoneDust, 0.22f);
            Shake(bishop, null, 0.12f, 8);
        }

        void BishopDrop(QueenHillFxEvent e)
        {
            if (hoverTiles.TryGetValue(e.by, out var tile)) tile.dropAt = clock;
        }

        // ---------------------------------------------------------------- knight: the press leap (B)

        void KnightGather(QueenHillFxEvent e)
        {
            var knight = e.by;
            for (int i = 0; i < 5; i++)
            {
                float ang = i * 72f * Mathf.Deg2Rad + 0.4f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var puff = new Puff(this, Knight) { ink = 0.32f };
                var f = Run(0.2f, (fx, d) =>
                {
                    float k = fx.age / fx.life;
                    Vector3 c = knight != null ? knight.FeetPoint : e.at;
                    puff.t.position = c + dir * Mathf.Lerp(0.7f, 0.15f, k) + Vector3.up * 0.1f;
                    puff.t.localScale = Vector3.one * 0.12f * Pop(fx.age);
                    puff.Apply(fx.age);
                    return true;
                });
                f.puffs.Add(puff);
            }
        }

        void KnightLeap(QueenHillFxEvent e)
        {
            var knight = e.by;
            flyKind[knight] = "leap";
            Vector3 c = e.at + Vector3.up * 0.02f;
            // A white ring flashes at the feet; wind puffs squash out in a ring.
            var ring = new Tile(this, "Take-off ring");
            var f = Run(8f * F, (fx, d) =>
            {
                ring.Floor(c, Vector3.forward, Vector2.one * 0.9f * (1f + fx.age / fx.life * 0.4f));
                ring.shape = 1f;
                ring.inner = 0.72f;
                ring.fill = A(Color.white, 1f);
                ring.ink = A(Knight.ink, 1f);
                ring.inkWidth = 0.025f;
                ring.core = Color.clear;
                ring.fade = fx.age < 2f * F ? 1f : 1f - (fx.age - 2f * F) / (6f * F);
                ring.Apply();
                return true;
            });
            f.tiles.Add(ring);
            DustRing(c, 5, 1f, Knight, 0.3f);
        }

        void KnightLand(QueenHillFxEvent e)
        {
            Vector3 c = e.at + Vector3.up * 0.02f;
            float radius = e.size;
            DustRing(c, 8, radius * 1.1f, Knight, 0.3f);
            // A horseshoe-shaped scuff stamps where it came down and fades.
            var scuff = new Tile(this, "Horseshoe scuff");
            var f = Run(60f * F, (fx, d) =>
            {
                scuff.Floor(c, e.dir, Vector2.one * 1f);
                Horseshoe(scuff, Knight, 0.6f);
                scuff.fade = 1f - fx.age / fx.life;
                scuff.Apply();
                return true;
            });
            f.tiles.Add(scuff);
            Shake(e.by, null, 0.12f, 8);
        }

        void Horseshoe(Tile t, Palette p, float alpha)
        {
            t.shape = 1f;
            t.inner = 0.66f;
            t.gap = 110f;
            t.fill = A(p.main, alpha);
            t.core = A(Color.white, alpha > 0.9f ? 1f : alpha);
            t.coreWidth = 0.04f;
            t.ink = A(p.ink, 1f);
            t.inkWidth = 0.03f;
            t.rim = Color.clear;
            t.stripe = Color.clear;
        }

        void KnightFlatten(QueenHillFxEvent e)
        {
            FlashBody(e.target, Knight.main);
            var t = new Tile(this, "Flatten ring");
            Vector3 at = e.at;
            var f = Run(12f * F, (fx, d) =>
            {
                float a = fx.age / F;
                float k = a < 3f ? Mathf.Lerp(0f, 1.1f, a / 3f) : a < 6f ? Mathf.Lerp(1.1f, 1f, (a - 3f) / 3f) : 1f;
                t.Floor(at, Vector3.forward, Vector2.one * (2f * Mathf.Lerp(0.3f, 0.9f, k)));
                t.shape = 1f;
                t.inner = 0.7f;
                t.fill = A(Knight.main, 1f);
                t.core = A(Color.white, 1f);
                t.coreWidth = 0.04f;
                t.ink = A(Knight.ink, 1f);
                t.inkWidth = 0.03f;
                t.fade = a < 6f ? 1f : 1f - (a - 6f) / 6f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
            Drops(at, Vector3.down + FlatDir(e.dir, Vector3.forward), Knight, 3, 5f);
            AddSquash(e.target, SquashKind.Flatten, S != null ? S.knightFlatten : 0.7f);
            if (e.count == 1)
            {
                // Stamped on the head (R93): a horseshoe stamp pops where its feet came down and cream dust bursts out.
                var stamp = new Tile(this, "Head stamp");
                var g = Run(14f * F, (fx, d) =>
                {
                    float a = fx.age / F;
                    stamp.Floor(at + Vector3.up * 0.05f, FlatDir(e.dir, Vector3.forward), Vector2.one * (1.1f * Pop(fx.age, 1.2f)));
                    Horseshoe(stamp, Knight, 1f);
                    stamp.fade = a < 6f ? 1f : 1f - (a - 6f) / 8f;
                    stamp.Apply();
                    return true;
                });
                g.tiles.Add(stamp);
                PuffBurst(at, Dust, 4, 0.6f, 0.16f, 0.4f);
            }
            HitStop(e.count == 1 ? 5 : 4);
            Shake(e.by, e.target, e.count == 1 ? 0.16f : 0.12f, 8);
        }

        // ---------------------------------------------------------------- pawn: squeeze through (B)

        /// <summary>The pawn's two squares ahead (its opening two-square move), filled in the few frames it crouches.</summary>
        void PawnSquares(QueenHillFxEvent e)
        {
            var pawn = e.by;
            float square = S != null ? S.square : 1.5f;
            Vector3 dir = FlatDir(e.dir, Vector3.forward);
            var squares = SquaresAlong(e.at, dir, square * 2f, square);
            float crouch = Mathf.Max(F, e.to.y);
            var f = Run(crouch + 0.05f, null);
            foreach (var c in squares)
            {
                var t = new Tile(this, "Pawn warning");
                t.Floor(c, dir, Vector2.one * square);
                f.tiles.Add(t);
            }
            f.step = (fx, d) =>
            {
                foreach (var t in fx.tiles) Warn(t, pawn, Mathf.Clamp01(fx.age / crouch));
                return pawn != null && pawn.SkillStage == SkillStage.Windup;
            };
            f.end = () => Release(squares, dir, pawn, square);
        }

        /// <summary>The take-off: the square it leaves stamps in its side's colour with a cream bevel and an overshoot; two
        /// dust puffs kick back from the heels.</summary>
        void PawnDash(QueenHillFxEvent e)
        {
            var pawn = e.by;
            var p = PieceColors(pawn);
            float square = S != null ? S.square : 1.5f;
            Vector3 dir = FlatDir(e.dir, Vector3.forward);
            var t = new Tile(this, "Take-off square");
            Vector3 at = e.at + Vector3.up * 0.015f;
            var f = Run(50f * F, (fx, d) =>
            {
                float a = fx.age / F;
                float s = a < 3f ? Mathf.Lerp(0.85f, 1.08f, a / 3f) : a < 6f ? Mathf.Lerp(1.08f, 1f, (a - 3f) / 3f) : 1f;
                t.Floor(at, dir, Vector2.one * (square * s));
                t.fill = A(p.main, 0.7f);
                t.core = A(Dust.light, 1f);
                t.coreWidth = 0.06f;
                t.rim = Color.clear;
                t.ink = A(p.ink, 1f);
                t.inkWidth = 0.05f;
                t.stripe = Color.clear;
                t.fade = a < 5f ? 1f : 1f - (a - 5f) / 45f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
            PuffBurst(e.at - dir * 0.25f + Vector3.up * 0.1f, Dust, 2, 0.3f, 0.18f, 0.35f, 0.3f);
        }

        void PawnBump(QueenHillFxEvent e)
        {
            var p = PieceColors(e.by);
            FlashBody(e.target, p.main);
            ImpactRing(e.at, e.dir, p, 0.2f, 0.55f);
            Drops(e.at, e.dir, p, 2, 5f);
            HitStop(2);
            Shake(e.by, e.target, 0.04f, 4);
        }

        // ---------------------------------------------------------------- watching the pieces (what lasts as long as a state)

        class HandProp
        {
            public Prop a, b, c;
            public float flashUntil, shown;
        }

        class HoverTile
        {
            public Prop tile;
            public Tile shadow;
            public float dropAt = -1f, drip;
        }

        readonly Dictionary<RagdollPawn, HandProp> swords = new Dictionary<RagdollPawn, HandProp>();
        readonly Dictionary<RagdollPawn, HandProp> scepters = new Dictionary<RagdollPawn, HandProp>();
        readonly Dictionary<RagdollPawn, HoverTile> hoverTiles = new Dictionary<RagdollPawn, HoverTile>();
        readonly Dictionary<RagdollPawn, string> flyKind = new Dictionary<RagdollPawn, string>();
        readonly Dictionary<RagdollPawn, bool> wasFlying = new Dictionary<RagdollPawn, bool>();

        void WatchPieces()
        {
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null) continue;
                var s = pawn.QueenHillSkills;
                bool fly = pawn.QhFlying;
                if (wasFlying.TryGetValue(pawn, out bool was) && was && !fly && flyKind.TryGetValue(pawn, out var kind) && kind == "castle"
                    && pawn.State == PawnState.Active)
                    CastleLand(pawn);
                wasFlying[pawn] = fly;
                if (pawn.WardLeft > 0f)
                {
                    WardRing(pawn);
                    WardOutline(pawn);
                }
                if (s == null)
                {
                    if (pawn.PawnRushSkills != null && pawn.Piece == PieceKind.Rook) WatchChargingRook(pawn);
                    continue;
                }
                var stage = pawn.SkillStage;
                switch (pawn.Piece)
                {
                    case PieceKind.King:
                        if (stage == SkillStage.Windup || stage == SkillStage.Recovery) Sceptre(pawn);
                        break;
                    case PieceKind.Queen:
                        if (stage != SkillStage.None) Sword(pawn);
                        if (stage == SkillStage.Windup) QueenLine(pawn, s);
                        break;
                    case PieceKind.Rook:
                        if (stage == SkillStage.Windup) RookAim(pawn, s);
                        break;
                    case PieceKind.Bishop:
                        if (stage == SkillStage.Windup) BishopHover(pawn, s);
                        if (stage == SkillStage.Windup && !pawn.QhAimLocked) BishopAim(pawn, s);
                        break;
                    case PieceKind.Knight:
                        if (stage == SkillStage.Windup && !pawn.QhAimLocked) KnightAim(pawn, s, false);
                        if (stage == SkillStage.Windup && pawn.QhAimLocked || stage == SkillStage.Active) KnightAim(pawn, s, true);
                        if (stage == SkillStage.Active && pawn.QhFlying) WindTrail(pawn);
                        break;
                    case PieceKind.Pawn:
                        if (stage == SkillStage.Active) SpeedWedges(pawn);
                        break;
                }
                if (pawn.QhCrouch > 0.01f) AddSquash(pawn, SquashKind.Crouch);
            }
        }

        // ----- king

        void Sceptre(RagdollPawn king)
        {
            if (!scepters.TryGetValue(king, out var h) || h.a == null)
            {
                h = new HandProp();
                h.a = new Prop(this, RoundBox(new Vector3(0.075f, 0.66f, 0.075f), 0.035f), new Palette("#FFD34D", "#FFF0A8", "#C7811A", "#3A2208"), "Sceptre shaft");
                h.b = new Prop(this, meshPuff, King, "Sceptre crown") { inkWidth = 0.012f };
                h.c = new Prop(this, meshTorus, new Palette("#FFD34D", "#FFF0A8", "#C7811A", "#3A2208"), "Sceptre band") { inkWidth = 0.008f };
                scepters[king] = h;
                var f = Keep(king, "sceptre", () => new Fx());
                f.props.Add(h.a);
                f.props.Add(h.b);
                f.props.Add(h.c);
                f.end = () => scepters.Remove(king);
                f.step = (fx, d) =>
                {
                    h.shown = Held(fx) ? Mathf.Min(1f, h.shown + d / (6f * F)) : h.shown - d / (6f * F);
                    if (h.shown <= 0f || king == null) return false;
                    PlaceInHand(king, h.a.t, 0.31f, h.shown);
                    Vector3 tip = h.a.t.position + h.a.t.up * 0.34f * h.shown;
                    h.b.t.position = tip + h.a.t.up * 0.05f;
                    h.b.t.localScale = Vector3.one * 0.21f * h.shown;
                    h.c.t.SetPositionAndRotation(tip, h.a.t.rotation);
                    h.c.t.localScale = Vector3.one * 0.09f * h.shown;
                    float flash = clock < h.flashUntil ? 1f : 0f;
                    h.a.flash = h.b.flash = h.c.flash = flash;
                    h.a.Apply();
                    h.b.Apply();
                    h.c.Apply();
                    return true;
                };
            }
            else Keep(king, "sceptre", () => new Fx());
        }

        /// <summary>A prop held as an extension of the forearm: from the hand on along the line shoulder → hand.</summary>
        static void PlaceInHand(RagdollPawn pawn, Transform t, float half, float scale)
        {
            var bodies = pawn.bodies;
            Vector3 hand = bodies[(int)BodyId.HandR].position, shoulder = bodies[(int)BodyId.ArmR].position;
            Vector3 along = hand - shoulder;
            if (along.sqrMagnitude < 1e-6f) along = Vector3.up;
            along.Normalize();
            Vector3 side = Vector3.Cross(along, pawn.Facing);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(along, Vector3.forward);
            Quaternion rot = Quaternion.LookRotation(side.normalized, along);
            t.SetPositionAndRotation(hand + along * (half * scale - 0.04f), rot);
            t.localScale = Vector3.one * scale;
        }

        void WardRing(RagdollPawn pawn)
        {
            var f = Keep(pawn, "ward", () =>
            {
                var fx = new Fx();
                fx.tiles.Add(new Tile(this, "Guard ring"));
                fx.step = (x, d) =>
                {
                    var t = x.tiles[0];
                    if (!Held(x) || pawn == null) return false;
                    t.Floor(pawn.FeetPoint + Vector3.up * 0.02f, pawn.Facing, Vector2.one * 0.95f);
                    t.shape = 1f;
                    t.inner = 0.86f;
                    t.fill = A(King.main, 1f);
                    t.core = A(GoldAccent, 1f);
                    t.coreWidth = 0.02f;
                    t.ink = A(King.ink, 1f);
                    t.inkWidth = 0.018f;
                    float left = pawn.WardLeft;
                    // Like anything that lasts (A): the last 0.6 s it blinks three times.
                    t.fade = left > 0.6f ? 1f : (Mathf.Sin((0.6f - left) / 0.6f * 3f * Mathf.PI * 2f) > -0.3f ? 1f : 0.25f);
                    t.Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>A guarded piece's body outlined (R93, 승규 님: "근접 호위를 받은 아군들은 몸에 윤곽선"): the body's own
        /// skin drawn twice more on the same bones, pushed out along its normals and inside out — an emerald line with
        /// an ink edge outside it. They live under the effects (not on the piece), so a hit flash leaves them be. Like
        /// the guard's ring, it blinks three times in the last 0.6 s.</summary>
        void WardOutline(RagdollPawn pawn)
        {
            if (pawn == null || pawn.skin == null || pawn.skin.sharedMesh == null) return;
            Keep(pawn, "ward outline", () =>
            {
                var fx = new Fx();
                var hulls = new List<SkinnedMeshRenderer> { Hull(pawn.skin, matWardInk, "Guard outline ink"), Hull(pawn.skin, matWardLine, "Guard outline") };
                fx.end = () =>
                {
                    foreach (var h in hulls) if (h != null) Destroy(h.gameObject);
                };
                fx.step = (x, d) =>
                {
                    if (!Held(x) || pawn == null) return false;
                    float left = pawn.WardLeft;
                    bool on = left > 0.6f || Mathf.Sin((0.6f - left) / 0.6f * 3f * Mathf.PI * 2f) > -0.3f;
                    // Into the outline in three frames (it pops on with the guard).
                    float grow = Mathf.Clamp01(x.age / (3f * F));
                    foreach (var h in hulls)
                        if (h != null) h.enabled = on && grow > 0.34f;
                    return true;
                };
                return fx;
            });
        }

        SkinnedMeshRenderer Hull(SkinnedMeshRenderer skin, Material material, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = skin.sharedMesh;
            r.bones = skin.bones;
            r.rootBone = skin.rootBone;
            r.quality = skin.quality;
            r.updateWhenOffscreen = true;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var mats = new Material[Mathf.Max(1, skin.sharedMesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            r.sharedMaterials = mats;
            return r;
        }

        // ----- queen

        void Sword(RagdollPawn queen)
        {
            if (!swords.TryGetValue(queen, out var h) || h.a == null)
            {
                h = new HandProp();
                h.a = new Prop(this, RoundBox(new Vector3(0.14f, 0.86f, 0.035f), 0.016f), new Palette("#FFF4D6", "#FFFFFF", "#E2C27A", "#3A2208"), "Sword blade") { inkWidth = 0.018f };
                h.b = new Prop(this, RoundBox(new Vector3(0.34f, 0.07f, 0.09f), 0.03f), Queen, "Sword guard") { inkWidth = 0.016f };
                h.c = new Prop(this, meshPuff, Queen, "Sword sheen") { inkWidth = 0f };
                swords[queen] = h;
                var f = Keep(queen, "sword", () => new Fx());
                f.props.Add(h.a);
                f.props.Add(h.b);
                f.props.Add(h.c);
                f.end = () => swords.Remove(queen);
                f.step = (fx, d) =>
                {
                    h.shown = Held(fx) ? Mathf.Min(1f, h.shown + d / (6f * F)) : h.shown - d / (6f * F);
                    if (h.shown <= 0f || queen == null) return false;
                    float k = h.shown < 1f ? Pop(h.shown * 6f * F, 1.1f) : 1f;
                    PlaceBlade(queen, h.a.t, h.b.t, k);
                    // The windup's sheen: a white oval sliding from the hilt to the tip.
                    var s = queen.QueenHillSkills;
                    bool winding = queen.SkillStage == SkillStage.Windup && queen.QhAimLocked && s != null;
                    float slide = winding ? Mathf.Clamp01(queen.SkillStageTime / Mathf.Max(0.05f, s.queenWindup)) : 0f;
                    h.c.Show(winding);
                    h.c.t.position = h.b.t.position + h.a.t.up * (0.08f + 0.78f * slide * k) + h.a.t.forward * 0.025f;
                    h.c.t.rotation = h.a.t.rotation;
                    h.c.t.localScale = new Vector3(0.05f, 0.12f, 0.03f) * k;
                    h.c.colors = new Palette { main = Color.white, light = Color.white, deep = Color.white, ink = Color.white };
                    float flash = clock < h.flashUntil ? 1f : 0f;
                    h.a.flash = h.b.flash = flash;
                    h.a.Apply();
                    h.b.Apply();
                    h.c.Apply();
                    return true;
                };
            }
            else Keep(queen, "sword", () => new Fx());
        }

        const float BladeReach = 0.9f;   // hand → tip of the sword at full size

        /// <summary>The sword in the queen's hand (R93): pointing where her swing says (RagdollPawn.QueenBladeDir), its
        /// flat across the swing so the edge leads, and never into the floor (it used to carry on from the forearm and
        /// went through the floor with the arm hanging or cutting low): a tip that would go under the floor is lifted
        /// round the hand until it clears it.</summary>
        static void PlaceBlade(RagdollPawn queen, Transform blade, Transform guard, float scale)
        {
            Vector3 hand = queen.bodies[(int)BodyId.HandR].position;
            Vector3 dir = queen.QueenBladeDir;
            float reach = BladeReach * scale, floor = queen.FeetPoint.y + 0.09f;
            if (reach > 1e-3f && hand.y + dir.y * reach < floor)
            {
                float up = Mathf.Clamp((floor - hand.y) / reach, -1f, 1f);
                dir = FlatDir(dir, queen.Facing) * Mathf.Sqrt(Mathf.Max(0f, 1f - up * up)) + Vector3.up * up;
            }
            // The swing's plane: ahead and up-left (its normal is the blade's thickness).
            Vector3 f = FlatDir(queen.Facing, Vector3.forward), r = Vector3.Cross(Vector3.up, f);
            Vector3 normal = Vector3.Cross(f, (Vector3.up * 0.82f - r * 0.57f).normalized);
            if (Mathf.Abs(Vector3.Dot(normal.normalized, dir)) > 0.95f) normal = r;
            Quaternion rot = Quaternion.LookRotation(Vector3.ProjectOnPlane(normal, dir).normalized, dir);
            guard.SetPositionAndRotation(hand, rot);
            guard.localScale = Vector3.one * scale;
            blade.SetPositionAndRotation(hand + dir * (0.47f * scale - 0.04f), rot);
            blade.localScale = Vector3.one * scale;
        }

        /// <summary>The queen's line of squares: faint while she aims, filling through the windup, full in its last six frames.</summary>
        void QueenLine(RagdollPawn queen, QueenHillSkillParams s)
        {
            var f = Keep(queen, "queen line", () =>
            {
                var fx = new Fx();
                for (int i = 0; i < 6; i++) fx.tiles.Add(new Tile(this, "Queen warning"));
                fx.step = (x, d) =>
                {
                    if (!Held(x) || queen == null) return false;
                    bool locked = queen.QhAimLocked;
                    Vector3 dir = FlatDir(queen.SlashDir, Vector3.forward);
                    // On the floors the slash runs along (R97; as R94 laid them, and the slash now goes where they are).
                    var squares = PathSquares(queen.SlashPath, dir, s.queenLength, s.square, queen.SlashReach, queen.SlashBlocked);
                    float strength = locked ? Charge(queen.SkillStageTime, s.queenWindup) : 0.18f;
                    // Only her own screen shows the aim; the others see the line once she commits (the windup).
                    bool show = locked || SideOf(queen) == Side.Self;
                    for (int i = 0; i < x.tiles.Count; i++)
                    {
                        var t = x.tiles[i];
                        bool on = show && i < squares.Count;
                        if (on) t.Floor(squares[i].at, dir, Vector2.one * s.square);
                        // Past the wall that stops the slash, the squares stay faint.
                        bool past = on && squares[i].past;
                        t.fade = on ? (past ? 0.35f : 1f) : 0f;
                        Warn(t, queen, past ? 0.15f : strength);
                    }
                    return true;
                };
                return fx;
            });
        }

        // ----- rook

        void RookAim(RagdollPawn rook, QueenHillSkillParams s)
        {
            var f = Keep(rook, "rook aim", () =>
            {
                var fx = new Fx();
                fx.tiles.Add(new Tile(this, "Rook square"));
                fx.tiles.Add(new Tile(this, "Ally square"));
                fx.tiles.Add(new Tile(this, "Ally ring"));
                fx.tiles.Add(new Tile(this, "Consent clock"));
                for (int i = 0; i < 14; i++) fx.puffs.Add(new Puff(this, Rook, meshBall) { ink = 0.4f, lump = 0f });
                fx.step = (x, d) =>
                {
                    if (!Held(x) || rook == null) return false;
                    var target = rook.QhAimTarget;
                    bool locked = rook.QhAimLocked, valid = rook.QhAimValid && target != null;
                    var mine = x.tiles[0];
                    var theirs = x.tiles[1];
                    var ring = x.tiles[2];
                    var clockTile = x.tiles[3];
                    Vector3 a = rook.FeetPoint + Vector3.up * 0.016f;
                    mine.Floor(a, rook.Facing, Vector2.one * s.square);
                    Warn(mine, rook, locked ? 0.9f : 0.45f, 0f, valid ? (Palette?)null : Grey);
                    mine.fade = target != null ? 1f : 0.4f;
                    mine.Apply();
                    if (target == null)
                    {
                        theirs.fade = ring.fade = clockTile.fade = 0f;
                        theirs.Apply();
                        ring.Apply();
                        clockTile.Apply();
                        foreach (var p in x.puffs) p.t.gameObject.SetActive(false);
                        return true;
                    }
                    Vector3 b = target.FeetPoint + Vector3.up * 0.016f;
                    var tp = valid ? PieceColors(target) : Grey;
                    theirs.Floor(b, rook.Facing, Vector2.one * s.square);
                    Warn(theirs, target, locked ? 0.9f : 0.5f, 0f, tp);
                    theirs.fade = 1f;
                    theirs.Apply();
                    ring.Floor(b + Vector3.up * 0.003f, Vector3.forward, Vector2.one * 1.1f);
                    ring.shape = 1f;
                    ring.inner = 0.84f;
                    ring.fill = A(valid ? (SideOf(target) == Side.Enemy ? TeamEnemy.main : TeamAlly.main) : Grey.main, 1f);
                    ring.core = Color.clear;
                    ring.ink = A(valid ? TeamAlly.ink : Grey.ink, 1f);
                    ring.inkWidth = 0.02f;
                    ring.fade = 1f;
                    ring.Apply();
                    // Waiting for the yes: a clock fills round the ally's square.
                    clockTile.Floor(b + Vector3.up * 0.006f, Vector3.forward, Vector2.one * (s.square + 0.25f));
                    clockTile.shape = 1f;
                    clockTile.inner = 0.9f;
                    clockTile.fill = A(Rook.main, 1f);
                    clockTile.core = Color.clear;
                    clockTile.ink = A(Rook.ink, 1f);
                    clockTile.inkWidth = 0.02f;
                    clockTile.arc = locked ? Mathf.Clamp01(target.CastleAskAge / Mathf.Max(0.1f, s.dummyAccept)) : 0f;
                    clockTile.fade = locked ? 1f : 0f;
                    clockTile.Apply();
                    // The way they would fly: dots along the arch (grey if the way or a spot is blocked).
                    float top = Mathf.Max(a.y, b.y) + s.rookArc + 0.35f;
                    for (int i = 0; i < x.puffs.Count; i++)
                    {
                        var p = x.puffs[i];
                        p.t.gameObject.SetActive(true);
                        float t = (i + 1f) / (x.puffs.Count + 1f);
                        p.t.position = Arc(a, b, top, t);
                        float beat = Mathf.Repeat(clock * 1.6f - t, 1f);
                        p.t.localScale = Vector3.one * (0.11f + 0.04f * Mathf.Clamp01(1f - beat * 3f));
                        p.colors = valid ? Rook : Grey;
                        p.Apply(clock);
                    }
                    return true;
                };
                return fx;
            });
        }

        // ----- bishop

        void BishopHover(RagdollPawn bishop, QueenHillSkillParams s)
        {
            if (!hoverTiles.TryGetValue(bishop, out var h) || h.tile == null)
            {
                h = new HoverTile();
                h.tile = new Prop(this, RoundBox(new Vector3(1.1f, 0.12f, 1.1f), 0.05f), Bishop, "Hover tile") { inkWidth = 0.02f };
                h.shadow = new Tile(this, "Hover shadow");
                hoverTiles[bishop] = h;
                float born = clock;
                var f = Keep(bishop, "hover", () => new Fx());
                f.props.Add(h.tile);
                f.tiles.Add(h.shadow);
                f.end = () => hoverTiles.Remove(bishop);
                f.step = (fx, d) =>
                {
                    if (bishop == null) return false;
                    float age = clock - born;
                    bool dropping = h.dropAt >= 0f || !Held(fx);
                    if (h.dropAt < 0f && !Held(fx)) h.dropAt = clock;
                    float since = dropping ? clock - h.dropAt : 0f;
                    // Dropping: three quick blinks, then it pops into puffs.
                    if (dropping && since > 0.3f)
                    {
                        PuffBurst(h.tile.t.position, Bishop, 4, 0.5f, 0.15f, 0.35f);
                        return false;
                    }
                    Vector3 feet = bishop.Hips.position - Vector3.up * (bishop.standHeight + 0.09f);
                    float sc = Pop(age, 1.1f);
                    h.tile.t.SetPositionAndRotation(feet, Quaternion.LookRotation(bishop.Facing, Vector3.up) * Quaternion.Euler(0f, 45f, 0f));
                    h.tile.t.localScale = Vector3.one * sc;
                    h.tile.Show(!dropping || Mathf.Repeat(since / 0.1f, 1f) < 0.55f);
                    h.tile.Apply();
                    Vector3 ground = FloorUnder(feet, 0.2f) + Vector3.up * 0.014f;
                    h.shadow.Floor(ground, Quaternion.Euler(0f, 45f, 0f) * bishop.Facing, Vector2.one * (1.05f * sc));
                    h.shadow.fill = A(Bishop.ink, 0.3f);
                    h.shadow.ink = Color.clear;
                    h.shadow.rim = Color.clear;
                    h.shadow.stripe = Color.clear;
                    h.shadow.round = 0.05f;
                    h.shadow.fade = 1f;
                    h.shadow.Apply();
                    // Every quarter second a small violet puff drips from a corner of the tile.
                    h.drip += dt;
                    if (h.drip >= 0.25f && !dropping)
                    {
                        h.drip = 0f;
                        float ang = Random.Range(0, 4) * 90f + 45f;
                        Vector3 corner = feet + Quaternion.Euler(0f, ang, 0f) * h.tile.t.forward * 0.62f;
                        var drip = new Puff(this, Bishop) { ink = 0.3f };
                        var g = Run(0.45f, (x, dd) =>
                        {
                            float k = x.age / x.life;
                            drip.t.position = corner - Vector3.up * (0.5f * k * k + 0.05f);
                            drip.t.localScale = Vector3.one * 0.13f * Pop(x.age);
                            drip.dissolve = Mathf.Clamp01((k - 0.3f) / 0.7f);
                            drip.Apply(x.age);
                            return true;
                        });
                        g.puffs.Add(drip);
                    }
                    return true;
                };
            }
            else Keep(bishop, "hover", () => new Fx());
        }

        /// <summary>The bishop's aim: a dotted violet arc to the target and the 1.5 m square it would land on.</summary>
        void BishopAim(RagdollPawn bishop, QueenHillSkillParams s)
        {
            var f = Keep(bishop, "bishop aim", () =>
            {
                var fx = new Fx();
                fx.tiles.Add(new Tile(this, "Bishop target"));
                for (int i = 0; i < 20; i++) fx.puffs.Add(new Puff(this, Bishop, meshBall) { ink = 0.4f, lump = 0f });
                fx.step = (x, d) =>
                {
                    if (!Held(x) || bishop == null) return false;
                    bool valid = bishop.QhAimValid;
                    Vector3 to = bishop.QhAimPoint + Vector3.up * 0.016f;
                    var t = x.tiles[0];
                    t.Floor(to, bishop.Facing, Vector2.one * s.square);
                    Warn(t, bishop, valid ? 0.75f : 0.3f, 0f, valid ? (Palette?)null : Grey);
                    t.fade = 1f;
                    t.Apply();
                    Vector3 from = bishop.bodies[(int)BodyId.HandR].position;
                    float top = Mathf.Max(from.y, to.y) + 1.2f;
                    for (int i = 0; i < x.puffs.Count; i++)
                    {
                        var p = x.puffs[i];
                        float k = (i + 1f) / (x.puffs.Count + 1f);
                        Vector3 q = Vector3.Lerp(from, to, k);
                        q.y = Mathf.Lerp(from.y, to.y, k) + 4f * (top - Mathf.Max(from.y, to.y)) * k * (1f - k);
                        p.t.position = q;
                        p.t.localScale = Vector3.one * 0.08f;
                        p.colors = valid ? Bishop : Grey;
                        p.Apply(clock);
                    }
                    return true;
                };
                return fx;
            });
        }

        // ----- knight

        /// <summary>The landing mark: a horseshoe (a thick ring open to one side: white core, sky, ink) turning on the
        /// spot, over the 1.5 m square it lands in (grey and no square where it cannot go: too high). It shrinks as the
        /// knight comes down and is exactly its smallest at touchdown.</summary>
        void KnightAim(RagdollPawn knight, QueenHillSkillParams s, bool committed)
        {
            var f = Keep(knight, committed ? "knight go" : "knight aim", () =>
            {
                var fx = new Fx();
                fx.tiles.Add(new Tile(this, "Knight square"));
                fx.tiles.Add(new Tile(this, "Horseshoe"));
                fx.step = (x, d) =>
                {
                    if (!Held(x) || knight == null) return false;
                    bool valid = committed || knight.QhAimValid;
                    // R93: an enemy the aim has caught: the square under it and the horseshoe turning over its head.
                    var target = committed ? knight.LeapTarget : knight.QhAimTarget;
                    bool onHead = target != null && target.State == PawnState.Active;
                    Vector3 at = (onHead ? target.FeetPoint : committed ? knight.LeapSpot : knight.QhAimPoint) + Vector3.up * 0.016f;
                    var sq = x.tiles[0];
                    var shoe = x.tiles[1];
                    sq.Floor(at, knight.Facing, Vector2.one * s.square);
                    Warn(sq, knight, committed ? 0.95f : 0.6f);
                    sq.fade = valid ? 1f : 0f;
                    sq.Apply();
                    float r = committed && knight.QhFlying ? Mathf.Lerp(0.7f, 0.5f, knight.QhFlyProgress) : committed ? 0.5f : 0.7f;
                    if (onHead) r *= 0.75f;
                    float spin = clock * 90f;
                    Vector3 shoeAt = at + Vector3.up * 0.004f;
                    if (onHead)
                    {
                        Vector3 head = target.bodies[(int)BodyId.Head].position;
                        shoeAt = new Vector3(head.x, head.y + 0.24f + 0.03f * Mathf.Sin(clock * 9f), head.z);
                    }
                    shoe.Floor(shoeAt, Quaternion.Euler(0f, spin, 0f) * Vector3.forward, Vector2.one * (2f * r));
                    Horseshoe(shoe, valid ? Knight : Grey, 1f);
                    shoe.fade = 1f;
                    shoe.Apply();
                    return true;
                };
                return fx;
            });
        }

        void WindTrail(RagdollPawn knight)
        {
            var f = Keep(knight, "wind trail", () =>
            {
                var fx = new Fx();
                var strip = new Strip(this, Knight) { core = Knight.light, coreShare = 0.3f, inkShare = 0.22f };
                fx.strips.Add(strip);
                var path = new List<Vector3>();
                float release = -1f;
                fx.step = (x, d) =>
                {
                    if (knight == null) return false;
                    if (Held(x)) path.Add(knight.Hips.position);
                    else if (release < 0f) release = x.age;
                    while (path.Count > 27) path.RemoveAt(0);   // 0.45 s
                    if (release >= 0f && path.Count > 0) path.RemoveAt(0);
                    if (path.Count < 2) return release < 0f;
                    strip.Build(path, i => 0.5f * i / Mathf.Max(1f, path.Count - 1f), Eye);
                    strip.taper = 0f;
                    strip.Apply();
                    return true;
                };
                return fx;
            });
        }

        // ----- pawn

        void SpeedWedges(RagdollPawn pawn)
        {
            var f = Keep(pawn, "wedges", () =>
            {
                var fx = new Fx();
                var p = PieceColors(pawn);
                for (int i = 0; i < 3; i++) fx.strips.Add(new Strip(this, p) { core = Dust.light, coreShare = 0.35f, inkShare = 0.24f });
                float release = -1f;
                fx.step = (x, d) =>
                {
                    if (pawn == null) return false;
                    if (!Held(x) && release < 0f) release = x.age;
                    float fade = release < 0f ? 1f : 1f - (x.age - release) / (8f * F);
                    if (fade <= 0f) return false;
                    Vector3 dir = FlatDir(pawn.SkillDirection, pawn.Facing);
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    Vector3 hips = pawn.Hips.position;
                    float[] off = { -0.22f, 0.05f, 0.24f }, high = { 0.05f, 0.25f, -0.05f }, len = { 0.9f, 0.7f, 0.8f };
                    for (int i = 0; i < 3; i++)
                    {
                        var s = x.strips[i];
                        Vector3 head = hips - dir * 0.3f + side * off[i] + Vector3.up * high[i];
                        s.Build(new List<Vector3> { head - dir * len[i], head }, k => 0.12f, Eye);
                        s.taper = 1f;
                        s.fade = fade;
                        s.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        // ----- the Pawn Rush rook the pawn dodges (6.B): drawn as the other side's warning row and a charge

        void WatchChargingRook(RagdollPawn rook)
        {
            float square = S != null ? S.square : 1.5f;
            var pr = rook.PawnRushSkills;
            bool locked = rook.SkillStage == SkillStage.Windup && !rook.SkillAiming;
            if (locked)
            {
                var f = Keep(rook, "charge row", () =>
                {
                    var fx = new Fx();
                    for (int i = 0; i < 4; i++) fx.tiles.Add(new Tile(this, "Charge warning"));
                    Vector3 from = rook.FeetPoint;
                    Vector3 dir = FlatDir(rook.SkillDirection, rook.Facing);
                    var squares = SquaresAlong(from, dir, square * 4f, square);
                    float released = -1f;
                    fx.step = (x, d) =>
                    {
                        if (rook == null) return false;
                        bool charging = rook.SkillDashing || rook.SkillStage != SkillStage.Windup;
                        if (charging && released < 0f) released = x.age;
                        float a = released >= 0f ? (x.age - released) / F : -1f;
                        for (int i = 0; i < x.tiles.Count; i++)
                        {
                            var t = x.tiles[i];
                            t.Floor(squares[Mathf.Min(i, squares.Count - 1)], dir, Vector2.one * square);
                            t.fade = released < 0f ? 1f : 1f - Mathf.Clamp01((a - 2f) / 4f);
                            Warn(t, rook, released < 0f ? Charge(rook.SkillStageTime, pr.rookLock) : 1f, a >= 0f && a < 2f ? 1f : 0f);
                        }
                        return released < 0f || a < 6f;
                    };
                    return fx;
                });
            }
            if (rook.SkillDashing)
            {
                var f = Keep(rook, "charge lines", () =>
                {
                    var fx = new Fx();
                    for (int i = 0; i < 4; i++) fx.strips.Add(new Strip(this, Rook) { core = Rook.light, coreShare = 0.3f });
                    float release = -1f;
                    fx.step = (x, d) =>
                    {
                        if (rook == null) return false;
                        if (!Held(x) && release < 0f) release = x.age;
                        float fade = release < 0f ? 1f : 1f - (x.age - release) / (8f * F);
                        if (fade <= 0f) return false;
                        Vector3 dir = FlatDir(rook.SkillDirection, rook.Facing);
                        Vector3 side = Vector3.Cross(Vector3.up, dir);
                        Vector3 hips = rook.Hips.position;
                        for (int i = 0; i < 4; i++)
                        {
                            var s = x.strips[i];
                            Vector3 head = hips - dir * 0.35f + side * ((i - 1.5f) * 0.2f) + Vector3.up * ((i % 2) * 0.25f);
                            s.Build(new List<Vector3> { head - dir * (1f + 0.2f * i), head }, k => 0.13f, Eye);
                            s.taper = 1f;
                            s.fade = fade;
                            s.Apply();
                        }
                        return true;
                    };
                    return fx;
                });
            }
        }

        // ---------------------------------------------------------------- squash (the skin's root bone, after the pose)

        enum SquashKind { Down, Flatten, Block, Crouch }

        class Squash
        {
            public RagdollPawn pawn;
            public Transform bone;
            public Vector3 baseScale, lastOffset, lastSet;
            public int axis;
            public readonly List<(SquashKind kind, float born, float hold)> marks = new List<(SquashKind, float, float)>();
            public float crouch;

            public void Restore()
            {
                if (bone == null) return;
                if (bone.position == lastSet) bone.position -= lastOffset;
                bone.localScale = baseScale;
            }
        }

        readonly Dictionary<RagdollPawn, Squash> squashes = new Dictionary<RagdollPawn, Squash>();

        void AddSquash(RagdollPawn pawn, SquashKind kind, float hold = 0f)
        {
            if (pawn == null || pawn.skin == null) return;
            if (!squashes.TryGetValue(pawn, out var s))
            {
                Transform bone = pawn.skin.rootBone != null ? pawn.skin.rootBone : pawn.skin.transform;
                int axis = 1;
                float best = -1f;
                for (int a = 0; a < 3; a++)
                {
                    Vector3 dir = a == 0 ? bone.right : a == 1 ? bone.up : bone.forward;
                    float dd = Mathf.Abs(Vector3.Dot(dir, Vector3.up));
                    if (dd > best) { best = dd; axis = a; }
                }
                s = new Squash { pawn = pawn, bone = bone, baseScale = bone.localScale, axis = axis };
                squashes[pawn] = s;
            }
            if (kind == SquashKind.Crouch) s.crouch = pawn.QhCrouch;
            else s.marks.Add((kind, clock, hold));
        }

        /// <summary>Height of one squash at this age: 1 = none.</summary>
        static float SquashHeight(SquashKind kind, float age, float hold)
        {
            float a = age / F;
            switch (kind)
            {
                case SquashKind.Down:   // 6 frames down to 0.8, back with a 1.05 overshoot
                    if (a < 3f) return Mathf.Lerp(1f, 0.8f, a / 3f);
                    if (a < 7f) return Mathf.Lerp(0.8f, 1.05f, (a - 3f) / 4f);
                    if (a < 11f) return Mathf.Lerp(1.05f, 1f, (a - 7f) / 4f);
                    return -1f;
                case SquashKind.Block:
                    if (a < 2f) return Mathf.Lerp(1f, 0.9f, a / 2f);
                    if (a < 6f) return Mathf.Lerp(0.9f, 1f, (a - 2f) / 4f);
                    return -1f;
                case SquashKind.Flatten:   // flat in 4 frames, held, then a springy way back (1.15 overshoot)
                    if (a < 4f) return Mathf.Lerp(1f, 0.42f, a / 4f);
                    if (age < hold) return 0.42f;
                    float t = age - hold;
                    if (t > 0.35f) return -1f;
                    return 1f + (0.42f - 1f) * Mathf.Exp(-t * 9f) * Mathf.Cos(t * 18f) + 0.15f * Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI) * (1f - t / 0.35f);
            }
            return -1f;
        }

        void ApplySquashes()
        {
            var done = new List<RagdollPawn>();
            foreach (var kv in squashes)
            {
                var s = kv.Value;
                if (s.bone == null || kv.Key == null) { done.Add(kv.Key); continue; }
                if (s.bone.position == s.lastSet) s.bone.position -= s.lastOffset;   // nothing re-posed it since last frame
                float sy = 1f;
                for (int i = s.marks.Count - 1; i >= 0; i--)
                {
                    var m = s.marks[i];
                    float h = SquashHeight(m.kind, clock - m.born, m.hold);
                    if (h < 0f) { s.marks.RemoveAt(i); continue; }
                    sy = Mathf.Min(sy, h) + Mathf.Max(0f, h - 1f);
                }
                // The crouch (B): lower and a little wider while the pawn is down for its dash.
                float crouch = kv.Key.QhCrouch;
                if (crouch > 0.01f) sy = Mathf.Min(sy, 1f - 0.12f * crouch);
                if (s.marks.Count == 0 && crouch <= 0.01f)
                {
                    s.bone.localScale = s.baseScale;
                    s.lastOffset = Vector3.zero;
                    done.Add(kv.Key);
                    continue;
                }
                float sx = 1f + (1f - Mathf.Min(sy, 1f)) * 0.55f;
                var sc = s.baseScale;
                for (int a = 0; a < 3; a++) sc[a] *= a == s.axis ? sy : sx;
                s.bone.localScale = sc;
                float above = Mathf.Max(0f, s.bone.position.y - (s.pawn.Hips.position.y - s.pawn.standHeight));
                s.lastOffset = Vector3.down * above * (1f - sy);
                s.bone.position += s.lastOffset;
                s.lastSet = s.bone.position;
            }
            foreach (var p in done)
            {
                if (p != null && squashes.TryGetValue(p, out var s) && s.marks.Count == 0) s.bone.localScale = s.baseScale;
                squashes.Remove(p);
            }
        }

        // ---------------------------------------------------------------- per frame

        void LateUpdate()
        {
            frame++;
            var cam = ViewCamera;
            if (cam != prepared && cam != null)
            {
                PawnRushSkillFx.PrepareCamera(cam);   // HDR and the hand-made bloom, as in the Pawn Rush test
                prepared = cam;
            }
            if (cam != null)
            {
                var bloom = cam.GetComponent<PawnRushSkillBloom>();
                if (bloom != null) bloom.enabled = effects;
            }
            Vector3 sun = new Vector3(0.35f, 0.79f, -0.5f);
            var light = RenderSettings.sun;
            if (light != null) sun = -light.transform.forward;
            Shader.SetGlobalVector("_FxSun", sun);

            if (effects) WatchPieces();
            StepEffects(dt);
            StepFlashes();
            ApplySquashes();

            if (shakeLeft > 0f && cam != null)
            {
                shakeLeft -= dt;
                float k = Mathf.Clamp01(shakeLeft / Mathf.Max(1e-4f, shakeTotal));
                float t = clock * 60f;
                Vector3 off = new Vector3(Mathf.Sin(t * 1.7f + 0.3f), Mathf.Sin(t * 2.3f + 1.1f), Mathf.Sin(t * 1.3f + 2.2f)) * (shakeAmp * k);
                cam.transform.position += cam.transform.TransformDirection(off);
            }
        }
    }
}
