using UnityEngine;

namespace ChessFight.ProtectKing
{
    public enum TeamId { Blue, Red }
    public enum PieceType { King, Queen, Rook, Bishop, Knight, Pawn }

    public sealed class PlayerIdentity : MonoBehaviour
    {
        public int playerId;
        public TeamId team;
        public PieceType piece;
        [System.NonSerialized] public int checkpoint;
        [System.NonSerialized] public int falls;
        [System.NonSerialized] public float[] checkpointTimes = new float[6];
        public string DisplayName => team + " " + piece;
    }
}
