using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The first-run tutorial's numbers (R829, Docs/Player/TUTORIAL_DESIGN_v0.1.md): where each lesson starts, where its
    /// gold square is and what counts as done. The map itself is the ragdoll lab's (LabLayout); the scene builder
    /// (RagdollLabEditor/TutorialSceneBuilder) places the new props from these same numbers, the course (TutorialCourse)
    /// checks against them.
    /// </summary>
    public static class TutorialLayout
    {
        // Start: the lab's P1 spawn, looking north.
        public static readonly Vector3 Start = LabLayout.SpawnP1;
        public const float StartYaw = 0f;

        // Lesson 1: walk to the gold square in the middle.
        public static readonly Vector3 WalkMark = new Vector3(0f, 0f, -1f);

        // Lesson 2: two low hurdles across the way east. 0.45 m is above what the Pawn Rush pawn walks up by itself
        // (PawnRushPhysicsParams.stepUpHeight 0.25 m) and far below its jump (hips about 1.09 m).
        public static readonly float[] HurdleX = { 3.2f, 5.4f };
        public const float HurdleZ = -1f;
        public const float HurdleLength = 5f;      // along z
        public const float HurdleHeight = 0.45f;
        public const float HurdleThickness = 0.3f;
        public static readonly Vector3 JumpStart = new Vector3(1.2f, 0f, -1f);
        public static readonly Vector3 JumpMark = new Vector3(7.3f, 0f, -1f);

        // Lesson 3: the lab's limbo bar (clearance 0.55 m), passed under from the north.
        public static readonly Vector3 DiveStart = new Vector3(9f, 0f, -3.6f);
        public const float LimboHalfSpan = 1.9f;   // the posts' inner faces are 2.0 m from the middle
        public static readonly Vector3 DiveMark = new Vector3(9f, 0f, -8.8f);
        public const float UnderBarDiveWindow = 2f;   // s: the crossing counts this long after the dive (the slide on the floor)

        // Lesson 4: sprint east to the 2 m wall (about 8 m), at least SprintNeeded seconds without a break.
        // Lesson 3-2: a dive from a jump, back west from the bar's far side along the open floor (the tunnel is 2.7 m south).
        public static readonly Vector3 AirDiveMark = new Vector3(4f, 0f, -8.8f);
        public static readonly Vector3 SprintStart = AirDiveMark;
        public const float SprintNeeded = 1.2f;
        public static readonly Vector3 SprintMark = new Vector3(18.3f, 0f, LabLayout.WallZ[0]);

        // Lessons 5 and 6: up the 2 m wall from a jump (ledge grab), up the 3 m wall by climbing.
        public static readonly Vector3 LedgeStart = SprintMark;
        public static readonly Vector3 ClimbStart = new Vector3(18.3f, 0f, LabLayout.WallZ[1]);

        // Lesson 7 (user 11-25): tackle, grab and get free, in a yard west of the walls where three practice bots walk to
        // and fro (TutorialBots). Clear of the hurdles (x 3.2 / 5.4, z -3.5..1.5) and the limbo bar (z -7).
        public static readonly Vector3 FightStart = new Vector3(16.5f, 0f, 5f);
        public const float FightStartYaw = 270f;
        public static readonly Vector3 FightMark = new Vector3(11f, 0f, 5f);
        public static readonly (Vector3 a, Vector3 b)[] BotLanes =
        {
            (new Vector3(8f, 0f, 3f), new Vector3(14f, 0f, 3f)),
            (new Vector3(8f, 0f, 5f), new Vector3(14f, 0f, 5f)),
            (new Vector3(8f, 0f, 7f), new Vector3(14f, 0f, 7f)),
        };
        // Where the bots keep to (they choose their own spots in it; TutorialBots), on the ground plan: x 7.5..14.5, z 2..8.
        public static readonly Rect BotYard = new Rect(7.5f, 2f, 7f, 6f);
        public const float TackleWindow = 1.5f;   // a bot that falls this long after the player's dive, near it, was tackled
        public const float TackleRange = 3f;
        public const float GrabHold = 0.8f;       // holding a bot this long
        public const float HeldBeforeEscape = 0.3f;

        // Lesson 10: the lab's beam, then the gold door on its landing.
        public static readonly Vector3 BeamStart = new Vector3(0f, 0f, LabLayout.BeamStartZ + 1.2f);
        public const float BeamStartYaw = 180f;
        public static readonly Vector3 GoalMark = new Vector3(0f, 0f, LabLayout.BeamEndZ - 3f);

        // The moat round the map, as TutorialMoatWater draws it (the scene's surfaceY -7, bedY -14, moat -52,-47 .. 47,42).
        public static readonly Vector3 MoatCenter = new Vector3(-2.5f, -10.5f, -2.5f);
        public static readonly Vector3 MoatSize = new Vector3(99f, 7f, 89f);
        public const float MoatRespawnDelay = 2.5f;   // s afloat before the lesson's start again

        // Under the whole map, below the moat's water (top -12): a fall comes back to the lesson's start.
        public static readonly Vector3 KillCenter = new Vector3(0f, LabLayout.KillHeight - 4f, -5f);
        public static readonly Vector3 KillSize = new Vector3(140f, 8f, 120f);

        /// <summary>A wall top (lessons 5 and 6) as a box on the ground plan: x and z of the wall, any height.</summary>
        public static Rect WallTop(int wall) => new Rect(LabLayout.WallFrontX, LabLayout.WallZ[wall] - LabLayout.WallWidth * 0.5f,
                                                        LabLayout.WallDepth, LabLayout.WallWidth);

        public static Vector3 WallTopMark(int wall) =>
            new Vector3(LabLayout.WallFrontX + LabLayout.WallDepth * 0.5f, LabLayout.WallHeights[wall], LabLayout.WallZ[wall]);
    }
}
