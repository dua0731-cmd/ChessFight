using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The five mission mini-games (course 01 level design v0.4 §5, "미니게임 v0.2"), each built
    // into a station's GameSocket in the station's frame (x = v across, z = u toward the gate).
    // Every game builds the station's floor itself, because B's chasm and C's moat cut through it.
    //
    // What a game can tell about a pawn is what the course sees: where its hips are and how they
    // move (MissionStation.Pawns). The ragdoll's grip is not visible from here, so "hold the rope"
    // and "push the bar" are read as walking the rope lane backwards and walking round the capstan;
    // planks are picked up and put down by standing at the pile and at the bridge end.
    public static class MissionGames
    {
        public static MiniGameBase Build(int game, MissionStation station)
        {
            var root = new GameObject(game >= 0 && game < PawnRushMissions.Count ? "MG_" + PawnRushMissions.Ids[game] : "MG_none");
            root.SetActive(false);
            root.transform.SetParent(station.Socket, false);
            var b = new CourseBuilder(station.Kit, root.transform);
            MiniGameBase built = null;
            switch (game)
            {
                case PawnRushMissions.Paint: built = MgPaint.Build(b, station); break;
                case PawnRushMissions.Planks: built = MgPlanks.Build(b, station); break;
                case PawnRushMissions.Drawbridge: built = MgDrawbridge.Build(b, station); break;
                case PawnRushMissions.Capstan: built = MgCapstan.Build(b, station); break;
                case PawnRushMissions.Bells: built = MgBells.Build(b, station); break;
            }
            root.SetActive(true);
            return built;
        }

        // Station floor slab between u0 and u1, the full 20 m across.
        public static void Floor(CourseBuilder b, float u0, float u1) => b.Floor("Station floor", -MissionStation.Half, MissionStation.Half, u0, u1, 0f);

        public static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    }
}
