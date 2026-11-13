using UnityEngine;

namespace ChessFight.PawnRush
{
    // One fixed mini-game plaza, drawn as a section prefab rather than rebuilt by the legacy two-plaza picker.
    [DefaultExecutionOrder(-450)]
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class MiniGameSection : MonoBehaviour, ISectionPart
    {
        public const string ContentName = "Content";
        public const string LayoutMark = "MiniGameSection_R312";

        [SerializeField] Course01Kit kit;
        [SerializeField, Range(0, 9)] int game;
        [SerializeField, Range(0, 2)] int zone;
        [SerializeField] bool buildIfEmpty = true;
        [SerializeField] MissionStation[] stations = new MissionStation[0];

        bool configured;
        int configuredSeed, configuredSlot;
        double configuredClock;

        public Course01Kit Kit => kit;
        public int GameIndex => game;
        public int Zone => zone;
        public MissionStation[] Stations => stations;
        public bool HasContent => transform.Find(ContentName + "/" + LayoutMark) != null;

        public void Configure(Course01Kit sourceKit, int gameIndex, int palaceZone)
        {
            kit = sourceKit;
            game = Mathf.Clamp(gameIndex, 0, 9);
            zone = Mathf.Clamp(palaceZone, 0, 2);
        }

        internal void SetStations(MissionStation[] value) => stations = value;

        void Awake()
        {
            if (kit == null) return;
            if (!HasContent || stations == null || stations.Length != 2 || stations[0] == null || stations[1] == null)
            {
                if (buildIfEmpty) MiniGameSectionBuilder.Build(this);
                return;
            }
            // Several original games keep runtime arrays outside Unity serialization. Recreate only the game
            // sockets after loading a baked prefab; the authored shell remains intact.
            MiniGameSectionBuilder.RebuildGames(this);
        }

        public void OnSection(PawnRushSection section)
        {
            if (stations == null || stations.Length != 2) return;
            int plaza = section.Slot / 2 + 1;
            bool rebound = false;
            foreach (var station in stations)
            {
                if (station == null) continue;
                if (station.Plaza != plaza)
                {
                    station.Configure(station.Team, plaza, station.Mirrored, station.Gate, station.Socket, kit);
                    rebound = true;
                }
            }
            bool changed = configured && (configuredSeed != section.Seed || configuredSlot != section.Slot || configuredClock != section.StartClock);
            if (rebound || changed) MiniGameSectionBuilder.RebuildGames(this);
            configured = true;
            configuredSeed = section.Seed;
            configuredSlot = section.Slot;
            configuredClock = section.StartClock;
        }

        public void ResetGames()
        {
            foreach (var station in stations)
                if (station != null) station.ResetRound();
        }
    }
}
