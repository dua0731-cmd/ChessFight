using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessFight.Network
{
    // How well a PC can carry a match. The host simulates every pawn (in the
    // ragdoll lab every physics body), so a slow host is lag on every screen.
    // Only the ratio between players matters; the numbers stay near 1000 for a
    // mid-range desktop so they read well in logs.
    public static class HostFitness
    {
        // What a player counts as before their probe has finished: below any
        // real measurement, so they are never preferred, but not zero.
        public const int Unknown = 300;
        // HostFitnessProbe's workload on a mid-range desktop CPU. It only scales
        // the numbers; the comparison between players does not depend on it.
        public const double ReferenceBenchMs = 40;
        // Below 40 fps the measured frame time says more than the benchmark.
        public const double SlowFrameMs = 25;

        // The machine alone, 0 when not measured yet.
        // benchMs  best time of the probe's fixed CPU workload
        // cores    logical processors (physics uses worker threads)
        // memoryMb system RAM, <= 0 = unknown
        // editor   inside the Unity Editor, which costs a host noticeably more
        public static int Base(double benchMs, int cores, int memoryMb, bool editor)
        {
            if (benchMs <= 0 || double.IsNaN(benchMs)) return 0;
            double score = 1000 * ReferenceBenchMs / benchMs;
            score *= 0.75 + 0.25 * Math.Min(Math.Max(cores, 1), 8) / 8.0;
            if (memoryMb > 0 && memoryMb < 8000) score *= 0.85;
            if (editor) score *= 0.85;
            return (int)Math.Round(Math.Min(Math.Max(score, 1), 99999));
        }

        // The machine as it runs right now: a PC already slow in the lobby will be
        // slower still when it also has to simulate the match.
        // frameMs  this PC's average frame time, <= 0 = unknown
        public static int WithFrames(int baseScore, double frameMs)
        {
            if (baseScore <= 0) return 0;
            double score = baseScore;
            if (frameMs > SlowFrameMs) score *= SlowFrameMs / frameMs;
            return (int)Math.Round(Math.Max(score, 1));
        }

        public static int Score(double benchMs, int cores, int memoryMb, bool editor, double frameMs) =>
            WithFrames(Base(benchMs, cores, memoryMb, editor), frameMs);
    }

    // Who runs the match. The current host decides with everyone's published
    // fitness and ping locations, and publishes the order it would hand over in,
    // so a crash still has an agreed successor.
    public static class HostElection
    {
        public struct Candidate
        {
            public ulong Id;
            public int Fitness;     // HostFitness.WithFrames: the machine as it runs now, 0 = unknown
            public int BaseFitness; // HostFitness.Base: the machine alone, 0 = unknown
            // Mean estimated ping to the other players, < 0 = unknown. The mean,
            // not the worst: one far-away player is far from every host alike,
            // while a far-away host is far from everyone.
            public int PingMs;
        }

        // Every other player's packets pass through the host, so its links are
        // everyone's delay once they go past this.
        public const int PingBudgetMs = 60;
        // How much fitter a player must be before the host role moves. Before the
        // match (the waiting room, and its start) the best PC simply hosts (R159,
        // 승규: "1등이 방장"): machine scores are measured once, so the order does not
        // flicker, and nothing is simulated yet, so a move costs nothing. Mid-match,
        // when the host is struggling, only a clearly stronger machine takes over:
        // moving then costs a hitch.
        public const double StartMargin = 1.0, StruggleMargin = 1.5;

        public static double Rating(Candidate c)
        {
            double fit = c.Fitness > 0 ? c.Fitness : HostFitness.Unknown;
            if (c.PingMs > PingBudgetMs) fit *= Math.Max(.3, (double)PingBudgetMs / c.PingMs);
            return fit;
        }

        // Best first. Equal ratings fall back to the lower Steam ID, so every PC
        // ranking the same data gets the same order.
        public static List<ulong> Rank(IEnumerable<Candidate> candidates) =>
            candidates.Where(c => c.Id != 0 && !BotIdentity.IsBot(c.Id))
                      .OrderByDescending(Rating).ThenBy(c => c.Id).Select(c => c.Id).ToList();

        public static bool Worth(Candidate current, Candidate best, double margin) =>
            best.Id != 0 && best.Id != current.Id && Rating(best) >= Rating(current) * margin;

        // R159: a player whose machine score has not reached the host yet: one who
        // has just come in (the last seat filling the room starts it in the same poll).
        public static bool Unknown(Candidate c) => c.BaseFitness <= 0 && c.Fitness <= 0;

        // R159: the host at the start of a match. Machines are compared alone (the
        // probe and the hardware), the same measurement on every PC: a player's live
        // frame time dips for a moment as it comes into the room, and a dip read then
        // kept the room's creator as host over a clearly stronger PC that came in
        // later. A slow frame rate during the match still moves the role (Takeover).
        // Unknown machines count as HostFitness.Unknown, as everywhere else.
        public static ulong StartHost(ulong self, IList<Candidate> candidates, double margin)
        {
            var alone = candidates.Select(c => c.BaseFitness > 0 ? new Candidate { Id = c.Id, Fitness = c.BaseFitness, BaseFitness = c.BaseFitness, PingMs = c.PingMs } : c).ToList();
            var ranked = Rank(alone);
            if (ranked.Count == 0) return self;
            var current = alone.FirstOrDefault(c => c.Id == self);
            if (current.Id == 0) current = new Candidate { Id = self };
            var best = alone.First(c => c.Id == ranked[0]);
            return Worth(current, best, margin) ? best.Id : self;
        }

        // Who takes over from a host that has stayed slow: the best-rated player
        // whose machine alone is clearly stronger, 0 when there is none. Machines
        // are compared, not current frame rates: a host is slow because it hosts,
        // and the old host would look fast again the moment it stopped, so a
        // frame-rate rule would bounce the role back and forth.
        public static ulong Takeover(Candidate host, IList<Candidate> others, double margin)
        {
            if (host.BaseFitness <= 0) return 0;
            foreach (ulong id in Rank(others))
            {
                var c = others.First(o => o.Id == id);
                if (c.Id != host.Id && c.BaseFitness >= host.BaseFitness * margin) return id;
            }
            return 0;
        }

        // The next host once `leaving` is gone: the first published successor who
        // is still here, otherwise the fittest of those present.
        public static ulong Successor(IEnumerable<ulong> published, ICollection<ulong> present, ulong leaving, IEnumerable<Candidate> fallback)
        {
            foreach (ulong id in published ?? Enumerable.Empty<ulong>())
                if (id != leaving && id != 0 && !BotIdentity.IsBot(id) && present.Contains(id)) return id;
            var ranked = Rank((fallback ?? Enumerable.Empty<Candidate>()).Where(c => c.Id != leaving && present.Contains(c.Id)));
            return ranked.Count > 0 ? ranked[0] : 0;
        }

        // Lobby metadata form: "id,id,id".
        public static string Encode(IEnumerable<ulong> ids) => string.Join(",", ids);
        public static List<ulong> Decode(string text)
        {
            var ids = new List<ulong>();
            foreach (string part in (text ?? "").Split(','))
                if (ulong.TryParse(part, out ulong id) && id != 0 && !ids.Contains(id) && ids.Count < 12) ids.Add(id);
            return ids;
        }
    }

    // The host's own frame time, smoothed. A host that stays slow for a while
    // hands the match to a fitter player; a single loading hitch must not.
    public sealed class FrameMonitor
    {
        public const double StruggleMs = 40, StruggleSeconds = 5, Smoothing = .1;
        public double AverageMs { get; private set; } = -1;
        double slowSince = -1;

        public void Add(double frameMs, double now)
        {
            if (frameMs <= 0 || double.IsNaN(frameMs) || double.IsInfinity(frameMs)) return;
            // One pathological frame (a scene load) may not dominate the average.
            frameMs = Math.Min(frameMs, 250);
            AverageMs = AverageMs < 0 ? frameMs : AverageMs + (frameMs - AverageMs) * Smoothing;
            if (AverageMs > StruggleMs) { if (slowSince < 0) slowSince = now; }
            else slowSince = -1;
        }

        public bool Struggling(double now) => slowSince >= 0 && now - slowSince >= StruggleSeconds;
        public void Clear() { AverageMs = -1; slowSince = -1; }
    }
}
