using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // Host-owned mission state, not a periodic scenery obstacle or a network packet.
    public sealed class KingRushSeesawRules
    {
        readonly KingRushRules match;
        readonly HashSet<ulong> crossed = new HashSet<ulong>();
        readonly int[] counts = new int[2];
        readonly bool[] access = new bool[2];
        double last;
        public double StartedAt { get; private set; } = -1;
        public double FirstComplete { get; private set; } = -1;
        public double Angle { get; private set; }
        public bool Started => StartedAt >= 0;
        public KingRushSeesawRules(KingRushRules rules) { match = rules; }
        public void Begin(double now)
        { if (!Started && Valid(now)) { StartedAt = last = now; } }
        static bool Valid(double n) => !double.IsNaN(n) && !double.IsInfinity(n) && n >= 0;
        public bool Bridges(double now) => Started && (now >= StartedAt + 150 || (FirstComplete >= 0 && now >= FirstComplete + 20));
        public bool Access(int team) => team >= 0 && team < 2 && access[team];
        public int Count(int team) => team >= 0 && team < 2 ? counts[team] : 0;
        public bool Crossed(ulong id) => crossed.Contains(id);
        public void Advance(double now, double weight)
        {
            if (!Started || !Valid(now) || now < last || double.IsNaN(weight) || double.IsInfinity(weight)) return;
            double target = Math.Max(-25, Math.Min(25, weight / 60 * 25));
            double step = 8 * (now - last); last = now;
            Angle += Math.Max(-step, Math.Min(step, target - Angle));
            double threshold = now >= StartedAt + 120 ? 10 : 15;
            for (int team = 0; team < 2; team++)
            {
                double up = team == 0 ? Angle : -Angle;
                if (Bridges(now) || up >= threshold) access[team] = true;
                else if (up < threshold - 3) access[team] = false;
            }
        }
        public bool Cross(ulong id, int team, double now)
        {
            if (!Started || id == 0 || team < 0 || team > 1 || !Valid(now) || now < last || !crossed.Add(id)) return false;
            counts[team]++;
            if (counts[team] == 4)
            { match.Complete(1, team, now); if (FirstComplete < 0) FirstComplete = now; }
            return true;
        }
    }
}
