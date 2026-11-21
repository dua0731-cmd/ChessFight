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
    // R734: the places' light mixer (Game ZoneLight) blends the ambient light as spherical harmonics and writes it to
    // RenderSettings.ambientProbe. This checks, by drawing a white sphere lit by nothing but the ambient light in an
    // empty scene, that (1) a probe written in the sky mode is what the sphere gets, and (2) ZoneLight.Gradient gives the
    // light Unity's own three-colour gradient mode gives (so a place's look at weight 1 is what its colours were tuned
    // to), and (3) a blend half way between two probes is half way on screen too.
    //   Unity -batchmode -projectPath <copy> -executeMethod ChessFight.PawnRush.Editor.ZoneLightCheck.RunBatch
    // (no -nographics). Report: Logs/ZoneLightCheck/report.txt. Tools/Run-ZoneLightCheck.ps1 runs it on the project copy.
    public static class ZoneLightCheck
    {
        static string Folder => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ZoneLightCheck");

        static readonly Color Sky = new Color(.9f, .15f, .1f), Equator = new Color(.1f, .8f, .15f), Ground = new Color(.1f, .15f, .9f);

        // Where on the sphere (screen x, y in -1..1 of its radius) the colours are read, and the surface's way there.
        static readonly Vector2[] Spots = { new Vector2(0f, .92f), new Vector2(0f, .55f), new Vector2(0f, 0f), new Vector2(.7f, 0f), new Vector2(0f, -.55f), new Vector2(0f, -.92f) };
        static readonly string[] SpotNames = { "top", "upper", "middle", "side", "lower", "bottom" };

        [MenuItem("ChessFight/Pawn Rush/Validate/Zone light - ambient blend (empty scene)", false, 214)]
        public static void RunBatch()
        {
            int failures = 0;
            var report = new StringBuilder();
            report.AppendLine("Zone light ambient check (R734): a white URP Lit sphere lit only by the ambient light.");
            Directory.CreateDirectory(Folder);
            Camera cam = null;
            RenderTexture rt = null;
            Material mat = null;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.fog = false;
                RenderSettings.ambientIntensity = 1f;
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.localScale = Vector3.one * 2f;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0f);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_EnvironmentReflections", 0f);
                mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                mat.SetFloat("_SpecularHighlights", 0f);
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                var r = sphere.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;

                cam = new GameObject("Check camera").AddComponent<Camera>();
                cam.transform.position = new Vector3(0f, 0f, -6f);
                cam.transform.rotation = Quaternion.identity;
                cam.orthographic = true;
                cam.orthographicSize = 1.05f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.nearClipPlane = .1f;
                cam.farClipPlane = 20f;
                rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                cam.targetTexture = rt;

                // 1. Unity's own gradient mode.
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = Sky;
                RenderSettings.ambientEquatorColor = Equator;
                RenderSettings.ambientGroundColor = Ground;
                var unityProbe = RenderSettings.ambientProbe;
                var trilight = Draw(cam, rt);
                report.AppendLine("Unity gradient mode, drawn:      " + Line(trilight));
                report.AppendLine("  its ambientProbe read back:    " + Line(Eval(unityProbe)));

                // 2. The sky mode with ZoneLight.Gradient written as the probe. Gradient is asked from the sky mode with
                //    another probe on, as the mixer asks it, and must leave that as it was.
                var grey = ZoneLight.Spread(new Color(.5f, .5f, .5f), new Color(.5f, .5f, .5f), new Color(.5f, .5f, .5f));
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientProbe = grey;
                var mine = ZoneLight.Gradient(Sky, Equator, Ground);
                bool kept = RenderSettings.ambientMode == AmbientMode.Skybox && RenderSettings.ambientProbe == grey;
                if (!kept) { failures++; report.AppendLine("FAIL: ZoneLight.Gradient did not put the ambient settings back"); }
                else report.AppendLine("PASS: ZoneLight.Gradient puts the ambient settings back");
                RenderSettings.ambientProbe = mine;
                var written = Draw(cam, rt);
                report.AppendLine("Sky mode + ZoneLight.Gradient:   " + Line(written));
                report.AppendLine("  Gradient evaluated:            " + Line(Eval(mine)));
                float worst = 0f;
                for (int i = 0; i < Spots.Length; i++) worst = Mathf.Max(worst, Diff(trilight[i], written[i]));
                report.AppendLine("  largest difference to Unity's gradient: " + F(worst));
                if (worst > .01f) { failures++; report.AppendLine("FAIL: ZoneLight.Gradient is not Unity's gradient light"); }
                else report.AppendLine("PASS: ZoneLight.Gradient gives Unity's gradient light");
                report.AppendLine("  (the stand-in ZoneLight.Spread, for the record: " + Line(Eval(ZoneLight.Spread(Sky, Equator, Ground))) + ")");

                // 3. A probe written in the sky mode is what is drawn (a flat grey all round).
                RenderSettings.ambientProbe = grey;
                var flat = Draw(cam, rt);
                report.AppendLine("Sky mode + flat grey .5:         " + Line(flat));
                bool greyOk = true;
                foreach (var c in flat) greyOk &= Mathf.Abs(c.r - flat[2].r) < .03f && c.r > .3f && c.r < .6f && Mathf.Abs(c.r - c.b) < .02f;
                if (!greyOk) { failures++; report.AppendLine("FAIL: the written probe is not what the sphere gets"); }
                else report.AppendLine("PASS: the written probe is what the sphere gets");

                // 4. Half way between the grey and the gradient, on screen as well.
                RenderSettings.ambientProbe = grey * .5f + mine * .5f;
                var half = Draw(cam, rt);
                report.AppendLine("Half grey, half gradient:        " + Line(half));
                float halfWorst = 0f;
                for (int i = 0; i < Spots.Length; i++) halfWorst = Mathf.Max(halfWorst, Diff(half[i], (flat[i] + written[i]) * .5f));
                report.AppendLine("  largest difference to the mean of the two: " + F(halfWorst));
                if (halfWorst > .02f) { failures++; report.AppendLine("FAIL: the blend is not linear on screen"); }
                else report.AppendLine("PASS: the blend is linear on screen");

                // 5. For the record: a probe written while in the gradient mode (does Unity keep it?).
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientProbe = grey;
                report.AppendLine("Gradient mode + grey written:    " + Line(Draw(cam, rt)) + "  (information only)");
            }
            catch (System.Exception e)
            {
                failures++;
                report.AppendLine("FAIL: " + e);
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                if (rt != null) Object.DestroyImmediate(rt);
                if (mat != null) Object.DestroyImmediate(mat);
            }
            report.AppendLine(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL (" + failures + ")");
            File.WriteAllText(Path.Combine(Folder, "report.txt"), report.ToString());
            Debug.Log("[ZoneLightCheck]\n" + report);
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static Color[] Draw(Camera cam, RenderTexture rt)
        {
            cam.Render();
            cam.Render();
            var was = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = was;
            var got = new Color[Spots.Length];
            for (int i = 0; i < Spots.Length; i++)
            {
                // Sphere radius 1 on an orthographic view of half height 1.05.
                float px = (Spots[i].x / 1.05f * .5f + .5f) * (rt.width - 1), py = (Spots[i].y / 1.05f * .5f + .5f) * (rt.height - 1);
                got[i] = tex.GetPixel(Mathf.RoundToInt(px), Mathf.RoundToInt(py));
            }
            Object.DestroyImmediate(tex);
            return got;
        }

        static Color[] Eval(SphericalHarmonicsL2 sh)
        {
            var dirs = new Vector3[Spots.Length];
            for (int i = 0; i < Spots.Length; i++)
            {
                var s = Spots[i];
                dirs[i] = new Vector3(s.x, s.y, -Mathf.Sqrt(Mathf.Max(0f, 1f - s.x * s.x - s.y * s.y)));
            }
            var got = new Color[Spots.Length];
            sh.Evaluate(dirs, got);
            return got;
        }

        static float Diff(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

        static string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        static string Line(Color[] c)
        {
            var s = new StringBuilder();
            for (int i = 0; i < c.Length; i++)
                s.Append(SpotNames[i]).Append(" (").Append(F(c[i].r)).Append(", ").Append(F(c[i].g)).Append(", ").Append(F(c[i].b)).Append(")  ");
            return s.ToString();
        }
    }
}
