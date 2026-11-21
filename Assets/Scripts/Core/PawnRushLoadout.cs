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
        // two steps' code), the bishop's long diagonal snipe (B-중7), the rook's chain from behind (R-중2, the old chain's
        // code), the king's call with his teammates set down in front of him (K-중1, the old call's code). The knight's
        // bent leap and the queen's check are the ones above, changed to the pool's (N-기1, Q-기1).
        public const int SteeredSteps = 81, LongSnipe = 82, ChainBehind = 83, KingSummon = 84;
        // The pool's plan 3, its basic row's new ones: the bishop's flank shot (B-기6), the rook's castling over a teammate
        // (R-기2), the queen's scholar's thrust (Q-기2). The pawn's, the knight's and the king's basics are 81, 43 and 72.
        public const int FlankShot = 85, CastleOver = 86, ScholarThrust = 87;
        // ...and its mid row's new ones: the pawn's chasing away (P-중4), the queen's series of checks (Q-중5). The knight's
        // and the bishop's mids are plan 1-나's fork and pin, made the pool's (N-중1, B-중1); the rook's and the king's are
        // 83 and 84.
        public const int ChaseAway = 88, CheckSeries = 89;
        // The pool's plan 2 (a skill that grows into another), the three not made for the others: the rook's back-rank
        // move (R-기5) and its straight charge (R-중1), the king's run (K-중7).
        public const int BackRank = 90, FileCharge = 91, KingRun = 92;
        // The pool's plan 3, its ultimates (each on a gauge): the pawn's promotion (P-궁1), the knight's smothered mate
        // (N-궁1), the bishop's long diagonal (B-궁1), the rook's chariot (R-궁6), the queen's sacrifice (Q-궁1), the king's
        // march (K-궁1).
        public const int Promote = 93, Smother = 94, LongDiagonal = 95, Chariot = 96, QueenSac = 97, KingMarch = 98;
        // The kits' second version (Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6, "기물별 한 벌 2판"), the ones no plan had made:
        // the queen's glide (Q-초), her eight lines (Q-상), the rook's wall charge (R-상). The others are the skills above,
        // changed to that version's (RagdollPawn.KitTwo.cs). And the bishop's long diagonal snipe as its high skill (B-상: the
        // same shot as 82 on a high skill's cooldown; 82 stays the pool's mid one).
        public const int QueenGlide = 99, EightLines = 100, WallCharge = 101, SnipeHigh = 102;
        // ...and its new ultimate: the rook's 7th rank (R-궁, a wall across the way that the rook pushes on). Its other five
        // are the pool's (93..95, 97, 98) with its warnings and numbers.
        public const int SeventhRank = 103;
        // The ultimates remade as the team's comeback move (Docs/Skills/PAWN_RUSH_SKILL_SYSTEM.md 7.4, the user 11-21: "훨씬훨씬
        // 더 강력하고, 체스 모티브도 살아 있고 아예 팀이 역전할 수 있는"): the pawn's en passant !!, the knight's tour, the
        // bishop's Greek gift, the rook's 7th rank, the queen's sacrifice, the king's general call (RagdollPawn.TeamUlts.cs).
        // The six above (93..98 less the chariot, and 103) are the high tier's candidates now.
        public const int PassantBang = 104, KnightTour = 105, GreekGift = 106, RankWall = 107, QueenDrop = 108, GeneralCall = 109;
        // The sets 1안-a and 2안-a (Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.9, the user 11-21: "추천안들을 1안-a세트 이런 이름으로").
        // Variants - a skill above with a set's own numbers, on the same code (BaseOf): the two steps at the mid tier (a
        // diagonal hit floors 2 s, 8 s), the snipe at the mid tier (20 m, 12 s), the wall charge at the mid tier (a 3 s wall,
        // 14 s), the series of checks on 12 s, the king's call on 16 s; the knight's leap fork (the bent leap kicking the
        // two beside where it lands), the passed pawn (the two steps whose second is a 6 m run through), the king's step
        // taking two teammates with him (RagdollPawn.SetSkills.cs).
        public const int StepsMid = 110, SnipeMid = 111, WallMid = 112, SeriesQuick = 113, SummonQuick = 114, LeapFork = 115, PassedPawn = 116,
                         KingEscort = 117;
        // The knight's second ultimate, picked from the previz (the user 11-21: "나이트의 궁극기 2개만 먼저 만들어줘"): the family
        // fork, Nf7+!! (RagdollPawn.KnightUlts.cs; the knight's tour, 105, is played the new way there too).
        public const int FamilyFork = 118;

        /// <summary>The skill whose code a variant runs on (itself for any other).</summary>
        public static int BaseOf(int id)
        {
            switch (id)
            {
                case StepsMid: return SteeredSteps;
                case PassedPawn: return SteeredSteps;
                case SnipeMid: return LongSnipe;
                case WallMid: return WallCharge;
                case SeriesQuick: return CheckSeries;
                case SummonQuick: return KingSummon;
                case LeapFork: return BentLeap;
                case KingEscort: return KingStep;
                default: return id;
            }
        }

        /// <summary>The long diagonal snipe, the pool's mid one or the kits' second version's high one.</summary>
        public static bool Snipe(int id) => id == LongSnipe || id == SnipeHigh || id == SnipeMid;

        /// <summary>Made as an ultimate: the first six (and the pool's chariot), the high tier's candidates now, or the team's
        /// comeback six.</summary>
        public static bool Ultimate(int id) => HighCandidate(id) || TeamUltimate(id);

        /// <summary>One of the first ultimates (the pool's six, the kits' 7th rank), the high tier's candidates since 11-21.</summary>
        public static bool HighCandidate(int id) => (id >= Promote && id <= KingMarch) || id == SeventhRank;

        /// <summary>One of the team's comeback ultimates (104..109, and the knight's family fork, 118).</summary>
        public static bool TeamUltimate(int id) => (id >= PassantBang && id <= GeneralCall) || id == FamilyFork;

        /// <summary>A swap of places with a teammate that agrees: plan 1-나's castling, or Queen of the Hill's.</summary>
        public static bool Castle(int id) => id == Castling || id == CastleSwap;

        /// <summary>One of the skills as they are: made for one piece, with that piece's effects and sounds. (The pool's
        /// two steps, chain and call run on the old ones' code, as the pawn, the rook and the king.)</summary>
        public static bool Old(int id)
        {
            id = BaseOf(id);
            return (id >= TwoSteps && id <= RingShove) || id == SteeredSteps || id == ChainBehind || id == KingSummon;
        }

        /// <summary>The piece an old skill was made for.</summary>
        public static PieceKind OldPiece(int id)
        {
            switch (BaseOf(id))
            {
                case TurnLeap: return PieceKind.Knight;
                case CrossRope: return PieceKind.Bishop;
                case ChainPull: return PieceKind.Rook;
                case ChainBehind: return PieceKind.Rook;
                case RingShove: return PieceKind.Queen;
                case KingSummon: return PieceKind.King;
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
        public const int Rows = 6, Slots = 4;
        public const int One = 1, Grow = 2, Many = 3;
        /// <summary>The slots' keys (the kits' second version, Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6: "키는 첫째 칸 F, 둘째 칸 E,
        /// 퀸의 셋째 칸 R, 궁극기 Q"): 0 the skill key (F), 1 E, 2 R (only the queen has a third), 3 Q, the ultimate's.
        /// (Three slots, F, E and Q, until 2026-11-20: a text from then has its Q slot read into this one.)</summary>
        public const int UltSlot = 3;

        /// <summary>The pieces that have slots, in the window's order. The king's row is the last (2026-11-20: a team has
        /// one of each piece); while it is empty the king plays his call as he always did (<see cref="KingSlots"/>).</summary>
        public static readonly PieceKind[] RowPieces = { PieceKind.Pawn, PieceKind.Knight, PieceKind.Bishop, PieceKind.Rook, PieceKind.Queen, PieceKind.King };

        // A change's first number: 0..23 a slot (row * Slots + slot, its value the skill), then these.
        public const int OpStage = Rows * Slots;          // 24..29: a row's stage (its value 1..3)
        public const int OpSystem = OpStage + Rows;       // 30: the system (1..3)
        public const int OpPreset = OpSystem + 1;         // 31: one of the loadouts that come with the game (Preset)
        public const int OpHostOnly = OpPreset + 1;       // 32: only the manager changes it (1) or everyone (0)

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

        /// <summary>The slots a piece's row has (all four, whatever the system: Plays says which of them play).</summary>
        public int SlotCount => Slots;

        /// <summary>Whether a slot plays under this system: all four with several skills a piece; with one a piece and with
        /// the growing one, the first (F) and the ultimate's (Q) - since the sets 1안-a and 2안-a (11-21) every plan has its
        /// ultimate slot. The growing system's second slot is what the first grows into, not a slot of its own.</summary>
        public bool Plays(int slot) => slot >= 0 && slot < Slots && (system == Many || slot == 0 || slot == UltSlot);

        /// <summary>How many normal slots play under this system (the ultimate's apart).</summary>
        public int NormalSlots => system == Many ? Slots - 1 : 1;

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

        /// <summary>The skill a piece plays in a slot now (none in a slot the system does not play, none for a piece without a
        /// row). With the growing system a skill in the row's second slot is what the first grows into from stage 2 (the
        /// skill pool's plan 2: "처음 → 나중"; the set 2안-a: the middlegame shape, then the endgame's); with none there the
        /// first one plays at every stage, as before. <paramref name="stage"/>: a piece's own stage (a team past its second
        /// mini-game), 0 for the row's.</summary>
        public int Skill(PieceKind piece, int slot, int stage = 0)
        {
            if (!Plays(slot)) return 0;
            int row = Row(piece);
            if (system == Grow && slot == 0 && (stage > 0 ? stage : Stage(piece)) >= 2)
            {
                int grown = Raw(row, 1);
                if (grown != 0) return grown;
            }
            return Raw(row, slot);
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

        /// <summary>The slot a piece plays a skill in now (or a variant of it, PawnRushSkillIds.BaseOf), or -1.</summary>
        public int SlotOf(PieceKind piece, int id, int stage = 0)
        {
            if (id == 0) return -1;
            for (int k = 0; k < SlotCount; k++)
            {
                int there = Skill(piece, k, stage);
                if (there == id || (there != 0 && PawnRushSkillIds.BaseOf(there) == id)) return k;
            }
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

        /// <summary>As one line of text: the system, the twenty-four slots, the six stages, and whether only the manager
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
            // Older texts: three slots a row (F, E, Q: before the queen's third key, R), and before that five rows (before
            // the king had one; his row stays empty). A three-slot row's Q slot is the ultimate's now, so a skill keeps its
            // key; R stays empty.
            int perRow = slotTexts.Length == Rows * Slots && stageTexts.Length == Rows ? Slots
                : (slotTexts.Length == Rows * 3 && stageTexts.Length == Rows) || (slotTexts.Length == (Rows - 1) * 3 && stageTexts.Length == Rows - 1) ? 3
                : 0;
            if (perRow == 0) return result;
            var read = new PawnRushLoadout { system = system, hostOnly = parts.Length == 4 && parts[3] == "1" };
            for (int i = 0; i < slotTexts.Length; i++)
            {
                if (!int.TryParse(slotTexts[i], out int id) || id < 0 || id > 255) return result;
                int slot = i % perRow;
                if (perRow == 3 && slot == 2) slot = UltSlot;
                read.slots[i / perRow * Slots + slot] = id;
            }
            for (int i = 0; i < stageTexts.Length; i++)
            {
                if (!int.TryParse(stageTexts[i], out int stage)) return result;
                read.stages[i] = stage < 1 ? 1 : stage > 3 ? 3 : stage;
            }
            return read;
        }

        /// <summary>The skill system a loadout that comes with the game is made for (the skill window switches to it with
        /// the sets): 1안-a and the old 1안 one a piece, 2안-a and the old 2안 the growing one, 3안-a and the old 3안 several;
        /// 0 for the others (the system stays as it is).</summary>
        public static int SystemOf(int preset) => preset == 9 || preset == 5 ? One : preset == 10 || preset == 7 ? Grow : preset == 8 || preset == 6 ? Many : 0;

        /// <summary>How many loadouts come with the game (<see cref="Preset"/>).</summary>
        public const int Presets = 11;

        /// <summary>The loadouts that come with the game: 0 the skills as they are (plan 1-가), 1 plan 1-나, 2 nothing
        /// in any slot, 3 the skill idea book's bundle A, 4 the kits' basic row (each piece's opening play:
        /// Docs/Skills/PAWN_RUSH_SKILL_KITS.md), 5 the skill pool's plan 1 (one skill a piece), 6 the pool's plan 3 (basic,
        /// mid, ultimate: its basic row in the first slots, its mid row in the second, the ultimates in Q's), 7 the pool's
        /// plan 2 (the first skill in the first slots, what each grows into in the second: the growing system plays it from
        /// stage 2), 8 the kits' second version (each piece's own mix of two or three, the queen's three, an ultimate each:
        /// the set 3안-a), 9 the set 1안-a (one skill a piece at the mid tier, and the ultimate), 10 the set 2안-a (one that
        /// grows from its middlegame shape to its endgame shape, and the ultimate). The king's row is empty (his call) in all
        /// but 4 to 10.</summary>
        public static PawnRushLoadout Preset(int which)
        {
            var p = new PawnRushLoadout();
            if (which == 2) return p;
            if (which == 9 || which == 10)
            {
                // KITS 6.9: the sets 1안-a (one normal slot) and 2안-a (the first slot's middlegame shape, the second its
                // endgame shape); the same ultimates as 3안-a.
                int[] mid = which == 9
                    ? new[] { PawnRushSkillIds.StepsMid, PawnRushSkillIds.LeapFork, PawnRushSkillIds.SnipeMid, PawnRushSkillIds.WallMid, PawnRushSkillIds.SeriesQuick, PawnRushSkillIds.SummonQuick }
                    : new[] { PawnRushSkillIds.SteeredSteps, PawnRushSkillIds.BentLeap, PawnRushSkillIds.FlankShot, PawnRushSkillIds.FileCharge, PawnRushSkillIds.Check, PawnRushSkillIds.KingStep };
                int[] end = which == 9 ? new int[Rows]
                    : new[] { PawnRushSkillIds.PassedPawn, PawnRushSkillIds.LeapFork, PawnRushSkillIds.LongSnipe, PawnRushSkillIds.WallCharge, PawnRushSkillIds.SeriesQuick, PawnRushSkillIds.KingEscort };
                int[] ult = { PawnRushSkillIds.PassantBang, PawnRushSkillIds.KnightTour, PawnRushSkillIds.GreekGift, PawnRushSkillIds.RankWall, PawnRushSkillIds.QueenDrop, PawnRushSkillIds.GeneralCall };
                for (int row = 0; row < Rows; row++)
                {
                    p.slots[row * Slots] = mid[row];
                    p.slots[row * Slots + 1] = end[row];
                    p.slots[row * Slots + UltSlot] = ult[row];
                }
                return p;
            }
            if (which == 8)
            {
                // KITS 6.0: the pawn, the knight and the king a basic and a mid, the bishop a basic and a high one, the rook a
                // mid and a high one, the queen all three (R her third key); the ultimate on Q.
                int[] f = { PawnRushSkillIds.SteeredSteps, PawnRushSkillIds.BentLeap, PawnRushSkillIds.FlankShot, PawnRushSkillIds.ChainBehind, PawnRushSkillIds.QueenGlide, PawnRushSkillIds.KingStep };
                int[] e = { PawnRushSkillIds.ChaseAway, PawnRushSkillIds.Fork, PawnRushSkillIds.SnipeHigh, PawnRushSkillIds.WallCharge, PawnRushSkillIds.CheckSeries, PawnRushSkillIds.KingSummon };
                // (The team's comeback six since 11-21; the first six are high-tier candidates in the skill tree.)
                int[] q = { PawnRushSkillIds.PassantBang, PawnRushSkillIds.KnightTour, PawnRushSkillIds.GreekGift, PawnRushSkillIds.RankWall, PawnRushSkillIds.QueenDrop, PawnRushSkillIds.GeneralCall };
                for (int row = 0; row < Rows; row++)
                {
                    p.slots[row * Slots] = f[row];
                    p.slots[row * Slots + 1] = e[row];
                    p.slots[row * Slots + UltSlot] = q[row];
                }
                p.slots[Row(PieceKind.Queen) * Slots + 2] = PawnRushSkillIds.EightLines;
                return p;
            }
            if (which == 6 || which == 7)
            {
                int[] first2 = which == 6
                    ? new[] { PawnRushSkillIds.SteeredSteps, PawnRushSkillIds.BentLeap, PawnRushSkillIds.FlankShot, PawnRushSkillIds.CastleOver, PawnRushSkillIds.ScholarThrust, PawnRushSkillIds.KingStep }
                    : new[] { PawnRushSkillIds.SteeredSteps, PawnRushSkillIds.BentLeap, PawnRushSkillIds.Fianchetto, PawnRushSkillIds.BackRank, PawnRushSkillIds.Check, PawnRushSkillIds.KingStep };
                int[] second = which == 6
                    ? new[] { PawnRushSkillIds.ChaseAway, PawnRushSkillIds.Fork, PawnRushSkillIds.Pin, PawnRushSkillIds.ChainBehind, PawnRushSkillIds.CheckSeries, PawnRushSkillIds.KingSummon }
                    : new[] { PawnRushSkillIds.ChaseAway, PawnRushSkillIds.Fork, PawnRushSkillIds.LongSnipe, PawnRushSkillIds.FileCharge, PawnRushSkillIds.CheckSeries, PawnRushSkillIds.KingRun };
                int[] third = which == 6
                    ? new[] { PawnRushSkillIds.Promote, PawnRushSkillIds.Smother, PawnRushSkillIds.LongDiagonal, PawnRushSkillIds.Chariot, PawnRushSkillIds.QueenSac, PawnRushSkillIds.KingMarch }
                    : new int[Rows];
                for (int row = 0; row < Rows; row++)
                {
                    p.slots[row * Slots] = first2[row];
                    p.slots[row * Slots + 1] = second[row];
                    p.slots[row * Slots + UltSlot] = third[row];
                }
                return p;
            }
            int[] first = which == 5
                ? new[] { PawnRushSkillIds.SteeredSteps, PawnRushSkillIds.BentLeap, PawnRushSkillIds.LongSnipe, PawnRushSkillIds.ChainBehind, PawnRushSkillIds.Check, PawnRushSkillIds.KingSummon }
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
