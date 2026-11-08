namespace ChessFight.Network
{
    /// <summary>
    /// The chess annotation a skill cast earns (R128, 승규 님 10-13: chess.com's move review on the skills): stamped over
    /// the caster's head once its result is known. Judged on the result only (there is no engine to say what the best
    /// move was): how many enemies it took, how many it put out, whom it saved, and whether the caster fell for it.
    /// A plain one-enemy hit earns nothing, so the stamps stay rare enough to mean something.
    /// </summary>
    public enum SkillGrade : byte
    {
        None,
        Brilliant,     // !!  several at once, a ring-out, a turn of the match
        Great,         // !   a save, or two at once
        Interesting,   // !?  it paid off, but the caster fell for it
        Mistake,       // ?   it hit nothing
        Blunder,       // ??  the caster fell, or only allies did
        Missed,        // ✕   a moment the skill was ready and not used (the match review only; not judged yet)
    }

    /// <summary>What a skill is for, as far as its grade goes.</summary>
    public struct SkillGradeRule
    {
        /// <summary>Enemies taken at once for !! (a ring-out is always !!).</summary>
        public int Big;
        /// <summary>A cast that took nobody and saved nobody is a ? (attacks; not moves, guards or swaps).</summary>
        public bool MissIsMistake;
        /// <summary>A single enemy is already a ! (the Sword Fight edge skills: one is all they aim at).</summary>
        public bool OneIsGreat;

        public static SkillGradeRule Attack(int big) => new SkillGradeRule { Big = big, MissIsMistake = true };
        public static SkillGradeRule Move(int big) => new SkillGradeRule { Big = big };
    }

    /// <summary>One cast's result so far.</summary>
    public struct SkillTally
    {
        public int Enemies;      // different enemies it hit
        public int RingOuts;     // of them, put off the board (fell well below where it was hit)
        public int Helped;       // allies saved, blows blocked, the caster saving himself
        public int AllyFalls;    // allies that fell because of it (the rook's crumbling edge)
        public bool SelfFell;    // the caster fell during it
        public bool Failed;      // the skill said so itself (a whiff, a cut that missed)
    }

    public static class SkillGrades
    {
        public static SkillGrade Decide(SkillTally t, SkillGradeRule rule)
        {
            int big = rule.Big < 1 ? 3 : rule.Big;
            bool paid = t.RingOuts > 0 || t.Enemies >= big;
            if (t.SelfFell) return paid || t.Enemies > 0 ? SkillGrade.Interesting : SkillGrade.Blunder;
            if (paid) return SkillGrade.Brilliant;
            if (t.AllyFalls > 0) return SkillGrade.Blunder;
            if (t.Helped > 0 || (big >= 3 && t.Enemies >= 2) || (rule.OneIsGreat && t.Enemies >= 1)) return SkillGrade.Great;
            if (t.Enemies == 0 && (t.Failed || rule.MissIsMistake)) return SkillGrade.Mistake;
            return SkillGrade.None;
        }

        /// <summary>!! is for everyone to see; the rest only on the caster's own screen.</summary>
        public static bool ShowsTo(SkillGrade grade, bool caster) =>
            grade != SkillGrade.None && grade != SkillGrade.Missed && (caster || grade == SkillGrade.Brilliant);

        /// <summary>A later result may only raise a stamp already shown to !! (a ring-out found after the cast ended).</summary>
        public static bool Raises(SkillGrade shown, SkillGrade now) =>
            now == SkillGrade.Brilliant && shown != SkillGrade.Brilliant && shown != SkillGrade.Interesting;

        public static string Symbol(SkillGrade grade)
        {
            switch (grade)
            {
                case SkillGrade.Brilliant: return "!!";
                case SkillGrade.Great: return "!";
                case SkillGrade.Interesting: return "!?";
                case SkillGrade.Mistake: return "?";
                case SkillGrade.Blunder: return "??";
                case SkillGrade.Missed: return "x";
                default: return "";
            }
        }

        public static string Name(SkillGrade grade)
        {
            switch (grade)
            {
                case SkillGrade.Brilliant: return "기상천외";
                case SkillGrade.Great: return "훌륭";
                case SkillGrade.Interesting: return "모험수";
                case SkillGrade.Mistake: return "실수";
                case SkillGrade.Blunder: return "블런더";
                case SkillGrade.Missed: return "놓친 수";
                default: return "";
            }
        }
    }
}
