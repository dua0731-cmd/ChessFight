using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>Test scene coordinates shared by the scene builder and the automated checks.</summary>
    public static class LabLayout
    {
        public const float MainHalfSize = 15f;

        public static readonly Vector3 SpawnP1 = new Vector3(-1.5f, 0f, -6f);
        public static readonly Vector3 SpawnP2 = new Vector3(1.5f, 0f, -6f);
        public static readonly Vector3[] DummySpawns =
        {
            new Vector3(0f, 0f, -2.5f),
            new Vector3(-3f, 0f, 0.5f),
            new Vector3(3f, 0f, 0.5f),
            new Vector3(0f, 0f, 3f),
        };

        // [2] Rotating bar (north)
        public static readonly Vector3 BarCenter = new Vector3(0f, 0f, 24f);
        public const float BarLength = 13f;
        public const float BarHeight = 0.45f;
        public const float BarRadius = 0.2f;

        // [3] Walls 2 / 3 / 4 m (east)
        public const float WallFrontX = 20f;
        public const float WallDepth = 5f;
        public const float WallWidth = 6f;
        public static readonly float[] WallHeights = { 2f, 3f, 4f };
        public static readonly float[] WallZ = { -9f, 0f, 9f };

        // [3b] Climbing faces along the north edge. Lane 0 is the real one: a wall far taller
        // than one stamina bar, with rock ledges big enough to stand on. You climb, top out on a
        // ledge, get your breath back, and go again - the wall is a series of decisions, not one
        // long hold of a key. Lanes 1 and 2 are shape tests (overhang, bulge).
        public const float ClimbFaceZ = 13.5f;       // the faces look toward -Z; the pawn approaches from -Z
        public const float ClimbHeight = 5f;         // the shape-test lanes
        public const float ClimbWidth = 3f;
        public static readonly float[] ClimbX = { -9f, 0f, 9f };   // ledges / overhang / curved

        // Lane 0: the big wall
        // Four blocks stacked and set back, like a staircase for a giant. The top of each block is
        // the rest ledge: climb one, flop onto it, get your breath, climb the next. No special ledge
        // logic is needed because the wall genuinely ends at every rest point.
        public const float LedgeWallWidth = 6f;
        public const float LedgeStepHeight = 3f;     // one block, just under one stamina bar
        public const float LedgeStepBack = 1.5f;     // how far each block sits behind the one below
        public const int LedgeSteps = 4;             // 12 m total

        // [4] Slopes 15 / 30 / 45 degrees (west), descending from a 5 m platform toward +X
        public const float PlatformHeight = 5f;
        public const float PlatformEdgeX = -35f;
        public const float PlatformBackX = -40f;
        public static readonly float[] SlopeAngles = { 15f, 30f, 45f };
        public static readonly float[] SlopeZ = { -9f, 0f, 9f };
        public const float SlopeWidth = 6f;

        // [5] Narrow beam over a drop (south)
        public const float BeamWidth = 0.6f;
        public const float BeamStartZ = -15f;
        public const float BeamEndZ = -31f;

        // [6] Low obstacles on the main floor
        public static readonly Vector3 LimboCenter = new Vector3(9f, 0f, -7f);
        public const float LimboClearance = 0.55f;
        public static readonly Vector3 TunnelCenter = new Vector3(9f, 0f, -11.5f);
        public const float TunnelClearance = 0.65f;
        public const float TunnelLength = 3f;

        public const float KillHeight = -12f;

        /// <summary>Online match spawn: team 0 faces +Z from the south, team 1 faces -Z from the north.</summary>
        public static Vector3 NetSpawn(int team, int slot)
        {
            float x = (Mathf.Clamp(slot, 0, 5) - 2.5f) * 1.6f;
            return new Vector3(x, 0f, team == 0 ? -6f : 6f);
        }

        public static Vector3 NetFacing(int team) => team == 0 ? Vector3.forward : Vector3.back;

        public static float SlopeBottomX(float angleDeg) =>
            PlatformEdgeX + PlatformHeight / Mathf.Tan(angleDeg * Mathf.Deg2Rad);
    }
}
