using System;

namespace ChessFight.Network
{
    // JY-skill-lab (2026-11-19, the user: "스킬 창을 따로 만들고 ... 드래그 앤 드롭으로 스킬들이 ... 자유롭게 넣었다 뺐다 할 수 있는
    // 거야. 막 바꿔보면서 테스트할 수 있게", a developers' test tool): which skill sits in which slot of which piece.
    //
    // A skill is a number. 1..5 are the skills as they are, each made for one piece; 11..15 are skill plan 1-나's
    // (Docs/Skills/PAWN_RUSH_2_SKILL_PLANS.md). Any skill may sit in any piece's slot.
    public static class PawnRushSkillIds
    {
        public const int None = 0;
        public const int TwoSteps = 1, TurnLeap = 2, CrossRope = 3, ChainPull = 4, RingShove = 5;
        public const int EnPassant = 11, Fork = 12, Pin = 13, Castling = 14, Check = 15;

        /// <summary>One of the skills as they are: made for one piece, with that piece's effects and sounds.</summary>
        public static bool Old(int id) => id >= TwoSteps && id <= RingShove;

        /// <summary>The piece an old skill was made for.</summary>
        public static PieceKind OldPiece(int id)
        {
            switch (id)
            {
                case TurnLeap: return PieceKind.Knight;
                case CrossRope: return PieceKind.Bishop;
                case ChainPull: return PieceKind.Rook;
                case RingShove: return PieceKind.Queen;
                default: return PieceKind.Pawn;
            }
        }
    }

    // The loadout: the skill system the user listed (1 a skill a piece, 2 the same and it grows, 3 several a piece),
    // the skills in each piece's slots, and for system 2 the stage each piece's skill is at. Core: no Unity (it goes
    // into a text for the settings of this PC, and later into the room's data).
    [Serializable]
    public sealed class PawnRushLoadout
    {
        public const int Rows = 5, Slots = 3;
        public const int One = 1, Grow = 2, Many = 3;

        /// <summary>The pieces that have slots, in the window's order.</summary>
        public static readonly PieceKind[] RowPieces = { PieceKind.Pawn, PieceKind.Knight, PieceKind.Bishop, PieceKind.Rook, PieceKind.Queen };

        public int system = One;
        public int[] slots = new int[Rows * Slots];
        public int[] stages = { 1, 1, 1, 1, 1 };

        public static int Row(PieceKind piece)
        {
            for (int i = 0; i < RowPieces.Length; i++)
                if (RowPieces[i] == piece) return i;
            return -1;
        }

        /// <summary>How many slots a piece has under this system: three with several skills a piece, one otherwise.</summary>
        public int SlotCount => system == Many ? Slots : 1;

        void Mend()
        {
            if (slots == null || slots.Length != Rows * Slots)
            {
                var fresh = new int[Rows * Slots];
                if (slots != null) Array.Copy(slots, fresh, Math.Min(slots.Length, fresh.Length));
                slots = fresh;
            }
            if (stages == null || stages.Length != Rows)
            {
                var fresh = new[] { 1, 1, 1, 1, 1 };
                if (stages != null) Array.Copy(stages, fresh, Math.Min(stages.Length, fresh.Length));
                stages = fresh;
            }
            if (system < One || system > Many) system = One;
        }

        /// <summary>The skill in a row's slot as the window shows it (every slot, whatever the system).</summary>
        public int Raw(int row, int slot)
        {
            Mend();
            return row < 0 || row >= Rows || slot < 0 || slot >= Slots ? 0 : slots[row * Slots + slot];
        }

        public void Set(int row, int slot, int id)
        {
            Mend();
            if (row < 0 || row >= Rows || slot < 0 || slot >= Slots) return;
            slots[row * Slots + slot] = id < 0 ? 0 : id;
        }

        /// <summary>The skill a piece plays in a slot now (none past the system's slots, none for a piece without a row).</summary>
        public int Skill(PieceKind piece, int slot)
        {
            return slot < 0 || slot >= SlotCount ? 0 : Raw(Row(piece), slot);
        }

        /// <summary>The slot a piece plays a skill in now, or -1.</summary>
        public int SlotOf(PieceKind piece, int id)
        {
            if (id == 0) return -1;
            for (int k = 0; k < SlotCount; k++)
                if (Skill(piece, k) == id) return k;
            return -1;
        }

        /// <summary>The stage a piece's skill is at: 1..3 with the growing system, 1 otherwise.</summary>
        public int Stage(PieceKind piece)
        {
            Mend();
            int row = Row(piece);
            if (system != Grow || row < 0) return 1;
            return stages[row] < 1 ? 1 : stages[row] > 3 ? 3 : stages[row];
        }

        public void SetStage(int row, int stage)
        {
            Mend();
            if (row >= 0 && row < Rows) stages[row] = stage < 1 ? 1 : stage > 3 ? 3 : stage;
        }

        public void CopyFrom(PawnRushLoadout other)
        {
            if (other == null) return;
            other.Mend();
            Mend();
            system = other.system;
            Array.Copy(other.slots, slots, slots.Length);
            Array.Copy(other.stages, stages, stages.Length);
        }

        public bool Same(PawnRushLoadout other) => other != null && Encode() == other.Encode();

        /// <summary>As one line of text: the system, the fifteen slots, the five stages.</summary>
        public string Encode()
        {
            Mend();
            return system + "|" + string.Join(",", slots) + "|" + string.Join(",", stages);
        }

        /// <summary>From <see cref="Encode"/>'s text; anything that is not one is the game as it is.</summary>
        public static PawnRushLoadout Decode(string text)
        {
            var result = Preset(0);
            if (string.IsNullOrEmpty(text)) return result;
            var parts = text.Split('|');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int system) || system < One || system > Many) return result;
            var slotTexts = parts[1].Split(',');
            var stageTexts = parts[2].Split(',');
            if (slotTexts.Length != Rows * Slots || stageTexts.Length != Rows) return result;
            var read = new PawnRushLoadout { system = system };
            for (int i = 0; i < slotTexts.Length; i++)
            {
                if (!int.TryParse(slotTexts[i], out int id) || id < 0 || id > 255) return result;
                read.slots[i] = id;
            }
            for (int i = 0; i < stageTexts.Length; i++)
            {
                if (!int.TryParse(stageTexts[i], out int stage)) return result;
                read.stages[i] = stage < 1 ? 1 : stage > 3 ? 3 : stage;
            }
            return read;
        }

        /// <summary>The loadouts that come with the game: 0 the skills as they are (plan 1-가), 1 plan 1-나, 2 nothing
        /// in any slot.</summary>
        public static PawnRushLoadout Preset(int which)
        {
            var p = new PawnRushLoadout();
            if (which == 2) return p;
            int[] first = which == 1
                ? new[] { PawnRushSkillIds.EnPassant, PawnRushSkillIds.Fork, PawnRushSkillIds.Pin, PawnRushSkillIds.Castling, PawnRushSkillIds.Check }
                : new[] { PawnRushSkillIds.TwoSteps, PawnRushSkillIds.TurnLeap, PawnRushSkillIds.CrossRope, PawnRushSkillIds.ChainPull, PawnRushSkillIds.RingShove };
            for (int row = 0; row < Rows; row++) p.slots[row * Slots] = first[row];
            return p;
        }
    }
}
