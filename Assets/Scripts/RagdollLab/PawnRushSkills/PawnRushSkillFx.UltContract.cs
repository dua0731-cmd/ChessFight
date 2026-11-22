using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// What the parts of the ultimates' "!!" presentation share (JY-skill-lab, 2026-11-22), so each can be built and
    /// compiled by itself: the stage lights (PawnRushSkillFx.Stage.cs), the chess board on the floor
    /// (PawnRushSkillFx.Brilliant.cs), the HUD (PawnRushSkillFx.Hud.cs) and the sounds (PawnRushSkillFx.Sounds.cs).
    ///
    ///   the board    one board for every ultimate on every PC: the world's axes, FamSquare (2.2 m) cells; each show's
    ///                own chess frame for the squares' names (the from-square carries the ultimate's name, the board runs
    ///                up the way the caster faced at the key, the black team's ranks mirrored)
    ///   the calls    each ultimate's notation, name and from-square
    ///   threats      which enemies an ultimate aims at now (any enemy, not only the piece played here)
    ///   the sounds   partial methods: a file without them compiles, and the sounds file implements them
    /// </summary>
    public partial class PawnRushSkillFx
    {
        // ---------------------------------------------------------------- the board

        /// <summary>A board cell's width (the knight's square, 2.2 m).</summary>
        float CellSize => FamSquare;

        /// <summary>The board cell a point is on.</summary>
        Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / CellSize), Mathf.RoundToInt(p.z / CellSize));

        /// <summary>A cell's middle at height 0 (its floor height is the board code's, from Ground()).</summary>
        Vector3 CellFlat(Vector2Int c) => new Vector3(c.x * CellSize, 0f, c.y * CellSize);

        /// <summary>A dark square ((x + z) odd).</summary>
        static bool IsDark(Vector2Int c) => ((c.x + c.y) & 1) != 0;

        /// <summary>The world axis nearest a flat direction, as a cell step ((0, 1) is +z).</summary>
        static Vector2Int AxisOf(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return new Vector2Int(0, 1);
            return Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? new Vector2Int(dir.x > 0f ? 1 : -1, 0) : new Vector2Int(0, dir.z > 0f ? 1 : -1);
        }

        static int Mod8(int v) => ((v % 8) + 8) % 8;

        /// <summary>A cell's name in a show's chess frame: the from-square is the ultimate's own (a1, e2 ..), ranks go up the way
        /// the caster faced at the key and files to its right (for the black team the other way round: its ranks mirrored),
        /// wrapping round a-h and 1-8.</summary>
        string UltCoord(UltShow u, Vector2Int cell)
        {
            Vector2Int d = cell - u.fromCell, right = new Vector2Int(u.up.y, -u.up.x);
            int along = d.x * u.up.x + d.y * u.up.y, side = d.x * right.x + d.y * right.y;
            string from = UltFromSquare(u.skill, u.black);
            int file = from[0] - 'a', rank = from[1] - '1';
            if (u.black)
            {
                file -= side;
                rank -= along;
            }
            else
            {
                file += side;
                rank += along;
            }
            return $"{(char)('a' + Mod8(file))}{(char)('1' + Mod8(rank))}";
        }

        /// <summary>A notation as the black team writes it: every rank r becomes 9 − r.</summary>
        static string Mirror(string nota)
        {
            var chars = nota.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] >= '1' && chars[i] <= '8') chars[i] = (char)('1' + ('8' - chars[i]));
            return new string(chars);
        }

        // ---------------------------------------------------------------- the calls (no king in Pawn Rush: its row is kept, never shown)

        /// <summary>The move an ultimate is, typed at its key.</summary>
        static string UltCallNota(int skill, bool black)
        {
            string white = skill switch
            {
                PawnRushSkillIds.Nightmare => "KNIGHTMARE",
                PawnRushSkillIds.GreatDiagonal => "a1–h8",
                PawnRushSkillIds.PromotionRun => "e8=Q",
                PawnRushSkillIds.RookRoll => "Ra1–a7",
                PawnRushSkillIds.QueenDescent => "Q×h7+",
                PawnRushSkillIds.KingWalk => "Kg3–f4–g5–h6",
                PawnRushSkillIds.FamilyFork => "Nf7+",
                PawnRushSkillIds.KnightTour => "Nf5–d6–e8",
                _ => "!!",
            };
            return black && skill != PawnRushSkillIds.Nightmare ? Mirror(white) : white;
        }

        /// <summary>An ultimate's name under its call.</summary>
        static string UltCallName(int skill) => skill switch
        {
            PawnRushSkillIds.FamilyFork => "패밀리 포크",
            PawnRushSkillIds.KnightTour => "나이트 투어",
            _ => DriveTitle(skill),
        };

        /// <summary>The square an ultimate is played from (the black team's mirrored).</summary>
        static string UltFromSquare(int skill, bool black)
        {
            string white = skill switch
            {
                PawnRushSkillIds.Nightmare => "g1",
                PawnRushSkillIds.GreatDiagonal => "a1",
                PawnRushSkillIds.PromotionRun => "e2",
                PawnRushSkillIds.RookRoll => "a1",
                PawnRushSkillIds.QueenDescent => "d1",
                PawnRushSkillIds.KingWalk => "e1",
                PawnRushSkillIds.FamilyFork => "g1",
                PawnRushSkillIds.KnightTour => "g1",
                _ => "e1",
            };
            return black ? Mirror(white) : white;
        }

        /// <summary>A show's chess frame, set at its key: the from-square, the way up the board, the side.</summary>
        void UltFrameSet(UltShow u)
        {
            u.fromCell = Cell(u.castAt);
            u.up = AxisOf(u.castDir.sqrMagnitude > 1e-4f ? u.castDir : u.by != null ? u.by.Facing : Vector3.forward);
            u.black = u.by != null && u.by.Team == Teams.Black;
            u.toCell = u.fromCell;
        }

        // ---------------------------------------------------------------- threats

        /// <summary>Whether a driven ultimate aims at <paramref name="e"/> now (its warnings, for any enemy).</summary>
        bool DriveThreat(UltShow u, RagdollPawn e) =>
            u != null && e != null && u.Driven && u.Playing && u.by != null && e != u.by && Teams.AreEnemies(e.Team, u.by.Team) && DriveWarning(u, e) != null;

        // ---------------------------------------------------------------- the sounds (PawnRushSkillFx.Sounds.cs)

        /// <summary>The presentation's new clips (made in code; the spec's sound table).</summary>
        public enum UltClipId { Tock, Capture, Check, Castle, Promote, ClockPress, SubDrop, Swell, Heart, Tap, Whoosh, Crack, Riser, Tick, Wah, Shimmer, Wind, March, Hum }

        /// <summary>One clip played now or after <paramref name="delay"/> s, through UltPlay (the films hear it), within the
        /// caps (five ultimate sounds at once, their levels summing to 1.8 at most).</summary>
        partial void UltClipPlay(UltClipId id, float level, float pitch, float delay);

        /// <summary>A looping bed this frame at this level and pitch; a bed not asked for again within 0.1 s fades out.</summary>
        partial void UltBedPlay(UltClipId id, float level, float pitch);

        /// <summary>The sounds' clips and sources gone (this object going).</summary>
        partial void UltSoundsDestroy();

        void UltClip(UltClipId id, float level, float pitch = 1f, float delay = 0f) => UltClipPlay(id, level, pitch, delay);

        void UltBed(UltClipId id, float level, float pitch = 1f) => UltBedPlay(id, level, pitch);

        /// <summary>A sound's level for this listener by its distance from <paramref name="at"/>: full to 30 m, falling to
        /// 0.45 at 120 m.</summary>
        float UltHeard(Vector3 at)
        {
            var cam = ViewCamera;
            if (cam == null) return 1f;
            float d = Vector3.Distance(cam.transform.position, at);
            return Mathf.Lerp(1f, 0.45f, Mathf.Clamp01((d - 30f) / 90f));
        }
    }
}
