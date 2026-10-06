using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The two team sections: an obstacle lane, then the mini-game island that is the next rank.
    // Built for white (x < 0) and mirrored for black (TeamMirror); phases are the same for both.
    public static partial class Course01Modules
    {
        // M6 team section 1 (70 m, 0 -> +3): the wind bridge, then slot 1.
        static void M6(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            var white = b.In(TeamMirror.WhiteName, () =>
            {
                TeamEntry(b);
                Divider(b, 57f, 70f, 3f);
                b.Floor("Lane front floor", -12f, -.25f, 5f, 9f);          // outer side (x -12): cliff
                b.Floor("Wind bridge", -8f, -5f, 9f, 27f);                 // 3 m, cliffs both sides
                // 15 air vents blowing -x across the bridge; the wind box's bottom is 0.5 under the deck.
                // z 16..20 between them is out of the wind: a place to wait for the beat.
                Hazard(b, k.airVent, "15 Air vent 1", V(-3.8f, -.5f, 14f), -90f, 0f);
                Hazard(b, k.airVent, "15 Air vent 2", V(-3.8f, -.5f, 22f), -90f, 3.4f);   // half of 6.8 s
                b.Wall("Vent post 1", -3.8f, -3.3f, -8f, -.5f, 12.6f, 15.4f, false);
                b.Wall("Vent post 2", -3.8f, -3.3f, -8f, -.5f, 20.6f, 23.4f, false);
                b.Floor("Lane back floor", -12f, -.25f, 27f, 35f);          // the island ahead
                Slot(b, 1, V(-6.5f, 0f, 35f), 3f, 9f, 4f, 4);
                b.Checkpoint(2, 6f, -6.5f, 10f, Teams.White);
                b.Checkpoint(3, 31f, -6.5f, 10f, Teams.White);
                Barrier(b, "Team barrier (exit)", 70f, -1f, 13f);
            });
            TeamMirror.MirrorTeam(white);
            b.KillFloor(70f);
            m.Configure(ModuleId.M6_TeamSection1, "팀 구간 1", 70f, 3f, true,
                new[] { V(-6.5f, 0f, 0f), V(-6.5f, 0f, 57f), V(-6.5f, 3f, 66f), V(-6.5f, 3f, 70f) });
        }

        // M11 team section 2 (80 m, 0 -> +6): hide behind a statue, run just after a piece has landed.
        // The island's exit is +6, so its ramp is long; promotion zone 3 is on its landing.
        static void M11(CourseBuilder b, CourseModule m)
        {
            var k = b.kit;
            var white = b.In(TeamMirror.WhiteName, () =>
            {
                TeamEntry(b);
                Divider(b, 57f, 80f, 6f);
                b.Floor("Lane floor", -12f, -.25f, 5f, 35f);
                Hazard(b, k.fallingPiece, "10 Falling piece 1", V(-6.5f, 0f, 14f), 0f, 0f);
                Hazard(b, k.fallingPiece, "10 Falling piece 2", V(-6.5f, 0f, 26f), 0f, 2.28f);   // half of 4.55 s
                // Cover: a statue stops the shockwave; about 3.7 m open either side of each.
                foreach (float z in new[] { 11f, 17f, 23f, 29f }) PawnStatue(b, z.ToString("0"), V(-6.5f, 0f, z));
                Slot(b, 2, V(-6.5f, 0f, 35f), 6f, 17f, 6f, 7);
                b.Checkpoint(8, 6f, -6.5f, 10f, Teams.White);
                b.Checkpoint(9, 31f, -6.5f, 10f, Teams.White);
                Barrier(b, "Team barrier (exit)", 80f, -1f, 16f);
            });
            TeamMirror.MirrorTeam(white);
            b.KillFloor(80f);
            m.Configure(ModuleId.M11_TeamSection2, "팀 구간 2", 80f, 6f, true,
                new[] { V(-6.5f, 0f, 0f), V(-6.5f, 0f, 57f), V(-6.5f, 6f, 74f), V(-6.5f, 6f, 80f) });
        }
    }
}
