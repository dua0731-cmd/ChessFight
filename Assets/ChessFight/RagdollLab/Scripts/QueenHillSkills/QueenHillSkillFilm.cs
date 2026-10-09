using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Films the Queen of the Hill skills for review (R89): plays the probe's runs from a fixed camera per shot, steps game
    /// time 1/fps per frame (Time.captureFramerate) so the video plays at true speed however long a frame takes, and hands
    /// every frame to whoever listens (in the editor QueenHillSkillFilmEncoder writes the .mp4). Two films in one go:
    /// every shot at full speed, then every shot again in the lab's slow motion (횞0.3) from the moment it is staged.
    /// Every StillEvery-th frame is kept as a .jpg next to each video (1 = every frame, to look at an effect frame by
    /// frame). A caption at the top names the skill, the key the probe presses shows at the bottom (only in the film).
    /// </summary>
    [DefaultExecutionOrder(300)]   // after the effects shake the camera (210)
    public class QueenHillSkillFilm : MonoBehaviour
    {
        public static event Action<string, int, int, int> Began;
        public static event Action<Texture2D> Frame;
        public static event Action Ended;
        public static string Status { get; private set; } = "idle";
        public static int Frames { get; private set; }
        public static int StillEvery = 10;
        /// <summary>A film is recording this frame (the bed hides its panel).</summary>
        public static bool Rolling { get; private set; }
        /// <summary>Per shot: the frame its takes began on, in each film (to find a shot among the stills).</summary>
        public static readonly List<string> Index = new List<string>();

        struct Shot
        {
            public string run, title, note;
            public Vector3 eye, look;
            public float fov;
        }

        static readonly Shot[] Shots =
        {
            new Shot { run = "king", title = "1. ??쨌 洹쇱젒 ?몄쐞 (B: ???먯떊? 鍮좎쭚)", note = "?먮찓?꾨뱶 臾쇨껐 ???꾧뎔 諛쒕컩 ?몄쐞 怨좊━ 쨌 ????寃寃⑹뿉 ?꾧뎔? 踰꾪떚怨?珥덈줉 怨좊━) ?밸쭔 ?섏뼱吏?,
                eye = new Vector3(3.8f, 4.6f, -7.2f), look = new Vector3(3.6f, 0.3f, -2.3f), fov = 42f },
            new Shot { run = "queen", title = "2. ??쨌 ?붾갑 寃寃?(A: ??諛⑺뼢 湲?寃寃?", note = "泥댁뒪??4移몄씠 李⑥삤由???湲덉깋 寃湲곌? ?좎븘媛?쨌 以??????섏뼱吏?쨌 踰쎌뿉 留됲? ?ㅻ뒗 ?덉쟾",
                eye = new Vector3(3.9f, 4.8f, -3.2f), look = new Vector3(3.7f, 0.3f, 3.3f), fov = 44f },
            new Shot { run = "rook", title = "3. 猷?쨌 罹먯뒳留?援먮? (B: ?꾧뎔 ?꾧뎄?? ?섎씫 諛쏄퀬)", note = "?꾧뎔 ?곗쓣 怨⑤씪 ?붿껌 ???섎씫(?쒓퀎媛 李? ???⑥젙臾??대━怨?????由щ낯 ?꾩튂濡??먮━ 諛붽퓞",
                eye = new Vector3(1.4f, 4.2f, -0.6f), look = new Vector3(-4.3f, 1.3f, 4.9f), fov = 46f },
            new Shot { run = "bishop", title = "4. 鍮꾩닄 쨌 援먯감 怨듭쨷 ?ш꺽 (B: 留욎쑝硫?諛???섏뼱吏?", note = "蹂대씪 留덈쫫紐?移??꾨줈 ?좎삤由??????섏쭚 ??X???뚮룞 ??踰쎌쓣 ?ㅻⅤ???곸씠 ?⑥뼱吏?,
                eye = new Vector3(-0.8f, 3.0f, -3.8f), look = new Vector3(-1.4f, 1.0f, 3.4f), fov = 46f },
            new Shot { run = "knight", title = "5. ?섏씠??쨌 ?꾩빟 ?뺤갑 (B: ??痢듦퉴吏, ?믪? 怨녹뿉???⑹옉?섎㈃ ?⑥뼱吏?", note = "2痢듭? ?덈Т ?믪븘 ?뚯깋 ??1痢?留먭돕 ?쒖떇 ???꾩빟 ???곸씠 ?⑹옉?댁졇 媛?μ옄由ъ뿉???⑥뼱吏?,
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
            new Shot { run = "pawn", title = "6. ??쨌 鍮꾩쭛怨??뚰뙆 (B: 紐몄쓣 ??텛?????숈옉)", note = "??猷??뚯쭊 寃쎄퀬(鍮④컙 以꾨Т??移? ???ㅻ━瑜??욌뮘濡?踰뚮━怨???쾶 ?놁쑝濡?鍮좎쭚 ??猷⑹씠 鍮쀫굹媛?,
                eye = new Vector3(-4.2f, 1.9f, -4.0f), look = new Vector3(2.4f, 0.4f, -4.6f), fov = 44f },
            new Shot { run = "pawn-squeeze", title = "6. ??쨌 鍮꾩쭛怨??뚰뙆 (醫곸? ??", note = "??쾶 紐몄쓣 ?숈씠怨??????ъ씠瑜?鍮꾩쭛怨?吏?섍컧 쨌 臾댁쟻 ?놁쓬, 遺?ろ엺 ?곸? ?놁쑝濡?鍮꾪궡",
                eye = new Vector3(-0.4f, 2.4f, -9.4f), look = new Vector3(0.6f, 0.3f, -5f), fov = 40f },
        };

        /// <summary>R93 (?밴퇋 ??s notes on R89): only what changed.</summary>
        static readonly Shot[] ShotsR93 =
        {
            new Shot { run = "king-help", title = "1. ??쨌 洹쇱젒 ?몄쐞 ???쒗뿕 ?꾩슦誘?쨌 紐??ㅺ낸??, note = "F ???꾧뎔 ??3紐낆씠 ?욎뿉, ???몄씠 5 m ?욎뿉 ??쨌 ?몄쐞 諛쏆? ?꾧뎔? 紐몄뿉 珥덈줉 ?ㅺ낸????寃寃⑹뿉 ?꾧뎔? 踰꾪떚怨??밸쭔 ?섏뼱吏?,
                eye = new Vector3(2.0f, 2.8f, -7.0f), look = new Vector3(3.2f, 0.4f, -2.5f), fov = 50f },
            new Shot { run = "queen", title = "2. ??쨌 ?붾갑 寃寃????꾨옒?먯꽌 ?꾨줈 ?щ젮 踰좉린", note = "移쇱쓣 ?ㅻⅨ履??꾨옒 ?ㅻ줈 ?대젮 ?↔퀬(諛붾떏???レ? ?딆쓬) ??移쇱씠 ?욎쓣 吏????寃湲????쇱そ ?꾨줈",
                eye = new Vector3(0.6f, 1.5f, -0.2f), look = new Vector3(1.3f, 0.5f, 3.0f), fov = 56f },
            new Shot { run = "rook-up-bishop", title = "3. 猷?쨌 罹먯뒳留?援먮? ??湲곕Ъ 怨좎쑀??由щ낯 (鍮꾩닄)", note = "F ???쒗뿕 ?꾩슦誘멸? ??痢??꾩뿉 ?꾧뎔 1紐?湲곕Ъ 臾댁옉?? 쨌 猷?二쇳솴 諛?+ 鍮꾩닄 蹂대씪 諛? 媛?대뜲 洹몃씪?곗씠??,
                eye = new Vector3(0.6f, 2.2f, 1.4f), look = new Vector3(-3.2f, 0.9f, 6.0f), fov = 46f },
            new Shot { run = "rook-up-queen", title = "3. 猷?쨌 罹먯뒳留?援먮? ??湲곕Ъ 怨좎쑀??由щ낯 (??", note = "猷?二쇳솴 諛?+ ??湲덉깋 諛? 媛?대뜲 洹몃씪?곗씠??,
                eye = new Vector3(0.6f, 2.2f, 1.4f), look = new Vector3(-3.2f, 0.9f, 6.0f), fov = 46f },
            new Shot { run = "rook-up-knight", title = "3. 猷?쨌 罹먯뒳留?援먮? ??湲곕Ъ 怨좎쑀??由щ낯 (?섏씠??", note = "猷?二쇳솴 諛?+ ?섏씠???섎뒛??諛? 媛?대뜲 洹몃씪?곗씠??,
                eye = new Vector3(0.6f, 2.2f, 1.4f), look = new Vector3(-3.2f, 0.9f, 6.0f), fov = 46f },
            new Shot { run = "knight", title = "5. ?섏씠??쨌 ?꾩빟 ?뺤갑 ?????믨쾶, 癒몃━ ???먮룞 議곗?", note = "2痢듭? ?덈Т ?믪븘 ?뚯깋 ????洹쇱쿂瑜?議곗??섎㈃ 癒몃━ ?꾩뿉 留먭돕 ?쒖떇 ???믨쾶 ?꾩빟??癒몃━瑜?諛잕퀬 ?⑹옉",
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
        };

        /// <summary>R94 (?밴퇋 ??s notes on R93): the rook's longer reach and seamless arch, the knight's tighter catch.</summary>
        static readonly Shot[] ShotsR94 =
        {
            new Shot { run = "rook-far", title = "3. 猷?쨌 罹먯뒳留?援먮? ??踰붿쐞 9 m", note = "6 m ??9 m(泥댁뒪??6移? 쨌 8.4 m ?⑥뼱吏??꾧뎔 ?곌낵 援먮?",
                eye = new Vector3(4.0f, 2.6f, -10.5f), look = new Vector3(4.0f, 0.8f, -3.5f), fov = 56f },
            new Shot { run = "rook-up-bishop", title = "3. 猷?쨌 罹먯뒳留?援먮? ??媛?대뜲 鍮덊땲 ?놁빊 (鍮꾩닄)", note = "由щ낯????以꾨줈 洹몃젮 ???됱씠 留뚮굹??怨녹뿉 ?덉씠 ?놁쓬 쨌 猷?二쇳솴 ??鍮꾩닄 蹂대씪 洹몃씪?곗씠??,
                eye = new Vector3(0.6f, 2.2f, 1.4f), look = new Vector3(-3.2f, 0.9f, 6.0f), fov = 46f },
            new Shot { run = "rook-up-knight", title = "3. 猷?쨌 罹먯뒳留?援먮? ??媛?대뜲 鍮덊땲 ?놁빊 (?섏씠??", note = "猷?二쇳솴 ???섏씠???섎뒛??洹몃씪?곗씠?? 瑗??湲곗뿉??諛섎컲",
                eye = new Vector3(0.6f, 2.2f, 1.4f), look = new Vector3(-3.2f, 0.9f, 6.0f), fov = 46f },
            new Shot { run = "knight", title = "5. ?섏씠??쨌 ?꾩빟 ?뺤갑 ??癒몃━ ?먮룞 議곗? 1.15 m", note = "?곸뿉??1.24 m ?⑥뼱吏?怨녹? ???≫옒 ??0.7 m濡???린硫?癒몃━ ?꾩뿉 留먭돕 ?쒖떇 ??癒몃━瑜?諛잕퀬 ?⑹옉",
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
        };

        /// <summary>R95 (?밴퇋 ??s notes on R94): the queen's warning squares on the slash's own line, the bishop's two
        /// shots, the knight's catch at 1.05 m.</summary>
        static readonly Shot[] ShotsR95 =
        {
            new Shot { run = "queen-high", title = "2. ??쨌 ?붾갑 寃寃??????꾩뿉??(寃쎄퀬 移?= ?ㅼ젣 寃湲?", note = "寃湲곕뒗 ???믪씠?먯꽌 ?묐컮濡???寃쎄퀬 移몃룄 洹??믪씠濡??좎꽌 ?댁뼱吏?쨌 媛숈? 痢??곷쭔 ?섏뼱吏? ?꾨옒痢돠룸컮?????꾨줈??吏?섍컧",
                eye = new Vector3(1.5f, 3.6f, 2.5f), look = new Vector3(-5.0f, 1.4f, 6.5f), fov = 50f },
            new Shot { run = "queen-low", title = "2. ??쨌 ?붾갑 寃寃????꾨옒?먯꽌 踰?履쎌쑝濡?(諛섎? 寃쎌슦)", note = "寃쎄퀬 移몄씠 寃湲곗쿂??踰쎌뿉??硫덉땄(?꾩링????洹몃젮吏? 쨌 ?욎쓽 ?곷쭔 ?섏뼱吏? 踰???1痢??곸? 洹몃?濡?,
                eye = new Vector3(0.4f, 2.0f, -1.6f), look = new Vector3(-2.8f, 0.6f, 3.2f), fov = 52f },
            new Shot { run = "bishop", title = "4. 鍮꾩닄 쨌 援먯감 怨듭쨷 ?ш꺽 ??2諛?, note = "1諛? 踰쎌쓣 ?ㅻⅤ???????⑥뼱吏硫??ㅼ떆 議곗? ??2諛? 1痢???쨌 2諛??섍굅???쒓컙?????섎㈃ ?대젮??,
                eye = new Vector3(-0.8f, 3.0f, -3.8f), look = new Vector3(-1.8f, 1.0f, 3.8f), fov = 50f },
            new Shot { run = "knight", title = "5. ?섏씠??쨌 ?꾩빟 ?뺤갑 ??癒몃━ ?먮룞 議곗? 1.05 m", note = "?곸뿉??1.12 m ?⑥뼱吏?怨녹? ???≫옒 ??0.7 m濡???린硫?癒몃━ ??留먭돕 ??癒몃━瑜?諛잕퀬 ?⑹옉",
                eye = new Vector3(0.6f, 2.4f, 12.6f), look = new Vector3(-3.0f, 1.0f, 7.0f), fov = 48f },
        };

        /// <summary>R97 (?밴퇋 ?? the queen's slash goes where its squares are): down from a tier, up onto one, and a real wall
        /// still stops it.</summary>
        static readonly Shot[] ShotsR97 =
        {
            new Shot { run = "queen-high", title = "2. ??쨌 ?붾갑 寃寃????꾩뿉???꾨옒濡?, note = "寃쎄퀬 移몄씠 ?꾨옒痢돠룸컮?μ쓣 ?곕씪 源붾┝ ??寃湲곕룄 移몄쓣 ?곕씪 ?대젮媛 媛숈? 痢돠룹븘?섏링쨌諛붾떏 ??紐⑤몢 ?섏뼱吏?쨌 以???履쎌쑝濡?諛由?,
                eye = new Vector3(1.5f, 3.6f, 2.5f), look = new Vector3(-5.0f, 1.4f, 6.5f), fov = 50f },
            new Shot { run = "queen-low", title = "2. ??쨌 ?붾갑 寃寃????꾨옒?먯꽌 ?꾨줈 (諛섎?)", note = "寃湲곌? ??痢?0.9 m) ?깆쓣 ?怨??щ씪媛????욎쓽 ?곷룄, ????1痢??곷룄 ?섏뼱吏?,
                eye = new Vector3(0.4f, 2.0f, -1.6f), look = new Vector3(-2.8f, 0.6f, 3.2f), fov = 52f },
            new Shot { run = "queen", title = "2. ??쨌 ?붾갑 寃寃????믪? 踰쎌? 洹몃?濡?留됲옒", note = "??痢듬낫???믪? ?뚮꼍(1.6 m)? 留됲옒 쨌 踰????곸? ?덉쟾, 以?諛??곷룄 洹몃?濡?,
                eye = new Vector3(3.9f, 4.8f, -3.2f), look = new Vector3(3.7f, 0.3f, 3.3f), fov = 44f },
        };

        /// <summary>R98 (?밴퇋 ??s screenshots on the green slopes: the squares stood flat, half in the slope): the squares lie on
        /// the slope and the slash runs up and down it; then the hill again, down and up.</summary>
        static readonly Shot[] ShotsR105 =
        {
            new Shot { run = "bishop-wait", title = "4. 鍮꾩닄 쨌 援먯감 怨듭쨷 ?ш꺽 ???묓깂?먯쿂??泥쒖쿇?? ???섎㈃ 痍⑥냼, 2諛쒖㎏ 湲곕떎由?, note = "?좎꽌 W濡?泥쒖쿇???대룞 ??3珥????섎㈃ ?대젮?????ㅼ떆 ?좎꽌 1諛???5珥??섍쾶 湲곕떎?ㅻ룄 ???섍컧 ??2諛?,
                eye = new Vector3(-1.0f, 3.4f, -5.5f), look = new Vector3(-1.0f, 1.2f, 4.5f), fov = 55f },
        };

        static readonly Shot[] ShotsR98 =
        {
            new Shot { run = "queen-slope-up", title = "2. ??쨌 ?붾갑 寃寃???寃쎌궗濡??꾨옒?먯꽌 ?꾨줈", note = "寃쎄퀬 移몄씠 寃쎌궗濡쒖뿉 遺숈뼱 源붾┝ ??寃湲곌? 寃쎌궗瑜??怨??щ씪媛 移??????????섏뼱吏?,
                eye = new Vector3(-11.8f, 2.6f, -11.8f), look = new Vector3(-19.5f, 0.8f, -9f), fov = 50f },
            new Shot { run = "queen-slope-down", title = "2. ??쨌 ?붾갑 寃寃???寃쎌궗濡??꾩뿉???꾨옒濡?, note = "寃쎄퀬 移몄씠 寃쎌궗濡쒖뿉 遺숈뼱 源붾┝ ??寃湲곌? 寃쎌궗瑜??怨??대젮媛 移??????????섏뼱吏?,
                eye = new Vector3(-16.0f, 2.8f, -15.0f), look = new Vector3(-20.0f, 0.7f, -9f), fov = 50f },
            new Shot { run = "queen-high", title = "2. ??쨌 ?붾갑 寃寃??????꾩뿉???꾨옒濡?, note = "移몄씠 ?꾨옒痢돠룸컮?μ뿉 源붾┝ ??寃湲곌? 移몄쓣 ?곕씪 ?대젮媛 媛숈? 痢돠룹븘?섏링쨌諛붾떏 ??紐⑤몢 ?섏뼱吏?,
                eye = new Vector3(1.5f, 3.6f, 2.5f), look = new Vector3(-5.0f, 1.4f, 6.5f), fov = 50f },
            new Shot { run = "queen-low", title = "2. ??쨌 ?붾갑 寃寃??????꾨옒?먯꽌 ?꾨줈", note = "寃湲곌? ??痢?0.9 m) ?깆쓣 ?怨??щ씪媛????욎쓽 ?곷룄, ????1痢??곷룄 ?섏뼱吏?,
                eye = new Vector3(0.4f, 2.0f, -1.6f), look = new Vector3(-2.8f, 0.6f, 3.2f), fov = 52f },
        };

        /// <summary>Which shots Run films: "r98" (the default, R98's queen on the slopes and the hill), "r97", "r95", "r94",
        /// "r93" or "r89" (all six skills).</summary>
        public static string ShotSet = "r105";

        LabGame game;
        Camera cam;
        RenderTexture rt;
        Texture2D frame;
        Font font;
        PawnRushSkillFx.Text3D title, note, key;
        Transform banner, keyStrip;
        int keySerial;
        float keyLeft;
        Vector3 eye, look;
        string stills;
        int width, height;
        // The captions hang this far in front of the camera, so that every shot's field of view shows them at the same
        // spot and size as a 44째 shot does at 1 m (a narrower shot pushed the title off the top).
        float depth = 1f, titleFit = 1f, noteFit = 1f, keyFit = 1f;

        /// <summary>Film every shot (or only those whose run starts with <paramref name="only"/>) into
        /// <paramref name="path"/> (full speed) and the same name + "_slow" (slow motion). Needs Play mode in
        /// QueenOfTheHill_SkillTest.</summary>
        public static string Run(string path, int width = 1280, int height = 720, int fps = 60, string only = null, bool slow = true)
        {
            var bed = FindFirstObjectByType<QueenHillSkillBed>();
            if (bed == null) return "no QueenHillSkillBed (open QueenOfTheHill_SkillTest and press Play)";
            var film = bed.GetComponent<QueenHillSkillFilm>();
            if (film == null) film = bed.gameObject.AddComponent<QueenHillSkillFilm>();
            film.StopAllCoroutines();
            // A film stopped half-way leaves its camera (and the caption strips on it) standing in the world.
            if (rig != null) Destroy(rig);
            rig = null;
            Rolling = false;
            film.StartCoroutine(film.Main(path, width, height, fps, only, slow));
            return Status = "starting";
        }

        static GameObject rig;

        /// <summary>One picture from a camera at <paramref name="eye"/> looking at <paramref name="look"/> into a .jpg
        /// (to try a shot's framing while the probe is frozen; the film's own look: HDR and bloom).</summary>
        public static string Snap(string path, Vector3 eye, Vector3 look, float fov = 44f, int width = 960, int height = 540)
        {
            var go = new GameObject("QotH snap camera");
            var c = go.AddComponent<Camera>();
            var game = FindFirstObjectByType<LabGame>();
            if (game != null && game.labCamera != null) c.CopyFrom(game.labCamera.Cam);
            c.rect = new Rect(0f, 0f, 1f, 1f);
            c.enabled = false;
            c.fieldOfView = fov;
            PawnRushSkillFx.PrepareCamera(c);
            c.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            c.targetTexture = target;
            c.Render();
            c.targetTexture = null;
            RenderTexture.active = target;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllBytes(path, tex.EncodeToJPG(88));
            DestroyImmediate(go);
            DestroyImmediate(target);
            DestroyImmediate(tex);
            return path;
        }

        IEnumerator Main(string path, int w, int h, int fps, string only, bool slowToo)
        {
            game = GetComponent<LabGame>();
            width = w;
            height = h;
            var go = new GameObject("QotH skill film camera");
            rig = go;
            cam = go.AddComponent<Camera>();
            if (game != null && game.labCamera != null) cam.CopyFrom(game.labCamera.Cam);
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.enabled = false;
            PawnRushSkillFx.PrepareCamera(cam);
            rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "留묒? 怨좊뵓", "Segoe UI", "Arial" }, 64);
            title = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(go.transform, font, " ", new Color(1f, 0.9f, 0.6f), NoteSize);
            banner = Banner(go.transform, 0.345f);
            keyStrip = Banner(go.transform, -0.335f, 0.5f);
            keyStrip.gameObject.SetActive(false);
            key = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, KeySize);

            QueenHillSkillFx.ViewOverride = cam;
            if (game != null) game.SuppressInput = true;
            Time.captureFramerate = fps;
            LabCamera.GameTimeClock = true;
            Index.Clear();
            foreach (bool slow in slowToo ? new[] { false, true } : new[] { false })
            {
                string file = slow ? Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path) + "_slow.mp4") : path;
                stills = Path.Combine(Path.GetDirectoryName(file) ?? ".", Path.GetFileNameWithoutExtension(file) + "_stills");
                Directory.CreateDirectory(stills);
                Frames = 0;
                Began?.Invoke(file, w, h, fps);
                foreach (var shot in ShotSet == "r89" ? Shots : ShotSet == "r93" ? ShotsR93 : ShotSet == "r94" ? ShotsR94 : ShotSet == "r95" ? ShotsR95 : ShotSet == "r97" ? ShotsR97 : ShotSet == "r98" ? ShotsR98 : ShotsR105)
                {
                    if (!string.IsNullOrEmpty(only) && !shot.run.StartsWith(only)) continue;
                    Status = $"filming {shot.run}{(slow ? " (slow)" : "")}";
                    eye = shot.eye;
                    look = shot.look;
                    cam.fieldOfView = shot.fov;
                    depth = Mathf.Tan(22f * Mathf.Deg2Rad) / Mathf.Tan(shot.fov * 0.5f * Mathf.Deg2Rad);
                    banner.localPosition = new Vector3(0f, 0.345f, 1.05f * depth);
                    keyStrip.localPosition = new Vector3(0f, -0.335f, 1.05f * depth);
                    SetCaption(shot.title, slow ? $"?먮━寃?횞{(game != null ? game.slowMotionScale : 0.3f):0.0#} 쨌 {shot.note}" : shot.note);
                    if (game != null) game.SetSlowMotion(false);
                    QueenHillSkillProbe.Run(shot.run);
                    yield return null;
                    while (!QueenHillSkillProbe.Staged && !Done) yield return null;
                    if (slow && game != null) game.SetSlowMotion(true);
                    Index.Add($"{(slow ? "slow" : "full")} {shot.run} from frame {Frames}");
                    keySerial = QueenHillSkillProbe.HintSerial;
                    Rolling = true;
                    while (!Done) yield return null;
                    Rolling = false;
                    if (game != null) game.SetSlowMotion(false);
                }
                Ended?.Invoke();
            }
            if (game != null) game.SuppressInput = false;
            Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            QueenHillSkillFx.ViewOverride = null;
            Destroy(go);
            rig = null;
            Destroy(rt);
            Destroy(frame);
            Status = $"done {Frames} frames ??{path}";
        }

        static bool Done => QueenHillSkillProbe.Status.StartsWith("done");

        void SetCaption(string a, string b)
        {
            title.Destroy();
            note.Destroy();
            title = new PawnRushSkillFx.Text3D(cam.transform, font, a, Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(cam.transform, font, b, new Color(1f, 0.9f, 0.6f), NoteSize);
            titleFit = Fit(a, TitleSize);
            noteFit = Fit(b, NoteSize);
        }

        const float TitleSize = 0.05f, NoteSize = 0.03f, KeySize = 0.046f;

        /// <summary>The scale that keeps a caption line inside the picture (a long title shrinks, a short one stays).</summary>
        float Fit(string text, float size)
        {
            // Text3D: 64 px of the font = size metres. The picture is 2쨌tan(22째)쨌aspect wide where the captions hang.
            font.RequestCharactersInTexture(text, 64, FontStyle.Bold);
            float px = 0f;
            foreach (char c in text)
                if (font.GetCharacterInfo(c, out var info, 64, FontStyle.Bold)) px += info.advance;
            float wide = px * size / 64f, room = 2f * Mathf.Tan(22f * Mathf.Deg2Rad) * width / height * 0.92f;
            return wide > room ? room / wide : 1f;
        }

        static Transform Banner(Transform cam, float y, float width = 2f)
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
            };
            var go = new GameObject("Caption strip");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0f, y, 1.05f);
            go.transform.localScale = new Vector3(width, y > 0f ? 0.15f : 0.085f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.05f, 0.08f, 0.19f, 0.72f) };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            if (Rolling)
            {
                title.Place(cam.transform.TransformPoint(new Vector3(0f, 0.37f, depth)), cam, titleFit, 1f);
                note.Place(cam.transform.TransformPoint(new Vector3(0f, 0.313f, depth)), cam, noteFit, 1f);
                ShowKey();
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                frame.Apply();
                RenderTexture.active = null;
                Frame?.Invoke(frame);
                if (StillEvery > 0 && Frames % StillEvery == 0) File.WriteAllBytes(Path.Combine(stills, $"f_{Frames:D4}.jpg"), frame.EncodeToJPG(85));
                Frames++;
            }
            // Back to the shot's own pose: the effects shake it from there again next frame.
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
        }

        void ShowKey()
        {
            if (QueenHillSkillProbe.HintSerial != keySerial)
            {
                keySerial = QueenHillSkillProbe.HintSerial;
                key.Destroy();
                string hint = $"[ {QueenHillSkillProbe.Hint} ]";
                key = new PawnRushSkillFx.Text3D(cam.transform, font, hint, new Color(1f, 0.85f, 0.3f), KeySize);
                keyFit = Fit(hint, KeySize);
                keyLeft = 1.1f;
            }
            keyLeft -= Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            bool on = keyLeft > 0f;
            keyStrip.gameObject.SetActive(on);
            key.Place(cam.transform.TransformPoint(new Vector3(0f, -0.335f, depth)), cam, on ? keyFit : 0.001f, Mathf.Clamp01(keyLeft / 0.3f));
        }

        void OnDestroy()
        {
            Rolling = false;
            if (rig != null) Destroy(rig);
            rig = null;
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            if (QueenHillSkillFx.ViewOverride == cam) QueenHillSkillFx.ViewOverride = null;
        }
    }
}
