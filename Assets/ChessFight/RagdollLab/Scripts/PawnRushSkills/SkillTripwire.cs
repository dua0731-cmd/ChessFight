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
    /// R82 ("상대가 거길 지나가면 줄이 늘어나는"): a line an enemy goes through catches on its legs and stretches out
    /// after them like a rubber band. One running through on the ground is held back by it, and once the line is
    /// pulled out far enough (or has held it a moment) it trips: the legs are yanked back, the body goes on over
    /// them, and the line lets go and twangs back straight (R81). Anyone else of the other side (low enough in the
    /// air to catch it, down, or already tripped by it) only pulls it out until it slips off their legs.
    ///
    /// The trip is checked by hand from the feet each physics step, not from trigger callbacks: a knockdown
    /// from inside OnTrigger is what HANDOFF rule 14 forbids.
    /// </summary>
    public class SkillTripwire : MonoBehaviour
    {
        static readonly List<SkillTripwire> Live = new List<SkillTripwire>();
        static readonly BodyId[] Feet = { BodyId.FootL, BodyId.FootR };
        static readonly BodyId[] Legs = { BodyId.FootL, BodyId.FootR, BodyId.ThighL, BodyId.ThighR };

        RagdollPawn owner;
        int team;
        PawnRushSkillParams skills;
        Vector3 center, a1, b1, a2, b2;
        float age, height;
        bool armed;
        int trips;
        readonly HashSet<RagdollPawn> tripped = new HashSet<RagdollPawn>();
        LineRenderer line1, line2;
        readonly List<Transform> pegs = new List<Transform>();
        Vector3[] pegFoot;
        // Spent, the wire stays while its last line twangs, then it goes.
        bool spent;
        float snapAt = -1f;

        // How a line gives (R81, R82): it stretches up to MaxStretch after the legs it is caught on; a running enemy
        // trips once it is pulled TripStretch out or has been held TripHold; a line let go twangs for TwangTime.
        const float MaxStretch = 0.9f, TripStretch = 0.75f, TripHold = 0.35f, TwangTime = 0.5f;
        // The stretched line's pull on the legs it holds back (m/s² per metre stretched); the hips get most of it, so
        // a runner is slowed while the line stretches instead of tearing through it in a few frames.
        const float Spring = 55f, HipsShare = 0.7f;
        /// <summary>The line's own colour: white with a little of the bishop's violet, not the side's blue (R82; the
        /// aim lost its blue in R80).</summary>
        static readonly Color Wire = new Color(0.96f, 0.9f, 1f);
        // Anyone else pulls a line out only going through it at least this fast (a piece lying on it does not), and
        // not again for a moment after it slipped off them.
        const float PassSpeed = 0.8f, Rest = 0.6f;

        /// <summary>One line caught on someone's legs, or let go and twanging back.</summary>
        class Pull
        {
            public RagdollPawn by;      // the legs it is caught on (null once let go)
            public bool trip;           // caught by an enemy running on the ground: it trips when pulled out far enough
            public float along = 0.5f;  // where along the line the legs hold it, 0..1
            public Vector3 side;        // the way they go through it (flat, across the line)
            public float depth;         // how far past the straight line the legs are now (m)
            public float caughtAt, letGoAt = -10f, most, released;
        }

        readonly Pull[] pulls = { new Pull(), new Pull() };
        readonly Dictionary<RagdollPawn, float> restUntil = new Dictionary<RagdollPawn, float>();

        public Vector3 Center => center;
        public bool Armed => armed;
        /// <summary>Seconds until it goes by itself (design A blinks it three times in its last second).</summary>
        public float TimeLeft => skills != null ? skills.bishopArm + skills.bishopActive - age : 0f;
        /// <summary>The furthest a line of this wire has been pulled out so far (m; the probe reports it).</summary>
        public float MostStretch { get; private set; }
        /// <summary>How many times a line has been pulled out and let go, and how far the last one was (m).</summary>
        public int Pulls { get; private set; }
        public float LastPull { get; private set; }
        /// <summary>How long the last line let go had held the legs (s).</summary>
        public float LastHold { get; private set; }

        /// <summary>One of the two lines as it is drawn now (at shin height): its ends and the point it is pulled out
        /// at (by the legs it is caught on, or twanging back after it let go; R81, R82). Straight, the middle.</summary>
        public void LinePoints(int line, out Vector3 a, out Vector3 mid, out Vector3 b)
        {
            Vector3 up = Vector3.up * height;
            a = (line == 0 ? a1 : a2) + up;
            b = (line == 0 ? b1 : b2) + up;
            var p = pulls[line];
            mid = Vector3.Lerp(a, b, Bent(line) ? p.along : 0.5f) + p.side * Out(line);
        }

        /// <summary>How far a line is pulled out of straight now, 0..1 (the effects glow it harder).</summary>
        public float Tension(int line) => Mathf.Clamp01(Mathf.Abs(Out(line)) / 0.5f);

        bool Bent(int line) => pulls[line].by != null || age - pulls[line].letGoAt < TwangTime;

        /// <summary>How far a line is pulled out now (m): after the legs while caught; let go, it twangs back,
        /// overshooting and settling (negative while past straight on the other side).</summary>
        float Out(int line)
        {
            var p = pulls[line];
            if (p.by != null) return Mathf.Max(0f, p.depth);
            float u = (age - p.letGoAt) / TwangTime;
            if (u < 0f || u >= 1f) return 0f;
            return p.released * Mathf.Cos(u * Mathf.PI * 3f) * (1f - u) * (1f - u);
        }

        static Vector3 Flat3(Vector3 v) => new Vector3(v.x, 0f, v.z);

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
            Color c = Wire;
            line1 = SkillMarks.Line(transform, "Line 1");
            line2 = SkillMarks.Line(transform, "Line 2");
            pegFoot = new[] { a1, b1, a2, b2 };
            foreach (var p in pegFoot)
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
                // Stretched, a line thins a little, like rubber.
                if (Bent(line)) SkillMarks.Polyline(lr, new[] { a, mid, b }, color, width * (1f - 0.35f * Tension(line)));
                else SkillMarks.Segment(lr, a, b, color, width, false);
            }
            LeanPegs();
        }

        /// <summary>A line pulled out tugs the tops of its two pegs in toward the pull (R82).</summary>
        void LeanPegs()
        {
            for (int i = 0; i < pegs.Count; i++)
            {
                if (pegs[i] == null) continue;
                int line = i / 2;
                LinePoints(line, out var a, out var mid, out var b);
                Vector3 toward = Flat3(mid - (i % 2 == 0 ? a : b));
                float lean = 12f * Tension(line);
                var tilt = lean > 0.01f && toward.sqrMagnitude > 1e-4f
                    ? Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, toward.normalized))
                    : Quaternion.identity;
                pegs[i].rotation = tilt;
                pegs[i].position = pegFoot[i] + tilt * (Vector3.up * (height * 0.5f + 0.02f));
            }
        }

        void Update()
        {
            if (!armed)
            {
                // Drawn once at the start; nothing pulls it before it is armed.
                return;
            }
            // The glint running along the lines.
            float glint = 0.6f + 0.4f * Mathf.Sin(Time.time * 9f);
            Draw(new Color(Wire.r, Wire.g, Wire.b, glint), 0.035f);
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
            for (int line = 0; line < 2; line++)
                if (pulls[line].by != null && Hold(line)) return;   // that trip spent it
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null || pawn == owner || !Teams.AreEnemies(team, pawn.Team) || Holds(pawn)) continue;
                int line = LineUnderFoot(pawn);
                if (line < 0) continue;
                // Running on the ground into it for the first time: the trip. Jumping, flying or down: over it.
                bool runner = pawn.State == PawnState.Active && pawn.Grounded && !tripped.Contains(pawn);
                if (runner && pawn.Piece == PieceKind.Rook)
                {
                    Snap($"{pawn.DisplayName}(룩)이 걸리지 않고 끊음");
                    return;
                }
                if (pulls[line].by != null && pulls[1 - line].by == null && NearLine(pawn, 1 - line)) line = 1 - line;
                if (pulls[line].by != null)
                {
                    // Both lines already held by others: a runner still trips, with no stretch of its own.
                    if (runner && Trip(pawn, -1, RunDir(pawn))) return;
                    continue;
                }
                if (runner) Catch(line, pawn, true);
                else if (age - pulls[line].letGoAt >= TwangTime && Rested(pawn) && GoingThrough(pawn, line)) Catch(line, pawn, false);
            }
        }

        bool Holds(RagdollPawn pawn) => pulls[0].by == pawn || pulls[1].by == pawn;

        bool Rested(RagdollPawn pawn) => !restUntil.TryGetValue(pawn, out float until) || age >= until;

        /// <summary>A line caught on someone's legs: from now on it is drawn out after them (R82).</summary>
        void Catch(int line, RagdollPawn pawn, bool trip)
        {
            var p = pulls[line];
            Vector3 la = line == 0 ? a1 : a2, lb = line == 0 ? b1 : b2;
            Vector3 across = Vector3.Cross(Vector3.up, Flat3(lb - la)).normalized;
            float way = Vector3.Dot(Flat3(pawn.Hips.linearVelocity), across);
            // Standing in it: it gives towards the side the body is on.
            if (Mathf.Abs(way) < 0.3f) way = Vector3.Dot(Flat3(pawn.Hips.position - la), across);
            p.side = across * (way >= 0f ? 1f : -1f);
            p.by = pawn;
            p.trip = trip;
            p.caughtAt = age;
            p.most = 0f;
            Follow(line);
        }

        /// <summary>A caught line this step: it follows the legs out; a runner is held back by it and trips once it is
        /// pulled out far enough or held a moment; anyone else it slips off. True when that trip spent the wire.</summary>
        bool Hold(int line)
        {
            var p = pulls[line];
            var pawn = p.by;
            if (pawn == null)
            {
                LetGo(line);
                return false;
            }
            Follow(line);
            float held = age - p.caughtAt;
            if (p.trip)
            {
                // The stretched line holds the legs back, the body less, so it leans out over them.
                float pull = Spring * Mathf.Max(0f, p.depth) * Time.fixedDeltaTime;
                foreach (var id in Legs) pawn.bodies[(int)id].linearVelocity -= p.side * pull;
                pawn.Hips.linearVelocity -= p.side * (pull * HipsShare);
                if (p.depth >= TripStretch || held >= TripHold || pawn.State != PawnState.Active)
                    return Trip(pawn, line, p.side);
                return false;
            }
            // It slips off when pulled out as far as it goes, when the legs go back out or lift over it, or after a while.
            if (p.depth >= MaxStretch || p.depth < -0.2f || held > 1.5f || !LegsLow(pawn))
            {
                if (p.most > 0.2f) Report($"{pawn.DisplayName}이(가) 지나가며 줄이 {p.most:0.00} m 늘어났다 튕김 (안 걸림)");
                LetGo(line);
            }
            return false;
        }

        /// <summary>Where the legs hold a caught line now: along it at the foot that is furthest through, pulled out
        /// that far (never further than the line gives, and never right at a peg).</summary>
        void Follow(int line)
        {
            var p = pulls[line];
            Vector3 la = line == 0 ? a1 : a2, ab = Flat3((line == 0 ? b1 : b2) - la);
            float best = float.MinValue, along = p.along;
            foreach (var id in Feet)
            {
                Vector3 f = Flat3(p.by.bodies[(int)id].position - la);
                float d = Vector3.Dot(f, p.side);
                if (d <= best) continue;
                best = d;
                along = Vector3.Dot(f, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude);
            }
            p.along = Mathf.Clamp(along, 0.08f, 0.92f);
            p.depth = Mathf.Min(best, MaxStretch);
            p.most = Mathf.Max(p.most, p.depth);
            MostStretch = Mathf.Max(MostStretch, p.depth);
        }

        void LetGo(int line)
        {
            var p = pulls[line];
            if (p.by != null) restUntil[p.by] = age + Rest;
            Pulls++;
            LastPull = p.most;
            LastHold = age - p.caughtAt;
            p.released = Mathf.Max(0f, p.depth);
            p.by = null;
            p.letGoAt = age;
        }

        /// <summary>The trip (R81, R82): the legs are yanked back, the body goes on over them face first, and the line
        /// they pulled out lets go and twangs back with them. True when it was the wire's last trip.</summary>
        bool Trip(RagdollPawn pawn, int line, Vector3 runDir)
        {
            tripped.Add(pawn);
            Vector3 push = runDir * 1.6f + Vector3.up * 0.3f;
            string result = owner != null ? owner.SkillTrip(pawn, push) : "-";
            foreach (var id in Legs)
                pawn.bodies[(int)id].linearVelocity += -runDir * 4.5f + Vector3.down * 0.8f;
            float stretched = 0f, held = 0f;
            if (line >= 0)
            {
                stretched = pulls[line].most;
                held = age - pulls[line].caughtAt;
                LetGo(line);
                restUntil[pawn] = age + TwangTime + Rest;
            }
            trips++;
            Vector3 trippedAt = pawn.Hips.position;
            trippedAt.y = center.y;
            RagdollPawn.RaiseSkillFx(new SkillFxEvent { kind = SkillFxKind.BishopTrip, by = owner, target = pawn, at = trippedAt, dir = runDir, count = trips, source = this });
            Report($"{pawn.DisplayName} 걸림 ({trips}/{skills.bishopTrips}) → {result}, 줄이 {held:0.00}초 동안 {stretched:0.00} m 늘어났다 다리를 잡아채고 튕김");
            if (trips < skills.bishopTrips) return false;
            spent = true;
            snapAt = age + TwangTime;
            return true;
        }

        static Vector3 RunDir(RagdollPawn pawn)
        {
            Vector3 run = Flat3(pawn.Hips.linearVelocity);
            return run.sqrMagnitude > 0.25f ? run.normalized : Vector3.forward;
        }

        /// <summary>The line a foot is on (at shin height or below, within 0.18 m of it), the nearer one at the
        /// crossing; -1 for none.</summary>
        int LineUnderFoot(RagdollPawn pawn)
        {
            int found = -1;
            float best = 0.18f;
            foreach (var id in Feet)
            {
                Vector3 foot = pawn.bodies[(int)id].position;
                if (!FootLow(foot)) continue;
                Vector2 f = Flat(foot);
                float d1 = SegmentDistance(f, Flat(a1), Flat(b1)), d2 = SegmentDistance(f, Flat(a2), Flat(b2));
                if (d1 < best) { best = d1; found = 0; }
                if (d2 < best) { best = d2; found = 1; }
            }
            return found;
        }

        bool NearLine(RagdollPawn pawn, int line)
        {
            Vector3 la = line == 0 ? a1 : a2, lb = line == 0 ? b1 : b2;
            foreach (var id in Feet)
            {
                Vector3 foot = pawn.bodies[(int)id].position;
                if (FootLow(foot) && SegmentDistance(Flat(foot), Flat(la), Flat(lb)) < 0.18f) return true;
            }
            return false;
        }

        bool FootLow(Vector3 foot) => foot.y <= center.y + height + 0.12f && foot.y >= center.y - 0.4f;

        bool LegsLow(RagdollPawn pawn) => FootLow(pawn.bodies[(int)BodyId.FootL].position) || FootLow(pawn.bodies[(int)BodyId.FootR].position);

        /// <summary>Feet moving across the line fast enough to pull it out (not a piece lying still on it).</summary>
        bool GoingThrough(RagdollPawn pawn, int line)
        {
            Vector3 la = line == 0 ? a1 : a2, lb = line == 0 ? b1 : b2;
            Vector3 across = Vector3.Cross(Vector3.up, Flat3(lb - la)).normalized;
            float fastest = 0f;
            foreach (var id in Feet)
                fastest = Mathf.Max(fastest, Mathf.Abs(Vector3.Dot(pawn.bodies[(int)id].linearVelocity, across)));
            return fastest >= PassSpeed;
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
