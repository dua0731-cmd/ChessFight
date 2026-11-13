using UnityEngine;

namespace ChessFight.PawnRush
{
    // Everything a course builder (CourseBuilder) takes from the project: the course materials and the
    // imported obstacle prefabs (Assets/Prefabs/Obstacles, used as they are; values a course changes are
    // set on the placed instance). Course 01 has its kit (Course01Kit.asset); the section prefabs have
    // theirs (SectionKit, the 1 m grid blockout look of the section level designs).
    public abstract class CourseKit : ScriptableObject
    {
        [Header("Materials")]
        [Tooltip("Walkable floor (course 01: cream and brown 2 m checker; sections: blue 1 m grid on top).")]
        public Material floorChecker;
        [Tooltip("Climbable wall: block-jointed stone on the sides.")]
        public Material wallClimbable;
        [Tooltip("Unclimbable wall (course 01: smooth marble; sections: grey 1 m grid). Always paired with NoClimbSurface.")]
        public Material wallNoClimb;
        public Material glass;
        public Material trimWhite;
        public Material trimBlack;
        public Material rankGold;
        [Tooltip("Mini-game boards and planks: plain cream and plain brown (A's squares, B's planks, C's drawbridge).")]
        public Material boardLight;
        public Material boardDark;
        [Tooltip("Course-built moving parts (spin bars, rook walls, the start bar, rolling statues).")]
        public Material hazard;
        [Tooltip("Sections: checkpoints and rest spots (sky blue).")]
        public Material marker;
        [Tooltip("Sections: the floor a fall ends on (the respawn floor).")]
        public Material respawnFloor;

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

        // A material that may be missing in an older kit asset: the fallback instead.
        public static Material Or(Material material, Material fallback) => material != null ? material : fallback;
    }
}
