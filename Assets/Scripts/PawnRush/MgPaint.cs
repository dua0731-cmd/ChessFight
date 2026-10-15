using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A, painting the chessboard (mini-games v1.0 §3): one board shared by the plaza's two stations (PaintBoard).
    // White paints the 32 light squares, black the 32 dark ones; a team's gate opens when its 32 are painted,
    // and the other team plays on. This is one station's side of it: its progress (its team's squares / 32), its
    // gate, and its team's half of the board on the wire (§3.6: 8 bytes of painted squares, four 2-byte charges).
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
        public int Total => PaintBoard.PerTeam;
        public int Painted => Done;

        public static MgPaint Build(CourseBuilder b, MissionStation station)
        {
            MissionGames.Floor(b, 0f, 20f);
            var game = b.parent.gameObject.AddComponent<MgPaint>();
            game.station = station;
            game.board = PaintBoard.For(station, b.parent);
            return game;
        }

        void FixedUpdate()
        {
            if (Completed || Remote || Board == null) return;
            int n = board.Count(Team);
            Progress01 = n / (float)PaintBoard.PerTeam;
            if (n >= PaintBoard.PerTeam) Complete();
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
