using System;

namespace ChessFight.Network
{
    /// <summary>R521: how a number in the match's metrics box reads at a glance. The order matters: the worse grade is
    /// the larger one (<see cref="NetHealth.Worse"/>).</summary>
    public enum NetGrade { None, Good, Warn, Bad }

    /// <summary>
    /// R521 (HOST_AUTHORITY §12): the lines the match's metrics box (F3, a development build) colours its numbers by -
    /// green at or inside the good line, orange up to the warn line, red past it, grey while there is nothing to grade.
    /// They sit where this game's own numbers put them: the 60 snapshots a second it aims at (SnapshotRate), its jitter
    /// buffer (PlaybackDelay) and the upload twelve players are expected to need (HOST_AUTHORITY §6).
    /// </summary>
    public static class NetHealth
    {
        /// <summary>Frames a second. The fast snapshot rate is one a frame at 60, so a host under it sends fewer.</summary>
        public const double FpsGood = 55, FpsWarn = 30;
        /// <summary>A client's round trip (its press to the host and back, the host's physics turn in it) or Steam's ping, ms.</summary>
        public const double PingGood = 80, PingWarn = 150;
        /// <summary>A press until this PC's own pawn is drawn moving, ms: the round trip and about 55 ms more at 60 snapshots
        /// a second, 95 at 30 (HOST_AUTHORITY §5).</summary>
        public const double FeltGood = 150, FeltWarn = 250;
        /// <summary>The share of snapshots lost. From 8% the jitter buffer waits a snapshot more (PlaybackDelay.LossyShare).</summary>
        public const double LossGood = 0.01, LossWarn = 0.05;
        /// <summary>Snapshot arrival jitter, ms. Up to 4 costs nothing (PlaybackDelay.FreeJitterMs), each ms over adds 2.5 ms
        /// of playback delay: 10 adds 15 ms, 30 adds 65.</summary>
        public const double JitterGood = 10, JitterWarn = 30;
        /// <summary>Snapshots a second: good at nine tenths of the rate aimed at; under 20 the others move in visible steps.</summary>
        public const double RateGood = 0.9;
        public const int RateWarn = 20;
        /// <summary>The host's physics steps against real time. Behind it the whole match runs slow for everyone.</summary>
        public const double PaceGood = 0.95, PaceWarn = 0.8;
        /// <summary>The host's upload, KB a second. Twelve players at 30 snapshots a second are about 261 and the skills' packets
        /// some 10 to 40 more (HOST_AUTHORITY §6): over 350 it sends more than a full match should.</summary>
        public const double UploadGood = 350, UploadWarn = 700;

        /// <summary>Lower is better: good up to <paramref name="good"/>, borderline up to <paramref name="warn"/>. A negative or
        /// NaN value is not measured yet.</summary>
        public static NetGrade AtMost(double value, double good, double warn)
        {
            if (double.IsNaN(value) || value < 0) return NetGrade.None;
            return value <= good ? NetGrade.Good : value <= warn ? NetGrade.Warn : NetGrade.Bad;
        }

        /// <summary>Higher is better: good from <paramref name="good"/>, borderline from <paramref name="warn"/>.</summary>
        public static NetGrade AtLeast(double value, double good, double warn)
        {
            if (double.IsNaN(value) || value < 0) return NetGrade.None;
            return value >= good ? NetGrade.Good : value >= warn ? NetGrade.Warn : NetGrade.Bad;
        }

        public static NetGrade Fps(double fps) => fps > 0 ? AtLeast(fps, FpsGood, FpsWarn) : NetGrade.None;
        /// <summary>A round trip or a ping, ms (0 or less: none yet).</summary>
        public static NetGrade Ping(double ms) => ms > 0 ? AtMost(ms, PingGood, PingWarn) : NetGrade.None;
        public static NetGrade Felt(double ms) => ms > 0 ? AtMost(ms, FeltGood, FeltWarn) : NetGrade.None;
        public static NetGrade Loss(double share) => AtMost(share, LossGood, LossWarn);
        public static NetGrade Jitter(double ms) => AtMost(ms, JitterGood, JitterWarn);
        public static NetGrade Pace(double share) => AtLeast(share, PaceGood, PaceWarn);
        public static NetGrade Upload(double kbPerSecond) => AtMost(kbPerSecond, UploadGood, UploadWarn);

        /// <summary>Snapshots a second against the rate aimed at (none to grade without a rate).</summary>
        public static NetGrade Snapshots(int count, int target)
        {
            if (target <= 0 || count < 0) return NetGrade.None;
            // A hair under nine tenths still counts (0.9 has no exact double: 27 of 30 must be green).
            return count >= RateGood * target - 1e-9 ? NetGrade.Good : count >= RateWarn ? NetGrade.Warn : NetGrade.Bad;
        }

        /// <summary>A client's guess at the rate the host aims at (30 or 60) from the host's interval as its stamps space them
        /// (PlaybackDelay.IntervalMs): nothing on the wire says it. A host aiming at 60 that manages only 40 reads as 30 here;
        /// the host's own box shows it short.</summary>
        public static int TargetFromInterval(double intervalMs) => intervalMs > 0 && intervalMs <= 1000.0 / 45 ? 60 : 30;

        public static NetGrade Worse(NetGrade a, NetGrade b) => a > b ? a : b;
    }

    /// <summary>
    /// R521: Steam's routes for the metrics box (Network's SteamRoute reads them, once a second). On a client the one to
    /// the host - <see cref="Direct"/> or <see cref="Relayed"/> is 1 - and its ping; on the host how many of its players go
    /// straight and how many through Valve's relays, and the worst ping. Nothing is known while no session is connected.
    /// </summary>
    public struct RouteSummary
    {
        public int Direct, Relayed;
        /// <summary>Steam's ping, ms (a client's to the host, the host's worst); -1 unknown.</summary>
        public int PingMs;

        public static RouteSummary None => new RouteSummary { PingMs = -1 };
        public bool Known => Direct + Relayed > 0;
    }
}
