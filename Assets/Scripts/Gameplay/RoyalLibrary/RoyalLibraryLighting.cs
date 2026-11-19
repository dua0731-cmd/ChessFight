using System;
using System.Collections;
using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChessFight.Gameplay
{
    // R7 (PLAN.md R7, 11-18): the Royal Library's light - concept D "the giant's desk", a late afternoon: low golden sun
    // through the tall windows on the right, its shafts across the room, warm bounce light, warm haze, a soft bloom and a
    // warm grade. Made in code in Play (no scene YAML): it adds itself to every RoyalLibraryDressing (the course's root,
    // WarRoomComplete) a scene loads, and builds once the zone models are hung.
    //
    //   sun            the scene's directional light, turned to come in low (27 deg) from the right windows (+x) and a
    //                  little from ahead, warm gold; the shell (walls, ceilings, outer box) stops casting shadows so the
    //                  sun reaches the room as if through the windows - tables, pieces, giants and props still cast them
    //   fills          a warm bounce up from the floor and the desks, a soft fill from the left (directional, no shadow)
    //   ambient        trilight: warm cream above, amber at the sides, dark walnut below
    //   haze           linear fog in a warm dusty colour: the far walls step back
    //   window glow    each right-hand window's dark glass gets a warm sky glow in front of it (additive, arched)
    //   light shafts   from each of those windows, a soft additive beam along the sun down to the floor
    //   grade          ZoneGrade (Game): a little warmer, more contrast, warm highlights, soft gold bloom, light vignette
    //                  - over GameLook's soft grade, under its no-bloom switch
    //   probes         a box reflection probe for the chess room and one for the front gallery, drawn once
    // No depth of field (승규 11-18: not on the play view). The desk lamp stays off. Off: `on` (the scene as it was).
    //
    // R706 (seoungtest R689, 승규 11-18 "서재 맵에 들어가면 지금처럼 서재 그래픽이 나오게"): in a Pawn Rush round the course is
    // the late obstacle section (a RoyalLibraryMatch above it), so the scene-wide parts - the sun, the ambient light, the haze,
    // the two fills and the grade - are the library's only while the camera is in the course: weight 1 inside its outer
    // walls, 0 `blendMetres` out, eased over half a second, and at 0 the scene's own are put back exactly as they were
    // noted (the carriage yard's way, PawnRush YardAtmosphere). The windows' glow and shafts, the probes and the shell's
    // shadows are the course's own objects and stay. In the course's own scene the weight is 1 throughout, as before.
    public sealed class RoyalLibraryLighting : MonoBehaviour
    {
        [Tooltip("The library's own light (off: the scene's light as it was)")]
        public bool on = true;
        [Tooltip("In a round: metres outside the course's outer walls over which its light fades out")]
        [Min(.5f)] public float blendMetres = 8f;

        [Header("Sun")]
        [Tooltip("The sun's height over the horizon (deg): late afternoon, long shafts")]
        [Range(5f, 60f)] public float sunHeight = 27f;
        [Tooltip("Where the light goes (deg about y; -90 = towards -x, from the right-hand windows). Past -90 it also "
                 + "comes towards the runners, from the far right windows, as in concept D")]
        public float sunYaw = -112f;
        public Color sunColor = new Color(1f, .78f, .5f);
        [Range(0f, 4f)] public float sunIntensity = 1.7f;
        [Range(0f, 1f)] public float shadowStrength = .78f;

        [Header("Bounce and ambient")]
        public Color ambientSky = new Color(.62f, .5f, .38f);
        public Color ambientEquator = new Color(.5f, .34f, .22f);
        public Color ambientGround = new Color(.22f, .13f, .08f);
        [Range(0f, 1f)] public float bounce = .28f;
        public Color bounceColor = new Color(1f, .7f, .45f);
        [Range(0f, 1f)] public float leftFill = .16f;
        public Color leftFillColor = new Color(.95f, .82f, .7f);

        [Header("Haze")]
        public bool haze = true;
        public Color hazeColor = new Color(.62f, .45f, .3f);
        public float hazeStart = 70f, hazeEnd = 480f;

        [Header("Windows and shafts")]
        public bool windowGlow = true;
        public Color glowColor = new Color(1f, .82f, .55f);
        [Range(0f, 2f)] public float glowStrength = .9f;
        public bool shafts = true;
        public Color shaftColor = new Color(1f, .78f, .48f);
        [Range(0f, .5f)] public float shaftStrength = .085f;

        [Header("Grade (over the game's own)")]
        public float exposure = .12f;
        [Range(-100f, 100f)] public float contrast = 10f, saturation = 6f, temperature = 9f;
        public Color filter = new Color(1f, .975f, .93f);
        public float bloomThreshold = .95f, bloomIntensity = .55f;
        [Range(0f, 1f)] public float bloomScatter = .62f;
        [Range(0f, 1f)] public float vignette = .16f;

        [Header("Reflections")]
        public bool probes = true;

        // The windows on the right-hand walls (the course's frame, Unity metres): R5 chess room (x 123, glass just
        // behind the walnut sash) and R4 front gallery (x 37.25). Centre z, sill y, springing y, half width, glow x.
        static readonly float[] ChessWindowZ = { 1.4f, 20f, 40f, 60f, 80f, 100f, 120f, 140f, 158.6f };
        static readonly float[] GalleryWindowZ = { -138.9f, -120f, -100f, -80f, -60f, -40f, -20.6f };
        const float Floor = -23f;

        // Pieces of the shell that would keep the sun out (models hung by the zones, and the blockout's outer box).
        static readonly string[] ShellWords = { "wall", "ceiling", "perimeter", "facade", "header", "ceiling beam", "cross beam" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
            Attach();
        }

        static void Loaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            foreach (var d in FindObjectsByType<RoyalLibraryDressing>(FindObjectsSortMode.None))
                if (d.GetComponent<RoyalLibraryLighting>() == null)
                    d.gameObject.AddComponent<RoyalLibraryLighting>();
        }

        readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();
        // The course's outer walls in its own frame (x ±160, z -151 ~ 171, the hall's floor to its roof).
        static readonly Bounds CourseArea = new Bounds(new Vector3(0f, 16f, 10f), new Vector3(320f, 92f, 322f));
        bool zoned, built, applied;
        float weight, lastApplied = -1f, lookForView;
        Camera view;
        ZoneGrade grade;
        readonly List<(Light light, float strength)> fills = new List<(Light, float)>();
        // The scene's own, noted the moment the library's light comes in.
        Light sun;
        Color sunColor0; float sunIntensity0, shadowStrength0; Quaternion sunRotation0; LightShadows shadows0;
        AmbientMode ambientMode0; Color sky0, equator0, ground0, flat0, skyFrom, equatorFrom, groundFrom; SphericalHarmonicsL2 probe0;
        bool fog0; FogMode fogMode0; Color fogColor0; float fogStart0, fogEnd0, fogDensity0;

        IEnumerator Start()
        {
            if (!on) yield break;
            yield return null;   // RoyalLibraryDressing hangs the models in its Start
            yield return null;
            zoned = GetComponentInParent<ChessFight.ProtectKing.RoyalLibraryMatch>() != null;
            var rig = new GameObject("Library light").transform;
            rig.SetParent(transform, false);
            int shell = ShellShadowsOff();
            Fill(rig, "Bounce fill", Quaternion.Euler(-70f, sunYaw + 180f, 0f), bounceColor, bounce);
            Fill(rig, "Left fill", Quaternion.Euler(20f, 90f, 0f), leftFillColor, leftFill);
            int windows = 0;
            if (windowGlow || shafts)
            {
                windows += Windows(rig, ChessWindowZ, 123.28f, 20f, 40f, 6f, "Chess room");
                windows += Windows(rig, GalleryWindowZ, 37.95f, 16.8f, 23.6f, 3.6f, "Gallery");
            }
            grade = ZoneGrade.Add(rig.gameObject, "Royal Library grade", new ZoneGrade.Look
            {
                exposure = exposure, contrast = contrast, saturation = saturation, temperature = temperature, tint = 0f,
                filter = filter,
                shadows = new Vector4(1.02f, .98f, .95f, 0f), highlights = new Vector4(1.05f, 1f, .9f, 0f),
                bloomThreshold = bloomThreshold, bloomIntensity = bloomIntensity, bloomScatter = bloomScatter,
                bloomTint = new Color(1f, .87f, .66f), vignette = vignette, vignetteSmoothness = .45f,
            });
            grade.Weight = 0f;
            if (probes)
            {
                Probe(rig, "Chess room probe", new Vector3(0f, 6f, 50f), new Vector3(0f, 17f, 80f), new Vector3(248f, 82f, 180f));
                Probe(rig, "Gallery probe", new Vector3(0f, 6f, -80f), new Vector3(0f, 3.5f, -80f), new Vector3(76f, 54f, 140f));
            }
            built = true;
            if (!zoned) { weight = 1f; Capture(); Apply(1f); }
            Debug.Log("[왕의 서재 조명] 해 · 하늘빛 · 안개 · 색 보정" + (probes ? " · 반사 프로브 2" : "") + ", 창 " + windows
                      + "개(" + (windowGlow ? "빛" : "") + (shafts ? " · 빛줄기" : "") + "), 그림자 끈 벽 · 천장 " + shell + "개"
                      + (zoned ? " — 판 구간: 카메라가 서재 안에 있을 때만" : ""));
        }

        Vector3 SunDirection() => Quaternion.Euler(sunHeight, sunYaw, 0f) * Vector3.forward;   // where the light goes

        // ------------------------------------------------------------------ the library's light over the scene's

        // In a round: how far in the camera is (1 inside the outer walls, 0 blendMetres out), eased in time so a camera
        // that jumps (a respawn, a spectator switch) does not flick the light.
        void LateUpdate()
        {
            if (!built || !zoned) return;
            weight = Mathf.MoveTowards(weight, AreaWeight(), Time.unscaledDeltaTime / .5f);
            if (weight <= .0005f) { if (applied) Restore(); return; }
            if (!applied) Capture();
            if (Mathf.Abs(weight - lastApplied) > .0005f) Apply(weight);
        }

        float AreaWeight()
        {
            if (view == null || !view.isActiveAndEnabled)
            {
                if (Time.unscaledTime < lookForView) return weight;
                lookForView = Time.unscaledTime + 1f;
                view = Camera.main;
                if (view == null)
                    foreach (var c in Camera.allCameras)
                        if (c.cameraType == CameraType.Game && c.targetTexture == null) { view = c; break; }
                if (view == null) return 0f;
            }
            var p = transform.InverseTransformPoint(view.transform.position);
            float d = Vector3.Distance(CourseArea.ClosestPoint(p), p);
            return 1f - Mathf.SmoothStep(0f, 1f, d / Mathf.Max(.5f, blendMetres));
        }

        bool IsFill(Light l)
        {
            foreach (var f in fills) if (f.light == l) return true;
            return false;
        }

        Light FindSun()
        {
            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional && !IsFill(RenderSettings.sun)) return RenderSettings.sun;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && !IsFill(l)) return l;
            return null;
        }

        void Capture()
        {
            applied = true;
            lastApplied = -1f;
            sun = FindSun();
            if (sun != null)
            {
                sunColor0 = sun.color; sunIntensity0 = sun.intensity; shadowStrength0 = sun.shadowStrength;
                sunRotation0 = sun.transform.rotation; shadows0 = sun.shadows;
            }
            ambientMode0 = RenderSettings.ambientMode;
            sky0 = RenderSettings.ambientSkyColor; equator0 = RenderSettings.ambientEquatorColor; ground0 = RenderSettings.ambientGroundColor;
            flat0 = RenderSettings.ambientLight; probe0 = RenderSettings.ambientProbe;
            switch (ambientMode0)
            {
                case AmbientMode.Trilight: skyFrom = sky0; equatorFrom = equator0; groundFrom = ground0; break;
                case AmbientMode.Flat: skyFrom = equatorFrom = groundFrom = flat0; break;
                default:
                    // The sky's (or a custom) ambient: what it gives straight up, sideways and straight down.
                    var dirs = new[] { Vector3.up, new Vector3(.7071f, 0f, .7071f), Vector3.down };
                    var got = new Color[3];
                    probe0.Evaluate(dirs, got);
                    skyFrom = got[0]; equatorFrom = got[1]; groundFrom = got[2];
                    break;
            }
            fog0 = RenderSettings.fog; fogMode0 = RenderSettings.fogMode; fogColor0 = RenderSettings.fogColor;
            fogStart0 = RenderSettings.fogStartDistance; fogEnd0 = RenderSettings.fogEndDistance; fogDensity0 = RenderSettings.fogDensity;
        }

        // w 1 = the library's light as R7 set it (the sun turned low through the right windows, trilight, haze, fills, grade).
        void Apply(float w)
        {
            lastApplied = w;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Slerp(sunRotation0, transform.rotation * Quaternion.Euler(sunHeight, sunYaw, 0f), w);
                sun.color = Color.Lerp(sunColor0, sunColor, w);
                sun.intensity = Mathf.Lerp(sunIntensity0, sunIntensity, w);
                sun.shadowStrength = Mathf.Lerp(shadowStrength0, shadowStrength, w);
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(skyFrom, ambientSky, w);
            RenderSettings.ambientEquatorColor = Color.Lerp(equatorFrom, ambientEquator, w);
            RenderSettings.ambientGroundColor = Color.Lerp(groundFrom, ambientGround, w);
            if (haze)
            {
                // From the scene's own linear fog, or from none (pushed past everything) when it had none.
                bool fromLinear = fog0 && fogMode0 == FogMode.Linear;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = Color.Lerp(fog0 ? fogColor0 : hazeColor, hazeColor, w);
                RenderSettings.fogStartDistance = Mathf.Lerp(fromLinear ? fogStart0 : 3000f, hazeStart, w);
                RenderSettings.fogEndDistance = Mathf.Lerp(fromLinear ? fogEnd0 : 6000f, hazeEnd, w);
            }
            else RenderSettings.fog = fog0 && w < .999f;
            foreach (var (light, strength) in fills)
            {
                if (light == null) continue;
                light.intensity = strength * w;
                light.enabled = light.intensity > .002f;
            }
            if (grade != null) grade.Weight = w;
        }

        void Restore()
        {
            applied = false;
            lastApplied = -1f;
            if (sun != null)
            {
                sun.color = sunColor0; sun.intensity = sunIntensity0; sun.shadowStrength = shadowStrength0;
                sun.transform.rotation = sunRotation0; sun.shadows = shadows0;
            }
            RenderSettings.ambientMode = ambientMode0;
            RenderSettings.ambientSkyColor = sky0; RenderSettings.ambientEquatorColor = equator0; RenderSettings.ambientGroundColor = ground0;
            RenderSettings.ambientLight = flat0; RenderSettings.ambientProbe = probe0;
            RenderSettings.fog = fog0; RenderSettings.fogMode = fogMode0; RenderSettings.fogColor = fogColor0;
            RenderSettings.fogStartDistance = fogStart0; RenderSettings.fogEndDistance = fogEnd0; RenderSettings.fogDensity = fogDensity0;
            foreach (var (light, _) in fills) if (light != null) light.enabled = false;
            if (grade != null) grade.Weight = 0f;
        }

        void OnDisable()
        {
            if (applied) Restore();
            weight = 0f;
        }

        int ShellShadowsOff()
        {
            int n = 0;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.shadowCastingMode == ShadowCastingMode.Off || !IsShell(r.transform)) continue;
                r.shadowCastingMode = ShadowCastingMode.Off;
                n++;
            }
            return n;
        }

        bool IsShell(Transform t)
        {
            for (var p = t; p != null && p != transform; p = p.parent)
            {
                string n = p.name.ToLowerInvariant();
                if (n.Contains("bookcase") || n.Contains("table") || n.Contains("frame") || n.Contains("door")) return false;
                foreach (var w in ShellWords)
                    if (n.Contains(w)) return true;
                if (n.StartsWith("hq ", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // A shadowless directional fill, at `strength` times the library's weight (Apply); off until the light comes in.
        void Fill(Transform rig, string name, Quaternion way, Color colour, float strength)
        {
            if (strength <= 0f) return;
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(rig, false);
            light.transform.rotation = transform.rotation * way;
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.color = colour;
            light.intensity = 0f;
            light.enabled = false;
            fills.Add((light, strength));
        }

        // ------------------------------------------------------------------ windows: glow and shafts

        int Windows(Transform rig, float[] zs, float glassX, float sill, float spring, float half, string room)
        {
            float crown = spring + half;
            var glowMat = windowGlow ? Additive(ArchTexture(half * 2f, crown - sill), glowColor * glowStrength) : null;
            var beamMat = shafts ? Additive(BeamTexture(), shaftColor * shaftStrength) : null;
            var parent = new GameObject(room + " windows").transform;
            parent.SetParent(rig, false);
            var dir = Quaternion.Inverse(transform.rotation) * SunDirection();   // the course's frame
            foreach (float z in zs)
            {
                if (glowMat != null)
                {
                    var q = Quad(parent, room + " window glow", new Vector3(glassX - .02f, (sill + crown) * .5f, z),
                                 new Vector2(half * 2f, crown - sill));
                    q.GetComponent<MeshRenderer>().sharedMaterial = glowMat;
                }
                if (beamMat != null)
                    Beam(parent, room + " light shaft", new Vector3(glassX - .3f, sill, z), half * 1.7f, crown - sill - 1.5f,
                         dir, beamMat);
            }
            return zs.Length;
        }

        Material Additive(Texture2D tex, Color tint)
        {
            var m = LitMaterials.NewAdditiveParticles(tex, tint);
            if (m == null) return null;
            m.SetFloat("_Cull", 0f);
            made.Add(m); made.Add(tex);
            return m;
        }

        // A quad facing -x (into the room from a right-hand wall), its centre at `centre`, size (along z, up y).
        GameObject Quad(Transform parent, string name, Vector3 centre, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            var mesh = new Mesh { name = name };
            float a = size.x * .5f, b = size.y * .5f;
            mesh.vertices = new[] { new Vector3(0, -b, a), new Vector3(0, -b, -a), new Vector3(0, b, -a), new Vector3(0, b, a) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            made.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // A soft beam from the window opening (bottom at `foot`, width along z, height up) along the sun to the floor:
        // the four long faces of the prism, u across a face, v from the window (0) to the floor (1).
        void Beam(Transform parent, string name, Vector3 foot, float width, float height, Vector3 dir, Material mat)
        {
            float len = (foot.y - Floor + height * .5f) / Mathf.Max(.05f, -dir.y);
            var c = new[]
            {
                foot + new Vector3(0, 0, -width * .5f), foot + new Vector3(0, 0, width * .5f),
                foot + new Vector3(0, height, width * .5f), foot + new Vector3(0, height, -width * .5f),
            };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 p0 = c[i], p1 = c[(i + 1) % 4];
                int k = v.Count;
                v.Add(p0); v.Add(p1); v.Add(p1 + dir * len); v.Add(p0 + dir * len);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                tri.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            made.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // Round-headed window: bright warm sky, a little brighter at the top, nothing outside the arch.
        static Texture2D ArchTexture(float w, float h)
        {
            const int W = 64, H = 128;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Window glow" };
            float r = w * .5f, spring = h - r;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float px = (x + .5f) / W * w - r, py = (y + .5f) / H * h;
                    float inside = py <= spring ? r - Mathf.Abs(px) : r - Vector2.Distance(new Vector2(px, py), new Vector2(0, spring));
                    float a = Mathf.Clamp01(inside / (r * .12f));
                    float lift = .75f + .25f * (py / h);
                    t.SetPixel(x, y, new Color(lift, lift, lift, a * lift));
                }
            t.Apply();
            return t;
        }

        // Shaft: bright near the window, fading along its length, soft across.
        static Texture2D BeamTexture()
        {
            const int W = 32, H = 128;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Light shaft" };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + .5f) / W, v = (y + .5f) / H;
                    float across = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.6f);
                    float along = Mathf.SmoothStep(0f, 1f, v / .06f) * Mathf.Pow(1f - v, 1.4f);
                    float a = across * along;
                    t.SetPixel(x, y, new Color(a, a, a, a));
                }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ reflections

        void Probe(Transform rig, string name, Vector3 eye, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig, false);
            go.transform.localPosition = eye;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.boxProjection = true;
            probe.hdr = true;
            probe.resolution = 128;
            probe.importance = 2;   // over GameLook's scene-wide probe
            probe.blendDistance = 4f;
            probe.farClipPlane = 500f;
            probe.size = size;
            probe.center = centre - eye;
            StartCoroutine(Render(probe));
        }

        static IEnumerator Render(ReflectionProbe probe)
        {
            yield return new WaitForSeconds(2.5f);   // the course's pieces and giants are built in the first frames
            if (probe != null) probe.RenderProbe();
        }

        void OnDestroy()
        {
            foreach (var o in made)
                if (o != null) Destroy(o);
        }
    }
}
