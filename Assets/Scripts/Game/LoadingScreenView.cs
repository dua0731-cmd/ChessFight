using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // What the loading screen shows this frame. Whoever runs the load fills it.
    public sealed class LoadingState
    {
        public string Step = "";
        public int Percent;
        // One entry per player, roster order sorted by team: their team (0 white,
        // 1 black), whether their match scene has finished loading, their name,
        // whether they are a bot, and which entry is this PC's player (-1: none).
        public readonly List<int> Teams = new List<int>();
        public readonly List<bool> Ready = new List<bool>();
        public readonly List<string> Names = new List<string>();
        public readonly List<bool> Bots = new List<bool>();
        public int Me = -1;
    }

    // The screen between the lobby and a match (Docs/Architecture/UI.md "로딩
    // 화면"), design C revised (2026-10-03): the mode's name top centre, white's
    // players down the left and black's down the right, and behind them
    // LoadingStudio's wooden stage, onto which each player walks as their PC
    // finishes loading; their card lights up ("입장!") as they arrive. With
    // everyone in, "모두 입장 완료 · 곧 출발!" and the whole stage cheers. The
    // progress line and a rotating tip run along the bottom.
    //
    // Display only, nothing to click, and it swallows clicks meant for the lobby
    // under it. It lives on the persistent runtime object, so it survives the
    // scene switch it hides; Hide() fades it out and removes it.
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

        sealed class Card
        {
            public VisualElement Root, Avatar, Flash;
            public Label Name, Sub, Status;
            public float Side, OnAt = -1;
            public bool Bot;
            public string Shown;
        }

        PanelSettings ownedPanel;
        Texture2D shade;
        readonly LoadingStudio studio = new LoadingStudio();
        VisualElement screen, fill, allIn;
        VisualElement[] rosters = new VisualElement[2];
        Label step, percent, tipTitle, tipText;
        Label[] counts = new Label[2];
        readonly List<Card>[] cards = { new List<Card>(), new List<Card>() };
        LoadingTip[] tips = System.Array.Empty<LoadingTip>();
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
            var cyan = MenuArt.Hex(0x1E78BE);
            var red = MenuArt.Hex(0xC8283C);
            Wash(root.Q<VisualElement>("load-side-white"), cyan, false);
            Wash(root.Q<VisualElement>("load-side-black"), red, true);
            var shadeLayer = root.Q<VisualElement>("load-shade");
            if (shadeLayer != null)
            {
                shade = MenuMarks.Vignette(new Color(8 / 255f, 4 / 255f, 2 / 255f), .55f, .8f);
                shadeLayer.style.backgroundImage = new StyleBackground(shade);
            }

            SetText(root.Q<Label>("load-kicker"), content.Kicker);
            SetText(root.Q<Label>("load-title"), content.Title);
            SetText(root.Q<Label>("load-title-hi"), content.Title);
            SetText(root.Q<Label>("load-rule"), content.Rule);
            SetText(root.Q<Label>("load-note"), content.Note);

            rosters[0] = root.Q<VisualElement>("load-roster-white");
            rosters[1] = root.Q<VisualElement>("load-roster-black");
            counts[0] = root.Q<Label>("load-count-white");
            counts[1] = root.Q<Label>("load-count-black");
            allIn = root.Q<VisualElement>("load-allin");
            step = root.Q<Label>("load-step");
            percent = root.Q<Label>("load-percent");
            fill = root.Q<VisualElement>("load-fill");
            if (fill != null) fill.style.backgroundImage = new StyleBackground(MenuArt.Line(MenuArt.Hex(MenuArt.Cyan), MenuArt.Hex(MenuArt.Gold), MenuArt.Hex(MenuArt.Red)));
            Ticks(root.Q<VisualElement>("load-line"));

            tipTitle = root.Q<Label>("load-tip-kicker");
            tipText = root.Q<Label>("load-tip-text");
            tips = content.Tips ?? System.Array.Empty<LoadingTip>();
            var tipRow = root.Q<VisualElement>("load-tip");
            if (tipRow != null && tips.Length == 0) tipRow.style.display = DisplayStyle.None;
            shownAt = Time.unscaledTime;
            Update();
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

        // A tick on the line for each rank a pawn passes, numbered 2 to 8.
        static void Ticks(VisualElement line)
        {
            if (line == null) return;
            for (int i = 1; i < 8; i++)
            {
                var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                tick.AddToClassList("load-tick");
                tick.style.left = new Length(i / 8f * 100f, LengthUnit.Percent);
                var label = new Label((i + 1).ToString()) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("load-tick-label");
                tick.Add(label);
                line.Add(tick);
            }
        }

        void Update()
        {
            if (screen == null) return;
            float now = Time.unscaledTime;
            if (step != null) step.text = State.Step;
            if (percent != null) percent.text = State.Percent + "%";
            if (fill != null) fill.style.width = new Length(Mathf.Clamp(State.Percent, 0, 100), LengthUnit.Percent);

            studio.Sync(State.Teams, State.Ready, State.Bots, now);
            studio.Animate(now);
            SyncCards(now);
            allIn?.EnableInClassList("load-allin-on", studio.AllIn);

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
            int[] seen = { 0, 0 }, ready = { 0, 0 };
            for (int i = 0; i < State.Teams.Count; i++)
            {
                int team = State.Teams[i] == 0 ? 0 : 1;
                int spot = seen[team]++;
                if (spot >= LoadingStudio.PerTeam) continue;
                var list = cards[team];
                bool bot = i < State.Bots.Count && State.Bots[i];
                if (spot >= list.Count) list.Add(NewCard(team, spot, bot));
                var card = list[spot];
                string name = i < State.Names.Count ? State.Names[i] : "";
                if (card.Shown != name) { card.Shown = name; card.Name.text = name; }
                card.Root.EnableInClassList("load-me", i == State.Me);
                bool isReady = i < State.Ready.Count && State.Ready[i];
                if (isReady)
                {
                    ready[team]++;
                    if (card.OnAt < 0) { card.OnAt = now; card.Root.AddToClassList("load-card-on"); }
                }
                float age = card.OnAt >= 0 ? now - card.OnAt : -1;
                card.Status.text = age < 0 ? "불러오는 중" : age < ArrivedSeconds ? "입장!" : "준비 완료";
                // The card slides in from its side with a little overshoot and a flash.
                if (age >= 0 && age < CardEnter)
                {
                    float x = age / CardEnter;
                    float back = 1 + 2.70158f * Mathf.Pow(x - 1, 3) + 1.70158f * Mathf.Pow(x - 1, 2);
                    card.Root.style.translate = new Translate(card.Side * -30f * (1 - back), 0);
                    card.Flash.style.opacity = .55f * (1 - x);
                }
                else if (age >= CardEnter)
                {
                    card.Root.style.translate = new Translate(0, 0);
                    card.Flash.style.opacity = 0;
                }
            }
            for (int team = 0; team < 2; team++)
            {
                for (int k = 0; k < cards[team].Count; k++) cards[team][k].Root.style.display = k < seen[team] ? DisplayStyle.Flex : DisplayStyle.None;
                if (counts[team] != null) counts[team].text = ready[team] + " / " + Mathf.Min(seen[team], LoadingStudio.PerTeam) + " 입장";
            }
        }

        Card NewCard(int team, int spot, bool bot)
        {
            var card = new Card { Side = team == 0 ? 1f : -1f, Bot = bot };
            card.Root = new VisualElement { pickingMode = PickingMode.Ignore };
            card.Root.AddToClassList("load-card");
            card.Root.style.backgroundImage = new StyleBackground(MenuArt.PanelTexture());
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("load-card-bar");
            card.Root.Add(bar);
            var kind = bot ? PieceKind.Pawn : LoadingStudio.Kinds[spot];
            card.Avatar = new VisualElement { pickingMode = PickingMode.Ignore };
            card.Avatar.AddToClassList("load-avatar");
            var face = MenuArt.Portrait("face", kind, team, new FigurePose { Arm = .3f }, new Vector2(0, 6), true);
            if (face != null) card.Avatar.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(face));
            card.Root.Add(card.Avatar);
            var names = new VisualElement { pickingMode = PickingMode.Ignore };
            names.AddToClassList("load-names");
            card.Name = new Label { pickingMode = PickingMode.Ignore };
            card.Name.AddToClassList("load-name");
            RuntimePanels.Display(card.Name);
            card.Sub = new Label((team == 0 ? "WHITE · " : "BLACK · ") + (bot ? "AI" : KindName(kind))) { pickingMode = PickingMode.Ignore };
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

        static string KindName(PieceKind kind) => kind.ToString().ToUpperInvariant();

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

        readonly List<Texture2D> washes = new List<Texture2D>();

        static void SetText(Label label, string text)
        {
            if (label == null) return;
            label.text = text ?? "";
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
            studio.Dispose();
            if (ownedPanel != null) Destroy(ownedPanel);
            if (shade != null) Destroy(shade);
            foreach (var texture in washes) if (texture != null) Destroy(texture);
        }
    }
}
