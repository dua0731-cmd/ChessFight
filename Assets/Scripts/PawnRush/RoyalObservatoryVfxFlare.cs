using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx (Royal Observatory R7, device VFX 2 of 7; user 11-20 "대천구 태양은 신경 많이 써서 플레이어가 들어갈 수
    // 있는 그림자 효과 등등 신경을 많이 써야 돼"): the great globe's solar flare. SolarFlare says when (the shared clock)
    // and the planets' orbits run on the same clock, so this only draws and every PC shows the same moment; nothing is sent.
    //   shadows  each planet's real shadow from the lamp's middle (Resources/RoyalObservatory/Vfx/RoSolarFlare.shader, a
    //            decal over the floor): an ellipse on the floor stretched away from under the lamp, and on a pawn standing
    //            in it the pawn itself - the shadow SolarFlare.Sheltered judges by (the user: "실제 빛 그림자", "판정도
    //            그림자에 맞춤", "폰도 어둡게"). Half dark when calm; deeper, with a sharp cool edge, in the warning and the
    //            burn, so where to stand reads
    //   warning  heat creeping out over the lit floor from under the lamp (its front at the floor's edge as the burn
    //            begins), pawns out of the shadows warming on their lamp side; the sun's flames and corona grow
    //            (RoSunCorona.shader) and draw in just before it bursts, its surface and its light brighten
    //   burn     the lit floor blazes and a ripple of light runs out over it, the air over it shimmers, exposed pawns
    //            glow hot on their lamp side; the sun flashes and its corona throws out
    //   caught   (user 11-20 "그림자 밖에서 맞으면 플레이어들 화면 테두리에 섬광 같은 효과 추가 해줘") the player whose own
    //            pawn is out of the shadows in the burn sees their screen's edges flare white-gold and fade - once a
    //            flare, from the first moment of the burn it is caught, by SolarFlare's own test (on the floor's disc,
    //            -1 .. 4 m over it, not Sheltered) with this PC's pawn (MiniGameBase.LocalDriver); drawn as
    //            PawnRushFallNotice draws its edges (IMGUI, not over the loading screen). Nothing is sent: each PC
    //            looks at its own pawn, so near a shadow's edge online it can differ from the host's throw by a hair.
    // The graybox's flat discs (its light, front and shadows) stop drawing. The planets and the sun stop throwing the
    // moon's shadow on the floor (the user: no shelters that are not shelters). Further than `farDistance` from the
    // camera nothing extra is drawn.
    [DisallowMultipleComponent]
    public sealed class RoyalObservatoryVfxFlare : MonoBehaviour
    {
        const string DecalPath = "RoyalObservatory/Vfx/RoSolarFlare", CoronaPath = "RoyalObservatory/Vfx/RoSunCorona";
        const int MaxPlanets = 8;
        // The decal's box over the floor: from under the floor up over a pawn's head, below the lowest planet (2.3 m).
        const float BoxBottom = -.6f, BoxTop = 2.2f;

        // Off: the flare as modelling left it (a switch for comparing).
        public static bool Enabled = true;

        static readonly int PlanetsId = Shader.PropertyToID("_Planets"), PlanetCountId = Shader.PropertyToID("_PlanetCount"),
            SunId = Shader.PropertyToID("_Sun"), SunWorldId = Shader.PropertyToID("_SunWorld"), FloorUpId = Shader.PropertyToID("_FloorUp"),
            BoxId = Shader.PropertyToID("_Box"), BoxCentreId = Shader.PropertyToID("_BoxCentre"), ReachId = Shader.PropertyToID("_Reach"),
            AlertId = Shader.PropertyToID("_Alert"), WarnId = Shader.PropertyToID("_Warn"), BurnId = Shader.PropertyToID("_Burn"),
            FrontId = Shader.PropertyToID("_Front"), RippleId = Shader.PropertyToID("_Ripple"), RippleFadeId = Shader.PropertyToID("_RippleFade"),
            AgeId = Shader.PropertyToID("_Age"), ShadeCalmId = Shader.PropertyToID("_ShadeCalm"), ShadeFlareId = Shader.PropertyToID("_ShadeFlare"),
            BodyShadeId = Shader.PropertyToID("_BodyShade"), RimLightId = Shader.PropertyToID("_RimLight"), HeatLightId = Shader.PropertyToID("_HeatLight"),
            FrontLightId = Shader.PropertyToID("_FrontLight"), BlazeLightId = Shader.PropertyToID("_BlazeLight"),
            RippleLightId = Shader.PropertyToID("_RippleLight"), BodyGlowId = Shader.PropertyToID("_BodyGlow"), ShimmerId = Shader.PropertyToID("_Shimmer"),
            LightId = Shader.PropertyToID("_Light"), ReachRadiiId = Shader.PropertyToID("_CoronaReach"), FlamesId = Shader.PropertyToID("_Flames"),
            FlameReachId = Shader.PropertyToID("_FlameReach"), FlashId = Shader.PropertyToID("_Flash"), SunRadiusId = Shader.PropertyToID("_SunRadius"),
            SizeId = Shader.PropertyToID("_Size"), EmissionId = Shader.PropertyToID("_EmissionColor"),
            ShockId = Shader.PropertyToID("_Shock"), ShockLightId = Shader.PropertyToID("_ShockLight"),
            RaysId = Shader.PropertyToID("_Rays"), RayReachId = Shader.PropertyToID("_RayReach");

        [Header("Shadows (the planets' real shadows from the lamp)")]
        [Range(0f, 1f), Tooltip("How dark a shadow is while the lamp is calm, and in the warning and the burn.")]
        public float shadeCalm = .5f, shadeFlare = .82f;
        [Range(0f, 1f), Tooltip("Share of that darkness on a pawn standing in a shadow.")]
        public float bodyShade = .8f;
        [Tooltip("The shadows' sharp cool edge on the floor (most in the warning and the burn).")]
        public float rimLight = 1.4f;

        [Header("The floor in the warning and the burn")]
        public float heatLight = .45f, frontLight = 1.8f, blazeLight = 1.3f, rippleLight = 5.5f;
        [Tooltip("Light on the lamp side of pawns out of the shadows (a third of it in the warning).")]
        public float bodyGlow = 2.2f;
        [Range(0f, .02f), Tooltip("Heat shimmer over the lit floor in the burn (share of the screen; 0 = off).")]
        public float shimmer = .006f;
        [Tooltip("Seconds the burn's ripple takes out to the floor's edge.")]
        public float rippleSeconds = .25f;

        [Header("The sun")]
        [Tooltip("Times its own light at the end of the warning, and as the burn begins.")]
        public float lightWarn = 1.45f, lightBurn = 4.2f;
        [Tooltip("Times its surface's own glow at the end of the warning, and as the burn begins.")]
        public float glowWarn = 1.8f, glowBurn = 4.2f;
        [Tooltip("The corona's light: calm, at the end of the warning, and the burn's flash.")]
        public float coronaCalm = .5f, coronaWarn = 1.3f, coronaFlash = 5f;

        [Header("The burst (user 11-20 \"더 폭발 하는 것처럼\")")]
        [Tooltip("The ring of light thrown out from the sun: how far (sun radii), how bright, how long (s).")]
        public float shockReach = 3.2f, shockLight = 2.6f, shockSeconds = .45f;
        [Tooltip("Thin rays shooting out all round: how bright, how long (sun radii).")]
        public float rayLight = 3f, rayReach = 3.4f;
        [Range(0, 160), Tooltip("Sparks thrown out of the sun (they fall to the floor and slide).")]
        public int ejectaCount = 70;
        public float ejectaLight = 6f;
        [Tooltip("Seconds after the burn the sun and the floor take to settle.")]
        public float settle = .5f;

        [Header("Caught in the burn: this player's screen edges flare")]
        [Tooltip("The flash's hot edge colour (its glow inward is orange).")]
        public Color edgeFlash = new Color(1f, .9f, .62f);
        [Range(0f, 1f)] public float edgeFlashStrength = .85f;
        [Tooltip("Seconds the flash takes to come and go.")]
        public float edgeFlashSeconds = .75f;

        [Header("Cost")]
        [Tooltip("Further than this from the camera (m) nothing extra is drawn.")]
        public float farDistance = 90f;

        SolarFlare flare;
        Material decalMaterial, coronaMaterial, ejectaMaterial;
        MeshRenderer decal, corona, ejecta;
        Mesh quad, ejectaMesh;
        float ejectaLife;
        MaterialPropertyBlock block;
        readonly Vector4[] planets = new Vector4[MaxPlanets];
        Light sunLight;
        float sunLight0;
        Color sunColor0;
        readonly List<Material> sunGlow = new List<Material>();
        readonly List<Color> sunGlow0 = new List<Color>();
        float lookForSun;
        bool ready, sunDone;
        float reach;
        // This PC's pawn caught in a burn: which flare (once each) and when; the pawn whose collider was looked up.
        long caughtFlare = long.MinValue;
        float caughtAt = -100f;
        Transform caughtPawn;
        Collider caughtCollider;
        Texture2D edgeWide, edgeTight;

        void Start()
        {
            flare = GetComponent<SolarFlare>();
            block = new MaterialPropertyBlock();
            var decalShader = Resources.Load<Shader>(DecalPath);
            var coronaShader = Resources.Load<Shader>(CoronaPath);
            if (flare == null || decalShader == null || !decalShader.isSupported)
            {
                Debug.LogWarning("[천문대 VFX] 태양 플레어 효과 셰이더가 없습니다(Resources/" + DecalPath + "): 회색 상자 그대로");
                enabled = false;
                return;
            }
            reach = flare.Radius + .35f;   // the floor's own edge (the builder: the flare reaches 0.35 m short of it)
            decalMaterial = new Material(decalShader) { name = "RO solar flare (decal)" };
            float side = 2f * (reach + .5f);
            decal = Part("Flare and shadows (decal)", transform, Resources.GetBuiltinResource<Mesh>("Cube.fbx"), decalMaterial);
            decal.transform.localPosition = Vector3.up * ((BoxBottom + BoxTop) * .5f);
            decal.transform.localScale = new Vector3(side, BoxTop - BoxBottom, side);
            if (coronaShader != null && coronaShader.isSupported)
            {
                coronaMaterial = new Material(coronaShader) { name = "RO sun corona" };
                quad = Quad();
                corona = Part("Sun corona", transform, quad, coronaMaterial);
                corona.transform.localPosition = flare.Sun;
            }
            // The burst's sparks: the hazards' particle shader (drawn from age alone), thrown out of the sun.
            var burstShader = ChessFight.VFX.VfxShaderKeep.Find("ChessFight/VFX/Burst Particles");
            if (burstShader != null && ejectaCount > 0)
            {
                ejectaMaterial = new Material(burstShader) { name = "RO sun ejecta" };
                ejectaMesh = EjectaMesh();
                ejecta = Part("Sun ejecta", transform, ejectaMesh, ejectaMaterial);
                ejecta.transform.localPosition = flare.Sun;
                ejecta.enabled = false;
            }
            // The graybox's flat light, front and shadows: SolarFlare still moves them, they no longer draw.
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == "Flare light" || child.name == "Flare front" || child.name.StartsWith("Planet shadow"))
                    foreach (var r in child.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
            // No moon shadows from the planets and the sun on the floor: the only planet shadows are the lamp's.
            int quiet = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child.name.StartsWith("Device_PlanetOrbit") && child.name != "Sun lamp") continue;
                foreach (var r in child.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    quiet++;
                }
            }
            ready = true;
            Debug.Log("[천문대 VFX] 태양 플레어: 행성 " + flare.PlanetCount + "개의 실제 그림자(판정과 같음) · 열 · 섬광 · 코로나"
                      + (corona == null ? " (코로나 셰이더 없음)" : "") + ", 달빛 그림자 끈 행성 · 태양 렌더러 " + quiet + "개");
        }

        static MeshRenderer Part(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }

        // A quad round the origin, its corners in uv; the corona's shader turns it to the camera and sizes it.
        static Mesh Quad()
        {
            var mesh = new Mesh { name = "RO sun corona quad", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(new List<Vector3> { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 32f);   // the largest the corona gets, with room
            return mesh;
        }

        // VFX_BurstParticles: a quad a spark, all starting on the sun's surface as it pops (its middle the mesh's origin)
        // and flying straight out every way, slowed by the air, pulled to the floor (SolarFlare's frame: down is -y).
        Mesh EjectaMesh()
        {
            var rng = new System.Random(61);
            float Next() => (float)rng.NextDouble();
            float surface = 2.2f * (SolarFlare.WarnPeak - SolarFlare.Inhale);   // the lamp's radius as the burn begins
            var points = new List<Vector3>();
            var corners = new List<Vector2>();
            var particles = new List<Vector4>();
            var velocities = new List<Vector3>();
            var triangles = new List<int>();
            ejectaLife = 0f;
            for (int i = 0; i < ejectaCount; i++)
            {
                // even over the sphere
                float y = 1f - 2f * (i + .5f) / ejectaCount, ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y)), turn = i * 2.39996f + Next() * .4f;
                var dir = new Vector3(Mathf.Cos(turn) * ring, y, Mathf.Sin(turn) * ring);
                float life = Mathf.Lerp(.55f, 1.15f, Next()), delay = Next() * .08f;
                ejectaLife = Mathf.Max(ejectaLife, life + delay);
                var particle = new Vector4(life, Mathf.Lerp(.7f, 1.4f, Next()), delay, 0f);
                var start = dir * surface;
                var velocity = dir * Mathf.Lerp(8f, 17f, Next());
                int at = points.Count;
                for (int c = 0; c < 4; c++)
                {
                    points.Add(start);
                    corners.Add(new Vector2(c & 1, c >> 1));
                    particles.Add(particle);
                    velocities.Add(velocity);
                }
                triangles.AddRange(new[] { at, at + 2, at + 1, at + 2, at + 3, at + 1 });
            }
            var mesh = new Mesh { name = "RO sun ejecta", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, particles);
            mesh.SetUVs(2, velocities);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 28f);   // out to the floor and a little past
            return mesh;
        }

        void EjectaLook(float age)
        {
            ejecta.GetPropertyBlock(block);
            block.SetColor("_ColorStart", new Color(1f, .95f, .8f));
            block.SetColor("_ColorEnd", new Color(1f, .45f, .12f));
            block.SetFloat(LightId, ejectaLight);
            block.SetFloat("_AlphaStart", 1f);
            block.SetFloat("_AlphaEnd", 0f);
            block.SetFloat("_FadeIn", .02f);
            block.SetFloat("_SizeStart", .14f);
            block.SetFloat("_SizeEnd", .05f);
            block.SetFloat("_Stretch", .03f);
            block.SetFloat("_Soft", .8f);
            block.SetFloat("_Hard", 0f);
            block.SetFloat("_Additive", 1f);
            block.SetFloat("_DepthFade", 0f);
            block.SetFloat("_Gravity", 5f);
            block.SetFloat("_Drag", 2.4f);
            block.SetFloat("_FloorY", -flare.Sun.y);
            block.SetFloat(AgeId, age);
            ejecta.SetPropertyBlock(block);
        }

        // The sun's light (GlobeSun) and its surface's glow (RO_PlanetSun) on the model riding the lamp.
        void FindSun()
        {
            var lamp = flare.Lamp;
            if (lamp == null) { sunDone = true; return; }
            if (sunLight == null)
            {
                sunLight = lamp.GetComponentInChildren<Light>(true);
                if (sunLight != null)
                {
                    sunLight0 = sunLight.intensity;
                    sunColor0 = sunLight.color;
                }
            }
            if (sunGlow.Count == 0)
                foreach (var r in lamp.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;   // the graybox sphere
                    bool any = false;
                    foreach (var m in r.sharedMaterials) any |= m != null && m.name.StartsWith("RO_PlanetSun");
                    if (!any) continue;
                    var mats = r.materials;   // this renderer's own copies
                    foreach (var m in mats)
                        if (m != null && m.name.StartsWith("RO_PlanetSun") && m.HasProperty(EmissionId))
                        {
                            sunGlow.Add(m);
                            sunGlow0.Add(m.GetColor(EmissionId));
                        }
                    r.materials = mats;
                }
            // The light is added by the dressing beside this; look a few seconds, then stop.
            lookForSun += Time.deltaTime;
            sunDone = sunLight != null && sunGlow.Count > 0 || lookForSun > 3f;
        }

        void LateUpdate()
        {
            if (!ready) return;
            if (!sunDone) FindSun();
            var cam = Camera.main;
            bool near = cam != null && (cam.transform.position - transform.position).sqrMagnitude < farDistance * farDistance;
            if (!Enabled || !near)
            {
                Show(false);
                return;
            }
            Show(true);

            // Where in the flare's cycle the shared clock is.
            double now = ObstacleClock.Now;
            CheckCaught(now);
            float t = flare.CycleTime(now), calm = flare.Calm, warn = Mathf.Max(flare.Warn, .01f), burn = Mathf.Max(flare.Burn, .01f);
            bool warning = t >= calm && t < calm + warn, burning = t >= calm + warn;
            float w = warning ? (t - calm) / warn : 0f;                    // 0 .. 1 over the warning
            float bt = burning ? t - calm - warn : 0f, b = bt / burn;      // seconds into the burn, 0 .. 1
            float after = !warning && !burning && t < settle ? 1f - t / Mathf.Max(settle, .01f) : 0f;   // just after a burn
            float alert = warning ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(w / .2f)) : burning ? 1f : after;
            // The burn comes on over its first 0.1 s rather than in one frame (user 11-20: "끊기면서" - nothing jumps).
            float attack = burning ? Mathf.SmoothStep(0f, 1f, bt / .1f) : 0f;
            float age = (float)(now % 600.0);

            // The planets as SolarFlare sees them: middle and radius in its frame.
            int count = Mathf.Min(flare.PlanetCount, MaxPlanets);
            for (int i = 0; i < MaxPlanets; i++)
                planets[i] = i < count && flare.Body(i, out var middle, out float size) ? new Vector4(middle.x, middle.y, middle.z, size) : Vector4.zero;
            float side = 2f * (reach + .5f);
            decal.GetPropertyBlock(block);
            block.SetVectorArray(PlanetsId, planets);
            block.SetFloat(PlanetCountId, count);
            block.SetVector(SunId, flare.Sun);
            block.SetVector(SunWorldId, transform.TransformPoint(flare.Sun));
            block.SetVector(FloorUpId, transform.up);
            block.SetVector(BoxId, new Vector4(side, BoxTop - BoxBottom, side, 0f));
            block.SetVector(BoxCentreId, new Vector4(0f, (BoxBottom + BoxTop) * .5f, 0f, 0f));
            block.SetFloat(ReachId, reach);
            block.SetFloat(AlertId, alert);
            block.SetFloat(WarnId, warning ? w : burning ? 1f - attack : 0f);
            block.SetFloat(BurnId, burning ? attack * (1f - .55f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, b))) : after * .45f);
            block.SetFloat(FrontId, warning ? reach * w : burning ? reach : 0f);
            block.SetFloat(RippleId, burning ? reach * Mathf.Clamp01(bt / Mathf.Max(rippleSeconds, .01f)) : 0f);
            block.SetFloat(RippleFadeId, burning ? 1f - Mathf.Clamp01((bt - rippleSeconds * .6f) / Mathf.Max(rippleSeconds * .8f, .01f)) : 0f);
            block.SetFloat(AgeId, age);
            block.SetFloat(ShadeCalmId, shadeCalm);
            block.SetFloat(ShadeFlareId, shadeFlare);
            block.SetFloat(BodyShadeId, bodyShade);
            block.SetFloat(RimLightId, rimLight);
            block.SetFloat(HeatLightId, heatLight);
            block.SetFloat(FrontLightId, frontLight);
            block.SetFloat(BlazeLightId, blazeLight);
            block.SetFloat(RippleLightId, rippleLight);
            block.SetFloat(BodyGlowId, bodyGlow);
            block.SetFloat(ShimmerId, shimmer);
            decal.SetPropertyBlock(block);

            // The sun: its corona's light, flames and reach, its surface's glow and its light, all on the same clock.
            // The corona draws in with the sun (SolarFlare.Swell) over the warning's last 15%, and breathes out with it.
            float inhale = warning ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SolarFlare.InhaleFrom, 1f, w)) : 0f;
            const float DrawnInReach = .55f * (1f - .35f), DrawnInFlames = .7f * (1f - .45f);   // the warning's last values
            float light, flames, coronaReach, flameReach, flash;
            if (burning)
            {
                flash = coronaFlash * attack * Mathf.Exp(-bt * 7f);
                light = Mathf.Lerp(coronaWarn, Mathf.Lerp(coronaWarn * 1.4f, coronaWarn, b), attack);
                flames = Mathf.Lerp(1f, .6f, b);
                coronaReach = Mathf.Lerp(DrawnInReach, Mathf.Lerp(.55f, 1.6f, Mathf.Sqrt(b)), attack);
                flameReach = Mathf.Lerp(DrawnInFlames, Mathf.Lerp(.7f, 1.2f, b), attack);
            }
            else if (warning)
            {
                flash = 0f;
                light = Mathf.Lerp(coronaCalm, coronaWarn, w);
                flames = Mathf.Lerp(.15f, 1f, w);
                coronaReach = Mathf.Lerp(.3f, .55f, w) * (1f - .35f * inhale);
                flameReach = Mathf.Lerp(.22f, .7f, w) * (1f - .45f * inhale);
            }
            else
            {
                // calm, settling from the burn's end
                flash = 0f;
                light = Mathf.Lerp(coronaCalm, coronaWarn, after);
                flames = Mathf.Lerp(.15f, .6f, after);
                coronaReach = Mathf.Lerp(.3f, 1.6f, after * after);
                flameReach = Mathf.Lerp(.22f, 1.2f, after * after);
            }
            // The burst: a ring thrown out and rays shooting out from the burn's start, sparks flying out of the sun.
            float sinceBurst = burning ? bt : t < 1.5f ? t + burn : -1f;   // the burn's start, carried into the calm
            float shockK = sinceBurst >= 0f ? Mathf.Clamp01(sinceBurst / Mathf.Max(shockSeconds, .05f)) : 1f;
            float shock = shockK < 1f ? shockReach * (1f - (1f - shockK) * (1f - shockK)) : 0f;
            float shockGlow = shockK < 1f ? shockLight * Mathf.Pow(1f - shockK, 1.5f) * Mathf.SmoothStep(0f, 1f, sinceBurst / .04f) : 0f;
            float rays = sinceBurst >= 0f ? rayLight * Mathf.SmoothStep(0f, 1f, sinceBurst / .05f) * Mathf.Exp(-sinceBurst * 5f) : 0f;
            float rayLength = sinceBurst >= 0f ? Mathf.Lerp(.8f, rayReach, 1f - Mathf.Exp(-sinceBurst * 8f)) : 0f;
            if (corona != null)
            {
                var lamp = flare.Lamp;
                float radius = lamp != null ? .5f * lamp.lossyScale.x : 2.2f;   // the lamp is a unit sphere, swelling
                corona.transform.localPosition = flare.Sun;
                corona.GetPropertyBlock(block);
                block.SetFloat(LightId, light);
                block.SetFloat(FlamesId, flames);
                block.SetFloat(ReachRadiiId, coronaReach);
                block.SetFloat(FlameReachId, flameReach);
                block.SetFloat(FlashId, flash);
                block.SetFloat(ShockId, shock);
                block.SetFloat(ShockLightId, shockGlow);
                block.SetFloat(RaysId, rays);
                block.SetFloat(RayReachId, rayLength);
                block.SetFloat(SunRadiusId, radius);
                float reachOut = Mathf.Max(Mathf.Max(coronaReach * 3.2f, flameReach * 1.6f),
                                           Mathf.Max(shockGlow > .01f ? shock + .5f : 0f, rays > .02f ? rayLength * 1.6f : 0f));
                block.SetFloat(SizeId, radius * (1f + reachOut + .15f));
                block.SetFloat(AgeId, age);
                corona.SetPropertyBlock(block);
            }
            if (ejecta != null)
            {
                bool flying = sinceBurst >= 0f && sinceBurst < ejectaLife;
                if (ejecta.enabled != flying) ejecta.enabled = flying;
                if (flying) EjectaLook(sinceBurst);
            }
            // a sharp peak that falls away fast (the square root): the burst, then the burn
            float lit = burning ? Mathf.Lerp(lightWarn, Mathf.Lerp(lightBurn, lightWarn, Mathf.Sqrt(b)), attack)
                      : warning ? Mathf.Lerp(1f, lightWarn, w) : Mathf.Lerp(1f, lightWarn, after);
            float glow = burning ? Mathf.Lerp(glowWarn, Mathf.Lerp(glowBurn, glowWarn, Mathf.Sqrt(b)), attack)
                       : warning ? Mathf.Lerp(1f, glowWarn, w) : Mathf.Lerp(1f, glowWarn, after);
            glow *= 1f + .04f * Mathf.Sin(age * 2.1f);   // it never quite sits still
            if (sunLight != null)
            {
                sunLight.intensity = sunLight0 * lit;
                sunLight.color = Color.Lerp(sunColor0, new Color(1f, .94f, .82f), burning ? .6f * (1f - b) * attack : 0f);
            }
            for (int i = 0; i < sunGlow.Count; i++)
                if (sunGlow[i] != null) sunGlow[i].SetColor(EmissionId, sunGlow0[i] * glow);
        }

        // Whether this PC's pawn is caught by the burn now, by SolarFlare's own test (its FixedUpdate): once a flare.
        void CheckCaught(double now)
        {
            if (!flare.Burning(now)) return;
            long index = flare.Index(now);
            if (index == caughtFlare) return;
            var pawn = MiniGameBase.LocalDriver?.FollowTarget;
            if (pawn == null) return;
            if (pawn != caughtPawn)
            {
                caughtPawn = pawn;
                caughtCollider = pawn.GetComponentInChildren<Collider>();
            }
            var actor = caughtCollider != null ? ImportedObstacleActor.Find(caughtCollider) : null;
            if (actor != null && !actor.isActiveAndEnabled) return;
            var at = transform.InverseTransformPoint(actor != null ? actor.Position : pawn.position);
            if (new Vector2(at.x, at.z).magnitude > flare.Radius || at.y < -1f || at.y > 4f || flare.Sheltered(at)) return;
            caughtFlare = index;
            caughtAt = Time.time;
            RoyalObservatorySound.For(this)?.FlareCaught(pawn.position);   // R742: the sizzle, this player only
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Enabled || LoadingScreenView.Showing) return;
            float since = Time.time - caughtAt, length = Mathf.Max(edgeFlashSeconds, .1f);
            if (since < 0f || since > length) return;
            // In over 0.05 s, then dying away, gone at the end.
            float k = Mathf.SmoothStep(0f, 1f, since / .05f) * Mathf.Exp(-Mathf.Max(0f, since - .05f) / (length * .35f))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(length * .7f, length, since)));
            k *= edgeFlashStrength;
            var full = new Rect(0, 0, Screen.width, Screen.height);
            var old = GUI.color;
            GUI.color = new Color(1f, .55f, .16f, .65f * k);
            GUI.DrawTexture(full, EdgeTexture(ref edgeWide, .42f));
            GUI.color = new Color(edgeFlash.r, edgeFlash.g, edgeFlash.b, .95f * k);
            GUI.DrawTexture(full, EdgeTexture(ref edgeTight, .74f));
            GUI.color = old;
        }

        // Clear in the middle, full at the screen's edges (a rounded box, so all four edges flare, not only the
        // corners): stretched over the screen and tinted.
        static Texture2D EdgeTexture(ref Texture2D texture, float inner)
        {
            if (texture != null) return texture;
            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + .5f) / size * 2f - 1f), dy = Mathf.Abs((y + .5f) / size * 2f - 1f);
                    float d = Mathf.Pow(Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f), .25f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 1f, d));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void Show(bool on)
        {
            if (decal != null && decal.enabled != on) decal.enabled = on;
            if (corona != null && corona.enabled != on) corona.enabled = on;
            if (!on && ejecta != null) ejecta.enabled = false;
            if (!on) Restore();
        }

        // The sun as the dressing made it.
        void Restore()
        {
            if (sunLight != null)
            {
                sunLight.intensity = sunLight0;
                sunLight.color = sunColor0;
            }
            for (int i = 0; i < sunGlow.Count; i++)
                if (sunGlow[i] != null) sunGlow[i].SetColor(EmissionId, sunGlow0[i]);
        }

        void OnDisable()
        {
            if (ready) Show(false);
        }

        void OnDestroy()
        {
            Restore();
            if (decalMaterial != null) Destroy(decalMaterial);
            if (coronaMaterial != null) Destroy(coronaMaterial);
            if (quad != null) Destroy(quad);
            if (ejectaMaterial != null) Destroy(ejectaMaterial);
            if (ejectaMesh != null) Destroy(ejectaMesh);
            if (edgeWide != null) Destroy(edgeWide);
            if (edgeTight != null) Destroy(edgeTight);
        }
    }
}
