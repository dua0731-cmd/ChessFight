using System;

namespace ChessFight.Network
{
    // The single-use promotion pads by the mini-games (R125). No Unity: the host and the offline playtest
    // run it, the tests run it.
    //
    // Every piece starts as a pawn. Each mission station has one pad beside its game, for its own team; the
    // first pawn of that team to stand on it for HoldSeconds becomes the pad's piece, and the pad is spent for
    // the round. The pieces are drawn every round: plaza 1's pads give one piece, plaza 2's another, and both
    // teams' pads in a plaza give the same one (the same game, the same prize).
    public static class PawnRushPromotion
    {
        // What a pad can give: the pieces with a Pawn Rush skill besides the pawn (no king, skills D1).
        public static readonly PieceKind[] Pieces = { PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight };

        // Standing still on the pad this long promotes (design doc v0.4 §5 "3초 서 있기"; an enemy pushing the
        // pawn off starts it over). Shorter than the doc's 3 s so a pad in the middle of a mini-game fight can
        // still be taken; the number is the team's to tune.
        public const float HoldSeconds = 2f;

        // R130: the pawn that has stood on a pad chooses its piece from cards; nobody choosing in this long gets a
        // random one (a player who walked off, or a lost connection, never keeps the pad held for good).
        public const float ChooseSeconds = 10f;

        public static string SkillName(PieceKind piece)
        {
            switch (piece)
            {
                case PieceKind.Queen: return "팔방 밀치기";
                case PieceKind.Rook: return "직선 돌파";
                case PieceKind.Bishop: return "교차 밧줄";
                case PieceKind.Knight: return "꺾어 도약";
                default: return "첫 두 걸음";
            }
        }

        public static string SkillLine(PieceKind piece)
        {
            switch (piece)
            {
                case PieceKind.Queen: return "둘레의 상대를 모두 밀쳐 낸다. 몸싸움과 방해에 강하다";
                case PieceKind.Rook: return "곧게 돌진해 앞을 뚫는다. 무겁고 잘 안 밀린다";
                case PieceKind.Bishop: return "대각선 밧줄을 깔아 길을 막는다. 상대 견제용";
                case PieceKind.Knight: return "높이 뛰어 꺾어 착지한다. 틈과 턱을 넘기 좋다";
                default: return "두 칸을 빠르게 내딛는다";
            }
        }

        public struct Draw
        {
            public PieceKind First, Second;   // plaza 1's pads, plaza 2's pads
        }

        // Two different pieces. Seeded: the same seed gives the same two on every PC.
        public static Draw Choose(int seed)
        {
            var rng = new Random(seed ^ 0x5eed);
            int a = rng.Next(Pieces.Length);
            int b = rng.Next(Pieces.Length - 1);
            if (b >= a) b++;
            return new Draw { First = Pieces[a], Second = Pieces[b] };
        }

        public static bool Gives(PieceKind piece) => Array.IndexOf(Pieces, piece) >= 0;

        // Only a pawn promotes (once promoted, a piece stays what it is until the next round).
        public static bool CanPromote(PieceKind piece) => piece == PieceKind.Pawn;
    }

    // The end of a Pawn Rush race (R125): a team wins the moment more than half of it stands on the crown
    // platform at the top of the tower at once (MatchResult.MajorityOf: 1 of 1, 2 of 2, 2 of 3, 3 of 4,
    // 3 of 5, 4 of 6). Bots do not run the course yet (R92), so a team is counted by its players only;
    // a team with none never wins.
    public static class PawnRushFinish
    {
        public const int None = -1;
        // The platform: hips within this of the circle's middle (flat) and this high over its floor. Radius is the
        // tower's 6 m crown circle; a platform of another size passes its own (the endgame throne's 8.5 m, R240).
        public const float Radius = 3f, Below = 0.6f, Above = 3.2f;

        public static int Needed(int teamPlayers) => MatchResult.MajorityOf(Math.Max(1, teamPlayers));

        public static bool Reached(int onPlatform, int teamPlayers) => teamPlayers > 0 && onPlatform >= Needed(teamPlayers);

        // 0 white, 1 black, None while nobody has it. Both at once (the same physics step): the one with more on
        // the platform; still level, nobody yet.
        public static int Winner(int whiteOn, int whitePlayers, int blackOn, int blackPlayers)
        {
            bool white = Reached(whiteOn, whitePlayers), black = Reached(blackOn, blackPlayers);
            if (white && black) return whiteOn > blackOn ? 0 : blackOn > whiteOn ? 1 : None;
            return white ? 0 : black ? 1 : None;
        }

        // On the platform: the hips' offset from the circle's middle on its floor.
        public static bool OnPlatform(float dx, float dy, float dz) => OnPlatform(dx, dy, dz, Radius);

        public static bool OnPlatform(float dx, float dy, float dz, float radius) =>
            dx * dx + dz * dz <= radius * radius && dy >= -Below && dy <= Above;
    }
}
