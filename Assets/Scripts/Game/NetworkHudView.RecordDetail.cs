using System;
using System.Collections.Generic;
using System.Linq;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Game
{
    // R742 (user 11-21: "전적에서 그 판을 더블 클릭 하면 내가 그 판에서 했던 상세 내역" → mock-up A,
    // https://claude.ai/artifact/Hss71hGh9SqW3vXe27er85): a match's detail in the record list's place. Double click a
    // line; '‹ 목록으로' or Esc goes back. What it shows is the record's MatchDetail (Core): no distances anywhere.
    //   header   the result's badge, the mode, the sides, when, the match's length
    //   hero     Pawn Rush: the crossing (or 도착 못 함 / 경기 중 나감) and the pieces this player went through;
    //            Sword Fight: the score and how it ended, this player's piece
    //   middle   Pawn Rush: the sections in order (when, falls, promotions) | my numbers and the teams' result;
    //            Sword Fight: the score over the match (this player's knockouts gold, falls red) | my numbers
    //   bottom   Pawn Rush: both teams' line-ups with places and times;
    //            Sword Fight: the falls this player was in | every player's falls caused and taken
    // A record from before R742 has no detail: the header and the hero, and a line that says so.
    public sealed partial class NetworkHudView
    {
        VisualElement recordDetail, recordList;
        public bool RecordDetailOpen => recordDetail != null && Visible(recordDetail);

        void BuildRecordDetail(VisualElement list)
        {
            recordList = list;
            recordDetail = Element("rd-page", list, PickingMode.Position);
            recordDetail.style.display = DisplayStyle.None;
        }

        // The detail of the record on a row of the page shown, as a double click on it opens it (the lobby capture's way in).
        public void OpenRecordDetailAt(int row)
        {
            if (row >= 0 && row < rcShown.Count) OpenRecordDetail(rcShown[row]);
        }

        public void CloseRecordDetail()
        {
            if (recordDetail == null) return;
            recordDetail.style.display = DisplayStyle.None;
            recordDetail.Clear();
        }

        void OpenRecordDetail(MatchRecord r)
        {
            if (recordDetail == null || r == null) return;
            recordDetail.Clear();
            bool sword = r.Mode == GameModes.SwordFight.Key;
            var d = r.Detail;

            // header
            var head = Element("rd-head", recordDetail);
            Pressable(Loc.T("‹ 목록으로"), "rd-back " + MenuSound.SoundClass(MenuSound.Kind.Close), head, CloseRecordDetail);
            var badge = Text(RecordText.Badge(r.Result), "rc-badge rd-badge", head);
            badge.AddToClassList(r.Result == RecordResult.Win ? "rc-badge-win" : r.Result == RecordResult.Loss ? "rc-badge-loss"
                                 : r.Result == RecordResult.Left ? "rc-badge-left" : "rc-badge-draw");
            var modeCell = Element("rd-mode", head);
            Element(sword ? "cx-dot cx-dot-sf" : "cx-dot cx-dot-pr", modeCell);
            Text(RecordText.ModeName(r.Mode), "rd-mode-name", modeCell);
            string length = d != null && d.Duration >= 0 ? " · " + Loc.F("경기 시간 {0}", MatchResult.Clock(d.Duration)) : "";
            // R745: and whether it counted for the 순위 boards.
            Text(RecordText.Size(r) + " · " + RecordText.When(r.When, DateTime.Now, TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)) + length +
                 (r.Ranked ? " · " + Loc.T("순위 판") : ""), "rd-meta", head);
            Element("grow", head);
            Text(Loc.T("Esc 목록으로"), "rd-esc", head);

            // hero
            var hero = Element("rd-hero", recordDetail);
            var heroText = Element("rd-hero-text", hero);
            Text(Headline(r), "rd-headline", heroText, true);
            Text(HeroLine(r, d), "rd-heroline", heroText);
            Element("grow", hero);
            var journey = Element("rd-journey", hero);
            if (!sword && d != null && d.Promotions.Count > 0)
            {
                Step(journey, PieceKind.Pawn, Loc.T("폰 출발"));
                foreach (var p in d.Promotions)
                {
                    Text("→\n" + (string.IsNullOrEmpty(p.Leg) ? "" : Loc.T(p.Leg) + " ") + MatchResult.Clock(p.At), "rd-arrow", journey);
                    Step(journey, p.Piece, Loc.T(ChessPieces.Name(p.Piece)));
                }
            }
            else Step(journey, r.Piece, Loc.T(ChessPieces.Name(r.Piece)));

            if (d == null || d.Empty)
            {
                Text(Loc.T("이 판은 상세 기록이 없어요"), "rc-empty rd-none", recordDetail);
                Text(Loc.T("상세 기록은 이 기능이 생긴 뒤(R742) 끝낸 온라인 경기부터 남아요"), "rc-empty-sub", recordDetail);
                Show(recordDetail, true);
                return;
            }
            if (sword) SwordDetail(r, d); else RushDetail(r, d);
            Show(recordDetail, true);
        }

        static string Headline(MatchRecord r)
        {
            if (r.Mode != GameModes.SwordFight.Key || r.Result == RecordResult.Left) return RecordText.Headline(r);
            string how = r.End == SwordEnd.Target ? Loc.T("먼저 10점") : r.End == SwordEnd.TimeUp ? Loc.T("시간 종료") : Loc.T("서든데스");
            return r.ScoreOurs + " : " + r.ScoreTheirs + " · " + how;
        }

        static string HeroLine(MatchRecord r, MatchDetail d)
        {
            string team = r.Result == RecordResult.Win ? Loc.T("우리 팀 승리") : r.Result == RecordResult.Loss ? Loc.T("우리 팀 패배")
                        : r.Result == RecordResult.Left ? Loc.T("끝나기 전에 나가서 패로 셈") : Loc.T("무승부");
            if (r.Mode == GameModes.SwordFight.Key)
            {
                if (r.Result == RecordResult.Left || d == null) return team;
                var mine = d.Players.FirstOrDefault(p => p.Self);
                bool top = mine != null && d.Players.Where(p => p.Ours).All(p => p.Knockouts <= mine.Knockouts) && mine.Knockouts > 0;
                return team + " · " + (top ? Loc.F("내가 넣은 점수 {0} (우리 팀에서 가장 많이)", r.Knockouts) : Loc.F("내가 넣은 점수 {0}", r.Knockouts));
            }
            if (r.Place > 0 && d != null)
            {
                int ourFinishers = d.Players.Count(p => p.Ours && p.Place > 0);
                int myOrder = d.Players.Count(p => p.Ours && p.Place > 0 && p.Place <= r.Place);
                return Loc.F("우리 팀 {0}명 중 {1}번째로 왕관 발판", ourFinishers, myOrder) + " · " + (r.CountsToWin ? Loc.T("우리 팀 승리에 들어감") : team);
            }
            return team;
        }

        void Step(VisualElement parent, PieceKind piece, string caption)
        {
            var step = Element("rd-step", parent);
            var icon = Element("rd-step-icon", step);
            SetIcon(icon, piece, 0);
            Text(caption, "rd-step-text", step);
        }

        void Stats(VisualElement parent, params (string value, string label)[] cells)
        {
            var grid = Element(cells.Length == 4 ? "rd-stats rd-stats-half" : "rd-stats", parent);
            foreach (var (value, label) in cells)
            {
                var cell = Element("rd-stat", grid);
                Text(value, "rd-stat-value", cell, true);
                Text(label, "rd-stat-label", cell);
            }
        }

        // ---------- Pawn Rush ----------

        void RushDetail(MatchRecord r, MatchDetail d)
        {
            var middle = Element("rd-middle", recordDetail);
            var legs = Element("rd-col rd-col-wide", middle);
            Text(Loc.T("구간 기록"), "rd-title", legs);
            double end = r.Place > 0 && r.FinishSeconds >= 0 ? r.FinishSeconds : d.Duration;
            for (int i = 0; i < d.Legs.Count && i < 7; i++)
            {
                var leg = d.Legs[i];
                bool last = i == d.Legs.Count - 1;
                var row = Element("rd-leg", legs);
                Text((i + 1).ToString(), "rd-leg-n", row);
                var texts = Element("rd-leg-texts", row);
                Text(Loc.T(leg.Name), "rd-leg-name", texts);
                var notes = new List<string>();
                if (leg.Falls > 0) notes.Add(Loc.F("떨어짐 {0}", leg.Falls));
                foreach (var p in d.Promotions) if (p.Leg == leg.Name) notes.Add(Loc.F("승격: {0}", Loc.T(ChessPieces.Name(p.Piece))));
                if (last && r.Place > 0) notes.Add(Loc.F("{0}위 도착", r.Place));
                Text(notes.Count > 0 ? string.Join(" · ", notes) : Loc.T("떨어짐 없음"), "rd-leg-note", texts);
                string to = last && end < 0 ? "" : " → " + MatchResult.Clock(d.LegEnd(i, end));
                Text(MatchResult.Clock(leg.At) + to, "rd-leg-time", row);
            }
            if (d.Legs.Count == 0) Text(Loc.T("구간 기록이 없어요"), "rd-leg-note", legs);

            var mine = Element("rd-col", middle);
            Text(Loc.T("내 플레이"), "rd-title", mine);
            Stats(mine, (d.Falls.ToString(), Loc.T("떨어짐")), (d.Checkpoints.ToString(), Loc.T("체크포인트")), (d.Skills.ToString(), Loc.T("스킬")),
                  (d.Dives.ToString(), Loc.T("다이빙")), (d.Grabs.ToString(), Loc.T("잡기")), (d.Promotions.Count.ToString(), Loc.T("승격")));
            // R792: while streamer mode hides names, the others by their place in the list ("플레이어 N").
            string Shown(DetailPlayer p) => StreamerMode.Listed(d.Players.IndexOf(p) + 1, p.Name, p.Self);
            if (d.Players.Count > 0)
            {
                int ours = d.Players.Count(p => p.Ours && p.Place > 0), theirs = d.Players.Count(p => !p.Ours && p.Place > 0);
                var clincher = d.Players.FirstOrDefault(p => p.Name == d.Clincher && p.Place > 0);
                string decided = clincher == null ? "" : r.Result == RecordResult.Win ? " · " + Loc.F("{0}의 {1} 도착으로 승리 결정", Shown(clincher), MatchResult.Clock(clincher.Seconds))
                                                    : " · " + Loc.F("상대 {0}의 {1} 도착으로 결정", Shown(clincher), MatchResult.Clock(clincher.Seconds));
                var box = Text(Loc.F("우리 팀 {0}명 도착 · 상대 팀 {1}명 도착", ours, theirs) + decided, "rd-team-result", mine);
                box.EnableInClassList("rd-team-lost", r.Result != RecordResult.Win);
            }

            if (d.Players.Count == 0) return;
            var bottom = Element("rd-bottom", recordDetail);
            foreach (bool ours in new[] { true, false })
            {
                var col = Element("rd-col", bottom);
                Text(ours ? Loc.T("우리 팀") + (r.Result == RecordResult.Win ? " · " + Loc.T("승리") : "") : Loc.T("상대 팀") + (r.Result == RecordResult.Loss ? " · " + Loc.T("승리") : ""), "rd-title", col);
                var grid = Element("rd-roster", col);
                foreach (var p in d.Players.Where(p => p.Ours == ours))
                {
                    var row = Element("rd-player" + (p.Self ? " rd-player-me" : ""), grid);
                    SetIcon(Element("rd-player-icon", row), p.Piece, ours ? 0 : 1);
                    Text(Shown(p), "rd-player-name", row);
                    Text(p.Place > 0 ? Loc.F("{0}위 {1}", p.Place, MatchResult.Clock(p.Seconds)) : Loc.T("도착 못 함"), "rd-player-result", row);
                }
            }
        }

        // ---------- Sword Fight ----------

        void SwordDetail(MatchRecord r, MatchDetail d)
        {
            var middle = Element("rd-middle", recordDetail);
            var chartCol = Element("rd-col rd-col-wide", middle);
            var title = Element("rd-chart-head", chartCol);
            Text(Loc.T("점수 흐름"), "rd-title", title);
            Key(title, "rd-key-us", Loc.T("우리 팀"));
            Key(title, "rd-key-them", Loc.T("상대 팀"));
            Key(title, "rd-key-me", Loc.T("내가 떨어뜨림"));
            Key(title, "rd-key-fell", Loc.T("내가 떨어짐"));
            chartCol.Add(new ScoreChart(d));

            var mine = Element("rd-col", middle);
            Text(Loc.T("내 플레이"), "rd-title", mine);
            Stats(mine, (r.Knockouts.ToString(), Loc.T("떨어뜨림")), (r.Falls.ToString(), Loc.T("떨어짐")),
                  (d.SwordF.ToString(), "F " + Loc.T(SwordSkillNames.Skill(r.Piece))), (d.SwordE.ToString(), "E " + Loc.T(SwordSkillNames.Edge(r.Piece))));

            var bottom = Element("rd-bottom", recordDetail);
            var events = Element("rd-col rd-col-wide", bottom);
            Text(Loc.T("나와 관련된 장외"), "rd-title", events);
            var involved = d.Feed.Where(f => f.ByMe || f.OfMe).Take(6).ToList();
            foreach (var f in involved)
            {
                var row = Element("rd-event", events);
                Text(MatchResult.Clock(f.At), "rd-event-time", row);
                Element(f.ByMe ? "rd-event-dot rd-dot-me" : "rd-event-dot rd-dot-fell", row);
                string text = f.ByMe ? Loc.F("{0} ← 나", f.Victim) : string.IsNullOrEmpty(f.Credit) ? Loc.T("나 — 스스로") : Loc.F("나 ← {0}", f.Credit);
                Text(text, "rd-event-text", row);
                Text(f.ByMe || !string.IsNullOrEmpty(f.Credit) ? Loc.T(f.Cause) : "", "rd-event-cause", row);
            }
            if (involved.Count == 0) Text(Loc.T("떨어뜨리거나 떨어진 일이 없어요"), "rd-leg-note", events);

            var roster = Element("rd-col", bottom);
            var head = Element("rd-sword-row rd-sword-head", roster);
            Text(Loc.T("명단"), "rd-title rd-sword-who", head);
            Text(Loc.T("떨어뜨림"), "rd-sword-num rd-title", head);
            Text(Loc.T("떨어짐"), "rd-sword-num rd-title", head);
            foreach (var p in d.Players)
            {
                var row = Element("rd-sword-row" + (p.Self ? " rd-player-me" : p.Ours ? "" : " rd-player-them"), roster);
                var who = Element("rd-sword-who", row);
                SetIcon(Element("rd-player-icon", who), p.Piece, p.Ours ? 0 : 1);
                Text(p.Name, "rd-player-name", who);
                Text(p.Knockouts.ToString(), "rd-sword-num rd-sword-ko", row);
                Text(p.Falls.ToString(), "rd-sword-num", row);
            }
        }

        void Key(VisualElement parent, string mark, string text)
        {
            var key = Element("rd-key", parent);
            Element("rd-key-mark " + mark, key);
            Text(text, "rd-key-text", key);
        }

        // The score over the match: each side's score as steps, this player's knockouts in gold and falls in red.
        sealed class ScoreChart : VisualElement
        {
            static readonly Color Us = new Color(.953f, .922f, .867f), Them = new Color(.54f, .48f, .4f),
                Gold = new Color(.965f, .722f, .243f), Red = new Color(.965f, .384f, .29f), Line = new Color(.953f, .922f, .867f, .25f);
            readonly MatchDetail detail;
            readonly double length;
            readonly int top;

            public ScoreChart(MatchDetail d)
            {
                detail = d;
                length = Math.Max(1, d.Duration > 0 ? d.Duration : d.Feed.Count > 0 ? d.Feed.Max(f => f.At) : 1);
                top = Math.Max(10, Math.Max(d.Score(true), d.Score(false)));
                AddToClassList("rd-chart");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                Axis("0", 0f, 1f, false); Axis(top.ToString(), 0f, 0f, false);
                Axis("0:00", 0f, 1f, true); Axis(MatchResult.Clock(length), 1f, 1f, true);
            }

            void Axis(string text, float x, float y, bool time)
            {
                var label = new Label(text) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("rd-axis");
                label.style.position = Position.Absolute;
                if (time) { label.style.bottom = -16; if (x > .5f) label.style.right = 0; else label.style.left = 22; }
                else
                {
                    label.style.left = 0;
                    label.style.top = y > .5f ? new Length(100, LengthUnit.Percent) : new Length(0);
                    label.style.translate = new Translate(new Length(0), Length.Percent(-50));
                }
                Add(label);
            }

            void Draw(MeshGenerationContext context)
            {
                float w = layout.width, h = layout.height;
                if (float.IsNaN(w) || w < 40f || h < 20f) return;
                const float left = 22f;
                float X(double t) => left + (float)(Math.Max(0, Math.Min(length, t)) / length) * (w - left - 4f);
                float Y(int s) => h - 2f - (float)s / top * (h - 8f);
                var p = context.painter2D;
                p.lineWidth = 1f;
                p.strokeColor = Line;
                p.BeginPath(); p.MoveTo(new Vector2(left, Y(0))); p.LineTo(new Vector2(w - 4f, Y(0))); p.Stroke();
                foreach (bool ours in new[] { false, true })
                {
                    p.lineWidth = 2.5f;
                    p.strokeColor = ours ? Us : Them;
                    p.BeginPath();
                    int s = 0;
                    p.MoveTo(new Vector2(X(0), Y(0)));
                    foreach (var f in detail.Feed.Where(f => f.Ours == ours).OrderBy(f => f.At))
                    {
                        p.LineTo(new Vector2(X(f.At), Y(s)));
                        s++;
                        p.LineTo(new Vector2(X(f.At), Y(s)));
                    }
                    p.LineTo(new Vector2(X(length), Y(s)));
                    p.Stroke();
                }
                int us = 0, them = 0;
                foreach (var f in detail.Feed.OrderBy(f => f.At))
                {
                    if (f.Ours) us++; else them++;
                    if (!f.ByMe && !f.OfMe) continue;
                    p.fillColor = f.ByMe ? Gold : Red;
                    p.BeginPath();
                    p.Arc(new Vector2(X(f.At), Y(f.Ours ? us : them)), 5f, 0f, 360f);
                    p.Fill();
                }
            }
        }
    }
}
