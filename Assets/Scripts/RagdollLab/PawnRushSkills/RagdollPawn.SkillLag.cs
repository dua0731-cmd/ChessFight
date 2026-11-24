using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R819 (2026-11-24, 사용자 "폰 러쉬 스킬 판정이 온라인에서 애매하대 온라인에서는 뛰고 계속 움직이고 하니까 스킬이 안 걸릴 때도 있어",
    /// "각 기물별 … 알아서 진행해"): the Pawn Rush skills judged online as the caster saw them. The host runs every skill against
    /// the pieces where they are now; a client aimed at a picture <see cref="SkillViewLag"/> seconds older (its playback delay and
    /// its round trip, <see cref="SkillLag"/>), so a running or jumping target it had in its sights was often out of the host's
    /// cone or range by then and the skill did nothing. Every piece keeps a short trail of its hips, chest and head (the host's;
    /// a client's puppets record nothing), and the skills' target searches count a piece that fits either where it is or where
    /// the caster's screen showed it (<see cref="SeenBy"/>). What a skill then does to the piece - the pull, the stomp, the
    /// push - still goes by where it really is. Offline and for the host's own player the lag is 0 and nothing changes.
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>How old (s) this pawn's player's picture of the others is on the host (0: the host's own, a bot, offline).
        /// Set by the online link from what the client reports with its input; at most <see cref="SkillLag.MostSeconds"/>.</summary>
        public float SkillViewLag
        {
            get => skillViewLag;
            set => skillViewLag = Mathf.Clamp(value, 0f, SkillLag.MostSeconds);
        }

        float skillViewLag;

        /// <summary>The trail: 0.4 s at 120 physics steps a second (0.8 s at 60), past the longest look-back.</summary>
        const int TrailSteps = 48;
        readonly SkillTrail skillTrail = new SkillTrail(TrailSteps, 9);
        readonly float[] trailRow = new float[9];

        /// <summary>Below this the look-back is not worth a second test (s).</summary>
        const float LagWorthIt = 0.02f;

        /// <summary>The trail's step: the hips, the chest and the head as they are at the start of this physics step.</summary>
        void RecordSkillTrail()
        {
            if (PawnRushSkills == null || NetworkPuppet) return;
            Put(0, bodies[0].position);
            Put(3, bodies[(int)BodyId.Chest].position);
            Put(6, bodies[(int)BodyId.Head].position);
            skillTrail.Add(Time.fixedTimeAsDouble, trailRow);
        }

        void Put(int at, Vector3 v)
        {
            trailRow[at] = v.x;
            trailRow[at + 1] = v.y;
            trailRow[at + 2] = v.z;
        }

        /// <summary>A piece's hips, chest and head as they were <paramref name="ago"/> seconds back (from its trail).</summary>
        public bool TrailAt(float ago, out Vector3 pelvis, out Vector3 chest, out Vector3 head)
        {
            pelvis = bodies[0].position;
            chest = bodies[(int)BodyId.Chest].position;
            head = bodies[(int)BodyId.Head].position;
            if (skillTrail.Count == 0 || !skillTrail.At(Time.fixedTimeAsDouble - ago, trailRow)) return false;
            pelvis = new Vector3(trailRow[0], trailRow[1], trailRow[2]);
            chest = new Vector3(trailRow[3], trailRow[4], trailRow[5]);
            head = new Vector3(trailRow[6], trailRow[7], trailRow[8]);
            return true;
        }

        /// <summary>Where <paramref name="other"/> was on this pawn's player's screen (<see cref="SkillViewLag"/> back). False when
        /// there is no look-back to make (no lag, no trail): the piece's own place is the only one then.</summary>
        bool SeenBy(RagdollPawn other, out Vector3 pelvis, out Vector3 chest, out Vector3 head)
        {
            pelvis = chest = head = Vector3.zero;
            if (other == null || skillViewLag < LagWorthIt) return false;
            return other.TrailAt(skillViewLag, out pelvis, out chest, out head);
        }

        /// <summary>The nearest point to <paramref name="p"/> on the segment <paramref name="a"/>..<paramref name="b"/>.</summary>
        static Vector3 NearestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            if (len < 1e-8f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len);
            return a + ab * t;
        }

        /// <summary>How near (m) the segment <paramref name="a"/>..<paramref name="b"/> comes to a piece's body: its hips, chest and
        /// head joined (a jumping piece's body is up where it is), and the point of the segment closest to it.</summary>
        static float SegmentToBody(Vector3 a, Vector3 b, RagdollPawn other, out Vector3 at)
        {
            Vector3 pelvis = other.bodies[0].position, head = other.bodies[(int)BodyId.Head].position;
            float best = float.MaxValue;
            at = a;
            // A few points along the body (hips to head): enough for a 0.2 m chain and a 0.6 m tall piece.
            for (int k = 0; k <= 4; k++)
            {
                Vector3 p = Vector3.Lerp(pelvis, head, k / 4f);
                Vector3 q = NearestOnSegment(a, b, p);
                float d = Vector3.Distance(p, q);
                if (d < best)
                {
                    best = d;
                    at = q;
                }
            }
            return best;
        }

        /// <summary>Where a piece's chest will be in <paramref name="seconds"/> going as it goes now, across the ground only (a
        /// jump's rise and fall is left to the flight's homing), at most <paramref name="most"/> m ahead.</summary>
        static Vector3 LeadChest(RagdollPawn other, float seconds, float most)
        {
            Vector3 chest = other.bodies[(int)BodyId.Chest].position;
            Vector3 v = Flat(other.bodies[0].linearVelocity) * Mathf.Max(0f, seconds);
            return chest + Vector3.ClampMagnitude(v, most);
        }
    }
}
