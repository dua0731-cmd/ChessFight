using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Hit-feel effects for the Pawn Rush skills, in the skill test scene only (Docs/Skills/EFFECTS.md; picked
    /// 2026-10-07: queen A "two rings", rook A "bowling pins", bishop B "diagonal squares", knight B "head
    /// stomp"; the pawn is not picked yet and keeps its plain look).
    ///
    /// Shared parts: a hit stop (the whole game holds still for a few hundredths of a second, a test-bed stand-in
    /// for holding only the two pieces), camera shake and a white flash on the piece that is hit. No words or
    /// numbers pop up (R74: taken out), no chips or blocks fly off a hit (R75: taken out) and no star pops up on a
    /// hit (R78: taken out). The pictures are flipbooks drawn in the effect workshop (Resources/PawnRushSkillFx,
    /// R78): rings and squares lie flat on the floor, sparks and dizzy stars face the camera. No sound yet.
    /// While a bishop aims, its squares show see-through where the X would go. It listens to RagdollPawn.SkillFx, which
    /// only the skills raise, so no other scene changes.
    /// </summary>
    [DefaultExecutionOrder(210)]   // after the lab camera (150) and the skeleton pose (100)
    public class PawnRushSkillFx : MonoBehaviour
    {
        public bool effects = true;
        [Tooltip("맞는 순간 게임 전체를 잠깐 멈춤 (시험용: 실제로는 때린 쪽·맞은 쪽만)")]
        public bool hitStop = true;
        public bool shake = true;

        /// <summary>The film's camera while it records; the lab camera otherwise.</summary>
        public static Camera ViewOverride;

        static readonly Color Ink = new Color(0.05f, 0.08f, 0.19f);

        // The workshop's flipbooks: name, frames, frames a second, loops. Each sheet holds its frames in rows of up
        // to eight, left to right, top to bottom (packed from the workshop's strips).
        static readonly (string name, int frames, float fps, bool loop)[] SheetList =
        {
            ("queen_shockwave", 20, 24f, false),
            ("queen_shockwave_hit_sparks", 8, 30f, false),
            ("rook_hit_sparks", 8, 30f, false),
            ("rook_stop_floor_ring", 17, 24f, false),
            ("bishop_tripwire_lightup", 8, 24f, false),
            ("bishop_tripwire_hold", 24, 24f, true),
            ("bishop_tripwire_preview", 12, 24f, true),
            ("bishop_trip_pop", 11, 24f, false),
            ("knight_stomp_sparks", 8, 24f, false),
            ("knight_stomp_dizzy_stars", 24, 24f, true),
            ("knight_land_dust_ring", 14, 24f, false),
        };

        // How far out each picture's drawing reaches, as a share of half its width: rings stop 3-4 px short of the
        // edge, the bishop's five squares are 17 px apart in a 96 px picture.
        const float QueenRingReach = 47f / 50f, FloorRingReach = 45f / 48f, BishopSquaresPerPicture = 96f / 17f;

        LabGame game;
        Transform root;
        Font font;
        Material lineMat, whiteMat;
        readonly Dictionary<string, Sheet> sheets = new Dictionary<string, Sheet>();

        // hit stop and shake
        float stopLeft, stopResume = 1f, shakeAmp, shakeLeft, shakeTotal, shakeClock;
        bool stopping;

        readonly List<Anim> anims = new List<Anim>();
        readonly Dictionary<RagdollPawn, Flash> flashes = new Dictionary<RagdollPawn, Flash>();
        readonly List<Squash> squashes = new List<Squash>();
        readonly Dictionary<Object, TileSet> tileSets = new Dictionary<Object, TileSet>();
        readonly Dictionary<RagdollPawn, float> stompedAt = new Dictionary<RagdollPawn, float>();
        readonly Dictionary<RagdollPawn, Flipbook> ghosts = new Dictionary<RagdollPawn, Flipbook>();
        EdgeFlash edge;

        // Real time, frame by frame: a hit stop does not stop it, and a recording (Time.captureFramerate)
        // steps it one frame at a time like the video it makes.
        static float Dt => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
        float clock;
        float Clock => clock;

        public Camera ViewCamera => ViewOverride != null ? ViewOverride : game != null && game.labCamera != null ? game.labCamera.Cam : Camera.main;

        void Awake()
        {
            game = GetComponent<LabGame>();
            if (game == null) game = FindFirstObjectByType<LabGame>();
            root = new GameObject("Pawn Rush skill effects").transform;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 64);
            lineMat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            whiteMat = Unlit(Color.white);
            foreach (var (name, frames, fps, loop) in SheetList)
            {
                var texture = Resources.Load<Texture2D>("PawnRushSkillFx/" + name);
                if (texture == null) { Debug.LogWarning($"[PawnRushSkillFx] missing Resources/PawnRushSkillFx/{name}.png"); continue; }
                var material = new Material(lineMat) { hideFlags = HideFlags.HideAndDontSave, mainTexture = texture };
                sheets[name] = new Sheet(material, texture, frames, fps, loop);
            }
            RagdollPawn.SkillFx += OnSkillFx;
        }

        void OnDestroy()
        {
            RagdollPawn.SkillFx -= OnSkillFx;
            if (stopping) Time.timeScale = stopResume;
            foreach (var f in flashes.Values) f.Restore();
            foreach (var s in squashes) s.Restore();
            foreach (var s in sheets.Values) Destroy(s.material);
            if (root != null) Destroy(root.gameObject);
        }

        // ---------------------------------------------------------------- the skills' moments

        void OnSkillFx(SkillFxEvent e)
        {
            if (!effects) return;
            switch (e.kind)
            {
                case SkillFxKind.QueenBlast: QueenBlast(e); break;
                case SkillFxKind.QueenHit:
                    FlashWhite(e.target, 0.07f);
                    Burst("queen_shockwave_hit_sparks", e.at + Vector3.up * 0.3f, 1.6f);
                    break;
                case SkillFxKind.RookHit: RookHit(e); break;
                case SkillFxKind.RookStop: RookStop(e); break;
                case SkillFxKind.RookWall:
                    // Into a wall: the hardest thud of the rook's (R75: a clear shake, up to 2.5°).
                    HitStop(0.08f);
                    Shake(0.25f, 0.4f);
                    Burst("rook_hit_sparks", e.at, 2.8f);
                    break;
                case SkillFxKind.RookBarricade:
                    HitStop(0.07f);
                    Shake(0.14f, 0.3f);
                    Burst("rook_hit_sparks", e.at + Vector3.up * 0.5f, 2.4f);
                    break;
                case SkillFxKind.BishopWire: BishopWire(e); break;
                case SkillFxKind.BishopTrip: BishopTrip(e); break;
                case SkillFxKind.KnightStomp: KnightStomp(e); break;
                case SkillFxKind.KnightLand: KnightLand(e); break;
            }
        }

        // Queen A: the workshop's shockwave on the floor: a gold disc and a thick inner ring out to 1.5 m (knocks
        // down), a thin outer ring out to 3 m (pushes); the screen shakes and its edges split red and cyan.
        void QueenBlast(SkillFxEvent e)
        {
            float outer = e.size > 0f ? e.size : 3f;
            Floor("queen_shockwave", Ground(e.at), Vector3.forward, outer * 2f / QueenRingReach, 0.03f);
            HitStop(0.06f);
            Shake(0.13f, 0.28f);
            Edge(0.22f);
        }

        // Rook A: each piece it sends flying gives orange sparks and a longer stop (0.04, 0.06, 0.08 s) and shake
        // than the one before; the fourth, which stops it, bigger sparks and an orange ring on the floor.
        void RookHit(SkillFxEvent e)
        {
            int i = Mathf.Clamp(e.count, 1, 3);
            HitStop(new[] { 0.04f, 0.06f, 0.08f }[i - 1]);
            Shake(new[] { 0.05f, 0.08f, 0.11f }[i - 1], 0.16f + 0.03f * i);
            FlashWhite(e.target, 0.06f);
            Burst("rook_hit_sparks", e.at, 1.6f + 0.3f * i);
        }

        void RookStop(SkillFxEvent e)
        {
            HitStop(0.09f);
            Shake(0.17f, 0.32f);
            FlashWhite(e.target, 0.07f);
            Burst("rook_hit_sparks", e.at, 2.6f);
            Floor("rook_stop_floor_ring", Ground(e.at), Vector3.forward, 1.3f * 2f / FloorRingReach, 0.04f);
        }

        // Bishop B: the board squares under the X light up violet, from the middle out, and pulse while the wire
        // lasts; the square an enemy trips on pops up.
        void BishopWire(SkillFxEvent e)
        {
            if (e.source == null || !sheets.ContainsKey("bishop_tripwire_lightup") || !sheets.ContainsKey("bishop_tripwire_hold")) return;
            Vector3 center = Ground(e.at);
            var set = new TileSet { source = e.source };
            float size = 0f;
            foreach (var (at, _, square, _) in Squares(center, e.dir, e.size))
            {
                set.squares.Add(at);
                size = square;
            }
            set.square = size;
            float picture = size * BishopSquaresPerPicture;
            set.lightUp = MakeFloor("bishop_tripwire_lightup", center, e.dir, picture, 0.02f);
            set.hold = MakeFloor("bishop_tripwire_hold", center, e.dir, picture, 0.02f);
            set.hold.delay = set.lightUp.Length;   // its first frame is the light-up's last
            if (tileSets.TryGetValue(e.source, out var old)) old.Destroy();
            tileSets[e.source] = set;
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

        /// <summary>While a bishop aims, its squares show see-through where the X would go (B's preview).</summary>
        void GhostSquares(float dt, Camera cam)
        {
            var bed = GetComponent<PawnRushSkillBed>();
            float length = bed != null ? bed.skills.bishopLineLength : 4.2f;
            float picture = length * 0.5f / (2f * Mathf.Sqrt(2f)) * BishopSquaresPerPicture;
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
                    ghost = MakeFloor("bishop_tripwire_preview", pawn.BishopAimPoint, pawn.BishopAimYaw, picture, 0.025f);
                    if (ghost == null) continue;
                    ghosts[pawn] = ghost;
                }
                ghost.hidden = !show;
                if (show)
                {
                    ghost.at = pawn.BishopAimPoint + Vector3.up * 0.025f;
                    ghost.Face(pawn.BishopAimYaw);
                }
                ghost.Step(dt, cam);
            }
        }

        void BishopTrip(SkillFxEvent e)
        {
            HitStop(0.05f);
            Shake(0.06f, 0.16f);
            FlashWhite(e.target, 0.06f);
            Vector3 at = Ground(e.at);
            float square = 0.74f;
            if (e.source != null && tileSets.TryGetValue(e.source, out var set) && set.squares.Count > 0)
            {
                float bestD = float.MaxValue;
                foreach (var s in set.squares)
                {
                    float d = Vector3.Distance(Flat(s), Flat(e.at));
                    if (d < bestD) { bestD = d; at = s; }
                }
                square = set.square;
            }
            // The popping square is drawn corner to corner 32 px wide in a 64 px picture (its diagonal, as the
            // squares on the floor are mostly seen), its middle 4 px below the picture's.
            var pop = Burst("bishop_trip_pop", at, square * 0.92f * Mathf.Sqrt(2f) * 2f);
            if (pop != null)
            {
                pop.pivot = new Vector2(0f, -4f / 64f);
                pop.keepAbove = at.y;
            }
        }

        // Knight B: the piece stomped on squashes flat and springs back, a long stop (0.09 s), sky-blue sparks and
        // dizzy stars circling over it. The knight's little hop off it lands with nothing more (R75: one set of
        // marks, not two).
        void KnightStomp(SkillFxEvent e)
        {
            HitStop(0.09f);
            Shake(0.1f, 0.2f);
            FlashWhite(e.target, 0.07f);
            Burst("knight_stomp_sparks", e.at, 2.2f);
            StartSquash(e.target);
            if (e.target != null)
            {
                var dizzy = Burst("knight_stomp_dizzy_stars", HeadOf(e.target), 1f);
                if (dizzy != null)
                {
                    dizzy.pull = 0.4f;
                    dizzy.overHead = e.target;
                    dizzy.headUp = 0.34f;
                    dizzy.delay = 0.45f;
                    dizzy.life = 1.8f;
                }
            }
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
            Floor("knight_land_dust_ring", g, Vector3.forward, radius * 2f / FloorRingReach, 0.03f);
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

        /// <summary>A workshop picture facing the camera, played once from <paramref name="at"/>, <paramref name="size"/>
        /// metres wide.</summary>
        Flipbook Burst(string sheet, Vector3 at, float size)
        {
            if (!sheets.TryGetValue(sheet, out var s)) return null;
            var f = new Flipbook(root, s, sheet) { at = at, size = size, billboard = true, pull = 0.8f };
            anims.Add(f);
            return f;
        }

        /// <summary>A workshop picture lying on the floor at <paramref name="at"/>, its top towards
        /// <paramref name="forward"/>, played once.</summary>
        void Floor(string sheet, Vector3 at, Vector3 forward, float size, float lift)
        {
            var f = MakeFloor(sheet, at, forward, size, lift);
            if (f != null) anims.Add(f);
        }

        Flipbook MakeFloor(string sheet, Vector3 at, Vector3 forward, float size, float lift)
        {
            if (!sheets.TryGetValue(sheet, out var s)) return null;
            var f = new Flipbook(root, s, sheet) { at = at + Vector3.up * lift, size = size };
            f.Face(forward);
            return f;
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

            // Squash after the skeleton has been posed this frame.
            for (int i = squashes.Count - 1; i >= 0; i--)
                if (!squashes[i].Apply(Clock)) squashes.RemoveAt(i);

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

        /// <summary>One of the effect workshop's flipbooks, loaded from Resources/PawnRushSkillFx.</summary>
        class Sheet
        {
            public readonly Material material;
            public readonly int frames, cols, rows;
            public readonly float fps;
            public readonly bool loop;
            readonly Vector2 inset;

            public Sheet(Material material, Texture2D texture, int frames, float fps, bool loop)
            {
                this.material = material;
                this.frames = frames;
                this.fps = fps;
                this.loop = loop;
                cols = Mathf.Min(frames, 8);
                rows = (frames + cols - 1) / cols;
                // Half a texel in from each frame's edge, so the frame beside it never shows.
                inset = new Vector2(0.5f / texture.width, 0.5f / texture.height);
            }

            public void Uv(int frame, Vector2[] uv)
            {
                int c = frame % cols, r = frame / cols;
                float u0 = (float)c / cols + inset.x, u1 = (float)(c + 1) / cols - inset.x;
                float top = 1f - (float)r / rows - inset.y, bottom = 1f - (float)(r + 1) / rows + inset.y;
                uv[0] = new Vector2(u0, bottom);
                uv[1] = new Vector2(u1, bottom);
                uv[2] = new Vector2(u1, top);
                uv[3] = new Vector2(u0, top);
            }
        }

        /// <summary>A workshop picture played on a square: lying on the floor, or facing the camera
        /// (<see cref="billboard"/>), optionally over a piece's head.</summary>
        class Flipbook : Anim
        {
            readonly Sheet sheet;
            readonly Transform tf;
            readonly MeshRenderer mr;
            readonly Mesh mesh;
            readonly Vector2[] uv = new Vector2[4];
            readonly Color[] colors = new Color[4];
            int shownFrame = -1;
            float shownAlpha = -1f;

            public Vector3 at;
            public float size = 1f, alpha = 1f, delay;
            /// <summary>Seconds it lasts (loops fade out over the last quarter second); below zero, as long as the
            /// picture plays (looping ones: until destroyed).</summary>
            public float life = -1f;
            public bool billboard, hidden;
            /// <summary>Where <see cref="at"/> sits in the picture, from its middle, in picture widths (+y = up).</summary>
            public Vector2 pivot;
            public RagdollPawn overHead;
            public float headUp;
            /// <summary>Facing the camera: drawn this many metres nearer it along the line of sight, as big on screen
            /// as before, so the body or wall it bursts from does not hide it.</summary>
            public float pull;
            /// <summary>Facing the camera: pulled nearer still until its lower edge clears this height (a floor).</summary>
            public float keepAbove = float.NaN;
            Quaternion floor = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            /// <summary>Seconds one play takes.</summary>
            public float Length => sheet.frames / sheet.fps;

            public Flipbook(Transform parent, Sheet sheet, string name)
            {
                this.sheet = sheet;
                var go = new GameObject("Effect " + name);
                go.transform.SetParent(parent, false);
                tf = go.transform;
                mesh = new Mesh
                {
                    vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) },
                    triangles = new[] { 0, 2, 1, 0, 3, 2 },
                };
                sheet.Uv(0, uv);
                mesh.uv = uv;
                mesh.colors = colors;
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = sheet.material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
            }

            /// <summary>Lie flat with the picture's top towards <paramref name="forward"/>.</summary>
            public void Face(Vector3 forward)
            {
                forward.y = 0f;
                floor = Quaternion.LookRotation(Vector3.down, forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward);
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                float t = age - delay;
                if (life >= 0f && t >= life) return false;
                if (overHead == null && headUp > 0f) return false;   // the piece it hung over is gone
                int frame = t < 0f ? -1 : Mathf.FloorToInt(t * sheet.fps);
                if (frame >= sheet.frames)
                {
                    if (!sheet.loop) return false;
                    frame %= sheet.frames;
                }
                mr.enabled = frame >= 0 && !hidden;
                if (!mr.enabled) return true;
                if (frame != shownFrame)
                {
                    sheet.Uv(frame, uv);
                    mesh.uv = uv;
                    shownFrame = frame;
                }
                float a = alpha * (life >= 0f ? Mathf.Clamp01((life - t) / 0.25f) : 1f);
                if (!Mathf.Approximately(a, shownAlpha))
                {
                    for (int i = 0; i < 4; i++) colors[i] = new Color(1f, 1f, 1f, a);
                    mesh.colors = colors;
                    shownAlpha = a;
                }
                Vector3 p = overHead != null ? HeadOf(overHead) + Vector3.up * headUp : at;
                Quaternion rotation = floor;
                float s = size;
                if (billboard && cam != null)
                {
                    var view = cam.transform;
                    rotation = view.rotation;
                    Vector3 eye = view.position;
                    float dist = Vector3.Distance(eye, p), most = dist * 0.6f;
                    float d = Mathf.Min(pull, most);
                    float reach = 0.5f * (Mathf.Abs(view.up.y) + Mathf.Abs(view.right.y));   // lowest corner, per metre of size
                    while (!float.IsNaN(keepAbove) && d < most)
                    {
                        float k = (dist - d) / dist;
                        Vector3 c = Vector3.MoveTowards(p, eye, d) - rotation * new Vector3(pivot.x, pivot.y, 0f) * size * k;
                        if (c.y - reach * size * k >= keepAbove + 0.02f) break;
                        d += 0.1f;
                    }
                    if (dist > 0.01f)
                    {
                        p = Vector3.MoveTowards(p, eye, d);
                        s = size * (dist - d) / dist;
                    }
                }
                tf.SetPositionAndRotation(p - rotation * new Vector3(pivot.x, pivot.y, 0f) * s, rotation);
                tf.localScale = Vector3.one * s;
                return true;
            }

            public override void Destroy()
            {
                if (mesh != null) Object.Destroy(mesh);
                if (tf != null) Object.Destroy(tf.gameObject);
            }
        }

        Text3D MakeText(string text, Color color, float height) => new Text3D(root, font, text, color, height);

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
            protected static float EaseOut(float t) => 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            protected static float Back(float t) { t = Mathf.Clamp01(t); const float c = 2.2f; return 1f + (c + 1f) * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f); }
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

        /// <summary>A bishop's lit squares: the light-up played once, then the pulse looping while its wire lasts,
        /// fading out in 0.35 s once the wire is gone.</summary>
        class TileSet
        {
            public Object source;
            public Flipbook lightUp, hold;
            public readonly List<Vector3> squares = new List<Vector3>();
            /// <summary>One square's side (metres).</summary>
            public float square;
            float fadeLeft = -1f;

            /// <summary>False once it has faded out.</summary>
            public bool Step(float dt, Camera cam, bool sourceGone)
            {
                if (sourceGone && fadeLeft < 0f) fadeLeft = 0.35f;
                if (fadeLeft >= 0f)
                {
                    fadeLeft -= dt;
                    if (fadeLeft <= 0f) return false;
                }
                float a = fadeLeft >= 0f ? fadeLeft / 0.35f : 1f;
                if (lightUp != null)
                {
                    lightUp.alpha = a;
                    if (!lightUp.Step(dt, cam)) { lightUp.Destroy(); lightUp = null; }
                }
                hold.alpha = a;
                hold.Step(dt, cam);
                return true;
            }

            public void Destroy()
            {
                lightUp?.Destroy();
                hold?.Destroy();
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
