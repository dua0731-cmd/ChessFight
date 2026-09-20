using UnityEngine;

namespace ChessFight.ProtectKing
{
    public enum MatchPhase { Ready, Running, Overtime, Finished }

    public sealed class ProtectTheKingMatchController : MonoBehaviour
    {
        [Min(1)] public float regulationSeconds = 300;
        [Min(0)] public float overtimeSeconds = 60;
        public StageMap map;
        public ThroneInteraction throne;
        public MatchPhase Phase { get; private set; } = MatchPhase.Ready;
        public float Remaining { get; private set; }
        public float Elapsed { get; private set; }
        public string Result { get; private set; } = "";
        public string Notice { get; private set; } = "";
        public float NoticeUntil { get; private set; }
        public bool HasAuthority { get; set; } = true;
        public void ApplyRemote(MatchPhase phase, float elapsed, float remaining, byte result)
        {
            if (HasAuthority) return;
            Phase=phase; Elapsed=elapsed; Remaining=remaining;
            Result=result==1?"BLUE WINS":result==2?"RED WINS":result==3?"DRAW - time expired":"";
        }
        public void ReturnToReady()
        {
            if(!HasAuthority)return;
            Phase=MatchPhase.Ready;Elapsed=0;Remaining=regulationSeconds;Result="";
            throne.ResetProgress();map.ResetPlayers();
        }
        public bool IsRunning => Phase == MatchPhase.Running || Phase == MatchPhase.Overtime;

        void Awake() { Remaining = regulationSeconds; }

        void Update() { AdvanceClock(Time.deltaTime); }

        public void StartMatch()
        {
            if (!HasAuthority || IsRunning) return;
            map.ResetPlayers();
            throne.ResetProgress();
            Elapsed = 0;
            Remaining = regulationSeconds;
            Result = "";
            Phase = MatchPhase.Running;
            Notify("Protect your King. Pass CP1 to CP5 in order.");
        }

        public void AdvanceClock(float seconds)
        {
            if (!HasAuthority || !IsRunning || seconds <= 0) return;
            Elapsed += seconds;
            Remaining -= seconds;
            if (Remaining > 0) return;
            if (Phase == MatchPhase.Running && overtimeSeconds > 0)
            {
                Phase = MatchPhase.Overtime;
                Remaining += overtimeSeconds;
                Notify("OVERTIME - the throne is still open!");
                if (Remaining > 0) return;
            }
            Remaining = 0;
            Phase = MatchPhase.Finished;
            Result = "DRAW - time expired";
            throne.ResetProgress();
        }

        // Called only by the throne after eligibility and continuous input have been checked.
        public bool TryFinish(PlayerIdentity winner)
        {
            if (!HasAuthority || !IsRunning || winner == null || winner.piece != PieceType.King ||
                winner.checkpoint != 5 || !throne.IsComplete(winner)) return false;
            Phase = MatchPhase.Finished;
            Result = winner.team.ToString().ToUpperInvariant() + " WINS";
            Notify(Result);
            return true;
        }

        public void Notify(string text)
        {
            Notice = text;
            NoticeUntil = Time.unscaledTime + 3;
        }
    }
}
