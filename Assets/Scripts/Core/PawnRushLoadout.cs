using System;

namespace ChessFight.Network
{
    // JY-skill-lab (2026-11-19, the user: "스킬 창을 따로 만들고 ... 드래그 앤 드롭으로 스킬들이 ... 자유롭게 넣었다 뺐다 할 수 있는
    // 거야. 막 바꿔보면서 테스트할 수 있게", a developers' test tool): which skill sits in which slot of which piece.
    //
    // A skill is a number. 1..5 are the skills as they are, each made for one piece; 11..15 are skill plan 1-나's
    // (Docs/Skills/PAWN_RUSH_2_SKILL_PLANS.md); 61..65 are bundle A of the skill idea book
    // (Docs/Skills/PAWN_RUSH_2_SKILL_IDEAS.md). Any skill may sit in any piece's slot.
    public static class PawnRushSkillIds
    {
        public const int None = 0;
        public const int TwoSteps = 1, TurnLeap = 2, CrossRope = 3, ChainPull = 4, RingShove = 5;
        public const int EnPassant = 11, Fork = 12, Pin = 13, Castling = 14, Check = 15;
        // Queen of the Hill's six (승규 님, R89), brought over to the slots: the king's close guard, the queen's long slash,
        // the rook's castling swap, the bishop's crossed air barrage, the knight's press leap, the pawn's squeeze through.
        public const int Ward = 44, Slash = 45, CastleSwap = 46, AirVolley = 47, PressLeap = 48, Squeeze = 49;
        // The skill idea book's bundle A (the user 2026-11-20: "A 묶음부터"), none of which knocks anyone down: the pawn's
        // touch-move, the knight's back kick, the opposite-coloured bishop, the rook's push from behind, the queen's two
        // hares (RagdollPawn.BookSkills.cs).
        public const int TouchMove = 61, BackKick = 62, Ghost = 63, BackPush = 64, TwoHares = 65;
        // The kits by piece (Docs/Skills/PAWN_RUSH_SKILL_KITS.md, 2026-11-20), the basic row - what each piece does in a
        // chess opening: the knight's bent leap (the first form of 승규 님's leap, made again for the slots), the bishop's
        // fianchetto, the king's step (RagdollPawn.KitSkills.cs). The pawn's, the rook's and the queen's basics are skills
        // that were there already (the two steps, castling, check).
        public const int BentLeap = 43, Fianchetto = 71, KingStep = 72;
        // The skill pool's plan 1 (Docs/Skills/PAWN_RUSH_SKILL_POOL.md, one skill a piece; the user 2026-11-20: "1안부터"),
        // the skills as the pool writes them: the pawn's two steps with a second step the move keys bend (P-기1, the old
        // two steps' code), the bishop's long diagonal snipe (B-중7). The knight's bent leap and the queen's check are
        // the ones above, changed to the pool's (N-기1, Q-기1).
        public const int SteeredSteps = 81, LongSnipe = 82;

        /// <summary>A swap of places with a teammate that agrees: plan 1-나's castling, or Queen of the Hill's.</summary>
        public static bool Castle(int id) => id == Castling || id == CastleSwap;

        /// <summary>One of the skills as they are: made for one piece, with that piece's effects and sounds. (The pool's
        /// two steps run on the old two steps' code, as the pawn.)</summary>
        public static bool Old(int id) => (id >= TwoSteps && id <= RingShove) || id == SteeredSteps;

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
    // into a text for the settings of this PC, and over a match from the host to everyone).
    //
    // Over a match (the user: "테스트하는 우리 모두 개발자이기 때문에 모두가 열 수도 있게 하되, 방장이 직접 지정하는 것(다른
    // 인원들은 스킬 못 바꾸는 것)도 가능하게"): a change is two numbers (Apply: which thing, what value), so anyone's
    // change can be asked of the host; hostOnly says only the room's manager may change anything (Allows).
    [Serializable]
    public sealed class PawnRushLoadout
    {
        public const int Rows = 6, Slots = 3;
        public const int One = 1, Grow = 2, Many = 3;

        /// <summary>The pieces that have slots, in the window's order. The king's row is the last (2026-11-20: a team has
        /// one of each piece); while it is empty the king plays his call as he always did (<see cref="KingSlots"/>).</summary>
        public static readonly PieceKind[] RowPieces = { PieceKind.Pawn, PieceKind.Knight, PieceKind.Bishop, PieceKind.Rook, PieceKind.Queen, PieceKind.King };

        // A change's first number: 0..17 a slot (row * Slots + slot, its value the skill), then these.
        public const int OpStage = Rows * Slots;          // 18..23: a row's stage (its value 1..3)
        public const int OpSystem = OpStage + Rows;       // 24: the system (1..3)
        public const int OpPreset = OpSystem + 1;         // 25: one of the loadouts that come with the game (Preset)
        public const int OpHostOnly = OpPreset + 1;       // 26: only the manager changes it (1) or everyone (0)

        public int system = One;
        public int[] slots = new int[Rows * Slots];
        public int[] stages = { 1, 1, 1, 1, 1, 1 };
        /// <summary>A room's: only its manager changes the loadout (everyone still sees it).</summary>
        public bool hostOnly;

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
                var fresh = new int[Rows];
                for (int i = 0; i < Rows; i++) fresh[i] = 1;
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

        /// <summary>The king's row holds a skill in a slot he plays now: he plays his slots. Empty: his call, as it was.</summary>
        public bool KingSlots
        {
            get
            {
                for (int k = 0; k < SlotCount; k++)
                    if (Skill(PieceKind.King, k) != 0) return true;
                return false;
            }
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
            hostOnly = other.hostOnly;
            Array.Copy(other.slots, slots, slots.Length);
            Array.Copy(other.stages, stages, stages.Length);
        }

        /// <summary>May this change be made by a player who is the room's manager or is not? Locking is the manager's
        /// alone; a locked loadout is the manager's to change.</summary>
        public bool Allows(int op, bool manager) => manager || (!hostOnly && op != OpHostOnly);

        /// <summary>One change (see the Op numbers). False when it is no change, or not one at all.</summary>
        public bool Apply(int op, int value)
        {
            Mend();
            if (op >= 0 && op < OpStage)
            {
                if (value < 0 || value > 255 || slots[op] == value) return false;
                slots[op] = value;
                return true;
            }
            if (op >= OpStage && op < OpSystem)
            {
                int stage = value < 1 ? 1 : value > 3 ? 3 : value;
                if (stages[op - OpStage] == stage) return false;
                stages[op - OpStage] = stage;
                return true;
            }
            switch (op)
            {
                case OpSystem:
                    if (value < One || value > Many || system == value) return false;
                    system = value;
                    return true;
                case OpPreset:
                {
                    // The slots only: the system, the stages and the lock stay as they are.
                    if (value < 0 || value >= Presets) return false;
                    var preset = Preset(value);
                    bool changed = false;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        changed |= slots[i] != preset.slots[i];
                        slots[i] = preset.slots[i];
                    }
                    return changed;
                }
                case OpHostOnly:
                    if (hostOnly == (value != 0)) return false;
                    hostOnly = value != 0;
                    return true;
                default:
                    return false;
            }
        }

        public bool Same(PawnRushLoadout other) => other != null && Encode() == other.Encode();

        /// <summary>As one line of text: the system, the eighteen slots, the six stages, and whether only the manager
        /// changes it.</summary>
        public string Encode()
        {
            Mend();
            return system + "|" + string.Join(",", slots) + "|" + string.Join(",", stages) + "|" + (hostOnly ? "1" : "0");
        }

        /// <summary>From <see cref="Encode"/>'s text; anything that is not one is the game as it is.</summary>
        public static PawnRushLoadout Decode(string text)
        {
            var result = Preset(0);
            if (string.IsNullOrEmpty(text)) return result;
            var parts = text.Split('|');
            // (Three parts: a text from before the lock was there.)
            if ((parts.Length != 3 && parts.Length != 4) || !int.TryParse(parts[0], out int system) || system < One || system > Many) return result;
            var slotTexts = parts[1].Split(',');
            var stageTexts = parts[2].Split(',');
            // (Five rows: a text from before the king had a row. His row stays empty.)
            bool old = slotTexts.Length == (Rows - 1) * Slots && stageTexts.Length == Rows - 1;
            if (!old && (slotTexts.Length != Rows * Slots || stageTexts.Length != Rows)) return result;
            var read = new PawnRushLoadout { system = system, hostOnly = parts.Length == 4 && parts[3] == "1" };
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

        /// <summary>How many loadouts come with the game (<see cref="Preset"/>).</summary>
        public const int Presets = 6;

        /// <summary>The loadouts that come with the game: 0 the skills as they are (plan 1-가), 1 plan 1-나, 2 nothing
        /// in any slot, 3 the skill idea book's bundle A, 4 the kits' basic row (each piece's opening play:
        /// Docs/Skills/PAWN_RUSH_SKILL_KITS.md), 5 the skill pool's plan 1 (one skill a piece). The king's row is empty
        /// (his call) in all but 4.</summary>
        public static PawnRushLoadout Preset(int which)
        {
            var p = new PawnRushLoadout();
            if (which == 2) return p;
            int[] first = which == 5
                // (The rook's and the king's are the chain and the call as they are until the pool's are made.)
                ? new[] { PawnRushSkillIds.SteeredSteps, PawnRushSkillIds.BentLeap, PawnRushSkillIds.LongSnipe, PawnRushSkillIds.ChainPull, PawnRushSkillIds.Check, 0 }
                : which == 1
                ? new[] { PawnRushSkillIds.EnPassant, PawnRushSkillIds.Fork, PawnRushSkillIds.Pin, PawnRushSkillIds.Castling, PawnRushSkillIds.Check, 0 }
                : which == 3
                ? new[] { PawnRushSkillIds.TouchMove, PawnRushSkillIds.BackKick, PawnRushSkillIds.Ghost, PawnRushSkillIds.BackPush, PawnRushSkillIds.TwoHares, 0 }
                : which == 4
                ? new[] { PawnRushSkillIds.TwoSteps, PawnRushSkillIds.BentLeap, PawnRushSkillIds.Fianchetto, PawnRushSkillIds.Castling, PawnRushSkillIds.Check, PawnRushSkillIds.KingStep }
                : new[] { PawnRushSkillIds.TwoSteps, PawnRushSkillIds.TurnLeap, PawnRushSkillIds.CrossRope, PawnRushSkillIds.ChainPull, PawnRushSkillIds.RingShove, 0 };
            for (int row = 0; row < Rows; row++) p.slots[row * Slots] = first[row];
            return p;
        }
    }
}
