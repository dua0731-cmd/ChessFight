using System.Collections;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Automated checks for the pieces' abilities (Queen of the Hill M12) on the [7k] floor: the knight's L onto
    /// a 6 m wall and its stomp, the rook's charge through two enemies and past a teammate and its charge up a
    /// 7.5 m wall, the king's shockwave (4 m, not 6 m), castling and the king's grace, the bishop's hover with
    /// its stones and its glide, and the move reaching a network client.
    /// </summary>
    public partial class LabAutoTest
    {
        IEnumerator AbilityChecks()
        {
            if (Bed == null)
            {
                Report("M12 기물 능력", false, "시험대가 없음");
                yield break;
            }
            yield return KnightLeapCheck();
            yield return KnightStompCheck();
            yield return RookChargeCheck();
            yield return RookWallCheck();
            yield return KingShockwaveCheck();
            yield return CastleCheck();
            yield return GraceCheck();
            yield return HoverCheck();
            yield return GlideCheck();
            yield return AbilityWire();
        }

        RagdollPawn SpawnPiece(Vector3 ground, Vector3 facing, string name, int team, PieceKind kind)
        {
            var pawn = SpawnTeam(ground, facing, name, team);
            pawn.SetPiece(kind);
            return pawn;
        }

        /// <summary>One press of E (or Q) along an aim, then hands off.</summary>
        IEnumerator UseAbility(RagdollPawn pawn, Vector3 aim, bool second = false)
        {
            pawn.SetInput(new PawnInput { ability = !second, ability2 = second, aim = aim });
            yield return Sim(Dt);
            pawn.SetInput(new PawnInput { aim = aim });
        }

        IEnumerator KnightLeapCheck()
        {
            var knight = SpawnPiece(QueenHillTestBed.KnightSpot, Vector3.right, "leap-knight", Teams.White, PieceKind.Knight);
            yield return Sim(0.6f);
            yield return UseAbility(knight, Vector3.right);
            Vector3 marker = knight.MoveTarget;
            bool moving = knight.Move == PieceMove.KnightLeap;
            yield return Sim(2.5f, () => knight.SetInput(new PawnInput { aim = Vector3.right }));
            float top = QueenHillTestBed.KnightWallCenter.y + QueenHillTestBed.KnightWallSize.y * 0.5f;
            Vector3 hips = knight.Hips.position;
            bool onTop = hips.y > top + 0.1f && hips.x > 42.2f && knight.State == PawnState.Active;
            bool markerOnTop = Mathf.Abs(marker.y - top) < 0.15f;
            float cool = knight.CooldownLeft(0);
            Report("M12 나이트: 6 m 벽 1.5 m 앞에서 E → L자로 벽 위에 섬, 착지 표시가 벽 위, 쿨 5초",
                moving && onTop && markerOnTop && cool > 1.5f && cool < 3f,
                $"도약 시작 {moving}, 끝 위치 ({hips.x:F1}, {hips.y:F2}) 벽 위 {top:F1} m, 상태 {knight.State}, 착지 표시 높이 {marker.y:F2}, 남은 쿨 {cool:F1}초");
            yield return Clear();
        }

        IEnumerator KnightStompCheck()
        {
            var black = SpawnTeam(new Vector3(50f, 0f, -124f), Vector3.left, "stomp-black", Teams.Black);
            var white = SpawnTeam(new Vector3(54f, 0f, -124f), Vector3.left, "stomp-white", Teams.White);
            var knight = SpawnPiece(new Vector3(47f, 0f, -124f), Vector3.right, "stomp-knight", Teams.White, PieceKind.Knight);
            yield return Sim(0.6f);
            knight.Teleport(black.Hips.position + Vector3.up * 2.4f, Vector3.right);
            bool squashed = false;
            yield return Sim(1.5f, () => squashed |= black.Squashed);
            int stomps = knight.Stomps;
            // A teammate's head is only a floor.
            knight.Teleport(white.Hips.position + Vector3.up * 2.4f, Vector3.right);
            bool friend = false;
            yield return Sim(1.5f, () => friend |= white.Squashed);
            Report("M12 나이트 밟기: 적 머리 위에 내려앉으면 찌그러짐 1.2초, 같은 팀은 안 됨",
                squashed && stomps == 1 && !friend && knight.Stomps == 1,
                $"적 찌그러짐 {squashed}, 밟기 {stomps} → {knight.Stomps}, 같은 팀 찌그러짐 {friend}");
            yield return Clear();
        }

        IEnumerator RookChargeCheck()
        {
            Vector3 start = QueenHillTestBed.AbilityStart;
            var rook = SpawnPiece(start, Vector3.right, "charge-rook", Teams.White, PieceKind.Rook);
            var ally = SpawnTeam(start + new Vector3(5f, 0f, -0.3f), Vector3.left, "charge-ally", Teams.White);
            var a = SpawnTeam(start + new Vector3(8f, 0f, 0.3f), Vector3.left, "charge-black1", Teams.Black);
            var b = SpawnTeam(start + new Vector3(11f, 0f, -0.4f), Vector3.left, "charge-black2", Teams.Black);
            yield return Sim(0.8f);
            Vector3 from = rook.Hips.position, aFrom = a.Hips.position, bFrom = b.Hips.position;
            // Aimed a little off the axis: the charge still runs along the board.
            yield return UseAbility(rook, new Vector3(0.95f, 0.1f, 0.3f).normalized);
            bool windup = rook.Move == PieceMove.RookWindup;
            float still = 0f;
            yield return Sim(PieceAbilities.RookWindup * 0.8f, () => still = Mathf.Max(still, Flat(rook.Hips.position - from).magnitude));
            yield return Sim(1.6f);
            Vector3 to = rook.Hips.position;
            float along = to.x - from.x, off = Mathf.Abs(to.z - from.z);
            bool knocked = a.Knockdowns > 0 && b.Knockdowns > 0;
            float aside = Mathf.Max(Mathf.Abs(a.Hips.position.z - aFrom.z), Mathf.Abs(b.Hips.position.z - bFrom.z));
            Report("M12 룩 돌진: 0.6초 경고선 → 판의 축을 따라 14 m, 경로의 적 둘 넘어뜨리고 옆으로, 같은 팀은 통과",
                windup && still < 0.3f && along > 11f && along < 16.5f && off < 0.6f && knocked && ally.Knockdowns == 0 && rook.RookHits == 2,
                $"준비 {windup}(그동안 {still:F2} m 움직임), 이동 {along:F1} m (옆 {off:F2} m), 적 넘어짐 {a.Knockdowns}/{b.Knockdowns}, "
                + $"적이 옆으로 {aside:F1} m, 같은 팀 넘어짐 {ally.Knockdowns}, 적중 {rook.RookHits}");
            yield return Clear();
        }

        IEnumerator RookWallCheck()
        {
            var rook = SpawnPiece(QueenHillTestBed.RookWallSpot, Vector3.right, "wall-rook", Teams.White, PieceKind.Rook);
            yield return Sim(0.8f);
            yield return UseAbility(rook, new Vector3(0.3f, 0.95f, 0f).normalized);
            yield return Sim(2.5f);
            float top = QueenHillTestBed.RookWallCenter.y + QueenHillTestBed.RookWallSize.y * 0.5f;
            Vector3 hips = rook.Hips.position;
            Report("M12 룩 수직 돌진: 벽 앞에서 위를 보고 E → 7.5 m 벽을 타고 올라 위에 섬",
                hips.y > top + 0.1f && hips.x > 42.2f && rook.State == PawnState.Active,
                $"끝 위치 ({hips.x:F1}, {hips.y:F2}), 벽 위 {top:F1} m, 상태 {rook.State}, 마지막: {rook.LastAbilityEvent}");
            yield return Clear();
        }

        IEnumerator KingShockwaveCheck()
        {
            var king = SpawnPiece(new Vector3(60f, 0f, -112f), Vector3.right, "check-king", Teams.White, PieceKind.King);
            var near = SpawnTeam(new Vector3(62.5f, 0f, -112f), Vector3.left, "check-near", Teams.Black);
            var far = SpawnTeam(new Vector3(66f, 0f, -112f), Vector3.left, "check-far", Teams.Black);
            yield return Sim(0.8f);
            Vector3 nearFrom = near.Hips.position;
            yield return UseAbility(king, Vector3.right);
            bool raised = king.Move == PieceMove.KingCheck;
            bool early = false;
            yield return Sim(PieceAbilities.CheckWindup * 0.8f, () => early |= near.Knockdowns > 0);
            yield return Sim(1.5f);
            float moved = Flat(near.Hips.position - nearFrom).magnitude;
            Report("M12 킹 체크!: 0.5초 뒤 반경 4 m 적을 넘어뜨리고 밀어냄, 6 m는 안 닿음",
                raised && !early && near.Knockdowns > 0 && far.Knockdowns == 0 && king.CheckHits == 1 && moved > 1.5f,
                $"홀 듦 {raised}, 준비 중 넘어짐 {early}, 2.5 m 적 넘어짐 {near.Knockdowns}(밀림 {moved:F1} m), 6 m 적 {far.Knockdowns}, 적중 {king.CheckHits}");
            yield return Clear();
        }

        IEnumerator CastleCheck()
        {
            var king = SpawnPiece(new Vector3(38f, 0f, -124f), Vector3.right, "castle-king", Teams.White, PieceKind.King);
            var rook = SpawnPiece(new Vector3(72f, 0f, -106f), Vector3.left, "castle-rook", Teams.White, PieceKind.Rook);
            var enemyRook = SpawnPiece(new Vector3(40f, 0f, -118f), Vector3.left, "castle-enemy", Teams.Black, PieceKind.Rook);
            yield return Sim(0.6f);
            Vector3 k0 = king.Hips.position, r0 = rook.Hips.position, e0 = enemyRook.Hips.position;
            yield return UseAbility(king, Vector3.right, true);
            yield return Sim(0.5f);
            bool swapped = Flat(king.Hips.position - r0).magnitude < 0.8f && Flat(rook.Hips.position - k0).magnitude < 0.8f;
            bool enemyStays = Flat(enemyRook.Hips.position - e0).magnitude < 0.5f;
            float cool = king.CooldownLeft(1);
            Report("M12 킹 캐슬링: Q → 가장 가까운 같은 팀 룩과 자리를 맞바꿈(적 룩은 아님), 쿨 20초",
                swapped && enemyStays && king.Castles == 1 && cool > 19f,
                $"맞바꿈 {swapped}, 적 룩 그대로 {enemyStays}, 캐슬링 {king.Castles}, 남은 쿨 {cool:F1}초");
            yield return Clear();
        }

        IEnumerator GraceCheck()
        {
            var king = SpawnPiece(new Vector3(60f, 0f, -118f), Vector3.right, "grace-king", Teams.White, PieceKind.King);
            var near = SpawnTeam(new Vector3(62f, 0f, -119f), Vector3.back, "grace-near", Teams.White);
            var far = SpawnTeam(new Vector3(69f, 0f, -119f), Vector3.back, "grace-far", Teams.White);
            yield return Sim(0.8f);
            bool inNear = near.InKingsGrace, inFar = far.InKingsGrace;
            near.GetComponent<IHitReceiver>().ApplyHit(new Vector3(0f, 0f, -4f), 0f, 0f, false);
            far.GetComponent<IHitReceiver>().ApplyHit(new Vector3(0f, 0f, -4f), 0f, 0f, false);
            float pNear = 0f, pFar = 0f;
            yield return Sim(0.1f, () =>
            {
                pNear = Mathf.Max(pNear, -near.Hips.linearVelocity.z);
                pFar = Mathf.Max(pFar, -far.Hips.linearVelocity.z);
            });
            float ratio = pNear / Mathf.Max(0.1f, pFar);
            Report("M12 킹의 가호: 반경 6 m 같은 팀은 가호(무게 1.3배 → 덜 밀림), 9 m는 아님",
                inNear && !inFar && ratio < 0.9f && king.InKingsGrace == false,
                $"2 m 가호 {inNear}, 9 m 가호 {inFar}, 같은 4 m/s에 최고 속도 {pNear:F2} / {pFar:F2} = {ratio:F2}배(예상 0.77)");
            yield return Clear();
        }

        IEnumerator HoverCheck()
        {
            var bishop = SpawnPiece(new Vector3(56f, 0f, -112f), Vector3.right, "hover-bishop", Teams.White, PieceKind.Bishop);
            var target = SpawnTeam(new Vector3(62f, 0f, -112f), Vector3.left, "hover-target", Teams.Black);
            yield return Sim(0.8f);
            float y0 = bishop.Hips.position.y;
            yield return UseAbility(bishop, Vector3.right);
            bool hovering = bishop.Move == PieceMove.BishopHover;
            yield return Sim(1f, () => bishop.SetInput(new PawnInput { aim = Vector3.right }));
            float rise = bishop.Hips.position.y - y0;
            Vector3 aim = (target.bodies[(int)BodyId.Chest].position - bishop.bodies[(int)BodyId.Chest].position).normalized;
            bishop.SetInput(new PawnInput { shove = true, aim = aim });
            yield return Sim(Dt);
            int thrown = bishop.ShardsThrown;
            bool staggered = false;
            yield return Sim(1.5f, () =>
            {
                bishop.SetInput(new PawnInput { aim = aim });
                staggered |= target.Staggered;
            });
            yield return Sim(3.2f, () => bishop.SetInput(new PawnInput { aim = aim }));
            bool ended = bishop.Move == PieceMove.None;
            float cool = bishop.CooldownLeft(0);
            Report("M12 비숍 호버: E → 약 1 m 떠올라 5초, 좌클릭 돌조각이 적을 비틀게 함, 끝난 뒤 쿨 9초",
                hovering && rise > 0.6f && rise < 1.6f && thrown == 1 && target.ShardHitsTaken >= 1 && staggered && ended && cool > 7f && cool < 9.01f,
                $"호버 {hovering}, 1초 뒤 {rise:F2} m 위, 돌조각 {thrown}, 적이 맞음 {target.ShardHitsTaken}(비틀 {staggered}), 끝남 {ended}, 남은 쿨 {cool:F1}초");
            yield return Clear();
        }

        IEnumerator GlideCheck()
        {
            var bishop = SpawnPiece(new Vector3(50f, 0f, -121f), Vector3.right, "glide-bishop", Teams.White, PieceKind.Bishop);
            yield return Sim(0.6f);
            bishop.Teleport(new Vector3(50f, 6.3f, -121f), Vector3.right);
            yield return Sim(0.15f);
            bishop.SetInput(new PawnInput { jump = true });
            yield return Sim(Dt);
            bool gliding = bishop.Move == PieceMove.BishopGlide;
            Vector3 from = bishop.Hips.position;
            yield return Sim(2f);
            Vector3 to = bishop.Hips.position;
            float on = to.x - from.x, drop = from.y - to.y;
            Report("M12 비숍 대각 활공: 공중에서 Space → 45° 아래로 미끄러져 내려가 착지",
                gliding && bishop.Move == PieceMove.None && bishop.Grounded && on > 4f && drop > 5f,
                $"활공 시작 {gliding}, 앞으로 {on:F1} m, 아래로 {drop:F1} m, 착지 {bishop.Grounded}, 마지막: {bishop.LastAbilityEvent}");
            yield return Clear();
        }

        IEnumerator AbilityWire()
        {
            var host = SpawnPiece(new Vector3(66f, 0f, -120f), Vector3.right, "wire-rook", Teams.White, PieceKind.Rook);
            var remote = SpawnTeam(new Vector3(66f, 0f, -124f), Vector3.right, "wire-remote", Teams.White);
            yield return Sim(0.6f);
            remote.SetNetworkPuppet(true);
            yield return UseAbility(host, Vector3.right);
            var pose = new RagdollPose { id = 1 };
            host.CaptureNetworkPose(pose);
            byte[] bytes = RagdollNetProtocol.Snapshot(79, 1, 1, new List<RagdollPose> { pose });
            var snapshot = new RagdollSnapshot();
            bool parsed = RagdollNetProtocol.ReadSnapshot(bytes, 79, snapshot);
            if (parsed) remote.ApplyNetworkPose(snapshot.At(0));
            float error = parsed ? Vector3.Distance(remote.MoveTarget, host.MoveTarget) : 99f;
            Report($"M12 네트워크: 능력의 움직임과 목표가 스냅샷으로 참가자에게 (폰당 {RagdollNetProtocol.PoseBytes}바이트)",
                parsed && remote.Move == PieceMove.RookWindup && error < 0.1f,
                $"해독 {parsed}, 참가자 쪽 {remote.Move}, 목표 오차 {error * 100f:F1} cm");
            remote.SetNetworkPuppet(false);
            yield return Clear();
        }
    }
}
