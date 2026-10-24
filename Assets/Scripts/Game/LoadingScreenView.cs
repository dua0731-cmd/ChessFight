using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // What the entrance screen shows this frame. Whoever runs matchmaking and the
    // load fills it.
    public sealed class LoadingState
    {
        // Matching: still finding players (no loading yet); Clock is the time
        // searched, shown where the percentage goes, and the line fills with players.
        public bool Matching, CanCancel;
        // "봇 추가" while matching: shown to whoever may fill the room (ShowAddBot),
        // clickable once there is a room with a free seat (CanAddBot).
        public bool ShowAddBot, CanAddBot;
        // "게임 시작" while matching (R148, 폰 러쉬): the room's host may start once each side has three or more
        // and the sides are even; StartNote says why it cannot yet.
        public bool ShowStart, CanStart;
        public string StartNote = "";
        public string Step = "", Clock = "";
        public int Percent;
        // Players the match will hold (12 for a public match): one card per seat,
        // empty ones "찾는 중". 0 shows only the players listed.
        public int Expected;
        // One entry per player, roster order sorted by team: their team (0 white,
        // 1 black), whether they are in (walk onto the stage), whether their match
        // scene has finished loading, their name, whether they are a bot, and which
        // entry is this PC's player (-1: none).
        public readonly List<int> Teams = new List<int>();
        public readonly List<bool> Joined = new List<bool>();
        public readonly List<bool> Ready = new List<bool>();
        public readonly List<string> Names = new List<string>();
        public readonly List<bool> Bots = new List<bool>();
        public int Me = -1;

        public void Clear()
        {
            Teams.Clear(); Joined.Clear(); Ready.Clear(); Names.Clear(); Bots.Clear();
            Me = -1;
        }
    }

    // The entrance screen (Docs/Architecture/UI.md §11), design C revised
    // (2026-10-03): from "게임 시작" on a public match until the match starts. The
    // mode's name top centre, white's seats down the left and black's down the
    // right, and behind them LoadingStudio's wooden stage. While matching, each
    // player walks on as they join (their card lights up, "입장!") and the line
    // fills with players; a click on "매칭 취소" calls it off. When the match starts
    // the same screen carries on as the loading screen: the line shows this PC's
    // load and each card says when that player is ready. With everyone in and
    // ready, "모두 입장 완료 · 곧 출발!" and the whole stage cheers.
    //
    // It swallows clicks meant for the lobby under it. It lives on the persistent
    // runtime object, so it survives the scene switch it hides; Hide() fades it
    // out and removes it.
    [DisallowMultipleComponent]
    public sealed class LoadingScreenView : MonoBehaviour
    {
        // Above every scene's own HUD panel (those keep the default order, 0).
        const float SortingOrder = 100f;
        const float TipSeconds = 4.5f, FadeSeconds = .35f, CardEnter = .7f, ArrivedSeconds = 1f;
        // Tip text is broken at spaces by hand: UI Toolkit breaks Korean between
        // any two syllables. A Hangul syllable counts two units (see NetworkHudView).
        const int TipLineUnits = 120;

        public readonly LoadingState State = new LoadingState();
        public bool Hiding => hideAt >= 0;
        // True while an entrance screen covers the view, fading out included: what
        // sits on the match underneath (the skill card) waits until it is gone.
        public static bool Showing => open > 0;
        static int open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOpen() => open = 0;

        void Awake() => open++;
        // "매칭 취소" clicked while matching (R64: not Esc, which is the settings window).
        public event Action CancelRequested;
        // "봇 추가" while matching: one more bot in the room.
        public event Action AddBotRequested;
        // "게임 시작" while matching (R148).
        public event Action StartRequested;

        sealed class Card
        {
            public VisualElement Root, Avatar, Flash;
            public Label Name, Sub, Status;
            public int Team, Spot;
            public float Side, OnAt = -1;
            public bool Present, Bot;
        }

        PanelSettings ownedPanel;
        Texture2D shade;
        readonly LoadingStudio studio = new LoadingStudio();
        readonly List<Texture2D> washes = new List<Texture2D>();
        VisualElement screen, fill, allIn, ticks;
        Button cancel, addBot, start;
        Label startNote;
        readonly VisualElement[] rosters = new VisualElement[2];
        Label kicker, step, percent, tipTitle, tipText;
        readonly Label[] counts = new Label[2];
        readonly List<Card>[] cards = { new List<Card>(), new List<Card>() };
        LoadingTip[] tips = Array.Empty<LoadingTip>();
        string kickerText = "";
        int tipIndex = -1;
        float shownAt, hideAt = -1;

        public void Build(LoadingContent content)
        {
            var root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("LoadingHud"),
                                            Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                            new Vector2Int(1280, 720), out ownedPanel);
            if (root == null) return;
            if (ownedPanel != null) ownedPanel.sortingOrder = SortingOrder;
            screen = root.Q<VisualElement>("loading");

            int width = Mathf.Clamp(Screen.width, 960, 1920);
            int height = Mathf.Max(360, Mathf.RoundToInt(width * (float)Screen.height / Mathf.Max(1, Screen.width)));
            studio.Build(transform, width, height);
            var stage = root.Q<VisualElement>("load-stage");
            if (stage != null && studio.Texture != null) stage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(studio.Texture));
            Wash(root.Q<VisualElement>("load-side-white"), MenuArt.Hex(0x1E78BE), false);
            Wash(root.Q<VisualElement>("load-side-black"), MenuArt.Hex(0xC8283C), true);
            var shadeLayer = root.Q<VisualElement>("load-shade");
            if (shadeLayer != null)
            {
                shade = MenuMarks.Vignette(new Color(8 / 255f, 4 / 255f, 2 / 255f), .55f, .8f);
                shadeLayer.style.backgroundImage = new StyleBackground(shade);
            }

            kicker = root.Q<Label>("load-kicker");
            rosters[0] = root.Q<VisualElement>("load-roster-white");
            rosters[1] = root.Q<VisualElement>("load-roster-black");
            counts[0] = root.Q<Label>("load-count-white");
            counts[1] = root.Q<Label>("load-count-black");
            allIn = root.Q<VisualElement>("load-allin");
            step = root.Q<Label>("load-step");
            percent = root.Q<Label>("load-percent");
            fill = root.Q<VisualElement>("load-fill");
            if (fill != null) fill.style.backgroundImage = new StyleBackground(MenuArt.Line(MenuArt.Hex(MenuArt.Cyan), MenuArt.Hex(MenuArt.Gold), MenuArt.Hex(MenuArt.Red)));
            ticks = Ticks(root.Q<VisualElement>("load-line"));
            BuildSquares(root.Q<VisualElement>("load-line"));
            cancel = root.Q<Button>("load-cancel");
            if (cancel != null)
            {
                cancel.focusable = false;
                cancel.clicked += () => CancelRequested?.Invoke();
            }
            addBot = root.Q<Button>("load-add-bot");
            if (addBot != null)
            {
                addBot.focusable = false;
                addBot.clicked += () => AddBotRequested?.Invoke();
            }
            start = root.Q<Button>("load-start");
            if (start != null)
            {
                start.focusable = false;
                start.clicked += () => { if (State.CanStart) StartRequested?.Invoke(); };
            }
            startNote = root.Q<Label>("load-start-note");
            ChunkyButtons.Attach(root);

            tipTitle = root.Q<Label>("load-tip-kicker");
            tipText = root.Q<Label>("load-tip-text");
            SetContent(content, root);
            shownAt = Time.unscaledTime;
            Update();
        }

        // The mode's words: title, rule, note and tips (again when the match the
        // search found differs, or a late player joins a running match).
        public void SetContent(LoadingContent content) => SetContent(content, screen);

        void SetContent(LoadingContent content, VisualElement root)
        {
            if (root == null || content == null) return;
            kickerText = content.Kicker ?? "";
            SetText(root.Q<Label>("load-title"), content.Title);
            SetText(root.Q<Label>("load-title-hi"), content.Title);
            SetText(root.Q<Label>("load-rule"), content.Rule);
            SetText(root.Q<Label>("load-note"), content.Note);
            tips = content.Tips ?? Array.Empty<LoadingTip>();
            tipIndex = -1;
            var tipRow = root.Q<VisualElement>("load-tip");
            if (tipRow != null) tipRow.style.display = tips.Length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // Starts the fade; the object removes itself when it is done. Clicks go
        // through to the game from the first faded frame.
        public void Hide()
        {
            if (Hiding) return;
            hideAt = Time.unscaledTime;
            if (screen == null) return;
            screen.pickingMode = PickingMode.Ignore;
            screen.AddToClassList("loading-out");
        }

        // 10-18 (승규 님, loading design 2): instead of the thin line, a row of twelve chess squares, ivory and
        // walnut, that turn gold one by one as players come in (or as this PC loads), with a gold knight that
        // hops on the next square and leaps ahead when it fills. The line stays in the layout, hidden, so
        // the row takes its place.
        const int SquareCount = 12;
        readonly List<VisualElement> squares = new List<VisualElement>();
        VisualElement knight;
        float knightAt = -1f, knightFrom, knightLeap = -10f;
        int squaresLit = -1, popIndex = -1;
        float popAt = -10f;
        static Texture2D knightTexture;

        void BuildSquares(VisualElement line)
        {
            if (line == null || line.parent == null) return;
            line.style.display = DisplayStyle.None;
            var row = new VisualElement { name = "load-squares", pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = 22;
            row.style.marginTop = 2;
            line.parent.Add(row);
            for (int i = 0; i < SquareCount; i++)
            {
                var sq = new VisualElement { pickingMode = PickingMode.Ignore };
                sq.style.flexGrow = 1;
                sq.style.marginLeft = sq.style.marginRight = 2;
                sq.style.backgroundColor = i % 2 == 0 ? new Color(.93f, .87f, .76f, .85f) : new Color(.35f, .23f, .14f, .85f);
                sq.style.borderTopWidth = sq.style.borderBottomWidth = sq.style.borderLeftWidth = sq.style.borderRightWidth = 1;
                var rim = new Color(.79f, .6f, .27f, .7f);
                sq.style.borderTopColor = sq.style.borderBottomColor = sq.style.borderLeftColor = sq.style.borderRightColor = rim;
                sq.style.transitionProperty = new List<StylePropertyName> { "background-color", "scale" };
                sq.style.transitionDuration = new List<TimeValue> { new TimeValue(.25f), new TimeValue(.25f) };
                sq.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOut), new EasingFunction(EasingMode.EaseOutBack) };
                row.Add(sq);
                squares.Add(sq);
            }
            knight = new VisualElement { pickingMode = PickingMode.Ignore };
            knight.style.position = Position.Absolute;
            knight.style.width = 40; knight.style.height = 40;
            knight.style.bottom = 20;
            knight.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            var tex = KnightTexture();
            if (tex != null) knight.style.backgroundImage = new StyleBackground(tex);
            row.Add(knight);
        }

        void TickSquares(float lit, float now)
        {
            if (squares.Count == 0) return;
            int n = Mathf.Clamp(Mathf.FloorToInt(lit + .001f), 0, SquareCount);
            if (n != squaresLit)
            {
                for (int i = 0; i < squares.Count; i++)
                {
                    bool on = i < n;
                    squares[i].style.backgroundColor = on ? new Color(.91f, .77f, .42f, 1f) : (i % 2 == 0 ? new Color(.93f, .87f, .76f, .85f) : new Color(.35f, .23f, .14f, .85f));
                    squares[i].style.scale = new Scale(Vector2.one);
                }
                if (squaresLit >= 0 && n > squaresLit)
                {
                    // the square just won pops up, then settles (below)
                    knightFrom = knightAt; knightLeap = now;
                    popIndex = n - 1; popAt = now;
                    squares[popIndex].style.scale = new Scale(new Vector2(1.08f, 1.3f));
                }
                squaresLit = n;
            }
            if (popIndex >= 0 && now - popAt > .22f) { squares[popIndex].style.scale = new Scale(Vector2.one); popIndex = -1; }
            if (knight == null) return;
            // The knight stands on the next square (the last one once all are gold); it leaps there in an arc.
            float target = Mathf.Min(n, SquareCount - 1) + .5f;
            if (knightAt < 0) knightAt = knightFrom = target;
            float k = Mathf.Clamp01((now - knightLeap) / .45f);
            float x = k < 1f ? Mathf.Lerp(knightFrom, target, k * k * (3 - 2 * k)) : target;
            knightAt = x;
            float hop = k < 1f ? Mathf.Sin(k * Mathf.PI) * 26f : Mathf.Abs(Mathf.Sin(now * 4.2f)) * 7f;
            knight.style.left = new Length(x / SquareCount * 100f, LengthUnit.Percent);
            knight.style.bottom = 20 + hop;
        }

        // A gold knight with a dark rim, from the symbol font (as the title screen's pennant glyphs).
        static Texture2D KnightTexture()
        {
            if (knightTexture != null) return knightTexture;
            const int n = 64;
            var mask = IntroStage.GlyphMask("♞", n, n);
            if (mask == null) return null;
            var px = new Color[n * n];
            Color gold = new Color(.95f, .8f, .4f, 1f), rim = new Color(.16f, .1f, .06f, 1f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float m = mask[y * n + x];
                    float near = 0f;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && xx < n && yy >= 0 && yy < n) near = Mathf.Max(near, mask[yy * n + xx]);
                        }
                    var c = Color.Lerp(rim, Color.Lerp(gold, Color.white, (y / (float)n) * .25f), m);
                    c.a = Mathf.Max(m, near);
                    px[y * n + x] = c;
                }
            knightTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Loading knight", wrapMode = TextureWrapMode.Clamp };
            knightTexture.SetPixels(px); knightTexture.Apply();
            return knightTexture;
        }

        // A tick on the line for each rank a pawn passes, numbered 2 to 8 (loading only).
        static VisualElement Ticks(VisualElement line)
        {
            if (line == null) return null;
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("load-ticks");
            for (int i = 1; i < 8; i++)
            {
                var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                tick.AddToClassList("load-tick");
                tick.style.left = new Length(i / 8f * 100f, LengthUnit.Percent);
                var label = new Label((i + 1).ToString()) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("load-tick-label");
                tick.Add(label);
                layer.Add(tick);
            }
            line.Add(layer);
            return layer;
        }

        void Update()
        {
            if (screen == null) return;
            float now = Time.unscaledTime;
            bool matching = State.Matching;
            int joined = 0, ready = 0;
            for (int i = 0; i < State.Teams.Count; i++)
            {
                if (i < State.Joined.Count && State.Joined[i]) joined++;
                if (i < State.Ready.Count && State.Ready[i]) ready++;
            }

            if (kicker != null) SetText(kicker, matching ? "빠른 매칭 · " + kickerText : kickerText);
            if (step != null) step.text = State.Step;
            if (percent != null) percent.text = matching ? State.Clock : State.Percent + "%";
            float filled = matching ? (State.Expected > 0 ? 100f * joined / State.Expected : 0f) : Mathf.Clamp(State.Percent, 0, 100);
            if (fill != null) fill.style.width = new Length(filled, LengthUnit.Percent);
            TickSquares(filled / 100f * SquareCount, now);
            if (ticks != null) ticks.style.display = matching ? DisplayStyle.None : DisplayStyle.Flex;
            if (cancel != null) cancel.style.display = matching && State.CanCancel && !Hiding ? DisplayStyle.Flex : DisplayStyle.None;
            if (addBot != null)
            {
                addBot.style.display = matching && State.ShowAddBot && !Hiding ? DisplayStyle.Flex : DisplayStyle.None;
                addBot.SetEnabled(State.CanAddBot);
            }
            if (start != null)
            {
                start.style.display = matching && State.ShowStart && !Hiding ? DisplayStyle.Flex : DisplayStyle.None;
                start.SetEnabled(State.CanStart);
            }
            if (startNote != null)
            {
                bool note = matching && !Hiding && !string.IsNullOrEmpty(State.StartNote);
                startNote.style.display = note ? DisplayStyle.Flex : DisplayStyle.None;
                if (note) startNote.text = State.StartNote;
            }

            studio.Sync(State.Teams, State.Joined, State.Bots, now);
            int total = State.Teams.Count;
            bool everyone = matching ? State.Expected > 0 && joined >= State.Expected : total > 0 && ready >= total;
            studio.Cheer = everyone && studio.AllIn;
            studio.Animate(now);
            SyncCards(now);
            allIn?.EnableInClassList("load-allin-on", studio.Cheer);

            if (tips.Length > 0)
            {
                int index = (int)((now - shownAt) / TipSeconds) % tips.Length;
                if (index != tipIndex) ShowTip(index);
            }

            if (Hiding && now - hideAt >= FadeSeconds) Destroy(gameObject);
        }

        // The studio draws after every Update, so its figures are this frame's.
        void LateUpdate()
        {
            if (screen != null && !Hiding) studio.Render();
        }

        void SyncCards(float now)
        {
            int[] seen = { 0, 0 }, joined = { 0, 0 }, ready = { 0, 0 };
            int seats = State.Expected > 0 ? Mathf.Min(LoadingStudio.PerTeam, State.Expected / 2) : 0;
            for (int i = 0; i < State.Teams.Count; i++)
            {
                int team = State.Teams[i] == 0 ? 0 : 1;
                int spot = seen[team]++;
                if (spot >= LoadingStudio.PerTeam) continue;
                var card = CardAt(team, spot);
                bool bot = i < State.Bots.Count && State.Bots[i];
                bool isIn = i < State.Joined.Count && State.Joined[i];
                bool isReady = i < State.Ready.Count && State.Ready[i];
                SetCard(card, isIn, bot, i < State.Names.Count ? State.Names[i] : "", now);
                card.Root.EnableInClassList("load-me", i == State.Me);
                if (isIn) joined[team]++;
                if (isReady) ready[team]++;
                float age = card.OnAt >= 0 ? now - card.OnAt : -1;
                card.Status.text = !isIn ? "찾는 중" : age < ArrivedSeconds ? "입장!"
                                 : State.Matching ? "대기 중" : isReady ? "준비 완료" : "불러오는 중";
                Slide(card, age);
            }
            for (int team = 0; team < 2; team++)
            {
                int shown = Mathf.Max(seats, Mathf.Min(seen[team], LoadingStudio.PerTeam));
                // Seats nobody has taken yet.
                for (int k = seen[team]; k < shown; k++)
                {
                    var empty = CardAt(team, k);
                    SetCard(empty, false, false, "", now);
                    empty.Root.RemoveFromClassList("load-me");
                    empty.Status.text = "찾는 중";
                    Slide(empty, -1);
                }
                for (int k = 0; k < cards[team].Count; k++) cards[team][k].Root.style.display = k < shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (counts[team] != null)
                    counts[team].text = State.Matching ? joined[team] + " / " + Mathf.Max(shown, 1) + " 입장"
                                                       : ready[team] + " / " + Mathf.Max(seen[team], 1) + " 준비";
            }
        }

        Card CardAt(int team, int spot)
        {
            var list = cards[team];
            while (list.Count <= spot) list.Add(NewCard(team, list.Count));
            return list[spot];
        }

        // Who sits in a card: lit and named when someone is in it, a dim empty seat otherwise.
        void SetCard(Card card, bool present, bool bot, string name, float now)
        {
            if (card.Present != present || (present && card.Bot != bot))
            {
                card.Present = present;
                card.Bot = bot;
                card.OnAt = present ? now : -1;
                card.Root.EnableInClassList("load-card-on", present);
                var kind = bot ? PieceKind.Pawn : LoadingStudio.Kinds[card.Spot];
                var face = present ? MenuArt.Portrait("face", kind, card.Team, new FigurePose { Arm = .3f }, new Vector2(0, 6), true) : null;
                card.Avatar.style.backgroundImage = face != null ? new StyleBackground(Background.FromRenderTexture(face)) : new StyleBackground(StyleKeyword.None);
                card.Sub.text = present ? (card.Team == 0 ? "WHITE · " : "BLACK · ") + (bot ? "AI" : kind.ToString().ToUpperInvariant()) : "";
            }
            string shown = present ? name : "빈 자리";
            if (card.Name.text != shown) card.Name.text = shown;
        }

        // The card slides in from its side with a little overshoot and a flash.
        static void Slide(Card card, float age)
        {
            if (age >= 0 && age < CardEnter)
            {
                float x = age / CardEnter;
                float back = 1 + 2.70158f * Mathf.Pow(x - 1, 3) + 1.70158f * Mathf.Pow(x - 1, 2);
                card.Root.style.translate = new Translate(card.Side * -30f * (1 - back), 0);
                card.Flash.style.opacity = .55f * (1 - x);
            }
            else
            {
                card.Root.style.translate = new Translate(0, 0);
                card.Flash.style.opacity = 0;
            }
        }

        Card NewCard(int team, int spot)
        {
            var card = new Card { Team = team, Spot = spot, Side = team == 0 ? 1f : -1f };
            card.Root = new VisualElement { pickingMode = PickingMode.Ignore };
            card.Root.AddToClassList("load-card");
            card.Root.style.backgroundImage = new StyleBackground(MenuArt.PanelTexture());
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("load-card-bar");
            card.Root.Add(bar);
            card.Avatar = new VisualElement { pickingMode = PickingMode.Ignore };
            card.Avatar.AddToClassList("load-avatar");
            card.Root.Add(card.Avatar);
            var names = new VisualElement { pickingMode = PickingMode.Ignore };
            names.AddToClassList("load-names");
            card.Name = new Label("빈 자리") { pickingMode = PickingMode.Ignore };
            card.Name.AddToClassList("load-name");
            RuntimePanels.Display(card.Name);
            card.Sub = new Label("") { pickingMode = PickingMode.Ignore };
            card.Sub.AddToClassList("load-sub");
            names.Add(card.Name);
            names.Add(card.Sub);
            card.Root.Add(names);
            var gap = new VisualElement { pickingMode = PickingMode.Ignore };
            gap.AddToClassList("grow");
            card.Root.Add(gap);
            card.Status = new Label { pickingMode = PickingMode.Ignore };
            card.Status.AddToClassList("load-state");
            card.Root.Add(card.Status);
            card.Flash = new VisualElement { pickingMode = PickingMode.Ignore };
            card.Flash.AddToClassList("load-card-flash");
            card.Root.Add(card.Flash);
            rosters[team]?.Add(card.Root);
            return card;
        }

        void ShowTip(int index)
        {
            tipIndex = index;
            var tip = tips[index];
            SetText(tipTitle, tip.Title);
            SetText(tipText, Wrap(tip.Text, TipLineUnits));
        }

        // A colour wash from the screen edge towards the middle (`fromRight` for black).
        void Wash(VisualElement element, Color color, bool fromRight)
        {
            if (element == null) return;
            const int w = 64;
            var texture = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Loading Wash" };
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f);
                if (fromRight) u = 1 - u;
                var c = color;
                c.a = u < .55f ? Mathf.Lerp(.38f, .08f, u / .55f) : Mathf.Lerp(.08f, 0f, (u - .55f) / .45f);
                texture.SetPixel(x, 0, c);
            }
            texture.Apply();
            washes.Add(texture);
            element.style.backgroundImage = new StyleBackground(texture);
        }

        static void SetText(Label label, string text)
        {
            if (label == null) return;
            if (label.text != (text ?? "")) label.text = text ?? "";
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        static string Wrap(string text, int units)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var wrapped = new System.Text.StringBuilder();
            int line = 0;
            foreach (string word in text.Split(' '))
            {
                int width = 0;
                foreach (char c in word) width += c >= '가' && c <= '힣' ? 2 : 1;
                if (line > 0 && line + 1 + width > units) { wrapped.Append('\n'); line = 0; }
                else if (line > 0) { wrapped.Append(' '); line++; }
                wrapped.Append(word);
                line += width;
            }
            return wrapped.ToString();
        }

        void OnDestroy()
        {
            open = Mathf.Max(0, open - 1);
            studio.Dispose();
            if (ownedPanel != null) Destroy(ownedPanel);
            if (shade != null) Destroy(shade);
            foreach (var texture in washes) if (texture != null) Destroy(texture);
        }
    }
}
