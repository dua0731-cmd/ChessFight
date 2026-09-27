using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // Mode-only rules. No health, no immunity on getting up, and one score per life.
    public sealed class SwordFightRules
    {
        readonly Dictionary<ulong, double> respawning = new Dictionary<ulong, double>();
        public int White { get; private set; }
        public int Black { get; private set; }
        public int Target { get; }
        public double Duration { get; }
        public double RespawnDelay { get; }
        public double Elapsed { get; private set; }
        public bool Finished => White >= Target || Black >= Target || Elapsed >= Duration;
        public int Winner => !Finished || White == Black ? -1 : White > Black ? 0 : 1;

        public SwordFightRules(int target = 20, double duration = 240, double respawnDelay = 3)
        { Target = Math.Max(1, target); Duration = Math.Max(1, duration); RespawnDelay = Math.Max(0.1, respawnDelay); }

        public void Advance(double dt) { if (!Finished && dt > 0 && !double.IsInfinity(dt)) Elapsed = Math.Min(Duration, Elapsed + dt); }
        public bool Alive(ulong id) => !respawning.ContainsKey(id);
        public double RespawnRemaining(ulong id) => respawning.TryGetValue(id, out double at) ? Math.Max(0, at - Elapsed) : 0;
        public bool RingOut(ulong id, int victimTeam)
        {
            if (id == 0 || victimTeam < 0 || victimTeam > 1 || Finished || !Alive(id)) return false;
            respawning[id] = Elapsed + RespawnDelay;
            // A fall always awards the other team, including an unassisted fall.
            if (victimTeam == 0) Black++; else White++;
            return true;
        }
        public bool TryRespawn(ulong id)
        {
            if (Finished || !respawning.TryGetValue(id, out double at) || Elapsed < at) return false;
            return respawning.Remove(id);
        }
        public void Remove(ulong id) => respawning.Remove(id);
    }
}
