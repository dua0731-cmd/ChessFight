using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The root of the mid-game obstacle section 2-1, the grand stair hall (Assets/Prefabs/PawnRushSections/
    // Mid_2-1_GrandStairHall.prefab; level design v0.3, Docs/KingRush/PAWN_RUSH_GRAND_STAIR_HALL_DESIGN_v0.3.md).
    //
    // The hall is built from code (GrandStairHallBuilder, the design doc's tables line by line). The menu
    // ChessFight > Pawn Rush > Build Mid 2-1 Grand Stair Hall bakes it into the prefab; a prefab that does not
    // hold it yet builds it when it wakes, so it can be played straight from git.
    //
    // The round's team size switches the wings (3 or 4 a side: one wing, the seed picks which, the other
    // behind its portcullis with its obstacles stopped) and the lift landing (one bundle, the east half a
    // wall). GrandStairHallRules (Core) holds the rule.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class GrandStairHall : MonoBehaviour, ISectionPart
    {
        public const string Id = "Mid_2-1_GrandStairHall";
        public const string ContentName = "Content";

        [SerializeField] CourseKit kit;
        [Tooltip("Build the hall from code when the prefab does not hold it yet (the Build menu bakes it in).")]
        [SerializeField] bool buildIfEmpty = true;

        [Header("Built parts (the builder sets these)")]
        [Tooltip("West, east: the wings whose obstacles stop when they are shut.")]
        [SerializeField] Transform[] wings = new Transform[2];
        [SerializeField] GameObject[] portcullis = new GameObject[2];
        [SerializeField] GameObject eastLift;
        [SerializeField] GameObject eastLiftWall;

        public CourseKit Kit => kit;
        public bool HasContent => transform.Find(ContentName) != null;
        public bool WingOpen(int wing) => portcullis != null && wing < portcullis.Length && portcullis[wing] != null && !portcullis[wing].activeSelf;

        void Awake()
        {
            if (buildIfEmpty && !HasContent && kit != null) GrandStairHallBuilder.Build(this);
        }

        // The builder's parts.
        public void Bind(Transform west, Transform east, GameObject westGate, GameObject eastGate, GameObject lift, GameObject liftWall)
        {
            wings = new[] { west, east };
            portcullis = new[] { westGate, eastGate };
            eastLift = lift;
            eastLiftWall = liftWall;
        }

        public void OnSection(PawnRushSection section)
        {
            bool both = GrandStairHallRules.BothWings(section.TeamSize);
            for (int wing = 0; wing < 2; wing++)
                SetWing(wing, GrandStairHallRules.WingOpen(wing, section.TeamSize, section.Seed));
            if (eastLift != null) eastLift.SetActive(both);
            if (eastLiftWall != null) eastLiftWall.SetActive(!both);
        }

        void SetWing(int wing, bool open)
        {
            if (portcullis != null && wing < portcullis.Length && portcullis[wing] != null) portcullis[wing].SetActive(!open);
            if (wings == null || wing >= wings.Length || wings[wing] == null) return;
            // A shut wing's obstacles stand still (level design v0.3 §3): the imported obstacles' scripts off, and
            // the moving stair (R150).
            foreach (var behaviour in wings[wing].GetComponentsInChildren<Behaviour>(true))
                if (behaviour != null && (behaviour.GetType().Namespace == "ChessFight.ProtectKing" || behaviour is SwingingStair))
                    behaviour.enabled = open;
        }
    }
}
