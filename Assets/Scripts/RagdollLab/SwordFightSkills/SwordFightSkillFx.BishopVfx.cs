using System.Collections.Generic;
using ChessFight.Network;
using ChessFight.VFX;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The bishop's F and E effects over his realistic drawing (R167, 10-27, 사용자: "비숍은 거의 vfx가 없으니까 만들어줘
    /// 리얼리스틱하게 안해줘도 돼"; begun again for the X-cut F in R175), made layer by layer by the editor menu
    /// ChessFight/VFX/Bishop/Build (Assets/Scripts/VFXEditor/BishopVfxBuilder.cs) into Assets/VFX/Prefabs/Bishop, listed in
    /// Resources/VfxSets/Bishop. Until the menu has been run there is no set and nothing plays. So far: the second sword
    /// forming in his left hand as he calls the skill (F_Draw, as the game puts it there, R194), the light running
    /// up his two blades as he draws them back (F_Windup, on SfFxKind.Cast, R188), the flash at his hands and the air
    /// split as he lets them go (F_Throw, on SfFxKind.BishopLeap, R191), the two swords on their way out (F_Flight, also
    /// on SfFxKind.BishopLeap, R181), the X of light where they cross (F_Cross, on SfFxKind.BishopCross), the
    /// X cut on whoever it strikes (F_Hit, on SfFxKind.BishopHit, R178) and the swords on their way back (F_Return, also
    /// on SfFxKind.BishopCross, R186). Every screen draws them (they hang off the same moments).
    /// <see cref="BishopVfxOn"/> false leaves the realistic drawing alone.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        public static bool BishopVfxOn = true;

        GameObject BishopVfx(string key, Vector3 at, Vector3 forward) => BishopVfxOn ? PieceVfx("Bishop", key, at, forward) : null;

        /// <summary>The bishops holding a second sword (from F to the end of the cut), so each one's F1 plays once.</summary>
        readonly HashSet<SwordFightSkills> bishopDrawn = new HashSet<SwordFightSkills>();

        /// <summary>R194, each frame (Watch): as the game puts a second sword in a bishop's left hand (he has called his X
        /// cut; every machine, SwordFightSkills.OffBlade), its F1.</summary>
        void BishopDrawWatch(SwordFightSkills s)
        {
            if (s.Piece != PieceKind.Bishop || !s.OffBlade.HasValue)
            {
                bishopDrawn.Remove(s);
                return;
            }
            if (bishopDrawn.Add(s)) BishopDraw(s);
        }

        /// <summary>R194: small silver shards flying in to his empty left hand and landing on the second sword's blade from
        /// where it starts up to the tip, the part not yet formed still light, a glint riding the forming edge and flaring at
        /// the tip; following that blade (SwordFightPawn.DrawnBlade). The root goes on the floor where he stands, its +Z
        /// his way.</summary>
        void BishopDraw(SwordFightSkills s)
        {
            var fighter = s.Fighter;
            if (fighter == null) return;
            var go = BishopVfx("F_Draw", Kit.FloorUnder(s.Pawn.Hips.position, 1.5f), s.AimDir);
            if (go == null) return;
            foreach (var form in go.GetComponentsInChildren<VfxBladeForm>())
            {
                int hand = form.hand;
                form.Follow((out Vector3 guard, out Vector3 tip) =>
                {
                    guard = tip = Vector3.zero;
                    return fighter != null && fighter.DrawnBlade(hand, out guard, out tip);
                });
            }
        }

        /// <summary>R188: light running up each of his two blades as he draws them back over his shoulders, the tips
        /// brightest as he lets go, then dying away on the flying swords; each following its sword
        /// (SwordFightPawn.DrawnBlade: the one in his hand, then its thrown copy). The root goes on the floor where he
        /// stands, its +Z his way.</summary>
        void BishopWindup(SfFxEvent e)
        {
            var fighter = e.by != null ? e.by.Fighter : null;
            if (fighter == null) return;
            var go = BishopVfx("F_Windup", e.by.Origin, e.dir);
            if (go == null) return;
            float seconds = S != null ? S.bishopThrowWindup : 0.2f;
            foreach (var charge in go.GetComponentsInChildren<VfxBladeCharge>())
            {
                int hand = charge.hand;
                charge.Follow((out Vector3 guard, out Vector3 tip) =>
                {
                    guard = tip = Vector3.zero;
                    return fighter != null && fighter.DrawnBlade(hand, out guard, out tip);
                }, seconds);
            }
        }

        /// <summary>R191: as his two swords leave his hands, a crisp short star at each hand and a thin bright line
        /// splitting the air from there to its sword as it flies off, closing up into the sword; each from where his hand
        /// let go of it (SwordFightPawn.SwordHand), following its sword's drawn copy (SwordFightPawn.ThrownSwordOut). The
        /// root goes on the floor where he stands, its +Z his way.</summary>
        void BishopRelease(SfFxEvent e)
        {
            var fighter = e.by != null ? e.by.Fighter : null;
            if (fighter == null) return;
            var go = BishopVfx("F_Throw", e.by.Origin, e.dir);
            if (go == null) return;
            foreach (var release in go.GetComponentsInChildren<VfxSwordRelease>())
            {
                int hand = release.hand;
                release.Follow((out Vector3 middle, out Vector3 blade) =>
                {
                    middle = blade = Vector3.zero;
                    return fighter != null && fighter.ThrownSwordOut(hand, out middle, out blade);
                }, fighter.SwordHand(hand));
            }
        }

        /// <summary>R181: a blur round each thrown sword where it spins and a trail along its way, each following its sword's
        /// drawn copy (SwordFightPawn.ThrownSwordOut) until it turns into the X. The root goes on the floor where he stands,
        /// its +Z his way.</summary>
        void BishopFlight(SfFxEvent e)
        {
            var fighter = e.by != null ? e.by.Fighter : null;
            if (fighter == null) return;
            var go = BishopVfx("F_Flight", e.by.Origin, e.dir);
            if (go == null) return;
            float seconds = e.by.BishopOutTime;
            foreach (var flight in go.GetComponentsInChildren<VfxSwordFlight>())
            {
                int hand = flight.hand;
                flight.Follow((out Vector3 middle, out Vector3 blade) =>
                {
                    middle = blade = Vector3.zero;
                    return fighter != null && fighter.ThrownSwordOut(hand, out middle, out blade);
                }, seconds);
            }
        }

        /// <summary>R186: a thin swaying trail after each sword on its way back from the X to his hand, each following its
        /// sword's drawn copy (SwordFightPawn.ThrownSwordBack) from the end of its cut (half the cut after this moment)
        /// until it drops into his hand. The root goes on the floor where he stands, its +Z his way.</summary>
        void BishopReturn(SfFxEvent e)
        {
            var fighter = e.by != null ? e.by.Fighter : null;
            if (fighter == null) return;
            var go = BishopVfx("F_Return", e.by.Origin, e.dir);
            if (go == null) return;
            float seconds = Mathf.Max(0.1f, e.by.BishopThrowTime - e.by.StageTime);
            foreach (var flight in go.GetComponentsInChildren<VfxSwordFlight>())
            {
                int hand = flight.hand;
                flight.Follow((out Vector3 middle, out Vector3 blade) =>
                {
                    middle = blade = Vector3.zero;
                    return fighter != null && fighter.ThrownSwordBack(hand, out middle, out blade);
                }, seconds);
            }
        }
    }
}
