using System;

namespace ChessFight.Network
{
    // A wave starts its countdown only once all authored promotion slots are claimed.
    // Ready is not Released: the owner must gather late players before opening the wall.
    public sealed class KingRushRallyRules
    {
        public const double Delay = 10;
        public int Wave { get; private set; }
        public int Required { get { return Wave + 2; } }
        public int Claimed { get; private set; }
        public double StartedAt { get; private set; } = -1;
        public bool Released { get; private set; }
        double last = -1;
        public KingRushRallyRules(int wave)
        { if (wave < 0 || wave > 2) throw new ArgumentOutOfRangeException("wave"); Wave = wave; }
        public void Advance(KingRushRules rules, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0 || now < last) return;
            last = now; Claimed = 0;
            for (int i = 0; i < 9; i++)
                if (KingRushRules.Wave(i) == Wave && rules.TryGetClaim(i, out _)) Claimed++;
            if (StartedAt < 0 && Claimed == Required) StartedAt = now;
        }
        public bool Ready(double now) => StartedAt >= 0 && !double.IsNaN(now) && !double.IsInfinity(now) && now >= StartedAt + Delay;
        public double Remaining(double now) => StartedAt < 0 ? Delay : Math.Max(0, StartedAt + Delay - now);
        public bool Release(double now)
        { if (Released || !Ready(now)) return false; Released = true; return true; }
        public void ReleaseForTest() { Released = true; }
    }
}
