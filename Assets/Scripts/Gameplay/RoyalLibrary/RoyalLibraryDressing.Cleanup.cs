namespace ChessFight.Gameplay
{
    // R0 cleanup (승규 11-18 "코스 외관 필요 없는 부분 지워"): blockout pieces seen only from outside the course, drawn by
    // no zone - the outer 320 x 320 m box's two sides (x +-160) and its north returns (x +-142, z 170). The gallery's and
    // the chess room's own walls close the course inside them, so these only showed as navy slabs from the editor's
    // outside view. They stop drawing; their colliders stay (unreachable, harmless).
    public sealed partial class RoyalLibraryDressing
    {
        static readonly Hang[] Cleanup =
        {
            new Hang { zone = "R0", model = null, covers = new[] { "Square perimeter side*", "North perimeter return*" } },
        };
    }
}
