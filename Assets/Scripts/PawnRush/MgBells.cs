using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // E, the bell towers: towers climbed up the stone, their tops 2 x 2 m. Each tower steps back 0.8 m every 3 m on
    // its south side, the climbing face (a rest ledge, design doc §3). A pawn of the team standing on a top rings its
    // bell, unless one of the other team stands there too; a rung bell stays rung. All of them: the gate opens.
    //
    // R543 (user 11-11: "종탑은 종탑의 개수를 조절해줘. 그리고 지금 종탑은 종탑 꼭대기에서 다른 종탑으로 다이빙을 통해 바로 넘어가지
    // 못하게 해야 해. 적절히 띄어서 배치해주고 높이도 신경써줘"): a tower for each player of the team (three to six), every
    // one 9 m, and every top 6 m or more from every other - further than a sprinting jump-and-dive carries a pawn on
    // the level (BellTowers, Core: where they stand and why). Before, six towers of 6, 7.5 and 9 m stood 3.6 m apart
    // down each side, and a pawn on a top dived onto the next lower one.
    public sealed class MgBells : MiniGameBase, IProgressCount
    {
        public const float Top = BellTowers.Top, Ledge = BellTowers.Ledge, Reach = Top * .5f + .1f;
        public const int MaxTowers = 8;   // the block's one byte of bells

        // The towers of a game saved in a scene before R543 (course 01's): (v, height, u).
        static readonly Vector3[] Legacy =
        {
            new Vector3(-5f, 6f, 5f), new Vector3(5f, 6f, 5f), new Vector3(-5f, 7.5f, 11f),
            new Vector3(5f, 7.5f, 11f), new Vector3(-5f, 9f, 17f), new Vector3(5f, 9f, 17f),
        };
        const float LegacyReach = 1.3f;

        [SerializeField] MissionStation station;
        [SerializeField] Renderer[] bells = new Renderer[0];
        [Tooltip("(v, height, u) of each tower in the game's frame, as built for the team's size.")]
        [SerializeField] Vector3[] towers = new Vector3[0];

        bool[] rung;
        int count;

        // The towers standing in this game, (v, height, u) in its frame.
        public Vector3[] Towers => towers != null && towers.Length > 0 ? towers : Legacy;
        float TopReach => towers != null && towers.Length > 0 ? Reach : LegacyReach;
        bool[] Rung => rung != null && rung.Length == Towers.Length ? rung : (rung = new bool[Towers.Length]);

        public int Done => count;
        public int Total => Towers.Length;

        static Vector3 V(float x, float y, float z) => MissionGames.V(x, y, z);

        public static MgBells Build(CourseBuilder b, MissionStation station)
        {
            var k = b.kit;
            MissionGames.Floor(b, 0f, 20f);
            var layout = BellTowers.Layout(station.TeamSize);
            float m = MiniGameParts.Mirror(station);
            var towers = new Vector3[Mathf.Min(layout.Length, MaxTowers)];
            var bells = new Renderer[towers.Length];
            for (int i = 0; i < towers.Length; i++)
            {
                // The side wall is at v -10 in white's station and at +10 in black's, its mirror.
                var t = new Vector3(layout[i].V * m, layout[i].Height, layout[i].U);
                towers[i] = t;
                float h = t.y, x0 = t.x - Top * .5f, x1 = t.x + Top * .5f, z1 = t.z + Top * .5f;
                // Storeys of 3 m (the last one shorter), each 0.8 m deeper to the south than the one on it.
                int storeys = BellTowers.Storeys(h);
                for (int s = 0; s < storeys; s++)
                {
                    float y0 = s * BellTowers.Storey, y1 = Mathf.Min(h, y0 + BellTowers.Storey), z0 = t.z - Top * .5f - (storeys - 1 - s) * Ledge;
                    b.Wall("Tower " + (i + 1) + " storey " + (s + 1), x0, x1, y0, y1, z0, z1, true, true);
                }
                // The bell over the top, on two posts (pictures: no colliders).
                b.Box("Bell post W", V(x0 + .1f, h, t.z - .1f), V(x0 + .25f, h + 2.4f, t.z + .1f), k.trimBlack, false, false);
                b.Box("Bell post E", V(x1 - .25f, h, t.z - .1f), V(x1 - .1f, h + 2.4f, t.z + .1f), k.trimBlack, false, false);
                b.Box("Bell beam", V(x0 + .1f, h + 2.3f, t.z - .1f), V(x1 - .1f, h + 2.45f, t.z + .1f), k.trimBlack, false, false);
                bells[i] = b.Cylinder("Bell", V(t.x, h + 1.6f, t.z), .9f, .7f, k.hazard, false).GetComponent<Renderer>();
            }
            var game = b.parent.gameObject.AddComponent<MgBells>();
            game.station = station;
            game.bells = bells;
            game.towers = towers;
            return game;
        }

        bool OnTop(Vector3 local, Vector3 tower)
        {
            float reach = TopReach;
            return Mathf.Abs(local.x - tower.x) <= reach && Mathf.Abs(local.z - tower.z) <= reach && local.y >= tower.y - .2f && local.y <= tower.y + 2f;
        }

        public override void WriteNet(ref NetBlock block)
        {
            int bits = 0;
            var r = Rung;
            for (int i = 0; i < r.Length; i++) if (r[i]) bits |= 1 << i;
            block.U8(bits);
        }

        public override void ReadNet(ref NetBlock block)
        {
            int bits = block.ReadU8();
            var r = Rung;
            count = 0;
            for (int i = 0; i < r.Length; i++)
            {
                r[i] = (bits >> i & 1) != 0;
                if (r[i]) count++;
                if (i < bells.Length && bells[i] != null) bells[i].sharedMaterial = r[i] ? station.Kit.rankGold : station.Kit.hazard;
            }
        }

        void FixedUpdate()
        {
            if (Completed || station == null || Remote) return;
            var pawns = station.Pawns();
            var all = Towers;
            var r = Rung;
            for (int i = 0; i < all.Length; i++)
            {
                if (r[i]) continue;
                bool own = false, other = false;
                foreach (var p in pawns)
                {
                    if (!OnTop(p.Local, all[i])) continue;
                    if (p.Own) own = true;
                    else other = true;
                }
                if (!own || other) continue;
                r[i] = true;
                count++;
                if (i < bells.Length && bells[i] != null) bells[i].sharedMaterial = station.Kit.rankGold;
            }
            Progress01 = count / (float)all.Length;
            if (count >= all.Length) Complete();
        }

        public override void ResetGame()
        {
            base.ResetGame();
            count = 0;
            var r = Rung;
            for (int i = 0; i < r.Length; i++)
            {
                r[i] = false;
                if (i < bells.Length && bells[i] != null) bells[i].sharedMaterial = station.Kit.hazard;
            }
        }
    }
}
