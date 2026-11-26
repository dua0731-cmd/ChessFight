using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 4판, round 1, bundle 나 (Docs/Skills/PAWN_RUSH_SET4_ROUND1.md 4장 and 5장; 11-26): fourteen skills, each on
    /// one key, the piece walking and running meanwhile unless it says otherwise:
    ///
    ///   폰    독 든 폰 (a sparkly little pawn at the feet, 8 s: the first enemy within 1 m 지침 3 s), 폰 폭풍 (three
    ///         clockwork pawns marching straight on, 4.5 m/s, 15 m or 6 s: each clings to the first enemy it meets, 묶임
    ///         깡충형 3 s; a diving or shoving enemy knocks one over; they fall off edges)
    ///   나이트 엉덩방아 (a hop backward onto the enemy behind within 6 m, else 4 m back, 1.5 m high: 콰당! 1.5 s within
    ///         1.5 m of the seat), 히힝! (round to the back of the aimed enemy in front within 5 m, 0.3 s, "히힝!": it jumps
    ///         1 m, 헤롱헤롱 2 s)
    ///   비숍  먹구름 (a rain cloud over the aimed enemy in sight within 20 m: after 0.5 s 미끌미끌 3 s), 향로 (smoke left
    ///         where it walks for 4 s, 2 m wide, each puff 3 s: 지침 3 s, once a censer), 종 씌우기 (a big bell 6 m over the
    ///         aimed enemy within 18 m drops after 0.8 s: "댕~", 헤롱헤롱 3 s with the bell on), 퇴장! (a crook out along
    ///         the aim, 10 m in 0.3 s: the first enemy on it is tossed over the bishop to 3 m behind, the bishop hops 3 m on)
    ///   룩    성벽! (a stone wall 1.5 m behind, 4 × 2 m, 4 s: allies pass, an enemy running into it falls), 수평 룩 (5 m
    ///         sideways in 0.4 s, the run ahead kept: whoever it meets is banged aside), 잔디깎이 (3 s of a spinning blade,
    ///         30% faster, steered: whoever it meets goes up with a boom)
    ///   퀸    스콜라 메이트 (to the back of the aimed enemy in front within 6 m in 0.4 s, a fan to the back of its head: it
    ///         falls on its face, lightly - unless it faces her, then it ducks), 영구 체크 (stuck to the aimed enemy in front
    ///         within 8 m for 3 s, a fan every 0.6 s, five: "체크!" and a stagger, 헤롱헤롱 3 s; the move keys or 3 m apart end
    ///         it), 퀸 희생!! (0.5 s of a glow, then a bang: enemies within 6 m boom away; she lies scattered 1.5 s, no keys,
    ///         untouchable, and is put together again)
    ///
    /// The host runs it all; every PC draws it from the S4 moments (PawnRushSkillFx.SetFourB.cs). Not seen in Unity by
    /// anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        sealed class Poison4 { public Vector3 at; public float left; }
        sealed class Marcher4 { public Vector3 pos, dir; public float flown, age; public int index; }
        sealed class Cloud4 { public RagdollPawn who; public float wait; }
        sealed class Smoke4 { public Vector3 at; public float left; }
        sealed class Bell4 { public RagdollPawn aimed; public Vector3 at; public float wait, fall; public bool dropping; }
        sealed class Rampart4 { public PawnRushKitWall wall; public Vector3 mid, face, along; public float left; public readonly HashSet<RagdollPawn> bumped = new HashSet<RagdollPawn>(); }

        Poison4 poison4;
        readonly List<Marcher4> marchers4 = new List<Marcher4>();
        readonly HashSet<RagdollPawn> stormCaught4 = new HashSet<RagdollPawn>();
        readonly List<Cloud4> clouds4 = new List<Cloud4>();
        readonly List<Smoke4> smoke4 = new List<Smoke4>();
        readonly HashSet<RagdollPawn> censed4 = new HashSet<RagdollPawn>();
        float censerLeft4, censerDrop4;
        readonly List<Bell4> bells4 = new List<Bell4>();
        readonly List<Rampart4> ramparts4 = new List<Rampart4>();
        RagdollPawn chase4;     // the one 히힝!, 스콜라 메이트 and 영구 체크 go for
        float leap4;            // 엉덩방아's flight
        int checkCount4;
        float checkNext4;

        // ---------------------------------------------------------------- shared

        /// <summary>The speed that throws a body from <paramref name="from"/> to <paramref name="to"/> over an arc whose top is
        /// <paramref name="high"/> over the higher end; <paramref name="time"/> its flight.</summary>
        static Vector3 ArcTo4(Vector3 from, Vector3 to, float high, out float time)
        {
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            float top = Mathf.Max(from.y, to.y) + Mathf.Max(0.2f, high);
            float vy = Mathf.Sqrt(2f * g * (top - from.y));
            time = vy / g + Mathf.Sqrt(2f * Mathf.Max(0.01f, top - to.y) / g);
            Vector3 flat = to - from;
            flat.y = 0f;
            return flat / Mathf.Max(0.1f, time) + Vector3.up * vy;
        }

        /// <summary>The body driven at <paramref name="want"/> (flat), the anchor with it, its facing left alone.</summary>
        void DriveVelocity4(Vector3 want, float dt)
        {
            Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
            AddVelocity(want - own);
            anchorVel = want + carryVel;
            Vector3 hp = bodies[0].position;
            anchorPos = new Vector3(hp.x + anchorVel.x * dt, anchorPos.y, hp.z + anchorVel.z * dt);
            anchor.MovePosition(anchorPos);
        }

        /// <summary>Run at <paramref name="spot"/> to be there in <paramref name="left"/> seconds (20 m/s at most), facing
        /// <paramref name="look"/>.</summary>
        void RunAt4(Vector3 spot, float left, Vector3 look, float dt)
        {
            Vector3 to = Flat(spot - bodies[0].position);
            skillDir = to.sqrMagnitude > 1e-4f ? to.normalized : FlatDir(facing);
            dashBase = Mathf.Min(20f, to.magnitude / Mathf.Max(0.04f, left));
            DriveDash(dt);
            if (look.sqrMagnitude > 1e-4f) facing = FlatDir(look);
        }

        /// <summary>A spot just behind <paramref name="t"/> (its back to us), on our side of it if it faces us.</summary>
        static Vector3 BehindOf4(RagdollPawn t, float gap) => t.bodies[0].position - FlatDir(t.Facing) * gap;

        static float Gravity4 => Mathf.Max(0.01f, -Physics.gravity.y);

        // ---------------------------------------------------------------- 폰

        void UpdatePoisonPawn(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill()) return;
            SkillUses++;
            if (poison4 != null) Fx(SkillFxKind.S4Poison, null, poison4.at, Vector3.up, 2);   // (a new one: the old one goes)
            Vector3 at = FeetPoint;
            if (SolidRay(at + Vector3.up, Vector3.down, 3f, out var floor)) at = floor.point;
            poison4 = new Poison4 { at = at, left = s.poisonSeconds };
            Fx(SkillFxKind.S4Poison, null, at, FlatDir(facing), 0, s.poisonSeconds);
            SkillDetail = "독 든 폰 (발밑에)";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.PoisonPawn, s));
        }

        void UpdatePawnStorm(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill()) return;
            SkillUses++;
            // (One storm at a time: the cooldown is longer than the march.)
            foreach (var old in marchers4) Fx(SkillFxKind.S4Storm, null, old.pos, old.dir, 20 + old.index);
            marchers4.Clear();
            stormCaught4.Clear();
            Vector3 dir = FlatDir(AimRay()), right = Vector3.Cross(Vector3.up, dir);
            Vector3 start = FeetPoint + dir * 0.8f;
            int n = Mathf.Clamp(s.stormCount, 1, 5);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = start + right * ((i - (n - 1) * 0.5f) * s.stormGap);
                if (SolidRay(p + Vector3.up * 0.8f, Vector3.down, 2f, out var g)) p = g.point;
                marchers4.Add(new Marcher4 { pos = p, dir = dir, index = i });
            }
            Fx(SkillFxKind.S4Storm, null, start, dir, 0, s.stormSpeed, new Vector3(s.stormRange, s.stormTime, s.stormGap));
            SkillDetail = $"폰 폭풍 ({n})";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.PawnStorm, s));
        }

        // ---------------------------------------------------------------- 나이트

        void UpdateButtSlam(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    // Onto the nearest enemy behind (within 6 m) - facing ahead all the way. USER_SKILLS 9.1 (the movement
                    // rule): only with one there, its feet no more than jyMarkAbove over ours; none, and it does not go.
                    Vector3 back = -FlatDir(facing), me = bodies[0].position;
                    float feetY = FeetPoint.y;
                    RagdollPawn onto = null;
                    float best = s.slamBack;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o)) continue;
                        Vector3 off = Flat(o.bodies[0].position - me);
                        if (off.magnitude > best || off.sqrMagnitude < 0.01f || Vector3.Dot(off.normalized, back) < 0.3f) continue;
                        if (o.FeetPoint.y > feetY + s.jyMarkAbove || o.bodies[0].position.y < me.y - 2f) continue;
                        best = off.magnitude;
                        onto = o;
                    }
                    if (onto == null)
                    {
                        SkipNoTarget4("엉덩방아");
                        break;
                    }
                    SkillUses++;
                    Vector3 to = onto.FeetPoint;
                    planPoint = to;
                    planDir = FlatDir(facing);
                    leap4 = LeapTo4(to, s.slamHigh);
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = $"엉덩방아 → {onto.DisplayName}";
                    Fx(SkillFxKind.S4Slam, onto, FeetPoint, back, 0, leap4, to);
                    break;
                }
                case PlanPhase.Active:
                    planTime += dt;
                    facing = planDir;   // (seat first)
                    if ((planTime > 0.25f && Grounded) || planTime > leap4 + 0.8f)
                    {
                        // Down on its seat: everyone under it flops.
                        Vector3 seat = FeetPoint;
                        int n = 0;
                        foreach (var o in All)
                        {
                            if (!UltFoe(o)) continue;
                            if (Flat(o.FeetPoint - seat).magnitude > s.slamReach || Mathf.Abs(o.bodies[0].position.y - bodies[0].position.y) > 1.5f) continue;
                            n++;
                            o.GiveCc(Cc4.Flop, s.slamFlop, this);
                            Fx(SkillFxKind.S3Hit, o, o.LassoBelly, FlatDir(o.bodies[0].position - bodies[0].position), n);
                        }
                        Fx(SkillFxKind.S4Slam, null, seat, -planDir, 1, n);
                        SkillDetail = n > 0 ? $"엉덩방아! {n}명 콰당!" : "엉덩방아 (아무도 없음)";
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.3f)
                    {
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.ButtSlam, s));
                    }
                    break;
            }
        }

        void UpdateNeigh(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    var aimed = AimedFoe4(s.neighRange, 120f);
                    if (aimed == null)
                    {
                        SkipNoTarget4("히힝!");
                        break;
                    }
                    SkillUses++;
                    chase4 = aimed;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = $"히힝! → {aimed.DisplayName}";
                    Fx(SkillFxKind.S4Neigh, aimed, FeetPoint, FlatDir(aimed.bodies[0].position - bodies[0].position), 0, s.neighTime);
                    break;
                }
                case PlanPhase.Active:
                {
                    planTime += dt;
                    var t = chase4;
                    if (t == null || t.State != PawnState.Active)
                    {
                        HaltDash();
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        break;
                    }
                    // Round its side to its back: the first half aims wide of it, the second at the spot behind it.
                    Vector3 me = bodies[0].position, them = t.bodies[0].position;
                    Vector3 behind = BehindOf4(t, 0.9f);
                    float k = Mathf.Clamp01(planTime / Mathf.Max(0.05f, s.neighTime));
                    Vector3 side = Vector3.Cross(Vector3.up, FlatDir(them - me));
                    Vector3 spot = k < 0.5f ? them + side * 1.1f : behind;
                    RunAt4(spot, Mathf.Max(0.04f, (k < 0.5f ? 0.5f : 1f) * s.neighTime - planTime), them - me, dt);
                    if (planTime < s.neighTime) break;
                    // 히힝!: it jumps out of its skin.
                    HaltDash();
                    facing = FlatDir(them - bodies[0].position);
                    t.Launch(Flat(t.bodies[0].linearVelocity) * 0.3f + Vector3.up * Mathf.Sqrt(2f * Gravity4 * Mathf.Max(0.1f, s.neighJump)));
                    t.GiveCc(Cc4.Dizzy, s.neighDizzy, this);
                    Say4(this, SayNeigh);
                    Fx(SkillFxKind.S4Neigh, t, t.bodies[(int)BodyId.Head].position, facing, 1);
                    SkillDetail = $"히힝! {t.DisplayName} 깜짝";
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.25f)
                    {
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.Neigh, s));
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- 비숍

        void UpdateRainCloud(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill(true)) return;
            var aimed = AimedFoe4(s.cloudRange, 360f, true);
            if (aimed == null)
            {
                SkipNoTarget4("먹구름");
                return;
            }
            SkillUses++;
            clouds4.Add(new Cloud4 { who = aimed, wait = s.cloudDelay });
            Fx(SkillFxKind.S4Cloud, aimed, aimed.bodies[(int)BodyId.Head].position + Vector3.up * 1.2f, Vector3.down, 0, s.cloudDelay + s.cloudSlip);
            SkillDetail = $"먹구름 → {aimed.DisplayName}";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.RainCloud, s));
        }

        void UpdateCenser(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill()) return;
            SkillUses++;
            censerLeft4 = s.censerSeconds;
            censerDrop4 = 0f;
            censed4.Clear();
            Fx(SkillFxKind.S4Censer, null, FeetPoint, FlatDir(facing), 0, s.censerSeconds);
            SkillDetail = "향로 (지나간 자리에 연기)";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.Censer, s));
        }

        void UpdateBell(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill(true)) return;
            var aimed = AimedFoe4(s.bellRange);
            if (aimed == null)
            {
                SkipNoTarget4("종 씌우기");
                return;
            }
            SkillUses++;
            Vector3 at = aimed.bodies[(int)BodyId.Head].position + Vector3.up * s.bellHigh;
            bells4.Add(new Bell4 { aimed = aimed, at = at, wait = s.bellDelay });
            Fx(SkillFxKind.S4Bell, aimed, at, Vector3.down, 0, s.bellDelay);
            SkillDetail = $"종 씌우기 → {aimed.DisplayName}";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.Bell, s));
        }

        void UpdateCrook(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planDir = FlatDir(AimRay());
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "퇴장! (지팡이)";
                    Fx(SkillFxKind.S4Crook, null, Chest, planDir, 0, s.crookTime, Chest + planDir * s.crookRange);
                    break;
                }
                case PlanPhase.Active:
                {
                    planTime += dt;
                    float reach = s.crookRange * Mathf.Clamp01(planTime / Mathf.Max(0.05f, s.crookTime));
                    Vector3 root = Chest;
                    // A wall stops the staff; the first enemy along it is hooked.
                    if (SolidRay(root, planDir, reach, out var wall)) reach = Mathf.Max(0.3f, wall.distance - 0.1f);
                    RagdollPawn hooked = null;
                    float best = float.MaxValue;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o)) continue;
                        Vector3 rel = o.bodies[(int)BodyId.Chest].position - root;
                        float along = Vector3.Dot(rel, planDir);
                        if (along < 0f || along > reach + 0.3f) continue;
                        if ((rel - planDir * along).magnitude > 0.75f || along >= best) continue;
                        best = along;
                        hooked = o;
                    }
                    if (hooked != null)
                    {
                        // By the scruff, over the bishop's head, down 3 m behind it; the bishop hops 3 m on.
                        Vector3 land = FeetPoint - planDir * s.crookBack;
                        if (SolidRay(land + Vector3.up * 2f, Vector3.down, 6f, out var floor)) land = floor.point;
                        Vector3 toss = ArcTo4(hooked.FeetPoint, land, s.crookHigh + 1f, out float flight);
                        SkillHit(hooked, toss, true, "퇴장!");
                        float hopUp = 0.6f, hopV = Mathf.Sqrt(2f * Gravity4 * hopUp), hopAir = 2f * hopV / Gravity4;
                        Launch(planDir * (s.crookHop / Mathf.Max(0.1f, hopAir)) + Vector3.up * hopV);
                        Fx(SkillFxKind.S4Crook, hooked, hooked.bodies[(int)BodyId.Chest].position, planDir, 1, flight, land);
                        Fx(SkillFxKind.S3Hit, hooked, hooked.LassoBelly, -planDir, 1);
                        SkillDetail = $"퇴장! {hooked.DisplayName}";
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                    }
                    else if (planTime >= s.crookTime)
                    {
                        Fx(SkillFxKind.S4Crook, null, root + planDir * reach, planDir, 2);
                        SkillDetail = "퇴장! (빗나감)";
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
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.Crook, s));
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- 룩

        void UpdateWallUp(PawnRushSkillParams s, bool press, float dt)
        {
            if (planPhase != PlanPhase.None || !press || !CanStartSkill()) return;
            SkillUses++;
            Vector3 back = -FlatDir(facing), along = Vector3.Cross(Vector3.up, back);
            Vector3 mid = FeetPoint + back * s.rampartBack;
            if (SolidRay(mid + Vector3.up * 1.5f, Vector3.down, 4f, out var floor)) mid = floor.point;
            Vector3 a = mid - along * (s.rampartWide * 0.5f), b = mid + along * (s.rampartWide * 0.5f);
            var wall = PawnRushKitWall.Raise(PawnRushKitWall.NextId(), this, a, b, s.rampartTall, s.rampartThick, s.rampartSeconds, 0.25f, true);
            // Our side walks through it.
            foreach (var o in All)
                if (o != null && o != this && IsAllyOf(o)) o.IgnoreBody(wall.Collider, true);
            ramparts4.Add(new Rampart4 { wall = wall, mid = mid, face = back, along = along, left = s.rampartSeconds + 0.4f });
            Fx(SkillFxKind.S4Wall, null, a, back, wall.Id, s.rampartSeconds, b);
            SkillDetail = "성벽! (등 뒤)";
            StartCooldown(SetFourCooldown(PawnRushSkillIds.WallUp, s));
        }

        void UpdateSideSlide(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    hit4.Clear();
                    // The side the move keys hold (to the right if none).
                    Vector3 right = Vector3.Cross(Vector3.up, FlatDir(facing));
                    float held = Vector3.Dot(Flat(skillMoveRaw), right);
                    planDir = held < -0.3f ? -right : right;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = held < -0.3f ? "수평 룩 (왼쪽)" : "수평 룩 (오른쪽)";
                    Fx(SkillFxKind.S4Slide, null, FeetPoint, planDir, 0, s.slideTime);
                    break;
                }
                case PlanPhase.Active:
                {
                    planTime += dt;
                    // USER_SKILLS 9.3: the floor ends to that side - it stops on the brink, teetering.
                    if (EdgeAhead4(planDir, 0.6f))
                    {
                        Teeter4(planDir);
                        SkillDetail = "수평 룩: 낭떠러지 앞 (어, 어, 어!)";
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        break;
                    }
                    // Sideways at the slide's speed; the run's own speed ahead is kept.
                    Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
                    Vector3 keep = own - planDir * Vector3.Dot(own, planDir);
                    DriveVelocity4(keep + planDir * (s.slideDist / Mathf.Max(0.05f, s.slideTime)), dt);
                    Vector3 chest = Chest;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o) || hit4.Contains(o)) continue;
                        Vector3 off = o.bodies[(int)BodyId.Chest].position - chest;
                        if (Flat(off).magnitude > s.slideReach || Mathf.Abs(off.y) > 1.3f) continue;
                        hit4.Add(o);
                        Hit4(o, planDir, 2, Cc4.None, 0f, "수평 룩", hit4.Count);
                    }
                    if (planTime < s.slideTime) break;
                    // (The slide's own speed goes with it; the run ahead stays.)
                    Vector3 left = Flat(bodies[0].linearVelocity) - carryVel;
                    float side = Vector3.Dot(left, planDir);
                    if (side > 0f) AddVelocity(-planDir * side);
                    Fx(SkillFxKind.S4Slide, null, FeetPoint, planDir, 1);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= (teeter4 ? s.teeterTime : 0.15f))
                    {
                        teeter4 = false;
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.SideSlide, s));
                    }
                    break;
            }
        }

        void UpdateMower(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    hit4.Clear();
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = "잔디깎이";
                    Fx(SkillFxKind.S4Mower, null, Chest, FlatDir(facing), 0, s.mowerSeconds);
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    Vector3 chest = Chest;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o) || hit4.Contains(o)) continue;
                        Vector3 off = o.bodies[(int)BodyId.Chest].position - chest;
                        if (Flat(off).magnitude > s.mowerReach || Mathf.Abs(off.y) > 1.3f) continue;
                        hit4.Add(o);
                        // Up it goes (a boom's height, a short way on).
                        float vy = Mathf.Sqrt(2f * Gravity4 * Mathf.Max(0.2f, s.mowerUp)), air = 2f * vy / Gravity4;
                        Vector3 way = Flat(off).sqrMagnitude > 0.01f ? FlatDir(off) : FlatDir(facing);
                        SkillHit(o, way * (s.mowerDist / Mathf.Max(0.1f, air)) + Vector3.up * vy, true, "잔디깎이");
                        Fx(SkillFxKind.S4Mower, o, o.bodies[(int)BodyId.Chest].position, way, 1);
                        Fx(SkillFxKind.S3Hit, o, o.LassoBelly, way, hit4.Count);
                    }
                    if (planTime < s.mowerSeconds) break;
                    Fx(SkillFxKind.S4Mower, null, Chest, FlatDir(facing), 2);
                    SkillDetail = hit4.Count > 0 ? $"잔디깎이: {hit4.Count}명" : "잔디깎이 (아무도 없음)";
                    EndPlanSkill();
                    StartCooldown(SetFourCooldown(PawnRushSkillIds.Mower, s));
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- 퀸

        void UpdateScholarMate(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    var aimed = AimedFoe4(s.scholarRange, 120f);
                    if (aimed == null)
                    {
                        SkipNoTarget4("스콜라 메이트");
                        break;
                    }
                    SkillUses++;
                    chase4 = aimed;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = $"스콜라 메이트 → {aimed.DisplayName}";
                    Fx(SkillFxKind.S4Fan, aimed, FeetPoint, FlatDir(aimed.bodies[0].position - bodies[0].position), 0, s.scholarTime);
                    break;
                }
                case PlanPhase.Active:
                {
                    planTime += dt;
                    var t = chase4;
                    if (t == null || t.State != PawnState.Active)
                    {
                        HaltDash();
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        break;
                    }
                    Vector3 them = t.bodies[0].position;
                    RunAt4(BehindOf4(t, 0.8f), s.scholarTime - planTime, them - bodies[0].position, dt);
                    if (planTime < s.scholarTime) break;
                    HaltDash();
                    Vector3 me = bodies[0].position;
                    facing = FlatDir(them - me);
                    // Facing her (its body within 60° of her): it ducks. Else the fan to the back of its head, on its face.
                    bool ducks = Vector3.Angle(FlatDir(t.Facing), FlatDir(me - them)) <= s.scholarDuck;
                    if (ducks)
                    {
                        Fx(SkillFxKind.S4Fan, t, t.bodies[(int)BodyId.Head].position, facing, 2);
                        SkillDetail = $"스콜라 메이트: {t.DisplayName} 피함";
                    }
                    else
                    {
                        SkillHit(t, Throw4(t.Facing, 1), true, "스콜라 메이트");
                        Fx(SkillFxKind.S4Fan, t, t.bodies[(int)BodyId.Head].position, facing, 1);
                        Fx(SkillFxKind.S3Hit, t, t.bodies[(int)BodyId.Head].position, FlatDir(t.Facing), 1);
                        SkillDetail = $"스콜라 메이트! {t.DisplayName} 꽈당";
                    }
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.3f)
                    {
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.ScholarMate, s));
                    }
                    break;
            }
        }

        void UpdatePerpetualCheck(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                {
                    if (!press || !CanStartSkill()) break;
                    var aimed = AimedFoe4(s.check4Range, 120f);
                    if (aimed == null)
                    {
                        SkipNoTarget4("영구 체크");
                        break;
                    }
                    SkillUses++;
                    chase4 = aimed;
                    checkCount4 = 0;
                    checkNext4 = 0f;
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    SkillDetail = $"영구 체크 → {aimed.DisplayName}";
                    Fx(SkillFxKind.S4Fan, aimed, Chest, FlatDir(aimed.bodies[0].position - bodies[0].position), 4, s.check4Seconds);
                    break;
                }
                case PlanPhase.Active:
                {
                    planTime += dt;
                    var t = chase4;
                    Vector3 me = bodies[0].position;
                    float gap = t != null ? Flat(t.bodies[0].position - me).magnitude : 99f;
                    // The player's own move keys end it (a moment after the press), as do 3 m between them or the time.
                    bool quit = Flat(skillMoveRaw).sqrMagnitude > 0.04f && planTime > 0.2f;
                    if (t == null || t.Floating || quit || planTime >= s.check4Seconds || checkCount4 >= s.check4Hits
                        || (gap > s.check4Break && planTime > 0.6f))
                    {
                        HaltDash();
                        Fx(SkillFxKind.S4Fan, t, Chest, FlatDir(facing), 5);
                        SkillDetail = $"영구 체크: {checkCount4}번";
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        break;
                    }
                    // Stuck to it: a step from it, on our side.
                    Vector3 them = t.bodies[0].position;
                    Vector3 spot = them - FlatDir(them - me) * 0.9f;
                    Vector3 to = Flat(spot - me);
                    skillDir = to.sqrMagnitude > 1e-4f ? to.normalized : FlatDir(facing);
                    dashBase = Mathf.Clamp(to.magnitude * 6f, 0f, 9f);
                    DriveDash(dt);
                    facing = FlatDir(them - me);
                    checkNext4 -= dt;
                    if (checkNext4 > 0f || gap > 1.7f) break;
                    // 체크!: the fan, a stagger.
                    checkNext4 = s.check4Every;
                    checkCount4++;
                    t.AddVelocity(FlatDir(them - me) * 1.6f + Vector3.up * 0.6f);
                    if (checkCount4 == 1) t.GiveCc(Cc4.Dizzy, s.check4Dizzy, this);
                    Say4(t, SayCheck);
                    Fx(SkillFxKind.S4Fan, t, t.bodies[(int)BodyId.Head].position, facing, 3, checkCount4);
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.25f)
                    {
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.PerpetualCheck, s));
                    }
                    break;
            }
        }

        void UpdateQueenSac4(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (!press || !CanStartSkill()) break;
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    SkillDetail = "퀸 희생!! (반짝)";
                    Fx(SkillFxKind.S4Sac, null, Chest, FlatDir(facing), 0, s.sac4Glow);
                    break;
                case PlanPhase.Windup:
                {
                    planTime += dt;
                    if (planTime < s.sac4Glow) break;
                    // 펑: everyone near flies; she comes apart where she stands.
                    Vector3 me = bodies[0].position;
                    int n = 0;
                    foreach (var o in All)
                    {
                        if (!UltFoe(o)) continue;
                        Vector3 off = Flat(o.bodies[0].position - me);
                        if (off.magnitude > s.sac4Range || Mathf.Abs(o.bodies[0].position.y - me.y) > 2.5f) continue;
                        n++;
                        Hit4(o, off.sqrMagnitude > 0.01f ? off : FlatDir(facing), 3, Cc4.None, 0f, "퀸 희생!!", n);
                    }
                    Say4(this, SaySac);
                    Fx(SkillFxKind.S4Sac, null, Chest, FlatDir(facing), 1, s.sac4Range);
                    planPoint = me;
                    HoldStill(s.sac4Scatter);
                    GuardFromSkills(s.sac4Scatter);
                    SkillDetail = n > 0 ? $"퀸 희생!! {n}명" : "퀸 희생!! (아무도 없음)";
                    planPhase = PlanPhase.Active;
                    planTime = 0f;
                    break;
                }
                case PlanPhase.Active:
                    planTime += dt;
                    if (planTime < s.sac4Scatter) break;
                    Fx(SkillFxKind.S4Sac, null, Chest, FlatDir(facing), 2);
                    planPhase = PlanPhase.Recovery;
                    planTime = 0f;
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= 0.2f)
                    {
                        EndPlanSkill();
                        StartCooldown(SetFourCooldown(PawnRushSkillIds.QueenSac4, s));
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- keys

        /// <summary>Before the run (SetFourKeys): what bundle 나 lets the keys do in its phases.</summary>
        void SetFourBKeys()
        {
            switch (activeSkill)
            {
                case PawnRushSkillIds.ButtSlam:
                case PawnRushSkillIds.Neigh:
                case PawnRushSkillIds.ScholarMate:
                case PawnRushSkillIds.PerpetualCheck:
                case PawnRushSkillIds.SideSlide:
                    // Carried by the skill (a hop, a dash round, a slide): the run's keys wait.
                    if (planPhase != PlanPhase.Active) break;
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    break;
                case PawnRushSkillIds.QueenSac4:
                    // In pieces: nothing (ROUND1: "조작 없음").
                    if (planPhase != PlanPhase.Active && planPhase != PlanPhase.Recovery) break;
                    input.move = Vector3.zero;
                    input.jump = false;
                    input.sprint = false;
                    input.shove = false;
                    input.shoveHeld = false;
                    input.grab = false;
                    break;
            }
        }

        // ---------------------------------------------------------------- the things left in the world (every step)

        /// <summary>The host's, every step (UpdateSetFourStates): the poisoned pawn, the clockwork pawns, the rain clouds,
        /// the censer's smoke, the bells, the ramparts.</summary>
        void UpdateSetFourBStates(PawnRushSkillParams s, float dt)
        {
            if (poison4 != null) StepPoison4(s, dt);
            if (marchers4.Count > 0) StepStorm4(s, dt);
            for (int i = clouds4.Count - 1; i >= 0; i--)
            {
                var c = clouds4[i];
                c.wait -= dt;
                if (c.wait > 0f) continue;
                if (c.who != null && UltFoe(c.who)) c.who.GiveCc(Cc4.Slippery, s.cloudSlip, this);
                clouds4.RemoveAt(i);
            }
            if (censerLeft4 > 0f || smoke4.Count > 0) StepCenser4(s, dt);
            if (bells4.Count > 0) StepBells4(s, dt);
            if (ramparts4.Count > 0) StepRamparts4(s, dt);
        }

        void StepPoison4(PawnRushSkillParams s, float dt)
        {
            poison4.left -= dt;
            RagdollPawn taken = null;
            if (poison4.left > 0f)
                foreach (var o in All)
                {
                    if (!UltFoe(o)) continue;
                    Vector3 f = o.FeetPoint;
                    if (Flat(f - poison4.at).magnitude > s.poisonReach || Mathf.Abs(f.y - poison4.at.y) > 1f) continue;
                    taken = o;
                    break;
                }
            if (taken != null)
            {
                taken.GiveCc(Cc4.Tired, s.poisonTired, this);
                Fx(SkillFxKind.S4Poison, taken, poison4.at, Vector3.up, 1, s.poisonTired);
                poison4 = null;
            }
            else if (poison4.left <= 0f)
            {
                Fx(SkillFxKind.S4Poison, null, poison4.at, Vector3.up, 2);
                poison4 = null;
            }
        }

        void StepStorm4(PawnRushSkillParams s, float dt)
        {
            for (int i = marchers4.Count - 1; i >= 0; i--)
            {
                var m = marchers4[i];
                m.age += dt;
                float step = s.stormSpeed * dt;
                int end = 0;   // 20: stopped or off an edge, 10: caught one, 30: knocked over
                RagdollPawn who = null;
                Vector3 next = m.pos + m.dir * step;
                if (SolidRay(m.pos + Vector3.up * 0.25f, m.dir, step + 0.15f, out _)) end = 20;                           // a wall
                else if (!SolidRay(next + Vector3.up * 0.6f, Vector3.down, 1.4f, out var g)) { m.pos = next; end = 20; }  // the edge
                else
                {
                    m.pos = g.point;
                    m.flown += step;
                    if (m.flown >= s.stormRange || m.age >= s.stormTime) end = 20;
                }
                if (end == 0)
                    foreach (var o in All)
                    {
                        if (!UltFoe(o) || stormCaught4.Contains(o)) continue;
                        Vector3 f = o.FeetPoint;
                        if (Flat(f - m.pos).magnitude > s.stormReach || Mathf.Abs(f.y - m.pos.y) > 1f) continue;
                        who = o;
                        // A dive or a shove into it knocks it over; anyone else it grabs by the legs.
                        end = o.Diving || o.Shoving ? 30 : 10;
                        break;
                    }
                if (end == 0) continue;
                if (end == 10)
                {
                    stormCaught4.Add(who);
                    who.GiveCc(Cc4.BoundHop, s.stormBound, this);
                    Fx(SkillFxKind.S4Storm, who, m.pos, m.dir, 10 + m.index, s.stormBound);
                }
                else Fx(SkillFxKind.S4Storm, who, m.pos, m.dir, end + m.index);
                marchers4.RemoveAt(i);
            }
        }

        void StepCenser4(PawnRushSkillParams s, float dt)
        {
            if (censerLeft4 > 0f)
            {
                censerLeft4 -= dt;
                censerDrop4 -= dt;
                if (censerDrop4 <= 0f)
                {
                    censerDrop4 = 0.15f;
                    smoke4.Add(new Smoke4 { at = FeetPoint, left = s.censerLinger });
                }
            }
            float r = s.censerWidth * 0.5f;
            for (int i = smoke4.Count - 1; i >= 0; i--)
            {
                smoke4[i].left -= dt;
                if (smoke4[i].left <= 0f) smoke4.RemoveAt(i);
            }
            if (smoke4.Count == 0) return;
            foreach (var o in All)
            {
                if (!UltFoe(o) || censed4.Contains(o)) continue;
                Vector3 f = o.FeetPoint;
                foreach (var p in smoke4)
                {
                    if (Flat(f - p.at).magnitude > r || Mathf.Abs(f.y - p.at.y) > 1.5f) continue;
                    censed4.Add(o);
                    o.GiveCc(Cc4.Tired, s.censerTired, this);
                    Fx(SkillFxKind.S4Censer, o, o.bodies[(int)BodyId.Head].position, Vector3.up, 1);
                    break;
                }
            }
        }

        void StepBells4(PawnRushSkillParams s, float dt)
        {
            for (int i = bells4.Count - 1; i >= 0; i--)
            {
                var b = bells4[i];
                if (!b.dropping)
                {
                    b.wait -= dt;
                    if (b.wait <= 0f) b.dropping = true;
                    continue;
                }
                b.fall += dt;
                if (b.fall < s.bellFall) continue;
                // Down: whoever stands under its rim gets it over the head.
                RagdollPawn under = null;
                float best = s.bellSize * 0.5f + 0.3f;
                foreach (var o in All)
                {
                    if (!UltFoe(o)) continue;
                    float d = Flat(o.bodies[0].position - b.at).magnitude;
                    if (d <= best)
                    {
                        best = d;
                        under = o;
                    }
                }
                if (under != null)
                {
                    under.GiveCc(Cc4.Dizzy, s.bellDizzy, this);
                    Say4(under, SayBell);
                    Fx(SkillFxKind.S4Bell, under, under.bodies[(int)BodyId.Head].position, Vector3.down, 1, s.bellDizzy);
                    Fx(SkillFxKind.S3Hit, under, under.bodies[(int)BodyId.Head].position, Vector3.down, 1);
                }
                else
                {
                    Vector3 floor = SolidRay(b.at, Vector3.down, 14f, out var hit) ? hit.point : b.at - Vector3.up * (s.bellHigh + 1f);
                    Fx(SkillFxKind.S4Bell, null, floor, Vector3.down, 2);
                }
                bells4.RemoveAt(i);
            }
        }

        void StepRamparts4(PawnRushSkillParams s, float dt)
        {
            for (int i = ramparts4.Count - 1; i >= 0; i--)
            {
                var r = ramparts4[i];
                r.left -= dt;
                if (r.wall == null || !r.wall.Standing || r.left <= 0f)
                {
                    ramparts4.RemoveAt(i);
                    continue;
                }
                // An enemy running into its face falls over (thrown back off it, comic).
                float half = s.rampartWide * 0.5f + 0.2f;
                foreach (var o in All)
                {
                    if (!UltFoe(o) || r.bumped.Contains(o) || o.State != PawnState.Active) continue;
                    Vector3 d = o.bodies[0].position - r.mid;
                    float off = Vector3.Dot(Flat(d), r.face), across = Vector3.Dot(Flat(d), r.along);
                    if (Mathf.Abs(across) > half || Mathf.Abs(off) > s.rampartThick * 0.5f + 0.55f || d.y > s.rampartTall + 0.3f) continue;
                    Vector3 v = Flat(o.bodies[0].linearVelocity);
                    float into = -Vector3.Dot(v, off >= 0f ? r.face : -r.face);
                    if (into < s.rampartBump) continue;
                    r.bumped.Add(o);
                    Vector3 away = off >= 0f ? r.face : -r.face;
                    SkillHit(o, away * 2.5f + Vector3.up * 1.5f, true, "성벽!");
                    Fx(SkillFxKind.S3Hit, o, o.bodies[(int)BodyId.Head].position, away, 1);
                }
            }
        }

        void ClearSetFourBStates()
        {
            poison4 = null;
            marchers4.Clear();
            stormCaught4.Clear();
            clouds4.Clear();
            smoke4.Clear();
            censed4.Clear();
            censerLeft4 = 0f;
            bells4.Clear();
            ramparts4.Clear();
            chase4 = null;
        }
    }
}
