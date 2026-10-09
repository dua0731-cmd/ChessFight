using UnityEngine;

namespace ChessFight.PawnRush
{
    // E, six bell towers: 6, 6, 7.5, 7.5, 9 and 9 m tall (u 5, 11, 17 at v -5 and 5), climbed up the
    // stone, their tops 2.4 x 2.4 m. Each tower steps back 0.8 m every 3 m on its south side, the
    // climbing face (a rest ledge, design doc §3). A pawn of the team standing on a top rings its
    // bell, unless one of the other team stands there too; a rung bell stays rung. All six: the
    // gate opens.
    public sealed class MgBells : MiniGameBase
    {
        public const float Top = 2.4f, Ledge = .8f, Reach = 1.3f;
        public static readonly Vector3[] Towers =
        {
            new Vector3(-5f, 6f, 5f), new Vector3(5f, 6f, 5f), new Vector3(-5f, 7.5f, 11f),
            new Vector3(5f, 7.5f, 11f), new Vector3(-5f, 9f, 17f), new Vector3(5f, 9f, 17f),
        };   // (v, height, u)

        [SerializeField] MissionStation station;
        [SerializeField] Renderer[] bells = new Renderer[6];

        readonly bool[] rung = new bool[6];
        int count;

        public int Rung => count;

        static Vector3 V(float x, float y, float z) => MissionGames.V(x, y, z);

        public static MgBells Build(CourseBuilder b, MissionStation station)
        {
            var k = b.kit;
            MissionGames.Floor(b, 0f, 20f);
            var bells = new Renderer[Towers.Length];
            for (int i = 0; i < Towers.Length; i++)
            {
                var t = Towers[i];
                float h = t.y, x0 = t.x - Top * .5f, x1 = t.x + Top * .5f, z1 = t.z + Top * .5f;
                // Storeys of 3 m (the last one shorter), each 0.8 m deeper to the south than the one on it.
                int storeys = Mathf.CeilToInt(h / 3f - .01f);
                for (int s = 0; s < storeys; s++)
                {
                    float y0 = s * 3f, y1 = Mathf.Min(h, y0 + 3f), z0 = t.z - Top * .5f - (storeys - 1 - s) * Ledge;
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
            return game;
        }

        static bool OnTop(Vector3 local, Vector3 tower) =>
            Mathf.Abs(local.x - tower.x) <= Reach && Mathf.Abs(local.z - tower.z) <= Reach && local.y >= tower.y - .2f && local.y <= tower.y + 2f;

        void FixedUpdate()
        {
            if (Completed || station == null) return;
            var pawns = station.Pawns();
            for (int i = 0; i < Towers.Length; i++)
            {
                if (rung[i]) continue;
                bool own = false, other = false;
                foreach (var p in pawns)
                {
                    if (!OnTop(p.Local, Towers[i])) continue;
                    if (p.Own) own = true;
                    else other = true;
                }
                if (!own || other) continue;
                rung[i] = true;
                count++;
                if (bells[i] != null) bells[i].sharedMaterial = station.Kit.rankGold;
            }
            Progress01 = count / (float)Towers.Length;
            if (count >= Towers.Length) Complete();
        }

        public override void ResetGame()
        {
            base.ResetGame();
            count = 0;
            for (int i = 0; i < rung.Length; i++)
            {
                rung[i] = false;
                if (bells[i] != null) bells[i].sharedMaterial = station.Kit.hazard;
            }
        }
    }
}
