using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Queen of the Hill's six piece skills, brought over to the Pawn Rush slots (JY-skill-lab, 2026-11-19; the user picked
    /// "퀸 오브 더 힐부터" of the skills 승규 님 made earlier, to be put where the developers' skill tree can slot them).
    /// Their rules and numbers are the ones of the code that left main with the mode at R497
    /// (RagdollLab/QueenHillSkills/RagdollPawn.QueenHillSkills.cs, R89..R156; still on the remote branches), rewritten on
    /// the slots' own machinery (RagdollPawn.SkillPlans.cs: PlanPhase, the plan's point, way and target, sent to every PC):
    ///
    ///   근접 호위 (the king's)    teammates close by shrug off knockdowns (a short stagger instead) and most of a push for a
    ///                            while; the one who gave it is not guarded himself, but a guarded teammate standing near
    ///                            takes a hit meant for him.
    ///   팔방 검격 (the queen's)   an aimed slash four squares long that runs along the floor - down drops, up steps of a tier,
    ///                            stopped by a wall or by a guarded piece - and knocks down whoever stands on its way.
    ///   캐슬링 교대 (the rook's)  swap places with a teammate close by once it agrees: plan 1-나's castling with this skill's
    ///                            own numbers (RagdollPawn.SkillPlans.cs, CastleOf).
    ///   교차 공중 포격 (bishop's) it rises and hovers, drifting slowly on the move keys; each left click throws a shot at the
    ///                            aimed spot, two a hover; where one lands an X of diagonals knocks enemies down and away.
    ///   도약 압착 (the knight's)  a leap to an aimed spot at most a tier up; enemies in the landing circle are squashed flat
    ///                            and slide out of it (off a ledge, they fall); an enemy close to the aim is caught by it and
    ///                            the knight comes down on its head.
    ///   비집고 돌파 (the pawn's)  low and quick a short way ahead; pieces it squeezes past are nudged aside; a wall stops it.
    ///
    /// Prototypes like the rest of the new skills: the rules and the physics, lines and rings for marks (PawnRushPlanMarks),
    /// none of the mode's effects, sounds or poses (the sword, the staff, the carpet). Not seen in Unity by anyone yet.
    /// </summary>
    public partial class RagdollPawn
    {
        // ---------------------------------------------------------------- what they share

        // A new skill's own small number, sent with its state: its aim can go there (bit 0); the volley's shots left
        // (bits 1 and 2).
        byte planNote;

        public bool PlanAimValid => (planNote & 1) != 0;
        public int VolleyShotsLeft => (planNote >> 1) & 3;

        /// <summary>A hit of one of these skills: a piece they knock down stays down long enough to read (hillDownHold).</summary>
        string HillHit(RagdollPawn target, Vector3 push, bool knockdown, string cause)
        {
            string result = SkillHit(target, push, knockdown, cause);
            var s = PawnRushSkills;
            if (result == "넘어짐" && s != null && s.hillDownHold > 0f) target.Knockdown(cause, s.hillDownHold);
            return result;
        }

        /// <summary>Before the run (PlanKeys): what each of these skills lets the keys do in its phase. (The clicks of an aim
        /// are read after this, so an aiming phase leaves them alone.)</summary>
        void HillKeys()
        {
            if (planPhase == PlanPhase.None) return;
            bool aim = planPhase == PlanPhase.Aim, rooted;
            switch (activeSkill)
            {
                case PawnRushSkillIds.Ward: rooted = true; break;
                case PawnRushSkillIds.Slash:
                case PawnRushSkillIds.PressLeap: rooted = !aim; break;             // aiming they walk
                case PawnRushSkillIds.AirVolley: rooted = true; break;            // the move keys slide the hover instead
                case PawnRushSkillIds.Squeeze: rooted = planPhase != PlanPhase.Active; break;   // (the dash roots itself)
                case PawnRushSkillIds.CastleSwap: rooted = false; break;
                default: return;
            }
            if (aim) input.sprint = false;
            if (!rooted) return;
            input.move = Vector3.zero;
            input.jump = false;
            input.sprint = false;
            if (aim) return;
            input.shove = false;
            input.shoveHeld = false;
        }

        /// <summary>Every step (UpdateSkills): the states these skills leave on a piece - a guard running out, a squashed
        /// piece sliding and falling, the piece a knight came down on.</summary>
        void UpdateHillStates(float dt)
        {
            if (wardLeft > 0f)
            {
                wardLeft -= dt;
                if (wardLeft <= 0f) wardBy = null;
            }
            UpdateFlattenFall(dt);
            if (stompedOne != null)
            {
                // The piece it came down on: the two collide again once they are apart, or after a while whatever happens.
                stompedLeft -= dt;
                if (stompedLeft <= 0f || Flat(stompedOne.bodies[0].position - bodies[0].position).magnitude > 0.62f)
                {
                    IgnorePiece(stompedOne, false);
                    stompedOne = null;
                }
            }
        }

        /// <summary>Everything these skills leave running when the skills are cleared.</summary>
        void ClearHillSkills()
        {
            planNote = 0;
            shellOut = false;
            hoverLeft = hoverLinger = 0f;
            slashBlocked = slashGuarded = false;
            pressLanded = false;
        }

        void IgnorePiece(RagdollPawn other, bool ignore)
        {
            if (other == null || ownColliders == null || other.ownColliders == null) return;
            foreach (var a in ownColliders)
            {
                if (a == null) continue;
                foreach (var b in other.ownColliders)
                    if (b != null) Physics.IgnoreCollision(a, b, ignore);
            }
        }

        /// <summary>Where a floor aim's ray starts: 1.6 m over the head and 1.2 m back along the aim (about the third-person
        /// camera).</summary>
        Vector3 FloorAimEye(Vector3 aim)
        {
            Vector3 flat = new Vector3(aim.x, 0f, aim.z);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Flat(facing).normalized;
            return bodies[(int)BodyId.Head].position + Vector3.up * 1.6f - flat * 1.2f;
        }

        /// <summary>The floor the camera's aim points at within <paramref name="range"/> across the ground, at most
        /// <paramref name="maxRise"/> above this piece's floor. Looking at a wall's face, the top just behind it; past the
        /// range, the floor under the range's end.</summary>
        void AimFloor(float range, float maxRise, out Vector3 point, out bool valid, out string why)
        {
            Vector3 head = bodies[(int)BodyId.Head].position;
            Vector3 aim = input.aim.sqrMagnitude > 1e-4f ? input.aim.normalized : facing;
            Vector3 flat = AimFlat();
            Vector3 me = FeetPoint;
            point = me + flat * Mathf.Min(3f, range);
            valid = false;
            why = "바닥이 없음";
            bool found = false;
            if (SolidRay(FloorAimEye(aim), aim, range + 10f, out var hit))
            {
                if (hit.normal.y >= 0.6f) { point = hit.point; found = true; }
                else if (SolidRay(hit.point + flat * 0.45f + Vector3.up * (maxRise < 50f ? maxRise + 1.6f : 4f), Vector3.down, 6f, out var top) && top.normal.y >= 0.6f)
                {
                    point = top.point;   // a ledge: the top just behind the face it looks at
                    found = true;
                }
            }
            if (found && Flat(point - me).magnitude > range) found = false;
            if (!found)
            {
                Vector3 far = new Vector3(me.x, Mathf.Max(me.y, head.y), me.z) + flat * range;
                if (SolidRay(far + Vector3.up * (maxRise < 50f ? maxRise + 1.2f : 3f), Vector3.down, 12f, out var floor) && floor.normal.y >= 0.6f)
                {
                    point = floor.point;
                    found = true;
                }
            }
            if (!found) return;
            if (Flat(point - me).magnitude < 0.8f) { why = "너무 가까움"; return; }
            if (point.y - me.y > maxRise) { why = $"너무 높음 ({point.y - me.y:0.0} m)"; return; }
            valid = true;
            why = "";
        }

        /// <summary>Nothing solid where a piece would stand on <paramref name="floor"/> (other pieces do not count).</summary>
        bool RoomToStandAt(Vector3 floor)
        {
            int n = Physics.OverlapSphereNonAlloc(floor + Vector3.up * 0.7f, 0.28f, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = skillOverlap[i];
                if (c == null || c.isTrigger || ColliderOwner.ContainsKey(c)) continue;
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- 근접 호위 (the king's close guard)

        // The guard on this piece (the host's; a client's puppet is told it is guarded).
        float wardLeft;
        RagdollPawn wardBy;

        public bool Warded => wardLeft > 0f;
        public float WardLeft => Mathf.Max(0f, wardLeft);
        public RagdollPawn WardBy => wardLeft > 0f ? wardBy : null;

        /// <summary>Guarded: pushes (TakeHit) are this much smaller.</summary>
        float WardPushScale => wardLeft > 0f ? (PawnRushSkills != null ? PawnRushSkills.wardPush : 0.25f) : 1f;

        /// <summary>
        /// The guard that takes a hit for the piece that gave the guard (승규 님 10-12: "아군이 그 공격을 대신 막아주는 … 나 자체는
        /// 무적이 아닌데 아군이 나를 막아줘서"): a teammate its guard is still on, standing within the guard's reach of it - the
        /// nearest. None (null) when no guard is near: then it is hit like anyone.
        /// </summary>
        RagdollPawn WardCover()
        {
            var s = PawnRushSkills;
            if (s == null || State == PawnState.Ragdoll) return null;
            RagdollPawn best = null;
            float bestD = float.MaxValue;
            foreach (var other in All)
            {
                if (other == null || other == this || other.wardBy != this || other.wardLeft <= 0f || other.NetworkPuppet) continue;
                if (other.State == PawnState.Ragdoll || other.Floating || !IsAllyOf(other)) continue;
                Vector3 d = other.bodies[0].position - bodies[0].position;
                float flat = Flat(d).magnitude;
                if (flat > s.wardRadius || Mathf.Abs(d.y) > s.wardHeight || flat >= bestD) continue;
                best = other;
                bestD = flat;
            }
            return best;
        }

        /// <summary>Guarded: a knockdown from anything (a tackle, a bump, an obstacle) becomes a short stagger; the piece
        /// that gave the guard is covered by a guarded teammate near it. Called from Knockdown.</summary>
        bool WardCatchesKnockdown(string cause, bool covering = false)
        {
            if (NetworkPuppet || PawnRushSkills == null) return false;
            // (The one covering does not look for a cover of its own: two pieces that guard each other would go round.)
            var cover = covering ? null : WardCover();
            if (cover != null && State != PawnState.Ragdoll && !Floating)
            {
                cover.WardCatchesKnockdown(cause, true);
                LastSkillHit = $"{cause}: {cover.DisplayName}이(가) 대신 막음";
                return true;
            }
            if (wardLeft <= 0f || State == PawnState.Ragdoll || Floating) return false;
            Stagger(PawnRushSkills.wardStagger);
            LastSkillHit = $"{cause}: 호위로 버팀";
            return true;
        }

        void UpdateWard(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        SkillUses++;
                        planPhase = PlanPhase.Windup;
                        planTime = 0f;
                        planPoint = FeetPoint;
                        SkillDetail = "호위 준비";
                    }
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    planPoint = FeetPoint;
                    if (planTime >= s.wardWindup)
                    {
                        GiveWard(s);
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "후딜";
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.wardRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.wardCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        void GiveWard(PawnRushSkillParams s)
        {
            Vector3 c = bodies[0].position;
            int n = 0;
            string names = "";
            foreach (var other in All)
            {
                // Its own body is left out (승규 님's pick B): the one who gives the guard can still be beaten.
                if (other == null || other == this || other.NetworkPuppet || !IsAllyOf(other) || other.State == PawnState.Ragdoll) continue;
                Vector3 d = other.bodies[0].position - c;
                if (Flat(d).magnitude > s.wardRadius || Mathf.Abs(d.y) > s.wardHeight) continue;
                other.wardLeft = s.wardTime;
                other.wardBy = this;
                n++;
                names += (names.Length > 0 ? ", " : "") + other.DisplayName;
            }
            Log($"근접 호위 — 팀원 {n}명 {s.wardTime:0.#}초 ({(n > 0 ? names : "범위 안에 없음")}) · 자신은 빠짐");
        }

        // ---------------------------------------------------------------- 팔방 검격 (the queen's long slash)

        // The slash runs along the floor, sampled every SlashStep metres from her feet.
        const float SlashStep = 0.25f;
        readonly List<Vector3> slashPath = new List<Vector3>();
        float slashReach;
        bool slashBlocked, slashGuarded;

        /// <summary>How far the slash reaches along its way (to a wall or a guarded piece that stops it).</summary>
        public float SlashReach => slashReach;

        /// <summary>The floor under <paramref name="p"/> as the slash finds its own: the first one below a step's height
        /// over <paramref name="near"/>, or <paramref name="near"/> if there is none.</summary>
        float SlashFloorAt(Vector3 p, float near, float climb)
        {
            float probe = near + climb + 0.1f;
            return SolidRay(new Vector3(p.x, probe, p.z), Vector3.down, climb + 0.1f + 40f, out var floor) && floor.normal.y >= 0.5f
                ? floor.point.y : near;
        }

        float SlashPathFloor(float along)
        {
            if (slashPath.Count == 0) return FeetPoint.y;
            float at = Mathf.Clamp(along / SlashStep, 0f, slashPath.Count - 1);
            int a = Mathf.FloorToInt(at), b = Mathf.Min(a + 1, slashPath.Count - 1);
            return Mathf.Lerp(slashPath[a].y, slashPath[b].y, at - a);
        }

        /// <summary>The slash's way along the aim (승규 님 R97: "위에 있고 적이 아래에 있으면 아래 적이 맞아야"): along the floor -
        /// down any drop, up steps of at most slashClimb - and a face taller than that stops it. The plan's point is its end.</summary>
        void SlashLine(PawnRushSkillParams s)
        {
            Vector3 feet = FeetPoint;
            slashPath.Clear();
            slashPath.Add(feet);
            slashReach = s.slashLength;
            slashBlocked = false;
            float level = feet.y, climb = Mathf.Max(0.1f, s.slashClimb);
            int n = Mathf.CeilToInt(s.slashLength / SlashStep);
            for (int i = 1; i <= n; i++)
            {
                float was = (i - 1) * SlashStep, d = Mathf.Min(i * SlashStep, s.slashLength);
                float probe = level + climb + 0.1f;
                Vector3 prev = feet + planDir * was, here = feet + planDir * d;
                if (SolidRay(new Vector3(prev.x, probe, prev.z), planDir, d - was, out var wall) && Mathf.Abs(wall.normal.y) < 0.6f)
                {
                    slashReach = Mathf.Max(0.3f, was + wall.distance - 0.1f);
                    slashBlocked = true;
                    if (wall.distance > 0.15f) slashPath.Add(new Vector3(prev.x, level, prev.z) + planDir * (wall.distance - 0.1f));
                    break;
                }
                level = SlashFloorAt(here, level, climb);
                slashPath.Add(new Vector3(here.x, level, here.z));
            }
            planPoint = slashPath[slashPath.Count - 1];
        }

        /// <summary>A piece on the slash's way up to <paramref name="front"/> m, and how far along it stands.</summary>
        bool OnSlash(RagdollPawn other, PawnRushSkillParams s, float front, out float along)
        {
            Vector3 start = slashPath.Count > 0 ? slashPath[0] : FeetPoint;
            Vector3 rel = other.bodies[0].position - start;
            along = Vector3.Dot(Flat(rel), planDir);
            if (along < -0.3f || along > front + 0.25f || along > slashReach + 0.2f) return false;
            float across = (Flat(rel) - planDir * along).magnitude;
            if (across > s.slashWidth * 0.5f + 0.3f) return false;
            // Standing on the floor the slash runs along there (a tier below or above her included), jumping over it, or on
            // the floor right under the piece found the same way (a slope tilted across the line, a step's edge).
            float climb = Mathf.Max(0.1f, s.slashClimb);
            float at = Mathf.Max(0f, along);
            float low = Mathf.Min(SlashPathFloor(at - SlashStep), SlashPathFloor(at + SlashStep)), high = Mathf.Max(SlashPathFloor(at - SlashStep), SlashPathFloor(at + SlashStep));
            float under = SlashFloorAt(other.bodies[0].position, SlashPathFloor(at), climb);
            low = Mathf.Min(low, under);
            high = Mathf.Max(high, under);
            float feet = other.FeetPoint.y;
            return feet >= low - s.slashDrop && feet <= high + s.slashHeight;
        }

        /// <summary>The slash stops at the first enemy on its way that is guarded (승규 님 10-12: "임팩트가 폰에서 끝나야 돼"): the
        /// guard takes it and staggers, nothing behind it is cut.</summary>
        void SlashStopAtGuard(PawnRushSkillParams s)
        {
            slashGuarded = false;
            float nearest = float.MaxValue;
            foreach (var other in All)
            {
                if (other == null || other == this || !IsEnemyOf(other) || other.wardLeft <= 0f || other.State == PawnState.Ragdoll) continue;
                if (!OnSlash(other, s, slashReach, out float along) || along >= nearest) continue;
                nearest = along;
            }
            if (nearest == float.MaxValue) return;
            slashReach = Mathf.Max(0.3f, nearest);
            slashGuarded = true;
            Log($"팔방 검격: 검기가 {slashReach:0.0} m의 호위 받는 적에게 막힘");
        }

        void SlashHits(PawnRushSkillParams s, float front)
        {
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other) || skillHitSet.Contains(other)) continue;
                if (!OnSlash(other, s, front, out float along)) continue;
                if (slashGuarded && along > slashReach + 0.05f) continue;
                skillHitSet.Add(other);
                string result = HillHit(other, planDir * s.slashPush + Vector3.up * s.slashLift, true, "팔방 검격");
                Fx(SkillFxKind.QueenHit, other, SkillContact(other), planDir, 1);
                Log($"팔방 검격 → {other.DisplayName}: {along:0.0} m → {result}");
            }
        }

        void UpdateSlash(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        planPhase = PlanPhase.Aim;
                        planTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        planDir = AimFlat();
                        SlashLine(s);
                        SkillDetail = "검격 조준 (좌클릭 베기 · 우클릭/스킬 키 취소)";
                    }
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    planDir = AimFlat();
                    if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = planDir;
                    SlashLine(s);
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("팔방 검격: 조준 취소");
                        break;
                    }
                    if (!confirm) break;
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    facing = planDir;
                    SkillDetail = "검격 준비";
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    facing = planDir;
                    if (planTime >= s.slashWindup)
                    {
                        SlashLine(s);
                        SlashStopAtGuard(s);
                        skillHitSet.Clear();
                        planPhase = PlanPhase.Active;
                        planTime = 0f;
                        SkillDetail = "검격";
                    }
                    break;
                case PlanPhase.Active:
                {
                    planTime += dt;
                    float k = Mathf.Clamp01(planTime / Mathf.Max(0.01f, s.slashTravel));
                    SlashHits(s, slashReach * k);
                    if (k >= 1f)
                    {
                        if (slashBlocked) Log($"팔방 검격: 검기가 {slashReach:0.0} m에서 벽에 막힘");
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "회복 (빈틈)";
                    }
                    break;
                }
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.slashRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.slashCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        // ---------------------------------------------------------------- 교차 공중 포격 (the bishop's crossed air barrage)

        float hoverHipY, hoverFromY, hoverLastY, hoverLeft, hoverLinger, hoverTime;
        Vector3 hoverVel, hoverAt, shellFrom;
        bool shellOut, volleyFired;
        int volleyShots;

        /// <summary>A shot is on its way (the host's): where it left from. Its end is the plan's point.</summary>
        public bool ShellOut => shellOut;
        public Vector3 ShellFrom => shellFrom;

        void SetVolleyNote(bool valid) => planNote = (byte)((valid ? 1 : 0) | (Mathf.Clamp(volleyShots, 0, 3) << 1));

        void VolleyAim(PawnRushSkillParams s)
        {
            AimFloor(s.volleyRange, 99f, out Vector3 point, out bool valid, out _);
            planPoint = point;
            Vector3 way = Flat(point - bodies[0].position);
            if (way.sqrMagnitude > 0.04f) planDir = way.normalized;
            SetVolleyNote(valid);
        }

        /// <summary>Held up at the hover height (no higher), bobbing a little; the move keys slide it very slowly, like a
        /// carpet (승규 님 R105): it eases up to volleyDrift and eases to a stop.</summary>
        void Hover(PawnRushSkillParams s, float dt)
        {
            bool up = hoverTime > s.volleyRise;
            float bob = up ? Mathf.Sin((hoverTime - s.volleyRise) * Mathf.PI * 1.2f) * 0.035f : 0f;
            Vector3 drift = up ? Vector3.ClampMagnitude(Flat(skillMoveRaw), 1f) * s.volleyDrift : Vector3.zero;
            hoverVel = up ? Vector3.Lerp(hoverVel, drift, Mathf.Clamp01(dt * 3f / Mathf.Max(0.05f, s.volleyDriftEase))) : Vector3.zero;
            // The point the hips are carried to (the flight other skills use, RagdollPawn.OuterFlight.cs: it takes the
            // standing anchor along and holds the body up): it drifts across the floor and rises to the hover height. It
            // never gets far ahead of the body, so a wall in the way stops the drift instead of being pushed into.
            Vector3 hips = bodies[0].position;
            hoverAt += hoverVel * dt;
            Vector3 lead = Flat(hoverAt - hips);
            if (lead.magnitude > 0.3f) hoverAt = new Vector3(hips.x, 0f, hips.z) + lead.normalized * 0.3f;
            float rise = Mathf.Clamp01(hoverTime / Mathf.Max(0.05f, s.volleyRise));
            float y = Mathf.Lerp(hoverFromY, hoverHipY, rise * rise * (3f - 2f * rise)) + bob;
            Vector3 point = new Vector3(hoverAt.x, y, hoverAt.z);
            Vector3 speed = hoverVel + Vector3.up * ((y - hoverLastY) / Mathf.Max(1e-4f, dt));
            hoverLastY = y;
            Vector3 aim = Flat(planPoint - hips);
            facing = aim.sqrMagnitude > 0.04f ? aim.normalized : AimFlat();
            Fly(point, speed, 0f, default, 10f);
        }

        void DropHover()
        {
            shellOut = false;
            EndFlight();
            KeepSkillLandingSafe();   // (coming down from the hover is not a fall)
            planPhase = PlanPhase.Recovery;
            planTime = 0f;
            SkillDetail = "내려옴";
        }

        void FireShell(PawnRushSkillParams s)
        {
            volleyShots--;
            volleyFired = true;
            shellOut = true;
            planPhase = PlanPhase.Active;
            planTime = 0f;
            shellFrom = bodies[(int)BodyId.HandR].position + Vector3.up * 0.1f;
            Vector3 way = Flat(planPoint - bodies[0].position);
            if (way.sqrMagnitude > 0.04f) planDir = way.normalized;
            SetVolleyNote(true);
            SkillDetail = "견제탄 날아가는 중";
        }

        /// <summary>The shot lands: an X of diagonals (±45° to the throw) and the spot itself. Enemies on it are pushed out
        /// from the middle and knocked down: off a wall or a ledge they fall.</summary>
        void VolleyImpact(PawnRushSkillParams s)
        {
            Vector3 c = planPoint;
            Vector3 d1 = Quaternion.Euler(0f, 45f, 0f) * planDir, d2 = Quaternion.Euler(0f, -45f, 0f) * planDir;
            int down = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other)) continue;
                Vector3 hip = other.bodies[0].position;
                Vector3 rel = Flat(hip - c);
                float dy = hip.y - other.standHeight - c.y;
                if (dy < -1.2f || dy > 2.2f) continue;
                Vector3 away = Vector3.zero;
                bool hit = rel.magnitude <= s.volleyCenter;
                if (hit) away = rel.sqrMagnitude > 0.01f ? rel.normalized : planDir;
                for (int i = 0; i < 2 && !hit; i++)
                {
                    Vector3 d = i == 0 ? d1 : d2;
                    float along = Vector3.Dot(rel, d);
                    Vector3 off = rel - d * along;
                    if (Mathf.Abs(along) > s.volleyArm || off.magnitude > s.volleyArmWidth * 0.5f + 0.2f) continue;
                    hit = true;
                    away = (d * Mathf.Sign(along) * 0.7f + (off.sqrMagnitude > 1e-6f ? off.normalized : Vector3.zero) * 0.3f).normalized;
                }
                if (!hit) continue;
                bool climbing = other.Climbing;
                string result = HillHit(other, away * s.volleyPush + Vector3.up * s.volleyLift, true, "교차 공중 포격");
                if (other.State == PawnState.Ragdoll) down++;
                Log($"교차 공중 포격 → {other.DisplayName}: {rel.magnitude:0.0} m" + (climbing ? " (벽에 매달림)" : "") + $" → {result}");
            }
            Fx(SkillFxKind.SkillFail, null, c, planDir, (int)PieceKind.Bishop);   // (a plain sound for the landing, until it has its own)
            Log($"교차 공중 포격: 교차 파동 — {down}명 넘어짐");
        }

        void UpdateVolley(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        SkillUses++;
                        planPhase = PlanPhase.Aim;
                        planTime = hoverTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        hoverHipY = FeetPoint.y + s.volleyHover + standHeight;
                        hoverFromY = hoverLastY = bodies[0].position.y;
                        hoverAt = new Vector3(bodies[0].position.x, 0f, bodies[0].position.z);
                        hoverLeft = s.volleyHoverTime;
                        hoverLinger = 0f;
                        hoverVel = Vector3.zero;
                        shellOut = volleyFired = false;
                        volleyShots = Mathf.Clamp(s.volleyShots, 1, 3);
                        VolleyAim(s);
                        SkillDetail = $"떠오름 (이동 키로 천천히 · 좌클릭 발사 {volleyShots}발 · {s.volleyHoverTime:0.#}초 안에 안 쏘면 취소)";
                        Log($"교차 공중 포격: 떠오름 {s.volleyHover:0.0} m · {s.volleyHoverTime:0.#}초");
                    }
                    break;
                case PlanPhase.Aim:
                    // Hovering and aiming. The hover's time only runs before the first shot (승규 님 R105).
                    planTime += dt;
                    hoverTime += dt;
                    Hover(s, dt);
                    VolleyAim(s);
                    if (!volleyFired) hoverLeft -= dt;
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        Log(volleyFired ? "교차 공중 포격: 남은 발을 두고 내려옴" : "교차 공중 포격: 쏘지 않고 내려옴");
                        DropHover();
                    }
                    else if (confirm && PlanAimValid) FireShell(s);
                    else if (confirm) Log("교차 공중 포격: 거기는 쏠 수 없어요");
                    else if (!volleyFired && hoverLeft <= 0f)
                    {
                        Log($"교차 공중 포격: {s.volleyHoverTime:0.#}초 동안 안 쏴서 취소");
                        DropHover();
                    }
                    break;
                case PlanPhase.Active:
                    // A shot on its way; then the next aim, or down after the last.
                    planTime += dt;
                    hoverTime += dt;
                    Hover(s, dt);
                    if (shellOut)
                    {
                        if (planTime >= s.volleyFlight)
                        {
                            shellOut = false;
                            VolleyImpact(s);
                            hoverLinger = 0.3f;
                        }
                        break;
                    }
                    if (volleyShots > 0)
                    {
                        planPhase = PlanPhase.Aim;
                        planTime = 0f;
                        SkillDetail = $"다시 조준 (좌클릭 발사, {volleyShots}발 남음 · 우클릭/스킬 키 내려옴)";
                        break;
                    }
                    hoverLinger -= dt;
                    if (hoverLinger <= 0f) DropHover();
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    KeepSkillLandingSafe();
                    if ((OnFloor && planTime > 0.12f) || planTime > 3f)
                    {
                        EndPlanSkill();
                        StartCooldown(s.volleyCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        // ---------------------------------------------------------------- 도약 압착 (the knight's press leap)

        // A squashed piece sliding out of the landing circle and watching for the fall off its ledge; the piece the
        // knight came down on.
        Vector3 flattenSlide;
        float flattenWatch, flattenFloor, flattenSlideLeft, stompedLeft;
        RagdollPawn stompedOne;
        bool pressLanded;
        string pressWhy = "";

        /// <summary>The top of a piece's head (its colliders' highest point; a little over the head body otherwise).</summary>
        static float HeadTop(RagdollPawn p)
        {
            var head = p.bodies[(int)BodyId.Head];
            float top = head.position.y + 0.12f;
            foreach (var c in head.GetComponentsInChildren<Collider>())
                if (!c.isTrigger) top = Mathf.Max(top, c.bounds.max.y);
            return top;
        }

        /// <summary>The enemy whose head the leap's aim catches: standing within pressSnap of the aimed spot, in reach and
        /// at most one tier up; the closest to the spot.</summary>
        RagdollPawn PressHeadPick(PawnRushSkillParams s, Vector3 spot)
        {
            Vector3 me = FeetPoint;
            RagdollPawn best = null;
            float bestD = s.pressSnap;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other) || other.State != PawnState.Active
                    || other.SkillUntargetable || other.castleCarry) continue;
                Vector3 feet = other.FeetPoint;
                if (feet.y - me.y > s.pressMaxRise) continue;
                float reach = Flat(feet - me).magnitude;
                if (reach > s.pressRange + 0.5f || reach < 0.8f) continue;
                float d = Flat(feet - spot).magnitude;
                if (d > bestD) continue;
                best = other;
                bestD = d;
            }
            return best;
        }

        void PressAim(PawnRushSkillParams s)
        {
            AimFloor(s.pressRange, s.pressMaxRise, out Vector3 point, out bool valid, out pressWhy);
            if (valid && !RoomToStandAt(point)) { valid = false; pressWhy = "설 자리가 없음"; }
            planPoint = point;
            // An enemy close to where the circle is gets caught by it, and the leap goes onto its head (승규 님 R93).
            planTarget = PressHeadPick(s, point);
            if (planTarget != null)
            {
                planPoint = planTarget.FeetPoint;
                valid = true;
                pressWhy = "";
            }
            planNote = (byte)(valid ? 1 : 0);
            Vector3 way = Flat(planPoint - bodies[0].position);
            if (way.sqrMagnitude > 0.04f) planDir = way.normalized;
        }

        void BeginPressLeap(PawnRushSkillParams s)
        {
            Vector3 from = FeetPoint;
            if (planTarget != null && planTarget.State != PawnState.Active) planTarget = null;
            Vector3 toHips = planPoint + Vector3.up * (standHeight + 0.02f);
            if (planTarget != null)
            {
                // Onto the head: the feet come down on top of it, where it stands now.
                Vector3 head = planTarget.bodies[(int)BodyId.Head].position;
                planPoint = new Vector3(head.x, planTarget.FeetPoint.y, head.z);
                toHips = new Vector3(head.x, HeadTop(planTarget) + standHeight, head.z);
            }
            // A thrown arc under a heavier gravity (short and sharp), always well over a head it comes down on.
            float g = Mathf.Max(0.01f, -Physics.gravity.y) * Mathf.Max(0.2f, s.pressGravity);
            float start = bodies[0].position.y;
            float top = Mathf.Max(Mathf.Max(from.y, planPoint.y) + s.pressArc + standHeight, toHips.y + 0.5f);
            float time = (Mathf.Sqrt(2f * g * Mathf.Max(0.05f, top - start)) + Mathf.Sqrt(2f * g * Mathf.Max(0.05f, top - toHips.y))) / g;
            skillHitSet.Clear();
            pressLanded = false;
            BeginArcCarry(toHips, time, top, planTarget);
            planPhase = PlanPhase.Active;
            planTime = 0f;
            SkillDetail = planTarget != null ? "도약 (머리 밟기)" : "도약";
            Log($"도약 압착: 도약 {Flat(planPoint - from).magnitude:0.0} m, 높이 차 {planPoint.y - from.y:+0.0;-0.0} m, {time:0.00}초"
                + (planTarget != null ? $", {planTarget.DisplayName} 머리 위로" : ""));
        }

        /// <summary>The first way <paramref name="p"/> can slide a metre along the floor without hitting a wall: the given
        /// way, then 45° either side of it, and so on round.</summary>
        Vector3 OpenWay(RagdollPawn p, Vector3 first)
        {
            if (first.sqrMagnitude < 1e-4f) first = Vector3.forward;
            Vector3 from = p.bodies[0].position;
            float[] turns = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
            foreach (float a in turns)
            {
                Vector3 d = Quaternion.Euler(0f, a, 0f) * first;
                if (!SolidRay(from, d, 1.1f, out var wall) || Mathf.Abs(wall.normal.y) >= 0.6f) return d;
            }
            return first;
        }

        void PressLand(PawnRushSkillParams s)
        {
            Vector3 feet = FeetPoint;
            int n = 0;
            foreach (var other in All)
            {
                if (other == null || other == this || other.NetworkPuppet || !IsEnemyOf(other) || other.State != PawnState.Active || other.SkillUntargetable) continue;
                Vector3 d = other.bodies[0].position - bodies[0].position;
                // The piece it came down on is under its feet: caught whatever the circle says.
                bool stomped = other == planTarget && Flat(other.bodies[(int)BodyId.Head].position - bodies[0].position).magnitude < 0.95f;
                if (!stomped && (Flat(d).magnitude > s.pressRadius || Mathf.Abs(other.FeetPoint.y - feet.y) > 1f)) continue;
                Vector3 away = Flat(d).sqrMagnitude > 0.01f ? Flat(d).normalized : Flat(facing).normalized;
                if (other.wardLeft > 0f || other.WardCover() != null)
                {
                    // Guarded: it only staggers and gives a little.
                    SkillHit(other, away * s.pressSlide, true, "도약 압착");
                    continue;
                }
                if (!other.Squash(s.pressFlatten, 0.5f))
                {
                    other.Stagger(0.3f);
                    continue;
                }
                float slide = s.pressSlide;
                if (stomped)
                {
                    // Squashed under its feet: it squirts out the way the knight was going if that is open (else the
                    // nearest open way), a little faster; the two collide again once they are apart.
                    away = OpenWay(other, Flat(facing).normalized);
                    slide *= 1.25f;
                    IgnorePiece(other, true);
                    stompedOne = other;
                    stompedLeft = 2.5f;
                }
                n++;
                // Slid out of the circle while flat: on a ledge's edge that is over it (flattened up high, it falls).
                other.flattenSlide = away * slide;
                other.flattenSlideLeft = s.pressSlideTime;
                other.flattenWatch = s.pressFlatten + 0.4f;
                other.flattenFloor = other.FeetPoint.y;
                other.LastSkillHit = stomped ? "도약 압착: 머리 밟혀 납작" : "도약 압착: 납작";
                Fx(SkillFxKind.KnightStomp, other, other.bodies[(int)BodyId.Head].position, away, 1);
                Log($"도약 압착 → {other.DisplayName}: {(stomped ? "머리 밟기 → " : "")}납작 {s.pressFlatten:0.0#}초");
            }
            Fx(SkillFxKind.KnightLand, null, feet, Flat(facing), n, s.pressRadius);
            if (n == 0) Log("도약 압착: 착지 원 안에 적이 없음");
        }

        /// <summary>Squashed and pushed out: it skids at the slide's speed for a moment, and if that takes it off a ledge it
        /// does not land on its feet.</summary>
        void UpdateFlattenFall(float dt)
        {
            if (flattenWatch <= 0f) return;
            flattenWatch -= dt;
            if (State == PawnState.Ragdoll) { flattenWatch = flattenSlideLeft = 0f; return; }
            if (flattenSlideLeft > 0f)
            {
                flattenSlideLeft -= dt;
                Vector3 own = Flat(bodies[0].linearVelocity) - carryVel;
                AddVelocity(flattenSlide - own);
                anchorVel = flattenSlide + carryVel;
                Vector3 hp = bodies[0].position;
                anchorPos = new Vector3(hp.x + anchorVel.x * dt, anchorPos.y, hp.z + anchorVel.z * dt);
                anchor.MovePosition(anchorPos);
            }
            bool off = !Grounded && coyote <= 0f && bodies[0].linearVelocity.y < -1f
                       && bodies[0].position.y < flattenFloor + standHeight - 0.2f;
            if (!off) return;
            flattenWatch = 0f;
            squashTimer = 0f;
            Knockdown("도약 압착: 납작해져 떨어짐", PawnRushSkills != null ? PawnRushSkills.hillDownHold : 0f);
            Log($"{DisplayName}: 납작해진 채 가장자리에서 떨어짐");
        }

        void UpdatePressLeap(PawnRushSkillParams s, bool press, bool confirm, bool cancel, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        planPhase = PlanPhase.Aim;
                        planTime = 0f;
                        skillGrabLatch = skillGrabRaw;
                        PressAim(s);
                        SkillDetail = "착지점 조준 (좌클릭 도약 · 우클릭/스킬 키 취소)";
                    }
                    break;
                case PlanPhase.Aim:
                    planTime += dt;
                    PressAim(s);
                    if (Flat(skillMoveRaw).sqrMagnitude < 0.04f) facing = AimFlat();
                    if (cancel || press)
                    {
                        if (cancel) skillGrabLatch = true;
                        EndPlanSkill();
                        Log("도약 압착: 조준 취소");
                        break;
                    }
                    if (!confirm) break;
                    if (!PlanAimValid)
                    {
                        Log($"도약 압착: 거기로는 못 뛰어요 ({pressWhy})");
                        break;
                    }
                    SkillUses++;
                    planPhase = PlanPhase.Windup;
                    planTime = 0f;
                    facing = FlatDir(planPoint - bodies[0].position);
                    SkillDetail = planTarget != null ? $"도약 준비 ({planTarget.DisplayName} 머리 위)" : "도약 준비";
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    if (planTime >= s.pressWindup) BeginPressLeap(s);
                    break;
                case PlanPhase.Active:
                    // (Its own flight holds the step back: this runs once it has come down or been dropped.)
                    planTime += dt;
                    if (castleCarry) break;
                    if (!pressLanded)
                    {
                        pressLanded = true;
                        PressLand(s);
                    }
                    if (OnFloor || planTime > 0.6f)
                    {
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "착지 후딜";
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.pressRecovery)
                    {
                        EndPlanSkill();
                        StartCooldown(s.pressCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        // ---------------------------------------------------------------- 비집고 돌파 (the pawn's squeeze through)

        /// <summary>Pieces it squeezes past are nudged aside (no knockdown); the pawn itself is not protected.</summary>
        void SqueezeBumps(PawnRushSkillParams s)
        {
            Vector3 at = bodies[(int)BodyId.Chest].position + skillDir * 0.3f;
            Vector3 right = Vector3.Cross(Vector3.up, skillDir);
            int n = Physics.OverlapSphereNonAlloc(at, 0.42f, skillOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (!ColliderOwner.TryGetValue(skillOverlap[i], out var other) || other == null || other == this || skillHitSet.Contains(other)) continue;
                if (other.State == PawnState.Ragdoll || other.NetworkPuppet) continue;
                skillHitSet.Add(other);
                float side = Vector3.Dot(Flat(other.bodies[0].position - bodies[0].position), right);
                Vector3 push = right * (Mathf.Abs(side) < 0.02f ? 1f : Mathf.Sign(side)) * s.squeezeNudge;
                other.TakeHit(push, 0f, 0f, false);
                other.LastSkillHit = "비집고 돌파: 옆으로 비킴";
                Log($"비집고 돌파 → {other.DisplayName}: 옆으로 비킴");
            }
        }

        void UpdateSqueeze(PawnRushSkillParams s, bool press, float dt)
        {
            switch (planPhase)
            {
                case PlanPhase.None:
                    if (press && CanStartSkill())
                    {
                        SkillUses++;
                        skillDir = MoveOrAim();
                        planDir = skillDir;
                        facing = skillDir;
                        planPhase = PlanPhase.Windup;
                        planTime = 0f;
                        SkillDetail = "몸 낮춤";
                    }
                    break;
                case PlanPhase.Windup:
                    planTime += dt;
                    facing = skillDir;
                    if (planTime >= s.squeezeCrouch)
                    {
                        skillHitSet.Clear();
                        StartDash(s.squeezeSpeed, s.squeezeDistance / Mathf.Max(0.5f, s.squeezeSpeed));
                        planPhase = PlanPhase.Active;
                        planTime = 0f;
                        SkillDetail = "비집고 돌파";
                    }
                    break;
                case PlanPhase.Active:
                    planTime += dt;
                    if (WallAhead(dashBase * dt + 0.22f, out _))
                    {
                        // A wall ends the dash where it stands: no passing through, no stagger.
                        dashLeft = 0f;
                        HaltDash();
                    }
                    else
                    {
                        DriveDash(dt);
                        SqueezeBumps(s);
                        dashLeft -= dt;
                    }
                    if (dashLeft <= 0f)
                    {
                        EndDash();
                        planPhase = PlanPhase.Recovery;
                        planTime = 0f;
                        SkillDetail = "일어남";
                    }
                    break;
                case PlanPhase.Recovery:
                    planTime += dt;
                    if (planTime >= s.squeezeRise)
                    {
                        EndPlanSkill();
                        StartCooldown(s.squeezeCooldown);
                    }
                    break;
                default:
                    EndPlanSkill();
                    break;
            }
        }

        /// <summary>These skills' part of the body pose (a lean only; the mode's own poses stayed with it): the pawn low
        /// and forward through its squeeze. True when it set one.</summary>
        bool HillPose(ref Quaternion chest, ref Quaternion head)
        {
            if (planPhase == PlanPhase.None || activeSkill != PawnRushSkillIds.Squeeze) return false;
            var s = PawnRushSkills;
            float low = planPhase == PlanPhase.Windup ? Mathf.Clamp01(planTime / Mathf.Max(0.01f, s.squeezeCrouch))
                      : planPhase == PlanPhase.Active ? 1f
                      : 1f - Mathf.Clamp01(planTime / Mathf.Max(0.01f, s.squeezeRise));
            chest = Quaternion.Slerp(chest, Quaternion.Euler(38f, 0f, 0f), low);
            head = Quaternion.Slerp(head, Quaternion.Euler(-24f, 0f, 0f), low);
            return true;
        }
    }
}
