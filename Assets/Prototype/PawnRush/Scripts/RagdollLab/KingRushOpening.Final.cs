using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class KingRushOpening
    {
        public KingRushFinalBoard finalBoard;
        public KingRushFinalRules Final { get; private set; }
        readonly Dictionary<KingRushPawn, double> finalFalls = new Dictionary<KingRushPawn, double>();
        readonly HashSet<KingRushPawn> eliminated = new HashSet<KingRushPawn>();
        bool resultShown;
        public bool Eliminated(KingRushPawn p) => eliminated.Contains(p);
        public bool ReentryPending(KingRushPawn p) => finalFalls.ContainsKey(p);
        public int Reentries { get; private set; }
        public static int RallyWave(KingRushSection section) => section >= KingRushSection.Red3 ? 2 : section >= KingRushSection.Red2 ? 1 : 0;
        public static KingRushSection RedSection(int wave) => wave == 0 ? KingRushSection.Red1 : wave == 1 ? KingRushSection.Red2 : KingRushSection.Red3;
        public static int RallyCheckpoint(int wave) => wave == 0 ? 6 : wave == 1 ? 11 : 15;
        public static int CheckpointWave(int index) => index < 7 ? 0 : index < 12 ? 1 : 2;
        public static Vector3 FinalEntry(int index) => new Vector3((index < 6 ? -1 : 1) * (5 + index % 3 * 2), 40, 664 + index % 6 / 3 * 2);
        void StepFinal(float dt)
        {
            int seats = 0, blocked = 0, fallen = 0;
            foreach (var p in Players)
            {
                bool inside = finalBoard.arena.Contains(p.BodyPosition) && !p.Pawn.Floating;
                if (p.Section == KingRushSection.Red3 && inside && rallies[2].Rules.Released) p.Section = KingRushSection.Final;
                if (p.Section == KingRushSection.Red3 && p.Pawn.Grounded && p.BodyPosition.x < -7 && p.BodyPosition.z > 452 && p.BodyPosition.z < 540)
                {
                    // A mild, readable crosswind on the optional narrow shortcut only.
                    Vector3 wind = Vector3.right * (Mathf.Sin((float)(ObstacleClock.Now % 8) * Mathf.PI / 4) * .6f);
                    foreach (var body in p.Pawn.bodies) if (!body.isKinematic) body.AddForce(wind, ForceMode.Acceleration);
                }
                if (p.Section != KingRushSection.Final) continue;
                p.AbilitiesEnabled = inside && !Final.Ended && !eliminated.Contains(p) && !finalFalls.ContainsKey(p);
                bool dropped = p.BodyPosition.y < 32 || p.Pawn.Floating || finalFalls.ContainsKey(p) || eliminated.Contains(p);
                if (p.FixedKing && dropped) fallen |= 1 << p.Team;
                if (!p.CountsAsBody || dropped) continue;
                if (p.FixedKing && inside && p.Pawn.Grounded) Final.ArriveKing(p.Team, Match.Now);
                if (finalBoard.dais.Contains(p.BodyPosition)) blocked |= 1 << (1 - p.Team);
                if (p.FixedKing && p.Pawn.Grounded && p.Pawn.State == PawnState.Active && !p.Pawn.Climbing && finalBoard.throne.Contains(p.BodyPosition)) seats |= 1 << p.Team;
            }
            Final.Advance(Match.Now, seats, blocked, fallen);
            finalBoard.Sample(Final, Match.Now);
        }
        void ResetFinal()
        {
            Final = new KingRushFinalRules(); finalFalls.Clear(); eliminated.Clear(); resultShown = false; Reentries = 0;
            finalBoard.crown.gameObject.SetActive(false); finalBoard.Sample(Final, Match.Now);
        }
        void UpdateFinal()
        {
            foreach (var p in Players)
            {
                if (p.Section != KingRushSection.Final || eliminated.Contains(p)) continue;
                bool fallen = p.BodyPosition.y < 32 || p.Pawn.Floating || !p.Pawn.IsFinite();
                if (fallen && !finalFalls.ContainsKey(p) && !Final.Ended)
                {
                    foreach (var holder in Players) holder.ReleaseHoldOn(p);
                    wetUntil.Remove(p); finalFalls[p] = Match.Now + 5; p.SetCaptured();
                }
                if (!finalFalls.TryGetValue(p, out double due)) continue;
                if (Final.Sudden(Match.Now) || Final.Ended)
                { eliminated.Add(p); p.Respawn(finalBoard.islands[(int)(p.Id - 1) % 4].position); p.SetCaptured(); finalFalls.Remove(p); continue; }
                if (Match.Now < due) continue;
                var island = finalBoard.islands[(int)(p.Id - 1) % 4].position;
                var target = finalBoard.ReentryTarget(island, Final, Match.Now);
                p.Respawn(island); p.LaunchFromCannon(LaunchPad.ArcVelocity(p.LaunchOrigin, target, 3, -Physics.gravity.y), false);
                finalFalls.Remove(p); Reentries++;
            }
            if (!Final.Ended || resultShown) return;
            resultShown = true;
            foreach (var p in Players)
            {
                foreach (var holder in Players) holder.ReleaseHoldOn(p);
                p.CancelAbility(); p.SetCaptured();
                if (Final.Winner >= 0 && p.FixedKing && p.Team != Final.Winner)
                    p.HitReceiver.ApplyHit(Vector3.right * 2, 8, 0, true);
            }
        }
        void DrawFinalCrown()
        {
            bool won = Final.Ended && Final.Winner >= 0;
            finalBoard.crown.gameObject.SetActive(won);
            if (won) finalBoard.crown.position = Players[Final.Winner * 6 + 5].BodyPosition + Vector3.up * 1.3f;
        }
        string FinalText()
        {
            if (Final.Ended) return (Final.Winner < 0 ? "무승부" : Final.Winner == 0 ? "백팀 승리 · 체크메이트!" : "흑팀 승리 · 체크메이트!") +
                $"\n{Final.Reason}\nF3 다시 시작 · Esc → Backspace 로비";
            if (!Final.Started) return Final.FirstArrival < 0 ? "킹의 결승 도착을 기다리는 중\n두 킹 도착 또는 첫 킹 +25초에 시작" :
                $"결승 시작 대기 · {System.Math.Max(0, 25 - (Match.Now - Final.FirstArrival)):0}초\n두 킹이 모두 도착하면 즉시 시작";
            return $"왕관 백 {Final.Progress(0):0%} / 흑 {Final.Progress(1):0%}\n{Final.Elapsed(Match.Now):0} / 180초 · 남은 칸 {finalBoard.Remaining}\n" +
                (Final.Sudden(Match.Now) ? "초읽기! · 게이지 2배 / 킹 추락 즉시 패배" : finalBoard.Warnings > 0 ? "붉은 칸 붕괴 예고!" : "적 단상 점유 시 정지 · 8초 누적");
        }
        public void ArrangeFinal(bool mission)
        {
            ResetRound(); Select(mission ? 5 : 0);
            rallies[0].OpenAfterGather(Match.Now, true); rallies[1].OpenAfterGather(Match.Now, true);
            if (mission) rallies[2].OpenAfterGather(Match.Now, true);
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i]; p.Section = mission ? KingRushSection.Final : KingRushSection.Red3; progress[p] = 12;
                p.Respawn(mission ? FinalEntry(i) : new Vector3((i < 6 ? -1 : 1) * (2 + i % 3 * 2), 3, 446 + i % 6 / 3 * 2));
            }
            CameraRig.yaw = 0;
        }
    }
}
