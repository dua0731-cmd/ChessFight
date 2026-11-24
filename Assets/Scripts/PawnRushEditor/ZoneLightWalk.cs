using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using ChessFight.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush.Editor
{
    // R734: the places' light mixer (Game ZoneLight) in Play, the way a round meets it. An empty scene with a sun, the
    // sky's ambient and the round's far fog; place A (a sunny yard: warmer, stronger sun, its own ambient and haze) at
    // z 0 ~ 40, a 10 m gap (a corridor), place B (a night hall: the sun turned and dimmed to moonlight, navy ambient and
    // haze) at z 50 ~ 90, each fading 24 m out as the game's do. A camera runs through at 7 m/s (a pawn's run) from
    // before A to past B - in, out of A into the gap, through both at once, into B and out - then stands, is thrown back
    // into A (a respawn) and far away again. Every frame the sun, the ambient light (up and level) and the haze are read
    // after the mixer; the largest change in one frame is checked against what still reads as a smooth turn, and with no
    // place left the scene's light must be back exactly. The old fade (14 m, eased over half a second) is worked out on
    // the same walk for comparison, and how far the old way's ambient jumped at a place's edge.
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.ZoneLightWalk.RunPlayBatch
    // (no -quit, no -nographics). Report: Logs/ZoneLightCheck/walk.txt (Tools/Run-ZoneLightCheck.ps1 runs both checks).
    [InitializeOnLoad]
    public static class ZoneLightWalk
    {
        const string ActiveKey = "ZoneLightWalk.Active", CodeKey = "ZoneLightWalk.Code";

        static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ZoneLightCheck");

        static ZoneLightWalk()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("ChessFight/Pawn Rush/Validate/Zone light - walk through two places (Play)", false, 215)]
        public static void RunPlayBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(CodeKey, 1);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode) new GameObject("Zone light walk").AddComponent<Walker>();
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(ActiveKey);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
            }
        }

        internal static void Done(string report, bool pass)
        {
            File.WriteAllText(Path.Combine(Folder, "walk.txt"), report);
            Debug.Log("[ZoneLightWalk]\n" + report);
            SessionState.SetInt(CodeKey, pass ? 0 : 1);
            EditorApplication.isPlaying = false;
        }

        // A place's owner, as the yard, the library and the observatory do it.
        sealed class Place : MonoBehaviour
        {
            public ZoneLight zone;
            public Bounds area;
            public float blend = 24f;

            void LateUpdate() => zone.Ease(ZoneLight.Inside(transform, area, blend));

            void OnDestroy() => zone?.Remove();
        }

        sealed class Walker : MonoBehaviour
        {
            const float Speed = 7f;
            // What still reads as a smooth turn: the most that may change in a sixtieth of a second (a frame's change is
            // scaled to a sixtieth, so a fast or slow batch frame rate reads the same and a one-frame jump still shows).
            const float MaxIntensityStep = .03f, MaxColourStep = .02f, MaxAmbientStep = .02f, MaxTurnStep = 1.5f, MaxFogStep = .03f;

            readonly StringBuilder report = new StringBuilder();
            int failures;
            Light sun;
            Camera cam;
            Place a, b;
            SphericalHarmonicsL2 baseProbe;
            Quaternion baseWay;

            // The last frame's readings and the largest step seen in the current stretch.
            float lastIntensity, lastFogStart, lastFogEnd;
            Color lastColour, lastUp, lastLevel, lastFog;
            Quaternion lastWay;
            float stepIntensity, stepColour, stepAmbient, stepTurn, stepFog;
            // The old fade worked out on the same walk: weight, its fastest change.
            float oldA, oldB, oldRate, newRate, lastNewA = -1f, lastNewB = -1f;

            IEnumerator Start()
            {
                Application.targetFrameRate = 60;
                QualitySettings.vSyncCount = 0;
                report.AppendLine("Zone light walk (R734): two places 10 m apart, a camera running through at 7 m/s.");

                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                sun.intensity = 1f;
                sun.color = new Color(1f, .97f, .92f);
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
                baseWay = sun.transform.rotation;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                baseProbe = ZoneLight.Gradient(new Color(.42f, .5f, .62f), new Color(.38f, .4f, .42f), new Color(.2f, .19f, .17f));
                RenderSettings.ambientProbe = baseProbe;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = ZoneLight.NoFogStart;
                RenderSettings.fogEndDistance = ZoneLight.NoFogEnd;
                RenderSettings.fogColor = new Color(.5f, .5f, .5f);

                cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                cam.transform.position = new Vector3(0f, 3f, -40f);

                a = MakePlace("Place A (sunny yard)", 20f, false, new Color(1f, .955f, .88f), 1.85f, .82f,
                              new Color(.5f, .6f, .78f), new Color(.6f, .58f, .54f), new Color(.4f, .36f, .31f), new Color(.74f, .84f, .94f), 80f, 520f);
                b = MakePlace("Place B (night hall)", 70f, true, new Color(.72f, .8f, 1f), 1.5f, .7f,
                              new Color(.52f, .54f, .74f), new Color(.44f, .41f, .56f), new Color(.26f, .22f, .34f), new Color(.06f, .07f, .15f), 90f, 600f);

                // How far the old way's ambient jumped at a place's edge: the sky's ambient swapped for a gradient of three
                // samples of it.
                var dirs = new[] { Vector3.up, new Vector3(.7071f, 0f, .7071f), Vector3.down };
                var got = new Color[3];
                baseProbe.Evaluate(dirs, got);
                var oldStart = ZoneLight.Gradient(got[0], got[1], got[2]);
                report.AppendLine("  the old way's ambient jump at a place's edge (this scene's sky): " + F(MaxDiff(oldStart, baseProbe)) + " (one frame)");

                for (int i = 0; i < 10; i++) yield return null;
                Read(true);

                yield return Run("run from before A to past B", -40f, 140f);
                yield return Stand("stand past B (4 s)", 4f);
                CheckBack("past both places, the scene's own light is back");
                Reset();
                cam.transform.position = new Vector3(0f, 3f, 20f);   // thrown back into A
                yield return Stand("a respawn: the camera thrown into A (3 s)", 3f);
                CheckIn(a, "after the respawn A is whole");
                Reset();
                cam.transform.position = new Vector3(0f, 3f, -200f);
                yield return Stand("thrown far away (4 s)", 4f);
                CheckBack("far from both, the scene's own light is back");

                report.AppendLine("  fastest weight change on the run: now " + F(newRate) + "/s, the old fade " + F(oldRate) + "/s");
                report.AppendLine(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");
                ZoneLightWalk.Done(report.ToString(), failures == 0);
            }

            Place MakePlace(string name, float z, bool turn, Color sunColour, float intensity, float shadow,
                            Color sky, Color equator, Color ground, Color fog, float fogStart, float fogEnd)
            {
                var go = new GameObject(name);
                var place = go.AddComponent<Place>();
                place.area = new Bounds(new Vector3(0f, 14f, z), new Vector3(60f, 52f, 40f));
                var zone = ZoneLight.Add(name);
                zone.TurnSun = turn;
                zone.SunRotation = Quaternion.Euler(55f, 190f, 0f);
                zone.SunColor = sunColour;
                zone.SunIntensity = intensity;
                zone.ShadowStrength = shadow;
                zone.SoftShadows = turn;
                zone.Sky = sky;
                zone.Equator = equator;
                zone.Ground = ground;
                zone.Fog = true;
                zone.FogColor = fog;
                zone.FogStart = fogStart;
                zone.FogEnd = fogEnd;
                place.zone = zone;
                return place;
            }

            IEnumerator Run(string what, float from, float to)
            {
                Reset();
                cam.transform.position = new Vector3(0f, 3f, from);
                float crossBoth = 0f;
                while (cam.transform.position.z < to)
                {
                    yield return null;   // (no end-of-frame in batch mode: the reading is of the frame before, the same lag throughout)
                    float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
                    Old(dt);
                    Read(false);
                    if (a.zone.Weight > .05f && b.zone.Weight > .05f) crossBoth = Mathf.Max(crossBoth, Mathf.Min(a.zone.Weight, b.zone.Weight));
                    if (lastNewA >= 0f) newRate = Mathf.Max(newRate, Mathf.Abs(a.zone.Weight - lastNewA) / dt, Mathf.Abs(b.zone.Weight - lastNewB) / dt);
                    lastNewA = a.zone.Weight;
                    lastNewB = b.zone.Weight;
                    cam.transform.position += Vector3.forward * Speed * Mathf.Min(dt, .05f);
                }
                Steps(what);
                Check(crossBoth > .1f, "between A and B both places are in at once (each at least " + F(crossBoth) + ")");
            }

            IEnumerator Stand(string what, float seconds)
            {
                Reset();
                float until = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;   // (no end-of-frame in batch mode: the reading is of the frame before, the same lag throughout)
                    Read(false);
                }
                Steps(what);
            }

            // The old fade: 14 m out, eased toward it at most 2 a second.
            void Old(float dt)
            {
                float ta = OldTarget(a.area), tb = OldTarget(b.area);
                float na = Mathf.MoveTowards(oldA, ta, dt / .5f), nb = Mathf.MoveTowards(oldB, tb, dt / .5f);
                oldRate = Mathf.Max(oldRate, Mathf.Abs(na - oldA) / dt, Mathf.Abs(nb - oldB) / dt);
                oldA = na;
                oldB = nb;
            }

            float OldTarget(Bounds area)
            {
                var p = cam.transform.position;
                return 1f - Mathf.SmoothStep(0f, 1f, Vector3.Distance(area.ClosestPoint(p), p) / 14f);
            }

            void Read(bool first)
            {
                var probe = RenderSettings.ambientProbe;
                var got = new Color[2];
                probe.Evaluate(new[] { Vector3.up, Vector3.forward }, got);
                var way = sun.transform.rotation;
                if (!first)
                {
                    float k = (1f / 60f) / Mathf.Max(Time.unscaledDeltaTime, 1f / 240f);
                    stepIntensity = Mathf.Max(stepIntensity, k * Mathf.Abs(sun.intensity - lastIntensity));
                    stepColour = Mathf.Max(stepColour, k * Diff(sun.color, lastColour));
                    stepAmbient = Mathf.Max(stepAmbient, k * Mathf.Max(Diff(got[0], lastUp), Diff(got[1], lastLevel)));
                    stepTurn = Mathf.Max(stepTurn, k * Quaternion.Angle(way, lastWay));
                    // The haze as it is seen: how much of it lies over things 30, 100, 250 and 600 m away.
                    float seen = 0f;
                    foreach (float d in HazeAt)
                        seen = Mathf.Max(seen, Mathf.Abs(Haze(d, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance)
                                                         - Haze(d, lastFogStart, lastFogEnd)));
                    stepFog = Mathf.Max(stepFog, k * Mathf.Max(Diff(RenderSettings.fogColor, lastFog), seen));
                }
                lastIntensity = sun.intensity;
                lastColour = sun.color;
                lastUp = got[0];
                lastLevel = got[1];
                lastWay = way;
                lastFog = RenderSettings.fogColor;
                lastFogStart = RenderSettings.fogStartDistance;
                lastFogEnd = RenderSettings.fogEndDistance;
            }

            static readonly float[] HazeAt = { 30f, 100f, 250f, 600f };

            static float Haze(float d, float start, float end) => Mathf.Clamp01((d - start) / Mathf.Max(.1f, end - start));

            void Reset() { stepIntensity = stepColour = stepAmbient = stepTurn = stepFog = 0f; }

            void Steps(string what)
            {
                report.AppendLine(what + ": largest change per 1/60 s - sun strength " + F(stepIntensity) + ", sun colour " + F(stepColour)
                                  + ", ambient " + F(stepAmbient) + ", sun turn " + F(stepTurn) + " deg, haze " + F(stepFog));
                Check(stepIntensity <= MaxIntensityStep && stepColour <= MaxColourStep && stepAmbient <= MaxAmbientStep
                      && stepTurn <= MaxTurnStep && stepFog <= MaxFogStep, "  no jump in " + what);
            }

            void CheckBack(string what)
            {
                bool back = RenderSettings.ambientProbe == baseProbe && Mathf.Abs(sun.intensity - 1f) < 1e-5f
                            && Quaternion.Angle(sun.transform.rotation, baseWay) < .01f
                            && Mathf.Abs(RenderSettings.fogStartDistance - ZoneLight.NoFogStart) < .01f
                            && RenderSettings.ambientMode == AmbientMode.Skybox && a.zone.Weight == 0f && b.zone.Weight == 0f;
                Check(back, what);
            }

            void CheckIn(Place p, string what) => Check(p.zone.Weight > .99f && Mathf.Abs(sun.intensity - p.zone.SunIntensity) < .02f, what);

            void Check(bool ok, string what)
            {
                report.AppendLine((ok ? "PASS: " : "FAIL: ") + what.Trim());
                if (!ok) failures++;
            }

            static float Diff(Color x, Color y) => Mathf.Max(Mathf.Abs(x.r - y.r), Mathf.Max(Mathf.Abs(x.g - y.g), Mathf.Abs(x.b - y.b)));

            static float MaxDiff(SphericalHarmonicsL2 x, SphericalHarmonicsL2 y)
            {
                var dirs = new[] { Vector3.up, Vector3.forward, Vector3.right, Vector3.down, new Vector3(.6f, .6f, .53f) };
                var gx = new Color[dirs.Length];
                var gy = new Color[dirs.Length];
                x.Evaluate(dirs, gx);
                y.Evaluate(dirs, gy);
                float m = 0f;
                for (int i = 0; i < dirs.Length; i++) m = Mathf.Max(m, Diff(gx[i], gy[i]));
                return m;
            }

            static string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
