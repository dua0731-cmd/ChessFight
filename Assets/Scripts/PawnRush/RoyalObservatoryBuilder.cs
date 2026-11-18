using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Observatory's graybox, second build (R720): structure v0.3
    // (Docs/PawnRush/PAWN_RUSH_ROYAL_OBSERVATORY_STRUCTURE_v0.3.md) with the eight obstacles of the obstacle plan v0.1
    // (PAWN_RUSH_ROYAL_OBSERVATORY_OBSTACLES_v0.1.md). The first build (R712, the level design v0.1's seven beats
    // round three sides of a square) is gone. Section frame: the entry's threshold is the origin, +z north (the way
    // in), +x east, y the floor's top. A round hall, its middle at (0, 64); the way passes THROUGH its set pieces
    // one after another instead of winding round the middle:
    //
    //   0 entry tunnel (z 0..14)
    //   1 the Big Dipper: seven star platforms on single pillars, joined by 3 m constellation lines, bending west
    //     and north (y 0 -> 3.2). Stars 2, 3 and 5 are variable stars (StarPetal), meteors fall on the bowl's
    //     three lines (MeteorShower), a comet shuttles the short way from star 4 to star 7 (CometFerry)
    //   2 the great globe: in by the west gate (y 4), straight up a 6 m climb inside the glass, out by the east
    //     gate (y 12). The moon on the ecliptic ring crosses both gates in turn; four planets cross the climb
    //   3 Cassiopeia: four stars and a binary star in a W, east of the globe at y 11.2 .. 12.2. Meteors on its two
    //     real lines; the binary's two platforms turn about each other
    //   4 the observing stage (a rest, railed)
    //   5 the great telescope: up the deck inside its truss tube (y 12 -> 20), through three iris gates (IrisLeaf)
    //     and two focus beams (FocusBeam), out through the dome's slit
    //   6 the balcony outside the dome -> exit (9, 20, 130)
    //
    // No imported obstacle prefab is used: everything that moves is new, and all of it is a pure function of the
    // shared obstacle clock (the meteors also of the section's seed), so the section sends nothing in a match.
    // Where the graybox departs from the two plans' numbers it says so beside the thing (the structure document
    // lists them under "회색 상자에서 바꾼 것"). Nothing here has been played by a person.
    public static class RoyalObservatoryBuilder
    {
        public const float StageTop = 12f, ExitTop = 20f, HallFloor = -18f, KillTop = -5f;
        public const int Checkpoints = 9, BeatBoxes = 6;
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
        // Which of them are variable stars, and how far apart in time (seconds).
        static readonly int[] VariableStars = { 1, 2, 4 };
        const float VariableStagger = 2f;
        // Graybox: the core is 1.75 m (the plan's 1.6), so with every petal down the gap from a line's end to the
        // core is 1.45 m - inside the 1.5 m a required gap may be.
        const float CoreRadius = 1.75f, PetalAngle = 84f;
        const float CometRadius = 2.2f;

        // 2: the great globe.
        static readonly Vector3 Globe = new Vector3(0f, 10f, 64f), Sun = new Vector3(0f, 16f, 64f);
        static readonly Vector3 WestGate = new Vector3(-12.65f, 4f, 64f), EastGate = new Vector3(13.86f, 12f, 64f);
        const float GlobeRadius = 14f, ClimbHalf = 3f, EclipticRadius = 15.6f, EclipticTilt = 12.8f, MoonRadius = 3.4f, MoonPeriod = 12f;
        const float PlanetGap = .2f;

        // 3: Cassiopeia's stars 1, 2, 4 and 5; its third is the binary star.
        static readonly Star[] Cassiopeia =
        {
            new Star(19f, 12f, 68f, 3f), new Star(27f, 11.2f, 73f, 3.4f), new Star(27f, 11.6f, 85f, 3.4f), new Star(20f, 12.2f, 89.5f, 3f),
        };
        // Graybox: the binary's platforms are at 11.4 (the plan's star 3 stood at 12.6). Their sweep reaches 5.7 m
        // from the middle, which leaves 1 m of line to each neighbour, and 1.4 m of rise in 1 m is no slope.
        static readonly Vector3 Binary = new Vector3(19f, 11.4f, 79f);
        const float BinaryOrbit = 3.4f, BinaryRadius = 2.3f, BinaryPeriod = 8f;

        // 4, 5: the stage and the telescope's deck.
        static readonly Vector3 Stage = new Vector3(9f, StageTop, 90f);
        const float StageRadius = 8f;
        static readonly Vector3 DeckFoot = new Vector3(9f, StageTop, 93f), DeckHead = new Vector3(9f, ExitTop, 118f);
        const float DeckHalf = 2.5f, TubeRadius = 3.7f;
        // Along the deck, 0 its foot .. 1 its head: the tube's seven rings, three of them irises; the two beams.
        static readonly float[] TubeRings = { .04f, .28f, .41f, .54f, .67f, .8f, .96f };
        static readonly float[] Irises = { .28f, .54f, .8f };
        static readonly float[] Beams = { .41f, .67f };
        const float IrisStagger = 1.7f, LeafClear = .1f, LeafHeight = 2.4f;

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

        // A prism: a convex outline (x, z) between two heights, in the current group's frame; its collider is that
        // convex mesh. (The course builder makes boxes, cylinders and spheres only. The mesh is made here, at build
        // time, and lives with the built content - the prefab holds none of it.)
        static GameObject Prism(CourseBuilder b, string name, Vector2[] outline, float y0, float y1, Material material, bool solid = true)
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
            for (int i = 0; i < Dipper.Length; i++)
            {
                string name = "Dipper star " + (i + 1);
                int variable = Array.IndexOf(VariableStars, i);
                if (variable >= 0) VariableStar(b, name, Dipper[i], Dipper[i - 1].at, Dipper[i + 1].at, VariableStagger * variable);
                else StarPlatform(b, name, Dipper[i]);
            }
            var lines = new MeteorShower.Line[Dipper.Length - 1];
            for (int i = 0; i + 1 < Dipper.Length; i++)
            {
                Ends(Dipper[i].at, Reach(i), Dipper[i + 1].at, Reach(i + 1), out var from, out var to);
                Line(b, "Dipper line " + (i + 1) + "-" + (i + 2), from, to);
                lines[i] = new MeteorShower.Line(from, to);
                string beat = i < 3 ? "1" : "2";
                route.Add((Dipper[i].at, beat));
                route.Add((from, beat));
                route.Add((to, beat));
            }
            route.Add((Dipper[6].at, "2"));

            // Meteors on the bowl's three lines (4-5, 5-6, 6-7), one about every 1.25 s.
            var shower = b.Group("Device_MeteorShower Dipper");
            shower.gameObject.AddComponent<MeteorShower>().Configure(new[] { lines[3], lines[4], lines[5] }, 1.25f, 31);
            Comet(b);

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

        // A variable star: a core that stays and five petals round it that fold down one after another and come up
        // together (StarPetal: level 2.6 s, warning 0.8 s, 0.25 s apart, 0.5 s each, up in 0.75 s; 6 s in all - the
        // last one hangs 0.35 s, the plan's "0.6 s or so" less what a 6 s cycle leaves). The wave starts beside the
        // way in and ends on the way out, so the petal a runner leaves by holds longest.
        static void VariableStar(CourseBuilder b, string name, Star s, Vector3 from, Vector3 to, float phaseSeconds)
        {
            var k = b.kit;
            b.Cylinder(name + " core", s.at - Vector3.up * StarThickness, CoreRadius * 2f, StarThickness, k.boardDark).AddComponent<CourseFloor>();
            StarMark(b, name + " mark", s.at, CoreRadius * .8f);
            Pillar(b, name + " pillar", s.at - Vector3.up * StarThickness, .5f);

            float yawIn = Yaw(from - s.at), around = Mathf.Repeat(Yaw(to - s.at) - yawIn, 360f);
            float step = around <= 180f ? 72f : -72f;   // round the star by the shorter way to the way out
            if (around > 180f) around = 360f - around;
            // The last petal the line out touches (a 3 m line's end spans 28 degrees either side of its middle).
            float lineHalf = Mathf.Asin(LineWidth * .5f / s.radius) * Mathf.Rad2Deg;
            int last = Mathf.Clamp(Mathf.FloorToInt((around + lineHalf + 36f) / 72f), 0, 4);
            float hinge = CoreRadius * Mathf.Cos(36f * Mathf.Deg2Rad);
            var outline = PetalOutline(s.radius - .02f, hinge);
            for (int i = 0; i < 5; i++)
            {
                var facing = Quaternion.Euler(0f, yawIn + step * i, 0f);
                var petal = b.Group("Device_StarPetal " + name + " " + (i + 1));
                // A centimetre under the core's top: the core's deck covers the petal's inner edge.
                petal.localPosition = s.at + facing * Vector3.forward * hinge + Vector3.down * .01f;
                petal.localRotation = facing;
                GameObject warning = null;
                Under(b, petal, () =>
                {
                    Prism(b, "Petal", outline, -.3f, 0f, k.boardLight).AddComponent<CourseFloor>();
                    warning = Prism(b, "Petal warning", outline, .002f, .02f, k.marker, false);
                    warning.SetActive(false);
                });
                petal.gameObject.AddComponent<StarPetal>().Configure((i - last + 4) % 5, 2.6f, .8f, .25f, .5f, .75f, 6f, PetalAngle, warning, phaseSeconds);
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

        // The comet: a platform 4.4 m across shuttling the short way across the bowl, from star 4's rim to star 7's
        // (wait 1.5 s, cross 3.5 s, wait, back: 10 s). Graybox: it travels 4.9 m - the 17 m of the plan is from the
        // one star's middle to the other's, and the two stars and the platform itself take the rest.
        static void Comet(CourseBuilder b)
        {
            var k = b.kit;
            Star a = Dipper[3], c = Dipper[6];
            var flat = Flat(c.at - a.at).normalized;
            Vector3 near = a.at + flat * (a.radius + CometRadius + .1f), far = c.at - flat * (c.radius + CometRadius + .1f);
            var comet = b.Group("Device_Comet");
            comet.localPosition = near;
            Under(b, comet, () =>
            {
                b.Cylinder("Comet deck", V(0f, -.5f, 0f), CometRadius * 2f, .5f, k.boardLight).AddComponent<CourseFloor>();
                b.Cylinder("Comet brass rim", V(0f, -.65f, 0f), CometRadius * 2f + .3f, .5f, k.rankGold, false);
                b.Sphere("Comet head", V(0f, -1.1f, 0f), 1.8f, k.rankGold, false);
                // Its tail streams away from the sun lamp, as a comet's does.
                var tail = b.Group("Comet tail");
                tail.localRotation = Quaternion.Euler(0f, Yaw(near - Sun), 0f);
                var pale = k.glass != null ? k.glass : k.boardLight;
                Under(b, tail, () =>
                {
                    for (int i = 0; i < 3; i++)
                        b.Box("Tail " + (i + 1), V(-.7f + .2f * i, -1.3f, 1.2f + 1.6f * i), V(.7f - .2f * i, -1f, 2.6f + 1.6f * i), pale, false, false);
                });
            });
            comet.gameObject.AddComponent<CometFerry>().Configure(far - near, 1.5f, 3.5f, 0f);
            Bar(b, "Comet orbit line", a.at + flat * a.radius + Vector3.down * .8f, c.at - flat * c.radius + Vector3.down * .8f, .12f, .06f, k.rankGold);
        }

        // ------------------------------------------------------------------ 2 the great globe

        // The climb's height at an x inside the globe.
        static float ClimbY(float x) => Mathf.LerpUnclamped(WestGate.y, EastGate.y, (x - WestGate.x) / (EastGate.x - WestGate.x));

        // A glass sphere 28 m across in the hall's middle (drawn only - glass does not hold a pawn, and a camera
        // sees through it). The way: a threshold outside each gate, 7 m wide (graybox: the plan's 3 m line would be
        // swept clean by the moon; on the east the threshold itself reaches the first of Cassiopeia's stars), and
        // one straight climb 6 m wide from the west gate (y 4) to the east (y 12), 17 degrees. No rails anywhere:
        // off the climb is a fall, back to the checkpoint just inside the west gate.
        static void GreatGlobe(CourseBuilder b, List<(Vector3, string)> route)
        {
            var k = b.kit;
            b.Floor("West gate threshold", -18.5f, -12.4f, 60.5f, 67.5f, WestGate.y);
            b.RampX("Globe climb", Globe.z - ClimbHalf, Globe.z + ClimbHalf, WestGate.x, EastGate.x, WestGate.y, EastGate.y, .4f);
            b.Floor("East gate threshold", 13.6f, 17.2f, 60.5f, 67.5f, EastGate.y - .01f);
            b.Checkpoint(4, V(-11.3f, ClimbY(-11.3f), Globe.z), 90f, ClimbHalf * 2f);
            b.Checkpoint(5, V(12.6f, ClimbY(12.6f), Globe.z), 90f, ClimbHalf * 2f);
            route.Add((WestGate, "3"));
            route.Add((EastGate, "3"));

            // The eclipse gate: the moon, a disc 6.8 m across, rides the ecliptic ring once in 12 s (30 deg/s). The
            // ring is tilted, west low and east high, so the moon crosses the west gate at y 6.5 and the east at
            // 13.5, six seconds apart, and each is covered for about a second and a half. It is thin: what it meets
            // on a threshold it turns aside along the way, toward the gate or back.
            var moon = Mover(b, "Device_EclipseMoon", Globe, MotionKind.Rotate, Vector3.up);
            moon.transform.localRotation = Quaternion.Euler(0f, 0f, EclipticTilt);
            moon.degreesPerSecond = 360f / MoonPeriod;
            Under(b, moon.transform, () =>
            {
                var disc = b.Cylinder("Moon", V(EclipticRadius, -.2f, 0f), MoonRadius * 2f, .4f, k.boardLight, true, true);
                disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // its faces look along the ring's radius
            });

            // The planets: each on a level orbit about the globe's axis, crossing the climb at one place (x) and
            // passing under or over it on the other side. The inner two are quick, the outer two slow; neighbours
            // turn opposite ways. Graybox: an orbit is level and the climb is not, so a planet is hung 0.2 m over
            // the highest the climb gets under its path (the west two ride 0.15 m higher than the plan's sum).
            var planets = new (float x, float body, float period, bool ring)[]
            {
                (-9f, 1.2f, 9f, false), (-4.5f, .9f, -5f, false), (4.5f, 1f, 5.5f, false), (9f, 1.6f, -10f, true),
            };
            for (int i = 0; i < planets.Length; i++)
            {
                var p = planets[i];
                float orbit = Mathf.Abs(p.x);
                float edge = p.x * Mathf.Cos(Mathf.Asin(Mathf.Min(1f, ClimbHalf / orbit)));
                float foot = Mathf.Max(ClimbY(p.x), ClimbY(edge)) + PlanetGap, y = foot + p.body;
                var planet = Mover(b, "Device_Planet " + (i + 1), V(Globe.x, y, Globe.z), MotionKind.Rotate, Vector3.up);
                planet.degreesPerSecond = 360f / p.period;
                planet.phase = i * .29f;
                Under(b, planet.transform, () =>
                {
                    b.Sphere("Planet body", V(p.x, 0f, 0f), p.body * 2f, k.hazard).AddComponent<NoClimbSurface>();
                    if (!p.ring) return;
                    // The fourth planet's ring: jumped, where the body is gone round. Graybox: a disc low round its
                    // foot (0.2 .. 0.4 m over the climb, tilted with it) - a ring at the body's middle would be at a
                    // pawn's head.
                    var ring = b.Cylinder("Planet ring", V(p.x, 0f, 0f), (p.body + 1f) * 2f, .2f, k.rankGold, true, true);
                    ring.transform.localPosition = V(p.x, ClimbY(p.x) + .3f - y, 0f);
                    ring.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(EastGate.y - WestGate.y, EastGate.x - WestGate.x) * Mathf.Rad2Deg);
                });
                Hoop(b, "Planet orbit " + (i + 1), V(Globe.x, y, Globe.z), Quaternion.identity, orbit, .08f, .08f, k.trimWhite, 24);
            }

            // What the globe looks like (drawn only): the glass, its three rings, the sun lamp, the stepped base,
            // the two gates' brass frames.
            b.Sphere("Great globe glass", Globe, GlobeRadius * 2f, k.glass != null ? k.glass : k.boardLight, false);
            Hoop(b, "Meridian ring", Globe, Quaternion.Euler(0f, 0f, -90f), 16.5f, .5f, .5f, k.rankGold, 36);
            Hoop(b, "Horizon ring", Globe, Quaternion.identity, 18f, 2f, .3f, k.rankGold, 36);
            Hoop(b, "Ecliptic ring", Globe, Quaternion.Euler(0f, 0f, EclipticTilt), EclipticRadius, .35f, .35f, k.rankGold, 36);
            b.Sphere("Sun lamp", Sun, 4.4f, k.rankGold, false);
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

        // A W of four stars and a binary, 11 to 12 m up. Graybox: the stars stand close, so only two of the plan's
        // four lines are long enough to rain on (1-2 and 4-5); the two by the binary are 1 m stubs.
        static void CassiopeiaPath(CourseBuilder b, List<(Vector3, string)> route)
        {
            var k = b.kit;
            Star s1 = Cassiopeia[0], s2 = Cassiopeia[1], s4 = Cassiopeia[2], s5 = Cassiopeia[3];
            StarPlatform(b, "Cassiopeia star 1", s1);
            StarPlatform(b, "Cassiopeia star 2", s2);
            StarPlatform(b, "Cassiopeia star 4", s4);
            StarPlatform(b, "Cassiopeia star 5", s5);

            Ends(s1.at, s1.radius - .3f, s2.at, s2.radius - .3f, out var a0, out var a1);
            Line(b, "Cassiopeia line 1-2", a0, a1);
            Ends(s4.at, s4.radius - .3f, s5.at, s5.radius - .3f, out var c0, out var c1);
            Line(b, "Cassiopeia line 4-5", c0, c1);
            // On to the stage: its rim and the fifth star's just touch, so a short line laps over both.
            Ends(s5.at, s5.radius - 1f, Stage, StageRadius - 1f, out var d0, out var d1);
            Line(b, "Cassiopeia line 5-stage", d0, d1);

            // The binary star: two platforms 4.6 m across, 3.4 m from their common middle, once round in 8 s. A line's
            // end is 0.15 m outside their sweep; a platform comes by each end every 4 s. Graybox: the W puts the way
            // in and the way out 74 degrees apart, so they turn the short way from the one to the other and the ride
            // is 1.6 s, not the plan's half turn - who misses the way out goes round again.
            float sweep = BinaryOrbit + BinaryRadius + .15f;
            Vector3 toIn = Flat(s2.at - Binary).normalized, toOut = Flat(s4.at - Binary).normalized;
            Vector3 in0 = s2.at - toIn * (s2.radius - .3f), in1 = Binary + toIn * sweep;
            Vector3 out0 = Binary + toOut * sweep, out1 = s4.at - toOut * (s4.radius - .3f);
            Line(b, "Cassiopeia stub 2-binary", in0, in1);
            Line(b, "Cassiopeia stub binary-4", out0, out1);
            var binary = Mover(b, "Device_BinaryStar", Binary, MotionKind.Rotate, Vector3.up);
            binary.degreesPerSecond = -360f / BinaryPeriod;
            Under(b, binary.transform, () =>
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    string name = side < 0 ? "Binary star A" : "Binary star B";
                    var at = V(side * BinaryOrbit, 0f, 0f);
                    b.Cylinder(name, at + Vector3.down * .5f, BinaryRadius * 2f, .5f, k.boardDark).AddComponent<CourseFloor>();
                    b.Cylinder(name + " brass rim", at + Vector3.down * .65f, BinaryRadius * 2f + .3f, .5f, k.rankGold, false);
                    StarMark(b, name + " mark", at, BinaryRadius * .75f);
                }
                b.Box("Binary bar", V(-BinaryOrbit, -.95f, -.15f), V(BinaryOrbit, -.65f, .15f), k.rankGold, false, false);
            });
            Pillar(b, "Binary pillar", Binary + Vector3.down * .95f, .5f);
            // The way across is a ride: nothing stands there for the section check to find.
            var ride = b.Group("Binary ride (intended gap)");
            ride.localPosition = Binary;
            ride.gameObject.AddComponent<IntendedGap>().Configure(new Vector3(13f, 6f, 13f), "쌍성: 도는 발판을 타고 건넌다");

            // Meteors on the two real lines, taking turns, one about every 1.4 s.
            var shower = b.Group("Device_MeteorShower Cassiopeia");
            shower.gameObject.AddComponent<MeteorShower>().Configure(new[] { new MeteorShower.Line(a0, a1), new MeteorShower.Line(c0, c1) }, 1.4f, 32);

            b.Checkpoint(6, s2.at, Yaw(Binary - s2.at), s2.radius * 2f);

            route.Add((s1.at, "4"));
            route.Add((a0, "4"));
            route.Add((a1, "4"));
            route.Add((s2.at, "4"));
            route.Add((in0, "4"));
            route.Add((in1, "4"));
            float from = Mathf.Atan2(toIn.z, toIn.x), to = Mathf.Atan2(toOut.z, toOut.x);
            for (int i = 0; i <= 4; i++)
            {
                float a = Mathf.Lerp(from, to, i / 4f);
                route.Add((Binary + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * BinaryOrbit, "4"));
            }
            route.Add((out0, "4"));
            route.Add((out1, "4"));
            route.Add((s4.at, "4"));
            route.Add((c0, "4"));
            route.Add((c1, "4"));
            route.Add((s5.at, "4"));
            route.Add((d0, "4"));
            route.Add((d1, "5"));
        }

        // ------------------------------------------------------------------ 4 the observing stage

        // A round deck 16 m across at y 12 on one thick pillar: the rest before the telescope. Railed all round but
        // where the fifth star's line comes in and where the tube's deck leaves; the fork mount's two arms stand
        // either side of the deck's foot and close the rim there.
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
            float chord = 2f * StageRadius * Mathf.Tan(Mathf.PI / Segments) + .02f, toStar = Yaw(Cassiopeia[3].at - Stage);
            var rails = b.Group("Stage rails");
            rails.localPosition = Stage;
            Under(b, rails, () =>
            {
                for (int i = 0; i < Segments; i++)
                {
                    float heading = (i + .5f) * 360f / Segments;
                    if (Mathf.Abs(Mathf.DeltaAngle(heading, 0f)) < 31f || Mathf.Abs(Mathf.DeltaAngle(heading, toStar)) < 17f) continue;
                    var piece = b.Group("Segment " + (i + 1));
                    piece.localRotation = Quaternion.Euler(0f, heading, 0f);
                    Under(b, piece, () => b.Wall("Stage rail", -chord * .5f, chord * .5f, 0f, RailHeight, StageRadius - T, StageRadius, false));
                }
            });
            b.Wall("Fork arm W", Stage.x - 4.1f, Stage.x - DeckHalf - .1f, StageTop - .8f, StageTop + 9.5f, 95.4f, 97.4f, false);
            b.Wall("Fork arm E", Stage.x + DeckHalf + .1f, Stage.x + 4.1f, StageTop - .8f, StageTop + 9.5f, 95.4f, 97.4f, false);

            b.Checkpoint(7, V(Stage.x, StageTop, 88f), 0f, 12f);
            route.Add((Stage, "5"));
        }

        // ------------------------------------------------------------------ 5 the great telescope

        static Vector3 DeckAlong => (DeckHead - DeckFoot).normalized;
        static Vector3 DeckNormal => Vector3.Cross(DeckAlong, Vector3.right);
        static Vector3 DeckPoint(float f) => Vector3.LerpUnclamped(DeckFoot, DeckHead, f);

        // A deck 5 m wide and 26 m long, 18 degrees, from the stage (y 12) up through the dome's slit to the balcony
        // (y 20), inside a truss tube of seven rings (drawn only: its sides are open, and off the deck is a fall).
        //   three iris gates at 0.28, 0.54 and 0.80 of the way: each two leaves closing from left and right (open
        //   2.4 s, warning 0.8 s, closing 0.5 s, shut 0.8 s, opening 0.5 s; 5 s), each 1.7 s later than the one below
        //   it, so the opening runs up the tube
        //   two focus beams between them, at 0.41 and 0.67: a hot spot 2.6 m across wandering over the 6.8 m of deck
        //   between two irises - the place a runner waits
        static void Telescope(CourseBuilder b, List<(Vector3, string)> route)
        {
            var k = b.kit;
            Vector3 along = DeckAlong, normal = DeckNormal;
            var facing = Quaternion.LookRotation(along, normal);
            b.Ramp("Telescope deck", DeckFoot.x - DeckHalf, DeckFoot.x + DeckHalf, DeckFoot.z, DeckHead.z, DeckFoot.y, DeckHead.y, .4f);
            route.Add((DeckFoot, "5"));
            route.Add((DeckHead, "5"));

            for (int i = 0; i < Irises.Length; i++) Iris(b, i + 1, Irises[i], Mathf.Repeat(-IrisStagger * i, 5f));

            // The light comes from the objective, the tube's top ring.
            float axis = Mathf.Sqrt(TubeRadius * TubeRadius - DeckHalf * DeckHalf);
            Vector3 objective = DeckPoint(TubeRings[TubeRings.Length - 1]) + normal * axis;
            for (int i = 0; i < Beams.Length; i++)
            {
                var beam = b.Group("Device_FocusBeam " + (i + 1));
                beam.localPosition = DeckPoint(Beams[i]);
                beam.localRotation = facing;
                beam.gameObject.AddComponent<FocusBeam>().Configure(1.3f, 1.9f, 2f, 3.1f, 4.7f, 1.9f * i,
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
            for (int side = -1; side <= 1; side += 2)
            {
                var leaf = b.Group("Device_IrisLeaf " + number + (side < 0 ? " W" : " E"));
                leaf.localPosition = at + Vector3.right * (side * (DeckHalf + LeafClear));
                leaf.localRotation = Quaternion.LookRotation(Vector3.right * -side, normal);   // toward the deck's middle
                Under(b, leaf, () => Prism(b, "Leaf", LeafOutline, .05f, LeafHeight, k.hazard).AddComponent<NoClimbSurface>());
                leaf.gameObject.AddComponent<IrisLeaf>().Configure(DeckHalf + LeafClear - .02f, 2.4f, .8f, .5f, .8f, .5f,
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
            b.Checkpoint(8, V(Exit.x, ExitTop, 121f), 0f, 10f);
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
            new SectionBeat("4 카시오페이아", V(17.2f, 8f, 60f), V(32f, 18f, 93f)),
            new SectionBeat("4 카시오페이아", V(13.6f, 8f, 60f), V(17.2f, 18f, 68f)),
            new SectionBeat("5 관측대와 대망원경", V(0f, 9f, 81f), V(17.2f, 28f, 118f)),
        };
    }
}
