using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The hand feel's shared devices (JY-skill-lab, 2026-11-20; Docs/Skills/PAWN_RUSH_SKILL_POOL.md 1.3, its numbers 2, 4, 6
    /// and 8, the pool's chapter 12 step 1). The user, after playing the kits' basic row: the direction is right, but "스킬을
    /// 쓰는 손맛이 약해". These sit under the skills instead of being made for each one; the skills' own rules are not changed.
    /// Number 5, a cooldown given back for a hit, was here and is gone: "맞히면 쿨 감소는 밸런스를 너무 파괴해 빼는게 맞고"
    /// (the user, the same evening).
    ///
    ///   멈칫 (hit stop)   a hit holds the hitter and the hit piece a moment: a weak CC 0.06 s, a hard one 0.10 s, an ultimate
    ///                     0.15 s; +0.01 s for each point a cheaper piece takes off a dearer one (the king left out), +0.02 s
    ///                     for each more piece hit at once (three at most); at most 0.20 s, and half of it again within 0.3 s.
    ///                     A SkillFeel moment on the host's effect stream; PawnRushSkillFx holds the picture (online only on
    ///                     the hitter's and the hit player's screens).
    ///   반동 (recoil)     a shot pushes the shooter back 0.4 m; a pawn that hits with its body bounces back 0.5 m (the knight
    ///                     already bounces off a head); a body skill that went for someone and missed lurches on 1 m. A
    ///                     basic's push stops 0.3 m short of an edge.
    ///   소리 자리 셋        the press, the hit (a piece set down on a wooden board, with the hit stop) and the miss (a swish,
    ///                     then a funny sound 0.3 s later): SkillFeel moments, played by PawnRushSkillSfx.
    ///   미리 누르기        a key pressed in the last 0.4 s of its cooldown, or while down, getting up or staggered, goes the
    ///                     first moment it can, once.
    ///
    /// All of it runs on the host. PawnRushSkillParams.feel turns it all off, to compare. Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>What a SkillFeel moment is (its count): a key that started a use, a hit, a miss.</summary>
        public const int FeelPressMoment = 0, FeelHitMoment = 1, FeelMissMoment = 2;
        // How hard a hit holds: a weak CC (a shove, a stagger, heavy feet), a hard one (floored), an ultimate.
        const int FeelWeak = 0, FeelHard = 1, FeelUltimate = 2;

        // When this piece last hit (a hit soon after holds half as long).
        float feelLastHitAt = -10f;
        // A key pressed a moment early, per slot: how long it is still kept; and whether this step's press is one of those.
        readonly float[] feelKept = new float[PawnRushLoadout.Slots];
        readonly bool[] feelSent = new bool[PawnRushLoadout.Slots];
        // The hand feel's own push (a recoil, a bounce, a lurch): flat, dying out over its time.
        Vector3 kickDir;
        float kickSpeed, kickLeft, kickTime;
        bool kickAfterDash;

        PawnRushSkillParams FeelParams => PawnRushSkills != null && PawnRushSkills.feel ? PawnRushSkills : null;

        /// <summary>This slot's key is being kept, to go the moment it can (the early press; the host's own pieces).</summary>
        public bool SlotKept(int slot) => slot >= 0 && slot < feelKept.Length && feelKept[slot] > 0f;

        // ---------------------------------------------------------------- 멈칫 and the sounds: the moments

        static int PieceValue(PieceKind k) => k switch
        {
            PieceKind.Pawn => 1,
            PieceKind.Knight => 3,
            PieceKind.Bishop => 3,
            PieceKind.Rook => 5,
            PieceKind.Queen => 9,
            _ => 0,
        };

        /// <summary>This piece's skill hit <paramref name="target"/> (and <paramref name="pieces"/> - 1 more with it): the hit
        /// stop and the "탁". The pieces' values count only for a <paramref name="capture"/> (not a teammate held with it, as
        /// in a castling).</summary>
        void FeelHit(RagdollPawn target, Vector3 at, Vector3 dir, int grade, int pieces = 1, bool capture = true, float baseStop = 0f)
        {
            var s = FeelParams;
            if (s == null) return;
            // (A skill may name its own hit stop, as the pool writes some: the knight's stomp, the queen's check.)
            float stop = baseStop > 0f ? baseStop
                : grade >= FeelUltimate ? s.feelStopUltimate : grade == FeelHard ? s.feelStopHard : s.feelStopWeak;
            if (capture && target != null && piece != PieceKind.King && target.piece != PieceKind.King)
                stop += Mathf.Max(0, PieceValue(target.piece) - PieceValue(piece)) * s.feelStopPerValue;
            stop += Mathf.Clamp(pieces - 1, 0, 3) * s.feelStopPerExtra;
            stop = Mathf.Min(stop, s.feelStopMax);
            if (Time.time - feelLastHitAt < s.feelStopRepeat) stop *= 0.5f;
            feelLastHitAt = Time.time;
            Fx(SkillFxKind.SkillFeel, target, at, dir, FeelHitMoment, stop);
        }

        /// <summary>This piece's skill went for something and missed: the swish, then the funny sound.</summary>
        void FeelMiss(Vector3 at, Vector3 dir)
        {
            if (FeelParams == null) return;
            Fx(SkillFxKind.SkillFeel, null, at, dir, FeelMissMoment);
        }

        // ---------------------------------------------------------------- 미리 누르기 and the press

        /// <summary>First thing in RunSlots: the kept keys age, and with no skill under way one goes now (once) if what held
        /// it back is over.</summary>
        void FeelSendKept(PawnRushSkillParams s, PawnRushLoadout lo, float dt)
        {
            bool idle = !SkillRunning;
            for (int k = 0; k < feelKept.Length; k++)
            {
                feelSent[k] = false;
                if (feelKept[k] <= 0f) continue;
                feelKept[k] -= dt;
                int id = k < lo.SlotCount ? lo.Skill(piece, k) : 0;
                if (id == 0 || !s.feel)
                {
                    feelKept[k] = 0f;
                    continue;
                }
                if (!idle || !FeelFree(s, k, id)) continue;
                feelKept[k] = 0f;
                if (slotPress[k]) continue;
                slotPress[k] = true;
                feelSent[k] = true;
            }
        }

        /// <summary>Free of what made a key wait: its cooldown, being down, getting up, staggered. (The king's step gets him up
        /// off the floor itself once he has lain there a moment.)</summary>
        bool FeelFree(PawnRushSkillParams s, int k, int id) =>
            slotCd[k] <= 0f
            && ((State == PawnState.Active && !Staggered)
                || (id == PawnRushSkillIds.KingStep && State == PawnState.Ragdoll && !Diving && downFor >= s.stepDownAfter));

        /// <summary>After a free piece's slots, none started: a key that could not go is kept a moment - in the last
        /// feelBuffer seconds of its cooldown, or down, getting up or staggered. A kept key that was sent is not kept again;
        /// any other no (no target, in the air, held) is the skill's own and drops the key.</summary>
        void FeelKeepPresses(PawnRushSkillParams s, PawnRushLoadout lo)
        {
            if (!s.feel) return;
            for (int k = 0; k < lo.SlotCount && k < feelKept.Length; k++)
            {
                if (!slotPress[k] || feelSent[k] || lo.Skill(piece, k) == 0) continue;
                float cd = slotCd[k];
                if (cd > s.feelBuffer) continue;   // too early in its cooldown: the key is just not taken
                float keep = cd > 0f ? cd + 0.1f : 0f;
                if (State != PawnState.Active) keep = Mathf.Max(keep, s.feelBufferDown);
                else if (Staggered) keep = Mathf.Max(keep, s.feelBuffer);
                if (keep > 0f) feelKept[k] = Mathf.Max(feelKept[k], keep);
            }
        }

        /// <summary>A slot's key started a use (a skill under way, or a cooldown begun at once): the press sound.</summary>
        void FeelUsed(int k)
        {
            if (k >= 0 && k < feelKept.Length) feelKept[k] = 0f;
            if (FeelParams == null) return;
            Fx(SkillFxKind.SkillFeel, null, FeetPoint, FlatDir(facing), FeelPressMoment);
        }

        // ---------------------------------------------------------------- 반동

        /// <summary>The hand feel's recoil, bounce or lurch: a <see cref="Kick"/> of feelKickTime that stops short of an edge
        /// (only while the hand feel is on).</summary>
        void FeelKick(Vector3 dir, float metres)
        {
            var s = FeelParams;
            if (s != null) Kick(dir, metres, s.feelKickTime, true);
        }

        /// <summary>Push this piece <paramref name="metres"/> along <paramref name="dir"/> over <paramref name="seconds"/>,
        /// dying out so it ends standing; <paramref name="edgeSafe"/> stops it feelKickEdge short of an edge (a basic's),
        /// otherwise over it goes (a mid skill's, the pool's 1.3 #4). One given in a dash waits for that dash to end. The
        /// skills' own pushes use it too (the queen's lunge, the bishop's recoil), the hand feel on or off.</summary>
        void Kick(Vector3 dir, float metres, float seconds, bool edgeSafe)
        {
            var s = PawnRushSkills;
            dir.y = 0f;
            if (s == null || NetworkPuppet || metres <= 0.01f || dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            float room = metres * KitPushMul;   // light after a knight's stomp: twice as far (RagdollPawn.KitTwo.cs)
            if (edgeSafe)
            {
                // Short of an edge: the farthest point, in 0.1 m steps back, with a floor feelKickEdge beyond it.
                Vector3 feet = FeetPoint;
                while (room > 0.05f && !FloorUnder(feet + dir * (room + s.feelKickEdge))) room -= 0.1f;
                if (room <= 0.05f) return;
            }
            kickDir = dir;
            kickTime = Mathf.Max(0.05f, seconds);
            kickLeft = kickTime;
            kickSpeed = 2f * room / kickTime;   // dying out evenly from this to nothing covers the room
            kickAfterDash = dashing;
        }

        /// <summary>Every step after the skills (UpdateSkills): the push drives the body as a dash does.</summary>
        void UpdateFeelKick(float dt)
        {
            if (kickLeft <= 0f) return;
            if (dashing)
            {
                // The dash it was given in runs out first; any other dash takes over from it.
                if (!kickAfterDash) kickLeft = 0f;
                return;
            }
            kickAfterDash = false;
            if (State != PawnState.Active || Climbing || rope != null || Floating || castleCarry || chainCarry || kingCarry || bindLeft > 0f)
            {
                kickLeft = 0f;
                return;
            }
            kickLeft -= dt;
            Vector3 want = kickDir * (kickSpeed * Mathf.Clamp01(kickLeft / kickTime));
            Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
            AddVelocity(want - own);
            anchorVel = want + carryVel;
            Vector3 hp = bodies[0].position;
            anchorPos = new Vector3(hp.x + anchorVel.x * dt, anchorPos.y, hp.z + anchorVel.z * dt);
            anchor.MovePosition(anchorPos);
        }

        /// <summary>Something to stand on under this point (from 0.5 m above it to <paramref name="depth"/> below): not a piece,
        /// not a trigger.</summary>
        bool FloorUnder(Vector3 point, float depth = 1.2f)
        {
            int n = Physics.RaycastNonAlloc(point + Vector3.up * 0.5f, Vector3.down, hits, depth + 0.5f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || ownSet.Contains(c) || ColliderOwner.ContainsKey(c) || PassesThrough(c)) continue;
                return true;
            }
            return false;
        }

        void ClearFeel()
        {
            kickLeft = 0f;
            for (int k = 0; k < feelKept.Length; k++) feelKept[k] = 0f;
        }
    }
}
