using UnityEngine;

namespace ChessFight.PawnRush
{
    // R503: the parts a Tripo model cannot be (user rule, 2026-11-07): real light and flames, and cloth that sways.
    // The models carry only empties where these go (SOCKET_Flame / SOCKET_Light / SOCKET_Lantern / SOCKET_Lamp,
    // ArtSource/PawnRush/MiniGames/Mid/b_hall_v1.py, b_core_scene_v1.py). Pictures only: no colliders, no game state.
    public static class MiniGameFixtures
    {
        const string Materials = "PawnRushMiniGames/Materials/";
        static Material flame, canvas, batten;

        static Material Mat(ref Material cache, string name)
        {
            if (cache == null) cache = Resources.Load<Material>(Materials + name);
            return cache;
        }

        // Every SOCKET_* empty under root gets its flame and/or light, once.
        public static void LightSockets(Transform root)
        {
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("SOCKET_") || t.childCount > 0) continue;
                if (t.name.StartsWith("SOCKET_Flame")) Flame(t, .045f, .1f);                        // a chandelier candle cup
                else if (t.name.StartsWith("SOCKET_Light")) Light(t, 11f, 2.2f);                    // the chandelier's one light
                else if (t.name.StartsWith("SOCKET_Lantern")) { Flame(t, .06f, .13f); Light(t, 6f, 1.4f); }   // wall sconce
                else if (t.name.StartsWith("SOCKET_Lamp")) { Flame(t, .07f, .15f); Light(t, 8f, 1.8f); }      // basement work lamp
            }
        }

        static void Flame(Transform at, float radius, float height)
        {
            var f = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            f.name = "Flame";
            Object.Destroy(f.GetComponent<Collider>());
            f.transform.SetParent(at, false);
            f.transform.localPosition = Vector3.zero;
            f.transform.localScale = Divide(new Vector3(radius * 2f, height * .5f, radius * 2f), at.lossyScale);
            var r = f.GetComponent<Renderer>();
            r.sharedMaterial = Mat(ref flame, "PR_Mid_Flame");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            f.AddComponent<FlameFlicker>();
        }

        static void Light(Transform at, float range, float intensity)
        {
            var go = new GameObject("Fixture light");
            go.transform.SetParent(at, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, .74f, .45f);
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            go.AddComponent<FlameFlicker>();
        }

        static Vector3 Divide(Vector3 a, Vector3 b) =>
            new Vector3(a.x / Mathf.Max(Mathf.Abs(b.x), 1e-4f), a.y / Mathf.Max(Mathf.Abs(b.y), 1e-4f), a.z / Mathf.Max(Mathf.Abs(b.z), 1e-4f));

        // A canvas dust sheet over a team gate box (any scale): it rides the gate as the gate rises.
        // width/height in metres; the sheet hangs from a top batten at the gate's front face.
        public static void GateCloth(Transform gate, float width, float height)
        {
            if (gate == null || gate.Find("MODEL_Cloth") != null) return;
            var holder = new GameObject("MODEL_Cloth").transform;
            holder.SetParent(gate, false);
            holder.localPosition = new Vector3(0f, -.5f, -.5f);              // bottom of the box's front face
            holder.localScale = Divide(Vector3.one, gate.localScale);        // metres inside the scaled box
            var sheet = new GameObject("Dust sheet");
            sheet.transform.SetParent(holder, false);
            sheet.transform.localPosition = new Vector3(0f, .1f, -.05f);
            sheet.AddComponent<MeshFilter>();
            var mr = sheet.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat(ref canvas, "PR_Mid_Canvas");
            sheet.AddComponent<SwayingCloth>().Configure(width - .1f, height - .3f);
            foreach (float y in new[] { .12f, height - .12f })
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Sheet batten";
                Object.Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(holder, false);
                b.transform.localPosition = new Vector3(0f, y, -.08f);
                b.transform.localScale = new Vector3(width + .1f, .16f, .14f);
                b.GetComponent<Renderer>().sharedMaterial = Mat(ref batten, "PR_Mid_JoistWood");
            }
        }

        static Material brass;

        // A team banner hanging from a brass rod whose top centre is `top` (parent frame) (user, 2026-11-07: "깃발을 더
        // 멋있게 해"): a knight's banner with a pointed hem, painted in code - the team colour field, a matte-brass
        // border along the sides, the top and both hem edges, and a cream pawn emblem with a brass outline - on the
        // canvas weave normal map; a brass rod with ball finials and a brass tassel that rides the swaying tip.
        public static void Pennant(Transform parent, string name, Vector3 top, float width, float height, Color team)
        {
            const float tip = .55f;                                      // hem point depth below the side corners
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = top;
            var cloth = new GameObject("Team banner");
            cloth.transform.SetParent(root, false);
            cloth.transform.localPosition = new Vector3(0f, -height - .06f, 0f);
            cloth.AddComponent<MeshFilter>();
            var mr = cloth.AddComponent<MeshRenderer>();
            var mat = new Material(Mat(ref canvas, "PR_Mid_Canvas")) { name = "Team banner " + name };
            mat.SetTexture("_BaseMap", BannerTexture(team, width, height, tip));
            mat.SetColor("_BaseColor", Color.white);
            mr.sharedMaterial = mat;
            var sway = cloth.AddComponent<SwayingCloth>();
            sway.Configure(width, height, tip, true);

            var brassMat = Mat(ref brass, "PR_Mid_MatteBrass");
            var rod = Part(PrimitiveType.Cylinder, root, "Banner rod", Vector3.zero, new Vector3(.08f, (width + .5f) * .5f, .08f), brassMat);
            rod.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float x in new[] { -(width * .5f + .3f), width * .5f + .3f })
                Part(PrimitiveType.Sphere, root, "Rod finial", new Vector3(x, 0f, 0f), Vector3.one * .2f, brassMat);
            // the tassel hangs from the hem point and follows it as the cloth sways
            var tassel = new GameObject("Banner tassel").transform;
            tassel.SetParent(cloth.transform, false);
            Part(PrimitiveType.Sphere, tassel, "Tassel knot", new Vector3(0f, -.08f, 0f), Vector3.one * .13f, brassMat);
            Part(PrimitiveType.Cylinder, tassel, "Tassel fringe", new Vector3(0f, -.3f, 0f), new Vector3(.12f, .16f, .12f), brassMat);
            sway.Follow(tassel);
        }

        static Transform Part(PrimitiveType type, Transform parent, string name, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        // The banner's colour map, UV 0..1 over the cloth (u across, v up; the hem is a V whose point is at v 0).
        static Texture2D BannerTexture(Color team, float w, float h, float tip)
        {
            const int W = 256, H = 320;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "Team banner", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            Color brassC = new Color(.69f, .54f, .27f), brassDark = new Color(.47f, .35f, .16f), cream = new Color(.93f, .89f, .8f);
            float border = .16f, line = .035f;                           // metres
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    float x = (i + .5f) / W * w - w * .5f, y = (j + .5f) / H * h;
                    // distance (m) to the nearest edge: sides, top, and the two hem lines (y = tip * |2x/w|)
                    float hem = (y - tip * Mathf.Abs(2f * x / w)) / Mathf.Sqrt(1f + (2f * tip / w) * (2f * tip / w));
                    float edge = Mathf.Min(Mathf.Min(w * .5f - Mathf.Abs(x), h - y), hem);
                    // the field: team colour, a little darker toward the hem and the sides (cloth in shade)
                    float shade = .82f + .18f * Mathf.Clamp01(y / h) * (1f - .3f * Mathf.Abs(2f * x / w));
                    Color c = team * shade; c.a = 1f;
                    if (edge < border) c = edge < border - line ? brassC : brassDark;                 // brass band + inner line
                    // pawn emblem (cream, brass outline), centred at 55 % height
                    float e = PawnDistance(x / w, (y - h * .56f) / w);
                    if (e < 0f) c = cream;
                    else if (e < .012f) c = brassDark;
                    px[j * W + i] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        // Signed distance (banner-width units) to a pawn silhouette: head, collar, body, base. Negative inside.
        static float PawnDistance(float x, float y)
        {
            float head = new Vector2(x, y - .15f).magnitude - .085f;
            float collar = Box(x, y - .045f, .085f, .018f);
            float t = Mathf.Clamp01((y + .17f) / .2f);                   // body widens downward
            float body = Box(x, y + .07f, Mathf.Lerp(.13f, .055f, t), .1f);
            float foot = Box(x, y + .19f, .16f, .03f);
            return Mathf.Min(Mathf.Min(head, collar), Mathf.Min(body, foot));
        }

        static float Box(float x, float y, float hx, float hy)
        {
            float dx = Mathf.Abs(x) - hx, dy = Mathf.Abs(y) - hy;
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f);
        }
    }

    // A small steady flicker: a light's intensity and a flame's height breathe on Perlin noise.
    public sealed class FlameFlicker : MonoBehaviour
    {
        Light lightSource;
        float baseIntensity, seed;
        Vector3 baseScale;

        void Start()
        {
            lightSource = GetComponent<Light>();
            if (lightSource != null) baseIntensity = lightSource.intensity;
            baseScale = transform.localScale;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * 2.3f);
            if (lightSource != null) lightSource.intensity = baseIntensity * (.82f + .3f * n);
            else transform.localScale = new Vector3(baseScale.x, baseScale.y * (.85f + .3f * n), baseScale.z);
        }
    }

    // A hanging sheet: a grid pinned along its top edge whose lower part sways slowly (vertex motion, no physics).
    [RequireComponent(typeof(MeshFilter))]
    public sealed class SwayingCloth : MonoBehaviour
    {
        const int Columns = 10, Rows = 12;
        Mesh mesh;
        Vector3[] rest, moved;
        float width, height, seed;
        Transform follower;
        int tipIndex;

        // tip > 0: the hem is a V whose point (centre) hangs `tip` metres below the side corners. normalizedUv: UV
        // 0..1 over the whole cloth (a painted banner) instead of metres (the canvas tile).
        // R508 (F ballroom's window drapes; both 0 elsewhere): pleat = depth (m) of vertical folds every 0.22 m;
        // gather = how far (0..1) the cloth is drawn in at a tie-back 35 % up, flaring out again below it.
        public void Configure(float w, float h, float tip = 0f, bool normalizedUv = false, float pleat = 0f, float gather = 0f)
        {
            width = w; height = h; seed = Random.value * 10f;
            mesh = new Mesh { name = "Swaying cloth" };
            rest = new Vector3[(Columns + 1) * (Rows + 1)];
            var uv = new Vector2[rest.Length];
            var tris = new int[Columns * Rows * 6];
            for (int r = 0, i = 0; r <= Rows; r++)
                for (int c = 0; c <= Columns; c++, i++)
                {
                    float x = (c / (float)Columns - .5f) * w;
                    float hem = tip * Mathf.Abs(2f * x / w);
                    float y = Mathf.Lerp(hem, h, r / (float)Rows);
                    float u = x;
                    if (gather > 0f)
                    {
                        float k = (y / h - .35f) / .14f;
                        x *= 1f - gather * Mathf.Exp(-k * k);
                    }
                    float fold = pleat > 0f ? pleat * Mathf.Sin(u / .22f * 2f * Mathf.PI) * (1f - .5f * y / h) : 0f;
                    rest[i] = new Vector3(x, y, fold);
                    uv[i] = normalizedUv ? new Vector2(c / (float)Columns, y / h) : new Vector2(u, y);
                }
            tipIndex = Columns / 2;                       // bottom row, centre column: the hem point
            for (int r = 0, k = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                {
                    int a = r * (Columns + 1) + c, b = a + Columns + 1;
                    tris[k++] = a; tris[k++] = b; tris[k++] = a + 1;
                    tris[k++] = a + 1; tris[k++] = b; tris[k++] = b + 1;
                }
            moved = (Vector3[])rest.Clone();
            mesh.vertices = rest; mesh.uv = uv; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void Update()
        {
            if (mesh == null) return;
            float t = Time.time + seed;
            for (int i = 0; i < rest.Length; i++)
            {
                var p = rest[i];
                float free = 1f - p.y / height;                       // pinned at the top, freest at the hem
                float wave = Mathf.Sin(t * 1.3f + p.x * .9f) * .6f + Mathf.Sin(t * 2.1f + p.y * 1.7f + p.x * .4f) * .4f;
                moved[i] = new Vector3(p.x + Mathf.Sin(t * .8f + p.y) * .03f * free, p.y, p.z - .12f * free * free * wave);
            }
            mesh.vertices = moved;
            mesh.RecalculateNormals();
            if (follower != null) follower.localPosition = moved[tipIndex];
        }

        // A child that rides the hem point (a tassel).
        public void Follow(Transform t)
        {
            follower = t;
            if (moved != null) follower.localPosition = moved[tipIndex];
        }
    }
}
