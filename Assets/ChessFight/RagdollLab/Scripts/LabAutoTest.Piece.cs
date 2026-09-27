using System.Collections;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Automated checks for promotion and the pieces (Queen of the Hill M11) on the [7j] pedestals: F at a
    /// pedestal promotes a pawn and only a pawn, one king per team; each piece carries DESIGN §6.2's numbers
    /// (the king runs 0.8 times as fast and never sprints, the rook climbs 0.7 times as fast, pushes move a
    /// piece 1 / weight as far, the king shrugs off a squash); the piece reaches a network client.
    /// </summary>
    public partial class LabAutoTest
    {
        IEnumerator PieceChecks()
        {
            var bed = Bed;
            if (bed == null || bed.Pedestals[0] == null)
            {
                Report("M11 승격", false, "시험대나 받침대가 없음");
                yield break;
            }
            yield return Promotions();
            yield return PieceSpeeds();
            yield return PiecePushes();
            yield return PieceClimb();
            yield return PieceWire();
        }

        static Vector3 AtPedestal(RagdollPawn pawn, int i) =>
            QueenHillTestBed.Pedestal(i) + new Vector3(0f, pawn.standHeight + 0.02f, 0.9f);

        IEnumerator PromoteAt(RagdollPawn pawn, int pedestal)
        {
            pawn.Teleport(AtPedestal(pawn, pedestal), Vector3.back);
            yield return Sim(0.4f);
            yield return RingBell(pawn);
        }

        IEnumerator Promotions()
        {
            // Pedestals: 0 rook, 1 bishop, 2 knight, 3 king, 4 back to a pawn.
            var white = SpawnTeam(QueenHillTestBed.Pedestal(0) + new Vector3(0f, 0f, 0.9f), Vector3.back, "promote-white", Teams.White);
            yield return Sim(0.5f);
            yield return RingBell(white);
            bool rook = white.Piece == PieceKind.Rook;
            yield return PromoteAt(white, 2);
            bool stays = white.Piece == PieceKind.Rook;
            yield return PromoteAt(white, 4);
            bool back = white.Piece == PieceKind.Pawn;
            yield return PromoteAt(white, 3);
            bool king = white.Piece == PieceKind.King;
            var second = SpawnTeam(new Vector3(40f, 0f, -88f), Vector3.back, "promote-white2", Teams.White);
            var black = SpawnTeam(new Vector3(43f, 0f, -88f), Vector3.back, "promote-black", Teams.Black);
            yield return Sim(0.4f);
            white.Teleport(new Vector3(52f, white.standHeight + 0.02f, -88f), Vector3.back);
            yield return PromoteAt(second, 3);
            bool oneKing = second.Piece == PieceKind.Pawn;
            // Out of the way first: two pawns put on the same spot throw each other off it.
            second.Teleport(new Vector3(40f, second.standHeight + 0.02f, -88f), Vector3.back);
            yield return PromoteAt(black, 3);
            bool blackKing = black.Piece == PieceKind.King;
            yield return PromoteAt(second, 1);
            bool bishop = second.Piece == PieceKind.Bishop;
            Report("M11 승격: 받침대 앞에서 F → 폰이 룩·비숍·나이트·킹이 됨 (폰만, 킹은 팀에 하나)",
                rook && stays && back && king && oneKing && blackKing && bishop,
                $"폰 → 룩 {rook}, 룩에서 나이트로 안 됨 {stays}, 폰으로 되돌림 {back}, → 킹 {king}, 백 둘째 킹 안 됨 {oneKing}, "
                + $"흑은 킹 됨 {blackKing}, 백 둘째 → 비숍 {bishop}");
            yield return Clear();
        }

        IEnumerator PieceSpeeds()
        {
            var pawn = Spawn(new Vector3(38f, 0f, -101f), Vector3.right, "speed-pawn");
            var king = Spawn(new Vector3(38f, 0f, -103f), Vector3.right, "speed-king");
            king.SetPiece(PieceKind.King);
            yield return Sim(0.5f);
            bool sprinted = false;
            yield return Sim(1.5f, () =>
            {
                pawn.SetInput(new PawnInput { move = Vector3.right });
                king.SetInput(new PawnInput { move = Vector3.right, sprint = true });
                sprinted |= king.Sprinting;
            });
            float vPawn = Flat(pawn.Hips.linearVelocity).magnitude, vKing = Flat(king.Hips.linearVelocity).magnitude;
            float ratio = vKing / Mathf.Max(0.1f, vPawn);
            bool immune = !king.GetComponent<IStatusReceiver>().Squash(1.2f, 1f);
            Report("M11 킹: 이동 0.8배, Shift를 눌러도 전력질주 없음, 찌그러짐 면역",
                Mathf.Abs(ratio - 0.8f) < 0.08f && !sprinted && immune,
                $"폰 {vPawn:F2} m/s, 킹(Shift 누름) {vKing:F2} m/s = {ratio:F2}배, 전력질주 {sprinted}, 찌그러짐 안 먹힘 {immune}");
            yield return Clear();
        }

        IEnumerator PiecePushes()
        {
            PieceKind[] kinds = { PieceKind.Pawn, PieceKind.Rook, PieceKind.King, PieceKind.Knight };
            var pawns = new List<RagdollPawn>();
            for (int i = 0; i < kinds.Length; i++)
            {
                var p = Spawn(new Vector3(60f, 0f, -88f - i * 2.5f), Vector3.back, $"push-{kinds[i]}");
                p.SetPiece(kinds[i]);
                pawns.Add(p);
            }
            yield return Sim(0.6f);
            var from = new List<Vector3>();
            foreach (var p in pawns)
            {
                from.Add(p.Hips.position);
                p.GetComponent<IHitReceiver>().ApplyHit(new Vector3(4f, 0f, 0f), 0f, 0f, false);
            }
            // The push is a change of speed, divided by the weight (an impulse on a heavier body). The distance
            // goes with its square, as the feet brake it.
            var peak = new float[pawns.Count];
            yield return Sim(0.1f, () =>
            {
                for (int i = 0; i < pawns.Count; i++) peak[i] = Mathf.Max(peak[i], pawns[i].Hips.linearVelocity.x);
            });
            yield return Sim(0.5f);
            var moved = new float[pawns.Count];
            for (int i = 0; i < pawns.Count; i++) moved[i] = pawns[i].Hips.position.x - from[i].x;
            float Share(int i) => peak[i] / Mathf.Max(0.01f, peak[0]);
            bool ok = peak[0] > 3f && Mathf.Abs(Share(1) - 1f / 1.5f) < 0.08f && Mathf.Abs(Share(2) - 1f / 3f) < 0.06f
                      && Mathf.Abs(Share(3) - 1.25f) < 0.1f;
            Report("M11 무게: 같은 4 m/s 피격이 주는 속도가 룩(1.5)은 2/3, 킹(3)은 1/3, 나이트(0.8)는 1.25배",
                ok,
                $"최고 속도 폰 {peak[0]:F2}, 룩 {peak[1]:F2} ({Share(1):F2}배), 킹 {peak[2]:F2} ({Share(2):F2}배), 나이트 {peak[3]:F2} m/s ({Share(3):F2}배); "
                + $"0.6초에 밀린 거리 {moved[0]:F2} / {moved[1]:F2} / {moved[2]:F2} / {moved[3]:F2} m");
            yield return Clear();
        }

        IEnumerator PieceClimb()
        {
            RagdollPawn pawn = null, rook = null;
            yield return StartOnWall("climb-pawn", LabLayout.WallZ[2], x => pawn = x);
            yield return StartOnWall("climb-rook", LabLayout.WallZ[2] - 1.3f, x => rook = x);
            rook.SetPiece(PieceKind.Rook);
            bool both = pawn.Climbing && rook.Climbing;
            float p0 = pawn.Hips.position.y, r0 = rook.Hips.position.y;
            yield return Sim(1.2f, () =>
            {
                ClimbInput(pawn, Vector3.right, Vector3.right);
                ClimbInput(rook, Vector3.right, Vector3.right);
            });
            float pr = pawn.Hips.position.y - p0, rr = rook.Hips.position.y - r0;
            float ratio = rr / Mathf.Max(0.01f, pr);
            Report("M11 룩: 벽 등반 0.7배 속도",
                both && pr > 0.5f && Mathf.Abs(ratio - 0.7f) < 0.12f,
                $"1.2초 W: 폰 {pr:F2} m, 룩 {rr:F2} m = {ratio:F2}배");
            yield return Clear();
        }

        IEnumerator PieceWire()
        {
            var host = Spawn(new Vector3(66f, 0f, -90f), Vector3.back, "piece-net-host");
            var remote = Spawn(new Vector3(66f, 0f, -96f), Vector3.back, "piece-net-remote");
            remote.SetNetworkPuppet(true);
            host.SetPiece(PieceKind.Knight);
            yield return Sim(0.4f);
            var pose = new RagdollPose { id = 1 };
            host.CaptureNetworkPose(pose);
            byte[] bytes = RagdollNetProtocol.Snapshot(78, 1, 1, new List<RagdollPose> { pose });
            var snapshot = new RagdollSnapshot();
            bool parsed = RagdollNetProtocol.ReadSnapshot(bytes, 78, snapshot);
            if (parsed) remote.ApplyNetworkPose(snapshot.At(0));
            bool sized = bytes != null && bytes.Length == RagdollNetProtocol.SnapshotBytes(1);
            Report("M11 네트워크: 기물 종류가 스냅샷으로 참가자에게 전달 (폰당 74바이트)",
                parsed && sized && remote.Piece == PieceKind.Knight,
                $"해독 {parsed}, {(bytes != null ? bytes.Length : 0)}바이트(예상 {RagdollNetProtocol.SnapshotBytes(1)}), 참가자 쪽 기물 {ChessPieces.Name(remote.Piece)}");
            yield return Clear();
        }
    }
}
