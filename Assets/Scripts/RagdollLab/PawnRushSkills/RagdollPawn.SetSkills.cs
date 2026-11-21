using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The sets 1안-a and 2안-a (JY-skill-lab, 2026-11-21; Docs/Skills/PAWN_RUSH_SKILL_KITS.md 6.9; the user: "추천안들을
    /// 1안-a세트 이런 이름으로 묶는 게 편할 듯"). Both are the kits' skills bound another way; what they need beyond 3안-a (the
    /// kits' second version) is here.
    ///
    ///   변형      a skill with a set's own numbers runs on that skill's code (PawnRushSkillIds.BaseOf); the slot it sits in
    ///             says which numbers: the two steps at the mid tier (a diagonal hit floors 2 s, 8 s), the snipe at the mid
    ///             tier (20 m, 12 s), the wall charge at the mid tier (a 3 s wall, 14 s), the series of checks on 12 s, the
    ///             king's call on 16 s.
    ///   도약 포크  the knight's one skill in 1안-a (and 2안-a's endgame shape): the bent leap, and landing, two enemies
    ///             beside it are both kicked (floored and thrown, as the fork's); one alone is stomped and made light.
    ///   통과한 폰  the pawn's endgame shape in 2안-a: the two steps whose second is a 6 m run that shoves aside whoever
    ///             is in the way and goes on.
    ///   데리고 걷기 the king's endgame shape in 2안-a: each step takes the two nearest teammates within 4 m along with it,
    ///             and they are not floored for a second.
    ///   모양      the growing system plays a row's first slot (its middlegame shape) and, from stage 2, its second (its
    ///             endgame shape). A piece's own stage (ShapeStage) is what the course sets when its team has cleared its
    ///             second mini-game; the skill test bed switches every row's at once.
    ///
    /// Prototypes. Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>This piece's own stage of the growing system (2: the endgame shapes, after its team's second
        /// mini-game); 0 plays the loadout's row stage.</summary>
        public int ShapeStage { get; set; }

        /// <summary>The skill as it sits in the slot at work (a variant, not its base): the host's, while it starts or runs.</summary>
        int VariantNow => SlotSkill(cdSlot);

        /// <summary>The skill as it sits in the slot under way (any PC: the slot comes from the host's state).</summary>
        public int ActiveVariant => ActiveSlot >= 0 ? SlotSkill(ActiveSlot) : 0;

        // The passed pawn's second step is under way (a run through).
        bool passedRun;

        /// <summary>A variant's cooldown against its base's (StartCooldown).</summary>
        float VariantCooldownScale(int variant)
        {
            var s = PawnRushSkills;
            if (s == null) return 1f;
            switch (variant)
            {
                case PawnRushSkillIds.StepsMid: return s.stepMidCooldown / Mathf.Max(0.1f, s.pawnCooldown);
                case PawnRushSkillIds.SnipeMid: return s.snipeMidCooldown / Mathf.Max(0.1f, s.snipeCooldown);
                case PawnRushSkillIds.WallMid: return s.wallMidCooldown / Mathf.Max(0.1f, s.wallCooldown);
                case PawnRushSkillIds.SeriesQuick: return s.seriesQuickCooldown / Mathf.Max(0.1f, s.seriesCooldown);
                case PawnRushSkillIds.SeriesB: return s.seriesBCooldown / Mathf.Max(0.1f, s.seriesCooldown);   // (3안-b: the queen's two)
                case PawnRushSkillIds.EightB: return s.eightBCooldown / Mathf.Max(0.1f, s.eightCooldown);
                case PawnRushSkillIds.SummonQuick: return s.summonQuickCooldown / Mathf.Max(0.1f, s.summonCooldown);
                case PawnRushSkillIds.LeapFork: return s.leapForkCooldown / Mathf.Max(0.1f, s.leapCooldown);
                default: return 1f;
            }
        }

        /// <summary>How long the two steps' diagonal hit and en passant floor a piece: the mid tier's longer.</summary>
        float StepKnockNow(PawnRushSkillParams s) => VariantNow == PawnRushSkillIds.StepsMid ? s.stepMidKnock : s.stepKnockHold;

        /// <summary>How long the wall charge's wall stands: the mid tier's shorter.</summary>
        float WallSecondsNow(PawnRushSkillParams s) => VariantNow == PawnRushSkillIds.WallMid ? s.wallMidSeconds : s.wallSeconds;

        // ---------------------------------------------------------------- 통과한 폰 (the passed pawn)

        /// <summary>The run meets an enemy: shoved off to the side it stands on, and the pawn runs on.</summary>
        void PassedShove(PawnRushSkillParams s, RagdollPawn other)
        {
            Vector3 dir = FlatDir(skillDir), right = Vector3.Cross(Vector3.up, dir);
            float side = Vector3.Dot(other.bodies[0].position - bodies[0].position, right) >= 0f ? 1f : -1f;
            other.Kick(right * side + dir * 0.3f, s.passedShove, 0.25f, false);
            other.LastSkillHit = $"통과한 폰: 옆으로 {s.passedShove:0.#} m 밀림";
            Vector3 contact = SkillContact(other);
            Fx(SkillFxKind.PawnHit, other, contact, right * side, 0);
            FeelHit(other, contact, right * side, FeelWeak);
            Log($"통과한 폰 → {other.DisplayName}: 밀고 지나감");
        }

        // ---------------------------------------------------------------- 도약 포크 (the leap fork)

        readonly List<RagdollPawn> leapForkNear = new List<RagdollPawn>();

        /// <summary>The bent leap came down (UpdateBentLeap): two enemies beside it are both kicked; one alone is stomped.</summary>
        void LeapForkLand(PawnRushSkillParams s)
        {
            Vector3 c = FeetPoint;
            leapForkNear.Clear();
            foreach (var e in All)
            {
                if (e == null || e == this || !IsEnemyOf(e) || e.State != PawnState.Active || e.SkillUntargetable) continue;
                Vector3 d = e.FeetPoint - c;
                if (Mathf.Abs(d.y) > 1.2f || Flat(d).magnitude > s.leapForkReach) continue;
                leapForkNear.Add(e);
            }
            leapForkNear.Sort((a, b) => Flat(a.FeetPoint - c).sqrMagnitude.CompareTo(Flat(b.FeetPoint - c).sqrMagnitude));
            if (leapForkNear.Count >= 2)
            {
                bool king = false, queen = false;
                for (int i = 0; i < 2; i++)
                {
                    var e = leapForkNear[i];
                    king |= e.piece == PieceKind.King;
                    queen |= e.piece == PieceKind.Queen;
                    Vector3 away = FlatDir(e.bodies[0].position - bodies[0].position);
                    string result = SkillHit(e, away * s.forkPush + Vector3.up * s.forkLift, true, "도약 포크", false, s.forkKnockHold);
                    Log($"도약 포크 → {e.DisplayName}: {result}");
                }
                FeelHit(leapForkNear[0], c, planDir, FeelHard, 2, true, s.forkDoubleStop);
                Fx(SkillFxKind.KnightLand, null, c, planDir, 2, s.leapForkReach);
                Log("도약 포크: 둘을 한꺼번에 참" + (king && queen ? " — 로열 포크!" : ""));
                return;
            }
            if (leapForkNear.Count == 1)
            {
                var e = leapForkNear[0];
                bool swayed = e.Stagger(s.leapStompStagger);
                MakeLight(e, s);
                e.LastSkillHit = "도약 포크: 밟혀 가벼워짐";
                Vector3 head = e.bodies[(int)BodyId.Head].position;
                Fx(SkillFxKind.KnightStomp, e, head, Flat(facing));
                FeelHit(e, head, Flat(facing), FeelWeak, baseStop: s.leapStompStop);
                Log($"도약 포크 → {e.DisplayName}: 하나뿐 — 밟아서 가볍게" + (swayed ? "" : " (휘청 안 걸림)"));
            }
        }

        // ---------------------------------------------------------------- 데리고 걷기 (the king taking two along)

        readonly List<RagdollPawn> escortNear = new List<RagdollPawn>();

        /// <summary>The king's step starts (UpdateKingStep): the two nearest standing teammates within escortReach go the same
        /// step with him, and are not floored for escortGuard.</summary>
        void EscortStep(PawnRushSkillParams s)
        {
            escortNear.Clear();
            Vector3 me = bodies[0].position;
            foreach (var p in All)
            {
                if (p == null || p == this || !IsAllyOf(p) || p.State != PawnState.Active) continue;
                if (Flat(p.bodies[0].position - me).magnitude > s.escortReach || Mathf.Abs(p.bodies[0].position.y - me.y) > 1.5f) continue;
                escortNear.Add(p);
            }
            escortNear.Sort((a, b) => Flat(a.bodies[0].position - me).sqrMagnitude.CompareTo(Flat(b.bodies[0].position - me).sqrMagnitude));
            int n = Mathf.Min(escortNear.Count, Mathf.Max(0, s.escortCount));
            float time = s.stepDistance / Mathf.Max(0.5f, s.stepSpeed) + 0.05f;
            for (int i = 0; i < n; i++)
            {
                var m = escortNear[i];
                m.Kick(FlatDir(skillDir), s.stepDistance, time, true);
                m.getUpGuardLeft = Mathf.Max(m.getUpGuardLeft, s.escortGuard);
                m.LastSkillHit = "데리고 걷기: 킹과 같이 한 걸음";
            }
            if (n > 0) Log($"데리고 걷기: 팀원 {n}명이 킹과 같이 걸음");
        }
    }
}
