using System;

namespace ChessFight.Network
{
    // A client's obstacle clock, read off the host's snapshots.
    //
    // The host stamps every snapshot with the obstacle time its physics had when the poses were
    // captured. A client draws the pawns at a playback moment between two snapshots; feeding the
    // obstacle time interpolated at that same moment here puts every obstacle where the host had it
    // when it judged those pawns. Steam's server clock cannot do that: it only ticks in whole
    // seconds, is not ms-accurate between machines, knows nothing of the network delay or the
    // playback rate, and keeps running while an overloaded host's physics falls behind.
    //
    // Sampled once per frame, read by every physics step, so between samples it runs on at the
    // playback rate for a short while. It never steps backwards by a small amount (that would shake
    // anything standing on a platform); a large jump back is a new stream and is taken at once.
    public sealed class HostObstacleClock
    {
        public const double MaxExtrapolation = 0.1;   // seconds past the last sample
        public const double ResetBelow = 0.25;        // a step back this large is a new stream

        double sample, sampleAt, rate = 1, last;
        bool hasSample, hasLast;

        public bool HasSample => hasSample;

        public static double Interpolate(double from, double to, double t) =>
            from + (to - from) * Math.Max(0.0, Math.Min(1.0, t));

        // obstacleTime: the host's obstacle time at the moment being drawn. now: local game time.
        public void Sample(double obstacleTime, double now, double playbackRate)
        {
            if (double.IsNaN(obstacleTime) || double.IsInfinity(obstacleTime)) return;
            if (hasLast && obstacleTime < last - ResetBelow) hasLast = false;
            sample = obstacleTime;
            sampleAt = now;
            rate = Math.Max(0.5, Math.Min(1.5, playbackRate));
            hasSample = true;
        }

        public double Now(double now)
        {
            if (!hasSample) return 0;
            double elapsed = Math.Max(-MaxExtrapolation, Math.Min(MaxExtrapolation, now - sampleAt));
            double value = sample + elapsed * rate;
            if (hasLast && value < last) return last;
            last = value;
            hasLast = true;
            return value;
        }

        public void Clear() { hasSample = hasLast = false; sample = sampleAt = last = 0; rate = 1; }
    }
}
