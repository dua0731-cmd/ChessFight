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
            /// <summary>Every planned light's colour times this (a whole plan made cooler or warmer at once).</summary>
            public Color PlanTint = Color.white;
            /// <summary>A section that lights itself (Map.OwnLook): the component of its own light, by type name, and fields of
            /// it set while the upgrade is on ("field=value;field=r|g|b"; put back when it is off), and its lights to put out
            /// (names, comma-separated).</summary>
            public string OwnType = "", OwnSet = "", OwnLightsOff = "";
            /// <summary>Shadows back on for every part whose own light code turned them off (glass, glows and effects
            /// aside), so the walls and the dome block light again (and the bake sees them).</summary>
            public bool ShadowsOn;
            /// <summary>A part that glows (its name's start): its material copied with this emission (times GlowIntensity, the
            /// base map as the emission map) and baked as a light, e.g. a starry dome as the room's sky.</summary>
            public string GlowPart = "";
            public Color GlowColor = Color.white;
            public float GlowIntensity;
            /// <summary>The glow's own emission map (a Resources path), e.g. only the stars of a night sky; empty: the base map.</summary>
            public string GlowMap = "";
            /// <summary>A moon where the section's moonlight comes from (its own light's dome point and MoonDirection, else
            /// the scene's directional light): a disc MoonSize metres across (GfxMoon), MoonDistance metres out from that
            /// point (below 0: inside a dome that is itself the night sky, as the observatory's; JY: "맵에 달과 거기서부터
            /// 빛이 새어나와서 맵에 빛줄기가 비춰지도록").</summary>
            public bool Moon;
            public float MoonDistance = 160f, MoonSize = 26f, MoonIntensity = 5f;
            public Color MoonColor = new Color(.86f, .92f, 1f);
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
            /// <summary>Offset and Aim turned to the hall's middle seen from above: x toward it, y up, z along the wall (a lamp
            /// in front of each statue round a rotunda, aimed back at it).</summary>
            public bool Radial;
            public int Max = 64;
            public float Spacing;
            /// <summary>A spot's way in the section's frame (its x toward the middle when Inward).</summary>
            public Vector3 Aim = Vector3.down;
            public float Intensity = 1f, Range = 10f, Angle = 60f, Inner = 40f;
            public Color Color = Color.white;
            public bool Shadows;
            /// <summary>Above 0: a spot's beam seen in the haze (a soft cone to where its light lands, GfxBeam), this bright.</summary>
            public float Beam;
            /// <summary>The share of its light that bounces in the bake (1 as real; lower keeps the light on what it is aimed
            /// at, for drama).</summary>
            public float Bounce = 1f;
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
            /// <summary>With OwnLook: the look's grade over the section's own all the same (a night of our own over theirs).</summary>
            public bool OwnGrade;
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
            new Map
            {
                Key = "3-2_RoyalObservatory", OwnLook = true, OwnGrade = true,
                Concept = "the night observatory (JY's own concept, 11-24): the runners' path lit from above like a stage under the night, the galleries round it as dark as a real night except where their lamps glow, and the starry dome itself the room's sky, its blue light falling over the whole hall",
                Look = new GameCourses.Look
                {
                    Name = "Royal observatory", Indoor = 1f, Bounce = 0f, BounceTint = new Color(1f, 1f, 1f), Exposure = .15f, Contrast = 30f, Saturation = 14f, BloomIntensity = 1.1f, BloomThreshold = .75f, Vignette = .24f, ShaftIntensity = .45f, ShaftDensity = .012f, ShaftTint = new Color(.75f, .85f, 1f), Temperature = -18f, Tint = 6f, ShadowsLift = -.04f, BloomScatter = .78f, ShadowsTone = new Color(.86f, .9f, 1.12f), HighlightsTone = new Color(1.03f, 1f, .96f), BloomTint = new Color(.78f, .86f, 1f),
                },
                Rig = new Rig { Lid = false, ClearGlass = true, FixtureBoost = 1f, FixtureReach = .7f, LampIntensity = 0f, UplightIntensity = 0f, GoalIntensity = 0f, StageIntensity = 0f, HeartIntensity = 0f, FloorGloss = 0f, OwnType = "RoyalObservatoryLighting", OwnSet = "moonIntensity=1.8;warmFill=0;ambientSky=.025|.03|.08;ambientEquator=.012|.015|.035;ambientGround=.006|.006|.015;hazeStart=30;hazeEnd=320;hazeColor=.01|.015|.04;shaftStrength=.08", OwnLightsOff = "Warm fill", ShadowsOn = true, GlowPart = "RoHall dome sky", GlowColor = new Color(1f, 1f, 1f), GlowIntensity = 2f, GlowMap = "Gfx/Glow/RO_DomeSky_Glow", Moon = true, MoonDistance = -2f, MoonSize = 9f, MoonIntensity = 2.5f, MoonColor = new Color(.86f, .92f, 1f) },
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
            // A section that lights itself keeps its own light; only the look's shafts go over it (and its grade, OwnGrade).
            GameCourses.Add(frame, box, map.Look, !map.OwnLook, !map.OwnLook || map.OwnGrade);
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
        readonly List<(Renderer beam, Light light)> beams = new List<(Renderer, Light)>();
        // A section that lights itself (Rig.OwnType): its own light's component, its fields as they were, its lights put out;
        // the parts whose shadows come back (Rig.ShadowsOn); the glowing part's materials (Rig.GlowPart). Each set once
        // when the upgrade comes on and put back when it goes off.
        MonoBehaviour own;
        readonly Dictionary<System.Reflection.FieldInfo, object> ownWas = new Dictionary<System.Reflection.FieldInfo, object>();
        readonly List<Light> ownOff = new List<Light>();
        readonly List<Renderer> shadowsBack = new List<Renderer>();
        readonly List<(Renderer renderer, Material[] was, Material[] glow)> glows = new List<(Renderer, Material[], Material[])>();
        bool ownOn;
        string ownSet = "";
        Renderer moon;
        /// <summary>The moon (Rig.Moon), where its light comes in and the way it goes (for the picture tool's view of it).</summary>
        public bool HasMoon => moon != null;
        public Vector3 MoonThrough { get; private set; }
        public Vector3 MoonWay { get; private set; }

        /// <summary>What the rig built and changed, in a line (the picture tool's notes).</summary>
        public string Notes()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string s = $"{ours.Count} lights of its own, {planned.Count} plan lights, {beams.Count} beams, {fixtures.Count} fixtures";
            if (own != null) s += $"; own light {own.GetType().Name} ({ownWas.Count} fields set, {ownOff.Count} lights out)";
            if (shadowsBack.Count > 0) s += $"; shadows back on {shadowsBack.Count} parts";
            if (glows.Count > 0) s += $"; {glows.Count} parts glowing";
            if (moon != null)
                s += string.Format(inv, "; moon at {0} ({1}), {2:0.0} m wide, through {3}, way {4}", moon.transform.position, moon.enabled ? "on" : "off",
                    moon.transform.lossyScale.x, MoonThrough, MoonWay);
            else if (map != null && map.Rig.Moon) s += "; moon wanted but not made";
            return s;
        }

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

            // A section that lights itself: what of its own light the map changes.
            Own();

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
                var points = new List<(Vector3 at, float side, Vector3 toward)>();
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
                        var toward = Toward(p, box);
                        p += Turn(line.Offset, line, side, toward);
                        bool near = false;
                        if (line.Spacing > 0f)
                            foreach (var q in points)
                                if (Vector3.Distance(q.at, p) < line.Spacing) { near = true; break; }
                        if (near) continue;
                        points.Add((p, side, toward));
                        if (points.Count >= line.Max) break;
                    }
                }
                else if (line.Count > 1)
                    for (int i = 0; i < line.Count; i++)
                    {
                        var p = Vector3.Lerp(line.At, line.To, i / (line.Count - 1f));
                        points.Add((p, p.x < box.center.x ? 1f : -1f, Toward(p, box)));
                    }
                else points.Add((line.At, line.At.x < box.center.x ? 1f : -1f, Toward(line.At, box)));
                foreach (var (at, side, toward) in points)
                {
                    var aim = Turn(line.Aim, line, side, toward);
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
                    l.bounceIntensity = line.Bounce;
                    l.intensity = 0f;
                    planned.Add((l, line));
                    if (line.Type == LightType.Spot && line.Beam > 0f)
                    {
                        var beam = Beam(go.transform, line, l);
                        if (beam != null) beams.Add((beam, l));
                    }
                }
            }
        }

        static Shader beamShader;

        // A spot's beam in the haze: a soft cone from the light to where it lands (the first thing in its way), drawn added.
        Renderer Beam(Transform light, GfxMapLights.Fixture line, Light l)
        {
            if (beamShader == null) beamShader = Shader.Find("ChessFight/Gfx Beam");
            if (beamShader == null) return null;
            float length = l.range * .85f;
            if (Physics.Raycast(light.position, light.forward, out var hit, l.range, ~0, QueryTriggerInteraction.Ignore)) length = Mathf.Max(.5f, hit.distance);
            float radius = length * Mathf.Tan(l.spotAngle * .5f * Mathf.Deg2Rad) * .8f;
            const int N = 24;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                var round = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var normal = (round * length - Vector3.forward * radius).normalized;
                vertices.Add(round * .06f);
                normals.Add(normal);
                uv.Add(new Vector2(i / (float)N, 0f));
                vertices.Add(round * radius + Vector3.forward * length);
                normals.Add(normal);
                uv.Add(new Vector2(i / (float)N, 1f));
            }
            for (int i = 0; i < N; i++)
            {
                int a0 = i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                triangles.Add(a0); triangles.Add(b0); triangles.Add(a1);
                triangles.Add(a1); triangles.Add(b0); triangles.Add(b1);
            }
            var mesh = new Mesh { name = "Beam (JY-gfx)", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("Beam (JY-gfx)");
            go.transform.SetParent(light, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var m = new Material(beamShader) { name = "Beam (JY-gfx)", hideFlags = HideFlags.DontSave };
            m.SetColor("_Color", line.Color);
            m.SetFloat("_Strength", line.Beam);
            r.sharedMaterial = m;
            r.enabled = false;
            return r;
        }

        // What of a section's own light the map changes: found once (Build).
        void Own()
        {
            var rig = map.Rig;
            if (!string.IsNullOrEmpty(rig.OwnType))
                foreach (var mb in GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && mb.GetType().Name == rig.OwnType) { own = mb; break; }
            if (!string.IsNullOrEmpty(rig.OwnLightsOff))
            {
                var names = new HashSet<string>(rig.OwnLightsOff.Split(','));
                foreach (var l in GetComponentsInChildren<Light>(true))
                    if (names.Contains(l.name.Trim())) ownOff.Add(l);
            }
            if (rig.ShadowsOn)
                foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.shadowCastingMode != ShadowCastingMode.Off || r.name.EndsWith("(JY-gfx)")) continue;
                    var n = r.name.ToLowerInvariant();
                    if (n.Contains("glass") || n.Contains("glow") || n.Contains("shaft") || n.Contains("beam") || n.Contains("halo") || n.Contains("fx")) continue;
                    bool clear = false;
                    foreach (var m in r.sharedMaterials)
                        if (m != null && (m.renderQueue >= 2450 || (m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent >= .5f))) { clear = true; break; }
                    if (!clear) shadowsBack.Add(r);
                }
            if (!string.IsNullOrEmpty(rig.GlowPart) && rig.GlowIntensity > 0f)
                foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!r.name.StartsWith(rig.GlowPart, System.StringComparison.Ordinal)) continue;
                    var was = r.sharedMaterials;
                    var glow = new Material[was.Length];
                    for (int i = 0; i < was.Length; i++)
                    {
                        if (was[i] == null) continue;
                        var m = new Material(was[i]) { name = was[i].name + " (glow)", hideFlags = HideFlags.DontSave };
                        m.EnableKeyword("_EMISSION");
                        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", rig.GlowColor * rig.GlowIntensity);
                        var glowMap = string.IsNullOrEmpty(rig.GlowMap) ? null : Resources.Load<Texture2D>(rig.GlowMap);
                        if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", glowMap != null ? glowMap : m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null);
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                        glow[i] = m;
                    }
                    glows.Add((r, was, glow));
                }
            if (rig.Moon) moon = Moon();
        }

        static Shader moonShader;

        // The moon: where its light comes in (the section's own light's slit, MoonDirection; else the scene's directional
        // light through the section's middle), MoonDistance further out, facing in.
        Renderer Moon()
        {
            var rig = map.Rig;
            if (moonShader == null) moonShader = Shader.Find("ChessFight/Gfx Moon");
            if (moonShader == null) return null;
            const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            Vector3 way = Vector3.zero, through = Vector3.zero;
            bool found = false;
            if (own != null)
            {
                var type = own.GetType();
                var wayOf = type.GetMethod("MoonDirection", Any);
                var dome = type.GetMethod("DomePoint", Any);
                var heading = type.GetField("moonHeading", Any);
                var elevation = type.GetField("moonElevation", Any);
                if (wayOf != null && dome != null && heading != null && elevation != null)
                {
                    way = (Vector3)wayOf.Invoke(own, null);
                    var local = (Vector3)dome.Invoke(null, new object[] { (float)heading.GetValue(own), (float)elevation.GetValue(own) });
                    through = own.transform.TransformPoint(local);
                    found = true;
                }
            }
            if (!found)
            {
                var sun = RenderSettings.sun;
                if (sun == null) return null;
                way = sun.transform.forward;
                through = GetComponentInChildren<Renderer>() != null ? transform.position : transform.position;
            }
            way = way.normalized;
            MoonThrough = through;
            MoonWay = way;
            var mesh = new Mesh { name = "Moon (JY-gfx)", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(new List<Vector3> { new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f), new Vector3(.5f, .5f, 0f), new Vector3(-.5f, .5f, 0f) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("Moon (JY-gfx)");
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(through - way * rig.MoonDistance, Quaternion.LookRotation(way));
            // The disc's radius is 0.24 of the quad's half-width (GfxMoon _Disc): a quad MoonSize / 0.24 wide makes the
            // disc MoonSize across, with room for the halo.
            go.transform.localScale = Vector3.one * (rig.MoonSize / .24f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var m = new Material(moonShader) { name = "Moon (JY-gfx)", hideFlags = HideFlags.DontSave };
            m.SetColor("_Color", rig.MoonColor * rig.MoonIntensity);
            r.sharedMaterial = m;
            r.enabled = false;
            return r;
        }

        // The section's own light as the map wants it while the upgrade is on, and back as it was when it is off: its fields
        // set again whenever the map's OwnSet changes (the picture tool trying others; "field=value" items split by ';' or
        // '&'), the glow and the moon kept in step with the rig's numbers.
        void OwnOn(bool on)
        {
            var rig = map.Rig;
            // Its lights stay out while on (its code may set them again when its weight moves).
            if (on) foreach (var l in ownOff) if (l != null) l.intensity = 0f;
            string set = on && own != null ? rig.OwnSet ?? "" : "";
            if (set != ownSet)
            {
                foreach (var kv in ownWas) kv.Key.SetValue(own, kv.Value);
                ownWas.Clear();
                ownSet = set;
                foreach (var item in set.Split(';', '&'))
                {
                    int eq = item.IndexOf('=');
                    if (eq <= 0) continue;
                    var f = own.GetType().GetField(item.Substring(0, eq).Trim());
                    if (f == null) continue;
                    var value = Parse(item.Substring(eq + 1).Trim(), f.FieldType);
                    if (value == null) continue;
                    if (!ownWas.ContainsKey(f)) ownWas[f] = f.GetValue(own);
                    f.SetValue(own, value);
                }
            }
            if (on)
            {
                var emission = rig.GlowColor * rig.GlowIntensity;
                foreach (var (_, _, glow) in glows)
                    foreach (var m in glow) if (m != null && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
                if (moon != null && moon.sharedMaterial != null) moon.sharedMaterial.SetColor("_Color", rig.MoonColor * rig.MoonIntensity);
            }
            if (on == ownOn) return;
            ownOn = on;
            foreach (var r in shadowsBack) if (r != null) r.shadowCastingMode = on ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (var (r, was, glow) in glows) if (r != null) r.sharedMaterials = on ? glow : was;
            if (moon != null) moon.enabled = on;
        }

        static object Parse(string text, System.Type type)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var parts = text.Split('|');
            float P(int i) => float.Parse(parts[i], System.Globalization.NumberStyles.Float, inv);
            if (type == typeof(float)) return P(0);
            if (type == typeof(int)) return Mathf.RoundToInt(P(0));
            if (type == typeof(bool)) return text == "1" || text == "true";
            if (type == typeof(string)) return text;
            if (type == typeof(Color)) return new Color(P(0), P(1), P(2), parts.Length > 3 ? P(3) : 1f);
            if (type == typeof(Vector3)) return new Vector3(P(0), P(1), P(2));
            return null;
        }

        // The level way from a point to the hall's middle (the section's box centre).
        static Vector3 Toward(Vector3 p, Bounds box)
        {
            var d = box.center - p;
            d.y = 0f;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
        }

        // A plan line's offset or aim as meant: when Radial, x toward the middle, y up, z along the wall; when Inward, its x
        // toward the middle; else as written.
        static Vector3 Turn(Vector3 v, GfxMapLights.Fixture line, float side, Vector3 toward)
        {
            if (line.Radial) return toward * v.x + Vector3.up * v.y + Vector3.Cross(Vector3.up, toward) * v.z;
            if (line.Inward) v.x *= side;
            return v;
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
            OwnOn(on);
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
                light.color = line.Color * rig.PlanTint;
                light.intensity = on ? line.Intensity * Scale(rig, line.Layer) : 0f;
                light.enabled = light.intensity > 0f;
            }
            foreach (var (beam, light) in beams)
                if (beam != null) beam.enabled = light != null && light.enabled;
            if (lid != null) lid.enabled = on && rig.Lid;
            Floors(on);
            foreach (var kv in floors)
                if (kv.Key != null) kv.Key.SetFloat(SmoothnessId, on && rig.FloorGloss > 0f ? Mathf.Max(kv.Value, rig.FloorGloss) : kv.Value);
            foreach (var (r, mode) in glass)
                if (r != null) r.shadowCastingMode = on && rig.ClearGlass ? ShadowCastingMode.Off : mode;
        }
    }
}
