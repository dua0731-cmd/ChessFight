using System.Collections;
using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R683 (the carriage yard's light and air, RoyalCarriageYard.Lighting.cs builds it): the yard's sun, sky light, haze,
    // sky, colour grade and lamps, faded in by where the camera is. The yard's box (its own frame) and `blend` metres round
    // it: weight 1 inside, 0 that far out, so as a section of the round the scene's own light comes back as the camera
    // leaves for the next room, and the round's other rooms never see the yard's. The whole map (map test scene) holds
    // weight 1 everywhere.
    //
    // At weight 0 nothing of the scene's is touched; the first frame over 0 it notes the scene's sky and from then on
    // blends from it; back at 0 (or switched off, or destroyed) it is put back exactly.
    // R734 (the user: "마차뜰에서 도개교로 넘어갈 때 후처리 때문에 조명이 확 바껴"): the sun, the ambient light and the haze go
    // through the one mixer of every place's light (Game ZoneLight) - the ambient blended as the sky's own harmonics, so
    // it no longer jumps as the yard's light comes in and goes - and the weight follows the camera smoothly over about
    // two seconds (ZoneLight.Ease) instead of half a second; the grade, the fills, the sky and the lamps follow that weight.
    // Pictures only: no collider, no physics, nothing synced (every PC fades by its own camera).
    public sealed class YardAtmosphere : MonoBehaviour
    {
        public sealed class Target
        {
            public bool turnSun;
            public Quaternion sunRotation;
            public Color sunColor;
            public float sunIntensity, shadowStrength;
            public Color sky, horizon, ground;
            public float skyFill, bounceFill;
            public Color skyFillColor, bounceFillColor;
            public bool fog;
            public Color fogColor;
            public float fogStart, fogEnd;
            public float clouds;
        }

        // A lamp the yard lit (a point light) and how it is kept: always at `intensity` x the yard's weight, off past the
        // tier's reach from the camera, with an oil flame's flicker, or in the colour of a cue lamp it follows (a coach's
        // amber that goes red through a charge, a hoist's green / amber / red).
        sealed class Lamp
        {
            public Light light;
            public float intensity, seed;
            public int tier;
            public bool flicker;
            public Renderer follows;
            public bool keepsIntensity;   // a MiniGameFlame torch flickers its own light: only switched on and off here
        }

        Target target;
        Bounds area;
        float blend;
        bool always;
        ZoneGrade grade;
        Light fillSky, fillBounce;
        readonly List<Lamp> lamps = new List<Lamp>();
        readonly List<ReflectionProbe> probes = new List<ReflectionProbe>();
        System.Func<Transform, List<ReflectionProbe>> makeProbes;
        bool probesWanted, probesBaked;
        int maxTier = 2, tier = -1;

        // The yard's sun, ambient and haze in the mixer of every place's light (R734).
        ZoneLight zone;
        // The scene's sky, noted the moment the yard's light comes in.
        bool applied;
        float weight, lastApplied = -1f;
        Material sky0Material, skyInstance;
        enum SkyKind { None, PawnRush, Procedural }
        SkyKind skyKind;
        Vector4 skySunDir0;
        float skyCover0, skyThickness0, skyExposure0;
        Color skyTint0;

        static readonly int SunDirId = Shader.PropertyToID("_SunDir"), CoverageId = Shader.PropertyToID("_Coverage"),
            ThicknessId = Shader.PropertyToID("_AtmosphereThickness"), ExposureId = Shader.PropertyToID("_Exposure"),
            TintId = Shader.PropertyToID("_SkyTint");

        public float Weight => weight;
        public int Tier => tier;

        public void Configure(Target to, Bounds localArea, float blendMetres, bool wholeMap, ZoneGrade zoneGrade, int tierCap,
                              Light skyLight, Light bounceLight, System.Func<Transform, List<ReflectionProbe>> probeMaker)
        {
            target = to;
            area = localArea;
            blend = Mathf.Max(.5f, blendMetres);
            always = wholeMap;
            grade = zoneGrade;
            maxTier = Mathf.Clamp(tierCap, 0, 2);
            fillSky = skyLight;
            fillBounce = bounceLight;
            makeProbes = probeMaker;
            probesWanted = probeMaker != null;
            if (fillSky != null) fillSky.enabled = false;
            if (fillBounce != null) fillBounce.enabled = false;
            ZoneLight.Own(fillSky);
            ZoneLight.Own(fillBounce);
            if (zone == null) zone = ZoneLight.Add("Royal Carriage Yard");
            zone.TurnSun = to.turnSun;
            zone.SunRotation = to.sunRotation;
            zone.SunColor = to.sunColor;
            zone.SunIntensity = to.sunIntensity;
            zone.ShadowStrength = to.shadowStrength;
            zone.BringSun = wholeMap;   // the whole map in a scene with no sun: the yard brings its own
            zone.Sky = to.sky;
            zone.Equator = to.horizon;
            zone.Ground = to.ground;
            zone.Fog = to.fog;
            zone.FogColor = to.fogColor;
            zone.FogStart = to.fogStart;
            zone.FogEnd = to.fogEnd;
            UpdateTier();
        }

        public void AddLamp(Light light, float intensity, int minTier, bool flicker, Renderer follows = null, bool keepsIntensity = false)
        {
            if (light == null) return;
            light.enabled = false;
            lamps.Add(new Lamp
            {
                light = light, intensity = intensity, tier = minTier, flicker = flicker, follows = follows,
                keepsIntensity = keepsIntensity, seed = Random.value * 100f,
            });
        }

        public int LampCount(int atTier)
        {
            int n = 0;
            foreach (var lamp in lamps) if (lamp.tier <= atTier) n++;
            return n;
        }

        void OnEnable()
        {
            GameSettings.Changed -= UpdateTier;
            GameSettings.Changed += UpdateTier;
        }

        void OnDisable()
        {
            GameSettings.Changed -= UpdateTier;
            if (applied) Restore();
            weight = 0f;
            if (zone != null) zone.Weight = 0f;
        }

        void OnDestroy()
        {
            if (applied) Restore();
            if (skyInstance != null) Destroy(skyInstance);
            zone?.Remove();
        }

        // ------------------------------------------------------------------ quality

        // 0 low (the "Mobile" quality level or the 낮음 preset), 1 medium, 2 high; a custom preset by its shadow step.
        static int SettingsTier()
        {
            int q = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            if (q >= 0 && q < names.Length && names[q].Contains("Mobile")) return 0;
            int preset = GameSettings.Preset;
            if (preset <= 2) return Mathf.Max(0, preset);
            int shadow = GameSettings.Shadow;
            return shadow >= 3 ? 2 : shadow >= 2 ? 1 : 0;
        }

        void UpdateTier()
        {
            int now = Mathf.Min(SettingsTier(), maxTier);
            if (now == tier) return;
            tier = now;
            lastApplied = -1f;   // the fills follow the tier: apply again
            if (tier >= 1 && probesWanted && probes.Count == 0 && makeProbes != null) probes.AddRange(makeProbes(transform));
            foreach (var probe in probes) if (probe != null) probe.enabled = tier >= 1;
            if (tier >= 1 && probes.Count > 0 && !probesBaked && isActiveAndEnabled) StartCoroutine(BakeProbes());
            Debug.Log("[마차뜰 조명] 품질 단계 " + tier + ": 보조광 " + (tier >= 2 ? 2 : tier) + "개, 반사 프로브 "
                      + (tier >= 1 ? probes.Count : 0) + "개, 등불 " + LampCount(tier) + "개");
        }

        // The probes see the yard once it is built and dressed (the models go on in the yard's Awake, the castle's
        // labels after; GameLook's own probe waits 1.5 s), one face a frame.
        IEnumerator BakeProbes()
        {
            probesBaked = true;
            yield return new WaitForSecondsRealtime(2.5f);
            foreach (var probe in probes)
            {
                if (probe == null || !probe.enabled) continue;
                int id = probe.RenderProbe();
                for (int frames = 0; frames < 30 && !probe.IsFinishedRendering(id); frames++) yield return null;
            }
        }

        void Start()
        {
            if (tier >= 1 && probes.Count > 0 && !probesBaked) StartCoroutine(BakeProbes());
        }

        // ------------------------------------------------------------------ the fade

        // 1 inside the yard's box, 0 `blend` metres out (the whole map: 1 everywhere); -1 with no camera yet (held).
        float AreaWeight() => always ? 1f : ZoneLight.Inside(transform, area, blend);

        void LateUpdate()
        {
            if (target == null || zone == null) return;
            // Eased in time too (ZoneLight.Ease, about two seconds), so a camera that jumps (a respawn, a spectator
            // switch) or a pawn running through the corridor does not flick the light. The sun, ambient and haze are the
            // mixer's (it runs after this); the rest is the yard's own.
            weight = zone.Ease(AreaWeight());
            if (weight <= .0005f)
            {
                if (applied) Restore();
            }
            else
            {
                if (!applied) Capture();
                var sun = ZoneLight.Sun;
                var way = sun != null ? sun.transform.rotation : target.sunRotation;
                if (Mathf.Abs(weight - lastApplied) > .0005f || Quaternion.Angle(way, lastSunWay) > .05f) Apply(weight, sun, way);
            }
            Lamps();
        }

        Quaternion lastSunWay = Quaternion.identity;

        void Capture()
        {
            applied = true;
            lastApplied = -1f;
            sky0Material = RenderSettings.skybox;
            skyKind = SkyKind.None;
            if (skyInstance != null) Destroy(skyInstance);
            skyInstance = null;
            if (sky0Material != null && sky0Material.shader != null)
            {
                string shader = sky0Material.shader.name;
                if (shader == "ChessFight/Pawn Rush Sky") skyKind = SkyKind.PawnRush;
                else if (shader == "Skybox/Procedural") skyKind = SkyKind.Procedural;
                if (skyKind != SkyKind.None)
                {
                    skyInstance = new Material(sky0Material) { name = sky0Material.name + " (yard)" };
                    if (skyInstance.HasProperty(SunDirId)) skySunDir0 = skyInstance.GetVector(SunDirId);
                    if (skyInstance.HasProperty(CoverageId)) skyCover0 = skyInstance.GetFloat(CoverageId);
                    if (skyInstance.HasProperty(ThicknessId)) skyThickness0 = skyInstance.GetFloat(ThicknessId);
                    if (skyInstance.HasProperty(ExposureId)) skyExposure0 = skyInstance.GetFloat(ExposureId);
                    if (skyInstance.HasProperty(TintId)) skyTint0 = skyInstance.GetColor(TintId);
                }
            }
        }

        void Apply(float w, Light sun, Quaternion sunNow)
        {
            lastApplied = w;
            lastSunWay = sunNow;
            var t = target;
            // The fills stand opposite the sun as it is now (the round's own sun keeps its way as a section).
            float sunYaw = sunNow.eulerAngles.y;
            if (fillSky != null)
            {
                fillSky.transform.rotation = Quaternion.Euler(52f, sunYaw + 180f, 0f);
                fillSky.color = t.skyFillColor;
                fillSky.intensity = t.skyFill * w;
                fillSky.enabled = tier >= 1 && fillSky.intensity > .002f;
            }
            if (fillBounce != null)
            {
                // Up from the sunlit paving: under the eaves, the balconies, the coaches and the cranes' jibs.
                fillBounce.transform.rotation = Quaternion.Euler(-35f, sunYaw, 0f);
                fillBounce.color = t.bounceFillColor;
                fillBounce.intensity = t.bounceFill * w;
                fillBounce.enabled = tier >= 2 && fillBounce.intensity > .002f;
            }

            if (skyInstance != null)
            {
                if (skyKind == SkyKind.PawnRush)
                {
                    if (skyInstance.HasProperty(CoverageId)) skyInstance.SetFloat(CoverageId, Mathf.Lerp(skyCover0, t.clouds, w));
                    // The sun's glow where the sun is.
                    if (skyInstance.HasProperty(SunDirId) && sun != null)
                        skyInstance.SetVector(SunDirId, Vector4.Lerp(skySunDir0, -sun.transform.forward, w));
                }
                else
                {
                    // Unity's default sky: a little bluer and clearer, as the concept's midday.
                    if (skyInstance.HasProperty(ThicknessId)) skyInstance.SetFloat(ThicknessId, Mathf.Lerp(skyThickness0, .8f, w));
                    if (skyInstance.HasProperty(TintId)) skyInstance.SetColor(TintId, Color.Lerp(skyTint0, new Color(.44f, .53f, .68f), w));
                    if (skyInstance.HasProperty(ExposureId)) skyInstance.SetFloat(ExposureId, Mathf.Lerp(skyExposure0, Mathf.Max(skyExposure0, 1.2f), w));
                }
                if (RenderSettings.skybox != skyInstance) RenderSettings.skybox = skyInstance;
            }

            if (grade != null) grade.Weight = w;
        }

        void Restore()
        {
            applied = false;
            lastApplied = -1f;
            if (fillSky != null) fillSky.enabled = false;
            if (fillBounce != null) fillBounce.enabled = false;
            if (skyInstance != null && RenderSettings.skybox == skyInstance) RenderSettings.skybox = sky0Material;
            if (grade != null) grade.Weight = 0f;
        }

        // ------------------------------------------------------------------ lamps

        // Reach from the camera at which a lamp is still lit (Forward+ pays per light on screen; past this its pool of
        // light is a few pixels).
        static readonly float[] Reach = { 0f, 32f, 48f };

        void Lamps()
        {
            if (lamps.Count == 0) return;
            float w = weight;
            var view = ZoneLight.View();
            Vector3 eye = view != null ? view.transform.position : transform.position;
            float reach = Reach[Mathf.Clamp(tier, 0, 2)], reach2 = reach * reach;
            float time = Time.time;
            foreach (var lamp in lamps)
            {
                var light = lamp.light;
                if (light == null) continue;
                bool on = w > .002f && tier >= lamp.tier && (light.transform.position - eye).sqrMagnitude < reach2;
                if (light.enabled != on) light.enabled = on;
                if (!on || lamp.keepsIntensity) continue;
                float k = 1f;
                if (lamp.flicker) k = .9f + .2f * Mathf.PerlinNoise(time * 2.3f + lamp.seed, lamp.seed * .37f);
                if (lamp.follows != null)
                {
                    var m = lamp.follows.sharedMaterial;
                    if (m != null)
                    {
                        var c = m.color;
                        c.a = 1f;
                        light.color = c;
                        // A warning (red) is meant to be seen: its pool is brighter than a resting lamp's.
                        if (c.r > c.g * 1.6f) k *= 2.4f;
                    }
                    if (!lamp.follows.enabled || !lamp.follows.gameObject.activeInHierarchy) k = 0f;
                }
                light.intensity = lamp.intensity * k * w;
            }
        }
    }
}
