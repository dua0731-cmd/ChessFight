using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The prototype match's HUD, the spec's minimum (§11), drawn with IMGUI: just enough for the rules to read. At the
    /// top the score "3 : 2", the time left (red for the last 30 s, a tick each of the last 10) and every piece of both
    /// sides (dim while out); over each head its name and piece, and a gold "E" when its edge skill is ready (enemies see
    /// it too); "장외! +1" and a kill feed line for each fall; "빼앗음! 나이트" and a gold light pillar for a steal; the pick
    /// cards, the 3·2·1, and the result (the winner, the score, each side's falls and steals).
    /// </summary>
    public sealed partial class SwordFightMatch
    {
        GUIStyle big, mid, small, head, card, cardOff, button;
        Texture2D panelTex;
        int feedSeen;
        readonly List<(string text, float at)> feedLines = new List<(string, float)>();
        string stamp;
        float stampAt = -10f;
        readonly Dictionary<ulong, (string text, float at)> heads = new Dictionary<ulong, (string, float)>();
        AudioSource tick;
        int lastTickSecond = -1;

        static readonly Color WhiteSide = new Color(0.97f, 0.94f, 0.85f), BlackSide = new Color(0.35f, 0.42f, 0.55f), Gold = new Color(1f, 0.82f, 0.25f);

        string NameOf(ulong id)
        {
            if (id != 0 && game.Fighters.TryGetValue(id, out var f) && f != null) return f.Pawn.DisplayName;
            return "?";
        }

        static string Side(int team) => team == 0 ? "백" : "흑";

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
                default: return "스스로";
            }
        }

        /// <summary>New lines of the feed (the host's own or a client's from the wire): the stamp, the feed, the steal.</summary>
        void StepFeed()
        {
            foreach (var e in view.Feed)
            {
                if (e.Seq <= feedSeen) continue;
                feedSeen = e.Seq;
                string victim = $"{Side(e.VictimTeam)} {NameOf(e.Victim)}";
                string line = e.Credit == 0 ? $"{victim} — 스스로" : $"{victim} ← {Side(1 - e.VictimTeam)} {NameOf(e.Credit)}의 {CauseName(e)}";
                if (e.Stolen != 255) line += $" · 빼앗음 {ChessPieces.Name((PieceKind)e.Stolen)}";
                feedLines.Add((line, Time.time));
                if (feedLines.Count > 5) feedLines.RemoveAt(0);
                stamp = "장외! +1";
                stampAt = Time.time;
                if (e.Stolen != 255 && e.Credit != 0)
                {
                    heads[e.Credit] = ($"빼앗음! {ChessPieces.Name((PieceKind)e.Stolen)}", Time.time);
                    game.Fighters.TryGetValue(e.Credit, out var credit);
                    StealPillar(credit);
                }
            }
            if (view.FeedSeq < feedSeen) feedSeen = view.FeedSeq;   // a new round
        }

        void StepClockSound()
        {
            if (!Live(view.Phase) || view.Phase == SwordPhase.SuddenDeath) { lastTickSecond = -1; return; }
            int s = Mathf.CeilToInt(Remaining);
            if (s <= 10 && s > 0 && s != lastTickSecond)
            {
                if (lastTickSecond != -1)
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
            }
            lastTickSecond = s;
        }

        void Styles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            mid = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            small = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            head = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            card = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
            cardOff = new GUIStyle(card);
            cardOff.normal.textColor = cardOff.hover.textColor = new Color(1f, 1f, 1f, 0.35f);
            panelTex = new Texture2D(1, 1);
            panelTex.SetPixel(0, 0, new Color(0.06f, 0.05f, 0.04f, 0.72f));
            panelTex.Apply();
        }

        void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = old;
        }

        void OnGUI()
        {
            if (game == null) return;
            Styles();
            float w = Screen.width, h = Screen.height;
            var me = game.Local;
            int myTeam = me != null ? me.Pawn.Team : 0;

            // ---- the top: score, clock, pieces
            GUI.DrawTexture(new Rect(w * 0.5f - 230f, 6f, 460f, 92f), panelTex);
            Shadowed(new Rect(w * 0.5f - 200f, 8f, 400f, 46f), $"{view.White}  :  {view.Black}", big, Color.white);
            Shadowed(new Rect(w * 0.5f - 230f, 12f, 120f, 40f), "백", mid, WhiteSide);
            Shadowed(new Rect(w * 0.5f + 110f, 12f, 120f, 40f), "흑", mid, BlackSide);
            string clock;
            if (view.Phase == SwordPhase.SuddenDeath) clock = $"서든데스 · 다음 1점이 승리 ({Mathf.Max(0, 60 - (int)PhaseTime)}초)";
            else { int left = Mathf.CeilToInt(Remaining); clock = $"{left / 60}:{left % 60:00}  ·  먼저 {settings.Target}점"; }
            Color clockColor = Live(view.Phase) && Remaining <= 30f ? new Color(1f, 0.3f, 0.25f) : Color.white;
            Shadowed(new Rect(w * 0.5f - 200f, 50f, 400f, 24f), clock, small.Centered(), clockColor);
            DrawPieces(new Rect(w * 0.5f - 225f, 72f, 220f, 24f), 0, TextAnchor.MiddleRight);
            DrawPieces(new Rect(w * 0.5f + 5f, 72f, 220f, 24f), 1, TextAnchor.MiddleLeft);

            // ---- over the heads
            var cam = game.CameraRig != null ? game.CameraRig.Cam : Camera.main;
            if (cam != null)
                foreach (var f in game.Fighters.Values)
                {
                    if (f == null || !f.Alive) continue;
                    Vector3 p = f.Pawn.bodies[(int)BodyId.Head].position + Vector3.up * 0.55f;
                    Vector3 s = cam.WorldToScreenPoint(p);
                    if (s.z < 0.2f) continue;
                    var info = view.Find(f.Id);
                    PieceKind piece = info.HasValue ? info.Value.Piece : f.Pawn.Piece;
                    if (view.Phase != SwordPhase.Pick) piece = f.Pawn.Piece;
                    string label = $"{f.Pawn.DisplayName} · {ChessPieces.Name(piece)}";
                    var r = new Rect(s.x - 110f, h - s.y - 14f, 220f, 22f);
                    Shadowed(r, label, head, f.Pawn.Team == 0 ? WhiteSide : new Color(0.7f, 0.78f, 0.95f));
                    if (info.HasValue && info.Value.EReady)
                        Shadowed(new Rect(s.x - 12f, h - s.y - 36f, 24f, 22f), "E", head, Gold);
                    if (heads.TryGetValue(f.Id, out var note) && Time.time - note.at < 1.6f)
                        Shadowed(new Rect(s.x - 110f, h - s.y - 58f, 220f, 22f), note.text, head, Gold);
                    if (debugView) DrawFallStart(f, s, h);
                }

            // ---- the stamp and the feed
            if (Time.time - stampAt < 1.2f)
            {
                float k = Mathf.Clamp01((Time.time - stampAt) / 0.15f);
                var style = new GUIStyle(big) { fontSize = Mathf.RoundToInt(Mathf.Lerp(80f, 54f, k)) };
                Shadowed(new Rect(0f, h * 0.3f, w, 80f), stamp, style, new Color(1f, 0.85f, 0.3f, 1f - Mathf.Clamp01((Time.time - stampAt - 0.9f) / 0.3f)));
            }
            for (int i = 0; i < feedLines.Count; i++)
            {
                float age = Time.time - feedLines[i].at;
                if (age > 8f) continue;
                Shadowed(new Rect(w - 520f, 110f + i * 22f, 510f, 22f), feedLines[i].text, small, new Color(1f, 1f, 1f, Mathf.Clamp01(8f - age)));
            }

            // ---- my keys
            if (me != null && me.Skills != null && Live(view.Phase))
            {
                var sk = me.Skills;
                string f = me.Pawn.Piece == PieceKind.Pawn ? "F 첫 두 걸음" : $"F {SwordFightSkills.Name(me.Pawn.Piece)}";
                f += sk.Cooldown > 0f ? $" {sk.Cooldown:0.0}초" : " 준비";
                string e = me.Pawn.Piece == PieceKind.Pawn ? "E 없음 (폰)" : $"E {SwordFightSkills.EdgeName(me.Pawn.Piece)}" + (sk.EdgeCooldown > 0f ? $" {sk.EdgeCooldown:0.0}초" : " 준비");
                GUI.DrawTexture(new Rect(12f, h - 64f, 420f, 52f), panelTex);
                Shadowed(new Rect(22f, h - 62f, 410f, 24f), f, small, Color.white);
                Shadowed(new Rect(22f, h - 38f, 410f, 24f), e, small, sk.EdgeCooldown <= 0f && me.Pawn.Piece != PieceKind.Pawn ? Gold : Color.white);
            }
            if (me != null && !me.Alive && Live(view.Phase))
            {
                var info = view.Find(me.Id);
                if (info.HasValue) Shadowed(new Rect(0f, h * 0.62f, w, 30f), $"{info.Value.Respawn:0.0}초 뒤 폰으로 부활", mid, Color.white);
            }

            switch (view.Phase)
            {
                case SwordPhase.Pick: DrawPick(w, h, myTeam); break;
                case SwordPhase.Countdown:
                {
                    int n = Mathf.Clamp(3 - (int)PhaseTime, 1, 3);
                    Shadowed(new Rect(0f, h * 0.35f, w, 120f), n.ToString(), new GUIStyle(big) { fontSize = 110 }, Color.white);
                    break;
                }
                case SwordPhase.Playing:
                    if (PhaseTime < 1f && view.Clock < 1.5f) Shadowed(new Rect(0f, h * 0.35f, w, 120f), "시작!", new GUIStyle(big) { fontSize = 90 }, Gold);
                    break;
                case SwordPhase.Result:
                    if (PhaseTime > 1.5f || !Authority) DrawResult(w, h, myTeam);
                    break;
            }
            if (debugView) Shadowed(new Rect(12f, 110f, 500f, 22f), "F12 판정 표시: 칼 원뿔(노랑) · 장외 선(빨강) · 떨어지기 시작(머리 위 글자)", small, Color.yellow);
        }

        void DrawPieces(Rect r, int team, TextAnchor anchor)
        {
            var parts = new List<string>();
            foreach (var f in view.Fighters)
            {
                if (!game.Fighters.TryGetValue(f.Id, out var fighter) || fighter == null || fighter.Pawn.Team != team) continue;
                string name = view.Phase == SwordPhase.Pick && !f.Picked ? "?" : ChessPieces.Name(f.Piece);
                parts.Add(f.Alive ? name : $"<color=#ffffff55>{name}</color>");
            }
            var style = new GUIStyle(small) { alignment = anchor, richText = true };
            Shadowed(r, string.Join(" ", parts), style, team == 0 ? WhiteSide : new Color(0.7f, 0.78f, 0.95f));
        }

        void DrawPick(float w, float h, int myTeam)
        {
            float left = Mathf.Max(0f, (float)(settings.PickSeconds - PhaseTime));
            GUI.DrawTexture(new Rect(w * 0.5f - 400f, h * 0.5f - 150f, 800f, 300f), panelTex);
            Shadowed(new Rect(0f, h * 0.5f - 145f, w, 40f), $"기물을 고르세요 ({left:0}초) — 같은 팀끼리는 겹칠 수 없어요", mid, Color.white);
            var mine = view.Find(game.LocalId);
            bool picked = mine.HasValue && mine.Value.Picked;
            var pieces = SwordMatchRules.Pickable;
            float cw = 140f, gap = 12f, x0 = w * 0.5f - (pieces.Length * cw + (pieces.Length - 1) * gap) * 0.5f;
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                ulong takenBy = 0;
                foreach (var f in view.Fighters)
                    if (f.Picked && f.Piece == p && f.Id != game.LocalId && game.Fighters.TryGetValue(f.Id, out var o) && o != null && o.Pawn.Team == myTeam) takenBy = f.Id;
                bool mineNow = picked && mine.Value.Piece == p;
                string text = $"{i + 1}\n{ChessPieces.Name(p)}\n<size=13>F {SwordFightSkills.Name(p)}\nE {SwordFightSkills.EdgeName(p)}</size>"
                              + (takenBy != 0 ? $"\n<size=12>{NameOf(takenBy)}</size>" : mineNow ? "\n<size=13>(내 기물)</size>" : "");
                var style = takenBy != 0 || (picked && !mineNow) ? cardOff : card;
                style.richText = true;
                var rect = new Rect(x0 + i * (cw + gap), h * 0.5f - 95f, cw, 200f);
                if (GUI.Button(rect, text, style) && !picked && takenBy == 0) PickLocal(p);
            }
            string waiting = picked ? "골랐어요. 다른 사람을 기다리는 중…" : pickAsked.HasValue ? "방장에게 보내는 중…" : "클릭하거나 1~5";
            Shadowed(new Rect(0f, h * 0.5f + 112f, w, 30f), waiting, small.Centered(), Color.white);
        }

        void DrawResult(float w, float h, int myTeam)
        {
            GUI.DrawTexture(new Rect(w * 0.5f - 320f, h * 0.5f - 170f, 640f, 340f), panelTex);
            string title = view.Winner < 0 ? "무승부" : view.Winner == myTeam ? "승리!" : "패배";
            string sub = view.Winner < 0 ? "" : $"{Side(view.Winner)}팀 승리";
            Shadowed(new Rect(0f, h * 0.5f - 160f, w, 70f), title, new GUIStyle(big) { fontSize = 60 }, view.Winner == myTeam ? Gold : Color.white);
            Shadowed(new Rect(0f, h * 0.5f - 90f, w, 30f), sub, mid, Color.white);
            Shadowed(new Rect(0f, h * 0.5f - 55f, w, 50f), $"백 {view.White}  :  {view.Black} 흑", big, Color.white);
            Shadowed(new Rect(0f, h * 0.5f + 5f, w, 26f), $"백팀  장외 {view.OutsWhite} · 빼앗기 {view.StealsWhite}", small.Centered(), WhiteSide);
            Shadowed(new Rect(0f, h * 0.5f + 33f, w, 26f), $"흑팀  장외 {view.OutsBlack} · 빼앗기 {view.StealsBlack}", small.Centered(), new Color(0.7f, 0.78f, 0.95f));
            if (GUI.Button(new Rect(w * 0.5f - 130f, h * 0.5f + 85f, 260f, 56f), "로비로 돌아가기", button)) game.Exit();
        }
    }

    static class GuiStyleExtensions
    {
        public static GUIStyle Centered(this GUIStyle s) => new GUIStyle(s) { alignment = TextAnchor.MiddleCenter };
    }
}
