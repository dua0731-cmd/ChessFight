using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's graybox: structure v0.3
    // (Docs/PawnRush/PAWN_RUSH_ROYAL_OBSERVATORY_STRUCTURE_v0.3.md) with the obstacle plan
    // (PAWN_RUSH_ROYAL_OBSERVATORY_OBSTACLES_v0.1.md, its R721 section for what the user's first run changed).
    // Section frame: the entry's threshold is the origin, +z north (the way in), +x east, y the floor's top. A round
    // hall, its middle at (0, 64); the way passes THROUGH its set pieces one after another instead of winding round
    // the middle:
    //
    //   0 entry tunnel (z 0..14)
    //   1 the Big Dipper (y 0 -> 3.2), bending west and north. Its handle: four star platforms on single pillars
    //     joined by 3 m constellation lines; stars 2 and 3 are variable stars (StarPetal) and meteors fall on the
    //     lines (MeteorShower). Its bowl, from star 4 to star 7, is two ways (R723): the long one round the west on
    //     three crossing orbits of turning platforms, the short one straight across on one planet that rolls as it goes to and fro (R724)
    //   2 the great globe: in by the west gate (y 4), across the tilted floor inside the glass - the plane of its
    //     solar system - and out by the east gate (y 12). Two moons on the ecliptic ring cross the gates in turn;
    //     the sun lamp flares and only the planets' moving shadows shelter (SolarFlare)
    //   3 Cassiopeia: one star that stands and four that turn - a train of meshed gears, each ridden round its rim
    //     from the gear before to the gear after, meteors on the arcs ridden
    //   4 the observing stage (a rest, railed)
    //   5 the great telescope: up the deck inside its truss tube (y 12 -> 20), through four iris gates (IrisLeaf)
    //     and three focus beams (FocusBeam), out through the dome's slit
    //   6 the balcony outside the dome -> exit (9, 20, 130)
    //
    // No imported obstacle prefab is used, and all that moves is a pure function of the shared obstacle clock (the
    // meteors also of the section's seed), so the section sends nothing in a match.
    // R720 built it; R721 is after the user ran it ("too easy", the globe's crossing planets "not new", the variable
    // star "too easy", the turning platforms "better meshed like gears"): everything faster and closer together,
    // the globe and Cassiopeia built anew. Where the graybox departs from the plans' numbers it says so beside the
    // thing (the structure document lists them under "회색 상자에서 바꾼 것").
    public static class RoyalObservatoryBuilder
    {
        public const float StageTop = 12f, ExitTop = 20f, HallFloor = -18f, KillTop = -5f;
        public const int Checkpoints = 8, BeatBoxes = 6;
        public static readonly Vector3 Entry = Vector3.zero, Exit = new Vector3(9f, ExitTop, 130f);

        const float RailHeight = 1.1f, T = .3f, LineWidth = 3f, StarThickness = .6f;

        struct Star
        {
            public Vector3 at;     // the middle of its top
            public float radius;

            public Star(float x, float y, float z, float radius)
            {
                at = new Vector3(x, y, z);
                this.radius = radius;
            }
        }

        // 1: the Big Dipper, handle first (stars 1 .. 4), then the bowl (4 .. 7).
        static readonly Star[] Dipper =
        {
            new Star(-2f, 0f, 18f, 4f), new Star(-10f, .8f, 26f, 3.2f), new Star(-19f, 1.8f, 33f, 3.2f), new Star(-27f, 2.4f, 43f, 3.6f),
            new Star(-38f, 1.2f, 49f, 3.2f), new Star(-37f, 2.2f, 62f, 3.6f), new Star(-25f, 3.2f, 60f, 4f),
        };
        // Which of them are variable stars: the handle's two, half a cycle apart. (R721 to R722 the bowl's stars 5 and
        // 6 were too; R723 took those two platforms away - the bowl's long way is the orbits now.)
        static readonly int[] VariableStars = { 1, 2 };
        // R721: the core is 2 m across (R720's was 3.5 m, a place to stand out the whole cycle) and the cycle 4.5 s
        // (was 6): level 1.5 s, warning 0.6 s, the petals 0.18 s apart and 0.4 s each, up in 0.75 s.
        // R722 (the user after running R721: "땅이 접혀도 점프로 그냥 넘어갈 수 있어. 점프로는 못넘어가게 해줘" - a
        // 2.15 m hop to the core and another off it crossed a folded star): the core is a hub 0.7 m across, nothing
        // to hop by - folded, the star is a 6.3 m hole from line to line, twice a sprint's jump. With no core to stop
        // on, the petals lie level longer: 2.4 s (was 1.5), the cycle 5.4 s.
        const float CoreRadius = .35f, PetalAngle = 84f;
        const float PetalLevel = 2.4f, PetalWarn = .6f, PetalApart = .18f, PetalFold = .4f, PetalRise = .75f, PetalCycle = 5.4f;
        // The bowl (R723, the user after running R722: the short way was still the easier - "반대가 되어야 해" - and the
        // long way's variable stars and meteors were the handle's over again).
        // The long way: three orbits 7.2 m across, their middles 6 m apart so each crosses the next, three platforms
        // 2.9 m across on each, 30 deg/s (1.9 m/s).
        const float OrbitRadius = 3.6f, OrbitPlanet = 1.45f, OrbitDock = .15f, OrbitApart = 6f, OrbitSpeed = 30f;
        // The short way (R724, the user on R723's six planets spinning where they stood: "내가 말한건 이런게 아니라 공
        // 하나가 왔다갔다하는데 ... 움직임에 따라 회전하는 구체에서 중심을 잡으며 이동하는걸 말하는거야"): one planet 4.4 m
        // across going to and fro between the two stars, rolling as it goes. It stands RollDwell at a star, its
        // middle RollDock beyond that star's rim, and crosses in RollCross, speeding up and slowing over RollEase.
        const float RollRadius = 2.2f, RollDock = .6f, RollDwell = 1.2f, RollCross = 6f, RollEase = .6f;

        // 2: the great globe. Its floor is the plane through the two gates: 17 degrees, west low.
        static readonly Vector3 Globe = new Vector3(0f, 10f, 64f);
        static readonly Vector3 WestGate = new Vector3(-12.65f, 4f, 64f), EastGate = new Vector3(13.86f, 12f, 64f);
        const float GlobeRadius = 14f, EclipticRadius = 15.6f, MoonRadius = 3.4f, MoonPeriod = 12f;
        // The sun lamp's height over the floor's middle.
        const float SunHeight = 8f;

        // 3: Cassiopeia's first star, the one that stands; the other four are gears. A gear's teeth are a quarter
        // turn of a metre apart (pi / 2 m), so its radius is a quarter of its teeth, and every rim runs at the same
        // speed: 1.25 teeth a second, 1.96 m/s.
        static readonly Star CassiopeiaFirst = new Star(19.5f, StageTop, 66.5f, 3f);
        const float TeethPerSecond = 1.25f, GearGap = .08f;

        struct Gear
        {
            public Vector3 at;    // the middle of its top
            public int teeth;
            public float turn;    // +1 clockwise seen from above, -1 the other way

            public Gear(Vector3 at, int teeth, float turn)
            {
                this.at = at;
                this.teeth = teeth;
                this.turn = turn;
            }

            public float Radius => teeth * .25f;        // where it meshes
            public float Pitch => 360f / teeth;          // degrees from tooth to tooth
            public float Outer => Radius - GearGap;      // its deck's edge
            public float Width => Mathf.Min(1.8f, Radius * .45f);
            public float Middle => Outer - Width * .5f;  // the middle of its rim
        }

        // 4, 5: the stage and the telescope's deck.
        static readonly Vector3 Stage = new Vector3(9f, StageTop, 90f);
        const float StageRadius = 8f;
        static readonly Vector3 DeckFoot = new Vector3(9f, StageTop, 93f), DeckHead = new Vector3(9f, ExitTop, 118f);
        const float DeckHalf = 2.5f, TubeRadius = 3.7f;
        // Along the deck, 0 its foot .. 1 its head: the tube's seven rings, four of them irises; the three beams.
        // (R721: four irises and three beams - R720 had three and two. R722: four and two.)
        static readonly float[] TubeRings = { .04f, .2f, .4f, .5f, .6f, .8f, .96f };
        static readonly float[] Irises = { .2f, .4f, .6f, .8f };
        // R722: two beams (R721's three left no gap between gates to wait in): the stretch between the second and
        // third gates is free of them.
        static readonly float[] Beams = { .3f, .7f };
        // R721: an iris's cycle is 3.6 s (was 5): open 1.4, warning 0.6, closing 0.35, shut 0.9, opening 0.35.
        // R722 (the user on R721's tube: "여긴 너무 어려운 것 같아"): 4.4 s - open 2.0, warning 0.7, closing 0.4, shut
        // 0.9, opening 0.4 - and each gate 1.8 s after the one below it (was 1.2), a run's pace up the deck.
        const float IrisOpen = 2f, IrisWarn = .7f, IrisClose = .4f, IrisShut = .9f, IrisReopen = .4f, IrisStagger = 1.8f;
        const float LeafClear = .1f, LeafHeight = 2.4f;

        // The hall. Graybox: its wall is drawn at 50 m (the plan's 46), nothing collided with - a camera following a
        // pawn along the Dipper's far stars has to stay inside what is drawn.
        const float HallRadius = 50f, WallTop = 14f;

        public static GameObject Build(RoyalObservatorySection observatory)
        {
            var kit = observatory != null ? observatory.Kit : null;
            if (kit == null)
            {
                Debug.LogError("[PawnRush] The Royal Observatory has no course kit: nothing built.");
                return null;
            }
            var root = new GameObject(RoyalObservatorySection.ContentName);
            root.SetActive(false);
            root.transform.SetParent(observatory.transform, false);
            var b = new CourseBuilder(kit, root.transform);
            var route = new List<(Vector3 at, string beat)>();

            b.In("0 Entry tunnel", () => EntryTunnel(b, route));
            b.In("1 Big Dipper", () => BigDipper(b, route));
            b.In("2 Great globe", () => GreatGlobe(b, route));
            b.In("3 Cassiopeia", () => CassiopeiaPath(b, route));
            b.In("4 Observing stage", () => ObservingStage(b, route));
            b.In("5 Great telescope", () => Telescope(b, route));
            b.In("6 Balcony", () => Balcony(b, route));
            b.In("Hall (looks)", () => Hall(b));
            // Everything under the way is a fall: the hall's floor is 18 m down, and what falls is taken 5 m down.
            b.In("KillVolumes", () => b.Trigger<KillVolume>("Kill volume (y -5)", V(-56f, KillTop - 2f, 2f), V(56f, KillTop, 132f)));
            Path(b, route);

            var section = observatory.GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(RoyalObservatorySection.Id, RoyalObservatorySection.DisplayName, PawnRushSection.SlotKind.Obstacle,
                                 Entry, Exit, 0f, Beats());
            root.SetActive(true);
            return root;
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        // The heading of a direction: 0 north (+z), 90 east (+x).
        static float Yaw(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        // The level direction of a heading.
        static Vector3 Toward(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        // ------------------------------------------------------------------ the vocabulary

        static void Under(CourseBuilder b, Transform under, Action body)
        {
            var before = b.parent;
            b.parent = under;
            try { body(); }
            finally { b.parent = before; }
        }

        // A rail 0.3 m thick outside a floor's edge: along x at the z edge (north: beyond it), or along z at the x edge.
        static void RailX(CourseBuilder b, string name, float x0, float x1, float z, bool north, float floor) =>
            b.Rail(name, x0, x1, north ? z : z - T, north ? z + T : z, floor, RailHeight);

        static void RailZ(CourseBuilder b, string name, float x, bool east, float z0, float z1, float floor) =>
            b.Rail(name, east ? x : x - T, east ? x + T : x, z0, z1, floor, RailHeight);

        // An empty device root carrying an ObstacleMotion (its Rigidbody comes with it).
        static ObstacleMotion Mover(CourseBuilder b, string name, Vector3 at, MotionKind kind, Vector3 axis)
        {
            var root = b.Group(name);
            root.localPosition = at;
            var motion = root.gameObject.AddComponent<ObstacleMotion>();
            motion.kind = kind;
            motion.axis = axis;
            motion.useLocalTranslationAxes = false;
            return motion;
        }

        // A bar from one point to another, drawn only.
        static GameObject Bar(CourseBuilder b, string name, Vector3 from, Vector3 to, float width, float height, Material material)
        {
            var go = b.SlopeBetween(name, from, to, width, height * .5f, height * .5f, material);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // A ring of boxes round the root's y axis, drawn only: `thick` out from the middle, `depth` along the axis.
        // `keep`: which headings (degrees round the axis) get a box.
        static Transform Hoop(CourseBuilder b, string name, Vector3 center, Quaternion facing, float radius, float thick, float depth,
                              Material material, int segments, Func<float, bool> keep = null)
        {
            var root = b.Group(name);
            root.localPosition = center;
            root.localRotation = facing;
            float chord = 2f * (radius + thick * .5f) * Mathf.Tan(Mathf.PI / segments) + .02f;
            Under(b, root, () =>
            {
                for (int i = 0; i < segments; i++)
                {
                    float heading = (i + .5f) * 360f / segments;
                    if (keep != null && !keep(heading)) continue;
                    var piece = b.Group("Segment " + (i + 1));
                    piece.localRotation = Quaternion.Euler(0f, heading, 0f);
                    Under(b, piece, () => b.Box("Arc", V(-chord * .5f, -depth * .5f, radius - thick * .5f), V(chord * .5f, depth * .5f, radius + thick * .5f),
                                                material, false, false));
                }
            });
            return root;
        }

        // A prism's mesh: a convex outline (x, z) between two heights. (The course builder makes boxes, cylinders
        // and spheres only. The mesh is made here, at build time, and lives with the built content - the prefab
        // holds none of it.)
        static Mesh PrismMesh(string name, Vector2[] outline, float y0, float y1)
        {
            int n = outline.Length;
            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % n];
                area += p.x * q.y - q.x * p.y;
            }
            bool ccw = area > 0f;   // counter-clockwise seen from above (x to the right, z up the page)
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            // The top and the bottom, each a fan (Unity's front faces go clockwise).
            for (int face = 0; face < 2; face++)
            {
                int first = vertices.Count;
                bool top = face == 0;
                for (int i = 0; i < n; i++)
                {
                    vertices.Add(new Vector3(outline[i].x, top ? y1 : y0, outline[i].y));
                    uv.Add(outline[i]);
                }
                for (int i = 1; i < n - 1; i++)
                {
                    bool flip = ccw == top;
                    triangles.Add(first);
                    triangles.Add(first + (flip ? i + 1 : i));
                    triangles.Add(first + (flip ? i : i + 1));
                }
            }
            // The sides, each its own four corners (flat shading).
            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % n];
                int s = vertices.Count;
                vertices.Add(new Vector3(p.x, y1, p.y));
                vertices.Add(new Vector3(q.x, y1, q.y));
                vertices.Add(new Vector3(q.x, y0, q.y));
                vertices.Add(new Vector3(p.x, y0, p.y));
                uv.Add(new Vector2(0f, 1f));
                uv.Add(new Vector2(1f, 1f));
                uv.Add(new Vector2(1f, 0f));
                uv.Add(new Vector2(0f, 0f));
                int[] quad = ccw ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
                foreach (int corner in quad) triangles.Add(s + corner);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A prism in the current group's frame; solid, its collider is the convex mesh itself.
        static GameObject Prism(CourseBuilder b, string name, Mesh mesh, Material material, bool solid = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(b.parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (solid)
            {
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;
            }
            return go;
        }

        // A constellation line: a bridge `width` across, its top from one point to the other.
        static GameObject Line(CourseBuilder b, string name, Vector3 from, Vector3 to, float width = LineWidth)
        {
            var go = b.SlopeBetween(name, from, to, width, .4f, 0f, b.kit.boardLight);
            go.AddComponent<CourseFloor>().sloped = Mathf.Abs(to.y - from.y) > .01f;
            Bar(b, name + " mark", from + Vector3.up * .012f, to + Vector3.up * .012f, .14f, .02f, b.kit.rankGold);
            return go;
        }

        // The ends of the line between two round platforms: on each one's rim, a little inside it.
        static void Ends(Vector3 a, float insideA, Vector3 c, float insideC, out Vector3 from, out Vector3 to)
        {
            var flat = Flat(c - a).normalized;
            from = a + flat * insideA;
            to = c - flat * insideC;
        }

        // Five gold rays on a platform's top.
        static void StarMark(CourseBuilder b, string name, Vector3 at, float reach)
        {
            var mark = b.Group(name);
            mark.localPosition = at;
            Under(b, mark, () =>
            {
                for (int i = 0; i < 5; i++)
                {
                    var ray = b.Group("Ray " + (i + 1));
                    ray.localRotation = Quaternion.Euler(0f, i * 72f, 0f);
                    Under(b, ray, () => b.Box("Gold", V(-.14f, .004f, 0f), V(.14f, .02f, reach), b.kit.rankGold, false, false));
                }
            });
        }

        // The one thin pillar a platform stands on, from the hall's floor (drawn only).
        static void Pillar(CourseBuilder b, string name, Vector3 under, float diameter) =>
            b.Cylinder(name, V(under.x, HallFloor, under.z), diameter, under.y - HallFloor, b.kit.rankGold, false);

        // A star platform: a round deck with a brass rim, five gold rays and its pillar.
        static void StarPlatform(CourseBuilder b, string name, Star s)
        {
            var k = b.kit;
            b.Cylinder(name, s.at - Vector3.up * StarThickness, s.radius * 2f, StarThickness, k.boardDark).AddComponent<CourseFloor>();
            b.Cylinder(name + " brass rim", s.at - Vector3.up * (StarThickness + .15f), s.radius * 2f + .3f, StarThickness, k.rankGold, false);
            StarMark(b, name + " mark", s.at, s.radius * .78f);
            Pillar(b, name + " pillar", s.at - Vector3.up * StarThickness, .5f);
        }

        // Meteors on these lines, one about every `interval` seconds, each warned for `warning`.
        static void Shower(CourseBuilder b, string name, MeteorShower.Line[] lines, float interval, int salt, float warning) =>
            b.Group(name).gameObject.AddComponent<MeteorShower>().Configure(lines, interval, salt, warning);

        // ------------------------------------------------------------------ 0 the entry

        static void EntryTunnel(CourseBuilder b, List<(Vector3, string)> route)
        {
            b.Floor("Entry floor", -6f, 6f, 0f, 14f);
            RailZ(b, "Entry rail W", -6f, false, 0f, 14f, 0f);
            RailZ(b, "Entry rail E", 6f, true, 0f, 14f, 0f);
            // The tunnel's mouth: a tongue under the first star (a centimetre lower, so the two tops do not flicker),
            // rails either side of it.
            b.Floor("Entry threshold", -5.5f, 1.5f, 14f, 16.2f, -.01f);
            RailX(b, "Entry rail N w", -6f - T, -5.5f, 14f, true, 0f);
            RailX(b, "Entry rail N e", 1.5f, 6f + T, 14f, true, 0f);
            b.Checkpoint(0, V(0f, 0f, 6f), 0f, 10f);
            route.Add((V(0f, 0f, 0f), "0"));
            route.Add((V(0f, 0f, 14f), "0"));
        }

        // ------------------------------------------------------------------ 1 the Big Dipper

        static bool IsVariable(int star) => Array.IndexOf(VariableStars, star) >= 0;
        // How far inside a star's rim a line's end is: just inside a variable star's petals, well inside a plain deck.
        static float Reach(int star) => Dipper[star].radius - (IsVariable(star) ? .05f : .3f);

        static void BigDipper(CourseBuilder b, List<(Vector3, string)> route)
        {
            // The handle's four stars and the bowl's last one. (Stars 5 and 6 are not platforms any more: R723.)
            foreach (int i in new[] { 0, 1, 2, 3, 6 })
            {
                string name = "Dipper star " + (i + 1);
                int variable = Array.IndexOf(VariableStars, i);
                // Neighbours half a cycle apart: one's petals are down while the next one's are level.
                if (variable >= 0) VariableStar(b, name, Dipper[i], Dipper[i - 1].at, Dipper[i + 1].at, (variable & 1) * PetalCycle * .5f);
                else StarPlatform(b, name, Dipper[i]);
            }
            // The handle: three lines, star 1 to star 4.
            var lines = new MeteorShower.Line[3];
            for (int i = 0; i < lines.Length; i++)
            {
                Ends(Dipper[i].at, Reach(i), Dipper[i + 1].at, Reach(i + 1), out var from, out var to);
                Line(b, "Dipper line " + (i + 1) + "-" + (i + 2), from, to);
                lines[i] = new MeteorShower.Line(from, to);
                route.Add((Dipper[i].at, "1"));
                route.Add((from, "1"));
                route.Add((to, "1"));
            }
            route.Add((Dipper[3].at, "1"));
            // Meteors on the handle's three lines, one about every 1.2 s, warned 0.9 s (R721). None on the bowl
            // since R723: neither of its ways has a line.
            Shower(b, "Device_MeteorShower Dipper handle", lines, 1.2f, 30, .9f);

            // The bowl, star 4 to star 7: the long way and the short way (R723, the short way again R724). Both are
            // rides, so the route measured is the straight line between the two stars and the section check is
            // told it is a ride (IntendedGap).
            OrbitCrossing(b);
            RollingPlanetWay(b, route);
            route.Add((Dipper[6].at, "2"));

            // The pointer: on from the bowl's last star to the globe's west gate (its threshold is the globe's).
            var toGate = Flat(WestGate - Dipper[6].at).normalized;
            Vector3 pointerFrom = Dipper[6].at + toGate * (Dipper[6].radius - .3f);
            Vector3 pointerTo = Dipper[6].at + toGate * ((-18.3f - Dipper[6].at.x) / toGate.x);
            pointerTo.y = WestGate.y;
            Line(b, "Pointer line 7-west gate", pointerFrom, pointerTo);
            route.Add((pointerFrom, "2"));
            route.Add((pointerTo, "3"));

            // Falls go back to the star before: stars 1, 4 and 7.
            b.Checkpoint(1, Dipper[0].at, Yaw(Dipper[1].at - Dipper[0].at), Dipper[0].radius * 2f);
            b.Checkpoint(2, Dipper[3].at, Yaw(Dipper[4].at - Dipper[3].at), Dipper[3].radius * 2f);
            b.Checkpoint(3, Dipper[6].at, Yaw(WestGate - Dipper[6].at), Dipper[6].radius * 2f);
        }

        // A variable star: a hub that stays and five petals round it that fold down one after another and come up
        // together (StarPetal). R721, after the user found it too easy: the wave comes AT the runner - the petal on
        // the way out goes first and the one on the way in last.
        // R722, after the user hopped across folded stars by their 2 m cores: the hub is 0.7 m across, only what the
        // petals hinge on. A star is crossed while it is level - 6.3 m from line to line, 2.5 s at a run and 1.8 s
        // at a sprint, of the 3 s before the petal on the way out starts down (2.4 s level, 0.6 s warned) - or
        // waited for on the line before it. Folded there is nothing to cross by: the hub is 2.8 m from a line's end
        // (a sprint's jump reaches about 3.1 m, onto 0.7 m) and nobody jumps 2.8 m from a standstill on it.
        static void VariableStar(CourseBuilder b, string name, Star s, Vector3 from, Vector3 to, float phaseSeconds)
        {
            var k = b.kit;
            b.Cylinder(name + " core", s.at - Vector3.up * StarThickness, CoreRadius * 2f, StarThickness, k.boardDark).AddComponent<CourseFloor>();
            StarMark(b, name + " mark", s.at, CoreRadius * .8f);
            Pillar(b, name + " pillar", s.at - Vector3.up * StarThickness, .5f);
            var wait = b.Group(name + " (intended gap)");
            wait.localPosition = s.at;
            wait.gameObject.AddComponent<IntendedGap>().Configure(new Vector3(s.radius * 2f + 1f, 5f, s.radius * 2f + 1f), "변광성: 꽃잎이 펴졌을 때 건넌다");

            // Petal 0 lies under the line in; the others follow round the star. Each folds in the order of how near
            // it is to the line out.
            float yawIn = Yaw(from - s.at), yawOut = Yaw(to - s.at);
            var away = new float[5];
            for (int i = 0; i < 5; i++) away[i] = Mathf.Abs(Mathf.DeltaAngle(yawIn + 72f * i, yawOut));
            float hinge = CoreRadius * Mathf.Cos(36f * Mathf.Deg2Rad);
            var outline = PetalOutline(s.radius - .02f, hinge);
            Mesh deck = PrismMesh(name + " petal", outline, -.3f, 0f), light = PrismMesh(name + " petal warning", outline, .002f, .02f);
            for (int i = 0; i < 5; i++)
            {
                int order = 0;
                for (int j = 0; j < 5; j++)
                    if (away[j] < away[i] || (away[j] == away[i] && j < i)) order++;
                var facing = Quaternion.Euler(0f, yawIn + 72f * i, 0f);
                var petal = b.Group("Device_StarPetal " + name + " " + (i + 1));
                // A centimetre under the core's top: the core's deck covers the petal's inner edge.
                petal.localPosition = s.at + facing * Vector3.forward * hinge + Vector3.down * .01f;
                petal.localRotation = facing;
                GameObject warning = null;
                Under(b, petal, () =>
                {
                    Prism(b, "Petal", deck, k.boardLight).AddComponent<CourseFloor>();
                    warning = Prism(b, "Petal warning", light, k.marker, false);
                    warning.SetActive(false);
                });
                petal.gameObject.AddComponent<StarPetal>().Configure(order, PetalLevel, PetalWarn, PetalApart, PetalFold, PetalRise, PetalCycle,
                                                                    PetalAngle, warning, phaseSeconds);
            }
        }

        // A petal seen from above, its hinge along x at z 0 and the star's rim away from it (+z): a fifth of the
        // ring between the core and the rim, a hair narrower so neighbours do not touch.
        static Vector2[] PetalOutline(float radius, float hinge)
        {
            const float Half = 35.3f;
            float inner = CoreRadius * Mathf.Sin(Half * Mathf.Deg2Rad);
            var points = new List<Vector2> { new Vector2(inner, 0f) };
            for (int i = 0; i <= 4; i++)
            {
                float a = Mathf.Lerp(Half, -Half, i / 4f) * Mathf.Deg2Rad;
                points.Add(new Vector2(radius * Mathf.Sin(a), radius * Mathf.Cos(a) - hinge));
            }
            points.Add(new Vector2(-inner, 0f));
            return points.ToArray();
        }

        // R723 - the bowl's long way, the easy one: planets going round, as the shadows go round the globe's floor.
        // Three orbits in a chain round the west of the bowl, each crossing the next; on each, three round platforms
        // a third of a turn apart, 1.9 m/s. A platform of the first orbit passes star 4's rim every 4 s (0.15 m
        // off), one of the last passes star 7's. Neighbouring orbits turn against each other, so through the place
        // where two cross their platforms run the same way, and they are set to take turns there like the teeth of
        // a zip: one of this orbit, one of that, each coming within 0.1 m of the one before it and running beside it
        // for about a second - the time to step across. The way: on at star 4, round to the crossing, across, round
        // the second orbit's far side to the next crossing, across, round to star 7. About 490 degrees of riding,
        // 16 s, against the 9.5 m straight across on the rolling planet.
        // All three lie in one plane tilted 3 degrees, star 4's height (2.4) at its dock and star 7's (3.2) at its.
        // Nothing falls on it and nothing folds: what there is to it is waiting for a platform and not stepping off
        // into the gap.
        static void OrbitCrossing(CourseBuilder b)
        {
            var k = b.kit;
            Star s4 = Dipper[3], s7 = Dipper[6];
            // Which way each star's dock lies, and where a platform's middle is as it passes: the star's rim, the
            // gap, its own radius.
            Vector3 out4 = new Vector3(-.92f, 0f, .39f).normalized, out7 = new Vector3(-.96f, 0f, -.28f).normalized;
            Vector3 dock4 = s4.at + out4 * (s4.radius + OrbitDock + OrbitPlanet), dock7 = s7.at + out7 * (s7.radius + OrbitDock + OrbitPlanet);
            var rise = Flat(dock7 - dock4);
            float slope = (dock7.y - dock4.y) / rise.magnitude;
            var uphill = rise.normalized;
            var tilt = Quaternion.FromToRotation(Vector3.up, (Vector3.up - uphill * slope).normalized);
            Func<Vector3, Vector3> onPlane = at => V(at.x, dock4.y + slope * Vector3.Dot(Flat(at - dock4), uphill), at.z);
            Vector3 first = onPlane(dock4 + out4 * OrbitRadius), last = onPlane(dock7 + out7 * OrbitRadius);
            var centres = new[] { first, onPlane(Touching(first, OrbitApart, last, OrbitApart, Vector3.left)), last };
            float[] turn = { -1f, 1f, -1f };
            float heading = 0f;
            for (int i = 0; i < centres.Length; i++)
            {
                if (i > 0)
                {
                    // When a platform of the orbit before is at the crossing, the nearest of this one's is a sixth
                    // of a turn short of it: the two orbits' platforms pass it by turns, 2 s apart.
                    Vector3 before = centres[i - 1], crossing = Touching(before, OrbitRadius, centres[i], OrbitRadius, Vector3.forward);
                    heading = Yaw(crossing - centres[i]) - turn[i] * 60f + Yaw(crossing - before) - heading;
                }
                var orbit = Mover(b, "Device_OrbitRing " + (i + 1), centres[i], MotionKind.Rotate, Vector3.up);
                orbit.transform.localRotation = tilt * Quaternion.Euler(0f, heading, 0f);
                orbit.degreesPerSecond = turn[i] * OrbitSpeed;
                Under(b, orbit.transform, () =>
                {
                    for (int n = 0; n < 3; n++)
                    {
                        var at = Toward(n * 120f) * OrbitRadius;
                        b.Cylinder("Planet deck " + (n + 1), at + Vector3.down * .4f, OrbitPlanet * 2f, .4f, k.boardDark).AddComponent<CourseFloor>();
                        b.Cylinder("Planet brass rim " + (n + 1), at + Vector3.down * .55f, OrbitPlanet * 2f + .25f, .4f, k.rankGold, false);
                        b.Sphere("Planet body " + (n + 1), at + Vector3.down * 1.05f, 2.2f, k.hazard, false);
                        StarMark(b, "Planet mark " + (n + 1), at, OrbitPlanet * .7f);
                        var arm = b.Group("Arm " + (n + 1));
                        arm.localRotation = Quaternion.Euler(0f, n * 120f, 0f);
                        Under(b, arm, () => b.Box("Rod", V(-.08f, -.92f, .6f), V(.08f, -.76f, OrbitRadius - .9f), k.rankGold, false, false));
                    }
                    b.Sphere("Orbit sun", V(0f, -.7f, 0f), 1.5f, k.rankGold, false);
                });
                Hoop(b, "Orbit track " + (i + 1), centres[i] + Vector3.down * .5f, tilt, OrbitRadius, .12f, .06f, k.rankGold, 36);
                Pillar(b, "Orbit pillar " + (i + 1), centres[i] + Vector3.down * 1.3f, .5f);
            }
        }

        // R724 - the bowl's short way, the hard one: ONE planet, a ball 4.4 m across, going to and fro between star 4
        // and star 7 and rolling as it goes, like a ball rolled along the ground - its top runs the way it travels.
        // A pawn rides its top as a log is ridden on water. Standing still it is carried forward off the crest (the
        // top runs on at 1.5 m/s over the ball's own 1.5 m/s), so the rider walks BACK against the roll, about
        // 2.2 m/s to stay where it is (a run is 2.5), for the 6 s of the crossing - and the crest is level for under
        // a metre any way before it falls away. Coming back the ball rolls the other way.
        // It stands 1.2 s at each star (still: the time to step on or off), its top 5 cm over the star's deck and
        // 0.6 m beyond its rim; 14.4 s there and back. A miss is the fall, back to star 4, and a wait for it.
        // (R723's six planets spinning where they stood were a ridge to run along - not what was meant.)
        static void RollingPlanetWay(CourseBuilder b, List<(Vector3, string)> route)
        {
            var k = b.kit;
            Star s4 = Dipper[3], s7 = Dipper[6];
            var flat = Flat(s7.at - s4.at).normalized;
            Vector3 lift = Vector3.up * (.05f - RollRadius);
            Vector3 near = s4.at + flat * (s4.radius + RollDock) + lift, far = s7.at - flat * (s7.radius + RollDock) + lift;
            var planet = b.Group("Device_RollingPlanet");
            planet.localPosition = near;
            Under(b, planet, () =>
            {
                var ball = b.Sphere("Planet", Vector3.zero, RollRadius * 2f, k.hazard);
                ball.AddComponent<CourseFloor>().sloped = true;
                ball.AddComponent<NoClimbSurface>();
                // What shows it rolling: two bands round it that tumble as it turns, and a spot.
                Hoop(b, "Band a", Vector3.zero, Quaternion.identity, RollRadius + .01f, .06f, .45f, k.boardLight, 20);
                Hoop(b, "Band b", Vector3.zero, Quaternion.Euler(90f, 0f, 0f), RollRadius + .01f, .06f, .45f, k.boardLight, 20);
                b.Sphere("Spot", new Vector3(.5f, .7f, .5f).normalized * (RollRadius - .15f), 1f, k.rankGold, false);
            });
            planet.gameObject.AddComponent<RollingPlanet>().Configure(far - near, RollRadius, RollDwell, RollCross, RollEase, 0f);

            // The rails it rolls on (drawn only), from under the one star's rim to under the other's.
            var side = Vector3.Cross(Vector3.up, flat) * .8f;
            Vector3 under = Vector3.down * (RollRadius + .1f), railFrom = near - flat * 1.2f + under, railTo = far + flat * 1.2f + under;
            foreach (float way in new[] { -1f, 1f })
                Bar(b, "Planet rail", railFrom + side * way, railTo + side * way, .16f, .16f, k.rankGold);
            for (int i = 0; i <= 2; i++)
            {
                var at = Vector3.Lerp(railFrom, railTo, i * .5f);
                Bar(b, "Planet rail tie " + (i + 1), at - side, at + side, .16f, .12f, k.rankGold);
                Pillar(b, "Planet rail post " + (i + 1), at + Vector3.down * .1f, .35f);
            }

            // The way measured: rim to rim, across the ride.
            Vector3 from = s4.at + flat * s4.radius, to = s7.at - flat * s7.radius;
            route.Add((from, "2"));
            route.Add((to, "2"));
            var ride = b.Group("Rolling planet ride (intended gap)");
            ride.localPosition = (from + to) * .5f;
            ride.localRotation = Quaternion.LookRotation(flat, Vector3.up);
            ride.gameObject.AddComponent<IntendedGap>().Configure(new Vector3(6f, 8f, Vector3.Distance(from, to) + 1f), "구르는 행성: 타고 건넌다");
        }

        // ------------------------------------------------------------------ 2 the great globe

        // The floor inside the globe: the plane through the two gates, level along z.
        static Quaternion FloorFacing => Quaternion.Euler(0f, 0f, Mathf.Atan2(EastGate.y - WestGate.y, EastGate.x - WestGate.x) * Mathf.Rad2Deg);
        static Vector3 FloorNormal => FloorFacing * Vector3.up;
        // How far under the globe's middle that plane passes, and the point of it nearest the middle.
        static float FloorDepth => Vector3.Dot(Globe - WestGate, FloorNormal);
        static Vector3 FloorMiddle => Globe - FloorNormal * FloorDepth;
        static Vector3 SunLamp => FloorMiddle + FloorNormal * SunHeight;

        // A glass sphere 28 m across in the hall's middle (drawn only - glass does not hold a pawn, and a camera
        // sees through it). A threshold outside each gate, 7 m wide, for the moons' sweep.
        //
        // R721 - inside, in place of the straight climb and the planets that crossed it: the whole section of the
        // globe at the gates' plane is a floor, a tilted disc 27.8 m across - the plane of the solar system, the
        // ecliptic ring outside continuing it. The sun lamp hangs 8 m over its middle and seven planets turn round
        // the lamp on three orbits, over the heads. Every 5 s the lamp flares (calm 3.2 s, warning 1.3 s, burn
        // 0.5 s), and who is on the floor outside a planet's shadow in the burn is thrown down the slope, toward the
        // west gate or off the floor's edge (SolarFlare). The shadows go round the floor at 3 to 3.3 m/s - faster
        // than a run, slower than a sprint - on three tracks drawn in gold; a crossing passes each track twice.
        static void GreatGlobe(CourseBuilder b, List<(Vector3, string)> route)
        {
            var k = b.kit;
            var facing = FloorFacing;
            Vector3 middle = FloorMiddle;
            float floorRadius = Mathf.Sqrt(GlobeRadius * GlobeRadius - FloorDepth * FloorDepth) + .1f;
            b.Floor("West gate threshold", -18.5f, -12.4f, 60.5f, 67.5f, WestGate.y);
            b.Floor("East gate threshold", 13.6f, 17.2f, 60.5f, 67.5f, EastGate.y - .01f);
            // Outside the flare's reach and west of the moons' track: who is thrown off the floor starts again here.
            b.Checkpoint(4, V(-17.3f, WestGate.y, Globe.z), 90f, 7f);
            route.Add((WestGate, "3"));
            route.Add((EastGate, "3"));

            // Orbit radius and height over the floor, the planet's radius, how many share the orbit, seconds a turn
            // (negative: the other way). A shadow is as much bigger and farther out as the floor is farther from the
            // lamp than the planet: tracks 10.4, 6.8 and 3.3 m from the floor's middle, shadows 1.9, 1.85 and 1.7 m.
            var orbits = new (float radius, float height, float body, int count, float period)[]
            {
                (6f, 3.4f, 1.1f, 3, 20f), (3.5f, 3.9f, .95f, 2, -13f), (1.45f, 4.5f, .75f, 2, 7f),
            };
            var floor = b.Group("Device_SolarFlare");
            floor.localPosition = middle;
            floor.localRotation = facing;
            var planets = new List<Transform>();
            var sizes = new List<float>();
            Transform lamp = null;
            Under(b, floor, () =>
            {
                b.Cylinder("Ecliptic floor", V(0f, -.5f, 0f), floorRadius * 2f, .5f, k.boardLight).AddComponent<CourseFloor>().sloped = true;
                lamp = b.Sphere("Sun lamp", V(0f, SunHeight, 0f), 4.4f, k.rankGold, false).transform;
                for (int i = 0; i < orbits.Length; i++)
                {
                    var o = orbits[i];
                    var orbit = Mover(b, "Device_PlanetOrbit " + (i + 1), V(0f, o.height, 0f), MotionKind.Rotate, Vector3.up);
                    orbit.degreesPerSecond = 360f / o.period;
                    orbit.phase = i * .31f;
                    orbit.shove = false;
                    Under(b, orbit.transform, () =>
                    {
                        for (int p = 0; p < o.count; p++)
                        {
                            // Over the heads, and not collided with: what matters of a planet is its shadow.
                            var planet = b.Sphere("Planet " + (p + 1), Toward(p * 360f / o.count) * o.radius, o.body * 2f, k.hazard, false);
                            planets.Add(planet.transform);
                            sizes.Add(o.body);
                        }
                    });
                    Hoop(b, "Planet orbit " + (i + 1), V(0f, o.height, 0f), Quaternion.identity, o.radius, .08f, .08f, k.trimWhite, 24);
                    Hoop(b, "Shadow track " + (i + 1), V(0f, .012f, 0f), Quaternion.identity, o.radius * SunHeight / (SunHeight - o.height), .14f, .02f,
                         k.rankGold, 36);
                }
            });
            floor.gameObject.AddComponent<SolarFlare>().Configure(floorRadius - .35f, V(0f, SunHeight, 0f), 3.2f, 1.3f, .5f, 0f,
                                                                  planets.ToArray(), sizes.ToArray(), lamp);

            // The eclipse gates: two moons, discs 6.8 m across, ride the ecliptic ring opposite each other, once
            // round in 12 s (30 deg/s) - a gate is covered every 6 s, about a second and a half each time (R721: a
            // second moon; R720's one covered a gate every 12 s). The ring lies in the floor's plane, so a moon
            // crosses the west threshold low and the east one high. They are thin: what one meets on a threshold it
            // turns aside along the way, toward the gate or back.
            var moons = Mover(b, "Device_EclipseMoon", middle, MotionKind.Rotate, Vector3.up);
            moons.transform.localRotation = facing;
            moons.degreesPerSecond = 360f / MoonPeriod;
            Under(b, moons.transform, () =>
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var disc = b.Cylinder(side < 0 ? "Moon A" : "Moon B", V(side * EclipticRadius, -.2f, 0f), MoonRadius * 2f, .4f, k.boardLight, true, true);
                    disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // its faces look along the ring's radius
                }
            });

            // What the globe looks like (drawn only): the glass, its three rings, the stepped base, the two gates'
            // brass frames.
            b.Sphere("Great globe glass", Globe, GlobeRadius * 2f, k.glass != null ? k.glass : k.boardLight, false);
            Hoop(b, "Meridian ring", Globe, Quaternion.Euler(0f, 0f, -90f), 16.5f, .5f, .5f, k.rankGold, 36);
            Hoop(b, "Horizon ring", Globe, Quaternion.identity, 18f, 2f, .3f, k.rankGold, 36);
            Hoop(b, "Ecliptic ring", middle, facing, EclipticRadius, .35f, .35f, k.rankGold, 36);
            float[] radii = { 13f, 10.5f, 7f, 4.5f }, tops = { -14f, -10f, -6.5f, Globe.y - GlobeRadius };
            float bottom = HallFloor;
            for (int i = 0; i < radii.Length; i++)
            {
                b.Cylinder("Globe base " + (i + 1), V(Globe.x, bottom, Globe.z), radii[i] * 2f, tops[i] - bottom, (i & 1) == 0 ? k.trimBlack : k.trimWhite, false);
                bottom = tops[i];
            }
            var across = Quaternion.Euler(0f, 0f, -90f);
            Hoop(b, "West gate frame", WestGate + V(-.5f, 1.9f, 0f), across, 3.8f, .35f, .5f, k.rankGold, 16);
            Hoop(b, "East gate frame", EastGate + V(.5f, 1.9f, 0f), across, 3.8f, .35f, .5f, k.rankGold, 16);
        }

        // ------------------------------------------------------------------ 3 Cassiopeia

        // The four gears, south to north: each touches the one before (the first touches the star that stands, the
        // last the stage), and they zigzag, so the W is the star and the four of them.
        static Gear[] Gears()
        {
            Vector3 northEast = new Vector3(.6f, 0f, .8f), northWest = new Vector3(-.6f, 0f, .8f);
            var first = new Gear(CassiopeiaFirst.at + northEast * (CassiopeiaFirst.radius + 4f), 16, 1f);
            var second = new Gear(first.at + northWest * (first.Radius + 5f), 20, -1f);
            var third = new Gear(second.at + northEast * (second.Radius + 4f), 16, 1f);
            var last = new Gear(Touching(third.at, third.Radius + 3f, Stage, 3f + StageRadius, Vector3.forward), 12, -1f);
            return new[] { first, second, third, last };
        }

        // The point `fromA` from a and `fromB` from b (level distances), on the side of the line between them that
        // `toward` points to.
        static Vector3 Touching(Vector3 a, float fromA, Vector3 b, float fromB, Vector3 toward)
        {
            var ab = Flat(b - a);
            float d = ab.magnitude;
            var u = ab / d;
            float along = (fromA * fromA - fromB * fromB + d * d) / (2f * d);
            float off = Mathf.Sqrt(Mathf.Max(0f, fromA * fromA - along * along));
            var side = new Vector3(-u.z, 0f, u.x);
            if (Vector3.Dot(side, toward) < 0f) side = -side;
            return V(a.x, a.y, a.z) + u * along + side * off;
        }

        // R721, after the user: "the turning platforms are good - meshed like gears they would be more fun." In
        // place of R720's four stars and binary: one star that stands (the checkpoint) and a train of four gears,
        // 8, 10, 8 and 6 m across, each turning against its neighbours and all rims at one speed (1.96 m/s), so where
        // two gears meet their rims run side by side and a pawn steps across as if they stood still. A gear is a
        // rim 1.35 to 1.8 m wide round a hole, four spokes and a hub. The way: on at the mesh with the gear before,
        // ride the rim round, off at the mesh with the gear after. The zigzag puts the three first gears' ways on the
        // side they turn toward (106 to 109 degrees); the last gear turns the long way round (248 degrees). Who misses
        // the way off goes round again; through the hole is a fall, back to the star.
        // Meteors land on the arcs ridden (one about every 0.9 s over five places), and a gear turns a pawn into a
        // warning ring as it would not walk into one.
        // Graybox: the cogs are a brass band under the deck, drawn only - teeth to stand on would close on a pawn
        // where two gears mesh. They are set so the two bands interleave.
        static void CassiopeiaPath(CourseBuilder b, List<(Vector3, string)> route)
        {
            var k = b.kit;
            StarPlatform(b, "Cassiopeia star 1", CassiopeiaFirst);
            var gears = Gears();
            b.Checkpoint(5, CassiopeiaFirst.at, Yaw(gears[0].at - CassiopeiaFirst.at), CassiopeiaFirst.radius * 2f);
            route.Add((CassiopeiaFirst.at, "4"));

            var zones = new List<MeteorShower.Line>();
            float yaw = 0f;
            for (int i = 0; i < gears.Length; i++)
            {
                var g = gears[i];
                Vector3 before = i == 0 ? CassiopeiaFirst.at : gears[i - 1].at, after = i + 1 < gears.Length ? gears[i + 1].at : Stage;
                if (i > 0)
                {
                    // Set so that where this gear meets the one before it, a cog of the one comes between two of the
                    // other: both turn at the same number of cogs a second, so it stays so.
                    var p = gears[i - 1];
                    float mesh = Yaw(g.at - p.at);
                    yaw = mesh + 180f + g.Pitch * (.5f - (yaw - mesh) / p.Pitch);
                }
                StarGear(b, "Cassiopeia gear " + (i + 2), g, yaw);

                // The arc ridden: from the mesh with the one before, the way the gear turns, to the mesh with the next.
                float yawIn = Yaw(before - g.at), yawOut = Yaw(after - g.at);
                float arc = Mathf.Repeat(g.turn * (yawOut - yawIn), 360f);
                route.Add((g.at + Toward(yawIn) * (g.Outer - .1f), "4"));
                int steps = Mathf.CeilToInt(arc / 20f);
                for (int s = 0; s <= steps; s++) route.Add((g.at + Toward(yawIn + g.turn * arc * s / steps) * g.Middle, "4"));
                route.Add((g.at + Toward(yawOut) * (g.Outer - .1f), "4"));
                // Where meteors land on it: the middle of a short arc, a third and two thirds of a long one.
                int places = arc > 180f ? 2 : 1;
                for (int z = 1; z <= places; z++)
                {
                    float at = yawIn + g.turn * arc * z / (places + 1);
                    Vector3 point = g.at + Toward(at) * g.Middle, along = Toward(at + 90f) * 2.5f;
                    zones.Add(new MeteorShower.Line(point - along, point + along));
                }
            }
            Shower(b, "Device_MeteorShower Cassiopeia", zones.ToArray(), .9f, 32, .8f);
            route.Add((Stage + Flat(gears[gears.Length - 1].at - Stage).normalized * (StageRadius - .5f), "5"));
        }

        // One gear: its deck's rim as one piece a cog, the brass cogs under them, a hub and four spokes, turning on
        // its axle. `yaw`: where its first cog points when the shared clock reads nought.
        static void StarGear(CourseBuilder b, string name, Gear g, float yaw)
        {
            var k = b.kit;
            float inner = g.Outer - g.Width, half = g.Pitch * .5f * Mathf.Deg2Rad, cog = Mathf.PI * .5f * .42f;
            float sin = Mathf.Sin(half), cos = Mathf.Cos(half);
            var piece = PrismMesh(name + " rim", new[]
            {
                new Vector2(inner * sin, inner * cos), new Vector2(g.Outer * sin, g.Outer * cos),
                new Vector2(-g.Outer * sin, g.Outer * cos), new Vector2(-inner * sin, inner * cos),
            }, -.5f, 0f);
            var gear = Mover(b, "Device_StarGear " + name, g.at, MotionKind.Rotate, Vector3.up);
            gear.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            gear.degreesPerSecond = g.turn * g.Pitch * TeethPerSecond;
            Under(b, gear.transform, () =>
            {
                for (int i = 0; i < g.teeth; i++)
                {
                    var tooth = b.Group("Tooth " + (i + 1));
                    tooth.localRotation = Quaternion.Euler(0f, i * g.Pitch, 0f);
                    bool dark = (i & 1) == 0;
                    Under(b, tooth, () =>
                    {
                        // Dark and light by turns, so its turning is seen.
                        Prism(b, "Rim", piece, dark ? k.boardDark : k.boardLight).AddComponent<CourseFloor>();
                        b.Box("Cog", V(-cog * .5f, -.95f, g.Radius - .4f), V(cog * .5f, -.3f, g.Radius + .3f), k.rankGold, false, false);
                    });
                }
                b.Cylinder("Hub", V(0f, -.5f, 0f), 1.8f, .5f, k.rankGold).AddComponent<CourseFloor>();
                for (int s = 0; s < 4; s++)
                {
                    var spoke = b.Group("Spoke " + (s + 1));
                    spoke.localRotation = Quaternion.Euler(0f, s * 90f + g.Pitch * .5f, 0f);
                    Under(b, spoke, () => b.Box("Beam", V(-.45f, -.4f, .7f), V(.45f, 0f, inner + .1f), k.boardDark).AddComponent<CourseFloor>());
                }
            });
            Pillar(b, name + " axle", g.at + Vector3.down * .5f, .6f);
        }

        // ------------------------------------------------------------------ 4 the observing stage

        // A round deck 16 m across at y 12 on one thick pillar: the rest before the telescope. Railed all round but
        // where the last gear meets it and where the tube's deck leaves; the fork mount's two arms stand either side
        // of the deck's foot and close the rim there.
        static void ObservingStage(CourseBuilder b, List<(Vector3, string)> route)
        {
            var k = b.kit;
            b.Cylinder("Observing stage", Stage - Vector3.up * .8f, StageRadius * 2f, .8f, k.boardDark).AddComponent<CourseFloor>();
            b.Cylinder("Observing stage brass rim", Stage - Vector3.up * 1f, StageRadius * 2f + .4f, .8f, k.rankGold, false);
            b.Cylinder("Sun mark", Stage + Vector3.up * .004f, 3f, .016f, k.rankGold, false);
            var rays = b.Group("Sun rays");
            rays.localPosition = Stage;
            Under(b, rays, () =>
            {
                for (int i = 0; i < 12; i++)
                {
                    var ray = b.Group("Ray " + (i + 1));
                    ray.localRotation = Quaternion.Euler(0f, i * 30f, 0f);
                    Under(b, ray, () => b.Box("Gold", V(-.12f, .004f, 1.9f), V(.12f, .02f, StageRadius * .82f), k.rankGold, false, false));
                }
            });
            Pillar(b, "Observing stage pillar", Stage - Vector3.up * .8f, 2.4f);

            const int Segments = 36;
            var gears = Gears();
            float chord = 2f * StageRadius * Mathf.Tan(Mathf.PI / Segments) + .02f, toGear = Yaw(gears[gears.Length - 1].at - Stage);
            var rails = b.Group("Stage rails");
            rails.localPosition = Stage;
            Under(b, rails, () =>
            {
                for (int i = 0; i < Segments; i++)
                {
                    float heading = (i + .5f) * 360f / Segments;
                    if (Mathf.Abs(Mathf.DeltaAngle(heading, 0f)) < 31f || Mathf.Abs(Mathf.DeltaAngle(heading, toGear)) < 17f) continue;
                    var piece = b.Group("Segment " + (i + 1));
                    piece.localRotation = Quaternion.Euler(0f, heading, 0f);
                    Under(b, piece, () => b.Wall("Stage rail", -chord * .5f, chord * .5f, 0f, RailHeight, StageRadius - T, StageRadius, false));
                }
            });
            b.Wall("Fork arm W", Stage.x - 4.1f, Stage.x - DeckHalf - .1f, StageTop - .8f, StageTop + 9.5f, 95.4f, 97.4f, false);
            b.Wall("Fork arm E", Stage.x + DeckHalf + .1f, Stage.x + 4.1f, StageTop - .8f, StageTop + 9.5f, 95.4f, 97.4f, false);

            b.Checkpoint(6, V(Stage.x, StageTop, 88f), 0f, 12f);
            route.Add((Stage, "5"));
        }

        // ------------------------------------------------------------------ 5 the great telescope

        static Vector3 DeckAlong => (DeckHead - DeckFoot).normalized;
        static Vector3 DeckNormal => Vector3.Cross(DeckAlong, Vector3.right);
        static Vector3 DeckPoint(float f) => Vector3.LerpUnclamped(DeckFoot, DeckHead, f);

        // A deck 5 m wide and 26 m long, 18 degrees, from the stage (y 12) up through the dome's slit to the balcony
        // (y 20), inside a truss tube of seven rings (drawn only: its sides are open, and off the deck is a fall).
        // R721 - closer and quicker:
        //   four iris gates at 0.2, 0.4, 0.6 and 0.8 of the way (5.25 m apart): each two leaves closing from left
        //   and right, 3.6 s a cycle, each 1.2 s later than the one below it, so the opening runs up the tube - a
        //   sprint (1.5 s a gate) rides it through all four, a run (2.1 s) through three
        //   three focus beams between them, at 0.3, 0.5 and 0.7: a hot spot 3 m across wandering over the deck
        //   between two irises - the place a runner waits
        // R722 - eased, the user found R721's too hard:
        //   the four gates 4.4 s a cycle and open 2 s of it, each 1.8 s after the one below - a run (2.1 s a gate)
        //   rides the opening through all four, a sprint waits a moment at each
        //   two beams, at 0.3 and 0.7, a spot 2.4 m across (half the deck is always clear of it) at R720's pace;
        //   no beam between the second and third gates
        static void Telescope(CourseBuilder b, List<(Vector3, string)> route)
        {
            var k = b.kit;
            Vector3 along = DeckAlong, normal = DeckNormal;
            var facing = Quaternion.LookRotation(along, normal);
            b.Ramp("Telescope deck", DeckFoot.x - DeckHalf, DeckFoot.x + DeckHalf, DeckFoot.z, DeckHead.z, DeckFoot.y, DeckHead.y, .4f);
            route.Add((DeckFoot, "5"));
            route.Add((DeckHead, "5"));

            float cycle = IrisOpen + IrisWarn + IrisClose + IrisShut + IrisReopen;
            for (int i = 0; i < Irises.Length; i++) Iris(b, i + 1, Irises[i], Mathf.Repeat(-IrisStagger * i, cycle));

            // The light comes from the objective, the tube's top ring.
            float axis = Mathf.Sqrt(TubeRadius * TubeRadius - DeckHalf * DeckHalf);
            Vector3 objective = DeckPoint(TubeRings[TubeRings.Length - 1]) + normal * axis;
            for (int i = 0; i < Beams.Length; i++)
            {
                var beam = b.Group("Device_FocusBeam " + (i + 1));
                beam.localPosition = DeckPoint(Beams[i]);
                beam.localRotation = facing;
                // R721: a spot 3 m across (was 2.6), across the deck in 2.3 s and along it in 3.7 s (were 3.1 and 4.7).
                // R722: 2.4 m across, 1.3 m either side of the deck's middle, back to 3.1 s and 4.7 s.
                beam.gameObject.AddComponent<FocusBeam>().Configure(1.2f, 1.3f, 1.2f, 3.1f, 4.7f, 1.3f * i,
                    Quaternion.Inverse(facing) * (objective - DeckPoint(Beams[i])),
                    Arena.Glow(new Color(1f, .82f, .35f, .8f)), Arena.Glow(new Color(1f, .9f, .6f, .16f)));
            }

            // The tube (drawn only): its rings round the axis, the deck a chord of them; four struts, two over the
            // heads and two under the deck, so nothing drawn stands where a rail would; the finder scope on top.
            var round = Quaternion.FromToRotation(Vector3.up, along);
            for (int i = 0; i < TubeRings.Length; i++)
            {
                bool top = i == TubeRings.Length - 1, iris = Array.IndexOf(Irises, TubeRings[i]) >= 0;
                Hoop(b, (top ? "Objective ring" : iris ? "Iris ring " : "Tube ring ") + (top ? "" : (i + 1).ToString()),
                     DeckPoint(TubeRings[i]) + normal * axis, round, top ? TubeRadius + .5f : TubeRadius, top ? .7f : .35f, top ? .7f : .35f,
                     iris ? k.hazard : k.rankGold, 16);
            }
            foreach (float degrees in new[] { 45f, 135f, 250f, 290f })
            {
                float a = degrees * Mathf.Deg2Rad;
                var offset = normal * axis + (Vector3.right * Mathf.Cos(a) + normal * Mathf.Sin(a)) * TubeRadius;
                Bar(b, "Tube strut " + degrees, DeckPoint(TubeRings[0]) + offset, DeckPoint(TubeRings[TubeRings.Length - 1]) + offset, .2f, .2f, k.rankGold);
            }
            var above = normal * (axis + TubeRadius + .8f);
            Bar(b, "Finder scope", DeckPoint(.3f) + above, DeckPoint(.64f) + above, .9f, .9f, k.boardDark);
        }

        // The leaf seen from above its own frame: it slides along +z, its leading edge a prow.
        static readonly Vector2[] LeafOutline =
        {
            new Vector2(-.25f, -2.7f), new Vector2(.25f, -2.7f), new Vector2(.25f, -.3f), new Vector2(0f, 0f), new Vector2(-.25f, -.3f),
        };

        // One iris gate: two leaves 2.7 m long standing on the deck, waiting just outside its edges and meeting on
        // its middle line. Graybox: the plan's eight blades are for the art; what is collided with is these two.
        static void Iris(CourseBuilder b, int number, float f, float phaseSeconds)
        {
            var k = b.kit;
            Vector3 normal = DeckNormal, at = DeckPoint(f);
            var mark = b.Group("Iris " + number + " warning");
            mark.localPosition = at;
            mark.localRotation = Quaternion.LookRotation(DeckAlong, normal);
            Under(b, mark, () => b.Box("Mark", V(-DeckHalf, .008f, -.4f), V(DeckHalf, .03f, .4f), k.marker, false, false));
            var shape = PrismMesh("Iris " + number + " leaf", LeafOutline, .05f, LeafHeight);
            for (int side = -1; side <= 1; side += 2)
            {
                var leaf = b.Group("Device_IrisLeaf " + number + (side < 0 ? " W" : " E"));
                leaf.localPosition = at + Vector3.right * (side * (DeckHalf + LeafClear));
                leaf.localRotation = Quaternion.LookRotation(Vector3.right * -side, normal);   // toward the deck's middle
                Under(b, leaf, () => Prism(b, "Leaf", shape, k.hazard).AddComponent<NoClimbSurface>());
                leaf.gameObject.AddComponent<IrisLeaf>().Configure(DeckHalf + LeafClear - .02f, IrisOpen, IrisWarn, IrisClose, IrisShut, IrisReopen,
                                                                  side < 0 ? mark.gameObject : null, phaseSeconds);
            }
        }

        // ------------------------------------------------------------------ 6 the balcony

        // Outside the dome: 12 x 12 m at y 20, railed west and east, nothing on it - a clear run to the exit.
        static void Balcony(CourseBuilder b, List<(Vector3, string)> route)
        {
            float x0 = Exit.x - 6f, x1 = Exit.x + 6f, z0 = DeckHead.z;
            b.Floor("Balcony", x0, x1, z0, Exit.z, ExitTop);
            RailZ(b, "Balcony rail W", x0, false, z0, Exit.z, ExitTop);
            RailZ(b, "Balcony rail E", x1, true, z0, Exit.z, ExitTop);
            RailX(b, "Balcony rail S w", x0 - T, Exit.x - DeckHalf, z0, false, ExitTop);
            RailX(b, "Balcony rail S e", Exit.x + DeckHalf, x1 + T, z0, false, ExitTop);
            b.Checkpoint(7, V(Exit.x, ExitTop, 121f), 0f, 10f);
            // The arch's posts (looks): 12 m between them, 6 m high.
            b.Box("Exit arch post W", V(x0 - .8f, ExitTop, Exit.z - .6f), V(x0, ExitTop + 6f, Exit.z), b.kit.trimWhite, false, false);
            b.Box("Exit arch post E", V(x1, ExitTop, Exit.z - .6f), V(x1 + .8f, ExitTop + 6f, Exit.z), b.kit.trimWhite, false, false);
            b.Box("Exit arch lintel", V(x0 - .8f, ExitTop + 6f, Exit.z - .6f), V(x1 + .8f, ExitTop + 6.8f, Exit.z), b.kit.trimWhite, false, false);
            route.Add((Exit, "출구"));
        }

        // ------------------------------------------------------------------ the hall round it (looks only)

        // The size of the place, nothing collided with: the round floor 18 m under the way, the wall to 14 m over it
        // (open at the tunnel), and the dome over that as ribs, a hoop and a crown - open between the two ribs the
        // telescope passes, the slit.
        static void Hall(CourseBuilder b)
        {
            var k = b.kit;
            var middle = V(Globe.x, 0f, Globe.z);
            b.Cylinder("Hall floor", middle + Vector3.up * (HallFloor - 1f), HallRadius * 2f, 1f, k.trimBlack, false);
            Hoop(b, "Hall wall", middle + Vector3.up * ((HallFloor + WallTop) * .5f), Quaternion.identity, HallRadius + .25f, .5f, WallTop - HallFloor,
                 k.trimWhite, 36, heading => Mathf.Abs(Mathf.DeltaAngle(heading, 180f)) > 10.5f);
            Hoop(b, "Hall wall coping", middle + Vector3.up * WallTop, Quaternion.identity, HallRadius + .25f, 1f, .6f, k.rankGold, 36);
            // Where the wall is left open for the tunnel: its jambs and the wall over it.
            b.Box("Hall wall S w", V(-8.9f, HallFloor, 14f), V(-6.3f, WallTop, 14.5f), k.trimWhite, false, false);
            b.Box("Hall wall S e", V(6.3f, HallFloor, 14f), V(8.9f, WallTop, 14.5f), k.trimWhite, false, false);
            b.Box("Hall wall S over the tunnel", V(-6.3f, 6f, 14f), V(6.3f, WallTop, 14.5f), k.trimWhite, false, false);

            var crown = middle + Vector3.up * WallTop;
            float slit = Yaw(V(DeckHead.x, 0f, 113f) - middle);
            Func<float, float, Vector3> onDome = (heading, elevation) =>
                crown + Quaternion.Euler(0f, heading, 0f) * new Vector3(0f, Mathf.Sin(elevation * Mathf.Deg2Rad), Mathf.Cos(elevation * Mathf.Deg2Rad)) * HallRadius;
            for (int rib = 0; rib < 12; rib++)
            {
                float heading = slit - 15f + rib * 30f;
                for (int step = 0; step < 7; step++)
                    Bar(b, "Dome rib " + (rib + 1), onDome(heading, step * 12f), onDome(heading, (step + 1) * 12f), .5f, .5f, k.rankGold);
            }
            const float HoopAt = 36f, CrownAt = 84f;
            Hoop(b, "Dome hoop", crown + Vector3.up * (HallRadius * Mathf.Sin(HoopAt * Mathf.Deg2Rad)), Quaternion.identity,
                 HallRadius * Mathf.Cos(HoopAt * Mathf.Deg2Rad), .4f, .4f, k.trimWhite, 36, heading => Mathf.Abs(Mathf.DeltaAngle(heading, slit)) > 15f);
            Hoop(b, "Dome crown", crown + Vector3.up * (HallRadius * Mathf.Sin(CrownAt * Mathf.Deg2Rad)), Quaternion.identity,
                 HallRadius * Mathf.Cos(CrownAt * Mathf.Deg2Rad), .5f, .5f, k.rankGold, 12);
        }

        // ------------------------------------------------------------------ the route and the beats

        static void Path(CourseBuilder b, List<(Vector3 at, string beat)> points)
        {
            var route = new Vector3[points.Count];
            var names = new string[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                route[i] = points[i].at;
                names[i] = points[i].beat;
            }
            var path = new GameObject("ProgressPath");
            path.transform.SetParent(b.parent, false);
            path.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), names);
        }

        // Five beats in six boxes (Cassiopeia's second box is the east gate's threshold).
        static SectionBeat[] Beats() => new[]
        {
            new SectionBeat("1 북두칠성 손잡이", V(-31f, -3f, 14f), V(4f, 8f, 47f)),
            new SectionBeat("2 북두칠성 국자", V(-43f, -3f, 39f), V(-18.5f, 9f, 67f)),
            new SectionBeat("3 대천구", V(-18.5f, 0f, 58f), V(13.6f, 18f, 70f)),
            new SectionBeat("4 카시오페이아", V(16.7f, 8f, 60f), V(34f, 18f, 97f)),
            new SectionBeat("4 카시오페이아", V(13.6f, 8f, 60f), V(16.7f, 18f, 68f)),
            new SectionBeat("5 관측대와 대망원경", V(0f, 9f, 81f), V(16.7f, 28f, 118f)),
        };
    }
}
