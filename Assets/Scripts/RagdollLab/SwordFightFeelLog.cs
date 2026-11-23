using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R813 (승규 님 11-24: "다음 빌드에서 테스트를 해보고 플레이 로그를 줄 테니까 정확한 수치와 확인 해볼 수 있는 내용을
    /// 기록하여"): what this PC saw of the click cut's feel in one Sword Fight match, as one block in Player.log when the
    /// match ends (or when this PC leaves it) - the 11-23 play log had nothing of the sword fight between its start and
    /// its result. Pictures and sounds are not judged here; the numbers are what to read them against
    /// (Docs/GameModes/SwordFight/HIT_FEEL_PLAN.md §4 shows how). Put on by SwordFightMatch; counts what
    /// SwordFightPawn, LabNetLink.Sword and the skills' moments tell <see cref="SwordFeelStats"/>; changes nothing in the game.
    /// </summary>
    public sealed class SwordFightFeelLog : MonoBehaviour
    {
        SwordFightGame game;
        bool measuring, reported;
        // The last frame seen while measuring, and the most people and bots seen: a report made as the scene goes (leaving
        // early) comes after the fighters and the clock of the match are gone.
        float startedAt, sampleAt, lastSeenAt;
        int peakPeople, peakBots;
        string roleSeen;
        // The connection, sampled once a second while the match is played (a client's numbers; the host has none).
        readonly List<float> roundTrips = new List<float>(), delays = new List<float>(), snapshots = new List<float>();
        // Every frame's length while it is played (for the frame line: a hitch at a first hit, say).
        readonly List<float> frames = new List<float>(16384);

        void Awake() => game = GetComponent<SwordFightGame>();

        void OnEnable() => SwordFightSkills.Fx += OnFx;

        void OnDisable() => SwordFightSkills.Fx -= OnFx;

        void OnFx(SfFxEvent e)
        {
            if (!measuring || e.kind != SfFxKind.SwordBlocked) return;
            if (e.by != null && IsLocal(e.by.Pawn)) SwordFeelStats.MyBlocked++;
            if (e.target != null && IsLocal(e.target)) SwordFeelStats.BlockedOnMe++;
        }

        bool IsLocal(RagdollPawn pawn) => game != null && game.Local != null && pawn == game.Local.Pawn;

        void Update()
        {
            if (game == null || game.Match == null || reported) return;
            bool live = !SwordFightGame.InputLocked && !game.Match.Finished;
            if (!measuring && live)
            {
                measuring = true;
                startedAt = sampleAt = Time.unscaledTime;
                SwordFeelStats.Clear();
                SwordFeelStats.Measuring = true;
            }
            if (!measuring) return;
            frames.Add(Time.unscaledDeltaTime);
            lastSeenAt = Time.unscaledTime;
            if (Time.unscaledTime >= sampleAt + 1f || peakPeople + peakBots == 0)
            {
                sampleAt = Time.unscaledTime;
                int people = 0, bots = 0;
                foreach (var f in game.Fighters.Values) { if (f == null) continue; if (f.Bot) bots++; else people++; }
                peakPeople = Mathf.Max(peakPeople, people);
                peakBots = Mathf.Max(peakBots, bots);
                // As it is while playing: leaving, the link may be gone before this reports.
                roleSeen = !LabNetLink.SwordOnline ? "혼자(온라인 아님)" : LabNetLink.SwordClient ? "참가자(물리 담당 아님)" : "물리 담당";
                var link = LabNetLink.Current;
                if (LabNetLink.SwordClient && link != null)
                {
                    roundTrips.Add(link.RoundTripMs);
                    delays.Add((float)link.PlaybackDelayMs);
                    snapshots.Add(link.SnapshotsPerSecond);
                }
            }
            if (game.Match.Finished) Report("결과까지");
        }

        void OnDestroy()
        {
            if (measuring && !reported) Report("결과 전에 나감");
            SwordFeelStats.Measuring = false;
        }

        void Report(string how)
        {
            reported = true;
            SwordFeelStats.Measuring = false;
            try { Debug.Log(Text(how)); }
            catch (Exception e) { Debug.Log("[소드 파이트 타격감] 기록 실패: " + e.Message); }
        }

        string Text(string how)
        {
            var c = CultureInfo.InvariantCulture;
            var s = SwordFeelStats.Snapshot();
            var t = new StringBuilder();
            string role = roleSeen ?? "?";
            float seconds = Mathf.Max(0f, lastSeenAt - startedAt);
            t.AppendFormat(c, "[소드 파이트 타격감] {0} · 이 PC {1} · 사람 {2}{3} · 잰 시간 {4}분 {5:00}초",
                how, role, peakPeople, peakBots > 0 ? " + 봇 " + peakBots : "", (int)(seconds / 60f), Mathf.Floor(seconds % 60f));
            if (roundTrips.Count > 0)
                t.AppendFormat(c, "\n  연결: 왕복 지연 평균 {0:0} ms (최대 {1:0}) · 재생 지연 평균 {2:0} ms (최대 {3:0}) · 받은 스냅샷 초당 {4:0.0}장 ({5}번 잼)",
                    Mean(roundTrips), Max(roundTrips), Mean(delays), Max(delays), Mean(snapshots), roundTrips.Count);
            else t.Append("\n  연결: 이 PC가 물리 계산을 맡아 지연 없음(참가자 PC의 로그에 있음)");
            t.AppendFormat(c, "\n  내 클릭: {0}번 · 가만히 있다 누른 {1}번에서 내 칼이 화면에서 움직이기까지 {2}",
                s.Clicks, s.ClickToBlade.Count, s.ClickToBlade.Count > 0
                    ? string.Format(c, "평균 {0:0} ms (가장 빠름 {1:0} · 가장 느림 {2:0} · 절반이 {3:0} ms 안)", s.ClickToBlade.Mean, s.ClickToBlade.Min, s.ClickToBlade.Max, s.ClickToBlade.Median)
                    : "잰 것 없음");
            t.AppendFormat(c, "\n  내 타격: 맞힘 {0} · 보호막에 막힘 {1} · 맞음 {2} · 내 보호막에 막아 냄 {3} · 금색 X {4}",
                s.MyHits, s.MyBlocked, s.HitsOnMe, s.BlockedOnMe, s.Marks);
            int all = s.CutsKnown + s.CutsByStrike + s.CutsByNearest + s.CutsWithout;
            t.AppendFormat(c, "\n  이 화면의 타격 {0}개: 때린 사람 = 물리 담당이 앎 {1} · 맞힘 표시 시각으로 {2} · 가장 가까운 칼로 {3} · 못 찾음 {4}",
                all, s.CutsKnown, s.CutsByStrike, s.CutsByNearest, s.CutsWithout);
            if (s.CutWait.Count > 0)
                t.AppendFormat(c, " · 그려지는 몸이 맞는 순간까지 기다림 평균 {0:0} ms (최대 {1:0}, 0.1초에서 끊김 {2}번)",
                    s.CutWait.Mean, s.CutWait.Max, s.CutWaitCapped);
            t.Append("\n  화면: ").Append(FrameLine(c));
            t.Append("\n  값: 첫 베기(준비 / 칼날 / 회복 초)");
            foreach (var piece in Pieces)
            {
                var (w, a, r) = SwordFightPawn.FirstCut(piece);
                t.AppendFormat(c, " {0} {1:0.00}/{2:0.00}/{3:0.00}", Short(piece), w, a, r);
            }
            t.AppendFormat(c, " · 칼 걸림 {0:0.00}초 × {1:0.00} · 남은 칼길 {2:0.0}배 · 맞은 그림 {3}프레임 · 맞은 표시 {4:0.0}초 · 남의 소리 {5:0.0} → {6:0.0}({7:0} ~ {8:0} m) · 보호막 {9:0.00} m",
                SwordFightPawn.BiteTime, SwordFightPawn.BitePace, SwordFightPawn.SnapPace, SwordFightSkillFx.CutHoldFrames, SwordFightPawn.CutFlash,
                SwordFightSkillSfx.OthersNear, SwordFightSkillSfx.OthersFar, SwordFightSkillSfx.OthersNearTo, SwordFightSkillSfx.OthersFarFrom, SwordFightPawn.ShellRadius);
            return t.ToString();
        }

        string FrameLine(CultureInfo c)
        {
            if (frames.Count == 0) return "잰 것 없음";
            double total = 0;
            int hitches = 0;
            float longest = 0f;
            foreach (var f in frames) { total += f; if (f > .1f) hitches++; if (f > longest) longest = f; }
            var sorted = new List<float>(frames);
            sorted.Sort();
            float slow = sorted[Mathf.Clamp(Mathf.FloorToInt(sorted.Count * .99f), 0, sorted.Count - 1)];
            return string.Format(c, "평균 {0:0} fps · 느린 1% {1:0} ms · 0.1초 넘는 끊김 {2}번 · 가장 긴 {3:0.00}초",
                frames.Count / Math.Max(1e-3, total), slow * 1000f, hitches, longest);
        }

        static readonly ChessFight.Network.PieceKind[] Pieces =
        {
            ChessFight.Network.PieceKind.King, ChessFight.Network.PieceKind.Queen, ChessFight.Network.PieceKind.Rook,
            ChessFight.Network.PieceKind.Bishop, ChessFight.Network.PieceKind.Knight, ChessFight.Network.PieceKind.Pawn,
        };

        static string Short(ChessFight.Network.PieceKind p) => p switch
        {
            ChessFight.Network.PieceKind.King => "킹", ChessFight.Network.PieceKind.Queen => "퀸", ChessFight.Network.PieceKind.Rook => "룩",
            ChessFight.Network.PieceKind.Bishop => "비숍", ChessFight.Network.PieceKind.Knight => "나이트", _ => "폰",
        };

        static float Mean(List<float> list)
        {
            if (list.Count == 0) return 0f;
            double sum = 0;
            foreach (var v in list) sum += v;
            return (float)(sum / list.Count);
        }

        static float Max(List<float> list)
        {
            float m = 0f;
            foreach (var v in list) m = Mathf.Max(m, v);
            return m;
        }
    }

    /// <summary>R813: the counts SwordFightFeelLog reports, told by SwordFightPawn and LabNetLink.Sword while it measures
    /// (only this PC's own view: its player's clicks and cuts, the cuts drawn on its screen).</summary>
    public static class SwordFeelStats
    {
        public static bool Measuring;
        public static int Clicks, MyHits, HitsOnMe, MyBlocked, BlockedOnMe, Marks;
        public static int CutsKnown, CutsByStrike, CutsByNearest, CutsWithout, CutWaitCapped;
        public static readonly Series ClickToBlade = new Series(), CutWait = new Series();

        public static void Clear()
        {
            Clicks = MyHits = HitsOnMe = MyBlocked = BlockedOnMe = Marks = 0;
            CutsKnown = CutsByStrike = CutsByNearest = CutsWithout = CutWaitCapped = 0;
            ClickToBlade.Clear();
            CutWait.Clear();
        }

        /// <summary>The counts as they are now (the report reads them once).</summary>
        public static (int Clicks, int MyHits, int HitsOnMe, int MyBlocked, int BlockedOnMe, int Marks, int CutsKnown, int CutsByStrike,
            int CutsByNearest, int CutsWithout, int CutWaitCapped, Series ClickToBlade, Series CutWait) Snapshot() =>
            (Clicks, MyHits, HitsOnMe, MyBlocked, BlockedOnMe, Marks, CutsKnown, CutsByStrike, CutsByNearest, CutsWithout, CutWaitCapped, ClickToBlade, CutWait);

        /// <summary>Values in milliseconds: how many, their mean, least, most and middle.</summary>
        public sealed class Series
        {
            readonly List<float> values = new List<float>();
            public int Count => values.Count;
            public float Mean { get { if (values.Count == 0) return 0f; double s = 0; foreach (var v in values) s += v; return (float)(s / values.Count); } }
            public float Min { get { float m = float.MaxValue; foreach (var v in values) m = Mathf.Min(m, v); return values.Count > 0 ? m : 0f; } }
            public float Max { get { float m = 0f; foreach (var v in values) m = Mathf.Max(m, v); return m; } }
            public float Median { get { if (values.Count == 0) return 0f; var s = new List<float>(values); s.Sort(); return s[s.Count / 2]; } }
            public void Add(float ms) { if (values.Count < 10000) values.Add(ms); }
            public void Clear() => values.Clear();
        }
    }
}
