using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 4판, round 1, bundle 다 (Docs/Skills/PAWN_RUSH_SET4_ROUND1.md 4장 and 5장; 11-26): ten skills that need a
    /// body or a big thing of their own:
    ///
    ///   폰    승격 5초 전 (a crown and 5, 4, 3, 2, 1 over the head for all to see; five seconds on its feet: "승격!", enemies
    ///         within 5 m boom away and it is gold 3 s; knocked over first: "푸쉬식…")
    ///   나이트 땅속 헤엄 (under the floor, its ears showing, up to 3 s at 6 m/s steered by the move keys, through everyone and
    ///         untouchable; the key again, the time or an edge ahead: up it comes, enemies within 1.5 m go up with a boom)
    ///   비숍  핀! (a big safety pin along the aim, 15 m at 20 m/s: its clothes pinned where it was hit, 3 s on a 2 m band
    ///         - 묶임 - and back there with a spring at the end)
    ///   룩    더 루우욱! (a mark on the aim within 20 m; 0.4 s crouched, 1.2 s through the air, "희생… 더 루우욱!": within 4 m
    ///         boom and 콰당! 2 s; it is stuck head first 1.5 s, no keys), 넘어간다~! (1 s growing to an 8 m tower where it
    ///         stands, no keys; it falls ahead along its shadow, 8 × 2 m, in 0.5 s: 콰당! 2 s; 2 s a bridge allies cross),
    ///         돼지 (2 s of its gate open, walking only: enemies in front within 60° and 8 m are pulled in about as hard as
    ///         one runs, three at most swallowed; spat out behind with a bang 2 s later or on the key), 룩 리프트 (a 3 m
    ///         tower rising 4 m under it in 0.5 s, 3 s: everyone on it goes up)
    ///   퀸    행차 (a red carpet rolling out along the aim, 3 m wide, 12 m/s, 20 m, 5 s: whoever it reaches is rolled up in
    ///         it 1.5 s - 콰당! - and pushed 3 m on; she runs 30% faster on it), 착석! (a throne's shadow 5 m across on the aim
    ///         within 25 m and a fanfare; she leaps up out of sight and comes down on it with the throne 1 s later: enemies
    ///         in the shadow boom away; she sits 0.5 s), 퀸스 갬빗 (five big pieces hanging upside down over a 10 m ring on the
    ///         aim within 25 m, their shadows below; from 1 s one drops every 0.3 s: under one, 2 m across, it is squashed)
    ///
    /// The host runs it all; every PC draws it from the S4 moments (PawnRushSkillFx.SetFourC.cs). Not seen in Unity by
    /// anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        sealed class Pin4 { public Vector3 pos, dir; public float flown; }
        sealed class Carpet4 { public Vector3 from, dir; public float rolled, left; public readonly HashSet<RagdollPawn> rolledUp = new HashSet<RagdollPawn>(); }
        sealed class Gambit4 { public Vector3 at; public float wait, fall; public int index; public bool dropping; }

        float promo4Left;
        Pin4 pin4;
        Carpet4 carpet4;
        readonly List<Gambit4> gambit4 = new List<Gambit4>();
        readonly List<RagdollPawn> swallowed4 = new List<RagdollPawn>();
        bool burrowed4;
        PawnRushKitWall bridge4;

        /// <summary>The run's top speed on her own carpet (SkillMoveScale).</summary>
        float CarpetScale => carpet4 != null && PawnRushSkills != null && OnCarpet4(FeetPoint) ? Mathf.Max(1f, PawnRushSkills.carpetBoost) : 1f;

        bool OnCarpet4(Vector3 p)
        {
            var s = PawnRushSkills;
            if (carpet4 == null || s == null) return false;
            Vector3 d = Flat(p - carpet4.from), right = Vector3.Cross(Vector3.up, carpet4.dir);
            float along = Vector3.Dot(d, carpet4.dir), across = Vector3.Dot(d, right);
            return along >= -0.3f && along <= carpet4.rolled && Mathf.Abs(across) <= s.carpetWide * 0.5f && Mathf.Abs(p.y - carpet4.from.y) < 1.2f;
        }

        static float SegmentDistance4(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (a + ab * t - p).magnitude;
        }

        // ---------------------------------------------------------------- 폰

        void UpdatePromoCountdown(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill() || promo4Left > 0f) return;
            SkillUses++;
            promo4Left = s.promoSeconds;
            Fx(SkillFxKind.S4Promo, null, Chest, FlatDir(facing), 0, s.promoSeconds);
            SkillDetail = "승격 5초 전";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.PromoCountdown, s));
        }

        // ---------------------------------------------------------------- 나이트

        void UpdateBurrow(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planDir = FlatDir(facing);
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SetBurrowed4(true);
                    SkillDetail = "땅속 헤엄 (키 = 솟기)";
                    Fx(SkillFxKind.S4Burrow, null, FeetPoint, planDir, 0, s.burrowSeconds);
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    GuardFromSkills(0.25f);
                    Vector3 way = Flat(skillMoveRaw).sqrMagnitude > 0.04f ? FlatDir(skillMoveRaw) : planDir;
                    planDir = way;
                    // An edge ahead: it comes up here.
                    bool edge = !SolidRay(FeetPoint + way * 0.8f + Vector3.up * 0.6f, Vector3.down, 1.6f, out _);
                    if (!edge)
                    {
                        skillDir = way;
                        dashBase = s.burrowSpeed;
                        DriveDash(dt);
                    }
                    if ((press && planTime > 0.25f) || planTime >= s.burrowSeconds || edge)
                    {
                        HaltDash();
                        SetBurrowed4(false);
                        // Up: whoever stands over it goes up with a boom.
                        int n = 0;
                        float vy = Mathf.Sqrt(2f * Gravity4 * Mathf.Max(0.2f, s.burrowUp));
                        foreach (var o in All)
                        {
                            if (!UltFoe(o)) continue;
                            Vector3 off = Flat(o.bodies[0].position - bodies[0].position);
                            if (off.magnitude > s.burrowReach || Mathf.Abs(o.bodies[0].position.y - bodies[0].position.y) > 1.5f) continue;
                            n++;
                            SkillHit(o, (off.sqrMagnitude > 0.01f ? off.normalized : planDir) * 1.5f + Vector3.up * vy, true, "땅속 헤엄");
                            Fx(SkillFxKind.S3Hit, o, o.LassoBelly, Vector3.up, n);
                        }
                        Fx(SkillFxKind.S4Burrow, null, FeetPoint, planDir, 1, n);
                        SkillDetail = n > 0 ? $"땅속 헤엄: 솟아 {n}명" : "땅속 헤엄: 솟음";
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.3f)
                    {
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.Burrow, s));
                    }
                    break;
            }
        }

        /// <summary>Under the floor: through every piece (and back).</summary>
        void SetBurrowed4(bool on)
        {
            if (burrowed4 == on) return;
            burrowed4 = on;
            foreach (var o in All)
                if (o != null && o != this) IgnorePiece(o, on);
        }

        // ---------------------------------------------------------------- 비숍

        void UpdateSafetyPin(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill(true)) return;
            SkillUses++;
            Vector3 dir = FlatDir(AimRay());
            pin4 = new Pin4 { pos = Chest + Vector3.up * 0.15f, dir = dir };
            Fx(SkillFxKind.S4Pin, null, pin4.pos, dir, 0, s.pinSpeed, pin4.pos + dir * s.pin4Range);
            SkillDetail = "핀!";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.SafetyPin, s));
        }

        // ---------------------------------------------------------------- 룩

        void UpdateRookDive(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planPoint = AimFloor4(s.diveRange);
                    planDir = FlatDir(planPoint - FeetPoint);
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "더 루우욱! (웅크림)";
                    Fx(SkillFxKind.S4Dive, null, planPoint, planDir, 0, s.diveCrouch + s.diveFlight);
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    if (planTime < s.diveCrouch) break;
                    // Off: a throw timed to come down on the mark in diveFlight seconds.
                    Vector3 from = FeetPoint, to = planPoint;
                    float T = Mathf.Max(0.3f, s.diveFlight);
                    Launch(Flat(to - from) / T + Vector3.up * ((to.y - from.y) / T + 0.5f * Gravity4 * T));
                    Say4(this, SayRookDive);
                    Fx(SkillFxKind.S4Dive, null, from, planDir, 1, T, to);
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Active:
                {
                    planTime += dt;
                    if (!((planTime > 0.3f && Grounded) || planTime > s.diveFlight + 0.6f)) break;
                    // Down head first: everyone near flies and flops; the rook is stuck upside down.
                    Vector3 me = bodies[0].position;
                    int n = 0;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o)) continue;
                        Vector3 off = Flat(o.bodies[0].position - me);
                        if (off.magnitude > s.diveReach || Mathf.Abs(o.bodies[0].position.y - me.y) > 2.5f) continue;
                        n++;
                        Hit4(o, off.sqrMagnitude > 0.01f ? off : planDir, 3, Cc4.Flop, s.diveFlop, "더 루우욱!", n);
                    }
                    HoldStill(s.diveStuck);
                    Fx(SkillFxKind.S4Dive, null, FeetPoint, planDir, 2, s.diveStuck);
                    SkillDetail = n > 0 ? $"더 루우욱! {n}명" : "더 루우욱! (아무도 없음)";
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < s.diveStuck) break;
                    Fx(SkillFxKind.S4Dive, null, FeetPoint, planDir, 3);
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.RookDive, s));
                    break;
            }
        }

        void UpdateTimber(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planDir = FlatDir(facing);
                    planPoint = FeetPoint;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    HoldStill(s.timberGrow + s.timberFall + s.timberBridge);
                    SkillDetail = "넘어간다~! (자람)";
                    Fx(SkillFxKind.S4Timber, null, planPoint, planDir, 0, s.timberGrow);
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    if (planTime < s.timberGrow) break;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    Fx(SkillFxKind.S4Timber, null, planPoint, planDir, 1, s.timberFall);
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    if (planTime < s.timberFall) break;
                    // Down along its line: whoever is under it flops.
                    Vector3 right = Vector3.Cross(Vector3.up, planDir);
                    int n = 0;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o)) continue;
                        Vector3 d = Flat(o.bodies[0].position - planPoint);
                        float along = Vector3.Dot(d, planDir), across = Vector3.Dot(d, right);
                        if (along < 0.3f || along > s.timberTall || Mathf.Abs(across) > s.timberWide * 0.5f + 0.3f) continue;
                        n++;
                        o.GiveCc(Cc4.Flop, s.timberFlop, this);
                        Fx(SkillFxKind.S3Hit, o, o.bodies[(int)BodyId.Head].position, Vector3.down, n);
                    }
                    // A bridge for its seconds: the fallen tower's body, low and wide (its picture is the tower itself).
                    bridge4 = PawnRushKitWall.Raise(PawnRushKitWall.NextId(), this, planPoint + planDir * 0.6f, planPoint + planDir * s.timberTall,
                        0.75f, s.timberWide, s.timberBridge, 0.05f, true);
                    bridge4.Hidden = true;   // (the fallen rook is the bridge's picture)
                    Fx(SkillFxKind.S4Timber, null, planPoint, planDir, 2, s.timberBridge, planPoint + planDir * s.timberTall);
                    SkillDetail = n > 0 ? $"넘어간다~! {n}명 콰당!" : "넘어간다~! (다리)";
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < s.timberBridge) break;
                    Fx(SkillFxKind.S4Timber, null, planPoint, planDir, 3);
                    bridge4 = null;
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.Timber, s));
                    break;
            }
        }

        void UpdateGatePig(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    SpitAll4(s);
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "돼지 (성문 열림)";
                    Fx(SkillFxKind.S4Pig, null, Chest, FlatDir(facing), 0, s.pigSeconds);
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    Vector3 me = bodies[0].position, face = FlatDir(facing), mouth = me + face * 0.7f;
                    int mouthful = Mathf.Max(1, s.pigMouthful);
                    foreach (var o in All)
                    {
                        if (!UltFoe(o) || swallowed4.Contains(o) || swallowed4.Count >= mouthful) continue;
                        Vector3 off = Flat(o.bodies[0].position - me);
                        float d = off.magnitude;
                        if (d > s.pigRange || (d > 0.1f && Vector3.Angle(face, off) > s.pigCone * 0.5f) || Mathf.Abs(o.bodies[0].position.y - me.y) > 2f) continue;
                        if (Flat(o.bodies[0].position - mouth).magnitude < 0.9f)
                        {
                            // Gulp: limp inside it (no keys) until it is spat out. (The moment first, so every screen
                            // knows it is out of sight before its 콰당! comes.)
                            swallowed4.Add(o);
                            IgnorePiece(o, true);
                            Fx(SkillFxKind.S4Pig, o, mouth, face, 1);
                            o.GiveCc(Cc4.Flop, Mathf.Max(0.5f, s.pigSeconds - planTime) + s.pigHold + 0.3f, this);
                            continue;
                        }
                        // Pulled in about as hard as one runs: running away holds it.
                        Vector3 toward = -off / Mathf.Max(0.01f, d);
                        float inward = Vector3.Dot(Flat(o.bodies[0].linearVelocity), toward);
                        if (inward < s.pigPull) o.AddVelocity(toward * ((s.pigPull - inward) * Mathf.Clamp01(dt * 8f)));
                    }
                    HoldSwallowed4();
                    if (planTime < s.pigSeconds && !(press && planTime > 0.3f && swallowed4.Count > 0)) break;
                    // The gate shuts on what it has; it is spat out pigHold later (or on the key).
                    Fx(SkillFxKind.S4Pig, null, Chest, face, 3);
                    SkillDetail = swallowed4.Count > 0 ? $"돼지: {swallowed4.Count}명 삼킴" : "돼지 (아무도 없음)";
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    if (press || swallowed4.Count == 0) planTime = s.pigHold;   // (the key again: out at once)
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    HoldSwallowed4();
                    if (planTime < s.pigHold && !(press && planTime > 0.2f)) break;
                    SpitAll4(s);
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.GatePig, s));
                    break;
            }
        }

        /// <summary>The swallowed kept inside the rook: drawn to its middle every step (they pass through it).</summary>
        void HoldSwallowed4()
        {
            Vector3 inside = bodies[0].position + Vector3.up * 0.1f;
            foreach (var o in swallowed4)
            {
                if (o == null) continue;
                Vector3 to = inside - o.bodies[0].position;
                o.AddVelocity(Vector3.ClampMagnitude(to * 20f, 20f) - o.bodies[0].linearVelocity);
            }
        }

        /// <summary>Out of the back with a bang, everyone swallowed.</summary>
        void SpitAll4(PawnRushSkillParams s)
        {
            if (swallowed4.Count == 0) return;
            Vector3 back = -FlatDir(facing);
            int n = 0;
            foreach (var o in swallowed4)
            {
                if (o == null) continue;
                n++;
                IgnorePiece(o, false);
                Fx(SkillFxKind.S4Pig, o, bodies[0].position + back * 0.8f, back, 2);   // (in sight again before its 콰당!)
                o.GiveCc(Cc4.Flop, 0.8f, this);   // (down a moment where it lands, not the rest of its time inside)
                SkillHit(o, Throw4(back + Vector3.Cross(Vector3.up, back) * ((n - 2) * 0.3f), 2), true, "돼지");
                Fx(SkillFxKind.S3Hit, o, o.LassoBelly, back, n);
            }
            swallowed4.Clear();
        }

        void UpdateRookLift(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill()) return;
            SkillUses++;
            Vector3 feet = FeetPoint;
            if (SolidRay(feet + Vector3.up, Vector3.down, 3f, out var floor)) feet = floor.point;
            PawnRushLiftTower.Raise(feet, s.liftWide, s.liftTall, s.liftRise, s.liftSeconds);
            Fx(SkillFxKind.S4Lift, null, feet, FlatDir(facing), 0, s.liftSeconds);
            SkillDetail = "룩 리프트";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.RookLift, s));
        }

        // ---------------------------------------------------------------- 퀸

        void UpdateProcession(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill()) return;
            SkillUses++;
            Vector3 dir = FlatDir(AimRay()), from = FeetPoint;
            carpet4 = new Carpet4 { from = from, dir = dir, left = s.carpetLength / Mathf.Max(1f, s.carpetSpeed) + s.carpetSeconds };
            Fx(SkillFxKind.S4Carpet, null, from, dir, 0, s.carpetSpeed, new Vector3(s.carpetLength, s.carpetWide, s.carpetSeconds));
            SkillDetail = "행차";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.Procession, s));
        }

        void UpdateThroneDrop(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planPoint = AimFloor4(s.throneRange);
                    planDir = FlatDir(planPoint - FeetPoint);
                    // Up out of sight and down on the shadow throneDelay later: one high throw timed to it.
                    Vector3 from = FeetPoint, to = planPoint;
                    float T = Mathf.Max(0.4f, s.throneDelay + 0.3f);
                    Launch(Flat(to - from) / T + Vector3.up * ((to.y - from.y) / T + 0.5f * Gravity4 * T));
                    GuardFromSkills(T);
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "착석!";
                    Fx(SkillFxKind.S4Throne, null, planPoint, planDir, 0, T, from);
                    break;
                }
                case PlanPhase.Active:
                {
                    planTime += dt;
                    if (!((planTime > 0.4f && Grounded) || planTime > s.throneDelay + 1.2f)) break;
                    // Down on the throne: the shadow's enemies boom away; she sits a moment.
                    Vector3 me = bodies[0].position;
                    int n = 0;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o)) continue;
                        Vector3 off = Flat(o.bodies[0].position - me);
                        if (off.magnitude > s.throneSize * 0.5f || Mathf.Abs(o.bodies[0].position.y - me.y) > 2.5f) continue;
                        n++;
                        Hit4(o, off.sqrMagnitude > 0.01f ? off : planDir, 3, Cc4.None, 0f, "착석!", n);
                    }
                    HoldStill(s.throneSit);
                    Fx(SkillFxKind.S4Throne, null, FeetPoint, planDir, 1, s.throneSit);
                    SkillDetail = n > 0 ? $"착석! {n}명" : "착석!";
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime < s.throneSit) break;
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.ThroneDrop, s));
                    break;
            }
        }

        void UpdateQueensGambit(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill(true)) return;
            SkillUses++;
            Vector3 c = AimFloor4(s.gambitRange);
            float r = Mathf.Max(0f, s.gambitSize * 0.5f - s.gambitPiece * 0.5f);
            gambit4.Clear();
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = GambitSpot4(c, r, i);
                if (SolidRay(p + Vector3.up * 3f, Vector3.down, 8f, out var floor)) p = floor.point;
                gambit4.Add(new Gambit4 { at = p, wait = s.gambitDelay + i * s.gambitEvery, index = i });
            }
            Fx(SkillFxKind.S4Gambit, null, c, FlatDir(c - FeetPoint), 0, s.gambitDelay, new Vector3(r, s.gambitEvery, s.gambitHigh));
            SkillDetail = "퀸스 갬빗";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.QueensGambit, s));
        }

        /// <summary>Where the gambit's piece <paramref name="i"/> hangs: the middle, then four round it (any PC works it out).</summary>
        public static Vector3 GambitSpot4(Vector3 center, float radius, int i) =>
            i == 0 ? center : center + Quaternion.AngleAxis(45f + (i - 1) * 90f, Vector3.up) * Vector3.forward * radius;

        // ---------------------------------------------------------------- keys

        /// <summary>Before the run (SetFourKeys): what bundle 다 lets the keys do in its phases.</summary>
        void SetFourCKeys()
        {
            switch (activeSkill)
            {
                case PawnRushSkillIds.Burrow:
                case PawnRushSkillIds.RookDive:
                case PawnRushSkillIds.Timber:
                case PawnRushSkillIds.ThroneDrop:
                    // Carried by the skill (swimming under the floor, a throw, a tower, a leap): the keys wait
                    // (ROUND1: 넘어간다~! and the stuck 더 루우욱! "조작 없음").
                    if (activeSkill == PawnRushSkillIds.Burrow && planPhase != PlanPhase.Active) break;
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    input.grab = false;
                    break;
                case PawnRushSkillIds.GatePig:
                    // Its gate open: walking only.
                    if (planPhase != PlanPhase.Active) break;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
            }
        }

        // ---------------------------------------------------------------- the things left in the world (every step)

        /// <summary>The host's, every step (UpdateSetFourStates): the countdown, the pin, the carpet, the falling pieces,
        /// and whatever a skill cut short left (a knight still under the floor, pieces still swallowed).</summary>
        void UpdateSetFourCStates(PawnRushSkillParams s, float dt)
        {
            if (burrowed4 && (activeSkill != PawnRushSkillIds.Burrow || planPhase != PlanPhase.Active))
            {
                SetBurrowed4(false);
                Fx(SkillFxKind.S4Burrow, null, FeetPoint, FlatDir(facing), 1, 0f);
            }
            if (swallowed4.Count > 0 && (activeSkill != PawnRushSkillIds.GatePig || planPhase == PlanPhase.None)) SpitAll4(s);
            if (promo4Left > 0f) StepPromo4(s, dt);
            if (pin4 != null) StepPin4(s, dt);
            if (carpet4 != null) StepCarpet4(s, dt);
            if (gambit4.Count > 0) StepGambit4(s, dt);
        }

        void StepPromo4(PawnRushSkillParams s, float dt)
        {
            if (State != PawnState.Active || Floating)
            {
                // Knocked over first: 푸쉬식.
                promo4Left = 0f;
                Say4(this, SayFizzle);
                Fx(SkillFxKind.S4Promo, null, Chest, Vector3.up, 2);
                SkillDetail = "승격 5초 전: 푸쉬식…";
                return;
            }
            promo4Left -= dt;
            if (promo4Left > 0f) return;
            // 승격!: everyone near flies; gold a while.
            Vector3 me = bodies[0].position;
            int n = 0;
            foreach (var o in All)
            {
                if (!UltFoe(o)) continue;
                Vector3 off = Flat(o.bodies[0].position - me);
                if (off.magnitude > s.promoRange || Mathf.Abs(o.bodies[0].position.y - me.y) > 2.5f) continue;
                n++;
                Hit4(o, off.sqrMagnitude > 0.01f ? off : FlatDir(facing), 3, Cc4.None, 0f, "승격!", n);
            }
            Say4(this, SayPromoted);
            Fx(SkillFxKind.S4Promo, null, Chest, Vector3.up, 1, s.promoGold);
            SkillDetail = n > 0 ? $"승격! {n}명" : "승격!";
        }

        void StepPin4(PawnRushSkillParams s, float dt)
        {
            float step = s.pinSpeed * dt;
            if (SolidRay(pin4.pos, pin4.dir, step, out var wall))
            {
                Fx(SkillFxKind.S4Pin, null, wall.point, pin4.dir, 2);
                pin4 = null;
                return;
            }
            Vector3 next = pin4.pos + pin4.dir * step;
            RagdollPawn hit = null;
            float best = s.pinReach;
            foreach (var o in All)
            {
                if (!UltFoe(o)) continue;
                float d = SegmentDistance4(o.bodies[(int)BodyId.Chest].position, pin4.pos, next);
                if (d <= best)
                {
                    best = d;
                    hit = o;
                }
            }
            if (hit != null)
            {
                // Its clothes pinned to the floor where it stands.
                Vector3 spot = hit.FeetPoint;
                hit.GiveCc(Cc4.Pinned, s.pin4Seconds, this, spot);
                Fx(SkillFxKind.S4Pin, hit, spot, pin4.dir, 1, s.pin4Seconds);
                Fx(SkillFxKind.S3Hit, hit, hit.LassoBelly, pin4.dir, 1);
                pin4 = null;
                return;
            }
            pin4.pos = next;
            pin4.flown += step;
            if (pin4.flown < s.pin4Range) return;
            Fx(SkillFxKind.S4Pin, null, next, pin4.dir, 2);
            pin4 = null;
        }

        void StepCarpet4(PawnRushSkillParams s, float dt)
        {
            carpet4.left -= dt;
            if (carpet4.rolled < s.carpetLength)
            {
                float before = carpet4.rolled;
                carpet4.rolled = Mathf.Min(s.carpetLength, before + s.carpetSpeed * dt);
                // The rolling front: whoever it reaches is rolled up in it and pushed on.
                Vector3 right = Vector3.Cross(Vector3.up, carpet4.dir);
                foreach (var o in All)
                {
                    if (!UltFoe(o) || carpet4.rolledUp.Contains(o)) continue;
                    Vector3 d = Flat(o.bodies[0].position - carpet4.from);
                    float along = Vector3.Dot(d, carpet4.dir), across = Vector3.Dot(d, right);
                    if (along < before - 0.6f || along > carpet4.rolled + 0.4f || Mathf.Abs(across) > s.carpetWide * 0.5f + 0.2f) continue;
                    if (Mathf.Abs(o.bodies[0].position.y - carpet4.from.y) > 2f) continue;
                    carpet4.rolledUp.Add(o);
                    o.GiveCc(Cc4.Flop, s.carpetRoll, this);
                    o.AddVelocity(carpet4.dir * (s.carpetPush * 1.4f) + Vector3.up * 1.2f);
                    Fx(SkillFxKind.S4Carpet, o, o.bodies[0].position, carpet4.dir, 1, s.carpetRoll);
                }
            }
            if (carpet4.left <= 0f) carpet4 = null;
        }

        void StepGambit4(PawnRushSkillParams s, float dt)
        {
            for (int i = gambit4.Count - 1; i >= 0; i--)
            {
                var g = gambit4[i];
                if (!g.dropping)
                {
                    g.wait -= dt;
                    if (g.wait <= 0f) g.dropping = true;
                    continue;
                }
                g.fall += dt;
                if (g.fall < s.gambitFall) continue;
                // Down: whoever is under it is squashed flat.
                int n = 0;
                foreach (var o in All)
                {
                    if (!UltFoe(o)) continue;
                    Vector3 off = Flat(o.bodies[0].position - g.at);
                    if (off.magnitude > s.gambitPiece * 0.5f + 0.2f || Mathf.Abs(o.bodies[0].position.y - g.at.y) > 2f) continue;
                    n++;
                    SkillHit(o, Vector3.down * 3f + (off.sqrMagnitude > 0.01f ? off.normalized : Vector3.forward) * 2f, true, "퀸스 갬빗");
                    Fx(SkillFxKind.S3Hit, o, o.bodies[(int)BodyId.Head].position, Vector3.down, n);
                }
                Fx(SkillFxKind.S4Gambit, null, g.at, Vector3.up, 1 + g.index, n);
                gambit4.RemoveAt(i);
            }
        }

        void ClearSetFourCStates()
        {
            SetBurrowed4(false);
            foreach (var o in swallowed4)
                if (o != null) IgnorePiece(o, false);
            swallowed4.Clear();
            promo4Left = 0f;
            pin4 = null;
            carpet4 = null;
            gambit4.Clear();
            bridge4 = null;
        }
    }
}
