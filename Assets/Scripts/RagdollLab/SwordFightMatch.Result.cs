using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R868 (user 11-27, design B of Docs/GameModes/SwordFight/RESULT_SCREEN_DESIGN_v0.1.md; "글은 몇 초 이런거 하지말고
    /// 실제 스팀 출시 기준으로"): the result screen after the winner's stamp, replacing the plain cream end card. On a
    /// 1280 x 720 stage scaled to the screen, in the Pawn Rush result's walnut and brass: the result word and the score at
    /// the top right, the best piece of the match standing large on the left (the match's own model), three award cards, the two teams' line-up
    /// (ring-outs, falls) and a bar along the bottom with "결과판 숨기기 H" and the way back to the lobby.
    /// Everything comes from the ring-out lines every PC hears (Core SwordResult); a PC that missed some of them (back in
    /// the match mid-way) shows the team scores only. No times on screen: counts and names only.
    /// </summary>
    public sealed partial class SwordFightMatch
    {
        // Seconds from the result (real time): the panel after the winner's stamp, then its parts one by one.
        const float ResultShowAt = 3.6f, ResultHeroAt = .4f, ResultAwardsAt = 1.1f, ResultAwardStep = .3f, ResultBoardAt = 2f, ResultButtonsAt = 2.4f;
        // Online, the way back to the lobby by itself after this long once the buttons are up (as Pawn Rush's result).
        const float ResultLobbySeconds = 12f;

        static readonly Color RCream = SwordFightShapes.Hex("#f6ecd6"), RGold = SwordFightShapes.Hex("#ffc93d"), RBrass = SwordFightShapes.Hex("#d9ae62"),
            RMuted = SwordFightShapes.Hex("#cdbda5"), RLabel = SwordFightShapes.Hex("#c9b79f"), RMe = SwordFightShapes.Hex("#7fdfff"),
            RDeep = SwordFightShapes.Hex("#2a1a00"), RGrey = SwordFightShapes.Hex("#cfc9c1"), REdge = SwordFightShapes.Hex("#3a2414");

        // Every ring-out line of this round, in order, and everyone it named (as they were then: a player who left is kept).
        readonly List<SwordResultLine> resultLines = new List<SwordResultLine>();
        readonly Dictionary<ulong, SwordResultPlayer> resultRoster = new Dictionary<ulong, SwordResultPlayer>();
        SwordResult result;
        float resultAt = -1f;
        bool resultHidden, resultLeft;

        Texture2D rWood, rBar, rRow, rRowTop, rGoldBtn, rWoodBtn, rMeTag, rGoldTag, rGlow, rDim, rCap, rPipW;
        GUIStyle rCell;
        // The stage: its scale, its corner on the screen, and the group being drawn (moved, faded).
        float rs = 1f, rox, roy, rdx, rdy, rfade = 1f;

        /// <summary>The panel is up (the winner's stamp gives way to it).</summary>
        bool ResultUp => resultAt >= 0f && view.Phase == SwordPhase.Result && Time.unscaledTime - resultAt >= ResultShowAt;

        void ResultReset()
        {
            resultLines.Clear();
            resultRoster.Clear();
            result = null;
            resultAt = -1f;
            resultHidden = resultLeft = false;
            FigureClear();
        }

        // Somebody as it is now (the name, the side and the piece), kept for the result.
        void ResultNote(ulong id)
        {
            if (id == 0 || !game.Fighters.TryGetValue(id, out var f) || f == null || f.Pawn == null) return;
            if (!resultRoster.TryGetValue(id, out var p)) resultRoster[id] = p = new SwordResultPlayer { Id = id };
            p.Name = f.Pawn.DisplayName;
            p.Team = f.Pawn.Team;
            p.Piece = f.Pawn.Piece;
            p.Self = id == game.LocalId;
        }

        void ResultLine(SwordFeed e)
        {
            ResultNote(e.Victim);
            ResultNote(e.Credit);
            resultLines.Add(new SwordResultLine { Victim = e.Victim, Credit = e.Credit, VictimTeam = e.VictimTeam, Cause = e.Credit == 0 ? SwordCause.Self : e.Cause, CausePiece = (PieceKind)e.CausePiece });
        }

        void ResultBegin()
        {
            foreach (var id in game.Fighters.Keys) ResultNote(id);
            result = SwordResult.Build(resultRoster.Values, resultLines, view.White, view.Black, view.Winner);
            // every face the screen will show, made now (out of OnGUI): MenuArt.Portrait renders with a camera of its own
            foreach (var p in result.Players) PipFace(p.Piece, p.Team);
            resultAt = Time.unscaledTime;
            resultHidden = resultLeft = false;
        }

        /// <summary>The name of a means, as the feed says it ("칼", the skill's name, "성벽 붕괴", "스스로"), shown.</summary>
        static string MeansShown(SwordCause cause, PieceKind piece) => Loc.T(MeansName(cause, piece));

        // ---------------------------------------------------------------- the best piece's model

        // User 11-27 ("기물을 실제 인게임 기물로 교체"): the best piece as the match draws it - the match's own model
        // (Resources/MenuPieces, the pawn's bones with the piece's skin; the black set on the black side), built by the
        // lobby's LastSceneFigure, standing far over the arena on a layer of its own with a camera and two small lights
        // of its own, drawn into a texture the card shows. Built once per result, gone with the round or the match.
        const int FigureLayer = 30;
        const float FigureFov = 22f;
        GameObject figureStage;
        RenderTexture figureTexture;
        LastSceneFigure figure;
        ulong figureFor;

        Texture FigureFor(SwordResultPlayer best)
        {
            if (best == null) return null;
            if (figureFor != best.Id) FigureBuild(best);
            if (figure != null && Event.current.type == EventType.Repaint)
            {
                // a slow look round and a breath, the arms a little up
                float t = Time.unscaledTime - resultAt;
                figure.Pose(new FigurePose { Yaw = .35f * Mathf.Sin(t * .6f), Arm = .32f + .06f * Mathf.Sin(t * 2.2f), Squash = .012f * Mathf.Sin(t * 2.2f) });
            }
            return figure != null ? figureTexture : null;
        }

        void FigureBuild(SwordResultPlayer best)
        {
            FigureClear();
            try
            {
                figureStage = new GameObject("Sword Fight result figure");
                figureStage.transform.SetParent(transform, false);
                figureStage.transform.SetPositionAndRotation(new Vector3(0f, 900f, 0f), Quaternion.identity);
                figure = LastSceneFigure.Build(best.Piece, best.Team, figureStage.transform);
                if (figure == null) { FigureClear(); return; }
                figure.Place(Vector3.zero, new Vector3(0f, 0f, 10f));
                foreach (var tr in figureStage.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = FigureLayer;

                // its size, to frame it
                var renderers = figureStage.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) { FigureClear(); return; }
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) { bounds.Encapsulate(r.bounds); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                float height = Mathf.Max(.2f, bounds.size.y), width = Mathf.Max(bounds.size.x, bounds.size.z);
                var centre = bounds.center;

                figureTexture = new RenderTexture(512, 600, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Sword Fight result figure" };
                figureStage.AddComponent<ResultFigureTexture>().Texture = figureTexture;
                var camGo = new GameObject("Camera") { layer = FigureLayer };
                camGo.transform.SetParent(figureStage.transform, false);
                var cam = camGo.AddComponent<Camera>();
                // the card's own brown, clear: where a renderer keeps no alpha the piece still stands on the card's colour
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(.227f, .149f, .086f, 0f);
                cam.cullingMask = 1 << FigureLayer;
                cam.fieldOfView = FigureFov;
                cam.allowHDR = false;
                cam.targetTexture = figureTexture;
                float fit = Mathf.Max(height * 1.12f, width * 1.12f * 600f / 512f);
                float distance = fit * .5f / Mathf.Tan(FigureFov * .5f * Mathf.Deg2Rad);
                cam.nearClipPlane = Mathf.Max(.01f, distance * .05f);
                cam.farClipPlane = distance * 4f;
                camGo.transform.position = centre + new Vector3(0f, height * .08f, distance);
                camGo.transform.LookAt(centre + new Vector3(0f, height * .02f, 0f));

                // a warm key from the front left and a gold rim from behind (only on its layer; nothing else is up here)
                FigureLight(figureStage.transform, centre + new Vector3(-height * .9f, height * .9f, height * 1.4f), new Color(1f, .9f, .76f), 2.2f, height * 5f);
                FigureLight(figureStage.transform, centre + new Vector3(height * 1.1f, height * .8f, -height * 1.1f), new Color(1f, .78f, .4f), 2.8f, height * 5f);

                // the match's own camera does not look up here
                var main = game.CameraRig != null ? game.CameraRig.Cam : Camera.main;
                if (main != null) main.cullingMask &= ~(1 << FigureLayer);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[소드 파이트 결과] 기물 모형을 만들지 못해 그림으로 대신합니다: " + e.Message);
                FigureClear();
            }
            finally { figureFor = best.Id; }   // once per result, whether or not it could be made
        }

        static void FigureLight(Transform parent, Vector3 at, Color colour, float intensity, float range)
        {
            var go = new GameObject("Light") { layer = FigureLayer };
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var light = go.AddComponent<UnityEngine.Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << FigureLayer;
        }

        void FigureClear()
        {
            if (figureStage != null) Destroy(figureStage);   // its ResultFigureTexture lets the texture go
            figureStage = null;
            figureTexture = null;
            figure = null;
            figureFor = 0;
        }

        // ---------------------------------------------------------------- textures

        // A rounded slab, lighter at the top, with an edge and a bright line along its top.
        static Texture2D Slab(int w, int h, float radius, Color top, Color bottom, Color edge, float edgeWidth, Color line, int lineHeight)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float px = x + .5f, py = y + .5f;
                    float qx = Mathf.Max(radius - px, px - (w - radius), 0f), qy = Mathf.Max(radius - py, py - (h - radius), 0f);
                    float d = radius - Mathf.Sqrt(qx * qx + qy * qy);
                    Color c = Color.Lerp(bottom, top, y / (h - 1f));
                    if (y >= h - lineHeight) c = line;
                    else if (d < edgeWidth) c = edge;
                    c.a *= Mathf.Clamp01(d + .5f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        static Texture2D Glow(int size, Color colour)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float k = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    var c = colour;
                    c.a *= k * k;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        void ResultStyles()
        {
            if (rWood != null) return;
            Color clear = new Color(0, 0, 0, 0);
            rWood = Slab(64, 64, 10f, SwordFightShapes.Hex("#4a301c"), SwordFightShapes.Hex("#26180e"), new Color(217 / 255f, 174 / 255f, 98 / 255f, .35f), 1f, RGold, 3);
            rBar = Slab(16, 64, 0f, SwordFightShapes.Hex("#3e2818"), SwordFightShapes.Hex("#1e130b"), clear, 0f, new Color(217 / 255f, 174 / 255f, 98 / 255f, .5f), 1);
            rRow = Slab(16, 16, 4f, new Color(0, 0, 0, .26f), new Color(0, 0, 0, .22f), clear, 0f, clear, 0);
            rRowTop = Slab(16, 16, 4f, new Color(1f, .79f, .24f, .34f), new Color(1f, .79f, .24f, .14f), new Color(1f, .79f, .24f, .6f), 1f, clear, 0);
            rGoldBtn = Slab(32, 48, 8f, SwordFightShapes.Hex("#ffe48a"), SwordFightShapes.Hex("#f2b94a"), SwordFightShapes.Hex("#b9781a"), 1f, clear, 0);
            rWoodBtn = Slab(32, 48, 8f, SwordFightShapes.Hex("#6b4428"), SwordFightShapes.Hex("#4a2e19"), new Color(217 / 255f, 174 / 255f, 98 / 255f, .55f), 1f, clear, 0);
            rMeTag = Slab(16, 16, 4f, RMe, RMe, RMe, 0f, clear, 0);
            rGoldTag = Slab(16, 16, 4f, SwordFightShapes.Hex("#ffe48a"), SwordFightShapes.Hex("#f2b94a"), clear, 0f, clear, 0);
            rCap = Slab(16, 16, 4f, clear, clear, new Color(RCream.r, RCream.g, RCream.b, .8f), 1.5f, clear, 0);
            rGlow = Glow(128, new Color(1f, .82f, .45f, .55f));
            // The white side's pip behind its faces: a deep bronze, not the match HUD's cream (pipW), on which the white
            // pieces' own faces could hardly be told apart (user 11-28).
            rPipW = Rounded(26, 26, 13f, SwordFightShapes.Hex("#8a6238"), SwordFightShapes.Hex("#e8c274"), 1.5f);
            rDim = new Texture2D(1, 1);
            rDim.SetPixel(0, 0, new Color(14 / 255f, 9 / 255f, 5 / 255f, .62f));
            rDim.Apply();
            rCell = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, clipping = TextClipping.Clip, wordWrap = false, richText = false };
        }

        // ---------------------------------------------------------------- the stage

        Rect S(float x, float y, float w, float h) => new Rect(rox + (x + rdx) * rs, roy + (y + rdy) * rs, w * rs, h * rs);
        int Fs(float size) => Mathf.Max(1, Mathf.RoundToInt(size * rs));
        Color Fade(Color c) => new Color(c.r, c.g, c.b, c.a * rfade);

        void P(float x, float y, float w, float h, Texture2D tex, int border)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, rfade);
            Panel(S(x, y, w, h), tex, border);
            GUI.color = old;
        }

        void T(float x, float y, float w, float h, string text, GUIStyle style, float size, Color colour, TextAnchor anchor = TextAnchor.MiddleLeft) =>
            Text(S(x, y, w, h), text, style, Fs(size), Fade(colour), anchor);

        // Black Han Sans with a dark edge and a drop under it (the stamps' look).
        void Outlined(float x, float y, float w, float h, string text, float size, Color fill, TextAnchor anchor)
        {
            var r = S(x, y, w, h);
            displayStyle.fontSize = Fs(size);
            displayStyle.alignment = anchor;
            var old = GUI.color;
            float o = Mathf.Max(1.5f, 2.5f * rs);
            GUI.color = Fade(REdge);
            GUI.Label(new Rect(r.x, r.y + o * 2f, r.width, r.height), text, displayStyle);
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * o, r.y + Mathf.Sin(a) * o, r.width, r.height), text, displayStyle);
            }
            GUI.color = Fade(fill);
            GUI.Label(r, text, displayStyle);
            GUI.color = old;
        }

        void Icon(float x, float y, float size, PieceKind piece, int team)
        {
            var icon = MatchNameplate.Icon(piece, team == 0);
            if (icon == null) return;
            float iw = size * icon.width / icon.height;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, rfade);
            GUI.DrawTexture(S(x + (size - iw) / 2f, y, iw, size), icon);
            GUI.color = old;
        }

        // A round team pip with the piece in it (the score panel's): the piece's own face as the match draws it (user
        // 11-27: "다른 것도 실제 기물을 넣을 수 있게 해", MenuArt.Portrait - the match's model, rendered once and kept),
        // the icon where it cannot be made.
        void Pip(float x, float y, float size, PieceKind piece, int team)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, rfade);
            var face = PipFace(piece, team);
            // the fallback icon keeps the HUD's cream pip, which it was drawn for
            GUI.DrawTexture(S(x, y, size, size), team == 1 ? pipB : face != null ? rPipW : pipW);
            if (face != null) GUI.DrawTexture(S(x - size * .08f, y - size * .14f, size * 1.16f, size * 1.16f), face, ScaleMode.ScaleToFit, true);
            GUI.color = old;
            if (face == null) Icon(x + size * .11f, y + size * .11f, size * .78f, piece, team);
        }

        static readonly Dictionary<int, Texture> pipFaces = new Dictionary<int, Texture>();

        static Texture PipFace(PieceKind piece, int team)
        {
            int key = (int)piece * 2 + (team == 1 ? 1 : 0);
            if (pipFaces.TryGetValue(key, out var face)) return face;
            try { face = MenuArt.Portrait("sword-result", piece, team == 1 ? 1 : 0, new FigurePose { Arm = .3f }, new Vector2(.6f, 6f), true); }
            catch (System.Exception e) { Debug.LogWarning("[소드 파이트 결과] 기물 얼굴을 만들지 못해 그림으로 대신합니다: " + e.Message); face = null; }
            pipFaces[key] = face;
            return face;
        }

        // A player's name always in the body face, as the line-up draws it (user 11-27: "떄 할 때 폰트가 깨지는 점" and,
        // shown both, "앞에 거가 맞아"): Black Han Sans carries the 2,350 common syllables only, and Font.HasCharacter
        // said yes to the missing ones on a dynamic font, so a name like "떄이" came out in two faces.
        GUIStyle rName;

        GUIStyle NameStyle(string name)
        {
            if (rName == null) rName = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, richText = false, clipping = TextClipping.Clip, wordWrap = false };
            rName.font = RuntimePanels.BodyFont;
            return rName;
        }

        // A small label plate ("나", "최고의 기물"); returns its width.
        float TagWidth(string text)
        {
            bodyStyle.fontSize = Fs(11f);
            return bodyStyle.CalcSize(new GUIContent(text)).x / rs + 10f;
        }

        float Tag(float x, float y, string text, Texture2D tex, Color ink)
        {
            float w = TagWidth(text);
            P(x, y, w, 17f, tex, 4);
            T(x, y, w, 17f, text, bodyStyle, 11f, ink, TextAnchor.MiddleCenter);
            return w;
        }

        static float OutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        static float OutBack(float t) { t = Mathf.Clamp01(t); const float c = 1.6f; float u = t - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }

        // ---------------------------------------------------------------- drawing

        void DrawResult(float w, float h, int myTeam)
        {
            if (result == null) return;
            ResultStyles();
            // the title face for the language shown (Black Han Sans has Hangul and Latin only), the body face otherwise
            var keep = displayStyle.font;
            displayStyle.font = RuntimePanels.DisplayFace != null ? RuntimePanels.DisplayFace : RuntimePanels.BodyFont;
            try { DrawResultScreen(w, h, myTeam); }
            finally { displayStyle.font = keep; }
        }

        void DrawResultScreen(float w, float h, int myTeam)
        {
            float t = Time.unscaledTime - resultAt - ResultShowAt;
            rs = Mathf.Min(w / 1280f, h / 720f);
            rox = (w - 1280f * rs) / 2f;
            roy = (h - 720f * rs) / 2f;
            bool won = view.Winner >= 0 && view.Winner == myTeam, draw = view.Winner < 0;
            float buttonsAt = ResultButtonsAt;

            // H (or the chip) hides the panel to look at the arena; not while a chat line is being typed.
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.H && !ChatBox.KeysHeld && t >= buttonsAt) { resultHidden = !resultHidden; e.Use(); }

            rdx = rdy = 0f;
            rfade = Mathf.Clamp01(t / .4f);
            if (!resultHidden)
            {
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, rfade);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), rDim);
                GUI.color = old;
                DrawResultHead(t, won, draw);
                DrawResultHero(t);
                DrawResultAwards(t);
                DrawResultBoard(t, myTeam);
            }
            else DrawResultMini(won, draw);
            DrawResultBar(w, h, t, buttonsAt);
        }

        // user 11-27: "승리! 할 때 ! 지워주고" - Pawn Rush's "승리!" without its mark, in every language ("Victory", "胜利").
        string ResultWord(bool won, bool draw) => draw ? Loc.T("무승부") : won ? Loc.T("승리!").TrimEnd('!', '！', ' ') : Loc.T("패배");

        string ResultHow()
        {
            string how = view.Clock > settings.Duration + .01 ? Loc.T("서든데스") : view.Clock < settings.Duration - .01 ? Loc.F("먼저 {0}점", settings.Target) : Loc.T("시간 종료");
            string side = view.Winner < 0 ? Loc.T("무승부") : IsWhite(view.Winner) ? Loc.T("백팀 승리") : Loc.T("흑팀 승리");
            return $"{side} · {how}";
        }

        void DrawResultHead(float t, bool won, bool draw)
        {
            // the word drops in big and settles
            float k = OutBack(t / .5f);
            float size = Mathf.LerpUnclamped(96f, 66f, k);
            rfade = Mathf.Clamp01(t / .25f);
            rdy = -30f * (1f - OutCubic(t / .5f));
            Outlined(486f, 26f, 520f, 86f, ResultWord(won, draw), size, won ? SwordFightShapes.Hex("#f7d58a") : RGrey, TextAnchor.MiddleLeft);
            T(490f, 108f, 520f, 26f, ResultHow(), displayStyle, 17f, SwordFightShapes.Hex("#ffe7b8"));
            // the score: white first, the winners' side gold
            Color wc = view.Winner == 0 ? RGold : RCream, bc = view.Winner == 1 ? RGold : RCream;
            T(1010f, 34f, 92f, 70f, view.White.ToString(), displayStyle, 52f, wc, TextAnchor.MiddleRight);
            T(1102f, 34f, 34f, 70f, ":", displayStyle, 44f, RMuted, TextAnchor.MiddleCenter);
            T(1136f, 34f, 92f, 70f, view.Black.ToString(), displayStyle, 52f, bc, TextAnchor.MiddleLeft);
            T(1010f, 100f, 92f, 20f, Loc.T("백팀"), bodyStyle, 12f, RLabel, TextAnchor.MiddleRight);
            T(1136f, 100f, 92f, 20f, Loc.T("흑팀"), bodyStyle, 12f, RLabel, TextAnchor.MiddleLeft);
            rdy = 0f;
        }

        void DrawResultHero(float t)
        {
            float k = t - ResultHeroAt;
            if (k < 0f) return;
            rfade = Mathf.Clamp01(k / .3f);
            rdx = -70f * (1f - OutCubic(k / .45f));
            const float x = 50f, y = 40f, w = 400f, hh = 572f;
            P(x, y, w, hh, rWood, 12);
            // the ribbon
            string title = Loc.T("최고의 기물");
            displayStyle.fontSize = Fs(20f);
            float rw = displayStyle.CalcSize(new GUIContent(title)).x / rs + 44f;
            P(x + (w - rw) / 2f, y + 18f, rw, 34f, rGoldBtn, 8);
            T(x, y + 18f, w, 34f, title, displayStyle, 20f, RDeep, TextAnchor.MiddleCenter);

            var best = result.Complete ? result.Find(result.Best) : null;
            if (best == null)
            {
                string why = result.Complete ? Loc.T("이번 경기에는 상대를 장외시킨 기물이 없습니다.") : Loc.T("경기 도중에 다시 들어와 개인 기록을 모두 받지 못했습니다.");
                var r = S(x + 36f, y + 200f, w - 72f, 120f);
                bodyStyle.wordWrap = true;
                Text(r, why, bodyStyle, Fs(15f), Fade(RMuted), TextAnchor.MiddleCenter);
                bodyStyle.wordWrap = false;
                rdx = 0f;
                return;
            }
            // the piece, standing in a pool of light: the match's own model (user 11-27: "기물을 실제 인게임 기물로 교체"),
            // the icon only where the model cannot be made
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, rfade);
            GUI.DrawTexture(S(x + 30f, y + 70f, w - 60f, 300f), rGlow);
            GUI.color = old;
            float pop = OutBack((k - .15f) / .5f);
            var figureTexture = FigureFor(best);
            if (figureTexture != null)
            {
                float fw = (w - 60f) * Mathf.Max(.01f, pop), fh = 300f * Mathf.Max(.01f, pop);
                GUI.color = new Color(1f, 1f, 1f, rfade);
                GUI.DrawTexture(S(x + (w - fw) / 2f, y + 62f + (300f - fh), fw, fh), figureTexture, ScaleMode.ScaleToFit, true);
                GUI.color = old;
            }
            else
            {
                float ps = 210f * Mathf.Max(.01f, pop);
                Icon(x + (w - ps) / 2f, y + 80f + (230f - ps), ps, best.Piece, best.Team);
            }

            T(x + 20f, y + 366f, w - 40f, 44f, best.Name, NameStyle(best.Name), 32f, RCream, TextAnchor.MiddleCenter);
            string side = IsWhite(best.Team) ? Loc.T("백팀") : Loc.T("흑팀");
            T(x + 20f, y + 408f, w - 40f, 22f, $"{side} · {Loc.T(ChessPieces.Name(best.Piece))}", bodyStyle, 14f, RLabel, TextAnchor.MiddleCenter);
            if (best.Self) Tag(x + (w - TagWidth(Loc.T("나"))) / 2f, y + 434f, Loc.T("나"), rMeTag, SwordFightShapes.Hex("#06253a"));

            // three numbers along the foot of the card
            var stats = new (string label, int value)[] { (Loc.T("장외"), best.Knockouts), (Loc.T("낙하"), best.Falls), (Loc.T("최다 연속"), best.BestStreak) };
            float cw = (w - 40f) / 3f;
            for (int i = 0; i < stats.Length; i++)
            {
                float cx = x + 20f + i * cw;
                T(cx, y + 466f, cw, 56f, stats[i].value.ToString(), displayStyle, 40f, RGold, TextAnchor.MiddleCenter);
                T(cx, y + 524f, cw, 20f, stats[i].label, bodyStyle, 12f, RLabel, TextAnchor.MiddleCenter);
            }
            rdx = 0f;
        }

        void DrawResultAwards(float t)
        {
            if (!result.Complete) return;
            const float x = 486f, y = 152f, gap = 12f, ch = 150f;
            float cw = (744f - gap * 2f) / 3f;
            for (int i = 0; i < result.Awards.Count; i++)
            {
                float k = t - ResultAwardsAt - i * ResultAwardStep;
                if (k < 0f) continue;
                var a = result.Awards[i];
                var who = result.Find(a.Player);
                if (who == null) continue;
                rfade = Mathf.Clamp01(k / .25f);
                rdy = -26f * (1f - OutBack(k / .45f));
                float cx = x + i * (cw + gap);
                P(cx, y, cw, ch, rWood, 12);
                T(cx + 18f, y + 16f, cw - 36f, 30f, AwardTitle(a.Award), displayStyle, 23f, RGold);
                Pip(cx + 18f, y + 58f, 28f, who.Piece, who.Team);
                T(cx + 54f, y + 58f, cw - 72f, 28f, who.Name, rCell, 16f, RCream);
                var r = S(cx + 18f, y + 94f, cw - 36f, 44f);
                bodyStyle.wordWrap = true;
                Text(r, AwardLine(a), bodyStyle, Fs(12.5f), Fade(RMuted), TextAnchor.UpperLeft);
                bodyStyle.wordWrap = false;
            }
            rdy = 0f;
        }

        static string AwardTitle(SwordAward award)
        {
            switch (award)
            {
                case SwordAward.FinishingBlow: return Loc.T("결정타");
                case SwordAward.IronWall: return Loc.T("철벽");
                case SwordAward.FreeFall: return Loc.T("자유 낙하");
                case SwordAward.FirstBlood: return Loc.T("선취점");
                case SwordAward.Streak: return Loc.T("연속 장외");
                default: return Loc.T("성벽 파괴자");
            }
        }

        static string AwardLine(SwordAwardPick a)
        {
            switch (a.Award)
            {
                case SwordAward.FinishingBlow: return Loc.F("승부를 끝낸 마지막 한 점 · {0}", MeansShown(a.Cause, a.CausePiece));
                case SwordAward.IronWall: return a.Count == 0 ? Loc.T("한 번도 떨어지지 않았습니다") : Loc.T("단 한 번만 떨어졌습니다");
                case SwordAward.FreeFall: return Loc.F("스스로 떨어짐 {0}회", a.Count);
                case SwordAward.FirstBlood: return Loc.F("경기의 첫 장외 · {0}", MeansShown(a.Cause, a.CausePiece));
                case SwordAward.Streak: return Loc.F("떨어지지 않고 연속 장외 {0}회", a.Count);
                default: return Loc.F("성벽 붕괴로 장외 {0}회", a.Count);
            }
        }

        void DrawResultBoard(float t, int myTeam)
        {
            float k = t - ResultBoardAt;
            if (k < 0f) return;
            rfade = Mathf.Clamp01(k / .3f);
            rdy = 30f * (1f - OutCubic(k / .45f));
            const float x = 486f, y = 318f, w = 744f, hh = 294f;
            P(x, y, w, hh, rWood, 12);
            float half = (w - 48f - 20f) / 2f;
            for (int team = 0; team < 2; team++)
            {
                float tx = x + 24f + team * (half + 20f), ty = y + 16f;
                bool winners = team == view.Winner;
                T(tx, ty, 120f, 34f, IsWhite(team) ? Loc.T("백팀") : Loc.T("흑팀"), displayStyle, 22f, RCream);
                T(tx + 90f, ty + 6f, 120f, 28f, team == myTeam ? Loc.T("우리 팀") : Loc.T("상대 팀"), bodyStyle, 11f, team == myTeam ? RMe : RLabel);
                T(tx + half - 80f, ty - 4f, 80f, 40f, (team == 0 ? view.White : view.Black).ToString(), displayStyle, 34f, winners ? RGold : RMuted, TextAnchor.MiddleRight);
                var oldc = GUI.color;
                GUI.color = Fade(new Color(RBrass.r, RBrass.g, RBrass.b, .4f));
                GUI.DrawTexture(S(tx, ty + 40f, half, 1f), Texture2D.whiteTexture);
                GUI.color = oldc;

                // the columns: piece, name (with its tags), ring-outs, falls (user 11-27: no main means)
                float nameW = half - 30f - 52f - 52f - 8f, c1 = tx + 30f + nameW + 8f, c2 = c1 + 52f;
                if (result.Complete)
                {
                    T(c1, ty + 46f, 52f, 16f, Loc.T("장외"), bodyStyle, 10.5f, RLabel, TextAnchor.MiddleCenter);
                    T(c2, ty + 46f, 52f, 16f, Loc.T("낙하"), bodyStyle, 10.5f, RLabel, TextAnchor.MiddleCenter);
                }
                float ry = ty + 66f;
                int n = 0;
                foreach (var p in result.Team(team))
                {
                    if (n++ >= 6) break;
                    bool best = result.Complete && p.Id == result.Best, top = best;
                    P(tx, ry, half, 32f, best ? rRowTop : rRow, 4);
                    Pip(tx + 4f, ry + 3f, 26f, p.Piece, p.Team);
                    float nx = tx + 36f;
                    rCell.fontSize = Fs(14f);
                    float nameNeed = rCell.CalcSize(new GUIContent(p.Name)).x / rs + 2f;
                    float meW = p.Self ? TagWidth(Loc.T("나")) + 4f : 0f, topW = top ? TagWidth(Loc.T("최고의 기물")) : 0f;
                    // a long name keeps its room: the gold tag goes first (the row is gold already), then the name is cut
                    if (top && nameNeed + 6f + meW + topW > nameW) { top = false; topW = 0f; }
                    float nameFit = Mathf.Max(20f, Mathf.Min(nameW - 6f - meW - topW, nameNeed));
                    T(nx, ry, nameFit, 32f, p.Name, rCell, 14f, p.Self ? RMe : RCream);
                    float tagX = nx + nameFit + 6f;
                    if (p.Self) tagX += Tag(tagX, ry + 7.5f, Loc.T("나"), rMeTag, SwordFightShapes.Hex("#06253a")) + 4f;
                    if (top) Tag(tagX, ry + 7.5f, Loc.T("최고의 기물"), rGoldTag, RDeep);
                    if (result.Complete)
                    {
                        T(c1, ry, 52f, 32f, p.Knockouts.ToString(), displayStyle, 18f, RCream, TextAnchor.MiddleCenter);
                        T(c2, ry, 52f, 32f, p.Falls.ToString(), displayStyle, 18f, RCream, TextAnchor.MiddleCenter);
                    }
                    ry += 35f;
                }
            }
            if (!result.Complete)
                T(x + 24f, y + hh - 30f, w - 48f, 20f, Loc.T("경기 도중에 다시 들어와 개인 기록을 모두 받지 못했습니다."), bodyStyle, 12f, RMuted);
            else if (result.Players.Exists(p => p.SelfFalls > 0))
                T(x + 24f, y + hh - 28f, w - 48f, 20f, Loc.T("스스로 떨어진 점수는 상대 팀 점수에만 더해집니다."), bodyStyle, 11.5f, RLabel);
            rdy = 0f;
        }

        // While the panel is hidden: the result and the score in a small plate at the top left.
        void DrawResultMini(bool won, bool draw)
        {
            rfade = 1f;
            P(22f, 18f, 300f, 46f, rWood, 10);
            T(36f, 18f, 130f, 46f, ResultWord(won, draw), displayStyle, 22f, won ? RGold : RGrey);
            T(150f, 18f, 160f, 46f, $"{view.White} : {view.Black}", displayStyle, 22f, RCream, TextAnchor.MiddleRight);
        }

        void DrawResultBar(float w, float h, float t, float buttonsAt)
        {
            float k = t - (buttonsAt - .4f);
            if (k < 0f) return;
            rfade = Mathf.Clamp01(k / .35f);
            rdy = 40f * (1f - OutCubic(k / .45f));
            // the bar spans the screen; what is on it sits on the stage
            float barTop = roy + (656f + rdy) * rs;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, rfade);
            Panel(new Rect(0f, barTop, w, h - barTop + 1f), rBar, 1);
            GUI.color = old;
            bool live = t >= buttonsAt;
            bool online = LabNetLink.SwordOnline;

            // online: back to the lobby by itself, the thin gold line along the top running out
            if (online && live)
            {
                float left = Mathf.Clamp01(1f - (t - buttonsAt) / ResultLobbySeconds);
                var oc = GUI.color;
                GUI.color = Fade(RGold);
                GUI.DrawTexture(new Rect(0f, barTop, w * left, Mathf.Max(2f, 3f * rs)), Texture2D.whiteTexture);
                GUI.color = oc;
                if (left <= 0f && !resultLeft) { resultLeft = true; game.Exit(); return; }
                T(560f, 656f, 440f, 64f, Loc.T("곧 로비로 돌아갑니다"), bodyStyle, 13f, SwordFightShapes.Hex("#e3d2ba"), TextAnchor.MiddleRight);
            }

            // 결과판 숨기기 H
            string hide = resultHidden ? Loc.T("결과판 보기") : Loc.T("결과판 숨기기");
            displayStyle.fontSize = Fs(16f);
            float hw = displayStyle.CalcSize(new GUIContent(hide)).x / rs + 66f;
            P(22f, 666f, hw, 44f, rWoodBtn, 8);
            T(38f, 666f, hw - 54f, 44f, hide, displayStyle, 16f, RCream);
            P(22f + hw - 40f, 678f, 24f, 20f, rCap, 4);
            T(22f + hw - 40f, 678f, 24f, 20f, "H", bodyStyle, 11f, RCream, TextAnchor.MiddleCenter);
            if (live && GUI.Button(S(22f, 666f, hw, 44f), GUIContent.none, GUIStyle.none)) resultHidden = !resultHidden;

            // back to the lobby
            string go = Loc.T("로비로 돌아가기");
            displayStyle.fontSize = Fs(19f);
            float gw = displayStyle.CalcSize(new GUIContent(go)).x / rs + 48f;
            float gx = 1258f - gw;
            P(gx, 664f, gw, 48f, rGoldBtn, 8);
            T(gx, 664f, gw, 46f, go, displayStyle, 19f, RDeep, TextAnchor.MiddleCenter);
            if (live && !resultLeft && GUI.Button(S(gx, 664f, gw, 48f), GUIContent.none, GUIStyle.none)) { resultLeft = true; game.Exit(); }
            rdy = 0f;
        }
    }

    /// <summary>Lets the result figure's render texture go with its stage (a new round, the match scene gone).</summary>
    sealed class ResultFigureTexture : MonoBehaviour
    {
        public RenderTexture Texture;

        void OnDestroy()
        {
            if (Texture == null) return;
            Texture.Release();
            Destroy(Texture);
        }
    }
}
