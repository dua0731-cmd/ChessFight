using System.Collections;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Automated checks for the knight (Queen of the Hill M12): E is the L-jump, 6 m up and 3 m on onto the
    /// [7h] ledge, where the marker said, then 5 s before the next; coming down on an enemy's head squashes
    /// it and bounces the knight, an ally's does not; and what stays the pawn's (the hook, the bells).
    /// </summary>
    public partial class LabAutoTest
    {
        IEnumerator KnightChecks()
        {
            yield return KnightJumpCheck();
            yield return KnightStomp();
            yield return PawnOnly();
        }

        static void AbilityInput(RagdollPawn pawn, Vector3 aim, bool ability = false) =>
            pawn.SetInput(new PawnInput { aim = aim, ability = ability });

        IEnumerator KnightJumpCheck()
        {
            // In front of the [7h] ledge, level with the L-pad but 2 m aside of it.
            Vector3 start = QueenHillTestBed.PadCenter + new Vector3(0f, 0f, -2f);
            Vector3 spot = start + new Vector3(3f, QueenHillTestBed.PadThrow.y, 0f);
            var knight = Spawn(start, Vector3.right, "knight");
            knight.SetPiece(PieceKind.Knight);
            yield return Sim(0.6f);
            AbilityInput(knight, Vector3.right, true);
            yield return Sim(Dt);
            bool jumped = knight.KnightJumps == 1, shown = knight.KnightLandingShown;
            Vector3 marked = knight.KnightLanding;
            // A second E in the air: nothing (the cooldown).
            AbilityInput(knight, Vector3.right, true);
            yield return Sim(Dt);
            float t = 0f, landAt = -1f;
            for (; t < 4f && (landAt < 0f || t - landAt < 0.8f); t += Dt)
                yield return Sim(Dt, () =>
                {
                    AbilityInput(knight, Vector3.right);
                    if (landAt < 0f && t > 0.3f && !knight.Launched) landAt = t;
                });
            Vector3 feet = knight.Hips.position - Vector3.up * knight.standHeight;
            float off = Flat(feet - spot).magnitude, markOff = Flat(feet - marked).magnitude;
            bool onLedge = knight.Hips.position.y > spot.y && knight.State == PawnState.Active;
            bool once = knight.KnightJumps == 1, hidden = !knight.KnightLandingShown;
            float cooldown = knight.AbilityCooldown;
            yield return Sim(cooldown + 0.1f, () => AbilityInput(knight, Vector3.back));
            AbilityInput(knight, Vector3.back, true);
            yield return Sim(Dt);
            bool again = knight.KnightJumps == 2;
            Report("M12 나이트 L자 도약: E로 위 6 m·앞 3 m 선반에 내림(±0.5 m), 착지 표시가 맞음, 쿨 5초",
                jumped && shown && onLedge && off < 0.5f && markOff < 0.5f && once && hidden && again,
                $"도약 {jumped}, 표시 {shown}, 선반 위에 섬 {onLedge}: 목표에서 {off:F2} m, 표시에서 {markOff:F2} m, "
                + $"공중에서 E 다시 → 안 됨 {once}, 내린 뒤 표시 사라짐 {hidden}, 남은 쿨 {cooldown:F1}초 뒤 다시 됨 {again}");
            yield return Sim(3f);
            yield return Clear();
        }

        IEnumerator KnightStomp()
        {
            Vector3 floor = new Vector3(66f, 0f, -60f);
            var enemy = SpawnTeam(floor, Vector3.right, "stomp-enemy", Teams.Black);
            var ally = SpawnTeam(floor + new Vector3(0f, 0f, -3.5f), Vector3.right, "stomp-ally", Teams.White);
            var knight = SpawnTeam(floor + new Vector3(-3f, 0f, 0f), Vector3.right, "stomp-knight", Teams.White);
            knight.SetPiece(PieceKind.Knight);
            yield return Sim(0.6f);
            IEnumerator DropOn(RagdollPawn victim)
            {
                Vector3 head = victim.bodies[(int)BodyId.Head].position;
                knight.Teleport(head + Vector3.up * (1.2f + knight.standHeight), Vector3.right);
                yield return Sim(0.8f, () => knight.SetInput(default));
            }
            yield return DropOn(enemy);
            bool squashed = enemy.Squashed && enemy.Squashes == 1 && knight.Stomps == 1;
            yield return Sim(1.5f);
            yield return DropOn(ally);
            bool spared = ally.Squashes == 0 && knight.Stomps == 1;
            Report("M12 나이트 밟기: 적 머리에 내려앉으면 적이 찌그러짐, 아군은 안 됨",
                squashed && spared,
                $"흑 머리에 떨어짐 → 찌그러짐 {squashed} (밟기 {knight.Stomps}회), 백(아군) 머리 → 안 찌그러짐 {spared}");
            yield return Clear();
        }

        IEnumerator PawnOnly()
        {
            var bed = Bed;
            var match = bed != null ? bed.Match : null;
            var rook = SpawnTeam(QueenHillTestBed.BellSpot(1) + new Vector3(-0.9f, 0f, 0f), Vector3.right, "rook-bell", Teams.White);
            rook.SetPiece(PieceKind.Rook);
            if (match != null) match.ResetRound();
            yield return Sim(0.6f);
            AbilityInput(rook, Vector3.right, true);
            yield return Sim(0.2f, () => AbilityInput(rook, Vector3.right));
            bool noHook = rook.Hook == HookPhase.None;
            yield return RingBell(rook);
            bool noBell = match != null && !match.IsOpen(1);
            Report("M12 폰만: 승격한 기물은 E로 갈고리를 꺼내지 않고, 종을 못 침",
                noHook && noBell,
                $"룩이 E → 갈고리 없음 {noHook}, S1 종 앞에서 F → 안 열림 {noBell}");
            if (match != null) match.ResetRound();
            yield return Clear();
        }
    }
}
