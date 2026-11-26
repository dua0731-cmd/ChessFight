using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The 4판 rook's 룩 리프트 (RagdollPawn.SetFourC.cs; 11-26, Docs/Skills/PAWN_RUSH_SET4_ROUND1.md 4.4: "발밑에서 지름 3 m
    /// 탑이 0.5초에 4 m 솟아 3초 선다. 위에 있던 모두가 함께 올라간다"): a stone block rising out of the floor, carrying up
    /// whoever stands on it, standing its time and sinking again. On the host a kinematic box moved through its body, so
    /// the pieces on it ride up as on any lift; every other PC draws it from the S4Lift moment (PawnRushSkillFx.SetFourC.cs)
    /// with no collider, as the bodies it shows are the host's.
    ///
    /// Under the movement rule (USER_SKILLS 9.1, 11-26 night: "탑 위에서는 누구도 옆으로 못 나간다. 탑이 내려오면 다 같이
    /// 내려온다"): it rises <c>delay</c> s after the key (the rook hops aside meanwhile: it does not ride), and four walls
    /// <c>wall</c> m high stand round its top, growing with it out of the floor, so nobody on it walks or jumps off before it
    /// is down again. Not seen in Unity by anyone yet.
    /// </summary>
    public class PawnRushLiftTower : MonoBehaviour
    {
        Rigidbody body;
        BoxCollider[] walls;
        Vector3 foot;
        float wide, tall, rise, stand, delay, wall, age;
        const float WallThick = 0.2f;

        /// <summary>How high a tower's top stands over the floor <paramref name="age"/> seconds after it began (risen over
        /// <paramref name="rise"/>, standing <paramref name="stand"/>, sinking as it rose).</summary>
        public static float TopAt(float age, float rise, float stand, float tall)
        {
            rise = Mathf.Max(0.05f, rise);
            if (age < rise) return tall * Mathf.SmoothStep(0f, 1f, age / rise);
            if (age < rise + stand) return tall;
            return tall * (1f - Mathf.SmoothStep(0f, 1f, (age - rise - stand) / rise));
        }

        /// <summary>A tower <paramref name="wide"/> across (square, a little inside the round one drawn), its top rising
        /// from the floor at <paramref name="feet"/> to <paramref name="tall"/> after <paramref name="delay"/> s, walled round
        /// its top <paramref name="wall"/> m high (0: no walls).</summary>
        public static PawnRushLiftTower Raise(Vector3 feet, float wide, float tall, float rise, float stand, float delay = 0f, float wall = 0f)
        {
            var go = new GameObject("Rook lift");
            var t = go.AddComponent<PawnRushLiftTower>();
            t.foot = feet;
            t.wide = Mathf.Max(0.5f, wide) * 0.88f;
            t.tall = Mathf.Max(0.5f, tall);
            t.rise = Mathf.Max(0.05f, rise);
            t.stand = Mathf.Max(0f, stand);
            t.delay = Mathf.Max(0f, delay);
            t.wall = Mathf.Max(0f, wall);
            t.body = go.AddComponent<Rigidbody>();
            t.body.isKinematic = true;
            t.body.interpolation = RigidbodyInterpolation.Interpolate;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(t.wide, t.tall, t.wide);
            if (t.wall > 0f)
            {
                t.walls = new BoxCollider[4];
                for (int i = 0; i < 4; i++) t.walls[i] = go.AddComponent<BoxCollider>();
            }
            go.transform.position = t.Center(0f);
            t.PlaceWalls(0f);
            return t;
        }

        float Top(float a) => TopAt(Mathf.Max(0f, a - delay), rise, stand, tall);

        Vector3 Center(float a) => foot + Vector3.up * (Top(a) - tall * 0.5f - 0.02f);

        /// <summary>The walls round the top: as high as the top stands over the floor, wall m at most (so none sticks out of
        /// the floor before it rises or after it has sunk).</summary>
        void PlaceWalls(float a)
        {
            if (walls == null) return;
            float h = Mathf.Min(wall, Mathf.Max(0.01f, Top(a)));
            float y = tall * 0.5f + h * 0.5f, half = wide * 0.5f - WallThick * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                bool alongX = i < 2;
                float sign = i % 2 == 0 ? 1f : -1f;
                walls[i].center = alongX ? new Vector3(0f, y, half * sign) : new Vector3(half * sign, y, 0f);
                walls[i].size = alongX ? new Vector3(wide, h, WallThick) : new Vector3(WallThick, h, wide);
            }
        }

        void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            body.MovePosition(Center(age));
            PlaceWalls(age);
            if (age > delay + rise * 2f + stand + 0.1f) Destroy(gameObject);
        }
    }
}
