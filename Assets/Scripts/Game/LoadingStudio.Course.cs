using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Game
{
    // The entrance screen's course model (10-25, 승규 님 chose concepts 1 + 5, then "more depth from this view"):
    // on the study table, instead of the chessboard, a walnut plate with a brass rim and a row of brass emitters,
    // and over it Pawn Rush course 01 (Course01v4Builder, design doc v0.4) at its real world coordinates: every
    // floor at its height (0, 3, 6 m, heights drawn 2.4 times taller so the steps read), the obstacles (spinning
    // bars, discs, sliding walls, the round board, the chess-clock gate posts, the rook walls), the plaza walls and
    // the 8th-rank tower with its battlements and a crown. 10-26 (승규 님: "플레이어가 들어오는 건 넣지마"): nobody walks
    // on; who is in shows only in the cards and in how far the route is lit.
    // 10-26 (승규 님: "실사적으로 홀로그램 이였으면 좋겠어"): the model is a hologram, light rather than solid gold
    // (ChessFight/Hologram and Hologram Line, HoloMesh). The floors, the plaza walls and the tower are one solid built
    // from the floor plan on a metre grid, so no faces overlap or show seams: faint light on its faces, densest near
    // the plate, glowing edges that bloom where it turns, a band of light rising through it, dust glinting in the
    // light, and the light it throws on the plate and on the pieces at the start. The crown turns slowly over the
    // tower. The route is a line of light just over the floors, dotted all the way and lit as far as the share of
    // players that are in (all twelve: it reaches the tower), its dashes running on towards the tower and a bright
    // point at the head of the light.
    // 10-26 (승규 님: "바닥에 LED 같이 홀로그램과 잘 어울러 졌으면 좋겠어", then "홀로그램이 없는 바닥에는 LED 처리 하지
    // 말고"): inside the walnut frame the plate is a panel of smoked glass, and under the hologram, only there, amber
    // LEDs in it (ChessFight/Hologram Floor) burn as if they threw it up, pulsing with each band of light that rises
    // through it.
    public sealed partial class LoadingStudio
    {
        const float CourseScale = .05f, CourseHeight = 2.4f, CourseYaw = 52f;
        static readonly Vector3 CourseAt = new Vector3(0f, .14f, 1.2f);

        static readonly Vector2[] Route =
        {
            new Vector2(0, -8), new Vector2(0, 8), new Vector2(0, 20), new Vector2(0, 28), new Vector2(0, 31), new Vector2(-1.5f, 35),
            new Vector2(1.5f, 45), new Vector2(-1.5f, 55), new Vector2(0, 65), new Vector2(16, 65), new Vector2(28, 65), new Vector2(40, 65),
            new Vector2(46, 72), new Vector2(46, 85), new Vector2(46, 101), new Vector2(46, 112), new Vector2(36, 120), new Vector2(27, 120),
            new Vector2(18, 120), new Vector2(11, 120), new Vector2(4, 120), new Vector2(-3, 124), new Vector2(-3, 132), new Vector2(-3, 142),
            new Vector2(-3, 161), new Vector2(-3, 176), new Vector2(-3, 186), new Vector2(-3, 194), new Vector2(-3, 202), new Vector2(-3, 212),
        };
        // x0, x1, z0, z1 (design doc v0.4 §4)
        static readonly Vector4[] Floors =
        {
            new Vector4(-8, 8, -12, 0), new Vector4(-5, 5, 0, 28), new Vector4(-5, 5, 28, 31), new Vector4(-6, 6, 59, 70), new Vector4(6, 40, 60, 70),
            new Vector4(40, 52, 58, 72), new Vector4(24, 68, 72, 97), new Vector4(24, 68, 101, 110), new Vector4(36, 52, 110, 124),
            new Vector4(34, 36, 116, 124), new Vector4(17, 20, 116, 124), new Vector4(3, 17, 116, 124), new Vector4(-9, 3, 112, 130),
            new Vector4(-25, 19, 130, 155), new Vector4(-25, 19, 161, 170), new Vector4(-9, 3, 170, 202), new Vector4(-17, 11, 202, 210),
        };

        // The floor plan's grid: one cell a course metre, x -25..67 and z -12..229. The solid's foot is a little
        // under the ground floors so they have a thickness.
        const int GridX = -25, GridZ = -12, GridW = 93, GridD = 242;
        const float Base = -.2f;
        // The LED panel: the plate less a walnut border, in course metres (x -30..73, z -17..243), with an LED about
        // every two metres.
        const int PanelX = -30, PanelZ = -17, PanelW = 103, PanelL = 260, LedsAcross = 52, LedsAlong = 130;

        bool courseModel;
        Transform course, crown, routeHead;
        Material holoFace, holoEdge, holoRoute, holoHead, holoMote, holoFloor;
        Texture2D footprint;
        float routeLength, routeShown, routeTarget;
        readonly List<Vector3> routePath = new List<Vector3>();
        readonly List<float> routeAt = new List<float>();
        readonly List<Mesh> courseMeshes = new List<Mesh>();

        static float FloorHeight(float x, float z)
        {
            if (z >= 202) return 6f;
            if ((z >= 130 && z <= 170) || (x <= 3 && z >= 112)) return 3f;
            if (z >= 28 && z <= 70 && x <= 40) return 3f;
            return 0f;
        }

        // Course coordinates (x east, y up, z north, metres) into the model's local space.
        static Vector3 L(float x, float y, float z) => new Vector3(x - 21.5f, y * CourseHeight, z - 111f);

        // A corner of the grid at height y.
        static Vector3 G(float i, float y, float j) => L(GridX + i, y, GridZ + j);

        void CourseModel(Transform t)
        {
            // the plate: walnut with a brass rim and emitters along the long sides
            var plate = new GameObject("Course Plate").transform;
            plate.SetParent(t, false);
            plate.localPosition = CourseAt - Vector3.up * .07f;
            plate.localRotation = Quaternion.Euler(0, CourseYaw, 0);
            float pw = 113f * CourseScale, pl = 270f * CourseScale, pz = 2f * CourseScale;
            MenuArt.Slab(plate, "Plate", new Vector3(0, 0, pz), new Vector3(pw, .14f, pl), MenuArt.Wood(MenuArt.Walnut, .5f, new Vector2(2f, 4f)));
            var brass = MenuArt.BrassMetal();
            foreach (float s in new[] { -1f, 1f })
            {
                MenuArt.Slab(plate, "Rim", new Vector3(s * pw / 2, .03f, pz), new Vector3(.06f, .2f, pl + .06f), brass);
                MenuArt.Slab(plate, "Rim", new Vector3(0, .03f, pz + s * pl / 2), new Vector3(pw + .06f, .2f, .06f), brass);
                for (int i = 0; i < 9; i++)
                    LastSceneArt.Part(plate, "Emitter", LastSceneArt.Sphere(.05f), MenuArt.Lamp(0xFFE6A8),
                                      new Vector3(s * (pw / 2 - .12f), .09f, pz - pl / 2 + .4f + i * (pl - .8f) / 8f), null, null, false);
            }

            // the LED panel: smoked glass inside the walnut border, 3 mm proud of it, the LEDs just over the glass
            // (satin rather than mirror smooth: the hologram's glow lights caught on mirror glass as orange streaks)
            float lw = PanelW * CourseScale, ll = PanelL * CourseScale;
            MenuArt.Slab(plate, "LED Glass", new Vector3(0, .0715f, pz), new Vector3(lw, .003f, ll), LastSceneArt.Lit(0x0B0A0A, .6f));

            course = new GameObject("Course Hologram").transform;
            course.SetParent(t, false);
            // its foot 7 mm over the walnut, just over the LEDs
            course.localPosition = CourseAt + Vector3.up * (-Base * CourseHeight * CourseScale + .007f);
            course.localRotation = Quaternion.Euler(0, CourseYaw, 0);
            course.localScale = Vector3.one * CourseScale;

            holoFace = HoloMaterial("ChessFight/Hologram", "Hologram/Hologram", "Course hologram");
            holoEdge = HoloMaterial("ChessFight/Hologram Line", "Hologram/HologramLine", "Course hologram edges");
            holoRoute = HoloMaterial("ChessFight/Hologram Line", "Hologram/HologramLine", "Course hologram route");
            holoHead = HoloMaterial("ChessFight/Hologram Line", "Hologram/HologramLine", "Course hologram route head");
            holoMote = HoloMaterial("ChessFight/Hologram Line", "Hologram/HologramLine", "Course hologram motes");
            holoFloor = HoloMaterial("ChessFight/Hologram Floor", "Hologram/HologramFloor", "Course hologram LEDs");

            var plan = Plan();
            var solid = new HoloMesh();
            Terrain(solid, plan);
            void Block(float x0, float x1, float y0, float y1, float z0, float z1, bool bottom = false) =>
                solid.Box(L(x0, y0, z0), L(x1, y1, z1), bottom);
            // obstacles: the spinning bars and the pendulum blocks hang free, the rest stand on the floors
            foreach (float z in new[] { 8f, 20f }) Block(-6, 6, .25f, .95f, z - .35f, z + .35f, true);
            foreach (var d in new[] { new Vector2(-1.5f, 35), new Vector2(1.5f, 45), new Vector2(-1.5f, 55) }) Block(d.x - 3.6f, d.x + 3.6f, 3, 3.7f, d.y - 3.6f, d.y + 3.6f);
            foreach (float x in new[] { 16f, 28f }) Block(x - .45f, x + .45f, 3, 6.2f, 60, 70);
            Block(20, 34, 0, .7f, 113, 127);
            foreach (float x in new[] { -5.6f, -.4f }) Block(x - .7f, x + .7f, 3, 8, 175.3f, 176.7f);
            foreach (float z in new[] { 186f, 194f }) Block(-6, 0, 3, 6.4f, z - 1, z + 1);
            foreach (float x in new[] { 14.5f, 11f, 7.5f }) Block(x - 1.1f, x + 1.1f, .6f, 2.8f, 119f, 121f, true);
            // the tower's battlements
            for (int i = 0; i < 6; i++)
            {
                Block(-13 + i * 3.6f, -11.4f + i * 3.6f, 15, 17.2f, 210, 211.8f);
                Block(-13 + i * 3.6f, -11.4f + i * 3.6f, 15, 17.2f, 228.2f, 230);
            }
            for (int i = 1; i < 5; i++)
            {
                Block(-13, -11.2f, 15, 17.2f, 210 + i * 4, 211.6f + i * 4);
                Block(5.2f, 7, 15, 17.2f, 210 + i * 4, 211.6f + i * 4);
            }
            HoloPart(course, "Course Solid", solid.FaceMesh("Course hologram faces"), holoFace);
            HoloPart(course, "Course Edges", solid.LineMesh("Course hologram edges", 1f), holoEdge);

            // the crown, turning over the tower: a band and five points
            crown = new GameObject("Crown").transform;
            crown.SetParent(course, false);
            crown.localPosition = L(-3, 17.5f, 220) + Vector3.up * 1.5f;
            var crownLines = new HoloMesh();
            crownLines.Ring(Vector3.zero, 4f, 40);
            crownLines.Ring(Vector3.up * 2.4f, 4f, 40);
            LastSceneArt.Part(crown, "Crown Band", LastSceneArt.Cylinder(4f, 4f, 2.4f, 40), holoFace, Vector3.up * 1.2f, null, null, false);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var foot = new Vector3(Mathf.Cos(a) * 3.4f, 2.4f, Mathf.Sin(a) * 3.4f);
                LastSceneArt.Part(crown, "Crown Point", LastSceneArt.Cone(1.2f, 4.5f, 16), holoFace, foot + Vector3.up * 2.25f, null, null, false);
                for (int k = 0; k < 4; k++)
                {
                    float b = k * Mathf.PI / 2f + a;
                    crownLines.Line(foot + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * 1.2f, foot + Vector3.up * 4.5f, .8f);
                }
            }
            HoloPart(crown, "Crown Edges", crownLines.LineMesh("Course hologram crown", 1f), holoEdge);

            // the route: from the start to the tower, just over the floors; up or down a level along the old height
            // first, then straight up or down
            var route = new HoloMesh();
            Vector3 last = Vector3.zero;
            routeLength = 0f;
            routePath.Clear();
            routeAt.Clear();
            for (int i = 0; i < Route.Length; i++)
            {
                var p = L(Route[i].x, FloorHeight(Route[i].x, Route[i].y) + .45f, Route[i].y);
                if (i > 0)
                {
                    if (Mathf.Abs(p.y - last.y) > .01f)
                    {
                        var turn = new Vector3(p.x, last.y, p.z);
                        RouteLeg(route, last, turn);
                        last = turn;
                    }
                    RouteLeg(route, last, p);
                }
                last = p;
            }
            HoloPart(course, "Course Route", route.LineMesh("Course hologram route", 1f), holoRoute);
            var dot = new HoloMesh();
            dot.Line(Vector3.zero, Vector3.up * .01f);
            routeHead = HoloPart(course, "Route Head", dot.LineMesh("Course hologram route head", 2f), holoHead).transform;
            routeHead.gameObject.SetActive(false);

            // dust in the light: motes over the floors, rising and glinting
            var motes = new HoloMesh();
            var rng = new System.Random(26);
            for (int n = 0; n < 220;)
            {
                int i = rng.Next(GridW), j = rng.Next(GridD);
                if (float.IsNaN(plan[i, j])) continue;
                var p = G(i + (float)rng.NextDouble(), plan[i, j] + (float)rng.NextDouble() * 1.5f, j + (float)rng.NextDouble());
                float seed = (float)rng.NextDouble();
                motes.Line(p, p + Vector3.up * .05f, .4f + .6f * (float)rng.NextDouble(), seed, seed);
                n++;
            }
            HoloPart(course, "Course Motes", motes.LineMesh("Course hologram motes", 8f), holoMote);

            // the light it gives off, on the plate round it
            var amber = new Color(1f, .62f, .24f);
            foreach (var at in new[] { new Vector2(0, 0), new Vector2(34, 100), new Vector2(-3, 200) })
            {
                var glow = new GameObject("Hologram Glow").AddComponent<Light>();
                glow.transform.SetParent(course, false);
                glow.transform.localPosition = L(at.x, 4f, at.y);
                glow.type = LightType.Point;
                glow.color = amber;
                glow.range = 3.5f;
                glow.intensity = .25f;
                glow.shadows = LightShadows.None;
                glow.renderMode = LightRenderMode.ForcePixel;
            }

            // the LEDs over the glass, one every two course metres
            footprint = Footprint(plan);
            var leds = new Mesh
            {
                name = "Course hologram LEDs",
                vertices = new[] { new Vector3(-lw / 2, 0, -ll / 2), new Vector3(-lw / 2, 0, ll / 2), new Vector3(lw / 2, 0, ll / 2), new Vector3(lw / 2, 0, -ll / 2) },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            courseMeshes.Add(leds);
            LastSceneArt.Part(plate, "LEDs", leds, holoFloor, new Vector3(0, .0745f, pz), null, null, false);

            // the looks: amber light, white-gold edges blooming amber; the route brighter, dotted past where it is lit
            holoFace.SetColor("_Color", amber);
            holoFace.SetFloat("_Fill", .075f);
            holoFace.SetFloat("_Top", .045f);
            holoFace.SetFloat("_Rim", .3f);
            holoFace.SetFloat("_Base", 1f);
            holoFace.SetFloat("_BaseHeight", .12f);
            float footY = course.TransformPoint(L(0, Base, 0)).y;
            holoFace.SetFloat("_SweepFrom", footY);
            holoFace.SetFloat("_SweepHeight", course.TransformPoint(L(0, 24f, 0)).y - footY);
            holoEdge.SetColor("_Color", new Color(1f, .88f, .6f));
            holoEdge.SetColor("_GlowColor", new Color(1f, .58f, .2f));
            holoEdge.SetFloat("_Width", .045f);
            holoEdge.SetFloat("_CoreWidth", .12f);
            holoEdge.SetFloat("_Glow", .5f);
            holoRoute.SetColor("_Color", new Color(1f, .97f, .86f));
            holoRoute.SetColor("_GlowColor", new Color(1f, .74f, .34f));
            holoRoute.SetFloat("_Width", .035f);
            holoRoute.SetFloat("_CoreWidth", .3f);
            holoRoute.SetFloat("_Glow", .8f);
            holoRoute.SetFloat("_Dim", .3f);
            holoRoute.SetFloat("_Dash", 1f);
            holoRoute.SetFloat("_DashFlow", .15f);
            holoRoute.SetFloat("_Head", 2.5f);
            holoRoute.SetFloat("_Reveal", 0f);
            holoHead.SetColor("_Color", new Color(1f, .97f, .86f));
            holoHead.SetColor("_GlowColor", new Color(1f, .74f, .34f));
            holoHead.SetFloat("_Width", .07f);
            holoHead.SetFloat("_CoreWidth", .22f);
            holoHead.SetFloat("_Glow", 1.3f);
            holoMote.SetColor("_Color", new Color(1f, .93f, .7f));
            holoMote.SetColor("_GlowColor", amber);
            holoMote.SetFloat("_Width", .006f);
            holoMote.SetFloat("_CoreWidth", .45f);
            holoMote.SetFloat("_Glow", .9f);
            holoMote.SetFloat("_Drift", .3f);
            holoMote.SetFloat("_DriftTime", 9f);
            holoFloor.mainTexture = footprint;
            holoFloor.SetColor("_Color", amber);
            holoFloor.SetVector("_Cells", new Vector4(LedsAcross, LedsAlong, 0, 0));
        }

        void RouteLeg(HoloMesh route, Vector3 a, Vector3 b)
        {
            float d = Vector3.Distance(a, b) * CourseScale;
            if (routePath.Count == 0)
            {
                routePath.Add(a);
                routeAt.Add(0f);
            }
            route.Line(a, b, 1f, routeLength, routeLength + d, false);
            routeLength += d;
            routePath.Add(b);
            routeAt.Add(routeLength);
        }

        // The point `along` metres down the route, in the model's space.
        Vector3 RoutePoint(float along)
        {
            for (int i = 1; i < routePath.Count; i++)
                if (along <= routeAt[i] || i == routePath.Count - 1)
                    return Vector3.Lerp(routePath[i - 1], routePath[i], Mathf.InverseLerp(routeAt[i - 1], routeAt[i], along));
            return Vector3.zero;
        }

        GameObject HoloPart(Transform parent, string name, Mesh mesh, Material material)
        {
            courseMeshes.Add(mesh);
            return LastSceneArt.Part(parent, name, mesh, material, Vector3.zero, null, null, false);
        }

        static Material HoloMaterial(string shader, string resource, string name)
        {
            var s = Shader.Find(shader);
            if (s == null) s = Resources.Load<Shader>(resource);
            return new Material(s != null ? s : Shader.Find("Sprites/Default")) { name = name };
        }

        // The floor plan: the top of every cell in course metres, NaN where there is none. The floors, the plazas'
        // north walls and the 8th-rank tower (standing from the ground) make one solid.
        static float[,] Plan()
        {
            var h = new float[GridW, GridD];
            for (int i = 0; i < GridW; i++)
                for (int j = 0; j < GridD; j++) h[i, j] = float.NaN;
            void Raise(float x0, float x1, float z0, float z1, float top)
            {
                for (int i = Mathf.RoundToInt(x0) - GridX; i < Mathf.RoundToInt(x1) - GridX; i++)
                    for (int j = Mathf.RoundToInt(z0) - GridZ; j < Mathf.RoundToInt(z1) - GridZ; j++)
                        if (float.IsNaN(h[i, j]) || h[i, j] < top) h[i, j] = top;
            }
            foreach (var f in Floors) Raise(f.x, f.y, f.z, f.w, FloorHeight((f.x + f.y) / 2f, (f.z + f.w) / 2f));
            Raise(24, 68, 96, 97, 6);
            Raise(-25, 19, 154, 155, 9);
            Raise(-13, 7, 210, 230, 15);
            return h;
        }

        static float At(float[,] h, int i, int j) => i < 0 || j < 0 || i >= GridW || j >= GridD ? float.NaN : h[i, j];

        // The plan as one solid: the tops (merged into rectangles), the walls where the height changes, and the edges
        // where the solid turns: along the top and the foot of every wall and up every corner.
        static void Terrain(HoloMesh m, float[,] h)
        {
            var done = new bool[GridW, GridD];
            for (int j = 0; j < GridD; j++)
                for (int i = 0; i < GridW; i++)
                {
                    float y = h[i, j];
                    if (done[i, j] || float.IsNaN(y)) continue;
                    int i1 = i, j1 = j;
                    while (i1 + 1 < GridW && !done[i1 + 1, j] && h[i1 + 1, j] == y) i1++;
                    while (j1 + 1 < GridD && SameRow(h, done, i, i1, j1 + 1, y)) j1++;
                    for (int a = i; a <= i1; a++)
                        for (int b = j; b <= j1; b++) done[a, b] = true;
                    m.Face(G(i, y, j), G(i, y, j1 + 1), G(i1 + 1, y, j1 + 1), G(i1 + 1, y, j));
                }

            // walls across x (k = 0, on the grid line x = at) and across z (k = 1), one face for every run of the
            // same wall; their top and foot edges are gathered by height and joined afterwards
            var edges = new Dictionary<(int k, int at, float y, bool top), List<int>>();
            for (int k = 0; k < 2; k++)
            {
                int lines = k == 0 ? GridW : GridD, cells = k == 0 ? GridD : GridW;
                for (int at = 0; at <= lines; at++)
                {
                    int start = -1;
                    float runLo = 0, runHi = 0;
                    for (int c = 0; c <= cells; c++)
                    {
                        float lo = 0, hi = 0;
                        bool wall = c < cells && (k == 0 ? Step(At(h, at - 1, c), At(h, at, c), out lo, out hi)
                                                         : Step(At(h, c, at - 1), At(h, c, at), out lo, out hi));
                        if (start >= 0 && (!wall || lo != runLo || hi != runHi))
                        {
                            if (k == 0) m.Face(G(at, runLo, start), G(at, runHi, start), G(at, runHi, c), G(at, runLo, c));
                            else m.Face(G(start, runLo, at), G(start, runHi, at), G(c, runHi, at), G(c, runLo, at));
                            start = -1;
                        }
                        if (!wall) continue;
                        if (start < 0)
                        {
                            start = c;
                            runLo = lo;
                            runHi = hi;
                        }
                        Mark(edges, (k, at, hi, true), c);
                        Mark(edges, (k, at, lo, false), c);
                    }
                }
            }
            foreach (var e in edges)
            {
                var cs = e.Value;
                cs.Sort();
                // the foot of the solid on the plate glows least, the foot of a wall on a lower floor a little more
                float strength = e.Key.top ? 1f : e.Key.y == Base ? .45f : .7f;
                int first = 0;
                for (int n = 1; n <= cs.Count; n++)
                {
                    if (n < cs.Count && cs[n] == cs[n - 1] + 1) continue;
                    int c0 = cs[first], c1 = cs[n - 1] + 1;
                    if (e.Key.k == 0) m.Line(G(e.Key.at, e.Key.y, c0), G(e.Key.at, e.Key.y, c1), strength);
                    else m.Line(G(c0, e.Key.y, e.Key.at), G(c1, e.Key.y, e.Key.at), strength);
                    first = n;
                }
            }

            // up the corners: at every grid point, for each level between the heights round it, the solid turns there
            // if one or three of the four cells reach over that level, or two across from each other
            var levels = new List<float>(5);
            var round = new float[4];
            for (int i = 0; i <= GridW; i++)
                for (int j = 0; j <= GridD; j++)
                {
                    round[0] = At(h, i - 1, j - 1);
                    round[1] = At(h, i, j - 1);
                    round[2] = At(h, i - 1, j);
                    round[3] = At(h, i, j);
                    levels.Clear();
                    levels.Add(Base);
                    foreach (float y in round)
                        if (!float.IsNaN(y) && !levels.Contains(y)) levels.Add(y);
                    if (levels.Count < 2) continue;
                    levels.Sort();
                    float from = float.NaN;
                    for (int n = 0; n + 1 < levels.Count; n++)
                    {
                        float over = levels[n + 1];
                        // NaN (no floor) is never over a level
                        bool a = round[0] >= over, b = round[1] >= over, c = round[2] >= over, d = round[3] >= over;
                        int count = (a ? 1 : 0) + (b ? 1 : 0) + (c ? 1 : 0) + (d ? 1 : 0);
                        bool corner = count == 1 || count == 3 || (count == 2 && a == d);
                        if (corner && float.IsNaN(from)) from = levels[n];
                        if (!corner && !float.IsNaN(from))
                        {
                            m.Line(G(i, from, j), G(i, levels[n], j));
                            from = float.NaN;
                        }
                    }
                    if (!float.IsNaN(from)) m.Line(G(i, from, j), G(i, levels[levels.Count - 1], j));
                }
        }

        static bool SameRow(float[,] h, bool[,] done, int i0, int i1, int j, float y)
        {
            for (int i = i0; i <= i1; i++)
                if (done[i, j] || h[i, j] != y) return false;
            return true;
        }

        // Whether a wall stands between two cells (NaN: no floor), and from what height to what height.
        static bool Step(float a, float b, out float lo, out float hi)
        {
            lo = hi = 0;
            bool noA = float.IsNaN(a), noB = float.IsNaN(b);
            if (noA && noB) return false;
            if (noA || noB)
            {
                lo = Base;
                hi = noA ? b : a;
                return true;
            }
            if (a == b) return false;
            lo = Mathf.Min(a, b);
            hi = Mathf.Max(a, b);
            return true;
        }

        static void Mark(Dictionary<(int k, int at, float y, bool top), List<int>> edges, (int k, int at, float y, bool top) key, int c)
        {
            if (!edges.TryGetValue(key, out var cells)) edges[key] = cells = new List<int>();
            cells.Add(c);
        }

        // The hologram's footprint over the LED panel, one texel a course metre: r where the course stands (where
        // the LEDs are), b that blurred far (its light on the glass).
        static Texture2D Footprint(float[,] plan)
        {
            const int ox = GridX - PanelX, oz = GridZ - PanelZ;
            var stands = new float[PanelW * PanelL];
            for (int i = 0; i < GridW; i++)
                for (int j = 0; j < GridD; j++)
                    if (!float.IsNaN(plan[i, j])) stands[(j + oz) * PanelW + i + ox] = 1f;
            var far = Blur(stands, PanelW, PanelL, 8);
            var pixels = new Color32[stands.Length];
            for (int k = 0; k < pixels.Length; k++)
                pixels[k] = new Color32((byte)(stands[k] * 255f), 0, (byte)(Mathf.Clamp01(far[k] * 1.3f) * 255f), 255);
            var texture = new Texture2D(PanelW, PanelL, TextureFormat.RGBA32, false)
            {
                name = "Course hologram footprint", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // Two box blurs each way: close enough to a gaussian.
        static float[] Blur(float[] source, int w, int d, int r)
        {
            var a = (float[])source.Clone();
            var b = new float[a.Length];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int z = 0; z < d; z++)
                    for (int x = 0; x < w; x++)
                    {
                        float s = 0;
                        for (int k = -r; k <= r; k++) s += a[z * w + Mathf.Clamp(x + k, 0, w - 1)];
                        b[z * w + x] = s / (2 * r + 1);
                    }
                for (int z = 0; z < d; z++)
                    for (int x = 0; x < w; x++)
                    {
                        float s = 0;
                        for (int k = -r; k <= r; k++) s += b[Mathf.Clamp(z + k, 0, d - 1) * w + x];
                        a[z * w + x] = s / (2 * r + 1);
                    }
            }
            return a;
        }

        // The route's light eases toward the share of players in; the crown turns.
        void TickRoute(float dt)
        {
            if (holoRoute == null) return;
            routeShown = Mathf.MoveTowards(routeShown, routeTarget, dt * .35f);
            holoRoute.SetFloat("_Reveal", routeShown * routeLength);
            if (routeHead != null)
            {
                routeHead.gameObject.SetActive(routeShown > .001f);
                routeHead.localPosition = RoutePoint(routeShown * routeLength);
            }
            if (crown != null) crown.localRotation = Quaternion.Euler(0, Time.unscaledTime * 18f, 0);
        }

        void DisposeCourse()
        {
            foreach (var m in new[] { holoFace, holoEdge, holoRoute, holoHead, holoMote, holoFloor }) if (m != null) Object.Destroy(m);
            foreach (var mesh in courseMeshes) if (mesh != null) Object.Destroy(mesh);
            courseMeshes.Clear();
            if (footprint != null) Object.Destroy(footprint);
        }
    }
}
