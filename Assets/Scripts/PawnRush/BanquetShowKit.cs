using System;
using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // What the grand banquet hall's shows (대연회장 연출, seoungtest R845) share: a model's parts by name with their LODs,
    // a mesh copied so its vertices can be moved every frame, a candle flame (the R843 chandelier flame, BanquetChandelier-
    // Lights: a teardrop with its colours in its vertices), soft glows and lights, and the swing of something that hangs
    // and is carried (a curtain's hem, a tassel, a crystal) worked out from the clock alone.
    public static class BanquetShowKit
    {
        // ------------------------------------------------------------------ parts

        // "BH curtain pleat 000_LOD1" -> "BH curtain pleat 000".
        public static string PartName(string name)
        {
            int lod = name.LastIndexOf("_LOD", StringComparison.Ordinal);
            return lod >= 0 ? name.Substring(0, lod) : name;
        }

        // The model's meshes by part (its LODs together), in the model's order.
        public static List<KeyValuePair<string, List<MeshFilter>>> Parts(Transform model)
        {
            var order = new List<KeyValuePair<string, List<MeshFilter>>>();
            if (model == null) return order;
            var byName = new Dictionary<string, List<MeshFilter>>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = PartName(filter.name);
                if (!byName.TryGetValue(n, out var list))
                {
                    byName[n] = list = new List<MeshFilter>();
                    order.Add(new KeyValuePair<string, List<MeshFilter>>(n, list));
                }
                list.Add(filter);
            }
            return order;
        }

        // ------------------------------------------------------------------ a mesh moved by bones

        // Which bones move a vertex (`p`: the vertex in the bones' frame): two of them, `a` weighted `wa` and `b` the rest.
        public delegate void Weigh(Vector3 p, out int a, out int b, out float wa);

        // A model's mesh drawn by a skinned renderer whose `bones` (children of `space`, standing where they are now as
        // the rest pose) move it: the graphics card moves the vertices, the show only sets a few transforms a frame
        // (moving every vertex on the CPU was over 100 ms a frame for the curtain). The model's own renderer is switched
        // off and its LOD group draws the skinned one in its place. `grow`: metres the bounds are widened by (the moves).
        public static SkinnedMeshRenderer Skin(MeshFilter filter, Transform space, Transform[] bones, Weigh weigh, float grow, List<UnityEngine.Object> owned)
        {
            var source = filter != null ? filter.sharedMesh : null;
            if (source == null || !source.isReadable || !filter.TryGetComponent<MeshRenderer>(out var old)) return null;
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = source.name + " (연출 뼈대)";
            owned.Add(mesh);
            var toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var v = mesh.vertices;
            var weights = new BoneWeight[v.Length];
            Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
            for (int i = 0; i < v.Length; i++)
            {
                var p = toSpace.MultiplyPoint3x4(v[i]);
                lo = Vector3.Min(lo, p);
                hi = Vector3.Max(hi, p);
                weigh(p, out int a, out int b, out float wa);
                wa = Mathf.Clamp01(wa);
                weights[i] = a == b || wa >= .999f ? new BoneWeight { boneIndex0 = a, weight0 = 1f }
                                                   : new BoneWeight { boneIndex0 = a, weight0 = wa, boneIndex1 = b, weight1 = 1f - wa };
            }
            mesh.boneWeights = weights;
            var bind = new Matrix4x4[bones.Length];
            for (int j = 0; j < bones.Length; j++) bind[j] = bones[j].worldToLocalMatrix * filter.transform.localToWorldMatrix;
            mesh.bindposes = bind;

            // The same name beside it ("<part>_LOD<k>"): the hall's per-part LOD groups (GrandBanquetHallDressing.SplitLods)
            // are made from the names, so, whether they are made before or after this, the skinned one is in its part's.
            var go = new GameObject(filter.name);
            go.layer = filter.gameObject.layer;
            go.transform.SetParent(filter.transform.parent, false);
            go.transform.localPosition = filter.transform.localPosition;
            go.transform.localRotation = filter.transform.localRotation;
            go.transform.localScale = filter.transform.localScale;
            var skin = go.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterials = old.sharedMaterials;
            skin.bones = bones;
            skin.rootBone = space;
            skin.shadowCastingMode = old.shadowCastingMode;
            skin.receiveShadows = old.receiveShadows;
            skin.updateWhenOffscreen = false;
            var bounds = new Bounds((lo + hi) * .5f, hi - lo);
            bounds.Expand(2f * grow);
            skin.localBounds = bounds;
            // every LOD group of the hung model that draws the old renderer (the importer's on the model, or SplitLods'
            // beside the part) draws the skinned one instead
            var model = filter.transform;
            while (model.parent != null && !model.name.EndsWith(" (model)", StringComparison.Ordinal)) model = model.parent;
            foreach (var group in model.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                for (int k = 0; k < lods.Length; k++)
                {
                    var list = lods[k].renderers;
                    if (list == null) continue;
                    for (int r = 0; r < list.Length; r++) if (list[r] == old) list[r] = skin;
                    lods[k].renderers = list;
                }
                group.SetLODs(lods);
            }
            old.enabled = false;
            return skin;
        }

        // A bone: a child of `space` standing at `at` (its frame), unturned.
        public static Transform Bone(string name, Transform space, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(space, false);
            t.localPosition = at;
            return t;
        }

        // ------------------------------------------------------------------ flames, glows, lights

        public static readonly Color CandleLight = new Color(1f, .72f, .4f);
        static Mesh flameMesh, quadMesh, dotMesh;

        // The candles' flame material (R843's: URP Particles/Unlit, alpha-blended, white - the colours are the mesh's).
        public static Material FlameMaterial(List<UnityEngine.Object> owned)
        {
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null) return null;
            var fire = new Material(shader) { name = "BH show candle flame", mainTexture = Texture2D.whiteTexture, color = new Color(1.9f, 1.75f, 1.5f, 1f) };
            LitMaterials.MakeTransparent(fire);
            owned.Add(fire);
            return fire;
        }

        // Light added over what is behind (a glow, a beam, a sparkle): its colour times the mesh's vertex colours.
        public static Material GlowMaterial(Color tint, List<UnityEngine.Object> owned)
        {
            var m = LitMaterials.NewAdditiveParticles(Texture2D.whiteTexture, tint);
            if (m == null) return null;
            m.name = "BH show glow";
            owned.Add(m);
            return m;
        }

        // Alpha-blended, its colour times the mesh's vertex colours (steam, smoke, drops of chocolate).
        public static Material SoftMaterial(Color tint, List<UnityEngine.Object> owned)
        {
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null) return null;
            var m = new Material(shader) { name = "BH show soft", mainTexture = Texture2D.whiteTexture, color = tint };
            LitMaterials.MakeTransparent(m);
            owned.Add(m);
            return m;
        }

        public static Transform Drawn(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        public static Transform Flame(Transform parent, Material material) => Drawn("BH show flame", parent, FlameMesh(), material);

        public static Light Point(string name, Transform parent, Color color, float range, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        // A teardrop 0.2 m high and 0.09 m across standing on its foot (BanquetChandelierLights.FlameMesh, R843): a dim blue
        // foot, a yellow-white body, an orange tip that thins out.
        public static Mesh FlameMesh()
        {
            if (flameMesh != null) return flameMesh;
            const int rings = 12, sides = 12;
            const float height = .2f, wide = .045f;
            Color foot = new Color(.3f, .45f, 1f, .35f), body = new Color(1f, .93f, .66f, 1f), high = new Color(1f, .74f, .3f, .95f), tip = new Color(1f, .5f, .12f, 0f);
            var v = new List<Vector3>();
            var c = new List<Color>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= rings; j++)
            {
                float u = (float)j / rings, r = wide * Mathf.Sin(Mathf.PI * Mathf.Pow(u, .55f));
                Color col = u < .16f ? Color.Lerp(foot, body, u / .16f) : u < .55f ? Color.Lerp(body, high, (u - .16f) / .39f) : Color.Lerp(high, tip, (u - .55f) / .45f);
                for (int i = 0; i < sides; i++)
                {
                    float a = 2f * Mathf.PI * i / sides;
                    v.Add(new Vector3(Mathf.Cos(a) * r, u * height, Mathf.Sin(a) * r));
                    c.Add(col);
                    uv.Add(new Vector2(.5f, .5f));
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < sides; i++)
                {
                    int p = j * sides + i, q = j * sides + (i + 1) % sides;
                    tris.Add(p); tris.Add(p + sides); tris.Add(q);
                    tris.Add(q); tris.Add(p + sides); tris.Add(q + sides);
                }
            flameMesh = new Mesh { name = "BH show flame" };
            flameMesh.SetVertices(v);
            flameMesh.SetColors(c);
            flameMesh.SetUVs(0, uv);
            flameMesh.SetTriangles(tris, 0);
            flameMesh.RecalculateNormals();
            flameMesh.RecalculateBounds();
            return flameMesh;
        }

        // A 1 x 1 sheet in x-y, facing -z (both sides drawn by its two faces), its colour bright in the middle line and
        // nothing at its edges (left and right), fading at the top and the foot: a blade of light.
        public static Mesh BladeMesh()
        {
            if (quadMesh != null) return quadMesh;
            const int across = 6, up = 8;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= up; j++)
                for (int i = 0; i <= across; i++)
                {
                    float x = (float)i / across - .5f, y = (float)j / up;
                    float a = Mathf.Pow(Mathf.Cos(Mathf.PI * x), 2f) * Mathf.Clamp01(y / .12f) * Mathf.Clamp01((1f - y) / .2f);
                    v.Add(new Vector3(x, y, 0f));
                    c.Add(new Color(1f, 1f, 1f, a));
                    uv.Add(new Vector2(x + .5f, y));
                }
            for (int j = 0; j < up; j++)
                for (int i = 0; i < across; i++)
                {
                    int p = j * (across + 1) + i, q = p + across + 1;
                    tris.Add(p); tris.Add(q); tris.Add(p + 1);
                    tris.Add(p + 1); tris.Add(q); tris.Add(q + 1);
                    tris.Add(p); tris.Add(p + 1); tris.Add(q);
                    tris.Add(p + 1); tris.Add(q + 1); tris.Add(q);
                }
            quadMesh = new Mesh { name = "BH show blade" };
            quadMesh.SetVertices(v);
            quadMesh.SetColors(c);
            quadMesh.SetUVs(0, uv);
            quadMesh.SetTriangles(tris, 0);
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        // A ball 1 across, opaque in the middle and clear at its rim as seen from any side (its alpha falls with the
        // normal's turn from the view is not known here: it falls with height, so a dot of steam or a droplet reads soft).
        public static Mesh DotMesh()
        {
            if (dotMesh != null) return dotMesh;
            const int rings = 8, sides = 12;
            var v = new List<Vector3>();
            var c = new List<Color>();
            var uv = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= rings; j++)
            {
                float t = Mathf.PI * j / rings, y = -.5f * Mathf.Cos(t), r = .5f * Mathf.Sin(t);
                for (int i = 0; i <= sides; i++)
                {
                    float a = 2f * Mathf.PI * i / sides;
                    v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                    c.Add(new Color(1f, 1f, 1f, Mathf.Sin(t)));
                    uv.Add(new Vector2((float)i / sides, (float)j / rings));
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < sides; i++)
                {
                    int p = j * (sides + 1) + i, q = p + sides + 1;
                    tris.Add(p); tris.Add(q); tris.Add(p + 1);
                    tris.Add(p + 1); tris.Add(q); tris.Add(q + 1);
                }
            dotMesh = new Mesh { name = "BH show dot" };
            dotMesh.SetVertices(v);
            dotMesh.SetColors(c);
            dotMesh.SetUVs(0, uv);
            dotMesh.SetTriangles(tris, 0);
            dotMesh.RecalculateNormals();
            dotMesh.RecalculateBounds();
            return dotMesh;
        }

        static Mesh discMesh;

        // A disc 1 across in x-y facing -z (both faces), opaque in its middle and fading to nothing at its rim: a puff
        // of steam or smoke turned to face the camera.
        public static Mesh SoftDiscMesh()
        {
            if (discMesh != null) return discMesh;
            const int sides = 24;
            var v = new List<Vector3> { Vector3.zero };
            var c = new List<Color> { Color.white };
            var tris = new List<int>();
            foreach (var (r, a) in new[] { (.22f, .75f), (.38f, .3f), (.5f, 0f) })
                for (int i = 0; i < sides; i++)
                {
                    float t = 2f * Mathf.PI * i / sides;
                    v.Add(new Vector3(Mathf.Cos(t) * r, Mathf.Sin(t) * r, 0f));
                    c.Add(new Color(1f, 1f, 1f, a));
                }
            for (int i = 0; i < sides; i++)
            {
                int p = 1 + i, q = 1 + (i + 1) % sides;
                tris.Add(0); tris.Add(q); tris.Add(p);
                tris.Add(0); tris.Add(p); tris.Add(q);
            }
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < sides; i++)
                {
                    int p = 1 + ring * sides + i, q = 1 + ring * sides + (i + 1) % sides, pp = p + sides, qq = q + sides;
                    tris.Add(p); tris.Add(q); tris.Add(pp);
                    tris.Add(q); tris.Add(qq); tris.Add(pp);
                    tris.Add(p); tris.Add(pp); tris.Add(q);
                    tris.Add(q); tris.Add(pp); tris.Add(qq);
                }
            discMesh = new Mesh { name = "BH show soft disc" };
            discMesh.SetVertices(v);
            discMesh.SetColors(c);
            discMesh.SetTriangles(tris, 0);
            discMesh.RecalculateBounds();
            return discMesh;
        }

        // The camera drawing the hall now: the main one, or (a batch check's) the first that is on.
        public static Camera Viewer()
        {
            if (Camera.main != null) return Camera.main;
            foreach (var c in Camera.allCameras) if (c.enabled) return c;
            return null;
        }

        // ------------------------------------------------------------------ the candle flame (R845, 11-26)

        // A candle's flame as the hall draws it (the user 11-26: better candle flames), in place of R843's one-colour
        // teardrop: upright cards turned to the camera (about the flame's own up, so a flame leaning in a draught still
        // leans), each drawn with a flame painted in code -
        //   the body: soft-edged, a dim blue foot round the wick, a white-hot heart, yellow, an orange tip thinning out;
        //   the heart again, added on (it glows over the body);
        //   a halo of warm light round it, faint (added; it never whitens what is behind);
        //   the wick's ember, a red-orange spark at its foot.
        // Its root takes the pose the old flame took (on the wick, its up the flame's lean, its scale the candle's size
        // times how much it burns: a size-1 flame is 0.22 m high), so every candle code keeps its own flicker and timing.
        public static Transform NewFlame(string name, Transform parent)
        {
            CandleMaterials();
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var cards = new GameObject("cards").transform;
            cards.SetParent(root, false);
            Card(cards, "body", flameBody, new Vector3(0f, -.014f, 0f), new Vector2(.105f, .235f), false);
            Card(cards, "heart", flameHeart, new Vector3(0f, -.004f, -.002f), new Vector2(.05f, .12f), false);
            Card(cards, "halo", flameHalo, new Vector3(0f, .085f, .004f), new Vector2(.46f, .46f), true);
            Card(cards, "ember", flameEmber, new Vector3(0f, .002f, -.003f), new Vector2(.022f, .022f), true);
            root.gameObject.AddComponent<CandleFlameFacing>().cards = cards;
            return root;
        }

        static Material flameBody, flameHeart, flameHalo, flameEmber;
        static Mesh footCard, middleCard;

        static void Card(Transform parent, string name, Material material, Vector3 at, Vector2 size, bool centred)
        {
            var t = Drawn(name, parent, centred ? MiddleCard() : FootCard(), material);
            t.localPosition = at;
            t.localScale = new Vector3(size.x, size.y, 1f);
        }

        static void CandleMaterials()
        {
            if (flameBody != null) return;
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null) return;
            var flame = FlameTexture();
            var glow = GlowTexture();
            Material Make(string n, Texture tex, Color c, bool add)
            {
                var m = new Material(shader) { name = n, mainTexture = tex, color = c, hideFlags = HideFlags.DontSave };
                LitMaterials.MakeTransparent(m, add, (int)UnityEngine.Rendering.RenderQueue.Transparent + 5);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                return m;
            }
            flameBody = Make("BH candle flame", flame, new Color(1.25f, 1.15f, 1.05f, 1f), false);
            flameHeart = Make("BH candle flame heart", flame, new Color(1f, .9f, .7f, .55f), true);
            flameHalo = Make("BH candle flame halo", glow, new Color(1f, .68f, .36f, .2f), true);
            flameEmber = Make("BH candle wick ember", glow, new Color(1f, .4f, .12f, 1f), true);
        }

        // The flame painted on a 64 x 128 card, its foot at the bottom.
        static Texture2D FlameTexture()
        {
            const int w = 64, h = 128;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "BH candle flame", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            Color heart = new Color(1f, .98f, .9f), body = new Color(1f, .85f, .45f), rim = new Color(1f, .56f, .16f), tip = new Color(.95f, .36f, .08f),
                  blue = new Color(.32f, .5f, 1f);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + .5f) / h;
                // the outline: round at the foot, widest a third up, drawn to a point
                float half = Mathf.Max(.02f, .95f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(v, .6f)), 1.2f));
                float foot = Mathf.Clamp01(1f - v / .2f);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + .5f) / w * 2f - 1f, d = Mathf.Abs(u) / half;
                    float a = 1f - Smooth((d - .45f) / .55f);
                    a *= Smooth(v / .05f) * (1f - .75f * Smooth((v - .7f) / .3f));
                    var c = Color.Lerp(heart, body, Smooth((d - .1f) / .45f));
                    c = Color.Lerp(c, rim, Smooth((d - .5f) / .45f));
                    c = Color.Lerp(c, tip, .75f * Smooth((v - .45f) / .5f));
                    // the foot: blue and thin round the wick, darker in the middle where the wax vapour is
                    c = Color.Lerp(c, blue, .85f * foot * foot);
                    a *= 1f - .45f * foot * (1f - Mathf.Clamp01(d));
                    c.a = Mathf.Clamp01(a);
                    px[y * w + x] = c;
                }
            }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }

        // A soft round glow on a 64 x 64 card.
        static Texture2D GlowTexture()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BH candle glow", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = new Vector2((x + .5f) / n * 2f - 1f, (y + .5f) / n * 2f - 1f).magnitude;
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 2.4f));
                }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }

        static Mesh FootCard() => footCard != null ? footCard : footCard = CardMesh("BH flame card", 0f);
        static Mesh MiddleCard() => middleCard != null ? middleCard : middleCard = CardMesh("BH glow card", -.5f);

        static Mesh CardMesh(string name, float from)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            m.SetVertices(new List<Vector3> { new Vector3(-.5f, from, 0f), new Vector3(.5f, from, 0f), new Vector3(.5f, from + 1f, 0f), new Vector3(-.5f, from + 1f, 0f) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            m.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            m.SetTriangles(new List<int> { 0, 2, 1, 0, 3, 2 }, 0);
            m.RecalculateNormals();
            m.bounds = new Bounds(new Vector3(0f, from + .5f, 0f), new Vector3(1f, 1f, 1f));
            return m;
        }

        // ------------------------------------------------------------------ candles

        // The wicks' tops of a model's candles, in `frame`'s frame: the black wick (the "piece_black" slot, drawn as
        // BH_Ebony) of every candle is a little tube; its vertices are gathered by nearness, and each small gathering's
        // highest point is a wick's top. Only the finest level of a part is read (its "_LOD0", or a part without levels).
        public static List<Vector3> Wicks(Transform model, Transform frame, Func<string, bool> part = null)
        {
            var tops = new List<Vector3>();
            if (model == null) return tops;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = filter.name;
                if (n.Contains("_LOD") && !n.EndsWith("_LOD0", StringComparison.Ordinal)) continue;
                if (part != null && !part(n)) continue;
                var mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable || !filter.TryGetComponent<Renderer>(out var r)) continue;
                var mats = r.sharedMaterials;
                var toFrame = frame.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var v = mesh.vertices;
                var tris = new List<int>();
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    string m = mats[s] != null ? mats[s].name : "";
                    if (m.IndexOf("Ebony", StringComparison.OrdinalIgnoreCase) < 0 && m.IndexOf("piece_black", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    mesh.GetTriangles(tris, s);
                    var used = new HashSet<int>(tris);
                    var groups = new List<(Vector3 first, Vector3 top, Vector3 lo, Vector3 hi)>();
                    foreach (int i in used)
                    {
                        var p = toFrame.MultiplyPoint3x4(v[i]);
                        int k = groups.FindIndex(g => (g.first - p).sqrMagnitude < .06f * .06f);
                        if (k < 0) groups.Add((p, p, p, p));
                        else
                        {
                            var g = groups[k];
                            groups[k] = (g.first, p.y > g.top.y ? p : g.top, Vector3.Min(g.lo, p), Vector3.Max(g.hi, p));
                        }
                    }
                    // a wick is small: anything wider is some other black part
                    // a wick is small: anything wider is some other black part; a model may hold the same candles twice
                    // (a level without its "_LOD" name beside the named ones): one flame a wick
                    // (a long wick may fall into two gatherings: one flame per wick, on the higher)
                    foreach (var g in groups)
                    {
                        if ((g.hi - g.lo).magnitude >= .14f) continue;
                        int same = tops.FindIndex(t => new Vector2(t.x - g.top.x, t.z - g.top.z).sqrMagnitude < .05f * .05f && Mathf.Abs(t.y - g.top.y) < .15f);
                        if (same < 0) tops.Add(g.top);
                        else if (g.top.y > tops[same].y) tops[same] = g.top;
                    }
                }
            }
            return tops;
        }

        // Candle flames on `wicks` (the frame's): each a flame wavering on its own, with a soft halo behind it if `halo`
        // is given; drawn under `holder`. Update() every frame (decoration: Time.time is enough).
        public sealed class Flames
        {
            readonly List<Transform> flames = new List<Transform>(), halos = new List<Transform>();
            readonly List<float> seeds = new List<float>();
            readonly float size;
            MaterialPropertyBlock block;

            public int Count => flames.Count;
            public bool Lit { get; private set; } = true;

            public Flames(Transform holder, IList<Vector3> wicks, Material fire, Material halo, float flameSize)
            {
                size = flameSize;
                foreach (var w in wicks)
                {
                    var f = NewFlame("BH show flame", holder);
                    f.localPosition = w - new Vector3(0f, .01f, 0f);
                    flames.Add(f);
                    seeds.Add(Mathf.Repeat(w.x * 1.31f + w.z * .77f + w.y, 1f) * 50f);
                }
            }

            // `level` 0 out .. 1 burning (over 1: flaring); the halos turned to `eye`.
            public void Update(float level, Transform eye)
            {
                float t = Time.time * 3.1f;
                bool on = level > .02f;
                if (on != Lit)
                {
                    foreach (var f in flames) f.gameObject.SetActive(on);
                    foreach (var h in halos) h.gameObject.SetActive(on);
                    Lit = on;
                }
                if (!on) return;
                for (int i = 0; i < flames.Count; i++)
                {
                    float s = seeds[i], f = Mathf.PerlinNoise(t * 2.3f + s, s * .37f) - .5f, g = Mathf.PerlinNoise(s * .61f, t * 1.7f + s) - .5f;
                    flames[i].localRotation = Quaternion.Euler(14f * f, 0f, 14f * g);
                    float wide = size * Mathf.Min(1f, level + .25f) * (1f - .12f * f), high = size * level * (1f + .3f * f + .1f * g);
                    flames[i].localScale = new Vector3(wide, high, wide);
                    if (i < halos.Count)
                    {
                        var h = halos[i];
                        float r = size * .55f * (1f + .15f * f) * Mathf.Min(1f, level);
                        h.localScale = new Vector3(r, r, r);
                        if (eye != null) h.rotation = Quaternion.LookRotation(h.position - eye.position);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ swings from the clock

        // How far something hung from a carrier lags behind it (in the carrier's units, signed): the damped spring
        // y'' + 2 z w y' + w^2 y = -a(t) driven by the carrier's acceleration a, summed over the last `window` seconds
        // (its impulse response e^(-z w s) sin(wd s) / wd). `place(t)` is the carrier's place at clock t - a pure function,
        // so the lag is one too: every PC draws the same, nothing accumulates (HANDOFF rule 7).
        public static float Lag(Func<double, float> place, double now, float hertz, float damping, float window = 4f, float step = .04f)
        {
            const float e = .02f;
            return Response(t => -(place(t + e) - 2f * place(t) + place(t - e)) / (e * e), now, hertz, damping, window, step);
        }

        // The same spring driven by any force (per unit mass): y'' + 2 z w y' + w^2 y = force(t). A tassel pushed by the
        // air a curtain drags is driven by the curtain's speed.
        public static float Response(Func<double, float> force, double now, float hertz, float damping, float window = 4f, float step = .04f)
        {
            float w = 2f * Mathf.PI * hertz, wd = w * Mathf.Sqrt(Mathf.Max(1e-4f, 1f - damping * damping));
            float sum = 0f;
            for (float s = step * .5f; s < window; s += step)
            {
                float f = force(now - s);
                if (f == 0f) continue;
                sum += f * Mathf.Exp(-damping * w * s) * Mathf.Sin(wd * s) / wd * step;
            }
            return sum;
        }

        // The speed of a pure function of the clock at t.
        public static float Speed(Func<double, float> place, double t, float e = .02f) => (place(t + e) - place(t - e)) / (2f * e);

        // A lag kept under `limit` and easing into it.
        public static float Soft(float x, float limit) => limit * (float)Math.Tanh(x / limit);

        public static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }

    // Turns a candle flame's cards to the camera about the flame's own up (BanquetShowKit.NewFlame), every frame.
    [DefaultExecutionOrder(260)]
    public sealed class CandleFlameFacing : MonoBehaviour
    {
        public Transform cards;

        void LateUpdate()
        {
            var viewer = BanquetShowKit.Viewer();
            if (viewer == null || cards == null) return;
            var up = transform.up;
            var away = cards.position - viewer.transform.position;
            away -= up * Vector3.Dot(away, up);
            if (away.sqrMagnitude < 1e-8f) return;
            cards.rotation = Quaternion.LookRotation(away, up);
        }
    }
}
