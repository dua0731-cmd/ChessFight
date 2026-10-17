using System;

namespace ChessFight.Network
{
    // Offline prototype rules. Inputs are all occupants of each plate (either allegiance), plus defusers per team.
    public sealed class ChessTimerRules
    {
        public sealed class TeamState
        {
            public float Remaining { get; internal set; }
            public float Recovery { get; internal set; }
            public float Work { get; internal set; }
            public int Stage { get; internal set; }
            public int Explosions { get; internal set; }
            public bool Complete => Stage >= 3;
        }
        public readonly TeamState[] Teams = { new TeamState(), new TeamState() };
        public int PressedTeam { get; private set; } = -1;
        public readonly float Limit, WorkSeconds, RecoverySeconds, SwitchDelay;
        public readonly int SoloRiders;
        int candidate = -1;
        float stable;

        public ChessTimerRules(float limit = 45, float workSeconds = 10, float recoverySeconds = 6,
            float switchDelay = .35f, int soloRiders = 2)
        {
            Limit = Math.Max(1, limit); WorkSeconds = Math.Max(1, workSeconds);
            RecoverySeconds = Math.Max(0, recoverySeconds); SwitchDelay = Math.Max(0, switchDelay);
            SoloRiders = Math.Max(1, soloRiders); Reset();
        }
        public void Reset()
        {
            foreach (var team in Teams)
            { team.Remaining = Limit; team.Recovery = team.Work = 0; team.Stage = team.Explosions = 0; }
            PressedTeam = candidate = -1; stable = 0;
        }
        // Returns an explosion bit mask. Timer expiry takes priority over work on the same step.
        public int Step(float dt, int blueRiders, int redRiders, int blueWorkers, int redWorkers)
        {
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return 0;
            int blue = Teams[0].Complete ? 0 : Math.Max(0, blueRiders);
            int red = Teams[1].Complete ? 0 : Math.Max(0, redRiders);
            int next = Teams[0].Complete ? (red >= SoloRiders ? 1 : -1)
                : Teams[1].Complete ? (blue >= SoloRiders ? 0 : -1)
                : blue == red ? -1 : blue > red ? 0 : 1;
            if (next != candidate) { candidate = next; stable = 0; }
            stable += dt;
            // No stale time protection while a different switch is settling.
            if (PressedTeam != next) PressedTeam = -1;
            if (stable >= SwitchDelay) PressedTeam = next;
            int explosions = 0;
            for (int i = 0; i < 2; i++)
            {
                var team = Teams[i];
                if (team.Complete) continue;
                if (team.Recovery > 0) { team.Recovery = Math.Max(0, team.Recovery - dt); continue; }
                if (PressedTeam != i) team.Remaining = Math.Max(0, team.Remaining - dt);
                if (team.Remaining <= 0)
                {
                    team.Explosions++; team.Remaining = Limit; team.Recovery = RecoverySeconds; team.Work = 0;
                    explosions |= 1 << i; continue;
                }
                int workers = Math.Max(0, i == 0 ? blueWorkers : redWorkers);
                team.Work += Math.Min(3, workers) * dt;
                if (team.Work >= WorkSeconds)
                {
                    team.Stage++; team.Work = 0;
                    if (team.Complete && PressedTeam == i) PressedTeam = -1;
                }
            }
            return explosions;
        }
    }
}
