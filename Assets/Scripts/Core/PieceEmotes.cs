namespace ChessFight.Network
{
    // The piece emotes (skill network test branch): four per piece, picked with the number keys. No Unity in
    // here, so the table is tested on its own. Slots 1 and 2 are made first (taunt, joy); 3 and 4 (dismay, anger)
    // come later and are locked until then. The pawn has none yet: its model is not finished.
    public static class PieceEmotes
    {
        public const int Slots = 4;

        // How many slots can be played so far. Raise it to 4 when the dismay and anger poses exist.
        public const int Unlocked = 2;

        // 1 taunt / confidence, 2 joy / victory, 3 dismay, 4 anger.
        static readonly float[] Seconds = { 1.8f, 2.2f, 1.8f, 1.8f };

        static readonly string[][] Names =
        {
            // Pawn
            new[] { "주먹 불끈", "깡충 점프", "방패 뒤에 숨기", "땅 치며 울상" },
            // Rook
            new[] { "팔짱 끼고 우뚝", "껄껄 웃음", "성벽에 금이 감", "성벽 쾅" },
            // Bishop
            new[] { "음흉한 미소", "평온한 기도", "지팡이를 떨어뜨림", "한숨" },
            // Knight
            new[] { "창 겨누기", "승리 포즈", "말이 뒷걸음질", "콧김 씩씩" },
            // King
            new[] { "호령", "호탕한 웃음", "왕관이 삐딱해짐", "왕관을 벗어 쥠" },
            // Queen
            new[] { "턱 괴고 윙크", "우아한 박수", "부채로 얼굴 가림", "홀을 겨눔" },
        };

        public static bool IsValidSlot(int slot) => slot >= 1 && slot <= Slots;

        // Slot 0 means no emote. A piece with no pose yet (the pawn, and any slot past Unlocked) cannot play.
        public static bool Available(PieceKind kind, int slot) =>
            kind != PieceKind.Pawn && ChessPieces.IsValid((int)kind) && slot >= 1 && slot <= Unlocked;

        public static float Duration(int slot) => IsValidSlot(slot) ? Seconds[slot - 1] : 0f;

        public static string Name(PieceKind kind, int slot) =>
            ChessPieces.IsValid((int)kind) && IsValidSlot(slot) ? Names[(int)kind][slot - 1] : "";
    }
}
