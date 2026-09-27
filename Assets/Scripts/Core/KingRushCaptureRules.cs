using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // Blue1 only. Capture reservations last until the host has moved the body out.
    // This is not part of KR1 (which still transports only gates and promotions).
    public sealed class KingRushCaptureRules
    {
        public const int Target = 6;
        public const double Overtime = 120, AutoInterval = 10;
        readonly KingRushRules gates;
        readonly int[] planks = new int[2], captures = new int[2];
        readonly Dictionary<ulong, double> detained = new Dictionary<ulong, double>();
        int autoSteps;
        double lastTime;
        public double StartedAt { get; private set; } = -1;
        public bool Started => StartedAt >= 0;
        public KingRushCaptureRules(KingRushRules gates) { this.gates = gates ?? throw new ArgumentNullException(nameof(gates)); }
        public int Planks(int team) => ValidTeam(team) ? planks[team] : 0;
        public int Captures(int team) => ValidTeam(team) ? captures[team] : 0;
        public bool Detained(ulong id) => detained.ContainsKey(id);
        public double ReleaseAt(ulong id) => detained.TryGetValue(id, out double at) ? at : -1;
        public void Release(ulong id) => detained.Remove(id);
        public void Begin(double now)
        { if (!Started && ValidTime(now)) { StartedAt = lastTime = now; } }
        public void Advance(double now)
        {
            if (!Started || !ValidTime(now) || now < lastTime) return;
            lastTime = now;
            int steps = Math.Min(Target, (int)Math.Max(0, Math.Floor((now - StartedAt - Overtime) / AutoInterval)));
            while (autoSteps < steps)
            {
                double at = StartedAt + Overtime + (++autoSteps) * AutoInterval;
                AddPlank(0, at); AddPlank(1, at);
            }
        }
        public bool Deposit(ulong victim, int victimTeam, int boxTeam, double now)
        {
            if (!Started || victim == 0 || !ValidTeam(victimTeam) || !ValidTeam(boxTeam) ||
                !ValidTime(now) || now < lastTime || detained.ContainsKey(victim)) return false;
            Advance(now);
            bool enemy = victimTeam != boxTeam;
            detained.Add(victim, now + (enemy ? 3 : 2));
            if (enemy && planks[boxTeam] < Target) { captures[boxTeam]++; AddPlank(boxTeam, now); }
            return true;
        }
        void AddPlank(int team, double now)
        { if (planks[team] < Target && ++planks[team] == Target) gates.Complete(0, team, now); }
        public int VisiblePlanks(int team, double now) => gates.IsOpen(0, team, now) ? Target : Planks(team);
        static bool ValidTeam(int team) => team == 0 || team == 1;
        static bool ValidTime(double now) => !double.IsNaN(now) && !double.IsInfinity(now) && now >= 0 && now <= 86400;
    }
}
