using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // R552 (승규 "원형 창문에서 회전을 하며 빛이 수학적으로 계산이 되며 빛은 인트로 씬에 있는 이런 노을 빛이
    // 실시간으로 렌더 되게 해줘"): the kingdom hall at sunset, lit like the intro (IntroStage.cs "Sunset Light").
    // A low warm sun stands behind the stair end; its real-time shadows let it in through the round window and over
    // the walls. The intro's sunset sky is laid round the hall (HallSunsetSky.shader). The window's tracery turns
    // slowly (Rose_Tracery, whose origin is the window's centre). The light's shafts in the air are ray-marched every
    // frame (RoseShafts.shader) from the same sun, the same turn and the sun's shadow map.
    // R563 (승규 "회전 창문이 좀 밋밋해 빛 그래픽도 별로고", then "플레이어가 압도 할만한 느낌"): the great rose (6.9 m
    // across, Kit/stair_end.py), gothic tracery and stained glass (Rose_Glass_*, four colours) that glows and turns
    // with it. The glass casts no shadow, so the sun comes through it; the shafts read the window's coloured pattern
    // (RosePattern.png, Kit/rose_pattern.py), drift with dust and lay the window's colours on the floor and stage.
    public sealed partial class LobbyStage
    {
        // Where the sun stands: degrees from +Z (the stair end) towards +X, and above the horizon. R563: at 27 degrees
        // the beam through the great rose's centre (7.8 m up) comes down on the stage.
        const float HallSunAzimuth = 6f, HallSunElevation = 27f;
        // degrees a second. R619: 0, the armillary sphere in front of it turns instead (LobbyStage.Hall.Armillary.cs); was 8
        const float RoseTurnSpeed = 0f;
        const float RosePatternHalf = 2.4984f, RosePatternPixels = 1024f; // RosePattern.png: +-ROSE_HOLE m (rose_pattern.py); R590: 3.47 x 0.72
        const float SunAngularRadius = .00465f;                           // radians: the sun's disc
        const float ShaftLength = 22f, ShaftWidth = 6.6f;                  // the box the shafts are marched in
        const float ShaftDensity = .03f;                                  // AI Play capture: .012 too faint, .035 strong
        const float ShaftSurface = 1f;                                    // the window's colours laid on the floor
        // R620 (승규 picked "B 꿀빛 샴페인" of five light colours captured in Play): the purple-pink light (glass tint
        // .75, the intro's orange sun 0xFF9C58) fought the gold armillary, mocha stone and red carpets. A light honey sun
        // and only a little of the glass's colour in the light; the panes themselves still glow in their colours.
        const int HallSunColor = 0xFFC98A;
        // RoseShafts _Tint (shader default .75). R621: .15 -> .45 now the glass (and RosePattern.png) is warm, so the
        // floor shows the window's honey and amber panes
        const float ShaftGlassTint = .45f;
        // R563: the panes (mesh, glass colour as in rose_pattern.py, glow)
        static readonly (string mesh, int color, float glow)[] RosePanes =
        {
            // R621 (승규 "뒤에 색도 바꿔주고"): warm honey and amber (were 0xFFC24A 1.6, 0x3E6BFF 2.2, 0xE0352B 2.0,
            // 0xFF8A2A 1.7: the bloom washed them white-pink); same colours as Kit/rose_pattern.py GLASS
            ("Rose_Glass_Gold", 0xF2B04A, 1.0f),
            ("Rose_Glass_Blue", 0xF7CF8A, .9f),
            ("Rose_Glass_Red", 0xD96A2B, 1.1f),
            ("Rose_Glass_Amber", 0xE8902F, 1.0f),
        };

        Transform rose;
        Quaternion roseBase;
        Vector3 roseAxis;
        Vector4 roseShape;
        Material hallSky, roseShafts;
        readonly System.Collections.Generic.List<Material> roseGlass = new System.Collections.Generic.List<Material>();

        // The way the sunlight travels (from the sun into the hall).
        static Vector3 HallSunTravel()
        {
            float a = HallSunAzimuth * Mathf.Deg2Rad, e = HallSunElevation * Mathf.Deg2Rad;
            return -new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
        }

        void HallSunset(Transform root)
        {
            var travel = HallSunTravel();
            var sunColor = MenuArt.Hex(HallSunColor);
            var sun = new GameObject("Hall Sunset").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.transform.rotation = Quaternion.LookRotation(travel);
            sun.type = LightType.Directional;
            sun.color = sunColor;
            sun.intensity = 3.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            RenderSettings.sun = sun;

            // The intro's fills: the stair end faces the camera with the sun behind it, so a soft front light
            // shows its shapes, and the dusk sky comes down through the glass vault.
            HallFill(root, "Hall Front", 0xFFF4E6, .4f, new Vector3(-.2f, -.45f, .87f));
            HallFill(root, "Hall Dusk", 0xC9B8FF, .25f, new Vector3(.1f, -1f, .15f));
            // R587 (승규 "벽", reference_side_windows.webp: warm cream walls): the side walls face across the hall, so
            // neither the sun behind the stairs nor the front fill reached them and they read dark brown. A warm fill
            // from each side, nearly level (a steeper one lit a bright spot in the polished floor).
            for (int s = -1; s <= 1; s += 2)
                HallFill(root, "Hall Side", 0xFFD6A8, .8f, new Vector3(-s, -.1f, .15f));
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = MenuArt.Hex(0x9A7A5A) * .75f;
            RenderSettings.ambientEquatorColor = MenuArt.Hex(0x5A4030) * .75f;
            RenderSettings.ambientGroundColor = MenuArt.Hex(0x1E140C);

            var skyShader = Resources.Load<Shader>("Lobby/KingdomHall/HallSunsetSky");
            var image = Resources.Load<Texture2D>("Intro/SunsetSky");
            if (skyShader != null && image != null)
            {
                hallSky = new Material(skyShader) { name = "Hall sunset sky" };
                hallSky.SetTexture("_MainTex", image);
                hallSky.SetVector("_SunAngles", new Vector4(HallSunAzimuth, HallSunElevation, 0f, 0f));
                RenderSettings.skybox = hallSky;
                view.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = MenuArt.Hex(0xE8A070);
            }

            rose = castle.transform.Find("Rose_Tracery");
            if (rose == null) return;
            roseBase = rose.rotation;
            roseAxis = root.forward;   // the window looks down the hall: it turns about the hall's length
            roseShape = new Vector4(RosePatternHalf, RosePatternPixels, 0f, SunAngularRadius);
            RoseGlass();
            var shaftShader = Resources.Load<Shader>("Lobby/KingdomHall/RoseShafts");
            var pattern = Resources.Load<Texture2D>("Lobby/KingdomHall/RosePattern");
            if (shaftShader == null || pattern == null) return;
            roseShafts = new Material(shaftShader) { name = "Rose window shafts" };
            roseShafts.SetVector("_SunColor", (Vector4)sunColor.linear);
            roseShafts.SetFloat("_Density", ShaftDensity);
            roseShafts.SetFloat("_Tint", ShaftGlassTint);
            roseShafts.SetFloat("_Surface", ShaftSurface);
            roseShafts.SetTexture("_RosePattern", pattern);
            roseShafts.SetVector("_WinCentre", rose.position);
            roseShafts.SetVector("_WinRight", root.right);
            roseShafts.SetVector("_WinUp", root.up);
            roseShafts.SetVector("_WinNormal", -root.forward);
            roseShafts.SetVector("_SunDir", travel);
            roseShafts.SetVector("_WinShape", roseShape);

            // From just outside the window down along the light to the floor (17 m away along it).
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Rose Window Shafts";
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(root, false);
            box.transform.SetPositionAndRotation(rose.position + travel * (ShaftLength / 2f - 1f), Quaternion.LookRotation(travel, Vector3.up));
            box.transform.localScale = new Vector3(ShaftWidth, ShaftWidth, ShaftLength);
            var r = box.GetComponent<MeshRenderer>();
            r.sharedMaterial = roseShafts;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // R563: the stained glass glows in its colours, turns with the tracery (same origin, the window's centre) and
        // lets the sun through (no shadow), so the sun's shadow map only holds the stone of the tracery.
        void RoseGlass()
        {
            foreach (var pane in RosePanes)
            {
                var glass = castle.transform.Find(pane.mesh);
                if (glass == null || !glass.TryGetComponent<Renderer>(out var r)) continue;
                glass.SetParent(rose, true);
                var m = r.material;
                roseGlass.Add(m);
                var c = MenuArt.Hex(pane.color);
                m.SetColor("_BaseColor", c * .25f);
                m.SetFloat("_Smoothness", .85f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * pane.glow);
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // R584 (승규 "이제 거기서 은은한 노을 빛이 약하게 늘어 올 수 있게 해"): a faint low sunset through the side
        // windows. The sun stands a little towards +X, so the +X wall's eight windows (measured on Window_Glass_*,
        // Kit/stair_end.py side_walls: x 9.586, z 1.89..6.77 m, 1.38 m wide) each get a weak warm spot outside them,
        // low and leaning down the hall away from the stair end. Its soft shadows keep it to the opening and lay
        // the leading's diamonds in long stretched patches across the floor towards the middle of the hall.
        static readonly float[] HallSideWindows = { -21.80f, -18.55f, -13.62f, -10.37f, -5.45f, -2.19f, 2.73f, 5.99f };
        const float SideWindowX = 9.586f, SideWindowY = 4.33f;
        static readonly Vector3 WindowSunTravel = new Vector3(-.85f, -.42f, -.32f);   // into the hall, down, away from the stairs
        const int WindowSunColor = 0xFFA860;     // R621 (승규 "창문 이 마름모에서 바닥에 그려지는 노을 빛도 그려줘"): was 0xFFB27A
        const float WindowSunIntensity = 180f;   // R621: 70 -> 180, the diamonds clear on the floor. R585 (승규 "창문에 들어오는 빛이 약해 더 세게"): was 28 (the sun is 3.6)
        const float WindowSunBack = 3f, WindowSunRange = 20f, WindowSunAngle = 70f;

        void HallWindowSun(Transform root)
        {
            var travel = WindowSunTravel.normalized;
            foreach (float z in HallSideWindows)
            {
                var spot = new GameObject("Window Sunset").AddComponent<Light>();
                spot.transform.SetParent(root, false);
                spot.transform.localPosition = new Vector3(SideWindowX, SideWindowY, z) - travel * WindowSunBack;
                spot.transform.localRotation = Quaternion.LookRotation(travel);
                spot.type = LightType.Spot;
                spot.color = MenuArt.Hex(WindowSunColor);
                spot.intensity = WindowSunIntensity;
                spot.range = WindowSunRange;
                spot.spotAngle = WindowSunAngle;
                spot.innerSpotAngle = WindowSunAngle * .6f;
                spot.shadows = LightShadows.Soft;
                spot.shadowStrength = 1f;        // R621: crisp diamonds (was .9)
                spot.shadowNearPlane = .2f;
            }
        }

        void HallFill(Transform root, string name, int color, float intensity, Vector3 travel)
        {
            var fill = new GameObject(name).AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.transform.rotation = Quaternion.LookRotation(travel);
            fill.type = LightType.Directional;
            fill.color = MenuArt.Hex(color);
            fill.intensity = intensity;
            fill.shadows = LightShadows.None;
        }

        // The tracery turns; the shafts are told the same turn so the light in the air matches its shadows.
        void TickHall(float t)
        {
            TickArmillary(t);
            if (rose == null) return;
            float turn = Mathf.Repeat(t * RoseTurnSpeed, 360f);
            rose.rotation = Quaternion.AngleAxis(turn, roseAxis) * roseBase;
            if (roseShafts == null) return;
            roseShape.z = turn * Mathf.Deg2Rad;
            roseShafts.SetVector("_WinShape", roseShape);
        }

        void DisposeHall()
        {
            if (hallSky != null) Destroy(hallSky);
            if (roseShafts != null) Destroy(roseShafts);
            if (lanternGlassMaterial != null) Destroy(lanternGlassMaterial);
            foreach (var m in roseGlass) if (m != null) Destroy(m);
            roseGlass.Clear();
            DisposeHallPaint();
        }
    }
}
