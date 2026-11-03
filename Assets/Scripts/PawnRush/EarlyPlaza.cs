using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The root of an early mini-game section, the palace front plaza (R153, Docs/KingRush/PAWN_RUSH_EARLY_PLAZA.md;
    // zone plan: mini-game zones v0.1 §3): the early zone's one plaza shell - the forecourt wall either side of the
    // arch, a front walk, two 20 x 20 m stations and the aisle, the palace facade with its closed main gate and the
    // two team gates, the passages meeting in a vestibule - with one of the zone's games in both stations. Each game
    // is its own prefab (Early_C_MoatDrawbridge first; D's portcullis gatehouse and A's ceremonial board later).
    //
    // The shell is built from code (EarlyPlazaBuilder). The menu ChessFight > Pawn Rush > Build Early Plaza C bakes
    // it into the prefab; a prefab that does not hold it yet builds it when it wakes. The game itself is installed
    // into the stations when Play starts (MissionStation.Install): its parts use materials made at run time, which a
    // prefab cannot keep.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class EarlyPlaza : MonoBehaviour, ISectionPart
    {
        public const string ContentName = "Content";

        [SerializeField] CourseKit kit;
        [Tooltip("The game both stations play (PawnRushMissions: 2 = C drawbridge).")]
        [SerializeField] int game = PawnRushMissions.Drawbridge;
        [Tooltip("Build the plaza from code when the prefab does not hold it yet (the Build menu bakes it in).")]
        [SerializeField] bool buildIfEmpty = true;
        [Tooltip("White's station, black's (the builder sets them).")]
        [SerializeField] MissionStation[] stations = new MissionStation[2];
        [Tooltip("The plain approach in the band's place, on only when no section comes before this one.")]
        [SerializeField] GameObject standIn;

        public CourseKit Kit => kit;
        public int Game => game;
        public bool HasContent => transform.Find(ContentName) != null;

        public MissionStation Station(int team) =>
            stations != null && team >= 0 && team < stations.Length ? stations[team] : null;

        public void Configure(MissionStation white, MissionStation black, GameObject standIn)
        {
            stations = new[] { white, black };
            this.standIn = standIn;
        }

        // The round placed this section: with no section before it (the round's first, or dropped in a scene on its
        // own) the stand-in approach takes the obstacle section's band.
        public void OnSection(PawnRushSection section)
        {
            var round = PawnRushRound.Current;
            bool first = round == null || round.Sections.Count == 0 || round.Sections[0] == section;
            if (standIn != null) standIn.SetActive(first);
        }

        void Awake()
        {
            if (buildIfEmpty && !HasContent && kit != null) EarlyPlazaBuilder.Build(this);
        }

        void Start() => InstallGames();

        // Both stations get this plaza's game afresh, their gates shut (also the validator's, in Edit mode).
        public void InstallGames()
        {
            for (int team = 0; team < 2; team++)
            {
                var station = Station(team);
                if (station != null) station.Install(game);
            }
        }

        // The same game back to its start, the gates shut and the promotion pads ready again (the playtest's restart).
        public void ResetRound()
        {
            for (int team = 0; team < 2; team++)
            {
                var station = Station(team);
                if (station != null) station.ResetRound();
            }
        }

        // The station of `team` (the playtest's F6: finish our game).
        public MiniGameBase GameOf(int team)
        {
            var station = Station(team == Teams.Black ? Teams.Black : Teams.White);
            return station != null ? station.Game : null;
        }
    }
}
