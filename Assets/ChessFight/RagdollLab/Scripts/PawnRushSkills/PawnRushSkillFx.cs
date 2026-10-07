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
    /// for holding only the two pieces), camera shake, a white flash on the piece that is hit, and pop-up words.
    /// Test-bed art: lines, cubes and text built in code, no sound yet. It listens to RagdollPawn.SkillFx, which
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

        static readonly Color QueenGold = new Color(1f, 0.8f, 0.26f);
        static readonly Color QueenDeep = new Color(1f, 0.6f, 0.08f);
        static readonly Color RookOrange = new Color(1f, 0.54f, 0.24f);
        static readonly Color BishopViolet = new Color(0.65f, 0.48f, 1f);
        static readonly Color BishopLight = new Color(0.86f, 0.78f, 1f);
        static readonly Color KnightSky = new Color(0.36f, 0.78f, 1f);
        static readonly Color Ink = new Color(0.05f, 0.08f, 0.19f);
        static readonly Color[] Stone = { new Color(0.85f, 0.8f, 0.7f), new Color(0.6f, 0.56f, 0.47f), new Color(1f, 0.54f, 0.24f) };
        static readonly Color[] Blocks = { new Color(0.78f, 0.27f, 0.23f), new Color(0.96f, 0.91f, 0.82f) };

        LabGame game;
        Transform root;
        Font font;
        Material lineMat, whiteMat;
        readonly Dictionary<Color, Material> litMats = new Dictionary<Color, Material>();
        Mesh boxMesh, discMesh;

        // hit stop and shake
        float stopLeft, stopResume = 1f, shakeAmp, shakeLeft, shakeTotal, shakeClock;
        bool stopping;

        readonly List<Anim> anims = new List<Anim>();
        readonly Dictionary<RagdollPawn, Flash> flashes = new Dictionary<RagdollPawn, Flash>();
        readonly List<Squash> squashes = new List<Squash>();
        readonly Dictionary<Object, TileSet> tileSets = new Dictionary<Object, TileSet>();
        readonly Dictionary<RagdollPawn, float> stompedAt = new Dictionary<RagdollPawn, float>();
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
            boxMesh = WhiteBox();
            discMesh = Disc(48);
            RagdollPawn.SkillFx += OnSkillFx;
        }

        void OnDestroy()
        {
            RagdollPawn.SkillFx -= OnSkillFx;
            if (stopping) Time.timeScale = stopResume;
            foreach (var f in flashes.Values) f.Restore();
            foreach (var s in squashes) s.Restore();
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
                    Sparks(e.at + Vector3.up * 0.3f, QueenGold, 6, 0.6f, 0.25f);
                    break;
                case SkillFxKind.RookHit: RookHit(e); break;
                case SkillFxKind.RookStop: RookStop(e); break;
                case SkillFxKind.RookWall:
                    HitStop(0.06f);
                    Shake(0.1f, 0.22f);
                    StarFlash(e.at, Color.white, 0.6f);
                    Debris(e.at, 10, Stone, 2.5f, 2f, 0.07f);
                    Word("쿵!", e.at + Vector3.up * 0.9f, RookOrange, 0.5f, 0.9f);
                    break;
                case SkillFxKind.RookBarricade:
                    HitStop(0.07f);
                    Shake(0.1f, 0.24f);
                    Debris(e.at + Vector3.up * 0.3f, 22, Blocks, 3.5f, 3f, 0.16f);
                    StarFlash(e.at + Vector3.up * 0.5f, RookOrange, 0.8f);
                    Word("와장창!", e.at + Vector3.up * 1.6f, RookOrange, 0.55f, 1f);
                    break;
                case SkillFxKind.BishopWire: BishopWire(e); break;
                case SkillFxKind.BishopTrip: BishopTrip(e); break;
                case SkillFxKind.KnightStomp: KnightStomp(e); break;
                case SkillFxKind.KnightLand: KnightLand(e); break;
            }
        }

        // Queen A: two rings run out over the floor, the inner one (1.5 m, knocks down) bright and thick, the
        // outer one (3 m, pushes) thin; the screen shakes and its edges split red and cyan for a moment.
        void QueenBlast(SkillFxEvent e)
        {
            Vector3 c = Ground(e.at);
            float inner = PawnRushSkillsParams()?.queenInner ?? 1.5f, outer = e.size > 0f ? e.size : 3f;
            DiscPulse(c, inner, QueenGold, 0.22f, 0.45f, 0.6f);
            RingPulse(c, 0f, inner, QueenDeep, 0.24f, 0.22f, 0.55f);
            RingPulse(c, 0f, outer, QueenGold, 0.08f, 0.45f, 0.85f);
            HitStop(0.06f);
            Shake(0.13f, 0.28f);
            Edge(0.22f);
        }

        // Rook A: each piece it sends flying gives stone chips, a white star and its number, and a longer stop
        // (0.04, 0.06, 0.08 s) and shake than the one before; the fourth, which stops it, gives a big "쿵".
        void RookHit(SkillFxEvent e)
        {
            int i = Mathf.Clamp(e.count, 1, 3);
            HitStop(new[] { 0.04f, 0.06f, 0.08f }[i - 1]);
            Shake(new[] { 0.05f, 0.08f, 0.11f }[i - 1], 0.16f + 0.03f * i);
            FlashWhite(e.target, 0.06f);
            StarFlash(e.at, Color.white, 0.45f + 0.1f * i);
            Debris(e.at, 6 + 3 * i, Stone, 2.6f, 2.4f, 0.07f);
            Word(i.ToString(), HeadOf(e.target) + Vector3.up * 0.45f, Color.white, 0.38f, 0.6f);
        }

        void RookStop(SkillFxEvent e)
        {
            HitStop(0.09f);
            Shake(0.17f, 0.32f);
            FlashWhite(e.target, 0.07f);
            StarFlash(e.at, RookOrange, 0.9f);
            Debris(e.at, 18, Stone, 3f, 3f, 0.08f);
            RingPulse(Ground(e.at), 0f, 1.3f, RookOrange, 0.2f, 0.35f, 0.7f);
            Word("쿵!", e.at + Vector3.up * 1.1f, RookOrange, 0.75f, 1f);
        }

        // Bishop B: the board squares under the X light up violet, from the middle out; the square an enemy trips
        // on jumps up ("덜컥!").
        void BishopWire(SkillFxEvent e)
        {
            if (e.source == null) return;
            Vector3 c = Ground(e.at);
            Vector3 f = e.dir;
            f.y = 0f;
            f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            float half = (e.size > 0f ? e.size : 4.2f) * 0.5f;
            float size = half / (2f * Mathf.Sqrt(2f));   // the X's arms run corner to corner across two squares
            var set = new TileSet { source = e.source, born = Clock };
            for (int i = -2; i <= 2; i++)
                foreach (int j in i == 0 ? new[] { 0 } : new[] { i, -i })
                {
                    var tile = new Tile
                    {
                        ring = Mathf.Abs(i),
                        at = c + (r * j + f * i) * size + Vector3.up * 0.02f,
                        renderer = Box(root, "Bishop square", new Vector3(size * 0.92f, 0.03f, size * 0.92f)),
                    };
                    tile.renderer.transform.rotation = Quaternion.LookRotation(f, Vector3.up);
                    tile.renderer.transform.position = tile.at;
                    set.tiles.Add(tile);
                }
            if (tileSets.TryGetValue(e.source, out var old)) old.Destroy();
            tileSets[e.source] = set;
        }

        void BishopTrip(SkillFxEvent e)
        {
            HitStop(0.05f);
            Shake(0.06f, 0.16f);
            FlashWhite(e.target, 0.06f);
            Vector3 at = e.at;
            if (e.source != null && tileSets.TryGetValue(e.source, out var set))
            {
                Tile best = null;
                float bestD = float.MaxValue;
                foreach (var t in set.tiles)
                {
                    float d = Vector3.Distance(Flat(t.at), Flat(e.at));
                    if (d < bestD) { bestD = d; best = t; }
                }
                if (best != null) { best.popAt = Clock; at = best.at; }
            }
            Sparks(at + Vector3.up * 0.25f, BishopLight, 9, 0.7f, 0.3f);
            Word("덜컥!", at + Vector3.up * 1.5f, BishopLight, 0.55f, 0.9f);
        }

        // Knight B: the piece stomped on squashes flat and springs back, a long stop (0.09 s), "뿅!", and the
        // knight's bounce off it lands with a small "통".
        void KnightStomp(SkillFxEvent e)
        {
            HitStop(0.09f);
            Shake(0.1f, 0.2f);
            FlashWhite(e.target, 0.07f);
            StarFlash(e.at, Color.white, 0.75f);
            Sparks(e.at, KnightSky, 12, 0.9f, 0.32f);
            Word("뿅!", e.at + Vector3.up * 0.75f, KnightSky, 0.75f, 0.95f);
            StartSquash(e.target);
            Dizzy(e.target, 0.45f, 1.8f);
            if (e.by != null) stompedAt[e.by] = Clock;
        }

        void KnightLand(SkillFxEvent e)
        {
            Vector3 g = Ground(e.at);
            if (e.by != null && stompedAt.TryGetValue(e.by, out float t) && Clock - t < 2.5f)
            {
                stompedAt.Remove(e.by);
                Word("통", g + Vector3.up * 0.9f, Color.white, 0.35f, 0.6f);
                RingPulse(g, 0.1f, 0.8f, Color.white, 0.06f, 0.25f, 0.45f);
                return;
            }
            RingPulse(g, 0.1f, e.size > 0f ? e.size : 1.5f, new Color(0.92f, 0.86f, 0.74f), 0.08f, 0.3f, 0.6f);
        }

        PawnRushSkillParams PawnRushSkillsParams()
        {
            var bed = GetComponent<PawnRushSkillBed>();
            return bed != null ? bed.skills : null;
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

        void Word(string text, Vector3 at, Color color, float height, float life) =>
            anims.Add(new Pop(MakeText(text, color, height), at, life, 0.35f, height));

        void StarFlash(Vector3 at, Color color, float height) =>
            anims.Add(new Pop(MakeText("★", color, height), at, 0.22f, 0f, height) { grow = true });

        void Dizzy(RagdollPawn pawn, float delay, float life)
        {
            if (pawn == null) return;
            var stars = new Text3D[3];
            for (int i = 0; i < 3; i++) stars[i] = MakeText("★", new Color(1f, 0.89f, 0.48f), 0.16f);
            anims.Add(new DizzyStars(stars, pawn, delay, life));
        }

        void Sparks(Vector3 at, Color color, int n, float length, float life)
        {
            var lines = new LineRenderer[n];
            var dirs = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                lines[i] = SkillMarks.Line(root, "Spark");
                dirs[i] = Random.onUnitSphere;
                dirs[i].y = Mathf.Abs(dirs[i].y) * 0.8f + 0.1f;
            }
            anims.Add(new SparkBurst(lines, dirs, at, color, length, life));
        }

        void Debris(Vector3 at, int n, Color[] colors, float speed, float up, float size)
        {
            float ground = Ground(at).y;
            for (int i = 0; i < n; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.name = "Chip";
                go.transform.SetParent(root, false);
                go.GetComponent<MeshRenderer>().sharedMaterial = Lit(colors[i % colors.Length]);
                float s = size * Random.Range(0.6f, 1.4f);
                go.transform.localScale = Vector3.one * s;
                Vector3 v = Random.insideUnitSphere * speed;
                v.y = up * Random.Range(0.4f, 1.3f);
                anims.Add(new Chip(go.transform, at, v, Random.insideUnitSphere * 720f, ground + s * 0.5f, Random.Range(0.9f, 1.4f)));
            }
        }

        void RingPulse(Vector3 center, float from, float to, Color color, float width, float grow, float life)
        {
            var lr = SkillMarks.Line(root, "Ring");
            anims.Add(new Ring(lr, center, from, to, color, width, grow, life));
        }

        void DiscPulse(Vector3 center, float radius, Color color, float grow, float life, float alpha)
        {
            var go = new GameObject("Disc");
            go.transform.SetParent(root, false);
            go.transform.position = center + Vector3.up * 0.03f;
            go.AddComponent<MeshFilter>().sharedMesh = discMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(lineMat) { hideFlags = HideFlags.HideAndDontSave };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            anims.Add(new DiscAnim(mr, radius, color, grow, life, alpha));
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
                if (!kv.Value.Step(Clock, kv.Key == null)) { kv.Value.Destroy(); gone.Add(kv.Key); }
            foreach (var k in gone) tileSets.Remove(k);

            if (edge != null && edge.Alive) edge.Step(dt, cam);

            if (shakeLeft > 0f && cam != null)
            {
                shakeLeft -= dt;
                shakeClock += dt;
                bool free = ViewOverride == null && game != null && game.labCamera != null && game.labCamera.freeMode;
                if (!free)
                {
                    float k = shakeAmp * Mathf.Clamp01(shakeLeft / Mathf.Max(0.01f, shakeTotal));
                    float x = (Mathf.PerlinNoise(shakeClock * 32f, 0.3f) - 0.5f) * 2f;
                    float y = (Mathf.PerlinNoise(0.7f, shakeClock * 32f) - 0.5f) * 2f;
                    cam.transform.position += (cam.transform.right * x + cam.transform.up * y * 0.7f) * k;
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

        Material Lit(Color c)
        {
            if (litMats.TryGetValue(c, out var m)) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, color = c };
            litMats[c] = m;
            return m;
        }

        MeshRenderer Box(Transform parent, string name, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = boxMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(lineMat) { hideFlags = HideFlags.HideAndDontSave };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mr;
        }

        /// <summary>A unit cube with white vertex colours (Sprites/Default tints by vertex colour).</summary>
        static Mesh WhiteBox()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            Destroy(tmp);
            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors;
            return mesh;
        }

        static Mesh Disc(int segments)
        {
            var v = new Vector3[segments + 1];
            var c = new Color[segments + 1];
            var t = new int[segments * 3];
            c[0] = Color.white;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                v[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                c[i + 1] = new Color(1f, 1f, 1f, 0.35f);
                t[i * 3] = 0;
                t[i * 3 + 1] = (i + 1) % segments + 1;
                t[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { vertices = v, colors = c, triangles = t };
            mesh.RecalculateBounds();
            return mesh;
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

        class Pop : Anim
        {
            readonly Text3D text;
            readonly Vector3 at;
            readonly float life, rise;
            public bool grow;

            public Pop(Text3D text, Vector3 at, float life, float rise, float height)
            {
                this.text = text;
                this.at = at;
                this.life = life;
                this.rise = rise;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                float t = age / life;
                if (t >= 1f) return false;
                float scale = grow ? 0.6f + 0.7f * EaseOut(t) : t < 0.18f ? Back(t / 0.18f) : 1f;
                float alpha = grow ? 1f - t : t > 0.7f ? 1f - (t - 0.7f) / 0.3f : 1f;
                text.Place(at + Vector3.up * rise * EaseOut(t), cam, scale, alpha);
                return true;
            }

            public override void Destroy() => text.Destroy();
        }

        class DizzyStars : Anim
        {
            readonly Text3D[] stars;
            readonly RagdollPawn pawn;
            readonly float delay, life;

            public DizzyStars(Text3D[] stars, RagdollPawn pawn, float delay, float life)
            {
                this.stars = stars;
                this.pawn = pawn;
                this.delay = delay;
                this.life = life;
                foreach (var s in stars) s.Place(Vector3.down * 100f, null, 0.001f, 0f);
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (pawn == null || age > delay + life) return false;
                Vector3 head = HeadOf(pawn) + Vector3.up * 0.32f;
                float show = age < delay ? 0f : Mathf.Clamp01((delay + life - age) / 0.3f);
                for (int i = 0; i < stars.Length; i++)
                {
                    float a = age * 5f + i * Mathf.PI * 2f / stars.Length;
                    stars[i].Place(head + new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a * 2f) * 0.03f, Mathf.Sin(a) * 0.3f), cam, show > 0f ? 1f : 0.001f, show);
                }
                return true;
            }

            public override void Destroy() { foreach (var s in stars) s.Destroy(); }
        }

        class SparkBurst : Anim
        {
            readonly LineRenderer[] lines;
            readonly Vector3[] dirs;
            readonly Vector3 at;
            readonly Color color;
            readonly float length, life;

            public SparkBurst(LineRenderer[] lines, Vector3[] dirs, Vector3 at, Color color, float length, float life)
            {
                this.lines = lines;
                this.dirs = dirs;
                this.at = at;
                this.color = color;
                this.length = length;
                this.life = life;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                float t = age / life;
                if (t >= 1f) return false;
                float r0 = length * EaseOut(t), r1 = r0 + length * 0.45f * (1f - t);
                var c = new Color(color.r, color.g, color.b, 1f - t * 0.7f);
                for (int i = 0; i < lines.Length; i++)
                    SkillMarks.Segment(lines[i], at + dirs[i] * r0, at + dirs[i] * r1, c, 0.045f * (1f - t) + 0.01f, false);
                return true;
            }

            public override void Destroy() { foreach (var l in lines) if (l != null) Object.Destroy(l.gameObject); }
        }

        class Chip : Anim
        {
            readonly Transform t;
            Vector3 pos, vel;
            readonly Vector3 spin;
            readonly float floor, life;
            bool bounced;

            public Chip(Transform t, Vector3 pos, Vector3 vel, Vector3 spin, float floor, float life)
            {
                this.t = t;
                this.pos = pos;
                this.vel = vel;
                this.spin = spin;
                this.floor = floor;
                this.life = life;
                t.position = pos;
            }

            public override bool Step(float dt, Camera cam)
            {
                // Game time: the chips hold still in a hit stop and slow down with the slow motion.
                float gdt = Time.deltaTime;
                age += gdt;
                if (age >= life) return false;
                vel += Physics.gravity * gdt;
                pos += vel * gdt;
                if (pos.y < floor)
                {
                    pos.y = floor;
                    vel = bounced ? Vector3.zero : new Vector3(vel.x * 0.4f, -vel.y * 0.3f, vel.z * 0.4f);
                    bounced = true;
                }
                t.position = pos;
                if (vel.sqrMagnitude > 0.01f) t.Rotate(spin * gdt, Space.World);
                if (age > life - 0.25f) t.localScale *= 0.9f;
                return true;
            }

            public override void Destroy() { if (t != null) Object.Destroy(t.gameObject); }
        }

        class Ring : Anim
        {
            readonly LineRenderer lr;
            readonly Vector3 center;
            readonly float from, to, width, grow, life;
            readonly Color color;

            public Ring(LineRenderer lr, Vector3 center, float from, float to, Color color, float width, float grow, float life)
            {
                this.lr = lr;
                this.center = center;
                this.from = from;
                this.to = to;
                this.color = color;
                this.width = width;
                this.grow = grow;
                this.life = life;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (age >= life) return false;
                float r = Mathf.Lerp(from, to, EaseOut(age / grow));
                float fade = age < grow ? 1f : 1f - (age - grow) / Mathf.Max(0.01f, life - grow);
                SkillMarks.Circle(lr, center, Mathf.Max(0.02f, r), new Color(color.r, color.g, color.b, color.a * fade), width, 64);
                return true;
            }

            public override void Destroy() { if (lr != null) Object.Destroy(lr.gameObject); }
        }

        class DiscAnim : Anim
        {
            readonly MeshRenderer mr;
            readonly float radius, grow, life, alpha;
            readonly Color color;

            public DiscAnim(MeshRenderer mr, float radius, Color color, float grow, float life, float alpha)
            {
                this.mr = mr;
                this.radius = radius;
                this.color = color;
                this.grow = grow;
                this.life = life;
                this.alpha = alpha;
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (age >= life) return false;
                float r = Mathf.Max(0.02f, radius * EaseOut(age / grow));
                mr.transform.localScale = new Vector3(r, 1f, r);
                mr.sharedMaterial.color = new Color(color.r, color.g, color.b, alpha * (1f - age / life));
                return true;
            }

            public override void Destroy() { if (mr != null) { Object.Destroy(mr.sharedMaterial); Object.Destroy(mr.gameObject); } }
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

            /// <summary>Flat at once, then a springy way back (0.6 s). False once it is over.</summary>
            public bool Apply(float clock)
            {
                if (bone == null) return false;
                if (bone.position == lastSet) bone.position -= lastOffset;   // nothing re-posed it since last frame
                float t = clock - born;
                if (t > 0.65f) { Restore(); return false; }
                float flat = t < 0.06f ? 0.55f * (t / 0.06f) : 0.55f * Mathf.Exp(-(t - 0.06f) * 7f) * Mathf.Cos((t - 0.06f) * 24f);
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

        class Tile
        {
            public int ring;
            public Vector3 at;
            public MeshRenderer renderer;
            public float popAt = -1f;
        }

        class TileSet
        {
            public Object source;
            public float born, endAt = -1f;
            public readonly List<Tile> tiles = new List<Tile>();

            /// <summary>Squares light from the middle out and pulse while the wire lives, then fade. False = gone.</summary>
            public bool Step(float clock, bool sourceGone)
            {
                if (sourceGone && endAt < 0f) endAt = clock;
                float fade = endAt < 0f ? 1f : 1f - (clock - endAt) / 0.35f;
                if (fade <= 0f) return false;
                foreach (var t in tiles)
                {
                    if (t.renderer == null) continue;
                    float on = Mathf.Clamp01((clock - born - t.ring * 0.09f) / 0.1f);
                    float pulse = 0.85f + 0.15f * Mathf.Sin(clock * 6f + t.ring);
                    float lift = 0f, pop = 0f;
                    if (t.popAt >= 0f)
                    {
                        float p = clock - t.popAt;
                        if (p < 0.45f)
                        {
                            lift = 0.24f * Mathf.Sin(p / 0.45f * Mathf.PI) * (1f - p / 0.45f * 0.3f);
                            pop = 1f - p / 0.45f;
                        }
                    }
                    t.renderer.transform.position = t.at + Vector3.up * lift;
                    var baseColor = Color.Lerp(BishopViolet, BishopLight, pop);
                    t.renderer.transform.localScale = new Vector3(t.renderer.transform.localScale.x, 0.03f + lift * 0.6f, t.renderer.transform.localScale.z);
                    t.renderer.sharedMaterial.color = new Color(baseColor.r, baseColor.g, baseColor.b, (0.5f + 0.35f * pop) * on * pulse * fade);
                }
                return true;
            }

            public void Destroy()
            {
                foreach (var t in tiles)
                    if (t.renderer != null) { Object.Destroy(t.renderer.sharedMaterial); Object.Destroy(t.renderer.gameObject); }
                tiles.Clear();
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
