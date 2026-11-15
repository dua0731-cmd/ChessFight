using System.Collections;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R664 (승규 님 11-15: two pawns of one team stuck together with the grip ring on one): in Pawn Rush a hand never
    /// takes hold of a teammate (PawnHand, RagdollPawn.PawnRushTeammate). The same reach as GrabDrag, three ways:
    ///   Pawn Rush, same team       no grip, never held, and the HUD finds no hand on either
    ///   Pawn Rush, other team      caught as before
    ///   not Pawn Rush, same team   caught as before (other modes keep their rules)
    /// Run alone: RagdollLab.exe -batchmode -nographics -ragdollAutoTest r.txt -ragdollOnly TeamGrab
    /// </summary>
    public partial class LabAutoTest
    {
        IEnumerator TeamGrab()
        {
            yield return TeamGrabCase("폰 러쉬 같은 팀은 안 잡힘", rush: true, Teams.White, Teams.White, expectCaught: false);
            yield return TeamGrabCase("폰 러쉬 상대 팀은 잡힘", rush: true, Teams.White, Teams.Black, expectCaught: true);
            yield return TeamGrabCase("폰 러쉬 밖 같은 팀은 전처럼 잡힘", rush: false, Teams.White, Teams.White, expectCaught: true);
        }

        IEnumerator TeamGrabCase(string name, bool rush, int teamA, int teamB, bool expectCaught)
        {
            var a = rush ? SpawnRush(new Vector3(-8f, 0f, 3f), Vector3.forward, "grabber")
                         : Spawn(new Vector3(-8f, 0f, 3f), Vector3.forward, "grabber");
            var b = rush ? SpawnRush(new Vector3(-8f, 0f, 3.75f), Vector3.back, "held")
                         : Spawn(new Vector3(-8f, 0f, 3.75f), Vector3.back, "held");
            a.Team = teamA;
            b.Team = teamB;
            yield return Sim(0.8f);
            Drive(a, Vector3.forward * 0.25f, grab: true);
            float held = 0f;
            bool holdingPawn = false, hudHeld = false;
            yield return Sim(2f, () =>
            {
                if (b.BeingHeld) held += Dt;
                holdingPawn |= a.HoldingPawn(out _);
                hudHeld |= b.HeldGrip(out _);
            });
            bool caught = held > 0.2f && holdingPawn;
            bool clean = !expectCaught ? held == 0f && !holdingPawn && !hudHeld : caught;
            Report(name, clean,
                $"잡힌 시간 {held:F2} s, 잡는 손이 폰에 {holdingPawn}, 잡힘 표시 {hudHeld}, 손 L:{a.handL.IsHolding} R:{a.handR.IsHolding} (팀 {Teams.Name(teamA)}/{Teams.Name(teamB)}, 폰 러쉬 {rush})");
            Drive(a, Vector3.zero);
            yield return Clear();
        }
    }
}
