using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The prototype match's HUD in the demo's look (R149, Docs/GameModes/SwordFight/DEMO.md), drawn with IMGUI:
    /// cream panels with a brass line; at the top the score panel (each side's pieces as round pips, dim and dropped while
    /// out; "3 : 2" and the clock "1:23 / 4:00", red for the last 30 s, a tick each of the last 10); a pill name tag over
    /// every head (the piece, the name, a gold "E" when its edge skill is ready, enemies' too); the big tilted stamp
    /// ("장외!" with who and how, "빼앗음!", "반격!", "귀환!", "역전!", "구원!", "성벽 붕괴!", the winner) that pops in and
    /// fades; the caption panel at the bottom (here: what this player can do now); the pick cards, the 3·2·1 and the end
    /// card.
    /// </summary>
    public sealed partial class SwordFightMatch
    {
        static readonly Color Ink = SwordFightShapes.Hex("#2b1f17"), InkSoft = SwordFightShapes.Hex("#6a5443"), StampFill = SwordFightShapes.Hex("#f4e2b0"),
            StampLine = SwordFightShapes.Hex("#3a2414"), Cream = SwordFightShapes.Hex("#f3ecdd"), Gold = SwordFightShapes.Hex("#f2c14e");

        Font display;
        GUIStyle displayStyle, bodyStyle;
        Texture2D panelTex, pipW, pipB, pillW, pillB, badgeTex, dimTex, brassTex;
        readonly Dictionary<int, Texture2D> icons = new Dictionary<int, Texture2D>();
        int feedSeen;
        AudioSource tick;
        int lastTickSecond = -1;

        // The stamp: one at a time, the newest replaces it (as the demo).
        string stampTitle, stampSub;
        float stampAt = -99f, stampLen = 1f;
        readonly List<(float at, string title, string sub, float len)> stampQueue = new List<(float, string, string, float)>();
        bool endStamped;

        static string Team(int team) => team == 0 ? "흰" : "검은";

        string NameOf(ulong id) => id != 0 && game.Fighters.TryGetValue(id, out var f) && f != null ? f.Pawn.DisplayName : "?";

        static string CauseName(SwordFeed e)
        {
            var piece = (PieceKind)e.CausePiece;
            switch (e.Cause)
            {
                case SwordCause.Sword: return "칼";
                case SwordCause.Skill: return SwordFightSkills.Name(piece);
                case SwordCause.Edge: return SwordFightSkills.EdgeName(piece);
                case SwordCause.PawnStep: return "폰 걸음";
                case SwordCause.Collapse: return "성벽 붕괴";
                case SwordCause.Debug: return "F7";
                default: return "";
            }
        }

        void Stamp(string title, string sub, float len, float delay = 0f) => stampQueue.Add((Time.time + delay, title, sub, len));

        /// <summary>New lines of the feed: "장외!" with who and how, then "빼앗음!" a quarter second later.</summary>
        void StepFeed()
        {
            foreach (var e in view.Feed)
            {
                if (e.Seq <= feedSeen) continue;
                feedSeen = e.Seq;
                string who = e.Credit == 0 ? $"{NameOf(e.Victim)} — 스스로" : $"{NameOf(e.Victim)} ← {NameOf(e.Credit)}의 {CauseName(e)}";
                Stamp("장외!", $"{who} · {Team(1 - e.VictimTeam)} 팀 +1", 1.1f);
                if (e.Stolen != 255 && e.Credit != 0)
                {
                    Stamp("빼앗음!", $"{NameOf(e.Credit)} → {ChessPieces.Name((PieceKind)e.Stolen)}", 1.2f, 0.25f);
                    game.Fighters.TryGetValue(e.Credit, out var credit);
                    StealPillar(credit);
                }
            }
            if (view.FeedSeq < feedSeen) feedSeen = view.FeedSeq;   // a new round
            if (view.Phase == SwordPhase.Result && !endStamped)
            {
                endStamped = true;
                string how = view.Phase == SwordPhase.Result && view.Clock < settings.Duration - 0.01 ? $"먼저 {settings.Target}점" : view.Clock > settings.Duration + 0.01 ? "서든데스" : "4:00 종료";
                Stamp(view.Winner < 0 ? "무승부" : $"{Team(view.Winner)} 팀 승리", $"{view.White} : {view.Black} · {how}", 3.6f, 0.05f);
            }
            if (view.Phase != SwordPhase.Result) endStamped = false;
            for (int i = stampQueue.Count - 1; i >= 0; i--)
            {
                if (Time.time < stampQueue[i].at) continue;
                (stampTitle, stampSub, stampLen) = (stampQueue[i].title, stampQueue[i].sub, stampQueue[i].len);
                stampAt = Time.time;
                stampQueue.RemoveAt(i);
            }
        }

        /// <summary>The demo's named moments (every screen hears the skills' moments).</summary>
        void NamedStamp(SfFxEvent e)
        {
            switch (e.kind)
            {
                case SfFxKind.KingParry: Stamp("반격!", "칼을 받아냄", 0.9f); break;
                case SfFxKind.KingStab: Stamp("귀환!", "칼을 박고 매달림", 0.9f); break;
                case SfFxKind.KnightFlip: Stamp("역전!", "나이트가 머리를 넘는다", 0.9f); break;
                case SfFxKind.BishopCatch: Stamp("구원!", "금색 손이 붙잡음", 0.9f); break;
                case SfFxKind.RookBreak: Stamp("성벽 붕괴!", "바닥째 무너진다", 0.9f); break;
            }
        }

        void StepClockSound()
        {
            if (view.Phase != SwordPhase.Playing) { lastTickSecond = -1; return; }
            int s = Mathf.CeilToInt(Remaining);
            if (s <= 10 && s > 0 && s != lastTickSecond && lastTickSecond != -1)
            {
                if (tick == null)
                {
                    tick = gameObject.AddComponent<AudioSource>();
                    tick.playOnAwake = false;
                    tick.spatialBlend = 0f;
                    const int rate = 44100;
                    var data = new float[rate / 12];
                    for (int i = 0; i < data.Length; i++) data[i] = Mathf.Sin(i * 2f * Mathf.PI * 1320f / rate) * Mathf.Exp(-i / (rate * 0.02f)) * 0.5f;
                    tick.clip = AudioClip.Create("Sword Fight tick", data.Length, 1, rate, false);
                    tick.clip.SetData(data, 0);
                }
                tick.Play();
            }
            lastTickSecond = s;
        }

        // ---------------------------------------------------------------- textures

        static Texture2D Rounded(int w, int h, float radius, Color fill, Color line, float lineWidth)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Distance inside a rounded rectangle.
                    float px = x + 0.5f, py = y + 0.5f;
                    float qx = Mathf.Max(radius - px, px - (w - radius), 0f), qy = Mathf.Max(radius - py, py - (h - radius), 0f);
                    float d = radius - Mathf.Sqrt(qx * qx + qy * qy);   // > 0 inside
                    float a = Mathf.Clamp01(d + 0.5f);
                    Color c = d < lineWidth ? line : fill;
                    c.a *= a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        Texture2D Icon(PieceKind kind, bool whiteStyle)
        {
            int key = (int)kind * 2 + (whiteStyle ? 1 : 0);
            if (icons.TryGetValue(key, out var have) && have != null) return have;
            Vector2[] outline = kind switch
            {
                PieceKind.King => ChessOutlines.King, PieceKind.Queen => ChessOutlines.Queen, PieceKind.Rook => ChessOutlines.Rook,
                PieceKind.Bishop => ChessOutlines.Bishop, PieceKind.Knight => ChessOutlines.Knight, _ => ChessOutlines.Pawn,
            };
            const int W = 40, H = 64;
            float s = H / 205f;
            var inside = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = new Vector2((x + 0.5f) / s - (W / s - 100f) / 2f, (H - y - 0.5f) / s);
                    bool inn = Inside(outline, p);
                    if (kind == PieceKind.Queen) foreach (var b in ChessOutlines.QueenBalls) inn |= (p - new Vector2(b.x, b.y)).magnitude <= b.z;
                    if (kind == PieceKind.Pawn) inn |= (p - new Vector2(ChessOutlines.PawnHead.x, ChessOutlines.PawnHead.y)).magnitude <= ChessOutlines.PawnHead.z;
                    inside[y * W + x] = inn;
                }
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool inn = inside[y * W + x];
                    bool edge = false;
                    if (inn)
                        for (int dy = -2; dy <= 2 && !edge; dy++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= W || ny >= H || !inside[ny * W + nx]) { edge = true; break; }
                            }
                    Color c = !inn ? Color.clear : whiteStyle ? (edge ? Ink : Cream) : Cream;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            icons[key] = t;
            return t;
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }

        void Styles()
        {
            if (panelTex != null) return;
            display = Resources.Load<Font>("Fonts/BlackHanSans-Regular");
            displayStyle = new GUIStyle(GUI.skin.label) { font = display, alignment = TextAnchor.MiddleCenter, richText = false };
            bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            sliced = new GUIStyle();
            panelTex = Rounded(32, 32, 10f, new Color(246 / 255f, 236 / 255f, 214 / 255f, 0.92f), SwordFightShapes.Hex("#b8924a"), 1.2f);
            pipW = Rounded(26, 26, 13f, Cream, SwordFightShapes.Hex("#c9a04c"), 1.5f);
            pipB = Rounded(26, 26, 13f, SwordFightShapes.Hex("#2a2421"), SwordFightShapes.Hex("#c9a04c"), 1.5f);
            pillW = Rounded(24, 18, 9f, new Color(243 / 255f, 236 / 255f, 221 / 255f, 0.92f), new Color(243 / 255f, 236 / 255f, 221 / 255f, 0.92f), 0f);
            pillB = Rounded(24, 18, 9f, new Color(32 / 255f, 27 / 255f, 24 / 255f, 0.9f), new Color(32 / 255f, 27 / 255f, 24 / 255f, 0.9f), 0f);
            badgeTex = Rounded(16, 14, 6f, Gold, Gold, 0f);
            brassTex = Rounded(16, 16, 8f, SwordFightShapes.Hex("#c9a04c"), SwordFightShapes.Hex("#b8924a"), 1f);
            dimTex = new Texture2D(1, 1);
            dimTex.SetPixel(0, 0, new Color(18 / 255f, 12 / 255f, 8 / 255f, 0.55f));
            dimTex.Apply();
        }

        GUIStyle sliced;   // made in OnGUI: no Unity object in a field initializer (PITFALLS 45)

        void Panel(Rect r, Texture2D tex, int border = 10)
        {
            sliced.normal.background = tex;
            sliced.border = new RectOffset(border, border, border, border);
            if (Event.current.type == EventType.Repaint) sliced.Draw(r, false, false, false, false);
        }

        void Text(Rect r, string text, GUIStyle style, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            style.fontSize = size;
            style.alignment = anchor;
            var old = GUI.color;
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        // ---------------------------------------------------------------- drawing

        void OnGUI()
        {
            if (game == null) return;
            Styles();
            float w = Screen.width, h = Screen.height, ui = Mathf.Clamp(h / 900f, 0.8f, 1.6f);
            var me = game.Local;
            int myTeam = me != null ? me.Pawn.Team : 0;

            DrawTags(ui);
            DrawScore(w, ui);
            DrawCaption(w, h, ui, me);
            switch (view.Phase)
            {
                case SwordPhase.Pick: DrawPick(w, h, ui, myTeam); break;
                case SwordPhase.Countdown: BigStamp(w, h, Mathf.Clamp(3 - (int)PhaseTime, 1, 3).ToString(), null, (float)(PhaseTime % 1.0), 1f); break;
                case SwordPhase.Playing:
                    if (view.Clock < 1.0 && PhaseTime < 1.0) BigStamp(w, h, "시작!", null, (float)PhaseTime, 1f);
                    break;
            }
            if (Time.time - stampAt < stampLen) BigStamp(w, h, stampTitle, stampSub, (Time.time - stampAt) / stampLen, ui);
            if (view.Phase == SwordPhase.Result && PhaseTime > 4.0) DrawEndCard(w, h, ui, myTeam);
            if (debugView) Text(new Rect(14f, 70f * ui, 600f, 22f), "F12 판정 표시: 칼 원뿔(노랑) · 장외 선(빨강) · 떨어지기 시작(머리 위)", bodyStyle, Mathf.RoundToInt(13 * ui), Color.yellow, TextAnchor.MiddleLeft);
        }

        void DrawScore(float w, float ui)
        {
            var whites = new List<SwordMatchFighter>();
            var blacks = new List<SwordMatchFighter>();
            foreach (var f in view.Fighters)
                if (game.Fighters.TryGetValue(f.Id, out var fighter) && fighter != null) (fighter.Pawn.Team == 0 ? whites : blacks).Add(f);
            float pip = 26f * ui, gap = 5f * ui, clockW = 92f * ui;
            float sideW = Mathf.Max(whites.Count, blacks.Count, 1) * (pip + gap) - gap;
            float panelW = sideW * 2f + clockW + 2f * 14f * ui + 32f * ui, panelH = 46f * ui;
            var r = new Rect(w * 0.5f - panelW / 2f, 14f * ui, panelW, panelH);
            Panel(r, panelTex);
            float x = r.x + 16f * ui, cy = r.y + panelH / 2f;
            for (int i = 0; i < whites.Count; i++) DrawPip(new Rect(x + sideW - (whites.Count - i) * (pip + gap) + gap, cy - pip / 2f, pip, pip), whites[i], true);
            for (int i = 0; i < blacks.Count; i++) DrawPip(new Rect(r.xMax - 16f * ui - sideW + i * (pip + gap), cy - pip / 2f, pip, pip), blacks[i], false);
            float clockX = w * 0.5f - clockW / 2f;
            Text(new Rect(clockX, r.y + 3f * ui, clockW, 26f * ui), $"{view.White} : {view.Black}", displayStyle, Mathf.RoundToInt(22 * ui), Ink);
            string small;
            if (view.Phase == SwordPhase.SuddenDeath) small = "서든데스";
            else { int g = Mathf.Clamp(Mathf.FloorToInt((float)(view.Phase == SwordPhase.Pick || view.Phase == SwordPhase.Countdown ? 0 : Clock)), 0, (int)settings.Duration); small = $"{g / 60}:{g % 60:00} / 4:00"; }
            Color c = view.Phase == SwordPhase.Playing && Remaining <= 30f ? SwordFightShapes.Hex("#c0392b") : InkSoft;
            Text(new Rect(clockX - 10f, r.y + 27f * ui, clockW + 20f, 16f * ui), small, bodyStyle, Mathf.RoundToInt(11 * ui), c);
        }

        void DrawPip(Rect r, SwordMatchFighter f, bool white)
        {
            bool gone = !f.Alive;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, gone ? 0.18f : 1f);
            if (gone) r.y += 4f;
            GUI.DrawTexture(r, white ? pipW : pipB);
            var piece = view.Phase == SwordPhase.Pick && !f.Picked ? PieceKind.Pawn : f.Piece;
            var icon = Icon(piece, white);
            float ih = r.height * 0.78f, iw = ih * icon.width / icon.height;
            GUI.DrawTexture(new Rect(r.center.x - iw / 2f, r.center.y - ih / 2f, iw, ih), icon);
            GUI.color = old;
        }

        void DrawTags(float ui)
        {
            var cam = game.CameraRig != null ? game.CameraRig.Cam : Camera.main;
            if (cam == null) return;
            float h = Screen.height;
            foreach (var f in game.Fighters.Values)
            {
                if (f == null || !f.Alive) continue;
                Vector3 s = cam.WorldToScreenPoint(f.Pawn.bodies[(int)BodyId.Head].position + Vector3.up * 0.5f);
                if (s.z < 0.2f) continue;
                var info = view.Find(f.Id);
                bool white = f.Pawn.Team == 0;
                var piece = view.Phase == SwordPhase.Pick && info.HasValue ? info.Value.Piece : f.Pawn.Piece;
                bool e = info.HasValue && info.Value.EReady;
                int size = Mathf.RoundToInt(11 * ui);
                bodyStyle.fontSize = size;
                float textW = bodyStyle.CalcSize(new GUIContent(f.Pawn.DisplayName)).x;
                float ih = 14f * ui, iw = ih * 40f / 64f, pad = 6f * ui, badgeW = e ? 16f * ui : 0f;
                float tagW = pad + iw + 3f * ui + textW + (e ? 3f * ui + badgeW : 0f) + pad, tagH = 18f * ui;
                var r = new Rect(s.x - tagW / 2f, h - s.y - tagH, tagW, tagH);
                Panel(r, white ? pillW : pillB, 8);
                GUI.DrawTexture(new Rect(r.x + pad, r.y + (tagH - ih) / 2f, iw, ih), Icon(piece, white));
                Text(new Rect(r.x + pad + iw + 3f * ui, r.y, textW + 2f, tagH), f.Pawn.DisplayName, bodyStyle, size, white ? Ink : Cream, TextAnchor.MiddleLeft);
                if (e)
                {
                    var br = new Rect(r.xMax - pad - badgeW, r.y + 2f * ui, badgeW, tagH - 4f * ui);
                    GUI.DrawTexture(br, badgeTex);
                    Text(br, "E", bodyStyle, size, Ink);
                }
                if (debugView) DrawFallStart(f, s, h);
            }
        }

        /// <summary>The demo's stamp: Black Han Sans, cream with a dark brown edge and drop, tilted 6°, popping in from 1.8×
        /// and fading at the end; a smaller line under it.</summary>
        void BigStamp(float w, float h, string title, string sub, float u, float ui)
        {
            if (string.IsNullOrEmpty(title) || u < 0f || u > 1f) return;
            float sc = 1f, op = 1f;
            if (u < 0.12f) { float q = u / 0.12f; sc = 1.8f - 0.85f * q; op = q; }
            else if (u < 0.2f) sc = 0.95f + 0.05f * (u - 0.12f) / 0.08f;
            else if (u > 0.82f) op = 1f - (u - 0.82f) / 0.18f;
            var pivot = new Vector2(w * 0.5f, h * 0.4f);
            var saved = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * sc, pivot);
            GUIUtility.RotateAroundPivot(-6f, pivot);
            int size = Mathf.RoundToInt(Mathf.Clamp(w * 0.11f, 54f, 120f));
            var r = new Rect(pivot.x - w, pivot.y - size * 0.6f, w * 2f, size * 1.2f);
            displayStyle.fontSize = size;
            displayStyle.alignment = TextAnchor.MiddleCenter;
            var old = GUI.color;
            // The drop under it, then the dark edge all round, then the cream face.
            GUI.color = new Color(StampLine.r, StampLine.g, StampLine.b, op);
            GUI.Label(new Rect(r.x, r.y + 6f, r.width, r.height), title, displayStyle);
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * 3f, r.y + Mathf.Sin(a) * 3f, r.width, r.height), title, displayStyle);
            }
            GUI.color = new Color(StampFill.r, StampFill.g, StampFill.b, op);
            GUI.Label(r, title, displayStyle);
            if (!string.IsNullOrEmpty(sub))
            {
                int subSize = Mathf.RoundToInt(size * 0.24f);
                var sr = new Rect(r.x, r.yMax + 2f, r.width, subSize * 1.6f);
                bodyStyle.fontSize = subSize;
                bodyStyle.alignment = TextAnchor.MiddleCenter;
                GUI.color = new Color(0f, 0f, 0f, 0.6f * op);
                GUI.Label(new Rect(sr.x + 1f, sr.y + 2f, sr.width, sr.height), sub, bodyStyle);
                GUI.color = new Color(1f, 0.957f, 0.84f, op);
                GUI.Label(sr, sub, bodyStyle);
            }
            GUI.color = old;
            GUI.matrix = saved;
        }

        /// <summary>The demo's caption panel at the bottom: here what this player has now (its piece, its F and E).</summary>
        void DrawCaption(float w, float h, float ui, SwordFightPawn me)
        {
            if (me == null || !Live(view.Phase)) return;
            string main, sub;
            var info = view.Find(me.Id);
            if (!me.Alive)
            {
                float left = info.HasValue ? Mathf.Max(0f, LeftFor(info.Value)) : me.RespawnSeconds;
                main = $"{left:0.0}초 뒤 폰으로 돌아온다";
                sub = "들고 있던 기물은 잃는다. 폰으로 상대를 떨어뜨리면 그 기물을 빼앗는다";
            }
            else
            {
                var sk = me.Skills;
                var piece = me.Pawn.Piece;
                string f = piece == PieceKind.Pawn ? "F 첫 두 걸음" : $"F {SwordFightSkills.Name(piece)}";
                if (sk != null) f += sk.Cooldown > 0f ? $" {sk.Cooldown:0.0}초" : " 준비";
                string e = piece == PieceKind.Pawn ? "E 없음" : $"E {SwordFightSkills.EdgeName(piece)}" + (sk != null && sk.EdgeCooldown > 0f ? $" {sk.EdgeCooldown:0.0}초" : " 준비");
                main = $"{ChessPieces.Name(piece)} · {f} · {e}";
                sub = piece == PieceKind.Pawn ? "대각선으로 부딪히면 넘어뜨린다. 둘째 걸음은 방향을 바꿀 수 있다" : "좌클릭 베기 · F 조준 뒤 좌클릭 발동, 우클릭 취소 · E는 떨어지기 시작할 때";
            }
            float pw = Mathf.Min(760f * ui, w - 24f), ph = 58f * ui;
            var r = new Rect(w * 0.5f - pw / 2f, h - ph - 14f * ui, pw, ph);
            Panel(r, panelTex);
            Text(new Rect(r.x + 18f * ui, r.y + 8f * ui, pw - 36f * ui, 26f * ui), main, displayStyle, Mathf.RoundToInt(20 * ui), Ink, TextAnchor.MiddleLeft);
            Text(new Rect(r.x + 18f * ui, r.y + 33f * ui, pw - 36f * ui, 18f * ui), sub, bodyStyle, Mathf.RoundToInt(12 * ui), InkSoft, TextAnchor.MiddleLeft);
        }

        void DrawPick(float w, float h, float ui, int myTeam)
        {
            float left = Mathf.Max(0f, (float)(settings.PickSeconds - PhaseTime));
            var pieces = SwordMatchRules.Pickable;
            float cw = 128f * ui, chh = 190f * ui, gap = 12f * ui;
            float pw = pieces.Length * cw + (pieces.Length - 1) * gap + 48f * ui, ph = chh + 120f * ui;
            var r = new Rect(w * 0.5f - pw / 2f, h * 0.5f - ph / 2f, pw, ph);
            Panel(r, panelTex);
            Text(new Rect(r.x, r.y + 10f * ui, pw, 34f * ui), "시작할 때 기물을 고른다", displayStyle, Mathf.RoundToInt(26 * ui), Ink);
            Text(new Rect(r.x, r.y + 42f * ui, pw, 20f * ui), $"같은 팀 안에서는 겹치지 않는다 · {left:0}초", bodyStyle, Mathf.RoundToInt(13 * ui), InkSoft);
            var mine = view.Find(game.LocalId);
            bool picked = mine.HasValue && mine.Value.Picked;
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                ulong takenBy = 0;
                foreach (var f in view.Fighters)
                    if (f.Picked && f.Piece == p && f.Id != game.LocalId && game.Fighters.TryGetValue(f.Id, out var o) && o != null && o.Pawn.Team == myTeam) takenBy = f.Id;
                bool mineNow = picked && mine.Value.Piece == p;
                bool off = takenBy != 0 || (picked && !mineNow);
                var cr = new Rect(r.x + 24f * ui + i * (cw + gap), r.y + 72f * ui, cw, chh);
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, off ? 0.4f : 1f);
                Panel(cr, mineNow ? brassTex : myTeam == 0 ? pipW : pipB, 8);
                var icon = Icon(p, myTeam == 0);
                float ih = 64f * ui, iw = ih * icon.width / icon.height;
                GUI.DrawTexture(new Rect(cr.center.x - iw / 2f, cr.y + 12f * ui, iw, ih), icon);
                Color tc = myTeam == 0 || mineNow ? Ink : Cream;
                Text(new Rect(cr.x, cr.y + 80f * ui, cw, 26f * ui), $"{i + 1}  {ChessPieces.Name(p)}", displayStyle, Mathf.RoundToInt(20 * ui), tc);
                Text(new Rect(cr.x + 6f, cr.y + 108f * ui, cw - 12f, 34f * ui), $"F {SwordFightSkills.Name(p)}", bodyStyle, Mathf.RoundToInt(11 * ui), tc);
                Text(new Rect(cr.x + 6f, cr.y + 136f * ui, cw - 12f, 34f * ui), $"E {SwordFightSkills.EdgeName(p)}", bodyStyle, Mathf.RoundToInt(11 * ui), tc);
                if (takenBy != 0) Text(new Rect(cr.x, cr.yMax - 22f * ui, cw, 18f * ui), NameOf(takenBy), bodyStyle, Mathf.RoundToInt(11 * ui), tc);
                GUI.color = old;
                if (!picked && takenBy == 0 && GUI.Button(cr, GUIContent.none, GUIStyle.none)) PickLocal(p);
            }
            string waiting = picked ? "골랐다. 다른 사람을 기다리는 중" : pickAsked.HasValue ? "방장에게 보내는 중" : "클릭하거나 1~5";
            Text(new Rect(r.x, r.yMax - 36f * ui, pw, 24f * ui), waiting, bodyStyle, Mathf.RoundToInt(13 * ui), InkSoft);
        }

        void DrawEndCard(float w, float h, float ui, int myTeam)
        {
            GUI.DrawTexture(new Rect(0f, 0f, w, h), dimTex);
            float pw = Mathf.Min(560f * ui, w - 40f), ph = 300f * ui;
            var r = new Rect(w * 0.5f - pw / 2f, h * 0.5f - ph / 2f, pw, ph);
            Panel(r, panelTex);
            string how = view.Clock > settings.Duration + 0.01 ? "서든데스" : view.Clock < settings.Duration - 0.01 ? $"{settings.Target}점" : "4:00 종료";
            string title = view.Winner < 0 ? $"{how} — 무승부 {view.White} : {view.Black}" : $"{how} — {Team(view.Winner)} 팀 승리 {view.White} : {view.Black}";
            float x = r.x + 24f * ui, iw = pw - 48f * ui, y = r.y + 22f * ui;
            Text(new Rect(x, y, iw, 38f * ui), title, displayStyle, Mathf.RoundToInt(28 * ui), Ink, TextAnchor.MiddleLeft);
            y += 42f * ui;
            string mine = view.Winner < 0 ? "비겼다." : view.Winner == myTeam ? "우리 팀이 이겼다." : "우리 팀이 졌다.";
            Text(new Rect(x, y, iw, 22f * ui), mine + " 떨어뜨린 수가 점수, 폰으로 떨어뜨리면 그 기물을 빼앗는다.", bodyStyle, Mathf.RoundToInt(13 * ui), InkSoft, TextAnchor.MiddleLeft);
            y += 34f * ui;
            Row(x, ref y, iw, ui, "흰 팀", $"장외로 얻은 점수 {view.OutsWhite} · 빼앗기 {view.StealsWhite}");
            Row(x, ref y, iw, ui, "검은 팀", $"장외로 얻은 점수 {view.OutsBlack} · 빼앗기 {view.StealsBlack}");
            var br = new Rect(x, r.yMax - 64f * ui, 200f * ui, 42f * ui);
            Panel(br, brassTex, 7);
            Text(br, "로비로 돌아가기", bodyStyle, Mathf.RoundToInt(14 * ui), Ink);
            if (GUI.Button(br, GUIContent.none, GUIStyle.none)) game.Exit();
        }

        void Row(float x, ref float y, float w, float ui, string head, string body)
        {
            Text(new Rect(x, y, 90f * ui, 26f * ui), head, bodyStyle, Mathf.RoundToInt(14 * ui), Ink, TextAnchor.MiddleLeft);
            Text(new Rect(x + 90f * ui, y, w - 90f * ui, 26f * ui), body, bodyStyle, Mathf.RoundToInt(14 * ui), Ink, TextAnchor.MiddleLeft);
            y += 32f * ui;
        }
    }
}
