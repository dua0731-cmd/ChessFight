using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R682 (the user after the 6 vs 6 test, 11-16: "현재 순위 실시간 표시", "코스 진행도 표시"; the mock-up
    // Docs/PawnRush/Mockups/R681_RankProgressHud.svg, version B chosen): the match's standings.
    //
    //   the strip   at the top middle - this PC's rank ("6위 / 12"), the round's sections as a track of two lanes
    //               (white team above, black below; a dot a racer, this PC's own larger with a brass ring and a
    //               marker over it; a mini-game's segment framed in brass and filled by each team's progress), the
    //               crown platform's count (which until now was CrownFinish's long box in this place)
    //   the list    at the top right - the first five racers and this PC's own
    //
    // Where a racer is: the section its pawn stands in (PawnRushRound.SectionAt) and how far along that section's
    // ProgressPath it is; in a mini-game its team's progress there (1 once its gate is open). Between two sections
    // (a corridor, a fall under the floor) it keeps the last section it was seen in. The racers are the finish's
    // (CrownFinish.Runners: the match link's or the playtest's). Nothing is sent - every PC has the pawns' places and
    // the stations' progress - and the arithmetic is Core PawnRushStandings.
    //
    // Drawn as the mock-up at 1280 x 720, scaled by the screen's height; the strip stays in the middle, the list on
    // the right edge, and under the strip where a narrow screen leaves no room beside it.
    [DisallowMultipleComponent]
    public sealed class PawnRushStandingsHud : MonoBehaviour
    {
        /// <summary>The strip is on screen, and its lower edge in screen pixels (the mini-game briefing and the finish's
        /// notes go under it).</summary>
        public static bool Showing { get; private set; }
        public static float Bottom { get; private set; }

        const int MaxRacers = 24, ListLines = 6;
        const float Refresh = .1f, DotSpeed = .7f;
        // the mock-up's frame
        const float TrackX = 344f, TrackW = 556f, LaneWhiteY = 20f, LaneBlackY = 33f, LaneH = 8f, Gap = 4f;
        static readonly Color Panel = new Color(.055f, .07f, .11f, .88f), Brass = new Color(.85f, .7f, .35f), Gold = new Color(.945f, .81f, .48f),
                              Ivory = new Color(.957f, .918f, .824f), Muted = new Color(.75f, .68f, .56f), Ink = new Color(.055f, .07f, .11f);

        sealed class Segment
        {
            public PawnRushSection Section;
            public MiniGameSection Mini;
            public readonly List<Vector3>[] Path = new List<Vector3>[2];
            public readonly float[] Length = new float[2], TeamProgress = new float[2];
            public string Label;
        }

        sealed class Racer
        {
            public CrownFinish.Runner Runner;
            public int Section;
            public float Through, Shown = -1f;
            public bool Seen;
        }

        PawnRushRound round;
        readonly List<Segment> segments = new List<Segment>();
        readonly Dictionary<CrownFinish.Runner, Racer> known = new Dictionary<CrownFinish.Runner, Racer>();
        readonly List<Racer> racers = new List<Racer>();
        readonly double[] progress = new double[MaxRacers];
        readonly int[] ranks = new int[MaxRacers], order = new int[MaxRacers], lines = new int[ListLines];
        double[] weights = new double[0], start = new double[0], width = new double[0];
        int self = -1, lineCount;
        float nextRefresh;
        GUIStyle text;
        Texture2D disc;

        void Awake() => round = GetComponent<PawnRushRound>();

        void OnDisable()
        {
            Showing = false;
            Bottom = 0f;
        }

        void OnDestroy()
        {
            if (disc != null) Destroy(disc);
        }

        // ------------------------------------------------------------------ where everyone is

        void Update()
        {
            var finish = CrownFinish.Current;
            bool on = round != null && round.HasBuilt && finish != null && finish.Runners.Count > 0 && round.Sections.Count > 0 && !LoadingScreenView.Showing;
            if (!on)
            {
                Showing = false;
                Bottom = 0f;
                return;
            }
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + Refresh;
                Segments();
                Standings(finish);
            }
            // the dots glide to their places (a respawn is a quick slide back, not a jump)
            foreach (var r in racers)
            {
                float target = (float)PawnRushStandings.OnTrack(start, width, segments.Count, r.Section, r.Through);
                r.Shown = r.Shown < 0f ? target : Mathf.MoveTowards(r.Shown, target, DotSpeed * Time.unscaledDeltaTime);
            }
            Showing = racers.Count > 0;
            if (!Showing) Bottom = 0f;
        }

        // The round's sections as the track's segments (again whenever the round is built anew).
        void Segments()
        {
            var built = round.Sections;
            bool same = built.Count == segments.Count;
            for (int i = 0; same && i < built.Count; i++) same = segments[i].Section == built[i];
            if (!same)
            {
                segments.Clear();
                foreach (var racer in known.Values) { racer.Section = 0; racer.Through = 0f; racer.Seen = false; racer.Shown = -1f; }
                weights = new double[built.Count];
                start = new double[built.Count];
                width = new double[built.Count];
                for (int i = 0; i < built.Count; i++)
                {
                    var section = built[i];
                    var s = new Segment { Section = section, Mini = section != null ? section.GetComponent<MiniGameSection>() : null };
                    var path = section != null ? section.GetComponentInChildren<ProgressPath>(true) : null;
                    for (int team = 0; team < 2; team++)
                    {
                        s.Path[team] = path != null && path.Count >= 2 ? path.World(team) : null;
                        s.Length[team] = s.Path[team] != null ? ProgressPath.Length(s.Path[team]) : 0f;
                    }
                    bool mini = s.Mini != null || (section != null && section.Kind == PawnRushSection.SlotKind.MiniGame);
                    s.Label = mini ? Loc.T("미니게임") : section != null ? Loc.T(section.DisplayName) : "";
                    weights[i] = PawnRushStandings.Weight(mini, s.Length[0]);
                    segments.Add(s);
                }
                PawnRushStandings.Layout(weights, built.Count, Gap / TrackW, start, width);
            }
            // each team's progress in each mini-game (1 once its gate is open)
            foreach (var s in segments)
            {
                if (s.Mini == null || s.Mini.Stations == null) continue;
                foreach (var station in s.Mini.Stations)
                {
                    if (station == null || (station.Team != Teams.White && station.Team != Teams.Black)) continue;
                    var game = station.Game;
                    bool open = (station.Gate != null && station.Gate.IsOpen) || (game != null && game.Completed);
                    s.TeamProgress[station.Team] = open ? 1f : game != null ? Mathf.Clamp01(game.Progress01) : 0f;
                }
            }
        }

        void Standings(CrownFinish finish)
        {
            racers.Clear();
            self = -1;
            foreach (var runner in finish.Runners)
            {
                if (runner == null || racers.Count >= MaxRacers) continue;
                if (runner.Team != Teams.White && runner.Team != Teams.Black) continue;
                var target = runner.Driver is Object o && o == null ? null : runner.Driver?.FollowTarget;
                if (target == null) continue;       // a racer who has left: no dot, no line
                if (!known.TryGetValue(runner, out var r)) known[runner] = r = new Racer { Runner = runner };
                Place(r, target.position);
                if (runner.Self) self = racers.Count;
                progress[racers.Count] = PawnRushStandings.Progress(r.Section, r.Through);
                racers.Add(r);
            }
            if (known.Count > MaxRacers * 4) known.Clear();     // racers of rounds long gone
            PawnRushStandings.Ranks(progress, racers.Count, ranks);
            PawnRushStandings.Order(ranks, racers.Count, order);
            lineCount = PawnRushStandings.ListRows(order, racers.Count, self, ListLines, lines);
        }

        void Place(Racer r, Vector3 at)
        {
            var section = round.SectionAt(at, out _);
            int index = -1;
            if (section != null)
                for (int i = 0; i < segments.Count; i++)
                    if (segments[i].Section == section) { index = i; break; }
            if (index < 0)
            {
                // between two sections: past the end of the one it was in (or still before the first)
                if (r.Seen && round.InCorridor(at)) r.Through = r.Through > .5f ? 1f : 0f;
                return;
            }
            var s = segments[index];
            int team = r.Runner.Team == Teams.Black ? 1 : 0;
            r.Section = index;
            r.Seen = true;
            if (s.Mini != null) r.Through = s.TeamProgress[team];
            else if (s.Path[team] != null && s.Length[team] > .5f) r.Through = Mathf.Clamp01(ProgressPath.Along(s.Path[team], at, out _) / s.Length[team]);
            else
            {
                Vector3 a = section.EntryWorld.position, b = section.ExitWorld.position, ab = b - a;
                r.Through = ab.sqrMagnitude > .01f ? Mathf.Clamp01(Vector3.Dot(at - a, ab) / ab.sqrMagnitude) : 0f;
            }
        }

        // ------------------------------------------------------------------ the picture

        float unit, stripLeft, listLeft, listTop;

        Rect S(float x, float y, float w, float h) => new Rect(stripLeft + x * unit, y * unit, w * unit, h * unit);          // the strip's frame
        Rect L(float x, float y, float w, float h) => new Rect(listLeft + x * unit, listTop + y * unit, w * unit, h * unit);  // the list's

        void OnGUI()
        {
            if (!Showing || Event.current.type != EventType.Repaint || segments.Count == 0) return;
            unit = Mathf.Clamp(Screen.height / 720f, .75f, 2.4f);
            if (808f * unit > Screen.width - 16f) unit = (Screen.width - 16f) / 808f;
            stripLeft = Screen.width * .5f - 640f * unit;       // the mock-up's 1280 frame about the middle
            Bottom = 62f * unit;
            var old = GUI.color;
            Strip();
            // the list: on the right edge, and under the strip where the screen is too narrow to hold both side by side
            listLeft = Screen.width - 1280f * unit;
            listTop = listLeft + 1062f * unit < stripLeft + 1052f * unit ? Bottom + 2f * unit : 0f;
            List();
            GUI.color = old;
        }

        void Strip()
        {
            var finish = CrownFinish.Current;
            Box(S(236, 6, 808, 56), Panel, new Color(Brass.r, Brass.g, Brass.b, .6f));
            // the rank
            int myTeam = self >= 0 ? racers[self].Runner.Team : MissionStation.LocalTeam;
            var mine = Arena.TeamColor(myTeam, true);
            Box(S(244, 13, 80, 42), new Color(mine.r, mine.g, mine.b, .16f), new Color(mine.r, mine.g, mine.b, .75f));
            if (self >= 0)
            {
                Text(S(244, 13, 33, 42), ranks[self].ToString(), 28f, Ivory, TextAnchor.MiddleRight, true);
                Text(S(278, 23, 16, 24), Ordinal(ranks[self]), 15f, Ivory, TextAnchor.MiddleLeft, true);
            }
            else Text(S(246, 13, 50, 42), Loc.T("관전"), 15f, Ivory, TextAnchor.MiddleCenter, true);
            Text(S(299, 25, 25, 20), "/ " + racers.Count, 10.5f, Muted, TextAnchor.MiddleLeft, false);
            // the two lanes
            Text(S(322, 17, 18, 13), Short(Teams.White), 9f, Arena.TeamColor(Teams.White, true), TextAnchor.MiddleRight, true);
            Text(S(322, 30, 18, 13), Short(Teams.Black), 9f, Arena.TeamColor(Teams.Black, true), TextAnchor.MiddleRight, true);
            for (int i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                float x = TrackX + (float)start[i] * TrackW, w = (float)width[i] * TrackW;
                bool mini = s.Mini != null;
                for (int team = 0; team < 2; team++)
                {
                    var lane = S(x, team == 0 ? LaneWhiteY : LaneBlackY, w, LaneH);
                    if (!mini) { Fill(lane, new Color(Ivory.r, Ivory.g, Ivory.b, .22f)); continue; }
                    Box(lane, new Color(Brass.r, Brass.g, Brass.b, .2f), new Color(Brass.r, Brass.g, Brass.b, .75f));
                    var colour = Arena.TeamColor(team, true);
                    float done = s.TeamProgress[team];
                    if (done > 0f) Fill(new Rect(lane.x, lane.y, lane.width * done, lane.height), new Color(colour.r, colour.g, colour.b, done >= 1f ? .6f : .78f));
                }
                Text(S(x - 20f, 44, w + 40f, 14), Short(s.Label, w), 8.5f, mini ? Brass : Muted, TextAnchor.MiddleCenter, false);
            }
            // a dot a racer; this PC's own last, over the others
            for (int i = 0; i < racers.Count; i++)
                if (i != self) Dot(racers[i], false);
            if (self >= 0) Dot(racers[self], true);
            // the finish and the crown platform's count
            Text(S(902, 20, 22, 22), "♛", 17f, Gold, TextAnchor.MiddleCenter, false);
            if (finish != null)
            {
                for (int team = 0; team < 2; team++)
                {
                    string count = " " + finish.OnPlatform(team) + " / " + PawnRushFinish.Needed(finish.Players(team));
                    string line = Loc.T(team == 0 ? "백" : "흑") + count;
                    if (!Fits(line, 11f, 62f)) line = Short(team) + count;   // R758: as the lanes' tags
                    Text(S(928, team == 0 ? 17 : 31, 62, 14), line, 11f, Arena.TeamColor(team, true), TextAnchor.MiddleLeft, true);
                }
                Text(S(980, 28, 58, 14), Loc.T("왕관 발판"), 8.5f, Muted, TextAnchor.MiddleCenter, false);
            }
        }

        void Dot(Racer r, bool own)
        {
            float x = TrackX + Mathf.Clamp01(r.Shown) * TrackW;
            float y = (r.Runner.Team == Teams.Black ? LaneBlackY : LaneWhiteY) + LaneH * .5f;
            var colour = Arena.TeamColor(r.Runner.Team, true);
            if (own)
            {
                Disc(x, y, 8.2f, Gold);
                Disc(x, y, 6f, colour);
                Text(S(x - 8f, 5.5f, 16f, 11f), "▼", 9f, Gold, TextAnchor.MiddleCenter, false);
            }
            else
            {
                Disc(x, y, 5f, Ink);
                Disc(x, y, 4f, colour);
            }
        }

        void List()
        {
            if (lineCount <= 0) return;
            float height = 34f + lineCount * 23f - 2f;
            Box(L(1062, 6, 206, height), Panel, new Color(Brass.r, Brass.g, Brass.b, .6f));
            Text(L(1074, 10, 180, 18), Loc.T("실시간 순위"), 11f, Brass, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < lineCount; i++)
            {
                int racer = lines[i];
                var r = racers[racer];
                float y = 32f + i * 23f;
                bool own = racer == self;
                if (own) Box(L(1066, y - 1f, 198, 22), new Color(Gold.r, Gold.g, Gold.b, .2f), new Color(Gold.r, Gold.g, Gold.b, .7f));
                var colour = own ? Gold : Ivory;
                Text(L(1070, y, 22, 20), ranks[racer].ToString(), 13f, colour, TextAnchor.MiddleCenter, true);
                var at = L(1098, y + 10f, 0, 0);
                DiscAt(at.x, at.y, 4.5f * unit, Arena.TeamColor(r.Runner.Team, true));
                Text(L(1110, y, 150, 20), Name(r.Runner, own), 13f, colour, TextAnchor.MiddleLeft, own);
            }
        }

        static string Name(CrownFinish.Runner runner, bool own)
        {
            string name = string.IsNullOrEmpty(runner.Name) ? Loc.T(own ? "나" : "플레이어") : runner.Name;
            return name.Length > 10 ? name.Substring(0, 9) + "…" : name;
        }

        // R751: the small word after the big rank number - "위" in Korean, the table's word elsewhere ("名"), and
        // English's own ordinal ending (1st, 2nd, 3rd, 11th), which no single table cell can say.
        static string Ordinal(int rank)
        {
            if (Loc.Lang != "en") return Loc.T("위");
            int tens = rank % 100, ones = rank % 10;
            if (tens >= 11 && tens <= 13) return "th";
            return ones == 1 ? "st" : ones == 2 ? "nd" : ones == 3 ? "rd" : "th";
        }

        // A section's name under its segment: whole if it fits, else its last word ("왕실 수로 정원" -> "정원"). A
        // bracketed part is left off ("국왕 알현실 (임시)" -> "국왕 알현실"; R688).
        static string Short(string label, float room)
        {
            if (string.IsNullOrEmpty(label)) return "";
            int open = label.IndexOf('(');
            if (open > 0) label = label.Substring(0, open).Trim();
            if (label.Length * 8f <= room + 24f) return label;
            int space = label.LastIndexOf(' ');
            return space >= 0 && space + 1 < label.Length ? label.Substring(space + 1) : label;
        }

        // ------------------------------------------------------------------ drawing

        void Fill(Rect r, Color colour)
        {
            GUI.color = colour;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        void Box(Rect r, Color face, Color rim)
        {
            Fill(r, face);
            float t = Mathf.Max(1f, unit);
            GUI.color = rim;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
        }

        // A disc of `radius` (the mock-up's units) about a point of the strip's frame.
        void Disc(float x, float y, float radius, Color colour) => DiscAt(stripLeft + x * unit, y * unit, radius * unit, colour);

        void DiscAt(float x, float y, float radius, Color colour)
        {
            GUI.color = colour;
            GUI.DrawTexture(new Rect(x - radius, y - radius, radius * 2f, radius * 2f), DiscTexture());
        }

        // R758 (user 11-22, English: the lanes' "White" / "Black" ran over "/ 4"): the 18-unit tags take a team's word
        // when it is a letter or two (백 · 흑, 白 · 黑) and its first letter otherwise (W · B).
        static string Short(int team)
        {
            string word = Loc.T(team == Teams.White ? "백" : "흑");
            return word.Length > 2 ? word.Substring(0, 1) : word;
        }

        // Whether `value` drawn bold at `size` fits `width` (the mock-up's units).
        bool Fits(string value, float size, float width)
        {
            if (text == null) return true;
            text.fontSize = Mathf.Max(9, Mathf.RoundToInt(size * unit));
            text.fontStyle = FontStyle.Bold;
            return text.CalcSize(new GUIContent(value)).x <= width * unit;
        }

        void Text(Rect r, string value, float size, Color colour, TextAnchor anchor, bool bold)
        {
            if (text == null) text = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, richText = false, wordWrap = false, clipping = TextClipping.Overflow };
            text.fontSize = Mathf.Max(9, Mathf.RoundToInt(size * unit));
            text.alignment = anchor;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            GUI.color = colour;
            GUI.Label(r, value, text);
        }

        Texture2D DiscTexture()
        {
            if (disc != null) return disc;
            const int size = 48;
            disc = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            float half = size * .5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + .5f - half) * (x + .5f - half) + (y + .5f - half) * (y + .5f - half));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(half - d) * 255f));
                }
            disc.SetPixels32(pixels);
            disc.Apply(false, true);
            return disc;
        }
    }
}
