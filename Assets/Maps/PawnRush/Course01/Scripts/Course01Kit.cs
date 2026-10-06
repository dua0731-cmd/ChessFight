using UnityEngine;

namespace ChessFight.PawnRush
{
    // Everything the course builder takes from the project: the course materials and
    // the imported obstacle prefabs (Assets/Maps/ImportedChessFight/Prefabs/Obstacles,
    // used as they are; values the course changes are set on the placed instance).
    // One asset, Data/Course01Kit.asset.
    [CreateAssetMenu(menuName = "ChessFight/Pawn Rush/Course Kit", fileName = "Course01Kit")]
    public sealed class Course01Kit : ScriptableObject
    {
        [Header("Materials")]
        [Tooltip("Walkable floor: cream and brown 2 m checker on top.")]
        public Material floorChecker;
        [Tooltip("Climbable wall: block-jointed stone on the sides.")]
        public Material wallClimbable;
        [Tooltip("Unclimbable wall: smooth marble on the sides. Always paired with NoClimbSurface.")]
        public Material wallNoClimb;
        public Material glass;
        public Material trimWhite;
        public Material trimBlack;
        public Material rankGold;
        [Tooltip("Course-built moving parts (spin bars, rook walls, the start bar, the slot door).")]
        public Material hazard;

        [Header("Obstacle prefabs")]
        public GameObject spinningDisc;      // 01
        public GameObject jumpPad;           // 02
        public GameObject pusher;            // 03
        public GameObject hammer;            // 04 (X2)
        public GameObject risingTiles;       // 05
        public GameObject slidingWalls;      // 06 (X1, X2)
        public GameObject conveyor;          // 08 (X1)
        public GameObject clockGates;        // 09
        public GameObject fallingPiece;      // 10
        public GameObject pendulum;          // 11
        public GameObject foldingBridge;     // 12
        public GameObject airVent;           // 15
        public GameObject knightCavalry;     // 17

        public Material Team(int team) => team == Gameplay.Teams.Black ? trimBlack : trimWhite;
    }
}
