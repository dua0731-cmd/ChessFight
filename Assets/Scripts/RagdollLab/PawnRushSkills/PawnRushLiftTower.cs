using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The 4판 rook's 룩 리프트 (RagdollPawn.SetFourC.cs; 11-26, Docs/Skills/PAWN_RUSH_SET4_ROUND1.md 4.4: "발밑에서 지름 3 m
    /// 탑이 0.5초에 4 m 솟아 3초 선다. 위에 있던 모두가 함께 올라간다"): a stone block rising out of the floor under the rook,
    /// carrying up whoever stands on it, standing its time and sinking again. On the host a kinematic box moved through its
    /// body, so the pieces on it ride up as on any lift; every other PC draws it from the S4Lift moment
    /// (PawnRushSkillFx.SetFourC.cs) with no collider, as the bodies it shows are the host's. Not seen in Unity by anyone yet.
    /// </summary>
    public class PawnRushLiftTower : MonoBehaviour
    {
        Rigidbody body;
        Vector3 foot;
        float wide, tall, rise, stand, age;

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
        /// from the floor at <paramref name="feet"/> to <paramref name="tall"/>.</summary>
        public static PawnRushLiftTower Raise(Vector3 feet, float wide, float tall, float rise, float stand)
        {
            var go = new GameObject("Rook lift");
            var t = go.AddComponent<PawnRushLiftTower>();
            t.foot = feet;
            t.wide = Mathf.Max(0.5f, wide) * 0.88f;
            t.tall = Mathf.Max(0.5f, tall);
            t.rise = Mathf.Max(0.05f, rise);
            t.stand = Mathf.Max(0f, stand);
            t.body = go.AddComponent<Rigidbody>();
            t.body.isKinematic = true;
            t.body.interpolation = RigidbodyInterpolation.Interpolate;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(t.wide, t.tall, t.wide);
            go.transform.position = t.Center(0f);
            return t;
        }

        Vector3 Center(float a) => foot + Vector3.up * (TopAt(a, rise, stand, tall) - tall * 0.5f - 0.02f);

        void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            body.MovePosition(Center(age));
            if (age > rise * 2f + stand + 0.1f) Destroy(gameObject);
        }
    }
}
