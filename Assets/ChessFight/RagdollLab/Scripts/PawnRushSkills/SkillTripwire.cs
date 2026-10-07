using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The bishop's crossed tripwire (DESIGN §5): two shin-high lines in an X, four pegs. Armed after a moment
    /// (the "팅"), then it trips the first enemies that run through it on the ground; a jump goes over, allies
    /// pass, a rook snaps it. Two trips and it is spent; it goes after its active time either way.
    ///
    /// The trip is checked by hand from the feet each physics step, not from trigger callbacks: a knockdown
    /// from inside OnTrigger is what HANDOFF rule 14 forbids.
    /// </summary>
    public class SkillTripwire : MonoBehaviour
    {
        static readonly List<SkillTripwire> Live = new List<SkillTripwire>();

        RagdollPawn owner;
        int team;
        PawnRushSkillParams skills;
        Vector3 center, a1, b1, a2, b2;
        float age, height;
        bool armed;
        int trips;
        readonly HashSet<RagdollPawn> tripped = new HashSet<RagdollPawn>();
        LineRenderer line1, line2;
        // A trip bends the line it caught (R81: "잡아당기는"): for a moment it stretches with the legs it caught,
        // then twangs back straight. Spent, the wire stays that long before it goes.
        const float BendTime = 0.5f, StretchTime = 0.12f, MaxStretch = 0.9f;
        int bentLine = -1;
        float bentAt, snapAt = -1f;
        RagdollPawn bentBy;
        Vector3 bentContact, released;
        bool spent;
        readonly List<Transform> pegs = new List<Transform>();

        public Vector3 Center => center;
        public bool Armed => armed;

        /// <summary>One of the two lines as it is drawn now (at shin height): its ends and its middle, which is where
        /// it is pulled to while it holds a tripped piece's legs, and where it twangs back from (R81).</summary>
        public void LinePoints(int line, out Vector3 a, out Vector3 mid, out Vector3 b)
        {
            Vector3 up = Vector3.up * height;
            a = (line == 0 ? a1 : a2) + up;
            b = (line == 0 ? b1 : b2) + up;
            mid = Bend(line, a, b);
        }

        /// <summary>How far a line is pulled out of straight now, 0..1 (the effects glow it harder).</summary>
        public float Tension(int line)
        {
            if (line != bentLine) return 0f;
            LinePoints(line, out var a, out var mid, out var b);
            return Mathf.Clamp01(Vector3.Distance(mid, ClosestOn(a, b, mid)) / 0.5f);
        }

        Vector3 Bend(int line, Vector3 a, Vector3 b)
        {
            Vector3 straight = (a + b) * 0.5f;
            if (line != bentLine) return straight;
            float t = age - bentAt;
            if (t >= BendTime) return straight;
            Vector3 contact = bentContact + Vector3.up * height;
            if (t < StretchTime && bentBy != null)
            {
                // Held by the legs it caught: drawn after them, stretching.
                Vector3 legs = (bentBy.bodies[(int)BodyId.FootL].position + bentBy.bodies[(int)BodyId.FootR].position) * 0.5f;
                released = Vector3.ClampMagnitude(legs - contact, MaxStretch);
                return contact + released;
            }
            // Let go: it twangs back, overshooting and settling.
            float u = (t - StretchTime) / (BendTime - StretchTime);
            return contact + released * (Mathf.Cos(u * Mathf.PI * 3f) * (1f - u) * (1f - u));
        }

        static Vector3 ClosestOn(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }

        public static SkillTripwire Spawn(RagdollPawn owner, Vector3 at, Vector3 forward, PawnRushSkillParams skills)
        {
            // One wire per bishop: a new one replaces the old.
            for (int i = Live.Count - 1; i >= 0; i--)
                if (Live[i] != null && Live[i].owner == owner) Live[i].Snap(null);
            var go = new GameObject($"{owner.DisplayName} 교차 밧줄");
            var wire = go.AddComponent<SkillTripwire>();
            wire.Set(owner, at, forward, skills);
            return wire;
        }

        /// <summary>Snap every enemy wire (of <paramref name="byTeam"/>) with a part within <paramref name="radius"/>
        /// of <paramref name="at"/>: the queen's blast, the knight's landing.</summary>
        public static int BreakNear(Vector3 at, float radius, int byTeam)
        {
            int n = 0;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                var w = Live[i];
                if (w == null || !Teams.AreEnemies(w.team, byTeam)) continue;
                float d = Mathf.Min(SegmentDistance(Flat(at), Flat(w.a1), Flat(w.b1)), SegmentDistance(Flat(at), Flat(w.a2), Flat(w.b2)));
                if (d > radius || Mathf.Abs(at.y - w.center.y) > 2f) continue;
                w.Snap("부서짐");
                n++;
            }
            return n;
        }

        void Set(RagdollPawn by, Vector3 at, Vector3 forward, PawnRushSkillParams s)
        {
            owner = by;
            team = by.Team;
            skills = s;
            center = at;
            height = s.bishopHeight;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            float half = s.bishopLineLength * 0.5f;
            Vector3 d1 = Quaternion.Euler(0f, 45f, 0f) * forward, d2 = Quaternion.Euler(0f, -45f, 0f) * forward;
            a1 = at - d1 * half; b1 = at + d1 * half;
            a2 = at - d2 * half; b2 = at + d2 * half;
            Color c = SkillMarks.TeamColor(team);
            line1 = SkillMarks.Line(transform, "Line 1");
            line2 = SkillMarks.Line(transform, "Line 2");
            foreach (var p in new[] { a1, b1, a2, b2 })
            {
                var peg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                var col = peg.GetComponent<Collider>();
                if (col != null) Destroy(col);
                peg.name = "Peg";
                peg.transform.SetParent(transform, false);
                peg.transform.position = p + Vector3.up * (height * 0.5f + 0.02f);
                peg.transform.localScale = new Vector3(0.07f, height * 0.5f + 0.03f, 0.07f);
                var r = peg.GetComponent<MeshRenderer>();
                r.material.color = new Color(0.92f, 0.9f, 0.82f);
                pegs.Add(peg.transform);
            }
            Draw(new Color(c.r, c.g, c.b, 0.35f), 0.025f);
            Live.Add(this);
            RagdollPawn.RaiseSkillFx(new SkillFxEvent { kind = SkillFxKind.BishopWire, by = by, at = at, dir = forward, size = s.bishopLineLength, source = this });
        }

        void OnDestroy() => Live.Remove(this);

        void Draw(Color color, float width)
        {
            for (int line = 0; line < 2; line++)
            {
                LinePoints(line, out var a, out var mid, out var b);
                var lr = line == 0 ? line1 : line2;
                if (line == bentLine && age - bentAt < BendTime) SkillMarks.Polyline(lr, new[] { a, mid, b }, color, width);
                else SkillMarks.Segment(lr, a, b, color, width, false);
            }
        }

        void Update()
        {
            if (!armed)
            {
                // Drawn once at the start; only a bend moves it before it is armed.
                return;
            }
            // The glint running along the lines.
            float glint = 0.6f + 0.4f * Mathf.Sin(Time.time * 9f);
            Color c = Color.Lerp(SkillMarks.TeamColor(team), Color.white, 0.35f);
            Draw(new Color(c.r, c.g, c.b, glint), 0.035f);
        }

        void FixedUpdate()
        {
            age += Time.fixedDeltaTime;
            if (spent)
            {
                // The last trip's yank and twang, then it goes.
                if (age >= snapAt) Snap("끊어짐");
                return;
            }
            if (!armed && age >= skills.bishopArm)
            {
                armed = true;
                Report("무장 (\"팅\")");
            }
            if (age >= skills.bishopArm + skills.bishopActive)
            {
                Snap("시간 끝 (사라짐)");
                return;
            }
            if (!armed) return;
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null || pawn == owner || tripped.Contains(pawn) || !Teams.AreEnemies(team, pawn.Team)) continue;
                if (pawn.State != PawnState.Active || !pawn.Grounded) continue;   // jumping, flying or down: over it
                if (!FootOnLine(pawn)) continue;
                tripped.Add(pawn);
                if (pawn.Piece == PieceKind.Rook)
                {
                    Snap($"{pawn.DisplayName}(룩)이 걸리지 않고 끊음");
                    return;
                }
                Vector3 run = pawn.Hips.linearVelocity;
                run.y = 0f;
                Vector3 runDir = run.sqrMagnitude > 0.25f ? run.normalized : Vector3.forward;
                // The wire catches the legs and yanks them back (R81: "잡아당기는"): the body goes on over them, face
                // first, and the line it caught is pulled out after the legs before it twangs back.
                Vector3 push = runDir * 1.6f + Vector3.up * 0.3f;
                string result = owner != null ? owner.SkillTrip(pawn, push) : "-";
                foreach (var id in new[] { BodyId.FootL, BodyId.FootR, BodyId.ThighL, BodyId.ThighR })
                    pawn.bodies[(int)id].linearVelocity += -runDir * 4.5f + Vector3.down * 0.8f;
                Vector3 feet = (pawn.bodies[(int)BodyId.FootL].position + pawn.bodies[(int)BodyId.FootR].position) * 0.5f;
                Vector2 f = Flat(feet);
                bentLine = SegmentDistance(f, Flat(a1), Flat(b1)) <= SegmentDistance(f, Flat(a2), Flat(b2)) ? 0 : 1;
                Vector3 la = bentLine == 0 ? a1 : a2, lb = bentLine == 0 ? b1 : b2;
                bentContact = ClosestOn(la, lb, new Vector3(feet.x, la.y, feet.z));
                bentAt = age;
                bentBy = pawn;
                released = Vector3.zero;
                trips++;
                Vector3 trippedAt = pawn.Hips.position;
                trippedAt.y = center.y;
                RagdollPawn.RaiseSkillFx(new SkillFxEvent { kind = SkillFxKind.BishopTrip, by = owner, target = pawn, at = trippedAt, dir = run, count = trips, source = this });
                Report($"{pawn.DisplayName} 걸림 ({trips}/{skills.bishopTrips}) → {result}, 다리를 잡아챔");
                if (trips >= skills.bishopTrips)
                {
                    spent = true;
                    snapAt = age + BendTime;
                    return;
                }
            }
        }

        bool FootOnLine(RagdollPawn pawn)
        {
            foreach (var id in new[] { BodyId.FootL, BodyId.FootR })
            {
                Vector3 foot = pawn.bodies[(int)id].position;
                if (foot.y > center.y + height + 0.12f || foot.y < center.y - 0.4f) continue;
                Vector2 f = Flat(foot);
                if (SegmentDistance(f, Flat(a1), Flat(b1)) < 0.18f || SegmentDistance(f, Flat(a2), Flat(b2)) < 0.18f) return true;
            }
            return false;
        }

        void Snap(string why)
        {
            if (why != null) Report(why);
            Destroy(gameObject);
        }

        void Report(string text) => PawnRushSkillBed.Report($"비숍 밧줄: {text}");

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
