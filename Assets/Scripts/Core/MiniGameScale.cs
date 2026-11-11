using System;

namespace ChessFight.Network
{
    // R543 (user 11-11: "현재 3대3이든 6대6이든 모두 동일한 레벨의 미니게임이 등장하는걸 몇 대 몇이냐에 따라 미니게임 규모를 정할거야.
    // 현재 적용되어있는 미니게임은 5대5 정도가 적당한 것 같아서 이걸 기준으로 3대3, 4대4, 6대6의 미니게임 규모를 만들어줘"): how big each of
    // the ten mini-games is for the size the teams started the match with. Five a side is what the games were before
    // (the baseline, and what a station outside a section round plays: course 01); three, four and six a side are scaled
    // from it so that one player's share of the work stays about the same. One table, no Unity: every PC reads the same
    // numbers from the same team size, the host judges with them, and the Core tests check them.
    //
    //                          3 a side     4 a side     5 a side     6 a side      the user's knob
    //   A paint                6 x 6        8 x 6        8 x 8        10 x 8        the board's size (squares a team 18 / 24 / 32 / 40)
    //   B planks               6            8            10           12            planks to carry
    //   C drawbridge           x 1.67       x 1.25       x 1          x 0.83        what one puller's pull is worth
    //   D capstan              3 bars, 3.5  4 bars, 5    5 bars, 6    6 bars, 7     a bar a player, turns to open
    //   E bell towers          3            4            5            6             towers, a 9 m one a player
    //   F en passant           10           13           16           19            statues to take
    //   G king escort          11 squares   13           15           17            the road, its sand and rooks, the sled's weight
    //   H checkmate hunt       4            5            6            7             kings
    //   I check block          lighter      light        as it was    heavier       how many checks a position holds
    //   J knight's road        6 x 1.9 m    7 x 2.0 m    8 x 2.05 m   9 x 2.2 m     squares, and how wide
    public static class MiniGameScale
    {
        public const int Min = 3, Max = 6, Baseline = 5;

        // The size a game is built for: a match of one or two a side (a test) plays the smallest.
        public static int Clamp(int teamSize) => Math.Max(Min, Math.Min(Max, teamSize));

        static T Pick<T>(int teamSize, T three, T four, T five, T six)
        {
            switch (Clamp(teamSize))
            {
                case 3: return three;
                case 4: return four;
                case 6: return six;
                default: return five;
            }
        }

        // How many of a team count toward a game at once where a game caps it (A's squares charging, G's pushers):
        // four as before (PawnRushMissions.WorkerCap), five of six.
        public static int Workers(int teamSize) => Clamp(teamSize) >= 6 ? 5 : PawnRushMissions.WorkerCap;

        // A: the shared board, files (white's side to black's) by ranks, 2.25 m squares, centred where the 8 x 8 was.
        public static BoardGrid PaintBoard(int teamSize) =>
            Pick(teamSize, new BoardGrid(6, 6), new BoardGrid(8, 6), new BoardGrid(8, 8), new BoardGrid(10, 8));

        // B: the plank bridge (PlankLayout).
        public static PlankLayout Planks(int teamSize) => PlankLayout.For(teamSize);

        // C: what one puller (and one holder) is worth, in baseline pullers. The rope's 7 m and its top speed stay.
        public static float RopeForce(int teamSize) => (float)Baseline / Clamp(teamSize);

        // D: a bar a player; the turns that open the gate. The bars turn at 70 % of what they did (user: "지금 회전 속도가
        // 조금 빠른데 지금의 70%정도로 줄여줘"): 0.091 turns a second for each pusher, up to four pushers' worth (0.364; it
        // was 0.13 and 0.52), so the baseline's eight turns became six to take the time they took.
        public const float CapstanTurnRate = .13f * .7f;
        public const int CapstanSpeedPushers = 4;
        public static int CapstanBars(int teamSize) => Clamp(teamSize);
        public static float CapstanTurns(int teamSize) => Pick(teamSize, 3.5f, 5f, 6f, 7f);

        // E: the bell towers (BellTowers.Layout): one a player, every one 9 m. (Before: six of 6, 7.5 and 9 m, 45 m to
        // climb in all - what five 9 m towers are.)
        public static int BellTowerCount(int teamSize) => Clamp(teamSize);
        public static float BellTowerHeight(int teamSize) => 9f;

        // F: statues to take.
        public static int EnPassantStatues(int teamSize) => Pick(teamSize, 10, 13, 16, 19);

        // G: the king's road - its squares, how many of them are sand, the rooks standing in it - and the sled's weight:
        // this many pushing move it at its full speed.
        public static int KingRoadCells(int teamSize) => Pick(teamSize, 11, 13, 15, 17);
        public static int KingRoadSand(int teamSize) => Pick(teamSize, 2, 3, 4, 5);
        public static int KingRoadRooks(int teamSize) => Pick(teamSize, 1, 2, 2, 3);
        public static int KingSledCrew(int teamSize) => Pick(teamSize, 3, 4, 4, 5);

        // H: kings to hunt.
        public static int CheckmateKings(int teamSize) => Pick(teamSize, 4, 5, 6, 7);

        // J: the road's squares (two of them without a piece: the first and the last) and how wide one is.
        public static int KnightSquares(int teamSize) => Pick(teamSize, 6, 7, 8, 9);
        public static float KnightSquareSize(int teamSize) => Pick(teamSize, 1.9f, 2f, 2.05f, 2.2f);
    }

    // A board of `Files` x `Ranks` squares in a station's frame (v across, u north), a1 dark as on a real board. A's
    // shared board is one of these (MiniGameScale.PaintBoard); the 8 x 8 one is PawnRushBoard's.
    public sealed class BoardGrid
    {
        public const float DefaultCell = PawnRushBoard.Cell;
        // The 8 x 8 board's middle: v 0, u 11 (u 2..20).
        public const float CentreV = 0f, CentreU = PawnRushBoard.U0 + PawnRushBoard.N * PawnRushBoard.Cell * .5f;

        public readonly int Files, Ranks;
        public readonly float Cell, V0, U0;

        public BoardGrid(int files, int ranks, float cell = DefaultCell)
        {
            if (files < 1 || ranks < 1 || files > 26 || ranks > 9) throw new ArgumentOutOfRangeException(nameof(files));
            Files = files;
            Ranks = ranks;
            Cell = cell;
            V0 = CentreV - files * cell * .5f;
            U0 = CentreU - ranks * cell * .5f;
        }

        public int Count => Files * Ranks;
        public float V1 => V0 + Files * Cell;
        public float U1 => U0 + Ranks * Cell;
        public float HalfWidth => Files * Cell * .5f;

        public StationPoint Center(int file, int rank) => new StationPoint(V0 + Cell * (file + .5f), U0 + Cell * (rank - .5f));
        public bool IsLight(int file, int rank) => PawnRushBoard.IsLight(file, rank);
        // i = file x ranks + rank - 1.
        public int Index(int file, int rank) => file * Ranks + rank - 1;
        public int FileOf(int index) => index / Ranks;
        public int RankOf(int index) => index % Ranks + 1;
        public bool OnBoard(int file, int rank) => file >= 0 && file < Files && rank >= 1 && rank <= Ranks;

        public bool SquareAt(StationPoint p, out int file, out int rank)
        {
            file = (int)Math.Floor((p.V - V0) / Cell);
            rank = (int)Math.Floor((p.U - U0) / Cell) + 1;
            return OnBoard(file, rank);
        }

        public string Name(int file, int rank) => PawnRushBoard.Name(file, rank);

        // Squares of one colour (light: white's to paint).
        public int CountOf(bool light)
        {
            int n = 0;
            for (int i = 0; i < Count; i++) if (IsLight(FileOf(i), RankOf(i)) == light) n++;
            return n;
        }
    }

    // E, the bell towers (R543, user 11-11: "종탑은 종탑의 개수를 조절해줘. 그리고 지금 종탑은 종탑 꼭대기에서 다른 종탑으로 다이빙을
    // 통해 바로 넘어가지 못하게 해야 해. 적절히 띄어서 배치해주고 높이도 신경써줘"). Before, six towers stood 3.6 m apart north
    // to south and stepped up 1.5 m a row (6, 7.5 and 9 m): from a top a pawn dived onto the next lower one.
    //
    // How far a top reaches: the best a pawn does is a sprint (3.6 m/s), a jump (4.9 m/s up under 1.4 g) and a dive at
    // once (3 m/s more, 6.6 m/s; in the air the dive lifts only 0.55 m/s), landing on its belly 0.3 m lower than it
    // stood: about 5.4 m to a top of the same height, and 1.2 m more for a top 1.5 m lower (DiveReach). So:
    //   - every tower of a station is the same height (9 m): a lower neighbour would only be easier to reach;
    //   - any two tops are MinGap (6 m) or more apart, edge to edge.
    // The room sets the rest (measured from the library's models, E_HallEnv and E_LightsEnv): the gallery along the
    // side wall reaches 1.3 m into the station 4 to 7 m up, so the towers keep 1.4 m off that side; the tops are 2 m
    // square instead of 2.4 m, which lets three columns stand 6 m apart in what is left; and a tower in the middle
    // column stands clear of the two chandeliers that hang over it (1.5 m round, down to 10.3 m, at u 3.5 and 13.5).
    // Six such towers are all a station holds: one for each player of the biggest team.
    public static class BellTowers
    {
        public const float Top = 2f, Ledge = .8f, Storey = 3f, MinGap = 6f;
        // The station's side wall is at v -10 in white's frame (the gallery reaches v -8.68), the aisle at v +10.
        public const float WallSide = -8.6f, AisleSide = 9.6f;

        public struct Tower
        {
            public float V, U, Height;
            public Tower(float v, float u, float height) { V = v; U = u; Height = height; }
        }

        // The towers in a station's frame as seen from white's station (v: the side wall at -10, the aisle at +10;
        // black's station is the mirror), south row first, wall side first.
        public static Tower[] Layout(int teamSize)
        {
            float h = MiniGameScale.BellTowerHeight(teamSize);
            switch (MiniGameScale.BellTowerCount(teamSize))
            {
                case 3:
                    return new[] { new Tower(.4f, 6.5f, h), new Tower(-5.6f, 15.6f, h), new Tower(6.4f, 15.6f, h) };
                case 4:
                    return new[] { new Tower(-5.6f, 6.5f, h), new Tower(6.4f, 6.5f, h), new Tower(-5.6f, 15.6f, h), new Tower(6.4f, 15.6f, h) };
                case 5:
                    return new[]
                    {
                        new Tower(-7.6f, 6.5f, h), new Tower(8.4f, 6.5f, h), new Tower(.4f, 10.5f, h),
                        new Tower(-7.6f, 15.6f, h), new Tower(8.4f, 15.6f, h),
                    };
                default:
                    return new[]
                    {
                        new Tower(-7.6f, 6.5f, h), new Tower(.4f, 6.5f, h), new Tower(8.4f, 6.5f, h),
                        new Tower(-7.6f, 15.6f, h), new Tower(.4f, 16.4f, h), new Tower(8.4f, 15.6f, h),
                    };
            }
        }

        // The library's chandeliers over a station's middle line: (v, u), ChandelierRadius round, down to ChandelierBottom.
        public static readonly float[] ChandelierU = { 3.5f, 13.5f };
        public const float ChandelierRadius = 1.52f, ChandelierBottom = 10.3f;

        // Storeys of 3 m (the last one shorter): how many a tower of this height has.
        public static int Storeys(float height) => (int)Math.Ceiling(height / Storey - .01f);

        // Between two tops, edge to edge (two squares of Top, side by side or corner to corner).
        public static float Gap(Tower a, Tower b)
        {
            float dv = Math.Max(0f, Math.Abs(a.V - b.V) - Top), du = Math.Max(0f, Math.Abs(a.U - b.U) - Top);
            return (float)Math.Sqrt(dv * dv + du * du);
        }

        // The Pawn Rush movement this is worked out from (RagdollTuning.asset, PawnRushPhysicsParams): if they change,
        // the towers' spacing has to be looked at again.
        public const float Sprint = 3.6f, DiveBoost = 3f, JumpSpeed = 4.9f, AirDiveLift = .55f;
        public const float RiseGravity = 9.81f * 1.4f, FallGravity = RiseGravity * 1.15f, BellyDrop = .3f;

        // How far across a sprinting pawn's jump-and-dive carries it before it is `drop` metres under where it stood
        // (plus the 0.3 m it lands lower on its belly).
        public static float DiveReach(float drop)
        {
            float up = JumpSpeed + AirDiveLift;
            float rise = up / RiseGravity, apex = up * up / (2f * RiseGravity);
            float fall = (float)Math.Sqrt(2f * Math.Max(0f, apex + BellyDrop + drop) / FallGravity);
            return (Sprint + DiveBoost) * (rise + fall);
        }
    }
}
