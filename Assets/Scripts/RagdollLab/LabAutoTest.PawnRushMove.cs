using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R513 (the user, 11-09: "점프 높이가 매번 다른 문제, 다이빙도 매번 다른 문제 … 폴가이즈를 레퍼런스로 … 다이빙 할 때 캐릭터가
    /// 높이서 떨어진다고 공중에서 공중제비가 돌아지면 안돼. 높이가 어쨌든 배치기하는 형태로 낙하"): the Pawn Rush jump and dive done
    /// many times over, every pawn holding the Pawn Rush movement as the Pawn Rush scenes hand it out (PawnRushPhysicsParams).
    ///   jumps       standing, running and sprinting, the key pressed at a different point of the stride each time: how high
    ///               the hips get over the floor, the spread between the highest and the lowest
    ///   buffer      a second jump pressed a moment before the first one lands: does it go
    ///   dives       from a run, a sprint and standing: how far, how high, how the body lies (the hips' pitch along the dive,
    ///               0 upright, 90 flat head first, 180 head down; and whether the front faces the floor)
    ///   high dives  diving right after a jump's top and from 4, 8 and 15 m up: never past head down (no somersault), on its
    ///               front when it comes down
    /// Run alone: RagdollLab.exe -batchmode -nographics -ragdollAutoTest r.txt -ragdollOnly PawnRushMove
    ///
    /// Measured 11-09 (Unity 6000.3, 120 Hz), before R513 → after: the hips' top over the floor standing 1.08 / running
    /// 1.13~1.25 / sprinting 0.58~0.86 m → 1.09 / 1.07~1.09 / 1.07~1.09 m; a press 30~100 ms before landing went 0 of 4
    /// times → 4 of 4; a dive from 4 / 8 / 15 m turned 163 / 298 / 280 degrees and came down on its back from 8 m → at most
    /// 104, down at 88~90 on its front.
    /// </summary>
    public partial class LabAutoTest
    {
        // A lane on the main floor clear of everything (the sprint check's circle is west of it, the dummies east).
        static readonly Vector3 RushLane = new Vector3(-12f, 0f, -12f);
        const int RushJumpTrials = 12, RushDiveTrials = 8;

        RagdollPawn SpawnRush(Vector3 ground, Vector3 forward, string name)
        {
            var pawn = Spawn(ground, forward, name);
            pawn.PawnRushPhysics = new PawnRushPhysicsParams();
            return pawn;
        }

        /// <summary>The hips' pitch along <paramref name="dir"/>: 0 upright, 90 flat head first, 180 head down.</summary>
        static float RushPitch(RagdollPawn pawn, Vector3 dir)
        {
            Vector3 spine = pawn.Hips.transform.up;
            return Mathf.Atan2(Vector3.Dot(spine, dir), Vector3.Dot(spine, Vector3.up)) * Mathf.Rad2Deg;
        }

        /// <summary>1: the front faces the floor (a belly flop); -1: lying on the back.</summary>
        static float RushBelly(RagdollPawn pawn) => Vector3.Dot(pawn.Hips.transform.forward, Vector3.down);

        static string RushStats(List<float> xs) => xs.Count == 0 ? "-"
            : $"{xs.Min():F2}~{xs.Max():F2} (평균 {xs.Average():F2}, 폭 {xs.Max() - xs.Min():F2}, n={xs.Count})";

        IEnumerator PawnRushMove()
        {
            yield return Clear();
            var pawn = SpawnRush(RushLane, Vector3.forward, "rush-move");
            yield return Sim(1f);
            float stand = pawn.standHeight;

            // ---- jumps: standing, running, sprinting, pressed at a different point of the stride each time
            var standing = new List<float>();
            var running = new List<float>();
            var sprinting = new List<float>();
            var airTimes = new List<float>();
            var runAir = new List<float>();
            for (int kind = 0; kind < 3; kind++)
            {
                var into = kind == 0 ? standing : kind == 1 ? running : sprinting;
                rushLowTrace = "";
                for (int i = 0; i < RushJumpTrials; i++)
                {
                    // 31 ms later each time: twelve presses walk once through a stride (about 0.38 s at the run).
                    float runFor = kind == 0 ? 0f : 1.1f + i * 0.031f;
                    yield return RushJump(pawn, kind > 0, kind == 2, runFor, into, kind == 1 ? runAir : airTimes);
                }
                if (kind > 0) Info("PawnRush 가장 낮은 점프의 첫 0.5초 (y 골반 높이, vy 몸 위아래 속도, G 땅, up 보정 횟수)", rushLowTrace);
            }
            float ideal = PawnRushIdealApex(pawn);
            Info("PawnRush 점프 (골반 최고 높이 - 바닥, m)",
                $"제자리 {RushStats(standing)} / 달리기 {RushStats(running)} / 질주 {RushStats(sprinting)}; 서 있는 골반 {stand:F2}, 이론 {ideal:F2}; 체공 달리기 {RushStats(runAir)} s · 제자리·질주 {RushStats(airTimes)} s");
            float all = standing.Concat(running).Concat(sprinting).Max() - standing.Concat(running).Concat(sprinting).Min();
            Report("PawnRush 점프 높이가 매번 같음 (제자리·달리기·질주, 보폭 어디서 눌러도)",
                all <= 0.08f && running.Max() - running.Min() <= 0.05f && sprinting.Max() - sprinting.Min() <= 0.05f,
                $"전체 폭 {all:F2} m (기준 0.08), 달리기 폭 {running.Max() - running.Min():F2}, 질주 폭 {sprinting.Max() - sprinting.Min():F2} (기준 0.05)");

            // ---- buffer: the next running jump pressed this long before the first one lands (the running jumps' air time)
            float land = runAir.Count > 0 ? runAir.Where(a => a > 0f).DefaultIfEmpty(0.65f).Average() : 0.65f;
            var early = new[] { 0.03f, 0.06f, 0.08f, 0.10f };
            var went = new List<string>();
            int buffered = 0;
            foreach (float e in early)
            {
                bool second = false;
                yield return RushJumpBuffer(pawn, land - e, ok => second = ok);
                went.Add($"{e * 1000f:F0}ms {(second ? "점프" : "안 됨")}");
                if (second) buffered++;
            }
            Report("PawnRush 착지 직전에 누른 점프가 착지하면 나감 (0.12초 전까지)", buffered == early.Length, string.Join(", ", went));

            // ---- dives from the floor
            var dist = new List<float>();
            var top = new List<float>();
            var pitch = new List<float>();
            var belly = new List<float>();
            var upAt = new List<float>();
            int flips = 0, rolls = 0;
            for (int kind = 0; kind < 3; kind++)
            {
                dist.Clear(); top.Clear(); pitch.Clear(); belly.Clear(); upAt.Clear();
                int trials = kind == 2 ? 4 : RushDiveTrials;
                for (int i = 0; i < trials; i++)
                {
                    float runFor = kind == 2 ? 0f : 1.1f + i * 0.047f;
                    yield return RushDive(pawn, kind < 2, kind == 1, runFor, (d, h, p, b, t) =>
                    {
                        dist.Add(d); top.Add(h); pitch.Add(p); belly.Add(b); upAt.Add(t);
                    });
                }
                flips += pitch.Count(p => p > 150f);
                rolls += belly.Count(b => b < 0.5f);
                string name = kind == 0 ? "달리기" : kind == 1 ? "질주" : "제자리";
                Info($"PawnRush 다이빙 ({name})",
                    $"거리 {RushStats(dist)} m, 골반 최고 {RushStats(top)} m, 최대 기울기 {RushStats(pitch)}°, 일어날 때 배 {RushStats(belly)}, 일어나기 시작 {RushStats(upAt)} s");
                if (kind < 2)
                    Report($"PawnRush {name} 다이빙 거리가 매번 비슷함", dist.Max() - dist.Min() <= 0.15f * dist.Average(),
                        $"거리 {RushStats(dist)} m (폭 기준 평균의 15%)");
            }
            Report("PawnRush 바닥 다이빙: 앞으로 엎어져 미끄러짐 (머리 아래로 안 넘어가고 등으로 안 구름)", flips == 0 && rolls == 0,
                $"머리 아래로 넘어감 {flips}번, 등으로 누움 {rolls}번");

            // ---- dives off a jump's top and from high up: no somersault, down on the front
            var landPitch = new List<string>();
            int badHigh = 0;
            foreach (float height in new[] { 0f, 4f, 8f, 15f })
            {
                for (int i = 0; i < 2; i++)
                {
                    float maxPitch = 0f, pitchDown = 0f, bellyDown = 0f;
                    bool knocked = false;
                    yield return RushHighDive(pawn, height, i, (m, p, b, k) => { maxPitch = m; pitchDown = p; bellyDown = b; knocked = k; });
                    // R513 measured 104 at most, 88~101 coming down, belly 0.98~1.00 (before: 280~298 and on its back from 8 m).
                    bool ok = maxPitch <= 115f && pitchDown >= 70f && pitchDown <= 110f && bellyDown >= 0.8f;
                    if (!ok) badHigh++;
                    string where = height <= 0f ? "점프 꼭대기" : $"{height:F0} m";
                    landPitch.Add($"{where}: 최대 {maxPitch:F0}° · 닿을 때 {pitchDown:F0}° · 배 {bellyDown:F2}{(knocked ? " · 넘어짐" : "")}");
                }
            }
            Info("PawnRush 공중 다이빙 (골반 기울기: 90 = 배로 수평, 180 = 머리 아래)", string.Join(" / ", landPitch));
            Report("PawnRush 공중·높은 곳 다이빙: 공중제비 없이 배로 떨어짐", badHigh == 0, $"어긋난 다이빙 {badHigh}번 / 8번");
            yield return Clear();
        }

        /// <summary>The hips' highest over the floor a clean Pawn Rush jump from standing reaches (standing height + v² / 2g).</summary>
        static float PawnRushIdealApex(RagdollPawn pawn)
        {
            var q = pawn.PawnRushPhysics;
            float g = -Physics.gravity.y * q.gravityScale;
            return pawn.standHeight + q.jumpSpeed * q.jumpSpeed / (2f * g);
        }

        void RushReset(RagdollPawn pawn) => pawn.Teleport(RushLane + Vector3.up * pawn.standHeight, Vector3.forward);

        IEnumerator RushJump(RagdollPawn pawn, bool moving, bool sprint, float runFor, List<float> apex, List<float> air)
        {
            RushReset(pawn);
            yield return Sim(0.6f, () => Drive(pawn, Vector3.zero));
            if (moving) yield return Sim(runFor, () => Drive(pawn, Vector3.forward, sprint: sprint));
            float highest = float.MinValue, t = 0f, landed = -1f;
            bool pressed = false, left = false;
            var trace = new System.Text.StringBuilder();
            int topUps = pawn.RiseTopUps, steps = pawn.StepOvers;
            trace.Append($"(뛰는 동안 턱 오르기 {steps}번) ");
            yield return Sim(1.4f, () =>
            {
                Drive(pawn, moving ? Vector3.forward : Vector3.zero, jump: !pressed, sprint: sprint);
                pressed = true;
                // Every 2nd step of the first half second: what the climb was doing (the lowest jump's is reported).
                if (t < 0.5f && Mathf.RoundToInt(t / Dt) % 2 == 0)
                    trace.Append($"{t:F3}:y{pawn.Hips.position.y - RushLane.y:F2} vy{RushComVy(pawn):F2}{(pawn.Grounded ? " G" : "")}"
                                 + $"{(pawn.Stunned ? " hit" : "")}{(pawn.Touching ? " touch" : "")} up{pawn.RiseTopUps - topUps}"
                                 + $"{(pawn.StepOvers > steps ? " step" + (pawn.StepOvers - steps) : "")} ");
                t += Dt;
                if (landed < 0f) highest = Mathf.Max(highest, pawn.Hips.position.y);
                if (!pawn.Grounded) left = true;
                else if (left && landed < 0f) landed = t;
            });
            float rise = highest - RushLane.y;
            if (apex.Count == 0 || rise < apex.Min()) rushLowTrace = $"{(sprint ? "질주" : moving ? "달리기" : "제자리")} {rise:F2} m: {trace}";
            apex.Add(rise);
            air.Add(landed);
        }

        string rushLowTrace = "";

        static float RushComVy(RagdollPawn pawn)
        {
            float m = 0f, p = 0f;
            foreach (var rb in pawn.bodies)
            {
                m += rb.mass;
                p += rb.mass * rb.linearVelocity.y;
            }
            return p / Mathf.Max(0.001f, m);
        }

        /// <summary>A running jump, and the next one pressed at <paramref name="pressAt"/> s after the first press.</summary>
        IEnumerator RushJumpBuffer(RagdollPawn pawn, float pressAt, System.Action<bool> secondJump)
        {
            RushReset(pawn);
            yield return Sim(0.6f, () => Drive(pawn, Vector3.zero));
            yield return Sim(1.1f, () => Drive(pawn, Vector3.forward));
            float t = 0f;
            bool first = false, second = false, fell = false, again = false;
            yield return Sim(2f, () =>
            {
                bool press = false;
                if (!first) { press = true; first = true; }
                else if (!second && t >= pressAt) { press = true; second = true; }
                Drive(pawn, Vector3.forward, jump: press);
                t += Dt;
                // Only a jump sends the body up faster than 3 m/s: once the first one is coming down, a second take-off.
                // (The landing itself may never show as Grounded here: a jump kept for it goes in that very step.)
                float vy = RushComVy(pawn);
                if (t > 0.2f && vy < -1f) fell = true;
                if (fell && vy > 3f && t - pressAt < 0.6f) again = true;
            });
            secondJump(again);
        }

        IEnumerator RushDive(RagdollPawn pawn, bool moving, bool sprint, float runFor, System.Action<float, float, float, float, float> done)
        {
            RushReset(pawn);
            yield return Sim(0.6f, () => Drive(pawn, Vector3.zero));
            if (moving) yield return Sim(runFor, () => Drive(pawn, Vector3.forward, sprint: sprint));
            Vector3 from = pawn.Hips.position;
            float highest = float.MinValue, maxPitch = 0f, last = RushPitch(pawn, Vector3.forward), turned = last, t = 0f, up = -1f, belly = 0f;
            Vector3 at = from;
            bool pressed = false;
            yield return Sim(3f, () =>
            {
                Drive(pawn, moving ? Vector3.forward : Vector3.zero, shove: !pressed, sprint: sprint);
                pressed = true;
                t += Dt;
                if (up >= 0f) return;
                highest = Mathf.Max(highest, pawn.Hips.position.y);
                float now = RushPitch(pawn, Vector3.forward);
                turned += Mathf.DeltaAngle(last, now);
                last = now;
                maxPitch = Mathf.Max(maxPitch, turned);
                if (t > 0.1f && pawn.State == PawnState.GettingUp)
                {
                    up = t;
                    belly = RushBelly(pawn);
                    at = pawn.Hips.position;
                }
            });
            if (up < 0f) at = pawn.Hips.position;
            done(Flat(at - from).magnitude, highest - RushLane.y, maxPitch, belly, up);
        }

        /// <summary>
        /// A dive in the air: off a jump's top (<paramref name="height"/> 0, from a run), or from <paramref name="height"/>
        /// metres up (dropped, the dive pressed 0.1 s into the fall). Reports the pitch the hips reached on the way down
        /// (unwrapped: past 180 is a somersault), the pitch and the belly as they come down, and whether it was floored.
        /// </summary>
        IEnumerator RushHighDive(RagdollPawn pawn, float height, int trial, System.Action<float, float, float, bool> done)
        {
            int knocks = pawn.Knockdowns;
            RushReset(pawn);
            yield return Sim(0.6f, () => Drive(pawn, Vector3.zero));
            bool fromJump = height <= 0f;
            if (fromJump) yield return Sim(1.1f + trial * 0.07f, () => Drive(pawn, Vector3.forward));
            else
            {
                pawn.Teleport(RushLane + Vector3.up * (height + pawn.standHeight), Vector3.forward);
                yield return Sim(0.1f, () => Drive(pawn, Vector3.forward));
            }
            float last = RushPitch(pawn, Vector3.forward), turned = last, maxPitch = turned, pitchDown = 0f, bellyDown = 0f;
            // From a run: jump first, then dive at the jump's top (the hips stop rising). From up high: dive at once.
            bool jumped = !fromJump, dived = false, down = false;
            float t = 0f, sinceJump = 0f;
            yield return Sim(fromJump ? 2.4f : 4f, () =>
            {
                bool jump = !jumped, dive = false;
                jumped = true;
                sinceJump += Dt;
                if (!dived && (!fromJump || (sinceJump > 0.1f && pawn.Hips.linearVelocity.y <= 0f)))
                {
                    dive = true;
                    dived = true;
                }
                Drive(pawn, Vector3.forward, jump: jump, shove: dive);
                if (!dived) return;
                t += Dt;
                if (down) return;
                float now = RushPitch(pawn, Vector3.forward);
                turned += Mathf.DeltaAngle(last, now);
                last = now;
                maxPitch = Mathf.Max(maxPitch, turned);
                // Down: the hips within half a metre of the floor (lying flat they are about 0.15 m over it).
                if (t > 0.05f && pawn.Hips.position.y - RushLane.y < 0.45f)
                {
                    down = true;
                    pitchDown = turned;
                    bellyDown = RushBelly(pawn);
                }
            });
            done(maxPitch, pitchDown, bellyDown, pawn.Knockdowns > knocks);
        }
    }
}
