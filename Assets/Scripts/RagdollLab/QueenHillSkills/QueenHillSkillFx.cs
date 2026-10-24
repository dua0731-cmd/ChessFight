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
    ///
    /// R141 (승규 님 10-14, with pictures of the castling ribbon, the castling arc's dots and the bishop's aim): what was
    /// drawn out of a springing piece's back (the pawn's speed wedges, the knight's wind ribbon and its white take-off
    /// ring) is the air it shoves back (<see cref="SkillAir"/>, as in Pawn Rush); the castling has no trapdoors or ribbon
    /// arch, the two pieces tell it (spring off, a beat where they pass, set down hard); the castling arc's and the
    /// bishop's dots are polished beads; the bishop's floor X is gone; the knight's horseshoe is forged steel, and its
    /// landing stamps the Pawn Rush knight's badge (none on a head).
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
        /// <summary>Hold every effect still (the probe's freeze: a moment looked at from several cameras). A hit stop does not
        /// stop the effects; this does.</summary>
        public static bool Hold;

        Transform root;
        LabGame game;
        QueenHillSkillBed bed;
        Camera prepared;
        float dt, rate = 1f, clock;

        // hit stop and shake
        float stopLeft, stopResume = 1f;
        bool stopping;
        float shakeAmp, shakeLeft, shakeTotal;

        // The person at this PC: P1 offline, this PC's own pawn online (R111).
        RagdollPawn Viewer => game != null ? game.LocalPawn : bed != null ? bed.P1 : null;
        // The moment being drawn: online its hit stop holds only these two pieces' picture (D-S2).
        RagdollPawn stopBy, stopTarget;
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
            BuildBoardLook();
            // R138 (승규 님: "임펙트를 좀 더 사실 적으로 … 운석이나 돌맹이 같아 보여"): real dust in place of the cartoon puffs.
            dust = new SkillDust(root);
            if (!dust.Ready) dust = null;
            // R141: the air shoved back as a piece springs off (SkillAir), in place of what was drawn out of its back.
            air = new SkillAir(root);
            RagdollPawn.QueenHillFx += OnFx;
        }

        /// <summary>R138: the dust the puffs are drawn as (null: the shader is missing, the cartoon puffs stay).</summary>
        SkillDust dust;

        /// <summary>R141: the air a springing piece shoves back (SkillAir).</summary>
        SkillAir air;

        /// <summary>R141 (승규 님 10-14, Pawn Rush and Queen of the Hill both: "f 키를 누르면 뒤에(엉덩이 부분)에 임펙트가 나가는데
        /// 다른 임펙트로 해줘(공기 임팩트)"): the air a piece shoves back from just behind its hips as it springs off, against
        /// <paramref name="dir"/> and <paramref name="down"/> toward the floor: bent air and white vapour (SkillAir), in place
        /// of the lines drawn out of its back (the pawn's speed wedges, the knight's wind ribbon).</summary>
        void AirKick(RagdollPawn pawn, Vector3 dir, float strength, float down)
        {
            if (air == null || pawn == null) return;
            Vector3 way = FlatDir(dir, pawn.Facing);
            air.Kick(pawn.Hips.position - way * 0.26f - Vector3.up * 0.04f, (-way + Vector3.down * down).normalized, strength, dust);
        }

        void OnDestroy()
        {
            RagdollPawn.QueenHillFx -= OnFx;
            if (stopping) Time.timeScale = stopResume;
            foreach (var f in live) f.Destroy();
            live.Clear();
            foreach (var b in flashes.Values) b.Restore();
            foreach (var s in squashes.Values) s.Restore();
            dust?.Destroy();
            dust = null;
            air?.Destroy();
            air = null;
            if (root != null) Destroy(root.gameObject);
            DestroyKit();
            DestroyCarpetMaterials();
            EndBreaks();
        }

        // ---------------------------------------------------------------- time, hit stop, shake

        void Update()
        {
            // Effect time: the game's own pace (slow motion slows the effects with it), but a hit stop does not stop
            // them; a recording steps one frame of 1/60 s at a time.
            float real = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (Time.timeScale > 0f && !stopping) rate = Time.timeScale;
            dt = Hold ? 0f : real * rate;
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

        /// <summary>A hit stop of so many frames (3 weak, 4 medium, 5-6 strong). Online (R111, D-S2) the game must not stop:
        /// only the hitter's and the hit piece's picture holds still on this screen.</summary>
        void HitStop(int frames)
        {
            if (!hitStop || frames <= 0) return;
            if (game != null && game.NetworkControlled)
            {
                if (stopBy != null) stopBy.HoldVisual(frames * F);
                if (stopTarget != null) stopTarget.HoldVisual(frames * F);
                if (LabNetLink.Trace) LabNetLink.Log($"hit stop{frames} frames on {(stopBy != null ? stopBy.DisplayName : "-")} and {(stopTarget != null ? stopTarget.DisplayName : "-")} (this screen only)");
                return;
            }
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
            stopBy = e.by;
            stopTarget = e.target;
            switch (e.kind)
            {
                case QueenHillFxKind.KingWindup: KingWindup(e); break;
                case QueenHillFxKind.KingWard: KingWave(e); break;
                case QueenHillFxKind.WardOn: WardOn(e); break;
                case QueenHillFxKind.WardBlock: WardBlock(e); break;
                case QueenHillFxKind.QueenSlash: QueenSlash(e); break;
                case QueenHillFxKind.QueenHit: Hit(e, look.Queen, 5, 0.16f, 4); break;
                case QueenHillFxKind.QueenWall: WallBang(e, look.Queen); break;
                case QueenHillFxKind.RookAccept: RookAccept(e); break;
                case QueenHillFxKind.RookSwap: RookSwap(e); break;
                case QueenHillFxKind.BishopRise: BishopRise(e); break;
                case QueenHillFxKind.BishopThrow: BishopThrow(e); break;
                case QueenHillFxKind.BishopImpact: BishopImpact(e); break;
                case QueenHillFxKind.BishopHit: Hit(e, look.Bishop, 6, 0.2f, 4); break;   // 10-12: the swords hit hard
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
            if (t.board)
            {
                BoardWarn(t, caster, strength, flash, fillOverride);
                return;
            }
            var side = SideOf(caster);
            Color sideFill = side == Side.Self ? look.SelfFill : side == Side.Ally ? look.TeamAlly.main : look.TeamEnemy.main;
            Color sideInk = side == Side.Self ? look.SelfInk : side == Side.Ally ? look.TeamAlly.ink : look.TeamEnemy.ink;
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
                t.rim = A(side == Side.Self ? look.SelfFill : sideFill, Mathf.Lerp(0.45f, 0.95f, strength));
            }
            t.inkWidth = 0.05f;
            t.rimWidth = 0.09f;
            t.round = 0.07f;
            t.shape = 0f;
            t.stripe = side == Side.Enemy ? A(look.TeamEnemy.deep, 0.55f * strength) : Color.clear;
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
        /// a lower tier, up a step — as 승규 님 asked; R95's straight line at her height is undone): the middle of each on
        /// the way's floor; past a wall that stops the slash, faint squares on whatever floor is there.</summary>
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
                if (blocked && c > reach + 0.3f) list.Add((FloorUnder(flat, 1.5f), true));
                else list.Add((new Vector3(flat.x, PathFloor(path, c), flat.z), false));
            }
            return list;
        }

        /// <summary>The floor of the slash's way <paramref name="along"/> metres from her feet.</summary>
        static float PathFloor(IReadOnlyList<Vector3> path, float along) => RagdollPawn.SlashPathFloor(path, along);

        /// <summary>Lay one of the queen's squares over the floor (R98): bent down a slope and over a crest, cut at a step's
        /// face — every point of it on the floor her slash finds there (on its way's floor, or past a wall the floor under
        /// the square), so the squares lie where the slash will run and who stands on them is who it takes.</summary>
        static void LaySquare(Tile t, RagdollPawn queen, IReadOnlyList<Vector3> path, Vector3 dir, (Vector3 at, bool past) square, float size)
        {
            if (queen == null || path == null || path.Count == 0)
            {
                t.Floor(square.at + Vector3.up * 0.016f, dir, Vector2.one * size);
                return;
            }
            Vector3 start = path[0];
            t.Drape(square.at, dir, Vector2.one * size, p =>
            {
                float near = square.past ? square.at.y : PathFloor(path, Mathf.Max(0f, Vector3.Dot(p - start, dir)));
                return queen.SlashFloorAt(p, near);
            });
        }

        /// <summary>A windup's strength: rises with the windup, full in its last six frames (A).</summary>
        static float Charge(float time, float total)
        {
            if (total <= 0f) return 1f;
            if (time >= total - 6f * F) return 1f;
            return Mathf.Lerp(0.3f, 0.85f, Mathf.Clamp01(time / Mathf.Max(F, total - 6f * F)));
        }

        /// <summary>Squares that flash white for two frames and are gone in six (the moment a warning turns into the move).</summary>
        void Release(List<Vector3> squares, Vector3 along, RagdollPawn caster, float square, Palette? fill = null)
        {
            var tiles = new List<Tile>();
            for (int i = 0; i < squares.Count; i++)
            {
                var t = new Tile(this, "Warning release");
                t.Floor(squares[i], along, Vector2.one * square);
                tiles.Add(t);
            }
            Release(tiles, caster, fill);
        }

        /// <summary>The same flash for squares already laid (the queen's, draped over the floor: R98).</summary>
        void Release(List<Tile> tiles, RagdollPawn caster, Palette? fill = null)
        {
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
                t.metal = p.metal;
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

        /// <summary>Cream dust puffs rolling out on the floor at uneven angles and eaten from below. R138: real dust rolling
        /// out over the floor (<see cref="SkillDust"/>).</summary>
        void DustRing(Vector3 at, int n, float radius, Palette? colors = null, float size = 0.3f)
        {
            if (dust != null)
            {
                var c = colors ?? look.Dust;
                dust.Ring(at, n, radius, c.light, c.deep, size);
                return;
            }
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                float ang = (start + i * 360f / n + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var puff = new Puff(this, colors ?? look.Dust) { ink = 0.32f };
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

        /// <summary>Puffs in a piece's colours bursting from a point (an impact's dust, a pop). R138: real dust, a little
        /// tinted by the palette (<see cref="SkillDust"/>).</summary>
        void PuffBurst(Vector3 at, Palette colors, int n, float radius, float size, float life = 0.45f, float up = 0.3f)
        {
            if (dust != null)
            {
                dust.Burst(at, colors.light, colors.deep, n, radius, size, life, up);
                return;
            }
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
            PuffBurst(at, look.Dust, 2, 0.35f, 0.18f);
        }

        /// <summary>A skill's hit: body flash, drops, hit stop and shake by strength. The knockdown kit follows on Down.
        /// R138 (승규 님: "모든 기물이 스킬 사용 후 적 맞출 때 나오는 반원 이펙트인데 빼주고"): no ring at the hit.</summary>
        void Hit(QueenHillFxEvent e, Palette p, int stopFrames, float shakeMetres, int drops)
        {
            if (e.count == 1) return;   // guarded (WardBlock draws it)
            FlashBody(e.target, p.main);
            Drops(e.at, e.dir, p, drops);
            HitStop(stopFrames);
            Shake(e.by, e.target, shakeMetres, stopFrames + 4);
        }

        void WallBang(QueenHillFxEvent e, Palette p)
        {
            ImpactRing(e.at - e.dir * 0.05f, -e.dir, p, 0.4f, 1.1f);
            PuffBurst(e.at - e.dir * 0.2f, look.Dust, 3, 0.5f, 0.3f);
            Shake(e.by, null, 0.08f, 6);
        }

        /// <summary>The knockdown kit: once the piece lies, an ink ring opening on the floor, four cream dust puffs, a squash
        /// (and, while <see cref="SkillInkKit.CapturedSquare"/>, the captured square in the hitter's colour under it: scale
        /// 1.1 → 1 in six frames, held 30, faded in 15; off since R129, 승규 님: "상대가 넘어질 때 생기는 발판을 다 지워줘").</summary>
        void Down(QueenHillFxEvent e)
        {
            var target = e.target;
            if (target == null) return;
            var p = PieceColors(e.by);
            bool stamped = false;
            var tile = SkillInkKit.CapturedSquare ? new Tile(this, "Captured square") : null;
            // R138: with real dust the fall throws up dust only: no ink ring drawn on the floor.
            var ring = dust == null ? new Tile(this, "Floor ink ring") : null;
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
                if (tile != null)
                {
                    tile.Floor(at, Vector3.forward, Vector2.one * (square * (a < 6f ? Mathf.Lerp(1.1f, 1f, a / 6f) : 1f)));
                    tile.fill = A(p.main, 0.8f);
                    tile.ink = A(p.ink, 1f);
                    tile.inkWidth = 0.08f;
                    tile.rim = Color.clear;
                    tile.stripe = Color.clear;
                    tile.round = 0.06f;
                    if (tile.board) BoardStamp(tile, p);
                    tile.fade = a < 36f ? 1f : 1f - (a - 36f) / 15f;
                    tile.Apply();
                }
                if (ring != null)
                {
                    float rr = Mathf.Lerp(0.3f, 0.9f, EaseOut(a / 10f));
                    ring.Floor(at + Vector3.up * 0.003f, Vector3.forward, Vector2.one * (2f * rr));
                    FlatRing(ring);
                    ring.shape = 1f;
                    ring.inner = Mathf.Max(0.01f, 1f - 0.06f / rr);
                    ring.fill = A(look.Dust.ink, 0.6f);
                    ring.ink = Color.clear;
                    ring.core = Color.clear;
                    ring.fade = 1f - Mathf.Clamp01((a - 10f) / 12f);
                    ring.Apply();
                }
                return a < (tile != null ? 51f : 22f);
            });
            if (tile != null) f.tiles.Add(tile);
            if (ring != null) f.tiles.Add(ring);
        }

        // ---------------------------------------------------------------- king: close guard (B)

        readonly Dictionary<RagdollPawn, Fx> kingRings = new Dictionary<RagdollPawn, Fx>();

        /// <summary>The windup: 24 small tiles in a ring at the guard's reach (A, from the king's check); emerald, with the
        /// viewer's side rim. R133 (승규 님 10-14: "스킬 미리 보기가 각 칸마다 따로 차징이 돼 … 같이 차징 되게"): they come on
        /// together and charge as one (they lit clockwise a third at a time, like a clock).</summary>
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
                    float lit = released >= 0f ? 1f : Mathf.Clamp01(fx.age / (4f * F));
                    float a = released >= 0f ? (fx.age - released) / F : -1f;
                    t.fade = released >= 0f ? 1f - Mathf.Clamp01((a - 2f) / 4f) : lit;
                    Warn(t, king, released >= 0f ? 1f : Charge(fx.age, windup), released >= 0f && a < 2f ? 1f : 0f);
                }
                return released < 0f || (fx.age - released) < 6f * F;
            };
        }

        /// <summary>The release: the sceptre comes down, a wave rolls out to the reach, puffs ride its edge. R130 (승규 님:
        /// "그냥 짧게 팡~만 하는데 그 연출을 살짝만 길게"): the wave rolls out over 14 frames (was 10) and stays to frame 24
        /// before it fades (gone at 48, was 30), a thinner light ring follows it a beat behind and a little wider, and the
        /// king's crown rises over his head (<see cref="CrownBurst"/>).</summary>
        void KingWave(QueenHillFxEvent e)
        {
            var king = e.by;
            float radius = e.size;
            Vector3 c = e.at + Vector3.up * 0.02f;
            var wave = new Tile(this, "Guard wave");
            var echo = new Tile(this, "Guard echo");
            var f = Run(48f * F, (fx, d) =>
            {
                float a = fx.age / F;
                float k = a < 14f ? EaseOut(a / 14f) : 1f;
                float r = Mathf.Max(0.2f, radius * k);
                wave.Floor(c, Vector3.forward, Vector2.one * (2f * r));
                FlatRing(wave);
                wave.metal = look.King.metal;
                wave.shape = 1f;
                wave.inner = Mathf.Clamp01(1f - 0.4f / r);
                wave.fill = A(look.King.main, 0.9f);
                wave.core = A(Color.white, 1f);
                wave.coreWidth = 0.06f;
                wave.ink = A(look.King.ink, 1f);
                wave.inkWidth = 0.04f;
                wave.fade = a < 24f ? 1f : 1f - (a - 24f) / 24f;
                wave.arc = 1f;
                wave.Apply();
                // The echo: a thin light ring five frames behind, a little wider.
                float b = a - 5f;
                float ke = b < 16f ? EaseOut(Mathf.Max(0f, b) / 16f) : 1f;
                float re = Mathf.Max(0.2f, radius * 1.12f * ke);
                echo.Floor(c + Vector3.up * 0.002f, Vector3.forward, Vector2.one * (2f * re));
                FlatRing(echo);
                echo.metal = look.King.metal;
                echo.shape = 1f;
                echo.inner = Mathf.Clamp01(1f - 0.11f / re);
                echo.fill = A(look.King.light, 0.95f);
                echo.core = Color.clear;
                echo.ink = A(look.King.ink, 0.85f);
                echo.inkWidth = 0.02f;
                echo.arc = 1f;
                echo.fade = b <= 0f ? 0f : b < 16f ? 1f : 1f - Mathf.Clamp01((b - 16f) / 14f);
                echo.Apply();
                return true;
            });
            f.tiles.Add(wave);
            f.tiles.Add(echo);
            for (int i = 0; i < 8; i++)
            {
                float ang = (i / 8f) * Mathf.PI * 2f + 0.3f;
                Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                var puff = new Puff(this, look.King) { ink = 0.3f };
                float size = Random.Range(0.22f, 0.3f);
                var g = Run(0.7f, (fx, d) =>
                {
                    float a = fx.age / F;
                    float k = a < 14f ? EaseOut(a / 14f) : 1f;
                    puff.t.position = c + dir * (radius * k) + Vector3.up * (size * 0.6f);
                    puff.t.localScale = Vector3.one * (size * Pop(fx.age, 1.2f));
                    puff.dissolve = Mathf.Clamp01((fx.age / fx.life - 0.4f) / 0.6f);
                    puff.Apply(fx.age);
                    return true;
                });
                g.puffs.Add(puff);
            }
            if (scepters.TryGetValue(king, out var sceptre)) sceptre.flashUntil = clock + 3f * F;
            CrownBurst(king);
            HitStop(3);
            Shake(king, null, 0.08f, 10);
        }

        /// <summary>R130: the king's crown (the sceptre's, big) over his head as the guard goes out: it pops in (white for
        /// three frames, 1.3 then 1 by frame 8), turns slowly and bobs, then floats up and shrinks away by 0.9 s.</summary>
        void CrownBurst(RagdollPawn king)
        {
            if (king == null) return;
            var head = king.bodies[(int)BodyId.Head];
            var crown = new Prop(this, SkillMeshes.SceptreCrown(), look.King, "Guard crown") { inkWidth = 0.02f };
            // R155: the Tripo crown (about 1 m across) over the procedural one (0.17 m), a little wider.
            var real = ModelOn(LoadModel("KingModels/KingCrown"), crown.t, 0.2f);
            var f = Run(0.9f, (fx, d) =>
            {
                if (king == null || head == null) return false;
                float a = fx.age / F;
                float s = a < 4f ? Mathf.Lerp(0f, 1.3f, a / 4f) : a < 8f ? Mathf.Lerp(1.3f, 1f, (a - 4f) / 4f) : 1f;
                float leave = Mathf.Clamp01((fx.age - 0.55f) / 0.35f);
                s *= 1f - leave * leave;
                Vector3 at = head.position + Vector3.up * (0.48f + 0.28f * EaseOut(leave) + 0.025f * Mathf.Sin(fx.age * 8f));
                crown.t.SetPositionAndRotation(at, Quaternion.Euler(0f, fx.age * 150f, 0f));
                crown.t.localScale = Vector3.one * (2.3f * Mathf.Max(0.001f, s));
                crown.flash = a < 3f ? 1f : 0f;
                crown.Apply();
                ShowModel(real, s > 0.01f, crown.flash, crown);
                return true;
            });
            f.props.Add(crown);
            PuffBurst(head.position + Vector3.up * 0.4f, look.King, 5, 0.38f, 0.11f, 0.5f, 0.25f);
        }

        /// <summary>An ally the guard reached: white for two frames, then the guard's colour (not a hit's); R130 a few
        /// little puffs of it rise round the ally.</summary>
        void WardOn(QueenHillFxEvent e)
        {
            FlashBody(e.target, look.King.main);
            if (e.target != null) PuffBurst(e.target.FeetPoint + Vector3.up * 0.25f, look.King, 3, 0.4f, 0.09f, 0.55f, 0.9f);
        }

        /// <summary>A guarded piece shrugging a hit off: puffs, a little squash. R138: no ring at the contact (승규 님: "적 맞출
        /// 때 나오는 반원 이펙트인데 빼주고").</summary>
        void WardBlock(QueenHillFxEvent e)
        {
            FlashBody(e.target, look.King.main);
            PuffBurst(e.at, look.King, 3, 0.35f, 0.16f, 0.35f);
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
            var lit = new List<Tile>();
            foreach (var q in PathSquares(path, dir, S != null ? S.queenLength : 6f, square, reach, e.count >= 1))   // 1 a wall, 2 a guard: no squares past it
            {
                if (q.past) continue;
                var t = new Tile(this, "Warning release");
                LaySquare(t, queen, path, dir, q, square);
                lit.Add(t);
            }
            Release(lit, queen);
            if (swords.TryGetValue(queen, out var sword)) sword.flashUntil = clock + 2f * F;

            // The swing (R93): a gold crescent along the path of the sword's tip, from low behind on the right up across
            // her front to high on the left (the sword passes straight ahead right now, so half of it is drawn at once).
            var swing = new Strip(this, look.Queen) { core = Color.white, coreShare = 0.28f, inkShare = 0.2f };
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
            var blade = new Strip(this, look.Queen) { core = Color.white, coreShare = 0.3f, inkShare = 0.2f };
            var low = new Strip(this, look.Queen) { core = Color.white, coreShare = 0.3f, inkShare = 0.22f };
            var trail = new Strip(this, look.Queen) { core = look.Queen.light, coreShare = 0.25f, inkShare = 0.25f };
            float life = travel + 10f * F;
            var g = Run(life, (fx, d) =>
            {
                float k = Mathf.Clamp01(fx.age / Mathf.Max(F, travel));
                float eaten = fx.age > travel ? (fx.age - travel) / (10f * F) : 0f;
                // R97: it rides the way's floor (down a drop, up a step) with its lower tip just over it; R98: smoothly up
                // or down a slope, its tip clear of the floor just behind it too.
                float dist = 0.6f + (reach - 0.6f) * k, floorY = Mathf.Max(PathFloor(path, dist), PathFloor(path, dist - 0.3f));
                Vector3 front = start + dir * dist;
                front.y = floorY + 0.72f;
                var up = new List<Vector3>();
                var flat = new List<Vector3>();
                for (int i = 0; i <= 14; i++)
                {
                    float ang = Mathf.Lerp(-62f, 62f, i / 14f) * Mathf.Deg2Rad, back = 0.5f * (Mathf.Cos(ang) - 1f);
                    up.Add(front + dir * (0.45f * (Mathf.Cos(ang) - 1f)) + lean * (0.75f * Mathf.Sin(ang)));
                    Vector3 pt = start + dir * (dist + back) + right * (0.7f * Mathf.Sin(ang));
                    flat.Add(new Vector3(pt.x, PathFloor(path, dist + back) + 0.05f, pt.z));
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
            PuffBurst(e.at + dir * 0.8f - Vector3.up * 0.3f, look.Dust, 2, 0.3f, 0.16f, 0.35f);
        }

        // ---------------------------------------------------------------- rook: castling (B)

        void RookAccept(QueenHillFxEvent e)
        {
            if (e.target != null) FlashBody(e.target, PieceColors(e.target).main);
        }

        /// <summary>
        /// The swap. R141 (승규 님 10-14, a picture of the ribbon arch: "룩이 캐슬링 할 때 위에 사진같은 디자인 말고 아예 다른
        /// 그래픽 디자인으로 해줘 캐슬링을 한다 ! 라는 vfx 로(없어도 됨)"): no trapdoors and no ribbon arch (R93-R94), nothing made
        /// and no coloured squares (R130: "아무 색깔도 넣지마"). The swap is told by the two pieces themselves: both spring off
        /// their squares, the air shoved down and back under each and dust off the squares; where they pass each other over
        /// the middle the beat of the swap, both white for two frames, a 2-frame stop, the air between them squeezed out to
        /// both sides and a shake; and each is set down hard on its new square (<see cref="CastleLand"/>).
        /// </summary>
        void RookSwap(QueenHillFxEvent e)
        {
            var rook = e.by;
            var ally = e.target;
            if (rook == null || ally == null) return;
            Vector3 a = e.at, b = e.to;
            Vector3 along = FlatDir(b - a, Vector3.forward);
            float total = Mathf.Max(0.2f, e.size);
            flyKind[rook] = "castle";
            flyKind[ally] = "castle";
            Spring(rook, a, along);
            Spring(ally, b, -along);
            // The beat where they pass: the moment their hips cross over the line between the squares.
            bool crossed = false;
            Run(total + 0.2f, (fx, d) =>
            {
                if (crossed || rook == null || ally == null) return false;
                Vector3 r = rook.Hips.position, l = ally.Hips.position;
                if (Vector3.Dot(l - r, along) > 0f) return true;
                crossed = true;
                Vector3 mid = (r + l) * 0.5f;
                Vector3 side = Vector3.Cross(Vector3.up, along);
                if (air != null)
                {
                    air.Kick(mid + side * 0.2f, side, 0.75f, dust);
                    air.Kick(mid - side * 0.2f, -side, 0.75f, dust);
                }
                FlashBody(rook, Color.white);
                FlashBody(ally, Color.white);
                // Online the stop holds only these two pieces' picture (D-S2): the moment's own, not the last event's.
                stopBy = rook;
                stopTarget = ally;
                HitStop(2);
                Shake(rook, ally, 0.07f, 5);
                return false;
            });
        }

        /// <summary>R141: a piece springing off its square for the swap: the air its spring shoves down and back
        /// (<see cref="SkillAir"/>), a little dust off the square.</summary>
        void Spring(RagdollPawn pawn, Vector3 feet, Vector3 along)
        {
            Vector3 floor = FloorUnder(feet, 0.6f);
            DustRing(floor + Vector3.up * 0.02f, 3, 0.45f, look.Dust, 0.24f);
            air?.Kick(pawn.Hips.position - along * 0.2f - Vector3.up * 0.1f, (Vector3.down * 0.8f - along * 0.6f).normalized, 0.85f, dust);
        }


        static Vector3 Arc(Vector3 a, Vector3 b, float top, float t)
        {
            Vector3 p = Vector3.Lerp(a, b, t);
            float baseY = Mathf.Lerp(a.y, b.y, t);
            p.y = baseY + 4f * (top - baseY) * t * (1f - t) + 0.35f * (1f - 4f * t * (1f - t)) + 0.35f;
            return p;
        }

        /// <summary>A piece set down by the swap. R141: set down hard, as a chess piece put down on its new square: it squashes
        /// and springs back, dust pushed out from under its base, a short shake. (Its square stamped in its own colour with
        /// four puffs before.)</summary>
        void CastleLand(RagdollPawn pawn)
        {
            Vector3 at = FloorUnder(pawn.Hips.position, 0.6f) + Vector3.up * 0.02f;
            AddSquash(pawn, SquashKind.Down);
            DustRing(at, 5, 0.75f, look.Dust, 0.26f);
            Shake(pawn, null, 0.08f, 6);
        }

        // ---------------------------------------------------------------- bishop: the air barrage (B)

        void BishopRise(QueenHillFxEvent e)
        {
            var bishop = e.by;
            Vector3 c = e.at;
            // Two violet rings climb round the body to the hover height, then pop into puffs.
            for (int i = 0; i < 2; i++)
            {
                var ring = new Prop(this, meshTorus, look.Bishop, "Rising ring") { inkWidth = 0.012f };
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
                        PuffBurst(bishop.Hips.position + Vector3.up * 0.4f, look.Bishop, 2, 0.4f, 0.13f, 0.35f);
                    return fx.age < popAt + d;
                });
            }
        }

        // The bishop's shot and its landing (the mitre shell): QueenHillSkillFx.Mitre.cs.

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
                var puff = new Puff(this, look.Knight) { ink = 0.32f };
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
            Vector3 dir = FlatDir(e.dir, Vector3.forward);
            // R130: the hind hooves push off: two hoofprints left behind where it sprang.
            Hoofprints(c, dir, -0.3f, 1.1f);
            // R141 (승규 님 10-14, Pawn Rush and Queen of the Hill both: "f 키를 누르면 뒤에(엉덩이 부분)에 임펙트가 나가는데 다른
            // 임펙트로 해줘(공기 임팩트)"): the air its spring shoves back and down from behind its hips, in place of the white
            // ring flashed at its feet and the wind ribbon it trailed from its hips; dust off the floor as before.
            AirKick(knight, dir, 1f, 0.3f);
            DustRing(c, 5, 1f, look.Knight, 0.3f);
        }

        void KnightLand(QueenHillFxEvent e)
        {
            Vector3 c = e.at + Vector3.up * 0.02f;
            float radius = e.size;
            DustRing(c, 8, radius * 1.1f, look.Knight, 0.3f);
            // R130 ("실제 말 같이"): the front hooves come down first: two hoofprints just ahead of it, pressed in.
            Hoofprints(c, FlatDir(e.dir, Vector3.forward), 0.55f, 1.4f);
            // R141 (승규 님 10-14: "착지 할 때만 폰러쉬에서 나이트 착지할 때 쓰는 같은 로고를 적용 해줘"): the Pawn Rush knight's
            // landing badge stamps where it came down (the horseshoe scuff in the knight's colour before), and only here.
            BadgeStamp(FloorUnder(e.at + Vector3.up * 0.3f, 0.6f), FlatDir(e.dir, Vector3.forward));
            Shake(e.by, null, 0.12f, 8);
        }

        /// <summary>R141: the Pawn Rush knight's landing badge and how it is stamped (PawnRushSkillFx.PaintBadge and BadgeStamp,
        /// R138-R139): the horseshoe badge (Skill Tile shape 4) in polished dark gold, glossy black lacquer round it and in its
        /// keyhole and nail holes, on a slab in the board look; white for two frames, 1.25 → 1.15 → 1 in six, held, faded out
        /// over its second half second, its sheen sweeping across it once as it stamps.</summary>
        void BadgeStamp(Vector3 floor, Vector3 along)
        {
            var t = new Tile(this, "Knight badge stamp");
            Vector3 g = floor + Vector3.up * 0.032f;
            var f = Run(60f * F, (fx, d) =>
            {
                float a = fx.age / F;
                t.Floor(g, along, Vector2.one * SkillInkKit.Pop(fx.age, 1.15f, 1.25f));
                PaintBadge(t, a < 2f ? 1f : 0f);
                t.sheenAt = Mathf.Min(0.999f, 0.15f + fx.age / 0.45f);
                t.fade = a < 30f ? 1f : 1f - (a - 30f) / 30f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
        }

        /// <summary>The badge's polished dark gold and the black lacquer round it (as PawnRushSkillFx's, R139).</summary>
        static readonly Palette BadgeGold = new Palette("#8C6524", "#D9B45A", "#4A320C", "#141418", 1f);
        static readonly Color BadgeLacquer = Hex("#141418");

        void PaintBadge(Tile t, float flash)
        {
            t.shape = 4f;
            t.gap = 0f;
            t.arc = 1f;
            t.fill = A(BadgeGold.main, 1f);
            t.core = A(BadgeLacquer, 1f);
            t.ink = A(BadgeLacquer, 1f);
            t.inkWidth = 0.09f;
            t.rim = Color.clear;
            t.stripe = Color.clear;
            // Design A's flat tile has no gloss: there the gold is its metal look.
            t.metal = t.board ? 0f : 1f;
            t.bevel = Color.clear;
            t.bevelWidth = 0.06f;
            t.charge = -1f;
            t.glow = Color.clear;
            t.baseColor = BadgeLacquer;
            t.thick = t.board ? 0.035f : 0f;
            t.shadow = t.board ? 0.25f : 0f;
            t.gloss = 1f;
            t.sheenAt = -1f;
            t.flash = flash;
        }

        /// <summary>Two hoofprints side by side, <paramref name="ahead"/> metres along <paramref name="dir"/> from
        /// <paramref name="at"/>: small dark prints pressed into the floor that fade over <paramref name="life"/> seconds,
        /// popping in with a puff of dust each (R130, the knight like a horse). R133: a shod hoof's real print (Skill Tile
        /// shape 3: the shoe's U with its nail holes and the frog's wedge between the heels), its toe the way it went (it
        /// was a ring with a gap).</summary>
        void Hoofprints(Vector3 at, Vector3 dir, float ahead, float life)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = FloorUnder(at + dir * ahead + side * (i == 0 ? -0.17f : 0.17f), 0.8f) + Vector3.up * 0.015f;
                PuffBurst(p + Vector3.up * 0.05f, look.Dust, 2, 0.25f, 0.1f, 0.35f, 0.15f);
                // R157: pressed into the floor as a decal (it follows a step or a slope); the tile where there is none.
                if (SkillDecals.Stamp("Hoof", p, dir, 0.32f, life)) continue;
                var t = new Tile(this, "Hoofprint");
                var f = Run(life, (fx, d) =>
                {
                    float k = fx.age / fx.life;
                    t.Floor(p, dir, Vector2.one * (0.32f * Pop(fx.age, 1.15f)));
                    t.shape = 3f;
                    t.gap = 0f;
                    t.thick = 0f;
                    t.shadow = 0f;
                    t.fill = A(HoofDirt, 0.5f);
                    t.metal = 0f;
                    t.core = Color.clear;
                    t.ink = A(HoofDirt, 0.75f);
                    t.inkWidth = 0.005f;
                    t.rim = Color.clear;
                    t.stripe = Color.clear;
                    t.arc = 1f;
                    t.fade = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                    t.Apply();
                    return true;
                });
                f.tiles.Add(t);
            }
        }

        /// <summary>R141: a hoof print's colour: the dark brown of dirt pressed into the board (as the Pawn Rush knight's;
        /// the ink's navy before).</summary>
        static readonly Color HoofDirt = Hex("#5A4738");

        void KnightFlatten(QueenHillFxEvent e)
        {
            FlashBody(e.target, look.Knight.main);
            // R138 (승규 님: "적 맞출 때 나오는 반원 이펙트인데 빼주고"): no ring round the flattened piece.
            Vector3 at = e.at;
            Drops(at, Vector3.down + FlatDir(e.dir, Vector3.forward), look.Knight, 3, 5f);
            AddSquash(e.target, SquashKind.Flatten, S != null ? S.knightFlatten : 0.7f);
            if (e.count == 1)
            {
                // Stamped on the head (R93): cream dust bursts out where its feet came down. R141 ("착지 할 때만"): no
                // horseshoe stamped on the head; the badge is the landing's only.
                PuffBurst(at, look.Dust, 4, 0.6f, 0.16f, 0.4f);
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
        /// dust puffs kick back from the heels. R141: the air it shoves back from behind its hips (AirKick), in place of the
        /// three speed wedges drawn behind them while it dashed.</summary>
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
                t.core = A(look.Dust.light, 1f);
                t.coreWidth = 0.06f;
                t.rim = Color.clear;
                t.ink = A(p.ink, 1f);
                t.inkWidth = 0.05f;
                t.stripe = Color.clear;
                if (t.board)
                {
                    BoardStamp(t, pawn != null && pawn.Piece == PieceKind.Pawn ? boardTile : p);
                    BoardSide(t, pawn);
                }
                t.fade = a < 5f ? 1f : 1f - (a - 5f) / 45f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
            PuffBurst(e.at - dir * 0.25f + Vector3.up * 0.1f, look.Dust, 2, 0.3f, 0.18f, 0.35f, 0.3f);
            AirKick(pawn, dir, 0.85f, 0.1f);
        }

        void PawnBump(QueenHillFxEvent e)
        {
            var p = PieceColors(e.by);
            FlashBody(e.target, p.main);
            // R138: no ring at the hit.
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
            /// <summary>R130: the flying carpet (QueenHillSkillFx.Carpet.cs) instead of the tile.</summary>
            public Carpet carpet;
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
                // R130 (승규 님: "아군 밑에 원이 있는데 그거 빼줘"): the ring under a guarded ally is gone; its outline stays.
                if (pawn.WardLeft > 0f) WardOutline(pawn);
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
                        break;
                }
                if (pawn.QhCrouch > 0.01f) AddSquash(pawn, SquashKind.Crouch);
            }
        }

        // ----- king

        /// <summary>The sceptre in the king's right hand. R130 (승규 님: "지팡이를 더 멋있는 걸로 만들어줘 막대는 일반 체스
        /// 계열에 끝 부분은 지금 왕 색이랑 같은데 모형이나 디자인 왕 스러운걸로"): the rod is turned like a chess piece's stem
        /// in polished ebony with an ivory band (SkillMeshes.SceptreRod), and on top sits a chess king's head in the king's
        /// gold, a crown with eight points round its rim and a cross (SkillMeshes.SceptreCrown). It glows brighter as the
        /// windup gathers (little gold motes draw into the crown) and flashes as it comes down.</summary>
        void Sceptre(RagdollPawn king)
        {
            if (!scepters.TryGetValue(king, out var h) || h.a == null)
            {
                h = new HandProp();
                h.a = new Prop(this, SkillMeshes.SceptreRod(), board ? BEbony : new Palette("#3A3340", "#8A8296", "#17131C", "#0B0A10"), "Sceptre rod") { inkWidth = 0.008f };
                h.b = new Prop(this, SkillMeshes.SceptreCrown(), look.King, "Sceptre crown") { inkWidth = 0.01f };
                h.c = new Prop(this, meshTorus, board ? BIvory : new Palette("#F3E7D3", "#FFFFFF", "#C4AE90", "#3A2E22"), "Sceptre band") { inkWidth = 0.006f };
                scepters[king] = h;
                var f = Keep(king, "sceptre", () => new Fx());
                f.props.Add(h.a);
                f.props.Add(h.b);
                f.props.Add(h.c);
                f.end = () => scepters.Remove(king);
                // R155: the Tripo sceptre (0.98 m, its foot at the rod's) in place of rod, band and crown (1.01 m).
                var real = ModelOn(LoadModel("KingModels/KingSceptre"), h.a.t, 1.04f);
                float mote = 0f;
                f.step = (fx, d) =>
                {
                    h.shown = Held(fx) ? Mathf.Min(1f, h.shown + d / (6f * F)) : h.shown - d / (6f * F);
                    if (h.shown <= 0f || king == null) return false;
                    float sc = h.shown;
                    // Held 0.2 m up from its foot; the crown on its top; the ivory band under the cup.
                    Quaternion rot = HandFrame(king, out Vector3 hand, out Vector3 along);
                    h.a.t.SetPositionAndRotation(hand - along * (0.2f * sc), rot);
                    h.a.t.localScale = Vector3.one * sc;
                    Vector3 top = h.a.t.position + along * (0.735f * sc);
                    h.b.t.SetPositionAndRotation(top, rot);
                    h.c.t.SetPositionAndRotation(h.a.t.position + along * (0.638f * sc), rot);
                    h.c.t.localScale = Vector3.one * 0.031f * sc;
                    // The windup gathers into the crown: brighter and a little bigger toward the end, gold motes drawn in.
                    float gather = king.SkillStage == SkillStage.Windup ? Mathf.Clamp01(king.SkillStageTime / Mathf.Max(0.05f, S != null ? S.kingWindup : 0.45f)) : 0f;
                    h.b.t.localScale = Vector3.one * (1.15f * sc * (1f + 0.18f * gather * gather));
                    if (gather > 0f)
                    {
                        mote += d;
                        while (mote >= 0.045f)
                        {
                            mote -= 0.045f;
                            Mote(top + along * 0.1f, look.King);
                        }
                    }
                    float flash = clock < h.flashUntil ? 1f : 0f;
                    h.a.flash = h.c.flash = flash;
                    h.b.flash = Mathf.Max(flash, gather > 0.6f ? 0.35f * (0.5f + 0.5f * Mathf.Sin(clock * 40f)) * gather : 0f);
                    h.a.Apply();
                    h.b.Apply();
                    h.c.Apply();
                    ShowModel(real, true, flash, h.a, h.b, h.c);
                    return true;
                };
            }
            else Keep(king, "sceptre", () => new Fx());
        }

        /// <summary>The right hand's frame for a held prop: local +y along the line shoulder → hand (R130, the sceptre).</summary>
        static Quaternion HandFrame(RagdollPawn pawn, out Vector3 hand, out Vector3 along)
        {
            var bodies = pawn.bodies;
            hand = bodies[(int)BodyId.HandR].position;
            Vector3 shoulder = bodies[(int)BodyId.ArmR].position;
            along = hand - shoulder;
            if (along.sqrMagnitude < 1e-6f) along = Vector3.up;
            along.Normalize();
            Vector3 side = Vector3.Cross(along, pawn.Facing);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(along, Vector3.forward);
            return Quaternion.LookRotation(side.normalized, along);
        }

        /// <summary>A little gold mote drawn into <paramref name="to"/> from round it, shrinking as it arrives (R130, the
        /// sceptre gathering its guard).</summary>
        void Mote(Vector3 to, Palette p)
        {
            Vector3 from = to + Random.onUnitSphere * Random.Range(0.35f, 0.55f);
            var puff = new Puff(this, p, meshBall) { ink = 0.3f, lump = 0f };
            var f = Run(0.2f, (fx, d) =>
            {
                float k = fx.age / fx.life;
                puff.t.position = Vector3.Lerp(from, to, k * k);
                puff.t.localScale = Vector3.one * (0.05f * (1f - k * 0.7f));
                puff.Apply(fx.age);
                return true;
            });
            f.puffs.Add(puff);
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
                    t.thick = 0.02f;   // the board look: a thin ring pad
                    t.metal = look.King.metal;
                    t.fill = A(look.King.main, 1f);
                    t.core = A(look.GoldAccent, 1f);
                    t.coreWidth = 0.02f;
                    t.ink = A(look.King.ink, 1f);
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
                h.a = new Prop(this, RoundBox(new Vector3(0.14f, 0.86f, 0.035f), 0.016f), board ? BBlade : new Palette("#FFF4D6", "#FFFFFF", "#E2C27A", "#3A2208"), "Sword blade") { inkWidth = 0.018f };
                h.b = new Prop(this, RoundBox(new Vector3(0.34f, 0.07f, 0.09f), 0.03f), look.Queen, "Sword guard") { inkWidth = 0.016f };
                h.c = new Prop(this, meshPuff, look.Queen, "Sword sheen") { inkWidth = 0f };
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
                    // On the floors the slash runs along (R97; as R94 laid them, and the slash now goes where they are),
                    // lying over them (R98: down a slope too, not flat at their middle's height).
                    var squares = PathSquares(queen.SlashPath, dir, s.queenLength, s.square, queen.SlashReach, queen.SlashBlocked);
                    float strength = locked ? Charge(queen.SkillStageTime, s.queenWindup) : 0.18f;
                    // Only her own screen shows the aim; the others see the line once she commits (the windup).
                    bool show = locked || SideOf(queen) == Side.Self;
                    for (int i = 0; i < x.tiles.Count; i++)
                    {
                        var t = x.tiles[i];
                        bool on = show && i < squares.Count;
                        if (on) LaySquare(t, queen, queen.SlashPath, dir, squares[i], s.square);
                        // Past the wall that stops the slash, the squares stay faint.
                        bool past = on && squares[i].past;
                        t.fade = on ? (past ? 0.35f : 1f) : 0f;
                        t.ghost = past ? 0.35f : 1f;   // a solid pad shows faint by this, not by its fade
                        Warn(t, queen, past ? 0.15f : strength);
                    }
                    return true;
                };
                return fx;
            });
        }

        // ----- rook

        /// <summary>
        /// The rook's aim. R130 (승규 님: "룩이 캐슬링을 시도 할 때 아무 색깔도 넣지마 발판 상대 발판 대신 캐슬링을 시도한다!
        /// 라는 연출을 줄 수 있게끔"): no coloured pads under the rook and the ally any more. Instead the swap itself is
        /// shown before it happens: a see-through rook leaps along the arch from the rook to the ally's spot while a
        /// see-through copy of the ally's piece leaps the other way, over and over (each lands with a thin ring), the dotted
        /// arch between them pulses both ways, and two white arrows chase round each other over the middle of the arch
        /// (castling's "change places"), turning faster and with a ring filling round them while the ally is asked. All in
        /// ivory and white with navy ink; grey and still when the swap cannot go. With no ally picked yet only the arrows
        /// show, faint, over the rook.
        /// R141 (승규 님 10-14: "저 돌같은 거를 조금 더 그래픽 좋게 생성해줘"): the arch's dots read as stones (low balls in
        /// cartoon bands with a thick ink edge); they are polished silver beads now (<see cref="Beads"/>), a gleam running
        /// along them both ways.
        /// </summary>
        void RookAim(RagdollPawn rook, QueenHillSkillParams s)
        {
            Keep(rook, "rook aim", () =>
            {
                var fx = new Fx();
                var neutral = board ? BIvory : new Palette("#FFF8EC", "#FFFFFF", "#CBBBA4", "#24242F");
                var rookGhost = new Prop(this, SkillMeshes.Piece(PieceKind.Rook), neutral, "Castling ghost (rook)");
                var allyGhost = new Prop(this, SkillMeshes.Piece(PieceKind.Pawn), neutral, "Castling ghost (ally)");
                var arrows = new Prop(this, SkillMeshes.SwapArrows(), neutral, "Castling arrows") { inkWidth = 0.012f };
                fx.props.Add(rookGhost);
                fx.props.Add(allyGhost);
                fx.props.Add(arrows);
                fx.tiles.Add(new Tile(this, "Castling clock"));
                fx.tiles.Add(new Tile(this, "Castling landing (rook)"));
                fx.tiles.Add(new Tile(this, "Castling landing (ally)"));
                var beads = new Beads(this, fx, 17, "Castling bead");
                PieceKind allyKind = PieceKind.Pawn;
                float spin = 0f;
                fx.step = (x, d) =>
                {
                    if (!Held(x) || rook == null) return false;
                    var target = rook.QhAimTarget;
                    bool locked = rook.QhAimLocked, valid = rook.QhAimValid && target != null;
                    var clockTile = x.tiles[0];
                    Vector3 a = rook.FeetPoint + Vector3.up * 0.016f;
                    Vector3 eye = Eye;
                    spin += d * (locked ? 540f : 220f);
                    if (target == null)
                    {
                        rookGhost.Show(false);
                        allyGhost.Show(false);
                        beads.Hide();
                        for (int i = 0; i < 3; i++) { x.tiles[i].fade = 0f; x.tiles[i].Apply(); }
                        // Only the arrows, faint, over the rook: castling, pick an ally.
                        Arrows(arrows, rook.bodies[(int)BodyId.Head].position + Vector3.up * 0.6f, eye, spin, 0.42f, Pop(x.age, 1.15f), 0f, false);
                        return true;
                    }
                    if (target.Piece != allyKind)
                    {
                        allyKind = target.Piece;
                        var mf = allyGhost.t.GetComponent<MeshFilter>();
                        if (mf != null) mf.sharedMesh = SkillMeshes.Piece(allyKind);
                    }
                    Vector3 b = target.FeetPoint + Vector3.up * 0.016f;
                    float top = Mathf.Max(a.y, b.y) + s.rookArc + 0.35f;
                    // The two leaps: 0.85 s along the arch, 0.25 s standing where they landed, again.
                    float period = locked ? 0.85f : 1.1f;
                    float phase = Mathf.Repeat(x.age, period);
                    float fly = valid ? Mathf.Clamp01(phase / (period * 0.72f)) : 0f;
                    float ease = fly * fly * (3f - 2f * fly);
                    float landed = valid && fly >= 1f ? (phase - period * 0.72f) / (period * 0.28f) : -1f;
                    Ghost(rookGhost, a, b, top, ease, valid, x.age, 0.95f);
                    Ghost(allyGhost, b, a, top, ease, valid, x.age, GhostHeight(allyKind));
                    LandingRing(x.tiles[1], b, landed);
                    LandingRing(x.tiles[2], a, landed);
                    // The arrows over the middle of the arch, and the ally's answer filling round them.
                    Vector3 mid = Arc(a, b, top, 0.5f) + Vector3.up * 0.55f;
                    float ask = locked ? Mathf.Clamp01(target.CastleAskAge / Mathf.Max(0.1f, s.dummyAccept)) : 0f;
                    Arrows(arrows, mid, eye, spin, 0.5f, Pop(x.age, 1.15f), ask, valid);
                    Vector3 toEye = eye - mid;
                    clockTile.Facing(mid, toEye.sqrMagnitude > 1e-4f ? toEye.normalized : Vector3.back, Vector2.one * 0.78f);
                    FlatRing(clockTile);
                    clockTile.shape = 1f;
                    clockTile.inner = 0.86f;
                    clockTile.fill = A(neutral.light, 1f);
                    clockTile.core = Color.clear;
                    clockTile.ink = A(BoardInkOrNavy, 1f);
                    clockTile.inkWidth = 0.015f;
                    clockTile.arc = ask;
                    clockTile.fade = locked && valid ? 1f : 0f;
                    clockTile.Apply();
                    // The way they fly: beads along the arch, a gleam running each way at once (grey and still if it is blocked).
                    float pop = Pop(x.age, 1.15f);
                    for (int i = 0; i < beads.Count; i++)
                    {
                        float t = (i + 1f) / (beads.Count + 1f);
                        float beatA = Mathf.Repeat(clock * 1.6f - t, 1f), beatB = Mathf.Repeat(clock * 1.6f - (1f - t), 1f);
                        float beat = Mathf.Max(Mathf.Clamp01(1f - beatA * 4f), Mathf.Clamp01(1f - beatB * 4f));
                        beads.Place(i, Arc(a, b, top, t), 0.085f * pop, valid ? beat : 0f, valid ? BeadSilver : look.Grey);
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The ink colour of the neutral castling parts: the board's navy (design A's navy too).</summary>
        Color BoardInkOrNavy => board ? boardInk : Hex("#24242F");

        /// <summary>How tall a castling ghost of this piece stands (m): the pieces' own heights next to each other.</summary>
        static float GhostHeight(PieceKind kind) => kind switch
        {
            PieceKind.King => 1.1f,
            PieceKind.Queen => 1.05f,
            PieceKind.Bishop => 0.98f,
            PieceKind.Knight => 0.95f,
            PieceKind.Rook => 0.95f,
            _ => 0.8f,
        };

        /// <summary>A castling ghost flying the arch from <paramref name="from"/> to <paramref name="to"/>
        /// (<paramref name="k"/> 0..1): see-through white, a band of light sliding down it, half a turn as it flies; grey,
        /// still and fainter where it cannot go.</summary>
        void Ghost(Prop g, Vector3 from, Vector3 to, float top, float k, bool valid, float age, float height)
        {
            Vector3 foot = Arc(from, to, top, k) - Vector3.up * 0.7f;
            // At the ends the arch is 0.7 m up (Arc): the foot on the floor there.
            Vector3 along = FlatDir(to - from, Vector3.forward);
            g.t.SetPositionAndRotation(foot, Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(0f, 180f * k, 0f));
            float pop = Pop(age, 1.12f);
            g.t.localScale = Vector3.one * (height * pop);
            g.ghost = valid ? 0.62f : 0.3f;
            g.ghostColor = valid ? new Color(0.94f, 0.95f, 1f) : new Color(0.55f, 0.57f, 0.63f);
            g.ghostEdge = valid ? new Color(1.3f, 1.35f, 1.5f) : new Color(0.7f, 0.72f, 0.78f);
            g.ghostScanY = foot.y + height * (1.1f - Mathf.Repeat(age * 1.4f, 1.3f));
            g.Show(pop > 0.01f);
            g.Apply();
        }

        /// <summary>A thin white ring opening on the floor where a ghost has just landed (<paramref name="landed"/> 0..1,
        /// below 0: not now).</summary>
        void LandingRing(Tile t, Vector3 at, float landed)
        {
            if (landed < 0f)
            {
                t.fade = 0f;
                t.Apply();
                return;
            }
            float k = EaseOut(landed);
            t.Floor(at + Vector3.up * 0.004f, Vector3.forward, Vector2.one * Mathf.Lerp(0.5f, 1.4f, k));
            FlatRing(t);
            t.shape = 1f;
            t.inner = 0.88f;
            t.fill = A(Color.white, 1f);
            t.core = Color.clear;
            t.ink = A(BoardInkOrNavy, 0.9f);
            t.inkWidth = 0.012f;
            t.arc = 1f;
            t.fade = 1f - landed;
            t.Apply();
        }

        /// <summary>The castling arrows facing the eye at <paramref name="at"/>, turning; brighter while the ally is asked.</summary>
        void Arrows(Prop arrows, Vector3 at, Vector3 eye, float spin, float size, float pop, float ask, bool valid)
        {
            Vector3 toEye = eye - at;
            if (toEye.sqrMagnitude < 1e-4f) toEye = Vector3.back;
            arrows.t.SetPositionAndRotation(at, Quaternion.LookRotation(-toEye.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, -spin));
            arrows.t.localScale = Vector3.one * (size * pop * (1f + 0.08f * Mathf.Sin(clock * 9f)));
            arrows.colors = valid ? (board ? BIvory : new Palette("#FFF8EC", "#FFFFFF", "#CBBBA4", "#24242F")) : look.Grey;
            arrows.flash = ask > 0.97f ? 0.6f : 0f;
            arrows.Show(pop > 0.01f);
            arrows.Apply();
        }

        // ----- bishop

        /// <summary>What the bishop floats on. R130: a flying carpet with a soft shadow under it
        /// (QueenHillSkillFx.Carpet.cs); the dark board square of R126 only if the carpet's shaders are missing.</summary>
        void BishopHover(RagdollPawn bishop, QueenHillSkillParams s)
        {
            if (!hoverTiles.TryGetValue(bishop, out var h) || (h.tile == null && h.carpet == null))
            {
                h = new HoverTile();
                if (CarpetReady()) h.carpet = NewCarpet();
                else
                {
                    // The board look: a dark board square, glossy, with navy sides.
                    h.tile = new Prop(this, RoundBox(new Vector3(1.1f, 0.12f, 1.1f), 0.05f), board ? BSquare : look.Bishop, "Hover tile") { inkWidth = 0.02f, twoTone = board };
                    h.shadow = new Tile(this, "Hover shadow");
                }
                hoverTiles[bishop] = h;
                float born = clock;
                var f = Keep(bishop, "hover", () => new Fx());
                if (h.tile != null) f.props.Add(h.tile);
                if (h.shadow != null) f.tiles.Add(h.shadow);
                if (h.carpet != null) foreach (var t in h.carpet.tassels) f.props.Add(t);
                f.end = () =>
                {
                    hoverTiles.Remove(bishop);
                    h.carpet?.Destroy();
                };
                f.step = (fx, d) =>
                {
                    if (bishop == null) return false;
                    float age = clock - born;
                    bool dropping = h.dropAt >= 0f || !Held(fx);
                    if (h.dropAt < 0f && !Held(fx)) h.dropAt = clock;
                    float since = dropping ? clock - h.dropAt : 0f;
                    Vector3 feet = bishop.Hips.position - Vector3.up * (bishop.standHeight + 0.09f);
                    // Dropping: it flutters, blinks three times, then goes up in puffs.
                    if (dropping && since > 0.3f)
                    {
                        PuffBurst(feet, look.Bishop, h.carpet != null ? 6 : 4, h.carpet != null ? 0.7f : 0.5f, 0.15f, 0.35f);
                        return false;
                    }
                    float sc = Pop(age, 1.1f);
                    bool shown = !dropping || Mathf.Repeat(since / 0.1f, 1f) < 0.55f;
                    if (h.carpet != null)
                    {
                        Vector3 ground = FloorUnder(feet, 0.2f);
                        PlaceCarpet(h.carpet, feet + Vector3.up * 0.03f, bishop.Facing, ground.y, sc, dropping ? Mathf.Clamp01(since / 0.3f) : 0f, shown, d);
                        return true;
                    }
                    h.tile.t.SetPositionAndRotation(feet, Quaternion.LookRotation(bishop.Facing, Vector3.up) * Quaternion.Euler(0f, 45f, 0f));
                    h.tile.t.localScale = Vector3.one * sc;
                    h.tile.Show(shown);
                    h.tile.Apply();
                    Vector3 floor = FloorUnder(feet, 0.2f) + Vector3.up * 0.014f;
                    h.shadow.Floor(floor, Quaternion.Euler(0f, 45f, 0f) * bishop.Facing, Vector2.one * (1.05f * sc));
                    FlatRing(h.shadow);
                    h.shadow.fill = A(look.Bishop.ink, 0.3f);
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
                        var drip = new Puff(this, look.Bishop) { ink = 0.3f };
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

        /// <summary>The bishop's aim: the mitre it will fire standing see-through at the spot (QueenHillSkillFx.Mitre.cs) and a
        /// beaded arc from its hand to the mitre's point. R141 (승규 님 10-14: "비숍도 마찬가지로 돌 같은 거를 그래픽 좋게 생성해주고
        /// 밑에 미리보기로 x 자로 표시된 부분 지워줘"): the arc's dots, which read as stones, are polished gold beads (the mitre's
        /// gold, <see cref="Beads"/>) with a gleam running from the hand out to the mitre, and the light X on the floor under
        /// the mitre (10-12) is gone.</summary>
        void BishopAim(RagdollPawn bishop, QueenHillSkillParams s)
        {
            var f = Keep(bishop, "bishop aim", () =>
            {
                var fx = new Fx();
                var beads = new Beads(this, fx, 22, "Bishop aim bead");
                var mitre = new Mitre(this, fx);
                fx.step = (x, d) =>
                {
                    if (!Held(x) || bishop == null) return false;
                    bool valid = bishop.QhAimValid;
                    Vector3 to = bishop.QhAimPoint;
                    Vector3 yaw = FlatDir(bishop.QhAimPoint - bishop.bodies[0].position, bishop.Facing);
                    // The mitre at the spot, and the beaded arc from the bishop's hand to its point.
                    Vector3 point = AimMitre(mitre, to, yaw, valid, x.age);
                    Vector3 from = bishop.bodies[(int)BodyId.HandR].position;
                    float top = Mathf.Max(from.y, point.y) + 1.2f;
                    float pop = Pop(x.age, 1.15f);
                    for (int i = 0; i < beads.Count; i++)
                    {
                        float k = (i + 1f) / (beads.Count + 1f);
                        Vector3 q = Vector3.Lerp(from, point, k);
                        q.y = Mathf.Lerp(from.y, point.y, k) + 4f * (top - Mathf.Max(from.y, point.y)) * k * (1f - k);
                        float gleam = Mathf.Clamp01(1f - Mathf.Repeat(clock * 1.1f - k, 1f) * 5f);
                        beads.Place(i, q, Mathf.Lerp(0.08f, 0.06f, k) * pop, valid ? gleam : 0f, valid ? BeadGold : look.Grey);
                    }
                    return true;
                };
                return fx;
            });
        }

        // ----- knight

        /// <summary>R133: the horseshoe's nail holes, dark iron.</summary>
        static readonly Palette ShoeIron = new Palette("#2B2E37", "#555A68", "#17181E", "#0E0F14", 0.4f);
        /// <summary>R141: a real horseshoe's forged steel: dark grey iron, the face it wears on bright and polished.</summary>
        static readonly Palette ShoeSteel = new Palette("#4B4E54", "#9A9EA5", "#1E2024", "#101114", 1f);

        /// <summary>The landing mark: a horseshoe turning on the spot, over the 1.5 m square it lands in (grey and no square
        /// where it cannot go: too high). It shrinks as the knight comes down and is exactly its smallest at touchdown.
        /// R130 (승규 님: "위에 있는 원형 고리 같은 것도 더 좋게 만들어줘"): the horseshoe is a real one now, solid and
        /// polished, floating a little over the square or over the head it caught, tilted toward the eye so it reads,
        /// bobbing, with a soft ring of its colour under it; the flat one is only drawn if the solid one cannot be.
        /// R133 (승규 님 10-14: "나이트 말 발굽을 조금 더 사실적으로 다시 디자인 해줘"): a real shoe's shape
        /// (SkillMeshes.Horseshoe: a U, wider at the toe, its groove round the face) with eight dark nail holes in the
        /// groove (SkillMeshes.HorseshoeHoles), turning slower (72°/s, was 126°/s) so it can be seen.
        /// R141 (승규 님 10-14: "나이트 스킬 움직임 모션은 좋은데 말 발굽 그래픽을 바꿔줘 색을 실제 말 발굽 같이"): coloured as a
        /// real horseshoe, forged steel (<see cref="ShoeSteel"/>: dark grey iron, its worn face bright), not the knight's
        /// polished silver; no ring of its colour under it (the square shows where it lands).</summary>
        void KnightAim(RagdollPawn knight, QueenHillSkillParams s, bool committed)
        {
            var f = Keep(knight, committed ? "knight go" : "knight aim", () =>
            {
                var fx = new Fx();
                fx.tiles.Add(new Tile(this, "Knight square"));
                fx.props.Add(new Prop(this, SkillMeshes.Horseshoe(), ShoeSteel, "Horseshoe (solid)") { inkWidth = 0.012f });
                fx.props.Add(new Prop(this, SkillMeshes.HorseshoeHoles(), ShoeIron, "Horseshoe nail holes") { inkWidth = 0.004f });
                fx.step = (x, d) =>
                {
                    if (!Held(x) || knight == null) return false;
                    bool valid = committed || knight.QhAimValid;
                    // R93: an enemy the aim has caught: the square under it and the horseshoe turning over its head.
                    var target = committed ? knight.LeapTarget : knight.QhAimTarget;
                    bool onHead = target != null && target.State == PawnState.Active;
                    Vector3 at = (onHead ? target.FeetPoint : committed ? knight.LeapSpot : knight.QhAimPoint) + Vector3.up * 0.016f;
                    var sq = x.tiles[0];
                    sq.Floor(at, knight.Facing, Vector2.one * s.square);
                    Warn(sq, knight, committed ? 0.95f : 0.6f);
                    sq.fade = valid ? 1f : 0f;
                    sq.Apply();
                    float r = committed && knight.QhFlying ? Mathf.Lerp(0.7f, 0.5f, knight.QhFlyProgress) : committed ? 0.5f : 0.7f;
                    if (onHead) r *= 0.75f;
                    float spin = clock * 90f;
                    Vector3 shoeAt = at + Vector3.up * (0.004f + PadTop);   // on the square's top when it is a pad
                    if (onHead)
                    {
                        Vector3 head = target.bodies[(int)BodyId.Head].position;
                        shoeAt = new Vector3(head.x, head.y + 0.24f + 0.03f * Mathf.Sin(clock * 9f), head.z);
                    }
                    var solid = x.props[0];
                    var pal = valid ? ShoeSteel : look.Grey;
                    // The solid horseshoe: over the spot (higher over a head), tilted 35° toward the eye, bobbing, turning.
                    Vector3 lift = Vector3.up * ((onHead ? 0.12f : 0.32f) + 0.04f * Mathf.Sin(clock * 5f));
                    Vector3 toEye = FlatDir(Eye - shoeAt, Vector3.back);
                    Quaternion tilt = Quaternion.AngleAxis(35f, Vector3.Cross(Vector3.up, toEye));
                    solid.t.SetPositionAndRotation(shoeAt + lift, tilt * Quaternion.Euler(0f, spin * 0.8f, 0f));
                    solid.t.localScale = Vector3.one * (2.05f * r * Pop(x.age, 1.2f));
                    solid.colors = pal;
                    solid.flash = committed && x.age < 2f * F ? 1f : 0f;
                    solid.Apply();
                    var holes = x.props[1];
                    holes.t.SetPositionAndRotation(solid.t.position, solid.t.rotation);
                    holes.t.localScale = solid.t.localScale;
                    holes.flash = solid.flash;
                    holes.Apply();
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
                // R141: the air its charge shoves back from behind its hips as it sets off, once a charge (four lines
                // streamed behind its hips before).
                Keep(rook, "charge air", () =>
                {
                    AirKick(rook, FlatDir(rook.SkillDirection, rook.Facing), 1f, 0.1f);
                    return new Fx { step = (x, d) => Held(x) };
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
            dust?.Step(dt, cam);   // R138
            air?.Step(dt);   // R141
            UploadBreaks();   // R137: what the bishop's strike broke, to the ground (Skill Ground)
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
