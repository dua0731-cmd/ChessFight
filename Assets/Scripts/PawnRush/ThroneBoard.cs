using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The chessboard of an ending hall the kings call their teams onto (ThroneSummonsShow, JY 2026-11-22: "실제 체스랑
    // 같은 기물 위치로 소환"): a1's middle on the floor, the files' way (a to h) and the ranks' way (white's side to
    // black's), the size of a square, all in the hall's own frame. The halls lay the ranks across the way in, white's two
    // on the left, so the platform is as far from either army. A hall without one (Square 0) stands the teams in rows.
    public readonly struct ThroneBoard
    {
        public readonly Vector3 A1, File, Rank;
        public readonly float Square;

        public ThroneBoard(Vector3 a1, Vector3 file, Vector3 rank, float square)
        {
            A1 = a1;
            File = file.normalized;
            Rank = rank.normalized;
            Square = square;
        }

        public bool Exists => Square > 0f;
        // The middle of a square (file 1-8 = a-h, rank 1-8) on the floor.
        public Vector3 At(int file, int rank) => A1 + File * ((file - 1) * Square) + Rank * ((rank - 1) * Square);
        // A team's square counted from its own side (its rank 1 the back rank): black's 1 is the board's 8.
        public Vector3 Of(int team, int file, int ownRank) => At(file, team == Teams.Black ? 9 - ownRank : ownRank);
        // The hall's king of a team stands on e1 / e8.
        public Vector3 King(int team) => Of(team, 5, 1);
        public Vector3 Middle => A1 + (File + Rank) * (3.5f * Square);
        // A team's pieces look across the board at the other side.
        public Vector3 Facing(int team) => team == Teams.Black ? -Rank : Rank;
        // How tall the hall's kings stand: half again a player's king on a 2 m square, more on bigger squares.
        public float KingHeight => Mathf.Clamp(Square * .75f, 1f, 2.4f);

        // The squares and a gold frame laid on the floor (pictures: the floor under them is what is walked on).
        public void Inlay(CourseBuilder b, float y = 0f)
        {
            var turn = Quaternion.LookRotation(File, Vector3.up);
            float s = Square, t = .02f, half = 4f * s, w = .25f * Mathf.Max(1f, s * .5f);
            for (int f = 1; f <= 8; f++)
                for (int r = 1; r <= 8; r++)
                    HallParts.Panel(b, "Board " + "abcdefgh"[f - 1] + r, At(f, r) + Vector3.up * (y + t * .5f), turn, new Vector3(s, t, s),
                                    (f + r) % 2 == 0 ? b.kit.boardDark : b.kit.boardLight, false);
            Vector3 m = Middle + Vector3.up * (y + t * .5f);
            HallParts.Panel(b, "Board frame a", m - File * (half + w * .5f), turn, new Vector3(2f * (half + w), t, w), b.kit.rankGold, false);
            HallParts.Panel(b, "Board frame h", m + File * (half + w * .5f), turn, new Vector3(2f * (half + w), t, w), b.kit.rankGold, false);
            HallParts.Panel(b, "Board frame 1", m - Rank * (half + w * .5f), turn, new Vector3(w, t, 2f * half), b.kit.rankGold, false);
            HallParts.Panel(b, "Board frame 8", m + Rank * (half + w * .5f), turn, new Vector3(w, t, 2f * half), b.kit.rankGold, false);
        }
    }
}
