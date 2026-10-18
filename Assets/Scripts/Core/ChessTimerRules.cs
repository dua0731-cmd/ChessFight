using System;

namespace ChessFight.Network
{
    // Offline prototype rules. Inputs are all occupants of each plate (either allegiance), plus defusers per team.
    public sealed class ChessTimerRules
    {
        public sealed class TeamState
        {
            public float Remaining { get; internal set; }
            public float Work { get; internal set; }
            public int Stage { get; internal set; }
            public int Explosions { get; internal set; }
            public bool Complete => Stage >= 3;
            public bool Expired { get; internal set; }
            public bool Finished => Complete || Expired;
            public bool GateOpen { get; internal set; }
        }
        public readonly TeamState[] Teams = { new TeamState(), new TeamState() };
        public int Winner { get; private set; } = -2; // -2 undecided, -1 tied, otherwise team index.
        public int ExpiredTeams { get; private set; }
        public int CompletedTeams { get; private set; }
        public int PressedTeam { get; private set; } = -1;
        public readonly float Limit, WorkSeconds, SwitchDelay;
        public readonly int SoloRiders;
        public float WrongWirePenalty => Limit / 9f;
        int candidate = -1;
        float stable;

        public ChessTimerRules(float limit = 45, float workSeconds = 30,
            float switchDelay = .35f, int soloRiders = 2)
        {
            Limit = Math.Max(1, limit); WorkSeconds = Math.Max(1, workSeconds);
            SwitchDelay = Math.Max(0, switchDelay);
            SoloRiders = Math.Max(1, soloRiders); Reset();
        }
        public void Reset()
        {
            foreach (var team in Teams)
            { team.Remaining = Limit; team.Work = 0; team.Expired = team.GateOpen = false; team.Stage = team.Explosions = 0; }
            PressedTeam = candidate = -1; stable = 0; CompletedTeams = ExpiredTeams = 0; Winner = -2;
        }
        // Returns an explosion bit mask. Timer expiry takes priority over work on the same step.
        public int Step(float dt, int blueRiders, int redRiders, int blueWorkers, int redWorkers)
            => Advance(dt, blueRiders, redRiders, blueWorkers, redWorkers, false, false, false, 0, 0);

        // Physical puzzles submit one-shot results; timer expiry still precedes success in the same tick.
        public int StepPuzzle(float dt, int blueRiders, int redRiders, bool blueSolved, bool redSolved,
            float bluePenalty = 0, float redPenalty = 0)
            => Advance(dt, blueRiders, redRiders, 0, 0, true, blueSolved, redSolved, bluePenalty, redPenalty);

        int Advance(float dt, int blueRiders, int redRiders, int blueWorkers, int redWorkers,
            bool puzzle, bool blueSolved, bool redSolved, float bluePenalty, float redPenalty)
        {
            CompletedTeams = ExpiredTeams = 0;
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return 0;
            int blue = Teams[0].Finished ? 0 : Math.Max(0, blueRiders);
            int red = Teams[1].Finished ? 0 : Math.Max(0, redRiders);
            int next = Teams[0].Finished ? (red >= SoloRiders ? 1 : -1)
                : Teams[1].Finished ? (blue >= SoloRiders ? 0 : -1)
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
                if (team.Finished) continue;
                float penalty = i == 0 ? bluePenalty : redPenalty;
                if (!float.IsNaN(penalty) && !float.IsInfinity(penalty)) team.Remaining = Math.Max(0, team.Remaining - Math.Max(0, penalty));
                if (PressedTeam != i) team.Remaining = Math.Max(0, team.Remaining - dt);
                if (team.Remaining <= 0)
                {
                    team.Expired = true; ExpiredTeams |= 1 << i;
                    explosions |= 1 << i; continue;
                }
                if (puzzle)
                {
                    if (i == 0 ? blueSolved : redSolved) team.Stage = 3;
                }
                else
                {
                    int workers = Math.Max(0, i == 0 ? blueWorkers : redWorkers);
                    team.Work += Math.Min(3, workers) * dt;
                    if (team.Work >= WorkSeconds) { team.Stage++; team.Work = 0; }
                }
                if (team.Complete)
                {
                    team.GateOpen = true; CompletedTeams |= 1 << i;
                    if (PressedTeam == i) PressedTeam = -1;
                }
            }
            // A timeout opens both physical exits, without pretending the other puzzle is solved.
            if (ExpiredTeams != 0) Teams[0].GateOpen = Teams[1].GateOpen = true;
            // Resolve both completions together, after both clocks have advanced.
            if (CompletedTeams != 0 && Winner == -2)
            {
                if (CompletedTeams == 3)
                {
                    float difference = Teams[0].Remaining - Teams[1].Remaining;
                    Winner = Math.Abs(difference) <= .001f ? -1 : difference > 0 ? 0 : 1;
                }
                else Winner = CompletedTeams == 1 ? 0 : 1;
                explosions |= Winner == -1 ? 3 : 1 << (1 - Winner);
            }
            for (int i = 0; i < 2; i++)
                if ((explosions & (1 << i)) != 0) Teams[i].Explosions++;
            if (Teams[0].Finished && Teams[1].Finished) PressedTeam = -1;
            return explosions;
        }
    }
}
