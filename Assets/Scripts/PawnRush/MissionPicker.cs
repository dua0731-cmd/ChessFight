using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // MissionPicker (design doc v0.4 §5 "뽑기"): at the start of every round two of the five
    // mini-games are drawn (PawnRushMissions.Choose: two different ones, last round's left out),
    // one per mission plaza, and built into both teams' stations there - the same game for both.
    // Offline the draw is seeded from the clock; online the host will draw and send the pair.
    public sealed class MissionPicker : MonoBehaviour
    {
        [Tooltip("Plaza 1 white, plaza 1 black, plaza 2 white, plaza 2 black.")]
        [SerializeField] MissionStation[] stations = new MissionStation[4];
        [Tooltip("Draw two games when Play starts (off: keep the ones built in the scene).")]
        [SerializeField] bool drawOnStart = true;

        // The previous round's pair in this room (offline: this session).
        public static int LastFirst = PawnRushMissions.None, LastSecond = PawnRushMissions.None;

        public static event Action<MissionPicker> RoundStarted;

        public int First { get; private set; } = PawnRushMissions.None;
        public int Second { get; private set; } = PawnRushMissions.None;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            LastFirst = LastSecond = PawnRushMissions.None;
            RoundStarted = null;
        }

        public void Configure(MissionStation[] stations) => this.stations = stations;

        public MissionStation Station(int plaza, int team) => stations[(plaza - 1) * 2 + team];

        void Start()
        {
            if (drawOnStart) NewRound(Environment.TickCount);
            else
            {
                First = Station(1, 0) != null ? Station(1, 0).GameIndex : PawnRushMissions.None;
                Second = Station(2, 0) != null ? Station(2, 0).GameIndex : PawnRushMissions.None;
            }
        }

        // A new round: a fresh draw, every station rebuilt and its gate shut.
        public void NewRound(int seed)
        {
            var draw = PawnRushMissions.Choose(seed, LastFirst, LastSecond);
            Install(draw.First, draw.Second);
        }

        public void Install(int first, int second)
        {
            First = first;
            Second = second;
            LastFirst = first;
            LastSecond = second;
            for (int team = 0; team < 2; team++)
            {
                Station(1, team)?.Install(first);
                Station(2, team)?.Install(second);
            }
            Debug.Log($"[PawnRush] 이번 판 미니게임: 광장 ① {Name(first)}, 광장 ② {Name(second)}");
            RoundStarted?.Invoke(this);
        }

        // One plaza's game changed by hand (the playtest's F9/F10): the next in turn that the
        // other plaza does not have.
        public void Cycle(int plaza)
        {
            int current = plaza == 1 ? First : Second, other = plaza == 1 ? Second : First;
            int next = current;
            do next = (next + 1 + PawnRushMissions.Count) % PawnRushMissions.Count;
            while (next == other);
            if (plaza == 1) Install(next, Second);
            else Install(First, next);
        }

        // Every station's game back to its start and every gate shut, the same games.
        public void ResetRound()
        {
            foreach (var s in stations)
                if (s != null) s.ResetRound();
        }

        public static string Name(int game) =>
            game >= 0 && game < PawnRushMissions.Count ? PawnRushMissions.Names[game] : "없음";
    }
}
