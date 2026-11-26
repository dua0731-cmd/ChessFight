using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 4판, round 1 (Docs/Skills/PAWN_RUSH_SET4_ROUND1.md; the user 11-26: "이 게임은 웃긴 게임이라고. 래그돌 물리
    /// 기반의, 체스 컨셉의." and "뭐가됐던 웃긴 상황을 만들어낼수 있는게 첫번째 고려사항이여야 하고"). The first bundle (가),
    /// eleven skills, each on one key (its slot's), the piece walking and running meanwhile unless it says otherwise:
    ///
    ///   폰    앙파상! (both feet out 0.6 s: an enemy passing close trips flat, 콰당! + "홀리 헬!"), 벽돌 (a brick over the aimed
    ///         enemy's head drops after 0.6 s: 헤롱헤롱), 구덩이 (a pit just behind: the first enemy in is stuck to the waist)
    ///   나이트 포크! (a leap to the aim point; two enemies near the landing knock heads, one is stomped: 헤롱헤롱),
    ///         뒷발차기 (the kick as it was, but a bang and 헤롱헤롱: RagdollPawn.BookSkills.cs)
    ///   비숍  성수병 (a bottle to the aim point, 15 m: the puddle makes 미끌미끌), 설교 (a little bishop on the aimed
    ///         enemy's shoulder: 지침)
    ///   룩    직진! (a bow, then 12 m straight at 10 m/s: whoever it meets flies; a wall makes the rook 헤롱헤롱),
    ///         돌 얹기 (a wall stone set on the aimed enemy's head: 무거움)
    ///   퀸    왕관 헌납 (a crown thrown along the aim: on a head, 무거움), 무엄하다! (a breath and a shout: the enemies in
    ///         front go 쪼꼬미)
    ///
    /// The three throws (ROUND1 2장): light 2 m / 0.5 m up, bang 5 m / 2 m, boom 8 m / 3.5 m. "The aimed enemy" is the one
    /// nearest the camera's line in the skill's reach; a skill that needs one does not go, and takes no cooldown, without
    /// one. No edge guard: what flies over an edge falls. The host runs it all; every PC draws it from the S4 moments
    /// (PawnRushSkillFx.SetFour.cs). Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // The words in a speech bubble (SkillFxKind.S4Say's count).
        public static readonly string[] Words4 = { "", "홀리 헬!", "무엄하다!", "댕~", "체크!", "히힝!", "희생… 더 루우욱!", "승격!", "5", "4", "3", "2", "1", "!!", "푸쉬식…", "혹!" };
        public const int SayHolyHell = 1, SayInsolent = 2, SayBell = 3, SayCheck = 4, SayNeigh = 5, SayRookDive = 6, SayPromoted = 7,
                         SayFive = 8, SaySac = 13, SayFizzle = 14, SayBump = 15;

        sealed class Brick4 { public RagdollPawn aimed; public Vector3 at; public float wait, fall; public bool dropping; }
        sealed class Pit4 { public Vector3 at; public float left; }
        sealed class Water4 { public Vector3 at; public float wait, left; public bool broken; public readonly HashSet<RagdollPawn> wet = new HashSet<RagdollPawn>(); }
        sealed class Crown4 { public Vector3 from, to; public float age, flight; }
        sealed class PitOut { public RagdollPawn who; public float left; }

        readonly List<Brick4> bricks4 = new List<Brick4>();
        readonly List<Pit4> pits4 = new List<Pit4>();
        readonly List<Water4> waters4 = new List<Water4>();
        readonly List<Crown4> crowns4 = new List<Crown4>();
        readonly List<PitOut> pitOuts4 = new List<PitOut>();
        readonly HashSet<RagdollPawn> hit4 = new HashSet<RagdollPawn>();
        float forkFlight4;

        // ---------------------------------------------------------------- shared

        void Say4(RagdollPawn over, int word)
        {
            if (over == null) return;
            Fx(SkillFxKind.S4Say, over, over.bodies[(int)BodyId.Head].position + Vector3.up * 0.55f, Vector3.up, word);
        }

        /// <summary>The set's three throws (ROUND1 2장): 1 light (2 m, 0.5 m up), 2 a bang (5 m, 2 m), 3 a boom (8 m, 3.5 m) -
        /// the speed for that arc on level ground.</summary>
        Vector3 Throw4(Vector3 dir, int strength)
        {
            var s = PawnRushSkills;
            float dist = strength >= 3 ? (s != null ? s.s4BoomDist : 8f) : strength == 2 ? (s != null ? s.s4BangDist : 5f) : (s != null ? s.s4LightDist : 2f);
            float high = strength >= 3 ? (s != null ? s.s4BoomUp : 3.5f) : strength == 2 ? (s != null ? s.s4BangUp : 2f) : (s != null ? s.s4LightUp : 0.5f);
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            float vy = Mathf.Sqrt(2f * g * Mathf.Max(0.05f, high));
            float air = 2f * vy / g;
            return FlatDir(dir) * (dist / air) + Vector3.up * vy;
        }

        /// <summary>A hit of the set: thrown (0 for none) - it falls the way ragdolls fall - then a hindrance, with the comic
        /// smack on every screen.</summary>
        string Hit4(RagdollPawn e, Vector3 dir, int strength, Cc4 kind, float seconds, string cause, int count = 1)
        {
            if (e == null) return "-";
            string result = strength > 0 ? SkillHit(e, Throw4(dir, strength), true, cause) : "-";
            if (kind != Cc4.None) e.GiveCc(kind, seconds, this);
            Fx(SkillFxKind.S3Hit, e, e.LassoBelly, FlatDir(dir), count);
            return result;
        }

        /// <summary>"The aimed enemy": in reach (and within <paramref name="frontDeg"/> of the camera's way, and in sight of
        /// the head if <paramref name="sight"/>), the one nearest the camera's line from the head.</summary>
        RagdollPawn AimedFoe4(float reach, float frontDeg = 360f, bool sight = false)
        {
            Vector3 eye = bodies[(int)BodyId.Head].position, aim = AimRay(), me = bodies[0].position, way = FlatDir(aim);
            RagdollPawn best = null;
            float bestScore = float.MaxValue;
            foreach (var o in All)
            {
                if (!UltFoe(o)) continue;
                Vector3 at = o.bodies[(int)BodyId.Chest].position, flat = Flat(at - me);
                if (flat.magnitude > reach) continue;
                if (frontDeg < 359f && flat.sqrMagnitude > 0.01f && Vector3.Angle(way, flat) > frontDeg * 0.5f) continue;
                Vector3 rel = at - eye;
                if (sight)
                {
                    Vector3 to = o.bodies[(int)BodyId.Head].position - eye;
                    if (SolidRay(eye, to.normalized, Mathf.Max(0f, to.magnitude - 0.3f), out _)) continue;
                }
                float along = Vector3.Dot(rel, aim);
                float side = (rel - aim * along).magnitude;
                float score = side + (along < 0f ? 3f - along : 0.03f * along);   // (behind the line of sight: last)
                if (score < bestScore)
                {
                    bestScore = score;
                    best = o;
                }
            }
            return best;
        }

        /// <summary>Where the camera's line meets the floor, no farther than <paramref name="reach"/> from the feet (flat).</summary>
        Vector3 AimFloor4(float reach)
        {
            Vector3 feet = FeetPoint, p = AimPoint(reach + 20f);
            Vector3 flat = Flat(p - feet);
            if (flat.magnitude > reach) p = feet + flat.normalized * reach + Vector3.up * (p.y - feet.y);
            return SolidRay(p + Vector3.up * 2f, Vector3.down, 8f, out var hit) ? hit.point : new Vector3(p.x, feet.y, p.z);
        }

        void SkipNoTarget4(string what)
        {
            SkillDetail = $"{what}: 겨눈 적이 없어요 (쿨 없음)";
            Fx(SkillFxKind.SkillFail, null, FeetPoint, AimFlat(), (int)piece);
        }

        // ---------------------------------------------------------------- 폰

        void UpdateTrip4(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    hit4.Clear();
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    planDir = FlatDir(facing);
                    SkillDetail = "앙파상! 양옆으로 발 쭉";
                    Fx(SkillFxKind.S4Trip, null, FeetPoint, planDir, 0, s.tripWindow);
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    Vector3 me = bodies[0].position, right = Vector3.Cross(Vector3.up, FlatDir(facing));
                    foreach (var o in All)
                    {
                        if (!UltFoe(o) || hit4.Contains(o)) continue;
                        Vector3 off = Flat(o.bodies[0].position - me);
                        float d = off.magnitude;
                        if (d > s.tripReach || d < 0.15f || Mathf.Abs(o.bodies[0].position.y - me.y) > 1.2f) continue;
                        if (Mathf.Min(Vector3.Angle(off, right), Vector3.Angle(off, -right)) > s.tripCone * 0.5f) continue;
                        hit4.Add(o);
                        // Over the foot, on its face, the way it was going.
                        Vector3 going = Flat(o.bodies[0].linearVelocity);
                        Vector3 way = going.sqrMagnitude > 0.5f ? going.normalized : FlatDir(off);
                        SkillHit(o, way * 2.5f + Vector3.up * 0.5f, true, "앙파상!", false, s.tripFlop);
                        o.GiveCc(Cc4.Flop, s.tripFlop, this);
                        Say4(o, SayHolyHell);
                        Fx(SkillFxKind.S4Trip, o, o.FeetPoint, way, 1);
                        Fx(SkillFxKind.S3Hit, o, o.FeetPoint + Vector3.up * 0.3f, way, hit4.Count);
                        SkillDetail = $"앙파상! {o.DisplayName} 콰당!";
                    }
                    if (planTime >= s.tripWindow)
                    {
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.Trip4, s));
                    }
                    break;
                }
            }
        }

        void UpdateBrick(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill(true)) return;
            var aimed = AimedFoe4(s.brickRange);
            if (aimed == null)
            {
                SkipNoTarget4("벽돌");
                return;
            }
            SkillUses++;
            Vector3 at = aimed.bodies[(int)BodyId.Head].position + Vector3.up * s.brickHigh;
            bricks4.Add(new Brick4 { aimed = aimed, at = at, wait = s.brickDelay });
            Fx(SkillFxKind.S4Brick, aimed, at, Vector3.down, 0, s.brickDelay);
            SkillDetail = $"벽돌 → {aimed.DisplayName}";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.Brick, s));
        }

        void UpdatePit(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill()) return;
            SkillUses++;
            Vector3 at = FeetPoint - FlatDir(facing) * s.pitBack;
            if (SolidRay(at + Vector3.up * 1f, Vector3.down, 3f, out var hit)) at = hit.point;
            pits4.Add(new Pit4 { at = at, left = s.pitSeconds });
            Fx(SkillFxKind.S4Pit, null, at, Vector3.up, 0, s.pitSeconds);
            SkillDetail = "구덩이 (뒤에)";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.Pit, s));
        }

        // ---------------------------------------------------------------- 나이트

        void UpdateForkBonk(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    Vector3 from = FeetPoint, to = AimFloor4(s.forkRange);
                    planPoint = to;
                    planDir = FlatDir(to - from);
                    forkFlight4 = LeapTo4(to, s.forkHigh);
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "포크! 뛰어오름";
                    Fx(SkillFxKind.S4Leap, null, from, planDir, 0, forkFlight4, to);
                    break;
                }
                case PlanPhase.Active:
                    planTime += dt;
                    if ((planTime > 0.25f && Grounded) || planTime > forkFlight4 + 0.8f)
                    {
                        ForkLand4(s);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.3f)
                    {
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.ForkBonk, s));
                    }
                    break;
            }
        }

        /// <summary>A thrown arc from the feet to <paramref name="to"/>, its top <paramref name="high"/> over the higher end;
        /// returns its time.</summary>
        float LeapTo4(Vector3 to, float high)
        {
            Vector3 from = FeetPoint;
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            float top = Mathf.Max(from.y, to.y) + Mathf.Max(0.3f, high);
            float vy = Mathf.Sqrt(2f * g * (top - from.y));
            float t = vy / g + Mathf.Sqrt(2f * Mathf.Max(0.01f, top - to.y) / g);
            Vector3 flat = Flat(to - from);
            Launch(flat / Mathf.Max(0.1f, t) + Vector3.up * vy);
            return t;
        }

        void ForkLand4(PawnRushSkillParams s)
        {
            Vector3 feet = FeetPoint;
            RagdollPawn a = null, b = null;
            float da = float.MaxValue, db = float.MaxValue;
            foreach (var o in All)
            {
                if (!UltFoe(o)) continue;
                float d = Flat(o.bodies[0].position - feet).magnitude;
                if (d > s.forkReach || Mathf.Abs(o.bodies[0].position.y - bodies[0].position.y) > 1.5f) continue;
                if (d < da) { b = a; db = da; a = o; da = d; }
                else if (d < db) { b = o; db = d; }
            }
            Fx(SkillFxKind.KnightLand, null, feet, planDir, 0, 0.5f);
            if (a != null && b != null)
            {
                // Two: their heads knocked together - each flung at the other.
                Vector3 ha = a.bodies[(int)BodyId.Head].position, hb = b.bodies[(int)BodyId.Head].position, mid = (ha + hb) * 0.5f;
                a.AddVelocity(FlatDir(hb - ha) * 4f + Vector3.up * 1f);
                b.AddVelocity(FlatDir(ha - hb) * 4f + Vector3.up * 1f);
                a.GiveCc(Cc4.Dizzy, s.forkDizzyTwo, this);
                b.GiveCc(Cc4.Dizzy, s.forkDizzyTwo, this);
                Fx(SkillFxKind.S4Bonk, a, mid, Vector3.up, 2, 0f, hb);
                SkillDetail = $"포크! {a.DisplayName} · {b.DisplayName} 박치기";
            }
            else if (a != null)
            {
                // One: its head stomped.
                SkillHit(a, Vector3.down * 2f + FlatDir(a.bodies[0].position - feet) * 1.5f, false, "포크! 밟기");
                a.GiveCc(Cc4.Dizzy, s.forkDizzyOne, this);
                Fx(SkillFxKind.S4Bonk, a, a.bodies[(int)BodyId.Head].position, Vector3.up, 1);
                SkillDetail = $"포크! {a.DisplayName} 밟기";
            }
            else SkillDetail = "포크! 아무도 없음";
        }

        // ---------------------------------------------------------------- 비숍

        void UpdateHolyWater4(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill(true)) return;
            SkillUses++;
            Vector3 from = Chest + Vector3.up * 0.4f, to = AimFloor4(s.water4Range);
            waters4.Add(new Water4 { at = to, wait = s.water4Flight, left = s.water4Seconds });
            Fx(SkillFxKind.S3WaterThrow, null, from, FlatDir(to - from), 0, s.water4Flight, to);
            SkillDetail = "성수병 (겨눈 곳에)";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.HolyWater4, s));
        }

        void UpdateSermon(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill(true)) return;
            var aimed = AimedFoe4(s.sermonRange, 360f, true);
            if (aimed == null)
            {
                SkipNoTarget4("설교");
                return;
            }
            SkillUses++;
            aimed.GiveCc(Cc4.Tired, s.sermonTired, this);
            Fx(SkillFxKind.S4Sermon, aimed, Chest + Vector3.up * 0.4f, FlatDir(aimed.bodies[0].position - bodies[0].position), 0, s.sermonTired);
            SkillDetail = $"설교 → {aimed.DisplayName} 지침";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.Sermon, s));
        }

        // ---------------------------------------------------------------- 룩

        void UpdateCharge4(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    hit4.Clear();
                    planDir = FlatDir(facing);   // the way it faced when the key went: no turning after
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "직진! (고개 숙임)";
                    Fx(SkillFxKind.S4Charge, null, FeetPoint, planDir, 0, s.charge4Bow);
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    if (planTime < s.charge4Bow) break;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    skillDir = planDir;
                    dashBase = s.charge4Speed;
                    SkillDetail = "직진!";
                    Fx(SkillFxKind.S4Charge, null, FeetPoint, planDir, 1, s.charge4Dist / Mathf.Max(1f, s.charge4Speed));
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    skillDir = planDir;
                    dashBase = s.charge4Speed;
                    DriveDash(dt);
                    Vector3 chest = Chest;
                    // A wall ahead: it stops dead and sees stars.
                    if (SolidRay(chest, planDir, 0.75f, out var wall) && Vector3.Dot(wall.normal, -planDir) > 0.5f)
                    {
                        HaltDash();
                        GiveCc(Cc4.Dizzy, s.charge4WallDizzy, null);
                        Fx(SkillFxKind.S4Charge, null, wall.point, planDir, 2);
                        SkillDetail = "직진! 벽에 쾅 (헤롱헤롱)";
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        break;
                    }
                    foreach (var o in All)
                    {
                        if (!UltFoe(o) || hit4.Contains(o)) continue;
                        Vector3 off = o.bodies[(int)BodyId.Chest].position - chest;
                        if (Flat(off).magnitude > s.charge4Reach || Mathf.Abs(off.y) > 1.3f) continue;
                        hit4.Add(o);
                        // Bowled over ahead and to its side.
                        Vector3 side = Vector3.Cross(Vector3.up, planDir);
                        float sign = Vector3.Dot(Flat(off), side) >= 0f ? 1f : -1f;
                        Hit4(o, planDir + side * sign * 0.8f, 2, Cc4.None, 0f, "직진!", hit4.Count);
                    }
                    if (planTime * s.charge4Speed >= s.charge4Dist)
                    {
                        Fx(SkillFxKind.S4Charge, null, FeetPoint, planDir, 3);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.25f)
                    {
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.Charge4, s));
                    }
                    break;
            }
        }

        void UpdateStoneCap(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill(true)) return;
            var aimed = AimedFoe4(s.capRange, 140f);
            if (aimed == null)
            {
                SkipNoTarget4("돌 얹기");
                return;
            }
            SkillUses++;
            aimed.GiveCc(Cc4.Heavy, s.capHeavy, this);
            Fx(SkillFxKind.S4Cap, aimed, aimed.bodies[(int)BodyId.Head].position, Vector3.down, 0, s.capHeavy);
            SkillDetail = $"돌 얹기 → {aimed.DisplayName} 무거움";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.StoneCap, s));
        }

        // ---------------------------------------------------------------- 퀸

        void UpdateCrownToss(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill(true)) return;
            SkillUses++;
            // Lobbed the way it aims: onto the head of the enemy nearest the aim in front (it can still step out from
            // under it), else down where the aim meets the floor - never sailing the whole 12 m over a near one's head.
            Vector3 from = Chest + Vector3.up * 0.5f;
            var aimed = AimedFoe4(s.crownRange, 60f);
            Vector3 to = aimed != null ? aimed.bodies[(int)BodyId.Head].position + Vector3.up * 0.15f : AimFloor4(s.crownRange) + Vector3.up * 0.3f;
            float flight = s.crownFlight * Mathf.Clamp(Vector3.Distance(from, to) / Mathf.Max(1f, s.crownRange), 0.35f, 1f);
            crowns4.Add(new Crown4 { from = from, to = to, flight = flight });
            Fx(SkillFxKind.S4Crown, null, from, FlatDir(to - from), 0, flight, to);
            SkillDetail = aimed != null ? $"왕관 헌납 → {aimed.DisplayName}" : "왕관 헌납";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.CrownToss, s));
        }

        /// <summary>Where a thrown crown is at <paramref name="t"/> (0..1) of its flight: a lob over the line, higher the
        /// farther it goes (0.25 to 1.2 m).</summary>
        public static Vector3 CrownArc(Vector3 from, Vector3 to, float t) =>
            Vector3.Lerp(from, to, t) + Vector3.up * (4f * Mathf.Clamp(0.15f * Vector3.Distance(from, to), 0.25f, 1.2f) * t * (1f - t));

        void UpdateInsolent(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "무엄하다! (숨 들이쉬기)";
                    Fx(SkillFxKind.S4Shout, null, Chest, FlatDir(facing), 0, s.shoutBreath);
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    if (planTime < s.shoutBreath) break;
                    Vector3 me = bodies[0].position, way = FlatDir(facing);
                    int n = 0;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o)) continue;
                        Vector3 off = Flat(o.bodies[0].position - me);
                        if (off.magnitude > s.shoutRange || (off.sqrMagnitude > 0.01f && Vector3.Angle(way, off) > s.shoutCone * 0.5f)) continue;
                        o.GiveCc(Cc4.Tiny, s.shoutTiny, this);
                        n++;
                    }
                    Fx(SkillFxKind.S4Shout, null, Chest, way, 1, s.shoutRange);
                    Say4(this, SayInsolent);
                    SkillDetail = n > 0 ? $"무엄하다! {n}명 쪼꼬미" : "무엄하다! (아무도 없음)";
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.35f)
                    {
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.Insolent, s));
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- keys and cooldowns

        /// <summary>Before the run (PlanKeys): what these skills let the keys do in their phases.</summary>
        void SetFourKeys()
        {
            if (planPhase == PlanPhase.None) return;
            switch (activeSkill)
            {
                case PawnRushSkillIds.Charge4:
                    // Head down, then straight: no turning, no jump, no dive.
                    input.move = planPhase == PlanPhase.Active ? planDir : Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.ForkBonk:
                    if (planPhase != PlanPhase.Active) break;
                    input.jump = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                default:
                    SetFourBKeys();   // bundle 나 (RagdollPawn.SetFourB.cs)
                    SetFourCKeys();   // bundle 다 (RagdollPawn.SetFourC.cs)
                    SetFourJYKeys();  // the user's own (RagdollPawn.SetFourJY.cs)
                    break;
            }
        }

        /// <summary>The cooldown of the skill's grade (PawnRushSkillIds.Grade4: 초급 6 s, 중급 12 s, 상급 30 s).</summary>
        static float SetFourCooldown(int id, PawnRushSkillParams s)
        {
            int grade = PawnRushSkillIds.Grade4(id);
            return grade >= 3 ? s.s4High : grade == 2 ? s.s4Mid : s.s4Basic;
        }

        // ---------------------------------------------------------------- the things left in the world (every step)

        /// <summary>The host's, every step: bricks dropping, pits waiting, holy water breaking into puddles, crowns
        /// flying, pieces popping out of a pit.</summary>
        void UpdateSetFourStates(PawnRushSkillParams s, float dt)
        {
            if (NetworkPuppet || s == null) return;
            UpdateSetFourBStates(s, dt);   // bundle 나's (RagdollPawn.SetFourB.cs)
            UpdateSetFourCStates(s, dt);   // bundle 다's (RagdollPawn.SetFourC.cs)
            UpdateSetFourJYStates(s, dt);  // the user's own (RagdollPawn.SetFourJY*.cs)
            if (bricks4.Count == 0 && pits4.Count == 0 && waters4.Count == 0 && crowns4.Count == 0 && pitOuts4.Count == 0) return;
            for (int i = bricks4.Count - 1; i >= 0; i--)
            {
                var b = bricks4[i];
                if (!b.dropping)
                {
                    b.wait -= dt;
                    if (b.wait <= 0f) b.dropping = true;
                    continue;
                }
                b.fall += dt;
                if (b.fall < s.brickFall) continue;
                // Down: whoever stands under it now gets it on the head.
                RagdollPawn under = null;
                float best = s.brickHit;
                foreach (var o in All)
                {
                    if (!UltFoe(o)) continue;
                    float d = Flat(o.bodies[0].position - b.at).magnitude;
                    if (d <= best) { best = d; under = o; }
                }
                if (under != null)
                {
                    under.GiveCc(Cc4.Dizzy, s.brickDizzy, this);
                    Fx(SkillFxKind.S4Brick, under, under.bodies[(int)BodyId.Head].position, Vector3.down, 1);
                    Fx(SkillFxKind.S3Hit, under, under.bodies[(int)BodyId.Head].position, Vector3.down, 1);
                    Say4(under, SayBump);
                }
                else
                {
                    Vector3 floor = SolidRay(b.at, Vector3.down, 12f, out var hit) ? hit.point : b.at - Vector3.up * s.brickHigh;
                    Fx(SkillFxKind.S4Brick, null, floor, Vector3.down, 2);
                }
                bricks4.RemoveAt(i);
            }
            for (int i = pits4.Count - 1; i >= 0; i--)
            {
                var p = pits4[i];
                p.left -= dt;
                RagdollPawn caught = null;
                if (p.left > 0f)
                    foreach (var o in All)
                    {
                        if (!UltFoe(o) || o.CurrentCc == Cc4.BoundRoot) continue;
                        Vector3 feet = o.FeetPoint;
                        if (Flat(feet - p.at).magnitude > s.pitSize * 0.5f || Mathf.Abs(feet.y - p.at.y) > 0.45f) continue;
                        caught = o;
                        break;
                    }
                if (caught != null)
                {
                    caught.GiveCc(Cc4.BoundRoot, s.pitHold, this, p.at);
                    Fx(SkillFxKind.S4Pit, caught, p.at, Vector3.up, 1, s.pitHold);
                    pitOuts4.Add(new PitOut { who = caught, left = s.pitHold });
                    pits4.RemoveAt(i);
                    continue;
                }
                if (p.left <= 0f)
                {
                    Fx(SkillFxKind.S4Pit, null, p.at, Vector3.up, 2);
                    pits4.RemoveAt(i);
                }
            }
            for (int i = pitOuts4.Count - 1; i >= 0; i--)
            {
                var o = pitOuts4[i];
                o.left -= dt;
                if (o.left > 0f) continue;
                if (o.who != null && o.who.State == PawnState.Active) o.who.AddVelocity(Vector3.up * s.pitPop);   // out it pops
                pitOuts4.RemoveAt(i);
            }
            for (int i = waters4.Count - 1; i >= 0; i--)
            {
                var w = waters4[i];
                if (!w.broken)
                {
                    w.wait -= dt;
                    if (w.wait > 0f) continue;
                    w.broken = true;
                    Fx(SkillFxKind.S4Puddle, null, w.at, Vector3.up, (int)Team + 1, s.water4Size * 0.5f);
                }
                w.left -= dt;
                if (w.left <= 0f)
                {
                    waters4.RemoveAt(i);
                    continue;
                }
                foreach (var o in All)
                {
                    if (!UltFoe(o) || !o.Grounded) continue;
                    if (Flat(o.FeetPoint - w.at).magnitude > s.water4Size * 0.5f || Mathf.Abs(o.FeetPoint.y - w.at.y) > 0.6f) continue;
                    // In the puddle: slippery feet (again whenever the last ran out while it still stands in it).
                    if (o.CurrentCc == Cc4.Slippery) continue;
                    o.GiveCc(Cc4.Slippery, s.water4Slip, this);
                    if (w.wet.Add(o)) Fx(SkillFxKind.S3Slip, o, o.FeetPoint, Flat(o.bodies[0].linearVelocity));
                }
            }
            for (int i = crowns4.Count - 1; i >= 0; i--)
            {
                var c = crowns4[i];
                c.age += dt;
                float t = Mathf.Clamp01(c.age / Mathf.Max(0.05f, c.flight));
                Vector3 at = CrownArc(c.from, c.to, t);
                RagdollPawn struck = null;
                foreach (var o in All)
                {
                    if (!UltFoe(o)) continue;
                    if ((o.bodies[(int)BodyId.Head].position - at).magnitude <= s.crownHit || (o.bodies[(int)BodyId.Chest].position - at).magnitude <= s.crownHit)
                    {
                        struck = o;
                        break;
                    }
                }
                if (struck != null)
                {
                    struck.GiveCc(Cc4.Heavy, s.crownHeavy, this);
                    Fx(SkillFxKind.S4Crown, struck, struck.bodies[(int)BodyId.Head].position, FlatDir(c.to - c.from), 1, s.crownHeavy);
                    crowns4.RemoveAt(i);
                    continue;
                }
                if (t >= 1f)
                {
                    Fx(SkillFxKind.S4Crown, null, at, FlatDir(c.to - c.from), 2);
                    crowns4.RemoveAt(i);
                }
            }
        }

        void ClearSetFourStates()
        {
            ClearSetFourBStates();
            ClearSetFourCStates();
            ClearSetFourJYStates();
            bricks4.Clear();
            pits4.Clear();
            waters4.Clear();
            crowns4.Clear();
            pitOuts4.Clear();
            hit4.Clear();
        }
    }
}
