using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChessFight.Game;
using ChessFight.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ChessFight.PawnRush.Editor
{
    // R736: pictures of the lobby's 기물 도감 and 전적 tabs for the AI's own look (not a user check). Play in an empty
    // scene: the lobby's 3D stage (LobbyStage) and the lobby HUD (NetworkHudView) built as the lobby builds them, but
    // with no NetworkRuntime (no Steam: nothing is made on Steam), a made-up party and a made-up record book
    // (MatchRecords.Preview, nothing saved). The stage's camera draws into one texture, the HUD into another
    // (RuntimePanels.CaptureTarget, cleared see-through), and each picture lays the HUD over the stage.
    // Run on the project copy: Tools/Run-LobbyTabsCapture.ps1. Pictures and lobby_tabs.txt in Logs/LobbyTabsCapture.
    [InitializeOnLoad]
    public static class LobbyTabsCapture
    {
        const string ActiveKey = "ChessFight.R736.LobbyTabs.Capture";
        const string ExitKey = "ChessFight.R736.LobbyTabs.Exit";
        const string CodeKey = "ChessFight.R736.LobbyTabs.Code";
        const string Folder = "Logs/LobbyTabsCapture";
        const int Width = 1280, Height = 720, FirstHold = 90, HoldFrames = 25;
        const float RuntimeLimit = 300f;

        static LobbyStage stage;
        static NetworkHudView hud;
        static RenderTexture scene, ui;
        static int index, held;
        static double beganAt, playRequestedAt, editorIdleAt;
        static readonly List<string> errors = new List<string>();
        static readonly List<string> lines = new List<string>();

        // Each picture: its name and what to show first.
        static readonly (string name, Action show)[] Shots =
        {
            ("1_play", () => hud.SetTab(LobbyTab.Play)),
            ("2_codex_knight_white", () => { hud.SetTab(LobbyTab.Codex); hud.SelectCodex(PieceKind.Knight, 0); }),
            ("3_codex_king_black", () => hud.SelectCodex(PieceKind.King, 1)),
            ("4_codex_pawn_white", () => hud.SelectCodex(PieceKind.Pawn, 0)),
            ("5_record_all", () => hud.SetTab(LobbyTab.Record)),
            // R742: a line double-clicked: the newest (Pawn Rush, 2nd) and the fifth (Sword Fight 10 : 6).
            ("5b_record_detail_rush", () => hud.OpenRecordDetailAt(0)),
            ("5c_record_detail_sword", () => hud.OpenRecordDetailAt(4)),
            ("6_record_sword", () => hud.SelectRecordFilter(2)),
            // R743: the 순위 tab over made-up boards (Rankings.Preview). R745: the friends who are not on the board yet
            // ("12/20판"), this player's neighbours, the second page, profile pictures on some lines. R746: two boards -
            // Pawn Rush's and Sword Fight's, none over both: Pawn Rush's (this player 96th), its friends, its neighbours
            // and its second page, Sword Fight's (322nd), then Sword Fight short of its 20 matches (and its friends),
            // a player short of both, and no Steam.
            ("6b_rank_rush", () => { MatchRecords.Preview(RankExample(false)); PreviewBoards(true, true); hud.SetTab(LobbyTab.Rank); hud.SelectRanking(RankBoard.PawnRush, RankScope.Global); }),
            ("6c_rank_rush_friends", () => hud.SelectRanking(RankBoard.PawnRush, RankScope.Friends)),
            ("6g_rank_rush_around", () => hud.SelectRanking(RankBoard.PawnRush, RankScope.Around)),
            ("6h_rank_rush_page2", () => { hud.SelectRanking(RankBoard.PawnRush, RankScope.Global); hud.TurnRanking(1); }),
            ("6d_rank_sword", () => hud.SelectRanking(RankBoard.SwordFight, RankScope.Global)),
            ("6e_rank_sword_short", () => { MatchRecords.Preview(RankExample(true)); PreviewBoards(true, false); hud.SelectRanking(RankBoard.SwordFight, RankScope.Global); }),
            ("6i_rank_sword_short_friends", () => hud.SelectRanking(RankBoard.SwordFight, RankScope.Friends)),
            ("6j_rank_new", () => { MatchRecords.Preview(Example()); PreviewBoards(false, false); hud.SelectRanking(RankBoard.PawnRush, RankScope.Global); }),
            ("6f_rank_offline", () => { Rankings.Preview(null, null); hud.SelectRanking(RankBoard.PawnRush, RankScope.Global); }),
            ("7_record_empty", () => { MatchRecords.Preview(new RecordBook()); hud.SetTab(LobbyTab.Record); hud.SelectRecordFilter(0); }),
            // R738: the settings window (its own picture: it covers the lobby) with 게임 나가기, then its question.
            ("8_settings", () =>
            {
                RuntimePanels.CaptureTarget = settings;
                try { SettingsWindow.Open(); }
                finally { RuntimePanels.CaptureTarget = null; }
            }),
            ("9_settings_quit", SettingsWindow.AskQuitNow),
        };
        static readonly HashSet<string> SettingsShots = new HashSet<string> { "8_settings", "9_settings_quit" };
        static RenderTexture settings;

        static LobbyTabsCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(ExitKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += CompleteExit;
        }

        [MenuItem("ChessFight/Development/Lobby Tabs Capture (Play)", false, 300)]
        public static void RunMenu() => RunPlayBatch();

        public static void RunPlayBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetInt(CodeKey, 1);
            playRequestedAt = editorIdleAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= BeginPlayWhenIdle;
            EditorApplication.update += BeginPlayWhenIdle;
        }

        static void BeginPlayWhenIdle()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - playRequestedAt > 90f)
            {
                EditorApplication.update -= BeginPlayWhenIdle;
                SessionState.SetBool(ActiveKey, false);
                Debug.LogError("[LobbyTabsCapture] FAIL: Editor startup did not settle before Play Mode");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { editorIdleAt = now; return; }
            if (now - editorIdleAt < 2f) return;
            EditorApplication.update -= BeginPlayWhenIdle;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false)) Begin();
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ExitKey, false)) CompleteExit();
        }

        static void Begin()
        {
            errors.Clear();
            lines.Clear();
            index = held = 0;
            beganAt = EditorApplication.timeSinceStartup;
            Directory.CreateDirectory(Folder);
            Application.logMessageReceived += OnLog;
            lines.Add("R736 lobby tabs capture: the lobby stage and HUD with no Steam, a made-up party and record book.");
            // R749: `-captureLanguage zh-Hans` (Run-LobbyTabsCapture.ps1 -Language) shows the pictures in that language.
            // Loc.Use only, never GameLanguage.Pick: nothing goes into PlayerPrefs, which this PC's open Editor shares.
            string language = CaptureLanguage();
            if (language.Length > 0)
            {
                if (Loc.Use(language)) lines.Add("language " + Loc.Lang);
                else errors.Add("the string table has no column '" + language + "'");
            }
            try
            {
                MatchRecords.Preview(Example());

                stage = new GameObject("Lobby Stage").AddComponent<LobbyStage>();
                stage.Build();
                scene = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "Lobby capture (stage)" };
                scene.Create();
                if (stage.View != null) stage.View.targetTexture = scene;

                ui = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "Lobby capture (HUD)" };
                ui.Create();
                settings = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "Lobby capture (settings)" };
                settings.Create();
                // R738: the menu sounds are there and play (MenuSound; nothing can be heard in batch mode).
                foreach (MenuSound.Kind kind in Enum.GetValues(typeof(MenuSound.Kind)))
                {
                    if (kind == MenuSound.Kind.None) continue;
                    var clip = Resources.Load<AudioClip>("UiSound/Menu" + kind);
                    if (clip == null) errors.Add("missing sound Resources/UiSound/Menu" + kind);
                    else lines.Add($"sound Menu{kind}: {clip.length:0.000} s, {clip.frequency} Hz, {clip.channels} ch");
                    MenuSound.Play(kind);
                }
                RuntimePanels.CaptureTarget = ui;
                var host = new GameObject("Lobby HUD");
                try
                {
                    hud = host.AddComponent<NetworkHudView>();
                    hud.Build(Resources.Load<VisualTreeAsset>("NetworkHud"), Resources.Load<ThemeStyleSheet>("NetworkTheme"), null, new Vector2Int(Width, Height));
                }
                finally { RuntimePanels.CaptureTarget = null; }
                var document = host.GetComponent<UIDocument>();
                if (document != null && document.panelSettings != null) document.panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);

                hud.TabChanged += tab => stage.Showcase(tab != LobbyTab.Play, tab == LobbyTab.Codex ? hud.CodexKind : (PieceKind?)null, hud.CodexTeam);
                hud.CodexPiece += (kind, team) => { if (hud.Tab == LobbyTab.Codex) stage.Showcase(true, kind, team); };
                hud.CodexSpin += degrees => stage.SpinShowcase(degrees);

                var party = new List<LineupEntry>
                {
                    new LineupEntry { Id = 11, Name = "나이트메어", Tag = "파티장 · 2/6", Leader = true, Me = true },
                    new LineupEntry { Id = 12, Name = "龙骑士", Tag = "대기 중" },
                    new LineupEntry { Invite = true },
                };
                stage.Show(party);
                hud.SetLineup(party, stage.View);
                hud.Render(new HudModel
                {
                    Online = true, PlayerName = "나이트메어", FriendsOnline = 3, Version = "capture",
                    ModeKey = GameModes.KingRush.Key, CanChangeMode = true, PartySize = 2, PartyCode = "123456", Bots = "0",
                    CanPlay = true, CanCreateTest = true, CanInvite = true, CanJoinParty = true, CanLeaveParty = true, CanCopyParty = true,
                });
            }
            catch (Exception e)
            {
                errors.Add(e.ToString());
                Finish(1);
                return;
            }
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static string CaptureLanguage()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-captureLanguage") return args[i + 1].Trim();
            return "";
        }

        // A made-up book: the mock-up's recent matches, newest first.
        static RecordBook Example()
        {
            var book = new RecordBook();
            double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string pr = GameModes.KingRush.Key, sf = GameModes.SwordFight.Key;
            var list = new List<MatchRecord>
            {
                new MatchRecord { Mode = pr, Result = RecordResult.Loss, Piece = PieceKind.Knight, Ours = 6, Theirs = 6, MetersLeft = 12 },
                new MatchRecord { Mode = sf, Result = RecordResult.Loss, Piece = PieceKind.Knight, Ours = 3, Theirs = 3, ScoreOurs = 5, ScoreTheirs = 10, Knockouts = 1, Falls = 4 },
                new MatchRecord { Mode = sf, Result = RecordResult.Win, Piece = PieceKind.Rook, Ours = 2, Theirs = 2, ScoreOurs = 10, ScoreTheirs = 4, Knockouts = 6, Falls = 1 },
                new MatchRecord { Mode = sf, Result = RecordResult.Win, Piece = PieceKind.Bishop, Ours = 6, Theirs = 6, ScoreOurs = 10, ScoreTheirs = 8, Knockouts = 3, Falls = 2 },
                RecordBuild.Left(pr, PieceKind.Pawn, 6, 6),
                new MatchRecord { Mode = sf, Result = RecordResult.Loss, Piece = PieceKind.Queen, Ours = 6, Theirs = 6, ScoreOurs = 8, ScoreTheirs = 9, Knockouts = 1, Falls = 2, End = SwordEnd.TimeUp },
                new MatchRecord { Mode = pr, Result = RecordResult.Win, Piece = PieceKind.Queen, Ours = 6, Theirs = 6, Place = 1, FinishSeconds = 238, CountsToWin = true },
                new MatchRecord { Mode = sf, Result = RecordResult.Win, Piece = PieceKind.Knight, Ours = 4, Theirs = 4, ScoreOurs = 10, ScoreTheirs = 6, Knockouts = 4, Falls = 1, Detail = SwordExample() },
                new MatchRecord { Mode = pr, Result = RecordResult.Loss, Piece = PieceKind.Bishop, Ours = 5, Theirs = 5, MetersLeft = 41 },
                new MatchRecord { Mode = pr, Result = RecordResult.Win, Piece = PieceKind.Rook, Ours = 6, Theirs = 6, Place = 5, FinishSeconds = 302, CountsToWin = true },
                new MatchRecord { Mode = sf, Result = RecordResult.Loss, Piece = PieceKind.Rook, Ours = 3, Theirs = 3, ScoreOurs = 7, ScoreTheirs = 10, Knockouts = 2, Falls = 3 },
                new MatchRecord { Mode = pr, Result = RecordResult.Win, Piece = PieceKind.Knight, Ours = 6, Theirs = 6, Place = 2, FinishSeconds = 261, CountsToWin = true, Detail = RushExample() },
            };
            // Oldest first into the book, an hour and a bit apart.
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                r.Match = (ulong)(1000 + i);
                r.When = now - (list.Count - i) * 4000;
                // R745: six of them were ranked matches (matchmaking, six people a side): the list marks those.
                r.Ranked = i == 0 || (i >= 3 && i <= 6) || i == 9;
                book.Add(r);
            }
            return book;
        }

        // R743: the book for the 순위 pictures: older matches before the example's twelve. R745: only ranked matches
        // count, so the older ones are all ranked ones (and six of the example's are): Pawn Rush 38 ranked with 21
        // wins, Sword Fight 24 with 12 - or, `swordShort`, 8 with 4 (short of the 20 its board asks; Pawn Rush's board
        // has this player all the same, R746).
        static RecordBook RankExample(bool swordShort)
        {
            var book = new RecordBook();
            double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int rush = 34, sword = swordShort ? 6 : 22;
            for (int i = 0; i < rush + sword; i++)
            {
                bool sf = i >= rush;
                int k = sf ? i - rush : i;
                bool win = sf ? k % 2 == 0 : k < 19;
                book.Add(new MatchRecord
                {
                    Match = (ulong)(2000 + i), Mode = sf ? GameModes.SwordFight.Key : GameModes.KingRush.Key,
                    Result = win ? RecordResult.Win : RecordResult.Loss, Piece = sf || k % 3 != 0 ? PieceKind.Knight : PieceKind.Rook,
                    Ours = 6, Theirs = 6, Place = !sf && win ? 3 : 0, FinishSeconds = !sf && win ? 280 : -1, CountsToWin = !sf && win,
                    When = now - 86400 * 2 - (rush + sword - i) * 3600, Ranked = true,
                });
            }
            foreach (var r in Enumerable.Reverse(Example().Recent)) book.Add(r);
            return book;
        }

        // A made-up player's Steam ID: the same for a name on every board.
        static ulong Pid(string name)
        {
            uint hash = 2166136261;
            foreach (char c in name) hash = (hash ^ c) * 16777619;
            return 76561190000000000UL + hash;
        }

        static readonly Dictionary<ulong, Texture2D> faces = new Dictionary<ulong, Texture2D>();

        // A made-up profile picture for some of the players (two colours of their own), so both looks are in the
        // pictures: a picture, and the coloured initial of a player whose picture has not come.
        static Texture2D Face(ulong id)
        {
            if (id % 5 >= 3) return null;
            if (faces.TryGetValue(id, out var kept) && kept != null) return kept;
            const int size = 48;
            var a = Color.HSVToRGB((id * 2654435761UL % 360UL) / 360f, .55f, .85f);
            var b = Color.HSVToRGB(((id * 2654435761UL + 140UL) % 360UL) / 360f, .5f, .45f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - size * .5f, dy = y - size * .42f;
                    bool head = dx * dx + dy * dy < size * size * .045f, body = y < size * .3f && Mathf.Abs(dx) < size * (.34f - y * .004f);
                    pixels[y * size + x] = head || body ? b : a;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Capture face " + id, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels(pixels);
            texture.Apply();
            faces[id] = texture;
            return texture;
        }

        // R743: made-up boards, the mock-up's (https://claude.ai/artifact/6NgDdWLAxx2WaZLduqD2Cb); this player's own
        // lines as the book above has them. R745: a second page, this player's neighbours, and the friends' progress
        // lines (every friend who has played) that put the ones who are not on a board under it. R746: two boards,
        // Pawn Rush's and Sword Fight's - none over both. `rushOn` / `swordOn`: this player is on that board.
        static void PreviewBoards(bool rushOn, bool swordOn)
        {
            int today = Rankings.Today;
            RankEntry E(int rank, string name, PieceKind piece, int games, int wins, int days, bool friend = false, bool self = false) =>
                new RankEntry { Id = Pid(name), Rank = rank, Name = name, Piece = piece, Games = games, Wins = wins,
                                LastDay = today - days, Friend = friend, Self = self };
            const PieceKind P = PieceKind.Pawn, R = PieceKind.Rook, N = PieceKind.Knight, B = PieceKind.Bishop, Q = PieceKind.Queen, K = PieceKind.King;
            const string me = "나이트메어";

            var rush = new Rankings.Board { Status = Rankings.Status.Ready, Total = 686 };
            rush.Top.AddRange(new[]
            {
                E(1, "그랜드마스터", K, 75, 54, 0), E(2, "체크메이트", Q, 50, 35, 0), E(3, "앙파상", P, 24, 16, 35),
                E(4, "프로모션", P, 21, 14, 1), E(5, "룩마스터", R, 29, 19, 1), E(6, "퀸사이드", Q, 40, 25, 0, true),
                E(7, "폰스타", P, 28, 17, 0, true), E(8, "비숍킹", B, 42, 25, 2), E(9, "스테일메이트", B, 24, 14, 3), E(10, "시실리안", N, 40, 23, 0),
            });
            rush.Pages[1] = new List<RankEntry>
            {
                E(11, "캐슬링", R, 21, 12, 41, true), E(12, "폰드림", P, 44, 25, 0), E(13, "검은기사", N, 30, 17, 52), E(14, "룩앤롤", R, 53, 30, 1),
                E(15, "캐슬러", R, 23, 13, 4), E(16, "미들게임", R, 39, 22, 0), E(17, "나이트워치", N, 48, 27, 2), E(18, "다크비숍", B, 32, 18, 9),
                E(19, "퀸즈갬빗", Q, 41, 23, 0), E(20, "츠크츠방", K, 25, 14, 3),
            };
            rush.Friends.AddRange(new[] { E(6, "퀸사이드", Q, 40, 25, 0, true), E(7, "폰스타", P, 28, 17, 0, true), E(11, "캐슬링", R, 21, 12, 41, true) });
            if (rushOn)
            {
                rush.Mine = E(96, me, N, 38, 21, 0, self: true);
                rush.Around.AddRange(new[]
                {
                    E(93, "백랭크", Q, 45, 25, 9), E(94, "오픈파일", R, 27, 15, 0), E(95, "핀과포크", N, 47, 26, 2), rush.Mine,
                    E(97, "화이트폰", P, 20, 11, 6), E(98, "더블체크", B, 31, 17, 0), E(99, "엔드게임", K, 24, 13, 1),
                });
                rush.Friends.Add(rush.Mine);
            }
            rush.Friends.Add(E(402, "정원사", B, 23, 11, 2, true));

            var sword = new Rankings.Board { Status = Rankings.Status.Ready, Total = 669 };
            sword.Top.AddRange(new[]
            {
                E(1, "체크메이트", Q, 34, 25, 0), E(2, "룩앤롤", R, 23, 16, 1), E(3, "퀸사이드", Q, 28, 18, 0, true),
                E(4, "그랜드마스터", K, 50, 31, 0), E(5, "캐슬링", R, 20, 12, 41, true), E(6, "비숍킹", B, 28, 16, 2),
                E(7, "검은기사", N, 25, 14, 52), E(8, "폰드림", P, 40, 22, 0), E(9, "캐슬러", R, 22, 12, 4), E(10, "시실리안", N, 21, 11, 0),
            });
            sword.Friends.AddRange(new[] { E(3, "퀸사이드", Q, 28, 18, 0, true), E(5, "캐슬링", R, 20, 12, 41, true) });
            if (swordOn)
            {
                sword.Mine = E(322, me, N, 24, 12, 0, self: true);
                sword.Around.AddRange(new[]
                {
                    E(319, "미들게임", R, 35, 18, 0), E(320, "다크비숍", B, 40, 20, 31), E(321, "퀸즈갬빗", Q, 26, 13, 0), sword.Mine,
                    E(323, "나이트워치", N, 22, 11, 2), E(324, "핀과포크", N, 20, 10, 2), E(325, "츠크츠방", K, 27, 13, 3),
                });
                sword.Friends.Add(sword.Mine);
            }
            sword.Friends.Add(E(377, "정원사", B, 22, 10, 2, true));

            // Every friend who has played, as the progress board has them: their ranked matches of each mode.
            RankProgress F(string name, PieceKind piece, int rushGames, int rushWins, int swordGames, int swordWins, int days)
            {
                double last = (today - days) * 86400.0;
                var p = new RankProgress { Id = Pid(name), Name = name, Friend = true, RushPiece = piece, SwordPiece = piece };
                p.Rush = new ModeTotals { Games = rushGames, Wins = rushWins, Losses = rushGames - rushWins, RankedGames = rushGames, RankedWins = rushWins, Last = last, RankedLast = rushGames > 0 ? last : 0 };
                p.Sword = new ModeTotals { Games = swordGames, Wins = swordWins, Losses = swordGames - swordWins, RankedGames = swordGames, RankedWins = swordWins, Last = last, RankedLast = swordGames > 0 ? last : 0 };
                p.PieceGames[(int)piece] = rushGames + swordGames;
                return p;
            }
            var friends = new List<RankProgress>
            {
                F("퀸사이드", Q, 40, 25, 28, 18, 0), F("캐슬링", R, 21, 12, 20, 12, 41), F("정원사", B, 23, 11, 22, 10, 2),
                F("폰스타", P, 28, 17, 18, 9, 0), F("비숍러버", B, 12, 7, 3, 1, 1), F("오프닝", N, 5, 2, 0, 0, 12),
            };
            Rankings.Preview(rush, sword, friends, Face);
        }

        // R742: made-up details, as the mock-up's (https://claude.ai/artifact/Hss71hGh9SqW3vXe27er85).
        static MatchDetail RushExample()
        {
            var d = new MatchDetail { Duration = 298, Clincher = "캐슬링", Checkpoints = 11, Skills = 7, Dives = 12, Grabs = 3 };
            d.Enter("왕실 수로 정원", 0); d.Fall();
            d.Enter("해자 도개교", 68); d.Promote(PieceKind.Knight, 112);
            d.Enter("대계단 현관홀", 140); d.Fall(); d.Fall();
            d.Enter("국왕 체스 홀", 198);
            d.Enter("국왕 작전실", 236);
            d.Enter("국왕 알현실 (임시)", 254);
            void P(string name, bool ours, PieceKind piece, int place, double secs, bool self = false) =>
                d.Add(new DetailPlayer { Name = name, Ours = ours, Piece = piece, Place = place, Seconds = secs, Self = self });
            P("체크메이트", true, PieceKind.Queen, 1, 242); P("나이트메어", true, PieceKind.Knight, 2, 261, true); P("퀸사이드", true, PieceKind.Rook, 3, 275);
            P("캐슬링", true, PieceKind.Bishop, 5, 298); P("앙파상", true, PieceKind.Pawn, 0, -1); P("BOT 1", true, PieceKind.Pawn, 0, -1);
            P("룩마스터", false, PieceKind.Rook, 4, 290); P("비숍킹", false, PieceKind.Knight, 0, -1); P("스테일메이트", false, PieceKind.Queen, 0, -1);
            P("폰스타", false, PieceKind.Pawn, 0, -1); P("프로모션", false, PieceKind.Rook, 0, -1); P("BOT 2", false, PieceKind.Pawn, 0, -1);
            return d;
        }

        static MatchDetail SwordExample()
        {
            var d = new MatchDetail { Duration = 192, SwordF = 6, SwordE = 1 };
            var them = new[] { "룩마스터", "비숍킹", "스테일메이트", "폰스타" };
            double[] ours = { 25, 38, 52, 65, 88, 104, 140, 150, 178, 192 }, theirs = { 30, 75, 107, 120, 160, 170 };
            var mine = new Dictionary<double, (string victim, string cause)>
                { { 38, ("룩마스터", "칼") }, { 65, ("폰스타", "포크 강하") }, { 140, ("스테일메이트", "벼랑 끝 역전") }, { 178, ("룩마스터", "칼") } };
            var all = ours.Select(t => (t, true)).Concat(theirs.Select(t => (t, false))).OrderBy(x => x.Item1);
            int n = 0;
            foreach (var (t, us) in all)
            {
                n++;
                if (us && mine.TryGetValue(t, out var m)) d.Score(new DetailFall { At = t, Ours = true, ByMe = true, Victim = m.victim, Credit = "나이트메어", Cause = m.cause });
                else if (!us && t == 107) d.Score(new DetailFall { At = t, Ours = false, OfMe = true, Victim = "나이트메어", Credit = "비숍킹", Cause = "포크 강하" });
                else d.Score(new DetailFall { At = t, Ours = us, Victim = us ? them[n % 4] : "퀸사이드", Credit = us ? "퀸사이드" : them[n % 4], Cause = "칼" });
            }
            void P(string name, bool o, PieceKind piece, int ko, int falls, bool self = false) =>
                d.Add(new DetailPlayer { Name = name, Ours = o, Piece = piece, Knockouts = ko, Falls = falls, Self = self });
            P("나이트메어", true, PieceKind.Knight, 4, 1, true); P("퀸사이드", true, PieceKind.Queen, 3, 2); P("캐슬링", true, PieceKind.Rook, 2, 2); P("BOT 1", true, PieceKind.Bishop, 1, 1);
            P("룩마스터", false, PieceKind.Rook, 2, 3); P("비숍킹", false, PieceKind.Knight, 2, 2); P("스테일메이트", false, PieceKind.Queen, 1, 3); P("폰스타", false, PieceKind.Rook, 1, 2);
            return d;
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (condition != null && condition.StartsWith("Host type is not matching any asset type") && condition.Contains(".urtshader")) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) errors.Add(condition + "\n" + stack);
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || hud == null) return;
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            try
            {
                if (EditorApplication.timeSinceStartup - beganAt > RuntimeLimit) throw new InvalidOperationException("capture took too long");
                if (index >= Shots.Length) { Finish(errors.Count == 0 ? 0 : 1); return; }
                if (held == 0) Shots[index].show();
                if (++held < (index == 0 ? FirstHold : HoldFrames)) return;
                held = 0;
                Save(Shots[index].name);
                index++;
            }
            catch (Exception e)
            {
                errors.Add(e.ToString());
                Finish(1);
            }
        }

        static Color[] Read(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var picture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            picture.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            picture.Apply();
            RenderTexture.active = previous;
            var pixels = picture.GetPixels();
            Object.DestroyImmediate(picture);
            return pixels;
        }

        // The HUD (drawn see-through, its colour already weighed by its cover) over the stage.
        static void Save(string name)
        {
            if (SettingsShots.Contains(name))
            {
                var shot = Read(settings);
                for (int i = 0; i < shot.Length; i++) shot[i].a = 1f;
                var own = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                own.SetPixels(shot);
                own.Apply();
                string at = Path.Combine(Folder, name + ".png");
                File.WriteAllBytes(at, own.EncodeToPNG());
                Object.DestroyImmediate(own);
                lines.Add("picture " + at);
                return;
            }
            if (stage.View != null) stage.View.Render();
            var back = Read(scene);
            var front = Read(ui);
            for (int i = 0; i < back.Length; i++)
            {
                var f = front[i];
                float a = Mathf.Clamp01(f.a);
                back[i] = new Color(Mathf.Clamp01(f.r + back[i].r * (1f - a)), Mathf.Clamp01(f.g + back[i].g * (1f - a)), Mathf.Clamp01(f.b + back[i].b * (1f - a)), 1f);
            }
            var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            output.SetPixels(back);
            output.Apply();
            string path = Path.Combine(Folder, name + ".png");
            File.WriteAllBytes(path, output.EncodeToPNG());
            Object.DestroyImmediate(output);
            lines.Add("picture " + path);
        }

        static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            foreach (var e in errors) lines.Add("ERROR " + e);
            lines.Add(code == 0 ? "PASS: pictures taken, no errors logged" : "FAIL: see the errors above");
            try { File.WriteAllLines(Path.Combine(Folder, "lobby_tabs.txt"), lines); } catch (Exception) { }
            SettingsWindow.Close();
            foreach (var t in new[] { scene, ui, settings }) if (t != null) t.Release();
            scene = ui = settings = null;
            hud = null;
            stage = null;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(CodeKey, code);
            SessionState.SetBool(ExitKey, true);
            EditorApplication.isPlaying = false;
        }

        static void CompleteExit()
        {
            SessionState.SetBool(ExitKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(CodeKey, 1));
        }
    }
}
