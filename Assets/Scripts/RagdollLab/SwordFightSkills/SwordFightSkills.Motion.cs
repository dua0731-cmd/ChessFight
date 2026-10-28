using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 10-26 (승규 님: "기물이 스킬 쓸 때 연출과 모션이 필요하다고 하면 추가해도 돼"): the pieces' own bodies through their skills,
    /// so a skill reads as the piece doing it - set before the release, the release, the blow held, the follow-through,
    /// the settle - and not only as effects appearing. The king, the queen and the knight so far.
    /// <list type="bullet">
    /// <item>Everything that moves a body is the host's: joint targets through <see cref="SkillBodyPose"/> (asked by
    /// SwordFightPawn.SwordPose, RagdollPawn.PoseOverride) and the whole body through RagdollPawn.SkillTurn / SkillLean /
    /// SkillStance and the skills' own Fly. Other machines see it in the bodies they are sent: nothing new goes on the wire.
    /// Only the drawn free arm's share (<see cref="LeftArmPosed"/>) is worked out on every machine, from replicated state.</item>
    /// <item>A pose is blended in from the puppet's own and let go over a moment, joint targets turn at most about 25 rad/s
    /// (the drives' feed-forward stops at 30), and grounded legs never split further than the drawn feet allow (they cannot
    /// rise into the piece's base, RagdollVisualSync), so nothing snaps, kicks the floor or sinks.</item>
    /// </list>
    /// </summary>
    public partial class SwordFightSkills
    {
        /// <summary>The body motions on (off: the skills as they were, to compare).</summary>
        public static bool BodyMotion = true;

        /// <summary>One moment of a skill's body. Angles in degrees.</summary>
        struct BodyKey
        {
            /// <summary>Weight of the chest, head, legs and whole-body hold against the puppet's own pose (0 = the puppet's).</summary>
            public float w;
            /// <summary>The chest in the hips' frame (Euler: +x bends forward, +y takes the sword shoulder back, +z raises it).</summary>
            public Vector3 chest;
            /// <summary>The head in the chest's frame (+x nods down, +y turns right); unused while <see cref="look"/> is set.</summary>
            public Vector3 head;
            /// <summary>A world point the drawn head turns to (zero: <see cref="head"/>), nodding <see cref="nod"/> more.</summary>
            public Vector3 look;
            public float nod;
            /// <summary>The free (left) arm: a direction in the chest's frame (x right, y up, z ahead) and its weight.</summary>
            public Vector3 armL;
            public float armW;
            /// <summary>The sword arm off the blade's line (chest frame) and its weight (0: along the blade).</summary>
            public Vector3 armR;
            public float swordW;
            /// <summary>The legs: thigh pitch (+ swings the foot back), splay out, extra foot pitch. Off: the gait's own.</summary>
            public bool legs;
            public float thighL, thighR, splay, foot;
            /// <summary>The whole body (grounded): turn (+ toward the sword hand) and lean (+ forward).</summary>
            public float turn, lean;
        }

        BodyKey bodyKey, lastBodyKey;
        bool bodyOn;
        float bodyFade, bodyRamp;
        float struckLeft;
        readonly Quaternion[] struckPose = new Quaternion[RagdollPawn.Count];

        static readonly Vector3 ArmRest = new Vector3(-0.94f, -0.34f, 0f);

        static float Out2(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        static float In2(float t) { t = Mathf.Clamp01(t); return t * t; }
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        static float Sm(float a, float b, float x) => Smooth(Mathf.InverseLerp(a, b, x));

        static Vector3 SlerpDir(Vector3 a, Vector3 b, float k)
        {
            if (a == Vector3.zero) return b;
            if (b == Vector3.zero) return a;
            return Vector3.Slerp(a.normalized, b.normalized, k);
        }

        static BodyKey Mix(in BodyKey a, in BodyKey b, float k)
        {
            k = Mathf.Clamp01(k);
            var m = new BodyKey
            {
                w = Mathf.Lerp(a.w, b.w, k),
                chest = Vector3.Lerp(a.chest, b.chest, k),
                head = Vector3.Lerp(a.head, b.head, k),
                look = a.look == Vector3.zero ? b.look : b.look == Vector3.zero ? a.look : Vector3.Lerp(a.look, b.look, k),
                nod = Mathf.Lerp(a.nod, b.nod, k),
                armL = SlerpDir(a.armL, b.armL, k),
                armW = Mathf.Lerp(a.armW, b.armW, k),
                armR = SlerpDir(a.armR, b.armR, k),
                swordW = Mathf.Lerp(a.swordW, b.swordW, k),
                turn = Mathf.Lerp(a.turn, b.turn, k),
                lean = Mathf.Lerp(a.lean, b.lean, k),
                legs = a.legs || b.legs,
            };
            var la = a.legs ? a : b;
            var lb = b.legs ? b : a;
            m.thighL = Mathf.Lerp(la.thighL, lb.thighL, k);
            m.thighR = Mathf.Lerp(la.thighR, lb.thighR, k);
            m.splay = Mathf.Lerp(la.splay, lb.splay, k);
            m.foot = Mathf.Lerp(la.foot, lb.foot, k);
            return m;
        }

        static BodyKey Key(float chestX, float headX, float thighL, float thighR, Vector3 armL, float splay = 0f, float foot = 0f) => new BodyKey
        {
            w = 1f, chest = new Vector3(chestX, 0f, 0f), head = new Vector3(headX, 0f, 0f), armL = armL.normalized, armW = 1f,
            legs = true, thighL = thighL, thighR = thighR, splay = splay, foot = foot,
        };

        static BodyKey Upper(float turn, float lean, Vector3 chest, Vector3 head) => new BodyKey { w = 1f, turn = turn, lean = lean, chest = chest, head = head };

        // ---------------------------------------------------------------- the step (host)

        void FixedUpdate()
        {
            StepSkills();
            if (!NetPuppet) StepBodyMotion(Time.fixedDeltaTime);
        }

        void StepBodyMotion(float dt)
        {
            if (Pawn == null) return;
            struckLeft -= dt;
            bool active = Pawn.State == PawnState.Active;
            // Out of a knock or a get-up it comes in over a moment, not at once.
            bodyRamp = active ? Mathf.MoveTowards(bodyRamp, 1f, dt / 0.08f) : 0f;
            BodyKey k = default;
            bool on = BodyMotion && active && S != null && Fighter != null && Fighter.Alive && Fighter.Authority && BuildKey(out k);
            if (on)
            {
                k.w *= bodyRamp;
                k.armW *= bodyRamp;
                bodyKey = lastBodyKey = k;
                bodyFade = 0.12f;
            }
            else if (bodyFade > 0f && active)
            {
                // A skill cut short or ended on a pose lets the pose go over a moment instead of snapping (split legs
                // snapped back kick the floor).
                bodyFade -= dt;
                float f = Mathf.Clamp01(bodyFade / 0.12f);
                bodyKey = lastBodyKey;
                bodyKey.w *= f;
                bodyKey.armW *= f;
                bodyKey.swordW = 0f;
                on = bodyFade > 0f;
            }
            else bodyFade = 0f;
            bodyOn = on;
            Pawn.SkillTurn = on ? bodyKey.turn * bodyKey.w : 0f;
            Pawn.SkillLean = on ? bodyKey.lean * bodyKey.w : 0f;
            Pawn.SkillStance = on && bodyKey.legs && Pawn.Grounded
                ? StanceDrop(Mathf.Max(Mathf.Abs(GroundThigh(bodyKey.thighL)), Mathf.Abs(GroundThigh(bodyKey.thighR))) * bodyKey.w) : 0f;
        }

        bool BuildKey(out BodyKey k)
        {
            k = default;
            switch (Piece)
            {
                case PieceKind.King: return EdgeStage != SfEdge.None ? KingEdgeKey(ref k) : KingKey(ref k);
                case PieceKind.Queen: return QueenKey(ref k);
                case PieceKind.Rook: return EdgeStage != SfEdge.None ? RookEdgeKey(ref k) : RookKey(ref k);
                case PieceKind.Bishop: return EdgeStage != SfEdge.None ? BishopEdgeKey(ref k) : BishopKey(ref k);
                case PieceKind.Knight: return EdgeStage != SfEdge.None ? KnightEdgeKey(ref k) : KnightKey(ref k);
            }
            return false;
        }

        /// <summary>How far the hips come down for legs split <paramref name="split"/> degrees (a straight leg swung out
        /// reaches less far down).</summary>
        float StanceDrop(float split) => Pawn.LegReach * (1f - Mathf.Cos(Mathf.Clamp(split, 0f, 45f) * Mathf.Deg2Rad));

        /// <summary>The widest a grounded leg may swing: the drawn feet may rise no more than a couple of centimetres into
        /// the piece's base (RagdollVisualSync.PieceFootLift), so the hips may come down only ~3.5 cm for it.</summary>
        float MaxGroundSplit => Pawn != null && Pawn.LegReach > 0.05f ? Mathf.Acos(1f - Mathf.Min(0.035f, Pawn.LegReach * 0.9f) / Pawn.LegReach) * Mathf.Rad2Deg : 25f;

        float GroundThigh(float x) => Pawn != null && Pawn.Grounded ? Mathf.Clamp(x, -MaxGroundSplit, MaxGroundSplit) : Mathf.Clamp(x, -48f, 50f);

        // ---------------------------------------------------------------- what the drives are asked (host)

        /// <summary>The part's local rotation in its parent body for the skill's motion now (host only), or null for the
        /// puppet's own. A piece the queen's edge cut went through is held as it stood (<see cref="HoldStruck"/>).</summary>
        public Quaternion? SkillBodyPose(int part)
        {
            if (NetPuppet || Pawn == null) return null;
            if (struckLeft > 0f && Pawn.State == PawnState.Active && part >= (int)BodyId.Chest && part <= (int)BodyId.HandR)
                return struckPose[part];
            if (!bodyOn || Pawn.State != PawnState.Active) return null;
            var k = bodyKey;
            switch ((BodyId)part)
            {
                case BodyId.Chest:
                    if (k.w <= 0f) return null;
                    return Blend(part, Quaternion.Euler(Mathf.Clamp(k.chest.x, -28f, 28f), Mathf.Clamp(k.chest.y, -30f, 30f), Mathf.Clamp(k.chest.z, -28f, 28f)), k.w);
                case BodyId.Head:
                    if (k.w <= 0f) return null;
                    return Blend(part, k.look != Vector3.zero ? HeadAt(k.look, k.nod)
                        : Quaternion.Euler(Mathf.Clamp(k.head.x, -38f, 38f), Mathf.Clamp(k.head.y, -42f, 42f), Mathf.Clamp(k.head.z, -30f, 30f)), k.w);
                case BodyId.ArmR:
                    // An empty sword hand (the bishop's sword is thrown) follows the motion here; one holding the sword is
                    // aimed by the sword's own pose (SwordFightPawn.SwordPose, SkillSwordArm).
                    if (k.swordW <= 0f || k.armR == Vector3.zero || Blade != null) return null;
                    return Blend(part, Quaternion.FromToRotation(Vector3.right, k.armR.normalized), k.swordW);
                case BodyId.ArmL:
                {
                    if (k.armW <= 0f || k.armL == Vector3.zero || CarriedByBishop) return null;
                    // Never in across the face (the stub has no elbow).
                    Vector3 a = k.armL.normalized;
                    a.x = Mathf.Min(a.x, 0.3f);
                    return Blend(part, Quaternion.FromToRotation(Vector3.left, a.normalized), k.armW);
                }
                case BodyId.ThighL:
                    return k.legs && k.w > 0f ? Blend(part, Thigh(GroundThigh(k.thighL), -k.splay), k.w) : (Quaternion?)null;
                case BodyId.ThighR:
                    return k.legs && k.w > 0f ? Blend(part, Thigh(GroundThigh(k.thighR), k.splay), k.w) : (Quaternion?)null;
                case BodyId.FootL:
                    return k.legs && k.w > 0f ? Blend(part, Foot(BodyId.ThighL, Thigh(GroundThigh(k.thighL), -k.splay), k.foot), k.w) : (Quaternion?)null;
                case BodyId.FootR:
                    return k.legs && k.w > 0f ? Blend(part, Foot(BodyId.ThighR, Thigh(GroundThigh(k.thighR), k.splay), k.foot), k.w) : (Quaternion?)null;
            }
            return null;
        }

        /// <summary>The sword arm's own aim in the chest's frame while the skill's motion takes it off the blade's line.</summary>
        public bool SkillSwordArm(out Vector3 arm, out float w)
        {
            arm = bodyKey.armR;
            w = bodyOn && !NetPuppet && Pawn != null && Pawn.State == PawnState.Active ? Mathf.Clamp01(bodyKey.swordW) : 0f;
            return w > 0f && arm != Vector3.zero;
        }

        Quaternion Blend(int part, Quaternion want, float w) =>
            w >= 0.999f ? want : Quaternion.Slerp(Pawn.puppet[part].localRotation, want, Mathf.Clamp01(w));

        static Quaternion Thigh(float x, float z) => Quaternion.Euler(x, 0f, z);

        /// <summary>A foot's sole: flat on the board while standing (whatever the hips' lean), level with the hips in the air.</summary>
        Quaternion Foot(BodyId thighId, Quaternion thighLocal, float extra)
        {
            Vector3 ahead = Flat(Pawn.Hips.transform.forward);
            Quaternion want = Pawn.Grounded
                ? Quaternion.Inverse(Pawn.bodies[(int)thighId].rotation) * Quaternion.LookRotation(ahead.sqrMagnitude > 1e-4f ? ahead : Vector3.forward, Vector3.up) * Quaternion.Euler(extra, 0f, 0f)
                : Quaternion.Inverse(thighLocal) * Quaternion.Euler(extra, 0f, 0f);
            return Quaternion.RotateTowards(Quaternion.identity, want, 38f);
        }

        /// <summary>The head joint's target that turns the drawn head at a world point. The piece draws its chest at half the
        /// chest body's bend and its head at half the head's turn on that, so the joint is asked twice what should show.</summary>
        Quaternion HeadAt(Vector3 point, float nod)
        {
            var b = Pawn.bodies;
            Vector3 to = point - b[(int)BodyId.Head].position;
            Quaternion want = Quaternion.LookRotation(Flat(to).sqrMagnitude > 1e-4f ? to : Pawn.Facing, Vector3.up) * Quaternion.Euler(nod, 0f, 0f);
            Quaternion drawnChest = Quaternion.Slerp(Pawn.Hips.rotation, b[(int)BodyId.Chest].rotation, 0.5f);
            (Quaternion.Inverse(drawnChest) * want).ToAngleAxis(out float a, out Vector3 ax);
            if (a > 180f) a -= 360f;
            if (float.IsNaN(ax.x) || ax.sqrMagnitude < 1e-6f) return Quaternion.identity;
            return Quaternion.AngleAxis(Mathf.Clamp(2f * a, -40f, 40f), ax);
        }

        /// <summary>Where a joint turns (in the world): the arm's shoulder, say.</summary>
        Vector3 Pivot(BodyId part)
        {
            var j = Pawn.joints[(int)part];
            return j != null ? j.transform.TransformPoint(j.anchor) : Pawn.bodies[(int)part].position;
        }

        bool CarriedByBishop
        {
            get
            {
                if (Pawn == null) return false;
                foreach (var o in All) if (o != null && o != this && o.Carried == Pawn) return true;
                return false;
            }
        }

        /// <summary>
        /// Every machine, from replicated state: the skill poses this piece's free arm now, so its model draws it where it
        /// goes (RagdollPawn.LeftArmShare, eased). Not while a bishop's rope has its hand (SwordFightSkillFx.RopeHand).
        /// </summary>
        public bool LeftArmPosed
        {
            get
            {
                if (!BodyMotion || Pawn == null || Pawn.State != PawnState.Active || CarriedByBishop) return false;
                switch (Piece)
                {
                    case PieceKind.King:
                        return EdgeStage == SfEdge.Active || (EdgeStage == SfEdge.Recovery && EdgeTime < 0.3f)
                            || (EdgeStage == SfEdge.None && (HostStage == SfStage.Windup || HostStage == SfStage.Active || (HostStage == SfStage.Recovery && KingCountered)));
                    case PieceKind.Queen:
                        return EdgeStage == SfEdge.Windup || EdgeStage == SfEdge.Recovery
                            || (EdgeStage == SfEdge.None && (Stage == SfStage.Windup || Stage == SfStage.Active || Stage == SfStage.Recovery));
                    case PieceKind.Rook:
                        return EdgeStage == SfEdge.Windup
                            || (EdgeStage == SfEdge.None && (Stage == SfStage.Windup || Stage == SfStage.Active || Stage == SfStage.Recovery));
                    case PieceKind.Bishop:
                        // 10-27: and while he aims (the second sword is in that hand from F).
                        return EdgeStage == SfEdge.Active || EdgeStage == SfEdge.Recovery
                            || (EdgeStage == SfEdge.None && (Stage == SfStage.Aim || Stage == SfStage.Windup || Stage == SfStage.Active || Stage == SfStage.Recovery));
                    case PieceKind.Knight:
                        return EdgeStage != SfEdge.None || Stage == SfStage.Active || Stage == SfStage.Recovery;
                }
                return false;
            }
        }

        /// <summary>The queen's edge cut has gone through this piece: it is held as it stood (chest, head, arms; its legs are
        /// the puppet's, it is pinned) until it is thrown - it does not know yet (host).</summary>
        internal void HoldStruck(float seconds)
        {
            if (Pawn == null || !BodyMotion) return;
            var b = Pawn.bodies;
            for (int i = (int)BodyId.Chest; i <= (int)BodyId.HandR; i++)
                struckPose[i] = Quaternion.Inverse(b[RagdollPawn.ParentOf[i]].rotation) * b[i].rotation;
            struckLeft = seconds;
        }

        // ---------------------------------------------------------------- king · 왕의 반격

        /// <summary>The share of the counter's swing at which it meets whoever he blocked (the hits land there).</summary>
        const float KingStrikeAt = 0.25f;
        /// <summary>Where the blade pointed as F was pressed (the guard is drawn up from there).</summary>
        Vector3 kingPressBlade;
        bool spinRaised;

        // The blade's lines (f ahead, s right): the guard across his front, cocked over the right shoulder, the strike
        // through the front, low left at the end, the tip dropped (an opening), down at the bishop's hands.
        static Vector3 KingG(Vector3 f, Vector3 s) => (-s * 0.9f + f * 0.3f + Vector3.up * 0.2f).normalized;
        static Vector3 KingC(Vector3 f, Vector3 s) => (s * 0.5f + Vector3.up * 0.85f + f * 0.05f).normalized;
        static Vector3 KingX(Vector3 f, Vector3 s) => (-s * 0.15f - Vector3.up * 0.15f + f).normalized;
        static Vector3 KingL(Vector3 f, Vector3 s) => (-s * 0.65f - Vector3.up * 0.35f + f * 0.65f).normalized;
        static Vector3 KingD(Vector3 f, Vector3 s) => (s * 0.2f - Vector3.up * 0.75f + f * 0.6f).normalized;
        static Vector3 KingB(Vector3 f, Vector3 s) => (f * 0.55f - Vector3.up * 0.8f - s * 0.2f).normalized;

        /// <summary>The king's blade through his F (null outside it).</summary>
        Vector3? KingBlade(Vector3 f, Vector3 s)
        {
            float t = StageTime;
            switch (Stage)
            {
                case SfStage.Windup:
                {
                    // Out of wherever it was (the holster, the click guard) into the guard across his front.
                    if (!BodyMotion) return (-s + Vector3.up * 0.15f).normalized;
                    Vector3 from = kingPressBlade.sqrMagnitude > 0.5f ? kingPressBlade : KingG(f, s);
                    return Vector3.Slerp(from, KingG(f, s), Out2(t / 0.07f));
                }
                case SfStage.Active:
                {
                    float beat = S.kingCounterBeat, k = CounterSpin;
                    Vector3 start = Blocked == SfBlock.Bishop ? Vector3.Slerp(KingG(f, s), KingB(f, s), Out2(t / 0.04f)) : KingG(f, s);
                    if (t < 0.03f) return start;   // the clash is drawn on it
                    if (t < beat) return Vector3.Slerp(start, KingC(f, s), Smooth((t - 0.03f) / Mathf.Max(0.01f, beat - 0.03f)));
                    if (k < KingStrikeAt) return Vector3.Slerp(KingC(f, s), KingX(f, s), In2(k / KingStrikeAt));
                    return Vector3.Slerp(KingX(f, s), KingL(f, s), Out2((k - KingStrikeAt) / (1f - KingStrikeAt)));
                }
                case SfStage.Recovery:
                {
                    if (!BodyMotion) return null;
                    Vector3 home = Fighter != null ? Fighter.HomeBlade : -f;
                    if (KingCountered) return t < 0.12f ? KingL(f, s) : Vector3.Slerp(KingL(f, s), home, Smooth((t - 0.12f) / 0.28f));
                    // The guard came to nothing: the tip drops to the board, he is open, then he picks it up.
                    if (t < 0.12f) return Vector3.Slerp(KingG(f, s), KingD(f, s), Smooth(t / 0.12f));
                    return t < 0.45f ? KingD(f, s) : Vector3.Slerp(KingD(f, s), home, Smooth((t - 0.45f) / 0.15f));
                }
            }
            return null;
        }

        /// <summary>The blade's line in the chest's frame, kept out in front of the body (the stub cannot cross it).</summary>
        Vector3 AlongBlade(Transform chest)
        {
            Vector3 l = chest.InverseTransformDirection(Blade ?? Pawn.Facing);
            l.x = Mathf.Max(l.x, -0.3f);
            return l.sqrMagnitude > 1e-6f ? l.normalized : Vector3.forward;
        }

        /// <summary>Where the sword arm goes once a skill hands the sword back (chest frame).</summary>
        Vector3 HomeArm(Transform chest)
        {
            if (Fighter == null || !Fighter.ClassicControls) return new Vector3(0.94f, -0.34f, 0.12f);
            Vector3 f = Flat(Pawn.Facing).normalized;
            return chest.InverseTransformDirection(f * 0.75f + Vector3.Cross(Vector3.up, f) * 0.3f - Vector3.up * 0.55f).normalized;
        }

        bool KingKey(ref BodyKey k)
        {
            if (Stage != SfStage.Windup && Stage != SfStage.Active && Stage != SfStage.Recovery) return false;
            Transform chest = Pawn.bodies[(int)BodyId.Chest].transform;
            Vector3 f = Flat(Dir).sqrMagnitude > 1e-4f ? Flat(Dir).normalized : Flat(Pawn.Facing).normalized, s = Vector3.Cross(Vector3.up, f);
            // The free hand on the blade's flat (a half-sword guard), at the foe as he winds up, pulled back past the hip
            // as he cuts (the torque).
            Vector3 brace = chest.InverseTransformDirection(Pawn.bodies[(int)BodyId.HandR].position + KingG(f, s) * 0.25f - Pivot(BodyId.ArmL));
            Vector3 atFoe = chest.InverseTransformDirection((f + Vector3.up * 0.15f).normalized);
            Vector3 pulled = new Vector3(-0.6f, -0.6f, -0.5f);
            Vector3 guardArm = new Vector3(-0.1f, -0.2f, 0.97f);
            Vector3 along = AlongBlade(chest);
            var guard = Upper(-14f, 6f, new Vector3(8f, -10f, 0f), new Vector3(6f, 28f, 0f));
            guard.armL = brace; guard.armW = 1f;
            var follow = Upper(-32f, 8f, new Vector3(14f, -26f, -8f), new Vector3(6f, 22f, 0f));
            follow.armL = pulled; follow.armW = 1f;
            float t = StageTime;
            switch (Stage)
            {
                case SfStage.Windup:
                {
                    // He sets himself: bladed, the sword shoulder forward, weight in, the free hand on the blade.
                    float e = Out2(t / 0.07f);
                    k = guard;
                    k.w = k.armW = e;
                    k.armR = guardArm;
                    k.swordW = e;
                    return true;
                }
                case SfStage.Active:
                {
                    float beat = S.kingCounterBeat, c = CounterSpin;
                    // The blow knocks him back (harder for a heavier attack; down at the bishop's hands at his feet) ...
                    var recoil = Blocked == SfBlock.Bishop ? Upper(-10f, 8f, new Vector3(22f, -8f, 0f), new Vector3(22f, 12f, 0f))
                        : Blocked == SfBlock.Sword || Blocked == SfBlock.None ? Upper(-8f, -4f, new Vector3(-6f, -4f, 0f), new Vector3(-6f, 16f, 0f))
                        : Upper(-4f, -7f, new Vector3(-12f, -2f, 0f), new Vector3(-10f, 10f, 0f));
                    recoil.armL = brace; recoil.armW = 1f;
                    // ... he winds toward the sword side, the free hand at the foe, then unwinds and drives the cut down
                    // through it and carries on through.
                    var coil = Upper(22f, -6f, new Vector3(-8f, 22f, 6f), new Vector3(0f, -30f, 0f));
                    coil.armL = atFoe; coil.armW = 1f;
                    var strike = Upper(-20f, 12f, new Vector3(18f, -18f, -4f), new Vector3(10f, 12f, 0f));
                    strike.armL = pulled; strike.armW = 1f;
                    if (t < 0.04f) k = Mix(guard, recoil, Out2(t / 0.04f));
                    else if (t < beat) k = Mix(recoil, coil, Smooth((t - 0.04f) / Mathf.Max(0.01f, beat - 0.04f)));
                    else if (c < KingStrikeAt) k = Mix(coil, strike, In2(c / KingStrikeAt));
                    else k = Mix(strike, follow, Out2((c - KingStrikeAt) / (1f - KingStrikeAt)));
                    k.armR = t < 0.04f ? guardArm : Vector3.Slerp(guardArm, along, Smooth((t - 0.04f) / 0.12f));
                    k.swordW = 1f;
                    return true;
                }
                default:
                {
                    if (KingCountered)
                    {
                        // The finish held a moment, then he comes back to himself.
                        float r = Smooth((t - 0.12f) / 0.28f);
                        k = follow;
                        k.w = k.armW = 1f - r;
                        k.armR = Vector3.Slerp(along, HomeArm(chest), r);
                        k.swordW = 1f;
                        return true;
                    }
                    // 빈틈: shoulders sag, head drops, guard down - he is open.
                    var spent = Upper(0f, 3f, new Vector3(12f, 0f, 0f), new Vector3(14f, 0f, 0f));
                    k = t < 0.12f ? Mix(guard, spent, Smooth(t / 0.12f)) : spent;
                    k.armL = brace;
                    k.armW = 1f - Mathf.Clamp01(t / 0.08f);
                    float back = Smooth((t - 0.45f) / 0.15f);
                    k.w *= 1f - back;
                    k.armR = Vector3.Slerp(along, HomeArm(chest), back);
                    k.swordW = 1f;
                    return true;
                }
            }
        }

        // ---------------------------------------------------------------- king · 왕의 귀환

        /// <summary>The landing's split (degrees): the legs take the drop, held, then he rises.</summary>
        float KingLandSplit(float t)
        {
            float most = Mathf.Min(42f, MaxGroundSplit);
            if (t < 0.04f) return Mathf.Lerp(Mathf.Min(16f, most), most, Out2(t / 0.04f));
            if (t < 0.15f) return most;
            return Mathf.Lerp(most, 0f, Smooth((t - 0.15f) / 0.15f));
        }

        /// <summary>The landing's whole-body tip (degrees, + forward over the cut).</summary>
        static float KingLandTip(float t) => t < 0.04f ? Mathf.Lerp(20f, 26f, Out2(t / 0.04f)) : t < 0.15f ? Mathf.Lerp(26f, 20f, (t - 0.04f) / 0.11f) : Mathf.Lerp(20f, 0f, Smooth((t - 0.15f) / 0.15f));

        bool KingEdgeKey(ref BodyKey k)
        {
            var X = EdgeParams;
            if (X == null) return false;
            float t = EdgeTime, c = X.kingCatch, h = X.kingHang, v = X.kingVault;
            Transform chest = Pawn.bodies[(int)BodyId.Chest].transform;
            // The free hand grabs at the lip beside the sword (he faces the board, so its left is along +Cross(up, out)).
            Vector3 lip = EdgeAt + Vector3.Cross(Vector3.up, EdgeOut) * 0.25f + EdgeOut * 0.04f - Vector3.up * 0.03f;
            Vector3 onLip = chest.InverseTransformDirection(lip - Pivot(BodyId.ArmL));
            k.w = 1f;
            k.legs = true;
            if (EdgeStage == SfEdge.Active)
            {
                if (KingHanging)
                {
                    k.armL = onLip;
                    k.armW = Out2(t / 0.08f);
                    if (t < 0.05f)
                    {
                        // Reaching up and driving the sword in, the legs trailing.
                        k.chest = new Vector3(-10f, -12f, 14f);
                        k.head = new Vector3(-28f, 0f, 0f);
                        k.thighL = k.thighR = 15f;
                    }
                    else if (t < c)
                    {
                        // The fall caught: the legs swing in under the hilt.
                        float e = Out2((t - 0.05f) / Mathf.Max(0.01f, c - 0.05f));
                        k.chest = Vector3.Lerp(new Vector3(-10f, -12f, 14f), new Vector3(-6f, -8f, 12f), e);
                        k.head = Vector3.Lerp(new Vector3(-28f, 0f, 0f), new Vector3(-22f, 0f, -6f), e);
                        k.thighL = k.thighR = Mathf.Lerp(15f, -15f, e);
                    }
                    else
                    {
                        // Hanging off his sword shoulder, the legs swinging out the catch; in its last moment he plants his
                        // soles on the face for the spring.
                        float hang = t - c;
                        float swing = -6f - 9f * Mathf.Cos(hang * 15f) * Mathf.Exp(-6f * hang);
                        float plant = Smooth((t - (c + h - 0.12f)) / 0.12f);
                        k.chest = Vector3.Lerp(new Vector3(-4f, -8f, 10f), new Vector3(12f, -6f, 8f), plant);
                        k.head = Vector3.Lerp(new Vector3(-22f, 0f, -6f), new Vector3(-20f, 0f, 0f), Mathf.Max(Smooth(hang / 0.1f) * 0.5f, plant));
                        k.thighL = k.thighR = Mathf.Lerp(swing, -28f, plant);
                    }
                    return true;
                }
                float u = Mathf.Clamp01((t - c - h) / Mathf.Max(0.05f, v));
                Vector3 up = new Vector3(-0.35f, 0.85f, 0.4f), back = new Vector3(-0.6f, 0.1f, 0.8f), wide = new Vector3(-0.9f, 0.15f, 0.35f);
                k.armW = 1f;
                if (u < 0.16f)
                {
                    // The spring: he extends off the wall, the sword torn out and swung up, the free arm thrown up.
                    float e = Out2(u / 0.16f);
                    k.chest = Vector3.Lerp(new Vector3(12f, -6f, 8f), new Vector3(-14f, 0f, 0f), e);
                    k.head = Vector3.Lerp(new Vector3(-20f, 0f, 0f), new Vector3(-26f, 0f, 0f), e);
                    k.armL = SlerpDir(onLip, up, e);
                    k.thighL = k.thighR = Mathf.Lerp(-28f, 22f, e);
                }
                else if (u < 0.42f)
                {
                    // Up, gathered: the sword drawn back over the head, the legs tucked.
                    float e = Smooth((u - 0.16f) / 0.26f);
                    k.chest = new Vector3(Mathf.Lerp(-14f, 10f, e), 0f, 0f);
                    k.head = new Vector3(Mathf.Lerp(-26f, 6f, e), 0f, 0f);
                    k.armL = SlerpDir(up, back, e);
                    k.thighL = k.thighR = Mathf.Lerp(22f, -30f, e);
                }
                else if (u < 0.8f)
                {
                    // Coming down, eyes on the enemies, the free arm out for balance, the feet reaching for the board.
                    float e = Smooth((u - 0.42f) / 0.38f);
                    k.chest = new Vector3(Mathf.Lerp(10f, 6f, e), 0f, 0f);
                    k.head = new Vector3(Mathf.Lerp(6f, -6f, e), 0f, 0f);
                    k.armL = SlerpDir(back, wide, e);
                    k.thighL = k.thighR = Mathf.Lerp(-30f, -10f, e);
                }
                else
                {
                    // The chop arrives with the feet.
                    float e = In2((u - 0.8f) / 0.2f);
                    k.chest = new Vector3(Mathf.Lerp(6f, 22f, e), 0f, 0f);
                    k.head = new Vector3(Mathf.Lerp(-6f, -10f, e), 0f, 0f);
                    k.armL = wide;
                    k.thighL = Mathf.Lerp(-10f, -16f, e);
                    k.thighR = Mathf.Lerp(-10f, 16f, e);
                }
                return true;
            }
            if (EdgeStage == SfEdge.Recovery)
            {
                // The landing taken in the legs, the chop held down, then he rises.
                float a = KingLandSplit(t), rise = Smooth((t - 0.15f) / 0.15f);
                k.w = 1f - Smooth((t - 0.28f) / 0.07f);
                k.chest = new Vector3(Mathf.Lerp(22f, 4f, rise), 0f, 0f);
                k.head = new Vector3(Mathf.Lerp(-12f, 0f, rise), 0f, 0f);
                k.armL = new Vector3(-0.85f, -0.15f, -0.3f);
                k.armW = 1f - rise;
                k.thighL = -a;
                k.thighR = a;
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- queen · 꼬치 베기, 체크메이트 일섬

        /// <summary>StageTime of the thrust's last cut (the blade bites: a small jolt through her).</summary>
        float queenHitAt = -10f;
        /// <summary>Whom her head follows after the checkmate cut.</summary>
        RagdollPawn queenWatch;
        /// <summary>Her blink's finished cut (the pose she arrives in).</summary>
        BodyKey queenBlinkKey;

        Vector3 QueenLaneLook => Floor(Origin) + Dir * Mathf.Max(2f, Reach) + Vector3.up * 0.5f;

        static BodyKey QueenKeyOf(Vector3 chest, Vector3 armR, Vector3 armL, float split, float lean, float nod, Vector3 look) => new BodyKey
        {
            w = 1f, chest = chest, armR = armR, swordW = 1f, armL = armL, armW = 1f, legs = true,
            thighL = split, thighR = -split, lean = lean, nod = nod, look = look,
        };

        bool QueenKey(ref BodyKey k)
        {
            float t;
            if (EdgeStage == SfEdge.Windup)
            {
                t = EdgeTime;
                Vector3 look = EdgeTarget != null ? EdgeTarget.bodies[(int)BodyId.Head].position : Pawn.Hips.position + Pawn.Facing * 3f;
                // The predator sets: the blade cocked across her front, the off hand at her hip where a scabbard would hang,
                // dead still but for the head on its target; a breath before the draw.
                float set = Out2(t / 0.10f), creep = Smooth((t - 0.10f) / 0.32f), load = In2((t - 0.42f) / 0.08f);
                k = QueenKeyOf(new Vector3(8f + creep + load, -24f - 3f * creep - 3f * load, 3f * load),
                    Vector3.Slerp(new Vector3(-0.25f, -0.30f, 0.92f), new Vector3(-0.30f, -0.36f, 0.88f), Mathf.Max(creep * 0.4f, load)),
                    new Vector3(-0.45f, -0.80f, 0.40f), 30f * set + 3f * creep, 3f, 6f + 2f * load, look);
                k.w = k.armW = k.swordW = set;
                return true;
            }
            if (EdgeStage == SfEdge.Recovery)
            {
                t = EdgeTime;
                var X = EdgeParams;
                float delay = X != null ? X.queenDelay : 0.35f;
                if (queenWatch != null) queenBlinkKey.look = queenWatch.bodies[(int)BodyId.Chest].position;
                k = queenBlinkKey;
                if (EdgeDetail == "헛벰")
                {
                    // Nothing stopped it: over-rotated, looking up at whoever jumped; back to rest.
                    k.chest = new Vector3(4f, 30f, -6f);
                    k.nod = -15f;
                    float back = Smooth((t - 0.25f) / 0.15f);
                    k.w = k.armW = k.swordW = 1f - back;
                    return Pawn.Grounded || t < 0.1f;
                }
                if (t < 0.06f)
                {
                    float e = Out2(t / 0.06f);
                    k.chest = Vector3.Lerp(new Vector3(10f, 28f, -4f), new Vector3(16f, 30f, -5f), e);
                    k.thighL = Mathf.Lerp(38f, 40f, e);
                }
                else if (t < delay)
                {
                    // Zanshin: the finish held, her eyes on what she cut.
                    float e = Smooth((t - 0.06f) / 0.29f);
                    k.chest = Vector3.Lerp(new Vector3(16f, 30f, -5f), new Vector3(12f, 28f, -4f), e);
                    k.thighL = Mathf.Lerp(40f, 38f, e);
                }
                else
                {
                    // The flick that answers the throw (chiburi), the off hand back to the hip.
                    float e = Out2((t - delay) / 0.05f);
                    k.chest = Vector3.Lerp(new Vector3(12f, 28f, -4f), new Vector3(6f, 18f, -2f), e);
                    k.armL = SlerpDir(k.armL, new Vector3(-0.50f, -0.80f, 0.20f), e);
                    k.thighL = Mathf.Lerp(38f, 30f, e);
                }
                k.thighR = -k.thighL;
                return Pawn.Grounded;
            }
            if (EdgeStage != SfEdge.None) return false;
            t = StageTime;
            switch (Stage)
            {
                case SfStage.Windup:
                {
                    // The chamber: the fist drawn back to her hip, the blade down the lane, the off hand sighting it, her
                    // weight rocked onto the rear leg; still while it loads, then a last draw into the release.
                    float set = Out2(t / 0.10f), creep = Smooth((t - 0.10f) / 0.23f), load = In2((t - 0.33f) / 0.07f);
                    k = QueenKeyOf(new Vector3(-6f - creep - load, 20f + 3f * creep + 3f * load, 2f * load),
                        Vector3.Slerp(new Vector3(0.55f, -0.60f, -0.58f), new Vector3(0.58f, -0.58f, -0.57f), creep),
                        new Vector3(-0.30f, 0.10f - 0.08f * load, 0.95f), 26f + 2f * creep + 2f * load, -4f - creep - load, 4f + 3f * load, QueenLaneLook);
                    k.w = k.armW = k.swordW = set;
                    return true;
                }
                case SfStage.Active:
                {
                    // The release, hips to chest to arm to the off arm thrown back - the lunge held as she glides.
                    var load = QueenKeyOf(new Vector3(-8f, 26f, 2f), new Vector3(0.58f, -0.58f, -0.57f), new Vector3(-0.30f, 0.02f, 0.95f), 30f, -6f, 7f, QueenLaneLook);
                    var lunge = QueenKeyOf(new Vector3(18f, -22f, -4f), new Vector3(0.58f, -0.58f, -0.57f), new Vector3(-0.45f, 0.30f, -0.84f), 38f, 6f, 6f, QueenLaneLook);
                    k = Mix(load, lunge, Out2(t / 0.07f));
                    k.armL = SlerpDir(load.armL, lunge.armL, Out2(t / 0.10f));
                    k.swordW = 1f - Out2(t / 0.09f);
                    // Each cut: the blade bites and jolts back through her.
                    float bite = t - queenHitAt;
                    if (bite >= 0f && bite < 0.105f)
                    {
                        float e = bite < 0.025f ? Out2(bite / 0.025f) : 1f - Smooth((bite - 0.025f) / 0.08f);
                        k.chest += new Vector3(-6f, 5f, 0f) * e;
                    }
                    return true;
                }
                case SfStage.Recovery:
                {
                    if (!Pawn.Grounded) return false;   // off the edge: the air pose is right for a fall
                    // The plant: the upper body carries on over the front leg, held a moment, then she recovers.
                    var lunge = QueenKeyOf(new Vector3(18f, -22f, -4f), Vector3.zero, new Vector3(-0.45f, 0.30f, -0.84f), 38f, 6f, 6f, QueenLaneLook);
                    var plant = QueenKeyOf(new Vector3(26f, -20f, -4f), Vector3.zero, new Vector3(-0.45f, 0.25f, -0.86f), 42f, 10f, 12f, QueenLaneLook);
                    var hold = QueenKeyOf(new Vector3(22f, -18f, -3f), Vector3.zero, new Vector3(-0.45f, 0.25f, -0.86f), 42f, 6f, 6f, QueenLaneLook);
                    if (t < 0.05f) k = Mix(lunge, plant, Out2(t / 0.05f));
                    else if (t < 0.20f) k = Mix(plant, hold, Smooth((t - 0.05f) / 0.15f));
                    else
                    {
                        k = hold;
                        float r = Smooth((t - 0.20f) / 0.25f);
                        k.w = k.armW = 1f - r;
                        k.armR = HomeArm(Pawn.bodies[(int)BodyId.Chest].transform);
                        k.swordW = r;
                        return true;
                    }
                    k.swordW = 0f;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The queen's thrust blade (null outside it).</summary>
        Vector3? QueenBlade(Vector3 f)
        {
            float t = StageTime;
            switch (Stage)
            {
                case SfStage.Windup:
                    if (!BodyMotion) return (f + Vector3.up * 0.1f).normalized;
                    return (f + Vector3.up * (0.06f - 0.05f * In2((t - 0.33f) / 0.07f))).normalized;
                case SfStage.Active: return f;
                case SfStage.Recovery:
                    if (!BodyMotion) return f;
                    Vector3 dip = (f - Vector3.up * 0.09f).normalized;
                    if (t < 0.20f || Fighter == null) return dip;
                    return Vector3.Slerp(dip, Fighter.HomeBlade, Smooth((t - 0.20f) / 0.25f));
            }
            return null;
        }

        /// <summary>The queen's edge-cut blade.</summary>
        Vector3 QueenEdgeBlade(Vector3 f, Vector3 side)
        {
            float t = EdgeTime;
            if (!BodyMotion)
                return EdgeStage == SfEdge.Windup ? (f + Vector3.up * 0.35f - side * 0.4f).normalized : (f - Vector3.up * 0.1f + side * 0.7f).normalized;
            if (EdgeStage == SfEdge.Windup)
            {
                Vector3 cocked = (f * 0.55f + Vector3.up * 0.20f - side * 0.80f).normalized, drawn = (f * 0.45f + Vector3.up * 0.15f - side * 0.88f).normalized;
                return Vector3.Slerp(cocked, drawn, In2((t - 0.42f) / 0.08f));
            }
            // Through: the cut finished 53 degrees to her right, overshooting a hair as she lands it, then flicked down.
            Vector3 cut = (f * 0.6f - Vector3.up * 0.1f + side * 0.8f).normalized, over = (f * 0.55f - Vector3.up * 0.16f + side * 0.86f).normalized;
            Vector3 flick = (f * 0.45f - Vector3.up * 0.60f + side * 0.65f).normalized;
            float delay = EdgeParams != null ? EdgeParams.queenDelay : 0.35f;
            if (EdgeDetail == "헛벰") return Vector3.Slerp(cut, Fighter != null ? Fighter.HomeBlade : f, Smooth((t - 0.25f) / 0.15f));
            if (t < 0.06f) return Vector3.Slerp(cut, over, Mathf.Sin(t / 0.06f * Mathf.PI));
            if (t < delay) return cut;
            return Vector3.Slerp(cut, flick, Out2((t - delay) / 0.05f));
        }

        /// <summary>The blink lands her already through the cut (host, from QueenStrikes): lowered into her stance, every part
        /// placed in the finished cut, so no screen sees a T-pose she moves out of.</summary>
        void QueenArrives(Vector3 ground, Vector3 face, RagdollPawn target)
        {
            Vector3 f = Flat(face).normalized, side = Vector3.Cross(Vector3.up, f);
            var blink = QueenKeyOf(new Vector3(10f, 28f, -4f), Vector3.zero, new Vector3(-0.70f, -0.20f, -0.68f), 38f, 4f, 4f,
                target != null ? target.bodies[(int)BodyId.Chest].position : ground + f * 1f + Vector3.up);
            blink.swordW = 0f;
            queenBlinkKey = blink;
            queenWatch = target;
            if (!BodyMotion)
            {
                Pawn.Teleport(ground + Vector3.up * (Pawn.standHeight + 0.02f), face);
                return;
            }
            float split = Mathf.Min(38f, MaxGroundSplit);
            Pawn.SkillStance = StanceDrop(split);
            Pawn.Teleport(ground + Vector3.up * (Pawn.standHeight + 0.02f - Pawn.SkillStance), face);
            Pawn.SnapStance();
            // The bodies placed straight into the cut (parent first: the chest, then the head on it, the arms, the legs).
            Vector3 cut = (f * 0.6f - Vector3.up * 0.1f + side * 0.8f).normalized;
            bodyKey = blink;
            bodyOn = true;
            Pawn.SnapPose(part =>
            {
                if (part == (int)BodyId.ArmR)
                {
                    Vector3 l = Pawn.bodies[(int)BodyId.Chest].transform.InverseTransformDirection(cut);
                    return Quaternion.FromToRotation(Vector3.right, l);
                }
                if (part == (int)BodyId.HandL || part == (int)BodyId.HandR) return null;
                return SkillBodyPose(part);
            });
        }

        // ---------------------------------------------------------------- rook · 열린 파일 포격, 성벽 붕괴

        bool RookKey(ref BodyKey k)
        {
            float t = StageTime;
            Vector3 lane = Floor(Origin) + Dir * Mathf.Clamp(Reach, 2.5f, 6f);
            // Loaded: bent over the sword he drags low behind him, his sword shoulder back and the body turned to it, the
            // free arm out for balance, the sword-side foot back, his eyes down the lane. The heave: as the blade sweeps up
            // out of the floor he straightens and unwinds into it, the free arm flung down and back - brute force, the
            // whole body behind the blade. Held as the wave runs, then he comes back to himself.
            var load = new BodyKey
            {
                w = 1f, chest = new Vector3(18f, 22f, -4f), look = lane + Vector3.up * 0.2f, nod = 6f,
                armL = new Vector3(-0.55f, -0.35f, 0.75f), armW = 1f, legs = true, thighL = -16f, thighR = 16f, turn = 12f, lean = 6f,
            };
            var heave = new BodyKey
            {
                w = 1f, chest = new Vector3(-10f, -14f, 4f), look = lane + Vector3.up * 1.4f, nod = -4f,
                armL = new Vector3(-0.75f, -0.55f, -0.25f), armW = 1f, legs = true, thighL = -10f, thighR = 10f, turn = -10f, lean = -6f,
            };
            switch (Stage)
            {
                case SfStage.Windup:
                {
                    float set = Out2(t / 0.12f), sink = Smooth((t - 0.12f) / Mathf.Max(0.05f, S.rookWindup - 0.12f));
                    k = load;
                    k.chest = Vector3.Lerp(new Vector3(12f, 16f, -4f), load.chest, sink);
                    k.w = k.armW = set;
                    return true;
                }
                case SfStage.Active:
                    k = Mix(load, heave, Smooth(t / 0.2f));
                    return true;
                case SfStage.Recovery:
                {
                    k = heave;
                    float r = Smooth((t - 0.1f) / Mathf.Max(0.05f, S.rookRecovery - 0.1f));
                    k.w = k.armW = 1f - r;
                    return true;
                }
            }
            return false;
        }

        bool RookEdgeKey(ref BodyKey k)
        {
            if (EdgeStage != SfEdge.Windup) return false;
            // He hammers the lip: bowed over the blow at once (the board under it starts to crack then), the free hand
            // braced out low, the sword-side foot back; held, then up a little as he steps back from it.
            float t = EdgeTime;
            var blow = new BodyKey
            {
                w = 1f, chest = new Vector3(26f, -6f, -6f), look = EdgeAt, nod = 14f,
                armL = new Vector3(-0.6f, -0.6f, 0.5f), armW = 1f, legs = true, thighL = -16f, thighR = 16f, lean = 10f,
            };
            var up = blow;
            up.chest = new Vector3(14f, -4f, -3f);
            up.lean = 5f;
            up.nod = 8f;
            if (t < 0.05f)
            {
                k = blow;
                k.w = k.armW = Out2(t / 0.05f);
                return true;
            }
            k = t < 0.15f ? blow : Mix(blow, up, Smooth((t - 0.15f) / 0.15f));
            return true;
        }

        /// <summary>The rook's edge blade: brought down hard on the lip in its first frames, then held there.</summary>
        Vector3 RookEdgeBlade()
        {
            Vector3 toLip = Flat(EdgeAt - Pawn.Hips.position).normalized;
            Vector3 down = (toLip - Vector3.up * 0.9f).normalized, raised = (toLip * 0.5f + Vector3.up * 0.85f).normalized;
            return Vector3.Slerp(raised, down, In2(EdgeTime / 0.06f));
        }

        // ---------------------------------------------------------------- bishop · 관통 핀, 구원의 손

        /// <summary>
        /// 10-27, the bishop's X cut: a sword in each hand, mirrored across him; he stays where he is.
        /// - Aiming: both held low and forward, a little apart (a V), the chest square, the legs his own (he walks).
        /// - Drawing back: both swords up and back over his shoulders, leaning back, a foot forward.
        /// - The throw: both arms whip forward and out (the swords leave his hands); then held out ahead of him while they fly.
        /// - Their way back: his hands open out to take them, and close on them as they come in (the catch: a little give),
        ///   then let go.
        /// </summary>
        bool BishopKey(ref BodyKey k)
        {
            float t = StageTime;
            Vector3 look = PinCandidate != null ? PinCandidate.bodies[(int)BodyId.Chest].position
                : (Stage == SfStage.Aim ? AimPoint : Point) + Vector3.up * 0.4f;
            if (Stage != SfStage.Aim && bishopTarget != null) look = bishopTarget.bodies[(int)BodyId.Chest].position;
            BodyKey Two(Vector3 chest, Vector3 arm, float lean, float thigh, bool legs) => new BodyKey
            {
                w = 1f, chest = chest, look = look, nod = 4f, armR = arm, swordW = 1f, armL = new Vector3(-arm.x, arm.y, arm.z), armW = 1f,
                legs = legs, thighL = -thigh, thighR = legs ? thigh * 0.6f : 0f, lean = lean,
            };
            var ready = Two(new Vector3(4f, 0f, 0f), new Vector3(0.35f, -0.35f, 0.87f), 0f, 0f, false);
            var cocked = Two(new Vector3(-8f, 0f, 0f), new Vector3(0.55f, 0.55f, -0.6f), -4f, 14f, true);
            var thrown = Two(new Vector3(14f, 0f, 0f), new Vector3(0.45f, 0.05f, 0.89f), 6f, 16f, true);
            var guiding = Two(new Vector3(6f, 0f, 0f), new Vector3(0.4f, 0.18f, 0.9f), 2f, 12f, true);
            var open = Two(new Vector3(2f, 0f, 0f), new Vector3(0.7f, 0.2f, 0.68f), 0f, 12f, true);
            var caught = Two(new Vector3(-4f, 0f, 0f), new Vector3(0.5f, 0.35f, 0.79f), -2f, 12f, true);
            switch (Stage)
            {
                case SfStage.Aim:
                    k = ready;
                    k.w = k.armW = k.swordW = Out2(t / 0.12f);
                    return true;
                case SfStage.Windup:
                    k = Mix(ready, cocked, Out2(t / Mathf.Max(0.05f, S.bishopThrowWindup)));
                    return true;
                case SfStage.Active:
                {
                    float back = BishopOutTime + S.bishopCrossTime, all = BishopThrowTime;
                    if (t < 0.1f) { k = Mix(cocked, thrown, Out2(t / 0.1f)); return true; }
                    if (t < back) { k = Mix(thrown, guiding, Smooth((t - 0.1f) / 0.2f)); return true; }
                    // Hands out for them, then on them as they come in.
                    k = Mix(guiding, open, Smooth((t - back) / 0.25f));
                    float near = Smooth((t - (all - 0.18f)) / 0.18f);
                    if (near > 0f) k = Mix(k, caught, near);
                    return true;
                }
                case SfStage.Recovery:
                {
                    // The catch gives a little, then he lets the pose go.
                    float give = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.12f));
                    k = caught;
                    k.chest.x -= 4f * give;
                    float r = Smooth((t - 0.1f) / Mathf.Max(0.05f, S.bishopCatchAfter - 0.1f));
                    k.w = k.armW = k.swordW = 1f - r;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The right sword's line (world): in his hand while he aims and draws back; while it is thrown the hand's
        /// own line is where it will be caught (the thrown copy turns into it as it comes in); then handed back.</summary>
        Vector3 BishopBlade(Vector3 f, Vector3 side)
        {
            Vector3 up = Vector3.up;
            Vector3 ready = (f * 0.75f - up * 0.35f + side * 0.25f).normalized;
            Vector3 cocked = (-f * 0.6f + up * 0.55f + side * 0.3f).normalized;
            Vector3 caught = BishopCatchBlade(f, side);
            float t = StageTime;
            switch (Stage)
            {
                case SfStage.Aim: return ready;
                case SfStage.Windup: return Vector3.Slerp(ready, cocked, Out2(t / Mathf.Max(0.05f, S != null ? S.bishopThrowWindup : 0.2f)));
                case SfStage.Active: return caught;
                case SfStage.Recovery:
                {
                    float after = S != null ? S.bishopCatchAfter : 0.35f;
                    Vector3 home = Fighter != null ? Fighter.HomeBlade : ready;
                    return Vector3.Slerp(caught, home, Smooth((t - 0.1f) / Mathf.Max(0.05f, after - 0.1f)));
                }
            }
            return ready;
        }

        /// <summary>
        /// 10-27: the bishop's second sword, in his left hand from F to the end of the cut (every machine, from the skill's
        /// replicated state; SwordFightPawn.OffHand draws it): his right sword mirrored across him, so the two make an X
        /// where they cross. Null when he has only the one.
        /// </summary>
        public Vector3? OffBlade
        {
            get
            {
                if (!BodyMotion || Piece != PieceKind.Bishop || EdgeStage != SfEdge.None || Stage == SfStage.None || Pawn == null || Pawn.State != PawnState.Active)
                    return null;
                if (!(Blade is Vector3 b)) return null;
                Vector3 f = Flat(Stage == SfStage.Aim ? AimDir : Dir);
                if (f.sqrMagnitude < 1e-4f) f = Flat(Pawn.Facing);
                Vector3 side = Vector3.Cross(Vector3.up, f.normalized);
                return b - 2f * Vector3.Dot(b, side) * side;
            }
        }

        bool BishopEdgeKey(ref BodyKey k)
        {
            var X = EdgeParams;
            if (X == null || (EdgeStage != SfEdge.Active && EdgeStage != SfEdge.Recovery)) return false;
            float t = EdgeTime;
            Transform chest = Pawn.bodies[(int)BodyId.Chest].transform;
            var ally = Carried;
            Vector3 allyAt = ally != null ? ally.bodies[(int)BodyId.Chest].position : EdgeAt + EdgeOut * 0.5f;
            Vector3 toAlly = chest.InverseTransformDirection(allyAt - Pivot(BodyId.ArmL));
            // The throw: the free arm whips the rope out at the ally, his free shoulder leading. The haul: he leans back and
            // turns away from it, drawing the rope arm back past his hip, the legs braced; then he lets it go.
            var thrown = new BodyKey
            {
                w = 1f, chest = new Vector3(8f, -18f, 0f), look = allyAt, nod = 4f, armL = toAlly, armW = 1f,
                legs = true, thighL = -14f, thighR = 14f, lean = 6f,
            };
            var hauled = new BodyKey
            {
                w = 1f, chest = new Vector3(-12f, 12f, 0f), look = allyAt, nod = 2f, armL = new Vector3(-0.45f, -0.35f, -0.8f), armW = 1f,
                legs = true, thighL = -22f, thighR = 22f, lean = -8f,
            };
            if (EdgeStage == SfEdge.Active)
            {
                if (t < X.bishopReach)
                {
                    float e = Smooth(t / Mathf.Max(0.05f, X.bishopReach));
                    k = thrown;
                    k.w = k.armW = e;
                    k.armL = SlerpDir(new Vector3(-0.6f, 0.3f, -0.6f), toAlly, e);
                    return true;
                }
                k = Mix(thrown, hauled, Smooth((t - X.bishopReach) / Mathf.Max(0.1f, X.bishopPull)));
                return true;
            }
            k = hauled;
            float r = Smooth(t / Mathf.Max(0.1f, X.bishopRecovery));
            k.w = k.armW = 1f - r;
            return true;
        }

        // ---------------------------------------------------------------- knight · 포크 강하

        /// <summary>The fork dive's gather before it springs (s; 0 = it leaves at once).</summary>
        public float KnightGather => S != null && BodyMotion ? Mathf.Max(0f, S.knightGather) : 0f;
        /// <summary>The fork dive's flight clock: negative through the gather, 0 = it leaves the square.</summary>
        public float KnightClock => StageTime - KnightGather;

        static readonly BodyKey FGather = Key(22f, -12f, -18f, 18f, new Vector3(-0.45f, -0.55f, -0.70f), 4f);
        static readonly BodyKey FLaunch = Key(-14f, -28f, 28f, 28f, new Vector3(-0.35f, 0.70f, 0.62f));
        static readonly BodyKey FTuck = Key(4f, -10f, -40f, -40f, new Vector3(-0.25f, -0.15f, 0.95f), 0f, 18f);
        static readonly BodyKey FLook = Key(18f, 30f, -30f, -30f, new Vector3(-0.30f, -0.60f, 0.75f), 0f, 12f);
        static readonly BodyKey FImpact = Key(26f, 24f, -26f, 26f, new Vector3(-0.45f, -0.80f, 0.40f), 5f);
        static readonly BodyKey FUp = Key(0f, -6f, 0f, 0f, ArmRest);

        static BodyKey WithArm(BodyKey k, Vector3 arm) { k.armL = arm.normalized; return k; }

        bool KnightKey(ref BodyKey k)
        {
            if (Stage != SfStage.Active && Stage != SfStage.Recovery) return false;
            float R = S.knightRise, H = S.knightHover, T = R + H + S.knightDive;
            if (Stage == SfStage.Recovery)
            {
                // The impact held (it covers the landing's stop), then up into a low guard and back to itself.
                float r = StageTime, u = Out2((r - 0.10f) / 0.18f);
                k = r < 0.10f ? FImpact : Mix(FImpact, FUp, u);
                float fade = 1f - Sm(0.26f, 0.35f, r);
                k.w *= fade;
                k.armW *= fade;
                return true;
            }
            float t = KnightClock;
            if (t < 0f)
            {
                // The gather: down into a split stance, the free hand swung back.
                k = FGather;
                k.w = k.armW = Out2(StageTime / 0.04f);
                return true;
            }
            if (t < 0.08f)
            {
                // The push-off: rearing, the legs driving back, the front leg up.
                k = Mix(FGather, FLaunch, Out2(t / 0.08f));
                k.armL = SlerpDir(FGather.armL, FLaunch.armL, Sm(0f, 0.16f, t));
                return true;
            }
            if (t < R) { k = Mix(FLaunch, FTuck, Sm(0.08f, 0.20f, t)); return true; }
            // Over the top its nose comes down at the square and the free hand points at it.
            Vector3 toSquare = Pawn.bodies[(int)BodyId.Chest].transform.InverseTransformDirection(Point - Pivot(BodyId.ArmL));
            var look = WithArm(FLook, toSquare);
            if (t < R + H) { k = Mix(FTuck, look, Sm(R, R + H, t)); return true; }
            k = Mix(look, FImpact, Sm(R + H, T - 0.02f, t));
            return true;
        }

        /// <summary>The fork dive's blade: low behind it in the gather, up behind as it springs, wound back over the top,
        /// driven down in front as it lands, then up into a low guard.</summary>
        Vector3 KnightDiveBlade(Vector3 f, Vector3 side)
        {
            Vector3 low = (-f * 0.55f + side * 0.45f - Vector3.up * 0.7f).normalized, high = (Vector3.up - f * 0.2f).normalized;
            Vector3 cocked = (Vector3.up * 0.75f - f * 0.6f).normalized, down = (f * 0.85f + side * 0.15f - Vector3.up * 0.55f).normalized;
            Vector3 guard = (f * 0.8f + side * 0.25f - Vector3.up * 0.45f).normalized;
            if (Stage == SfStage.Recovery) return Vector3.Slerp(down, guard, Sm(0.12f, 0.30f, StageTime));
            float R = S.knightRise, H = S.knightHover, T = R + H + S.knightDive, t = KnightClock;
            if (t < 0f) return low;
            if (t < 0.16f) return Vector3.Slerp(low, high, Sm(0f, 0.16f, t));
            if (t < R) return high;
            if (t < R + H - 0.04f) return Vector3.Slerp(high, cocked, Sm(R, R + H - 0.04f, t));
            return Vector3.Slerp(cocked, down, Sm(R + H - 0.04f, T, t));
        }

        // ---------------------------------------------------------------- knight · 벼랑 끝 역전

        public const float KnightCoilTip = 10f, KnightThrowTip = -24f, KnightBuckTip = 40f, KnightTuckAt = 0.42f;

        /// <summary>The flip's turn (degrees about its right, + nose down) at <paramref name="k"/> of the flight: thrown back
        /// off the lip, held there for the look, then over forward once and on into the buck, so it lands already kicking.</summary>
        public static float KnightFlipTurn(float k)
        {
            if (!BodyMotion) return 360f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FlipOut + 0.05f, 0.86f, k));
            float back = Mathf.Lerp(KnightCoilTip, KnightThrowTip, Out2(k / 0.10f));
            return Mathf.Lerp(back, 360f + KnightBuckTip, Sm(KnightTuckAt, 0.95f, k));
        }

        static readonly BodyKey ECoil = Key(24f, -14f, -22f, 22f, new Vector3(-0.40f, -0.72f, 0.55f), 5f);
        static readonly BodyKey ECoilAir = Key(20f, -14f, -34f, -34f, new Vector3(-0.70f, -0.50f, 0.50f), 0f, 15f);
        static readonly BodyKey EThrow = Key(-10f, 24f, -12f, -12f, new Vector3(-0.85f, 0.45f, 0.25f));
        static readonly BodyKey EHang = Key(0f, 36f, 10f, 10f, new Vector3(-0.85f, 0.50f, 0.15f));
        static readonly BodyKey ETuck = Key(28f, 34f, -45f, -45f, new Vector3(-0.30f, -0.45f, 0.85f), 0f, 20f);
        static readonly BodyKey EKick = Key(18f, 30f, 48f, 48f, new Vector3(-0.35f, -0.80f, 0.50f), 0f, -15f);
        static readonly BodyKey ESettle = Key(-4f, -8f, -8f, -8f, ArmRest);

        bool KnightEdgeKey(ref BodyKey k)
        {
            var X = EdgeParams;
            if (X == null) return false;
            float t = EdgeTime;
            var coil = knightAir ? ECoilAir : ECoil;
            switch (EdgeStage)
            {
                case SfEdge.Windup:
                    // Coiled, the gaze level on its enemy, dead still: the tell before the spring.
                    k = coil;
                    k.w = k.armW = Smooth(t / 0.1f);
                    return true;
                case SfEdge.Active:
                {
                    float q = Mathf.Clamp01(t / Mathf.Max(0.1f, X.knightFlip));
                    if (q < 0.10f) { k = Mix(coil, EThrow, Out2(q / 0.10f)); k.armL = SlerpDir(coil.armL, EThrow.armL, Sm(0f, 0.15f, q)); return true; }
                    if (q < KnightTuckAt) { k = Mix(EThrow, EHang, Sm(0.10f, 0.30f, q)); return true; }
                    if (q < 0.82f) { k = Mix(EHang, ETuck, Out2((q - KnightTuckAt) / 0.10f)); k.armL = SlerpDir(EHang.armL, ETuck.armL, Sm(KnightTuckAt, KnightTuckAt + 0.15f, q)); return true; }
                    k = Mix(ETuck, EKick, Sm(0.82f, 0.95f, q));
                    return true;
                }
                case SfEdge.Recovery:
                {
                    // The kick held at full stretch through its stop, then the weight comes down onto the hind legs.
                    float s = Out2((t - 0.09f) / 0.13f);
                    k = t < 0.09f ? EKick : Mix(EKick, ESettle, s);
                    float fade = 1f - Sm(0.24f, 0.35f, t);
                    k.w *= fade;
                    k.armW *= fade;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The flip's blade: high behind the shoulder in the coil, going round with the body in the air (held in its
        /// frame, so the arm does not wheel round it), low in front at the kick (the legs make the hit, not the sword).</summary>
        Vector3 KnightEdgeBlade(Vector3 f, Vector3 side)
        {
            Vector3 cock = (-f * 0.6f + side * 0.4f + Vector3.up * 0.7f).normalized, front = (f * 0.7f + side * 0.3f - Vector3.up * 0.6f).normalized;
            if (EdgeStage == SfEdge.Windup) return cock;
            if (EdgeStage == SfEdge.Recovery) return front;
            Quaternion cr = Pawn.bodies[(int)BodyId.Chest].rotation;
            float k = Mathf.Clamp01(EdgeTime / Mathf.Max(0.1f, EdgeParams.knightFlip));
            Vector3 high = cr * new Vector3(0.4f, 0.85f, 0.25f).normalized, tuck = cr * new Vector3(0.1f, -0.85f, 0.5f).normalized;
            if (k < KnightTuckAt) return Vector3.Slerp(cock, high, Sm(0f, 0.12f, k));
            if (k < 0.8f) return Vector3.Slerp(high, tuck, Sm(KnightTuckAt, KnightTuckAt + 0.12f, k));
            return Vector3.Slerp(tuck, front, Sm(0.8f, 0.97f, k));
        }
    }
}
