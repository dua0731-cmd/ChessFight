using System.Collections.Generic;
using System.Text;
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
        // One entry per player, roster order: their team (0 white, 1 black) and
        // whether their match scene has finished loading.
        public readonly List<int> Teams = new List<int>();
        public readonly List<bool> Ready = new List<bool>();
    }

    // The screen between the lobby and a match (Docs/Architecture/UI.md "로딩
    // 화면"): sample A's layout over sample B's backdrop, chosen 2026-09-30.
    // Title and rule top left; one file of the board bottom left, where a pawn
    // walks from rank 2 to rank 8 as this PC loads and promotes to a queen when
    // it is ready; a dot per player; a rotating tip bottom right. Behind it the
    // menu board, dimmed at the sides, with two pawns squaring up in the middle.
    //
    // Display only, nothing to click, and it swallows clicks meant for the lobby
    // under it. It lives on the persistent runtime object, so it survives the
    // scene switch it hides; Hide() fades it out and removes it.
    [DisallowMultipleComponent]
    public sealed class LoadingScreenView : MonoBehaviour
    {
        // Above every scene's own HUD panel (those keep the default order, 0).
        const float SortingOrder = 100f;
        const float TipSeconds = 4.5f, HopSeconds = .34f, HopHeight = 16f, FadeSeconds = .35f;
        // Matches .load-rank and .load-mark in NetworkHud.uss.
        const float RankInner = 514f, MarkSize = 76f;
        // Tip text is broken at spaces by hand: UI Toolkit breaks Korean between
        // any two syllables. A Hangul syllable counts two units (see NetworkHudView).
        const int TipLineUnits = 32;

        static readonly Color TileBlend = PieceFigure.Hex(0xC9C2B8);
        static readonly Color Navy = new Color(9 / 255f, 15 / 255f, 30 / 255f);

        public readonly LoadingState State = new LoadingState();
        public bool Hiding => hideAt >= 0;

        PanelSettings ownedPanel;
        RenderTexture stage, duel;
        Texture2D dim;
        VisualElement screen, rank, mark, pips, tipCard, tipArt;
        Label step, percent, ready, tipTitle, tipText;
        CrownMark crown;
        readonly VisualElement[] tints = new VisualElement[8];
        readonly List<VisualElement> pipDots = new List<VisualElement>();
        LoadingTip[] tips = System.Array.Empty<LoadingTip>();
        PieceSkin skin;
        RenderTexture pawnArt, queenArt;
        int shownRank, tipIndex = -1;
        float shownAt, hopAt = -1, hideAt = -1;

        public void Build(LoadingContent content, PieceSkin mine)
        {
            skin = mine;
            var root = RuntimePanels.Create(gameObject, Resources.Load<VisualTreeAsset>("LoadingHud"),
                                            Resources.Load<ThemeStyleSheet>("NetworkTheme"), null,
                                            new Vector2Int(1280, 720), out ownedPanel);
            if (root == null) return;
            if (ownedPanel != null) ownedPanel.sortingOrder = SortingOrder;
            screen = root.Q<VisualElement>("loading");

            // The pictures first: they are taken from the scene still on screen.
            // The board is dimmed behind the screen, so 1600 wide is plenty.
            int width = Mathf.Clamp(Screen.width, 640, 1600);
            int height = Mathf.Max(360, Mathf.RoundToInt(width * (float)Screen.height / Mathf.Max(1, Screen.width)));
            stage = LoadingBackdrop.Stage(width, height);
            duel = LoadingBackdrop.Duel(780, 520, Navy);
            dim = Dim();
            Paint(root.Q<VisualElement>("load-backdrop"), stage);
            Paint(root.Q<VisualElement>("load-duel"), duel);
            var dimLayer = root.Q<VisualElement>("load-dim");
            if (dimLayer != null) dimLayer.style.backgroundImage = new StyleBackground(dim);

            SetText(root.Q<Label>("load-kicker"), content.Kicker);
            SetText(root.Q<Label>("load-logo-top"), content.LogoTop);
            SetText(root.Q<Label>("load-logo-bottom"), content.LogoBottom);
            SetText(root.Q<Label>("load-rule"), content.Rule);
            SetText(root.Q<Label>("load-note"), content.Note);

            step = root.Q<Label>("load-step");
            percent = root.Q<Label>("load-percent");
            ready = root.Q<Label>("load-ready");
            pips = root.Q<VisualElement>("load-pips");
            rank = root.Q<VisualElement>("load-rank");
            BuildRank();

            tipCard = root.Q<VisualElement>("load-tip");
            tipTitle = root.Q<Label>("load-tip-kicker");
            tipText = root.Q<Label>("load-tip-text");
            tipArt = root.Q<VisualElement>("load-tip-art");
            tips = content.Tips ?? System.Array.Empty<LoadingTip>();
            if (tipCard != null && tips.Length == 0) tipCard.style.display = DisplayStyle.None;

            pawnArt = PiecePortraits.Get(PieceKind.Pawn, skin, -26f, TileBlend);
            queenArt = PiecePortraits.Get(PieceKind.Queen, skin, -22f, TileBlend);
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

        void BuildRank()
        {
            if (rank == null) return;
            for (int r = 1; r <= 8; r++)
            {
                var square = new VisualElement { pickingMode = PickingMode.Ignore };
                square.AddToClassList("load-sq");
                // The board's a1 is dark, so odd ranks are dark on this file.
                square.AddToClassList(r % 2 == 1 ? "load-sq-dark" : "load-sq-light");
                if (r == 1) square.AddToClassList("load-sq-first");
                if (r == 8) square.AddToClassList("load-sq-last");
                var tint = new VisualElement { pickingMode = PickingMode.Ignore };
                tint.AddToClassList("load-tint");
                square.Add(tint);
                tints[r - 1] = tint;
                var number = new Label(r.ToString()) { pickingMode = PickingMode.Ignore };
                number.AddToClassList("load-rn");
                square.Add(number);
                if (r == 8)
                {
                    crown = new CrownMark();
                    crown.AddToClassList("load-crown");
                    square.Add(crown);
                }
                rank.Add(square);
            }
            mark = new VisualElement { pickingMode = PickingMode.Ignore };
            mark.AddToClassList("load-mark");
            rank.Add(mark);
        }

        void Update()
        {
            if (screen == null) return;
            float now = Time.unscaledTime;
            if (step != null) step.text = State.Step;
            if (percent != null) percent.text = State.Percent + "%";

            // Rank 2 is where a pawn starts; rank 8 is promotion, which is ready.
            bool promoted = State.Percent >= MatchStart.Ready;
            int at = promoted ? 8 : 2 + Mathf.Clamp(Mathf.FloorToInt(State.Percent / 100f * 6f), 0, 5);
            if (at != shownRank)
            {
                if (shownRank > 0) hopAt = now;
                shownRank = at;
                for (int i = 0; i < tints.Length; i++)
                {
                    if (tints[i] == null) continue;
                    tints[i].EnableInClassList("load-tint-passed", i + 1 < at);
                    tints[i].EnableInClassList("load-tint-here", i + 1 == at);
                }
                if (mark != null)
                {
                    mark.style.left = (at - .5f) * RankInner / 8f - MarkSize / 2f;
                    var art = promoted ? queenArt : pawnArt;
                    if (art != null) mark.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(art));
                }
                if (crown != null) crown.Promoted = promoted;
            }
            if (mark != null)
            {
                float hop = hopAt >= 0 ? (now - hopAt) / HopSeconds : 1f;
                mark.style.translate = new StyleTranslate(new Translate(0, hop < 1f ? -HopHeight * Mathf.Sin(hop * Mathf.PI) : 0f));
            }

            SyncPips();
            if (ready != null)
            {
                int count = 0;
                foreach (bool r in State.Ready) if (r) count++;
                ready.text = "플레이어 준비 " + count + " / " + State.Ready.Count;
            }

            if (tips.Length > 0)
            {
                int index = (int)((now - shownAt) / TipSeconds) % tips.Length;
                if (index != tipIndex) ShowTip(index);
            }

            if (Hiding && now - hideAt >= FadeSeconds) Destroy(gameObject);
        }

        void SyncPips()
        {
            if (pips == null) return;
            int count = State.Teams.Count;
            if (pipDots.Count != count)
            {
                pips.Clear();
                pipDots.Clear();
                for (int i = 0; i < count; i++)
                {
                    // A thin line between the two teams.
                    if (i > 0 && State.Teams[i] != State.Teams[i - 1])
                    {
                        var gap = new VisualElement { pickingMode = PickingMode.Ignore };
                        gap.AddToClassList("load-pip-gap");
                        pips.Add(gap);
                    }
                    var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                    dot.AddToClassList("load-pip");
                    pips.Add(dot);
                    pipDots.Add(dot);
                }
            }
            for (int i = 0; i < count; i++)
            {
                var dot = pipDots[i];
                dot.EnableInClassList("load-pip-white", State.Teams[i] == 0);
                dot.EnableInClassList("load-pip-black", State.Teams[i] != 0);
                dot.EnableInClassList("load-pip-on", i < State.Ready.Count && State.Ready[i]);
            }
        }

        void ShowTip(int index)
        {
            tipIndex = index;
            var tip = tips[index];
            SetText(tipTitle, tip.Title);
            SetText(tipText, Wrap(tip.Text, TipLineUnits));
            var art = PiecePortraits.Get(tip.Piece, skin, tip.Piece == PieceKind.Knight ? 42f : -10f, Navy);
            if (tipArt != null && art != null) tipArt.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(art));
        }

        static void Paint(VisualElement element, RenderTexture texture)
        {
            if (element != null && texture != null) element.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
        }

        static void SetText(Label label, string text)
        {
            if (label == null) return;
            label.text = text ?? "";
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        static string Wrap(string text, int units)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var wrapped = new StringBuilder();
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

        // Navy over the backdrop: strong at both sides, where the title, the
        // file and the tip sit, lighter in the middle behind the two pawns.
        static Texture2D Dim()
        {
            const int width = 256;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Loading Dim"
            };
            var navy = Navy;
            for (int x = 0; x < width; x++)
            {
                float side = Mathf.Abs(x / (width - 1f) - .5f) * 2f;   // 0 in the middle, 1 at the edges
                float a = side < .46f ? Mathf.Lerp(.52f, .84f, side / .46f) : Mathf.Lerp(.84f, .95f, (side - .46f) / .54f);
                navy.a = StageKit.Darkening(a);
                texture.SetPixel(x, 0, navy);
            }
            texture.Apply();
            return texture;
        }

        void OnDestroy()
        {
            if (ownedPanel != null) Destroy(ownedPanel);
            if (dim != null) Destroy(dim);
            foreach (var texture in new[] { stage, duel })
                if (texture != null) { texture.Release(); Destroy(texture); }
        }

        // The crown on rank 8: an outline until this PC is ready, then gold.
        // Drawn with the vector API, since the project has no image assets for it.
        sealed class CrownMark : VisualElement
        {
            static readonly Vector2[] Outline =
            {
                new Vector2(-11, 8), new Vector2(-12, -5), new Vector2(-5, 0), new Vector2(0, -10),
                new Vector2(5, 0), new Vector2(12, -5), new Vector2(11, 8)
            };
            static readonly Color Gold = PieceFigure.Hex(0xFFD23A), Deep = PieceFigure.Hex(0xB38A12), Ink = PieceFigure.Hex(0x0A1428);
            bool promoted;

            public bool Promoted
            {
                get => promoted;
                set { if (promoted == value) return; promoted = value; MarkDirtyRepaint(); }
            }

            public CrownMark()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                float scale = Mathf.Min(rect.width / 26f, rect.height / 20f);
                var center = rect.center + new Vector2(0, scale);
                var painter = context.painter2D;
                painter.lineJoin = LineJoin.Round;
                painter.lineWidth = 2f;
                painter.BeginPath();
                for (int i = 0; i < Outline.Length; i++)
                {
                    var point = center + Outline[i] * scale;
                    if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
                }
                painter.ClosePath();
                if (promoted)
                {
                    painter.fillColor = Gold;
                    painter.Fill();
                    painter.strokeColor = Ink;
                }
                else painter.strokeColor = Deep;
                painter.Stroke();
            }
        }
    }
}
