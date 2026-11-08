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
                case PawnRushMissions.EnPassant: built = MgEnPassant.Build(b, station); break;
                case PawnRushMissions.KingEscort: built = MgKingEscort.Build(b, station); break;
                case PawnRushMissions.Checkmate: built = MgCheckmate.Build(b, station); break;
                case PawnRushMissions.CheckBlock: built = MgCheckBlock.Build(b, station); break;
                case PawnRushMissions.KnightPads: built = MgKnightPads.Build(b, station); break;
            }
            root.SetActive(true);
            return built;
        }

        // Station floor slab between u0 and u1, the full 20 m across: the arena dais (v1.0 §2.1), a dark slate
        // slab down to the plaza floor with the gold rim on its outer edges.
        public static GameObject Floor(CourseBuilder b, float u0, float u1)
        {
            var floor = b.Floor("Station floor", -MissionStation.Half, MissionStation.Half, u0, u1, 0f);
            var station = b.parent.GetComponentInParent<MissionStation>(true);
            floor.GetComponent<Renderer>().sharedMaterial = station != null && station.PalaceStyle
                ? MiniGamePalaceArt.DaisMaterial(station.GameIndex) : Arena.DaisMaterial;
            Arena.Rim(b, u0, u1);
            return floor;
        }

        // R498: how far a picture stands off a face it would otherwise share with another body. Two faces in one
        // plane z-fight (flicker) at any distance; a centimetre is invisible up close and holds at palace range.
        public const float Proud = .01f;

        // A solid wall built into the edge of a floor slab (a moat's or a pool's lining). The collider stays exactly
        // where it was; only its picture moves: `Proud` out of the slab's face toward `facing` (+1 = +z, -1 = -z),
        // `Proud` in from both ends and under the slab's top, so no face of it lies in a face of the slab.
        public static GameObject LinedWall(CourseBuilder b, string name, Vector3 min, Vector3 max, Material material, float facing)
        {
            var wall = b.Box(name, min, max, material, true);
            Object.DestroyImmediate(wall.GetComponent<MeshRenderer>());
            if (facing > 0f) max.z += Proud; else min.z -= Proud;
            min.x += Proud; max.x -= Proud;
            if (max.y > -Proud) max.y = -Proud;
            Arena.Picture(b.parent, name + " look", min, max, material);
            return wall;
        }

        public static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    }
}
