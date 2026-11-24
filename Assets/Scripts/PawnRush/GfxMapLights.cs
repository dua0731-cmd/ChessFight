using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    /// <summary>
    /// JY-gfx (2026-11-24): each indoor map's own light. JY: "3코스도 각 맵들마다 컨셉이 아에 다른데 획일화되잖아", with
    /// twenty pictures of interiors lit by their architecture: chandeliers and sconces, lanterns along a nave, uplight on
    /// columns, skylights and leaded windows throwing their pattern on stone (그래픽-재구축\조명\참고_JY). So every map has
    /// a concept of its own (Maps), from what the map is: a look (GameCourses: how much of the morning's sky light
    /// gets in, its grade and shafts; the sun and the sky stay the scene's, one time of day) and a rig built from its own parts:
    ///   lid       a shadow-only roof over the roofless hall: the sun gets in only through its windows and skylights, as
    ///             patterned pools and beams (the shafts only draw where the sun is blocked), and the lamps can show;
    ///   glass     the windows' panes stop casting shadows, so the light comes through them;
    ///   fixtures  the map's own sconces, chandeliers and work lamps (MiniGameFixtures) stronger and reaching further;
    ///   lamps     a light where a lamp glows that has none of its own;
    ///   uplights  warm light washing up the columns from their feet;
    ///   goal      the way out lit from above and before it;
    ///   stage     one great pool from high over the hall's middle;
    ///   heart     a warm light at the map's heart (a bed, a hearth).
    /// GfxSections hands each course 2 and 3 section in (Dress). The rig's numbers are read every frame (GfxMapRig), so
    /// the picture tool can try others. Off with the upgrade.
    /// </summary>
    public static class GfxMapLights
    {
        public sealed class Rig
        {
            public bool Lid = true, ClearGlass = true;
            public float FixtureBoost = 1f, FixtureReach = 1f;
            public float LampIntensity = 2f;
            public Color LampColor = new Color(1f, .8f, .58f);
            public float UplightIntensity;
            public Color UplightColor = new Color(1f, .78f, .52f);
            public int UplightMax = 16;
            public float GoalIntensity = 3f;
            public Color GoalColor = new Color(1f, .86f, .62f);
            public float StageIntensity;
            public Color StageColor = new Color(1f, .93f, .8f);
            public float StageAngle = 70f;
            public string HeartWord = "";
            public float HeartIntensity, HeartRange = 10f;
            public Color HeartColor = new Color(1f, .6f, .32f);
            /// <summary>The floors polished at least this smooth, so they hold the lamps and windows (0 leaves them).</summary>
            public float FloorGloss;
            /// <summary>A floor to change: the material named FloorFrom (its name without " (Gfx)") becomes FloorTo (a
            /// material in Resources/PawnRushMiniGames/Materials, the swatch family's; empty keeps it), times FloorTint.</summary>
            public string FloorFrom = "", FloorTo = "";
            public Color FloorTint = Color.white;
            /// <summary>Each layer of the map's light plan times this (and all of it times PlanScale): tunable as a whole.</summary>
            public float PlanScale = 1f, DaylightScale = 1f, GeneralScale = 1f, WashScale = 1f, UplightScale = 1f, DecorativeScale = 1f, AccentScale = 1f, BounceScale = 1f;
        }

        /// <summary>
        /// One line of a map's light plan, its lighting design (JY 11-24: "조명 설계부터 다시 하고 라이팅 구현해"): a light at a
        /// place in the section's frame (x right, y up, z along the way), a row of them, or one at each part of a name, with
        /// the layer it belongs to.
        /// </summary>
        public sealed class Fixture
        {
            /// <summary>daylight, general, wash, uplight, decorative, accent or bounce (each has a scale on the rig).</summary>
            public string Layer = "general";
            /// <summary>What it is for, in words (the design's reason).</summary>
            public string Note = "";
            public LightType Type = LightType.Point;
            /// <summary>Where; with Count over 1, a row of Count lights from At to To (both ends included).</summary>
            public Vector3 At, To;
            public int Count = 1;
            /// <summary>Or one light at each enabled part whose name starts with this, at Anchor ("centre", "top", "bottom")
            /// of its box plus Offset (its x toward the hall's middle when Inward), at most Max, none closer than Spacing.</summary>
            public string Each = "";
            public string Anchor = "centre";
            public Vector3 Offset;
            public bool Inward;
            public int Max = 64;
            public float Spacing;
            /// <summary>A spot's way in the section's frame (its x toward the middle when Inward).</summary>
            public Vector3 Aim = Vector3.down;
            public float Intensity = 1f, Range = 10f, Angle = 60f, Inner = 40f;
            public Color Color = Color.white;
            public bool Shadows;
        }

        public sealed class Map
        {
            /// <summary>In the section's name.</summary>
            public string Key;
            /// <summary>The map's light in words.</summary>
            public string Concept;
            public GameCourses.Look Look;
            public Rig Rig;
            /// <summary>The map's lighting design, light by light (built with the rig, read every frame).</summary>
            public List<Fixture> Plan = new List<Fixture>();
            /// <summary>The section lights its own sky, sun and ambient (the team's code: the Royal Library, the night
            /// Observatory): no look of the course's over it, only the rig (its plan) and its baked light.</summary>
            public bool OwnLook;
        }

        // ------------------------------------------------------------------ the maps

        static GameCourses.Look L(string name) => new GameCourses.Look { Name = name };

        /// <summary>Course 2's grey stand-ins and unknown maps: a plain bright interior.</summary>
        public static readonly Map Course2Default = new Map
        {
            Key = "", Concept = "a plain bright interior",
            Look = new GameCourses.Look
            {
                Name = "Course 2", Indoor = .85f, Bounce = .7f,
            },
            Rig = new Rig(),
        };

        /// <summary>Course 3's grey stand-ins and unknown maps: a plain darker interior.</summary>
        public static readonly Map Course3Default = new Map
        {
            Key = "", Concept = "a plain darker interior",
            Look = new GameCourses.Look
            {
                Name = "Course 3", Indoor = .45f, Bounce = .6f, Exposure = .1f, Contrast = 26f, Saturation = 14f, BloomIntensity = .9f, BloomThreshold = .85f, Vignette = .2f,
                ShaftIntensity = 2f, ShaftDensity = .05f,
            },
            Rig = new Rig { LampIntensity = 3f },
        };

        public static readonly List<Map> Maps = new List<Map>
        {
            // ---- course 2: the castle's entrance, light but grand
            new Map
            {
                Key = "2-1_GrandStairHall",
                Concept = "the grand entrance of light: daylight falling through the dome's lattice onto the red stair, warm uplight washing every column and wall (a great station hall), the hub chandelier glowing, marble gleaming",
                Look = new GameCourses.Look
                {
                    Name = "Grand stair hall", Indoor = .85f, Bounce = .6f, Exposure = 0f, Contrast = 24f, Saturation = 18f, BloomIntensity = .8f, BloomThreshold = .9f, Vignette = .14f,
                    ShaftIntensity = 1.4f, ShaftDensity = .03f,
                },
                Rig = new Rig { UplightColor = new Color(1f, .9f, .78f), Lid = false, FixtureBoost = 1.5f, FixtureReach = 1.3f, LampIntensity = 2.5f, UplightIntensity = 2f, UplightMax = 16, GoalIntensity = 0f },
            },
            new Map
            {
                Key = "MG_B_RepairWing",
                Concept = "the royal workshop: daylight pouring in from above as through a skylight, sawdust haze, warm work-lamp pools over the benches and the pit, warm light washing up the columns, walnut planks underfoot instead of the dark slate",
                Look = new GameCourses.Look
                {
                    Name = "Repair wing", Indoor = .85f, Bounce = .7f, Exposure = .1f, Contrast = 28f, Saturation = 16f, ShaftIntensity = 1.6f, ShaftDensity = .045f,
                },
                Rig = new Rig { FloorTo = "PR_Mid_WalnutFloor", FloorFrom = "PR_Mid_SlateFloor", FloorGloss = .75f, UplightMax = 20, UplightIntensity = 4f, Lid = false, FixtureBoost = 3f, FixtureReach = 1.6f, LampIntensity = 4f, GoalIntensity = 2.5f },
            },
            new Map
            {
                Key = "MG_E_RoyalLibrary",
                Concept = "the bright reading room: cream daylight from above between the bookshelf towers, warm reading lamps and sconces, warm light up the columns, a polished herringbone parquet holding the lamps",
                Look = new GameCourses.Look
                {
                    Name = "Reading room", Indoor = .85f, Bounce = .7f, Exposure = .1f, Contrast = 26f, Saturation = 18f, ShaftIntensity = 1.8f, ShaftDensity = .05f,
                },
                Rig = new Rig { FloorTo = "PR_Mid_Parquet", FloorFrom = "PR_Mid_SlateFloor", FloorGloss = .75f, UplightMax = 20, UplightIntensity = 4f, Lid = false, FixtureBoost = 3f, FixtureReach = 1.6f, LampIntensity = 4f, GoalIntensity = 2.5f },
            },
            new Map
            {
                Key = "MG_F_ClockworkBallroom",
                Concept = "the glamour ballroom: the chandeliers are the stars, great warm pools on the dance floor and sparkle, gold columns lit from below, cool daylight from above, polished cream marble doubling the lamps",
                Look = new GameCourses.Look
                {
                    Name = "Ballroom", Indoor = .85f, Bounce = .7f, Exposure = .1f, Contrast = 24f, Saturation = 20f, BloomIntensity = .95f, BloomThreshold = .8f, ShaftIntensity = 1.2f,
                },
                Rig = new Rig { FloorTo = "PR_Mid_BallroomMarble", FloorFrom = "PR_Mid_SlateFloor", FloorGloss = .85f, Lid = false, FixtureBoost = 4f, FixtureReach = 1.6f, LampIntensity = 4f, UplightIntensity = 4f, UplightMax = 20, GoalIntensity = 3f },
            },
            new Map
            {
                Key = "MG_J_PalaceLaundry",
                Concept = "the fresh washhouse: clean cool daylight from above into steam over the pools, the water's light on white tiles, warm sconces by the doors",
                Look = new GameCourses.Look
                {
                    Name = "Laundry", Indoor = .85f, Bounce = .6f, Exposure = .1f, Contrast = 20f, Saturation = 18f, ShaftIntensity = 1.4f, ShaftDensity = .04f,
                },
                Rig = new Rig { FloorTo = "PR_Mid_WhiteTile", FloorFrom = "PR_Mid_SlateFloor", FloorGloss = .75f, UplightMax = 20, UplightIntensity = 4f, Lid = false, FixtureBoost = 3f, FixtureReach = 1.6f, LampIntensity = 4f, GoalIntensity = 2.5f },
            },
            // ---- course 3: the castle's heart, solemn and grand, each hall its own
            new Map
            {
                Key = "MG_G_RoyalProcession",
                Concept = "the ceremonial nave: columns of light falling from the high skylights onto the carpet, gold light rising up every column, the side aisles in cool shade, the far doors the brightest",
                Look = new GameCourses.Look
                {
                    Name = "Procession nave", Indoor = .45f, Bounce = .6f, Exposure = .15f, Contrast = 28f, Saturation = 16f, BloomIntensity = 1f, BloomThreshold = .8f, Vignette = .22f,
                    ShaftIntensity = 2.6f, ShaftDensity = .06f,
                },
                Rig = new Rig { LampIntensity = 6f, UplightIntensity = 9f, UplightColor = new Color(1f, .75f, .45f), UplightMax = 16, GoalIntensity = 8f },
            },
            new Map
            {
                Key = "MG_H_KingsChessHall",
                Concept = "the king's game: the board as a stage under one great pool of light, long window grids across it, the hall's edges falling away into shade",
                Look = new GameCourses.Look
                {
                    Name = "King's chess hall", Indoor = .4f, Bounce = .6f, Exposure = .1f, Contrast = 28f, Saturation = 16f, BloomIntensity = .9f, BloomThreshold = .85f, Vignette = .22f,
                    ShaftIntensity = 2.2f, ShaftDensity = .05f,
                },
                Rig = new Rig { LampIntensity = 5f, UplightIntensity = 4f, UplightMax = 10, GoalIntensity = 4f, StageIntensity = 8f },
            },
            new Map
            {
                Key = "MG_I_KingsBedchamber",
                Concept = "the royal bedchamber: an intimate room in deep plum and red, the canopy bed glowing warm at its heart, soft daylight through the curtained windows, the lasers bright in the dim",
                Look = new GameCourses.Look
                {
                    Name = "Bedchamber", Indoor = .4f, Bounce = .6f, Exposure = .1f, Contrast = 28f, Saturation = 18f, BloomIntensity = 1f, BloomThreshold = .8f, Vignette = .25f,
                    ShaftIntensity = 1.6f, ShaftDensity = .045f,
                },
                Rig = new Rig { LampIntensity = 5f, GoalIntensity = 3f, HeartWord = "bed", HeartIntensity = 5f, HeartRange = 10f },
            },
            new Map
            {
                Key = "WarRoom",
                Concept = "the war council before the end: a dark Gothic hall, lanterns marching along the bridge, cool light high through the lancets, the final golden door blazing",
                Look = new GameCourses.Look
                {
                    Name = "War room", Indoor = .4f, Bounce = .6f, Exposure = .1f, Contrast = 28f, Saturation = 16f, BloomIntensity = 1f, BloomThreshold = .8f, Vignette = .24f,
                    ShaftIntensity = 2.4f, ShaftDensity = .06f,
                },
                Rig = new Rig { FixtureBoost = 1.2f, FixtureReach = 1.2f, LampIntensity = 3f, GoalIntensity = 8f },
            },
        };

        /// <summary>The map a section is, by its name (its course's default when none is named).</summary>
        public static Map For(string sectionName, int course)
        {
            foreach (var m in Maps) if (sectionName.Contains(m.Key)) return m;
            return course == 2 ? Course2Default : Course3Default;
        }

        /// <summary>Whether the section has a map of its own (not a course's default).</summary>
        public static bool Has(string sectionName)
        {
            foreach (var m in Maps) if (sectionName.Contains(m.Key)) return true;
            return false;
        }

        // ------------------------------------------------------------------ dressing a section

        /// <summary>A course 2 or 3 section: its look in place (unless it lights itself) and its rig built.</summary>
        public static void Dress(PawnRushSection section, int course)
        {
            if (section == null || section.GetComponent<GfxMapRig>() != null) return;
            var map = For(section.name, course);
            var frame = section.transform;
            if (!Box(frame, out var box)) return;
            if (!map.OwnLook) GameCourses.Add(frame, box, map.Look);
            var rig = section.gameObject.AddComponent<GfxMapRig>();
            rig.Build(map, box, section);
        }

        /// <summary>The section's drawn parts as a box in its own frame (anything bigger than a hall, a backdrop, left out).</summary>
        public static bool Box(Transform frame, out Bounds box)
        {
            box = new Bounds();
            bool any = false;
            foreach (var r in frame.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var b = r.bounds;
                if (b.size.x > 300f || b.size.z > 300f) continue;
                for (int i = 0; i < 8; i++)
                {
                    var corner = frame.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (!any) { box = new Bounds(corner, Vector3.zero); any = true; }
                    else box.Encapsulate(corner);
                }
            }
            return any;
        }
    }

    /// <summary>
    /// JY-gfx: one section's light rig (GfxMapLights): the lights it made, the fixtures it strengthens, its lid and the
    /// glass it lets light through, kept in step with its map's numbers every frame and with the upgrade's switch.
    /// </summary>
    public sealed class GfxMapRig : MonoBehaviour
    {
        enum Kind { Lamp, Uplight, Goal, Stage, Heart }

        sealed class Fixture
        {
            public Light light;
            public float intensity, range;
            public FlameFlicker flicker;
        }

        GfxMapLights.Map map;
        readonly List<(Light light, Kind kind)> ours = new List<(Light, Kind)>();
        // The floors' materials and their own smoothness (put back when the upgrade is off).
        readonly Dictionary<Material, float> floors = new Dictionary<Material, float>();
        // The floor slots FloorFrom names (renderer, slot, the material the map put there) and what is on them now.
        readonly List<(Renderer renderer, int slot, Material original)> floorSlots = new List<(Renderer, int, Material)>();
        string floorKey = "", floorFrom = "";
        Material floorMade;
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        readonly List<Fixture> fixtures = new List<Fixture>();
        readonly List<(Renderer renderer, ShadowCastingMode mode)> glass = new List<(Renderer, ShadowCastingMode)>();
        Renderer lid;
        // The map's light plan as built: each light with the plan line it came from.
        readonly List<(Light light, GfxMapLights.Fixture line)> planned = new List<(Light, GfxMapLights.Fixture)>();

        public string Concept => map != null ? map.Concept : "";

        public void Build(GfxMapLights.Map map, Bounds box, PawnRushSection section)
        {
            this.map = map;
            var frame = transform;
            var renderers = GetComponentsInChildren<MeshRenderer>(true);

            // The map's own lights, before ours.
            foreach (var l in GetComponentsInChildren<Light>(true))
            {
                if (l.name.EndsWith("(JY-gfx)") || l.type == LightType.Directional) continue;
                fixtures.Add(new Fixture { light = l, intensity = l.intensity, range = l.range, flicker = l.GetComponent<FlameFlicker>() });
            }

            // The floors: flat things whose material says floor, slate, checker, marble, paving or tile.
            foreach (var r in renderers)
            {
                if (!r.enabled || r.bounds.size.y > 1.2f || r.bounds.size.x * r.bounds.size.z < 4f) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !m.HasProperty(SmoothnessId) || floors.ContainsKey(m)) continue;
                    var n = m.name.ToLowerInvariant();
                    if (n.Contains("floor") || n.Contains("slate") || n.Contains("checker") || n.Contains("marble") || n.Contains("paving") || n.Contains("tile"))
                        floors[m] = m.GetFloat(SmoothnessId);
                }
            }

            // Glass lets the sun through.
            foreach (var r in renderers)
            {
                var m = r.sharedMaterial;
                bool isGlass = r.name.ToLowerInvariant().Contains("glass") || (m != null && (m.name.StartsWith("Window") || m.name.ToLowerInvariant().Contains("glass")));
                if (isGlass && r.shadowCastingMode != ShadowCastingMode.Off) glass.Add((r, r.shadowCastingMode));
            }

            // The lid: shadow-only tiles over the hall at its walls' top, none over the skylights.
            lid = Lid(box, renderers);

            // Lamps that glow and have no light: one light per 1.5 m cell, at most 24.
            var cells = new Dictionary<Vector3Int, (Vector3 sum, int count)>();
            foreach (var r in renderers)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                bool glows = false;
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent >= .5f) { glows = true; break; }
                if (!glows) continue;
                var b = r.bounds;
                if (b.size.magnitude > 4f) continue;
                bool lit = false;
                foreach (var f in fixtures) if (f.light != null && Vector3.Distance(f.light.transform.position, b.center) < 1.5f) { lit = true; break; }
                if (lit) continue;
                var key = Vector3Int.FloorToInt(b.center / 1.5f);
                cells.TryGetValue(key, out var c);
                cells[key] = (c.sum + b.center, c.count + 1);
            }
            foreach (var c in cells.Values)
            {
                if (Count(Kind.Lamp) >= 24) break;
                Add(Kind.Lamp, LightType.Point, c.sum / c.count, Quaternion.identity, 6f, 0f);
            }

            // Uplights at the columns' feet, spread along the hall.
            var columns = new List<Bounds>();
            foreach (var r in renderers)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var n = r.name.ToLowerInvariant();
                if (!(n.Contains("column") || n.Contains("pillar") || n.Contains("pilaster"))) continue;
                var b = r.bounds;
                if (b.size.y < 4f || b.size.x > 3f || b.size.z > 3f) continue;
                columns.Add(b);
            }
            int up = Mathf.Min(map.Rig.UplightMax, columns.Count);
            if (up > 0)
            {
                columns.Sort((a, b) => frame.InverseTransformPoint(a.center).z.CompareTo(frame.InverseTransformPoint(b.center).z));
                var middle = frame.TransformPoint(box.center);
                for (int i = 0; i < up; i++)
                {
                    var b = columns[Mathf.Min(columns.Count - 1, Mathf.RoundToInt((i + .5f) * columns.Count / up - .5f))];
                    var toMiddle = middle - b.center;
                    toMiddle.y = 0f;
                    toMiddle = toMiddle.sqrMagnitude > 1e-4f ? toMiddle.normalized : -frame.forward;
                    var foot = new Vector3(b.center.x, b.min.y + .35f, b.center.z) + toMiddle * (Mathf.Max(b.extents.x, b.extents.z) + .5f);
                    var aim = Quaternion.LookRotation((Vector3.up * 4f - toMiddle).normalized, -toMiddle);
                    Add(Kind.Uplight, LightType.Spot, foot, aim, b.size.y + 2f, 48f);
                }
            }

            // The goal: the way out, lit from above and before it.
            if (section != null)
            {
                var exit = section.ExitWorld.position;
                var from = exit + frame.up * 7f - frame.forward * 7f;
                Add(Kind.Goal, LightType.Spot, from, Quaternion.LookRotation((exit + frame.up * 2.5f - from).normalized), 22f, 55f);
            }

            // The stage: one great pool from high over the hall's middle.
            {
                var middle = frame.TransformPoint(new Vector3(box.center.x, box.max.y - 1f, box.center.z));
                Add(Kind.Stage, LightType.Spot, middle, Quaternion.LookRotation(-frame.up, frame.forward), box.size.y + 6f, map.Rig.StageAngle);
            }

            // The heart: the biggest thing named for it.
            if (!string.IsNullOrEmpty(map.Rig.HeartWord))
            {
                Renderer heart = null;
                foreach (var r in renderers)
                    if (r.enabled && r.name.ToLowerInvariant().Contains(map.Rig.HeartWord) && (heart == null || r.bounds.size.sqrMagnitude > heart.bounds.size.sqrMagnitude)) heart = r;
                if (heart != null) Add(Kind.Heart, LightType.Point, heart.bounds.center + Vector3.up * 1f, Quaternion.identity, map.Rig.HeartRange, 0f);
            }

            // The map's light plan, line by line.
            Plan(box);
            LateUpdate();
        }

        static string Plain(string name) => name.EndsWith(" (Gfx)") ? name.Substring(0, name.Length - 6) : name;

        // A world box as a box in the section's frame.
        Bounds Local(Bounds b)
        {
            var l = new Bounds(transform.InverseTransformPoint(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                l.Encapsulate(transform.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z)));
            return l;
        }

        // Every line of the map's plan as lights: one at At, a row from At to To, or one at each part of a name.
        void Plan(Bounds box)
        {
            var frame = transform;
            var parts = GetComponentsInChildren<Renderer>(true);
            foreach (var line in GfxMapPlans.For(map.Key) ?? map.Plan)
            {
                var points = new List<(Vector3 at, float side)>();
                if (!string.IsNullOrEmpty(line.Each))
                {
                    foreach (var r in parts)
                    {
                        if (!r.enabled || !r.gameObject.activeInHierarchy || !r.name.StartsWith(line.Each, System.StringComparison.Ordinal)) continue;
                        var b = Local(r.bounds);
                        var p = line.Anchor == "top" ? new Vector3(b.center.x, b.max.y, b.center.z)
                              : line.Anchor == "bottom" ? new Vector3(b.center.x, b.min.y, b.center.z) : b.center;
                        // +1: the hall's middle is to the right of the part (its x larger).
                        float side = p.x < box.center.x ? 1f : -1f;
                        var offset = line.Offset;
                        if (line.Inward) offset.x *= side;
                        p += offset;
                        bool near = false;
                        if (line.Spacing > 0f)
                            foreach (var q in points)
                                if (Vector3.Distance(q.at, p) < line.Spacing) { near = true; break; }
                        if (near) continue;
                        points.Add((p, side));
                        if (points.Count >= line.Max) break;
                    }
                }
                else if (line.Count > 1)
                    for (int i = 0; i < line.Count; i++)
                    {
                        var p = Vector3.Lerp(line.At, line.To, i / (line.Count - 1f));
                        points.Add((p, p.x < box.center.x ? 1f : -1f));
                    }
                else points.Add((line.At, line.At.x < box.center.x ? 1f : -1f));
                foreach (var (at, side) in points)
                {
                    var aim = line.Aim;
                    if (line.Inward) aim.x *= side;
                    var way = frame.TransformDirection(aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector3.down);
                    var up = Mathf.Abs(Vector3.Dot(way, frame.up)) > .95f ? frame.forward : frame.up;
                    var go = new GameObject("Plan " + line.Layer + " light (JY-gfx)");
                    go.transform.SetParent(frame, false);
                    go.transform.SetPositionAndRotation(frame.TransformPoint(at), Quaternion.LookRotation(way, up));
                    var l = go.AddComponent<Light>();
                    l.type = line.Type;
                    l.range = line.Range;
                    if (line.Type == LightType.Spot)
                    {
                        l.spotAngle = line.Angle;
                        l.innerSpotAngle = Mathf.Min(line.Inner, line.Angle);
                    }
                    l.shadows = line.Shadows ? LightShadows.Soft : LightShadows.None;
                    l.color = line.Color;
                    l.intensity = 0f;
                    planned.Add((l, line));
                }
            }
        }

        static float Scale(GfxMapLights.Rig rig, string layer)
        {
            float s;
            switch (layer)
            {
                case "daylight": s = rig.DaylightScale; break;
                case "general": s = rig.GeneralScale; break;
                case "wash": s = rig.WashScale; break;
                case "uplight": s = rig.UplightScale; break;
                case "decorative": s = rig.DecorativeScale; break;
                case "accent": s = rig.AccentScale; break;
                case "bounce": s = rig.BounceScale; break;
                default: s = 1f; break;
            }
            return s * rig.PlanScale;
        }

        // The floor FloorFrom names as the rig wants it now: the FloorTo material (dressed as the upgrade dresses it)
        // or the map's own, times FloorTint (a copy for this section), or the map's own back when the upgrade is off.
        void Floors(bool on)
        {
            var rig = map.Rig;
            // FloorFrom changed (or first): the earlier floor back, then the slots of the one named now, wherever this
            // section lays it.
            if ((rig.FloorFrom ?? "") != floorFrom)
            {
                foreach (var (r, slot, original) in floorSlots)
                {
                    if (r == null) continue;
                    var back = r.sharedMaterials;
                    if (slot < back.Length) back[slot] = original;
                    r.sharedMaterials = back;
                }
                floorSlots.Clear();
                floorFrom = rig.FloorFrom ?? "";
                floorKey = "";
                if (floorFrom.Length > 0)
                    foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            if (mats[i] != null && Plain(mats[i].name) == floorFrom) floorSlots.Add((r, i, mats[i]));
                    }
            }
            string key = on ? rig.FloorTo + "|" + rig.FloorTint : "";
            if (key == floorKey || floorSlots.Count == 0) return;
            floorKey = key;
            if (floorMade != null) Destroy(floorMade);
            floorMade = null;
            Material want = null;
            if (on)
            {
                var source = floorSlots[0].original;
                if (!string.IsNullOrEmpty(rig.FloorTo))
                {
                    var to = Resources.Load<Material>("PawnRushMiniGames/Materials/" + rig.FloorTo);
                    if (to != null) source = GameSurfaces.Dressed(to);
                }
                want = source;
                if (rig.FloorTint != Color.white && source.HasProperty("_BaseColor"))
                {
                    floorMade = new Material(source) { name = source.name + " (" + map.Look.Name + ")", hideFlags = HideFlags.DontSave };
                    var c = source.GetColor("_BaseColor");
                    floorMade.SetColor("_BaseColor", new Color(c.r * rig.FloorTint.r, c.g * rig.FloorTint.g, c.b * rig.FloorTint.b, c.a));
                    want = floorMade;
                }
            }
            foreach (var (r, slot, original) in floorSlots)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (slot >= mats.Length) continue;
                mats[slot] = on ? want : original;
                r.sharedMaterials = mats;
            }
        }

        int Count(Kind kind)
        {
            int n = 0;
            foreach (var o in ours) if (o.kind == kind) n++;
            return n;
        }

        void Add(Kind kind, LightType type, Vector3 at, Quaternion way, float range, float angle)
        {
            var go = new GameObject(kind + " light (JY-gfx)");
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(at, way);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.range = range;
            if (type == LightType.Spot)
            {
                l.spotAngle = angle;
                l.innerSpotAngle = angle * .6f;
            }
            l.shadows = LightShadows.None;
            l.intensity = 0f;
            ours.Add((l, kind));
        }

        Renderer Lid(Bounds box, MeshRenderer[] renderers)
        {
            // The walls' top: the highest wall part of the shell (ENV_SideWall), else the box's top.
            float top = float.NegativeInfinity;
            foreach (var r in renderers)
            {
                if (!r.enabled) continue;
                var t = r.transform;
                bool wall = false;
                for (var p = t; p != null && p != transform; p = p.parent)
                    if (p.name.StartsWith("ENV_SideWall")) { wall = true; break; }
                if (!wall) continue;
                top = Mathf.Max(top, transform.InverseTransformPoint(r.bounds.max).y);
            }
            if (float.IsNegativeInfinity(top)) top = box.max.y;
            // Skylights stay open.
            var holes = new List<Rect>();
            foreach (var r in renderers)
            {
                if (!r.name.StartsWith("Skylight glazing")) continue;
                var b = r.bounds;
                var a = transform.InverseTransformPoint(b.min);
                var c = transform.InverseTransformPoint(b.max);
                holes.Add(Rect.MinMaxRect(Mathf.Min(a.x, c.x) - .2f, Mathf.Min(a.z, c.z) - .2f, Mathf.Max(a.x, c.x) + .2f, Mathf.Max(a.z, c.z) + .2f));
            }
            const float Tile = 2f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (float x = box.min.x - 1f; x < box.max.x + 1f; x += Tile)
                for (float z = box.min.z - 1f; z < box.max.z + 1f; z += Tile)
                {
                    var centre = new Vector2(x + Tile * .5f, z + Tile * .5f);
                    bool open = false;
                    foreach (var h in holes) if (h.Contains(centre)) { open = true; break; }
                    if (open) continue;
                    Slab(vertices, triangles, new Vector3(x, top + .1f, z), new Vector3(x + Tile, top + .5f, z + Tile));
                }
            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = "Sun lid (JY-gfx)", hideFlags = HideFlags.DontSave };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            var go = new GameObject("Sun lid (JY-gfx)");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null) mr.sharedMaterial = new Material(lit) { name = "Sun lid (JY-gfx)", hideFlags = HideFlags.DontSave };
            mr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            mr.receiveShadows = false;
            return mr;
        }

        static void Slab(List<Vector3> v, List<int> t, Vector3 a, Vector3 b)
        {
            int i = v.Count;
            v.Add(new Vector3(a.x, a.y, a.z)); v.Add(new Vector3(b.x, a.y, a.z)); v.Add(new Vector3(b.x, a.y, b.z)); v.Add(new Vector3(a.x, a.y, b.z));
            v.Add(new Vector3(a.x, b.y, a.z)); v.Add(new Vector3(b.x, b.y, a.z)); v.Add(new Vector3(b.x, b.y, b.z)); v.Add(new Vector3(a.x, b.y, b.z));
            int[] f = { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 };
            foreach (var k in f) t.Add(i + k);
        }

        // The section's baked indirect light (GfxBakedLight, where one was baked) given back with it.
        void OnDestroy() => GfxBaked.Set(transform, false);

        void LateUpdate()
        {
            if (map == null) return;
            bool on = GameCourses.On && GameGfx.Upgrade;
            // Its baked indirect light on with the upgrade (GfxBaked: a lightmap per section, where one was baked).
            GfxBaked.Set(transform, on);
            var rig = map.Rig;
            foreach (var (light, kind) in ours)
            {
                if (light == null) continue;
                switch (kind)
                {
                    case Kind.Lamp: light.intensity = on ? rig.LampIntensity : 0f; light.color = rig.LampColor; break;
                    case Kind.Uplight: light.intensity = on ? rig.UplightIntensity : 0f; light.color = rig.UplightColor; break;
                    case Kind.Goal: light.intensity = on ? rig.GoalIntensity : 0f; light.color = rig.GoalColor; break;
                    case Kind.Stage: light.intensity = on ? rig.StageIntensity : 0f; light.color = rig.StageColor; light.spotAngle = rig.StageAngle; break;
                    case Kind.Heart: light.intensity = on ? rig.HeartIntensity : 0f; light.color = rig.HeartColor; light.range = rig.HeartRange; break;
                }
                light.enabled = light.intensity > 0f;
            }
            foreach (var f in fixtures)
            {
                if (f.light == null) continue;
                f.light.range = f.range * (on ? rig.FixtureReach : 1f);
                // A flickering light has its strength set again every frame (FlameFlicker.Update): scale it after
                // (only while the flicker runs, or the scaling would pile up).
                if (f.flicker != null && f.flicker.isActiveAndEnabled) f.light.intensity *= on ? rig.FixtureBoost : 1f;
                else f.light.intensity = f.intensity * (on ? rig.FixtureBoost : 1f);
            }
            foreach (var (light, line) in planned)
            {
                if (light == null) continue;
                light.intensity = on ? line.Intensity * Scale(rig, line.Layer) : 0f;
                light.enabled = light.intensity > 0f;
            }
            if (lid != null) lid.enabled = on && rig.Lid;
            Floors(on);
            foreach (var kv in floors)
                if (kv.Key != null) kv.Key.SetFloat(SmoothnessId, on && rig.FloorGloss > 0f ? Mathf.Max(kv.Value, rig.FloorGloss) : kv.Value);
            foreach (var (r, mode) in glass)
                if (r != null) r.shadowCastingMode = on && rig.ClearGlass ? ShadowCastingMode.Off : mode;
        }
    }
}
