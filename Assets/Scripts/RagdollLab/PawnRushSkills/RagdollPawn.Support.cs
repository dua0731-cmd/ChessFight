using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The support slot (JY-skill-lab, 2026-11-22; Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.1-6.5 "지원" and 6.10; the user: "선두여도
    /// 꼴찌 유저를 지원해서 빠르게 데려오는 게 본인 팀에 이득이 되도록 설계해야 해"): the team's leader alone, on G, brings its last
    /// runner up to it - never a teleport: the last runner rides, hangs or is thrown there with its own body, and enemies can
    /// cut it on the way.
    ///
    ///   when       G lights for the team's leader while its last runner is in another section, the team has not used it in
    ///              the leader's section and the last runner is not in a mini-game (RaceSection, InMiniGame: the course's to
    ///              say; without them a section is every supSection metres of the race)
    ///   the call   the last runner's screen says "선두가 부른다!" and what comes; it answers with F within supAnswer s (a test
    ///              dummy by itself). No answer, or the leader letting go first, calls it off: the section's use is kept
    ///   how far    toward the leader, supReach m at most, never past it; stopped at the door of a mini-game it has not done
    ///              (MissionGate)
    ///   the leader floored by nothing while it stands for it, only pushed: the knight's whistle (1 s), the rook's, the queen's
    ///              and the pawn's held G, the bishop's drive of its drone
    ///   cut        the carried runner floored (an enemy's hit), the rope or a chain's pawn struck, the leader letting go or
    ///              pushed off its spot: the runner stays where it fell, and the section's use is spent
    ///   arrived    both tied by a teal light and supBoostScale fast for supBoost s (side by side: both sent supDash m ahead),
    ///              the team's ultimate gauge +supGauge, "구조 !!" on every screen, one more rescue for the leader
    ///
    ///   knight     an empty horse runs to it (60 m/s); it rides back (30 m/s), steering with its keys, leaping with the jump
    ///              key, holding on with F; the leader is free after its 1 s whistle
    ///   rook       a drawbridge from the last runner toward the leader; its floor carries whoever is on it at 35 m/s (the
    ///              teammates on it too), enemies slip off it; the leader holds the chain (G)
    ///   queen      a rope of light from 15 m over the last runner down to the leader; it climbs to the crown pulley and slides
    ///              (45 m/s, the fastest), and any enemy at the rope cuts it (the easiest to cut); the leader holds its end (G)
    ///   bishop     a mitre drone flies over it (60 m/s); it hangs on, the leader drives it with its keys and camera (30 m/s, 6 s
    ///              at most) and G again sets it down
    ///   pawn       stone pawns rise in a zigzag chain every 10 m and throw it from one to the next (0.3 s a throw); an enemy
    ///              at a pawn ahead topples it and it falls through the gap; the leader holds the chain's end (G)
    ///   (The king's call over a section stays in the documents only: there is no king in Pawn Rush.)
    ///
    /// The host's; every PC draws it from the moments (SupCall, SupGo, SupStep, SupCut, SupCancel, SupSaved) and the pieces'
    /// bodies (PawnRushSkillFx.Support.cs). Not seen in Unity yet.
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>Which section of the race a piece is in, the course's to say. Null: every supSection metres of progress.</summary>
        public static Func<RagdollPawn, int> RaceSection;
        /// <summary>Whether a piece is inside a mini-game now (the course's to say). Null: never.</summary>
        public static Func<RagdollPawn, bool> InMiniGame;
        /// <summary>The progress (m) of the door of the first mini-game the last runner has not done between it and the leader
        /// (NaN for none; the course's to say). Null: none.</summary>
        public static Func<RagdollPawn, RagdollPawn, float> MissionGate;
        /// <summary>The test dummies answer a support's call by themselves (standing in for another PC's F).</summary>
        public static bool SupportBotsAnswer = true;

        static readonly HashSet<long> supUsed = new HashSet<long>();
        static long SupKey(int team, int section) => ((long)team << 32) ^ (uint)section;

        /// <summary>Every team's sections usable again (a new round, the test bed starting).</summary>
        public static void ResetSupport() => supUsed.Clear();

        /// <summary>A support went in a team's section (every PC: the host on the spot, the others from its moment).</summary>
        public static void NoteSupportUsed(int team, int section)
        {
            if (team != Teams.None) supUsed.Add(SupKey(team, section));
        }

        /// <summary>A piece of Pawn Rush that has a support (no king).</summary>
        public static bool HasSupport(PieceKind k) => k == PieceKind.Pawn || k == PieceKind.Knight || k == PieceKind.Bishop || k == PieceKind.Rook || k == PieceKind.Queen;

        /// <summary>The support's name for a piece.</summary>
        public static string SupportName(PieceKind k) => k switch
        {
            PieceKind.Knight => "말 보내기",
            PieceKind.Rook => "도개교",
            PieceKind.Queen => "퀸의 줄",
            PieceKind.Bishop => "주교관 드론",
            PieceKind.Pawn => "폰 사슬",
            _ => "지원 없음",
        };

        /// <summary>Which section of the race this piece is in now.</summary>
        public int SectionNow => RaceSection != null ? RaceSection(this)
                                 : Mathf.FloorToInt(Progress(this) / Mathf.Max(1f, PawnRushSkills != null ? PawnRushSkills.supSection : 100f));

        // The leader's: the one it called (waiting for its answer, or on its way), the whistle's time, the drone's drive, the
        // G key's last state and its press, where it stands holding, the drone and its drive keys.
        RagdollPawn supMate;
        float supStandLeft, supDriveLeft, supSlipNext;
        bool supHeldWas, supPress, supTap;
        Vector3 supHoldAt, supDrone, supDriveMove, supDriveAim;
        // The carried one's: the leader, the leader's piece at the call, the answer's time, since the call (then since it
        // went), when what comes is there, metres carried, how long the body has been off the carry point, F let go for how
        // long, a hop's or a leap's time; called (or riding along a bridge), carried; where from and to, the rope's top and
        // end, the horse's heading, the carry point, a leap's or a hop's ends; the chain's pawns and their gaps.
        RagdollPawn supBy;
        int supKind, supHop, supSection;
        float supWait, supT, supReadyAt, supRun, supLag, supGripOff, supHopT, supJumpT = -1f;
        bool supCalled, supCarry, supJumpPress;
        Vector3 supFrom, supTo, supTop, supEnd, supHead, supPos, supJumpFrom, supJumpTo, supSteer;
        readonly List<Vector3> supChain = new List<Vector3>();
        readonly List<bool> supGap = new List<bool>();
        // The reward: its time and the one it is tied to; the leader's rescues.
        float supBoostLeft;
        RagdollPawn supTether;

        /// <summary>The rescues this piece has made (the result screen's count).</summary>
        public int Rescues { get; private set; }
        /// <summary>A support's reward on this piece now: its time left, and the one it is tied to.</summary>
        public float SupportBoostLeft => Mathf.Max(0f, supBoostLeft);
        public RagdollPawn SupportTether => supBoostLeft > 0f ? supTether : null;
        /// <summary>The reward's speed (SkillMoveScale).</summary>
        float SupportScale => supBoostLeft > 0f && PawnRushSkills != null ? PawnRushSkills.supBoostScale : 1f;
        /// <summary>This piece is carried by a support now (the host's).</summary>
        public bool SupportCarried => supCarry;
        /// <summary>The leader whose call is on this piece (waiting or carrying; the host's).</summary>
        public RagdollPawn SupportCaller => supBy;
        /// <summary>The one this leader called (the host's).</summary>
        public RagdollPawn SupportMate => supMate;
        /// <summary>The bishop's drone (the host's; others draw it over the one hanging on).</summary>
        public Vector3 SupportDrone => supDrone;
        /// <summary>The drive's time left (the bishop's drone).</summary>
        public float SupportDriveLeft => Mathf.Max(0f, supDriveLeft);

        // ---------------------------------------------------------------- who, and whether it lights

        /// <summary>A team's leader and last runner by the race (the pieces with Pawn Rush skills; no last without a teammate).</summary>
        public static void TeamEnds(int team, out RagdollPawn first, out RagdollPawn last)
        {
            first = last = null;
            if (team == Teams.None) return;
            float hi = float.MinValue, lo = float.MaxValue;
            foreach (var p in All)
            {
                if (p == null || p.team != team || p.PawnRushSkills == null) continue;
                float pr = Progress(p);
                if (pr > hi)
                {
                    hi = pr;
                    first = p;
                }
                if (pr < lo)
                {
                    lo = pr;
                    last = p;
                }
            }
            if (first == last) last = null;
        }

        /// <summary>G lights (any PC, from the pieces' places and the uses known): see <see cref="SupportWhyNot"/>.</summary>
        public bool SupportLit => SupportWhyNot(out _) == null;

        /// <summary>Why the support cannot go now (null: it can), and whom it would call: this piece must be its team's leader,
        /// the last runner in another section and not in a mini-game, the team's use of this section not spent, nothing of
        /// this piece's under way.</summary>
        public string SupportWhyNot(out RagdollPawn last)
        {
            last = null;
            if (PawnRushSkills == null || team == Teams.None) return "팀 없음";
            if (!HasSupport(piece)) return "지원 없음";
            if (supMate != null || supBy != null) return "지원 중";
            foreach (var p in All)
                if (p != null && p != this && p.team == team && (p.supMate != null || p.supCarry)) return "팀이 지원 중";
            if (State != PawnState.Active || SkillRunning || bindLeft > 0f || kingCarry) return "바쁨";
            TeamEnds(team, out var first, out last);
            if (first != this || last == null) return "선두가 아님";
            int section = SectionNow;
            if (last.SectionNow == section) return "꼴찌가 같은 구간";
            if (InMiniGame != null && InMiniGame(last)) return "꼴찌가 미니게임 중";
            if (supUsed.Contains(SupKey(team, section))) return "이 구간에서 씀";
            if (last.supBy != null) return "꼴찌가 이미 불림";
            return null;
        }

        // ---------------------------------------------------------------- the keys (PreSkills)

        /// <summary>Before the run: G's press (from its held state; every step), the leader standing for its support, the carried
        /// one's keys taken (its steering, its jump and its grip kept for the horse).</summary>
        void SupportKeys()
        {
            bool held = input.supportHeld || supTap;   // (a tap since the last step counts)
            supTap = false;
            if (held && !supHeldWas) supPress = true;
            supHeldWas = held;
            if (supMate != null && SupportStanding)
            {
                if (piece == PieceKind.Bishop)
                {
                    supDriveMove = input.move;
                    supDriveAim = input.aim;
                }
                input.move = Vector3.zero;
                input.jump = false;
                input.sprint = false;
                input.shove = false;
                input.shoveHeld = false;
                input.grab = false;
                input.ability = input.ability2 = input.ability3 = false;
            }
            if (supCarry)
            {
                supSteer = input.move;
                supJumpPress |= input.jump;
                input.move = Vector3.zero;
                input.jump = false;
                input.sprint = false;
                input.shove = false;
                input.shoveHeld = false;
                input.grab = false;
                input.ability = input.ability2 = input.ability3 = false;
            }
        }

        /// <summary>The leader stands for its support now (rooted, floored by nothing).</summary>
        bool SupportStanding => supMate != null && (piece == PieceKind.Knight ? supStandLeft > 0f : true);

        // ---------------------------------------------------------------- the leader (UpdateSkills, the host's)

        /// <summary>Every step: the reward's time; G's press calls the last runner (or says no); while the call or the carry
        /// lasts, the leader stands for it - holding G for the rook, the queen and the pawn (let go: it ends), pushed off its
        /// spot (it ends), the whistle's second for the knight, the drone for the bishop. Returns whether the skill key was
        /// used (the leader's own skills wait while it stands).</summary>
        bool UpdateSupportLeader(PawnRushSkillParams s, bool press, float dt)
        {
            if (supBoostLeft > 0f) supBoostLeft -= dt;
            bool g = supPress;
            supPress = false;
            var mate = supMate;
            if (mate == null)
            {
                if (!g || NetworkPuppet) return press;
                string why = SupportWhyNot(out var last);
                if (why == null) StartSupport(s, last);
                else
                {
                    Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)piece);
                    Log($"{DisplayName}: 지원 못 씀 ({why})");
                }
                return press;
            }
            if (mate.supBy != this)
            {
                EndSupportLeader();
                return press;
            }
            if (supStandLeft > 0f) supStandLeft -= dt;
            if (SupportStanding)
            {
                getUpGuardLeft = Mathf.Max(getUpGuardLeft, 0.25f);   // only pushed, never floored, while it stands for it
                press = false;
            }
            bool hold = piece == PieceKind.Rook || piece == PieceKind.Queen || piece == PieceKind.Pawn;
            if (hold)
            {
                if (!input.supportHeld)
                {
                    mate.SupportStop(mate.supCarry, "선두가 놓음");
                    return press;
                }
                if (Flat(bodies[0].position - supHoldAt).magnitude > s.supHoldSlip)
                {
                    mate.SupportStop(mate.supCarry, "선두가 밀려남");
                    return press;
                }
            }
            if (piece == PieceKind.Rook) SupportBridgeSlip(s, mate);
            if (piece == PieceKind.Bishop) SupportDroneStep(s, mate, g, dt);
            return press;
        }

        void EndSupportLeader()
        {
            supMate = null;
            supStandLeft = 0f;
        }

        /// <summary>G: the call. Where the last runner is taken (toward the leader, supReach at most, never past it, stopped at a
        /// mini-game's door), what comes for it (each piece's), and the moment every PC draws it from.</summary>
        void StartSupport(PawnRushSkillParams s, RagdollPawn last)
        {
            supMate = last;
            supHoldAt = bodies[0].position;
            supStandLeft = piece == PieceKind.Knight ? s.supWhistle : 0f;
            supDriveLeft = s.supDroneTime;
            last.SupportClear();
            last.supBy = this;
            last.supKind = (int)piece;
            last.supCalled = true;
            last.supWait = s.supAnswer;
            last.supSection = SectionNow;
            Vector3 from = last.FeetPoint, to = FeetPoint, d = Flat(to - from);
            float dist = d.magnitude, reach = Mathf.Min(s.supReach, Mathf.Max(0f, dist - 2.5f));
            float gate = MissionGate != null ? MissionGate(last, this) : float.NaN;
            if (!float.IsNaN(gate))
            {
                float pl = Progress(last), pf = Progress(this);
                if (pf - pl > 1f) reach = Mathf.Min(reach, Mathf.Max(0f, dist * Mathf.Clamp01((gate - pl) / (pf - pl)) - 1f));
            }
            Vector3 dir = dist > 0.01f ? d / dist : FlatDir(facing);
            Vector3 dest = from + dir * reach;
            var floor = DriveFloor(dest, 3f, 8f);
            dest = floor ?? new Vector3(dest.x, Mathf.Lerp(from.y, to.y, reach / Mathf.Max(1f, dist)), dest.z);
            last.supFrom = from;
            last.supTo = dest;
            Vector3 at = FeetPoint, far = dest;
            switch (piece)
            {
                case PieceKind.Knight:
                    last.supReadyAt = Flat(from - FeetPoint).magnitude / Mathf.Max(1f, s.supHorseGo);
                    break;
                case PieceKind.Rook:
                    last.supReadyAt = s.supBridgeOpen;
                    at = from;
                    break;
                case PieceKind.Queen:
                    last.supTop = last.bodies[0].position + Vector3.up * s.supRopeHigh;
                    last.supEnd = dest + Vector3.up * (last.standHeight + 1.1f);
                    last.supReadyAt = s.supRopeShoot;
                    at = last.supTop;
                    far = last.supEnd;
                    break;
                case PieceKind.Bishop:
                    supDrone = bodies[(int)BodyId.Head].position + Vector3.up * 2f;
                    last.supReadyAt = Vector3.Distance(supDrone, last.bodies[(int)BodyId.Head].position + Vector3.up * 3f) / Mathf.Max(1f, s.supDroneGo);
                    at = supDrone;
                    break;
                case PieceKind.Pawn:
                    SupportChainPoints(from, dest, s, last.supChain);
                    last.supGap.Clear();
                    for (int i = 0; i < last.supChain.Count; i++) last.supGap.Add(false);
                    last.supReadyAt = s.supChainRise;
                    at = from;
                    break;
            }
            last.supWait = Mathf.Max(s.supAnswer, last.supReadyAt + 2f);   // (a far horse or drone: 2 s to answer after it is there)
            Fx(SkillFxKind.SupCall, last, at, dir, (int)piece, last.supWait, far);
            Log($"{DisplayName}: 지원 {SupportName(piece)} — {last.DisplayName}를 부름 ({dist:0} m 뒤, {reach:0} m 데려옴, {last.supWait:0}초 안에 F)");
        }

        /// <summary>The pawn's chain: a stone pawn every supChainGap m from <paramref name="from"/> to <paramref name="to"/>
        /// (supChainMax at most), zigzagging supChainZig to either side, the last one on the destination, each on its floor.
        /// The same on every PC (the effects draw it from the call's two ends).</summary>
        public static void SupportChainPoints(Vector3 from, Vector3 to, PawnRushSkillParams s, List<Vector3> into)
        {
            into.Clear();
            Vector3 d = Flat(to - from);
            float len = d.magnitude;
            if (len < 1f || s == null) return;
            Vector3 dir = d / len, side = new Vector3(dir.z, 0f, -dir.x);
            int n = Mathf.Clamp(Mathf.CeilToInt(len / Mathf.Max(1f, s.supChainGap)), 1, Mathf.Max(1, s.supChainMax));
            for (int i = 1; i <= n; i++)
            {
                float x = len * i / n;
                Vector3 p = from + dir * x + side * (i == n ? 0f : (i % 2 == 0 ? -1f : 1f) * s.supChainZig);
                var floor = DriveFloor(p, 3f, 8f);
                into.Add(floor ?? new Vector3(p.x, Mathf.Lerp(from.y, to.y, x / len), p.z));
            }
        }

        // ---------------------------------------------------------------- the one called (UpdateSkills, the host's)

        /// <summary>A support's call on this piece this step: its own key answers yes once what comes is there (a test dummy by
        /// itself), the call runs out, or the carry runs. Returns the key press if it was not used.</summary>
        bool UpdateSupportMate(PawnRushSkillParams s, bool press, float dt)
        {
            var by = supBy;
            if (by == null) return press;
            if (supCarry)
            {
                SupportCarryStep(s, dt);
                return false;
            }
            if (by.supMate != this)
            {
                SupportClear();
                return press;
            }
            supWait -= dt;
            supT += dt;
            if (supWait <= 0f)
            {
                SupportStop(false, "대답 없음");
                return press;
            }
            bool yes = SkillBot ? SupportBotsAnswer && supT >= Mathf.Max(0.6f, supReadyAt + 0.2f) : press;
            if (!yes) return press;
            // Not there yet, down, tied, held or in the water: the answer waits (the key is used up).
            if (supT < supReadyAt || State != PawnState.Active || bindLeft > 0f || BeingHeld || Floating) return false;
            SupportGo(s);
            return false;
        }

        /// <summary>The answer: the section's use is spent, its own skills stop, and it is on its way (the rook's bridge takes
        /// its teammates on it too).</summary>
        void SupportGo(PawnRushSkillParams s)
        {
            var by = supBy;
            NoteSupportUsed(by.team, supSection);
            ClearSkills();
            DropHook();
            DropRope();
            EndClimbPose();
            supCarry = true;
            supT = supRun = supLag = supGripOff = supHopT = 0f;
            supJumpT = -1f;
            supJumpPress = false;
            supHop = 0;
            supPos = bodies[0].position;
            supJumpFrom = supPos;
            supHead = FlatDir(supTo - supPos);
            if ((PieceKind)supKind == PieceKind.Rook)
            {
                supRun = SupportAlong(supFrom, supTo, supPos);
                foreach (var p in All)
                {
                    if (p == null || p == this || p == by || p.team != by.team || p.supBy != null || p.State != PawnState.Active || p.PawnRushSkills == null) continue;
                    if (SupportOffLine(supFrom, supTo, p.FeetPoint) > s.supBridgeWidth * 0.5f + 0.6f) continue;
                    p.SupportRide(by, this);
                }
            }
            by.Fx(SkillFxKind.SupGo, this, supPos, supHead, supSection, supKind);
            Log($"{DisplayName}: {SupportName((PieceKind)supKind)}에 탐 → {by.DisplayName} 쪽으로");
        }

        /// <summary>A teammate on the rook's bridge when it went: carried along it too (no reward of its own).</summary>
        void SupportRide(RagdollPawn by, RagdollPawn called)
        {
            SupportClear();
            supBy = by;
            supKind = called.supKind;
            supCalled = false;
            supFrom = called.supFrom;
            supTo = called.supTo;
            ClearSkills();
            supCarry = true;
            supT = supLag = 0f;
            supPos = bodies[0].position;
            supRun = SupportAlong(supFrom, supTo, supPos);
            by.Fx(SkillFxKind.SupGo, this, supPos, FlatDir(supTo - supFrom), -1, supKind);
        }

        static float SupportAlong(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = Flat(b - a);
            float len = ab.magnitude;
            return len < 1e-3f ? 0f : Mathf.Clamp(Vector3.Dot(Flat(p - a), ab / len), 0f, len);
        }

        static float SupportOffLine(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = Flat(b - a);
            float len = ab.magnitude;
            if (len < 1e-3f) return Flat(p - a).magnitude;
            float x = Mathf.Clamp(Vector3.Dot(Flat(p - a), ab / len), 0f, len);
            return Flat(p - (a + ab / len * x)).magnitude;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        // ---------------------------------------------------------------- on its way

        /// <summary>One step of the carry: each piece's way of bringing it, the carry point flown to; floored on the way (an
        /// enemy's hit) or held back from the point too long, it is cut.</summary>
        void SupportCarryStep(PawnRushSkillParams s, float dt)
        {
            supT += dt;
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            if (State != PawnState.Active)
            {
                SupportStop(true, "넘어짐");
                return;
            }
            Vector3 hips = bodies[0].position, next;
            switch ((PieceKind)supKind)
            {
                case PieceKind.Knight: if (!HorseStep(s, dt, hips, out next)) return; break;
                case PieceKind.Rook: if (!BridgeStep(s, dt, out next)) return; break;
                case PieceKind.Queen: if (!RopeStep(s, dt, hips, out next)) return; break;
                case PieceKind.Bishop: if (!DroneHang(s, out next)) return; break;
                default: if (!ChainStep(s, dt, out next)) return; break;
            }
            Fly(next, (next - supPos) / Mathf.Max(1e-4f, dt), 0f, default, 18f);
            supPos = next;
            if ((hips - next).magnitude > 4f)
            {
                supLag += dt;
                if (supLag > 0.4f) SupportStop(true, "걸림");
            }
            else supLag = 0f;
        }

        /// <summary>The knight's horse: 30 m/s along its own steering (toward the leader without it), on the floor; the jump key
        /// a big leap over what is in the way; F held to stay on. Arrived near the leader, at the reach or past the leader.</summary>
        bool HorseStep(PawnRushSkillParams s, float dt, Vector3 hips, out Vector3 next)
        {
            next = hips;
            var by = supBy;
            if (!SkillBot && !input.skillHeld)
            {
                supGripOff += dt;
                if (supGripOff > s.supGripGrace)
                {
                    SupportStop(true, "말에서 떨어짐 (F를 놓음)");
                    return false;
                }
            }
            else supGripOff = 0f;
            Vector3 toDest = Flat(supTo - hips);
            float left = toDest.magnitude;
            if (left < 3f || supRun >= s.supReach || (by != null && Progress(this) >= Progress(by) - 3f))
            {
                SupportArrive(s);
                return false;
            }
            bool steered = supSteer.sqrMagnitude > 0.04f;
            Vector3 want = steered ? Flat(supSteer).normalized : toDest / Mathf.Max(0.01f, left);
            supHead = Vector3.RotateTowards(supHead, want, (steered ? 150f : 70f) * Mathf.Deg2Rad * dt, 0f);
            supHead = FlatDir(supHead);
            Face(supHead);
            if (supJumpPress && supJumpT < 0f)
            {
                supJumpT = 0f;
                supJumpFrom = supPos;
                supJumpTo = supPos + supHead * s.supHorseJump;
                (by ?? this).Fx(SkillFxKind.SupStep, this, supPos, supHead, 1, s.supHorseJumpTime);   // (by the leader: its show)
            }
            supJumpPress = false;
            float stepLen = s.supHorseRide * dt;
            if (supJumpT >= 0f)
            {
                supJumpT += dt;
                float u = Mathf.Clamp01(supJumpT / Mathf.Max(0.05f, s.supHorseJumpTime));
                next = Vector3.Lerp(supJumpFrom, supJumpTo, u) + Vector3.up * (4f * s.supHorseJumpTop * u * (1f - u));
                if (u >= 1f)
                {
                    supJumpT = -1f;
                    var land = DriveFloor(next, 3f, 10f);
                    if (land.HasValue) next = land.Value + Vector3.up * (standHeight + 0.6f);
                }
            }
            else
            {
                Vector3 ahead = supPos + supHead * stepLen;
                var floor = DriveFloor(ahead, 1.5f, 6f);
                if (!floor.HasValue)
                {
                    SupportStop(true, "길 끝");
                    return false;
                }
                next = floor.Value + Vector3.up * (standHeight + 0.6f);
            }
            supRun += stepLen;
            return true;
        }

        /// <summary>The rook's drawbridge: along it toward its near end at 35 m/s (a straight plank between its two ends).</summary>
        bool BridgeStep(PawnRushSkillParams s, float dt, out Vector3 next)
        {
            Vector3 a = supFrom, b = supTo;
            float len = Flat(b - a).magnitude;
            supRun += s.supBridgeSpeed * dt;
            float x = Mathf.Min(len, supRun), k = len > 1e-3f ? x / len : 1f;
            next = Vector3.Lerp(a, b, k) + Vector3.up * (standHeight + 0.35f);
            Face(FlatDir(b - a));
            if (x < len) return true;
            if (supCalled) SupportArrive(s);
            else EndSupportCarry(true);
            return false;
        }

        /// <summary>The queen's rope: up to the crown pulley over it, then down along the rope at 45 m/s, hanging under it; any
        /// enemy at the rope ahead cuts it.</summary>
        bool RopeStep(PawnRushSkillParams s, float dt, Vector3 hips, out Vector3 next)
        {
            next = hips;
            Vector3 top = supTop - Vector3.up * 1.1f, end = supEnd - Vector3.up * 1.1f;
            float lift = Mathf.Max(0.05f, s.supRopeLift);
            Vector3 pulley;
            if (supT < lift)
            {
                float k = Mathf.SmoothStep(0f, 1f, supT / lift);
                next = Vector3.Lerp(supJumpFrom, top, k);
                pulley = supTop;
            }
            else
            {
                float len = Vector3.Distance(top, end);
                supRun = Mathf.Min(len, supRun + s.supRopeSpeed * dt);
                next = Vector3.Lerp(top, end, len > 1e-3f ? supRun / len : 1f);
                pulley = next + Vector3.up * 1.1f;
                Face(FlatDir(end - top));
                if (supRun >= len)
                {
                    SupportArrive(s);
                    return false;
                }
            }
            foreach (var e in All)
            {
                if (e == null || e.team == team || !UltFoe(e)) continue;
                if (SegmentDistance(e.bodies[0].position, pulley, supEnd) <= s.supCutReach)
                {
                    SupportStop(true, $"{e.DisplayName}가 줄을 끊음");
                    return false;
                }
            }
            return true;
        }

        /// <summary>The bishop's drone: it hangs under it (the leader drives it: SupportDroneStep).</summary>
        bool DroneHang(PawnRushSkillParams s, out Vector3 next)
        {
            var by = supBy;
            if (by == null)
            {
                next = bodies[0].position;
                SupportStop(true, "드론이 사라짐");
                return false;
            }
            if (!SkillBot && !input.skillHeld)
            {
                supGripOff += Time.fixedDeltaTime;
                if (supGripOff > s.supGripGrace)
                {
                    next = bodies[0].position;
                    SupportStop(true, "드론에서 떨어짐 (F를 놓음)");
                    return false;
                }
            }
            else supGripOff = 0f;
            next = by.supDrone - Vector3.up * s.supDroneHang;
            return true;
        }

        /// <summary>The leader's drone: flying to over the one called and hovering there until it hangs on; then driven by the
        /// leader's keys and camera (30 m/s, its height kept over the floor), set down by G again, near the leader or when its
        /// time is up.</summary>
        void SupportDroneStep(PawnRushSkillParams s, RagdollPawn mate, bool g, float dt)
        {
            if (!mate.supCarry)
            {
                Vector3 over = mate.bodies[(int)BodyId.Head].position + Vector3.up * 3f;
                supDrone = Vector3.MoveTowards(supDrone, over, s.supDroneGo * dt);
                return;
            }
            supDriveLeft -= dt;
            Vector3 flat = Flat(supDriveMove);
            Vector3 vel = flat.sqrMagnitude > 0.01f ? flat.normalized * s.supDroneDrive * Mathf.Clamp01(flat.magnitude) : Vector3.zero;
            if (flat.sqrMagnitude > 0.01f && supDriveAim.sqrMagnitude > 0.1f) vel.y = Mathf.Clamp(supDriveAim.y, -0.8f, 0.8f) * s.supDroneDrive * Mathf.Clamp01(flat.magnitude);
            Vector3 nextDrone = supDrone + vel * dt;
            float ground = float.NaN;
            if (Physics.Raycast(nextDrone + Vector3.up * 0.5f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<RagdollPawn>() == null)
                ground = hit.point.y;
            if (!float.IsNaN(ground)) nextDrone.y = Mathf.Clamp(nextDrone.y, ground + s.supDroneHang + s.supDroneLow, ground + s.supDroneHigh);
            supDrone = nextDrone;
            bool near = Flat(supDrone - bodies[0].position).magnitude <= s.supDroneDrop;
            if (g || near || supDriveLeft <= 0f) mate.SupportDrop(s);
        }

        /// <summary>The drone sets it down: arrived when near its leader (or it was brought most of the way), else just let go.</summary>
        void SupportDrop(PawnRushSkillParams s)
        {
            var by = supBy;
            bool arrived = by != null && (Flat(by.bodies[0].position - bodies[0].position).magnitude <= 12f || Flat(supTo - bodies[0].position).magnitude <= 6f);
            if (arrived) SupportArrive(s);
            else
            {
                EndSupportCarry(true);
                if (by != null)
                {
                    by.Fx(SkillFxKind.SupStep, this, bodies[0].position, Vector3.down, 2, 0f);
                    by.EndSupportLeader();
                }
                SupportClear();
            }
        }

        /// <summary>The pawn's chain: thrown from one stone pawn to the next (a hop of supChainHop s); an enemy at a pawn still
        /// ahead topples it, and the throw into that gap drops it there.</summary>
        bool ChainStep(PawnRushSkillParams s, float dt, out Vector3 next)
        {
            next = bodies[0].position;
            var by = supBy;
            for (int i = supHop; i < supChain.Count; i++)
            {
                if (supGap[i]) continue;
                foreach (var e in All)
                {
                    if (e == null || e.team == team || !UltFoe(e)) continue;
                    if (Flat(e.bodies[0].position - supChain[i]).magnitude > s.supCutReach || Mathf.Abs(e.FeetPoint.y - supChain[i].y) > 2.5f) continue;
                    supGap[i] = true;
                    (by ?? this).Fx(SkillFxKind.SupStep, this, supChain[i], Vector3.up, i, -1f);
                    Log($"{e.DisplayName}가 폰 사슬의 {i + 1}번째 폰을 쓰러뜨림");
                    break;
                }
            }
            if (supHop >= supChain.Count)
            {
                SupportArrive(s);
                return false;
            }
            if (supGap[supHop])
            {
                SupportStop(true, "폰 사슬이 끊김");
                return false;
            }
            Vector3 target = supChain[supHop] + Vector3.up * standHeight;
            supHopT += dt;
            float u = Mathf.Clamp01(supHopT / Mathf.Max(0.05f, s.supChainHop));
            next = Vector3.Lerp(supJumpFrom, target, u) + Vector3.up * (4f * s.supChainTop * u * (1f - u));
            Face(FlatDir(target - supJumpFrom));
            if (u >= 1f)
            {
                supHop++;
                supHopT = 0f;
                supJumpFrom = target;
                (by ?? this).Fx(SkillFxKind.SupStep, this, supChain[supHop - 1], FlatDir(target - supFrom), supHop - 1, 1f);
            }
            return true;
        }

        /// <summary>The rook's bridge: enemies on it slip off to the side (every quarter second).</summary>
        void SupportBridgeSlip(PawnRushSkillParams s, RagdollPawn mate)
        {
            if (Time.time < supSlipNext || mate == null) return;
            supSlipNext = Time.time + 0.25f;
            Vector3 a = mate.supFrom, b = mate.supTo;
            foreach (var e in All)
            {
                if (e == null || e.team == team || !UltFoe(e)) continue;
                float off = SupportOffLine(a, b, e.FeetPoint);
                if (off > s.supBridgeWidth * 0.5f + 0.3f) continue;
                Vector3 ab = FlatDir(b - a), side = new Vector3(ab.z, 0f, -ab.x);
                if (Vector3.Dot(Flat(e.FeetPoint - a), side) < 0f) side = -side;
                e.Kick(side, 4f, 0.3f, true);
                e.LastSkillHit = $"도개교: 미끄러져 떨어짐";
            }
        }

        // ---------------------------------------------------------------- the end of it

        /// <summary>Arrived: the reward (both tied and fast; side by side, both sent ahead; the team's gauge; "구조 !!"; one more
        /// rescue).</summary>
        void SupportArrive(PawnRushSkillParams s)
        {
            var by = supBy;
            bool called = supCalled;
            EndSupportCarry(true);
            if (!called || by == null)
            {
                SupportClear();
                return;
            }
            by.Rescues++;
            supBoostLeft = by.supBoostLeft = s.supBoost;
            supTether = by;
            by.supTether = this;
            if (Flat(by.bodies[0].position - bodies[0].position).magnitude <= s.supNear)
            {
                Vector3 ahead = FlatDir(by.facing);
                by.Kick(ahead, s.supDash, 0.5f, true);
                Kick(ahead, s.supDash, 0.5f, true);
            }
            by.AddGauge(s.supGauge, $"{DisplayName} 구조");
            by.Fx(SkillFxKind.SupSaved, this, FeetPoint, FlatDir(by.facing), by.Rescues, s.supGauge);
            Log($"{by.DisplayName}: {DisplayName} 구조!! ({SupportName((PieceKind)supKind)}, {supT:0.0}초) — 팀 게이지 +{s.supGauge * 100f:0}%");
            by.EndSupportLeader();
            SupportClear();
        }

        /// <summary>Called off (before the answer: the section's use kept) or cut on the way (spent: it stays where it fell).</summary>
        void SupportStop(bool cut, string why)
        {
            var by = supBy;
            if (supCarry) EndSupportCarry(false);
            if (by != null)
            {
                by.Fx(cut ? SkillFxKind.SupCut : SkillFxKind.SupCancel, this, bodies[0].position, Vector3.up, cut ? 1 : 0, 0f);
                if (supCalled)
                {
                    by.EndSupportLeader();
                    // The bridge's riders drop with it.
                    foreach (var p in All)
                        if (p != null && p != this && p.supBy == by && !p.supCalled && p.supCarry) p.SupportStop(true, "다리가 올라감");
                }
            }
            Log($"{DisplayName}: 지원 {(cut ? "끊김" : "취소")} ({why})");
            SupportClear();
        }

        void EndSupportCarry(bool safe)
        {
            if (!supCarry) return;
            supCarry = false;
            EndFlight(safe ? Vector3.zero : (Vector3?)null);
            if (safe) KeepSkillLandingSafe();
        }

        void SupportClear()
        {
            supBy = null;
            supCalled = supCarry = false;
            supChain.Clear();
            supGap.Clear();
            supJumpT = -1f;
        }

        /// <summary>This piece leaving the game or reset: whatever support it was in ends (as a cut, without a moment).</summary>
        void SupportReset()
        {
            if (supCarry) EndSupportCarry(false);
            SupportClear();
            if (supMate != null && supMate.supBy == this) supMate.SupportClear();
            EndSupportLeader();
            supBoostLeft = 0f;
            supTether = null;
        }
    }
}
