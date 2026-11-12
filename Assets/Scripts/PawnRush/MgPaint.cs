using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A, painting the chessboard (mini-games v1.0 §3): one board shared by the plaza's two stations (PaintBoard).
    // White paints the light squares, black the dark ones (32 each on the 8 x 8 board; R543: 18, 24, 32 or 40 by the
    // team's size); a team's gate opens when its squares are painted, and the other team plays on. This is one
    // station's side of it: its progress (its team's squares of its half), its gate, and its team's half of the board
    // on the wire (PaintBoard.Write: a bit a square, a 2-byte charge for each square that may charge at once).
    public sealed class MgPaint : MiniGameBase, IProgressCount
    {
        [SerializeField] MissionStation station;
        [SerializeField] PaintBoard board;

        int Team => station != null ? station.Team : Teams.White;

        // The plaza's board; found again if the station that built it has rebuilt its game.
        PaintBoard Board
        {
            get
            {
                if (board == null && station != null && Application.isPlaying) board = PaintBoard.For(station, transform);
                return board;
            }
        }

        public int Done => Board != null ? board.Count(Team) : 0;
        public int Total => Board != null ? board.PerTeam : PawnRushBoard.N * PawnRushBoard.N / 2;
        public int Painted => Done;

        public static MgPaint Build(CourseBuilder b, MissionStation station)
        {
            MissionGames.Floor(b, 0f, 20f);
            var game = b.parent.gameObject.AddComponent<MgPaint>();
            game.station = station;
            game.board = PaintBoard.For(station, b.parent);
            if (game.board != null) game.board.ClipRims(b.parent);
            return game;
        }

        void FixedUpdate()
        {
            if (Completed || Remote || Board == null) return;
            int n = board.Count(Team);
            Progress01 = n / (float)board.PerTeam;
            if (n >= board.PerTeam) Complete();
        }

        public override void WriteNet(ref NetBlock block)
        {
            if (Board != null) board.Write(Team, ref block);
        }

        public override void ReadNet(ref NetBlock block)
        {
            if (Board != null) board.Read(Team, ref block);
        }

        public override void ResetGame()
        {
            base.ResetGame();
            if (Board != null) board.ResetTeam(Team);
        }
    }
}
