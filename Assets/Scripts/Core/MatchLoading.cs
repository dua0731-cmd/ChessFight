using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChessFight.Network
{
    // Everyone loads the match scene on their own PC behind the loading screen,
    // then the race starts for all of them at one moment (Docs/Architecture/UI.md
    // "로딩 화면"). Each player publishes how far it got ("load", 0-100, member
    // data); the host publishes one start time on the shared Steam clock ("go",
    // room data) once everyone is ready or once it has waited long enough for the
    // slowest. Both live in the lobby, so a new host after a handover sees them too.
    // Pure rules: SteamSession stores the values, the loading screen asks here.
    public static class MatchStart
    {
        public const int Ready = 100;
        // How long a ready host waits for the slowest player before starting anyway.
        // The late player starts from the line as soon as it is ready.
        public const double MaxWait = 20;
        // Between the host's decision and the start, so the room data reaches everyone first.
        public const double Lead = 1.5;
        // A ready client whose host never announces a start (it left mid-load, or
        // runs a build that never does) stops waiting after this.
        public const double ClientGiveUp = MaxWait + 10;

        public static int Clamp(int percent) => Math.Max(0, Math.Min(Ready, percent));

        public static int Parse(string text) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? Clamp(value) : 0;

        public static string Format(int percent) => Clamp(percent).ToString(CultureInfo.InvariantCulture);

        // 0 when the text is not a usable time.
        public static double ParseTime(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double time) &&
            time > 0 && !double.IsInfinity(time) && !double.IsNaN(time) ? time : 0;

        public static string FormatTime(double time) => time.ToString("F3", CultureInfo.InvariantCulture);

        // Whether a new load report is worth a lobby write: the first one, every
        // ten points, and the finish.
        public static bool WorthPublishing(int percent, int published) =>
            published < 0 || (percent == Ready && published != Ready) || Math.Abs(percent - published) >= 10;

        // The host's call: start once every player is ready, or once it has
        // waited `waited` seconds past its own readiness.
        public static bool ShouldStart(IEnumerable<int> percents, double waited)
        {
            if (waited >= MaxWait) return true;
            foreach (int percent in percents) if (percent < Ready) return false;
            return true;
        }
    }

    // The stages of one PC's load, in order.
    public enum LoadStage { Loading, Opening, WarmingUp, Ready }

    public static class LoadProgress
    {
        // One percentage for the whole load. The scene's own progress stops at 90%
        // until it is allowed to switch on (Unity's rule), so it fills 0-70 here;
        // switching on is a single frozen step; warming up fills the rest.
        public static int Percent(LoadStage stage, double fraction)
        {
            if (double.IsNaN(fraction)) fraction = 0;
            fraction = Math.Max(0, Math.Min(1, fraction));
            switch (stage)
            {
                case LoadStage.Loading: return (int)Math.Floor(Math.Min(1, fraction / .9) * 70);
                case LoadStage.Opening: return 72;
                case LoadStage.WarmingUp: return 75 + (int)Math.Floor(fraction * 24);
                default: return MatchStart.Ready;
            }
        }
    }

    // After the scene switches on, its first frames are slow while shaders
    // compile and bodies settle. The loading screen stays up until frames run
    // smoothly for a while, so the race does not start inside that stutter.
    public sealed class WarmupMeter
    {
        public const double SmoothFrameMs = 50, MinSeconds = .5, MaxSeconds = 6;
        public const int StableFrames = 12;

        public double Elapsed { get; private set; }
        public int Stable { get; private set; }

        public void Add(double frameMs)
        {
            if (double.IsNaN(frameMs) || frameMs < 0) return;
            Elapsed += frameMs / 1000;
            Stable = frameMs <= SmoothFrameMs ? Stable + 1 : 0;
        }

        public bool Done => Elapsed >= MaxSeconds || (Elapsed >= MinSeconds && Stable >= StableFrames);

        public double Fraction => Done ? 1 : Math.Min(.99, Math.Max(Elapsed / MaxSeconds,
            Math.Min(1, Elapsed / MinSeconds) * Stable / StableFrames));
    }
}
