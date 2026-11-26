using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>The set 4판's hindrances (Docs/Skills/PAWN_RUSH_SET4_ROUND1.md 3장; the user 11-26: "적의 넘어뜨리거나 띄우는건
    /// 항상 일어나기때문에 그건 cc기로 만들어선 안돼. 뭐 디버프를 받아 변신했거나, 무거워졌거나, 미끄럽게 됐거나, 스테미나 소모가
    /// 심해진다던가, 조작이 몇초동안 안된다거나 뭐 이런 류여야해").</summary>
    /// <remarks>Pinned (11-26, the bishop's 핀!): 묶임 as ROUND1 counts it, but on a 2 m rubber band round where the pin
    /// went in, the keys free inside it, springing back there at the end.</remarks>
    public enum Cc4 : byte { None = 0, Flop = 1, Dizzy = 2, Tired = 3, Heavy = 4, BoundHop = 5, BoundRoot = 6, Slippery = 7, Tiny = 8, Pinned = 9 }

    /// <summary>
    /// The set 4판's seven hindrances (ROUND1 3장): a body state for a few seconds, one at a time - a new one ends the last -
    /// and the keys still work (all but 콰당!). Falling over and flying are not hindrances: they happen as they happen.
    ///
    ///   콰당! (Flop)        flat out, no keys (a knockdown held as long as it lasts)
    ///   헤롱헤롱 (Dizzy)     the way it goes wobbles ±35° back and forth (1.2 s), pushes take it 1.5 times as far
    ///   지침 (Tired)         no stamina and none coming back: no sprint, no dive; jumps 60% as high
    ///   무거움 (Heavy)       runs at 60%, jumps half as high, pushed half as far
    ///   묶임 (BoundHop)      legs tied: no walking, the jump key hops it 1.2 m (one every 0.5 s)
    ///   묶임 (BoundRoot)     feet stuck where it is (a pit, a pin): no walking, no jumping; the arms still work
    ///   미끌미끌 (Slippery)  speeding up, stopping and turning take only 20%
    ///   쪼꼬미 (Tiny)        drawn at half size (PawnRushSkillFx), runs at 70%, jumps 60% as high, pushed twice as far
    ///
    /// The host gives them (GiveCc) and every PC hears of it (SkillFxKind.S4Cc for the look; the kind in Flags3's bits 2-5
    /// for the keys and the speed of a piece its own PC drives). Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        Cc4 cc;
        float ccLeft, ccTotal, ccPhase, ccHopWait;
        Vector3 ccAt;

        /// <summary>The hindrance on this piece now (None when there is none).</summary>
        public Cc4 CurrentCc => ccLeft > 0f ? cc : Cc4.None;
        public float CcLeft => Mathf.Max(0f, ccLeft);
        public float CcTotal => ccTotal;

        PawnRushSkillParams CcParams => PawnRushSkills;

        /// <summary>Host: <paramref name="kind"/> on this piece for <paramref name="seconds"/>, ending whatever it had.
        /// <paramref name="at"/>: where a stuck piece is held (its hips' spot if not given).</summary>
        public void GiveCc(Cc4 kind, float seconds, RagdollPawn by, Vector3? at = null)
        {
            if (NetworkPuppet || kind == Cc4.None || seconds <= 0f) return;
            if (SkillUntargetable || Floating) return;
            EndCc(false);
            cc = kind;
            ccLeft = ccTotal = seconds;
            ccPhase = 0f;
            ccHopWait = 0f;
            ccAt = at ?? bodies[0].position;
            switch (kind)
            {
                case Cc4.Flop:
                    Knockdown("콰당!", seconds);
                    break;
                case Cc4.Tired:
                    if (stamina > 0f) stamina = 0f;
                    Exhausted = true;
                    break;
            }
            // Raised by its giver (the moment's "by"; the skill grades count it as the giver's hit), on this piece (its
            // "target"); one it got by itself (a rook into a wall) is its own.
            (by != null ? by : this).Fx(SkillFxKind.S4Cc, this, bodies[(int)BodyId.Head].position + Vector3.up * 0.3f, Vector3.up, (int)kind, seconds);
            if (by != null) by.LastSkillHitOther = $"{DisplayName}: {CcName(kind)} {seconds:0.#}초";
        }

        /// <summary>For the test bed's log: the last hindrance this piece gave.</summary>
        public string LastSkillHitOther { get; private set; }

        public static string CcName(Cc4 kind) => kind switch
        {
            Cc4.Flop => "콰당!",
            Cc4.Dizzy => "헤롱헤롱",
            Cc4.Tired => "지침",
            Cc4.Heavy => "무거움",
            Cc4.BoundHop => "묶임",
            Cc4.BoundRoot => "묶임",
            Cc4.Pinned => "묶임",
            Cc4.Slippery => "미끌미끌",
            Cc4.Tiny => "쪼꼬미",
            _ => "",
        };

        /// <summary>It ends (and every PC hears so, unless <paramref name="quiet"/>: a new one is about to be told).</summary>
        void EndCc(bool tell = true)
        {
            if (ccLeft <= 0f && cc == Cc4.None) return;
            bool was = ccLeft > 0f;
            ccLeft = 0f;
            cc = Cc4.None;
            if (tell && was && !NetworkPuppet) Fx(SkillFxKind.S4Cc, this, bodies[(int)BodyId.Head].position, Vector3.up, 0, 0f);
        }

        /// <summary>Every step (UpdateKitTwoStates; on the PC that drives this piece): the time runs out; the water or a
        /// respawn ends it.</summary>
        void UpdateCc(float dt)
        {
            if (ccLeft <= 0f) return;
            ccPhase += dt;
            ccHopWait -= dt;
            if (!NetworkPuppet)
            {
                ccLeft -= dt;
                if (ccLeft <= 0f || Floating || skillGuardLeft > 0f)
                {
                    // A pin's band, run out: it springs back to where the pin went in.
                    bool snap = cc == Cc4.Pinned && ccLeft <= 0f && !Floating && State == PawnState.Active;
                    Vector3 home = ccAt;
                    EndCc();
                    if (snap && Flat(home - FeetPoint).magnitude > 0.4f) Launch(ArcTo4(FeetPoint, home, 0.7f, out _));
                    return;
                }
            }
            if (cc == Cc4.Tired)
            {
                if (stamina > 0f) stamina = 0f;
                Exhausted = true;
            }
            if (cc == Cc4.BoundRoot && State == PawnState.Active) HoldCcSpot(dt);
            if (cc == Cc4.Pinned && State == PawnState.Active) HoldCcLeash();
        }

        /// <summary>On a pin's band: free inside its length, pulled back past it (harder the farther).</summary>
        void HoldCcLeash()
        {
            float r = CcParams != null ? Mathf.Max(0.3f, CcParams.pinLeash) : 2f;
            Vector3 off = Flat(bodies[0].position - ccAt);
            float d = off.magnitude;
            if (d <= r) return;
            Vector3 back = -off / d;
            Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
            float outward = Mathf.Max(0f, -Vector3.Dot(own, back));
            AddVelocity(back * (outward + Mathf.Min(4f, (d - r) * 6f)));
        }

        /// <summary>Stuck where it was caught: pulled back there, standing (as the R138 rope holds).</summary>
        void HoldCcSpot(float dt)
        {
            Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
            Vector3 want = Vector3.ClampMagnitude(Flat(ccAt - bodies[0].position) * 8f, 2f);
            AddVelocity(want - own);
        }

        /// <summary>Before the run (PreSkills): what the hindrance does to the keys.</summary>
        void CcKeys()
        {
            if (ccLeft <= 0f) return;
            var s = CcParams;
            switch (cc)
            {
                case Cc4.Dizzy:
                    if (input.move.sqrMagnitude > 0.01f)
                    {
                        float period = s != null ? Mathf.Max(0.2f, s.ccDizzyPeriod) : 1.2f;
                        float angle = (s != null ? s.ccDizzyAngle : 35f) * Mathf.Sin(ccPhase * Mathf.PI * 2f / period);
                        input.move = Quaternion.AngleAxis(angle, Vector3.up) * input.move;
                    }
                    break;
                case Cc4.Tired:
                    input.sprint = false;
                    input.shove = false;     // no dive
                    input.shoveHeld = false;
                    break;
                case Cc4.BoundHop:
                {
                    // Legs tied: it goes only by hops, the jump key's, the way the move keys say (or the way it faces).
                    Vector3 way = Flat(input.move).sqrMagnitude > 0.01f ? Flat(input.move).normalized : FlatDir(facing);
                    if (input.jump && ccHopWait <= 0f && Grounded && State == PawnState.Active)
                    {
                        float g = Mathf.Max(0.01f, -Physics.gravity.y);
                        float up = 2.6f, air = 2f * up / g;
                        float dist = s != null ? s.ccHopDist : 1.2f;
                        Launch(way * (dist / air) + Vector3.up * up);
                        ccHopWait = s != null ? s.ccHopEvery : 0.5f;
                        Fx(SkillFxKind.S4Hop, this, FeetPoint, way, 0, 0f);
                    }
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    break;
                }
                case Cc4.BoundRoot:
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    break;
            }
        }

        /// <summary>The run's top speed under the hindrance (SkillMoveScale).</summary>
        float CcMoveScale
        {
            get
            {
                if (ccLeft <= 0f) return 1f;
                var s = CcParams;
                return cc switch
                {
                    Cc4.Heavy => s != null ? s.ccHeavyMove : 0.6f,
                    Cc4.Tiny => s != null ? s.ccTinyMove : 0.7f,
                    _ => 1f,
                };
            }
        }

        /// <summary>How high its jumps go under the hindrance (SkillJumpScale).</summary>
        float CcJumpScale
        {
            get
            {
                if (ccLeft <= 0f) return 1f;
                var s = CcParams;
                return cc switch
                {
                    Cc4.Tired => s != null ? s.ccTiredJump : 0.6f,
                    Cc4.Heavy => s != null ? s.ccHeavyJump : 0.5f,
                    Cc4.Tiny => s != null ? s.ccTinyJump : 0.6f,
                    _ => 1f,
                };
            }
        }

        /// <summary>How far pushes take it under the hindrance (PushScale).</summary>
        float CcPushScale
        {
            get
            {
                if (ccLeft <= 0f) return 1f;
                var s = CcParams;
                return cc switch
                {
                    Cc4.Dizzy => s != null ? s.ccDizzyPush : 1.5f,
                    Cc4.Heavy => s != null ? s.ccHeavyPush : 0.5f,
                    Cc4.Tiny => s != null ? s.ccTinyPush : 2f,
                    _ => 1f,
                };
            }
        }

        /// <summary>How much speeding up, stopping and turning take (the run's acceleration): slippery feet.</summary>
        float CcAccelScale => ccLeft > 0f && cc == Cc4.Slippery ? (CcParams != null ? Mathf.Clamp(CcParams.ccSlipAccel, 0.02f, 1f) : 0.2f) : 1f;

        /// <summary>Gone with the skills' states (a reset, a respawn): every PC hears it ended (its look stops).</summary>
        void ClearCc()
        {
            bool had = ccLeft > 0f;
            ccLeft = 0f;
            cc = Cc4.None;
            if (had && !NetworkPuppet && bodies != null && bodies.Length > 0 && bodies[0] != null)
                Fx(SkillFxKind.S4Cc, this, bodies[0].position, Vector3.up, 0, 0f);
        }

        // ---------------------------------------------------------------- over the network (RagdollPawn.SkillNet.cs)

        // Flags3 bits 2-5 (1 and 2 are light and heavy): the hindrance's kind.
        const int NetCcShift = 2, NetCcMask = 15;

        bool CcNetBusy => ccLeft > 0f;

        void CaptureCcNet(ref SkillWireState s)
        {
            if (ccLeft > 0f) s.Flags3 |= (byte)(((int)cc & NetCcMask) << NetCcShift);
        }

        void ApplyCcNet(in SkillWireState s)
        {
            var kind = (Cc4)((s.Flags3 >> NetCcShift) & NetCcMask);
            if (kind == Cc4.None)
            {
                ccLeft = 0f;
                cc = Cc4.None;
                return;
            }
            if (cc != kind || ccLeft <= 0f)
            {
                cc = kind;
                ccPhase = 0f;
                ccLeft = ccTotal = 3f;   // (this PC's own count until the host's state says it is over)
                ccAt = bodies[0].position;
            }
        }

        void AdvanceCcView(float dt)
        {
            if (ccLeft > 0f)
            {
                ccLeft = Mathf.Max(0.05f, ccLeft - dt);
                ccPhase += dt;
            }
        }
    }
}
