using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// A rook's wall (JY-skill-lab, 2026-11-21; the kits' second version, Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.4): a stone
    /// box standing on the floor along a line, chest high, for a while. The wall charge (R-상, RagdollPawn.KitTwo.cs)
    /// raises one behind it as it runs. On the host it is solid: a box collider the pieces run into and must climb (the
    /// rook that raised it does not touch it); every other PC is told when it stands and when it breaks
    /// (SkillFxKind.KitWall, KitWallBreak) and stands a picture of it with no collider, as the bodies it shows are the
    /// host's. It rises out of the floor, stands its time, and sinks back; an enemy rook's charge breaks it at once.
    /// 11-26 (USER_SKILLS 5.3, the user's own walls and the 4판's 성벽! alike): no longer a grey-brown box - sand bricks
    /// with dark mortar, battlements on top and an ink edge, in one-metre pieces that rise one after another (to 110% and
    /// back) as it grows; a band flowing at its foot (red stripes to the other team, teal to the owner's, who also see it
    /// half clear); shaking its last second, then tumbling down from its head into dust and a few bouncing bricks (the
    /// effects draw those: OnCrumble). Not seen in Unity by anyone yet.
    /// </summary>
    public class PawnRushKitWall : MonoBehaviour
    {
        public static readonly List<PawnRushKitWall> All = new List<PawnRushKitWall>();
        static int nextId;
        static Material stone;   // (the old grey-brown box's; unused since the bricks, 11-26)
        const float SinkTime = 0.3f;

        public int Id { get; private set; }
        public RagdollPawn Owner { get; private set; }
        /// <summary>A collider (the host's), or only a picture (another PC's).</summary>
        public bool Solid { get; private set; }
        public Vector3 From => from;
        public Vector3 To => to;
        public Vector3 Middle => (from + to) * 0.5f + Vector3.up * (height * 0.5f);
        public bool Standing => left > 0f;

        Vector3 from, to;
        float height, thick, left, rise, risen;
        BoxCollider box;
        Rigidbody body;
        Transform picture;   // (the pieces' root)

        // ---------------------------------------------------------------- the look (11-26)

        /// <summary>The team of the piece played at this PC (the effects set it every frame): a wall of its team is drawn
        /// half clear with a teal band, any other with a red one.</summary>
        public static int ViewerTeam = -1;
        /// <summary>A wall starts tumbling down (from, to: its line): the effects add the dust and the bouncing bricks.</summary>
        public static System.Action<PawnRushKitWall, Vector3, Vector3> OnCrumble;

        sealed class Piece
        {
            public Transform root, body, top, inkBody, inkTop, band;
            public Renderer bodyR, topR, bandR;
            public float born;
        }
        readonly List<Piece> pieces = new List<Piece>();
        MaterialPropertyBlock mpb;   // (made on first use: no Unity objects in a component's field initializers)
        static Material brickMat, brickClearMat, inkMat, bandRedMat, bandTealMat;
        static Texture2D brickTex, stripeTex;
        float created, crumbleAt = -1f;
        bool firstFit = true;
        const float PieceLength = 1f;

        /// <summary>The solid wall's collider (null for a picture).</summary>
        public Collider Collider => box;

        /// <summary>11-26 (the user's own skills): drawn by someone else (the holy sword's cross): no stones of its own.</summary>
        public bool Hidden
        {
            get => hidden;
            set
            {
                hidden = value;
                if (picture != null) picture.gameObject.SetActive(!value);
            }
        }
        bool hidden;

        /// <summary>11-26 (USER_SKILLS 2.1): nobody stands on its top - a piece there is slid off its side (the host's).</summary>
        public bool NoTop { get; set; }

        /// <summary>The wall of <paramref name="owner"/>'s that is still standing (the newest), or null.</summary>
        public static PawnRushKitWall OwnedBy(RagdollPawn owner)
        {
            for (int i = All.Count - 1; i >= 0; i--)
                if (All[i] != null && All[i].Owner == owner && All[i].Standing) return All[i];
            return null;
        }

        // A wall that pushes the other team back off its face (the team ultimate's 7th rank): whose, which way its face
        // looks (the way they run), how fast.
        bool pushing;
        int pushTeam;
        Vector3 pushWay;
        float pushSpeed;

        /// <summary>Host: the pieces of the team that is not <paramref name="ownerTeam"/>'s, just before the wall's face
        /// (coming along <paramref name="way"/>), are pushed back at <paramref name="speed"/> m/s while it stands.</summary>
        public void PushBack(int ownerTeam, Vector3 way, float speed)
        {
            way.y = 0f;
            if (!Solid || way.sqrMagnitude < 1e-4f) return;
            pushing = true;
            pushTeam = ownerTeam;
            pushWay = way.normalized;
            pushSpeed = speed;
        }

        void FixedUpdate()
        {
            if (NoTop) SlideOffTop();
            if (!pushing || !Standing || box == null || !box.enabled) return;
            Vector3 mid = (from + to) * 0.5f, side = Vector3.Cross(Vector3.up, pushWay);
            float half = new Vector3(to.x - from.x, 0f, to.z - from.z).magnitude * 0.5f + 0.3f;
            foreach (var p in RagdollPawn.All)
            {
                if (p == null || p.NetworkPuppet || !Teams.AreEnemies(p.Team, pushTeam) || p.State != PawnState.Active) continue;
                Vector3 d = p.Hips.position - mid;
                float along = Vector3.Dot(new Vector3(d.x, 0f, d.z), pushWay), across = Vector3.Dot(new Vector3(d.x, 0f, d.z), side);
                if (Mathf.Abs(across) > half || along > 0.2f || along < -(thick * 0.5f + 2f) || Mathf.Abs(d.y) > height + 1f) continue;
                Vector3 v = p.Hips.linearVelocity;   // (the hips' body: what the run drives)
                float back = -Vector3.Dot(new Vector3(v.x, 0f, v.z), pushWay);
                if (back < pushSpeed) p.AddVelocity(-pushWay * (pushSpeed - back));
            }
        }

        /// <summary>A piece standing on the top (its feet over the box, near its top) is pushed off the nearer side: no wall is a
        /// step up (USER_SKILLS 2.1).</summary>
        void SlideOffTop()
        {
            if (box == null || !box.enabled || !Solid) return;
            Vector3 mid = (from + to) * 0.5f, line = to - from;
            line.y = 0f;
            float half = Mathf.Max(0.15f, line.magnitude * 0.5f);
            Vector3 along = line.sqrMagnitude > 1e-4f ? line.normalized : Vector3.forward, side = Vector3.Cross(Vector3.up, along);
            float top = Mathf.Min(from.y, to.y) + height * Mathf.Clamp01(risen);
            foreach (var p in RagdollPawn.All)
            {
                if (p == null || p.NetworkPuppet) continue;
                Vector3 f = p.FeetPoint - mid;
                float a = Vector3.Dot(new Vector3(f.x, 0f, f.z), along), c = Vector3.Dot(new Vector3(f.x, 0f, f.z), side);
                if (Mathf.Abs(a) > half + 0.25f || Mathf.Abs(c) > thick * 0.5f + 0.3f) continue;
                if (p.FeetPoint.y < top - 0.35f || p.FeetPoint.y > top + 1.2f) continue;
                Vector3 off = side * (c >= 0f ? 1f : -1f);
                Vector3 v = p.Hips.linearVelocity;
                if (Vector3.Dot(new Vector3(v.x, 0f, v.z), off) < 3f) p.AddVelocity(off * 3.5f);
            }
        }

        /// <summary>A wall that moves (the 7th rank): on the host a kinematic body, so it pushes what it meets as it goes.</summary>
        public void MakeMoving()
        {
            if (!Solid || body != null) return;
            body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }

        /// <summary>A number for a new wall (1..250, then round again).</summary>
        public static int NextId()
        {
            nextId = nextId % 250 + 1;
            return nextId;
        }

        public static PawnRushKitWall Find(int id)
        {
            foreach (var w in All)
                if (w != null && w.Id == id) return w;
            return null;
        }

        /// <summary>A wall from <paramref name="a"/> to <paramref name="b"/> (points on the floor), <paramref name="tall"/>
        /// high and <paramref name="thickness"/> thick, standing <paramref name="seconds"/> once risen over
        /// <paramref name="riseTime"/>.</summary>
        public static PawnRushKitWall Raise(int id, RagdollPawn owner, Vector3 a, Vector3 b, float tall, float thickness, float seconds, float riseTime, bool solid)
        {
            var old = Find(id);
            if (old != null) old.Break();   // (the number came round, or a moment told twice)
            var go = new GameObject("Rook wall " + id);
            var w = go.AddComponent<PawnRushKitWall>();
            w.Id = id;
            w.Owner = owner;
            w.Solid = solid;
            w.height = Mathf.Max(0.2f, tall);
            w.thick = Mathf.Max(0.1f, thickness);
            w.left = Mathf.Max(0.1f, seconds);
            w.rise = Mathf.Max(0.01f, riseTime);
            w.created = Time.time;
            var stones = new GameObject("Stones");
            stones.transform.SetParent(go.transform, false);
            w.picture = stones.transform;
            if (solid)
            {
                w.box = go.AddComponent<BoxCollider>();
                if (owner != null) owner.IgnoreBody(w.box, true);
            }
            w.SetLine(a, b);
            All.Add(w);
            return w;
        }

        /// <summary>The bricks: two across and four up a metre (0.5 x 0.25 m), sand coloured with a little variation, the
        /// mortar dark brown; made once.</summary>
        static Texture2D BrickTexture()
        {
            if (brickTex != null) return brickTex;
            const int N = 128;
            brickTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Wall bricks", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            var mortar = new Color32(92, 66, 44, 255);
            var rnd = new System.Random(7);
            var tints = new Color32[16];
            for (int i = 0; i < tints.Length; i++)
            {
                float k = 0.9f + 0.18f * (float)rnd.NextDouble();
                tints[i] = new Color32((byte)Mathf.Clamp(222 * k, 0, 255), (byte)Mathf.Clamp(186 * k, 0, 255), (byte)Mathf.Clamp(128 * k, 0, 255), 255);
            }
            for (int y = 0; y < N; y++)
            {
                int row = y / (N / 4);
                int yIn = y % (N / 4);
                int shift = row % 2 == 0 ? 0 : N / 4;
                for (int x = 0; x < N; x++)
                {
                    int xs = (x + shift) % N, col = xs / (N / 2), xIn = xs % (N / 2);
                    bool joint = yIn < 3 || xIn < 3;
                    var c = joint ? mortar : tints[(row * 2 + col) % tints.Length];
                    if (!joint && (yIn == 3 || xIn == 3)) c = new Color32((byte)(c.r * 0.9f), (byte)(c.g * 0.9f), (byte)(c.b * 0.9f), 255);
                    px[y * N + x] = c;
                }
            }
            brickTex.SetPixels32(px);
            brickTex.Apply(true);
            return brickTex;
        }

        /// <summary>Diagonal stripes, white over clear, for the band at the foot (tinted by its material).</summary>
        static Texture2D StripeTexture()
        {
            if (stripeTex != null) return stripeTex;
            const int N = 64;
            stripeTex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "Wall band stripes", wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    px[y * N + x] = ((x + y) / (N / 4)) % 2 == 0 ? new Color32(255, 255, 255, 255) : new Color32(150, 150, 150, 255);
            stripeTex.SetPixels32(px);
            stripeTex.Apply(true);
            return stripeTex;
        }

        static Material NewWallMaterial(Color color, Texture2D tex, string name)
        {
            Material m = LitMaterials.Lit != null ? LitMaterials.NewLit(color, 0.08f) : new Material(Shader.Find("Sprites/Default")) { color = color };
            m.name = name;
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                m.mainTexture = tex;
            }
            return m;
        }

        static void MakeClear(Material m, float alpha)
        {
            // URP Lit, transparent.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            c.a = alpha;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
        }

        static void Materials()
        {
            if (brickMat != null) return;
            brickMat = NewWallMaterial(Color.white, BrickTexture(), "Wall bricks");
            brickClearMat = NewWallMaterial(Color.white, BrickTexture(), "Wall bricks (own side)");
            MakeClear(brickClearMat, 0.45f);
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            inkMat = unlit != null ? new Material(unlit) { name = "Wall ink" } : new Material(Shader.Find("Sprites/Default")) { name = "Wall ink" };
            if (inkMat.HasProperty("_BaseColor")) inkMat.SetColor("_BaseColor", new Color(0.13f, 0.09f, 0.06f));
            inkMat.color = new Color(0.13f, 0.09f, 0.06f);
            if (inkMat.HasProperty("_Cull")) inkMat.SetFloat("_Cull", 1f);   // (front faces off: an outline round the piece)
            bandRedMat = NewWallMaterial(new Color(0.95f, 0.25f, 0.2f), StripeTexture(), "Wall band (theirs)");
            bandTealMat = NewWallMaterial(new Color(0.2f, 0.78f, 0.72f), StripeTexture(), "Wall band (ours)");
        }

        Piece NewPiece(int index)
        {
            Materials();
            var p = new Piece();
            p.root = new GameObject("Piece " + index).transform;
            p.root.SetParent(picture, false);
            p.body = Cube(p.root, brickMat, "Bricks", out p.bodyR);
            p.inkBody = Cube(p.root, inkMat, "Bricks ink", out _);
            p.top = Cube(p.root, brickMat, "Battlement", out p.topR);
            p.inkTop = Cube(p.root, inkMat, "Battlement ink", out _);
            p.band = Cube(p.root, bandRedMat, "Foot band", out p.bandR);
            // The first pieces of a wall raised whole rise one after another; a growing wall's come as it grows.
            p.born = firstFit ? created + index * 0.028f : Time.time;
            return p;
        }

        static Transform Cube(Transform parent, Material m, string name, out Renderer r)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = c.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            c.name = name;
            c.transform.SetParent(parent, false);
            r = c.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = m == inkMat ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            return c.transform;
        }

        /// <summary>The look every frame: the pieces along the line, each risen as far as its own rise and the wall's allow;
        /// the band's stripes flowing; its last second shaking; tumbling down from the head once it is over.</summary>
        void FitLook(float length)
        {
            if (picture == null || hidden) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            int n = Mathf.Max(1, Mathf.CeilToInt(length / PieceLength - 0.01f));
            while (pieces.Count < n) pieces.Add(NewPiece(pieces.Count));
            firstFit = false;
            bool ours = Owner != null && ViewerTeam >= 0 && Owner.Team == ViewerTeam;
            float now = Time.time;
            float shake = Standing && left < 1f ? 0.03f : 0f;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (i >= n)
                {
                    p.root.gameObject.SetActive(false);
                    continue;
                }
                p.root.gameObject.SetActive(true);
                float start = i * PieceLength, w = Mathf.Min(PieceLength, length - start);
                if (w <= 0.01f)
                {
                    p.root.gameObject.SetActive(false);
                    continue;
                }
                // Its own rise: to 110% in 0.18 s, back to 100% by 0.3 s; the wall's sinking or tumbling over it.
                float t = now - p.born;
                float k = t <= 0f ? 0f : t < 0.18f ? 1.1f * Mathf.Sin(t / 0.18f * Mathf.PI * 0.5f) : t < 0.3f ? Mathf.Lerp(1.1f, 1f, (t - 0.18f) / 0.12f) : 1f;
                if (crumbleAt >= 0f)
                {
                    // From the head (the far end) back: each piece goes down in 0.25 s, 0.04 s after the one before.
                    float order = (n - 1 - i) * 0.04f, c = Mathf.Clamp01((now - crumbleAt - order) / 0.25f);
                    k *= 1f - c * c;
                }
                else if (left <= 0f) k *= Mathf.Clamp01(risen);
                float h = height * k;
                bool shown = h > 0.02f;
                p.root.gameObject.SetActive(shown);
                if (!shown) continue;
                Vector3 jitter = shake > 0f ? new Vector3(Random.Range(-shake, shake), 0f, Random.Range(-shake, shake)) : Vector3.zero;
                p.root.localPosition = new Vector3(0f, 0f, -length * 0.5f + start + w * 0.5f) + jitter;
                p.body.localScale = new Vector3(thick, h, w);
                p.body.localPosition = new Vector3(0f, h * 0.5f, 0f);
                p.inkBody.localScale = new Vector3(thick + 0.07f, h + 0.06f, w + 0.05f);
                p.inkBody.localPosition = p.body.localPosition;
                float th = Mathf.Min(0.38f, h * 0.25f), tw = w * 0.5f;
                p.top.localScale = new Vector3(thick * 0.92f, th, tw);
                p.top.localPosition = new Vector3(0f, h + th * 0.5f, 0f);
                p.inkTop.localScale = new Vector3(thick * 0.92f + 0.06f, th + 0.05f, tw + 0.05f);
                p.inkTop.localPosition = p.top.localPosition;
                float bh = Mathf.Min(0.28f, h * 0.3f);
                p.band.localScale = new Vector3(thick + 0.04f, bh, w);
                p.band.localPosition = new Vector3(0f, bh * 0.5f, 0f);
                var bodyMat = ours ? brickClearMat : brickMat;
                if (p.bodyR.sharedMaterial != bodyMat)
                {
                    p.bodyR.sharedMaterial = bodyMat;
                    p.topR.sharedMaterial = bodyMat;
                }
                var bandMat = ours ? bandTealMat : bandRedMat;
                if (p.bandR.sharedMaterial != bandMat) p.bandR.sharedMaterial = bandMat;
                // The bricks a metre to a metre; the band's stripes flowing along.
                mpb.Clear();
                mpb.SetVector("_BaseMap_ST", new Vector4(w, h, 0f, 0f));
                mpb.SetVector("_MainTex_ST", new Vector4(w, h, 0f, 0f));
                if (shake > 0f) mpb.SetColor("_BaseColor", ours ? new Color(0.85f, 0.82f, 0.8f, 0.45f) : new Color(0.85f, 0.82f, 0.8f, 1f));
                p.bodyR.SetPropertyBlock(mpb);
                mpb.Clear();
                mpb.SetVector("_BaseMap_ST", new Vector4(w * 2f, 1f, -now * 0.8f, 0f));
                mpb.SetVector("_MainTex_ST", new Vector4(w * 2f, 1f, -now * 0.8f, 0f));
                p.bandR.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Lay it along a new line (the run grows it).</summary>
        public void SetLine(Vector3 a, Vector3 b)
        {
            from = a;
            to = b;
            Fit();
        }

        /// <summary>It stands <paramref name="seconds"/> from now (the run is over).</summary>
        public void Stand(float seconds) => left = Mathf.Max(left, seconds);

        /// <summary>It stands exactly <paramref name="seconds"/> more (a growing wall raised for long, its time told at last).</summary>
        public void StandFor(float seconds) => left = Mathf.Max(0.05f, seconds);

        /// <summary>Taken down at once: it sinks away.</summary>
        public void Break()
        {
            left = 0f;
            if (box != null) box.enabled = false;
        }

        void Fit()
        {
            Vector3 d = to - from;
            d.y = 0f;
            float length = Mathf.Max(0.3f, d.magnitude);
            Vector3 at = (from + to) * 0.5f;
            var turn = Quaternion.LookRotation(d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward, Vector3.up);
            if (body != null)
            {
                // (A moving wall: through its kinematic body, so the physics sees it sweep, not jump.)
                body.MovePosition(at);
                body.MoveRotation(turn);
            }
            else transform.SetPositionAndRotation(at, turn);
            // Risen this much: its top at risen * height above the floor (the rest still under it).
            float top = height * Mathf.Clamp01(risen);
            FitLook(length);
            if (box != null)
            {
                box.size = new Vector3(thick, height, length);
                box.center = new Vector3(0f, top - height * 0.5f, 0f);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (left > 0f)
            {
                left -= dt;
                risen = Mathf.Min(1f, risen + dt / rise);
                if (left <= 0f && box != null) box.enabled = false;
            }
            else
            {
                // Over: it tumbles down from its head (the effects add the dust and bricks), or a hidden one just goes.
                if (crumbleAt < 0f)
                {
                    crumbleAt = Time.time;
                    if (!hidden) OnCrumble?.Invoke(this, from, to);
                }
                risen -= dt / SinkTime;
                float crumbleTime = 0.3f + Mathf.Max(1, pieces.Count) * 0.04f;
                if (risen <= 0f && Time.time - crumbleAt > crumbleTime)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            Fit();
        }

        void OnDestroy() => All.Remove(this);

        /// <summary>How far a point is from the wall's line, along the floor (its ends count as the line's).</summary>
        public float FlatDistance(Vector3 p)
        {
            Vector3 a = new Vector3(from.x, 0f, from.z), b = new Vector3(to.x, 0f, to.z), q = new Vector3(p.x, 0f, p.z);
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            return (q - (a + ab * t)).magnitude;
        }

        /// <summary>Host: a rook charging meets an enemy rook's standing wall ahead of its chest: the wall breaks (every PC is
        /// told). Its own team's walls it does not break.</summary>
        public static void BreakAhead(RagdollPawn rook, Vector3 chest, Vector3 dir, float reach)
        {
            if (rook == null || rook.NetworkPuppet) return;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var w = All[i];
                if (w == null || !w.Solid || !w.Standing || w.Owner == null || w.Owner == rook || !Teams.AreEnemies(w.Owner.Team, rook.Team)) continue;
                Vector3 ahead = chest + dir * reach;
                if (Mathf.Abs(ahead.y - (w.from.y + w.height * 0.5f)) > w.height + 0.5f) continue;
                if (w.FlatDistance(ahead) > w.thick * 0.5f + 0.4f && w.FlatDistance(chest) > w.thick * 0.5f + 0.4f) continue;
                w.Break();
                rook.TellWallBroken(w);
            }
        }
    }
}
