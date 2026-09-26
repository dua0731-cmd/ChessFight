using System;
using System.Collections;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Automated checks for the pioneer bells, their opened paths and the checkpoints (Queen of the Hill
    /// M8, M9) on the [7g] pioneer tower: the first team to ring a bell opens the section's lift, which is
    /// that team's alone for 20 s; out of the water a character comes back at the higher of its own
    /// landing and one below its team's best; the host's openings reach a client.
    /// </summary>
    public partial class LabAutoTest
    {
        IEnumerator PioneerChecks()
        {
            var bed = Bed;
            var match = bed != null ? bed.Match : null;
            if (match == null)
            {
                Report("M8 개척의 탑", false, "시험대나 경기(QueenHillMatch)가 없음");
                yield break;
            }
            match.ResetRound();
            yield return PioneerBells(bed, match);
            yield return PioneerExclusive(bed, match);
            yield return PioneerCheckpoints(match);
            PioneerWire(match);
            match.ResetRound();
        }

        RagdollPawn SpawnTeam(Vector3 ground, Vector3 facing, string name, int team)
        {
            var pawn = Spawn(ground, facing, name);
            pawn.Team = team;
            return pawn;
        }

        IEnumerator RingBell(RagdollPawn pawn)
        {
            pawn.SetInput(new PawnInput { interact = true });
            yield return Sim(Dt);
            yield return Sim(0.2f, () => pawn.SetInput(default));
        }

        IEnumerator PioneerBells(QueenHillTestBed bed, QueenHillMatch match)
        {
            Vector3 bell = QueenHillTestBed.BellSpot(1);
            var white = SpawnTeam(bell + new Vector3(-0.9f, 0f, 0f), Vector3.right, "pioneer-white", Teams.White);
            var black = SpawnTeam(bell + new Vector3(-0.9f, 0f, 1.2f), Vector3.right, "pioneer-black", Teams.Black);
            yield return Sim(0.6f);
            bool shutBefore = !match.IsOpen(1) && !bed.PillarLifts[1].gameObject.activeInHierarchy;
            yield return RingBell(white);
            bool opened = match.IsOpen(1) && match.Pioneer(1) == Teams.White;
            yield return Sim(0.1f);
            bool shown = bed.PillarLifts[1].gameObject.activeInHierarchy;
            bool exclusive = match.Exclusive(1);
            yield return RingBell(black);
            bool kept = match.Pioneer(1) == Teams.White;
            // Black rings the second section's bell, on top of the second wall.
            black.Teleport(QueenHillTestBed.BellSpot(2) + new Vector3(-0.9f, black.standHeight + 0.02f, 0f), Vector3.right);
            yield return Sim(0.5f);
            yield return RingBell(black);
            bool second = match.IsOpen(2) && match.Pioneer(2) == Teams.Black;
            Report("M8 종: 먼저 친 팀이 그 구간의 빛의 기둥 칸을 연다 (나중에 친 팀은 못 바꿈)",
                shutBefore && opened && shown && exclusive && kept && second && white.Interactions == 1,
                $"치기 전 닫힘·승강기 숨김 {shutBefore}, 백이 S1 종 → 열림 {opened}·승강기 나타남 {shown}·독점 {exclusive}, "
                + $"흑이 S1 종을 또 쳐도 백 그대로 {kept}, 흑이 S2 종 → 흑이 엶 {second}");
            yield return Clear();
        }

        IEnumerator PioneerExclusive(QueenHillTestBed bed, QueenHillMatch match)
        {
            // S1 was opened by white just now. Put a white and a black pawn on its lift once it is up high.
            Transform lift = bed.PillarLifts[1].transform;
            var white = SpawnTeam(new Vector3(44f, 0f, -12f), Vector3.right, "ride-white", Teams.White);
            var black = SpawnTeam(new Vector3(44f, 0f, -13.5f), Vector3.right, "ride-black", Teams.Black);
            yield return Sim(0.5f);
            IEnumerator WaitHigh()
            {
                for (float t = 0f; t < 15f && lift.position.y < 2.4f; t += Dt) yield return Sim(Dt);
            }
            float LiftTop() => lift.position.y + QueenHillTestBed.PlatformThickness * 0.5f;
            void PutOn(RagdollPawn pawn, float side) =>
                pawn.Teleport(new Vector3(lift.position.x + side, LiftTop() + pawn.standHeight + 0.02f, lift.position.z), Vector3.right);

            yield return WaitHigh();
            bool exclusive = match.Exclusive(1);
            PutOn(white, -0.45f);
            PutOn(black, 0.45f);
            yield return Sim(1f);
            float whiteAbove = white.Hips.position.y - LiftTop();
            float blackAbove = black.Hips.position.y - LiftTop();
            bool whiteRode = whiteAbove > 0.05f && whiteAbove < 0.6f;
            bool blackFell = blackAbove < -1f;

            // After the head start anyone rides.
            float left = match.ExclusiveLeft(1);
            yield return Sim(left + 0.3f);
            bool over = !match.Exclusive(1);
            yield return Sim(0.1f);
            yield return WaitHigh();
            PutOn(black, 0f);
            yield return Sim(1f);
            float blackLater = black.Hips.position.y - LiftTop();
            bool blackRides = blackLater > 0.05f && blackLater < 0.6f;
            Report("M8 독점: 연 팀은 20초 동안 혼자 탄다 (다른 팀은 빠져 떨어짐), 그 뒤에는 누구나",
                exclusive && whiteRode && blackFell && over && blackRides,
                $"독점 중 {exclusive}: 백 승강기 위 {whiteAbove:+0.00;-0.00} m (탐 {whiteRode}), 흑 {blackAbove:+0.00;-0.00} m (빠짐 {blackFell}); "
                + $"{left:F1}초 뒤 독점 끝 {over}, 흑 승강기 위 {blackLater:+0.00;-0.00} m (탐 {blackRides})");
            yield return Clear();
        }

        IEnumerator PioneerCheckpoints(QueenHillMatch match)
        {
            // Now S1 is white's and S2 black's. A black straggler comes back one below black's best (S1);
            // a white pawn that stood on the S1 landing itself comes back there; a white straggler has
            // nothing higher than its bridge (white's best S1, one below = none).
            var blackLag = SpawnTeam(new Vector3(44f, 0f, -12f), Vector3.right, "lag-black", Teams.Black);
            var whiteLag = SpawnTeam(new Vector3(44f, 0f, -13.5f), Vector3.right, "lag-white", Teams.White);
            var climber = SpawnTeam(QueenHillTestBed.LandingSpawn(1), Vector3.right, "landed-white", Teams.White);
            yield return Sim(0.8f);
            var climberDriver = climber.GetComponent<RagdollDriver>();
            int climberBest = climberDriver != null ? match.PersonalBest(climberDriver) : -1;
            float y = QueenHillTestBed.WaterTop + 0.3f;
            blackLag.Teleport(new Vector3(11f, y, -40f), Vector3.forward);
            whiteLag.Teleport(new Vector3(18.5f, y, -40f), Vector3.forward);
            climber.Teleport(new Vector3(11f, y, -43f), Vector3.forward);
            float delay = Bed.Water != null ? Bed.Water.RespawnDelay : 5f;
            yield return Sim(delay + 1f);
            float Off(RagdollPawn pawn, Vector3 spot) => Flat(pawn.Hips.position - spot).magnitude;
            Vector3 s1 = QueenHillTestBed.LandingSpawn(1);
            float blackOff = Off(blackLag, s1), climberOff = Off(climber, s1), whiteOff = Off(whiteLag, QueenHillTestBed.Checkpoint);
            // Two come back to the S1 landing at once: the second takes the next free spot (1 m on).
            bool ok = climberBest == 1 && blackOff < 1.6f && blackLag.Hips.position.y > QueenHillTestBed.SectionRise
                      && climberOff < 1.6f && climber.Hips.position.y > QueenHillTestBed.SectionRise && whiteOff < 1f
                      && Flat(blackLag.Hips.position - climber.Hips.position).magnitude > 0.6f;
            Report("M9 체크포인트: 물에서 나오는 곳 = 내가 선 가장 높은 착지대와 우리 팀 최고 구간 한 칸 아래 중 높은 곳",
                ok,
                $"흑(팀 최고 S2, 본인 없음) → S1 착지대에서 {blackOff:F2} m, 백(S1 착지대에 섰음, 기록 S{climberBest}) → S1 착지대에서 {climberOff:F2} m, "
                + $"백(팀 최고 S1, 본인 없음) → 7e 체크포인트(다리 대신)에서 {whiteOff:F2} m");
            yield return Clear();
        }

        void PioneerWire(QueenHillMatch match)
        {
            string text = match.Encode();
            byte[] bytes = RagdollNetProtocol.Match(55, text);
            bool read = bytes != null && RagdollNetProtocol.ReadMatch(bytes, 55, out string got) && got == text;
            var client = new QueenHillRules(match.Rules.Sections, match.Rules.ExclusiveSeconds);
            bool applied = read && client.Apply(text) && client.Pioneer(1) == match.Pioneer(1) && client.Pioneer(2) == match.Pioneer(2)
                           && Math.Abs(client.OpenedAt(1) - match.Rules.OpenedAt(1)) < 0.002;
            bool otherMatch = bytes != null && !RagdollNetProtocol.ReadMatch(bytes, 56, out _);
            Report("M8 네트워크: 열린 구간(팀·시각)이 방장에서 참가자로 전달됨",
                read && applied && otherMatch,
                $"\"{text}\" ({(bytes != null ? bytes.Length : 0)}바이트), 해독 {read}, 적용 뒤 같음 {applied}, 다른 경기 번호는 거부 {otherMatch}");
        }
    }
}
