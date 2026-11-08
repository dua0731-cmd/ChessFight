using UnityEngine;

namespace ChessFight.Game
{
    // The lobby castle's walls and towers in real stone blocks (10-17, 승규 님: the same polygon count as
    // the title hall's wall and piers): the baked castle (Resources/Lobby/Castle) keeps its shapes, and
    // blocks from StoneBlocks are laid over the faces the camera sees, standing a little proud of them so
    // the baked brick pattern shows only as mortar. Coordinates are the castle's in the lobby (Unity X, the
    // Blender Z as height, the Blender Y as depth; the camera looks along +Z), from build_castle.py:
    //   back wall      front z 18.1, x 3.9..23 each side, height 5 (the wall walk on top)
    //   gatehouse      front z 17.2, x -3.9..3.9, from the landing (1.68) to 7, doorway round-headed
    //   gate towers    (±5.6, z 18.3), radius 1.7, blocks from .7 to the gold band at 5.5
    //   corner towers  (±17.5, z 17.6), radius 2.3, blocks from .7 to the gold band at 6.3
    public sealed partial class LobbyStage
    {
        Mason mason;

        void Blocks(Transform root)
        {
            mason = new Mason(1718);
            const float bw = .9f, bh = .42f, depth = .16f, proud = .07f;
            const float landing = 1.68f;

            // Back wall, both sides, where the towers do not stand in front of it.
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side > 0 ? 7.2f : -23f, x1 = side > 0 ? 23f : -7.2f;
                mason.Face(new Vector3(x0, 0f, 18.1f), Vector3.right, Vector3.up, Vector3.back, x1 - x0, 5f, bw, bh, depth, proud,
                           (u, v) => { float x = x0 + u; return Mathf.Abs(Mathf.Abs(x) - 17.5f) < 2.2f; });
            }

            // The gatehouse face round its doorway, the arch stones and the keystone.
            const float doorHalf = 1.6f, spring = landing + .08f + 1.4f, archOuter = doorHalf + .62f;
            mason.Face(new Vector3(-3.9f, landing, 17.2f), Vector3.right, Vector3.up, Vector3.back, 7.8f, 7f - .25f - landing, bw, bh, depth, proud,
                       (u, v) =>
                       {
                           float x = -3.9f + u, y = landing + v;
                           if (Mathf.Abs(x) < archOuter && y < spring) return true;
                           float dx = x, dy = y - spring;
                           if (dx * dx + dy * dy < archOuter * archOuter) return true;
                           return Mathf.Abs(x) < .7f && y < spring + archOuter + 1.4f;   // keystone and crown
                       });

            // Round towers: the half the camera sees.
            System.Func<Vector3, bool> front = dir => dir.z < .35f;
            foreach (float side in new[] { -1f, 1f })
            {
                mason.Ring(new Vector3(side * 5.6f, 0, 18.3f), 1.7f, .72f, 5.48f, bw, bh, depth, proud, front);
                mason.Ring(new Vector3(side * 17.5f, 0, 17.6f), 2.3f, .72f, 6.28f, bw, bh, depth, proud, front);
            }
            mason.Build(root, "Castle Blocks");
        }
    }
}
