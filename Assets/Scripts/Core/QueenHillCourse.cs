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

    // The numbers of the Queen of the Hill map, "sky palace" (DESIGN §3, 8th
    // revision, R50): the tower is a chessboard stood on end. Seven floors, one per
    // rank (a pawn climbs from rank 1 to rank 7), and the summit is rank 8, where a
    // pawn promotes. Floor n stands on rank n and is climbed to rank n+1: out of the
    // team's plaza on rank n, round its west or east wing, back to its plaza on
    // rank n+1, where the team's bell for floor n hangs (QueenHillRace).
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
        public const int Sections = Floors;             // QueenHillRules sections (the 7th revision's shared bells)
        public const float SeaLevel = 0f;
        public const float BridgeTop = 12f;             // the two broken bridges
        public const float FirstRank = 16f;             // the top of the cliff
        public const float FloorHeight = 26f;

        // The pawn (measured in the ragdoll lab, Docs/RagdollLab/README.md and Player/RAGDOLL §8).
        public const float RunSpeed = 5.5f;
        public const float ClimbSpeed = 1.2f;
        public const float ClimbOnOneBar = 8f;          // metres of wall on one bar of stamina, about
        public const float HookFullReach = 18.4f;       // a fully charged throw (M5)
        public const float HipsHeight = 0.6f;           // above the feet, standing

        // The gap between the broken end of a bridge and the cliff (rank 1).
        public const float BridgeGap = 10.5f;

        // A team's shortcut lift up a floor, once its bell has rung, and its vacuum tube.
        public const float LiftSpeed = 4f, LiftPause = 1.5f;
        public const float TubeSpeed = 32f;

        public static readonly QueenHillFloor[] All =
        {
            new QueenHillFloor(1, "폭포 테라스 · 대계단", "서: 대계단(미끄러지는 돌, 무너진 가운데) · 아치 회랑 · 폰의 행진 / 동: 피스톤 경사로 · 무너지는 체스판 · 계단 벽 · 룩 징검다리 · 밀려오는 벽", false),
            new QueenHillFloor(2, "사슬 곤돌라", "서: 곤돌라 큐브 · 흔들리는 추 · 사슬 사다리 · 도개교 / 동: 룩 징검다리 · 승강기 · 곤돌라 · 그네 · 피스톤 경사로", false),
            new QueenHillFloor(3, "떠오르는 석판 · 나이트 · 비숍", "서: 기둥을 도는 석판 · 밀려오는 벽 · 비숍 레일 · 도개교 / 동: 나이트 도약대 두 번 · 뛰어오르는 계단 · 무너지는 체스판", false),
            new QueenHillFloor(4, "궤도 고리 · 룩의 탑", "기울어 도는 고리 두 개(반 바퀴에 10 m) → 탑 벽 · 동서 네 룩의 탑(6.5 m씩 네 번)", true),
            new QueenHillFloor(5, "시계 태엽", "서: 톱니 원판 · 시계판 벽 · 흔들리는 추 · 폰의 행진 / 동: 체스 시계 · 관람차 · 태엽 스프링 · 외나무다리", false),
            new QueenHillFloor(6, "공중 정원", "서: 떠 있는 섬 · 사슬 사다리 · 그네 · 도개교 · 승강기 / 동: 외나무다리 · 담쟁이 벽 · 떠 있는 섬 · 폰의 행진", false),
            new QueenHillFloor(7, "궁전 대발코니 · 빛의 계단", "두 갈래 나선 계단(드럼의 피스톤) · 켜졌다 꺼지는 빛의 계단 25칸", true),
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

        // Straight-line distance from a pawn's hips on the broken end of a bridge to the cliff's rim:
        // inside an easy throw of the hook.
        public static float HookDistanceToRim =>
            (float)Math.Sqrt(BridgeGap * BridgeGap + Math.Pow(FirstRank - (BridgeTop + HipsHeight), 2));

        // Seconds for a team's shortcut lift to carry a floor.
        public static float LiftSeconds => (FloorHeight - 0.3f) / LiftSpeed + Math.Max(0.5f, LiftSpeed / 6f);
    }
}
