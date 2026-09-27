using System;

namespace ChessFight.Network
{
    public sealed class QueenHillFloor
    {
        public int Number { get; }          // 1..7: the floor standing on rank n, climbed to rank n+1
        public string Name { get; }
        public string Ways { get; }         // the ways up, for signs and the playtest panel
        public bool Shared { get; }         // both teams on the same structures (the centre and the top)

        public QueenHillFloor(int number, string name, string ways, bool shared)
        {
            Number = number;
            Name = name;
            Ways = ways;
            Shared = shared;
        }
    }

    // The numbers of the Queen of the Hill map, "sky palace" (DESIGN §3, 7th
    // revision, R49): the tower is a chessboard stood on end. Seven floors, one per
    // rank (a pawn climbs from rank 1 to rank 7), and the summit is rank 8, where a
    // pawn promotes. Floor n stands on rank n and is climbed to rank n+1, so the
    // pioneer bell, the light-column lift and the checkpoint of floor n are all on
    // rank n+1 (QueenHillRules section n).
    //
    // The map itself is data: Tools/QueenHill/build_layout.py writes
    // Assets/Resources/QueenHill/QueenHillLayout.json and QueenHillLevel builds it.
    // The heights here and there must agree (QueenHillLevel warns if they do not).
    // Nothing here needs Unity, so a test (Tests/Network/NetworkCoreTests.cs) keeps
    // the heights, the hook throw and the floors in step.
    public static class QueenHillCourse
    {
        public const int Ranks = 8;
        public const int Floors = Ranks - 1;
        public const int Sections = Floors;             // QueenHillRules sections
        public const float SeaLevel = 0f;
        public const float BridgeTop = 12f;             // the two broken bridges
        public const float FirstRank = 16f;             // the top of the cliff
        public const float FloorHeight = 20f;

        // The pawn (measured in the ragdoll lab, Docs/RagdollLab/README.md and Player/RAGDOLL §8).
        public const float RunSpeed = 5.5f;
        public const float ClimbSpeed = 1.2f;
        public const float ClimbOnOneBar = 8f;          // metres of wall on one bar of stamina, about
        public const float HookFullReach = 18.4f;       // a fully charged throw (M5)
        public const float HipsHeight = 0.6f;           // above the feet, standing

        // The gap between the broken end of a bridge and the cliff; the arcade roof on the
        // first floor is the "two squares" landing a full throw can reach.
        public const float BridgeGap = 10.5f;
        public const float TwoSquaresRise = 6f + 1f;    // the arcade roof above rank 1
        public const float TwoSquaresAcross = 8f;       // its corner, sideways from the end of the bridge
        public const float TwoSquaresOut = 10.5f;       // and toward the tower

        // The light column's lift for each floor (DESIGN §3.3 B).
        public const float LiftSpeed = 3.5f, LiftPause = 2f;

        public static readonly QueenHillFloor[] All =
        {
            new QueenHillFloor(1, "폭포 테라스 · 대계단", "대계단(가운데가 무너짐, 미끄러지는 돌) · 아치 회랑(기둥 → 탑 → 외나무) · 갈고리 2칸", false),
            new QueenHillFloor(2, "사슬 곤돌라", "번갈아 오르내리는 체크 큐브 5개 · 사슬 사다리(쉬는 발코니, 흔들리는 추)", false),
            new QueenHillFloor(3, "떠오르는 석판 · 나이트 · 비숍", "기둥을 도는 석판 12개 · 나이트 도약대 3번 · 비숍 대각선 레일 2번", false),
            new QueenHillFloor(4, "궤도 고리 · 룩의 탑", "기울어 도는 고리 두 개(반 바퀴에 9 m) · 네 귀퉁이 룩의 탑", true),
            new QueenHillFloor(5, "시계 태엽", "시계 관람차 · 바늘을 피하며 시계판 오르기 · 태엽 스프링 2번", false),
            new QueenHillFloor(6, "공중 정원", "떠 있는 섬 → 사슬 → 그네 → 오르는 섬 · 담쟁이 버팀벽", false),
            new QueenHillFloor(7, "궁전 대발코니 · 빛의 계단", "두 갈래 나선 계단(쓸어내는 팔) · 켜졌다 꺼지는 빛의 계단", true),
        };

        public static QueenHillFloor Floor(int number) =>
            number >= 1 && number <= Floors ? All[number - 1] : null;

        // Height of the top of rank 1..8 (the floor you stand on).
        public static float RankHeight(int rank) => FirstRank + FloorHeight * (Math.Max(1, Math.Min(Ranks, rank)) - 1);

        public static float TopHeight => RankHeight(Ranks);

        // The rank a height is at or above (1..8): the floor a character has got up to is this minus one.
        public static int RankAt(float height)
        {
            int rank = 1;
            for (int r = 2; r <= Ranks; r++)
                if (height >= RankHeight(r) + 0.2f) rank = r;
            return rank;
        }

        // Straight-line distance from a pawn's hips on the broken end of a bridge to the cliff's
        // rim, and to the corner of the arcade roof (the "two squares"): both inside a full throw.
        public static float HookDistanceToRim =>
            (float)Math.Sqrt(BridgeGap * BridgeGap + Math.Pow(FirstRank - (BridgeTop + HipsHeight), 2));

        public static float HookDistanceToTwoSquares =>
            (float)Math.Sqrt(TwoSquaresAcross * TwoSquaresAcross + TwoSquaresOut * TwoSquaresOut
                             + Math.Pow(FirstRank + TwoSquaresRise - (BridgeTop + HipsHeight), 2));

        // Seconds for the light column's lift to carry a floor.
        public static float LiftSeconds => (FloorHeight - 0.3f) / LiftSpeed + Math.Max(0.5f, LiftSpeed / 6f);
    }
}
