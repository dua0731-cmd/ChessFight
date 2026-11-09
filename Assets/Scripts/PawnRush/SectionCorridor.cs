using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R517 (the user's list, 11-09: "각 구간이 이어지는 통로도 복도 형태로 디자인 해서 넣을 것"): a corridor between two
    // sections of a round, instead of one section's exit opening straight onto the next one's entry. PawnRushRound builds
    // one at every join (six in a full round): ten metres of level way 12 m wide - the width every section's boundary
    // opens to - between walls nobody climbs, under a lid nobody leaps over, in the look of the zone it leaves:
    //   early   an open-air castle passage: limestone walls with battlements, piers, banners, lanterns
    //   mid     a palace hallway: walnut wainscot, portraits, a beamed ceiling with chandeliers, a navy runner
    //   late    a marble gallery: tall curtained windows, a skylit ceiling, a purple runner
    // Corridor frame: entry (0, 0, 0) on the previous section's exit, exit (0, 0, Length), floor top 0.
    //
    // The joins it meets (PAWN_RUSH_SECTIONS §3, the boundary): every obstacle section ends in a 12 m band with rails at
    // |x| 6..6.3 and the hall begins in a 12 m porch walled at |x| 6..6.6, so there the walls run end to end and a gate
    // frame stands round the end (MiniGamePalaceArt.Corridor); a mini-game's exit and entry walls and arches stand up to
    // a metre beyond its exit and before its entry, so the walls stop short of them (FromMiniGame, IntoMiniGame); the
    // war room opens 28.5 m wide, so an end wall closes its south edge either side.
    // Pictures never collide; the bodies here are plain boxes. Nothing moves and nothing goes on the wire: every PC
    // builds the same corridors from the same round.
    public static class SectionCorridor
    {
        public const float Length = 10f, Half = 6f, Wall = .6f;
        // A mini-game's exit end wall and arch reach 0.6 m past its exit; its entry wing wall and arch 0.8 m before its entry.
        public const float FromMiniGame = .65f, IntoMiniGame = .8f;
        // The war room's start floor is 28.5 m across between its side walls.
        public const float FinalHalf = 14.6f;
        public const string NamePrefix = "Corridor ";

        public enum Style { Early, Mid, Late }

        // The zone a corridor takes after the section in `slot` (0-based): the outer court after slots 0 and 1 (the first
        // obstacle section, the early mini-game), the lower palace after 2 and 3, the upper palace after 4 and 5.
        public static Style After(int slot) => slot <= 1 ? Style.Early : slot <= 3 ? Style.Mid : Style.Late;

        public static float Height(Style style) => style == Style.Early ? 4.5f : style == Style.Mid ? 6.5f : 7.5f;

        // Open to the sky the walls stand lower, so the lid over them is out of sight; it stops a knight's leap.
        public static float Lid(Style style) => style == Style.Early ? 7.5f : Height(style);

        // Builds the corridor from `before`'s exit (`at`) to `after`; its exit (Exit) is where `after`'s entry goes.
        public static Transform Build(Transform parent, Pose at, PawnRushSection before, PawnRushSection after, int index)
        {
            var style = After(before.Slot);
            var root = new GameObject(NamePrefix + index + " (" + style + ")").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(at.position, at.rotation);
            float height = Height(style), lid = Lid(style);
            float from = before.Kind == PawnRushSection.SlotKind.MiniGame ? FromMiniGame : 0f;
            float to = Length - (after.Kind == PawnRushSection.SlotKind.MiniGame ? IntoMiniGame : 0f);
            float endHalf = after.Kind == PawnRushSection.SlotKind.Final ? FinalHalf : 0f;
            // The walls' bodies as tall as their pictures (the early wall to its battlements' tops); over an open-air
            // wall an unseen fence up to the lid, which the follow camera looks through as it does the unseen lid.
            float wallTop = style == Style.Early ? height + .7f : lid;

            var bodies = new GameObject("Bodies").transform;
            bodies.SetParent(root, false);
            Body(bodies, "Corridor floor", new Vector3(-Half - Wall, -1f, 0f), new Vector3(Half + Wall, 0f, Length)).AddComponent<CourseFloor>();
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = Mathf.Min(side * Half, side * (Half + Wall)), x1 = Mathf.Max(side * Half, side * (Half + Wall));
                Body(bodies, "Corridor wall", new Vector3(x0, -1f, from), new Vector3(x1, wallTop, to)).AddComponent<NoClimbSurface>();
                if (wallTop < lid)
                {
                    var fence = Body(bodies, "Corridor fence (unseen)", new Vector3(x0, wallTop, from), new Vector3(x1, lid, to));
                    fence.AddComponent<NoClimbSurface>();
                    fence.AddComponent<CameraSeeThrough>();
                }
            }
            var top = Body(bodies, "Corridor lid", new Vector3(-Half - Wall, lid, from), new Vector3(Half + Wall, lid + .5f, to));
            if (style == Style.Early) top.AddComponent<CameraSeeThrough>();
            if (endHalf > 0f)
                foreach (float side in new[] { -1f, 1f })
                    Body(bodies, "Corridor end wall", new Vector3(Mathf.Min(side * (Half + Wall), side * endHalf), -3f, Length - Wall),
                         new Vector3(Mathf.Max(side * (Half + Wall), side * endHalf), lid, Length)).AddComponent<NoClimbSurface>();
            // Nothing falls out of a corridor; this only catches a pawn the physics ever pushes through its floor, as the
            // sections' own far-below volumes do (y -42 .. -40).
            var kill = new GameObject("Kill volume (far below)");
            kill.transform.SetParent(bodies, false);
            kill.transform.localPosition = new Vector3(0f, -41f, Length * .5f);
            var box = kill.AddComponent<BoxCollider>();
            box.size = new Vector3(2f * (Half + Wall), 2f, Length);
            box.isTrigger = true;
            kill.AddComponent<KillVolume>();

            MiniGamePalaceArt.Corridor(root, (int)style, Length, from, to, height, endHalf,
                                       before.Kind == PawnRushSection.SlotKind.Obstacle && !FramesItsEnd(before, true),
                                       after.Kind == PawnRushSection.SlotKind.Obstacle && !FramesItsEnd(after, false));
            return root;
        }

        // An obstacle section whose own end at the join is framed already, so the corridor puts no gate frame there:
        // the grand stair hall's entry porch has its arch and lintel (R303).
        static bool FramesItsEnd(PawnRushSection section, bool exit) => !exit && section.Id == GrandStairHall.Id;

        public static Pose Exit(Transform corridor) => new Pose(corridor.TransformPoint(new Vector3(0f, 0f, Length)), corridor.rotation);

        // Is a world point inside the corridor's walls, between its floor and its lid.
        public static bool Holds(Transform corridor, Vector3 world)
        {
            if (corridor == null) return false;
            var p = corridor.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= Half + Wall && p.z >= 0f && p.z <= Length && p.y >= -1f && p.y <= 8f;
        }

        static GameObject Body(Transform parent, string name, Vector3 min, Vector3 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (min + max) * .5f;
            go.AddComponent<BoxCollider>().size = max - min;
            return go;
        }
    }
}
