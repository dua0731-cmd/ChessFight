using System.Collections.Generic;
using UnityEngine;
#if CF_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24): the light of Pawn Rush's courses 2 and 3. JY: "2코스는 성 내부로 막 들어간건데 ... 비교적
    /// 가볍지만 그래도 내가 멋진 성 안으로 들어갓다는 느김이 확 느겨지면 좋겟고, 3코스는 엄중하고, 압도적이거나 웅장하고,
    /// 게임의 결말같은 느김을 줄 수 잇엇으면 좋겟어" (course 1 is outside and keeps the sky and the sun).
    ///   places  every section of course 2 or 3 is a place (ZoneLight, the team's mixer): its course's sun, turned in
    ///           the section's own frame so the light comes through its windows the same way whichever way the round
    ///           laid the section, its ambient light and its haze, faded in over the corridor as the camera comes in.
    ///   look    each course's grade and light shafts are a global volume over GameGrade's, weighted by how far the
    ///           camera is inside that course's places; the sky seen over the roofless halls takes the course's tint.
    /// Sections with a light of their own (the Royal Library, the Observatory) are not places here. The PawnRush side
    /// (GfxSections) finds the sections and hands them in (Add). Off with the upgrade (GameGfx).
    /// </summary>
    public static class GameCourses
    {
        public sealed class Look
        {
            public string Name;
            /// <summary>The way the sun's light travels in the section's frame (x right, y up, z along the route).</summary>
            public Vector3 SunWay;
            public Color SunColor;
            public float SunIntensity, ShadowStrength = 1f;
            /// <summary>The ambient light from above, the sides and below (sRGB, the gradient mode's).</summary>
            public Color Sky, Equator, Ground;
            public Color FogColor;
            public float FogStart, FogEnd;
            public float Exposure, Contrast, Saturation, Temperature, BloomIntensity, BloomThreshold, Vignette;
            public float ShaftIntensity, ShaftDensity;
            public Color ShaftTint;
            /// <summary>The sky over the roofless halls, times this.</summary>
            public Color SkyTint = Color.white;
            /// <summary>How bright the light at the section's lamps, flames and chandeliers is (0 none).</summary>
            public float LampIntensity;
        }

        /// <summary>Course 2: a bright morning in the castle's first halls. Warm sun through the side windows into clean,
        /// light shade (a warm grade or warm shade turned the whole picture yellow: only the sunlight is warm).</summary>
        public static readonly Look Course2 = new Look
        {
            Name = "Course 2 (JY-gfx)",
            SunWay = new Vector3(.55f, -.64f, -.54f), SunColor = new Color(1f, .95f, .88f), SunIntensity = 2f,
            Sky = new Color(.68f, .68f, .7f), Equator = new Color(.62f, .6f, .6f), Ground = new Color(.36f, .33f, .3f),
            FogColor = new Color(.86f, .86f, .88f), FogStart = 45f, FogEnd = 420f,
            Exposure = -.05f, Contrast = 24f, Saturation = 20f, Temperature = -1f, BloomIntensity = .75f, BloomThreshold = .9f, Vignette = .14f,
            ShaftIntensity = 1.4f, ShaftDensity = .03f, ShaftTint = new Color(1f, .95f, .86f),
            LampIntensity = 2.5f,
        };

        /// <summary>Course 3: the castle's heart at dusk. Dark, cool halls; amber light falling steeply from above and
        /// ahead through the open roof in shafts; lamps and candles in warm pools. (A low sun from the far end never
        /// cleared the walls: the halls went black.)</summary>
        public static readonly Look Course3 = new Look
        {
            Name = "Course 3 (JY-gfx)",
            SunWay = new Vector3(.15f, -.85f, -.5f), SunColor = new Color(1f, .72f, .45f), SunIntensity = 3.2f,
            Sky = new Color(.36f, .36f, .52f), Equator = new Color(.3f, .27f, .38f), Ground = new Color(.15f, .12f, .15f),
            FogColor = new Color(.36f, .3f, .4f), FogStart = 6f, FogEnd = 110f,
            Exposure = -.08f, Contrast = 34f, Saturation = 14f, Temperature = -3f, BloomIntensity = 1f, BloomThreshold = .8f, Vignette = .3f,
            ShaftIntensity = 2.2f, ShaftDensity = .05f, ShaftTint = new Color(1f, .78f, .55f),
            SkyTint = new Color(.7f, .55f, .65f),
            LampIntensity = 5f,
        };

        public static bool On = true;
        /// <summary>Metres out of a section's box over which its course fades (then eased over about a second).</summary>
        public static float Blend = 12f;
        const int Priority = 25;

        sealed class Place
        {
            public Transform frame;
            public Bounds area;
            public int course;
            public ZoneLight zone;
            public readonly List<Light> lamps = new List<Light>();
        }

        static readonly List<Place> places = new List<Place>();
#if CF_URP
        static readonly Volume[] volumes = new Volume[2];
        static readonly VolumeProfile[] profiles = new VolumeProfile[2];
#endif
        static readonly int TintId = Shader.PropertyToID("_Tint"), EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>How far the camera is in course 2's places and course 3's (0 to 1).</summary>
        public static float Weight2 { get; private set; }
        public static float Weight3 { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            places.Clear();
            var go = new GameObject("Gfx courses (JY-gfx)") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Watch>();
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        /// <summary>A section of course 2 or 3: its frame, its box in that frame, its course.</summary>
        public static void Add(Transform frame, Bounds area, int course)
        {
            if (frame == null || (course != 2 && course != 3)) return;
            foreach (var p in places) if (p.frame == frame) return;
            var zone = ZoneLight.Add((course == 2 ? Course2 : Course3).Name + ": " + frame.name);
            var place = new Place { frame = frame, area = area, course = course, zone = zone };
            Lamps(place);
            places.Add(place);
        }

        // Light where the section's own lamps glow: every renderer whose material glows (emission on, bright) is a lamp;
        // lamps within 1.5 m are one light (a chandelier's flames), at most 24 a section, no shadows. Under the section,
        // so they go with it.
        static void Lamps(Place place)
        {
            var cells = new Dictionary<Vector3Int, (Vector3 sum, Color colour, int count, float top)>();
            foreach (var r in place.frame.GetComponentsInChildren<MeshRenderer>(false))
            {
                Color glow = Color.black;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !m.IsKeywordEnabled("_EMISSION") || !m.HasProperty(EmissionId)) continue;
                    var e = m.GetColor(EmissionId);
                    if (e.maxColorComponent > glow.maxColorComponent) glow = e;
                }
                if (glow.maxColorComponent < .5f) continue;
                var b = r.bounds;
                if (b.size.magnitude > 4f) continue;   // a glowing wall or floor is not a lamp
                var key = Vector3Int.FloorToInt(b.center / 1.5f);
                cells.TryGetValue(key, out var c);
                cells[key] = (c.sum + b.center, c.colour + glow / glow.maxColorComponent, c.count + 1, Mathf.Max(c.top, b.center.y));
            }
            foreach (var c in cells.Values)
            {
                if (place.lamps.Count >= 24) break;
                var go = new GameObject("Lamp light (JY-gfx)");
                go.transform.SetParent(place.frame, false);
                go.transform.position = c.sum / c.count;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                var colour = c.colour / c.count;
                light.color = Color.Lerp(colour, new Color(1f, .85f, .65f), .35f);
                // Up high (a chandelier), it reaches further down.
                light.range = 5f + Mathf.Clamp(c.top - place.frame.position.y - 3f, 0f, 8f);
                light.intensity = 0f;
                place.lamps.Add(light);
            }
        }

        // The courses' volumes, in each round scene (as GameGrade's).
        static void Loaded(Scene scene, LoadSceneMode mode)
        {
#if CF_URP
            if (mode != LoadSceneMode.Single && SceneManager.GetActiveScene() != scene) return;
            if (!GameGfx.InScope(scene)) return;
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject((i == 0 ? Course2 : Course3).Name);
                SceneManager.MoveGameObjectToScene(go, scene);
                var volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = Priority + i;
                volume.weight = 0f;
                if (profiles[i] == null) profiles[i] = Profile(i == 0 ? Course2 : Course3);
                volume.sharedProfile = profiles[i];
                volumes[i] = volume;
            }
#endif
        }
#if CF_URP

        /// <summary>A course's grade and shafts over GameGrade's (only what the course changes).</summary>
        public static VolumeProfile Profile(Look look)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = look.Name;
            var adjust = profile.Add<ColorAdjustments>(false);
            adjust.postExposure.Override(look.Exposure);
            adjust.contrast.Override(look.Contrast);
            adjust.saturation.Override(look.Saturation);
            profile.Add<WhiteBalance>(false).temperature.Override(look.Temperature);
            var bloom = profile.Add<Bloom>(false);
            bloom.intensity.Override(look.BloomIntensity);
            bloom.threshold.Override(look.BloomThreshold);
            profile.Add<Vignette>(false).intensity.Override(look.Vignette);
            var shafts = profile.Add<GfxLightShafts>(false);
            shafts.intensity.Override(look.ShaftIntensity);
            shafts.density.Override(look.ShaftDensity);
            shafts.tint.Override(look.ShaftTint);
            return profile;
        }

#endif
        /// <summary>The looks' numbers changed (the picture tool): the volumes' profiles made again.</summary>
        public static void Refresh()
        {
#if CF_URP
            for (int i = 0; i < 2; i++)
            {
                if (profiles[i] != null) Object.Destroy(profiles[i]);
                profiles[i] = Profile(i == 0 ? Course2 : Course3);
                if (volumes[i] != null) volumes[i].sharedProfile = profiles[i];
            }
#endif
        }

        static void Follow()
        {
            bool wanted = On && GameGfx.Upgrade && GameGfx.InScope();
            float w2 = 0f, w3 = 0f;
            for (int i = places.Count - 1; i >= 0; i--)
            {
                var p = places[i];
                if (p.frame == null)
                {
                    p.zone.Remove();
                    places.RemoveAt(i);
                    continue;
                }
                var look = p.course == 2 ? Course2 : Course3;
                foreach (var lamp in p.lamps)
                    if (lamp != null) lamp.intensity = wanted ? look.LampIntensity : 0f;
                var z = p.zone;
                z.TurnSun = true;
                z.SunRotation = Quaternion.LookRotation(p.frame.TransformDirection(look.SunWay.normalized), Vector3.up);
                z.SunColor = look.SunColor;
                z.SunIntensity = look.SunIntensity;
                z.ShadowStrength = look.ShadowStrength;
                z.Sky = look.Sky;
                z.Equator = look.Equator;
                z.Ground = look.Ground;
                z.Fog = true;
                z.FogColor = look.FogColor;
                z.FogStart = look.FogStart;
                z.FogEnd = look.FogEnd;
                float w = z.Ease(wanted ? ZoneLight.Inside(p.frame, p.area, Blend) : 0f);
                if (p.course == 2) w2 = Mathf.Max(w2, w);
                else w3 = Mathf.Max(w3, w);
            }
            Weight2 = w2;
            Weight3 = w3;
#if CF_URP
            if (volumes[0] != null) volumes[0].weight = w2;
            if (volumes[1] != null) volumes[1].weight = w3;
#endif
            // The sky over the roofless halls (GameSky's own copy of the sky material only).
            var sky = RenderSettings.skybox;
            if (sky != null && sky.name == "Gfx sky" && sky.HasProperty(TintId))
            {
                var tint = Color.Lerp(Color.white, Course2.SkyTint, w2);
                tint = Color.Lerp(tint, Course3.SkyTint, w3);
                sky.SetColor(TintId, tint);
            }
        }

        sealed class Watch : MonoBehaviour
        {
            void LateUpdate() => Follow();
        }
    }
}
