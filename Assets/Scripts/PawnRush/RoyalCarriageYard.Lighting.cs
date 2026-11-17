using System;
using System.Collections.Generic;
using System.Globalization;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R683 (승규's carriage yard at its best: the light, the air and the lamps; the models are other sessions'): the yard's
    // own light rig, made in code at the end of Awake under "Light and air". The look is the concept's (R676 PROMPT.md,
    // images 1-6): bright clear midday, a blue sky with a few soft clouds, short soft shadows, warm white light,
    // turquoise water with the sky in it and a slight bloom on the glowing cues; every lantern but the coaches' and the
    // hoists' is unlit by day, so the lamp posts' and sheds' lights are a warm glow round their glass and no more.
    //
    //   the sun            the scene's own directional light, warm white; turned to the yard's (high, from the
    //                      south-east behind the runners) only for the whole map - as a section the round's sun keeps
    //                      its way and only its colour, strength and shadow darkness change
    //   two fills          a cool sky light opposite the sun (the shade side reads blue-grey, not black) and a warm
    //                      bounce up from the sunlit paving (eaves, coach bellies, jibs); directional, no shadows
    //   ambient            sky / horizon / ground: blue overhead, the cream stone's warmth round the sides
    //   haze               linear fog in the sky's horizon colour: the far palace and towers step back
    //   sky                the scene's sky (the Pawn Rush sky or Unity's default) as a copy: fewer clouds, the sun's
    //                      glow where the sun is
    //   grade              ZoneGrade (Game): a touch more contrast and colour on the clay, cool shadows, warm highlights,
    //                      a softer, warmer bloom, a light vignette - over GameLook's, so the 빛 번짐 switch still wins
    //   reflection probes  one box per zone, drawn once 2.5 s after the build (the water and the gold trims)
    //   lamps              a point light in each lamp post's and storage shed's lamp, one at each coach's lamps that goes
    //                      amber / red with them, one at each loft hoist's lamp (green / amber / red); and any torches,
    //                      lanterns or lit windows Resources/PawnRushCarriageYard/RcyLights.txt places (none yet)
    //
    // Everything fades in by the camera's distance from the yard (YardAtmosphere) and is put back as it leaves; the
    // quality settings take parts away (YardAtmosphere.Tier: 0 low - grade, sun, ambient and haze only; 1 medium - the
    // sky fill, probes at 64, lamps within 32 m; 2 high - both fills, probes at 128, every lamp within 48 m). No light
    // probes: the yard is built at run time and Unity bakes those only in the editor; the ambient and the fills stand in.
    // Off: Light and air → on (the scene's light as it was). Costs and what to look at: Docs/PawnRush/RCY_MODELS.md
    // "R683 조명".
    public partial class RoyalCarriageYard
    {
        [Serializable]
        public sealed class LightSettings
        {
            [Tooltip("The yard's own sun colour, sky light, haze, sky, grade, probes and lamps (off: the scene's light as it was)")]
            public bool on = true;
            [Tooltip("Turn the scene's sun to the yard's way. The whole map always does; as a section only with this on "
                     + "(the round's sun is every room's, and a sun that swings as you walk in reads as a fault)")]
            public bool turnSunAsSection = false;
            [Tooltip("The sun's height over the horizon (deg): the concept's midday, short shadows")]
            [Range(30f, 80f)] public float sunHeight = 58f;
            [Tooltip("The way the light goes (deg about y, 0 = north): -40 comes from the south-east, behind and to the "
                     + "right of the runners, so what they run at is lit")]
            public float sunYaw = -40f;
            public Color sunColor = new Color(1f, .955f, .88f);
            [Range(0f, 4f)] public float sunIntensity = 1.85f;
            [Tooltip("How dark the sun's shadows are (1 = only the ambient light in them)")]
            [Range(0f, 1f)] public float shadowStrength = .82f;

            [Header("Sky light")]
            public Color ambientSky = new Color(.5f, .6f, .78f);
            public Color ambientHorizon = new Color(.6f, .58f, .54f);
            public Color ambientGround = new Color(.4f, .36f, .31f);
            [Tooltip("Cool fill from the sky opposite the sun (directional, no shadows; medium and high)")]
            [Range(0f, 1f)] public float skyFill = .22f;
            public Color skyFillColor = new Color(.62f, .74f, 1f);
            [Tooltip("Warm bounce up from the sunlit paving (directional, no shadows; high only)")]
            [Range(0f, 1f)] public float bounceFill = .12f;
            public Color bounceFillColor = new Color(1f, .86f, .68f);

            [Header("Air and sky")]
            public bool haze = true;
            public Color hazeColor = new Color(.74f, .84f, .94f);
            [Tooltip("Metres from the camera where the haze starts and where it is whole")]
            public float hazeStart = 80f, hazeEnd = 520f;
            [Tooltip("The Pawn Rush sky's cloud cover in the yard (the concept: a few soft clouds)")]
            [Range(0f, 1f)] public float clouds = .36f;

            [Header("Grade (over the game's own)")]
            public float exposure = .6f;
            [Range(-100f, 100f)] public float contrast = 9f, saturation = 10f;
            [Range(-100f, 100f)] public float temperature = 5f;
            public Color filter = new Color(1f, .985f, .955f);
            public float bloomThreshold = 1.05f, bloomIntensity = .8f;
            [Range(0f, 1f)] public float bloomScatter = .55f;
            [Range(0f, 1f)] public float vignette = .14f;

            [Header("Reach and quality")]
            [Tooltip("Metres round the yard over which its light fades in and out (as a section)")]
            public float blend = 14f;
            [Tooltip("A reflection probe per zone (medium and high)")]
            public bool probes = true;
            [Tooltip("Point lights in the lamps, the coaches' and the hoists' lamps, and RcyLights.txt's places")]
            public bool lamps = true;
            [Tooltip("The most the quality settings may turn on: 0 low, 1 medium, 2 high")]
            [Range(0, 2)] public int maxTier = 2;
        }

        [Header("Look: light and air (R683, RoyalCarriageYard.Lighting.cs)")]
        [SerializeField] LightSettings lighting = new LightSettings();

        static readonly Color LampColour = new Color(1f, .78f, .42f), WindowColour = new Color(1f, .74f, .48f);
        // Where the code's own lamps stand (the map's frame): a marked place this close to one is that lamp, already lit.
        readonly List<Vector3> litLamps = new List<Vector3>();
        const float SameLamp = 1.2f;
        const string LightsFile = ModelFolder + "RcyLights";

        // The yard's box in its own frame (the map's: x across, z north): every zone and the walls round it, up to the
        // gate towers' tops. The whole map runs on into the finish court.
        Bounds YardBox()
        {
            float z1 = asSection ? SectionExit.z + 2f : GateZ + LaterShift + 46f;
            const float z0 = -20f, half = 30f, y0 = -12f, y1 = 40f;
            return new Bounds(new Vector3(0f, (y0 + y1) * .5f, (z0 + z1) * .5f), new Vector3(half * 2f, y1 - y0, z1 - z0));
        }

        void Lighting()
        {
            // A prefab built by an editor tool must not carry the yard's light into a saved scene.
            if (!lighting.on || !Application.isPlaying) return;
            var rig = new GameObject("Light and air");
            rig.transform.SetParent(transform, false);
            var s = lighting;
            var to = new YardAtmosphere.Target
            {
                turnSun = !asSection || s.turnSunAsSection,
                // The sun's way is the yard's: turned with the map wherever the round has put it.
                sunRotation = transform.rotation * Quaternion.Euler(s.sunHeight, s.sunYaw, 0f),
                sunColor = s.sunColor, sunIntensity = s.sunIntensity, shadowStrength = s.shadowStrength,
                sky = s.ambientSky, horizon = s.ambientHorizon, ground = s.ambientGround,
                skyFill = s.skyFill, bounceFill = s.bounceFill, skyFillColor = s.skyFillColor, bounceFillColor = s.bounceFillColor,
                fog = s.haze, fogColor = s.hazeColor, fogStart = s.hazeStart, fogEnd = s.hazeEnd, clouds = s.clouds,
            };

            var grade = ZoneGrade.Add(rig, "Royal Carriage Yard grade", new ZoneGrade.Look
            {
                exposure = s.exposure, contrast = s.contrast, saturation = s.saturation, temperature = s.temperature, tint = 0f,
                filter = s.filter,
                shadows = new Vector4(.95f, .98f, 1.06f, 0f), highlights = new Vector4(1.04f, 1.01f, .95f, 0f),
                bloomThreshold = s.bloomThreshold, bloomIntensity = s.bloomIntensity, bloomScatter = s.bloomScatter,
                bloomTint = new Color(1f, .95f, .86f), vignette = s.vignette, vignetteSmoothness = .45f,
            });

            var air = rig.AddComponent<YardAtmosphere>();
            air.Configure(to, YardBox(), s.blend, !asSection, grade, s.maxTier, Fill(rig.transform, "Sky fill"), Fill(rig.transform, "Bounce fill"),
                          s.probes ? (Func<Transform, List<ReflectionProbe>>)ZoneProbes : null);
            int lamps = 0, cues = 0, marked = 0;
            if (s.lamps)
            {
                YardLamps(air, out lamps, out cues);
                marked = MarkedLights(air, rig.transform);
            }
            Debug.Log("[마차뜰 조명] 해 · 하늘빛 · 안개 · 색 보정" + (s.probes ? " · 반사 프로브" : "") + ", 등불 빛 " + lamps
                      + "개 · 신호 빛 " + cues + "개 · 표시한 불빛 " + marked + "개" + (asSection ? " (구간: 카메라가 마차뜰 안에 있을 때만)" : ""));
        }

        static Light Fill(Transform rig, string name)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(rig, false);
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.intensity = 0f;
            light.enabled = false;
            return light;
        }

        static Light Point(Transform parent, Vector3 local, Color colour, float range, string name)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = local;
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.bounceIntensity = 0f;
            light.enabled = false;
            return light;
        }

        // The lamps the yard's code builds (YardKit.Lamp, glowing #FFC86A): the lamp posts' and the storage sheds'
        // (still lamps with an oil flame's flicker), the coaches' (one light between a coach's two, in their colour: amber
        // at rest, red from the turn to the end of a charge - a red pool on the paving round a coach about to go) and the
        // loft hoists' (in their green / amber / red).
        void YardLamps(YardAtmosphere air, out int lamps, out int cues)
        {
            lamps = cues = 0;
            litLamps.Clear();
            var coaches = new Dictionary<Transform, List<Renderer>>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                if (r.name == "Hoist lamp")
                {
                    air.AddLamp(Point(r.transform, new Vector3(0f, -1.2f, 0f), LampColour, 4f, "Hoist lamp light"), 1.6f, 2, false, r);
                    cues++;
                    continue;
                }
                bool shed = r.name == "Shed lamp";
                if (r.name != "Lamp" && !shed) continue;
                var coach = Above(r.transform, "Carriage");
                if (coach != null)
                {
                    if (!coaches.TryGetValue(coach, out var list)) coaches[coach] = list = new List<Renderer>();
                    list.Add(r);
                    continue;
                }
                // Hung on the lamp's own box (a child light under a scaled box keeps its range): a little under it, so
                // the post's head and the wall behind it take the light.
                air.AddLamp(Point(r.transform, new Vector3(0f, -.6f, 0f), LampColour, shed ? 4.5f : 6f, shed ? "Shed lamp light" : "Lamp light"),
                            shed ? 1.1f : 1.5f, shed ? 2 : 1, true);
                litLamps.Add(transform.InverseTransformPoint(r.bounds.center));
                lamps++;
            }
            foreach (var pair in coaches)
            {
                var mid = Vector3.zero;
                foreach (var lamp in pair.Value) mid += pair.Key.InverseTransformPoint(lamp.bounds.center);
                mid /= pair.Value.Count;
                // A little ahead of the lamps, at the nose: the pool falls on the horses and the paving ahead.
                air.AddLamp(Point(pair.Key, mid + new Vector3(0f, -.2f, .6f), LampColour, 7f, "Coach lamp light"), 1.5f, 1, false, pair.Value[0]);
                cues++;
            }
        }

        static Transform Above(Transform t, string name)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == name) return p;
            return null;
        }

        // Places for lights the models leave room for (the model sessions write them: torch / lantern / window x y z, in
        // the map's frame - x across, y up, z north, metres - one a line, # comments). A torch is a flame and its
        // flickering light (MiniGameFlame); a lantern a flickering warm light; a window a steady soft one.
        // A torch or lantern where the code already lights a lamp (the lamp posts) is that lamp: skipped.
        int MarkedLights(YardAtmosphere air, Transform rig)
        {
            var text = Resources.Load<TextAsset>(LightsFile);
            if (text == null) return 0;
            int n = 0;
            int skipped = 0;
            foreach (var raw in text.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var f = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (f.Length < 4
                    || !float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                    || !float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                    || !float.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                {
                    Debug.LogWarning("[마차뜰 조명] " + LightsFile + ".txt 줄을 읽지 못했습니다: " + line);
                    continue;
                }
                var at = new Vector3(x, y, z);
                if (f[0] != "window" && Near(at)) { skipped++; continue; }
                switch (f[0])
                {
                    case "torch":
                        var flame = MiniGameFlame.Light(rig, at, new Color(1f, .62f, .26f), 2.2f, 7f);
                        var light = flame.GetComponent<Light>();
                        light.shadows = LightShadows.None;
                        air.AddLamp(light, 2.2f, 1, false, null, keepsIntensity: true);
                        break;
                    case "lantern":
                        air.AddLamp(Point(rig, at, LampColour, 6f, "Marked lantern light"), 1.5f, 1, true);
                        break;
                    case "window":
                        air.AddLamp(Point(rig, at, WindowColour, 5f, "Marked window light"), .9f, 2, false);
                        break;
                    default:
                        Debug.LogWarning("[마차뜰 조명] " + LightsFile + ".txt의 모르는 종류: " + f[0] + " (torch / lantern / window)");
                        continue;
                }
                n++;
            }
            if (skipped > 0)
                Debug.Log("[마차뜰 조명] " + LightsFile + ".txt의 " + skipped + "곳은 코드의 등불이 이미 있는 자리라(" + SameLamp + " m 안) 빛을 한 번만 켭니다");
            return n;
        }

        bool Near(Vector3 at)
        {
            foreach (var p in litLamps)
                if ((p - at).sqrMagnitude < SameLamp * SameLamp) return true;
            return false;
        }

        // One box-projected probe per zone, in the map's frame (as a section the round may have moved the map; a probe's
        // box is the world's, so each box is the zone's turned box's bounds). Drawn once, after the build, one face a frame.
        List<ReflectionProbe> ZoneProbes(Transform rig)
        {
            float exit = asSection ? SectionExit.z + 2f : GateZ + LaterShift + 46f;
            // centre of the box, its size, the eye the probe sees from (a pawn's head over the zone's floor)
            var zones = new[]
            {
                (name: "Probe 1 yard", centre: new Vector3(0f, 7f, 17f), size: new Vector3(48f, 20f, 68f), eye: new Vector3(0f, 2.5f, 20f)),
                (name: "Probe 2 canal", centre: new Vector3(0f, 6f, (CanalZ0 + CanalZ1) * .5f), size: new Vector3(48f, 22f, CanalZ1 - CanalZ0),
                 eye: new Vector3(0f, 2.5f, (CanalZ0 + CanalZ1) * .5f)),
                (name: "Probe 3 sorting yard", centre: new Vector3(0f, 7f, (CanalZ1 + CliffZ + LaterShift) * .5f),
                 size: new Vector3(56f, 22f, CliffZ + LaterShift - CanalZ1), eye: new Vector3(0f, 2.5f, (CanalZ1 + CliffZ + LaterShift) * .5f)),
                (name: "Probe 4 gate", centre: new Vector3(0f, Top + 6f, (CliffZ + LaterShift + exit) * .5f),
                 size: new Vector3(56f, 20f, exit - CliffZ - LaterShift), eye: new Vector3(0f, Top + 2.5f, (CliffZ + LaterShift + exit) * .5f)),
            };
            int resolution = SettingsHigh() ? 128 : 64;
            var made = new List<ReflectionProbe>();
            foreach (var z in zones)
            {
                var go = new GameObject(z.name);
                go.transform.SetParent(rig, false);
                go.transform.localPosition = z.eye;
                var probe = go.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.boxProjection = true;
                probe.hdr = true;
                probe.resolution = resolution;
                probe.importance = 2;   // over GameLook's scene-wide probe (1) inside the yard
                probe.blendDistance = 2f;
                probe.shadowDistance = 60f;
                probe.farClipPlane = 400f;
                // The world's box round the turned zone box.
                var b = new Bounds(transform.TransformPoint(z.centre), Vector3.zero);
                var e = z.size * .5f;
                for (int i = 0; i < 8; i++)
                    b.Encapsulate(transform.TransformPoint(z.centre + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
                probe.size = b.size;
                probe.center = b.center - go.transform.position;
                made.Add(probe);
            }
            return made;
        }

        static bool SettingsHigh()
        {
            int q = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            bool mobile = q >= 0 && q < names.Length && names[q].Contains("Mobile");
            return !mobile && (GameSettings.Preset == 2 || (GameSettings.Preset == 3 && GameSettings.Shadow >= 3));
        }
    }
}
