using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Pawn Rush king's skill 왕의 부름 (R146, 승규 님 10-15, planned in the planning chat with UI mockup D "체스 시계" and
    /// a motion mockup). The king presses F and a list opens of the teammates in his checkpoint section (any distance inside
    /// it; <see cref="KingCallSection"/>, the whole scene in the test bed). He picks at most two with a left click each and
    /// calls them with F; the right click calls it off. Each one called has three seconds to press their own F. They then
    /// rise 12 cm off the floor for 0.15 s and come in 0.6 s (a sine, fastest midway) to the squares right behind the king,
    /// through any wall on the way, invincible all the while. Hitting the king while he calls (a knockdown, a hit, a skill,
    /// being grabbed or tied) calls off whoever has not set out. The cooldown is not decided yet: 2 s for the test.
    /// The host runs all of it: the list's section, the picks (an online client sends them, LabNetLink), the answers, the
    /// invincibility and the walls. Every PC draws it from the skill moments KingCall, KingCallGo, KingCallLand and
    /// KingCallEnd (PawnRushSkillFx.KingCall.cs, PawnRushKingCallUi).
    /// </summary>
    public partial class RagdollPawn
    {
        /// <summary>Whether two pieces are in the same checkpoint section (a course sets it). Null: the scene is one section
        /// (the skill test bed, or its own test split).</summary>
        public static Func<RagdollPawn, RagdollPawn, bool> KingCallSection;

        /// <summary>Test dummies (<see cref="SkillBot"/>) answer a call by themselves after kingCallDummyAnswer seconds
        /// (false: they never answer, to see a call run out).</summary>
        public static bool KingCallBotsAnswer = true;

        public enum KingSlotState : byte { None, Waiting, Going, Arrived, Missed, Cancelled }

        // The king's side (the host's).
        readonly RagdollPawn[] kingAsk = new RagdollPawn[2];
        bool kingAsked;
        readonly RagdollPawn[] kingCalled = new RagdollPawn[2];
        readonly KingSlotState[] kingSlot = new KingSlotState[2];
        int kingCount, kingHitsAtCall;
        string kingHitAtCall;
        readonly List<RagdollPawn> kingListScratch = new List<RagdollPawn>();

        // The called piece's side (the host's).
        RagdollPawn kingCaller;
        int kingCallSlot;
        float kingCallLeft, kingCallAge;

        // The trip in (the host's).
        bool kingCarry;
        RagdollPawn kingCarryKing;
        int kingCarrySlot;
        float kingCarryT, kingShieldLeft, kingCarryFromFloor;
        Vector3 kingCarryFrom, kingCarryTo;

        // What this PC has seen of a call to this piece (any PC; PawnRushKingCallUi notes it), so the key it presses to
        // answer is not drawn as its own skill starting (D-S3).
        float kingSeenUntil;

        /// <summary>On its way in to a king, or just landed: nothing hurts it (R146).</summary>
        public bool KingCallShielded => kingCarry || kingShieldLeft > 0f;
        public bool KingCarried => kingCarry;
        /// <summary>The king who has called this piece and is waiting for its answer (the host's), and how long is left.</summary>
        public RagdollPawn KingCaller => kingCaller;
        public float KingCallLeft => Mathf.Max(0f, kingCallLeft);
        /// <summary>This king has his list open (any PC: the stage comes with the host's skill state).</summary>
        public bool KingListOpen => piece == PieceKind.King && PawnRushSkills != null && skillStage == SkillStage.Windup;
        public RagdollPawn KingCalledAt(int slot) => slot >= 0 && slot < 2 ? kingCalled[slot] : null;
        public KingSlotState KingSlotAt(int slot) => slot >= 0 && slot < 2 ? kingSlot[slot] : KingSlotState.None;
        public int KingCallCount => kingCount;

        /// <summary>This PC has been told (a KingCall moment) that this piece is called, for <paramref name="seconds"/>;
        /// 0: no longer (it set out, or the call ended).</summary>
        public void NoteKingCallSeen(float seconds) => kingSeenUntil = seconds > 0f ? Time.time + seconds : 0f;
        bool KingCallSeen => Time.time < kingSeenUntil;

        /// <summary>A teammate this king may call: on his team, in his section, not already on its way in.</summary>
        public bool KingCanCall(RagdollPawn mate)
        {
            if (mate == null || mate == this || !IsAllyOf(mate) || mate.PawnRushSkills == null || mate.kingCarry) return false;
            return KingCallSection == null || KingCallSection(this, mate);
        }

        /// <summary>The teammates this king may call, in a steady order (his list's).</summary>
        public void KingCallList(List<RagdollPawn> into)
        {
            into.Clear();
            foreach (var p in All)
                if (KingCanCall(p)) into.Add(p);
            into.Sort((a, b) =>
            {
                int byName = string.CompareOrdinal(a.DisplayName, b.DisplayName);
                return byName != 0 ? byName : a.GetInstanceID().CompareTo(b.GetInstanceID());
            });
        }

        /// <summary>The king's picks from his list: called on his next step if the list is open (his own screen, or a
        /// client's through the host). Neither (both null): the list closes.</summary>
        public void AskKingCall(RagdollPawn a, RagdollPawn b)
        {
            kingAsk[0] = a;
            kingAsk[1] = b == a ? null : b;
            kingAsked = true;
        }

        // ---------------------------------------------------------------- the king

        void UpdateKingCall(PawnRushSkillParams s, bool press, bool cancel, float dt)
        {
            switch (skillStage)
            {
                case SkillStage.None:
                    kingAsked = false;
                    if (!press || !CanStartSkill()) break;
                    skillStage = SkillStage.Windup;
                    stageTime = 0f;
                    SkillDetail = "부를 팀원 고르기";
                    KingCallList(kingListScratch);
                    Log($"{DisplayName}: 왕의 부름 — 목록 (같은 구간 팀원 {kingListScratch.Count}명)");
                    break;
                case SkillStage.Windup:
                    // The list: its own key does nothing here (the screen sends the picks with it); the right click closes it.
                    stageTime += dt;
                    if (cancel)
                    {
                        skillGrabLatch = true;
                        ClearSkills();
                        Log("왕의 부름: 취소 (쿨타임 없음)");
                        break;
                    }
                    if (!kingAsked) break;
                    kingAsked = false;
                    CallKingMates(s);
                    break;
                case SkillStage.Active:
                    stageTime += dt;
                    UpdateKingCalling(s);
                    break;
                default:
                    ClearSkills();
                    break;
            }
        }

        void CallKingMates(PawnRushSkillParams s)
        {
            int most = Mathf.Clamp(s.kingCallMax, 1, 2);
            var picks = new RagdollPawn[2];
            int n = 0;
            for (int i = 0; i < 2; i++)
            {
                var mate = kingAsk[i];
                kingAsk[i] = null;
                if (n >= most || !KingCanCall(mate) || mate.kingCaller != null || Array.IndexOf(picks, mate) >= 0) continue;
                picks[n++] = mate;
            }
            if (n == 0)
            {
                ClearSkills();
                Log("왕의 부름: 고른 팀원이 없어 닫음 (쿨타임 없음)");
                return;
            }
            kingCount = n;
            for (int i = 0; i < n; i++)
            {
                var mate = picks[i];
                kingCalled[i] = mate;
                kingSlot[i] = KingSlotState.Waiting;
                mate.kingCaller = this;
                mate.kingCallSlot = i;
                mate.kingCallLeft = s.kingCallWindow;
                mate.kingCallAge = 0f;
                Fx(SkillFxKind.KingCall, mate, KingSpot(i, n), FlatDir(facing), i, s.kingCallWindow);
            }
            skillStage = SkillStage.Active;
            stageTime = 0f;
            SkillUses++;
            SkillDetail = "대답 기다림";
            kingHitsAtCall = Hits;
            kingHitAtCall = LastSkillHit;
            Log($"{DisplayName}: 왕의 부름 → {picks[0].DisplayName}{(n > 1 ? ", " + picks[1].DisplayName : "")} ({s.kingCallWindow:0.#}초 안에 F)");
        }

        /// <summary>The call under way: hit, the ones not yet set out are called off; it ends once everyone has come in or
        /// run out of time (then the cooldown).</summary>
        void UpdateKingCalling(PawnRushSkillParams s)
        {
            bool hit = bindLeft > 0f || BeingHeld || Hits != kingHitsAtCall || LastSkillHit != kingHitAtCall;
            bool late = stageTime > s.kingCallWindow + s.kingCallLift + s.kingCallDash + 1f;
            bool open = false;
            for (int i = 0; i < 2; i++)
            {
                var mate = kingCalled[i];
                if (kingSlot[i] == KingSlotState.Waiting)
                {
                    // A piece gone from the scene, or one this long silent, is not coming.
                    if (mate == null || late) EndKingCallFor(i, KingSlotState.Missed);
                    else if (hit) EndKingCallFor(i, KingSlotState.Cancelled);
                    else open = true;
                }
                else if (kingSlot[i] == KingSlotState.Going)
                {
                    if (mate == null || late || !mate.kingCarry) kingSlot[i] = mate != null && !mate.kingCarry ? KingSlotState.Arrived : KingSlotState.Missed;
                    else open = true;
                }
            }
            if (hit && open) Log("왕의 부름: 킹이 맞아 출발 안 한 팀원 취소");
            if (open) return;
            ResetKingSlots();
            skillStage = SkillStage.None;
            stageTime = 0f;
            SkillDetail = "";
            StartCooldown(s.kingCooldown);
        }

        void ResetKingSlots()
        {
            kingCalled[0] = kingCalled[1] = null;
            kingSlot[0] = kingSlot[1] = KingSlotState.None;
            kingCount = 0;
        }

        /// <summary>This king's call to the piece in <paramref name="slot"/> ends without it coming (it ran out of time,
        /// or the king was hit).</summary>
        void EndKingCallFor(int slot, KingSlotState how)
        {
            var mate = kingCalled[slot];
            kingSlot[slot] = how;
            if (mate != null && mate.kingCaller == this) mate.kingCaller = null;
            bool cancelled = how == KingSlotState.Cancelled;
            Fx(SkillFxKind.KingCallEnd, mate, mate != null ? mate.FeetPoint : FeetPoint, Vector3.zero, slot + (cancelled ? 2 : 0));
            Log($"왕의 부름: {(mate != null ? mate.DisplayName : "팀원")} {(cancelled ? "취소 (킹이 맞음)" : "대답 없음")}");
        }

        /// <summary>Everything a call leaves running when the skills are cleared (a knockdown, a piece switch, a respawn):
        /// the king's called-off ones (those on their way come in anyway); a called piece's call lapses (not its trip in).</summary>
        void ClearKingCall()
        {
            for (int i = 0; i < 2; i++)
                if (kingCalled[i] != null && kingSlot[i] == KingSlotState.Waiting) EndKingCallFor(i, KingSlotState.Cancelled);
            ResetKingSlots();
            kingAsked = false;
            kingAsk[0] = kingAsk[1] = null;
            var king = kingCaller;
            if (king == null) return;
            int slot = kingCallSlot;
            kingCaller = null;
            if (king.kingCalled[slot] == this && king.kingSlot[slot] == KingSlotState.Waiting) king.EndKingCallFor(slot, KingSlotState.Missed);
        }

        // ---------------------------------------------------------------- where they come to

        static readonly float[] KingSpotTurns = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 130f, -130f, 180f };
        static readonly RaycastHit[] kingHits = new RaycastHit[16];
        static readonly Collider[] kingOverlap = new Collider[16];

        /// <summary>Where a called piece comes to, on the floor: the squares right behind the king (one called: straight
        /// behind him; two: the two side by side, the first on his left). One with something solid in it is swapped for the
        /// nearest free spot round him.</summary>
        public Vector3 KingSpot(int slot, int count)
        {
            var s = PawnRushSkills;
            float back = s != null ? s.kingCallBehind : 1.5f, spread = s != null ? s.kingCallSpread : 1.5f;
            Vector3 fwd = FlatDir(facing), right = Vector3.Cross(Vector3.up, fwd);
            Vector3 feet = FeetPoint;
            Vector3 offset = -fwd * back + right * (count >= 2 ? (slot == 0 ? -0.5f : 0.5f) * spread : 0f);
            foreach (float turn in KingSpotTurns)
            {
                Vector3 at = feet + Quaternion.AngleAxis(turn, Vector3.up) * offset;
                float? y = KingFloorAt(at, feet.y);
                if (y == null || Mathf.Abs(y.Value - feet.y) > 1.2f) continue;
                at.y = y.Value;
                if (KingSpotFree(at)) return at;
            }
            return feet + offset;
        }

        /// <summary>The floor under <paramref name="at"/> near the height <paramref name="nearY"/> (pieces and triggers do not
        /// count), or null.</summary>
        static float? KingFloorAt(Vector3 at, float nearY)
        {
            var top = new Vector3(at.x, nearY + 1.2f, at.z);
            int n = Physics.SphereCastNonAlloc(top, 0.12f, Vector3.down, kingHits, 3.2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float? y = null;
            for (int i = 0; i < n; i++)
            {
                var h = kingHits[i];
                if (h.collider == null || h.distance <= 0f || ColliderOwner.ContainsKey(h.collider) || h.normal.y < 0.6f) continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    y = h.point.y;
                }
            }
            return y;
        }

        /// <summary>Nothing solid (a wall, a box) where a piece would stand on <paramref name="floor"/>.</summary>
        static bool KingSpotFree(Vector3 floor)
        {
            int n = Physics.OverlapCapsuleNonAlloc(floor + Vector3.up * 0.4f, floor + Vector3.up * 1.1f, 0.3f, kingOverlap, ~0,
                                                   QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = kingOverlap[i];
                if (c == null || ColliderOwner.ContainsKey(c)) continue;
                var rb = c.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;   // a loose thing is pushed aside
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------- the one called

        /// <summary>A call from a king this step (the host's): its own key answers yes (a test dummy answers by itself), or
        /// the trip in runs. Returns the key press if it was not used.</summary>
        bool UpdateKingCallMate(PawnRushSkillParams s, bool press, float dt)
        {
            if (kingShieldLeft > 0f) kingShieldLeft -= dt;
            if (kingCarry)
            {
                CarryToKing(s, dt);
                return false;
            }
            var king = kingCaller;
            if (king == null) return press;
            kingCallLeft -= dt;
            kingCallAge += dt;
            int slot = kingCallSlot;
            bool still = king.piece == PieceKind.King && king.skillStage == SkillStage.Active && king.kingCalled[slot] == this
                         && king.kingSlot[slot] == KingSlotState.Waiting;
            if (!still)
            {
                kingCaller = null;
                return press;
            }
            if (kingCallLeft <= 0f)
            {
                king.EndKingCallFor(slot, KingSlotState.Missed);
                return press;
            }
            bool yes = SkillBot ? KingCallBotsAnswer && kingCallAge >= s.kingCallDummyAnswer : press;
            if (!yes) return press;
            // Down, tied, held or in the water: the answer waits (the key is used up).
            if (State != PawnState.Active || bindLeft > 0f || BeingHeld || Floating) return false;
            StartKingCarry(s, king, slot);
            return false;
        }

        void StartKingCarry(PawnRushSkillParams s, RagdollPawn king, int slot)
        {
            kingCaller = null;
            ClearSkills();   // its own skill stops; the cooldown it has keeps running
            DropHook();
            DropRope();
            EndClimbPose();
            king.kingSlot[slot] = KingSlotState.Going;
            kingCarry = true;
            kingCarryKing = king;
            kingCarrySlot = slot;
            kingCarryT = 0f;
            kingCarryFrom = bodies[0].position;
            kingCarryFromFloor = FeetPoint.y;
            kingCarryTo = king.KingSpot(slot, Mathf.Max(1, king.kingCount));
            SetKingCarrySolid(false);
            king.Fx(SkillFxKind.KingCallGo, this, kingCarryTo, FlatDir(kingCarryTo - kingCarryFrom), slot, s.kingCallLift + s.kingCallDash);
            Log($"{DisplayName}: 왕의 부름에 대답 → 킹 뒤로 ({Flat(kingCarryTo - kingCarryFrom).magnitude:0.0} m)");
        }

        /// <summary>Through walls on the way in: none of its bodies meets anything (the pieces it passes, the floor) until it
        /// lands.</summary>
        void SetKingCarrySolid(bool solid)
        {
            foreach (var rb in bodies) rb.detectCollisions = solid;
        }

        /// <summary>Where the hips are <paramref name="t"/> seconds into the trip: rising <c>kingCallHover</c> for
        /// <c>kingCallLift</c>, then that high over the floor along the straight line to the square, eased by a sine (still at
        /// both ends, fastest midway).</summary>
        Vector3 KingCarryPath(float t, PawnRushSkillParams s)
        {
            float lift = Mathf.Max(0.01f, s.kingCallLift), dash = Mathf.Max(0.05f, s.kingCallDash), hover = s.kingCallHover;
            Vector3 a = kingCarryFrom, b = kingCarryTo;
            if (t <= lift)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / lift);
                return new Vector3(a.x, kingCarryFromFloor + standHeight + hover * k, a.z);
            }
            float u = Mathf.Clamp01((t - lift) / dash);
            float e = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * u);
            float floor = Mathf.Lerp(kingCarryFromFloor, b.y, e);
            return new Vector3(Mathf.Lerp(a.x, b.x, e), floor + standHeight + hover, Mathf.Lerp(a.z, b.z, e));
        }

        void CarryToKing(PawnRushSkillParams s, float dt)
        {
            var king = kingCarryKing;
            kingCarryT += dt;
            grabImmune = Mathf.Max(grabImmune, 0.25f);
            // The square follows the king if he moves on meanwhile.
            if (king != null && king.kingCount > 0 && king.kingCalled[kingCarrySlot] == this && king.State == PawnState.Active)
                kingCarryTo = king.KingSpot(kingCarrySlot, king.kingCount);
            float end = Mathf.Max(0.01f, s.kingCallLift) + Mathf.Max(0.05f, s.kingCallDash);
            if (kingCarryT >= end)
            {
                EndKingCarry(true);
                return;
            }
            Vector3 at = KingCarryPath(kingCarryT, s), next = KingCarryPath(kingCarryT + dt, s);
            Vector3 way = Flat(kingCarryTo - kingCarryFrom);
            if (way.sqrMagnitude > 0.01f) Face(way);
            Fly(at, (next - at) / Mathf.Max(1e-4f, dt), 0f, default, 18f);
        }

        void EndKingCarry(bool arrived)
        {
            kingCarry = false;
            kingShieldLeft = 0.25f;
            SetKingCarrySolid(true);
            EndFlight(Vector3.zero);
            KeepSkillLandingSafe();
            var king = kingCarryKing;
            kingCarryKing = null;
            if (king != null) Face(king.facing);
            if (!arrived) return;
            if (king != null && king.kingCalled[kingCarrySlot] == this) king.kingSlot[kingCarrySlot] = KingSlotState.Arrived;
            (king != null ? king : this).Fx(SkillFxKind.KingCallLand, this, kingCarryTo, FlatDir(king != null ? king.facing : facing), kingCarrySlot);
            Log($"{DisplayName}: 킹 뒤 칸에 도착 ({kingCarryT:0.00}초)");
        }

        /// <summary>On its way in the keys do nothing (they still arrive).</summary>
        void HoldKeysForKingCall()
        {
            if (!kingCarry) return;
            input.move = Vector3.zero;
            input.jump = false;
            input.sprint = false;
            input.shove = false;
            input.shoveHeld = false;
            input.grab = false;
        }
    }
}
