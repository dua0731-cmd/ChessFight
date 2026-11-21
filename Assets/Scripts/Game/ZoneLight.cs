using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ChessFight.Game
{
    /// <summary>
    /// One place's light over the scene's (R734): the sun's colour, strength, shadow darkness and (if it says so) way,
    /// the ambient light and the haze, mixed with every other place's by one mixer each frame.
    /// </summary>
    /// <remarks>
    /// R734 (the user, 11-21: "마차뜰에서 도개교로 넘어갈 때 후처리 때문에 조명이 확 바껴 자연스럽게 이어지게 처리 해주고
    /// 나머지 모든 조합들 에서도 자연스럽게"). The carriage yard (PawnRush YardAtmosphere), the Royal Library and the Royal
    /// Observatory each noted the scene's light the first frame their weight went over 0, wrote their own over it and put
    /// the noted one back at 0. Three things made the change sudden:
    ///   - the round's ambient light is the sky's (spherical harmonics); each place switched it to a three-colour gradient
    ///     made from three samples of it, so the light jumped the moment a place came in and again as it went
    ///   - the fade was short (8 to 14 m from the place's box, mostly inside the 10 m corridor) and eased over only half
    ///     a second, so a running pawn crossed the whole change in a second or so
    ///   - two places fading at once would each have noted the other's light as "the scene's" and put it back wrongly
    /// Now every place hands its look and a weight to this class; the mixer notes the scene's light once, blends the
    /// scene's and every place's by their weights (the ambient light as spherical harmonics, so weight 0 is the scene's
    /// light exactly), and puts the scene's back when no place is left. Places ease their weight with Ease (a smooth
    /// follow over about two seconds) and fade it by the camera's distance from their box with Inside.
    /// Nothing is synced: every PC fades by its own camera. Pictures only.
    /// </remarks>
    public sealed class ZoneLight
    {
        // ------------------------------------------------------------------ the look (read by the mixer every frame)

        public string Name;
        /// <summary>Turn the scene's sun to SunRotation (else only its colour, strength and shadows change).</summary>
        public bool TurnSun;
        public Quaternion SunRotation = Quaternion.identity;
        public Color SunColor = Color.white;
        public float SunIntensity = 1f, ShadowStrength = 1f;
        /// <summary>Soft shadows on the sun while this place is in.</summary>
        public bool SoftShadows;
        /// <summary>In a scene with no directional light, make one (strength 0 outside the place).</summary>
        public bool BringSun;
        /// <summary>The ambient light as a sky / horizon / ground gradient.</summary>
        public Color Sky = Color.gray, Equator = Color.gray, Ground = Color.gray;
        /// <summary>A linear haze (off: the scene's fog stays).</summary>
        public bool Fog;
        public Color FogColor = Color.gray;
        public float FogStart = 100f, FogEnd = 500f;

        /// <summary>Seconds a place's weight takes to follow where the camera is (SmoothDamp's smooth time).</summary>
        public const float EaseSeconds = .7f;
        /// <summary>The linear fog a scene with none fades from ("none": past everything, PawnRushRound.FarFog's).</summary>
        public const float NoFogStart = 3000f, NoFogEnd = 6000f;
        internal const float Off = .0005f;

        internal float weight;
        float velocity;
        bool eased, removed;
        // The ambient gradient as spherical harmonics, made again when the colours change.
        SphericalHarmonicsL2 ambient;
        Color madeSky, madeEquator, madeGround;
        bool ambientMade;

        internal static readonly List<ZoneLight> zones = new List<ZoneLight>();
        internal static readonly HashSet<Light> owned = new HashSet<Light>();

        /// <summary>A new place. Its weight starts at 0 (nothing of the scene's is touched).</summary>
        public static ZoneLight Add(string name)
        {
            var zone = new ZoneLight { Name = name };
            zones.Add(zone);
            ZoneLightMixer.Ensure();
            return zone;
        }

        /// <summary>The place is gone (its owner destroyed): the mix goes on without it.</summary>
        public void Remove()
        {
            removed = true;
            weight = 0f;
            zones.Remove(this);
        }

        /// <summary>A light the place made (a fill): never taken for the scene's sun.</summary>
        public static void Own(Light light)
        {
            if (light != null) owned.Add(light);
        }

        /// <summary>0 = nothing of the place's, 1 = all of it.</summary>
        public float Weight
        {
            get => weight;
            set => weight = removed ? 0f : Mathf.Clamp01(value);
        }

        /// <summary>
        /// While true, places jump straight to where the camera is (no easing): the round's prewarm tour, which shows one
        /// frame at each spot behind the loading screen and must find each place's light whole there.
        /// </summary>
        public static bool Instant;

        /// <summary>
        /// Eases the weight toward `target` (a smooth follow over about EaseSeconds) and returns it. A negative target (no
        /// camera yet) holds it. The first real target is taken at once, so a place the round starts in is lit from the
        /// first frame.
        /// </summary>
        public float Ease(float target)
        {
            if (target < 0f) return weight;
            target = Mathf.Clamp01(target);
            if (!eased || Instant)
            {
                eased = true;
                velocity = 0f;
                Weight = target;
                return weight;
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            float w = Mathf.SmoothDamp(weight, target, ref velocity, EaseSeconds, Mathf.Infinity, dt);
            if (Mathf.Abs(w - target) < .002f && Mathf.Abs(velocity) < .02f)
            {
                w = target;
                velocity = 0f;
            }
            Weight = w;
            return weight;
        }

        // ------------------------------------------------------------------ the camera and the place's box

        static Camera view;
        static float lookForView;

        /// <summary>The camera drawing to the screen (Camera.main, else the first game camera), looked for at most once a second.</summary>
        public static Camera View()
        {
            if (view != null && view.isActiveAndEnabled) return view;
            if (Time.unscaledTime < lookForView) return null;
            lookForView = Time.unscaledTime + 1f;
            view = Camera.main;
            if (view == null)
                foreach (var c in Camera.allCameras)
                    if (c.cameraType == CameraType.Game && c.targetTexture == null) { view = c; break; }
            return view;
        }

        /// <summary>
        /// How far in the camera is: 1 inside `area` (in `frame`'s space), 0 `blend` metres (frame units) out, a smooth
        /// step between; -1 with no camera (Ease then holds).
        /// </summary>
        public static float Inside(Transform frame, Bounds area, float blend)
        {
            var cam = View();
            if (cam == null || frame == null) return -1f;
            var p = frame.InverseTransformPoint(cam.transform.position);
            float d = Vector3.Distance(area.ClosestPoint(p), p);
            return 1f - Mathf.SmoothStep(0f, 1f, d / Mathf.Max(.5f, blend));
        }

        /// <summary>The scene's sun as the mixer drives it (null while no place is in).</summary>
        public static Light Sun => ZoneLightMixer.Sun;

        // ------------------------------------------------------------------ ambient as spherical harmonics

        static bool warnedSpread;

        /// <summary>
        /// A sky / horizon / ground gradient as exactly the ambient light Unity's own gradient mode gives it: the gradient
        /// is put into RenderSettings for a moment and the harmonics Unity makes of it read back (it makes them at once;
        /// ZoneLightCheck draws both and compares), then everything is put back. Should Unity not have made them (the probe
        /// unchanged), an own approximation (Spread) stands in.
        /// </summary>
        public static SphericalHarmonicsL2 Gradient(Color sky, Color equator, Color ground)
        {
            var mode = RenderSettings.ambientMode;
            Color sky0 = RenderSettings.ambientSkyColor, equator0 = RenderSettings.ambientEquatorColor,
                  ground0 = RenderSettings.ambientGroundColor, flat0 = RenderSettings.ambientLight;
            var probe0 = RenderSettings.ambientProbe;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            var made = RenderSettings.ambientProbe;
            RenderSettings.ambientMode = mode;
            RenderSettings.ambientSkyColor = sky0;
            RenderSettings.ambientEquatorColor = equator0;
            RenderSettings.ambientGroundColor = ground0;
            RenderSettings.ambientLight = flat0;
            RenderSettings.ambientProbe = probe0;
            bool sameColours = mode == AmbientMode.Trilight && sky0 == sky && equator0 == equator && ground0 == ground;
            if (made == probe0 && !sameColours)
            {
                if (!warnedSpread) Debug.LogWarning("[ChessFight] 구간 조명: Unity가 그라디언트 주변광을 바로 만들지 않아 근사값을 씁니다.");
                warnedSpread = true;
                return Spread(sky, equator, ground);
            }
            return made;
        }

        // Directions spread evenly over the sphere (a Fibonacci spiral), for Spread.
        static Vector3[] spread;
        static float unitScale;
        static bool calibrated;

        /// <summary>
        /// The stand-in: the gradient taken as the light coming from every direction (sky straight up, horizon level,
        /// ground straight down, blended by height), summed as many small directional lights and scaled so one colour all
        /// round gives exactly that colour. Softer than Unity's own (ZoneLightCheck reports both).
        /// </summary>
        public static SphericalHarmonicsL2 Spread(Color sky, Color equator, Color ground)
        {
            Calibrate();
            var sh = new SphericalHarmonicsL2();
            sh.Clear();
            foreach (var d in spread)
            {
                var c = d.y >= 0f ? Color.Lerp(equator, sky, d.y) : Color.Lerp(equator, ground, -d.y);
                sh.AddDirectionalLight(d, c, unitScale);
            }
            return sh;
        }

        static void Calibrate()
        {
            if (calibrated) return;
            calibrated = true;
            const int n = 384;
            spread = new Vector3[n];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < n; i++)
            {
                float y = 1f - (i + .5f) * 2f / n, r = Mathf.Sqrt(1f - y * y), a = golden * i;
                spread[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
            // AddDirectionalLight's own scale and which way it takes the direction are Unity's: measure them. One light
            // from straight up must light an upward surface more than a downward one (else the directions are flipped);
            // white from every direction must come out white.
            var one = new SphericalHarmonicsL2();
            one.Clear();
            one.AddDirectionalLight(Vector3.up, Color.white, 1f);
            var probe = new[] { Vector3.up, Vector3.down };
            var got = new Color[2];
            one.Evaluate(probe, got);
            if (got[0].r < got[1].r)
                for (int i = 0; i < n; i++) spread[i] = -spread[i];
            var all = new SphericalHarmonicsL2();
            all.Clear();
            foreach (var d in spread) all.AddDirectionalLight(d, Color.white, 1f);
            all.Evaluate(probe, got);
            float level = (got[0].r + got[1].r) * .5f;
            unitScale = level > 1e-6f ? 1f / level : 1f;
        }

        internal SphericalHarmonicsL2 AmbientSH()
        {
            if (!ambientMade || Sky != madeSky || Equator != madeEquator || Ground != madeGround)
            {
                ambient = Gradient(Sky, Equator, Ground);
                madeSky = Sky;
                madeEquator = Equator;
                madeGround = Ground;
                ambientMade = true;
            }
            return ambient;
        }

    }
}
