using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's kit (왕실 마차뜰, 11-02): the materials and the few shapes the map and its obstacles
    // are made of, and the questions every obstacle asks about a character (may this PC push it, is it holding on to
    // me). Built at run time like the rest of the Pawn Rush map kit: the materials come from Arena.Lit (stone and
    // wood with the kit's textures), the round parts get MapLook's smooth mesh for the picture and a 20-sided convex
    // collider for the physics.
    public static class YardKit
    {
        // What a shape's collider is: none (a picture), solid, solid but never climbed or gripped (marble, railings),
        // or gripped and carried but never climbed (the wheels, the gate).
        public enum Solid { None, Solid, NoClimb, GrabOnly }

        // ------------------------------------------------------------------ materials

        public static Material Stone => Arena.Lit("M_LightStone", Arena.Hex("#D8CBB0"), 0f, .75f);
        public static Material WallStone => Arena.Lit("M_PillarStone", Arena.Hex("#CDBB98"), 0f, .8f);
        // The project's sign for "cannot be climbed": smooth marble (NoClimbSurface is always paired with it).
        public static Material Marble => Arena.Lit("M_YardMarble", Arena.Hex("#EEE7DA"), 0f, .22f);
        public static Material WoodDark => Arena.Lit("M_Walnut", Arena.Walnut, 0f, .7f);
        public static Material WoodLight => Arena.Lit("M_Maple", Arena.Maple, 0f, .7f);
        public static Material Gold => Arena.TrimGold;
        public static Material Roof => Arena.Lit("M_YardRoof", Arena.Hex("#3A5AA8"), 0f, .45f);
        public static Material Iron => Arena.Lit("M_YardIron", Arena.Hex("#2A2B31"), 0f, .45f);
        public static Material Glass => Arena.Lit("M_YardGlass", Arena.Hex("#1D2733"), 0f, .1f);
        public static Material Banner => Arena.Lit("M_YardBanner", Arena.Hex("#24408F"), 0f, .85f);
        public static Material Hedge => Arena.Lit("M_YardHedge", Arena.Hex("#3E6A34"), 0f, .9f);
        public static Material Lamp => Arena.Emissive(Arena.Hex("#FFC86A"), 1.4f);
        public static Material Warning => Arena.Emissive(Arena.Hex("#FF4A3A"), 1.6f);
        public static Material CarriageGreen => Arena.Lit("M_CarriageGreen", Arena.Hex("#2E5D4B"), 0f, .35f);
        public static Material CarriageRed => Arena.Lit("M_CarriageRed", Arena.Hex("#7A2430"), 0f, .35f);
        public static Material CarriageBlue => Arena.Lit("M_CarriageBlue", Arena.Hex("#27407A"), 0f, .35f);

        static Material water, fall;
        static Texture2D streaks;

        // The kit's water if the project has it (Resources/PR_Water), else a see-through blue.
        public static Material Water
        {
            get
            {
                var kit = MapLook.Water;
                if (kit != null) return kit;
                if (water == null)
                {
                    water = LitMaterials.NewLit(new Color(.22f, .52f, .76f, .72f), .92f);
                    water.name = "M_YardWater";
                    LitMaterials.MakeTransparent(water);
                }
                return water;
            }
        }

        // Falling water: an unlit see-through sheet of streaks that WaterfallFlow scrolls down.
        public static Material Waterfall
        {
            get
            {
                if (fall != null) return fall;
                fall = LitMaterials.NewUnlit(new Color(.86f, .94f, 1f, .78f));
                fall.name = "M_YardWaterfall";
                LitMaterials.MakeTransparent(fall);
                fall.mainTexture = Streaks();
                fall.mainTextureScale = new Vector2(3f, 1.5f);
                return fall;
            }
        }

        static Texture2D Streaks()
        {
            if (streaks != null) return streaks;
            const int w = 32, h = 128;
            streaks = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "YardWaterfallStreaks", wrapMode = TextureWrapMode.Repeat };
            var random = new System.Random(7);
            var pixels = new Color[w * h];
            for (int x = 0; x < w; x++)
            {
                float column = .55f + (float)random.NextDouble() * .45f;
                float wave = (float)random.NextDouble() * 6.283f;
                for (int y = 0; y < h; y++)
                {
                    float streak = .5f + .5f * Mathf.Sin(y * .19f + wave + x * .7f);
                    float a = Mathf.Clamp01(column * (.45f + .55f * streak));
                    float v = .82f + .18f * streak;
                    pixels[y * w + x] = new Color(v, v, 1f, a);
                }
            }
            streaks.SetPixels(pixels);
            streaks.Apply();
            return streaks;
        }

        // ------------------------------------------------------------------ shapes

        public static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // A box by its centre and size, in `parent`'s frame.
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material,
                                     Solid solid = Solid.Solid, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = Abs(size);
            Finish(go, material, solid);
            return go;
        }

        // A box by its min and max corners (the map's tables are written that way).
        public static GameObject Span(Transform parent, string name, Vector3 min, Vector3 max, Material material, Solid solid = Solid.Solid) =>
            Box(parent, name, (min + max) * .5f, max - min, material, solid);

        // A 1 m thick floor whose top is `top`, walked on (CourseFloor).
        public static GameObject Floor(Transform parent, string name, float x0, float x1, float z0, float z1, float top, Material material = null)
        {
            var go = Span(parent, name, new Vector3(x0, top - 1f, z0), new Vector3(x1, top, z1), material != null ? material : Stone);
            go.AddComponent<CourseFloor>();
            return go;
        }

        // A wall that cannot be climbed (marble), x0..x1, y0..y1, z0..z1.
        public static GameObject Wall(Transform parent, string name, float x0, float x1, float y0, float y1, float z0, float z1, Material material = null) =>
            Span(parent, name, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), material != null ? material : Marble, Solid.NoClimb);

        // A tilted slab whose top centre line runs from `from` to `to`, `width` across and `thickness` deep under it.
        public static GameObject Slab(Transform parent, string name, Vector3 from, Vector3 to, float width, float thickness,
                                      Material material, Solid solid = Solid.Solid)
        {
            var d = to - from;
            var rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            var normal = rotation * Vector3.up;
            var go = Box(parent, name, (from + to) * .5f - normal * (thickness * .5f), new Vector3(width, thickness, d.magnitude),
                         material, solid, rotation);
            return go;
        }

        // A cylinder `length` long along `axis`, centred on `center`.
        public static GameObject Cylinder(Transform parent, string name, Vector3 center, Vector3 axis, float diameter, float length,
                                          Material material, Solid solid = Solid.Solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up);
            go.transform.localScale = new Vector3(diameter, length * .5f, diameter);
            var primitive = go.GetComponent<MeshFilter>().sharedMesh;
            MapLook.Smooth(go);   // 96 sides for the picture; the collider keeps the primitive's 20
            if (solid != Solid.None)
            {
                var round = go.AddComponent<MeshCollider>();
                round.sharedMesh = primitive;
                round.convex = true;
            }
            Finish(go, material, solid);
            return go;
        }

        public static GameObject Ball(Transform parent, string name, Vector3 center, float diameter, Material material, Solid solid = Solid.Solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = Vector3.one * diameter;
            Finish(go, material, solid);
            return go;
        }

        static Mesh cone;

        // A round roof: a cone standing on `baseCenter` (a picture, no collider).
        public static GameObject Cone(Transform parent, string name, Vector3 baseCenter, float diameter, float height, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = baseCenter;
            go.transform.localScale = new Vector3(diameter, height, diameter);
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Mesh ConeMesh()
        {
            if (cone != null) return cone;
            const int sides = 48;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                int v = vertices.Count;
                vertices.Add(new Vector3(Mathf.Sin(a0) * .5f, 0f, Mathf.Cos(a0) * .5f));
                vertices.Add(new Vector3(0f, 1f, 0f));
                vertices.Add(new Vector3(Mathf.Sin(a1) * .5f, 0f, Mathf.Cos(a1) * .5f));
                triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
                int b = vertices.Count;
                vertices.Add(new Vector3(Mathf.Sin(a0) * .5f, 0f, Mathf.Cos(a0) * .5f));
                vertices.Add(new Vector3(Mathf.Sin(a1) * .5f, 0f, Mathf.Cos(a1) * .5f));
                vertices.Add(Vector3.zero);
                triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
            }
            cone = new Mesh { name = "YardCone" };
            cone.SetVertices(vertices);
            cone.SetTriangles(triangles, 0);
            cone.RecalculateNormals();
            cone.RecalculateBounds();
            return cone;
        }

        // A ring of `segments` boxes round `axis` (local), `radius` to the middle of the band, `band` thick across
        // the radius and `depth` along the axis. Wheel rims.
        public static void Ring(Transform parent, string name, Vector3 center, Vector3 axis, float radius, float band, float depth,
                                int segments, Material material, Solid solid)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = center;
            group.localRotation = Quaternion.FromToRotation(Vector3.forward, axis.normalized);
            float step = 360f / segments;
            float chord = 2f * radius * Mathf.Tan(step * .5f * Mathf.Deg2Rad) + band * .1f;
            for (int i = 0; i < segments; i++)
            {
                var r = Quaternion.AngleAxis(i * step, Vector3.forward);
                Box(group, name + " " + i, r * (Vector3.up * radius), new Vector3(chord, band, depth), material, solid, r);
            }
        }

        // A picture: a box with no collider.
        public static GameObject Picture(Transform parent, string name, Vector3 min, Vector3 max, Material material) =>
            Span(parent, name, min, max, material, Solid.None);

        static void Finish(GameObject go, Material material, Solid solid)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (material != null && renderer != null) renderer.sharedMaterial = material;
            // The primitive's own collider: a cylinder's capsule goes (Cylinder adds its convex mesh), anything not
            // solid loses it. Immediately: the parts are built on inactive objects that are switched on right after.
            var own = go.GetComponent<Collider>();
            if (own is CapsuleCollider || (solid == Solid.None && own != null)) Object.DestroyImmediate(own);
            if (solid == Solid.None)
            {
                var extra = go.GetComponent<MeshCollider>();
                if (extra != null) Object.DestroyImmediate(extra);
                return;
            }
            if (solid == Solid.NoClimb) go.AddComponent<NoClimbSurface>();
            else if (solid == Solid.GrabOnly) go.AddComponent<GrabOnlySurface>();
        }

        static Font font;

        // A sign over the course: Korean if the game's font is there, else the built-in one (numbers and English).
        public static TextMesh Label(Transform parent, string text, Vector3 at, float size, Color color, float yaw = 0f)
        {
            if (font == null) font = RuntimePanels.KoreanFont != null ? RuntimePanels.KoreanFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = .06f * size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            if (font != null) go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return mesh;
        }

        // ------------------------------------------------------------------ the carriage

        public const float CarriageLength = 4.2f, CarriageWidth = 2.3f, CarriageHeight = 2.75f, CarriageWheel = 1.1f;

        // A royal carriage, nose to local +z, standing on y 0: pictures only, and its four wheel pivots (each turns
        // about its local x) for whoever rolls them. The collider is the caller's one box over all of it
        // (CarriageCollider): no gap under the chassis for anyone to be pinned in, nothing sticking out of it.
        public static Transform[] Carriage(Transform parent, Material body)
        {
            var c = new GameObject("Carriage").transform;
            c.SetParent(parent, false);
            const Solid none = Solid.None;
            Box(c, "Chassis", new Vector3(0f, .72f, 0f), new Vector3(1.5f, .24f, 4f), WoodDark, none);
            Box(c, "Cabin", new Vector3(0f, 1.66f, -.3f), new Vector3(2.08f, 1.64f, 2.9f), body, none);
            Box(c, "Trim low", new Vector3(0f, .9f, -.3f), new Vector3(2.14f, .09f, 2.96f), Gold, none);
            Box(c, "Trim high", new Vector3(0f, 2.44f, -.3f), new Vector3(2.14f, .1f, 2.96f), Gold, none);
            foreach (float side in new[] { -1f, 1f })
            {
                Box(c, "Window", new Vector3(1.05f * side, 1.86f, -.3f), new Vector3(.04f, .62f, 1.3f), Glass, none);
                Box(c, "Door line", new Vector3(1.05f * side, 1.55f, -.3f), new Vector3(.05f, 1.2f, .06f), Gold, none);
                Box(c, "Lamp", new Vector3(1.08f * side, 2.02f, 1.2f), new Vector3(.18f, .3f, .18f), Lamp, none);
            }
            Box(c, "Roof", new Vector3(0f, 2.57f, -.3f), new Vector3(2.28f, .18f, 3.1f), WoodDark, none);
            Box(c, "Crown base", new Vector3(0f, 2.71f, -.3f), new Vector3(.62f, .12f, .62f), Gold, none);
            Ball(c, "Crown", new Vector3(0f, 2.82f, -.3f), .3f, Gold, none);
            Box(c, "Driver's bench", new Vector3(0f, 1.22f, 1.5f), new Vector3(1.5f, .22f, .7f), WoodLight, none);
            Box(c, "Footboard", new Vector3(0f, .95f, 1.92f), new Vector3(1.5f, .08f, .36f), WoodDark, none);
            var wheels = new Transform[4];
            int n = 0;
            foreach (float z in new[] { -1.3f, 1.3f })
                foreach (float side in new[] { -1f, 1f })
                {
                    var pivot = new GameObject("Wheel").transform;
                    pivot.SetParent(c, false);
                    pivot.localPosition = new Vector3(1.08f * side, CarriageWheel * .5f, z);
                    Cylinder(pivot, "Tyre", Vector3.zero, Vector3.right, CarriageWheel, .14f, WoodDark, none);
                    Cylinder(pivot, "Hub", new Vector3(.08f * side, 0f, 0f), Vector3.right, .3f, .06f, Gold, none);
                    Box(pivot, "Spoke", Vector3.zero, new Vector3(.06f, CarriageWheel * .9f, .08f), WoodLight, none);
                    Box(pivot, "Spoke", Vector3.zero, new Vector3(.06f, .08f, CarriageWheel * .9f), WoodLight, none);
                    wheels[n++] = pivot;
                }
            return wheels;
        }

        // The carriage's one solid box, and the trigger shell round it that knocks characters away.
        public static YardImpact CarriageCollider(Transform parent, float knockback)
        {
            var body = new GameObject("Carriage (collider)");
            body.transform.SetParent(parent, false);
            var box = body.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, (CarriageHeight + .05f) * .5f, 0f);
            box.size = new Vector3(CarriageWidth, CarriageHeight - .05f, CarriageLength);
            body.AddComponent<NoClimbSurface>();
            return Shell(parent, "Carriage (hit)", box.center, box.size + new Vector3(.5f, .2f, .5f), knockback);
        }

        // A trigger box that knocks characters away from the moving thing it belongs to.
        public static YardImpact Shell(Transform parent, string name, Vector3 center, Vector3 size, float knockback, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            var impact = go.AddComponent<YardImpact>();
            impact.knockbackForce = knockback;
            return impact;
        }

        // ------------------------------------------------------------------ characters

        // The character a collider belongs to, if this PC is the one that moves it: offline, or the host of a match
        // (ObstacleClock.Authority), and not a network puppet (its hips are kinematic there).
        public static bool Movable(Collider collider, out ICharacterDriver driver, out IHitReceiver hits)
        {
            hits = null;
            driver = collider != null ? collider.GetComponentInParent<ICharacterDriver>() : null;
            if (driver == null || !ObstacleClock.Authority) return false;
            hits = collider.GetComponentInParent<IHitReceiver>();
            if (hits == null) return false;
            var hips = driver.FollowTarget != null ? driver.FollowTarget.GetComponent<Rigidbody>() : null;
            return hips == null || !hips.isKinematic;
        }

        static readonly Collider[] near = new Collider[128];
        static readonly List<ICharacterDriver> found = new List<ICharacterDriver>();

        // Every character with a part within `radius` of `center` (one entry each). The list is reused: read it
        // before asking again.
        public static List<ICharacterDriver> CharactersNear(Vector3 center, float radius)
        {
            found.Clear();
            int n = Physics.OverlapSphereNonAlloc(center, radius, near, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var driver = near[i].GetComponentInParent<ICharacterDriver>();
                if (driver != null && !found.Contains(driver)) found.Add(driver);
            }
            return found;
        }

        static readonly List<FixedJoint> grips = new List<FixedJoint>();

        // The grip a character holds `body` by: the ragdoll's hands hold on with a FixedJoint to whatever they grab
        // (right mouse button held), so a joint on one of its parts connected to `body` is a hand on it.
        public static FixedJoint GripOn(ICharacterDriver driver, Rigidbody body)
        {
            var owner = driver as Component;
            if (owner == null || body == null) return null;
            owner.GetComponentsInChildren(grips);
            foreach (var grip in grips)
                if (grip != null && grip.connectedBody == body) return grip;
            return null;
        }
    }
}
