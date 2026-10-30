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
    ///
    /// R138 (승규 님 10-14: "비숍이 스킬을 사용 할 때 밧줄에 걸려서 넘어 지는 게 아니라 밧줄에 묶이게 해줘 (실제 밧줄에 묶이는게 잘
    /// 보여야 돼)"): the trip is a tie now. Where it tripped, the runner is tied up standing (RagdollPawn.SkillBind,
    /// bishopBind seconds): the line that caught it, and the other one if it holds no one, close on its waist from all four
    /// pegs and stay there while it is bound (the effects wind the rope round it), then let go and twang back. The wire
    /// stays until the last tie is let go, even past its time.
    /// </summary>
    public class SkillTripwire : MonoBehaviour
    {
        static readonly List<SkillTripwire> Live = new List<SkillTripwire>();
        static readonly BodyId[] Feet = { BodyId.FootL, BodyId.FootR };
        static readonly BodyId[] Legs = { BodyId.FootL, BodyId.FootR, BodyId.ThighL, BodyId.ThighR };
        static ushort nextNetId = 1;

        RagdollPawn owner;
        int team;
        PawnRushSkillParams skills;
        Vector3 center, a1, b1, a2, b2, forwardDir = Vector3.forward;
        float age, height;
        bool armed;
        int trips;
        // R153: the thin thread from the nearest peg to the bishop's hand (its leash). Not drawn since 10-25 (ShowLeash).
        LineRenderer leash;
        static readonly bool ShowLeash = false;

        /// <summary>How far the bishop is from the wire's middle, across the floor (R153: past bishopLeash the wire goes).</summary>
        public float LeashDistance => owner != null ? Vector2.Distance(Flat(owner.Hips.position), Flat(center)) : float.MaxValue;

        // ---- online (R111): the host's wires are sent whole in every skills packet; a client draws its copy of each
        // (Remote: no trips judged here, the lines bent where the host's are).

        /// <summary>Every wire laid now (the host sends them; a new host takes them over).</summary>
        public static IReadOnlyList<SkillTripwire> LiveWires => Live;
        /// <summary>The number the network knows this wire by (its moments point at it).</summary>
        public ushort NetId { get; private set; }
        /// <summary>A client's copy of the host's wire: drawn from the host's state, judges nothing.</summary>
        public bool Remote { get; private set; }
        /// <summary>A client has shown the wire's landing effect (its BishopWire moment, or one made up for a late joiner).</summary>
        public bool NetFxShown { get; set; }
        public RagdollPawn Owner => owner;
        public Vector3 Forward => forwardDir;
        public float LineLength => skills != null ? skills.bishopLineLength : Vector3.Distance(a1, b1);
        public float Age => age;
        readonly float[] netAlong = { 0.5f, 0.5f }, netOut = { 0f, 0f };
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
            public bool bind;           // R138: tied round a bound piece's waist until it is free
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
            var tied = BoundTo(line);
            if (tied != null)
            {
                // R138: tied round a bound piece: both halves run from their pegs up to its waist.
                mid = Waist(tied);
                return;
            }
            if (Remote)
            {
                mid = Vector3.Lerp(a, b, netAlong[line]) + Across(line) * netOut[line];
                return;
            }
            var p = pulls[line];
            mid = Vector3.Lerp(a, b, Bent(line) ? p.along : 0.5f) + p.side * Out(line);
        }

        /// <summary>How far a line is pulled out of straight now, 0..1 (the effects glow it harder). Tied round a piece: taut.</summary>
        public float Tension(int line) => BoundTo(line) != null ? 1f : Mathf.Clamp01(Mathf.Abs(Remote ? netOut[line] : Out(line)) / 0.5f);

        bool Bent(int line) => BoundTo(line) != null || (Remote ? Mathf.Abs(netOut[line]) > 0.005f : pulls[line].by != null || age - pulls[line].letGoAt < TwangTime);

        // R138: a client's copy is told of a tie by the host's moment (BishopTrip): both lines close on the piece until
        // its time is up or it is knocked down (the host's lines do the same).
        readonly RagdollPawn[] netBind = new RagdollPawn[2];
        readonly float[] netBindUntil = new float[2];

        /// <summary>The piece a line is tied round now (R138), or null.</summary>
        public RagdollPawn BoundTo(int line)
        {
            if (Remote)
            {
                var n = netBind[line];
                return n != null && n.State == PawnState.Active && age < netBindUntil[line] ? n : null;
            }
            var p = pulls[line];
            return p.bind && p.by != null && p.by.SkillBound ? p.by : null;
        }

        /// <summary>Where the rope closes on a tied piece: its waist.</summary>
        public static Vector3 Waist(RagdollPawn pawn) => pawn.Hips.position + Vector3.up * 0.04f;

        /// <summary>A client's copy (R138): the host tied <paramref name="pawn"/> for <paramref name="seconds"/>; both lines close
        /// on it (the one already tied round someone else stays).</summary>
        public void NetBind(RagdollPawn pawn, float seconds)
        {
            if (!Remote || pawn == null) return;
            for (int line = 0; line < 2; line++)
            {
                if (BoundTo(line) != null && netBind[line] != pawn) continue;
                netBind[line] = pawn;
                netBindUntil[line] = age + seconds;
            }
        }

        /// <summary>Square to a line, flat: the way "pulled out" is measured on the wire.</summary>
        Vector3 Across(int line)
        {
            Vector3 la = line == 0 ? a1 : a2, lb = line == 0 ? b1 : b2;
            Vector3 d = Flat3(lb - la);
            return d.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, d).normalized : Vector3.right;
        }

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
            Build(by, at, forward, s);
            NetId = nextNetId;
            nextNetId = (ushort)(nextNetId == ushort.MaxValue ? 1 : nextNetId + 1);
            Live.Add(this);
            RagdollPawn.RaiseSkillFx(new SkillFxEvent { kind = SkillFxKind.BishopWire, by = by, at = at, dir = forwardDir, size = s.bishopLineLength, source = this });
        }

        /// <summary>A client's copy of a wire the host laid (R111): the same lines and pegs, drawn from the host's state.
        /// Its landing effect comes with the host's moment (or the link makes one up for a late joiner).</summary>
        public static SkillTripwire SpawnRemote(RagdollPawn owner, ushort id, Vector3 at, Vector3 forward, PawnRushSkillParams skills)
        {
            var go = new GameObject($"{(owner != null ? owner.DisplayName : "?")} 교차 밧줄 (방장)");
            var wire = go.AddComponent<SkillTripwire>();
            wire.Remote = true;
            wire.NetId = id;
            wire.Build(owner, at, forward, skills);
            Live.Add(wire);
            return wire;
        }

        /// <summary>The whole wire for the skills packet (the host).</summary>
        public void CaptureNet(ref SkillWireObject o, System.Func<RagdollPawn, byte> refOf)
        {
            o.Id = NetId;
            o.Owner = owner != null ? refOf(owner) : SkillWire.None;
            o.X = center.x; o.Y = center.y; o.Z = center.z;
            o.Yaw = Mathf.Atan2(forwardDir.x, forwardDir.z) * Mathf.Rad2Deg;
            o.Age = age;
            o.Flags = (byte)((armed ? 1 : 0) | (spent ? 2 : 0));
            o.Trips = (byte)Mathf.Clamp(trips, 0, 255);
            for (int line = 0; line < 2; line++)
            {
                float along = Bent(line) ? pulls[line].along : 0.5f;
                float signed = Out(line) * (Vector3.Dot(pulls[line].side, Across(line)) >= 0f ? 1f : -1f);
                if (line == 0) { o.Along0 = along; o.Out0 = signed; }
                else { o.Along1 = along; o.Out1 = signed; }
            }
        }

        /// <summary>The host's state of this wire (a client's copy).</summary>
        public void ApplyNet(in SkillWireObject o)
        {
            age = o.Age;
            armed = (o.Flags & 1) != 0;
            spent = (o.Flags & 2) != 0;
            trips = o.Trips;
            netAlong[0] = o.Along0;
            netAlong[1] = o.Along1;
            netOut[0] = o.Out0;
            netOut[1] = o.Out1;
        }

        /// <summary>This PC became the host (D-S6): its copy of the wire is the wire from now on, still armed, still
        /// counting its trips and its time.</summary>
        public void Adopt()
        {
            if (!Remote) return;
            Remote = false;
            if (owner != null) team = owner.Team;
            for (int line = 0; line < 2; line++)
            {
                pulls[line].by = null;
                pulls[line].letGoAt = -10f;
                pulls[line].released = 0f;
            }
        }

        /// <summary>This PC stopped hosting: what it had laid is now drawn from the new host's packets.</summary>
        public void Yield()
        {
            Remote = true;
            NetFxShown = true;
        }

        /// <summary>Gone on the host (a client's copy goes with it).</summary>
        public void RemoveRemote() => Destroy(gameObject);

        /// <summary>A new host's own wires get numbers past the ones it took over (D-S6), so no two wires share one.</summary>
        public static void ReserveIds(ushort upTo)
        {
            if (upTo >= nextNetId && upTo < ushort.MaxValue) nextNetId = (ushort)(upTo + 1);
        }

        void Build(RagdollPawn by, Vector3 at, Vector3 forward, PawnRushSkillParams s)
        {
            owner = by;
            team = by != null ? by.Team : Teams.None;
            skills = s;
            center = at;
            height = s.bishopHeight;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            forwardDir = forward;
            float half = s.bishopLineLength * 0.5f;
            Vector3 d1 = Quaternion.Euler(0f, 45f, 0f) * forward, d2 = Quaternion.Euler(0f, -45f, 0f) * forward;
            a1 = at - d1 * half; b1 = at + d1 * half;
            a2 = at - d2 * half; b2 = at + d2 * half;
            // R149: each peg stands on the floor under it (up or down a step within 1 m): laid flat at the bishop's own
            // floor, the pegs across a mini-game station's dais edge stood inside the dais. The trips only use the wire's
            // flat positions and its middle's height, so this moves only what is drawn (and the same on every PC).
            a1 = Seat(a1); b1 = Seat(b1); a2 = Seat(a2); b2 = Seat(b2);
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
        }

        static Vector3 Seat(Vector3 p) =>
            SkillInkKit.FloorUnder(p, 1f, out Vector3 floor) && Mathf.Abs(floor.y - p.y) <= 1f ? floor : p;

        void OnDestroy() => Live.Remove(this);

        /// <summary>R139: the effects draw the two lines themselves (as rope), so its own thin lines stay hidden; its pegs
        /// still lean.</summary>
        public bool LinesDrawnElsewhere { get; set; }

        /// <summary>R142: the effects draw the pegs too (as wooden stakes), so its own stay hidden (they still lean: where the
        /// lines are tied, <see cref="PegTie"/>).</summary>
        public bool PegsDrawnElsewhere
        {
            get => pegsElsewhere;
            set
            {
                if (pegsElsewhere == value) return;
                pegsElsewhere = value;
                foreach (var peg in pegs)
                {
                    var r = peg != null ? peg.GetComponent<MeshRenderer>() : null;
                    if (r != null) r.enabled = !value;
                }
            }
        }
        bool pegsElsewhere;

        /// <summary>R142: where a line's peg stands on the floor (<paramref name="end"/> 0 = its a end, 1 = its b end).</summary>
        public Vector3 PegFoot(int line, int end)
        {
            int i = line * 2 + end;
            if (pegFoot != null && i < pegFoot.Length) return pegFoot[i];
            LinePoints(line, out var a, out _, out var b);
            return (end == 0 ? a : b) - Vector3.up * height;
        }

        void Draw(Color color, float width)
        {
            for (int line = 0; line < 2; line++)
            {
                LinePoints(line, out var a, out var mid, out var b);
                var lr = line == 0 ? line1 : line2;
                if (LinesDrawnElsewhere)
                {
                    SkillMarks.Hide(lr);
                    continue;
                }
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
                var tilt = PegTilt(i);
                pegs[i].rotation = tilt;
                pegs[i].position = pegFoot[i] + tilt * (Vector3.up * (height * 0.5f + 0.02f));
            }
        }

        /// <summary>How peg <paramref name="i"/> (line × 2 + end) leans now: in toward where its line is pulled.</summary>
        Quaternion PegTilt(int i)
        {
            int line = i / 2;
            LinePoints(line, out var a, out var mid, out var b);
            Vector3 toward = Flat3(mid - (i % 2 == 0 ? a : b));
            float lean = 12f * Tension(line);
            return lean > 0.01f && toward.sqrMagnitude > 1e-4f
                ? Quaternion.AngleAxis(lean, Vector3.Cross(Vector3.up, toward.normalized))
                : Quaternion.identity;
        }

        /// <summary>R139: where a line is tied on to its peg (<paramref name="end"/> 0 = its a end, 1 = its b end), leaning with
        /// the peg, and the peg's lean (the effects' rope and its knot follow the peg).</summary>
        public Vector3 PegTie(int line, int end, out Quaternion lean)
        {
            int i = line * 2 + end;
            if (pegFoot == null || i >= pegFoot.Length)
            {
                LinePoints(line, out var a, out _, out var b);
                lean = Quaternion.identity;
                return end == 0 ? a : b;
            }
            lean = PegTilt(i);
            return pegFoot[i] + lean * (Vector3.up * height);
        }

        void Update()
        {
            DrawLeash();
            if (LinesDrawnElsewhere)
            {
                SkillMarks.Hide(line1);
                SkillMarks.Hide(line2);
                if (armed) LeanPegs();
                return;
            }
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
            if (Remote) return;   // a client's copy: the host judges the trips (and its packets set the age)
            if (spent)
            {
                // R138: its last ties held until their pieces are free, then the twang, then it goes.
                if (HoldTies()) return;
                if (snapAt < 0f) snapAt = age + TwangTime;
                if (age >= snapAt) Snap("끊어짐");
                return;
            }
            // R153 (skill plan v0.3 §6): the trap holds only while its bishop guards it: the bishop more than bishopLeash from
            // its middle, or knocked down, and its rope comes loose and goes.
            if (owner == null || owner.State == PawnState.Ragdoll || LeashDistance > skills.bishopLeash)
            {
                Snap(owner == null ? "비숍이 없음 (사라짐)" : owner.State == PawnState.Ragdoll ? "비숍이 넘어져 풀림" : $"비숍이 {skills.bishopLeash:0} m 넘게 멀어져 풀림");
                return;
            }
            if (!armed && age >= skills.bishopArm)
            {
                armed = true;
                Report("무장 (\"팅\")");
            }
            if (age >= skills.bishopArm + skills.bishopActive)
            {
                // R138: not while a piece is still tied in it.
                if (HoldTies()) return;
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
                bool runner = pawn.State == PawnState.Active && pawn.Grounded && !tripped.Contains(pawn) && !pawn.SkillUntargetable;
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

        /// <summary>R138: the lines tied round a bound piece this step (let go once it is free). True while any still holds.</summary>
        bool HoldTies()
        {
            bool tying = false;
            for (int line = 0; line < 2; line++)
                if (pulls[line].bind && pulls[line].by != null)
                {
                    Hold(line);
                    tying |= pulls[line].bind;
                }
            return tying;
        }

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
            if (p.bind)
            {
                // R138: tied round a bound piece's waist until it is free (its time, a knockdown), then it twangs back.
                if (!pawn.SkillBound)
                {
                    p.bind = false;
                    restUntil[pawn] = age + TwangTime + Rest;
                    LetGo(line);
                    return false;
                }
                Follow(line);
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
            p.bind = false;
            p.letGoAt = age;
        }

        /// <summary>The catch (R81, R82; R138 a tie, not a trip): the runner is tied up where it stands for bishopBind seconds,
        /// and the line that caught it closes on its waist with the other one (if that holds no one), from all four pegs;
        /// they let go and twang back when it is free. One that cannot be tied (down, just up) is let go. True when it was the
        /// wire's last catch.</summary>
        bool Trip(RagdollPawn pawn, int line, Vector3 runDir)
        {
            tripped.Add(pawn);
            float seconds = skills.bishopBind;
            string result = owner != null ? owner.SkillBind(pawn, seconds, "교차 밧줄") : "-";
            bool tied = pawn.SkillBound;
            float stretched = 0f, held = 0f;
            if (line >= 0)
            {
                stretched = pulls[line].most;
                held = age - pulls[line].caughtAt;
            }
            if (tied)
            {
                for (int l = 0; l < 2; l++)
                {
                    var p = pulls[l];
                    if (l != line && p.by != null) continue;   // the other line holds someone else: it stays theirs
                    if (p.by == null)
                    {
                        p.side = Across(l);
                        if (Vector3.Dot(Flat3(pawn.Hips.position - (l == 0 ? a1 : a2)), p.side) < 0f) p.side = -p.side;
                        p.along = 0.5f;
                        p.most = 0f;
                    }
                    p.by = pawn;
                    p.trip = false;
                    p.bind = true;
                    p.caughtAt = age;
                    Follow(l);
                }
            }
            else if (line >= 0)
            {
                LetGo(line);
                restUntil[pawn] = age + TwangTime + Rest;
            }
            trips++;
            Vector3 trippedAt = pawn.Hips.position;
            trippedAt.y = center.y;
            RagdollPawn.RaiseSkillFx(new SkillFxEvent { kind = SkillFxKind.BishopTrip, by = owner, target = pawn, at = trippedAt, dir = runDir, count = trips, size = tied ? seconds : 0f, source = this });
            Report($"{pawn.DisplayName} 걸림 ({trips}/{skills.bishopTrips}) → {result}, 줄이 {held:0.00}초 동안 {stretched:0.00} m 늘어났다 " + (tied ? "몸을 감아 묶음" : "튕김"));
            if (trips < skills.bishopTrips) return false;
            spent = true;
            snapAt = -1f;   // once its last tie is let go (FixedUpdate)
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
            // R138: a wire that goes (broken by a blast or a landing, replaced) frees whoever it still has tied.
            if (!Remote)
                for (int line = 0; line < 2; line++)
                    if (pulls[line].bind && pulls[line].by != null) pulls[line].by.SkillUnbind();
            Destroy(gameObject);
        }

        void Report(string text) => PawnRushSkillBed.Report($"비숍 밧줄: {text}");

        /// <summary>
        /// R153: the leash, a thin hemp thread from the peg nearest the bishop to its hand. Slack it sags; past
        /// bishopLeashWarn it is drawn straight and taut, flickering brighter the nearer the bishop is to letting it go.
        /// </summary>
        void DrawLeash()
        {
            // 10-25 승규 님 (picture of the thread running into the bishop's hand: "비숍 스킬 쓸 때 자기손에 들어 있는 줄은 빼줘"): the
            // thread is not drawn any more; the leash itself (6 m, SkillTripwire.FixedUpdate) still holds.
            SkillMarks.Hide(leash);
            if (ShowLeash == false) return;
            if (owner == null || skills == null || owner.State == PawnState.Ragdoll)
            {
                SkillMarks.Hide(leash);
                return;
            }
            if (leash == null) leash = SkillMarks.Line(transform, "Leash");
            Vector3 hand = owner.bodies[(int)BodyId.HandR].position;
            Vector3 tie = PegTie(0, 0, out _);
            float bestD = float.MaxValue;
            for (int line = 0; line < 2; line++)
                for (int end = 0; end < 2; end++)
                {
                    var t = PegTie(line, end, out _);
                    float d = (t - hand).sqrMagnitude;
                    if (d < bestD) { bestD = d; tie = t; }
                }
            float dist = LeashDistance;
            float taut = Mathf.InverseLerp(skills.bishopLeashWarn, skills.bishopLeash, dist);
            // Hemp, like the trap's rope (an undyed white thread was lost on the light floor).
            var thread = new Color(0.62f, 0.48f, 0.3f, 0.95f);
            if (dist <= skills.bishopLeashWarn)
            {
                // Slack: a low curve between the peg and the hand.
                const int N = 10;
                var pts = new Vector3[N + 1];
                float span = Vector3.Distance(tie, hand);
                float sag = Mathf.Lerp(0.6f, 0.05f, Mathf.Clamp01(dist / Mathf.Max(0.1f, skills.bishopLeashWarn))) * Mathf.Min(1f, span / 3f);
                for (int i = 0; i <= N; i++)
                {
                    float u = i / (float)N;
                    pts[i] = Vector3.Lerp(tie, hand, u) + Vector3.down * (4f * sag * u * (1f - u));
                }
                SkillMarks.Polyline(leash, pts, thread, 0.022f);
                return;
            }
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(10f, 26f, taut));
            var hot = Color.Lerp(thread, new Color(1f, 0.93f, 0.7f, 1f), blink);
            SkillMarks.Segment(leash, tie, hand, hot, Mathf.Lerp(0.022f, 0.016f, taut), false);
        }

        static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
