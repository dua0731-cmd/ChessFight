using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The chocolate fountain's show on the dressed hall (대연회장 연출 R845; the user 11-26: "초콜릿 흐르는 연출 꼼꼼하게").
    // The tiers keep their colliders and their pull (ChocolateFlow: 0.3 m/s down the sides, still for 0.45 m under the
    // rim); everything here is a picture:
    //   - every tier wears a sheet of chocolate: out of its pool, over the gold rim in a rolled lip and down the whole of
    //     its side to the pool under it, where it spreads in a foot. The sheet streams (thicker and thinner runs round
    //     it, swaying), little waves run down it, and the builder's ripples (ChocolateRipples: rings welling out of the
    //     rim and running down at the flow's speed) are drawn as swells of the sheet itself - their cylinder pictures are
    //     hidden. Its texture runs down with it;
    //   - out of the crown's collar the chocolate pours down round the pedestal into the top pool the same way;
    //   - drops run down over the sheets, faster than it, into the pool under them;
    //   - rings spread out across every pool (the bowl's too) from where the sheet above meets it, and sink;
    //   - the chocolate is drawn glossy brown (the library's is a perfect mirror: the pools showed the sky, blue),
    //     the model's pools and streams too, its texture sliding.
    // Pure functions of the shared obstacle clock (HANDOFF rule 7): nothing is sent, nothing adds up.
    [DefaultExecutionOrder(205)]
    public sealed class BanquetFountainShow : MonoBehaviour
    {
        const int Around = 128, DropsPerTier = 7, RingsPerPool = 3;
        const float Out = .045f, DropRun = 1.9f, RingLife = 2.6f, Swell = .06f, SwellWidth = .16f, Sheen = .55f;
        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        sealed class Drop { public Transform t; public int tier; public float seed, every; }
        sealed class Ring { public Transform t; public float y, from, to, delay; }

        // A sheet of chocolate round the axis: its profile (r, y, how much it moves: 0 at the lip's top, 1 down the side),
        // drawn Around times round; moved every frame.
        sealed class Sheet
        {
            public Mesh mesh;
            public float[] r, y, free;
            public Vector3[] at;
            public float speed, streams, seed;
            public ChocolateRipples ripples;
            public float ripplesFoot;
        }

        readonly List<Sheet> sheets = new List<Sheet>();
        readonly List<Drop> drops = new List<Drop>();
        readonly List<Ring> rings = new List<Ring>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Material chocolate;
        Vector4 baseSt = new Vector4(1f, 1f, 0f, 0f);

        public int Sheets => sheets.Count;
        public int Drops => drops.Count;
        public int Rings => rings.Count;
        public int Surfaces { get; private set; }
        public int RipplesHidden { get; private set; }
        // As last drawn: how far the chocolate's surface has slid (m) and how many drops are on their way.
        public float Slid { get; private set; }
        public int Falling { get; private set; }

        public void Configure(GrandBanquetHallDressing dressing)
        {
            var frame = dressing.transform;
            var f = GrandBanquetHallBuilder.Fountain;
            var library = Resources.Load<Material>("BanquetHall/Materials/BH_Chocolate");
            if (library == null) return;
            // warm glossy chocolate with runs painted down it (FlowTexture), its library normal map kept for the gloss
            chocolate = new Material(library) { name = "BH_Chocolate (flowing)", mainTexture = FlowTexture() };
            // (11-26 "좀 더 초콜릿 스럽게") melted chocolate is smooth and very glossy: no bumpy normal map, sharp
            // highlights from the hall's lights, no sky in it
            if (chocolate.HasProperty("_Smoothness")) chocolate.SetFloat("_Smoothness", .9f);
            if (chocolate.HasProperty("_BumpMap")) chocolate.SetTexture("_BumpMap", null);
            chocolate.DisableKeyword("_NORMALMAP");
            if (chocolate.HasProperty("_Metallic")) chocolate.SetFloat("_Metallic", 0f);
            if (chocolate.HasProperty("_EnvironmentReflections")) { chocolate.SetFloat("_EnvironmentReflections", 0f); chocolate.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
            chocolate.color = Color.white;
            baseSt = new Vector4(1f / 1.4f, 1f / 1.6f, 0f, 0f);
            chocolate.SetVector(BaseMapSt, baseSt);
            owned.Add(chocolate);

            var holder = new GameObject("BH fountain show").transform;
            holder.SetParent(frame, false);
            holder.localPosition = new Vector3(f.x, 0f, f.y);
            float[] tierR = GrandBanquetHallBuilder.TierR, tierTop = GrandBanquetHallBuilder.TierTop;
            float top = tierTop[tierTop.Length - 1], collar = top + 1.2f - .14f;

            // the builder's ripples: hidden, drawn as the sheets' swells
            var ripples = new Dictionary<int, ChocolateRipples>();
            foreach (var cr in dressing.GetComponentsInChildren<ChocolateRipples>(true))
            {
                var name = cr.name;
                if (int.TryParse(name.Substring(name.LastIndexOf(' ') + 1), out int k)) ripples[k - 1] = cr;
                for (int i = 0; i < cr.Count; i++)
                    if (cr.Ring(i) != null && cr.Ring(i).TryGetComponent<Renderer>(out var r) && r.enabled) { r.enabled = false; RipplesHidden++; }
            }

            // a sheet on every tier, then round the crown's pedestal
            for (int k = 0; k < tierR.Length; k++)
            {
                float R = tierR[k], T = tierTop[k], foot = k == 0 ? GrandBanquetHallBuilder.BowlTop : tierTop[k - 1];
                var p = new List<Vector3>   // (r, y, free)
                {
                    new Vector3(R - .03f, T - .012f, 0f), new Vector3(R + .012f, T - .018f, .1f), new Vector3(R + .05f, T - .05f, .3f), new Vector3(R + .062f, T - .09f, .45f),
                    new Vector3(R + Out, T - .13f, .6f),
                };
                for (float yy = T - .22f; yy > foot + .14f; yy -= .1f) p.Add(new Vector3(R + Out, yy, 1f));
                p.Add(new Vector3(R + Out + .02f, foot + .07f, 1f));
                p.Add(new Vector3(R + Out + .08f, foot + .025f, .8f));
                p.Add(new Vector3(R + Out + .16f, foot + .004f, .5f));
                p.Add(new Vector3(R + Out + .24f, foot - .008f, .2f));
                ripples.TryGetValue(k, out var rr);
                AddSheet(holder, p, .6f, 11f + 4f * k, k * 1.7f, rr, foot);
            }
            {
                // the crown: out from under the collar, down the fluted pedestal, spreading into the top pool
                var p = new List<Vector3>
                {
                    new Vector3(.3f, collar + .03f, 0f), new Vector3(.37f, collar - .01f, .2f), new Vector3(.33f, collar - .09f, .6f),
                };
                for (float yy = collar - .2f; yy > top + .32f; yy -= .09f) p.Add(new Vector3(.285f, yy, 1f));
                p.Add(new Vector3(.32f, top + .2f, 1f));
                p.Add(new Vector3(.4f, top + .09f, .9f));
                p.Add(new Vector3(.5f, top + .025f, .6f));
                p.Add(new Vector3(.6f, top - .006f, .2f));
                AddSheet(holder, p, .85f, 6f, 5.3f, null, top);
            }

            // drops over every tier's sheet
            var dot = BanquetShowKit.DotMesh();
            for (int k = 0; k < tierR.Length; k++)
                for (int i = 0; i < DropsPerTier; i++)
                {
                    var d = new Drop
                    {
                        t = BanquetShowKit.Drawn("BH fountain drop", holder, dot, chocolate), tier = k,
                        seed = Mathf.Repeat(i * .618f + k * .29f, 1f), every = 2.2f + .37f * ((i * 5 + k * 3) % 7),
                    };
                    d.t.gameObject.SetActive(false);
                    drops.Add(d);
                }
            // rings across every pool: the bowl's (under tier 1) and each tier's top (under the next tier, the crown's)
            var torus = Torus();
            for (int k = 0; k <= tierR.Length; k++)
            {
                float y = k == 0 ? GrandBanquetHallBuilder.BowlTop - .01f : tierTop[k - 1] - .01f;
                float from = k < tierR.Length ? tierR[k] + Out + .2f : .6f, to = k == 0 ? 3.9f : tierR[k - 1] - .25f;
                for (int i = 0; i < RingsPerPool; i++)
                    rings.Add(new Ring
                    {
                        t = BanquetShowKit.Drawn("BH fountain ring", holder, torus, chocolate), y = y, from = from, to = to,
                        delay = RingLife * i / RingsPerPool + .41f * k,
                    });
            }
            // the model's chocolate (its pools, its streams under the sheets): the same flowing chocolate
            var model = GrandBanquetHallDressing.ModelOn(frame, "bh_fountain");
            if (model != null)
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].name.StartsWith("BH_Chocolate", System.StringComparison.Ordinal)) { mats[i] = chocolate; changed = true; Surfaces++; }
                    if (changed) r.sharedMaterials = mats;
                }
            Debug.Log("[대연회장 분수 연출] 초콜릿 막 " + sheets.Count + " · 방울 " + drops.Count + " · 물결 " + rings.Count + " · 흐르는 초콜릿 면 " + Surfaces +
                      " · 숨긴 빌더 물결 " + RipplesHidden);
        }

        void AddSheet(Transform holder, List<Vector3> profile, float speed, float streams, float seed, ChocolateRipples ripples, float foot)
        {
            int rows = profile.Count, cols = Around + 1;
            var s = new Sheet
            {
                r = new float[rows], y = new float[rows], free = new float[rows], at = new Vector3[rows * cols],
                speed = speed, streams = streams, seed = seed, ripples = ripples, ripplesFoot = foot,
            };
            var uv = new Vector2[rows * cols];
            float along = 0f;
            for (int j = 0; j < rows; j++)
            {
                s.r[j] = profile[j].x; s.y[j] = profile[j].y; s.free[j] = profile[j].z;
                if (j > 0) along += new Vector2(profile[j].x - profile[j - 1].x, profile[j].y - profile[j - 1].y).magnitude;
                for (int i = 0; i < cols; i++)
                    uv[j * cols + i] = new Vector2(2f * Mathf.PI * profile[j].x * i / Around, -along);
            }
            var tris = new List<int>(rows * Around * 6);
            for (int j = 0; j + 1 < rows; j++)
                for (int i = 0; i < Around; i++)
                {
                    int a = j * cols + i, b = a + 1, c = a + cols, d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            s.mesh = new Mesh { name = "BH chocolate sheet" };
            s.mesh.MarkDynamic();
            Pose(s, 0f);
            s.mesh.SetUVs(0, uv);
            s.mesh.SetTriangles(tris, 0);
            s.mesh.RecalculateNormals();
            s.mesh.RecalculateTangents();
            s.mesh.RecalculateBounds();
            var b2 = s.mesh.bounds;
            b2.Expand(.3f);
            s.mesh.bounds = b2;
            owned.Add(s.mesh);
            var t = BanquetShowKit.Drawn("BH chocolate sheet", holder, s.mesh, chocolate);
            if (t.TryGetComponent<Renderer>(out var rend)) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            sheets.Add(s);
        }

        // The sheet at clock time t: its runs round it (some thicker, swaying a little), little waves running down, the
        // ripples' swells where ChocolateRipples has them now.
        void Pose(Sheet s, float t)
        {
            int rows = s.r.Length, cols = Around + 1;
            int ringCount = s.ripples != null ? s.ripples.Count : 0;
            var swells = new float[ringCount];
            for (int i = 0; i < ringCount; i++)
                swells[i] = s.ripplesFoot + s.ripples.Height - ChocolateRipples.Half - .01f - s.ripples.Below(i, t);
            for (int i = 0; i < cols; i++)
            {
                float a = 2f * Mathf.PI * (i % Around) / Around, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float runs = .5f + .5f * Mathf.Sin(s.streams * a + 1.3f * Mathf.Sin(3f * a + s.seed) + .25f * Mathf.Sin(.7f * t + s.seed));
                for (int j = 0; j < rows; j++)
                {
                    float y = s.y[j], free = s.free[j];
                    float wave = Mathf.Sin(7f * (y + s.speed * t) + 2f * Mathf.Sin(5f * a + s.seed) + 3f * a);
                    float dr = free * (.024f * runs * runs + .007f * wave);
                    for (int k = 0; k < ringCount; k++)
                    {
                        float d = (y - swells[k]) / SwellWidth;
                        if (d > -3f && d < 3f) dr += free * Swell * Mathf.Exp(-d * d) * (.75f + .25f * runs);
                    }
                    s.at[j * cols + i] = new Vector3(ca * (s.r[j] + dr), y + free * .004f * wave, sa * (s.r[j] + dr));
                }
            }
            s.mesh.SetVertices(s.at);
        }

        void OnDestroy()
        {
            foreach (var o in owned)
                if (o != null) Destroy(o);
        }

        void LateUpdate()
        {
            if (chocolate == null) return;
            double now = ObstacleClock.Now;
            float t = (float)(now % 3600.0);
            float[] tierR = GrandBanquetHallBuilder.TierR, tierTop = GrandBanquetHallBuilder.TierTop;

            foreach (var s in sheets)
            {
                Pose(s, t);
                s.mesh.RecalculateNormals();
            }

            // the drops: each over the lip now and then, running down the sheet (gravity, held back by it), into the pool
            int falling = 0;
            foreach (var d in drops)
            {
                float r = tierR[d.tier] + Out + .03f, rim = tierTop[d.tier] - .05f;
                float foot = d.tier == 0 ? GrandBanquetHallBuilder.BowlTop : tierTop[d.tier - 1];
                float run = rim - foot;
                float age = Mathf.Repeat(t + d.seed * d.every, d.every);
                float slid = Mathf.Min(run, .3f * age + 1.6f * age * age);
                bool on = age < DropRun && slid < run;
                if (d.t.gameObject.activeSelf != on) d.t.gameObject.SetActive(on);
                if (!on) continue;
                falling++;
                float lap = Mathf.Floor((t + d.seed * d.every) / d.every);
                float a = 2f * Mathf.PI * Mathf.Repeat(d.seed + lap * .237f, 1f);
                float grow = Mathf.Clamp01(age / .25f), shrink = Mathf.Clamp01((run - slid) / .25f);
                float size = .12f * grow * shrink * (1f + .2f * d.seed);
                d.t.localPosition = new Vector3(Mathf.Cos(a) * r, rim - slid, Mathf.Sin(a) * r);
                d.t.localScale = new Vector3(size, size * (1.3f + Mathf.Min(1.4f, slid * .5f)), size * .7f);
                d.t.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
            Falling = falling;

            // the rings: out from where the sheet meets the pool, flattening and sinking as they go
            foreach (var ring in rings)
            {
                float k = Mathf.Repeat(t + ring.delay, RingLife) / RingLife;
                float radius = Mathf.Lerp(ring.from, ring.to, Mathf.Sqrt(k));
                float high = .5f * (1f - k) * Mathf.Clamp01(k / .08f);
                ring.t.localPosition = new Vector3(0f, ring.y, 0f);
                ring.t.localScale = new Vector3(radius, Mathf.Max(.02f, high), radius);
            }

            // the texture runs down the sheets (their v is metres along them, downward negative) and the model's chocolate
            float slide = (float)(now * Sheen % 1000.0);
            Slid = slide;
            chocolate.SetVector(BaseMapSt, new Vector4(baseSt.x, baseSt.y, baseSt.z, Mathf.Repeat(baseSt.w + slide * baseSt.y, 1f)));
        }

        // Chocolate running down (v, metres along the sheet, the texture's y), painted 128 x 256 for a 1.4 x 1.6 m tile:
        // runs of darker and lighter brown side by side, wavering, a bright glossy line down the crown of some, little
        // ripples across them.
        Texture2D FlowTexture()
        {
            const int w = 128, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "BH chocolate flowing", wrapMode = TextureWrapMode.Repeat };
            Color deep = new Color(.25f, .12f, .06f), mid = new Color(.38f, .19f, .095f), light = new Color(.48f, .26f, .14f), gloss = new Color(.6f, .36f, .21f);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    // the runs: a sum of waves across, each wavering a little down its length (all whole periods: it tiles)
                    float wob = .012f * Mathf.Sin(2f * Mathf.PI * (v + 2f * u)) + .006f * Mathf.Sin(2f * Mathf.PI * (2f * v - u));
                    float uu = u + wob;
                    float run = .55f + .25f * Mathf.Sin(2f * Mathf.PI * 3f * uu) + .12f * Mathf.Sin(2f * Mathf.PI * 5f * uu + 1.3f);
                    float ripple = .5f + .5f * Mathf.Sin(2f * Mathf.PI * (5f * v + .5f * Mathf.Sin(2f * Mathf.PI * uu)));
                    var c = Color.Lerp(deep, mid, Mathf.Clamp01(run));
                    c = Color.Lerp(c, light, .25f * Mathf.Clamp01(run) * Mathf.Clamp01(run) + .04f * ripple);
                    float crown = Mathf.Clamp01((run - .72f) / .3f);
                    c = Color.Lerp(c, gloss, .22f * crown * crown);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true, false);
            owned.Add(tex);
            return tex;
        }

        // A ring of chocolate standing out of the pool: radius 1, its section a low round bump (0.08 across, 1 high).
        Mesh Torus()
        {
            const int around = 64, section = 8;
            var v = new List<Vector3>();
            var tris = new List<int>();
            for (int j = 0; j <= around; j++)
            {
                float a = 2f * Mathf.PI * j / around;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int i = 0; i <= section; i++)
                {
                    float b = Mathf.PI * i / section;
                    v.Add(d * (1f - .04f * Mathf.Cos(b)) + Vector3.up * (.1f * Mathf.Sin(b) - .02f));
                }
            }
            for (int j = 0; j < around; j++)
                for (int i = 0; i < section; i++)
                {
                    int p = j * (section + 1) + i, q = p + section + 1;
                    tris.Add(p); tris.Add(q); tris.Add(p + 1);
                    tris.Add(p + 1); tris.Add(q); tris.Add(q + 1);
                }
            var m = new Mesh { name = "BH fountain ring" };
            m.SetVertices(v);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            owned.Add(m);
            return m;
        }
    }
}
