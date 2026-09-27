using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Automated checks for the launch pads (Queen of the Hill M7) on the [7h] test bed. However a pawn gets
    /// onto the L-shaped pad (walking, sprinting, from the side, jumping, put down on a corner), every throw
    /// lands within half a metre of its spot, "up 6 m, 3 m on". The body reaches the arc's full height: the
    /// movement anchor does not hold it down. A crate flies the same way. The clockwork spring throws
    /// everyone on it at once onto the floor above, as far apart as they stood.
    /// </summary>
    public partial class LabAutoTest
    {
        IEnumerator LaunchChecks()
        {
            var bed = Bed;
            if (bed == null || bed.Pad == null || bed.Spring == null)
            {
                Report("M7 도약대", false, "시험대나 도약대가 없음");
                yield break;
            }
            yield return PadLandings(bed);
            yield return SpringLift(bed);
        }

        struct PadThrowResult
        {
            public string name;
            public bool thrown, limp, upright;
            public float off, rise;
            public Vector3 end;
        }

        /// <summary>A pawn spawned at `ground`, settled, started by `start` (if any), then driven by `drive`
        /// (seconds since the start) until it has been thrown and has landed. The input stops on landing.</summary>
        IEnumerator ThrowFromPad(string name, Vector3 ground, Vector3 facing, Action<RagdollPawn> start,
                                 Func<float, PawnInput> drive, Vector3 spot, List<PadThrowResult> results)
        {
            var pawn = Spawn(ground, facing, name);
            yield return Sim(0.6f);
            start?.Invoke(pawn);
            int before = pawn.Launches;
            float t = 0f, lastY = pawn.Hips.position.y, fromY = 0f, peak = float.MinValue, launchAt = -1f, landAt = -1f;
            bool limp = false;
            void Step()
            {
                bool flying = launchAt >= 0f && landAt < 0f;
                pawn.SetInput(landAt >= 0f ? default : drive(t));
                t += Dt;
                if (launchAt < 0f && pawn.Launches > before)
                {
                    launchAt = t;
                    fromY = lastY;
                }
                lastY = pawn.Hips.position.y;
                if (!flying) return;
                peak = Mathf.Max(peak, pawn.Hips.position.y);
                limp |= pawn.State == PawnState.Ragdoll;
                if (!pawn.Launched && t - launchAt > 0.3f) landAt = t;
            }
            for (float s = 0f; s < 7f && (landAt < 0f || t - landAt < 0.8f); s += Dt) yield return Sim(Dt, Step);
            results.Add(new PadThrowResult
            {
                name = name,
                thrown = launchAt >= 0f,
                limp = limp,
                // Where it comes to rest (the input stops on touching down), not the first step it counts as down.
                off = landAt >= 0f ? Flat(pawn.Hips.position - spot).magnitude : 99f,
                rise = peak - fromY,
                end = pawn.Hips.position,
                // The hips stand about 0.35 m over the feet: above the spot's height is on the ledge.
                upright = landAt >= 0f && pawn.State == PawnState.Active && pawn.Hips.position.y > spot.y + 0.1f,
            });
            yield return Clear();
        }

        IEnumerator PadLandings(QueenHillTestBed bed)
        {
            Vector3 pad = QueenHillTestBed.PadCenter, spot = bed.Pad.Landing;
            var runs = new List<PadThrowResult>();
            yield return ThrowFromPad("걸어서", pad + new Vector3(-3f, 0f, 0f), Vector3.right, null,
                _ => new PawnInput { move = Vector3.right }, spot, runs);
            yield return ThrowFromPad("달려서", pad + new Vector3(-5f, 0f, 0f), Vector3.right, null,
                _ => new PawnInput { move = Vector3.right, sprint = true }, spot, runs);
            yield return ThrowFromPad("옆에서", pad + new Vector3(0.3f, 0f, -2.6f), Vector3.forward, null,
                _ => new PawnInput { move = Vector3.forward }, spot, runs);
            // A hop onto it (a running jump clears the whole pad and is not thrown).
            yield return ThrowFromPad("점프해서", pad + new Vector3(-2f, 0f, 0f), Vector3.right, null,
                s => new PawnInput { move = Vector3.right, jump = s < 0.1f && s + Dt >= 0.1f }, spot, runs);
            yield return ThrowFromPad("모서리에 내려놓음", pad + new Vector3(-3f, 0f, 0f), Vector3.right,
                pawn => pawn.Teleport(pad + new Vector3(-0.6f, pawn.standHeight + 0.02f, 0.6f), Vector3.right),
                _ => default, spot, runs);

            bool all = runs.All(r => r.thrown && !r.limp && r.upright && r.off <= 0.5f);
            Report("M7 L자 도약대: 위 6 m·앞 3 m, 어떻게 올라타도 매번 ±0.5 m 안에 내림 (넘어지지 않음)", all,
                string.Join(", ", runs.Select(r => r.thrown
                    ? $"{r.name} {r.off:F2} m{(r.limp ? " 넘어짐" : "")}{(r.upright ? "" : " 선반 위에 못 섬")}"
                    : $"{r.name} 안 던져짐 (끝에 ({r.end.x:F1}, {r.end.y:F1}, {r.end.z:F1}))")));

            // The height: the arc peaks PadClearance above the landing, 7.2 m over the pad. The anchor must not
            // hold the body down on the way (it rides along with the hips in the air).
            float want = QueenHillTestBed.PadThrow.y + QueenHillTestBed.PadClearance;
            var still = runs[runs.Count - 1];
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "launch-crate";
            crate.transform.position = pad + new Vector3(0.2f, 1.3f, 0.2f);
            crate.transform.localScale = Vector3.one * 0.5f;
            var body = crate.AddComponent<Rigidbody>();
            body.mass = 8f;
            int throwsBefore = bed.Pad.Throws;
            float cratePeak = float.MinValue;
            yield return Sim(4f, () => cratePeak = Mathf.Max(cratePeak, crate.transform.position.y));
            bool crateThrown = bed.Pad.Throws > throwsBefore;
            float crateY = crate.transform.position.y;
            float crateOff = Flat(crate.transform.position - spot).magnitude;
            Destroy(crate);
            bool high = still.thrown && still.rise > want * 0.93f && still.rise < want * 1.07f;
            bool crateOk = crateThrown && crateY > QueenHillTestBed.PadThrow.y && crateOff < 2f;
            Report("M7 발사: 몸이 계산한 높이까지 오름 (이동 앵커가 위로 튀는 힘을 막지 않음), 상자도 같은 곡선",
                high && crateOk,
                $"제자리에서 던져진 몸 {still.rise:F2} m 오름 (계산 {want:F1} m), "
                + $"상자: 던져짐 {crateThrown}, 최고 {cratePeak:F1} m, 선반 위 {crateY:F2} m, 표시에서 {crateOff:F2} m");
            yield return Sim(0.2f);
        }

        IEnumerator SpringLift(QueenHillTestBed bed)
        {
            var spring = bed.Spring;
            // Start early in a cycle, so the pawns stand on it a while before it fires.
            if (spring.SecondsToFire < 2.2f) yield return Sim(spring.SecondsToFire + 0.1f);
            Vector3 center = QueenHillTestBed.SpringCenter + Vector3.up * QueenHillTestBed.SpringTop;
            Vector3[] spots = { new Vector3(-0.6f, 0f, -0.6f), new Vector3(0.6f, 0f, 0.1f), new Vector3(-0.5f, 0f, 0.75f) };
            var pawns = spots.Select((o, i) => Spawn(center + o, Vector3.right, $"spring-{i + 1}")).ToArray();
            yield return Sim(0.6f);
            var from = pawns.Select(p => p.Hips.position).ToArray();
            yield return Sim(Mathf.Max(0f, spring.SecondsToFire - 0.3f));
            bool waited = pawns.All(p => p.Launches == 0);
            int[] launchStep = pawns.Select(_ => -1).ToArray();
            int step = 0;
            yield return Sim(1f, () =>
            {
                step++;
                for (int i = 0; i < pawns.Length; i++)
                    if (launchStep[i] < 0 && pawns[i].Launches > 0) launchStep[i] = step;
            });
            bool together = launchStep.All(s => s > 0 && s == launchStep[0]);
            yield return Sim(3f);
            Vector3 shift = new Vector3(QueenHillTestBed.SpringThrow.z, 0f, 0f);
            var offs = pawns.Select((p, i) => Flat(p.Hips.position - (from[i] + shift)).magnitude).ToArray();
            bool landed = pawns.All(p => p.State == PawnState.Active && p.Hips.position.y > QueenHillTestBed.SpringLedgeTop + 0.1f);
            Report("M7 태엽 스프링: 감기는 동안은 그대로, 풀리면 위에 선 모두를 한꺼번에 8 m 위 바닥으로 (선 간격 그대로)",
                waited && together && landed && offs.All(o => o < 0.6f),
                $"감기는 동안 안 튐 {waited}, 같은 순간 발사 {together}, 위층에 서 있음 {landed}, "
                + $"각자 선 곳 + 6 m에서 {string.Join(" / ", offs.Select(o => o.ToString("F2")))} m");
            yield return Clear();
        }
    }
}
