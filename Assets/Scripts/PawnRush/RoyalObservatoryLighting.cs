using System.Collections;
using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R725 R7 (ArtSource/PawnRush/RoyalObservatory/PLAN.md section 5, the user 11-20 "후처리 먼저" / "한번에 해"): the Royal
    // Observatory's light and screen finish - the concepts RO_1 ~ RO_3: a night hall, cool moonlight from the slit, navy air
    // and shadows, warm gold lamps that glow. Made in code in Play (no scene YAML), the royal library's way
    // (RoyalLibraryLighting): RoyalObservatoryDressing adds it once the models hang (.Lighting.cs).
    //
    //   moon           the scene's directional light, turned to come down through the dome's slit onto the great globe
    //                  (about 57 deg high, from the north-north-east), cool pale blue; the hall's shell (dome, walls, the
    //                  outside, the abyss) stops casting shadows so the moon reaches the course as if through the slit
    //   warm fill      a shadowless warm light from behind the runners (south, above): the faces they look at read gold,
    //                  as the lamps light them in the concepts
    //   ambient        trilight: navy above, violet at the sides, the abyss's dark violet below
    //   haze           thin linear fog in deep navy: the far wall and the abyss step back (far: the approved abyss keeps
    //                  most of its colour)
    //   moon shaft     a soft additive beam (three crossed planes) from the slit down to the globe
    //   grade          ZoneGrade (Game): more contrast, navy shadows, warm gold highlights, a warm bloom a little stronger
    //                  than the game's (lamp globes, gold, the globe's sun, the glass's rims), a light vignette - over
    //                  GameLook's soft grade, under its no-bloom switch (the player's bloom setting still wins)
    //   probes         a box reflection probe for the hall and one inside the great globe, drawn once under this light
    // No depth of field (the library's R7 rule: not on the play view). Off: `on` (the scene as it was).
    // 11-20, the user's first Play: "너무 어두워 졌어" - moon 1 -> 1.5, warm fill .35 -> .75, ambient about twice as bright,
    // exposure 0 -> +.3, haze pushed back (90 ~ 600 m), vignette .2 -> .14.
    //
    // The scene-wide parts (moon, fill, ambient, haze, grade) are the observatory's only while the camera is in it: weight 1
    // inside its box (the tunnel, the hall, the balcony), 0 `blendMetres` out, eased over half a second, and at 0 the
    // scene's own are put back exactly as they were noted. The shaft, the probes and the shell's shadows are the course's
    // own objects and stay.
    // R734 (the user, 11-21: every join between two rooms to change its light smoothly): the moon, the ambient light and
    // the haze go through the one mixer of every place's light (Game ZoneLight) - the ambient as the sky's own harmonics,
    // so it no longer jumps where the observatory's light comes in and goes - and the fade reaches 24 m out (was 8) with
    // a two-second ease. The night sky (RoyalObservatorySky) is still swapped whole: it is seen only through the slit and
    // from the balcony.
    public sealed class RoyalObservatoryLighting : MonoBehaviour
    {
        [Tooltip("The observatory's own light (off: the scene's light as it was)")]
        public bool on = true;
        [Tooltip("Metres outside the observatory over which its light fades out")]
        [Min(.5f)] public float blendMetres = 24f;

        [Header("Moon (the scene's directional light)")]
        [Tooltip("Where the moonlight comes in on the dome: heading (deg, 0 north) - the slit's - and elevation (deg)")]
        public float moonHeading = 10.4f;
        [Range(20f, 84f)] public float moonElevation = 55f;
        public Color moonColor = new Color(.72f, .8f, 1f);
        [Range(0f, 3f)] public float moonIntensity = 1.5f;
        [Range(0f, 1f)] public float shadowStrength = .7f;

        [Header("Warm fill and ambient")]
        [Range(0f, 1.5f)] public float warmFill = .75f;
        public Color warmFillColor = new Color(1f, .78f, .52f);
        [Tooltip("The warm fill's height (deg) and heading (deg, where it comes from; 180 = the south, behind the runners)")]
        public float warmFillHeight = 35f, warmFillFrom = 180f;
        public Color ambientSky = new Color(.52f, .54f, .74f);
        public Color ambientEquator = new Color(.44f, .41f, .56f);
        public Color ambientGround = new Color(.26f, .22f, .34f);

        [Header("Haze")]
        public bool haze = true;
        public Color hazeColor = new Color(.06f, .07f, .15f);
        public float hazeStart = 90f, hazeEnd = 600f;

        [Header("Moon shaft")]
        public bool shaft = true;
        public Color shaftColor = new Color(.7f, .8f, 1f);
        [Range(0f, .5f)] public float shaftStrength = .07f;
        [Tooltip("The shaft's width at the slit and at the globe (m)")]
        public float shaftTop = 9f, shaftFoot = 14f;

        [Header("Grade (over the game's own)")]
        public float exposure = .3f;
        [Range(-100f, 100f)] public float contrast = 12f, saturation = 8f, temperature = -6f, tint = 3f;
        public Color filter = new Color(.97f, .97f, 1f);
        public Vector4 shadowTint = new Vector4(.92f, .95f, 1.12f, 0f), highlightTint = new Vector4(1.08f, 1f, .86f, 0f);
        public float bloomThreshold = .9f, bloomIntensity = .6f;
        [Range(0f, 1f)] public float bloomScatter = .68f;
        public Color bloomTint = new Color(1f, .88f, .7f);
        [Range(0f, 1f)] public float vignette = .14f;

        [Header("Reflections")]
        public bool probes = true;

        // The builder's frame (RoyalObservatoryBuilder): the hall round the great globe, its wall 50 m out, the floor 18 m
        // under the way, the dome's springing at 14 m; the entry tunnel from z 0, the balcony and exit to z 130.
        static readonly Vector3 HallMiddle = new Vector3(0f, 14f, 64f);   // the dome's centre (the wall's top over the globe)
        static readonly Vector3 GlobeCentre = new Vector3(0f, 10f, 64f);
        const float HallRadius = 50f, GlobeRadius = 14f;
        static readonly Bounds Area = new Bounds(new Vector3(0f, -25f, 65f), new Vector3(108f, 190f, 136f));

        // The hall's shell: models and graybox pieces whose shadows would keep the moon out.
        static readonly string[] ShellStarts = { "RoHallDome", "RoHallOutside", "RoHallAbyss", "RoHallNicheBay", "RoHallArcadeBay",
                                                 "RoHallPortal", "Hall wall", "Hall floor", "Dome " };

        readonly List<Object> made = new List<Object>();
        bool built, applied;
        float weight, lastApplied = -1f;
        ZoneGrade grade;
        Light fill;
        readonly List<ReflectionProbe> probeList = new List<ReflectionProbe>();
        float probesAt = float.MaxValue;
        // R734: the moon, ambient and haze in the mixer of every place's light (Game ZoneLight).
        ZoneLight zone;

        public static void AddTo(GameObject content)
        {
            if (content.GetComponent<RoyalObservatoryLighting>() == null) content.AddComponent<RoyalObservatoryLighting>();
        }

        IEnumerator Start()
        {
            if (!on) yield break;
            yield return null;   // the dressing's own after-dress steps (walls, banners, LODs) finish this frame
            var rig = new GameObject("Observatory light").transform;
            rig.SetParent(transform, false);
            int shell = ShellShadowsOff();
            fill = new GameObject("Warm fill").AddComponent<Light>();
            fill.transform.SetParent(rig, false);
            fill.transform.rotation = transform.rotation * Quaternion.Euler(warmFillHeight, warmFillFrom + 180f, 0f);
            fill.type = LightType.Directional;
            fill.shadows = LightShadows.None;
            fill.color = warmFillColor;
            fill.intensity = 0f;
            fill.enabled = false;
            if (shaft) MoonShaft(rig);
            grade = ZoneGrade.Add(rig.gameObject, "Royal Observatory grade", new ZoneGrade.Look
            {
                exposure = exposure, contrast = contrast, saturation = saturation, temperature = temperature, tint = tint,
                filter = filter, shadows = shadowTint, highlights = highlightTint,
                bloomThreshold = bloomThreshold, bloomIntensity = bloomIntensity, bloomScatter = bloomScatter,
                bloomTint = bloomTint, vignette = vignette, vignetteSmoothness = .45f,
            });
            grade.Weight = 0f;
            if (probes)
            {
                Probe(rig, "Hall probe", HallMiddle + new Vector3(0f, 4f, -24f), new Vector3(0f, -2f, 64f),
                      new Vector3(HallRadius * 2f, 84f, HallRadius * 2f), 4f);
                Probe(rig, "Great globe probe", GlobeCentre, GlobeCentre, Vector3.one * (GlobeRadius * 2f), 1f);
            }
            probesAt = Time.time + 2.5f;   // the course's devices and models settle in the first frames
            built = true;
            zone = ZoneLight.Add("Royal Observatory");
            ZoneLight.Own(fill);
            Look();
            Debug.Log("[천문대 조명] 달빛 · 따뜻한 보조광 · 남색 주변광" + (haze ? " · 안개" : "") + " · 색 보정 · 블룸"
                      + (shaft ? " · 달빛 줄기" : "") + (probes ? " · 반사 프로브 2" : "") + ", 그림자 끈 홀 껍데기 " + shell
                      + "개 — 카메라가 천문대 안에 있을 때만");
        }

        // Where the moonlight goes (world): from its point on the dome down to the great globe.
        Vector3 MoonDirection()
        {
            var from = DomePoint(moonHeading, moonElevation);
            return transform.rotation * (GlobeCentre - from).normalized;
        }

        static Vector3 DomePoint(float heading, float elevation)
        {
            float h = heading * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
            return HallMiddle + new Vector3(Mathf.Sin(h) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(h) * Mathf.Cos(e)) * HallRadius;
        }

        // ------------------------------------------------------------------ the observatory's light over the scene's

        // How far in the camera is (1 inside the box, 0 blendMetres out), eased in time (ZoneLight.Ease, about two
        // seconds) so a camera that jumps (a respawn, a spectator switch) or a pawn running in does not flick the light.
        // The moon, ambient and haze are the mixer's (it runs after this); the fill and the grade are the observatory's.
        void LateUpdate()
        {
            if (!built || zone == null) return;
            Look();
            weight = zone.Ease(ZoneLight.Inside(transform, Area, blendMetres));
            if (weight <= .0005f) { if (applied) Restore(); return; }
            applied = true;
            if (Mathf.Abs(weight - lastApplied) > .0005f) Apply(weight);
            // The probes are drawn once, under the observatory's own light (not the scene's day light the first frames
            // may still show).
            if (weight >= .999f && Time.time >= probesAt)
            {
                probesAt = float.MaxValue;
                foreach (var probe in probeList)
                    if (probe != null) probe.RenderProbe();
            }
        }

        // The observatory's look as the mixer takes it (read every frame: the fields may be tuned while playing).
        void Look()
        {
            zone.TurnSun = true;
            zone.SunRotation = Quaternion.LookRotation(MoonDirection());
            zone.SunColor = moonColor;
            zone.SunIntensity = moonIntensity;
            zone.ShadowStrength = shadowStrength;
            zone.SoftShadows = true;
            zone.Sky = ambientSky;
            zone.Equator = ambientEquator;
            zone.Ground = ambientGround;
            zone.Fog = haze;
            zone.FogColor = hazeColor;
            zone.FogStart = hazeStart;
            zone.FogEnd = hazeEnd;
        }

        // w 1 = the observatory's fill and grade whole.
        void Apply(float w)
        {
            lastApplied = w;
            if (fill != null)
            {
                fill.intensity = warmFill * w;
                fill.enabled = fill.intensity > .002f;
            }
            if (grade != null) grade.Weight = w;
        }

        void Restore()
        {
            applied = false;
            lastApplied = -1f;
            if (fill != null) fill.enabled = false;
            if (grade != null) grade.Weight = 0f;
        }

        void OnDisable()
        {
            if (applied) Restore();
            weight = 0f;
            if (zone != null) zone.Weight = 0f;
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
                foreach (var s in ShellStarts)
                    if (p.name.StartsWith(s, System.StringComparison.Ordinal)) return true;
            return false;
        }

        // ------------------------------------------------------------------ the moon shaft

        // From the slit (the moon's point on the dome) to just over the great globe: three planes crossed along the
        // beam (it reads from any side), widening down, bright under the slit and gone before the glass.
        void MoonShaft(Transform rig)
        {
            var tex = ShaftTexture();
            made.Add(tex);
            var mat = LitMaterials.NewAdditiveParticles(tex, shaftColor * shaftStrength);
            if (mat == null) return;
            mat.SetFloat("_Cull", 0f);
            made.Add(mat);
            var slit = DomePoint(moonHeading, moonElevation);
            var top = slit - (slit - HallMiddle).normalized * 1.5f;
            var axis = (GlobeCentre - top).normalized;
            var foot = GlobeCentre - axis * (GlobeRadius + 1f);
            var side = Vector3.Cross(axis, Vector3.up).normalized;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                var across = Quaternion.AngleAxis(i * 60f, axis) * side;
                int k = v.Count;
                v.Add(top - across * shaftTop * .5f); v.Add(top + across * shaftTop * .5f);
                v.Add(foot + across * shaftFoot * .5f); v.Add(foot - across * shaftFoot * .5f);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                tri.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            }
            var mesh = new Mesh { name = "Moon shaft" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            made.Add(mesh);
            var go = new GameObject("Moon shaft");
            go.transform.SetParent(rig, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // Soft across, bright just under the slit, fading to nothing before the globe.
        static Texture2D ShaftTexture()
        {
            const int W = 32, H = 128;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Moon shaft" };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + .5f) / W, s = (y + .5f) / H;
                    float across = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2f);
                    float along = Mathf.SmoothStep(0f, 1f, s / .08f) * Mathf.Pow(1f - s, 1.6f);
                    float a = across * along;
                    t.SetPixel(x, y, new Color(a, a, a, a));
                }
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------------ reflections

        void Probe(Transform rig, string name, Vector3 eye, Vector3 centre, Vector3 size, float blend)
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
            probe.blendDistance = blend;
            probe.farClipPlane = 400f;
            probe.size = size;
            probe.center = centre - eye;
            probeList.Add(probe);
        }

        void OnDestroy()
        {
            zone?.Remove();
            foreach (var o in made)
                if (o != null) Destroy(o);
        }
    }
}
