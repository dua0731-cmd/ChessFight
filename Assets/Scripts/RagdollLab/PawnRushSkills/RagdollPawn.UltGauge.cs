using System;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The ultimate gauge (JY-skill-lab, 2026-11-21; the kits' second version, Docs/Skills/PAWN_RUSH_SKILL_SYSTEM.md 7.4 and
    /// 7.5). The user: "얻는 방법은 그냥 게이지로, 맵상의 왕관 몇 개를 주우면 조금씩 차고, 시간이 지나면서 (진 팀은 보너스)로도
    /// 차고, 공격당해 넘어지면 게이지가 차는 식으로". Each piece has one, 0 to 1; with PawnRushSkillParams.realGauge an
    /// ultimate (in any slot) has no cooldown and goes only on a full gauge.
    ///
    ///   차는 것   a map crown 10% (PawnRushGaugeCrown: it comes back 30 s later); time 0.15% a second, 1.5 times for the
    ///             losing team (TeamLosing); floored, thrown or squashed by an enemy's skill 4%, at most 12% in 10 s (not a
    ///             fall, an obstacle or its own doing). Full, it fills no more and the piece wears a crown over its head.
    ///   쓰는 것   the ultimate's key on a full gauge: it is spent the moment the ultimate goes (past its aim). One of a
    ///             team's at a time: while one is under way, a teammate's key does nothing. Floored during its warning,
    ///             half the gauge is left.
    ///   언제      GaugeAllowed (the course: after the promotion, not in a mini-game); null, always (the skill test scene).
    ///             The test scene's flat test cooldown keeps every gauge full.
    ///
    /// The host's; a client is told the gauge in the piece's skill state (a byte, sent when it is full or has just moved
    /// by 5%). Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>Whether a piece's gauge fills now (the course: after the promotion, not in a mini-game). Null: always.</summary>
        public static Func<RagdollPawn, bool> GaugeAllowed;
        /// <summary>Whether a team is the losing one (its fourth runner is behind), for time's bonus. Null: neither.</summary>
        public static Func<int, bool> TeamLosing;

        // 0..1; an ultimate under way that has spent it; when the knockdowns that filled it came (the last few); how long to
        // keep sending it to the other PCs, and the step it was last sent at.
        float ultGauge01;
        bool ultCommitted;
        readonly float[] gaugeKnockAt = { -100f, -100f, -100f, -100f, -100f, -100f };
        float gaugeSendLeft;
        int gaugeSentStep = -1;

        /// <summary>The ultimate gauge, 0..1 (any PC: a client is told it).</summary>
        public float UltGauge => Mathf.Clamp01(ultGauge01);
        /// <summary>Full: the ultimate's key goes.</summary>
        public bool UltReady => ultGauge01 >= 0.999f;
        /// <summary>An ultimate of this piece's is under way (it has spent its gauge).</summary>
        public bool UltUnderWay => ultCommitted && SkillRunning;

        /// <summary>This slot holds an ultimate that goes on the gauge (not a cooldown).</summary>
        bool UltSlotOnGauge(int slot)
        {
            var s = PawnRushSkills;
            var lo = Loadout;
            return s != null && s.realGauge && lo != null && slot >= 0 && slot < PawnRushLoadout.Slots
                   && PawnRushSkillIds.Ultimate(lo.Skill(piece, slot));
        }

        public bool UltOnGauge(int slot) => UltSlotOnGauge(slot);

        /// <summary>Some slot of this piece's holds an ultimate on the gauge.</summary>
        public bool HasUltimate
        {
            get
            {
                for (int k = 0; k < SlotCount; k++)
                    if (UltSlotOnGauge(k)) return true;
                return false;
            }
        }

        /// <summary>Every step (UpdateKitStates, the host's): time fills it; the test cooldown keeps it full.</summary>
        void UpdateGauge(PawnRushSkillParams s, float dt)
        {
            if (gaugeSendLeft > 0f) gaugeSendLeft -= dt;
            if (s == null || !s.realGauge || !HasUltimate) return;
            if (s.testCooldown > 0f && !ultCommitted) ultGauge01 = 1f;
            else if (!ultCommitted && ultGauge01 < 1f && (GaugeAllowed == null || GaugeAllowed(this)))
            {
                bool losing = TeamLosing != null && TeamLosing(team);
                ultGauge01 = Mathf.Min(1f, ultGauge01 + s.gaugeTime * (losing ? s.gaugeLosing : 1f) * dt);
            }
            GaugeMoved();
        }

        /// <summary>The gauge moved: the other PCs are told once it has crossed a 5% step (and while it is full).</summary>
        void GaugeMoved()
        {
            int step = Mathf.FloorToInt(UltGauge * 20f);
            if (step == gaugeSentStep) return;
            gaugeSentStep = step;
            gaugeSendLeft = 0.25f;
        }

        void AddGauge(float amount, string why)
        {
            var s = PawnRushSkills;
            if (s == null || !s.realGauge || amount <= 0f || ultGauge01 >= 1f || ultCommitted) return;
            ultGauge01 = Mathf.Min(1f, ultGauge01 + amount);
            GaugeMoved();
            Log($"{DisplayName}: 게이지 +{amount * 100f:0}% ({why}) → {UltGauge * 100f:0}%" + (UltReady ? " — 궁극기 준비!" : ""));
        }

        /// <summary>Floored, thrown or squashed by <paramref name="by"/>'s skill: the gauge fills a little, at most
        /// gaugeKnockCap within gaugeKnockWindow.</summary>
        public void GaugeKnocked(RagdollPawn by)
        {
            var s = PawnRushSkills;
            if (s == null || !s.realGauge || by == null || by == this || NetworkPuppet || !Teams.AreEnemies(team, by.team)) return;
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

        /// <summary>A map crown touched (PawnRushGaugeCrown, the host's): taken unless the gauge is full already.</summary>
        public bool TakeCrown(PawnRushGaugeCrown crown)
        {
            var s = PawnRushSkills;
            if (s == null || !s.realGauge || NetworkPuppet || UltReady || ultCommitted || !HasUltimate) return false;
            if (GaugeAllowed != null && !GaugeAllowed(this)) return false;
            AddGauge(s.gaugeCrown, "왕관");
            Fx(SkillFxKind.KitCrown, null, crown.transform.position, Vector3.up, crown.Index, s.crownRespawn);
            PopOver(this, PopCrown);
            return true;
        }

        /// <summary>The test bed's: the gauge full at once.</summary>
        public void FillGauge()
        {
            ultGauge01 = 1f;
            GaugeMoved();
        }

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
                if (p != null && p != this && p.team == team && p.UltUnderWay) return true;
            return false;
        }

        /// <summary>After the slots (UpdateSkills): an ultimate past its aim spends the gauge; one over lets the team's next go.</summary>
        void TrackUltimate()
        {
            if (!SkillRunning || !PawnRushSkillIds.Ultimate(activeSkill) || !UltSlotOnGauge(activeSlot))
            {
                ultCommitted = false;
                return;
            }
            if (ultCommitted || planPhase == PlanPhase.None || planPhase == PlanPhase.Aim) return;
            ultCommitted = true;
            ultGauge01 = 0f;
            GaugeMoved();
            Log($"{DisplayName}: 궁극기 {PawnRushSkillBook.Name(activeSkill)} — 게이지를 씀");
        }

        /// <summary>The ultimate under way is floored (RunSlots): during its warning, half the gauge is left.</summary>
        void UltCut(PawnRushSkillParams s)
        {
            if (!ultCommitted || s == null || planPhase != PlanPhase.Windup) return;
            ultGauge01 = Mathf.Max(ultGauge01, s.gaugeCutKeep);
            GaugeMoved();
            PopOver(this, PopUltCut);
            Log($"{DisplayName}: 궁극기가 예고 중에 끊김 — 게이지 {s.gaugeCutKeep * 100f:0}% 남음");
        }

        // ---------------------------------------------------------------- over the network

        bool GaugeNetBusy => UltReady || gaugeSendLeft > 0f;

        void CaptureGaugeNet(ref SkillWireState s) => s.Gauge = UltGauge;

        void ApplyGaugeNet(in SkillWireState s) => ultGauge01 = s.Gauge;
    }
}
