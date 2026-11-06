using UnityEngine;

namespace ChessFight.PawnRush
{
    // 승규 님's final approach and throne room, grey boxes for the test scene (seoungtest, 10-30): what follows the
    // endgame's mission 3 gate (Docs/KingRush/PAWN_RUSH_ENDGAME_RESEARCH.md §9). The tower's two ways up are kept and
    // rebuilt for six a side. Running north from z 0 on y 0:
    //
    //   z  0 .. 24   the yard behind the mission 3 gate, 30 m wide                                            CP 1
    //   fast way     x -6..6: a 6 m climb in two 3 m faces (rest ledge at 3 m), two pendulums over its upper half
    //   safe ways    x -15..-9 and 9..15: 6 m wide ramps, z 10 -> 30 rising 0 -> 6 m, two 04 hammers each
    //   z 30 .. 36   the antechamber on top (y 6)                                                             CP 2
    //   z 36 .. 60   the throne room: walled, nothing to fall off, grabbing allowed (no NoGrabZone); the dais in the
    //                middle with the 8.5 m throne circle, the finish (more than half a team on it at once: four of six)
    public static partial class Course01v4Builder
    {
        const float FinalTop = 6f, ThroneZ = 50f, ThroneCircle = 8.5f;

        static void FinalApproach(CourseBuilder b)
        {
            var k = b.kit;
            b.In("Yard", () =>
            {
                b.Floor("Yard floor", -15f, 15f, 0f, 30f, 0f);
                // The mission 3 gate behind (closed here: the shared mission room is not built yet).
                b.Wall("Back wall", -15.5f, 15.5f, -1f, 4f, -.5f, 0f, false);
                b.Wall("Mission 3 gate pier W", -7f, -6f, 0f, 5.5f, -.5f, .5f, false);
                b.Wall("Mission 3 gate pier E", 6f, 7f, 0f, 5.5f, -.5f, .5f, false);
                b.Wall("Mission 3 gate lintel", -7f, 7f, 4.5f, 5.5f, -.5f, .5f, false);
                b.Rail("Yard rail W", -15.5f, -15f, 0f, 10f, 0f);
                b.Rail("Yard rail E", 15f, 15.5f, 0f, 10f, 0f);
                b.Checkpoint(1, V(0f, 0f, 3f), 0f, 24f);
            });

            b.In("Fast way", () =>
            {
                // Two 3 m faces of climbable stone, the upper set back 0.8 m (the rest ledge), the block behind to the top.
                b.Wall("Face 1", -6f, 6f, 0f, 3f, 24f, 25.6f, true, true);
                b.Wall("Face 2", -6f, 6f, 3f, FinalTop, 24.8f, 25.6f, true, true);
                b.Wall("Climb block", -6f, 6f, 0f, FinalTop, 25.6f, 30f, false, true);
                // Marble shoulders between the climb and the ramps, set back to the upper face so the balls clear them.
                b.Wall("Shoulder W", -9f, -6f, 0f, FinalTop, 24.8f, 30f, false, true);
                b.Wall("Shoulder E", 6f, 9f, 0f, FinalTop, 24.8f, 30f, false, true);
                // Two pendulums swinging together along x over the ledge and the upper face: pivots 10.2 m up, balls
                // 3.1 .. 5.3 m up at the bottom of the swing (clear of the lower face's top), 0.1 m in front of the
                // upper face, out to x 8 at either end (clear of the ramps from x 9). Hung from a frame on the shoulders.
                foreach (float x in new[] { -2f, 2f })
                {
                    var p = Hazard(b, k.pendulum, "11 Pendulum F " + (x < 0f ? "W" : "E"), V(x, 2.9f, 23.6f), 0f, 0f);
                    p.AddComponent<ObstacleSpan>().Configure(Vector3.right, -6f, 6f, ObstacleSpan.Probe.Face, 4.5f, Vector3.forward, "F face");
                }
                b.Wall("Pendulum frame post W", -9f, -8.2f, FinalTop, 11.2f, 24.8f, 25.6f, false);
                b.Wall("Pendulum frame post E", 8.2f, 9f, FinalTop, 11.2f, 24.8f, 25.6f, false);
                b.Wall("Pendulum frame beam", -9f, 9f, 10.7f, 11.2f, 23.1f, 25.6f, false);
            });

            // The safe ways: a ramp either side (16.7 degrees), solid to the ground, its sides marble so it is walked up,
            // not climbed onto halfway. The east one is the west one mirrored (hammers turn the other way, same phase).
            var west = b.Group("Safe way W");
            var before = b.parent;
            b.parent = west;
            b.Slope("Ramp W", -15f, -9f, 10f, 30f, 0f, FinalTop, 7f, 0f, k.floorChecker, true).AddComponent<CourseFloor>().sloped = true;
            b.Slope("Ramp W rail", -15.3f, -15f, 10f, 30f, 0f, FinalTop, 0f, 1.5f, k.wallNoClimb, true);
            float[] hz = { 14f, 22f }, phase = { 0f, 2.55f };
            for (int i = 0; i < 2; i++)
            {
                float y = FinalTop * (hz[i] - 10f) / 20f;
                var hammer = Hazard(b, k.hammer, "04 Hammer W" + (i + 1), V(-12f, y, hz[i]), 0f, phase[i]);
                // Its post out on the rail, off the 6 m way.
                var pedestal = hammer.transform.Find("Back pedestal");
                if (pedestal != null) pedestal.localPosition = V(-3.5f, 1.9f, 1.2f);
                Span(hammer, Vector3.right, -3.6f, 3.6f, y);
            }
            b.parent = before;
            var east = TeamMirror.MirrorGroup(west, "Safe way E");
            foreach (Transform t in east.GetComponentsInChildren<Transform>(true))
                if (t.name == "Back pedestal") t.localPosition = V(3.5f, 1.9f, 1.2f);   // the prefab is not mirrored inside

            b.In("Antechamber", () =>
            {
                b.Wall("Antechamber base", -15f, 15f, 0f, FinalTop - 1f, 30f, 36f, false);
                b.Floor("Antechamber floor", -15f, 15f, 30f, 36f, FinalTop);
                b.Rail("Antechamber rail W", -15.5f, -15f, 30f, 36.5f, FinalTop);
                b.Rail("Antechamber rail E", 15f, 15.5f, 30f, 36.5f, FinalTop);
                b.Rail("Antechamber rail NW", -15f, -12.5f, 36f, 36.5f, FinalTop);
                b.Rail("Antechamber rail NE", 12.5f, 15f, 36f, 36.5f, FinalTop);
                b.Checkpoint(2, V(0f, FinalTop, 32f), 0f, 24f);
            });

            b.In("Throne room", () =>
            {
                float wall = FinalTop + 5f;
                b.Wall("Throne room base", -12f, 12f, 0f, FinalTop - 1f, 36f, 60f, false);
                b.Floor("Throne room floor", -12f, 12f, 36f, 60f, FinalTop);
                b.Wall("Wall W", -12.5f, -12f, FinalTop, wall, 36f, 60.5f, false);
                b.Wall("Wall E", 12f, 12.5f, FinalTop, wall, 36f, 60.5f, false);
                b.Wall("Wall N", -12.5f, 12.5f, FinalTop, wall, 60f, 60.5f, false);
                b.Wall("Wall S W", -12.5f, -6f, FinalTop, wall, 36f, 36.5f, false);
                b.Wall("Wall S E", 6f, 12.5f, FinalTop, wall, 36f, 36.5f, false);
                b.Wall("Door lintel", -6f, 6f, FinalTop + 4f, wall, 36f, 36.5f, false);
                // The dais: one 0.15 m step (course 01's kerb height; steps catch a ragdoll's feet) with a 0.5 m rim
                // round the 8.5 m circle.
                var middle = V(0f, FinalTop, ThroneZ);
                b.Cylinder("Dais", middle, ThroneCircle + 1f, .15f, k.trimWhite).AddComponent<CourseFloor>();
                var circle = middle + Vector3.up * .15f;
                FinishCircle(b, "Finish (throne circle)", "Throne circle", circle, ThroneCircle);
                // The throne behind the circle (looks only, not a place to stand) and the crown over the circle.
                b.Box("Throne seat", V(-2f, FinalTop, ThroneZ + 6f), V(2f, FinalTop + 1.2f, ThroneZ + 8f), k.rankGold, true);
                b.Box("Throne back", V(-2.5f, FinalTop, ThroneZ + 8f), V(2.5f, FinalTop + 5f, ThroneZ + 9f), k.rankGold, true);
                Crown(b, "Throne crown", circle + Vector3.up * 4f);
            });
        }
    }
}
