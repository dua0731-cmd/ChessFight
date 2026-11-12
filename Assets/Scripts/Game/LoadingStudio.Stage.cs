using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if CF_URP
using UnityEngine.Rendering.Universal;
#endif

namespace ChessFight.Game
{
    // R608 (11-12, 승규 님: mockup B "대형 LED 무대", "LED 전광판에 원 안에 폰 러쉬 라고 해줘", "너가 할 수 있는 가장 좋은 연출 효과
    // 그래픽을 내주고 유니티에 적용 시켜줘"): the matching screen's background is a game show's stage instead of the study.
    //
    // What stands on it:
    //   - A polished black deck with the wooden chessboard let into it (the players walk onto the board as before), a
    //     line of warm light round the board. The deck and the board mirror the stage (a second camera under the floor,
    //     as on the title screen).
    //   - Behind, a wall of LEDs (Resources/Loading/Stage/StageLedWall.shader) showing chess squares, and in its middle
    //     a ring: a neon tube, a row of bulbs stepping round it and a thin outer tube. Inside the ring the wall's lamps
    //     are finer and spell the mode's name (SetTitle draws it from the display font).
    //   - A truss tower each side with three moving heads (white's side blue, black's red) and four more on a rig
    //     overhead, out of sight: beams through the haze, a flare when a lamp turns towards the camera.
    //   - Real spotlights for the pools on the board (a warm key with shadows, a blue and a red pool, the wall's violet
    //     from behind, a little fill), dust in the air, low haze in front of the wall.
    //   - Its own grading (ACES, bloom, a slight vignette) on a layer of its own, so no scene's look reaches it.
    //
    // What it does:
    //   - Idle: the squares breathe, rings and rays leave the ring, the bulbs step, the beams wander over the board.
    //   - A player lands on their spot (StageJoin): their team's light runs across the wall from its side, that side's
    //     lamps snap to the player for a second, the ring, its bulbs and the name flash.
    //   - Everyone is in and ready (Cheer): gold runs over the wall, the bulbs run twice as fast, the beams turn gold
    //     and sweep, foil falls.
    //   - Pawn Rush's course reveal covers the stage (House): the lights go down to a tenth.
    // Every frame is worked out from the time handed to Animate, so a still of any moment can be drawn in the editor.
    public sealed partial class LoadingStudio
    {
        // The stage's own numbers, in its metres: x to the right, y up, z away from the camera.
        const float DeckTop = .478f, WallZ = 12.6f, WallWide = 46f, WallTall = 14f;
        const float RingRadius = 3.1f, RingHeight = 4.9f;
        const float WallPitch = .1f, ScreenPitch = .05f, WallSquare = 2.3f;
        const float TowerX = 11.6f, TowerZ = 8.4f, TowerTall = 9.5f;
        const float LensRadius = .13f, BoardHalf = (8 * 1.15f + .7f) / 2f;
        // How bright things are before the grading.
        const float WallGain = 1.25f, TitleGain = 2.3f, BeamStrength = .42f;
        // A lamp stays on a player who has just come in this long, then lets go this long.
        const float SpotHold = 1f, SpotLet = .5f;
        // "Water" and "TransparentFX" in the tag manager, used by nothing else: what lies on the floor (the mirror
        // camera does not see it, as on the title screen), and the stage's own grading volume.
        const int FloorLayer = 4, LookLayer = 1;
        const string StageShaders = "Loading/Stage/";
        static readonly int StageTimeId = Shader.PropertyToID("_CfStageTime");

        // The LED stage is what every mode's matching screen shows; false brings the study back. R609 (11-12, 승규 님: "그냥
        // 옛날 버전으로 돌려 놔"): off again, the study is back; the stage stays in the code.
        bool ledStage = false;
        // 0: the show's lights. 1: the house goes dark (Pawn Rush's course reveal covers the stage).
        public float House { get; set; }

        sealed class Head
        {
            public int Side, Index;   // -1 white's tower, +1 black's, 0 the rig overhead
            public Vector3 At, Aim, SpotAt;
            public float Length, Radius, Power, Kick, SpotFrom = -100f;
            public int Rgb;
            public Transform Body, Beam, Flare, Streak;
            public Material BeamLook, Lens, FlareLook, StreakLook;
        }

        readonly List<Head> heads = new List<Head>();
        readonly List<Material> stageMaterials = new List<Material>();
        readonly List<Mesh> stageMeshes = new List<Mesh>();
        readonly Vector4[] pulses = new Vector4[4];
        readonly Light[] teamLight = new Light[2];
        readonly float[] teamBase = new float[2];
        int pulseNext;
        Material wall, neon, neonThin, marquee, boardStrip, wallFoot, dust, glitter, haze, hazeNear, deckGloss, boardGloss, trussMetal;
        Renderer glitterRenderer;
        Mesh beamTube;
        Camera mirror;
        RenderTexture mirrorTexture, titleTexture;
        Light key, wallLight, fill;
        float keyBase, wallBase, fillBase;
        Object lookProfile;
        string stageTitle;
        bool stageRan, cheering;
        float stageBase, stageNow, stageT, stageDt, cheerAt = -100f, cheerLevel, houseLevel, rayPhase, runPhase, bulbStep;

        static Vector4 V4(Color linear, float k, float a = 1f) => new Vector4(linear.r * k, linear.g * k, linear.b * k, a);
        static Vector4 Lin(int rgb, float k = 1f, float a = 1f) => V4(MenuArt.Hex(rgb).linear, k, a);

        Material StageMaterial(string shader, string name)
        {
            var found = Resources.Load<Shader>(StageShaders + shader);
            if (found == null) Debug.LogWarning("[ChessFight] 무대 셰이더(Resources/" + StageShaders + shader + ")를 찾지 못했습니다.");
            var material = new Material(found != null ? found : Shader.Find("Sprites/Default")) { name = "Stage " + name };
            stageMaterials.Add(material);
            return material;
        }

        // On the floor: the mirror camera does not draw it, and it casts no shadow.
        static GameObject Lay(GameObject go)
        {
            go.layer = FloorLayer;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ---------- building ----------

        void Stage(Transform t, int width, int height)
        {
            camera.allowHDR = true;
            camera.farClipPlane = 150f;
            camera.backgroundColor = Color.black;
            StageLook();

            // the deck: polished black, wide enough that the players walk in on it from out of sight
            Lay(MenuArt.Slab(t, "Stage Deck", new Vector3(0, DeckTop - .3f, 2f), new Vector3(64f, .6f, 34f), LastSceneArt.Lit(0x0B0B10, .88f)))
                .GetComponent<Renderer>().receiveShadows = true;
            // the chessboard the players walk onto, as in the study, let into the deck
            MenuArt.WoodBoard(t, "Board", 8, 1.15f, Vector3.zero, StageHeight - .02f, .7f);
            var board = t.Find("Board");
            if (board != null) foreach (Transform part in board) part.gameObject.layer = FloorLayer;
            // a line of warm light round the board
            boardStrip = StageMaterial("StageLamp", "board strip");
            float edge = BoardHalf + .05f;
            for (int s = -1; s <= 1; s += 2)
            {
                Lay(MenuArt.Slab(t, "Board Strip", new Vector3(0, DeckTop + .012f, s * edge), new Vector3(edge * 2f + .06f, .024f, .06f), boardStrip));
                Lay(MenuArt.Slab(t, "Board Strip", new Vector3(s * edge, DeckTop + .012f, 0), new Vector3(.06f, .024f, edge * 2f + .06f), boardStrip));
            }
            StageGloss(t, width, height);

            // the LED wall, and a strip of cold light where it meets the deck
            wall = StageMaterial("StageLedWall", "LED wall");
            float wallMid = DeckTop + WallTall / 2f, ringY = RingHeight - wallMid;
            LastSceneArt.Part(t, "LED Wall", LastSceneArt.Panel(WallWide, WallTall), wall, new Vector3(0, wallMid, WallZ), null, null, false);
            wall.SetVector("_Ring", new Vector4(0, ringY, RingRadius, 0));
            wall.SetVector("_Pitch", new Vector4(WallPitch, ScreenPitch, WallSquare, 0));
            wall.SetVector("_Extent", new Vector4(WallWide / 2f, -WallTall / 2f - ringY, WallTall / 2f - ringY, 0));
            wall.SetVector("_Title", Vector4.zero);
            wall.SetVector("_ColDeep", Lin(0x0D0930));
            wall.SetVector("_ColMid", Lin(0x4A3AD0));
            wall.SetVector("_ColLine", Lin(0x94A4FF));
            wall.SetVector("_ColWhite", Lin(0x2EA8FF));
            wall.SetVector("_ColBlack", Lin(0xFF3355));
            wall.SetVector("_ColGold", Lin(0xFFC24A));
            wall.SetVector("_ColText", Lin(0xFFF2D4));
            wall.SetVector("_ColScreen", Lin(0x070618));
            wall.SetVectorArray("_Pulse", pulses);
            // the picture drawn for the wall, if there is one (a chess scene made with Higgsfield, Resources/Loading/Stage/
            // WallArt): it stands on the deck, 32 m wide, in place of the squares; without it the squares show
            var art = Resources.Load<Texture2D>(StageShaders + "WallArt");
            if (art != null)
            {
                const float artHalf = 16f;
                wall.SetTexture("_ArtTex", art);
                wall.SetVector("_Art", new Vector4(artHalf, -WallTall / 2f - ringY, 2f * artHalf * art.height / Mathf.Max(1, art.width), 1f));
            }
            wallFoot = StageMaterial("StageLamp", "wall foot");
            Lay(MenuArt.Slab(t, "Wall Foot", new Vector3(0, DeckTop + .04f, WallZ - .08f), new Vector3(WallWide, .08f, .06f), wallFoot));

            // the ring on the wall: the neon tube round the round screen, the bulbs, a thin tube outside them
            var ring = LastSceneArt.Group(t, "Ring", new Vector3(0, RingHeight, WallZ - .16f)).transform;
            var upright = Quaternion.Euler(90f, 0, 0);   // MenuArt.Torus lies flat
            neon = StageMaterial("StageLamp", "ring neon");
            LastSceneArt.Part(ring, "Neon", MenuArt.Torus(RingRadius, .1f, 128, 12), neon, Vector3.zero, upright, null, false);
            neonThin = StageMaterial("StageLamp", "ring neon thin");
            LastSceneArt.Part(ring, "Neon Thin", MenuArt.Torus(RingRadius + .68f, .04f, 128, 8), neonThin, Vector3.zero, upright, null, false);
            marquee = StageMaterial("StageMarquee", "ring bulbs");
            LastSceneArt.Part(ring, "Bulbs", Band(RingRadius + .2f, RingRadius + .56f, 160), marquee, new Vector3(0, 0, .03f), null, null, false);
            marquee.SetVector("_Bulbs", new Vector4(60, RingRadius + .38f, .09f, 0));
            marquee.SetVector("_ColOn", Lin(0xFFD27A, 3.4f));
            marquee.SetVector("_ColOff", Lin(0x6A4418, .55f));
            marquee.SetVector("_ColPlate", Lin(0x0B0B10));

            // the towers and their moving heads; the rig overhead is out of sight, only its beams come down
            trussMetal = LastSceneArt.Lit(0x1B1D24, .55f, .8f);
            beamTube = Tube();
            for (int side = -1; side <= 1; side += 2) Tower(t, side);
            float[] over = { -8.5f, -5f, 5f, 8.5f };
            for (int i = 0; i < over.Length; i++)
                AddHead(t, 0, i, new Vector3(over[i], 11.5f, Mathf.Abs(over[i]) > 6f ? 3.6f : 2.2f), 15f, 1.25f, 0xFFE9CC, .8f);

            // the light that falls on the board and the players
            key = MenuArt.Spot(t, new Vector3(0, 11f, -2.5f), new Vector3(0, StageHeight, .4f), 0xFFE7C4, 3f, .62f, .75f, true);
            teamLight[0] = MenuArt.Spot(t, new Vector3(-10.5f, 7.5f, 3f), new Vector3(-2.3f, StageHeight, 0f), 0x3FA8FF, 2.4f, .36f, .9f);
            teamLight[1] = MenuArt.Spot(t, new Vector3(10.5f, 7.5f, 3f), new Vector3(2.3f, StageHeight, 0f), 0xFF4058, 2.4f, .36f, .9f);
            wallLight = MenuArt.Spot(t, new Vector3(0, 6.5f, WallZ - 1.2f), new Vector3(0, StageHeight, -3f), 0x8A78FF, 1.6f, 1.05f, .9f);
            fill = MenuArt.Spot(t, new Vector3(0, 4f, -15f), new Vector3(0, 1.2f, 0f), 0xFFDCC0, .55f, .6f, .9f);
            keyBase = key.intensity; wallBase = wallLight.intensity; fillBase = fill.intensity;
            teamBase[0] = teamLight[0].intensity; teamBase[1] = teamLight[1].intensity;

            // the air: dust in the lights, foil for when everyone is in, low haze in front of the wall and behind the board
            dust = StageMaterial("StageDust", "dust");
            Air(t, "Dust", Motes(340, 7, false), dust, new Vector3(-13f, .7f, -7f), new Vector3(26f, 8.4f, 18f));
            dust.SetVector("_Mote", new Vector4(.045f, 1f, .8f, 0));
            dust.SetVector("_Fall", Vector4.zero);
            glitter = StageMaterial("StageDust", "glitter");
            glitterRenderer = Air(t, "Glitter", Motes(520, 19, true), glitter, new Vector3(-11f, .6f, -6f), new Vector3(22f, 9.4f, 14f));
            glitter.SetVector("_Mote", new Vector4(.06f, 0, 0, 0));
            glitter.SetVector("_Color", new Vector4(1.6f, 1.6f, 1.6f, 1));
            glitterRenderer.enabled = false;
            haze = StageMaterial("StageHaze", "haze");
            LastSceneArt.Part(t, "Haze", LastSceneArt.Panel(44f, 4.4f), haze, new Vector3(0, DeckTop + 2.2f, WallZ - 1.6f), null, null, false);
            haze.SetVector("_Drift", new Vector4(.22f, .35f, .75f, 0));
            hazeNear = StageMaterial("StageHaze", "haze near");
            LastSceneArt.Part(t, "Haze", LastSceneArt.Panel(40f, 2.6f), hazeNear, new Vector3(0, DeckTop + 1.3f, 6.4f), null, null, false);
            hazeNear.SetVector("_Drift", new Vector4(.3f, -.25f, .8f, 0));
        }

        Renderer Air(Transform t, string name, Mesh mesh, Material material, Vector3 min, Vector3 size)
        {
            mesh.bounds = new Bounds(min + size / 2f, size + Vector3.one * 2f);
            material.SetVector("_BoxMin", Origin + min);
            material.SetVector("_BoxSize", size);
            return LastSceneArt.Part(t, name, mesh, material, Vector3.zero, null, null, false).GetComponent<Renderer>();
        }

        void Tower(Transform t, int side)
        {
            float x = side * TowerX;
            LastSceneArt.Part(t, "Truss Tower", Truss(TowerTall, .56f, .56f), trussMetal, new Vector3(x, DeckTop, TowerZ), null, null, false);
            MenuArt.Slab(t, "Truss Base", new Vector3(x, DeckTop + .04f, TowerZ), new Vector3(1.1f, .08f, 1.1f), trussMetal);
            for (int i = 0; i < 3; i++)
            {
                var at = new Vector3(x - side * .62f, 2.5f + i * 1.75f, TowerZ - .1f);
                // the arm that holds the lamp out from the tower
                MenuArt.Slab(t, "Lamp Arm", at + new Vector3(side * .34f, 0, 0), new Vector3(.46f, .07f, .07f), trussMetal);
                AddHead(t, side, i, at, 20f, 1.7f, side < 0 ? 0x54B6FF : 0xFF4D66, 1f);
            }
        }

        void AddHead(Transform t, int side, int index, Vector3 at, float length, float radius, int rgb, float power)
        {
            var h = new Head { Side = side, Index = index, At = at, Length = length, Radius = radius, Rgb = rgb, Power = power };
            h.Body = LastSceneArt.Group(t, "Moving Head", at).transform;
            var along = Quaternion.Euler(90f, 0, 0);   // the helpers' cylinder and disc stand along y; the lamp looks along z
            LastSceneArt.Part(h.Body, "Can", LastSceneArt.Cylinder(.16f, .2f, .5f, 16), trussMetal, new Vector3(0, 0, -.12f), along, null, false);
            h.Lens = StageMaterial("StageLamp", "lens");
            LastSceneArt.Part(h.Body, "Lens", MenuArt.Disc(LensRadius + .04f, 32), h.Lens, new Vector3(0, 0, .135f), along, null, false);
            h.BeamLook = StageMaterial("StageBeam", "beam");
            h.BeamLook.SetVector("_Shape", new Vector4(LensRadius, radius, length, 0));
            h.BeamLook.SetVector("_Soft", new Vector4(1.7f, 1.35f, .55f, .55f));
            h.Beam = LastSceneArt.Part(t, "Beam", beamTube, h.BeamLook, at, null, null, false).transform;
            h.FlareLook = StageMaterial("StageFlare", "flare");
            h.FlareLook.mainTexture = MenuArt.RadialTexture();
            h.Flare = LastSceneArt.Part(t, "Flare", LastSceneArt.Panel(1f, 1f), h.FlareLook, at, null, null, false).transform;
            h.StreakLook = StageMaterial("StageFlare", "streak");
            h.StreakLook.mainTexture = MenuArt.RadialTexture();
            h.Streak = LastSceneArt.Part(t, "Streak", LastSceneArt.Panel(1f, 1f), h.StreakLook, at, null, null, false).transform;
            heads.Add(h);
        }

        // The mirror under the floor and the two sheets that lay its picture on the deck and on the board.
        void StageGloss(Transform t, int width, int height)
        {
            if (Resources.Load<Shader>(StageShaders + "StageFloor") == null) return;
            mirrorTexture = new RenderTexture(Mathf.Max(64, width / 2), Mathf.Max(64, height / 2), 16, RenderTextureFormat.ARGBHalf)
                { name = "Loading Stage mirror", wrapMode = TextureWrapMode.Clamp };
            mirrorTexture.Create();
            var go = new GameObject("Stage Mirror");
            go.transform.SetParent(t, false);
            mirror = go.AddComponent<Camera>();
            mirror.enabled = false;
            mirror.clearFlags = CameraClearFlags.SolidColor;
            mirror.backgroundColor = Color.black;
            mirror.cullingMask = ~((1 << FloorLayer) | (1 << 5) | (1 << LookLayer));
            mirror.allowHDR = true;
            mirror.allowMSAA = false;
            mirror.targetTexture = mirrorTexture;
#if CF_URP
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
#endif
            deckGloss = StageMaterial("StageFloor", "deck gloss");
            deckGloss.SetTexture("_ReflTex", mirrorTexture);
            deckGloss.SetVector("_Gloss", new Vector4(.14f, .8f, .0032f, .5f));
            deckGloss.SetVector("_Tint", new Vector4(1f, 1f, 1f, 1f));
            // w 1: the mirror's picture is read as it is (the camera draws into a texture, where the screen's v already runs the
            // other way; checked on stills: the pawns' reflections meet their feet)
            deckGloss.SetVector("_Hole", new Vector4(Origin.x, Origin.z, BoardHalf, 1));
            Lay(LastSceneArt.Part(t, "Deck Gloss", LastSceneArt.Panel(64f, 34f), deckGloss, new Vector3(0, DeckTop + .004f, 2f), Quaternion.Euler(90f, 0, 0), null, false));
            boardGloss = StageMaterial("StageFloor", "board gloss");
            boardGloss.SetTexture("_ReflTex", mirrorTexture);
            boardGloss.SetVector("_Gloss", new Vector4(.045f, .4f, .006f, .35f));
            boardGloss.SetVector("_Tint", new Vector4(1f, .93f, .82f, 1f));
            boardGloss.SetVector("_Hole", new Vector4(0, 0, 0, 1));
            Lay(LastSceneArt.Part(t, "Board Gloss", LastSceneArt.Panel(8 * 1.15f, 8 * 1.15f), boardGloss, new Vector3(0, StageHeight + .004f, 0), Quaternion.Euler(90f, 0, 0), null, false));
        }

        // The stage's own grading: nothing of the scene's look (its volumes sit on the default layer) reaches it, and
        // the project's default profile is overridden whole.
        void StageLook()
        {
#if CF_URP
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.volumeLayerMask = 1 << LookLayer;
            data.dithering = true;
            var go = new GameObject("Stage Look") { layer = LookLayer };
            go.transform.SetParent(root.transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Stage Look";
            var tone = profile.Add<Tonemapping>(false);
            tone.mode.Override(TonemappingMode.ACES);
            var grade = profile.Add<ColorAdjustments>(false);
            grade.postExposure.Override(.25f);
            grade.contrast.Override(8f);
            grade.colorFilter.Override(Color.white);
            grade.hueShift.Override(0f);
            grade.saturation.Override(6f);
            var bloom = profile.Add<Bloom>(false);
            bloom.threshold.Override(.9f);
            bloom.intensity.Override(.8f);
            bloom.scatter.Override(.72f);
            bloom.tint.Override(Color.white);
            bloom.highQualityFiltering.Override(true);
            bloom.dirtIntensity.Override(0f);
            var vignette = profile.Add<Vignette>(false);
            vignette.color.Override(Color.black);
            vignette.center.Override(new Vector2(.5f, .5f));
            vignette.intensity.Override(.2f);
            vignette.smoothness.Override(.45f);
            vignette.rounded.Override(false);
            volume.sharedProfile = profile;
            lookProfile = profile;
#endif
        }

        // ---------- the mode's name on the round screen ----------

        // Draws `text` in the display font into a texture and has the wall's fine lamps inside the ring show it.
        public void SetTitle(string text)
        {
            text = text ?? "";
            if (wall == null || text == stageTitle) return;
            stageTitle = text;
            var font = Resources.Load<Font>("Fonts/BlackHanSans-Regular");
            var shader = Resources.Load<Shader>(StageShaders + "StageText");
            if (font == null || shader == null || text.Trim().Length == 0)
            {
                wall.SetVector("_Title", Vector4.zero);
                return;
            }
            const int size = 200, pad = 14, tall = 256;
            font.RequestCharactersInTexture(text, size, FontStyle.Normal);
            var glyphs = new CharacterInfo[text.Length];
            float pen = 0f, left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
            for (int i = 0; i < text.Length; i++)
            {
                if (!font.GetCharacterInfo(text[i], out glyphs[i], size, FontStyle.Normal)) continue;
                if (glyphs[i].maxX > glyphs[i].minX)
                {
                    left = Mathf.Min(left, pen + glyphs[i].minX);
                    right = Mathf.Max(right, pen + glyphs[i].maxX);
                    bottom = Mathf.Min(bottom, glyphs[i].minY);
                    top = Mathf.Max(top, glyphs[i].maxY);
                }
                pen += glyphs[i].advance;
            }
            if (right <= left || top <= bottom)
            {
                wall.SetVector("_Title", Vector4.zero);
                return;
            }
            float k = (tall - 2f * pad) / (top - bottom);
            int wide = Mathf.Clamp(Mathf.CeilToInt((right - left) * k) + 2 * pad, 64, 2048);
            k = Mathf.Min(k, (wide - 2f * pad) / (right - left));
            if (titleTexture != null && titleTexture.width != wide)
            {
                titleTexture.Release();
                Object.Destroy(titleTexture);
                titleTexture = null;
            }
            if (titleTexture == null)
            {
                titleTexture = new RenderTexture(wide, tall, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                    { name = "Loading Stage title", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                titleTexture.Create();
            }
            var ink = new Material(shader) { mainTexture = font.material.mainTexture };
            var before = RenderTexture.active;
            RenderTexture.active = titleTexture;
            GL.Clear(false, true, Color.clear);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, wide, 0, tall);
            ink.SetPass(0);
            GL.Begin(GL.QUADS);
            pen = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                var g = glyphs[i];
                if (g.maxX > g.minX)
                {
                    float x0 = pad + (pen + g.minX - left) * k, x1 = pad + (pen + g.maxX - left) * k;
                    float y0 = pad + (g.minY - bottom) * k, y1 = pad + (g.maxY - bottom) * k;
                    GL.TexCoord(g.uvBottomLeft); GL.Vertex3(x0, y0, 0);
                    GL.TexCoord(g.uvTopLeft); GL.Vertex3(x0, y1, 0);
                    GL.TexCoord(g.uvTopRight); GL.Vertex3(x1, y1, 0);
                    GL.TexCoord(g.uvBottomRight); GL.Vertex3(x1, y0, 0);
                }
                pen += g.advance;
            }
            GL.End();
            GL.PopMatrix();
            RenderTexture.active = before;
            Object.Destroy(ink);

            // on the round screen: as tall as the sample's name, narrower if a long name would touch the ring
            float halfTall = .74f, halfWide = halfTall * wide / tall, most = RingRadius * .8f;
            if (halfWide > most) { halfTall *= most / halfWide; halfWide = most; }
            wall.SetTexture("_TitleTex", titleTexture);
            wall.SetVector("_Title", new Vector4(0, 0, halfWide, halfTall));
        }

        // ---------- every frame ----------

        // Before the players are posed: the stage's clock (a landing this frame stamps its pulse with it).
        void StageClock(float now)
        {
            if (!stageRan)
            {
                stageRan = true;
                stageBase = now - 10f;
                stageNow = now;
            }
            stageDt = Mathf.Clamp(now - stageNow, 0f, .1f);
            stageNow = now;
            stageT = now - stageBase;
        }

        // A player has landed on their spot: their team's light runs across the wall from its side, and that side's
        // lamps turn to them.
        void StageJoin(Player p, float now)
        {
            int side = p.Team == 0 ? -1 : 1;
            pulses[pulseNext] = new Vector4(side, stageT, 0, 0);
            pulseNext = (pulseNext + 1) % pulses.Length;
            foreach (var h in heads)
            {
                if (h.Side != side && !(h.Side == 0 && h.At.x * side > 0f)) continue;
                h.SpotAt = p.At + Vector3.up * .8f;
                h.SpotFrom = now;
                h.Kick = 1f;
            }
        }

        void AnimateStage(float now)
        {
            if (wall == null) return;
            float t = stageT, dt = stageDt;
            Shader.SetGlobalFloat(StageTimeId, t);
            if (Cheer && !cheering) cheerAt = t;
            cheering = Cheer;
            cheerLevel = dt > 0f ? Mathf.MoveTowards(cheerLevel, Cheer ? 1f : 0f, dt / (Cheer ? .3f : .9f)) : cheerLevel;
            houseLevel = dt > 0f ? Mathf.MoveTowards(houseLevel, Mathf.Clamp01(House), dt / .6f) : houseLevel;
            float show = 1f - houseLevel, lamps = Mathf.Lerp(.12f, 1f, show);
            rayPhase += dt * Mathf.Lerp(.35f, 1.5f, cheerLevel);
            runPhase += dt * 1.3f;
            bulbStep += dt * 3f;

            // the newest entrance's punch: a short flash on the ring, its bulbs, the name and that team's pool
            float punch = 0f;
            int punchTeam = -1;
            foreach (var pulse in pulses)
            {
                float age = t - pulse.y;
                if (pulse.x == 0f || age < 0f || age >= .35f) continue;
                float f = 1f - age / .35f;
                if (f > punch) { punch = f; punchTeam = pulse.x < 0f ? 0 : 1; }
            }
            punch *= punch;

            wall.SetVectorArray("_Pulse", pulses);
            wall.SetVector("_Look", new Vector4(WallGain * Mathf.Lerp(.1f, 1f, show), cheerLevel, TitleGain, punch));
            wall.SetVector("_Phase", new Vector4(rayPhase, runPhase, 0, 0));
            var warm = Color.Lerp(MenuArt.Hex(0xFFE7B8), MenuArt.Hex(0xFFD070), cheerLevel).linear;
            float breathe = .9f + .1f * Mathf.Sin(t * 1.7f);
            neon.SetVector("_Color", V4(warm, (3.2f * breathe + 6f * punch + 1.2f * cheerLevel) * lamps, .5f));
            neonThin.SetVector("_Color", V4(warm, (1.7f + 3f * punch + .8f * cheerLevel) * lamps, .5f));
            marquee.SetVector("_State", new Vector4(punch, cheerLevel, lamps, bulbStep));
            boardStrip.SetVector("_Color", Lin(0xFFD9A0, 2.4f * lamps, 0f));
            wallFoot.SetVector("_Color", Lin(0x9FA8FF, 2f * lamps, 0f));

            // the pools on the board drift a little; the pool of the team that has just got a player jumps
            key.intensity = keyBase * Mathf.Lerp(.55f, 1f, show);
            fill.intensity = fillBase * Mathf.Lerp(.6f, 1f, show);
            wallLight.intensity = wallBase * show * (1f + .8f * punch);
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                var to = new Vector3(side * 2.3f + .9f * Mathf.Sin(t * .33f + k * 2f), StageHeight, .9f * Mathf.Sin(t * .27f + k));
                teamLight[k].transform.localRotation = Quaternion.LookRotation(to - teamLight[k].transform.localPosition);
                teamLight[k].intensity = teamBase[k] * show * (1f + 1.4f * (punchTeam == k ? punch : 0f));
            }

            // the air
            dust.SetVector("_Color", Lin(0xFFF0DC, .55f * lamps));
            var mist = Color.Lerp(MenuArt.Hex(0x5A48E0), MenuArt.Hex(punchTeam == 0 ? 0x2EA8FF : 0xFF3355), punchTeam < 0 ? 0f : punch * .7f);
            mist = Color.Lerp(mist, MenuArt.Hex(0xFFC24A), cheerLevel * .5f).linear;
            haze.SetVector("_Color", V4(mist, .085f * lamps * (1f + punch)));
            hazeNear.SetVector("_Color", V4(mist, .04f * lamps * (1f + punch)));
            bool falling = cheerLevel > 0f;
            if (glitterRenderer.enabled != falling) glitterRenderer.enabled = falling;
            if (falling) glitter.SetVector("_Fall", new Vector4(1.5f, 7f, cheerAt, cheerLevel * lamps));

            foreach (var h in heads) AnimateHead(h, now, t, dt, show);
        }

        void AnimateHead(Head h, float now, float t, float dt, float show)
        {
            var target = IdleTarget(h, t);
            if (cheerLevel > 0f) target = Vector3.Lerp(target, CheerTarget(h, t), cheerLevel);
            // a player has just come in on this lamp's side: it snaps to them, holds, and lets go
            float since = now - h.SpotFrom;
            if (since >= 0f && since < SpotHold + SpotLet)
                target = Vector3.Lerp(target, h.SpotAt, since < SpotHold ? 1f : 1f - (since - SpotHold) / SpotLet);
            h.Aim = dt > 0f ? Vector3.Lerp(h.Aim, target, 1f - Mathf.Exp(-dt * 10f)) : target;
            h.Kick = Mathf.Max(0f, h.Kick - dt / .45f);

            var dir = (h.Aim - h.At).normalized;
            var turn = Quaternion.LookRotation(dir);
            h.Body.localRotation = turn;
            h.Beam.localPosition = h.At + dir * .14f;
            h.Beam.localRotation = turn;

            var tint = Color.Lerp(MenuArt.Hex(h.Rgb), MenuArt.Hex(0xFFCB5C), cheerLevel * (h.Side == 0 ? .6f : .85f)).linear;
            float power = h.Power * show * (1f + 1.6f * h.Kick + .5f * cheerLevel);
            h.BeamLook.SetVector("_Color", V4(tint, BeamStrength * power));
            h.Lens.SetVector("_Color", V4(tint, 6f * power + .02f, 0f));
            // the lamp flares, with a streak across it, when it is turned towards the camera
            var lens = root.transform.TransformPoint(h.At + dir * .15f);
            var toEye = camera.transform.position - lens;
            float flare = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(dir, toEye.normalized)), 4f) * power;
            var face = Quaternion.LookRotation(-toEye);
            h.Flare.SetPositionAndRotation(lens, face);
            h.Flare.localScale = Vector3.one * (1.2f + 2.6f * flare);
            h.FlareLook.SetVector("_Color", V4(tint, 2.2f * flare));
            h.Streak.SetPositionAndRotation(lens, face);
            h.Streak.localScale = new Vector3(5f + 9f * flare, .28f, 1f);
            h.StreakLook.SetVector("_Color", V4(tint, 1.4f * flare));
        }

        // Where a lamp points with nothing going on: each wanders over its own part of the board. A tower's lowest
        // lamp keeps to its own team's half, the middle one to the middle, the top one reaches across.
        Vector3 IdleTarget(Head h, float t)
        {
            if (h.Side == 0)
                return new Vector3(h.At.x * .42f + 1.8f * Mathf.Sin(t * .27f + h.Index * 1.9f), StageHeight, -.8f + 2.4f * Mathf.Sin(t * .21f + h.Index * 1.1f));
            float own = h.Side * (2.6f - 2.6f * h.Index), skew = h.Side > 0 ? 1.3f : 0f;
            return new Vector3(own + 1.7f * Mathf.Sin(t * .31f + h.Index * 2.1f + skew), StageHeight, -1.2f + 2.6f * Mathf.Sin(t * .23f + h.Index * 1.7f + skew * .5f));
        }

        // Everyone is in: the lamps sweep the board from side to side, the two towers against each other.
        Vector3 CheerTarget(Head h, float t)
        {
            float phase = h.Index * .9f + (h.Side > 0 ? Mathf.PI : 0f) + (h.Side == 0 ? h.Index * .7f : 0f);
            return new Vector3(4.6f * Mathf.Sin(t * 2.6f + phase), StageHeight + 1f, -1f + 3f * Mathf.Cos(t * 1.3f + phase));
        }

        // ---------- drawing ----------

        // Around the camera's draw (LoadingStudio.Render): the stage's haze and ambient light in place of the hall's.
        // No sky is mirrored: the deck would shine with the active scene's sky.
        void StageLighting()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = MenuArt.Hex(0x05050A);
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 130f;
            float lit = Mathf.Lerp(.5f, 1f, 1f - houseLevel);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = MenuArt.Hex(0x343060) * (.55f * lit);
            RenderSettings.ambientEquatorColor = MenuArt.Hex(0x241E3C) * (.5f * lit);
            RenderSettings.ambientGroundColor = MenuArt.Hex(0x0A090E);
            RenderSettings.reflectionIntensity = 0f;
        }

        // The mirror stands where the camera would be seen in the floor (no roll: its picture is upside down, which
        // the floor's shader undoes) and draws what the floor mirrors.
        void RenderMirror()
        {
            if (mirror == null) return;
            var at = camera.transform.position;
            var forward = camera.transform.forward;
            float plane = root.transform.position.y + StageHeight;
            mirror.transform.SetPositionAndRotation(new Vector3(at.x, 2f * plane - at.y, at.z),
                                                    Quaternion.LookRotation(new Vector3(forward.x, -forward.y, forward.z), Vector3.up));
            mirror.fieldOfView = camera.fieldOfView;
            mirror.nearClipPlane = camera.nearClipPlane;
            mirror.farClipPlane = camera.farClipPlane;
            mirror.aspect = Texture.width / (float)Mathf.Max(1, Texture.height);
            mirror.Render();
        }

        void DisposeStage()
        {
            foreach (var material in stageMaterials) if (material != null) Object.Destroy(material);
            foreach (var mesh in stageMeshes) if (mesh != null) Object.Destroy(mesh);
            stageMaterials.Clear();
            stageMeshes.Clear();
            heads.Clear();
            if (mirror != null) mirror.targetTexture = null;
            if (mirrorTexture != null) { mirrorTexture.Release(); Object.Destroy(mirrorTexture); mirrorTexture = null; }
            if (titleTexture != null) { titleTexture.Release(); Object.Destroy(titleTexture); titleTexture = null; }
            if (lookProfile != null) Object.Destroy(lookProfile);
            lookProfile = null;
            wall = null;
        }

        // ---------- meshes ----------

        // A beam: a tube of radius 1 along +z from 0 to 1, opened into a cone by StageBeam.shader.
        Mesh Tube()
        {
            const int n = 48;
            var v = new Vector3[(n + 1) * 2];
            var tris = new int[n * 6];
            for (int j = 0; j <= n; j++)
            {
                float a = Mathf.PI * 2f * j / n, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[j * 2] = new Vector3(c, s, 0f);
                v[j * 2 + 1] = new Vector3(c, s, 1f);
            }
            for (int j = 0; j < n; j++)
            {
                int a = j * 2, k = j * 6;
                tris[k] = a; tris[k + 1] = a + 1; tris[k + 2] = a + 2;
                tris[k + 3] = a + 2; tris[k + 4] = a + 1; tris[k + 5] = a + 3;
            }
            var mesh = new Mesh { name = "Stage beam", vertices = v, triangles = tris };
            // the shader draws it far beyond its own vertices
            mesh.bounds = new Bounds(new Vector3(0, 0, 14f), new Vector3(10f, 10f, 32f));
            stageMeshes.Add(mesh);
            return mesh;
        }

        // A flat band round the ring, upright, its vertices in metres from the ring's centre.
        Mesh Band(float inner, float outer, int n)
        {
            var v = new Vector3[(n + 1) * 2];
            var tris = new int[n * 6];
            for (int j = 0; j <= n; j++)
            {
                float a = Mathf.PI * 2f * j / n, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[j * 2] = new Vector3(c * inner, s * inner, 0f);
                v[j * 2 + 1] = new Vector3(c * outer, s * outer, 0f);
            }
            for (int j = 0; j < n; j++)
            {
                int a = j * 2, k = j * 6;
                tris[k] = a; tris[k + 1] = a + 1; tris[k + 2] = a + 2;
                tris[k + 3] = a + 2; tris[k + 4] = a + 1; tris[k + 5] = a + 3;
            }
            var mesh = new Mesh { name = "Stage band", vertices = v, triangles = tris };
            mesh.RecalculateBounds();
            stageMeshes.Add(mesh);
            return mesh;
        }

        // A box truss standing on its foot: four chords, and on each face a rung and a brace per bay.
        Mesh Truss(float tall, float wide, float bay)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tris = new List<int>();
            float h = wide / 2f;
            Vector3[] corner = { new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(h, 0, h), new Vector3(-h, 0, h) };
            foreach (var c in corner) Bar(v, n, tris, c, c + Vector3.up * tall, .034f);
            int bays = Mathf.Max(1, Mathf.RoundToInt(tall / bay));
            float rise = tall / bays;
            for (int b = 0; b <= bays; b++)
                for (int k = 0; k < 4; k++)
                {
                    Vector3 a = corner[k] + Vector3.up * (b * rise), c = corner[(k + 1) % 4] + Vector3.up * (b * rise);
                    Bar(v, n, tris, a, c, .02f);
                    if (b == bays) continue;
                    if ((b + k) % 2 == 0) Bar(v, n, tris, a, c + Vector3.up * rise, .02f);
                    else Bar(v, n, tris, c, a + Vector3.up * rise, .02f);
                }
            var mesh = new Mesh { name = "Stage truss" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            stageMeshes.Add(mesh);
            return mesh;
        }

        // A six-sided bar from a to b.
        static void Bar(List<Vector3> v, List<Vector3> n, List<int> tris, Vector3 a, Vector3 b, float r)
        {
            var axis = (b - a).normalized;
            var side = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized;
            var other = Vector3.Cross(axis, side);
            int first = v.Count;
            const int sides = 6;
            for (int j = 0; j <= sides; j++)
            {
                float angle = Mathf.PI * 2f * j / sides;
                var normal = side * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                v.Add(a + normal * r); n.Add(normal);
                v.Add(b + normal * r); n.Add(normal);
            }
            for (int j = 0; j < sides; j++)
            {
                int k = first + j * 2;
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1);
                tris.Add(k + 2); tris.Add(k + 3); tris.Add(k + 1);
            }
        }

        // `count` motes for StageDust.shader: each a quad whose four vertices share a place in the unit box and two
        // random numbers; the shader moves them and turns them to the camera. Foil comes in the stage's colours.
        Mesh Motes(int count, int seed, bool foil)
        {
            var rng = new System.Random(seed);
            var v = new Vector3[count * 4]; var corner = new Vector2[count * 4]; var rnd = new Vector2[count * 4];
            var tint = new Color[count * 4];
            var tris = new int[count * 6];
            Color[] foils =
            {
                MenuArt.Hex(0xFFD36B).linear, MenuArt.Hex(0xFFF1D0).linear, MenuArt.Hex(0xFFB84A).linear,
                MenuArt.Hex(0x6FC4FF).linear, MenuArt.Hex(0xFF6B7F).linear
            };
            for (int i = 0; i < count; i++)
            {
                var at = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble());
                var two = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                // mostly gold and ivory, a little of each team's colour
                var c = foil ? foils[rng.Next(100) < 72 ? rng.Next(3) : 3 + rng.Next(2)] : Color.white;
                for (int k = 0; k < 4; k++)
                {
                    v[i * 4 + k] = at;
                    corner[i * 4 + k] = new Vector2(k == 0 || k == 3 ? -1f : 1f, k < 2 ? -1f : 1f);
                    rnd[i * 4 + k] = two;
                    tint[i * 4 + k] = c;
                }
                int b = i * 4, q = i * 6;
                tris[q] = b; tris[q + 1] = b + 2; tris[q + 2] = b + 1;
                tris[q + 3] = b; tris[q + 4] = b + 3; tris[q + 5] = b + 2;
            }
            var mesh = new Mesh { name = foil ? "Stage glitter" : "Stage dust", vertices = v, uv = corner, uv2 = rnd, colors = tint, triangles = tris };
            stageMeshes.Add(mesh);
            return mesh;
        }
    }
}
