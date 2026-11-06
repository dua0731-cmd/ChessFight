using System;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class KingRushOpening
    {
        public KingRushRallyGate[] rallies;
        public KingRushSeesawBoard seesawBoard;
        public KingRushSeesawExit[] seesawExits;
        public KingRushSeesawRules Seesaw { get; private set; }
        public float SeesawWeight { get; private set; }
        public int GatheredLast { get; private set; }
        public static Vector3 SeesawEntry(int i) => new Vector3(i == 0 ? 0 : (i < 6 ? -1 : 1) * (1.5f + i % 3 * 1.5f), 0, 394 + i % 6 / 3 * 2);
        readonly Bounds secondArena = new Bounds(new Vector3(0, 3, 412), new Vector3(34, 22, 40));
        readonly RaycastHit[] supportHits = new RaycastHit[32];
        public bool IsSeesawWeight(KingRushPawn p)
        {
            if (!p.CountsAsBody || (!p.Pawn.Grounded && p.Pawn.State != PawnState.Ragdoll)) return false;
            int count = Physics.RaycastNonAlloc(p.BodyPosition + Vector3.up * .15f, Vector3.down, supportHits, 1.5f, ~0, QueryTriggerInteraction.Ignore);
            float distance = float.PositiveInfinity; Collider nearest = null;
            for (int i = 0; i < count; i++)
            {
                if (Array.IndexOf(p.BodyColliders, supportHits[i].collider) >= 0 || supportHits[i].distance >= distance) continue;
                distance = supportHits[i].distance; nearest = supportHits[i].collider;
            }
            return nearest != null && nearest.GetComponentInParent<KingRushSeesawBoard>() == seesawBoard &&
                (p.Pawn.Grounded || distance < .75f);
        }
        void GatherReadyWaves()
        {
            foreach (var rally in rallies)
            {
                if (rally.Rules.Released || !rally.Rules.Ready(Match.Now)) continue;
                var red = RedSection(rally.wave);
                GatheredLast = 0;
                foreach (var p in Players)
                {
                    if (p.Section > red) continue;
                    if (p.Section == red && !p.Captured && !p.Pawn.Floating && rally.arrival.Contains(p.BodyPosition))
                    { progress[p] = RallyCheckpoint(rally.wave); continue; }
                    // Collect both teams, including players still detained in the previous mission.
                    // Clear inbound grips before teleporting, and cancel all obsolete respawn reservations.
                    foreach (var holder in Players) holder.ReleaseHoldOn(p);
                    pendingCapture.Remove(p); Mission.Release(p.Id); wetUntil.Remove(p);
                    if (p.Section < red) p.LeaveBlue();
                    p.Section = red; progress[p] = RallyCheckpoint(rally.wave);
                    Vector3 spot = rally.Slot((int)p.Id - 1);
                    // Keep arrivals in place, without spawning a late body on top of them.
                    for (int attempt = 0; attempt < 48; attempt++)
                    {
                        bool occupied = false;
                        foreach (var other in Players)
                        {
                            Vector3 delta = other.BodyPosition - spot; delta.y = 0;
                            if (other != p && delta.sqrMagnitude < 1.8f) { occupied = true; break; }
                        }
                        if (!occupied) break;
                        spot = new Vector3(rally.arrival.center.x + (attempt % 6 - 2.5f) * 2.5f,
                            rally.arrival.min.y, rally.arrival.center.z + 6 - attempt / 6 * 2);
                    }
                    p.Respawn(spot + Vector3.up * .02f); GatheredLast++;
                }
                // Release is one-shot, and happens only AFTER the whole catch-up operation.
                rally.OpenAfterGather(Match.Now);
            }
            // A thrown body cannot bypass a closed phase gate to start the next mission early.
            foreach (var p in Players)
            {
                if (p.Section == KingRushSection.Red1 && !rallies[0].Rules.Released && Arena.Contains(p.BodyPosition)) Respawn(p);
                if (p.Section == KingRushSection.Red2 && !rallies[1].Rules.Released && secondArena.Contains(p.BodyPosition)) Respawn(p);
                if (p.Section == KingRushSection.Red3 && !rallies[2].Rules.Released && finalBoard.arena.Contains(p.BodyPosition)) Respawn(p);
            }
        }
        void StepCastle(float dt)
        {
            SeesawWeight = 0;
            foreach (var p in Players)
            {
                if (p.Captured) continue;
                bool inside = secondArena.Contains(p.BodyPosition) && !p.Pawn.Floating;
                if (inside && p.Section == KingRushSection.Red2 && rallies[1].Rules.Released) p.Section = KingRushSection.Blue2;
                if (p.Section != KingRushSection.Blue2) continue;
                p.AbilitiesEnabled = inside;
                if (inside) Seesaw.Begin(Match.Now);
                if (!IsSeesawWeight(p)) continue;
                float weight = seesawBoard.Weight(p.BodyPosition); SeesawWeight += weight;
                // Only the raised half becomes slippery. Gripping a statue remains useful.
                if (Mathf.Abs(seesawBoard.Angle) >= 20 && weight * seesawBoard.Angle < 0 && !p.Pawn.Grabbing && !p.Pawn.Climbing)
                {
                    Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, seesawBoard.transform.up) * 4;
                    foreach (var b in p.Pawn.bodies) if (!b.isKinematic) b.AddForce(downhill, ForceMode.Acceleration);
                }
            }
            Seesaw.Advance(Match.Now, SeesawWeight); seesawBoard.SetAngle((float)Seesaw.Angle, dt);
            foreach (var exit in seesawExits)
            {
                exit.Apply(Match, Seesaw.Access(exit.team), Seesaw.Bridges(Match.Now), Local.Team);
                foreach (var p in Players)
                    if (p.Section == KingRushSection.Blue2 && p.Team == exit.team && !p.Captured && !p.Pawn.Floating &&
                        exit.crossing.Contains(p.BodyPosition)) Seesaw.Cross(p.Id, p.Team, Match.Now);
                exit.label.text = (exit.team == 0 ? "백팀" : "흑팀") + $" {Seesaw.Count(exit.team)} / 4\n" +
                    (Seesaw.Access(exit.team) ? "출구로 건너오세요" : "반대편을 무겁게!");
            }
            for (int i = 2; i < gates.Length; i++)
                gates[i].barrier.GetComponent<Renderer>().enabled = !(gates[i].Open && gates[i].team == Local.Team);
        }
        string MissionText(string opening)
        {
            if (Final.Ended || Local.Section == KingRushSection.Final) return FinalText();
            if (!KingRushPieces.IsBlue(Local.Section))
            {
                var r = rallies[RallyWave(Local.Section)].Rules;
                return r.Released ? "전원 집결 완료\n열린 벽을 지나 미션으로" : r.StartedAt < 0 ?
                    $"승격 인원 {r.Claimed} / {r.Required}\n모두 정해지면 10초 카운트다운\n끝나면 양 팀 미도착 인원 자동 합류" :
                    $"출발까지 {Math.Ceiling(r.Remaining(Match.Now)):0}초\n끝나면 양 팀 미도착 인원 자동 합류";
            }
            if (Local.Section == KingRushSection.Blue1) return opening;
            double elapsed = Seesaw.Started ? Match.Now - Seesaw.StartedAt : 0;
            return $"판 뒤집기 · 백팀 {Seesaw.Count(0)}/4 / 흑팀 {Seesaw.Count(1)}/4\n기울기 {Seesaw.Angle:0.0}° · 무게 {SeesawWeight:0.0}\n" +
                (Seesaw.Bridges(Match.Now) ? "구제 다리 열림" : $"미션 {elapsed:0}초 · 120초 완화 / 150초 다리");
        }
        public void ArrangeRally(int wave)
        {
            // Solo fixture: put distinct eligible pawns on pads; real 1.5 s claims and 10 s countdown still run.
            ResetRound(); Select(0);
            var red = RedSection(wave);
            int first = wave == 0 ? 0 : wave == 1 ? 2 : 5;
            Local.Section = red; progress[Local] = RallyCheckpoint(wave);
            for (int i = 0; i < wave + 2; i++)
            {
                var p = i == 0 ? Local : Players[6 + i - 1]; p.Section = red;
                progress[p] = progress[Local]; p.Respawn(pads[first + i].transform.position + Vector3.up * .03f);
            }
            for (int i = 0; i < wave; i++) rallies[i].OpenAfterGather(Match.Now, true);
        }
        public void ArrangeCastle(bool mission)
        {
            ResetRound(); Select(0); rallies[0].OpenAfterGather(Match.Now, true);
            if (mission) rallies[1].OpenAfterGather(Match.Now, true);
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i]; p.Section = mission ? KingRushSection.Blue2 : KingRushSection.Red2; progress[p] = 7;
                // Opposite-side counterweights let a solo tester traverse the seesaw without AI.
                p.Respawn(mission ? i < 7 ? SeesawEntry(i) : new Vector3(9, 0, 404 + (i - 7) * 3) :
                    new Vector3((i < 6 ? -1 : 1) * (2 + i % 3 * 2), 0, 222 + i % 6 / 3 * 2));
            }
        }
    }
}
