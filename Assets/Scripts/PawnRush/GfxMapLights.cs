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
    /// a concept of its own (Maps), from what the map is: a look (GameCourses: its sun through its windows, ambient,
    /// haze, grade, sky) and a rig built from its own parts:
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
        }

        public sealed class Map
        {
            /// <summary>In the section's name.</summary>
            public string Key;
            /// <summary>The map's light in words.</summary>
            public string Concept;
            public GameCourses.Look Look;
            public Rig Rig;
        }

        // ------------------------------------------------------------------ the maps

        static GameCourses.Look L(string name) => new GameCourses.Look { Name = name };

        /// <summary>Course 2's grey stand-ins and unknown maps: a plain bright interior.</summary>
        public static readonly Map Course2Default = new Map
        {
            Key = "", Concept = "a plain bright interior",
            Look = new GameCourses.Look
            {
                Name = "Course 2", SunWay = new Vector3(.6f, -.62f, -.5f), SunColor = new Color(1f, .97f, .92f), SunIntensity = 2f,
                Sky = new Color(.6f, .6f, .62f), Equator = new Color(.55f, .53f, .52f), Ground = new Color(.32f, .3f, .28f),
                FogColor = new Color(.82f, .82f, .84f), FogStart = 40f, FogEnd = 360f,
            },
            Rig = new Rig(),
        };

        /// <summary>Course 3's grey stand-ins and unknown maps: a plain darker interior.</summary>
        public static readonly Map Course3Default = new Map
        {
            Key = "", Concept = "a plain darker interior",
            Look = new GameCourses.Look
            {
                Name = "Course 3", SunWay = new Vector3(.2f, -.85f, -.45f), SunColor = new Color(1f, .96f, .9f), SunIntensity = 1.6f,
                Sky = new Color(.26f, .26f, .32f), Equator = new Color(.22f, .21f, .24f), Ground = new Color(.11f, .1f, .11f),
                FogColor = new Color(.3f, .3f, .34f), FogStart = 8f, FogEnd = 140f,
                Exposure = -.1f, Contrast = 30f, Saturation = 14f, BloomIntensity = .9f, BloomThreshold = .85f, Vignette = .26f,
                ShaftIntensity = 2f, ShaftDensity = .05f, SkyTint = new Color(.55f, .58f, .68f),
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
                    Name = "Grand stair hall", SunWay = new Vector3(.3f, -.82f, -.48f), SunColor = new Color(1f, .97f, .92f), SunIntensity = 2.2f,
                    Sky = new Color(.62f, .6f, .6f), Equator = new Color(.55f, .5f, .48f), Ground = new Color(.4f, .3f, .28f),
                    FogColor = new Color(.85f, .84f, .84f), FogStart = 40f, FogEnd = 320f,
                    Exposure = 0f, Contrast = 24f, Saturation = 18f, BloomIntensity = .8f, BloomThreshold = .9f, Vignette = .14f,
                    ShaftIntensity = 1.4f, ShaftDensity = .03f,
                },
                Rig = new Rig { UplightColor = new Color(1f, .9f, .78f), Lid = false, FixtureBoost = 1.5f, FixtureReach = 1.3f, LampIntensity = 2.5f, UplightIntensity = 2f, UplightMax = 16, GoalIntensity = 0f },
            },
            new Map
            {
                Key = "MG_B_RepairWing",
                Concept = "the royal workshop: daylight pouring in from above as through a skylight, sawdust haze, warm work-lamp pools over the benches and the pit, warm light washing up the columns",
                Look = new GameCourses.Look
                {
                    Name = "Repair wing", SunWay = new Vector3(.35f, -.85f, -.4f), SunColor = new Color(.95f, .97f, 1f), SunIntensity = 2.2f,
                    Sky = new Color(.72f, .7f, .7f), Equator = new Color(.64f, .6f, .58f), Ground = new Color(.4f, .35f, .32f),
                    FogColor = new Color(.7f, .68f, .66f), FogStart = 15f, FogEnd = 200f,
                    Exposure = .1f, Contrast = 28f, Saturation = 16f, ShaftIntensity = 1.6f, ShaftDensity = .045f,
                },
                Rig = new Rig { FloorGloss = .75f, UplightMax = 20, UplightIntensity = 4f, Lid = false, FixtureBoost = 3f, FixtureReach = 1.6f, LampIntensity = 4f, GoalIntensity = 2.5f },
            },
            new Map
            {
                Key = "MG_E_RoyalLibrary",
                Concept = "the bright reading room: cream daylight from above between the bookshelf towers, warm reading lamps and sconces, warm light up the columns, a polished floor holding the lamps",
                Look = new GameCourses.Look
                {
                    Name = "Reading room", SunWay = new Vector3(.35f, -.85f, -.4f), SunColor = new Color(1f, .95f, .86f), SunIntensity = 2.2f,
                    Sky = new Color(.72f, .7f, .7f), Equator = new Color(.64f, .6f, .58f), Ground = new Color(.4f, .35f, .32f),
                    FogColor = new Color(.76f, .74f, .72f), FogStart = 12f, FogEnd = 180f,
                    Exposure = .1f, Contrast = 26f, Saturation = 18f, ShaftIntensity = 1.8f, ShaftDensity = .05f, ShaftTint = new Color(1f, .95f, .85f),
                },
                Rig = new Rig { FloorGloss = .75f, UplightMax = 20, UplightIntensity = 4f, Lid = false, FixtureBoost = 3f, FixtureReach = 1.6f, LampIntensity = 4f, GoalIntensity = 2.5f },
            },
            new Map
            {
                Key = "MG_F_ClockworkBallroom",
                Concept = "the glamour ballroom: the chandeliers are the stars, great warm pools on the dance floor and sparkle, gold columns lit from below, cool daylight from above, a polished floor doubling the lamps",
                Look = new GameCourses.Look
                {
                    Name = "Ballroom", SunWay = new Vector3(.35f, -.85f, -.4f), SunColor = new Color(.92f, .95f, 1f), SunIntensity = 2.2f,
                    Sky = new Color(.72f, .7f, .7f), Equator = new Color(.64f, .6f, .58f), Ground = new Color(.4f, .35f, .32f),
                    FogColor = new Color(.78f, .78f, .82f), FogStart = 25f, FogEnd = 260f,
                    Exposure = .1f, Contrast = 24f, Saturation = 20f, BloomIntensity = .95f, BloomThreshold = .8f, ShaftIntensity = 1.2f,
                },
                Rig = new Rig { FloorGloss = .85f, Lid = false, FixtureBoost = 4f, FixtureReach = 1.6f, LampIntensity = 4f, UplightIntensity = 4f, UplightMax = 20, GoalIntensity = 3f },
            },
            new Map
            {
                Key = "MG_J_PalaceLaundry",
                Concept = "the fresh washhouse: clean cool daylight from above into steam over the pools, the water's light on the tiles, warm sconces by the doors",
                Look = new GameCourses.Look
                {
                    Name = "Laundry", SunWay = new Vector3(.35f, -.85f, -.4f), SunColor = new Color(.95f, .98f, 1f), SunIntensity = 2.2f,
                    Sky = new Color(.72f, .7f, .7f), Equator = new Color(.64f, .6f, .58f), Ground = new Color(.4f, .35f, .32f),
                    FogColor = new Color(.86f, .9f, .92f), FogStart = 10f, FogEnd = 160f,
                    Exposure = .1f, Contrast = 20f, Saturation = 18f, ShaftIntensity = 1.4f, ShaftDensity = .04f, ShaftTint = new Color(.95f, .98f, 1f),
                },
                Rig = new Rig { FloorGloss = .75f, UplightMax = 20, UplightIntensity = 4f, Lid = false, FixtureBoost = 3f, FixtureReach = 1.6f, LampIntensity = 4f, GoalIntensity = 2.5f },
            },
            // ---- course 3: the castle's heart, solemn and grand, each hall its own
            new Map
            {
                Key = "MG_G_RoyalProcession",
                Concept = "the ceremonial nave: columns of light falling from the high skylights onto the carpet, gold light rising up every column into the dark, indigo hush around, the far door the brightest",
                Look = new GameCourses.Look
                {
                    Name = "Procession nave", SunWay = new Vector3(.1f, -.92f, -.38f), SunColor = new Color(1f, .97f, .92f), SunIntensity = 3f,
                    Sky = new Color(.32f, .32f, .42f), Equator = new Color(.28f, .26f, .32f), Ground = new Color(.14f, .13f, .15f),
                    FogColor = new Color(.26f, .26f, .34f), FogStart = 6f, FogEnd = 120f,
                    Exposure = -.02f, Contrast = 32f, Saturation = 16f, BloomIntensity = 1f, BloomThreshold = .8f, Vignette = .3f,
                    ShaftIntensity = 2.6f, ShaftDensity = .06f, SkyTint = new Color(.5f, .52f, .62f),
                },
                Rig = new Rig { LampIntensity = 6f, UplightIntensity = 9f, UplightColor = new Color(1f, .75f, .45f), UplightMax = 16, GoalIntensity = 8f },
            },
            new Map
            {
                Key = "MG_H_KingsChessHall",
                Concept = "the king's game: the board as a stage under one great pool of light, long window grids across it, the hall's edges falling away into darkness",
                Look = new GameCourses.Look
                {
                    Name = "King's chess hall", SunWay = new Vector3(.78f, -.5f, -.37f), SunColor = new Color(1f, .96f, .88f), SunIntensity = 2.6f,
                    Sky = new Color(.32f, .32f, .42f), Equator = new Color(.28f, .26f, .32f), Ground = new Color(.14f, .13f, .15f),
                    FogColor = new Color(.28f, .27f, .32f), FogStart = 8f, FogEnd = 130f,
                    Exposure = -.02f, Contrast = 30f, Saturation = 16f, BloomIntensity = .9f, BloomThreshold = .85f, Vignette = .28f,
                    ShaftIntensity = 2.2f, ShaftDensity = .05f, SkyTint = new Color(.55f, .56f, .64f),
                },
                Rig = new Rig { LampIntensity = 5f, UplightIntensity = 4f, UplightMax = 10, GoalIntensity = 4f, StageIntensity = 8f },
            },
            new Map
            {
                Key = "MG_I_KingsBedchamber",
                Concept = "the royal bedchamber: an intimate room in deep plum and red, the canopy bed glowing warm at its heart, soft daylight through the curtained windows, the lasers bright in the dim",
                Look = new GameCourses.Look
                {
                    Name = "Bedchamber", SunWay = new Vector3(.7f, -.55f, -.45f), SunColor = new Color(.85f, .9f, 1f), SunIntensity = 1.4f,
                    Sky = new Color(.32f, .32f, .42f), Equator = new Color(.28f, .26f, .32f), Ground = new Color(.14f, .13f, .15f),
                    FogColor = new Color(.26f, .2f, .26f), FogStart = 8f, FogEnd = 110f,
                    Exposure = -.02f, Contrast = 30f, Saturation = 18f, BloomIntensity = 1f, BloomThreshold = .8f, Vignette = .32f,
                    ShaftIntensity = 1.6f, ShaftDensity = .045f, ShaftTint = new Color(.9f, .94f, 1f), SkyTint = new Color(.5f, .5f, .6f),
                },
                Rig = new Rig { LampIntensity = 5f, GoalIntensity = 3f, HeartWord = "bed", HeartIntensity = 5f, HeartRange = 10f },
            },
            new Map
            {
                Key = "WarRoom",
                Concept = "the war council before the end: a dark Gothic hall, lanterns marching along the bridge, cool light high through the lancets, the final golden door blazing",
                Look = new GameCourses.Look
                {
                    Name = "War room", SunWay = new Vector3(.6f, -.6f, -.53f), SunColor = new Color(.85f, .9f, 1f), SunIntensity = 1.8f,
                    Sky = new Color(.3f, .31f, .42f), Equator = new Color(.28f, .26f, .32f), Ground = new Color(.14f, .13f, .15f),
                    FogColor = new Color(.24f, .24f, .3f), FogStart = 8f, FogEnd = 140f,
                    Exposure = -.02f, Contrast = 32f, Saturation = 16f, BloomIntensity = 1f, BloomThreshold = .8f, Vignette = .3f,
                    ShaftIntensity = 2.4f, ShaftDensity = .06f, ShaftTint = new Color(.92f, .95f, 1f), SkyTint = new Color(.45f, .48f, .6f),
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

        // ------------------------------------------------------------------ dressing a section

        /// <summary>A course 2 or 3 section: its look in place and its rig built.</summary>
        public static void Dress(PawnRushSection section, int course)
        {
            if (section == null || section.GetComponent<GfxMapRig>() != null) return;
            var map = For(section.name, course);
            var frame = section.transform;
            if (!Box(frame, out var box)) return;
            GameCourses.Add(frame, box, map.Look);
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
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        readonly List<Fixture> fixtures = new List<Fixture>();
        readonly List<(Renderer renderer, ShadowCastingMode mode)> glass = new List<(Renderer, ShadowCastingMode)>();
        Renderer lid;

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
            LateUpdate();
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

        void LateUpdate()
        {
            if (map == null) return;
            bool on = GameCourses.On && GameGfx.Upgrade;
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
            if (lid != null) lid.enabled = on && rig.Lid;
            foreach (var kv in floors)
                if (kv.Key != null) kv.Key.SetFloat(SmoothnessId, on && rig.FloorGloss > 0f ? Mathf.Max(kv.Value, rig.FloorGloss) : kv.Value);
            foreach (var (r, mode) in glass)
                if (r != null) r.shadowCastingMode = on && rig.ClearGlass ? ShadowCastingMode.Off : mode;
        }
    }
}
