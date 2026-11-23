using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The ultimate gauge (JY-skill-lab, 2026-11-21; Docs/Skills/PAWN_RUSH_SKILL_SYSTEM.md 7.3 and 7.5). First one a piece
    /// (the user: "맵상의 왕관 몇 개를 주우면 조금씩 차고, 시간이 지나면서 (진 팀은 보너스)로도 차고, 공격당해 넘어지면 게이지가
    /// 차는 식으로"); then, with the ultimates made the team's comeback move ("아예 팀이 역전할 수 있는"), one a team.
    ///
    ///   팀에 하나  white's and black's gauge, 0 to 1 (a piece with no team, a lab dummy, keeps one of its own). Whatever
    ///             sits in a piece's ultimate slot (Q) goes on it, with no cooldown; the other slots keep cooldowns.
    ///   차는 것   a map crown any teammate takes 4% (PawnRushGaugeCrown: back 30 s later); time 0.1% a second, twice as
    ///             fast for the losing team (its fourth runner behind); a teammate floored, thrown or squashed by an
    ///             enemy's skill 1.5%, one piece at most 6% in 10 s (not a fall, an obstacle or its own doing).
    ///   쓰는 것   full, every teammate's Q lights; the first to press uses its own piece's ultimate, and the gauge is spent
    ///             the moment it goes past its aim. Floored in its warning, half is left.
    ///   역전 배율  how far a team's fourth runner is behind the other team's sets how big its ultimate is: ahead by more
    ///             than 20 m x0.5, within 20 m x1, behind by more than 20 m x1.5, by more than 40 m x2 (Comeback). The
    ///             race's progress is the course's to say (RaceProgress); without it, how far along +Z.
    ///   언제      GaugeAllowed (the course: after the promotion, not in a mini-game); null, always (the skill test scene).
    ///             The test scene's flat test cooldown keeps every gauge full.
    ///   한 판에 한 번  a team's ultimate goes once a match (11-22, the user: "궁극 빈도는 팀마다 1번"): once it has gone past its
    ///             warning the team's gauge fills no more until the next round (cut in its warning, it is the team's again).
    ///             The test cooldown still keeps it full, for trying them over and over.
    ///
    /// The host's; a client is told its team's gauge in the pieces' skill state (a byte, sent when it is full or has just
    /// moved by 5%). Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>Whether a piece's gauge fills now (the course: after the promotion, not in a mini-game). Null: always.</summary>
        public static Func<RagdollPawn, bool> GaugeAllowed;
        /// <summary>Whether a team is the losing one, for time's bonus. Null: its fourth runner is behind (TeamLead).</summary>
        public static Func<int, bool> TeamLosing;
        /// <summary>How far along the race a piece is (m), the course's to say. Null: how far along +Z.</summary>
        public static Func<RagdollPawn, float> RaceProgress;
        /// <summary>A team's comeback multiplier, set by hand (the skill test bed). Null: from the fourth runners.</summary>
        public static Func<int, float> TeamComeback;

        // The teams' gauges (white, black); when time last filled each; how long to keep telling the other PCs, and the
        // step each was last told at.
        static readonly float[] teamGauge = new float[2];
        static readonly float[] teamTickAt = { -1f, -1f };
        static readonly float[] teamSendLeft = new float[2];
        static readonly int[] teamSentStep = { -1, -1 };
        // A team's ultimate gone this match (11-22: once a match; the gauge fills no more).
        static readonly bool[] teamUltSpent = new bool[2];

        /// <summary>A team's ultimate has gone this match (its gauge fills no more until the next round).</summary>
        public static bool TeamUltSpent(int t)
        {
            int i = TeamIndex(t);
            return i >= 0 && teamUltSpent[i];
        }

        bool GaugeSpent
        {
            get
            {
                int i = TeamIndex(team);
                return i >= 0 && teamUltSpent[i];
            }
        }

        // A piece with no team's own gauge; an ultimate under way that has spent it; when the knockdowns that filled it
        // came (the last few).
        float soloGauge;
        bool ultCommitted;
        readonly float[] gaugeKnockAt = { -100f, -100f, -100f, -100f, -100f, -100f };
        float soloSendLeft;
        int soloSentStep = -1;

        static int TeamIndex(int t) => t == Teams.White ? 0 : t == Teams.Black ? 1 : -1;

        /// <summary>Every gauge empty again (a new round, the skill test bed starting).</summary>
        public static void ResetTeamGauges()
        {
            for (int i = 0; i < 2; i++)
            {
                teamGauge[i] = 0f;
                teamTickAt[i] = -1f;
                teamSendLeft[i] = 0f;
                teamSentStep[i] = -1;
                teamUltSpent[i] = false;
            }
            ResetSupport();   // (every section's support usable again: RagdollPawn.Support.cs)
            ResetSetThree();  // (no puddle left: RagdollPawn.SetThree.cs)
        }

        float GaugeNow
        {
            get
            {
                int i = TeamIndex(team);
                return i >= 0 ? teamGauge[i] : soloGauge;
            }
            set
            {
                int i = TeamIndex(team);
                if (i >= 0) teamGauge[i] = Mathf.Clamp01(value);
                else soloGauge = Mathf.Clamp01(value);
            }
        }

        /// <summary>This piece's team's ultimate gauge, 0..1 (any PC: a client is told it).</summary>
        public float UltGauge => Mathf.Clamp01(GaugeNow);
        /// <summary>Full: the ultimate's key goes.</summary>
        public bool UltReady => GaugeNow >= 0.999f;
        /// <summary>An ultimate of this piece's is under way (it has spent the gauge).</summary>
        public bool UltUnderWay => ultCommitted && SkillRunning;

        /// <summary>This slot goes on the gauge: the ultimate slot (Q), with a skill in it.</summary>
        bool UltSlotOnGauge(int slot)
        {
            var s = PawnRushSkills;
            var lo = Loadout;
            return s != null && s.realGauge && lo != null && slot == PawnRushLoadout.UltSlot && lo.SlotCount > PawnRushLoadout.UltSlot
                   && SlotSkill(slot) != 0;
        }

        public bool UltOnGauge(int slot) => UltSlotOnGauge(slot);

        /// <summary>This piece's ultimate slot holds something.</summary>
        public bool HasUltimate => UltSlotOnGauge(PawnRushLoadout.UltSlot);

        /// <summary>An ultimate-made skill's cooldown when it is not on the gauge: a high skill's for the first six (the
        /// high tier's candidates now), the old three minutes for the rest; the gauge off, three minutes for every one.</summary>
        static float UltCooldown(int id, PawnRushSkillParams s) =>
            s.realGauge && PawnRushSkillIds.HighCandidate(id) ? s.highUltCooldown : s.ultGauge;

        // ---------------------------------------------------------------- the race: progress, the fourth runners, the comeback

        static float Progress(RagdollPawn p) => RaceProgress != null ? RaceProgress(p) : p.bodies[0].position.z;

        readonly static List<float> progressScratch = new List<float>();

        /// <summary>A team's fourth runner's progress (its last one with fewer than four); NaN with nobody.</summary>
        static float FourthOf(int team)
        {
            progressScratch.Clear();
            foreach (var p in All)
                if (p != null && p.team == team && p.PawnRushSkills != null) progressScratch.Add(Progress(p));
            if (progressScratch.Count == 0) return float.NaN;
            progressScratch.Sort((a, b) => b.CompareTo(a));
            return progressScratch[Mathf.Min(3, progressScratch.Count - 1)];
        }

        /// <summary>How far a team's fourth runner is ahead of the other team's (m; behind, below 0). 0 without both teams.</summary>
        public static float TeamLead(int team)
        {
            if (TeamIndex(team) < 0) return 0f;
            float mine = FourthOf(team), theirs = FourthOf(team == Teams.White ? Teams.Black : Teams.White);
            return float.IsNaN(mine) || float.IsNaN(theirs) ? 0f : mine - theirs;
        }

        /// <summary>A team's comeback multiplier (Docs/Skills/PAWN_RUSH_SKILL_SYSTEM.md 7.2): x0.5 ahead by more than 20 m,
        /// x1 within 20 m, x1.5 behind by more than 20 m, x2 by more than 40 m.</summary>
        public static float Comeback(int team)
        {
            if (TeamComeback != null) return TeamComeback(team);
            float lead = TeamLead(team);
            return lead > 20f ? 0.5f : lead >= -20f ? 1f : lead >= -40f ? 1.5f : 2f;
        }

        /// <summary>An ultimate's number at a multiplier: <paramref name="atOne"/> at x1, <paramref name="atTwo"/> at x2 (in
        /// between along the line), and x0.5 half of <paramref name="atOne"/>.</summary>
        static float ByComeback(float f, float atOne, float atTwo) => f >= 1f ? Mathf.Lerp(atOne, atTwo, f - 1f) : atOne * f;

        static bool Losing(int team) => TeamLosing != null ? TeamLosing(team) : TeamLead(team) < -1f;

        // ---------------------------------------------------------------- filling

        /// <summary>Every step (UpdateKitStates, the host's): time fills the team's gauge, once a step whoever of it comes
        /// first; the test cooldown keeps it full.</summary>
        void UpdateGauge(PawnRushSkillParams s, float dt)
        {
            int i = TeamIndex(team);
            if (i >= 0) { if (teamSendLeft[i] > 0f) teamSendLeft[i] -= dt; }
            else if (soloSendLeft > 0f) soloSendLeft -= dt;
            if (s == null || !s.realGauge || !HasUltimate) return;
            if (s.testCooldown > 0f && !ultCommitted)
            {
                if (GaugeNow < 1f) GaugeNow = 1f;
                GaugeMoved();
                return;
            }
            float step = dt;
            if (i >= 0)
            {
                float now = Time.fixedTime;
                if (teamTickAt[i] < 0f || now - teamTickAt[i] > 1f) teamTickAt[i] = now - dt;   // (the first step, or after a pause)
                step = now - teamTickAt[i];
                teamTickAt[i] = now;
            }
            if (step <= 0f || ultCommitted || GaugeSpent || GaugeNow >= 1f || (GaugeAllowed != null && !GaugeAllowed(this))) return;
            GaugeNow += s.gaugeTime * (Losing(team) ? s.gaugeLosing : 1f) * step;
            GaugeMoved();
        }

        /// <summary>The gauge moved: the other PCs are told once it has crossed a 5% step (and while it is full).</summary>
        void GaugeMoved()
        {
            int step = Mathf.FloorToInt(UltGauge * 20f), i = TeamIndex(team);
            if (i >= 0)
            {
                if (step == teamSentStep[i]) return;
                teamSentStep[i] = step;
                teamSendLeft[i] = 0.25f;
                return;
            }
            if (step == soloSentStep) return;
            soloSentStep = step;
            soloSendLeft = 0.25f;
        }

        void AddGauge(float amount, string why)
        {
            var s = PawnRushSkills;
            if (s == null || !s.realGauge || amount <= 0f || GaugeNow >= 1f || ultCommitted || (GaugeSpent && s.testCooldown <= 0f)) return;
            GaugeNow += amount;
            GaugeMoved();
            Log($"{Teams.Name(team)}팀 게이지 +{amount * 100f:0.#}% ({DisplayName}: {why}) → {UltGauge * 100f:0}%" + (UltReady ? " — 궁극기 준비!" : ""));
        }

        /// <summary>Floored, thrown or squashed by <paramref name="by"/>'s skill: the team's gauge fills a little, one piece
        /// at most gaugeKnockCap within gaugeKnockWindow.</summary>
        public void GaugeKnocked(RagdollPawn by)
        {
            var s = PawnRushSkills;
            if (s == null || !s.realGauge || by == null || by == this || NetworkPuppet || !Teams.AreEnemies(team, by.team) || !HasUltimate) return;
            int recent = 0, oldest = 0;
            for (int i = 0; i < gaugeKnockAt.Length; i++)
            {
                if (Time.time - gaugeKnockAt[i] <= s.gaugeKnockWindow) recent++;
                if (gaugeKnockAt[i] < gaugeKnockAt[oldest]) oldest = i;
            }
            if ((recent + 1) * s.gaugeKnock > s.gaugeKnockCap + 1e-4f) return;
            gaugeKnockAt[oldest] = Time.time;
            AddGauge(s.gaugeKnock, $"{by.DisplayName}에게 당함");
        }

        /// <summary>A map crown touched (PawnRushGaugeCrown, the host's): taken unless the team's gauge is full already.</summary>
        public bool TakeCrown(PawnRushGaugeCrown crown)
        {
            var s = PawnRushSkills;
            if (s == null || !s.realGauge || NetworkPuppet || UltReady || ultCommitted || !HasUltimate || (GaugeSpent && s.testCooldown <= 0f)) return false;
            if (GaugeAllowed != null && !GaugeAllowed(this)) return false;
            AddGauge(s.gaugeCrown, "왕관");
            Fx(SkillFxKind.KitCrown, null, crown.transform.position, Vector3.up, crown.Index, s.crownRespawn);
            PopOver(this, PopCrown);
            return true;
        }

        /// <summary>The test bed's: the gauge full at once.</summary>
        public void FillGauge()
        {
            GaugeNow = 1f;
            GaugeMoved();
        }

        // ---------------------------------------------------------------- using it

        /// <summary>Before an ultimate's key is read (RunSlots): it goes only on a full gauge, and only while no teammate's is
        /// under way. A key that cannot go is a short "no".</summary>
        bool UltMayGo(int slot)
        {
            if (!UltSlotOnGauge(slot)) return true;
            string why = !UltReady ? $"게이지 {UltGauge * 100f:0}%" : TeamUltBusy() ? "우리 팀의 다른 궁극기가 나가는 중" : null;
            if (why == null) return true;
            Fx(SkillFxKind.SkillFail, null, FeetPoint, FlatDir(facing), (int)piece);
            Log($"{DisplayName}: 궁극기 못 씀 ({why})");
            return false;
        }

        bool TeamUltBusy()
        {
            foreach (var p in All)
                if (p != null && p != this && p.team == team && p.team != Teams.None && p.UltUnderWay) return true;
            return false;
        }

        /// <summary>After the slots (UpdateSkills): an ultimate past its aim spends the gauge; one over lets the team's next go.</summary>
        void TrackUltimate()
        {
            if (!SkillRunning || !UltSlotOnGauge(activeSlot))
            {
                ultCommitted = false;
                return;
            }
            if (ultCommitted || planPhase == PlanPhase.None || planPhase == PlanPhase.Aim) return;
            ultCommitted = true;
            GaugeNow = 0f;
            GaugeMoved();
            int spent = TeamIndex(team);
            if (spent >= 0) teamUltSpent[spent] = true;   // once a match: the team's gauge fills no more
            Log($"{DisplayName}: 궁극기 {PawnRushSkillBook.Name(activeSkill)} — {Teams.Name(team)}팀 게이지를 씀 (역전 배율 ×{Comeback(team):0.#})");
        }

        /// <summary>The ultimate under way is floored (RunSlots): during its warning, half the gauge is left.</summary>
        void UltCut(PawnRushSkillParams s)
        {
            if (!ultCommitted || s == null || planPhase != PlanPhase.Windup) return;
            GaugeNow = Mathf.Max(GaugeNow, s.gaugeCutKeep);
            GaugeMoved();
            int back = TeamIndex(team);
            if (back >= 0) teamUltSpent[back] = false;   // cut in its warning: not spent, the team's again
            PopOver(this, PopUltCut);
            Log($"{DisplayName}: 궁극기가 예고 중에 끊김 — 게이지 {s.gaugeCutKeep * 100f:0}% 남음");
        }

        // ---------------------------------------------------------------- over the network

        bool GaugeNetBusy
        {
            get
            {
                int i = TeamIndex(team);
                return HasUltimate && (UltReady || (i >= 0 ? teamSendLeft[i] > 0f : soloSendLeft > 0f));
            }
        }

        void CaptureGaugeNet(ref SkillWireState s) => s.Gauge = UltGauge;

        void ApplyGaugeNet(in SkillWireState s)
        {
            if (HasUltimate) GaugeNow = s.Gauge;
        }
    }
}
